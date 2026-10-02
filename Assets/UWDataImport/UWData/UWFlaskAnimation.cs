namespace UWDataImport.UWData
{
	/// <summary>
	/// The bubbling of a flask (2026-09-17, per user: "when the flask is full it should not
	/// bubble, only when the liquid has some surface").
	///
	/// THE RULE, from the reference (uimanager_flasks.AnimateFlasks) and matching that memory:
	///
	///   - It only bubbles at MIDDLE LEVELS, 3 to 9 of the thirteen (0 to 12), and only while the
	///     level is not moving - a flask that is filling or emptying shows no bubbles.
	///   - Every step (an eighth of a second, 32 of the original's timer ticks at 0.00391 s) a
	///     resting flask has a 1 in 32 chance to START bubbling. About once every four seconds.
	///   - Once it runs it walks through the twelve pictures, one per step, and then stops. A
	///     bubbling therefore lasts one and a half seconds.
	///
	/// The pictures sit in FLASKS.GR right behind the thirteen fill slices of their colour
	/// (see UWFlasks): red 13 to 24, mana 38 to 49, poison 63 to 74.
	/// </summary>
	public sealed class UWFlaskAnimation
	{
		/// <summary>Seconds per step - 32 ticks of the original's timer.</summary>
		public const float StepSeconds = 32f * 0.00391f;

		public const int FrameCount = 12;

		/// <summary>Below and above this the surface is too small or the flask too full.</summary>
		public const int LowestLevel = 3;

		public const int HighestLevel = 9;

		/// <summary>One in this many steps starts a bubbling.</summary>
		private const int StartChance = 32;

		private readonly UWTickClock mOClock = new UWTickClock(StepSeconds);

		private int miFrame = -1;

		/// <summary>The picture to draw over the liquid, or -1 while it is quiet.</summary>
		public int Frame => miFrame;

		/// <summary>
		/// Advances by the elapsed time. piLevel is the fill level (0 to 12), pbSettled says the
		/// flask is not filling or emptying right now. Returns whether the picture changed.
		/// </summary>
		public bool Advance(float pfElapsedSeconds, int piLevel, bool pbSettled)
		{
			bool lbAllowed = pbSettled && piLevel >= LowestLevel && piLevel <= HighestLevel;

			if (!lbAllowed)
			{
				mOClock.Advance(pfElapsedSeconds);

				if (miFrame < 0)
					return false;

				miFrame = -1;

				return true;
			}

			int liSteps = mOClock.Advance(pfElapsedSeconds);

			if (liSteps <= 0)
				return false;

			bool lbChanged = false;

			for (int liStep = 0; liStep < liSteps; liStep++)
			{
				if (miFrame < 0)
				{
					if (UWRandom.Next(StartChance) != 0)
						continue;

					miFrame = 0;
					lbChanged = true;

					continue;
				}

				miFrame++;
				lbChanged = true;

				if (miFrame >= FrameCount)
					miFrame = -1;
			}

			return lbChanged;
		}

		public void Stop()
		{
			miFrame = -1;
		}
	}
}
