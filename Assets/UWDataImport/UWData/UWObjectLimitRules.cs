using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The object limit of a level, as the original keeps it while playing. READ 2026-09-25
	/// (GetFreeObject_seg027_446 and CullObjects_seg027_2861_329, whole):
	///
	/// a level holds 256 mobile slots (creatures; 0 and 1 are never handed out) and 768 static
	/// ones, the same tables in memory as in LEV.ARK. Every new object takes a slot from the free
	/// list of its kind. When that list is EMPTY, the original first culls: row by row from
	/// tile 0/0, on tiles whose distance from the player's tile (|dx| + |dy|) is above 10 minus
	/// the range, it runs the culling test with that range over every object of the tile chain
	/// and removes what fails - with RANGE 3 for both lists (tiles more than 7 away), up to FIVE
	/// objects for a mobile slot and TEN for a static one. If the list is still empty then, the
	/// new object is not made.
	///
	/// CORRECTED BY THE ORIGINAL 2026-09-25 (per user, SAVE4 level 7 filled to 768 static slots,
	/// one sack thrown, saved to SAVE3): the first reading had the two arguments of CullObjects
	/// the wrong way round (range 5 or 10, three at most). What the original did: it emptied the
	/// apple tile 50/2 (distance 8) and left 51/2 (distance 7) alone, took 63 objects of
	/// priority 4 and 5 all over the map, and none of priority 6 or more - of the 70 candidates
	/// of priority up to 5, 7 stayed, as the second roll lets some through. So the range is 3
	/// plus the roll of 0 to 2 and NOT shifted by one as the water's base of 11 is (see
	/// UWLiquidCulling - that shift must come from something else). NOT UNDERSTOOD: 63 objects
	/// means seven or more calls for a throw that put only two objects into the level (the sack
	/// and the wand in it).
	///
	/// OURS keeps no free lists while playing, so the slots are counted: every object reachable
	/// from the tile lists (with the contents of containers and creatures) holds one - a loaded
	/// object the slot its place in the object list gives it, a new one a mobile slot if it is a
	/// creature and a static one otherwise, as UWLevelWriter hands them out. One difference is
	/// known: what flies (a missile, a throw in the air) is not in our tile lists and so holds no
	/// slot, while the original's holds a mobile one.
	///
	/// Until 2026-09-25 ours had no limit while playing, and the writer silently dropped whatever
	/// found no slot when saving - the newest objects, not the far ones.
	/// </summary>
	public static class UWObjectLimitRules
	{
		/// <summary>Slots 2 to 255.</summary>
		public const int MobileSlots = 254;

		/// <summary>Slots 256 to 1023.</summary>
		public const int StaticSlots = 768;

		public const int FirstMobileSlot = 2;

		public const int FirstStaticSlot = 256;

		public const int SlotCount = 1024;

		/// <summary>The range both lists cull with (GetFreeObject pushes 3 as the first
		/// argument).</summary>
		public const int CullRange = 3;

		/// <summary>How many objects one call culls at most, for a mobile and a static slot.</summary>
		public const int MobileCullLimit = 5;

		public const int StaticCullLimit = 10;

		/// <summary>A tile qualifies when its distance is above this minus the range.</summary>
		private const int CullDistanceBase = 10;

		private const int MaxDepth = 16;

		/// <summary>Whether a NEW object of this kind needs a mobile slot.</summary>
		public static bool NeedsMobileSlot(UWObject pOObject)
		{
			return pOObject != null && pOObject.ID >= UWObjectMechanics.FirstCritterObjectId
				&& pOObject.ID <= UWObjectMechanics.LastCritterObjectId;
		}

		/// <summary>Counts the slots in use - see the class comment.</summary>
		public static void CountUsage(UWLevel pOLevel, out int piMobile, out int piStatic)
		{
			piMobile = 0;
			piStatic = 0;

			if (pOLevel == null || pOLevel.TileData == null)
				return;

			Dictionary<UWObject, int> lOSlots = new Dictionary<UWObject, int>();
			List<UWObject> lOMaster = pOLevel.Masterlist;

			for (int liAt = 0; lOMaster != null && liAt < SlotCount && liAt < lOMaster.Count; liAt++)
			{
				if (lOMaster[liAt] != null && !lOSlots.ContainsKey(lOMaster[liAt]))
					lOSlots[lOMaster[liAt]] = liAt;
			}

			HashSet<UWObject> lOSeen = new HashSet<UWObject>();

			foreach (UWTile lOTile in pOLevel.TileData)
			{
				if (lOTile == null || lOTile.ObjectsInTile == null)
					continue;

				foreach (UWObject lOObject in lOTile.ObjectsInTile)
					fCount(lOObject, lOMaster, lOSlots, lOSeen, 0, ref piMobile, ref piStatic);
			}
		}

		private static void fCount(UWObject pOObject, List<UWObject> pOMaster, Dictionary<UWObject, int> pOSlots,
			HashSet<UWObject> pOSeen, int piDepth, ref int piMobile, ref int piStatic)
		{
			if (pOObject == null || piDepth > MaxDepth || !pOSeen.Add(pOObject))
				return;

			int liSlot;
			bool lbMobile = pOSlots.TryGetValue(pOObject, out liSlot) && liSlot >= FirstMobileSlot
				? liSlot < FirstStaticSlot
				: NeedsMobileSlot(pOObject);

			if (lbMobile)
				piMobile++;
			else
				piStatic++;

			foreach (UWObject lOChild in fChildren(pOObject, pOMaster))
				fCount(lOChild, pOMaster, pOSlots, pOSeen, piDepth + 1, ref piMobile, ref piStatic);
		}

		/// <summary>What hangs below an object: its resolved contents, or else the chain its
		/// link starts in the level's object list.</summary>
		private static IEnumerable<UWObject> fChildren(UWObject pOObject, List<UWObject> pOMaster)
		{
			if (pOObject.Contents != null)
			{
				foreach (UWObject lOChild in pOObject.Contents)
					yield return lOChild;

				yield break;
			}

			if (pOObject.HasQuantity || pOObject.Quantity == 0 || pOMaster == null)
				yield break;

			int liAt = pOObject.Quantity;

			for (int liStep = 0; liStep < SlotCount && liAt > 0 && liAt < pOMaster.Count; liStep++)
			{
				UWObject lOChild = pOMaster[liAt];

				if (lOChild == null)
					yield break;

				yield return lOChild;

				liAt = lOChild.Link;
			}
		}

		/// <summary>Whether a slot of this kind is free. piInventoryObjects: the player's
		/// inventory, which the original keeps in the current level's static slots (see
		/// MakeRoomForInventory).</summary>
		public static bool HasFreeSlot(UWLevel pOLevel, bool pbMobile, int piInventoryObjects = 0)
		{
			int liMobile;
			int liStatic;

			CountUsage(pOLevel, out liMobile, out liStatic);

			return pbMobile ? liMobile < MobileSlots : liStatic + piInventoryObjects < StaticSlots;
		}

		/// <summary>How many objects the inventory holds: everything given, with the contents of
		/// containers.</summary>
		public static int CountObjects(IEnumerable<UWObject> pORoots)
		{
			HashSet<UWObject> lOSeen = new HashSet<UWObject>();
			int liCount = 0;

			if (pORoots != null)
				foreach (UWObject lORoot in pORoots)
					liCount += fCountTree(lORoot, lOSeen, 0);

			return liCount;
		}

		private static int fCountTree(UWObject pOObject, HashSet<UWObject> pOSeen, int piDepth)
		{
			if (pOObject == null || piDepth > MaxDepth || !pOSeen.Add(pOObject))
				return 0;

			int liCount = 1;

			if (pOObject.Contents != null)
				foreach (UWObject lOChild in pOObject.Contents)
					liCount += fCountTree(lOChild, pOSeen, piDepth + 1);

			return liCount;
		}

		/// <summary>
		/// THE INVENTORY ARRIVES IN THE LEVEL. Found by the user's control test on 2026-09-25
		/// (SAVE4 loaded and saved at once, nothing thrown): 64 objects went - as many as the
		/// inventory the original wrote back holds. The original keeps the player's inventory in
		/// the object lists of the level he is on, so loading a game or entering a level asks
		/// GetFreeObject for a static slot per carried object, and with the list empty every
		/// such request culls (range 3, up to ten). Saving writes the inventory back to
		/// PLAYER.DAT and frees those slots again, which is why the saved level showed 64 free.
		///
		/// Ours keeps the inventory apart, so it counts it against the static slots (HasFreeSlot)
		/// and, on arrival, culls in the same steps of ten until level and inventory fit or
		/// nothing more goes. Returns how many objects went.
		/// </summary>
		public static int MakeRoomForInventory(UWLevel pOLevel, int piPlayerX, int piPlayerY, int piInventoryObjects,
			Func<UWObject, bool> pbCulls, Action<UWObject, int, int> poRemove)
		{
			int liMobile;
			int liStatic;

			CountUsage(pOLevel, out liMobile, out liStatic);

			int liMissing = liStatic + piInventoryObjects - StaticSlots;
			int liCulled = 0;

			while (liMissing > 0)
			{
				int liNow = Cull(pOLevel, piPlayerX, piPlayerY, CullRange, StaticCullLimit, pbCulls, poRemove);

				if (liNow == 0)
					break;

				liCulled += liNow;
				liMissing -= liNow;
			}

			return liCulled;
		}

		/// <summary>Whether CullObjects looks at this tile at all.</summary>
		public static bool TileQualifies(int piTileX, int piTileY, int piPlayerX, int piPlayerY, int piRange)
		{
			return Math.Abs(piPlayerY - piTileY) + Math.Abs(piPlayerX - piTileX) > CullDistanceBase - piRange;
		}

		/// <summary>
		/// CullObjects: row by row, the tiles that qualify, each object of the tile list that
		/// pbCulls condemns goes through poRemove (object, tile x, tile y), until piLimit went.
		/// Returns how many went.
		/// </summary>
		public static int Cull(UWLevel pOLevel, int piPlayerX, int piPlayerY, int piRange, int piLimit,
			Func<UWObject, bool> pbCulls, Action<UWObject, int, int> poRemove)
		{
			int liRemoved = 0;

			if (pOLevel == null || pOLevel.TileData == null || pbCulls == null || poRemove == null)
				return 0;

			for (int liY = 0; liY < UWWorldScale.TilesPerAxis; liY++)
			{
				for (int liX = 0; liX < UWWorldScale.TilesPerAxis; liX++)
				{
					if (!TileQualifies(liX, liY, piPlayerX, piPlayerY, piRange))
						continue;

					UWTile lOTile = pOLevel.GetTile(liX, liY);

					if (lOTile == null || lOTile.ObjectsInTile == null || lOTile.ObjectsInTile.Count == 0)
						continue;

					foreach (UWObject lOObject in lOTile.ObjectsInTile.ToArray())
					{
						if (lOObject == null || !pbCulls(lOObject))
							continue;

						poRemove(lOObject, liX, liY);

						if (++liRemoved >= piLimit)
							return liRemoved;
					}
				}
			}

			return liRemoved;
		}

		/// <summary>
		/// The culling test of ObjectCulling with a given range argument, over the object and
		/// what hangs below it (a container or creature with something valuable stays): the
		/// range is rolled once - the argument plus 0 to 2, as read and as the original's culling
		/// of 2026-09-25 bears out (see the class comment); an object with word 0 bit 13 set is
		/// never culled; and if the value test condemns it, the routine's second roll (0 to 9)
		/// still saves it when it reaches the range.
		/// </summary>
		public static bool Culls(UWObject pOObject, int piRangeArgument, UWCommonObjectProperties pOProperties,
			List<UWObject> pOMaster)
		{
			if (pOObject == null || pOProperties == null)
				return false;

			int liRange = piRangeArgument + UWRandom.Next(UWLiquidCulling.RangeDice);

			if (!fFailsValueTest(pOObject, liRange, pOProperties, pOMaster, 0))
				return false;

			return UWRandom.Next(UWLiquidCulling.SecondChanceDice) < liRange;
		}

		private static bool fFailsValueTest(UWObject pOObject, int piRange, UWCommonObjectProperties pOProperties,
			List<UWObject> pOMaster, int piDepth)
		{
			if (pOObject.DoorDirection || piDepth > MaxDepth)
				return false;

			UWCommonObjectProperties.Entry lOEntry;

			if (!pOProperties.TryGet(pOObject.ID, out lOEntry))
				return false;

			int liCount = pOObject.HasQuantity && pOObject.Quantity < UWObjectMechanics.SpecialPropertyThreshold
				? pOObject.Quantity : 1;

			if (!UWLiquidCulling.Swallows(lOEntry.CullingPriority, liCount, piRange))
				return false;

			// ONE LEVEL DOWN only: ObjectCulling runs the test over the object's own chain
			// (RunCodeOnObjectChain_seg027_117), and the test itself does not descend - so
			// something valuable in a bag inside a bag does not save the outer one (read whole
			// 2026-09-25). There is NO exception for creatures: they go by their COMOBJ priority
			// like anything else, and their inventory can save them.
			if (piDepth > 0)
				return true;

			foreach (UWObject lOChild in fChildren(pOObject, pOMaster))
			{
				if (lOChild != null && !fFailsValueTest(lOChild, piRange, pOProperties, pOMaster, piDepth + 1))
					return false;
			}

			return true;
		}
	}
}
