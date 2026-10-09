# Building

How to build [Underworld Revisited](../README.md) yourself, pack a release, and use the engine-free
layer and its tools.

- [Building the game](#building-the-game)
- [Packing a release](#packing-a-release)
- [The engine-free layer](#the-engine-free-layer)
- [Tools](#tools)

## Building the game

1. Clone the repository and open the folder with Unity 6000.6.0f1.
2. Select `Assets/Resources/UWSettings.asset` and set **GogInstallPath** to your GOG
   installation folder (the folder containing `game.gog`), or to `game.gog` itself. Common
   installation folders are found automatically when the field is empty.
3. Open `Assets/UW.unity` and press Play, or build a player.

Build the player from Unity's build window (`File > Build Profiles` in Unity 6), target
Windows or Linux (the Linux Build Support (Mono) module of the Hub). There is nothing else to
prepare: the whole game is built from `Assets/UW.unity`, and the settings asset in
`Assets/Resources` travels with it. In batch mode:

```
Unity.exe -batchmode -quit -nographics -projectPath <project> -buildWindows64Player <project>\Build\UR.exe
Unity.exe -batchmode -quit -nographics -projectPath <project> -buildTarget Linux64 -buildLinux64Player <project>\Build\Linux\UR.x86_64
```

The built game finds the game data the same way the editor does. If **GogInstallPath** is
empty, these folders are searched in this order:

```
C:\GOG Games\Ultima Underworld
D:\GOG Games\Ultima Underworld
C:\Program Files (x86)\GOG Galaxy\Games\Ultima Underworld
C:\Program Files (x86)\GOG.com\Ultima Underworld
C:\Ultima Underworld
D:\Ultima Underworld
```

On Linux the same role falls to `~/GOG Games/Ultima Underworld` and `~/GOG Games/Ultima
Underworld 1+2`, `~/Games/Ultima Underworld`, Heroic's `~/Games/Heroic/Ultima Underworld 1+2`,
Lutris' `~/Games/gog/ultima-underworld-1-2`, and the same `GOG Games` folder inside
`~/.wine/drive_c` and the Steam Proton `compatdata` prefixes. What is looked for is the folder
with `game.gog`, the CD image every GOG release carries.

So a build made on one machine runs on another as long as the game is installed in one of
them. To be sure your copy works, run the self-check (see Tools) before building.

In the GOG release the game data is not installed as separate files but kept in `game.gog`, a
CD image. On the first start the Ultima Underworld 1 files (`CRIT`, `CUTS`, `DATA`, `SOUND`,
`UW.EXE`) are extracted from it once into a local cache folder; nothing is written to the
installation. Built from source, the settings asset's **SavegameFolder** overrides where the
save games go, and its **DataPath** can point to an already extracted `DATA` folder instead.

The native libraries the game loads come built in `Assets/Plugins`: PDFium for the manual and
Munt's libmt32emu for the MT-32 music. libmt32emu is built unmodified from the source in
`ThirdParty/Munt` by `Tools/Munt/build-windows.bat` (Visual Studio 2022 with the C++ tools, no
CMake) and `Tools/Munt/build-linux.sh` (g++; built on Ubuntu 22.04, so it needs no newer glibc
than Unity 6 itself); see [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md).

## Packing a release

To pack a build for others, run `Tools\Release\MakeReleaseZip.ps1` (Windows PowerShell). It
writes `Release\UnderworldRevisited-<version>-win64.zip` with the player, LICENSE,
THIRD_PARTY_NOTICES.md and the full licence texts of the `ThirdParty` folder (their notices
have to travel with the binary), the README with the controls and a short README.txt for
players - and without Unity's `*_BackUpThisFolder_ButDontShipItWithYourGame` folder. With
`-Platform linux64` it packs `Build\Linux` into `UnderworldRevisited-<version>-linux64.tar.gz`,
the launcher with its executable bit (GNU tar of Git for Windows; without it a zip, and the
player has to `chmod +x`). It refuses a build that holds anything looking like original game
data.

## The engine-free layer

Everything that reads and interprets the original data lives in `Assets/UWDataImport` and
knows nothing about any game engine: file formats, strings and conversations, object and
critter tables, the OPL2 emulation and the music drivers, the cutscene decoder, the 3D models
from `UW.EXE`, and a growing part of the game rules. Unity enforces this - the assembly
definition sets `noEngineReferences`, so a single `using UnityEngine` in there fails to
compile.

`Core/UWDataImport.csproj` builds exactly those sources with the plain .NET SDK, without
Unity, as a .NET Standard 2.1 library. The tools below run on top of it, and they keep the
claim honest: if the layer ever grew an engine dependency, their build would break.

Input, camera, collision, rendering and audio output stay on the Unity side. The long-term plan
is to move more rules across the line, so the game logic can be reused without Unity.

## Tools

The path the tools take is a `DATA` folder, a GOG installation folder or `game.gog` itself.

`Tools/UWSelfCheck` reads a complete installation once and reports what came through -
levels, strings, textures, the 3D models from `UW.EXE`, cutscenes and sound - and runs the
rule checks of the engine-free layer. Use it to find out whether your copy of the game works
with this port before starting Unity at all:

```
dotnet run --project Tools/UWSelfCheck -- "C:\GOG Games\Ultima Underworld 1"
```

```
Data folder: C:\Users\...\Temp\UnderworldRevisited\UW1\DATA

  OK   load       151 ms
  OK   levels     9 levels, 4101 objects in tiles
  OK   strings    122 blocks, 11106 strings
  OK   textures   818 pictures in 6 kinds
  OK   models     26 models with 484 faces, UW.EXE is the supported build
  OK   cutscenes  47 files, 1210 frames
  OK   sound      24 effects, 12 music pieces with 6690 notes
```

`Tools/UWDump` prints single pieces of the data:

```
dotnet run --project Tools/UWDump -- info   "C:\GOG Games\Ultima Underworld 1"
dotnet run --project Tools/UWDump -- level  <path> 1 3 50 6
dotnet run --project Tools/UWDump -- object <path> 349
dotnet run --project Tools/UWDump -- find   <path> lockpick
```

`Tools/UWSoundDump` renders sound to WAV files, so it can be listened to outside the game: the
sound effects on the emulated AdLib, and the music's MT-32 version on General MIDI or on the
MT-32 emulation (with your own ROMs):

```
dotnet run --project Tools/UWSoundDump -- <DATA path> <output folder> [effect number ...]
dotnet run --project Tools/UWSoundDump -- <DATA path> <output folder> music <soundfont.sf2> [piece ...]
dotnet run --project Tools/UWSoundDump -- <DATA path> <output folder> mt32 <ROM folder> [piece ...]
```

`Tools/UWSoundFontTrim` made the General MIDI soundfont that ships with the game: it cuts a
SoundFont 2 file down to the presets and key ranges the music plays, without changing a kept
zone (the result renders bit for bit like the full file).

`Tools/UWRuleIndex` and `Tools/UWOriginalCoverage` write the two indexes in `Docs`: where the
original is implemented ([RULES-INDEX.md](RULES-INDEX.md)), and which routines of `UW.EXE`
have not been looked at ([ORIGINAL-COVERAGE.md](ORIGINAL-COVERAGE.md)).
