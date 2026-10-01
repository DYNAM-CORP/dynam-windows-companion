[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$installerPath = Join-Path $repoRoot "installer.iss"
$licensePath = Join-Path $repoRoot "LICENSE"
$installer = Get-Content -LiteralPath $installerPath -Raw
$license = Get-Content -LiteralPath $licensePath -Raw

if ($license -notmatch "(?m)^MIT License\s*$") {
    throw "Repository LICENSE is not the expected MIT license."
}

if ($installer -notmatch '(?m)^Source: "LICENSE"; DestDir: "\{app\}"; Flags: ignoreversion\s*$') {
    throw "installer.iss must install LICENSE into the application directory."
}

Write-Host "Inno license packaging checks passed."
