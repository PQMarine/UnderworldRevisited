using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S SCHEDULER (UW2 plan G6 part 1, 2026-11-16), from Underworld Exhumed's uw2 event/SCHEDULE.C and
	/// event/SCDEVENT.C. SCD.ARK holds a schedule per X clock (block 0 the time of day in 72 steps, block 1 the castle
	/// plot, ... block 15 the world's rules); each keeps, per level, the time reached and the next row not run. Bringing
	/// a block up to date runs every row whose time is passed, for the current level only (a row names one level, any
	/// level 0xFF, or a world 0xF6 + n), so a level the player was away from catches up when he comes back.
	///
	/// WHEN: SetAllClocks after a conversation (free_converse), a level change (ChangeLevel), passing time (pass_time)
	/// and the clock hack 37; StepDay on duration_check's hour (PLAYTIME.C:155). Block 15 is rescanned from row 0 every
	/// time, its rows being tests (event 10) with disabled rows after them.
	///
	/// THE EVENTS (Sched_DoEvent): 0 and 6 nothing, 1 goal, 2 teleport, 3 kill, 4 a quest bit, 5 the scheduled triggers
	/// on a square, 7 a hack by its byte (1 garg, 2 soldier, 3 freeze, 4 closing doors, 6 nystul, 7 gotha), 8 attitude,
	/// 9 a numbered variable, 10 a test that runs the disabled rows after it, 11 remove. Most act on critters chosen by
	/// gronk_critid: by whoami, by race (loners skipped), one object, or all.
	/// </summary>
	public static class UWScheduleRulesUw2
	{
		public const int DayBlock = 0;

		/// <summary>The world rules' block, rescanned from row 0 each time.</summary>
		public const int WorldRulesBlock = 15;

		public const int DaySteps = UWPlayerRecordUw2.DaySteps;

		/// <summary>Eight levels to a world (PlayerLevel - 1) / 8.</summary>
		public const int LevelsPerWorld = 8;

		/// <summary>Sched_DoEvent's results.</summary>
		public const int Ran = 0;

		public const int Deleted = 4;

		public const int NotHere = 5;

		public const int UnknownEvent = 6;

		public const int EventCount = 12;

		public const int EventGoal = 1;

		public const int EventTeleport = 2;

		public const int EventKill = 3;

		public const int EventQuestBit = 4;

		public const int EventTrigger = 5;

		public const int EventHack = 7;

		public const int EventAttitude = 8;

		public const int EventVariable = 9;

		public const int EventTest = 10;

		public const int EventRemove = 11;

		public const int HackGarg = 1;

		public const int HackSoldier = 2;

		public const int HackFreeze = 3;

		public const int HackDoors = 4;

		public const int HackNystul = 6;

		public const int HackGotha = 7;

		/// <summary>gronk_critid's selections.</summary>
		public const int ByWhoAmI = 0;

		public const int ByRace = 1;

		public const int ByObject = 2;

		public const int AllCritters = 3;

		/// <summary>The scheduled triggers: class 0xA, in-class 0xC (ev_trigger).</summary>
		public const int ScheduledTriggerInClass = 0xC;

		/// <summary>XC_CHANGED, counted up by ev_kill's odd case.</summary>
		public const int ChangedClock = 15;

		/// <summary>check_alert: within this many squares the player could see it.</summary>
		public const int AlertRange = 8;

		/// <summary>DoClosingDoors' chance in ten.</summary>
		public const int DoorChanceInTen = 3;

		/// <summary>freeze's change_terrain: the wall kept (0x3F) and the type 0x10.</summary>
		private const int KeepWall = 0x3F;

		private const int FreezeType = 0x10;

		// ------------------------------------------------- The clocks (SCHEDULE.C)

		/// <summary>Sched_SetAllClocks: every block's clock for the current level from its X clock (block 0 wrapping at
		/// 72), the rows due run; then the migration queue into block 0. Returns the first error, else 0.</summary>
		public static int SetAllClocks(UWScheduleBookUw2 pOBook, IUWScheduleHost pIHost, bool pbRun = true)
		{
			if (pOBook == null || pIHost == null || pIHost.Record == null)
				return 0;

			// get_workspace: the schedules running (a scheduled trigger's hack 37) refuse a second pass with 3.
			if (pOBook.IsRunning)
				return WorkspaceBusy;

			pOBook.IsRunning = true;

			try
			{
				return fSetAllClocks(pOBook, pIHost, pbRun);
			}
			finally
			{
				pOBook.IsRunning = false;
			}
		}

		/// <summary>Sched_SetAllClocks' refusal while the workspace is taken.</summary>
		public const int WorkspaceBusy = 3;

		private static int fSetAllClocks(UWScheduleBookUw2 pOBook, IUWScheduleHost pIHost, bool pbRun)
		{
			int liError = 0;

			for (int liBlock = 0; liBlock < UWScheduleUw2.ScheduleCount && liError == 0; liBlock++)
			{
				if (pOBook.Blocks[liBlock] == null)
					continue;

				int liClock = UWPlayerRecordUw2.XClock(pIHost.Record, liBlock);

				liError = liBlock == DayBlock
					? WrapTime(pOBook, liBlock, liClock, DaySteps, pbRun, pIHost)
					: SetTime(pOBook, liBlock, liClock, pbRun, pIHost);

				DoMigrations(pOBook);
			}

			return liError;
		}

		/// <summary>duration_check's hour, after X clock 0 stepped on: block 0 alone (Sched_WrapTime with mode 1).</summary>
		public static int StepDay(UWScheduleBookUw2 pOBook, IUWScheduleHost pIHost)
		{
			if (pOBook == null || pIHost == null || pIHost.Record == null || pOBook.Blocks[DayBlock] == null)
				return 0;

			if (pOBook.IsRunning)
				return WorkspaceBusy;

			pOBook.IsRunning = true;

			try
			{
				int liError = WrapTime(pOBook, DayBlock, UWPlayerRecordUw2.XClock(pIHost.Record, DayBlock), DaySteps, true, pIHost);

				DoMigrations(pOBook);

				return liError;
			}
			finally
			{
				pOBook.IsRunning = false;
			}
		}

		/// <summary>Sched_SetTime: the level's clock on this block to piTime; with pbRun the rows due run. A clock going
		/// back restarts from row 0 when running, else only moves the next row back.</summary>
		public static int SetTime(UWScheduleBookUw2 pOBook, int piBlock, int piTime, bool pbRun, IUWScheduleHost pIHost)
		{
			UWScheduleUw2 lOBlock = pOBook.Blocks[piBlock];
			int liLevel = pIHost.LevelNumber;

			if (lOBlock == null || liLevel < 0 || liLevel >= UWScheduleUw2.ClockCount)
				return 0;

			int liOld = lOBlock.ClockTime[liLevel];

			lOBlock.ClockTime[liLevel] = piTime;

			if (liOld == piTime)
				return 0;

			if (liOld > piTime)
			{
				if (!pbRun)
				{
					fMoveNextBack(lOBlock, liLevel);

					return 0;
				}

				lOBlock.ClockNextRow[liLevel] = 0;
			}

			return fRunDueRows(pOBook, piBlock, pbRun, pIHost);
		}

		/// <summary>Sched_WrapTime: a cyclic clock; one that has to wrap runs to the end of the cycle first.</summary>
		public static int WrapTime(UWScheduleBookUw2 pOBook, int piBlock, int piTime, int piSpan, bool pbRun, IUWScheduleHost pIHost)
		{
			UWScheduleUw2 lOBlock = pOBook.Blocks[piBlock];
			int liLevel = pIHost.LevelNumber;

			if (lOBlock == null || liLevel < 0 || liLevel >= UWScheduleUw2.ClockCount)
				return 0;

			piTime %= piSpan;

			if (lOBlock.ClockTime[liLevel] > piTime)
			{
				int liResult = SetTime(pOBook, piBlock, piSpan - 1, pbRun, pIHost);

				if (liResult != 0)
					return liResult;

				lOBlock.ClockTime[liLevel] = 0;
				lOBlock.ClockNextRow[liLevel] = 0;
			}

			return SetTime(pOBook, piBlock, piTime, pbRun, pIHost);
		}

		/// <summary>FindSCDRowsToExecute: from the level's next row (block 15 from row 0) every row up to the clock's time;
		/// a row that deleted itself is looked at again; any other result than ran, deleted or not here stops.</summary>
		private static int fRunDueRows(UWScheduleBookUw2 pOBook, int piBlock, bool pbRun, IUWScheduleHost pIHost)
		{
			UWScheduleUw2 lOBlock = pOBook.Blocks[piBlock];
			int liLevel = pIHost.LevelNumber;
			int liRow = piBlock == WorldRulesBlock ? 0 : lOBlock.ClockNextRow[liLevel];

			while (liRow < lOBlock.Rows.Count)
			{
				if (lOBlock.Rows[liRow].Time > lOBlock.ClockTime[liLevel])
					break;

				if (pbRun)
				{
					int liResult = DoEvent(pOBook, piBlock, liRow, pIHost);

					if (liResult == Deleted)
						liRow--;
					else if (liResult != Ran && liResult != NotHere)
					{
						lOBlock.ClockNextRow[liLevel] = liRow;

						return liResult;
					}
				}

				liRow++;
			}

			lOBlock.ClockNextRow[liLevel] = liRow;

			return 0;
		}

		/// <summary>The clock went back without running: the next row back to the first after the clock's time.</summary>
		private static void fMoveNextBack(UWScheduleUw2 pOBlock, int piLevel)
		{
			int liRow = pOBlock.ClockNextRow[piLevel];

			while (liRow > 0)
			{
				if (liRow < pOBlock.Rows.Count && pOBlock.Rows[liRow].Time <= pOBlock.ClockTime[piLevel])
				{
					liRow++;

					break;
				}

				liRow--;
			}

			pOBlock.ClockNextRow[piLevel] = liRow;
		}

		/// <summary>Sched_InsertLong for one row: after the rows of the same time; every clock past its time bumps the
		/// CURRENT level's next row (the original's slip, kept); with pbRun a row already due runs at once.</summary>
		public static int Insert(UWScheduleBookUw2 pOBook, int piBlock, UWScheduleUw2.Row pORow, bool pbRun, IUWScheduleHost pIHost)
		{
			UWScheduleUw2 lOBlock = pOBook.Blocks[piBlock];
			int liLevel = pIHost != null ? pIHost.LevelNumber : 0;

			if (lOBlock == null)
				return 0;

			int liAt = lOBlock.Rows.Count;

			while (liAt > 0 && pORow.Time < lOBlock.Rows[liAt - 1].Time)
				liAt--;

			lOBlock.Rows.Insert(liAt, pORow);

			if (liLevel >= 0 && liLevel < UWScheduleUw2.ClockCount)
			{
				for (int liClock = 0; liClock < UWScheduleUw2.ClockCount; liClock++)
				{
					if (lOBlock.ClockTime[liClock] > pORow.Time)
						lOBlock.ClockNextRow[liLevel]++;
				}

				if (pbRun && lOBlock.ClockTime[liLevel] > pORow.Time)
				{
					int liResult = DoEvent(pOBook, piBlock, liAt, pIHost);

					if (liResult != Ran && liResult != Deleted)
						return liResult;
				}
			}

			return 0;
		}

		/// <summary>Sched_Delete: the row out, every clock's next row past it one back.</summary>
		public static void Delete(UWScheduleUw2 pOBlock, int piRow)
		{
			pOBlock.Rows.RemoveAt(piRow);

			for (int liClock = 0; liClock < UWScheduleUw2.ClockCount; liClock++)
			{
				if (pOBlock.ClockNextRow[liClock] > piRow)
					pOBlock.ClockNextRow[liClock]--;
			}
		}

		/// <summary>Sched_Migrate: a row queued for block 0; false when the sixteen are taken.</summary>
		public static bool Migrate(UWScheduleBookUw2 pOBook, UWScheduleUw2.Row pORow)
		{
			if (pOBook.Migrations.Count >= UWScheduleBookUw2.MigrationLimit)
				return false;

			pOBook.Migrations.Add(UWScheduleBookUw2.Copy(pORow));

			return true;
		}

		/// <summary>do_migrations: the queue into block 0 without running (Sched_Insert(row, 0)).</summary>
		public static void DoMigrations(UWScheduleBookUw2 pOBook)
		{
			if (pOBook.Migrations.Count == 0 || pOBook.Blocks[DayBlock] == null)
				return;

			foreach (UWScheduleUw2.Row lORow in pOBook.Migrations)
				Insert(pOBook, DayBlock, lORow, false, null);

			pOBook.Migrations.Clear();
		}

		// ------------------------------------------------- One row (SCDEVENT.C)

		/// <summary>Whether a row's level byte takes this level: itself, 0xFF any, 0xF6 + its world.</summary>
		public static bool RowTakesLevel(int piRowLevel, int piLevelNumber)
		{
			return piRowLevel == piLevelNumber || piRowLevel == UWScheduleUw2.AnyLevel
				|| (piLevelNumber - 1) / LevelsPerWorld == piRowLevel - UWScheduleUw2.FirstWorld;
		}

		/// <summary>Sched_DoEvent: NotHere for another level or a disabled row, UnknownEvent above 11; a once row is
		/// deleted after it ran (Deleted).</summary>
		public static int DoEvent(UWScheduleBookUw2 pOBook, int piBlock, int piRow, IUWScheduleHost pIHost)
		{
			UWScheduleUw2 lOBlock = pOBook.Blocks[piBlock];
			UWScheduleUw2.Row lORow = lOBlock.Rows[piRow];

			if (!RowTakesLevel(lORow.Level, pIHost.LevelNumber) || lORow.Event < 0)
				return NotHere;

			if (lORow.Event >= EventCount)
				return UnknownEvent;

			int liResult = fRun(pOBook, piBlock, piRow, lORow, pIHost);

			if (lORow.Once != 0)
			{
				int liNow = lOBlock.Rows.IndexOf(lORow);

				if (liNow >= 0)
					Delete(lOBlock, liNow);

				return Deleted;
			}

			return liResult;
		}

		private static int fRun(UWScheduleBookUw2 pOBook, int piBlock, int piRow, UWScheduleUw2.Row pORow, IUWScheduleHost pIHost)
		{
			byte[] lyP = pORow.Parameters;

			switch (pORow.Event)
			{
				case EventGoal:
					fGronk(fWord(lyP, 0), pIHost, lONpc => GronkChangeGoal(lONpc, lyP[2], lyP[3]));
					break;

				case EventTeleport:
					fGronk(fWord(lyP, 2), pIHost, lONpc => fTeleport(pOBook, pORow, lONpc, pIHost));
					break;

				case EventKill:
					fGronk(fWord(lyP, 0), pIHost, lONpc => fKill(pOBook, pORow, lONpc, pIHost));
					break;

				case EventQuestBit:
					UWPlayerRecordUw2.SetQuest(pIHost.Record, lyP[0], lyP[1]);
					break;

				case EventTrigger:
					FireScheduledTriggers((sbyte)lyP[0], (sbyte)lyP[1], pIHost);
					break;

				case EventHack:
					fHack(pORow, pIHost);
					break;

				case EventAttitude:
					fGronk(fWord(lyP, 0), pIHost, lONpc => GronkAttitude(lONpc, lyP[2]));
					break;

				case EventVariable:
					UWTrapRulesUw2.SetNumbered(pIHost.Record, fWord(lyP, 0), lyP[2], fWord(lyP, 3));
					break;

				case EventTest:
					fTest(pOBook, piBlock, piRow, pIHost);
					break;

				case EventRemove:
					fGronk(fWord(lyP, 0), pIHost, lONpc => pIHost.World != null && pIHost.World.Remove(lONpc));
					break;
			}

			return Ran;
		}

		private static int fWord(byte[] pyData, int piAt)
		{
			return pyData[piAt] | (pyData[piAt + 1] << 8);
		}

		/// <summary>ev_checkvar: count numbered variables from var combined by op, against value (inverted by invert); when
		/// it passes, the disabled rows after it run in turn, each enabled for the call and disabled again unless it is a
		/// once row, up to the next test.</summary>
		private static void fTest(UWScheduleBookUw2 pOBook, int piBlock, int piRow, IUWScheduleHost pIHost)
		{
			UWScheduleUw2 lOBlock = pOBook.Blocks[piBlock];
			byte[] lyP = lOBlock.Rows[piRow].Parameters;
			int liIndex = fWord(lyP, 0);
			int liUpper = liIndex + lyP[2] - 1;
			int liOp = lyP[3];
			bool lbInvert = lyP[4] != 0;
			int liWanted = (short)fWord(lyP, 5);
			int liValue = UWPlayerRecordUw2.GetNumbered(pIHost.Record, liIndex);

			while (++liIndex <= liUpper)
				liValue = UWTrapRulesUw2.DoMathOp(liValue, liOp, UWPlayerRecordUw2.GetNumbered(pIHost.Record, liIndex));

			if ((liValue != liWanted) == !lbInvert)
				return;

			for (int liAt = piRow + 1; liAt < lOBlock.Rows.Count && lOBlock.Rows[liAt].Event < 0; liAt++)
			{
				UWScheduleUw2.Row lORow = lOBlock.Rows[liAt];
				bool lbDisableAgain = lORow.Once == 0;

				lORow.Event = -lORow.Event;

				if (DoEvent(pOBook, piBlock, liAt, pIHost) == Deleted)
					liAt--;

				if (lbDisableAgain)
					lORow.Event = -lORow.Event;

				if (liAt < 0 || -lOBlock.Rows[liAt].Event == EventTest)
					break;
			}
		}

		// ------------------------------------------------- The critters (gronk_critid)

		/// <summary>gronk_critid with every match (loop set): mode in the low byte (0 whoami, 1 race with loners skipped,
		/// 2 the object of that index, 3 all), the matched value in the high byte. Over a snapshot of the active list, as
		/// a callback may take a critter out of it.</summary>
		private static void fGronk(int piCritters, IUWScheduleHost pIHost, Func<UWNpc, bool> pfDo)
		{
			UWLevel lOLevel = pIHost.World != null ? pIHost.World.Level : null;

			foreach (UWNpc lONpc in Select(lOLevel, piCritters, pIHost.Properties))
				pfDo(lONpc);
		}

		/// <summary>The critters gronk_critid chooses, in the active list's order.</summary>
		public static List<UWNpc> Select(UWLevel pOLevel, int piCritters, UWObjectClassProperties pOProperties)
		{
			List<UWNpc> lOChosen = new List<UWNpc>();

			if (pOLevel == null || pOLevel.Masterlist == null)
				return lOChosen;

			int liMode = piCritters & 0xFF;
			int liFilter = (piCritters >> 8) & 0xFF;

			if (liMode == ByObject)
			{
				if (liFilter < pOLevel.Masterlist.Count && pOLevel.Masterlist[liFilter] is UWNpc lOOne)
					lOChosen.Add(lOOne);

				return lOChosen;
			}

			if (pOLevel.ActiveMobiles == null)
				return lOChosen;

			foreach (int liIndex in pOLevel.ActiveMobiles)
			{
				UWNpc lONpc = liIndex > 0 && liIndex < pOLevel.Masterlist.Count ? pOLevel.Masterlist[liIndex] as UWNpc : null;

				if (lONpc == null)
					continue;

				switch (liMode)
				{
					case ByWhoAmI:
						if (lONpc.NPCwhoami == liFilter)
							lOChosen.Add(lONpc);
						break;

					case ByRace:
						if (UWObjectIds.IsCreature(lONpc.ID) && RaceOf(lONpc, pOProperties) == liFilter && !new UWCritterRecord(lONpc).AttitudeLocked)
							lOChosen.Add(lONpc);
						break;

					case AllCritters:
						if (UWObjectIds.IsCreature(lONpc.ID))
							lOChosen.Add(lONpc);
						break;
				}
			}

			return lOChosen;
		}

		/// <summary>Creature[].race: CRIT.DAT byte 9 (GeneralType); -1 without a row.</summary>
		public static int RaceOf(UWNpc pONpc, UWObjectClassProperties pOProperties)
		{
			return pOProperties != null && pOProperties.TryGetCritter(pONpc.ID, out UWObjectClassProperties.Critter lOCritter) ? lOCritter.GeneralType : -1;
		}

		private static bool fIsFlier(UWNpc pONpc, UWObjectClassProperties pOProperties)
		{
			return pOProperties != null && pOProperties.TryGetCritter(pONpc.ID, out UWObjectClassProperties.Critter lOCritter) && lOCritter.IsFlier;
		}

		/// <summary>critter_set_goal: a guarding critter (goal 4) remembers it as its old goal.</summary>
		public static bool GronkChangeGoal(UWNpc pONpc, int piGoal, int piTarget)
		{
			UWCritterRecord lORecord = new UWCritterRecord(pONpc);

			if (lORecord.Goal == 4)
				lORecord.BackupGoal = lORecord.Goal;

			lORecord.Goal = piGoal & UWNpcRecordCodec.GoalMask;
			lORecord.GTarg = piTarget;

			return false;
		}

		/// <summary>gronkify_attitude: the attitude; any but hostile forgets who last hit it.</summary>
		public static bool GronkAttitude(UWNpc pONpc, int piAttitude)
		{
			UWCritterRecord lORecord = new UWCritterRecord(pONpc);

			lORecord.Attitude = piAttitude & UWNpcRecordCodec.AttitudeMask;

			if (piAttitude != 0)
				lORecord.Attacker = 0;

			return false;
		}

		/// <summary>
		/// gronkify_teleport: only where the player sees neither the critter nor the destination unless the row's unseen
		/// byte says so; teleport_critter (UWConversationRulesUw2.TeleportCritter) and with sethome the destination its
		/// home. A move that did not happen with a retry delay queues a once copy for the day clock's time plus the delay.
		/// Row bytes from 5: x, y, critters (word), level, unseen, sethome, retry.
		/// </summary>
		private static bool fTeleport(UWScheduleBookUw2 pOBook, UWScheduleUw2.Row pORow, UWNpc pONpc, IUWScheduleHost pIHost)
		{
			byte[] lyP = pORow.Parameters;
			int liX = lyP[0];
			int liY = lyP[1];
			UWCritterRecord lORecord = new UWCritterRecord(pONpc);

			if (lyP[5] != 0 || (!PlayerLooking(liX << 3, liY << 3, pIHost)
				&& !PlayerLooking((lORecord.TileX << 3) + lORecord.FineX, (lORecord.TileY << 3) + lORecord.FineY, pIHost)))
			{
				if (UWConversationRulesUw2.TeleportCritter(pIHost.World, pONpc, liX, liY, fIsFlier(pONpc, pIHost.Properties)))
				{
					if (lyP[6] != 0)
						fSetHome(pONpc, liX, liY);

					return false;
				}
			}

			if ((sbyte)lyP[7] > 0)
			{
				UWScheduleUw2.Row lOCopy = UWScheduleBookUw2.Copy(pORow);

				lOCopy.Time = (UWPlayerRecordUw2.XClock(pIHost.Record, DayBlock) + lyP[7]) % DaySteps;
				lOCopy.Once = 1;
				Migrate(pOBook, lOCopy);
			}

			return false;
		}

		/// <summary>gronkify_slay: the conversation's partner is spared unless the row's byte 7 says otherwise (and then a
		/// repeating row with byte 8 set counts XC_CHANGED up when the first queued migration's event is 15 - the
		/// original's odd test, kept); anyone else dies at once (instant_kill).</summary>
		private static bool fKill(UWScheduleBookUw2 pOBook, UWScheduleUw2.Row pORow, UWNpc pONpc, IUWScheduleHost pIHost)
		{
			byte[] lyP = pORow.Parameters;

			if (lyP[2] == 0 && pONpc == pIHost.TalkingTo)
			{
				if (lyP[3] > 0 && pOBook.Migrations.Count > 0 && pOBook.Migrations[0].Event == 15 && pORow.Once == 0)
					UWPlayerRecordUw2.SetXClock(pIHost.Record, ChangedClock, UWPlayerRecordUw2.XClock(pIHost.Record, ChangedClock) + 1);

				return false;
			}

			return pIHost.World != null && pIHost.World.KillCreature(pONpc);
		}

		private static void fSetHome(UWNpc pONpc, int piX, int piY)
		{
			pONpc.Quality = (ushort)((pONpc.Quality & ~0x3F) | (piX & 0x3F));
			pONpc.Owner = (ushort)((pONpc.Owner & ~0x3F) | (piY & 0x3F));
		}

		/// <summary>player_looking (SCDEVENT.C:120): within check_alert's eight squares and roughly in front of the player
		/// (deltatotheta's eighth within one of his facing).</summary>
		public static bool PlayerLooking(int piX8, int piY8, IUWScheduleHost pIHost)
		{
			int liPx = pIHost.PlayerX8;
			int liPy = pIHost.PlayerY8;

			if (Math.Abs((liPx >> 3) - (piX8 >> 3)) >= AlertRange || Math.Abs((liPy >> 3) - (piY8 >> 3)) >= AlertRange)
				return false;

			int liHeading = DeltaToTheta((sbyte)((sbyte)piX8 - (sbyte)liPx), (sbyte)((sbyte)piY8 - (sbyte)liPy));
			int liDiff = Math.Abs(liHeading - pIHost.PlayerFacingEighth);

			return liDiff < 2 || liDiff > 6;
		}

		/// <summary>deltatotheta (uw2 critter/PATHFIND.C:1204): the nearest of the eight headings, 0 along +y, clockwise
		/// towards +x; the doubling in a char as the original has it.</summary>
		public static int DeltaToTheta(sbyte piX, sbyte piY)
		{
			int liDx = (sbyte)(piX << 1);
			int liDy = (sbyte)(piY << 1);

			if (piY > liDx)
			{
				if (piX > -liDy)
					return piY > -liDx ? 0 : 7;

				return piX > liDy ? 5 : 6;
			}

			if (piX > -liDy)
				return piX > liDy ? 2 : 1;

			return piY > -liDx ? 3 : 4;
		}

		// ------------------------------------------------- Event 5: the scheduled triggers

		/// <summary>ev_trigger: every trigger of in-class 0xC on the square goes off with UseTrigger's scheduled mode, as
		/// by nobody. Returns how many.</summary>
		public static int FireScheduledTriggers(int piX, int piY, IUWScheduleHost pIHost)
		{
			UWLevel lOLevel = pIHost.World != null ? pIHost.World.Level : null;
			UWTile lOTile = lOLevel != null && piX >= 0 && piY >= 0 ? lOLevel.GetTile(piX, piY) : null;

			if (lOTile == null || lOTile.ObjectsInTile == null || pIHost.Traps == null)
				return 0;

			int liFired = 0;

			foreach (UWObject lOObject in new List<UWObject>(lOTile.ObjectsInTile))
			{
				if (lOObject == null || !UWTrapRulesUw2.IsTrigger(lOObject.ID) || (lOObject.ID & 0xF) != ScheduledTriggerInClass)
					continue;

				UWTrapRulesUw2.UseTrigger(null, lOObject, UWTrapRulesUw2.ScheduledMode, pIHost.Traps);
				liFired++;
			}

			return liFired;
		}

		// ------------------------------------------------- Event 7: the hacks

		/// <summary>ev_hack by the row's first parameter byte. The row's bytes from 6 are p[0..]; Parameters[1 + i].</summary>
		private static void fHack(UWScheduleUw2.Row pORow, IUWScheduleHost pIHost)
		{
			byte[] lyP = pORow.Parameters;

			switch (lyP[0])
			{
				case HackGarg:
					fGronk(fWord(lyP, 1), pIHost, lONpc =>
					{
						UWCritterRecord lORecord = new UWCritterRecord(lONpc);

						if (lORecord.TileX == lyP[3] && lORecord.TileY == lyP[4])
							UWTrapRulesUw2.SetNumbered(pIHost.Record, fWord(lyP, 5), lyP[7], fWord(lyP, 8));

						return false;
					});
					break;

				case HackSoldier:
					fGronk(fWord(lyP, 3), pIHost, lONpc => fSoldier(lONpc, lyP, pIHost));
					break;

				case HackFreeze:
					fFreeze(lyP[1], lyP[2], fWord(lyP, 3), pIHost);
					break;

				case HackDoors:
					// DoClosingDoors(arg): the shipped rows pass 0, the night's form (every door, wherever the player is).
					if (pIHost.World != null)
						UWSleepRules.CloseDoorsAtNight(pIHost.World, DoorChanceInTen);
					break;

				case HackNystul:
					fGronk(fWord(lyP, 1), pIHost, lONpc =>
					{
						UWCritterRecord lORecord = new UWCritterRecord(lONpc);
						int liHeading = lyP[3] & 7;

						lORecord.FineHeading = liHeading << 5;
						lORecord.SetFacing(liHeading);

						return false;
					});
					break;

				case HackGotha:
					fGronk(fWord(lyP, 1), pIHost, lONpc => fGotha(lONpc, lyP[3], lyP[4], lyP[5], lyP[6], pIHost));
					break;
			}
		}

		/// <summary>gronkify_soldier: at the square p[0], p[1] (or that square 0, 0) the critter goes to a random free square
		/// of the rectangle p[4..7], tried as many times as it has squares, and makes it its home.</summary>
		private static bool fSoldier(UWNpc pONpc, byte[] pyP, IUWScheduleHost pIHost)
		{
			UWCritterRecord lORecord = new UWCritterRecord(pONpc);
			int liSqX = pyP[1];
			int liSqY = pyP[2];

			if ((liSqX != 0 || liSqY != 0) && (lORecord.TileX != liSqX || lORecord.TileY != liSqY))
				return false;

			int liX1 = pyP[5];
			int liY1 = pyP[6];
			int liX2 = pyP[7];
			int liY2 = pyP[8];
			int liMax = (liX2 - liX1 + 1) * (liY2 - liY1 + 1);
			bool lbFlier = fIsFlier(pONpc, pIHost.Properties);

			for (int liTry = 0; liTry < liMax; liTry++)
			{
				int liX = liX1 + ((UWRandom.Next(0x8000) * (liX2 - liX1 + 1)) / 0x8000);
				int liY = liY1 + ((UWRandom.Next(0x8000) * (liY2 - liY1 + 1)) / 0x8000);

				if (UWConversationRulesUw2.TeleportCritter(pIHost.World, pONpc, liX, liY, lbFlier))
				{
					fSetHome(pONpc, liX, liY);

					break;
				}
			}

			return false;
		}

		/// <summary>gronkify_gotha: the rectangle scanned row by row, teleport_critter tried on each square; a fit only ends
		/// that row (the inner loop's break), so the next rows try again; the home becomes the loops' last x and y, as the
		/// original leaves them.</summary>
		private static bool fGotha(UWNpc pONpc, int piX1, int piY1, int piX2, int piY2, IUWScheduleHost pIHost)
		{
			bool lbFlier = fIsFlier(pONpc, pIHost.Properties);
			int liX = piX1;
			int liY;

			for (liY = piY1; liY <= piY2; liY++)
			{
				for (liX = piX1; liX <= piX2; liX++)
				{
					if (UWConversationRulesUw2.TeleportCritter(pIHost.World, pONpc, liX, liY, lbFlier))
						break;
				}
			}

			fSetHome(pONpc, liX, liY);

			return false;
		}

		/// <summary>ev_freeze_hack: across the map about half the squares whose floor slot is piFrom get piTo and their height
		/// plus piRaise (change_terrain with the wall kept and type 0x10).</summary>
		private static void fFreeze(int piFrom, int piTo, int piRaise, IUWScheduleHost pIHost)
		{
			UWLevel lOLevel = pIHost.World != null ? pIHost.World.Level : null;

			if (lOLevel == null || pIHost.Traps == null)
				return;

			for (int liX = 0; liX < UWWorldScale.TilesPerAxis; liX++)
			{
				for (int liY = 0; liY < UWWorldScale.TilesPerAxis; liY++)
				{
					UWTile lOTile = lOLevel.GetTile(liX, liY);

					if (lOTile == null || ((lOTile.RawTileData >> 10) & 0xF) != piFrom || UWRandom.Next(0x8000) % 2 != 1)
						continue;

					UWTrapRulesUw2.ChangeTerrain(liX, liY, KeepWall, piTo, (lOTile.FloorHeight >> 4) + (short)piRaise, FreezeType, 0, 0, pIHost.Traps);
				}
			}
		}

		// ------------------------------------------------- Hack 36: the castle's day (WORLDEV.C, CRITTIME.C)

		public const int CastleQuest = 109;

		public const int BritishJailedQuest = 112;

		public const int CastleClock = 1;

		public const int CastleClockEnd = 16;

		public const int FirstCastleWhoAmI = 0x81;

		public const int LastCastleWhoAmI = 0x8F;

		public const int SyriaWhoAmI = 0xA8;

		public const int BritishWhoAmI = 0x8E;

		public const int ToryWhoAmI = 0x8D;

		public const int NelsonWhoAmI = 0x8B;

		public const int PattersonWhoAmI = 0x8C;

		public const int MirandaWhoAmI = 0x88;

		public const int NystulWhoAmI = 0x82;

		/// <summary>game_clock / 0xE1000: the hour.</summary>
		public const int ClockPerHour = 0xE1000;

		private static readonly int[] miOwnX = { 0x2A, 0x24, 0x15, 0x25, 0x16, 0x19, 0x1B, 0x2C, 0x2B, 0x16, 0x15, 0x18, 0x1A, 0x19 };

		private static readonly int[] miOwnY = { 0x2B, 0x33, 0x2A, 0x23, 0x33, 0x2B, 0x24, 0x30, 0x31, 0x25, 0x22, 0x27, 0x30, 0x22 };

		/// <summary>The rectangles of places 1 to 4 (x1, y1, x2, y2).</summary>
		private static readonly int[,] miPlaces =
		{
			{ 0, 0, 0, 0 },
			{ 0x1B, 0x22, 0x23, 0x25 },
			{ 0x1E, 0x27, 0x20, 0x2B },
			{ 0x1D, 0x2D, 0x21, 0x33 },
			{ 0x23, 0x2F, 0x2F, 0x34 }
		};

		/// <summary>move_folks_around (hack 36): while quest 109 is set and the castle plot below 16, the first castle person
		/// of each whoami 0x81-0x8F and Syria may go to their next place - but Lord British once jailed (quest 112), Lady
		/// Tory from plot 8, Nelson and Patterson from 11. Returns how many were sent.</summary>
		public static int MoveFolksAround(IUWScheduleHost pIHost)
		{
			UWPlayerFile lOFile = pIHost.Record;
			int liPlot = UWPlayerRecordUw2.XClock(lOFile, CastleClock);
			UWLevel lOLevel = pIHost.World != null ? pIHost.World.Level : null;

			if (UWPlayerRecordUw2.GetQuest(lOFile, CastleQuest) == 0 || liPlot >= CastleClockEnd || lOLevel == null)
				return 0;

			int liSent = 0;

			for (int liWho = FirstCastleWhoAmI; liWho <= LastCastleWhoAmI; liWho++)
			{
				if (liWho == BritishWhoAmI && UWPlayerRecordUw2.GetQuest(lOFile, BritishJailedQuest) != 0)
					continue;

				if (liWho == ToryWhoAmI && liPlot >= 8)
					continue;

				if ((liWho == NelsonWhoAmI || liWho == PattersonWhoAmI) && liPlot >= 11)
					continue;

				UWNpc lONpc = UWTrapRules.FindActiveByWhoAmI(lOLevel, liWho);

				if (lONpc != null && fGoHangOut(lONpc, pIHost))
					liSent++;
			}

			UWNpc lOSyria = UWTrapRules.FindActiveByWhoAmI(lOLevel, SyriaWhoAmI);

			if (lOSyria != null && fGoHangOut(lOSyria, pIHost))
				liSent++;

			return liSent;
		}

		/// <summary>maybe_go_hang_out: the place where_shall_we_hang_out picks becomes the home, goal 1; unseen at both ends
		/// the critter is there at once.</summary>
		private static bool fGoHangOut(UWNpc pONpc, IUWScheduleHost pIHost)
		{
			WhereToHangOut(pONpc, pIHost.Record, out int liX, out int liY);

			UWCritterRecord lORecord = new UWCritterRecord(pONpc);

			fSetHome(pONpc, liX, liY);
			lORecord.Goal = 1;

			if (!PlayerLooking((lORecord.TileX << 3) + lORecord.FineX, (lORecord.TileY << 3) + lORecord.FineY, pIHost)
				&& !PlayerLooking((liX << 3) + 3, (liY << 3) + 3, pIHost))
				UWConversationRulesUw2.TeleportCritter(pIHost.World, pONpc, liX, liY, fIsFlier(pONpc, pIHost.Properties));

			return true;
		}

		/// <summary>where_shall_we_hang_out (CRITTIME.C:646): the day in twelve two-hour stages; a random place of six, but at
		/// night and else one time in three the stage's own; Miranda, everyone before the castle plot starts, Lord British
		/// after quests[28] bit 3 and Nystul from plot 12 keep their own spot. Places: 0 the own spot by tables (whoami
		/// 0x81 reads 0x22 and 0 past the tables' start, Syria 0x2A, 0x24), 1-3 a random square of a rectangle (2 avoids
		/// 0x1F, 0x29), 4 and others stay where they are, 5 home.</summary>
		public static void WhereToHangOut(UWNpc pONpc, UWPlayerFile pOFile, out int piX, out int piY)
		{
			int liHour = (int)((uint)UWPlayerRecordUw2.GameClock(pOFile) / ClockPerHour % 24);
			int liStage = (((liHour + 1) >> 1) + 11) % 12;
			int liLoc = UWRandom.Next(0x8000) % 6;

			if (UWRandom.Next(0x8000) % 3 == 0 || liStage <= 2)
			{
				switch (liStage)
				{
					case 3: case 5: case 9: liLoc = 4; break;
					case 0: case 1: case 2: case 11: liLoc = 0; break;
					case 6: case 7: case 8: liLoc = 1; break;
				}
			}

			int liPlot = UWPlayerRecordUw2.XClock(pOFile, CastleClock);

			if (pONpc.NPCwhoami == MirandaWhoAmI || liPlot == 0)
				liLoc = 0;
			else if (pONpc.NPCwhoami == BritishWhoAmI && (UWPlayerRecordUw2.GetQuest(pOFile, 28 * 4 + 3) != 0))
				liLoc = 0;
			else if (pONpc.NPCwhoami == NystulWhoAmI && liPlot >= 0xC)
				liLoc = 0;

			UWCritterRecord lORecord = new UWCritterRecord(pONpc);

			if (liLoc == 5)
			{
				piX = pONpc.Quality & 0x3F;
				piY = pONpc.Owner & 0x3F;

				return;
			}

			if (liLoc == 0)
			{
				if (pONpc.NPCwhoami == SyriaWhoAmI)
				{
					piX = 0x2A;
					piY = 0x24;
				}
				else
				{
					int liAt = pONpc.NPCwhoami - NystulWhoAmI;

					piX = liAt >= 0 && liAt < miOwnX.Length ? miOwnX[liAt] : miOwnY[miOwnY.Length - 1];
					piY = liAt >= 0 && liAt < miOwnY.Length ? miOwnY[liAt] : 0;
				}

				return;
			}

			if (liLoc < 1 || liLoc > 3)
			{
				piX = lORecord.TileX;
				piY = lORecord.TileY;

				return;
			}

			int liX1 = miPlaces[liLoc, 0];
			int liY1 = miPlaces[liLoc, 1];
			int liX2 = miPlaces[liLoc, 2];
			int liY2 = miPlaces[liLoc, 3];

			piX = liX1 + (UWRandom.Next(0x8000) % (liX2 - liX1 + 1));
			piY = liY1 + (UWRandom.Next(0x8000) % (liY2 - liY1 + 1));

			if (liLoc == 2 && piX == 0x1F && piY == 0x29)
				piX++;
		}
	}
}
