# DYNAM companion local pilot evidence

Date: 2026-09-26. Upstream base: v2026.9.4,
`3c43751b2bace876de3febe478ebabeca172e3ac`.
Candidate app version: `2026.9.27`. This is an unsigned local source pilot,
not a published client installer or production deployment.

## Verified locally

- Full build.ps1 build succeeded: Shared, CLI, WinNode CLI, SetupEngine and WinUI.
- x64 self-contained WinUI build succeeded with zero errors.
- Native window title is **DYNAM Windows Companion**, with the existing gold D
  logo in the title bar and assistant avatar.
- The existing gateway record and paired operator connection survived the upgrade.
  The test gateway reported OpenClaw `2026.9.5`.
- A fresh message in a synthetic test conversation returned exactly
  `DYNAM_WINDOWS_COMPANION_OK` in the branded native chat.
- The laptop's pre-existing CLI node remained paired and connected. Companion
  node mode remains disabled on this pilot to preserve that existing setup.
- Local screenshot and private rollback data were retained outside the repository.

## Automated evidence

- Deployment contract tests: 7 test groups passed.
- Signing configuration tests: 21 synthetic checks passed; no actual signing.
- Tray tests: 2,978 passed, zero failed. The laptop has no Visual Studio C++
  Redistributable component, so the build initially copied an obsolete 14.29
  fallback. For this local test, current Microsoft runtime 14.51 DLLs were copied
  from Windows System32 into the test/build/pilot outputs. The second test run
  used `--no-build --no-restore` so the fallback was not copied back. Production
  publishing still requires the repository's Visual Studio runtime/signing gates.
- Shared tests: 3,984 passed, 32 explicitly skipped, zero failed, through a source
  directory junction with no spaces in its path. Three unchanged command-binding
  tests initially failed because they embed an unquoted host path containing
  spaces. The same compiled tests passed via the short path; no binder changed.
- Documentation validator: 50 documents passed before this evidence file.
- CI workflow contracts, stable correction regression and whitespace checks passed.
- Rubber-duck review found outdated brand assertions and signing identity
  validation; those findings were corrected and checked.

## Commands

```powershell
dotnet build src/OpenClaw.Tray.WinUI -c Release -r win-x64 --self-contained -p:Platform=x64 -p:Version=2026.9.27 -p:InformationalVersion=2026.9.27
dotnet test tests/OpenClaw.Tray.Tests/OpenClaw.Tray.Tests.csproj -c Release --no-build --no-restore
dotnet test <short-source-path>/tests/OpenClaw.Shared.Tests/OpenClaw.Shared.Tests.csproj -c Release --no-build --no-restore
python -m unittest discover -s deployment -p 'test_*.py'
./scripts/Test-DynamSigningConfiguration.Tests.ps1
./scripts/test-stable-correction-release-validator.ps1
./scripts/test-ci-workflow-contract.ps1
./scripts/validate-docs.ps1
```

`OPENCLAW_REPO_ROOT` pointed to this source checkout; tray tests used an isolated
`OPENCLAW_TRAY_DATA_DIR`. The local full build uses `build.ps1 -Configuration
Release -WindowsSdkRoot <extracted-Microsoft-Windows-SDK-root>` after the
machine-wide SDK installer failed. That option verifies matching makepri tools
and Windows.winmd metadata and supplies WindowsSdkDir to MSBuild.

## Not established by this pilot

Signed public installers, ARM64 acceptance, fresh-client install/uninstall,
tenant-specific native admission, portal invitation redemption, cross-tenant
rejection, native browser proxy without a shared token, and arbitrary application
UI automation are not verified. The default client deployment policy remains
pending until its release and enrollment gates pass.

The pilot also reports the upstream MXC sandbox host-fallback notification on this
laptop. This branding change does not change sandbox behavior or execution policy.
