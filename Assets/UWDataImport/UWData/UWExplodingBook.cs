namespace UWDataImport.UWData
{
	/// <summary>
	/// BRONUS' BOOK FOR MORLOCK, OPENED (ExplodingBook_ovr107_1259, read 2026-10-01; per user:
	/// "rebuild the opening as the original has it"). Bronus (conversation 190, level 6) makes the
	/// player promise not to open it; Morlock (conversation 13) waits for it.
	///
	/// USING object 0x114 from the inventory (Major_4_Minor_1_ObjectUsage_seg040_B84, case 0x114,
	/// only with the in-inventory flag set; used in the world nothing happens) calls the routine.
	/// It searches the player's tile, depth first through every container, for the first object
	/// of major class 4, minor class 1, index 4 - 0x114 itself, nothing else of the group
	/// (FindMatchingObject_seg027_B36 with 4, 1, 4) - and only when there is one:
	///   - "The book explodes in your face!" (a literal in UW.EXE, not in STRINGS.PAK);
	///   - quest flag 8 is set (bit 8 of the word at PLAYER.DAT 0x65) - Bronus reads it ("A little
	///     curious, wert thou?"), hands over a second copy and clears it again;
	///   - the class 9 curse with three dice on the player (MajorSpellClass9_CursedObjects_seg038_473,
	///     minor 3);
	///   - the book leaves the inventory and is gone.
	/// The a_do trap 41 calls the same routine (UWTrapRules); no level holds one.
	///
	/// Ours looks in the inventory (UWInventoryModel.FindCarried, the original's inventory order):
	/// the used book is carried, and the only other copy is the one Bronus creates after the
	/// first is gone. A second copy lying on the floor of the player's tile is not looked at.
	/// </summary>
	public static class UWExplodingBook
	{
		/// <summary>a_book, Bronus' book (string block 4, "a_book" at 0x114).</summary>
		public const int BookObjectId = 0x114;

		public const string Message = "The book explodes in your face!";

		public const int QuestFlag = 8;

		/// <summary>How many dice the curse rolls (minor class 3 of the class 9 curse).</summary>
		public const int CurseDice = 3;

		/// <summary>The first book 0x114 carried, in the original's inventory order.</summary>
		public static UWObject FindCarried(UWInventoryModel pOInventory)
		{
			return pOInventory != null ? pOInventory.FindCarried(pOObject => pOObject.ID == BookObjectId) : null;
		}

		/// <summary>The book was used from the inventory. Always true: whatever the search
		/// finds, the use is over.</summary>
		public static bool Open(UWObject pOItem, UWInventoryModel pOInventory, IUWItemUseHost pIHost)
		{
			UWObject lOBook = pOInventory != null ? FindCarried(pOInventory) : pOItem;

			if (lOBook == null)
				return true;

			pIHost.AddMessage(Message);
			UWQuestFlags.Set(QuestFlag, 1);
			pIHost.CursePlayer(CurseDice);

			if (lOBook == pOItem || pOInventory == null)
				pIHost.ConsumeUsedItem();
			else
				pOInventory.RemoveItem(lOBook);

			if (pOInventory != null)
				pOInventory.NotifyChanged();

			return true;
		}
	}
}
