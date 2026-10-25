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
            lOStart.FallbackMaxHP = 30f;
            lOStart.FallbackCurrentHP = 12.5f;

            UWPlayerVitals lOVitals = new UWPlayerVitals(null);

            lOVitals.Initialise(null, lOStart);
            lOVitals.SetMana(19.5f, 7.5f);

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

            // Vitality 12.5 and mana 7.5 of 19.5 round half to even as Mathf.RoundToInt did.
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

            // The signature follows every number shown.
            int liBefore = lOOriginal.Signature;

            lOVitals.SetMana(19.5f, 9f);
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
    }
}
