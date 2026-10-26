using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// TRANSLATION STEP 3R (2026-10-26): the language switched while the game runs, its engine-free
    /// part. The game's strings switched between two made-up packs (a German one and one of another
    /// code), back to none and to the German again (UWGameData.UseGameText) are each time exactly the
    /// strings of a fresh load with that pack - every block, every string - with its grammar; the
    /// block lists stay the same objects (refilled in place); a pack that cannot be read leaves
    /// English. The port's own texts switched from one language file to another and back are those of
    /// the file alone. Made-up strings only.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckGameTextSwitch(DataImport pOData)
        {
            Check.Group("Language switched while the game runs (UWGameData.UseGameText, UWText - translation step 3R)");

            if (pOData == null || pOData.DataPath == null)
            {
                Check.Fail("switch: data", "not loaded");

                return;
            }

            string lsFolder = Path.Combine(Path.GetTempPath(), "UWSelfCheck-switch-" + Environment.ProcessId);

            try
            {
                Directory.CreateDirectory(lsFolder);

                string lsGerman = Path.Combine(lsFolder, "de.strings.tsv");
                string lsOther = Path.Combine(lsFolder, "xx.strings.tsv");

                // Made-up rows: a few the two packs share, a few only one of them has.
                File.WriteAllText(lsGerman, "# made-up\n4\t1\tm:Wumpel&Wumpels\n4\t3\tf:Zirbe&Zirben\n1\t2\tEin erfundener Satz.\n"
                    + "1\t149\tDu hast Stufe \n5\t97\tgrummelig\n", new UTF8Encoding(false));
                File.WriteAllText(lsOther, "# made-up\n4\t1\ta_wumpel&wumpels\n4\t2\ta_gloop&gloops\n1\t3\tA made-up line.\n",
                    new UTF8Encoding(false));

                UWGameData lOEnglish = new UWGameData(pOData.DataPath);
                UWGameData lOFreshGerman = new UWGameData(pOData.DataPath, lsGerman);
                UWGameData lOFreshOther = new UWGameData(pOData.DataPath, lsOther);
                UWGameData lOSwitched = new UWGameData(pOData.DataPath);
                List<string> lsNames = lOSwitched.Strings.Blocks[4].Strings;

                Check.Bool("switch: the made-up packs change something", fSameStrings(lOEnglish.Strings, lOFreshGerman.Strings) == null
                    || fSameStrings(lOEnglish.Strings, lOFreshOther.Strings) == null, false);

                fSwitchTo(lOSwitched, lsGerman, lOFreshGerman, "de", "to de");
                fSwitchTo(lOSwitched, lsOther, lOFreshOther, "xx", "de to xx");
                fSwitchTo(lOSwitched, null, lOEnglish, "en", "xx to none");
                fSwitchTo(lOSwitched, lsGerman, lOFreshGerman, "de", "none to de again");
                fSwitchTo(lOSwitched, lsGerman + ".missing", lOEnglish, "en", "de to a pack that cannot be read");

                Check.Bool("switch: the block lists are the same objects, refilled", ReferenceEquals(lsNames, lOSwitched.Strings.Blocks[4].Strings), true);

                // Through a DataImport: its grammar and a message line follow the switch.
                DataImport lOImport = new DataImport(pOData.DataPath);
                string lsBefore = UWItemMessages.IsLocked(lOImport, 0);

                lOImport.GameData.UseGameText(lsGerman, out _);

                string lsGermanLine = UWItemMessages.IsLocked(lOImport, 0) + "|" + UWGrammar.Of(lOImport).Code;

                lOImport.GameData.UseGameText(null, out _);

                Check.Str("switch: a message line follows the names and the grammar", lsGermanLine + "|" + (UWItemMessages.IsLocked(lOImport, 0) == lsBefore),
                    "The der Wumpel is locked.|de|True");
            }
            catch (Exception lOError)
            {
                Check.Fail("switch: the game's strings", lOError.GetType().Name + ": " + lOError.Message);
            }
            finally
            {
                try
                {
                    Directory.Delete(lsFolder, true);
                }
                catch (Exception)
                {
                }
            }

            fCheckTextSwitch();
        }

        /// <summary>One switch, compared with the fresh load.</summary>
        private static void fSwitchTo(UWGameData pOSwitched, string psPack, UWGameData pOFresh, string psGrammar, string psLabel)
        {
            bool lbRead = pOSwitched.UseGameText(psPack, out UWGameTextPack lOPack);

            Check.Str("switch " + psLabel + ": the strings are a fresh load's", fSameStrings(pOSwitched.Strings, pOFresh.Strings) ?? "same", "same");
            Check.Str("switch " + psLabel + ": the grammar and the pack", lbRead + "|" + pOSwitched.Grammar.Code + "|" + (lOPack != null) + "|"
                + (pOSwitched.GameText != null), "True|" + psGrammar + "|" + (pOFresh.GameText != null) + "|" + (pOFresh.GameText != null));
        }

        /// <summary>Null when both hold the same blocks with the same strings, else the first difference.</summary>
        private static string fSameStrings(UWStrings pOA, UWStrings pOB)
        {
            if (pOA.Blocks.Count != pOB.Blocks.Count)
                return "block count " + pOA.Blocks.Count + " and " + pOB.Blocks.Count;

            foreach (KeyValuePair<int, UWStringBlock> lOPair in pOA.Blocks)
            {
                if (!pOB.Blocks.TryGetValue(lOPair.Key, out UWStringBlock lOOther))
                    return "block " + lOPair.Key + " missing";

                List<string> lsA = lOPair.Value.Strings;
                List<string> lsB = lOOther.Strings;

                if (lsA.Count != lsB.Count)
                    return "block " + lOPair.Key + ": " + lsA.Count + " and " + lsB.Count + " strings";

                for (int liAt = 0; liAt < lsA.Count; liAt++)
                {
                    if (!string.Equals(lsA[liAt], lsB[liAt], StringComparison.Ordinal))
                        return lOPair.Key + ":" + liAt + " differs";
                }
            }

            return null;
        }

        /// <summary>The port's own texts: a file, another, the first again, English - each time what the
        /// file alone gives.</summary>
        private static void fCheckTextSwitch()
        {
            UWTextLanguageFile lOFirst = UWTextLanguageFile.Parse("setup.done\tErledigt\nmsg.item.locked\t{0} ist zu.\n");
            UWTextLanguageFile lOSecond = UWTextLanguageFile.Parse("setup.done\tKlaar\nhud.menu.use\tBruuk\n");

            try
            {
                UWText.UseLanguage("de", lOFirst);
                UWText.UseLanguage("nds", lOSecond);
                UWText.UseLanguage("de", lOFirst);

                Check.Str("switch: our texts back to the first file", UWText.T("setup.done") + "|" + UWText.F("msg.item.locked", "x") + "|"
                    + UWText.T("hud.menu.use"), "Erledigt|x ist zu.|" + UWText.English("hud.menu.use"));

                int liVersion = UWText.LanguageVersion;

                UWText.MarkGameTextChanged();

                Check.Int("switch: a change of the game's strings counts as a change of language", UWText.LanguageVersion - liVersion, 1);
            }
            finally
            {
                UWText.UseEnglish();
            }

            Check.Str("switch: English again", UWText.T("setup.done") + "|" + UWText.T("hud.menu.use"),
                UWText.English("setup.done") + "|" + UWText.English("hud.menu.use"));
        }
    }
}
