using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S ENCHANTMENT SPELL (UW2 plan G1 part D2b, 2026-11-12; Underworld Exhumed uw2 combat/SPELLS2.C
	/// sp_enchant, chargeable, charge_object, obj/OBJUSE.C decode_obj_spell and useNSpellCharges, combat/DAMAGE.C debris_type):
	/// class 7 minor 14 on a clicked thing, in the world or carried.
	///
	///   the thing                                    what happens
	///   carries a cast-on-use spell (a wand)         chargeable: recharged by 1 + Casting / 15, or with the chance
	///                                                ((16 - diff + q) * 1024) / (q + 24) in 1024 it blows up into debris,
	///                                                diff = (level + 1) / 2 - circle + 8, q = charges / diff;
	///                                                not chargeable (food, reagents, books, two spells): destroyed
	///   a weapon or armour enchanted with major 0xC  the next tier while the tier is within the caster's reach
	///                                                (weapons (level - 8) / 4 + Casting / 11, armour level + Casting / 11
	///                                                - 10), past it destroyed; the top tiers cannot go on
	///   a plain weapon or armour without contents    the lowest tier of one of its two kinds at random
	///   anything else                                "You cannot enchant that."
	///
	/// The mana is paid whatever happens (obj_spells' case 14). The engine side (UWObjectSpellUw2) shows the blast and
	/// takes away what is destroyed.
	/// </summary>
	public static class UWEnchantSpellRulesUw2
	{
		public enum OutcomeEnum
		{
			/// <summary>"You cannot enchant that."</summary>
			CannotEnchant,

			/// <summary>"You have enchanted the ...": a new tier, a first enchantment or new charges.</summary>
			Enchanted,

			/// <summary>charge_object failed: the blast, the spell gone and the thing turned to its debris in place.</summary>
			Blown,

			/// <summary>sp_enchant_destroy: the blast, and the thing is destroyed (carried: its debris by the player).</summary>
			Destroyed
		}

		/// <summary>The spell object a thing carries in its contents (MAJOR_SPEC minor 2 index 0).</summary>
		public const int SpellObjectId = 0x120;

		/// <summary>The major of the weapon and armour enchantments that have tiers.</summary>
		public const int TierMajor = 0xC;

		public const int CannotEnchantString = 0x12C + 1;

		/// <summary>"You have enchanted the " - then the thing's name and string 0x60.</summary>
		public const int EnchantedString = 0x12D + 1;

		/// <summary>"Your attempt to enchant the " - then the name and " destroys it in a blaze of fire!".</summary>
		public const int AttemptString = 0x13E + 1;

		public const int BlazeString = 0x13F + 1;

		/// <summary>damage_square's first kind: 10d6 of magic and fire on the thing's square.</summary>
		public const int BlastDice = 10;

		public const int BlastSides = 6;

		public const int BlastDamageType = UWDamageTypes.Fire | UWDamageTypes.Magic;

		/// <summary>The explosion sp_enchant_faildestroymess builds (ITEM_EXPLOSION_1C2).</summary>
		public const int ExplosionId = UWExplosionRules.ExplosionObjectId;

		private const int MajorHack = 0;

		private const int MajorRect = 5;

		private const int ClassWeapon = 0x00;

		private const int ClassFood = 0x0B;

		private const int ClassReagent = 0x0E;

		private const int ClassBook = 0x13;

		private const int ClassFurniture = 0x15;

		private const int DaggerId = 0x003;

		private const int BrokenDaggerId = 0x0C7;

		private const int DebrisId = 0x0D6;

		private const int WoodChipsId = 0x0DC;

		/// <summary>Weapons[].skill + 0xC5 is the broken weapon of that skill (SKILL_SWORD 3 gives 0xC8).</summary>
		private const int BrokenWeaponBase = 0xC5;

		/// <summary>Bit 11 of the id word (ID_FLAG11), bit 2 of UWObject.Flags: a spell cast on use, its charges shown.</summary>
		private const int CastOnUseFlag = 0x4;

		/// <summary>The link's special bit (LINK_SPECIAL).</summary>
		private const int SpecialLink = 0x200;

		/// <summary>
		/// decode_obj_spell: the spell a thing carries - the spell object in its contents, or for a thing with a quantity and
		/// the enchanted bit the thing itself (not a door or model, not a trap). A spell object of quality 0 hides 4 times in
		/// 10. pbCastOnUse is ID_FLAG11; with it the major is 12 plus link bits 6 to 8 (-1 for 0) and the effect link bits 0
		/// to 5, without it the major link >> 4 and the effect the low four bits. False when there is none.
		/// </summary>
		public static bool TryDecode(UWObject pOThing, IList<UWObject> pORecords, out UWObject pOSpell, out int piMajor, out int piEffect,
			out bool pbCastOnUse)
		{
			pOSpell = null;
			piMajor = 0;
			piEffect = 0;
			pbCastOnUse = false;

			if (pOThing == null || UWObjectIds.Major(pOThing.ID) == UWObjectMechanics.TrapMajorClass)
				return false;

			if (!pOThing.HasQuantity && pOThing.Quantity > 0)
			{
				pOSpell = FindSpellObject(pOThing, pORecords);

				if (pOSpell != null && pOSpell.Quality == 0 && UWRandom.Next(10) < 4)
					return false;
			}
			else if (pOThing.HasQuantity && pOThing.IsEnchanted && UWObjectIds.Major(pOThing.ID) != MajorRect)
				pOSpell = pOThing;

			if (pOSpell == null)
				return false;

			int liLink = pOSpell.Quantity & 0x1FF;

			pbCastOnUse = (pOSpell.Flags & CastOnUseFlag) != 0;

			if (pbCastOnUse)
			{
				piMajor = liLink >> 6;
				piMajor = piMajor == 0 ? -1 : piMajor + 12;
				piEffect = pOSpell.Quantity & 0x3F;
			}
			else
			{
				piMajor = liLink >> 4;
				piEffect = pOSpell.Quantity & 0xF;
			}

			return true;
		}

		/// <summary>The spell object (0x120) in a thing's contents, or null.</summary>
		public static UWObject FindSpellObject(UWObject pOThing, IList<UWObject> pORecords)
		{
			if (pOThing == null || pOThing.HasQuantity)
				return null;

			IEnumerable<UWObject> lOChain = pOThing.Contents != null ? (IEnumerable<UWObject>)pOThing.Contents
				: pORecords != null ? UWObjectChain.Walk(pOThing, pORecords, pORecords.Count) : null;

			if (lOChain == null)
				return null;

			foreach (UWObject lOAt in lOChain)
			{
				if (lOAt != null && lOAt.ID == SpellObjectId)
					return lOAt;
			}

			return null;
		}

		/// <summary>chargeable: not food, reagents or books, and not the spells major 7 effect 14 and major 13 effects 12 to 15.</summary>
		public static bool Chargeable(int piObjectId, int piMajor, int piEffect)
		{
			if (piMajor == 7 && piEffect == 0xE)
				return false;

			if (piMajor == 0xD && piEffect >= 0xC && piEffect <= 0xF)
				return false;

			int liClass = (piObjectId & 0x1F0) >> 4;

			return liClass != ClassFood && liClass != ClassReagent && liClass != ClassBook;
		}

		/// <summary>debris_type(item, 0): a weapon its skill's broken weapon (a dagger the broken dagger), furniture wood
		/// chips, anything else a pile of debris.</summary>
		public static int DebrisOf(int piObjectId, UWObjectClassProperties pOProperties)
		{
			int liClass = (piObjectId & 0x1F0) >> 4;

			if (liClass == ClassWeapon)
			{
				if (piObjectId == DaggerId)
					return BrokenDaggerId;

				if (pOProperties != null && pOProperties.TryGetMeleeWeapon(piObjectId, out UWObjectClassProperties.MeleeWeapon lOWeapon))
					return lOWeapon.SkillType + BrokenWeaponBase;

				return DebrisId;
			}

			return liClass == ClassFurniture ? WoodChipsId : DebrisId;
		}

		/// <summary>
		/// sp_enchant on pOThing (its contents from pORecords where they are not resolved), for a caster of piLevel and
		/// piCasting. Changes the thing as the original: a tier or a first enchantment into its quantity field, new charges
		/// into its spell object; for Blown the spell object is taken out (pOSpellGone) and the thing's id is its debris.
		/// Destroyed leaves the thing as it is for the engine side to destroy.
		/// </summary>
		public static OutcomeEnum Enchant(UWObject pOThing, IList<UWObject> pORecords, int piLevel, int piCasting,
			UWObjectClassProperties pOProperties, out UWObject pOSpellGone)
		{
			pOSpellGone = null;

			if (pOThing == null)
				return OutcomeEnum.CannotEnchant;

			bool lbAlready = TryDecode(pOThing, pORecords, out UWObject lOSpell, out int liMajor, out int liEffect, out bool lbCastOnUse);

			if (lbAlready)
			{
				if (lbCastOnUse && Chargeable(pOThing.ID, liMajor, liEffect))
					return fCharge(pOThing, pORecords, liEffect, piLevel, piCasting, pOProperties, out pOSpellGone);

				if (lbCastOnUse)
					return OutcomeEnum.Destroyed;

				if (liMajor == TierMajor)
					return fNextTier(pOThing, liEffect, piLevel, piCasting);

				return OutcomeEnum.CannotEnchant;
			}

			if (UWObjectIds.Major(pOThing.ID) != MajorHack || !(pOThing.HasQuantity || pOThing.Quantity == 0))
				return OutcomeEnum.CannotEnchant;

			// A plain weapon or armour: the lowest tier of one of its two kinds (0 or 4 for a weapon, 0 or 8 for armour).
			int liMinor = UWObjectIds.Minor(pOThing.ID);
			int liKind = liMinor <= 1 ? (UWRandom.Next(0x8000) % 2) << 2 : (UWRandom.Next(0x8000) % 2) << 3;

			pOThing.HasQuantity = true;
			pOThing.IsEnchanted = true;
			pOThing.Flags = (ushort)(pOThing.Flags & ~CastOnUseFlag);
			pOThing.Quantity = (ushort)(SpecialLink | (TierMajor << 4) | liKind);

			return OutcomeEnum.Enchanted;
		}

		/// <summary>A weapon's or armour's next tier: weapons the effects 0..2 and 4..6, armour 0..6 and 8..14, each tier
		/// allowed while it is within the caster's reach; past it the thing is destroyed. Anything else cannot.</summary>
		private static OutcomeEnum fNextTier(UWObject pOThing, int piEffect, int piLevel, int piCasting)
		{
			if (UWObjectIds.Major(pOThing.ID) != MajorHack)
				return OutcomeEnum.CannotEnchant;

			int liMinor = UWObjectIds.Minor(pOThing.ID);
			int liTier;
			int liReach;

			if (liMinor <= 1 && piEffect < 8 && (piEffect & ~4) < 3)
			{
				liTier = piEffect & ~4;
				liReach = ((piLevel - 8) / 4) + (piCasting / 11);
			}
			else if (liMinor >= 2 && (piEffect & ~8) < 7)
			{
				liTier = piEffect & ~8;
				liReach = piLevel + (piCasting / 11) - 10;
			}
			else
				return OutcomeEnum.CannotEnchant;

			if (liTier > liReach)
				return OutcomeEnum.Destroyed;

			pOThing.Quantity = (ushort)((pOThing.Quantity & 0x1F0) | ((piEffect + 1) & 0xF) | SpecialLink);

			return OutcomeEnum.Enchanted;
		}

		/// <summary>charge_object: the spell's circle (effect / 8 + 1, 4 from 0x40), diff = (level + 1) / 2 - circle + 8,
		/// q = charges / diff; with the chance it blows up, else 1 + Casting / 15 more charges (useNSpellCharges with a
		/// negative count, at most 63).</summary>
		private static OutcomeEnum fCharge(UWObject pOThing, IList<UWObject> pORecords, int piEffect, int piLevel, int piCasting,
			UWObjectClassProperties pOProperties, out UWObject pOSpellGone)
		{
			pOSpellGone = null;

			UWObject lOSpell = FindSpellObject(pOThing, pORecords);

			if (lOSpell == null)
				return OutcomeEnum.Enchanted;

			int liCircle = piEffect < 0x40 ? (piEffect / 8) + 1 : 4;
			int liDiff = ((piLevel + 1) / 2) - liCircle + 8;

			if (liDiff < 1)
				liDiff = 1;

			int liQ = lOSpell.Quality / liDiff;
			int liChance = ((0x10 - liDiff + liQ) << 10) / (liQ + 0x18);

			if ((UWRandom.Next(0x8000) & 0x3FF) < liChance)
			{
				pOSpellGone = lOSpell;
				pOThing.ID = (ushort)DebrisOf(pOThing.ID, pOProperties);

				return OutcomeEnum.Blown;
			}

			int liCharges = lOSpell.Quality + 1 + (piCasting / 15);

			lOSpell.Quality = (ushort)System.Math.Min(liCharges, 0x3F);

			return OutcomeEnum.Enchanted;
		}
	}
}
