namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIRST GAME'S CREATURE STEPS (phase 0 step 2, 2026-10-28): what UWCritterBrain did at these
	/// steps until then, moved here unchanged.
	/// </summary>
	public sealed class UWCritterVariantUw1 : IUWCritterVariant
	{
		/// <summary>Only a hostile creature tries a door it bumped into; any other stands blocked.</summary>
		public bool TriesDoor(int piAttitude)
		{
			return piAttitude == UWNpc.AttitudeHostile;
		}

		/// <summary>The death sound when the row's byte 8 low three bits are 1.</summary>
		public int DeathCry(UWObjectClassProperties.Critter pORow)
		{
			return (pORow.RowByte(8) & 7) == 1 ? UWCritterRules.DeathSound : -1;
		}
	}
}
