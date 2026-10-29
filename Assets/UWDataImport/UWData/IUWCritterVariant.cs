namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STEPS OF THE CREATURES' RULES WHERE THE GAMES PART (Docs/UW2-PLAN.md section 4.3, phase 0
	/// step 2, 2026-10-28), from Underworld Exhumed's list of differences (docs/UW1-UW2-DIFFERENCES.md,
	/// "AI and critters"). The numbers that differ (the path flood of 32 or 40 steps, the owners'
	/// races, the corpse's chance) are rule set entries; what only one game has (UW1's guards of
	/// Tybal, UW2's held goal 15 and its talkers turning guard) comes with that game's variant or
	/// system.
	/// </summary>
	public interface IUWCritterVariant
	{
		/// <summary>Whether a creature that bumped into a closed door and did not give up on the roll
		/// tries to open it (UW1: only a hostile one, PATHFIND.C:1154; UW2: any, except on level 10,
		/// PATHFIND.C:1282 and :1394).</summary>
		bool TriesDoor(int piAttitude);

		/// <summary>The sound a dying creature cries, or -1 for none (UW1: effect 6 when the row's byte
		/// 8 says so, AI.C:1441; UW2: one of four by the death kind, AI.C:1437).</summary>
		int DeathCry(UWObjectClassProperties.Critter pORow);
	}
}
