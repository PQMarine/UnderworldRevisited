using System.Collections.Generic;

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
	/// THE LAYOUT OF A BLOCK (0x7C08 bytes, reference: tilemap.cs and ObjectFreeLists):
	///
	///   0x0000  64 x 64 tiles, four bytes each
	///   0x4000  256 mobile objects, 27 bytes each (8 like every object, then 19 extra)
	///   0x5B00  768 static objects, 8 bytes each
	///   0x7300  free list of the mobile slots, 16 bits per entry
	///   0x74FC  free list of the static slots, 16 bits per entry
	///   0x7AFC  list of the active mobile objects, one byte per entry
	///   0x7C00  number of active mobile objects
	///   0x7C02  topmost used entry of the mobile free list (-1 means empty)
	///   0x7C04  the same for the static free list
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
	/// </summary>
	public static class UWLevelWriter
	{
		public const int BlockSize = 0x7C08;

		private const int TileCount = 64 * 64;

		private const int MobileBase = 0x4000;

		private const int MobileRecordSize = 27;

		private const int MobileCount = 256;

		private const int StaticBase = 0x5B00;

		private const int StaticRecordSize = 8;

		private const int ObjectCount = 1024;

		private const int MobileFreeBase = 0x7300;

		private const int StaticFreeBase = 0x74FC;

		private const int ActiveMobileBase = 0x7AFC;

		private const int ActiveMobileCountOffset = 0x7C00;

		private const int MobileFreePtrOffset = 0x7C02;

		private const int StaticFreePtrOffset = 0x7C04;

		/// <summary>Slot 0 is "no object", slot 1 belongs to the player character. Neither is
		/// ever allocated or released.</summary>
		private const int FirstFreeableIndex = 2;

		/// <summary>Critters are objects 64 to 127 - they need a mobile slot with the extra
		/// bytes.</summary>
		private const int FirstCritterId = UWObjectMechanics.FirstCritterObjectId;

		private const int LastCritterId = UWObjectMechanics.LastCritterObjectId;

		/// <summary>What could not be written during the last call - for instance because the
		/// free list was empty. Empty means: everything is in.</summary>
		public static readonly List<string> Warnings = new List<string>();

		public static byte[] BuildBlock(UWLevel pOLevel, byte[] pyOriginal)
		{
			byte[] lyOut = (byte[])pyOriginal.Clone();

			if (pOLevel == null || pOLevel.TileData == null || pOLevel.Masterlist == null
				|| pyOriginal.Length < BlockSize)
				return lyOut;

			List<UWObject> lOMaster = pOLevel.Masterlist;

			// --- 1. Who sits where in the file - an object's number is its slot in the
			//        Masterlist, as long as it is below 1024.
			Dictionary<UWObject, int> lOOriginalIndex = new Dictionary<UWObject, int>();

			for (int liAt = 0; liAt < ObjectCount && liAt < lOMaster.Count; liAt++)
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

			for (int liTile = 0; liTile < TileCount && liTile < pOLevel.TileData.Length; liTile++)
			{
				UWTile lOTile = pOLevel.TileData[liTile];

				if (lOTile == null || lOTile.ObjectsInTile == null)
					continue;

				foreach (UWObject lOObject in lOTile.ObjectsInTile)
					fCollect(lOObject, lOMaster, lOLive, lOChildren, lOUnresolved, lOOrder);
			}

			// --- 3. Numbers: whoever has one keeps it; whoever is new gets a slot from the
			//        top of the free list.
			List<int> lOMobileFree = fReadFreeList(pyOriginal, MobileFreeBase, MobileFreePtrOffset);
			List<int> lOStaticFree = fReadFreeList(pyOriginal, StaticFreeBase, StaticFreePtrOffset);

			Dictionary<UWObject, int> lOIndex = new Dictionary<UWObject, int>();
			HashSet<int> lOUsed = new HashSet<int>();

			foreach (UWObject lOObject in lOOrder)
			{
				int liIndex;

				if (lOOriginalIndex.TryGetValue(lOObject, out liIndex) && liIndex >= FirstFreeableIndex)
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
				if (liIndex >= FirstFreeableIndex && !lOUsed.Contains(liIndex))
					lORemoved.Add(liIndex);
			}

			lORemoved.Sort();

			// Occupied slots have no business being on any free list.
			lOMobileFree.RemoveAll(liIndex => lOUsed.Contains(liIndex));
			lOStaticFree.RemoveAll(liIndex => lOUsed.Contains(liIndex));

			foreach (int liIndex in lORemoved)
			{
				List<int> lOList = liIndex < MobileCount ? lOMobileFree : lOStaticFree;

				if (!lOList.Contains(liIndex))
					lOList.Add(liIndex);
			}

			// Only NEWLY allocated slots - the list of active critters does not hold every
			// critter of the level, only those the original is currently moving.
			HashSet<int> lONewIndices = new HashSet<int>();

			List<UWObject> lODropped = new List<UWObject>();

			foreach (UWObject lOObject in lOOrder)
			{
				if (lOIndex.ContainsKey(lOObject))
					continue;

				bool lbCritter = lOObject.ID >= FirstCritterId && lOObject.ID <= LastCritterId;
				List<int> lOList = lbCritter ? lOMobileFree : lOStaticFree;

				// An item may take a mobile slot if need be, but a critter may not take a
				// static one - it would lack the extra bytes.
				if (lOList.Count == 0 && !lbCritter && lOMobileFree.Count > 0)
					lOList = lOMobileFree;

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
				if (!lbCritter && lOObject.SlotIndex >= MobileCount && lOStaticFree.Contains(lOObject.SlotIndex))
				{
					liIndex = lOObject.SlotIndex;
					lOStaticFree.Remove(liIndex);
				}
				else
					lOList.RemoveAt(lOList.Count - 1);

				lOIndex[lOObject] = liIndex;
				lOUsed.Add(liIndex);
				lONewIndices.Add(liIndex);
			}

			// --- 4. Chain successors: tile chains and child chains
			Dictionary<UWObject, int> lONext = new Dictionary<UWObject, int>();

			for (int liTile = 0; liTile < TileCount && liTile < pOLevel.TileData.Length; liTile++)
			{
				UWTile lOTile = pOLevel.TileData[liTile];

				if (lOTile == null || lOTile.ObjectsInTile == null)
					continue;

				fLinkChain(lOTile.ObjectsInTile, lOIndex, lONext);
			}

			foreach (KeyValuePair<UWObject, List<UWObject>> lOPair in lOChildren)
				fLinkChain(lOPair.Value, lOIndex, lONext);

			// --- 5. Write records
			foreach (KeyValuePair<UWObject, int> lOPair in lOIndex)
			{
				UWObject lOObject = lOPair.Key;
				int liIndex = lOPair.Value;
				int liOffset = liIndex < MobileCount
					? MobileBase + (liIndex * MobileRecordSize)
					: StaticBase + ((liIndex - MobileCount) * StaticRecordSize);

				int liWord0 = (lOObject.ID & 0x1FF)
					| ((lOObject.Flags & 0x7) << 9)
					| ((lOObject.IsEnchanted ? 1 : 0) << 12)
					| ((lOObject.DoorDirection ? 1 : 0) << 13)
					| ((lOObject.IsHidden ? 1 : 0) << 14)
					| ((lOObject.HasQuantity ? 1 : 0) << 15);

				int liWord1 = (lOObject.ZPos & 0x7F)
					| ((lOObject.Heading & 0x7) << 7)
					| ((lOObject.YPos & 0x7) << 10)
					| ((lOObject.XPos & 0x7) << 13);

				int liNext;

				if (!lONext.TryGetValue(lOObject, out liNext))
					liNext = 0;

				int liWord2 = (lOObject.Quality & 0x3F) | (liNext << 6);

				int liSpecial = lOObject.Quantity & 0x3FF;

				if (!lOObject.HasQuantity && !lOUnresolved.Contains(lOObject))
				{
					List<UWObject> lOKids;

					liSpecial = lOChildren.TryGetValue(lOObject, out lOKids) && lOKids.Count > 0
						&& lOIndex.ContainsKey(lOKids[0])
						? lOIndex[lOKids[0]]
						: 0;
				}

				int liWord3 = (lOObject.Owner & 0x3F) | (liSpecial << 6);

				fWrite16(lyOut, liOffset, liWord0);
				fWrite16(lyOut, liOffset + 2, liWord1);
				fWrite16(lyOut, liOffset + 4, liWord2);
				fWrite16(lyOut, liOffset + 6, liWord3);

				if (liIndex < MobileCount)
					fWriteNpcBytes(lyOut, liOffset + StaticRecordSize, lOObject as UWNpc);
			}

			// A removed slot loses its object number - that is how the original releases
			// (ObjectFreeLists.ReleaseFreeObject sets item_id to zero).
			foreach (int liIndex in lORemoved)
			{
				if (lOUsed.Contains(liIndex))
					continue;

				int liOffset = liIndex < MobileCount
					? MobileBase + (liIndex * MobileRecordSize)
					: StaticBase + ((liIndex - MobileCount) * StaticRecordSize);

				fWrite16(lyOut, liOffset, fRead16(lyOut, liOffset) & ~0x1FF);
			}

			// --- 6. Tiles
			for (int liTile = 0; liTile < TileCount && liTile < pOLevel.TileData.Length; liTile++)
			{
				UWTile lOTile = pOLevel.TileData[liTile];

				if (lOTile == null)
					continue;

				int liHead = 0;

				if (lOTile.ObjectsInTile != null)
				{
					foreach (UWObject lOObject in lOTile.ObjectsInTile)
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
				lyWord = (lyWord & 0x003FFFFFu) | ((uint)liHead << 22);

				int liAt = liTile * 4;

				lyOut[liAt] = (byte)(lyWord & 0xFF);
				lyOut[liAt + 1] = (byte)((lyWord >> 8) & 0xFF);
				lyOut[liAt + 2] = (byte)((lyWord >> 16) & 0xFF);
				lyOut[liAt + 3] = (byte)((lyWord >> 24) & 0xFF);
			}

			// --- 7. Free lists and active critters
			fWriteFreeList(lyOut, MobileFreeBase, MobileFreePtrOffset, lOMobileFree, (StaticFreeBase - MobileFreeBase) / 2);
			fWriteFreeList(lyOut, StaticFreeBase, StaticFreePtrOffset, lOStaticFree, (ActiveMobileBase - StaticFreeBase) / 2);
			fWriteActiveMobiles(lyOut, pyOriginal, lOIndex, lONewIndices, lOUsed, lORemoved);

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

				HashSet<UWObject> lOSeen = new HashSet<UWObject>();
				UWObject lOCurrent = pOMaster[pOObject.Quantity];

				while (lOCurrent != null && lOSeen.Add(lOCurrent) && lOSeen.Count <= ObjectCount)
				{
					lOKids.Add(lOCurrent);
					lOCurrent = lOCurrent.Link == 0 || lOCurrent.Link >= pOMaster.Count ? null : pOMaster[lOCurrent.Link];
				}
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

			for (int liTile = 0; liTile < TileCount; liTile++)
			{
				int liAt = liTile * 4;
				uint lyWord = (uint)(pyBlock[liAt] | (pyBlock[liAt + 1] << 8) | (pyBlock[liAt + 2] << 16) | (pyBlock[liAt + 3] << 24));
				int liHead = (int)(lyWord >> 22);

				if (liHead != 0)
					lOStack.Push(liHead);
			}

			while (lOStack.Count > 0)
			{
				int liIndex = lOStack.Pop();

				if (liIndex <= 0 || liIndex >= ObjectCount || !lOReach.Add(liIndex))
					continue;

				int liOffset = liIndex < MobileCount
					? MobileBase + (liIndex * MobileRecordSize)
					: StaticBase + ((liIndex - MobileCount) * StaticRecordSize);

				int liWord0 = fRead16(pyBlock, liOffset);
				int liNext = fRead16(pyBlock, liOffset + 4) >> 6;
				int liSpecial = fRead16(pyBlock, liOffset + 6) >> 6;

				if (liNext != 0)
					lOStack.Push(liNext);

				if ((liWord0 & 0x8000) == 0 && liSpecial != 0)
					lOStack.Push(liSpecial);
			}

			return lOReach;
		}

		/// <summary>Entries 0 up to the pointer, in storage order - the topmost last.
		/// </summary>
		private static List<int> fReadFreeList(byte[] pyBlock, int piBase, int piPtrOffset)
		{
			List<int> lOList = new List<int>();
			int liPtr = (short)fRead16(pyBlock, piPtrOffset);

			for (int liAt = 0; liAt <= liPtr; liAt++)
				lOList.Add(fRead16(pyBlock, piBase + (liAt * 2)));

			return lOList;
		}

		/// <summary>
		/// Writes a free list back. Entries above the pointer stay as they were - the
		/// original also leaves old values lying there. Only when the list gets LONGER are
		/// they overwritten.
		/// </summary>
		private static void fWriteFreeList(byte[] pyBlock, int piBase, int piPtrOffset, List<int> pOList, int piCapacity)
		{
			int liCount = pOList.Count < piCapacity ? pOList.Count : piCapacity;

			for (int liAt = 0; liAt < liCount; liAt++)
				fWrite16(pyBlock, piBase + (liAt * 2), pOList[liAt]);

			fWrite16(pyBlock, piPtrOffset, (liCount - 1) & 0xFFFF);
		}

		/// <summary>
		/// The list of active critters: removed ones are dropped (the last entry moves into
		/// their place, as in the original), newly created ones are appended at the end.
		/// </summary>
		private static void fWriteActiveMobiles(byte[] pyOut, byte[] pyOriginal, Dictionary<UWObject, int> pOIndex,
			HashSet<int> pONewIndices, 			HashSet<int> pOUsed, List<int> pORemoved)
		{
			int liCount = fRead16(pyOriginal, ActiveMobileCountOffset);
			List<int> lOList = new List<int>();

			for (int liAt = 0; liAt < liCount && ActiveMobileBase + liAt < ActiveMobileCountOffset; liAt++)
				lOList.Add(pyOriginal[ActiveMobileBase + liAt]);

			foreach (int liIndex in pORemoved)
			{
				if (liIndex >= MobileCount || pOUsed.Contains(liIndex))
					continue;

				int liPos = lOList.IndexOf(liIndex);

				if (liPos < 0)
					continue;

				lOList[liPos] = lOList[lOList.Count - 1];
				lOList.RemoveAt(lOList.Count - 1);
			}

			foreach (KeyValuePair<UWObject, int> lOPair in pOIndex)
			{
				if (lOPair.Value >= MobileCount || !pONewIndices.Contains(lOPair.Value) || lOList.Contains(lOPair.Value))
					continue;

				if (lOPair.Key.ID < FirstCritterId || lOPair.Key.ID > LastCritterId)
					continue;

				lOList.Add(lOPair.Value);
			}

			int liMax = ActiveMobileCountOffset - ActiveMobileBase;

			for (int liAt = 0; liAt < lOList.Count && liAt < liMax; liAt++)
				pyOut[ActiveMobileBase + liAt] = (byte)lOList[liAt];

			fWrite16(pyOut, ActiveMobileCountOffset, lOList.Count < liMax ? lOList.Count : liMax);
		}

		/// <summary>
		/// The nineteen extra bytes. The basis is the bytes that were read, or zeros for a new
		/// critter (conjured) - not the leftovers of a critter that previously occupied this
		/// slot. The fields UWLevel.load_object_list reads are overwritten with the same masks,
		/// except NPCSpawned, NPCNoHealing and NPCAttitudeLocked, which stay as they are in the
		/// basis bytes - at runtime the record writes them there itself (breaking Tybal's orb sets
		/// NPCNoHealing on him).
		/// </summary>
		private static void fWriteNpcBytes(byte[] pyBlock, int piAt, UWNpc pONpc)
		{
			if (pONpc == null)
				return;

			byte[] lyNpc = pONpc.RawNpcBytes != null && pONpc.RawNpcBytes.Length >= 19
				? (byte[])pONpc.RawNpcBytes.Clone()
				: new byte[19];

			lyNpc[0] = pONpc.HitPoints;

			int liWord3 = lyNpc[3] | (lyNpc[4] << 8);
			liWord3 = (liWord3 & ~0x0FFF) | (pONpc.NPCGoal & 0xF) | ((pONpc.NPCGTarg & 0xFF) << 4);
			lyNpc[3] = (byte)(liWord3 & 0xFF);
			lyNpc[4] = (byte)((liWord3 >> 8) & 0xFF);

			int liWord5 = lyNpc[5] | (lyNpc[6] << 8);
			liWord5 = (liWord5 & ~0xF00F) | (pONpc.NPCLevel & 0xF) | ((pONpc.NPCLootSpawned ? 1 : 0) << 12)
				| ((pONpc.NPCTalkedTo ? 1 : 0) << 13) | ((pONpc.NPCAttitude & 0x3) << 14);
			lyNpc[5] = (byte)(liWord5 & 0xFF);
			lyNpc[6] = (byte)((liWord5 >> 8) & 0xFF);

			int liWord14 = lyNpc[14] | (lyNpc[15] << 8);
			liWord14 = (liWord14 & ~0xFFF0) | ((pONpc.NPCYHome & 0x3F) << 4) | ((pONpc.NPCXHome & 0x3F) << 10);
			lyNpc[14] = (byte)(liWord14 & 0xFF);
			lyNpc[15] = (byte)((liWord14 >> 8) & 0xFF);

			lyNpc[16] = (byte)((lyNpc[16] & ~0x1F) | (pONpc.NPCHeading & 0x1F));
			// Byte 0x19: only the ally bit comes from a field. Bits 0-5 are the creature AI's
			// perception and temper flags (target confirmed, heard, spell slot, made a stand,
			// relentless; Docs/AI/creature-ai.md 2.2) and bit 7 the hunger bit of the
			// conversation import - all of them live in the raw bytes, written by
			// UWCritterRecord and UWConversationSession. Until 2026-09-20 this line copied the
			// load-time NPCHunger over bits 0-6 and so threw the AI flags away on every save.
			lyNpc[17] = (byte)((lyNpc[17] & ~0x40) | (pONpc.NPCIsAlly ? 0x40 : 0));
			lyNpc[18] = pONpc.NPCwhoami;

			System.Array.Copy(lyNpc, 0, pyBlock, piAt, 19);
		}

		private static int fRead16(byte[] pyData, int piAt)
		{
			return pyData[piAt] | (pyData[piAt + 1] << 8);
		}

		private static void fWrite16(byte[] pyData, int piAt, int piValue)
		{
			pyData[piAt] = (byte)(piValue & 0xFF);
			pyData[piAt + 1] = (byte)((piValue >> 8) & 0xFF);
		}
	}
}
