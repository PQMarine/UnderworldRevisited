using System;
using System.Collections.Generic;
using System.Linq;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Writes a level back into its LEV.ARK block - tiles, object lists, free lists.
	///
	/// THE REFERENCE DOES THIS DIFFERENTLY: it keeps the raw block bytes in memory and changes
	/// them directly during play, its writer only copies. We break them up into UWTile and
	/// UWObject on load. That is why the block is REBUILT from the model here - on top of the
	/// original block: only what the model knows is overwritten, everything else stays byte
	/// for byte. A level in which nothing has changed therefore comes out byte-identical.
	/// That is exactly what the round-trip test checks.
	///
	/// THE LAYOUT OF A BLOCK is UWLevArkLayout's (0x7C08 bytes: tiles, mobile and static
	/// objects, the two free lists, the active mobile list and the three counts); the records
	/// are written by UWObjectRecordCodec and UWNpcRecordCodec, the lists by UWLevArkCodec
	/// (cleanup phase 2, step "Codecs", 2026-10-12 - until then all of it stood here a second
	/// time next to UWLevel's copy).
	///
	/// WHO LIVES AND WHO DOES NOT: what counts is what is reachable from the tiles - via the
	/// chain (Link) and via the special link in the quantity field, as long as an object
	/// carries no quantity. This is done twice: once on the original bytes, once on the
	/// model. What was reachable only in the original has been removed (picked up, slain);
	/// what exists only in the model and has no number is new (put down, conjured,
	/// dropped). The free list itself is NOT recalculated from reachability - that would
	/// lose slots the original occupies for reasons we do not know. The list is only
	/// extended by what was removed and shortened by what is new, both at the TOP, just as
	/// the original itself releases and allocates.
	///
	/// THE MODEL'S OBJECTS STAY UNCHANGED - only the bytes get the new references. Sole
	/// exception: after writing, a new object is placed into the Masterlist at its number,
	/// so the next save recognises it and does not move it again.
	///
	/// THE ORDER OF THE WORK is the order of the collected list (lOOrder: tile by tile, each
	/// object followed by its contents), not a dictionary's enumeration (since 2026-10-12).
	/// It decides which new creature is appended to the active list first, and a dictionary
	/// only happened to keep its insertion order.
	///
	/// THE THINGS IN FLIGHT (UWLevel.InFlight, 2026-10-19) go back into their tiles' lists as the
	/// original keeps them: mobile records (a mobile slot, the nineteen bytes from the motion's
	/// record, UWMobileRecordCodec) on the active mobile list, at their place in the list (the head
	/// once they changed tiles). The original and ours load them as flights again. Until then a
	/// flight was in no list and so not in the save at all.
	/// </summary>
	public static class UWLevelWriter
	{
		/// <summary>What could not be written during the last call - for instance because the
		/// free list was empty. Empty means: everything is in. The session's diagnostics since
		/// 2026-10-14 (UWSessionDiagnostics.WriterWarnings, cleanup step 2.7).</summary>
		public static List<string> Warnings
		{
			get { return UWGameSession.Current.Diagnostics.WriterWarnings; }
		}

		/// <summary>The number every object of the last built block got (step 3), for the
		/// animation list that follows the level (DataImport.SaveMapData).</summary>
		public static Dictionary<UWObject, int> LastIndices { get; private set; }

		public static byte[] BuildBlock(UWLevel pOLevel, byte[] pyOriginal)
		{
			byte[] lyOut = (byte[])pyOriginal.Clone();

			if (pOLevel == null || pOLevel.TileData == null || pOLevel.Masterlist == null
				|| pyOriginal.Length < UWLevArkLayout.BlockSize)
				return lyOut;

			List<UWObject> lOMaster = pOLevel.Masterlist;

			// --- 0. The tiles' lists as they go into the file: each with its flights linked back in.
			Dictionary<UWObject, UWMobileRecord> lOFlights = new Dictionary<UWObject, UWMobileRecord>();
			List<UWObject>[] lOChains = fChainsWithFlights(pOLevel, lOFlights);

			// --- 1. Who sits where in the file - an object's number is its slot in the
			//        Masterlist, as long as it is below 1024.
			Dictionary<UWObject, int> lOOriginalIndex = new Dictionary<UWObject, int>();

			for (int liAt = 0; liAt < UWLevArkLayout.ObjectCount && liAt < lOMaster.Count; liAt++)
			{
				if (lOMaster[liAt] != null && !lOOriginalIndex.ContainsKey(lOMaster[liAt]))
					lOOriginalIndex[lOMaster[liAt]] = liAt;
			}

			HashSet<int> lOReachOriginal = fReachFromBytes(pyOriginal);

			// --- 2. What is alive now, including chain order
			HashSet<UWObject> lOLive = new HashSet<UWObject>();
			Dictionary<UWObject, List<UWObject>> lOChildren = new Dictionary<UWObject, List<UWObject>>();
			HashSet<UWObject> lOUnresolved = new HashSet<UWObject>();
			List<UWObject> lOOrder = new List<UWObject>();

			for (int liTile = 0; liTile < UWLevArkLayout.TileCount && liTile < pOLevel.TileData.Length; liTile++)
			{
				if (lOChains[liTile] == null)
					continue;

				foreach (UWObject lOObject in lOChains[liTile])
					fCollect(lOObject, lOMaster, lOLive, lOChildren, lOUnresolved, lOOrder);
			}

			// --- 3. Numbers: whoever has one keeps it; whoever is new gets a slot from the
			//        top of the free list.
			List<int> lOMobileFree = UWLevArkCodec.ReadMobileFreeList(pyOriginal, 0);
			List<int> lOStaticFree = UWLevArkCodec.ReadStaticFreeList(pyOriginal, 0);

			Dictionary<UWObject, int> lOIndex = new Dictionary<UWObject, int>();
			HashSet<int> lOUsed = new HashSet<int>();

			foreach (UWObject lOObject in lOOrder)
			{
				int liIndex;

				// A flight keeps only a mobile number - the slot its record holds first; one that was a
				// lying object (knocked loose, a throw from the bag) gets a mobile slot, as
				// static_to_mob and missile_fire take one.
				UWMobileRecord lOFlight;

				if (lOFlights.TryGetValue(lOObject, out lOFlight) && lOFlight.Slot >= UWLevArkLayout.FirstFreeMobileSlot
					&& UWLevArkLayout.IsMobileSlot(lOFlight.Slot) && lOFlight.Slot < lOMaster.Count
					&& lOMaster[lOFlight.Slot] == lOObject && !lOUsed.Contains(lOFlight.Slot))
				{
					lOIndex[lOObject] = lOFlight.Slot;
					lOUsed.Add(lOFlight.Slot);
				}
				else if (lOOriginalIndex.TryGetValue(lOObject, out liIndex) && liIndex >= UWLevArkLayout.FirstFreeMobileSlot
					&& (lOFlight == null || UWLevArkLayout.IsMobileSlot(liIndex)))
				{
					lOIndex[lOObject] = liIndex;
					lOUsed.Add(liIndex);
				}
			}

			// Removed: was reachable in the original, is not any more. The slot goes onto the
			// top of the free list - before anything new is allocated, just as in the original.
			List<int> lORemoved = new List<int>();

			foreach (int liIndex in lOReachOriginal)
			{
				if (liIndex >= UWLevArkLayout.FirstFreeMobileSlot && !lOUsed.Contains(liIndex))
					lORemoved.Add(liIndex);
			}

			lORemoved.Sort();

			// Occupied slots have no business being on any free list.
			lOMobileFree.RemoveAll(liIndex => lOUsed.Contains(liIndex));
			lOStaticFree.RemoveAll(liIndex => lOUsed.Contains(liIndex));

			foreach (int liIndex in lORemoved)
			{
				List<int> lOList = UWLevArkLayout.IsMobileSlot(liIndex) ? lOMobileFree : lOStaticFree;

				if (!lOList.Contains(liIndex))
					lOList.Add(liIndex);
			}

			List<UWObject> lODropped = new List<UWObject>();

			foreach (UWObject lOObject in lOOrder)
			{
				if (lOIndex.ContainsKey(lOObject))
					continue;

				// Critters need a mobile slot with the extra bytes, and so does a thing in flight.
				bool lbCritter = UWObjectIds.IsCreature(lOObject.ID);
				bool lbFlight = lOFlights.ContainsKey(lOObject);
				List<int> lOList = lbCritter || lbFlight ? lOMobileFree : lOStaticFree;

				// An item may take a mobile slot if need be, but a critter may not take a
				// static one - it would lack the extra bytes. A flight without a mobile slot is
				// written as a lying object where it is.
				if (lOList.Count == 0 && !lbCritter && lOMobileFree.Count > 0)
					lOList = lOMobileFree;

				if (lOList.Count == 0 && lbFlight && lOStaticFree.Count > 0)
				{
					lOList = lOStaticFree;
					Warnings.Add(string.Format("Level {0}: no mobile slot for object {1} in flight, saved lying", pOLevel.LevelNumber, lOObject.ID));
				}

				if (lOList.Count == 0)
				{
					lODropped.Add(lOObject);
					Warnings.Add(string.Format("Level {0}: no free slot for object {1}", pOLevel.LevelNumber, lOObject.ID));
					continue;
				}

				int liIndex = lOList[lOList.Count - 1];

				// A CARRIED OBJECT KEEPS ITS NUMBER: in the original it already lives in that static
				// slot (UWCarriedSlots), and handing it over - to Shak for repair - does not move it.
				// A conversation may have kept the number (Shak's global 2, take_id_from_npc), so a
				// new one from the top of the free list lost the item after saving (2026-10-01).
				if (!lbCritter && !lbFlight && UWLevArkLayout.IsStaticSlot(lOObject.SlotIndex) && lOStaticFree.Contains(lOObject.SlotIndex))
				{
					liIndex = lOObject.SlotIndex;
					lOStaticFree.Remove(liIndex);
				}
				else
					lOList.RemoveAt(lOList.Count - 1);

				lOIndex[lOObject] = liIndex;
				lOUsed.Add(liIndex);
			}

			LastIndices = lOIndex;

			// --- 4. Chain successors: tile chains and child chains
			Dictionary<UWObject, int> lONext = new Dictionary<UWObject, int>();

			for (int liTile = 0; liTile < UWLevArkLayout.TileCount && liTile < pOLevel.TileData.Length; liTile++)
			{
				if (lOChains[liTile] != null)
					fLinkChain(lOChains[liTile], lOIndex, lONext);
			}

			foreach (KeyValuePair<UWObject, List<UWObject>> lOPair in lOChildren)
				fLinkChain(lOPair.Value, lOIndex, lONext);

			// --- 5. Write records, in the collected order
			foreach (UWObject lOObject in lOOrder)
			{
				int liIndex;

				if (!lOIndex.TryGetValue(lOObject, out liIndex))
					continue;

				int liOffset = UWLevArkLayout.ObjectOffset(liIndex);

				int liNext;

				if (!lONext.TryGetValue(lOObject, out liNext))
					liNext = 0;

				int liSpecial = lOObject.Quantity & UWObjectRecordCodec.QuantityMask;

				if (!lOObject.HasQuantity && !lOUnresolved.Contains(lOObject))
				{
					List<UWObject> lOKids;

					liSpecial = lOChildren.TryGetValue(lOObject, out lOKids) && lOKids.Count > 0
						&& lOIndex.ContainsKey(lOKids[0])
						? lOIndex[lOKids[0]]
						: 0;
				}

				UWObjectRecordCodec.Write(lyOut, liOffset, lOObject, lOObject.ID, liNext, liSpecial);

				UWMobileRecord lOFlight;

				if (lOFlights.TryGetValue(lOObject, out lOFlight))
				{
					UWBinary.Write16(lyOut, liOffset + 2, UWMobileRecordCodec.PositionWord(lOFlight));

					if (UWLevArkLayout.IsMobileSlot(liIndex))
					{
						byte[] lyMobile = UWMobileRecordCodec.Write(lOFlight, (lOObject as UWNpc)?.RawNpcBytes);

						System.Array.Copy(lyMobile, 0, lyOut, liOffset + UWObjectRecordCodec.RecordSize, lyMobile.Length);
					}
				}
				else if (UWLevArkLayout.IsMobileSlot(liIndex) && lOObject is UWNpc lONpc)
					UWNpcRecordCodec.Write(lyOut, liOffset + UWObjectRecordCodec.RecordSize, lONpc);
			}

			// A removed slot loses its object number - that is how the original releases
			// (ObjectFreeLists.ReleaseFreeObject sets item_id to zero).
			foreach (int liIndex in lORemoved)
			{
				if (lOUsed.Contains(liIndex))
					continue;

				UWObjectRecordCodec.ClearId(lyOut, UWLevArkLayout.ObjectOffset(liIndex));
			}

			// --- 6. Tiles
			for (int liTile = 0; liTile < UWLevArkLayout.TileCount && liTile < pOLevel.TileData.Length; liTile++)
			{
				UWTile lOTile = pOLevel.TileData[liTile];

				if (lOTile == null)
					continue;

				int liHead = 0;

				if (lOChains[liTile] != null)
				{
					foreach (UWObject lOObject in lOChains[liTile])
					{
						if (lOObject != null && lOIndex.ContainsKey(lOObject))
						{
							liHead = lOIndex[lOObject];
							break;
						}
					}
				}

				// The floor texture stays as it was in the file: at runtime it only changes
				// for display (maze navigation, see UWMazeNavigation).
				uint lyWord = lOTile.RawTileData;

				lyWord = (lyWord & ~0xFu) | ((uint)lOTile.TileType & 0xFu);
				lyWord = (lyWord & ~0xF0u) | ((uint)lOTile.FloorHeight & 0xF0u);
				lyWord = (lyWord & ((1u << UWLevArkLayout.TileFirstObjectShift) - 1)) | ((uint)liHead << UWLevArkLayout.TileFirstObjectShift);

				UWBinary.Write32(lyOut, liTile * UWLevArkLayout.TileRecordSize, (int)lyWord);
			}

			// --- 7. Free lists and active critters
			UWLevArkCodec.WriteMobileFreeList(lyOut, 0, lOMobileFree);
			UWLevArkCodec.WriteStaticFreeList(lyOut, 0, lOStaticFree);
			fWriteActiveMobiles(lyOut, pyOriginal, pOLevel, lOChains, lOFlights, lOOrder, lOIndex);

			// --- 8. New objects into the Masterlist at their number
			foreach (KeyValuePair<UWObject, int> lOPair in lOIndex)
			{
				int liCurrent;

				if (lOOriginalIndex.TryGetValue(lOPair.Key, out liCurrent) && liCurrent == lOPair.Value)
					continue;

				while (lOMaster.Count <= lOPair.Value)
					lOMaster.Add(null);

				lOMaster[lOPair.Value] = lOPair.Key;
			}

			return lyOut;
		}

		/// <summary>
		/// The tiles' lists with the level's flights linked back in (UWLevel.InFlight): each at its
		/// place (UWMobileRecord.ChainPosition, the head once it changed tiles), the lowest place first
		/// so that two flights of one tile come back in their order. A tile without flights gives its
		/// own list. pOFlights collects the flights by their objects.
		/// </summary>
		private static List<UWObject>[] fChainsWithFlights(UWLevel pOLevel, Dictionary<UWObject, UWMobileRecord> pOFlights)
		{
			List<UWObject>[] lOChains = new List<UWObject>[UWLevArkLayout.TileCount];
			bool[] lbCopied = new bool[UWLevArkLayout.TileCount];

			for (int liTile = 0; liTile < UWLevArkLayout.TileCount && liTile < pOLevel.TileData.Length; liTile++)
			{
				UWTile lOTile = pOLevel.TileData[liTile];

				lOChains[liTile] = lOTile != null ? lOTile.ObjectsInTile : null;
			}

			List<UWMobileRecord> lOFlights = new List<UWMobileRecord>();

			foreach (UWMobileRecord lORecord in pOLevel.InFlight)
			{
				if (lORecord != null && lORecord.Object != null && !pOFlights.ContainsKey(lORecord.Object))
				{
					pOFlights[lORecord.Object] = lORecord;
					lOFlights.Add(lORecord);
				}
			}

			// Stable: two flights at the same place keep the order of InFlight.
			foreach (UWMobileRecord lORecord in Enumerable.OrderBy(lOFlights, pORecord => pORecord.ChainPosition))
			{
				int liTile = ((lORecord.TileY & 0x3F) * UWWorldScale.TilesPerAxis) + (lORecord.TileX & 0x3F);

				if (liTile >= pOLevel.TileData.Length || pOLevel.TileData[liTile] == null
					|| (lOChains[liTile] != null && lOChains[liTile].Contains(lORecord.Object)))
				{
					pOFlights.Remove(lORecord.Object);

					continue;
				}

				if (!lbCopied[liTile])
				{
					lOChains[liTile] = lOChains[liTile] != null ? new List<UWObject>(lOChains[liTile]) : new List<UWObject>();
					lbCopied[liTile] = true;
				}

				List<UWObject> lOChain = lOChains[liTile];

				lOChain.Insert(Math.Max(0, Math.Min(lORecord.ChainPosition, lOChain.Count)), lORecord.Object);
			}

			return lOChains;
		}

		/// <summary>Adds an object and everything attached to it to the order.
		/// </summary>
		private static void fCollect(UWObject pOObject, List<UWObject> pOMaster, HashSet<UWObject> pOLive,
			Dictionary<UWObject, List<UWObject>> pOChildren, HashSet<UWObject> pOUnresolved, List<UWObject> pOOrder)
		{
			if (pOObject == null || !pOLive.Add(pOObject))
				return;

			pOOrder.Add(pOObject);

			if (pOObject.HasQuantity)
				return;

			List<UWObject> lOKids = new List<UWObject>();

			if (pOObject.Contents != null)
			{
				foreach (UWObject lOItem in pOObject.Contents)
				{
					if (lOItem != null)
						lOKids.Add(lOItem);
				}
			}
			else if (pOObject.Quantity != 0)
			{
				if (pOObject.Quantity >= pOMaster.Count || pOMaster[pOObject.Quantity] == null)
				{
					// Cannot be resolved - then the field stays as it was.
					pOUnresolved.Add(pOObject);
					return;
				}

				// In chain order - the bytes depend on it. At most ObjectCount objects, a circle
				// ends the walk (the same guard as the loop here before UWObjectChain, 2026-10-11).
				foreach (UWObject lOLinked in UWObjectChain.Walk(pOObject, pOMaster, UWLevArkLayout.ObjectCount))
					lOKids.Add(lOLinked);
			}

			if (lOKids.Count == 0)
				return;

			pOChildren[pOObject] = lOKids;

			foreach (UWObject lOKid in lOKids)
				fCollect(lOKid, pOMaster, pOLive, pOChildren, pOUnresolved, pOOrder);
		}

		private static void fLinkChain(List<UWObject> pOChain, Dictionary<UWObject, int> pOIndex, Dictionary<UWObject, int> pONext)
		{
			UWObject lOPrevious = null;

			foreach (UWObject lOObject in pOChain)
			{
				if (lOObject == null || !pOIndex.ContainsKey(lOObject))
					continue;

				// Whoever already has a place in a chain stays there.
				if (pONext.ContainsKey(lOObject))
					continue;

				if (lOPrevious != null)
					pONext[lOPrevious] = pOIndex[lOObject];

				pONext[lOObject] = 0;
				lOPrevious = lOObject;
			}
		}

		/// <summary>Reachable slots, directly from the original bytes - the same rules as
		/// fCollect: chain and, without the quantity bit, special link.</summary>
		private static HashSet<int> fReachFromBytes(byte[] pyBlock)
		{
			HashSet<int> lOReach = new HashSet<int>();
			Stack<int> lOStack = new Stack<int>();

			for (int liTile = 0; liTile < UWLevArkLayout.TileCount; liTile++)
			{
				int liHead = (int)(UWBinary.Read32(pyBlock, liTile * UWLevArkLayout.TileRecordSize) >> UWLevArkLayout.TileFirstObjectShift);

				if (liHead != 0)
					lOStack.Push(liHead);
			}

			while (lOStack.Count > 0)
			{
				int liIndex = lOStack.Pop();

				if (liIndex <= 0 || liIndex >= UWLevArkLayout.ObjectCount || !lOReach.Add(liIndex))
					continue;

				int liOffset = UWLevArkLayout.ObjectOffset(liIndex);
				int liNext = UWObjectRecordCodec.ReadLink(pyBlock, liOffset);
				int liSpecial = UWObjectRecordCodec.ReadQuantityOrLink(pyBlock, liOffset);

				if (liNext != 0)
					lOStack.Push(liNext);

				if (!UWObjectRecordCodec.ReadIsQuantity(pyBlock, liOffset) && liSpecial != 0)
					lOStack.Push(liSpecial);
			}

			return lOReach;
		}

		/// <summary>
		/// The list of active critters - the ones the original's critter loop moves.
		///
		/// WHAT BELONGS ON IT: Obj_Alloc (OBJECTS.C) appends every mobile it hands out
		/// (active_critter) and Obj_Free takes it off (free_critter, the last entry moving into
		/// the hole). In the original's own data that comes to exactly the creatures standing in
		/// a tile, all nine levels of the game's LEV.ARK and every level of SAVE4_ORIGINAL
		/// (counted 2026-10-12); a creature hanging in a trap as its template is not on it.
		///
		/// So the list keeps its order, loses every entry that no longer holds a creature
		/// standing in a tile, and gains, at the end in the collected order, every such creature
		/// it lacks. Until 2026-10-12 it was rebuilt from the old bytes plus what this writer
		/// itself allocated - but a creature made during play (a summoning, a creation trap)
		/// takes its slot then (UWLevel.TryPlaceInFreeMobileSlot), so it was left off, and a
		/// goblin summoned in ours stood frozen in the original and could not be pushed (per
		/// user on SAVE4). Saving such a save again now repairs it.
		///
		/// A THING IN FLIGHT belongs on it as well (2026-10-19): Obj_Alloc(1) appended it at the
		/// launch (UnderworldExhumed obj/OBJECTS.C:236), and move_mobile steps it from there
		/// (critter/AI.C:1539-1567).
		/// </summary>
		private static void fWriteActiveMobiles(byte[] pyOut, byte[] pyOriginal, UWLevel pOLevel, List<UWObject>[] pOChains,
			Dictionary<UWObject, UWMobileRecord> pOFlights, List<UWObject> pOOrder, Dictionary<UWObject, int> pOIndex)
		{
			HashSet<int> lOStanding = new HashSet<int>();
			List<int> lOStandingInOrder = new List<int>();
			HashSet<UWObject> lOInTiles = new HashSet<UWObject>();

			foreach (List<UWObject> lOChain in pOChains)
			{
				if (lOChain != null)
					lOInTiles.UnionWith(lOChain);
			}

			foreach (UWObject lOObject in pOOrder)
			{
				int liIndex;

				if (!lOInTiles.Contains(lOObject) || !(UWObjectIds.IsCreature(lOObject.ID) || pOFlights.ContainsKey(lOObject))
					|| !pOIndex.TryGetValue(lOObject, out liIndex) || !UWLevArkLayout.IsMobileSlot(liIndex))
					continue;

				if (lOStanding.Add(liIndex))
					lOStandingInOrder.Add(liIndex);
			}

			List<int> lOList = pOLevel.ActiveMobiles != null
				? new List<int>(pOLevel.ActiveMobiles)
				: UWLevArkCodec.ReadActiveMobiles(pyOriginal, 0);

			foreach (int liListed in new List<int>(lOList))
			{
				if (!lOStanding.Contains(liListed))
					UWLevArkCodec.RemoveActiveMobile(lOList, liListed);
			}

			foreach (int liIndex in lOStandingInOrder)
				UWLevArkCodec.AddActiveMobile(lOList, liIndex);

			UWLevArkCodec.WriteActiveMobiles(pyOut, 0, lOList);
		}
	}
}
