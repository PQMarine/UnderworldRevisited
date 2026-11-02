using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE COMPASS'S REPORT IN THE SECOND GAME (print_info, uw2 ui/GAMESCR.C:64): the hunger and the fatigue, the world
	/// when the player has learnt its name, the time of day. Its own lines in its block 1, and no day count as the
	/// first game's (UWStatusReport); the strings below are the game's, ours count one higher.
	/// </summary>
	public static class UWStatusReportUw2
	{
		private const int CurrentlyString = 0x44;

		private const int GuessString = 0x48;

		private const int InWorldString = 0x49;

		private const int FirstDaypartString = 0x54;

		private const int FullStopString = 0x60;

		private const int AndString = 0x74;

		private const int FirstHungerString = 0x75;

		private const int LeastFatigueString = 0x83;

		private const int AsleepString = 0x128;

		private const int HungerPerStep = 0x1E;

		private const int FatiguePerStep = 0x17;

		private const int MostFatigueSteps = 5;

		/// <summary>Two hours of game_clock, twelve of them to the day.</summary>
		private const int ClockPerDaypart = 0x1C2000;

		private const int Dayparts = 12;

		private const int LevelsPerWorld = 8;

		/// <summary>quest_bytes[3]: a bit for each world after the first whose name the player knows.</summary>
		private const int KnownWorldsByte = 3;

		/// <summary>An empty line of the scroll: a space, the log drops an empty message (UWMessageLog.ClearMessages).</summary>
		private const string BlankLine = " ";

		/// <summary>The hunger's word: its string in our numbering.</summary>
		public static int HungerWord(int piHunger)
		{
			return FirstHungerString + (piHunger / HungerPerStep) + 1;
		}

		/// <summary>The fatigue's word: its string in our numbering.</summary>
		public static int FatigueWord(int piFatigue)
		{
			return LeastFatigueString - System.Math.Min(MostFatigueSteps, piFatigue / FatiguePerStep) + 1;
		}

		/// <summary>The time of day's word: its string in our numbering.</summary>
		public static int DaypartWord(int piClock)
		{
			return FirstDaypartString + (int)((uint)piClock / ClockPerDaypart % Dayparts) + 1;
		}

		/// <summary>The world's name as the report calls it, in our numbering: an unknown world is "an alternate
		/// dimension", Britannia is always known. -1 when the player knows no world's name yet: no line then.</summary>
		public static int WorldWord(int piKnownWorlds, int piLevelNumber)
		{
			if (piKnownWorlds == 0)
				return -1;

			int liWorld = (piLevelNumber - 1) / LevelsPerWorld;
			int liName = (((piKnownWorlds << 1) + 1) & (1 << liWorld)) != 0 ? liWorld + 2 : 1;

			return InWorldString + liName + 1;
		}

		/// <summary>The first line: "You are currently hungry and rested."</summary>
		public static string ConditionLine(DataImport pOData, UWPlayerFile pOFile)
		{
			int liHunger = pOFile.Record[UWPlayerRecordUw2.HungerOffset];
			int liFatigue = pOFile.Record[UWPlayerRecordUw2.FatigueOffset];
			int liAwake = UWPlayerRecordUw2.IsInVoid(pOFile) ? AsleepString + 1 : FatigueWord(liFatigue);

			return fGet(pOData, CurrentlyString + 1) + fGet(pOData, HungerWord(liHunger)) + fGet(pOData, AndString + 1) + fGet(pOData, liAwake)
				+ fGet(pOData, FullStopString + 1);
		}

		/// <summary>The report's lines, in order. The scroll is not wiped: a blank line first and one last, a second
		/// one last when the world's line is missing, so it always takes five lines, all the box has, and what stood
		/// there scrolls away.</summary>
		public static List<string> Lines(DataImport pOData, UWPlayerFile pOFile, int piLevelNumber)
		{
			List<string> lOLines = new List<string> { BlankLine, ConditionLine(pOData, pOFile) };
			int liWorld = WorldWord(pOFile.Record[UWPlayerRecordUw2.QuestBytesOffset + KnownWorldsByte], piLevelNumber);

			if (liWorld >= 0)
				lOLines.Add(fGet(pOData, InWorldString + 1) + fGet(pOData, liWorld) + fGet(pOData, FullStopString + 1));

			lOLines.Add(fGet(pOData, GuessString + 1) + fGet(pOData, DaypartWord(UWPlayerRecordUw2.GameClock(pOFile))) + fGet(pOData, FullStopString + 1));
			lOLines.Add(BlankLine);

			if (liWorld < 0)
				lOLines.Add(BlankLine);

			return lOLines;
		}

		private static string fGet(DataImport pOData, int piIndex)
		{
			return UWText.General(pOData, piIndex).TrimEnd('\r', '\n');
		}
	}
}
