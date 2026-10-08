param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'Windows\RekordboxSetupCloner\RekordboxSetupCloner.csproj'
$output = Join-Path $root "dist\windows-$Runtime"
$assets = Join-Path $root 'dist\windows-assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null

[xml]$plist = Get-Content (Join-Path $root 'Resources\Info.plist') -Raw
$versionNode = $plist.SelectSingleNode('/plist/dict/key[text()="CFBundleShortVersionString"]/following-sibling::string[1]')
$version = $versionNode.InnerText
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Info.plist must contain a three-part version number.' }

# Create a Windows icon from the existing selected artwork (PNG inside an ICO container).
Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Image]::FromFile((Join-Path $root 'Resources\AppIcon.png'))
$bitmap = New-Object System.Drawing.Bitmap 256, 256
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$png = New-Object System.IO.MemoryStream
try {
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($source, 0, 0, 256, 256)
    $bitmap.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $png.ToArray()
    $icon = Join-Path $assets 'AppIcon.ico'
    $writer = New-Object System.IO.BinaryWriter ([System.IO.File]::Create($icon))
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22)
        $writer.Write($bytes)
    } finally { $writer.Dispose() }
} finally {
    $graphics.Dispose(); $bitmap.Dispose(); $source.Dispose(); $png.Dispose()
}

# Self-contained portable executable: users do not need to install .NET.
dotnet publish $project -c Release -r $Runtime --self-contained true -o $output `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false "-p:Version=$version" "-p:ApplicationIcon=$icon" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
Write-Output "Built $output\RekordboxSetupCloner.exe (version $version)"
