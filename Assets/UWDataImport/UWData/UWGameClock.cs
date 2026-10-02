namespace UWDataImport.UWData
{
	/// <summary>
	/// The game clock: what time it is and which day it is.
	///
	/// THE UNIT is in the reference, in its conversions for the pocket watch
	/// (playerdatclock): 0x3C00 units per game minute, 0xE1000 per hour - that works out
	/// exactly with sixty minutes per hour. A day is therefore 0x1518000, and the
	/// compass text splits the day into twelve parts of 0x1C2000 each, i.e. two hours each.
	///
	/// SECOND EVIDENCE (UWReverseEngineering, 2026-09-08): it says about the second clock byte
	/// "counts up 255 seconds or 4:15 minutes". A one-second tick in the second byte means 256
	/// units per second, so 15360 per minute - exactly the 0x3C00.
	///
	/// HOW FAST IT RUNS: AT REAL TIME, 256 units per real second - read and measured on
	/// 2026-09-23. seg034_2F89_4E adds the PIT ticks passed since the last frame to PLAYER.DAT
	/// 0xCE every frame (label 4C0), and the PIT runs at 256 per second, so awake a game
	/// second is a real second and a game day twenty-four real hours; the days go by mostly
	/// in sleep, which adds its hours on its own (UWSleepRules). The measurement (per user):
	/// Henrietta idle for forty minutes in both games from the same save - the original's
	/// clock grew by 613,021, which is 2,394 seconds at 256, ours by 113 ticks of 0x6000.
	///
	/// UNTIL THEN it was an estimate from hunger, 0x6000 per tick, which made a game day
	/// five and a quarter real hours and our clock 4.6 times too fast. The reference had left
	/// the spot commented out ("not sure what the exact rate should be here").
	///
	/// We advance it per player tick by the tick's own length in PIT ticks
	/// (UnitsPerTick), not per frame - the same rate, in steps of twenty-one seconds, which
	/// nothing that reads the clock can tell apart: the finest thing shown is the minute of
	/// the pocket watch.
	/// </summary>
	public static class UWGameClock
	{
		/// <summary>Clock units per game minute - see class comment.</summary>
		public const int UnitsPerMinute = 0x3C00;

		/// <summary>How far the clock advances per player tick: the tick's length in PIT
		/// ticks, 21 * 256 - real time, see class comment.</summary>
		public const int UnitsPerTick = UWPlayerTick.PitTicks;

		/// <summary>Clock units per hour. Sixty minutes, exactly.</summary>
		public const int UnitsPerHour = 0xE1000;

		/// <summary>The twelve day parts of the compass text are two hours long each.
		/// </summary>
		public const int UnitsPerDaypart = UnitsPerHour * 2;

		/// <summary>Twelve parts make a day.</summary>
		public const int DaypartsPerDay = 12;

		/// <summary>Which day part, 0 to 11 - the index for the day text.
		/// </summary>
		public static int GetDaypart(int piClockValue)
		{
			return (piClockValue / UnitsPerDaypart) % DaypartsPerDay;
		}

		/// <summary>How many full days have passed, from zero.</summary>
		public static int GetDays(int piClockValue)
		{
			return piClockValue / (UnitsPerDaypart * DaypartsPerDay);
		}

		/// <summary>The hour on the twelve-hour dial of the pocket watch.</summary>
		public static int GetWatchHour(int piClockValue)
		{
			return (piClockValue / UnitsPerHour) % 12;
		}

		/// <summary>The minute of the pocket watch.</summary>
		public static int GetWatchMinute(int piClockValue)
		{
			return (piClockValue / UnitsPerMinute) % 60;
		}
	}
}
