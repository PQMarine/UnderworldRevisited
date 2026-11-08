using System;

namespace UWDataImport.UWData
{
	/// <summary>The setting SkillRaises: how a skill raise's chance point falls.</summary>
	public enum UWSkillRaiseEnum
	{
		/// <summary>As the original: the chance point is rolled anew at every raise.</summary>
		Off = 0,

		/// <summary>The chance point always comes where it can.</summary>
		Highest = 1,

		/// <summary>The chance point is rolled once for each raise: loading again gives the same result.</summary>
		Fixed = 2
	}

	/// <summary>
	/// A SKILL RAISE'S CHANCE POINT AT ITS HIGHEST OR FIXED (per user, 2026-11-08): in the original a raise at
	/// a shrine or by a trainer gives one point, one more while the governing attribute's half lies above
	/// the skill, and a third by chance (UWPlayerVitals.TryIncreaseSkill) - players saved before chanting and
	/// loaded again when the chance point did not come.
	///   - HIGHEST gives it wherever it can come: like "Choose attributes at creation" it saves the reloading.
	///   - FIXED is the opposite, against that save-scumming: the roll comes from what loading cannot change -
	///     the character's name and attributes, the skill and its value before the raise (the first game's
	///     attributes never change) - so the same raise always ends the same way. The odds stay the
	///     original's: a number spread evenly over the roll's range, compared as the roll is. A GROUP
	///     MANTRA'S PICK of skills is fixed the same way (per user, the same day): keyed on the mantra, the
	///     attempt and the group's skill values at that moment (UWShrineRules.fAdvanceGroup) - the same
	///     chant picks the same skills after loading, a chant that raised something picks anew next time.
	/// Every limit stays (30, twice the attribute). Off and Highest roll a group mantra's pick as the original. Both games.
	/// </summary>
	public static class UWSkillRaise
	{
		/// <summary>Where the setting is read (the Unity layer's settings; the self-check its own); null reads Off.</summary>
		public static Func<UWSkillRaiseEnum> ModeSource { get; set; }

		public static UWSkillRaiseEnum Mode => ModeSource == null ? UWSkillRaiseEnum.Off : ModeSource();

		/// <summary>The roll's number in 0 .. piRange - 1: the original's random for Off, the fixed one for Fixed,
		/// 0 (the best) for Highest.</summary>
		public static int Roll(int piRange, Func<int, int> pRandom, string psName, int piStrength, int piDexterity,
			int piIntelligence, int piSkill, int piValue)
		{
			if (piRange <= 1)
				return 0;

			switch (Mode)
			{
				case UWSkillRaiseEnum.Highest:
					return 0;

				case UWSkillRaiseEnum.Fixed:
					return FixedRoll(piRange, psName, piStrength, piDexterity, piIntelligence, piSkill, piValue);

				default:
					return pRandom(piRange);
			}
		}

		/// <summary>The fixed roll: FNV-1a over the name and the numbers, the same on every machine and run
		/// (string.GetHashCode is not), spread over the range.</summary>
		public static int FixedRoll(int piRange, string psName, int piStrength, int piDexterity, int piIntelligence,
			params int[] piNumbers)
		{
			uint luHash = 2166136261;

			void fMix(int piByte)
			{
				luHash ^= (uint)(piByte & 0xFF);
				luHash *= 16777619;
			}

			foreach (char lcChar in psName ?? string.Empty)
			{
				fMix(lcChar);
				fMix(lcChar >> 8);
			}

			fMix(0);
			fMix(piStrength);
			fMix(piDexterity);
			fMix(piIntelligence);

			foreach (int liNumber in piNumbers)
			{
				fMix(liNumber);
				fMix(liNumber >> 8);
			}

			// The last mixing spreads the low bits before the modulo.
			luHash ^= luHash >> 15;
			luHash *= 0x2C1B3C6D;
			luHash ^= luHash >> 12;

			return (int)(luHash % (uint)piRange);
		}
	}
}
