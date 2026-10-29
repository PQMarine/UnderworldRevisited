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
with the original - confirmed in the game by the user). Phase 3 (the screen and the hands) is next. Written 2026-10-27 from a read of
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
   three as without a save; Create Character starts the walk, `UWWalkUw2`, the introduction and
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
   `UWLighting.LevelMinimum` (`UWWalkUw2View`); levels 65-72 but 68 unlit (`process_grid`). The 3D
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
   levitating 1, flying 14, slow fall 4), the turn the state's share below levitation, the view's
   follow 0x600, no fall damage on levels 65-72 ((level - 1) / 8 == 8 - UE's prose says world 8,
   its code the ninth), no lava exception, impacts above 0x180. The base speeds, the turn step, the
   jump and the fall's Acrobat check are the same numbers in both games. STILL THE FIRST GAME'S
   NUMBERS IN A UW2 RULE SET, each to be held to UW2 in phase 4: combat, magic, experience, vitals
   (swimming's rise and sink among them), sleep, lights, levels, items, traps, creatures, the
   conversation - everything but motion; the profile's other variants (magic, combat, player,
   creatures, items, traps, conversation, the trap and a_do tables) still throw. THE WALK: the player
   stands on tile 0x13/0x30 and walks with the motion core (`UWPlayerMovement` runs while
   `UWWalkUw2.IsWalking`, `HasWorld` stays false for the first game's systems); the modern scheme's
   mouse look captures, the classic keeps the system arrow and walks by the keys; no move triggers
   fire and no transitions are built (step 5); the Swimming skill is 0 without a character (step 6).
   The variant's numbers are held by the GameProfile group.
   PART B, DOORS AND THE AUTOMAP, BUILT 2026-10-29: the walk opens and closes the door in front with
   the modern use key or a click (`UWWalkUw2View`, within two tiles; a locked door stays shut, no
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
   frame); MODERN's own motor does not yet. DEVIATIONS: the skid effect (`set_effect(0x80, 4)`) is not
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
   cloud_saves; "Automatisch + Schalter"): automatic follows the folder with the newest SAVE1-4, without
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
   the carrying limit (CanCarry needs the first game's player data). Saving the moved inventory is part D.
   PART B BUILT 2026-10-29 (e0274bd9), CONFIRMED in the game by the user the same day after b21211c9 (carried objects got no picture and vanished when dropped): picking up and putting down are the same code in
   both games (UE player_3dget, ReturnObject) but for strings and UW2's pressure plates. The right button
   in the view: with an object on the pointer it is dropped in front (UWPlayerThrow.TryFindDropSpot, lifted
   out of the first game's drop unchanged; no room says the game's 0x10D); with Get it takes; with no
   command or Look a drag takes and a release looks (mous_in_3d's "look and drag to get") - within the
   hand's reach (UWReachRules.CanReach; too far says 0x6A), the thing out of its tile onto the pointer, a
   container with its contents. Not yet: the terrain between (BlockingTerrain), the weight limit, theft,
   moonstones, pick-up triggers and pressure plates, the throw - the second game's rules (phase 4).
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
   for UW2's main menu (UWWalkUw2.Leave; the first game's Quit also leaves to the menu, not to DOS). The
   file page lists the four descriptions in the scroll (0x120, "I- " to "IV- "); saving asks 0xB7 with
   the old description (or "<not used yet>") selected, up to 30 characters, Enter or a click saves,
   Escape aborts, then only the outcome stays in the scroll (0xAE + 4/5/6); restoring an empty slot says
   0xAE + 1, a save starts the walk from it and the new screen says "Restore Game Complete.". The
   provisional Ctrl+S and its backups went (UWWalkUw2View.Save is the panel's). As the first game's
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
   characters, `FONT_4X5P`), the eraser; the automap that can be off and the map scraps.
7. **Conversations**: `BYT.ARK` block 2, the portraits (`CHARHEAD.GR`, `GHED.GR`, `HEADS.GR`), the
   names, the two scrolls, six trade slots a side with UW2's regions, the VM with UW2's 51 built-ins
   (the nine new ones: `babl_hack`, `give_all_stuff`, `set_sequence`, `switch_pic`, `teleport_player`,
   `teleport_talker`, `transform_talker`, `x_clock`, `x_exp`), the quest store of 128 bits and 16 bytes,
   the refusals; built-ins whose rules come in phase 4 log what they would do.
8. **The modern scheme on UW2** (decision 5): the presets, Classic Wide, the bags, the minimap and the
   modern conversation over UW2's art and numbers.

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
