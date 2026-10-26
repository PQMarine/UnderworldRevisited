using System;
using System.Collections.Generic;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.5 (2026-10-19): text in the original fonts is drawn engine-free
    /// (UWTextRaster) and only uploaded by Unity (UWTextTextures). The old renderer
    /// (UWFontRenderer.RenderLines: a new texture, cleared, one SetPixel per glyph pixel, rows bottom
    /// up), the conversation's line-by-line composition and the prompt cursor drawn into the finished
    /// texture are kept here as reference loops; every font and a set of lines - colour codes across
    /// lines, centring, clipping, characters the font lacks - must come out byte for byte the same
    /// after the upload rows (UWUploadPixels.FromColours, Keep, no colour help). Plus the measures
    /// the labels size themselves by: the height formula, the tight width, the cursor box.
    /// </summary>
    public static partial class SelfChecks
    {
        private static readonly UWColor32 msRasterBrown = new UWColor32(0x3C, 0x28, 0x20, 255);

        private static readonly UWColor32[] msRasterTable =
        {
            new UWColor32(0x3C, 0x28, 0x20, 255), new UWColor32(10, 20, 30, 255), new UWColor32(40, 50, 60, 255),
            new UWColor32(255, 255, 255, 255), new UWColor32(200, 0, 0, 255), new UWColor32(0, 0, 200, 255),
            new UWColor32(0, 180, 0, 255)
        };

        private static void fCheckTextRaster(DataImport pOData)
        {
            Check.Group("Text raster (UWTextRaster - cleanup step 5.5)");

            int liFonts = 0;

            foreach (UWFonts.FontType leType in (UWFonts.FontType[])Enum.GetValues(typeof(UWFonts.FontType)))
            {
                UWFont lOFont = pOData != null && pOData.Fonts != null ? pOData.Fonts.Get(leType) : null;

                if (lOFont == null || !lOFont.IsLoaded)
                    continue;

                liFonts++;
                fCheckTextRasterFont(leType.ToString(), lOFont);
            }

            if (liFonts == 0)
            {
                // The GOG archive the check unpacks carries no FONT*.SYS.
                Check.Pass("text: the fonts", "no FONT*.SYS in this data folder, not compared");

                return;
            }

            UWFont lONormal = pOData.Fonts.Get(UWFonts.FontType.Normal);

            if (lONormal == null || !lONormal.IsLoaded)
                return;

            // The measures.
            List<string> lOTwo = new List<string> { "abc", "de" };

            fExpectInt("text: the height of two lines", UWTextRaster.Height(lONormal, 2),
                (2 * (lONormal.Height + UWTextLayout.LineSpacing)) - UWTextLayout.LineSpacing);
            fExpectInt("text: the tight width is the measured width less one spacing",
                UWTextRaster.TightWidth(lONormal, "[MORE]"), lONormal.MeasureText("[MORE]", 0, UWTextLayout.CharacterSpacing) - UWTextLayout.CharacterSpacing);
            fExpectInt("text: the tight width of nothing is one pixel", UWTextRaster.TightWidth(lONormal, string.Empty), 1);
            fExpectBool("text: no lines, no picture", UWTextRaster.Render(lONormal, new List<string>(), 20, null, UWTextRaster.ColoursEnum.Codes, false) == null, true);
            fExpectBool("text: no width, no picture", UWTextRaster.Render(lONormal, lOTwo, 0, null, UWTextRaster.ColoursEnum.Codes, false) == null, true);

            UWPicture lOPicture = UWTextRaster.Render(lONormal, lOTwo, 40, null, UWTextRaster.ColoursEnum.Codes, false);

            fExpectBool("text: the picture is as wide as asked and as tall as its lines",
                lOPicture.Width == 40 && lOPicture.Height == UWTextRaster.Height(lONormal, 2), true);

            // The glyph's place: the first line's 'a' at x 0, the second line's 'd' one line lower.
            int liFirstInk = fFirstInk(lOPicture, 0, lONormal.Height, out UWColor32 lOInk);

            fExpectBool("text: the first glyph sits at the line's left edge, white without colours",
                liFirstInk >= 0 && liFirstInk < lONormal.MeasureText("a", 0, UWTextLayout.CharacterSpacing) && lOInk.R == 255 && lOInk.A == 255, true);
            fExpectBool("text: the second line starts one line height down",
                fFirstInk(lOPicture, lONormal.Height + UWTextLayout.LineSpacing, lONormal.Height, out _) >= 0, true);

            // Centring: "I" in a picture 21 wide starts at (21 - its width) / 2.
            int liI = lONormal.MeasureText("I", 0, UWTextLayout.CharacterSpacing);
            UWPicture lOCentred = UWTextRaster.Render(lONormal, new List<string> { "I" }, 21, null, UWTextRaster.ColoursEnum.Codes, true);
            int liFirstColumn = fFirstInkColumn(lOCentred);

            fExpectBool("text: a centred line starts at (width - line width) / 2",
                liFirstColumn >= (21 - liI) / 2 && liFirstColumn < ((21 - liI) / 2) + liI, true);

            // Colours: a code switches and holds into the next line; the line colours report it.
            List<UWColor32> lOLineColours = new List<UWColor32>();

            UWTextRaster.Render(lONormal, new List<string> { "a\\4b", "c", "\\0d" }, 60, msRasterTable, UWTextRaster.ColoursEnum.Codes, false,
                false, lOLineColours);

            fExpectBool("text: the colour at each line's start (a code holds across lines)",
                lOLineColours.Count == 3 && fSame(lOLineColours[0], msRasterTable[0]) && fSame(lOLineColours[1], msRasterTable[4])
                && fSame(lOLineColours[2], msRasterTable[4]), true);

            lOLineColours.Clear();
            UWTextRaster.Render(lONormal, new List<string> { "a\\4b", "c" }, 60, new[] { msRasterTable[5], msRasterTable[6] },
                UWTextRaster.ColoursEnum.PerLine, false, false, lOLineColours);

            fExpectBool("text: per line, every line starts in its own colour",
                lOLineColours.Count == 2 && fSame(lOLineColours[0], msRasterTable[5]) && fSame(lOLineColours[1], msRasterTable[6]), true);

            // The cursor box behind the last line.
            UWRectInt lOBox = UWTextRaster.CursorBox(lONormal, lOTwo, 40, lOPicture.Height);

            fExpectBool("text: the cursor box stands right after the last line's text, a line down",
                lOBox.X == lONormal.MeasureText("de", 0, UWTextLayout.CharacterSpacing) && lOBox.Y == lONormal.Height + UWTextLayout.LineSpacing
                && lOBox.Width == Math.Max(2, lONormal.SpaceWidth) && lOBox.Height == lONormal.Height, true);
            fExpectInt("text: a cursor box past the right edge is cut away",
                UWTextRaster.CursorBox(lONormal, new List<string> { "WWWWWWWW" }, 3, lONormal.Height).Width, 0);
        }

        /// <summary>One font against the old loops.</summary>
        private static void fCheckTextRasterFont(string psFont, UWFont pOFont)
        {
            System.Text.StringBuilder lOAll = new System.Text.StringBuilder();

            for (int liChar = 32; liChar < 127; liChar++)
                lOAll.Append((char)liChar);

            List<string>[] lyCases =
            {
                new List<string> { "Ab" },
                new List<string> { "9674" },
                new List<string> { lOAll.ToString() },
                new List<string> { "  leading spaces", "and a second line", string.Empty, "after an empty one" },
                new List<string> { "\\4Save Game Failed." },
                new List<string> { "\\6     Please enter a save file description:", "typed name", "\\0back to normal" },
                new List<string> { "codes \\1one \\2two \\9beyond the table \\3three" },
                new List<string> { "a character the font lacks: äöüß end" },
                new List<string> { "[MORE]" },
                new List<string> { "a line much too long for the narrow picture it is drawn into" }
            };

            int[] liWidths = { 1, 7, 40, 159, 291, 320 };
            int liCompared = 0;
            int liDiffering = 0;
            string lsFirstDiffering = null;

            foreach (List<string> lOLines in lyCases)
            {
                foreach (int liWidth in liWidths)
                {
                    foreach (bool lbCentred in new[] { false, true })
                    {
                        foreach (UWColor32[] lyColours in new[] { new[] { msRasterBrown }, msRasterTable, null })
                        {
                            List<UWColor32> lONewColours = new List<UWColor32>();
                            List<UWColor32> lOOldColours = new List<UWColor32>();
                            UWPicture lONew = UWTextRaster.Render(pOFont, lOLines, liWidth, lyColours, UWTextRaster.ColoursEnum.Codes, lbCentred,
                                false, lONewColours);
                            byte[] lyOld = fOldRenderLines(pOFont, lOLines, liWidth, lyColours, lbCentred, lOOldColours);

                            liCompared++;

                            if (!fSameUpload(lONew, lyOld) || !fSameColours(lONewColours, lOOldColours))
                            {
                                liDiffering++;
                                lsFirstDiffering = lsFirstDiffering ?? ("'" + lOLines[0] + "' width " + liWidth + (lbCentred ? " centred" : ""));
                            }
                        }
                    }

                    // The conversation: each line in its own colour, left-aligned.
                    UWColor32[] lyPerLine = new UWColor32[lOLines.Count];

                    for (int liLine = 0; liLine < lyPerLine.Length; liLine++)
                        lyPerLine[liLine] = msRasterTable[liLine % msRasterTable.Length];

                    liCompared++;

                    if (!fSameUpload(UWTextRaster.Render(pOFont, lOLines, liWidth, lyPerLine, UWTextRaster.ColoursEnum.PerLine, false),
                        fOldConversationLines(pOFont, lOLines, lyPerLine, liWidth)))
                    {
                        liDiffering++;
                        lsFirstDiffering = lsFirstDiffering ?? ("per line '" + lOLines[0] + "' width " + liWidth);
                    }

                    // The message log with its blinking box.
                    List<UWColor32> lOLogColours = new List<UWColor32>();
                    byte[] lyLog = fOldRenderLines(pOFont, lOLines, liWidth, msRasterTable, false, lOLogColours);

                    if (lyLog != null)
                        fOldDrawPromptCursor(lyLog, liWidth, UWTextRaster.Height(pOFont, lOLines.Count), pOFont, lOLines, lOLogColours[lOLogColours.Count - 1]);

                    liCompared++;

                    if (!fSameUpload(UWTextRaster.Render(pOFont, lOLines, liWidth, msRasterTable, UWTextRaster.ColoursEnum.Codes, false, true), lyLog))
                    {
                        liDiffering++;
                        lsFirstDiffering = lsFirstDiffering ?? ("cursor '" + lOLines[0] + "' width " + liWidth);
                    }
                }
            }

            if (liDiffering == 0)
                Check.Pass("text: " + psFont + " as the old renderer drew it", liCompared + " pictures byte for byte");
            else
                Check.Fail("text: " + psFont + " as the old renderer drew it", liDiffering + " of " + liCompared + " differ, first " + lsFirstDiffering);
        }

        /// <summary>UWFontRenderer.RenderLines as it was until 2026-10-19: rows bottom up, RGBA.</summary>
        private static byte[] fOldRenderLines(UWFont pOFont, List<string> pOLines, int piWidth, UWColor32[] pyColourTable, bool pbCentred,
            List<UWColor32> pOLineColours)
        {
            if (pOFont == null || !pOFont.IsLoaded || pOLines == null || pOLines.Count == 0)
                return null;

            UWColor32 lOColour = pyColourTable != null && pyColourTable.Length > 0 ? pyColourTable[0] : new UWColor32(255, 255, 255, 255);
            int liLineHeight = pOFont.Height + UWTextLayout.LineSpacing;
            int liHeight = (liLineHeight * pOLines.Count) - UWTextLayout.LineSpacing;
            byte[] lyPixels = new byte[piWidth * liHeight * 4];

            for (int liLine = 0; liLine < pOLines.Count; liLine++)
            {
                string lsLine = pOLines[liLine];
                int liLineWidth = pOFont.MeasureText(lsLine, 0, UWTextLayout.CharacterSpacing);
                int liCursor = pbCentred ? Math.Max(0, (piWidth - liLineWidth) / 2) : 0;
                int liTop = liLine * liLineHeight;

                pOLineColours?.Add(lOColour);

                for (int liAt = 0; liAt < lsLine.Length; liAt++)
                {
                    char lcChar = lsLine[liAt];

                    if (UWFont.IsColourCode(lsLine, liAt))
                    {
                        int liCode = lsLine[liAt + 1] - '0';

                        if (pyColourTable != null && liCode < pyColourTable.Length)
                            lOColour = pyColourTable[liCode];

                        liAt++;
                        continue;
                    }

                    if (lcChar == ' ')
                    {
                        liCursor += pOFont.SpaceWidth + UWTextLayout.CharacterSpacing;
                        continue;
                    }

                    if (!pOFont.TryGetGlyph(lcChar, out UWFont.Glyph lOGlyph))
                        continue;

                    for (int liY = 0; liY < pOFont.Height; liY++)
                    {
                        for (int liX = 0; liX < lOGlyph.Width; liX++)
                        {
                            if (lOGlyph.Pixels[(liY * lOGlyph.Width) + liX] == 0)
                                continue;

                            int liTargetX = liCursor + liX;
                            int liTargetY = liHeight - 1 - (liTop + liY);

                            if (liTargetX >= 0 && liTargetX < piWidth && liTargetY >= 0 && liTargetY < liHeight)
                                fPut(lyPixels, piWidth, liTargetX, liTargetY, lOColour);
                        }
                    }

                    liCursor += lOGlyph.Width + UWTextLayout.CharacterSpacing;
                }
            }

            return lyPixels;
        }

        /// <summary>UWConversationScreen.fRenderInto as it was: every line rendered on its own in its
        /// colour, its visible pixels stacked into one picture.</summary>
        private static byte[] fOldConversationLines(UWFont pOFont, List<string> pOLines, UWColor32[] pyColours, int piWidth)
        {
            int liLineHeight = pOFont.Height + UWTextLayout.LineSpacing;
            int liHeight = Math.Max(1, pOLines.Count * liLineHeight);
            byte[] lyPixels = new byte[piWidth * liHeight * 4];

            for (int liLine = 0; liLine < pOLines.Count; liLine++)
            {
                byte[] lyLine = fOldRenderLines(pOFont, new List<string> { pOLines[liLine] }, piWidth, new[] { pyColours[liLine] }, false, null);

                if (lyLine == null)
                    continue;

                int liLineRows = lyLine.Length / 4 / piWidth;
                int liTargetTop = liHeight - ((liLine + 1) * liLineHeight);

                for (int y = 0; y < liLineRows && y < liLineHeight; y++)
                {
                    int liTargetY = liTargetTop + y;

                    if (liTargetY < 0 || liTargetY >= liHeight)
                        continue;

                    for (int x = 0; x < piWidth; x++)
                    {
                        int liFrom = ((y * piWidth) + x) * 4;

                        if (lyLine[liFrom + 3] > 0)
                            Array.Copy(lyLine, liFrom, lyPixels, ((liTargetY * piWidth) + x) * 4, 4);
                    }
                }
            }

            return lyPixels;
        }

        /// <summary>UWHudMessageLog.fDrawPromptCursor as it was, into the finished picture.</summary>
        private static void fOldDrawPromptCursor(byte[] pyPixels, int piWidth, int piHeight, UWFont pOFont, List<string> pOLines, UWColor32 pOColour)
        {
            int liLast = pOLines.Count - 1;
            int liLeft = pOFont.MeasureText(pOLines[liLast], 0, UWTextLayout.CharacterSpacing);
            int liTop = liLast * (pOFont.Height + UWTextLayout.LineSpacing);
            int liWide = Math.Max(2, pOFont.SpaceWidth);

            for (int liY = 0; liY < pOFont.Height; liY++)
            {
                int liTargetY = piHeight - 1 - (liTop + liY);

                for (int liX = 0; liX < liWide; liX++)
                {
                    int liTargetX = liLeft + liX;

                    if (liTargetX >= 0 && liTargetX < piWidth && liTargetY >= 0 && liTargetY < piHeight)
                        fPut(pyPixels, piWidth, liTargetX, liTargetY, pOColour);
                }
            }
        }

        private static void fPut(byte[] pyPixels, int piWidth, int piX, int piBottomUpY, UWColor32 pOColour)
        {
            int liAt = ((piBottomUpY * piWidth) + piX) * 4;

            pyPixels[liAt] = pOColour.R;
            pyPixels[liAt + 1] = pOColour.G;
            pyPixels[liAt + 2] = pOColour.B;
            pyPixels[liAt + 3] = pOColour.A;
        }

        /// <summary>The new picture as Unity receives it (UWTextTextures: Keep, no colour help) against
        /// the old bytes.</summary>
        private static bool fSameUpload(UWPicture pONew, byte[] pyOld)
        {
            if (pONew == null || pyOld == null)
                return pONew == null && pyOld == null;

            byte[] lyNew = UWUploadPixels.FromColours(pONew.Pixels, pONew.Width, pONew.Height, null, UWUploadPixels.TransparentEnum.Keep);

            return System.Linq.Enumerable.SequenceEqual(lyNew, pyOld);
        }

        private static bool fSameColours(List<UWColor32> pONew, List<UWColor32> pOOld)
        {
            if (pONew.Count != pOOld.Count)
                return false;

            for (int liAt = 0; liAt < pONew.Count; liAt++)
            {
                if (!fSame(pONew[liAt], pOOld[liAt]))
                    return false;
            }

            return true;
        }

        private static bool fSame(UWColor32 pOLeft, UWColor32 pORight)
        {
            return pOLeft.R == pORight.R && pOLeft.G == pORight.G && pOLeft.B == pORight.B && pOLeft.A == pORight.A;
        }

        /// <summary>The first column with a visible pixel in rows piTop.. piTop + piRows - 1, its colour
        /// out; -1 for none.</summary>
        private static int fFirstInk(UWPicture pOPicture, int piTop, int piRows, out UWColor32 pOInk)
        {
            for (int x = 0; x < pOPicture.Width; x++)
            {
                for (int y = piTop; y < Math.Min(pOPicture.Height, piTop + piRows); y++)
                {
                    if (pOPicture.Get(x, y).A != 0)
                    {
                        pOInk = pOPicture.Get(x, y);

                        return x;
                    }
                }
            }

            pOInk = default;

            return -1;
        }

        private static int fFirstInkColumn(UWPicture pOPicture)
        {
            return fFirstInk(pOPicture, 0, pOPicture.Height, out _);
        }
    }
}
