using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S SMALLER FORMATS (Docs/UW2-PLAN.md phase 1 step 4, 2026-10-29), held to what
    /// Underworld Exhumed measured on the same files (docs/FORMATS.md): SCD.ARK's 16 schedules (51, 78,
    /// 6 and 84 rows in blocks 0, 1, 14 and 15, none in the rest; each block rows x 16 + 0x144 long, its
    /// rows in time order, events from -11 to 11, two rows at time 0xFFFF), each block read and written
    /// back byte for byte, the DOS saves' schedules too (--uw2-saves); DL.DAT's 80 levels; every
    /// BYT.ARK screen in its palette (checked by eye in a scratch render, 2026-10-29: automap, character
    /// creation, conversation, game screen, main menu, the two logos, the two end screens); SOUNDS.DAT
    /// of both games (49 effects of 8 bytes and 24 of 5, the lengths high byte first: 0x40, 0x80, 0x180
    /// and the like), every digital effect's VOC present; the music names of both games.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2Files(CheckContext pOContext)
        {
            Check.Group("The second game's schedules, light, screens, sounds and music (UW2 plan, phase 1 step 4)");

            fCheckMusicNames(pOContext);

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2 files", "no second game here: skipped");

                return;
            }

            fCheckSchedules(UWArk.Read(Path.Combine(lsData, "SCD.ARK"), UWArk.LayoutEnum.Uw2), "DATA", true);

            if (pOContext.Uw2SavesPath != null)
            {
                foreach (string lsSave in Directory.GetDirectories(pOContext.Uw2SavesPath, "SAVE?"))
                {
                    string lsFile = Path.Combine(lsSave, "SCD.ARK");

                    if (File.Exists(lsFile))
                        fCheckSchedules(UWArk.Read(lsFile, UWArk.LayoutEnum.Uw2), Path.GetFileName(lsSave), false);
                }
            }

            // The conversations (phase 1 step 6): the first game's parser on the second game's decoded blocks.
            UWArk lOConversations = UWArk.Read(Path.Combine(lsData, "CNV.ARK"), UWArk.LayoutEnum.Uw2);
            UWStrings lOStrings = new UWStrings(File.ReadAllBytes(Path.Combine(lsData, "STRINGS.PAK")));
            int liConversations = 0;
            List<string> lsBadConversations = new List<string>();

            for (int liSlot = 0; liSlot < lOConversations.BlockCount; liSlot++)
            {
                byte[] lyBlock = lOConversations.ReadBlock(liSlot);

                if (lyBlock.Length == 0)
                    continue;

                liConversations++;

                UWConversations.Conversation lOConversation = UWConversations.Parse(lyBlock, 0, liSlot);

                if (!lOStrings.Blocks.ContainsKey(lOConversation.StringBlock) || lOConversation.Imports.Count == 0 || lOConversation.Code.Length == 0)
                    lsBadConversations.Add(liSlot.ToString());
            }

            Check.Str("conv: UW2's conversations parse, each with imports, code and its string block in STRINGS.PAK",
                liConversations + " " + string.Join(" ", lsBadConversations), "102 ");

            UWDarknessUw2 lODark = new UWDarknessUw2(File.ReadAllBytes(Path.Combine(lsData, UWDarknessUw2.FileName)));
            int liMinimum = 0;

            for (int liLevel = 0; liLevel < UWDarknessUw2.LevelCount; liLevel++)
                liMinimum += lODark.IsMinimum(liLevel) ? 1 : 0;

            Check.Str("dl: DL.DAT's 80 levels, the minimum lights, level 1's light and whether it is a minimum (as read)",
                new FileInfo(Path.Combine(lsData, UWDarknessUw2.FileName)).Length + " " + liMinimum + " " + lODark.LightLevel(0) + " " + lODark.IsMinimum(0),
                "80 23 4 True");

            List<string> lsMissing = new List<string>();

            foreach (UWScreensUw2.ScreenEnum leScreen in System.Enum.GetValues(typeof(UWScreensUw2.ScreenEnum)))
            {
                UWPicture lOScreen = UWScreensUw2.Screen(lsData, leScreen);

                if (lOScreen == null || lOScreen.Width != UWScreensUw2.ScreenWidth || lOScreen.Height != UWScreensUw2.ScreenHeight)
                    lsMissing.Add(leScreen.ToString());
            }

            Check.Str("screens: every BYT.ARK screen of the nine in its palette (the logos 5 and 6, character creation 3)", string.Join(" ", lsMissing), string.Empty);

            string lsSound = Path.Combine(Path.GetDirectoryName(lsData), "SOUND");
            UWSoundsDat lOUw2Sounds = new UWSoundsDat(File.ReadAllBytes(Path.Combine(lsSound, "SOUNDS.DAT")), UWGame.Uw2);
            List<string> lsNoVoc = new List<string>();
            int liDigital = 0;

            for (int liAt = 0; liAt < lOUw2Sounds.Effects.Count; liAt++)
            {
                if (lOUw2Sounds.Effects[liAt].Digital == 0)
                    continue;

                liDigital++;

                if (!File.Exists(Path.Combine(lsSound, UWSoundsDat.Uw2VocName(liAt) + ".VOC")))
                    lsNoVoc.Add(UWSoundsDat.Uw2VocName(liAt));
            }

            Check.Str("sounds: UW2's SOUNDS.DAT (effects, their lengths high byte first, the digital ones)",
                lOUw2Sounds.Effects.Count + " " + fLengths(lOUw2Sounds) + " " + liDigital, "49 64,128,384,640,896,1408,6400 29");
            Check.Str("sounds: ... every digital effect's VOC is there (SPnn below 100, UWnn from 100)", string.Join(" ", lsNoVoc), string.Empty);
            Check.Str("sounds: the VOC names (5, 48, 100, 101)", UWSoundsDat.Uw2VocName(5) + " " + UWSoundsDat.Uw2VocName(48) + " "
                + UWSoundsDat.Uw2VocName(100) + " " + UWSoundsDat.Uw2VocName(101), "SP05 SP48 UW00 UW01");

            if (pOContext.DataPath != null)
            {
                string lsUw1Sound = Path.Combine(Path.GetDirectoryName(pOContext.DataPath), "SOUND", "SOUNDS.DAT");

                if (File.Exists(lsUw1Sound))
                {
                    UWSoundsDat lOUw1Sounds = new UWSoundsDat(File.ReadAllBytes(lsUw1Sound), UWGame.Uw1);

                    Check.Str("sounds: UW1's SOUNDS.DAT read as the game reads it (effects, lengths high byte first)",
                        lOUw1Sounds.Effects.Count + " " + fLengths(lOUw1Sounds), "24 64,128,384,6400");
                }
            }
        }

        private static string fLengths(UWSoundsDat pOSounds)
        {
            SortedSet<int> lOLengths = new SortedSet<int>();

            foreach (UWSoundsDat.Effect lOEffect in pOSounds.Effects)
                lOLengths.Add(lOEffect.Length);

            return string.Join(",", lOLengths);
        }

        /// <summary>The 16 schedules: each read and written back; the shipped file also to UE's counts.</summary>
        private static void fCheckSchedules(UWArk pOArk, string psLabel, bool pbShipped)
        {
            List<string> lsBad = new List<string>();
            List<string> lsRows = new List<string>();
            int liAtEnd = 0;
            int liLowest = 0;
            int liHighest = 0;

            for (int liBlock = 0; liBlock < pOArk.BlockCount; liBlock++)
            {
                byte[] lyBlock = pOArk.ReadBlock(liBlock);
                UWScheduleUw2 lOSchedule = UWScheduleUw2.Read(lyBlock);

                if (lOSchedule == null)
                {
                    lsBad.Add(liBlock + " unread");

                    continue;
                }

                if (lyBlock.Length != UWScheduleUw2.HeaderSize + (lOSchedule.Rows.Count * UWScheduleUw2.RowSize))
                    lsBad.Add(liBlock + " length " + lyBlock.Length);

                if (!fSameBytes(lOSchedule.ToBytes(), lyBlock))
                    lsBad.Add(liBlock + " written back differently");

                for (int liRow = 0; liRow < lOSchedule.Rows.Count; liRow++)
                {
                    UWScheduleUw2.Row lORow = lOSchedule.Rows[liRow];

                    if (liRow > 0 && lORow.Time < lOSchedule.Rows[liRow - 1].Time)
                        lsBad.Add(liBlock + " row " + liRow + " out of time order");

                    liAtEnd += lORow.Time == 0xFFFF ? 1 : 0;
                    liLowest = System.Math.Min(liLowest, lORow.Event);
                    liHighest = System.Math.Max(liHighest, lORow.Event);
                }

                if (lOSchedule.Rows.Count > 0)
                    lsRows.Add(liBlock + ":" + lOSchedule.Rows.Count);
            }

            Check.Str("scd: " + psLabel + " SCD.ARK's 16 schedules - each rows x 16 + 0x144 long, its rows in time order, read and written back",
                pOArk.BlockCount + " " + string.Join("; ", lsBad), "16 ");

            if (pbShipped)
                Check.Str("scd: the shipped schedules (block:rows), the rows at time 0xFFFF, the events from lowest to highest (UE: 51, 78, 6, 84; two; -11 to 11)",
                    string.Join(" ", lsRows) + " " + liAtEnd + " " + liLowest + ".." + liHighest, "0:51 1:78 14:6 15:84 2 -11..11");
        }

        /// <summary>The music names of both games; the first game's as UWSound and UWMusicSelector name them.</summary>
        private static void fCheckMusicNames(CheckContext pOContext)
        {
            bool lbSame = true;

            for (int liTheme = 0; liTheme < 64; liTheme++)
            {
                string lsNumber = UWMusicSelector.GetFileNumber(liTheme);

                lbSame &= UWSoundsDat.MusicName(UWGame.Uw1, liTheme, true) == "UW" + lsNumber
                    && UWSoundsDat.MusicName(UWGame.Uw1, liTheme, false) == "AW" + lsNumber;
            }

            Check.Bool("music: the first game's names as UWSound and UWMusicSelector give them, themes 0 to 63", lbSame, true);
            Check.Str("music: the second game's (theme 8 for the Roland card, 24 for the others)",
                UWSoundsDat.MusicName(UWGame.Uw2, 8, true) + " " + UWSoundsDat.MusicName(UWGame.Uw2, 24, false), "UWR10 UWA30");

            if (pOContext.Uw2DataPath == null)
                return;

            string lsSound = Path.Combine(Path.GetDirectoryName(pOContext.Uw2DataPath), "SOUND");
            int liThemes = 0;

            for (int liTheme = 0; liTheme < 64; liTheme++)
            {
                bool lbRoland = File.Exists(Path.Combine(lsSound, UWSoundsDat.MusicName(UWGame.Uw2, liTheme, true) + ".XMI"));
                bool lbOther = File.Exists(Path.Combine(lsSound, UWSoundsDat.MusicName(UWGame.Uw2, liTheme, false) + ".XMI"));

                if (lbRoland != lbOther)
                    liThemes = -1000;
                else if (lbRoland)
                    liThemes++;
            }

            Check.Int("music: the second game's themes, each in both versions (UWA and UWR, 17 in SOUND)", liThemes, 17);
        }
    }
}
