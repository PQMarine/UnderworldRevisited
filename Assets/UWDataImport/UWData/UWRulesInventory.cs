namespace UWDataImport.UWData
{
	/// <summary>
	/// THE INVENTORY RULES of the rule set (inventory.*; cleanup phase 10, wave 2, 2026-10-26): what
	/// the player can carry and how far his hand reaches (UWPlayerData, UWReachRules). Each default
	/// is the const of the class that applies it; see UWRules.
	///
	/// NOT HERE, on purpose: the weights and the container capacities are the game data's
	/// (COMOBJ.DAT, OBJECTS.DAT), not the code's; the stack limit is a deviation without a second
	/// path (UWStackRules.MaxStackCount - above 511 the count turns into a special property); the
	/// quality band of a stack is a shift (quality &gt;&gt; 4) and stays code.
	/// </summary>
	public sealed class UWRulesInventory
	{
		[UWRule(typeof(UWPlayerData), nameof(UWPlayerData.WeightPerStrength),
			"Carrying capacity (tenths of a stone) per point of Strength, given to a new character; a save keeps its own.")]
		public int CarryPerStrength = UWPlayerData.WeightPerStrength;

		public readonly ReachRules Reach = new ReachRules();

		/// <summary>inventory.reach.*: the squared distance in eighths of a tile the hand reaches -
		/// bare, with a pole, for a close look - and the height window above and below the feet
		/// (doubled with the pole).</summary>
		public sealed class ReachRules
		{
			[UWRule(typeof(UWReachRules), nameof(UWReachRules.UseDistanceSquared),
				"The bare hand's reach for using and taking, squared, in eighths of a tile (144: one and a half tiles).",
				Minimum = 1)]
			public int UseDistanceSquared = UWReachRules.UseDistanceSquared;

			[UWRule(typeof(UWReachRules), nameof(UWReachRules.PoleDistanceSquared),
				"The reach with a pole in hand, squared, in eighths of a tile.", Minimum = 1)]
			public int PoleDistanceSquared = UWReachRules.PoleDistanceSquared;

			[UWRule(typeof(UWReachRules), nameof(UWReachRules.LookDetailDistanceSquared),
				"Within this squared distance a look at a fixed thing shows its picture.", Minimum = 1)]
			public int LookDetailDistanceSquared = UWReachRules.LookDetailDistanceSquared;

			[UWRule(typeof(UWReachRules), nameof(UWReachRules.ReachAbove),
				"How far above the feet a thing may be, in height steps (8 to a floor level).", Minimum = 0)]
			public int Above = UWReachRules.ReachAbove;

			[UWRule(typeof(UWReachRules), nameof(UWReachRules.ReachBelow),
				"How far below the feet a thing may be, in height steps.", Minimum = 0)]
			public int Below = UWReachRules.ReachBelow;
		}
	}
}
