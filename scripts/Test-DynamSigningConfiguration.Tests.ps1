[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$preflight = Join-Path $PSScriptRoot 'Test-DynamSigningConfiguration.ps1'
$good = @{
    Endpoint = 'https://synthetic-signing.example.com'
    Account = 'dynam-synthetic-account'
    Profile = 'dynam-synthetic-profile'
    SignerSubject = 'CN=DYNAM Synthetic Publisher, O=DYNAM Synthetic'
}
$passed = 0

function Assert-Rejected {
    param([hashtable]$Arguments, [string]$Case)
    $rejected = $false
    try {
        $null = & $preflight @Arguments
    }
    catch {
        $rejected = $true
    }
    if (-not $rejected) {
        throw "Expected signing configuration rejection: $Case"
    }
}

$result = & $preflight @good
if ($result.Valid -ne $true -or $result.Mode -ne 'release') {
    throw 'Valid synthetic DYNAM release configuration was not accepted.'
}
$passed++

$result = & $preflight -SignerSubject $good.SignerSubject -SubjectOnly
if ($result.Valid -ne $true -or $result.Mode -ne 'subject-only') {
    throw 'Subject-only validation unexpectedly requires Azure configuration.'
}
$passed++

foreach ($name in @('Endpoint', 'Account', 'Profile', 'SignerSubject')) {
    foreach ($empty in @('', '   ')) {
        $arguments = $good.Clone()
        $arguments[$name] = $empty
        Assert-Rejected -Arguments $arguments -Case "$name is empty"
        $passed++
    }
}

foreach ($name in @('Account', 'Profile')) {
    foreach ($upstream in @('openclaw', ' OpenClaw ')) {
        $arguments = $good.Clone()
        $arguments[$name] = $upstream
        Assert-Rejected -Arguments $arguments -Case "$name is the upstream identity"
        $passed++
    }
}

foreach ($subject in @('', '   ', 'CN=OpenClaw', 'CN=DYNAM OpenClaw', 'CN=Other Publisher')) {
    Assert-Rejected -Arguments @{ SignerSubject = $subject; SubjectOnly = $true } -Case 'subject-only identity boundary'
    $passed++
}

$generator = Join-Path $PSScriptRoot 'New-DynamReleaseManifest.ps1'
$missingInstallers = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString())
foreach ($subject in @('CN=OpenClaw', 'CN=Other Publisher')) {
    $preflightRejected = $false
    try {
        $null = & $generator -Version '2026.9.27' -SourceCommit ('a' * 40) -SignerSubject $subject -InstallerDirectory $missingInstallers
    }
    catch {
        $preflightRejected = $_.Exception.Message -match 'certificate subject cannot sign|certificate subject must identify DYNAM'
    }
    if (-not $preflightRejected) {
        throw 'Manifest generation must reject the signing identity before reading installer files.'
    }
    $passed++
}

[pscustomobject]@{
    Passed = $passed
    ActualSigningPerformed = $false
}
