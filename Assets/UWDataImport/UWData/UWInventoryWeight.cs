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

		/// <summary>
		/// THE CAPACITY LEFT in tenths of stones: the player's carrying capacity
		/// (UWPlayerData.MaxWeight, none without a player) minus the carried load - negative when
		/// overloaded. The one home of this subtraction since cleanup step 5.6 (2026-10-20): the
		/// classic frame's circle (UWHudInventory), the modern original page
		/// (UWModernInventoryPage), the help window's stats and UWInventoryModel.CanCarry each
		/// wrote it themselves until then.
		/// </summary>
		public static int RemainingTenthStones(UWPlayerData pOPlayer, int piCarriedTenthStones)
		{
			return (pOPlayer != null ? pOPlayer.MaxWeight : 0) - piCarriedTenthStones;
		}

		/// <summary>What the circle below the paper doll shows: the capacity left in WHOLE stones,
		/// never below 0 - the original shows no decimal place (per screenshot from the user,
		/// 2026-09-01, see UWHudInventory.fRefreshWeight).</summary>
		public static int RemainingStones(UWPlayerData pOPlayer, int piCarriedTenthStones)
		{
			return System.Math.Max(0, RemainingTenthStones(pOPlayer, piCarriedTenthStones)) / 10;
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

			// THE RAW COUNT: the original's ItemWeight (UnderworldExhumed inv/INVDATA.C) multiplies
			// the mass by the quantity field of a stack, so an is_quant object placed with a count of
			// 0 weighs nothing; a special link or no quantity bit weighs one piece. Until 2026-10-15
			// ours weighed a count of 0 as one piece (decided by the user, audit round 2 C10).
			int liTotal = liOwn * UWStackRules.Count(pOItem);

			if (pOItem.Contents != null)
				liTotal += fSum(pOItem.Contents, pOProperties, piDepth + 1);

			return liTotal;
		}
	}
}
