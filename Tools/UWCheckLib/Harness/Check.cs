using System;
using System.Collections.Generic;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The expectations of the self-check and their two outcomes. Every passing expectation
    /// prints a line unless Quiet is set; every failing one prints and counts. The names are
    /// the stable part of the output: a run before and after a refactor must produce the same
    /// set of OK and FAIL names, which is what the baseline comparison of the cleanup relies on.
    /// </summary>
    public static class Check
    {
        /// <summary>Print only failures and the summary.</summary>
        public static bool Quiet;

        public static int Failures { get; private set; }
        public static int Passes { get; private set; }

        /// <summary>The check group currently running, for the failure summary.</summary>
        public static string CurrentGroup = "";

        /// <summary>Every failure as its line, in order (for the --junit report).</summary>
        public static readonly List<string> FailLines = new List<string>();

        public static void Pass(string psName, string psDetail)
        {
            Passes++;

            if (!Quiet)
                Console.WriteLine(string.Format("  OK   {0,-10} {1}", psName, psDetail));
        }

        public static void Fail(string psName, string psDetail)
        {
            Failures++;

            string lsLine = string.Format("  FAIL {0,-10} {1}", psName, psDetail);

            FailLines.Add(lsLine.Trim());
            Console.WriteLine(lsLine);
        }

        /// <summary>A group heading, printed as the checks always did: a blank line, then the title.</summary>
        public static void Group(string psTitle)
        {
            CurrentGroup = psTitle;

            if (Quiet)
                return;

            Console.WriteLine();
            Console.WriteLine(psTitle);
        }

        public static void Bool(string psName, bool pbActual, bool pbExpected)
        {
            if (pbActual == pbExpected)
                Pass(psName, pbActual.ToString());
            else
                Fail(psName, "got " + pbActual + ", expected " + pbExpected);
        }

        public static void Int(string psName, int piActual, int piExpected)
        {
            if (piActual == piExpected)
                Pass(psName, piActual.ToString());
            else
                Fail(psName, "got " + piActual + ", expected " + piExpected);
        }

        public static void Str(string psName, string psActual, string psExpected)
        {
            if (string.Equals(psActual, psExpected, StringComparison.Ordinal))
                Pass(psName, psActual ?? "null");
            else
                Fail(psName, "got \"" + (psActual ?? "null") + "\", expected \"" + (psExpected ?? "null") + "\"");
        }

        /// <summary>Within a tolerance, for the few float rules.</summary>
        public static void Near(string psName, double pdActual, double pdExpected, double pdTolerance)
        {
            if (Math.Abs(pdActual - pdExpected) <= pdTolerance)
                Pass(psName, pdActual.ToString("R"));
            else
                Fail(psName, "got " + pdActual.ToString("R") + ", expected " + pdExpected.ToString("R") + " within " + pdTolerance.ToString("R"));
        }

        /// <summary>Two sequences element by element; the first difference is reported.</summary>
        public static void Seq(string psName, int[] piActual, int[] piExpected)
        {
            if (piActual == null || piExpected == null)
            {
                Bool(psName, piActual == null && piExpected == null, true);
                return;
            }

            int liCount = Math.Min(piActual.Length, piExpected.Length);

            for (int liAt = 0; liAt < liCount; liAt++)
            {
                if (piActual[liAt] != piExpected[liAt])
                {
                    Fail(psName, "differs at " + liAt + ": got " + piActual[liAt] + ", expected " + piExpected[liAt]);
                    return;
                }
            }

            if (piActual.Length != piExpected.Length)
            {
                Fail(psName, "length " + piActual.Length + ", expected " + piExpected.Length);
                return;
            }

            Pass(psName, string.Join(" ", piActual));
        }
    }
}
