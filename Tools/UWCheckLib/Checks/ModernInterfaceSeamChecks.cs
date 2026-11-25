using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE MODERN INTERFACE NAMES NO GAME (Docs/UW2-PLAN.md, "The modern interface for UW2: one interface, two
    /// hosts", rule 1; stage U3 part D, 2026-11-23): the modern windows ask the running game through
    /// IUWModernSession and its domain interfaces, so no UWModern* source may name a component of either game.
    /// A scan of the sources, comments left out. Excepted are the two games' own sides: the hosts, the
    /// conversation hosts, the first game's world input, the root's wiring (UWModernUi) and the second game's
    /// stopgap, which goes with stage U4.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>What a window may not name: the first game's components and the second game's.</summary>
        private static readonly string[] msSeamForbidden =
        {
            "UWCharacter", "Interaction", "UWInventory", "UWGameUI", "UWHudRunes", "UWGameActions", "UWPlayerData", "UWItemDrag",
            "UWSaveFlow", "UWGameScreenUw2", "UWWorldViewUw2", "UWVitalsUw2", "UWPlayerRecordUw2", "UWAttackUw2"
        };

        /// <summary>The games' own sides among the UWModern* sources.</summary>
        private static readonly HashSet<string> msSeamGameSide = new HashSet<string>(StringComparer.Ordinal)
        {
            "UWModernUi.cs", "UWModernSessionUw1.cs", "UWModernSessionUw2.cs", "UWModernConversationHostUw1.cs", "UWModernConversationHostUw2.cs",
            "UWModernWorldInput.cs"
        };

        private static void fCheckModernInterfaceSeam()
        {
            Check.Group("Modern interface: the windows name no game (rule 1 of the UW2 plan's modern interface)");

            string lsRoot = CheckContext.ProjectRoot;

            if (lsRoot == null)
            {
                Check.Pass("windows: no game's component named", "skipped - no source tree");

                return;
            }

            string lsFolder = Path.Combine(lsRoot, "Assets", "UWScripts");
            Regex lOToken = new Regex("\\b(" + string.Join("|", msSeamForbidden) + ")\\b");
            StringBuilder lOFound = new StringBuilder();
            int liFiles = 0;

            foreach (string lsFile in Directory.GetFiles(lsFolder, "UWModern*.cs"))
            {
                string lsName = Path.GetFileName(lsFile);

                if (msSeamGameSide.Contains(lsName))
                    continue;

                liFiles++;

                string[] lsLines = File.ReadAllLines(lsFile);

                for (int liLine = 0; liLine < lsLines.Length; liLine++)
                {
                    // Comments may name what a window replaced or stands beside.
                    string lsCode = Regex.Replace(lsLines[liLine], "//.*$", string.Empty);

                    foreach (Match lOMatch in lOToken.Matches(lsCode))
                        lOFound.Append(lsName).Append(':').Append(liLine + 1).Append(' ').Append(lOMatch.Value).Append("; ");
                }
            }

            Check.Bool("windows: sources found", liFiles > 30, true);
            Check.Str("windows: no game's component named", lOFound.ToString(), string.Empty);
        }
    }
}
