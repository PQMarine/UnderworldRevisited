using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// ONE CREATURE OF THE GAME, ENGINE-FREE (cleanup step 4.11, 2026-10-18; section 7.4 of
	/// Docs/CLEANUP-PLAN.md): its record (UWCritterRecord over its UWNpc), its table row, its index in
	/// the level's master list and its mind (UWCritterBrain), and everything the mind asks of the
	/// world around it - the ICritterHost - with the creature's own decisions around the mind: the
	/// blow's outcome and aftermath, the doors, the missiles and spells, the talk, the music, the
	/// death's events, the damage it takes, the spells on it, the theft it sees, the sleep ambush's
	/// path and place, the push.
	///
	/// WHY: until this step all of it lived in the MonoBehaviour UWCritter beside the picture, so no
	/// creature decision outside the mind could run without a scene. UWCritter keeps the Unity side
	/// (ICreatureView: the body and its picture, the animator, the sounds and effects at the body, the
	/// trace) and the registry the driver walks; the world's part this class cannot answer from the
	/// data is ICreatureWorld (UWCreatureWorld, one per level). The self-check runs an actor on its
	/// fake world and view.
	///
	/// NOTHING CHANGED IN WHAT IT DOES: the members moved from UWCritter with their comments, in the
	/// same order of calls - the random draws included (the brain's through Random, the blow's
	/// through UWAttackResolution).
	///
	/// THE DEATH IS ONE STATE MACHINE HERE (cleanup step 4.12, 2026-10-19; B22): Alive, Dying, Removed
	/// (UWCreatureDeathState) - see the section "Death" below. Every step of a death goes through it:
	/// the brain's DamageNPC death (OnDeathStarted) with its experience, a named creature's refusal
	/// (VetoDeath), the drowning (OnDrowned), the creature saved while dying (the constructor), and
	/// the end at frame 3 (OnDeathFinished) with the special death, the remains and the loot - once,
	/// on the creature's own updates, whether it is near, far or under Freeze Time.
	/// </summary>
	public sealed class UWCreatureActor : ICritterHost, IUWAttackDefender
	{
		// ------------------------------------------------- Constants

		/// <summary>Attitude 0 of the object data.</summary>
		private const int HostileAttitude = UWNpc.AttitudeHostile;

		private const int WanderGoal = 2;

		private const int GoalWithdraw = 6;

		private const int GoalParalysed = 7;

		/// <summary>The player's object index and item id as the brain names them.</summary>
		private const int PlayerIndex = UWCritterBrain.PlayerIndex;

		/// <summary>How many due updates one creature may run in one frame. The clock never
		/// advances more than four slots per frame, so an interval-1 creature needs at most four;
		/// the margin catches a record whose interval loaded as 0.</summary>
		private const int MaxUpdatesPerFrame = 8;

		/// <summary>Radius and height from COMOBJ, in case the table is missing: creatures 2 and
		/// 0x20.</summary>
		private const int FallbackCritterRadius = 2;

		private const int FallbackCritterHeight = 0x20;

		/// <summary>First ammunition object: the arrow. The critter table counts from here.</summary>
		private const int FirstAmmunitionObjectId = UWObjectMechanics.FirstAmmunitionObjectId;

		/// <summary>flood_path's range for the ambush: no danger at all.</summary>
		private const int AmbushPathRange = 0;

		// ------------------------------------------------- What it is made of

		private readonly ICreatureWorld mIWorld;

		private readonly ICreatureView mIView;

		/// <summary>The hit points' rules of its body (the UWDamageable's UWHealth), reading and
		/// writing the record's byte 8 (KeepHitPointsIn).</summary>
		private readonly UWHealth mOHealth;

		private readonly UWObjectClassProperties.Critter mOStats;

		private readonly bool mbHasStats;

		private readonly bool mbFlying;

		private readonly bool mbSwimming;

		/// <summary>Its own object data; the record lies over its RawNpcBytes.</summary>
		private readonly UWNpc mONpc;

		private readonly UWCritterRecord mORecord;

		private readonly UWCritterBrain mOBrain;

		/// <summary>The object's index in the level's master list (1 is the player); 0 when the
		/// object is not in the list.</summary>
		private readonly int miIndex;

		/// <summary>What the last step on the motion core left behind (the F1 overlay).</summary>
		private StepResult mOLastStep;

		/// <summary>Where its death stands - the one state of the death machine (cleanup step 4.12,
		/// 2026-10-19; see the section "Death"). Until then a removed flag here, the body's destroyed
		/// flag and UWCritterRemains' own dying flag with its timer each held a part of it.</summary>
		private UWCreatureDeathState meDeath;

		private int miAttackIndex;

		/// <summary>How often this creature has shot or cast - only for the F1 overlay.</summary>
		private int miShotsFired;

		/// <summary>Where the record stood at build time: tile, eighth and zpos (cleanup step 4.8,
		/// 2026-10-18; the body's world point until then).</summary>
		private readonly int miSpawnTileX;

		private readonly int miSpawnTileY;

		private readonly int miSpawnFineX;

		private readonly int miSpawnFineY;

		private readonly int miSpawnZPos;

		// ------------------------------------------------- Setup

		/// <param name="pONpc">Its object data; null gives a record over a scratch UWNpc.</param>
		/// <param name="pOStats">Its table row.</param>
		/// <param name="pbHasStats">Whether the row is there.</param>
		/// <param name="pIWorld">The level's creature world; null without a level.</param>
		/// <param name="pIView">Its picture.</param>
		/// <param name="pOHealth">Its body's hit points (they read the record from now on); null for
		/// none.</param>
		public UWCreatureActor(UWNpc pONpc, UWObjectClassProperties.Critter pOStats, bool pbHasStats, ICreatureWorld pIWorld,
			ICreatureView pIView, UWHealth pOHealth)
		{
			mOStats = pOStats;
			mbHasStats = pbHasStats;
			mIWorld = pIWorld;
			mIView = pIView;
			mOHealth = pOHealth;

			// Flier and swimmer come from table byte 0x0A bits 7 and 6 (spec 2, deviation 42),
			// not from the category byte any more.
			mbFlying = pbHasStats && pOStats.IsFlier;
			mbSwimming = pbHasStats && pOStats.IsSwimmer;

			// A creature without data still gets a record over a scratch UWNpc so the brain can
			// run; nothing of it is saved.
			mONpc = pONpc ?? new UWNpc((ushort)(pbHasStats ? UWObjectClassProperties.CritterFirstId : 0));
			mORecord = new UWCritterRecord(mONpc);

			// clear_paths with every level build (init_level_creature_stuff): no creature holds a
			// stored path - the slots are new (UWGameSession.ResetCritterServices).
			mORecord.HasPath = false;

			UWLevel lOLevel = fLevel();

			miIndex = lOLevel != null && pONpc != null ? Math.Max(0, lOLevel.Masterlist.IndexOf(pONpc)) : 0;

			// THE BRAIN'S SERVICES AND CONSTANTS AT ITS BIRTH (cleanup step 4.13, 2026-10-19): the
			// session's random source, critter clock, kin alarm and path slots (read anew at every
			// call, so the level build's fresh clock and alarm reach it), this kind's object height and
			// the ammunition table's velocities. ICritterHost asked each of them per call until then;
			// the values are the same - the kind and the data do not change while the creature lives.
			GetOwnSize(out int liOwnRadius, out int liOwnHeight);

			mOBrain = new UWCritterBrain(mORecord, pOStats, miIndex, UWGameSession.Current, liOwnHeight, fMissileVelocities());
			mOBrain.GoalChanged += fOnGoalChanged;

			miSpawnTileX = mORecord.TileX;
			miSpawnTileY = mORecord.TileY;
			miSpawnFineX = mORecord.FineX;
			miSpawnFineY = mORecord.FineY;
			miSpawnZPos = mORecord.ZPos;

			// THE RECORD'S HIT POINTS ARE THE BRAIN'S. Until 2026-10-18 a record holding 0 got the
			// table value here (the spawner's UWDamageable started there), so that the first scratch
			// would not kill. THE ORIGINAL KEEPS THE 0: nothing fills a creature's hit points on
			// loading (UnderworldExhumed has no such step), and a living creature with 0 is one
			// whose death was refused - the golem saved before his script sets 125 - and dies at the
			// next blow (per user's decision the same day, cleanup 4.8). The shipped levels hold 0
			// only in five 0x7D/0x7E records of level 9, whose table value is 0 as well.
			// A creature SAVED WHILE DYING (animation 0x0C, hp 0) loads dead: its hit points stay 0 and
			// the body counts as destroyed, without a second death (2026-10-17, B11). Before, the
			// spawner's table value revived it, and one that never finishes dying could be killed
			// again for a second share of experience.
			IsLoadedDying = pONpc != null && mORecord.HitPoints == 0 && mORecord.Animation == UWCritterBrain.AnimDying;

			// FROM HERE ON THE RECORD IS THE BODY'S ONLY STORE (cleanup step 4.8, 2026-10-18, B23): the
			// UWDamageable reads and writes byte 8 (UWHealth.KeepHitPointsIn), so nothing has to keep
			// two counts in step any more.
			if (mOHealth != null)
				mOHealth.KeepHitPointsIn(mORecord);

			// The machine starts where the record stands: one saved while dying goes on dying - the
			// body committed at once, without experience, a sound or a second death (B11), and the
			// brain counts on from the saved frame to the removal. UWCritter.Initialise committed it
			// until cleanup step 4.12 (2026-10-19).
			if (IsLoadedDying)
				fCommitDeath("loaded dying");
		}

		/// <summary>It was saved while dying: it starts in Dying, its body destroyed (see the
		/// constructor).</summary>
		public bool IsLoadedDying { get; }

		// ------------------------------------------------- What the rest of the port reads

		/// <summary>This creature's index in the level's mobile list - who it is to everyone
		/// else's damage attribution.</summary>
		public int Index => miIndex;

		/// <summary>The creature's record - for the sleep ambush (UWSleepRules.TryAmbush).</summary>
		public UWCritterRecord Record => mORecord;

		/// <summary>Its object data (a scratch one for a creature without data).</summary>
		public UWNpc Npc => mONpc;

		/// <summary>The brain, for tools that want to look at the record (F1 overlay).</summary>
		public UWCritterBrain Brain => mOBrain;

		/// <summary>Its picture - the world's melee search and launch start from the body.</summary>
		public ICreatureView View => mIView;

		public UWObjectClassProperties.Critter Stats => mOStats;

		public bool HasStats => mbHasStats;

		public bool IsFlying => mbFlying;

		/// <summary>Is this a water creature? For UWCritterAnimator, which has to lift its image
		/// back up by exactly what the body was lowered into the water.</summary>
		public bool IsSwimming => mbSwimming;

		/// <summary>The death went through at frame 3: nothing runs any more.</summary>
		public bool IsRemoved => meDeath == UWCreatureDeathState.Removed;

		/// <summary>
		/// THE ONE RULE FOR BLOWS ON A CREATURE (cleanup step 4.12c, 2026-10-19, the user's decision
		/// "as the original"): it takes every blow - melee, missile, blast, target spell, the player's
		/// and a creature's - until the death machine removes it. A dying creature's record stays in
		/// its tile list until frame 3 (UnderworldExhumed critter/AI.C:1135-1140), every blow finds
		/// it, and damage_critter (AI.C:1458-1513) has no dying test: it adds to the tally, stores the
		/// attacker, writes the kin alarm for the player's blow and plays the combat or foe-hurt
		/// music; only crit_die (AI.C:1436-1447) refuses a second death (the brain's fDeath, and
		/// OnDeathStarted's Alive test keeps a second experience out). The world's blow routing
		/// (UWBlowRouting, IUWBlowBodies.CreatureTakesBlows) and the sites before it ask this through
		/// the body (UWDamageable.TakesBlows). Until then the routing let no damage through to a
		/// committed body - the player's blow, a missile, a blast and Smite Undead or Poison on a
		/// dying creature left its record alone.
		/// </summary>
		public bool TakesBlows => IsInTileList;

		/// <summary>
		/// ITS RECORD IS IN ITS TILE LIST: alive or dying, until the death machine removes it at frame
		/// 3 (critter_ai's Obj_Rem, UnderworldExhumed critter/AI.C:1135-1140). Everything the
		/// original finds through the tile lists finds the dying body too - the blows (TakesBlows)
		/// and the collision scan ObjectCheck (motion/COLLIDE.C:466-527, no dying test), so the
		/// placement test can_place (COLLIDE.C:578-640) counts it as in the way; Summon Monster asks
		/// that (combat/SPELLS.C:615, UWWorld.CreatureFitsAt). Since 2026-10-19 (the user's decision
		/// "as the original"); the summon test let a destroyed body pass until then.
		/// </summary>
		public bool IsInTileList => !IsRemoved;

		/// <summary>Where its death stands (the death machine, cleanup step 4.12).</summary>
		public UWCreatureDeathState DeathState => meDeath;

		/// <summary>What the last step on the motion core left behind (the F1 overlay).</summary>
		public StepResult LastStep => mOLastStep;

		/// <summary>How often this creature has shot or cast - only for the F1 overlay.</summary>
		public int ShotsFired => miShotsFired;

		/// <summary>Which of the three melee attacks is running or ran last: zero the bash, one the
		/// slash, two the thrust - animation slot minus one.</summary>
		public int AttackIndex => miAttackIndex;

		/// <summary>Hostile (attitude 0). A hit makes any creature hostile through the brain's
		/// damage reaction; the trespass trap through Anger.</summary>
		public bool IsAggressive => mORecord.Attitude == HostileAttitude;

		/// <summary>The attitude as it belongs in the object data, 0 hostile to 3 friendly.</summary>
		public int Attitude => mORecord.Attitude;

		/// <summary>The goal of the object data.</summary>
		public int Goal => mORecord.Goal;

		/// <summary>Byte 0x0A bit 7: the attitude is locked. Such a creature minds a theft only
		/// when the owner has bit 5 (UWCritterRules.MindsTheft).</summary>
		public bool IsAttitudeLocked => mORecord.AttitudeLocked;

		/// <summary>Is this creature Tybal? For breaking the orb (UWTybalOrbRules.TybalWhoAmI).</summary>
		public bool IsTybal => mORecord.WhoAmI == UWTybalOrbRules.TybalWhoAmI;

		/// <summary>Its death is committed - it may still be falling (Dying) or gone (Removed). A
		/// creature whose death was refused lives on with 0 hit points and is not dead.</summary>
		public bool IsDead => meDeath != UWCreatureDeathState.Alive;

		/// <summary>Critter table byte 0x1C, upper nibble; 0 without a table row.</summary>
		public int TravelRange => mbHasStats ? mOStats.TravelRange : 0;

		/// <summary>The ally bit of the mobile record - the player's blow passes over an ally
		/// (UWPlayerMelee.fFindClassicMeleeTarget, set_hitobj).</summary>
		public bool IsAlly => mORecord.IsAlly;

		/// <summary>The table's average hit points (critter byte 4, avghit) - what the eyes measure
		/// against (UWCombat.GetEyesGroup); 0 without a table row.</summary>
		public int TableVitality => mbHasStats ? mOStats.Vitality : 0;

		/// <summary>Whether the player's critical failure against this creature wears the weapon
		/// (UWEquipmentWear).</summary>
		public bool WearsWeaponOnCriticalFailure => mbHasStats && UWEquipmentWear.WearsWeaponOnCriticalFailure(mOStats);

		/// <summary>The record has left the spot it was set up on - UWWorldSync only relinks it and
		/// pulls it clear of the walls then, so whoever stood still keeps its place exactly as loaded.
		/// Any change of tile, eighth or zpos counts, as any of them moved the body by more than the
		/// one unit the old test asked for (an eighth is 8 units, a zpos step 2).</summary>
		public bool HasMoved => mORecord.TileX != miSpawnTileX || mORecord.TileY != miSpawnTileY
			|| mORecord.FineX != miSpawnFineX || mORecord.FineY != miSpawnFineY || mORecord.ZPos != miSpawnZPos;

		/// <summary>
		/// Takes one step of goodwill away from the creature, never below zero, which is hostile
		/// (UWCritterRules.LowerAttitudeForTheft, AngerNPCByIllegalAction_ovr104_C37). Used by the
		/// theft notice, a_do trap 5 (trespass) and a conversation. Returns the NEW attitude, which
		/// picks the message - or -1 for a creature that is gone. A creature already hostile stays
		/// so and still returns 0: in the original it says "is angered" again (read 2026-09-23;
		/// until then it stayed silent here).
		/// </summary>
		public int Anger()
		{
			if (IsDead)
				return -1;

			mORecord.Attitude = UWCritterRules.LowerAttitudeForTheft(mORecord.Attitude);
			fTrace("angered: attitude {0}", mORecord.Attitude);

			return mORecord.Attitude;
		}

		/// <summary>
		/// After a conversation: what the script imported into the UWNpc (attitude, goal,
		/// target, ally bit, hit points - see UWConversationSession) is carried into the record,
		/// whose properties keep the two in step from then on. The original's scripts write the
		/// same fields (contract section 6).
		/// </summary>
		public void RefreshFromData()
		{
			if (mORecord.Attitude != mONpc.NPCAttitude)
				mORecord.Attitude = mONpc.NPCAttitude;

			if (mORecord.Goal != mONpc.NPCGoal)
				mORecord.Goal = mONpc.NPCGoal;

			if (mORecord.GTarg != mONpc.NPCGTarg)
				mORecord.GTarg = mONpc.NPCGTarg;

			if (mORecord.IsAlly != mONpc.NPCIsAlly)
				mORecord.IsAlly = mONpc.NPCIsAlly;

			// The body reads the record's hit points (cleanup step 4.8, 2026-10-18), so it follows by
			// itself (the golem's script sets 125 after he was beaten, 2026-09-28) - all 125 of them:
			// until then the body's copy stopped at its maximum, the larger of the table's and the
			// loaded hit points.
			if (mONpc.HitPoints > 0 && mORecord.HitPoints != mONpc.HitPoints)
				mORecord.HitPoints = mONpc.HitPoints;

			fTrace("refreshed from data: goal {0} gtarg {1} attitude {2}", mORecord.Goal, mORecord.GTarg, mORecord.Attitude);
		}

		// ------------------------------------------------- The loop (contract section 1)

		/// <summary>
		/// Called by UWCritterDriver once per frame after the level clock advanced: while the
		/// creature is due, the brain runs one update - the physics of the previous decision
		/// through RunMotion in the middle of it, then the mind - and the picture takes the
		/// result. The order is the original's (spec 1.4): the body moves with the values the
		/// LAST goal routine left in the record, then the mind runs.
		/// </summary>
		public void RunDueUpdates(UWCritterClock pOClock)
		{
			if (IsRemoved || pOClock == null)
				return;

			for (int liGuard = 0; liGuard < MaxUpdatesPerFrame && pOClock.IsDue(mORecord.DueSlot); liGuard++)
			{
				int liSlotBefore = mORecord.DueSlot;

				UWCritterBrain.Motion lOMotion = mOBrain.Update(this);

				if (!lOMotion.Culled && lOMotion.Animation >= 1 && lOMotion.Animation <= 3)
					miAttackIndex = lOMotion.Animation - 1;

				mIView.ShowMotion(lOMotion);

				// Removed: the brain reached frame 3 and the machine ended the death in that update
				// (OnDeathFinished).
				if (lOMotion.Removed)
					return;

				// A record whose interval loaded as 0 would stay due for ever; one slot on is
				// the host's guard, not the original's (its interval field is never 0 in play).
				if (mORecord.DueSlot == liSlotBefore && !lOMotion.Culled)
					mORecord.DueSlot = UWCritterClock.NextSlot(liSlotBefore, 1);
			}
		}

		// ------------------------------------------------- The physics (contract section 3)

		/// <summary>
		/// The step of the previous decision on the motion core (UWCreatureMotion.Step, stage 2 of
		/// the motion rework, 2026-10-05), called by the brain in the middle of its update: the
		/// record moves - eighth, height, tile, heading byte, speed, gravity, pitch, contact
		/// state - and the body follows the record. The tile list is relinked when the tile
		/// changed. The lava burn of the write-back (1 in 5 per moving update) is 1 point of
		/// PLAIN fire on the UWDamageable, charged to nobody (attacker 0); the resistances of
		/// COMOBJ decide in UWHealth. The walls, doors, the player, other creatures, bridges,
		/// ledges, water and lava are all the core's now - the clearance probes and sweeps of the
		/// old substitute are gone.
		/// </summary>
		StepResult ICritterHost.RunMotion()
		{
			mOLastStep = default(StepResult);

			if (IsRemoved || mIWorld == null)
				return mOLastStep;

			UWCreatureMotion lOMotion = mIWorld.Motion;

			if (lOMotion == null)
				return mOLastStep;

			// The interval that has just elapsed: the picture glides over it.
			int liInterval = mORecord.Interval;

			mOLastStep = lOMotion.Step(mORecord, miIndex, fMotionKind());

			// A LYING OBJECT in the collision scan carries a transient index of the world (4096 and
			// up); the brain's door actions and GetObject want the level's master-list index, as a
			// creature's index is. Found 2026-10-05 (goblins stood at closed doors without opening
			// them: UseDoor on the transient index found no door).
			if (mOLastStep.HitObject && mIWorld.TryGetStaticObject(mOLastStep.HitObjectIndex, out UWObject lOHit))
				mOLastStep.HitObjectIndex = lOHit != null ? fIndexOf(lOHit) : 0;

			if (mOLastStep.TileChanged)
				mIWorld.MoveObjectData(mONpc, mORecord.TileX, mORecord.TileY);

			if (mOLastStep.LavaBurn)
				fDamage(mIView.DamageTarget, UWBlow.Lava(1));

			if (mOLastStep.Collided)
				fTrace("step: collided{0}{1}{2}{3}{4}", mOLastStep.HitClosedDoor ? ", closed door" : mOLastStep.HitObject ? " with object " + mOLastStep.HitObjectIndex : "",
					mOLastStep.HeadingDeflected ? ", deflected to " + mORecord.FineHeading : "", mOLastStep.Stuck ? ", stuck" : "",
					mOLastStep.Drowned ? ", drowned" : "", mOLastStep.TouchedCeiling ? ", ceiling" : "");

			mIView.PlaceBody(liInterval);

			return mOLastStep;
		}

		private UWCreatureMotion.Kind fMotionKind()
		{
			return mbHasStats ? UWCreatureMotion.KindOf(mOStats) : UWCreatureMotion.Kind.Land;
		}

		// The random source, the clock and the kin alarm are the session's (ICritterServices, handed
		// to the brain in the constructor since cleanup step 4.13, 2026-10-19): the session's generator
		// (cleanup step 2.8, 2026-10-14) - one sequence for the brain, combat, loot and the motion core,
		// as the original has one rand.

		// ------------------------------------------------- ICritterHost: the level

		int ICritterHost.FloorLevelAt(int piTileX, int piTileY)
		{
			UWTile lOTile = fGetTileAt(piTileX, piTileY);

			return lOTile != null ? lOTile.FloorHeight >> 4 : 0;
		}

		/// <summary>The mind's question before a cast: the anti-magic bit alone, the Void left to the
		/// cast's refusal (UWEndgameRules.IsAntiMagicTile, cleanup step 4.10).</summary>
		bool ICritterHost.IsMagicBlockedAt(int piTileX, int piTileY)
		{
			IUWWorld lOWorld = World;

			return lOWorld != null && UWEndgameRules.IsAntiMagicTile(lOWorld.Level, new UWTilePos(piTileX, piTileY));
		}

		/// <summary>Tybal's rule: his lair, level 7, while the orb stands (UWTybalOrbRules, the bit
		/// PLAYER.DAT 0x60 bit 5). Until 2026-09-23 this asked for level index 7 - level 8 - and
		/// never for the bit.</summary>
		int ICritterHost.DungeonLevel => World != null ? World.LevelIndex + 1 : 0;

		bool ICritterHost.TybalOrbStands => World != null && UWTybalOrbRules.OrbStands(World.LevelIndex);

		// ------------------------------------------------- ICritterHost: other objects

		CritterTarget ICritterHost.GetObject(int piIndex)
		{
			return fGetObject(piIndex);
		}

		private CritterTarget fGetObject(int piIndex)
		{
			if (piIndex == PlayerIndex)
				return mIWorld != null ? mIWorld.SnapshotPlayer() : new CritterTarget();

			return fSnapshotObject(piIndex);
		}

		/// <summary>The remote camera's tile while it shows (camera trap, Roaming Sight), else the
		/// player's - see ICritterHost.ViewTile.</summary>
		UWTilePos ICritterHost.ViewTile => mIWorld != null ? mIWorld.ViewTile : new UWTilePos(-100, -100);

		/// <summary>Any other object of the master list by index: a creature from its actor's
		/// record and table row, a door or item from its data.</summary>
		private CritterTarget fSnapshotObject(int piIndex)
		{
			CritterTarget lOTarget = new CritterTarget();
			UWObject lOObject = fObjectAt(piIndex);

			if (lOObject == null)
				return lOTarget;

			UWCreatureActor lOOther = mIWorld.ActorOf(lOObject);

			if (lOOther != null)
			{
				UWCritterRecord lORecord = lOOther.mORecord;

				lOTarget.Exists = !lOOther.IsRemoved;
				lOTarget.Index = piIndex;
				lOTarget.IsCreature = true;
				lOTarget.ItemId = lORecord.ItemId;
				lOTarget.X = lORecord.X;
				lOTarget.Y = lORecord.Y;
				lOTarget.ZPos = lORecord.ZPos;
				lOTarget.TileX = lORecord.TileX;
				lOTarget.TileY = lORecord.TileY;
				lOTarget.FacingEighth = lORecord.FacingEighth;
				lOTarget.HitPoints = lOOther.IsDead ? 0 : lORecord.HitPoints;
				lOTarget.Goal = lORecord.Goal;
				lOTarget.IsAlly = lORecord.IsAlly;

				lOOther.GetOwnSize(out int liRadius, out int liHeight);
				lOTarget.ObjectHeight = liHeight;

				if (lOOther.mbHasStats)
				{
					lOTarget.Loudness = lOOther.mOStats.LoudnessAsTarget;
					lOTarget.Visibility = lOOther.mOStats.VisibilityAsTarget;
					lOTarget.Kind = lOOther.mOStats.GeneralType;
				}

				return lOTarget;
			}

			lOTarget.Exists = true;
			lOTarget.Index = piIndex;
			lOTarget.ItemId = lOObject.ID & 0x1FF;
			lOTarget.IsCreature = UWObjectIds.IsCreature(lOObject.ID);
			lOTarget.TileX = lOObject.TileX;
			lOTarget.TileY = lOObject.TileY;
			lOTarget.X = (lOObject.TileX << 3) + (lOObject.XPos & 7);
			lOTarget.Y = (lOObject.TileY << 3) + (lOObject.YPos & 7);
			lOTarget.ZPos = lOObject.ZPos & 0x7F;
			lOTarget.FacingEighth = lOObject.Heading & 7;
			lOTarget.HitPoints = 0;

			return lOTarget;
		}

		private UWObject fObjectAt(int piIndex)
		{
			UWLevel lOLevel = fLevel();

			if (lOLevel == null || piIndex <= 0)
				return null;

			List<UWObject> lOList = lOLevel.Masterlist;

			return lOList != null && piIndex < lOList.Count ? lOList[piIndex] : null;
		}

		private int fIndexOf(UWObject pOObject)
		{
			UWLevel lOLevel = fLevel();

			if (pOObject == null || lOLevel == null)
				return 0;

			int liIndex = lOLevel.Masterlist.IndexOf(pOObject);

			return liIndex < 0 ? 0 : liIndex;
		}

		/// <summary>
		/// TestBetweenPoints (spec 3.1) over the tile map - the rule is engine-free in
		/// UWTilePath.TestBetweenPoints since 2026-09-23, with the heights and the closed sides of
		/// every tile the line passes, the diagonals included (deviation 23 closed). Doors are
		/// objects and do not block - that is how the goblin in front of the storage room notices a
		/// theft through a closed door (per user, 2026-09-16).
		/// </summary>
		bool ICritterHost.HasLineOfSight(int piX0, int piY0, int piZ0, int piX1, int piY1, int piZ1)
		{
			return fHasLineOfSight(piX0, piY0, piZ0, piX1, piY1, piZ1);
		}

		private bool fHasLineOfSight(int piX0, int piY0, int piZ0, int piX1, int piY1, int piZ1)
		{
			UWLevel lOLevel = fLevel();

			if (lOLevel == null)
				return false;

			return UWTilePath.TestBetweenPoints(lOLevel.TileData, piX0, piY0, piZ0, piX1, piY1, piZ1);
		}

		// ------------------------------------------------- ICritterHost: movement

		/// <summary>
		/// seg006_1477_1938: the straight tile line from the creature's tile to the destination,
		/// walked one axis at a time (the original tests the three tiles around every step of a
		/// Bresenham walk), each step through the creature's own passability - walker, flier or
		/// swimmer, water, lava against the range budget, closed doors when it could open them.
		/// </summary>
		/// <summary>
		/// The straight tile line seg006_1477_1938 of the original (UWTileRoute.StraightLine with
		/// this creature's triple test, since 2026-10-05): every tile of the line tested as the
		/// middle of a triple, the destination included, with budget 0 - so water on the way or at
		/// the destination fails it for a land creature, a drop of two levels fails it, a closed
		/// door across the way fails it. Until then the host's UWTilePath walked the line with its
		/// own per-tile rules (deviation 14).
		/// </summary>
		bool ICritterHost.IsStraightLineClear(int piToTileX, int piToTileY)
		{
			if (!fPrepareRoute())
				return false;

			UWTile lOTile = fGetTileAt(mORecord.TileX, mORecord.TileY);
			int liStart = lOTile != null ? lOTile.FloorHeight >> 4 : mORecord.FloorLevel;

			// CLEAR ONLY WHEN CLEAR (crit_head_for_loc: beeline(...) == 1): the line to the own tile
			// (LineSameTile, -1) is none, which a creature arriving home meets (A20, 2026-10-15).
			return mORoute.StraightLine(mORecord.TileX, mORecord.TileY, piToTileX, piToTileY, liStart, mOTraverse.Triple)
				== UWTileRoute.LineClear;
		}

		private UWTileRoute mORoute;

		private UWTileTraverse mOTraverse;

		/// <summary>The level's shared route and triple test with this creature's context: the
		/// handler masks of its kind, its COMOBJ height, its jump bit (table byte 0x0A bit 5).</summary>
		private bool fPrepareRoute()
		{
			UWTileTraverse lOTraverse = mIWorld != null ? mIWorld.Traverse : null;

			if (lOTraverse == null)
				return false;

			mORoute = mIWorld.Route;
			mOTraverse = lOTraverse;

			GetOwnSize(out int liRadius, out int liHeight);

			mOTraverse.SetCreature(fMotionKind(), liHeight,
				mbHasStats && (mOStats.RowByte(0x0A) & UWTileTraverse.JumpRowBit) != 0);

			return true;
		}

		/// <summary>
		/// PathFindBetweenTiles of the original (flood_path; UWTileRoute.FindPath with this creature's
		/// triple test, since 2026-10-05): the breadth-first search in the box with the range budget,
		/// the goal next to the start accepted untested, a farther goal tested as the middle of the
		/// final triple; the squares and their jump flags go to the brain, which stores them in one of
		/// the level's sixteen slots and follows them (since 2026-10-15; until then this answered the
		/// next tile and reused it for half a second, deviation 14). Every closed door blocks by its
		/// orientation whatever the creature's door skill (UWTileTraverse), as in the original.
		/// </summary>
		bool ICritterHost.TryFindPath(int piFromX, int piFromY, int piFromHeight, int piToTileX, int piToTileY, int piToHeight,
			int piRangeBudget, List<UWTilePos> pOSquares, List<bool> pOJumps)
		{
			pOSquares.Clear();
			pOJumps.Clear();

			if (!fPrepareRoute())
				return false;

			if (!mORoute.FindPath(piFromX, piFromY, piFromHeight, piToTileX, piToTileY, piToHeight,
				piRangeBudget, mOTraverse.Triple) || mORoute.Tiles.Count < 2)
				return false;

			pOSquares.AddRange(mORoute.Tiles);
			pOJumps.AddRange(mORoute.Jumps);

			return true;
		}

		// The level's sixteen stored creature paths are the session's, cleared with every level build
		// (ICritterServices.PathSlots since cleanup step 4.13, 2026-10-19).

		/// <summary>
		/// Goal 3's relocation: the record onto the tile centre at floor height, relinked, the body
		/// there at once. UW.EXE checks nothing; here a solid tile or one outside the map refuses
		/// (deviation 37, kept for safety) - a creature inside a wall would be worse than a skipped
		/// jump.
		///
		/// ONE WRITE, HERE (cleanup step 4.8, 2026-10-18): seg007_1798_3E4 (UnderworldExhumed AI.C)
		/// moves the follower to the middle of the tile at the tile's height times eight, a flier too.
		/// Until then this wrote the record with a flier's height above the floor kept and the brain
		/// wrote it again at floor height after the call, so the record ended at the floor while the
		/// body showed the flier at its old height above it until the next step.
		/// </summary>
		bool ICritterHost.Teleport(int piTileX, int piTileY)
		{
			UWTile lOTile = fGetTileAt(piTileX, piTileY);

			if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
				return false;

			if (!mbFlying && mbSwimming != fIsWater(lOTile))
				return false;

			fRelocate(piTileX, piTileY, 4, 4, (lOTile.FloorHeight >> 4) << 3);
			fTrace("teleported to {0}/{1}", piTileX, piTileY);

			return true;
		}

		/// <summary>The record onto a tile, eighth and height, the tile list relinked, the body
		/// there at once.</summary>
		private void fRelocate(int piTileX, int piTileY, int piFineX, int piFineY, int piZPos)
		{
			mORecord.TileX = piTileX;
			mORecord.TileY = piTileY;
			mORecord.FineX = piFineX;
			mORecord.FineY = piFineY;
			mORecord.ZPos = piZPos;

			if (mIWorld != null)
				mIWorld.MoveObjectData(mONpc, piTileX, piTileY);

			mIView.SetBodyNow();
		}

		// ------------------------------------------------- ICritterHost: doors (spec 7.5)

		/// <summary>The door of a door object in the master list, through the world's doors.</summary>
		private IUWDoor fDoorAt(int piDoorIndex)
		{
			UWObject lODoorData = fObjectAt(piDoorIndex);
			IUWDoors lODoors = lODoorData != null && World != null ? World.Doors : null;

			return lODoors != null ? lODoors.Find(lODoorData) : null;
		}

		/// <summary>ObjectUse on the door: opens it unless it is locked or spiked (UWDoorRules.Use
		/// with the creature as the user - UseRect refuses a creature the spiked door, the player
		/// not). True when it is open afterwards.</summary>
		bool ICritterHost.UseDoor(int piDoorIndex)
		{
			IUWDoor lODoor = fDoorAt(piDoorIndex);

			if (lODoor == null)
				return false;

			if (!lODoor.IsClosed)
				return true;

			bool lbSpiked = lODoor.Data != null && UWObjectMechanics.IsDoorSpiked(lODoor.Data);

			if (UWDoorRules.Use(true, lbSpiked, lODoor.LockState.IsLocked, false, false) != UWDoorRules.UseOutcome.Toggle)
				return false;

			lODoor.Toggle(UWTrapRules.Setter.Creature);
			fTrace("opened door {0}", piDoorIndex);

			return !lODoor.IsClosed;
		}

		/// <summary>
		/// UnlockDoor_seg040_352B_1D3B with minus the skill: a lockpick attempt by the creature,
		/// UWLockRules.Unlock with the pick path - read 2026-09-25: the skill against three times
		/// the lock difficulty, and a lock without its keep bit is gone afterwards.
		/// A SUCCESS ONLY UNLOCKS (try_to_open_door and checkLock, UnderworldExhumed PATHFIND.C and
		/// USEITEMS.C): no sound, and the door stays shut until the creature's next bump uses it.
		/// Until 2026-10-15 ours played the lockpick sound and opened the door at once (audit round
		/// 2, A19 / KR D19). checkLock's door trap (checkTrap with trigger type 6, an unlock trigger)
		/// has nothing to fire: no level of the game holds one (TRIAGE B12, 2026-10-15).
		/// </summary>
		void ICritterHost.PickDoor(int piDoorIndex, int piSkill)
		{
			IUWDoor lODoor = fDoorAt(piDoorIndex);

			if (lODoor == null || !lODoor.LockState.IsLocked)
				return;

			bool lbUnlocked = UWLockRules.Unlock(lODoor.LockState, null, null, !lODoor.IsClosed, -piSkill)
				== UWLockRules.Outcome.Unlocked;

			fTrace("picks door {0}: {1}", piDoorIndex, lbUnlocked ? "unlocked" : "failed");
		}

		/// <summary>DamageObject type 4 on the door: its UWDamageable takes the bash - the spike of
		/// a spiked door first (UWDoor.TryDamageSpike through UWDamageable.TakesBlowFirst, B4) - and
		/// UWDoor bursts it open at zero.</summary>
		void ICritterHost.BashDoor(int piDoorIndex, int piDamage)
		{
			UWObject lODoorData = fObjectAt(piDoorIndex);

			if (lODoorData == null || mIWorld == null)
				return;

			UWMeleeContact lODoor = mIWorld.DoorContact(lODoorData);

			if (lODoor.Kind == UWMeleeContactKind.Nothing)
				return;

			fTrace("bashes door {0} for {1}", piDoorIndex, piDamage);
			mIWorld.ShowDoorBashed(lODoor, piDamage << 2);
			fDamage(lODoor.Target, UWBlow.CreatureBlow(piDamage, miIndex));
		}

		// ------------------------------------------------- Missiles: the velocity table

		/// <summary>The velocities of the ammunition table, entry n for object 0x10 + n (byte 1 of
		/// the 16 x 3 table, UWObjectProperties' ranged speed), 15 each without data - for the
		/// brain's constructor since cleanup step 4.13 (2026-10-19; ICritterHost.MissileVelocity
		/// asked the data per shot until then).</summary>
		private int[] fMissileVelocities()
		{
			int[] liVelocities = new int[UWObjectProperties.RangedObjectCount];
			DataImport lOData = fData();

			for (int liAmmo = 0; liAmmo < liVelocities.Length; liAmmo++)
			{
				liVelocities[liAmmo] = lOData == null || lOData.ObjectProperties == null
					? 15
					: lOData.ObjectProperties.GetRangedSpeed(FirstAmmunitionObjectId + liAmmo);
			}

			return liVelocities;
		}

		// ------------------------------------------------- ICritterHost: events

		void ICritterHost.OnSwingStarted(int piAttack)
		{
			miAttackIndex = Math.Clamp(piAttack, 0, 2);
			fEvent("swing {0} started", piAttack);
		}

		/// <summary>
		/// The blow at frame 4 - NPCExecuteAttack_seg022_15DE (83437), spec 8.3. The reach scan
		/// of CheckForAttackHit_seg022_466 (80356) is a collision scan from the attacker along
		/// its facing over 2 + 3 = 5 eighths with radius 2 + 1 = 3, and the NEAREST collision is
		/// the defender - not necessarily the goal target. The host does it as a capsule probe
		/// over the body height (the world's FindMeleeDefender): the player, a creature, or
		/// something of the world. Nothing in reach is a plain miss with sound 0x0A at the
		/// attacker (label C34); a wall in the way gets the impact animation as well
		/// (SpawnImpactAnimo). The arithmetic - score, skill check, critical, dice, charge, flank,
		/// armour, difficulty - is UWAttackResolution's. Creature against creature is refused for
		/// the same ally bit (labels DBB-E19). A miss with 0 damage still reaches the defender's
		/// OnDamaged, so it remembers the attacker.
		/// </summary>
		void ICritterHost.OnBlow(int piTargetIndex, int piAttack, int piCharge, int piSwingType, int piFlank)
		{
			miAttackIndex = Math.Clamp(piAttack, 0, 2);

			UWMeleeContact lOContact = mIWorld != null ? mIWorld.FindMeleeDefender(this) : UWMeleeContact.Nothing;

			switch (lOContact.Kind)
			{
				// A DOOR NEARER THAN THE BODY THE PROBE MET IS THE DEFENDER (per user, 2026-09-29), and
				// AN OBJECT IN THE WAY IS THE DEFENDER (per user, 2026-09-29) - see
				// UWCreatureWorld.FindMeleeDefender.
				case UWMeleeContactKind.Object:
					fStrikeObject(lOContact, piAttack, piCharge, piFlank);

					return;

				case UWMeleeContactKind.Nothing:
					fEvent("blow {0}: nothing in reach", piAttack);
					mIView.PlayMissSound();

					return;

				case UWMeleeContactKind.Player:
					fStrikePlayer(piAttack, piCharge, piFlank);

					return;

				case UWMeleeContactKind.Creature:
					fStrikeCreature(lOContact.Creature, piAttack, piCharge, piFlank);

					return;
			}

			// The world is in the way: the impact on it, then the miss (CheckForAttackHit's wall
			// branch and CombatMissImpactSound(0)). The lurker in the water striking at the bank
			// shows the same flash as the player's blow on a barrel (per user, 2026-09-16).
			fEvent("blow {0}: hits the world at {1}", piAttack, lOContact.Name);
			mIWorld.ShowBlowOnWorld(this, lOContact);
			mIView.PlayMissSound();
		}

		/// <summary>
		/// A blow on an object that is no creature - a door, a chest, a barrel: no check
		/// (CalculateAttackResults_seg022_230E_6B9 rolls none against an object) and no armour
		/// (AttackerAppliesFinalDamage_seg022_8A5 subtracts it only for creatures); the rolled
		/// damage, charge and flank go into DamageObject with type 4. A door without hit points
		/// (the portcullis) takes the blow and nothing happens to it. The numbers are
		/// UWAttackResolution's, a thing as the defender (cleanup step 4.9, 2026-10-18). The impact
		/// and the sound are the world's (UWCreatureWorld.ShowBlowOnObject).
		/// </summary>
		private void fStrikeObject(in UWMeleeContact pOContact, int piAttack, int piCharge, int piFlank)
		{
			UWAttackOutcome lOOutcome = UWAttackResolution.Resolve(fGetAttacker(piAttack, piCharge, piFlank),
				UWAttackResolution.ObjectDefender, UWAttackResolution.NoPart);

			fEvent("blow {0}: hits {1} for {2}", piAttack, pOContact.Name ?? "an object", lOOutcome.Rolled);

			mIWorld.ShowBlowOnObject(this, pOContact, lOOutcome.HitVolume);

			if (pOContact.TakesBlows)
				fDamage(pOContact.Target, lOOutcome.Blow);
		}

		/// <summary>The score and the base damage of this attack (NPCExecuteAttack labels
		/// 1629-1679): the table's to-hit and damage bytes of attack i, half the signed byte
		/// 0x11 and a fifth of byte 5, the strong individual's 7 + RNG(6) and 4 + RNG(12) - with the
		/// charge, the flank, this creature's index and its row's kinds and poison as one attacker for
		/// UWAttackResolution (UWAttackerStats.Creature, cleanup step 4.9, 2026-10-18;
		/// fGetAttackNumbers until then, the same draws).</summary>
		private UWAttackerStats fGetAttacker(int piAttack, int piCharge, int piFlank)
		{
			return UWAttackerStats.Creature(mOStats, mbHasStats, piAttack, mbHasStats && mORecord.IsStrong, miIndex,
				piCharge, piFlank);
		}

		/// <summary>This creature's blow on a body - the player or another creature - whose feet and
		/// top (zpos) are given: the ninth, the strong bonus, the part, the check, in the original's
		/// order (UWAttackResolution.CreatureBlow, cleanup step 4.9b, 2026-10-18).</summary>
		private UWAttackOutcome fStrikeBody(int piAttack, int piCharge, int piFlank, int piOwnHeight, IUWAttackDefender pIDefender,
			int piDefenderFeet, int piDefenderTop, out UWAttackerStats pOAttacker)
		{
			return UWAttackResolution.CreatureBlow(mOStats, mbHasStats, piAttack, mbHasStats && mORecord.IsStrong, miIndex,
				piCharge, piFlank, mORecord.ZPos, piOwnHeight, pIDefender, piDefenderFeet, piDefenderTop, out pOAttacker);
		}

		/// <summary>
		/// The blow against the player, in the original's order (CalculateAttackResults 80688,
		/// AttackerAppliesFinalDamage 81050): the part from the heights, the enchantment
		/// protection of that part off the score, the skill check against his defence with
		/// the flank bonus; a critical doubles the base 1 in 2 and flashes red (0xB8); the dice,
		/// the charge over 128 plus the flank; the hit sound with THAT value; his armour of the
		/// part; half on the easy difficulty; then the damage, the shake (in the vitals) and
		/// the poison. A miss is DamageObject with 0 on the player, i.e. nothing.
		/// </summary>
		private void fStrikePlayer(int piAttack, int piCharge, int piFlank)
		{
			if (!mIWorld.TryGetPlayerAsDefender(out IUWAttackDefender lIPlayerBody, out int liPlayerFeet, out int liPlayerHeight))
				return;

			GetOwnSize(out int liOwnRadius, out int liOwnHeight);

			// THE PART FROM THE BLOW'S PROBE, a random third of this body (critter_attack's wsize 2
			// and the ninth rand() % 9 from AI.C, into resolve_attack and pickloc) - C3, by the user's
			// decision of 2026-10-12; until then this whole body, which sent tall creatures' blows
			// at the head far less often than the original does.
			//
			// THE CHECK, THE CRITICAL AND THE DICE are UWAttackResolution's since cleanup step 4.9
			// (2026-10-18), the player's defence and protection of the part UWPlayerCombatant's
			// (UWCharacter.Combatant): the check value is his row byte 0x12, his protection of the
			// part comes off the score; a critical doubles the base 1 in 2 and wears the piece of the
			// part (UWPlayerCombatant.OnCriticalHit) before the dice.
			//
			// IN THE ORIGINAL'S ORDER since cleanup step 4.9b (2026-10-18, by the user's decision): the
			// ninth, the strong creature's bonus, the part, the check (UWAttackResolution.CreatureBlow;
			// ours drew the part before the strong bonus until then).
			UWAttackOutcome lOOutcome = fStrikeBody(piAttack, piCharge, piFlank, liOwnHeight, lIPlayerBody, liPlayerFeet,
				liPlayerFeet + liPlayerHeight, out UWAttackerStats lOAttacker);
			int liPart = lOOutcome.Part;

			if (!lOOutcome.Hit)
			{
				fEvent("blow {0} {1} (score {2} + flank {3} vs defence {4})", piAttack,
					lOOutcome.Result == UWSkillCheck.ResultEnum.CriticalFailure ? "missed badly" : "missed",
					lOAttacker.Score, piFlank, lIPlayerBody.Defence);

				// Blocked: 7 or 8 at the player, by this creature's weapon kind and the piece he wears
				// on the struck part (UWCritterCombat.GetBlockedSound, through UWAttackResolution).
				mIWorld.ShowBlowOnPlayer(lOOutcome);

				// A MISS STILL STARTS THE COMBAT MUSIC: do_attack hands the player damage_item with
				// 0 (COMBAT.C), and damage_critter picks MUSIC_DANGER or MUSIC_COMBAT for the player
				// whenever the attacker is known (critter/AI.C). Until 2026-10-12 only a hit did.
				// Through the world as the miss's request since cleanup step 4.2 (2026-10-17;
				// UWDamageRequest.CreatureMiss, the outcome's blow since step 4.9).
				fDamage(UWDamageTarget.ThePlayer, lOOutcome.Blow);

				return;
			}

			// THE RED FLASH (0xB8) comes with the damage request (UWDamageRequest.CreatureBlow,
			// cleanup step 4.2, 2026-10-17); it was flashed here until then.

			// Sound 3 at the avatar, volume damage * 4 - before the armour, so an absorbed blow
			// still sounds (label 938).
			mIWorld.ShowBlowOnPlayer(lOOutcome);

			fEvent("blow {0} {1} part {2} for {3} before the armour (charge {4}, flank {5})", piAttack,
				lOOutcome.Critical ? "critical" : "hit", liPart, lOOutcome.Rolled, piCharge, piFlank);

			// A BLOW IS BLUNT DAMAGE: DamageObject with type 4 (label A3D). The armour of the part, half
			// on the easy difficulty, the combat shake from the damage before the Easy halving (C5,
			// 2026-10-12), the critical's flash and the combat music - all in the damage pipeline
			// since cleanup step 4.2 (2026-10-17; UWDamageRequest.CreatureBlow, which the outcome's
			// blow carries since step 4.9).
			fDamage(UWDamageTarget.ThePlayer, lOOutcome.Blow);

			fTryPoisonPlayer(lOAttacker, lOOutcome);
		}

		/// <summary>
		/// The blow against another creature: refused for the same ally bit (ExecuteAttack
		/// labels DBB-E19), else the same path as against the player without enchantment
		/// protection, flash, difficulty or poison: the skill check against its row byte 0x12,
		/// the dice, the charge and flank, sound 4 at the object with the pre-armour value, the
		/// armour of the part with the strong defender's 5/3, then DamageObject charged to this
		/// creature's index - with 0 on a miss, so the defender still records the attacker. A
		/// damaging hit leaves the blood splat or the flash at HitZ of the part (labels B49-C17).
		///
		/// A DYING CREATURE IS STRUCK TOO (cleanup step 4.12b, 2026-10-19, the user's decision "as the
		/// original"): do_attack, resolve_attack, set_hitobj and critter_attack (UnderworldExhumed
		/// combat/COMBAT.C:133-184, 232-272, 491-507, 788-812) have no test for a dying target, whose
		/// record stays in its tile list until frame 3 (critter/AI.C:1140) - the check, the sound, the
		/// damage call and the blood or flash as on a living one; damage_critter's crit_die starts no
		/// second death (AI.C:1438, the brain's fDeath). Ours passed over a dying target until then. Only
		/// a body that is gone (Removed) takes nothing (TakesBlows). The damage call itself ends in the
		/// world's routing, which let no blow through to a committed body until cleanup step 4.12c
		/// (2026-10-19); since then damage_critter's part runs on the dying one too (TakesBlows).
		/// </summary>
		private void fStrikeCreature(UWCreatureActor pOOther, int piAttack, int piCharge, int piFlank)
		{
			if (pOOther == null || pOOther.mOHealth == null || !pOOther.TakesBlows)
				return;

			int liTargetIndex = pOOther.miIndex;

			// The same side is spared (UWAttackResolution.SparesSameSide, do_attack).
			if (UWAttackResolution.SparesSameSide(false, UWDefenderKind.Creature, mORecord.IsAlly, pOOther.mORecord.IsAlly))
			{
				fEvent("blow {0} refused: same side as {1}", piAttack, liTargetIndex);

				return;
			}

			GetOwnSize(out int liOwnRadius, out int liOwnHeight);

			// THE PART BEFORE THE CHECK, as the original's resolve_attack picks it before frp_check
			// (since cleanup step 4.9b, 2026-10-18, by the user's decision; ours rolled the check first
			// and the probe and the part after it until then): the blow's probe, a random third of this
			// body (C3, 2026-10-12, as in fStrikePlayer), the strong creature's bonus, the part, then the
			// check (UWAttackResolution.CreatureBlow). A hit: the critical, the dice, the charge and the
			// flank, then the armour of the part with the strong defender's 5/3 (pOOther as
			// IUWAttackDefender).
			pOOther.GetOwnSize(out int liOtherRadius, out int liOtherHeight);

			UWAttackOutcome lOOutcome = fStrikeBody(piAttack, piCharge, piFlank, liOwnHeight, pOOther, pOOther.mORecord.ZPos,
				pOOther.mORecord.ZPos + liOtherHeight, out _);
			int liPart = lOOutcome.Part;
			UWSkillCheck.ResultEnum leResult = lOOutcome.Result;

			// Sound 4 at the defender on a hit; blocked: 7 or 8 at the defender - the armour kind read
			// from THIS creature's row, as do_miss does (UWCritterCombat.GetBlockedSound, through
			// UWAttackResolution).
			pOOther.mIView.PlayStruckSound(lOOutcome);

			fEvent("blow {0} at creature {1}: {2} part {3} for {4}", piAttack, liTargetIndex, leResult, liPart, lOOutcome.Damage);

			// Charged to this creature (UWBlow.CreatureBlow), 0 on a miss.
			fDamage(pOOther.mIView.DamageTarget, lOOutcome.Blow);

			if (lOOutcome.LeavesTrace)
				pOOther.mIView.ShowHitEffect(liPart, lOOutcome.Damage, 0);
		}

		/// <summary>
		/// The poison of a hit on the player (NPCExecuteAttack labels 16A4-16EC, deviation 48):
		/// after ExecuteAttack returned 1 - a hit, even one the armour swallowed - his poison
		/// nibble must be below the row's byte 0x0F and a Poison Resistance must not scale a
		/// test damage of type 0x10 to zero; then the nibble becomes byte 0x0F &amp; 0xF. No
		/// roll, no armour hurdle. (UWAttackResolution.PoisonNibble since cleanup step 4.9,
		/// 2026-10-18, with the row's byte in the attacker.)
		/// </summary>
		private void fTryPoisonPlayer(in UWAttackerStats pOAttacker, in UWAttackOutcome pOOutcome)
		{
			IUWPlayerAvatar lOPlayer = World != null ? World.Player : null;
			UWPlayerVitals lOVitals = lOPlayer != null ? lOPlayer.Vitals : null;

			if (lOVitals == null)
				return;

			int liNibble = UWAttackResolution.PoisonNibble(pOAttacker, pOOutcome, lOVitals.Poison, lOPlayer.DamageTypeProof);

			if (liNibble < 0)
				return;

			fEvent("poisons the player with {0}", liNibble);
			lOVitals.ApplyPoison(liNibble);
		}

		// ------------------------------------------------- IUWAttackDefender (cleanup step 4.9)

		/// <summary>This creature as the defender of a blow (UWAttackResolution, cleanup step 4.9,
		/// 2026-10-18): a creature.</summary>
		UWDefenderKind IUWAttackDefender.DefenderKind => UWDefenderKind.Creature;

		/// <summary>Its row byte 0x12 (frp_check's cr->defence) - the body's Defence without table
		/// data, as the creature's blow read it (the player's blow read the body's, which the spawner
		/// fills from the same byte).</summary>
		int IUWAttackDefender.Defence => mbHasStats ? mOStats.DefensePower : (mOHealth != null ? mOHealth.Defence : 0);

		/// <summary>A creature has no enchantment protection (frp_check asks the player's only).</summary>
		int IUWAttackDefender.ProtectionAt(int piPart) => 0;

		/// <summary>GetArmourOfPart. A part that was never picked (the player's blow without a
		/// character to measure the probe from) keeps the body's one value, as that blow did.</summary>
		int IUWAttackDefender.ArmourAt(int piPart)
		{
			if (piPart < 0)
				return mOHealth != null ? mOHealth.Armour : 0;

			return GetArmourOfPart(piPart);
		}

		/// <summary>Never asked: do_miss reads the ATTACKER's row for a creature as target.</summary>
		bool IUWAttackDefender.IsArmourMetalAt(int piPart) => false;

		/// <summary>Nothing of a creature wears on a critical hit (frp_check: the player only).</summary>
		void IUWAttackDefender.OnCriticalHit(int piPart)
		{
		}

		/// <summary>The part a blow whose probe spans these heights (zpos) hits on this body -
		/// pickloc (UWArmourProtection.PickBodyPart); the player's blow asks it with its probe.</summary>
		public int PickPartHitBy(int piProbeBottom, int piProbeTop)
		{
			return fPickPartHitBy(piProbeBottom, piProbeTop);
		}

		/// <summary>PickBodyHitPoint over this body and the attacker's heights in zpos.</summary>
		private int fPickPartHitBy(int piAttackerFeet, int piAttackerTop)
		{
			GetOwnSize(out int liRadius, out int liHeight);

			return UWArmourProtection.PickBodyPart(mORecord.ZPos, mORecord.ZPos + liHeight, piAttackerFeet, piAttackerTop);
		}

		/// <summary>The armour of one part of this creature: row[part % 4], a 0xFF byte falling
		/// back to byte 0, times 5 / 3 for a strong individual (AttackerAppliesFinalDamage labels
		/// 990-A02). A creature without table data has none.</summary>
		public int GetArmourOfPart(int piPart)
		{
			return mbHasStats ? UWCritterCombat.GetCreatureArmour(mOStats, piPart, mORecord.IsStrong) : 0;
		}

		/// <summary>
		/// The part a missile from these heights (zpos, the projectile's z and top) strikes on this
		/// body: the part PickBodyHitPoint picks, + 4 for a missile (MissileAttackHit label 158B).
		/// Its armour (GetArmourOfPart, this creature as IUWAttackDefender) replaces the
		/// UWDamageable's single value for projectiles (UWMissiles.StrikeCreature) - deviation 49.
		/// PICKED ONCE for the armour and the effect's place, as missile_thwack picks hitloc once
		/// (UnderworldExhumed combat/COMBAT.C:772; cleanup step 4.9b, 2026-10-18, by the user's
		/// decision to follow the original): until then GetArmourAgainst and ShowHitEffect each
		/// picked it, and the blood could sit at another part than the one whose armour counted.
		/// </summary>
		public int PickMissilePart(int piMissileZ, int piMissileTop)
		{
			return UWCritterCombat.GetMissileBodyPart(fPickPartHitBy(piMissileZ, piMissileTop));
		}

		// ------------------------------------------------- Missiles and spells

		/// <summary>
		/// NPCMissileLaunch at frame 4 of animation 5: the ammunition of the table (object 0x10 +
		/// ammo) from the creature's eye, aimed at its target. The damage is rolled with charge
		/// 0x80 and flank 0 (deviation 46). The original computes the pitch from the height
		/// difference over the missile's velocity; the port aims the same way it did before -
		/// straight at the target with the drop of a ballistic missile added - so the brain's
		/// pitch is only traced here (TODO: use it once the projectile flies in the original.s units);
		/// a SPELL missile uses it since 2026-09-27, see fGetSpellLaunch.
		/// </summary>
		void ICritterHost.OnMissileLaunched(int piAmmoIndex, int piPitch)
		{
			fEvent("missile {0} launched, pitch {1}", piAmmoIndex, piPitch);

			CritterTarget lOTarget = fGetObject(mORecord.GTarg);

			if (!lOTarget.Exists)
				return;

			fLaunchProjectile(FirstAmmunitionObjectId + piAmmoIndex, UWDamageTypes.Missile, piPitch);
		}

		/// <summary>
		/// SpellTrapWandCast at frame 4 of animation 0x0D. Until 2026-09-27 ONLY THE MISSILE SPELLS
		/// WERE BUILT (major class 5): magic missile, lightning, fireball and acid cover imp,
		/// gazer, mage, fire elemental and acid slug; the area and special spells of trolls and
		/// wisps were left for the port's spell host.
		///
		/// THROUGH THE ONE EFFECT PATH since cleanup step 4.10 (2026-10-18): critter_ai casts
		/// cast(spells[i], critter, 0) (UnderworldExhumed AI.C), which is do_spell's dispatch for every
		/// caster - here UWSpellCasting.CastSpell with this creature's UWCreatureSpellHost (its record's
		/// spot and heading, and fReleaseSpellMissile as its launcher). The area spells and Summon
		/// Monster (since 2026-09-27) and the missile spells had dispatches of their own here until
		/// then, and only the first two refused in the Void (B2); do_spell's refusal now holds for all
		/// (UWEndgameRules.IsMagicBlocked), while the mind still starts a cast there (it tests the
		/// anti-magic bit alone, as the original's). The comments of the two paths it replaced:
		///
		/// A CREATURE'S AREA SPELL (2026-09-27): SpellTrapWandCast_seg038_27 hands the creature's
		/// spell to CastSpells_seg038_3307_78 with the creature as the caster, and class 6 goes to
		/// Class6Spells with it - the area lies four tiles ahead ALONG THE CREATURE'S HEADING, the
		/// hits and the damage are the player's spell's (UWAreaSpellRules.CastAt), and the damage
		/// is charged to the creature. Nothing on a no-magic tile or in the void, as CastSpells
		/// refuses there. Until then a creature's area spell did nothing at all: a summoned mage
		/// cast Flame Wind at a reaper without any effect (per user). The other classes a creature
		/// holds - heals of class 4, which CastSpells refuses without a target (SpellTrapWandCast
		/// passes none), and Light and Resist Blows, which it runs for the player only - do
		/// nothing in the original either, nor does class 0xB (Tremor, Local Teleport), which
		/// MajorSpellClassB runs for the player only. Summoning (class 8): fSummonMonster.
		///
		/// A CREATURE SUMMONS (2026-09-27, mage 106's third spell): Class8Spells with the creature
		/// as caster - see UWSummonSpellRules.SummonByCreature. The spot lies along the creature's
		/// facing from its body; the new creature is hostile and heads for the player. Nothing on a
		/// no-magic tile or in the void, as CastSpells refuses there.
		/// </summary>
		void ICritterHost.OnSpellCast(int piSpellIndex, int piPitch)
		{
			fEvent("spell {0} cast, pitch {1}", piSpellIndex, piPitch);

			IUWWorld lOWorld = World;

			if (lOWorld == null || lOWorld.Level == null || lOWorld.Data == null)
				return;

			UWCastContext lOCast = UWCastContext.Creature(Index,
				new UWSpot(new UWTilePos(mORecord.TileX, mORecord.TileY), mORecord.FineX, mORecord.FineY, mORecord.ZPos),
				mORecord.FullFacing);

			UWCreatureSpellHost lOHost = new UWCreatureSpellHost(lOWorld, lOCast,
				piProjectile => fReleaseSpellMissile(piProjectile, piPitch));

			if (!UWSpellCasting.CastSpell(piSpellIndex, lOHost))
				fEvent("spell {0}: did not take (no magic here)", piSpellIndex);
		}

		/// <summary>A spell missile of the creature (release_missile -> spell_fire): at the target
		/// along its facing at the brain's pitch, its damage type from the projectile table. Only
		/// while the creature has a target, as before cleanup step 4.10 (2026-10-18).</summary>
		private void fReleaseSpellMissile(int piProjectile, int piPitch)
		{
			CritterTarget lOTarget = fGetObject(mORecord.GTarg);

			if (!lOTarget.Exists)
				return;

			DataImport lOData = fData();

			fLaunchProjectile(piProjectile, UWRunicMagic.GetProjectileDamageType(piProjectile,
				lOData != null ? lOData.ObjectProperties : null), piPitch);
		}

		/// <summary>
		/// Sends a missile at the target along the creature's facing at the brain's pitch
		/// (GetPitchToGTarg): a mobile record on the motion core since 2026-10-05
		/// (UWMissiles.LaunchFromCreature). It starts radii + 4 eighths ahead at five sixths
		/// of the creature's height plus two per pitch step; NO ROOM there (seg025_B51: a wall, a
		/// step, a creature in melee contact) means no missile, as in the original.
		/// </summary>
		private void fLaunchProjectile(int piObjectId, int piDamageType, int piPitch)
		{
			DataImport lOData = fData();

			if (lOData == null || lOData.ObjectProperties == null)
				return;

			// The ammunition table's damage through the dice with charge 0x80 and flank 0
			// (MissileAttackHit_seg022_14BF, deviation 46); the target's armour of the part hit
			// and the difficulty are subtracted on impact (UWMissiles.StrikePlayer). The dice are
			// rolled at the hit since cleanup step 4.9b (2026-10-18, by the user's decision to follow
			// the original's missile_thwack -> do_damage; UWAttackResolution.Missile); the flight
			// carries the base.
			int liBase = lOData.ObjectProperties.GetRangedDamage(piObjectId);

			if (!mIWorld.LaunchMissile(this, piObjectId, piPitch, liBase, piDamageType, UWRunicMagic.GetProjectileImpactId(piObjectId)))
			{
				fEvent("missile {0}: no room at the start, none made", piObjectId);

				return;
			}

			miShotsFired++;
		}

		// ------------------------------------------------- Talk, sound, music

		/// <summary>
		/// Goal 10: the player looked back within reach - TalkTo, with the goal untouched (crit_talk,
		/// UnderworldExhumed AI.C): the script imports npc_goal 10 and the write-back keeps whatever it
		/// leaves (CONVVARS.C, RefreshFromData), so a script that sets no goal leaves the creature
		/// waiting to talk, and it starts again when the player faces it within reach. Until
		/// 2026-10-15 ours cleared the goal to 0 first, so such a creature stood for good (audit
		/// round 2, A13 / KR D13, GH D11).
		/// </summary>
		void ICritterHost.OnTalk()
		{
			Talk();
		}

		/// <summary>The creature's own talk (ICritterHost.OnTalk; the special deaths' talk through
		/// UWWorld.TalkTo): nothing while a conversation is open.</summary>
		public void Talk()
		{
			if (mIWorld == null || !mIWorld.CanTalk)
				return;

			fEvent("talks");
			mIWorld.Talk(mONpc);
		}

		/// <summary>LoadSoundAtCoordinate at the creature: the numbers are the original's, which
		/// UWAudioService plays directly (footsteps 1, 2, 5, 0x0D, 0x0E, 0x17; death 6).</summary>
		void ICritterHost.PlaySound(int piSound)
		{
			mIView.PlaySound(piSound);
		}

		/// <summary>ChangeThemeMusic with the original's theme numbers (5 the hurt combat theme, 6
		/// combat), and the combat music timer reset. At a swing's frame 0 the brain asks for 6
		/// even while a combat theme already plays - then only the timer is reset (53136-53160).</summary>
		void ICritterHost.PlayMusic(int piTheme)
		{
			if (mIWorld == null)
				return;

			int liPlaying = mIWorld.MusicTheme;
			bool lbCombatPlaying = liPlaying >= UWMusicSelector.FirstCombatTheme && liPlaying <= UWMusicSelector.LastCombatTheme;

			if (!(piTheme == UWCritterRules.CombatTheme && lbCombatPlaying) && World != null)
				World.Presentation.ChangeMusicTheme(piTheme);

			mIWorld.MarkCombatMusic();
		}

		// ------------------------------------------------- Death: one state machine (cleanup step 4.12)

		// THE DEATH OF A CREATURE, AS THE ORIGINAL RUNS IT (UnderworldExhumed critter/AI.C), in one
		// place since cleanup step 4.12 (2026-10-19; B22, section 7.4 of Docs/CLEANUP-PLAN.md):
		//
		//   Alive   - damage_critter takes the hit points to 0 and crit_die asks a named creature
		//             first (go_into_dying_sequence: death_check(obj, 0), UWSpecialDeaths.Stage.Dying);
		//             a refusal keeps it Alive with 0 hit points (the golem until his script sets 125).
		//   Dying   - sequence 0x0C from frame 0 at rate 4 (the brain's fDeath), shown at once (since
		//             cleanup step 4.12b, OnDeathStarted - under Freeze Time too), the experience for the
		//             player's kill (player_killed_a), the body committed (destroyed); the player's blows
		//             still land until frame 3 (the record stays in its tile list, UWCritter.Initialise;
		//             a creature's blow too since cleanup step 4.12b, fStrikeCreature - it passed over
		//             the dying one until then; their damage reaches the record since cleanup step
		//             4.12c - tally, attacker, alarm, music - where the routing dropped it, TakesBlows),
		//             and a save keeps it with 0 hit
		//             points and the sequence (UWWorldCapture). A creature saved so starts here (the
		//             constructor, B11).
		//   Removed - critter_ai's dying branch at frame 3 (AI.C:1135-1145): death_check(obj, 1),
		//             Obj_Rem, the loot, the corpse and the spill (the view's CompleteDeath, which
		//             hands them to UWCritterRemains), Obj_Free. Once; nothing runs any more.
		//
		// A drowning goes from Alive straight to Removed in one update (crit_hndlr_walk writes
		// sequence 0x0C at frame 3, PATHFIND.C, and critter_ai removes it in the same update):
		// the splash, then the commit and the end. Its hit points stay as they were (crit_hndlr_walk
		// writes no hp; since cleanup step 4.12b, 2026-10-19, ours set 0 until then). A creature of
		// goal 3 or 11 that drowns never reaches frame 3 (below): it stays Alive with its hit points
		// and sequence 0x0C, as in the original, is saved so, loads so (not as one saved while
		// dying, which needs 0 hit points), and a blow to 0 starts no death (crit_die refuses a
		// creature in sequence 0x0C, the brain's fDeath).
		//
		// THE END COMES ONLY ON THE CREATURE'S OWN UPDATES, as in the original: a dying creature far
		// from the player and the view (the brain's distance cull, critter_ai's first test) or under
		// Freeze Time (no updates at all) stays Dying until its updates run again, and then ends with
		// its special death, remains and loot. Until this step UWCritterRemains polled the animator
		// every frame and removed such a creature after three seconds of real time, with its remains
		// and loot but WITHOUT the special death (B22) - Tybal or Garamon killed far away or frozen
		// lost their story consequences.
		//
		// Goals 3 and 11 never reach frame 3: the body stays as it is, moving on with the death
		// pictures (UWCritterRules.SkipsAnimationMachine, per user in the original 2026-10-17). Until
		// then UWCritterRemains' fallback removed it after three seconds with loot and remains; since
		// 2026-10-17 it skipped them, and since cleanup step 4.12 nothing but frame 3 removes a body.

		/// <summary>
		/// A land creature ended its step in deep water (seg006_1477_476, asm lines 41417-41460):
		/// the class-7 object with offset 6 (0x1C6 "a_splash") at its position.
		///
		/// THE ONE SPLASH OF A DROWNING IS THIS ONE, the creature itself. The death removal that
		/// follows drops loot and remains as after any other death; what lands on the water tile
		/// is removed SILENTLY there (UWLevelLoader.fSpawnObjectAt), which is why the user counted
		/// exactly one splash although the goblin left nothing behind (2026-09-20). Only an object
		/// that FALLS into water splashes on landing - see Todo.md section 4.
		///
		/// The sound: the branch itself calls no LoadSoundAtCoordinate, so the number is not in
		/// the disassembly. The user heard a splash (2026-09-20), and sound 0 is the game's water
		/// sound - the one the player's own step into water plays (UWPlayerTerrain). Both are the
		/// view's (UWCritter.ShowDrowning).
		///
		/// The machine stays where it is: the brain's animation machine ends the death in the same
		/// update (OnDeathFinished), which commits the body first.
		/// </summary>
		void ICritterHost.OnDrowned()
		{
			fEvent("drowned");
			mIView.ShowDrowning();
		}

		/// <summary>
		/// Death began in the brain (animation 0x0C, frame 0, interval 4): the experience for a
		/// kill by the player (AwardKillEXP; the formula lives in UWPlayerVitals), and the
		/// UWDamageable marked destroyed (cleanup step 4.8, 2026-10-18; drained to zero until then) -
		/// so the rest of the port sees the creature as destroyed. The dragon nod comes with the
		/// kill experience (UWCharacter.OnKillRewarded).
		///
		/// Alive to Dying, once: crit_die starts no second death for one already dying (the brain's
		/// fDeath refuses it too), so a second share of experience is impossible.
		/// </summary>
		void ICritterHost.OnDeathStarted(bool pbByPlayer)
		{
			if (meDeath != UWCreatureDeathState.Alive)
			{
				fTrace("death started again while {0}: ignored", meDeath);

				return;
			}

			fEvent("dying{0}", pbByPlayer ? " by the player" : "");

			UWPlayerVitals lOVitals = pbByPlayer && mbHasStats && mIWorld != null && mIWorld.HasPlayer && World != null
				? World.Player.Vitals
				: null;

			if (lOVitals != null)
				lOVitals.AwardKillExperience(mOStats, mORecord.IsStrong, World.LevelIndex + 1);

			fCommitDeath(null);

			// THE FIRST DEATH PICTURE AT ONCE (cleanup step 4.12b, 2026-10-19; per user in the original
			// the same day): go_into_dying_sequence writes sequence 0x0C frame 0 into the record
			// (UnderworldExhumed critter/AI.C:1424-1425) and the renderer draws the record's sequence
			// and frame, whether critter_ai runs or not - a creature killed under Freeze Time shows its
			// first death picture at once and keeps it until the freeze ends. Ours showed a picture
			// only from the Motion of the creature's own updates, so until then it kept the picture
			// from before the kill (under the freeze until it ended, otherwise until its next update).
			// The record is unchanged and no draw is made: the view only shows what the brain wrote.
			mIView.ShowMotion(new UWCritterBrain.Motion(mORecord, false, false));
		}

		/// <summary>
		/// SpecialDeathCases(obj, 0) for a creature with a whoami - the moment at which the
		/// original lets a named creature refuse its own death (UWSpecialDeaths, built
		/// 2026-09-21). Only two do: the nameless 0x0B and the metal golem, who both talk instead
		/// of dying. A refusal leaves the machine Alive.
		/// </summary>
		bool ICritterHost.VetoDeath()
		{
			int liWhoami = mONpc.NPCwhoami;

			if (!UWSpecialDeaths.HasSpecialDeath(liWhoami))
				return false;

			// The world and the creature itself since cleanup step 3.6 (2026-10-17); a host of its own
			// (SpecialDeathHost, on UWCritter) until then.
			// A refused death must not kill the body (per user, 2026-09-28: the golem fell to dust
			// after his conversation). Nothing to do for it since cleanup step 4.8 (2026-10-18): the
			// body is destroyed only when the death is committed (UWHealth.MarkDestroyed), not when its
			// hit points reach 0, so the golem lives on with his record's 0, as in the original, until
			// his script sets 125. Until then the body counted its own copy, which had reached 0 when
			// the brain asked, and got at least 1 back (fKeepBodyAlive) - the 1 the save wrote (B23).
			bool lbRefused = !UWSpecialDeaths.Apply(liWhoami, UWSpecialDeaths.Stage.Dying, World, mIView.Ref);

			if (lbRefused)
				fEvent("death refused (whoami {0})", liWhoami);

			return lbRefused;
		}

		/// <summary>
		/// SpecialDeathCases(obj, 1): what follows a named creature's death. The original asks
		/// this from NPCInitialProcessing, that is when a dead one is next processed; we ask it
		/// the moment the death is committed, which is the same creature in the same state.
		/// (The decompilation shows the moment exactly: critter_ai's dying branch asks
		/// death_check(obj, 1) at frame 3, right before Obj_Rem, AI.C:1137 - the update in which
		/// OnDeathFinished runs.)
		/// </summary>
		private void fApplySpecialDeath()
		{
			int liWhoami = mONpc.NPCwhoami;

			if (UWSpecialDeaths.HasSpecialDeath(liWhoami))
				UWSpecialDeaths.Apply(liWhoami, UWSpecialDeaths.Stage.Dead, World, mIView.Ref);
		}

		/// <summary>
		/// Frame 3 of animation 0x0C: the body goes, ONCE (Dying, or Alive after a drowning, to
		/// Removed). The special death first, then the view's CompleteDeath: UWCritterRemains leaves
		/// the remains and the loot and frees the object. The only way a creature's body leaves the
		/// level by its death since cleanup step 4.12 (2026-10-19; UWCritterRemains' three-second
		/// fallback did it too until then, without the special death - B22).
		/// </summary>
		void ICritterHost.OnDeathFinished()
		{
			if (meDeath == UWCreatureDeathState.Removed)
			{
				fTrace("death finished again: ignored");

				return;
			}

			fEvent("death finished");

			// A drowned creature reaches the end without a start: its body is committed here, without
			// experience (crit_hndlr_walk passes no damage_critter) and with its hit points as they
			// were (since cleanup step 4.12b, 2026-10-19).
			if (meDeath == UWCreatureDeathState.Alive)
				fCommitDeath("at the end");

			meDeath = UWCreatureDeathState.Removed;

			// SpecialDeathCases with mode 1 - what follows a named creature death. The original
			// asks it from NPCInitialProcessing, when a dead one is next processed; the moment the
			// body goes is the closest one we have (see fApplySpecialDeath).
			fApplySpecialDeath();

			mIView.CompleteDeath();
		}

		/// <summary>Alive to Dying: the body counts as destroyed from now on (the view's
		/// MarkDestroyed, UWDamageable raising Destroyed once). psWhy names a commit that is no
		/// DamageNPC death for the trace (a load, a drowning); null for none.</summary>
		private void fCommitDeath(string psWhy)
		{
			meDeath = UWCreatureDeathState.Dying;

			if (psWhy != null)
				fTrace("death committed {0}", psWhy);

			mIView.MarkDestroyed();
		}

		/// <summary>The brain's GoalChanged (ICritterHost.OnGoalChanged until cleanup step 4.13).</summary>
		private void fOnGoalChanged(int piOldGoal, int piNewGoal, int piGTarg)
		{
			fTrace("goal {0} -> {1} (gtarg {2})", piOldGoal, piNewGoal, piGTarg);
		}

		// ------------------------------------------------- Damage intake

		/// <summary>
		/// Every blow, missile, spell or burn the UWDamageable took, as DamageNPC, with the attacker
		/// the blow names (UWDamageable.CreatureTakesBlow, cleanup step 4.8, 2026-10-18): a creature's
		/// index, the player, or nobody - a miss of the player's blow too, which lands with 0. The
		/// brain updates its bytes 0x11, 0x12 and 8, writes the kin alarm, starts the death and
		/// switches the music; the mind reacts at its next update.
		///
		/// THE AMOUNT IS THE WHOLE DAMAGE (2026-10-19, the user's decision "as the original"):
		/// damage_critter (UnderworldExhumed critter/AI.C:1458-1515) adds it whole to the tally of
		/// byte 0x11 (SET_DAMAGE, a byte field that wraps, include/object.h:286; the brain's
		/// OnDamaged wraps it the same way) before it compares the hit points. Since cleanup step 4.8
		/// (2026-10-18) the amount here was capped at the record's hit points - the drop of the body's
		/// own copy before that - so a dying creature (0 hit points) and one whose death was refused
		/// tallied 0 and a killing blow only what was left. The hit points come out the same: the
		/// brain sets 0 when the damage reaches them.
		/// </summary>
		public void TakeBlow(int piDamage, int piAttacker)
		{
			int liAmount = Math.Max(0, piDamage);

			fTrace("damaged {0} by {1} (hp {2})", liAmount, piAttacker == PlayerIndex ? "the player" : piAttacker.ToString(),
				Math.Max(0, mORecord.HitPoints - liAmount));
			mOBrain.OnDamaged(this, piAttacker, liAmount);
		}

		/// <summary>A blow of this creature or on it, through the world (cleanup step 3.3, 2026-10-17).</summary>
		private void fDamage(UWDamageTarget pOTarget, UWBlow pOBlow)
		{
			IUWWorld lOWorld = World;

			if (lOWorld != null)
				lOWorld.Damage(pOTarget, pOBlow);
		}

		// ------------------------------------------------- Spells on the creature

		/// <summary>Confusion: goal 2, attitude 1, the player as target - the three fields the
		/// original's spell writes (deviation 39 adopted), after the resistance check
		/// (fPassesSpellResistance). No duration.</summary>
		public UWTargetSpellRules.Result Confuse()
		{
			if (mOHealth == null || IsDead)
				return UWTargetSpellRules.Result.Missed;

			if (!fPassesSpellResistance("confusion"))
				return UWTargetSpellRules.Result.Spent;

			fTrace("confused");
			mORecord.Goal = WanderGoal;
			mORecord.GTarg = PlayerIndex;
			mORecord.Attitude = 1;

			return UWTargetSpellRules.Result.Shown;
		}

		/// <summary>
		/// THE RESISTANCE CHECK before Cause Fear, Confusion, Paralyse and Ally, built 2026-09-23
		/// (per user, it had been left out since the spells were rebuilt, deviation 39): the
		/// original's Ally_seg038_3307_8F8 and the shared routine of the other three ask
		/// ScaleDamageAgainstObject whether one point of MAGIC damage (type 3) gets through
		/// (UWDamageTypes.Scale). Magic resistance 0 - the low two bits of COMOBJ byte 8 - lets
		/// every spell through, 1 turns one in three away, 2 two in three, 3 all. A spell turned
		/// away is spent and does nothing, as in the original, and the caller shows no message - nor
		/// the spell picture (UWTargetSpellRules.Result.Spent, since 2026-10-12).
		/// </summary>
		private bool fPassesSpellResistance(string psSpell)
		{
			if (UWDamageTypes.Scale(mOHealth.Resistances, 1, UWDamageTypes.Magic) != 0)
				return true;

			fTrace("resists the {0} spell", psSpell);

			return false;
		}

		/// <summary>
		/// ALLY, the spell (Ally_seg038_3307_8F8, read 2026-09-23): unless it is an ally already,
		/// goal 2 with gtarg 0 - wandering, bound to nobody; then byte 0x19 bit 6 and attitude 3.
		/// For good, no duration. From then on it fights for the player the original's way: the
		/// kin alarm sends it at whatever the player strikes, and whoever strikes it is its target
		/// (UWCritterBrain). Until 2026-09-23 this was Confusion (goal 2, attitude 1, the player
		/// as target), because the port had no allies.
		/// </summary>
		public UWTargetSpellRules.Result Befriend()
		{
			if (mOHealth == null || IsDead)
				return UWTargetSpellRules.Result.Missed;

			if (!fPassesSpellResistance("ally"))
				return UWTargetSpellRules.Result.Spent;

			fTrace("befriended");

			if (!mORecord.IsAlly)
			{
				mORecord.Goal = WanderGoal;
				mORecord.GTarg = 0;
			}

			mORecord.IsAlly = true;
			mORecord.Attitude = 3;

			return UWTargetSpellRules.Result.Shown;
		}

		/// <summary>Paralyze: goal 7 (stand still) and attitude 1, after the resistance check
		/// (fPassesSpellResistance), without a duration as in the uw1 branch of the reference.
		/// A hit wakes it: the damage reaction sets goal 5.</summary>
		public UWTargetSpellRules.Result Paralyse()
		{
			if (mOHealth == null || IsDead)
				return UWTargetSpellRules.Result.Missed;

			if (!fPassesSpellResistance("paralysis"))
				return UWTargetSpellRules.Result.Spent;

			fTrace("paralysed");
			mORecord.Goal = GoalParalysed;
			mORecord.Attitude = 1;

			return UWTargetSpellRules.Result.Shown;
		}

		/// <summary>Cause Fear: goal 6 against the player, without a morale roll.</summary>
		public UWTargetSpellRules.Result Flee()
		{
			if (mOHealth == null || IsDead || mIWorld == null || !mIWorld.HasPlayer)
				return UWTargetSpellRules.Result.Missed;

			if (!fPassesSpellResistance("fear"))
				return UWTargetSpellRules.Result.Spent;

			fTrace("flees (spell)");
			mORecord.SetNewGoal(GoalWithdraw, PlayerIndex);

			return UWTargetSpellRules.Result.Shown;
		}

		/// <summary>
		/// The orb is broken: Tybal's hit points are halved plus one and he no longer heals when
		/// the player leaves the level (UWTybalOrbRules.WeakenedHitPoints, word 0x0D bit 9).
		/// </summary>
		public void WeakenByBrokenOrb()
		{
			if (mOHealth == null || IsDead)
				return;

			// Into the record, through the health's 1-to-maximum bound (one write since cleanup step
			// 4.8, 2026-10-18; the health's copy and the record until then).
			mOHealth.SetCurrentHealth(UWTybalOrbRules.WeakenedHitPoints(mOHealth.CurrentHealth));
			mORecord.NoLevelExitHealing = true;

			fTrace("weakened by the broken orb: {0} hit points", mORecord.HitPoints);
		}

		// ------------------------------------------------- The sleep ambush and the night

		/// <summary>
		/// The sleep ambush's path (wander_that_monster, UnderworldExhumed CRITTIME.C): set_critter_vars
		/// for this creature, then flood_path from its tile and height to pOTo at piToHeight with
		/// range 0 - the brain's own search (UWTileRoute.FindPath with this creature's triple test)
		/// with no danger allowed: no drop of more than a level, no costly terrain, a closed door
		/// blocked by its orientation alone, whatever the creature's door skill. pOPath gets the
		/// tiles, start first. Until 2026-10-15 this was the old walker (UWTilePath.TryGetPath) with
		/// the door and lava terms left over from the last brain search (audit round 2, A11 / KR D11).
		/// </summary>
		public bool TryGetPathTo(UWTilePos pOTo, int piToHeight, List<UWTilePos> pOPath)
		{
			pOPath.Clear();

			if (!fPrepareRoute())
				return false;

			if (!mORoute.FindPath(mORecord.TileX, mORecord.TileY, mORecord.FloorLevel, pOTo.X, pOTo.Y, piToHeight,
				AmbushPathRange, mOTraverse.Triple))
				return false;

			pOPath.AddRange(mORoute.Tiles);

			return true;
		}

		/// <summary>
		/// Sets the creature down on a sub-tile spot (0..7 each) of a tile - the sleep ambush
		/// (UWSleepRules.TryAmbush). Refused on a solid tile or the wrong ground for its kind; the
		/// motion core sorts out whatever else stands there on its first step (until stage 2 the
		/// host's probes also refused a door, the player, a creature and a force field).
		/// </summary>
		public bool PlaceAtSpot(UWTilePos pOTile, int piSubX, int piSubY)
		{
			UWTile lOTile = fGetTileAt(pOTile.X, pOTile.Y);

			if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
				return false;

			if (!mbFlying && mbSwimming != fIsWater(lOTile))
				return false;

			// On the floor, fliers too: the ambush hands the tile's floor height times eight as zpos
			// (ovr104_71C) - unlike the level exit, which lifts a flier halfway up.
			fRelocate(pOTile.X, pOTile.Y, piSubX & 7, piSubY & 7, (lOTile.FloorHeight >> 4) << 3);
			fTrace("set down by the sleep ambush on {0}/{1} at {2}/{3}", pOTile.X, pOTile.Y, piSubX, piSubY);

			return true;
		}

		/// <summary>
		/// The night's time pass rewrote the record (UWLevelExit.PassTime, 2026-10-15): the body
		/// stands at the record's spot and facing at once. The pass heals a wounded creature half way
		/// to its kind's average in the record, which the health reads (cleanup step 4.8, 2026-10-18;
		/// copied over until then).
		/// </summary>
		public void FollowRecord()
		{
			if (mOHealth == null || IsDead)
				return;

			mIView.SetBodyNow();

			fTrace("time passed: tile {0}/{1}, facing {2}, hp {3}", mORecord.TileX, mORecord.TileY,
				mORecord.FacingEighth, mORecord.HitPoints);
		}

		// ------------------------------------------------- Theft notice (spec 4.5)

		/// <summary>
		/// Does this creature SEE a theft on that tile? For UWTriggerSystem.AngerRaceAround. The
		/// original checks the distance to the ITEM against its own sight range from the critter
		/// table - not against the player's stealth - and a clear line between the two with the
		/// heights of TestBetweenPoints, which a closed door does not block (per user,
		/// 2026-09-16).
		/// </summary>
		/// <remarks>The point is the object's spot in eighths and its sight height
		/// (UWCritterRules.GetTheftSightZ) - since 2026-09-23; before, the floor of the tile
		/// centre.</remarks>
		public bool CanSeeTheftAt(int piX8, int piY8, int piZ)
		{
			if (fLevel() == null)
				return false;

			int liRange = mbHasStats ? mOStats.SightRange : 8;

			if (!UWCritterRules.IsWithinTheftSight(mORecord.X - piX8, mORecord.Y - piY8, liRange))
			{
				fTrace("theft at {0}/{1} z {2}: out of sight range {3} (from {4}/{5})", piX8, piY8, piZ, liRange, mORecord.X, mORecord.Y);
				return false;
			}

			GetOwnSize(out int liOwnRadius, out int liOwnHeight);

			bool lbSees = fHasLineOfSight(mORecord.X, mORecord.Y, mORecord.ZPos + liOwnHeight, piX8, piY8, piZ);

			fTrace("theft at {0}/{1} z {2}: {3} (from {4}/{5} z {6})", piX8, piY8, piZ,
				lbSees ? "sees it" : "line blocked", mORecord.X, mORecord.Y, mORecord.ZPos + liOwnHeight);

			return lbSees;
		}

		// ------------------------------------------------- Push

		/// <summary>
		/// A shove - the player walking into the creature (UWPlayerMovement, the bridge until stage
		/// 3), a missile or thrown thing striking it (UWMotionHost.PushObject): the momentum
		/// transfer's write-back on the record (UWCreatureMotion.Push) - the pusher's 16-bit
		/// heading, the speed and the vertical speed in the stepper's units. The creature's next
		/// update moves it that way before its mind decides anew; in the original an opponent can
		/// be pushed away by walking into it (per user, 2026-09-05), and the core keeps it out of
		/// walls and water on that step.
		/// </summary>
		public void Push(int piHeading, int piSpeed, int piVz)
		{
			if (IsRemoved)
				return;

			UWCreatureMotion.Push(mORecord, piHeading, piSpeed, piVz);
			fTrace("pushed: heading {0}, speed {1}", mORecord.FineHeading, mORecord.Speed);
		}

		// ------------------------------------------------- Tiles, size, data

		private IUWWorld World => mIWorld != null ? mIWorld.World : null;

		private DataImport fData()
		{
			IUWWorld lOWorld = World;

			return lOWorld != null ? lOWorld.Data : null;
		}

		private UWLevel fLevel()
		{
			IUWWorld lOWorld = World;

			return lOWorld != null ? lOWorld.Level : null;
		}

		private UWTile fGetTileAt(int piTileX, int piTileY)
		{
			UWLevel lOLevel = fLevel();

			if (lOLevel == null || lOLevel.TileData == null)
				return null;

			return lOLevel.GetTile(piTileX, piTileY);
		}

		private bool fIsWater(UWTile pOTileData)
		{
			DataImport lOData = fData();

			if (pOTileData == null || lOData == null)
				return false;

			UWTerrain lOTerrain = lOData.Terrain;

			return lOTerrain != null && lOTerrain.IsWaterFloor(pOTileData.TextureFloor);
		}

		/// <summary>Radius and height from COMOBJ.DAT, in the original's units - eighths of a tile
		/// and zpos steps.</summary>
		public void GetOwnSize(out int piRadius, out int piHeight)
		{
			piRadius = FallbackCritterRadius;
			piHeight = FallbackCritterHeight;

			DataImport lOData = fData();
			UWCommonObjectProperties lOCommon = lOData != null ? lOData.CommonObjectProperties : null;

			if (lOCommon != null && lOCommon.TryGet(mONpc.ID, out UWCommonObjectProperties.Entry lOOwn))
			{
				piRadius = lOOwn.Radius;
				piHeight = lOOwn.Height;
			}
		}

		// ------------------------------------------------- Trace

		private void fTrace(string psFormat, params object[] pOArguments)
		{
			mIView.Trace(psFormat, pOArguments);
		}

		private void fEvent(string psFormat, params object[] pOArguments)
		{
			mIView.Event(psFormat, pOArguments);
		}
	}

	/// <summary>
	/// Where a creature's death stands - the states of UWCreatureActor's death machine (cleanup step
	/// 4.12, 2026-10-19; the section "Death" there, after critter_ai and crit_die of UnderworldExhumed
	/// critter/AI.C).
	/// </summary>
	public enum UWCreatureDeathState
	{
		/// <summary>Living - with 0 hit points too, when a named creature refused its death.</summary>
		Alive,

		/// <summary>The death is committed and sequence 0x0C counts to frame 3 on the creature's own
		/// updates; the body is destroyed, blows still land, a save keeps it with 0 hit points.</summary>
		Dying,

		/// <summary>Frame 3 came: the special death, the remains and the loot are done, the body is
		/// gone.</summary>
		Removed
	}
}
