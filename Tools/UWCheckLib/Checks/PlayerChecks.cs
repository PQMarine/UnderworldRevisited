using System;
using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The player's own rules read out of the disassembly one by one: the sound settings in the
    /// save, the impact, the hit volume, the manual attributes, the orb's mana and the stealth.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// Music and sound belong to the SAVE, not to the program: PLAYER.DAT 0xB5 carries
        /// both, bits 0-1 the sound effects and bits 2-3 the music, and the loading routine
        /// hands them straight to the two toggles (ovr133 labels 1A4-1C2, 380792-380822; the
        /// writing side is labels 8E-B9). Found on 2026-09-21 after the user tried it in the
        /// original: switch the music off, save, load again and it is still off, while another
        /// save keeps what it was saved with.
        ///
        /// Pinned on the reading side; the writer flips the same two bit pairs with the same
        /// constants.
        /// </summary>
        private static void fCheckSoundInTheSave()
        {
            Check.Group("Sound settings in the save");

            // 0x35 is what the original writes into a fresh save: 01 sound, 01 music, 11 detail.
            fExpectBool("sound in save: the fresh 0x35 has the sound on",
                fReadSetting(0x35).SoundEnabled, true);
            fExpectBool("sound in save: and the music on", fReadSetting(0x35).MusicEnabled, true);

            fExpectBool("sound in save: clearing bits 0-1 turns the sound off",
                fReadSetting(0x34).SoundEnabled, false);
            fExpectBool("sound in save: and leaves the music alone",
                fReadSetting(0x34).MusicEnabled, true);

            fExpectBool("sound in save: clearing bits 2-3 turns the music off",
                fReadSetting(0x31).MusicEnabled, false);
            fExpectBool("sound in save: and leaves the sound alone",
                fReadSetting(0x31).SoundEnabled, true);

            fExpectBool("sound in save: the detail bits do not reach either",
                fReadSetting(0x05).SoundEnabled && fReadSetting(0x05).MusicEnabled, true);
        }

        /// <summary>A plain PLAYER.DAT block with nothing in it but the settings byte.</summary>
        private static UWPlayerData fReadSetting(int piValue)
        {
            byte[] lyPlain = new byte[320];

            lyPlain[0xB5] = (byte)piValue;

            return UWPlayerData.FromPlain(lyPlain);
        }

        /// <summary>
        /// The impact block of ApplyPlayerMotion_seg008_90D (labels AF3 to B95), read out on
        /// 2026-09-22 because the user could not provoke the wall impact reliably. The answer
        /// is the Acrobat skill.
        /// </summary>
        private static void fCheckImpact()
        {
            Check.Group("Impact");

            // A full run is 0x3AC of momentum, so three points - enough to be heard, one short
            // of hurting.
            int liValue = UWImpactRules.GetImpactValue(0x3AC, false);

            fExpectInt("impact: a full run is worth three", liValue, 3);
            fExpectBool("impact: which is heard", UWImpactRules.PlaysSound(liValue, false), true);
            fExpectBool("impact: but does not hurt", UWImpactRules.CausesDamage(liValue), false);
            fExpectInt("impact: at volume three times four less sixty",
                UWImpactRules.GetVolume(liValue), -48);

            fExpectInt("impact: falling doubles it", UWImpactRules.GetImpactValue(0x3AC, true), 6);
            fExpectBool("impact: and that hurts", UWImpactRules.CausesDamage(6), true);

            // Half a run is one point: silent on dry ground, a splash over water.
            fExpectInt("impact: half a run is worth one", UWImpactRules.GetImpactValue(0x1D6, false), 1);
            fExpectBool("impact: one is not heard on dry ground",
                UWImpactRules.PlaysSound(1, false), false);
            fExpectBool("impact: over water it is", UWImpactRules.PlaysSound(1, true), true);
            fExpectBool("impact: and nothing at all is still heard over water",
                UWImpactRules.PlaysSound(0, true), true);

            // THE ACROBAT SKILL is why the user could not provoke it. Henrietta carries 25 in
            // PLAYER.DAT 0x32 in all four saves.
            fExpectInt("impact: a failed check leaves the three whole",
                UWImpactRules.ApplyAcrobat(3, 25, false), 3);
            fExpectInt("impact: Henrietta's Acrobat 25 takes it to nothing",
                UWImpactRules.ApplyAcrobat(3, 25, true), 0);
            fExpectBool("impact: so she hears no wall",
                UWImpactRules.PlaysSound(UWImpactRules.ApplyAcrobat(3, 25, true), false), false);
            fExpectInt("impact: a beginner keeps it even on a passed check",
                UWImpactRules.ApplyAcrobat(3, 0, true), 3);
            fExpectInt("impact: Acrobat 30 takes everything",
                UWImpactRules.ApplyAcrobat(9, 30, true), 0);
            fExpectInt("impact: the check is rolled against twice the value",
                UWImpactRules.GetAcrobatTarget(3), 6);

            // THE JUMP IS THE ANCHOR of the fall: it takes off with 0x263 and comes down at the
            // same speed, so it must sound and must not hurt. That is what our conversion of a
            // fall speed into momentum was calibrated on (UWPlayerMovement.fReportLanding, the
            // CharacterController path's, removed in cleanup 4.1 on 2026-10-18).
            int liJump = UWImpactRules.GetImpactValue(UWImpactRules.JumpMomentum, false);

            fExpectInt("impact: a jump on the spot is worth two", liJump, 2);
            fExpectBool("impact: it is heard", UWImpactRules.PlaysSound(liJump, false), true);
            fExpectBool("impact: and does not hurt", UWImpactRules.CausesDamage(liJump), false);
            fExpectInt("impact: at volume minus fifty-two", UWImpactRules.GetVolume(liJump), -52);
            fExpectBool("impact: a fall of twice that speed does hurt",
                UWImpactRules.CausesDamage(UWImpactRules.GetImpactValue(UWImpactRules.JumpMomentum * 2, false)), true);
        }

        /// <summary>
        /// The volume of the hit sound comes from the damage BEFORE the armour
        /// (AttackerAppliesFinalDamage_seg022_8A5: effect 3 or 4 at label 938, the armour only
        /// at labels A04-A10). Corrected on the player's own blow on 2026-09-22; the
        /// creature's blows had it right since C3.
        /// </summary>
        private static void fCheckHitVolume()
        {
            Check.Group("Hit volume");

            // Armour that swallows everything: no damage, but the sound still has a value.
            int liBefore;
            int liDamage = UWCombat.ComputeDamage(20, false, 30, 0, 128, 1000, out liBefore);

            fExpectInt("hit volume: armour that swallows the blow leaves no damage", liDamage, 0);
            fExpectBool("hit volume: the sound is still loud", liBefore > 0, true);

            // Without armour the two are the same.
            liDamage = UWCombat.ComputeDamage(20, false, 30, 0, 128, 0, out liBefore);

            fExpectInt("hit volume: without armour both are the same", liDamage, liBefore);

            // With armour the difference is exactly the armour.
            liDamage = UWCombat.ComputeDamage(40, false, 30, 0, 128, 3, out liBefore);

            fExpectBool("hit volume: with armour the sound is louder than the damage by it",
                liDamage == 0 || liBefore - liDamage == 3, true);

            // THE EYES (do_damage, COMBAT.C): fx = hp * 3 / avghit, at most 2, frame 3 - fx.
            fExpectInt("eyes: at the table average healthy", UWCombat.GetEyesGroup(30, 30), 0);
            fExpectInt("eyes: above it healthy too", UWCombat.GetEyesGroup(50, 30), 0);
            fExpectInt("eyes: 20 of 30 still healthy", UWCombat.GetEyesGroup(20, 30), 0);
            fExpectInt("eyes: 19 of 30 wounded", UWCombat.GetEyesGroup(19, 30), 1);
            fExpectInt("eyes: 9 of 30 critical", UWCombat.GetEyesGroup(9, 30), 2);
            fExpectInt("eyes: an average of 0 is always critical", UWCombat.GetEyesGroup(40, 0), 2);

            // THE EFFECT'S PART (do_damage hitloc &= 4 for an armour byte 0xFF).
            UWObjectClassProperties.Critter lORow = new UWObjectClassProperties.Critter { Row = new byte[0x30] };

            lORow.Row[1] = 0xFF;
            fExpectInt("effect part: a part with its own armour keeps its height", UWCritterCombat.GetEffectPart(lORow, 2), 2);
            fExpectInt("effect part: a blow on an 0xFF part splashes at the body", UWCritterCombat.GetEffectPart(lORow, 1), 0);
            fExpectInt("effect part: a missile on it at the missile's height", UWCritterCombat.GetEffectPart(lORow, 5), 4);

            // THE BLOW'S PROBE (resolve_attack) and pickloc against it (C3/C6, 2026-10-12).
            UWCritterCombat.GetBlowProbe(0, 30, 9, 1, out int liProbeBottom, out int liProbeTop);
            fExpectInt("probe: ninth 9 sits a whole body up", liProbeBottom, 30);
            fExpectInt("probe: reach 1 is 12 high", liProbeTop - liProbeBottom, 12);
            UWCritterCombat.GetBlowProbe(0, 30, 2, 2, out liProbeBottom, out liProbeTop);
            fExpectInt("probe: ninth 2 stays at the feet", liProbeBottom, 0);
            UWCritterCombat.GetBlowProbe(0, 30, 3, 2, out liProbeBottom, out liProbeTop);
            fExpectInt("probe: ninth 3 (right column) is a third up", liProbeBottom, 10);
            fExpectInt("pickloc: a probe middle above the top is the head",
                UWArmourProtection.PickBodyPart(0, 20, 16, 28), UWArmourProtection.PartHead);
            fExpectInt("pickloc: a probe middle below the feet is the legs",
                UWArmourProtection.PickBodyPart(10, 30, 0, 12), UWArmourProtection.PartLegs);

            // THE CONDITION WORD (LOOK.C): step 0 only at quality 0, class 3 from quality 1.
            fExpectInt("condition: quality 0 is step 0", UWCombat.GetConditionStringIndex(10, 0), 60);
            fExpectInt("condition: a torch of quality 1 is step 1", UWCombat.GetConditionStringIndex(10, 1), 61);
            fExpectInt("condition: a lantern of 21 is step 2", UWCombat.GetConditionStringIndex(10, 21), 62);
            fExpectInt("condition: class 3 at quality 1 is the top word", UWCombat.GetConditionStringIndex(0, 1, 3), 5);
            fExpectInt("condition: quality 63 is step 4", UWCombat.GetConditionStringIndex(1, 63), 10);
        }

        /// <summary>The player deals a fighter's 12 bonus points himself (Str 20, Dex 16, Int 12):
        /// never below the class value, never above 30, done only with none left.</summary>
        private static void fCheckManualAttributes(DataImport pOData)
        {
            UWCharacterGeneration lOGeneration = new UWCharacterGeneration(pOData.Strings, pOData.MiscDataFiles);

            if (!lOGeneration.IsLoaded)
            {
                fFail("manual attributes", "character creation data not loaded");
                return;
            }

            lOGeneration.ManualAttributes = true;
            lOGeneration.Begin();
            lOGeneration.Choose(0);
            lOGeneration.Choose(0);
            lOGeneration.Choose(0);

            fExpectBool("manual attributes: stage after the class", lOGeneration.CurrentStage == UWCharacterGeneration.Stage.Attributes, true);
            fExpectInt("manual attributes: points", lOGeneration.BonusPointsLeft, 12);

            lOGeneration.Choose(6);
            fExpectBool("manual attributes: no done with points left", lOGeneration.CurrentStage == UWCharacterGeneration.Stage.Attributes, true);

            lOGeneration.Choose(1);
            fExpectInt("manual attributes: not below the class value", lOGeneration.Strength, 20);

            for (int liAt = 0; liAt < 11; liAt++)
                lOGeneration.Choose(0);

            fExpectInt("manual attributes: capped at 30", lOGeneration.Strength, 30);
            fExpectInt("manual attributes: points after the cap", lOGeneration.BonusPointsLeft, 2);

            lOGeneration.Choose(1);
            lOGeneration.Choose(4);
            lOGeneration.Choose(4);
            lOGeneration.Choose(4);

            fExpectInt("manual attributes: strength given back", lOGeneration.Strength, 29);
            fExpectInt("manual attributes: intelligence", lOGeneration.Intelligence, 15);
            fExpectInt("manual attributes: none left", lOGeneration.BonusPointsLeft, 0);

            lOGeneration.Choose(6);
            fExpectBool("manual attributes: on to the skills", lOGeneration.CurrentStage == UWCharacterGeneration.Stage.Skill, true);
        }

        /// <summary>ovr143_95: a Mana gain in Tybal's lair goes into the orb's backup, the flask stays
        /// "0 of 0"; elsewhere, or with the orb broken, into the maximum. The maximum is recomputed
        /// by the shrine's player_compute (RecomputeMaxima), not by the raise itself.</summary>
        private static void fCheckOrbManaAside()
        {
            bool lbWasDestroyed = UWGameFlags.OrbDestroyed;
            int liWasBackup = UWGameFlags.OrbManaBackup;
            int liManaSkill = (int)UWPlayerData.Skill.Mana + 2;

            UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

            lOStart.Intelligence = 24;
            lOStart.ManaSkill = 5;

            FakeVitalsHost lOHost = new FakeVitalsHost { DungeonLevelIndex = UWTybalOrbRules.LairLevelIndex };
            UWPlayerVitals lOVitals = new UWPlayerVitals(lOHost);

            lOVitals.Initialise(null, lOStart);
            UWGameFlags.OrbDestroyed = false;
            UWGameFlags.OrbManaBackup = 18;
            lOVitals.SetMana(0, 0);

            fExpectBool("orb mana: a Mana gain in the lair", lOVitals.TryIncreaseSkill(liManaSkill), true);
            lOVitals.RecomputeMaxima();
            fExpectInt("orb mana: the maximum stays 0", (int)lOVitals.MaxMana, 0);
            fExpectInt("orb mana: the backup holds the new maximum",
                UWGameFlags.OrbManaBackup, UWExperience.GetMaximumMana(lOVitals.GetSkill(UWPlayerData.Skill.Mana), 24));

            lOHost.DungeonLevelIndex = UWTybalOrbRules.LairLevelIndex - 1;
            lOVitals.TryIncreaseSkill(liManaSkill);
            lOVitals.RecomputeMaxima();
            fExpectInt("orb mana: elsewhere the maximum",
                (int)lOVitals.MaxMana, UWExperience.GetMaximumMana(lOVitals.GetSkill(UWPlayerData.Skill.Mana), 24));

            // Leaving the lair: the maximum from the backup, a quarter of it as current mana.
            lOHost.DungeonLevelIndex = UWTybalOrbRules.LairLevelIndex;
            lOVitals.SetMana(0, 0);
            UWGameFlags.OrbManaBackup = 17;
            UWTybalOrbRules.OnLevelChange(lOVitals, UWTybalOrbRules.LairLevelIndex, UWTybalOrbRules.LairLevelIndex - 1);
            fExpectInt("orb mana: leaving the lair, the maximum back", (int)lOVitals.MaxMana, 17);
            fExpectInt("orb mana: leaving the lair, a quarter as current", (int)lOVitals.CurrentMana, 4);

            UWGameFlags.OrbDestroyed = lbWasDestroyed;
            UWGameFlags.OrbManaBackup = liWasBackup;
        }

        /// <summary>
        /// The stealth check, built 2026-09-21: the three class-3 spells Stealth, Conceal and
        /// Invisibility become bits 1 to 3 of one accumulator (Class3_Bonuses_ovr133_38E,
        /// label 3BD: 1 shifted by minor class minus one), and the first loop of
        /// ApplyDefenceStealthBonuses_ovr133_65D turns them into the two bases that
        /// ApplyPlayerSneakScore_seg034_2F89_898 writes into the player's row byte 0x1D.
        /// The names come from string block 6 at 16 * major + minor + 1 - 51 Stealth,
        /// 52 Conceal, 53 Invisibilty.
        /// </summary>
        private static void fCheckStealth(DataImport pOData)
        {
            Check.Group("Stealth");

            // The bits themselves, the way the class-3 branch builds them.
            List<UWActiveSpellEffect> lOSpells = new List<UWActiveSpellEffect>();
            UWArmourProtection.Status lOStatus = new UWArmourProtection.Status();

            lOSpells.Add(new UWActiveSpellEffect { MajorClass = 3, MinorClass = 2, Stability = 1 });
            UWArmourProtection.Compute(null, pOData, lOSpells, lOStatus);
            fExpectInt("stealth: the Stealth spell sets bit 1", lOStatus.StealthBonus,
                UWArmourProtection.StealthNoiseBit);

            lOSpells.Clear();
            lOSpells.Add(new UWActiveSpellEffect { MajorClass = 3, MinorClass = 3, Stability = 1 });
            UWArmourProtection.Compute(null, pOData, lOSpells, lOStatus);
            fExpectInt("stealth: Conceal sets bit 2", lOStatus.StealthBonus,
                UWArmourProtection.ConcealVisibilityBit);

            lOSpells.Clear();
            lOSpells.Add(new UWActiveSpellEffect { MajorClass = 3, MinorClass = 4, Stability = 1 });
            UWArmourProtection.Compute(null, pOData, lOSpells, lOStatus);
            fExpectInt("stealth: Invisibility sets bit 3", lOStatus.StealthBonus,
                UWArmourProtection.InvisibilityBit);

            // Minor class 1 is Curse, not a stealth bonus - the loop skips bit 0.
            lOSpells.Clear();
            lOSpells.Add(new UWActiveSpellEffect { MajorClass = 3, MinorClass = 1, Stability = 1 });
            UWArmourProtection.Compute(null, pOData, lOSpells, lOStatus);
            fExpectInt("stealth: minor class 1 is no stealth bonus", lOStatus.StealthBonus, 0);

            // The noise half: bit 1 takes 0x10 off, everything else leaves the base alone.
            fExpectInt("stealth: without a spell the noise base stands",
                UWArmourProtection.ApplyToNoise(13, 0), 13);
            fExpectInt("stealth: Stealth takes the noise base to zero",
                UWArmourProtection.ApplyToNoise(13, UWArmourProtection.StealthNoiseBit), 0);
            fExpectInt("stealth: Conceal does not touch the noise",
                UWArmourProtection.ApplyToNoise(13, UWArmourProtection.ConcealVisibilityBit), 13);

            // The visibility half: Conceal five, Invisibility everything, together still zero.
            fExpectInt("stealth: without a spell the visibility base stands",
                UWArmourProtection.ApplyToVisibility(15, 0), 15);
            fExpectInt("stealth: Conceal takes five off",
                UWArmourProtection.ApplyToVisibility(15, UWArmourProtection.ConcealVisibilityBit), 10);
            fExpectInt("stealth: Conceal never goes below zero",
                UWArmourProtection.ApplyToVisibility(3, UWArmourProtection.ConcealVisibilityBit), 0);
            fExpectInt("stealth: Invisibility takes all of it",
                UWArmourProtection.ApplyToVisibility(15, UWArmourProtection.InvisibilityBit), 0);
            fExpectInt("stealth: both together are still zero",
                UWArmourProtection.ApplyToVisibility(15,
                    UWArmourProtection.ConcealVisibilityBit | UWArmourProtection.InvisibilityBit), 0);

            // The movement summand, an integer division of ten over the forward speed.
            fExpectInt("stealth: standing adds nothing", UWPlayerVitals.GetMovingNoise(0f), 0);
            fExpectInt("stealth: half speed adds five", UWPlayerVitals.GetMovingNoise(0.5f), 5);
            fExpectInt("stealth: a full run adds ten", UWPlayerVitals.GetMovingNoise(1f), 10);
            fExpectInt("stealth: a creeping step adds nothing yet", UWPlayerVitals.GetMovingNoise(0.09f), 0);

            // Which is why Stealth does not silence a running player: base 0, plus ten, minus five.
            fExpectInt("stealth: a runner under Stealth is still heard at five",
                UWArmourProtection.ApplyToNoise(13, UWArmourProtection.StealthNoiseBit)
                    + UWPlayerVitals.GetMovingNoise(1f) - UWCritterRules.PlayerNoiseMovingOffset, 5);
            fExpectInt("stealth: at half speed he is silent",
                Math.Max(0, UWArmourProtection.ApplyToNoise(13, UWArmourProtection.StealthNoiseBit)
                    + UWPlayerVitals.GetMovingNoise(0.5f) - UWCritterRules.PlayerNoiseMovingOffset), 0);
        }
    }
}
