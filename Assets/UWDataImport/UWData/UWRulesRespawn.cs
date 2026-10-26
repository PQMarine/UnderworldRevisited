namespace UWDataImport.UWData
{
	/// <summary>
	/// THE RESPAWN RULES of the rule set (respawn.*; cleanup phase 10 wave 2, 2026-10-26): the
	/// wandering monsters - how often the five-minute block calls the creation traps with a zero
	/// flags field, how far from the player they fire, and how near a creature they made before
	/// keeps them from making another (UWRespawnRules, UWTrapRules). Each default is the const of
	/// the class that applies it; see UWRules.
	/// </summary>
	public sealed class UWRulesRespawn
	{
		[UWRule(typeof(UWRespawnRules), nameof(UWRespawnRules.Chance),
			"The five-minute block calls the wandering monsters with one chance in this.", Minimum = 1)]
		public int ChanceInN = UWRespawnRules.Chance;

		[UWRule(typeof(UWRespawnRules), nameof(UWRespawnRules.PlayerNearTiles),
			"A creation trap fires only this many tiles or more from the player on one axis (not while sleeping).",
			Minimum = 0)]
		public int PlayerNearTiles = UWRespawnRules.PlayerNearTiles;

		[UWRule(typeof(UWTrapRules), nameof(UWTrapRules.CrowdTilesBack),
			"A creature template makes nothing while a creature it made is within this many tiles back ...",
			Minimum = 0)]
		public int CrowdTilesBack = UWTrapRules.CrowdTilesBack;

		[UWRule(typeof(UWTrapRules), nameof(UWTrapRules.CrowdTilesForward),
			"... or this many tiles forward of it, on both axes.", Minimum = 0)]
		public int CrowdTilesForward = UWTrapRules.CrowdTilesForward;
	}
}
