using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE INVENTORY OF A PLAYER.DAT WORKSPACE AS OBJECTS (UW2 plan phase 3 step 3, 2026-10-29): the
	/// workspace UWPlayerFile reads for both games - the player object, the cursor object, the slot words
	/// (an object's number in bits 6-15) and the saved objects from 0x5B, eight bytes each, numbered from
	/// 1 (Underworld Exhumed docs/FORMATS.md "PLAYER.DAT") - turned into UWObjects as the first game's
	/// UWPlayerData.fParseInventory does it from its own offsets: every record read through
	/// UWObjectRecordCodec, the eleven equipment places and the eight of the backpack from the slots,
	/// the containers' contents resolved against the records themselves, not the level's.
	/// </summary>
	public sealed class UWInventoryWorkspace
	{
		public const int EquipmentSlotCount = 11;

		public const int BackpackSlotCount = 8;

		/// <summary>The records by their number; 0 is none.</summary>
		public List<UWObject> Records { get; } = new List<UWObject> { null };

		/// <summary>The eleven places in the save's order (helmet, chest, gloves, leggings, boots,
		/// shoulders, hands, rings - UWInventoryModel.FromSavegameSlot).</summary>
		public UWObject[] Equipment { get; } = new UWObject[EquipmentSlotCount];

		public UWObject[] Backpack { get; } = new UWObject[BackpackSlotCount];

		public static UWInventoryWorkspace Read(UWPlayerFile pOFile)
		{
			UWInventoryWorkspace lOResult = new UWInventoryWorkspace();
			byte[] lyWorkspace = pOFile != null ? pOFile.Workspace : null;

			if (lyWorkspace == null)
				return lOResult;

			for (int liAt = UWPlayerFile.WorkspaceHeaderSize + UWPlayerFile.SavedObjectSize;
				liAt + UWPlayerFile.SavedObjectSize <= lyWorkspace.Length; liAt += UWPlayerFile.SavedObjectSize)
			{
				UWObject lOObject = new UWObject((ushort)UWObjectRecordCodec.ReadId(lyWorkspace, liAt));

				UWObjectRecordCodec.Read(lyWorkspace, liAt, lOObject);
				lOResult.Records.Add(lOObject);
			}

			for (int liSlot = 0; liSlot < EquipmentSlotCount + BackpackSlotCount; liSlot++)
			{
				int liNumber = pOFile.Slot(liSlot);

				if (liNumber < 1 || liNumber >= lOResult.Records.Count)
					continue;

				if (liSlot < EquipmentSlotCount)
					lOResult.Equipment[liSlot] = lOResult.Records[liNumber];
				else
					lOResult.Backpack[liSlot - EquipmentSlotCount] = lOResult.Records[liNumber];
			}

			for (int liRecord = 1; liRecord < lOResult.Records.Count; liRecord++)
				lOResult.Records[liRecord].EnsureContentsLoaded(lOResult.Records);

			return lOResult;
		}

		/// <summary>
		/// THE INVENTORY WRITTEN (UW2 plan phase 3 step 3: out of the first game's UWPlayerData, the code as
		/// it was): the records renumbered from 1, the pack's eight first and then the eleven places, each
		/// depth first with what hangs off it; the slot words at piSlotOffset, the top level linked as a
		/// chain, the count plus one at piCountOffset, the records from piRecordOffset (record 1), the array
		/// cut or grown to them. The objects themselves are not changed. The first game passes its file's
		/// offsets, the second game's workspace writer its own (WriteInto). The remarks below are the first
		/// game's, where the code was found and checked.
		/// </summary>
		public static string WriteRecords(ref byte[] pyPlain, int piCountOffset, int piSlotOffset, int piRecordOffset,
			UWObject[] pOEquipment, UWObject[] pOBackpack, List<UWObject> pOWorldObjects, List<UWObject> pOLoadedRecords)
		{
			if (pyPlain.Length < piRecordOffset)
				return "PLAYER.DAT is too short for an inventory.";

			List<UWObject> lOOrder = new List<UWObject>();
			Dictionary<UWObject, int> lOIndex = new Dictionary<UWObject, int>();
			Dictionary<UWObject, List<UWObject>> lOChildren = new Dictionary<UWObject, List<UWObject>>();

			UWObject[] lOTop = new UWObject[EquipmentSlotCount + BackpackSlotCount];

			for (int liSlot = 0; liSlot < EquipmentSlotCount; liSlot++)
				lOTop[liSlot] = liSlot < pOEquipment.Length ? pOEquipment[liSlot] : null;

			for (int liSlot = 0; liSlot < BackpackSlotCount; liSlot++)
				lOTop[EquipmentSlotCount + liSlot] = liSlot < pOBackpack.Length ? pOBackpack[liSlot] : null;

			// THE ORDER: first the eight backpack slots, then the eleven equipment slots, each
			// depth first with its contents - the reference's "DOS-canonical order"
			// (PlayerDatWriter.SerializeUw1Canonical). It is NOT the original's rule (corrected
			// 2026-10-12): UW.EXE writes the player's object chain depth first in chain order
			// and loads any order, by link and by saved index (UnderworldExhumed INVSAVE.C
			// InvSaveNexts, putInInv). So this order is ours and harmless.
			List<UWObject> lOTopEmitted = new List<UWObject>();

			for (int liSlot = EquipmentSlotCount; liSlot < lOTop.Length; liSlot++)
				fCollectTop(lOTop[liSlot], lOTopEmitted, lOOrder, lOIndex, lOChildren, pOWorldObjects, pOLoadedRecords);

			for (int liSlot = 0; liSlot < EquipmentSlotCount; liSlot++)
				fCollectTop(lOTop[liSlot], lOTopEmitted, lOOrder, lOIndex, lOChildren, pOWorldObjects, pOLoadedRecords);

			// Ten bits for the index - the format holds no more.
			if (lOOrder.Count > 1023)
				return "Too many items for the format (" + lOOrder.Count + ").";

			byte[] lyNew = new byte[piRecordOffset + (lOOrder.Count * UWObjectRecordCodec.RecordSize)];
			System.Array.Copy(pyPlain, lyNew, piRecordOffset);

			for (int liSlot = 0; liSlot < lOTop.Length; liSlot++)
			{
				int liRecord = lOTop[liSlot] != null && lOIndex.ContainsKey(lOTop[liSlot])
					? lOIndex[lOTop[liSlot]] : 0;

				UWBinary.Write16(lyNew, piSlotOffset + (liSlot * 2), liRecord << 6);
			}

			// Who follows whom: siblings in a chain point to each other via link.
			Dictionary<UWObject, int> lONext = new Dictionary<UWObject, int>();

			foreach (KeyValuePair<UWObject, List<UWObject>> lOPair in lOChildren)
			{
				for (int liAt = 0; liAt < lOPair.Value.Count; liAt++)
					lONext[lOPair.Value[liAt]] = liAt + 1 < lOPair.Value.Count ? lOIndex[lOPair.Value[liAt + 1]] : 0;
			}

			// THE TOP LEVEL IS ITSELF A CHAIN. That was the bug in the first
			// attempt (per user, 2026-09-10): we set zero everywhere there, and the
			// original walked the chain from record one and stopped right away. All that remained
			// was what sat in record one - the crown in the helmet slot. The slot table alone
			// is not enough for the original; our own loader only reads that, which is why everything
			// looked right for us. Checked on Henrietta's SAVE3 written by the original:
			// there every top-level item points to the next one.
			for (int liAt = 0; liAt < lOTopEmitted.Count; liAt++)
				lONext[lOTopEmitted[liAt]] = liAt + 1 < lOTopEmitted.Count ? lOIndex[lOTopEmitted[liAt + 1]] : 0;

			HashSet<UWObject> lOTopSet = new HashSet<UWObject>(lOTopEmitted);

			// THE NUMBER OF RECORDS PLUS ONE is at 0xD2, sixteen bits, in plain text.
			// The original reads from it how many records to load. Checked on all four of the
			// user's save games (15, 29, 63 and 67 records - each stores
			// one more).
			UWBinary.Write16(lyNew, piCountOffset, lOOrder.Count + 1);

			for (int liAt = 0; liAt < lOOrder.Count; liAt++)
			{
				UWObject lOObject = lOOrder[liAt];
				int liOffset = piRecordOffset + (liAt * UWObjectRecordCodec.RecordSize);

				int liWrittenId = lOObject.ID & 0x1FF;

				// AN OPEN SACK AT THE TOP OF THE INVENTORY IS WRITTEN CLOSED. A container of
				// classes 128 to 139 knows its open state via bit 0 of the object number. The
				// original closes every bag it has open before it saves - the open one and those
				// it was opened from, nested ones included (UnderworldExhumed INVSAVE.C
				// makePlayerInvCopy -> BAGS.C FixBagArea, CloseAllBags, MakeBagClose: an odd
				// inclass below 12 loses its bit). The earlier wording here, "the original
				// considers an open sack at top level invalid", was the reference's (corrected
				// 2026-10-12). Ours never sets the bit when a bag is opened (only the picture
				// changes, UWObjectIds.OpenPicture), so no bag the player opened can reach this
				// file open, nested or not; an odd number only comes from the data, and only at
				// the top is it closed here. 143, the rune bag, is outside 128 to 139.
				if (lOTopSet.Contains(lOObject) && UWObjectIds.HasOpenPicture(liWrittenId)
					&& (liWrittenId & 0x1) != 0)
					liWrittenId &= ~0x1;

				int liNext = lONext.ContainsKey(lOObject) ? lONext[lOObject] : 0;

				// Quantity field: a real quantity stays, a special link now points to the
				// new number of the first child - or to nothing if none was found.
				int liSpecial = lOObject.Quantity;

				if (!lOObject.HasQuantity)
				{
					List<UWObject> lOKids;

					liSpecial = lOChildren.TryGetValue(lOObject, out lOKids) && lOKids.Count > 0
						? lOIndex[lOKids[0]] : 0;
				}

				// The record itself is UWObjectRecordCodec's (2026-10-12; the same eight bytes
				// as in the level's object list).
				UWObjectRecordCodec.Write(lyNew, liOffset, lOObject, liWrittenId, liNext, liSpecial);
			}

			pyPlain = lyNew;

			return null;
		}

		/// <summary>A top-level slot: records it for the top-level chain
		/// and adds it together with its contents.</summary>
		private static void fCollectTop(UWObject pOObject, List<UWObject> pOTop, List<UWObject> pOOrder,
			Dictionary<UWObject, int> pOIndex, Dictionary<UWObject, List<UWObject>> pOChildren,
			List<UWObject> pOWorldObjects, List<UWObject> pOLoadedRecords)
		{
			if (pOObject == null || pOIndex.ContainsKey(pOObject))
				return;

			pOTop.Add(pOObject);
			fCollectInventory(pOObject, pOOrder, pOIndex, pOChildren, pOWorldObjects, pOLoadedRecords);
		}

		/// <summary>Adds a thing and everything hanging off it to the order.
		/// Numbers start at one; the format has no record zero.</summary>
		private static void fCollectInventory(UWObject pOObject, List<UWObject> pOOrder,
			Dictionary<UWObject, int> pOIndex, Dictionary<UWObject, List<UWObject>> pOChildren,
			List<UWObject> pOWorldObjects, List<UWObject> pOLoadedRecords)
		{
			if (pOObject == null || pOIndex.ContainsKey(pOObject))
				return;

			pOOrder.Add(pOObject);
			pOIndex[pOObject] = pOOrder.Count;

			List<UWObject> lOKids = fResolveChildren(pOObject, pOWorldObjects, pOLoadedRecords);

			if (lOKids.Count == 0)
				return;

			pOChildren[pOObject] = lOKids;

			foreach (UWObject lOKid in lOKids)
				fCollectInventory(lOKid, pOOrder, pOIndex, pOChildren, pOWorldObjects, pOLoadedRecords);
		}

		/// <summary>The chain behind a thing's special link, as a list.</summary>
		private static List<UWObject> fResolveChildren(UWObject pOObject, List<UWObject> pOWorldObjects, List<UWObject> pOLoadedRecords)
		{
			List<UWObject> lOResult = new List<UWObject>();

			if (pOObject.HasQuantity)
				return lOResult;

			if (pOObject.Contents != null)
			{
				foreach (UWObject lOItem in pOObject.Contents)
				{
					if (lOItem != null)
						lOResult.Add(lOItem);
				}

				return lOResult;
			}

			if (pOObject.Quantity == 0)
				return lOResult;

			// If it comes from the loaded inventory, that list applies; otherwise the level's.
			List<UWObject> lOList = pOLoadedRecords != null && pOLoadedRecords.Contains(pOObject)
				? pOLoadedRecords
				: pOWorldObjects;

			if (lOList == null || pOObject.Quantity >= lOList.Count)
				return lOResult;

			// At most 1024 objects, a circle collected up to that limit as the loop here did
			// before UWObjectChain (2026-10-11) - hence no stop on a revisit.
			foreach (UWObject lOItem in UWObjectChain.Walk(pOObject, lOList, 1024, false))
				lOResult.Add(lOItem);

			return lOResult;
		}

		/// <summary>
		/// THE SECOND GAME'S INVENTORY INTO ITS PLAYER.DAT (UW2 plan phase 3 step 3 part D): the count word
		/// and the workspace laid out as one array - the count at 0, the workspace from 2 - written by
		/// WriteRecords (slots at 2 + 0x23, record 1 at 2 + 0x5B + 8, as UWPlayerFile reads them); then the
		/// player object's link (word 3, bits 6-15) points at record 1 when there is one, as the first game's
		/// fWritePlayerObject sets it (the original walks the chain from there, INVSAVE.C), and the record's
		/// running weight at 0x4A takes piCarriedWeight. pOLoadedRecords: the workspace's records as read, for
		/// a link not yet resolved into contents; pOWorldObjects the level's. Returns null or what failed.
		/// </summary>
		public static string WriteInto(UWPlayerFile pOFile, UWObject[] pOEquipment, UWObject[] pOBackpack,
			List<UWObject> pOWorldObjects, List<UWObject> pOLoadedRecords, int piCarriedWeight)
		{
			if (pOFile == null || pOFile.Workspace == null || pOFile.Workspace.Length < UWPlayerFile.WorkspaceHeaderSize)
				return "no workspace to write into";

			byte[] lyData = new byte[CountWordSize + pOFile.Workspace.Length];

			System.Array.Copy(pOFile.Workspace, 0, lyData, CountWordSize, pOFile.Workspace.Length);

			string lsError = WriteRecords(ref lyData, 0, CountWordSize + UWPlayerFile.SlotsOffset,
				CountWordSize + UWPlayerFile.WorkspaceHeaderSize + UWPlayerFile.SavedObjectSize,
				pOEquipment, pOBackpack, pOWorldObjects, pOLoadedRecords);

			if (lsError != null)
				return lsError;

			int liCount = UWBinary.ReadU16(lyData, 0);
			byte[] lyWorkspace = new byte[lyData.Length - CountWordSize];

			System.Array.Copy(lyData, CountWordSize, lyWorkspace, 0, lyWorkspace.Length);

			int liLink = UWBinary.ReadU16(lyWorkspace, PlayerLinkOffset);

			UWBinary.Write16(lyWorkspace, PlayerLinkOffset, (liLink & 0x3F) | ((liCount > 1 ? 1 : 0) << 6));
			pOFile.SetWorkspace(liCount, lyWorkspace);
			UWBinary.Write16(pOFile.Record, UWPlayerRecordUw2.WeightOffset, piCarriedWeight & 0xFFFF);

			return null;
		}

		/// <summary>The count word before the workspace in PLAYER.DAT.</summary>
		public const int CountWordSize = 2;

		/// <summary>The player object's word 3 in the workspace: its link, the head of the carried chain.</summary>
		public const int PlayerLinkOffset = 6;

		/// <summary>
		/// Gives every record its picture (UWObjectAppearance, as UWObjectFactory does for an object made
		/// during play; the first game's UWPlayerData.AssignTextures does it for its own). Without it a
		/// carried object put down in the world had no picture to be drawn with: the object builder skips
		/// an object without one (per user, 2026-10-29: things carried from the save vanished when dropped).
		/// </summary>
		public void AssignPictures(UWTextures pOTextures)
		{
			if (pOTextures == null)
				return;

			for (int liRecord = 1; liRecord < Records.Count; liRecord++)
			{
				if (Records[liRecord] != null)
					UWObjectAppearance.Apply(Records[liRecord], pOTextures, null);
			}
		}
	}
}
