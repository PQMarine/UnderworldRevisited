namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STEPS OF THE INVENTORY WHERE THE GAMES PART (UW2 plan phase 3 step 3, 2026-10-29): what goes
	/// into which place of the paperdoll. Both games' ItemFitsSlot (Underworld Exhumed inv/INVPANEL.C) ask
	/// the same: an armour piece by the category of its OBJECTS.DAT row, a ring by its, the hands and
	/// shoulders anything but a stack of weapons in the weapon hand. The first game's port answers from
	/// its own tables (UWArmorItemMap, set up against the original before the class data was read); the
	/// second game's from the class data; the containers' masks likewise (part C of the step).
	/// </summary>
	public interface IUWInventoryVariant
	{
		/// <summary>Whether the item may be put into the place (the weapon hand is pmMainHand).</summary>
		bool FitsBodySlot(UWObject pOItem, UWArmorItemMap.BodySlot peSlot, UWArmorItemMap.BodySlot peMainHand,
			UWObjectClassProperties pOClasses);

		/// <summary>The place an item goes to when it is put on without a place (the armour's own, a
		/// ring's left one, else the right hand).</summary>
		bool TryGetPreferredBodySlot(UWObject pOItem, UWObjectClassProperties pOClasses, out UWArmorItemMap.BodySlot peSlot);

		/// <summary>Whether a container's OBJECTS.DAT mask takes the item: -1 anything, an item id below
		/// 0x200, else 0x200 plus a kind (runes, missiles, scrolls, food, and the second game's keys).</summary>
		bool MatchesContainerMask(int piMask, UWObject pOItem, UWObjectClassProperties pOClasses);
	}
}
