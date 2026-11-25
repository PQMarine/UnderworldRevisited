using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.9 (2026-10-20): what the character pages show (UWCharacterSheet) left the
    /// classic UWHudPanel, the modern original stats page, the modern panel's Character tab and the
    /// help window's Stats tab. Pinned with what those copies computed: both spellings, the six
    /// values with Unity's rounding, the skill list and its paging, the signature.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckCharacterSheet()
        {
            Check.Group("Character sheet (UWCharacterSheet - cleanup step 5.9)");

            UWPlayerData lOPlayer = UWPlayerData.FromPlain(UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter
            {
                Name = "Sir Selfcheck of the Abyss"
            }));

            UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

            lOStart.Strength = 21;
            lOStart.Dexterity = 17;
            lOStart.Intelligence = 14;
            lOStart.Attack = 5;
            lOStart.Level = 3;
            lOStart.FallbackMaxHP = 30;
            lOStart.FallbackCurrentHP = 12;

            UWPlayerVitals lOVitals = new UWPlayerVitals(null);

            lOVitals.Initialise(null, lOStart);
            lOVitals.SetMana(20, 8);

            UWCharacterSheet lOOriginal = new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Original, 6).Bind(null, lOPlayer, lOVitals);
            UWCharacterSheet lOReadable = new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Readable).Bind(null, lOPlayer, lOVitals);

            Check.Str("name: the original's page cuts to fifteen, upper case (STATS.C sp_hdr)", lOOriginal.Name, "SIR SELFCHECK O");
            Check.Str("name: the readable pages keep it whole", lOReadable.Name, "Sir Selfcheck of the Abyss");
            Check.Str("name: without one AVATAR / Avatar",
                new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Original).Name + " " + new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Readable).Name,
                "AVATAR Avatar");
            Check.Str("level: 3RD on the original's page", lOOriginal.LevelText, "3RD");
            Check.Str("level: the modern panel's and the help window's lines", lOReadable.ClassWithLevel + "|" + lOReadable.LevelWithClass,
                ", level 3|Level 3 ");

            // Vitality 12 and mana 8 of 20: until the vitals became whole numbers (cleanup phase 10,
            // 2026-10-26) the check set 12.5, 7.5 and 19.5 and the page rounded them half to even to
            // these same numbers.
            string lsValues = string.Empty;

            for (int liRow = 0; liRow < UWCharacterSheet.ValueCount; liRow++)
                lsValues += lOOriginal.Value(liRow) + ";";

            Check.Str("values: the original's page, a/b", lsValues, "21;17;14;12/30;8/20;0;");
            Check.Str("values: the readable pages, a / b", lOReadable.Value(3) + ";" + lOReadable.Value(4), "12 / 30;8 / 20");
            Check.Str("values: past the six, nothing", lOOriginal.Value(6) + lOOriginal.Value(-1), string.Empty);
            Check.Str("values: the labels the modern panel and the help window write",
                string.Join(",", UWCharacterSheet.ValueLabels), "Strength,Dexterity,Intelligence,Vitality,Mana,Experience");

            // Without the strings: the original's page leaves a row empty, the readable ones say "?".
            lOOriginal.TryGetSkill(0, out string lsName, out string lsValue);
            Check.Str("skills: no word, no line on the original's page", lsName + "|" + lsValue, "|");
            lOReadable.TryGetSkill(0, out lsName, out lsValue);
            Check.Str("skills: no word, '?' and the attack on the readable pages", lsName + "|" + lsValue, "?|5");
            fExpectBool("skills: past the list, nothing", lOReadable.TryGetSkill(UWCharacterSheet.SkillEntries, out lsName, out lsValue)
                || lsName.Length + lsValue.Length > 0, false);
            Check.Str("skills: entry 1 is the defence, the value alone", lOReadable.SkillValue(1), lOVitals.Defence.ToString());

            // Paging: one entry per click, clamped to 0..14 for six rows (ovr145_445).
            fExpectBool("paging: back at the top does nothing", lOOriginal.PageSkills(-1), false);
            fExpectBool("paging: forward moves one", lOOriginal.PageSkills(1) && lOOriginal.SkillOffset == 1 && lOOriginal.EntryAtRow(5) == 6, true);

            for (int liClick = 0; liClick < 30; liClick++)
                lOOriginal.PageSkills(1);

            fExpectInt("paging: the last page ends with entry 19", lOOriginal.SkillOffset, 14);
            fExpectInt("paging: the readable pages show the whole list", lOReadable.MaxSkillOffset, 0);

            // THE SECOND GAME'S ORIGINAL PAGE (UW2 plan, stage U5 step 1): the skill points first, 21 entries, the
            // offset 0 to 15 (uw2 STATS.C sp_skill row + skill_top - 1, mous_in_stat's mvcheck to 0xF).
            UWCharacterSheet lOSecond = new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Original, 6).Bind(null, lOPlayer, lOVitals, UWGame.Uw2);

            fExpectBool("uw2 page: the first row is the skill points", lOSecond.EntryAtRow(0) == UWCharacterSheet.PointsEntry
                && lOSecond.EntryAtRow(1) == 0, true);
            fExpectInt("uw2 page: 21 entries", lOSecond.ListLength, 21);
            fExpectInt("uw2 page: the offset up to 15 for six rows", lOSecond.MaxSkillOffset, 15);
            lOSecond.TryGetSkill(UWCharacterSheet.PointsEntry, out lsName, out lsValue);
            Check.Str("uw2 page: no word for the points, no line", lsName + "|" + lsValue, "|");

            for (int liClick = 0; liClick < 30; liClick++)
                lOSecond.PageSkills(1);

            fExpectBool("uw2 page: the last page ends with entry 19", lOSecond.SkillOffset == 15 && lOSecond.EntryAtRow(5) == 19, true);
            lOSecond.Bind(null, lOPlayer, lOVitals);
            fExpectBool("uw2 page: back on the first game's list the offset stays on it", lOSecond.SkillOffset == 14
                && lOSecond.EntryAtRow(0) == 14, true);
            fExpectBool("uw2 page: the readable spelling keeps the first game's list",
                new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Readable).Bind(null, lOPlayer, lOVitals, UWGame.Uw2).EntryAtRow(0) == 0, true);

            // The signature follows every number shown.
            int liBefore = lOOriginal.Signature;

            lOVitals.SetMana(20, 9);
            fExpectBool("signature: mana changes it", lOOriginal.Signature != liBefore, true);
            fExpectInt("signature: without vitals 0", new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Original).Signature, 0);
        }

        private static void fCheckCharacterSheetData(DataImport pOData)
        {
            Check.Group("Character sheet with the game's data (cleanup step 5.9)");

            UWPlayerData lOPlayer = UWPlayerData.FromPlain(UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter
            {
                Name = "Selfcheck", CharacterClass = 0
            }));
            UWPlayerVitals lOVitals = new UWPlayerVitals(null);

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);

            UWCharacterSheet lOOriginal = new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Original, 6).Bind(pOData.Strings, lOPlayer, lOVitals);
            UWCharacterSheet lOReadable = new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Readable).Bind(pOData.Strings, lOPlayer, lOVitals);

            Check.Str("class: upper case on the original's page", lOOriginal.ClassName, "FIGHTER");
            Check.Str("class: the modern panel's line", lOReadable.ClassWithLevel, "Fighter, level 1");
            Check.Str("class: the help window's line", lOReadable.LevelWithClass, "Level 1 Fighter");

            lOOriginal.TryGetSkill(lOOriginal.EntryAtRow(0), out string lsName, out string lsValue);
            Check.Str("skills: the original's first row, the word as the file has it", lsName,
                UWCharacterText.SkillName(pOData.Strings, 0));
            Check.Str("skills: the readable pages in title case", lOReadable.SkillName(0) + " " + lOReadable.SkillName(19), "Attack Swimming");
            Check.Str("skills: the original's name alone as TryGetSkill's", lOOriginal.SkillName(2), UWCharacterText.SkillName(pOData.Strings, 2));
        }

        /// <summary>
        /// The stats page per game (UW2 plan, stage U5 step 1, 2026-11-23): the first game's measures are the ones the
        /// modern stats page had as constants; the second game's lines stand where RedispStat writes them (uw2
        /// STATS.C), and every point of the page pages the list as mous_in_stat does, written out here as the classic
        /// page of UWGameScreenUw2 computes it on the screen.
        /// </summary>
        private static void fCheckStatsPageGeometry()
        {
            Check.Group("Stats page per game (UWStatsPageGeometry - UW2 plan stage U5 step 1)");

            UWStatsPageGeometry lOUw1 = UWStatsPageGeometry.Uw1;
            UWStatsPageGeometry lOUw2 = UWStatsPageGeometry.Uw2;

            Check.Str("uw1: the lines where the modern page had them",
                lOUw1.NameTop + " " + lOUw1.NameCentreX.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + lOUw1.ClassTop + " "
                + lOUw1.LabelLeft + " " + lOUw1.ValueRight + " " + lOUw1.ValueTop(0) + " " + lOUw1.ValueTop(5) + " " + lOUw1.SkillTop(0) + " "
                + lOUw1.SkillTop(5), "7 41.5 14 6 76 21 56 64 99");
            Check.Str("uw1: the paging strip, the bottom eight rows split at 37",
                lOUw1.PagingLeft + " " + lOUw1.PagingRight + " " + lOUw1.PagingTop + " " + lOUw1.PagingBottom + " " + lOUw1.PagingSplit, "0 83 106 114 37");
            Check.Str("uw2: the lines of RedispStat on the page (show at 0xEB, 0xC0)",
                lOUw2.NameTop + " " + lOUw2.NameCentreX.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + lOUw2.ClassTop + " "
                + lOUw2.LabelLeft + " " + lOUw2.ValueRight + " " + lOUw2.ValueTop(0) + " " + lOUw2.ValueTop(5) + " " + lOUw2.SkillTop(0) + " "
                + lOUw2.SkillTop(5), "3 41 10 5 74 17 52 60 95");
            Check.Str("uw2: the page and its colours", lOUw2.PageWidth + "x" + lOUw2.PageHeight + " " + lOUw2.ValueColourIndex + " "
                + lOUw2.SkillColourIndex + " " + lOUw2.IsPageOpaque + " " + lOUw1.IsPageOpaque, "79x112 196 201 True False");

            // Every row against the screen's y of RedispStat: sp_att (2 - i) * 7 + 0xA1, sp_hp 0x9A, sp_mp 0x93, sp_xp
            // 0x8C; sp_skill 0x84 - i * 7. A string's top y counted up, the page's top at row 7 of the screen.
            int[] liValueY = { 0xAF, 0xA8, 0xA1, 0x9A, 0x93, 0x8C };
            int liWrong = 0;

            for (int liRow = 0; liRow < 6; liRow++)
            {
                if (lOUw2.ValueTop(liRow) != 199 - liValueY[liRow] - 7)
                    liWrong++;

                if (lOUw2.SkillTop(liRow) != 199 - (0x84 - (liRow * 7)) - 7)
                    liWrong++;
            }

            fExpectInt("uw2: every value and skill row as RedispStat's", liWrong, 0);

            int liDiffer = 0;
            int liBack = 0;
            int liForward = 0;

            for (int liRow = 0; liRow < lOUw2.PageHeight; liRow++)
            {
                for (int liX = 0; liX < lOUw2.PageWidth; liX++)
                {
                    // The classic page: the screen's x and y counted up, in the region from (0xF0, 0x51) to 0x13B, its
                    // bottom ten rows, left of 0x26 back.
                    int liScreenX = liX + 0xEB;
                    int liScreenY = 199 - (liRow + 7);
                    int liClassic = 0;

                    if (liScreenX >= 0xF0 && liScreenX <= 0x13B && liScreenY >= 0x51 && liScreenY < 0x51 + 10)
                        liClassic = liScreenX - 0xF0 < 0x26 ? -1 : 1;

                    int liOurs = lOUw2.PagingStepAt(liX + 0.5f, liRow + 0.5f);

                    if (liOurs != liClassic)
                        liDiffer++;
                    else if (liOurs < 0)
                        liBack++;
                    else if (liOurs > 0)
                        liForward++;
                }
            }

            fExpectInt("uw2: every pixel pages as mous_in_stat", liDiffer, 0);
            // The region reaches to 0x13B, two columns past the page's 79: 36 of its 38 forward columns lie on the page.
            Check.Str("uw2: the strip's halves on the page, 38 columns back and 36 forward, ten rows", liBack + " " + liForward, "380 360");
        }

        /// <summary>The second game's words on its original page (stage U5 step 1): its block 2, upper case.</summary>
        private static void fCheckUw2StatsPageWords(CheckContext pOContext)
        {
            Check.Group("The second game's stats page words (UW2 plan stage U5 step 1)");

            if (pOContext.Uw2DataPath == null)
            {
                Check.Pass("uw2", "no second game here: skipped");

                return;
            }

            UWStrings lOStrings = new UWStrings(System.IO.File.ReadAllBytes(System.IO.Path.Combine(pOContext.Uw2DataPath, "STRINGS.PAK")));
            UWPlayerData lOPlayer = UWPlayerData.FromPlain(UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter
            {
                Name = "Selfcheck", CharacterClass = 0
            }));
            UWPlayerVitals lOVitals = new UWPlayerVitals(null);

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);

            UWCharacterSheet lOSheet = new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Original, 6).Bind(lOStrings, lOPlayer, lOVitals, UWGame.Uw2);
            string lsRows = string.Empty;

            for (int liRow = 0; liRow < 4; liRow++)
            {
                lOSheet.TryGetSkill(lOSheet.EntryAtRow(liRow), out string lsName, out string lsValue);
                lsRows += lsName + ";";
            }

            Check.Str("uw2: the class upper case (0x17 + class)", lOSheet.ClassName, "FIGHTER");
            Check.Str("uw2: the list's first rows, the skill points' word 0x1F + 100 first", lsRows, "SKILL PT;ATTACK;DEFENSE;BAREHAND;");
        }
    }
}
