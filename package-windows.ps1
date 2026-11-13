param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$OutputDirectory,
    [string]$WebsiteDirectory,
    [string]$WebsiteReleaseJson,
    [string]$TemplatePath
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'dist'
}
if ([string]::IsNullOrWhiteSpace($TemplatePath)) {
    $TemplatePath = Join-Path $root 'packaging\windows\README.txt.template'
}

if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
    throw "Executable introuvable : $ExePath"
}
if (-not (Test-Path -LiteralPath $TemplatePath -PathType Leaf)) {
    throw "Modele README introuvable : $TemplatePath"
}

$exeItem = Get-Item -LiteralPath $ExePath
$sha256 = (Get-FileHash -LiteralPath $exeItem.FullName -Algorithm SHA256).Hash
$exeName = "easy-ip-switch-$Version-windows.exe"
$zipName = "easy-ip-switch-$Version-windows.zip"

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputExe = Join-Path $OutputDirectory $exeName
$zipPath = Join-Path $OutputDirectory $zipName
$checksumsPath = Join-Path $OutputDirectory 'SHA256SUMS.txt'

if ([IO.Path]::GetFullPath($exeItem.FullName) -ne [IO.Path]::GetFullPath($outputExe)) {
    Copy-Item -LiteralPath $exeItem.FullName -Destination $outputExe -Force
}

$template = Get-Content -LiteralPath $TemplatePath -Raw -Encoding UTF8
$readme = $template.Replace('{{VERSION}}', $Version).Replace('{{EXE_NAME}}', $exeName).Replace('{{SHA256}}', $sha256)

$stagingRoot = Join-Path ([IO.Path]::GetTempPath()) ('EasyIpSwitchPackage-' + [Guid]::NewGuid().ToString('N'))
$packageDir = Join-Path $stagingRoot ("easy-ip-switch-$Version-windows")

try {
    New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
    Copy-Item -LiteralPath $outputExe -Destination (Join-Path $packageDir $exeName) -Force
    [IO.File]::WriteAllText((Join-Path $packageDir 'README.txt'), $readme, (New-Object System.Text.UTF8Encoding($true)))

    $license = Join-Path $root 'LICENSE'
    if (Test-Path -LiteralPath $license -PathType Leaf) {
        Copy-Item -LiteralPath $license -Destination (Join-Path $packageDir 'LICENSE.txt') -Force
    }

    $innerChecksums = "$sha256  $exeName`r`n"
    [IO.File]::WriteAllText((Join-Path $packageDir 'SHA256SUMS.txt'), $innerChecksums, (New-Object System.Text.UTF8Encoding($false)))

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path $packageDir -DestinationPath $zipPath -CompressionLevel Optimal
}
finally {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
}

$zipSha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
$checksums = "$sha256  $exeName`r`n$zipSha256  $zipName`r`n"
[IO.File]::WriteAllText($checksumsPath, $checksums, (New-Object System.Text.UTF8Encoding($false)))

if (-not [string]::IsNullOrWhiteSpace($WebsiteDirectory)) {
    New-Item -ItemType Directory -Force -Path $WebsiteDirectory | Out-Null
    Copy-Item -LiteralPath $outputExe -Destination (Join-Path $WebsiteDirectory $exeName) -Force
    Copy-Item -LiteralPath $checksumsPath -Destination (Join-Path $WebsiteDirectory 'SHA256SUMS.txt') -Force
}

if (-not [string]::IsNullOrWhiteSpace($WebsiteReleaseJson)) {
    if (-not (Test-Path -LiteralPath $WebsiteReleaseJson -PathType Leaf)) {
        throw "Fichier release.json introuvable : $WebsiteReleaseJson"
    }
    $release = Get-Content -LiteralPath $WebsiteReleaseJson -Raw -Encoding UTF8 | ConvertFrom-Json
    $release.windowsVersion = $Version
    $release.windowsSha256 = $sha256
    $json = $release | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText($WebsiteReleaseJson, $json + "`r`n", (New-Object System.Text.UTF8Encoding($false)))
}

Write-Host ""
Write-Host "Paquet Windows genere : $zipPath" -ForegroundColor Green
Write-Host "SHA-256 exe : $sha256"
Write-Host "SHA-256 zip : $zipSha256"
Write-Host "Empreintes  : $checksumsPath"
if (-not [string]::IsNullOrWhiteSpace($WebsiteDirectory)) {
    Write-Host "Site web    : $WebsiteDirectory"
}
