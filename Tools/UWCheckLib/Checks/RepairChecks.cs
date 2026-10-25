using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The anvil (UWRepairRules) against the reference audit of 2026-10-14: get_rep_diff, do_repair
    /// and repair_item of event/WORLDEV.C (UnderworldExhumed), brought over on 2026-10-15.
    /// </summary>
    public static partial class SelfChecks
    {
        private const int RepairSlingId = 0x18;

        private const int RepairBowId = 0x19;

        private const int RepairCrossbowId = 0x1A;

        private const int RepairSwordId = 0x04;

        private const int RepairLeatherArmourId = 0x20;

        private static void fCheckRepair(DataImport pOData)
        {
            Check.Group("Repair at the anvil (UWRepairRules)");

            fCheckRepairDurability(pOData);
            fCheckRepairResolve();
            fCheckRepairAttempt(pOData);
        }

        /// <summary>do_repair after the skill check, with scripted rolls.</summary>
        private static void fCheckRepairResolve()
        {
            fExpectResolve("critical failure: 61 beats quality 40 plus skill 20, destroyed, quality kept",
                40, 20, UWSkillCheck.ResultEnum.CriticalFailure, new[] { 61 }, UWRepairRules.OutcomeEnum.Destroyed, 40, "64");
            fExpectResolve("critical failure: 60 does not beat it, loses 4 + 7",
                40, 20, UWSkillCheck.ResultEnum.CriticalFailure, new[] { 60, 7 }, UWRepairRules.OutcomeEnum.Damaged, 29, "64 8");
            fExpectResolve("critical failure: quality plus skill 63 never breaks by the roll",
                30, 33, UWSkillCheck.ResultEnum.CriticalFailure, new[] { 63, 0 }, UWRepairRules.OutcomeEnum.Damaged, 26, "64 8");
            fExpectResolve("critical failure: a loss that leaves nothing destroys, quality 0",
                5, 0, UWSkillCheck.ResultEnum.CriticalFailure, new[] { 0, 3 }, UWRepairRules.OutcomeEnum.Destroyed, 0, "64 8");
            fExpectResolve("success: landing on exactly 63 is partially",
                55, 25, UWSkillCheck.ResultEnum.Success, new int[0], UWRepairRules.OutcomeEnum.Partially, 63, "");
            fExpectResolve("success: going past 63 is fully, held at 63",
                56, 25, UWSkillCheck.ResultEnum.Success, new int[0], UWRepairRules.OutcomeEnum.Fully, 63, "");
            fExpectResolve("critical success: fully",
                10, 0, UWSkillCheck.ResultEnum.CriticalSuccess, new int[0], UWRepairRules.OutcomeEnum.Fully, 63, "");
            fExpectResolve("failure: no effect",
                10, 0, UWSkillCheck.ResultEnum.Failure, new int[0], UWRepairRules.OutcomeEnum.NoEffect, 10, "");
        }

        private static void fExpectResolve(string psName, int piQuality, int piSkill, UWSkillCheck.ResultEnum peCheck,
            int[] piRolls, UWRepairRules.OutcomeEnum peExpected, int piExpectedQuality, string psExpectedRanges)
        {
            RecordingRandom lORandom = new RecordingRandom(piRolls);
            UWObject lOItem = new UWObject(RepairSwordId) { Quality = (ushort)piQuality };

            UWRandom.Use(lORandom);

            try
            {
                UWRepairRules.OutcomeEnum leOutcome = UWRepairRules.Resolve(lOItem, peCheck, piSkill);

                Check.Str("repair: " + psName, leOutcome + " " + lOItem.Quality + " [" + string.Join(" ", lORandom.Ranges) + "]",
                    peExpected + " " + piExpectedQuality + " [" + psExpectedRanges + "]");
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>repair_item's line and the fate of a destroyed item: the skill roll (0..30),
        /// the destruction roll, then Obj_Elem_Fate's two draws of 0..0x7FFF.</summary>
        private static void fCheckRepairAttempt(DataImport pOData)
        {
            string lsName = UWText.ObjectName(pOData, RepairSwordId);
            UWObject lOItem = new UWObject(RepairSwordId) { Quality = 0 };
            UWRepairRules.Result lOResult = fAttemptWith(lOItem, pOData, 0, 0, 63, 0, 0);

            Check.Str("attempt: destroyed, with the name and a full stop", lOResult.Message,
                UWText.General(pOData, UWRepairRules.CannotRepairMessage - 2) + lsName + ".");
            fExpectBool("attempt: ... and it leaves", lOResult.Destroyed, true);

            lOItem = new UWObject(RepairSwordId) { Quality = 20, DoorDirection = true };
            lOResult = fAttemptWith(lOItem, pOData, 0, 0, 63, 0, 0);

            Check.Str("attempt: a tenacious item survives: \"You cannot repair that.\" without a name", lOResult.Message,
                UWText.General(pOData, UWRepairRules.CannotRepairMessage));
            fExpectBool("attempt: ... it stays", lOResult.Destroyed, false);
            fExpectInt("attempt: ... with its quality", lOItem.Quality, 20);

            lOResult = fAttemptWith(new UWObject(RepairBowId), pOData, 30, 30);
            fExpectInt("attempt: a bow takes no time", lOResult.Minutes, 0);
            Check.Str("attempt: ... and cannot be repaired", lOResult.Message, UWText.General(pOData, UWRepairRules.CannotRepairMessage));
        }

        private static UWRepairRules.Result fAttemptWith(UWObject pOItem, DataImport pOData, int piSkill, params int[] piRolls)
        {
            UWRandom.Use(new RecordingRandom(piRolls));

            try
            {
                return UWRepairRules.Attempt(pOItem, pOData, piSkill);
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>get_rep_diff: the minor class 0x10-0x1F answers -1, launchers included.</summary>
        private static void fCheckRepairDurability(DataImport pOData)
        {
            fExpectInt("durability: a sling cannot be repaired", UWRepairRules.GetDurability(new UWObject(RepairSlingId), pOData), -1);
            fExpectInt("durability: a bow cannot be repaired", UWRepairRules.GetDurability(new UWObject(RepairBowId), pOData), -1);
            fExpectInt("durability: a crossbow cannot be repaired", UWRepairRules.GetDurability(new UWObject(RepairCrossbowId), pOData), -1);
            fExpectBool("durability: a sword has one", UWRepairRules.GetDurability(new UWObject(RepairSwordId), pOData) >= 0, true);
            fExpectBool("durability: leather armour has one",
                UWRepairRules.GetDurability(new UWObject(RepairLeatherArmourId), pOData) >= 0, true);

            string lsQuestion;

            fExpectBool("question: none for the bow", UWRepairRules.TryGetQuestion(new UWObject(RepairBowId), pOData, 10, out lsQuestion), false);
            Check.Str("message 143", UWText.General(pOData, UWRepairRules.CannotRepairMessage), "You cannot repair that.");
        }
    }
}
