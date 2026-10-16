namespace UWDataImport.UWData
{
	/// <summary>
	/// Where the fishing pole may be cast (go_fish, UnderworldExhumed event/WORLDEV.C, read
	/// 2026-10-15). The Unity side (UWFishing) finds the tile 11 eighths ahead and asks here.
	/// </summary>
	public static class UWFishingRules
	{
		/// <summary>
		/// Whether the water's floor lies low enough: go_fish refuses with "You cannot fish there."
		/// when (player zpos &gt;&gt; 3) &lt;= (uint16)(water height - 1), so the player's height
		/// step must be at least the water's - the water may not lie above the player. Until
		/// 2026-10-15 ours let it lie up to two steps above (the reference's rule; reference audit
		/// round 2, A49).
		///
		/// NOT BUILT (B8, waiting for a test in the original): the unsigned comparison makes water
		/// of height 0 refuse every cast (0 - 1 is 0xFFFF). Ours accepts it; the test would join
		/// here as "piWaterHeightSteps == 0 refuses".
		/// </summary>
		public static bool WaterLowEnough(int piPlayerHeightSteps, int piWaterHeightSteps)
		{
			return piPlayerHeightSteps >= piWaterHeightSteps;
		}
	}
}
