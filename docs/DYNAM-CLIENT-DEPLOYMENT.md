# DYNAM Windows Companion deployment contract

## Standard for new Windows clients

`deployment/windows-companion-policy.json` declares **DYNAM Windows Companion**
the required default companion for new DYNAM Windows client deployments. DYNAM
provisions the client's isolated OpenClaw runtime and offers this branded app to
connect the PC to that runtime. The customer does not install a second local WSL
gateway for this hosted-runtime journey.

The checked-in policy starts at `releaseState: pending`, `selectedVersion: null`.
This means the standard is defined and rollout is blocked until the release and
tenant enrollment gates pass. A policy file does not itself change production
provisioning or establish a working native connection. The deployment adapter
must read and enforce it. Do not mark a client enrolled after only delivering a
download link, generating a setup code, or receiving a health check.

## Email or chat journey

1. After the isolated runtime is ready, send the customer the public, architecture
   appropriate DYNAM installer link and an authenticated DYNAM portal link.
2. The customer runs the installer, then signs in to the portal and selects
   **Connect this Windows PC** for their runtime.
3. The server verifies the authenticated user's workspace access, selects the
   runtime from its own database, and exchanges a short-lived, single-use
   invitation for a fresh limited OpenClaw setup code. An email or chat link may
   open this authenticated page. Opening the link alone must not redeem an
   invitation, because link scanners and previews may follow it.
4. Open the app's Connections page (`openclaw://settings`) and paste the code.
   The existing **Connect to an existing gateway** first-run choice also routes
   to Connections. Choose the setup-code input there. The registered protocol
   remains `openclaw` for upgrade compatibility.
5. Pair through the client's native WSS endpoint, then confirm both operator chat
   and a synthetic node file operation from the runtime. Restart the companion
   and confirm it reconnects using stored role device tokens.

No credential-bearing enrollment deep link is implemented in this release.
`openclaw://setup` opens the local setup wizard; it does not ingest a setup code.
Do not invent a `dynam://connect?token=...` route or add setup codes to query
strings, installer arguments, public manifests, release assets, email templates,
analytics, logs, or screenshots. Email and chat deliver links; the authenticated
portal reveals the short-lived code just before the customer uses it.

## Public release manifest

Release assets live in `DYNAM-CORP/dynam-windows-companion`. Publish the manifest
named `DYNAMWindowsCompanion-release.json` beside immutable installers:

- `DYNAMWindowsCompanion-Setup-x64.exe`
- `DYNAMWindowsCompanion-Setup-arm64.exe`, when that architecture passes proof
- SHA-256 checksums

The manifest uses `deployment/release-manifest.schema.json`: version, release
time, source commit, minimum gateway version, architecture, immutable installer
URL, hash, and DYNAM signing publisher/certificate thumbprint. It contains no
workspace identifiers or credentials. Public assets must download without a
GitHub account. A private repository's authenticated release URL cannot serve
this customer journey without a separate public artifact distribution path.

First candidate: companion `2026.9.27`, gateway minimum `2026.9.5`. The minimum
is a candidate compatibility floor based on the current test runtime, not a
claim that every newer runtime or all existing templates have passed. Do not
offer the release to an older template merely because it is healthy. Record
acceptance for the actual runtime image/version selected by provisioning.

The installer must pass a real Authenticode verification with the intended
DYNAM signing identity. Metadata declaring `signed` is not cryptographic proof.
Pin the accepted publisher/certificate in the release verification pipeline.
The SHA-256 hash must match downloaded bytes. A checksum authenticates bytes
only when obtained from the trusted DYNAM release channel. The updater must use
the DYNAM feed so an upstream update cannot replace the branding.

## Private enrollment contract

Keep PC enrollment separate from provider onboarding (`/agent-enrollment`).
The proposed enrollment purpose is `windows-companion`; portal/backend route
integration is an adapter requirement, not an endpoint implemented by this repo.

The server must:

- Authorize workspace access from the current authenticated session. Never trust
  caller-supplied workspace ownership, runtime URLs, readiness, or proof flags.
- Select the runtime and native admission URL from the workspace-scoped database.
- Require a released companion artifact and recorded native acceptance evidence.
- Store only a hash of an opaque invitation with purpose, workspace, runtime,
  expiry and redemption state. Expire within ten minutes. Redeem atomically once;
  rate limit issuance and redemption. Cancel invitations when access is revoked
  or the runtime changes. A replay must fail before minting another setup code.
- Mint the setup code inside the selected runtime using `openclaw qr --limited`
  and that runtime's verified public WSS URL. Do not reuse another client's code.
  Capture its secret output privately. Redeem the invitation before minting a
  fresh code, then report the code's embedded expiration exactly. The installed
  CLI issues a fixed ten-minute bootstrap and has no shorter TTL flag; ending
  the portal invitation does not shorten an already issued code.
- Return the private response described by
  `deployment/enrollment-response.schema.json` with cache disabled. Never place
  it in a shared cache. Audit safe issuance/redemption/device identifiers only.
- Verify that the decoded setup-code URL matches the selected runtime before
  returning it. The companion does not enforce DYNAM workspace ownership itself.
- Revoke operator and node device tokens when the device/client loses access.
  Revoke unused bootstrap credentials where the gateway supports it; otherwise
  retain the short expiry and block revoked native admission at the server.

The upstream setup code is base64url JSON
`{ "url": ..., "bootstrapToken": ..., "expiresAtMs": ... }`.
It is not encrypted. The opaque token carries server-enforced profiles; the code
does not reveal which profile was minted. The source decoder limits the code to
2048 characters and the token to 512 characters. The validator requires the
private response's `expiresAt` to equal the embedded millisecond expiry and be
within ten minutes. The upstream bootstrap is bound to the first device ID/public
key that uses it. Same-device retries and additional role handoff are permitted.
That binding is **not** an exactly-once invitation exchange. DYNAM must implement
the exchange above and prove expiry/replay behavior against the installed gateway
version.

## Operator, node and native admission

Chat uses the `operator` role; PC capability calls use the `node` role. Upstream
stores a per-gateway device key/identity with separate role device tokens. Do not
require separate device IDs or substitute an operator token for a node token.
Use the limited bootstrap profile, omit `operator.admin` and `operator.pairing`,
and preserve the existing gateway's approved capability/exec policy. Do not ship
a shared gateway token, provider API key, or runtime root SSH key to the client.

The portal dashboard proxy substitutes an operator identity and admits dashboard
chat/session traffic. It is not a generic native node admission path. Provide a
dedicated tenant-bound native WebSocket path that preserves the real signed
device handshake, nonce/challenge, role tokens, node invokes, and server-side
revocation. Bind its route to the selected runtime and verify cross-tenant denial.
An outbound WSS connection should suffice; customers should not open router
ports or receive infrastructure SSH credentials.

Pairing alone does not prove all PC capabilities. Browser proxy in the pinned
upstream currently requires a shared gateway token for its registration path.
That is excluded from this client contract; browser proxy remains unavailable
until upstream supports device-token registration or a separately reviewed,
isolated alternative. Operating local files through approved node commands and
desktop UI automation need independent success and denial proofs.

## Offline adapter checks

`deployment/companion_contract.py` is a dependency-free Python 3 validator and
plan builder for orchestration adapters. It rejects unsigned declarations,
mutable/upstream artifact URLs, unsupported architectures, older/unaccepted
runtimes, plaintext/private/credential-bearing endpoints, wrong workspace or
runtime responses, expired codes, and mismatched setup-code gateway URLs.
It makes no network calls and issues no credentials. It cannot prove access
control, token authenticity, Authenticode, endpoint reachability, or readiness.

From the repository root:

```powershell
python deployment/companion_contract.py validate-release path/to/release.json
python deployment/companion_contract.py plan path/to/release.json path/to/runtime.json --architecture x64
python -m unittest discover -s deployment -p "test_*.py" -v
```

The runtime plan input has exactly `workspaceId`, `runtimeId`, `gatewayVersion`,
`nativeGatewayUrl`, `state: "ready"`, and `companionReady: true`. These are a
server-owned projection after recorded acceptance gates pass, not a client body.
The plan returns a public download and an enrollment request. It does not carry
bootstrap credentials. For private response validation, call `validate_enrollment`
in process with the authorized workspace/runtime/URL, or pipe the private JSON
to `validate-enrollment - --workspace ... --runtime ... --gateway ...`.
Only safe static diagnostics are emitted. Do not place real codes in shell
arguments, command transcripts or checked-in fixtures.

The plan command reads the checked-in default policy and fails while it remains
pending, or if the manifest differs from its promoted version. An adapter can
provide its reviewed policy object to `deployment_plan` in process. Promotion
must come from the orchestrator's trusted configuration, never a request body.

## Promotion and evidence

Every gate in `requiredReleaseGates` must have an artifact or reproducible receipt
for the exact companion commit, installer hash and runtime image. Include fresh
install, preserving existing pairing during upgrade, clean uninstall, visible
gold-D branding, native chat, node file operation, denied unapproved operation,
cross-tenant/runtime denial, expiry/replay, token revocation and restart reconnect.
Use synthetic client files and isolated test tenants. A green build or canary
runtime does not establish this whole journey.

Only after these gates pass may the orchestrator set the policy to `released`,
pin `selectedVersion`, wire the default portal/provisioning adapter, and offer
the companion to new Windows clients. Record that deployment separately from
source/build proof. Existing working laptop connections must remain intact
through an in-place upgrade. Keep rollback installer/source and preserve the
upstream MIT license and attribution in distributed source and packages.
