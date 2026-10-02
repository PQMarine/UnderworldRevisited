# Underworld Revisited

A source port of **Ultima Underworld: The Stygian Abyss** (1992) to Unity, built on the
original game data. The goal is full gameplay parity with the original, plus an optional
modern render mode. Support for Ultima Underworld II is planned for later.

**You need your own copy of Ultima Underworld - the GOG.com version** ("Ultima Underworld
1+2"); other releases are not supported (see Requirements). This repository contains no game
data: no graphics, sounds, texts, maps or executables of the original. Everything is read at
runtime from your installation. The only exception are the screenshots below, which show the
original's artwork.

This project is not affiliated with or endorsed by the owners of the Ultima Underworld
rights. Ultima and Ultima Underworld are trademarks of their respective owners. Underworld
Revisited is a free, non-commercial fan project: it is not sold, and no donations are accepted.

## Screenshots

![The locked doors of the Abyss, in the classic 4:3 frame](Screenshots/UR01.png)

*The classic interface at 4:3, Palette mode, with the modern font.*

| Palette mode | Remastered mode |
|---|---|
| ![Palette mode with the help window's map tab](Screenshots/UR02.png) | ![Remastered mode, the same spot](Screenshots/UR03.png) |
| *Light computed like the original, the help window beside the view with a live map.* | *The same spot lit by URP, with relief and glow.* |

| Original font | Modern font |
|---|---|
| ![A conversation in the original font, the help window's spells tab](Screenshots/UR04.png) | ![The same conversation in the modern font, the stats tab](Screenshots/UR05.png) |
| *A conversation, and the spells the character knows.* | *The same conversation, and the character's stats.* |

![The rune shelf, an ankh shrine and the player's own notes](Screenshots/UR06.png)

*The rune shelf, a shrine, and the player's own notes in the help window.*

The screenshots show the artwork of the original game, which belongs to the owners of the
Ultima Underworld rights. It is shown for illustration only and is not covered by this
project's licence.

## State

Built and largely confirmed against the original: the world and its objects, combat,
conversations and trading, traps and triggers, spells, character creation, saving and
loading (compatible with the original's save games), the main menu, cutscenes, AdLib music
and sound effects through an OPL2 emulation, and the classic interface. The whole game has
been played through from character creation to the end sequence. Open points are listed in
[ROADMAP.md](ROADMAP.md).

What is in:

- The nine dungeon levels with their objects, doors, locks, traps and triggers
- Two render modes: **Palette** computes light like the original through SHADES.DAT and
  LIGHT.DAT, **Remastered** lights with URP and adds relief, specular highlights, glow,
  shadows and torch lights - each of them switchable on its own, so the plain modern look is
  just all of them turned off
- The 3D models (barrel, chest, chair, shrine, bridge, portcullis ...) read from `UW.EXE`
- Combat with the original's tables, magic with runes, and the creatures with their goals,
  attitudes and group alarm
- Conversations, running the original conversation bytecode, including trading
- Character creation, inventory and paperdoll, automap, sleeping, hunger and fatigue, the
  endgame with talismans and the void
- Save games compatible with the original's, in `UNDEROM1\SAVE1` to `SAVE4`
- Intro, dreams and the end sequence from the original's cutscene files, with speech
- AdLib music and sound effects through an OPL2 emulation of the project's own
- The original's mouse-pointer steering (a modern free-look scheme is started but not yet
  playable, see Controls)
- Additions of the port's own, each optional: a help window beside the view (stats with the
  hidden values, a live map, notes kept with the save game, spells, mantras and the game's
  manual), a modern readable font, the original's 4:3 frame on wide screens, and dealing the
  attribute points at character creation yourself instead of rolling them

## Requirements

- **Windows** (64-bit). It is the only system the port is built and tested on, and the
  release build is for Windows only. Building from source for Linux or macOS may work but is
  untested: the help window's manual needs the PDFium library, of which only the Windows one
  is included, and the game folder is searched only in Windows locations (it can be chosen
  by hand). Running the Windows build under Proton or Wine is untested as well.
- Unity **6000.6.0f1** (Unity 6), Universal Render Pipeline - only to build it yourself
- **Ultima Underworld 1 from GOG.com** ("Ultima Underworld 1+2"). Only this version is
  supported: some data (3D models, their colours) is read directly from the game's executable
  at build-specific offsets, verified against the GOG `UW.EXE` (547,248 bytes). Other
  releases (original floppy, CD, other stores) may differ and are not supported.

## Setup

1. Install Ultima Underworld from GOG.com.
2. Clone the repository and open the folder with Unity 6000.6.0f1.
3. Select `Assets/Resources/UWSettings.asset` and set **GogInstallPath** to your GOG
   installation folder (the folder containing `game.gog`), or to `game.gog` itself. Common
   installation folders are found automatically when the field is empty.
4. Open `Assets/UW.unity` and press Play.

In the GOG release the game data is not installed as separate files but kept in `game.gog`, a
CD image. On the first start the Ultima Underworld 1 files (`CRIT`, `CUTS`, `DATA`, `SOUND`,
`UW.EXE`) are extracted from it once into a local cache folder; nothing is written to the
installation. Save games are read from and written to the installation's `UNDEROM1\SAVE1` to
`SAVE4`, so they can be exchanged with the original game. **SavegameFolder** overrides that
location, and **DataPath** can point to an already extracted `DATA` folder instead.

## Controls

**Play with the original scheme.** It is complete, and the whole game has been played through
with it. The modern scheme (see below) is unfinished and not yet playable. **Shift+F2**
switches between the two schemes at any time.

*Original scheme* - as in 1992: the mouse pointer is visible and steers the walking.

| | |
|---|---|
| Hold the left mouse button in the view | Walk. Where the pointer sits decides direction and speed, and the pointer's shape shows it, as in the original |
| Left mouse button, dragging an object | Pick it up and carry it on the pointer - to the inventory, to another spot, or onto something else to use it there |
| Right mouse button, short click | Look at whatever is under the pointer |
| Right mouse button, held and moved | Use it: open a door, throw a lever, drink from a fountain |
| W, S | Forward, backward |
| A, D | Turn left, right |
| The two keys next to X | Step sideways |
| J or Space | Jump |
| 1, 2, 3 | Look down, straight ahead, up |
| Q, E | Sink, rise - while hovering or flying |
| F1 to F6 | Options, talk, get, look, fight, use - the command icons, as in the original |
| F7 | Turn the panel between inventory and character |
| F8 | Cast the runes on the shelf |
| F10 | Make camp |
| Tab | The help window: spells, mantras, own notes and the game's manual (not with Alt, so Alt+Tab to another program leaves it alone) |
| Ctrl+S, R, M, F, D, Q | Save, restore, music, sound, detail, quit - the options shortcuts of the original |
| M, F | The map (with a map in the pack), the modern font |

*Modern scheme* - unfinished, not yet playable: free mouse look, WASD walks and steps
sideways, **I** opens the inventory. Walking, picking things up and putting them on work;
everything else still needs the original scheme. It is due for a complete rework (see
[ROADMAP.md](ROADMAP.md)).

Combat mode is started as in the original, by clicking the weapon on the paperdoll. Where in
the view you press then decides the blow - upper third bash, middle slash, lower third
stab - and the longer you hold, the stronger it lands.

## Building

Open the project with Unity 6000.6.0f1 and build it from Unity's build window
(`File > Build Profiles` in Unity 6), target Windows. There is nothing else to prepare: the
whole game is built from `Assets/UW.unity`, and the settings asset in `Assets/Resources`
travels with it.

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

So a build made on one machine runs on another as long as the game is installed in one of
them. To be sure your copy works, run the self-check above before building.

To pack a build for others, run `Tools\Release\MakeReleaseZip.ps1` (Windows PowerShell). It
writes `Release\UnderworldRevisited-<version>-win64.zip` with the player, LICENSE,
THIRD_PARTY_NOTICES.md (their notices have to travel with the binary), this README and a short
README.txt for players - and without Unity's `*_BackUpThisFolder_ButDontShipItWithYourGame`
folder. It refuses a build that holds anything looking like original game data.

## The engine-free layer

Everything that reads and interprets the original data lives in `Assets/UWDataImport` and
knows nothing about any game engine: file formats, strings and conversations, object and
critter tables, the OPL2 emulation for music and sound effects, the cutscene decoder, the
3D models from `UW.EXE`, and a growing part of the game rules. Unity enforces this - the
assembly definition sets `noEngineReferences`, so a single `using UnityEngine` in there
fails to compile.

`Core/UWDataImport.csproj` builds exactly those sources with the plain .NET SDK, without
Unity, as a .NET Standard 2.1 library. Two small command line programs run on top of it.

`Tools/UWSelfCheck` reads a complete installation once and reports what came through -
levels, strings, textures, the 3D models from `UW.EXE`, cutscenes and sound. Use it to find
out whether your copy of the game works with this port before starting Unity at all:

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

The path is a `DATA` folder, a GOG installation folder or `game.gog` itself. The tool is
handy for looking things up, and it keeps the claim honest: if the layer ever grew an
engine dependency, this build would break.

Input, camera, movement, collision, rendering and audio output stay on the Unity side. The
long-term plan is to move more rules across the line, so the game logic can be reused
without Unity.

## How this was made

I have been a fan of Ultima Underworld ever since it came out in 1992. In 2012 I came
across uw-formats.txt, a text file describing the game's data formats, more or less by
chance, and thought: why not give it a try? That was the start of this project, but I
never had the time or the experience to take it further. In 2026, with the help of Claude
(Anthropic's AI model, via Claude Code), I picked it up again. Since then the code has
been written by Claude; I decided what to build, tested every behaviour against the
original game and confirmed or rejected the results.

The application icon (`Art/Icon/ur-icon.svg`, rendered to `Assets/UWIcon/UWRIcon.png`) was
also made with generative AI (Claude). It shows the runes uruz and raidho (U and R), drawn
after the historical Elder Futhark; nothing in it is taken from the game.

## License

Underworld Revisited is released under the MIT License, see [LICENSE](LICENSE). Parts are
derived from other projects under their own licenses, see
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
