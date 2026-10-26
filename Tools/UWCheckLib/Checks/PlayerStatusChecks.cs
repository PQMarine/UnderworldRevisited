using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// A worn set through the whole status pass, with the real OBJECTS.DAT tables - spec
        /// 8.8. It pins what PlayerStatusUpdate_ovr133_784 would write into the player's row:
        /// the four armour bytes, the protection slots and the weapon skill of the defence.
        /// </summary>
        private static void fCheckPlayerStatus(DataImport pOData)
        {
            Check.Group("Player status pass");

            FakeEquipment lOWorn = new FakeEquipment();

            // A full set at full quality. From the tables: chest 0x22 and helmet 0x2E carry 6,
            // gloves 0x28 and boots 0x2B carry 5, leggings 0x25 carry 6, the shield 0x3B 3.
            lOWorn.Put(UWArmorItemMap.BodySlot.Chest, 0x22, UWPlayerCritterRow.MaxQuality);
            lOWorn.Put(UWArmorItemMap.BodySlot.Helmet, 0x2E, UWPlayerCritterRow.MaxQuality);
            lOWorn.Put(UWArmorItemMap.BodySlot.Gloves, 0x28, UWPlayerCritterRow.MaxQuality);
            lOWorn.Put(UWArmorItemMap.BodySlot.Legs, 0x25, UWPlayerCritterRow.MaxQuality);
            lOWorn.Put(UWArmorItemMap.BodySlot.Boots, 0x2B, UWPlayerCritterRow.MaxQuality);

            // Right-handed: the weapon in the right hand, the shield in the left (labels 7EB,
            // 887). A long sword (0x03) carries skill type 3, so the Sword skill.
            lOWorn.Put(UWArmorItemMap.BodySlot.RightHandSlot, 0x03, UWPlayerCritterRow.MaxQuality);
            lOWorn.Put(UWArmorItemMap.BodySlot.LeftHandSlot, 0x3B, UWPlayerCritterRow.MaxQuality);

            UWArmourProtection.Status lOStatus = new UWArmourProtection.Status();

            UWArmourProtection.Compute(lOWorn.Model, pOData, null, lOStatus);

            fExpectInt("worn: body is chest 6 plus shield 3", lOStatus.Armour[UWArmourProtection.PartBody], 9);
            fExpectInt("worn: hands are gloves 5 plus shield 3", lOStatus.Armour[UWArmourProtection.PartHands], 8);
            fExpectInt("worn: legs are leggings 6 plus boots 5", lOStatus.Armour[UWArmourProtection.PartLegs], 11);
            fExpectInt("worn: the head is the helmet alone", lOStatus.Armour[UWArmourProtection.PartHead], 6);
            fExpectInt("worn: the sword decides the defence skill", (int)lOStatus.WeaponSkill, (int)UWPlayerData.Skill.Sword);
            fExpectInt("worn: defence 30 with sword 25 is 42",
                UWPlayerCritterRow.GetDefence(30, 25), 42);

            // Half quality halves the pieces (4, 4, 3, 3, 3 and the shield 2).
            lOWorn.Put(UWArmorItemMap.BodySlot.Chest, 0x22, 32);
            lOWorn.Put(UWArmorItemMap.BodySlot.LeftHandSlot, 0x3B, 32);

            UWArmourProtection.Compute(lOWorn.Model, pOData, null, lOStatus);

            fExpectInt("worn: a half-worn chest and shield give 4 + 2", lOStatus.Armour[UWArmourProtection.PartBody], 6);

            // A bow in the weapon hand keeps the Unarmed skill (the ranged branch never sets
            // var_6), and it is no shield either.
            lOWorn.Put(UWArmorItemMap.BodySlot.RightHandSlot, 0x10, UWPlayerCritterRow.MaxQuality);

            UWArmourProtection.Compute(lOWorn.Model, pOData, null, lOStatus);

            fExpectInt("worn: a bow leaves the Unarmed skill", (int)lOStatus.WeaponSkill, (int)UWPlayerData.Skill.Unarmed);

            // The enchantments of class 0xC: leggings with minor 2 give protection 3 on the
            // legs, a ring with minor 0x0A (bit 3 set) gives armour 3 TWICE on the body and none
            // on the hands - player_affected_by adds it to armour[slots[0]] in both turns of its
            // loop (Exhumed audit row 12, 2026-10-11; until then 3 on body and hands each).
            lOWorn.Enchant(UWArmorItemMap.BodySlot.Legs, 0xC, 0x2);
            lOWorn.Enchant(UWArmorItemMap.BodySlot.LeftRing, 0x36, 0xC, 0xA);

            UWArmourProtection.Compute(lOWorn.Model, pOData, null, lOStatus);

            fExpectInt("enchantment: leggings of minor 2 protect the legs by 3",
                lOStatus.Protection[UWArmourProtection.PartLegs], 3);
            fExpectInt("enchantment: they do not protect the head",
                lOStatus.Protection[UWArmourProtection.PartHead], 0);
            fExpectInt("enchantment: a ring with bit 3 armours the body by 3 twice",
                lOStatus.Armour[UWArmourProtection.PartBody], 12);
            fExpectInt("enchantment: and the hands not at all",
                lOStatus.Armour[UWArmourProtection.PartHands], 7);

            // THE SWITCH for the original's dead resistance nibble, asked for by the user on
            // 2026-09-20 (UWArmourProtection.ResistBlowsAddsArmour, UWSettings of the same
            // name): a class-2 enchantment of minor 4 is seen either way - the value is what
            // the panel shows - but only with the switch on does it reach the armour. Off is
            // the original, where PlayerStatusUpdate writes the nibble that
            // ApplyDefenceStealthBonuses never reads.
            lOWorn.Enchant(UWArmorItemMap.BodySlot.RightRing, 0x36, 0x2, 0x4);

            UWArmourProtection.ResistBlowsAddsArmour = false;
            UWArmourProtection.Compute(lOWorn.Model, pOData, null, lOStatus);

            int liWithoutResistance = lOStatus.Armour[UWArmourProtection.PartHead];

            fExpectInt("resistance: the value is seen", lOStatus.DamageResistance, 4);
            fExpectInt("resistance: the switch off leaves the armour alone", liWithoutResistance, 6);

            UWArmourProtection.ResistBlowsAddsArmour = true;
            UWArmourProtection.Compute(lOWorn.Model, pOData, null, lOStatus);

            fExpectInt("resistance: the switch on reaches every part",
                lOStatus.Armour[UWArmourProtection.PartHead], liWithoutResistance + 4);

            UWArmourProtection.ResistBlowsAddsArmour = false;

            // THE SHIELD HAND (ObjWorn, FixPlayerEquips, 2026-10-12): dragon skin boots held there
            // protect from lava; an enchanted plate helmet there casts its enchantment (onto body
            // and hands as a ring's would); an enchanted pair of dragon skin boots does not count.
            FakeEquipment lOHand = new FakeEquipment();

            lOHand.Put(UWArmorItemMap.BodySlot.LeftHandSlot, 0x2F, UWPlayerCritterRow.MaxQuality);
            UWArmourProtection.Compute(lOHand.Model, pOData, null, lOStatus);
            fExpectBool("shield hand: dragon skin boots there protect from lava", lOStatus.HasDragonSkinBoots, true);

            lOHand.Enchant(UWArmorItemMap.BodySlot.LeftHandSlot, 0xC, 0x2);
            UWArmourProtection.Compute(lOHand.Model, pOData, null, lOStatus);
            fExpectBool("shield hand: ... not when enchanted", lOStatus.HasDragonSkinBoots, false);

            lOHand.Enchant(UWArmorItemMap.BodySlot.LeftHandSlot, 0x2E, 0xC, 0x2);
            UWArmourProtection.Compute(lOHand.Model, pOData, null, lOStatus);
            fExpectInt("shield hand: an enchanted plate helmet protects the body by 3",
                lOStatus.Protection[UWArmourProtection.PartBody], 3);
            fExpectInt("shield hand: ... but gives no armour", lOStatus.Armour[UWArmourProtection.PartBody], 0);

            // THROWN CLASS 1 OBJECTS (missile_newhit, OBJPHYS.C): ammunition (ammo byte -64) is of
            // type 0x40; a launcher's type is -ammo, and only ammunition is skill-scaled.
            fExpectInt("thrown: an arrow is ammunition", UWPlayerAttack.GetMissileAmmoByte(pOData, 0x12), UWPlayerAttack.AmmunitionMarker);
            fExpectInt("thrown: ... of type 0x40", UWPlayerAttack.GetMissileDamageType(pOData, 0x12), UWDamageTypes.Missile);
            fExpectInt("thrown: the lightning bolt's own type is 0x23", UWPlayerAttack.GetMissileDamageType(pOData, 0x15), 0x23);
            fExpectBool("thrown: a sling is no ammunition",
                UWPlayerAttack.GetMissileAmmoByte(pOData, 0x18) != UWPlayerAttack.AmmunitionMarker, true);

            fCheckWornRegeneration(pOData, lOWorn, lOStatus);
        }

        /// <summary>
        /// The Ring of Regeneration and its mana counterpart - class 11, minor 0xE and 0xF
        /// (ActiveEnchantedItem_Spells_ovr133_347, branches HealthRegeneration_ovr133_45D and
        /// ManaRegeneration_ovr133_468). Pinned here are the two halves of the rule: the
        /// status pass collecting the bits, and the tick turning one bit into exactly one
        /// point, capped at the maximum.
        ///
        /// THE USER MEASURED IT IN THE ORIGINAL on 2026-09-20: "about one hit point every
        /// twenty seconds". The disassembly says twenty-one - the player tick fires when the
        /// accumulated whole seconds exceed 0x14 (PlayerUpdateTick_seg024_24DC_3A4, label
        /// 4D7), which is 21 s or 5376 PIT ticks.
        /// </summary>
        private static void fCheckWornRegeneration(DataImport pOData, FakeEquipment pOWorn,
            UWArmourProtection.Status pOStatus)
        {
            // The rule in numbers.
            fExpectInt("regeneration: major class 11", UWWornRegeneration.MajorClass, 11);
            fExpectInt("regeneration: health is minor class 14", UWWornRegeneration.HealthMinorClass, 0xE);
            fExpectInt("regeneration: mana is minor class 15", UWWornRegeneration.ManaMinorClass, 0xF);
            fExpectInt("regeneration: one point per tick", UWWornRegeneration.Amount, 1);
            fExpectInt("regeneration: the interval is 21 seconds", UWWornRegeneration.IntervalSeconds, 21);
            fExpectInt("regeneration: which is 5376 PIT ticks", UWWornRegeneration.IntervalPitTicks, 5376);

            // THE SHARED TICK. Its length used to sit in four places and ran twenty seconds
            // on the Unity side (2026-09-21); everything now derives from UWPlayerTick, so
            // these checks guard that nothing drifts apart again. The Unity hosts cannot be
            // reached from here - UWCharacter.SpellTickSeconds takes the same constant, and the
            // swim check runs on the tick itself (cleanup step 4.4, 2026-10-17).
            fExpectInt("tick: one player tick is 21 seconds", UWPlayerTick.Seconds, 21);

            // THE DIFFICULTY in the attack score (2026-09-21): seven points on easy, measured
            // from two saves the user made, one per setting - Standard writes 0 into PLAYER.DAT
            // byte 0xB4, Easy writes 1.
            fExpectInt("difficulty: easy gives seven points on the attack score",
                UWPlayerAttack.EasyDifficultyAttackBonus, 7);
            fExpectInt("tick: which is 5376 PIT ticks", UWPlayerTick.PitTicks, 5376);
            fExpectInt("tick: regeneration uses the shared tick",
                UWWornRegeneration.IntervalSeconds, UWPlayerTick.Seconds);
            fExpectInt("tick: three ticks are the minute", UWPlayerTick.MinuteTicks, 3);
            fExpectInt("tick: twenty-four ticks are the five minutes", UWPlayerTick.FiveMinuteTicks, 24);
            fExpectBool("tick: and the minute divides it, as the original's reset needs",
                UWPlayerTick.FiveMinuteTicks % UWPlayerTick.MinuteTicks == 0, true);
            fExpectInt("tick: so the block is eight and a half real minutes",
                UWPlayerTick.FiveMinuteTicks * UWPlayerTick.Seconds, 504);
            // The respawn roll sits in the block itself since 2026-10-15 (YearlyCheckup group).

            // The names in string block 6 are (major << 4) | minor - the proof that the two
            // branches of the disassembly are these two enchantments.
            if (pOData != null && pOData.Strings != null)
            {
                fExpectBool("regeneration: 0xBE is named Regeneration",
                    fSpellNameIs(pOData, UWWornRegeneration.MajorClass, UWWornRegeneration.HealthMinorClass, "Regeneration"), true);
                fExpectBool("regeneration: 0xBF is named Mana Regeneration",
                    fSpellNameIs(pOData, UWWornRegeneration.MajorClass, UWWornRegeneration.ManaMinorClass, "Mana Regeneration"), true);
            }

            // The status pass: a ring in either ring slot sets the bit, a helmet of another
            // class sets nothing, and taking the ring off clears it again.
            FakeEquipment lOBare = new FakeEquipment();

            lOBare.Enchant(UWArmorItemMap.BodySlot.LeftRing, 0x36,
                UWWornRegeneration.MajorClass, UWWornRegeneration.HealthMinorClass);

            UWArmourProtection.Compute(lOBare.Model, pOData, null, pOStatus);

            fExpectInt("regeneration: a worn ring sets the health bit",
                pOStatus.RegenerationBits, UWWornRegeneration.HealthBit);

            lOBare.Enchant(UWArmorItemMap.BodySlot.RightRing, 0x36,
                UWWornRegeneration.MajorClass, UWWornRegeneration.ManaMinorClass);

            UWArmourProtection.Compute(lOBare.Model, pOData, null, pOStatus);

            fExpectInt("regeneration: two rings set both bits",
                pOStatus.RegenerationBits, UWWornRegeneration.HealthBit | UWWornRegeneration.ManaBit);

            UWArmourProtection.Compute(pOWorn.Model, pOData, null, pOStatus);

            fExpectInt("regeneration: without the rings the bits are gone", pOStatus.RegenerationBits, 0);

            // The tick: one point per tick and not one more, and the cap holds. Wounded at
            // 10 of 30, so there is room for the ring to work.
            FakeVitalsHost lOHost = new FakeVitalsHost { Regeneration = UWWornRegeneration.HealthBit };
            UWPlayerVitals lOVitals = fWoundedVitals(lOHost);

            lOVitals.RunGameTick(0, 0);

            fExpectInt("regeneration: one tick gives exactly one hit point", (int)lOVitals.CurrentHP, 11);

            for (int liTick = 0; liTick < 5; liTick++)
                lOVitals.RunGameTick(0, 0);

            fExpectInt("regeneration: five more ticks give five more", (int)lOVitals.CurrentHP, 16);

            // Up to the cap and no further. Twenty ticks are more than the missing fourteen.
            for (int liTick = 0; liTick < 20; liTick++)
                lOVitals.RunGameTick(0, 0);

            fExpectInt("regeneration: it stops at the maximum", (int)lOVitals.CurrentHP, 30);

            // The mana bit on its own, on the same tick. A fresh character, and only two
            // ticks, because the third is the minute tick on which the Mana skill regenerates
            // by its own rule (fRegenerateMana) and would blur the number.
            FakeVitalsHost lOManaHost = new FakeVitalsHost { Regeneration = UWWornRegeneration.ManaBit };
            UWPlayerVitals lOManaVitals = fWoundedVitals(lOManaHost);

            lOManaVitals.RunGameTick(0, 0);
            lOManaVitals.RunGameTick(0, 0);

            fExpectInt("regeneration: the mana bit gives one mana per tick", (int)lOManaVitals.CurrentMana, 7);
            fExpectInt("regeneration: and leaves the hit points alone", (int)lOManaVitals.CurrentHP, 10);

            // No ring, no point.
            FakeVitalsHost lOBareHost = new FakeVitalsHost();
            UWPlayerVitals lOBareVitals = fWoundedVitals(lOBareHost);

            lOBareVitals.RunGameTick(0, 0);
            lOBareVitals.RunGameTick(0, 0);

            fExpectInt("regeneration: without a ring the hit points stay", (int)lOBareVitals.CurrentHP, 10);
            fExpectInt("regeneration: and so does the mana", (int)lOBareVitals.CurrentMana, 5);

            // NATURAL HEALING (PlayerUpdates, five-minute block): one point at most per block,
            // on a Strength check against 15 - not the sleep formula it used to be.
            UWPlayerVitals lOHealVitals = fWoundedVitals(new FakeVitalsHost());

            for (int liTick = 0; liTick < UWPlayerTick.FiveMinuteTicks; liTick++)
                lOHealVitals.RunGameTick(0, 0);

            fExpectBool("healing: a five-minute block gives at most one hit point",
                lOHealVitals.CurrentHP >= 10 && lOHealVitals.CurrentHP <= 11, true);
            fExpectInt("healing: the meal counter counts the block", lOHealVitals.MealHealCounter, 1);

            // A MEAL HEALS an eighth of the blocks since the last one (ChangeHunger_ovr143_1107):
            // after eight blocks one point, and the count starts again.
            for (int liTick = 0; liTick < 7 * UWPlayerTick.FiveMinuteTicks; liTick++)
                lOHealVitals.RunGameTick(0, 0);

            float lfBeforeMeal = lOHealVitals.CurrentHP;

            fExpectBool("meal: eating succeeds", lOHealVitals.ChangeHunger(1), true);
            fExpectInt("meal: eight blocks heal one point", (int)(lOHealVitals.CurrentHP - lfBeforeMeal), 1);
            fExpectInt("meal: and the counter starts again", lOHealVitals.MealHealCounter, 0);
            fExpectBool("meal: beyond 255 nothing is eaten", lOHealVitals.ChangeHunger(UWPlayerVitals.FullHunger), false);
        }

        /// <summary>A character with 10 of 30 hit points and 5 of 20 mana, without a save game
        /// and without a skill preset - the latter would set the mana to its maximum.</summary>
        private static UWPlayerVitals fWoundedVitals(IUWVitalsHost pIHost)
        {
            UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

            lOStart.FallbackMaxHP = 30;
            lOStart.FallbackMaxMana = 20;
            lOStart.FallbackCurrentHP = 10;
            lOStart.FallbackCurrentMana = 5;

            UWPlayerVitals lOVitals = new UWPlayerVitals(pIHost);

            lOVitals.Initialise(null, lOStart);

            return lOVitals;
        }

        /// <summary>The name an enchantment gets from string block 6 - the index is
        /// (major &lt;&lt; 4) | minor, plus the project's usual one.</summary>
        private static bool fSpellNameIs(DataImport pOData, int piMajor, int piMinor, string psExpected)
        {
            try
            {
                string lsName = pOData.Strings.Blocks[UWEnchantment.SpellStringBlock]
                    .Strings[((piMajor << 4) | piMinor) + 1];

                return lsName != null && lsName.TrimEnd('\r', '\n') == psExpected;
            }
            catch
            {
                return false;
            }
        }
    }
}
