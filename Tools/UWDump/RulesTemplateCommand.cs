using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>The rule set's template and the constant inventory - neither needs game data, so both
    /// skip the load.</summary>
    public static partial class Program
    {
        /// <summary>
        /// THE RULE SET'S TEMPLATE (cleanup phase 10, 2026-10-25): every key of UWRules at the
        /// original's value, each with its one-line meaning as a comment above it - a valid
        /// rules.json as it stands (UWRulesJson reads the comments), generated from the defaults so
        /// it cannot drift from the code. A filter keeps the keys that contain it
        /// (rules-template magic.).
        /// </summary>
        private static int fPrintRulesTemplate(DumpContext pOContext, List<string> pOArgs)
        {
            string lsFilter = pOArgs.Count > 0 ? pOArgs[0] : null;
            string lsTemplate = UWRulesJson.Write(UWRules.CreateOriginal(), true);

            if (lsFilter != null)
            {
                // The same file with only the matching keys (each with its comment line above it); the
                // last kept line loses its comma.
                string[] lsLines = lsTemplate.Split('\n');
                List<string> lsKept = new List<string>();

                for (int liAt = 0; liAt < lsLines.Length; liAt++)
                {
                    string lsLine = lsLines[liAt];

                    if (lsLine.StartsWith("  \"", StringComparison.Ordinal))
                    {
                        if (lsLine.IndexOf(lsFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            lsKept.Add(lsLines[liAt - 1]);
                            lsKept.Add(lsLine.TrimEnd(','));
                        }

                        continue;
                    }

                    if (!lsLine.StartsWith("  //", StringComparison.Ordinal))
                        lsKept.Add(lsLine);
                }

                for (int liAt = 0; liAt < lsKept.Count; liAt++)
                {
                    if (lsKept[liAt].StartsWith("  \"", StringComparison.Ordinal) && liAt + 2 < lsKept.Count
                        && lsKept[liAt + 2].StartsWith("  \"", StringComparison.Ordinal))
                        lsKept[liAt] += ",";
                }

                lsTemplate = string.Join("\n", lsKept);
            }

            fWriteRaw(lsTemplate);

            return 0;
        }

        /// <summary>
        /// Every constant and table of the engine-free layer as "Type.Name = value", sorted - the
        /// inventory the rule set is cut from domain by domain (Docs/CLEANUP-PLAN.md section 6): about
        /// 300 of them are gameplay numbers that become rules, the rest are file-format facts that
        /// stay. It is the self-check's constant pin (Pins/rule-constants.txt) byte for byte. Until
        /// cleanup phase 10 this was rules-template. A filter keeps the lines whose name contains it
        /// (constants UWSkillCheck).
        /// </summary>
        private static int fPrintConstants(DumpContext pOContext, List<string> pOArgs)
        {
            string lsFilter = pOArgs.Count > 0 ? pOArgs[0] : null;
            SortedDictionary<string, string> lOConstants = UWConstantScan.Scan(out int liSkipped);
            StringBuilder lOText = new StringBuilder();

            foreach (KeyValuePair<string, string> lOEntry in lOConstants)
            {
                if (lsFilter != null && lOEntry.Key.IndexOf(lsFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                lOText.Append(lOEntry.Key).Append(" = ").Append(lOEntry.Value).Append('\n');
            }

            fWriteRaw(lOText.ToString());

            if (liSkipped > 0)
                Console.Error.WriteLine(liSkipped + " type(s) could not be reflected.");

            return liSkipped == 0 ? 0 : 5;
        }

        /// <summary>Raw bytes, so the text is the pin's to the byte (no CRLF, no code page).</summary>
        private static void fWriteRaw(string psText)
        {
            byte[] lyText = new UTF8Encoding(false).GetBytes(psText);

            using (Stream lOStdout = Console.OpenStandardOutput())
                lOStdout.Write(lyText, 0, lyText.Length);
        }
    }
}
