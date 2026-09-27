import base64
import copy
import json
import unittest
from datetime import datetime, timedelta, timezone

from companion_contract import ContractError, deployment_plan, validate_enrollment, validate_release


def release_fixture():
    # Synthetic metadata only. These hashes and signatures are not release evidence.
    return {
        "schemaVersion": 1, "product": "DYNAM Windows Companion", "channel": "stable",
        "version": "2026.9.27", "releasedAt": "2026-09-27T00:00:00Z",
        "source": {"repository": "DYNAM-CORP/dynam-windows-companion", "commit": "a" * 40},
        "minimumGatewayVersion": "2026.9.5",
        "artifacts": [{
            "architecture": "x64",
            "installerUrl": "https://github.com/DYNAM-CORP/dynam-windows-companion/releases/download/v2026.9.27/DYNAMWindowsCompanion-Setup-x64.exe",
            "sha256": "b" * 64,
            "signing": {"status": "signed", "publisher": "DYNAM Synthetic test publisher",
                        "certificateThumbprint": "c" * 40}
        }]
    }


def runtime_fixture():
    return {
        "workspaceId": "synthetic-workspace", "runtimeId": "synthetic-runtime",
        "gatewayVersion": "2026.9.5", "nativeGatewayUrl": "wss://native.example.com/runtime",
        "state": "ready", "companionReady": True
    }


def promoted_policy_fixture():
    return {"schemaVersion": 1, "product": "DYNAM Windows Companion", "platform": "windows",
            "repository": "DYNAM-CORP/dynam-windows-companion", "requiredForNewDeployments": True,
            "releaseState": "released", "selectedVersion": "2026.9.27"}


class ReleaseTests(unittest.TestCase):
    def test_valid_plan_binds_release_and_selected_runtime_without_credentials(self):
        plan = deployment_plan(release_fixture(), runtime_fixture(), "x64", promoted_policy_fixture())
        self.assertEqual("synthetic-runtime", plan["enrollmentRequest"]["runtimeId"])
        self.assertEqual("2026.9.27", plan["enrollmentRequest"]["releaseVersion"])
        self.assertEqual("openclaw://settings", plan["settingsDeepLink"])
        self.assertNotIn("Token", json.dumps(plan))

    def test_checked_in_pending_policy_and_different_selected_version_block_plans(self):
        with self.assertRaisesRegex(ContractError, "promotion gates"):
            deployment_plan(release_fixture(), runtime_fixture(), "x64")
        policy = promoted_policy_fixture()
        policy["selectedVersion"] = "2026.9.26"
        with self.assertRaisesRegex(ContractError, "promotion gates"):
            deployment_plan(release_fixture(), runtime_fixture(), "x64", policy)

    def test_upstream_latest_unsigned_and_duplicate_architecture_are_rejected(self):
        for mutation in ("upstream", "latest", "unsigned", "foreign-publisher", "upstream-publisher", "duplicate", "bad-hash", "secret-field"):
            with self.subTest(mutation=mutation):
                release = release_fixture()
                artifact = release["artifacts"][0]
                if mutation == "upstream":
                    artifact["installerUrl"] = artifact["installerUrl"].replace("DYNAM-CORP/dynam-windows-companion", "openclaw/openclaw-windows-node")
                elif mutation == "latest":
                    artifact["installerUrl"] = artifact["installerUrl"].replace("download/v2026.9.27", "latest/download")
                elif mutation == "unsigned":
                    artifact["signing"]["status"] = "unsigned"
                elif mutation == "foreign-publisher":
                    artifact["signing"]["publisher"] = "CN=Synthetic Other Publisher"
                elif mutation == "upstream-publisher":
                    artifact["signing"]["publisher"] = "CN=OpenClaw Foundation, O=DYNAM Synthetic"
                elif mutation == "duplicate":
                    release["artifacts"].append(copy.deepcopy(artifact))
                elif mutation == "bad-hash":
                    artifact["sha256"] = "not-a-hash"
                else:
                    release["bootstrapToken"] = "synthetic-secret"
                with self.assertRaises(ContractError):
                    validate_release(release)

    def test_old_runtime_unverified_admission_and_missing_architecture_fail_closed(self):
        cases = [("gatewayVersion", "2026.8.2"), ("companionReady", False),
                 ("state", "provisioning"), ("nativeGatewayUrl", "ws://127.0.0.1:18790")]
        for field, value in cases:
            with self.subTest(field=field):
                runtime = runtime_fixture()
                runtime[field] = value
                with self.assertRaises(ContractError):
                    deployment_plan(release_fixture(), runtime, "x64", promoted_policy_fixture())
        with self.assertRaises(ContractError):
            deployment_plan(release_fixture(), runtime_fixture(), "arm64", promoted_policy_fixture())

    def test_private_and_credential_bearing_native_urls_fail_closed(self):
        for url in ("wss://127.0.0.1", "wss://192.168.1.1", "wss://localhost",
                    "wss://pc.local", "wss://user:secret@native.example.com",
                    "wss://native.example.com?token=synthetic-secret", "wss://[::1]"):
            with self.subTest(url=url):
                runtime = runtime_fixture()
                runtime["nativeGatewayUrl"] = url
                with self.assertRaises(ContractError):
                    deployment_plan(release_fixture(), runtime, "x64", promoted_policy_fixture())


class EnrollmentTests(unittest.TestCase):
    def setUp(self):
        self.now = datetime(2026, 9, 27, tzinfo=timezone.utc)
        self.gateway = "wss://native.example.com/runtime"
        code = base64.urlsafe_b64encode(json.dumps({
            "url": self.gateway, "bootstrapToken": "synthetic-bootstrap-credential"
        }).encode()).decode().rstrip("=")
        self.response = {
            "schemaVersion": 1, "purpose": "windows-companion",
            "workspaceId": "synthetic-workspace", "runtimeId": "synthetic-runtime",
            "gatewayUrl": self.gateway, "setupCode": code,
            "expiresAt": (self.now + timedelta(minutes=5)).isoformat()
        }

    def validate(self):
        return validate_enrollment(self.response, "synthetic-workspace", "synthetic-runtime",
                                   self.gateway, self.now)

    def test_valid_enrollment_returns_only_safe_diagnostics(self):
        result = self.validate()
        self.assertTrue(result["valid"])
        self.assertNotIn("setupCode", result)
        self.assertNotIn("bootstrap", json.dumps(result))

    def test_wrong_tenant_runtime_purpose_expiration_and_tampered_code_are_rejected(self):
        mutations = [("workspaceId", "other-workspace"), ("runtimeId", "other-runtime"),
                     ("purpose", "provider"), ("gatewayUrl", "wss://other.example.com"),
                     ("expiresAt", self.now.isoformat()),
                     ("expiresAt", (self.now + timedelta(minutes=11)).isoformat()),
                     ("setupCode", "malformed")]
        for field, value in mutations:
            with self.subTest(field=field, value=value):
                original = self.response[field]
                self.response[field] = value
                with self.assertRaises(ContractError):
                    self.validate()
                self.response[field] = original
        self.response["setupCode"] = base64.urlsafe_b64encode(json.dumps({
            "url": "wss://other.example.com/runtime", "bootstrapToken": "synthetic"
        }).encode()).decode().rstrip("=")
        with self.assertRaises(ContractError):
            self.validate()


if __name__ == "__main__":
    unittest.main()
