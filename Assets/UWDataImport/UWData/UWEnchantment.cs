using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The suffix an enchantment appends to an item's name - that is how
	/// "a_shiny sword" becomes the Sword of Justice.
	///
	/// WHERE THE ENCHANTMENT IS STORED
	///
	/// In the field after the owner, i.e. the same word that otherwise holds the quantity
	/// (for us UWObject.Quantity). Two cases:
	///
	///   Quantity bit set: the value is stored right there, provided the enchantment bit
	///   is set as well.
	///
	///   Quantity bit not set: the value points to another object. From there the
	///   chain is searched for a spell object (Id 288); its own field then
	///   carries the enchantment and its quality the number of charges. This is how
	///   wands do it, which need both at the same time.
	///
	/// HOW A NAME IS MADE FROM IT
	///
	/// The value splits into a major and a minor class, and how it splits depends
	/// on the third flag bit of the carrying object. From that a number in string block 6
	/// is computed - the block of spell names.
	///
	/// Source: uw-formats.txt 4.3.1 describes the mapping only in prose and itself admits
	/// uncertainties. The calculation implemented here comes from UnderworldGodot
	/// (MIT), src/magic/MagicEnchantment.cs, which is disassembled from the original.
	///
	/// NOT PART OF THIS CLASS: the lore check that decides in the original whether the player
	/// recognises the enchantment at all - that is UWLoreCheck, applied by the caller; this
	/// class always returns the name.
	///
	/// NO SPECIAL CASE FOR POISONED POTIONS (settled 2026-09-24): the reference names one "of
	/// poison", but MagicItemDescription_ovr122_3AC has no such branch - its enchantment lookup
	/// searches the chain for a spell object only, finds none behind the poisoned potion's damage
	/// trap and reports nothing, so the original shows the plain "a red potion". The one poisoned
	/// potion in the game lies on level 2 at 14/51 (damage trap, quality 10, owner 1).
	/// </summary>
	public static class UWEnchantment
	{
		/// <summary>Object id of the spell object to which wands attach their enchantment.</summary>
		public const int SpellObjectId = UWObjectMechanics.SpellObjectId;

		/// <summary>String block of the spell names. The reference addresses it as 0xC00 and
		/// divides by 512 - which gives exactly this block.</summary>
		public const int SpellStringBlock = 6;

		/// <summary>Traps and triggers never carry an enchantment.</summary>
		private const int TrapMajorClass = UWObjectMechanics.TrapMajorClass;

		/// <summary>Major class 9 means cursed - then there is no name suffix.</summary>
		private const int CursedMajorClass = 9;

		public struct Result
		{
			/// <summary>The bare name of the enchantment, e.g. "Very Great Damage" - without
			/// the connecting word. The caller adds the " of ", because a curse is attached
			/// differently. Empty if there is no enchantment.</summary>
			public string Name;

			/// <summary>Cursed. In the original the word stands BEFORE the item name,
			/// not as an "of" suffix after it.</summary>
			public bool IsCursed;

			/// <summary>Remaining charges, or -1 if the item has none.</summary>
			public int Charges;

			/// <summary>
			/// An enchantment is attached at all - regardless of whether we find a
			/// name for it.
			///
			/// Needed for the middle tier of the lore check (see UWLoreCheck): there
			/// only "magical" stands before the name, and for that it is enough to know THAT
			/// something is on it.
			/// </summary>
			public bool HasCarrier;

			public bool HasEnchantment => !string.IsNullOrEmpty(Name) || IsCursed;
		}

		/// <summary>
		/// Determines the name suffix. pOObjectList is the object list the item
		/// is in - for the inventory the record list of the save game, in the world the level's
		/// master list.
		/// </summary>
		public static Result Get(UWObject pOItem, List<UWObject> pOObjectList, UWStrings pOStrings)
		{
			Result lOResult = new Result { Charges = -1 };

			if (pOItem == null || pOStrings == null)
				return lOResult;

			int liCharges = -1;

			UWObject lOCarrier = fFindCarrier(pOItem, pOObjectList, ref liCharges);

			if (lOCarrier == null)
				return lOResult;

			lOResult.HasCarrier = true;

			int liValue = lOCarrier.Quantity;
			bool lbFlag2 = ((lOCarrier.Flags >> 2) & 1) != 0;

			int liMajor;
			int liMinor;

			if (lbFlag2)
			{
				liMajor = (liValue & 0x1FF) >> 6;
				liMajor = liMajor == 0 ? -1 : liMajor + 0xC;
				liMinor = liValue & 0x3F;
			}
			else
			{
				liMajor = (liValue & 0x1FF) >> 4;
				liMinor = liValue & 0xF;
			}

			lOResult.IsCursed = liMajor == CursedMajorClass;

			if (!lOResult.IsCursed)
				lOResult.Name = fGetName(pOItem, liMajor, liMinor, lbFlag2, pOStrings);

			lOResult.Charges = liCharges;

			return lOResult;
		}

		/// <summary>
		/// Which object carries the enchantment: the item itself or a chained
		/// spell. Returns null if there is none.
		/// </summary>
		private static UWObject fFindCarrier(UWObject pOItem, List<UWObject> pOObjectList, ref int piCharges)
		{
			if ((pOItem.ID >> 6) == TrapMajorClass)
				return null;

			if (pOItem.HasQuantity)
			{
				// No enchantment bit, or an item of class 5 - there the field
				// means something else.
				if (!pOItem.IsEnchanted || (pOItem.ID >> 6) == 5)
					return null;

				return pOItem;
			}

			if (pOItem.Quantity == 0 || pOObjectList == null)
				return null;

			UWObject lOSpell = fFindSpellInChain(pOItem.Quantity, pOObjectList);

			if (lOSpell == null)
				return null;

			piCharges = lOSpell.Quality;

			return lOSpell;
		}

		/// <summary>Follows the object chain from the given index and looks for a
		/// spell object.</summary>
		private static UWObject fFindSpellInChain(int piStartIndex, List<UWObject> pOObjectList)
		{
			int liIndex = piStartIndex;

			// Upper limit against a chain running in a circle in a damaged file.
			for (int liStep = 0; liStep < 256; liStep++)
			{
				if (liIndex <= 0 || liIndex >= pOObjectList.Count)
					return null;

				UWObject lOCurrent = pOObjectList[liIndex];

				if (lOCurrent == null)
					return null;

				if (lOCurrent.ID == SpellObjectId)
					return lOCurrent;

				liIndex = lOCurrent.Link;
			}

			return null;
		}

		private static string fGetName(UWObject pOItem, int piMajor, int piMinor, bool pbFlag2, UWStrings pOStrings)
		{
			int liStringNumber;

			if (piMajor == 0xC)
			{
				// Weapons and armour: accuracy, damage, protection, toughness. Armour
				// is sixteen entries further on.
				liStringNumber = 0x1C0;

				if (((pOItem.ID >> 4) & 0x3) >= 2)
					liStringNumber += 16;

				liStringNumber += piMinor;
			}
			else if (pbFlag2 && piMajor <= 0)
			{
				liStringNumber = 0x100 + piMinor;
			}
			else
			{
				liStringNumber = piMinor + (piMajor << 4);
			}

			try
			{
				// The project's usual +1 on the string index.
				string lsName = pOStrings.Blocks[SpellStringBlock].Strings[liStringNumber + 1];

				return string.IsNullOrWhiteSpace(lsName) ? null : lsName.TrimEnd('\r', '\n');
			}
			catch
			{
				return null;
			}
		}
	}
}
