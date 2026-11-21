using System.Collections.Generic;
using System.IO;
using System.Linq;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE VORZ OF TALORUS TURN HOSTILE (per user 2026-11-22: "when do the Vorz become aggressive?"). The shipped
        /// SCD.ARK, block 0 (the time of day, 72 steps) rows 14-17, 26-29 and 38-41 for world 4 (0xFA) at times 0, 24
        /// and 48: a test of quest 35 (the Futurian's warning, conversation 157) passing when it is set, inside it a
        /// test of quest 59 (the pick-up trigger in the Delgnigzator, level 34 31/20) passing while it is clear, and
        /// behind it the scheduled triggers of 5/25 (the Vorz made) and every Vorz (race 0x1A) to attitude 0.
        /// </summary>
        private static void fCheckUw2VorzRule(CheckContext pOContext)
        {
            Check.Group("The Vorz of Talorus turn hostile (UW2)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("vorz rule", "no second game here: skipped");

                return;
            }

            UWGameSession lOBefore = UWGameSession.Current;

            try
            {
                foreach (int liCase in new[] { 0, 1, 2 })
                {
                    DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);

                    UWGameSession.MakeCurrent(lOImport.Session);

                    string lsScd = Path.Combine(lsData, UWSaveCodec.ScheduleFileName);
                    UWScheduleBookUw2 lOBook = UWScheduleBookUw2.Read(UWArk.Read(lsScd, UWArk.LayoutEnum.Uw2));
                    FakeTrapHost lOTraps = new FakeTrapHost(lOImport, 32);
                    FakeScheduleHost lOHost = new FakeScheduleHost
                    {
                        LevelNumber = 33, World = lOTraps.Fake, Traps = lOTraps, Properties = lOImport.ObjectClassProperties
                    };
                    UWLevel lOLevel = lOImport.Levels[32];
                    List<UWNpc> lOVorz = UWScheduleRulesUw2.Select(lOLevel, UWScheduleRulesUw2.ByRace | (0x1A << 8), lOImport.ObjectClassProperties);

                    // The day at step 10: past the rows of time 0 without the warning.
                    UWPlayerRecordUw2.SetXClock(lOHost.Record, UWScheduleRulesUw2.DayBlock, 10);
                    UWScheduleRulesUw2.SetAllClocks(lOBook, lOHost);

                    string lsStart = string.Join(",", lOVorz.Select(v => new UWCritterRecord(v).Attitude).Distinct().OrderBy(a => a));
                    int liLogStart = lOTraps.Fake.Log.Count;

                    // Case 0: the warning heard, the Delgnigzator not taken. Case 1: both. Case 2: neither.
                    if (liCase != 2)
                        UWPlayerRecordUw2.SetQuest(lOHost.Record, 35, 1);

                    if (liCase == 1)
                        UWPlayerRecordUw2.SetQuest(lOHost.Record, 59, 1);

                    // The day on to step 30, past the row of time 24.
                    UWPlayerRecordUw2.SetXClock(lOHost.Record, UWScheduleRulesUw2.DayBlock, 30);
                    UWScheduleRulesUw2.SetAllClocks(lOBook, lOHost);

                    string lsAfter = string.Join(",", lOVorz.Select(v => new UWCritterRecord(v).Attitude).Distinct().OrderBy(a => a));
                    int liLogged = lOTraps.Fake.Log.Count - liLogStart;
                    string lsWhat = lOVorz.Count + " vorz, attitudes " + lsStart + " -> " + lsAfter + ", " + liLogged + " world events";

                    if (liCase == 0)
                        Check.Bool("vorz rule: the warning heard, the Delgnigzator not taken: at the next third of the day every Vorz hostile ("
                            + lsWhat + ")", lOVorz.Count > 0 && lsStart != "0" && lsAfter == "0" && liLogged > 0, true);
                    else
                        Check.Bool("vorz rule: " + (liCase == 1 ? "the Delgnigzator taken as well" : "no warning") + ": the Vorz stay as they were ("
                            + lsWhat + ")", lOVorz.Count > 0 && lsAfter == lsStart && liLogged == 0, true);
                }
            }
            finally
            {
                UWGameSession.MakeCurrent(lOBefore);
            }
        }
    }
}
