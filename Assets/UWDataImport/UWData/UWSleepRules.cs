namespace UWDataImport.UWData
{
	/// <summary>
	/// What sleeping needs from the game: whether the spot allows it, whether an enemy is
	/// near, the sleep music, the respawner, and the dream or the two seconds of black.
	/// The Unity side is UWSleep.
	/// </summary>
	public interface IUWSleepHost
	{
		/// <summary>Dry and on solid ground - water and lava forbid sleeping.</summary>
		bool CanSleepHere { get; }

		/// <summary>The creatures (not the player) standing on the tiles of the square this many
		/// tiles around the player's tile, collected for CreatureRecord and the rest - the area
		/// of the original's walk around an object, with distance 0. Returns how many.</summary>
		int CollectCreatures(int piTileRadius);

		/// <summary>The record of a collected creature, or null.</summary>
		UWCritterRecord CreatureRecord(int piIndex);

		/// <summary>Critter table byte 0x1C, upper nibble (UWObjectClassProperties.Critter.TravelRange).</summary>
		int CreatureTravelRange(int piIndex);

		/// <summary>The creature's own path search from its tile to pOTo, both ends included
		/// (UWTilePath.TryGetPath).</summary>
		bool TryGetCreaturePath(int piIndex, UWTilePos pOTo, System.Collections.Generic.List<UWTilePos> pOPath);

		/// <summary>The rune-of-warding move triggers on this tile go off for the creature.</summary>
		void FireWardTriggers(int piIndex, UWTilePos pOTile);

		/// <summary>Moves the creature onto this sub-tile spot (0..7 each) of the tile; false when
		/// it does not fit there - a closed door, the player, another creature, a force field,
		/// the wrong ground for its kind.</summary>
		bool PlaceCreature(int piIndex, UWTilePos pOTile, int piSubX, int piSubY);

		/// <summary>The type of a tile of the current level; solid outside the map.</summary>
		UWTile.TileTypeEnum TileTypeAt(UWTilePos pOTile);

		UWTilePos PlayerTile { get; }

		/// <summary>The player's height in the record's units (zpos &gt;&gt; 3).</summary>
		int PlayerFloorLevel { get; }

		/// <summary>"Maps &amp; Legends" for sleeping (reference: sleep.cs, uw1 branch).</summary>
		void PlaySleepTheme();

		/// <summary>The respawner runs once - the original respawns while sleeping too.</summary>
		void RunRespawner();

		/// <summary>The carried lights burn for this many player ticks at once, with counter 0
		/// (UWInventoryModel.BurnTick).</summary>
		void BurnCarriedLights(int piTicks);

		/// <summary>A line of string block 1, our numbering.</summary>
		void AddGeneralMessage(int piIndex);

		/// <summary>The dream plays as a cutscene. False when no player can show it - the
		/// night then passes as black.</summary>
		bool TryPlayDream(int piCutscene);

		/// <summary>NO DREAM means two seconds of black in the view window - this is how the
		/// original shows that the night has passed.</summary>
		void ShowNightBlack(float pfSeconds);

		/// <summary>Standing in water - the original refuses a voluntary sleep for it and
		/// drowns whoever PASSES OUT there (see Sleep).</summary>
		bool IsInWater { get; }

		/// <summary>Standing on lava - the same, only that passing out there burns instead of
		/// drowning.</summary>
		bool IsOnLava { get; }

		/// <summary>Levitating or flying, which spares the sleeper the lava
		/// (MagicMotionAbilities mask 0x16 in SleepingOnDamagingSurface_ovr143_C7A).</summary>
		bool HasMagicMotion { get; }

		/// <summary>Damage while asleep - see fApplyDamagingSurface.</summary>
		void DamagePlayer(int piDamage);

		/// <summary>The doors the sleeper closes around himself - see
		/// FindAndCloseDoors_ovr153_15C4.</summary>
		void CloseDoorsAround(int piTileRadius);
	}

	/// <summary>
	/// Sleeping in the bedroll (original: sleep.Sleep, here the uw1 branch). Engine-free
	/// since 2026-09-18 (P3 of the engine separation), out of UWSleep.
	///
	/// IN UW1 IT ONLY WORKS WITH THE BEDROLL. The reference also knows a bed, but that is
	/// an object from Underworld 2, and sleeping while drunk (the reference's sleep method -2)
	/// is not wired up here. Intoxication is only reduced by a night's sleep (see fRecover).
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
	///   7. Otherwise more hours, seven to ten in total; longer when vitality is low.
	///      Then recovery, and hunger strikes.
	///   8. A dream of Garamon if one is due, otherwise two seconds of black (see fDream).
	///
	/// RECOVERY DEPENDS ON TWO NUMBERS. The fatigue factor is 2 + fatigue/2, at most
	/// 5 - whoever was tired recovers more. The food factor is one if the player is fed enough
	/// (hunger from 0x40) AND has a bedroll, otherwise zero. Vitality gets both
	/// multiplied, mana first six fixed points and then another
	/// (fatigue factor + 1) * food factor + fatigue factor - 1.
	///
	/// With food factor zero, sleep therefore heals NOTHING AT ALL - you just wake up rested.
	/// That is how the original does it.
	///
	/// The doors the sleeper closes around him are built (FindAndCloseDoors_ovr153_15C4, since
	/// 2026-09-21), and so is the monster respawner. The carried lights burn down while asleep
	/// (IUWSleepHost.BurnCarriedLights, since 2026-09-24).
	/// </summary>
	public static class UWSleepRules
	{
		/// <summary>The bedroll. From the reference's class calculation: major class 4,
		/// minor class 2, index 1 gives (4 &lt;&lt; 6) | (2 &lt;&lt; 4) | 1.</summary>
		public const int BedrollObjectId = 289;

		/// <summary>Player ticks a sleeping hour counts for the lights (0xB4).</summary>
		public const int SleepTicksPerHour = 180;

		// Messages from string block 1, in OUR numbering - i.e. one above each of the reference's
		// numbers (see UWStatusReport).
		private const int EnemiesNearbyMessage = 0x0F;
		private const int MakeCampMessage = 0x10;

		/// <summary>The Void: the original refuses a voluntary sleep there as it does on water
		/// or lava (Sleep_ovr143_D1F, label D44).</summary>
		private const int VoidLevel = 9;

		/// <summary>How far the sleeper closes doors around him. The original asks
		/// CheckIfWithin8TilesFromObject_ovr153_14C4 for it.</summary>
		private const int CloseDoorRadius = 8;

		/// <summary>What water does to whoever passes out in it: everything
		/// (SleepingOnDamagingSurface_ovr143_C7A hands DamageObject 0xFF).</summary>
		private const int DrownDamage = 0xFF;

		/// <summary>Lava burns for twelve plus ten per step of a six-sided roll.</summary>
		private const int LavaBurnBase = 12;

		private const int LavaBurnStep = 10;

		private const int LavaBurnDie = 6;
		private const int GoToSleepMessage = 0x11;
		private const int StarvingMessage = 0x12;
		private const int RestedMessage = 0x14;
		private const int CannotSleepHereMessage = 0x15;
		private const int InterruptedMessage = 0x16;

		/// <summary>From this hunger value on the player counts as fed enough to heal while sleeping.</summary>
		private const int WellFedHunger = 0x40;

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

		/// <summary>How long the view window stays black when there is no dream.
		/// </summary>
		private const float SleepFlashSeconds = 2f;

		/// <summary>
		/// Goes to sleep. Returns false if it is not possible at all - then a message
		/// is already in the message scroll.
		/// </summary>
		/// <param name="piDungeonLevel">The dungeon level, 1-based - the second dream only comes
		/// from the second level on.</param>
		public static bool Sleep(UWPlayerVitals pOVitals, int piDungeonLevel, IUWSleepHost pIHost,
			bool pbPassedOut = false)
		{
			if (pOVitals == null || pIHost == null)
				return false;

			// WHOEVER PASSES OUT IS NOT ASKED. The original's Sleep takes a method, and a
			// negative one - the alcohol - jumps past every refusal (Sleep_ovr143_D1F, label
			// D30: "jump when method < 0"). Only a voluntary sleep is refused on water, lava,
			// level 9 or with enemies about; whoever drinks himself down sleeps where he
			// stands, and then the ground has its say.
			if (!pbPassedOut)
			{
				if (!pIHost.CanSleepHere || piDungeonLevel == VoidLevel)
				{
					pIHost.AddGeneralMessage(CannotSleepHereMessage);

					return false;
				}

				pIHost.PlaySleepTheme();

				if (IsHostileNear(pIHost))
				{
					pIHost.AddGeneralMessage(EnemiesNearbyMessage);

					return false;
				}
			}
			else
			{
				pIHost.PlaySleepTheme();

				fApplyDamagingSurface(pIHost);
			}

			// The sleeper closes the doors around him (FindAndCloseDoors_ovr153_15C4 out of
			// Sleep), otherwise anything could walk in while he lies there.
			pIHost.CloseDoorsAround(CloseDoorRadius);

			pIHost.AddGeneralMessage(MakeCampMessage);
			pIHost.AddGeneralMessage(GoToSleepMessage);

			int liHours = 2 + UWRandom.Next(5);

			pOVitals.EndAllSpells();
			pOVitals.AdvanceClockHours(liHours);

			// Sleep ends the hallucination as well: the original clears PLAYER.DAT 0x61 bits 2-3
			// together with the active spells, right after the clock (read 2026-09-25). Ours
			// let it run on until then.
			pOVitals.EndHallucination();

			// THE LIGHTS BURN while asleep, right after each advance of the clock: 180 player
			// ticks an hour, counter zero, so 1 + hours * 180 / rate points (UpdateInventoryLightSources
			// called twice from the sleep routine, 2026-09-24). Ours burned nothing until then.
			pIHost.BurnCarriedLights(liHours * SleepTicksPerHour);

			// Poison takes the whole series at once: at strength three that is 3+2+1.
			if (pOVitals.Poison > 0)
			{
				pOVitals.ApplyDamage((pOVitals.Poison + 1) * pOVitals.Poison / 2, UWDamageTypes.Poison, 0);
				pOVitals.CurePoison();
			}

			if (pOVitals.CurrentHP <= 0)
				return true;

			if (TryAmbush(pIHost))
			{
				fInterrupt(pOVitals, pIHost);

				return true;
			}

			pIHost.RunRespawner();

			// Seven to ten hours in total. Whoever is wounded sleeps longer.
			int liRest = 7 + UWRandom.Next(4) - liHours;

			if (pOVitals.CurrentHP < 10)
				liRest += 1 + UWRandom.Next(2);

			if (liRest > 0)
				pOVitals.AdvanceClockHours(liRest);

			// The second burn comes whatever the rest is - at zero or below still the one point
			// the counter zero brings.
			pIHost.BurnCarriedLights(liRest * SleepTicksPerHour);

			// The food factor applies to both: recovery AND the probability
			// that a later dream is rolled instead of the one that is due.
			int liFood = pOVitals.Hunger < WellFedHunger ? 0 : 1;

			fRecover(pOVitals, pIHost, liFood);
			fDream(piDungeonLevel, liFood, pIHost);

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
		private static void fDream(int piDungeonLevel, int piFood, IUWSleepHost pIHost)
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

			if (liStage < 0 || UWGameFlags.GaramonBuried || !pIHost.TryPlayDream(FirstDreamCutscene + liStage))
			{
				pIHost.ShowNightBlack(SleepFlashSeconds);

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
		private static void fRecover(UWPlayerVitals pOVitals, IUWSleepHost pIHost, int piFood)
		{
			int liFatigue = System.Math.Min(5, 2 + (pOVitals.Fatigue / 2));

			pOVitals.ClearFatigue();

			if (pOVitals.Hunger == 0)
			{
				pIHost.AddGeneralMessage(StarvingMessage);
				pOVitals.ApplyDamage(2, UWDamageTypes.None, 0);
			}
			else
			{
				pOVitals.RegenerateOnSleep(liFatigue * piFood,
					6 + ((liFatigue + 1) * piFood) + liFatigue - 1);
			}

			// A night costs 24 plus RNG & 0x1F of hunger and takes 32 off the intoxication
			// (Sleep_ovr143_D1F, labels 1016 and 102F, read 2026-09-27). Ours took 2 plus 0 to 30
			// and 16, after the reference, until then.
			pOVitals.ChangeHunger(-24 - UWRandom.Next(0x20));
			pOVitals.ReduceIntoxication(UWPlayerVitals.SleepIntoxicationRelief);

			pIHost.AddGeneralMessage(RestedMessage - piFood);
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

		/// <summary>
		/// WHO KEEPS THE PLAYER FROM LYING DOWN (read 2026-09-24, Todo.md section 0b row 3):
		/// Sleep_ovr143_D1F asks CheckNearbyNPCHunger_ovr104_6F1 - a misleading name -, which runs
		/// over every creature on the tiles two around the player's (distance 0, radius 2, the
		/// player himself skipped) and answers "There are hostile creatures near!" for one whose
		/// goal is 5 or 4, or 9 with byte 0x19 bit 0 (target confirmed). NOTHING ELSE: not the
		/// attitude, not the line of sight - a wall between them does not help, a hostile that
		/// has not taken up the chase does not keep him awake. Until 2026-09-24 ours refused for any
		/// creature with a hostile attitude in the radius.
		/// </summary>
		public static bool BlocksSleep(int piGoal, bool pbTargetConfirmed)
		{
			return piGoal == GoalAttack || piGoal == GoalFour
				|| (piGoal == GoalStandAndFight && pbTargetConfirmed);
		}

		public static bool IsHostileNear(IUWSleepHost pIHost)
		{
			int liCount = pIHost.CollectCreatures(HostileCheckTileRadius);

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				UWCritterRecord lORecord = pIHost.CreatureRecord(liAt);

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
		///   - its path search (PathFindBetweenTiles_seg006_1477_12BB) finds the way to the
		///     sleeper's tile, AT LEAST TWO STEPS long - one standing next to him already is not
		///     moved.
		/// It then walks that way at once: every move trigger on the way that leads to a rune of
		/// warding goes off for it, from its own tile up to the one beside the sleeper. It is set
		/// down on the tile TWO STEPS before him (the path buffer's entry count - 2), on the
		/// fixed spot of that tile's type (TryGetAmbushSpot) if it fits there - otherwise the
		/// next one is tried -, confirms its target and heads for him. The sleep is interrupted.
		/// THE FIT is the original's item-fits-in-tile test with the creature's radius;
		/// ours asks the open half of the tile (UWTileQueries.IsSubTileInOpenHalf) and the
		/// creature's own blockers (UWCritter.PlaceAtSpot).
		/// Until 2026-09-24 ours interrupted for any hostile within two tiles and moved nobody.
		/// </summary>
		public static bool TryAmbush(IUWSleepHost pIHost)
		{
			int liCount = pIHost.CollectCreatures(AmbushTileRadius);
			UWTilePos lOPlayer = pIHost.PlayerTile;
			System.Collections.Generic.List<UWTilePos> lOPath = new System.Collections.Generic.List<UWTilePos>();

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				UWCritterRecord lORecord = pIHost.CreatureRecord(liAt);

				if (lORecord == null || lORecord.Attitude != HostileAttitude)
					continue;

				if (UWRandom.Next(2) == 0)
					continue;

				if (!WithinAmbushReach(lORecord.TileX - lOPlayer.X, lORecord.TileY - lOPlayer.Y,
					pIHost.CreatureTravelRange(liAt)))
					continue;

				if (!pIHost.TryGetCreaturePath(liAt, lOPlayer, lOPath) || lOPath.Count - 1 < AmbushMinSteps)
					continue;

				for (int liStep = 0; liStep < lOPath.Count - 1; liStep++)
					pIHost.FireWardTriggers(liAt, lOPath[liStep]);

				UWTilePos lOLanding = lOPath[lOPath.Count - 3];
				UWTile.TileTypeEnum leType = pIHost.TileTypeAt(lOLanding);

				if (!TryGetAmbushSpot(leType, out int liSubX, out int liSubY)
					|| !UWTileQueries.IsSubTileInOpenHalf(leType, liSubX, liSubY)
					|| !pIHost.PlaceCreature(liAt, lOLanding, liSubX, liSubY))
					continue;

				lORecord.TargetConfirmed = true;
				lORecord.SetDestination(lOPlayer.X, lOPlayer.Y, pIHost.PlayerFloorLevel);

				return true;
			}

			return false;
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
		private const int InterruptedFatigueRelief = 0x20;

		/// <summary>An interrupted sleep brings little: a bit of fatigue gone, but
		/// a lot of hunger.</summary>
		private static void fInterrupt(UWPlayerVitals pOVitals, IUWSleepHost pIHost)
		{
			pOVitals.ReduceFatigue(InterruptedFatigueRelief);
			pIHost.AddGeneralMessage(InterruptedMessage);
			pOVitals.ChangeHunger(-12 - UWRandom.Next(16));

			// Half a night's intoxication (label EB3); ours left it until 2026-09-27.
			pOVitals.ReduceIntoxication(UWPlayerVitals.InterruptedSleepIntoxicationRelief);
		}
		/// <summary>
		/// What the ground does to whoever passes out on it
		/// (SleepingOnDamagingSurface_ovr143_C7A, called from Sleep): in WATER it is over, the
		/// original hands DamageObject 255 points; on LAVA it costs twelve plus ten times a
		/// six-sided roll, unless the sleeper levitates or flies.
		/// </summary>
		private static void fApplyDamagingSurface(IUWSleepHost pIHost)
		{
			if (pIHost.IsInWater)
			{
				pIHost.DamagePlayer(DrownDamage);

				return;
			}

			if (pIHost.IsOnLava && !pIHost.HasMagicMotion)
				pIHost.DamagePlayer(LavaBurnBase + (LavaBurnStep * UWRandom.Next(LavaBurnDie)));
		}

		/// <summary>
		/// The poison takes effect: damage equal to the current value, and the value drops by one.

	}
}
