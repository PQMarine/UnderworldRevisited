using System.IO;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE TEMPLATE OF THE PORT'S OWN TEXTS (UWTextTemplate, translation wave 6, 2026-10-26): what the
    /// editor menu and `uwdump text-template` write. The English template reads back as the English
    /// with no line skipped; a shipped translation brought up to date comes back byte for byte (it is in
    /// the table's order and complete); a made-up translation of a few lines - one out of order, one
    /// with a comment above it, one whose id is gone, one the game skips - keeps its lines as written,
    /// adds the missing ids as comments with their English, and lists and keeps the obsolete one.
    /// Data-free; the shipped file only with a source tree.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckTextTemplate()
        {
            Check.Group("Text template (UWTextTemplate - uwdump text-template, translation wave 6)");

            string lsEnglish = UWTextTemplate.English();
            UWTextLanguageFile lOBack = UWTextLanguageFile.Parse(lsEnglish);
            int liDiffering = 0;

            foreach (string lsId in UWText.TextIds)
            {
                if (!lOBack.Texts.TryGetValue(lsId, out string lsText) || lsText != UWText.English(lsId))
                    liDiffering++;
            }

            fExpectBool("template: starts with the header line", lsEnglish.StartsWith(UWTextTemplate.Header, System.StringComparison.Ordinal), true);
            Check.Str("template: every line reads", string.Join("; ", lOBack.Problems), string.Empty);
            fExpectInt("template: every id with its English (" + UWText.TextIds.Count + ")", lOBack.Texts.Count - liDiffering, UWText.TextIds.Count);

            // The template updated from itself is itself.
            UWTextTemplate.Result lOSelf = UWTextTemplate.Update(lsEnglish);

            fExpectBool("update: the English template comes back unchanged", lOSelf.Text == lsEnglish && lOSelf.Added.Count == 0
                && lOSelf.Obsolete.Count == 0 && lOSelf.Problems.Count == 0, true);

            // A made-up translation (every word invented).
            string lsFirst = UWText.TextIds[0];
            string lsSecond = UWText.TextIds[1];
            string lsLast = UWText.TextIds[UWText.TextIds.Count - 1];
            string lsMadeUp = "# xq, a made-up language\n\n"
                + lsSecond + "\tZweto\\nmurk\n"
                + "# the first one, moved\n" + lsFirst + "\tAinsto\n"
                + "gone.away.id\tFurro\n"
                + "grammar.look.you_see\tVu zei {7}.\n"
                + "# the end\n";
            UWTextTemplate.Result lOUpdate = UWTextTemplate.Update(lsMadeUp);
            string[] lsLines = lOUpdate.Text.Split('\n');

            fExpectInt("update: lines kept", lOUpdate.Kept, 3);
            fExpectInt("update: ids added as comments", lOUpdate.Added.Count, UWText.TextIds.Count - 3);
            Check.Str("update: the obsolete id", string.Join(",", lOUpdate.Obsolete), "gone.away.id");
            Check.Str("update: the top first, then the moved line with its comment, then the second as written",
                lsLines[0] + "|" + lsLines[1] + "|" + lsLines[2] + "|" + lsLines[3] + "|" + lsLines[4],
                "# xq, a made-up language||# the first one, moved|" + lsFirst + "\tAinsto|" + lsSecond + "\tZweto\\nmurk");
            fExpectBool("update: a missing id as a comment with its English", lOUpdate.Text.IndexOf("\n# " + lsLast + "\t"
                + UWTextLanguageFile.Escape(UWText.English(lsLast)) + "\n", System.StringComparison.Ordinal) >= 0, true);
            fExpectBool("update: the obsolete line kept at the end, then the closing comment",
                lOUpdate.Text.EndsWith("# obsolete: gone.away.id\tFurro\n# the end\n", System.StringComparison.Ordinal), true);
            fExpectBool("update: a line the game skips is kept as written and named",
                lOUpdate.Text.IndexOf("\ngrammar.look.you_see\tVu zei {7}.\n", System.StringComparison.Ordinal) >= 0
                && lOUpdate.Problems.Count == 1 && lOUpdate.Problems[0].IndexOf("grammar.look.you_see", System.StringComparison.Ordinal) >= 0, true);

            UWTextLanguageFile lOUpdated = UWTextLanguageFile.Parse(lOUpdate.Text);

            fExpectBool("update: the result reads - the kept texts, the rest English", lOUpdated.Texts.Count == 2
                && lOUpdated.Texts[lsFirst] == "Ainsto" && lOUpdated.Texts[lsSecond] == "Zweto\nmurk", true);

            // The shipped translations come back byte for byte.
            string lsRoot = CheckContext.ProjectRoot;
            string lsFolder = lsRoot == null ? null : Path.Combine(lsRoot, "Languages");

            if (lsFolder == null || !Directory.Exists(lsFolder))
            {
                Check.Pass("update: the shipped translations", "skipped - no source tree");

                return;
            }

            foreach (string lsFile in Directory.GetFiles(lsFolder, "*.tsv"))
            {
                if (lsFile.EndsWith(UWGameTextPack.FileSuffix, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string lsContent = File.ReadAllText(lsFile, Encoding.UTF8).TrimStart('﻿').Replace("\r\n", "\n");
                UWTextTemplate.Result lOShipped = UWTextTemplate.Update(lsContent);

                fExpectBool(Path.GetFileName(lsFile) + ": brought up to date, the same bytes (" + lOShipped.Kept + " kept)",
                    lOShipped.Text == lsContent && lOShipped.Added.Count == 0 && lOShipped.Obsolete.Count == 0, true);
            }
        }
    }
}
