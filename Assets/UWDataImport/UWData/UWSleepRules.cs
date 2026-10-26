using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Sleeping (player_sleep, UnderworldExhumed SKILLS.C; before, the reference's sleep.Sleep).
	/// Engine-free since 2026-09-18 (P3 of the engine separation), out of UWSleep.
	///
	/// THREE WAYS, as the original's "how": camping without a bedroll (F10 with none carried),
	/// in the bedroll (using it, or F10 with one carried), or passing out drunk. Only the bedroll
	/// gives the COMFORT that a full belly needs to make a night restful (comfort = hunger above
	/// 0x40 AND the bedroll; until 2026-10-11 ours asked the hunger alone, from 0x40 on).
	///
	/// THE SEQUENCE, step by step as in the reference:
	///
	///   1. Is the player dry and on solid ground at all? Otherwise just a message.
	///   2. Is anyone hostile nearby who has noticed the player? Then no sleep either.
	///   3. "You make camp." and "You go to sleep."
	///   4. Two to six hours pass. Active spells end.
	///   5. Poison takes its toll all at once: (poison + 1) * poison / 2 damage, then it is gone.
	///   6. Do enemies come upon the sleeper? Then sleep is interrupted - less recovery,
	///      more hunger.
	///   7. Otherwise time passes for the level's creatures (UWLevelExit.PassTime) and the
	///      respawner runs; then more hours, seven to ten in total; longer when vitality is low.
	///      Then recovery, and hunger strikes.
	///   8. A dream of Garamon if one is due, otherwise 1.5 seconds of black (see fDream);
	///      whoever passed out neither dreams nor waits.
	///
	/// RECOVERY (see UWPlayerVitals.RegenerateOnSleep): regen = 2 + fatigue / 2, at most 5;
	/// the hit points get the share regen + regen * comfort - 1, the mana six and then the share
	/// regen + (regen + 1) * comfort - 1 - a night always heals, a comfortable one twice as much.
	/// A starving sleeper gets nothing and two points of damage.
	///
	/// THE DOORS: every open door of the level shuts with a chance of three in ten (DoClosingDoors,
	/// since 2026-10-11; before, every door within eight tiles), as if someone closed it in the
	/// night, and up to twenty far objects are tidied away (Obj_GarbageCollect, since
	/// 2026-10-15). The monster respawner runs, and the carried lights burn down while asleep
	/// (fBurnCarriedLights, since 2026-09-24).
	///
	/// THE WORLD INSTEAD OF A HOST (cleanup step 3.6, 2026-10-17): the night asks IUWWorld for
	/// everything around the sleeper - the player's motion state, tile and inventory, the creatures
	/// near him, the doors, the level's own time - and shows itself through the world's
	/// presentation. Until then IUWSleepHost (22 members, implemented by UWSleep) did, most of it
	/// forwarding to the world already; the night's door rule lived in that host.
	/// </summary>
	public static class UWSleepRules
	{
		/// <summary>The bedroll (UWObjectIds since 2026-10-11).</summary>
		public const int BedrollObjectId = UWObjectIds.Bedroll;

		/// <summary>Player ticks a sleeping hour counts for the lights (0xB4).</summary>
		public const int SleepTicksPerHour = 180;

		// Messages from string block 1, in OUR numbering - i.e. one above each of the reference's
		// numbers (see UWStatusReport).
		private const int EnemiesNearbyMessage = 0x0F;
		private const int MakeCampMessage = 0x10;

		/// <summary>The chance in ten that an open door shuts in the night (DoClosingDoors).</summary>
		internal const int DoorClosesInTen = 3;

		/// <summary>The night's Obj_GarbageCollect(1, 0x14): range 1, twenty objects at most.</summary>
		public const int NightCullRange = 1;

		public const int NightCullLimit = 0x14;

		/// <summary>What water and lava do to whoever passes out there: everything
		/// (drop_drunk_player hands damage_item 0xFF).</summary>
		private const int DrownDamage = 0xFF;
		private const int GoToSleepMessage = 0x11;
		private const int StarvingMessage = 0x12;

		/// <summary>A night on an empty stomach costs two untyped points (damage_item(..., 2, 0)).</summary>
		internal const int StarvingDamage = 2;
		private const int RestedMessage = 0x14;
		private const int CannotSleepHereMessage = 0x15;
		private const int InterruptedMessage = 0x16;

		/// <summary>Above this hunger value the player counts as fed enough for a comfortable night.</summary>
		internal const int WellFedHunger = 0x40;

		/// <summary>The dreams of Garamon exist as cutscenes 24 to 31 - octal in
		/// the file names, i.e. CS030 to CS037. The roll in fDream can also pick stages 8 and 9
		/// (cutscenes 32 and 33, CS040 and CS041), for which there is no file.</summary>
		private const int FirstDreamCutscene = 0x18;

		/// <summary>This quest flag records which dreams have already played - one bit per
		/// dream.</summary>
		private const int DreamQuestFlag = UWPlayerData.DreamQuestFlag;

		/// <summary>The first of the rolled dreams.</summary>
		private const int FirstRandomDream = 4;

		/// <summary>This many are eligible - four to nine.</summary>
		private const int RandomDreamCount = 6;

		/// <summary>How long the view window stays black when there is no dream: dream
		/// (UnderworldExhumed SKILLS.C) waits 0x180 ticks of the 1/256-second game clock, a second
		/// and a half. Two seconds until 2026-10-15 (audit round 2, A34 / SW D14).</summary>
		public const float SleepFlashSeconds = 0x180 / 256f;

		/// <summary>
		/// Goes to sleep. Returns false if it is not possible at all - then a message
		/// is already in the message scroll.
		/// </summary>
		/// <param name="piDungeonLevel">The dungeon level, 1-based - the second dream only comes
		/// from the second level on.</param>
		/// <param name="pbBedroll">In the bedroll - the only way to a comfortable night.</param>
		/// <param name="pOWorld">The world around the sleeper; its presentation is the night's (the
		/// scroll the sleep was asked from, see UWScopedWorld).</param>
		public static bool Sleep(UWPlayerVitals pOVitals, int piDungeonLevel, IUWWorld pOWorld,
			bool pbPassedOut = false, bool pbBedroll = false)
		{
			if (pOVitals == null || pOWorld == null)
				return false;

			IUWPresentation lOShown = pOWorld.Presentation;

			// The night's numbers are the rule set's (sleep.*, cleanup phase 10); the consts of this
			// class are their defaults. The set is the world's session's (since wave 4).
			UWRules lORuleSet = pOWorld.Session.Rules;
			UWRulesSleep lORules = lORuleSet.Sleep;

			// WHOEVER PASSES OUT IS NOT ASKED. The original's Sleep takes a method, and a
			// negative one - the alcohol - jumps past every refusal (Sleep_ovr143_D1F, label
			// D30: "jump when method < 0"). Only a chosen sleep is refused on water, lava, off the
			// ground, on level 9 or with enemies about - and only then are "You make camp." and
			// "You go to sleep." said; whoever drinks himself down sleeps where he stands, and
			// then the ground has its say. The Void is refused like water or lava (label D44).
			if (!pbPassedOut)
			{
				if (!CanSleepHere(pOWorld.Player) || !pOWorld.Player.IsOnGround
					|| piDungeonLevel == UWLevelIds.Number(lORuleSet.Levels.Void))
				{
					lOShown.AddGeneralMessage(CannotSleepHereMessage);

					return false;
				}

				if (IsHostileNear(pOWorld))
				{
					lOShown.AddGeneralMessage(EnemiesNearbyMessage);

					return false;
				}

				lOShown.AddGeneralMessage(MakeCampMessage);
			}

			// The music after the refusals (player_sleep sets it only once the night is sure;
			// ours played it before the enemies were asked until 2026-10-11). "Maps & Legends" for
			// sleeping (reference: sleep.cs, uw1 branch).
			lOShown.ChangeMusicTheme(UWMusicSelector.MapsAndLegendsTheme);

			if (!pbPassedOut)
				lOShown.AddGeneralMessage(GoToSleepMessage);

			CloseDoorsAtNight(pOWorld, lORules.DoorClosesInTen);

			// FAR JUNK GOES IN THE NIGHT: player_sleep (UnderworldExhumed SKILLS.C) runs
			// Obj_GarbageCollect(1, 0x14) right after the doors - up to twenty objects on tiles
			// more than nine from the player that are not tenacious against 1 + 0..2 go, each with
			// that chance in ten. Ours culled only when the level ran out of slots until 2026-10-15
			// (audit round 2, A2 / GI D13).
			pOWorld.CollectGarbage(NightCullRange, NightCullLimit);

			int liHours = lORules.Hours.FirstBase + UWRandom.Next(lORules.Hours.FirstRoll);

			// THE PROOF BEFORE THE NIGHT, for the poison below: player_sleep clears the active
			// spells, but the player's resist byte keeps them until its FixPlayerEquips at the end
			// (UnderworldExhumed SKILLS.C, PLAYDATA.C), while ours recomputes the proof at once.
			int liProofBeforeNight = pOWorld.Player.DamageTypeProof;

			pOVitals.EndAllSpells();
			pOVitals.AdvanceClockHours(liHours);

			// Sleep ends the hallucination as well: the original clears PLAYER.DAT 0x61 bits 2-3
			// together with the active spells, right after the clock (read 2026-09-25). Ours
			// let it run on until then.
			pOVitals.EndHallucination();

			// THE LIGHTS BURN while asleep, right after each advance of the clock: 180 player
			// ticks an hour, counter zero, so 1 + hours * 180 / rate points (UpdateInventoryLightSources
			// called twice from the sleep routine, 2026-09-24). Ours burned nothing until then.
			fBurnCarriedLights(pOWorld, liHours * lORuleSet.Lights.SleepTicksPerHour);

			// Poison takes the whole series at once: at strength three that is 3+2+1. POISON
			// RESISTANCE WARDS IT OFF, as on the awake tick: damage_item(..., DMG_POISON) runs
			// check_res (SKILLS.C player_sleep, DAMAGE.C). Ours ignored it until 2026-10-17 (B5,
			// decided by the original; UWDamageRequest.SleepPoison).
			if (pOVitals.Poison > 0)
			{
				UWDamagePipeline.Apply(pOVitals,
					UWDamageRequest.SleepPoison((pOVitals.Poison + 1) * pOVitals.Poison / 2, liProofBeforeNight));
				pOVitals.CurePoison();
			}

			// The ground under whoever passed out, after the poison (drop_drunk_player).
			if (pbPassedOut)
				fApplyDamagingSurface(pOWorld);

			if (pOVitals.CurrentHP <= 0)
				return true;

			if (TryAmbush(pOWorld))
			{
				fInterrupt(pOVitals, lOShown, lORules);

				return true;
			}

			// THE NIGHT PASSES FOR THE CREATURES TOO: player_sleep (UnderworldExhumed SKILLS.C) runs
			// update_all_critters_whilst_player_snoozes right before DoWanderingMonsters(0) on every
			// undisturbed night - the same pass as leaving the level (UWLevelExit). Until 2026-10-15
			// ours ran it on the level change only, so wounded monsters stayed wounded, chasers away
			// from their posts and allies allies after any number of nights (audit round 2, KR D2).
			pOWorld.LetTimePassForCreatures();
			// Asleep, the distance test is off: every respawn trap fires (Sleep_ovr143_D1F
			// calls TriggerCreateObjectTrap_ovr153_1517 with 0).
			pOWorld.RunRespawn(true);

			// Seven to ten hours in total. Whoever is wounded sleeps longer.
			int liRest = lORules.Hours.TotalBase + UWRandom.Next(lORules.Hours.TotalRoll) - liHours;

			if (pOVitals.CurrentHP < lORules.Hours.WoundedBelow)
				liRest += lORules.Hours.WoundedExtraBase + UWRandom.Next(lORules.Hours.WoundedExtraRoll);

			if (liRest > 0)
				pOVitals.AdvanceClockHours(liRest);

			// The second burn comes whatever the rest is - at zero or below still the one point
			// the counter zero brings.
			fBurnCarriedLights(pOWorld, liRest * lORuleSet.Lights.SleepTicksPerHour);

			// The comfort applies to both: recovery AND the probability that a later dream is
			// rolled. Whoever passed out has none and does not dream.
			int liComfort = !pbPassedOut && pbBedroll && pOVitals.Hunger > lORules.Recovery.WellFedHunger ? 1 : 0;

			fRecover(pOVitals, lOShown, liComfort, lORules);

			// WHOEVER PASSED OUT DOES NOT WAIT: player_sleep calls dream only for a chosen sleep,
			// and dream's moment of black is its own wait - passing out only fades out and in
			// (fadeout3d, fadein3d), with no hold. Ours held two seconds of black until 2026-10-15
			// (audit round 2, A34 / SW D14).
			if (!pbPassedOut)
				fDream(piDungeonLevel, liComfort, lOShown);

			return true;
		}

		/// <summary>
		/// Dreams, if a dream is due.
		///
		/// THE ORDER IS KEPT IN QUEST FLAG 37, one bit per dream (reference:
		/// sleep.DreamsUW1). If none is set yet, the first dream comes. After that it depends
		/// on how far the player has got: the second only from the second level on.
		///
		/// ONLY WHEN NONE IS PENDING ANY MORE is a roll made - one in four, one in eight
		/// when sleeping well fed - and then one of four to nine is taken, provided it has not played yet.
		///
		/// HERE WE DEVIATE FROM THE REFERENCE, and based on measurement. There the
		/// stage variable starts at -1, only the branch "first dream pending" sets it to zero,
		/// and the roll depends on "== 0" - so it would apply precisely when the first
		/// dream is due. The user counted this in the ORIGINAL (2026-09-07): from
		/// a save with no bits set, hunger set to 50 and thus in the
		/// one-in-four case, slept more than twenty times - always the same dream. If the roll
		/// were effective there, that would be 0.75 to the power of 20, i.e. three per mille. So in the original
		/// that spot holds the marker for "no dream", not the one for "first dream"; the
		/// reference's transcription chose -1 for it and thereby blurred the difference.
		///
		/// The rolled bit is toggled via XOR, as in the reference.
		///
		/// ONCE GARAMON IS BURIED THERE ARE NO DREAMS AT ALL (GaramonDream_ovr143_B73, label C14:
		/// PLAYER.DAT 0x62 bit 3 skips the cutscene and the bit, whatever stage was picked).
		/// Missing here until 2026-09-23; noticed with Henrietta, who has buried him and still
		/// dreamt the pending Tyball dream every time, while the original stayed dark (per user).
		/// </summary>
		private static void fDream(int piDungeonLevel, int piFood, IUWPresentation pOShown)
		{
			int liFlags = UWQuestFlags.Get(DreamQuestFlag);
			int liStage = fGetDreamStage(liFlags, piDungeonLevel);

			// If none of the four story dreams is pending any more, a later one can be rolled
			// - one in four, one in eight when sleeping well fed. If the choice falls on one
			// that has already played, there is none at all.
			if (liStage < 0 && UWRandom.Next(4 + (piFood << 2)) == 0)
			{
				liStage = FirstRandomDream + UWRandom.Next(RandomDreamCount);

				if ((liFlags & (1 << liStage)) != 0)
					liStage = -1;
			}

			if (liStage < 0 || UWGameFlags.GaramonBuried || !pOShown.PlayCutscene(FirstDreamCutscene + liStage))
			{
				pOShown.ShowNightBlack(SleepFlashSeconds);

				return;
			}

			UWQuestFlags.Set(DreamQuestFlag, liFlags ^ (1 << liStage));
		}

		/// <summary>Which dream is due, or -1 for none - see fDream.</summary>
		private static int fGetDreamStage(int piFlags, int piDungeonLevel)
		{
			if ((piFlags & 1) == 0)
				return 0;

			if (piDungeonLevel > 1 && (piFlags & 2) == 0)
				return 1;

			if ((piFlags & 4) != 0)
				return 2;

			if ((piFlags & 8) != 0)
				return 3;

			return -1;
		}

		/// <summary>Recovery and hunger after a full night's sleep.</summary>
		private static void fRecover(UWPlayerVitals pOVitals, IUWPresentation pOShown, int piComfort, UWRulesSleep pORules)
		{
			int liRegen = System.Math.Min(pORules.Recovery.Most, pORules.Recovery.Base + (pOVitals.Fatigue / 2));

			pOVitals.ClearFatigue();

			if (pOVitals.Hunger == 0)
			{
				pOShown.AddGeneralMessage(StarvingMessage);
				UWDamagePipeline.Apply(pOVitals, UWDamageRequest.Starvation(pORules.StarvingDamage));
			}
			else
			{
				pOVitals.RegenerateOnSleep(liRegen + (liRegen * piComfort) - 1,
					liRegen + ((liRegen + 1) * piComfort) - 1);
			}

			// A night costs 24 plus RNG & 0x1F of hunger and takes 32 off the intoxication
			// (Sleep_ovr143_D1F, labels 1016 and 102F, read 2026-09-27). Ours took 2 plus 0 to 30
			// and 16, after the reference, until then.
			pOVitals.ChangeHunger(-pORules.Hunger.NightBase - UWRandom.Next(pORules.Hunger.NightRoll));
			pOVitals.ReduceIntoxication(pORules.Relief.Intoxication);

			pOShown.AddGeneralMessage(RestedMessage - piComfort);
		}

		// ------------------------------------------------- Who keeps the player awake

		/// <summary>The square around the player searched before sleeping, in tiles.</summary>
		public const int HostileCheckTileRadius = 2;

		/// <summary>The square around the sleeper searched for an ambush, in tiles.</summary>
		public const int AmbushTileRadius = 8;

		/// <summary>The goals that keep the player from sleeping - see BlocksSleep.</summary>
		private const int GoalAttack = UWNpc.GoalAttack;

		private const int GoalFour = 4;

		private const int GoalStandAndFight = 9;

		/// <summary>The attitude an ambusher must have: hostile.</summary>
		private const int HostileAttitude = 0;

		/// <summary>An ambush needs a path of at least this many steps.</summary>
		public const int AmbushMinSteps = 2;

		/// <summary>The hours of a night (Sleep): two plus 0 to 4 before the ambush test, seven
		/// plus 0 to 3 in all, and one plus 0 to 1 more below ten hit points.</summary>
		public const int FirstHoursBase = 2;

		public const int FirstHoursRoll = 5;

		public const int TotalHoursBase = 7;

		public const int TotalHoursRoll = 4;

		public const int WoundedBelowHitPoints = 10;

		public const int WoundedExtraHoursBase = 1;

		public const int WoundedExtraHoursRoll = 2;

		/// <summary>The recovery (fRecover): regen = 2 + fatigue / 2, at most 5.</summary>
		public const int RecoveryBase = 2;

		public const int RecoveryMost = 5;

		/// <summary>The hunger a night costs: 24 plus RNG &amp; 0x1F (fRecover); an interrupted one
		/// 12 plus 0 to 15 (fInterrupt).</summary>
		public const int NightHungerBase = 24;

		public const int NightHungerRoll = 0x20;

		public const int InterruptedHungerBase = 12;

		public const int InterruptedHungerRoll = 16;

		/// <summary>
		/// WHO KEEPS THE PLAYER FROM LYING DOWN (hostile_creatures_near with its callback
		/// check_for_hostile_creature, UnderworldExhumed CRITTIME.C): over every creature in the
		/// area two around the player's tile (InArea) a creature that attacks (goal 5), guards (4)
		/// or is cornered (9) AND knows where its target is (byte 0x19 bit 0) answers "There are
		/// hostile creatures near!". NOTHING ELSE: not the attitude, not the line of sight - a wall
		/// between them does not help. Until 2026-09-24 ours refused for any creature with a hostile
		/// attitude in the radius; until 2026-10-15 for every attacker and guard whether it knew
		/// where the player was or not, so a friendly guard or one that never saw the player kept
		/// him awake (the IDA reading had the bit test on goal 9 only; audit round 2, A6 / KR D1).
		/// </summary>
		public static bool BlocksSleep(int piGoal, bool pbTargetConfirmed)
		{
			return pbTargetConfirmed
				&& (piGoal == GoalAttack || piGoal == GoalFour || piGoal == GoalStandAndFight);
		}

		/// <summary>
		/// THE AREA OF gronk_area (UnderworldExhumed SPELLS.C): process_area gets the corner at the
		/// tile minus the radius and a width of radius * 2 + 1, and its loops run from the corner
		/// to corner + width INCLUSIVE - so the area reaches one tile further on the +x and +y side:
		/// -2..+3 for the sleep refusal, -8..+9 for the ambush. Ours was the square -r..+r until
		/// 2026-10-15 (audit round 2, KR D1).
		/// </summary>
		public static bool InArea(int piDx, int piDy, int piRadius)
		{
			return AreaAround(new UWTilePos(0, 0), piRadius).Contains(new UWTilePos(piDx, piDy));
		}

		/// <summary>The same area as a creature search's (IUWWorld.FindCreatures, cleanup step 3.2,
		/// 2026-10-17): around the sleeper's tile, by the RECORD's tile, where the original keeps
		/// it.</summary>
		public static UWTileArea AreaAround(UWTilePos pOCentre, int piRadius)
		{
			return UWTileArea.Around(pOCentre, piRadius, piRadius + 1, UWCreatureTile.Record);
		}

		/// <summary>
		/// THE CREATURES AROUND THE SLEEPER: the living ones on the tiles of the area of this radius
		/// around the player's tile (AreaAround), by the tile in their record, where the original
		/// keeps it too - ordered as RunCodeOnObjectsInArea_seg038_3307_9C7 walks them: column by
		/// column (x outer, y inner). Found by the world's creature search since cleanup step 3.2
		/// (2026-10-17; before, a scene search); collected here instead of on the sleep's host since
		/// step 3.6 (the same day), with the same sort over the same list. Clears pOInto and returns
		/// how many.
		/// </summary>
		public static int CollectCreaturesAround(IUWWorld pOWorld, int piTileRadius, List<UWCreatureRef> pOInto)
		{
			pOWorld.FindCreatures(AreaAround(pOWorld.Player.FloorSpot.Tile, piTileRadius), UWCreatureFilter.Living,
				pOInto);

			pOInto.Sort((pOLeft, pORight) => pOLeft.Record.TileX != pORight.Record.TileX
				? pOLeft.Record.TileX.CompareTo(pORight.Record.TileX)
				: pOLeft.Record.TileY.CompareTo(pORight.Record.TileY));

			return pOInto.Count;
		}

		public static bool IsHostileNear(IUWWorld pOWorld)
		{
			List<UWCreatureRef> lOCreatures = new List<UWCreatureRef>();
			int liCount = CollectCreaturesAround(pOWorld, pOWorld.Session.Rules.Sleep.HostileCheckTileRadius, lOCreatures);

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				UWCritterRecord lORecord = lOCreatures[liAt].Record;

				if (lORecord != null && BlocksSleep(lORecord.Goal, lORecord.TargetConfirmed))
					return true;
			}

			return false;
		}

		/// <summary>Whether the sleeper lies within an ambusher's reach: the squared tile
		/// distance at most three times the square of its travel range.</summary>
		public static bool WithinAmbushReach(int piDx, int piDy, int piTravelRange)
		{
			return (piDx * piDx) + (piDy * piDy) <= 3 * piTravelRange * piTravelRange;
		}

		/// <summary>
		/// THE AMBUSH IN THE NIGHT (UpdateNearByNPCMovements_ovr104_B80 with its callback
		/// ovr104_71C, read 2026-09-24): after the first hours of sleep the original runs over the
		/// creatures on the tiles eight around the sleeper and takes the FIRST that passes, in the
		/// order of the tiles (column by column):
		///   - its attitude is hostile (0);
		///   - a coin comes up (one in two);
		///   - the sleeper lies within its reach (WithinAmbushReach, critter table 0x1C upper
		///     nibble);
		///   - its path search (flood_path with range 0, no danger allowed - the brain's search,
		///     since 2026-10-15; before, the old walker) finds the way to the sleeper's tile and
		///     height, AT LEAST TWO STEPS long - one standing next to him already is not moved.
		/// It then walks that way at once. It is set down on the tile TWO STEPS before him (the path buffer's entry count - 2), on the
		/// fixed spot of that tile's type (TryGetAmbushSpot) if it fits there - otherwise the
		/// next one is tried -, confirms its target and heads for him. The sleep is interrupted.
		/// THE FIT is the original's item-fits-in-tile test with the creature's radius;
		/// ours asks the open half of the tile (UWTileQueries.IsSubTileInOpenHalf) and the
		/// creature's own blockers (UWCritter.PlaceAtSpot).
		/// Until 2026-09-24 ours interrupted for any hostile within two tiles and moved nobody.
		/// </summary>
		public static bool TryAmbush(IUWWorld pOWorld)
		{
			List<UWCreatureRef> lOCreatures = new List<UWCreatureRef>();
			UWRulesSleep lORules = pOWorld.Session.Rules.Sleep;
			int liCount = CollectCreaturesAround(pOWorld, lORules.Ambush.TileRadius, lOCreatures);
			UWTilePos lOPlayer = pOWorld.Player.FloorSpot.Tile;
			List<UWTilePos> lOPath = new List<UWTilePos>();

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				UWCreatureRef lOCreature = lOCreatures[liAt];
				UWCritterRecord lORecord = lOCreature.Record;

				if (lORecord == null || lORecord.Attitude != HostileAttitude)
					continue;

				if (UWRandom.Next(2) == 0)
					continue;

				if (!WithinAmbushReach(lORecord.TileX - lOPlayer.X, lORecord.TileY - lOPlayer.Y,
					pOWorld.CreatureTravelRange(lOCreature)))
					continue;

				if (!pOWorld.TryGetCreaturePath(lOCreature, lOPlayer, fSleeperHeight(pOWorld), lOPath)
					|| lOPath.Count - 1 < lORules.Ambush.MinSteps)
					continue;

				// A RUNE OF WARDING ON THE WAY DOES NOTHING: wander_that_monster (UnderworldExhumed
				// CRITTIME.C) runs UseTrap on every ward trap under a trigger along the path, but
				// directly, outside SetOffTrap, so nobody is the one who set it off and the ward's
				// first test (TRIGGER.C, TRAP_WARD) fails - no damage, no message, the rune stays
				// armed. Until 2026-10-12 ours fired the rune for the creature here, which hurt it
				// and used the rune up (reference audit, traps D2).

				UWTilePos lOLanding = lOPath[lOPath.Count - 3];
				UWTile.TileTypeEnum leType = fTileTypeAt(pOWorld, lOLanding);

				if (!TryGetAmbushSpot(leType, out int liSubX, out int liSubY)
					|| !UWTileQueries.IsSubTileInOpenHalf(leType, liSubX, liSubY)
					|| !pOWorld.PlaceCreature(lOCreature, lOLanding, liSubX, liSubY))
					continue;

				lORecord.TargetConfirmed = true;
				lORecord.SetDestination(lOPlayer.X, lOPlayer.Y, fSleeperHeight(pOWorld));

				return true;
			}

			return false;
		}

		/// <summary>The player's zpos (0 to 127) shifted by three, as the record keeps a height - the
		/// FEET's height (the avatar's FeetSpot), not the floor's under the eye.</summary>
		private static int fSleeperHeight(IUWWorld pOWorld)
		{
			return pOWorld.Player.FeetSpot.FloorLevel;
		}

		/// <summary>The type of a tile of the current level; solid outside the map.</summary>
		private static UWTile.TileTypeEnum fTileTypeAt(IUWWorld pOWorld, UWTilePos pOTile)
		{
			UWTile lOTile = pOWorld.Tiles != null ? pOWorld.Tiles.GetTile(pOTile.X, pOTile.Y) : null;

			return lOTile != null ? lOTile.TileType : UWTile.TileTypeEnum.solid;
		}

		// ------------------------------------------------- The ground, the doors, the lights

		/// <summary>
		/// Water and lava forbid a chosen sleep: the original refuses in motion states swimming
		/// and lava (player_sleep's motion_state &amp; 0x1B) - the same states that kill whoever
		/// passes out (IsSwimming, IsOnLava), so the shore refuses and water walking does not.
		/// Floating is IsOnGround's: the original refuses a chosen sleep in motion states 0x1B and
		/// while the vertical acceleration is not zero.
		/// </summary>
		public static bool CanSleepHere(IUWPlayerAvatar pOPlayer)
		{
			return !pOPlayer.IsSwimming && !pOPlayer.IsOnLava;
		}

		/// <summary>
		/// The night's closing doors (DoClosingDoors(0), UnderworldExhumed TRIGGER.C): every open door
		/// of the level, wherever it is, that is not moving, on a tile without the tile's door bit,
		/// closes with a chance of piChanceInTen in ten (UWRandom.Next(10) per door) - the rule is
		/// UWDoorRules.NightCloses. Until 2026-10-11 every door within eight tiles (the reference's
		/// reading); until 2026-10-12 the swinging doors only, never a portcullis (B11:
		/// DoClosingDoors walks every door object of the open range, the raised grate 0x14E
		/// included - UnderworldExhumed event/TRIGGER.C). On the sleep's host until cleanup step
		/// 3.6 (2026-10-17); a rule since, over the world's doors.
		/// </summary>
		public static void CloseDoorsAtNight(IUWWorld pOWorld, int piChanceInTen)
		{
			UWLevel lOLevel = pOWorld.Level;

			// Walked over a copy: a door closing from here raises its state into the data, and
			// the registry must not change under the loop.
			foreach (IUWDoor lODoor in new List<IUWDoor>(pOWorld.Doors.All))
			{
				if (lODoor == null || lODoor.Data == null)
					continue;

				UWTile lOTileData = lOLevel != null ? lOLevel.GetTile(lODoor.Data.TileX, lODoor.Data.TileY) : null;
				bool lbTileHasDoorBit = lOTileData != null && lOTileData.HasDoor;

				// The roll is drawn for a candidate only, as the original's && does.
				if (!UWDoorRules.IsNightCandidate(!lODoor.IsClosed, lODoor.IsMoving, lbTileHasDoorBit))
					continue;

				if (!UWDoorRules.NightCloses(!lODoor.IsClosed, lODoor.IsMoving, lbTileHasDoorBit,
					UWRandom.Next(10), piChanceInTen))
					continue;

				lODoor.Close();
			}
		}

		/// <summary>The carried lights burn for this many player ticks at once, with counter 0
		/// (UWInventoryModel.BurnTick).</summary>
		private static void fBurnCarriedLights(IUWWorld pOWorld, int piTicks)
		{
			UWInventoryModel lOInventory = pOWorld.Player.Inventory;

			if (lOInventory != null)
				lOInventory.BurnTick(piTicks, 0);
		}

		/// <summary>
		/// WHERE IN THE TILE THE AMBUSHER IS SET DOWN (ovr104_BB): a fixed spot per tile type, in
		/// eighths - 4/4 on an open tile and on the slopes, a corner of the open half on three of
		/// the diagonals (south-east 6/1, south-west 1/1, north-east 6/6), nothing on a solid
		/// tile. NORTH-WEST HAS 6/1 in the original's table, the same as south-east, which lies in
		/// the CLOSED half - so its fit test refuses it and a creature is never set down on a
		/// north-west diagonal. Kept as the original has it.
		/// </summary>
		public static bool TryGetAmbushSpot(UWTile.TileTypeEnum peType, out int piSubX, out int piSubY)
		{
			switch (peType)
			{
				case UWTile.TileTypeEnum.solid:
					piSubX = 0;
					piSubY = 0;

					return false;

				case UWTile.TileTypeEnum.diagonal_se:
				case UWTile.TileTypeEnum.diagonal_nw:
					piSubX = 6;
					piSubY = 1;

					return true;

				case UWTile.TileTypeEnum.diagonal_sw:
					piSubX = 1;
					piSubY = 1;

					return true;

				case UWTile.TileTypeEnum.diagonal_ne:
					piSubX = 6;
					piSubY = 6;

					return true;

				default:
					piSubX = 4;
					piSubY = 4;

					return true;
			}
		}

		/// <summary>What an interrupted sleep takes off the fatigue (Sleep_ovr143_D1F after the
		/// ambush: 0x20 off PLAYER.DAT 0x3A, down to 0). Ours took 0x18 until 2026-09-24.</summary>
		internal const int InterruptedFatigueRelief = 0x20;

		/// <summary>An interrupted sleep brings little: a bit of fatigue gone, but
		/// a lot of hunger.</summary>
		private static void fInterrupt(UWPlayerVitals pOVitals, IUWPresentation pOShown, UWRulesSleep pORules)
		{
			pOVitals.ReduceFatigue(pORules.Relief.InterruptedFatigue);
			pOShown.AddGeneralMessage(InterruptedMessage);
			pOVitals.ChangeHunger(-pORules.Hunger.InterruptedBase - UWRandom.Next(pORules.Hunger.InterruptedRoll));

			// Half a night's intoxication (label EB3); ours left it until 2026-09-27.
			pOVitals.ReduceIntoxication(pORules.Relief.InterruptedIntoxication);
		}
		/// <summary>
		/// What the ground does to whoever passes out on it (drop_drunk_player, UnderworldExhumed
		/// SKILLS.C): when the motion state swims or is lava, damage_item hands the player 255 -
		/// in water, AT THE WATER'S EDGE (the shore gives the swimming state too) and on lava
		/// alike. The routine's 12 to 62 for a fall tests a floating state without a levitate,
		/// fly or slow-fall bit, which the FixPlayerEquips just before makes impossible (its
		/// fizix_update sets the state anew from those bits): it never deals anything.
		/// Until 2026-10-15 ours burned 12 to 62 on lava (nothing when levitating or walking on
		/// water) and let a shore tile pass (audit round 2, A31 / SW D7).
		/// </summary>
		private static void fApplyDamagingSurface(IUWWorld pOWorld)
		{
			// Drowning or lava in sleep, untyped - so neither Flameproof nor the dragon skin boots
			// help (B5, decided by the original: drop_drunk_player hands damage_item type 0) -
			// through the world since cleanup step 3.3 (2026-10-17), as a request since step 4.2.
			if (pOWorld.Player.IsSwimming || pOWorld.Player.IsOnLava)
				pOWorld.Damage(UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.SleepSurface(DrownDamage)));
		}

		/// <summary>
		/// The poison takes effect: damage equal to the current value, and the value drops by one.

	}
}
