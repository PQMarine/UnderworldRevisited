namespace UWDataImport.UWData
{
	/// <summary>What happens to an experience gain at the player's present total (IUWPlayerVariant).</summary>
	public enum UWExperienceGateEnum
	{
		/// <summary>The gain is ignored, skill points and levels too.</summary>
		Ignore,

		/// <summary>The gain counts and gives skill points, but no more levels.</summary>
		CountWithoutLevels,

		/// <summary>The gain counts in full.</summary>
		Count
	}

	/// <summary>
	/// THE STEPS OF THE PLAYER'S RULES WHERE THE GAMES PART (Docs/UW2-PLAN.md section 4.3, phase 0
	/// step 2, 2026-10-28), from Underworld Exhumed's list of differences (docs/UW1-UW2-DIFFERENCES.md,
	/// "Skills, experience and the player record"). The numbers that differ (a skill point every 3000
	/// or 1500, the food's healing, the interrupted sleep, the new character's fatigue and clock, the
	/// light's last quality) are rule set entries; death, which the second game replaces as a whole
	/// (jail, the pits, the blackrock gem), is each game's own system.
	/// </summary>
	public interface IUWPlayerVariant
	{
		/// <summary>player_get_exp's gate at the present total (UW1: above the award cap a gain is
		/// ignored entirely, SKILLCHK.C:85; UW2: it still counts and gives skill points but no more
		/// levels, and a gain past 0x7FFF0 is refused, SKILLCHK.C:88-91).</summary>
		UWExperienceGateEnum ExperienceGate(UWRulesExperience pORules, int piExperience, int piGain);

		/// <summary>The gain that counts (UW1: halved, plus one, on a dungeon level too shallow for
		/// the character - twice the level plus 2 below its level, SKILLCHK.C:88; UW2: every gain halved
		/// first, rounding at random, and the shallow test by the world number, SKILLCHK.C:70-79).</summary>
		int ExperienceGain(int piGain, int piDungeonLevel, int piCharacterLevel);

		/// <summary>Whether a chosen sleep is allowed on this level (UW1: not in the Void, SKILLS.C:525;
		/// UW2: not in the pits, and in the Void it wakes the player, SKILLS.C:441).</summary>
		bool MaySleepAt(UWRules pORules, int piDungeonLevel);

		/// <summary>The target the Search skill rolls against to find a trap on an object (UW1: 8,
		/// SKILLS.C:898; UW2: 10 plus twice the world number, SKILLS.C:885).</summary>
		int TrapDetectTarget();

		/// <summary>The target the Traps skill rolls against to disarm one (UW1: 8, SKILLS.C:928;
		/// UW2: 8 plus the world number, SKILLS.C:923).</summary>
		int TrapDisarmTarget();
	}
}
