namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FOOTSTEPS' FORMULA (cleanup step 4.7, 2026-10-18, engine-free out of
	/// UWPlayerMovement.fUpdateFootsteps; the host is UWPlayerFootsteps). Using the original's formula
	/// (reference: playerdatloop, uw1 branch): the interval is 0x40 plus 0x1770 divided by (1 plus
	/// momentum divided by 4) PIT ticks, at most 0xC8 - at full run 89 ticks, i.e. 0.35 s, at half
	/// 0.45 s, when sneaking 0.78 s. Louder with momentum: 0xF plus momentum divided by 32 as note
	/// offset. The momentum comes from the distance ACTUALLY travelled, not the target velocity -
	/// whoever turns on the spot makes no footsteps (per user, 2026-09-11).
	/// </summary>
	public static class UWFootstepRules
	{
		/// <summary>The original's momentum at full run (motion_player, 0x3AC).</summary>
		public const int FullMomentum = 0x3AC;

		/// <summary>From this speed in tiles per second on, footsteps play.</summary>
		public const float StepSpeedThreshold = 0.3f;

		/// <summary>The longest pause between two steps, in PIT ticks.</summary>
		public const int MaxIntervalTicks = 0xC8;

		/// <summary>The momentum, 0 to FullMomentum, of a speed against the full speed (both in
		/// tiles per second): rounded to the nearest, ties to even, as Unity's Mathf.RoundToInt.</summary>
		public static int Momentum(float pfSpeed, float pfFullSpeed)
		{
			float lfMomentum = FullMomentum * pfSpeed / pfFullSpeed;
			int liMomentum = UWRounding.HalfToEven(lfMomentum);

			if (liMomentum < 0)
				return 0;

			return liMomentum > FullMomentum ? FullMomentum : liMomentum;
		}

		/// <summary>The PIT ticks until the next step at this momentum.</summary>
		public static int IntervalTicks(int piMomentum)
		{
			int liTicks = 0x40 + (0x1770 / (1 + (piMomentum >> 2)));

			return liTicks < MaxIntervalTicks ? liTicks : MaxIntervalTicks;
		}

		/// <summary>The step's note offset: the louder the faster.</summary>
		public static int NoteOffset(int piMomentum)
		{
			return 0xF + (piMomentum >> 5);
		}
	}
}
