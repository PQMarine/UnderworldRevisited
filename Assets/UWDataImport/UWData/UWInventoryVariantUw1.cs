namespace UWDataImport.UWData
{
	/// <summary>The first game's inventory steps: UWArmorItemMap's tables, as before the variant.</summary>
	public sealed class UWInventoryVariantUw1 : IUWInventoryVariant
	{
		public int LoreLookTarget => UWLoreCheck.LoreTarget;

		public int CarryLimit(DataImport pOData)
		{
			return pOData != null && pOData.InitialPlayer != null ? pOData.InitialPlayer.MaxWeight : -1;
		}

		public bool FitsBodySlot(UWObject pOItem, UWArmorItemMap.BodySlot peSlot, UWArmorItemMap.BodySlot peMainHand,
			UWObjectClassProperties pOClasses)
		{
			// The weapon hand: no stack of melee weapons (uw1 inv/INVPANEL.C:585-586; cleanup plan 2, K14, 2026-11-28, as the
			// second game's variant). The same weapon already there swaps, as in the original (per user the same day).
			if (pOItem != null && peSlot == peMainHand && pOItem.ID <= UWInventoryVariantUw2.LastMeleeWeaponId
				&& UWStackRules.Count(pOItem) > 1)
				return false;

			return UWArmorItemMap.Fits(pOItem, peSlot);
		}

		public bool TryGetPreferredBodySlot(UWObject pOItem, UWObjectClassProperties pOClasses, out UWArmorItemMap.BodySlot peSlot)
		{
			return UWArmorItemMap.TryGetPreferredSlot(pOItem, out peSlot);
		}

		public bool MatchesContainerMask(int piMask, UWObject pOItem, UWObjectClassProperties pOClasses)
		{
			return pOItem != null && UWContainerCapacity.MatchesMaskUw1(piMask, pOItem.ID);
		}
	}
}
