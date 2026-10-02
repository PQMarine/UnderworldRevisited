namespace UWDataImport.UWData
{
	/// <summary>
	/// What a creature decides on the tile grid, engine-free (P4 of the engine separation,
	/// 2026-09-18). The decisions of a creature are made on the map, only the movement stays in
	/// floats (Todo section 11.3, decided 2026-09-16).
	///
	/// Here: every constant of the creature AI of UW.EXE (Docs/AI/creature-ai.md section
	/// 9, one member per row, 2026-09-20), the eight-way heading the original quantises a
	/// vector into, and the two questions asked with it - does the creature look at the player
	/// (noticing him), and does the player look at the creature (being addressed by it). The
	/// brain that uses them is UWCritterBrain, the record they act on UWCritterRecord.
	///
	/// Units: "eighths" are eighths of a tile (a tile is 8 x 8 of them), "eighths^2" a squared
	/// distance in them, "tiles^2" a squared distance in whole tiles, "heading units" the 256
	/// steps of the fine heading, "slots" the 16 slots of the shared clock (UWCritterClock),
	/// "PIT ticks" the 256 Hz timer of the original, "zpos" the 7-bit height.
	/// </summary>
	public static class UWCritterRules
	{
		// ------------------------------------------------- The tick (spec section 1)

		/// <summary>One slot of the 16-slot clock in PIT ticks, about 62.5 ms
		/// (seg034_2F89_406, 114965-114990).</summary>
		public const int SlotPitTicks = 16;

		/// <summary>The slot advance per frame is clamped to this many (64 PIT ticks, 114958):
		/// slow frames catch up instead of skipping updates.</summary>
		public const int MaxSlotsPerFrame = 4;

		/// <summary>An appointment fires 1..4 slots after its slot (seg007_1798_3825, 54807).</summary>
		public const int DueWindowSlots = 4;

		/// <summary>Walking, attacking, goals 3, 5, 6, 9, 11, dying (47356, 49533, 54486).</summary>
		public const int IntervalWalking = 4;

		/// <summary>Standing idle, goals 10 and 12, reacting to the player (48212, 48819, 51248).</summary>
		public const int IntervalStanding = 6;

		/// <summary>Stuck, drowning, the climb hop, an inactive NPC_Goto (41408, 46766, 46420).</summary>
		public const int IntervalStuck = 1;

		/// <summary>
		/// Goals 13-15 (SwitchGoals_seg007_1798_3247, 54050-54056). They are the DEFAULT case
		/// of the goal switch and do nothing but set this interval - no movement, no
		/// perception, no attack.
		///
		/// AND THAT IS ALL THEY ARE IN UW1, gone through on 2026-09-22 because the Todo carried
		/// them as "petrification" and as something still to build. The shipped game does not
		/// use them either: a walk over DATA\LEV.ARK finds twenty-one mobile records with a
		/// goal of 13 or 14, but every one of them is a FREE SLOT that no tile list links to,
		/// so the bytes are leftovers and not creatures. Six are the same two slots, 113 and
		/// 120, repeating level after level, which is what a template leaves behind.
		///
		/// THE NAME WAS NOT INVENTED AFTER ALL - it is UW2 (per user, who suspected cut
		/// content, 2026-09-22; checked in the reference the same day). There goal 15 is
		/// npc_goal_petrified: the Paralyse spell of class 7 sets it with a DURATION in gtarg
		/// and attitude 1, a rune trap can set it too, and the creature is drawn with its stone
		/// art. IN UW1 THE SAME SPELL SETS GOAL 7 INSTEAD, stand still, with attitude 1 and no
		/// duration - which is what UWCritter.Paralyse does. So 13 to 15 are slots this engine
		/// keeps free and the sequel fills; goal 13 and 14 have no name even there.
		/// </summary>
		public const int IntervalUnknownGoal = 7;

		/// <summary>The distance cull advances the due slot by this, not an interval (52715).</summary>
		public const int FarRescheduleSlots = 8;

		/// <summary>Beyond this squared tile distance from the player a creature is frozen and
		/// re-examined every half second; goal 3 is exempt (NPCInitialProcessing 52640-52720).</summary>
		public const int FarTileDistanceSquared = 100;

		/// <summary>
		/// The distance cull asks TWO distances and freezes only when BOTH are beyond ten tiles:
		/// to the player object (index 1, its tile word 0x16) and to the tile the view is drawn
		/// from (the asm's PlayerTileX/Y, set from the camera every frame). Read 2026-09-24 after
		/// the user saw the slugs in front of the moonstone move through the camera trap's picture
		/// while ours stood still; the same keeps creatures awake under Roaming Sight's eye.
		/// </summary>
		public static bool IsFarFromBoth(int piDxPlayer, int piDyPlayer, int piDxView, int piDyView)
		{
			return (piDxPlayer * piDxPlayer) + (piDyPlayer * piDyPlayer) > FarTileDistanceSquared
				&& (piDxView * piDxView) + (piDyView * piDyView) > FarTileDistanceSquared;
		}

		// ------------------------------------------------- Perception and temper (sections 3, 4)

		/// <summary>The kin alarm lives this long, 512 PIT ticks, about 2 s (NPCBehaviours 53553).</summary>
		public const int KinAlarmPitTicks = 0x200;

		/// <summary>Heard when hear2 / 4 &gt; d2, strict (SearchForGoalTarget 51704-51757).</summary>
		public const int HearingDivisor = 4;

		/// <summary>Lost when d2 &gt;= hear2 * 4 (51895-51913); in between the result is
		/// "unchanged".</summary>
		public const int LostMultiplier = 4;

		/// <summary>Relative eighth 0, 1 or 7 sees (51838): one eighth to either side.</summary>
		public const int FacingToleranceEighths = 1;

		/// <summary>The wander step faces a player closer than this (squared eighths) when the
		/// creature stands or the player has a weapon drawn (ReactToPlayerPresence 51383-51507).</summary>
		public const int ReactToPlayerDistanceSquared = 0x90;

		/// <summary>The wander heading is bent away from a player within this many eighths
		/// (MaybeVectorsToPlayer called at 48062).</summary>
		public const int WanderAvoidPlayerEighths = 10;

		/// <summary>Goal 6 bends its heading away from a player within this many eighths (50858).</summary>
		public const int WithdrawAvoidPlayerEighths = 0x18;

		/// <summary>Goal 5 re-searches the target with 1 in this per update after the target
		/// changed tile (AttackGoalSearchForTarget 49626).</summary>
		public const int ReSearchChance = 8;

		/// <summary>On the search result "unchanged" a coin (1 in this) keeps the target
		/// (49690) or walks over to look (48777).</summary>
		public const int UnchangedCoin = 2;

		/// <summary>The standing rolls: RNG(16) &lt;= initiative turns, RNG(16) &lt; initiative
		/// searches (StandStillGoal 48688-48716).</summary>
		public const int InitiativeRoll = 16;

		/// <summary>The kin alarm's window, the damage gate and the search are all clocked by the
		/// creature's own updates; the morale check rolls RNG(4) on top of the hp term
		/// (seg007_1798_3383, 54246).</summary>
		public const int MoraleRoll = 4;

		// ------------------------------------------------- The wander step (section 6.3)

		/// <summary>Whether a walking creature keeps walking or stops in its wander step is its
		/// RESTLESSNESS (table byte 0x1F, low nibble) against a roll of this many, at frame 3
		/// only (47906, 47933): restless kinds keep going, placid ones stand.</summary>
		public const int RestlessnessRoll = 16;

		/// <summary>RNG(0x40) &lt; restlessness + 8 deflects the walking heading (48030-48062).</summary>
		public const int WanderDeflectRoll = 0x40;

		public const int WanderDeflectBonus = 8;

		/// <summary>RNG(0x80) &lt; restlessness deflects the heading while standing (48085-48125).</summary>
		public const int IdleTurnRoll = 0x80;

		/// <summary>A deflection is RNG(0x40) - 0x20 heading units (48062, 50858).</summary>
		public const int DeflectionHalfRange = 0x20;

		/// <summary>The wander step on a collision with unchanged heading
		/// (NPCWanderUpdate_seg007_1798_1, 47988-48000): the fine heading turns by this, a
		/// quarter of the circle to the left or the right, and the speed is zero for that update.</summary>
		public const int QuarterTurn = 0x40;

		/// <summary>The same quarter turn in degrees, for the host's traces.</summary>
		public const int BlockedTurnDegrees = 90;

		/// <summary>In the wander step a HOSTILE creature (attitude 0) runs StandStillGoal in one
		/// update of this many instead of wandering (RNG mod 2, 47781).</summary>
		public const int HostileStandChance = 2;

		// ------------------------------------------------- Movement (section 7)

		/// <summary>
		/// THE BLOCKED STEP (UW.EXE NPC_Goto_seg006_1477_29A1, read 2026-09-19). When a
		/// creature's step collides with an unchanged heading, the creature does not slide: a
		/// collision with a creature that is itself attacking while this one attacks counts for
		/// nothing, a closed door is tried three times in four by a hostile creature, an open
		/// door lets a flier duck; anything else - the player included (46937) - sets its blocked
		/// bit (object byte 0x18 bit 6). While the bit stands the creature takes wander steps
		/// instead of the approach, and each update one in this many clears the bit and the
		/// approach resumes (RNG mod 8, 47010).
		/// </summary>
		public const int BlockedClearChance = 8;

		/// <summary>A hostile creature tries a closed door on 3 of this many collisions, the
		/// fourth goes blocked (46870).</summary>
		public const int DoorTryChance = 4;

		/// <summary>With door skill RNG(2) != 0 picks the lock (47540) ...</summary>
		public const int LockpickChance = 2;

		/// <summary>... otherwise RNG(4) == 0 bashes it with RNG(table byte 0x14) damage (47570).</summary>
		public const int BashChance = 4;

		/// <summary>The facing and the moving heading turn at most this much per update, 45
		/// degrees (seg007_1798_1FDC, 52040, 52100).</summary>
		public const int TurnLimitPerUpdate = 0x20;

		/// <summary>A wanted turn of this much or more (up to 0xC0) stops the creature for one
		/// update (label seg007_1798_2108).</summary>
		public const int ReversalStopThreshold = 0x40;

		/// <summary>Momentum = speed * 47 (InitMotionParams_seg029_29EE_3CC, 100347-100380).</summary>
		public const int MomentumPerSpeedPoint = 0x2F;

		/// <summary>The player's full walk momentum (BaseForwardSpeed, 280275): a creature at
		/// speed s covers s / 20 of it per update.</summary>
		public const int PlayerBaseMomentum = 0x3AC;

		/// <summary>The physics consumes the momentum over interval * 16 PIT ticks
		/// (CalculateMotionTopLevel_seg006_6CB, 41703).</summary>
		public const int StepTicksPerInterval = 16;

		/// <summary>The stepper's scale as read in 04-movement.md: 0x2000 sub-units per eighth,
		/// so a walking update at speed s moves s * 47 * 64 / 8192 = 0.367 * s eighths. The
		/// absolute scale is to be confirmed in game (spec section 12, question 3).</summary>
		public const int SubUnitsPerEighth = 0x2000;

		/// <summary>The path search box reaches this many tiles beyond start and target (43618).</summary>
		public const int PathBoxMarginTiles = 5;

		public const int PathDepth = 32;

		public const int PathWave = 64;

		public const int PathSlots = 16;

		/// <summary>The straight-line test lists at most this many tiles (45460).</summary>
		public const int StraightLineMaxTiles = 0x3F;

		/// <summary>The path record advances once the fine position is 6 or more (or 1 or less)
		/// towards the next tile (seg006_1477_24A8, 45991).</summary>
		public const int PathCornerCutFineHigh = 6;

		public const int PathCornerCutFineLow = 1;

		/// <summary>Aim point of a normal path step on an axis: tile * 8 + 4 when the tile
		/// column equals the creature's, + 7 when the path tile lies below, + 0 when above
		/// (TurnTowardsPath_seg006_1477_2504, 46170-46230).</summary>
		public const int PathAimCentre = 4;

		public const int PathAimBelow = 7;

		public const int PathAimAbove = 0;

		/// <summary>The climb hop (seg006_1477_2679, 46420): speed 11, pitch 22, started when the
		/// Manhattan distance to the aim point falls below 3.</summary>
		public const int HopSpeed = 11;

		public const int HopPitch = 22;

		public const int HopManhattan = 3;

		/// <summary>A flier aims at 20 + 8 * destination floor zpos, capped at 0x78
		/// (seg006_1477_3061, 47390).</summary>
		public const int FlierTargetHeightBase = 20;

		public const int FlierTargetHeightMax = 0x78;

		/// <summary>Pitch values of the fliers: 14 sinks, 18 climbs, 16 is level (47369-47440).</summary>
		public const int FlierPitchDown = 14;

		public const int FlierPitchUp = 18;

		public const int FlierPitchLevel = 16;

		// ------------------------------------------------- Combat (section 8)

		/// <summary>Goal 5, the attack (NPC_Goal5_Attack_seg007_1798_891): below this squared
		/// distance in eighths of a tile, ten eighths, the melee branch runs and the start roll
		/// is made (48965, 49462). See MeleeStandDistanceSquared for why a swing rolled here does
		/// not always survive.</summary>
		public const int MeleeDistanceSquared = 0x64;

		/// <summary>
		/// THE BLOW DOES NOT STOP THE FEET. After the melee decision goal 5 still runs its
		/// movement (AttackGoalSearchForTarget_seg007_E5D), and a melee creature keeps closing
		/// until the squared distance in eighths is at most this - eight eighths, one tile
		/// ((1 * 1) shifted left by three twice, 49755-49790) - and the floors lie within four
		/// height levels.
		///
		/// THE EFFECTIVE STRIKE RANGE OF GOAL 5 IS THIS, NOT MeleeDistanceSquared (found
		/// 2026-09-19, spec 6.5 and 11.15): NPC_Goto writes animation 0x2C and frame++
		/// unconditionally at its end (label seg006_1477_2FB7, 47293-47355) and has no guard for
		/// a running swing at its head, so a swing rolled at 65..100 squared eighths is
		/// overwritten by the walk in the same update. A blow is only executed when it was
		/// rolled at 64 or below, or on the target's tile. That is why the original's goblin
		/// stood at 0.9 tiles where ours stood at 1.2 (per user, 2026-09-19, two saves written
		/// by the original in the middle of the fight, goal 5 both times). Goal 9 keeps its own
		/// band, StandMeleeReachSquared, because it returns right after the roll.
		/// </summary>
		public const int MeleeStandDistanceSquared = 64;

		/// <summary>Goal 9 strikes below this squared eighth distance, without a floor test
		/// (NPCGoal9_seg007_1798_12C6, 50313).</summary>
		public const int StandMeleeReachSquared = 0x90;

		/// <summary>Below this (7 eighths) the melee footwork side-steps or backs off
		/// (ChooseMeleeAttackToMake_seg007_1798_AFF, 49256).</summary>
		public const int MeleeBackOffDistanceSquared = 0x31;

		/// <summary>Up to this (9 eighths) it circles or stands (49349).</summary>
		public const int MeleeCircleDistanceSquared = 0x51;

		/// <summary>Goal 5 melee needs a floor difference below this many levels (48985); a
		/// flier needs none.</summary>
		public const int MeleeHeightLevels = 4;

		/// <summary>In range, every update rolls 1 in this to start a swing (49469).</summary>
		public const int MeleeStartChance = 4;

		/// <summary>The blow executes at this frame, the fifth update of the swing (53040-53080).</summary>
		public const int MeleeHitFrame = 4;

		/// <summary>The swing charge nibble (word 0x0F bits 12-15) rises by one per failed start
		/// roll up to this (49540-49555) and is cleared when the blow lands (53070).</summary>
		public const int ChargeIndexMax = 15;

		/// <summary>The damage multiplier by charge index, over 128 (seg060, 263511). The same
		/// numbers as UWCombat.NpcSwingCharges.</summary>
		public static readonly int[] ChargeTable =
		{
			50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 155, 170, 185, 205, 230, 255
		};

		/// <summary>Below 7 eighths 1 in this side-steps instead of backing off (49256).</summary>
		public const int SideStepChance = 4;

		/// <summary>The footwork speeds: backing off with animation 7, walking in, circling
		/// (49307, 49380, 49363).</summary>
		public const int BackOffSpeed = 2;

		public const int WalkInSpeed = 2;

		public const int CircleSpeed = 1;

		/// <summary>The side-step runs at wanderSpeed * 2 / 3 (49290).</summary>
		public const int SideStepSpeedNumerator = 2;

		public const int SideStepSpeedDenominator = 3;

		/// <summary>Goal 6 close branch: tile^2 &lt;= 3 and |dz| &lt; 16 zpos (50540-50568).</summary>
		public const int WithdrawNearTilesSquared = 3;

		public const int WithdrawNearHeight = 16;

		/// <summary>The stand roll of goal 6: RNG(256) &lt; morale &gt;&gt; 3 (50569).</summary>
		public const int StandRoll = 256;

		/// <summary>Goal 6 far branch: a collision within this squared tile distance corners the
		/// creature into goal 9 (50678).</summary>
		public const int CorneredTilesSquared = 9;

		/// <summary>Goal 6 runs at the pursuit speed below this squared tile distance, the wander
		/// speed above (50936).</summary>
		public const int FleeFastTilesSquared = 64;

		/// <summary>Goal 9 faces and holds within this squared tile distance (50330).</summary>
		public const int HoldGroundTilesSquared = 4;

		/// <summary>The leash of goal 5 applies only beyond this squared eighth distance to the
		/// target, two tiles (49129) ...</summary>
		public const int LeashTargetDistanceSquared = 0x100;

		/// <summary>... when the squared tile distance from home exceeds 4 * range^2, twice the
		/// travel range (49141).</summary>
		public const int LeashRangeFactor = 4;

		/// <summary>Goal 8 walks home beyond range^2 (48600).</summary>
		public const int HomeRangeFactor = 1;

		/// <summary>Goal 3: swings for show within 2 tiles^2, teleports beyond 64 to four tiles
		/// short of the target (48300-48470).</summary>
		public const int FollowNearTilesSquared = 2;

		public const int FollowTeleportTilesSquared = 64;

		public const int FollowTeleportShortTiles = 4;

		/// <summary>Goal 10, wants to talk: within this squared distance in eighths of a tile
		/// the creature turns to the player (GoalTalkto_seg007_1798_1937: cmp si, 190h). Twenty
		/// eighths, two and a half tiles.</summary>
		public const int TurnToTargetDistanceSquared = 0x190;

		/// <summary>... and within this one it speaks, if the player faces it (cmp si, 90h).
		/// Twelve eighths, one and a half tiles - the same number as the hand's reach.</summary>
		public const int TalkDistanceSquared = 0x90;

		/// <summary>The same routine with the caster's argument: a MAGIC USER stops closing as
		/// soon as the target is within FOUR tiles (squared tile distance at most 16, squared
		/// eighths at most 4 * 4 shifted left by three twice = 1024) and casts from there. The
		/// reverse engineer's comment says 8, the code loads 4 (NPC_Goal5_Attack, the first
		/// instruction of the frame, 48879; AttackGoalSearchForTarget 49740-49775).</summary>
		public const int CasterStandTiles = 4;

		/// <summary>THE MAGES of the orb level (kind 0x13, "a_mage") use stand-off 1 and do not cast
		/// while the orb stands (48879-48905). NOT Tybal: he is kind 0x19 and casts with the orb
		/// whole, in the original as in ours (per user, 2026-09-23) - until then this was called
		/// TybalKind.</summary>
		public const int OrbMageKind = 0x13;

		public const int OrbMageStandTiles = 1;

		/// <summary>Spells 1 and 2 need a squared tile distance below 64, eight tiles
		/// (NPCStartMagicAttack_seg007_1798_109F, 49958-50169).</summary>
		public const int CasterSpellTilesSquared = 64;

		/// <summary>A missile needs a squared tile distance below 16, four tiles
		/// (seg007_1798_11FB, 50175-50278).</summary>
		public const int MissileTilesSquared = 16;

		/// <summary>RNG(0xC0) &lt;= dexterity starts the missile (50237).</summary>
		public const int MissileRoll = 0xC0;

		/// <summary>RNG(256) &lt; spell power casts the third spell (TryToDoMagicAttack 49862).</summary>
		public const int Spell3Roll = 256;

		/// <summary>RNG(128) &lt; spell power casts spell 1 or 2 (50090).</summary>
		public const int Spell12Roll = 128;

		/// <summary>RNG(16) &lt; 11 picks spell 1, else spell 2 (50125).</summary>
		public const int PrimarySpellRoll = 16;

		public const int PrimarySpellChance = 11;

		/// <summary>The spell slot bits of byte 0x19 (2-3): 1 = table byte 0x2A, 2 = 0x2B,
		/// 3 = 0x2C.</summary>
		public const int SpellSlotThird = 3;

		/// <summary>The blow scans for a collision 2 + 3 eighths ahead with radius 2 + 1
		/// (CheckForAttackHit_seg022_466, labels 48A-4A0).</summary>
		public const int AttackReachEighths = 5;

		public const int AttackReachRadius = 3;

		/// <summary>The swing type handed to NPCExecuteAttack is RNG(9) (53040-53080).</summary>
		public const int SwingTypeRoll = 9;

		/// <summary>SkillCheck_seg037_32E6_C (123242): v = skill - check + RNG(31); above 28 a
		/// critical hit, above 15 a hit, above 2 a miss, else a critical miss.</summary>
		public const int SkillCheckRoll = 31;

		public const int CritThreshold = 28;

		public const int HitThreshold = 15;

		public const int MissThreshold = 2;

		/// <summary>A strong individual (word 0x0D bit 10) adds 7 + RNG(6) to the score and
		/// 4 + RNG(12) to the damage (NPCExecuteAttack label 1679).</summary>
		public const int StrongToHitBase = 7;

		public const int StrongToHitRoll = 6;

		public const int StrongDamageBase = 4;

		public const int StrongDamageRoll = 12;

		/// <summary>A strong defender's armour is a * 5 / 3 (label 990).</summary>
		public const int StrongArmourNumerator = 5;

		public const int StrongArmourDenominator = 3;

		/// <summary>Missile hits use charge 128 over 128 (MissileAttackHit_seg022_14BF, 83234).</summary>
		public const int MissileCharge = 0x80;

		/// <summary>Poison: ScaleDamageAgainstObject type 0x10 decides the resistance (label 16B1).</summary>
		public const int PoisonResistanceType = 0x10;

		/// <summary>Kill experience: 4 * base + 2d(base) (AwardKillEXP_seg022_1725, 83677).</summary>
		public const int ExperienceFactor = 4;

		/// <summary>A strong individual's experience is times (24 + RNG(24)) / 16 (83787-83803).</summary>
		public const int StrongExperienceBase = 24;

		public const int StrongExperienceRoll = 24;

		public const int StrongExperienceDivisor = 16;

		/// <summary>The corpse object is placed with 7 in 16 (DropNPCRemains, 41019-41024).</summary>
		public const int CorpseChance = 7;

		public const int CorpseRoll = 16;

		/// <summary>The body is removed at frame 3 of animation 0x0C (53030-53090).</summary>
		public const int DeathRemovalFrame = 3;

		// ------------------------------------------------- Drowning (section 7, deviation 43)

		/// <summary>The terrain and footing bits of the tile state the land creature's motion
		/// callback masks out before it compares (seg006_1477_476, asm line 41419
		/// "and ax, 0F8h"): 0x08 plain floor, 0x10 water, 0x20 lava, 0x40 the fourth terrain,
		/// 0x80 standing on an object.</summary>
		public const int TileStateFootingMask = 0xF8;

		/// <summary>The only value that may remain after the mask: water alone, so water under
		/// the feet with nothing to stand on (asm line 41420 "cmp ax, 10h").</summary>
		public const int TileStateWaterOnly = 0x10;

		/// <summary>
		/// Does this step drown the creature? The land creature's motion callback drowns it
		/// when the tile state masked with 0xF8 equals 0x10 (seg006_1477_476, asm lines
		/// 41418-41421), that is: water under the feet, no plain floor, no lava, no object
		/// carrying it. A SWIMMER (table byte 0x0A bit 6) and a FLIER (bit 7) never reach the
		/// branch - their callbacks seg006_1477_65E and seg006_1477_5FE have no water branch
		/// at all, and the swimmer's handler word 0 = 0x0010 already drops the water bit
		/// before the callback runs (Docs/AI/creature-ai.md section 7.6).
		/// </summary>
		public static bool Drowns(int piTileState, bool pbSwimmer, bool pbFlier)
		{
			if (pbSwimmer || pbFlier)
				return false;

			return (piTileState & TileStateFootingMask) == TileStateWaterOnly;
		}

		/// <summary>The same rule for a host that has no tile-state word but knows the two
		/// facts it stands for: deep water under the creature, and whether something carries
		/// it (the original's bit 0x80).</summary>
		public static bool Drowns(bool pbInDeepWater, bool pbStandsOnObject, bool pbSwimmer, bool pbFlier)
		{
			int liState = (pbInDeepWater ? TileStateWaterOnly : 0x08) | (pbStandsOnObject ? 0x80 : 0);

			return Drowns(liState, pbSwimmer, pbFlier);
		}

		/// <summary>Death sound 6 plays when table byte 8 &amp; 7 == 1 (Death_seg007_1798_35CB,
		/// label 35F3).</summary>
		public const int DeathSound = 6;

		/// <summary>The music: theme 6 is combat, theme 5 the hurt variant when
		/// hp * 64 / (vitality + 1) &lt; 16 (DamageNPC 54563-54802, swing frame 0 at 53136).</summary>
		public const int CombatTheme = 6;

		public const int HurtCombatTheme = 5;

		public const int HurtThemeThreshold = 16;

		// ------------------------------------------------- The player's loudness (section 3.2)

		/// <summary>The player's noise base is 13 - Sneak / 3, his visibility 15 - Sneak / 5
		/// (ResetPlayerStatusValues_ovr133_1E4, 380878-380916).</summary>
		public const int PlayerNoiseBase = 13;

		public const int PlayerNoiseSneakDivisor = 3;

		public const int PlayerVisibilityBase = 15;

		public const int PlayerVisibilitySneakDivisor = 5;

		/// <summary>Moving: base + speed * 10 / forward - 5; +4 in water; base + 4 in easy move;
		/// decays one per 8th call (ApplyPlayerSneakScore_seg034_2F89_898, 115605-115709).</summary>
		public const int PlayerNoiseSpeedFactor = 10;

		public const int PlayerNoiseMovingOffset = 5;

		public const int PlayerNoiseWater = 4;

		public const int PlayerNoiseEasyMove = 4;

		public const int PlayerNoiseDecayCalls = 8;

		// ------------------------------------------------- Theft (section 4.5)

		/// <summary>
		/// THE AREA of a theft or a trespass: ovr104_E4C hands RunCodeOnObjectsInArea_seg038_3307_9C7
		/// the corner seven tiles back and a size of 15 (labels E9E-EB8) - and that routine's loops run
		/// UP TO AND INCLUDING corner + size (labels C32-C46, jl on the far side), so the area is 16
		/// tiles: seven back, the centre, eight forward, on both axes (read 2026-09-23). The trespass
		/// had this lopsided area from the reference already; the theft searched seven to each side
		/// until then. The sight line goes to the item's z + height + 12 (341300 area).
		/// </summary>
		public const int TheftAreaBack = 7;

		public const int TheftAreaForward = 8;

		public const int TheftItemHeightBonus = 12;

		/// <summary>
		/// WHERE A CREATURE LOOKS WHEN IT CHECKS A THEFT (AngerNPCByIllegalAction_ovr104_C37,
		/// labels D0B to the TestBetweenPoints call): at the object's own sub-tile spot in
		/// eighths on the usage tile, and at its zpos plus its COMOBJ height plus 12 - well
		/// above the floor. Until 2026-09-23 ours aimed at the floor of the tile's centre, and a
		/// goblin standing two steps lower than the barrels saw nothing; in the original it saw
		/// both thefts (per user: the goblin on 51/58, attitude 2 to 0 in the original, 2 in ours).
		/// The creature's own end is its zpos plus its COMOBJ height.
		/// </summary>
		public static int GetTheftSightZ(int piZPos, int piHeight)
		{
			return piZPos + piHeight + TheftItemHeightBonus;
		}

		/// <summary>
		/// The sight range test before the line (label D5x-DB2): the distances in eighths are
		/// divided by 8 - truncated toward zero, as idiv does - then squared and added, against
		/// the square of the upper nibble of critter byte 0x1E.
		/// </summary>
		public static bool IsWithinTheftSight(int piDeltaX8, int piDeltaY8, int piSightRange)
		{
			int liX = piDeltaX8 / 8;
			int liY = piDeltaY8 / 8;

			return (liX * liX) + (liY * liY) <= piSightRange * piSightRange;
		}

		/// <summary>
		/// WHO MINDS A THEFT OR A TRESPASS, per creature - AngerNPCByIllegalAction_ovr104_C37
		/// (341158-341515), the one routine behind both (read 2026-09-23). The owner is the full
		/// six bits of the item's owner field (byte 6 &amp; 0x3F), or the trap's for a trespass.
		///
		///   the creature's race (table byte 9) must be owner &amp; 0x1F  (label C70)
		///   a creature with byte 0x0A bit 7 minds it only when the owner has bit 5 (C74-C8C)
		///   owner 0x20 exactly is minded ONLY by such creatures                    (C8E-CA6)
		///   owner 0x0D, the knights, minds nothing once quest 32 has reached 3     (CAB-CBA)
		///
		/// The sight range and the line to the item come after that (UWCritter.CanSeeTheftAt).
		/// </summary>
		public static bool MindsTheft(int piOwner, int piCreatureRace, bool pbAttitudeLocked, int piKnightQuest)
		{
			if (piOwner == 0 || piCreatureRace != (piOwner & TheftOwnerRaceMask))
				return false;

			if (pbAttitudeLocked && (piOwner & TheftOwnerLockedBit) == 0)
				return false;

			if (piOwner == TheftOwnerLockedBit && !pbAttitudeLocked)
				return false;

			return !(piOwner == TheftKnightOwner && piKnightQuest >= TheftKnightQuestValue);
		}

		/// <summary>The race part of an owner value.</summary>
		public const int TheftOwnerRaceMask = 0x1F;

		/// <summary>Bit 5 of an owner value: creatures whose attitude is locked mind it too.</summary>
		public const int TheftOwnerLockedBit = 0x20;

		/// <summary>The knights' owner value, and the quest that makes the player one of them.</summary>
		public const int TheftKnightOwner = 0x0D;

		public const int TheftKnightQuest = 32;

		public const int TheftKnightQuestValue = 3;

		/// <summary>
		/// What a creature that minds it does: one step of goodwill less, never below zero
		/// (label DD0-DFB), and it says so by its NEW attitude - string block 2 0xE1 + attitude in
		/// UW.EXE, our 226 + attitude (the usual one higher): 0 " is angered by your action.",
		/// 1 " is annoyed by your action.", 2 " notes your action." (label E01-E28). Unlike ours
		/// until 2026-09-23 a creature already hostile still says it, and the message follows
		/// the attitude instead of being fixed per caller.
		/// </summary>
		public static int LowerAttitudeForTheft(int piAttitude)
		{
			return piAttitude > 0 ? piAttitude - 1 : 0;
		}

		public const int TheftMessageBase = 226;

		/// <summary>AFTERWARDS the item belongs to nobody - but only when owner &amp; 0x1F is at
		/// most 0x1B (ovr104_E4C labels ECF-EDA); the top two bits of the byte stay. Ours
		/// cleared it always, and asked a different limit (28) before minding at all.</summary>
		public static bool ClearsOwnerAfterTheft(int piOwner)
		{
			return (piOwner & TheftOwnerRaceMask) <= TheftOwnerClearLimit;
		}

		public const int TheftOwnerClearLimit = 0x1B;

		// ------------------------------------------------- Headings

		/// <summary>
		/// The original's eight-way quantisation of a vector (GetVectorHeading_seg006_1477_2862):
		/// 0 north (positive Y), then clockwise in steps of 45 degrees, 2 east, 4 south, 6 west.
		///
		/// THE SECTORS ARE NOT EQUAL. It compares against twice the other component, so the
		/// boundaries lie at the slopes 2 and 1/2: a cardinal direction owns 53 degrees, a diagonal
		/// only 37. A vector of (0, 0) comes out as 4, south, as in the original.
		///
		/// The original works on signed bytes and doubles them in a byte too, so anything beyond
		/// 63 wraps there. The callers stay far below that: sight ranges in tiles, talk distances in
		/// eighths within a few tiles.
		/// </summary>
		public static int GetVectorHeading(int piDx, int piDy)
		{
			if (piDy > 2 * piDx)
			{
				if (piDx > -2 * piDy)
					return piDy > -2 * piDx ? 0 : 7;

				return piDx > 2 * piDy ? 5 : 6;
			}

			if (piDx > -2 * piDy)
				return piDx > 2 * piDy ? 2 : 1;

			return piDy > -2 * piDx ? 3 : 4;
		}

		/// <summary>
		/// Does a creature with this heading (0..7) look towards the vector? The original accepts
		/// the heading of the vector itself and its two neighbours (SearchForGoalTarget: the
		/// difference modulo eight is 0, 1 or 7) - so up to one eighth to either side. For
		/// noticing the player the vector is counted in whole tiles.
		/// </summary>
		public static bool IsFacing(int piHeading, int piDx, int piDy)
		{
			int liRelative = (GetVectorHeading(piDx, piDy) - (piHeading & 7) + 8) % 8;

			return liRelative == 0 || liRelative == 1 || liRelative == 7;
		}

		/// <summary>
		/// Does the other one, with this heading, look back along the vector that points from
		/// here to him? The original turns it round (GoalTalkto: the other's heading plus eight
		/// minus the vector's heading, masked to eight, is 3, 4 or 5) - straight back or one eighth
		/// to either side. For being addressed the vector is counted in eighths of a tile.
		/// </summary>
		public static bool IsFacedBy(int piOtherHeading, int piDx, int piDy)
		{
			int liRelative = ((piOtherHeading & 7) + 8 - GetVectorHeading(piDx, piDy)) & 7;

			return liRelative >= 3 && liRelative <= 5;
		}

		/// <summary>The flanking bonus of a blow (CalcFlankingBonus_seg022_230E_D48, 81871):
		/// d = (defenderFacing + 12 - attackerFacing) &amp; 7, d if d &lt;= 4 else 8 - d. Zero
		/// from the front, four from behind; added to the attack score and the damage.</summary>
		public static int GetFlankingBonus(int piAttackerFacing, int piDefenderFacing)
		{
			int liD = ((piDefenderFacing & 7) + 12 - (piAttackerFacing & 7)) & 7;

			return liD <= 4 ? liD : 8 - liD;
		}

		/// <summary>A heading more than this away from the wall's direction (fine heading units,
		/// 256 a circle; 0x3000 of the 16-bit angle, 67.5 degrees) hits the wall head-on: no
		/// deflection.</summary>
		public const int HeadOnWallLimit = 0x30;

		/// <summary>
		/// THE WALL DEFLECTION of a creature's step (seg030_2B26_A47, 104317; per user,
		/// 2026-09-28: Biden, wandering diagonally, kept walking into the corridor walls). The
		/// physics hands in the direction the wall runs; if it points more than a quarter turn
		/// away from the heading it is turned round, so it is the wall direction nearer the
		/// heading. More than 67.5 degrees from it (head-on) nothing is deflected and the step is
		/// a collision; otherwise a creature's heading simply BECOMES the wall direction - no
		/// mirror, that is for missiles - and the step goes on along the wall. A heading already
		/// along the wall is left to the sub-tile snap of the original, which is not built.
		/// </summary>
		public static bool TryDeflectAlongWall(int piFineHeading, int piWallDirection, out int piNewHeading)
		{
			int liWall = piWallDirection & 0xFF;
			int liDiff = (sbyte)(byte)((liWall - piFineHeading) & 0xFF);

			if (liDiff > QuarterTurn || liDiff < -QuarterTurn)
			{
				liWall = (liWall + 0x80) & 0xFF;
				liDiff = (sbyte)(byte)((liWall - piFineHeading) & 0xFF);
			}

			piNewHeading = liWall;

			int liAbsolute = liDiff < 0 ? -liDiff : liDiff;

			return liAbsolute != 0 && liAbsolute <= HeadOnWallLimit;
		}

		/// <summary>
		/// The morale check (seg007_1798_3383, 54196-54277): true means "withdraw". Barely hurt
		/// (hp above three quarters of the vitality) or nearly dead (below an eighth) never
		/// withdraws; more than half the vitality taken since the last decision always does;
		/// otherwise (15 - morale) &gt;= hp * 16 / vitality + RNG(4). The roll is handed in so the
		/// self-check can script it.
		/// </summary>
		public static bool MoraleCheck(int piVitality, int piHitPoints, int piMorale, int piDamageTaken, int piRoll)
		{
			bool lbDecided;

			if (MoraleCheckDecided(piVitality, piHitPoints, piDamageTaken, out lbDecided))
				return lbDecided;

			return 15 - piMorale >= (piHitPoints * 16 / piVitality) + piRoll;
		}

		/// <summary>The early exits of the morale check, so the brain rolls RNG(4) only when
		/// the original does: true when decided without the roll, the answer in pbResult.</summary>
		public static bool MoraleCheckDecided(int piVitality, int piHitPoints, int piDamageTaken, out bool pbResult)
		{
			pbResult = false;

			if (piHitPoints > piVitality * 3 / 4)
				return true;

			if (piHitPoints < piVitality / 8)
				return true;

			if (piDamageTaken > piVitality / 2)
			{
				pbResult = true;

				return true;
			}

			return piVitality == 0;
		}

		/// <summary>The path range budget (seg007_1798_340A, 54283): hp * 4 / vitality +
		/// morale / 4 for a hostile creature with vitality and without word 0 bit 13, else 0.
		/// The host's path search spends it on drops and lava (spec 7.4).</summary>
		public static int GetPathRangeBudget(int piAttitude, int piVitality, int piHitPoints, int piMorale, bool pbExcluded)
		{
			if (piAttitude != 0 || piVitality <= 0 || pbExcluded)
				return 0;

			return (piHitPoints * 4 / piVitality) + (piMorale / 4);
		}
	}
}
