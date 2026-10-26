namespace UWDataImport.UWData
{
	/// <summary>
	/// THE LEVELS THE STORY KNOWS BY NUMBER, in the rule set (levels.*; cleanup phase 10, wave 2,
	/// 2026-10-26): not balance but the anchors a total conversion repoints (Docs/CLEANUP-PLAN.md
	/// section 6), all ZERO-BASED as the port counts its levels. Each default is UWLevelIds' const;
	/// see UWRules. The per-level table of the mod plan (section 3.4, levels.json) can grow out of
	/// this later.
	/// </summary>
	public sealed class UWRulesLevels
	{
		[UWRule(typeof(UWLevelIds), nameof(UWLevelIds.NewMoonstone),
			"The level a new game's moonstone lies on (0-based).", Minimum = 0)]
		public int NewMoonstone = UWLevelIds.NewMoonstone;

		[UWRule(typeof(UWLevelIds), nameof(UWLevelIds.CupOfWonder),
			"The level of the Cup of Wonder, where the mandolin's melody and the shrine's direction point (0-based).",
			Minimum = 0)]
		public int CupOfWonder = UWLevelIds.CupOfWonder;

		[UWRule(typeof(UWLevelIds), nameof(UWLevelIds.Bullfrog),
			"The level of the bullfrog puzzle; its trap says so on any other (0-based).", Minimum = 0)]
		public int Bullfrog = UWLevelIds.Bullfrog;

		[UWRule(typeof(UWLevelIds), nameof(UWLevelIds.Lair),
			"Tybal's lair, where his orb holds the mana back (0-based).", Minimum = 0)]
		public int Lair = UWLevelIds.Lair;

		[UWRule(typeof(UWLevelIds), nameof(UWLevelIds.Volcano),
			"The volcano's level, where a talisman burns in its lava (0-based).", Minimum = 0)]
		public int Volcano = UWLevelIds.Volcano;

		[UWRule(typeof(UWLevelIds), nameof(UWLevelIds.Void),
			"The Ethereal Void: no magic, no sleep, drawn unlit, nothing comes to rest (0-based).", Minimum = 0)]
		public int Void = UWLevelIds.Void;
	}
}
