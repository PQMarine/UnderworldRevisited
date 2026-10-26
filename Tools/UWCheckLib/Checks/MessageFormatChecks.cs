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
    /// TRANSLATION WAVE 4 (2026-10-26): the frames' format (UWMessageFormat) - slots in game
    /// fragments, select by a name's gender and case, plural by CLDR category with # and =N, one level
    /// of nesting, quoting, the safety of names and typed text (put in as they stand, braces and all),
    /// a fragment that names itself; the reader against string.Format on every text of the English
    /// table and of the repository's language files (the same result: the English stays byte-exact);
    /// Check's findings; the placeholders of a text with select; a language file's frame that leaves
    /// an argument to its fragments (only frames of UWTextFragments may); the name format generalised
    /// (UWNameFormat: wave 2's forms read as before, named case and per-category plural forms, the
    /// fallbacks) and German asking it for a named case. Every word made up. With the data: a
    /// made-up de pack whose anvil fragment holds the name, gendered frames through the game's entry
    /// points, and that the player's English STRINGS.PAK holds no brace at all.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckMessageFormat()
        {
            Check.Group("Frames with slots, select and plural (UWMessageFormat - translation wave 4)");

            fCheckFormatAgainstStringFormat();

            UWNamePhrase lOWumpel = new UWNamePhrase("den Wumpel", "m", "acc");
            UWNamePhrase lOZirbe = new UWNamePhrase("die Zirbe", "f", "acc");
            UWNamePhrase lOEnglish = new UWNamePhrase("wumpel", null, "acc");

            Check.Str("slots: a fragment names the frame's argument", UWMessageFormat.Format("{0}.", new object[] { new UWTextFragment("Halb geflickt hast du {1}"), lOWumpel }, "de"),
                "Halb geflickt hast du den Wumpel.");
            Check.Str("slots: a fragment without a slot is its text, the frame as string.Format", UWMessageFormat.Format("{0}{1}.",
                new object[] { new UWTextFragment("You mended the "), "wumpel" }, "en"), "You mended the wumpel.");
            Check.Str("select: gender.case, then gender, then other", UWMessageFormat.Format("{0,select,m.acc{A}m{B}other{C}}|{1,select,m.acc{A}f{B}other{C}}|{2,select,m{B}other{C}}",
                new object[] { lOWumpel, lOZirbe, lOEnglish }, "de"), "A|B|C");
            Check.Str("select: a plain argument by its text", UWMessageFormat.Format("{0,select,ja{yes}other{no}}/{1,select,ja{yes}other{no}}", new object[] { "ja", "nein" }, "de"),
                "yes/no");
            Check.Str("plural: Russian-style categories with #", fPlurals("ru", "{0,plural,one{# glump}few{# glumpa}many{# glumpov}other{# glumpa}}", 1, 2, 5, 11, 21, 22, 25, 111),
                "1 glump|2 glumpa|5 glumpov|11 glumpov|21 glump|22 glumpa|25 glumpov|111 glumpov");
            Check.Str("plural: French 0 and 1 are one; =N first; ja only other", fPlurals("fr", "{0,plural,=1{un seul}one{# glop}other{# glops}}", 0, 1, 2) + "|"
                + fPlurals("ja", "{0,plural,one{x}other{#z}}", 1, 3), "0 glop|un seul|2 glops|1z|3z");
            Check.Str("plural: a name's count, a number in a text, no number takes other", UWMessageFormat.Format("{0,plural,one{one}other{other}}/{1,plural,one{one}other{#}}/{2,plural,one{one}other{x}}",
                new object[] { new UWNamePhrase("glumps", "p", null, 1), " 1", new UWNamePhrase("glumps", "m", null, -1) }, "en"), "one/one/x");
            Check.Str("nesting: a select inside a plural, # of the plural inside it", UWMessageFormat.Format("{1,plural,one{{0,select,f{eine #}other{ein #}}}other{{0,select,f{# Stueck f}other{# Stueck}}}}",
                new object[] { lOZirbe, 1 }, "de") + "|" + UWMessageFormat.Format("{1,plural,one{{0,select,f{eine #}other{ein #}}}other{{0,select,f{# Stueck f}other{# Stueck}}}}",
                new object[] { lOWumpel, 4 }, "de"), "eine 1|4 Stueck");
            Check.Str("quoting: '{' '}' '#' are characters; {{ }} at the top as string.Format", UWMessageFormat.Format("'{'{0}'}' {{x}} {1,plural,other{'#'#}}",
                new object[] { "a", 3 }, "en"), "{a} {x} #3");

            string lsTyped = "{0}{1,select,m{boom}other{x}}#'{'";

            Check.Str("safety: a typed text or a name is put in as it stands, braces and all", UWMessageFormat.Format("{0,select,other{<{0}>}} {1}",
                new object[] { new UWNamePhrase(lsTyped, "m"), lsTyped }, "de"), "<" + lsTyped + "> " + lsTyped);
            Check.Str("safety: a fragment naming itself stops after two levels", UWMessageFormat.Format("{0}", new object[] { new UWTextFragment("a{0}") }, "en"),
                "aaa{0}");

            Check.Str("check: a good text has nothing", string.Join("; ", UWMessageFormat.Check("{0,select,m{a {1}}other{b}} {2,plural,one{#}other{#}}", 3, "de")), string.Empty);
            Check.Str("check: no other, an argument too far, a bad key, nesting too deep, a category the language lacks",
                string.Join("; ", UWMessageFormat.Check("{0,select,m{a}}", 2)) + " | " + string.Join("; ", UWMessageFormat.Check("{3}", 2)) + " | "
                + string.Join("; ", UWMessageFormat.Check("{0,plural,lots{a}other{b}}")) + " | "
                + string.Join("; ", UWMessageFormat.Check("{0,select,a{{1,select,b{{2,select,c{x}other{y}}}other{z}}}other{w}}")) + " | "
                + string.Join("; ", UWMessageFormat.Check("{0,plural,few{a}other{b}}", -1, "de")),
                "error: a select without its other{...} at 0 | error: {3} names an argument the frame does not have (it has 2) | "
                + "error: plural key lots is no CLDR category (zero, one, two, few, many, other) at 10 | "
                + "error: select or plural nested more than one level deep at 24 | warning: plural key few: the language de never takes it (it has one, other)");
            Check.Str("check: braces that do not pair", string.Join("; ", UWMessageFormat.Check("{0,select,m{a}other{b}")) + " | "
                + string.Join("; ", UWMessageFormat.Check("a } b")) + " | " + string.Join("; ", UWMessageFormat.Check("{x}")),
                "error: a select not closed at 0 | error: a } that closes nothing at 2 | error: a { not followed by an argument number at 0");
            Check.Str("placeholders: a text with select counts its branches", UWText.PlaceholderSet("{1,select,m{a {0}}other{{0}}}") + "|"
                + UWText.PlaceholderCount("{2,plural,one{#}other{{0}}}"), "0,1|3");

            UWTextLanguageFile lOFile = UWTextLanguageFile.Parse("msg.repair.result\t{0}.\nmsg.item.locked\tzu.\nmsg.item.empty\t{0,select,m{a}}\n"
                + "msg.level_up\t{0}{1,plural,one{# Stufe}other{# Stufen}}\n");

            Check.Str("language file: a frame with fragments may leave an argument out, another may not; select must read",
                string.Join(",", lOFile.Texts.Keys.OrderBy(psKey => psKey, StringComparer.Ordinal)) + " | " + string.Join(" / ", lOFile.Problems),
                "msg.level_up,msg.repair.result | line 2: msg.item.locked has other placeholders than the English / "
                + "line 3: msg.item.empty a select without its other{...} at 0");
            Check.Str("language file: what a frame leaves to its fragments", UWTextLanguageFile.LeftOut("msg.repair.result", "{0}.") + "|"
                + UWTextLanguageFile.LeftOut("msg.repair.result", "{1}{0}.") + "|" + UWTextLanguageFile.LeftOut("msg.repair.estimate", "{0}{1}{2}{4}"), "1||3");

            StringBuilder lOUses = new StringBuilder();

            foreach (UWTextFragments.Use lOUse in UWTextFragments.Uses)
            {
                string lsEnglish = UWText.English(lOUse.Frame);

                if (lsEnglish == null || lOUse.Argument >= UWText.PlaceholderCount(lsEnglish) || lOUse.First > lOUse.Last)
                    lOUses.Append(lOUse.Frame).Append(' ').Append(lOUse.Block).Append(':').Append(lOUse.First).Append("; ");
            }

            Check.Str("fragments: every use names a frame of the table and an argument it has (" + UWTextFragments.Uses.Length + " uses)", lOUses.ToString(), string.Empty);

            fCheckNameFormat();
        }

        /// <summary>The reader and string.Format give the same text for every text of the English table
        /// and of the repository's language files, with every argument filled.</summary>
        private static void fCheckFormatAgainstStringFormat()
        {
            List<string> lsTexts = new List<string>();

            foreach (string lsId in UWText.TextIds)
                lsTexts.Add(UWText.English(lsId));

            string lsRoot = CheckContext.ProjectRoot;
            string lsFolder = lsRoot != null ? Path.Combine(lsRoot, "Languages") : null;

            if (lsFolder != null && Directory.Exists(lsFolder))
            {
                foreach (string lsFile in Directory.GetFiles(lsFolder, "*.tsv").Where(psFile => !psFile.EndsWith(UWGameTextPack.FileSuffix, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(psFile => psFile, StringComparer.Ordinal))
                {
                    UWTextLanguageFile lOFile = UWTextLanguageFile.Parse(File.ReadAllText(lsFile, Encoding.UTF8));

                    foreach (string lsId in UWText.TextIds)
                    {
                        if (lOFile.Texts.TryGetValue(lsId, out string lsText) && !UWMessageFormat.IsExtended(lsText))
                            lsTexts.Add(lsText);
                    }
                }
            }

            int liDiffering = 0;
            string lsFirst = string.Empty;

            foreach (string lsText in lsTexts)
            {
                int liCount = UWText.PlaceholderCount(lsText);
                object[] laArgs = new object[liCount];

                for (int liAt = 0; liAt < liCount; liAt++)
                    laArgs[liAt] = "a" + liAt + (liAt % 2 == 0 ? "{" : "}");

                string lsWanted;
                string lsGot;

                try
                {
                    lsWanted = string.Format(lsText, laArgs);
                }
                catch (FormatException)
                {
                    lsWanted = "(FormatException)";
                }

                try
                {
                    lsGot = UWMessageFormat.Render(lsText, laArgs, "en");
                }
                catch (FormatException)
                {
                    lsGot = "(FormatException)";
                }

                if (lsGot != lsWanted)
                {
                    if (liDiffering == 0)
                        lsFirst = lsText + " -> " + lsGot + " not " + lsWanted;

                    liDiffering++;
                }
            }

            Check.Str("the reader as string.Format on every text of the table and the language files (" + lsTexts.Count + " texts)",
                liDiffering == 0 ? string.Empty : liDiffering + " differ, first: " + lsFirst, string.Empty);
        }

        private static string fPlurals(string psCode, string psPattern, params int[] piCounts)
        {
            return string.Join("|", piCounts.Select(piCount => UWMessageFormat.Format(psPattern, new object[] { piCount }, psCode)));
        }

        // ------------------------------------------------- The name format generalised

        private static void fCheckNameFormat()
        {
            UWNameFormat lOWeak = UWNameFormat.Parse("m:Murkel|Murkeln&Murkeln");
            UWNameFormat lORich = UWNameFormat.Parse("m:Wumpel|gen=Wumpla|ins=Wumplem|Wumpl&Wumple&few=Wumple&many=Wumpli&dat=Wumplen");

            Check.Str("name format: wave 2's forms read as before", lOWeak.Gender + "|" + lOWeak.Article + "|" + lOWeak.Singular + "|" + lOWeak.Oblique + "|" + lOWeak.Plural
                + "|" + UWNameFormat.Parse("md:Oberwumpel der_Tiefe").Singular + "|" + UWNameFormat.Parse("fo:Schmiere").Article + "|" + UWNameFormat.Parse("p:Klunker").IsPluralOnly
                + "|" + (UWNameFormat.Parse("a_wumpel&wumpels") == null) + "|" + (UWNameFormat.Parse("x:Wumpel") == null) + "|" + (UWNameFormat.Parse("c:Wump", "mfnp") == null),
                "m|i|Murkel|Murkeln|Murkeln|Oberwumpel der Tiefe|o|True|True|True|True");
            Check.Str("name format: named cases, the oblique for the rest, the singular in the nominative", lORich.SingularIn("gen") + "|" + lORich.SingularIn("ins") + "|"
                + lORich.SingularIn("dat") + "|" + lORich.SingularIn("nom") + "|" + lORich.SingularIn(null), "Wumpla|Wumplem|Wumpl|Wumpel|Wumpel");
            Check.Str("name format: a count's category, one the singular, the plain plural for the rest", lORich.PluralFor("one") + "|" + lORich.PluralFor("few") + "|"
                + lORich.PluralFor("many") + "|" + lORich.PluralFor("other") + "|" + UWNameFormat.Parse("p:Klunker&one=Klunkerl").PluralFor("one") + "|"
                + UWNameFormat.Parse("p:Klunker").PluralFor("one") + "|" + UWNameFormat.Parse("n:Ding").PluralFor("many"), "Wumpel|Wumple|Wumpli|Wumple|Klunkerl|Klunker|Ding");
            Check.Str("name format: an uncounted plural in a case", lORich.PluralIn("dat") + "|" + lORich.PluralIn("acc"), "Wumplen|Wumple");
            Check.Str("name format: x=y with no known key is text; odd segments are reported", UWNameFormat.Parse("n:Ding|a=b").Oblique + "|"
                + string.Join(", ", UWNameFormat.Parse("n:Ding|Dinge|Dingen&&gen=").Problems),
                "a=b|a second form without a case key after |: Dingen, an empty plural after &, an empty form for gen after &");
            Check.Str("name format: checked for a language", lORich.Check("mf", new[] { "nom", "gen", "ins", "dat" }, "ru") + "|"
                + UWNameFormat.Parse("c:Wump&many=Wumpe").Check("mfn", new[] { "nom", "acc" }, "de"),
                "|the gender c is none of the language's (mfn); the plural category many is none of de's (one other)");
            Check.Str("German: a named case form first, the oblique for the rest", UWGrammar.German.TheName("m:Wumpel|dat=Wumpele", UWGrammarCase.Dative) + "|"
                + UWGrammar.German.TheName("m:Wumpel|dat=Wumpele", UWGrammarCase.Accusative) + "|" + UWGrammar.German.TheName("m:Murkel|Murkeln|dat=Murkle", UWGrammarCase.Accusative)
                + "|" + UWGrammar.German.TheName("p:Klunker&dat=Klunkern", UWGrammarCase.Dative) + "|" + UWGrammar.German.GenderOf("fd:Zirbe") + "|"
                + (UWGrammar.German.GenderOf("a_wumpel") ?? "null") + "|" + (UWGrammar.English.GenderOf("m:Wumpel") ?? "null"),
                "dem Wumpele|den Wumpel|den Murkeln|den Klunkern|f|null|null");
            Check.Str("German: the name check knows the named forms", (UWGrammar.German.CheckName("m:Wumpel|dat=Wumpele&Wumpels") ?? "fine") + "|"
                + UWGrammar.German.CheckName("m:Wumpel|ins=Wumplem&few=Wumple"),
                "fine|the case ins is none the language asks for (nom acc dat); the plural category few is none of de's (one other)");
        }

        // ------------------------------------------------- With the data

        private static void fCheckMessageFormatData(DataImport pOData)
        {
            Check.Group("Frames with slots and select on the data (a made-up de pack - translation wave 4)");

            if (pOData == null || pOData.Strings == null)
            {
                Check.Fail("format data: data", "not loaded");

                return;
            }

            int liBraces = 0;

            foreach (KeyValuePair<int, UWStringBlock> lOPair in pOData.Strings.Blocks)
            {
                foreach (string lsText in lOPair.Value.Strings)
                {
                    if (lsText != null && (lsText.IndexOf('{') >= 0 || lsText.IndexOf('}') >= 0))
                        liBraces++;
                }
            }

            Check.Int("format data: the player's English STRINGS.PAK holds no brace (its fragments take string.Format's way)", liBraces, 0);

            // validate-tsv's view of slots (UWGameTextPack.Validate): a piece of a frame read as the frame,
            // a word of block 5 with {0} the name, a brace elsewhere shown as written, a frame that leaves
            // an argument its pieces do not name.
            UWGameTextPack lOBad = UWGameTextPack.Parse("1\t145\tHalb {2} da\n1\t146\tGanz {1,plural,few{x}other{y}}\n5\t97\t{0,select,f{a}}\n1\t2\tmit {Klammer}\n"
                + "1\t141\tKaputt {1}\n1\t142\tVerbeult\n");
            Func<string, string> lOFrame = psId => psId == "msg.repair.result" ? "{0}." : null;

            Check.Str("format data: validate-tsv reads the slots", string.Join(" / ", lOBad.Validate(pOData.Strings, UWGrammar.German, lOFrame, "de")
                .Select(pOFinding => pOFinding.ToString())),
                "error line 0 (1:144): the frame msg.repair.result leaves {1} to its pieces ({0} this piece, {1} the thing (accusative)), this one does not name it (the pack leaves it English) / "
                + "error line 1 (1:145): the frame msg.repair.result leaves {1} to its pieces ({0} this piece, {1} the thing (accusative)), this one does not name it / "
                + "error line 1 (1:145): slots of a piece of the frame msg.repair.result ({0} this piece, {1} the thing (accusative)): {2} names an argument the frame does not have (it has 2) / "
                + "warning line 2 (1:146): slots of a piece of the frame msg.repair.result ({0} this piece, {1} the thing (accusative)): plural key few: the language de never takes it (it has one, other) / "
                + "error line 3 (5:97): slots of a word beside a name ({0} the name): a select without its other{...} at 0 / "
                + "warning line 4 (1:2): a brace in a string that is no piece of a frame: the game shows it as written / "
                + "error line 6 (1:142): the frame msg.repair.result leaves {1} to its pieces ({0} this piece, {1} the thing (accusative)), this one does not name it / "
                + "warning line 6 (1:142): the English ends with a blank, the text does not");

            const int liWumpel = 0x00;
            const int liZirbe = 0x01;
            const int liKind = 0x41;
            string lsFolder = Path.Combine(Path.GetTempPath(), "UWSelfCheck-format-" + Environment.ProcessId);
            StringBuilder lOPack = new StringBuilder("# made-up names and lines, nothing of the game\n");

            lOPack.Append("4\t").Append(liWumpel + 1).Append("\tm:Wumpel&Wumpels\n");
            lOPack.Append("4\t").Append(liZirbe + 1).Append("\tf:Zirbe&Zirben\n");
            lOPack.Append("4\t").Append(liKind + 1).Append("\tm:Grummler&Grummler\n");
            lOPack.Append("1\t145\tHalb geflickt hast du {1}\n");
            lOPack.Append("1\t146\tGanz {1,select,f{geflickt ist sie}other{geflickt ist er}}: {1}\n");
            lOPack.Append("1\t").Append(UWItemMessages.FirstTasteMessage + 4).Append("\t{0} {0,select,f{war}other{war}} lecker\n");
            lOPack.Append("1\t").Append(UWCritterRules.TheftMessageBase).Append("\t{0,select,m{Er}other{Es}} grollt.\n");
            lOPack.Append("1\t148\tStufe {1} da\n");

            const string lsFrames = "msg.repair.result\t{0}.\nmsg.item.taste\t{1}!\nmsg.item.angered\t{1}\nmsg.level_up\t{0}, {1,plural,one{# Ding}other{# Dinge}}\n"
                + "msg.wear.damaged\t{0} {0,select,f{ist verbeult (sie)}other{ist verbeult}}.\n";

            try
            {
                Directory.CreateDirectory(lsFolder);

                string lsGerman = Path.Combine(lsFolder, "de.strings.tsv");

                File.WriteAllText(lsGerman, lOPack.ToString(), new UTF8Encoding(false));

                DataImport lOGerman = new DataImport(pOData.DataPath, null, lsGerman);
                UWTextLanguageFile lOFrames = UWTextLanguageFile.Parse(lsFrames);

                Check.Str("format data: the made-up frames read", lOFrames.Texts.Count + "|" + string.Join("; ", lOFrames.Problems), "5|");

                UWText.UseLanguage("de", lOFrames);

                Check.Str("format data: the anvil's name inside the fragment, select on its gender", UWRepairRules.ResultMessage(lOGerman, UWRepairRules.OutcomeEnum.Partially, liWumpel) + "|"
                    + UWRepairRules.ResultMessage(lOGerman, UWRepairRules.OutcomeEnum.Fully, liZirbe) + "|" + UWRepairRules.ResultMessage(lOGerman, UWRepairRules.OutcomeEnum.Fully, liWumpel),
                    "Halb geflickt hast du den Wumpel.|Ganz geflickt ist sie: die Zirbe.|Ganz geflickt ist er: den Wumpel.");
                Check.Str("format data: the taste and the theft with the name in the fragment", UWItemMessages.Taste(lOGerman, liZirbe, 4) + "|"
                    + UWItemMessages.Angered(lOGerman, liKind, 0), "Die Zirbe war lecker!|Er grollt.");
                Check.Str("format data: the level-up's number through plural, a gendered frame", UWPlayerVitals.LevelUpMessage(lOGerman, 1) + "|"
                    + UWPlayerVitals.LevelUpMessage(lOGerman, 12) + "|" + UWEquipmentWear.Message(lOGerman, liZirbe, false) + "|" + UWEquipmentWear.Message(lOGerman, liWumpel, false),
                    "Stufe 1 da, 1 Ding|Stufe 12 da, 12 Dinge|Deine Zirbe ist verbeult (sie).|Dein Wumpel ist verbeult.");

                UWText.UseEnglish();

                // The English frame "{0}{1}." writes the name after the fragment as well: a fragment with
                // slots wants its own language's frame (validate-tsv says which frame leaves what out).
                Check.Str("format data: English frames over the made-up fragments fill the slots too", UWRepairRules.ResultMessage(lOGerman, UWRepairRules.OutcomeEnum.Partially, liWumpel),
                    "Halb geflickt hast du den Wumpelden Wumpel.");
            }
            catch (Exception lOError)
            {
                Check.Fail("format data: a made-up pack", lOError.GetType().Name + ": " + lOError.Message);
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
        }
    }
}
