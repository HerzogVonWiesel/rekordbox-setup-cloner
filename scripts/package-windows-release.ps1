param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build-windows.ps1') -Runtime $Runtime
if ($LASTEXITCODE -ne 0) { throw 'Windows build failed.' }
[xml]$plist = Get-Content (Join-Path $root 'Resources\Info.plist') -Raw
$version = $plist.SelectSingleNode('/plist/dict/key[text()="CFBundleShortVersionString"]/following-sibling::string[1]').InnerText
$architecture = $Runtime.Substring(4)
$release = Join-Path $root 'dist\release'
$staging = Join-Path $root "dist\windows-package-$architecture"
New-Item -ItemType Directory -Force -Path $release, $staging | Out-Null
Copy-Item (Join-Path $root "dist\windows-$Runtime\RekordboxSetupCloner.exe") $staging -Force
Copy-Item (Join-Path $root 'README.md') $staging -Force
$archiveName = "Rekordbox-Setup-Cloner-v$version-windows-$architecture.zip"
$archive = Join-Path $release $archiveName
Compress-Archive -Path (Join-Path $staging 'RekordboxSetupCloner.exe'), (Join-Path $staging 'README.md') -DestinationPath $archive -Force
$hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
# A platform-specific filename avoids colliding with the existing Mac release checksum asset.
[System.IO.File]::WriteAllText((Join-Path $release "SHA256SUMS-windows-$architecture.txt"), "$hash  $archiveName`n", (New-Object System.Text.UTF8Encoding $false))
Write-Output "Packaged $archive"
