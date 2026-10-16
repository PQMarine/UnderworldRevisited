using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ONE DEFINITION OF A STACK: whether an object's count field is a count at all, how
	/// many pieces it is, and how one piece comes off it.
	///
	/// The link word of an is_quant object (UWObject.HasQuantity) is a count below 512 and a
	/// special property from 512 on - the word less 0x200 is then an enchantment, a string
	/// index and so on (uw-formats.txt 4.3). UW.EXE tests exactly this wherever it counts a
	/// stack: is_quant and not (link and 0x200) - chkTenacious, ItemWeight, invRemoveObject,
	/// the look, eat and barter code (UnderworldExhumed OBJECTS.C, INVDATA.C, LOOK.C,
	/// USEITEMS.C, BARTER.C, read 2026-10-11). The ENCHANTED flag plays no part in it: an
	/// enchanted is_quant object carries its spell in the same word from 512 on, so the
	/// threshold already excludes it (GetItemEnchantment reads the word and 0x1FF, whatever
	/// the 0x200 bit).
	///
	/// Until 2026-10-11 this rule was written six times with three differences (B7 of the
	/// cleanup plan): some copies passed a count of 0 through and some read it as 1, some also
	/// refused an enchanted object, and the consume paths of UWInventoryModel and UWItemDrag
	/// disagreed on an enchanted stack. Count keeps the original's raw reading (0 stays 0, as
	/// chkTenacious and the barter value multiply with it); CountAtLeastOne is for the places
	/// that show or prompt for a number.
	/// </summary>
	public static class UWStackRules
	{
		/// <summary>From here on the quantity field is a special property, not a count.</summary>
		public const int SpecialPropertyThreshold = 512;

		/// <summary>
		/// The largest count a stack may reach. A DELIBERATE DEVIATION: the original's
		/// AddTogether merges while the total stays below 999, and a total from 512 to 998 then
		/// sets the special bit - the stack turns into something else (per user on the original,
		/// 2026-10-12: 300 and 250 arrows combined became a single arrow). Ours stops at 511 and
		/// puts the rest on a free slot.
		/// </summary>
		public const int MaxStackCount = SpecialPropertyThreshold - 1;

		/// <summary>Is the quantity field really a count - so may one subtract from it, or
		/// split and join the pile? False without the quantity bit (the word is a link to an
		/// object chain) and from 512 on (a special property).</summary>
		public static bool IsStackable(UWObject pOObject)
		{
			return pOObject != null && pOObject.HasQuantity && pOObject.Quantity < SpecialPropertyThreshold;
		}

		/// <summary>The quantity bit is set but the word is a special property (an enchantment
		/// on the object itself, a string index), not a count.</summary>
		public static bool HasSpecialProperty(UWObject pOObject)
		{
			return pOObject != null && pOObject.HasQuantity && pOObject.Quantity >= SpecialPropertyThreshold;
		}

		/// <summary>How many pieces, as UW.EXE reads it: the count of a stackable object (a 0
		/// stays 0), 1 for anything else - a single piece without the bit, a container, an
		/// enchanted object.</summary>
		public static int Count(UWObject pOObject)
		{
			return IsStackable(pOObject) ? pOObject.Quantity : 1;
		}

		/// <summary>Count, but never below 1 - for a shown number, a "Move how many?" prompt,
		/// a weight. A quantity field of 0 (an object placed without a count) is one piece.</summary>
		public static int CountAtLeastOne(UWObject pOObject)
		{
			return Math.Max(1, Count(pOObject));
		}

		/// <summary>More than one piece in a pile.</summary>
		public static bool IsStack(UWObject pOObject)
		{
			return Count(pOObject) > 1;
		}

		/// <summary>
		/// Takes one piece off a stack: the count drops by one and the object stays where it is.
		/// False when the object is a single piece (or no count at all) - then the caller
		/// removes the whole object, as invRemoveObject does when the stack is not larger than
		/// what is taken.
		/// </summary>
		public static bool TryTakeOne(UWObject pOObject)
		{
			if (!IsStack(pOObject))
				return false;

			pOObject.Quantity = (ushort)(pOObject.Quantity - 1);

			return true;
		}

		/// <summary>
		/// THE BARTER'S JOIN (2026-10-15): may pOAdded's count go onto pOTarget, the target keeping
		/// its own quality? Both counted stacks of the same item, and the sum within the limit. The
		/// barter code tests exactly this wherever two piles meet - a drop on a trade slot
		/// (CombineToSlot_ovr097_D30), coins the partner receives (npc_inv_add,
		/// probablyTradeObjects_ovr097_100A) and an item he creates (npc_inv_create), UnderworldExhumed
		/// BARTER.C - with no quality band, unlike the inventory's AddTogether. The original's limit
		/// is a sum below 999; ours stops at MaxStackCount (see there).
		/// </summary>
		public static bool CanJoinBarterStack(UWObject pOTarget, UWObject pOAdded)
		{
			return IsStackable(pOTarget) && IsStackable(pOAdded) && pOTarget.ID == pOAdded.ID
				&& pOTarget.Quantity + pOAdded.Quantity <= MaxStackCount;
		}
	}
}
