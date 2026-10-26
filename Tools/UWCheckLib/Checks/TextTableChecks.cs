using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 8.5 (2026-10-24): the port's own texts (UWText.T and F, the English table
    /// UWTextEnglish, the language files). The table: every id once, well formed, not empty; every
    /// setting row's id there. The code (Assets, read as text): every id it names exists, every
    /// F call passes as many arguments as its English text has placeholders, no T call names a
    /// text with placeholders, and every id of the table is named somewhere. The languages: the
    /// pseudo-language changes every text, keeps its placeholders and tags and makes it at least
    /// 40 % longer; a language file in the pseudo-language for half the ids, with bad lines, loads,
    /// reports exactly the bad lines and falls back to English per id. Data-free.
    /// </summary>
    public static partial class SelfChecks
    {
        private static readonly Regex msTextId = new Regex(@"^[a-z]+(\.[A-Za-z0-9_\-]+)+$");

        /// <summary>The areas of the table (UWTextEnglish); a literal in the code that starts with
        /// one of these and a dot is taken for an id.</summary>
        private static readonly string[] msTextAreas =
            { "setting", "setup", "keys", "pad", "help", "editor", "layout", "menu", "hud", "prompt", "effects", "grid", "grammar", "msg" };

        private static void fCheckTextTable()
        {
            Check.Group("Text table (UWText.T/F, UWTextEnglish, language files - cleanup step 8.5)");

            try
            {
                fCheckTextTableIds();
                fCheckTextTableSettings();
                fCheckTextTableCode();
                fCheckTextPseudo();
                fCheckTextLanguageFile();
            }
            finally
            {
                UWText.UseEnglish();
            }
        }

        private static void fCheckTextTableIds()
        {
            IReadOnlyList<string> lsIds = UWText.TextIds;
            StringBuilder lOProblems = new StringBuilder();

            foreach (string lsId in lsIds)
            {
                if (!msTextId.IsMatch(lsId))
                    lOProblems.Append("malformed id ").Append(lsId).Append("; ");
                else if (!msTextAreas.Contains(lsId.Substring(0, lsId.IndexOf('.'))))
                    lOProblems.Append("id of no area ").Append(lsId).Append("; ");

                if (string.IsNullOrEmpty(UWText.English(lsId)))
                    lOProblems.Append("empty text ").Append(lsId).Append("; ");
            }

            Check.Str("table: ids given twice", string.Join(",", UWText.TextIdsGivenTwice), string.Empty);
            Check.Str("table: every id well formed, in an area, with a text", lOProblems.ToString(), string.Empty);

            string lsAreas = string.Join(", ", lsIds.GroupBy(psId => psId.Substring(0, psId.IndexOf('.')))
                .Select(pOGroup => pOGroup.Key + " " + pOGroup.Count()));

            Check.Pass("table: ids", lsIds.Count + " (" + lsAreas + ")");
        }

        /// <summary>Every id a setting row names (UWUserSettingsTable, step 8.4) is in the table.</summary>
        private static void fCheckTextTableSettings()
        {
            StringBuilder lOMissing = new StringBuilder();
            int liIds = 0;

            foreach (UWSettingDef lODef in UWUserSettingsTable.All)
            {
                List<string> lsIds = new List<string> { lODef.LabelId, lODef.HintId };

                if (lODef.ChoiceIds != null)
                    lsIds.AddRange(lODef.ChoiceIds);

                foreach (string lsId in lsIds)
                {
                    if (lsId == null)
                        continue;

                    liIds++;

                    if (!UWText.HasText(lsId))
                        lOMissing.Append(lsId).Append("; ");
                }
            }

            Check.Str("settings: every row's id has a text (" + liIds + " ids)", lOMissing.ToString(), string.Empty);
            Check.Str("settings: a label reads through the table", UWUserSettingsTable.MusicVolume.Label, "Music volume");
            Check.Str("settings: choices read through the table", string.Join(",", UWUserSettingsTable.SmoothRampTicks.ChoiceLabels),
                "0.6 s,0.3 s,0.15 s,At once");
        }

        // ------------------------------------------------- The code

        private static void fCheckTextTableCode()
        {
            string lsRoot = CheckContext.ProjectRoot;

            if (lsRoot == null)
            {
                Check.Pass("code: ids in the code", "skipped - no source tree");

                return;
            }

            HashSet<string> lOUsed = new HashSet<string>();
            StringBuilder lOMissing = new StringBuilder();
            StringBuilder lOArguments = new StringBuilder();
            StringBuilder lOPlainWithPlaceholders = new StringBuilder();
            int liCalls = 0;
            int liFiles = 0;

            foreach (string lsFile in Directory.GetFiles(Path.Combine(lsRoot, "Assets"), "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(lsFile).StartsWith("UWTextEnglish", StringComparison.Ordinal))
                    continue;

                string lsSource = File.ReadAllText(lsFile);
                string lsName = Path.GetFileName(lsFile);

                if (lsSource.IndexOf("UWText", StringComparison.Ordinal) < 0 && lsSource.IndexOf('"') < 0)
                    continue;

                liFiles++;

                // Every literal that looks like an id of an area: in a call, an array, a field - not
                // in a comment, and not a row's own id (Id = "layout.window", the rows of step 8.4).
                string lsCode = Regex.Replace(lsSource, "//[^\\n]*", string.Empty);

                foreach (Match lOMatch in Regex.Matches(lsCode, "(\\bId\\s*=\\s*)?\"([a-z]+\\.[A-Za-z0-9_.\\-]*[A-Za-z0-9_])\""))
                {
                    if (lOMatch.Groups[1].Success)
                        continue;

                    string lsId = lOMatch.Groups[2].Value;

                    if (!msTextAreas.Contains(lsId.Substring(0, lsId.IndexOf('.'))))
                        continue;

                    lOUsed.Add(lsId);

                    if (!UWText.HasText(lsId))
                        lOMissing.Append(lsName).Append(": ").Append(lsId).Append("; ");
                }

                foreach (Match lOMatch in Regex.Matches(lsSource, "UWText\\.(T|F)\\(\\s*\"([^\"]+)\""))
                {
                    liCalls++;

                    string lsId = lOMatch.Groups[2].Value;
                    string lsEnglish = UWText.English(lsId);

                    if (lsEnglish == null)
                        continue;

                    int liWanted = UWText.PlaceholderCount(lsEnglish);

                    if (lOMatch.Groups[1].Value == "T")
                    {
                        if (liWanted > 0)
                            lOPlainWithPlaceholders.Append(lsName).Append(": ").Append(lsId).Append("; ");

                        continue;
                    }

                    int liGiven = fCountCallArguments(lsSource, lOMatch.Index + lOMatch.Value.IndexOf('(')) - 1;

                    if (liGiven != liWanted)
                        lOArguments.Append(lsName).Append(": ").Append(lsId).Append(" given ").Append(liGiven)
                            .Append(", wants ").Append(liWanted).Append("; ");
                }

                // UWText.Sentence(data, id, ...): a message line's frame (translation step 3A), the
                // data before the id.
                foreach (Match lOMatch in Regex.Matches(lsSource, "UWText\\.Sentence\\(\\s*[^,\"()]+(\\(\\))?\\s*,\\s*\"([^\"]+)\""))
                {
                    liCalls++;

                    string lsId = lOMatch.Groups[2].Value;
                    string lsEnglish = UWText.English(lsId);

                    if (lsEnglish == null)
                        continue;

                    int liGiven = fCountCallArguments(lsSource, lOMatch.Index + lOMatch.Value.IndexOf('(')) - 2;

                    if (liGiven != UWText.PlaceholderCount(lsEnglish))
                        lOArguments.Append(lsName).Append(": ").Append(lsId).Append(" given ").Append(liGiven)
                            .Append(", wants ").Append(UWText.PlaceholderCount(lsEnglish)).Append("; ");
                }
            }

            // The setting rows' ids are built from the keys (UWUserSettingsTable.Rows), not written out.
            foreach (UWSettingDef lODef in UWUserSettingsTable.All)
            {
                lOUsed.Add(lODef.LabelId ?? string.Empty);
                lOUsed.Add(lODef.HintId ?? string.Empty);

                if (lODef.ChoiceIds != null)
                    lOUsed.UnionWith(lODef.ChoiceIds);
            }

            string lsUnused = string.Join(", ", UWText.TextIds.Where(psId => !lOUsed.Contains(psId)));

            Check.Str("code: every id named in " + liFiles + " files exists", lOMissing.ToString(), string.Empty);
            Check.Str("code: every F call (of " + liCalls + " T and F calls) fills its placeholders", lOArguments.ToString(), string.Empty);
            Check.Str("code: no T call names a text with placeholders", lOPlainWithPlaceholders.ToString(), string.Empty);
            Check.Str("code: every id of the table is named", lsUnused, string.Empty);
        }

        /// <summary>The arguments of a call whose "(" stands at the index: top-level commas plus one.
        /// Strings, characters and nested brackets are skipped.</summary>
        private static int fCountCallArguments(string psSource, int piOpen)
        {
            int liDepth = 0;
            int liCommas = 0;

            for (int liAt = piOpen; liAt < psSource.Length; liAt++)
            {
                char lcChar = psSource[liAt];

                if (lcChar == '"' || lcChar == '\'')
                {
                    bool lbVerbatim = lcChar == '"' && liAt > 0 && psSource[liAt - 1] == '@';

                    for (liAt++; liAt < psSource.Length && psSource[liAt] != lcChar; liAt++)
                    {
                        if (psSource[liAt] == '\\' && !lbVerbatim)
                            liAt++;
                    }

                    continue;
                }

                if (lcChar == '(' || lcChar == '[' || lcChar == '{')
                    liDepth++;
                else if (lcChar == ')' || lcChar == ']' || lcChar == '}')
                {
                    liDepth--;

                    if (liDepth == 0)
                        return liCommas + 1;
                }
                else if (lcChar == ',' && liDepth == 1)
                    liCommas++;
            }

            return -1;
        }

        // ------------------------------------------------- Languages

        private static void fCheckTextPseudo()
        {
            UWText.UsePseudo();

            StringBuilder lOProblems = new StringBuilder();
            int liShort = 0;

            foreach (string lsId in UWText.TextIds)
            {
                string lsEnglish = UWText.English(lsId);
                string lsPseudo = UWText.T(lsId);

                if (lsPseudo == lsEnglish || !lsPseudo.StartsWith("[") || !lsPseudo.EndsWith("]"))
                    lOProblems.Append("unchanged ").Append(lsId).Append("; ");

                if (UWText.PlaceholderSet(lsPseudo) != UWText.PlaceholderSet(lsEnglish))
                    lOProblems.Append("placeholders ").Append(lsId).Append("; ");

                if (Regex.Matches(lsPseudo, "<").Count != Regex.Matches(lsEnglish, "<").Count)
                    lOProblems.Append("tags ").Append(lsId).Append("; ");

                if (fVisible(lsPseudo).Length < fVisible(lsEnglish).Length * 1.4)
                    liShort++;

                object[] laArgs = Enumerable.Range(0, UWText.PlaceholderCount(lsEnglish)).Select(piAt => (object)piAt).ToArray();

                if (UWText.F(lsId, laArgs) == lsPseudo && laArgs.Length > 0)
                    lOProblems.Append("F does not fill ").Append(lsId).Append("; ");
            }

            Check.Str("pseudo: every text changed, placeholders and tags kept, F fills", lOProblems.ToString(), string.Empty);
            Check.Int("pseudo: texts less than 40 % longer", liShort, 0);
            Check.Str("pseudo: a short one", UWText.T("setup.done"), "[Doonee]");
            Check.Str("pseudo: placeholders untouched", UWText.F("keys.slot", 3), "[AActiioon baar sloot 3~~]");
            Check.Str("pseudo: a setting row's choices follow", UWUserSettingsTable.SmoothRampTicks.ChoiceLabels[3], "[AAt ooncee]");
            Check.Str("pseudo: the language's name", UWText.LanguageName, UWText.PseudoLanguage);

            UWText.UseEnglish();

            Check.Str("english: back", UWText.T("setup.done") + "|" + UWUserSettingsTable.SmoothRampTicks.ChoiceLabels[3], "Done|At once");
        }

        /// <summary>A text without its placeholders and rich text tags: what a reader sees of it.</summary>
        private static string fVisible(string psText)
        {
            return Regex.Replace(psText, @"\{\d[^}]*\}|</?(b|i|color|size)(=[^>]*)?>", string.Empty);
        }

        /// <summary>A language file: the pseudo texts for every other id, written out and read back,
        /// with a comment, an empty line, a line without a TAB, an unknown id, a text with other
        /// placeholders than the English and an id given twice.</summary>
        private static void fCheckTextLanguageFile()
        {
            List<string> lsIds = UWText.TextIds.Where((psId, piAt) => piAt % 2 == 0 && psId != "keys.slot").ToList();
            string lsTranslated = UWTextLanguageFile.Write(lsIds, psId => UWText.Pseudo(UWText.English(psId)));
            string lsFile = "\uFEFF# a test language\r\n\r\n" + lsTranslated.Replace("\n", "\r\n")
                + "no tab on this line\n"
                + "no.such.id\tx\n"
                + "keys.slot\tSlot without its number\n"
                + "setup.done\tFertig\n"
                + "setup.game.title\tLine one\\nLine two\\tTabbed \\\\ back\n";

            UWTextLanguageFile lOFile = UWTextLanguageFile.Parse(lsFile);

            Check.Int("file: texts read", lOFile.Texts.Count, lsIds.Count + (lsIds.Contains("setup.game.title") ? 0 : 1)
                + (lsIds.Contains("setup.done") ? 0 : 1));
            Check.Int("file: the bad lines reported", lOFile.Problems.Count, 3 + (lsIds.Contains("setup.done") ? 1 : 0)
                + (lsIds.Contains("setup.game.title") ? 1 : 0));

            UWText.UseLanguage("test", lOFile);

            string lsFirst = lsIds.First(psId => psId != "setup.done" && psId != "setup.game.title");
            string lsOther = UWText.TextIds.First(psId => !lsIds.Contains(psId) && psId != "keys.slot");

            Check.Str("file: a translated id", UWText.T(lsFirst), UWText.Pseudo(UWText.English(lsFirst)));
            Check.Str("file: an id the file lacks stays English", UWText.T(lsOther), UWText.English(lsOther));
            Check.Str("file: a text with other placeholders stays English", UWText.F("keys.slot", 4), "Action bar slot 4");
            Check.Str("file: the later of two lines counts", UWText.T("setup.done"), "Fertig");
            Check.Str("file: escapes", UWText.T("setup.game.title"), "Line one\nLine two\tTabbed \\ back");
            Check.Str("file: an unknown id gives itself", UWText.T("no.such.id"), "no.such.id");
            Check.Str("file: the name", UWText.LanguageName, "test");
            Check.Str("file: written and read again", UWTextLanguageFile.Unescape(UWTextLanguageFile.Escape("a\\b\nc\td")), "a\\b\nc\td");

            UWText.UseEnglish();

            Check.Str("english: every id back", string.Join(",", UWText.TextIds.Where(psId => UWText.T(psId) != UWText.English(psId))), string.Empty);
        }
    }
}
