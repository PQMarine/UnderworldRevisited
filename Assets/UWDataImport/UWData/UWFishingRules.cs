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
		/// WATER OF HEIGHT 0 REFUSES EVERY CAST: the comparison is unsigned, 0 - 1 is 0xFFFF. Per
		/// user in the original 2026-10-17 (audit round 2 B8, level 1 at 7/7 facing the channel at
		/// 6/7): "You cannot fish there." every time; up a slope in higher water it worked, while
		/// swimming too. Until then ours let water of height 0 be fished.
		/// </summary>
		public static bool WaterLowEnough(int piPlayerHeightSteps, int piWaterHeightSteps)
		{
			return piPlayerHeightSteps > (ushort)(piWaterHeightSteps - 1);
		}
	}
}
