using UWDataImport;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The four lines that appear in the original on a click on the compass:
	///
	///     You are currently well fed and wide awake.
	///     You are on the fifth level of the Abyss.
	///     It is the eleventh day of your imprisonment.
	///     You guess that it is currently night.
	///
	/// Photographed like this by the user in the original (2026-09-07). The structure is in the reference
	/// (uimanager_compass.StatusMessageUW1), including all numbers in string block 1:
	///
	///    65  "You are currently "        104 " and "
	///    66  "You are on the "            67 " level of the Abyss."
	///    68  "It is the "                 69 " day of your imprisonment."
	///    70  the replacement line when more than a hundred days have passed
	///    71  "You guess that it is currently "
	///    72..83   the twelve parts of the day, "night" to "night"
	///   105..113  nine levels of hunger, from "starving" to "satiated"
	///   114..119  six levels of fatigue, from "fatigued" to "wide awake"
	///   412..     the ordinals, "first" upwards
	///
	/// ALL NUMBERS ARE ONE ABOVE THOSE OF THE REFERENCE - that is the usual project +1
	/// by which our parsed string blocks are shifted against the game's numbering.
	/// The first implementation took the reference numbers unchecked, and the four lines came
	/// out garbled (per user, 2026-09-07). The values here are read off a printout
	/// of the block, no longer derived.
	///
	/// The ordinals serve twice: for the level with 411 + level (the level counts from
	/// one, so 416 for the fifth, "fifth") and for the day with 412 + elapsed days
	/// (so after ten days 422, "eleventh"). Both match the user's photo.
	/// </summary>
	public static class UWStatusReport
	{
		private const int YouAreCurrently = 65;
		private const int YouAreOnThe = 66;
		private const int LevelOfTheAbyss = 67;
		private const int ItIsThe = 68;
		private const int DayOfYourImprisonment = 69;
		private const int UncountableDays = 70;
		private const int YouGuessItIs = 71;
		private const int FirstDaypart = 72;
		private const int And = 104;
		private const int FirstHunger = 105;

		/// <summary>The fatigue texts count BACKWARDS: 119 is "wide awake", 114 is
		/// "fatigued".</summary>
		private const int LeastFatigue = 119;

		/// <summary>"first". The level uses it as 411 + level, the day as 412 + days.
		/// </summary>
		private const int FirstOrdinal = 412;

		/// <summary>After this many days the original stops counting.</summary>
		private const int UncountableAfterDays = 100;

		/// <summary>How finely the two bars break down into text levels - values of the reference.
		/// </summary>
		private const int HungerPerStep = 30;

		private const int FatiguePerStep = 23;

		private const int MostFatigueSteps = 5;

		/// <summary>
		/// Line one: hunger and fatigue.
		/// </summary>
		public static string GetConditionLine(DataImport pOData, int piHunger, int piFatigue)
		{
			if (pOData == null)
				return null;

			int liFatigueSteps = System.Math.Min(MostFatigueSteps, piFatigue / FatiguePerStep);

			return fGet(pOData, YouAreCurrently)
				+ fGet(pOData, FirstHunger + (piHunger / HungerPerStep))
				+ fGet(pOData, And)
				+ fGet(pOData, LeastFatigue - liFatigueSteps)
				+ ".";
		}

		/// <summary>
		/// Line two: which level one is on. Also needed on its own - the original
		/// shows it when opening the map (per user, 2026-09-07).
		/// </summary>
		public static string GetLevelLine(DataImport pOData, int piDungeonLevel)
		{
			if (pOData == null)
				return null;

			return fGet(pOData, YouAreOnThe)
				+ fGet(pOData, FirstOrdinal - 1 + piDungeonLevel)
				+ fGet(pOData, LevelOfTheAbyss);
		}

		/// <summary>Line three: which day of the imprisonment it is.</summary>
		public static string GetDayLine(DataImport pOData, int piClockValue)
		{
			if (pOData == null)
				return null;

			int liDays = UWGameClock.GetDays(piClockValue);

			if (liDays > UncountableAfterDays)
				return fGet(pOData, UncountableDays);

			return fGet(pOData, ItIsThe)
				+ fGet(pOData, FirstOrdinal + liDays)
				+ fGet(pOData, DayOfYourImprisonment);
		}

		/// <summary>Line four: the estimated time of day.</summary>
		public static string GetTimeLine(DataImport pOData, int piClockValue)
		{
			if (pOData == null)
				return null;

			return fGet(pOData, YouGuessItIs)
				+ fGet(pOData, FirstDaypart + UWGameClock.GetDaypart(piClockValue));
		}

		/// <summary>If a string is missing, the spot stays empty instead of aborting the message - a
		/// wrong index should become visible, not halt the game.</summary>
		private static string fGet(DataImport pOData, int piIndex)
		{
			try
			{
				string lsText = pOData.GetGeneralMessage(piIndex);

				return lsText == null ? string.Empty : lsText.TrimEnd('\r', '\n');
			}
			catch
			{
				return string.Empty;
			}
		}
	}
}
