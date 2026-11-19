using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PITS OF CARNAGE (UW2 plan G6 part 4, 2026-11-17), from Underworld Exhumed's uw2 conv/BABLHACK.C (babl_hack
	/// modes 0, 1 and 2, set_me_inarena, place_pitfighter), event/WORLDEV.C (in_arena, arena_player_runs, do_teleport's
	/// test) and critter/CRITTIME.C (arena_opponent_runs). The player's side of a fight is in his record:
	/// UWPlayerRecordUw2.IsInPits, the five pit fighters' mobile indexes, the pit record (quest byte 1) and Jospur's debt
	/// (quest byte 5); remove_opponent is UWPlayerRecordUw2.RemovePitOpponent, the deaths' part is UWSpecialDeathsUw2's
	/// and UWDeathRulesUw2's.
	///
	/// TWO RUNTIME FLAGS, not saved, as the original keeps them in memory: the arrival (npp_func = set_me_inarena: the
	/// player is in the pits after his next new_player_pos, the conversation's teleport into the arena) and
	/// running_away (babl_hack 1 reads and clears it).
	///
	/// NOT BUILT: hack 31 (maybe_cheat_arena_fire, the rescue from the lava): the original looks the fighter's fine
	/// position up as a tile (Map_GetAddr past 63 gives a null tile, FARNULLTRAP reads the interrupt table), so what it
	/// does is not the arena's rule but the machine's memory; Exhumed leaves open whether it can ever work.
	/// </summary>
	public static class UWArenaRulesUw2
	{
		/// <summary>The arena's centre.</summary>
		public const int Centre = 0x1F;

		/// <summary>babl_hack's modes built here.</summary>
		public const int HackChallenge = 0;

		public const int HackRunningAway = 1;

		public const int HackSetUpFight = 2;

		/// <summary>place_pitfighter's creature: whoami 0x66, a temporary loner attacking the player.</summary>
		public const int PitFighterWhoAmI = 0x66;

		public const int StrongPower = 0x63;

		public const int HumanFirst = 0x75;

		public const int Human78 = 0x78;

		public const int Human7B = 0x7B;

		public const int GreatTroll = 0x5C;

		/// <summary>can_place's range for a pit fighter.</summary>
		public const int PlaceRange = 3;

		public const int PlaceFine = 3;

		/// <summary>Jospur's debt by the fighters placed (2, 3, 4, 5; else 0).</summary>
		public static int DebtFor(int piPlaced)
		{
			switch (piPlaced)
			{
				case 2: return 8;
				case 3: return 12;
				case 4: return 20;
				case 5: return 40;
				default: return 0;
			}
		}

		/// <summary>npp_func = set_me_inarena: the next arrival puts the player in the pits.</summary>
		public static bool InArenaOnArrival { get; set; }

		/// <summary>running_away: set when the player ran from a fight, read and cleared by babl_hack 1.</summary>
		public static bool RunningAway { get; set; }

		/// <summary>babl_hack 1: 1 once after running away.</summary>
		public static int TakeRunningAway()
		{
			if (!RunningAway)
				return 0;

			RunningAway = false;

			return 1;
		}

		/// <summary>set_me_inarena, run at the arrival: in the pits.</summary>
		public static bool Arrive(UWPlayerFile pOFile)
		{
			if (!InArenaOnArrival || pOFile == null)
				return false;

			InArenaOnArrival = false;
			pOFile.Record[UWPlayerRecordUw2.PitsByteOffset] |= UWPlayerRecordUw2.PitsBit;

			return true;
		}

		/// <summary>babl_hack 0: the talker challenges the player - pit fighter 0, in the arena after the next arrival.</summary>
		public static void Challenge(UWPlayerFile pOFile, int piTalkerIndex)
		{
			if (pOFile == null)
				return;

			InArenaOnArrival = true;
			fSetFighter(pOFile, 0, piTalkerIndex);
		}

		/// <summary>
		/// babl_hack 2(power, side, count): the fighters from the centre towards corner side (0 +x +y, 1 -x +y, 2 -x -y,
		/// 3 +x -y); with 5 the first, always strong, 6 x and 4 y off the centre (its index lands in the byte after the
		/// list, pit_fighters[5], as the original writes it), the rest in a triangle at offsets 4 to 6. A fighter that
		/// cannot be placed uses its place all the same (its index 0). Jospur's debt by the count placed; the player in the
		/// arena after the next arrival. Returns how many were placed. pfPlace makes one (place_pitfighter) and returns
		/// its mobile index, 0 when it could not.
		/// </summary>
		public static int SetUpFight(UWPlayerFile pOFile, int piPower, int piSide, int piCount, Func<int, int, int, int> pfPlace)
		{
			if (pOFile == null || pfPlace == null)
				return 0;

			int liDx = piSide == 1 || piSide == 2 ? -1 : 1;
			int liDy = piSide > 1 ? -1 : 1;
			int liCount = 0;
			int liN = piCount;

			if (liN == 5)
			{
				int liIndex = pfPlace(StrongPower, (liDx * 6) + Centre, (liDy * 4) + Centre);

				if (liIndex > 0)
					liCount++;

				fSetFighter(pOFile, liN, liIndex);
				liN--;
			}

			bool lbDone = liN <= 0;

			for (int liRow = 0; liRow < 3 && !lbDone; liRow++)
			{
				for (int liAt = 0; liAt <= liRow; liAt++)
				{
					int liDepth = liRow - liAt;
					int liIndex = pfPlace(piPower, (liDx * (liAt + 4)) + Centre, (liDy * (liDepth + 4)) + Centre);

					if (liIndex > 0)
						liCount++;

					liN--;
					fSetFighter(pOFile, liN, liIndex);

					if (liN == 0)
					{
						lbDone = true;

						break;
					}
				}
			}

			UWPlayerRecordUw2.SetQuest(pOFile, JospurDebtQuest, DebtFor(liCount));
			InArenaOnArrival = true;

			return liCount;
		}

		/// <summary>QB_JOSPUR_DEBT and QB_PIT_RECORD as quests (128 + n).</summary>
		public const int JospurDebtQuest = 0x80 + 5;

		public const int PitRecordQuest = 0x80 + 1;

		/// <summary>XC_PIT_KILLS.</summary>
		public const int PitKillsClock = 14;

		/// <summary>pit_fighters[n] (n up to 5: the sixth is the byte after the list, as the original writes it).</summary>
		private static void fSetFighter(UWPlayerFile pOFile, int piAt, int piIndex)
		{
			if (piAt >= 0 && piAt <= UWPlayerRecordUw2.PitFighterCount)
				pOFile.Record[UWPlayerRecordUw2.PitFightersOffset + piAt] = (byte)piIndex;
		}

		/// <summary>place_pitfighter's kind and strength from its rolls (rand() each): one in 16 a human 0x75 to 0x77, else
		/// 0x78 or 0x79; with chance power in 3 strong, and then two in five a human 0x7B or a great troll instead, not
		/// strong.</summary>
		public static int RollFighter(int piPower, Func<int> pfRand, out bool pbStrong)
		{
			int liItem = pfRand() % 16 != 0 ? (pfRand() % 2) + Human78 : (pfRand() % 3) + HumanFirst;

			pbStrong = false;

			if (pfRand() % 3 < piPower)
			{
				pbStrong = true;

				int liRoll = pfRand() % 5;

				if (liRoll < 2)
				{
					liItem = liRoll != 0 ? Human7B : GreatTroll;
					pbStrong = false;
				}
			}

			return liItem;
		}

		/// <summary>place_pitfighter's record once made: home and fine spot here, whoami 0x66, strong as rolled,
		/// temporary, hostile, goal 5 on the player, a loner.</summary>
		public static void MakePitFighter(UWNpc pONpc, int piX, int piY, bool pbStrong)
		{
			UWCritterRecord lORecord = new UWCritterRecord(pONpc);

			pONpc.Quality = (ushort)((pONpc.Quality & ~0x3F) | (piX & 0x3F));
			pONpc.Owner = (ushort)((pONpc.Owner & ~0x3F) | (piY & 0x3F));
			pONpc.NPCwhoami = PitFighterWhoAmI;

			if (pONpc.RawNpcBytes != null && pONpc.RawNpcBytes.Length > UWNpcRecordCodec.WhoAmIByte)
				pONpc.RawNpcBytes[UWNpcRecordCodec.WhoAmIByte] = PitFighterWhoAmI;

			lORecord.IsStrong = pbStrong;
			pONpc.NPCSpawned = true;
			lORecord.Attitude = 0;
			lORecord.Goal = 5;
			lORecord.GTarg = 1;
			lORecord.AttitudeLocked = true;
		}

		/// <summary>
		/// place_pitfighter(power, x, y) in the world: the kind rolled, can_place at 3/3 of the tile on its floor (as a
		/// flier, range 3), then CreateObj and creature_obj_init (UWObjectFactory, InitialiseAsNew, as the summons do),
		/// MakePitFighter and the drop. Returns its mobile index, 0 when it could not be placed.
		/// </summary>
		public static int PlacePitFighter(IUWWorld pIWorld, int piPower, int piX, int piY)
		{
			UWLevel lOLevel = pIWorld != null ? pIWorld.Level : null;
			UWTile lOTile = lOLevel != null ? lOLevel.GetTile(piX, piY) : null;

			if (lOTile == null || pIWorld.Data == null || pIWorld.Data.ObjectClassProperties == null)
				return 0;

			int liItem = RollFighter(piPower, () => UWRandom.Next(UWOriginalRandom.Range), out bool lbStrong);
			int liZ = (lOTile.FloorHeight >> 4) << 3;

			if (!pIWorld.CanPlace(liItem, (piX << 3) + PlaceFine, (piY << 3) + PlaceFine, liZ, true, PlaceRange))
				return 0;

			if (!(UWObjectFactory.Create(liItem, pIWorld.Data) is UWNpc lONpc))
				return 0;

			int liVitality = pIWorld.Data.ObjectClassProperties.TryGetCritter(liItem, out UWObjectClassProperties.Critter lORow) ? lORow.Vitality : 0;
			UWCritterRecord lORecord = new UWCritterRecord(lONpc);

			lORecord.InitialiseAsNew(liVitality, UWRandom.Next(UWCritterRecord.NewCreatureHitPointRollRange));
			lORecord.TileX = piX;
			lORecord.TileY = piY;
			lONpc.NPC_HP = (byte)liVitality;
			lONpc.XPos = PlaceFine;
			lONpc.YPos = PlaceFine;
			MakePitFighter(lONpc, piX, piY, lbStrong);

			if (!pIWorld.DropObjectAt(lONpc, UWUnits.SubTileToWorldAxis(piX, PlaceFine), UWUnits.SubTileToWorldAxis(piY, PlaceFine)))
				return 0;

			return Math.Max(0, UWAreaWalk.MobileIndexOf(lOLevel, lONpc));
		}

		/// <summary>mpos (sys/MISCUTIL.C:62): the direction of (x, y) in eighths, 0 along +y, 2 +x; a component less than half
		/// the other counts as none.</summary>
		public static int MPos(sbyte piX, sbyte piY)
		{
			int liAx = Math.Abs((int)piX);
			int liAy = Math.Abs((int)piY);

			if (liAx / 2 > liAy)
				return piX > 0 ? 2 : 6;

			if (liAy / 2 > liAx)
				return piY > 0 ? 0 : 4;

			if (piX < 0)
				return ((piY > 0) ? 2 : 0) + 5;

			return ((piY < 0) ? 2 : 0) + 1;
		}

		private static readonly int[] miLimits = { 0xF, 0x10, 0xE, 0xF };

		/// <summary>in_arena: a ring round the centre, at least 3 off on both axes, its outer edge by the quarter.</summary>
		public static bool InArena(int piX, int piY)
		{
			int liX = piX - Centre;
			int liY = piY - Centre;
			int liQuarter = MPos((sbyte)liX, (sbyte)liY) >> 1;

			liX = Math.Abs(liX);
			liY = Math.Abs(liY);

			if (liX < 3 || liY < 3)
				return false;

			return miLimits[liQuarter] >= liX && miLimits[liQuarter] >= liY;
		}

		/// <summary>do_teleport's test for the player in the pits: to another level, out of the arena, or into another quarter
		/// of it is running away.</summary>
		public static bool TeleportRuns(UWPlayerFile pOFile, int piDestLevel, int piCurrentLevel, int piX, int piY, int piOldX, int piOldY)
		{
			if (!UWPlayerRecordUw2.IsInPits(pOFile))
				return false;

			return piDestLevel != piCurrentLevel || !InArena(piX, piY)
				|| (MPos((sbyte)(piX - Centre), (sbyte)(piY - Centre)) >> 1) != (MPos((sbyte)(piOldX - Centre), (sbyte)(piOldY - Centre)) >> 1);
		}

		/// <summary>
		/// arena_player_runs: running_away set; every listed fighter stands down (attitude 1, no last hit, goal 1), the
		/// first stays listed for the talk and the rest leave the list; then the first is talked to (pfTalk) and leaves it;
		/// out of the pits, the pit record and Jospur's debt 0. pOLevel finds the fighters by their indexes. Returns the
		/// first's index, 0 for none.
		/// </summary>
		public static int PlayerRuns(UWPlayerFile pOFile, UWLevel pOLevel, Action<UWNpc> pfTalk)
		{
			if (!UWPlayerRecordUw2.IsInPits(pOFile))
				return 0;

			RunningAway = true;

			int liFirst = -1;

			for (int liAt = 0; liAt < UWPlayerRecordUw2.PitFighterCount; liAt++)
			{
				UWNpc lOWarrior = fFighter(pOFile, pOLevel, liAt);

				if (lOWarrior == null)
					continue;

				UWCritterRecord lORecord = new UWCritterRecord(lOWarrior);

				lORecord.Attitude = 1;
				lORecord.Attacker = 0;
				lORecord.Goal = 1;

				if (liFirst == -1)
					liFirst = liAt;
				else
					fSetFighter(pOFile, liAt, 0);
			}

			int liFirstIndex = 0;

			if (liFirst > -1)
			{
				liFirstIndex = UWPlayerRecordUw2.PitFighter(pOFile, liFirst);

				if (pfTalk != null)
					pfTalk(fFighter(pOFile, pOLevel, liFirst));

				fSetFighter(pOFile, liFirst, 0);
			}

			pOFile.Record[UWPlayerRecordUw2.PitsByteOffset] &= unchecked((byte)~UWPlayerRecordUw2.PitsBit);
			UWPlayerRecordUw2.SetQuest(pOFile, PitRecordQuest, 0);
			UWPlayerRecordUw2.SetQuest(pOFile, JospurDebtQuest, 0);

			return liFirstIndex;
		}

		/// <summary>arena_opponent_runs: the pit record one up (its best in X clock 14), the opponent flees (goal 6) and leaves
		/// the fight (remove_opponent).</summary>
		public static void OpponentRuns(UWPlayerFile pOFile, UWNpc pONpc, int piIndex)
		{
			if (pOFile == null || pONpc == null)
				return;

			int liRecord = (UWPlayerRecordUw2.GetQuest(pOFile, PitRecordQuest) + 1) & 0xFF;

			UWPlayerRecordUw2.SetQuest(pOFile, PitRecordQuest, liRecord);

			if (liRecord > UWPlayerRecordUw2.XClock(pOFile, PitKillsClock))
				UWPlayerRecordUw2.SetXClock(pOFile, PitKillsClock, liRecord);

			new UWCritterRecord(pONpc).Goal = 6;
			UWPlayerRecordUw2.RemovePitOpponent(pOFile, piIndex);
		}

		/// <summary>Obj_IntTMem(pit_fighters[n]): the listed creature, null for an empty place or a slot without one.</summary>
		private static UWNpc fFighter(UWPlayerFile pOFile, UWLevel pOLevel, int piAt)
		{
			int liIndex = UWPlayerRecordUw2.PitFighter(pOFile, piAt);

			return pOLevel != null && liIndex > 0 && liIndex < pOLevel.Masterlist.Count ? pOLevel.Masterlist[liIndex] as UWNpc : null;
		}
	}
}
