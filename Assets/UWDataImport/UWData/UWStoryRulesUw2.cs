using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S STORY CUTSCENES, engine-free (UW2 plan phase 5 part D): the night's dream (uw2 game/SKILLS.C dream)
	/// and the cutscene a conversation leaves pending (cs_check).
	///
	/// THE DREAMS: the castle dreams 0 to 3 come due as the castle's X clock (1) reaches 4, 6, 10 and 14; the latest due
	/// one not yet seen is shown. With none due, one in 4 + 4 * sleepfactor nights rolls one of dreams 4 to 6, none if
	/// that one was seen. Dream n is cutscene 0x18 + n; its bit in dreamflags (record 0xF7) is flipped once shown. The
	/// dream plant's Void comes first (UWVoidRulesUw2), and passing out does not dream.
	///
	/// cs_check: quest byte 15 (quest 143) holds a cutscene's number plus one; after a conversation it plays and is
	/// cleared - cutscene 2 is the ending (player_won_game).
	/// </summary>
	public static class UWStoryRulesUw2
	{
		public const int DreamFlagsOffset = 0xF7;

		/// <summary>XC_CASTLE, the castle plot's X clock.</summary>
		public const int CastleClock = 1;

		/// <summary>The castle clock each castle dream waits for.</summary>
		public static readonly int[] CastleDreamClocks = { 4, 6, 10, 14 };

		public const int FirstRandomDream = 4;

		public const int RandomDreams = 3;

		public const int FirstDreamCutscene = 0x18;

		/// <summary>The night's last message: 0x13 - sleepfactor ("Your sleep is uneasy." or "You feel rested.").</summary>
		public const int UneasyString = 0x13;

		/// <summary>quest_bytes[QB_CUTSCENE]: quest 128 + 15.</summary>
		public const int PendingCutsceneQuest = 143;

		/// <summary>The cutscene that is the game's end.</summary>
		public const int EndingCutscene = 2;

		/// <summary>The credits after the end (player_won_game).</summary>
		public const int CreditsCutscene = 10;

		/// <summary>
		/// dream's choice: the dream to show, or -1. pFRoll(n) is rand() % n, drawn as the original draws it (the random
		/// dream only when no castle dream is due).
		/// </summary>
		public static int PickDream(UWPlayerFile pOFile, int piSleepFactor, Func<int, int> pFRoll)
		{
			int liFlags = pOFile.Record[DreamFlagsOffset];
			int liClock = UWPlayerRecordUw2.XClock(pOFile, CastleClock);
			int liFound = -1;

			for (int liDream = 0; liDream < CastleDreamClocks.Length; liDream++)
			{
				if (liClock >= CastleDreamClocks[liDream] && (liFlags & (1 << liDream)) == 0)
					liFound = liDream;
			}

			if (liFound < 0 && pFRoll(4 + (piSleepFactor << 2)) == 0)
			{
				liFound = FirstRandomDream + pFRoll(RandomDreams);

				if (((1 << liFound) & liFlags) != 0)
					liFound = -1;
			}

			return liFound;
		}

		/// <summary>The dream shown: its bit flipped.</summary>
		public static void MarkDreamShown(UWPlayerFile pOFile, int piDream)
		{
			pOFile.Record[DreamFlagsOffset] = (byte)(pOFile.Record[DreamFlagsOffset] ^ (1 << piDream));
		}

		/// <summary>cs_check's pending cutscene, taken (cleared): its number, or -1 for none.</summary>
		public static int TakePendingCutscene(UWPlayerFile pOFile)
		{
			int liValue = UWPlayerRecordUw2.GetQuest(pOFile, PendingCutsceneQuest);

			if (liValue <= 0)
				return -1;

			UWPlayerRecordUw2.SetQuest(pOFile, PendingCutsceneQuest, 0);

			return liValue - 1;
		}
	}
}
