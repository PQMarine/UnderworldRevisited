using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The game-text packs (translation wave 1, 2026-10-26; UWGameTextPack): the template a
    /// translator starts from, made of the player's own STRINGS.PAK, and the check of a pack against
    /// it. Both read STRINGS.PAK afresh, so a --strings pack never stands in for the English.
    /// </summary>
    public static partial class Program
    {
        /// <summary>
        /// uwdump strings-template [path] &lt;code&gt; [--out file] [--empty]: every non-empty string as
        /// a row of a pack for the language code - the English as the text, or with --empty an empty
        /// text under the English as a comment. To stdout (UTF-8) or the file. The original's text: it
        /// goes where the translator chooses and is never committed or shipped.
        /// </summary>
        private static int fStringsTemplate(DumpContext pOContext, List<string> pOArgs)
        {
            string lsOut = fTakeOption(pOArgs, "--out");
            bool lbEmpty = fTakeFlag(pOArgs, "--empty");

            if (pOArgs.Count < 1 || !fIsLanguageCode(pOArgs[0]))
            {
                Console.Error.WriteLine("Which language? A code of letters, digits, - and _, for example: uwdump strings-template de --out de.strings.tsv");

                return 1;
            }

            string lsCode = pOArgs[0];
            UWStrings lOEnglish = fEnglishStrings(pOContext);
            byte[] lyText = new UTF8Encoding(false).GetBytes(UWGameTextPack.Template(lOEnglish, lsCode, lbEmpty));

            // The template read back is the English unchanged - the proof that nothing was lost.
            UWStrings lOBack = fEnglishStrings(pOContext);
            UWGameTextPack lOPack = UWGameTextPack.Parse(Encoding.UTF8.GetString(lyText));

            lOPack.Apply(lOBack);

            int liDiffering = fCountDiffering(lOEnglish, lOBack);

            if (lsOut != null)
            {
                File.WriteAllBytes(lsOut, lyText);
            }
            else
            {
                using (Stream lOStdout = Console.OpenStandardOutput())
                    lOStdout.Write(lyText, 0, lyText.Length);
            }

            Console.Error.WriteLine(string.Format("{0} strings{1}{2}; read back: {3}", lbEmpty ? lOPack.EmptyRows : lOPack.Rows.Count,
                lbEmpty ? " with empty texts" : string.Empty, lsOut != null ? " written to " + lsOut : string.Empty,
                liDiffering == 0 && lOPack.Problems.Count == 0 ? "the English unchanged" : liDiffering + " differ, " + lOPack.Problems.Count + " problems"));

            return liDiffering == 0 && lOPack.Problems.Count == 0 ? 0 : 5;
        }

        /// <summary>
        /// uwdump validate-tsv [path] &lt;file&gt; [--language code]: a pack against the player's English
        /// strings - every error and warning by line (UWGameTextPack.Validate), the rows per block, and
        /// that laying the pack over the English changes exactly the strings it names. The language's
        /// name format is checked too (translation wave 2): the code from --language or the file's name
        /// (de.strings.tsv), its grammar (UWGrammar.For) - German warns about a block 4 name without its
        /// gender prefix. Since translation wave 4 also the slots, select and plural of the strings that
        /// are pieces of a frame (UWTextFragments) and of block 5's words, names in the format in blocks
        /// 1, 7 and 10, and - with the language's own texts beside the pack (de.tsv beside
        /// de.strings.tsv, loaded for this) - that every piece of a frame that leaves an argument to its
        /// pieces names it, the generic grammar's header (UWGrammarGeneric.CheckHeader) and which frames
        /// leave what. Exit 0 without errors, 4 with, 5 for a file that is no UTF-8.
        /// </summary>
        private static int fValidateTsv(DumpContext pOContext, List<string> pOArgs)
        {
            string lsLanguage = fTakeOption(pOArgs, "--language");

            if (pOArgs.Count < 1 || !File.Exists(pOArgs[0]))
            {
                Console.Error.WriteLine("Which pack? An existing UTF-8 file, for example: uwdump validate-tsv Languages/de.strings.tsv");

                return 1;
            }

            string lsContent;

            try
            {
                lsContent = File.ReadAllText(pOArgs[0], new UTF8Encoding(false, true));
            }
            catch (DecoderFallbackException lOError)
            {
                Console.Error.WriteLine("Not UTF-8: " + lOError.Message);

                return 5;
            }

            UWStrings lOEnglish = fEnglishStrings(pOContext);
            UWGameTextPack lOPack = UWGameTextPack.Parse(lsContent);
            string lsCode = lsLanguage ?? UWGrammar.CodeOfPackFile(pOArgs[0]);
            IUWGrammar lIGrammar = UWGrammar.For(lsCode);

            // The language's own texts beside the pack (Languages/de.tsv beside de.strings.tsv, translation
            // wave 4): the frames whose arguments the fragments name, the generic grammar's header.
            string lsTexts = string.IsNullOrEmpty(lsCode) ? null
                : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(pOArgs[0])), lsCode + ".tsv");
            UWTextLanguageFile lOTexts = lsTexts != null && File.Exists(lsTexts) ? UWText.LoadLanguage(lsCode, lsTexts) : null;
            Func<string, string> lOFrame = lOTexts == null ? null : (Func<string, string>)(psId => lOTexts.Texts.TryGetValue(psId, out string lsText) ? lsText : null);
            string lsPluralCode = lOTexts != null ? UWText.PluralCode : lIGrammar.Code;
            List<UWGameTextPack.Finding> lOFindings = lOPack.Validate(lOEnglish, lIGrammar, lOFrame, lsPluralCode);
            int liErrors = 0;
            int liWarnings = 0;

            if (lIGrammar is UWGrammarGeneric)
            {
                foreach (string lsProblem in UWGrammarGeneric.CheckHeader(psId => lOTexts != null && lOTexts.Texts.TryGetValue(psId, out string lsText) ? lsText : null))
                    lOFindings.Insert(0, new UWGameTextPack.Finding { Line = 0, Key = string.Empty, IsError = false, Message = "header of " + lsTexts + ": " + lsProblem });
            }

            foreach (UWGameTextPack.Finding lOFinding in lOFindings)
            {
                if (lOFinding.IsError)
                    liErrors++;
                else
                    liWarnings++;

                Console.WriteLine(lOFinding.ToString());
            }

            // Laid over a fresh copy: the strings it names change, no other.
            UWStrings lOTranslated = fEnglishStrings(pOContext);
            HashSet<string> lOKeys = new HashSet<string>();
            SortedDictionary<int, int> lOPerBlock = new SortedDictionary<int, int>();
            int liSame = 0;

            lOPack.Apply(lOTranslated);

            foreach (UWGameTextPack.Row lORow in lOPack.Rows)
            {
                if (!lOEnglish.Blocks.TryGetValue(lORow.Block, out UWStringBlock lOBlock) || lORow.Index < 1 || lORow.Index >= lOBlock.Strings.Count)
                    continue;

                if (!lOKeys.Add(lORow.Key))
                    continue;

                if (lOBlock.Strings[lORow.Index] == lORow.Text)
                    liSame++;

                lOPerBlock.TryGetValue(lORow.Block, out int liCount);
                lOPerBlock[lORow.Block] = liCount + 1;
            }

            int liChangedElsewhere = 0;

            foreach (KeyValuePair<int, UWStringBlock> lOPair in lOEnglish.Blocks)
            {
                List<string> lsBefore = lOPair.Value.Strings;
                List<string> lsAfter = lOTranslated.Blocks[lOPair.Key].Strings;

                for (int liIndex = 0; liIndex < lsBefore.Count; liIndex++)
                {
                    if (!lOKeys.Contains(lOPair.Key + ":" + liIndex) && lsBefore[liIndex] != lsAfter[liIndex])
                        liChangedElsewhere++;
                }
            }

            if (liChangedElsewhere > 0)
            {
                Console.WriteLine("error: " + liChangedElsewhere + " strings the pack does not name changed");
                liErrors++;
            }

            foreach (KeyValuePair<int, int> lOPair in lOPerBlock)
                Console.WriteLine(string.Format("block {0} (0x{0:X4}): {1} strings", lOPair.Key, lOPair.Value));

            if (lIGrammar is UWGrammarGeneric)
                Console.WriteLine("grammar: " + lIGrammar.Code + " - no class of its own, the header of " + (lOTexts != null ? lsTexts : "(no " + lsCode + ".tsv beside it: the defaults)")
                    + ": genders " + (UWGrammarGeneric.Genders.Length > 0 ? UWGrammarGeneric.Genders : "none") + ", cases " + string.Join(" ", UWGrammarGeneric.Cases)
                    + ", plural rule " + lsPluralCode + " (" + string.Join(" ", UWPluralRules.CategoriesOf(lsPluralCode)) + ")");
            else
                Console.WriteLine("grammar: " + lIGrammar.Code + (lIGrammar.NamesHaveGender
                    ? " - the names of block 4 with a gender prefix (m:, f:, n:, p:)" : " - the English rules for the names"));

            if (lOTexts != null)
            {
                List<string> lsLeft = new List<string>();

                foreach (UWTextFragments.Use lOUse in UWTextFragments.Uses)
                {
                    string lsLeftOut = UWTextLanguageFile.LeftOut(lOUse.Frame, lOFrame(lOUse.Frame));
                    string lsEntry = lOUse.Frame + " leaves {" + lsLeftOut.Replace(",", "},{") + "}";

                    if (lsLeftOut.Length > 0 && !lsLeft.Contains(lsEntry))
                        lsLeft.Add(lsEntry);
                }

                Console.WriteLine("frames of " + lsTexts + (lsLeft.Count == 0 ? ": none leaves an argument to its game pieces"
                    : " that leave an argument to their game pieces: " + string.Join("; ", lsLeft)));
            }

            // THE CLASSIC FIXED BOXES (translation 5.2, UWClassicFit): a string wider than its box is clipped.
            foreach (UWClassicFit.Entry lOEntry in UWClassicFit.Measure(lOTranslated, pOContext.Data.MiscDataFiles, pOContext.Data.Fonts))
            {
                if (!lOEntry.IsOver)
                    continue;

                Console.WriteLine("warning: " + lOEntry.Key + ": \"" + lOEntry.Text + "\" is " + lOEntry.Width + " pixels in a box of " + lOEntry.Box
                    + " (" + lOEntry.Place + ") - clipped; uwdump fit lists them all");
                liWarnings++;
            }

            Console.WriteLine(string.Format("{0} rows: {1} strings with a text ({2} of them the English as it is), {3} empty (stay English); {4} errors, {5} warnings",
                lOPack.Rows.Count + lOPack.EmptyRows, lOKeys.Count, liSame, lOPack.EmptyRows, liErrors, liWarnings));

            return liErrors == 0 ? 0 : 4;
        }

        /// <summary>
        /// uwdump fit [path] [--strings pack] [--over]: every game string the classic look draws into a box of
        /// fixed width (UWClassicFit: character creation's questions, buttons, sex and class line, attribute
        /// labels, skill list and name prompt; the classic stats page's class and skill names) with its width
        /// in pixels in that place's font, its box, and OVER where it is clipped - with --strings in that
        /// language (its .tsv beside it gives the port's own words). --over prints only the clipped ones.
        /// Exit 0 when everything fits, 6 when something is over.
        /// </summary>
        private static int fFit(DumpContext pOContext, List<string> pOArgs)
        {
            bool lbOverOnly = fTakeFlag(pOArgs, "--over");
            // By place, in the order the places come first.
            List<UWClassicFit.Entry> lOEntries = new List<UWClassicFit.Entry>(System.Linq.Enumerable.SelectMany(System.Linq.Enumerable.GroupBy(
                UWClassicFit.Measure(pOContext.Data.Strings, pOContext.Data.MiscDataFiles, pOContext.Data.Fonts), pOEntry => pOEntry.Place), pOGroup => pOGroup));
            int liOver = 0;
            string lsPlace = null;

            foreach (UWClassicFit.Entry lOEntry in lOEntries)
            {
                if (lOEntry.IsOver)
                    liOver++;
                else if (lbOverOnly)
                    continue;

                if (lOEntry.Place != lsPlace)
                {
                    lsPlace = lOEntry.Place;
                    Console.WriteLine(lsPlace + " (box " + lOEntry.Box + ")");
                }

                Console.WriteLine(string.Format("  {0,-4} {1,-10} {2,3}/{3,-3} \"{4}\"", lOEntry.IsOver ? "OVER" : "ok", lOEntry.Key, lOEntry.Width, lOEntry.Box,
                    lOEntry.Text));
            }

            Console.WriteLine(lOEntries.Count + " strings measured" + (pOContext.StringsPath != null ? " with " + pOContext.StringsPath : " (English)") + ", "
                + liOver + " over their box");

            return liOver == 0 ? 0 : 6;
        }

        /// <summary>The player's STRINGS.PAK, read afresh: the English, whatever --strings says.</summary>
        private static UWStrings fEnglishStrings(DumpContext pOContext)
        {
            return new UWStrings(File.ReadAllBytes(Path.Combine(pOContext.DataPath, "STRINGS.PAK")));
        }

        private static int fCountDiffering(UWStrings pOLeft, UWStrings pORight)
        {
            int liDiffering = 0;

            foreach (KeyValuePair<int, UWStringBlock> lOPair in pOLeft.Blocks)
            {
                List<string> lsLeft = lOPair.Value.Strings;
                List<string> lsRight = pORight.Blocks[lOPair.Key].Strings;

                for (int liIndex = 0; liIndex < lsLeft.Count; liIndex++)
                {
                    if (liIndex >= lsRight.Count || lsLeft[liIndex] != lsRight[liIndex])
                        liDiffering++;
                }
            }

            return liDiffering;
        }

        /// <summary>A language code as the game takes one (UWLanguage): letters, digits, - and _.</summary>
        private static bool fIsLanguageCode(string psCode)
        {
            if (string.IsNullOrEmpty(psCode))
                return false;

            foreach (char lcChar in psCode)
            {
                if (!char.IsLetterOrDigit(lcChar) && lcChar != '-' && lcChar != '_')
                    return false;
            }

            return true;
        }
    }
}
