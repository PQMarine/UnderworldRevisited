namespace UWDataImport.UWData
{
	/// <summary>
	/// Which pictures the paper doll shows: the body by the player data, and for each armour slot
	/// the material, condition and type of what is worn there (cleanup step 4.6, 2026-10-18; part
	/// of UWCharacter until then, the textures are UWPaperdollView's).
	/// </summary>
	public static class UWPaperdollRules
	{
		/// <summary>The body picture when nothing better is known (the Inspector's default).</summary>
		public const int FallbackBodyIndex = 9;

		/// <summary>How many body pictures BODIES.BYT has: five types, each for a man and a woman.
		/// </summary>
		public const int BodyCount = 10;

		/// <summary>The body types per sex.</summary>
		private const int BodyTypes = 5;

		/// <summary>
		/// The body picture. THE BODY TYPE COMES FROM THE SAVE GAME, if one is loaded. Until
		/// 2026-09-07 only the fixed value from the Inspector was used, and the paper doll therefore
		/// ALWAYS showed the same figure - regardless of whom you had loaded (noticed by the user:
		/// "Or have we never loaded the right image?" - that was exactly the case).
		///
		/// The composition is in the reference (uimanager_paperdoll.SetBody): body type zero to
		/// four, plus five for a woman. Both sit in the same byte as the character class (see
		/// UWPlayerData). piInspectorIndex is kept without loaded player data; out of range gives
		/// the fallback.
		/// </summary>
		public static int GetBodyIndex(UWPlayerData pOPlayer, int piInspectorIndex)
		{
			int liIndex = piInspectorIndex;

			if (pOPlayer != null && pOPlayer.IsLoaded)
			{
				int liBody = pOPlayer.Body < 0 ? 0 : (pOPlayer.Body > BodyTypes - 1 ? BodyTypes - 1 : pOPlayer.Body);

				liIndex = liBody + (pOPlayer.IsFemale ? BodyTypes : 0);
			}

			if (liIndex > BodyCount - 1 || liIndex < 0)
				liIndex = FallbackBodyIndex;

			return liIndex;
		}

		/// <summary>The figure's sex by its body picture: the first five are men.</summary>
		public static UWWearables.Sex GetSex(int piBodyIndex)
		{
			return piBodyIndex < BodyTypes ? UWWearables.Sex.Male : UWWearables.Sex.Female;
		}

		/// <summary>
		/// Maps the quality of an armour piece (0 to 63) to one of the four
		/// paper doll conditions.
		///
		/// According to the user, the original has its own sprite for each condition (2026-09-01).
		///
		/// QUARTERS OF THE QUALITY RANGE, quality >> 4: 0-15, 16-31, 32-47, 48-63 (the reference
		/// computes the frame as (quality / 16) * 15 plus the item's class index). Until 2026-09-17
		/// we mapped the six condition-word levels onto the four images, which put a breastplate
		/// at quality 20 on the damaged image; in the original it is worn (per user).
		/// </summary>
		public static UWWearables.ArmorConditions GetArmorCondition(int piQuality)
		{
			int liQuarter = piQuality >> 4;

			switch (liQuarter < 0 ? 0 : (liQuarter > 3 ? 3 : liQuarter))
			{
				case 0: return UWWearables.ArmorConditions.Damaged;
				case 1: return UWWearables.ArmorConditions.Worn;
				case 2: return UWWearables.ArmorConditions.Servicable;
				default: return UWWearables.ArmorConditions.Excellent;
			}
		}

		/// <summary>What one armour slot of the paper doll shows.</summary>
		public struct SlotLook
		{
			public UWWearables.ArmorMaterials Material;

			public UWWearables.ArmorConditions Condition;

			/// <summary>None when nothing is worn there.</summary>
			public UWWearables.ArmorTypes Type;

			/// <summary>How many rows lower the picture is drawn (UWWearables.GetArmorDrop).</summary>
			public int Drop;
		}

		/// <summary>
		/// The look of a slot from the actually equipped item - colour/condition/slot via
		/// UWArmorItemMap. If nothing is equipped (or it is no armour), the slot shows no armour
		/// (ArmorTypes.None).
		/// </summary>
		public static SlotLook GetSlotLook(UWObject pOItem, UWWearables.Sex peSex)
		{
			SlotLook lOLook;

			if (pOItem != null && UWArmorItemMap.TryGet(pOItem.ID, out UWArmorItemMap.Entry lOEntry))
			{
				lOLook.Material = lOEntry.Material;
				lOLook.Type = lOEntry.RenderType;

				// The condition comes from the item's quality. An earlier attempt showed
				// wrong pieces; the bug was in GetArmor, which withheld the condition offset
				// from boots and helmets. That note said "fixed since", but GetArmor still tested
				// "> Gloves" until 2026-09-17, when a repaired leather cap kept its old image (per user).
				lOLook.Condition = GetArmorCondition(pOItem.Quality);
			}
			else
			{
				// Nothing equipped = nothing visible, the same for all 5 slots. (The old
				// Inspector preview fields HelmetType/LegsType/... from before this function
				// made the paper doll's legs look inconsistent with the empty slots; they were
				// unused and are gone since 2026-09-18.)
				lOLook.Type = UWWearables.ArmorTypes.None;
				lOLook.Material = UWWearables.ArmorMaterials.Leather;
				lOLook.Condition = UWWearables.ArmorConditions.Servicable;
			}

			lOLook.Drop = lOLook.Type == UWWearables.ArmorTypes.None ? 0
				: UWWearables.GetArmorDrop(peSex, lOLook.Material, lOLook.Type);

			return lOLook;
		}
	}
}
