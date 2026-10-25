using System.IO;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// The index of where the original is implemented (Docs/RULES-INDEX.md, built by
        /// UWRuleIndexScan, written by Tools/UWRuleIndex). Two things are asked of it here, so
        /// that neither can rot between releases:
        ///
        ///   - NO PLACE of UW.EXE may be implemented in two rule classes unless the pairing is
        ///     declared in msExpectedPairs with a reason. That is the audit against rules
        ///     written twice, which used to be a hand exercise before the release.
        ///   - THE FILE ON DISK must match the sources, so that nobody reads a stale index.
        ///     Fails with the one command that repairs it.
        ///
        /// Needs no game data, only the source tree; if that is not found - a published build
        /// without sources - the check says so and passes.
        /// </summary>
        private static void fCheckRuleIndex()
        {
            Check.Group("Index of the implemented places");

            string lsRoot = CheckContext.ProjectRoot;

            if (lsRoot == null)
            {
                fPass("rule index", "no source tree here, nothing to check");

                return;
            }

            UWRuleIndexScan.Result lOResult = UWRuleIndexScan.Scan(lsRoot);

            if (lOResult == null)
            {
                fFail("rule index", "no citations of the original found - has the comment style changed?");

                return;
            }

            fPass("rule index", lOResult.Citations + " citations of " + lOResult.Places
                + " places, " + lOResult.Shared + " of them in more than one engine-free file");

            if (lOResult.Suspects.Count == 0)
            {
                fPass("rule index: no place is implemented in two rule classes", "none");
            }
            else
            {
                foreach (string lsSuspect in lOResult.Suspects)
                    fFail("rule index: implemented in two rule classes", lsSuspect);
            }

            string lsPath = UWRuleIndexScan.GetIndexPath(lsRoot);

            if (!File.Exists(lsPath))
            {
                fFail("rule index: the file exists", "missing - run dotnet run --project Tools/UWRuleIndex");

                return;
            }

            if (File.ReadAllText(lsPath).Replace("\r\n", "\n") == lOResult.Markdown.Replace("\r\n", "\n"))
                fPass("rule index: the file matches the sources", "current");
            else
                fFail("rule index: the file matches the sources",
                    "out of date - run dotnet run --project Tools/UWRuleIndex");
        }
    }
}
