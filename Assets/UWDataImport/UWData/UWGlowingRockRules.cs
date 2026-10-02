namespace UWDataImport.UWData
{
	/// <summary>
	/// The glowing rocks (0x129, "a glowing rock"; the disassembly calls them zanium). READ
	/// 2026-09-30 in UW.EXE after the user's report: in the original one gathers them by walking
	/// over them, in ours the player bumped into them.
	///
	/// NOT SOLID: COMOBJ.DAT gives the rock height 4, but byte 6 bit 0 - "solid", which the
	/// collision reads for the struck object (motion_collisions: without it the mover passes) - is
	/// clear. It is the only object with a height that is not solid (the move trigger aside).
	///
	/// TOUCHED, IT USES ITSELF on whoever walks into it: byte 6 bit 1 makes the collision call
	/// ObjectUse(rock, mover) (UWCommonObjectProperties.UsedWhenThrown - the same bit carries the
	/// missiles' hit), and Class_4_2_ObjectUsage_seg040_352B_16C4 runs, for a rock lying in the
	/// world, the branch GlowingRock_seg040_352B_186E:
	///
	///   - only for the player as the one who touches it;
	///   - the rock that takes it in: the one at the pointer (ObjectInHand), else the first
	///     carried one - FindObjectInInventory 4/2/9 over the equipment and backpack slots
	///     (mode 2), then inside the containers (mode 4);
	///   - none carried: nothing happens, and the player walks on through it;
	///   - else the touched rock's quantity (word +6 bits 6-15) is added to the carried one's,
	///     masked to ten bits, the slot is redrawn and the touched rock is cleared from the world.
	///
	/// So the FIRST rock is picked up by hand like anything else; after that the others join it
	/// by walking over them. The rock weighs nothing (COMOBJ mass 0), so the carried weight does
	/// not change.
	/// </summary>
	public static class UWGlowingRockRules
	{
		public const int GlowingRockId = 0x129;

		/// <summary>The ten bits of the quantity field.</summary>
		private const int QuantityMask = 0x3FF;

		/// <summary>An object that is gathered by walking over it.</summary>
		public static bool IsGatheredByTouch(int piObjectId)
		{
			return piObjectId == GlowingRockId;
		}

		/// <summary>The carried rock a touched one joins: at the pointer first, then in the slots,
		/// then inside the containers (UWInventoryModel.FindCarried keeps that order).</summary>
		public static UWObject FindCarriedRock(UWInventoryModel pOInventory)
		{
			if (pOInventory == null)
				return null;

			if (pOInventory.CursorItem != null && pOInventory.CursorItem.ID == GlowingRockId)
				return pOInventory.CursorItem;

			return pOInventory.FindCarried(pOItem => pOItem.ID == GlowingRockId);
		}

		/// <summary>
		/// The player touched a rock lying in the world: true when a carried rock took it in -
		/// its quantity is added, the touched rock's static slot goes back to the level's free
		/// list, and the caller takes it out of the world. False when none is carried.
		/// </summary>
		public static bool TryGather(UWObject pOTouched, UWInventoryModel pOInventory, UWLevel pOLevel)
		{
			if (pOTouched == null || pOTouched.ID != GlowingRockId)
				return false;

			UWObject lOCarried = FindCarriedRock(pOInventory);

			if (lOCarried == null || lOCarried == pOTouched)
				return false;

			lOCarried.Quantity = (ushort)((lOCarried.Quantity + pOTouched.Quantity) & QuantityMask);

			pOLevel?.ReturnStaticSlot(pOTouched.SlotIndex);

			return true;
		}
	}
}
