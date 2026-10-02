using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Maps armour object IDs (32-63, ObjectCategoryEnum.ArmorAndClothing) to their
	/// body area and material - for the paperdoll (UWWearables.GetArmor) and the
	/// equip logic (which inventory slot fits which item).
	///
	/// OBJECTS.DAT itself contains no material field (see UWObjectProperties - the
	/// armour table only has protection/durability/an unknown byte/the
	/// paperdoll category), so this is maintained by hand from the item names in string block 4.
	/// Covers only the five body-fitted armour pieces (helmet/torso/
	/// gloves/legs/boots, myItems) - rings/weapon hands/use slots go through
	/// Fits/TryGetPreferredSlot per object category instead of a table, see there.
	///
	/// Important: string block 4 is indexed shifted by one relative to the object ID
	/// (the description of object ID N is at Strings[N+1] - confirmed at tile (24,10)
	/// on level 1: object ID 35 is really "leather leggings" in the original, not ID 36 as
	/// assumed in the first dump attempt). The original table was therefore one
	/// too high - already corrected here (keys = real object ID, not string index).
	/// </summary>
	public static class UWArmorItemMap
	{
		public enum BodySlot
		{
			Helmet,
			Chest,
			Gloves,
			Legs,
			Boots,

			// New (2026-08-25): no body-fitted paperdoll sprite as for the
			// five armour pieces above - they show their own item icon instead,
			// just like a backpack slot. See Fits/TryGetPreferredSlot instead of the
			// armour table myItems.
			LeftRing,
			RightRing,

			// Four equivalent hand slots (confirmed in the original: everything that fits in the
			// backpack also fits in a hand) - RightHandSlot/LeftHandSlot are
			// the front ones (weapon/shield), RightOffHandSlot/LeftOffHandSlot the two
			// circles next to the head (e.g. light sources). Which of the four is the "active"
			// combat hand follows the character's handedness (UWInventoryModel.MainHandSlot,
			// PLAYER.DAT 0x64 bit 0).
			RightHandSlot,
			LeftHandSlot,
			RightOffHandSlot,
			LeftOffHandSlot
		}

		public struct Entry
		{
			public BodySlot Slot;
			public UWWearables.ArmorTypes RenderType;
			public UWWearables.ArmorMaterials Material;

			public Entry(BodySlot peSlot, UWWearables.ArmorTypes peRenderType, UWWearables.ArmorMaterials peMaterial)
			{
				Slot = peSlot;
				RenderType = peRenderType;
				Material = peMaterial;
			}
		}

		private static readonly Dictionary<int, Entry> myItems = new Dictionary<int, Entry>
		{
			// Torso: 32 leather vest, 33 chain shirt, 34 breastplate
			{ 32, new Entry(BodySlot.Chest, UWWearables.ArmorTypes.Chest, UWWearables.ArmorMaterials.Leather) },
			{ 33, new Entry(BodySlot.Chest, UWWearables.ArmorTypes.Chest, UWWearables.ArmorMaterials.Chain) },
			{ 34, new Entry(BodySlot.Chest, UWWearables.ArmorTypes.Chest, UWWearables.ArmorMaterials.Plate) },

			// Legs: 35 leather leggings, 36 chain leggings, 37 plate leggings
			{ 35, new Entry(BodySlot.Legs, UWWearables.ArmorTypes.Legs, UWWearables.ArmorMaterials.Leather) },
			{ 36, new Entry(BodySlot.Legs, UWWearables.ArmorTypes.Legs, UWWearables.ArmorMaterials.Chain) },
			{ 37, new Entry(BodySlot.Legs, UWWearables.ArmorTypes.Legs, UWWearables.ArmorMaterials.Plate) },

			// Gloves: 38 leather, 39 chain, 40 plate
			{ 38, new Entry(BodySlot.Gloves, UWWearables.ArmorTypes.Gloves, UWWearables.ArmorMaterials.Leather) },
			{ 39, new Entry(BodySlot.Gloves, UWWearables.ArmorTypes.Gloves, UWWearables.ArmorMaterials.Chain) },
			{ 40, new Entry(BodySlot.Gloves, UWWearables.ArmorTypes.Gloves, UWWearables.ArmorMaterials.Plate) },

			// Boots: 41 leather, 42 chain, 43 plate, 47 dragon skin (own paperdoll
			// graphic UWWearables.ArmorTypes.SpecialBoots, material only needed for GetArmor
			// and without visible effect for this special type - Leather is a guess).
			{ 41, new Entry(BodySlot.Boots, UWWearables.ArmorTypes.Boots, UWWearables.ArmorMaterials.Leather) },
			{ 42, new Entry(BodySlot.Boots, UWWearables.ArmorTypes.Boots, UWWearables.ArmorMaterials.Chain) },
			{ 43, new Entry(BodySlot.Boots, UWWearables.ArmorTypes.Boots, UWWearables.ArmorMaterials.Plate) },
			{ 47, new Entry(BodySlot.Boots, UWWearables.ArmorTypes.SpecialBoots, UWWearables.ArmorMaterials.Leather) },

			// Helmet: 44 leather cap, 45 chain cowl, 46 helmet (plate).
			//
			// 48 to 50 are all three called "a_crown", but have THREE DIFFERENT images at
			// the special slots 61, 62 and 63: a narrow circlet, the large crown, a
			// second circlet. Until 2026-09-01 all three showed the large crown.
			//
			// Object 48 is proven: the user photographed a head with the narrow
			// circlet in the original, and the matching save game has exactly 48 in the
			// helmet slot. For 49 and 50 the order is inferred from that, not proven.
			// Material as with SpecialBoots without visible effect - Plate is a guess.
			{ 44, new Entry(BodySlot.Helmet, UWWearables.ArmorTypes.Helmet, UWWearables.ArmorMaterials.Leather) },
			{ 45, new Entry(BodySlot.Helmet, UWWearables.ArmorTypes.Helmet, UWWearables.ArmorMaterials.Chain) },
			{ 46, new Entry(BodySlot.Helmet, UWWearables.ArmorTypes.Helmet, UWWearables.ArmorMaterials.Plate) },
			{ 48, new Entry(BodySlot.Helmet, UWWearables.ArmorTypes.SpecialBand1, UWWearables.ArmorMaterials.Plate) },
			{ 49, new Entry(BodySlot.Helmet, UWWearables.ArmorTypes.Crown, UWWearables.ArmorMaterials.Plate) },
			{ 50, new Entry(BodySlot.Helmet, UWWearables.ArmorTypes.SpecialBand2, UWWearables.ArmorMaterials.Plate) },
		};

		public static bool TryGet(int piObjectId, out Entry pOEntry)
		{
			return myItems.TryGetValue(piObjectId, out pOEntry);
		}

		// Identified by the user from the exported object icons (ring-shaped
		// graphic) - 55 deliberately left out, does not belong according to the user.
		private static readonly HashSet<int> myRingIds = new HashSet<int> { 51, 52, 53, 54, 56, 57, 58 };

		/// <summary>
		/// Does an item fit a particular equipment slot? For the five
		/// body-fitted armour pieces (helmet/torso/gloves/legs/boots) an exact
		/// 1:1 mapping via myItems. The four hand slots, however, accept ANY item
		/// (confirmed in the original: everything that fits in the backpack also fits in a
		/// hand) - hence checked BEFORE the myItems check, otherwise e.g. a helmet could
		/// not be put into a hand just because it happens to fit Helmet as well.
		/// Rings check myRingIds last.
		/// </summary>
		public static bool Fits(UWObject pOItem, BodySlot peSlot)
		{
			if (pOItem == null)
				return false;

			if (fIsHandSlot(peSlot))
				return true;

			if (myItems.TryGetValue(pOItem.ID, out Entry lOEntry))
				return lOEntry.Slot == peSlot;

			switch (peSlot)
			{
				case BodySlot.LeftRing:
				case BodySlot.RightRing:
					return myRingIds.Contains(pOItem.ID);
				default:
					return false;
			}
		}

		private static bool fIsHandSlot(BodySlot peSlot)
		{
			return peSlot == BodySlot.RightHandSlot
				|| peSlot == BodySlot.LeftHandSlot
				|| peSlot == BodySlot.RightOffHandSlot
				|| peSlot == BodySlot.LeftOffHandSlot;
		}

		/// <summary>
		/// For equipping by click/auto (UWInventory.TryAutoEquip), where (unlike
		/// deliberately dragging onto a particular slot) no slot is given. The five
		/// armour pieces have only one possible slot anyway, rings prefer their
		/// left slot. Everything else (weapons, light sources, really everything else - see
		/// Fits) lands in RightHandSlot by default; which of the four hands is actually
		/// meant is decided in the original only by deliberately dragging onto a
		/// particular slot.
		/// </summary>
		public static bool TryGetPreferredSlot(UWObject pOItem, out BodySlot peSlot)
		{
			peSlot = BodySlot.RightHandSlot;

			if (pOItem == null)
				return false;

			if (myItems.TryGetValue(pOItem.ID, out Entry lOEntry))
			{
				peSlot = lOEntry.Slot;
				return true;
			}

			if (myRingIds.Contains(pOItem.ID))
				peSlot = BodySlot.LeftRing;

			return true;
		}
	}
}
