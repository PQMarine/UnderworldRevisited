using System;
using System.Diagnostics;
using System.IO;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Reads a complete Ultima Underworld 1 installation once and reports what came through.
    ///
    /// It answers two questions. For someone trying the port: does my copy of the game work
    /// with it? For the project itself: does everything still load after a change to the data
    /// layer - all nine levels, the strings, every texture, the 3D models from UW.EXE, all
    /// cutscenes and the sound. The check only reads, it changes nothing and needs no engine.
    ///
    /// Exit code 0 means everything passed, 1 means at least one check failed, 2 that the
    /// data was not found at all or the command line was wrong, 3 that no group matched the
    /// filter.
    ///
    /// The checks live in Tools/UWCheckLib (SelfChecks.BuildCatalog, the options in
    /// CheckOptions); this is their runner.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] psArgs)
        {
            CheckRunner lORunner = new CheckRunner();
            CheckOptions lOOptions = CheckOptions.Parse(psArgs, lORunner);

            if (lOOptions.Help)
            {
                fPrintUsage();

                return 0;
            }

            if (lOOptions.Error != null)
            {
                Console.Error.WriteLine(lOOptions.Error);

                return 2;
            }

            if (lOOptions.Root != null)
            {
                if (!Directory.Exists(Path.Combine(lOOptions.Root, "Assets")))
                {
                    Console.Error.WriteLine("--root " + lOOptions.Root + " is no project root (no Assets folder in it)");

                    return 2;
                }

                CheckContext.RootOverride = Path.GetFullPath(lOOptions.Root);
            }

            CheckCatalog lOCatalog = SelfChecks.BuildCatalog();

            if (lOOptions.List)
            {
                foreach (CheckEntry lOEntry in lOCatalog.Entries)
                {
                    if (lORunner.Matches(lOEntry))
                        Console.WriteLine(string.Format("  {0,-26} {1}{2}", lOEntry.Name,
                            lOEntry.NeedsData ? "data  " : "      ", string.Join(" ", lOEntry.Tags)));
                }

                return 0;
            }

            string lsDataPath = null;

            if (lOOptions.DataArgument != null)
            {
                lsDataPath = DataPath.Resolve(lOOptions.DataArgument, out string lsError);

                if (lsDataPath == null)
                {
                    Console.Error.WriteLine(lsError);

                    return 2;
                }

                Console.WriteLine("Data folder: " + lsDataPath);
                Console.WriteLine();
            }
            else if (!lORunner.NoData)
            {
                fPrintUsage();

                return 2;
            }

            Stopwatch lOTotal = Stopwatch.StartNew();
            CheckContext lOContext = new CheckContext(lsDataPath) { SavesPath = lOOptions.Saves, Record = lOOptions.Record };

            lORunner.Run(lOCatalog, lOContext);
            lOTotal.Stop();
            lORunner.PrintSummary(lOTotal.ElapsedMilliseconds);

            if (lOOptions.JUnitPath != null)
            {
                JUnitReport.Write(lOOptions.JUnitPath, "uwselfcheck", lORunner.Results, lOTotal.ElapsedMilliseconds);
                Console.WriteLine("JUnit report: " + Path.GetFullPath(lOOptions.JUnitPath));
            }

            if (lORunner.Ran == 0)
            {
                Console.WriteLine("No group matched the filter.");

                return 3;
            }

            return Check.Failures == 0 ? 0 : 1;
        }

        private static void fPrintUsage()
        {
            Console.WriteLine("uwselfcheck - reads a complete installation and reports what came through.");
            Console.WriteLine();
            CheckOptions.PrintUsage("uwselfcheck");
        }
    }
}
