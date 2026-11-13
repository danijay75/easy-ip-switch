param(
    [switch]$Preview,
    [string]$OutputPath,
    [switch]$Sign,
    [ValidateSet('Azure', 'Pfx', 'Store')]
    [string]$SignMode = 'Azure',
    [string]$PfxPath,
    [string]$PfxPassword,
    [string]$Thumbprint,
    [string]$TimestampUrl = 'http://timestamp.acs.microsoft.com',
    [string]$AzureDlib,
    [string]$AzureMetadata,
    [switch]$Package,
    [string]$WebsiteDirectory,
    [string]$WebsiteReleaseJson
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) {
    $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    $csc = Join-Path $fw 'csc.exe'
}
if (-not (Test-Path $csc)) {
    throw "Compilateur C# introuvable (csc.exe)."
}

$src = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
$assets = Join-Path $root 'assets'
$icon = Join-Path $assets 'app.ico'
$manifest = Join-Path $src 'app.manifest'

$appInfo = Get-Content -LiteralPath (Join-Path $src 'AppInfo.cs') -Raw
$version = '1.1.3'
if ($appInfo -match 'Version\s*=\s*"([^"]+)"') {
    $version = $Matches[1]
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null
New-Item -ItemType Directory -Force -Path $assets | Out-Null

if (-not (Test-Path $icon)) {
    $tool = Join-Path $env:TEMP 'EasyIpSwitch.MakeIcon.exe'
    & $csc /nologo /target:exe /out:$tool /r:System.Drawing.dll (Join-Path $root 'tools\MakeIcon.cs')
    if ($LASTEXITCODE -ne 0) { throw "Echec de compilation de l'outil d'icone." }
    & $tool $icon
    if ($LASTEXITCODE -ne 0) { throw "Echec de generation de l'icone." }
}

$sources = @(Get-ChildItem -Path $src -Filter *.cs -Recurse | ForEach-Object { $_.FullName })
$references = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Management.dll') |
    ForEach-Object { '/r:' + (Join-Path $fw $_) }

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $output = $OutputPath
    } else {
        $output = Join-Path $root $OutputPath
    }
} elseif ($Preview) {
    $output = Join-Path $dist 'Easy IP-Switch (apercu).exe'
} else {
    $output = Join-Path $dist ("easy ip-switch v{0}.exe" -f $version)
}

$outputDirectory = Split-Path -Parent $output
if ($outputDirectory) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$options = @(
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/nologo',
    '/langversion:5'
)
$options += "/win32icon:$icon"
$qr = Join-Path $assets 'donation-qr.png'
if (Test-Path -LiteralPath $qr) {
    $options += ('/resource:"{0}",EasyIpSwitch.DonationQr.png' -f $qr)
}
foreach ($code in @('fr', 'gb', 'de', 'es')) {
    $flag = Join-Path $assets ('flags\' + $code + '.png')
    if (Test-Path -LiteralPath $flag) {
        $options += ('/resource:"{0}",EasyIpSwitch.Flag.{1}.png' -f $flag, $code)
    }
}
# Compiler sur le disque local : csc renomme son fichier temporaire vers la
# sortie, ce que le lecteur virtuel pCloud traduit parfois par un doublon de
# casse (Easy IP-Switch.exe / easy ip-switch.exe).
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ('EasyIpSwitch-' + [Guid]::NewGuid().ToString('N') + '.exe')
$options += "/out:$staging"
if (-not $Preview) {
    $options += "/win32manifest:$manifest"
}
$options += $references
$options += $sources

try {
    & $csc $options
    if ($LASTEXITCODE -ne 0) {
        throw "Compilation echouee (code $LASTEXITCODE)."
    }
    try {
        Copy-Item -LiteralPath $staging -Destination $output -Force
    }
    catch {
        throw "Impossible d'ecrire '$output' (application encore ouverte ?). $($_.Exception.Message)"
    }
}
finally {
    Remove-Item -LiteralPath $staging -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
    throw "Le fichier de sortie est introuvable : $output"
}

if ($Sign) {
    if ($Preview) {
        throw "La signature n'est pas prise en charge pour l'apercu."
    }

    $signScript = Join-Path $root 'sign.ps1'
    if (-not (Test-Path -LiteralPath $signScript -PathType Leaf)) {
        throw "Script de signature introuvable : $signScript"
    }

    $signParams = @{
        Path = $output
        Mode = $SignMode
        TimestampUrl = $TimestampUrl
    }
    if (-not [string]::IsNullOrWhiteSpace($PfxPath)) { $signParams.PfxPath = $PfxPath }
    if (-not [string]::IsNullOrEmpty($PfxPassword)) { $signParams.PfxPassword = $PfxPassword }
    if (-not [string]::IsNullOrWhiteSpace($Thumbprint)) { $signParams.Thumbprint = $Thumbprint }
    if (-not [string]::IsNullOrWhiteSpace($AzureDlib)) { $signParams.AzureDlib = $AzureDlib }
    if (-not [string]::IsNullOrWhiteSpace($AzureMetadata)) { $signParams.AzureMetadata = $AzureMetadata }

    & $signScript @signParams
}

if ($Package) {
    if (-not $Sign) {
        Write-Warning "Le paquet est genere sans signature de code."
    }

    $packageScript = Join-Path $root 'package-windows.ps1'
    if (-not (Test-Path -LiteralPath $packageScript -PathType Leaf)) {
        throw "Script de paquet introuvable : $packageScript"
    }

    $packageParams = @{
        ExePath = $output
        Version = $version
        OutputDirectory = $dist
    }
    if (-not [string]::IsNullOrWhiteSpace($WebsiteDirectory)) {
        $packageParams.WebsiteDirectory = $WebsiteDirectory
    }
    if (-not [string]::IsNullOrWhiteSpace($WebsiteReleaseJson)) {
        $packageParams.WebsiteReleaseJson = $WebsiteReleaseJson
    }

    & $packageScript @packageParams
}

$size = [math]::Round((Get-Item $output).Length / 1KB)
$signature = Get-AuthenticodeSignature -LiteralPath $output
Write-Host ""
Write-Host "Compilation reussie : $output ($size Ko)"
Write-Host "Signature : $($signature.Status)"
