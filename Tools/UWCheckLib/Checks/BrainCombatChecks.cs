using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The creature brain, part of the CritterBrain group: the execution arithmetic of
    /// UWCritterCombat and the player as the defender.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// Spec 8.3-8.7, deviation rows 45-51: the execution arithmetic of
        /// UWCritterCombat - the skill check bands at the four thresholds, the flank bonus at
        /// four facing differences, the strong bonuses at their ends, the dice bounds for a
        /// base value, the charge table, the missile charge, the poison gate, the armour byte
        /// fallback, the experience with and without the strong factor, the corpse roll.
        /// </summary>
        private static void fCheckCombatExecution()
        {
            // To-hit: score - protection + flank - defence + roll, banded by SkillCheck_seg037_32E6_C.
            fExpectInt("to-hit: 2 is a critical miss", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 0)), (int)UWSkillCheck.ResultEnum.CriticalFailure);
            fExpectInt("to-hit: 3 is a miss", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 1)), (int)UWSkillCheck.ResultEnum.Failure);
            fExpectInt("to-hit: 15 is a miss", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 13)), (int)UWSkillCheck.ResultEnum.Failure);
            fExpectInt("to-hit: 16 is a hit", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 14)), (int)UWSkillCheck.ResultEnum.Success);
            fExpectInt("to-hit: 28 is a hit", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 26)), (int)UWSkillCheck.ResultEnum.Success);
            fExpectInt("to-hit: 29 is a critical hit", (int)UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(10, 0, 0, 8, 27)), (int)UWSkillCheck.ResultEnum.CriticalSuccess);
            fExpectInt("to-hit: flank adds, protection subtracts", UWCritterCombat.GetHitScore(10, 4, 3, 8, 0), 3);
            fExpectInt("to-hit: the roll is taken modulo 31", UWCritterCombat.GetHitScore(0, 0, 0, 0, 31), 0);

            // CalcFlankingBonus_seg022_230E_D48: d = (defender + 12 - attacker) & 7, d or 8 - d.
            fExpectInt("flank: from the front is 0", UWCritterRules.GetFlankingBonus(0, 4), 0);
            fExpectInt("flank: one eighth off the front is 1", UWCritterRules.GetFlankingBonus(0, 3), 1);
            fExpectInt("flank: from the side is 2", UWCritterRules.GetFlankingBonus(0, 2), 2);
            fExpectInt("flank: from the other side is 2", UWCritterRules.GetFlankingBonus(0, 6), 2);
            fExpectInt("flank: from behind is 4", UWCritterRules.GetFlankingBonus(0, 0), 4);
            fExpectInt("flank: wraps at 7 to 1", UWCritterRules.GetFlankingBonus(7, 3), 0);

            // The strong individual's bonuses (NPCExecuteAttack label 1679).
            fExpectInt("strong: to-hit at least 7", UWCritterCombat.GetStrongScoreBonus(0), 7);
            fExpectInt("strong: to-hit at most 12", UWCritterCombat.GetStrongScoreBonus(5), 12);
            fExpectInt("strong: damage at least 4", UWCritterCombat.GetStrongDamageBonus(0), 4);
            fExpectInt("strong: damage at most 15", UWCritterCombat.GetStrongDamageBonus(11), 15);

            // The score and damage of an attack from the row: signed to-hit and byte 0x11.
            byte[] lyRow = new byte[48];

            lyRow[5] = 12;
            lyRow[0x11] = 0xFD;
            lyRow[0x13] = 5;
            lyRow[0x14] = 4;
            lyRow[0x16] = 0xFE;
            lyRow[0x17] = 9;

            UWObjectClassProperties.Critter lOKind = new UWObjectClassProperties.Critter { Row = lyRow };

            fExpectInt("attack: score = to-hit + (signed 0x11 >> 1)", UWCritterCombat.GetAttackScore(lOKind, 0), 3);
            fExpectInt("attack: a negative to-hit byte counts", UWCritterCombat.GetAttackScore(lOKind, 1), -4);
            fExpectInt("attack: damage = byte + strength / 5", UWCritterCombat.GetAttackDamage(lOKind, 0), 6);
            fExpectInt("attack: the second attack's damage", UWCritterCombat.GetAttackDamage(lOKind, 1), 11);

            // The critical multiplier ((RNG & 31) + 48) >> 5.
            fExpectInt("critical: low nibble below 16 gives x1", UWCritterCombat.GetCriticalMultiplier(15), 1);
            fExpectInt("critical: 16 gives x2", UWCritterCombat.GetCriticalMultiplier(16), 2);
            fExpectInt("critical: only the low five bits count", UWCritterCombat.GetCriticalMultiplier(32 + 3), 1);

            // The dice: D = max(2, base), (D / 6)d6 + 1d(D % 6), over many rolls within the bounds.
            UWRandom.UseSeed(1220);

            int liMinimum;
            int liMaximum;

            UWCritterCombat.GetDamageBounds(8, out liMinimum, out liMaximum);
            fExpectInt("dice: bounds of 8 are 2..8, low", liMinimum, 2);
            fExpectInt("dice: bounds of 8 are 2..8, high", liMaximum, 8);
            fExpectBool("dice: 400 rolls of 8 stay within 2..8", fRollsWithin(8, 2, 8, 400), true);
            fExpectBool("dice: a base below 2 rolls as 2 (1..2)", fRollsWithin(1, 1, 2, 200), true);
            fExpectBool("dice: 6 is one die (1..6)", fRollsWithin(6, 1, 6, 400), true);
            fExpectBool("dice: 12 is two dice (2..12)", fRollsWithin(12, 2, 12, 400), true);

            UWRandom.Use(null);

            // The charge table seg060 and rolled * charge >> 7 + flank.
            fExpectInt("charge: table starts at 50", UWCritterRules.ChargeTable[0], 50);
            fExpectInt("charge: index 5 is 100", UWCritterRules.ChargeTable[5], 100);
            fExpectInt("charge: table ends at 255", UWCritterRules.ChargeTable[UWCritterRules.ChargeIndexMax], 255);
            fExpectInt("charge: 16 entries", UWCritterRules.ChargeTable.Length, 16);
            fExpectInt("charge: 128 leaves the roll", UWCritterCombat.ScaleByCharge(10, 128, 0), 10);
            fExpectInt("charge: 50 gives 10 * 50 >> 7 = 3", UWCritterCombat.ScaleByCharge(10, 50, 0), 3);
            fExpectInt("charge: 255 gives 19, plus flank 2", UWCritterCombat.ScaleByCharge(10, 255, 2), 21);

            // Missiles: charge 0x80, part + 4, no roll.
            fExpectInt("missile: charge is 0x80", UWCritterRules.MissileCharge, 0x80);
            fExpectInt("missile: body part + 4", UWCritterCombat.GetMissileBodyPart(2), 6);
            fExpectInt("missile: part 7 uses armour byte 3", new UWObjectClassProperties.Critter { Row = new byte[] { 3, 2, 2, 8 } }.ArmourOfPart(7), 8);

            UWRandom.UseSeed(7);
            fExpectBool("missile: damage 6 at charge 0x80 rolls 1..6", fMissileRollsWithin(6, 1, 6, 400), true);
            UWRandom.Use(null);

            // Poison: nibble below byte 0x0F and no resistance, nothing else.
            fExpectBool("poison: 0 below 4 poisons", UWCritterCombat.ShouldPoison(0, 4, 0), true);
            fExpectBool("poison: 3 below 4 poisons", UWCritterCombat.ShouldPoison(3, 4, 0), true);
            fExpectBool("poison: 4 is not below 4", UWCritterCombat.ShouldPoison(4, 4, 0), false);
            fExpectBool("poison: a byte of 0 never poisons", UWCritterCombat.ShouldPoison(0, 0, 0), false);
            fExpectBool("poison: a Poison Resistance rules it out", UWCritterCombat.ShouldPoison(0, 4, UWDamageTypes.Poison), false);
            fExpectInt("poison: writes byte 0x0F & 0xF", UWCritterCombat.GetPoisonNibble(0x14), 4);

            // The armour bytes: row[part % 4], 0xFF falls back to byte 0, strong x 5 / 3.
            UWObjectClassProperties.Critter lOOneValue = new UWObjectClassProperties.Critter { Row = new byte[] { 2, 0xFF, 0xFF, 0xFF } };
            UWObjectClassProperties.Critter lOFourValues = new UWObjectClassProperties.Critter { Row = new byte[] { 3, 2, 2, 8 } };

            fExpectInt("armour: byte 0 is part 0", lOFourValues.ArmourOfPart(0), 3);
            fExpectInt("armour: byte 3 is part 3", lOFourValues.ArmourOfPart(3), 8);
            fExpectInt("armour: part 4 wraps to byte 0", lOFourValues.ArmourOfPart(4), 3);
            fExpectInt("armour: 0xFF in byte 1 falls back to byte 0", lOOneValue.ArmourOfPart(1), 2);
            fExpectInt("armour: 0xFF in byte 3 falls back to byte 0", lOOneValue.ArmourOfPart(3), 2);
            fExpectInt("armour: a strong defender has 3 * 5 / 3 = 5", UWCritterCombat.GetCreatureArmour(lOFourValues, 0, true), 5);
            fExpectInt("armour: a strong defender's 8 becomes 13", UWCritterCombat.GetCreatureArmour(lOFourValues, 3, true), 13);
            fExpectInt("armour: equal to the damage leaves 0", UWCritterCombat.SubtractArmour(5, 5), 0);
            fExpectInt("armour: one above leaves 1", UWCritterCombat.SubtractArmour(6, 5), 1);
            fExpectInt("difficulty: easy halves 5 to 2", UWCritterCombat.HalveOnEasy(5, true), 2);
            fExpectInt("difficulty: standard keeps 5", UWCritterCombat.HalveOnEasy(5, false), 5);

            // The aftermath: effect size, HitZ, blood or flash.
            fExpectInt("effect: size is damage / 4", UWCritterCombat.GetEffectSize(11), 2);
            fExpectInt("effect: size caps at 3", UWCritterCombat.GetEffectSize(16), 3);
            fExpectInt("effect: HitZ of the head is 7", UWCritterCombat.GetHitZ(UWArmourProtection.PartHead), 7);
            fExpectInt("effect: HitZ of a missile part is 0", UWCritterCombat.GetHitZ(4), 0);
            fExpectInt("effect: byte 8 bits 3-4 set bleeds (448)", UWObjectMechanics.GetHitEffectObjectId(0x28), UWObjectMechanics.BloodEffectObjectId);
            fExpectInt("effect: byte 8 bits 3-4 clear flashes (459)", UWObjectMechanics.GetHitEffectObjectId(0x80), UWObjectMechanics.FlashEffectObjectId);
            fExpectInt("effect: blood is class 7 offset 0", UWObjectMechanics.BloodEffectObjectId, 0x1C0);
            fExpectInt("effect: flash is class 7 offset 0x0B", UWObjectMechanics.FlashEffectObjectId, 0x1CB);
            fExpectInt("critical: the off hand for a body hit, right-handed", UWCritterCombat.GetCriticalEquipmentSlot(0, true, 0), 8);
            fExpectInt("critical: the off hand for a hands hit, left-handed", UWCritterCombat.GetCriticalEquipmentSlot(1, false, 0), 7);
            fExpectInt("critical: the helmet for a head hit", UWCritterCombat.GetCriticalEquipmentSlot(3, true, 0), 0);
            fExpectInt("critical: the boots for a leg hit 1 in 5", UWCritterCombat.GetCriticalEquipmentSlot(2, true, 0), 4);
            fExpectInt("critical: else the leggings", UWCritterCombat.GetCriticalEquipmentSlot(2, true, 1), 3);

            // AwardKillEXP: 4 * base + 2d(base), strong x (24 + RNG(24)) / 16, no halving.
            fExpectInt("experience: 4 * 6 + dice 7 = 31", UWCritterCombat.GetKillExperience(6, 7, -1), 31);
            fExpectInt("experience: strong at the low roll is 31 * 24 / 16", UWCritterCombat.GetKillExperience(6, 7, 0), 46);
            fExpectInt("experience: strong at the high roll is 31 * 47 / 16", UWCritterCombat.GetKillExperience(6, 7, 23), 91);
            fExpectInt("experience: base 0 yields nothing", UWCritterCombat.GetKillExperience(0, 2, -1), 0);

            UWRandom.UseSeed(3);
            fExpectBool("experience: 200 rolls for base 6 stay within 26..36", fExperienceWithin(6, 26, 36, 200), true);
            UWRandom.Use(null);

            // EXPChange_seg037_48 (123406-123418): above 96000 tenths nothing is awarded any
            // more - measured against the original 2026-09-20 with a character at 100000.
            UWExperience.State lOAt100000 = fMakeExperienceState(100000, 16, 4, 34);

            fExpectInt("experience gate: nothing above 96000", fChange(ref lOAt100000, 30, 1), 100000);
            fExpectInt("experience gate: no skill point above 96000", lOAt100000.SkillPoints, 4);

            UWExperience.State lOAtCap = fMakeExperienceState(UWExperience.AwardCap, 16, 4, 34);

            fExpectInt("experience gate: exactly at 96000 still counts", fChange(ref lOAtCap, 30, 7), UWExperience.AwardCap + 30);

            UWExperience.State lOShallow = fMakeExperienceState(1000, 16, 0, 0);

            fExpectInt("experience: a level 16 character on dungeon level 1 gets 1 + half",
                fChange(ref lOShallow, 30, 1), 1016);

            UWExperience.State lODeep = fMakeExperienceState(1000, 16, 0, 0);

            fExpectInt("experience: from dungeon level 7 the full amount", fChange(ref lODeep, 30, 7), 1030);

            UWExperience.State lOLoss = fMakeExperienceState(100000, 16, 4, 34);

            fExpectInt("experience: a loss still works above the gate", fChange(ref lOLoss, -1000, 1), 99000);

            // DropNPCRemains: corpse 0xC0 + n with RNG(16) < 7, blood 0xD8 + n.
            fExpectBool("corpse: roll 6 drops", UWCritterCombat.ShouldDropCorpse(6, 6), true);
            fExpectBool("corpse: roll 7 does not", UWCritterCombat.ShouldDropCorpse(6, 7), false);
            fExpectBool("corpse: index 0 never drops", UWCritterCombat.ShouldDropCorpse(0, 0), false);
            fExpectInt("corpse: index 6 is object 0xC6", UWCritterCombat.GetCorpseObjectId(6), 0xC6);
            fExpectInt("corpse: byte 0x0A 0x38 gives index 6", new UWObjectClassProperties.Critter { Row = fRowWith(0x0A, 0x38) }.CorpseIndex, 6);
            fExpectInt("corpse: the owner carries the kind (build_corpse)", UWCritterCombat.GetCorpseOwner(0x4E), 0x0E);
            fExpectInt("blood: byte 8 0x28 gives object 217", UWCritterCombat.GetBloodObjectId(new UWObjectClassProperties.Critter { Row = fRowWith(8, 0x28) }.BloodIndex), 217);
            fExpectInt("blood: index 0 gives none", UWCritterCombat.GetBloodObjectId(0), -1);
            fExpectInt("blood: GetRemainsObjectId agrees", UWObjectMechanics.GetRemainsObjectId(0xE8), 0xD8 + 7);
        }

        private static byte[] fRowWith(int piOffset, byte pyValue)
        {
            byte[] lyRow = new byte[48];

            lyRow[piOffset] = pyValue;

            return lyRow;
        }

        private static bool fRollsWithin(int piBase, int piMinimum, int piMaximum, int piCount)
        {
            for (int liRoll = 0; liRoll < piCount; liRoll++)
            {
                int liValue = UWCritterCombat.RollDamage(piBase);

                if (liValue < piMinimum || liValue > piMaximum)
                    return false;
            }

            return true;
        }

        private static bool fMissileRollsWithin(int piAmmoDamage, int piMinimum, int piMaximum, int piCount)
        {
            for (int liRoll = 0; liRoll < piCount; liRoll++)
            {
                int liValue = UWCritterCombat.RollMissileDamage(piAmmoDamage);

                if (liValue < piMinimum || liValue > piMaximum)
                    return false;
            }

            return true;
        }

        /// <summary>A character's experience state for the checks above.</summary>
        private static UWExperience.State fMakeExperienceState(int piExperience, int piLevel,
            int piSkillPoints, int piSkillPointsTotal)
        {
            return new UWExperience.State
            {
                Experience = piExperience,
                Level = piLevel,
                SkillPoints = piSkillPoints,
                SkillPointsTotal = piSkillPointsTotal,
            };
        }

        /// <summary>Applies a change and returns the experience afterwards.</summary>
        private static int fChange(ref UWExperience.State pOState, int piAmount, int piDungeonLevel)
        {
            UWExperience.Change(ref pOState, piAmount, piDungeonLevel);

            return pOState.Experience;
        }

        private static bool fExperienceWithin(int piBase, int piMinimum, int piMaximum, int piCount)
        {
            for (int liRoll = 0; liRoll < piCount; liRoll++)
            {
                int liValue = UWCritterCombat.GetKillExperience(piBase, false);

                if (liValue < piMinimum || liValue > piMaximum)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Spec 8.8: the player is row 63 of the creature table, and
        /// PlayerStatusUpdate_ovr133_784 (382211) fills it. Pinned here without data: the
        /// defence value of row byte 0x12 and the quality scaling of the armour bytes. The
        /// whole pass over a worn set is in fCheckPlayerStatus, which needs OBJECTS.DAT.
        /// </summary>
        private static void fCheckPlayerAsDefender()
        {
            // Row byte 0x12 = PLAYER.DAT[0x22] + PLAYER.DAT[0x21 + var_6] / 2 (labels 86A-938).
            fExpectInt("defence: an empty hand is the Defence skill alone", UWPlayerCritterRow.GetDefence(30, 0), 30);
            fExpectInt("defence: skill 30 with a weapon skill of 25 is 42", UWPlayerCritterRow.GetDefence(30, 25), 42);
            fExpectInt("defence: the weapon skill is halved downwards", UWPlayerCritterRow.GetDefence(0, 7), 3);
            fExpectInt("defence: a weapon skill of 1 adds nothing", UWPlayerCritterRow.GetDefence(10, 1), 10);

            // THE USER MEASURED THIS IN THE ORIGINAL ON 2026-09-20: a reaper (item 119, attack 0
            // to-hit 18 and row byte 0x11 = 25, so an attack score of 18 + 12 = 30) NEVER
            // flashed the critical colour 0xB8 at a character with Defence skill 30 - while our
            // port did. A critical needs score - check + RNG(31) > 28, and the roll reaches 30,
            // so it is impossible from check >= score + 2 on. The skill alone leaves 2 of 31
            // rolls; half a weapon skill of 4 already closes it.
            const int ReaperAttackScore = 30;

            fExpectInt("reaper: the bare Defence skill leaves 2 critical rolls of 31",
                fCountCriticalRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 0)), 2);
            fExpectInt("reaper: skill 30 plus unarmed 3 leaves 1 of 31",
                fCountCriticalRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 3)), 1);
            fExpectInt("reaper: skill 30 plus unarmed 4 makes a critical impossible",
                fCountCriticalRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 4)), 0);
            fExpectInt("reaper: skill 30 plus sword 25 makes a critical impossible",
                fCountCriticalRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 25)), 0);
            // It still lands blows: with the check at 42 the roll has to reach 28 of 30, so
            // roughly one swing in ten gets through - and none of them as a critical.
            fExpectInt("reaper: skill 30 plus sword 25 still lets 3 of 31 rolls hit",
                fCountHitRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 25)), 3);
            fExpectInt("reaper: against the bare skill 15 of 31 rolls hit",
                fCountHitRolls(ReaperAttackScore, UWPlayerCritterRow.GetDefence(30, 0)), 15);

            // The armour bytes: ((table protection * quality) >> 6) + 1 per piece (ovr133_72D).
            // A full plate mail is 6 of 6, a worn one less, a ruined one still 1.
            fExpectInt("armour: plate mail (6) at full quality gives 6", UWPlayerCritterRow.ScaleArmourValue(6, 63), 6);
            fExpectInt("armour: the same at quality 40 gives 4", UWPlayerCritterRow.ScaleArmourValue(6, 40), 4);
            fExpectInt("armour: the same at quality 10 gives 1", UWPlayerCritterRow.ScaleArmourValue(6, 10), 1);
            fExpectInt("armour: a ruined piece still gives 1", UWPlayerCritterRow.ScaleArmourValue(6, 0), 1);
            fExpectInt("armour: a tower shield (3) at full quality gives 3", UWPlayerCritterRow.ScaleArmourValue(3, 63), 3);

            // Only 0x3B to 0x3F count as a shield in the off hand (labels 80E-840).
            fExpectBool("shield: 0x3B counts", UWPlayerCritterRow.IsShield(0x3B), true);
            fExpectBool("shield: 0x3F counts", UWPlayerCritterRow.IsShield(0x3F), true);
            fExpectBool("shield: 0x3A does not", UWPlayerCritterRow.IsShield(0x3A), false);
            fExpectBool("shield: a sword does not", UWPlayerCritterRow.IsShield(0x03), false);
        }

        /// <summary>How many of the 31 rolls of SkillCheck_seg037_32E6_C end as a critical hit -
        /// the red flash at the player.</summary>
        private static int fCountCriticalRolls(int piAttackScore, int piDefence)
        {
            int liCriticals = 0;

            for (int liRoll = 0; liRoll < UWCritterRules.SkillCheckRoll; liRoll++)
            {
                if (UWSkillCheck.GetResult(UWCritterCombat.GetHitScore(piAttackScore, 0, 0, piDefence, liRoll))
                    == UWSkillCheck.ResultEnum.CriticalSuccess)
                    liCriticals++;
            }

            return liCriticals;
        }

        /// <summary>How many of the 31 rolls land as a hit or a critical hit.</summary>
        private static int fCountHitRolls(int piAttackScore, int piDefence)
        {
            int liHits = 0;

            for (int liRoll = 0; liRoll < UWCritterRules.SkillCheckRoll; liRoll++)
            {
                if (UWSkillCheck.IsSuccess(UWSkillCheck.GetResult(
                    UWCritterCombat.GetHitScore(piAttackScore, 0, 0, piDefence, liRoll))))
                    liHits++;
            }

            return liHits;
        }
    }
}
