namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S FOOTSTEPS AND SWIM STROKES (make_noise, Underworld Exhumed uw2 motion/PLAYMOVE.C:420; UW2 plan
	/// G2, 2026-11-13). The first game has neither. Per call:
	///
	///   swimming          every 15th call one stroke (0x1A) while the player moves and the last footstep's gap is over
	///   in the air        (motion state bit 3: levitating, flying) nothing
	///   on the ground     a footstep when the speed is above 0x2F and the gap is over: floor 1/2, ice with grip
	///                     0x2F/0x30, slippery ice 0x1D, the feet in turn; the gap is 6000 / (speed / 4 + 1) + 0x40
	///                     ticks, at most 200; an easy-movement step plays one at once and waits 100 ticks
	///
	/// Every sound plays at its entry's volume plus (speed >> 5) - 0x10, louder the faster. The pan (0x38 the left foot,
	/// 0x48 the right) is not built: no sound of the port is panned. The original counts its strokes in frames; the
	/// caller calls this at the original's 20 a second (CallsPerSecond), not at ours.
	/// </summary>
	public sealed class UWPlayerNoiseRulesUw2
	{
		/// <summary>step_sfx: two feet on floor, ice with grip, slippery ice.</summary>
		private static readonly int[] miStepSounds = { 1, 2, 0x2F, 0x30, 0x1D, 0x1D };

		public const int StrokeSound = 0x1A;

		/// <summary>A stroke every this many calls.</summary>
		public const int StrokeEvery = 15;

		/// <summary>
		/// THE FIRST STROKE COMES SOON AFTER ENTERING THE WATER, on this call (per user 2026-11-14: "one starts swimming at
		/// once"). The original's swim_count is a global that goes on from where it stopped, counted in its frames - under
		/// DOSBox far more than our 20 calls a second, so there the first stroke is heard at once; ours started from 0 and
		/// came after 0.75 s. Entering the water now sets the count so the stroke falls on the third call (0.15 s), and the
		/// last footstep's gap does not hold it back. Noted in Docs/DEVIATIONS.md.
		/// </summary>
		public const int FirstStrokeCall = 3;

		/// <summary>The speed above which a walk makes footsteps.</summary>
		public const int StepSpeed = 0x2F;

		/// <summary>The gap after an easy-movement step, in ticks.</summary>
		public const int EasyStepGap = 100;

		/// <summary>The longest gap between footsteps, in ticks.</summary>
		public const int LongestGap = 200;

		/// <summary>The rate the original calls make_noise at - its frames, as the swimming view counts them
		/// (UWSwimViewRules.OriginalRollsPerSecond).</summary>
		public const float CallsPerSecond = 20f;

		private int miFoot;

		private long mlNextStep;

		private int miSwimCount;

		private bool mbWasSwimming;

		/// <summary>The sound a call makes, or -1; piVolume its volume offset.</summary>
		public int Call(bool pbSwimming, bool pbInTheAir, bool pbAirborne, int piIce, int piSpeed, long plNow, bool pbEasyStep,
			out int piVolume)
		{
			piVolume = (piSpeed >> 5) - 0x10;

			if (pbSwimming && !mbWasSwimming)
			{
				miSwimCount = StrokeEvery - FirstStrokeCall;

				if (mlNextStep > plNow)
					mlNextStep = plNow - 1;
			}

			mbWasSwimming = pbSwimming;

			if (pbSwimming)
			{
				miSwimCount = (miSwimCount + 1) % StrokeEvery;

				return miSwimCount == 0 && piSpeed != 0 && plNow > mlNextStep ? StrokeSound : -1;
			}

			if (pbInTheAir || pbAirborne)
				return -1;

			int liIce = System.Math.Max(0, System.Math.Min(2, piIce));

			if (pbEasyStep)
			{
				int liSound = miStepSounds[(liIce * 2) + miFoot];

				miFoot ^= 1;
				mlNextStep = plNow + EasyStepGap;

				return liSound;
			}

			if (piSpeed <= StepSpeed || plNow <= mlNextStep)
				return -1;

			int liStep = miStepSounds[(liIce * 2) + miFoot];

			miFoot ^= 1;
			mlNextStep = plNow + Gap(piSpeed);

			return liStep;
		}

		/// <summary>6000 / ((speed >> 2) + 1) + 0x40, at most 200.</summary>
		public static int Gap(int piSpeed)
		{
			return System.Math.Min(LongestGap, (6000 / ((piSpeed >> 2) + 1)) + 0x40);
		}
	}
}
