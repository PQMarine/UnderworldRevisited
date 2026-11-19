using System.Linq;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE ETHEREAL VOID AND KILLORN'S COUNTDOWN (UWVoidRulesUw2, the rest of UW2 plan G6): the dream plant, the Void's
    /// dream and go_void's squares, the countdown of sleepbits, waking, the locks while asleep, the teleport out refused,
    /// and duration_check's countdown to the keep's crash.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2Void(CheckContext pOContext)
        {
            Check.Group("UW2 the Void: the dream plant, go_void, punt_void, the locks; Killorn's countdown");

            // The dream plant: sleepbits 2 to 5 by the roll; not in the Void.
            UWPlayerFile lOFile = UWPlayerRecordUw2.NewWalker();
            UWVoidRulesUw2.EatDreamPlant(lOFile, 7);
            string lsPlant = UWVoidRulesUw2.SleepBits(lOFile) + " " + UWVoidRulesUw2.DreamIsVoid(lOFile);
            UWPlayerFile lOInVoid = UWPlayerRecordUw2.NewWalker();
            UWVoidRulesUw2.SetInVoid(lOInVoid, true);
            lsPlant += " " + UWVoidRulesUw2.EatDreamPlant(lOInVoid, 7) + " " + UWVoidRulesUw2.SleepBits(lOInVoid);

            Check.Str("the dream plant: sleepbits by roll 7, the dream is the Void | eaten in the Void: nothing", lsPlant, "5 True False 0");

            // go_void: quest 48, sleepbits anew, in the Void, the place stored; squares by the rolls.
            UWVoidRulesUw2.GoVoid(lOFile, 17, 40, 41, 0x80, 1, 5, 1, 1, out int liX, out int liY);
            string lsGo = UWPlayerRecordUw2.GetQuest(lOFile, 48) + " " + UWVoidRulesUw2.SleepBits(lOFile) + " " + UWPlayerRecordUw2.IsInVoid(lOFile) + " "
                + UWVoidRulesUw2.DreamLevel(lOFile) + " " + UWVoidRulesUw2.DreamX(lOFile) + "/" + UWVoidRulesUw2.DreamY(lOFile) + " " + UWVoidRulesUw2.DreamHeadingByte(lOFile)
                + " -> " + liX + "/" + liY;
            UWPlayerFile lOOther = UWPlayerRecordUw2.NewWalker();
            UWVoidRulesUw2.GoVoid(lOOther, 1, 0, 0, 0, 0, 2, 1, 0, out int liX2, out int liY2);
            UWVoidRulesUw2.GoVoid(lOOther, 1, 0, 0, 0, 0, 3, 0, 1, out int liX3, out int liY3);

            Check.Str("go_void: quest 48, sleepbits, in the Void, the stored level, square and heading -> the square (roll 5: 32/28) | rolls 2 and 3 moved by one",
                lsGo + " | " + liX2 + "/" + liY2 + " " + liX3 + "/" + liY3, "1 3 True 17 40/41 128 -> 32/28 | 39/32 14/41");

            // The locks while asleep in the Void, the teleport out refused (in the Void's world, to this level: not).
            string lsLocks = UWVoidRulesUw2.IsAsleepInVoid(lOFile) + " " + UWVoidRulesUw2.TeleportRefused(lOFile, 17) + " "
                + UWVoidRulesUw2.TeleportRefused(lOFile, 70) + " " + UWVoidRulesUw2.TeleportRefused(lOFile, 0) + " " + UWVoidRulesUw2.SleepWakes(lOFile, 69)
                + " " + UWVoidRulesUw2.SleepWakes(lOFile, 17);

            Check.Str("asleep in the Void: the locks, a teleport to 17 refused, to 70 and to this level not; sleeping on 69 wakes, on 17 not",
                lsLocks, "True True False False True False");

            // The countdown of sleepbits: 3 -> 2 -> 1 -> 0, waking at 0 in the Void; then out of it.
            string lsTicks = UWVoidRulesUw2.TickSleep(lOFile) + " " + UWVoidRulesUw2.TickSleep(lOFile) + " " + UWVoidRulesUw2.TickSleep(lOFile) + " "
                + UWVoidRulesUw2.TickSleep(lOFile);
            UWVoidRulesUw2.Wake(lOFile);

            Check.Str("duration_check: sleepbits 3 down, waking at 0 | punt_void: out of the Void, sleepbits 0, no lock",
                lsTicks + " | " + UWPlayerRecordUw2.IsInVoid(lOFile) + " " + UWVoidRulesUw2.SleepBits(lOFile) + " " + UWVoidRulesUw2.IsAsleepInVoid(lOFile),
                "False False True False | False 0 False");

            // Killorn's countdown: nothing without quest 50; with it a shake while the byte counts down (time frozen: shake
            // only); past 0 the crash - quest 54, XC_CHANGED, the byte 0xFF; then nothing.
            UWPlayerFile lOKeep = UWPlayerRecordUw2.NewWalker();
            string lsKeep = UWVoidRulesUw2.KillornCountdown(lOKeep, false).ToString();
            UWPlayerRecordUw2.SetQuest(lOKeep, UWLevelHacksUw2.KeepFallsQuest, 1);
            UWPlayerRecordUw2.SetQuest(lOKeep, UWVoidRulesUw2.CountdownQuest, 1);
            lsKeep += " " + UWVoidRulesUw2.KillornCountdown(lOKeep, true) + " " + UWPlayerRecordUw2.GetQuest(lOKeep, UWVoidRulesUw2.CountdownQuest)
                + " " + UWVoidRulesUw2.KillornCountdown(lOKeep, false) + " " + UWVoidRulesUw2.KillornCountdown(lOKeep, false) + " "
                + UWPlayerRecordUw2.GetQuest(lOKeep, 54) + " " + UWPlayerRecordUw2.XClock(lOKeep, UWScheduleRulesUw2.ChangedClock) + " "
                + UWPlayerRecordUw2.GetQuest(lOKeep, UWVoidRulesUw2.CountdownQuest) + " " + UWVoidRulesUw2.KillornCountdown(lOKeep, false);

            Check.Str("Killorn's countdown: without quest 50 | frozen (the byte stays 1), 1 -> 0, 0 -> crash; quest 54, XC_CHANGED, the byte; afterwards",
                lsKeep, "None Shake 1 Shake Crash 1 1 255 None");

            fCheckUw2KeepCrash(pOContext);
        }

        /// <summary>The keep's crash on its level 1 (17) with the shipped SCD.ARK: quest 54 and XC_CHANGED make block 15's
        /// rows for level 17 kill its creatures (per user 2026-11-17: in the original level 17 stood empty afterwards).</summary>
        private static void fCheckUw2KeepCrash(CheckContext pOContext)
        {
            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
                return;

            DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);
            string lsScd = System.IO.Path.Combine(lOImport.GameData.DataPath, UWSaveCodec.ScheduleFileName);
            UWScheduleBookUw2 lOBook = UWScheduleBookUw2.Read(UWArk.Read(lsScd, UWArk.LayoutEnum.Uw2));
            FakeTrapHost lOTraps = new FakeTrapHost(lOImport, 16);
            FakeScheduleHost lOHost = new FakeScheduleHost { World = lOTraps.Fake, Traps = lOTraps, Properties = lOImport.ObjectClassProperties, LevelNumber = 17 };

            UWScheduleRulesUw2.SetAllClocks(lOBook, lOHost);
            int liBefore = lOTraps.Fake.Log.Count(l => l.StartsWith("kill"));
            UWLevelHacksUw2.KeepCrashed(lOHost.Record);
            int liError = UWScheduleRulesUw2.SetAllClocks(lOBook, lOHost);
            int liKills = lOTraps.Fake.Log.Count(l => l.StartsWith("kill"));

            Check.Str("the keep's crash on level 17: kills before | the pass's answer, kills after (block 15's rows on quest 54)",
                liBefore + " | " + liError + " " + (liKills > 0), "0 | 0 True");
        }
    }
}
