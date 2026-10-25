using System;
using System.Collections.Generic;
using System.Diagnostics;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Runs the catalog: filters the groups, guards the static state around each, seeds the
    /// random source per group so no group depends on what an earlier one drew, catches an
    /// exception so one broken group does not hide the results of the rest, reloads the world
    /// after a group that changed it, times every group and prints the summary.
    /// </summary>
    public sealed class CheckRunner
    {
        /// <summary>What one group came to, for the summary and the --junit report.</summary>
        public sealed class GroupResult
        {
            public CheckEntry Entry;

            public long Milliseconds;

            public int Passes;

            public int Failures;

            /// <summary>Skipped for want of the game data; nothing ran.</summary>
            public bool Skipped;

            /// <summary>The group's FAIL lines, in order.</summary>
            public readonly List<string> FailLines = new List<string>();

            public string Name
            {
                get { return Entry.Name; }
            }
        }

        /// <summary>Only groups whose name contains one of these (case-insensitive); empty means all.</summary>
        public readonly List<string> Only = new List<string>();

        /// <summary>Groups whose name contains one of these are skipped.</summary>
        public readonly List<string> Skip = new List<string>();

        /// <summary>Only groups carrying one of these tags; empty means all.</summary>
        public readonly List<string> Tags = new List<string>();

        /// <summary>Run only groups that need no data; the data folder may then be missing.</summary>
        public bool NoData;

        /// <summary>Print the per-group timing table at the end.</summary>
        public bool Times;

        /// <summary>
        /// Added to every group's seed. The default seeds are fixed, so a run repeats; a different
        /// offset flushes out an expectation that only holds for one seed.
        /// </summary>
        public int SeedOffset;

        private readonly List<GroupResult> mOResults = new List<GroupResult>();
        private int miRan;
        private int miSkipped;

        public int Ran
        {
            get { return miRan; }
        }

        /// <summary>Every group that matched the filter, run or skipped, in the order of the run.</summary>
        public IReadOnlyList<GroupResult> Results
        {
            get { return mOResults; }
        }

        public bool Matches(CheckEntry pOEntry)
        {
            if (Only.Count > 0 && !fContainsAny(pOEntry.Name, Only))
                return false;

            if (Skip.Count > 0 && fContainsAny(pOEntry.Name, Skip))
                return false;

            if (Tags.Count > 0)
            {
                bool lbTagged = false;

                foreach (string lsTag in Tags)
                    lbTagged |= pOEntry.HasTag(lsTag);

                if (!lbTagged)
                    return false;
            }

            return true;
        }

        public void Run(CheckCatalog pOCatalog, CheckContext pOContext)
        {
            foreach (CheckEntry lOEntry in pOCatalog.Entries)
            {
                if (!Matches(lOEntry))
                    continue;

                if (lOEntry.NeedsData && (NoData || !pOContext.HasData))
                {
                    miSkipped++;
                    mOResults.Add(new GroupResult { Entry = lOEntry, Skipped = true });

                    if (!Check.Quiet)
                        Console.WriteLine(string.Format("  SKIP {0,-10} needs the game data", lOEntry.Name));

                    continue;
                }

                fRunOne(lOEntry, pOContext);
            }
        }

        private void fRunOne(CheckEntry pOEntry, CheckContext pOContext)
        {
            int liFailuresBefore = Check.Failures;
            int liPassesBefore = Check.Passes;
            int liFailLinesBefore = Check.FailLines.Count;
            StateGuard lOGuard = new StateGuard();
            Stopwatch lOWatch = Stopwatch.StartNew();

            // A fixed seed per group, derived from its name, so a group's rolls do not depend
            // on which groups ran before it. A group that needs a particular seed sets its own.
            UWRandom.UseSeed(fSeedOf(pOEntry.Name) + SeedOffset);

            try
            {
                pOEntry.Run(pOContext);
            }
            catch (Exception lOException)
            {
                Check.Fail(pOEntry.Name, "threw " + lOException.GetType().Name + ": " + lOException.Message);
            }
            finally
            {
                lOWatch.Stop();
                lOGuard.Restore();

                if (pOEntry.MutatesData)
                    pOContext.FreshData();
            }

            miRan++;

            GroupResult lOResult = new GroupResult
            {
                Entry = pOEntry,
                Milliseconds = lOWatch.ElapsedMilliseconds,
                Passes = Check.Passes - liPassesBefore,
                Failures = Check.Failures - liFailuresBefore
            };

            lOResult.FailLines.AddRange(Check.FailLines.GetRange(liFailLinesBefore, Check.FailLines.Count - liFailLinesBefore));
            mOResults.Add(lOResult);
        }

        public void PrintSummary(long plTotalMilliseconds)
        {
            if (Times)
            {
                Console.WriteLine();
                Console.WriteLine("Group timings");

                foreach (GroupResult lOResult in mOResults)
                {
                    if (!lOResult.Skipped)
                        Console.WriteLine(string.Format("  {0,6} ms  {1}{2}", lOResult.Milliseconds, lOResult.Name,
                            lOResult.Failures > 0 ? "  (" + lOResult.Failures + " failed)" : ""));
                }
            }

            if (Check.Failures > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Groups with failures:");

                foreach (GroupResult lOResult in mOResults)
                    if (lOResult.Failures > 0)
                        Console.WriteLine("  " + lOResult.Name + " (" + lOResult.Failures + ")");
            }

            Console.WriteLine();
            Console.WriteLine(Check.Failures == 0
                ? string.Format("Everything passed, in {0} ms.", plTotalMilliseconds)
                : string.Format("{0} check(s) FAILED, in {1} ms.", Check.Failures, plTotalMilliseconds));

            if (miSkipped > 0)
                Console.WriteLine(string.Format("{0} group(s) skipped for want of the game data.", miSkipped));
        }

        private static bool fContainsAny(string psName, List<string> psParts)
        {
            foreach (string lsPart in psParts)
                if (psName.IndexOf(lsPart, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

            return false;
        }

        /// <summary>A stable hash of the name (FNV-1a), the same on every run and machine.</summary>
        private static int fSeedOf(string psName)
        {
            uint luHash = 2166136261u;

            foreach (char lcAt in psName)
                luHash = unchecked((luHash ^ lcAt) * 16777619u);

            return (int)(luHash & 0x7FFFFFFF);
        }
    }
}
