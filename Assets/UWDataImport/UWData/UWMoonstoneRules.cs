namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE THE MOONSTONE IS, for Gate Travel: PLAYER.DAT 0x5E, lower nibble, the dungeon level
	/// (1-based) the stone lies on, 0 while nobody knows - in the pack, or never put down.
	/// Read 2026-09-24 after the marker audit found ours reading it only from the loaded save
	/// (Todo.md section 0b, row 2).
	///
	/// THE ORIGINAL KEEPS IT UP TO DATE IN TWO PLACES, both through
	/// CheckIfItemID_Or_ContainsItemID_seg027_C36 with the stone's id, so a container holding it
	/// counts as well:
	///   - putting the object in hand into the world, dropped or thrown
	///     (the pointer handler of the inventory, in the case after
	///     DropOrThrowByPlayer_seg025_355 succeeded): the current dungeon level;
	///   - taking an object from the world (Pickup_seg024_A9E): 0.
	/// A new character starts with 2, where the stone lies (the character setup writes it).
	/// Gate Travel reads it (GateTravel_seg038_1759): 0 gives "The moonstone is not available.",
	/// anything else teleports to that level and searches the level for the stone, into
	/// containers as well (FindTeleportToObject_ovr143_14AD).
	///
	/// Until 2026-09-24 ours read the byte from the save at load and wrote a searched level at
	/// saving, so a stone put down elsewhere read "not available" until save and reload.
	/// </summary>
	public static class UWMoonstoneRules
	{
		public const int MoonstoneId = UWObjectMechanics.MoonstoneObjectId;

		/// <summary>How deep containers are searched - a guard against a looping chain.</summary>
		private const int MaxContainerDepth = 8;

		/// <summary>The object in hand went into the world, dropped or thrown, on this dungeon
		/// level (1-based).</summary>
		public static void OnPutIntoWorld(UWObject pOObject, int piDungeonLevel)
		{
			if (IsOrContainsMoonstone(pOObject))
				UWGameFlags.MoonstoneLevel = piDungeonLevel & 0xF;
		}

		/// <summary>An object was taken from the world onto the pointer or into the pack.</summary>
		public static void OnTakenFromWorld(UWObject pOObject)
		{
			if (IsOrContainsMoonstone(pOObject))
				UWGameFlags.MoonstoneLevel = 0;
		}

		/// <summary>The stone itself, or a container with the stone somewhere inside.</summary>
		public static bool IsOrContainsMoonstone(UWObject pOObject)
		{
			return fIsOrContains(pOObject, 0);
		}

		private static bool fIsOrContains(UWObject pOObject, int piDepth)
		{
			if (pOObject == null)
				return false;

			if (pOObject.ID == MoonstoneId)
				return true;

			if (pOObject.Contents == null || piDepth >= MaxContainerDepth)
				return false;

			foreach (UWObject lOItem in pOObject.Contents)
			{
				if (fIsOrContains(lOItem, piDepth + 1))
					return true;
			}

			return false;
		}

		/// <summary>
		/// The object in a tile list that is the stone or holds it - where Gate Travel lands.
		/// Containers are loaded from the level's master list when their contents are not known
		/// yet. Null when the level has none.
		/// </summary>
		public static UWObject FindHolderInLevel(UWLevel pOLevel)
		{
			if (pOLevel == null || pOLevel.TileData == null)
				return null;

			foreach (UWTile lOTile in pOLevel.TileData)
			{
				if (lOTile == null || lOTile.ObjectsInTile == null)
					continue;

				foreach (UWObject lOObject in lOTile.ObjectsInTile)
				{
					if (lOObject == null)
						continue;

					if (lOObject.Contents == null && pOLevel.Masterlist != null
						&& lOObject.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
						lOObject.EnsureContentsLoaded(pOLevel.Masterlist);

					if (IsOrContainsMoonstone(lOObject))
						return lOObject;
				}
			}

			return null;
		}
	}
}
