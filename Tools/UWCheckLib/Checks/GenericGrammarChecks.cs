using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// TRANSLATION WAVE 4 (2026-10-26): the grammar of a language without a class of its own
    /// (UWGrammarGeneric) on the two made-up test languages of Tools/Fixtures/i18n - "xg", gendered
    /// with articles by select, the words after the noun agreeing with it, French plural categories;
    /// "xs", Slavic-like without articles, named cases and Russian categories one/few/many. Which
    /// grammar a code takes; names, the look's phrases, counts, words, the definite and possessive
    /// forms, the capital, the letters, the name check. With the data: the fixture packs laid over the
    /// player's strings and their port texts in force - looks, a stack, a creature, the anvil, a lock,
    /// worn equipment and a trap through the game's own entry points. Every word made up.
    /// </summary>
    public static partial class SelfChecks
    {
        private const string FixtureFolder = "Tools/Fixtures/i18n";

        /// <summary>A fixture file of the made-up languages, or null without a source tree.</summary>
        private static string fFixture(string psName)
        {
            string lsRoot = CheckContext.ProjectRoot;

            return lsRoot == null ? null : Path.Combine(lsRoot, FixtureFolder, psName);
        }

        private const string Wumpo = "m:wumpo&wumpos";

        private const string Zirba = "f:zirba&zirbas";

        private const string Wumpel = "m:wumpel|acc=wumpel|loc=wumple&wumpli&few=wumpla&many=wumplov";

        private const string Zirbu = "f:zirba|acc=zirbu|loc=zirbe&zirby&few=zirby&many=zirb";

        private static void fCheckGenericGrammar()
        {
            Check.Group("The generic grammar of a language without a class (UWGrammarGeneric, made-up xg and xs - translation wave 4)");

            Check.Str("generic: which grammar a code takes", UWGrammar.For("en").GetType().Name + "|" + UWGrammar.For("en-GB").GetType().Name + "|"
                + UWGrammar.For("de_AT").GetType().Name + "|" + UWGrammar.For("fr").GetType().Name + "|" + UWGrammar.For("zh-TW").Code + "|"
                + ReferenceEquals(UWGrammar.For("xg"), UWGrammar.For("XG")) + "|" + UWGrammar.For(" ").Code,
                "UWGrammarEnglish|UWGrammarEnglish|UWGrammarGerman|UWGrammarGeneric|zh-TW|True|en");

            string lsXg = fFixture("xg.tsv");
            string lsXs = fFixture("xs.tsv");

            if (lsXg == null || !File.Exists(lsXg) || !File.Exists(lsXs))
            {
                Check.Pass("generic: the fixtures", "skipped - no source tree");

                return;
            }

            UWTextLanguageFile lOXg = UWTextLanguageFile.Parse(File.ReadAllText(lsXg, Encoding.UTF8));
            UWTextLanguageFile lOXs = UWTextLanguageFile.Parse(File.ReadAllText(lsXs, Encoding.UTF8));

            Check.Str("generic: the fixtures' port texts read whole", lOXg.Texts.Count + " " + string.Join("; ", lOXg.Problems) + "|"
                + lOXs.Texts.Count + " " + string.Join("; ", lOXs.Problems), "19 |11 ");

            IUWGrammar lIXg = UWGrammar.For("xg");
            IUWGrammar lIXs = UWGrammar.For("xs");

            try
            {
                UWText.UseLanguage("xg", lOXg);

                Check.Str("xg: names with the article by gender, a plural, a definite name", lIXg.Name(Wumpo, false, true, UWGrammarCase.Nominative) + "|"
                    + lIXg.Name(Zirba, false, true, UWGrammarCase.Nominative) + "|" + lIXg.Name(Zirba, true, true, UWGrammarCase.Nominative) + "|"
                    + lIXg.Name("fd:Kopa da_Lumo", false, true, UWGrammarCase.Nominative) + "|" + lIXg.Name(Zirba, false, false, UWGrammarCase.Dative),
                    "ko wumpo|ka zirba|zirbas|la Kopa da Lumo|zirba");
                Check.Str("xg: the words after the noun, agreeing; a count's plural and category", lIXg.ItemPhrase(Zirba, 1, "{0,select,f{frotta}other{frotto}}", true, true)
                    + "|" + lIXg.ItemPhrase(Wumpo, 3, null, true, false) + "|" + lIXg.ItemPhrase(Wumpo, 1, null, true, false),
                    "ka zirba glima brakka frotta|3 wumpos brakkos|ko wumpo brakko");
                Check.Str("xg: the definite, the possessive, the capital, the label", lIXg.TheName(Zirba, UWGrammarCase.Accusative) + "|"
                    + lIXg.YourName(Wumpo, UWGrammarCase.Nominative) + "|" + lIXg.Sentence("la zirba es  klausa.") + "|" + lIXg.ItemLabel(Zirba, 1, true)
                    + "|" + lIXg.ItemLabel(Wumpo, 2, false) + "|" + lIXg.LiteralLabel("fd:Kopa da Lumo"),
                    "la zirba|tuo wumpo|La zirba es klausa.|Zirba brakka|2 wumpos|La Kopa da Lumo");
                Check.Str("xg: the creature with its attitude word agreeing, named; identify_inv", lIXg.CreatureLook("{0,select,f{grumma}other{grummo}}", "m:grumblo&grumblos", null)
                    + "|" + lIXg.CreatureLook("{0,select,f{grumma}other{grummo}}", "m:grumblo", "Bodo") + "|" + lIXg.IdentifyName(Wumpo, 1, true, false, true)
                    + "|" + lIXg.IdentifyName(Zirba, 4, false, false, true),
                    "Vu zei ko grumblo grummo.|Vu zei ko grumblo grummo nomo Bodo.|ko wumpo glimo|4 zirbas");
                Check.Str("xg: genders, cases, letters (the classic fonts' only), the name check",
                    lIXg.NamesHaveGender + "|" + lIXg.GenderOf(Zirba) + "|" + lIXg.CaseKey(UWGrammarCase.Dative) + "|" + lIXg.TypeableLetters + "|"
                    + lIXg.CheckName("wumpo") + "|" + lIXg.CheckName("n:glimo") + "|" + lIXg.CheckName("m:wumpo&few=wumpi") + "|" + (lIXg.CheckName(Wumpo) ?? "fine"),
                    "True|f|nom|äöü|no gender prefix (m:, f: as in m:name&names) - the name reads by the English rules|the gender n is none of the language's (mf)|"
                    + "the plural category few is none of fr's (one many other)|fine");
                Check.Str("xg: a name left English reads by the English rules", lIXg.Name("a_wumpel&wumpels", false, true, UWGrammarCase.Nominative) + "|"
                    + lIXg.ItemPhrase("a_wumpel&wumpels", 2, null, false, false), "a wumpel|2 wumpels");

                UWText.UseLanguage("xs", lOXs);

                Check.Str("xs: counts by one, few, many (21 one, 11 many)", string.Join("|", new[] { 1, 2, 5, 11, 21, 22, 111 }
                    .Select(piCount => lIXs.ItemPhrase(Wumpel, piCount, null, false, false))), "wumpel|2 wumpla|5 wumplov|11 wumplov|21 wumpel|22 wumpla|111 wumplov");
                Check.Str("xs: no articles; the cases of the roles", lIXs.TheName(Zirbu, UWGrammarCase.Nominative) + "|" + lIXs.TheName(Zirbu, UWGrammarCase.Accusative)
                    + "|" + lIXs.TheName(Zirbu, UWGrammarCase.Dative) + "|" + lIXs.ItemPhrase(Zirbu, 1, null, false, false) + "|" + lIXs.CaseKey(UWGrammarCase.Dative)
                    + "|" + lIXs.TheName("n:glimo", UWGrammarCase.Accusative), "zirba|zirbu|zirbe|zirbu|loc|glimo");
                Check.Str("xs: the charges' frame by plural", lIXs.EnchantmentPart(null, 2) + "|" + lIXs.EnchantmentPart(null, 5) + "|" + lIXs.EnchantmentPart(null, 21),
                    " s 2 blikami| s 5 blikov| s 21 blikom");
                Check.Str("xs: the name check knows the cases and categories", (lIXs.CheckName(Wumpel) ?? "fine") + "|" + lIXs.CheckName("m:wumpel|abl=wumpla&two=wumpe"),
                    "fine|the case abl is none the language asks for (nom gen dat acc ins loc); the plural category two is none of ru's (one few many other)");
            }
            finally
            {
                UWText.UseEnglish();
            }

            Check.Str("generic: English and German stay as they are", UWGrammar.English.Name("a_wumpel&wumpels", false, true, UWGrammarCase.Nominative) + "|"
                + UWGrammar.German.Name("m:Wumpel&Wumpels", false, true, UWGrammarCase.Accusative) + "|" + lIXg.Name(Zirba, false, true, UWGrammarCase.Nominative),
                "a wumpel|einen Wumpel|zirba");
        }

        // ------------------------------------------------- With the data

        private static void fCheckGenericGrammarData(DataImport pOData)
        {
            Check.Group("The generic grammar on the data (the made-up xg and xs packs - translation wave 4)");

            if (pOData == null || pOData.CommonObjectProperties == null)
            {
                Check.Fail("generic data: data", "not loaded");

                return;
            }

            string lsXg = fFixture("xg.strings.tsv");

            if (lsXg == null || !File.Exists(lsXg))
            {
                Check.Pass("generic data: the fixtures", "skipped - no source tree");

                return;
            }

            // The condition word of the three named things at quality 30 (block 5), made up per language.
            StringBuilder lOWords = new StringBuilder();

            for (int liId = 0; liId < 3; liId++)
            {
                if (!pOData.CommonObjectProperties.TryGet(liId, out UWCommonObjectProperties.Entry lOEntry))
                    continue;

                lOWords.Append("5\t").Append(UWCombat.GetConditionStringIndex(lOEntry.QualityType, 30, lOEntry.QualityClass) + 1).Append("\t{WORD}\n");
            }

            string lsFolder = Path.Combine(Path.GetTempPath(), "UWSelfCheck-generic-" + Environment.ProcessId);

            try
            {
                Directory.CreateDirectory(lsFolder);

                string lsXgPack = Path.Combine(lsFolder, "xg.strings.tsv");
                string lsXsPack = Path.Combine(lsFolder, "xs.strings.tsv");

                File.WriteAllText(lsXgPack, File.ReadAllText(lsXg, Encoding.UTF8) + lOWords.ToString().Replace("{WORD}", "{0,select,f{frotta}other{frotto}}"),
                    new UTF8Encoding(false));
                File.WriteAllText(lsXsPack, File.ReadAllText(fFixture("xs.strings.tsv"), Encoding.UTF8)
                    + lOWords.ToString().Replace("{WORD}", "{0,select,f{rzava}n{rzavo}other{rzav}}"), new UTF8Encoding(false));

                // The fixtures as validate-tsv reads them: no error (their names, slots and frames fit).
                StringBuilder lOValid = new StringBuilder();

                foreach (string lsCode in new[] { "xg", "xs" })
                {
                    UWTextLanguageFile lOTexts = UWTextLanguageFile.Parse(File.ReadAllText(fFixture(lsCode + ".tsv"), Encoding.UTF8));
                    UWGameTextPack lOFixture = UWGameTextPack.Parse(File.ReadAllText(fFixture(lsCode + UWGameTextPack.FileSuffix), Encoding.UTF8));

                    UWText.UseLanguage(lsCode, lOTexts);

                    List<UWGameTextPack.Finding> lOFindings = lOFixture.Validate(pOData.Strings, UWGrammar.For(lsCode),
                        psId => lOTexts.Texts.TryGetValue(psId, out string lsText) ? lsText : null, UWText.PluralCode);

                    lOValid.Append(lsCode).Append(' ').Append(lOFindings.Count(pOFinding => pOFinding.IsError)).Append(" errors ")
                        .Append(lOFindings.Count).Append(" findings|");
                }

                UWText.UseEnglish();

                Check.Str("generic data: the fixtures validate (validate-tsv's reading)", lOValid.ToString(), "xg 0 errors 0 findings|xs 0 errors 0 findings|");

                DataImport lOXg = new DataImport(pOData.DataPath, null, lsXgPack);
                DataImport lOXs = new DataImport(pOData.DataPath, null, lsXsPack);

                Check.Str("generic data: the packs' codes choose the generic grammar", UWGrammar.Of(lOXg).Code + "|" + UWGrammar.Of(lOXs).Code + "|"
                    + UWGrammar.Of(lOXg).GetType().Name, "xg|xs|UWGrammarGeneric");

                UWText.LoadLanguage("xg", fFixture("xg.tsv"));

                Check.Str("generic data xg: the look of one and of a stack, the condition word agreeing",
                    UWItemDescriptions.DescribeItem(new UWObject(1) { Quality = 30 }, lOXg, 0, null, UWLoreCheck.ResultUnknown) + "|"
                    + UWItemDescriptions.DescribeItem(new UWObject(0) { Quality = 30, HasQuantity = true, Quantity = 3 }, lOXg, 0, null, UWLoreCheck.ResultUnknown),
                    "Vu zei ka zirba frotta.|Vu zei 3 wumpos frotto.");
                Check.Str("generic data xg: the creature, the lock, the anvil's fragment, worn equipment",
                    UWItemDescriptions.DescribeCreature(new UWNpc(0x41) { NPCwhoami = 0, NPCAttitude = 0 }, UWText.RawObjectName(lOXg, 0x41), lOXg) + "|"
                    + UWItemMessages.IsLocked(lOXg, 1) + "|" + UWRepairRules.ResultMessage(lOXg, UWRepairRules.OutcomeEnum.Partially, 0) + "|"
                    + UWEquipmentWear.Message(lOXg, 1, false),
                    "Vu zei ko grumblo grummo.|La zirba es klausa.|Tu fikkasti lo wumpo a medi.|Tua zirba es rumpa.");

                UWText.LoadLanguage("xs", fFixture("xs.tsv"));

                Check.Str("generic data xs: the look in the accusative, stacks by category, the word agreeing",
                    UWItemDescriptions.DescribeItem(new UWObject(1) { Quality = 30 }, lOXs, 0, null, UWLoreCheck.ResultUnknown) + "|"
                    + UWItemDescriptions.DescribeItem(new UWObject(0) { Quality = 30, HasQuantity = true, Quantity = 5 }, lOXs, 0, null, UWLoreCheck.ResultUnknown) + "|"
                    + UWItemDescriptions.DescribeItem(new UWObject(2) { Quality = 30, HasQuantity = true, Quantity = 3 }, lOXs, 0, null, UWLoreCheck.ResultUnknown),
                    "Vizish rzava zirbu.|Vizish 5 rzav wumplov.|Vizish 3 rzavo glima.");
                Check.Str("generic data xs: the trap on a thing (the place's case), worn equipment by gender",
                    UWTrapDisarming.DisarmedMessage(lOXs, 0x180, 0) + "|" + UWEquipmentWear.Message(lOXs, 2, false) + "|" + UWEquipmentWear.Message(lOXs, 1, false),
                    "Snapka na wumple zatknuta.|Glimo pognuto.|Zirba pognuta.");
            }
            catch (Exception lOError)
            {
                Check.Fail("generic data: the made-up packs", lOError.GetType().Name + ": " + lOError.Message);
            }
            finally
            {
                UWText.UseEnglish();

                try
                {
                    Directory.Delete(lsFolder, true);
                }
                catch (Exception)
                {
                }
            }

            Check.Str("generic data: English data stays English", UWItemMessages.IsLocked(pOData, 1), "The " + UWText.ObjectName(pOData, 1) + " is locked.");
        }
    }
}
