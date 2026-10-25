using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.7 (2026-10-20): the message log of both schemes reads UWMessageLogModel - the
    /// paging of long texts by the shown scroll's lines (decision 17: the classic box's four new
    /// lines and [MORE], the modern scroll's two to eight; B28), the [MORE] row kept free, the one
    /// blink rate of 0.4 s, the width the lines are wrapped and drawn at (B27), the colour codes and
    /// the rolling edges. Data-free: font5x6p's stand-in (six rows, characters 4 pixels wide).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckMessageLogModel()
        {
            Check.Group("Message log model (UWMessageLogModel - cleanup step 5.7)");

            UWFont lOFont = fStandInFont();

            if (lOFont == null)
            {
                Check.Fail("log: a font", "the stand-in could not be read");
                return;
            }

            fCheckLogBlink();
            fCheckLogCapacity(lOFont);
            fCheckLogPaging();
            fCheckLogShown(lOFont);
            fCheckLogWrapWidth(lOFont);
            fCheckLogColours(lOFont);
        }

        private static void fCheckLogBlink()
        {
            Check.Bool("blink: 0.4 s on, 0.4 s off", UWMessageLogModel.CursorBlinkSeconds == 0.4f, true);
            Check.Bool("blink: on at the start", UWMessageLogModel.IsCursorOn(0f), true);
            Check.Bool("blink: still on at 0.39 s", UWMessageLogModel.IsCursorOn(0.39f), true);
            Check.Bool("blink: off at 0.4 s", UWMessageLogModel.IsCursorOn(0.4f), false);
            Check.Bool("blink: still off at 0.79 s", UWMessageLogModel.IsCursorOn(0.79f), false);
            Check.Bool("blink: on again at 0.8 s", UWMessageLogModel.IsCursorOn(0.8f), true);

            bool lbAlternates = true;

            for (int liPhase = 0; liPhase < 40; liPhase++)
            {
                if (UWMessageLogModel.IsCursorOn(0.2f + (liPhase * 0.4f)) != (liPhase % 2 == 0))
                    lbAlternates = false;
            }

            Check.Bool("blink: every 0.4 s the other state, for 16 s", lbAlternates, true);
        }

        private static void fCheckLogCapacity(UWFont pOFont)
        {
            fExpectInt("capacity: the classic box's 30 rows hold five six-pixel lines",
                UWMessageLogModel.CapacityOf(pOFont, UWClassicLayout.MessageLog.Height), 5);
            fExpectInt("capacity: five without a font", UWMessageLogModel.CapacityOf(null, UWClassicLayout.MessageLog.Height), 5);
            fExpectInt("[MORE]: the classic box keeps four lines above it", UWMessageLogModel.LinesAboveMore(5), 4);
            fExpectInt("[MORE]: ... in its fifth row", UWMessageLogModel.MoreRow(5), 4);
            fExpectInt("[MORE]: the smallest modern scroll keeps one", UWMessageLogModel.LinesAboveMore(2), 1);
            fExpectInt("[MORE]: ... in its second row", UWMessageLogModel.MoreRow(2), 1);
            fExpectInt("[MORE]: the largest modern scroll keeps seven", UWMessageLogModel.LinesAboveMore(8), 7);
            fExpectInt("[MORE]: ... in its eighth row", UWMessageLogModel.MoreRow(8), 7);
            Check.Str("[MORE]: the marker", UWMessageLogModel.MoreMarkerText, "[MORE]");
        }

        private static List<string> fLogLines(string psPrefix, int piCount)
        {
            List<string> lOLines = new List<string>();

            for (int liAt = 1; liAt <= piCount; liAt++)
                lOLines.Add(psPrefix + liAt);

            return lOLines;
        }

        private static string fJoinLog(List<string> pOLines)
        {
            return string.Join("|", pOLines);
        }

        /// <summary>Two old lines in the box and a text of eleven lines, paged at one capacity; returns
        /// what the box held after each page.</summary>
        private static List<string> fPageThrough(int[] piyCapacities, List<string> pOPrinted, out List<bool> pOScrolled)
        {
            List<string> lOPending = fLogLines("L", 11);
            List<string> lOShown = new List<string>();
            List<string> lOPages = new List<string>();
            int liPage = 0;

            pOScrolled = new List<bool>();

            do
            {
                int liCapacity = piyCapacities[System.Math.Min(liPage, piyCapacities.Length - 1)];

                pOScrolled.Add(UWMessageLogModel.ShowNextPage(pOPrinted, lOPending, liCapacity, lOShown));
                lOPages.Add(fJoinLog(lOShown) + (lOPending.Count > 0 ? " [MORE]" : string.Empty));
                liPage++;
            }
            while (lOPending.Count > 0 && liPage < 20);

            return lOPages;
        }

        private static void fCheckLogPaging()
        {
            // The classic box: four new lines and [MORE] per page, the old lines scrolling up.
            List<string> lOPages = fPageThrough(new[] { 5 }, new List<string> { "o1", "o2" }, out List<bool> lOScrolled);

            fExpectInt("paging, classic 5: three pages for eleven lines", lOPages.Count, 3);
            Check.Str("paging, classic 5: page 1, four new lines and [MORE]", lOPages[0], "L1|L2|L3|L4 [MORE]");
            Check.Bool("paging, classic 5: page 1 pushed the old lines out (the scroll moved)", lOScrolled[0], true);
            Check.Str("paging, classic 5: page 2", lOPages[1], "L5|L6|L7|L8 [MORE]");
            Check.Str("paging, classic 5: the last page fills all five lines", lOPages[2], "L7|L8|L9|L10|L11");

            // The modern scroll at its smallest and largest size (B28: paged by the classic five until then).
            lOPages = fPageThrough(new[] { 2 }, new List<string> { "o1", "o2" }, out lOScrolled);
            fExpectInt("paging, modern 2: one new line per page", lOPages.Count, 11);
            Check.Str("paging, modern 2: page 1", lOPages[0], "L1 [MORE]");
            Check.Str("paging, modern 2: page 2", lOPages[1], "L2 [MORE]");
            Check.Str("paging, modern 2: the last page", lOPages[10], "L10|L11");

            lOPages = fPageThrough(new[] { 8 }, new List<string> { "o1", "o2" }, out lOScrolled);
            fExpectInt("paging, modern 8: two pages", lOPages.Count, 2);
            Check.Str("paging, modern 8: page 1, seven new lines and [MORE]", lOPages[0], "L1|L2|L3|L4|L5|L6|L7 [MORE]");
            Check.Str("paging, modern 8: the last page fills all eight lines", lOPages[1], "L4|L5|L6|L7|L8|L9|L10|L11");

            // A short text stays below the old lines without a page and without a scroll.
            List<string> lOPrinted = new List<string> { "o1", "o2" };
            List<string> lOPending = new List<string> { "L1", "L2" };
            List<string> lOShown = new List<string>();
            bool lbScrolled = UWMessageLogModel.ShowNextPage(lOPrinted, lOPending, 5, lOShown);

            Check.Str("paging: a short text below the old lines", fJoinLog(lOShown), "o1|o2|L1|L2");
            Check.Bool("paging: ... nothing pending", lOPending.Count == 0, true);
            Check.Bool("paging: ... the scroll did not move", lbScrolled, false);

            // Leading lines count as printed: a long text pushes them out without a confirmation.
            lOPages = fPageThrough(new[] { 5 }, new List<string> { "look", "You read the scroll..." }, out lOScrolled);
            Check.Str("paging: the leading lines pushed out by the first page", lOPages[0], "L1|L2|L3|L4 [MORE]");

            // Shift+F2 between pages: each page follows the scroll shown at that moment.
            lOPages = fPageThrough(new[] { 5, 2, 8 }, new List<string>(), out lOScrolled);
            fExpectInt("paging, scheme switch: three pages", lOPages.Count, 3);
            Check.Str("paging, scheme switch: classic page", lOPages[0], "L1|L2|L3|L4 [MORE]");
            Check.Str("paging, scheme switch: then the 2-line scroll, one line", lOPages[1], "L5 [MORE]");
            Check.Str("paging, scheme switch: then the 8-line scroll, the rest", lOPages[2], "L4|L5|L6|L7|L8|L9|L10|L11");
        }

        private static void fCheckLogShown(UWFont pOFont)
        {
            const int liWidth = UWClassicLayout.MessageLog.Width;
            UWMessageLogModel lOModel = new UWMessageLogModel();

            // The classic box while [MORE] waits: the four lines the page left, [MORE] in the fifth row.
            Check.Bool("shown: the first update draws", lOModel.Update(pOFont, fLogLines("L", 4), liWidth, 5, true, false), true);
            Check.Str("shown, classic waiting: the four lines", fJoinLog(new List<string>(lOModel.ShownLines)), "L1|L2|L3|L4");
            Check.Bool("shown, classic waiting: [MORE]", lOModel.MoreShown, true);
            fExpectInt("shown, classic waiting: [MORE] in row 4", lOModel.MoreRowShown, 4);

            // More lines than room while waiting: the newest kept, [MORE]'s row stays free.
            lOModel.Update(pOFont, fLogLines("L", 6), liWidth, 5, true, false);
            Check.Str("shown, waiting: the newest above [MORE]", fJoinLog(new List<string>(lOModel.ShownLines)), "L3|L4|L5|L6");

            // Not waiting: as many as fit, the newest.
            lOModel.Update(pOFont, fLogLines("L", 7), liWidth, 5, false, false);
            Check.Str("shown: the newest five", fJoinLog(new List<string>(lOModel.ShownLines)), "L3|L4|L5|L6|L7");
            Check.Bool("shown: no [MORE]", lOModel.MoreShown, false);

            // The modern scroll at four lines while waiting: three lines and [MORE] in the fourth row.
            UWMessageLogModel lOModern = new UWMessageLogModel();

            lOModern.Update(pOFont, fLogLines("L", 3), liWidth, 4, true, false);
            Check.Str("shown, modern 4 waiting: three lines", fJoinLog(new List<string>(lOModern.ShownLines)), "L1|L2|L3");
            fExpectInt("shown, modern 4 waiting: [MORE] in row 3", lOModern.MoreRowShown, 3);

            // Empty messages stay empty lines, a line break inside a message splits it.
            lOModern.Update(pOFont, new List<string> { "a", string.Empty, "b\nc" }, liWidth, 8, false, false);
            Check.Str("shown: an empty message is an empty line, a break splits", fJoinLog(new List<string>(lOModern.ShownLines)), "a||b|c");

            // Same input, nothing to draw; the input box alone redraws without turning the edges.
            UWMessageLogModel lOEdges = new UWMessageLogModel();
            List<string> lOMessages = fLogLines("m", 5);

            lOEdges.Update(pOFont, lOMessages, liWidth, 5, false, false);
            Check.Bool("key: the same messages change nothing", lOEdges.Update(pOFont, lOMessages, liWidth, 5, false, false), false);
            Check.Bool("key: the input box blinking on redraws", lOEdges.Update(pOFont, lOMessages, liWidth, 5, false, true), true);
            Check.Bool("key: ... with the box", lOEdges.CursorOn, true);
            fExpectInt("edges: the blink does not turn them", lOEdges.EdgeFrame, 0);

            lOMessages.Add("m6");
            lOEdges.Update(pOFont, lOMessages, liWidth, 5, false, false);
            fExpectInt("edges: one line out at the top, one frame", lOEdges.EdgeFrame, 1);

            lOMessages.Add("m7");
            lOMessages.Add("m8");
            lOEdges.Update(pOFont, lOMessages, liWidth, 5, false, false);
            fExpectInt("edges: two lines, two frames", lOEdges.EdgeFrame, 3);
            fExpectInt("edges: ... the scroll counted", lOEdges.Scrolled, 2);

            lOMessages.Add("m9");
            lOMessages.Add("m10");
            lOEdges.Update(pOFont, lOMessages, liWidth, 5, false, false);
            fExpectInt("edges: the loop of five wraps", lOEdges.EdgeFrame, 0);

            lOEdges.Invalidate();
            Check.Bool("edges: hidden and shown again redraws", lOEdges.Update(pOFont, lOMessages, liWidth, 5, false, false), true);
            fExpectInt("edges: ... without turning", lOEdges.EdgeFrame, 0);
        }

        private static void fCheckLogWrapWidth(UWFont pOFont)
        {
            const int liPaper = UWClassicLayout.MessageLog.Width;
            const int liWide = 40;

            // A line wider than the original's paper but not than Classic Wide's.
            string lsLine = "word";

            while (pOFont.MeasureText(lsLine, 0, UWTextLayout.CharacterSpacing) <= liPaper + 10)
                lsLine += " word";

            UWMessageLogModel lOModel = new UWMessageLogModel();

            lOModel.Update(pOFont, new List<string> { lsLine }, liPaper + liWide, 5, false, false);
            fExpectInt("B27: the wide paper keeps the line whole", lOModel.ShownLines.Count, 1);
            fExpectInt("B27: drawn as wide as it was wrapped", lOModel.Width, liPaper + liWide);

            int liWidest = 0;

            foreach (string lsShown in lOModel.ShownLines)
                liWidest = System.Math.Max(liWidest, pOFont.MeasureText(lsShown, 0, UWTextLayout.CharacterSpacing));

            Check.Bool("B27: the line is wider than the original's paper", liWidest > liPaper, true);
            Check.Bool("B27: ... and fits the width it is drawn at", liWidest <= lOModel.Width, true);

            lOModel.Update(pOFont, new List<string> { lsLine }, liPaper, 5, false, false);
            fExpectInt("wrap: the original's paper wraps it to two lines", lOModel.ShownLines.Count, 2);
            Check.Bool("wrap: ... each fitting", pOFont.MeasureText(lOModel.ShownLines[0], 0, UWTextLayout.CharacterSpacing) <= liPaper
                && pOFont.MeasureText(lOModel.ShownLines[1], 0, UWTextLayout.CharacterSpacing) <= liPaper, true);

            // The paging wrap counts a line of spaces as empty, as the box's paging always did.
            List<string> lOPaged = UWMessageLogModel.WrapForPaging(pOFont, "a\n   \nb", liPaper);

            fExpectInt("wrap for paging: three lines", lOPaged.Count, 3);
            Check.Str("wrap for paging: the line of spaces empty", lOPaged[1], string.Empty);
        }

        private static void fCheckLogColours(UWFont pOFont)
        {
            fExpectInt("colours: seven codes", UWMessageLogModel.ColourCodeCount, 7);
            fExpectInt("colours: 0 the usual brown (46)", UWMessageLogModel.ColourIndices[0], 46);
            fExpectInt("colours: 4 the red of \"Save Game Failed.\" (180)", UWMessageLogModel.ColourIndices[4], 180);
            fExpectInt("colours: 6 the green of \"Save Game Succeeded.\" (212)", UWMessageLogModel.ColourIndices[6], 212);
            fExpectInt("colours: \\6 switches to green", UWMessageLogModel.ColourCodeAfter("\\6Save Game Succeeded.", 0), 6);
            fExpectInt("colours: \\4 to red", UWMessageLogModel.ColourCodeAfter("\\4Save Game Failed.", 0), 4);
            fExpectInt("colours: \\0 back to brown", UWMessageLogModel.ColourCodeAfter("\\6Save Game Succeeded.\\0", 0), 0);
            fExpectInt("colours: \\9 changes nothing", UWMessageLogModel.ColourCodeAfter("\\9text", 4), 4);

            List<string> lOPrompt = new List<string> { "Saving.", "\\6     Please enter a save file description:", "my save" };

            Check.Seq("colours: the name prompt's green holds into the typed line",
                UWMessageLogModel.LineColourCodesOf(lOPrompt), new[] { 0, 0, 6 });

            UWMessageLogModel lOModel = new UWMessageLogModel();

            lOModel.Update(pOFont, lOPrompt, UWClassicLayout.MessageLog.Width, 5, false, true);
            Check.Seq("colours: the model's line codes", new List<int>(lOModel.LineColourCodes).ToArray(), new[] { 0, 0, 6 });

            // The rule is the raster's: each line's starting colour as UWTextRaster reports it.
            UWColor32[] lyTable = new UWColor32[UWMessageLogModel.ColourCodeCount];

            for (int liAt = 0; liAt < lyTable.Length; liAt++)
                lyTable[liAt] = new UWColor32((byte)(10 + liAt), 0, 0, 255);

            List<string> lOLines = new List<string> { "\\4red", "still red\\9", "\\6green", "\\0brown", "brown" };
            List<UWColor32> lORaster = new List<UWColor32>();

            UWTextRaster.Render(pOFont, lOLines, 200, lyTable, UWTextRaster.ColoursEnum.Codes, false, false, lORaster);

            int[] liyCodes = UWMessageLogModel.LineColourCodesOf(lOLines);
            int[] liyRaster = new int[lORaster.Count];

            for (int liAt = 0; liAt < liyRaster.Length; liAt++)
                liyRaster[liAt] = lORaster[liAt].R - 10;

            Check.Seq("colours: the line codes are the raster's", liyCodes, liyRaster);
            Check.Seq("colours: ... across lines", liyCodes, new[] { 0, 4, 4, 6, 0 });

            UWColor32[] lyFallback = UWMessageLogModel.ColourTable(null, out bool lbFromPalette);

            Check.Bool("colours: no palette, the fallback table", lbFromPalette, false);
            Check.Bool("colours: ... #3D1C08 for every code", lyFallback.Length == 7 && lyFallback[0].R == 61 && lyFallback[0].G == 28
                && lyFallback[0].B == 8 && lyFallback[6].R == 61, true);
        }
    }
}
