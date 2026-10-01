[CmdletBinding()]
param(
    [string]$Endpoint = '',
    [string]$Account = '',
    [string]$Profile = '',
    [string]$SignerSubject = '',
    [switch]$SubjectOnly
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SignerSubject)) {
    throw 'DYNAM signing requires a nonempty certificate subject.'
}
if ($SignerSubject -match 'OpenClaw') {
    throw 'The upstream OpenClaw certificate subject cannot sign a DYNAM release.'
}
if ($SignerSubject -notmatch 'DYNAM') {
    throw 'The signing certificate subject must identify DYNAM.'
}

if (-not $SubjectOnly) {
    foreach ($setting in @(
        @{ Name = 'endpoint'; Value = $Endpoint },
        @{ Name = 'account'; Value = $Account },
        @{ Name = 'profile'; Value = $Profile }
    )) {
        if ([string]::IsNullOrWhiteSpace($setting.Value)) {
            throw "DYNAM signing requires a nonempty $($setting.Name)."
        }
    }
    if ($Account.Trim() -ieq 'openclaw' -or $Profile.Trim() -ieq 'openclaw') {
        throw 'The upstream OpenClaw signing account or profile cannot sign a DYNAM release.'
    }
}

# Configuration preflight only. The release pipeline separately verifies the
# actual Authenticode signature and exact subject on each produced installer.
[pscustomobject]@{
    Valid = $true
    Mode = $(if ($SubjectOnly) { 'subject-only' } else { 'release' })
}
