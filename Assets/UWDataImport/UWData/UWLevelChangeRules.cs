using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PLAYER'S THINGS ON A LEVEL CHANGE - one order for loading a game (UWLevelLoader.Start)
	/// and for walking into another level (UWLevelLoader.TravelTo), decision 9 of
	/// Docs/CLEANUP-PLAN.md (2026-10-12).
	///
	/// The original keeps the inventory in the static records of the level the player is on
	/// (UWCarriedSlots). Its GetLevel (GAMEWRAP.C) runs SavePlayerInv (the inventory into the
	/// workspace), Map_Load (the level with its own free lists), then RestorePlayerInv
	/// (INVSAVE.C), whose InvRestoreNexts takes one static record per carried object with
	/// Obj_Alloc (OBJECTS.C) - and Obj_Alloc garbage collects (range 3, ten objects) only when
	/// the free stack is empty. RestoreGame reaches the same order through GetLevel. So: the
	/// level first, then the slots, then room only where the slots did not suffice.
	///
	/// Ours counts the slots (UWObjectLimitRules): they do not suffice when the level's objects
	/// and the inventory together exceed the static slots, and MakeRoomForInventory culls exactly
	/// what is missing, nothing when nothing is. The numbers come from the in-memory free list
	/// (UWCarriedSlots.TakeSlots), which does not follow the objects placed while playing, so it
	/// does not decide whether to cull.
	///
	/// Leaving is the mirror: SaveLevel frees the inventory's records (FreePlayerInv; ours
	/// UWCarriedSlots.ReturnSlots, ClearObjectList_ovr118_4BE), after the running effects gave
	/// back theirs - they took them last.
	///
	/// Until 2026-10-12 Start made room before taking the slots and TravelTo took them first, and
	/// TravelTo culled around wherever the player's body stood after placing it.
	/// </summary>
	public static class UWLevelChangeRules
	{
		/// <summary>Out of the level being left: the effects' slots, then the carried objects'
		/// (pOCarriedChain may be null - a jump within the level keeps them).</summary>
		public static void Leave(UWWorldEdit pOEdit, IList<UWObject> pOCarriedChain)
		{
			if (pOEdit == null)
				return;

			pOEdit.ReleaseAllEffectSlots();

			if (pOCarriedChain != null)
				UWCarriedSlots.ReturnSlots(pOEdit.Level, pOCarriedChain);
		}

		/// <summary>
		/// Into the level just built: the carried objects take their numbers in the order of their
		/// chain (UWCarriedSlots.TakeSlots), then, if level and inventory do not fit the static
		/// slots, far objects around the player's tile are culled in steps of ten
		/// (UWObjectLimitRules.MakeRoomForInventory). piCarriedObjects counts what holds a slot,
		/// contents included. Returns how many objects were culled.
		/// </summary>
		public static int Arrive(UWWorldEdit pOEdit, IList<UWObject> pOCarriedChain, int piCarriedObjects,
			int piPlayerTileX, int piPlayerTileY)
		{
			if (pOEdit == null || pOEdit.Level == null)
				return 0;

			UWCarriedSlots.TakeSlots(pOEdit.Level, pOCarriedChain);

			return pOEdit.MakeRoomForInventory(piCarriedObjects, piPlayerTileX, piPlayerTileY);
		}
	}
}
