namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT AN IMPACT DOES TO THE PLAYER - the sound, the volume and the fall damage. One block
	/// for all of it, ApplyPlayerMotion_seg008_90D labels AF3 to B95 (56455-56540), and it makes
	/// no difference whether one falls onto the floor or runs into a wall: both feed the same
	/// accumulator.
	///
	/// WHERE THE IMPACT COMES FROM, and there are two ways in (seg030_2B26). SIDEWAYS, labels
	/// B8E to BB2 (104520-104530): the horizontal momentum times fifteen minus the surviving
	/// fraction, over fifteen - so a glancing touch is nothing and a head-on stop at a full run
	/// is the whole momentum. DOWNWARDS, label E2D (104930-104940): the VERTICAL speed of the
	/// motion array times the same fifteen minus the surviving fraction, taken as an absolute
	/// value and ASSIGNED rather than added - and there without the division by fifteen, which
	/// we could not reconcile with the numbers and did not take over (see JumpMomentum).
	///
	/// THE BLOCK, in order:
	///
	///   value = impact / 256, doubled while the pitch field is set
	///   a skill check of ACROBAT against value * 2; if it PASSES,
	///       value = value * (30 - Acrobat) / 30
	///   above 3 the player takes that as damage
	///   the sound plays when value is above 1, or when the tile under the feet is liquid
	///   its volume offset is value * 4 - 60
	///
	/// WHY THE WALL IMPACT IS SO HARD TO PROVOKE (the user could not pin it down, 2026-09-22):
	/// the Acrobat skill. At a full run the momentum is 0x3AC, so the value is 3 - just enough
	/// for the sound and just short of damage. Henrietta carries Acrobat 25 (PLAYER.DAT 0x32 in
	/// all four of the user's saves), her check against a target of 6 passes nearly always, and
	/// 3 times 5 over 30 is ZERO: no sound at all. A fresh character has almost no Acrobat, the
	/// check fails, the 3 survives and it sounds. The "not every time" is that same roll.
	/// </summary>
	public static class UWImpactRules
	{
		/// <summary>The impact accumulator is in momentum units; the value the block works with
		/// is its high byte (label AFD: shr ax, 8).</summary>
		public const int MomentumPerPoint = 0x100;

		/// <summary>An Acrobat of this much takes the whole impact away - (30 - skill) / 30
		/// (labels B2E to B47).</summary>
		public const int AcrobatFull = 30;

		/// <summary>The check the Acrobat skill is rolled against is twice the value
		/// (label B13).</summary>
		public const int AcrobatCheckFactor = 2;

		/// <summary>Above this the impact hurts (label B48).</summary>
		public const int DamageThreshold = 3;

		/// <summary>Above this it sounds on dry ground (label B6C).</summary>
		public const int SoundThreshold = 1;

		/// <summary>Volume offset of the impact sound: the value times four, less sixty
		/// (label B79, 0xFFC4).</summary>
		public const int VolumeFactor = 4;

		public const int VolumeOffset = -60;

		/// <summary>The lowest and highest offset the sound routine takes.</summary>
		public const int MinVolume = -60;

		public const int MaxVolume = 40;

		/// <summary>The momentum a jump takes off with (seg008_1B2A, label 56700 writes 0x263
		/// into the vertical field of the motion array). At a full run the horizontal one is
		/// 0x3AC, which is what a wall impact is measured against.</summary>
		public const int JumpMomentum = 0x263;

		/// <summary>The horizontal momentum of a full run - see JumpMomentum.</summary>
		public const int RunMomentum = 0x3AC;

		/// <summary>The impact value before the Acrobat skill: the accumulated momentum over
		/// 256, doubled while falling (label B10, the pitch field).</summary>
		public static int GetImpactValue(int piMomentumLost, bool pbFalling)
		{
			if (piMomentumLost <= 0)
				return 0;

			int liValue = piMomentumLost / MomentumPerPoint;

			return pbFalling ? liValue * 2 : liValue;
		}

		/// <summary>What the Acrobat check is rolled against - see AcrobatCheckFactor.</summary>
		public static int GetAcrobatTarget(int piValue)
		{
			return piValue * AcrobatCheckFactor;
		}

		/// <summary>What is left of the impact after a PASSED Acrobat check. A failed one leaves
		/// it whole, which is why a beginner feels every wall.</summary>
		public static int ApplyAcrobat(int piValue, int piAcrobatSkill, bool pbCheckPassed)
		{
			if (!pbCheckPassed || piValue <= 0)
				return piValue;

			if (piAcrobatSkill >= AcrobatFull)
				return 0;

			if (piAcrobatSkill < 0)
				piAcrobatSkill = 0;

			return (piValue * (AcrobatFull - piAcrobatSkill)) / AcrobatFull;
		}

		/// <summary>
		/// Whether the impact is heard. Above SoundThreshold always; below it only over
		/// LIQUID - the tile state's bit 0x10, which the drowning work of 2026-09-20 read as
		/// water under the feet. That is the splash of a landing in water, which sounds however
		/// gently one comes down.
		/// </summary>
		public static bool PlaysSound(int piValue, bool pbOverLiquid)
		{
			return piValue > SoundThreshold || pbOverLiquid;
		}

		public static bool CausesDamage(int piValue)
		{
			return piValue > DamageThreshold;
		}

		public static int GetVolume(int piValue)
		{
			int liVolume = (piValue * VolumeFactor) + VolumeOffset;

			if (liVolume < MinVolume)
				return MinVolume;

			return liVolume > MaxVolume ? MaxVolume : liVolume;
		}
	}
}
