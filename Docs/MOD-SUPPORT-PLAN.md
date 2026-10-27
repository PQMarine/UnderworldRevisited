# Mod support: plan

Status: PARTLY BUILT (updated 2026-10-27, version 0.10.0). Written 2026-10-11 from five
read-only surveys of the code (rule constants, level-bound behaviour, logic left on the Unity
side, data load points, texts). Since then the code cleanup is complete
([CLEANUP-PLAN.md](CLEANUP-PLAN.md), phases 0 to 10 with 0.9.2), the rule set shipped with 0.9.2
and the languages with 0.10.0. What is built is summed up in section 0; sections 2 and 3 say per
point what the code does today and what is still the plan. Line numbers were right on the day
they were written and will drift; the file names will not.

**Contributions welcome** (decided 2026-10-27): the open parts are not planned to be built here
for now - the next work is the small points under Next in [ROADMAP.md](../ROADMAP.md), then
Ultima Underworld II. A pull request for any phase below is welcome; open an issue first
([CONTRIBUTING.md](../CONTRIBUTING.md)). Section 4 says where each piece attaches and what it
can start from.

The plan covers four things that turn out to be one piece of infrastructure:

1. A **mod folder** with manifests, a load order and an in-game switch. *Open.*
2. **Overlays** on the game's data: texts first (that is the translation kit), later the tables.
   *Texts built (as language files, not yet as mods); tables and pictures open.*
3. A **rule set** that holds the numbers and quirks now hard-coded, so a mod can change them.
   *Built as one rules.json; difficulty page and per-level table open.*
4. **Hooks** for scripted mods, after the first three exist. *The seams exist; no hook API yet.*

The Reddit wishes of 2026-10-06 (randomizer, more item combinations, difficulty sliders,
free level layout, object limit) map onto these layers; where they land is noted per phase.

## 0. State on 2026-10-27

| Piece | State | Where |
|---|---|---|
| Session, world interface, registries (the seams of phases 0-4) | built (cleanup phases 2-4) | `UWGameData`, `UWGameSession`, `IUWWorld`, `UWTrapTable`, `UWConversationFunctionTable`, `UWItemUseTable`, `UWFoodTable` |
| One seeded generator per game | built (cleanup 2.8); the seed is not shown or kept | `UWSessionRandom` |
| Rule set with `rules.json` | built (0.9.2): 248 rules in 15 domains, quirk and deviation switches, `levels.*`, `story.*`; a few of the first twenty values still `const` | `UWRules*.cs`, `UWRulesJson`, `uwdump rules-template` |
| Difficulty group and in-game rules page | open | - |
| Per-level table (`levels.json`, `levelCount`) | open; the story's level numbers are rules (`levels.*`) | `UWRulesLevels`, `UWLevelIds` |
| Game-text overlay | built (0.10.0) as `Languages/<code>.strings.tsv`, not as a mod | `UWGameTextPack`, `UWGameData` |
| The port's own texts | built (0.10.0): about 870 ids, German shipped | `UWText`, `UWTextEnglish.*.cs`, `Languages/de.tsv` |
| Grammar | built (0.10.0): English, German, a generic grammar driven by a header | `IUWGrammar`, `UWGrammarEnglish/German/Generic` |
| Fonts and decoding | built (0.10.0): `CharSymbol` fixed, German glyphs, fallback fonts, IME | `UWFontGlyphBuilder`, `UWFallbackGlyphs`, `UWLanguageFont` |
| Translator tools | built (0.10.0): `text-template`, `strings-template`, `validate-tsv`, `fit`, `looks`, `typed-words`, the Translation Kit | `Tools/UWDump`, [TRANSLATING.md](TRANSLATING.md) |
| Mod folder, manifest, load order, Mods page | open | - |
| Save marker in `UWR.json` (mods, seed, format) | open | `UWHelpNotes` has the sidecar |
| Table overlays (COMOBJ, OBJECTS, CMB) | open | - |
| Conversation patches as data | open; the built-in patches have a switch (`deviations.conversationPatches`) | `UWConversationPatches` |
| Picture overlays | open | - |
| Damage pipeline, pickup, doors, creature casting, terrain, death event | built (cleanup phases 2-4) | `UWDamagePipeline`, `UWPickupRules`, `UWDoorRules`, `UWPlayerTerrainRules`, `UWDeathRules` |
| Hook API (`UWEvents`), scripting | open | - |
| Randomizer | open | - |

## 1. Goals, limits, legal line

- A mod ships only its own content: rule files, language files, patch tables, its own
  pictures and scripts. Never a byte of the original game. Translated GAME texts are an
  adaptation of the rights holders' text and stay out of this repository; the translator
  publishes them separately (decided 2026-10-08, built so in 0.10.0: the repository and the
  Translation Kit hold no game text, English or translated). Translations of the port's own
  texts can live in the repository (`Languages/de.tsv`).
- Save games stay readable by the DOS original where a mod does not change the data format.
  A mod that does change it (more object types, more levels) gives up that compatibility,
  and the save carries a marker saying so. The languages keep this already: names in a save
  hold Latin-1 only.
- The engine-free layer (`Assets/UWDataImport`, no `UnityEngine`) is where mods attach.
  Nothing a mod can reach lives on a MonoBehaviour.
- English stays byte-exact. Every rule keeps the original's value as its default; a mod
  overrides, it never replaces the code path. The rule set holds to it: only coefficients are
  rules, never a formula's integer shape, and a switch only chooses between two paths that are
  both in the code.
- Not planned: loading C# DLLs from mods (arbitrary code on the player's machine).

## 2. What the surveys found (2026-10-11), and what changed since

### 2.1 Where the data is loaded

`DataImport` (`Assets/UWDataImport/DataImport.cs`) was the single root. Every loader takes
the DATA folder path and does `Path.Combine` + `File.ReadAllBytes`; there is no virtual
file layer. `UWLevelLoader.fLoadData` builds it once and keeps it in a static
(`msOSharedData`), rebuilding only when the path changes.

*Since then:* `DataImport` is a facade over `UWGameData` (one per installation, everything that
does not depend on a save) and `UWGameSession` (one per running game). `UWGameData` takes the
game-text file besides the path and lays it over the strings in its constructor, before anything
reads them; the language is part of the cached data's identity (`GameTextFile`), exactly the
pattern the mod set needs.

| Data | Class | Mutable after load | Overlay today |
|---|---|---|---|
| STRINGS.PAK | `UWStrings.Blocks` (dict of lists) | yes, fully public | **built**: `UWGameTextPack` from `Languages/<code>.strings.tsv` |
| LEV.ARK (levels, notes, automaps) | `UWSessionWorld`, `UWLevel.TileData`, `Masterlist` | yes | in memory; `UWLevArkCodec` and the writers persist it |
| CNV.ARK conversations | `UWConversations`, `Conversation.Code` (ushort[]) | code in place | `UWConversationPatches` patches a clone per slot, compiled in, switchable |
| COMOBJ.DAT | `UWCommonObjectProperties` (private `Entry[]`) | no | needs a setter |
| OBJECTS.DAT (weapons, armour, critters, lights, ranged) | `UWObjectClassProperties`, `UWObjectProperties` (private) | no | needs setters |
| CMB.DAT combinations | `UWObjectCombining` (`IReadOnlyList<Rule>`) | no | needs an add |
| *.GR / *.BYT pictures | `UWTextures` (public dicts, `ReplaceIndices`) | yes | in memory; a stray file in DATA throws |
| FONT*.SYS | `UWFonts` / `UWFont` (glyph tables) | German glyphs appended (`UWFontGlyphBuilder`) | file level |
| PALS, LIGHT, SHADES, XFER, TERRAIN, WEAPONS.DAT, SOUND, CUTS | private | no | file level |

Two facts shape the design: the GOG cache (`persistentDataPath/GameData/UW1`) is wiped and
rebuilt when `game.gog` changes, so a mod folder must live outside it; and the only seam
that catches every file is the DATA path string handed to the data root, while in-memory
overlays catch the mutable structures right after the constructor.

Saves: `UWSavegameStore.Write` copies the pristine `DATA/LEV.ARK` into the slot on the first
save and the writers rebuild the object lists from the in-memory model, so a world changed at
new-game time is persisted by the existing writers. `SAVEn/UWR.json` (`UWHelpNotes`) is a JSON
sidecar the original ignores; it already carries a stamp of PLAYER.DAT, so a "made with mods"
marker fits there - and so would the side file for names beyond Latin-1 that
[TRANSLATING.md](TRANSLATING.md) mentions as a plan.

### 2.2 Hard-coded rule values

About 300 rule values and formulas sat in the engine-free layer as `const` fields and
inline arithmetic. There was no central rules class; a few tunables sat on the Unity
`UWSettings` ScriptableObject (lava damage, swim intervals, attack charge timing), and one
flag (`UWArmourProtection.ResistBlowsAddsArmour`) was pushed from UWSettings every frame.

*Since then (cleanup phase 10, 0.9.2):* the rule set exists (section 3.3), 248 rules. Of the
twenty values a modder would want first, most are in it; a few are still `const`:

| # | Value | In the rule set |
|---|---|---|
| 1 | Skill-check roll width 31 and the band cuts 2 / 15 / 28 | yes, `combat.skillCheck.*` |
| 2 | Player hit score: weapon skill + attack/2 + dex/7 | yes, `combat.player.*` |
| 3 | Damage: unarmed 4 + str/6 + 2/5 skill, armed str/9 + weapon, minimum 2 | yes, `combat.damage.*` |
| 4 | Critical multiplier (1 or 2 at even odds) | **no** |
| 5 | Flanking bonus 0..4 | **no** |
| 6 | Easy: +7 attack, creature damage halved | yes, `combat.player.easyAttackBonus`, `critter.easyHalvesDamage` |
| 7 | Creature swing-charge table | yes, `critter.chargeTable` |
| 8 | Strong-creature bonuses (7+d6, 4+d12, armour x5/3) | yes, `critter.strong.*` |
| 9 | Armour value, Resist Blows switch | the switch, `combat.resistBlowsAddsArmour` (no longer on UWSettings); the formula is a shape |
| 10 | Casting check, mana per circle, circle gate, cast pause | yes, `magic.castingCheck.*`, `magic.manaPerCircle`, `magic.castDelay.*`; the circle gate **no** |
| 11 | Spell durations 3d24 / 2d8 / 2d3 ticks; the 21-second tick | the durations, `magic.duration.*`; the tick **no** |
| 12 | Experience: 500 per unit, 3000 per skill point, level table, 96000 gate, depth penalty | yes, `experience.*` (`experience.awardCap` is the gate); the depth penalty **no** |
| 13 | Kill XP 4*base + 2d(base) | yes, `experience.kill.*`, `critter.strong.experience*` |
| 14 | Max HP, max mana, carry weight | max HP `experience.maxHealth.*`, carry `inventory.carryPerStrength`; max mana **no** |
| 15 | Hunger, healing, mana regeneration | yes, `vitals.hunger.*`, `vitals.healing.*`, `vitals.manaRegeneration.*` |
| 16 | Sleep hours, recovery shares, ambush chance, night hunger | yes, `sleep.*` |
| 17 | Respawn every 24 ticks, 1 in 4 | the chance, `respawn.chanceInN`; the interval **no** |
| 18 | Stack limit 512 and the quality band | **no** (the stack cap 511 is a deviation that replaced the path) |
| 19 | Light burn counter, oil fill 32/63, 180 ticks per sleeping hour | `lights.oilFlaskFill`, `lights.sleepTicksPerHour`, `lights.sources`; the burn counter **no** |
| 20 | Trade threshold, patience and appraisal noise, the 3/2 like bonus, Charm | yes, `trade.*` |

Besides these: creature behaviour (`critter.melee.*`, `sense.*`, `wander.*`, `door.*`,
`withdraw.*`, `follow.*`, `talk.*`, `spell.*`, `missile.*`, `noise.*`), the player's motion
(`motion.*`), reach distances, meals, drinks, lava, swimming, the Void. The exact keys come
from `uwdump rules-template` (the release packages carry it as `Docs/rules-template.jsonc`);
that list is the reference, not this table. The Unity-side lava numbers moved into
`vitals.lava.*`, the attack charge step into `combat.player.chargeStepMilliseconds`; what stays
on UWSettings is presentation (the swim view's sway and roll, for example).

Twenty quirks of the original kept "as it plays" were listed as natural on/off switches. Four
became switches (`quirks.*`): toughness twice onto the body, the lit taper staying lit, one
lava-proof flier making lava costly on every flier's path, the group mantra spending its point
regardless. The XP gate is a number (`experience.awardCap`). The rest - Strengthen Door doing
nothing, the NW ambush spot, the repair loss, the shared burn counter, the drifting weight
total, 6-bit game variables, the Lore reset, the free skill point, the quality-0 damage trap,
the flank bonus in check and damage, Acrobat - need their second path written before they can
be a switch (`UWRulesQuirks` says so). Of the deliberate deviations three are switches
(`deviations.*`: conversation patches, orb mana aside only while the orb stands, the follower
landing where it can stand); the others replaced the original's path and would need it
written back first (`UWRulesDeviations`).

### 2.3 Behaviour tied to level numbers, tiles and ids

About 25 spots keyed on a dungeon level number; the Void index alone was written as five
separate literals with eight Unity-side consumers. Ten fixed coordinates and about 25 fixed
object / NPC / conversation ids sat in the code, two of them firing without a level guard
(burial trigger, Tybal's prison cleanup). Already data-driven: stairs, no-magic tiles, water
and lava, window textures, conversation memory sizes, gravestones, camera traps, text traps.

*Since then:* the level numbers live in one place, `UWLevelIds` (zero-based), and the six the
story knows are rules: `levels.newMoonstone`, `cupOfWonder`, `bullfrog`, `lair`, `volcano`,
`void`. The Void's consumers all read `UWRules.Current.Levels.Void` (the Unity side through
`UWEndgame.VoidLevelIndex`). The story's coordinates and ids are `story.*`
(`UWRulesStory`): talking-door conversation, conversation-trap whoami, emerald, cup, bullfrog
field and its variables, Tybal's prison, volcano centre and radius, burial trigger, Void
landing, start tile. Still open: the nine-level assumption (`UWLevArkLayout.LevelBlockCount`,
blocks 0-8, 18+j, 27+j, and the editor bake window), the 4-bit moonstone/tree level nibbles in
PLAYER.DAT (at most 15 levels without a format change), the maze's floor texture and the other
per-level facts that are no single number, and a level guard for the burial trigger and
Tybal's prison cleanup.

### 2.4 Logic still on the Unity side

The survey found pickup implemented three times, damage to the player applied from about nine
places without a pipeline, door use twice, creature casting bypassing `UWSpellCasting`, player
death polled per frame, lava/drowning/swim entirely Unity-side, six `UnityEngine.Random` sites in
game logic, and the item-use and conversation-function dispatch as hard-coded switches.

*Since then (cleanup phases 2 to 4), all of that is engine-free:*

- **Pickup**: `UWPickupRules` / `UWWorldPickup`, one road for every site.
- **Damage to the player**: `UWDamagePipeline` with `UWDamageRequest`; every site goes through
  it (through `IUWWorld.Damage` where it has a world). `UWPlayerVitals.Damaged(request,
  outcome)` reports amount, type and source. Damage to creatures and objects goes through
  `IUWWorld.Damage(target, UWBlow)`.
- **Doors**: `UWDoorRules` for the player and creatures. **Creature casting** goes through the
  same spell path as the player and the traps (0.8.3).
- **Death**: `UWDeathRules`, raised as `UWPlayerVitals.Died`, not polled.
- **Lava, drowning, swim check**: `UWPlayerTerrainRules` on the tick, numbers in the rule set.
  The respawn runs on the player tick. Left on real time: the wand cooldown
  (`UWItemUseHost`, `Time.time`).
- **Random numbers**: every game draws from its session's generator (`UWSessionRandom`, the
  original's rand); the `UnityEngine.Random` calls left are presentation only (screen shake
  jitter, hit sparks, light flicker, the Void creatures' drift).
- **Dispatch**: registration tables `UWTrapTable`, `UWConversationFunctionTable`,
  `UWItemUseTable`, `UWFoodTable`, plus `UWItemTargetRules` and `UWWorldUseRules`; a mod row
  would be one more registration.
- **Events** in the layer, about 27: on the vitals (`Damaged`, `Died`, `VitalsChanged`,
  `KillRewarded`, `MessageDue`, the tick events), the world edit (`ObjectPlaced`,
  `ObjectRemoved`, `ObjectMoved`, `LevelCleared`), the creatures (`GoalChanged`), the combat
  state, the inventory (`InventoryChanged`, `LightBurnedOut`, `ItemMerged`), the locks.
  `ICritterHost.VetoDeath` is a veto hook; `UWTrapRules.fRunTrap` brackets every trap.
- **Self-check**: `Tools/UWCheckLib` runs traps, spells, sleep and the shrine headless on a
  `FakeWorld`; the rule set has its own checks (`RulesChecks`, `RulesSwitchChecks`, the pin
  `Tools/UWSelfCheck/Pins/rules-original.json`).

### 2.5 Texts

The survey found about 750 phrase literals and 860 single words of the port's own text across
96 files, no localisation table, English grammar assembled in a dozen places, three drawing
paths (IMGUI, uGUI with Lexend Exa loaded in eleven places, the FONT*.SYS bitmap renderer), the
`(sbyte)` decoding bug that made bytes above 0x7F unreadable, and typed input filtered to ASCII
in four places.

*Since then (cleanup phase 8, 0.9.1 and 0.10.0), all of it is built:*

- The port's texts are one table: `UWText` with the English in `UWTextEnglish.*.cs` (about 870
  ids such as `setting.Language`, `grammar.look.you_see`), translated by `Languages/<code>.tsv`.
- The sentences the original assembled are frames (`grammar.*`, `msg.*`) with ICU-style
  `select` and `plural` (CLDR categories), filled by `IUWGrammar`: `UWGrammarEnglish`
  (byte-exact), `UWGrammarGerman` (in code), `UWGrammarGeneric` (driven by the header ids of a
  language file). Game strings that are pieces of a frame are listed in `UWTextFragments` and
  may carry slots.
- One drawing path: since cleanup 8b every window is drawn by the game's own interface kit;
  `UWInterfaceFont` is the one accessor of the modern and the interface font.
- `UWStringNode.CharSymbol` is read as a byte. The classic fonts draw `ä ö ü Ä Ö Ü ß` in the
  original's style (`UWFontGlyphBuilder`, own art); other letters come from a language's own
  font, per character (`UWFallbackGlyphs`, `UWLanguageFont`). Typed input takes every letter
  of the language's alphabet; `grammar.rule.ime` turns on the system's input method.
- `UWTextLayout` breaks lines between CJK characters with the usual no-break punctuation;
  `UWConversationTextRules` treats non-ASCII letters as letters for the typed-word matches.

## 3. Architecture

### 3.1 The mod folder (open)

```
<persistentDataPath>/Mods/
  <mod-id>/
    mod.json            id, name, version, author, loadOrder, requires, description, kind
    rules.json          overrides of the rule set (section 3.3) - the same format as today's file
    levels.json         the per-level table (section 3.4)
    lang/<code>.tsv           the port's own texts, the format of Languages/<code>.tsv
    lang/<code>.strings.tsv   game texts, the format of Languages/<code>.strings.tsv
    lang/<font files>         the fonts the language names (grammar.rule.font)
    data/               table patches: comobj.json, objects.json, combine.json, conversations.json
    pictures/           own pictures as PNG with palette indices (never the original's)
    scripts/            later: Lua
```

What exists today and has to fit in: the player's own `rules.json` in the game's folder beside
`settings.json` (`persistentDataPath/UW1`, `UWGameFolder`), and the folder `Languages` beside the
executable with `<code>.tsv`, `<code>.strings.tsv` and font files. Proposed: both stay as they
are - the player's rules.json applies last, over every mod; the `Languages` folder is the
"installed languages" source beside the mods. A mod of `"kind": "language"` adds its
`lang/` files to the Language row's list (the row already lists what `Languages` holds, see
`UWLanguage`); a mod of another kind may still bring texts for its own additions.

A mod is enabled per installation in `settings.json` (`EnabledMods`, ordered). The game menu
gets a "Mods" window on the interface kit listing the folder, with on/off and the load order;
changes take effect at the next session start (the rule set is applied at a session's start
already) or data rebuild (the data root is rebuilt when the effective mod set changes, as it
is rebuilt today when the DATA path or the game-text file changes).

Engine-free: `UWModCatalog` (scans the folder, parses manifests, resolves the order and
`requires`), `UWModSet` (the enabled, ordered list), both in `Assets/UWDataImport`. Unity
side: the folder path, the window, and `UWLevelLoader` handing the `UWModSet` to the data root
and the session.

### 3.2 Overlays on the game data

One place applies every overlay: `UWGameData`'s constructor, which already lays the game-text
pack over the strings before anything reads them. It gains the mod set; order = load order,
later mods win. The level data overlays run in the new-game path.

- **Strings: built as language files.** `UWGameTextPack` reads `Languages/<code>.strings.tsv`
  (block TAB index TAB text, UTF-8, the format of the higesho fork's `ja.tsv`; untranslated
  entries stay English; numbers unchanged, so the VM, traps, shrines and cutscenes need
  nothing). The template comes from the translator's own copy (`uwdump strings-template`),
  checked by `validate-tsv`, `fit`, `looks`, `typed-words`. Left for mods: a mod's
  `lang/<code>.strings.tsv` laid over the language's pack in load order, so a mod that adds a
  combination can name its prompt in every language it ships.
- **Tables (open)**: `UWCommonObjectProperties`, `UWObjectClassProperties`,
  `UWObjectProperties` and `UWObjectCombining` get `Patch(...)` entry points (set one field of
  one entry; add a combination rule). The JSON names the entry by object id and the field by
  its parsed name (mass, value, qualityClass, stack, vitality, armour[0..3],
  attacks[i].damage, ...). This is where the Reddit "more item interactions" lands: a mod adds
  `wood + pebbles -> arrows` as a CMB rule plus, if it wants a prompt, a string. The food table
  (`UWFoodTable`) and the light table (`UWLightSourceTable`) are data tables of the port and
  take patches the same way. New object TYPES are out of scope of this phase (section 6).
- **Conversations (open)**: `UWConversationPatches` becomes data-driven: the compiled-in
  `Patch[]` stays as the built-in set (on/off by `deviations.conversationPatches`), a mod's
  `data/conversations.json` adds entries of the same shape (slot, address, opcode, expected,
  replacement, appended). The guard (opcode and expected word must match) protects a different
  CNV.ARK as it does today. New conversation functions are one more row in
  `UWConversationFunctionTable`, once scripts exist.
- **Pictures (open)**: a mod's PNG is read as palette indices (nearest palette colour on
  import, warned when a colour is not in the palette) and replaces `UWTexture.PaletteIndices`
  of the named picture (`UWTextures.ReplaceIndices` exists). Frames, cursors and the generated
  art of the HUD builders stay as they are in the first step.
- **Levels (open)**: a mod's `levels/` may carry a complete LEV.ARK of its own making (a
  modder's level set, nothing of the original) which replaces the pristine one for a new game.
  The overlay route for the ORIGINAL levels is the randomizer (phase 5), which edits the
  in-memory model and lets the existing writers persist it.

Precedence: the mod set is part of the data root's identity, as the game-text file is today.

### 3.3 The rule set (built in 0.9.2)

What is built ([CLEANUP-PLAN.md](CLEANUP-PLAN.md) section 6, phase 10):

- `UWRules` with one C# class per domain: `Combat`, `Magic`, `Experience`, `Vitals`, `Sleep`,
  `Critter`, `Respawn`, `Motion`, `Trade`, `Inventory`, `Lights`, `Quirks`, `Deviations`,
  `Story`, `Levels` - 248 rules. The defaults stay in code: each field's initialiser is the
  const the rule class always had, and its `UWRule` attribute names that anchor, a one-line
  meaning and, where a formula needs it, a minimum and maximum.
- The set hangs on the session (`UWGameSession.Rules`); `UWRules.Current` is the transitional
  facade. `CreateOriginal()` builds the defaults; `UWGameSession.ApplyRulesFile` applies the
  player's `rules.json` (`UW1/rules.json` beside `settings.json`) at each session's start, from the defaults
  every time.
- Keys are flat and dotted, built by reflection from the classes
  (`combat.skillCheck.rollRange`); values are whole numbers, switches or tables of whole
  numbers. `UWRulesJson` is forgiving: an unknown key (with a "did you mean"), a wrong kind or
  a value out of range is reported and skipped; a malformed file leaves the original's rules.
  The report goes to the log and the debug panel (`UWDebugReport`).
- `uwdump rules-template` writes every key with its default and meaning (in the release
  packages as `Docs/rules-template.jsonc`), so the documentation cannot drift from the code.
- Self-check: the field defaults equal their anchors, `CreateOriginal()` serialises to the
  pinned `rules-original.json`, a file that sets every key is accepted, and each switch takes
  its other branch (`RulesChecks`, `RulesSwitchChecks`).

What is left:

- **Mods stack**: `ApplyRulesFile` takes one file; with the mod set it applies each enabled
  mod's `rules.json` in load order, then the player's own. The report names the file each
  applied key came from.
- **Difficulty**: `difficulty.skillCheckBonus.<skill>` (a flat bonus on one skill's checks),
  `difficulty.loreAlwaysIdentifiesAt`, and the like - new entries whose default is "off", read
  at the places where the check is made. The easy setting's two numbers exist already
  (`combat.player.easyAttackBonus`, `critter.easyHalvesDamage`). This is the Reddit
  "difficulty sliders" wish: a window on the interface kit shows a handful of them and writes
  the player's `rules.json`; a mod can set any.
- **The remaining quirks** as switches, each after its second path is written (section 2.2).
- **The save marker**: which rules were not the original's, so a save made with them says so.

### 3.4 The level table (partly built)

Built: the story's level numbers (`levels.*`) and coordinates and ids (`story.*`) are rules,
so a total conversion can repoint which level is the Void, where the volcano is, where the
game starts.

Open: a per-level table `UWLevelTable` with one row per level, defaults = the original's nine
levels, overridable by `levels.json`, for the facts that are no single number:

```json
{ "levels": {
    "7": { "manaDrainWhileOrbStands": true, "mazeFloorTexture": 12 },
    "8": { "talismansBurnInLava": true, "volcano": { "x": 32, "y": 32, "radius": 6 } },
    "9": { "void": true, "unlit": true, "noMagic": true, "noSleep": true,
           "cullsLandedObjects": true, "landing": { "x": 27, "y": 23 } } },
  "levelCount": 9 }
```

Rows carry: void, unlit, noMagic, noSleep, cullsLandedObjects, talismansBurnInLava, volcano,
manaDrain, mazeFloorTexture, bullfrogField, cup, windowFrame, the dream gate, and a level for
the burial trigger and Tybal's prison (today unguarded). Today's `levels.*` keys become the
defaults of these rows (the Void is the row with `void: true`); a `rules.json` that sets
`levels.void` keeps working.

`levelCount` is read by the data root instead of `UWLevArkLayout.LevelBlockCount` (blocks
0..n-1, 18+j, 27+j follow from it; the editor bake window too). The PLAYER.DAT nibbles limit a
mod to 15 levels unless it also opts out of DOS compatibility, which the manifest states
(`"saveFormat": "extended"`), and then the save marker says so.

### 3.5 The port's own texts and grammar (built in 0.10.0, differently from the plan)

The plan proposed the English text as the key (`UWText.T("Save game")`) and a `ui.txt` per
language; the build took ids (`setting.Language`) and a TSV per language, because the same
English stands for different things in different places and a translator needs the context.
The rest landed as planned or further:

- `UWText` with the English tables in `UWTextEnglish.*.cs`; `Languages/<code>.tsv` (id TAB
  text). `uwdump text-template <code> --from <old>` brings a file up to date after a release
  and keeps obsolete ids as comments. The pseudo-language (`-language pseudo`) finds clipping.
- Grammar: `IUWGrammar` with English (today's code, byte-exact), German, and a generic grammar
  read from header ids (`grammar.rule.*`, `grammar.article.*`, `grammar.phrase.*`) so a new
  language needs no code. Names in block 4 and the names built from blocks 1, 7, 10 carry a
  name format (`gender[article]:singular|case=form&plural&category=form`); frames use
  `select` and `plural` with the CLDR categories. Made-up examples in `Tools/Fixtures/i18n`.
- Typed words: `uwdump typed-words` lists every word compared with typed input; the translator
  keeps them consistent. Per-language word lists in a pack were not needed.
- The language switches at once from the Game window (`UWLanguage.Switch`), also the game's
  texts.

For mods: a mod may ship `lang/<code>.tsv` with ids of its own (a mod's window, its messages),
proposed with the mod id as their prefix. The language files stay the format of
[TRANSLATING.md](TRANSLATING.md).

### 3.6 Fonts (built in 0.10.0)

- The decoding is fixed (`CharSymbol` as a byte); the overlay is read as UTF-8 strings.
- `UWInterfaceFont` is the one accessor of the modern and interface fonts; `UWUiFonts`
  switches the classic texts to the modern font and back.
- A language names its fonts (`grammar.rule.font`): font files in `Languages` or installed
  fonts (`os:`). The classic pixel paths ask them per character for what the pixel font lacks
  and draw it at the pixel font's height, without grey (`UWFallbackGlyphs`) - the Exult-for-zh
  "HiRes Text" idea reduced to what the port needs. The modern fonts fall back to the `os:`
  entries (Unity's dynamic fonts cannot fall back to a file).
- German has own-drawn `ä ö ü Ä Ö Ü ß` in the original's style appended to the glyph tables
  (own art, in the repository).

Left: pixel fonts of their own for scripts beyond Latin in the classic look (a language
mod could bring them as a picture set once picture overlays exist), and the save side file for
names beyond Latin-1.

### 3.7 Hooks (later)

Events in the engine-free layer, raised by the rules, with a cancel flag where the original
has a veto: `UWEvents.BeforeCast / AfterCast`, `BeforeDamage / AfterDamage`, `ItemUsed`,
`TrapFired`, `LevelChanged`, `Slept`, `Traded`, `PickedUp / Dropped`, `CreatureDied`,
`PlayerDied`, `ConversationFunction`. A Lua runtime (MoonSharp, MIT, pure C#, works under
IL2CPP) subscribes to them from `scripts/*.lua`.

The seams for all of them exist now (section 2.4): `UWDamagePipeline` and
`UWPlayerVitals.Damaged` for damage, `UWPlayerVitals.Died`, the world edit's
`ObjectPlaced/Removed/Moved`, `UWTrapRules.fRunTrap`'s bracket, `ICritterHost.VetoDeath`, the
registration tables for item use and conversation functions, `UWPickupRules`, `UWDoorRules`,
the one spell path for player, traps and creatures. What is missing is `UWEvents` itself: one
public surface that gathers them, with "before" events that can cancel. The events are useful
for the self-check and the debug panel before any scripting exists.

## 4. Phases

Each phase ends buildable, exported and tested; nothing is half-wired across a release.

The cleanup is complete (0.9.2): the seams the phases start from - `UWGameData` and
`UWGameSession`, `IUWWorld` and `IUWPresentation`, the session generator, the registration
tables, the damage pipeline, the rule set - are in place. Marks: **done**, **partly**, **open**.

### Phase 0: foundations (small)

1. **open** - `UWModCatalog`, `UWModSet`, `mod.json`; the Mods folder; `EnabledMods` in
   settings; the "Mods" window (list, on/off, order, "open folder").
2. **partly** - the overlay seam: `UWGameData` lays the game-text file over the strings and
   keeps it in its identity; the mod set joins it.
3. **partly** - the seeded generator exists (`UWSessionRandom`, seeded from the clock at each
   new session); left: the seed shown (help window's stats tab) and kept in the save marker,
   and a way to start a game from a given seed. The barter's draw goes through the session.
4. **open** - the save marker in `UWR.json`: mod ids and versions, the seed, the rule keys that
   differ from the original, `saveFormat`.
5. **done** - `UWStringNode.CharSymbol` and the UTF-8 read path (0.10.0).

### Phase 1: the translation kit (done in 0.10.0)

1. **done** - `uwdump strings-template` writes the template from the player's copy; the game
   loads `Languages/<code>.strings.tsv`. Instead of a line-number switch and a reload key:
   `validate-tsv`, `looks`, `fit` and the live language switch.
2. **done** - `UWText` and `uwdump text-template`; every text of the port in the table.
3. **done** - the font accessor, the language fonts, `UWFallbackGlyphs`, the IME.
4. **done** - German as the first pack, the port's texts fully. Traditional Chinese (pmanyeh)
   is prepared (CJK breaker, fonts, IME), no pack yet.
5. **done** - the grammar (`IUWGrammar`, name formats, select and plural, the generic grammar).
6. **done** - the Translation Kit beside every release, [TRANSLATING.md](TRANSLATING.md).

Left from this phase, for the mod work: language files inside mods (section 3.1).

### Phase 2: the rule set and the level table (medium)

1. **partly** - `UWRules` with the defaults and `rules.json`, 248 rules domain by domain with
   the self-check pinning the defaults. Of the twenty first values still `const`: the critical
   multiplier, the flanking bonus, the circle gate, the 21-second tick, the depth penalty, max
   mana, the respawn interval, the stack limit and quality band, the burn counter (section 2.2).
2. **partly** - quirk and deviation switches: four quirks and three deviations; the rest each
   after its second path is written.
3. **open** - the difficulty entries and the in-game window for a few of them.
4. **partly** - the story's levels and ids are rules (`levels.*`, `story.*`); the Void literals
   are collapsed. Open: `UWLevelTable` and `levels.json`, `levelCount` in the data root and the
   editor, the level guards of the burial trigger and Tybal's prison.
5. **done** - the Unity-side tunables that are rules moved into the rule set (lava, attack
   charge, Resist Blows); the rest of UWSettings is presentation.
6. **partly** - `uwdump rules-template` (in the release packages); open: `levels-template`
   and a "Rules" page in Docs generated from the template.
7. **open** - several `rules.json` in load order (the mods', then the player's).

### Phase 3: table and picture overlays (medium)

1. **open** - setters on the four table classes; `data/comobj.json`, `objects.json`,
   `combine.json`; patches for `UWFoodTable` and `UWLightSourceTable`.
2. **open** - `UWConversationPatches` data-driven.
3. **open** - PNG pictures over `UWTextures`.
4. **open** - a validator for a mod folder (manifest, unknown keys, ids out of range, palette
   colours), as `uwdump validate-mod` beside `validate-tsv`, reusing `UWRulesJson`'s report.

### Phase 4: consolidation on the Unity side (done in the cleanup, phases 2 to 4)

1. **done** - damage pipeline: `UWDamagePipeline`, every site; `UWPlayerVitals.Damaged`
   carries request and outcome.
2. **done** - pickup / drop: `UWPickupRules`, `UWWorldPickup`.
3. **done** - door use: `UWDoorRules` for the player and creatures.
4. **done** - creature casting through the one spell path (0.8.3).
5. **done, one left** - lava, drowning, swim check in `UWPlayerTerrainRules` on the tick; the
   respawn on the player tick. Still on real time: the wand cooldown (`UWItemUseHost`).
6. **done** - player death as an event (`UWDeathRules`, `UWPlayerVitals.Died`).
7. **done** - registration tables (`UWTrapTable`, `UWConversationFunctionTable`,
   `UWItemUseTable`, `UWFoodTable`).
8. **done** - headless traps, spells, sleep and shrine in the self-check (`FakeWorld`).

### Phase 5: the randomizer (medium to large, open)

1. Simple tier at new-game time, from the seed: shuffle items within a level by class
   (weapons among weapons, food among food), creatures among creatures of similar level
   (table vitality), keys and quest objects (`story.*`) pinned in place; options in a
   `randomizer.json` of a mod or an in-game window. Edits the session's world before the
   first save (`UWWorldEdit`); the writers persist it; the DOS original can load the result.
   Needs phase 0 item 3 (the seed kept and enterable).
2. Full tier: a zone graph (zones, the keys that connect them: items, quests, spells) as a
   data file of the port's own facts, a placement that keeps the game solvable, challenge
   modes (no food, ranged only, "a golem on level 1 you can walk around"). The Reddit
   author mapped the zones already; the graph file is the handover point.

### Phase 6: scripting (large, decided later)

`UWEvents` over the existing seams (section 3.7), then MoonSharp with a sandboxed API (read
state, raise messages, spawn from the object table, cancel an event). Only after phases 0, 2
and 3 give it a mod folder and data to hold on to.

### Suggested order from here

1. Phase 0 items 1, 3, 4 and phase 2 item 7: the mod folder, the seed, the save marker, the
   rule files in load order. Small, and it turns today's single `rules.json` and the
   `Languages` folder into something a mod can ship.
2. Phase 2 item 3: the difficulty entries and their window - the most visible wish.
3. Phase 3 item 1: the table overlays (item combinations).
4. Phase 2 item 4 rest and phase 3 items 2-4.
5. Phase 5, then phase 6.

## 5. What a mod can and cannot do

Today, without a mod folder, a player can: change any of the 248 rules and the seven switches
in `rules.json` (including which level is the Void and where the story's places are), and
play the game in a language of the port's texts and the game's texts.

At the end of phase 3 a mod can: change any rule number or quirk, add a difficulty rule,
re-table the levels (which one is the Void, where the volcano is, how many there are up to
15), retune every object and creature, add item combinations, patch conversation bytecode,
replace pictures with its own, translate everything, ship a level set of its own making.

Cannot yet: add new object types (the 9-bit id and the 512-entry COMOBJ.DAT; ids 464-511
have no category and empty names in the English data, the code makes no claim they are
free), raise the per-level object limit (254 mobile + 768 static is the LEV.ARK format; a
"safeguard against unreferenced items" is a loader change we can make regardless), run
scripts.

## 6. Open decisions for the user

1. Mod folder location: `persistentDataPath/Mods` (default, beside `settings.json`; or per game,
   `UW1/Mods` beside `UW1/rules.json`) or beside the executable too (where `Languages` is).
2. ~~Whether the language packs for GAME texts are mods in the same folder.~~ Decided by the
   build (0.10.0): languages live in `Languages` beside the game, two files per code. Open
   instead: whether a mod may also bring a language (proposed: yes, `lang/` in the mod, listed
   in the Language row beside `Languages`).
3. Whether the rule-set window in the game shows all rules (a long inspector) or only the
   difficulty entries (proposed), with the rest file-only.
4. ~~Whether the deliberate deviations get switches at all.~~ Decided in phase 10: a switch
   where the original's branch is still in the code (three today); the others stay the port's
   own unless their original branch is written back.
5. Whether the remaining quirks of section 2.2 get their second path written so they become
   switches, or stay as they play.
6. New object types: extended save format (breaks DOS compatibility for that save) versus
   reusing ids 464-511 after checking them in the data; postponed.
7. Scripting language, when phase 6 comes: Lua via MoonSharp (MIT) is the proposal.

## 7. Work split

- Mechanical moves (constants into `UWRules`, template exporters, table setters): a fast model
  with the self-check as the net; one domain per run. The rule set showed the pattern works.
- Design-heavy pieces (the mod catalog and load order, the level table, the difficulty
  entries, the randomizer's placement, `UWEvents`): the strongest model, with the user's test
  in the game.
- Surveys and audits (which literals are left, which quirks have no second path, which
  `Random` sites remain): read-only agents, as for this plan.
