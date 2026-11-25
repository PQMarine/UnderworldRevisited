namespace UWDataImport.UWData
{
	/// <summary>
	/// The level the two flasks SHOW and their bubbling (cleanup step 4.6, 2026-10-18; part of
	/// UWCharacter until then, the textures are UWFlaskView's and the second game's screen's).
	///
	/// ONE SLOW UPDATE FOR BOTH, AS THE ORIGINAL IN BOTH GAMES (2026-11-25, UW2 beta gate 220.7; uw1 and uw2 ui/PANELS.C
	/// update_screen and adjust_flasks): every 64 ticks of the 1/256 s clock (UWFlaskAnimation.StepSeconds) one draw of
	/// rand() may start a flask bubbling (UWFlaskAnimation.StartChance, the low bit the flask), then each flask moves its
	/// level one step towards the real one (set_screen_frame puts the flasks on the slow update) and steps its bubbles at
	/// the new level. Whoever drinks a potion sees the flask run up, not jump. Minus one until the first pass, so that a
	/// loaded character starts at their real level instead of filling up from empty. Until 2026-11-25 the level stepped
	/// every eighth of a second and each flask drew its own chance (the reference, uimanager_flasks.AnimateFlasks), and
	/// the second game's flasks did not use this class.
	/// </summary>
	public sealed class UWFlaskGauge
	{
		private int miShownHealthLevel = -1;

		private int miShownManaLevel = -1;

		private readonly UWTickClock mOClock = new UWTickClock(UWFlaskAnimation.StepSeconds);

		private readonly UWFlaskAnimation mOHealthBubbles = new UWFlaskAnimation();

		private readonly UWFlaskAnimation mOManaBubbles = new UWFlaskAnimation();

		/// <summary>The health flask's shown level, 0 to UWFlasks.FillLevels; -1 before the first
		/// Advance.</summary>
		public int ShownHealthLevel => miShownHealthLevel;

		public int ShownManaLevel => miShownManaLevel;

		/// <summary>The bubbling picture over the liquid (1 to UWFlaskAnimation.FrameCount - 1), or -1.</summary>
		public int HealthBubbleFrame => mOHealthBubbles.FrameAt(miShownHealthLevel);

		public int ManaBubbleFrame => mOManaBubbles.FrameAt(miShownManaLevel);

		/// <summary>
		/// Advances by the elapsed time towards the real values; pbFilling says the first game's resurrection fill runs
		/// (then nothing bubbles). Returns whether a level or a bubble changed.
		/// </summary>
		public bool Advance(float pfElapsedSeconds, int piHealth, int piMaxHealth, int piMana, int piMaxMana,
			bool pbFilling)
		{
			// In whole numbers as the original (UWFlasks.GetLevel): the float quotient times the steps
			// could land a hair below a step (10 of 30 gave 3.9999 and showed three).
			int liHealth = UWFlasks.GetLevel(piHealth, piMaxHealth);
			int liMana = UWFlasks.GetLevel(piMana, piMaxMana);

			bool lbChanged = false;

			if (miShownHealthLevel < 0 || miShownManaLevel < 0)
			{
				miShownHealthLevel = liHealth;
				miShownManaLevel = liMana;
				lbChanged = true;
			}

			// The resurrection's fill (UWFlaskView.StartFlaskFill) shows no bubbles.
			if (pbFilling && (mOHealthBubbles.IsBubbling || mOManaBubbles.IsBubbling))
			{
				mOHealthBubbles.Stop();
				mOManaBubbles.Stop();
				lbChanged = true;
			}

			int liSteps = mOClock.Advance(pfElapsedSeconds);

			for (int liStep = 0; liStep < liSteps; liStep++)
			{
				if (!pbFilling)
				{
					int liRoll = UWRandom.Next(UWOriginalRandom.Range);

					if (liRoll < UWFlaskAnimation.StartChance)
						((liRoll & 1) == 0 ? mOHealthBubbles : mOManaBubbles).Start();
				}

				if (miShownHealthLevel != liHealth)
				{
					miShownHealthLevel += miShownHealthLevel < liHealth ? 1 : -1;
					lbChanged = true;
				}

				if (miShownManaLevel != liMana)
				{
					miShownManaLevel += miShownManaLevel < liMana ? 1 : -1;
					lbChanged = true;
				}

				lbChanged |= mOHealthBubbles.Step(miShownHealthLevel);
				lbChanged |= mOManaBubbles.Step(miShownManaLevel);
			}

			return lbChanged;
		}

		/// <summary>The level shown as a fill fraction - half a step into its band, so the rounding
		/// in UWFlasks lands on exactly this level.</summary>
		public static float GetShownFill(int piLevel)
		{
			return (piLevel + 0.5f) / UWFlasks.FillLevels;
		}
	}
}
