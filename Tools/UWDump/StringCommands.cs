using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>The string commands: one block, a search over all, and the lossless export.</summary>
    public static partial class Program
    {
        private static int fPrintStrings(DumpContext pOContext, List<string> pOArgs)
        {
            if (pOArgs.Count < 1 || !fParse(pOArgs[0], out int liBlock))
            {
                Console.Error.WriteLine("Which block? For example: uwdump strings <path> 1");

                return 1;
            }

            if (!pOContext.Strings.Blocks.TryGetValue(liBlock, out UWStringBlock lOBlock) || lOBlock.Strings == null)
            {
                Console.Error.WriteLine("Block " + liBlock + " does not exist.");

                return 4;
            }

            int liFrom = fArgument(pOArgs, 1, 0);
            int liCount = fArgument(pOArgs, 2, lOBlock.Strings.Count);

            for (int liAt = Math.Max(0, liFrom); liAt < lOBlock.Strings.Count && liAt < liFrom + liCount; liAt++)
                Console.WriteLine(string.Format("[{0}:{1}] {2}", liBlock, liAt, fOneLine(lOBlock.Strings[liAt])));

            return 0;
        }

        private static int fFindString(DumpContext pOContext, List<string> pOArgs)
        {
            if (pOArgs.Count < 1)
            {
                Console.Error.WriteLine("What should be searched for? For example: uwdump find <path> lockpick");

                return 1;
            }

            string lsNeedle = string.Join(" ", pOArgs);
            int liHits = 0;

            foreach (KeyValuePair<int, UWStringBlock> lOPair in pOContext.Strings.Blocks)
            {
                List<string> lOStrings = lOPair.Value == null ? null : lOPair.Value.Strings;

                for (int liAt = 0; lOStrings != null && liAt < lOStrings.Count; liAt++)
                {
                    if (lOStrings[liAt] == null
                        || lOStrings[liAt].IndexOf(lsNeedle, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    Console.WriteLine(string.Format("[{0}:{1}] {2}", lOPair.Key, liAt, fOneLine(lOStrings[liAt])));
                    liHits++;
                }
            }

            Console.WriteLine(liHits + " hits.");

            return 0;
        }

        // ------------------------------------------------------------------ export

        /// <summary>
        /// EVERY STRING of STRINGS.PAK, one line each, LOSSLESS: "BBBB:NNN = text" with the block
        /// in hex and the index as our parsed lists count it (the project's usual +1 against the
        /// game's numbering), UTF-8 - planned once as the translation overlay's format (Docs/MOD-SUPPORT-PLAN.md
        /// 3.2); the overlay became the fork-compatible TSV of strings-template (UWGameTextPack,
        /// 2026-10-26), this stays the lossless dump. Line breaks, backslashes, other control characters and
        /// a space at either end are escaped, so every line stands alone, survives an editor that
        /// trims lines, and reads back to exactly the original text; the command reads its own
        /// output back and says so. Replaces the editor's string exporter, which dropped the
        /// carriage returns.
        ///
        /// The output is the original game's text: it goes to a file of the user's choosing and
        /// is never committed.
        /// </summary>
        private static int fExportStrings(DumpContext pOContext, List<string> pOArgs)
        {
            string lsOut = fTakeOption(pOArgs, "--out");

            List<int> lOBlocks = new List<int>(pOContext.Data.Strings.Blocks.Keys);
            lOBlocks.Sort();

            StringBuilder lOText = new StringBuilder();
            int liTotal = 0;

            foreach (int liBlock in lOBlocks)
                liTotal += fStrings(pOContext, liBlock).Count;

            lOText.Append("# Ultima Underworld strings (STRINGS.PAK), written by uwdump strings-export.\n");
            lOText.Append("# One line per string: BBBB:NNN = text - the block in hex, the index as the port counts it.\n");
            lOText.Append("# Escapes: \\\\ backslash, \\n line feed, \\r carriage return, \\t tab, \\xHH any other\n");
            lOText.Append("# control character and a space at the start or end. Lines starting with # are comments.\n");
            lOText.Append("#\n");
            lOText.Append(string.Format(CultureInfo.InvariantCulture, "# {0} blocks, {1} strings in total:\n", lOBlocks.Count, liTotal));

            foreach (int liBlock in lOBlocks)
                lOText.Append(string.Format(CultureInfo.InvariantCulture, "#   block {0} (0x{0:X4}): {1} strings\n",
                    liBlock, fStrings(pOContext, liBlock).Count));

            foreach (int liBlock in lOBlocks)
            {
                List<string> lOStrings = fStrings(pOContext, liBlock);

                lOText.Append('\n');
                lOText.Append(string.Format(CultureInfo.InvariantCulture, "# block {0} (0x{0:X4}), {1} strings\n",
                    liBlock, lOStrings.Count));

                for (int liAt = 0; liAt < lOStrings.Count; liAt++)
                    lOText.Append(fExportKey(liBlock, liAt)).Append(" = ").Append(fEscape(lOStrings[liAt])).Append('\n');
            }

            string lsText = lOText.ToString();

            // Proof that nothing was lost: read the text back and compare every string.
            Dictionary<string, string> lORead = fReadExport(lsText);
            int liDiffering = 0;

            foreach (int liBlock in lOBlocks)
            {
                List<string> lOStrings = fStrings(pOContext, liBlock);

                for (int liAt = 0; liAt < lOStrings.Count; liAt++)
                {
                    if (!lORead.TryGetValue(fExportKey(liBlock, liAt), out string lsBack) || lsBack != (lOStrings[liAt] ?? string.Empty))
                        liDiffering++;
                }
            }

            byte[] lyText = new UTF8Encoding(false).GetBytes(lsText);

            if (lsOut != null)
            {
                File.WriteAllBytes(lsOut, lyText);
            }
            else
            {
                using (Stream lOStdout = Console.OpenStandardOutput())
                    lOStdout.Write(lyText, 0, lyText.Length);
            }

            Console.Error.WriteLine(string.Format("{0} strings from {1} blocks{2}; read back: {3}", liTotal, lOBlocks.Count,
                lsOut != null ? " written to " + lsOut : string.Empty,
                liDiffering == 0 && lORead.Count == liTotal ? "every string identical" : liDiffering + " differ"));

            return liDiffering == 0 && lORead.Count == liTotal ? 0 : 5;
        }

        private static List<string> fStrings(DumpContext pOContext, int piBlock)
        {
            UWStringBlock lOBlock = pOContext.Data.Strings.Blocks[piBlock];

            return lOBlock == null || lOBlock.Strings == null ? new List<string>() : lOBlock.Strings;
        }

        private static string fExportKey(int piBlock, int piIndex)
        {
            return piBlock.ToString("X4", CultureInfo.InvariantCulture) + ":" + piIndex.ToString("D3", CultureInfo.InvariantCulture);
        }

        private static string fEscape(string psText)
        {
            if (string.IsNullOrEmpty(psText))
                return string.Empty;

            StringBuilder lOOut = new StringBuilder(psText.Length + 8);

            for (int liAt = 0; liAt < psText.Length; liAt++)
            {
                char lcChar = psText[liAt];

                if (lcChar == '\\')
                    lOOut.Append("\\\\");
                else if (lcChar == '\n')
                    lOOut.Append("\\n");
                else if (lcChar == '\r')
                    lOOut.Append("\\r");
                else if (lcChar == '\t')
                    lOOut.Append("\\t");
                else if (lcChar < 0x20 || lcChar == 0x7F || (lcChar == ' ' && (liAt == 0 || liAt == psText.Length - 1)))
                    lOOut.Append("\\x").Append(((int)lcChar).ToString("X2", CultureInfo.InvariantCulture));
                else
                    lOOut.Append(lcChar);
            }

            return lOOut.ToString();
        }

        /// <summary>Reads an export back into key and text; it proves the export lossless.</summary>
        private static Dictionary<string, string> fReadExport(string psText)
        {
            Dictionary<string, string> lORead = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string lsRawLine in psText.Split('\n'))
            {
                string lsLine = lsRawLine.TrimEnd('\r');

                if (lsLine.Length == 0 || lsLine[0] == '#')
                    continue;

                int liEquals = lsLine.IndexOf(" =", StringComparison.Ordinal);

                if (liEquals < 0)
                    continue;

                string lsValue = liEquals + 3 <= lsLine.Length ? lsLine.Substring(liEquals + 3) : string.Empty;

                lORead[lsLine.Substring(0, liEquals)] = fUnescape(lsValue);
            }

            return lORead;
        }

        private static string fUnescape(string psText)
        {
            StringBuilder lOOut = new StringBuilder(psText.Length);

            for (int liAt = 0; liAt < psText.Length; liAt++)
            {
                char lcChar = psText[liAt];

                if (lcChar != '\\' || liAt + 1 >= psText.Length)
                {
                    lOOut.Append(lcChar);

                    continue;
                }

                char lcNext = psText[++liAt];

                if (lcNext == 'n')
                    lOOut.Append('\n');
                else if (lcNext == 'r')
                    lOOut.Append('\r');
                else if (lcNext == 't')
                    lOOut.Append('\t');
                else if (lcNext == 'x' && liAt + 2 < psText.Length
                    && int.TryParse(psText.Substring(liAt + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int liCode))
                {
                    lOOut.Append((char)liCode);
                    liAt += 2;
                }
                else
                {
                    lOOut.Append(lcNext);
                }
            }

            return lOOut.ToString();
        }
    }
}
