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
		/// <summary>The string of block 1 a new level's line begins with (advance: game_sprint 0x93 in UW1, 0xA1 in
		/// UW2, one more in our block).</summary>
		int LevelUpString { get; }

		/// <summary>What a newly mapped tile counts for, before the tenth (process_grid: the dungeon level in UW1,
		/// the world in UW2, GRIDDB.C:256).</summary>
		int ExplorationWeight(int piDungeonLevel);

		/// <summary>The maximum mana goes aside on one level (UW1's Tybal, player_compute; UW2 has no such case).</summary>
		bool HasOrbLair { get; }

		/// <summary>player_get_exp's gate at the present total (UW1: above the award cap a gain is
		/// ignored entirely, SKILLCHK.C:85; UW2: it still counts and gives skill points but no more
		/// levels, and a gain past 0x7FFF0 is refused, SKILLCHK.C:88-91).</summary>
		UWExperienceGateEnum ExperienceGate(UWRulesExperience pORules, int piExperience, int piGain);

		/// <summary>The gain that counts (UW1: halved, plus one, on a dungeon level too shallow for
		/// the character - twice the level plus 2 below its level, SKILLCHK.C:88; UW2: every gain halved
		/// first, rounding at random, and the shallow test by the world number, SKILLCHK.C:70-79).</summary>
		int ExperienceGain(int piGain, int piDungeonLevel, int piCharacterLevel);

		/// <summary>Whether a chosen sleep is allowed on this level (UW1: not in the Void, SKILLS.C:525;
		/// UW2: everywhere, the Void wakes the player instead, SKILLS.C:436).</summary>
		bool MaySleepAt(UWRules pORules, int piDungeonLevel);

		/// <summary>Refused as if enemies stood near (UW2: in the pits, SKILLS.C:441; UW1: never).</summary>
		bool IsHeldAwake { get; }

		/// <summary>The line of lying down, in our numbering: piHow 0 on the ground, 1 a bedroll, 2 a bed (UW1: "You
		/// make camp." for all; UW2 has a line each for the last two, SKILLS.C:449).</summary>
		int CampLine(int piHow);

		/// <summary>Passing out here kills (UW2: in the pits, SKILLS.C:486; UW1: never).</summary>
		bool PassingOutKills { get; }

		/// <summary>The first game's dreams of Garamon by its quest flag; the second game's are its own, by the castle's
		/// clock, and come with its cutscenes (SKILLS.C:334).</summary>
		bool HasGaramonDreams { get; }

		/// <summary>duration_check's long cycle, hunger and healing, in player ticks (UW1: 24, PLAYTIME.C:128;
		/// UW2: 30, PLAYTIME.C:137).</summary>
		int LongCycleTicks { get; }

		/// <summary>The target the Search skill rolls against to find a trap on an object (UW1: 8,
		/// SKILLS.C:898; UW2: 10 plus twice the world number, SKILLS.C:885).</summary>
		int TrapDetectTarget();

		/// <summary>The target the Traps skill rolls against to disarm one (UW1: 8, SKILLS.C:928;
		/// UW2: 8 plus the world number, SKILLS.C:923).</summary>
		int TrapDisarmTarget();

		/// <summary>Whether restore_mana gives anything here - every mana gain but the plain regeneration of
		/// worn things goes through it: the mana spell, the minute's regeneration, a night, a mushroom (UW1:
		/// always; UW2: not in the Scintillus Academy, world 5, but on its first level and on its eighth from
		/// x 25 on, SPELLS.C:160). piDungeonLevel 1-based, piTileX -1 unknown.</summary>
		bool MayRestoreMana(int piDungeonLevel, int piTileX);
	}
}
