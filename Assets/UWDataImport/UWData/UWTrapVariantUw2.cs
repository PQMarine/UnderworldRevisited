namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S TRAP STEPS (G3 part H1, 2026-11-13): the fishing bite and the door trap's lock, from Underworld
	/// Exhumed's uw2 sources.
	/// </summary>
	public sealed class UWTrapVariantUw2 : IUWTrapVariant
	{
		/// <summary>go_fish (WORLDEV.C:538): no luck when rand() % 5 is above (Track + 7) / 8.</summary>
		public bool Bites(int piTrackSkill)
		{
			return UWRandom.Next(5) <= (piTrackSkill + 7) / 8;
		}

		/// <summary>TRAP_DOOR (TRIGGER.C:594): the lock is swapped only when the trap's owner is set.</summary>
		public bool SwapsLock(int piTrapOwner)
		{
			return piTrapOwner != 0;
		}
	}
}
