namespace UWDataImport.UWData
{
	/// <summary>The first game's inventory steps: UWArmorItemMap's tables, as before the variant.</summary>
	public sealed class UWInventoryVariantUw1 : IUWInventoryVariant
	{
		public bool FitsBodySlot(UWObject pOItem, UWArmorItemMap.BodySlot peSlot, UWArmorItemMap.BodySlot peMainHand,
			UWObjectClassProperties pOClasses)
		{
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
