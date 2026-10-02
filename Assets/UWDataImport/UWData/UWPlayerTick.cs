namespace UWDataImport.UWData
{
	/// <summary>
	/// The player tick: the beat everything time-related in the original hangs on, and the
	/// one place its length is written down.
	///
	/// THE LENGTH comes out of PlayerUpdateTick_seg024_24DC_3A4 (labels 485 to 4E8, lines
	/// 85797-85864). The routine takes the free-running PIT counter
	/// (PITTimerGlobal_dseg_5c99_2364), shifts it right by eight to get whole SECONDS, adds
	/// the seconds passed since the last frame to an accumulator (dseg_5c99_28D) and fires
	/// when the accumulator is GREATER than 0x14, resetting it to zero afterwards. Twenty is
	/// therefore the last value that does NOT fire, and the interval is TWENTY-ONE seconds,
	/// or 21 * 256 = 5376 PIT ticks. The whole chain of evidence sits in UWWornRegeneration,
	/// where it was first read out.
	///
	/// REAL TIME, NOT GAME TIME: the accumulator hangs on the PIT counter and not on the game
	/// clock in PLAYER.DAT 0xCE, so sleeping brings no ticks - sleeping has its own recovery.
	///
	/// WHAT HANGS ON IT: spell duration (one point of stability per tick), the worn curse and
	/// the worn regeneration, poison, mana and the intoxication on every third tick, hunger,
	/// fatigue and natural healing on every twenty-fourth (all UWPlayerVitals.RunGameTick), the
	/// respawning of monsters (UWRespawnRules) and the swimming check
	/// (UWPlayerTerrain.fUpdateSwimCounter) - the original runs those last two in the same
	/// periodic player loop. They all take their interval from here, so there is ONE number
	/// and not five that could drift apart.
	///
	/// UNTIL 2026-09-21 the port ran the tick at twenty seconds, about five per cent fast.
	/// THE GAME CLOCK advances by the tick's length in PIT ticks (UWGameClock.UnitsPerTick),
	/// i.e. at real time as in the original - since 2026-09-23; before, by an estimated
	/// 0x6000 per tick, 4.6 times too fast (see UWGameClock).
	/// </summary>
	public static class UWPlayerTick
	{
		/// <summary>One tick in real seconds - see the class comment.</summary>
		public const int Seconds = 21;

		/// <summary>The PIT rate the original measures its whole schedule in.</summary>
		public const int PitTicksPerSecond = 256;

		/// <summary>One tick in PIT ticks: 21 * 256.</summary>
		public const int PitTicks = Seconds * PitTicksPerSecond;

		/// <summary>Three ticks are the original's "minute" - POISON and MANA hang on it, and
		/// nothing else (PlayerUpdates_seg028_2985_13D label 25C to 2F4). Sixty-three real
		/// seconds; that is what counting in ticks costs.
		///
		/// CORRECTED 2026-09-22: intoxication and the mushroom used to hang here too. Neither
		/// belongs. The intoxication is stepped down in the TWENTY-FOUR block (label 31F,
		/// the six bits at PLAYER.DAT 0x61 bit 4), and the mushroom on EVERY tick, before any
		/// of the counters are looked at (label 1CD, bits 2 and 3 of the same byte).</summary>
		public const int MinuteTicks = 3;

		/// <summary>
		/// TWENTY-FOUR ticks are the original's "five minutes" - hunger, fatigue, natural
		/// healing and the respawning hang on it. Eight and a half real minutes; on that label
		/// see UWRespawnRules.
		///
		/// READ OUT PROPERLY ON 2026-09-22, after it had stood at thirty since the beginning
		/// and had been seen as 24 in passing on 2026-09-20. PlayerUpdates_seg028_2985_13D
		/// (98460-98812) counts its own calls in dseg_5c99_3F9, raised once per entry at label
		/// 140. Label 25C divides that counter by THREE and runs the minute block on a
		/// remainder of zero; label 2F4 divides it by 0x18, which is TWENTY-FOUR, and runs this
		/// block on a remainder of zero. At the end of it, label 3AD, the counter is set back
		/// to zero, so the cycle is exactly twenty-four long and the minute block falls on 3,
		/// 6, 9 and so on up to 24. Counting the modulo on a counter that keeps growing comes
		/// to the same, because 24 divides by 3.
		/// </summary>
		public const int FiveMinuteTicks = 24;
	}
}
