<#
.SYNOPSIS
Packs the built player into a release archive - the Windows player as a zip, the Linux player
as a tar.gz.

.DESCRIPTION
Takes the build folder (default Build with UR.exe for Windows, Build\Linux with UR.x86_64 for
Linux), leaves out what must not be shipped - the *_BackUpThisFolder_ButDontShipItWithYourGame
folder Unity writes for debugging - adds LICENSE, THIRD_PARTY_NOTICES.md (their notices have to
travel with a binary: ymfm's BSD licence, PDFium, the SIL Open Font License of Lexend Exa),
README.md and the players' README.txt from this folder, and writes
Release\UnderworldRevisited-<version>-win64.zip or ...-linux64.tar.gz.

THE LINUX ARCHIVE IS A TAR.GZ because a zip made on Windows carries no executable bit, and the
player would not start without a chmod. The tar is written by Git for Windows' GNU tar
(C:\Program Files\Git\usr\bin\tar.exe), which can set the mode of UR.x86_64 to 755; without
it the script falls back to a zip and says so (README.txt tells the player to chmod).

Before packing it refuses the build if anything of the original game shows up in it (game.gog,
UW.EXE, *.ARK, *.GR, *.DAT, SAVE folders ...): the game data is read at runtime from the
player's own GOG installation and never belongs in a package.

Windows PowerShell 5.1 or later. Run from anywhere:
    powershell -ExecutionPolicy Bypass -File Tools\Release\MakeReleaseZip.ps1
    powershell -ExecutionPolicy Bypass -File Tools\Release\MakeReleaseZip.ps1 -Platform linux64
    powershell -ExecutionPolicy Bypass -File Tools\Release\MakeReleaseZip.ps1 -Version 0.9
#>
param(
    [ValidateSet("win64", "linux64")]
    [string]$Platform = "win64",
    [string]$BuildDir = "",
    [string]$OutDir = "",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$IsLinux = $Platform -eq "linux64"

if ($BuildDir -eq "") { $BuildDir = if ($IsLinux) { Join-Path $ProjectRoot "Build\Linux" } else { Join-Path $ProjectRoot "Build" } }
if ($OutDir -eq "") { $OutDir = Join-Path $ProjectRoot "Release" }

$ExeName = if ($IsLinux) { "UR.x86_64" } else { "UR.exe" }
$Exe = Join-Path $BuildDir $ExeName

if (-not (Test-Path $Exe)) {
    $Hint = if ($IsLinux) { "-buildTarget Linux64 -buildLinux64Player Build\Linux\UR.x86_64" } else { "File > Build Profiles, Windows" }
    throw "No $ExeName in $BuildDir - build the player first ($Hint)."
}

# The version: given, or bundleVersion from the player settings plus the date of the build's
# data (the launcher itself is Unity's unchanged file and keeps an old date).
if ($Version -eq "") {
    $Settings = Get-Content (Join-Path $ProjectRoot "ProjectSettings\ProjectSettings.asset")
    $Line = $Settings | Where-Object { $_ -match '^\s*bundleVersion:\s*(.+)$' } | Select-Object -First 1
    $Bundle = if ($Line -match '^\s*bundleVersion:\s*(.+)$') { $Matches[1].Trim() } else { "0" }
    $Data = Join-Path $BuildDir "UR_Data\globalgamemanagers"
    $Stamp = if (Test-Path $Data) { (Get-Item $Data).LastWriteTime } else { Get-Date }
    $Version = "$Bundle-" + $Stamp.ToString("yyyyMMdd")
}

$Name = "UnderworldRevisited-$Version-$Platform"
$Stage = Join-Path ([System.IO.Path]::GetTempPath()) $Name

if (Test-Path $Stage) { Remove-Item $Stage -Recurse -Force }
New-Item -ItemType Directory -Path $Stage | Out-Null

# The build, without Unity's debug backup - and without the Linux build, which lives in
# Build\Linux inside the Windows build folder (the first win64 zip after the Linux build
# weighed 80 MB instead of 45 before this line, 2026-10-05).
Get-ChildItem $BuildDir -Force | Where-Object { $_.Name -notlike "*_BackUpThisFolder_ButDontShipItWithYourGame" -and -not (-not $IsLinux -and $_.Name -eq "Linux") } |
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

$GnuTar = "C:\Program Files\Git\usr\bin\tar.exe"
$Archive = $null

if ($IsLinux -and (Test-Path $GnuTar)) {
    # Everything but the launcher with the default mode, then the launcher appended as 755 -
    # GNU tar's --mode applies to every file of one call, and only the launcher must be
    # executable. Archived with the folder itself, so it unpacks into a folder of its own.
    $Archive = Join-Path $OutDir "$Name.tar.gz"
    $Tar = Join-Path ([System.IO.Path]::GetTempPath()) "$Name.tar"
    $Parent = Split-Path $Stage -Parent

    if (Test-Path $Archive) { Remove-Item $Archive -Force }
    if (Test-Path $Tar) { Remove-Item $Tar -Force }

    Push-Location $Parent
    try {
        # --force-local: to GNU tar a Windows path with its colon looks like host:file.
        & $GnuTar --force-local --format=ustar --owner=0 --group=0 --exclude="$Name/$ExeName" -cf $Tar $Name
        if ($LASTEXITCODE -ne 0) { throw "tar failed ($LASTEXITCODE)." }
        & $GnuTar --force-local --format=ustar --owner=0 --group=0 --mode="755" -rf $Tar "$Name/$ExeName"
        if ($LASTEXITCODE -ne 0) { throw "tar append failed ($LASTEXITCODE)." }
    }
    finally { Pop-Location }

    # The gzip with .NET, so that no second tar flavour gets a say.
    $In = [System.IO.File]::OpenRead($Tar)
    $Out = [System.IO.File]::Create($Archive)
    $Gzip = New-Object System.IO.Compression.GZipStream($Out, [System.IO.Compression.CompressionLevel]::Optimal)
    try { $In.CopyTo($Gzip) }
    finally { $Gzip.Dispose(); $Out.Dispose(); $In.Dispose() }

    Remove-Item $Tar -Force
}
else {
    if ($IsLinux) {
        Write-Host "GNU tar of Git for Windows not found at $GnuTar - writing a zip; the player has to chmod +x $ExeName (README.txt says so)."
    }

    $Archive = Join-Path $OutDir "$Name.zip"

    if (Test-Path $Archive) { Remove-Item $Archive -Force }

    # Zipped with the folder itself, so the archive unpacks into a folder of its own.
    Compress-Archive -Path $Stage -DestinationPath $Archive -CompressionLevel Optimal
}

Remove-Item $Stage -Recurse -Force

$Size = [Math]::Round((Get-Item $Archive).Length / 1MB, 1)
Write-Host "Written: $Archive ($Size MB)"
