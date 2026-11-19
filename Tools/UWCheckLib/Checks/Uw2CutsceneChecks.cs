using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S CUTSCENES (UWCutsceneRunnerUw2, UW2 plan phase 5 part C1): every shipped script read to its end
    /// by the second game's table, and every cutscene run headless to its end - the files it opens, the records it
    /// draws, how long it takes, as uw2 gfx/CUTS.C's player gives them.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>A host that reads the CUTS folder and PALS.DAT, speaks nothing and notes the subtitles.</summary>
        private sealed class CutsceneHostUw2 : UWCutsceneRunnerUw2.IHost
        {
            public string CutsPath;

            public string DataPath;

            public readonly List<string> Log = new List<string>();

            public UWCutscene OpenAnimation(string psFileName)
            {
                string lsPath = Path.Combine(CutsPath, psFileName);

                return File.Exists(lsPath) ? new UWCutscene(lsPath) : null;
            }

            public byte[] LoadBack(int piNumber)
            {
                string lsPath = Path.Combine(CutsPath, "LBACK" + System.Convert.ToString(piNumber, 8).PadLeft(3, '0') + ".BYT");

                return File.Exists(lsPath) ? File.ReadAllBytes(lsPath) : null;
            }

            public byte[] LoadQuickPalette(int piNumber)
            {
                string lsPath = Path.Combine(DataPath, "PALS.DAT");

                if (!File.Exists(lsPath))
                    return null;

                byte[] lyAll = File.ReadAllBytes(lsPath);

                if ((piNumber + 1) * 0x300 > lyAll.Length)
                    return null;

                byte[] lyPalette = new byte[0x300];

                System.Array.Copy(lyAll, piNumber * 0x300, lyPalette, 0, 0x300);

                return lyPalette;
            }

            public bool StartSpeech(int piVoice)
            {
                Log.Add("speech " + piVoice);

                return false;
            }

            public bool IsSpeechPlaying => false;

            public void StopSpeech()
            {
            }

            public void Music(int piTheme)
            {
                Log.Add("music " + piTheme);
            }

            public void DrawText(UWCutsceneScreenUw2 pOScreen, string psText, int piColour)
            {
                Log.Add("text " + piColour);
            }
        }

        private static void fCheckUw2Cutscenes(CheckContext pOContext)
        {
            Check.Group("UW2 cutscenes: the scripts by the second game's table, every cutscene run to its end");

            // dream (SKILLS.C): the latest due castle dream not yet seen; none due, the roll; a seen one rolls nothing.
            UWPlayerFile lOFile = UWPlayerRecordUw2.NewWalker();
            int liNone = UWStoryRulesUw2.PickDream(lOFile, 0, n => 1);
            UWPlayerRecordUw2.SetXClock(lOFile, UWStoryRulesUw2.CastleClock, 10);
            int liDue = UWStoryRulesUw2.PickDream(lOFile, 0, n => 1);
            UWStoryRulesUw2.MarkDreamShown(lOFile, liDue);
            int liNext = UWStoryRulesUw2.PickDream(lOFile, 0, n => 1);
            UWStoryRulesUw2.MarkDreamShown(lOFile, 1);
            UWStoryRulesUw2.MarkDreamShown(lOFile, 0);
            List<int> lOAsked = new List<int>();
            int liRolled = UWStoryRulesUw2.PickDream(lOFile, 1, n => { lOAsked.Add(n); return lOAsked.Count == 1 ? 0 : 1; });
            UWStoryRulesUw2.MarkDreamShown(lOFile, liRolled);
            int liSeen = UWStoryRulesUw2.PickDream(lOFile, 1, n => n == 8 ? 0 : 1);

            Check.Str("dreams: none due, roll missed | castle clock 10: dream 2, then 1, | none due: the roll of 8 hits, dream 5 | 5 seen: none | flags",
                liNone + " | " + liDue + " " + liNext + " | " + string.Join(",", lOAsked) + " " + liRolled + " | " + liSeen + " | "
                + lOFile.Record[UWStoryRulesUw2.DreamFlagsOffset].ToString("X2"),
                "-1 | 2 1 | 8,3 5 | -1 | 27");

            // cs_check: quest 143 holds the cutscene plus one, taken once.
            UWPlayerRecordUw2.SetQuest(lOFile, UWStoryRulesUw2.PendingCutsceneQuest, 3);
            int liTaken = UWStoryRulesUw2.TakePendingCutscene(lOFile);
            int liAgain = UWStoryRulesUw2.TakePendingCutscene(lOFile);

            Check.Str("cs_check: quest 143 = 3 gives the ending (2), then nothing", liTaken + " " + liAgain, "2 -1");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
                return;

            DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);
            string lsCuts = Path.Combine(Path.GetDirectoryName(lOImport.GameData.DataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "CUTS");
            List<string> lsBad = new List<string>();
            int liScripts = 0;

            foreach (string lsScript in Directory.GetFiles(lsCuts, "CS*.N00"))
            {
                UWCutsceneScript lOScript = new UWCutsceneScript(lsScript, UWGame.Uw2);

                liScripts++;

                if (!lOScript.IsLoaded || lOScript.UnreadBytes != 0)
                    lsBad.Add(Path.GetFileName(lsScript) + ":" + lOScript.UnreadBytes);
            }

            Check.Str("the shipped scripts read to their end | the ones that do not", liScripts + " | " + string.Join(" ", lsBad), "18 | ");

            // What the second game's recordings say (UWCutsceneSpoken, UW2 entries): each check value is the game's
            // string's and the subtitle changes; the first castle dream's last line and the fourth's two.
            List<string> lsSpokenWrong = new List<string>();
            int liSpoken = 0;

            foreach (UWCutsceneSpoken.Change lOChange in UWCutsceneSpoken.Changes)
            {
                if (lOChange.Game != UWGame.Uw2)
                    continue;

                liSpoken++;

                string lsGame = UWText.Block(lOImport.Strings, UWDataImport.Audio.UWSound.SpeechStringBlock + lOChange.Cutscene, lOChange.Index + 1, string.Empty).Trim();

                if (UWCutsceneSpoken.Check(lsGame) != lOChange.WrittenCheck)
                    lsSpokenWrong.Add(lOChange.Cutscene + ":" + lOChange.Index + " check " + UWCutsceneSpoken.Check(lsGame).ToString("X8"));
                else if (UWCutsceneSpoken.Subtitle(UWGame.Uw2, lOChange.Cutscene, lOChange.Index, lsGame) == lsGame)
                    lsSpokenWrong.Add(lOChange.Cutscene + ":" + lOChange.Index + " not changed");
            }

            string lsWish = UWCutsceneSpoken.Subtitle(UWGame.Uw2, 24, 3, UWText.Block(lOImport.Strings, UWDataImport.Audio.UWSound.SpeechStringBlock + 24, 4, string.Empty).Trim());
            string lsPatience = UWCutsceneSpoken.Subtitle(UWGame.Uw2, 27, 0, UWText.Block(lOImport.Strings, UWDataImport.Audio.UWSound.SpeechStringBlock + 27, 1, string.Empty).Trim());
            string lsAway = UWCutsceneSpoken.Subtitle(UWGame.Uw2, 27, 1, UWText.Block(lOImport.Strings, UWDataImport.Audio.UWSound.SpeechStringBlock + 27, 2, string.Empty).Trim());

            Check.Str("UW2 spoken lines (" + liSpoken + "): each matches the game's string and is changed | the dreams' changed lines; the first game's cutscene 24 untouched",
                string.Join(", ", lsSpokenWrong) + " | " + lsWish.StartsWith("As it is said on your world, wish thou") + " "
                + lsPatience.StartsWith("Patience, Avatar, I shall") + " [" + lsAway + "] " + (UWCutsceneSpoken.Subtitle(24, 3, "Wish thou wert here") == "Wish thou wert here"),
                " | True True [] True");

            // Each cutscene headless, a 60th of a second a step, at most ten minutes.
            int[] liCutscenes = { 9, 0, 1, 10, 24, 28, 0x103 };
            List<string> lsRuns = new List<string>();

            foreach (int liCutscene in liCutscenes)
            {
                UWCutsceneScript lOScript = new UWCutsceneScript(Path.Combine(lsCuts, UWCutscene.FileName(liCutscene, 0)), UWGame.Uw2);
                CutsceneHostUw2 lOHost = new CutsceneHostUw2 { CutsPath = lsCuts, DataPath = lOImport.GameData.DataPath };
                UWRandom.UseSeed(7);
                UWCutsceneRunnerUw2 lORunner = new UWCutsceneRunnerUw2(liCutscene, lOScript, lOImport.Strings, lOHost, new byte[0x300], true, false);
                bool lbRunning = lORunner.Start();
                int liSteps = 0;

                while (lbRunning && liSteps < 60 * 600)
                {
                    lbRunning = lORunner.Tick(1f / 60f);
                    liSteps++;
                }

                if (liCutscene == 0)
                    Check.Pass("UW2 intro's files", string.Join(" ", lORunner.FilesOpened));

                lsRuns.Add(liCutscene.ToString("X") + ": " + lORunner.FilesOpened.Count + " files " + lORunner.RecordsDrawn + " records "
                    + (int)(lORunner.Ticks / UWCutsceneRunnerUw2.TicksPerSecond) + "s" + (lbRunning ? " NOT ENDED" : string.Empty));
            }

            // Pinned 2026-11-17 from the first run, reasoned against the scripts: the title 37 frames at 4 a second with
            // its fades, the intro's 13 files, the credits' 48 frames at 1 a second, a dream's 40 frames at 3, the final
            // death's first file and its second eleven times over (the loop of 10) and once to its end.
            Check.Str("UW2 cutscene runs: files opened, records drawn, seconds", string.Join(" | ", lsRuns),
                "9: 1 files 25 records 10s | 0: 13 files 354 records 90s | 1: 5 files 57 records 14s | A: 1 files 22 records 49s | 18: 1 files 1 records 17s | 1C: 1 files 1 records 8s | 103: 12 files 95 records 12s");
        }
    }
}
