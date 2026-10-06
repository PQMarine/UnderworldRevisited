# The creature AI of Ultima Underworld 1 (UW.EXE)

2026-09-19. One engine-agnostic description of the creature AI, put together from five
separate readings of it - the tick and the schedule, the goals, perception and temper,
movement, combat. Where those readings disagreed, the IDA disassembly of UW.EXE decided
(section 11); the five texts themselves were dropped 2026-09-22 because this one supersedes
them and is the one the code cites. Every fact carries its routine name and a line
number into `UW1_asm.asm` in brackets; line numbers are locating hints, routine names are the
reverse engineer's (`Name_segNNN_BASE_OFFSET`), labels are `segNNN_BASE_OFFSET`. The
disassembly itself is not quoted anywhere in this document. The MIT recreation UnderworldGodot
was a reading aid only and is not evidence.

## 0. Scope, sources, units

Scope: everything a creature (mobile object of class 1) does on its own: when it is updated,
how it perceives, how its temper changes, its goal state machine, how it moves on the tile
grid, how it fights and dies. Not in scope: conversations (only where they write goals or
attitudes), the player's own combat, the projectile physics beyond what a creature needs.

Units and conventions used throughout:

- Tile: the level is 64 x 64 tiles. A tile is 8 eighths; a creature's fine position inside the
  tile is 0..7 on each axis (word 2 bits 10-12 y, bits 13-15 x of the mobile record). The
  coordinate globals are `tile * 8 + fine` in eighths [`NPCInitialProcessing_seg007_2488`
  snapshot, 52900-52960; `CurrObjXCoordinate_dseg_246E`].
- Squared distances come in two units. "Squared tile distance" is `dx*dx + dy*dy` of the tile
  numbers (`dseg_5c99_2450`, set by `GetDistancesToGTarg_seg007_326B`, 54088-54190). "Squared
  eighth distance" is the same over the eighth coordinates (32-bit in `dseg_5c99_2454:2456`,
  same routine). Every threshold below names its unit.
- Heading: two encodings. The fine heading is a byte with 256 steps per turn (byte 9 of the
  record, 0 = +Y (north), clockwise, 64 = +X (east)). The facing is 0..7 in word 2 bits 7-9
  (eighths of a turn, same origin and sense) plus a 5-bit residual in byte 0x18 bits 0-4; the
  full facing is `eighth * 32 + residual` [`seg007_1798_1FDC`, 52016-52040].
- Height: zpos is the 7-bit height in word 2 bits 0-6; "floor height" or "height level" is
  `zpos >> 3` (word 2 bits 3-6), the unit in which tile floors are stored [unit conventions
  of section 0; `NPCInitialProcessing` 52900-52930].
- RNG(n): the game's `RNG_seg005_DE7` followed by an integer division by n, i.e. a uniform
  0..n-1 (the remainder). "1 in n" means `RNG(n) == 0`.
- Creature table row: 48 bytes per creature kind at OBJECTS.DAT 0x132 + 48 * (item id & 0x3F),
  addressed as `Critters_0_dseg_4A52 + row * 48` [`NPCInitialProcessing` 52586-52610]. "Table
  byte 0xNN" means a byte of that row; "byte 0xNN" alone means a byte of the 27-byte mobile
  object record.
- Player: mobile object index 1, item id 0x7F; its own creature row is row 63
  (`PlayerCritterData_dseg_7272`, 383395-383421). gtarg 1 means "the player".

## 1. The tick

### 1.1 Time base and slot clock

- The PIT counter (`PITTimerGlobal_dseg_5c99_2364`, 284794; incremented by the sound driver's
  timer callback, 75002) runs at about 256 Hz (measured in DOSBox, not literal in UW.EXE; the
  period is programmed through the AIL timer service, 77490). One PIT tick is about 3.9 ms.
- The frame tick `seg034_2F89_406` (114950) runs once per main loop pass (game loop function
  table entry 11, re-armed by the mode bit word 0x3800, 282528, 59149). It computes
  `elapsed = now - last` PIT ticks; if elapsed > 64 it is clamped to 64 and the slot
  accumulator `dseg_5c99_778` gets +4, otherwise the accumulator gets the number of 16-tick
  boundaries crossed, `(now >> 4) - (last >> 4)` [114958-114990]. Elapsed 0 returns at once.
- The game clock (PLAYER.DAT 0xCE, 3 bytes) advances by `elapsed` PIT ticks [label
  `seg034_2F89_4C0`, about 114994]. So 0x200 clock units are 512 PIT ticks, about 2 s.
- One slot is therefore 16 PIT ticks, about 62.5 ms, and the 16-slot phase clock laps once per
  second. This is time based: a creature interval of 4 slots is 0.25 s at any frame rate. The
  main loop is throttled to at least 4 PIT ticks and a retrace per frame (`seg003_28`, 2153).
- Speed enchantment on the player: the slot units handed to the world are halved and the odd
  slot is kept in the accumulator [114998-115008]. Freeze Time: `GameObjectLoop_seg034_2F89_518`
  (115015) skips `ProcessMobileObjects` while `UsedEnchantment_dseg_282` is nonzero [115082].
  The "easy move" fixed step `seg034_2F89_334` (114747) adds 64 ticks and 4 slots per call.

### 1.2 The mobile object walk and the due test

`ProcessMobileObjects_seg007_1798_3868` (54860) with `units` slot advances: `old = phase`,
`new = (old + units) & 15`. It walks the level's mobile list (indices between
`dseg_5c99_273C:273E` and `dseg_5c99_2732`; every allocated mobile object of the level,
creatures and projectiles, no distance or visibility filter; appended by `seg027_2861_A70`
96703, compacted by swap-with-last in `seg027_2861_A83` 96716). For each entry, while the due
test says due: creatures (word 0 bits 6-8 == 1) run `NPCInitialProcessing_seg007_2488`,
everything else `seg006_1477_164` (41077, the projectile update). A return of 0 means the
object was freed and the list index is not advanced. Afterwards `phase = new`.

Due test `seg007_1798_3825` (54807) with the object's slot (byte 0x0A low nibble):

    due = (new > slot and new - slot <= 4) or (old > new and slot >= old)

The object fires as soon as the phase has moved 1 to 4 slots past its appointment; the second
term covers the wrap of the 16-slot clock. Because the caller loops while still due and the
phase never advances more than 4 slots per frame, slow frames catch up instead of skipping
updates: below 4 fps the world slows down.

Rescheduling at the end of `NPCInitialProcessing` [53373]: `slot = (slot + interval) & 15`,
interval = byte 0x14 bits 0-2, the high nibble of byte 0x0A kept. Projectiles do the same
[41205].

### 1.3 Distance cull

Squared tile distance from the creature's tile (word 0x16) to the player tile
(`PlayerTileX/Y_dseg_5c99_248E/2490`) and, separately, to the player object's tile. If both
exceed 100 (more than 10 tiles) and the goal (word 0x0B bits 0-3) is not 3, the slot is
advanced by 8 and the update returns without motion, animation or goal logic
[`NPCInitialProcessing` 52640-52720, label `seg007_1798_25BC`]. Far creatures are frozen and
re-examined every 0.5 s. Note this is a slot advance of 8, not an interval value (the interval
field has only 3 bits).

The "player tile" `PlayerTileX/Y` is really the CAMERA's tile: `PositionCamera_seg031_396` sets
it from the camera every frame. So a creature stays awake within ten tiles of the player OR of
the view - the camera trap's spot, Roaming Sight's eye. Until 2026-09-24 the port asked the
player's tile only, and the slugs in front of the moonstone stood still in the camera trap's
picture while they move in the original (per user). Now `UWCritterRules.IsFarFromBoth` with
`ICritterHost.ViewTile`.

### 1.4 Order of one creature update (`NPCInitialProcessing_seg007_2488`, 52586-53399)

1. Snapshot: object index, table row pointer, current tile from word 0x16 (bits 10-15 x,
   bits 4-9 y) into `CurrObjXHome/YHome_dseg_245F/2460` [52590-52625]. (The reverse
   engineer named these "XHome"; they are the current tile. The home post lives in bytes 4 and
   6, section 2.)
2. Distance cull (1.3).
3. Motion handler by table byte 0x0A: bit 7 flier, bit 6 swimmer, else walker
   [52768-52790]; fire resistant kinds (COMOBJ byte 8 bit 3) move lava out of the collision
   masks [52780].
4. Per-update globals cleared: `RelatedToMotorCollision_2452`, `dseg_2462` (ceiling),
   `HasCurrObjHeadingChanged_2449`, `dseg_246D` (object hit), `RelatedToColliding_2473`
   (closed door), `FlyingPitchingRelated_244A`; `IsNPCActive_dseg_2440 := 1` [52787].
5. Path bookkeeping: if the animation (byte 0x15 bits 0-5) is neither 0x2C (walk) nor 0x20
   (stand) and byte 0x15 bit 7 (has path) is set, the path slot (word 0x16 bits 0-3) is
   released into `PathfindBitField_dseg_5c99_B4` and bit 7 cleared [52800-52825].
6. Motion, unless the creature is standing (byte 0x15 bit 6 set) with speed 0 (byte 0x13 &
   0x7F) and level pitch (byte 0x14 >> 3 == 16) [`NeedsToMove_seg007_1798_26D4`]:
   `InitMotionParams_seg029_29EE_3CC` (100109; heading = byte 9 * 256, pitch =
   (byte 0x14 >> 3 - 16) * 64, momentum = (byte 0x13 & 0x7F) * 0x2F, a random nudge of
   RNG(32) sub-units on x and y and RNG(8) on z, 100420), the tile states around the body
   (`seg006_1477_367`), `CalculateMotionTopLevel_seg006_6CB` (41703, sets the step budget
   `speed_12 = interval * 16` and runs the physics; this is where `IsNPCActive` may be
   cleared), `ApplyProjectileMotion_seg029_29EE_61A` (100436; writes fine position, zpos,
   byte 9, byte 0x13, pitch, relinks the tile lists and writes the tile into word 0x16 at
   100871/100886). `HasCurrObjHeadingChanged := 1` if byte 9 changed [52891].
7. Position snapshot: tile, floor height, eighth coordinates, home from bytes 4 and 6
   (`CurrObjQualityX/Y_dseg_246A/246B`), fine heading (`CurrObjProjectileHeading_dseg_2480`),
   full facing (`CurrObjTotalHeading_dseg_247C`), speed (`dseg_5c99_2477`) [52900-52985].
8. Goals 3 and 11 skip the animation machine and go straight to `NPCBehaviours`
   [label `seg007_1798_2849`].
9. Animation frame machine on `anim = byte 0x15 & 0x3F` and `frame = word 0x0B >> 12`
   [52990-53355]; each branch ends at the reschedule without running the goal routine:
   - anim 0x0C (dying): frame 3 -> `SpecialDeathCases` (mode 1), unlink, `DropNPCLoot`,
     `DropNPCRemains_seg006_5`, `SpillCritterInventory`, free the object, return 0
     [label `seg007_1798_2875`, 53030-53090]; else frame + 1.
   - anim 1..3 (melee swing): frame 0 with gtarg 1 starts the combat music
     [`seg007_1798_2985`]; frame 4 -> `NPCExecuteAttack_seg022_15DE(table byte 0x0F, anim - 1,
     charge table seg060[word 0x0F bits 12-15], RNG(9))`, then anim := 0, frame := 0, charge
     index := 0 [`seg007_1798_29CF`, `seg007_1798_2A29`, 53040-53080]; else frame + 1 mod 16.
   - anim 0x0D (spell) with byte 0x19 bits 2-3 nonzero: frame 4 -> pitch to target
     (`GetPitchToGTarg_seg007_1798_22C1(0x1E, 0)`), `SpellTrapWandCast_seg038_27(table byte
     0x29 + slot)`, anim := 0, frame := 0, slot bits cleared [`seg007_1798_2AAD`, 53276]; else
     frame + 1.
   - anim 5 (missile): frame 4 -> `NPCMissileLaunch_seg025_262` with the ammunition from table
     byte 0x20 bits 1-4, anim := 0, frame := 0 [`seg007_1798_2B63`]; else frame + 1.
   - anything else -> `NPCBehaviours_seg007_1798_2C4A`.
10. Reschedule [53373] and return 1.

`NPCBehaviours_seg007_1798_2C4A` (53405-54064): clear byte 0x18 bit 5 and byte 0x15 bit 6;
goal 11 jumps to the switch; footstep sound (anim 0x2C, odd frame, category table byte 0x10
low nibble: 1 -> sounds 1 and 2 on frames 1 and 3, 2 -> 0x17, 3 -> 5, 4 -> 0x0E, 5 -> 0x0D)
[53431-53500]; passive kinds (table byte 0x0A bit 1) skip to the switch [53508-53516]; kin
alarm (4.2) [53514-53600]; damage reaction (4.3) [53603-53866]; target validity guard for
goals 3, 5, 6, 9 (`GetDistancesToGTarg`, target hp byte 8 == 0 -> `ResetGoalAndGtarg`, nothing
else this update) [53880-53960, 54108]; goal switch `SwitchGoals_seg007_1798_3247` (54066,
goals 13-15 only set interval 7, 54050-54056); then the turn limiter `seg007_1798_1FDC`
[54061] (section 7.7).

Consequence: target vectors are computed after this update's motion; the goal routine chooses
heading, speed, pitch, animation, frame and interval for the next update, and the movement
they imply happens at the start of the next due update.

### 1.5 Intervals and cadences

| Interval (slots) | Seconds | Where set |
|---|---|---|
| 1 | 0.0625 | stuck or drowning (`seg006_1477_431` 41401-41457), inactive in `NPC_Goto` (46766) and `NPCWanderUpdate` (47741), a hop on a climb step (46420), freshly launched projectile (`PrepareProjectileObject_seg025_791` 90679) |
| 2 | 0.125 | projectile bounce path (101356) |
| 4 | 0.25 | walking (`NPC_Goto` 47356, `NPCWanderUpdate` 48246), goal 3 (48361), goal 5 stand (49097), melee swing and combat idle (49533, 49568), goal 9 (50451), goal 6 (50728, 50973), goal 11 (53998), dying (`ProcessDeath` 54486) |
| 6 | 0.375 | standing idle (`NPCWanderUpdate` 48212, `StandStillGoal` 48819), goal 10 (51248, 51324), goal 12 (51605), `ReactToPlayerPresence` (51454) |
| 7 | 0.4375 | goals 13 and above (54056) |
| slot + 8 | 0.5 | distance cull (52715) |

Animation is advanced by the creature's own update, one frame per update: walking cycles four
frames in one second (interval 4); standing advances a frame with probability 1/2 per
interval-6 update (average 0.75 s per frame, irregular) [48836, 48214, 51458]; a melee swing
runs frame 0..4 over five updates and strikes on the fifth (1.25 s after the decision), the
goal routine runs again on the sixth (1.5 s); missile and spell have the same shape; dying is
four updates from frame 0 to removal (1 s). Missile and spell routines do not write the
interval, it stays at the goal's value.

## 2. Data

### 2.1 Mobile object record (27 bytes) - fields used by the AI

| Field | Meaning | Written by |
|---|---|---|
| word 0 bits 0-8 | item id (row = id & 0x3F) | level data |
| word 0 bit 13 | excludes the creature from the path range budget (`seg007_1798_340A` 54283) | level data |
| word 2 bits 0-6 | zpos | physics |
| word 2 bits 7-9 | facing eighth 0..7 | goal routines, turn limiter |
| word 2 bits 10-12, 13-15 | fine y, fine x (eighths) | physics |
| byte 4 bits 0-5, byte 6 bits 0-5 | home post x, y ("quality", "owner") | level data, conversation (`npc_xhome/yhome`, 338371-338560) |
| byte 8 | hit points | `DamageNPC` |
| byte 9 | fine heading 0..255 | goal routines, physics deflection, turn limiter |
| byte 0x0A bits 0-3 | due slot | reschedule (53373), cull (52715) |
| byte 0x0A bits 4-6 | tile state of the last physics step | `ApplyProjectileMotion` |
| byte 0x0A bit 7 | "attitude locked" (skipped by kin alarm, theft, kind balancing; cleared by a conversation routine 337525, set for the golem 349981) | scripts |
| word 0x0B bits 0-3 | goal | `SetNewGoalAndGtarg`, `ResetGoalAndGtarg`, `SetGoalAndGtarg_ovr104_0` (338671) |
| word 0x0B bits 4-11 | gtarg (goal target object index; 1 = player) | same |
| word 0x0B bits 12-15 | animation frame; the goal routines cycle it 0..3 and use it as a phase counter (odd frames play footsteps, frame 3 gates the wander switches) | goal routines, animation machine |
| word 0x0D bits 0-3 | backup goal ("npc_level"), only ever 4 or 0 | `SetNewGoalAndGtarg` (54350) |
| word 0x0D bits 4-7 | destination height | `SetNPCTargetDestination_seg006_1477_28EA` (46574) |
| word 0x0D bit 10 | "strong individual": combat bonuses (8.3) | level data |
| word 0x0D bit 13 | talked to | conversation |
| word 0x0D bits 14-15 | attitude: 0 hostile, 1 upset, 2 mellow, 3 friendly | section 4.1 |
| word 0x0F bits 0-5, 6-11 | destination tile x, y | `SetNPCTargetDestination`, `NPC_Goto` |
| word 0x0F bits 12-15 | swing charge index 0..15 | `ChooseMeleeAttackToMake` (49540-49555), cleared at the blow (53070) |
| byte 0x11 | damage taken since the last decision | `DamageNPC` (+=), `NPCBehaviours` (:= 0) |
| byte 0x12 | index of the last attacker | `DamageNPC`, `NPCBehaviours` (:= 0) |
| byte 0x13 bits 0-6 | speed (momentum = speed * 0x2F) | goal routines, `ApplyProjectileMotion` (momentum / 0x2F) |
| byte 0x13 bit 7 | gravity on | physics |
| byte 0x14 bits 0-2 | update interval in slots | goal routines |
| byte 0x14 bits 3-7 | pitch, 16 = level (fliers) | goal routines, physics |
| byte 0x15 bits 0-5 | animation slot: 0 combat idle, 1 bash, 2 slash, 3 thrust, 5 missile, 7 backing off, 0x0C dying, 0x0D spell, 0x20 standing, 0x2C walking | goal routines |
| byte 0x15 bit 6 | standing, no motion needed | goal routines; cleared at the start of `NPCBehaviours` |
| byte 0x15 bit 7 | has a path (slot in word 0x16 bits 0-3) | `NPC_Goto` (47250) |
| word 0x16 bits 0-3 | path slot | `NPC_Goto` (47261) |
| word 0x16 bits 4-9, 10-15 | current tile y, x (the asm's "YHome/XHome") | `ApplyProjectileMotion` (100871), goal 3 teleport (48458) |
| byte 0x18 bits 0-4 | facing residual (fine part of the facing) | turn limiter, goal routines (cleared when a goal sets the facing) |
| byte 0x18 bit 5 | new destination this update | `SetNPCTargetDestination`; cleared at the start of `NPCBehaviours` |
| byte 0x18 bit 6 | blocked | `NPC_Goto` (46937, 47000-47020) |
| byte 0x18 bit 7 | straight tile line to the destination known clear | `NPC_Goto` (47040) |
| byte 0x19 | perception and temper flags, see 2.2 | |
| byte 0x1A | whoami (conversation slot) | level data |

### 2.2 Flag bits of byte 0x19

| Bit | Meaning | Set by | Cleared by |
|---|---|---|---|
| 0 | target confirmed (in view or known) | search result 0 (51873), `StandStillGoal` (48730), kin alarm (53640), hit by the player (53751) | search result 1 (51910), goal 5 re-search result 1 or 2-with-coin (49640-49700), leash (49150), `CalmNPCS_ovr104_115` (338969) |
| 1 | heard something, searching | `StandStillGoal` on result 2 (48760), goal 5 re-search result 2-with-coin (49690) | `StandStillGoal` on a failed initiative roll (48700), goal 5 result 1, leash, blocked path reset (49812), `CalmNPCS` |
| 2-3 | spell slot to cast at anim 0x0D frame 4 (1 = table 0x2A, 2 = 0x2B, 3 = 0x2C) | `TryToDoMagicAttack` (3), `NPCStartMagicAttack` (1 or 2) | after the cast (53276) |
| 4 | has made a stand: the next hit gives goal 9 without a morale check | goal 6 close branch (50591), damage reaction | `CalmNPCS` |
| 5 | pursues without morale check and without leash | damage reaction at squared tile distance > 2 (decision 3, 53775-53830) | `CalmNPCS` |
| 6 | ally of the player | `Ally_seg038_3307_8F8` (125556), conversation `npc_attitude` >= 4 (338516-338528) | `CalmNPCS`, conversation (337525) |
| 7 | hungry (conversation import, 338384-338400); no reader in the AI | conversation | `CalmNPCS` |

All bits are cleared on a level change by `CalmNPCS_ovr104_115`.

### 2.3 Creature table row bytes used by the AI

| Byte | Bits | Meaning | Used in |
|---|---|---|---|
| 0x00-0x03 | all | armour of body part 0..3 (byte 3 == 0xFF: use byte 0); the player's row 63 is all zeros in the file and is filled at runtime from his equipment (8.8) | `AttackerAppliesFinalDamage` (81050, label 990) |
| 0x04 | all | vitality (base hp of the kind) | morale check (54196), path range (54283), music threshold (`DamageNPC`), UI eyes |
| 0x05 | all | strength: / 5 into base damage | `NPCExecuteAttack` (label 1629-1648) |
| 0x06 | all | dexterity: circling roll RNG(64) < dex (49349), missile roll RNG(192) <= dex (50237) | `ChooseMeleeAttackToMake`, `seg007_1798_11FB` |
| 0x08 | 0-2 | == 1: death sound 6 | `Death_seg007_1798_35CB` (label 35F3) |
| 0x08 | 3-4 | nonzero: blood splat on a damaging hit, zero: impact flash | `AttackerAppliesFinalDamage` (label B49-C17) |
| 0x08 | 5-7 | blood object 0xD8 + n at death | `DropNPCRemains_seg006_5` (40872) |
| 0x09 | all | kind ("general type"): kin alarm, theft owner, `set_race_attitude`, Tybal 0x13 | `NPCBehaviours`, `AngerNPCByIllegalAction`, goal 5 |
| 0x0A | 1 | passive: no kin alarm, no damage reaction | `NPCBehaviours` (53508) |
| 0x0A | 2-4 | corpse object 0xC0 + n at death (7 in 16) | `DropNPCRemains` (41019-41024) |
| 0x0A | 4-6 | size shift for the collision body | `InitMotionParams` (100244) |
| 0x0A | 5 | may climb more than one height level on a path (reference reading) | `TraverseMultipleTiles_seg006_1477_6F6` (41730) |
| 0x0A | 6 | swimmer | handler choice (52782) |
| 0x0A | 7 | flier | handler choice (52768), melee without height test (48985), pitch rolls |
| 0x0B | 0-6 | wander speed: every goal but 5 in `NPC_Goto` (47332), the wander step (48233), goal 6 far when the target is >= 8 tiles away (50936), back-off (wander + 1) / 2 (50650), side-step wander * 2 / 3 (49290) | |
| 0x0C | 0-6 | pursuit speed: `NPC_Goto` in goal 5 (47320), goal 6 within 8 tiles (50936) | |
| 0x0F | all | poison strength written into the player (low nibble) | `NPCExecuteAttack` (label 16A4-16EC) |
| 0x10 | 0-3 | footstep sound category | `NPCBehaviours` (53455) |
| 0x10 | 4-7 | impact sound category for creature-vs-creature hits | `CombatMissImpactSound_seg022_C29` |
| 0x11 | all, signed | attack power: / 2 into the attack score | `NPCExecuteAttack` (label 1656) |
| 0x12 | all | defence: the check value of the attacker's skill check; for the player it is the Defence skill PLUS half the skill of the weapon in hand, written by `PlayerStatusUpdate_ovr133_784` (8.8) | `CalculateAttackResults` (label 797) |
| 0x13 + 3i, 0x14 + 3i, 0x15 + 3i | | attack i (0 bash, 1 slash, 2 thrust): to-hit (signed), damage, chance in 100 | `ChooseMeleeAttackToMake` (49480-49516), `NPCExecuteAttack` |
| 0x14 | all | also: bash damage die against doors | `NPCTryToOpenDoor` (47570) |
| 0x1C | 0-3 | morale | morale check (54196), goal 6 stand roll (50569), path range |
| 0x1C | 4-7 | travel range in tiles: goal 8 home limit range^2 (48600), goal 5 leash 4 * range^2 (49141) | |
| 0x1D | 0-3, 4-7 | loudness and visibility of this kind AS A TARGET (the player's row is rewritten at runtime) | `SearchForGoalTarget` (51704-51760) |
| 0x1E | 0-3 | hearing: search (51704), kin alarm reach (53595) | |
| 0x1E | 4-7 | sight: search (51758), theft notice range (341300) | |
| 0x1F | 0-3 | restlessness: wander switches and heading jitter (47906, 47933, 48062, 48125), goal 6 jitter (50845) | |
| 0x1F | 4-7 | initiative: standing rolls (48688-48716) | |
| 0x20 | 1-4 | ammunition index: missile object 0x10 + n, entry n of the 16 x 3 byte ammo table at 0x5942 (`RangedWeapons1_dseg_5c99_5943`, loaded at 358837) | missile launch (label 2B6D) |
| 0x20 | 5-7 | == 1: has a missile weapon | goal 5 (49030), goal 9 (50370) |
| 0x28 | word | experience base | `AwardKillEXP_seg022_1725` (83677) |
| 0x2A, 0x2B, 0x2C | all | spells 1, 2 (11 in 16, 5 in 16) and 3 (0xFF = none); spell index into the runic table | magic routines |
| 0x2D | 0 | caster: uses spells 1 and 2, stand-off 4 tiles, damage reaction exception (decision 3) | goal 5, `NPCBehaviours` |
| 0x2D | 1-7 | spell power: RNG(256) and RNG(128) rolls | `TryToDoMagicAttack` (49862), `NPCStartMagicAttack` (50090) |
| 0x2E | all | door skill: use the door, then lockpick | `NPCTryToOpenDoor_seg006_1477_3123` (47474) |
| COMOBJ byte 0, byte 1 bits 0-2 | | object height, collision radius (eighths) | `InitMotionParams`, `ScanForCollisions_seg026_BF6` (93164), sight line eye height |
| COMOBJ byte 8 bit 3 | | fire resistance: lava neither collides nor costs | `NPCInitialProcessing` (52780) |

### 2.4 Globals that carry state between routines

| Global | Meaning |
|---|---|
| `IsNPCActive_dseg_5c99_2440` | set to 1 at the start of every creature update (52787, also 41590); cleared only by the motion callbacks when the step left the creature stuck (result flag 0x1000: interval 1, 41408) or drowning or burning (result masked 0xF8 == 0x10: splash, anim 0x0C frame 3, interval 1, 41420-41423). Goal routines test it first and do nothing (or set interval 1) when it is 0. It is a per-update "the step succeeded" flag, not an activity criterion. |
| `RelatedToMotorCollision_2452` | a motion callback fired this update (collision) |
| `dseg_5c99_246D`, `CollisionObject_dseg_5c99_2442` | an object was hit, and which (106531) |
| `RelatedToColliding_2473` | a closed door was among the collision records (106351) |
| `dseg_5c99_2462` | a flier touched the ceiling |
| `HasCurrObjHeadingChanged_2449` | byte 9 changed during the physics (wall deflection or push) |
| `FlyingPitchingRelated_244A`, `dseg_5c99_2472` | flier ducked under a door this update; a hop was started this update (skips the walk block of `NPC_Goto`) |
| `XVectorToGTARG_243C`, `YVectorToGTARG_243E`, `dseg_5c99_2450`, `dseg_5c99_2454:2456`, `GoalTargetXHOME_2448`, `GoalTarget_YHome_2453`, `GoalTargetFloorHeight_2478` | target vectors in eighths, squared tile and eighth distances, target tile and floor height (`GetDistancesToGTarg`, 54088-54190) |
| `LastDamagedNPCIndex_dseg_5c99_C5`, `LastDamagedNPCGeneralType_C6`, `DamagedCharacterXHome/YHome_248A/248B`, `DamagedCharacterZPos_248C`, `LastPlayerAppliedDamageTimer_2486/2488` | kin alarm: written by `DamageNPC` (54655-54700) only when the source resolves to the player and the victim's byte 0x0A bit 7 is clear; saved with the game (341052-341140, PLAYER.DAT 0xBA-0xC1); reset on a level change (341140: index 0, kind 0xFF) |
| `PathfindBitField_dseg_5c99_B4` (280256, initial 0xFFFF) | free path slots, 16 of 0x1C bytes in seg057 (262783) |
| `PlayerCritterData_dseg_7272` | the player's own creature row: loudness and visibility (3.2), armour and defence (8.8) |
| `dseg_1AFE`, `dseg_1AFF`, `dseg_783` | player base noise, base visibility, noise decay counter (3.4) |

## 3. Perception

### 3.1 SearchForGoalTarget_seg007_1798_1CEB (51655-51914)

Inputs: the searcher (its row) and the goal target (from word 0x0B bits 4-11 through
`GetDistancesToGTarg`). Outputs the target's tile into two parameters and returns 0 found,
1 lost, 2 unchanged.

    dx = targetTileX - myTileX; dy = targetTileY - myTileY (signed bytes)
    d2 = dx*dx + dy*dy                                        ; squared tile distance
    hear = (targetRow[0x1D] & 15) * (myRow[0x1E] & 15) / 16;  hear2 = hear * hear
    if hear2 / 4 > d2: return FOUND                           ; heard, strict, no flag write [51704-51757]
    see = (targetRow[0x1D] >> 4) * (myRow[0x1E] >> 4) / 16
    if d2 <= see * see:                                       ; in sight range [51758-51780]
        rel = (GetVectorHeading(dx, dy) - myFacingEighth + 8) mod 8
        if rel in {0, 1, 7}                                   ; the target's eighth or a neighbour [51838]
           and TestBetweenPoints(myX, myY, myZ + myObjectHeight, tX, tY, tZ + tObjectHeight):
            flags |= bit 0; return FOUND                      ; seen [51873]
    if hear2 * 4 > d2: return UNCHANGED                       ; [51895]
    flags &= ~bit 0; return LOST                              ; [51910]

- `GetVectorHeading_seg006_1477_2862` (46472-46561): eight-way quantisation with sector
  boundaries at slopes 2 and 1/2 (a cardinal sector spans 53 degrees, a diagonal 37); 0 = +Y,
  clockwise; (0, 0) gives 4. Signed bytes are doubled in a byte, so components beyond 63 wrap.
- `TestBetweenPoints_seg006_1477_1BD1` (44792-45426): a line walk in eighths with Z
  interpolated over the longer axis; per tile step `TestTileTraversal_seg006_1056`
  (43103-43420) tests the wall bits of the tile type for the step direction (leaving and
  entering) and blocks the line where the interpolated height (>> 3) falls below the next
  tile's floor. Doors are objects and are not tested. The same routine serves theft notice and
  missiles.
- Asymmetry: heard within hear2 / 4, lost beyond hear2 * 4, unchanged in between.

Callers and what they do with the result:

- `StandStillGoal_seg007_1798_6B0` (goals 0, 4, 7 and the hostile half of goal 2's updates),
  only when attitude == 0: gtarg := 1, `GetDistancesToGTarg` [48663-48670]. If bit 0 is set:
  `SetNewGoalAndGtarg(5, 1)` at once [48676-48683]. Else, if bit 1 is set: RNG(16) <=
  initiative (table byte 0x1F high nibble) -> `TurnTowardsTarget(0)` (facing one eighth
  towards the target), otherwise bit 1 is cleared [48688-48704]. Then RNG(16) < initiative
  -> the search [48706-48717]: 0 -> bit 0 set, `SetNPCTargetDestination(target tile)`, goal 5
  gtarg 1; 2 -> bit 1 set and, with RNG(2) == 0, `NPC_Goto(target tile)` (walk over and
  look) [48760-48790]; 1 -> nothing. Note the two rolls differ: <= for the turn, < for the
  search.
- `AttackGoalSearchForTarget_seg007_E5D` (goal 5 tail, 6.5): 1 in 8 per update when the
  stored destination differs from the target tile in x OR y.
- `GoalTalkto_seg007_1798_1937` (goal 10, 6.9): result 1 -> stand idle.

Activation: nothing farther than 10 tiles perceives anything (1.3).

### 3.2 The player's loudness and visibility (row 63 byte 0x1D)

`ApplyPlayerSneakScore_seg034_2F89_898` (115605-115709), called from `GameObjectLoop_seg034_2F89_518`
(115114) once per FRAME of the game loop - the same loop that moves the slot clock (1.2):

- Base values in `ResetPlayerStatusValues_ovr133_1E4` (380878-380916): noise
  `dseg_1AFE = 13 - Sneak / 3`, visibility `dseg_1AFF = 15 - Sneak / 5` (Sneak is
  PlayerData[0x2E] on this pointer). `ApplyDefenceStealthBonuses_ovr133_65D` (381902-382122),
  run at the end of `PlayerStatusUpdate_ovr133_784` (383016), subtracts per set bit of a 4-bit
  mask: bit 1 up to 16 from the noise, bit 2 up to 5 from the visibility, bit 3 up to 16 from
  it (bit 0 has no case at all). The mask comes from the spell class 3 with a minor class of 2
  to 4, which sets bit `minor - 1` (`Stealth_ovr133_3BD`, 381276) - from a cast spell as well
  as from a worn enchanted item, since both run through the same case distinction (8.8).
- Per tick target `t`: in "easy move" `base + 4`; otherwise 0 when not moving; otherwise
  `base + speed * 10 / forwardSpeed - 5` (louder the faster); +4 when the swim or tile-state
  byte PlayerData[0xB8] is nonzero; clamped 0..15.
- The row's low nibble jumps UP to `t` at once but decays DOWN by one only every eighth call
  (counter `dseg_783` mod 8), so one step per eight frames - how long that takes depends on the
  frame rate of the machine. The high nibble is set to `dseg_1AFF & 15` every call.
- Other writers of the low nibble exist in the player's combat code (winding up, striking);
  they were not read (the port's `ChargingNoise`/`StrikeNoise` are unverified).

### 3.3 Other perception rules

- `ReactToPlayerPresence_seg007_1AF6` (51383-51507), called at the end of a wander step: if
  the creature's speed is 0 or the player has a weapon drawn: gtarg := 1; if the squared
  eighth distance is < 0x90 it faces the player, speed 0, anim 0x20, interval 6, frame + 1
  with probability 1/2.
- `MaybeVectorsToPlayer_seg007_1798_181D` (50985-51156, argument = distance in eighths):
  bends a chosen heading away from the player when he is within the argument (squared
  compare): a heading within 90 degrees of "away" is kept, otherwise it snaps to the away
  direction or turns 45 degrees towards it (steering table skimmed only). Called with 10 from
  the wander step (48062 area) and 0x18 from goal 6 (50858 area).
- `TurnTowardsTarget_seg007_1798_1F1A` (51920-52010): argument 0 turns the facing eighth one
  step (45 degrees) per call towards the goal target the shorter way and returns 1 when facing;
  argument 1 delegates to `seg007_1798_2123` (52174-52376, fine turn for shots and spells;
  its tolerance was not read).
- `CheckNearbyNPCHunger_ovr104_6F1` (340065, misnamed): refuses the player's rest when a
  creature within 0x7F eighths has goal 4, 5 or 9 and bit 0 set.

## 4. Temper

### 4.1 Attitude writers (word 0x0D bits 14-15)

| Where | New value | Condition |
|---|---|---|
| `NPCBehaviours` kin alarm (53640-53644) | 0 | alarm accepted (4.2) |
| `NPCBehaviours` damage reaction (53748-53751) | 0 | last attacker was the player |
| `NPC_Goal5_Attack` (48957-48963) | 0 | every update in goal 5 with gtarg 1 (a guard sent to attack by a conversation becomes hostile in the data and stays so) |
| `AngerNPCByIllegalAction_ovr104_C37` (341158-341521) | max(att - 1, 0) | theft or trespass seen (4.5) |
| `ApplyAttitudeChangingSpellToNPC_seg038_3307_889` (125347) | argument | Confusion (goal 2, attitude 1), Paralyse (goal 7, attitude 1), Cause Fear (goal 6, attitude unchanged); via `SetGoalAndGtarg` with gtarg 1, only if `ScaleDamageAgainstObject(type 1, 3)` allows |
| `Ally_seg038_3307_8F8` (125432-125556) | 3 | plus byte 0x19 bit 6; goal 2 gtarg 0 unless already an ally |
| `RetrieveImportedVariablesAfterConversation_ovr103_492` (338371-338560) | `npc_attitude`; >= 4 gives 3 plus bit 6 | after every conversation; also `npc_goal`, `npc_gtarg`, `npc_hp`, `npc_xhome/yhome` (bytes 4/6), `npc_hunger` -> bit 7, talked-to bit 13 |
| `set_attitude_ovr094_21` (318317), `set_race_attitude_ovr094_5C` (318390-318674) | stack value | all NPCs with that whoami; all NPCs of the kind within N tiles of the partner, skipping byte 0x0A bit 7 |
| `do_demand_ovr095_12BA` (323610-323888) | att - 1 on success via the import | a refused demand: goal 5 gtarg 1 |
| `TalkingDoor_ovr107_1319` (349688) | 3 | temporary talking object |
| `CalmNPCS_ovr104_115` (339150-339200) | per kind | level change: hostile members count one step down, friendly one up, applied to the kind except byte 0x0A bit 7 |

Attitudes above 0 are never raised by the AI itself; only conversations, spells and the level
change do.

### 4.2 The kin alarm

Written by `DamageNPC_seg007_1798_3622` (54655-54700) when the damage source resolves to
index 1 (the player) and the victim's byte 0x0A bit 7 is clear: kind (table byte 9), index,
tile, zpos >> 3, game clock. A miss (damage 0 through `DamageObject`) writes them too.

Read in `NPCBehaviours` (53514-53600) every update, before the goal switch, never for a
passive kind:

    kin   = bit 6 clear and myIndex != LastDamaged.index and myRow[9] == LastDamaged.kind and my byte 0x0A bit 7 clear
    ally  = bit 6 set
    if (kin or ally) and clock <= LastDamaged.clock + 0x200                 ; 512 PIT ticks, about 2 s [53553]
       and |myTileX - LastDamaged.x| + |myTileY - LastDamaged.y| < myRow[0x1E] & 15:   ; Manhattan, strict [53595]
        attitude := 0; bit 0 := 1
        if goal not in {6, 9}: SetNewGoalAndGtarg(5, ally ? LastDamaged.index : 1)
                               SetNPCTargetDestination(LastDamaged tile, z)

The alarm is by kind, only from the player's blows, does not propagate creature to creature,
and has no reach beyond hearing and the 10-tile cull.

### 4.3 The damage reaction (NPCBehaviours 53603-53866)

`DamageNPC` itself changes no goal: it adds the damage to byte 0x11, stores the attacker index
in byte 0x12 (1 for the player; for a projectile the projectile's own byte 0x12, its launcher;
0 if none), writes the alarm, kills or subtracts, switches the music (theme 5 when
`hp * 64 / (vitality + 1) < 16`, else 6) [54563-54802]. The mind reacts on the next
`NPCBehaviours` pass, gated on byte 0x12 != 0:

    if attacker == 1 or I am an ally (bit 6) or the attacker is an ally: react, else ignore   ; NPC-vs-NPC hits do not provoke bystanders
    gtarg := attacker; GetDistancesToGTarg (a dead attacker ends the reaction)
    if attacker == 1: attitude := 0; destination := player tile and z; bit 0 := 1       ; [53748-53760]
    ; decision 3, label seg007_1798_2F8C, 53775-53830:
    if tileDist2 > 2 and (not caster (row[0x2D] bit 0 clear) or my tile has the no-magic bit):
        bit 5 := 1; goal := 5
    elif bit 5 set:  goal := 5
    elif bit 4 set:  goal := 9
    else:            goal := (MoraleCheck() ? 6 : 5)                                  ; seg007_1798_3383
    target := attacker in every case; byte 0x12 := 0; byte 0x11 := 0

So a creature hit from more than about 1.4 tiles that cannot cast back never rolls for flight,
pursues without leash (bit 5) and keeps that for the rest of the level. A creature hit up close
(or a caster that could cast back) rolls once per hit; after it has once made a stand (bit 4)
every further hit sends it to goal 9. Any hit ends paralysis (goal 7) because the reaction
assigns a goal regardless of the old one; no other release of goal 7 was found.

### 4.4 The morale check `seg007_1798_3383` (54196-54277) and the stand roll of goal 6

Arguments: vitality = table byte 4, hp = byte 8, morale = table byte 0x1C low nibble,
damage = byte 0x11.

    if hp > vitality * 3 / 4: return false        ; barely hurt
    if hp < vitality / 8:     return false        ; nearly dead creatures fight to the death
    if damage > vitality / 2: return true         ; more than half the vitality since the last decision
    if vitality == 0:         return false
    return (15 - morale) >= hp * 16 / vitality + RNG(4)     ; [54246]

Flight is `SetNewGoalAndGtarg(6, attacker)`. The stand roll inside goal 6 (6.7): in the close
branch only, `RNG(256) < morale >> 3` (morale 0..7: never; 8..15: 1 in 256 per update) OR a
collision with unchanged heading -> bit 4 := 1, goal 9 [50569-50606]. There is no timer and
no hp recovery that ends a flight; only the stand roll, a collision, the next hit, or the
target's death (`ResetGoalAndGtarg`).

`seg007_1798_340A` (54283): the path range budget, `hp * 4 / vitality + morale / 4` for a
hostile creature with vitality > 0 and word 0 bit 13 clear (0 for whoami 0x16 on level 6), else
0. Consumed by the path search (7.5).

### 4.5 Theft and trespass

`ovr104_E4C` (341521-341620), called from pickup (87156) and the trespass trap (349423): the
owner is the argument or, if COMOBJ byte 7 bit 7 allows an owner, the item's byte 6 & 0x3F;
runs `AngerNPCByIllegalAction` over a 15 x 15 tile area around the item; afterwards clears the
item's owner when `owner & 0x1F <= 0x1B`. Per candidate: kind == owner & 0x1F; a creature
with byte 0x0A bit 7 only if owner bit 5 is set, and owner 0x20 exactly only such creatures;
owner 0x0D (knights) with PlayerData[0x69] >= 3: nothing; squared tile distance <= (sight
nibble)^2; `TestBetweenPoints` from the creature's eye to the item's z + height + 12; then
attitude := max(attitude - 1, 0) and message string block 2, 0xE1 + new attitude ("angered",
"annoyed", "notes").

## 5. The goal state machine

### 5.1 The 13 goals

| Goal | Routine | One sentence |
|---|---|---|
| 0 | `StandStillGoal_seg007_1798_6B0` (48640) | Stands; a hostile one turns towards noises and searches for the player with its initiative. |
| 1 | `NPC_Goto` from the switch (53875) | Walks to the home post (bytes 4/6); on arrival becomes goal 8. |
| 2 | `NPCWanderUpdate_seg007_1798_1` (47653) | Aimless stop-and-go driven by restlessness; a hostile one runs `StandStillGoal` on half of its updates. |
| 3 | `seg007_1798_3E4` (48278) | Follows the target: stands and swings without damage within 2 squared tiles, walks in between, teleports when farther than 8 tiles. |
| 4 | `StandStillGoal` then `Goal8_seg007_1798_5DF` | Wander around home with hostile detection first; the only goal remembered as backup. |
| 5 | `NPC_Goal5_Attack_seg007_1798_891` (48870) | Melee, missile or magic; pursues with `AttackGoalSearchForTarget`. |
| 6 | `NPCGoal6_seg007_1798_13FA` (50483) | Backs away facing the target when close, runs when far; cornered or a rare roll leads to 9. |
| 7 | `StandStillGoal` | Same code as 0 (the switch maps 7 to case 0, 54073); used by Paralyse. |
| 8 | `Goal8_seg007_1798_5DF` (48522) | Walks home beyond the travel range, else wanders; a hostile one becomes goal 4 on its first update. |
| 9 | `NPCGoal9_seg007_1798_12C6` (50284) | Holds ground: strikes below 0x90 squared eighths, faces within 2 tiles, else shoots, casts or runs the withdraw routine. |
| 10 | `GoalTalkto_seg007_1798_1937` (51162) | Faces the player within 0x190 squared eighths and starts the conversation within 0x90 when he looks back. |
| 11 | inline `Ethereal_seg007_1798_318D` (53968) | Random heading, speed 0 or 1, random height; no reactions at all. |
| 12 | `NPCGoalC_seg007_1798_1BF4` (51513) | Walks to the home post and stands there; a hostile one becomes goal 4. |

Goals 13-15: default case, interval 7 and nothing else (54050-54056).

### 5.2 What SetNewGoalAndGtarg and ResetGoalAndGtarg remember

- `SetNewGoalAndGtarg_seg007_348E(goal, gtarg)` (54350-54393): if the CURRENT goal is 4, copy
  4 into word 0x0D bits 0-3; then write goal and gtarg. Nothing else is ever saved.
- `ResetGoalAndGtarg_seg007_1798_34FD` (54399-54446): if word 0x0D bits 0-3 are nonzero:
  goal := that value, gtarg := 1, clear the nibble; else goal := 2, gtarg := 0.
- Callers of the reset: the target validity guard (target hp 0, 54108), goal 5 re-search
  results 1 and 2-with-coin (49640-49700), `NPC_Goto` reporting the blocked bit after the
  approach (49800-49815).
- `SetGoalAndGtarg_ovr104_0` (338671) is the external entry for conversations and scripts
  (goals 3, 10, 12 are assigned this way; level data assigns the rest).

Consequence: only guards (goal 4) return to their post after a fight; everyone else ends in
goal 2 and never walks home again (goal 2 has no home).

### 5.3 Transition table

| From | To | Condition | Where |
|---|---|---|---|
| 0, 4, 7, (2) | 5 | attitude 0 and bit 0 set; or search result 0 (initiative roll passed) | `StandStillGoal` 48676-48683, 48730-48745 |
| 1 | 8 | standing on the home tile | `NPC_Goto` 46742 |
| 8 | 4 | attitude 0 | `Goal8` 48560-48575 |
| 12 | 4 | attitude 0 | `NPCGoalC` 51530-51545 |
| any but 6, 9 | 5 | kin alarm accepted | `NPCBehaviours` 53640-53660 |
| any | 5 | hit at squared tile distance > 2 by a non-caster or a caster on a no-magic tile (bit 5); hit with bit 5 set; hit and morale check false | `NPCBehaviours` 53775-53830 |
| any | 9 | hit with bit 4 set (and bit 5 clear, near) | same |
| any | 6 | hit, near, bits 4 and 5 clear, morale check true | same, `seg007_1798_3383` |
| 5 | 4 | leash: squared eighths to target > 0x100, backup == 4, bit 5 clear, squared tile distance from home > 4 * range^2 | `NPC_Goal5_Attack` 49129-49172 |
| 5 | backup or 2 | re-search result 1, or 2 with RNG(2) == 0 | `AttackGoalSearchForTarget` 49640-49700 |
| 5 | backup or 2 | `NPC_Goto` set the blocked bit this update | 49800-49815 |
| 3, 5, 6, 9 | backup or 2 | target hp byte 8 == 0 | `NPCBehaviours` 54108 |
| 6 | 9 | close branch: RNG(256) < morale >> 3, or collision with unchanged heading; far branch: collision, unchanged heading, squared tile distance < 9 (bit 4 set) | `NPCGoal6` 50569-50606, 50678-50700 |
| 9 | 6 (routine only) | far, no shot: `NPCGoal6` is CALLED, the goal stays 9 | `NPCGoal9` 50374 |
| any | 2, 7, 6, 2 | spells Confusion, Paralyse, Cause Fear, Ally | 125347-125586 |
| any | script value | conversation `npc_goal`, `set_goal`, refused demand (5 gtarg 1) | 338371-338560, 323610 |

### 5.4 Diagram

```
             level data / conversation (SetGoalAndGtarg_ovr104_0)
                                  |
  +---------+  hostile   +--------v---------+  hostile, first update   +---------+
  | 12 home |----------->| 4 wander at home |<-------------------------| 8 wander|
  +---------+            |   (remembered)   |                          | at home |
                         +---+----------+---+                          +----^----+
     +---------+  sees/hears  |          |  lost target, backup 4             | arrived
     | 0 / 7   |---------+    |          +---------------------------+        |
     | stand   |         |    |                                      |  +-----+-----+
     +---------+         v    v   sees/hears, hit, kin alarm         |  | 1 go home |
                       +-----------+ <---------------------------------+  +-----------+
  +---------+  1 in 2  |    5      |----> 4   leash (> 2 x travel range, backup 4, bit 5 clear)
  | 2 wander|--------->|  attack   |----> backup or 2   target dead, lost on re-search, blocked step
  |         |<---------|           |
  +---------+  lost,   +-----+-----+
               no backup     |  hit, squared tile distance <= 2, bits 4/5 clear, morale check true
                             v
                       +-----------+  stand roll (morale >> 3 of 256) or bumped while close  +-----------+
                       |    6      |------------------------------------------------------->|  9 hold   |
                       | withdraw  |<------ called as a routine, goal stays 9, when far -----| ground    |
                       +-----------+                                                        +-----------+
                             ^  hit again: bit 5 -> 5; bit 4 -> 9; else morale check -> 6 or 5    |
                             +---------------------------------------------------------------------+

  3 follow, 10 talk, 11 drift: no transitions of their own (3 resets when the target dies).
```

## 6. Per-goal algorithms (tile grid and eighths)

Common: "active" means `IsNPCActive` != 0. "frame++" means `(frame + 1) & 3`. "stand pose"
means interval 6, speed 0, anim 0x20, standing bit set, frame++ with probability 1/2.

### 6.1 Goals 0, 4, 7, 8 (`StandStillGoal` 48640-48864, `Goal8` 48522-48634)

    StandStillGoal:
      if not active: return
      if attitude != 0: goto pose
      gtarg := 1; GetDistancesToGTarg
      if bit 0: SetNewGoalAndGtarg(5, 1); return
      if bit 1:
          if RNG(16) <= initiative: TurnTowardsTarget(0) else bit 1 := 0
      if RNG(16) < initiative:
          r = SearchForGoalTarget(out tx, ty)
          if r == 0: bit 0 := 1; SetNPCTargetDestination(tx, ty, z); SetNewGoalAndGtarg(5, 1); return
          if r == 2: bit 1 := 1; if RNG(2) == 0: NPC_Goto(tx, ty, z); return
      pose:
      switch goal: 0 or 7: stand pose; 2: NPCWanderUpdate; else (4): Goal8

    Goal8 (goals 4 and 8):
      if not active: return
      if attitude == 0 and goal != 4: SetNewGoalAndGtarg(4, 1); return
      d2home = (homeX - tileX)^2 + (homeY - tileY)^2                ; tiles
      if d2home > travelRange^2: NPC_Goto(home) else NPCWanderUpdate

A hostile standing creature therefore notices the player with probability initiative/16 per
0.375 s; goal 7 never moves on its own.

### 6.2 Goal 1

`NPC_Goto(homeX, homeY, floor height of that tile)` [53875]; inside, standing on the home tile
with goal 1 gives `SetNewGoalAndGtarg(8, 0)` [46742]. No detection while walking; walks at
the wander speed (7.1).

### 6.3 Goal 2 (`NPCWanderUpdate` 47653-48272)

    if has path: release it
    if not active: interval := 1; return
    if attitude == 0 and RNG(2) == 1: StandStillGoal; return                    ; [47781]
    if flier: pitch := floor > 14 ? 14 + RNG(3) : (zpos>>3 < tileFloor + 2 ? 16 + RNG(3) : 14 + RNG(5))   ; [47817-47868]
    if anim == 0x20: if RNG(16) < restlessness and frame == 3: anim := 0x2C     ; start walking [47906]
    else:            if not (restlessness >= RNG(16)) and frame == 3: anim := 0x20   ; stop [47933]
    if walking:
        if collided and heading unchanged: byte 9 += (RNG(2) ? +0x40 : -0x40); facing follows; speed := 0; return   ; quarter turn, no step, no frame advance [47988-48000]
        if RNG(0x40) < restlessness + 8: byte 9 += RNG(0x40) - 0x20            ; [48030-48062]
        if heading unchanged by the physics: MaybeVectorsToPlayer(byte 9, 10)   ; away from a player within 10 eighths
    else:
        if RNG(0x80) < restlessness: byte 9 += RNG(0x40) - 0x20                 ; [48085-48125]
    standing: stand pose (interval 6, frame++ 1 in 2)                            ; [48130-48214]
    walking:  standing bit clear, speed := wanderSpeed, interval 4, frame++      ; [48233-48249]
    ReactToPlayerPresence                                                        ; 3.3

Goal 2 has no home and no territory. It leaves only through `StandStillGoal` (to 5).

### 6.4 Goal 3 (`seg007_1798_3E4` 48278-48516), squared tile distance

- <= 2: if active: anim := 1 (the swing is display only: goal 3 bypasses the attack frame
  machine, 1.4 step 8), frame++, speed 0, facing := vector heading to the target, interval 4.
- > 64: teleport: `root = sqrt(d2)`, new tile = target tile + (own - target) * 4 / root (four
  tiles short of the target on the line towards the follower); relinked, word 0x16 rewritten
  [48458-48471], fine position tile centre, height := that tile's floor. No walkability test.
- else, if active: `NPC_Goto(target tile)` (wander speed).

### 6.5 Goal 5 (`NPC_Goal5_Attack` 48870-49198)

    if not active: return
    standoff := 4; on level index 7 with the orb standing (PlayerData 0x60 bit 5 clear) and kind 0x13: standoff := 1   ; [48879-48905]
    d2 = XVectorToGTARG^2 + YVectorToGTARG^2                        ; squared eighths [48909]
    d2home = squared tile distance from the home post                ; [48918]
    if gtarg == 1: attitude := 0                                     ; [48957]
    started := false
    if (d2 < 0x64 or same tile as the target) and (|targetFloor - myFloor| < 4 or flier):
        ChooseMeleeAttackToMake(d2)                                  ; 8.2; its return is not used
    elif spellPower > 0:
        if not TryToDoMagicAttack():                                 ; spell 3, 8.5
            if caster: started := NPCStartMagicAttack()              ; spells 1/2
    elif rangedType == 1:
        started := TryMissile()                                      ; seg007_1798_11FB, 8.4
    if started:                                                      ; sight and facing were given
        if anim in {5, 0x0D, 1}: return                              ; attack running
        anim := 0; interval 4; frame++; speed 0; return              ; stands this update [49045-49097]
    ; leash [49129-49172]
    if d2 > 0x100 and backup == 4 and bit 5 clear and d2home > 4 * travelRange^2:
        bits 0, 1 := 0; SetNewGoalAndGtarg(4, 0); return
    AttackGoalSearchForTarget(targetTileX, targetTileY, caster ? standoff : 1)

`AttackGoalSearchForTarget_seg007_E5D(tx, ty, range)` (49597-49843):

    if destination (word 0x0F bits 0-11) != (tx, ty) in x or y:                 ; [49606-49620]
        if RNG(8) == 0:                                                          ; [49626]
            r = SearchForGoalTarget(out tx, ty)
            if r == 1: bits 0, 1 := 0; ResetGoalAndGtarg; return
            if r == 2 and RNG(2) == 0: bit 0 := 0; bit 1 := 1; ResetGoalAndGtarg; return
            SetNPCTargetDestination(tx, ty, targetFloor)                         ; r == 0, or r == 2 with the coin
    if same tile as (tx, ty) and |targetFloor - myFloor| < 4: return              ; [49715-49735]
    if range > 1 and range^2 < tileDist2: goto move                              ; [49740-49750]
    if range^2 * 64 < eighthDist2: goto move                                     ; strict [49755-49775]
    if range > 1: return                                                         ; caster within range
    if |targetFloor - myFloor| < 4: return                                       ; melee within one tile
    move:
    NPC_Goto(tx, ty, targetFloor)
    if byte 0x18 bit 6 (blocked): ResetGoalAndGtarg; bit 1 := 0                  ; [49800-49815]

Consequences:

- A melee creature keeps closing while the squared eighth distance is above 64 (one tile) and
  stands at 64 or below when the floors are within 4 levels.
- A caster (standoff 4) closes while the squared tile distance is above 16 or the squared
  eighth distance above 1024, i.e. it stops 4 tiles away (decision 1).
- `NPC_Goto` writes anim 0x2C and frame++ unconditionally at its end [label `seg006_1477_2FB7`,
  47293-47355], and its head has no guard for a running attack animation [46652-46800]. A
  swing that `ChooseMeleeAttackToMake` rolled at 65..100 squared eighths, and a spell 3 that
  `TryToDoMagicAttack` started outside the stand range, are therefore overwritten by the walk
  in the same update. In goal 5 a blow is only ever executed when it was rolled at <= 64
  squared eighths (or on the target's tile), and spell 3 is only cast when the creature would
  have stood anyway (a caster within 4 tiles). In goal 9 both survive, because goal 9 returns
  right after them (50318-50338).
- The charge nibble (word 0x0F bits 12-15) survives the walk: `NPC_Goto` touches only bits
  0-11 through `SetNPCTargetDestination`.

### 6.6 Goal 6 (`NPCGoal6` 50483-50979)

    if not active: return
    f = GetVectorHeading(vector to target); dz = targetZ - myZ (word 2 bits 0-6)
    if flier: pitch := (zpos > 0x6E ? 13 : 15) + RNG(5)
    if tileDist2 <= 3 and |dz| < 16:                                              ; close [50540-50568]
        if RNG(256) < morale >> 3 or (collided and heading unchanged):
            bit 4 := 1; SetNewGoalAndGtarg(9, gtarg); return                      ; [50569-50606]
        byte 9 := ((f + 4) mod 8) * 32; facing := f; residual := 0                ; walks away, looks at the target
        anim := 7; frame++; speed := (wanderSpeed + 1) / 2; return                ; [50607-50660]
    if collided and heading unchanged:                                            ; far, cornered [50661]
        if tileDist2 < 9:
            if goal == 9: facing := f, speed 0, anim 0, interval 4, frame++; return   ; called from goal 9
            bit 4 := 1; SetNewGoalAndGtarg(9, gtarg); return                      ; [50678-50700]
        byte 9 := ((f +- 2) mod 8) * 32 + RNG(32)                                 ; quarter turn [label 1692]
    else:
        if TryToDoMagicAttack(): return                                           ; [label 16D2]
        if RNG(0x40) < restlessness + 8: byte 9 += RNG(0x40) - 0x20               ; [50845-50858]
        if heading unchanged: MaybeVectorsToPlayer(byte 9, 0x18)
    speed := (tileDist2 < 64 ? pursuitSpeed : wanderSpeed); anim := 0x2C; frame++; interval 4   ; [50936-50973]

Nothing in goal 6 returns to 5 by itself; that happens through the damage reaction, the
target's death, or a script.

### 6.7 Goal 9 (`NPCGoal9` 50284-50477)

    if not active: return
    if eighthDist2 < 0x90 or same tile: ChooseMeleeAttackToMake(eighthDist2); return     ; no floor test [50313-50325]
    if tileDist2 > 4:                                                                    ; [50330]
        if TryToDoMagicAttack(): return
        if spellPower > 0: NPCStartMagicAttack()
        elif rangedType == 1: TryMissile()
        else: NPCGoal6()                                                                 ; as a routine, goal stays 9 [50374]
        return
    facing := vector heading; speed 0; anim 0; interval 4; frame++                        ; hold within 2 tiles [50379-50451]

### 6.8 Goal 10 (`GoalTalkto` 51162-51377)

    if not active: return
    gtarg := 1; GetDistancesToGTarg; d2 = eighthDist2; r = SearchForGoalTarget()
    if r == 1 or d2 >= 0x190: stand pose; return                                   ; [51248]
    facing := vector heading; byte 9 := facing * 32; speed 0; anim 0x20; interval 6
    if d2 < 0x90:
        rel = (playerFacing + 8 - facing) & 7
        if 3 <= rel <= 5: TalkTo()                                                 ; the player looks back within one eighth [51330]

No attitude check, no movement; the conversation changes the goal.

### 6.9 Goal 11 (`Ethereal_seg007_1798_318D` 53968-54030)

Every update, skipping the whole preamble: interval 4, speed := RNG(2), byte 9 := RNG(256),
pitch := 15 + RNG(3), frame++, standing bit set. No detection, no reaction to damage.

### 6.10 Goal 12 (`NPCGoalC` 51513-51649)

    if not active: return
    if attitude == 0: SetNewGoalAndGtarg(4, 1); return
    if not on the home tile: NPC_Goto(home) else stand pose (interval 6)         ; [51605]

### 6.11 Leash, re-search cadence, phase counter

- Leash: only goal 5, only with backup 4 and bit 5 clear, only while the target is farther
  than 0x100 squared eighths (2 tiles), when the squared tile distance from the home post
  exceeds 4 * range^2 (twice the travel range) [49129-49172]. Goal 8 walks home beyond
  range^2 [48600]. Goals 1 and 12 walk to the post itself. Goal 2 has no home.
- Re-search: 1 in 8 per goal-5 update, only when the stored destination differs from the
  target's tile in x or y; a player standing still on his tile is never re-searched, one who
  changed tile on one axis already is [49606-49626]. The stored destination is updated by every
  `SetNPCTargetDestination` and by `NPC_Goto`.
- Phase counter: the frame nibble (word 0x0B bits 12-15) is cycled 0..3 by the goal routines;
  footsteps play on odd frames, the wander switches only at frame 3 (so at most once per
  second while walking), the idle pose advances it with probability 1/2.

## 7. Movement

### 7.1 NPC_Goto_seg006_1477_29A1 (46652-47363), arguments target x, y, z

    SetNPCTargetDestination(x, y, z)                    ; word 0x0F bits 0-11, word 0x0D bits 4-7; if changed: bit 5 := 1, bit 6 := 0 [46574-46646]
    if bit 5 and has path: release the path             ; [46680]
    if on the target tile:                              ; [46700-46745]
        release the path; if goal == 1: SetNewGoalAndGtarg(8, 0)
        elif active: speed 0; standing bit; anim 0x20
        return
    if not active: interval := 1; advance the path record if on its tile; return     ; [46747-46785]
    if collided and heading unchanged and not blocked:                             ; [46841-46960]
        if an object was hit:
            if a closed door was among the records:
                anim 0x20; if RNG(4) == 0 or attitude != 0: blocked := 1 else NPCTryToOpenDoor   ; [46870]
            elif the object is a creature, not item 0x7F (the player), and both goals are 5: nothing   ; [46880-46925]
            elif the object is an open door (0x148..0x14F) and I am a flier: pitch 14, collision cleared, speed 0 this update   ; [46930]
            else: blocked := 1                          ; the player included (decision 2) [46937]
        if still collided: release the path; bit 7 := 0; skipStraight := 1   ; [46954]
    if has path: TurnTowardsPath (7.4); at the end of the path release it
    elif bit 7 (straight known) and not bit 5: heading := GetVectorHeading(target tile - my tile); byte 9 := heading * 32; facing := heading; residual 0; flier pitch (7.6)   ; [46906]
    elif blocked and not bit 5: if RNG(8) == 0: blocked := 0; NPCWanderUpdate; return   ; [47000-47020, label 2F86/2F9A]
    else:
        if not skipStraight and the straight tile line is clear (seg006_1477_1938): bit 7 := 1; heading as above; blocked := 0; release path   ; [47030-47050]
        elif a slot is free and PathFindBetweenTiles succeeds: fill the slot; blocked := 0; has path; TurnTowardsPath   ; [47060-47261]
        else: blocked := 1; bit 7 := 0; NPCWanderUpdate; return
    unless a hop started this update (dseg_2472):       ; [label seg006_1477_2FB7, 47293-47355]
        standing bit clear; anim := 0x2C
        speed := (goal == 5 ? table 0x0C : table 0x0B) & 0x7F (0 for a flier that just ducked under a door)
        frame++; interval := 4

`NPC_Goto` aims at a TILE, never at a sub-position; arrival is tile equality. While the
straight line is known clear the heading is the eight-way heading of the tile difference.

### 7.2 Step length

Momentum = speed * 0x2F (47) [`InitMotionParams` 100347-100380]; the physics consumes it once
per update over `speed_12 = interval * 16` PIT ticks [`CalculateMotionTopLevel` 41703]. The
player's full walk uses momentum `BaseForwardSpeed_1dseg_5c99_CE` = 0x3AC (940) [280275] over
`elapsed` PIT ticks per frame through the same stepper (`InitalMotionCalc_seg030_410` 103373,
`LikelyTranslateXY_seg030_8A4` 104045). A creature at speed s therefore covers, per 0.25 s
update, s/20 of what the player covers at full walking pace in 0.25 s, independent of the
frame rate. With the stepper's scale of 0x2000 sub-units per eighth (read while the movement was first worked out, not re-derived here):
eighths per walking update = s * 47 * 64 / 8192 = 0.367 * s; speed 12 moves 0.55 tiles per
update (2.2 tiles/s), speed 8 0.37 tiles (1.47 tiles/s), speed 6 0.275 tiles (1.1 tiles/s),
and the player at full walk 0.918 tiles per 0.25 s (3.67 tiles/s). The ratio s/20 is the
robust statement; the absolute scale rests on the stepper reading and should be confirmed in
game (section 12). A stuck creature (interval 1) moves a quarter step.

### 7.3 Collision rules and the blocked bit

The physics does not slide. Per step the 3 x 3 tiles around the body (corners at +- the
COMOBJ radius) and the objects in the tile (every object with COMOBJ height > 0, creatures
and the player included, `ScanForCollisions_seg026_BF6` 93164) are tested; bits masked by the
handler's word 0 are dropped, bits in word 1 call the handler callback, and when it returns 1
the motion ends at the last good position [`ProcessCollisions_seg030_115F` 105453,
`DoTileCollisionMaybe_seg030_2B26_1640` 106199]. Wall, solid and object bits (0x700) deflect
the heading (`seg030_2B26_A47` 104317; for creatures the sub-tile position snaps to the tile
edge instead of a mirror reflection) and retry up to two further steps; an object collision
pushes the other object when its COMOBJ allows (`DoCollision_seg030_2B26_C93` 104680). The AI
sees `RelatedToMotorCollision` (a callback fired), `dseg_246D` (an object was hit),
`RelatedToColliding` (a closed door), `HasCurrObjHeadingChanged` (byte 9 changed), and
`IsNPCActive` (0 when stuck or drowning).

Two outcomes for the AI: a deflected heading (HasCurrObjHeadingChanged) never sets the blocked
bit; the turn limiter then keeps the deflected heading (7.7). An unchanged heading with a
collision sets the blocked bit (7.1), except a creature-vs-creature bump where both attack, a
closed door a hostile creature tries (3 of 4), and an open door for a flier. A collision with
the player sets the blocked bit like a wall (decision 2).

The wander step while blocked: every blocked update rolls RNG(8) == 0 to clear the bit and runs
`NPCWanderUpdate` in the same update (6.3): a further collision with unchanged heading turns
the fine heading a quarter circle left or right and zeroes the speed for that update; a hostile
creature runs `StandStillGoal` on half of the updates (which may re-acquire the target or, in
goal 4/8, walk home); walking or standing follows restlessness.

Terrain collisions (water edge, lava edge, too high a step, no floor) set none of the object
flags; the path is still released and bit 7 cleared. Lava and drops collide only when the start
tile did not already have the bit and no path is held; airborne sets gravity -4, interval 1,
inactive. Lava deals 1 point of plain fire (type 8) at a chance of 1 in 5 per physics update
while the creature moves on it (`ApplyProjectileMotion`, label 7F4; read 2026-10-02 - every update
and type 0x0B before); a standing creature runs no physics and takes none.

**Drowning** (built 2026-09-20, deviation 43). The land creature's motion callback
`seg006_1477_431` tests the tile state it rebuilt after the step; the water branch is
`seg006_1477_476` [41417-41460]:

1. [41418-41421] `mov ax, [si]; and ax, 0F8h; cmp ax, 10h` - of the five terrain and footing
   bits (0x08 plain floor, 0x10 water, 0x20 lava, 0x40 the fourth terrain, 0x80 standing on an
   object) only water may stand. Water under the feet with nothing to stand on.
2. [41422-41423] `RelatedToMotorCollision` = 1, `IsNPCActive` = 0 - the motion ends here and
   the next update treats the creature as inactive.
3. [41424-41443] `SpawnClass7Object_seg044_A33` with class index offset **6** and animation
   duration **3** at the creature's position. The routine adds 0x1C0 to the offset [142859], so
   the object is 0x1C6 = 454, string block 4 entry 454 "a_splash". Exactly one.
4. [41444-41448] byte 0x15 bits 0-5 = **0x0C**, the death animation.
5. [41449-41453] word 0x0B bits 12-15 = **3**, the death removal frame.
6. [41454-41458] byte 0x14 bits 0-2 = **1**, the interval - the creature is due again after one
   slot, and that update's animation machine runs the death removal [53018-53090].

Only land creatures get here. The flier callback `seg006_1477_5FE` and the swimmer callback
`seg006_1477_65E` have no water branch, and the swimmer's handler word 0 = 0x0010 drops the
water bit before the callback runs (section 7.6); a swimmer collides with plain
floor (0x8) instead.

The user watched this in the original on 2026-09-20 (level 5, a goblin at the water's edge):
a creature cannot be pushed into the water by walking into it; it gets in only while it is in
melee and closing in on the player (the direct approach of `ChooseMeleeAttackToMake` sets the
heading straight at the target without any tile test, 6.5); when the player is too far away it
switches to a ranged attack instead of walking in. There is a splash with a sound, and the
creature disappears at once - **no loot and no pool of blood**. The level data of the save
taken afterwards confirms it: the goblin's record is gone from the object list, and neither its
blood stain (221 = 0xD8 + 5) nor bones (198 = 0xC0 + 6) nor any of its belongings lie on the
water tiles.

**Why nothing is left - the water, not the death code.** The branch at animation 0x0C frame 3
calls `DropNPCLoot`, `DropNPCRemains` and `SpillCritterInventory` for a drowned creature like
for any other [53055-53080], and the goblin's table says blood index 5 and corpse index 6, so
both are placed. They are then destroyed where they land. Every object that comes to rest goes
through `PlacedObjectCollison_seg029_104D`, and there the tile state's low three bits - terrain
index in bits 0-1 (`seg026_2C9`: 0 normal, 1 water, 2 lava) plus the on-ground bit 4
(`seg026_33E`) - decide: value 5 (water on the ground) sets the destroy flag [102349-102356]
and the object is removed again at the end [102560-102570], value 6 (lava) applies fire damage
8 instead [102358-102380]. The blood [41008], the corpse (via `PlaceObjectAtNPC_seg026_12E5`
-> `MoveObjectToCoordinates_seg026_122B` -> `InsertObjectToList_seg026_133F`) and the spilled
inventory all run through it; only the rolled loot of `SpawnNPCLoot_ovr150_516` [400462] uses
the raw `InsertObjectToList_seg027_561` without it.

Three measurements of the user pin the behaviour (all 2026-09-20):

1. Drowned in the middle of the water: nothing at all is left on the tiles, and exactly ONE
   splash was seen and heard.
2. Drowned close to the shore: a piece of meat stayed lying on the land, while what ended up
   in the water was gone - and made no sound of its own.
3. A creature killed at the water's EDGE: loot that falls into the water vanishes with a
   splash of its own, one per object.

So an object PLACED directly onto a deep water tile is removed silently, and an object that
FALLS into water splashes when it lands. The single splash of a drowning is the creature's own.

The port builds it that way. The drowning takes the ordinary death path with loot, remains and
inventory (`UWCritterRemains.CompleteDeath`); the water rule lives in the one place every
dropped, thrown, scattered and spilled object passes, `UWLevelLoader.fSpawnObjectAt`, which
destroys silently what sinks on a water or lava tile. Ours still asks
`UWCommonObjectProperties.SinksInLiquid` (COMOBJ byte 9 below 40, from the user's throwing
tests of 2026-08-31) where the original asks only the tile; blood, bones and meat all sink, so
the measured cases agree, and the splash for a falling object is not built yet (Todo.md
section 4).

### 7.4 The path search

Invoked from `NPC_Goto` when neither the straight line is known nor the creature is blocked, or
when the destination is new [47030]. First the straight-line test `seg006_1477_1938` (44397): a
Bresenham-like walk over the tiles (slope in 1/128, accumulator 0x40), each tile appended to
the seg056 list (at most 0x3F) and tested with `TraverseMultipleTiles_seg006_1477_6F6` over
the last three tiles (45432). Success sets bit 7 and the eight-way heading.

`PathFindBetweenTiles_seg006_1477_12BB` (43491): breadth-first over the four neighbours in a
box 5 tiles beyond the smaller and larger of start and target on each axis, clamped to
1..0x40 [43551-43678]; two alternating 64-entry wave lists [44210]; depth limit 32 [44239]
(both in `UWTilePath` since 2026-09-29 as MaxWaveWidth and MaxPathLength - before, ours found
ways of any length inside the box, and Biden walked towards a home 42 steps away that the
original never finds); a
neighbour is accepted when `TraverseMultipleTiles` allows it and it is unvisited, or visited
with a longer path and a worse height match. The path is stored as x, y, z and a climb flag per
tile. Slots: the first free bit of `PathfindBitField` (`seg006_1477_21D1` 45613); no free slot
means no path (blocked, wander). `UpdateSeg57TurningValues_seg006_1477_2244` (45701) fills
the slot: start tile, step index (bit 7 = current step is a climb), length, two bits per step
for the direction, one bit per step for the climb flag. The creature stores the slot in word
0x16 bits 0-3 and sets byte 0x15 bit 7 [47250-47261].

Budget: `TraverseMultipleTiles` (reference reading, not re-read) tests walls from the tile-type
table, the handler's terrain masks (word 2 impassable, word 3 costs 2), a drop of more than one
level costs (drop - 1), a climb of more than one requires table byte 0x0A bit 5, costs 1 and
sets the climb flag; any cost above the range from `seg007_1798_340A` (4.4) fails, so only a
hostile creature descends or crosses lava on a path. A closed door on the middle tile is
passable when `CharacterDoorLockAndKeyInteraction(creature, door, 0)` returns 0.

Following: `TurnTowardsPath_seg006_1477_2504` (46064) every update while bit 7 of byte 0x15
is set: `seg006_1477_24A8` (45991) counts the creature as on the current path tile once its
fine position is 6 or more (or 1 or less) towards it (corner cutting two eighths before the
edge); `MaybeUpdatePathFlag_seg006_1477_23CB` (45887) then advances the record or returns 0
at the end (release). The aim point of a normal step is `tile * 8 + 4` on the axis where the
tile column equals the creature's, `+ 7` when the path tile lies below (smaller coordinate),
`+ 0` when above; heading by `GetVectorHeading` of aim minus fine position [46170-46230]. A
climb step (`seg006_1477_2679` 46247) aims at `tile * 8 - 2`, `+ 6` when equal, `+ 11` when
smaller, and when the Manhattan distance to the aim point is below 3 starts the hop: heading
towards the next tile, `dseg_2472 := 1`, interval 1, pitch 22, speed 11 [46420].

Abandonment: any collision (46954), a new destination (bit 5), the end of the path, entering
the wander step, an animation that is neither walk nor stand (1.4 step 5), arrival.

### 7.5 Doors and door skill

`NPCTryToOpenDoor_seg006_1477_3123` (47474): portcullises (item id low 3 bits == 7) return
at once. With table byte 0x2E (door skill) nonzero the door is used (`ObjectUse_seg040_352B_2`
on the door tile found by the collision scan). If it is still closed (class 0x14, index below
8): with skill and RNG(2) != 0 `UnlockDoor_seg040_352B_1D3B` with minus the skill (a lockpick
attempt) [47540]; otherwise RNG(4) == 0 bashes it: `DamageObject` with RNG(table byte 0x14)
damage of type 4 [47570]. Reached only by a hostile creature on 3 of 4 door collisions. The
door collision comes from the land handler (`seg030_2B26_170C` 106351); fliers and swimmers
have no door branch and simply go blocked, except a flier under an OPEN door.

### 7.6 Fliers and swimmers

Handler tables (`InitVariablesAndDelegates_seg006_1477_28E` 41231): land {0, 0x1F30, 0x1010,
0x0020} with callback `seg006_1477_431`; flier {0x1000, 0x0700, 0x0080, 0} with
`seg006_1477_5FE` (41590); swimmer {0x0010, 0x1728, 0x10A8, 0} with `seg006_1477_65E`
(41625-41671). Word 0 = tile-state bits ignored, word 1 = bits that call the callback, words
2 and 3 = path terrain masks (impassable, costs 2). Tile-state bits (reference-derived names):
0x8 plain floor, 0x10 water, 0x20 lava, 0x40 terrain 3, 0x80 standing on an object, 0x100 step
too high, 0x200 solid or ceiling, 0x400 object, 0x800 floor below, 0x1000 airborne.

- Flier: ignores airborne (never falls); 0x200 collides, 0x100 sets the ceiling flag, 0x400 the
  object collision; water and lava passable on paths (no height checks). Pitch (byte 0x14 bits
  3-7, 16 = level) is the vertical component `(pitch - 16) * 64` [100300].
  `seg006_1477_3061` (47369), called when heading straight or along a path: target height =
  min(0x78, 20 + 8 * destination floor); zpos below target - 8, or ceiling touched below 0x78:
  pitch 18; zpos above 0x78 or above target + 8: pitch 14; else 15 + RNG(3) [47390]. Wander
  rolls (6.3), melee rolls (8.2: 18 if the target's zpos + 14 is more than one level higher,
  14 if lower, else 15 + RNG(3)) and goal 6 rolls (6.6) add height variation.
- Swimmer: plain floor (0x8), 0x100/0x200, 0x400 and airborne collide, water is ignored; no
  height code of its own, zpos comes from the physics. Reaching plain floor gives a collision
  and the blocked bit; the distance it keeps from a bank is only its collision radius.

### 7.7 The turn limiter `seg007_1798_1FDC` (52016-52168)

Runs after the goal routine [54061]:

    wanted = facing eighth * 32 + residual (as the goal left it); before = CurrObjTotalHeading (this update's snapshot)
    diff = (wanted - before) mod 256
    if 0x20 <= diff <= 0xE0: wanted = before + (diff < 0x80 ? 0x20 : -0x20)      ; at most 45 degrees per update [52040-52060]
    write wanted into word 2 bits 7-9 and byte 0x18 bits 0-4
    if HasCurrObjHeadingChanged: byte 9 := CurrObjProjectileHeading; return      ; keep the physics' deflected heading, speed untouched [52085, label 2116]
    if speed before the goal <= 1 or speed now (byte 0x13 & 0x7F) <= 1: return   ; [52087-52100]
    d = (byte 9 as the goal set it - CurrObjProjectileHeading) mod 256
    if d < 0x20 or d > 0xE0: keep the goal's byte 9                              ; small turn taken directly
    elif d < 0x40:  byte 9 := old + 0x20                                         ; clamp to 45 degrees
    elif d > 0xC0:  byte 9 := old - 0x20
    else: speed := 0 (bit 7 kept); byte 9 := old                                 ; a reversal of 90 degrees or more stops the creature for one update [label 2108]

So a creature turns its picture by at most one eighth per 0.25 s, and a moving creature that
wants to turn around stands one update and then takes the new heading directly.

## 8. Combat

### 8.1 Attack choice

Goal 5 (6.5): melee band `d2 < 0x64` squared eighths (10 eighths, 1.25 tiles) or same tile,
with |floor difference| < 4 unless flier [48965-49000]; otherwise spell power > 0 selects the
magic branch (spell 3 first, then spells 1/2 for casters; a non-caster with spell power walks
in) [49000-49020]; otherwise byte 0x20 bits 5-7 == 1 selects the missile [49030-49040]. A
refused start after sight and facing were given makes the creature stand this update (anim 0,
interval 4, speed 0). Goal 9 (6.7): melee below 0x90 or same tile without a floor test;
beyond 2 tiles magic, then spells 1/2 if spell power > 0, else the missile, else the withdraw
routine. No new attack can start while an attack animation runs, because the animation machine
(1.4 step 9) does not call the goal routine.

### 8.2 The melee swing: ChooseMeleeAttackToMake_seg007_1798_AFF (49204-49591), argument d2

    facing := GetVectorHeading(vector to target); byte 9 := facing * 32; residual 0; standing bit clear   ; [49219-49250]
    if d2 < 0x31 (7 eighths):                                                                   ; [49256-49318]
        if RNG(4) == 0: byte 9 := (facing +- 2) * 32 (RNG(2) picks the side); anim 0; speed := wanderSpeed * 2 / 3   ; side-step
        else: byte 9 := (facing + 4) * 32; anim 7; speed 2                                      ; backs off, picture still faces the target
    elif d2 <= 0x51 (9 eighths):                                                                ; [49349-49363]
        if RNG(64) < dexterity: byte 9 := RNG(8) * 32; anim 0; speed 1                          ; circles
        else: anim 0; speed 0                                                                   ; stands in the combat stance
    else: anim 0x2C; speed 2                                                                    ; walks in (NPC_Goto then sets the pursuit speed)
    if flier: pitch by height difference (7.6)                                                  ; [label CD8]
    if d2 <= 0x64:                                                                              ; [label D51, 49462]
        if RNG(4) == 0:                                                                         ; [label D59]
            r = RNG(100); i = 0
            while i < 2 and chance[i] <= r: r -= chance[i]; i++                                 ; chance[i] = table byte 0x15 + 3i [49480-49516]
            anim := i + 1; interval 4; frame := 0                                               ; [49520-49535]
        else:
            if chargeIndex < 15: chargeIndex++                                                   ; word 0x0F bits 12-15 [49540-49555]
            interval 4; frame++
    else: interval 4; frame++
    return 1

Cadence (decisions 4 and 5): while in range every 0.25 s update rolls 1 in 4 to start a swing.
The swing runs frames 0..4 over the next five updates and the blow executes on the fifth
(1.25 s after the decision); the goal routine runs again on the sixth (1.5 s). The wait before
the next start is geometric with p = 1/4 (mean 3 updates, 0.75 s; minimum 0), so the mean time
between two blows is 8 updates (2 s) and the minimum 6 (1.5 s). Between swings the combat
idle (anim 0) advances one frame per update and the charge index rises by one per failed roll,
capped at 15; the charge is cleared when the blow lands [53070]. Out of range no roll is made
and the charge keeps its value. Remember 6.5: in goal 5 a swing rolled at 65..100 is
overwritten by the walk in the same update, so in goal 5 the blow effectively starts only from
64 squared eighths (8 eighths, one tile) or the target's tile.

### 8.3 To-hit and damage

`NPCExecuteAttack_seg022_15DE` (83437; called at frame 4 with swing type RNG(9), charge value
seg060[charge index], attack i = anim - 1, poison = table byte 0x0F):

- Reach class `dseg_5c99_2656` = 2. `AttackDamage = table[0x14 + 3i] + table[5] / 5`;
  `AttackScore = (signed) table[0x13 + 3i] + (signed) table[0x11] / 2` [labels 1629-1656].
  Word 0x0D bit 10 (strong individual): score += 7 + RNG(6), damage += 4 + RNG(12) [1679].
- `ExecuteAttack_seg022_D9C` (81929): `CheckForAttackHit_seg022_466` (80356) scans for a
  collision from the attacker's position along its facing over reach 2 + 3 = 5 eighths with
  radius 2 + 1 = 3 [labels 48A-4A0], at height zpos + object height * (swingType / 3) / 3
  [4CE]; the nearest collision is the defender; `PickBodyHitPoint_seg022_A` (79423) picks the
  body part 0..3. No collision: wall impact animation, miss sound 0x0A at the attacker, return
  0. Creature against creature with the same byte 0x19 bit 6: dropped [labels DBB-E19] (allies
  never damage allies, monsters never damage monsters).
- `CalcFlankingBonus_seg022_230E_D48` (81871): `d = (defenderFacing + 12 - attackerFacing) & 7`,
  bonus = d if d <= 4 else 8 - d (0 from the front, 4 from behind).
- `CalculateAttackResults_seg022_230E_6B9` (80688): defender player: score -=
  `EnchantmentProtectionSlots[part]` [76F]. `SkillCheck_seg037_32E6_C(score + flank,
  defenderRow[0x12])` (123242; the player's own row and what fills it: 8.8): `v = skill - check + RNG(31)`; v > 28: critical hit (damage
  times `(RNG & 31 + 48) >> 5`, i.e. x1 or x2; red flash 0xB8; armour piece damaged); v > 15:
  hit; v > 2: miss; else critical miss. A miss still calls `DamageObject` with 0 damage
  (which records the attacker and makes the target hostile) plus the impact sound.
- `AttackerAppliesFinalDamage_seg022_8A5` (81050): `D = max(2, AttackDamage)`; rolled =
  `(D / 6)d6 + 1d(D % 6)` [8FC, 910]; damage = rolled * charge >> 7 [921] + flank [930]; sound
  3 at the avatar or 4 at the object with volume damage * 4 [938-98D]; armour `a =
  row[part % 4]` (0xFF in byte 3: `a = row[0]`), `a = a * 5 / 3` for a strong defender other
  than the player; damage = a >= damage ? 0 : damage - a [990-A3A]; halved for the player on
  the easy difficulty (PLAYER.DAT 0xB4 != 0) [A28]; `DamageObject_seg023_35A(defender,
  attacker, tile, damage, type 4)` (84583) through `ScaleDamageAgainstObject_seg023_2FF`
  (COMOBJ resistances) into `DamageNPC` for creature defenders. Aftermath: screen shake
  (0x20, min(3, damage / 4) * 5) for the player; for a creature defender hit by the player the
  UI eyes; blood (table byte 8 bits 3-4 nonzero) as a class-7 animation offset 1 with duration
  min(3, damage / 4) at HitZ[part] from {5, 3, 1, 7, 0}, a second one on a critical by the
  player; otherwise the impact flash offset 0x0B [B49-C17].
- Charge table seg060 (263511): 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 155, 170, 185,
  205, 230, 255; the blow's damage is multiplied by value / 128.

### 8.4 Missiles

Attempt `seg007_1798_11FB` (50175-50278): squared tile distance < 16 (4 tiles);
`TestBetweenPoints` eye to eye; `TurnTowardsTarget(1)` must report facing; then
RNG(0xC0) <= dexterity starts anim 5 (speed 0, frame 0) [50237-50259]. Returns 1 whenever
sight and facing were given. Launch at frame 4: velocity = ammo table[ammo * 3 + 1];
`GetPitchToGTarg_seg007_1798_22C1(velocity, 1)` (52382): dz = target zpos - own zpos, dist =
sqrt(squared eighth distance); pitch = clamp(dz * 4 / dist, -15, 15) (or +-15 at dist 0), plus
dist * 3 / velocity for gravity; `NPCMissileLaunch_seg025_262` (89492): object 0x10 + ammo
index at the launcher's position, heading = launcher's byte 9, zpos = launcher zpos + COMOBJ
height * 5 / 6 + 2 * pitch, byte 0x12 = launcher [`PrepareProjectileObject_seg025_791` 90266,
label 909]. Hit `seg029_29EE_AD` (99534) and `MissileAttackHit_seg022_14BF` (83234): no skill
roll; damage = ammo table byte 0, charge 0x80, flank 0, body part from `PickBodyHitPoint` + 4,
then the same final-damage path with damage type = minus ammo table byte 2. The projectile
flies until it collides; the 4-tile limit is only on starting.

### 8.5 Magic

- `TryToDoMagicAttack_seg007_1798_FF2` (49849-49952): table byte 0x2C != 0xFF; RNG(256) <
  spell power (byte 0x2D bits 1-7) [49862]; no no-magic bit on the creature's tile; not
  Tybal's kind on level 7 while the orb stands; then speed 0, anim 0x0D, byte 0x19 bits 2-3 :=
  3, frame 0, return 1. No range and no sight test (but see 6.5 for goal 5).
- `NPCStartMagicAttack_seg007_1798_109F` (49958-50169): magic-allowed tile; not Tybal with orb;
  squared tile distance < 64 (8 tiles); `TestBetweenPoints` eye to eye; `TurnTowardsTarget(1)`;
  then RNG(128) < spell power [50090] starts anim 0x0D with bits 2-3 := (RNG(16) < 11 ? 1 :
  2) [50125], speed 0, frame 0. Returns 1 whenever sight and facing were given.
- Cast at frame 4 [label 2AAD]: `GetPitchToGTarg(0x1E, 0)`, `SpellTrapWandCast_seg038_27(row
  [0x29 + slot], caster, 0, 0)`; the spell byte indexes the runic spell table (seg064, 4 bytes
  per spell) and `CastSpells_seg038_3307_78` dispatches by class. Then anim 0, frame 0, slot
  bits cleared.
- Distance keeping: within 8 tiles with sight a caster stands every update it fails the roll;
  without sight or beyond 8 tiles it approaches to 4 tiles (decision 1). No mana, no cooldown,
  no spell limit.

### 8.6 Poison, paralysis, sleep

Poison [NPCExecuteAttack labels 16A4-16EC]: after `ExecuteAttack` returned 1 (a hit, even if
the armour absorbed everything) against defender 1: the player's poison nibble (PLAYER.DAT
0x5F bits 2-5) must be below table byte 0x0F and `ScaleDamageAgainstObject(player, 1, 0x10)`
nonzero (resistance); then the nibble := byte 0x0F & 0xF. No roll, no armour test.
Paralysis is only the class-7 spell `Paralyse_seg038_3307_9AB` (125586; against an NPC goal 7
and attitude 1 after a resistance test); sleep is only the player's rest.

### 8.7 Death and the corpse

`DamageNPC` (54563-54802): if hp <= damage: hp := 0, `Death_seg007_1798_35CB` (54504): unless
anim 0x0C already runs, `ProcessDeath_seg007_1798_3577` (54452): whoami != 0 lets
`SpecialDeathCases_ovr107_149F(obj, 0)` veto; else anim 0x0C, frame 0, interval 4, hp 0. Death
sound 6 only when table byte 8 & 7 == 1 [label 35F3]. A kill by the player:
`AwardKillEXP_seg022_1725` (83677): exp = 4 * table word 0x28 + 2d(word 0x28), times
(24 + RNG(24)) / 16 for a strong individual, dragon nod and fanfare theme 9. At frame 3 of anim
0x0C (1.4 step 9): `SpecialDeathCases(obj, 1)`, unlink, `DropNPCLoot`, `DropNPCRemains_seg006_5`
(40801): blood object 0xD8 + (byte 8 >> 5 & 7) if nonzero at the creature's position; corpse
0xC0 + (byte 0x0A >> 2 & 7) if nonzero AND RNG(16) < 7 (7 in 16) [41019-41024] via
`PlaceObjectAtNPC_seg026_12E5`; `SpillCritterInventory`; free the object. One second from death
to corpse; the drowning path sets frame 3 directly, so the body goes on the next update.

### 8.8 The player as a defender: his own critter row

The player is row 63 of the in-memory creature table:
`PlayerCritterData_dseg_7272 = Critters_0_dseg_4A52 + (playerObject.word0 & 0x3F) * 0x30`,
set in `ovr134_152` (383401). In OBJECTS.DAT that row is all zeros - the game FILLS it at
runtime. Nothing in combat treats the player specially for this: `CalculateAttackResults`
(80688) takes the defender's row and rolls against `row[0x12]` (label 797), and
`AttackerAppliesFinalDamage` (81050) subtracts `row[part % 4]` (label 990). Three routines
write into it: character generation `ovr098` (326366) bytes 5, 6 and 7 (strength, dexterity,
the strong-individual bit), `ApplyPlayerSneakScore_seg034_2F89_898` byte 0x1D (3.2), and
`PlayerStatusUpdate_ovr133_784` (382211-383111) everything below.

`PlayerStatusUpdate_ovr133_784` runs on demand, not per frame: 26 call sites, among them
`InitPlayer_ovr098_0` (326408), every inventory change (`ovr120_57` 365544,
`RemoveObjectFromInventory_ovr120_5D0`, `ClickOnInventorySlot_ovr117_0` 359155, `ovr121_48`),
casting and expiry of spells (`CastSpells_seg038_3307_78` 124292, `MajorSpellClassB_seg038_1645`,
`PlayerStatusEffectSpells_seg028_589` 99297), eating, lighting, repairing, the exploding book,
`ChantMantraAtShrine_ovr143_48D`, `Sleep_ovr143_D1F`, `Resurrection_ovr143_1521` and the level
change (`SetCameraAtObject_ovr134_CA8` 385912).

Its order, with `var_6` meaning something different in each block:

1. **Clear** `row[0..3]` [label 795].
2. **The five armour pieces** [7A6-7E5]: for paper doll slots `si` = 0..4,
   `row[LocationDefenceIndex[si]] += ovr133_72D(item)`. The table
   `LocationDefenceIndex_dseg_5c99_1AF8` (283288) is `3, 0, 1, 2, 2` - helmet on the head,
   chest on the body, gloves on the hands, leggings and boots both on the legs.
   `ovr133_72D` (382128-382205) is the value of one piece: 0 for an object below id 0x20 (a
   weapon, and without the +1), otherwise
   `((armourTable[id - 0x20].protection * (object.quality & 0x3F)) >> 6) + 1`. A ruined piece
   therefore still gives 1, and a full plate mail 6 of its table value 6.
3. **The shield** [7EB-867]: the object in slot `7 + (PLAYER.DAT[0x64] & 1)` - the hand that is
   not the weapon hand. Only when its id lies in 0x3B..0x3F (bits 6-8 clear, bits 4-5 both set,
   low nibble 0x0B..0x0F): `var_6 = ovr133_72D(shield)`, then `row[0] += var_6` AND
   `row[1] += var_6`. A shield armours body and hands, nothing else.
4. **The defence value** [86A-938]: `row[0x12] = PLAYER.DAT[0x22]` (the Defence skill), then
   `row[0x12] += PLAYER.DAT[0x21 + var_6] / 2`. Here `var_6` is the skill of the weapon in slot
   `8 - (PLAYER.DAT[0x64] & 1)`: default 2 (Unarmed); for a melee weapon (id 0x00..0x0F) the
   byte `meleeTable[id].skillType` clamped to 3..5, i.e. 3 sword, 4 axe, 5 mace [8D0-8FB]. A
   ranged weapon, a shield or an empty hand all keep 2. PLAYER.DAT 0x21 is Attack, 0x22
   Defence, 0x23 the first named skill, so `0x21 + 2` is Unarmed and `0x21 + 3` Sword.
   **This is the whole defence value an attacking creature rolls against.**
5. **Reset** `ResetPlayerStatusValues_ovr133_1E4` (380861): the four protection slots
   `EnchantmentProtectionSlots_dseg_5c99_266A`, the stealth bases (3.2), the movement bits, the
   light and the regeneration flags.
6. **Active spells** [9DD-A1E]: for each of the `PLAYER.DAT[0x5F] >> 6 & 0xF` entries at
   `PLAYER.DAT[0x3E + 2i]`, `ActiveEnchantedItem_Spells_ovr133_347(major = byte >> 4,
   minor = byte & 0xF, accumulator, slot = -1)`.
7. **Worn enchantments** [A25-ABB]: slots 0..10, filtered by `ovr120_BA2` (367615) - the five
   armour pieces, the two rings (9, 10) and the SHIELD hand when a shield is in it; never the
   weapon hand, never the two shoulder slots (5, 6). Same call with `slot = si`. The boots slot
   is also checked for object 0x2F (dragon skin boots) for the lava flag [AA7-AB6].
8. **The stealth and resistance pass** `ApplyDefenceStealthBonuses_ovr133_65D` (381902) with the
   accumulated word (3.2 for the stealth half).

`ActiveEnchantedItem_Spells_ovr133_347` (381211-381624), the case distinction both loops run
through, as far as it touches defence:

- **Class 0xC** (`ClassC_ProtectionToughness_ovr133_49F`, 381456): returns at once when the
  slot is negative [49F] - a CAST spell of this class does nothing, only a worn item counts.
  The body parts are `LocationDefenceIndex[slot]` for slots 0..4, and parts 0 AND 1 for
  everything above [4C6]. The value is `(minor & 7) + 1`, and bit 3 of the minor class decides
  where it goes: set, it is added to `row[part]`, i.e. ARMOUR; clear, to
  `EnchantmentProtectionSlots[part]` [4DC-514].
- **Class 3 minor 1** (luck, 381294): `EnchantmentProtectionSlots[0..3] += 3`.
- **Class 2** (resistances, 381235): keeps the highest minor class in bits 4-7 of the
  accumulator.

`EnchantmentProtectionSlots` is not armour: `CalculateAttackResults` subtracts
`EnchantmentProtectionSlots[BodyPartHit]` from the attacker's score before the skill check, and
only when the defender is the player [76F]. So protection lowers the hit chance, armour
absorbs damage.

**A bug in the original**: the armour bonus of the resistance spells never arrives.
`ApplyDefenceStealthBonuses` shifts its argument right four times in the stealth loop [6CA] and
then reads `si >> 4` for the armour nibble [6DF], i.e. bits 8-11 of the original word - while
class 2 writes the resistance into bits 4-7 and clears everything above it [379]. The loop that
adds the nibble to `row[0..3]` therefore always adds zero, so Resist Blows gives no armour in
UW1.

**The measurement that found this** (user, 2026-09-20): a reaper (item 119, attack 0 to-hit 18,
row byte 0x11 = 25, so attack score 18 + 12 = 30) NEVER produced the critical flash against a
character with Defence skill 30, while the port did. A critical needs
`score - check + RNG(31) > 28`, and the roll reaches 30, so it is impossible from
`check >= score + 2`. With the bare skill 30 that is 2 of 31 rolls (about 7 percent of hits);
half a weapon skill of 4 already closes it, and a sword skill of 25 puts the check at 42, where
only 3 of 31 rolls hit at all and none of them criticals.

## 9. Constants

Proposed C# names follow `UWCritterRules` (existing names marked "exists").

| Name | Value | Unit | Meaning | Where |
|---|---|---|---|---|
| SlotPitTicks | 16 | PIT ticks | one slot of the 16-slot clock (about 62.5 ms) | `seg034_2F89_406` 114965-114990 |
| MaxSlotsPerFrame | 4 | slots | clamp of the slot advance per frame (64 PIT ticks) | 114958 |
| DueWindowSlots | 4 | slots | an appointment fires 1..4 slots after its slot | `seg007_1798_3825` 54807 |
| IntervalWalking | 4 | slots | walking, attacking, goals 3, 5, 6, 9, 11, dying | 47356, 49533, 54486 |
| IntervalStanding | 6 | slots | standing idle, goals 10, 12, reacting to the player | 48212, 48819, 51248 |
| IntervalStuck | 1 | slots | stuck, drowning, hop | 41408, 46766, 46420 |
| IntervalUnknownGoal | 7 | slots | goals 13-15 | 54056 |
| FarRescheduleSlots | 8 | slots | slot advance for culled creatures | 52715 |
| FarTileDistanceSquared | 100 | tiles^2 | distance cull (goal 3 exempt) | 52640-52720 |
| KinAlarmPitTicks | 0x200 | PIT ticks | alarm lifetime (about 2 s) | 53553 |
| HearingDivisor | 4 | - | heard if hear2 / 4 > d2 (strict) | 51704-51757 |
| LostMultiplier | 4 | - | lost if d2 >= hear2 * 4 | 51895-51913 |
| FacingToleranceEighths | 1 | eighths of a turn | relative eighth 0, 1 or 7 sees | 51838 |
| ReactToPlayerDistanceSquared | 0x90 | eighths^2 | wander step faces a close or armed player | 51383-51507 |
| WanderAvoidPlayerEighths | 10 | eighths | wander heading bent away from the player | 48062 area |
| WithdrawAvoidPlayerEighths | 0x18 | eighths | goal 6 heading bent away | 50858 area |
| ReSearchChance | 8 | 1 in n | goal 5 re-search per update after a tile change | 49626 |
| UnchangedCoin | 2 | 1 in n | keep the target on "unchanged" | 49690, 48777 |
| InitiativeRoll | 16 | - | RNG(16) <= initiative turns, < initiative searches | 48688-48716 |
| RestlessnessRoll (exists) | 16 | - | start / stop walking at frame 3 | 47906, 47933 |
| WanderDeflectRoll, WanderDeflectBonus | 0x40, 8 | - | RNG(0x40) < restlessness + 8 deflects | 48030-48062 |
| IdleTurnRoll | 0x80 | - | RNG(0x80) < restlessness deflects while standing | 48085-48125 |
| DeflectionHalfRange | 0x20 | heading units | deflection RNG(0x40) - 0x20 | 48062, 50858 |
| QuarterTurn | 0x40 | heading units | wander turn on a collision | 47988-48000 |
| HostileStandChance (exists) | 2 | 1 in n | hostile wanderer runs StandStillGoal | 47781 |
| BlockedClearChance (exists) | 8 | 1 in n | blocked bit cleared per update | 47010 |
| DoorTryChance | 4 | 3 in n | hostile creature tries a closed door | 46870 |
| LockpickChance, BashChance | 2, 4 | - | with skill RNG(2) != 0 picks; else RNG(4) == 0 bashes | 47540, 47570 |
| TurnLimitPerUpdate | 0x20 | heading units | facing and moving heading turn at most 45 degrees | 52040, 52100 |
| ReversalStopThreshold | 0x40 | heading units | a wanted turn of 90..270 degrees stops one update | label 2108 |
| MomentumPerSpeedPoint | 0x2F | - | momentum = speed * 47 | 100347-100380 |
| PlayerBaseMomentum | 0x3AC | - | player full walk; creature = speed / 20 of it | 280275 |
| StepTicksPerInterval | 16 | PIT ticks | speed_12 = interval * 16 | 41703 |
| PathBoxMarginTiles | 5 | tiles | search box beyond start and target | 43618 |
| PathDepth, PathWave, PathSlots | 32, 64, 16 | - | search limits, shared slots | 44239, 44210, 280256 |
| StraightLineMaxTiles | 0x3F | tiles | straight-line test list | 45460 |
| PathCornerCutFine | 6 / 1 | eighths | record advances at fine 6+ or 1- | 45991 |
| PathAimOffsets | 0 / 4 / 7 | eighths | aim point of a normal step | 46170-46230 |
| HopSpeed, HopPitch, HopManhattan | 11, 22, 3 | - | climb hop | 46420 |
| FlierTargetHeightBase, FlierTargetHeightMax | 20, 0x78 | zpos | 20 + 8 * floor, capped | 47390 |
| FlierPitchDown, Up, Level | 14, 18, 16 | pitch | pitch values | 47369-47440 |
| MeleeDistanceSquared (exists) | 0x64 | eighths^2 | goal 5 melee band and start roll limit | 48965, 49462 |
| MeleeStandDistanceSquared (exists) | 64 | eighths^2 | goal 5 stands at or below (1 * 1 << 6) | 49755-49790 |
| StandMeleeReachSquared | 0x90 | eighths^2 | goal 9 melee band | 50313 |
| MeleeBackOffDistanceSquared | 0x31 | eighths^2 | below: side-step or back off | 49256 |
| MeleeCircleDistanceSquared | 0x51 | eighths^2 | up to: circle or stand | 49349 |
| MeleeHeightLevels | 4 | floor levels | goal 5 melee needs a smaller floor difference | 48985 |
| MeleeStartChance | 4 | 1 in n | swing start roll per update | 49469 |
| MeleeHitFrame | 4 | frames | the blow executes at frame 4 (fifth update) | 53040-53080 |
| ChargeIndexMax | 15 | - | swing charge nibble cap | 49540-49555 |
| ChargeTable | 50 .. 255 (16 values) | /128 | damage multiplier by charge index | seg060 263511 |
| SideStepChance | 4 | 1 in n | side-step instead of backing off | 49256 |
| BackOffSpeed, WalkInSpeed, CircleSpeed | 2, 2, 1 | speed | footwork speeds | 49307, 49380, 49363 |
| SideStepSpeedNumerator | 2 / 3 | - | wanderSpeed * 2 / 3 | 49290 |
| WithdrawBackSpeed | (wander + 1) / 2 | speed | goal 6 close branch | 50650 |
| WithdrawNearTilesSquared, WithdrawNearHeight | 3, 16 | tiles^2, zpos | goal 6 close branch | 50540-50568 |
| StandRoll | 256 | - | RNG(256) < morale >> 3 | 50569 |
| CorneredTilesSquared | 9 | tiles^2 | goal 6 far branch collision -> 9 | 50678 |
| FleeFastTilesSquared | 64 | tiles^2 | pursuit speed below, wander speed above | 50936 |
| HoldGroundTilesSquared | 4 | tiles^2 | goal 9 faces and holds | 50330 |
| LeashTargetDistanceSquared | 0x100 | eighths^2 | leash only beyond 2 tiles | 49129 |
| LeashRangeFactor | 4 | - | 4 * range^2 tiles^2 from home | 49141 |
| HomeRangeFactor | 1 | - | goal 8 walks home beyond range^2 | 48600 |
| FollowNearTilesSquared, FollowTeleportTilesSquared, FollowTeleportShortTiles | 2, 64, 4 | tiles | goal 3 | 48300-48470 |
| TurnToTargetDistanceSquared (exists), TalkDistanceSquared (exists) | 0x190, 0x90 | eighths^2 | goal 10 | 51250, 51330 |
| CasterStandTiles (exists) | 4 | tiles | caster stand-off | 48879, 49740-49775 |
| CasterSpellTilesSquared | 64 | tiles^2 | spells 1/2 need < 8 tiles | 49958-50169 |
| MissileTilesSquared | 16 | tiles^2 | missile needs < 4 tiles | 50175-50278 |
| MissileRoll | 0xC0 | - | RNG(0xC0) <= dexterity | 50237 |
| Spell3Roll, Spell12Roll | 256, 128 | - | RNG(n) < spell power | 49862, 50090 |
| PrimarySpellChance | 11 of 16 | - | spell 1 else spell 2 | 50125 |
| AttackReachEighths, AttackReachRadius | 5, 3 | eighths | collision scan of a blow (2 + 3, 2 + 1) | labels 48A-4A0 |
| SkillCheckRoll, CritThreshold, HitThreshold, MissThreshold | 31, 28, 15, 2 | - | SkillCheck | 123242 |
| StrongToHitBonus, StrongDamageBonus | 7 + RNG(6), 4 + RNG(12) | - | word 0x0D bit 10 | label 1679 |
| StrongArmourFactor | 5 / 3 | - | defender armour | label 990 |
| MissileCharge | 0x80 | /128 | missile hits use charge 128 | 83234 |
| PoisonResistanceType | 0x10 | - | ScaleDamageAgainstObject type | label 16B1 |
| ExperienceFactor | 4 | - | 4 * base + 2d(base) | 83677 |
| CorpseChance | 7 of 16 | - | corpse object placed | 41019-41024 |
| DeathRemovalFrame | 3 | frames | body removed at frame 3 | 53030-53090 |
| PlayerNoiseBase, PlayerVisibilityBase | 13 - Sneak / 3, 15 - Sneak / 5 | nibble | player row byte 0x1D | 380878-380916 |
| PlayerNoiseSpeedTerm | speed * 10 / forward - 5 | nibble | moving | 115605-115709 |
| PlayerNoiseWater, PlayerNoiseEasyMove | +4, base + 4 | nibble | | same |
| PlayerNoiseDecayCalls | 8 | calls | decays by one every 8th call, rises at once | same |
| TheftAreaTiles | 15 x 15 | tiles | AngerNPC area | 341521-341620 |
| TheftItemHeightBonus | 12 | zpos | sight line to the item | 341300 area |

## 10. Deviations of the port from the original

RE-EVALUATED 2026-09-23 against the code, row by row. "Port today" names classes and
symbols instead of line numbers, which drift - the first version quoted the lines of
2026-09-19, before the Unity host was rebuilt around the brain. "Decision" says what
became of the proposal, with its date. THE NUMBERS STAY PUT: the code cites them
("deviation 43", "deviation rows 45-51"); a row whose deviation is gone stays and says so.

What is left after the re-evaluation, apart from the rows kept on purpose (14, 37, 43's
shore, 44, 62): the wall reflection of a creature's heading (13), the push of the player
(10) and the aim of a creature's missile (46, researched 2026-09-23 and moved to P6 as well). Theft (40), the allies (52), the combat music
(53) and the diagonal walls in the sight line (23) were closed 2026-09-23; the noise decay (24) turned out identical, only its call rate
stands in for the original's frame rate.

| # | Topic | Original | Port today | Decision |
|---|---|---|---|---|
| 1 | Update cadence | per-creature interval in slots of 1/16 s on a shared 16-slot clock: 4 while walking or fighting, 6 standing, 1 stuck, 7 unknown goals; the slot test is the only filter (1.1-1.5) | the brain runs on the shared 16-slot clock (`UWCritterClock`), per creature `UWCritterRecord.Interval` = `UWCritterRules.IntervalWalking` 4, `IntervalStanding` 6, `IntervalStuck` 1, `IntervalUnknownGoal` 7; `UWCritter` only calls `UWCritterBrain.Update` when the clock says due; the old `CritterDecisionInterval` is gone | ADOPTED 2026-09-20 (the brain) |
| 2 | Melee cadence and stance (decision 5) | 1-in-4 roll per update in range starts a swing; blow at frame 4 on the fifth update; goal resumes on the sixth; combat idle frames and charge index advance per update between swings; no stance requirement, no fixed interval (8.2) | `UWCritterBrain` rolls `fRandom(UWCritterRules.MeleeStartChance)` == 0 per update in range and runs the blow at frame 4; `mfNextAttackTime`, `CritterAttackInterval` and the stance gate are gone | ADOPTED 2026-09-20 (the brain; measured against the original the same day) |
| 3 | Effective melee start range in goal 5 | rolled at <= 0x64 but a swing rolled at 65..100 is overwritten by `NPC_Goto`'s walk in the same update; net: blows only from <= 64 squared eighths or the same tile (6.5) | `UWCritterRules.MeleeDistanceSquared` 0x64 for the roll, `MeleeStandDistanceSquared` 64 for the blow, and the overwrite by `NPC_Goto` in between is modelled (see the comment at `MeleeStandDistanceSquared`); `CritterAttackRange` is gone | ADOPTED 2026-09-20 |
| 4 | Swing charge | +1 per failed start roll in range, cap 15, cleared when the blow lands (8.2) | `UWCritterRecord.ChargeIndex`: +1 per failed start roll up to `UWCritterRules.ChargeIndexMax` 15, cleared when the blow lands, read through `ChargeTable` (`UWCritterBrain`) | ADOPTED 2026-09-20 |
| 5 | Ranged and magic cadence | one roll per 0.25 s update: RNG(0xC0) <= dexterity, RNG(256) / RNG(128) < spell power; no cooldown beyond the 5-update animation (8.4, 8.5) | one roll per update: `fRandom(UWCritterRules.MissileRoll)` 0xC0 <= dexterity, `Spell3Roll` 256 / `Spell12Roll` 128 < spell power (`UWCritterBrain`); `CritterDistanceAttackInterval` and the 0.1 s attempt tick are gone | ADOPTED 2026-09-20 (with row 1) |
| 6 | Third spell | rolled with no range or sight test; in goal 5 it only survives when the creature stands anyway (6.5) | `UWCritterBrain.fTryMagicAttack`: no range or sight test, only the no-magic tile and Tybal's orb | ADOPTED 2026-09-20 |
| 7 | Caster distance (decision 1) | standoff 4 tiles: stands within 8 tiles with sight, approaches to 4 tiles otherwise (8.5) | `UWCritterRules.CasterStandTiles` 4, used as the stand-off in the attack goal (`UWCritterBrain`) | ADOPTED 2026-09-20 |
| 8 | Withdraw step after a blow | none in goal 5: after the blow the goal resumes and `ChooseMelee`'s footwork (side-step or back off below 7 eighths) is the only stepping | the footwork of `ChooseMelee` in `UWCritterBrain`: below `MeleeBackOffDistanceSquared` back off with `AnimBackOff` or side-step (`SideStepChance`), circle by dexterity; no withdraw of our own | ADOPTED 2026-09-20 |
| 9 | Deliberation before pursuing | none: goal 5 never rolls initiative; the pause the user sees is the running swing (5 updates without goal logic), the turn limiter and the 0.25 s cadence | goal 5 rolls no initiative; initiative is rolled only where the original does, in `StandStillGoal` (`UWCritterBrain`, `InitiativeRoll`); `fWantsToPursue` is gone | ADOPTED 2026-09-20 |
| 10 | Blocked step, player collision (decision 2) | a collision with the player sets the blocked bit like a wall; exceptions: two attacking creatures, doors (7.3) | the bit as in the original: a refused step reaches the brain as a collision with item 0x7F and sets the blocked bit like a wall; `UWCritter.fIsBlockedByPlayer` stays as the pre-check (see its comment, "deviation 10") | PARTLY A DEVIATION until stage 2 of the motion rework (2026-10-05): the player is now a collision record of the motion core at his feet (item 0x7F, `UWProjectileWorld.GetBodiesInTile`), so a creature's step stops at his body as in the original and the brain gets the collision; the pre-check is gone. Pushing the player as the original does is not built - and in the original his motion never reads the pushed record, so creatures cannot shove him there either |
| 11 | Blocked release timing | RNG(8) per update (0.25 s) and the wander step in the same update | every blocked update rolls `fRandom(UWCritterRules.BlockedClearChance)` and runs the wander step in the same update (`UWCritterBrain`); the decision-step gate is gone with row 1 | ADOPTED 2026-09-20 |
| 12 | Wander step while blocked | `NPCWanderUpdate` in full: hostile runs `StandStillGoal` (may re-acquire the target or walk home), restlessness switches at frame 3 only, deflection RNG(0x40) - 0x20, steering away from a player within 10 eighths, quarter turn zeroes the speed for the update (6.3) | `NPCWanderUpdate` in `UWCritterBrain`: the deflection RNG(0x40) - 0x20 (`UWCritterRules`), the quarter turn with zero speed on a bump, vectoring away from a player close by (`fMaybeVectorsToPlayer`), restlessness switched at frame 3; `fWanderWhileBlocked` is gone | ADOPTED 2026-09-20 |
| 13 | Path commit and sliding step | straight tile line first, one path search stored whole in a slot, followed with corner cutting; dropped on any collision; no sliding: a deflected heading is kept and the creature never slides along a wall, an unchanged heading goes blocked (7.1, 7.3) | `UWCritter` applies one displacement of `StepEighths` along the fine heading through the clearance probes, NO SLIDING: a step that cannot be taken whole ends at the last good position and is reported as a collision (see the comment there, "deviation 13"); `PathCommitSeconds` and `fMoveTowards` are gone. `ICritterHost.HeadingDeflected` is never set - the original's reflection of the heading off a wall (`seg030_2B26_A47`) is not built | ADOPTED 2026-09-20 as decided (straight line, path, blocked; no slide). WALL DEFLECTION BUILT 2026-09-28 (per user: Biden wandering diagonally kept walking into the corridor walls): seg030_2B26_A47 read - for a creature the heading becomes the wall's direction nearer to it, unless it hits within 67.5 .. 90 degrees (head-on), and the step goes on (`UWCritterRules.TryDeflectAlongWall`, `UWCritter.fTryDeflectedStep`, HeadingDeflected is now set). SINCE STAGE 2 (2026-10-05) THE ORIGINAL'S OWN: the step runs on `UWMotionCore` (`UWCreatureMotion`), the deflection is `TurnAlongWall` in slide style with the wall octant of the terrain sampling and the sub-tile snap; the path stays the host's (deviation 14) |
| 14 | Path search | box 5 tiles around the segment, depth 32, 64 per wave, 16 shared slots, range budget for drops and lava, closed doors passable for creatures that could open them, hop on climbs (7.4) | the host keeps its own search in place of `PathFindBetweenTiles`: `UWTilePath` breadth-first from the target, next tile only, reused for `PathRefreshSeconds` 0.5 s (`UWCritter`, "deviation 14"); the range budget (`UWCritterRules.GetPathRangeBudget`) and the door rule apply per edge. Since 2026-09-21 the search stays in the original's window, five tiles around start and target (`UWTilePath.SearchMargin`, compiled, untested in game). SINCE 2026-10-05 THE ORIGINAL'S OWN: `UWTileRoute` (the straight line seg006_1477_1938 and PathFindBetweenTiles with its two wave lists, the untested adjacent goal, the goal tested as the middle of the final triple) over `UWTileTraverse` (TraverseMultipleTiles: the B-C edge, closed doors by orientation, heights, drop cost d - 1, the terrain of the middle tile, the two jumps of byte 0x0A bit 5); the host still asks for the next tile per update and caches it half a second instead of storing the path in a slot | ADOPTED 2026-10-05 (the whole chain transcribed after the user saw goblins follow him into the water); what stays ours is the slot bookkeeping (next tile per call, 0.5 s cache) and the hop on climbs (not built) |
| 15 | Doors | hostile creatures use, pick or bash closed doors on 3 of 4 collisions; portcullises never; fliers duck under open doors (7.5) | a closed door is reported to the brain as the object hit; the brain tries it (`DoorTryChance`), picks or bashes it when hostile (`fTryOpenDoor`), and the path search lets a creature with door skill through (`UWCritter`, spec 7.5); `fIsBlockedByDoor` is gone | ADOPTED 2026-09-20 |
| 16 | Speed scale | speed / 20 of the player's full walk per update; speed 0x0C only in goal 5 and goal 6 near, 0x0B everywhere else (7.2) | `UWCritterRules.MomentumPerSpeedPoint` 0x2F: speed s covers s / 20 of the player's full walk per update; `CritterSpeedScale` is gone. The absolute scale is still to be measured (section 12, test 3) | ADOPTED 2026-09-20 |
| 17 | Continuous versus stepped movement | one displacement per update along one of eight headings from the TILE difference; facing turns at most 45 degrees per update; a reversal stops one update (7.1, 7.7) | one displacement per update along the eight-way vector heading, the turn limiter and the reversal stop in the brain; the PICTURE is interpolated between two updates (`UWCritter.Update`, "deviation 17") | ADOPTED 2026-09-20, with the interpolation of the picture as the user decided |
| 18 | Arrival | `NPC_Goto` stops on the target tile with speed 0 and anim 0x20 | `NPC_Goto` in `UWCritterBrain` stops on the target tile (`fOnTargetTile`) with speed 0 and `AnimStanding` 0x20; `fApproach` and `fReturnHome` are gone | ADOPTED 2026-09-20 |
| 19 | Distance cull, Speed enchantment | frozen beyond 10 tiles (goal 3 exempt), re-checked every 0.5 s; the player's Speed halves the world's slot advances (1.1, 1.3) | the brain returns at the distance cull (`Motion.Culled`, goal 3 exempt), and `UWCritterClock` halves the slot advance under the player's Speed and keeps the odd one | ADOPTED 2026-09-20 |
| 20 | Animation clock | the AI update is the frame clock: walk 4 frames/s, stand 1/2 per 0.375 s, combat idle per update in range, death 4 updates (1.5) | the animator has no timer of its own: the brain advances the frame with every update and `UWCritter.fShowMotion` / `UWCritterAnimator.ShowMotion` show exactly that slot and frame ("deviation 20"); `CritterFramesPerSecond` is gone | ADOPTED 2026-09-20 |
| 21 | Slot numbering | 0x07 is "backing off while too close" (heading reversed, picture faces the target); walking is 0x2C in every goal | `UWCritterAnimator` maps the original's slots (see its class comment, "deviation 21"): 0x07 backing off with the pictures facing the target, 0x2C walking in every goal | ADOPTED 2026-09-20 |
| 22 | Hearing and sight comparisons | heard: hear2 / 4 > d2 strict; seen: d2 <= see^2 (3.1) | `UWCritterBrain` (SearchForGoalTarget): heard within hear2 / 4 strict, seen within see^2 with the facing and a clear line - see the self-check "deviation 22" | ADOPTED 2026-09-20 |
| 23 | Line of sight | `TestBetweenPoints` with heights: the interpolated line blocked below the next tile's floor (3.1) | the sight line is engine-free, `UWTilePath.TestBetweenPoints`, called by the host (`UWCritter.HasLineOfSight`, `ICritterHost`): a line in eighths with the height interpolated, blocked by solid tiles, by a floor above the line, and at every tile change by a closed side of the tile it leaves or enters - the original's table `TileTraverseFlags_dseg_5c99_1D8A`, which is what `UWTilePath.AllowsEdge` already held for the path search; doors do not block. Self-check section "Sight line". `HasClearTileLine` is gone | ADOPTED 2026-09-23 (read in TestBetweenPoints_seg006_1477_1BD1 and TestTileTraversal_seg006_1056). CONFIRMED in game the same day against the original: a creature whose line runs into the closed side of a diagonal tile does not see a theft next to it, one with a line past it does - both games alike (Todo.md, row "Theft") |
| 24 | Player noise | base + speed * 10 / forward - 5 while moving, base + 4 in easy move, +4 in water, decays one step per 8th call; equipment reductions for both nibbles (3.2) | `UWPlayerVitals` keeps both nibbles: base plus the speed term while moving, the stealth spells on both bases (2026-09-21), the easy-move base of four (`ReportEasyMovementStep`, 2026-09-21), water on top; `TickQuietness` jumps up at once and decays one step per eight calls with the original's comparisons; `UWCritter.fSnapshotPlayer` hands the nibbles to the brain ("deviation 24") | ADOPTED 2026-09-20 and 2026-09-21; the decay rule checked 2026-09-23 and identical. What stays ours is only the CALL RATE, 12 per second (`QuietnessTicksPerSecond`): the original calls the rule once per frame of `GameObjectLoop_seg034_2F89_518`, and its frame rate depends on the machine, so there is no fixed value to take over (the old "once per 8 player ticks" was a misreading). The combat writers of the noise nibble are still unverified (section 12) |
| 25 | Noticing rules | initiative roll `<=` for the turn and `<` for the search per 0.375 s; bit 1 cleared on a failed turn roll; the walk-and-look destination persists until reached or replaced (3.1) | the initiative rolls `<=` for the turn and `<` for the search in `StandStillGoal` (`UWCritterBrain`); bit 1 as `UWCritterRecord.HeardSomething`, cleared on a failed turn roll; the destination persists in the record; `fTryNoticePlayer`, `mbHasHeardTarget` and the 8 s timer are gone | ADOPTED 2026-09-20 |
| 26 | Re-search gate | 1 in 8 when the stored destination differs in x OR y (6.11) | `fAttackGoalSearchForTarget` in `UWCritterBrain` re-searches 1 in `ReSearchChance` 8 when the stored destination differs in x OR y | ADOPTED 2026-09-20 |
| 27 | Kin alarm | applies to any attitude; sets the walk destination to the victim's tile; allies attack the damaged NPC; excludes byte 0x0A bit 7; window 0x200 PIT ticks = 2 s (4.2) | `UWCritterAlarm` holds the kin alarm of a level with the 0x200 PIT tick window (2 s); the brain sets the victim's tile as destination; `fCheckAlarm` and `AlarmSeconds` are gone | ADOPTED 2026-09-20 |
| 28 | Damage reaction (decision 3) | gate: far and unable to cast back -> bit 5, goal 5 without roll; bit 5 -> 5; bit 4 -> 9; else morale check at any distance for a close hit (4.3) | the damage reaction in `UWCritterBrain` with the gate of label `seg007_1798_2F8C`: far and unable to cast back sets bit 5 (`UWCritterRecord.Relentless`) and goal 5, bit 5 goes to 5, bit 4 (`MadeStand`) to 9, else the morale check; hits by other creatures go through the same path (the attacker's index) | ADOPTED 2026-09-20 |
| 29 | Morale check inputs | table byte 4 (vitality of the kind) | `fMoraleCheck` in `UWCritterBrain` takes the vitality of the kind from the table row (`mORow.Vitality`, byte 4) | ADOPTED 2026-09-20 |
| 30 | Goal 6 | morale roll and cornering only in the close branch (tile^2 <= 3, dz < 16); close branch never strikes; far branch runs; no bursts (6.6) | `fGoal6` in `UWCritterBrain` (NPCGoal6): backs away facing the target when close, runs when far, the stand roll and a close bump lead to goal 9; no bursts, no strike of its own; `fGoalWithdraw` and `CritterWithdrawBurstSeconds` are gone | ADOPTED 2026-09-20 |
| 31 | Goal 9 | hold within tile^2 <= 4, melee below 0x90 without a floor test, else shot, spell or the goal 6 routine (6.7) | `fGoal9` in `UWCritterBrain` (NPCGoal9): strikes below 0x90 without a floor test, holds within two tiles, else shoots, casts or runs the goal 6 routine; `fCanReachInMelee` is gone | ADOPTED 2026-09-20 |
| 32 | Goal 10 (talk) | no attitude check; waits, faces within 0x190, talks within 0x90 when the player looks back (6.8) | `fGoal10` in `UWCritterBrain` (GoalTalkto): no attitude check, faces the player within 0x190, talks within 0x90 when he looks back | ADOPTED 2026-09-20 |
| 33 | Goal 11 | ethereal drift: random heading, speed 0/1, random height, no reactions (6.9) | goal 11 in `UWCritterBrain` (Ethereal): random heading, speed 0 or 1, random height, no reactions; `miStationaryGoals` is gone | ADOPTED 2026-09-20 |
| 34 | Goal 12 | hostile -> goal 4 on the first active update, no search of its own (6.10) | goal 12 in `UWCritterBrain` (NPCGoalC): hostile becomes goal 4 on the first active update, no search of its own | ADOPTED 2026-09-20 |
| 35 | Goal 8/4 home | walk home beyond range^2, no give-up timer; goal 2 has no home (6.1, 6.3) | the brain walks goal 8/4 home beyond the range and has no give-up timer (the blocked bit is the give-up); goal 2 has no home and no radius, as in the original; `fWanderWithin`, `CritterHomeGiveUpSeconds` and `CritterWanderRadius` are gone | ADOPTED 2026-09-20 - for goal 2 as well: the radius was the user's question, and with the brain it simply went, the original has none |
| 36 | Goal 1 | home never changes; 1 -> 8 inside `NPC_Goto` on arrival | goal 1 becomes goal 8 inside `NPC_Goto` on arrival (`UWCritterBrain`), and nothing moves the home any more - the port's "destination becomes the new home" is gone with the old code | ADOPTED 2026-09-20 (the old "keep ours" is obsolete) |
| 37 | Goal 3 | teleport without a walkability test (6.4) | `ICritterHost.Teleport` / `UWCritter`: a solid tile or one outside the map refuses the jump ("deviation 37") | KEPT OURS as decided, reason: safety - a creature inside a wall would be worse than a skipped jump |
| 38 | Flee spell | Cause Fear sets goal 6 without a roll | Cause Fear sets goal 6 without a roll | NONE, matched from the start |
| 39 | Paralyse, Confuse | attitude 1 and goal 7 / 2 written into the object | `UWCritter.Confuse` writes goal 2, attitude 1 and the player as target; `UWCritter.Paralyse` goal 7 and attitude 1, both without a duration ("deviation 39"); since 2026-09-23 after the resistance check the original makes (ScaleDamageAgainstObject, one point of magic damage, `UWCritter.fPassesSpellResistance`), for Cause Fear and Ally too | ADOPTED 2026-09-20, RESISTANCE ADDED 2026-09-23 |
| 40 | Theft | messages by new attitude; owner bit 0x20 and byte 0x0A bit 7 rules; owner cleared only when `owner & 0x1F <= 0x1B`; heights in the line test (4.5) | `Interaction.ReportTheft` and the trespass trap (`UWTrapRules`, `IUWTrapHost.AngerRaceNearPlayer`) go through one routine, `UWTriggerSystem.AngerRaceAround`, as in the original: the area seven tiles back and eight forward (`UWCritterRules.TheftAreaBack`/`Forward` - the area routine includes its far end), `UWCritterRules.MindsTheft` (race = owner & 0x1F, the locked creatures only with the owner's bit 5, owner 0x20 only them, the knights once quest 32 has reached 3), the creature's sight range and line with heights (`UWCritter.CanSeeTheftAt`), then `UWCritter.Anger` one step down but never below zero and the message by the NEW attitude (`TheftMessageBase` 226 + attitude: angered, annoyed, notes); afterwards the owner is cleared only when owner & 0x1F <= 0x1B (`ClearsOwnerAfterTheft`). Self-check section "Theft" | ADOPTED 2026-09-23 (read in AngerNPCByIllegalAction_ovr104_C37, ovr104_E4C and RunCodeOnObjectsInArea_seg038_3307_9C7). Untested in game. The trespass trap is not used up by it: it hands the routine the player object |
| 41 | Fliers | pitch 14 / 15-17 / 18 towards 20 zpos above the destination floor, capped at 0x78; height is a by-product (7.6) | fliers pitch as in the original - `UWCritterRules.FlierPitchLevel` 16 level, the rolls of the wander, melee and goal 6 routines in `UWCritterBrain`; the height is a by-product of the pitch; `FlightMargin` and the random height are gone | ADOPTED 2026-09-20 |
| 42 | Flier and swimmer flags | table byte 0x0A bits 7 and 6 | flier and swimmer from table byte 0x0A bits 7 and 6 (`IsFlier`, `IsSwimmer`; `UWCritter`, "deviation 42"), not from the category byte | ADOPTED 2026-09-20 |
| 43 | Water, lava, shore | no advance refusal: tile tests avoid water, a step ending in deep water drowns; lava blocked at the edge without a path, 1 fire damage per moving update; a swimmer keeps only its collision radius from the bank (7.3, 7.6) | drowning built 2026-09-20: `fCanStandAt` no longer refuses a land creature the water, only a swimmer the dry floor; `fIsDrowningAt` reports the step, `UWCritterRules.Drowns` holds the 0xF8 / 0x10 rule, the one splash 0x1C6 and the water sound, and the ordinary death removal, whose remains the water swallows (`UWCritterRemains`, "deviation 43"); lava damage built; `CritterShoreClearance` 16 stays | BUILT (drowning, lava). SINCE STAGE 2 (2026-10-05) THE ORIGINAL'S OWN: the land callback `seg006_1477_431` transcribed in `UWCreatureMotion` - water alone under a sub-step drowns, water with support stops a creature without a path, a drop or lava edge stops one without a path unless it was already on it; the swimmer's callback collides with plain floor; the shore clearance and `fIsDrowningAt` are gone |
| 44 | Clearance probes | body corners at the COMOBJ radius in the 3 x 3 tiles; creatures drop freely | `UWCritter.fClearanceFailures`: the body corners against wall, drop and diagonal with our clearances | GONE with stage 2 of P6 (2026-10-05): the body corners are the core's own (ProcessMotionTileHeights at the COMOBJ radius) |
| 45 | To-hit details | flanking bonus 0..4 on score and damage; strong individual bonuses; critical hit x1 or x2 with flash and armour damage; blow needs a collision within 5 eighths ahead (8.3) | `UWCritterCombat` (see its class comment, "deviation rows 45-51") in the original's order: score, damage and the strong bonuses, the to-hit through `UWSkillCheck`, `GetCriticalMultiplier` on the base damage, the dice, `ScaleByCharge` with the flank bonus; the host's reach scan for the defender (`UWCritter`); `fCanReachInMelee` is gone | ADOPTED 2026-09-20 and measured against the original the same day |
| 46 | Missile damage | charge 0x80 for every missile hit | a creature's missile rolls with charge 0x80 and flank 0 (`UWCritter`, NPCMissileLaunch, "deviation 46"). The brain computes the original's pitch (`GetPitchToGTarg`, `UWCritterBrain`) and hands it over with `ICritterHost.OnMissileLaunched`, but the host aims itself: straight at the aim point the user observed in the original (a fireball at eye height, an acid slug's missile at chest height), plus the drop of our ballistic flight | DAMAGE ADOPTED 2026-09-20. THE AIM STAYS OURS, researched 2026-09-23: the pitch is a slope in the original's own units (dz * 4 / dist, 16 for 45 degrees), and its gravity term dist * 3 / velocity is calibrated to the ORIGINAL's gravity - taken alone with our speed and gravity it would shoot systematically high or low. It can only be adopted with the projectile flying in the original's units (momentum, the vertical component from the pitch, gravity per update, ApplyProjectileMotion_seg029_29EE_61A); belongs with P6. Also still ours then: the heading (the original flies along the creature's byte 9) and the launch height (zpos + COMOBJ height * 5 / 6 + 2 * pitch) |
| 47 | Difficulty | melee and missile damage to the player halved on easy | `UWCritterCombat.HalveOnEasy` for creature blows and missiles against the player, driven by `UWPlayerVitals.IsEasyDifficulty` (PLAYER.DAT 0xB4, "deviation 47") | ADOPTED 2026-09-20 |
| 48 | Poison | current < byte 0x0F and resistance only | the poison of a hit on the player: after a hit, even one the armour swallowed, the poison nibble below the row's byte 0x0F and a Poison Resistance check only (`UWCritter`, "deviation 48"); the armour hurdle is gone | ADOPTED 2026-09-20 |
| 49 | Creature armour bytes | part p uses row[p % 4], byte 3 == 0xFF falls back to byte 0 | part p uses row[p % 4], byte 3 == 0xFF falls back to byte 0 (`UWObjectClassProperties`), and `UWCritter.GetArmourAgainst` picks the part over the body from the attacker's heights for blows and projectiles alike (`Interaction`, `UWSpellProjectile`, "deviation 49") | ADOPTED 2026-09-20 |
| 50 | Kill experience | 4 * base + 2d(base), strong x (24..47) / 16 | `UWExperience`: 4 * base + 2d(base), strong x (24..47) / 16, straight to the experience - the halving with random rounding of the reference is gone ("deviation 50") | ADOPTED 2026-09-20 |
| 51 | Remains | corpse 0xC0 + n with 7 in 16; blood 0xD8 + n; hit effect by byte 8 bits 3-4 | `UWCritterRemains` leaves the blood 0xD8 + n and the corpse 0xC0 + n with 7 in 16 where the creature stood; the hit effect by byte 8 bits 3-4; the blood object 0x1C0, checked against the disassembly (`UWObjectMechanics.BloodEffectObjectId`, "deviation 51") | ADOPTED 2026-09-20 |
| 52 | Creature versus creature | same attack path, refused only for pairs with the same ally bit | one attack path for all: the brain's damage reaction takes any attacker index, and the ally bit (`UWCritterRecord.IsAlly`) decides who reacts to whom (`UWCritterBrain`); what is missing is the ALLIES themselves - the two spells do not set the bit yet (Todo "Allies") | PARTLY ADOPTED 2026-09-20. ALLIES BUILT 2026-09-23: the Ally spell (UWCritter.Befriend) and the summoning set the bit; targets come from the kin alarm and the damage reaction, as in the original (Todo "Allies") |
| 53 | Music | theme 5 when hp * 64 / (vitality + 1) < 16 else 6; combat theme at swing frame 0 against the player | `UWMusicSelector` for the player's side (hp * 64 / (vitality + 1) against 0x10, theme 6 or 7); the creature's side is the brain's alone (`ICritterHost.PlayMusic`): theme 5 or 6 by the same formula for EVERY damage the player deals, and theme 6 at a swing's frame 0 against the player | ADOPTED 2026-09-23. The rounding was a leftover: `Interaction` called the reference's float version after every melee hit, AFTER the brain, and overwrote its theme; that call and the two dead functions of the reference are gone |
| 54 | Talk goal facing window | relative 3..5 | matches (`UWCritterRules`) | NONE |
| 55 | `GetVectorHeading`, `IsFacing` | unequal sectors, 0/1/7 | matches (`UWCritterRules`: the unequal sectors of GetVectorHeading, IsFacing 0/1/7) | NONE |
| 56 | Attack pick | chance > remaining roll | matches (`UWCritterBrain`) | NONE |
| 57 | Backup goal | saved only from goal 4; any nonzero nibble restored | matches (`UWCritterBrain`, `UWCritterRecord`) | NONE |
| 58 | The player's defence value | row byte 0x12 = Defence skill + half the skill of the weapon in hand (8.8, labels 86A-938) | `UWPlayerCritterRow.GetDefence`, `UWCharacter.DefenceCheck`, used by `UWCritter.fStrikePlayer` | ADOPTED 2026-09-20 |
| 59 | Armour value of a worn piece | `((table protection * quality) >> 6) + 1` per piece, 0 for a weapon (`ovr133_72D`) | `UWPlayerCritterRow.GetArmourValue` per worn piece | ADOPTED 2026-09-20 |
| 60 | Shield | the off hand (7 + handedness bit), ids 0x3B-0x3F only, adds its value to armour parts 0 AND 1 (labels 7EB-867); its enchantment also counts (`ovr120_BA2`) | the shield in the off hand adds to parts 0 and 1, its enchantment counts too (`UWArmourProtection`) | ADOPTED 2026-09-20 |
| 61 | Protection enchantment (class 0xC) | value `(minor & 7) + 1`; bit 3 set makes it ARMOUR, clear PROTECTION; part from `LocationDefenceIndex` for slots 0-4, parts 0 and 1 for rings and shield; a cast spell of the class does nothing | `UWArmourProtection.fApplyProtection` with the original's value, the armour or protection branch by bit 3 and the parts by slot | ADOPTED 2026-09-20 |
| 62 | Resistance spells (class 2) as armour | adds nothing: the nibble is written into bits 4-7 and read from bits 8-11 (8.8) | `UWArmourProtection.ResistBlowsAddsArmour` with `UWSettings.ResistBlowsAddsArmour`, OFF by default ("deviation row 62") | SETTLED 2026-09-20: a switch as the user asked, the faithful state is the default |
| 63 | Goal 3 with target 0 | `GetDistancesToGTarg_seg007_326B` does not check the target: `LookupObjectByIndex(0)` returns a null pointer and the target's hit points and position are read from 0000:0000, the interrupt table (6.4) | a follower without a target follows the player (`UWCritterBrain.fGetDistancesToGTarg`) | ADOPTED FROM OBSERVATION 2026-09-29: the Void's Slasher of Veils has goal 3 and target 0 in the level data and follows the player in the original (per user, 2026-09-14 and 2026-09-29); where the original's read of the null pointer gets the player's position from is not found. Taking target 0 as none made him wander |

The user's in-game observations against the original (Todo.md, section 5) and what the code
says about them:

- Goblin fight saves (2026-09-19): goal 5 both times, goblin at 0.9 and 1.13 tiles. Explained:
  at 1.13 tiles (81 squared eighths) the goblin is walking in; it stands at <= 64 (6.5).
- Between strikes the combat stance shows once, rarely twice; "a moment of deliberation,
  longer or shorter depending on when the tick comes" (2026-09-19). Explained by the geometric
  1-in-4 roll per 0.25 s update with the combat idle frame advancing per update: P(wait >= 4
  updates) = (3/4)^4 = 32 percent, one four-frame pass most of the time (8.2).
- The rat flees only shortly before death, does not go through doors, attacks again within
  reach (2026-09-12). Explained by the morale window (4.4; rat vitality 8, morale 11), the door
  routine (a kind without door skill can only bash with RNG(4) and RNG(byte 0x14) damage), and
  goal 9 or the damage reaction.
- The fleeing rat backs off one to two tiles backwards and slower, then turns and runs faster
  (2026-09-12). Explained by the goal 6 close branch (anim 7, speed (wander + 1) / 2, facing the
  target) and the far branch (anim 0x2C at the pursuit speed within 8 tiles).
- "It backs off a bit further and thinks longer" than the port's first build: the original
  re-evaluates every 0.25 s; the close branch lasts while the player stays within tile^2 <= 3;
  the turn-around costs one stopped update plus the facing catching up at one eighth per
  update (7.7). The port's 2 s bursts are a substitute for this.
- "Backing off is only the first burst; within three tiles it turns to face and stands"
  (2026-09-12) versus "in the original it never came back on its own" (2026-09-16): goal 6 has
  no standing branch; the standing the user saw is goal 9 after the stand roll or a collision
  (holds within tile^2 <= 4, strikes below 0x90). Both observations are consistent with the
  code.
- Lurker "leaves almost at once" (2026-09-16): the damage > vitality / 2 rule with a strong
  character. Lurker at 1.5 to 2 tiles "stood still, came no closer, fled again when I moved
  away" (2026-09-16): goal 9 (hold within tile^2 <= 4, the goal 6 routine beyond). "When it
  flees and you are in melee reach it attacks; if you swim backwards it mostly follows": the
  hit reaction gate (a hit at tile^2 > 2 gives goal 5 without a roll) or goal 9's strike.
- Goal 9 shows the combat stance, goal 6's back-off the walking picture (confirmed
  2026-09-17): anim 0 at 50379, anim 7 at 50640.
- "Our creatures were too hectic, in the original they stop more often" (2026-09-12): the wander
  switches roll only at frame 3, once per second while walking (6.3).
- The wandering rat walks back and forth in front of the player without entering his tile
  (2026-09-12): `MaybeVectorsToPlayer` bends the heading away within 10 eighths,
  `ReactToPlayerPresence` faces a close player, and a bump into the player sets the blocked bit
  and turns a quarter (6.3, 7.3).
- The pushed-away door guard walks home no faster than it wanders (2026-09-12): matches the
  code, `NPC_Goto` uses byte 0x0C only in goal 5 (7.1).
- The goblin with a home behind a closed door drifts around Lanugo (2026-09-12): the blocked
  bit and the wander steps; a hostile goblin would try the door 3 of 4 times, an upset one
  never.
- All wolf spiders came to the reaper fight (2026-09-13): not the kin alarm (different kind)
  and not a clear hearing hit at 4 to 6 tiles; the "unchanged" result of `StandStillGoal`
  (within hear2 * 4) sets bit 1 and in half the cases walks the creature to the player's tile
  (3.1). The player's loudness while striking is the unverified part.
- Sneaking: "you get closer as long as the creature is not looking your way" (2026-09-12): the
  facing test of the sight branch (3.1).
- Bats change height now and then, even in combat (2026-09-12): the pitch rolls in the wander
  step, in `ChooseMeleeAttackToMake` and in goal 6 (7.6).
- Slasher of Veils strikes while following without damage (2026-09-14): goal 3 (6.4).
- Slasher attacks on his own with attitude 2 in the original (2026-09-14): no code path found;
  remains open (section 12).
- "If you step out of melee the opponent deliberates before following, by its decisiveness"
  (2026-09-10): the original has no initiative roll in goal 5; the pause is the running swing
  (up to 1.25 s without goal logic), the reversal stop and the cadence (deviation 9).
- Alarm range = hearing range in Manhattan tiles, pursuing creatures that gave up stay hostile
  (2026-09-12): match (4.2, 5.2).
- "Speed is not a speed but a number of ticks" (old reference reading in Todo): contradicted,
  the speed byte is momentum consumed once per update (7.2).

## 11. Readings that the disassembly settled

Each of these was read two ways while the creature AI was being worked out. What stands here is
what the disassembly says, with the place that decides it.

1. Caster stand-off: the literal is 4 [48879], and `AttackGoalSearchForTarget` uses it in tiles
   [49740-49775]. Four tiles, not eight.
2. Player collision in goal 5: the exception excludes item 0x7F [46880-46925]; the player does
   set the blocked bit [46937]. He is not "nothing" to a creature.
3. Damage reaction gate, label `seg007_1798_2F8C` [53775-53830]: far AND (non-caster OR
   no-magic tile) sets bit 5 and goal 5; otherwise bit 5 -> 5, bit 4 -> 9, else the morale
   check. The full gate, not only the caster on a no-magic tile.
4. Time base: the slot clock decides [114965-114990, 54807]. One slot = 16 PIT ticks, and
   interval 4 is 0.25 s at any frame rate - not "one update per logic frame".
5. Melee cadence: one in four per update, frame 4 on the fifth update. The port's stance gate
   is a deviation, not a reading.
6. Kin alarm window: the game clock advances by the elapsed PIT ticks per frame [label
   `seg034_2F89_4C0`, about 114994], so 0x200 clock units are 2 s, not 0.7 s. The 0x40 per tick
   is the easy-move fixed step only [114747].
7. Distance cull: the interval field has three bits; the code adds 8 to the slot nibble and
   returns [52712-52720]. Slot + 8, not interval 8.
8. Byte 0x0A low nibble: the due slot [53373, 54807], and the cull adds 8 to it. Not a
   far-away counter.
9. Word 0x16: `ApplyProjectileMotion` writes it on every relink [100871, 100886] and the goal 3
   teleport rewrites it [48458-48471]. It holds the current tile; the home post is bytes 4
   and 6.
10. Word 0x0B bits 12-15: one field, the frame nibble. The attack machine counts to 4 and masks
    with 15 [53040-53080]; the goal routines cycle the same nibble with `& 3`.
11. Initiative rolls: `<=` for the turn [48696, jump on greater] and `<` for the search
    [48713]. Two different comparisons, not one.
12. Turn limiter on a deflected heading: the branch at 52085 (label `seg007_1798_2116`) only
    restores byte 9 to the post-physics heading. Speed 0 is written solely in the reversal
    branch (label `seg007_1798_2108`) for a wanted turn of 0x40..0xC0, so a deflection leaves
    the speed untouched.
13. Reversal threshold: the branch stops at exactly 0x40 as well [52130-52150]. Ninety degrees
    or more, not more than ninety.
14. Corpse: `DropNPCRemains` rolls RNG(16) < 7 [41019-41024]. Seven in sixteen, not whenever
    the type is nonzero.
15. Melee then walk: `NPC_Goto` writes anim 0x2C unconditionally at its end [label
    `seg006_1477_2FB7`, 47293-47297] and has no attack guard at its head [46652-46800]; goal 5
    continues into `AttackGoalSearchForTarget` after `ChooseMelee` [49000-49005, label 9CF]. So
    a swing rolled at 65..100 squared eighths is overwritten, and the same happens to spell 3
    started outside the stand range. Goal 9 returns right after both [50318-50338].
16. `AttackGoalSearchForTarget` range test: the compare is strict - d2 > range^2 * 64 moves,
    equality stands [49755-49775].
17. Who reads the charge nibble: `NPCInitialProcessing` at frame 4 indexes seg060 with it
    [53055-53070] and clears it.
18. Restlessness: the walking switch is RNG(16) at frame 3 [47933]; RNG(128) against
    restlessness is the idle heading deflection [48085-48125]. Two different rolls.
19. Byte 0x19 bit 1: set on search result 2 [48760, 49690], cleared on a failed initiative roll
    [48700]. It means "heard something", not "lost target".
20. Goal 6 morale roll: the original rolls only in the close branch [50540-50590]; the port
    rolls at any distance. Listed once as deviation 30.

## 12. Open questions for in-game measurement (DOSBox, SAVE4 goblin fight)

1. Melee start range. Stand exactly 9 eighths (1.13 tiles) from the goblin without moving: the
   code says it walks in (anim 0x2C) and never swings from there; at 8 eighths or less it
   stands and swings. Test: from the loaded fight step back a quarter tile at a time and note
   the distance at which the swing animation stops appearing; expected boundary one tile
   centre to centre.
2. Blow cadence. With the goblin in reach and the player not moving, time 20 blows: expected
   mean 2.0 s between blows, minimum 1.5 s, occasionally 3 s or more, never a fixed interval.
   Count the stance pictures between blows: mostly one four-frame pass, sometimes two.
3. Absolute walking speed. Let a speed-8 goblin (pursuit) chase the player down a straight
   corridor of known length while the player walks at full pace: expected creature 0.4 of the
   player's pace (8 / 20), about 1.5 tiles/s if the player does 3.7 tiles/s. Measure the
   player's own full-walk speed first (tiles per second over 20 tiles).
4. Turn limiter. Circle the goblin at melee range: its picture should turn at most one eighth
   per quarter second (a half turn takes one second), and a creature walking away that must
   turn around should stop for a quarter second before walking back.
5. Deflection versus blocked. Walk the goblin diagonally into a wall (pursuing across a
   corridor corner): does it slide along the wall keeping the deflected heading (no blocked
   bit) or stop and wander (blocked bit)? Count how often each happens; the physics deflection
   branch was read in the reference only.
6. Charge. After a long wait in reach (creature standing, no swing for 3 s or more) the next
   blow should hit noticeably harder (charge up to 255 / 128) than a blow right after the
   previous one (50 / 128). Compare damage numbers with F1 over ten blows each.
7. Third spell and non-casters. A creature with spell power but no caster bit (check
   OBJECTS.DAT for kinds with byte 0x2D bit 0 clear and bits 1-7 nonzero): does it ever cast
   its third spell while pursuing (goal 5)? The code says only within one tile or in goals 6
   and 9.
8. Caster standoff. An imp or mage without line of sight: does it stop about four tiles from
   the player and wait, or walk into melee? With sight within 8 tiles: does it stand still every
   time it does not cast?
9. Player loudness while fighting. Stand still and swing at the air with wolf spiders 4 to 6
   tiles away: do they come? The combat writers of the noise nibble were not read; the Todo
   quotes the reference's 10 (wind-up) and 15 (strike).
10. Slasher of Veils with attitude 2: walk into range without any strike; if he attacks, a
    code path is missing (no special case for item 0x7C, whoami 248 or level 9 was found).
11. PIT rate. Not measurable in game directly; the slot clock can be checked by counting the
    walk cycle of a goblin: four frames per second at interval 4 if the PIT is 256 Hz.
12. Drowning. Push a land creature (rat) into deep water with the player: the code says it
    drowns with a splash and the death animation; confirm before adopting deviation 43.
13. Paralysed creature (goal 7): does it ever wake without being hit or spoken to? No timer was
    found.
14. Door bashing. A hostile goblin (door skill unknown) behind a closed door: does it open,
    pick or bash it within a minute? Expected: with skill it opens at once; without, RNG(4)
    bashes with tiny damage.
15. Physics collision branch. The reading of the wall deflection versus the blocked bit (7.3,
    7.7, deviation 13) rests on `motion_collisions.cs` of the reference, which its own PR 118
    (2026, read in the re-check of the reference, 2026-09-19) shows to be a UW2 transcription with UW2
    addresses. Before the deflection is built, read UW1's `seg030_2B26_A47`,
    `ProcessMotionTileHeights_seg026_379` and `GetCollisionHeightState_seg030_2B26_1259`
    directly; test 5 above is the in-game counterpart.
