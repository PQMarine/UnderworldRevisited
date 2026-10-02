using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The carried weight.
	///
	/// COMOBJ.DAT holds a mass per object type in TENTHS of stones (bits 4 to 15 of the second
	/// word, see UWCommonObjectProperties.MassTenthStones). Everything the character carries
	/// is counted: equipment, backpack and the contents of containers.
	///
	/// A stack counts multiple times - eleven loaves weigh eleven times as much as one. From 512 on,
	/// however, the quantity field is no longer a number but a special property
	/// (uw-formats.txt 4.3); then the item counts once.
	///
	/// What the original does with the weight is described in UWPlayerData.MaxWeight - it also
	/// notes there that the documented formula does not match the stored values.
	/// </summary>
	public static class UWInventoryWeight
	{
		/// <summary>From here on the quantity field is a special property, not an item count.</summary>
		private const int SpecialPropertyThreshold = UWObjectMechanics.SpecialPropertyThreshold;

		/// <summary>Safeguard against container contents linked in a circle.</summary>
		private const int MaximumDepth = 16;

		/// <summary>Weight in tenths of stones over a list of items, containers
		/// included.</summary>
		public static int GetTotalTenthStones(IEnumerable<UWObject> pOItems, UWCommonObjectProperties pOProperties)
		{
			return fSum(pOItems, pOProperties, 0);
		}

		/// <summary>Weight of a single item including contents, in tenths of stones.</summary>
		public static int GetItemTenthStones(UWObject pOItem, UWCommonObjectProperties pOProperties)
		{
			if (pOItem == null || pOProperties == null)
				return 0;

			return fItem(pOItem, pOProperties, 0);
		}

		/// <summary>Tenths of stones as a readable number, e.g. "12.4".</summary>
		public static string Format(int piTenthStones)
		{
			return (piTenthStones / 10) + "." + (piTenthStones % 10);
		}

		private static int fSum(IEnumerable<UWObject> pOItems, UWCommonObjectProperties pOProperties, int piDepth)
		{
			if (pOItems == null || pOProperties == null || piDepth > MaximumDepth)
				return 0;

			int liTotal = 0;

			foreach (UWObject lOItem in pOItems)
			{
				if (lOItem != null)
					liTotal += fItem(lOItem, pOProperties, piDepth);
			}

			return liTotal;
		}

		private static int fItem(UWObject pOItem, UWCommonObjectProperties pOProperties, int piDepth)
		{
			UWCommonObjectProperties.Entry lOEntry;

			int liOwn = pOProperties.TryGet(pOItem.ID, out lOEntry) ? lOEntry.MassTenthStones : 0;

			int liCount = 1;

			if (pOItem.HasQuantity && pOItem.Quantity > 1 && pOItem.Quantity < SpecialPropertyThreshold)
				liCount = pOItem.Quantity;

			int liTotal = liOwn * liCount;

			if (pOItem.Contents != null)
				liTotal += fSum(pOItem.Contents, pOProperties, piDepth + 1);

			return liTotal;
		}
	}
}
