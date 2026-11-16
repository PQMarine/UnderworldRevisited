namespace UWDataImport.UWData
{
	/// <summary>
	/// USING THINGS IN THE SECOND GAME'S WORLD (UW2 plan G4 part A, 2026-11-13): the parts of UseRect (uw2
	/// obj/USEITEMS.C) on the wall's things - the switches (minor 3, 0x170-0x17F) flip, the lever (0x161) and the switch
	/// picture (0x162) step their flags through 0 to 7 - and UseObj's checkTrap with mode 4 after the use (OBJUSE.C).
	/// Doors, the bed and the rest stay on the Unity side (UWWalkUw2View); the chest dumped out in the world (UseCont,
	/// DumpTheBag) is not built yet.
	/// </summary>
	public static class UWUseRulesUw2
	{
		/// <summary>The lever of eight positions (minor 2, index 1).</summary>
		public const int LeverId = 0x161;

		/// <summary>The switch picture (minor 2, index 2), stepped as the lever.</summary>
		public const int SwitchPictureId = 0x162;

		private const int SwitchClassMask = 0x1F0;

		private const int SwitchClassBase = 0x170;

		private const int FlagsSteps = 7;

		/// <summary>flip_switch's state that always flips (UseRect's minor 3).</summary>
		private const int AlwaysFlip = 3;

		/// <summary>A thing UseRect moves: a switch, the lever or the switch picture.</summary>
		public static bool IsWallControl(UWObject pOThing)
		{
			return pOThing != null && ((pOThing.ID & SwitchClassMask) == SwitchClassBase || pOThing.ID == LeverId || pOThing.ID == SwitchPictureId);
		}

		/// <summary>
		/// UseRect on a wall control, then UseObj's checkTrap(who, obj, 4) with the same host: a switch flips (flip_switch
		/// with 3, its click and its picture through IUWTrapHost.ToggleSwitchVisual), the lever and the switch picture take
		/// (flags + 1) &amp; 7 (the lever's eight pictures through IUWTrapHost.AdvanceLever, the record alone without one).
		/// False when the thing is none of them.
		/// </summary>
		public static bool UseWallControl(UWObject pOThing, IUWTrapHost pIHost)
		{
			if (!IsWallControl(pOThing) || pIHost == null)
				return false;

			if ((pOThing.ID & SwitchClassMask) == SwitchClassBase)
				UWTrapRulesUw2.FlipSwitch(pOThing, AlwaysFlip, pIHost);
			else
			{
				int liStepped = (pOThing.Flags + 1) & FlagsSteps;

				pIHost.AdvanceLever(pOThing);

				if (pOThing.Flags != liStepped)
					pOThing.Flags = (ushort)liStepped;

				UWTrapLog.Detail = string.Format("-> 0x{0:X} flags {1}", pOThing.ID, liStepped);
			}

			UWTrapRulesUw2.CheckTrap(pOThing, UWTrapRulesUw2.UseMode, pOThing.TileX, pOThing.TileY, pIHost);

			return true;
		}
	}
}
