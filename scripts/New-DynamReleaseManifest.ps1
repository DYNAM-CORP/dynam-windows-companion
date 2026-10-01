[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$SourceCommit,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$SignerSubject,
    [string]$MinimumGatewayVersion = '2026.9.5',
    [string]$InstallerDirectory = 'Output'
)
$ErrorActionPreference = 'Stop'
$null = & (Join-Path $PSScriptRoot 'Test-DynamSigningConfiguration.ps1') -SignerSubject $SignerSubject -SubjectOnly
$artifacts = foreach ($architecture in @('x64', 'arm64')) {
    $name = "DYNAMWindowsCompanion-Setup-$architecture.exe"
    $path = Join-Path $InstallerDirectory $name
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -ne $SignerSubject) {
        throw "Installer signing verification failed: $name"
    }
    [ordered]@{
        architecture = $architecture
        installerUrl = "https://github.com/DYNAM-CORP/dynam-windows-companion/releases/download/v$Version/$name"
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        signing = [ordered]@{
            status = 'signed'
            publisher = $SignerSubject
            certificateThumbprint = $signature.SignerCertificate.Thumbprint
        }
    }
}
$manifest = [ordered]@{
    schemaVersion = 1
    product = 'DYNAM Windows Companion'
    channel = 'stable'
    version = $Version
    releasedAt = [DateTimeOffset]::UtcNow.ToString('o')
    source = [ordered]@{ repository = 'DYNAM-CORP/dynam-windows-companion'; commit = $SourceCommit }
    minimumGatewayVersion = $MinimumGatewayVersion
    artifacts = @($artifacts)
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $InstallerDirectory 'DYNAMWindowsCompanion-release.json') -Encoding utf8
$artifacts | ForEach-Object { "$($_.sha256)  DYNAMWindowsCompanion-Setup-$($_.architecture).exe" } |
    Set-Content -LiteralPath (Join-Path $InstallerDirectory 'SHA256SUMS') -Encoding ascii
