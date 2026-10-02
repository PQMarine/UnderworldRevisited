namespace UWDataImport.UWData
{
	/// <summary>
	/// THE HEAD BOB WHILE WALKING, read 2026-09-23 (per user: "we have no head bob").
	///
	/// PlayerMotion_seg034_2F89_604 (labels 678 to 6DE) sets the camera's height offset
	/// dseg_5c99_3580, which PositionCameraAtObject_seg034_2F89_B99 adds to the eye height 0xA4
	/// (label C07). The per-frame game loop of the same segment clears the offset and advances
	/// a phase byte, dseg_5c99_729, by the PIT ticks of that frame - 256 per second, so the
	/// byte wraps once a second.
	///
	///   only while the motion command is 1 (walking or turning), not while the player is
	///   off the ground (tile state bit 0x10), and only when the current speed is above a
	///   quarter of the full forward speed;
	///   factor = current speed * 4 / (full speed / 2) - 1, at least 2 - so 7 at full speed;
	///   offset = BobTable[phase / 16] * factor, in eighths of a zpos step.
	///
	/// The table (dseg_5c99_72A) is one step, 1 3 4 3 1 -3 0 0, twice - two steps a second. At
	/// full speed the eye rises by 28 eighths and dips by 21.
	///
	/// Swimming has its own sway (UWPlayerTerrain), the jump its own dip (motion command 7,
	/// not built).
	///
	/// THE WEAPON'S JITTER is a rule of its own (seg036_3087_245D, which draws the weapon in
	/// the view, and seg036_3087_14C8, found 2026-09-23 after the user saw it depend on the
	/// input, not on the bob). Every time the weapon is drawn at rest, a level comes from the
	/// motion speed: 0 when standing, else speed * 2 / 0x31F + 1 against a base speed of
	/// 0x3AC - so 1 below 42 per cent of the normal speed, 2 below 85, 3 above. Level 1 rolls
	/// a new offset of 0 to 4 pixels to the LEFT, level 2 of 0 to 9, level 3 and above keep
	/// the last one, standing sets it to 0. During the swing frames the offset is 0. The sign
	/// never turns: the offset is always subtracted, for the left-handed weapon art too.
	///
	/// OURS DOES NOT HANG ON THE FRAME RATE: the original rolls per drawn frame, we roll
	/// WeaponRollsPerSecond times a second - the original as if it ran at twenty frames a
	/// second (per user), jumping like it. And it measures the real movement,
	/// so walking into a wall counts as standing - the original keeps its speed against a
	/// north or east wall and jitters there (per user), a slip we do not copy.
	/// </summary>
	public static class UWHeadBobRules
	{
		/// <summary>dseg_5c99_72A: one step of the bob, twice, indexed by the phase / 16.</summary>
		private static readonly int[] miBobTable = { 1, 3, 4, 3, 1, -3, 0, 0, 1, 3, 4, 3, 1, -3, 0, 0 };

		/// <summary>The phase byte wraps at this; it advances by PIT ticks, 256 a second.</summary>
		public const int PhaseRange = 256;

		/// <summary>The factor never drops below this once the bob runs (label 6A6).</summary>
		public const int MinFactor = 2;

		/// <summary>Advances the phase by the real seconds of a frame, as the original adds the
		/// frame's PIT ticks. The fraction is carried in the float.</summary>
		public static float AdvancePhase(float pfPhase, float pfSeconds)
		{
			float lfPhase = pfPhase + (pfSeconds * UWPlayerTick.PitTicksPerSecond);

			return lfPhase % PhaseRange;
		}

		/// <summary>
		/// The camera's height offset in eighths of a zpos step. pfSpeed and pfFullSpeed are
		/// the current and the full forward speed in any common unit; the original compares
		/// them in its own motion units, the ratio is what counts. The speed is capped at the
		/// full one - the original's never exceeds it, a measured one can for a frame.
		/// </summary>
		public static int GetOffset(float pfPhase, float pfSpeed, float pfFullSpeed)
		{
			if (pfFullSpeed <= 0f || pfSpeed <= pfFullSpeed / 4f)
				return 0;

			if (pfSpeed > pfFullSpeed)
				pfSpeed = pfFullSpeed;

			int liFactor = (int)(pfSpeed * 4f / (pfFullSpeed / 2f)) - 1;

			if (liFactor < MinFactor)
				liFactor = MinFactor;

			return miBobTable[((int)pfPhase >> 4) & 15] * liFactor;
		}

		/// <summary>The three settings of the switch (per user, 2026-09-23): none, the original's
		/// steps, or ours smoothed.</summary>
		public enum ModeEnum
		{
			Off = 0,
			Original = 1,
			Smooth = 2
		}

		/// <summary>
		/// OURS, the Smooth setting: the same table and the same amplitude, but without the
		/// original's two kinds of steps. The height glides through the sixteen table values on a
		/// Catmull-Rom curve instead of holding each for a sixteenth of a second, and the factor
		/// grows with the speed without being cut to whole numbers (still at least MinFactor).
		/// In eighths of a zpos step, like GetOffset.
		/// </summary>
		public static float GetSmoothOffset(float pfPhase, float pfSpeed, float pfFullSpeed)
		{
			if (pfFullSpeed <= 0f || pfSpeed <= pfFullSpeed / 4f)
				return 0f;

			if (pfSpeed > pfFullSpeed)
				pfSpeed = pfFullSpeed;

			float lfFactor = (pfSpeed * 4f / (pfFullSpeed / 2f)) - 1f;

			if (lfFactor < MinFactor)
				lfFactor = MinFactor;

			float lfPosition = pfPhase / 16f;
			int liAt = (int)System.Math.Floor(lfPosition);
			float lfT = lfPosition - liAt;

			float lfP0 = miBobTable[(liAt - 1) & 15];
			float lfP1 = miBobTable[liAt & 15];
			float lfP2 = miBobTable[(liAt + 1) & 15];
			float lfP3 = miBobTable[(liAt + 2) & 15];

			float lfT2 = lfT * lfT;
			float lfT3 = lfT2 * lfT;

			float lfValue = 0.5f * ((2f * lfP1) + ((lfP2 - lfP0) * lfT)
				+ (((2f * lfP0) - (5f * lfP1) + (4f * lfP2) - lfP3) * lfT2)
				+ (((3f * lfP1) - lfP0 - (3f * lfP2) + lfP3) * lfT3));

			return lfValue * lfFactor;
		}

		/// <summary>The original's base forward speed (dseg_5c99_CE), the unit of the jitter
		/// levels.</summary>
		public const int OriginalBaseSpeed = 0x3AC;

		/// <summary>The divisor of the jitter level (seg036_3087_245D).</summary>
		public const int JitterLevelStep = 0x31F;

		/// <summary>
		/// The jitter level for a speed given as a fraction of the normal forward speed: 0
		/// standing, else speed * 2 / 0x31F + 1 in the original's units.
		/// </summary>
		public static int GetWeaponJitterLevel(float pfFractionOfNormalSpeed)
		{
			if (pfFractionOfNormalSpeed <= 0f)
				return 0;

			int liSpeed = (int)(pfFractionOfNormalSpeed * OriginalBaseSpeed);

			return (liSpeed * 2 / JitterLevelStep) + 1;
		}

		/// <summary>How many pixels the level rolls from (the offset is 0 up to one less, to the
		/// left); 0 for "set it to 0" and -1 for "keep the last one".</summary>
		public static int GetWeaponJitterRange(int piLevel)
		{
			switch (piLevel)
			{
				case 0: return 0;
				case 1: return 5;
				case 2: return 10;
				default: return -1;
			}
		}

		/// <summary>How often the host rolls a new offset: the original rolls on every drawn
		/// frame, so this is the original at twenty frames a second (per user, 2026-09-23) - the
		/// offset jumps as there, it does not glide, but no longer hangs on our frame rate.
		/// Six a second with gliding was tried first and felt too far from the original.</summary>
		public const float WeaponRollsPerSecond = 20f;

		/// <summary>OURS, the weapon's Smooth setting (per user, 2026-09-23: "Original is too
		/// tiring, but without it something is missing"): fewer rolls a second, and the drawn
		/// offset glides towards the rolled one instead of jumping.</summary>
		public const float WeaponSmoothRollsPerSecond = 6f;

		/// <summary>OURS: how fast the Smooth offset glides, per second - an exponential approach,
		/// the same at every frame rate.</summary>
		public const float WeaponSmoothGlidePerSecond = 12f;

		/// <summary>OURS: below this share of the normal speed one counts as standing - the
		/// measured speed is never exactly zero while the physics settles.</summary>
		public const float StandingFraction = 0.02f;
	}
}
