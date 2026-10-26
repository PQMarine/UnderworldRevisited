<#
.SYNOPSIS
Packs the built player into a release archive - the Windows player as a zip, the Linux player
as a tar.gz - and beside it the translation kit for the same platform.

.DESCRIPTION
Takes the build folder (default Build with UR.exe for Windows, Build\Linux with UR.x86_64 for
Linux), leaves out what must not be shipped - the *_BackUpThisFolder_ButDontShipItWithYourGame
folder Unity writes for debugging - adds LICENSE, THIRD_PARTY_NOTICES.md (their notices have to
travel with a binary: ymfm's BSD licence, PDFium, the SIL Open Font License of Lexend Exa), the
full licence texts THIRD_PARTY_NOTICES.md points to (the ThirdParty folder: PDFium's and its
components', Lexend's OFL - missing from the packages up to 0.3.0), README.md and the players'
README.txt from this folder, the port's own texts in other languages (Languages/*.tsv - never a
game-text pack, *.strings.tsv), and writes
Release\UnderworldRevisited-<version>-win64.zip or ...-linux64.tar.gz.

THE LINUX ARCHIVE IS A TAR.GZ because a zip made on Windows carries no executable bit, and the
player would not start without a chmod. The tar is written by Git for Windows' GNU tar
(C:\Program Files\Git\usr\bin\tar.exe), which can set the mode of UR.x86_64 to 755; without
it the script falls back to a zip and says so (README.txt tells the player to chmod).

THE TRANSLATION KIT (translation wave 6, 2026-10-26) is a package of its own beside the game's,
Release\UnderworldRevisited-<version>-TranslationKit-win64.zip or ...-linux64.tar.gz (a tar.gz
for the same reason as the game's: uwdump must stay executable): uwdump built from Tools/UWDump
as one self-contained file (the .NET runtime inside, compressed, nothing trimmed - a translator
installs nothing; about 36 MB, framework-dependent it would be 1.5 MB but want .NET 8 installed),
TRANSLATING.md, Languages\en.tsv written by uwdump text-template (the port's own texts in English)
and Languages\de.tsv as an example, the kit's README (TranslationKit-README.txt in this folder),
LICENSE, THIRD_PARTY_NOTICES.md with MeltySynth's licence text (uwdump carries MeltySynth) and the
.NET runtime's licence and notices from the runtime pack it was built with. It needs no built
player: -KitOnly makes the kit alone. The kit holds no text of the game, English or translated -
the same check as the game's package refuses one.

Before packing it refuses the build if anything of the original game shows up in it (game.gog,
UW.EXE, *.ARK, *.GR, *.DAT, SAVE folders ...): the game data is read at runtime from the
player's own GOG installation and never belongs in a package.

Windows PowerShell 5.1 or later, the .NET 8 SDK for the kit. Run from anywhere:
    powershell -ExecutionPolicy Bypass -File Tools\Release\MakeReleaseZip.ps1
    powershell -ExecutionPolicy Bypass -File Tools\Release\MakeReleaseZip.ps1 -Platform linux64
    powershell -ExecutionPolicy Bypass -File Tools\Release\MakeReleaseZip.ps1 -Version 0.9
    powershell -ExecutionPolicy Bypass -File Tools\Release\MakeReleaseZip.ps1 -KitOnly
#>
param(
    [ValidateSet("win64", "linux64")]
    [string]$Platform = "win64",
    [string]$BuildDir = "",
    [string]$OutDir = "",
    [string]$Version = "",
    # Only the translation kit, no game package - needs no built player.
    [switch]$KitOnly,
    # Only the game package, no translation kit.
    [switch]$NoKit
)

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$IsLinux = $Platform -eq "linux64"

if ($KitOnly -and $NoKit) { throw "-KitOnly and -NoKit together leave nothing to pack." }

if ($BuildDir -eq "") { $BuildDir = if ($IsLinux) { Join-Path $ProjectRoot "Build\Linux" } else { Join-Path $ProjectRoot "Build" } }
if ($OutDir -eq "") { $OutDir = Join-Path $ProjectRoot "Release" }

$ExeName = if ($IsLinux) { "UR.x86_64" } else { "UR.exe" }
$Exe = Join-Path $BuildDir $ExeName

if (-not $KitOnly -and -not (Test-Path $Exe)) {
    $Hint = if ($IsLinux) { "-buildTarget Linux64 -buildLinux64Player Build\Linux\UR.x86_64" } else { "File > Build Profiles, Windows" }
    throw "No $ExeName in $BuildDir - build the player first ($Hint), or make the translation kit alone with -KitOnly."
}

# The version: given, or bundleVersion from the player settings plus the date of the build's
# data (the launcher itself is Unity's unchanged file and keeps an old date); without a build
# (-KitOnly) today's date.
if ($Version -eq "") {
    $Settings = Get-Content (Join-Path $ProjectRoot "ProjectSettings\ProjectSettings.asset")
    $Line = $Settings | Where-Object { $_ -match '^\s*bundleVersion:\s*(.+)$' } | Select-Object -First 1
    $Bundle = if ($Line -match '^\s*bundleVersion:\s*(.+)$') { $Matches[1].Trim() } else { "0" }
    $Data = Join-Path $BuildDir "UR_Data\globalgamemanagers"
    $Stamp = if (Test-Path $Data) { (Get-Item $Data).LastWriteTime } else { Get-Date }
    $Version = "$Bundle-" + $Stamp.ToString("yyyyMMdd")
}

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

# Nothing of the original game in a package - nor a game-text pack (Languages/<code>.strings.tsv,
# the game's strings, translated or not) a test may have put beside the built game. Removes the
# stage and throws when it finds one.
function Assert-NoOriginalData([string]$Stage) {
    $Forbidden = @("game.gog", "uw.exe", "uw2.exe", "*.ark", "*.gr", "*.dat", "*.byt", "*.cmb", "*.sys", "*.pak",
                   "*.n0*", "*.xmi", "*.voc", "*.tr", "*.asm", "*.idb", "*.strings.tsv")
    $Found = @()

    foreach ($Pattern in $Forbidden) {
        $Found += Get-ChildItem $Stage -Recurse -Force -File -Filter $Pattern -ErrorAction SilentlyContinue
    }

    $Found += Get-ChildItem $Stage -Recurse -Force -Directory | Where-Object { $_.Name -match '^SAVE\d' }

    if ($Found.Count -gt 0) {
        $Found | ForEach-Object { Write-Host "  original data? $($_.FullName)" }
        Remove-Item $Stage -Recurse -Force
        throw "The package holds files that look like original game data - nothing was packed."
    }
}

# The staged folder as an archive that unpacks into a folder of its own: on Linux a tar.gz with
# the one executable at mode 755, elsewhere (or without GNU tar) a zip. Returns the archive's path
# and removes the stage.
function Write-Archive([string]$Stage, [string]$Name, [string]$Executable) {
    $GnuTar = "C:\Program Files\Git\usr\bin\tar.exe"
    $Archive = $null

    if ($IsLinux -and (Test-Path $GnuTar)) {
        # Everything but the executable with the default mode, then the executable appended as 755 -
        # GNU tar's --mode applies to every file of one call, and only the executable must be
        # executable. Archived with the folder itself, so it unpacks into a folder of its own.
        $Archive = Join-Path $OutDir "$Name.tar.gz"
        $Tar = Join-Path ([System.IO.Path]::GetTempPath()) "$Name.tar"
        $Parent = Split-Path $Stage -Parent

        if (Test-Path $Archive) { Remove-Item $Archive -Force }
        if (Test-Path $Tar) { Remove-Item $Tar -Force }

        Push-Location $Parent
        try {
            # --force-local: to GNU tar a Windows path with its colon looks like host:file.
            & $GnuTar --force-local --format=ustar --owner=0 --group=0 --exclude="$Name/$Executable" -cf $Tar $Name
            if ($LASTEXITCODE -ne 0) { throw "tar failed ($LASTEXITCODE)." }
            & $GnuTar --force-local --format=ustar --owner=0 --group=0 --mode="755" -rf $Tar "$Name/$Executable"
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
            Write-Host "GNU tar of Git for Windows not found at $GnuTar - writing a zip; $Executable has to be made executable with chmod +x (the README says so)."
        }

        $Archive = Join-Path $OutDir "$Name.zip"

        if (Test-Path $Archive) { Remove-Item $Archive -Force }

        # Zipped with the folder itself, so the archive unpacks into a folder of its own.
        Compress-Archive -Path $Stage -DestinationPath $Archive -CompressionLevel Optimal
    }

    Remove-Item $Stage -Recurse -Force

    $Size = [Math]::Round((Get-Item $Archive).Length / 1MB, 1)
    Write-Host "Written: $Archive ($Size MB)"

    return $Archive
}

# ------------------------------------------------- The game

if (-not $KitOnly) {
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

    # The full controls README.md and README.txt point to, under the same path as in the project.
    $Controls = Join-Path $ProjectRoot "Docs\CONTROLS.md"

    if (-not (Test-Path $Controls)) { throw "Missing Docs\CONTROLS.md in the project root." }

    New-Item -ItemType Directory -Force -Path (Join-Path $Stage "Docs") | Out-Null
    Copy-Item $Controls -Destination (Join-Path $Stage "Docs")

    # The rule set's keys with their defaults and meanings (since 0.9.2), generated from the code by
    # uwdump rules-template (no game data needed), so it cannot drift from the build.
    $RulesTemplate = Join-Path $Stage "Docs\rules-template.jsonc"
    $UWDump = Join-Path $ProjectRoot "Tools\UWDump"
    $RulesText = & dotnet run --project $UWDump -c Release -- rules-template 2>$null

    if ($LASTEXITCODE -ne 0 -or -not $RulesText) { throw "uwdump rules-template failed." }

    [System.IO.File]::WriteAllLines($RulesTemplate, [string[]]$RulesText, (New-Object System.Text.UTF8Encoding($false)))

    # The port's own texts in other languages (Languages/<code>.tsv, read beside the executable by
    # UWLanguage), fresh from the project; the editor's build copies them too (UWLanguageBuildCopy).
    # Never a game-text pack (<code>.strings.tsv): that is the game's own text - see the check below.
    $Languages = Join-Path $ProjectRoot "Languages"

    if (Test-Path $Languages) {
        $LanguageStage = Join-Path $Stage "Languages"

        New-Item -ItemType Directory -Force -Path $LanguageStage | Out-Null
        Get-ChildItem $Languages -File -Filter "*.tsv" | Where-Object { $_.Name -notlike "*.strings.tsv" } |
            ForEach-Object { Copy-Item $_.FullName -Destination $LanguageStage -Force }
    }

    # The full licence texts THIRD_PARTY_NOTICES.md names, under the same path as in the project.
    $ThirdParty = Join-Path $ProjectRoot "ThirdParty"

    if (-not (Test-Path $ThirdParty)) { throw "Missing the ThirdParty folder in the project root." }

    Copy-Item $ThirdParty -Destination $Stage -Recurse

    Assert-NoOriginalData $Stage
    Write-Archive $Stage $Name $ExeName | Out-Null
}

# ------------------------------------------------- The translation kit

if (-not $NoKit) {
    $KitName = "UnderworldRevisited-$Version-TranslationKit-$Platform"
    $KitStage = Join-Path ([System.IO.Path]::GetTempPath()) $KitName
    $Runtime = if ($IsLinux) { "linux-x64" } else { "win-x64" }
    $ToolName = if ($IsLinux) { "uwdump" } else { "uwdump.exe" }
    $UWDump = Join-Path $ProjectRoot "Tools\UWDump"
    $Publish = Join-Path ([System.IO.Path]::GetTempPath()) "$KitName-publish"

    if (Test-Path $KitStage) { Remove-Item $KitStage -Recurse -Force }
    if (Test-Path $Publish) { Remove-Item $Publish -Recurse -Force }
    New-Item -ItemType Directory -Path $KitStage | Out-Null

    # uwdump as one self-contained file: the .NET runtime inside and compressed (36 MB instead of 69,
    # unpacked in memory at start, nothing written to disk), no debug files, and NO TRIMMING - a
    # trimmed build drops what the trimmer cannot see being used, and nothing here tests what it
    # would take away.
    & dotnet publish $UWDump -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugType=none -o $Publish | Out-Null

    if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $Publish $ToolName))) { throw "dotnet publish of uwdump for $Runtime failed." }

    Copy-Item (Join-Path $Publish $ToolName) -Destination $KitStage
    Remove-Item $Publish -Recurse -Force

    # The guide, the kit's README, the licences: the project's, MeltySynth's (inside uwdump through
    # the engine-free layer), the .NET runtime's.
    $Guide = Join-Path $ProjectRoot "Docs\TRANSLATING.md"

    if (-not (Test-Path $Guide)) { throw "Missing Docs\TRANSLATING.md in the project root." }

    Copy-Item $Guide -Destination $KitStage
    Copy-Item (Join-Path $PSScriptRoot "TranslationKit-README.txt") -Destination (Join-Path $KitStage "README.txt")

    foreach ($File in @("LICENSE", "THIRD_PARTY_NOTICES.md")) {
        Copy-Item (Join-Path $ProjectRoot $File) -Destination $KitStage
    }

    New-Item -ItemType Directory -Force -Path (Join-Path $KitStage "ThirdParty\MeltySynth") | Out-Null
    Copy-Item (Join-Path $ProjectRoot "ThirdParty\MeltySynth\LICENSE.txt") -Destination (Join-Path $KitStage "ThirdParty\MeltySynth")

    # The runtime inside uwdump is the runtime pack publish took from the NuGet cache (MIT, .NET
    # Foundation) - not the dotnet installation's own LICENSE.txt, which is Microsoft's licence of its
    # build of the SDK. The pack of the highest 8.x there is the one publish used (the latest patch).
    $NuGet = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE ".nuget\packages" }
    $PackRoot = Join-Path $NuGet "microsoft.netcore.app.runtime.$Runtime"
    $Pack = Get-ChildItem $PackRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^8\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } | Select-Object -Last 1

    if (-not $Pack) { throw "No .NET 8 runtime pack for $Runtime in $PackRoot." }

    $DotnetStage = Join-Path $KitStage "ThirdParty\dotnet"

    New-Item -ItemType Directory -Force -Path $DotnetStage | Out-Null

    foreach ($File in @("LICENSE.TXT", "THIRD-PARTY-NOTICES.TXT")) {
        $Source = Join-Path $Pack.FullName $File

        if (-not (Test-Path $Source)) { throw "Missing $File in the runtime pack $($Pack.FullName)." }

        Copy-Item $Source -Destination $DotnetStage
    }

    # The port's own texts: the English template, fresh from the code (uwdump text-template, the same
    # bytes as the editor's menu), and the German translation as an example. Never a game-text pack.
    $KitLanguages = Join-Path $KitStage "Languages"
    $EnglishFile = Join-Path $KitLanguages "en.tsv"

    New-Item -ItemType Directory -Force -Path $KitLanguages | Out-Null

    # uwdump reports on stderr, which Windows PowerShell turns into an error under "Stop".
    $ErrorActionPreference = "Continue"
    & dotnet run --project $UWDump -c Release -- text-template en --out $EnglishFile 2>$null
    $TemplateExit = $LASTEXITCODE
    $ErrorActionPreference = "Stop"

    if ($TemplateExit -ne 0 -or -not (Test-Path $EnglishFile)) { throw "uwdump text-template failed." }

    $German = Join-Path $ProjectRoot "Languages\de.tsv"

    if (-not (Test-Path $German)) { throw "Missing Languages\de.tsv, the kit's example." }

    Copy-Item $German -Destination $KitLanguages

    Assert-NoOriginalData $KitStage
    Write-Archive $KitStage $KitName $ToolName | Out-Null
}
