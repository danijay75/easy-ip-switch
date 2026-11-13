param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [ValidateSet('Azure', 'Pfx', 'Store')]
    [string]$Mode = 'Azure',

    [string]$PfxPath,
    [string]$PfxPassword,
    [string]$Thumbprint,
    [string]$TimestampUrl = 'http://timestamp.acs.microsoft.com',
    [string]$AzureDlib,
    [string]$AzureMetadata
)

$ErrorActionPreference = 'Stop'

function Resolve-SignTool {
    $command = Get-Command 'signtool.exe' -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $roots = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'),
        (Join-Path $env:ProgramFiles 'Windows Kits\10\bin')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    foreach ($root in $roots) {
        $versions = Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^10\.' } |
            Sort-Object { [version]$_.Name } -Descending
        foreach ($version in $versions) {
            $candidate = Join-Path $version.FullName 'x64\signtool.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                return $candidate
            }
        }
    }

    throw "signtool.exe introuvable. Installez le Windows SDK (Windows Kits 10) ou ajoutez signtool.exe au PATH."
}

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Fichier a signer introuvable : $Path"
}

$signTool = Resolve-SignTool
$arguments = @('sign', '/v', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256')

switch ($Mode) {
    'Azure' {
        if ([string]::IsNullOrWhiteSpace($AzureDlib)) {
            $AzureDlib = $env:AZURE_ARTIFACT_SIGNING_DLIB
        }
        if ([string]::IsNullOrWhiteSpace($AzureMetadata)) {
            $AzureMetadata = $env:AZURE_ARTIFACT_SIGNING_METADATA
        }
        if ([string]::IsNullOrWhiteSpace($AzureDlib) -or -not (Test-Path -LiteralPath $AzureDlib -PathType Leaf)) {
            throw "Dlib Azure Artifact Signing introuvable. Passez -AzureDlib ou definissez AZURE_ARTIFACT_SIGNING_DLIB."
        }
        if ([string]::IsNullOrWhiteSpace($AzureMetadata) -or -not (Test-Path -LiteralPath $AzureMetadata -PathType Leaf)) {
            throw "Metadonnees Azure Artifact Signing introuvables. Passez -AzureMetadata ou definissez AZURE_ARTIFACT_SIGNING_METADATA."
        }

        $metadata = Get-Content -LiteralPath $AzureMetadata -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($field in @('Endpoint', 'CodeSigningAccountName', 'CertificateProfileName')) {
            if ([string]::IsNullOrWhiteSpace([string]$metadata.$field)) {
                throw "Champ '$field' manquant dans $AzureMetadata."
            }
        }

        $arguments += @('/dlib', (Resolve-Path -LiteralPath $AzureDlib).Path)
        $arguments += @('/dmdf', (Resolve-Path -LiteralPath $AzureMetadata).Path)
    }
    'Pfx' {
        if ([string]::IsNullOrWhiteSpace($PfxPath) -or -not (Test-Path -LiteralPath $PfxPath -PathType Leaf)) {
            throw "Fichier PFX introuvable. Passez -PfxPath."
        }
        $arguments += @('/f', (Resolve-Path -LiteralPath $PfxPath).Path)
        if (-not [string]::IsNullOrEmpty($PfxPassword)) {
            $arguments += @('/p', $PfxPassword)
        }
    }
    'Store' {
        if ([string]::IsNullOrWhiteSpace($Thumbprint)) {
            throw "Empreinte du certificat manquante. Passez -Thumbprint."
        }
        $arguments += @('/sha1', ($Thumbprint -replace '\s', ''))
    }
}

$arguments += (Resolve-Path -LiteralPath $Path).Path

Write-Host "Signature de $Path (mode $Mode)..."
& $signTool @arguments
if ($LASTEXITCODE -ne 0) {
    throw "signtool sign a echoue (code $LASTEXITCODE)."
}

Write-Host "Verification de la signature..."
& $signTool verify /pa /v $Path
if ($LASTEXITCODE -ne 0) {
    throw "La verification de signature a echoue (code $LASTEXITCODE)."
}

$signature = Get-AuthenticodeSignature -LiteralPath $Path
if ($signature.Status -ne 'Valid') {
    throw "Signature non valide : $($signature.Status)."
}

Write-Host "Signature valide : $($signature.SignerCertificate.Subject)" -ForegroundColor Green
