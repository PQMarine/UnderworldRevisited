namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STEPS OF THE ITEMS' RULES WHERE THE GAMES PART (Docs/UW2-PLAN.md section 4.3, phase 0
	/// step 2, 2026-10-28), from Underworld Exhumed's list of differences (docs/UW1-UW2-DIFFERENCES.md,
	/// "Items and objects"). What only the second game has (the key ring, the bed, owned containers
	/// angering their owners, the leftovers of eating, the dream plant, the second moonstone, a
	/// breaking pick) comes with its variant in phase 4; the containers' lists are each game's data.
	/// </summary>
	public interface IUWObjectVariant
	{
		/// <summary>Whether a light of this quality is used up and will not light (UW1: quality 0,
		/// USEITEMS.C:522; UW2: 1 or less, USEITEMS.C:620).</summary>
		bool LightUsedUp(int piQuality);

		/// <summary>Rolls which valuable a critter carries, as an offset from the first valuable
		/// (UW1: by the dungeon level, TREASURE.C:107; UW2: by the world, never type 1,
		/// TREASURE.C:103-106).</summary>
		int RollValuableType(int piDungeonLevel);

		/// <summary>The skill check of picking a lock of this height (UW1: Picklock against three
		/// times the height, USEITEMS.C:232; UW2: Picklock plus 1, USEITEMS.C:535). The heights that
		/// refuse every pick are the same in both games.</summary>
		UWSkillCheck.ResultEnum PickRoll(int piPicklock, int piLockHeight);
	}
}
