using System;
using System.Collections.Generic;
using System.Globalization;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The command line of a check runner (section 9.1 of the cleanup plan): the filters go into
    /// the runner, --quiet into Check, the rest is kept here for the runner's Main. A runner over
    /// another source of rules (a mod validator) takes the same options.
    /// </summary>
    public sealed class CheckOptions
    {
        /// <summary>The path argument: a DATA folder, a GOG installation folder or game.gog.</summary>
        public string DataArgument;

        /// <summary>--saves: a folder of save games for the round-trip check.</summary>
        public string Saves;

        /// <summary>--root: the project root for the checks of the source tree and the pins.</summary>
        public string Root;

        /// <summary>--junit: where to write the JUnit XML report.</summary>
        public string JUnitPath;

        public bool List;

        public bool Record;

        /// <summary>--sweep n: the motion sweep's runs a level (MotionSweepChecks).</summary>
        public int SweepRuns;

        public bool Help;

        /// <summary>What was wrong with the command line, or null.</summary>
        public string Error;

        public static CheckOptions Parse(string[] psArgs, CheckRunner pORunner)
        {
            CheckOptions lOOptions = new CheckOptions();

            for (int liAt = 0; liAt < psArgs.Length && lOOptions.Error == null; liAt++)
            {
                string lsArg = psArgs[liAt];

                switch (lsArg)
                {
                    case "--only": pORunner.Only.AddRange(fSplit(fValue(psArgs, ref liAt, lOOptions))); break;
                    case "--skip": pORunner.Skip.AddRange(fSplit(fValue(psArgs, ref liAt, lOOptions))); break;
                    case "--tag": pORunner.Tags.AddRange(fSplit(fValue(psArgs, ref liAt, lOOptions))); break;
                    case "--seed":
                        string lsSeed = fValue(psArgs, ref liAt, lOOptions);

                        if (lsSeed != null && !int.TryParse(lsSeed, NumberStyles.Integer, CultureInfo.InvariantCulture, out pORunner.SeedOffset))
                            lOOptions.Error = "--seed needs a whole number, not " + lsSeed;

                        break;
                    case "--saves": lOOptions.Saves = fValue(psArgs, ref liAt, lOOptions); break;
                    case "--root": lOOptions.Root = fValue(psArgs, ref liAt, lOOptions); break;
                    case "--junit": lOOptions.JUnitPath = fValue(psArgs, ref liAt, lOOptions); break;
                    case "--list": lOOptions.List = true; break;
                    case "--quiet": Check.Quiet = true; break;
                    case "--no-data": pORunner.NoData = true; break;
                    case "--times": pORunner.Times = true; break;
                    case "--record": lOOptions.Record = true; break;
                    case "--sweep":
                        string lsRuns = fValue(psArgs, ref liAt, lOOptions);

                        if (lsRuns != null && (!int.TryParse(lsRuns, NumberStyles.Integer, CultureInfo.InvariantCulture, out lOOptions.SweepRuns) || lOOptions.SweepRuns < 1))
                            lOOptions.Error = "--sweep needs a positive whole number, not " + lsRuns;

                        break;
                    case "--help":
                    case "-h":
                        lOOptions.Help = true;
                        break;
                    default:
                        if (lsArg.StartsWith("--", StringComparison.Ordinal))
                            lOOptions.Error = "Unknown option " + lsArg;
                        else
                            lOOptions.DataArgument = lsArg;

                        break;
                }
            }

            return lOOptions;
        }

        /// <summary>The options as the usage lists them, under the runner's own first lines.</summary>
        public static void PrintUsage(string psTool)
        {
            Console.WriteLine("  " + psTool + " <path> [options]");
            Console.WriteLine("  " + psTool + " --no-data [options]");
            Console.WriteLine();
            Console.WriteLine("<path> is the DATA folder of an installation, or the GOG installation");
            Console.WriteLine("folder or game.gog itself.");
            Console.WriteLine();
            Console.WriteLine("  --only a,b      run only the groups whose name contains a or b");
            Console.WriteLine("  --skip a,b      skip the groups whose name contains a or b");
            Console.WriteLine("  --tag t,u       run only the groups tagged t or u");
            Console.WriteLine("  --list          print the groups the filters select and stop");
            Console.WriteLine("  --quiet         print only failures and the summary");
            Console.WriteLine("  --no-data       run only the groups that need no game data");
            Console.WriteLine("  --times         print the time every group took");
            Console.WriteLine("  --seed n        add n to every group's random seed");
            Console.WriteLine("  --sweep n       the motion sweep runs n starts a level and prints its report");
            Console.WriteLine("  --saves <dir>   a folder of save games for the round-trip check");
            Console.WriteLine("  --root <dir>    the project root for the source and pin checks (default: the");
            Console.WriteLine("                  folder with Assets above the working directory)");
            Console.WriteLine("  --junit <file>  also write the result as a JUnit XML report, one case per group");
            Console.WriteLine("  --record        the pin checks write Tools/UWSelfCheck/Pins instead of comparing");
            Console.WriteLine();
            Console.WriteLine("Exit code 0: everything passed; 1: a check failed; 2: no data or a wrong");
            Console.WriteLine("command line; 3: no group matched the filter.");
        }

        private static string fValue(string[] psArgs, ref int piAt, CheckOptions pOOptions)
        {
            if (piAt + 1 >= psArgs.Length)
            {
                pOOptions.Error = psArgs[piAt] + " needs a value";

                return null;
            }

            piAt++;

            return psArgs[piAt];
        }

        private static IEnumerable<string> fSplit(string psList)
        {
            if (string.IsNullOrEmpty(psList))
                yield break;

            foreach (string lsPart in psList.Split(','))
                if (lsPart.Trim().Length > 0)
                    yield return lsPart.Trim();
        }
    }
}
