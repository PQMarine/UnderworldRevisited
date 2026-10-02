using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// What a creature carries (the reference: npc/npcloot.cs).
	///
	/// Four groups, in this order: something valuable, something edible, up to two
	/// pieces of equipment, up to two other things. Everything is in the creature table
	/// of OBJECTS.DAT and is populated there so plausibly that the field mapping
	/// confirms itself (checked 2026-09-10):
	///
	///   Goblin       club, leather cap, sling, sling stone, meat
	///   Skeleton     shortsword, leather cap, leather boots
	///   Fighter      longsword, chain boots, torch, lantern, bread
	///   Mage         dagger, flute, runestones, cheese
	///   Troll        skull, bone, meat
	///   Stone golem  boulder - and no money
	///
	/// IN THE ORIGINAL THE LOOT SITS IN THE CREATURE'S BELLY: it is generated as soon as
	/// someone looks inside (trade) or the creature dies, and is then dumped into the world.
	/// We do the same: trading rolls it (UWConversationTrade), otherwise it is rolled on
	/// death - see UWCritterRemains.
	/// </summary>
	public static class UWCritterLoot
	{
		/// <summary>First valuable: the coin. Then follow gold coin, ruby,
		/// red gem, small blue gem, large blue gem, sapphire.</summary>
		public const int FirstValuableObjectId = 0xA0;

		/// <summary>First food. The table value counts from here.</summary>
		public const int FirstFoodObjectId = 0xB0;

		/// <summary>All loot rolls are against zero to fifteen (sixteen values).</summary>
		private const int ProbabilityRange = 16;

		/// <summary>One piece of loot, ready to be placed.</summary>
		public struct Drop
		{
			public int ObjectId;

			/// <summary>Item count, or zero for a single item without a quantity field.
			/// </summary>
			public int Quantity;

			/// <summary>Condition, or minus one for "unchanged".</summary>
			public int Quality;
		}

		/// <summary>
		/// Rolls the complete loot of a creature.
		///
		/// piDungeonLevel affects two things: which valuables occur at all
		/// (better ones further down) and how well preserved equipment is.
		/// </summary>
		public static List<Drop> Generate(UWObjectClassProperties.Critter pOCritter,
			UWCommonObjectProperties pOCommon, int piDungeonLevel, UWObjectProperties pORanged = null)
		{
			List<Drop> lODrops = new List<Drop>();

			fAddValuable(pOCritter, pOCommon, piDungeonLevel, lODrops);
			fAddFood(pOCritter, pOCommon, lODrops);
			fAddWeapons(pOCritter, pOCommon, pORanged, piDungeonLevel, lODrops);
			fAddOther(pOCritter, pOCommon, piDungeonLevel, lODrops);

			return lODrops;
		}

		/// <summary>Condition of a freshly created object (UW.EXE PrepareNewObjectProps:
		/// quality 0x28). Food and valuables keep it.</summary>
		private const int NewObjectQuality = 0x28;

		/// <summary>Identifier of physical missiles in the ranged weapon table.</summary>
		private const int PhysicalMissileType = 0xC0;

		/// <summary>Quantity of a freshly created object: 1 if its quantity class
		/// provides for it, otherwise none (UWCommonObjectProperties.StartsWithQuantity).</summary>
		private static int fNewQuantity(UWCommonObjectProperties pOCommon, int piObjectId)
		{
			UWCommonObjectProperties.Entry lOEntry;

			return pOCommon != null && pOCommon.TryGet(piObjectId, out lOEntry) && lOEntry.StartsWithQuantity ? 1 : 0;
		}

		/// <summary>
		/// Coins and gems.
		///
		/// HOW MANY DEPENDS ON THE VALUE of the item: coins come by the handful,
		/// gems singly. The reference converts the money value from COMOBJ.DAT into
		/// a comparison number and rolls against it - the more expensive, the less likely
		/// more than one. Checked against the numbers: a coin (value 3) drops one to
		/// twelve times with a multiple value of five, a ruby (value 25) practically
		/// always singly.
		///
		/// WHICH valuable depends on the LEVEL: the roll shifts upwards with
		/// depth, so better things drop further down. On level one it is almost
		/// always the coin.
		///
		/// REWRITTEN FROM UW.EXE (SpawnValuableLoot_ovr150_15A, 2026-09-16). The version
		/// before was copied from the reference and deviated from the original in four
		/// places:
		/// (a) the conversion runs in the BYTE register al and is read back sign-extended
		///     (cbw), so everything above 127 comes out NEGATIVE. With the values of uw1
		///     that hits ruby, large blue gem, sapphire, emerald and amulet - and it is
		///     no accident: a negative comparison number always takes the dice branch, whose
		///     range then falls to zero, and the dice roll returns its plain die count, so
		///     exactly ONE piece drops. Expensive valuables therefore drop singly, coins by
		///     the handful.
		/// (b) the middle step is (value &lt;&lt; 2) + 0xEC, not (0xEC + value) &lt;&lt; 2.
		/// (c) the lowest step uses the VALUE, not the valuable's index.
		/// (d) which branch is taken is decided by four times the multiple against the
		///     comparison number, and the single roll succeeds when the random number falls
		///     BELOW four times the multiple - the reference had that the other way round,
		///     which made cheap valuables rare and expensive ones common.
		/// </summary>
		private static void fAddValuable(UWObjectClassProperties.Critter pOCritter,
			UWCommonObjectProperties pOCommon, int piDungeonLevel, List<Drop> pODrops)
		{
			if (UWRandom.Next(0, ProbabilityRange) >= pOCritter.ValuableProbability)
				return;

			int liSpan = 40 - (piDungeonLevel * 3);
			int liShift = 33 - (piDungeonLevel * 3);

			if (liSpan < 1)
				liSpan = 1;

			int liType = UWRandom.Next(0, liSpan) - liShift;

			if (liType < 0)
				liType = 0;

			int liValue = 1;

			UWCommonObjectProperties.Entry lOEntry;

			// UW.EXE reads a single BYTE out of the COMOBJ record (the table starts at the
			// coin's monetary value and steps by the record length of eleven).
			if (pOCommon != null && pOCommon.TryGet(FirstValuableObjectId + liType, out lOEntry)
				&& (lOEntry.Value & 0xFF) > 0)
				liValue = lOEntry.Value & 0xFF;

			// UW.EXE computes in al and reads the result back with cbw - see the note (a)
			// above, which is why the cast to sbyte belongs here.
			if (liValue >= 12)
				liValue = (sbyte)((liValue << 3) + 0xBC);
			else if (liValue >= 8)
				liValue = (sbyte)((liValue << 2) + 0xEC);
			else if (liValue >= 4)
				liValue = (sbyte)((liValue << 1) + 0xFC);

			int liMultiple = pOCritter.ValuableMultiple << 2;
			int liCount = 0;

			if (liMultiple < liValue)
			{
				// Probability liMultiple / liValue - the dearer the piece, the rarer.
				if (UWRandom.Next(0, liValue) < liMultiple)
					liCount = 1;
			}
			else
			{
				// A negative comparison number gives a range of zero or less; fRollDice then
				// returns its die count, four, which after the shift means exactly one piece.
				liCount = UWRandom.RollDice(4, (liValue == 0 ? 0 : liMultiple / liValue) << 1) >> 2;
			}

			if (liCount > 0)
				pODrops.Add(new Drop
				{
					ObjectId = FirstValuableObjectId + liType,
					Quantity = liCount,
					Quality = NewObjectQuality
				});
		}

		/// <summary>Food: condition and quantity like a freshly created object (UW.EXE
		/// SpawnFoodLoot_ovr150_2BB sets nothing afterwards).</summary>
		private static void fAddFood(UWObjectClassProperties.Critter pOCritter,
			UWCommonObjectProperties pOCommon, List<Drop> pODrops)
		{
			if (UWRandom.Next(0, ProbabilityRange) >= pOCritter.FoodProbability)
				return;

			int liId = FirstFoodObjectId + pOCritter.FoodItemIndex;

			pODrops.Add(new Drop
			{
				ObjectId = liId,
				Quantity = fNewQuantity(pOCommon, liId),
				Quality = NewObjectQuality
			});
		}

		/// <summary>Equipment drops without a roll of its own - if it is in the table, it is
		/// there. Its condition is rolled; physical ammunition comes in stacks of 4 to 11
		/// (UW.EXE SpawnArmsLoot_ovr150_321).</summary>
		private static void fAddWeapons(UWObjectClassProperties.Critter pOCritter,
			UWCommonObjectProperties pOCommon, UWObjectProperties pORanged, int piDungeonLevel, List<Drop> pODrops)
		{
			if (pOCritter.WeaponLoot == null)
				return;

			foreach (int liObjectId in pOCritter.WeaponLoot)
			{
				if (liObjectId < 0)
					continue;

				int liQuantity = fNewQuantity(pOCommon, liObjectId);

				if (((liObjectId & 0x30) >> 4) == 1 && pORanged != null
					&& pORanged.GetRangedType(liObjectId) == PhysicalMissileType)
					liQuantity = 4 + UWRandom.Next(0, 8);

				pODrops.Add(new Drop
				{
					ObjectId = liObjectId,
					Quantity = liQuantity,
					Quality = fRollQuality(piDungeonLevel)
				});
			}
		}

		private static void fAddOther(UWObjectClassProperties.Critter pOCritter,
			UWCommonObjectProperties pOCommon, int piDungeonLevel, List<Drop> pODrops)
		{
			if (pOCritter.OtherLoot == null)
				return;

			foreach (UWObjectClassProperties.CritterLootEntry lOEntry in pOCritter.OtherLoot)
			{
				if (lOEntry.Probability <= 0
					|| UWRandom.Next(0, ProbabilityRange) >= lOEntry.Probability)
					continue;

				// NO special value 0x40 for items that do not wear out: only the reference sets it
				// (npcloot.OtherLoot), UW.EXE SpawnOtherLoot_ovr150_442 always rolls. The
				// wood remains of a reaper have condition 21 to 40 and quantity 1 in the original,
				// ours had 63 without quantity (per user, compared on level 5, 2026-09-13).
				pODrops.Add(new Drop
				{
					ObjectId = lOEntry.ObjectId,
					Quantity = fNewQuantity(pOCommon, lOEntry.ObjectId),
					Quality = fRollQuality(piDungeonLevel)
				});
			}
		}

		/// <summary>
		/// The condition of a loot item: in half of the cases pure chance, otherwise
		/// by depth - further down the equipment is better preserved.
		/// </summary>
		private static int fRollQuality(int piDungeonLevel)
		{
			// UW.EXE: on an even roll by depth, otherwise random mod 64 (including 0).
			if (UWRandom.Next(0, 2) != 0)
				return UWRandom.Next(0, 0x40);

			return (UWRandom.Next(0, System.Math.Max(1, piDungeonLevel << 2)) + (piDungeonLevel << 2)) & 0x3F;
		}
	}
}
