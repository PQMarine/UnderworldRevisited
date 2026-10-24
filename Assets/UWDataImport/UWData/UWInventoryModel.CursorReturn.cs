using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// PUTTING THE THING ON THE POINTER BACK (cleanup step 5.15, 2026-10-22): one rule for both
	/// schemes, read from one UWCursorOrigin. Until then the classic drag had CancelCursorItem here
	/// and the modern bags their own fReturnCursorItem (UWModernBags) - the two ways back differ, and
	/// each difference is behaviour the player knows, so both stay, named by ReturnEnum.
	/// </summary>
	public sealed partial class UWInventoryModel
	{
		/// <summary>How a thing goes back (see ReturnCursorItem).</summary>
		public enum ReturnEnum
		{
			/// <summary>
			/// The classic drag's cancel (a scheme change during dragging, or similar): the place it
			/// was taken from - a backpack place or a paperdoll slot - is guaranteed to still be
			/// empty (see BeginDragFrom*), and it goes back there as it was; without a known origin
			/// (after a swap, see DropCursorItemIn*; dragged from the world or from a container - all
			/// of them deliberately fall back only into the backpack instead of their old place) the
			/// first free backpack place is the safety net; with the backpack full it stays on the
			/// pointer.
			/// </summary>
			AsTaken,

			/// <summary>
			/// The modern bags (closing them, the character panel closing, the pointer locking, a
			/// piece that would not go on): back where it came from while that place is still free
			/// or the container or stack still carried, else wherever there is room
			/// (DropCursorItemAnywhere); the carried light is applied afterwards.
			/// </summary>
			OrAnywhere
		}

		/// <summary>
		/// Puts CursorItem back by pOOrigin, the way peWay says. True when the pointer is empty
		/// afterwards (also when nothing hung there); false when the thing found no place and stays.
		/// </summary>
		public bool ReturnCursorItem(UWCursorOrigin pOOrigin, ReturnEnum peWay)
		{
			if (CursorItem == null)
				return true;

			if (peWay == ReturnEnum.AsTaken)
				fReturnAsTaken(pOOrigin);
			else
				fReturnOrAnywhere(pOOrigin);

			return CursorItem == null;
		}

		/// <summary>Whether pOItem lies anywhere in the inventory - backpack, equipment or a
		/// container's resolved contents.</summary>
		public bool IsCarried(UWObject pOItem)
		{
			foreach (UWObject lOItem in EnumerateAll())
			{
				if (lOItem == pOItem)
					return true;
			}

			return false;
		}

		private void fReturnAsTaken(UWCursorOrigin pOOrigin)
		{
			UWObject lOItem = CursorItem;
			CursorItem = null;

			if (pOOrigin.Kind == UWCursorOrigin.KindEnum.Backpack)
			{
				Backpack[pOOrigin.Index] = lOItem;
			}
			else if (pOOrigin.Kind == UWCursorOrigin.KindEnum.Equip)
			{
				EquipSlots[(int)pOOrigin.Slot] = lOItem;
			}
			else
			{
				int liFreeSlot = Array.IndexOf(Backpack, null);

				if (liFreeSlot >= 0)
					Backpack[liFreeSlot] = lOItem;
				else
					CursorItem = lOItem; // Backpack full - better kept at the pointer than lost.
			}

			InventoryChanged?.Invoke();
		}

		private void fReturnOrAnywhere(UWCursorOrigin pOOrigin)
		{
			bool lbBack = false;

			// The bags put the pointer's thing back: a part goes onto the stack it came from while
			// that is still carried.
			if (pOOrigin.Kind == UWCursorOrigin.KindEnum.Stack && pOOrigin.Holder != null && IsCarried(pOOrigin.Holder)
				&& TryStackInto(pOOrigin.Holder))
				lbBack = true;
			else if (pOOrigin.Kind == UWCursorOrigin.KindEnum.Equip && GetEquipped(pOOrigin.Slot) == null)
				lbBack = DropCursorItemInEquip(pOOrigin.Slot, false);
			else if (pOOrigin.Kind == UWCursorOrigin.KindEnum.Backpack && pOOrigin.Index >= 0
				&& Backpack[pOOrigin.Index] == null)
				lbBack = DropCursorItemInBackpack(pOOrigin.Index);
			else if (pOOrigin.Kind == UWCursorOrigin.KindEnum.Container && pOOrigin.Holder != null
				&& IsCarried(pOOrigin.Holder))
				lbBack = InsertCursorItemInContainer(pOOrigin.Holder, pOOrigin.Index);

			if (!lbBack && CursorItem != null)
				DropCursorItemAnywhere();

			ApplyCarriedLight();
		}
	}
}
