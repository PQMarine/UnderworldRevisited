namespace UWDataImport.UWData
{
	/// <summary>
	/// The second game's inventory steps, as ItemFitsSlot has them (Underworld Exhumed uw2
	/// inv/INVPANEL.C:496): the five armour places take a hack-class object of minor class 2 or 3 (ids
	/// 0x20 to 0x3F) whose OBJECTS.DAT armour row has the place's category - 8 the head, 1 the body, 4 the
	/// gloves, 3 the legs, 5 the feet -, the rings category 9; the weapon hand refuses a stack of more than
	/// one melee weapon; the other hands and shoulders take anything. Food in the head's place (eating it)
	/// and the lights' lit and unlit ids come with the second game's rules (phase 4).
	/// </summary>
	public sealed class UWInventoryVariantUw2 : IUWInventoryVariant
	{
		public const int FirstArmourId = 0x20;

		public const int LastArmourId = 0x3F;

		public const int LastMeleeWeaponId = 0x0F;

		public const int BodyCategory = 1;

		public const int LegsCategory = 3;

		public const int GlovesCategory = 4;

		public const int FeetCategory = 5;

		public const int HeadCategory = 8;

		public const int RingCategory = 9;

		public bool FitsBodySlot(UWObject pOItem, UWArmorItemMap.BodySlot peSlot, UWArmorItemMap.BodySlot peMainHand,
			UWObjectClassProperties pOClasses)
		{
			if (pOItem == null)
				return false;

			int liCategory = fCategory(pOItem, pOClasses);

			switch (peSlot)
			{
				case UWArmorItemMap.BodySlot.Helmet:
					return liCategory == HeadCategory;
				case UWArmorItemMap.BodySlot.Chest:
					return liCategory == BodyCategory;
				case UWArmorItemMap.BodySlot.Gloves:
					return liCategory == GlovesCategory;
				case UWArmorItemMap.BodySlot.Legs:
					return liCategory == LegsCategory;
				case UWArmorItemMap.BodySlot.Boots:
					return liCategory == FeetCategory;
				case UWArmorItemMap.BodySlot.RightRing:
				case UWArmorItemMap.BodySlot.LeftRing:
					return liCategory == RingCategory;
			}

			// The weapon hand: no stack of melee weapons (INVPANEL.C:569-572).
			if (peSlot == peMainHand && pOItem.ID <= LastMeleeWeaponId && UWStackRules.Count(pOItem) > 1)
				return false;

			return true;
		}

		public bool TryGetPreferredBodySlot(UWObject pOItem, UWObjectClassProperties pOClasses, out UWArmorItemMap.BodySlot peSlot)
		{
			peSlot = UWArmorItemMap.BodySlot.RightHandSlot;

			if (pOItem == null)
				return false;

			switch (fCategory(pOItem, pOClasses))
			{
				case HeadCategory:
					peSlot = UWArmorItemMap.BodySlot.Helmet;
					break;
				case BodyCategory:
					peSlot = UWArmorItemMap.BodySlot.Chest;
					break;
				case GlovesCategory:
					peSlot = UWArmorItemMap.BodySlot.Gloves;
					break;
				case LegsCategory:
					peSlot = UWArmorItemMap.BodySlot.Legs;
					break;
				case FeetCategory:
					peSlot = UWArmorItemMap.BodySlot.Boots;
					break;
				case RingCategory:
					peSlot = UWArmorItemMap.BodySlot.LeftRing;
					break;
			}

			return true;
		}

		/// <summary>The masks' kinds above 0x200 (object.h CONT_RUNES to CONT_KEYS).</summary>
		public const int RunesMask = 0x200;

		public const int MissilesMask = 0x201;

		public const int ScrollsMask = 0x202;

		public const int FoodMask = 0x203;

		public const int KeysMask = 0x204;

		/// <summary>The drinks the food mask leaves out, and what it takes besides the food class (items.h:
		/// the bottles of ale, water and wine; two plants, a third, the lump of wax, the stick, a bone).</summary>
		private static readonly int[] msNotFood = { 0xBB, 0xBC, 0xBD };

		private static readonly int[] msFoodExtras = { 0xCE, 0xCF, 0x114, 0xD2, 0x13E, 0xC5 };

		/// <summary>
		/// ItemFitsSlot's masks (Underworld Exhumed uw2 inv/INVPANEL.C:610-645): an id below 0x200 is that
		/// item alone; the rune bag takes runestones (major 3, minor 3, or minor 2 above class 7); the key
		/// ring keys (major 4, minor 0, not class 0) and a container that is itself a key ring; the quiver
		/// sling stones, bolts and arrows (major 0, minor 1, class below 3) and wands (major 2, minor 1, class
		/// 8 and up); the scroll case scrolls of classes 4 to 7, 9 and 10 (major 4, minor 3); the food mask the
		/// food class but the three drinks, and six more by id.
		/// </summary>
		public bool MatchesContainerMask(int piMask, UWObject pOItem, UWObjectClassProperties pOClasses)
		{
			if (pOItem == null)
				return false;

			int liId = pOItem.ID;

			// The mask read as an unsigned word: 0xFFFF is the original's -1, anything.
			if (piMask == UWObjectClassProperties.ContainerMaskAll || piMask < 0)
				return true;

			if (piMask < RunesMask)
				return liId == piMask;

			int liMajor = UWObjectIds.Major(liId);
			int liMinor = UWObjectIds.Minor(liId);
			int liClass = liId & 0xF;

			switch (piMask)
			{
				case RunesMask:
					return liMajor == 3 && (liMinor == 3 || (liMinor == 2 && liClass > 7));
				case KeysMask:
					if (liMajor == 4 && liMinor == 0 && liClass != 0)
						return true;

					return pOClasses != null && pOClasses.TryGetContainer(liId, out UWObjectClassProperties.Container lOInner)
						&& lOInner.ObjectMask == KeysMask;
				case MissilesMask:
					return (liMajor == 0 && liMinor == 1 && liClass < 3) || (liMajor == 2 && liMinor == 1 && liClass >= 8);
				case ScrollsMask:
					return liMajor == 4 && liMinor == 3 && ((liClass >= 4 && liClass < 8) || liClass == 9 || liClass == 10);
				case FoodMask:
					return (liMajor == 2 && liMinor == 3 && System.Array.IndexOf(msNotFood, liId) < 0)
						|| System.Array.IndexOf(msFoodExtras, liId) >= 0;
			}

			return false;
		}

		/// <summary>The armour row's category of an armour id, -1 for anything else (get_class_data's
		/// byte 3).</summary>
		private static int fCategory(UWObject pOItem, UWObjectClassProperties pOClasses)
		{
			if (pOClasses == null || pOItem.ID < FirstArmourId || pOItem.ID > LastArmourId)
				return -1;

			return pOClasses.TryGetWearable(pOItem.ID, out UWObjectClassProperties.Wearable lOWearable) ? lOWearable.PaperdollSlot : -1;
		}
	}
}
