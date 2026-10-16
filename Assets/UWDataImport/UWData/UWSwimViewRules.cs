using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SWIMMER'S VIEW as parse_effect moves it (motion/PLAYMOVE.C:527-544 of UnderworldExhumed;
	/// reference audit 2026-10-14, C6, the user's decision of 2026-10-15: "like the original, but
	/// with a switch like the head bob, off as well").
	///
	/// Every physics round while swimming the original dips the eye by the swim counter (ours
	/// keeps that in UWPlayerTerrain for every setting - it is how one sees oneself sinking).
	/// Once the counter is above 0x50 - always in deep water, which starts at 0x60 - it also moves
	/// the eye:
	///
	///   amplitude = speed * 4 / (full speed / 2) - 3, at least 1 - so 1 standing, 5 at full speed;
	///   phase = the clock byte / 16, the same byte as the head bob's (one second a round);
	///   bank = amplitude * RollTable[phase] * 64 while moving, a random -0x100 to 0xFF standing;
	///   height = RollTable[phase + 2] * 2 * amplitude, in eighths of a zpos step;
	///   heading and pitch = amplitude * a random -0x40 to 0x3F each,
	///
	/// angles in the original's 16-bit units. The same for every swimmer - the Swimming skill
	/// plays no part (ours scaled the sway with it until 2026-10-15; that is the Smooth setting now).
	///
	/// OURS DOES NOT HANG ON THE FRAME RATE: the original draws new values every round, ours
	/// OriginalRollsPerSecond times a second and holds them in between - the original as at
	/// twenty frames a second, as for the weapon's jitter (UWHeadBobRules).
	///
	/// The three settings are UWHeadBobRules.ModeEnum (UWUserSettings.SwimViewMode): Off - a calm
	/// view, Original - this, Smooth - our gentle sway with a strength slider.
	/// </summary>
	public static class UWSwimViewRules
	{
		/// <summary>rorEffect: a triangle over the sixteen phases of a second.</summary>
		private static readonly int[] miRollTable = { -4, -3, -2, -1, 0, 1, 2, 3, 4, 3, 2, 1, 0, -1, -2, -3 };

		/// <summary>Above this swim counter the view moves (swim_count > 0x50).</summary>
		public const int SwayThreshold = UWPlayerMotion.SwimCounterCheckThreshold;

		/// <summary>The amplitude never drops below this.</summary>
		public const int MinAmplitude = 1;

		/// <summary>speed * 4 / (full / 2) less this is the amplitude.</summary>
		private const int AmplitudeBias = 3;

		/// <summary>The bank while moving is the table value times the amplitude, shifted by six.</summary>
		private const int BankShift = 6;

		/// <summary>Standing, the bank is (rand and 0x1FF) - 0x100.</summary>
		private const int StandingBankRange = 0x200;

		private const int StandingBankBias = 0x100;

		/// <summary>Heading and pitch: amplitude times (rand and 0x7F) - 0x40.</summary>
		private const int JitterRange = 0x80;

		private const int JitterBias = 0x40;

		/// <summary>The table phase is two ahead for the height.</summary>
		private const int HeightPhaseLead = 2;

		/// <summary>How often the host draws new values in Original: the original at twenty frames
		/// a second, like the weapon's jitter.</summary>
		public const float OriginalRollsPerSecond = UWHeadBobRules.WeaponRollsPerSecond;

		/// <summary>The four offsets of the eye: height in eighths of a zpos step, the angles in
		/// the original's 16-bit units (0x10000 a full turn).</summary>
		public struct Offsets
		{
			public int HeightEighths;

			public int Heading;

			public int Pitch;

			public int Bank;
		}

		/// <summary>The amplitude for a speed against the full forward speed in water (pFPS, which
		/// newFPS sets for swimming). The speed is capped at the full one.</summary>
		public static int GetAmplitude(float pfSpeed, float pfFullSpeed)
		{
			if (pfFullSpeed <= 0f)
				return MinAmplitude;

			if (pfSpeed > pfFullSpeed)
				pfSpeed = pfFullSpeed;

			int liAmplitude = (int)(pfSpeed * 4f / (pfFullSpeed / 2f)) - AmplitudeBias;

			return liAmplitude < MinAmplitude ? MinAmplitude : liAmplitude;
		}

		/// <summary>
		/// One round of the original's swimming view, nothing at or below SwayThreshold. pfPhase
		/// is the clock byte (UWHeadBobRules.AdvancePhase), pbStanding whether the speed is zero,
		/// pfNext a random draw of 0 to one less than its argument - the bank first when standing,
		/// then heading and pitch, in the original's order.
		/// </summary>
		public static Offsets GetOriginal(int piSwimCounter, float pfPhase, float pfSpeed, float pfFullSpeed,
			bool pbStanding, Func<int, int> pfNext)
		{
			Offsets lOOffsets = default;

			if (piSwimCounter <= SwayThreshold || pfNext == null)
				return lOOffsets;

			int liAmplitude = pbStanding ? MinAmplitude : GetAmplitude(pfSpeed, pfFullSpeed);
			int liPhase = ((int)pfPhase >> 4) & 15;

			lOOffsets.Bank = pbStanding
				? pfNext(StandingBankRange) - StandingBankBias
				: (liAmplitude * miRollTable[liPhase]) << BankShift;

			lOOffsets.HeightEighths = miRollTable[(liPhase + HeightPhaseLead) & 15] * 2 * liAmplitude;
			lOOffsets.Heading = liAmplitude * (pfNext(JitterRange) - JitterBias);
			lOOffsets.Pitch = liAmplitude * (pfNext(JitterRange) - JitterBias);

			return lOOffsets;
		}

		/// <summary>A signed 16-bit angle in degrees.</summary>
		public static float AngleToDegrees(int piUnits)
		{
			return piUnits * 360f / UWWorldScale.FullCircleUnits;
		}
	}
}
