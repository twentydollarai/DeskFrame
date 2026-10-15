$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$release = Join-Path $PSScriptRoot 'release'
$package = Join-Path $release 'DeskFrame-1.0.0'
if (Test-Path -LiteralPath $package) { throw 'Package directory already exists; use a fresh workspace' }
if (Test-Path -LiteralPath (Join-Path $release 'DeskFrame-1.0.0.zip')) { throw 'Release ZIP already exists' }
if (Test-Path -LiteralPath (Join-Path $release 'SHA256SUMS.txt')) { throw 'Checksum manifest already exists' }
New-Item -ItemType Directory -Path $package -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'build\DeskFrame.exe'),(Join-Path $PSScriptRoot 'build\DeskFrame.exe.config'),(Join-Path $PSScriptRoot 'LICENSE'),(Join-Path $PSScriptRoot 'CHANGELOG.md'),(Join-Path $PSScriptRoot 'Start portable.cmd'),(Join-Path $PSScriptRoot 'RELEASE_NOTES.md'),(Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination $package
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'USER_GUIDE.txt') -Destination (Join-Path $package 'README.txt')
$zip = Join-Path $release 'DeskFrame-1.0.0.zip'
Compress-Archive -LiteralPath $package -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Value ($hash + '  DeskFrame-1.0.0.zip') -Encoding ASCII
Get-ChildItem -LiteralPath $package | Select-Object Name,Length
Get-Item -LiteralPath $zip | Select-Object Name,Length
