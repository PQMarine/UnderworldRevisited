using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Translation step 3D (2026-10-26): every language file of the port's own texts the repository
    /// ships (Languages/&lt;code&gt;.tsv beside the game; the game-text packs, &lt;code&gt;.strings.tsv, are
    /// never in the repository and are skipped). Each file reads without a skipped line - every id
    /// known, the same {n} placeholders as the English, no id twice - and holds every id of the
    /// table; its texts keep the blanks the English has at its ends (the message lines join pieces
    /// around them, and an editor that strips trailing blanks would glue words together), and use
    /// only the characters the classic fonts draw: printable ASCII and the letters
    /// UWFontGlyphBuilder adds. Since translation wave 4 also: every select and plural reads with the
    /// language's own CLDR categories (no warning either), the generic grammar's header reads
    /// (UWGrammarGeneric.CheckHeader), and the frames that leave an argument to their game pieces are
    /// the ones expected for the file (msLeftOut: its pack's pieces must name it). Data-free; skipped
    /// without a source tree.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckLanguageFiles()
        {
            Check.Group("Language files of the port's own texts (Languages/*.tsv - translation step 3D)");

            string lsRoot = CheckContext.ProjectRoot;

            if (lsRoot == null)
            {
                Check.Pass("language files", "skipped - no source tree");

                return;
            }

            string lsFolder = Path.Combine(lsRoot, "Languages");
            List<string> lsFiles = Directory.Exists(lsFolder)
                ? Directory.GetFiles(lsFolder, "*.tsv")
                    .Where(psFile => !psFile.EndsWith(UWGameTextPack.FileSuffix, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(psFile => psFile, StringComparer.Ordinal).ToList()
                : new List<string>();

            Check.Str("languages: game-text packs kept out of the repository's Languages folder",
                Directory.Exists(lsFolder)
                    ? string.Join(", ", Directory.GetFiles(lsFolder, "*" + UWGameTextPack.FileSuffix).Select(Path.GetFileName))
                    : string.Empty, string.Empty);

            if (lsFiles.Count == 0)
            {
                Check.Pass("languages: files", "none");

                return;
            }

            foreach (string lsFile in lsFiles)
            {
                string lsName = Path.GetFileName(lsFile);
                UWTextLanguageFile lOFile = UWTextLanguageFile.Parse(File.ReadAllText(lsFile, Encoding.UTF8));
                List<string> lsMissing = UWText.TextIds.Where(psId => !lOFile.Texts.ContainsKey(psId)).ToList();
                StringBuilder lOEnds = new StringBuilder();
                StringBuilder lOCharacters = new StringBuilder();

                // A language with a font of its own (grammar.rule.font, translation wave 6) draws the rest
                // with it.
                bool lbOwnFont = lOFile.Texts.TryGetValue(UWLanguageFont.FontRuleId, out string lsFontRule)
                    && UWLanguageFont.Parse(lsFontRule, null).Count > 0;

                foreach (KeyValuePair<string, string> lOPair in lOFile.Texts)
                {
                    string lsEnglish = UWText.English(lOPair.Key) ?? string.Empty;

                    if (fTrailingBlanks(lOPair.Value) != fTrailingBlanks(lsEnglish))
                        lOEnds.Append(lOPair.Key).Append("; ");

                    foreach (char lcChar in lOPair.Value)
                    {
                        if ((lcChar < ' ' && lcChar != '\n') || (lcChar > '~' && !lbOwnFont && Array.IndexOf(UWFontGlyphBuilder.Characters, lcChar) < 0))
                        {
                            lOCharacters.Append(lOPair.Key).Append(" U+").Append(((int)lcChar).ToString("X4")).Append("; ");

                            break;
                        }
                    }
                }

                Check.Str(lsName + ": every line read (known id, the English placeholders, once)", string.Join("; ", lOFile.Problems), string.Empty);
                Check.Str(lsName + ": every id of the table there (" + lOFile.Texts.Count + " of " + UWText.TextIds.Count + ", "
                    + lsMissing.Count + " missing)", string.Join(", ", lsMissing.Take(20)), string.Empty);
                Check.Str(lsName + ": the trailing blanks of the English kept", lOEnds.ToString(), string.Empty);
                Check.Str(lsName + (lbOwnFont ? ": no control codes (its own font draws the rest)" : ": only characters the classic fonts draw"),
                    lOCharacters.ToString(), string.Empty);

                // Translation wave 4: select and plural read with the language's own categories, the
                // generic grammar's header reads, and which frames leave an argument to their game pieces.
                string lsCode = Path.GetFileNameWithoutExtension(lsFile);
                string lsPluralCode = lOFile.Texts.TryGetValue("grammar.rule.plural", out string lsRule) && lsRule.Trim() != UWText.AutoRule ? lsRule.Trim() : lsCode;
                StringBuilder lOFormat = new StringBuilder();
                List<string> lsLeft = new List<string>();

                foreach (string lsId in UWText.TextIds)
                {
                    if (!lOFile.Texts.TryGetValue(lsId, out string lsText))
                        continue;

                    foreach (UWMessageFormat.Problem lOProblem in UWMessageFormat.Check(lsText, -1, lsPluralCode))
                        lOFormat.Append(lsId).Append(": ").Append(lOProblem).Append("; ");

                    string lsLeftOut = UWTextLanguageFile.LeftOut(lsId, lsText);

                    if (lsLeftOut.Length > 0)
                        lsLeft.Add(lsId + " {" + lsLeftOut + "}");
                }

                Check.Str(lsName + ": select and plural read, with " + lsPluralCode + "'s categories", lOFormat.ToString(), string.Empty);
                Check.Str(lsName + ": the generic grammar's header reads", string.Join("; ",
                    UWGrammarGeneric.CheckHeader(psId => lOFile.Texts.TryGetValue(psId, out string lsValue) ? lsValue : null)), string.Empty);

                if (msLeftOut.TryGetValue(lsName, out string lsWanted))
                    Check.Str(lsName + ": the frames that leave an argument to their game pieces", string.Join(", ", lsLeft), lsWanted);
                else
                    Check.Pass(lsName + ": the frames that leave an argument to their game pieces", lsLeft.Count == 0 ? "none" : string.Join(", ", lsLeft));
            }
        }

        /// <summary>The frames each shipped language file leaves an argument of to its game pieces
        /// (UWTextFragments): its pack's pieces must name it, so a change here is on purpose.</summary>
        private static readonly Dictionary<string, string> msLeftOut = new Dictionary<string, string>
        {
            { "de.tsv", "msg.repair.result {1}, msg.shrine.advanced_greatly {1}, msg.shrine.advanced {1}" }
        };

        private static int fTrailingBlanks(string psText)
        {
            return psText.Length - psText.TrimEnd(' ').Length;
        }
    }
}
