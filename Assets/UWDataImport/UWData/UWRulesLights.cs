namespace UWDataImport.UWData
{
	/// <summary>
	/// THE LIGHT RULES of the rule set (lights.*; cleanup phase 10, wave 2, 2026-10-26): the oil a
	/// flask gives a lantern or a torch, how fast the carried lights burn in a night, and the
	/// glowing things of the world as a table (UWLightSourceTable). Each default is the const or the
	/// table of the class that applies it; see UWRules.
	///
	/// NOT HERE: how fast a light burns while awake is the game data's (the light table's duration
	/// byte of OBJECTS.DAT, UWInventoryModel.BurnTick), and how far a light reaches is SHADES.DAT's.
	/// The Remastered mode's columns of the table (reach in tiles, brightness, flicker, glow, fire)
	/// are presentation tuning in fractions and stay in UWLightSourceTable.
	/// </summary>
	public sealed class UWRulesLights
	{
		[UWRule(typeof(UWObjectMechanics), nameof(UWObjectMechanics.OilFlaskFill),
			"Fuel (quality points, 63 full) a flask of oil gives a lantern or a torch.", Minimum = 0)]
		public int OilFlaskFill = UWObjectMechanics.OilFlaskFill;

		[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.SleepTicksPerHour),
			"Player ticks a sleeping hour counts for the carried lights' burning.", Minimum = 0)]
		public int SleepTicksPerHour = UWSleepRules.SleepTicksPerHour;

		public readonly SourceRules Sources = new SourceRules();

		/// <summary>lights.sources.*: the nine glowing things of the world, one column each - which
		/// object, the light level it shines with in the palette mode (0-7, as a carried light of that
		/// level would), and whether it is open fire (1: it flickers, its light jitters, and it casts
		/// no ground blob like the others). The rows keep their order; the Remastered columns of a row
		/// stay its own.</summary>
		public sealed class SourceRules
		{
			[UWRule(typeof(UWLightSourceTable), nameof(UWLightSourceTable.OriginalIds),
				"The object ids that glow: torch, candle, lantern, campfire, fire elemental, glowing stone, mushroom, orb, orb stone (9 numbers).",
				Minimum = 0)]
			public int[] Ids = (int[])UWLightSourceTable.OriginalIds.Clone();

			[UWRule(typeof(UWLightSourceTable), nameof(UWLightSourceTable.OriginalPaletteLightLevels),
				"Palette mode: the light level each of them shines with, in the order of ids (9 numbers, 0-7).",
				Minimum = 0)]
			public int[] PaletteLightLevel = (int[])UWLightSourceTable.OriginalPaletteLightLevels.Clone();

			[UWRule(typeof(UWLightSourceTable), nameof(UWLightSourceTable.OriginalOpenFire),
				"1 for open fire, 0 for a steady light, in the order of ids (9 numbers).", Minimum = 0)]
			public int[] OpenFire = (int[])UWLightSourceTable.OriginalOpenFire.Clone();
		}
	}
}
