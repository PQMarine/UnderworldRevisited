# The contract between the creature brain and its host

2026-09-20. The creature AI of `Docs/AI/creature-ai.md` lives engine-free in
`Assets/UWDataImport/UWData/` (namespace `UWDataImport.UWData`, compiled by
`Core/UWDataImport.csproj` and by Unity as the UWDataImport assembly):

| File | What |
|---|---|
| `UWCritterRecord.cs` | The per-creature state as properties over the 27-byte mobile record of a `UWNpc` (the 19 extra bytes in `RawNpcBytes`, word 2 in the common fields). Writes through, keeps the duplicated `UWNpc` fields in step. |
| `ICritterHost.cs` | What the brain asks of the world and the events it raises; the snapshots `CritterTarget` and `StepResult`. |
| `UWCritterClock.cs` | The shared 16-slot clock: `Advance`, `IsDue`, the reschedule and the cull arithmetic. |
| `UWCritterAlarm.cs` | The kin alarm globals of one level. |
| `UWCritterBrain.cs` | `Update(host)` in the original's order, the goals 0-12, `NPC_Goto`, the wander step, perception, combat choice, the turn limiter, `OnDamaged`. Returns a `Motion`. |
| `UWCritterRules.cs` | Every constant of spec section 9 plus the heading helpers, the flank bonus, the morale check and the path range budget. |
| `UWObjectClassProperties.Critter` | Now carries the raw 48-byte `Row` with named accessors for the bytes the AI reads (flier, swimmer, door skill, ranged type, footstep category, loudness as a target, ...). |
| `UWLevelWriter` | No longer copies the load-time `NPCHunger` over byte 0x19 bits 0-5; the AI flags survive a save. |

The Unity host (`UWCritter.cs`, `UWCritterAnimator.cs`) is built around this API. This document
is the contract between the two halves: what the brain owns, what the host owns, and what they
say to each other.

## 1. The loop the host runs

Per level one `UWCritterClock` and one `UWCritterAlarm`; per creature one `UWCritterRecord`
(over its `UWNpc`), its `Critter` table row, and one `UWCritterBrain(record, row, objectIndex)`.
Since stage 2 of the motion rework (2026-10-05) the physics is the engine-free
`UWCreatureMotion` on the level's `IUWMotionWorld` (`UWProjectileWorld`), the same core the
missiles fly on.

Per frame:

    units = clock.Advance(elapsedPitTicks, playerHasSpeedEnchantment, timeIsFrozen)   ; 256 Hz ticks, about 3.9 ms each
    if units == 0: return                                 ; Freeze Time consumes the units and hands out none
    for each creature:
        while clock.IsDue(record.DueSlot):
            motion = brain.Update(host)                   ; cull, path bookkeeping, host.RunMotion(), verdict, mind
            if motion.Removed: drop the creature; break
            show motion.Animation / Frame
    projectiles.RunDue(clock)

`Update` runs the physics itself through `host.RunMotion()` at the point the original runs it
(NPCInitialProcessing 52702-52990: after the distance cull and the path bookkeeping, before
the step's verdict): the body moves with the values the last goal routine left in the record,
then the mind decides anew (spec 1.4, step 6 before step 9). A creature that was not due does
nothing; between two due updates the host may interpolate the picture (section 3), but no
decision, frame or step happens outside `Update`.

Elapsed PIT ticks: the host converts its frame time with 256 ticks per second. `Advance` clamps
to 64 ticks (4 units) per frame, halves the units under Speed and keeps the odd one, and
advances `Clock`, the PIT tick total the kin alarm compares against. Under Freeze Time the
units are consumed and dropped, so the phase stands still and everything resumes in step
(115082). The easy move's step or turn gives the world a fixed 64 ticks on top of the real
time (`AdvanceFixedStep`, seg034_2F89_334), halved under Speed as well.

`Culled` motions (creature beyond 10 tiles from the player and from the view, goal 3 exempt)
carry nothing but the new `DueSlot`; no physics runs for them.

## 2. ICritterHost

All positions in eighths of a tile (`tile * 8 + fine`, fine 0..7), heights in zpos (7 bits,
8 per floor level), tiles 0..63, facing eighths 0..7 and fine headings 0..255 (0 = +Y north,
clockwise, 64 = +X east), time in PIT ticks. No floats cross the boundary.

| Member | The host delivers |
|---|---|
| `long Clock` | `UWCritterClock.Clock` of the level, PIT ticks. |
| `int Random(int n)` | A uniform 0..n-1, the original's `RNG_seg005_DE7` divided by n. Must be a real RNG in the game; the self-check scripts it. The brain calls it in the original's order, so a scripted stream reproduces a decision exactly. |
| `UWCritterAlarm Alarm` | The level's kin alarm object. Reset it on a level change (`Reset()`); it is saved with the game in the original (PLAYER.DAT 0xBA-0xC1), which the port may add later. |
| `int FloorLevelAt(x, y)` | The tile's floor nibble (the tile word's bits 4-7; `UWTile.FloorHeight >> 4` in the port's units), 0 off the map. |
| `bool IsMagicBlockedAt(x, y)` | `UWTile.NoMagicAllowed`. |
| `bool TybalOrbStands` | True only on level index 7 while PlayerData 0x60 bit 5 is clear. |
| `int OwnObjectHeight` | COMOBJ byte 0 of this creature's kind (the object height), added to zpos for the eye of the sight line. |
| `CritterTarget GetObject(index)` | A snapshot of any mobile object by index (1 = the player); `Exists` false for none. Filled from the object's record and its kind's row; for the player: `Loudness` = `UWPlayerVitals.Quietness`, `Visibility` = the player's visibility nibble, `WeaponDrawn`, `HitPoints` nonzero while alive, `ObjectHeight` the player's COMOBJ height. |
| `bool HasLineOfSight(x0, y0, z0, x1, y1, z1)` | `TestBetweenPoints` semantics: a line in eighths with the height interpolated, blocked by the wall bits of the tiles crossed and where the height falls below the next tile's floor; doors do not block. The heights already include the object heights. `UWTilePath.HasClearTileLine` is the current approximation without heights (deviation 23, to be completed). |
| `bool IsStraightLineClear(toX, toY)` | The straight tile line from the creature's tile to the destination is walkable for THIS creature (walker, flier or swimmer, water, lava with fire resistance, closed doors it could open). The original's `seg006_1477_1938`. |
| `bool TryGetNextPathTile(toX, toY, rangeBudget, mayOpenDoors, out nextX, out nextY)` | The host's `UWTilePath` search from the creature's tile: the next tile towards the destination, false when there is none. `rangeBudget` is the original's path range (spent as costs on drops of more than one level and on lava, spec 7.4); `mayOpenDoors` says a closed door counts as passable. The host may cache a path and re-run it on its own schedule (deviation 14). |
| `StepResult RunMotion()` | The physics of this update: the step of the previous decision on `UWCreatureMotion` (NeedsToMove, the params from the record, the pre-state, the core with the kind's callback, the write-back into the record), the tile list relinked, the lava burn applied to the body, the body moved to the record. Returns what the step left behind (section 4). Called by the brain after the cull and the path bookkeeping. |
| `bool Teleport(x, y)` | Goal 3: put the body on that tile (centre, floor height) and relink it; the host's walkability check may refuse (deviation 37). On true the brain writes tile, fine position 4/4 and zpos into the record. |
| `bool UseDoor(doorIndex)` | `ObjectUse` on the door; true when it is open afterwards. |
| `void PickDoor(doorIndex, skill)` | A lockpick attempt with minus the skill (`UnlockDoor_seg040_352B_1D3B`). |
| `void BashDoor(doorIndex, damage)` | `DamageObject` type 4 on the door. |
| `int MissileVelocity(ammoIndex)` | Byte 1 of entry n of the 16 x 3 ammunition table at OBJECTS.DAT 0x82 (`UWObjectProperties`' ranged speed). |

The brain's own record supplies everything else about itself; the host must keep the record's
position fields current (section 6).

## 3. The Motion output and the physics

`UWCritterBrain.Motion` (immutable, the same values stand in the record):

| Field | Meaning |
|---|---|
| `Moves` | False when standing with speed 0 and level pitch: no physics this interval (the step tests the record itself). |
| `FineHeading` | Byte 9, the direction of the step. |
| `FacingEighth` | The picture's direction (word 2 bits 7-9), already limited to 45 degrees per update. |
| `Speed` | Byte 0x13 bits 0-6. |
| `StepSubUnits` | `speed * 0x2F * interval * 16`: the momentum consumed over the interval, in the stepper's sub-units of which 0x2000 make one eighth. Informational since stage 2: the core computes the step from the record. |
| `Pitch` | Byte 0x14 bits 3-7, 16 level; a flier's vertical component is (pitch - 16) * 64 in the same sub-units per tick. |
| `Gravity` | Byte 0x13 bit 7, the physics' own flag. |
| `Animation`, `Frame` | Byte 0x15 bits 0-5 and word 0x0B bits 12-15: what the animator shows until the next update. |
| `Interval` | Slots until the next update (1, 4, 6 or 7). |
| `DueSlot` | The rescheduled slot. |
| `Culled`, `Removed` | See section 1. |

The physics (`UWCreatureMotion.Step`, the original's motion part of NPCInitialProcessing):

1. Nothing when the creature stands with speed 0 and level pitch.
2. The params block of the kind (land, flier, swimmer) from the record: the position is the
   record's eighth and height with a random spot inside (`InitMotionParams`), the momentum
   speed * 0x2F over interval * 16 ticks, step height 8, slide style.
3. The pre-state: the terrain flags of the 3x3 tiles around the record position
   (`seg006_1477_367`), which the land callback holds the drop and lava flags against.
4. `UWMotionCore.CalculateMotion` with the kind's handler and callback: walls deflect the
   heading along them (`TurnAlongWall`, head-on stops), objects, doors, the player and other
   creatures are collision records, water, lava and drops are the callback's (section 4),
   ledges are the step rule of `GetCollisionHeightState`, a creature without ground falls.
5. The write-back: eighth, height, tile (relinked by the host), heading byte, gravity bit,
   pitch field, speed byte, contact state. No fine position for a creature.
6. The host moves the body to the record and lets the picture glide there over the interval
   that has just elapsed (deviation 17, the user's decision to interpolate).

The picture: the animator gets `Animation` and `Frame` from the Motion and shows exactly that
segment and frame, with no timer of its own (deviation 20). The facing for the eight-way
pictures is `FacingEighth`.

## 4. StepResult

Filled by `UWCreatureMotion.Step` from the callbacks of the original (`seg006_1477_431` land,
`seg006_1477_5FE` flier, `seg006_1477_65E` swimmer):

| Field | The original's global | Set when |
|---|---|---|
| `Collided` | `RelatedToMotorCollision_2452` | Any collision stopped or deflected the step (wall, object, door, a water, lava or drop edge, airborne). |
| `HitObject`, `HitObjectIndex`, `HitObjectItemId`, `HitObjectIsCreature` | `dseg_246D`, `CollisionObject_2442` | The first overlapping collision record (`seg030_2B26_17F5`): another creature, the player (item 0x7F), a door, a force field, a lying thing with height. |
| `HitClosedDoor` | `RelatedToColliding_2473` | A closed door (class 0x14, index below 8) was among the overlapping records (`seg030_2B26_170C`; land creatures only, the other callbacks have no door search). |
| `HeadingDeflected` | `HasCurrObjHeadingChanged_2449` | Byte 9 after the step differs from before (the wall deflection). The brain keeps that heading and never sets the blocked bit on it. |
| `Stuck` | `IsNPCActive := 0` | Airborne (0x1000: gravity on, interval 1, the creature falls) or drowned. |
| `Drowned` | the splash path 41420 | A land creature's sub-step ended with water alone under it (0x10 of the 0xF8 bits): the brain sets animation 0x0C frame 3, interval 1, hp 0 and raises `OnDrowned`; the body is removed at the frame-3 pass. Water with something carrying the creature stops it when it holds no path. |
| `TouchedCeiling` | `dseg_2462` | A flier met the step flag 0x100 (a raised floor ahead); its vertical speed is pushed up (0x80) and the brain turns the pitch. |
| `TileChanged`, `OldTileX`, `OldTileY` | the relink of `ApplyProjectileMotion` | The step left the tile; the host relinks the object's tile list. |
| `LavaBurn` | the lava roll of `ApplyProjectileMotion` | 1 in 5 per moving update on lava: the host applies 1 point of plain fire (type 8) to the body, charged to nobody. |
| `ImpactDamage` | the impact branch of `ApplyProjectileMotion` | Impact above 0x100; for a creature the original overwrites the hit points right after, so nothing lasts - reported for the trace only. |

A drop (0x800) or lava (0x20) edge stops a creature that holds no path and that was not
already on such an edge before the step (the pre-state); with a path it walks on, over the
ledge and falls, or onto the lava and burns. The lava-proof quirk: a lava-proof kind (COMOBJ
byte 8 bit 3) modifies its kind's static handler for good (`UWCreatureMotion.LavaFlagDropped`),
as the original does.

## 5. Events (brain to host)

| Event | When | The host does |
|---|---|---|
| `OnSwingStarted(attack)` | Animation 1..3 passed frame 0, the update after the decision. A swing rolled at 65..100 squared eighths and overwritten by the walk in the same update never gets here. | Optional: sounds, traces. |
| `OnBlow(target, attack, charge, swingType, flank)` | Frame 4 of the swing. `charge` is the table value 50..255 (over 128), `swingType` RNG(9), `flank` 0..4 from the two facings. | `NPCExecuteAttack` of spec 8.3: reach scan 5 eighths ahead with radius 3 at the swing type's height, refuse creature against creature with the same ally bit (spec 8.3, deviation 52), skill check against the defender's defence with the strong bonuses, dice, charge / 128, flank, armour by part, easy difficulty halving, `DamageObject` into the defender's `OnDamaged` (a miss with 0 damage still records the attacker), poison, blood, shake. |
| `OnMissileLaunched(ammoIndex, pitch)` | Frame 4 of animation 5. | `NPCMissileLaunch`: object 0x10 + ammo at the launcher, heading = the record's byte 9, launch height zpos + COMOBJ height * 5 / 6 + 2 * pitch, byte 0x12 = launcher; damage from the ammo table with charge 0x80 and flank 0 (deviation 46). |
| `OnSpellCast(spellIndex, pitch)` | Frame 4 of animation 0x0D. | `SpellTrapWandCast` with the runic spell index. |
| `OnTalk()` | Goal 10, the player looks back within 0x90. | `TalkTo` the creature's conversation. |
| `PlaySound(n)` | Footsteps (1, 2, 0x17, 5, 0x0E, 0x0D) on odd walking frames by category; death sound 6. | `LoadSoundAtCoordinate` at the creature. |
| `PlayMusic(theme)` | 6 at swing frame 0 against the player (only if the current theme is not already 5..7); 5 or 6 after a blow by the player. | `ChangeThemeMusic` and reset the combat music timer (spec 8.7 area, `CombatMusicTimer_2482`). |
| `OnDrowned()` | See `Drowned`. | The splash (class 7 object 6). |
| `OnDeathStarted(byPlayer)` | `DamageNPC` killed it: animation 0x0C frame 0, interval 4. | `AwardKillEXP` when by the player (4 * base + 2d(base), strong x (24..47) / 16, deviation 50), the dragon nod. |
| `VetoDeath()` (query) | A creature with a whoami is about to die. | `SpecialDeathCases(obj, 0)`; true keeps it alive at 0 hp. |
| `OnDeathFinished()` | Frame 3 of animation 0x0C. | `SpecialDeathCases(obj, 1)`, unlink, loot, blood object 0xD8 + n, corpse 0xC0 + n with 7 in 16 (deviation 51), spill the inventory, free the object. |
| `OnGoalChanged(old, new, gtarg)` | Every `SetNewGoalAndGtarg`, `ResetGoalAndGtarg` and reaction. | Traces only. |

Incoming: `brain.OnDamaged(host, attackerIndex, damage)` is `DamageNPC` (spec 4.3, 8.7): the
host calls it for every hit and miss on the creature with the attacker's index (1 the player,
a projectile's launcher, 0 none). It updates bytes 0x11, 0x12 and 8, the kin alarm, starts the
death, plays the music. The mind reacts at its next update.

## 6. Record fields the brain owns

The host must not write these; it reads them through `UWCritterRecord` and saves them
through `UWLevelWriter` as before (the writer takes `RawNpcBytes` as its base, so every bit
below is saved; `NPCGoal`, `NPCGTarg`, `NPCLevel`, `NPCAttitude`, `HitPoints`, `NPCXHome`,
`NPCYHome`, `NPCHeading`, `NPCIsAlly` are kept in step by the record's setters):

- byte 9 `FineHeading` (the physics may write it back after a deflection, and only then),
- byte 0x0A bits 0-3 `DueSlot`,
- word 0x0B `Goal`, `GTarg`, `Frame`,
- word 0x0D bits 0-3 `BackupGoal`, bits 4-7 `DestinationHeight`, bits 14-15 `Attitude`
  (conversations, spells and the level change may still write the attitude and the goal
  through the same properties, as the original's scripts do),
- word 0x0F `DestinationX`, `DestinationY`, `ChargeIndex`,
- bytes 0x11 `DamageTaken`, 0x12 `Attacker` (via `OnDamaged` only),
- byte 0x13 bits 0-6 `Speed`, byte 0x14 `Interval` and `Pitch`,
- byte 0x15 `Animation`, `IsStanding`, `HasPath`,
- byte 0x18 `FacingResidual`, `NewDestination`, `Blocked`, `StraightLineKnown`,
- byte 0x19 bits 0-5 `TargetConfirmed`, `HeardSomething`, `SpellSlot`, `MadeStand`,
  `Relentless` (bit 6 `IsAlly` and bit 7 hunger stay the scripts'),
- word 2 bits 7-9 `FacingEighth` (the picture's direction, limited by the turn limiter).

The host owns and must keep current after every step: word 2 `ZPos`, `FineX`, `FineY`; word
0x16 `TileX`, `TileY` (the current tile, rewritten on every relink); byte 0x13 bit 7
`Gravity`; byte 0x0A bits 4-6 `TileState`. Byte 8 `HitPoints` changes only through
`OnDamaged` and healing scripts.

`UWNpc.NPCHunger` is a load-time snapshot of byte 0x19 bits 0-6 under a wrong name; nothing
writes it back any more (`UWConversationTrade` reads its bit 6 as the ally bit).

## 7. What in UWCritter.cs becomes obsolete, and what stays

Obsolete (the brain does it; remove with the fields and timers they use):

- The decision clock and its gates: `fIsDecisionStep`, `fIsAttackAttemptStep`,
  `fStartAttackCooldown`, `mfNextAttackTime`, `fGetSwingSeconds`, `fGetStanceSeconds`, the
  `StateEnum` machine as a source of truth (Pursuing, Attacking, Ready, Wandering map onto
  `Motion.Animation` now).
- Perception and temper: `fSearchForPlayer`, `fTryNoticePlayer`, `fGoTowardsHeardTarget`,
  `fPlayerQuietness`, `fPlayerVisibility` (the host still fills `CritterTarget.Loudness` and
  `Visibility` for `GetObject(1)`), `fHasTileLineToPlayer`, `fCheckAlarm`, `ResetAlarm` and
  the static alarm fields (`UWCritterAlarm` on the host replaces them), `fShouldWithdraw`,
  `fOnDamaged`'s decision part (it becomes `brain.OnDamaged` plus the host's damage
  bookkeeping), `Anger` keeps only the attitude write through the record, `mbAggressive`,
  `miAttitude`, `miBackupGoal`, `miAccumulatedDamage`, `miLastHealth`, `mbPursuing`,
  `mOLastKnownPosition`, `mbHasLastKnown`, `mbHasHeardTarget` and `HeardTargetSeconds`.
- The goals: `fGoalStandStill`, `fGoalStandAtLocation`, `fGoalWanderHome`, `fGoalGoto`,
  `fGoalFollow` (the teleport stays as `Teleport`), `fJumpTowardsTarget`, `fGoalTalk`,
  `fGoalAttack`, `fGoalWithdraw` and the burst fields (`mOWithdrawDirection`,
  `mfNextWithdrawHeadingTime`, `mbWithdrawBacking`, `mbWithdrawBackPending`),
  `fGoalDistanceAttack`, `fWantsToPursue`, `fSetGoal`, `fResetGoal`, `fRememberSearchTile`,
  `mOSearchTile`, `miStationaryGoals`, `fIsStationary`, `fIdle`.
- Movement decisions: `fWander`, `fWanderWithin` (goal 2's radius and `CritterWanderRadius`
  go; the original has none), `fWanderWhileBlocked`, `fApproach` with `PathCommitSeconds`,
  `fMoveAlongPath`'s re-run timer, `fReturnHome` and `mfReturnDelaySeconds` /
  `mfReturnAllowedTime`, `fPickFreeHeading`, `fPickWalkableHeading`, `fIsHeadingFree`,
  `fFollowCorridor`, `fQuantizeHeading`, `fRandomDeflection`, `fGetWanderPace`,
  `fQuarterTurn`, `fIsStuck`, `fResetProgress`, `fFaceTowards` (the facing comes from the
  Motion), `fPickFlightHeight`, `fUpdateFlightHeight` (fliers follow the pitch).
- Combat decisions: `fAttack`'s decision part, `fTryDistanceAttack`, `fTryRangedAttack`,
  `fTryMagicAttack`, `fBeginMagicAttack`, `fPickAttack`, `fCanReachInMelee` (the reach scan of
  `OnBlow` replaces it), `fTryPoisonPlayer`'s extra armour hurdle (deviation 48),
  `Confuse`, `Paralyse`, `Flee` keep only the record writes (goal 2 / 7 / 6 and attitude 1
  through `UWCritterRecord`).
- `GetFacingDegrees`, `SetFacingDegrees`, `FacingEighth`, `fFullHeading` become
  `record.FacingEighth * 45` and the record's setters.

Stays as host duties (the physics substitute and the engine work):

- `Initialise`, `RefreshFromData`, `Awake`, `OnDestroy`, `fEnsurePlayer`, the `UWNpc` and
  table row lookup (build the record, the row and the brain here).
- The step: since stage 2 of the motion rework (2026-10-05) `RunMotion` on
  `UWCreatureMotion` and the body placed from the record (`fPlaceBodyFromRecord`,
  `fBodyFromRecord`, `fRelocate`); the probes of the substitute (`fCanStandAt`,
  `fProbeStandingHeight`, `fClearanceFailures`, the diagonal, shore and drop probes,
  `fIsBlockedByPlayer`, `fBlockingCreature`, `fIsBlockedByForceField`, `fBlockingDoor`,
  `fTryDeflectedStep`) are gone. Stay: `fIsWater`, `fIsLava`, `fFindClosedDoor` and
  `CanEnterTile` for the path search, `fGetOwnSize`, `fGetTileAt`, `fTryGetTile`,
  `fGetSwimmerOffset`, `fGetFloorHeight`, `fHeadingToDirection`, `Push` (the record write of
  the momentum transfer), `fTrace`, `GetTrace`.
- `fHasLineOfSight` becomes `HasLineOfSight` with the heights of `TestBetweenPoints`
  (deviation 23); `fGetEyePosition`, `fIsPlayerCollider` serve it.
- `fMoveAlongPath` becomes `TryGetNextPathTile` over `UWTilePath` with the range budget and
  the door rule as costs (deviation 14).
- `CanSeeTheftAt` (theft notice, spec 4.5) stays on the host, using `HasLineOfSight`.
- The blow: `fAttack`'s execution part, `fShowBlowOnGeometry`, `fGetAttackDamage`,
  `fGetAttackToHit`, `fTryPoisonPlayer` (without the extra hurdle), `DescribeCombatStats`,
  `fOnKilled`, `OnMissedByPlayer` (becomes `OnDamaged(host, 1, 0)`), `fCastSpell`,
  `fLaunchProjectile`, `fGetProjectileDamage`, `fIsWeightless`, `fGetAimPoint`.
- `fGetPlayerFacingEighth`, `fGetEighthsToPlayer`, `fUpdatePlayerVector` become the filling
  of `CritterTarget` for `GetObject(1)`.

`UWCritterAnimator.cs`: `Update`'s own frame timer, `GetAttackSeconds`,
`GetCombatIdleSeconds`, `GetSlotSeconds`, `fChooseSlot`'s mapping from `UWCritter.State` and
`fGetAttackSlot` go; the animator shows `Motion.Animation` at `Motion.Frame` for
`Motion.FacingEighth` and nothing else. The slot numbering follows the original (deviation 21):
0x20 standing and 0x2C walking are the direction sets (the animator maps them to its
per-direction pages), 0x07 is backing off with the picture facing the target, 0 the combat
idle.

`UWSettings.cs` fields that go: `CritterSpeedScale` (the scale is `StepEighths`, deviation
16), `CritterFramesPerSecond`, `CritterAttackRange`, `CritterAttackInterval`,
`CritterDecisionInterval`, `CritterWithdrawBurstSeconds`, `CritterAttackAttemptInterval`,
`CritterDistanceAttackInterval`, `CritterWanderRadius`, `CritterWanderPace`,
`CritterHomeGiveUpSeconds`. Stay: the clearances (`CritterWallClearance`,
`CritterDropClearance`, `CritterDiagonalClearance`, `CritterShoreClearance`,
`CritterMaxStepHeight`, `CritterRadiusScale`, `CritterMinimumColliderHeight`), the sprite
scale, the trace switches.

## 8. Where this implementation departs from the spec, and why

- Path following (7.4): the host's search gives the next tile per call; the brain aims at it
  with the original's aim points (tile * 8 + 4 / + 7 / + 0) and heading. The slot bookkeeping,
  corner cutting two eighths before the edge, the stored path and the climb hop (speed 11,
  pitch 22, interval 1) are not built: the path stays the host's (deviation 14) and the hop is
  a physics feature; the constants are in `UWCritterRules` for when the host wants them.
  Whether a closed door counts as passable on a path is approximated by "door skill nonzero"
  where the original asks `CharacterDoorLockAndKeyInteraction`.
- The fine turn for shots and spells (`seg007_1798_2123`) uses `Math.Atan2` in place of the
  original's table arctangent; the tolerance (snap within 0x20, else 0x20 per update) is the
  original's.
- The mutual recursion of `NPCWanderUpdate` and `StandStillGoal` (a hostile wanderer runs the
  stand routine on RNG(2) == 1, whose pose for goal 2 is the wander routine) is the
  original's and is kept, with a depth guard of 8 that only a scripted RNG can reach.
- Drowning: the code order (callback sets frame 3, then the animation machine of the same
  update sees it) removes the body in the same update; the spec's prose says the next. The
  brain marks the record and raises `OnDrowned` in the update the host reports it, and the
  frame-3 removal follows in that update's machine pass unless the goal is 3 or 11.
- The creature-versus-creature refusal (same ally bit, spec 8.3) lives in the host's `OnBlow`,
  as the whole to-hit path does. The brain treats every target index alike (deviation 52 as
  decided).
- The distance cull compares against the player object's tile only; the original also reads
  the `PlayerTileX/Y` globals, which are the same tile.
- The player's loudness writer stays in `UWPlayerVitals` (deviation 24); the brain reads the
  nibble from the snapshot. What it lacks is listed in the TODO at
  `UWCritterBrain.fSearchForTarget`: the speed term, the easy-move base + 4, the equipment
  reductions of both nibbles, the visibility nibble 15 - Sneak / 5, and the decay clock (once
  per 8 player ticks, not a 12 Hz tick).
- `Random(n)` is called exactly where the original calls RNG; the physics' own RNG calls
  (the nudge of RNG(32), RNG(32), RNG(8) in `InitMotionParams`) are the host's and do not
  pass through the brain.
