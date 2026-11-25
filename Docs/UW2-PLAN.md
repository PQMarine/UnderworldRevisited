# Ultima Underworld II: plan

Status: PHASE 0 COMPLETE 2026-10-28 - steps 1 to 6 built (the game id, the profile with the first
game's, the game data and the session carrying it; the rules asking the game's variants in seven
domains, UW1's variants today's code moved; the registries, the caches and the paths per game,
the settings sorted into the player's and the game's; both games found in the one game.gog,
each extracted into a cache of its own; the switch in the Game window, UW2 showing its main menu
screen - confirmed in the game by the user; the tools and the self-check on UW2's generic data).
Decisions 2 to 4 taken 2026-10-28 (as proposed); PHASE 1 COMPLETE 2026-10-29 (the formats: archives
and LZSS both ways, LEV.ARK per game, PLAYER.DAT, schedules, light, screens, sounds, music, 3D models,
the tools, UE's vectors). PHASE 2 COMPLETE 2026-10-29 (a walk through Britannia: UW2's data and
world, its levels in Unity, walking with its motion, doors, level changes, saving and loading both ways
with the original - confirmed in the game by the user). STATE 2026-11-19 (header corrected, it still
said phase 3 was next): phases 0-4 complete (G0-G6), phase 5 parts A-D confirmed 2026-11-18; built and
confirmed 2026-11-19: the special weapons' powers, held creatures ignore blows (goal 15), the portcullis
takes blows, the inventory look's Lore target 8. Next: 5E (a full play-through against the original),
THE MODERN INTERFACE FOR UW2 (decided per user 2026-11-19: before the beta - what phase 3 step 8 built is a
stopgap, UWModernHudUw2; missing are the first game's windows: the character and rune panels, the bag windows,
the presets Modern, Classic Modular and Classic Wide, the layout editor, the backs, the automatic key, the
windows with the free pointer, the [MORE] details 86.2/86.3; PLANNED 2026-11-23, see "The modern interface for
UW2: one interface, two hosts" before phase 6), then the beta gate (the classic scheme against
the original, phase 6), phase 6's docs, then the second cleanup as a long branch. Written 2026-10-27 from a read of
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
  game's (last save, game folder state - per game). The files of a game already have a folder
  of their own beside `settings.json` (`UWGameFolder`, since 2026-10-27): `UW1/rules.json` and
  `UW1/Music` (the player's recordings), so UW2's go into `UW2/`; `settings.json`,
  `graphics.json` and the MT-32 ROMs stay shared;
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
profile; `rules.json` is per game in the game's folder (`UW1/rules.json`, `UW2/rules.json`).

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
   BUILT 2026-10-28: `UWGame`, `IUWGameProfile`, `UWGameProfileUw1` (the image folder, `UW.EXE`
   with its size and hash, the cache, save and game folders - the literals `UWGogInstall` and
   `UWGameFolder` held, which now read them from it), `UWGameProfiles`, `UWGameData.Profile`,
   `UWGameSession.Game`; the self-check group GameProfile.
2. The static rule classes take the game (section 4.3), domain by domain, and the variant
   interfaces come in with UW1's variant - today's code moved. UW1 byte-identical throughout; this
   is the step that carries the risk, so it goes first and alone.
   BUILT 2026-10-28. The way: a rule reaches its game through the session it already has
   (`IUWWorld.Session`, every world's being the current one), not a new static; a number that
   differs is a rule set entry, each game's default from its profile
   (`IUWGameProfile.CreateOriginalRules`, the session's rule set made by it); the steps that
   differ, per UE's `UW1-UW2-DIFFERENCES.md`, go into a variant. MAGIC DONE: `IUWMagicVariant`
   (may cast at all, may cast here, pays on release, too soon) with `UWMagicVariantUw1`, asked by
   `UWSpellCasting.CastRunes`; a check with a test game whose variant refuses proves the flow asks
   it. COMBAT DONE: `IUWCombatVariant` (the weapon enchantment, the poisonous blow against the
   armour, an unresisted type in `check_res`, damage_object's first test, the debris) with
   `UWCombatVariantUw1`; small static helpers reach it through the current session, as they reach
   the rule set; what only UW2 has (Valor, Poison Weapon, special weapons, sounds by weapon kind)
   gets its methods with UW2's variant in phase 4. PLAYER DONE: `IUWPlayerVariant` (the
   experience gate and gain, where one may sleep, the trap targets) with `UWPlayerVariantUw1`;
   the numbers (skill points, food, the interrupted sleep, the new character) are rule set
   entries, death is each game's own system. CREATURES DONE: `IUWCritterVariant` (whether a
   creature tries a door it bumped into, the death cry) with `UWCritterVariantUw1`, which the brain
   reaches through its services (`ICritterServices.Game`); the path flood, the owners' races and
   the corpse's chance are rule set entries. OBJECTS AND TRAPS DONE: `IUWObjectVariant` (a light
   used up, the valuable a critter carries, the pick's roll) and `IUWTrapVariant` (the fishing
   bite, which gets the Track skill for UW2's rule; whether the door trap swaps the lock) with the
   first game's variants; change terrain's bounds are rule set numbers; the trigger classes, the
   variable traps, UW2's new traps, the hack traps and the plot deaths come in phase 4. MOTION
   DONE: `IUWMotionVariant` (a speed's share by the motion state - UW1 the rule set's tenths, UW2
   twentieths with swimming from the skill; whether the lava spares the player; an impact's
   damage) with `UWMotionVariantUw1`, asked by the player motion, the terrain rules and the
   object and creature motion through their world's session; the turn divisor, the view's swing
   and a bounce's speed are rule set numbers; world 8's falls, paralysis, ice and currents come
   in phase 4. CONVERSATIONS DONE: `IUWConversationVariant` (who answers whatever it is doing,
   whether the partner gives in to a demand, whether giving in lowers the attitude) with
   `UWConversationVariantUw1`; the quest bits and bytes, x_traps' range and the trade slots are
   each game's data or rule set numbers. THE STEP'S RESULT: eight variants on the profile for the seven domains (magic,
   combat, player, critter, objects, traps, motion, conversation), each with a
   spy in the GameProfile checks that proves the flow asks it and value checks that pin UW1's
   answers; the self-check, the sweep and the Unity build green after every domain. Still reached
   through `UWGameSession.Current` in the static helpers (UWClickRules, UWCritterLoot, UWLockRules,
   UWItemUse, UWPlayerTerrainRules, UWFishing, the combat helpers): step 3 and phase 4 take the
   session in as each of them gets a second game to serve.
3. The statics that assume one game (the survey's list) keyed by game or moved onto the profile;
   settings split into the player's and the game's.
   BUILT 2026-10-28, the survey's list item by item. REGISTRIES ON THE PROFILE: the trap kinds, the
   a_do actions and the conversation functions were statics built once (`UWTrapRules.Traps`,
   `DoActions`, `UWConversationSession`'s table); now `IUWGameProfile.TrapKinds`, `DoActions` and
   `ConversationFunctions`, which UW1's profile builds on first use with the old builders
   (`UWTrapRules.CreateTrapTable`, `CreateDoActionTable`, `UWConversationFunctionTable.CreateDefault`);
   a firing reaches its game's tables through its world's session, a conversation through its
   session. THE RUNNING GAME: `UWSettings.Game` (UW1's profile until the switch of step 5 sets it)
   is the one place the engine side names the game before a session exists. CACHES BY GAME:
   `UWLevelLoader` reads its shared data for that game (`DataImport` takes the profile) and reads
   anew when another game runs, the atlas, the texture array and the 3D models going with it;
   `UWMainFrame`'s picture was keyed by its `MAIN.BYT` texture already, so another game's data
   decodes anew without a change. PATHS BY GAME: `UWSettings`' resolved paths (the extracted DATA
   folder, the save folder, the image's error) are one record per game instead of one set of
   statics, the program's name is the profile's, and the developer's `DataPath` and
   `SavegameFolder` overrides name the first game's folders; the image path stays one, as the
   installation holds both games. THE SETTINGS SPLIT: `settings.json` is the player's - every
   entry of `UWUserData` was read through: controls, display, graphics, motion, sound, interface,
   language and the GOG folder, which serves both games. What is a game's lies in its folder
   beside it (`UWGameFolder.Under(root, game)`: rules.json, the music recordings) or is resolved
   per game (above); a rules.json an earlier version left beside settings.json is adopted by the
   first game's folder only. No per-game settings file yet: no entry needs one, and nothing like a
   last save is kept today. The candidate is the modern layout (`ModernLayout`, positions of UW1's
   HUD pieces), once decision 5 says whether UW2 gets the modern scheme. Checks: a test game's own
   trap table runs a fired trap and its own function table answers a conversation (the first
   game's function missing there); the first game's tables are built once; another game's folder
   leaves the old rules.json alone. Left static on purpose: `UWVariableTrapOperations` (the same
   operations in both games; UW2's own variable traps come in phase 4) and
   `UWGameSession.Current`, which the step 2 helpers still read.
4. `UWGogInstall` finds both programs in the one `game.gog` (the image's `UW` and `UW2` folders,
   `UW2.EXE` size and hash of the GOG build), a cache and a save folder per game.
   BUILT 2026-10-28. READ FROM THE IMAGE FIRST (the user's install, GOG's Ultima Underworld,
   product 1207662463): its `game.gog` is the 1+2 CD, root `UW` and `UW2`; `UW2` holds CRIT, CUTS,
   DATA, SOUND and `UW2.EXE` as plain files like `UW` (DATA with `LEV.ARK`, `T64.TR`, `BYT.ARK`,
   `CNV.ARK`, `SCD.ARK`, no `.lzh` to unpack); `UW2.EXE` is 675184 bytes, SHA-1
   `41a23de0fa85c375920ae5d0dbd2ec5f4657c758`; the image's `UW2.BAT` looks for `UNDEROM2` as
   `UW.BAT` looks for `UNDEROM1`. THE SECOND PROFILE: `UWGameProfileUw2` with those install facts
   (title, image folder, program, size, hash, cache `UW2`, saves `UNDEROM2`, game folder `UW2`);
   its rules, variants and tables throw `NotSupportedException` until phase 4, so nothing plays it
   with the first game's rules by mistake. `UWGameProfiles` holds both (`Uw2`, `All`, `Of`). THE
   INSTALL CODE PER GAME: `UWGogInstall.EnsureExtracted`, `GetSavegameFolder`, `ReadDataFile`
   and `IsSupportedExe` take the profile, the error texts name its folder, program and title, and
   the copied constants of the first game's build went; `HasGame` says whether the image holds a
   game (its folder with the supported build of its program), for step 5's offer. `UWSettings`
   extracts and looks for saves for the running game; the level loader checks the running game's
   program before reading 3D models; the tools stay on the first game until step 6. Checks: the
   second profile's values and its rules throwing; the image holds both games and not another
   build; UW2's DATA read straight from the image (`T64.TR` there, not in UW's); UW2 extracts into
   `UW2/DATA` of the tools' cache with CRIT, CUTS, SOUND and the supported `UW2.EXE`; each game's
   saves are its own folder beside the image. OPEN, for step 5 or when UW2 first saves: this install
   has no `UNDEROM2` (GOG's UW1 product ships the 1+2 image but only UW1's save folder; whether
   GOG's UW2 product brings its own image and `UNDEROM2` is not checked). Without it the saves fall
   back as the first game's would without `UNDEROM1`: the folder above the extracted DATA, the
   game's cache folder. Whether to create `UNDEROM2` beside the image instead is the user's call.
5. The switch: the main menu and the game menu offer the other game when it is installed; one
   path builds a game from a profile, the first start included. In this phase UW2's profile is a
   stub that shows its main menu screen and nothing more.
   BUILT 2026-10-28, CONFIRMED IN THE GAME (per user, 2026-10-28: the switch both ways, the
   screen, the bar over it, a restart keeping the choice). THE SCREEN: UW2's main menu is block 5
   of `BYT.ARK` in palette 2 of `PALS.DAT` (UE FORMATS.md "Pictures"; MAINMENU.C:150-160 reads both
   and then draws the OPBTN buttons, which wait for phase 3). `UWLzss` decodes the original's LZSS
   (Okumura's, as FORMATS.md "Compression" describes it), `UWArkUw2` reads the UW2 archive layout
   (read only; phase 1 joins it with UW1's into one reader), `UWScreensUw2.MainMenu` gives the
   screen as a picture. A scratch Python decoder written apart from the C# one gave the same 64000
   bytes (SHA-1 pinned in the GameProfile group) and a picture checked by eye: the title over grey
   stone. THE SWITCH: the player's choice is `settings.json`'s `Game` (empty for the first; a new
   settings fixture `game-2026-10-28.json`, every fixture re-recorded with the editor oracle);
   `UWGameSwitch` lists the games the installation holds (`UWGogInstall.HasGame` on the image,
   `UWSettings.FindGogImage` finding it without extracting; the first game only without an image),
   `Chosen` is the stored game when the installation holds it, else the first, and `Switch` keeps
   the choice and starts the scene again. THE ONE PATH: `UWLevelLoader.Start` sets
   `UWSettings.Game = UWGameSwitch.Chosen()` before anything reads data, then builds the game as
   always, or - for a profile that is not playable yet (`IUWGameProfile.IsPlayable`: UW1 yes, UW2
   no) - shows its main menu screen through the main menu's new screen-only mode, without music,
   world or data behind it; a screen that cannot be read starts the first game instead. THE MENU:
   a Game row in the bar's Game window (the games the installation holds, the running one marked),
   usable over the main menu only, as the game folder is: in a running game a switch would end it
   without the save question, which our Quit does not ask either; over the screen-only menu the
   bar shows the Game window only. `UW_DATA` naming a DATA folder serves the first game only. Texts
   in English and German.
6. Tools: `uwdump info` and the self-check on UW2 (strings, palettes, pictures, critters - what is
   generic already); a data path per game.
   BUILT 2026-10-28. A DATA PATH PER GAME: `Tools/Shared/DataPath.Resolve` and `Find` take the
   game (without one the first, as before; a DATA folder given directly is taken as it is, and
   only the first game's is found by `UW_DATA` naming one), `FindExe` the game's program. THE
   SELF-CHECK takes UW2 from the same `game.gog` into the tools' cache (`CheckContext.Uw2DataPath`,
   null without it) and its group Uw2Data reads it where the readers are generic: `STRINGS.PAK`
   (136 blocks, 13319 strings; block 4's object names at id + 1 as in UW1), `PALS.DAT` (11
   palettes), every `.GR` file but `PANELS.GR` through `UWTextures.ReadImageFile` (34 files, 1717
   pictures, 28 absent entries whose offset is the file's end - UW1's reader built the same
   pictures inline, now through the same method), the four archives with every compressed block
   decoding to its declared length (`LEV.ARK` 320 blocks, 75 compressed; `CNV.ARK` 320, 102;
   `BYT.ARK` 11, 9; `SCD.ARK` 16, none). NOT GENERIC, said rather than read: `PANELS.GR` (no picture
   headers, another size than UW1's) and the critters (`AS.AN`, `CR.AN`, `PG.MP`, pages `CRxx.y` of
   their own format, as UnderworldGodot reads them apart from UW1's) - phase 1 and 3 read them; the
   check holds that the UW1 critter reader finds nothing there. `uwdump --game uw2 info` prints the
   same survey; the other commands say they read the first game only so far.

### Phase 1: the formats (M)

1. `UWArk` with the UW2 header and LZSS (decode; encode per decision 4), round trip on the
   shipped `LEV.ARK`, `CNV.ARK`, `BYT.ARK`.
   BUILT 2026-10-28. `UWArk` reads and writes both layouts whole (`IUWGameProfile.ArchiveLayout`):
   UW1's offsets only, UW2's offsets, flags, lengths and room (it replaces phase 0's read-only
   `UWArkUw2`). Writing follows `put_arc` (UE sys/ARC.C:203): a block flagged for compression is
   kept compressed only when the stream is shorter than the data (`CompressLZW_disk`, LZSS.C),
   keeps its room when it fits (with bit 2 up to it, else exactly), grows to its length plus 15%
   with bit 2 otherwise and then goes to the file's end, as `put_arc` appends it; blocks keep the
   order they lay in (UW1's `SAVE1` has a block appended after the others). `UWLzss.Encode` is
   Okumura's encoder with his binary trees (public domain, 1989; written for us, nothing copied).
   THE SHIPPED FILES AS EVIDENCE: Okumura's encoder gives every shipped compressed stream back byte
   for byte (UW2 `LEV.ARK` 75, `CNV.ARK` 102, `BYT.ARK` 9 - first in a scratch Python script, then
   in the self-check), so it is the original's choice of matches; the shipped UW2 archives lie in
   block order without gaps, `LEV.ARK`'s level blocks with 5% room (written by Origin's tools, not by
   `put_arc`'s 15%). Self-check group Archives: LZSS round trips (empty, a byte, runs, noise); the
   writer's rules (room kept, grown by 15%, plain without gain, an outgrown block at the end, UW1's
   layout); read-and-write gives back byte for byte UW1's `LEV.ARK` and `CNV.ARK`, the five save
   games' `LEV.ARK`, and UW2's four archives; and every UW2 block written again through the writer
   (compression, flags, room) gives the shipped file back. NOT YET: UW1's own readers and writer
   (`UWLevArkCodec.ReadBlockOffsets`, `UWConversations`, `UWLevelWriter`, `UWSaveCodec`) still
   read and write their archives in place - they move onto `UWArk` with the level layout as an
   instance (step 2), UW1 byte-identical.
2. The level layout as an instance; UW2's block with overlays and timers; texture map,
   `TERRAIN.DAT`, the automap byte.
   BUILT 2026-10-28. `UWLevArkFile`, one instance per game (`IUWGameProfile.LevArk`): the level
   count, which block holds which kind, the level block's and the texture map's length, and the
   second game's overlays and timers inside the level block (0x7C08 and 0x7D88; UE FORMATS.md
   "LEV.ARK", level.h:28-29). The first game's numbers moved out of `UWLevArkLayout` unchanged (the
   constants pin shows each value as before), which keeps what both games share - the block's first
   0x7C08 bytes (`CommonBlockSize`). `UWSaveCodec` loads and writes with the game's instance.
   READERS (engine-free, for the second game until phase 2 builds its levels): `UWLevelBlock` (the
   tile's fields, the free and active lists, the magic word, the overlays and timers),
   `UWTextureMapUw2` (64 `T64.TR` numbers, six door textures; a floor index names an entry directly,
   `TxmID[tile.floor]`), `UWTerrainUw2` (class 1 water, 2 lava, 3 ice in bits 6-7; flow 3-5; mark
   0-2), `UWAutomapCode` (the byte read each game's way: the games swap the kind and the terrain
   between bits 4-5 and 6-7; our UW1 code already reads it as UE describes UW1's). CHECKED AGAINST
   UE's MEASUREMENTS on the shipped file (group Uw2Levels): 42 level and 42 texture blocks, every
   level block 0x7E08 with the magic word, every texture map 0x86, the free lists in range, automaps
   in 160, 233, 238, notes in 240, 313, 318 with 14, 51 and 30 notes, stairs on ice on level 79's
   map, and the floors' terrain words with class or flow bits exactly UE's nine (one more floor word
   carries a mark only, 0x05, a "blackness" texture). OPEN, a decision for when the second game
   first saves: UW1's save writer (`UWSaveCodec.SaveWorld`) keeps a grown block in its place in the
   file, where DOS moves it to the end (UE FORMATS.md, UW1 ARC.C:89) as `UWArk` does; moving UW1's
   writer onto `UWArk` would change where its saves put such a block, so it stays as it is and UW1's
   saves byte-identical.
3. `PLAYER.DAT` for UW2: record, cipher, the inventory workspace; a save written by the original
   loads and writes back identically.
   BUILT 2026-10-29. SAVES WRITTEN BY THE ORIGINAL: the user's machine has GOG's separate UW2
   product at `D:/Ultima Underworld 2` (product 1207662473), with its own `game.gog` - the same 1+2
   CD, `UW.EXE` and `UW2.EXE` of the builds our profiles accept - and `UNDEROM2` with two saves the
   DOS game wrote (2015). `UWPlayerFile` reads and writes a save's `PLAYER.DAT` of either game as a
   file: the key byte, the record deciphered (0xD2 or 0x37D bytes), the plain count word, the
   workspace (the player object, the cursor object, 0x1C slot words of which 0 to 0x12 are used, the
   saved objects), and anything after it; each game's cipher (UE FORMATS.md "The cipher": UW1's xor
   sequence, UW2's table of 0x50 built from the key with each byte chained to the previous plain and
   enciphered ones; a scratch Python prototype first). The record's fields stay `UWPlayerData`'s for
   UW1; UW2's tail comes with its rules. Self-check group PlayerFiles (the UW2 saves through the new
   option `--uw2-saves <UNDEROM2>`, read only, nothing of them pinned or copied): both ciphers their
   own inverse, a one-byte last block left as it is; every save of both games read and written back
   byte for byte, its length 1 + record + 2 + 0x5B + 8 x the count word; UW2's key the name's first
   letter xor 0xAA as DOS writes it; inventory head and slots within the saved objects; position and
   level within the game's; UW1's record deciphered as `UWPlayerData` does. FOUND ON THE WAY (UW1,
   not changed here): saves this port wrote carry the fixed key 0x2A (`UWSaveCodec.NewGameKey`) where
   DOS writes the name's first letter xor 0xAA - harmless, the key travels in the file (Todo.md).
   FOR THE SAVE FOLDER QUESTION (step 4's open point): GOG's UW2 product keeps `UNDEROM2` in its own
   installation folder, so UW2's saves are to be looked for there too, not only beside the image the
   player chose. SETTLED 2026-10-29 with GOG's current installers (one product per game, each with the
   same whole 1+2 CD as `game.gog`, SHA-1 56fbac93...; DOSBox lays `cloud_saves` over drive C:):
   `UWGogInstall.GetSavegameFolder` takes `cloud_saves/UNDEROMn` where the installation has
   `cloud_saves`, else `UNDEROMn`, and finds UW2's in its own product's folder beside UW1's; an
   installation serves only the game whose `UNDEROMn` it has. Applies to UW1 as well.
4. `SCD.ARK`, `DL.DAT`, `BYT.ARK` screens, `SOUNDS.DAT`, the music names.
   BUILT 2026-10-29. `UWScheduleUw2` reads and writes one of `SCD.ARK`'s 16 schedules (the clocks
   per level, the rows of time, level, once, event and 11 parameter bytes; UE FORMATS.md "SCD.ARK");
   `UWDarknessUw2` reads `DL.DAT` (a light level per level, 10 and up a minimum); `UWScreensUw2`
   gives every `BYT.ARK` screen in its palette (UE's table; a scratch render of all nine checked by
   eye: automap, character creation, conversation, game screen, main menu, the Origin and Looking
   Glass logos, the two end screens); `UWSoundsDat` reads `SOUNDS.DAT` of both games as they read it
   (5 or 8 bytes an effect, the length high byte first, UW2's digital flag and priority) and names
   UW2's effect VOCs (`SPnn` below 100, `UWnn` from 100, decimal) and both games' music files (the
   octal theme behind `UW`/`AW`, `UWR`/`UWA`). Self-check group Uw2Files holds them to UE's
   measurements: the schedules' 51, 78, 6 and 84 rows in blocks 0, 1, 14, 15, each block rows x 16 +
   0x144, rows in time order, two at 0xFFFF, events -11 to 11, every block (shipped and the DOS
   saves') written back byte for byte; 49 and 24 effects with lengths 0x40, 0x80, 0x180 and the like;
   every digital effect's VOC present; 17 themes in both versions; UW1's music names as our code
   gives them. THE ARCHIVES OF THE DOS SAVES (group Archives, `--uw2-saves`): DOS's `put_arc` writes
   a block that fits over the start of its room and leaves the rest as it was (70 and 62 blocks of
   the two saves' `LEV.ARK` carry such bytes), so `UWArk` now keeps each room as read; with that both
   saves' `LEV.ARK` and `SCD.ARK` come back byte for byte, also with every block written again, and
   the encoder gives the saves' 118 and 114 compressed streams back as DOS wrote them. FOUND (UW1, not
   changed): our `UWSound` reads an effect's length low byte first; UW1 reads it high byte first (UE
   UW1 SOUND.C:703-708), so a length of 0x80 reads as 0x8000, "plays until the program ends", and
   0x40 as 0x4000 - our effects run longer than the original's. FIXED the same day at the user's
   word (2026-10-29): `UWSound` takes its effects from `UWSoundsDat`; confirmed by ear the same day.
5. The 3D models from `UW2.EXE`.
   BUILT 2026-10-29. `UW3DModelLayout` per game (`IUWGameProfile.Models`): the file offsets of the
   model table, the code base, the colour registers and the colour records, which `UW3DModelImport`
   now takes (UW1's values moved unchanged; the loader passes the running game's). UW2's were found
   from UE's sources and the program: the models in the renderer's far data (UE's segment 052 at
   paragraph 0x4FAF, file 0x526F0); `cDbase` at its 0x26A4 and `grdb_blank`'s 64 labels in the words
   before it give the table at 0x2600 and the code base 0x26A4; `Clk(n)` gives the colour registers at
   0x2680 + 2n; the colour records (`rect_cols`, 5 bytes: flags, three colours, the first texture and
   the count, 32 of them) and the item-to-model table (`rect_sub`) after them at file 0x6908A and
   0x6912A, found by their bytes in a scratch script, once each - no values from UE in our source,
   the code reads them from the program as for UW1. The bytecode is UW1's: models 1 to 3 start with
   the same words in both programs, and the one opcode UW2 adds, 0xD2 (`do_compact_map`, UE
   3d/TMAPOPS.ASM: the textured quad of 0xA0 with the current texture), is now read too. Self-check
   group Uw2Models: 30 models with geometry, 583 faces, none with an unknown opcode; the models both
   games have keep UW1's shape but for four (the arrow 0x08, 0x0D, the table 0x18, the chair 0x1C);
   all 26 3D object types name a model with geometry; the door frame's colour from UW2's record. Not
   yet looked at in the game - that comes with phase 2's walk.
6. `uwdump level`, `maprender`, `conv` and `strings` on UW2; the self-check's UW2 group with
   UE's vectors as fixtures (decision 3).
   BUILT 2026-10-29. UWDUMP: `--game uw2` now has `strings`, `find`, `level` (the tile map and each
   tile's objects by name, from `UWLevelBlock` and the object chains), `maprender` (every level as a
   PNG, doors blue, water, lava and ice by the floor's terrain class; level 1 drawn and looked at:
   Lord British's castle) and `conv` (the first game's disassembler on the second game's decoded
   blocks - `UWConversations.Parse` reads a conversation from any block now; all 102 parse, each with
   its string block in `STRINGS.PAK`, held by the Uw2Files group). THE VECTORS: UE's ten tables
   copied unchanged to `Tools/Fixtures/uw2-vectors` (UE commit 81cc4b5), credited in
   THIRD_PARTY_NOTICES; `UWOriginalRandom.FromState` replays a row's generator state. Group Uw2Vectors
   runs our rule code on the routines both games share, comparing the result and the generator's
   state afterwards: the skill check 300/300, the body part a blow lands on 300/300, move_along
   400/400, sine and cosine 400/400, the square root 300/300, the flank 200/200 - the original's own
   outputs, draw for draw. FOUND: `rollem` 44/300 - both games scale each die, rand() * sides / 0x8000
   (UE uw1 and uw2 sys/UTIL.C), where `UWRandom.RollDice` took rand() % sides: the same draws and
   spread, other values. FIXED the same day at the user's word: `RollDice` scales as the games do,
   and `rollem` agrees on 300/300. `check_res`, `debris_type`
   and `is_sharp` differ between the games and are held to UW2's variants in phase 4.

### Phase 2: a walk through Britannia (L)

The world without the game's rules: a UW2 level loads in Unity and renders in both modes, the
player walks with UW2's motion (speeds in twentieths, ice and currents, no fall damage in world
8), doors open, objects and creatures stand where they belong, the automap fills in, level
changes work across worlds. Creatures stand still. Saving and loading the walk.

STARTED 2026-10-29 (the user: "Mach erstmal weiter"). A probe of `UWGameData`'s loaders on UW2's
DATA found: `UWTextures` fails on `T64.TR` (264 entries, 256 textures and 8 absent ones pointing to
the file's end) and would on UW2's `.GR` names; the weapon pictures want UW1's `WEAPONS.DAT` (UW2:
`WEAP.DAT`, `WEAP.GR`); conversations, terrain and sound read UW2's files in UW1's layouts without
an error (`CNV.ARK` compressed, `TERRAIN.DAT` 256 words, `SOUNDS.DAT` 8-byte entries); the critter
art is not found (UW2's own format). The rest loads. The steps:

1. **UW2's game data loads**: `UWGameData` for the second profile, the readers that differ per game
   behind it - the textures (`T64.TR` for walls and floors, absent entries skipped, UW2's `.GR`
   names), the terrain (`UWTerrainUw2`), the conversations (`CNV.ARK` through `UWArk`), the sound
   (`UWSoundsDat`, the UWR/UWA music), the weapon pictures (`WEAP.DAT`, `WEAP.GR`) and the critter
   art (`AS.AN`, `CR.AN`, `PG.MP`, the `CRnn.n` pages); a self-check group that loads it whole and
   holds each part to the files.
   BUILT 2026-10-29. `UWGameData` hands the profile's game to the readers that differ: `UWTextures`
   (`T64.TR` under both the wall and the floor list, its 8 absent entries at the file's end left out;
   `WEAP.GR` as the weapon pictures, `GEMPT.GR` and `GHED.GR` as new types appended to the enum;
   `PANELS.GR` 79 x 112 with a 3 x 112 narrow side; the nine `BYT.ARK` screens under the first game's
   screen types, each in its palette), `UWWeaponAnimations` (`WEAP.DAT`'s eight records as stored,
   with their 31 pictures each - UW2's steps do not fit the first game's swing layout, so there is
   no `UWWeaponAnimation` for it until phase 4), `UWConversations` (`CNV.ARK` through `UWArk`),
   `UWTerrain` (water and lava served from the classes, the whole word in `.Uw2`), `UWSound` (the
   eight-byte entries, the VOC of a digital effect, the music by theme), `WEAP.CM`, and the power
   gem's pulse (the pictures after the ninth: two in UW2). `UWCritterArtUw2` reads the creature art
   as UE's `CRPAGES.C` and `PGCACHE.ASM` do: AS.AN's map and palette per type, CR.AN's sequences
   (the count in byte 7), PG.MP's last frame per page, the `CRmm.0p` pages (four palettes, the frame
   table at 0x80). Group Uw2GameData holds every part to counts measured on the files apart from the
   code: 256 textures; 35 picture files; 9 screens; 8 weapon records; 13 water and 3 lava textures;
   102 conversations; 49 effects, 29 digital, each VOC loading; 17 themes in both versions; 63 types
   on 32 maps, every step of their 256 sequences found in the page the renderer would take and
   decoded (10544 steps, formats 6 and 8). FOUND: 8 sequences have another count in byte 7 of some
   angle than of the first; the original reads the first (`set_cur_seq_len`).
2. **UW2's world in a session**: the 80 levels from `LEV.ARK` (`UWArk`, `UWLevArkFile.Uw2`) as
   `UWLevel` - tiles and objects as UW1's, UW2's texture map, the overlays and timers inside the
   block -, automaps with UW2's byte, map notes; where a new game starts (level and tile, from UE).
   BUILT 2026-10-29. `UWSaveCodec.Load` reads the second game's LEV.ARK through `UWArk` into the
   session: `UWSessionWorld.Levels` has 80 places, a `UWLevel` for each of the 42 map blocks and null
   for the rest, `Ark` keeps the archive. The new `UWLevel` constructor takes the decoded block and
   the texture block: tiles, objects, free and active lists by the shared codecs, the overlays and
   timers inside the block, `TextureMapUw2` (without a texture block entry i names texture i, as
   `init_txtlib`). `UWTile` maps the second game's indices straight into the 64 entries - wall 6
   bits, floor 4 bits, the ceiling entry 15 (UE `TEXTMAPS.C`, `GRIDDB.C:585`). Automaps (0x1000
   bytes, UW2's byte as stored) on the built levels; the notes of every page (`ParseMapNoteBlock`).
   A new game starts on level 1, tile 0x13/0x30, facing north (`MAINMENU.C:200-203`,
   `player_setup`; `UWGameProfileUw2.NewGame...`). A UW2 session has NO RULE SET (`Rules` null) and
   NO CHARACTER (`Player` null) yet: its profile's rules come in phase 4, PLAYER.DAT's 0x37D record
   in step 6. Group Uw2World: 80 places and 42 levels; every level of DATA and of both DOS saves
   held to its block read apart (textures against `UWLevelBlock` and `UWTextureMapUw2`, the objects
   in the tiles plus the flights against a walk of the block's own chains, the lists, the overlays
   and timers); objects 507 on level 1, 8162 in all; the notes 14, 51 and 30 on levels 1, 74 and 79
   as UE measured; the automaps of levels 74 and 79, which have no map block, stay in the archive;
   the start tile open at the block's floor height (12).
3. **A UW2 level in Unity**: the level loader builds it in both render modes (the 256 textures on
   walls and floors, `DL.DAT`'s light, the terrain classes), objects and 3D models in place, the
   creatures standing; a start into the walk from UW2's main menu screen.
   BUILT 2026-10-29, COMPILED AND THE PLAYER BUILT, NOT YET TRIED IN THE GAME. A survey agent read
   the build path first: the geometry, texture arrays, atlas and both render paths take UW2's data
   as it is; what broke was the rules file at the start, the Void check per frame
   (`UWRenderPathHost`), door and bridge pictures, the creatures and the light. THE START:
   `UWLevelLoader.fStartUw2` reads UW2's data and makes its session current, then shows UW2's main
   menu (`UWMainMenu.InitWalkMenu`: its screen, OPBTN's buttons at the places UE gives both games,
   three as without a save; Create Character starts the walk, `UWGameStartUw2`, the introduction and
   acknowledgements do not answer yet) or builds the walk: level 1 through `fBuildLevel`, the noclip
   camera at tile 0x13/0x30 facing north at the player's eye height (`fEnterWalkView`), Page Up/Down
   to the next level that exists (`DebugLoadLevel` skips the empty places), N cannot leave it. No
   rules file, no HUD, no creature driver, death or Void components, `HasWorld` false: nothing of
   the first game's play runs. THE PICTURES: `UWLevel`'s UW2 table is 70 entries, the 64 textures and
   then the six door pictures, which `UWObjectAppearance` tells by the length (doors from 64, a
   bridge's floor from 0). THE CREATURES stand in their stored sequence and frame
   (`UWCritterArtUw2.PictureFor`, as `DRAWOBJ.C:197-215` picks it; palette 3 at goal 0xF) through
   `UWCritterAtlasBuilder.BuildUw2` (a map's frames by number) and `UWCritterAnimator.InitialiseUw2`;
   the spawner gives them no body, mind or remains. THE LIGHT: `UWDarknessUw2.TileMinimum` (DL.DAT's
   minimum, turned by the tile's light bit - `load_dl`, `change_GrSq`) under the camera into
   `UWLighting.LevelMinimum` (`UWWorldViewUw2`); levels 65-72 but 68 unlit (`process_grid`). The 3D
   models: UW2's `rect_sub` is UW1's plus four (items 0x163, 0x167-0x169), so the shared mapping
   holds; those four stay sprites. KNOWN GAPS for later steps: the TMOBJ indices, the light-source
   ids, the palette rotation ranges and Remastered's gloss table are UW1's; no music; the menu bar has
   Game and Graphics only. Group Uw2World checks the light rule, the unlit levels, the door table and
   that all 21 creatures of level 1 have a front picture in their pages.
4. **Moving**: UW2's motion variant (speeds in twentieths, swimming from the skill, ice, currents,
   no fall damage in world 8), doors opening, the automap filling in.
   PART A BUILT 2026-10-29 (walking; ice, currents, doors and the automap are part B). THE RULE SET
   (decided by the user the same day, "Regelsatz jetzt"): a UW2 session has one now -
   `UWGameProfileUw2.CreateOriginalRules` gives the rule set's own defaults, the first game's numbers,
   and its MOTION is the second game's through `UWMotionVariantUw2` (UE `PHYSICS.C` newFPS, :591,
   :604, `OBJPHYS.C:299`): speeds out of 20 (foot 20, swimming Swimming / 2 + 4, lava 14, ice 20,
   levitating 1, flying 14, slow fall 4), the turn always the full one (newFPS's pTurn is never read; ours scaled it until 2026-11-14, per user too slow in water), the view's
   follow 0x600, no fall damage on levels 65-72 ((level - 1) / 8 == 8 - UE's prose says world 8,
   its code the ninth), no lava exception, impacts above 0x180. The base speeds, the turn step, the
   jump and the fall's Acrobat check are the same numbers in both games. STILL THE FIRST GAME'S
   NUMBERS IN A UW2 RULE SET, each to be held to UW2 in phase 4: combat, magic, experience, vitals
   (swimming's rise and sink among them), sleep, lights, levels, items, traps, creatures, the
   conversation - everything but motion; the profile's other variants (magic, combat, player,
   creatures, items, traps, conversation, the trap and a_do tables) still throw. THE WALK: the player
   stands on tile 0x13/0x30 and walks with the motion core (`UWPlayerMovement` runs while
   `UWGameStartUw2.IsRunning`, `HasWorld` stays false for the first game's systems); the modern scheme's
   mouse look captures, the classic keeps the system arrow and walks by the keys; no move triggers
   fire and no transitions are built (step 5); the Swimming skill is 0 without a character (step 6).
   The variant's numbers are held by the GameProfile group.
   PART B, DOORS AND THE AUTOMAP, BUILT 2026-10-29: the walk opens and closes the door in front with
   the modern use key or a click (`UWWorldViewUw2`, within two tiles; a locked door stays shut, no
   door trigger fires). The automap byte is written in each game's order (`UWAutomapCode.Compose`:
   UW2 the kind in bits 4-5 and the floor's terrain class in 6-7, ice as 3; UW1's bytes unchanged),
   the stairs marked by the texture's terrain mark 3 or 4 (`DRAWOBJ.C:251`), the render band runs in
   the walk, and the first game's Void rule (no map on level 9) is its own. The map screen is the
   first game's HUD, so the filled automap is seen with the second game's interface (phase 3); the
   Uw2World group holds the byte. ICE AND CURRENTS are their own part C: `set_player_phys_params`
   (`PHYSICS.C:399-470`) blends the motion per tick by `munge_vectors` (ice: the grip from the
   terrain's bits 3-5, slopes push downhill; water: the current's direction, speed 0x8D, the
   player's own heading restored after), with an ice state of its own in the contact and the
   motion state - inside the motion core, in all three motion modes.
   PART C BUILT 2026-10-29. The core already had the slot: contact 8 is set_resterr's 1 << class for
   class 3, the motion state 3. `UWMotionHost.TryGetTile` hands the second game's floor class (ice 3)
   to the core, `UWPlayerMotion` gets `State.Ice` from contact 8 and runs `fIceAndCurrents` in its
   initial calc (`set_player_phys_params`: the blend by `UWMotionVariantUw2.MungeVectors` with the
   grip from the floor word's bits 3-5, the ice bounce 0xF - bits, an ice slope's push 0x2F, a
   current's 0x8D in direction bits - 1, kept from the last tile without one, the player's own motion
   kept) and gives it back in the write-back (`phys_affect_player`: lasth/lasts, the head-on stop only
   off the ice and without a current, the view's follow only while nothing pushes). The floor words
   come from the host (`TerrainWordAt`). Original and Smooth run it (Smooth's ice strength per original
   frame); MODERN's own motor does not yet (BUILT, stale entry corrected 2026-11-19: Modern's ice strength
   and wall bounce built 2026-11-14, `UWPlayerMotion.fIceStrength` and `UWCharacterMotor.WallBounce` on
   `State.Ice`). DEVIATIONS: the skid effect (`set_effect(0x80, 4)`) is not
   made; on flat ice the original's push angle is an uninitialised local, ours 0. The walk's debug jump
   (Shift+F7 "level,X,Y") builds the level and stands the player there. Test spots (measured): ice
   level 33 tile 34/31 and level 42 tile 16/29 (grip 0), level 25 is mostly grip 7 (no effect);
   currents level 4 tile 53/15 and level 5 tile 24/15.
   STEP 4 COMPLETE 2026-10-29: walking, doors, jumping, ice and currents confirmed in the game by the
   user; a fall in the walk is logged, not dealt (no UW2 character before step 6).
5. **Level changes across worlds**: UW2's stairs, move triggers and teleports with its level numbers.
   BUILT 2026-10-29, not yet tried in the game. `UWTeleportUw2` decodes the teleport trap as UseTrap
   does (`TRIGGER.C:309-322`): tile from quality and owner, level from the height (1-80, 0 the current
   one), from the fine x the falling arrival (bit 0), a heading (bit 1, the eighth + 8 shifted by 2,
   bit 0x20 in new_player_pos's word) and the map's stop (bit 2, not acted on - the map screen comes
   with the interface). In the walk the motion core's move triggers that the player may set off fire
   through `UWMotionHost.fFireWalkTriggers`: a teleport goes to `UWLevelLoader.WalkTeleport` (the level
   built, the strict pass of find_good_x_and_y, the player set up, near the ceiling when falling,
   turned to the heading); any other trap is logged once per kind (its rules are phase 4). The Uw2World
   group decodes all 298 teleports of DATA (190 to another level, 108 within one; 109 falling, 140
   with a heading, 37 stopping the map) and holds every target to the world: one shipped target lies
   in rock (level 25, 53/41 to 55/41), which find_good_x_and_y moves off. Stairs of level 1: 35/34 and
   39/34 down to level 2, 14/53 to level 3, 44/52 to level 2; level 2's 11/42 back to level 1 with a
   heading. CONFIRMED IN THE GAME 2026-10-29 by the user.
6. **Saving and loading the walk**: `PLAYER.DAT` with UW2's record, `LEV.ARK` written compressed
   (decision 4), the saves where DOS keeps them (`cloud_saves/UNDEROM2`, settled 2026-10-29).
   BUILT 2026-10-29. THE SAVE FOLDER first (per user: his own DOSBox
   Staging in the UW2 install comments the overlay out, so DOS saved to UNDEROM2 while the port took
   cloud_saves; "Automatisch + Schalter"; REWORKED 2026-11-12 for GOG Galaxy: automatic follows the newer SAVE0,
   the original's working copy, then the overlay mount, then the newest SAVE1-4; cloud_saves is read as DOSBox's
   overlay over UNDEROMn, slot by slot, and written alone - UWGogInstall.SlotFolderToRead): automatic follows the folder with the newest SAVE1-4, without
   saves cloud_saves only when GOG's `dosbox*_single.conf` mounts it; the Game window's switch sets
   cloud_saves or the installation's folder (settings.json `SaveFolder`), both games. LOADING: the walk
   menu offers Journey Onward when a save exists; a save starts the walk from its record
   (`UWPlayerRecordUw2`: saved_x/y/z, facing and level at 0x54-0x5D as in the first game, the Swimming
   skill at 0x34 for the motion) with its LEV.ARK. SAVING: Ctrl+S in the walk writes into the slot the
   walk came from (copied first to the port's SaveBackups/UW2) or the first free one, never over another
   save - `UWSaveCodec.SaveUw2`: every level block from the session (UWLevelWriter, the overlays and the
   timers inside it as Anim_Save leaves them), automaps and notes, through UWArk's put_arc and LZSS;
   PLAYER.DAT the loaded save's with the new position, or a walker named Avatar (not a character the
   original could play); BGLOBALS.DAT, SCD.ARK copied, DESC. The doors' state goes into the data before
   a save and a level change (UWWorldSync). The Uw2World group saves DATA and both DOS saves unchanged and
   reads them back: all 320 blocks decode as before, the DOS saves' PLAYER.DAT comes back byte for byte.
   Loading confirmed in the game by the user (a DOS save made beside the cook). THE CREATURES' SIZE
   followed from it (deferred from step 3): in_scalebm sizes a sprite by its record's bytes at +0x0A
   and +0x0C; UW.EXE's records are all 4 and 5, UW2.EXE's objects 0x20 and 0x28 but its critter record
   0x18 and 0x18 - a UW2 creature pixel is three quarters of an object's, both ways
   (`UWCritterArtUw2.CreaturePixelScale` in the creature quad; ours had the first game's 1 and 1.3).
   CONFIRMED IN THE GAME 2026-10-29 by the user: a walk saved with Ctrl+S into SAVE1 (level 1, 32/34)
   loads in DOSBox's UW2 at that spot. PHASE 2 COMPLETE.

### Phase 3: the screen and the hands (L)

The UW2 classic interface (layout table, panels, compass, gem, flasks, the message scroll),
inventory with UW2's containers (key ring, quiver, scroll case), character creation and the main
menu, conversations (the VM with UW2's built-ins, 6 trade slots, the refusals), the map with its
UW2 rules. Then the modern scheme on UW2.

PLANNED 2026-10-29 from two read-only surveys (this repository's interface code; UE's `uw2/src/ui`,
`inv`, `conv`, `game`). What stands: `UWTextures` already serves UW2's nine `BYT.ARK` screens under
the first game's screen types and `PANELS.GR` at 79 x 112; `UWPowerGem` knows UW2's eleven frames;
the walk menu, the save folder and `UWPlayerRecordUw2` are game-aware. Everything else on the screen
is UW1's: `UWClassicLayout` (1039 lines of constants from UW.EXE, `MAIN.BYT`, `CONV.BYT`), the click
areas of `UWClickRules`, `UWPaperdollLayout`, the cleanup phase 5 view models, the `UWHud*` and
`UWModern*` classes, and the art builders under `HudArt` all assume the first game; no profile member
describes a screen. UW2 differs in nearly every number (UE, y counted up from the screen's bottom):
the 3D view 208 x 128 at 16/17 (`UWEDIT.C:333`); the panel 79 x 112 at 235/7 sliding sideways over
20 frames, not turning (`PANELS.C:186, 804`); the six commands as a 2 x 3 bar at the bottom right
(`ICONS.C:3-148`), not a column on the left; the message scroll 16-223 only (`SCROLL.C:59`); no
dragons, so every picture number from `INV.GR` on moves (`LOADGR.C:422`); four chain pictures, not
sixteen; the options as a seven-button panel in the right panel (`WRAPPER.C`), saves listed in the
scroll; trade 6 slots a side (`BARTER.C:8`); the map's gem choosing a world (`AUTOMAP.C:470-746`);
character creation from `BYT.ARK` block 1 with the answers drawn over blank buttons (`CHARGEN.C`).

THE SHAPE: `UWClassicLayout` stays as it is, UW1's table, so UW1 does not move by a byte. A UW2 table
beside it (`UWClassicLayoutUw2`, every value with its UE file and line, measured where UE gives none),
and the consumers asking the running game's table through one engine-free description of a screen
that the profile hands out - element by element as each step needs it, not a rewrite up front. Where
the two games draw the same thing alike (flasks, compass needle, gem, eyes, scroll edges) the code is
shared and only the numbers differ; where they differ in kind (the panel's slide, the command bar, the
options panel, the map's gem) the UW2 piece is its own class beside UW1's. The walk becomes the game
step by step: from step 2 on it has a character, from step 5 on a new game makes one.

1. **The game screen around the walk**: `BYT.ARK` block 4 in the classic scheme, the 3D view in its
   208 x 128 hole with the original's angle (74.7 vertical; the field-of-view point deferred from
   phase 2), the message scroll (16-223, `FONT_5X6P`, its edges and colours), the compass, the power
   gem, the gargoyle's eyes, the command bar (picture, the six icons, F1-F6) and UW2's own pointers
   (`CURSORS.GR`, the eight arrow regions around the view, `mous_player`) in place of the system arrow.
   What a command does stays the walk's (Look prints a name, Use opens doors); the rest is logged.
   PART A BUILT 2026-10-29 (ba54df73), CONFIRMED in the game by the user the same day (the view-angle slider to try once UW2 has its options): `UWClassicLayoutUw2` beside UW1's table (the
   view 16/17, 208 x 128, from place_3d_view; zoom 0x61A8 and aspect word 0x6A3D as UW1's, the latter
   read at UW2.EXE 0x53B94); `UWGameScreenUw2`, the second game's own screen owner (UWGameUI stays
   UW1's and uninitialised in the walk): block 4 drawn opaque - its window is index 0 edged in index 1,
   the palette's (0, 0, 4), which our palette reads as UW1's transparency marker - with the view's
   window cleared, the camera in it at 74.7 degrees vertical with the VGA pixel's 5/6, the modern
   scheme the whole window at the scene's angle, both with the view-angle setting. The gargoyle's
   teeth reach ten pixels into the view's first row; the view covers them, as drawn over the screen.
   `uwdump --game uw2 pictures <NAME.GR|BYT> --out dir` writes the pictures as PNG for measuring.
   PART B BUILT 2026-10-29 (084cd06d), CONFIRMED in the game by the user the same day (compass turns, Look prints): the message scroll (main_scroll 16-223, rows
   169-198; the paper 0x71 over the rows the screen leaves in index 1, its last row and corners the
   screen's own; the text 0x76 and spec_col through the first game's scroll model in FONT_5X6P; the
   edges SCRLEDGE 0-4 / 5-9 turning with each scroll), the compass (COMPASS.GR's face by heading and 3,
   a needle of sixteen at 0x78/0x2B plus nedl_x/nedl_y), the power gem at rest (POWER.GR 0,
   transparent), the eyes open (EYES.GR 0), the command bar (OPTB.GR 0, the chosen icon's rectangle of
   OPTB.GR 1 over it). UW2's pictures are opaque except index 0 where drawn transparent (GRLIBL.ASM
   _5467); index 1 is a colour (the eyes' sockets, the bar's shadows), not UW1's marker. A click on the
   bar or a command key chooses, the same again none, the options icon nothing yet; the walk's right
   button in the view acts by the command as mous_in_3d (none or Look: the first game's "You see ..."
   with the block 4 name; Use: a door; the rest "not yet"), the left button still the door until part
   C. The walk's lines (locked, saved, not saved, nothing there, not yet) are the port's texts, English
   and German. OPEN: UW2.EXE's F-keys (F1 use, F2 fight, F3 talk, F4 look, F5 get, F6 options) are not
   the first game's - the command keys follow the first game's bindings until the second game gets key
   defaults of its own.
   PART C BUILT 2026-10-29 (38179a89), CONFIRMED in the game by the user the same day - STEP 1 COMPLETE: mous_player and parse_playin are the same code in
   both games (UE uw1 and uw2 game/PLAYER.C mous_player, motion/PLAYMOVE.C), only the window differs - so
   the pointer's eight regions (fifteenths 5, 3 and 6 of 208 x 128, UWClassicLayoutUw2.PointerZones) pick
   CURSORS.GR 0 to 8 in the first game's order, scaled as the first game's classic pointer; a left press
   begun in the view drives the walk by the pointer's pixel through UWPlayerMotion.PointerCommand with
   208 x 128 (UWPlayerInput's first-game path unchanged), the pointer held in the view meanwhile. Our own
   pointer is captured on the walk's classic screen; the system arrow stays for the second game's main
   menu only (until step 5). The left button no longer opens doors in the classic walk - Use (right
   button) does.
2. **The character on the screen**: the UW2 record as the walk's character (strength to skills,
   vitality and mana, from a save or the walker), the flasks (12 levels, poison), the right panel with
   its three pages and the sideways slide with the chains, the stats page, the paperdoll (`BODIES.GR`,
   `ARMOR_F/M`, the 23 places of `InvDisplay`), the backpack's eight slots shown, the weight left in
   stones (`displayEnc`), the weapon in the hand (`WEAP.GR`, `WEAP.DAT`) standing still.
   STARTED 2026-10-29 ("Ja, Schritt 2"), in three parts: A the flasks and the inventory page, B the
   statistics page and the panel's slide, C the weapon in the hand.
   PART A BUILT 2026-10-29 (7c38f8f3), CONFIRMED in the game by the user the same day (PQMarine: body, full flasks, 69): struct Player agrees with the first game's to 0x5D
   (UE include/player.h:20); sex, body, class and hand are the byte at 0x65 (the first game's 0x63), the
   vitality kept is the workspace's player object's hit points; the inventory is the first game's
   workspace (slots, saved objects from 0x5B + 8n). `UWPlayerRecordUw2` reads them; the DOS saves'
   characters and slots are pinned in Uw2World (SAVE1 and SAVE2: Henrietta, a tinker, level 15). The
   flasks fill by twelfths of the maximum (set_flask, set_screen_frame), the page is PANELS.GR 0 at
   235/7 (equal to the screen's own panel, pixel for pixel), the paperdoll and the objects at
   InvDisplay's places (DisplayToSlot, ArmorSlots; armour picture type + 15 x condition), stack counts in
   FONT_4X5P, the weight left centred at 305. A walk without a save shows no character.
   PART B BUILT 2026-10-29 (dde7281f), CONFIRMED in the game by the user the same day after the pointer fix d3ed7071 (clicks read the captured system mouse): the chain (flask_info's middle columns, above
   its bottom ten rows) pulls the inventory page to the statistics and back (pull_chain); the slide is
   do_panel_frame's twenty frames of an eighth of a second (32 ticks of the 1/256 s clock), the old page
   out to the right by eight columns a frame, then the new one in from the right, clipped to the panel
   over colour 1, CHAINS.GR 0 to 3 on frames 1, 4, 13 and 16 (the last equal to the screen's chain at
   rest). The statistics page (PANELS.GR 2, STATS.C): FONT_5X6I, header and numbers 0xC4, skills 0xC9,
   the name centred, class and level with its ordinal (port texts: ST/ND/RD/TH, German "12."), the
   attributes, vitality and mana now/most, experience, six skill rows that scroll from the panel's
   bottom strip, the skill points first. A click low on a flask says its value (the game's own
   sentence, the poison first).
   PART C BUILT 2026-10-29 (3977e1f9; the view moved to rows 16-143 after the user saw the floor under the fist, see Frame.ViewWindowTop), CONFIRMED in the game by the user the same day - STEP 2 COMPLETE: the weapon is not "standing still" in the
   original - sheathed it is not drawn at all; the fight icon draws it (adjust_weapon: three steps,
   ready, three to sheathe, an eighth of a second each), its kind from the hand (PLAYDATA.C: a melee
   weapon by OBJECTS.DAT's skill, else the fist), WEAP.DAT and WEAP.GR by hand and kind, its sixteen
   colours WEAP.CM's set for the body (load_weapcm; the first game's weapons are still shown
   untinted). Swings and the sway with the speed are the fight's (phase 4). And do_fbuf_bms draws
   3DWIN.GR 0 over the view each frame: the gargoyle's teeth in its first row, which the view had
   covered since step 1 (its second row equals the screen's pixels there; the frame buffer's y counts
   up, a picture's top row in the view is 127 - y).
3. **The hands**: picking up, dropping, moving, wearing; the containers (a bag opens in the backpack's
   rows, scrolling by four, nested; the key ring, the quiver taking wands, the scroll case's types),
   the rune panel (24 runes, 4 x 6), the active spells shown; the inventory written into `PLAYER.DAT`
   and the level's objects on saving, read back on loading, and loaded by DOS.
   STARTED 2026-10-29 ("Ja, Schritt 3") in four parts: A the inventory in the model and moved in the
   panel, B the world (picking up, dropping, throwing), C containers and runes, D saving it. A survey
   found the first game's engine-free UWInventoryModel reusable but for a few id tables (categories,
   container masks, armour ids, the rune bag, lights) and its load and save through UWPlayerData; both
   games' ItemFitsSlot are the same code over different numbers (UE inv/INVPANEL.C).
   PART A BUILT 2026-10-29 (db0a479d), CONFIRMED in the game by the user the same day: `IUWInventoryVariant` on the profile (UW1 through
   UWArmorItemMap as before; UW2 by OBJECTS.DAT's armour categories - head 8, body 1, gloves 4, legs 3,
   feet 5, rings 9 - and the weapon hand refusing a weapon stack), asked by UWInventoryModel's fitting;
   `UWInventoryModel.LoadFromSlots` (LoadFromPlayerData calls it); `UWInventoryWorkspace` reads a
   workspace as UWObjects. On the inventory page a left click takes or puts (InvDisplay's hit
   rectangles, the model's fitting, stacking, combining, swapping), an empty weapon hand toggles the
   fight, the right button looks; the object on the pointer is its picture. Not yet: the stack's "Move
   how many?", food into the head's place, the lights' lit ids, UW2's combining rule (any listed pair),
   the carrying limit (CanCarry needs the first game's player data) (BUILT but the combining rule, stale
   entry corrected 2026-11-19: `UWGameScreenUw2.AskCount`, `UWFoodUw2.IsEatenFromHead`,
   `UWItemUse.ToggleLight` from the UW2 screen, `UWInventoryVariantUw2.CarryLimit`; UW2's combining rule
   still open). Saving the moved inventory is part D.
   PART B BUILT 2026-10-29 (e0274bd9), CONFIRMED in the game by the user the same day after b21211c9 (carried objects got no picture and vanished when dropped): picking up and putting down are the same code in
   both games (UE player_3dget, ReturnObject) but for strings and UW2's pressure plates. The right button
   in the view: with an object on the pointer it is dropped in front (UWPlayerThrow.TryFindDropSpot, lifted
   out of the first game's drop unchanged; no room says the game's 0x10D); with Get it takes; with no
   command or Look a drag takes and a release looks (mous_in_3d's "look and drag to get") - within the
   hand's reach (UWReachRules.CanReach; too far says 0x6A), the thing out of its tile onto the pointer, a
   container with its contents. Not yet: the terrain between (BlockingTerrain), the weight limit, theft,
   moonstones, pick-up triggers and pressure plates, the throw - the second game's rules (phase 4) (BUILT
   but the terrain between, stale entry corrected 2026-11-19: `UWWorldViewUw2.fTakeCount` checks
   CanCarry, the theft, the moonstone, the pick-up trigger and the plates (G3 part H4), `fTryThrow` throws;
   the terrain between the hand and the thing is still not tested).
   PART C1 BUILT 2026-10-29 (c65e80d2), CONFIRMED in the game by the user the same day with the arrow fix 1becce45 (nested bags, the rune bag refusing other things; key ring and quiver not yet tried): the bag code is the same in both games (UE
   BAGS.C) over other numbers. `IUWInventoryVariant.MatchesContainerMask` (UW1 through UWContainerCapacity
   unchanged; UW2 by ItemFitsSlot: key ring 0x8C keys and key rings, quiver 0x8D missiles and wands, scroll
   cases 0x88/0x89 classes 4-7, 9, 10, food 0x8E without the drinks, rune bag 0x8F runestones - checked
   against UW2's OBJECTS.DAT). On the inventory page a press waits: dragged it takes, let go the left
   button uses (a bag opens in the pack's rows over INV.GR 6, open picture on display 20, arrows moving a
   row of four) and the right looks (Part A took on the press; mous_in_inv/DoInventoryMouse take on the
   drag). A bag takes what is put on it, the open bag's place gives back to the pack or the bag around.
   PART C2 BUILT 2026-10-29 (6e2c365f), CONFIRMED in the game by the user the same day: the rune bag (record 0x44, a bit per rune), the
   shelf (0x47, its count in word 0x61) and the active spells (0x3E, their count in word 0x60) as the
   first game's record has them; both DOS saves hold all 24 runes and Vas Ort Ylem on the shelf. The rune
   bag's use slides the panel to PANELS.GR 1 (OpenTheBag), the runes at ShowRune's places; mous_in_rune:
   left puts a rune on the shelf (a fourth pushes the oldest), right looks, the bottom strip clears; the
   shelf (rune_x, y 0x32) and the active spells (spell_x, y 0x37, SPELLS.GR by spell_class_values) on the
   frame. A runestone put on the rune bag sets its bit (UWInventoryModel.LearnRuneStone, a hook the first
   game leaves unset). Casting (the cast area) and dispelling are the second game's magic (phase 4).
   PART D BUILT 2026-10-29 (9e7714b8), CONFIRMED by the user the same day (saved in ours, loaded in ours and in DOSBox) - STEP 3 COMPLETE: the first game's inventory writer lifted out of
   UWPlayerData unchanged (UWInventoryWorkspace.WriteRecords: the records renumbered, pack then places,
   depth first; the slot words; the top level as a chain; the count; UW1 calls it with its own offsets,
   its save round trips unchanged); UWInventoryWorkspace.WriteInto lays the UW2 workspace under its count
   word, links the player object to record 1 and writes the running weight at 0x4A. Ctrl+S in the walk
   writes the inventory before the save (the object on the pointer into the pack: the original keeps no
   held object in a save). Checked: both DOS saves' workspaces written back from their objects read again
   with the same objects, places and contents.
4. **Options, saving and loading**: the seven-button panel in the right panel (Save, Restore, Music,
   Sound, Detail, Quit, Return and their groups), the slots listed in the scroll ("I-" to "IV-", 30
   characters, "<not used yet>"), Ctrl+S, R, M, F, D, Q, O; the walk's provisional Ctrl+S goes (and with
   it the backup on every press). The Setup menu's own pages stay as for UW1.
   BUILT 2026-10-29 (6d010d49, the description's box e4d8606c), CONFIRMED by the user the same day - STEP 4
   COMPLETE: UWOptionsPanelUw2 (engine-free: the five groups of ICONS.C's tables,
   the lit button, the message row, Move round the seven past the empty ones, Press as deal_with_button
   returning what the game is to do), UWClassicLayoutUw2.Options (WRAPPER.C's places: the group picture
   at 0xEC/0xC2, button b at its rows 16b + 2, message rows over button 0, the sound page's title from
   OPTBTNS.GR 14 over button 1, the click area 6 - (y - 0x4E) / 16, the open bar OPTBTNS.GR 2). The screen
   opens it from the options icon and Ctrl+S, R, M, F, D, Q, O (refused with an object on the pointer,
   string 0xAE); while it is up the clock is held (UWGameClock.OptionsHold), the walk's input waits and
   the pointer is the plain one; mouse press-follow-release, Up/Left, Space/Right/Down, Enter, Escape
   (clears the scroll), Alt+X on quit, F6 on the game page. Music and sound through UWSoundOptions, the
   detail through UWGraphicsDetail.Choose (Very High opens the effects window as in UW1), quit yes leaves
   for UW2's main menu (UWGameStartUw2.Leave; the first game's Quit also leaves to the menu, not to DOS). The
   file page lists the four descriptions in the scroll (0x120, "I- " to "IV- "); saving asks 0xB7 with
   the old description (or "<not used yet>") selected, up to 30 characters, Enter or a click saves,
   Escape aborts, then only the outcome stays in the scroll (0xAE + 4/5/6); restoring an empty slot says
   0xAE + 1, a save starts the walk from it and the new screen says "Restore Game Complete.". The
   provisional Ctrl+S and its backups went (UWWorldViewUw2.Save is the panel's). As the first game's
   panel: no cursor keys inside the description (wdialog has them). Self-check group Uw2OptionsPanel.
5. **The main menu and character creation**: Create Character builds a character (block 1, palette 3,
   `CHRBTNS.GR` with the answers drawn on them, `CHRGEN.DAT`, `SKILLS.DAT`, the eight questions with
   difficulty, the name up to 29) and starts the game where the original does, with its first state
   (the clock 0x465000, the moonstones, the fatigue; `CHARGEN.C:73-129`); the intro and
   acknowledgements as far as the cutscene player carries them (UW2's cutscenes are phase 5).
   PARTS (2026-10-29): A the creation screen, B the new character in the walk (its PLAYER.DAT: name,
   attributes, skills, maxima, the body byte, easy, init_char's fixed values, the workspace's player
   object; the start at 0x13/0x30), C the main menu as the original's (button places, keys, the save
   list of found saves only, "You reenter the Underworld . . .").
   PART A BUILT 2026-10-29, not yet tried: UWClassicLayoutUw2.Chargen and UWChargenLayoutUw2 (drawopt's
   columns, rows, spacing and centring; selopt's places; mousopt's hit), UWCharacterCreationScreenUw2
   (BYT.ARK screen 1, palette 3, CHRBTNS.GR 0/1 buttons and 3/4 frames, FONTCHAR in 0xC4; the left page
   by CHARGEN.C's places; mouse press-follow-release, cursor keys by rows, Home/End, Enter, Escape
   restarts or leaves from the first question; the name without Escape or cursor; "awakens . . ." for
   1.5 s) on the first game's model (CHRGEN.DAT identical, SKILLS.DAT the same form, the same rules).
   The walk menu's Create Character opens it; "Yes" starts the plain walk until part B. Checks
   Uw2ChargenLayout and the creation part of Uw2GameData. CONFIRMED by the user the same day after the
   pictures were built opaque (d63eff2a: the menu builder kept UW1's transparent palette index).
   PART B BUILT 2026-10-29, not yet tried: UWPlayerRecordUw2.NewCharacter - the record as init_char(1)
   blanks it and gen_char fills it (name, attributes, skills, vitality and mana at their maxima, level 1,
   one skill point, hunger 0xC0, fatigue and food_heal 0x30, empty shelf, weight limit str * 13 + 300,
   moonstones 3 and 0x2D, automap on, map_scrap 1, bF6 8, easy, detail 3, clock 0x465000, X clocks zero
   as init_char's memset leaves them, the body byte with lefty = 1 - choice) and the workspace's player
   object as InitPlayerRec and player_setup make it (0x207F, whoami 0xFD, home 0x13/0x30, fine 3/3,
   seq 1, hit points; level 1's object slot 1 is not the player - it holds 0x3F); the creation's "Yes"
   keeps the choices (UWCharacterGeneration.Result), the walk's start takes them
   (UWSaveCodec.StartNewCharacterUw2) and begins where a new game does. CONFIRMED by the user the same day.
   PART C BUILT 2026-10-29 (84b024d3), CONFIRMED by the user the same day - STEP 5 COMPLETE:
   UWClassicLayoutUw2.MainMenu (MAINMENU.C's buttons, lights,
   list, re-entry message) and the walk menu's own input in UWMainMenu: one button always lit (Create
   Character without a save, Journey Onward with one), the cursor keys and Ctrl+F/N/B/P, Home/Page Up,
   End/Page Down, Enter; a press followed, no light on a passing pointer; Journey Onward lists the found
   saves only, centred in FONT_BIG in colours 7 and 0xCD, Escape and Alt+X back; the choice shows "You
   reenter the Underworld . . ." for 1.5 s and loads. Alt+X on the buttons (end the program) left out.
6. **The map**: `BYT.ARK` block 0, three pixels a tile in UW2's automap byte order (the painter is
   UW1's today), the gem choosing a world (Britannia and eight worlds, 6 and 7 swapped, only those
   visited lit), the level arrows within a world, the level number, the notes (100 a level, 46
   characters, `FONT_4X5P`), the eraser; the automap that can be off and the map scraps. PART A BUILT 2026-10-30 (UWAutomapScreenUw2, UWMapScreenUw2): the screen, the tiles (UWAutomapPainter with UWGame.Uw2: ice, three colours), the gem, the corners, closing; opened with M and by using the map; user-confirmed. PART B BUILT 2026-10-30: the notes on UWMapNotesModel and UWMapNoteRules (the first game's, note->x/y = mx/my + 2 here), the eraser, the level number, the pen and eraser as the game screen's pointer, either button acting; check MapScreenUw2; user-confirmed with the cursors in the map's palette. FOUND 2026-10-31 by the user's test: UWPlayerTerrain, which writes the automap, was made only with the first game's interface, so the walk wrote nothing and the flag never came back; made in fStartUw2 now, its automap part only. PART C BUILT 2026-10-30 (UWAutomapStateUw2): the flag and map_scrap in the record, the map opening on the scrap's level with no arrow while off, drawn tiles not written then, the teleport trap's bit, the Void, player_newsq, the bit of a map (eight in the shipped file, pages 74 and 79). Left for the rules of phase 4: the scrap's experience, Locate, waking from the Void, the crystal ball (BUILT, stale entry corrected 2026-11-19: `UWGameScreenUw2.fUseMapScrap` pays CopyScrap's experience, `UWExtraSpellRulesUw2` Locate turns the automap on, the Void's world gives it back on leaving (`UWAutomapStateUw2.ChangeLevel`) and the gem's waking turns it on (`UWDeathUw2`), the crystal ball is hack 2 in `UWTrapRulesUw2` on the remote camera, which leaves the player's automap alone).
7. **Conversations**: `BYT.ARK` block 2, the portraits (`CHARHEAD.GR`, `GHED.GR`, `HEADS.GR`), the
   names, the two scrolls, six trade slots a side with UW2's regions, the VM with UW2's 51 built-ins
   (the nine new ones: `babl_hack`, `give_all_stuff`, `set_sequence`, `switch_pic`, `teleport_player`,
   `teleport_talker`, `transform_talker`, `x_clock`, `x_exp`), the quest store of 128 bits and 16 bytes,
   the refusals; built-ins whose rules come in phase 4 log what they would do. PART A BUILT 2026-10-30
   (c4629e26): TalkTo's rules (UWTalkRulesUw2), the screen (UWConversationScreenUw2, BYT.ARK's screen 2 over
   all but the right panel), the session and view of the first game on a host of the record
   (UWConversationHostUw2), the machine's built-ins by name, the quests, numbered variables and X clocks in
   the record, the new built-ins (UWConversationSessionUw2); checks Uw2Conversations (all 102 run) and
   Uw2TalkRules. PART B BUILT 2026-10-30: six slots a side, the marks and counts, UseTradeSlot's clicks
   with the panel staying usable, do_demand with its nothing-selected case and the levels 9 and 17,
   do_offer's verdicts, coins at full quality, greed and fudge, give_all_stuff. TALKING AND TRADING
   USER-CONFIRMED 2026-10-31 (test list sections 3 and 4; coins at full quality by reading BARTER.C:804, the
   comparison in the game was not conclusive), with fixes that day: the wait before partner lines, typed
   answers in the answer scroll, the wisp, the object pictures' black edge, a look from the panel in the
   answer scroll, the partner's goods (35c136ff: UWObjectVariantUw2, the valuable's roll) and AskHowMany in the
   pack, the slots and the world (fe28c123: UWCountAskUw2 on UWCountPrompt, classic scheme only). Left:
   EncumCheck on taking from a slot (BUILT, stale entry corrected 2026-11-19: G5,
   `UWConversationScreenUw2.fTakeCountFromSlot`, "That is too heavy to take."), a container's refusal, pause's wait, splitting on the modern scheme
   (step 8).
8. **The modern scheme on UW2** (decision 5): the presets, Classic Wide, the bags, the minimap and the
   modern conversation over UW2's art and numbers. SURVEYED 2026-10-30: the UWModern* classes hang on
   UWGameUI.Init's data (UWModernHud's gate), UWCharacter's Vitals, Interaction and UWInventory, which do not
   run on the second game; only UWInventoryModel is shared. PART A BUILT 2026-10-30: the modern scheme no longer
   dead-ends: the options (Escape), the map (M) and a conversation (E on a creature) stand on the classic
   screen as the first game's big map does, Q looks. PART B BUILT 2026-10-31 (UWModernHudUw2, its own class as
   UWModernHud stands on UWCharacter): the flasks with their values, the scroll's last lines beside them, the
   game's pointer in the view's middle and the name of what it is on. PART C BUILT 2026-10-31: the bags in
   UWModernHudUw2, a grid of the classic page's displays (B opens, E takes), the first game's windows left for
   later. PART D BUILT 2026-10-31: the minimap in UWModernHudUw2, a window of the painted map round the character,
   only with the map carried and the automap on, without the first game's notes, zoom and turning. PARTS A TO D
   USER-CONFIRMED 2026-10-31 with the test list (the first game too, unchanged; poison and flasks changing wait
   for phase 4), fixes: the flasks' places as wide as their values and only their own colour 0 clear, a
   creature's hit area its body (607608bf). PART E BUILT 2026-10-31 (a46e9dbf), untested: the action bar
   (UWModernBarUw2: ten places bound to carried pieces, kept in UWR.json as in the first game, using is the
   inventory's own use, spells with phase 4) and Move how many? in the bags, its line beside the flasks;
   USER-CONFIRMED 2026-10-31. PART F BUILT AND USER-CONFIRMED 2026-10-31 (22e7c6c1), UW1's unchanged by the user's
   repeat of its test: one modern conversation for both games (per user): UWModernConversation
   stands on IUWModernConversationHost, UWModernConversationHostUw1 keeps what the class did, the second game's
   host runs on UWConversationScreenUw2 with the classic screen hidden, six places a side two across and three
   down, the screen's own pictures, backs and slots in the frame's slate shades, no original's look; the history
   keeps pace with the waits (no click, per user) and shows a small head at a change of speaker; Escape at a typed
   answer leaves no "-" there (per user). The conversation is no longer on the classic screen in the modern
   scheme. Left: Classic Wide, DEFERRED BY THE USER 2026-10-31 UNTIL AFTER PHASE 4 (the rules put more parts on the
   frame): UWClassicWideBuilder's cuts are measured on UW1's MAIN.BYT and need UW2's frame measured and checked by
   eye, the top bar either side of the gargoyle, the ledge either side of the compass, the panel at x 224 moved
   whole; UWGameScreenUw2 stands on 320 columns (clicks, the camera's area, the scroll, the parts on the frame).
   SUPERSEDED 2026-11-23 for everything but the conversation: the stopgap goes, see "The modern interface for
   UW2: one interface, two hosts" before phase 6 (Classic Wide is its stage U7).

### Phase 4: the rules (XL, domain by domain)

UE's difference list as the work list, each domain with its self-check cases, in the order the
game needs them. FIRST, BEFORE ANY DOMAIN (surveyed 2026-10-31): the second game has no player in play. UWCharacter
and its vitals stand on UWPlayerData, Interaction and UWGameUI; UWGameSession.Player is null, `UWGameProfileUw2`'s
Magic, Combat, Player, Critter, Traps, TrapKinds and DoActions still throw, and the walk keeps its numbers in
UWPlayerFile's record. So step 0: decide how the rule systems reach the player (a seam over both records), then the
pure variants with their checks, then the domains below. STEP 0 DONE 2026-10-31: the rule classes start from
UWPlayerData and hold their numbers themselves, so the seam is UWPlayerData.FromSecondGame (1e278b52), held to the
record on the DOS saves; no layer through the 58 scripts on UWCharacter. PLAYER ON THE CLOCK (fb6de248): UWVitalsUw2
runs UWPlayerVitals on the same tick and writes through to the record after every tick, takes over what a
conversation wrote, UWPlayerVariantUw2 (the gain's halving, a skill point at 1500, the long cycle of 30); mana
user-confirmed, the rest of test list 10 waits until it shows better. Death is not built, trap targets throw.
CREATURES BROUGHT FORWARD BY THE USER, USER-CONFIRMED 2026-10-31 (790663c3, 86119820): the first game's body and brain
on the second game's table through UWCritterDriver, UWCritterVariantUw2 puts the brain's slots and the record's
sequences into each other, walking, standing, talking, saving into DOSBox and level changes confirmed, UW1
unchanged. THEY PERCEIVE AND STRIKE THE PLAYER, user-confirmed 2026-11-01 (808d61f8, 5e0cbde1, 080f9ee9): UWVitalsUw2
is the player in UWCreatureWorld and UWPlayerAvatar, with the status pass's armour, a defender and the noise; note
UWCharacter stands in the second game's scene too with vitals nothing runs, so its presence says nothing, ask
UWGameStartUw2.IsRunning first. UWCombatVariantUw2 (enchantments below 8, the poison blow's armour roll, fire and cold
doubled, doors damaged, debris); poison confirmed on the flask. Resistances of creatures 0, no death, no remains, the
rest of step 4 below. THE PLAYER'S BLOW BUILT 2026-11-01, untested: UWAttackUw2 over the first game's UWPlayerAttackState
and ResolveMelee, player_attack's steps read from WEAP.DAT's flags (0x20 lead-in, 0x40 hold and charge, 0x80 strike; row
0 slash, 1 bash, 2 stab), the power gem on the classic screen, UWCritterRemains for the second game (blood 0xD9 + n,
corpse always in the eighth world), experience into the record. Target by ray and sphere before the eye; the classic box
probe needs the first game's character. Open: weapon wear, Valor, special weapons, missiles, blood and shake, eyes,
creature resistances, the second game's death_check (HasSpecialDeath false), the gem in the modern scheme
(BUILT, stale entry corrected 2026-11-19: `UWAttackUw2` has the wear, Valor, the blood, the eyes, the missiles
and the special weapons' powers (2026-11-19), `UWVitalsUw2` the combat shake, `UWCombatVariantUw2` the
resistances, `UWCritterVariantUw2.HasSpecialDeath` true (G6), `UWModernHudUw2` the gem).
THE PLAYER'S BLOWS, EXPERIENCE AND DEATH CONFIRMED 2026-11-02: the blow, kills, exploring, map scraps and conversations
pay; UWDeathRulesUw2 and UWDeathUw2 take a ninth of the experience and wake the player by the gem below Britannia, in it
the game ends. Jail waits for triggers and schedules (it fires a scheduled trigger and needs Lord British's place), and
until then a castle guard's kill ends the game too. Per user 2026-11-14 the original also jails the player who breaks the djinn bottle in Castle British (level 1) before the quest's step 5 and dies of it (player_is_dead asks last_hit: probably an older guard's hit still standing) - to check with the jail. TEST FOR THE JAIL (moved from test list 41.1 per user 2026-11-15): the cell on level 1 - the portcullis 41/38 has an unpickable lock (height 15) and probably opens through the jailer's conversation; the move trigger on 40/37 fires a door trap of quality 2 (close), so leaving the cell there shuts the portcullis behind the player (fallen-log "door 41/38: moves"). Add it to the test list when the jail is built.

THE SWEEP OF 2026-11-06 AND THE ORDER FROM IT (per user, 2026-11-06: "should we check the order again? Things often do
not work because something else is missing"). The same day four placeholders had made built things fail in silence -
the motion spells' bits handed on only by the idle UWCharacter, the fall damage only logged, UWPlayerTerrain returning
at once (no view bob), the debug spell list casting through UWHudRunes. A read-only sweep (an Explore agent, its
findings spot-checked: the drawn weapon, Acrobat, the hallucination, the typing lock and the throwing trap tables read
as reported) found the rest. THE ROOT: in the second game UWScene.Character, Interaction and UWGameUI are not null -
they stand there uninitialised, so a call to them succeeds and reads or writes dead state. The groups, in the order
to build them:

- G0 WIRING (small, each a few lines, makes built things work; first) (BUILT but F9, stale entry corrected
  2026-11-19: the setup bar is whole in the walk (`UWSetupMenu`), F7 turns the panel and F8 casts
  (`UWGameScreenUw2.fChooseCommand`), the wiring spot-checked in the code; only F9 Track is still open in UW2):
  the player read from the idle first-game parts - Acrobat for falls (UWPlayerMovement takes it from UWCharacter,
  Swimming is already the record's), the drawn weapon creatures see (UWCreatureWorld from Interaction; take
  UWPlayerRecordUw2.IsWeaponDrawn), the hallucination palette (UWRenderPathHost from UWCharacter.Vitals), the help's
  Map tab (UWGameUI.IsCarryingMap), UWPlayerAvatar.LeaveCombatMode, the easy movement's step (noise and clock to
  UWCharacter); UWVitalsUw2 not subscribed to Damaged (no red/blue/curse flash), KillRewarded; IsInMotionState false
  (too little noise); the status pass only on the vitals tick (armour, light and motion items up to 21 s late after an
  inventory change); cursed worn items never strike (the dice worked out, not dealt). INPUT: the typing lock leaves
  out UWDebugTools.IsTyping, UWHelpWindow.BlocksGameKeys, UWKitRows.IsTyping and the instrument (typing in the debug
  search walks and fires keys); F1-F6 take any modifier (Shift+F1 opens Options too, Alt+F4 picks Look) and run under
  the help; the option shortcuts take left Ctrl, the modern sink key; Ctrl+M opens the map before the shortcut; E and
  Q, Escape, B, Z, R and the digits without a modifier or help gate; F7 (turn the panel), F8 (cast, classic) and F9
  (track) missing, the gamepad's cast; the setup bar while walking shows only Game and Graphics (Motion, Display,
  Controls, Sound, Colours unreachable). THROWS INSTEAD OF "NOT YET": UWGameProfileUw2 Traps, TrapKinds, DoActions
  (a smashed container or a Tremor boulder over a use trigger, UWContainerDamage, UWTileBlast to UWTrapRules), the
  fishing pole (UWFishing on Game.Traps), PickRoll; the debug spell list casts unbuilt spells into empty host stubs
  without a word (no IsBuilt check in CastFromObject).
- G1 MAGIC STAGE 2 PARTS B TO E as planned (area, missiles, target spells and Roaming Sight, creation), with the
  creatures' attack spells (CastSpell returns false for the unbuilt classes) and the Clear Mind flash
  (NightPresentation.FlashWindow empty). Contained, and potions and wands (G3) need it.
- G2 TERRAIN: water and lava in the walk - UWPlayerTerrain's swim counter, sinking view, lava damage (returns at once
  in UW2), the swim check on UWVitalsUw2 (SwimCheckDue not subscribed, RunSwimCheck hurts the idle UWCharacter), so
  drowning; the weapon put away in water if the second game has the rule; the player's paralysis on the walk and its
  countdown (BUILT with part E2, 2026-11-13). THE SECOND GAME'S COLOUR CYCLING (cycle_colors, gfx/COLCYCLE.C:49:
  0xE0 to 0xEF four banks of four one place down, 3 to 7 and 8 to 10 one place up, every 64 ticks; UWPaletteRotation
  has the first game's ranges only, so nothing rotates in a UW2 session) - its water, lava and the flam and tym runes,
  which the user saw move in the original (2026-11-13). BUILT 2026-11-13: UWPaletteRotation.IsUpIndex/IsDownIndex by game (the
  rotation table, the pre-baked Remastered steps, the grime's exclusion), the shader's UWIsRotatingIndex on
  _UWSecondGameRotation; lava's glow and water's gloss stay the first game's; test list section 36. SECTION 36 CONFIRMED by the user
  2026-11-13 (runes and water move, nothing else flickers, the first game unchanged; UW2 lava not seen yet). WATER AND LAVA BUILT 2026-11-13: UWPlayerTerrain's second-game
  path (fUpdateTerrainUw2: the core's contact, the swim counter in the record's 0x307, the water loop, punt_fightmode
  on entering, the lava on the game's clock without dragon skin boots and with the djinn's mud at step 3, the swim view
  as a height on the walk's eye), RunSwimCheck on UWVitalsUw2's SwimCheckDue, fight refused while swimming; the
  second game's footsteps and swim strokes (make_noise: a stroke 0x1A every 15 steps panned left and right while
  moving, footsteps by floor and ice) BUILT 2026-11-13 (UWPlayerNoiseRulesUw2 driven by UWPlayerTerrain at the
  original's 20 calls a second, the speed measured as the first game's footsteps, which no longer play on the second
  game; easy steps from UWPlayerMovement; no pan, DEVIATIONS; test list section 39); test list section 38. G2 COMPLETE but its test.
- G3 TRAPS AND TRIGGERS: the second game's TrapKinds and DoActions tables (UE's nine new kinds, hack traps), move
  triggers for the player beyond teleports (UWMotionHost.fFireWalkTriggers) and for creatures, door, look, pick-up
  and use triggers, the trap search, the wandering monsters (RunRespawn 0, FiveMinuteBlockDue, YearlyCheckup), the
  level exit's tidying (WalkTeleport skips UWLevelExit.LeaveLevel), the automap flag pieces.
  G3 PLANNED 2026-11-13 (from two surveys: Underworld Exhumed docs/behaviour/traps-triggers.md and uw2
  event/TRIGGER.C against uw1, and our own gates). THE WAY (decided by the user 2026-11-13 after the comparison: the
  second game's TRIGGER.C and WORLDEV.C are some 3500 lines against the first's 1500, nearly every step differs in
  detail - the chain's going on and its else branch, numbered variables, who may set a trigger off, checkTrap over every
  trigger, seven changed and ten new kinds, forty hack traps of its own): SHARED AND LEFT AS IT IS, the frame -
  UWTrapTable and UWDoActionTable as types, IUWTrapHost, UWTrapChainRemoval, UWTriggerPacer, UWTrapLog, UWTrapDisarming
  where it fits, UWRespawnRules, UWLevelExit, and UWTriggerSystem with a switch by game; THE SECOND GAME'S OWN CLASSES
  for what the original kept apart - the chain walk with the else branch and the setters (UWTrapRulesUw2), its kinds,
  the numbered variables, the hack traps - filling UWGameProfileUw2.TrapKinds, DoActions and Traps (all throw today);
  the kinds that are the same (0x180 damage, 0x182 arrow, 0x186 spell, 0x189 ward) are CALLED from the first game's
  rules, not copied. So UWTrapRules (2400 lines, tested on the first game) stays almost untouched and the first game
  needs a counter-test only where the switch sits; the doubled part is the chain walk, which differs anyway, as the
  two TRIGGER.C files show. Not against the lesson of GitHub issue 37: there a convenience in one game went beside
  its own rule; here each game has its own rule on one frame, as the second game's magic (UWTargetSpellRulesUw2,
  UWExtraSpellRulesUw2 on the shared casting). THE GATES that come off one by one: UWMotionHost.fFireWalkTriggers (teleports only) and the
  creature/thing triggers in LateUpdate, UWTriggerSystem.fSecondGameWaits, UWDoor's open trigger, the walk's pick-up
  and use, UWLookService's look trigger and trap search, UWSpellHostUw2.FireLookTrigger, UWWorld.RunRespawn,
  UWVitalsUw2's missing FiveMinuteBlockDue, the walk's level change without UWLevelExit.LeaveLevel. SIX PARTS:
  H1 THE SECOND GAME'S ENGINE: UWTrapRulesUw2's chain walk on the shared frame, the switch in UWTriggerSystem, the
     tables with the kinds both games share (0x180 damage, 0x182 arrow,
     0x186 spell, 0x189 ward, 0x18F null, 0x190 text - UW2 only with owner bit 5 clear) and the ones UW2 changed
     (0x181 teleport's fine-x bits: from the ceiling, the facing, the map scrap; 0x185 terrain moving the neighbours'
     things; 0x187 create object with the castle monster for template 0x7F and nothing in world 6 after quest 7;
     0x188 door: the lock swapped only with owner set and after the move, the chain going on with owner 0; 0x18B
     delete through trap_obj_del and the animation list; 0x18C inventory with worn and the else chain); the numbered
     variables of 0x18D/0x18E (0-FF vars, 100-17F quest bits, 180-18F quest bytes, 190-19F X clocks, do_math_op's
     eight ops, XC_CHANGED); the chain's SetOffTrap and RUN_ELSE_CHAIN; UseTrigger's setters (things need id bit 9 on
     UW2); the player's and the creatures' move triggers on the full engine instead of the teleport-only walk.
  H2 TRAP SEARCH AND DISARM: DetectedTrap and RemoveTrap from the look and the two spells (test 30.6 with a real
     trap), Remove Trap's fumble setting the trap off, Open setting off a lock's trap.
  H3 THE NEW TRAP KINDS: 0x184 sfx (sound, shake, flash), 0x18A skill-check condition, 0x191 experience, 0x192 jump,
     0x193/0x194 change-from/to, 0x195 oscillator (with the sunken moongate on level 0x44), 0x196 proximity
     condition, 0x197 pit, 0x198 bridge (place_bridge, destroy_bridge).
  H4 THE TRIGGER KINDS AND THEIR SITES: the Triggers[16] modes from OBJECTS.DAT; pick-up (2), use (4, the sites the
     walk has; the rest with G4), look (5) and Reveal, enter/exit (6/0xE, check_pplate on a change of square),
     pressure/release (7/0xF with the plate's state in fine y, update_pplate), open/close (8/9), unlock (0xB), timer
     (0xA: the level's list of up to 64, every z+1 ticks within 8 squares of the player or the roaming eye);
     checkTrap walking every trigger and a switch's second state taking the second trap.
  H5 TIME AND LEVELS: DoWanderingMonsters (create-object traps of flags 0 with a mobile template, out of sight; one
     in four every 30th update, and on sleep), yearly_checkup, DoClosingDoors on sleep, FiveMinuteBlockDue on
     UWVitalsUw2, the level exit's tidying (update_all_critters_whilst_player_snoozes, init_level_creature_stuff,
     Sched_SetAllClocks), the automap pieces (the teleport's map scrap, hack 45).
  H6 THE HACK TRAPS (do_trap_hack, 0x183): first the general ones (2 crystal ball, 3/4 eight-position switch, 5
     trespass with owner, 21/22 raise or lower, 23/28 owner, 27 quality, 37 clocks, 39 invisible, 43 goal by whoami,
     44 sleep, 45 map unseen, 62 the triggerer's goal), then the world's own (10 arena prize, 11 fraznium field, 12
     standing wave, 14 texture cycling, 17 thin ice, 18 flip switch, 19 arrow pillars, 20 Scintillus pillars, 24
     graffiti, 25 Bliy Skup, 26 force field, 29 random switches, 30/31 arena, 32 q*bert floor, 33 bottles, 34 Britannia
     goes dry, 35 light spheres, 36 castle household, 38 cure potions, 40-42 vending machines, 54/55 blackrock gem) -
     those that need G4 (using things) or G6 (schedules, plot deaths) wait for them.
  H1 CUT IN TWO (2026-11-13): H1A BUILT the same day - UWTrapRulesUw2 (UseTrigger with the switch pass-on, the 16
     modes of OBJECTS.DAT 0xD92 for both trigger classes, the setter test with id bit 9 for things; SetOffTrap and
     UseTrap's chain, RUN_ELSE_CHAIN restricted to traps and triggers; set_numbered_variable and the check), the
     profile's TrapKinds, DoActions (empty) and Traps (UWTrapVariantUw2: the fishing bite, the lock swap), the kinds
     damage (UWTrapRules.FireDamageTrap, the same whack_thing), arrow (the first game's launch plus sound 0xA), teleport
     (IUWWorld.TeleportSecondGame, the walk's blink, nobody but the player), set and check variable, null, text (owner
     bit 5 with b62_5); UWTrapChainRemoval with both trigger classes for the second game (kill_triggers; the timer
     list waits for H4); the move triggers of the player, creatures and things on it (UWMotionHost.TriggerMove takes
     every trigger of mode 0, UWTriggerSystem.TryFireMoveTriggerUw2 with the scroll's presentation). Not called for
     the second game: the ward (no ward trap in its levels, cast_trap_spell unused). uwdump traps --game uw2 lists
     every trigger and its chain and counts the kinds (teleport 401, hack 218, terrain 187, door 140, create 96, spell
     73, pit 72, set 65, oscillator 53, check 45, text 35, delete 31, jump 31). Self-check Uw2Traps, test list section 40.
     H1B BUILT 2026-11-13: terrain (change_terrain of the second game: bounds 0xE/0xF/0x3F/10, water, lava or rock
     removing lying things by Obj_Punt's culling, the neighbours' overlapping things by filter, above the ceiling
     struck for 0xFF; creatures of the changed square through the host), create object (the first game's copy after
     the quantity bit and the Tombs' quest 7; the adventurer template's random castle monster waits for H5), door
     (moves first, the lock only with an owner, UWTrapVariantUw2.SwapsLock), delete object (a trap or trigger through
     UWTrapChainRemoval.DeleteTrapObjectSecondGame = trap_obj_del, else the world's Remove), inventory (FindObj with its
     slot, ObjWorn, the count, the else branch), spell (classes 5, 11/9, 13, 14 and the latent 6-8 with the trap as
     the caster: UWTrapHost.FireSpellTrap's second-game branch - a missile from the trap's spot, the player's host for
     class 13, a creature-like host at the trap for the rest; 0-4, 9, 10 and 11 but Tremor do nothing). Self-check
     Uw2Traps on real chains (levels 25, 1, 13), test list section 41. H1 COMPLETE; next H2 (trap search and disarm).
  H2 BUILT 2026-11-13: UWTrapRulesUw2.DetectTrap and RemoveTrap (SKILLS.C:874/893 - disarmed: delete_trap at the
     trigger's square through UWTrapChainRemoval.AfterOneShotTrigger, both trigger classes; fumbled: UseTrigger with
     mode -1 as by the player with the thing acted on; else "Unable to defuse trap."), CheckTrap (OBJUSE.C:388, every
     trigger in a thing's chain for a mode, the switch's one, the plain trap on a use), the look's search on the walk
     (UWWorldViewUw2.fSearchForTrap: Look lit or the modern look key, wyorn's question in the scroll -
     UWGameScreenUw2.AskYesNo on UWYesNoPrompt, the first game's yes/no input moved out of UWPromptController so both
     games share it), Remove Trap ("if (DetectedTrap(...))": any detection but a plain failure), Detect Trap, Open's
     checkTrap 0xB. Fixed: UWObjectSpellRulesUw2.TryFindTrap asked for the trigger's quantity bit clear and found no trap
     behind most triggers. Self-check: level 13's trapped chests at 33/37 and 34/37. Test list section 42; test spot
     level 26's doors 18/54 and 18/45. Next H3 (the new trap kinds).
  H3 BUILT 2026-11-13: UWTrapRulesUw2's own kinds - special effect (do_sfx: sound 0x64 + owner, shake owner * 2, the
     flash through FlashWindow, which the second game's presentation does not draw yet), skill (attribute, 15 or a skill
     against owner * 3, rolled with heading 1; passing takes the else branch), experience ((q * 8 + (o & 7) - 0x100)
     << (o >> 3) through UWPlayerVitals.ChangeExperience), jump (set_jmp on the player: IUWPlayerAvatar.Jump ->
     UWPlayerMotion.Jump; a creature's or thing's not built), change from / to (do_change_grokking over the area or the
     map), oscillator (height, floor or wall stepped within quality..owner, the turn kept in the trap's fine x, the
     sunken moongate on level 0x44), proximity (UWTrapFiring.SetterSpot for a creature, the avatar's FeetSpot for the
     player), pit, bridge (place_bridge, destroy_bridge). change_terrain takes adjust (1/3 step, 4 allows 15). The
     conditions are decided before the kind's log line, so the skill roll is made once. Self-check on real chains
     (levels 3, 5, 18, 33, 34). Test list section 43. Most of these hang on pressure, timer, open and use triggers: H4
     next.
  H4 BUILT 2026-11-13: the sites on checkTrap and UseTrigger - look (the walk's player_3dlook after the trap search,
     inv_look before the look, with the Search a hidden look trigger asks; Reveal through UWSpellHostUw2.FireLookTrigger
     with 45), pick-up (release_3d before the thing leaves its tile, then release on its plate), use (UseObj's checkTrap 4
     after a door or the bed; levers, switches and the rest with G4), doors (UWDoor.Toggle: open 8, close 9 by the
     mover, a swing turned round by nobody), a destroyed thing's use triggers (UWTriggerSystem.FireTraps). The plates
     (UWTrapRulesUw2.CheckPlates after check_pplate: the first trigger as enter/exit, then the pressure mode; plates set
     off as by the player through UWTrapHostAsPlayer, their state in fine y bit 0, the floor stepping with bit 1; the
     weight of check_weight with the player's load and the creatures by their records; MoverMoved after change_GrSq,
     set_phys_data and hgt_change) for the player and the creatures each frame (UWMotionHost.fFollowPlatesUw2), a thing
     coming to rest, put down or taken up. The timers (TickTimers after update_animobj: the level's list, every z + 1 of
     4 steps a second of game time, within 8 squares of the player or the roaming eye, not while time is frozen; a
     deleted timer trigger leaves the list, rem_timer_obj). UseTrigger now answers whether it set its trap off (the
     original's result), whatever the kind did. Self-check: level 2's enter trigger, level 26's heavy plate, level 3's
     timer. Test list section 44. NOT YET: a thing in flight over a plate (ours only when it comes to rest), the
     pressure test at every physics step (ours once a frame). Next H5 (time and levels).
  H5 BUILT 2026-11-13: DoWanderingMonsters (UWTrapRulesUw2.FireWanderingTrap: flags 0, a mobile template marked
     temporary, the trap run at its square; UWTriggerSystem.RespawnCreatures and FireTrapInTile take the walk's player and
     the second game's rules, UWWorld.RunRespawn no longer waits), the 30-tick block on UWVitalsUw2 (FiveMinuteBlockDue:
     the one-in-four roll and yearly_checkup, the first game's UWLevelExit.YearlyCheckup, the same code), the create trap
     on its own (the roll, the crowding, the Tombs, and the adventurer template's random castle monster: RollCastleMonster
     by the level in the world, XC_CASTLE and the character level, eligible by strength; the template set up and loner,
     the copy hostile and temporary; UWTrapRules' create wrapper of H1b removed), the level exit's tidying on the walk's
     level changes (UWLevelExit.LeaveLevel with the second game's rule that a creature coming to talk keeps its facing),
     the night's creature pass with the same rule. The sleep's DoClosingDoors and wandering monsters were the shared
     sleep's already (UWSleepRules), the doors now set off their close triggers (H4). NOT BUILT: init_level_creature_stuff
     on arrival (ours sets the creatures up on the level build), Sched_SetAllClocks and the schedule's door hack (G6),
     hack 45 (H6). Self-check: the castle monster's draw, a plain and an adventurer wandering trap of the shipped levels.
     Test list section 45. Next H6 (the hack traps).
  H6 CUT IN TWO (2026-11-13). H6A BUILT the same day: the profile's DoActions with the general hacks - 2 crystal ball
     (the trigger's square, not with Search 45), 11 fraznium field, 14 texture cycle, 17 thin ice, 21/22 object height,
     23/28 owner, 27 quality, 39 show or hide (IUWWorld.SetObjectHidden), 43 goal by whoami (IUWWorld.SetCreatureGoal),
     45 map unseen (the automap's low nibble), 62 the setter's goal (UWTrapFiring.SetterIndex). The second game draws no
     invisible thing any more (UWLevelLoader.fHideInvisibleObjects after the build; before, every ID_INVIS object but the
     traps was drawn). Self-check: level 3's look trigger showing a switch, level 1's wall picture owner, made-up 22 and
     14. Test list section 46. H6B NEXT: 32 q*bert (52 traps, needs the plate's square in the chain), 24 graffiti (the
     wall picture redrawn), 20 and 19 pillars, 12 standing wave, 26 force field, 29 and 18 switches, 35 light spheres,
     33 bottles, 34 courtyard, 38 cure potions, 25 Bliy Skup, 54/55 the gem; with G4 or G6: 5 trespass (the theft
     rules), 10/30/31 the arena, 36/37 the schedules, 40-42 vending, 44 sleep.
  H6B BUILT 2026-11-13: 55 black_gem_trip (the facet from the player's spot around 0xE3/0x143, open when used or shown,
     worlds[] to UWTeleportUw2.FromWord, QB_WORLDS_VISITED with facets 5 and 6 swapped), 54 black_gem_rotate (vars[6]
     by the castle plot), 12 standing wave (the phase kept in the trap), 18 switch flip (UWTrapRulesUw2.FlipSwitch through
     IUWTrapHost.ToggleSwitchVisual, its use triggers as by nobody - UWTrapHostAsPlayer now takes any setter), 29 random
     switches, 19 arrow pillars, 20 Scintillus pillars, 26 force fields, 35 light spheres (no sparkle). Self-check: the
     facets and draw, level 5's gem bounced and travelling, a wave and a switch. Test list section 47. STILL OPEN: 32
     q*bert (the plate's square in the chain), 24 graffiti, 33 bottles and 38 cure potions (a changed thing's picture
     redrawn), 34 the courtyard, 25 Bliy Skup (fire_trigger_at, G6); with G4/G6 trespass, arena, vending, schedules,
     sleep. G3 otherwise COMPLETE; next by the plan G4 (using things).
  H6C BUILT 2026-11-13 (per user "Build the rest"): 24 graffiti, 33 bottles, 38 cure potions (morpheus_ruin_potion, the
     player's pack and pointer when he stands there; the trap set as the potion's contents, UWObject.SetContents), 34 the
     courtyard (kill_plants, the fountains, the mushrooms; its stages per session as the original's static), 25 Bliy
     Skup (fire_trigger_at as UWTrapRulesUw2.FireTriggerAt, for G6 as well), 32 q*bert (all four owner ranges; the plate's
     square from CheckPlates through UWTrapHostAsPlayer.PlateTile). A changed thing's picture: IUWWorld.RedrawObject
     (UWWorldView.Redraw). Self-check: bottle, potion, Bliy Skup's crystals, the pyramid's colours. Test list section
     48. G3 IS COMPLETE but for the hacks waiting for G4/G6 (5 trespass, 10/30/31 arena, 36/37 schedules, 40-42 vending,
     44 sleep). Next G4.
  G4 PART A BUILT 2026-11-13 (per user: without using things the traps cannot be tested): UseRect's wall controls
     (UWUseRulesUw2.UseWallControl: switches flip_switch 3, the lever 0x161 and the switch picture 0x162 flags + 1 & 7,
     then checkTrap 4), other wall pictures looked at, every world use ends with checkTrap 4; the locked door's
     "The <door> is locked." with sound 0x2D; UseKey/UseThing as UWAttackUw2.BeginUseOn (GameInputMode 2 with
     ObjectActing, the item's picture on the classic pointer, ended by any click), UseKeyOn through
     UWLockRules.ToggleWithKey (IsOpenForKeySecondGame: an open door or container of class 0x80), UseLockpickOn as
     UWLockRules.PickLockSecondGame (Picklock + 1, the fumble Outcome.Fumbled breaks the pick unless Dexterity against
     20 saves it, no Picklock on an unlocked lock fails); an unlock fires checkTrap 0xB. The DEFAULT MODE's right
     button (player_3dlook, player_3dget with def_mode): the look at the press, a drag takes, talks to a creature or wisp,
     or uses on the release (per user). The use aims at the frontmost door or thing (pick_3d). Self-check: a level 1
     switch, lever and picture, the pick's fumble/save/hard lock, the key on an open container. Test list section 49.
     FIX the same day (per user, level 3's portcullis at 45/56 opened by hand): UWLockState finds the lock in the door's
     chain (FindLoadedLock, checkLock's Obj_InList) instead of taking the first object; 16 doors of the second game hold
     a door trap before the lock, none of the first.
     STILL OPEN in G4: UseCont in the world (DumpTheBag, theft), fountains, instruments, the fishing pole, potions and
     wands carried, oil and rock hammer on targets, UseUnique, the trespass/arena/vending/schedule/sleep hacks.
  G4 REST PART 1 BUILT 2026-11-16: potions and wands carried - UWCarriedSpellRulesUw2 (checkSpell with nextSpellTime's
     0x2FD ticks, useNSpellCharges, UseWand's crack to id + 4, a broken wand does nothing), the potions 0xE1-0xE7 through
     UWFoodUw2 (UseReag -> UseFood: the quaff line, sound 0x1E; every meal's spell with no wait, then checkTrap 4), the
     meals' sounds (0x25/0x1F/0x21 by hunger, 0x1E for drinks). Self-check Uw2CarriedSpells; test list section 57.
  G4 REST PART 2 BUILT 2026-11-16: UseMagic's class 0x12 things - UWToolRulesUw2 (rules, the second game's lines) and
     UWToolUseUw2 (Unity side): leeches (0xEF only when poisoned, cure, backfire 2d8 not below 3, used up), mandolin and
     flute (UWInstrumentPlayer.BeginSecondGame: 0x109/0x10A, notes 0x80 ticks, no cup), the fishing pole (UWFishing's
     water test, the Track bite, the fish onto the empty pointer, place_new), the oil flask on carried things only (wood ->
     torch 0x28, lantern/torch +0x20 to 0x3F, full, lit, else 0xC0; one off a stack onto the pointer), the rock hammer on
     a boulder in the world (the boulder goes first, fresh pieces put around it, range 6), the fountain in the world and
     through its class 9 animation (dry at quality 0, else the refresh line; a fountain's own spell is not cast - none
     shipped carries one). Every use from the pack ends with checkTrap 4. Self-check Uw2Tools; test list section 59.
  G4 REST PART 3A BUILT 2026-11-16: UseCont in the world (DumpTheBag: locked says so, else the chain down with UWSpill,
     player_did_bad with a can_own container's owner, "You empty the" / "is empty"), the theft on pick-up (player_grabbed
     before release_3d, UWTriggerSystem.PlayerGrabbedUw2: the first game's search without the knights, block 1 0xF0 +
     attitude, the owner cleared up to 0x1D and a container's contents; the guarded book 0x138 with its door bit: quest
     106 and player_did_bad(0x1C)), hack 5 trespass (the trap host's AngerRaceNearPlayer speaking through the second
     game's scroll), UseUnique (the stoppered bottle 0x8D, the horn: 0x161 below the djinn clock 6, else play_instrument(2)
     and 0x162; the key gems: UseKeyGem on the large gem - castle clock for gem 4, the gems' clock and lines, QB_GEMS_USED
     with 6 and 7 swapped, vars[6]). Self-check Uw2Tools; test list section 60.
  G4 REST PART 3B BUILT 2026-11-16: go_vend (hacks 40-42: vars[owner] the choice from the switch's flags, the price
     (value + 1) / 2 + 1 taken from the coins on the target square by vend_check_gold, the new thing at quality 0x3F; the
     selection line 0x15D), hack 44 player_sleep(owner) through IUWTrapHost.SleepByTrap (UWSleepUw2.OnTheGround, InBedroll,
     InBed), hacks 30/31 nothing outside a pit fight (in_pits; inside one not built, G6), the moonstones'
     two places on drop, throw and take (UWMoonstoneRules.OnPutIntoWorldSecondGame, OnTakenFromWorldSecondGame). G4 IS
     COMPLETE but for the arena inside a pit fight. Self-check Uw2Tools; test list section 61.
  G5 PLAN 2026-11-16 (survey of the 102 conversations by uwdump conv --game uw2, every call with its arguments;
     the rules from uw2 conv/BABLHACK.C, game/SKILLS.C, event/WORLDEV.C, critter/CRITTIME.C). ALREADY BUILT: x_traps,
     x_clock, teleport_player, switch_pic, gronk_door, set_attitude, set_race_attitude, x_obj_stuff, x_obj_pos,
     place_object, remove_talker, give_all_stuff, babl_hack 3/8/9, the trade's carry check (UseTradeSlot's EncumCheck,
     "That is too heavy to take." 0x10C) and creature-started talk (test list section 51). LEFT, by use in the data:
     - x_exp(n) 15 calls in 13 conversations (45 to 9999; e.g. the goblin guard 45 when he turns on the player, Lord
       British 300, Blog 1000, Jerry the Rat 9999; names are block 7 at whoami + 17):
       player_get_exp(n), the total / 16 back - the shared UWExperience.Change with the second game's variant.
     - x_skills 54 calls: value 10001 in 16 conversations (the trainers: Geoffrey 0/1, Dupre 4/5 (his sword lesson 3 is unreachable code), Iolo 6/18/19,
       Julia 10/14/16, Nystul 7/9, Nelson 8/11, Patterson and Lady Tory 15, Fissif 10/13/16/17, Syria 2/3, ...) spends a skill point on grant_skill_advance(skill) -
       get_skill: nothing above twice the governing attribute or at 30, else +1, +1 more when the attribute is not
       strength and half of it is above the skill, +1 more by chance (attribute - skill) / {25, 30, 15} while below
       the attribute; lore also clears the lore tries; player_compute after (the groups -1/-2/-3 are not called by
       any conversation, built anyway as the routine has them); 1 when it rose, else 0. Value 9999 only reads (14
       calls of skill 15). 10000 (get_skill without a point) and 0..30 (set) are not called but kept.
     - babl_hack 10 (Silanus and two soldiers, conversations 34, 51, 104): the Guardian's signet ring 0x35 on either ring slot.
     - babl_hack 7 (Merzan, conversation 105): useNSpellCharges(thing, -5) on a carried wand - UWCarriedSpellRulesUw2.
     - babl_hack 5 (Nelson, conversation 139): every creature with whoami 140 (Patterson) a loner.
     - teleport_talker(x, y) 14 calls (Zaria, Dorstag, a warrior, Relk): teleport_critter when the conversation is
       over (do_babl_teleport): set_gridx_and_y_based_on_tile_type, a flier half way up to 0x80, can_place, the
       transient bits cleared; nothing when it does not fit.
     - transform_talker(item, whoami, powerful, terrain) 2 calls (Trystero: creature 8, whoami 27, powerful; Fissif:
       the fourth value only): transform_creature, -1 keeps a value, settled on the floor unless a flier.
     - set_sequence(whoami, seq, frame) 1 call (Nelson: whoami 139, sequence 7, frame 0): the animation of every
       creature with that whoami.
     LEFT TO G6: babl_hack 0, 1, 2 and 4 (in every conversation: the pit fights, place_pitfighter, running_away,
     in_pits) with the arena.
     PART 1 BUILT 2026-11-16 (29afe209): x_exp through ChangeExperience; x_skills by UWConversationRulesUw2.SkillCall,
     above 10000 IUWConversationHost.AdvancePlayerSkillByPoint -> UWPlayerVitals.GrantSkillAdvance (get_skill =
     TryIncreaseSkill, the second game's skill_rng 25/30/15 in its rule set), the skills and maxima written into the
     record (UWVitalsUw2.WriteSkillsToRecord), a set 0..30 into both; babl_hack 10 and 7 (UWConversationRulesUw2).
     Self-check Uw2ConversationEffects; test list section 62.
     PART 2 BUILT 2026-11-16 (fb44abd0): UWConversationRulesUw2.MakeLoner, SetSequence (the record's raw sequence),
     TeleportCritter (IUWWorld.TeleportCreature, UWCreatureActor.TeleportTo; done at the conversation's end by
     UWConversationSession.DoTalkerTeleport), TransformCreature (the original's terrain field and can_place kept; the
     creature redrawn). Self-check Uw2TalkerEffects; test list section 63. G5 IS COMPLETE but for babl_hack 0/1/2/4 (G6).
     PARTS: 1 the player (x_exp, x_skills' advance, the signet ring, the wand's charges); 2 the talker (teleport_talker,
     transform_talker, set_sequence, the loner bit). Each part engine-free rules (UWConversationRulesUw2) with
     self-checks, then its test list section from 62.
     DJINN STEP 0 -> 2 BUILT 2026-11-14 (per user, the quest replayed from the start): do_filanium as
     UWDjinnRulesUw2.OilInMud (spell 0xD/3 cast on use, floor texture 0xC1 = level 26's mud at 58-60/15-16), called from
     both water landings (UWMotionHost.Splash for mob_to_static, UWWorldEdit.PutInWater for obj_deal), the line to the
     scroll. The oil is a colorless potion 0xE6 with link 0x243 (levels 3, 57, 59, 67). No conversation sets the clock;
     Zoranthus (conversation 96, Pits of Carnage) only tells the recipe. Self-check uw2 djinn; test list section 50. STEP 5 -> 6 BUILT the same day: remove_object's bottle branch as UWDjinnRulesUw2.BottleBroken (before 5 the daemon rends, 0xFF untyped; from 5 at level 69 21-22/52-53 absorbed: fill_FB(2), clock 6, castle 0xD -> 0xE, quests[26] bit 1, the bottle gone; elsewhere the flash and debris), hooked into UWMissiles.BecomeDebrisInFlight, UWTileBlast and UWContainerDamage: per user a blow hits the bottle in the original (resolve_attack's ObjectCheck takes every object with a height and the touch bit; of the second game's loose things only 0x116 has a height), so every loose sprite with a body gets health by the data alone (UWObjectSpawner; class 3 takes the blow and no harm, as the models - the fountain in both games, 0x117 in the first; for mods the same rule). UW2's fill_FB flashes were silent: UWSleepUw2's presentation now shows them. Still open: what reads step 6 (the horn, UseUnique; WORLDEV hack 0x8C) (BUILT, stale entry corrected 2026-11-19: the horn in G4 rest part 3A, `UWToolRulesUw2.HornSounds` from `UWToolUseUw2`; whoami 0x8C is Patterson's death_check in G6 part 2, `UWSpecialDeathsUw2` reading the djinn clock for the castle plot).
  LEFT TO G6: the scheduled triggers (mode 0xC, SCDEVENT.C ev_trigger, call_out_the_guards; TEST when built, from test list 51.2 on 2026-11-16: the castle people's day, hack 36 on level 1 (move_folks_around, CRITTIME.C maybe_go_hang_out: whoami 0x81-0x8F and 0xA8 get a new home and goal 1 while quest 109 is set and the castle plot is below 16), fired by the level 1 schedule's trigger at 30/44 every six units and caught up after every conversation (free_converse, Sched_SetAllClocks) - in the original Charles, set to goal 10, talked once and then went home with nearly all of the castle (SAVE3 against the original's SAVE4); TEST when built, moved from test list 48.4 on 2026-11-16: the castle courtyard, hack 34 on level 1 set off only by the scheduled trigger at 28/39 - plants wilt with the castle plot, fountains dry, later mushrooms), fire_trigger_at for plot
  deaths; TEST when built, from test list 62.2 on 2026-11-16: the goblin guard of level 10 (conversation 1) sets quest 60
  on his hostile ending, and SCD.ARK block 15 rows 0-2 for world 1 (level byte 0xF7) test it (event 10, variable 0x13C)
  and turn every goblin (race 6) hostile with goal 5 on the player (events 8 and 1) - in the original all of them attack
  at once after the talk, ours only the guard (per user), jail and the Prison Tower's level 2, do_level_hacks' world rules (the Void's automap, Killorn's crash, the TK
  wand, the Tombs' genocide). ORDER: H1, H2 (it finishes section 30), H3, H4, H5, H6; each part its test list section
  and checks on the second game's levels (TrapChecks run on UW1 only today).
- G4 USING THINGS (SUPERSEDED OUTLINE, stale entry corrected 2026-11-19: built by the detailed G4 parts above)
  (needs G3 for levers and G1 for potions and wands): use on a target (keys, lockpicks - a locked
  door only says so), levers, switches, buttons, chests opened in place, shrines, fountains; carried potions, wands,
  keys, picks, instruments, the fishing pole; taking with its consequences (theft anger, pick-up triggers, pressure
  plates, the moonstone).
- G5 CONVERSATIONS' EFFECTS (SUPERSEDED OUTLINE, stale entry corrected 2026-11-19: built by the detailed G5
  parts above): x_exp, x_skills (trainers; TryIncreasePlayerSkill only logs), set_sequence,
  transform_talker, teleport_talker, babl_hack (signet ring, pit modes, wand charges), the trade's carry check;
  creature-started talk goes to the first game's Interaction and UWConversationController.
- G6 PLAN 2026-11-16 (survey of uw2 event/SCHEDULE.C, SCDEVENT.C, WORLDEV.C, game/SKILLS.C, GAMEWRAP.C and the shipped
  SCD.ARK: 219 rows in blocks 0 (the day, 51), 1 (the castle plot, 78), 14 (6) and 15 (the world rules, 84); events
  10 test variables 61, 5 fire scheduled triggers 56, 2 teleport 26, 7 hacks 25 (door closing 12, freeze 3, soldier 3,
  garg 2, nystul 3, gotha 3), 1 change goal 11, 3 kill 11, 11 remove 10, 8 attitude 9, 9 set a variable 6, 4 a quest
  bit 1). Ours reads SCD.ARK and copies it with a save, and runs nothing. In parts:
  1 THE SCHEDULER: the 16 blocks held in the session (the save's SCD.ARK, written back with a save), each level's clock
    and next row, Sched_SetTime/WrapTime (block 0 by XC_TIME, 72 steps a day, PLAYTIME.C), Sched_SetAllClocks after a
    conversation, a level change, a restore and an X clock change (x_clock, the clock traps, world events), rows by level
    (0xFF any, 0xF6 + world), once rows deleted, Sched_Insert and the migration queue. Events 0-11 with gronk_critid's
    four selections (whoami, race, one object, all, loners skipped where the original does): goal, teleport, kill,
    quest bit, attitude, variable, the conditional blocks of event 10 and remove; event 5's scheduled triggers (trap
    minor class 0xC, ev_trigger) and with them hack 34 (courtyard_hacking) and hack 36 (move_folks_around); event 7's
    hacks 1-7 (garg, soldier, freeze, doors, nystul, gotha). Engine-free UWScheduleRulesUw2 behind a host for the
    creatures and the world. Tests waiting: the goblins of world 1 after the guard's hostile ending (quest 60), the
    castle's day and Charles (hack 36), the courtyard (hack 34), doors closing at night.
    PART 1 BUILT 2026-11-16 (0667ec3c, fixes 528a986d), CONFIRMED in the game per user the same day, test list section
    64: the goblins, the castle's day, the doors; the original's save of the same test has the same SCD.ARK. The
    courtyard (hack 34) is not tested yet.
  2 THE PLOT'S DEATHS: death_check (WORLDEV.C:703) modes 0 and 1 by whoami - the castle's people who cannot die and
    call_out_the_guards, Lady Tory after castle plot 8, Patterson and Nelson, Altara, Mokpo, Mystell, Bliy Skup, the
    guard 0x4B turning into a hordling, Freemis, Zaria, Dorstag, Blog, Bishop, Relk, Lord Umbria, Praecor Loth and his
    liches, Mors Gotha, the Listener and Altara's dagger, Krilner and Fissif who yield and talk (stop_and_talk), race 0xB,
    the bloodworm count; HasSpecialDeath for the second game.
    PART 2 BUILT 2026-11-16 (dd62e5d0, fix 8a254b91), CONFIRMED in the game per user the same day, test list section
    65: the castle guard, Patterson's last talk, Fissif, the Listener and the dagger, the hordling guard, the counted
    brain creatures. The pit cases wait for part 4.
  3 THE PLAYER'S DEATH (player_is_dead, SKILLS.C:802, on top of UWDeathRulesUw2's gem and end): jail for a castle
    guard's kill (put_player_in_jail, WORLDEV.C:2283), waking from the Void (punt_void), the lost pit fight.
    PART 3 BUILT 2026-11-16 (7aad6e44), CONFIRMED in the game per user 2026-11-17, test list section 66: jail by the
    castle's people, compared with the original; another killer the end. The Void's waking waits for the Void.
  4 THE ARENA: babl_hack 0, 1, 2 and 4 (the challenge, running away, place_pitfighter's opponents and Jospur's debt,
    in_pits), set_me_inarena, remove_opponent and the pit record, the arena hacks 30/31 (arena_player_runs), in_arena.
    PART 4 BUILT 2026-11-17 (1b0d7cf3, fixes 844d1da7, 2b1bb520, e0d137da), CONFIRMED in the game per user the same
    day, test list section 68, compared with the original: a fight at Jospur's paid, running away. Hack 31 not built.
  5 THE LEVEL HACKS (do_level_hacks, GAMEWRAP.C:443): the Prison Tower's alarm on leaving (prison_alarm_check) and its
    level 2's two triggers, Killorn's crash (Killorn_just_crashed) and the telekinesis wand taken away (remove_TK_wand),
    the Tombs' genocide after Loth's death, the castle door on arrival (gruesome_door_hack); the Void's automap is built.
    Goal 15 creatures and a refused spawn rule's objects (the old list) looked at here too.
    PART 5 BUILT 2026-11-17 (33fab84d), CONFIRMED in the game per user the same day, test list section 69 (the
    telekinesis wand; the rest in the self-check). STILL OPEN OF G6: the Ethereal Void (go_void, punt_void, do_dreamret),
    goal 15 creatures, a refused spawn rule's objects. THE VOID AND KILLORN'S COUNTDOWN BUILT 2026-11-17 (3f6d3b3e, fixes
    28e92a5c, 40c42633, fbad88c9), CONFIRMED the same day, test list section 70, compared with the original. G6 COMPLETE
    2026-11-17: goal 15 creatures keep their brain over a load (8bc49285, section 71); the refused spawn rule's objects
    are the phase 2 safety net in UWObjectSpawner, which reports each kind left out - none reported.
  ORDER: 1 first, as it unlocks most of the waiting tests and parts 2 and 4 lean on it (XC_CHANGED, the castle plot);
  each part its rules engine-free with self-checks, then its test list section from 64.
- G6 THE WORLD (SUPERSEDED OUTLINE, stale entry corrected 2026-11-19: the G6 plan above, complete 2026-11-17):
  SCD.ARK schedules, plot deaths (HasSpecialDeath false), jail, the Void and the pits on death, goal 15
  creatures (pictures only, no brain or hit points), objects left out by a refused spawn rule, the world and level
  hacks (step 7 below).
- PHASE 5 as it stands (SUPERSEDED OUTLINE, stale entry corrected 2026-11-19: phase 5 parts A-D below, the music
  and the presentation built, confirmed 2026-11-18): music (none in UW2, the combat flag from the idle Interaction), death cries, cutscenes; the
  world's presentation (UWPresentation over the idle Interaction and UWGameUI) for any rule that does not use the
  NightPresentation wrapper.
- SAVE: complete as far as built; creature alarms and missiles in flight are not written (UW1 writes them), unsure
  whether the second game keeps them.

THE FIRST OUTLINE OF PHASE 4, kept for its history (SUPERSEDED, stale entry corrected 2026-11-19: the work went by
the G0-G6 plans above, all complete; its open points are not a to-do list):

1. Player: experience (halving, 1500 per point, the 0x7FFF0 cap), carrying, the slow cycle and
   the X clocks, food, sleep, death (jail, the pits, the blackrock gem), traps on objects.
2. Combat: the attack score with Valor, enchantments and special weapons, Poison Weapon, the
   poisonous blow, resistances, damaging doors and owned objects, combat sounds.
3. Magic: the casting check, circles of 8 and the Britannia limit, the area spell table, class 7
   with cursor aiming, healing the caster, the new spells, the Academy's mana. STAGE 1 (runes and casting,
   classes 0 to 4) CONFIRMED 2026-11-02. STAGE 2 the classes from 5, in five parts (planned 2026-11-05):
   A the spells without aiming - mana (class 10) with the Academy's rule on every restore_mana, class 11 by the
   second game's minors (UWExtraSpellRulesUw2: Speed, Portal, Restoration, Locate, Cure Poison, Telekinesis,
   Tremor with the Void's things, Gate Travel, Freeze Time, Armageddon, Dispel Hunger), class 13
   (UWSpecialSpellRulesUw2: a line of power, the mushroom spells, Map Area, mana), Iron Flesh's glaze, the rune
   page in the modern scheme (Z, the hollow casts, F8); B the area spells (class 6) by area_spells; C the
   missiles (class 5) with the aiming pointer in both schemes; D the target spells (class 7) on creatures and
   objects with the pointer, Roaming Sight's camera; E creating (class 8). PART B BUILT 2026-11-11
   (UWAreaSpellRulesUw2: area_spells[] with its fixed counts, distances and radii, sp_true_sight, sp_sheet_light,
   sp_confusion, sp_meteor on its own square, sp_repel_undead, sp_shockwave, sp_frost; UWAreaWalk's open squares of
   the second game's AREA_OBJECTS; floating skulls with the world's hacks, Reveal's triggers with G3), test list
   section 26 ALL CONFIRMED 2026-11-11 (fix on the way: effect pictures hang on the original's fixed hot spot
   8/16 and put_effect's height on a creature, both games, b4396a4f; and a save from Modern stores the nearest
   place the original accepts, da0bced3). PART C SPLIT 2026-11-12: C1 the four missiles that fly
   straight, C2 the homing dart (Deadly Seeker, check_homing in PATHFIND.C), the snowball, the two missiles of
   class 13 and the creatures' class 5 by the second game's table. PART C1 BUILT 2026-11-12: IUWMagicVariant.
   GetProjectileId (release_missile's six items plus 0x10), class 5 minors 1 to 4 built, the other fireball 0x1D
   explodes as the first (mts_doanim, UWObjectDamageRules), UWAttackUw2 holds the waiting missile (GameInputMode 3:
   the aiming pointer, the classic right button in the view, the modern attack button or at once with the pointer
   locked, Escape lets go; mana on release, string 0x10F without room, launch sound 0x28), test list section 27 ALL CONFIRMED 2026-11-12 (fixes on the way: no impact sound for the
   player's own fireball or bolt in the second game, a4d92f82; the modern scheme's free pointer on the right button,
   aiming from our pointer, the left button outside the HUD's windows, 1a5fb22c 7df56f9f b400ead0). Found the same day:
   the user's DOSBox Staging had sbtype sb16, the original's SB Pro speech driver failed and every effect fell back to
   AdLib; sbpro2 since. PART C2 BUILT 2026-11-12: UWHomingDartRules (check_homing after every
   step in flight: process_area's box with its inclusive bounds, store_targ's fine Manhattan distance, get_theta over
   UWVanillaMath, merge_angles, the pitch with its unsigned height difference, the weave, the trail class 14, a hit
   point one step in four, the loner bit set by a creature hit in UWMissiles), class 5 minors 5 and 6 built, class 13's
   acid and snowball through do_spell(5, 4/6), the creatures' class 5 by the second game's table (AllSpells is the
   game's), test list section 28 ALL CONFIRMED 2026-11-12 - PART C COMPLETE (fixes on the way: animation
   objects above OBJECTS.GR's last picture are made, so the trail 0x1CE shows, 40b92607; a spell's missile leaves with
   spell_fire's 0x28 for creatures too, f20a4e99). PART D SPLIT 2026-11-12 (read in uw2 SPELLS.C nail_1area,
   target_spells, obj_spells, SPELLS2.C sp_study_monster, ui/INTERACT.C mous_in_3d case 2): D1 the target mode
   (GameInputMode 2, pointer 0x1076, pick_3d with InPickRange(PickDist) and BlockingTerrain or "You cannot reach that.",
   nothing picked keeps waiting, the mana paid only when the handler took) and the creature spells of area1_spells
   0..6 (sp_bleed, sp_fear, sp_ward_undead with the liche's half, sp_charm by strings 0x15E/0x15F and remove_opponent,
   sp_poison, sp_hold with the held goal 15 counting down in the brain, sp_smite); D2 obj_spells 8..15 (Dispel Rune,
   Mending, Remove Trap, Name Enchantment, Open, Detect Trap, Enchantment, Gate Travel by the moonstone) on the world
   and the inventory, and Study Monster (minor 7); D3 Roaming Sight. PART D1 BUILT 2026-11-12: UWTargetSpellRulesUw2 (minors 0..6 with the
   resistance, the pictures, wound_foe, the liche's half, the charm strings and RemovePitOpponent), the held goal 15 in
   the brain (IUWCritterVariant.CountsDownHeldGoal), IUWMagicVariant.TargetsByClick routes the player's class 7 to the
   target mode on UWAttackUw2 (cursor Enchant, reach by UWReachRules, mana on success, Escape lets go), IUWWorld.
   SetCreatureGoal and CreatureHitPoints, test list section 29. SECTION 29 ALL CONFIRMED 2026-11-12 (fixes: the target pointer in
   the cursor table 19464957, a click on the classic frame ends the spell 2f53dfc0, a held creature drawn in palette 3
   while its goal is 15, on every render path 941ff431 75289580). PART D2 BUILT 2026-11-12: UWObjectSpellRulesUw2 (mendable, DetectedTrap's
   and RemoveTrap's trigger of minor 3 and targets 10 + 2 * world and 8 + world, Gate Travel's choice on the two moonstone
   records, sp_study_monster's lines with study_monster_spells), UWObjectSpellUw2 on the clicked thing (Dispel Rune,
   Mending, Remove Trap with ClearSpecialChain, Name Enchantment through the look service, Open by UWLockRules.Unlock with
   -45, Detect Trap, Gate Travel), UWAttackUw2.ApplyTargetToItem for the inventory (classic page: a thing takes it, the
   arrows page, an empty place ends it; modern bags the same), UWPlayerVariantUw2's trap targets, test list section 30.
   Enchantment (sp_enchant, charging and tiers) is part D2b; a fumbled disarm and Open's lock trap wait for G3. SECTION 30
   CONFIRMED 2026-11-12 but traps with a trap (G3) (fixes: Mending redraws the panel 60293b29, reach to a thing by its
   record c564f778, UW2's checkLock roll was a throwing placeholder 71498e87; the moonstone record is not cleared when a
   stone is taken, G4; the save tool sets it with uw2set moonstones=a,b). PART D2b BUILT 2026-11-12: UWEnchantSpellRulesUw2
   (decode_obj_spell, chargeable, charge_object with its blow-up chance and 1 + Casting / 15 charges, the tiers of major
   0xC with the caster's reach, the first enchantment of a plain weapon or armour, debris_type), UWObjectSpellUw2's case 14
   with the blast (explosion 0x1C2 and its flames, 10d6 of magic and fire on the square), a destroyed carried thing's
   debris by the player; test list section 31. SECTION 31 CONFIRMED by the user 2026-11-12, the original
   compared (fixed on the way: a thing destroyed in the world becomes its debris in place, 1438fb88; a wand just picked up
   showed no spell or charges to the look, 9f9cca5e). PART D3 BUILT 2026-11-12: Roaming Sight (xt_spells case 7) on the
   first game's UWRoamingSight, now owned by either game (SpellEnder, PointerCell, a static running instance);
   UWVitalsUw2 begins and stops it with the effect, the Void says 0x142; GameInputMode 8 as gates on the UW2 screen
   (command bar, inventory, runes and cast, option shortcuts with 0xAE, the view's use, the attack, camping); test
   list section 32. SECTION 32 CONFIRMED by the user 2026-11-12 with the original (fixes c3989519 the modern HUD
   and pointer during the view, 430cf317 the noclip line; two deviations in DEVIATIONS.md: a loaded save with the
   spell raises the view, the keys glide where UW2 steps) - PART D COMPLETE. Next: part E (class 8). PART E SPLIT 2026-11-12: E1 Create
   Food and Summon Demon, E2 the runes of flame and stasis (placed at the caster's z + 12, UseRune when a body touches
   one - the collision's UseObj of usable objects, OBJPHYS.C), E3 the magic satellite (check_sat round its caster,
   missile_newhit never on the caster). PART E1 BUILT 2026-11-12: UWSummonSpellRules.CastSecondGame (nine eighths
   ahead with the swerve, 0x125 when there is no room, demons[] by (Casting + 0..29) / 12, the demon hostile with the
   player's tile as its destination as creat_spell sets every summoning but the player's Summon Monster); test list
   section 33. SECTION 33 CONFIRMED by the user 2026-11-12 with the original (fix on the way: can_place on
   the motion core so a door leaves no room, 76d8aad1, the first game too 2b0da597 - its regression, section 34, CONFIRMED by the user 2026-11-13). PART E2 BUILT 2026-11-13: UWRuneRulesUw2 (UseRune: the flam
   rune's 3d4 + 4 of plain fire, nobody's, and its explosion; the tym rune's paralysis 4 to 19 or a creature's hold by
   HitCritterGoal), placed by creat_spell's cases 2 and 3 at the caster's height + 12 through can_place; the runes
   drawn (do_obj draws every object without ID_INVIS); a body running into one queued by UWMotionHost.UseOnMover and
   fired after the motion; the second game's player paralysis (the counter down once a real second, no walking, no
   view or panel click, no fight, no cast); test list section 35. SECTION 35 CONFIRMED by the user 2026-11-13 with the original
   (the runes' flicker came with the colour cycling, c4eb28fc). PART E3 BUILT 2026-11-13: UWSatelliteRulesUw2 (check_sat round its
   source by IUWMobileObjectHost.TryGetSourceSpot, after every step that keeps it in flight, as the homing dart's
   steering; the start heading and height), UWMissiles.LaunchSatellite over IUWWorld.LaunchSatellite (63 hp, period 2,
   the caster as Owner, no launch sound), missile_newhit's skip of its own source in MissileHits and UseOnMover;
   creat_spell's case 6 through can_place at the floor; test list section 37. PART E COMPLETE (class 8 but the
   player's Summon Monster, which the second game gives creatures only). SECTION 37 CONFIRMED by the user 2026-11-13 (fix 5895783d: brushing its source
   cleared the collided bit only in the record, which the step overwrote - the core clears it now, UWMotionBody.Owner;
   walking through the satellite makes it hit less often afterwards, in the original as well). PART A BUILT
   2026-11-05, test list section 25 ALL CONFIRMED 2026-11-11 (fixes: taking aims with UWWorldPicker over the full
   range so Telekinesis takes from afar, 11e588a7; teleport traps, Portal and Gate Travel fade out and in as
   new_player_pos with NewPlyFade 3, d7570fc3).
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

SURVEY 2026-11-17 (Exhumed `sound/SOUND.C`, `gfx/CUTS.C`, `SKILLS.C`; the shipped data):
- Already game-aware: `UWSoundsDat` (8-byte entries, UWA/UWR names), `UWSound.GetEffectVoc` and
  `GetMusicTheme`, the digital SPnn/UWnn playback, the effect bank, the CUTS path, the OGG recordings' folders.
- UW2's themes (`include/sound.h:45-54`): 1 menu, 2 foe hurt, 3 combat, 4 danger, 5 armed, 6 victory,
  7 death, 8-15 walking by world (`walking_music[9][3]`, SOUND.C:175), 0x18/0x19 intro; 1, 6, 7 are
  followed by 10 (`loop_music_maybe`). UW1's numbers differ: `UWCritterRules.CombatTheme` 6 would be
  victory in UW2. `UWAudioMixCore.SelectPiece` asks for UW1 names (UW/AW), so UW2 is silent by missing
  files; the AdLib device keeps UW1's `UW.AD` after a game switch.
- Death cries (`crit_die`, AI.C:1432): critter byte 8's low 3 bits, 2/3/4 -> 0x22/0x23/0x24, else 6.
  `UWCritterVariantUw2.DeathCry` returns -1. `play_effect`'s remap of 0x5A/0x5B and the re-pan of a
  moving digital source are not in our audio.
- Cutscenes: the same LPF animations and .N00 scripts, plus LBACK000-007.BYT (raw 320x200). Our
  `UWCutsceneScript` knows 16 opcodes (opcode 5 with one argument); UW2 has 28 (5 without argument,
  16-27 new: palette ranges and fades with PALS.DAT, vscreen, focus, lback, pan, splity, music, test,
  wait_for_sound), data(996) a random CS034-037 picture, speech as BSPnn.VOC with say 998/999. No CS402
  in UW2 (the view's reference picture). UW1 rules in the runner (cutscenes 1-3 start theme 4).
- Triggers: title 9, intro 0 then 1 (menu and first start), credits 10, the ending 2 (`player_won_game`,
  screens 8/9 with stats, credits, back), `cs_check` after a conversation (quest byte 15 - 1, 2 wins),
  dreams 0x18+n (castle dreams 0-3 at XC_CASTLE 4/6/10/14 by `dreamflags`, else 1 in 4+4*sleepfactor
  a random 4-6), spell class 14, books with ID_FLAG10, final death 0x103.

PARTS, in build order:
- **5A Music playback and selector**: the pieces through `GetMusicTheme`, the devices on the game's
  banks (UW.AD, UW.MT), a UW2 selector (`change_music_maybe` with its 10 s and 8 s, `loop_music_maybe`,
  the walking table by world, -2 keeping the theme within a world), armed from the UW2 attack state,
  music on/off on the option panel. Test: menu, a walking theme per world, drawing the weapon.
  BUILT 2026-11-17: UWMusicSelectorUw2 (SOUND.C's five functions, engine-free; UWAudioService runs it for the
  second game's data and leaves the first game's requests out there), UWSound.GetMusic and the mods' piece test
  by UWSoundsDat.MusicPrefix, the AdLib music device on the running game's UW.AD (UWAudioMixCore.UseBank; every
  timbre UW2's pieces ask for is in its own UW.AD, checked). Hooks: the walk menu's theme 1 then 10, the walk's
  first theme (MAINMENU.C:224), a level change sheathed (-2), the fight command (pick_fightmode, punt_fightmode),
  and, from part B, the map (AUTOMAP.C:101, :232), conversations (CONVERSE.C:172, :235) and sleep (SKILLS.C:452,
  :555). The screens that loop on their own run loop_music_maybe, the walk change_music_maybe every frame (the
  original on each step of the game clock). The MT-32 path sends no UW.MT timbres, as for the first game. Self-check
  Uw2Music; one log line a piece ([Music] Theme n). Test list section 72 CONFIRMED by the user 2026-11-17 (fixes: 129f5cfd the main menu's open flag stuck after Create Character, which held the debug keys and kept the music in the menu's loop; 93a72175 the save's sound and music switches, word 0x302).
- **5B Music by the rules and death cries**: a UW2 critter variant for the combat themes (2/3/4 from
  AI.C's damage), victory 6, death 7, theme 10 for jail, gem and dreamret, `DeathCry` by byte 8, the
  0x5A/0x5B remap and the moving source (the sleep and conversation picks came with 5A).
  BUILT 2026-11-17: IUWCritterVariant.CombatTheme, FoeHurtTheme and KeepsPlayingTheme (the brain asks with the
  game's numbers; UW2 leaves a playing combat theme alone only on a swing), UWPresentation.ChangeMusicTheme to
  set_new_music for the second game, the combat timer through UWAudioService.MarkCombat; the player damaged by
  someone (a miss's 0 and his own missile too, not when it kills) 4 or 3 (UWVitalsUw2); victory on every kill
  (KillRewarded); death 7 by load_new_music after the pits, 10 at the gem, in jail and on waking from the Void
  (after the level's walking choice); UWCritterVariantUw2.DeathCry (shipped kinds 0:13 1:24 2:16 3:7 4:4, every
  cry in SOUNDS.DAT); play_effect's 0x5A/0x5B as 1 and 2. NOT BUILT: the digital source's re-pan and volume as the
  player moves (update_digi_playback; ours is mono and sets the volume once) (VOLUME BUILT, stale entry corrected
  2026-11-19: the volume follow built 2026-11-18, `UWAudioService.FollowDigitalSource`; the stereo pan stays out by
  decision). Test list section 73 CONFIRMED by the user 2026-11-17 (theme 4 giving way quickly to 3 is the original's: the player's own hit always asks for 3, and theme_changed is set only on a combat-to-combat change, so a theme 4 that followed a non-combat theme has no eight-second guard). Test: fights, a kill, jail, the Void,
  sleeping, talking.
- **5C The UW2 cutscene player**: a per-game opcode table, opcodes 16-27, LBACK and the virtual screen
  with pan and splity, PALS fades, data(996), BSP speech and 998/999, the music opcode, the window
  geometry. Test: title 9, intro 0+1, credits 10, CS403.
  READ 2026-11-17 (uw2 gfx/CUTS.C whole, GRFX.C fadein/fadeout, COLCYCLE.C rotate_bank, GRCORE/VIDMODE's
  virtual screen): the second game's player differs from the first's in its model, not only its opcodes -
  a file plays all its records unless next (5, no argument) or end comes, frame 997 runs before a file
  opens and 999 after it, loop replays the file with the script pointer left where it is, data(996)
  draws CS034-037, the palette is the file's (6-bit) set at frame 0 of each file once faded in, palrange
  and the palette fades (16 time-based tasks stepped every 8 ticks) work on it, colour cycles rotate
  every 0x38E / ((rate >> 8) & 0x3F) ticks, the virtual screen scrolls above the split and the bar below
  it holds the subtitles, LBACK pictures go into the virtual screen, LPF records are drawn at the view
  (the delta stream onto what is there), a record may carry a flag byte with extra data to skip.
  DECISION: a runner of its own for the second game (UWCutsceneRunnerUw2, after show_anm,
  cuts_process_anm, cuts_process_lp and cuts_process_opcodes) over an indexed screen model
  (UWCutsceneScreenUw2); the first game's runner and its segment model stay untouched.
  PARTS:
  - C1 engine-free: the script table per game (16-27, the variable palrange and palfade), the record's
    flag byte and drawing one record onto any buffer (UWCutscene), the screen model, the runner with its
    flags, waits, tasks, cycles and fades; self-checks: every shipped script parses to its end, every
    cutscene runs headless to its end with the files, frames and loops the original's code gives.
  - C2 Unity: the player picks the runner by game, the view shows the indexed screen through the shown
    palette, the subtitles drawn into the bar in FONTBIG, BSP speech, the music opcode; the walk menu's
    Introduction (0 then 1) and Acknowledgements (10), the title (9) before the menu. Test: those four.
  - The viewport cutscenes (0x103's window), the dreams, the ending and the Guardian's speeches are
    tested with their hooks in 5D.
  C1 AND C2 BUILT 2026-11-17: UWCutsceneScript by game (UnreadBytes 0 for all 18 shipped scripts),
  UWCutscene.DrawRecord, PlayedRecords, the pages and the cycles, UWCutsceneScreenUw2, UWCutsceneRunnerUw2,
  UWCutsceneTextUw2 (FONT_BIG); the intro player plays the second game's through it (UWCutsceneView.ShowIndexed),
  Escape to the runner (the fade-out after the file), keys as input; the walk menu's Introduction (0, 1) and
  Acknowledgements (10), the title (9) at the second game's first menu of a run and the introduction after it
  without a save (do_intro_scene); the music in change mode while a cutscene plays. Self-check Uw2Cutscenes pins
  the runs (the intro's 13 files N01-N07, N10, N12, N10, N13-N15 by data's octal digits). Frames rendered headless
  and looked at (intro, title, credits). Test list section 74 CONFIRMED by the user 2026-11-17 against the original (the title, the introduction with its file order, the slow credits, Escape fading out and a click doing nothing in the intro, the menu's theme afterwards). Not yet: the logos before the title (display_screen 5 and 6) (BUILT, stale
  entry corrected 2026-11-19: in 5D, `UWIntroPlayer.PlaySplashScreens` and `UWSplashSequence` for the second game), the start
  palette (ours starts black, the original fades from the screen before).
- **5D The story's hooks**: intro and acknowledgements in the menu and on first start, `dream()` with
  `dreamflags` and the castle clock, `NightPresentation` passing cutscenes and themes on, `cs_check`
  and `player_won_game`, 0x103 on the final death, spell class 14 and the ID_FLAG10 books on the UW2
  walk. Test: the castle clock by the save tool, Lord British's first talk, the Guardian's traps.
  BUILT 2026-11-17 (part D): the viewport window (show_cutscene's 0x10, 0xB6, 0xD0, 0x7F, 0x103 four rows higher
  and five taller; a record leaves out the frame's first 320 - w columns and 200 - h rows, the content painted at
  the bottom right - CS403 rendered), the game palette PALS 0 as the window's start, UWCutsceneView.PlaceWindow;
  the final death plays 0x103 (real_death(1)) before the menu; dream() as UWStoryRulesUw2.PickDream after the
  night (UWSleepRules.LastComfort the sleepfactor, dreamflags 0xF7, the castle clock); cs_check after every
  conversation (quest 143) and player_won_game as UWEndingUw2 (theme 1, cutscene 2, the end screens with
  game_stats in UWVictoryScreens, credits, the menu); the opening's logos and title through the existing opening
  (the title not cut short in the second game) and do_intro_scene after it without a save. The spell traps of
  class 14 play their cutscene (the Guardian's speeches CS004-007 on levels 17, 26, 49, 57) since da34d9e0: every
  UW2 trap host took UWSleepUw2's NightPresentation, whose PlayCutscene still said no; level 17's never goes off,
  in the original too (its enter trigger is not the first of its square, Todo: the six dead triggers). The
  books with ID_FLAG10 (cutscene 0x100 + link) not built: the shipped data has none that the scan found, and only
  CS403 exists from 0x100 on. Test list section 75 CONFIRMED by the user 2026-11-18 (the final death, the dreams with
  speech and subtitles, the Guardian after a talk, the ending, the opening, the speech on level 26) - PART D COMPLETE.
- **5E A full play-through** against the original in DOSBox.

### The modern interface for UW2: one interface, two hosts (L)

DECIDED 2026-11-23 per user, after the play-through of phase 5E: the modern interface gets no second copy
for UW2. Its windows talk to the running game through interfaces, as the modern conversation does since
phase 3 step 8 part F (`IUWModernConversationHost`), and each game brings a host. It comes before the beta
gate of phase 6. What phase 3 step 8 built for UW2 (`UWModernHudUw2`, `UWModernBarUw2`) was a stopgap and
goes when stage U4 stands.

SURVEYED 2026-11-23: 47 `UWModern*` classes of the first game, about 21,100 lines. The layout, the layout
editor, the message box, the crosshair, the backs and the window mechanics know no game. The rest reaches
the first game's objects through the HUD that owns the part (`UWModernPart`: `mOUi`, `mOCharacter`,
`mOInteraction`):

- `UWInventory` (nine classes, the bags above all): the object on the pointer, the equipment slots, the
  drags from pack, slot and container, the drops, the carried light, the weight, a container's refusal.
  The rules behind it are `UWInventoryModel`, which UW2 runs already (`UWGameScreenUw2.Inventory`).
- `UWCharacter`: the vitals (hit points, mana, level, active spells), the flasks' and the gem's pictures,
  the weapon in the hand, the paperdoll's pictures and where they come from.
- `Interaction`: what the crosshair is on (name, lock, verb), use, look and talk, the fight mode, a spell's
  aim, the questions (yes or no, the mantra, how many), the message lines, reading.
- `UWGameUI`: the texture filter, the runes and the shelf, the cast, the map, the options' refusals, the
  compass, track, camp and status, the window's picture, the pointer's picture.

None of these runs in a UW2 session. There `UWGameScreenUw2` holds the inventory and the screen,
`UWPlayerRecordUw2` and `UWVitalsUw2` the character, `UWWorldViewUw2` and `UWAttackUw2` what is done to
the world.

THE SHAPE. One entry, `IUWModernSession`, built when a session starts and handed to `UWModernUi`. It
bundles narrow interfaces by domain, not one that knows everything:

| Interface | What the windows get | UW1's host stands on | UW2's host stands on |
|---|---|---|---|
| `IUWModernInventory` | the `UWInventoryModel`, the game's slots and paperdoll places, dropping into the world, taking, using, looking, eating | `UWInventory`, `Interaction` | `UWGameScreenUw2` |
| `IUWModernCharacter` | name, class, level, experience, attributes and skills, hit points, mana, hunger, poison, the gem's state | `UWCharacter` | `UWPlayerRecordUw2`, `UWVitalsUw2` |
| `IUWModernMagic` | the runes carried, the shelf, the cast, the active spells and their texts | `UWRunicMagic`, `UWHudRunes`, `UWGameActions` | `UWRunicMagic`, `UWVitalsUw2`, `UWSpellHostUw2` |
| `IUWModernWorld` | the crosshair's target, use, look, talk, attack, the fight mode, a spell's aim, the map, sleep, track, the compass, the level | `Interaction`, `UWGameUI` | `UWWorldViewUw2`, `UWAttackUw2`, `UWGameScreenUw2` |
| `IUWModernMessages` | the lines for the message box, [MORE], the questions | `Interaction` | `UWGameScreenUw2`'s scroll |
| `IUWModernArt` | the game's freed pieces: paperdoll, panels, slots, flasks, gem, compass, hollow, shelf | `UWHudArt`, `UWModernHudArt` | new, from `PANELS.GR` and `BYT.ARK` |
| `IUWModernConversationHost` | stands | `UWModernConversationHostUw1` | `UWModernConversationHostUw2` |

The hosts are `UWModernSessionUw1` and `UWModernSessionUw2`. `UWGameSession` stays what it is, the
engine-free state of a run (quests, variables, rules); a host may read it, a window may not.

RULES.

1. The windows, the layout and the layout editor name no game and none of `UWCharacter`, `UWInventory`,
   `Interaction`, `UWGameUI`, `UWGameScreenUw2`. After stage U3 a scan of the `UWModern*` sources in the
   self-check holds that (the hosts and `UWModernUi`'s wiring excepted).
2. What needs no engine has none: state, and actions that answer with a message. Unity's types (textures,
   sprites) only in `IUWModernArt` and in the windows themselves. So the hosts' contracts can be checked
   with fakes in the self-check.
3. Where the games differ in kind, the session says which parts exist and brings the part: the Classic
   Wide frame, the command bar, the panel's slide, the trade's six places.
4. UW1 does not move. Domain by domain: first UW1 behind the interface with exactly today's behaviour and
   the user's test, then UW2's host for that domain.
5. An interface grows from what the windows ask today (the survey's lists), not from what a game could
   offer. A member only one window of one game needs stays in that game's part.

STAGES. Each of U1 to U3 ends with the first game's modern test repeated, unchanged.

- **U1 The entry, character and messages.** `IUWModernSession` and `UWModernSessionUw1`; `UWModernPart`
  hands the session on in place of `mOCharacter`. Vitals, flasks, gem, active spells, the weapon in the
  hand, the message box. BUILT AND USER-CONFIRMED 2026-11-23: the session on `UWModernUi.Session`
  (made in `UWModernHud.Start`), `IUWModernCharacter`, `IUWModernMagic` (the active spells as a picture
  of `SPELLS.GR` and a text per place, since the games keep them differently) and `IUWModernMessages`
  engine-free in `UWData`, `IUWModernArt` with the flasks, the gem and the weapon's frame. The scan of
  rule 1 waits for U3: `UWModernPanel`, `UWModernInventoryPage` and the rune panel still read
  `UWCharacter`.
- **U2 The inventory.** The bags (pages, drag, clicks, split, menu, trade, open bags), the action bar's
  carried pieces, the pointer's object, the character page's slots: the model directly, the world's part
  (drop, take, use, look, the carried light) through `IUWModernInventory`. BUILT AND USER-CONFIRMED
  2026-11-23: the windows used of `UWInventory` only what forwards one to one to the model, so they hold
  `UWInventoryModel` now (`UWModernUi.Inventory`); `IUWModernInventory` carries the use chain, the use
  mode's two targets, eating from the pointer, the drop or throw into the view, the look, reading and a
  spell waiting for a thing; `IUWModernMessages` says the windows' own lines. The context menu's three
  entries on a thing in the world wait for U3.
- **U3 Magic and the world.** The rune panel, the bar's casts, the crosshair and its target, use, look and
  talk, the questions, the minimap and the map, the game menu, the pointer. Then rule 1's scan.
  PARTS A AND B BUILT AND USER-CONFIRMED 2026-11-23. Part A: the game's data and the texture filter
  through `UWModernUi.Data` and `UWModernUi.Filter` in every window. Part B: the rune panel, the hollow,
  the action bar's casts and the clicks on active spells and the compass on `IUWModernMagic` (the bag's
  runes, what could be cast, the shelf, the two ways to cast, the learned hidden spells) and
  `IUWModernCharacter.ReportStatus`. `UWModernWorldInput` is the first game's world input in the modern
  scheme, on the host's side, and keeps `Interaction`. Part C, the world, is next: the crosshair's
  target, the questions, the fight mode, the menu's world entries, the map, the compass, the options'
  refusals, track and camp, the pointer's pictures and spell aiming.
  PART C BUILT AND USER-CONFIRMED 2026-11-23: `IUWModernWorld` (what covers the view, the crosshair's
  target, the fight mode, a spell's aim, a thing in the world, the map, the compass's step, track and
  camp, the menu's refusals), the questions on `IUWModernMessages`, the crosshair's, the compass's and
  the view's picture on `IUWModernArt`. Left before rule 1's scan (part D, with U5): the pages' character
  data, the fields kept only as null gates, `UWModernUiScheme`, the trade's link to the conversation.
  PART D BUILT AND USER-CONFIRMED 2026-11-23 (U1 to U3 complete): the pages read the character through
  `IUWModernCharacter.BindSheet` and the figure through `IUWModernArt`, the game menu saves and loads
  through `IUWModernWorld`, the session hands out the conversation's host, and `UWGameUI.Start` makes the
  first game's host before it adds the windows. No window has a `UWGameUI`, `UWCharacter` or `Interaction`
  field left. RULE 1'S SCAN RUNS in the self-check (`ModernInterfaceSeamChecks`, entry
  `ModernInterfaceSeam`). What is still the first game's in the windows is content, not a name: the
  paperdoll's and the classic layout's numbers, the pages of `PANELS.GR`, the Classic Wide frame - stages
  U5 to U7.
- **U4 UW2's host, the preset Modern.** `UWModernSessionUw2`; UW2 runs `UWModernHud` with the first game's
  windows: vitals, bags, action bar, rune panel, minimap, spells, messages, crosshair. The pieces UW1 cuts
  from its own art come from UW2's. `UWModernHudUw2` and `UWModernBarUw2` go; the bar's bindings kept in
  UWR.json stay readable. The automatic key, the windows with the free pointer and [MORE] under a fade
  (test list 86.2 and 86.3) are checked here.
  SURVEYED AND CUT INTO PARTS 2026-11-23. What helps: the inventory's rules for UW2 are inside the model
  already (`UWInventoryModel` asks the running game's `IUWInventoryVariant`), so the first game's bag
  windows get UW2's places and refusals without a change; `UWRunicMagic` asks the running game. What
  costs: nearly every window lies on art that `UWModernHudArt` cuts from the FIRST game's frame (about 25
  builders under `UWData/HudArt`: leather, slots, shelf, stone shelf, heading strip, hollow, compass disc,
  gem stand, paperdoll page), and UW2's frame (`BYT.ARK` block 4) is another picture - each piece needs
  UW2's own cut, checked by eye. The builders are engine-free, so a tool can write their pictures to PNG
  for review before anything is tried in the game. Parts, each replacing its piece of `UWModernHudUw2`
  and ending with the user's test in UW2 (UW1 repeated briefly):
  THE SAME MECHANISMS AS UW1 (decided per user 2026-11-23, after the bag use mode at test list 182 and the
  survey of the parts built so far): every part from here on uses the first game's mechanism for what the
  player sees and does in the modern scheme - its windows, its modes (the bag use mode, the context menus,
  dragging), its input (E tapped and held, the free pointer on the world, the pad's hints), its timing - and
  the host only answers the interfaces with the second game's rules. A second way beside the first game's
  is built only for a compelling reason (the original's rule differs, or the first game's mechanism cannot
  carry the second game's data), and that reason is written down where it is built. A member of the host
  left empty is either a later part's (named there) or such a reason, never a quiet gap. The surveys of
  each part check the host's empty members and the screen's own modern paths against this.
  - U4a the host `UWModernSessionUw2` (character and messages from `UWPlayerRecordUw2`, `UWVitalsUw2`
    and the screen's scroll; the world from `UWWorldViewUw2`, `UWAttackUw2`, `UWGameScreenUw2`) and the
    HUD's core in UW2: weapon, flasks with values, gem, messages, crosshair and target, active spells.
    `UWModernUi.IsSpellWaiting`'s second half moves into the host.
    STEP 1 BUILT AND USER-CONFIRMED 2026-11-23: `IUWModernSession.Has(part)` and `Supports(preset)`; the HUD's parts each in
    a group of its own, shown only while the game has the part. Next: the host with the parts that need
    no art work (messages, crosshair and target, active spells), then the flasks, the gem and the weapon,
    which need UW2's pictures and measures.
    STEP 2 BUILT AND USER-CONFIRMED 2026-11-23: `UWModernSessionUw2` over `UWGameScreenUw2`, set on
    `UWModernUi.Session` by the screen's modern frame; the first game's message box and crosshair show in
    UW2 and the stopgap's lines and pointer are off. The active spells' row waits for the minimap (part D),
    which it aligns with.
    STEP 3 BUILT AND USER-CONFIRMED 2026-11-23 (part U4a complete): the flasks, the gem and the weapon. The vitals ask the
    game for their art (`IUWModernArt.BuildVitalsArt`), the weapon's frame carries the game's view measures.
    UW2's flasks are composed into one picture per state and cut free of their black; they stand in a row
    without a shelf (decided per user 2026-11-23: they stay free for now).
  - U4b the bags and the action bar (the model directly; use, look, drop through the host), with the
    leather and the slots from UW2's art. `UWModernBarUw2` goes.
    SURVEYED 2026-11-23. What the bag windows and the bar ask beyond the model is `IUWModernInventory`
    (use, the use mode, eating from the pointer, drop or throw, look and read, a spell waiting for a thing),
    `IUWModernMessages.SayGeneral` and `General` (the first game's string numbers: the second game's host
    translates the few the bags say), `IUWModernWorld.ToggleFightMode` and `ShowPointerForBags`; the screen
    has all of it (`fUseCarried`, the eat of the held thing, `UWWorldViewUw2.DropHeld`, the look with its
    trap check and `ShowReading`, `UWAttackUw2.ApplyTargetToItem`, `fChoose`). WHAT THE CUT MUST RESPECT:
    the stopgap's bags show the pack's eight places AND the paperdoll's eleven; the first game's bag windows
    show the pack and the open bags only - the worn things are the character panel's (`UWModernPanel`, the
    bags ask it through `UWModernBagPages.PanelSlotAt`). Bags without the panel would leave no place to put
    a thing on, and two click systems side by side (the first game's drag and the stopgap's cells) would
    have to hand the pointer's thing to each other. DECIDED per user 2026-11-23 ("Ja, vorziehen"): the
    character panel comes forward from U4d and U5 into this part, in four steps -
    (1) the art: leather and slot circle per game; (2) the character panel on UW2 as a view (name, the
    paperdoll page from `PANELS.GR` 0 with UW2's places, the figure, the attributes and skills through
    `BindSheet`), the stopgap's cells still taking the clicks; (3) the bag windows switch on with the host's
    inventory half, the panel's slots take and give, the stopgap's cells and the matching blocks of
    `fModernKeys` go; (4) the action bar, `UWModernBarUw2` goes.
    STEP 1 BUILT 2026-11-23 (no change in the game yet, the pictures reviewed as PNG): `UWLeatherBuilder`
    has a `Frame` per game (`FrameOf`: where the panel picture has its frame, its calm centre and edges).
    UW2's INV.GR 6 is the open bag's dark marble, 75 x 41, frame 2/1/1/2 and no transparent columns; the
    constants `LeatherLeft` and so on stay the first game's and stay the windows' insets, the marble runs on
    under the pixels the frame is thinner by. `UWSlotBuilder` cuts UW2's circle from the inventory page
    (`PANELS.GR` 0, the left shoulder's ring, 16 across in a box of 17) and frees its ring by UW2's own luma
    bounds. NOT YET PER GAME: the heading strip (U4d) still composes with the first game's frame.
    STEP 2 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 179): the character panel shows on UW2.
    `UWPaperdollGeometry` (UWData, new) holds the page's measures per game - the page's place on the screen,
    the body's and the armour's places and drawing order, the six places with a thing's own picture, the
    place under a point (`SlotAt`: UW1's `UWClickRules.InventorySpotAt`, UW2's hit rectangles of InvDisplay) -
    and `UWModernPanel` reads them instead of the first game's tables. `UWPaperdollBuilder` builds UW2's
    page from `PANELS.GR` 0 (71 rows, nothing widened or removed), its marks by luma, and frees UW2's figure:
    the body is what differs from the page under it (the marble is grey, the figure is not), opaque (colour 0
    is the hair's black), a helmet that paints the page is freed the same way, and only the largest piece
    stays. The host binds the sheet as the help's Stats tab does, hands out the body and the armour's
    pictures (`ARMOR_F`/`ARMOR_M` by type and condition) and makes camp; `IUWModernWorld.OffersCamp` and
    `OffersTrack` say which of the panel's two buttons a game has (tracking is not built on UW2).
    INTERIM, GOES WITH STEP 3: the stopgap's cells, minimap and spells keep beside the open panel
    (`UWModernHudUw2.fBagsRight`, `fTopRightEdge`); `fModernKeys` toggles the panel on C and closes it on
    Escape after the bags; a click on the panel drops nothing into the world. The panel's places take no
    click on UW2 yet - the bag windows do the dragging (step 3).
    FOUND BY THE USER 2026-11-23 with a picture, fixed the same day: the things on the panel stood in black
    boxes. The first game's icons (`UWIconCache`, `UWUploadPixels.IconIndices`) take a pixel as clear by the
    palette's dark blue marker; the second game's pictures are clear on THEIR OWN colour 0. `UWTexture`
    carries it now (`ZeroIsClear`, set by `UWTextures` for UW2's OBJECTS, SPELLS, ARMOR_F and ARMOR_M) and
    the icons go by it - so every first-game window gets UW2's things right, the bags of step 3 included.
    SECOND FINDING per user the same day, the original beside ours: the armour's black outline was missing
    on the figure. It is the second game's near black (0, 0, 4), a colour with no red, no green and some blue, which the first game's palette rule (UWPalette) takes for its transparency marker and
    the resolved colour gives as clear (per user: the first game's finding is not the second's); `UWPaperdollBuilder.PieceUw2` now clears only the picture's own
    colour 0 and draws every other pixel.
    STEP 3 BUILT 2026-11-23, ALL 26 POINTS CONFIRMED BY THE USER THE SAME DAY (test list section 180), one wish
    beside them built and confirmed (180.27): a conversation opens the character panel on its Character tab and puts
    it back afterwards, as the first game's host does. The first game's bag windows run on
    UW2 (`Has(Bags)`); the stopgap's nineteen cells are gone, and with them `mbBagsOpen`, the pack-cell press, the
    cells' and the drop's part of `fClickBags` and the keys B, C and Escape's bag and panel steps in
    `fModernKeys` (Escape goes down `UWModernUi.CloseNext` after the rune page). The host's inventory half is
    the classic page's own routines on the screen (`ModernUse`, `ModernLook`, `ModernEatFromPointer`,
    `ModernDrop`, `ModernContainerOpened`, `ModernToggleFight`); the first game's use mode is not entered (a key
    waits on `UWAttackUw2` as from the classic page). NEW ON THE INTERFACES: `IUWModernInventory.ContainerOpened`
    (UseObj's traps and spell after UseCont; called where the player opens a bag window),
    `IUWModernWorld.HoldsPanels` (mous_in_panel: paralysed, asleep in the Void; part of
    `UWModernPointer.PanelsHeld`). ENGINE-FREE: `UWStringIds.InGame` maps the sentences the windows say to the
    second game's numbers (runes only 263, does not fit 264, no place 275, cannot barter 202), used by
    `UWItemMessages.ContainerRejection` and the host's `SayGeneral`. BEHAVIOUR AS THE FIRST GAME'S NOW: the bags
    are not modal (`IsPanelOpen` is the rune page alone), B only opens the windows, E takes straight into the
    pack (`UWWorldViewUw2.fStowTaken`; on the pointer with the pointer freed only where the pack has no room), E
    and Q over a slot use and look there, a stack is not asked for in the world. The conversation's host frees
    the pointer and gives it back as the first game's; `UWModernBagTrade.IsConversing` covers both games'
    conversations (no use, nothing into the world), and UW2's conversation screen takes a thing let go over the
    player's trade slot. The pointer's thing is drawn by the bag windows; the screen's own pointer shows nothing
    meanwhile (`ModernThingIsPointer`). The rune bag opens the screen's rune page (`UWModernBags.OpenRunes`)
    until U4c. KEPT AS IT WAS, UNTIL ITS PART: the screen's rune page, active spells and action bar
    (`IsOverOwnModernWindow`, counted as interface by `UWModernUi.IsOverUi`). NOT WIRED: the tooltip's line for
    an identified thing's magic on UW2 (the tables there are the first game's; it shows the plain name).
    STEP 4 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 181; part U4b complete with it): the first
    game's action bar runs on UW2 (`Has(ActionBar)`); `UWModernBarUw2` is deleted, and with it the slot keys'
    block of `fModernKeys`, `fUseBar`, `fClickBar` and `CaptureBar` (UWHelpNotes.SaveTo captures the bar and the
    open bags for both games). The places are read from the same UWR.json entries, so a save with the old bar
    keeps them. A weapon goes into the hand or is drawn there (`ToggleFightMode` through the host); the bar's
    keys stand while `IUWModernWorld.HoldsPanels`. E and Q with the free pointer over a slot reach the bar after
    the bags. Spells on the bar come with part U4c.
    ADDED 2026-11-23 per user at the test (test list section 182 open): as the first game's bar
    (UWCarriedItemUse, UWItemTargetUse.OnCrosshair), a tool whose target stands in the world acts on the
    crosshair's target at once - `UWToolUseUw2.TargetsWorld` (key, lockpick, rock hammer, pole, bones);
    `UWModernSessionUw2.Use` runs the classic use inside `UWAttackUw2.UseOnCrosshair`, and the classic use's
    `AskUseOn` (the question and BeginUseOn, now one place for UWLockUseUw2 and UWToolUseUw2) leaves the question
    out and applies along the crosshair's ray, through the click's own reach and actor. The oil flask, the anvil
    and the key gems still ask. Missile spells already fly at once with the pointer locked (BeginSpellMissile).
    Points 182.1 to 182.8 CONFIRMED per user the same day. The one thing beside them: a key used from the bags with
    the free pointer kept waiting after the right button (which locks the pointer). A first patch let the game's wait
    go with the bags' use mode (8a3e5a1d); per user ("Sollte es nicht genauso laufen?") the cause went instead, built
    and confirmed as 182.9 to 182.16 the same day: step 3 had not entered the first game's bag use mode on UW2, the thing waited on
    `UWAttackUw2` (BeginUseOn) and `UseOn`/`UseOnWorld` stood idle. NOW `UWAttackUw2.AskUseOn` (UseThing) hands the
    thing and its actor to `UWModernBags.BeginUse` in the modern scheme; the bags hold it on the pointer, say the
    question (`SayUsePrompt`), take the click and end it as on the first game (right button, Escape, locked pointer,
    bags shut); `UseOn` and `UseOnWorld` hand the click back (`ApplyHandedOn` without reach, `ApplyHandedAt` along
    the free pointer's ray with the reach and the pole's). `UseFrom` says where the use comes from: carried (the
    bags), world (the anvil and the pole in the world, and the bar's tools for a carried target - the bags open and
    the pointer frees, as the first game's anvil), crosshair (the bar's world tools). The patch's interface members
    are gone again. The classic scheme waits on the attack host as before.
    THE WORLD'S INPUT AS THE FIRST GAME'S, BUILT AND CONFIRMED 2026-11-23 before U4c per user (test list section 183, all thirteen points), from
    the survey of the parts built so far under the rule above: `UWWorldViewUw2.fUpdateModernActions` is the second
    game's `UWModernWorldInput.UpdateActions` - E through the first game's `UWHoldTap` (tapped on the release, held
    the direct use, the ring through `IUWModernWorld.UseHoldProgress`, over a window the slot), Q as before, the
    free pointer's click on a thing opening `UWModernBags.OpenWorldMenu` and its drag taking onto the pointer
    (`BeginWorldDrag`); the host's `UseThing`, `LookAt` and `TakeToPointer` answer the menu with this game's
    rules (`UseFromMenu` with the use target measured at the click, `fLookAt`, `fTake`), `TargetPadVerb` with
    `CrosshairPadVerb`, `IsMouseInWorld` as the first game's, which `UWAttackUw2.fPressed` now asks (a click on a
    window does not strike). The crosshair's name hides a closed secret door and a wall panel as the first game's
    (`UWModernWorldInput.HidesFromCrosshair`, `IsWallPanel`, now public). `LineTimes` gives each line its own
    time (the host follows the screen's list). STILL EMPTY IN THE HOST AND WHOSE: the magic members (U4c), the
    active spells' text, look and cancel, the question box, the compass, the map, the game menu and the saves
    (U4d), the lock icon and `IsSpellAiming` (U4e), the mantra (UW2 trains with teachers, the help's Teachers tab), Track (not built
    on UW2).
    THE DROP AND THROW AT THE FREE POINTER, found by the user at 183 (every drop a throw from the centre), built the
    same day and CONFIRMED (test list section 184, all seven points): `DropOrThrowPointerItem`'s point now reaches the throw
    (`UWGameScreenUw2.ModernDrop` -> `UWWorldViewUw2.DropHeld(inventory, point)`), as the first game's host hands its
    point to `UWPlayerThrow`. The throw had read the system mouse in the modern scheme, which is captured there and
    stands in the screen's middle; the classic view and `punt_mouse_obj` take `UWPointer.Position`.
    MOVED HERE FROM U4d AND U5: the character panel's sheet, the paperdoll's places per game. U5 keeps the
    pages of the other presets (`UWModernInventoryPage`, `UWModernStatsPage`), which can read
    `UWPaperdollGeometry` too.
  - U4c the rune panel and the hollow (the record's runes, the screen's shelf and cast).
    SURVEYED 2026-11-23. The first game's rune panel (`UWModernRunePanel`) asks the game only through
    `IUWModernMagic`, and the action bar's spells too (`TryBindSpell`, `CastCheck`, `CastSequence`), so with the
    host's magic half filled both run on UW2 as they are. The rules are there already: the spell table is the
    running game's (`UWRunicMagic.AllSpells`), the names (block 6) and the cast (`UWSpellCasting.CastRunes`
    through `UWSpellHostUw2`) work for UW2, the screen has the bag (`UWPlayerRecordUw2.HasRune`), the shelf
    (`miShelf`, the record's three places) and its rules (`fClickRunes`, `fCastShelf`, `PutOnShelf`). WHAT DIFFERS:
    (1) the tablet. Both games' rune page is `PANELS.GR` 1 and mous_in_rune is the same arithmetic (cells of 18
    by 15, 20 - 4 * row + column, the clear strip below row 0x12), but the pictures differ and so do the runes'
    places on them and the mouse region (the first game counts its columns from x 3, the second from 0) -
    `UWRuneShelfModel`'s page functions read the first game's `UWClassicLayout.RuneShelf`. They need the page's
    measures per game, as `UWPaperdollGeometry` gave the paperdoll's. (2) The spell's picture in the panel's list
    and on the bar: `UWModernRunePanel.SpellIcon` works it out with the first game's tables
    (`UWRunicMagic.GetIconIndex`, the class 11 effects, the missiles); the second game's SPELLS.GR goes by
    parse_aspells' table (`UWClassicLayoutUw2.Runes.SpellClassFirst`). That belongs to the game: a member of
    `IUWModernMagic`. (3) FOUND IN PASSING, both schemes: UW2's mous_in_rune and try_cast return at once while
    GameInputMode is not 0 (UnderworldExhumed uw2 RUNES.C:99, :172), as the first game's
    (`UWHudRunes.InAnotherInputMode`); ours on UW2 clicks a rune and casts in any mode. The host's
    `IsHeldByAnotherInput` is that test, and the screen's rune routines ask it too. KEPT: the classic frame's
    hollow (`UWModernRuneHollow`, Classic+) is stage U6's; the active spells' text, look and cancel stay U4d's
    with their row; the cast key (F8) stays the screen's, as the first game's is its UI keys' (UWGameUiKeys).
    STEPS (proposed): (1) the tablet's measures per game in UWData, the second game's tablet with all runes
    written to PNG and checked, the panel's room for it; (2) the host's magic half, the panel on UW2
    (`Has(RunePanel)`), the rune bag and the rune key open it, spells on the bar, the screen's modern rune page
    goes (`mbRunesOpen`, the rune page's and hollow's clicks in `fClickBags`, its block of `fModernKeys`, the
    stopgap's page and hollow in `UWModernHudUw2`), the GameInputMode test.
    DECIDED per user 2026-11-23 ("Ja, Schritt 1"): the two steps as proposed.
    STEP 1 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 185, both points, UW1 only - nothing changes on UW2 yet):
    `UWRuneTabletGeometry` (UWData, new) holds the rune page's measures per game - the page's size, rune 0's
    picture and the pitch, the panel's mouse region, where the columns begin in it, the clear strip - with
    `SlotLeft`, `SlotTop`, `RuneCellAt` and `IsClearStrip`, which were `UWRuneShelfModel`'s with the first game's
    numbers. The first game's classic frame (`UWHudPanel`, `UWHudRunes`) reads `Uw1`, the modern rune panel the
    running game's (`Of`), and lays the narrower second game's page (79 x 112 against 83 x 114) in its middle.
    The second game's numbers come from ShowRune and BeginInventory's region (`UWClassicLayoutUw2.Runes`,
    `Statistics.AreaTopY` new); the self-check compares every pixel of its region with mous_in_rune as the
    classic page computes it. The page with all 24 runes and the cells over it was written to PNG with a scratch
    tool and looked at: every rune sits in its compartment, the cells and the clear strip lie on the picture.
    STEP 2 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 186, all twelve points; PART U4c COMPLETE): the
    first game's rune panel runs on UW2 (`Has(RunePanel)`), and with it the action bar's spells. The host's magic half
    answers from the record's bag, the screen's shelf and its rules (`ModernAddRune`, `ModernLookAtRune`,
    `ModernCastShelf`, `ModernCastSequence` split out of the classic page's `fClickRunes` and `fCastShelf`), the level
    and mana of `UWVitalsUw2`. NEW ON THE INTERFACE: `IUWModernMagic.SpellPicture` - the first game's rule moved from
    `UWModernRunePanel.SpellIcon` into its host; the second game's by parse_aspells' table and its own missiles, checked
    with the scratch tool against the active spells' names (block 6 from 0x180): all 22 lasting spells match their
    picture (two by a short name, Bounce and Leap). THE INPUT MODE: `UWGameScreenUw2.IsInAnotherInputMode` (a thing on
    the pointer, a target or missile spell or a thing used on another waiting, the roaming view, the modern bag use
    mode) holds the rune clicks and the cast in both schemes, as mous_in_rune and try_cast. GONE: the screen's modal
    rune page (`mbRunesOpen`, `IsPanelOpen` and its callers, `fTakeOverRunePanel`, the rune key's and Escape's steps in
    `fModernKeys`, the rune page's and hollow's clicks in `fClickBags`), the stopgap's page and hollow
    (`UWModernHudUw2`). The rune bag opens the panel (`UWModernBags.OpenRunes`). STILL EMPTY IN THE HOST AND WHOSE:
    the active spells' text, look and cancel (U4d, with their row), the question box, the compass, the map, the game
    menu and the saves (U4d), the lock icon and `IsSpellAiming` (U4e), the mantra (UW2 trains with teachers), Track
    (not built on UW2).
  - U4d the minimap, the heading strip and compass, the question box, the game menu (UW2 saves through
    its options panel's rules), the character panel's sheet.
    SURVEYED 2026-11-23 (per user "Ja, mit U4d anfangen"). In the preset Modern the part's windows are the active
    spells' row, the heading strip, the minimap, the question box and the game menu; the compass disc and the view's
    picture show only in the classic kinds and Custom (`UWModernLayout.IsCompassShown`), so they stay U6's. The
    character panel's sheet came forward into U4b. What each window asks and what UW2 has:
    (1) THE ACTIVE SPELLS' ROW (`UWModernActiveSpells`): pictures and count are answered since U4a; the text, the
    look and the cancel are not - the screen has them as try_clear (`fClickActiveSpell`: the name of block 6 from
    0x180 and the stability's words, the dispel). Its clicks are `UWModernHudInput`'s, which is off on UW2
    (`Has(HudInput)`); the screen's own spell clicks (`fClickModernSpellIcon`, `fClickBags`) and the stopgap's row
    go with it. HudInput also carries the heading's click (the status) - FOUND: `IUWModernCharacter.ReportStatus`
    is empty on UW2 and named nowhere; the screen has print_info (`UWStatusReportUw2.Lines`).
    (2) THE HEADING STRIP (`UWModernHeadingStrip`): `CompassStep` (empty) and the strip's art, which
    `UWModernHudArt.BuildHeadingStrip` cuts from the first game's frame (noted at U4b step 1) - it needs UW2's cut,
    looked at as PNG first. The strip opens on a hit with the gargoyle's eyes and the foe's name and state from the
    first game's `UWEyesDisplay`; UW2 has the eyes too (uw2 PANELS.C adjust_eyes, `UWClassicLayoutUw2`'s eyes,
    drawn by the classic screen), so the host hands them over instead of the first game's component.
    (3) THE MINIMAP (`UWModernMinimap`): `CarriesMap`, `OpenMap` and `Automap` (empty). `UWAutomapPictures` is the
    first game's class on `UWGameUI` (parchment, tiles apart, notes, the player's pixel); the second game paints the
    whole map screen at once (`UWAutomapPainter` into BLNKMAP's indices, the stopgap's `fPaintMap`, the map in its own
    palette). The first game's mechanism cannot take UW2's map as it is, so the minimap's pictures become a per-game
    answer (an interface or a base class with the two games' sides) - the window stays the first game's.
    (4) THE QUESTION BOX (`UWModernQuestion`): `YesNoQuestion` and `AnswerYesNo` (empty); the screen asks on the
    scroll's last line through the first game's `UWYesNoPrompt` (wyorn: the trap found, the anvil's repair), which has the
    question and an answer call, so the box takes it over.
    (5) THE GAME MENU (`UWModernGameMenu`): `RefusesMenu`, `RefusesToSave` (both refuse today), the saves and the
    quit (empty). UW2 has every piece in its options panel: the slots (`UWSavegameSlots`), the save
    (`UWWorldViewUw2.Save`), the restore (`UWGameStartUw2.Request`), the quit (`UWGameStartUw2.Leave`), the refusal
    (do_option_shortcut: not while GameInputMode is set). Escape, F1 and the Ctrl shortcuts open the first game's
    menu then (on UW1 `UWGameUI` routes them); the options panel over the view stays the classic scheme's. The
    menu's layout entry waits for U4e (`Has(LayoutEditor)`).
    STEPS (proposed): (1) the heading strip's art per game, written to PNG and looked at; (2) the active spells' row
    and the heading strip on UW2 with HudInput (the row's text, look and cancel, the status, the compass step, the
    eyes), the stopgap's row and the screen's spell clicks go; (3) the minimap, its pictures per game, the stopgap's
    minimap goes; (4) the question box and the game menu, the modern scheme's options panel goes.
    DECIDED per user 2026-11-23 ("Ja, Schritt 1"): the four steps as proposed.
    STEP 1 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 187, both points, UW1 only - the strip shows on UW2 with step 2): the heading
    strip per game. `UWLeatherBuilder.BuildHeadingStrip` and `BuildHeadingStripWithBrandAt` take the game's panel
    measures (`FrameOf`; until then the strip composed with the first game's whatever the data), its gargoyle's head
    and that head's mask (`UWHudMasks.GargoyleHeadOf`). The second game's head lies at x 108 on BYT.ARK block 4, the
    first game's 24 x 18 (`UWClassicLayoutUw2.GargoyleHead`), its eyes' sprite two columns in as the first game's
    (self-check); its mask (`UWHudMasks.GargoyleHeadUw2`) is set by hand from a luma map - the outline has gaps a
    flood leaks through - and keeps the frame's moss over the left brow (per user the same day, from the strip's
    PNG: cut out, the brow showed a notch). FOUND ON THE WAY: `UWMainFrame.Picture`
    of the second game went partly clear - the first game's palette rule takes the near black (0, 0, 4) for its
    marker, as at the armour in U4b step 2; the frame is a whole screen, so on UW2 every pixel is opaque now. The
    strips of both games at 44 and 120 pixels were written to PNG and looked at. FOR STEP 2: the second game's eyes
    picture's top is the head's row 4, the first game's EYES.GR row 3 (`EyesTopInBrand`), so the eyes' place on the
    strip needs the game's.
    STEP 2 BUILT 2026-11-23 (test list section 188 open; self-check green, build done, not tried in the game): the first
    game's active spells' row and heading strip run on UW2 (`Has(ActiveSpells)`, `Has(Heading)`), with the HUD's clicks
    (`Has(HudInput)`: the spells' look and cancel, the heading's status). NEW ON THE INTERFACE: `IUWModernWorld.FoeEyes`
    (struct `UWModernFoeEyes`: lit, picture, alpha, the foe's name and condition, the head's row of the picture's top);
    the strip read the first game's `UWEyesDisplay` itself until then, the first game's host now hands it over. The
    second game's host: the eyes from the screen (`ModernFoeEyes`: the blink's picture while it runs, then the
    condition's widest frame; lit and fading for `UWSettings.EyesSeconds` as the first game's - THE SAME MECHANISM for
    the strip, the pictures the game's), the name now handed to `ShowEyes`; `CompassStep` (`CompassStepNow`, set_compass's
    reading); the row's text, look and cancel (`ActiveSpellLine`, `ModernDescribeActiveSpell`, `ModernCancelActiveSpell`
    out of try_clear); `ReportStatus` (print_info, `ModernReportStatus`). FOUND ON THE WAY: try_clear returns in
    GameInputMode 1 to 3 (uw2 RUNES.C:142), ours clicked through - `fClickActiveSpell` asks now, in both schemes. GONE: the
    screen's own spell clicks (`fClickModernSpellIcon`, `fClickBags`), `IsOverOwnModernWindow` (no window of the screen's
    own is left; `UWModernUi.IsOverUi` lost its second-game line), the stopgap's row. INTERIM until step 3: the row stands
    at the top right over the stopgap's minimap (it makes room only for the first game's).
    TEST 188 per user the same day: 188.1 to 188.3, 188.9 and 188.10 CONFIRMED; 188.4 to 188.8 (the row's points) could
    not be seen under the stopgap's minimap, as the interim said - tested again with step 3 (189.8).
    STEP 3 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 189, all nine points, and 188.4 to 188.8 with it): the first
    game's minimap runs on UW2 (`Has(Minimap)`). NEW: `IUWMinimapPictures` (IUWModernWorld.Automap's type; a second way
    for a reason, written at the interface: the first game's map is the parchment, the tiles apart on a clear ground and
    the notes, the second game paints the whole screen at once in its own palette). `UWAutomapPictures` answers it for
    the first game (its members public, `TilesRect` new); `UWAutomapPicturesUw2` (in UWModernSessionUw2.cs) for the
    second: the whole map screen of `UWHelpMapUw2` (`UWMapScreenUw2.ComposeScreen`, now with `pbPlayerMark` - the
    minimap draws its own arrow), no parchment, the notes in the map's ink, the player's pixel at every ask
    (`UWMapScreenUw2.PlayerPixelNow`, the arrow's place out of ComposeScreen). The host: `CarriesMap` (the map carried
    and the automap on, the stopgap's rule, `ModernCarriesMap`), `OpenMap` (`ModernOpenMap`, the carried map's use
    without its line). The minimap lays the tiles by `TilesRect`. GONE: the stopgap's minimap.
    STEP 4 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 190, all twelve points; PART U4d COMPLETE; the own
    layout greyed on UW2 is as meant, its editor is U4e's): the first
    game's question box and game menu run on UW2 (`Has(Question)`, `Has(GameMenu)`). THE QUESTION: the screen asks through
    the first game's `UWYesNoPrompt` already; the host hands its question and the box's answer over
    (`ModernYesNoQuestion`, `ModernAnswerYesNo`). THE MENU: `UWGameUI`, which routes the first game's menu keys
    (UWHotkeys groups 2 and 3), is never initialised on UW2, so the screen routes them the same way (`fModernMenuKeys`:
    with the menu open the back press and F1 close it or step back, closed F1 and the shortcut modifier's S, R, M, F, D,
    Q, O open its pages; Escape's chain ends in it) - the options panel over the view stays the classic scheme's. The host's
    saves are the options panel's pieces (`ModernSaveSlotName`, `ModernHasSave`, `ModernWriteSave` - " " for an empty
    name -, `ModernReportSave` and `ModernReportNoSave` as DoSaveRest's outcome lines, `ModernLoadSave` through
    `UWGameStartUw2.Request`, `ModernQuit` through `UWGameStartUw2.Leave`), the refusals the original's
    (`ModernRefusesMenu`: do_option_shortcut with a thing on the pointer or the roaming view; `ModernRefusesToSave`:
    check_save, uw2 GAMESCR.C:178, while GameInputMode is set - string 0xAE). The menu greys a preset the running game
    has not built (`IUWModernSession.Supports`, and the own layout also on `Has(LayoutEditor)`; until then every preset
    was offered on UW2 too). STILL EMPTY IN THE HOST AND WHOSE after part U4d: the lock icon and `IsSpellAiming` (U4e),
    the mantra (UW2 trains with teachers), Track (not built on UW2), the compass's picture and the view's picture
    (`Compass`, `ViewPicture`: the classic kinds' pieces, stages U6 and U7).
  - U4e `UWModernHudUw2` goes; the layout editor in UW2; the automatic key (86.2 and 86.3 confirmed 2026-11-23 with part A's message box), the windows
    with the free pointer.
    THE INPUT'S GOAL (decided per user 2026-11-23): today `UWGameScreenUw2.fModernKeys` decides the whole
    modern input of UW2 in one routine - B and Z for the bags and the runes, H, Escape's order, the right
    button's pointer switch, the clicks on the pack's cells, the rune page, the hollow and the active
    spells, the action bar's keys. In UW1 each window brings its own input. So with each window UW2 takes
    over (U4a the active spells, U4b the bags and the bar, U4c the runes, U4d the game menu) the matching
    block of `fModernKeys` goes, and exactly one side owns each key and click at every step
    (`IUWModernSession.Has`). The pointer switch and H end in the HUD, as on UW1. At the end of U4e
    `fModernKeys` holds only the world's input - drawing the weapon, striking, using and looking in the
    world, the cast key - which is UW2's counterpart of `UWModernWorldInput` and stays on the game's side.
    UW2 then behaves in the modern scheme as UW1 does: the same windows, the same order of closing on
    Escape, the same context menu, dragging and dropping as in UW1.
    SURVEYED 2026-11-23 (per user "Ja, U4e starten"). WHAT IS LEFT:
    (1) THE INPUT. `fModernKeys` still holds H (the HUD's own toggle skips UW2, `!UWGameStartUw2.IsRunning`, and the
    screen's LateUpdate releases the hide by its own rules), the right button's switch (`UWModernPointer.Tick`, the HUD's
    `Has(HudPointer)` off), Escape's first steps (a waiting missile or target spell lets go - the first game's chain does
    that through `IsSpellAiming`/`CancelSpellAim`, which the second game's host leaves empty while `UWModernUi.IsSpellWaiting`
    and the chain's spell row still name `UWAttackUw2` in the root), and the world's own: the cast key, R, the inventory
    press. The pad's hints (`UWPadHintStrip`, `Has(PadHints)` off) read the first game's `UWGameUI`.
    (2) THE AUTOMATIC KEY (a modern aid, UWUserSettings.AutoKey, the first game's `UWWorldUse.KeyAtHand` and
    `UWModernWorldInput.GetCrosshairLock`): E on a closed locked door uses the carried key that fits and the crosshair
    shows the key or the crossed-out key. The second game has none (its key waits on the use key, `ApplyWaitingUseOn`);
    `TryGetTargetLock` is empty. By the rule for conveniences it goes through the second game's own lock rule
    (`UWLockUseUw2`).
    (3) THE LAYOUT EDITOR AND THE OWN LAYOUT PER GAME (decided 2026-11-23): one `UWUserSettings.ModernLayout` and the own
    layout's switches (minimap and bar hidden, compass, scroll, stone shelf, hollow, tablet, stats panel, character
    page ...) serve both games; the editor offers every element. On UW2 the classic kinds' pieces are not built (U6), so
    the editor offers only what the game has (`Has`), and the layout and its switches are kept per game.
    `Supports(Custom)` and `Has(LayoutEditor)` turn on with it.
    (4) THE STOPGAP (`UWModernHudUw2`): every piece of it stands aside behind `Has` (flasks, gem, lines, pointer and
    name, cells, weapon); the screen still ticks it and hands it the weapon's picture. It goes, and with it the screen's
    `fTickModernHud`, `ShowWeapon`'s call and `SetShown`. The free pointer's windows (`UWUserSettings.FreeOpens*`,
    `UWModernPointer`) run through the shared code already; checked in the test.
    STEPS (proposed): (1) the input into the HUD - H, the pointer's switch, the spell aim through the host (the root's
    `UWAttackUw2` lines go), the pad's hints; `fModernKeys` keeps the world's input; (2) the automatic key and the lock
    icon on UW2; (3) the layout editor and the own layout per game; (4) the stopgap goes, the host's empty members checked
    a last time.
    DECIDED per user 2026-11-23 ("Ja, Schritt 1"): the four steps, and the preset's choice kept per game as well as the own
    layout (proposed with the cut: each game keeps its interface, the second game never falls back from a preset it lacks).
    STEP 1 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 191, all seven points): the input
    into the HUD. H's toggle and release are the HUD's for both games (`UWModernHud.Update`; the second game's screen
    toggled and released it itself), the right button's switch too (`Has(HudPointer)` on UW2). The host answers
    `IsSpellAiming` and `CancelSpellAim` from the attack host (missile, target spell, a thing used on another), so
    `UWModernUi.IsSpellWaiting` and the chain's spell link name no second-game class any more; Escape on UW2 is the chain
    and then the menu, as the first game's UI keys route it. The pad's hints run on UW2 (`Has(PadHints)`; they ask the
    root's `IsConversationOpen` and `IsMapOpen` instead of the first game's conversation controller and map). AS THE FIRST
    GAME'S NOW: H does nothing while the pointer is free. `fModernKeys` keeps the roaming view's branch, Escape's routing,
    the cast key, R, the inventory press, the screen's pointer picture and the stopgap's tick (step 4).
    TEST 191 CONFIRMED per user the same day (all seven points).
    STEP 2 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 192, all seven points): the automatic
    key and the lock icon on UW2, as the first game's (doors only, the modern scheme and UWUserSettings.AutoKey through
    `UWWorldUse.KeyUsedAutomatically`, one switch for both). `UWLockUseUw2.KeyAtHand` finds the key by the first game's
    search (`UWObjectMechanics.FindMatchingKey`: never the lockpick, no key for a lock 0), `UnlockDoorWithKey` goes through
    the lock rule as UseKeyOn (`UWLockRules.ToggleWithKey`, the unlock triggers), the door's use (`UWWorldViewUw2.fUseAt`)
    names the key (the first game's "msg.door.unlocked_with") and opens it. `CrosshairLock` answers the host's
    `TryGetTargetLock`. FOUND ON THE WAY: the crosshair's key picture came through `UWPicture.From`, whose first-game rule
    leaves the second game's colour 0 opaque and its (0, 0, 4) outline clear - a key in a black box; `UWPicture.FromOwnClear`
    (colour 0 clear, all else opaque for a ZeroIsClear picture) - checked as PNG for three keys.
    TEST 192 CONFIRMED per user the same day (all seven points).
    STEP 3 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 193, all seven points; self-check green with the settings fixtures recorded anew, build
    done, not tried in the game): the layout editor on UW2 and the layout per game. THE FIRST PER-GAME SETTING: the table's
    Layout entries (places and sizes, every part's switches and looks, the UI size, the conversation's text size) and the
    preset belong to the running game; `UWUserData.LayoutGame` says whose they are, `OtherGameLayout` keeps the other game's
    as JSON (new entries `LayoutGame`, `OtherGameLayout`, fixture `layout-per-game-2026-11-23`), and
    `UWUserSettings.UseLayoutOf` swaps the two when the other game's session takes the windows (`UWModernUi.Session`'s
    setter, by `UWGameSession.Current`'s game; not while the editor holds the file). A game without one starts from the
    defaults (Modern). On UW2 `Supports(Custom)` and `Has(LayoutEditor)`; the editor offers only what the game has
    (`UWModernLayoutEditor.Inspector`: the compass, the stone shelf, the hollow, the stats and character pages by `Has`, the
    tablet alone, the scroll and the stands by `Supports(ClassicPlus)`).
    Per user at the test: the classic kinds' pieces the editor leaves out on UW2 (193.3) come with Classic Modular's cuts
    (U6) - the editor asks `Has`/`Supports` already and offers them then by itself.
    STEP 4 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 194, all four points; PART U4e AND STAGE U4 COMPLETE): the stopgap
    HUD `UWModernHudUw2` is deleted (every piece already stood aside behind `Has`), and with it the screen's
    `fTickModernHud`, the weapon's hand-over to it and its `SetShown`; `fEnsureModernHud` makes the host and the
    conversation's host alone; the seam scan's exception for it went. THE HOST'S EMPTY MEMBERS, checked a last time, each
    name their stage or reason in the code: the mantra (no shrines, UW2 trains with teachers), Track (not built on UW2),
    the compass's picture (U6), the view's picture (U7), the dragons (the second game's frame has none, U7), `TextUi` (the
    first game's interface does not run here, the texts are made without it), `Describe`/`TryRead` (this game's look
    speaks and reads itself). `fModernKeys` holds the world's input, Escape's routing and the menu's keys - the goal of
    the input (above) reached. STAGE U4 COMPLETE 2026-11-23 ("194 passt alles"): the modern scheme of UW2 runs on the first
    game's windows. NEXT: stage U5.
- **U5 The character and stats pages on UW2's art.** The page from `PANELS.GR` (79 x 112), the paperdoll's
  places per game (`UWPaperdollLayout` beside a UW2 table), the stats rows, the rune tablet.
  SURVEYED 2026-11-23 (per user "Ja, U5 starten"). The three pieces are the classic kinds' (Classic+, Custom), each an
  element of its own; on UW2 they stay off (`Has(StatsPage)`, `Has(CharacterPage)`; the tablet alone by
  `Supports(ClassicPlus)` in the editor). WHAT EACH ASKS AND WHAT DIFFERS:
  (1) THE STATS PAGE (`UWModernStatsPage`): `PANELS.GR` 2 through `UWGrTextures` (per game already) and its texts at the
  first game's rows and columns (constants in the class: the class's row, the six values' rows, the skills' six rows,
  the labels' and values' columns, the paging strip of eight rows split at x 37) from `UWCharacterSheet`, which the
  second game's host binds from the record turned into the first game's player data (`UWPlayerData.FromSecondGame`). The
  second game's page (STATS.C, `UWClassicLayoutUw2.Statistics`, what the classic screen draws) has its own places (the
  name over 0x48 columns from 0xF0, three attributes, vitality and mana as "now/most", the experience in tenths, six
  skill rows of which the first is the skill points while the list stands at its top, a paging strip of ten rows
  split at 0x26). So the page's measures go per game, and the sheet's rows as the second game writes them.
  (2) THE CHARACTER PAGE (`UWModernInventoryPage`): `PANELS.GR` 0 and the figure at the first game's places
  (`UWPaperdollLayout`, `UWClassicLayout.Backpack`'s slots and weight, the open container's back INV.GR 6 and the
  arrows BUTTONS 27/28). `UWPaperdollGeometry` (U4b step 2) has the second game's page, figure, armour and the six
  places with a thing's picture already; it lacks the backpack's eight places, the weight's place and colour, the open
  bag's back and its arrows (`UWClassicLayoutUw2.Inventory`: displays 12 to 19, `WeightCentreX`/`WeightTop`,
  `OpenBagBack*`, `UpArrowPicture`/`DownArrowPicture`). The page then reads the geometry instead of the first game's
  tables.
  (3) THE RUNE TABLET ALONE (`UWModernRunePanel` in Classic+'s tablet mode): its measures are per game since U4c step 1;
  only the editor's gate holds it back on UW2.
  STEPS (proposed): (1) the stats page per game - its measures, the sheet's rows as the second game writes them, the
  paging; (2) the character page per game - `UWPaperdollGeometry` grows the backpack, the weight, the open bag and the
  arrows, the page reads it, checked as PNG; (3) the three pieces on UW2 (`Has(StatsPage)`, `Has(CharacterPage)`, the
  tablet's gate), offered by the editor in the own layout - Classic+ itself stays U6's. STEPS APPROVED per user the same
  day ("Ja, Schritt 1").
  STEP 1 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 195, all six points; the second game's page cannot be
  opened before step 3): THE STATS PAGE PER GAME. `UWStatsPageGeometry` (UWData, `Uw1`/`Uw2`/`Of`)
  holds the lines' places (name centred on a column, class and level, the six values seven rows apart, six skill rows),
  the colours (the first game's fixed black and blue-grey, the second game's palette 0 indices 0xC4 and 0xC9), the paging
  strip (`PagingStepAt`) and whether the page is opaque (the second game's, as its frame: the near black is no clear
  colour, so `UWGrTextures` mode Colours). The first game's numbers are `UWClassicLayout.Stats`, the ones the modern page
  had as constants; the second game's come from RedispStat and the panel's mouse region: name top 3 centred on 41,
  class and level row 10, labels from 5, values to 74, values from row 17, skills from row 60, the strip rows 102 to 111
  from x 5 split at 43 (the region reaches two columns past the 79-pixel page). `UWCharacterSheet.Bind` takes the game:
  the second game's original page has 21 entries, the skill points first (`PointsEntry`, the word 0x1F + 100 "Skill Pt"),
  the skill words upper case, the offset 0 to 15; the readable spelling keeps the first game's list on both. The second
  game's host binds with its game; `UWModernStatsPage` reads the geometry instead of its constants. Self-check: the
  geometry's numbers, every row against RedispStat's y, every pixel of the page paging as mous_in_stat, the second
  game's list and its words from its STRINGS.PAK. Rendered as PNG with a second-game save: the values stand beside
  the picture's labels, the strip over the two arrows.
  STEP 2 BUILT AND USER-CONFIRMED 2026-11-23 (test list section 196, all five points; self-check green; the second
  game's page cannot be opened before step 3): THE CHARACTER PAGE PER GAME. `UWPaperdollGeometry` grows the whole page:
  its place on the screen (`PanelLeft`/`PanelTop`), the backpack's boxes and middles, the weight's middle and colour
  (the second game's 305 under row 56 in FONT5X6P's six rows, palette 0x88), the open bag's back (INV.GR 6 at 0xEE/77),
  its icon (display 20, ending above row 77) and the arrows (displays 21 and 22, `ArrowLeft`; the first game's by their
  right edges at 314, 3 apart), and `SpotAt`: the first game's table, the second game's FindInventoryHit over displays 1
  to 22 named by the first game's spots (the arrows' top row is the right ring's, display 11 found first, as there).
  `UWModernInventoryPage` reads all of it instead of its constants and `UWPaperdollLayout`, draws the armour in the
  game's order (`ArmourOrder`), and takes the second game's page and bag back opaque and its arrows clear in their own
  colour 0 (`UWGrTextures.ModeEnum.ZeroClear`, `UWPicture.FromZeroClear`, as `fCachedPicture` draws BUTTONS.GR).
  Self-check: the first game's places as the page had them, every point of the screen against both tables, the
  shoulders', hands' and rings' spots against `SlotAt`. Rendered as PNG for both games, open and with the spots tinted.
  STEP 3 BUILT 2026-11-23 (test list section 197 open; self-check green, build done, not tried in the game): THE THREE
  PIECES ON UW2. The second game's host has `StatsPage` and `CharacterPage`; the layout editor offers the tablet alone
  by the rune panel (`Has(RunePanel)`: it is that panel's own mode, its page per game since U4c) instead of by
  `Supports(ClassicPlus)`, which keeps only the scroll and the stands. So the own layout on UW2 offers the stats page,
  the character page and the tablet on the second game's art; Classic+ itself stays U6's (`Supports(ClassicPlus)` false).
  FOUND AT THE TEST (per user, 2026-11-23: the tablet's clear button with a frame that showed through; no figure and the
  weight 0 on the character page; the inventory and saving the layout worked), FIXED THE SAME DAY: the second game's
  pages, the bag's back and the tablet through `UWGrTextures.ModeEnum.Opaque` (`UWPicture.FromOpaque`; Colours kept
  the first game's palette rule, under which the second game's near black (0, 0, 4) is clear); the second game's
  host answers `FigureTexture` (BODIES.GR and the armour, clear in their own colour 0 as `fCachedPicture` draws them;
  it gave null); `IUWModernCharacter.MaxLoad` gives the most the character carries (the second game's record's
  weight_max), which the page's weight (`UWInventoryView.Refresh`'s piMaxLoad) and the backpack window's load read -
  both read the first game's player data before. Test points 197.12 to 197.15.
  TEST 197 CONFIRMED per user the same day (all fifteen points; the hollow is not there in the modern scheme yet - it is
  U6's - and was checked in the classic scheme: the runes lie in it). STAGE U5 COMPLETE 2026-11-23.
- **U6 Classic Modular (`ClassicPlus`) from UW2's pieces.** Compass, flasks, gem, hollow, shelf and the
  panels as single elements, freed from `BYT.ARK` block 4.
  SURVEYED 2026-11-23 (per user "Ja, U6 starten"; the second game's side read by a subagent and spot-checked, the
  frame's bottom rendered as PNG). WHAT CLASSIC MODULAR IS ON UW1 (`UWModernLayout`, the preset's pieces): the compass
  freed, on its stone disc with the movement arrows (`UWCompassDiscBuilder`, MAIN.BYT and COMPASS.GR; `IsCompassDisc`),
  the rune hollow cut from the frame (`UWFrameCutBuilder.BuildRuneHollow`, the runes at `RuneHollowRuneX/Y/Pitch`), the
  stone shelf grown from the frame's ledge (`UWStoneShelfBuilder`), the message scroll with the frame's rollers and
  knobs (`UWScrollPaperBuilder`, MAIN.BYT rows 167-199 and SCRLEDGE.GR), the flasks and the gem apart on stands
  (`UWShelfBuilder`, the gem's hexagonal collar `UWHudMasks.GemStand`), the pages and the tablet (U5), the
  conversation in the original's look (`UWConversationArtBuilder`, CONV.BYT), no minimap, no action bar; the places
  are the user's own layout taken over as the preset (`UWLayoutPresets.ClassicModularLayout`, 2026-10-11). The
  generated backs (`UWBackdropArt`) take first-game wall textures (W64 126, 207, 201).
  WHAT THE SECOND GAME'S FRAME HAS (BYT.ARK block 4 through `UWMainFrame.Picture`, every pixel opaque): along the
  bottom of the view a MARBLE LEDGE with THREE RECESSES - at the left the active spells (`Runes.SpellX` 0x2F, 0x21,
  0x13, try_clear from 0x11 to 0x42), in the middle the COMPASS (COMPASS.GR 0-3 the faces by step & 3, 4-19 the needles;
  0x34 x 0x10 at 0x5E/0x33, print_info from 0x5B to 0x94) WITH THE POWER GEM IN ITS MIDDLE (POWER.GR at 0x71/0x30 - not
  beside the flasks as on UW1), at the right the three prepared runes (`Runes.ShelfX` 0xB3, 0xC1, 0xCF at 0x32,
  try_cast from 0xA9 to 0xDF); under the ledge the MESSAGE SCROLL with rollers above and below and knobs at both ends
  (paper 0x71, `MessageLog` 0x10 to 0xDF, the rolling edges SCRLEDGE.GR as on UW1); at the right the two flasks on a
  stone with golden feet (`UWFlasksUw2` keeps the stone strip as the flask's foot). NO MOVEMENT ARROWS and no stone
  disc; no measured crop of any of it yet (`UWMainFrame`'s cuts and `UWHudMasks` are MAIN.BYT's, but the gargoyle).
  The host's empty members for it: `Compass(step, freed)` null, `Has(Compass/RuneHollow/StoneShelf/Backs)` false,
  `Supports(ClassicPlus)` false, the vitals a free row (`BuildVitalsArt`, no shelf), the conversation host's
  `HasOriginalLook` false.
  PROPOSED SHAPE (the second game's pieces as its frame has them, the first game's mechanisms): the CENTRE RECESS is
  the compass's disc (no arrows: `ArrowAt` finds none on UW2), the compass freed from it as UW1's cross; THE GEM LIES IN
  THE COMPASS as on UW2's frame, and "gem apart" frees it onto its own small piece of the recess; the RIGHT RECESS is
  the hollow with the three runes at the frame's places (the hollow's rune places per game, as the tablet's); the
  LEDGE grown like UW1's stone shelf (its plain marble, the recesses left out); the SCROLL'S PAPER from UW2's rows with
  its rollers and knobs, grown as UW1's; the flasks as now (their stone is their stand); the LEFT RECESS as an optional
  back for the active spells; the backs with second-game walls; a Classic Modular layout of its own for UW2, drafted at
  the original's places, arranged by the user and taken over as UW2's preset (the preset's places per game). The
  CONVERSATION'S ORIGINAL LOOK on UW2 (its own conversation screen) stays out of U6 - with U7, whose frame it shares.
  STEPS (proposed): (1) the recesses: compass with gem and the hollow cut from UW2's frame (masks measured on the
  picture and checked by eye as PNG with the user, as the gargoyle), the host's `Compass` and the hollow's art and rune
  places per game, `Has(Compass)` and `Has(RuneHollow)`, the editor offers them; (2) the ledge and the scroll: UW2's
  stone shelf and scroll paper grown, `Has(StoneShelf)`, the scroll for UW2, the left recess as the active spells'
  back, the backs with UW2's walls (`Has(Backs)`); (3) Classic Modular on UW2: a draft layout at the original's places,
  the user arranges it, it becomes UW2's preset, `Supports(ClassicPlus)`, the editor's `fHasClassicPieces` gates
  reviewed. STEPS APPROVED per user the same day ("Ja, Schritt 1").
  STEP 1 BUILT 2026-11-23, USER-CONFIRMED 2026-11-24 (test list section 198, all thirteen points; the pieces
  checked as PNG): THE COMPASS WITH THE GEM AND THE HOLLOW ON UW2. `UWClassicLayoutUw2.CompassRecess` (84/145, 72 x 21,
  its top corners bevelled: `UWHudMasks.CompassRecessUw2`, each row from the outline's left pixel to its right, read
  from a luma map) and `RuneHollow` (170/148, 54 x 18, the runes at rune_x, pitch 14). `UWCompassDiscBuilder.Build`
  gives the second game's disc (`BuildUw2`: the recess cut, opaque, a pixel of room, the optional outline),
  `ComposeUw2Compass` the compass of a step over the box of its four faces and sixteen needles (93/148, 53 x 16; whole,
  or freed by the first game's cross and tip rules). `UWCompassGeometry` (new, UWData) says per game how the compass
  lies on its disc (`CrossOnDisc`: the first game's places, the second's 10/4), whether it has arrows (UW1) and the
  gem (UW2, `GemOnCross` 20/3) and whether the cross goes freed onto the disc (UW1) or whole (UW2: the face's stone
  covers the cross baked into the recess). `UWFrameCutBuilder` cuts the hollow per game (`RuneHollowOf`,
  `RuneHollowRunes`). The host answers `Compass(step, freed)` and has `Compass` and `RuneHollow`; `UWModernCompass`
  draws the live power gem in the compass while `UWModernLayout.IsGemInCompass` (the second game's compass shows, the
  gem not apart), and the vitals then leave it out of their row (`BuildVitalsArt` without the gem's room). Open for
  the user: the freed needle keeps only its red tip (the first game's rule) - 198.6.
  FOUND AT THE TEST (per user, the same day; the rest passed, the freed needle's red tip too): the hollow kept a light
  strip left of its black line - it now begins at the line (174), as wide on both sides; the compass's look said
  "Disc" on UW2 too, where it lies in a hollow - the editor's choice says "Hollow" there, as the help names it
  (`editor.compass.hollow`; the
  inspector's rows are built anew when another game runs). Test points 198.9 and 198.10.
  FOUND AT THE SECOND TEST (per user, 2026-11-24; all else passed): clicks behind the open layout editor took things
  in the world on UW2. The menu holds the world on the control scheme (`HoldUiModal`, `IsWorldInputBlocked`), which the
  first game's Interaction asks; the second game's world view (`fIsInputTaken`) and attack (`fBusy`) did not - both ask
  it now, and the menu falls back to the shared scheme when the HUD has none. Test points 198.11 to 198.13.
  STEP 2 SPLIT 2026-11-24 into four parts, each with its own test: 2a the ledge as the stone shelf, 2b the scroll's
  paper, 2c the left hollow for the active spells (a new look of theirs, a new setting), 2d the generated backs - their
  four colours are ramps read at fixed places of the first game's frame (MAIN.BYT 82/135, 104/136, 240/12, INV.GR 6)
  and their patterns its wall textures (W64 126, 207, 201), so the second game needs its own sources, chosen with the
  user by eye.
  STEP 2a BUILT AND USER-CONFIRMED 2026-11-24 (test list section 199, all four points; checked as
  PNG): THE STONE SHELF FROM THE MARBLE LEDGE. `UWClassicLayoutUw2.Ledge` (0/145, 242 x 21, the bevelled ends 12 and 15
  columns) and `SpellHollow` (16/148, 50 x 18, as the rune hollow). `UWStoneShelfBuilder.Build` gives the second game's
  shelf (`ComposeUw2`): its two ends as the ledge has them, the marble between grown pixel by pixel with
  `UWTextureSynth.GrowFrom` from the ledge's plain marble only (never the three hollows, by their places and the
  compass recess's mask) - the plain stretches between the hollows are 14 columns, too short for the first game's
  quilted pieces; the same shelf every time, a wider one grows on the right (but for the two columns grown against the
  right end). The host has `StoneShelf`.
  STEP 2b BUILT AND USER-CONFIRMED 2026-11-24 (test list section 200, all ten points; checked as
  PNG): THE SCROLL FROM UW2'S FRAME. `UWClassicLayoutUw2.ScrollPicture` (8/167, 224 x 33: knob to knob, the rods'
  rows). `UWScrollPaperBuilder.Build` gives the second game's scroll (`BuildUw2`/`ComposeUw2`): the frame's rows with
  the ground (0, 0, 4) and the pillars' bluish stone clear, the paper in palette 0x71 over the rows the classic screen
  fills (169 to 197, column 16 on, 208 wide), the edges SCRLEDGE.GR (left from 0, right from 5, their frames as the
  classic screen's) opaque at 12 and 224; taller by the first game's row rule, wider at column 120, narrower from
  column 32 (up to 160). `UWModernScroll` takes the game's paper (its width, its left 8, its own width 224 for the
  editor's number) and the game's log colours. FOUND ON THE WAY: the modern message box and scroll read the first
  game's colour codes on UW2 too - `UWMessageLogModel.ColourIndicesOf`/`ColourTable(palettes, game)` give the second
  game's (font_color 0x76, spec_col). The editor offers the scroll on both games (`fHasClassicPieces` keeps only the
  stands).
  FOUND AT THE TEST (per user, 2026-11-24; the rest passed): the scroll kept five lines however tall, and the layout
  editor's Kenney pointers did not show on UW2. The screen kept five lines (`KeptMessages`, the classic box's) - now
  `UWMessageLog.HistoryCount` as the first game's - and paged by the classic box always - now by the scroll or box
  shown in the modern scheme (`UWControlScheme.Ui.ShownLogLines`, decision 17). Every editor pointer goes through
  `UWModernLayoutEditor.PointerTile`, which only the first game's pointer asked; `UWPointerCursor.TryShowEditorPointer`
  is the one routine now, the second game's screen asks it too. Test points 200.7 to 200.10. TEST 200 CONFIRMED per
  user the same day (all ten points).
  FOUND AT THE TEST (per user, 2026-11-24): on UW2 one click went over more than one [MORE]. THE PAGING'S INPUT IS ONE
  ROUTINE NOW (per user: "a candidate for a common routine"): `UWPageInput.TryConfirm` - any key, mouse or pad button,
  one page a frame whoever asks, after a mouse press the buttons up before the next (the original's mouse_release) -
  asked by the first game's log (`UWMessageLog.UpdatePaging`), the second game's screen (`fUpdatePaging`) and its
  conversation's answer scroll (`fUpdateLookPaging`), each of which read the press itself until then;
  `ConsumedThisFrame` and the second game's waiting tell the pad's owners and the back chain on both games (only the
  first game's Interaction did). No path that paged on the release was found in the code; the routine rules out both
  a second page in the frame and one on the release. Test list section 201.
  FOUND AT THE TEST of 201 with a TEMPORARY one-line log per page step (who called, which press): a scroll of 4 lines
  with the messages at 3 - E printed 2 lines, the mouse's press the last 2, the release paged nothing. But the second
  game's `IsWaitingForPage` stays true until the buttons are up after the last page (the click must not reach the
  world), and the modern scroll and box read it for [MORE]: they kept [MORE] and a line less until the release, which
  looked like a second page; a key holds nothing, so all came at once - right, the text had two pages. Now
  `UWGameScreenUw2.IsMoreShown` (the pending lines) is what the host gives the windows, `IsWaitingForPage` stays the
  input's hold - as the first game keeps its wait apart from the press that paged. The log is out again. Test points
  201.6 and 201.7.
  FOUND THE SAME DAY (per user): a narrowed scroll did not count its extra wraps. Both games wrapped a long text for
  paging at the classic box's width (291, 208) and the modern scroll wrapped each line again at its own; a page grew
  past the scroll and its first lines scrolled away unread - on UW1 too since the scroll could be narrowed (issue 29).
  `IUWUiScheme.ShownLogWidth` (the classic scheme the box's, the modern scheme `UWModernScroll.ShownTextWidth` while the
  scroll shows) is what both games wrap to now (`UWHudMessageLog.WrapLogText`, `UWGameScreenUw2.SayPaged`). Test points
  201.8 to 201.10. TEST 201 CONFIRMED per user the same day (all ten points).
  STEP 2c BUILT 2026-11-24 (test list section 202 open; self-check green, settings fixtures recorded anew, build done,
  not tried in the game; checked as PNG): THE ACTIVE SPELLS IN THE LEFT HOLLOW. The piece is `SpellHollow.CutPlace`
  (16/145, 50 x 21: the hollow with the ledge's rim above it - the spells, 16 x 18 at 0x37, reach up into it on the
  frame), cut by `UWFrameCutBuilder.BuildSpellHollow` as the rune hollow; `SpellHollowIcon` gives each spell's place
  (spell_x, the first at the right). New layout setting `ModernSpellHollow` (look: row or hollow; in the presets, Classic
  Modular on), new part `UWModernPartEnum.SpellHollow` (the second game's host has it), `UWModernLayout.IsSpellHollowShown`;
  `UWModernActiveSpells` draws the hollow as the Spells element with the spells in it, the hollow's outline as the rune
  hollow's; the editor offers the look at the spells where the game has the part.
  FOUND AT THE TEST (per user, the same day; the rest passed): the first game's host answered yes to every part, the
  spells' hollow too - its editor offered the look and Classic Modular cut the first game's frame at the second game's
  places; it has every part but `SpellHollow` now. And the hollow at the screen's bottom showed the spell's name under
  the screen: `UWModernActiveSpells.fPlaceTip` puts it above the spells where there is no room below (row and hollow).
  Test points 202.7 to 202.9. TEST 202 CONFIRMED per user the same day (all nine points).
  STEP 2d BUILT 2026-11-24 (test list section 203 open; self-check green, build done, not tried in the game): THE
  GENERATED BACKS IN UW2'S COLOURS AND WALLS, chosen per user from two sheets (four colour sources with two candidates
  each beside the first game's, sixteen walls as patterns in one colour). `UWClassicLayoutUw2.BackSources`: leather the
  scroll's rods and knobs (8/167), marble the ledge between the spells' and the compass's hollows (70/150), stone the
  compass hollow's inside (86/150), bronze the compass's gold (94/148); patterns T64.TR 209 (rough rock), 62 (blots), 88
  (veins). `UWBackdropArt.fRampUw2` reads them with its own filters (warm, bluish, plain grey), `fPatternTexture` the
  game's walls; the host has `Backs`. Until now the second game read the first game's places on its own frame. TEST 203 CONFIRMED per user 2026-11-24 (all five points). STEP 2 COMPLETE.
  STEP 3 BUILT 2026-11-24 (test list section 204 open; self-check green, build done, not tried in the game): CLASSIC
  MODULAR ON UW2. Per user the same day: the arrangement about as on UW1, and THE GEM STAYS IN THE COMPASS, without a stand
  of its own. `Supports(ClassicPlus)` on the second game's host; its places are `UWLayoutPresets.ClassicModularLayoutUw2`,
  A DRAFT equal to the first game's until the user arranges the pieces in the own layout (from the editor's Classic
  Modular start) and they are taken over (`ClassicModularLayoutOf`, `Apply(start, data, game)`,
  `UWModernClassicPlus.Layout` per game). `UWModernLayout.IsGemOfTheCompass` (the game's compass has the gem): the gem
  lies in the compass while it shows, else between the flasks, and alone without a stand only with the flasks apart and
  no compass (`UWModernVitals.fUpdateGemAlone`); the editor offers no gem stand there, the flasks' stands it does. The
  shelf's width in Classic Modular leaves the game's page width (`UWModernClassicPlus.PageWidth`). THE USER'S
  ARRANGEMENT TAKEN OVER the same day (from settings.json, the second game's own layout laid out from the editor's
  Classic Modular start): `UWLayoutPresets.ClassicModularLayoutUw2` holds it, pinned in LayoutPresetChecks. Test point
  204.7. The takeover first missed the narrowed scroll (per user): Classic Modular's scroll width is the game's now
  (`ClassicModularScrollWidthOf`: 0 on UW1, -48 on UW2; `UWModernClassicPlus.ScrollWidth` in `UWModernScroll.fWide`),
  and the editor's Classic Modular start writes it (on UW1 back to 0, where it left the own width before). Test point
  204.8.
  PER USER the same day: the spells' hollow without the rim above it, cut as the rune hollow (`SpellHollow.Place`; the
  rim cut `CutPlace` is gone) - the spells reach 2 to 3 pixels above it, as into the rim on the frame. Test point 204.6.
  TEST 204 CONFIRMED per user 2026-11-24 (all eight points). STEP 3 COMPLETE. STAGE U6 COMPLETE 2026-11-24.
  AFTER IT, PER USER the same day: the own layout offered no way to free the second game's gem from the compass. Now
  `ModernGemApart` frees it there too, alone without a stand (`IsGemApart` on UW2: the own layout's switch, or the
  flasks apart without a compass; `IsGemInCompass` only while not apart); the editor offers it at the compass ("In
  compass / Apart") and at the freed gem ("Apart / In compass", the flasks left as they are); the Classic Modular
  start writes it off on UW2. TEST 205 CONFIRMED per user 2026-11-24 (all five points).
- **U7 Classic Wide (`Classic`) for UW2.** A frame of its own: `UWClassicWideBuilder`'s cuts are measured on
  UW1's `MAIN.BYT`; UW2 has no dragons, the commands as a 2 x 3 bar at the bottom right and a panel that
  slides. Measured on UW2's frame and checked by eye with the user (the notes at the end of phase 3 step 8).
  SURVEYED 2026-11-24 (per user "Ja, U7 starten"; both games' frames and conversation screens rendered as PNG).
  WHAT CLASSIC WIDE IS ON UW1 (`UWModernClassicFrame`, `UWClassicWideBuilder`): MAIN.BYT pulled to the screen's width
  at two cuts in each of four bands (`UWClassicLayout.ClassicWide`: top bar rows 0-18 at x 78/198, view 19-127 at
  53/223, ledge 128-167 at 100/173, scroll 168-199 at 82/223), the columns put in grown pixel by pixel from plain
  sources (`UWMainFrame.ClassicWideSources`, the rust left out), the hole flooded from the view's middle over MAIN.BYT's
  clear pixels; the pieces at the frame's places shifted by their band (`ClassicWideShiftX`): the compass on its base,
  the gem on its stand, the flasks, the rune hollow, the active spells on the left stone, the scroll, the panel at the
  right with its turn and chains (the stats on the chain's pull, the tablet on the runes key), the gargoyle's eyes,
  the dragons (`UWHudDragons`), the weapon under the frame, the command column as decoration (the fight icon lit),
  the conversation in the original's look over the view (`UWConversationArtBuilder`, CONV.BYT's pieces). Nearly all of
  it reads `UWClassicLayout` (the first game's numbers) directly; the host gives `ViewPicture` and `DragonSprite`.
  WHAT THE SECOND GAME'S FRAME HAS (BYT.ARK block 4, all opaque): the view's black window x 16-223, rows 16-143; the
  top bar over it with the gargoyle (head x 108-131, its claws reaching to about x 65 and 177) and the eyes (0x6E);
  the marble ledge (rows 145-165) with the spells' hollow, the compass recess with the gem and the rune hollow (U6's
  cuts); the scroll under it (x 8-231, rows 167-199); and A RIGHT BLOCK from about x 226 over all rows - the panel
  (0xEB, 79 x 112, sliding in twenty frames with CHAINS.GR at 0x110), the flasks on their stone with the chain between
  them (`FlaskClicks`: values, the chain's pull), the command bar (OPTB.GR, 2 x 3 at 0xF3, rows 171-199) - and no
  dragons, no left command column, no clear pixel (the hole is the black window, not a flood). THE CONVERSATION SCREEN
  (BYT.ARK screen 2, `UWConversationScreenUw2`) covers everything left of the right block - the portraits and trade
  boxes at the top (rows 0-76), the parchment (rows 77-165) and the answers' scroll (rows 166-199) - and keeps the block.
  PROPOSED SHAPE (UW1's mechanisms, UW2's pieces): the RIGHT BLOCK MOVES WHOLE to the right edge in every band, the
  left part grows: the top bar cut either side of the gargoyle's claws (the gargoyle stays over the view's middle), the
  view between its walls, the ledge between the spells' hollow and the compass recess and between the recess and the
  rune hollow (the compass stays in the middle, the rune hollow goes with the right side), the scroll in its paper -
  the columns grown as on UW1 (`ClassicWideSources` per game). A CLASSIC WIDE GEOMETRY PER GAME (as the stats page's,
  the paperdoll's, the compass's, the tablet's): the bands, cuts, sources, hole, camera area, the pieces' places, the
  panel's turn (UW1) or slide (UW2) with its chains, the eyes, the command bar; `UWModernClassicFrame` reads it instead
  of `UWClassicLayout`. The conversation's original look on UW2: screen 2 widened by its own cuts over the frame's left
  part, the right block staying (as the original keeps it); in Classic Modular the screen's pieces freed (the portraits'
  and trade boxes, the parchment), as UW1's CONV.BYT pieces; the host's `HasOriginalLook` then true.
  STEPS (proposed): (1) THE WIDENED FRAME: the geometry per game and the builder on UW2's frame, rendered as PNG at
  16:9 and 21:9 and checked by eye with the user before anything runs (the cuts, the grown stone, the gargoyle's bar);
  (2) CLASSIC WIDE RUNS ON UW2: `Supports(Classic)`, the frame class on the geometry - the camera's area, the weapon and
  the gargoyle's teeth (3DWIN.GR 0), the pieces at the frame's places (compass with gem, both hollows, flasks, scroll),
  the panel with its pages, its slide and chains, the eyes, the command bar as decoration with the fight icon lit (as
  UW1's column), no dragons; (3) THE CONVERSATION'S ORIGINAL LOOK on UW2, in Classic Wide over the widened left part and
  in Classic Modular as freed pieces.
  DECIDED per user the same day: THE COMMAND BAR IS DECORATION, as UW1's column (shown, the fight icon lit while the
  weapon is drawn, clicks do nothing). THE CLASSIC SCHEME'S WIDESCREEN BOX for UW2 (UWClassicWide, UW1's classic
  scheme) COMES AFTER U7, as a part of its own on U7's cuts; UW2's classic screen stays at 320 columns until then.
  STEP 1 BUILT 2026-11-24 (test list section 206 open, PNGs only - Temp/U7/classicwide_uw2_16x9.png and _21x9.png;
  self-check green, the first game's widened frame byte for byte as before at 0, 106 and 240 columns): THE WIDENED FRAME.
  `UWClassicWideGeometry` per game (the bands, cuts, sources, hole, camera's area, how the hole is told - the first
  game's clear pixels, the second game's window black, `HoleBlack` - and whether COMPASS.GR 0 covers a baked needle);
  `UWClassicWideBuilder.Build/Compose` cut on it, its static members the first game's as before. The second game's
  numbers in `UWClassicLayoutUw2.ClassicWide` (measured on luma maps): bands from rows 15 (view), 145 (ledge), 167
  (scroll); cuts top bar 46/209 (plain stone either side of the claws), view 16/224 (the black's edges), ledge 75/165
  (plain marble either side of the compass recess), scroll 60/180; sources the bar's stone without moss (30-61,
  201-216), the ledge's marble without the hollows' black top lines on row 148 (70-83, 157-169; the first try took
  them and grew dark dots), the scroll's rods (20-109, 130-219). The hole is the camera's area (16/16, 208 x 128),
  flooded over the window's black: its black ring and the gargoyle's teeth stay. Checks: `Uw2ClassicWide`. The
  baked compass, gem and flasks stay in the frame until step 2 lays the pieces on them.
  PER USER the same day, from three rendered variants of the ledge (A the hollows apart as built, B together round
  the compass, C spread): C. A BAND MAY HAVE ANY NUMBER OF CUTS now (`UWClassicWideGeometry.Cuts` rows,
  `Share`: the columns put in shared evenly, the remainder to the later cuts - two cuts as before, the first game's
  frame byte for byte the same at 0, 106 and 240); the second game's ledge cuts four times, at 13 and 226 (the only
  full-height plain columns beside the bevels at its ends) and at 75 and 165, a quarter each: the spells' hollow
  moves a quarter, the compass recess half, the rune hollow three quarters - the group stays, the rune hollow comes
  close to the panel and its rune page. Test point 206.6. TEST 206 CONFIRMED per user 2026-11-24 (all six points, on the
  PNGs; UW1's Classic Wide looked at in the game, on the build before U7). STEP 1 COMPLETE.
  STEP 2 SPLIT the same day (it touches some twenty places that read the first game's frame): 2a Classic Wide runs on
  UW2 with its pieces and clicks, a page changing at once; 2b the panel's own slide with the chains. NOTE on the user's
  answer "decoration as UW1's column": the first game's column is decoration but for the fight icon, which lights and
  works (draws or sheathes the weapon) - so the second game's bar does the same.
  STEP 2a BUILT 2026-11-24 (test list section 207 open; self-check green, build done, not tried in the game): the host
  `Supports` every preset and `Has(ClassicFrame)`; `UWModernClassicFrame` reads the geometry (`ShiftX`, the camera's
  area, the hole, `ProjectionCentreRow` - the second game's window middle, row 80 -, `WeaponRow` - its view's bottom,
  144 -, `HasDragons`) and places the second game's pieces (`fTryGetPlaceUw2`: the compass whole over
  `Uw2CompassBox` with the gem, the flasks at FL_X on their stone, the rune hollow, the spells at spell_x moved with
  the spells' hollow, the scroll from its knobs, the pages at 0xEB/7, the modern conversation over the view's middle),
  the eyes at its head, the command bar drawn plain (OPTB.GR 0) with the fight icon lit (OPTB.GR 1's first, 26 x 15)
  and clickable; the clicks by rules the classic screen now shares (`UWClickRules.CommandIconAtUw2`, `IsInFlasksUw2`,
  `FlaskAtUw2`). `UWGameScreenUw2` puts the camera in the frame's window at the original's angle. The scroll is its own
  picture widened by the frame's columns, four lines and [MORE]'s row as on the first game (both boxes are 30 rows;
  counting the second game's five rows as lines made the paper a line taller and showed its top rod twice, per user's
  screenshot - fixed the same day); per the user's next screenshot also fixed: the camera's area began half the
  columns put in too far right (`fSpan`'s left edge now takes the columns put in at a cut on it - the second game's
  view cuts at its window's edge, the first game's spans start left of their cuts), and the clipped weapon stood the
  view's bottom too high (its top counted from the clip area's bottom); and per the user's crops beside the original,
  the scroll's rolling edges were a row short at the bottom in both schemes: SCRLEDGE.GR's pictures are 29 rows and
  pic_to_screen draws a picture at its own size (GRSPIC.C seg009_73 overwrites the 0x1C draw_edges passes) -
  `UWClassicLayoutUw2.MessageLog.EdgeHeight` 29 now (test point 207.10); TEST 207 CONFIRMED per user 2026-11-24 (all
  ten points). STEP 2a COMPLETE.
  STEP 2b BUILT 2026-11-24 (test list section 208 open; self-check green, build done, not tried in the game): THE PANEL
  SLIDES in Classic Wide on UW2. `UWPanelTurn.Slides` (set by the frame per game before Begin): twenty frames
  (`PanelSlide`), `Frame.Offset` the old page's columns out for ten frames, then the new page's in, the flip on the
  eleventh, `ChainFrame` CHAINS.GR 0 to 3 on frames 1, 4, 13, 16 (-1 between); the same sides, targets and conversation
  hold as the turn. `UWModernClassicFrame.fUpdateSlideUw2`: a frame each `FrameSeconds`, the pages moved by the offset
  (`ApplySquash`), the panel's place in palette colour 1 under them, over them the frame right of the panel and black to
  the screen's edge (a canvas of its own, `UWUiSorting.ClassicWideSlideCover`), the last chain picture kept as the
  classic screen keeps it. Checks: the slide's frames in `Uw2ClassicWide`. Not done: the classic screen's own slide
  (`UWGameScreenUw2.fSlideStep`) still has its own copy of the frame rule - a later clean-up could run it on
  `UWPanelTurn.Slides` too. TEST 208 CONFIRMED per user 2026-11-24 (all six points). STEP 2b AND STEP 2 COMPLETE.
  PER USER the same day (a screenshot: the pointer on the frame, the name in the middle "Table"): the second game named
  in the middle what the free pointer was on (its ray through the pointer, even off the camera's area); now as the
  first game (the user's choice of three): the view's middle's thing (`UWWorldViewUw2.PointedEntity`, `fCentreEntity`;
  the key icon's door too), the clicks still the pointer's. TEST 209 CONFIRMED per user 2026-11-24 (all three points).
  STEP 3 SURVEYED 2026-11-24 (per user "Ja, Schritt 3"): THE CONVERSATION'S ORIGINAL LOOK ON UW2. On UW1
  (`UWModernConversation.fLayoutOriginal`, `UWConversationArtBuilder`) it is one block of CONV.BYT's pieces: the top
  (name plates, portrait frames, the two trade areas, x 43-232, rows 0-46), the parchment with the history under it
  (rows 47-135) and an answers' parchment of its own with the scroll's knob rollers; the names, portraits, slots, the
  history's and the answers' places and the colours are `UWClassicLayout.Conversation`'s; in Classic Wide the block lies
  over the view at x 43 (the dragons beside it), in Classic Modular where the layout puts it. The host's
  `HasOriginalLook` is false on UW2. UW2'S CONVERSATION SCREEN (BYT.ARK screen 2, `UWConversationScreenUw2`) covers
  everything left of the right block and below it: the top (rows 0-76) with the two portrait boxes (portraits at 2 and
  0xA8, row 3), the two trade grids of 2 x 3 between them (0x4B and 0x7C) and the name bars (the partner's under the
  grids at 0x47, the player's above them, right-aligned before 0xA4); the parchment (rows 77-165) with the history
  (npc_scroll, x 0x18 to 0xDA, 13 lines of 6 rows from row 81) and its rolling edges (three tiles a side at 0x11 and
  0xDB); and the answers on a scroll the full width of the screen (menu_scroll, x 8 to 0x137, rows 166-199) - over the
  command bar; the inks are palette colours (0x75 partner, 0x68 player, 1 narrator).
  PROPOSED SUB-STEPS: (3a) the second game's pieces cut from screen 2 (the top, the parchment, the answers' scroll)
  and a conversation geometry per game for the places, rendered as PNG and checked with the user; (3b) the original
  look in Classic Modular on UW2 (`HasOriginalLook`, the modern conversation reading the geometry); (3c) the original
  look in Classic Wide on UW2. OPEN for the user: in Classic Wide, (A) screen 2 widened like the frame (the left part
  and the bottom scroll grow, the right block stays - as the original covers the screen), or (B) the pieces at their
  places over the widened frame (as the first game's block over its view). Both can be rendered as PNG in 3a.
  DECIDED per user the same day, after a third variant came up: (C) CLASSIC WIDE GOES BACK TO 4:3 FOR THE CONVERSATION,
  as the first game's classic scheme does with its widescreen box (`UWClassicWide.Update`: Extra 0 while the
  conversation frame shows) - the frame at the original's 320 columns, black at the sides, screen 2's pieces at their
  own places over its left part and the full-width answers' scroll, as the original shows it; wide again afterwards.
  The first game's modern Classic Wide keeps its block over the wide view (unchanged). So 3c is the switch to 4:3
  and the pieces at screen 2's places; 3a and 3b as proposed.
  STEP 3a BUILT 2026-11-24 (test list section 210 open, a PNG only; self-check green): THE SECOND GAME'S CONVERSATION
  PIECES from screen 2 (`UWConversationArtBuilder.BuildHeaderUw2`, `BuildParchmentUw2(rows)`, `BuildAnswersUw2(width)`;
  `UWClassicLayoutUw2.Conversation.Header` 0/0 234 x 77, `Parchment` 17/77 206 x 85 with `ParchmentRim` 6,
  `AnswersScroll` 0/167 320 x 33 narrowed or widened from `AnswersNarrowFrom` 40): opaque, the black reached from the
  edge clear; the stone columns and the dark shade beside the parchment are the frame's. First cut a column short at
  the right box (its outline at 233; the box went clear through the gap) and two columns too wide at the parchment.
  Checks: `Uw2ConversationPieces`. The places of the names, portraits, slots and texts on them come with 3b. NOTE: the
  Unity build clears the project's Temp folder - the PNGs for the user go to the session's scratchpad now. PER USER the
  same day: the black dashes at the answers' scroll's ends were the pillars' dark edge (colour 163, bluish) - cleared
  as the main scroll's bluish stone (test point 210.4); so was a pixel over each of the parchment's top corners, cleared
  the same way (`fClearPillarEdge`, test point 210.5). TEST 210 CONFIRMED per user 2026-11-24 (all five points, on the
  PNG). STEP 3a COMPLETE.
  STEP 3b BUILT 2026-11-24 (test list section 211 open; self-check green, build done, not tried in the game): THE
  ORIGINAL LOOK IN CLASSIC MODULAR ON UW2. `UWConversationLook` per game (data layer): the block's width, the pieces'
  places, the history's and the answers' text, the names (the second game's player's right-aligned), the portraits,
  the slots (2 x 3 a side on UW2), the colours (the first game's fixed, the second game's palette inks); the first
  game's values exactly the constants `UWModernConversation` kept (checked). `UWModernConversation.fLayoutOriginal`
  reads it; `UWHudArt.BuildConversationHeader/Parchment` give the game's pieces (UW2: screen 2's top, parchment, and the
  answers' scroll the block's width, as high as the first game's answers' parchment, 52 rows - `AnswersRim` 10);
  the host's `HasOriginalLook` true. Until 3c the original look stands in Classic Wide too, over the view. Fixed at the
  test: the player's name came from the view (empty on UW2) - now the host's as in the modern look, the view's for the
  editor's preview; and the preview's history was empty on UW2 (its history shows `LinesRevealed`, 0 without a
  session) - `UWConversationView.Preview` gives every line out at once; and the preview did not come on UW2 at all
  (`fIsPreviewing` asked for a control scheme above the conversation, which on UW2 hangs on its screen - the scene's
  now); the preview's partner is the host's (`PreviewPartnerName/Portrait`: the first game's Shak, the second game's
  Lord British, whoami 142, his name from block 7), its heads from the view where no conversation runs (UW2 host). Then the preview came only after a first
  conversation: the UW2 host's `Data` was the conversation screen's, which exists only from then on - the game
  screen's before; its names and heads only from a running conversation (`fRunning`), else the last partner's showed.
  The preview's player name is the save's on UW2 (`UWPlayerRecordUw2.Name`, it said "Avatar"); its sample text stays
  the first game's (Shak at his forge) beside Lord British - per user, fine for a preview. TEST 211 CONFIRMED per user
  2026-11-24 (all eleven points). STEP 3b COMPLETE.
  STEP 3c BUILT 2026-11-24 (test list section 212 open; self-check green, build done, not tried in the game): CLASSIC WIDE
  AT 4:3 FOR THE SECOND GAME'S CONVERSATION. `UWModernClassicFrame.fTalkAt4By3` (UW2 and a conversation open): Extra 0
  and the frame in the middle as with the 4:3 box; `fUpdateConversationScreenUw2` lays screen 2 over the frame (opaque, the
  right block from PanelLeft above PanelBottom left clear, so the frame's block with the page shows), the command bar and
  the fight icon hidden under it; the conversation block from screen 2's top-left (TryGetPlace). `UWConversationLook.Uw2Screen`:
  the block's places but the answers on the full-width scroll at row 167 (33 rows, menu_scroll's text from x 8 and row
  169, 302 wide; `AnswersWidth` for the piece); `UWModernConversation` takes it in Classic Wide on UW2 and does not dim
  there. The first game's Classic Wide unchanged (its block over the wide view). TEST 212 CONFIRMED per user 2026-11-24
  (all six points). STEP 3c AND STEP 3 COMPLETE. NOTE: the user had thought the first game's Classic Wide goes to 4:3 in a
  conversation too (it is its classic scheme's widescreen box that does); whether the second game should rather lay its
  pieces over the wide frame as the first game does is being discussed.
  DECIDED AND BUILT the same day (per user, from rendered variants; test list section 213 open; self-check green, build
  done, not tried in the game): AS THE FIRST GAME'S, variant b - the 4:3 switch and screen 2 under the conversation are
  gone again; the frame stays wide, the block over the view from (Left, 0) (`TryGetPlace`), `UWConversationLook.Uw2Wide`:
  the parchment 70 rows (`WideParchmentRows`, the history 63 rows), the answers' scroll of its own right under it at 52
  rows, the block 199 rows - no scaling; the frame's scroll stays empty, the world dims as on the first game. With it the
  second game's top is whole and opaque (`ComposeHeaderUw2` clears nothing: the black column between the grids, reached
  from the edge, had gone clear). TEST 213 CONFIRMED per user 2026-11-24 (all five points). STEP 3 COMPLETE. STAGE U7
  COMPLETE 2026-11-24. Left after it (not U7): the classic scheme's widescreen box for UW2 (on U7's cuts, per user after
  U7); the clean-up of the classic screen's slide copy onto `UWPanelTurn.Slides`.
- **U8 The classic scheme's widescreen box for UW2** (per user after U7, "Breitbild-Schalter UW2", 2026-11-24).
  SURVEYED the same day. ON UW1 (`UWClassicWide`, the Graphics page's "Classic screen widescreen", `ClassicWidescreen`):
  the classic frame (`UWGameUI.mGameFrame`) widened to the window by Classic Wide's cuts (`UWClassicHud` builds the
  picture with `UWHudArt.BuildClassicWideFrame`), and once a frame, before the canvases draw, every direct child of the
  frame moved by its place's band (`ClassicWideShiftX`); parts in the view's hole move with its middle (`InHole`: the
  weapon, the window picture), the message box grows (`Widened`), corner-anchored nodes keep their size (`FrameSized`);
  `Extra` 0 while the conversation frame or the big map shows, in the modern scheme and with the help squeezing the
  frame; the click table reads original columns (`OriginalX` -> `UWClassicGeometry.OriginalColumn`, -1 in the columns
  put in). It all reads the first game's cuts (two a band). ON UW2: the switch shows on the Graphics page but does
  nothing (`UWGameScreenUw2` stands on 320 columns): its frame `mOFrame` with the screen picture and every part as a
  direct child at frame coordinates (`fImage`, groups such as the panel's window with its mask), side bars at 4:3, the
  camera from `UWClassicLayoutUw2.Frame.ViewWindow*`, `TryGetOriginalPoint` on 320 columns, the pointer's zones in
  fifteenths of the 208-wide window, its own conversation screen (screen 2) and map screen.
  PROPOSED STEPS (the first game's mechanism, the second game's geometry): (1) `UWClassicWide` and
  `UWClassicGeometry.OriginalColumn` per game (`UWClassicWideGeometry`: `ShiftX`, any number of cuts); on UW2 the frame
  picture widened (`UWClassicWideBuilder.Build`), `mOFrame` and the side bars as wide as the window, the parts moved by
  their bands, the weapon and the teeth in the hole, the scroll's paper and text widened, the camera in the widened
  window - a look first, the clicks still at 320; (2) the clicks and the pointer: `TryGetOriginalPoint` through the
  original columns, the pointer's zones over the widened window as the first game's, the scroll's wrap at the wider
  width, the hover places; (3) where the original's 320 stays, as on the first game: the conversation screen, the map,
  the options panel if it covers the view, the help beside it. OPEN: none known; the first game's rules apply.
  STEP 1 BUILT 2026-11-24 (test list section 214 open; self-check green, build done, not tried in the game): THE LOOK.
  `UWClassicWide.Attach(frame, geometry)` (the first game's unless given) moves the parts by that game's bands,
  `PlacedAs` for a part moving as another place (the second game's active spells with their hollow);
  `UWGameScreenUw2`: attached with `UWClassicWideGeometry.Uw2`, `UWClassicWide.Update` while the classic screen shows
  (suspended for the conversation screen and the map; 0 otherwise), the frame `320 + Extra` wide with the widened
  picture (`fEnsureWideScreen`, through the colour help), the side bars from that width, the scroll's paper `Widened`,
  the camera's window `ViewWindowWidth + Extra` wide. Clicks, the pointer's zones and the scroll's wrap still on 320
  columns (step 2). TEST 214 CONFIRMED per user 2026-11-24 (all six points). STEP 1 COMPLETE.
  STEP 2 BUILT the same day (test list section 215 open; self-check green, build done, not tried in the game): THE CLICKS.
  The way back per game (`UWClassicWideGeometry.OriginalColumn`, any number of cuts; `UWClassicGeometry.OriginalColumn`
  and `UWClassicWide.OriginalX` onto it - the first game's columns checked to be the same as before); the second game's
  `TryGetOriginalPoint` reads the widened frame's width and maps back by its cuts, false in the columns put in (every click
  of the frame - inventory, flasks, chain, compass, cast area, command bar, options - goes through it); the scroll wraps at
  `MessageLog.Width + Extra` (`ClassicLogWidth`, the paged text too, as the first game's `fLogWidth`). The pointer's zones,
  the walk and the missile window were already fractions of the camera's window (now widened), as on the first game.
  TEST 215 CONFIRMED per user 2026-11-24 (all nine points). STEP 2 COMPLETE.
  STEP 3 SURVEYED the same day, no code (per user, a test only; test list section 216 open): the first game keeps the
  original's 320 only under its conversation frame and the big map (`UWGameUI`'s `UWClassicWide.Update`), which the second
  game's screen has done since step 1 (its conversation screen and map, test 214.4); with the help beside the frame
  `UWClassicWide.Update` itself narrows the widening to leave the help its full width, the same call on both games; the
  options lie in the panel on both games and cover no view, so the frame stays wide under them. TEST 216 CONFIRMED per
  user 2026-11-24 (all six points). STEP 3 COMPLETE. STAGE U8 COMPLETE 2026-11-24. Next: the beta gate.

DECIDED 2026-11-23 per user: the Custom layout (`ModernLayout`) is kept PER GAME - UW2's pieces have other
measures than UW1's, a layout arranged for one does not fit the other; today's layout stays UW1's. It is
the first per-game setting (phase 0's candidate) and is built with part U4e.
OPEN, for the user when the stage comes: how Classic Wide lays out UW2's frame (U7) - surveyed 2026-11-24, a shape
proposed in U7's entry.

### Phase 6: the port's own things for UW2 (M)

DECIDED 2026-11-17 per user: after phase 5 and this phase's documentation (README, CONTROLS, DEVIATIONS for two
games) UW2 is declared a BETA, and the second code cleanup starts in its own branch (Todo: 'A second cleanup phase').
The rest of this phase is built on the cleaned code.
GATE ADDED 2026-11-18 per user: before UW2 is declared an alpha or a beta, the classic scheme is checked to match the
original. One deviation is decided: the original moves things in the inventory only by dragging with the right
button, the first game with either button; ours keeps either button for the second game too (an entry in
Docs/DEVIATIONS.md).

Remastered details (ice, the new texture set), the help window (stats, map, notes, spells) (BUILT but the worn
magic, stale entry corrected 2026-11-19: `UWHelpMapTab` with `UWHelpMapUw2`, the UW2 branches of `UWHelpStatsTab`
and `UWHelpSpellsTab`, the Teachers tab in `UWHelpMantrasTab`, test 89; the worn magic in the Stats tab is still
missing in UW2),
`UW2/rules.json` and its template, the translation kit for UW2's texts, the gamepad and the modern
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
2. ~~**UE's decompiled C as the reference for UW2**, read and cited, never copied.~~ Decided
   2026-10-28: yes - the same rule as for the UW1 disassembly; comments cite UE's file, line and
   routine. UE's licence covers its own work only and grants no right in the decompiled code.
3. ~~**UE's vectors as self-check fixtures**~~ Decided 2026-10-28: yes - `uw2/vectors/*.csv`
   (numbers computed by running UW2.EXE's routines, MIT) go under `Tools/Fixtures`, credited in
   THIRD_PARTY_NOTICES; the UW2 variants of phase 4 are checked against them.
4. ~~**Writing UW2's `LEV.ARK`**~~ Decided 2026-10-28: compressed like DOS, with an LZSS encoder
   (a block stored plain where compression saves nothing, as `put_arc` does).
5. ~~**The modern scheme and the port's additions** for UW2 from phase 3 on (proposed), or the
   classic game first and the additions after phase 5.~~ Decided 2026-10-29: at the end of phase 3,
   as its step 8, after the classic interface of steps 1-7 (the user also took the eight steps in
   their order).
6. ~~**Releases**: UW2 work in `main` behind the game id, or a long-lived branch.~~ Decided
   2026-10-28: in the main line behind the game id; every step keeps UW1 byte-identical, so UW1
   releases can go out at any time.
