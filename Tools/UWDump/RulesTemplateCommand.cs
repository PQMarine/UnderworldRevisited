using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnderworldRevisited.Tools
{
    /// <summary>The rule set's template - needs no game data, so it skips the load.</summary>
    public static partial class Program
    {
        /// <summary>
        /// Every constant and table of the engine-free layer as "Type.Name = value", sorted -
        /// the inventory the rule set (UWRules, Docs/CLEANUP-PLAN.md section 6 and phase 10) is
        /// cut from: about 300 of them are gameplay numbers that become editable, the rest are
        /// file-format facts that stay. Until UWRules exists this list IS the template; it is
        /// the self-check's constant pin (Pins/rule-constants.txt) byte for byte, so it cannot
        /// drift from the code. A filter keeps the lines whose name contains it
        /// (rules-template UWSkillCheck). When UWRules lands, the command writes its defaults
        /// with dotted keys instead.
        /// </summary>
        private static int fPrintRulesTemplate(DumpContext pOContext, List<string> pOArgs)
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

            // Raw bytes, so the text is the pin's to the byte (no CRLF, no code page).
            byte[] lyText = new UTF8Encoding(false).GetBytes(lOText.ToString());

            using (Stream lOStdout = Console.OpenStandardOutput())
                lOStdout.Write(lyText, 0, lyText.Length);

            if (liSkipped > 0)
                Console.Error.WriteLine(liSkipped + " type(s) could not be reflected.");

            return liSkipped == 0 ? 0 : 5;
        }
    }
}
