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

### 4.1 The game object, and switching at run time

`UWGame { Uw1, Uw2 }` names the game. Everything that differs between the two games hangs off
one object per game, `IUWGameProfile`, with `UWGameProfileUw1` and `UWGameProfileUw2`: the
formats (4.2), the rule variants and the registrations (4.3), the classic layout table and the
screens (4.4), the story anchors, the rule set's defaults, the cutscene and music numbers.
`UWGameData` is built for one profile, and the session reaches it as `session.Game`. Nothing that
belongs to one game lives in a static.

**One installation holds both games.** GOG's "Ultima Underworld 1+2" carries both in one
`game.gog`, UW1 in the image's `UW` folder and UW2 in `UW2`. So the game folder the player
chooses today serves both: `UWGogInstall` finds both programs in it, extracts each into a cache
of its own (`GameData/UW1`, `GameData/UW2`), and keeps each game's save folder. A game whose
folder is missing is simply not offered.

**The game is switched at run time** (decided 2026-10-27), in the main menu and from the game
menu, without a restart. A switch is the same as quitting to the main menu and starting a game:
the running session ends (with the usual question whether to save), the world is torn down, and
a new `UWGameData` and session are built for the other profile; textures, atlases, fonts, the HUD
art, the music and the language's game texts follow. What this needs:

- no per-game state in statics: today's shared caches (`UWLevelLoader`'s data, models and atlas,
  `UWMainFrame`'s picture, `UWSettings`' paths) are keyed by game or rebuilt on the switch;
  registries (`UWTrapRules.Traps`, the conversation function table) are built per profile;
- settings split into the player's (controls, graphics, sound, language - shared) and the
  game's (last save, rules file, game folder state - per game);
- the switch as one path in code, `UWLevelLoader` building a game from a profile, which the
  first start takes as well, so it is tested every time the game starts.

The self-check gets a data path per game (`UW_DATA` for UW1 and a second variable for UW2, or the
`game.gog` both come from).

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

### 4.3 Rules: one code path where the games agree, a variant object where they differ

No `if (game == ...)` in rule code. UE counts 688 routines that differ between the games; even if
only a third of them change a rule, switches would put some 200 game tests across the rules, with
no place that says what UW2 does differently, and each one a risk for UW1. Two complete copies of
each rule class (`UWCombatUw1`, `UWCombatUw2`) are no better: most rules are mostly the same
(`player_cast` differs in four of its steps), a fix would reach one copy only, and the rule index
would see every routine in two places - the duplication the cleanup removed.

Four tools instead, by the size of the difference:

| The difference | The tool | Example |
|---|---|---|
| a number | **data**: the rule set, with each game's defaults (`UWRules.CreateOriginal(game)`) | a skill point every 3000 or 1500; the slow cycle 24 or 30 steps; the cast pause base 0x40 or 0x80 |
| a format | **an interface, one implementation per game**, chosen once with the profile | `PLAYER.DAT`, the level layout, the archives (4.2) |
| a rule that differs in some of its steps | **the shared rule, asking a small variant object of its domain** for those steps | the casting check, the experience halving, the poisonous blow |
| a system of one game only | **its own class, registered by that game's profile** | UW2's schedules, jail, pits; UW1's mantras, talismans |

The third row in code: the rule keeps its one flow and asks the game's variant where the games
part.

```csharp
// UWSpellCasting: the flow, written once
int liTarget = pOGame.Magic.CastingTarget(piCircle);      // UW1: 2 * circle, UW2: 3 * circle
if (!pOGame.Magic.MayCastHere(pOPlayer, piCircle)) ...    // UW2: the Britannia limit

// one small variant per game and domain
public interface IUWMagicVariant { int CastingTarget(int piCircle); bool MayCastHere(...); ... }
public sealed class UWMagicVariantUw1 : IUWMagicVariant { ... }   // today's code, moved
public sealed class UWMagicVariantUw2 : IUWMagicVariant { ... }   // read from UE, cited
```

A variant per domain (`IUWCombatVariant`, `IUWMagicVariant`, `IUWPlayerVariant`,
`IUWCritterVariant`, `IUWObjectVariant`, `IUWTrapVariant`, `IUWMotionVariant`,
`IUWConversationVariant`), named "variant" so it is not taken for the rule set's number classes
(`UWRulesMagic` and the rest). So:

- every UW2 difference of a domain stands in one file, in the order of UE's difference list, each
  method citing both games' routines;
- UW1's variant is today's code moved, not changed, and the self-check runs both variants side by
  side;
- every rule of the original still has one home, the shared flow; the rule index learns that a
  variant method is part of its rule.

Number differences go into the rule set, never into a variant; a variant method that only returns
a constant is a rule set entry in disguise.

**The price:** many rule classes are static today (`UWCombat`, `UWSpellCasting`,
`UWExperience`, ...). To ask a variant they need the game, through the session they mostly have
already (`UWGameSession`, `IUWWorld`) or as a parameter. That move comes first (phase 0), domain by
domain, mechanical and pinned by the self-check, as the rule set's was in cleanup phase 10; the
variant objects are introduced with UW1's variant only, so UW1 is proved unchanged before a line
of UW2 is written.

One-game systems: UW2's `UWScheduleRules` (`SCD.ARK`), `UWXClocks`, `UWJailRules`,
`UWPitsRules`, `UWSpecialWeapons`, the new trap kinds as rows of `UWTrapTable`, the new
conversation built-ins as rows of `UWConversationFunctionTable`, the world and level hacks. UW1's
(mantras, talismans, the orb, Garamon) stay and are registered by UW1's profile only.

The story anchors (`UWLevelIds`, `UWQuestPlaces`, `story.*`, `levels.*`) get a UW2 set in UW2's
profile; `rules.json` stays per game (`rules.json` for UW1, `rules-uw2.json` for UW2).

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

### Phase 0: the game object, both games found, the switch (L)

1. `UWGame`, `IUWGameProfile` with UW1's profile only; `UWGameData` and the session carry it.
2. The static rule classes take the game (section 4.3), domain by domain, and the variant
   interfaces come in with UW1's variant - today's code moved. UW1 byte-identical throughout; this
   is the step that carries the risk, so it goes first and alone.
3. The statics that assume one game (the survey's list) keyed by game or moved onto the profile;
   settings split into the player's and the game's.
4. `UWGogInstall` finds both programs in the one `game.gog` (the image's `UW` and `UW2` folders,
   `UW2.EXE` size and hash of the GOG build), a cache and a save folder per game.
5. The switch: the main menu and the game menu offer the other game when it is installed; one
   path builds a game from a profile, the first start included. In this phase UW2's profile is a
   stub that shows its main menu screen and nothing more.
6. Tools: `uwdump info` and the self-check on UW2 (strings, palettes, pictures, critters - what is
   generic already); a data path per game.

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
  structure only, behind pins; UW1's variants and profile are today's code moved, not rewritten;
  the UW1 self-check runs on every change.
- **Size.** 42 levels in several worlds, with schedules and per-world hacks; UW2 is a larger game than
  UW1 and its play-through takes longer.
- **What UE does not know.** A few constants are marked unknown (the text trap's player flag, a
  path reach whose intent is unclear); they are kept as the code has them.
- **Single-game statics** surface late if they are missed in phase 0; the survey's list is the
  start, and the switch at run time finds the rest (a static left over shows as the other game's
  data after a switch). A self-check that switches back and forth and compares makes that a test.
- **Moving the static rule classes onto the game** touches most of the rules before UW2 adds
  anything. It is mechanical and pinned, and it goes first, alone, with UW1's variant only.
- **The legal line** holds only if UE's C is never pasted, not even "just this table"; tables the
  game reads from its data come from the player's own files at run time, as for UW1.

## 8. Decisions for the user

1. ~~One application for both games, or a build per game.~~ Decided 2026-10-27: one application,
   the game switched at run time (section 4.1); GOG's `game.gog` holds both.
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
