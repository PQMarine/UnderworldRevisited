using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The player's status pass: what worn equipment and active spells together
	/// add up to in armour, protection, light and movement abilities.
	///
	/// The original computes this in one piece (PlayerStatusUpdate_ovr133_784, 382211-383111)
	/// and in this order:
	///
	///   1. clear the four armour bytes of the player's creature row (label 795)
	///   2. enter the base values of the five armour pieces, quality-scaled (label 7A6,
	///      value from ovr133_72D - see UWPlayerCritterRow.GetArmourValue)
	///   3. a SHIELD in the off hand onto parts 0 and 1 (labels 7EB-867)
	///   4. the defence value into row byte 0x12 (labels 86A-938, see UWPlayerCritterRow)
	///   5. reset the protection slots (ResetPlayerStatusValues_ovr133_1E4, 380861)
	///   6. apply the active spells (label 9DD, slot -1)
	///   7. apply the enchantments of the worn equipment (label A25, slots 0 to 10)
	///   8. put the highest resistance value from 6 and 7 on all four body parts
	///      (ApplyDefenceStealthBonuses_ovr133_65D, 381902) - which the original never
	///      actually does, see ResistBlowsAddsArmour
	///
	/// Steps 3 and 4 run through THE SAME case distinction (CastEnchantedItemSpell) - a
	/// ring of protection and the cast spell end up in the same place. That is why both
	/// are kept together here and not separate.
	///
	/// ARMOUR AND PROTECTION ARE TWO SEPARATE THINGS, and they act at different points.
	/// The reference keeps two fields side by side for this (playerdatcombat):
	///
	///   ARMOUR (LocationalArmourValues) subtracts DAMAGE. It comes from the worn
	///   armour pieces and from the resistance spells. It is read the same way
	///   as a creature's toughness - critterObjectDat.toughness returns exactly this field
	///   for the player character.
	///
	///   PROTECTION (EnchantmentProtectionSlots_dseg_5c99_266A) lowers the HIT CHANCE. It comes
	///   from enchantments of class 0xC and from the luck spell, and is subtracted from the
	///   opponent's attack score before the skill check (CalculateAttackResults, label 76F).
	///
	/// I mixed this up at first and told the user that armour lowers the hit chance.
	/// It does not - it absorbs damage.
	///
	/// THE SLOT MAPPING is not the obvious one. It is a table of five bytes,
	/// LocationDefenceIndex_dseg_5c99_1AF8 (283288) = 3, 0, 1, 2, 2:
	///
	///   Helmet       -> part 3
	///   Chest armour -> part 0
	///   Gloves       -> part 1
	///   Leggings     -> part 2
	///   Boots        -> part 2   (share the part with the leggings)
	///
	/// Everything that is not one of these five slots - the rings and the shield - lands on
	/// parts 0 AND 1 instead (label 4C6).
	/// </summary>
	public static class UWArmourProtection
	{
		/// <summary>
		/// Whether a resistance spell of class 2 - Resist Blows - adds its value to the armour
		/// of all four body parts. The switch is UWSettings.ResistBlowsAddsArmour, and it is
		/// OFF by default because the original does not do it.
		///
		/// IN THE ORIGINAL THAT LINE IS DEAD: class 2 writes the resistance into bits 4-7 of
		/// the accumulator (PlayerStatusUpdate_ovr133_784, label 379) and clears everything
		/// above it, but ApplyDefenceStealthBonuses_ovr133_65D reads bits 8-11 for the armour
		/// bonus - its first loop has already shifted the value right four times and the
		/// second shifts it four more (labels 6CA and 6DF), so the nibble it adds is always
		/// zero. The spell therefore protects against nothing in UW1.
		///
		/// Switched on, the spell does what it was obviously meant to do (deviation row 62 in
		/// Docs/AI/creature-ai.md). DamageResistance is filled either way, so the value is
		/// visible even while it does nothing.
		/// </summary>
		public static bool ResistBlowsAddsArmour;

		/// <summary>The original keeps this many body parts.</summary>
		public const int PartCount = 4;

		public const int PartBody = 0;

		public const int PartHands = 1;

		public const int PartLegs = 2;

		public const int PartHead = 3;

		// ---------- Spell classes

		/// <summary>Light. The brightest minor class wins.</summary>
		private const int LightMajorClass = 0;

		/// <summary>Movement (leap, slow fall, levitate, water walk, fly) - as a
		/// bit field.</summary>
		private const int MotionMajorClass = 1;

		/// <summary>Resistances. The minor class IS the value; only the highest counts, and it
		/// then applies to all four body parts.</summary>
		private const int ResistanceMajorClass = 2;

		/// <summary>Bonuses - luck, stealth, the immunities against individual
		/// damage types.</summary>
		private const int BonusMajorClass = 3;

		/// <summary>Spell class of the protection enchantments.</summary>
		private const int ProtectionMajorClass = 0xC;

		/// <summary>
		/// Cursed items. The minor class is the NUMBER OF DICE with which the
		/// curse strikes - see UWCharacter.ApplyCurse.
		///
		/// In uw1 there are three kinds of cursed items, all on level 7 in Tybal's maze (counted in the
		/// level data, 2026-09-10): the crowns 48 (four times) and 49 (twice) with
		/// minor class 5 and the jewelled shield 63 with minor class 6. That is exactly the puzzle
		/// of the maze - three objects are called "a_crown", and only the third (50) carries the
		/// maze navigation, the other two are cursed.
		/// </summary>
		private const int CurseMajorClass = 9;

		/// <summary>The special class. In Underworld 1 exactly one spell lives in it: the
		/// minor class 4, the maze navigation of the crown (reference:
		/// spellcasting_objects.CastEnchantedItemSpell, branch "case 0xD").</summary>
		private const int MiscMajorClass = 0xD;

		/// <summary>Minor class of the maze navigation - see Status.HasMazeNavigation.
		/// </summary>
		private const int MazeNavigationMinor = 4;

		/// <summary>
		/// Of the minor class of a protection enchantment only the lower three bits are the
		/// value, and the value is (minor &amp; 7) + 1 (labels 4DC-514).
		///
		/// BIT 3 PICKS WHERE IT LANDS, and the two are not the same thing: with the bit set the
		/// value is added to the ARMOUR byte of the part, without it to the PROTECTION slot of
		/// the part. Until 2026-09-20 the port read both branches as protection and dropped
		/// the +1.
		/// </summary>
		private const int ProtectionValueMask = 0x7;

		/// <summary>Bit 3 of the minor class: armour instead of protection.</summary>
		private const int ProtectionToArmourBit = 0x8;

		/// <summary>LocationDefenceIndex_dseg_5c99_1AF8 (283288): which body part the five
		/// paper doll slots 0 to 4 belong to.</summary>
		private static readonly int[] msLocationDefenceIndex = { PartHead, PartBody, PartHands, PartLegs, PartLegs };

		/// <summary>Luck: gives protection on all four parts.</summary>
		private const int LuckMinor = 1;

		private const int LuckProtection = 3;

		/// <summary>Minor class 2, the Stealth spell (string block 6 entry 51).</summary>
		private const int FirstStealthMinor = 2;

		/// <summary>Minor class 4, Invisibility (block 6 entry 53); 3 is Conceal.</summary>
		private const int LastStealthMinor = 4;

		/// <summary>Bit of minor class 2, Stealth: takes the noise down to zero.</summary>
		public const int StealthNoiseBit = 0x2;

		/// <summary>Bit of minor class 3, Conceal: five less visibility.</summary>
		public const int ConcealVisibilityBit = 0x4;

		/// <summary>Bit of minor class 4, Invisibility: visibility down to zero.</summary>
		public const int InvisibilityBit = 0x8;

		/// <summary>What Stealth takes off the noise - more than the base can ever be, so
		/// zero (the original compares against 0x10 and subtracts at most that).</summary>
		private const int StealthNoiseReduction = 0x10;

		/// <summary>What Conceal takes off the visibility.</summary>
		private const int ConcealVisibilityReduction = 5;

		/// <summary>What Invisibility takes off it - again everything there is.</summary>
		private const int InvisibilityReduction = 0x10;

		/// <summary>
		/// The first loop of ApplyDefenceStealthBonuses_ovr133_65D (381902-382018), noise half:
		/// bit 1 of the accumulator, the Stealth spell, subtracts 0x10 from the noise base
		/// (dseg_5c99_1AFE) and never below zero. The base itself is 13 minus Sneak / 3
		/// (ResetPlayerStatusValues_ovr133_1E4), the movement is added afterwards in
		/// ApplyPlayerSneakScore_seg034_2F89_898 - so Stealth does not silence a RUNNING
		/// player completely, it only takes his own base away.
		///
		/// Bit 0 is skipped in the original: it would be minor class 1, the Curse spell, which
		/// the same switch handles as protection and not as stealth.
		/// </summary>
		public static int ApplyToNoise(int piNoiseBase, int piStealthBonus)
		{
			if ((piStealthBonus & StealthNoiseBit) != 0)
				piNoiseBase -= StealthNoiseReduction;

			return piNoiseBase < 0 ? 0 : piNoiseBase;
		}

		/// <summary>
		/// The same loop, visibility half (dseg_5c99_1AFF, base 15 minus Sneak / 5): bit 2,
		/// Conceal, takes five off it, bit 3, Invisibility, all of it. Both floor at zero, and
		/// both run when both spells are up.
		/// </summary>
		public static int ApplyToVisibility(int piVisibilityBase, int piStealthBonus)
		{
			if ((piStealthBonus & ConcealVisibilityBit) != 0)
				piVisibilityBase -= ConcealVisibilityReduction;

			if (piVisibilityBase < 0)
				piVisibilityBase = 0;

			if ((piStealthBonus & InvisibilityBit) != 0)
				piVisibilityBase -= InvisibilityReduction;

			return piVisibilityBase < 0 ? 0 : piVisibilityBase;
		}

		private const int FirstProofMinor = 5;

		private const int LastProofMinor = 9;

		/// <summary>
		/// Which bit the five immunities of class 3 set (minor class 5 to 9).
		/// The sequence is the reference's and looks arbitrary because the bit field is ordered by
		/// DAMAGE TYPES, but the spells are not.
		///
		/// DISPLAYED, BUT WITHOUT EFFECT YET: the reference sets the field
		/// (PlayerDamageTypeScale) and never reads it again. We carry it along the same way
		/// so that the display shows the spell is running - damage is not reduced
		/// by it yet. For that every creature would first need to bring a damage type.
		/// </summary>
		private static readonly int[] msProofBits = { 0x40, 0x08, 0x10, 0x01, 0x02 };

		// ---------- Paper doll slots in the original's numbering

		private const int DollHelmet = 0;

		private const int DollChest = 1;

		private const int DollGloves = 2;

		private const int DollLegs = 3;

		private const int DollBoots = 4;

		/// <summary>The rings are slots 9 and 10 in the original's numbering, the two hands 7
		/// and 8 - all of them beyond the five armour pieces, so all of them land on parts 0
		/// and 1.</summary>
		private const int DollRing = 9;

		/// <summary>The shield hand, 7 + the handedness bit (label 7EB). Only its exact value
		/// relative to DollBoots matters here.</summary>
		private const int DollShieldHand = 7;

		/// <summary>For everything that does not come from an equipment slot - i.e. for the
		/// cast spells.</summary>
		private const int DollNone = -1;

		/// <summary>Object number of the dragon skin boots - see Status.HasDragonSkinBoots.
		/// Matching this, the object carries fire resistance 0x08 in comobj.dat.</summary>
		private const int DragonSkinBootsId = 0x2F;

		/// <summary>
		/// What comes out at the end of a status pass. A container that is reused
		/// - the pass runs every frame, so nothing should be allocated.
		/// </summary>
		public sealed class Status
		{
			/// <summary>Subtracts damage, per body part.</summary>
			public readonly int[] Armour = new int[PartCount];

			/// <summary>Lowers the hit chance, per body part.</summary>
			public readonly int[] Protection = new int[PartCount];

			/// <summary>The highest resistance value of class 2. Included in Armour only when
			/// UWArmourProtection.ResistBlowsAddsArmour says so - in the original it is not.
			/// </summary>
			public int DamageResistance;

			/// <summary>
			/// The skill whose HALF goes into the defence value - the weapon in the weapon hand
			/// decides (UWPlayerCritterRow.GetDefenceWeaponSkill). Unarmed with an empty hand,
			/// a shield or a bow.
			/// </summary>
			public UWPlayerData.Skill WeaponSkill;

			/// <summary>The brightest magical light source (class 0).</summary>
			public int Brightness;

			/// <summary>Bit field of the movement spells (class 1), from spells AND equipment.
			/// </summary>
			public int MotionAbilities;

			/// <summary>
			/// The same set of bits, but only the part that comes from a CAST spell.
			///
			/// The difference matters when lifting off: whoever casts Levitate lifts off at once
			/// while standing (per user in the original, 2026-09-03). A ring, by contrast, lets you
			/// walk quite normally - it only carries you once the ground is gone (per user, 2026-09-09). See
			/// UWPlayerMovement.fUpdateHover.
			/// </summary>
			public int SpellMotionAbilities;

			/// <summary>Bit field of the stealth bonuses (class 3, minor class 2 to 4):
			/// Stealth, Conceal and Invisibility, as bits 1, 2 and 3. What they do is in
			/// ApplyToNoise and ApplyToVisibility.</summary>
			public int StealthBonus;

			/// <summary>Bit field of the immunities (class 3, minor class 5 to 9). See
			/// msProofBits.</summary>
			public int DamageTypeProof;

			/// <summary>
			/// The dragon skin boots are worn.
			///
			/// NOT AN ENCHANTMENT, BUT A SPECIAL CASE ON THE OBJECT NUMBER: the reference
			/// specifically checks the boots slot for object 0x2F and remembers that in a
			/// flag of its own (playerdatloop, ApplyEquipmentEffects). It is needed in
			/// exactly one place - lava damage, see UWPlayerTerrain.
			/// </summary>
			public bool HasDragonSkinBoots;

			/// <summary>
			/// The crown of maze navigation is worn.
			///
			/// It is the only item in the game with an enchantment of class 0xD, namely
			/// minor class 4. In the level data it is object 0x32 - one
			/// of the three "a_crown" - on level 7, i.e. in Tybal's maze, where it also takes effect
			/// (counted in all nine levels of LEV.ARK, 2026-09-10).
			///
			/// Its effect is purely visual and concerns exactly one floor texture of this one
			/// level - see UWMazeNavigation.
			/// </summary>
			public bool HasMazeNavigation;

			/// <summary>
			/// The regeneration bits of class 11 - bit 0 health, bit 1 mana, the same byte the
			/// original keeps in ManaHealthRegeneration_dseg_5c99_3F8. Not a status value: it
			/// only tells the player tick that a point is due, see UWWornRegeneration
			/// and UWPlayerVitals.RunGameTick.
			/// </summary>
			public int RegenerationBits;

			/// <summary>
			/// How many dice the worn curses roll together (class 9).
			///
			/// SUMMED UP AND NOT PER ITEM, because the result is exactly the same: the
			/// reference rolls minor class times an eight-sided die per cursed item, and
			/// two items with five and six dice give the same distribution as one with
			/// eleven.
			/// </summary>
			public int CurseDice;

			public void Reset()
			{
				for (int liAt = 0; liAt < PartCount; liAt++)
				{
					Armour[liAt] = 0;
					Protection[liAt] = 0;
				}

				DamageResistance = 0;
				WeaponSkill = UWPlayerData.Skill.Unarmed;
				Brightness = 0;
				MotionAbilities = 0;
				SpellMotionAbilities = 0;
				StealthBonus = 0;
				DamageTypeProof = 0;
				RegenerationBits = 0;
				HasDragonSkinBoots = false;
				HasMazeNavigation = false;
				CurseDice = 0;
			}
		}

		/// <summary>
		/// The whole pass. pOActiveSpells may be null (then only the equipment counts).
		/// </summary>
		public static void Compute(IUWEquipment pOInventory, DataImport pOData,
			IReadOnlyList<UWActiveSpellEffect> pOActiveSpells, Status pOStatus)
		{
			pOStatus.Reset();

			if (pOInventory != null && pOData != null && pOData.ObjectClassProperties != null)
			{
				fAddArmourValue(pOInventory, pOData, UWArmorItemMap.BodySlot.Helmet, PartHead, pOStatus);
				fAddArmourValue(pOInventory, pOData, UWArmorItemMap.BodySlot.Chest, PartBody, pOStatus);
				fAddArmourValue(pOInventory, pOData, UWArmorItemMap.BodySlot.Gloves, PartHands, pOStatus);
				fAddArmourValue(pOInventory, pOData, UWArmorItemMap.BodySlot.Legs, PartLegs, pOStatus);
				fAddArmourValue(pOInventory, pOData, UWArmorItemMap.BodySlot.Boots, PartLegs, pOStatus);

				// A SHIELD ARMOURS BODY AND HANDS, not a paper doll slot of its own: the original
				// reads the off hand and adds the same quality-scaled value to row bytes 0 and 1
				// (labels 7EB-867). The port had no shield in the armour at all until 2026-09-20.
				UWObject lOShield = pOInventory.GetEquipped(GetShieldSlot(pOInventory.IsLeftHanded));

				if (lOShield != null && UWPlayerCritterRow.IsShield(lOShield.ID))
				{
					int liShield = UWPlayerCritterRow.GetArmourValue(lOShield, pOData);

					pOStatus.Armour[PartBody] += liShield;
					pOStatus.Armour[PartHands] += liShield;
				}

				pOStatus.WeaponSkill = UWPlayerCritterRow.GetDefenceWeaponSkill(
					pOInventory.GetEquipped(GetWeaponSlot(pOInventory.IsLeftHanded)), pOData);
			}

			// First the spells, then the equipment - the same way round as in the reference. For the
			// resistances the order does not matter (the highest wins), nor for light.
			if (pOActiveSpells != null)
			{
				for (int liAt = 0; liAt < pOActiveSpells.Count; liAt++)
				{
					fApplyEnchantment(pOActiveSpells[liAt].MajorClass,
						pOActiveSpells[liAt].MinorClass, DollNone, pOStatus);
				}
			}

			// The movement bits collected up to here come from cast spells - whatever is
			// added afterwards comes from equipment. See Status.SpellMotionAbilities.
			pOStatus.SpellMotionAbilities = pOStatus.MotionAbilities;

			if (pOInventory != null)
			{
				fAddSlotEnchantment(pOInventory, UWArmorItemMap.BodySlot.Helmet, DollHelmet, pOStatus);
				fAddSlotEnchantment(pOInventory, UWArmorItemMap.BodySlot.Chest, DollChest, pOStatus);
				fAddSlotEnchantment(pOInventory, UWArmorItemMap.BodySlot.Gloves, DollGloves, pOStatus);
				fAddSlotEnchantment(pOInventory, UWArmorItemMap.BodySlot.Legs, DollLegs, pOStatus);
				fAddSlotEnchantment(pOInventory, UWArmorItemMap.BodySlot.Boots, DollBoots, pOStatus);

				UWObject lOBoots = pOInventory.GetEquipped(UWArmorItemMap.BodySlot.Boots);

				pOStatus.HasDragonSkinBoots = lOBoots != null && lOBoots.ID == DragonSkinBootsId;

				// WHICH SLOTS CAST THEIR ENCHANTMENT is decided by ovr120_BA2 (367615), called
				// per slot in the loop at label A25: the five armour pieces, the two rings and
				// the SHIELD HAND (but only when a shield is in it), never the weapon hand and
				// never the two shoulder slots. An enchanted weapon therefore only takes effect
				// when striking, not while it is held.
				fAddSlotEnchantment(pOInventory, UWArmorItemMap.BodySlot.LeftRing, DollRing, pOStatus);
				fAddSlotEnchantment(pOInventory, UWArmorItemMap.BodySlot.RightRing, DollRing, pOStatus);

				UWArmorItemMap.BodySlot leShieldSlot = GetShieldSlot(pOInventory.IsLeftHanded);
				UWObject lOShieldItem = pOInventory.GetEquipped(leShieldSlot);

				if (lOShieldItem != null && UWPlayerCritterRow.IsShield(lOShieldItem.ID))
					fAddSlotEnchantment(pOInventory, leShieldSlot, DollShieldHand, pOStatus);
			}

			// Resistance comes last and applies equally everywhere - if the switch allows it,
			// see ResistBlowsAddsArmour.
			if (!ResistBlowsAddsArmour)
				return;

			for (int liAt = 0; liAt < PartCount; liAt++)
				pOStatus.Armour[liAt] += pOStatus.DamageResistance;
		}

		/// <summary>
		/// The two front hand slots in the original's meaning: the weapon sits in slot
		/// 8 minus the handedness bit, the shield in slot 7 plus it (labels 7EB and 887). For a
		/// right-hander that is the right hand for the weapon and the left for the shield.
		/// </summary>
		public static UWArmorItemMap.BodySlot GetWeaponSlot(bool pbLeftHanded)
		{
			return pbLeftHanded ? UWArmorItemMap.BodySlot.LeftHandSlot : UWArmorItemMap.BodySlot.RightHandSlot;
		}

		public static UWArmorItemMap.BodySlot GetShieldSlot(bool pbLeftHanded)
		{
			return pbLeftHanded ? UWArmorItemMap.BodySlot.RightHandSlot : UWArmorItemMap.BodySlot.LeftHandSlot;
		}

		/// <summary>The base value of an armour piece onto its body part - quality-scaled, see
		/// UWPlayerCritterRow.GetArmourValue.</summary>
		private static void fAddArmourValue(IUWEquipment pOInventory, DataImport pOData,
			UWArmorItemMap.BodySlot peSlot, int piPart, Status pOStatus)
		{
			pOStatus.Armour[piPart] += UWPlayerCritterRow.GetArmourValue(pOInventory.GetEquipped(peSlot), pOData);
		}

		/// <summary>The enchantment of an equipment slot, if one is attached.</summary>
		private static void fAddSlotEnchantment(IUWEquipment pOInventory,
			UWArmorItemMap.BodySlot peSlot, int piDollSlot, Status pOStatus)
		{
			UWObject lOItem = pOInventory.GetEquipped(peSlot);

			int liMajor;
			int liMinor;

			if (!TryGetWearableEnchantment(lOItem, out liMajor, out liMinor))
				return;

			fApplyEnchantment(liMajor, liMinor, piDollSlot, pOStatus);
		}

		/// <summary>
		/// The one case distinction that everything runs through - reference:
		/// spellcasting_objects.CastEnchantedItemSpell.
		///
		/// OF CLASS 0xB (11) ONLY THE TWO REGENERATION SUBCLASSES ARE HERE. Freeze Time,
		/// Roaming Sight, Speed and Telekinesis exist only as cast spells and are evaluated by
		/// UWPlayerVitals.fApplyActiveSpells; Regeneration (0xE) and Mana Regeneration (0xF)
		/// exist only on worn items, and the original collects their two bits in exactly this
		/// pass (ActiveEnchantedItem_Spells_ovr133_347, branches HealthRegeneration_ovr133_45D
		/// and ManaRegeneration_ovr133_468) so that the player tick can read them. See
		/// UWWornRegeneration.
		/// </summary>
		private static void fApplyEnchantment(int piMajor, int piMinor, int piDollSlot,
			Status pOStatus)
		{
			switch (piMajor)
			{
				case LightMajorClass:
					if (piMinor > pOStatus.Brightness)
						pOStatus.Brightness = piMinor;
					break;

				case MotionMajorClass:
					if (piMinor >= 1)
						pOStatus.MotionAbilities |= 1 << (piMinor - 1);
					break;

				case ResistanceMajorClass:
					if (piMinor > pOStatus.DamageResistance)
						pOStatus.DamageResistance = piMinor;
					break;

				case BonusMajorClass:
					fApplyBonus(piMinor, pOStatus);
					break;

				case ProtectionMajorClass:
					fApplyProtection(piMinor, piDollSlot, pOStatus);
					break;

				case CurseMajorClass:
					// ONLY COUNT, DO NOT STRIKE. This pass runs EVERY FRAME;
					// letting the curse take effect from here would kill you within a second. The
					// reference calls its status pass on the player tick, and that is exactly
					// where the curse strikes in our code - see UWCharacter.ApplyCurse.
					pOStatus.CurseDice += piMinor;
					break;

				case UWWornRegeneration.MajorClass:
					// NO SLOT CHECK, unlike the protection class below: the original's branch
					// sets the bit whatever the fourth argument is, so a cast spell of this
					// class would count too. None exists in uw1.
					pOStatus.RegenerationBits |= UWWornRegeneration.GetBits(piMajor, piMinor);
					break;

				case MiscMajorClass:
					if (piMinor == MazeNavigationMinor)
						pOStatus.HasMazeNavigation = true;
					break;
			}
		}

		/// <summary>Class 3 - reference: spellcasting_class_0123.CastClass3Enchantment.</summary>
		private static void fApplyBonus(int piMinor, Status pOStatus)
		{
			if (piMinor == LuckMinor)
			{
				for (int liAt = 0; liAt < PartCount; liAt++)
					pOStatus.Protection[liAt] += LuckProtection;

				return;
			}

			if (piMinor >= FirstStealthMinor && piMinor <= LastStealthMinor)
			{
				pOStatus.StealthBonus |= 1 << (piMinor - 1);
				return;
			}

			if (piMinor >= FirstProofMinor && piMinor <= LastProofMinor)
				pOStatus.DamageTypeProof |= msProofBits[piMinor - FirstProofMinor];
		}

		/// <summary>
		/// Class 0xC - ActiveEnchantedItem_Spells_ovr133_347, branch
		/// ClassC_ProtectionToughness_ovr133_49F (381456-381578).
		///
		/// A CAST SPELL OF THIS CLASS DOES NOTHING. The original leaves at once when the slot
		/// is negative (label 49F), and the loop over the active spells passes -1 (label 9DD).
		/// Only a worn item carries this class.
		///
		/// WHERE IT LANDS: the five armour pieces on their own part via
		/// LocationDefenceIndex, everything else (rings, shield) on parts 0 AND 1 (label 4C6).
		/// The value is (minor &amp; 7) + 1, and bit 3 of the minor class decides whether it is
		/// armour or protection (labels 4DC-514).
		///
		/// Until 2026-09-20 the port allowed helmet and chest only, treated both branches as
		/// protection and dropped the +1 - so Henrietta's enchanted leggings did nothing at
		/// all and her helmet gave one point too little.
		/// </summary>
		private static void fApplyProtection(int piMinor, int piDollSlot, Status pOStatus)
		{
			if (piDollSlot < 0)
				return;

			int liValue = (piMinor & ProtectionValueMask) + 1;
			int[] lOTarget = (piMinor & ProtectionToArmourBit) != 0 ? pOStatus.Armour : pOStatus.Protection;

			if (piDollSlot < msLocationDefenceIndex.Length)
			{
				lOTarget[msLocationDefenceIndex[piDollSlot]] += liValue;

				return;
			}

			lOTarget[PartBody] += liValue;
			lOTarget[PartHands] += liValue;
		}

		/// <summary>
		/// How much the enchantment of an armour piece gives, or zero. Only for the display -
		/// in the pass above the same computation lives in fApplyProtection, which also decides
		/// whether the value counts as armour (bit 3 of the minor class) or as protection.
		/// </summary>
		public static int GetProtectionEnchantment(UWObject pOItem)
		{
			int liMajor;
			int liMinor;

			if (!TryGetWearableEnchantment(pOItem, out liMajor, out liMinor))
				return 0;

			return liMajor == ProtectionMajorClass ? (liMinor & ProtectionValueMask) + 1 : 0;
		}

		/// <summary>
		/// The enchantment of a clothing or weapon item as major and minor class.
		/// Returns false if none is attached.
		///
		/// AN ARMOUR PIECE CARRIES ITS SPELL IN THE OTHER FORM: a wand holds
		/// a rune spell number in the upper bits, here instead major and minor class -
		/// (link &amp; 0x1FF) >> 4 and (link &amp; 0xF). Which form applies is told by bit 2 of the flags;
		/// for armour it is not set (see UWObjectMechanics.GetObjectSpellIndex, which has
		/// the other half of the same case distinction).
		///
		/// Recomputed on Henrietta's equipment from SAVE3: helmet, chest armour and leggings
		/// all carry flags 8 and quantity fields around 706 to 714 - decoded major class 12,
		/// i.e. exactly this protection class.
		/// </summary>
		public static bool TryGetWearableEnchantment(UWObject pOItem, out int piMajor, out int piMinor)
		{
			piMajor = -1;
			piMinor = -1;

			if (pOItem == null || !pOItem.IsEnchanted)
				return false;

			// WITH BIT 2 SET the link is a rune spell number and not major/minor class -
			// decoding it here would give a nonsense class (see
			// UWObjectMechanics.GetObjectSpellIndex). In the data of uw1 the case does not
			// occur: of the 60 enchanted weapons and pieces of clothing lying in the nine
			// levels not one carries the bit (checked 2026-09-16), and nothing in our code sets
			// it either. It stays as a guard for save games and later item creation.
			if ((pOItem.Flags & UWObjectMechanics.SpellFormatFlag) != 0)
				return false;

			int liRaw = pOItem.Quantity & 0x1FF;

			piMajor = liRaw >> 4;
			piMinor = liRaw & 0xF;

			return true;
		}

		/// <summary>
		/// Which body part a blow hits, from the HEIGHTS of attacker and defender in zpos steps -
		/// feet and top of each body, the top being the feet plus the height from COMOBJ.DAT
		/// (PickBodyHitPoint_seg022_A, called for a weapon strike and for a missile alike):
		///
		///   The attacker's middle below the defender's feet:  legs, without a roll
		///   The attacker's feet above the defender's top:      head, without a roll
		///   Otherwise, attacker's middle at or above the
		///   defender's:                                        one third head, else on
		///   Attacker's middle lower:                           half legs, else on
		///   ... and on:                                        one third hands, the rest body
		///
		/// Until 2026-09-18 only the two centres in world units were compared and the two clear
		/// cases were missing (P4 of the engine separation).
		/// </summary>
		public static int PickBodyPart(int piDefenderFeet, int piDefenderTop, int piAttackerFeet, int piAttackerTop)
		{
			int liDefenderMiddle = (piDefenderFeet + piDefenderTop) >> 1;
			int liAttackerMiddle = (piAttackerFeet + piAttackerTop) >> 1;

			if (piDefenderFeet + 1 > liAttackerMiddle)
				return PartLegs;

			if (piDefenderTop - 1 < piAttackerFeet)
				return PartHead;

			if (liAttackerMiddle >= liDefenderMiddle)
			{
				if (UWRandom.Next(0, 3) == 0)
					return PartHead;
			}
			else
			{
				if (UWRandom.Next(0, 2) != 0)
					return PartLegs;
			}

			return UWRandom.Next(0, 3) == 0 ? PartHands : PartBody;
		}
	}
}
