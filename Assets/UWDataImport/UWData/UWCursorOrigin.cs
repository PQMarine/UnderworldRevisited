namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE THE THING ON THE POINTER CAME FROM, so that a drag which finds no place can put it back
	/// (cleanup step 5.15, 2026-10-22: one value read by one rule, UWInventoryModel.ReturnCursorItem,
	/// for both schemes). Until then the classic drag handed a backpack place and a paperdoll slot to
	/// UWInventoryModel.CancelCursorItem, and the modern bags kept five fields of their own (the split
	/// stack, the container, the index, a backpack flag and the paperdoll slot) with their own way
	/// back.
	///
	/// One origin at a time: whoever takes a thing onto the pointer says where from; after a swap
	/// the thing now on the pointer has no known origin (None).
	/// </summary>
	public readonly struct UWCursorOrigin
	{
		public enum KindEnum
		{
			/// <summary>Unknown - after a swap, from the world, from a trade slot.</summary>
			None,

			/// <summary>A backpack place (Index).</summary>
			Backpack,

			/// <summary>A paperdoll slot (Slot).</summary>
			Equip,

			/// <summary>A place in a carried container's contents (Holder, Index).</summary>
			Container,

			/// <summary>A part split off a carried stack (Holder is the stack).</summary>
			Stack
		}

		public readonly KindEnum Kind;

		/// <summary>The backpack place, or the place in the container's contents.</summary>
		public readonly int Index;

		public readonly UWArmorItemMap.BodySlot Slot;

		/// <summary>The container, or the stack the part came from.</summary>
		public readonly UWObject Holder;

		private UWCursorOrigin(KindEnum peKind, int piIndex, UWArmorItemMap.BodySlot peSlot, UWObject pOHolder)
		{
			Kind = peKind;
			Index = piIndex;
			Slot = peSlot;
			Holder = pOHolder;
		}

		public static UWCursorOrigin None => new UWCursorOrigin(KindEnum.None, -1, default, null);

		public static UWCursorOrigin Backpack(int piSlot)
		{
			return new UWCursorOrigin(KindEnum.Backpack, piSlot, default, null);
		}

		public static UWCursorOrigin Equip(UWArmorItemMap.BodySlot peSlot)
		{
			return new UWCursorOrigin(KindEnum.Equip, -1, peSlot, null);
		}

		public static UWCursorOrigin Container(UWObject pOContainer, int piIndex)
		{
			return new UWCursorOrigin(KindEnum.Container, piIndex, default, pOContainer);
		}

		public static UWCursorOrigin Stack(UWObject pOStack)
		{
			return new UWCursorOrigin(KindEnum.Stack, -1, default, pOStack);
		}
	}
}
