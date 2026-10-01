# DYNAM Windows Companion release

This product is derived from OpenClaw Windows Node v2026.9.4, commit
`3c43751b2bace876de3febe478ebabeca172e3ac`. Retain the upstream MIT license and
copyright notices. DYNAM supplies branding and its own release channel.

## Distribution

The public product repository is `DYNAM-CORP/dynam-windows-companion`.
Installers are `DYNAMWindowsCompanion-Setup-x64.exe` and
`DYNAMWindowsCompanion-Setup-arm64.exe`; portable update assets contain the RID
`win-x64` or `win-arm64`. The app checks this DYNAM repository for updates.
An immutable tagged release publishes `DYNAMWindowsCompanion-release.json` and `SHA256SUMS`.
Never attach a client's connection code or credentials to these public assets.

The first intended stable version is `2026.9.27`. A pending default policy
does not mean an installer has been published or that automatic enrollment is live.

## Signing configuration

Configure the GitHub `release-signing` environment with DYNAM's own Azure
Artifact Signing identity. Required secrets: `AZURE_CLIENT_ID`,
`AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`. Required variables:
`DYNAM_SIGNING_ENDPOINT`, `DYNAM_SIGNING_ACCOUNT`, `DYNAM_SIGNING_PROFILE`,
and the exact certificate subject `DYNAM_SIGNER_SUBJECT`.
Use OIDC and scope the Azure trust to this repository and environment.
Missing signing configuration blocks the release. Do not use OpenClaw's
publisher identity or claim its signature covers modified binaries.

The release preflight is `scripts/Test-DynamSigningConfiguration.ps1` with
`-Endpoint`, `-Account`, `-Profile`, and `-SignerSubject`. All four must be
nonempty. It rejects the known upstream account/profile `openclaw`, a subject
containing `OpenClaw`, and a subject that does not identify `DYNAM`. The manifest
generator calls the same helper with `-SubjectOnly` before reading installer
signatures. This checks configuration; it does not perform signing or replace
the generator's verification of a valid signature and exact certificate subject.
Focused synthetic checks: `./scripts/Test-DynamSigningConfiguration.Tests.ps1`.

## Build and publish

1. Use the repository SDK from `global.json`, the Windows SDK and the C++
   Redistributable Update component required by `build.ps1`.
2. Run `./build.ps1 -Configuration Release` and the shared/tray test projects
   listed in `AGENTS.md`. Use an isolated test data directory.
3. Verify the current build's title, gold D logo, tray entry, preserved pairing,
   chat response and native node behavior against the supported gateway.
4. Run the existing PR CI. Merge the reviewed candidate, then tag `v2026.9.27`
   only after its proof and signing configuration are ready.
5. The existing CI signs application binaries, checks native dependencies,
   builds and signs both installers, validates signatures and publishes the
   public manifest. A manifest alone does not prove tenant enrollment.
6. Apply the deployment policy only after the runtime and enrollment gates in
   `DYNAM-CLIENT-DEPLOYMENT.md` have passed.

## Existing installation

Internal executable, protocol, AUMID, mutex, data directory and scheduled task
identifiers remain compatible with the upstream installer. The branded installer
upgrades that installation in place. Do not run an upstream updater against the
branded payload. Keep a private backup of the existing installation/profile for
a local pilot; never commit pairing state or private keys.

MSIX Store distribution is paused upstream and is not the DYNAM distribution
path. Preserved MSIX internal identities are compatibility metadata, not a
claim that DYNAM owns the upstream Store listing.

## Repository automation

The inherited daily alpha release, Clawsweeper dispatch, repository triage and
stale issue workflows are disabled in the DYNAM repository. Keep them disabled
unless DYNAM deliberately adopts their responsibilities; Clawsweeper dispatch
addresses an upstream OpenClaw repository. The DYNAM CI and CodeQL remain enabled.
The reusable release-candidate workflow retains upstream reference semantics and
is not the production DYNAM client enrollment proof.

For a local managed WinUI build with an administratively extracted Microsoft
Windows SDK, build.ps1 accepts `-WindowsSdkRoot <Windows-Kits/10-root>`. It verifies
makepri.exe and Windows.winmd from matching SDK versions and passes WindowsSdkDir
to MSBuild. A machine-wide SDK install remains the default. This does not replace
the Visual Studio VC runtime requirements for publishing.
