using System;
using System.IO;
using System.Text;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Writes Docs/RULES-INDEX.md - the index of which place of UW.EXE each of our files
    /// implements. The scan itself is shared with the self-check (UWRuleIndexScan), which runs
    /// it on every pass, so the file on disk cannot quietly go stale.
    ///
    ///   dotnet run --project Tools/UWRuleIndex -- [project root]
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] psArgs)
        {
            string lsRoot = psArgs.Length > 0 ? psArgs[0] : UWRuleIndexScan.FindProjectRoot();

            if (lsRoot == null || !Directory.Exists(Path.Combine(lsRoot, "Assets")))
            {
                Console.WriteLine("uwruleindex [project root] - the folder with Assets and Docs");

                return 1;
            }

            UWRuleIndexScan.Result lOResult = UWRuleIndexScan.Scan(lsRoot);

            if (lOResult == null)
            {
                Console.WriteLine("no citations of the original found - has the comment style changed?");

                return 1;
            }

            string lsPath = UWRuleIndexScan.GetIndexPath(lsRoot);

            Directory.CreateDirectory(Path.GetDirectoryName(lsPath));
            File.WriteAllText(lsPath, lOResult.Markdown, new UTF8Encoding(false));

            Console.WriteLine($"{lOResult.Citations} citations, {lOResult.Places} places of the original, "
                + $"{lOResult.Shared} of them in more than one engine-free file, {lOResult.Suspects.Count} of those "
                + "between rule classes - look at those first");

            foreach (string lsSuspect in lOResult.Suspects)
                Console.WriteLine("  " + lsSuspect);

            Console.WriteLine("written: " + lsPath);

            return 0;
        }
    }
}
