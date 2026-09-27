"""Offline contract validation for the DYNAM deployment adapter.

These checks validate metadata and binding. The server remains responsible for
authorization, native admission, token issuance, single-use redemption and proofs.
No network calls, installation, credential issuance, or credential output occur.
"""

import argparse
import base64
import ipaddress
import json
import re
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from urllib.parse import urlsplit

PRODUCT = "DYNAM Windows Companion"
REPOSITORY = "DYNAM-CORP/dynam-windows-companion"
LIFETIME = timedelta(minutes=10)


class ContractError(ValueError):
    """Safe, static error text suitable for adapter diagnostics."""


def require(condition, message):
    if not condition:
        raise ContractError(message)


def fields(value, expected, label):
    require(isinstance(value, dict) and set(value) == set(expected),
            f"Invalid {label} fields")


def version(value):
    require(isinstance(value, str) and re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", value),
            "Invalid release or gateway version")
    return tuple(int(part) for part in value.split("."))


def timestamp(value):
    require(isinstance(value, str) and re.fullmatch(
        r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]+)?(?:Z|[+-][0-9]{2}:[0-9]{2})",
        value), "Invalid expiration or release timestamp")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        raise ContractError("Invalid expiration or release timestamp") from None
    require(parsed.tzinfo is not None, "Timestamp must include a timezone")
    return parsed.astimezone(timezone.utc)


def validate_release(manifest):
    fields(manifest, ("schemaVersion", "product", "channel", "version", "releasedAt",
                      "source", "minimumGatewayVersion", "artifacts"), "release")
    require(type(manifest["schemaVersion"]) is int and manifest["schemaVersion"] == 1 and manifest["product"] == PRODUCT
            and manifest["channel"] == "stable", "Unsupported companion release")
    version(manifest["version"])
    version(manifest["minimumGatewayVersion"])
    timestamp(manifest["releasedAt"])
    fields(manifest["source"], ("repository", "commit"), "source")
    require(manifest["source"]["repository"] == REPOSITORY, "Unexpected source repository")
    require(isinstance(manifest["source"]["commit"], str)
            and re.fullmatch(r"[a-f0-9]{40}", manifest["source"]["commit"]),
            "Invalid source commit")
    artifacts = manifest["artifacts"]
    require(isinstance(artifacts, list) and 1 <= len(artifacts) <= 2,
            "Release must include one or two architecture artifacts")
    seen = set()
    for artifact in artifacts:
        fields(artifact, ("architecture", "installerUrl", "sha256", "signing"), "artifact")
        architecture = artifact["architecture"]
        require(architecture in ("x64", "arm64") and architecture not in seen,
                "Invalid or duplicate architecture")
        seen.add(architecture)
        expected_url = (f"https://github.com/{REPOSITORY}/releases/download/"
                        f"v{manifest['version']}/DYNAMWindowsCompanion-Setup-{architecture}.exe")
        require(artifact["installerUrl"] == expected_url,
                "Installer must use the immutable DYNAM release URL")
        require(isinstance(artifact["sha256"], str)
                and re.fullmatch(r"[a-f0-9]{64}", artifact["sha256"]), "Invalid installer hash")
        signing = artifact["signing"]
        fields(signing, ("status", "publisher", "certificateThumbprint"), "signing")
        require(signing["status"] == "signed", "Unsigned installers cannot enter the client channel")
        require(isinstance(signing["publisher"], str) and signing["publisher"].strip(),
                "Missing signing publisher")
        require(isinstance(signing["certificateThumbprint"], str)
                and re.fullmatch(r"(?:[a-fA-F0-9]{40}|[a-fA-F0-9]{64})",
                                 signing["certificateThumbprint"]), "Invalid certificate thumbprint")
    return manifest


def public_gateway(value):
    require(isinstance(value, str) and len(value) <= 2048
            and not any(char.isspace() or ord(char) < 32 for char in value),
            "Invalid native gateway URL")
    try:
        uri = urlsplit(value)
        port = uri.port
    except ValueError:
        raise ContractError("Invalid native gateway URL") from None
    require(uri.scheme == "wss" and uri.hostname and not uri.username
            and not uri.password and not uri.query and not uri.fragment,
            "Native gateway requires a credential-free WSS URL")
    hostname = uri.hostname.lower()
    require(hostname != "localhost" and not hostname.endswith((".localhost", ".local")),
            "Native gateway must have a public endpoint")
    try:
        address = ipaddress.ip_address(hostname)
    except ValueError:
        require("." in hostname and not any(char.isspace() for char in hostname),
                "Native gateway must have a public hostname")
    else:
        require(address.is_global, "Native gateway must have a public endpoint")
    require(port is None or 1 <= port <= 65535, "Invalid native gateway port")
    return value


def deployment_plan(manifest, runtime, architecture, policy=None):
    """Build the adapter request only after external readiness gates pass.

    The authenticated server must obtain runtime from its own workspace-scoped
    database query. Do not use caller-supplied runtime ownership or evidence.
    """
    validate_release(manifest)
    policy = policy if policy is not None else load_json(Path(__file__).with_name("windows-companion-policy.json"))
    require(isinstance(policy, dict) and policy.get("schemaVersion") == 1
            and policy.get("product") == PRODUCT and policy.get("platform") == "windows"
            and policy.get("repository") == REPOSITORY and policy.get("requiredForNewDeployments") is True,
            "Invalid default Windows deployment policy")
    require(policy.get("releaseState") == "released"
            and policy.get("selectedVersion") == manifest["version"],
            "Companion release promotion gates have not passed")
    fields(runtime, ("workspaceId", "runtimeId", "gatewayVersion", "nativeGatewayUrl",
                     "state", "companionReady"), "runtime")
    require(runtime["state"] == "ready" and runtime["companionReady"] is True,
            "Runtime companion acceptance gates have not passed")
    for name in ("workspaceId", "runtimeId"):
        require(isinstance(runtime[name], str) and 1 <= len(runtime[name]) <= 128,
                "Invalid runtime ownership metadata")
    require(version(runtime["gatewayVersion"]) >= version(manifest["minimumGatewayVersion"]),
            "Gateway is older than this companion release supports")
    public_gateway(runtime["nativeGatewayUrl"])
    artifact = next((item for item in manifest["artifacts"]
                     if item["architecture"] == architecture), None)
    require(artifact is not None, "No release artifact for this machine architecture")
    return {
        "product": PRODUCT,
        "version": manifest["version"],
        "download": artifact,
        "enrollmentRequest": {
            "purpose": "windows-companion", "runtimeId": runtime["runtimeId"],
            "architecture": architecture, "releaseVersion": manifest["version"]
        },
        "settingsDeepLink": "openclaw://settings",
        "instructions": "Install the companion, sign in to the DYNAM portal, then paste the fresh setup code in Connections."
    }


def validate_enrollment(response, workspace_id, runtime_id, gateway_url, now=None):
    """Check a private response against the server-selected runtime.

    Does not verify gateway token authenticity or consume it. Never log response.
    """
    fields(response, ("schemaVersion", "purpose", "workspaceId", "runtimeId", "gatewayUrl",
                      "setupCode", "expiresAt"), "enrollment")
    require(type(response["schemaVersion"]) is int and response["schemaVersion"] == 1 and response["purpose"] == "windows-companion",
            "Invalid enrollment purpose")
    require(response["workspaceId"] == workspace_id and response["runtimeId"] == runtime_id,
            "Enrollment does not match the authorized workspace and runtime")
    public_gateway(gateway_url)
    require(response["gatewayUrl"] == gateway_url, "Enrollment gateway does not match the selected runtime")
    current = now or datetime.now(timezone.utc)
    expires = timestamp(response["expiresAt"])
    require(current < expires <= current + LIFETIME, "Enrollment is expired or exceeds ten minutes")
    code = response["setupCode"]
    require(isinstance(code, str) and 1 <= len(code) <= 2048
            and re.fullmatch(r"[A-Za-z0-9_-]+", code), "Invalid setup code encoding")
    try:
        payload = json.loads(base64.urlsafe_b64decode(code + "=" * (-len(code) % 4)))
    except (ValueError, UnicodeDecodeError):
        raise ContractError("Invalid setup code encoding") from None
    fields(payload, ("url", "bootstrapToken"), "setup code")
    require(payload["url"] == gateway_url, "Setup code gateway does not match the selected runtime")
    require(isinstance(payload["bootstrapToken"], str)
            and 1 <= len(payload["bootstrapToken"]) <= 512, "Invalid bootstrap credential")
    return {"valid": True, "purpose": "windows-companion", "expiresAt": response["expiresAt"]}


def load_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    release = sub.add_parser("validate-release")
    release.add_argument("manifest")
    plan = sub.add_parser("plan")
    plan.add_argument("manifest")
    plan.add_argument("runtime")
    plan.add_argument("--architecture", choices=("x64", "arm64"), required=True)
    enrollment = sub.add_parser("validate-enrollment")
    enrollment.add_argument("response", help="Private JSON file path, or - to read standard input")
    enrollment.add_argument("--workspace", required=True)
    enrollment.add_argument("--runtime", required=True)
    enrollment.add_argument("--gateway", required=True, help="Expected public WSS URL without credentials")
    args = parser.parse_args()
    try:
        if args.command == "validate-release":
            validate_release(load_json(args.manifest))
            result = {"valid": True, "product": PRODUCT}
        elif args.command == "plan":
            result = deployment_plan(load_json(args.manifest), load_json(args.runtime), args.architecture)
        else:
            response = json.load(sys.stdin) if args.response == "-" else load_json(args.response)
            result = validate_enrollment(response, args.workspace, args.runtime, args.gateway)
        print(json.dumps(result, indent=2))
        return 0
    except (ContractError, OSError, ValueError, TypeError) as error:
        # File/JSON exception text can include private source content or local paths.
        message = str(error) if isinstance(error, ContractError) else "Invalid or unreadable contract input"
        print(json.dumps({"valid": False, "error": message}), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
