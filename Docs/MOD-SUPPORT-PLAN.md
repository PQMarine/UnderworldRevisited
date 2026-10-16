# Mod support: plan

Status: PLAN, nothing built (2026-10-11). Written from five read-only surveys of the code
(rule constants, level-bound behaviour, logic left on the Unity side, data load points, texts).
Line numbers were right on that day and will drift; the file names will not.

The plan covers four things that turn out to be one piece of infrastructure:

1. A **mod folder** with manifests, a load order and an in-game switch.
2. **Overlays** on the game's data: texts first (that is the translation kit), later the tables.
3. A **rule set** that holds the numbers and quirks now hard-coded, so a mod can change them.
4. **Hooks** for scripted mods, after the first three exist.

The Reddit wishes of 2026-10-06 (randomizer, more item combinations, difficulty sliders,
free level layout, object limit) map onto these layers; where they land is noted per phase.

## 1. Goals, limits, legal line

- A mod ships only its own content: rule files, language files, patch tables, its own
  pictures and scripts. Never a byte of the original game. Translated GAME texts are an
  adaptation of the rights holders' text and stay out of this repository; the translator
  publishes them separately (decided 2026-10-08). Translations of the port's own texts can
  live in the repository.
- Save games stay readable by the DOS original where a mod does not change the data format.
  A mod that does change it (more object types, more levels) gives up that compatibility,
  and the save carries a marker saying so.
- The engine-free layer (`Assets/UWDataImport`, no `UnityEngine`) is where mods attach.
  Nothing a mod can reach lives on a MonoBehaviour.
- English stays byte-exact. Every rule keeps the original's value as its default; a mod
  overrides, it never replaces the code path.
- Not planned: loading C# DLLs from mods (arbitrary code on the player's machine).

## 2. What the surveys found

### 2.1 Where the data is loaded

`DataImport` (`Assets/UWDataImport/DataImport.cs`) is the single root. Every loader takes
the DATA folder path and does `Path.Combine` + `File.ReadAllBytes`; there is no virtual
file layer. `UWLevelLoader.fLoadData` builds it once and keeps it in a static
(`msOSharedData`), rebuilding only when the path changes.

| Data | Class | Mutable after load | Overlay today |
|---|---|---|---|
| STRINGS.PAK | `UWStrings.Blocks` (dict of lists) | yes, fully public | in memory, trivial |
| LEV.ARK (levels, notes, automaps) | `DataImport.Levels`, `UWLevel.TileData`, `Masterlist` | yes | in memory; the writers persist it |
| CNV.ARK conversations | `UWConversations`, `Conversation.Code` (ushort[]) | code in place | `UWConversationPatches` patches a clone per slot, compiled in |
| COMOBJ.DAT | `UWCommonObjectProperties` (private `Entry[]`) | no | needs a setter |
| OBJECTS.DAT (weapons, armour, critters, lights, ranged) | `UWObjectClassProperties`, `UWObjectProperties` (private) | no | needs setters |
| CMB.DAT combinations | `UWObjectCombining` (`IReadOnlyList<Rule>`) | no | needs an add |
| *.GR / *.BYT pictures | `UWTextures` (public dicts, `ReplaceIndices`) | yes | in memory; a stray file in DATA throws |
| FONT*.SYS | `UWFont` (lazy, private glyphs) | no | file level |
| PALS, LIGHT, SHADES, XFER, TERRAIN, WEAPONS.DAT, SOUND, CUTS | private | no | file level |

Two facts shape the design: the GOG cache (`persistentDataPath/GameData/UW1`) is wiped and
rebuilt when `game.gog` changes, so a mod folder must live outside it; and the only seam
that catches every file is the DATA path string handed to `DataImport`, while in-memory
overlays catch the mutable structures right after the constructor.

Saves: `UWSavegameStore.Write` copies the pristine `DATA/LEV.ARK` into the slot on the first
save and `UWLevelWriter.BuildBlock` rewrites the object lists from the in-memory model, so a
world changed at new-game time is persisted by the existing writers. `SAVEn/UWR.json`
(`UWHelpNotes`) is a JSON sidecar the original ignores; it already carries a stamp of
PLAYER.DAT, so a "made with mods" marker fits there.

### 2.2 Hard-coded rule values

About 300 rule values and formulas sit in the engine-free layer as `const` fields and
inline arithmetic (1,593 `const` declarations in UWDataImport in all). There is no central
rules class; 28 static `UW*Rules` classes hold constants, a few tunables sit on the Unity
`UWSettings` ScriptableObject (lava damage, swim intervals, attack charge timing), and one
flag (`UWArmourProtection.ResistBlowsAddsArmour`) is pushed from UWSettings every frame.
Two tables are duplicated (`UWCombat.NpcSwingCharges` = `UWCritterRules.ChargeTable`;
the skill-check bands in `UWSkillCheck` and `UWCritterRules`).

The twenty values a modder would want first (file names; see the survey for lines):

| # | Value | Where |
|---|---|---|
| 1 | Skill-check roll width 31 and the band cuts 2 / 15 / 28 | UWSkillCheck, UWCritterRules |
| 2 | Player hit score: weapon skill + attack/2 + dex/7 | UWPlayerAttack |
| 3 | Damage: unarmed 4 + str/6 + 2/5 skill, armed str/9 + weapon, minimum 2, dice | UWCombat |
| 4 | Critical multiplier (1 or 2 at even odds) | UWCombat, UWCritterCombat |
| 5 | Flanking bonus 0..4 | UWCritterRules, UWPlayerAttack |
| 6 | Easy: +7 attack, creature damage halved | UWPlayerAttack, UWCritterCombat |
| 7 | Creature swing-charge table | UWCombat, UWCritterRules |
| 8 | Strong-creature bonuses (7+d6, 4+d12, armour x5/3) | UWCritterRules |
| 9 | Armour value ((prot * q) >> 6) + 1, Resist Blows switch | UWPlayerCritterRow, UWArmourProtection |
| 10 | Casting check Casting+5 vs 2*circle, mana circle*3, circle gate (level+1)/2, cast pause | UWSpellCasting, UWRunicMagic |
| 11 | Spell durations 3d24 / 2d8 / 2d3 ticks; the 21-second tick | UWSpellCasting, UWPlayerTick |
| 12 | Experience: 500 per unit, 3000 per skill point, level table, 96000 gate, depth penalty | UWExperience |
| 13 | Kill XP 4*base + 2d(base) | UWCritterRules |
| 14 | Max HP 30 + str*level/5, max mana, carry weight str*20 | UWExperience, UWPlayerData |
| 15 | Hunger 3..6 per block, healing Strength vs 15, mana regen Mana vs 10 | UWPlayerVitals |
| 16 | Sleep hours, recovery shares, ambush chance, night hunger | UWSleepRules |
| 17 | Respawn every 24 ticks, 1 in 4 | UWRespawnRules |
| 18 | Stack limit 512 and the quality band | UWInventoryModel |
| 19 | Light burn counter, oil fill 32/63, 180 ticks per sleeping hour | UWInventoryModel, UWObjectMechanics, UWSleepRules |
| 20 | Trade threshold, patience and appraisal noise, the 3/2 like bonus, Charm | UWConversationTrade |

Twenty quirks of the original kept "as it plays", each a natural on/off switch: toughness on
a ring twice onto the body (UWArmourProtection), blocked-blow sound from the attacker's row
(UWCritterCombat), one lava-proof creature disabling lava stops for all (UWCreatureMotion),
Strengthen Door doing nothing (UWMiscSpellRules), the NW ambush spot never fitting
(UWSleepRules), repair loss likelier with better skill (UWRepairRules), the XP gate above
96000 (UWExperience), the lit taper staying lit on the ground (UWObjectMechanics), the shared
burn counter and the drifting weight total (UWInventoryModel), 6-bit game variables
(UWGameVariables), Lore reset for world items only (UWLoreCheck), the free skill point not
for Strength skills (UWPlayerVitals), the group mantra spending its point regardless
(UWShrineRules), a quality-0 damage trap doing nothing (UWTrapRules), the goal-3 follower
(UWCritterBrain), the flank bonus in check and damage (UWPlayerAttack), Acrobat negating
impact damage (UWImpactRules), Resist Blows (switch exists). And the deliberate deviations:
stack cap 511, orb mana aside only while the orb stands, manual attributes, the three
conversation patches of slot 288, Gate Travel's search, the level-exit home tile.

### 2.3 Behaviour tied to level numbers, tiles and ids

About 25 spots key on a dungeon level number; the Void index alone is written as five
separate literals (`UWEndgameRules.VoidLevelIndex`, `UWPaletteRenderToggle`,
`UWSleepRules.VoidLevel` = 9, `UWMobileObjectMotion` = 9, and the Unity alias
`UWEndgame.VoidLevelIndex`) with eight Unity-side consumers. The others: volcano idx 7
(talismans burn in lava), Tybal's lair idx 6 (mana drain, orb mages stand off), maze idx 6
(the crown recolours floor texture 12), bullfrog idx 3, Cup of Wonder idx 2 (duplicated in
`UWShrineRules` and `UWInstrumentPlayer` with different bases), the window frame clamp at 6,
the second dream from level 2, the nine-level assumption in `DataImport` (blocks 0-8,
18+j, 27+j) and in the editor bake window, and the 4-bit moonstone/tree level nibbles in
PLAYER.DAT (at most 15 levels without a format change).

Ten fixed coordinates (burial trigger 54/52 on whatever level, volcano centre 32/32 radius
6, Void landing 27/23, Cup 24/45, Tybal's prison 23/56, bullfrog field 48..55, emerald
offset 4, lever base height 32, start tile 32/2) and about 25 fixed object / NPC /
conversation ids (the nine talismans, Garamon's bones and graves, the seven special-death
whoamis, the talk gate, the orb and its mages, the talking-door slot 25, master-list slot
251 for a_do 50, the a_do quality table, the emerald, the silver seed and tree (duplicated),
the moonstone, the ending effects, the Bronus book, void creatures by goal 0xB, the generic
conversation base 256). Two of them fire without a level guard (burial trigger, Tybal's
prison cleanup). Already data-driven: stairs, no-magic tiles, water and lava, window
textures, conversation memory sizes, gravestones, camera traps, text traps.

### 2.4 Logic still on the Unity side

The formulas are in the engine-free layer; what remains in `Assets/UWScripts` is
orchestration, but some of it decides outcomes and is duplicated:

- **Pickup** is implemented three times (`Interaction.TakeOntoPointer`,
  `Interaction.fTryPickUp`, `UWItemDrag.fTakeFromSource`) and the pick-up trigger fires in a
  fourth place (`UWInventory.RemoveWorldObject`).
- **Damage to the player** is applied from about nine places (UWCritter, UWProjectileFlight,
  UWTileBlast, the trap host, UWPlayerTerrain for lava and drowning, UWPlayerMovement,
  UWVoidEffects, the sleep host, UWDeath); there is no damage pipeline and
  `IUWVitalsHost.OnDamageRecorded()` carries no amount, type or source.
- **Door use** is implemented twice (Interaction and UWCritter). **Creature casting**
  bypasses `UWSpellCasting` entirely. **Class 7 and 11 target spells** are Unity-only by
  design. **Player death** polls `CurrentHP <= 0` in `UWDeath.Update`.
- **Lava damage, drowning and the swim check** are entirely Unity-side with their parameters
  on UWSettings; **the wand cooldown** and **the respawn timer** run on real time.
- **Random numbers**: `UWRandom` is a swappable facade that the game never seeds, and six
  places roll `UnityEngine.Random` instead (`UWCritter.Random`, the creature brain's own RNG;
  UWDeath; UWVoidEffects; UWContainerDamage; UWLevelLoader scatter; UWEndgame). So the
  creature AI is not seedable today; a randomizer's "same seed, same game" needs this fixed.
- **Item use dispatch** (`UWItemDrag.fTryUseItemOnTarget`, `Interaction.UseTarget`) and the
  conversation function table (`UWConversationSession.CallUnknown`, about 61 `case` labels)
  are hard-coded switches.
- The hosts are outbound callbacks; the layer has five C# events in all
  (`InventoryChanged`, `LightBurnedOut`, `CarriedLightChanged`, `ItemMerged`,
  `UWLockState.Changed`). The best existing seam is `UWTrapRules.fRunTrap`, which already
  brackets every trap for the trap log. `ICritterHost.VetoDeath` is an existing veto hook.
- `Core/UWDataImport.csproj` (netstandard2.1, no Unity) plus `Tools/UWSelfCheck` and
  `Tools/UWDump` prove the layer is engine-free on every build. UWSelfCheck has fake hosts
  for critters, motion, item use, conversation, vitals and equipment, none for spells, traps,
  sleep and the shrine.

### 2.5 Texts

About 750 phrase literals and 860 single words across 96 files of the port's own text
(menus, help window, modern UI, layout editor, messages), the biggest in `UWHelpControls`,
`UWHelpWindow`, `UWSetupMenu`, `UWHelpContent`, `UWKeyBindings`, `UWModernLayoutEditor`.
No localisation table exists; the only central tables are English constants
(`UWHelpContent`, `UWModernLayout.Names`, `UWRunicMagic.RuneNames`, `UWSound.MusicTrackNames`,
`UWModernBags.msMenuLabels`, `UWGamepad.msNames`).

Sentence assembly with English grammar is concentrated in `UWObjectDescriptionFormatter`
("a_"/"an", "&" plural, "You see "), `UWItemDescriptions` (condition before the name, "of",
"with N full charges", "belonging to", "named"), `UWEquipmentWear` (" was"/" were"),
`UWTrapDisarming`, `UWRepairRules`, `UWStatusReport` (compass sentences), `UWPlayerVitals`
(level-up), and on the Unity side `UWModernPanel` ("1 point to spend"), `UWModernRunePanel`
("Circle N - M mana", "x of y"), `UWHelpWindow`, `UWModernConversation` ("'s goods").
Typed words are matched by `UWConversationSession` (`compare`, `contains`, ASCII-only
boundaries in `UWConversationTextRules`) and `UWShrineRules.fFindMantra`.

Three drawing paths: IMGUI with Liberation Sans (`UWInterfaceFont.Font`; menu bar, help
window, layout editor, in-game menu); legacy uGUI `Text` with Lexend Exa loaded in eleven
separate places and switched per frame by `UWUiFonts` between the modern font and a Unity
font built from FONT5X6P; and `UWFontRenderer.RenderLines`, which blits FONT*.SYS glyphs
to textures (conversation, message log, scroll, map, panel, cutscenes, character creation).
No TextMeshPro, no fallback fonts on the assets. `UWTextLayout.WrapText` breaks at spaces
only.

The decoding issue: `UWStringNode.CharSymbol = (sbyte)` makes a byte of 0x80-0xFF negative,
`UWStringBlock` casts it to a char of U+FF80 and up, and `UWFont.TryGetGlyph` fails for any
index beyond the glyph count, so `UWFontRenderer` skips it silently. The six FONT*.SYS hold
glyphs 32-126 at most (FONT4X5P has no lowercase, FONTBUTN ends at 93). The English
STRINGS.PAK has no byte above 0x7F, so the bug is invisible today. Typed input is filtered
to 0x20-0x7E in four places, save names too. Culture handling is mostly invariant already;
a handful of `ToString("0.#")` calls and `ToUpper()` without a culture remain.

## 3. Architecture

### 3.1 The mod folder

```
<persistentDataPath>/Mods/
  <mod-id>/
    mod.json            id, name, version, author, loadOrder, requires, description
    rules.json          overrides of the rule set (section 3.3)
    levels.json         overrides of the level table (section 3.4)
    strings/            game-text overlay: strings.txt (section 3.2)
    lang/<code>/        the port's own texts: ui.txt, and a font file if the language needs one
    data/               table patches: comobj.json, objects.json, combine.json
    pictures/           own pictures as PNG with palette indices (never the original's)
    scripts/            later: Lua
```

Language packs are mods whose manifest says `"kind": "language"`; the translator's kit
produces one. A mod is enabled per installation in `settings.json` (`EnabledMods`, ordered).
The game menu gets a "Mods" page listing the folder, with on/off and the load order; changes
take effect at the next game start or level rebuild (the data root is rebuilt when the
effective mod set changes, exactly as it is rebuilt when the DATA path changes).

Engine-free: `UWModCatalog` (scans the folder, parses manifests, resolves the order and
`requires`), `UWModSet` (the enabled, ordered list), both in `Assets/UWDataImport`. Unity
side: the folder path (`persistentDataPath`), the menu page, and `UWLevelLoader` handing
the `UWModSet` to the data root.

### 3.2 Overlays on the game data

One place applies every overlay: `DataImport` gains `ApplyMods(UWModSet)` called right after
`fLoadStaticData`, before the atlases are built (`UWLevelLoader.fPrepareShared`), and again
in the new-game path for level data. Order = mod load order; later mods win.

- **Strings** (layer 1 of the translation design): `Languages/<code>/strings.txt` becomes
  `strings/strings.txt` inside the mod, format `BBBB:NNN = text` (block hex, the project's
  usual +1 numbering), UTF-8. Applied over `UWStrings.Blocks`; untranslated entries stay
  English; numbers unchanged so the VM, traps, shrines and cutscenes need nothing. The
  template comes from the translator's own copy via `Tools/UWDump strings-export` (UWDump
  already has `strings`); the game gets a debug switch that shows each line's number and a
  key to reload the file.
- **Tables**: `UWCommonObjectProperties`, `UWObjectClassProperties`, `UWObjectProperties`
  and `UWObjectCombining` get `Patch(...)` entry points (set one field of one entry; add a
  combination rule). The JSON names the entry by object id and the field by its parsed name
  (mass, value, qualityClass, stack, vitality, armour[0..3], attacks[i].damage, ...). This
  is where the Reddit "more item interactions" lands: a mod adds `wood + pebbles -> arrows`
  as a CMB rule plus, if it wants a prompt, a string. New object TYPES are out of scope of
  this phase (section 6).
- **Conversations**: `UWConversationPatches` becomes data-driven: the compiled-in `Patch[]`
  stays as the built-in set, a mod's `data/conversations.json` adds entries of the same
  shape (slot, address, opcode, expected, replacement, appended). The guard (opcode and
  expected word must match) protects a different CNV.ARK as it does today.
- **Pictures**: a mod's PNG is read as palette indices (nearest palette colour on import,
  warned when a colour is not in the palette) and replaces `UWTexture.PaletteIndices` of
  the named picture (`UWTextures.ReplaceIndices` exists). Frames, cursors and the modern UI's
  generated art stay as they are in the first step.
- **Levels**: a mod's `levels/` may carry a complete LEV.ARK of its own making (a modder's
  level set, nothing of the original) which replaces the pristine one for a new game. The
  overlay route for the ORIGINAL levels is the randomizer (section 4, phase 5), which edits
  the in-memory model and lets the existing writers persist it.

Precedence: the mod set is part of the data root's identity; `UWLevelLoader` rebuilds the
root when it changes.

### 3.3 The rule set

A single engine-free class `UWRules` with one instance `UWRules.Current`, read by every rule
class instead of its `const`. It is built from the defaults (the original's values, kept in
code as the single source of truth) and the enabled mods' `rules.json` applied in order.
`UWRandom.Use` is the precedent for a swappable static service; `UWRules.Current` follows it.

Three groups of entries:

- **Numbers and tables**: the twenty of section 2.2 first, then the rest of the survey's
  inventory domain by domain. Names are dotted paths that mirror the classes:
  `combat.skillCheck.rollRange`, `combat.player.hitScore.dexDivisor`, `magic.manaPerCircle`,
  `experience.perSkillPoint`, `sleep.hoursFirst.base`, `respawn.chanceInN`, ...
- **Quirk switches**: `quirks.ringToughnessTwiceOnBody`, `quirks.lavaProofDisablesLavaStops`,
  `quirks.strengthenDoorDoesNothing`, `quirks.xpGateAbove96000`, `quirks.taperStaysLit`, and
  the rest of section 2.2; every one defaults to the original's behaviour. The deliberate
  deviations get switches the other way round (`deviations.stackCap511`, default on).
- **Difficulty**: `difficulty.skillCheckBonus.<skill>` (a flat bonus on one skill's checks),
  `difficulty.loreAlwaysIdentifiesAt` (30 by default off), `difficulty.easy.attackBonus`,
  `difficulty.easy.halveDamage`. This is the Reddit "difficulty sliders" wish: the in-game
  Graphics-menu style page shows a handful of them; a mod can set any.

Format: JSON, flat keys, only the overridden ones:

```json
{ "combat.skillCheck.rollRange": 31,
  "magic.castingCheck.bonus": 10,
  "quirks.ringToughnessTwiceOnBody": false,
  "difficulty.loreAlwaysIdentifiesAt": 30 }
```

Unknown keys are reported, not fatal. A `Tools/UWDump rules-template` writes the full list
with defaults and one-line meanings, generated from the defaults class, so the
documentation cannot drift from the code.

UWSelfCheck gains a check that every `const` moved into the rule set still yields the
original's numbers at the defaults (the existing checks already pin many of them), and a
check that a rules.json with every key set to a different value is accepted.

The Unity-side tunables that are rules (`UWSettings.LavaDamage*`, `Swim*`,
`AttackCharge*`, `VoidEffectTicksPerSecond`, `ResistBlowsAddsArmour`) move into the rule
set; the ScriptableObject keeps rendering and debug fields only.

### 3.4 The level table

A per-level table replaces the level-number literals: `UWLevelTable` with one row per
level, defaults = the original's nine levels, overridable by `levels.json`:

```json
{ "levels": {
    "7": { "manaDrainWhileOrbStands": true, "mazeFloorTexture": 12 },
    "8": { "talismansBurnInLava": true, "volcano": { "x": 32, "y": 32, "radius": 6 } },
    "9": { "void": true, "unlit": true, "noMagic": true, "noSleep": true,
           "cullsLandedObjects": true, "landing": { "x": 27, "y": 23 } } },
  "levelCount": 9 }
```

Rows carry: void, unlit, noMagic, noSleep, cullsLandedObjects, talismansBurnInLava,
volcano, manaDrain, mazeFloorTexture, bullfrogField, cup (tile, level), windowFrame, the
dream gate, start tile and level, the burial trigger tile and Tybal's prison tile (today
unguarded by level: the table gives them one). The five Void literals collapse into
`UWLevelTable.Get(level).IsVoid`; the Unity consumers read the same row.

`levelCount` is read by `DataImport` instead of the literal 9 (blocks 0..n-1, 18+j, 27+j
follow from it; the editor bake window too). The PLAYER.DAT nibbles limit a mod to 15
levels unless it also opts out of DOS compatibility, which the manifest states
(`"saveFormat": "extended"`), and then the save marker says so.

Fixed object and NPC ids (section 2.3, class C) move into `UWRules` under `story.*`
(`story.talismans`, `story.cupObject`, `story.silverSeed`, `story.orbMageKind`,
`story.talkingDoorSlot`, `story.conversationTrapNpcIndex`, ...). They are data, not code,
once they sit there; a total-conversion mod can repoint them.

### 3.5 The port's own texts (layer 3) and grammar (layer 2)

- `UWText.T("Save game")` and `UWText.F("Level {0}", n)` in `Assets/UWDataImport`: the
  English text is the key, the fallback is the key, lookups go to the active language pack's
  `lang/<code>/ui.txt` (`key = text`, UTF-8). An editor script (and `Tools/UWDump
  ui-template`) collects every `T(...)` into the template and marks what is new since the
  last export. About 750 phrases to wrap; the big tables (`UWHelpContent`, `UWHelpControls`,
  `UWKeyBindings`, the menu arrays) go first, the modern UI next, the messages last.
- `IUWGrammar` with `UWGrammarEnglish` = today's code moved unchanged (byte-exact English),
  behind: `ItemName(raw, count, qualityWord, enchantment)`, `LookMessage(...)`,
  `WasDestroyed(name)`, `PointsToSpend(n)`, `Possessive(name)`, `Ordinal(n)`. The name format
  per language lives in that language's `strings.txt` for block 4 (German `m:Dolch&Dolche`
  with gender, Chinese `把:劍` with classifier); `UWGrammarGerman` and `UWGrammarChinese`
  read it. The typed-word matches (`compare`, `contains`, the mantras) get per-language word
  lists in the pack and a boundary rule that treats non-ASCII letters as letters.
- Line breaking: `UWTextLayout.WrapText` gets a per-language breaker (CJK per character
  with the usual no-break punctuation; Latin at spaces and after hyphens).

### 3.6 Fonts

- Fix the decoding first: `UWStringNode.CharSymbol` as a byte, and the text pipeline reads
  the overlay as UTF-8 strings (the Huffman path is only for the original file).
- One font accessor: `UWFonts.Modern`, `UWFonts.Interface`, `UWFonts.Language` replace the
  eleven `Resources.Load<Font>("Fonts/LexendExa")` sites and `UWInterfaceFont`. The language
  pack may bring a TTF (`lang/<code>/font.ttf`, loaded with `Font` from file at run time)
  that `UWUiFonts.fApply` prefers for texts the active font does not cover (it already has
  `fCovers`). For CJK the modern UI uses it; the classic 320x200 pixel paths stay ASCII,
  and German gets own-drawn ä ö ü ß glyphs in the original's style appended to the glyph
  tables at indices 128+ (own art, in the repository).
- The bitmap path (`UWFontRenderer`) falls back per character to the language font drawn at
  the pixel font's height when a glyph is missing; that is the Exult-for-zh "HiRes Text"
  idea reduced to what the port needs.

### 3.7 Hooks (later)

Events in the engine-free layer, raised by the rules, with a cancel flag where the original
has a veto: `UWEvents.BeforeCast / AfterCast`, `BeforeDamage / AfterDamage` (needs the damage
pipeline, phase 4), `ItemUsed`, `TrapFired` (bracket exists in `UWTrapRules.fRunTrap`),
`LevelChanged`, `Slept`, `Traded`, `PickedUp / Dropped`, `CreatureDied` (`VetoDeath`
exists), `PlayerDied`, `ConversationFunction` (wrap `IHost.CallUnknown`). A Lua runtime
(MoonSharp, MIT, pure C#, works under IL2CPP) subscribes to them from `scripts/*.lua`.
Decided later; the events are useful for the self-check and the debug overlay before any
scripting exists.

## 4. Phases

Each phase ends buildable, exported and tested; nothing is half-wired across a release.

**State after phase 2 of the code cleanup (2026-10-14, [CLEANUP-PLAN.md](CLEANUP-PLAN.md)).**
Several seams this plan starts from now exist, so the phases below begin from them:

- `DataImport` is a facade over `UWGameData` (immutable, one per installation - the place for
  the data overlays of phases 0 and 3) and `UWGameSession` (one per running game: world,
  player, quests, variables, story flags, conversation memory, clocks, quirks, diagnostics),
  with `UWGameSession.Current` as a transitional static. Saves go through `UWSaveCodec`.
- Every game draws from its session's generator (`UWSessionRandom`, the original's own rand,
  seeded from the clock at each new session). Phase 0 item 3 therefore only needs the seed
  shown and kept (the save marker), not a new generator.
- Phase 4 items 2, 3 and 7 are largely done: `UWPickupRules`/`UWWorldPickup`, `UWDoorRules`,
  and the registration tables `UWTrapTable`, `UWDoActionTable`, `UWConversationFunctionTable`,
  `UWItemUseTable` and `UWFoodTable` (a mod row is one more registration).

### Phase 0: foundations (small)

1. `UWModCatalog`, `UWModSet`, `mod.json`; the Mods folder; `EnabledMods` in settings; the
   "Mods" menu page (list, on/off, order, "open folder").
2. `DataImport.ApplyMods(UWModSet)` with the strings overlay only; the root's identity
   includes the mod set.
3. Seed `UWRandom` at new game (from the clock, shown in the help window's stats tab) and
   replace the six `UnityEngine.Random` sites with `UWRandom`; keep `UWOriginalRandom` for
   barter. Without this the randomizer and any "same seed" test are impossible.
4. The save marker in `UWR.json`: mod ids and versions, the seed, `saveFormat`.
5. Fix `UWStringNode.CharSymbol` and the UTF-8 read path.

### Phase 1: the translation kit (small to medium)

1. `Tools/UWDump strings-export` writes the template (block 4 and the assembled fragments
   marked "do not translate yet"); the game loads `strings/strings.txt` from a language mod.
   Debug switch for line numbers, reload key.
2. `UWText.T/F` and the template export; wrap the big tables first.
3. The one font accessor, the language TTF, `UWUiFonts` preferring it, the bitmap fallback.
4. German as the first pack (the user can judge it): the port's own texts fully, game
   texts as far as the overlay carries them. pmanyeh's Traditional Chinese as the second,
   with the CJK breaker.
5. Layer 2 grammar (`IUWGrammar`, name formats) after the first pack shows where the
   assembled sentences break.

### Phase 2: the rule set and the level table (medium)

1. `UWRules` with the defaults class and `rules.json`; move the twenty first values, then the
   rest domain by domain (combat, magic, skills, vitals, inventory, traps, AI, trade,
   lights, motion). Every move is a mechanical `const` to `UWRules.Current.x` with the
   self-check pinning the default.
2. The quirk switches and the deviation switches.
3. The difficulty entries and the in-game page for a few of them.
4. `UWLevelTable` and `levels.json`; collapse the Void literals; `levelCount` into
   `DataImport` and the editor; the story ids into `story.*`.
5. Move the Unity-side tunables of UWSettings into the rule set.
6. `Tools/UWDump rules-template` and `levels-template`; a "Rules" section in Docs built
   from them.

### Phase 3: table and picture overlays (medium)

1. Setters on the four table classes; `data/comobj.json`, `objects.json`, `combine.json`.
2. `UWConversationPatches` data-driven.
3. PNG pictures over `UWTextures`.
4. A validator in UWSelfCheck for a mod folder (manifest, unknown keys, ids out of range,
   palette colours).

### Phase 4: consolidation on the Unity side (medium, spread over releases)

The moves that matter for mods, in order of value:

1. **Damage pipeline**: one engine-free `UWDamagePipeline.Apply(target, amount, type, source)`
   that every one of the nine sites goes through; `OnDamageRecorded` gets the arguments.
   Hooks and the debug overlay hang on it.
2. **Pickup / drop**: one engine-free `UWPickupRules.TryTake` (weight, theft, moonstone,
   trade weight, the pick-up trigger) called by the three Unity sites.
3. **Door use**: one `UWDoorRules.Use` for the player and creatures.
4. **Creature casting** through `UWSpellCasting` (a creature caster variant), so the cast
   hook sees it.
5. **Lava, drowning, swim check** into `UWPlayerTerrainRules` on the tick; the wand cooldown
   and the respawn timer on the game clock.
6. **Player death** as a rule event raised by `UWPlayerVitals` when HP reaches zero, not a
   per-frame poll.
7. The `switch` tables (item use on target, world-object use, conversation functions) as
   registration tables, so a mod can add a row.
8. Fake hosts in UWSelfCheck for spells, traps, sleep and the shrine, so the rules run
   headless.

### Phase 5: the randomizer (medium to large)

1. Simple tier at new-game time, from the seed: shuffle items within a level by class
   (weapons among weapons, food among food), creatures among creatures of similar level
   (table vitality), keys and quest objects (`story.*`) pinned in place; options in a
   `randomizer.json` of a mod or the in-game page. Edits `DataImport.Levels` before the
   first save; the writers persist it; the DOS original can load the result.
2. Full tier: a zone graph (zones, the keys that connect them: items, quests, spells) as a
   data file of the port's own facts, a placement that keeps the game solvable, challenge
   modes (no food, ranged only, "a golem on level 1 you can walk around"). The Reddit
   author mapped the zones already; the graph file is the handover point.

### Phase 6: scripting (large, decided later)

The events of section 3.7, then MoonSharp with a sandboxed API (read state, raise messages,
spawn from the object table, cancel an event). Only after phases 2 to 4 give it something
to hold on to.

## 5. What a mod can and cannot do at the end of phase 3

Can: change any rule number or quirk, add a difficulty rule, re-table the levels (which
one is the Void, where the volcano is, how many there are up to 15), retune every object and
creature, add item combinations, patch conversation bytecode, replace pictures with its
own, translate everything, ship a level set of its own making.

Cannot yet: add new object types (the 9-bit id and the 512-entry COMOBJ.DAT; ids 464-511
have no category and empty names in the English data, the code makes no claim they are
free), raise the per-level object limit (254 mobile + 768 static is the LEV.ARK format; a
"safeguard against unreferenced items" is a loader change we can make regardless), run
scripts.

## 6. Open decisions for the user

1. Mod folder location: `persistentDataPath/Mods` (default) or beside the executable too.
2. Whether the language packs for GAME texts (not the port's own texts) are mods in the same
   folder (proposed: yes, same manifest, `kind: language`).
3. Whether the rule-set page in the game shows all rules (a long inspector) or only the
   difficulty entries (proposed), with the rest file-only.
4. Whether the ten "deliberate deviations" get switches at all, or stay the port's own.
5. New object types: extended save format (breaks DOS compatibility for that save) versus
   reusing ids 464-511 after checking them in the data; postponed.
6. Scripting language, when phase 6 comes: Lua via MoonSharp (MIT) is the proposal.

## 7. Work split

- Mechanical moves (constants into `UWRules`, `const` to `Current.x`, `T(...)` wrapping,
  template exporters): a fast model with the self-check as the net; one domain per run.
- Design-heavy pieces (the damage pipeline, the level table, the grammar interface, the
  randomizer's placement): the strongest model, with the user's test in the game.
- Surveys and audits (which literals are left, which `Random` sites remain): read-only
  agents, as for this plan.
