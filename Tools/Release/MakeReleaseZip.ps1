<#
.SYNOPSIS
Packs the built Windows player into a release zip.

.DESCRIPTION
Takes the build folder (default Build, with UR.exe), leaves out what must not be shipped - the
*_BackUpThisFolder_ButDontShipItWithYourGame folder Unity writes for debugging - adds LICENSE,
THIRD_PARTY_NOTICES.md (their notices have to travel with a binary: ymfm's BSD licence, PDFium,
the SIL Open Font License of Lexend Exa), README.md and the players' README.txt from this folder,
and writes Release\UnderworldRevisited-<version>-win64.zip.

Before packing it refuses the build if anything of the original game shows up in it (game.gog,
UW.EXE, *.ARK, *.GR, *.DAT, SAVE folders ...): the game data is read at runtime from the
player's own GOG installation and never belongs in a package.

Windows PowerShell 5.1 or later. Run from anywhere:
    powershell -ExecutionPolicy Bypass -File Tools\Release\MakeReleaseZip.ps1
    powershell -ExecutionPolicy Bypass -File Tools\Release\MakeReleaseZip.ps1 -Version 0.9
#>
param(
    [string]$BuildDir = "",
    [string]$OutDir = "",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

if ($BuildDir -eq "") { $BuildDir = Join-Path $ProjectRoot "Build" }
if ($OutDir -eq "") { $OutDir = Join-Path $ProjectRoot "Release" }

$Exe = Join-Path $BuildDir "UR.exe"

if (-not (Test-Path $Exe)) {
    throw "No UR.exe in $BuildDir - build the player first (File > Build Profiles, Windows)."
}

# The version: given, or bundleVersion from the player settings plus the date of the build's
# data (UR.exe itself is Unity's unchanged launcher and keeps an old date).
if ($Version -eq "") {
    $Settings = Get-Content (Join-Path $ProjectRoot "ProjectSettings\ProjectSettings.asset")
    $Line = $Settings | Where-Object { $_ -match '^\s*bundleVersion:\s*(.+)$' } | Select-Object -First 1
    $Bundle = if ($Line -match '^\s*bundleVersion:\s*(.+)$') { $Matches[1].Trim() } else { "0" }
    $Data = Join-Path $BuildDir "UR_Data\globalgamemanagers"
    $Stamp = if (Test-Path $Data) { (Get-Item $Data).LastWriteTime } else { Get-Date }
    $Version = "$Bundle-" + $Stamp.ToString("yyyyMMdd")
}

$Name = "UnderworldRevisited-$Version-win64"
$Stage = Join-Path ([System.IO.Path]::GetTempPath()) $Name

if (Test-Path $Stage) { Remove-Item $Stage -Recurse -Force }
New-Item -ItemType Directory -Path $Stage | Out-Null

# The build, without Unity's debug backup.
Get-ChildItem $BuildDir -Force | Where-Object { $_.Name -notlike "*_BackUpThisFolder_ButDontShipItWithYourGame" } |
    ForEach-Object { Copy-Item $_.FullName -Destination $Stage -Recurse -Force }

# The licence texts and the descriptions.
foreach ($File in @("LICENSE", "THIRD_PARTY_NOTICES.md", "README.md")) {
    $Source = Join-Path $ProjectRoot $File

    if (-not (Test-Path $Source)) { throw "Missing $File in the project root." }

    Copy-Item $Source -Destination $Stage
}

Copy-Item (Join-Path $PSScriptRoot "README.txt") -Destination $Stage

# Nothing of the original game in the package.
$Forbidden = @("game.gog", "uw.exe", "uw2.exe", "*.ark", "*.gr", "*.dat", "*.byt", "*.cmb", "*.sys", "*.pak",
               "*.n0*", "*.xmi", "*.voc", "*.tr", "*.asm", "*.idb")
$Found = @()

foreach ($Pattern in $Forbidden) {
    $Found += Get-ChildItem $Stage -Recurse -Force -File -Filter $Pattern -ErrorAction SilentlyContinue
}

$Found += Get-ChildItem $Stage -Recurse -Force -Directory | Where-Object { $_.Name -match '^SAVE\d' }

if ($Found.Count -gt 0) {
    $Found | ForEach-Object { Write-Host "  original data? $($_.FullName)" }
    Remove-Item $Stage -Recurse -Force
    throw "The build holds files that look like original game data - nothing was packed."
}

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$Zip = Join-Path $OutDir "$Name.zip"

if (Test-Path $Zip) { Remove-Item $Zip -Force }

# Zipped with the folder itself, so the archive unpacks into a folder of its own.
Compress-Archive -Path $Stage -DestinationPath $Zip -CompressionLevel Optimal

Remove-Item $Stage -Recurse -Force

$Size = [Math]::Round((Get-Item $Zip).Length / 1MB, 1)
Write-Host "Written: $Zip ($Size MB)"
