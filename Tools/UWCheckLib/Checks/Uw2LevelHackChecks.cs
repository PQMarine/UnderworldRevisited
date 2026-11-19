using System.Collections.Generic;
using System.Linq;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S LEVEL HACKS (UWLevelHacksUw2, UW2 plan G6 part 5): which levels do what on leaving and arriving,
    /// the Prison Tower's alarm on its goblins, Killorn's crash, the telekinesis wand found where the data has it, and the
    /// Tombs' genocide of the undead.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2LevelHacks(CheckContext pOContext)
        {
            Check.Group("UW2 level hacks: do_level_hacks, prison_alarm_check, Killorn's crash, remove_TK_wand, genocide(0xFF)");

            UWPlayerFile lOFile = UWPlayerRecordUw2.NewWalker();
            string lsLevels = string.Join(" ", new[] { 9, 10, 17, 1 }.Select(UWLevelHacksUw2.ChecksPrisonAlarm)) + " | "
                + string.Join(" ", new[] { 9, 10, 18 }.Select(UWLevelHacksUw2.FiresPrisonTriggers)) + " | "
                + string.Join(" ", new[] { 19, 43, 20, 35 }.Select(UWLevelHacksUw2.TakesTelekinesisWand)) + " | "
                + UWLevelHacksUw2.KeepCrashes(17, lOFile) + " " + UWLevelHacksUw2.TombsCleansed(49, lOFile);
            UWPlayerRecordUw2.SetQuest(lOFile, UWLevelHacksUw2.KeepFallsQuest, 1);
            UWPlayerRecordUw2.SetQuest(lOFile, UWLevelHacksUw2.LothDeadQuest, 1);
            lsLevels += " " + UWLevelHacksUw2.KeepCrashes(17, lOFile) + " " + UWLevelHacksUw2.KeepCrashes(18, lOFile) + " "
                + UWLevelHacksUw2.TombsCleansed(49, lOFile) + " " + UWLevelHacksUw2.TombsCleansed(56, lOFile) + " " + UWLevelHacksUw2.TombsCleansed(57, lOFile);
            UWLevelHacksUw2.KeepCrashed(lOFile);

            Check.Str("which levels: the prison alarm (9, 10, 17, 1) | its level 2's triggers (9, 10, 18) | the wand (19, 43, 20, 35) | "
                + "the crash and the Tombs without, then with quests 50 and 7 (17, 18; 49, 56, 57); quest 54 and XC_CHANGED after the crash",
                lsLevels + " | " + UWPlayerRecordUw2.GetQuest(lOFile, 54) + " " + UWPlayerRecordUw2.XClock(lOFile, UWScheduleRulesUw2.ChangedClock),
                "True True False False | False True False | True True False False | False False True False True True False | 1 1");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2 level hacks on data", "no second game here: skipped");

                return;
            }

            DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);
            UWObjectClassProperties lOProps = lOImport.ObjectClassProperties;

            // The Prison Tower's level 2: no alarm while its goblins are not hostile; two hostile ones set quest 60 and move
            // XC_CHANGED twice; with quest 60 set nothing more.
            UWLevel lOPrison = lOImport.Levels[9];
            List<UWNpc> lOGoblins = UWScheduleRulesUw2.Select(lOPrison, UWScheduleRulesUw2.ByRace | (UWLevelHacksUw2.GoblinRace << 8), lOProps);
            UWPlayerFile lOAlarm = UWPlayerRecordUw2.NewWalker();
            int liCalm = UWLevelHacksUw2.PrisonAlarmCheck(lOPrison, lOProps, lOAlarm);
            lOGoblins.Take(2).ToList().ForEach(g => new UWCritterRecord(g).Attitude = 0);
            int liHostile = UWLevelHacksUw2.PrisonAlarmCheck(lOPrison, lOProps, lOAlarm);
            string lsAfter = UWPlayerRecordUw2.GetQuest(lOAlarm, 60) + " " + UWPlayerRecordUw2.XClock(lOAlarm, UWScheduleRulesUw2.ChangedClock);
            int liAgain = UWLevelHacksUw2.PrisonAlarmCheck(lOPrison, lOProps, lOAlarm);

            Check.Str("prison_alarm_check on level 10: calm goblins | two hostile (quest 60, XC_CHANGED) | again with quest 60",
                liCalm + " | " + liHostile + " " + lsAfter + " | " + liAgain, "0 | 2 1 2 | 0");

            // The telekinesis wand where the data keeps it: a wand 0x9B with the door bit and spell 0x27.
            string lsWands = string.Join(",", Enumerable.Range(0, lOImport.Levels.Count)
                .Where(i => lOImport.Levels[i] != null)
                .SelectMany(i => lOImport.Levels[i].Masterlist.Where(o => UWLevelHacksUw2.IsTelekinesisWand(o, lOImport.Levels[i].Masterlist))
                    .Select(o => (i + 1) + "@" + o.TileX + "/" + o.TileY)));

            Check.Str("find_TK_wand_check over every level's data: the telekinesis wand and where it lies", lsWands, "43@31/29");

            // genocide(0xFF) in the Tombs: every active undead killed, none of the others.
            UWLevel lOTombs = lOImport.Levels[48];
            FakeWorld lOWorld = new FakeWorld { Level = lOTombs, Data = lOImport };
            int liKilled = UWLevelHacksUw2.Genocide(lOWorld, lOProps, lOImport.CommonObjectProperties, out int liSkulls);
            int liUndead = lOTombs.ActiveMobiles.Select(i => lOTombs.Masterlist[i]).Count(o => o != null && UWObjectIds.IsCreature(o.ID)
                && lOImport.CommonObjectProperties.TryGet(o.ID, out UWCommonObjectProperties.Entry e) && UWDamageTypes.IsUndead(e.Resistances));
            int liActive = lOTombs.ActiveMobiles.Count(i => lOTombs.Masterlist[i] != null && UWObjectIds.IsCreature(lOTombs.Masterlist[i].ID));

            Check.Str("genocide(0xFF) on level 49: the kills are the undead among the active creatures, some live on",
                (liKilled == liUndead && liKilled > 0) + " " + (liActive > liKilled) + " skulls " + liSkulls, "True True skulls 0");
        }
    }
}
