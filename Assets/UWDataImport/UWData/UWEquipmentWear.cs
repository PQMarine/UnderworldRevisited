namespace UWDataImport.UWData
{
	/// <summary>
	/// Equipment that takes damage in a fight - read 2026-09-28 after the user saw the dagger
	/// lose quality twice in the original (15 to 8) while ours never wore.
	///
	/// CalculateAttackResults_seg022_230E_6B9 hands a piece of equipment to ovr120_C0F on three
	/// branches, each time with bash damage (type 4) and debris for a destroyed piece:
	///
	///   CRITICAL FAILURE of the PLAYER against a creature whose table byte 0x0A bit 0 is clear:
	///     the weapon hand (slot 8 minus the handedness bit) takes 2d3.
	///   The PLAYER strikes a DOOR (0x140-0x14F): when RNG * 12 / 0x8000 is below twice the
	///     door's id and 7, the weapon hand takes 2d4.
	///   A CRITICAL HIT on the PLAYER: the body part hit picks the slot (see
	///     UWCritterCombat.GetCriticalEquipmentSlot: the head the helmet, body and hands the
	///     shield hand, the legs the leggings and one time in five the boots); that slot takes
	///     2d4, through the ARMOUR test of ovr120_C0F (mode 1), not the weapon test.
	///
	/// ovr120_C0F: an empty slot, or in the weapon hand no weapon (ids 0x00-0x0F), or in the
	/// shield hand no shield, takes nothing. Otherwise the item takes the damage as any object
	/// does (see DamageObject_seg023_35A and UWObjectDamageRules): its resistances, then the
	/// wear (halved once per quality class, taken off the quality). At 0 the item is gone, a pile of debris lies at the player's feet and the
	/// message reads "Your dagger was destroyed."; a lower quality reads "... was damaged."; no
	/// change, no message. The words come from UW.EXE, not from STRINGS.PAK; a name ending in
	/// "s" takes "were".
	/// </summary>
	public static class UWEquipmentWear
	{
		public const int CriticalFailureDiceCount = 2;

		public const int CriticalFailureDiceRange = 3;

		public const int DoorDiceCount = 2;

		public const int DoorDiceRange = 4;

		public const int CriticalHitDiceCount = 2;

		public const int CriticalHitDiceRange = 4;

		/// <summary>The melee weapons, 0x00-0x0F: the weapon hand wears only with one of them.</summary>
		private const int WeaponIdLimit = 0x10;

		private const int FirstDoorId = 0x140;

		private const int LastDoorId = 0x14F;

		/// <summary>The creature table byte whose bit 0 spares the weapon on a critical failure.</summary>
		private const int SparesWeaponRowByte = 0x0A;

		public enum Outcome
		{
			None,
			Damaged,
			Destroyed
		}

		/// <summary>Whether a critical failure of the player against this creature wears the
		/// weapon.</summary>
		public static bool WearsWeaponOnCriticalFailure(UWObjectClassProperties.Critter pORow)
		{
			return (pORow.RowByte(SparesWeaponRowByte) & 1) == 0;
		}

		/// <summary>Whether a blow of the player against this object wears the weapon - only
		/// a door, and only on the roll; piRoll is the game's RNG, 0 to 0x7FFF.</summary>
		public static bool WearsWeaponOnDoor(int piObjectId, int piRoll)
		{
			if (piObjectId < FirstDoorId || piObjectId > LastDoorId)
				return false;

			return (piRoll * 12) / 0x8000 < (piObjectId & 7) * 2;
		}

		public static bool WearsWeaponOnDoor(int piObjectId)
		{
			return WearsWeaponOnDoor(piObjectId, UWRandom.Next(0, 0x8000));
		}

		/// <summary>Whether the item in the weapon hand can wear: a melee weapon.</summary>
		public static bool IsWearableWeapon(UWObject pOItem)
		{
			return pOItem != null && pOItem.ID < WeaponIdLimit;
		}

		/// <summary>Whether the item a critical hit finds in the slot can wear: in a hand only a
		/// shield, in the helmet, leggings and boots slots what is there.</summary>
		public static bool IsWearableArmour(UWObject pOItem, UWArmorItemMap.BodySlot peSlot)
		{
			if (pOItem == null)
				return false;

			if (peSlot == UWArmorItemMap.BodySlot.LeftHandSlot || peSlot == UWArmorItemMap.BodySlot.RightHandSlot)
				return UWPlayerCritterRow.IsShield(pOItem.ID);

			return true;
		}

		/// <summary>
		/// Wears the item by the damage and writes its new quality. The caller removes a
		/// destroyed item and puts the debris down (UWObjectDamageRules.RollDebrisObjectId).
		/// </summary>
		public static Outcome Wear(UWObject pOItem, int piDamage, DataImport pOData, out string psMessage)
		{
			psMessage = string.Empty;

			if (pOItem == null || pOData == null || pOData.CommonObjectProperties == null
				|| !pOData.CommonObjectProperties.TryGet(pOItem.ID, out UWCommonObjectProperties.Entry lOEntry))
				return Outcome.None;

			int liBefore = pOItem.Quality;
			int liDamage = UWDamageTypes.Scale(lOEntry.Resistances, piDamage, UWDamageTypes.Physical);

			bool lbDestroyed = UWObjectDamageRules.Wear(liBefore, liDamage, lOEntry.QualityClass,
				pOItem.DoorDirection, out int liQuality);

			pOItem.Quality = (ushort)liQuality;

			if (!lbDestroyed && liQuality == liBefore)
				return Outcome.None;

			string lsName = UWItemDescriptions.GetBareName(pOItem.ID, pOData);

			psMessage = "Your " + lsName + (lsName.EndsWith("s") ? " were" : " was")
				+ (lbDestroyed ? " destroyed." : " damaged.");

			return lbDestroyed ? Outcome.Destroyed : Outcome.Damaged;
		}
	}
}
