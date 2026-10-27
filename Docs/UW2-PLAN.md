# Ultima Underworld II: plan

Status: PLAN, nothing built (2026-10-27). Written from a read of
[Underworld Exhumed](https://github.com/abedegno/underworld-exhumed) (UE, at commit e5deadd) and a
read-only survey of this repository's code. Line numbers in UE are its own and will drift; the
routine and file names will not. Comes after the points under *Next* in
[ROADMAP.md](../ROADMAP.md).

The short answer to "can UW2 be derived from Underworld Exhumed": yes, better than UW1 could be
derived from anything when this port started. UE holds a decompilation of `UW2.EXE` that
rebuilds the shipped program byte for byte, a native port of it that replays twelve recorded DOS
sessions identically, a description of every data file both games read
([docs/FORMATS.md](https://github.com/abedegno/underworld-exhumed/blob/main/docs/FORMATS.md)),
and a list of where the two games' rules differ, routine by routine
([docs/UW1-UW2-DIFFERENCES.md](https://github.com/abedegno/underworld-exhumed/blob/main/docs/UW1-UW2-DIFFERENCES.md)).
What it does not give is the port: this codebase knows exactly one game today, and most of the
work is making it know two without moving UW1 by a byte.

## 1. Sources and the legal line

| Source | What it gives | How we use it |
|---|---|---|
| UE `uw2/src/` | the game's C and assembly, matched to `UW2.EXE`, with names (from the FM Towns build where it has them) and notes | read to learn a rule, cited by file and routine, **never copied** |
| UE `docs/FORMATS.md` | the layout of every data file of both games, from the code that reads it, with corrections to UW-Formats | the format reference for phase 1 |
| UE `docs/UW1-UW2-DIFFERENCES.md` | 1,692 paired routines compared; every rule difference with both games' source lines | the work list for phase 4 |
| UE `docs/behaviour/` | the rules as prose with exact numbers: AI, schedules, combat, magic, conversations, player upkeep, traps | readable entry to each domain |
| UE `uw2/docs/FINDINGS.md`, `subsystems/*.md` | likely bugs of the original, engine details, one page per subsystem | context; the bugs are kept "as it plays", as for UW1 |
| UE `uw2/vectors/*.csv` | inputs and outputs of eleven rules, computed by running `UW2.EXE`'s own code (numbers only) | self-check fixtures (decision 3) |
| UE's `uw2port` | the original's code running natively, proved against DOS | a faster reference than DOSBox for side-by-side checks |
| hankmorgan's UWReverseEngineering and UnderworldGodot | the disassembly with named routines; a recreation of both games (MIT) | reading aid, as for UW1; UE lists where UnderworldGodot is one rule for both games or differs |

The line stays where it is for UW1 (README, *Thanks*; CONTRIBUTING, *What must not go in*): the
decompiled code is derived from `UW2.EXE` and UE's MIT licence grants no right in it (UE's
`NOTICE`). We read it to learn what the game does and write our own code, citing the routine, as
we do with the UW1 disassembly. UE's documentation, tools and vectors are its own work under MIT.
Nothing of the game - data, text, art - goes into the repository.

## 2. UW2 in numbers, against UW1

From UE's FORMATS.md and DIFFERENCES.md unless marked otherwise.

| | UW1 | UW2 |
|---|---|---|
| Program | `UW.EXE` | `UW2.EXE` (GOG image: folder `UW2`) |
| Levels | 9 | 80 slots in worlds of 8 (`(level - 1) / 8` is the world); 42 shipped levels |
| `LEV.ARK` | 135 blocks, no compression, offsets only | 320 blocks; header with flags, lengths and room; blocks LZSS-compressed (Okumura 1989, 4 KB ring) |
| Level block | 0x7C08 | 0x7E08: the same, plus 64 animation overlays and 64 timers |
| Texture map | 0x7A: 48 walls (`W64.TR`), 10 floors (`F32.TR`), doors | 0x86: 64 textures of `T64.TR`, doors |
| `TERRAIN.DAT` | walls and floors, 512 words | one word per `T64.TR` texture; class water / lava / ice, currents, slipperiness |
| Automap byte | kind in bits 6-7, terrain in 4-5 | the two swapped; terrain class water / lava / ice |
| `PLAYER.DAT` | 0xD2-byte record, xor key + 3n | 0x37D-byte record, a chained cipher; the same to 0x5D, then 128 quest flags, 16 quest bytes, 256 16-bit variables, 16 X clocks |
| Game in progress | the save slot | a working folder `SAVE0` (pristine `LEV.ARK` and `SCD.ARK` copied in), saved by copying to `SAVE1`-`SAVE4` |
| Schedules | none | `SCD.ARK`: 16 schedules, rows of timed events per level (goals, teleports, kills, quest bits, triggers, variables) |
| Screens | `*.BYT`, raw | `BYT.ARK`, compressed blocks (game screen, conversation, map, chargen, menu, logos, end) |
| Palettes | 8 | 11; `DL.DAT` adds a minimum light per level |
| Sound effects | 24, 5 bytes | 49, 8 bytes; digital `SPnn.VOC` / `UWnn.VOC`; cutscene speech `BSPnn.VOC` |
| Music | `UWnn.XMI` / `AWnn.XMI` | `UWRnn.XMI` / `UWAnn.XMI` (octal numbers) |
| Conversations | 97 blocks, 15 generic | 102 blocks (compressed), no generic ones; 9 more built-ins; 6 trade slots a side |
| Cutscenes | `CUTS\csNNN.nXX` | the same format |
| 3D models | in `UW.EXE` | in `UW2.EXE`'s renderer data (seg052) |

The rules: of the paired routines, 646 are identical, 358 differ in constants only, 688 differ in
code, 364 are UW1's alone and 482 UW2's alone. UE estimates that fewer than a third of the
differing ones in the rule subsystems change a rule; the rest is edited C. Systems in one game
only: UW1's mantras and shrine, silver tree, talismans and Garamon, Tybal's orb, rotworm stew,
incense, the dragons; UW2's schedules and X clocks, the pits of Carnage, castle guards and jail,
the Void by the dream plant, vending machines and the other world hacks, timer triggers, nine new
trap kinds, special weapons, the enchantment, study-monster and mend spells, the key ring.

## 3. What this codebase brings

From the survey (2026-10-27). **There is no notion of a game**: no game id, and the data root is
a path. About 178 files reference UW1's id and layout classes (`UWObjectIds`,
`UWObjectMechanics`, `UWLevelIds`, `UWQuestPlaces`, `UWLevArkLayout`, `UWStringIds`) or literal
string blocks.

Generic already, by format or by design:

- the ISO reader of `game.gog`; .GR / .TR / critter art decoding; palettes, `XFER`, `SHADES`,
  `LIGHT`; `STRINGS.PAK`; fonts;
- the conversation VM and the name-keyed function table (`UWConversationFunctionTable`), the
  trap and a_do registries (`UWTrapTable`), the item-use and food tables;
- the cutscene script interpreter (`UWCutsceneScript`, `UWCutsceneRunner`);
- XMI, OPL2, VOC, General MIDI and MT-32 drivers;
- the session (`UWGameData`, `UWGameSession`), `IUWWorld`, the damage pipeline, the motion core
  (`UWPlayerMotion`, `UWMobileObjectMotion`, `UWCreatureMotion`);
- the rule set mechanism (`UWRules`, `rules.json`); the language files (`UWText`, `IUWGrammar`);
- the interface kit and the HUD layout and art helpers, several of which say so in their
  comments (`UWHudLayout`, `UWHudArt`, `UWBagPlacement`, `UWModernLayout`).

Bound to UW1:

- install and cache: `UWGogInstall` (cache folder `UW1`, extracts the image's `UW` folder, the
  `UW.EXE` size and hash, `UNDEROM1`), `UWSettings` (one set of paths), `Tools/Shared/DataPath`;
- formats: `UWLevArkLayout` (all `const`: 9 levels, block bases, 0x7C08), no LZSS anywhere,
  `UWPlayerData` (fixed offsets, the UW1 cipher), the texture map and `TERRAIN.DAT`
  readers, the four `.TR` files and the fixed `.BYT` names of `UWTextures`, `SOUNDS.DAT`,
  the music names, `UW3DModelImport` (fixed `UW.EXE` offsets);
- ids and story: `UWObjectIds`, `UWObjectMechanics`, `UWLevelIds`, `UWQuestPlaces`,
  `UWRulesStory`, `UWRulesLevels`, `UWTalismans`, `UWEndgameRules`, `UWTybalOrbRules`,
  `UWVoidRules`, `UWSpecialDeaths`; the contents of the trap and a_do tables; the 55 spells of
  `UWRunicMagic`; shrines; dreams; cutscene and music numbers;
- presentation: `UWClassicLayout` (about 174 positions measured on `MAIN.BYT` and friends),
  `UWMainFrame`, the paperdoll, the intro;
- statics built once for one game: `UWTrapRules.Traps` / `DoActions`, the conversation function
  table, `UWLevelLoader`'s shared data, models and atlas, `UWSettings`' path caches,
  `UWMainFrame`'s picture;
- tools and checks: 26 self-check files take the data path and pin UW1 values; `UWRuleIndex` and
  `UWOriginalCoverage` work on the UW1 disassembly; the editor's level window bakes 9 levels.

## 4. Architecture

### 4.1 A game id

`UWGame { Uw1, Uw2 }` on `UWGameData` (set when the installation is read: `UW.EXE` or
`UW2.EXE`), so every rule reaches it through its session. One running game at a time; the main
menu (or the command line) chooses which, when both are installed. Everything per game hangs off
it: the install folder and cache (`GameData/UW1`, `GameData/UW2`), the save folder, the settings
that belong to a game (last save, rules file), the self-check's data path (`UW_DATA` and a second
variable for UW2).

### 4.2 Formats behind the id

- **Archives**: one `UWArk` reader for both layouts, with the UW2 header and LZSS decoding; an
  LZSS encoder for writing (decision 4). `CNV.ARK` and `BYT.ARK` go through it too.
- **Level layout**: `UWLevArkLayout` from constants to an instance per game (level count 80,
  block bases, block size, overlays and timers at 0x7C08 / 0x7D88). The codec and the writer take
  the instance. UW1's instance has today's numbers; a pin checks that.
- **Texture map and terrain**: a per-game reader behind one interface (UW1: walls and floors;
  UW2: 64 textures of `T64.TR` and the terrain word with its class).
- **Player record**: `UWPlayerData` splits into the common head (to 0x5D) and a per-game tail
  with its cipher. UW2's quest flags, bytes, numbered variables and X clocks become session
  state beside UW1's.
- **Pictures, sound, music**: `BYT.ARK` blocks instead of named `.BYT` files; `SOUNDS.DAT` with 8
  bytes an entry; the UW2 music names.
- **3D models**: `UW3DModelImport` with the offsets as data per supported `UW2.EXE` build (the
  GOG one), found by the same method as for UW1; size and hash checked.

### 4.3 Rules: one code path where the games agree, branches where they differ

The rule classes stay shared. For each difference in UE's list there are three cases:

1. **A number** (skill point every 3000 or 1500, the slow cycle 24 or 30 steps, the cast pause
   base 0x40 or 0x80): the rule set already holds most of these. `UWRules.CreateOriginal(game)`
   gives each game its defaults; the code stays one path. New coefficients where UW2 changes a
   formula's terms within the same shape (carrying capacity `str * 20` against
   `str * 13 + 300` is a factor and a base).
2. **A different shape** (casting check, experience halving, area spells by table, the poisonous
   blow): a branch on the game in the rule, citing both games' routines, as UE does.
3. **One game only**: its own class. UW2's: `UWScheduleRules` (`SCD.ARK`), `UWXClocks`,
   `UWJailRules`, `UWPitsRules`, `UWSpecialWeapons`, the new trap kinds as rows of
   `UWTrapTable`, the new conversation built-ins as rows of `UWConversationFunctionTable`, the
   world and level hacks. UW1's (mantras, talismans, the orb, Garamon) stay and are simply not
   registered for UW2.

The registration tables built once as statics (`UWTrapRules.Traps`, the function table) become
per game, built when the session's data is known. The story anchors (`UWLevelIds`,
`UWQuestPlaces`, `story.*`, `levels.*`) get a UW2 set; `rules.json` stays per game
(`rules.json` for UW1, `rules-uw2.json` for UW2).

### 4.4 Presentation

- **Classic interface**: UW2's screen has its own layout and art (`BYT.ARK` block 4, its own
  panels, no dragons; the positions are to be measured). `UWClassicLayout` gets a UW2 table
  beside UW1's, measured the same way; the shared view models of cleanup phase 5 draw both.
- **Modern scheme**: built from engine-free descriptions, so it should carry over with UW2's art;
  the presets need a look.
- **Cutscenes**: the same player; the numbers per game. UW2's intro, the Guardian's cutscenes and
  speech from `BSPnn.VOC`.
- **Character creation, main menu, conversation screen, map**: UW2's `BYT.ARK` screens and its
  `CHRGEN.DAT` / `SKILLS.DAT`.
- **Render modes**: Palette and Remastered need UW2's terrain classes (ice), the larger texture
  set and `DL.DAT`'s minimum light.

### 4.5 Translations

`STRINGS.PAK` has the same format, so the game-text overlay works as it is. The language files of
the game's texts need the game in their name (`<code>.strings.tsv` for UW1, `<code>.uw2.strings.tsv`
for UW2); the port's own texts (`<code>.tsv`) are shared, with UW2's own frames added. `uwdump`'s
translator commands take the game from the installation.

## 5. Phases

Each phase ends buildable and releasable, and **UW1 does not move**: every UW1 self-check, pin and
fixture stays byte-identical, and UW1 releases can go on in between. Sizes are relative (S, M, L,
XL); hours are estimated once phase 1 shows the pace.

### Phase 0: two games in one installation (M)

1. `UWGame` and its detection; `UWGogInstall` reads the image's `UW2` folder and `UW2.EXE`
   (size and hash of the GOG build), a cache per game, the save folder per game.
2. `UWSettings` and the setup screen with a path per game; the main menu chooses the game when
   both are installed.
3. Tools: `uwdump info` and the self-check on a UW2 installation (strings, palettes, pictures,
   critters - what is generic already); a second data-path variable.
4. Statics that assume one game listed and either moved onto the session or keyed by game.

### Phase 1: the formats (M)

1. `UWArk` with the UW2 header and LZSS (decode; encode per decision 4), round trip on the
   shipped `LEV.ARK`, `CNV.ARK`, `BYT.ARK`.
2. The level layout as an instance; UW2's block with overlays and timers; texture map,
   `TERRAIN.DAT`, the automap byte.
3. `PLAYER.DAT` for UW2: record, cipher, the inventory workspace; a save written by the original
   loads and writes back identically.
4. `SCD.ARK`, `DL.DAT`, `BYT.ARK` screens, `SOUNDS.DAT`, the music names.
5. The 3D models from `UW2.EXE`.
6. `uwdump level`, `maprender`, `conv` and `strings` on UW2; the self-check's UW2 group with
   UE's vectors as fixtures (decision 3).

### Phase 2: a walk through Britannia (L)

The world without the game's rules: a UW2 level loads in Unity and renders in both modes, the
player walks with UW2's motion (speeds in twentieths, ice and currents, no fall damage in world
8), doors open, objects and creatures stand where they belong, the automap fills in, level
changes work across worlds. Creatures stand still. Saving and loading the walk.

### Phase 3: the screen and the hands (L)

The UW2 classic interface (layout table, panels, compass, gem, flasks, the message scroll),
inventory with UW2's containers (key ring, quiver, scroll case), character creation and the main
menu, conversations (the VM with UW2's built-ins, 6 trade slots, the refusals), the map with its
UW2 rules. Then the modern scheme on UW2.

### Phase 4: the rules (XL, domain by domain)

UE's difference list as the work list, each domain with its self-check cases, in the order the
game needs them:

1. Player: experience (halving, 1500 per point, the 0x7FFF0 cap), carrying, the slow cycle and
   the X clocks, food, sleep, death (jail, the pits, the blackrock gem), traps on objects.
2. Combat: the attack score with Valor, enchantments and special weapons, Poison Weapon, the
   poisonous blow, resistances, damaging doors and owned objects, combat sounds.
3. Magic: the casting check, circles of 8 and the Britannia limit, the area spell table, class 7
   with cursor aiming, healing the caster, the new spells, the Academy's mana.
4. Creatures: goal 15, doors three in four, paths of 40, talkers that turn guard, death cries,
   owners up to race 0x1D, animation lengths.
5. Objects and traps: treasure, combining, food leftovers, lights, beds, owned containers; trigger
   classes 2 and 3, the numbered variables, the nine new trap kinds, the hack traps, create-object
   from the adventurer template, terrain changes, timer triggers, time stop.
6. Schedules: `SCD.ARK` running on the X clocks, the level clocks, the events.
7. The world and level hacks (`WORLDEV.C`, `do_level_hacks`): vending machines, the Prison
   Tower's alarm, Killorn Keep's crash, the Academy's wand, Praecor Loth's liches, the Void.

### Phase 5: the story and the sound (L)

Cutscenes and speech, the intro and the ending, dreams by the castle clock, the Ethereal Void,
music themes per world and situation, sound effects with digital samples. A full play-through
compared with the original (DOSBox, or UE's `uw2port`), as UW1's was.

### Phase 6: the port's own things for UW2 (M)

Remastered details (ice, the new texture set), the help window (stats, map, notes, spells),
`rules-uw2.json` and its template, the translation kit for UW2's texts, the gamepad and the modern
presets checked on UW2, the documentation (README, CONTROLS, DEVIATIONS) for two games.

## 6. Checking against the original

- UE's vectors as self-check fixtures for the eleven rules they cover (`check_res`, `skill_check`,
  `debris_type`, `move_along`, ...), and the same method of comparing against the original's
  outputs where we can produce them.
- Save games written by the original: load, play, save, and load again in DOS.
- UE's `uw2port` replays a recorded input exactly; where a behaviour is in doubt, it is a faster
  way than DOSBox to see what the original does, and it is the original's code.
- The rule index: `Tools/UWRuleIndex` scans our comments for UW1's routine labels. For UW2 the
  comments cite UE's file and routine (`UW2 src/combat/RUNES.C player_cast`); the index learns
  that form, so "where is UW2's rule X" has the same answer as for UW1.

## 7. Risks

- **UW1 regressions through shared code.** The main risk. Mitigation: phase 0 and 1 change
  structure only, behind pins; every branch defaults to today's path for UW1; the UW1 self-check
  runs on every change.
- **Size.** 42 levels in several worlds, with schedules and per-world hacks; UW2 is a larger game than
  UW1 and its play-through takes longer.
- **What UE does not know.** A few constants are marked unknown (the text trap's player flag, a
  path reach whose intent is unclear); they are kept as the code has them.
- **Single-game statics** surface late if they are missed in phase 0; the survey's list is the
  start.
- **The legal line** holds only if UE's C is never pasted, not even "just this table"; tables the
  game reads from its data come from the player's own files at run time, as for UW1.

## 8. Decisions for the user

1. **One application for both games**, the game chosen at start (proposed), or a separate build
   per game.
2. **UE's decompiled C as the reference for UW2**, read and cited, never copied (proposed; the
   same rule as for the UW1 disassembly).
3. **UE's vectors as self-check fixtures** (proposed): they are numbers computed by the original's
   code, published under MIT for checking reimplementations; credited in THIRD_PARTY_NOTICES.
4. **Writing UW2's `LEV.ARK`**: compressed like DOS (proposed: closest to the original, needs an
   LZSS encoder), or stored uncompressed (DOS reads both).
5. **The modern scheme and the port's additions** for UW2 from phase 3 on (proposed), or the
   classic game first and the additions after phase 5.
6. **Releases**: UW2 work in `main` behind the game id while UW1 releases go on (proposed), or a
   long-lived branch.
