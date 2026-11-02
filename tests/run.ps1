$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) {
    $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
    $csc = Join-Path $fw 'csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'C# compiler not found.'
}

$testExe = Join-Path $env:TEMP ('EasyIpSwitch.Tests.' + [Guid]::NewGuid().ToString('N') + '.exe')
$references = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Management.dll') |
    ForEach-Object { '/r:' + (Join-Path $fw $_) }
$sources = @(Get-ChildItem -Path (Join-Path $root 'src') -Filter *.cs -Recurse | ForEach-Object { $_.FullName })
$sources += @(Get-ChildItem -Path (Join-Path $root 'tests') -Filter *.cs | ForEach-Object { $_.FullName })
$options = @('/nologo', '/target:exe', '/platform:anycpu', '/langversion:5', '/main:EasyIpSwitch.AdapterCardToggleTests')
$options += "/out:$testExe"
$options += $references
$qr = Join-Path $root 'assets\donation-qr.png'
if (Test-Path -LiteralPath $qr) {
    $options += ('/resource:"{0}",EasyIpSwitch.DonationQr.png' -f $qr)
}
foreach ($code in @('fr', 'gb', 'de', 'es')) {
    $flag = Join-Path $root ('assets\flags\' + $code + '.png')
    if (Test-Path -LiteralPath $flag) {
        $options += ('/resource:"{0}",EasyIpSwitch.Flag.{1}.png' -f $flag, $code)
    }
}
$options += $sources

try {
    & $csc $options
    if ($LASTEXITCODE -ne 0) { throw "Test compilation failed (exit $LASTEXITCODE)." }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw "Tests failed (exit $LASTEXITCODE)." }
} finally {
    Remove-Item -LiteralPath $testExe -Force -ErrorAction SilentlyContinue
}
