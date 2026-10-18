namespace UWDataImport.UWData
{
	/// <summary>
	/// The level the two flasks SHOW and their bubbling (cleanup step 4.6, 2026-10-18; part of
	/// UWCharacter until then, the textures are UWFlaskView's).
	///
	/// THE LEVEL ITSELF MOVES ONE STEP AT A TIME, an eighth of a second per step, as in the
	/// original (the reference: uimanager_flasks.AnimateFlasks; built 2026-09-17, per user).
	/// Whoever drinks a potion sees the flask run up, not jump. Minus one until the first pass,
	/// so that a loaded character starts at their real level instead of filling up from empty.
	/// </summary>
	public sealed class UWFlaskGauge
	{
		private int miShownHealthLevel = -1;

		private int miShownManaLevel = -1;

		private readonly UWTickClock mOFlaskLevelClock = new UWTickClock(UWFlaskAnimation.StepSeconds);

		/// <summary>The bubbling of the two flasks (see UWFlaskAnimation) - advanced every frame, so it
		/// runs on its own while nothing else changes.</summary>
		private readonly UWFlaskAnimation mOHealthBubbles = new UWFlaskAnimation();

		private readonly UWFlaskAnimation mOManaBubbles = new UWFlaskAnimation();

		/// <summary>The health flask's shown level, 0 to UWFlasks.FillLevels; -1 before the first
		/// Advance.</summary>
		public int ShownHealthLevel => miShownHealthLevel;

		public int ShownManaLevel => miShownManaLevel;

		/// <summary>The bubbling picture over the liquid, or -1.</summary>
		public int HealthBubbleFrame => mOHealthBubbles.Frame;

		public int ManaBubbleFrame => mOManaBubbles.Frame;

		/// <summary>
		/// Advances by the elapsed time towards the real values; pbFilling says the resurrection's
		/// fill animation runs (then nothing bubbles). Returns whether a level or a bubble changed.
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

			int liSteps = mOFlaskLevelClock.Advance(pfElapsedSeconds);

			for (int liStep = 0; liStep < liSteps; liStep++)
			{
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
			}

			// A flask whose level is moving does not bubble - and neither does one during the fill
			// animation of the resurrection (see UWFlaskView.StartFlaskFill).
			bool lbSettled = !pbFilling;

			lbChanged |= mOHealthBubbles.Advance(pfElapsedSeconds, miShownHealthLevel,
				lbSettled && miShownHealthLevel == liHealth);

			lbChanged |= mOManaBubbles.Advance(pfElapsedSeconds, miShownManaLevel,
				lbSettled && miShownManaLevel == liMana);

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
