# Cleanup plan

Status: plan, 2026-10-11. Nothing in this document has been implemented yet.

Line numbers refer to the sources at commit 1a35adc (2026-10-11) and will drift; the method and
class names are the stable reference.

## 0. Why, scope, method

Underworld Revisited has grown to about 155,000 lines of C# in 420 files (UWScripts 181 files /
93,000 lines, UWDataImport 192 files / 60,500 lines, Editor 14 / 2,200, Tools 35 / 10,000). The
mod-support plan (`Docs/MOD-SUPPORT-PLAN.md`) needs stable seams, and the first survey for it found
that many rules exist two to six times, on both sides of the engine boundary. Before any mod
support is built, the code is to be cleaned up:

- find everything that is implemented more than once and merge it, into the engine-free layer
  where possible;
- decide whether constants should live in editable files;
- replace long procedural code by object-oriented structure where that is clearer;
- read the big files and split them, or restructure them, where that helps.

Fourteen read-only reviews were run on 2026-10-11, one per area (Interaction and item drag; the
modern UI; creatures; level loader and spawner; player movement, character and damage; menus,
help, intro and settings; the HUD art builders; the engine-free layer and its constants; a
cross-cutting duplicate audit; self-check, tools and editor scripts; the classic HUD; the world
systems and host interfaces; rendering and audio; the input layer). This plan merges their
findings. The reviews are kept privately; this document is the public, maintained version.

Numbers in one line: 52 cross-cutting duplicate groups (about 3,700 duplicated lines, about 2,900
removable net), plus about 90 further area-local duplications; about 2,300 lines of dead code;
eleven long files between 2,000 and 5,600 lines; 16 host interfaces with about 240 members and
heavy overlap; about 1,600 `const` lines of which roughly 300 are original gameplay numbers; and
roughly 30 bugs or likely bugs found by comparing the copies with each other.

Total effort estimate: about 550 to 700 hours of implementation plus the user's test time. The
plan is ordered so that every phase leaves the game playable and releasable, and so that the first
phases pay for themselves (safety nets, bug fixes, dead code).

## 1. Ground rules for the whole cleanup

1. Behaviour first. Every step is either "no behaviour change" (verified by the safety nets of
   phase 0) or an explicit, separately committed behaviour fix checked against the original.
2. One step, one commit, self-check green, a short user test as listed. Steps marked "mechanical"
   need only a build and a smoke run.
3. The engine-free layer (`Assets/UWDataImport`, also compiled by `Core/UWDataImport.csproj`) is the
   home of every rule. Unity code only presents, reads input and implements host interfaces.
   New rule code is written there first and is covered by `Tools/UWSelfCheck`.
4. Keep instruction-faithful mirrors of original routines as they are (motion core, critter brain,
   conversation VM, trap chains). Long is acceptable there; a split would hurt the 1:1 mirror.
5. Keep file-format constants as `const` in code. Only original gameplay numbers become editable
   (section 6).
6. Nothing in this plan changes save-file bytes unless the step says so and the round-trip check
   of phase 0 is extended first.
7. Names the user has confirmed pixel by pixel, step by step or by ear (listed in section 11) are
   frozen; their code is moved verbatim, never rewritten.

## 2. Bugs and likely bugs found by comparing copies

These are independent of the restructuring and can be fixed early (phase 1). Each needs a check
against the original before the fix is committed. "Likely" means the reviewer saw a difference
between copies but could not confirm which copy is right.

Gameplay:

| # | Finding | Where |
|---|---|---|
| B1 | Silver tree id 459 is the string index (id+1); the tree is 0x1CA (458). The pass-through test skips the wrong id | `Interaction.cs:5042-5043` vs `UWSilverTreeRules.cs:25` |
| B2 | Creature brain casts in the Void; only two effect paths refuse. `UWCritter` tests the tile flag only, `UWEndgameRules.IsMagicBlocked` includes the Void | `UWCritter.cs:822-827, 2110, 2142` |
| B3 | `UWSpellHost.AreaTargetDistanceSquared` measures from the player even when `AreaCaster` is a creature | `UWSpellHost.cs:~495-503` |
| B4 | Door spike is honoured only by player melee; creature bash, missiles, blasts and spells hit a spiked door directly | `Interaction.cs:4044` vs `UWCritter.cs:1306, 1446`, `UWTileBlast`, `UWTargetSpell` |
| B5 | Trap, sleep and drowning damage pass no damage type, so type proof never applies; sleep lava is `None` while awake lava is `PlainFire`; sleep poison ignores Poison Resistance (awake tick uses it) | `UWTriggerSystem.cs:572`, `UWSleep.cs:98`, `UWPlayerTerrain.cs:765`, `UWSleepRules.cs:246, 591` |
| B6 | Combat music reacts to melee only; hit sound and easy-mode halving differ per damage site (17 sites, six behaviour differences) | table in section 7.5 |
| B7 | `UWInventoryModel.TryConsumeOne` decrements an enchanted stack; `UWItemDrag`'s `IsStackable` path does not. Six stack-count definitions differ | `UWInventoryModel.cs:601` vs `UWObjectMechanics.cs:1015` |
| B8 | `UWShrine` refuses a reward when the pointer is occupied, `UWSilverTree` loses the seed when hand and backpack are full; four "give to player" copies with four behaviours | `UWShrine.cs:76-96`, `UWSilverTree.cs:87-99`, `UWFishing.cs:83-90`, `UWInstrumentPlayer.cs:254-259` |
| B9 | Two clock units per minute: 0x3C00 (`UWGameClock.cs:35`) vs 15300 = 0x3BC4 (`UWConversationSession.cs:491`). Verify against UW.EXE; keep both only as named constants if both are right | |
| B10 | Gronk door decides on `IsHeadingClosed`, the door trap on `IsClosed` and skips moving doors; the open trigger fires only for player and creatures, not for trap, Gronk, burst or sleep | `UWTriggerSystem.cs:281-307, 503-556` |
| B11 | Sleep closes `DoorMover` only, so portcullises never close at night. Check whether the original closes them | `UWSleep.cs:107-129` |
| B12 | `UWProjectileWorld.PlaySoundAt`/`PlaySoundAtObject` drop the volume the core computes from mass | `UWProjectileWorld.cs:924, 964` |
| B13 | Object record flags: read mask `(w0 & 0x1F00) >> 9` (4 bits), write `Flags & 0x7`; the trade ORs 0x8 and writes `& 0xF` | `UWLevel.cs:576`, `UWLevelWriter.cs:237-268`, `UWConversationTrade.cs:515, 535` |
| B14 | `fCheckSummonRoll` adds a creature to level 9 that `fCheckObjectLimit` later counts ("every fresh level") | `Tools/UWSelfCheck/Program.cs:4209, 3784` |
| B15 | Modern pickup path (`UseTarget` -> `GetTarget`) checks use reach, not pickup reach; `pOPickableBehind` gets no reach check; `Interaction.cs:1343-1346` is unreachable | `Interaction.cs:1120, 1343-1346, 4333` |
| B16 | Classic "too heavy" message for bought goods goes to the log, which the conversation parchment hides | `UWItemDrag.cs:862, 1462-1464` |
| B17 | Classic target spell on an empty click consumes the spell (original); modern resolves only on an item. DECIDED 2026-10-22 (per user): both stay as they are - `UWPendingSpell` (5.14) holds the one rule with a per-scheme policy | `UWItemDrag.cs:392` vs `UWModernBags.cs:855-869` |
| B18 | Three crosshair definitions; `UseItemOnCrosshair` uses `Screen.width*0.5`, wrong in Classic Wide | `UWItemDrag.cs:1654` |
| B19 | `ApplyWardDamage` has no `IsDestroyed` guard | `UWTriggerSystem.cs:713-721` |
| B20 | `UWEndgame` and `UWContainerDamage` roll with `UnityEngine.Random` (also `UWCritter.cs:806-809`, `UWDeath.cs:102/176`, `UWVoidEffects.cs:79-117`, `UWLevelLoader.cs:1411-1432`), bypassing `UWRandom`; not reproducible in the self-check | |
| B21 | `UWCreatureMotion.msLavaFlagDropped`, the cast pause and the light-colour cache are static and leak across New Game and Load | `UWCreatureMotion.cs:62`, `UWSpellCasting.cs:108-110`, `UWObjectLightColour.cs:26` |
| B22 | A dying creature beyond the cull distance or during Freeze Time hits the 3 s fallback in `UWCritterRemains` and drops remains without the special-death check | `UWCritterRemains.cs:60-78`, `UWCritterBrain.cs:223-233` |
| B23 | Vetoed deaths (golem) save with HP 1 because `UWWorldCapture` clamps the `UWDamageable` value; HP lives in two stores | `UWWorldCapture.cs:101-102` |
| B24 | `UWProjectileWorld.mOStaticIndex`/`mOStaticByIndex` are never cleared and grow across levels | `UWProjectileWorld.cs:46-48` |
| B25 | `UWPaletteEffects` never rebuilds its normal array when `RemasterHeightStrength` changes (`UWRemasterRenderer` does) | `UWPaletteEffects.cs:319-326` |

UI:

| # | Finding | Where |
|---|---|---|
| B26 | Map notes cannot be typed on Linux: `UWHudMap` never calls `UWTypedKeys.ReadTyped()` (the other four text entries do) | `UWHudMap.cs:1130-1166` |
| B27 | Modern scroll wraps at `PaperWidth + fWide()` but renders into a `PaperWidth` texture: Classic Wide lines are cut off | `UWModernScroll.cs:290` vs `:229` |
| B28 | `Interaction` pages long text by the classic 5 lines while the modern scroll shows 2 to 8 | `Interaction.cs:4478`, `UWModernScroll.cs:117-119` |
| B29 | Modern character page shows the closed picture of an open container (`Icon ?? Texture`); classic and `UWModernBags` use the open variant | `UWModernInventoryPage.cs:378` |
| B30 | `UWEyesDisplay` builds its picture without `UWColourVision`; `UWGameUI.fApplyWideFrame` builds the classic wide frame without the colour help and does not rebuild on `UWColourVision.Version` | `UWEyesDisplay.cs:208-258`, `UWGameUI.cs:1644-1665` |
| B31 | Modern conversation does not escape `<` in rich text; a typed answer with `<` breaks the layout | `UWModernConversation.cs:920-968` |
| B32 | Setup menu: Controls and Colours dialogs can both be open; the Motion bar item never clears the palette dialog; missing braces at `UWSetupMenu.cs:1041-1043` run `mbPaletteEffectsDialogOpen = false` every frame | `UWSetupMenu.cs:320, 498, 803, 1041` |
| B33 | Texture leaks: stats lines per HP tick, map level number, inventory and rune icons, conversation sprites, dragon instances never cleared | `UWHudPanel.cs:1058-1067`, `UWHudMap.cs:310-334`, `UWHudInventory.cs:186-685`, `UWHudRunes.cs:314, 542`, `UWConversationScreen.cs:1530-1537`, `UWHudDragons.cs:77, 118` |
| B34 | `UWItemDrag.fTryResolveTargetSpellInInventory` reads `Mouse.current.position` instead of its warp-aware `fPointerPosition()` | `UWItemDrag.cs:~395` |
| B35 | `UWSweepMaskDriver` rebuilds the sweep mask every frame although the comment says "after the camera moved" (4225 `Atan2` calls and a texture upload per frame) | `UWSweepMaskDriver.cs:35-69` |
| B36 | Self-check: one exception in a non-loader group aborts the whole run; 20 data-free groups are skipped when the data load fails; seeds 31 and 47 are never reset | `Tools/UWSelfCheck/Program.cs:53-115` |

## 3. Dead code

About 2,300 lines can be deleted with no behaviour change (each deletion needs the test named in
the phase table):

- The whole old `CharacterController` movement path in `UWPlayerMovement.cs` (about 1,250 lines:
  gravity, fall, flow, hover, knockback, old velocity builders, `OnControllerColliderHit`,
  `fReportLanding/Impact/WallBump`, `fTryJump`, `fUpdateHover`, `fGetModernVelocity`,
  `fGetOriginalVelocity`), plus `UWPlayerTerrain.fUpdateHeightAboveFloor/fUpdateSpeed/fUpdateFlow`,
  `UWProjectileFlight.fPushPlayerBack`, `UWSettings` swim/flow/lava-speed fields, and
  `UWCharacter.ApplyDamageKeepingMinimum`. The old path still runs while no motion can be built
  (main menu, intro); it needs a "no world: stand still" branch first.
- `UWThrownItemFlight.cs` (82 lines), `UWLevelLoader.DropObjectAt`, `TrySnapOntoCarryingTile`,
  `DropThrownItemAt` (1919-2099), the flight branch of `fSpawnObjectAt` and the talisman branch
  2347-2357, `mfThrowFlight*`, `mfImpactHoverSeconds`, `SplashAt`, `GetSubTilePos`,
  `LastSpawnRefusedForSlot`, the `pfDelay` waits in `UWEndgame` (about 330 lines). The
  `pOFlightStartPosition` parameter is never non-null.
- `UWCombat.NpcSwingCharges/GetNpcCharge/MaxNpcSwingChargeIndex` (duplicate of
  `UWCritterRules.ChargeTable`, no callers); `UWCritterRules.CritThreshold/HitThreshold/
  MissThreshold` (duplicate of `UWSkillCheck` bands); `UWCombat.NeutralCharge` vs
  `UWCritterRules.MissileCharge`.
- `UWCritter`: `fSetPathRules`, `fTryGetTile`, `fGetFloorHeight`, `fGetTargetWorldPosition` and
  `mOPlayerBody`, `Get/SetFacingDegrees`, `StraightLineMaxTiles`, the animator branch in
  `Initialise` (464-467), the unused `pOTarget/pfRange` of `fLaunchProjectile`;
  `UWCritterRules.IsFacing/IsFacedBy` (no callers; or wire them in at `UWCritterBrain.cs:742, 1440`).
- `Interaction`: `IsPickupModeActive/IsUseModeActive/IsLookatModeActive`, `NightstandObjectId`,
  `msWorldPressChunk`, `Interaction.cs:1343-1346`; `UWItemDrag.fConsumeItem` world branch (1931).
- `UWSpellHost.cs:21-31` boulder constants, `UWTriggerSystem.cs:873-876` and `UWPlayerThrow.cs:71-73`
  arrow ids, `UWPlayerThrow.fIsWeightless`, `UWEndgame` constants 61/65/68/73,
  `IUWSpellHost.SpawnObjectInTile` and `CurrentLevelIndex` (no caller in the rules).
- `UWHudArt.FreePaperdollBody`; orphan summaries in `UWModernHudArt.cs:122-123, 140-141, 298-300`
  and about 20 orphaned or stacked doc comments in `Interaction.cs`, `UWItemDrag.cs`,
  `UWPlayerMovement.cs`, `UWItemUse.cs`, `UWSetupMenu.cs:866-875`; stale texture names
  ("UWGameUI.cs:2384" and similar in `UWHudPanel`, `UWHudMap`, `UWHudCompass`, `UWHudPictures`).
- `UWHudInventory.mArmorHitZones` and the zone floats (880-897, only null-checked);
  `UWHudMessageLog.mMessageLogText` (old `Text`, built and blanked every frame).
- `UWControls.ToggleCombat` (no bindings, still polled); `UWSoundPlayer.PlayAt`; `UWHudDragons`
  and `UWEndgame` forwarded constants that no one reads; `UWSettings.DisplayAs4By3` (always true
  since 2026-10-10); `UWSetupMenu.MenuEnum.Controls`.
- Editor scripts: ten of the fourteen `Assets/Editor` files (1,379 lines) are one-off dumps with
  hard-coded constants; they are replaced by UWDump commands (section 9.3) and then deleted.
- Comments that still describe the removed F6 render toggle: `UWPaletteRenderToggle.cs:9-10, 31`,
  `UWShadePalette.cs:10`, `UWSettings.cs:127-129`, `UWLevelLoader.cs:557`.
- `UWModernHud.UWFixedScenery`-style copies: the 30-line "V comes from the absolute height" comment
  appears twice verbatim in `UWLevelMeshBuilder.cs:443-475, 568-600`.

## 4. Duplications and their single home

The cross-cutting audit found 52 groups; the area reviews found about 90 more inside their areas.
This section lists every group that touches a rule or a visible behaviour, with its sites and the
class that becomes the single home. "EF" marks an engine-free home. Pure plumbing duplicates
(widget factories, texture tails) are in section 8.

### 4.1 Objects, ids, counts, names

| Group | Sites today | Single home |
|---|---|---|
| Creature id range 0x40..0x7F, nine forms | `UWObject.cs:213`, `UWObjectMechanics.cs:550`, `UWObjectClassProperties.cs:504`, `UWTrapRules.cs:899`, `UWConversationTrade.cs:1407`, `UWMotionCore.cs:258`, `UWObjectLimitRules.cs:71`, `UWLevelWriter.cs:77, 178, 527`, `UWSummonSpellRules.cs:41`, `Interaction.cs:157`, `GetCategory()==Monsters` at four more | EF `UWObjectIds.IsCreature(id)`, `Major(id)`, `Minor(id)` |
| Major/minor class bit tests (`ID >> 6` 17 times, `(ID>>4)&3` 8 times) | 12 files | EF `UWObjectIds` |
| `UWObject.GetCategory` 88-line if-chain overlapping `*FirstId`, `TrapMajorClass`, `DoorMajorClass`, light ranges | `UWObject.cs:203-290`, `UWObjectClassProperties.cs:498-528`, `UWObjectMechanics.cs:559, 1153-1185` | EF 32-entry table by `id >> 4` in `UWObjectIds` |
| Door, trigger, bones, instrument, open-container-picture predicates | `UWEquipmentWear.cs:44`, `UWWorldCapture.cs:23`, `UWObjectSpawner.cs:2430`, `UWLevel.cs:693`, `UWTile.cs:171`, `UWTileTraverse.cs:126`, `UWCritter.cs:1497`; triggers in 5 files; bones, instrument, open picture in 2 each | EF `UWObjectIds.IsDoor/IsOpenDoor/IsSecretDoor/IsTrigger/IsBones/IsInstrument/HasOpenPicture` |
| Object ids declared twice or more (lantern/torch/candle, bedroll, moonstone, seed, tree, cup, rune bag, spell object, doors, Key of Truth, boulders, food, debris, bones, stew, bridge, talismans, Tyball and Garamon whoami, player object 127 vs 63) | section 3 of the duplicate audit; about 40 constants in 25 files | EF `UWObjectIds` (one name per id, with the string index derived as id+1) |
| Level indices (Void 8/9, volcano, lair, cup level 2/3, bullfrog, new moonstone) | `UWEndgameRules`, `UWPaletteRenderToggle:611`, `UWSleepRules:138`, `UWMobileObjectMotion:309`, `UWMazeNavigation`, `UWTybalOrbRules`, `UWInstrumentPlayer:48`, `UWShrineRules:59`, `UWPlayerData:697` | EF `UWLevelIds` (0-based, with `Number(i)` for the 1-based form); quest coordinates in `UWQuestPlaces` |
| Message indices declared twice (96, 133, 187, 238, 248/249, 253, 272, 37, class base 0x18) | `UWItemDrag`, `UWModernBags*`, `Interaction`, `UWEndgame`, `UWItemApplications`, `UWLockRules`, `UWItemUse`, `UWObjectMechanics`, `UWTrapRules`, `UWShrineRules` | EF `UWStringIds` |
| Time units (PIT 256, clock minute, swim 0x60/0x50) | `UWPlayerTick`, `UWCritter:109`, `UWPlayerMovement:1287`, `UWGameClock`, `UWConversationSession`, `UWPlayerTerrain:53`, `UWReachRules:50` | EF `UWGameClock` and `UWPlayerTick` |
| Max quality 63 | `UWObjectMechanics.MaxQuality`, `UWPlayerCritterRow.MaxQuality`, `FullLightQuality`, `WardTrapAnyClass`, `& 0x3F` literals | EF `UWObjectIds.MaxQuality` |
| Stack count, stackable, consume one (six definitions, 0 mapped to 1 or not, enchanted or not) | `UWObjectMechanics.cs:1015`, `UWItemDescriptions.cs:304`, `UWHudInventory.cs:508`, `UWItemDrag.cs:777`, `UWModernBags.StackSplit.cs:47, 123`, `UWConversationScreen.cs:1363`, `UWConversationTrade.cs:741, 1482`, `UWInventoryModel.cs:202, 601, 937, 1009`, `UWInventoryWeight.cs:22, 73`, `UWObjectLimitRules.cs:314`, `UWLevelLoader.cs:1065`, `UWMobileObjectMotion.cs:376` | EF `UWStackRules.Count/IsStackable/TryTakeOne` (one definition, fixes B7) |
| Bare name, `&` plural, `_` article, "+1" string offset | `UWItemDescriptions.cs:236, 329`, `UWObjectDescriptionFormatter.cs:24-108`, `UWConversationSession.cs:600-624`, `UWTrapLog.cs:92`, `UWTrapDisarming.cs:159`, `UWItemDrag.cs:2329`, `Interaction.cs:1752, 1808, 2536, 5425, 5545`, `UWModernBags.cs:2425, 2539`, 13 `GetObjectDescription(ID+1)` | EF `UWText.ObjectName(id, plural, article)`, `General(i)`, `LevelMessage(i)`; sentence helpers in `UWItemMessages` |
| Safe string-block lookup (trim CR/LF, try/catch; two sites do not trim) | `DataImport.cs:420`, `Interaction.cs:1815`, `UWItemDescriptions.cs:342`, `UWRepairRules.cs:225`, `UWShrineRules.cs:428`, `UWConversationTrade.cs:1575`, `UWCharacterGeneration.cs:612`, `UWIntroPlayer.cs:334`, `UWModernHud.cs:2116`, `UWSpellHost.cs:193`, `UWItemDrag.cs:752`, `UWHudOptions.cs:294`; 24 raw `Strings.Blocks[` reads, about 55 `TrimEnd`, 30 bare `catch` | EF `UWText` |
| Fresh object creation (quality 0x28, quantity, `WasSpawned`, texture; two `NewObjectQuality` constants 0x28 and 0x3F; some sites skip quality or quantity) | `UWFishing.cs:122`, `UWInstrumentPlayer.cs:272`, `UWSilverTree.cs:110`, `UWProjectileWorld.cs:305, 483`, `UWShrine.cs:84`, `UWHudRunes.cs:248`, `UWLevelLoader.cs:998, 1629, 1690, 1747, 2628, 2636`, `UWConversationTrade.cs:1637`, `UWCritterLoot.cs:71`, `UWSummonSpellRules.cs:258`; 30 `GetTextureByType(OBJECTS` try/catch sites | EF `UWObjectFactory.Create(id, quantity, quality)` plus `UWObjectAppearance.Apply(obj, level)` (the per-id texture assignment now only in `UWLevel.load_object_list`), and a deep `Clone` |
| Give a new item to the player (four behaviours, B8) | `UWFishing`, `UWInstrumentPlayer`, `UWSilverTree`, `UWShrine` | EF `UWInventoryModel.GiveToPlayer(item, preferBags)` |
| Object chain walks (link / sp_link) with differing guards | `UWObjectMechanics.cs:987, 1063, 1115`, `UWObject.cs:174`, `UWPlayerData.cs:1239, 1758`, `UWLevelWriter.cs:385`, `UWTrapRules.cs:1676` | EF `UWObjectChain.Walk(start, records, maxSteps)` |
| Object record codec (8-byte layout read twice, written twice, partial parse once; B13) and NPC 19-byte extras (read `UWLevel.cs:721`, written `UWLevelWriter.cs:549`, raw words in `UWCritterRecord`) | `UWLevel.cs:569-588`, `UWPlayerData.cs:1772, 1132`, `UWLevelWriter.cs:237, 453` | EF `UWObjectRecordCodec`, `UWNpcRecordCodec` |
| LEV.ARK block layout, free lists, active-list swap-remove, block numbers 9/18/27/36 as literals | `UWLevel.cs:319-454`, `UWLevelWriter.cs:43-69, 469-519`, `DataImport.cs:131, 203, 213, 554-572` | EF `UWLevArkLayout`, `UWLevArkCodec` |
| Little-endian helpers (`fRead16` etc. in 5 files; host-endian `BitConverter` in 2) and the PLAYER.DAT XOR loop written twice | `UWPlayerData`, `UWLevelWriter`, `UWAdlibBank`, `UWFont`, `UWConversationGlobals`, `UWLevel`, `DataImport` | EF `UWBinary` |
| Dice (hand-rolled copies; `UWCombat.RollDamage` uses `Next(1,7)` instead of count + sum of `RNG(sides)`) | `UWRandom.RollDice`, `UWPlayerVitals.cs:807, 838`, `UWTrapRules.cs:394`, `UWSpellCasting.cs:427`, `UWCharacterGeneration.cs:577`, `UWCombat.cs:251` | EF `UWDice.Roll(count, sides)` |
| Restore share (`/16f` float vs `>>4` int), minimum-3-HP rule twice, bullfrog defaults twice, conversation `fArgument` twice | `UWPlayerVitals.cs:1418, 1506, 800-852`, `UWPlayerData.cs:707`, `UWObjectMechanics.cs:282`, `UWConversationSession.cs:439`, `UWConversationTrade.cs:573` | one integer function each |
| Clamp and rounding helpers (five private clamps, `Math.Round` banker's rounding in 7 files, `Math.Sqrt` vs `UWVanillaMath.Sqrt`) | `UWHudArt`, `UWHudLayout`, `UWOpl2`, `UWCritterCombat`, `UWTileQueries`, `UWUnits`, `UWCritterBrain.cs:808, 2058`, `UWSweepMask`, `UWPlayerMotion.cs:324` | EF `UWMath.Clamp`, `UWMath.RoundHalfEven/AwayFromZero` named explicitly; table arctangent for `FineHeadingOf` later (section 10, mirror hygiene) |

### 4.2 Coordinates and conversions

| Group | Sites today | Single home |
|---|---|---|
| World to tile (Round clamped vs Floor unclamped vs inline), world to eighth, eighth to world (+3.75 / +3.85 / +1.0), height to zpos (Round, Ceil, clamp 0..127), tile centre | `UWTileQueries.cs:54-93`, `UWUnits.cs:28-41`, `UWLevelLoader.cs:1446-1507, 2117, 2231, 2321, 1897`, `UWPlayerThrow.cs:443`, `UWPlayerMovement.cs:2430`, `UWTriggerSystem.cs:694-707`, `UWSavegameWriter.cs:64-77`, `UWWorldCapture.cs:125`, `Interaction.cs:3478, 3502`, `UWCharacter.cs:1008`, `UWTouchGather.cs:77`, `UWObjectSpawner.cs:627, 644, 1417, 2340`, `UWLevelMeshBuilder.cs:612`, `UWSpellHost.cs:424` | EF `UWUnits` (WorldToTile, WorldToEighth, EighthToWorld, WorldToZPos, TileCentre); one rounding rule per conversion, chosen after checking which copy the original matches |
| `y*64+x` 20 sites, raw `TileData[(y*64)+x]` about 58 sites, two `GetTile` | 11 files, `UWLevel.cs:13`, `UWTileQueries.cs:38`, `UWEndgame.cs:271` (no bounds check) | `UWLevel.GetTile` and `UWTilePos.Index` everywhere |
| Tile*8+fine, floor height `<<4/>>4`, PLAYER.DAT 8.8 position, `TilesPerAxis` twice | `UWCritterRecord`, `UWMobileObjectMotion`, `UWMotionCore`, `UWCritterBrain`, `UWTrapRules` (5), `UWTilePath`, `UWLevelWriter`, `UWPlayerData`, `UWAutomapPainter.cs:41` | EF `UWEighths`, `UWTile.FloorLevel`, `UWWorldScale.TilesPerAxis` |
| Yaw to 256 (Round vs Floor), yaw to 65536, 256 to vector (twice identical), eighth*45 (17 sites), units to degrees | `UWHudCompass.cs:270`, `UWPlayerThrow.cs:413`, `UWCritter.cs:334, 788`, `UWProjectileWorld.cs:183`, `UWCritterAnimator.cs:358`, `UWPlayerMovement.cs:1015, 2464`, `UWSavegameWriter.cs:76`, `UWProjectileFlight.cs:122-135`, `UWObjectSpawner` (14 sites), `UWScreenShake.cs:251`, `UWEndgame.cs:114`, `UWLevelLoader.cs:319` | EF `UWHeadings` (pure floats) plus Unity `UWViewpoint` vector wrappers; the throw must round like the compass |
| "Open half of a diagonal" three rules; COMOBJ radius clamp 1..3 three times | `UWTileQueries.cs:293`, `UWLevelLoader.cs:1408, 2145, 1436, 2171, 1328` | EF `UWTileQueries.IsSubTileInOpenHalf`, `UWPlacement` |
| Scale aliases (`TileSpacing` 69 uses vs `UWWorldScale.TileSize`, `HeightScale`, `SubTileScale`, `SubTileOffset`; 197 `const X = UWy.Z` re-exports) | `UWLevelMeshBuilder.cs:20`, `UWObjectSpawner.cs:44-79`, `UWViewpoint.cs:30`, `UWEndgame` (18), `UWCharacter` (17), `UWMusic` (13), `UWModernHudArt` (13) | use `UWWorldScale` and the home constant directly |
| Where the player stands, eight different derivations (camera, character transform, feet, motion params) | `UWSpellHost.cs:91`, `UWSleep.cs:236-247`, `UWTriggerSystem.cs:681`, `UWConversationHost.cs:138, 158`, `UWShrine.cs:100`, `UWInstrumentPlayer.cs:239`, `UWCreatureRespawner.cs:60`, `UWTileBlast.cs:196`, `UWProjectileWorld.cs:209, 640`, `UWCritter.cs:886` | one `UWSpot` (tile, eighths, zpos) from `UWPlayerMotion`, exposed as `World.Player.Spot` |
| Spot ahead of the player (11/8 tiles) and `fMessage` | `UWFishing.cs:34-58, 153`, `UWSilverTree.cs:42-49, 141` | EF `UWUnits.SpotAhead` |

### 4.3 World actions: pickup, use, doors, damage, spawning

| Group | Sites today | Single home |
|---|---|---|
| Picking up a world object, three paths (reach, weight whole vs count, creature test, pack-full text in English three ways, theft -> moonstone -> remove copied thrice; B15) | `Interaction.cs:955-975, 4333-4358`, `UWItemDrag.cs:826-900`, `UWModernBags.ContextMenu.cs:347, 391`, `UWModernBags.cs:525`, `UWItemDrag.cs:1287` | EF `UWPickupRules.Check(item, count, reachOk, inventory)` -> outcome and message id; Unity `UWWorldPickup.Take(entity, count, destination)` in the `UWItemDrag:849-855` order |
| Item-on-target dispatch (id knowledge in four places, "cannot be used on that" five times, anvil/oil branches twice) | `UWItemDrag.cs:1531, 1638, 1654, 1750, 2213-2266`, `Interaction.cs:1616` | EF `UWItemTargetRules.Classify/OnTarget`, one Unity applier `UWItemTargetUse` |
| Using a carried item, four chains (menu prints a literal that is general message 153) | `UWItemDrag.cs:659, 1672`, `UWModernBags.cs:1110`, `UWModernBags.ContextMenu.cs:357` | `UWCarriedItemUse.Use(item, context)` |
| Operating a door (player, creature use, creature pick, Gronk, trap, burst, sleep, Unlock spell; lock resolution at seven sites; `EnsureResolved` lazy at five; B4, B10, B11) | `Interaction.cs:3342-3389, 4266, 4306, 3866, 4047`, `UWCritter.cs:1227, 1266, 1306, 1466, 2868`, `UWTriggerSystem.cs:281, 503`, `UWDoorDamage.cs:31`, `UWSleep.cs:107`, `UWMiscSpell.cs:94`, `UWWorldSync.cs:75`, `DoorMover.cs:90`, `PortcullisMover.cs:87`, `UWDoorLock.cs:44` | EF `UWDoorRules` (DoorTrap, GronkTogglesDoor, NightCloses, CanOpen(spiked, locked, key)); Unity `UWDoor` component wrapping `IUsableDoor`, `UWDoorLock`, `UWDamageable`, resolving the lock once at spawn, one `fWriteStateToData`, `Toggle/Open/Close/ReplaceLock/BurstOpen` |
| Container spill (container keeps lock/triggers and fires a_use; corpse does not) | `Interaction.cs:1762-1800`, `UWCritterRemains.cs:192-204` | EF `UWSpillRules.Partition` plus one scatter call |
| Debris after destruction (two `Random` sources, two quality values, same remove/fallback lines) | `UWTileBlast.cs:153-189`, `UWContainerDamage.cs:62-80`, `UWProjectileWorld.cs:466` | EF `UWObjectDamageRules.RollDebrisObjectId` for both; one Unity remove path |
| Damage to objects and creatures (spike only in melee, effects per site) | `Interaction.cs:2358`, `UWCritter.cs:722, 1306, 1470, 1923`, `UWProjectileFlight.cs:200, 283`, `UWTileBlast.cs:84, 123`, `UWTriggerSystem.cs:364, 721`, `UWTargetSpell.cs:97, 106`, `UWSpellHost.cs:520` | `World.Damage(target, blow)` over EF `UWAttackResolution` (section 7.4) |
| Damage to the player, 17 sites, six differences (B5, B6) | table in section 7.5 | EF `UWDamagePipeline` (section 7.5) |
| Hit check and damage chain (creature vs player, same formulas, critical multiplier twice, hit-effect id twice, flank from two heading sources) | `UWCritterCombat.cs:125-223, 255`, `UWCombat.cs:185-215`, `UWPlayerAttack.cs:144`, `UWCritter.cs:1795-1948`, `Interaction.cs:2288, 2964`, `UWProjectileFlight.cs:239` | EF `UWAttackResolution` with `AttackerStats` and `IDefender` |
| Missile launch (two launchers, two damage functions that are equal), missile-id check twice, two 0.7 s effect constants | `UWCritter.cs:2166`, `Interaction.cs:2577, 5390`, `UWPlayerThrow.cs:211`, `UWProjectileWorld.cs:36, 201, 238`, `UWProjectileFlight.cs:60`, `UWPlayerThrow.cs:62`, `UWSpellHost.cs:19` | `UWMissiles` (section 7.6) |
| Creature spell casting bypasses `UWSpellCasting.ApplyEffect` (B3) | `UWCritter.cs:2044-2157` | creature `IUWSpellHost` -> `UWSpellCasting.ApplyEffect` |
| Creatures around a tile: four sources of truth (transform box, record square, physics sphere, `UWCritter.Active`); `UWScene.FindCritters` is a scene scan | `UWTriggerSystem.cs:179, 395`, `UWSleep.cs:160`, `UWSpellHost.cs:285, 388, 456`, `UWProjectileWorld.cs:527, 618`, `UWTileBlast.cs:68`, `UWPlayerThrow.cs:337`, `UWMoveTrigger.cs:209`, `UWDebugOverlay.cs:186` | `World.FindCreatures(area, filter)` over `UWCritter.Active` by record tile (physics-based area targets switched in a separate, checked step) |
| Magic-blocked tile (B2) | `UWEndgameRules.cs:83`, `UWCritter.cs:822` | `UWEndgameRules.IsMagicBlocked` for both hosts |
| Spell castability (float vs floored mana, circle formula copied into the panel, English literal) | `UWSpellCasting.cs:151`, `UWModernRunePanel.cs:397-436`, `UWModernActionBar.cs:472` | EF `UWSpellCasting.CanCast(seq, level, mana, runes)` -> reason |
| Cup of Wonder location and melody rule in a MonoBehaviour (level 2 vs 3, Chebyshev vs Manhattan) | `UWInstrumentPlayer.cs:33-54, 232-262`, `UWShrineRules.cs:55-64, 236`, `UWTalismans.cs:37` | EF `UWCupOfWonderRules` |
| Light source membership, three definitions (recipes, data brightness, id range) plus two parallel tables keyed by the same ids | `UWRemasterLights.cs:63-73, 418`, `UWPaletteEffects.cs:204-215`, `UWObjectProperties.cs:161`, `UWObjectMechanics.cs:1156-1191`, `UWObject.cs:221`, `UWLighting.cs:191-202`, `UWGlowingRockRules.cs:31` | EF `UWLightSourceTable {Id, Tiles, Brightness, Flicker, OpenFire, Glow, PaletteLightLevel, Shadows}` |
| Sequencing on `Time.time` for game rules (trigger spacing, respawn interval duplicating the player tick, death fallback, burial coroutine polling the conversation, endgame coroutines) | `UWProjectileWorld.cs:776-842`, `UWCreatureRespawner.cs:24-41`, `UWCritterRemains.cs:67`, `UWEndgame.cs:259-447`, `UWLogBridge.cs:19` | EF `UWWorldScheduler` on critter-clock units (section 7.7) |
| `PlayCutscene` written six times; shake twice (one ignores its argument); messages declared in six interfaces | `UWTriggerSystem.cs:619, 761, 772`, `UWSleep.cs:283`, `UWCritter` special-death host, `IUWItemUseHost`, `UWEndgame.cs:475`, `UWSpellHost.cs:332` | `IUWPresentation` (section 7.2) |

### 4.4 Player, creature, level

| Group | Sites today | Single home |
|---|---|---|
| Old movement path vs motion core (impact, jump, slow fall, hover, bounce, easy step, input mapping, fall damage type) | `UWPlayerMovement.cs` throughout, `UWPlayerMotion.cs:683, 781, 849, 1141, 1241` | delete the old path (section 3); make the core call `UWImpactRules` (the self-check tests the copy the core does not use) |
| Two swim counters (core never read by the host; terrain counter saved and read) | `UWPlayerMotion.cs:189, 873-897`, `UWPlayerTerrain.cs:49, 718-766` | EF `UWPlayerTerrainRules` with the core's counter only |
| Lava: creatures engine-free per step, player Unity-side on `Time.time` with a float chance | `UWCreatureMotion.cs:305`, `UWPlayerTerrain.cs:663-694`, `UWSettings.cs:418` | EF `UWPlayerTerrainRules`, driven by motion ticks |
| Player heading source (snapshot vs transform), player position (transform vs camera feet), copies of player HP into targets | `UWCritter.cs:886-903`, `UWProjectileWorld.cs:637` | one player snapshot per tick from the motion core |
| HP in two stores (`UWDamageable`/`UWHealth` vs record byte 8; six sync points; B23); position record vs body vs transform; goal/attitude mirrored | `UWCritter.cs:451-506, 2327-2334, 2510-2556, 2663-2672`, `UWWorldCapture.cs:93-127`, `UWCritterRecord.cs:182-349` | the record is the single source (section 7.4) |
| Death pipeline over 15 places (B22) | `UWCritterBrain`, `UWCritter`, `UWDamageable`, `UWCritterRemains`, `UWCritterAnimator`, `UWWorldCapture` | one actor state machine |
| Path engines: brain `UWTileRoute/UWTileTraverse` vs sleep ambush `UWTilePath` + `CanEnterTile` with different passability | `UWCritter.cs:1050, 1120, 1147, 2627`, `UWSleep.cs:198` | `UWTileRoute` |
| Placement paths (seven ways to compute a drop position; dead stores in `SpawnObjectById`; `UnityEngine.Random` with its own diagonal rule) | `UWLevelLoader.cs:968-1507, 1837, 2110-2252, 2286`, `UWPlayerThrow.cs:443`, `UWProjectileWorld.cs:1003`, `UWSpellHost.cs:415` | EF `UWPlacement` returning `{TileX, TileY, XPos, YPos, ZPos}` on `UWRandom` |
| Tile-list mutation in ten places; `UWInventory.RemoveWorldObject` duplicates `RemoveObjectFromWorld` and leaves `mEntityByData` stale; Armageddon skips `ReleaseMobileSlot` and leaves trigger volumes | `UWLevelLoader.cs:1560, 646`, `UWInventory.cs:264`, `UWTileQueries` (3) | EF `UWWorldEdit` (Place, Remove, Move, WipeAll) raising events; Unity `UWWorldView` subscribes |
| Start vs TravelTo: different order of `TakeSlots` and make-room; culling after the build destroys fresh objects | `UWLevelLoader.cs:288-333, 365-425` | EF `UWLevelChangeRules`, one pipeline |
| Effects (blood, splash, smoke) go through slot reservation, tile list and culling; a splat can cull real objects in a full level | `UWLevelLoader.cs:1589-1803` | `UWEffectViewFactory.Spawn(id, pos, seconds)` with no slot (after checking the original's class-7 handling) |
| Door and portcullis and secret door spawning share the tail three times; pickable rule twice; "mark as effect" thrice; "find the spawned child by childCount" because `SpawnSingleObject` returns void | `UWObjectSpawner.cs:2435-3134, 1062, 2124, 1657-1797, 2361` | `UWDoorViewHandler` with one `fBuildDoorAssembly`; `SpawnSingle` returns the GameObject |
| Pointer-zone handling: original table, RectTransform tests, per-class geometry; `fIsInventoryClickable` gate in six query methods | `UWGameUI.cs:200-226`, `UWHudInventory`, `UWHudCompass`, `UWHudRunes`, `UWHudOptions`, `UWHudPanel`, `UWHudMap`, `UWConversationScreen`, `UWMainMenu` | `UWClassicLayout` table (section 8.3) and `UWClickRules` |

### 4.5 Host interfaces (same bodies in several hosts)

`AddMessage` (Spell, Trap, Vitals, ItemUse), `AddGeneralMessage` (Spell, Trap, ItemUse, Sleep; the
sleep copy has no null check), `CurrentLevel` (Spell, Trap, Conversation; four conventions for the
level index), `HasPlayer`, `TravelTo`, `SpawnObjectById`, `SpawnObjectInTile`, `RemoveObjectFromWorld`,
`ShakeScreen` (trap ignores its argument), `DamagePlayer` (Trap, Sleep), `CursePlayer`, `PlayCutscene`,
`PlayerCastingSkill`, `Inventory`, `PlayerTile`, `PlayerMana` (floor vs round). Three RNG contracts
(`ICritterHost.Random(n)`, `IUWMotionWorld.Random()` 0..0x7FFF, `UWConversationVM.IHost.Random(max)`
1..max) plus the static `UWRandom`. Home: `IUWWorld` plus `IUWPresentation` (section 7.2), with the
existing interfaces shrinking to their truly specific members.

### 4.6 Classic HUD vs modern scheme

| Feature | Shared today | Doubled (approx. lines) | Shared model (EF) |
|---|---|---|---|
| Inventory / bags | `UWInventoryModel`, `UWClickRules.InventorySpotAt`, `GetSlotOfSpot` (lives in the classic view, called by two modern classes) | about 350: slot refresh three loops, stack count, remaining weight, equipment icon table (3 copies), paperdoll positions (4 copies), container back/icon/arrows, open-container picture (B29), rejection text, look, trade put/take | `UWInventoryView` (slots with icon id incl. open picture, count, free weight, rejection text); `GetSlotOfSpot` -> `UWClickRules.BodySlotOf`; `UWInventoryWeight.Format` |
| Character panel / stats (4 copies: classic panel, modern stats page, modern panel, help window) | character values | about 150 x 3; "a/b" vs "a / b", upper vs title case, "1ST" vs "level N", English labels hard-coded | `UWCharacterSheet` (Name, Class, LevelOrdinal, Values, Skills, Signature) |
| Runes / casting (selection state lives in the classic `UWHudRunes`, used by three modern classes; append-then-trim written twice; grid 8/5/18/15 and clear strip in both; hit tests 16x16 vs 14x14) | `UWSpellCasting`, `UWRunicMagic` | about 90 | `UWRuneShelfModel` (Selected, Add/Put/Clear/Load, Sequence, RuneAt on 14x14, IsClearStrip) |
| Conversation (CONV.BYT layout repeated as offsets minus 43, colours, trade counts, page marks split vs stripped, number format, caret, name colour 0x65 vs literal RGB, B31) | `UWConversationSession`, `UWConversationTrade`; modern reads the classic screen as its view | about 250 | `UWConversationLayout` (geometry, palette indices), `UWConversationView` (ShownLines with colour roles, MorePending, Choices, TypedAnswer, LookText, Trade, Portraits, EdgeFrame, TradeSlotAt) with a per-scheme `Policy {PauseForMore, WrapWidth, LinesCapacity}` |
| Map (two note renderers, note colour literal five times, tile constants twice; modern minimap depends on the classic class) | `UWAutomapPainter`, `UWHudMap.BuildMinimapTiles` | about 100 | `UWAutomapView`, `UWMapNotesModel` |
| Message log / scroll (wrap and trim, edge rolling, colour table, [MORE] marker, cursor key; B27, B28; blink 0.4 s vs half-second) | `UWTextLayout`, `UWScrollEdgeRules`, `LogColourIndices`, `fDrawPromptCursor` | about 80 | `UWMessageLogModel` (ShownLines, LineColourCodes, MoreRow, EdgeFrame, Key; one blink rule, one [MORE] policy) |
| Yes/no and quantity prompts (count prompt vs split box: identical pad repeat 0.35 s / 0.07 s / x10 after 2 s; wrap vs clamp) | yes/no state in `Interaction` | about 60 | `UWCountInput` (buffer, clamp, commit), `UWYesNoQuestion` |
| Options / menu (save/load/quit flows, untrimmed names in classic) | `UWSavegameWriter`, `UWSavegameSlots` | about 120 | Unity `UWSaveFlow` |
| Text entry (map note 46 upper, save name 30, conversation answer 40, mantra, character name; six filter sites; three lock mechanisms; B26) | `UWTypedKeys` | about 150 | `UWTextEntry` (section 8.5) |
| Compass (position 112/131 in three files; `BuildComposite` in the classic class for the modern one) | `UWClickRules.Compass`, `CurrentStep` | small | `UWCompassModel`; composite moves to `UWHudArt` |

### 4.7 Rendering and audio

| Group | Sites today | Single home |
|---|---|---|
| Shader globals: 59 `SetGlobal*` sites in 6 files, by string name, about 35 per frame; four Shown/Raw table pairs with their own names; index array and normal array each built twice (B25) | `UWShadePalette.cs:287-314, 502-512`, `UWPaletteRenderToggle.cs:375-604`, `UWPaletteEffects.cs:280-679`, `UWSweepMaskDriver.cs`, `UWOwnTile.cs` | `UWShaderIds` (PropertyToID constants), `UWShaderGlobals` (typed, change-detecting setters, `Invalidate`, `DumpAll`), `UWPaletteCommon` with `TableSet {Shown, Raw}`, `UWTextureCache` |
| Lookup-table construction (about 15 copies of 12 lines; the `linear` flag per table is critical), three flip-and-upscale loops, luminance twice | `UWShadePalette` (7), `UWTextureArrayBuilder.cs:107, 362, 494, 555`, `UWPaletteEffects`, `UWSweepMaskDriver`, `UWOwnTile`, `UWIndexAtlas`, `UWHeightMapBuilder` | `UWTextureFactory.Table2D/Table3D/SliceFromTexture` keeping each table's `linear` flag |
| Flicker three formulas (two identical), "who casts a ground shadow" twice, SSAO lookup twice, three writers of `renderPostProcessing`, parallax tuning pushed as material floats and as globals | `UWLighting.cs:498`, `UWLightFlicker.cs:62`, `UWPaletteEffects.cs:590, 519, 682`, `UWRemasterGroundShadows.cs:126`, `UWRemasterRenderer.cs:300, 343`, `UWWorldResolution.cs:164` | `UWFlicker`, shared ground-shadow rule, `UWRenderPathHost` owning camera flags and SSAO |
| Render mode: two sources of truth (`RenderMode` vs `IsPaletteActive`), nine `IsRemasteredActive` pollers, raw `RenderMode` reads in four files | `UWSettings.cs:131`, `UWPaletteRenderToggle.cs:162-270`, 8 files | `IUWRenderPath` with `UWRenderPathHost.Mode` and `OnChanged` |
| Colour help applied three ways (baked `_UWColourTable`, ChannelMixer, 25 UI re-bake sites); hallucination swap three ways | `UWShadePalette.cs:190`, `UWRemasterRenderer.cs:238`, `UWUiPaletteSwap.cs:57`, `UWModernHud`, `UWCharacter`, `UWModernScroll`, `UWHudMap`, `UWScreenUi` | `UWArtCache` on the Unity side (section 8.4); the world paths stay as they are |
| Audio: two `fEnsureListener`, two voice-allocation loops, XMIDI controllers 110-120 filtered per driver, drum channel 9 twice, instrument channel handled three ways, per-driver lifecycle repeated three times, `UWSoundPlayer.PlayAt` dead, hit volume `damage << 2` five times, `Camera.main` vs `UWViewpoint.Current` | `UWAudioEngine.cs:230, 283-418, 427-487`, `UWSoundPlayer.cs:140`, `UWGmMusicDriver.cs:120`, `UWMt32MusicDriver.cs:256`, `UWAdlibMusicDriver.cs:25, 263`, `UWCritter.cs:1320-1908`, `Interaction.cs:2375` | `IUWMusicDevice`, `UWEffectSynth`, `UWAudioMixer`, `UWAudioService`, filter in `UWXmiSequencer`, `UWAudioListenerGuard` |

### 4.8 Settings, menus, help, intro

| Group | Sites today | Single home |
|---|---|---|
| Three persistence stores (`UWSettings` asset, settings.json, graphics.json whose `EffectValues` mirrors 24 of 41 `Remaster*` fields); seven overlapping fields (sound x4, `DisplayAs4By3`, `GogInstallPath`) | `UWSettings.cs`, `UWUserSettings.cs`, `UWGraphicsDetail.cs:38-155` | `UWUserData` plain class, `UWSettingDef` table; `UWSettings` keeps the ~100 dev tuning fields only |
| `UWUserSettings`: 82 fields, 82 properties, 63 pure getter/setter pairs, clamps in two places | `UWUserSettings.cs:314-948` | table-driven `Normalize` and clamps; 46 call-site files regexed |
| IMGUI rows: about 10 slider rows with own offsets, 11 segmented copies, 13 toggle rows, 7 dialog frames, 27 + 37 `Save()` calls, `fListenFor` vs `fListenForPad`, `fChooseLevel` vs `fClickDetailEntry`, `fSetModal` twice, `fEnsureStyles`/`fTexture`/palette/`ReferenceHeight 720`/`GUI.matrix` in 4-7 files | `UWSetupMenu*.cs`, `UWModernLayoutEditor.cs`, `UWHelpWindow.cs`, `UWLetterGrid.cs`, `UWModernHud.cs`, `UWDebugTools.cs`, `UWMainMenu.cs`, `UWCharacterCreationScreen.cs` | `UWGuiTheme`, `UWGuiPanel`, widget set (`Toggle`, `Segmented`, `Stepper`, `SliderRow`, `Button`, `ListSelect`) |
| Help and setup texts: motion text three times (menu, CONTROLS.md, doc comments), controls text twice, defaults hard-coded in help, game rules in help strings | `UWSetupMenu.cs:905-983`, `UWHelpControls.cs:112-323`, `Docs/CONTROLS.md`, `UWHelpWindow.cs:970-979` | `UWText.T` table (see the mod plan's translation layer) with rule numbers taken from the rule classes |
| Cutscene interpretation inside the Unity player (segments, repeats, frame 999, `.N0x` increment, click-skip, 0x0E semantics, fades, file names, painted-block measure, palette-to-RGBA, victory glyph blitter paralleling `UWFontRenderer`) while `UWCutscene`/`UWCutsceneScript`/`DataImport.GetCutscene` exist | `UWIntroPlayer.cs:1080-1644, 224-409` | EF `UWCutsceneRunner` (steps: ShowFrame, Wait, WaitSpeechEnd, Fade, StartLine, Chime, OpenFile), checked beside `fCheckCutscenes` |

### 4.9 Self-check, tools, editor

Three `UWConversationVM.IHost` fakes (two in the self-check, one in the editor; the "+1" string
offset in three places); two `IWalker` fakes; two creature-record factories; the player body
literal six times; the launcher built twice; "frames until settled" inline about eight times; the
same ledge/creep/fall scenarios in three step modes (about 250 lines); level lookup eight copies;
inventory construction four copies; "no COMOBJ.DAT" guard five copies; two full `DataImport` loads
where `LoadGame(null)` suffices; seven orphaned doc comments sitting on the wrong method;
seven `fSafe`-style helpers in the editor copying `DataImport.GetObjectDescription`; nine editor
tools each loading the full `DataImport`. Home: `Tools/UWCheckLib` with `Harness/`, `Fakes/`,
`Checks/` (section 9).

## 5. What moves into the engine-free layer

New or extended engine-free classes, grouped. Each is self-checkable with a plain `dotnet run`.

Identity and data:
`UWObjectIds`, `UWLevelIds`, `UWQuestPlaces`, `UWStringIds`, `UWText`, `UWBinary`,
`UWObjectRecordCodec`, `UWNpcRecordCodec`, `UWLevArkLayout`, `UWLevArkCodec`, `UWObjectChain`,
`UWObjectFactory`, `UWObjectAppearance`, `UWDice`, `UWMath`, `UWUnits` (extended), `UWEighths`,
`UWHeadings`, `UWStackRules`, `UWLightSourceTable`.

Session and world state (section 7.1):
`UWGameData` (immutable), `UWGameSession` (mutable: world, player, quests, variables, story flags,
conversation memory, clock, critter clock and alarm, quirks, diagnostics), `UWSaveCodec`,
`UWWorldSession`, `UWWorldEdit`, `UWPlacement`, `UWLevelChangeRules`, `UWWorldScheduler`,
`UWTriggerPacer`, `UWSessionRandom`.

Rules now sitting on the Unity side:
`UWPickupRules`, `UWItemTargetRules`, `UWWorldUseRules`, `UWDoorRules`, `UWSpillRules`,
`UWItemMessages`, `UWCountInput`, `UWYesNoQuestion`, `UWAttackResolution`, `UWMissileHitRules`,
`UWDamagePipeline` (with `UWDamageRequest`/`UWDamageOutcome`), `UWPlayerTerrainRules`,
`UWDeathRules`, `UWVoidRules`, `UWFootstepRules`, `UWMotionStepper`, `UWCupOfWonderRules`,
`UWInstrumentRules`, `UWEndgameSequence`, `UWMoongateFlight`, `UWHealthRules`,
`UWSleepRules.CloseDoorsOnLevel` (from the sleep host), `UWCreatureActor` (the creature's logic,
section 7.4), `UWCreatureMotionHandlers` and `UWPathSearch` (instance state instead of statics),
`UWSpellCaster` (the cast pause in game ticks).

View models for both UI schemes (section 8.3):
`UWInventoryView`, `UWCharacterSheet`, `UWPanelTurn`, `UWRuneShelfModel`, `UWActiveSpellsText`,
`UWAutomapView`, `UWMapNotesModel`, `UWMessageLogModel`, `UWConversationLayout`,
`UWConversationView`, `UWConversationText`, `UWOptionsMenuModel`, `UWDragonAnimator`,
`UWCompassModel`, `UWClassicLayout`, `UWQuantityPrompt`, `UWTextEntryModel`.

Art and presentation (engine-free but not rules; section 8.4 and 8.6):
`UWMask`, `UWPixelOps`, `UWSeededRandom`, `UWNoise`, `UWMaskText`, `UWPixelRules`,
`UWTextureSynth`, one builder class per HUD piece under `UWData/HudArt/`, `UWMainFrame`,
`UWHudTuning`, `UWHudMasks`, `UWScrollPaperBuilder`, `UWCutsceneRunner`.

Tables that replace if-chains (section 7.8):
`UWTrapTable`, `UWDoActionTable`, `UWConversationFunctionTable`, `UWItemUseTable`, `UWFoodTable`,
`UWObjectAppearance`, the variable-trap operation table, the judgement threshold table.

Move out of the rules core into their own namespace or asmdef (about 9,500 lines: art, audio,
install) so that the rules core a future port must mirror is about 45,000 lines:
`UWDataImport.Presentation`, `UWDataImport.Audio`.

## 6. Constants: which become editable, and how

Counts (1,606 `const` lines including locals; a sampled classification):

- About 300 scalars, about 20 tables and about 25 quirk switches are original gameplay numbers:
  skill-check ranges, charge tables, restlessness rolls, melee distances, meal healing, sleep
  relief, unlock skills, fly speed, level-up thresholds, speed ratios, hit heights, the food table.
  These become editable.
- About 1,000 are file-format and data-tied facts: offsets, masks, record sizes, opcodes, object,
  trap and trigger ids, string indices, LEV.ARK blocks, a_do qualities, PLAYER.DAT bits. Changing
  them breaks loading. They stay `const`. Exception: about 25 story anchors (talking-door slot 25,
  conversation-trap NPC slot 251, bullfrog field, emerald, volcano and void, start tile) go into a
  separate `story` / `levels` table for total conversions, not into the balance rule set.
- About 280 are UI and engine tuning (HUD art thresholds, height map, colour filter, OPL2, head
  bob, palette, grime, `QuietnessTicksPerSecond`, `UWSettings`). They stay where they are
  (`UWSettings`, `UWHudTuning`, a presentation config). Frame-rate stand-ins that affect rules (the
  12/s quietness accumulator) are flagged as deviations.

Judgement: externalise the first group only, as one `rules.json` with flat dotted keys (as the mod
plan says), but one C# class per domain (`UWRules.Combat`, `.Critter`, `.Magic`, `.Experience`,
`.Vitals`, `.Sleep`, `.Trade`, `.Inventory`, `.Respawn`, `.Quirks`, `.Deviations`, `.Story`,
`.Levels`), so a domain can be moved and reviewed alone.

Rules of the rule set:

- Defaults stay in code as the source of truth: each field initialiser references the existing
  `const`, so the original-routine comment stays on the const (`public int SkillCheckRoll =
  UWSkillCheck.RollRange;`).
- `UWRules.CreateOriginal()` builds the defaults; `UWRulesJson.Apply(rules, json)` applies
  overrides; `uwdump rules-template` writes the full file; the same JSON can later generate a C++
  header so two ports read identical defaults.
- The rule set hangs on the session (`session.Rules`), not on a new static. `UWRules.Current` is
  allowed only as a transitional facade forwarding to `UWGameSession.Current.Rules`.
- Only coefficients are parameterised, never a formula's integer shape (`(v*3)>>1` keeps its shift);
  changing a formula is a hook, not a rule.
- Self-check pins: serialise `CreateOriginal()` and compare with a checked-in
  `rules-original.json`; reflectively assert every field equals its anchor const; apply a JSON that
  sets every key to a different value and assert it is accepted; all existing formula checks stay
  green at the defaults.
- Readability cost: `UWSkillCheck.RollRange` becomes `r.Combat.SkillCheckRoll`, mitigated by a local
  alias per method. `case` labels cannot use non-const values; only the format group is in switches.
  About 300 call sites, done domain by domain (about 2 h each).

Ids and layout facts get tables too, but as `const` (section 4.1): `UWObjectIds`, `UWLevelIds`,
`UWStringIds`, `UWTimeUnits`, `UWMainFrame` (the original's MAIN.BYT / CONV.BYT layout facts, about
160 numbers), `UWClassicLayout` (the 320x200 facts per area, every entry tagged EXE /
data-measured / estimated / user-tuned), `UWHudTuning` (the about 50 by-eye thresholds with a
one-line reason each). `UWLightSourceTable` and `UWFoodTable` are data tables and belong to the
editable group.

## 7. Object-oriented restructuring, gameplay side

### 7.1 Session instead of statics

Today `DataImport` holds static data and game state (`Levels`, `InitialPlayer`,
`ConversationGlobals`, map notes); `InitialPlayer` is mutated as live state; quest flags, game
variables, eight story flags, `UWCarriedSlots.Level`, the cast pause, `ResistBlowsAddsArmour`
(pushed every frame), the lava quirk, `UWRandom.msSource`, the critter clock and alarm, the
path-search scratch, `UWTrapLog.Detail` (a side-channel return value), `UWLevelWriter.Warnings` and
`UWObjectLightColour.msCache` are statics; two of them leak across New Game and Load (B21).

Target:

- `UWGameData`: immutable, one per installation (textures, strings wrapped by `UWText`, property
  tables, conversations, terrain, fonts, sound, shades, misc files).
- `UWGameSession`: mutable, one per running game. `Data`, `Rules`, `Random`, `World` (levels, raw
  LEV.ARK and block offsets, map notes, current level, carried slots), `Player` (vitals, inventory
  model, runes, position, rune bag), `Quests` (`int[38]`), `Variables` (`int[64]`), `Story`
  (today's `UWGameFlags`), `ConversationMemory`, `Clock`, `CritterClock`, `CritterAlarm`, `Quirks`
  (lava flag, last cast tick), `Diagnostics` (trap log, writer warnings).
- `UWSaveCodec`: `Load(data, folder) -> session`, `Save(session, folder)`, built from
  `UWPlayerDatCodec`, `UWLevArkCodec`, `UWBGlobalsCodec`. `UWPlayerData` shrinks to a DTO plus codec.
- Rule classes stay static and stateless and take the session (or a narrower `UWRuleContext`) as
  the first parameter. That maps 1:1 to free functions in any later port.
- Migration: pins first; `UWGameSession.Current` as a transitional static with the old static
  classes forwarding; signatures changed domain by domain; `Current` removed last.

### 7.2 One world interface, one presentation interface

Nine top-level and seven nested host interfaces with about 240 members overlap heavily (section
4.5). Target:

- `IUWWorld` (engine-free, about 200 lines): data, level, tiles, random, clock, scheduler; objects
  by handle (`UWObjectHandle {Level, Slot}`): `NewObject`, `Spawn(spot, mode)`, `Remove`,
  `GiveToHand`; `FindCreatures(area, filter)`; `Player` (spot, terrain, on ground, magic motion,
  inventory, skills); `Damage(target, blow)`; `Doors` (find, all, state, toggle, lock);
  `TravelTo`; `LevelGeneration` counter replacing the "a level change drops the rest" checks.
- `IUWPresentation`: fire-and-forget feedback (messages, sounds, music, shake, flash, bounce,
  cutscenes, dreams, night black, look text, spell icons, flask redraw). A headless check ignores it.
- `IUWPlayerAvatar`: the body (eye position, view direction, movement fraction, in liquid, on
  ground, charging, pending projectile and target cursors).
- Unity `UWWorld : MonoBehaviour, IUWWorld` on the level object (about 700 lines) absorbs the
  trap host's world and message parts, the theft/trespass search, the spell host's world members,
  the sleep host's creature and tile members, the conversation host's drop helpers, `UWTileBlast`
  (as `Damage` over a tile), the shared debris code and the object factory calls.
- Existing interfaces become thin views with `World`: `IUWTrapHost` 37 -> about 12 members (firing
  context, levers, engine traps, `RebuildGeometry`; `UWTrapHost` 478 -> about 150 lines),
  `IUWSpellHost` about 50 -> about 20 (caster numbers, view direction, pending projectile, active
  spells, icons; area targets become `World.FindCreatures` plus a `UWCastContext` value instead of
  the mutable `AreaCaster`; `UWSpellHost` 542 -> about 220), `IUWSleepHost` removed (`UWSleep.cs`
  303 -> about 30), `IUWConversationHost` keeps the player-stat block, shrine and special-death
  hosts replaced by `World` plus `UWGameFlags`, `IUWEquipment` deleted (`UWInventoryModel` is the
  authority), `IUWVitalsHost` callbacks become events on `UWPlayerVitals`.
- Keep unchanged: `IUWMotionWorld` and `IUWMobileObjectHost` (hot loop, instruction-faithful
  contract, about 2,400 lines of checks depend on them; only the implementing class is split),
  `ICritterHost` (per-creature context; implemented once by a session adapter, its `Random` dropped
  for the session RNG), `UWConversationVM.IHost`, the audio and palette interfaces,
  `UWTilePath.IWalker`.
- Call context as values: `UWTrapFiring` (trigger handle, switch handle, triggered-by, silent),
  `UWCastContext`, `UWAreaTargets`.
- Host lifetime: `UWTrapHost` is built per firing, `UWSpellHost` per cast, `UWConversationHost` per
  conversation, each carrying call state. The long-lived `UWWorld` must not absorb that state.

### 7.3 Interaction and item drag

`Interaction.cs` (5,558 lines, about 175 methods, seven concerns) stays the facade (about 110
external callers of `AddMessage`/`AddGeneralMessage`) at about 450 lines with `Update` as an ordered
list of `IModalInput.Tick()`. Extracted classes (target under 800 lines each): `UWMessageLog`
(with `IUWMessageSink`), `UWPromptController` (yes/no, mantra; `YesNoQuestion` and `TypedInput`
state objects), `UWWorldPicker` (one `Measure(ray, applySight)` -> frontmost, chunk, pickable,
hit; fixes B18), `UWClassicWorldGesture`, `UWModernWorldInput`, `UWPlayerAttackInput`
(`AttackState`), `UWPlayerMelee`, `UWPlayerMissiles`, `UWHitFeedback`, `UWPendingSpell`,
`UWWorldUse`, `UWLookService`, `UWItemTargetUse`, `UWWorldPickup`.

`UWItemDrag.cs` (2,464 lines) gets an explicit `DragState` enum (Idle, Pending, CountPrompt,
HeldDrag, StickyDrag, UseMode) replacing five bools, and one `DragSource` value (Kind, Index, Slot,
Entity; Peek/Take(count)/Return/Consume) replacing nine fields reset in five places with different
subsets. Extracted: `UWCountPrompt` on the engine-free `UWCountInput` shared with the modern split
box, `UWClassicTradeDrag`, `UWClassicUseMode`, `UWItemUseHost` (an `IUWItemUseHost` per use, no
`mOUsedItem` side channel), `UWDragIcon` as the single owner of `Cursor.visible` (ten writers
today). Model-level `ReturnCursorItem(origin)` replaces the classic/modern pair.

Frame-number side channels (`PageInputConsumedThisFrame`, `miYesNoAskedFrame`,
`miMenuClosedFrame`, `miSplitClosedFrame`, `UWMouseButtons.IsBlockedByPrompt`) are replaced by one
right-button router or an explicit execution order.

### 7.4 Creatures

`UWCritter.cs` (3,074 lines) becomes a thin view (about 550 lines: registry, picture lerp and held
motion, impact effects, transform snap, animator marks, trace, adapters) owning an engine-free
`UWCreatureActor` (about 1,000 lines: record, row, index, brain, world; implements `ICritterHost`
unchanged at first; queries, movement with the path cache on the level clock, doors via
`world.Doors`, blow via `UWAttackResolution`, missiles via `world.Launch`, spells via
`UWSpellCasting.ApplyEffect` with a creature spell host (fixes B2, B3), one death state machine
(special deaths move in), `OnDamaged(amount, type, attacker)` with the explicit attacker replacing
`miPendingAttacker`/`AnnounceAttacker`/`miLastHealth`/`mbSuppressDamageEvent`, one traced `SetGoal`
writer). `ICreatureWorld` (Unity `UWCreatureWorld`, one per level, about 600 lines, about 18
members) provides level, motion engines, player snapshot, actor lookup by index (dictionary instead
of `Masterlist.IndexOf`), melee defender search, doors, launch, spell host, sound, music, effects,
data moves, talk, experience, death completion.

`UWAttackResolution` (engine-free, about 250 lines): `AttackerStats {Score, BaseDamage, Charge,
Flank, FeetZ, TopZ, WeaponKind, Index, IsPlayer, PoisonByte}`, `IDefender {IsCreature, Defence,
ProtectionAt, ArmourAt, HalvesOnEasy, FeetZ, TopZ, ApplyDamage, Poison}`, `Resolve ->
AttackOutcome {Result, Part, Rolled, Damage, Critical, BlockedSound, HitVolume, Poisoned}`. Covers
object (no check, no armour), creature (row armour, x5/3 if strong) and player (protection, easy
halving, poison). Implementations: `UWCreatureActor`, `UWPlayerCombatant` over `UWCharacter`,
`UWObjectDefender`. Creature blow, player melee and missile-on-player all call it; player melee
keeps target search, wear, eyes and spike handling (B4 fixed by routing every object damage through
the door service).

Single source of truth: HP is the record (an `IHitPointStore` on `UWDamageable`; a vetoed golem
then saves as 0, not 1; `UWWorldSync` stops passing HP), position is the record (`BodyPosition`
for view and remains only; `fCaptureCritter` stops the float conversion for creatures). The
`UWCritterDriver` keeps walking `UWCritter.Active` in today's order (OnEnable order, not
`GetActiveMobileOrder`) unless the user decides to switch deliberately.

Later shrink of `ICritterHost` (31 -> about 22 members): inject random, clocks, own height and the
missile-velocity table into the brain constructor; `OnGoalChanged` becomes an event;
`FakeCritterHost` keeps its scripted roll queue as an `IUWRandom`.

### 7.5 Player: movement, damage, death, character

- Delete the old movement path (section 3), keep a "no world: stand still" branch.
- Split `UWPlayerMovement` (2,504 -> about 1,200 after the deletion): `UWPlayerInput` (command
  builders, `ConsumesRightClick`, easy-movement input), `UWPlayerMotionHost` (keeps the name:
  `fEnsureMotion`, stepping, sync, `fApplyCoreResult`, transform write-back, placement, bounce),
  `UWPlayerFootsteps` with an engine-free `UWFootstepRules`, engine-free `UWMotionStepper` (tick
  accumulator, Original/Smooth loop including the pending jump; self-checkable).
- One damage pipeline. Today 17 sites lower the player's HP:

| Site | Type | Armour | Easy halving | Resistance | Flash | Sound | Other |
|---|---|---|---|---|---|---|---|
| creature melee `UWCritter.cs:1858` | Physical | yes | yes | yes | critical only | HitPlayer | the only site telling the music; wear; poison |
| missile `UWProjectileFlight.cs:253` | missile's | creature missiles only | creature missiles only | yes | no | HitPlayer even for 0 | |
| blast `UWTileBlast.cs:199` | blast's | no | no | yes | no | no | |
| lava `UWPlayerTerrain.cs:687` | PlainFire | no | no | yes | no | no | `Time.time`, 0.2 chance |
| drowning `UWPlayerTerrain.cs:765` | None | no | no | n/a | 0xC6 | no | own clock |
| core fall `UWPlayerMovement.cs:2377` | Physical (doc says 0) | no | no | yes | no | landing | |
| trap `UWTriggerSystem.cs:575` | None | no | no | n/a | no | no | |
| sleep drowning/lava `UWSleep.cs:101` | None | no | no | n/a | no | no | lava asleep ignores Flameproof |
| trap item `UWItemUse.cs:294`, leeches `:461` | None, proof 0 | | | ignored | no | no | |
| sleep poison `UWSleepRules.cs:246` | Poison, proof 0 | | | Poison Resistance ignored | no | no | |
| starvation `UWSleepRules.cs:374`, poison tick `UWPlayerVitals.cs:965` | | | | | no | message | |
| curse (4 callers), backfire `UWSpellHost.cs:139` | None | | | none | 0xA8 | no | no shake, not counted for the dragons |
| void drain `UWVoidEffects.cs:106` | | | | | 0xB5 | no | `UnityEngine.Random` |

  `UWDamagePipeline` (engine-free): `enum UWDamageSource`, `struct UWDamageRequest {Amount, Type,
  Source, BodyPart, SubtractArmour, HalveOnEasy, KeepAtLeast, Shakes, CountsAsHit, FlashColour}`,
  `struct UWDamageOutcome {Requested, AfterArmour, AfterEasy, Applied, Resisted, Died, ShakeLevel}`,
  `Apply(vitals, in request)`. Armour via `IUWVitalsHost.GetArmourAt(part)`, proof via
  `IUWVitalsHost.DamageTypeProof`, the host told once via `OnDamaged(outcome, request)` (shake,
  flash, music there; per-site sounds stay). `ApplyDamage/ApplyCurse/ApplyBackfire/DrainVitality`
  collapse into private helpers.
- `UWPlayerTerrainRules` (engine-free): lava roll and swim counter with drowning, driven by motion
  ticks (one roll per 16 ticks in Smooth mode) and the vitals tick; one swim counter.
  `UWPlayerTerrain` becomes `UWPlayerView` (camera height, sway, roll, bobs) plus
  `UWAutomapExplorer`.
- Death as an event: vitals raise `OnDied` on the crossing; `UWDeathRules.Decide(talismanFlag,
  treeLevel, levelIndex, levelCount) -> Spared/Resurrect/Final`; bones on `UWRandom`; the
  "no regeneration at 0 HP" guard revisited. `UWVoidRules.Tick()` on `UWRandom`.
- `UWCharacter` (1,824 -> about 350): `UWWeaponView`, `UWPaperdollView`, `UWFlaskView`;
  `RefreshStatus` on change (inventory, spells) instead of per frame; `ResistBlowsAddsArmour` once at
  settings load; callers use `UWCharacter.Vitals`.
- Fixed Update order: Character -50, Movement 0, Terrain/View +10, Death +20 (or one
  `UWPlayerFrame` driver), removing the three-hop contact mirror and the one-frame lags.

### 7.6 Level loading, spawning, world edits

- `UWLevelLoader` (3,283 -> about 900): `UWStaticAssets` (shared textures, materials, atlas;
  `Get(dataPath, filterMode)`), `UWBootstrap` (the ten `AddComponent` calls, UI, lighting, debug
  overlay), `UWWorldSession` (engine-free game-state half), `UWPlacement` (engine-free, about 350),
  `UWWorldEdit` (engine-free, about 300: slot reservation, mobile slot, sink, tile list; events
  `ObjectPlaced/Removed/Moved/LevelCleared`), `UWWorldView` (Unity, subscribes: spawn, render mode
  via `ApplyToNewObject` at the single exit, registry `mEntityByData`), `UWEffectViewFactory`,
  `UWLevelChangeRules` (one pipeline for Start and TravelTo: flush, leave level and orb, return
  carried slots, switch index, take slots, make room before the build, build, place player,
  reapply palette), `UWPlayerPlacement`, `UWWorldSync` reduced to `UWWorldView.FlushToModel`
  (door quality written through from `UWDamageable`; creature moves through `UWWorldEdit.Move`).
- `UWObjectSpawner` (3,414 -> dispatcher about 150 plus handlers): `IObjectViewHandler {Handles,
  Build(SpawnContext, obj, x, z)}`, `SpawnContext` (replaces the mutable `miSpawnChainIndex`,
  `mbSpawnTileHasModel` and the loader back-reference), `UWViewResources` (resolve, UVs, quad cache,
  material variants), handlers `UWBillboardViewHandler`, `UWCritterViewSetup` (HP and armour from
  engine-free `UWHealthRules`), `UWWallDecalViewHandler`, `UWModelViewHandler`, `UWModelMeshBuilder`
  (with a cache by model, texture and colour; `fSpawn3DModel` is 447 lines today),
  `UWDoorViewHandler` (one door assembly for door, portcullis and secret door).
- `UWProjectileWorld` (1,058) splits into `UWMotionHost` (keeps `IUWMotionWorld` and
  `IUWMobileObjectHost`; fixes B12, B24) and `UWMissiles` (launches, strike, blast, impact);
  `UWProjectileFlight` keeps the picture, its hit maths move to `UWMissileHitRules`; the self-bump
  goes once the player is always on the motion core.

### 7.7 Sequencing on the game tick

`UWWorldScheduler` (engine-free, keyed by critter-clock units, advanced from `UWCritterDriver`
after `RunDue`, pausing with `UWGameClock` holds automatically): `After(units, action)`,
`AfterConversation(action)` (handles "not yet open"), `EveryPlayerTicks(n, action)`.
`UWEndgameSequence` replaces the three endgame coroutines (rending message, gate spawn, Slasher
message, 64 `UWMoongateFlight.PoseAt` steps to `World.Camera`, travel, final message; the static
`mbLaunching` becomes sequence state). `UWTriggerPacer` replaces the `Time.time` spacing (one unit =
16/256 s, today's value). Respawn becomes `EveryPlayerTicks(24)` and `UWCreatureRespawner` goes.

### 7.8 Tables instead of if-chains

- `UWTrapRules.fRunTrapInner` (15 `if (ID == ...)` over 0x180-0x190 with a nested a_do switch;
  `TeleportTrapId` declared locally): a 32-entry `TrapHandler[]` by `id & 0x1F` and a 64-entry
  `DoHandler[]` by quality; an unknown entry is logged and the chain goes on through its link, as UseTrap does (since 2026-10-12; built 2026-10-14 as `UWTrapTable`/`UWDoActionTable`). Variable-trap
  operations and their names become one `{name, op}` table.
- Conversations: three dispatch levels (VM built-ins, `CallUnknown`, trade `Handles` plus `Call`
  switch that must agree by hand) become one `UWConversationFunctionTable` keyed by name (the VM
  keeps its eight built-ins; missing names keep the `MissingFunctions` behaviour; mods register
  through the session's hooks).
- `UWItemUse`: `TryUseImmediately` precedence and the `fTryUseOnSelf` id chain become an ordered
  `UWItemUseTable` of (predicate, handler); `fEatLikeOriginal`'s id switch becomes `UWFoodTable`.
- Smaller: `fDoJudgement` thresholds, `fGetObjectField`/`fSetObjectField` parallel switches, per-id
  texture branches in `load_object_list` (`UWObjectAppearance`), `UWObject.GetCategory`.
- Keep as switches: `UWConversationVM.fExecuteOne` (an interpreter switch is the most faithful
  mirror; only extract operand decoding), the motion core, `UWCritterBrain` goals and `fGoto`,
  `UWTileRoute`, `UWTileTraverse.Triple`.

## 8. Object-oriented restructuring, presentation side

### 8.1 UI kit and plumbing

- `UWUiKit` (about 220 lines): `CreateRawImage/Image/Text(style)`, `SetRect`, `CreateOverlayCanvas`;
  replaces ten identical `fSetRect`, twelve `fCreateRawImage`, seven `fCreateImage`, eleven
  `fCreateText` and fourteen canvas setups (with their differences: a text that skips the font
  registration, one without outline, two fonts, two raw images starting enabled).
- `UWUiSorting` (six canvases share order 41 today; the rest are literals 38-47, 80, 90, 100),
  `UWModernPalette` (gold x6, text x5, dim, rule, light text; `UWModernHud`'s text colour differs
  and needs a decision), `UWInterfaceFont.Modern` (eleven `Resources.Load<Font>` sites),
  `UWControlScheme.IsModern` (59 scheme tests in three forms).
- `UWTextureFactory` (Unity, about 90 lines): `FromPicture`, `FromTexture` (replaces the 22
  `fGetTextureInvert` callers and the eleven inverted-texture copies), `FromColours` (seven
  procedural Bilinear sites, `ApplyMask`); fixes B30 as an explicit decision.
- `UWArtCache.Get(key, stamp, build)` replacing 27 version fields and three string-key dictionaries
  (key = colour version, backs version, filter, size); `UWIconCache` (six cached copies, nine
  uncached rebuilds); `UWVersionedTexture`.
- `UWTextTextures` owned by `UWGameUI`: `Get(font, lines, width, colours, centred, filter) ->
  handle`, reference-counted or LRU, owns and destroys its textures; `RenderLines` fills one
  `Color32[]` and calls `SetPixels32` once (today a new texture per call and a `SetPixel` per glyph
  pixel); replaces nine ad-hoc label caches and fixes the leaks of B33. `UWGrTextures.Get(type,
  index, filter, mode)` for pictures. The input cursor becomes a separate small image, never drawn
  into a cached texture.
- `UWGuiTheme` / `UWGuiPanel` / widgets for the IMGUI windows (section 4.8); `UWImguiSkin` for the
  shared styles.

### 8.2 Modern UI structure

- `IUWModernPart` with a `UWModernPart` base (placement, version keys, art rebuild, pointer gate),
  explicit update order, `UWModernDockedPanel` base for `Panel` and `RunePanel` (the docked-panel
  state machine exists twice today), `UWModernUi` root (one pointer gate, `IsOverUi`, the closable
  chain for Escape and pad B, pixel scale).
- Split `UWModernHud` (2,555) into about eight classes (game menu, heading strip, hollow and
  flasks, spells and crosshair, messages, pictures, action routing), `UWModernBags` (4,117 over four
  files) into about six (drag, click-click, split, context menu, trade, pages).
- `UWGameUI` (2,195 -> about 600): `UWViewportCamera`, `UWClassicGeometry`, `UWPointerZones`,
  `UWPointerCursor`, `UWUiKeyRouter` (replaced later by `UWHotkeys`, section 8.5),
  `UWTextEntryState`, `UWWeaponSway`, `UWClassicHud`, `UWGameActions` (one game action per
  trigger; the same action is started from several paths today). The 40 forwarding one-liners
  (`UWGameUI.cs:1830-2195`) go; callers use `mOUi.Runes/Map/...` directly.
- `IUWUiScheme` with Classic and Modern implementations as the last step, once the view models exist.

### 8.3 Shared view models (classic and modern become renderers)

`UWClassicLayout` (engine-free, static, one nested class per area, every entry tagged EXE /
data-measured / estimated / user-tuned): frame and window hole, panel and chain, paperdoll and
equipment icon table, backpack and container, weight centre, rune shelf and hollow and active
spells, stats rows and paging, message log and edges, compass, dragons, map, conversation
(CONV.BYT, trade tables, colours, name colour), options, main menu. `UWAutomapPainter`, `UWHudArt`
and the modern "original look" reference this table instead of repeating the numbers.

View models (listed in section 5) each answer "what to show" and "what a click on X does"
(returning message ids), with time passed in. Release-based input, frame guards, `ConsumesLeftClick`,
the dragons' RNG call order and the panel step timing stay in the Unity controllers.
`UWConversationScreen` splits into `UWConversationController` (input, release-frame guards, fade,
clock, music, gamepad) and `UWClassicConversationRenderer`; `UWModernConversation` reads the same
`UWConversationView`. `UWHudPanel.Update`'s routing becomes an explicit `UWClassicHudInput`.

### 8.4 HUD art builders

`UWHudArt.cs` (3,375 lines, about 2,000 lines of hand-written pixel algorithms, seven flood fills,
three copies of the neighbourhood-grow algorithm, six outline variants, eight opaque-forcing loops,
three black thresholds, three hand-parsed string masks without dimension checks, `fMain` decoded
14 times per rebuild):

- Pixel canvas: grow `UWPicture` with `UWPixelOps` (Fill, MakeOpaque, Clear, Blit, MirrorTile,
  Outline(predicate), Pad, Dilate/Erode) and a sibling `UWMask` (FromPicture, FromText with a legend
  and a dimension check, Flood, Pieces, Largest, CountAround); `UWSeededRandom` (xorshift32, named
  seeds), `UWNoise` (shared with `UWBackdropArt`), `UWPixelRules` (named colour predicates; the three
  black thresholds are named, not unified), `UWTextureSynth.PickBest3` (the top-3 insertion written
  three times).
- One builder class per piece under `UWData/HudArt/` (leather, slot, paperdoll, page freer, shelf,
  stone shelf, Classic Wide frame, conversation art, frame cuts, compass disc), each with
  `Build(UWTextures)` and a pure `Compose(UWPicture main, ...)` so the core runs on synthetic input.
  `UWHudArt` shrinks to a facade. `FreeOnPage` (337 lines) is split into its six steps verbatim.
- `UWMainFrame` (layout facts), `UWHudTuning` (by-eye thresholds), `UWHudMasks` (string arrays with
  their crop rects). `UWModernScroll.fBuildPaper` moves to an engine-free `UWScrollPaperBuilder`.
- Safety net first: `uwselfcheck --art-record / --art-verify` hashing every builder over a
  parameter grid with the real data (hashes stored locally, they are not art), plus synthetic checks
  (sizes, pure functions, mask integrity, determinism, alpha invariants).
- Known cost: Classic Wide rebuilds on every window resize (on the order of 60-90 million colour
  distances at extra = 480); a coarser quantisation of `Extra` is the real fix and is a separate
  decision.

### 8.5 Input

No `.inputactions` asset exists; all maps are built in code (`UWControls.cs:431-657`, 66 actions).
About 128 raw key reads in 26 files, 83 `Mouse.current.position` reads in 33 files, three cursor
systems, six text-entry filter sites, two key-routing chains in `UWGameUI`, about 100 scheme checks
in 25 files, about 14 Escape consumers with accidental priority, four `UWControls` instances.

Target (the Input System device bindings stay; rebinding works at that level):

- `UWInput` facade (`Pressed/Held/Released(UWAct)`, `Value`), `UWAct` enum plus `UWActionTable`
  (same strings as today), `UWInputContext` stack (World, UiModal, TextEntry, PadPointer, Cutscene)
  replacing the hand-written typing lock list and `IsWorldInputBlocked`, with one owner stack for
  Escape/back.
- `UWHotkeys` table replacing `UWGameUI.cs:1019-1266`; `UWPointer` (one position, one button
  state, one `IsFree`; sources Mouse, Pad, ClassicWarp; owns warp and `Cursor.*`, the "over UI"
  registry) done in two parts (read-only redirect of all 83 reads, then ownership). THE GOAL FOR
  `UWPointer` (DECIDED 2026-10-23, per user): a pointer of our own, as under DOS - the position
  kept from the mouse deltas like the mouse driver kept it, the picture drawn by us at the
  frame's scale, the system pointer locked and hidden; the classic clamp becomes a line, no
  `WarpCursorPosition`, no XWayland emulation, the pad and the mouse one path. Preconditions:
  the IMGUI parts (modern game menu, help, setup bar) on our kit, since they take position and
  clicks from the real pointer (today the pad warps it and clicks through `UWOsMouse`); a
  pointer speed setting, since the deltas come without the system's acceleration; focus loss and
  Alt+Tab in the classic scheme as the modern one has them. DECIDED 2026-10-25 (per user): a
  "Pointer speed" slider, 0.25 to 4, default 1 = one mouse count per screen pixel (Windows' default
  without acceleration), no acceleration; the mouse is captured only in the running game - the main
  menu, the game menu and the setup windows free the system pointer, drawn with our picture at its
  position. Until then the pointer is drawn by
  Unity (`CursorMode.ForceSoftware`, 2026-10-23: the system limits a hardware pointer's size) and
  the held walk draws its own at the clamped position (`UWPointerCursor`); `UWTextEntry`
  (hook plus `UWTypedKeys` fallback, one filter, max length, Backspace/Enter/Esc events; fixes B26);
  `UWHoldTap`/`UWRepeat` (three tap/hold and five repeat copies); `UWBindingInfo` (four lookup
  loops, two control-name tables).
- Rebinding file compatibility: `settings.json` stores `"Map|Action|BindingIndex|path"`; map name
  `Player`, action names and binding ordinals (including composite parts) must survive; never rename
  an action without a migration table; keep a golden settings.json with overrides as a fixture.
- Leave the layout editor's raw reads as they are (route only position and buttons through
  `UWPointer`); keep `UWOsMouse`/`UWOsKeys` untouched behind `UWPointer` (real OS clicks are the
  design point for IMGUI windows); test the pad on the Kubuntu VM.

### 8.6 Rendering and audio

- `IUWRenderPath {Mode, Activate, Deactivate, Tick, OnLevelBuilt, OnObjectSpawned, GetMaterial}`
  with `UWRenderPathHost` (single owner of `requiresColorOption`, `requiresDepthOption`,
  `renderPostProcessing`, SSAO; `Mode` and `OnChanged` replacing the nine pollers),
  `UWPaletteRenderPath` (toggle's material swap, the seven effects, the sweep mask with an early-out
  on x, y, heading, level), `UWRemasterRenderPath` (keyword, arrays, material floats pushed from a
  settings revision, Volume, colour vision; `IUWRemasterFeature` children), `UWPaletteCommon`
  (always on in both paths: tables, hallucination feed; `TableSet {Shown, Raw}`).
- `UWShaderIds`, `UWShaderGlobals` (cache reset in a `SubsystemRegistration` method), `UWTextureFactory`
  for lookup tables, `UWTextureCache` (one index array, one normal array with the staleness check).
- `UWLightSourceTable` (engine-free), `UWFlicker`, one ground-shadow rule.
- Audio: `IUWMusicDevice : IUWMidiDriver` (Name, XmiVersion, Prepare, Render, Reset, AllNotesOff,
  ReleaseInstrumentChannel, Dispose) with `UWAdlibMusicDevice` ticking the sequencer per chip sample
  inside `Render` (removes the reference-equality hacks), GM and MT-32 adapters,
  `UWMusicDeviceSelector` (fallback, ROM discovery with its timestamp cache), `UWEffectSynth` (nine
  voices, one allocation loop), `UWAudioMixer` (what is left of `UWAudioEngine`, 696 -> about 250),
  `UWAudioSettings` snapshot with a revision counter, `UWAudioService` facade absorbing
  `UWSoundEffects` and `UWMusic` and deciding `UWViewpoint.Current` once, `UWAudioListenerGuard`,
  the 110-120 controller filter in `UWXmiSequencer`.
- Keep all three keywords as `multi_compile`; keep exactly one pipeline and renderer asset; test a
  Windows player build in both modes after the render-path steps.

### 8.7 Settings, menus, help, intro

- Declarative settings: `UWSettingKind`, `UWSettingStore {User, Layout, Graphics, Live}`,
  `UWSettingDef {Key (= JSON field name, never renamed), Kind, Store, Min, Max, Step, LabelId,
  HintId, ChoiceIds, Invert, Format, EnabledWhen, VisibleWhen, OnChange, Normalize}`,
  `UWSettingsTable`, `UWSettingRow.Draw`. Dialogs become page objects; the layout editor's
  `fGatherItems` (208 lines) becomes per-element def lists; `StartEnum` presets and `fChooseStart`
  (20 fields by hand) become dictionaries; the layout snapshot iterates `Store == Layout` with a
  self-check that the set equals today's 24 names.
- `UWUserData` plain class (JSON identical), static facade for load/save and the few special
  properties; legacy-read quirks kept as `Normalize` entries with fixtures (MouseLookSpeed <= 0,
  StickLookSpeed <= 0, EasyMovementInterval <= 0, invalid frame ticks, modes out of range,
  InterfacePreset -1, ControlScheme -1, DisplayMode -1, MotionSmoothing 0/1).
- Help tabs as classes (`UWHelpTab` with `Id`, `TitleId`, `CapturesKeys`, `OnShown`, `Draw`);
  `UWHelpWindow` 1,697 -> about 450.
- `UWCutsceneRunner` (engine-free) and the split of `UWIntroPlayer` into `UWIntroPlayer`,
  `UWCutsceneView`, `UWSplashSequence`, `UWVictoryScreens`; public surface unchanged; startup
  ordering is delicate, so the interpreter is extracted first.
- `UWText.T/F` table for the port's own texts (about 750 ids: setup menu, help, editor, key
  bindings, gamepad, effects screen, letter grid) as the translation layer the mod plan needs; game
  string blocks stay in the data.

## 9. Self-check, tools, editor scripts

### 9.1 Harness

`Tools/UWSelfCheck/Program.cs` (4,767 lines, 844 of about 1,240 expectation sites) becomes
`Tools/UWCheckLib` (`Harness/`: `Check` with Bool/Int/Near/Str/Seq, `CheckRunner` with try/catch
and a stopwatch per check and a summary table, `CheckCatalog` with name, tags and `NeedsData`,
`CheckContext` with lazy data, `FreshData()` via `LoadGame(null)`, `Level(n)`, exe path;
`StateGuard` snapshotting `UWRandom`, quest and game flags, `ResistBlowsAddsArmour`, creature
handlers; `Fakes/`; `Checks/` one file per domain, none over about 760 lines) plus a 100-line
`Program.cs`. Options: `--only`, `--skip`, `--tag`, `--list`, `--quiet`, `--no-data`, `--root`,
`--junit`. Exit codes 0 ok, 1 failures, 2 data not found, 3 filter matched nothing. Fixes B14
and B36. A mod validator (`Tools/UWModCheck`) and `uwdump rules-template` reuse the library; pins
that equal the original carry the `original` tag so a mod run can skip them.

### 9.2 Coverage gaps to close (in the order of value for the cleanup)

1. Save round trip: `UWLevelWriter.BuildBlock(level, original) == original` for the nine DATA
   levels and the test saves; `UWPlayerData` decrypt -> parse -> write identical; BGLOBALS identity;
   a double save identical. No such check exists today, and the data-layer steps depend on it.
2. Fakes for the hosts that have none: `FakeWorld : IUWWorld` (tile grid shared with
   `FakeMotionWorld`, creature list, logs of messages, sounds, effects, spawns and removes, a door
   table, a scripted RNG, a scheduler advanced by units), with trap, spell, sleep, shrine,
   special-death and conversation views as thin wrappers. New checks: door trap on a moving door,
   ward damage, trespass box, sleep night doors, Tremor, the endgame sequence by units, a
   conversation smoke test running every slot to Finished.
3. Unchecked rule classes with no reference at all: `UWConversationTrade` (1,936 lines),
   `UWTrapRules` (only `RemoveOneShotTrap`), `UWSpellCasting`, `UWRunicMagic`, `UWAreaSpellRules`,
   `UWLevelWriter`, `UWSavegameStore`, `UWSpecialDeaths`, `UWCritterLoot`, `UWLevelExit`,
   `UWRepairRules`, `UWItemApplications`, `UWItemDescriptions`, `UWLoreCheck`, `UWEndgameRules`,
   `UWSilverTreeRules`, `UWTalismans`, `UWMazeNavigation`, `UWItemSpellRules`, `UWInventoryWeight`,
   `UWCarriedSlots`, `UWBagPlacement`, `UWReachRules`, `UWObjectLightColour`; `UWPlayerData`,
   `UWInventoryModel` and `UWPlayerVitals` only indirectly.
4. Pins that are weak today: stew and click message ids never checked against the string,
   `fCheckHitVolume` passes trivially (`liDamage == 0 ||`), `LiquidSurvivalThreshold > 0`.
5. Golden traces with the scripted RNG for a trap chain, a barter and a sleep.

### 9.3 UWDump and the editor scripts

Give `UWDump` a command registry (`Name, Usage, NeedsData, Run`) so data-free commands skip the
load, then add `strings-export` (lossless, as the mod plan's translation export), `tile`, `npcs
[level|all]`, `where <id...>`, `conv [slot|all]` and `conv --run <slot>`, `automap [--save dir]`
and `maprender` with a small PNG encoder (`ZLibStream`), a `--detail` flag for `level`, and
`rules-template`. Then delete `UWStringSearch`, `UWStringExporter`, `UWAreaDump`,
`UWTileGeometryDump`, `UWNpcDump`, `UWFindObjectDump`, `UWConversationDump`, `UWConversationRun`
(its all-slots loop becomes the conversation smoke check), `UWAutomapDump`, `UWMapRender`
(1,379 lines; keep `UWLevelWindow`, `UWProjectSetup`, `UWRemasterSetup`, `UWFontTemplates`).
Data path: add an optional `UW_DATA` environment variable and a shared candidate list; the tools
and the game extract game.gog to two different caches, and parallel cold starts can clobber the
`.partial` folder (a lock file is needed). `Tools/UWOriginalCoverage` still labels the motion
segments "deliberately Unity's job"; that table is stale.

## 10. Phases, order, effort

Hours are implementation time for someone who knows the code; the user's test time is extra. Every
phase ends in a releasable state. Within a phase the steps are ordered; between phases the order
below is the recommendation, but phases 5 to 9 are largely independent of each other once phase 2
is done.

### Phase 0: safety nets (about 20-25 h)

| Step | Hours | What it gives |
|---|---|---|
| Save round trip (section 9.2 item 1) in the self-check | 4 | the oracle for every data-layer step |
| Baseline: run the self-check once and keep the full output (minus timings); every later step must produce the same set of OK/FAIL names | 0.5 | |
| `--art-record` / `--art-verify` golden hashes for every HUD builder; synthetic art checks skeleton | 4-6 | pixel-exact oracle for section 8.4 |
| `DumpAll()` of every `_UW*` shader global at a fixed frame for scripted scenes, plus reference screenshots (classic 4:3, Classic Wide, Classic+, modern; help open; conversation both schemes; map with notes; stats; runes; container with more than eight items; every options screen; colour filter off and on; Point and Bilinear) | 3 | oracle for the render and UI steps |
| Golden `settings.json` from the oldest fielded version plus a current one, with key, pad and stick-swap overrides; load, save, reload, compare 82 values | 1 | fixture for settings and input |
| Const snapshot: serialise every rule constant to a checked-in JSON and compare | 2 | catches a moved or mistyped constant |
| Harness extraction: `Check`, runner with per-check try/catch and timing, catalog with `NeedsData`, `StateGuard`, `FreshData()`; move the orphaned doc comments; fix the level-9 leak | 5 | fixes B14, B36; makes every later check verifiable |
| `UWResourceWatch` baseline counts (textures, materials) | 0.5 | leak oracle |

Phase 0 status (2026-10-11): built, except the user's part.

- The self-check has a harness (`Tools/UWSelfCheck/Harness`): a catalog with tags and
  `NeedsData`/`MutatesData`, a runner with a fixed seed and a state guard per group, try/catch
  and timing per group, `--only`, `--skip`, `--tag`, `--list`, `--quiet`, `--no-data`, `--times`,
  `--seed`, `--saves <folder>`, `--record`. B14 and B36 are fixed by it. The OK/FAIL set of the
  run before the change is the baseline (kept privately).
- `SaveRoundTrip`: identical for the nine DATA levels and for every save folder tried (the
  writer is byte-exact today). `Constants`: 2,144 entries in `Pins/rule-constants.txt`.
  `HudArt`: 193 hashes in `Pins/hud-art-hashes.txt`. Re-record with `--record` only after a
  change that is meant.
- `Shift+F12` in the game writes the shader globals, camera flags, render mode and resource
  counts to `persistentDataPath/shader-globals-<time>.txt` (`UWShaderGlobalsDump`). The resource
  counts are the `UWResourceWatch` baseline.
- Settings fixtures in `Tools/Fixtures/settings` with `Assets/Editor/UWSettingsFixtureCheck`
  (menu UW / Check settings fixtures, or `-executeMethod UWSettingsFixtureCheck.Run`).
- Still to do by the user: the reference screenshots and one globals dump at each spot listed
  in the table, before phase 5 and 6 start. The `WritePosition` identity waits for an
  engine-free `SavedState` (phase 2).

### Phase 1: dead code, bug fixes, small merges (about 35-45 h)

| Step | Hours | User test |
|---|---|---|
| Delete the dead code of section 3 except the old movement path (that is phase 4) | 4 | drop and throw onto floor, water, lava; talisman into the volcano before and after the burial; Tremor boulders; trap bolt on level 8; a goblin fight; look and use |
| Bug fixes B1, B3, B12, B19, B24, B25, B32, B34, B35 (each its own commit) | 6 | silver tree pass-through; creature area spells on level 7; landing and lava sounds (audibly different: the original's volume); ward rune; level change after much throwing; Remastered normals after changing height strength; setup dialogs one at a time; the sweep-mask repro (level 1, 6/31, looking north-west) |
| `UWObjectIds`, `UWLevelIds`, `UWQuestPlaces`, `UWStringIds`, time units, max quality; replace every duplicate declaration and range predicate; decide the cup level (2 vs 3) and the Void (8 vs 9) once | 6 | build; level 9 void effects; cup of wonder; bullfrog; the silver tree |
| `UWBinary`, one XOR loop, `UWObjectChain`, `UWDice`, `UWMath`, restore share, conversation `fArgument`, bullfrog defaults; the clock-unit decision (B9) | 6 | round trip identical; barter with the Gray Goblin; Shak repair; potion trap; game_time in a conversation |
| `UWStackRules` (fixes B7), `UWText` safe lookups and bare names, `UWItemMessages`, one scheme test, container-rejection report | 4 | "cannot be used on that", "is too full", rune-bag refusal in both schemes; barrel empty/locked; "Move how many?" 2 and 99; eating a stack on the head; enchanted stacks |
| `UWRandom` everywhere (B20): `UWCritter`, `UWDeath`, `UWVoidEffects`, `UWEndgame`, `UWContainerDamage` (via `RollDebrisObjectId`), `UWLevelLoader.GetRandomPositionInTile`; re-baseline the seeded checks in the same commit | 3 | goblins and rats wander and attack; bones on death; void flashes; smashing a chest |
| Static leaks (B21): reset the lava quirk, the cast pause and the light cache on New Game and Load | 1 | New Game -> Load -> New Game; cast pause after a load |
| Conversions: `UWUnits` (world to tile, eighth, zpos, tile centre), `UWHeadings`, `UWWorldScale` for the 32 scale aliases, `GetTile` everywhere; only numerically identical copies first, each differing copy a separate commit after checking the original | 5 | doors, plaques, items at tile edges; throw direction vs compass; save/load/save identical bytes |
| Self-check additions for reach, item applications, lock pick and key, `CanCarry` with count, `TakeFromStack`, `DropCursorItemAnywhere`, `TryConsumeOne`, `TryEat` | 3 | uwselfcheck |

Phase 1 status (2026-10-12): built and tested by the user (39 points, all passed or explained
as the original's behaviour; six faults found on the way are fixed and confirmed - options in
the Void, smoke at a bag, the flash on a struck model, a menu across the scheme change, a sprite
through a door's lintel in Remastered, the anvil sequence's frame). Every step of the table is in; the
differing conversion copies are decisions 20-24; the respawn mark (bit 8 of word 0x0D) now
survives the save, found by the user's test. The self-check has five more groups (Reach,
ItemApplications, LockRules, InventoryModel, Eating).

Between phase 1 and phase 2 (2026-10-12, agreed with the user): `UWWorldPicker` of 7.3 was pulled
forward - one measurement for looking, using, taking and the target spells, one crosshair
(B18 fixed), the three raycasts and their helpers gone from Interaction. Built and confirmed by
the user the same day.

### Phase 2: engine-free foundations (about 60-75 h)

| Step | Hours | User test |
|---|---|---|
| Codecs: `UWObjectRecordCodec`, `UWNpcRecordCodec`, `UWLevArkLayout`, `UWLevArkCodec`; writer iterates the ordered list instead of a dictionary; decide B13 with a round-trip pin | 6-8 | byte identity; save in ours, load in the original (SAVE1-4) |
| `UWObjectFactory` plus `UWObjectAppearance`, `GiveToPlayer` (fixes B8, decides the shrine quality) | 4 | fish, cup, seed, shrine gift, loot, trade item, missiles; Key of Truth with the hand full and empty; quality words |
| `UWPickupRules` plus `UWWorldPickup` (fixes B15, B16, decides whether the classic count-aware weight check is the original); delete `Interaction.cs:1343-1346` | 4 | classic stack drag with count; too heavy; too far; theft; moonstone; pouch ambush level 3 49/52; modern E, menu, drag; bought goods in both schemes |
| `UWItemTargetRules`, `UWWorldUseRules`, `UWItemTargetUse` (one applier), `UWCarriedItemUse`; lock lookup in one place | 5 | pole on switch; key on door and chest; lockpick; spike; rock hammer x4; orb rock; oil; anvil; bones; Key of Infinity; fountains; cauldron; shrine; inscription |
| `UWDoorRules` and the Unity `UWDoor` component; switch the 13 callers; decide B10 and B11 against the original | 6 | Drog's grate (level 1, 15/33); talking door (level 6, 4/2); bashing doors (goblin and player, spiked); Unlock spell; lockpick; key; spike; creatures opening and picking doors; night closing; Gronk |
| `UWSpillRules`, debris through `UWObjectDamageRules` | 2 | spill a barrel (level 2, 24/36); corpse loot; smashed chest |
| Session skeleton (`UWGameData`, `UWGameSession`, `UWGameSession.Current` facade; statics forward; reset on New Game and Load) | 8-10 | New Game -> Load -> New Game; quest and variable puzzles (level 5 counting puzzle); a "two sessions share nothing" check |
| Session RNG (own seeded PRNG; route `ICritterHost`, `IUWMotionWorld` and the VM through it; the barter's global draw fixed) | 4-6 | re-baselined seeded checks; combat feel unchanged |
| Split `DataImport` into `UWGameData` + `UWSaveCodec` (`DataImport` stays a facade) | 10-14 | load all saves; new character; save and reload; the round trip |
| Registries: conversation functions, trap and a_do tables, item use and food tables; a dispatch-coverage check (every name and id handled or logged) | 10-15 | talking door; bullfrog; Garamon burial; food messages; every trap kind on levels 1-3 |
| `UWPlacement` on `UWRandom`; merge the three eighth formulas | 4-6 | kills beside walls and steps; spill; barter drop; drop on a bridge; diagonals |
| `UWWorldEdit` with events, `UWWorldView`, `RemoveWorldObject` folded in, `WipeAll` releasing slots; effects off the model path (after checking the original's class-7 handling) | 8-11 | pick up and drop; delete and create-object traps on level 1; Armageddon; culling in a full level (SAVE4 level 7 sack throw); flashes, blood, splash, smoke over lava; round trip and in-game save |

### Phase 3: hosts and world (about 40-50 h)

| Step | Hours | User test |
|---|---|---|
| `IUWWorld` / `IUWPresentation` / `IUWPlayerAvatar` phase A: level, tiles, `UWSpot`, messages with a silent scope, sound, effect, shake, flash, cutscene, travel; existing hosts delegate | 5 | a trap of each kind on levels 1-3; teleport; quake; dream; backfire; gronk door; barter drop |
| `FindCreatures` over `UWCritter.Active` replacing the four scene scans; physics queries left as they are | 3 | theft and trespass messages; creation-trap crowding; sleep ambush; Detect Monsters |
| `World.Damage` with `UWBlow` for every object and creature site; `UWMissileHitRules` | 4 | arrow and missile on creature, player, door; fireball blast; Flame Wind; Smite Undead; Poison; needle trap on a thrown wand (level 3) |
| `UWWorldScheduler`, `UWEndgameSequence`, `UWTriggerPacer`, respawn on the player tick | 6 | burial on level 5; talisman flames; void launch (step seconds 0 and 0.1); damage-trap hit rate at 60 and 144 fps; respawns over ten minutes and while sleeping |
| Split `UWProjectileWorld` into `UWMotionHost` and `UWMissiles`; slim `UWProjectileFlight` | 3 | all missile cases above; push and knock-loose; debris flying on; walking into your own missile |
| Shrink the interfaces (remove `IUWSleepHost`, shrine and special-death hosts, `IUWEquipment`; `IUWTrapHost` and `IUWSpellHost` to their specific members); `FakeWorld` and the new checks | 10-12 | self-check; repeat the smoke run; sleeping (bedroll, hunger, ambush, night doors); every shrine mantra |
| Explicit session parameters domain by domain, remove `Current` | 20-30 (spread over later phases) | per domain |
| Optional: area and blast targets by record tile instead of physics; compare with the original | 2 | fireballs at walls and corners; Flame Wind on crowds |

### Phase 4: player and creatures (about 70-85 h)

| Step | Hours | User test |
|---|---|---|
| Remove the old movement path; "no world: stand still"; delete knockback; one foot offset; `GetFeetZPos` from the core | 5 | main menu (no falling, no footsteps); walk and run in both schemes and pad; wall slide; jumps (standing, right-click); falls 1/2/4 tiles; levitation ring and spell; fly; slow fall; swim; lava; easy-move keys and compass arrows; noclip; level change; load next to a step, on a slope, in water; Original and Smooth |
| `UWDamagePipeline`, sites one at a time with self-checks; decide the two sleep questions (B5) | 5 | melee easy and normal; arrows; own missile; fireball; lava with and without boots and Flameproof; fall; trap; leeches; cursed crown; backfire; shake, flash and dragons |
| Death as an event, `UWDeathRules` | 2 | talisman flag 0; silver tree; final death; lava; poison tick; Void |
| `UWPlayerTerrainRules`: one swim counter, drowning on the tick, lava per tick | 4 | long swim loaded; save while swimming; lava rate Original and Smooth at 60 and 144 fps |
| `UWVoidRules` | 1.5 | level 9 |
| Split `UWCharacter`; status on change | 5 | weapon states, bow, gem, paper doll, flasks, levitation ring, cursed crown, light spell, maze crown |
| Split the rest of `UWPlayerMovement`, `UWMotionStepper` with self-check; fixed Update order | 5 | the movement test again; levitation while moving; water with mouse look |
| Creatures: explicit attacker in `ApplyDamage`; registry dictionary; HP single source (B23); position single source and the teleport double write; `UWWorldSync` from the record | 10 | archer arrow hitting another creature; summoned mage vs reaper; player miss angers; lava burn; golem veto and HP 125; Tybal orb weakening (level 7); save/load wounded goblin; level-exit healing; fireball on a tile edge; save/load next to walls; flier on goal 3 (Slasher) |
| `UWAttackResolution` shared by creature blow, player melee and missile-on-player | 7 | player vs goblin sounds 7/8 leather vs metal; goblin on easy halved; red flash on critical; poisonous creature; goblin on door and barrel; Slasher armour |
| Creature casting through `ApplyEffect` with a creature spell host (B2, B3) | 6 | level 7 mages (orb up and down), imp and gazer missiles, fire elemental, acid slug, Flame Wind, mage summoning |
| Extract `UWCreatureActor` and `ICreatureWorld` (queries, movement, doors, combat, death) | 14-20 | level 1 goblins; level 3 lurker; bats; drowning (SAVE4 pool); Tybal; Void Slasher; save/load mid-fight SAVE1-4 |
| Death pipeline as one actor state machine (B22); `UWCritterRemains` loses polling and fallback | 4 | kill by player; drowned goblin; golem veto; far kill by another creature; kill then Freeze Time |
| Optional: shrink `ICritterHost`, update `FakeCritterHost` | 3 | self-check plus a level 1 fight |

### Phase 5: UI, both schemes (about 130-160 h)

| Step | Hours | User test |
|---|---|---|
| `UWUiSorting`, `UWModernPalette`, `UWInterfaceFont.Modern`, `UWControlScheme.IsModern`; weapon ids, message ids, paperdoll and icon-slot tables engine-free | 4 | all four presets plus classic: panels, bags, conversation, question box, editor overlap; colours |
| `UWUiKit` and one font; replace the helpers part by part | 6 | screenshot parity per preset; font toggle; outlines and shadows |
| `UWClassicLayout` table (constants only); point `UWHudArt`, `UWAutomapPainter` and the modern original look at it | 4-6 | screenshots identical pixel for pixel; Classic Wide shift; help slide |
| `UWTextureFactory`, `UWGrTextures`, `UWIconCache`, `UWArtCache`, `UWVersionedTexture`; fix the icon and rune leaks; decide B30 | 8 | colour-vision switch; backs in the editor; rings, badges, pin, minimap buttons; panel turn, compass, map parchment, container arrows, torch flicker; resource-watch counts flat; the Classic Wide frame under the colour filter if the user chooses consistency |
| `UWTextTextures` and the `RenderLines` buffer rewrite; migrate labels one by one (numbers, stats, map, log, conversation, menus) | 6-8 | F toggle on every field; weight and quantity offsets; stats right edge; [MORE] position; answer click rows |
| Small engine-free helpers: open-variant id (B29, a behaviour change for the modern page, ask first), remaining weight, `BodySlotOf`, class and skill names, ordinal | 2 | modern page container picture; weights; stats |
| `UWMessageLogModel` for both schemes; choose the [MORE] and blink policy; fix B27 and B28 | 5 | long look texts paging in both schemes; Classic Wide; green and red save messages; cursor |
| `UWInventoryView` | 5-6 | drag and drop; containers with more than eight; counts in hands and rings; scroll arrows |
| `UWCharacterSheet`, `UWPanelTurn` | 5-6 | the 8-step turn with edge and chain; live stats; skill paging; conversation forcing the inventory side and restoring it; help window stats |
| `UWRuneShelfModel`, `UWSpellCasting.CanCast` | 3-4 | select, clear, look, cast, F8; modern hollow; cell borders; "not in your bag" text |
| `UWAutomapView`, `UWMapNotesModel`, `UWTextEntry` for all five inputs (B26) | 6-8 | notes write, erase, page; quill; Linux typing (VM); minimap; help map tab look |
| `UWConversationView`, `UWConversationLayout`, controller/renderer split, modern reads the view (B31) | 10-14 | Ketchaval wrap and indent; [MORE] after 12 lines; `\m` pauses; babl_ask reprint; trade crosses; 2 s look text; gamepad; modern both looks; help open during a conversation |
| `UWCountInput`, `UWYesNoQuestion`, `UWCountPrompt`; `UWPromptController`; `UWMessageLog`, `UWLookService` behind the `Interaction` facade | 8 | shrine classic (Esc, mouse ends, swallow); modern OK/Cancel; anvil yes/no; trap found in Look; pad A/B; count prompt all inputs; split box |
| `UWWorldPicker` (fixes B18, B34), `UWClassicWorldGesture`, `UWModernWorldInput`, combat classes (`UWPlayerAttackInput`, `UWPlayerMelee`, `UWPlayerMissiles`, `UWHitFeedback`), `UWPendingSpell` (B17), `UWWorldUse` | 16-18 | bread beside barrel; boulder vs blood stain; loot under blood; Slasher sparks; steep pickup; "You see nothing." at the light limit; Classic Wide crosshair; charge times; stab, slash, bash; door bash with spike; wall flash; no arrows; water mid-strike; critical wear; fireball classic and modern; Name Enchantment on a pack item; spell trap; wand cooldown |
| `UWItemDrag` split (`DragState`, `DragSource`, `UWItemUseHost` per call, `ReturnCursorItem`, trade adapter) | 8-10 | every drag source; sticky swap; invalid drop; scroll arrows; container icon; eat on head; drop and throw thirds; scheme switch mid-drag; barter refusal both schemes |
| `UWModernGameMenu`, `IUWModernPart` base, split `UWModernHud`, `UWModernDockedPanel`, `UWModernUi` root | 23 | Escape/F1/Ctrl+S/R; save name; load; presets; Customize round trip; pad stepping; every element dragged and sized; flasks and gem apart; stone shelf; rune hollow click; strip on hit; spells hover, click, right-click; crosshair, ring, pad prompt; window and grave pictures; handle peek, open, close; pin; tabs; free vs docked; Tab help; Y runes; LT tap and hold; Escape order; conversation, big map, cutscene, main menu, Shift+F2 mid-drag; drop into world vs panels; pad B |
| Split `UWModernBags`; `UWGameActions`, `UWPointerCursor` | 14 | drag and click-click; split; context menu; anvil use; trade; open-all; save/load with open bags; Classic page mode; F9/F10 both schemes; Sleep and Track buttons; cast via F8, LT, hollow, bar; status clicks; cursors |
| Shrink `UWGameUI` (`UWViewportCamera`, `UWClassicGeometry`, `UWPointerZones`, `UWTextEntryState`, `UWClassicHud`); `UWOptionsMenuModel`, `UWDragonAnimator`, `UWSaveFlow` | 12 | cursor zones and speeds; FOV 4:3 and 16:9; help shift; pad pointer clamp; typing locks; Ctrl shortcuts, key highlight, quit, detail and music screens; all three dragon animations; save and load from every menu |
| Optional: `IUWUiScheme` with Classic and Modern | 10 | full regression both schemes plus the big map |

### Phase 6: rendering and audio (about 55-70 h)

| Step | Hours | User test |
|---|---|---|
| `UWShaderIds`, `UWShaderGlobals`; replace the 59 call sites; per-frame sets become on-change | 6-8 | palette dither rings on level 1; hallucination (scramble, palette, dark); Remastered lights and ghosts; the globals dump identical |
| `UWTextureFactory` tables, `UWTextureCache`, `UWPaletteCommon` with `TableSet` | 6-8 | icon rotation (flames, light spell); colour-help modes; Night Vision (level 5 light); Remastered normals after changing height strength |
| Quick wins: sweep-mask early-out, cached camera data, no closure allocation in `fUpdateShadows` | 2-3 | the sweep-mask repro; a skeleton at the side of the Classic Wide view |
| `UWLightSourceTable`, `UWFlicker`, shared ground-shadow rule | 4-5 | torches, campfire, lava glow, ground blobs under creatures in both modes |
| `IUWRenderPath` and host; port Palette, then Remastered; replace the nine pollers; camera flags and SSAO in the host | 17-21 | every palette effect preset (Off, Subtle, Full); objects spawned after level load; switching Low to Very High in game; a save loaded at each level; player light shadows; world resolution on and off; Roaming Sight; SSAO both modes; a Windows player build in both modes |
| Audio: `IUWMusicDevice` and the three devices, `UWEffectSynth`, `UWAudioMixer`, `UWAudioSettings`, listener guard, `UWAudioService` | 12-16 | AdLib, General MIDI, MT-32: intro, level themes, combat themes, switching device mid-piece, mandolin and flute, door and hit effects at distance, water loop, music and sound toggles |
| Comment and dead-code cleanup (F6 references, `UWSoundPlayer.PlayAt`, the duplicated UV comment) | 1-2 | none |

### Phase 7: input (about 45 h)

| Step | Hours | User test |
|---|---|---|
| `UWTextEntry` replacing the six sites (if not already done in phase 5) | 4 | character name, save name, mantra, conversation answer, map note, pad letter grid; the same on the Linux VM |
| `UWBindingInfo`; `Easy*` follow `Move` rebinds or document why not | 3 | Controls dialog, help text, pad hint strip, with rebinds and each pad style |
| `UWHoldTap` / `UWRepeat` | 3 | E tap and hold; pad panel tap and hold; pad cast tap and hold; split box and count prompt on pad; letter grid repeat |
| `UWInput` facade with `UWAct` and the context stack (first a thin wrapper, then the typing lock as contexts; the lock list's exact set kept as a test) | 8 | everything keyboard in both schemes; typing in every text box; rebind and reset |
| `UWHotkeys` table and one Escape/back chain | 6 | F1-F10; Ctrl shortcuts (classic Ctrl, modern right Ctrl); M, F, Tab; Esc in every open state; pad B |
| Pad raw reads into actions | 3 | each pad flow, with the pad rebound too |
| `UWPointer` part A (read-only redirect of all 83 reads) and part B (ownership of warp and cursor state; part B after phase 8b) | 12 | classic cursor walk and clamp; drag with held button; modern lock and free; hold-to-look; pad pointer in both schemes; item in hand; Linux pad pointer |
| Remove `Takes()`, `SwitchedFrame`, `ConsumePress` latches in favour of the context | 4 | the pointer tests plus right-click on every window |

### Phase 8: menus, settings, help, intro (about 55-70 h)

| Step | Hours | User test |
|---|---|---|
| `UWGuiTheme`; single dialog enum for the bar | 6-8 | screenshots of every dialog, help tabs, editor window; main menu, first run, in-game: open and close every item |
| Widgets; port the dialogs one at a time | 8-10 | every row changes, persists, snaps; Motion greyed rows; rebind and conflict notice |
| `UWUserData` plus `UWSettingDef` table with `Normalize` and clamps | 6-8 | old file loads identically; editor open, change, Discard, Save; start without a file |
| Page tables; layout inspector on the shared controls; presets as tables; snapshot with the 24-key check | 8-10 | every part's inspector rows; presets; setup unchanged |
| `UWText` table and extraction (about 750 ids) | 6-8 | whole UI the same; a test language with long strings |
| Help tabs as classes | 5-6 | all seven tabs; notes Tab; map zoom; reading mode; modern panel and classic frame |
| `UWCutsceneRunner` with self-checks; split `UWIntroPlayer` | 14-16 | logos, intro, dreams, death cutscenes, end sequence, skips |
| `UWSettings` overlap cleanup; merge `fChooseLevel` with `fClickDetailEntry` | 2-3 | music and sound persist; detail levels in bar and classic options |

### Phase 8b: the IMGUI windows onto the kit (about 15-25 h, added 2026-10-24)

Decision 26 moves the IMGUI windows to the same kit as the modern interface; phase 5 rebuilt the
HUD, the bags and the panels, but the modern game menu, the setup bar with its dialogs, the help
window, the layout editor's window and the letter grid stayed IMGUI (found 2026-10-24, decided per
user the same day: after phase 8, before phase 7). Phase 8 leaves them table-driven with one thin
IMGUI drawer; this step swaps that drawer for one on the kit, window by window. The debug overlays
(F1, Shift+F12) may stay IMGUI. Afterwards no window takes its clicks from the system pointer, the
precondition for `UWPointer` part B and the pointer of our own (section 8.5).

| Step | Hours | User test |
|---|---|---|
| Row, slider, choice, toggle, tab and frame pieces on the kit, fed by phase 8's row and page tables; label columns and bar items sized by their text (the pseudo-language of 8.5 showed fixed widths clipping, 2026-10-24) | 5-8 | one dialog side by side with its IMGUI form; the pseudo-language (`-language pseudo`) without clipping |
| Port window by window: modern game menu, setup bar and dialogs, help window, layout editor window, letter grid | 10-17 | each window: every row, scaling, pad stepping, typing; screenshots for the user's approval |

### Phase 8c: the debug display on the kit (added and done 2026-10-25)

Per user (2026-10-25: "for UW2 the debug display will be needed more often again"): the F1 report
(Shift+F1) and the debug tools (Shift+F7, Shift+F10) on the kit, in folding sections built by an
engine-free report model (`UWDebugReport`); afterwards IMGUI remains only in the editor tool
`UWLevelWindow`. Done and user-tested the same day.

### Phase 8d: window themes (about 10-15 h, added 2026-10-25)

Per user (2026-10-25): the game's own windows get a second look. Today's look stays as an option;
a new theme takes the colours of the application icon (`Art/Icon/ur-icon.svg`): the stone's
grey-brown (radial #8a7f70 - #5e564c - #2f2a25), the warm torchlight from the top left (#ffb347,
faint), the ember of the runes for accents (#ffe29a - #ffa53a - #d9531e) and the light edge
(#b3a68f); the background as a colour gradient or a cloud pattern. Placed after phase 7 and before
phase 9 because it works on the kit drawers and `UWGuiTheme` that phases 8, 8b and 8c just built,
and needs nothing of phase 9.

- `UWGuiTheme`'s fixed palette becomes a theme table (engine-free: colours per role - background,
  bar, panel, hover, button hover, separator, text, dim text, accent, overlay, reading shade, error -
  plus the background fill: flat, gradient with its stops and light, or clouds); the kit drawers
  read the current theme, so a switch takes effect at once.
- The cloud pattern is generated, not painted: seeded fractal noise in the theme's colours, made
  engine-free as pixels (like the HUD art builders) and uploaded once per size and theme; a faint
  grain as on the icon's stone. No new art files.
- A setting for the theme and one for the background (gradient or clouds), saved on change; the
  settings fixtures gain the keys.
- Applies to every window on the kit: the setup bar and its windows, the modern game menu, the help
  window, the layout editor's window, the letter grid. DECIDED 2026-10-25 (per user): the modern
  HUD's panels and bags keep their layout-editor colours, the debug panel stays plain, the two
  settings sit in the Colours window, and today's look is the default.
- Text stays readable over the pattern (contrast checked per role, in the pseudo-language too).

| Step | Hours | User test |
|---|---|---|
| Theme table, `UWGuiTheme` reading it, today's look as the first theme (pixel-identical) | 3-4 | every window unchanged in the old theme |
| The icon theme with gradient and clouds, the generator, the settings | 6-9 | every window in both backgrounds, both schemes, small and large window, the pseudo-language; screenshots for the user's approval |
| Switching live, contrast fixes, the decided extras (HUD panels, debug panel) | 1-2 | switch while a window is open; restart keeps the choice |

### Phase 9: self-check split, tools, art builders (about 60-75 h)

| Step | Hours | User test |
|---|---|---|
| Move the fakes; merge the walker fakes, conversation hosts, record factories; split `Program.cs` into `Checks/`; regenerate RULES-INDEX; decide whether the rule-index scan skips the self-check | 7 | baseline diff identical |
| UWDump command registry and the new commands; delete the ten editor scripts | 7-8 | each command once; the project opens with 0 errors |
| Table-driven rewrite of the repeated motion scenarios | 2-3 | baseline diff identical |
| `UWCheckLib` split and a `UWModCheck` skeleton | 3 | when the mod work starts |
| HUD art: dead code and debris; `partial` split; `UWMask`, `UWPixelOps`, `UWSeededRandom`, `UWNoise`, `UWMaskText`, `UWPixelRules` switched in one builder at a time with `--art-verify` after each; `UWMainFrame` and `UWHudTuning`; builder classes with `Compose` cores; `fCheckHudArt` | 24-30 | nothing expected; spot-check the conversation and compass disc with outline on and off; the compass and heading eyes in Classic Wide |
| Optional: share the grow algorithm (`PickBest3` first, `GrowFrom` only if every hash stays equal); `fMain` cache; engine-free scroll-paper builder | 9-20 | Classic Wide at extra 100/200/480, the stone shelf at 24/81/250, the compass disc, side by side with the old hashes; toggle the colour help live; resize the window in Classic Wide |

### Phase 10: rule set, mirror hygiene, namespaces (about 40-55 h)

| Step | Hours | User test |
|---|---|---|
| `UWRules` plus JSON, domain by domain (about 12 domains, about 2 h each); `rules-original.json` pin; all-keys test; `uwdump rules-template` | 20-28 | one difficulty tweak via `rules.json`; defaults unchanged |
| Mirror hygiene: integer vitals (today float HP and mana with mixed arithmetic; off-by-one risk after a load/save cycle), table arctangent for `FineHeadingOf`, `UWVanillaMath.Sqrt` in the brain, explicit rounding helpers, cast pause and quietness on game ticks | 8-12 | HP and mana display after save/load; sneaking; cast pause feel |
| Move art, audio and install (about 9,500 lines) to `UWDataImport.Presentation` / `.Audio` namespaces or asmdefs | 3-5 | build only |
| Explicit session parameters: finish removing `UWGameSession.Current` | remainder of the 20-30 h | per domain |

### Totals

| Phase | Hours |
|---|---|
| 0 safety nets | 20-25 |
| 1 dead code, bugs, small merges | 35-45 |
| 2 engine-free foundations | 60-75 |
| 3 hosts and world | 40-50 |
| 4 player and creatures | 70-85 |
| 5 UI | 130-160 |
| 6 rendering and audio | 55-70 |
| 7 input | 45 |
| 8 menus, settings, help, intro | 55-70 |
| 8b the IMGUI windows onto the kit | 15-25 |
| 8c the debug display on the kit (done) | - |
| 8d window themes | 10-15 |
| 9 self-check, tools, art builders | 60-75 |
| 10 rule set, mirror hygiene, namespaces | 40-55 |
| Total | about 635-795 |

Phases 0 to 3 (about 155-195 h) give the safety nets, every bug fix, all dead code, the session,
the codecs and the world interface: that is the minimum before mod support (`Docs/MOD-SUPPORT-PLAN.md`
phases 0-2 build on `UWGameSession`, `IUWWorld`, `UWRules` and the registries). Phases 4 to 9 can
be interleaved with releases. Phase 10's rule set can be pulled forward right after phase 2 if mod
support is wanted sooner; the rest of phase 10 is independent.

## 11. Risks and frozen behaviour

Frozen, user-confirmed behaviour (moved verbatim, verified by the phase 0 oracles):

- Pixel art: cloud leather variant C and its hash constants, leather frame and tab, paperdoll
  widening, heading strip and gargoyle mask, slot ring and paperdoll marks, `ComposePaperdoll` /
  `FreeOnPage` (many rounds of user feedback; depends on traversal order and the integer depth
  encoding), shelf gem height and dither rows, gem stand mask (user-painted 2026-10-10), stone
  shelf seams and seeds, Classic Wide cut columns and source ranges (from the user's screenshots
  2026-10-10) and the shared seed state across band calls, the hole flood and camera bounds,
  conversation header, parchment, knob rollers and each outline predicate, rune hollow, compass
  disc mask and regrown F pixels, compass cross mask.
- Classic HUD positions: container arrows, ring icon +0.8, armour zones, number offset +1 px,
  weight raise 0.25, window-picture corner, log colours, rune slots +1/+1, stats rows, the 8-step
  panel turn and its edge picture, history width 159, trade cross 3x3, map note -3/-3, quill
  offset, shadow palette.
- Rendering: interpolation on, checkerboard dither, diagonal stretch off, dither cell size from the
  window height, rotation steps per second, cutoff distance per light level (3 tiles without light,
  7 with the brightest), Night Vision from MONO.DAT at level 5 world only, the Void unlit,
  hallucination scramble and palette swap, XFER ghost mixing, painter order and sweep mask (level 1
  at 6/31 looking north-west), colour tables `linear=false` and index tables `linear=true`, no
  mipmaps on index textures, gamma colour space.
- Motion and input: the pointer zone numbers in `UWGameUI.fCursorMovement` (mirror UW.EXE),
  `IsCursorMovementInProgress` start condition, `ConsumesRightClick`, release-based clicks,
  `miOpenedFrame` guards, `ConsumesLeftClick` across the `UWItemDrag` update order.
- Audio: everything in `OnAudioFilterRead` runs under the lock on the audio thread (no allocation
  there); device switching mid-piece restarts the piece; MT-32 ROM discovery cache.

Structural risks:

- Determinism: the core, the brain, combat, loot and motion draw from the shared `UWRandom` in a
  fixed order; routing more draws through it (B20) and replacing the clock-seeded `System.Random`
  with a session PRNG shift every later roll (intended) and re-baseline the seeded checks (one
  commit). Keep `UWOriginalRandom` for the barter. Unifying the three grow algorithms changes
  pixels unless the consumption order of `fNextRandom`, the first-found tie rule, the enumeration
  order and the per-site parameters are kept exactly.
- Save bytes: `UWLevelWriter` maps objects by reference, so cloning or replacing a `UWObject`
  loses its slot; chain order comes from tile-list order (Place appends, Remove must not reorder);
  the active-mobile and whoami order; carried `SlotIndex` seeding the barter appraisal;
  `TakeStaticSlot` order; the dictionary loop at `UWLevelWriter.cs:522` (replace with the ordered
  list); NPC byte 0x11 bits stay raw; `SavedState` nullable "leave as is" semantics; float to int
  vitals.
- Tick order and physics: the driver walks `UWCritter.Active` in OnEnable order; damage is
  synchronous mid-tick; the path cache moving from `Time.time` to the clock changes refresh
  frequency; `UWTileBlast` and area targets decide by the transform tile while sleep, Detect
  Monsters and the motion core use the record tile (moving everything to records changes who gets
  hit at tile edges: a separate, checked step).
- Palette path: every runtime view needs `ApplyToNewObject`; the toggle recognises materials by
  reference and pairs atlases by `mainTexture`; `fCreateObjectMaterials` creates new materials every
  Start; shader globals caches can go stale after a texture is destroyed (call `Invalidate`).
- Classic Wide: `UWClassicWide.fApply` moves only direct children of the frame and reads places
  from `anchoredPosition`; introducing group nodes, changing a pivot or anchors changes the shifting;
  `fBuildPanelPivot` reparents by position +-2 px; creation order and sibling indices are
  load-bearing; the help window caps `Extra`.
- Texture ownership: callers destroy textures today; a cache must take ownership in one step or
  textures are destroyed twice; `fDrawPromptCursor` writes into its texture and must never get a
  cached one; label size drives the conversation hit areas.
- Serialized values: gone since decision 12 (2026-10-22) - `UWGameUI` has no `[SerializeField]`
  layout values any more, `UWClassicLayout` holds them (the view angle was 90 in the scene, not
  the code's 75). `settings.json` field names, `graphics.json` schema,
  `HelpTab` numeric ids, binding ordinals, `ModernLayout`/`ModernBacks` opaque strings.
- Host lifetime and scheduler: per-firing and per-cast state must not leak into the long-lived
  `UWWorld`; `AfterConversation` must handle "not yet open"; the moongate camera needs a per-frame
  driver even with rule steps on ticks; sounds moved onto the scheduler shift by up to one clock
  unit (62.5 ms).
- Rebinding and Escape: event dispatch changes who sees a press first; pin the Escape chain and the
  `SwitchedFrame`/`ConsumePress` latches with an explicit order or the context stack first.
- Build-time: keep the three keywords as `multi_compile`; `Shader.Find` relies on the Always
  Included list (one guid there looks hand-written and must be checked); test a player build in
  both render modes after the render-path steps; the Linux pad path (`XTest`, untested) stays as is.

## 12. Decisions for the user

Collected from the reviews; each blocks or shapes one step. None has been decided yet.

1. B5: sleep lava damage becomes `PlainFire` (so Flameproof and dragon-skin boots count)? Sleep
   poison respects Poison Resistance like the awake tick? Trap and drowning damage get a type?
2. B9: clock minute 0x3C00 or 0x3BC4 for the conversation `game_time`? (Check UW.EXE first.)
3. B10 / B11: Gronk and the door trap decide alike? Does the original close portcullises at night?
4. B13: fix the object-record flags mask in reader, writer and trade together (changes bytes for
   enchanted objects) or keep the 4-bit in-memory `Flags`?
   DECIDED 2026-10-12: `Flags` is the original's 4-bit field (bits 9-12, `SET_FLAGS` writes four
   bits, bit 12 is the enchanted bit) and the single truth; `IsEnchanted` becomes a view on bit 3;
   the writer writes all four bits. No byte changes for consistent data; the round-trip pin guards it.
5. B29: the modern character page shows the open-container picture (a visible change)?
   DECIDED 2026-10-20 (per user): yes - the modern character page shows the open container's
   picture, as the classic scheme and UWModernBags do (step 5.6).
6. B30: the classic Classic Wide frame follows the colour help like Classic+ (visible when the
   colour filter is on)?
   DECIDED 2026-10-19 (per user): yes - the Classic Wide frame and the eyes' picture follow the
   colour help like every other picture, and rebuild when it changes.
7. Shrine gift quality (today none; `UWObjectFactory` would give 0x28) and the two
   `NewObjectQuality` values (0x28 loot vs 0x3F summon): which does the original use where?
   DECIDED 2026-10-12: `CreateObj` (MAPADDR.C) gives every new object quality 40 (0x28), owner 0,
   link 1 with the quantity bit for stack classes 0 and 2; the conversation's create hack takes its
   quality argument and keeps 40 for -1. CORRECTED the same day by step 2.2 against the source:
   `creat_spell` (SPELLS.C) sets 0x3F in its non-creature branch, i.e. on the conjured FOOD; the
   summoned creature goes through `creature_obj_init`. So `UWObjectFactory` gives 0x28, conjured
   food keeps 0x3F (ours was right), the shrine what the conversation says.
8. Effects (blood, splash, smoke) without a slot: does the original reserve a slot for class-7
   objects? If yes, keep the reservation but skip the culling.
   DECIDED 2026-10-12: yes - `put_effect` calls `CreateObj(cls + FIRST_ANIMOBJ, 0)`, a static slot
   from the free list, and makes no effect without one. Reserve the slot, leave effects out of the
   culling count.
9. `TakeSlots` before or after make-room on level change (Start and TravelTo differ today)?
   DECIDED 2026-10-12: one order for both, the original's `GetLevel` (GAMEWRAP.C): `SavePlayerInv`,
   `Map_Load`, `RestorePlayerInv` - build the level, then `TakeSlots`, then make room only if the
   slots did not suffice.
10. Creature tick order: keep OnEnable order or switch to the original's active-mobile order?
    DECIDED 2026-10-12: the original's order - the active-mobile list saved in LEV.ARK (`nactive`,
    offset 0x7AFC, which `UWLevel.fLoadActiveMobiles` already reads); newcomers are appended as
    `Obj_Alloc` does. The driver walks that list instead of `UWCritter.Active`.
11. Area and blast targets by record tile instead of physics (changes who gets hit at tile edges)?
    DECIDED 2026-10-18 (per user): as the original - `gronk_area`/`process_area` (SPELLS.C) walk the
    object lists of the squares in the area, no physics; the class 7 spells take the first creature
    in that walk. Built as the first step of phase 4; checked afterwards in the original (fireball at
    a wall corner, Flame Wind on a crowd, Poison with two creatures side by side).
12. `UWGameUI` serialized layout values: move to the static table (losing Inspector tuning)?
    DECIDED 2026-10-22 (per user): everything leaves the Inspector. The layout values and colours go to
    `UWClassicLayout`; the classic scheme's view angle too - the Graphics menu's view angle (`UWViewAngle`)
    already overrides it, at Original it is the original's own; the map's CLOSE debug switch and the
    zero window-picture nudge go as well. The stale `mOChainHitRect` entry leaves `UW.unity`.
    BUILT 2026-10-22. Found on the way: the scene held the view angle at 90 degrees since the first
    commit while the code's default said 75 - the game always showed 90, so 90 is the constant
    (`Frame.HorizontalViewAngle`); the plan's claim that the scene equalled the defaults was wrong there.
13. `TryGetEntityAt` and `fApplyTargetSpell` skip the sight limit: intended?
14. Classic count-aware weight check for bought goods vs modern whole-stack: which matches the
    original?
15. `UWModernHud`'s own text colour vs the shared palette: keep the difference?
    DECIDED 2026-10-19 (per user): keep it, named as its own entry of the palette - phase 5
    changes nothing of the look.
16. Classic Wide resize cost: quantise `Extra` (visible as coarser steps while dragging the window)?
17. Message log [MORE] policy and cursor blink: one rule for both schemes, and which?
    DECIDED 2026-10-20 (per user): both schemes page by what the shown scroll holds, its lines
    less one for [MORE] (classic unchanged: four and [MORE]; the modern scroll follows its size,
    which fixes B28); the cursor blinks at one fixed rate in both, 0.4 s on and 0.4 s off (the
    classic box's; the original counts passes of its input loop, SCROLLIO.C, so its speed hung on
    the CPU), recorded in Docs/DEVIATIONS.md, Rules.
18. `Easy*` actions follow a `Move` rebind?
    DECIDED 2026-10-25 (per user): yes - the easy steps take the keys of the walk and turn actions
    they belong to, with Shift (classic) or EasyModifier (modern); X stays its own key for the step
    that does not go off a ledge. No new rows in the Controls window. Built in phase 7.
19. `Docs/CLEANUP-PLAN.md` is public; the detailed per-area reviews stay private. Keep it that way?

Found by the conversions step of phase 1 (2026-10-11), copies that differ and were left as they are:

20. World to tile: `UWTileQueries.WorldToTile` rounds (half to even) and clamps to the map,
    `UWUnits.WorldAxisToTile` floors `(w + 32) / 64` unclamped. The original floors (`pos >> 8`,
    as the motion core does). They differ only on an exact tile boundary and off the map. Switch
    `WorldToTile` and `UWLevelLoader.IsInsideRock` to the floor?
    DECIDED 2026-10-12: the floor, as the original and the motion core do.
21. World to eighth: `UWTileQueries.WorldToEighths` inverts the draw offset (the spot an object
    was stored at), the trespass trap floors the eighth the point lies in (the original's
    `(pos >> 5) & 7`). Both are right for their purpose; name the second `WorldToEighthFloor`?
    DECIDED 2026-10-12: keep both, the second named `WorldToEighthFloor` with its purpose in the
    comment (a point's cell, the original's shift), the first stays the stored spot's inverse.
22. A thrown item lands at eighth + 1.0 (`UWPlayerThrow`), every other placement at + 3.75 (the
    original's `+0xF`). Using the common formula shifts a throw's landing point by 2.75 units;
    test "throw onto a floor edge" before changing.
    DECIDED 2026-10-12: the common formula (+0xF); the user tests a throw onto a floor edge before
    and after.
23. Camera yaw to the 256-step heading: the throw truncates (the original shifts the 65536-unit
    yaw `>> 8`), the compass and `UWProjectileWorld.AimFromDirection` round. Keep the throw's
    truncation (the original's reading) and let the compass round?
    DECIDED 2026-10-12: one conversion "degrees to the 256-step heading" by truncation
    (`pp->heading >> 8`, OBJPHYS.C) for the throw AND the compass; the compass then does exactly
    the original's `(v + 8 & 0xFF) >> 4` (INTERACT.C) on it. `AimFromDirection` keeps rounding:
    the original derives those headings from its arctangent table, not from the player's yaw.
24. `UWCritterAnimator` floors the yaw to the 65536 units, the player's movement rounds (chosen
    2026-10-10 so 44.99998 becomes 8192); the animator only needs the sixteenth. Harmless to
    switch, not identical.
    DECIDED 2026-10-12: the animator rounds like the movement.

Found by step 2.6 (2026-10-12):

25. Using a container in the world: the original's drop_link_chain puts the WHOLE chain down, lock and
    triggers too, and the use trigger (checkTrap kind 4 after UseCont) then finds an empty chain;
    ours kept lock and triggers in the container and fired the use trigger.
    DECIDED 2026-10-12 (per user, tested on the original): the pouch on level 3 at 49/52 holds a
    pick-up trigger that sends a headless; used in the world and then picked up empty, it sends
    nothing in the original, ours sent the headless. Now everything goes down as there; a smashed or
    burnt container fires its use triggers first (damage_object, checkTrap kind 4 before
    remove_object).

26. Which UI technique, also with a view to Linux, macOS and other engines?
    DECIDED 2026-10-19 (per user): the platforms are not the question (uGUI, IMGUI and UI Toolkit
    all run on Windows, Linux and macOS); the interface must not hang on Unity, for the Unreal front
    end beside Unity (Todo 11.7) and a possible SDL host (11.8). So: what to show, what a click does
    and every measure live in engine-free view models and tables (section 8.3); the CLASSIC scheme
    is drawn completely into our own engine-free pixel buffers (UWPicture and the HUD art builders,
    the text textures), Unity only uploads the finished picture; new kit pieces (the texture
    factory, the text textures) build on those buffers. The MODERN scheme stays on uGUI for now, as a
    thin renderer over the same view models; whether it becomes an own pixel UI too is decided when
    an Unreal or SDL front end becomes concrete. No move to UI Toolkit. The IMGUI windows move to
    the same kit over phase 5 - in fact phase 5 left the game menu, the setup bar, the help window,
    the editor window and the letter grid on IMGUI; they move in phase 8b, after phase 8 made them
    table-driven (per user, 2026-10-24).

27. Rune hit areas: classic hits the 16x16 rune pictures (with two extra columns and a shared row),
    modern the 14x14 compartments, both with gaps; the original (mous_in_rune, UnderworldExhumed
    RUNES.C:117-127, INVPANEL.C:161) tiles the page into gapless 18x15 cells.
    DECIDED 2026-10-20 (per user): as the original in both schemes - gapless cells (the modern
    shelf's cells mapped onto its compartments); the clear strip already matches.

## 13. Bookkeeping

- Each phase gets a Todo.md row; each step a commit referencing the step ("cleanup 2.4
  UWDoorRules").
- `Docs/RULES-INDEX.md` and `Docs/ORIGINAL-COVERAGE.md` are regenerated whenever comments citing
  original routines move (`dotnet run --project Tools/UWRuleIndex`), in the same commit.
- Public exports follow the usual recipe; this document goes out with the next export.
- `Docs/MOD-SUPPORT-PLAN.md` is updated when the session, `IUWWorld`, the registries and `UWRules`
  land, because its phases 0-2 then start from those seams instead of the current hosts.
