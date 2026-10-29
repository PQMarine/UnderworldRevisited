using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport.Install;
using UWDataImport.Presentation;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE GERMAN LETTERS IN THE ORIGINAL FONTS (UWFontGlyphBuilder, translations wave 1, 2026-10-26).
    /// On a made-up font (drawn here, nothing of the game): the builder adds its seven letters beyond
    /// the font's own and only there, the font's own glyphs stay as they were, and measuring, wrapping
    /// and drawing count the new ones. On the player's six fonts: every font's own glyphs exactly as a
    /// font read without the builder (English draws as before), and the new glyphs hashed against
    /// Pins/font-glyph-hashes.txt - the hashes of our results, not the fonts. The extracted cache may
    /// lack the FONT*.SYS the image holds (found 2026-10-26: the check's cache had none, so the font
    /// checks of TextRaster, ConversationView and Vitals were never compared); then they are read from
    /// game.gog itself.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckFontGlyphs(CheckContext pOContext)
        {
            Check.Group("Font glyphs (UWFontGlyphBuilder - the German letters, translations wave 1)");

            SortedDictionary<string, string> lONow = new SortedDictionary<string, string>(StringComparer.Ordinal);

            fCheckFontGlyphsMadeUp(lONow);

            int liFonts = 0;
            string lsSource = null;

            foreach (UWFonts.FontType leType in (UWFonts.FontType[])Enum.GetValues(typeof(UWFonts.FontType)))
            {
                byte[] lyBytes = fFontBytes(pOContext, leType, out string lsFrom);

                if (lyBytes == null)
                    continue;

                lsSource = lsSource ?? lsFrom;
                liFonts++;

                UWFont lOPlain = new UWFont(lyBytes);
                UWFont lOFont = new UWFont(lyBytes);

                UWFontGlyphBuilder.AddGerman(lOFont, leType);
                fExpectBool("font glyphs: " + leType + ", its own glyphs untouched", fSameOwnGlyphs(lOPlain, lOFont), true);

                if (pOContext.HasData && pOContext.Data.Fonts != null && pOContext.Data.Fonts.Get(leType).IsLoaded)
                    fExpectBool("font glyphs: " + leType + ", the data's font got them", fSameAdded(lOFont, pOContext.Data.Fonts.Get(leType)), true);

                fRecordGlyphs("font glyphs: " + leType, lOFont, lONow);
            }

            if (liFonts == 0)
            {
                Check.Pass("font glyphs: the player's fonts", "no FONT*.SYS in the data folder or an image, not compared");

                return;
            }

            Check.Pass("font glyphs: the player's fonts", liFonts + " read from " + lsSource);

            fComparePins(pOContext, "font-glyph-hashes.txt", "font glyphs", lONow, psName =>
            {
                string lsRest = psName.Substring("font glyphs: ".Length);
                int liAt = lsRest.IndexOf(' ');

                return liAt > 0 ? lsRest.Substring(0, liAt) : lsRest;
            });
        }

        /// <summary>A font's bytes: the data folder's file, else the same file in the image the data
        /// came from; null when neither has it.</summary>
        private static byte[] fFontBytes(CheckContext pOContext, UWFonts.FontType peType, out string psFrom)
        {
            psFrom = null;

            string lsName = UWFonts.FileName(peType);

            if (!string.IsNullOrEmpty(pOContext.DataPath) && File.Exists(Path.Combine(pOContext.DataPath, lsName)))
            {
                psFrom = "the data folder";

                return File.ReadAllBytes(Path.Combine(pOContext.DataPath, lsName));
            }

            if (string.IsNullOrEmpty(pOContext.ImagePath))
                return null;

            psFrom = "game.gog (the extracted data folder lacks them)";

            return UWGogInstall.ReadDataFile(pOContext.ImagePath, UWGameProfiles.Uw1, lsName);
        }

        /// <summary>The added letters as pins: size and hash, or "none".</summary>
        private static void fRecordGlyphs(string psPrefix, UWFont pOFont, SortedDictionary<string, string> pONow)
        {
            foreach (char lcChar in UWFontGlyphBuilder.Characters)
            {
                string lsName = psPrefix + " U+" + ((int)lcChar).ToString("X4");

                pONow[lsName] = pOFont.TryGetGlyph(lcChar, out UWFont.Glyph lOGlyph)
                    ? string.Format("{0}x{1} {2:X8}", lOGlyph.Width, pOFont.Height, fCrc32(lOGlyph.Pixels))
                    : "none";
            }
        }

        private static bool fSameOwnGlyphs(UWFont pOPlain, UWFont pOBuilt)
        {
            if (pOPlain.GlyphCount != pOBuilt.GlyphCount || pOPlain.Height != pOBuilt.Height || pOPlain.SpaceWidth != pOBuilt.SpaceWidth)
                return false;

            for (int liCode = 0; liCode < pOPlain.GlyphCount; liCode++)
            {
                pOPlain.TryGetGlyph(liCode, out UWFont.Glyph lOPlain);
                pOBuilt.TryGetGlyph(liCode, out UWFont.Glyph lOBuilt);

                if (lOPlain.Width != lOBuilt.Width || !fSameBytes(lOPlain.Pixels, lOBuilt.Pixels))
                    return false;
            }

            return true;
        }

        private static bool fSameAdded(UWFont pOA, UWFont pOB)
        {
            foreach (char lcChar in UWFontGlyphBuilder.Characters)
            {
                bool lbA = pOA.TryGetGlyph(lcChar, out UWFont.Glyph lOA);
                bool lbB = pOB.TryGetGlyph(lcChar, out UWFont.Glyph lOB);

                if (lbA != lbB || (lbA && (lOA.Width != lOB.Width || !fSameBytes(lOA.Pixels, lOB.Pixels))))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// A font made up here, six rows like font5x6p, 128 characters 4 pixels wide (blank), with
        /// a full stop, an I and the six base letters drawn plainly - shapes of our own, so the
        /// builder's rules are checked and pinned without the game.
        /// </summary>
        private static byte[] fMadeUpFontBytes()
        {
            const int liHeight = 6;
            const int liCount = 128;
            byte[] lyData = new byte[12 + (liCount * (liHeight + 1))];

            lyData[0] = 1;
            lyData[2] = liHeight;
            lyData[4] = 3;
            lyData[6] = liHeight;
            lyData[8] = 1;
            lyData[10] = 5;

            Dictionary<char, string[]> lODrawn = new Dictionary<char, string[]>
            {
                { '.', new[] { "..", "..", "..", "..", "#.", ".." } },
                { 'I', new[] { "#####.", "..#...", "..#...", "..#...", "#####.", "......" } },
                { 'a', new[] { "......", ".###..", "....#.", ".####.", "#...#.", ".####." } },
                { 'o', new[] { "......", "......", ".###..", "#...#.", "#...#.", ".###.." } },
                { 'u', new[] { "......", "......", "#...#.", "#...#.", "#..##.", ".##.#." } },
                { 'A', new[] { "..#...", ".#.#..", "#...#.", "#####.", "#...#.", "......" } },
                { 'O', new[] { ".###..", "#...#.", "#...#.", "#...#.", ".###..", "......" } },
                { 'U', new[] { "#...#.", "#...#.", "#...#.", "#...#.", ".###..", "......" } }
            };

            for (int liAt = 0; liAt < liCount; liAt++)
            {
                int liOffset = 12 + (liAt * (liHeight + 1));

                lyData[liOffset + liHeight] = 4;

                if (!lODrawn.TryGetValue((char)liAt, out string[] lsRows))
                    continue;

                lyData[liOffset + liHeight] = (byte)lsRows[0].Length;

                for (int y = 0; y < liHeight; y++)
                {
                    for (int x = 0; x < lsRows[y].Length; x++)
                    {
                        if (lsRows[y][x] == '#')
                            lyData[liOffset + y] |= (byte)(0x80 >> x);
                    }
                }
            }

            return lyData;
        }

        private static void fCheckFontGlyphsMadeUp(SortedDictionary<string, string> pONow)
        {
            byte[] lyBytes = fMadeUpFontBytes();
            UWFont lOPlain = new UWFont(lyBytes);
            UWFont lOFont = new UWFont(lyBytes);

            fExpectBool("made-up font: read from its bytes", lOFont.IsLoaded && lOFont.GlyphCount == 128 && lOFont.Height == 6, true);
            fExpectInt("made-up font: the seven letters added", UWFontGlyphBuilder.AddGerman(lOFont, UWFonts.FontType.Normal), 7);
            fExpectBool("made-up font: its own glyphs untouched", fSameOwnGlyphs(lOPlain, lOFont), true);
            Check.Str("made-up font: the added codes", string.Join(" ", lOFont.AddedCodes), "196 214 220 223 228 246 252");
            fExpectBool("made-up font: a code it has stays its own", lOFont.AddGlyph('a', new UWFont.Glyph { Width = 1, Pixels = new byte[6] }), false);
            fExpectBool("made-up font: a glyph of the wrong height is refused",
                lOFont.AddGlyph(0x100, new UWFont.Glyph { Width = 2, Pixels = new byte[6] }), false);
            fExpectBool("made-up font: without the builder no umlaut", lOPlain.TryGetGlyph('\u00E4', out _), false);
            fExpectInt("a font without lowercase: only the capitals",
                UWFontGlyphBuilder.AddGerman(fWithoutLowercase(lyBytes), UWFonts.FontType.Small), 3);

            // The letter keeps its foot; the dots stand above its top with a row between them.
            lOFont.TryGetGlyph('u', out UWFont.Glyph lOU);
            lOFont.TryGetGlyph('\u00FC', out UWFont.Glyph lOUmlaut);

            fExpectBool("u-umlaut: the u's foot row kept", fRow(lOUmlaut, 5) == fRow(lOU, 5) && fRow(lOUmlaut, 4) == fRow(lOU, 4), true);
            Check.Str("u-umlaut: dots, a blank row, the u", fRow(lOUmlaut, 0) + "|" + fRow(lOUmlaut, 1) + "|" + fRow(lOUmlaut, 2),
                "#...#.|......|#...#.");

            // A capital at the font's top: dots resting on the U's stems would only make them
            // taller, so the U gives up two of its straight rows for dots and a blank row; the A,
            // without a repeated row, gives up the one most like its neighbours (its middle) and
            // takes the dots right on its top, beside the apex.
            lOFont.TryGetGlyph('\u00DC', out UWFont.Glyph lOUUmlaut);
            lOFont.TryGetGlyph('\u00C4', out UWFont.Glyph lOAUmlaut);

            Check.Str("U-umlaut: dots, a blank row, the shortened U", fRow(lOUUmlaut, 0) + "|" + fRow(lOUUmlaut, 1) + "|" + fRow(lOUUmlaut, 2)
                + "|" + fRow(lOUUmlaut, 3) + "|" + fRow(lOUUmlaut, 4), "#...#.|......|#...#.|#...#.|.###..");
            Check.Str("A-umlaut: dots beside the apex, the A one row shorter", fRow(lOAUmlaut, 0) + "|" + fRow(lOAUmlaut, 1) + "|"
                + fRow(lOAUmlaut, 2) + "|" + fRow(lOAUmlaut, 3) + "|" + fRow(lOAUmlaut, 4), ".#.#..|..#...|.#.#..|#####.|#...#.");

            // Measuring, wrapping and drawing count them.
            int liUmlaut = lOFont.MeasureText("\u00E4", 0, UWTextLayout.CharacterSpacing);

            fExpectBool("measure: an umlaut has its glyph's width",
                liUmlaut > 0 && lOFont.TryGetGlyph('\u00E4', out UWFont.Glyph lOA) && liUmlaut == lOA.Width + UWTextLayout.CharacterSpacing, true);
            fExpectInt("measure: a word of them", lOFont.MeasureText("\u00E4\u00F6\u00FC\u00DF", 0, 0),
                fWidth(lOFont, '\u00E4') + fWidth(lOFont, '\u00F6') + fWidth(lOFont, '\u00FC') + fWidth(lOFont, '\u00DF'));
            fExpectInt("measure: without the builder they had no width", lOPlain.MeasureText("\u00E4\u00F6\u00FC\u00DF", 0, 0), 0);

            string lsWord = "\u00C4\u00D6\u00DC\u00DF";
            List<string> lOLines = UWTextLayout.WrapText(lOFont, lsWord + " " + lsWord, lOFont.MeasureText(lsWord, 0, UWTextLayout.CharacterSpacing));

            Check.Str("wrap: two words of umlauts as wide as one break", string.Join("|", lOLines), lsWord + "|" + lsWord);

            UWPicture lOPicture = UWTextRaster.Render(lOFont, new List<string> { "\u00FC" }, 8, null, UWTextRaster.ColoursEnum.Codes, false);

            fExpectBool("draw: the dots are drawn", lOPicture != null && lOPicture.Get(0, 0).A == 255 && lOPicture.Get(4, 0).A == 255
                && lOPicture.Get(1, 0).A == 0, true);

            fRecordGlyphs("font glyphs: made-up", lOFont, pONow);
        }

        /// <summary>The made-up font cut before the lowercase (as FONT4X5P ends at 96).</summary>
        private static UWFont fWithoutLowercase(byte[] pyBytes)
        {
            byte[] lyCut = new byte[12 + (97 * 7)];

            Array.Copy(pyBytes, lyCut, lyCut.Length);

            return new UWFont(lyCut);
        }

        private static int fWidth(UWFont pOFont, char pcChar)
        {
            return pOFont.TryGetGlyph(pcChar, out UWFont.Glyph lOGlyph) ? lOGlyph.Width : -1;
        }

        private static string fRow(UWFont.Glyph pOGlyph, int piRow)
        {
            char[] lcRow = new char[pOGlyph.Width];

            for (int x = 0; x < pOGlyph.Width; x++)
                lcRow[x] = pOGlyph.Pixels[(piRow * pOGlyph.Width) + x] != 0 ? '#' : '.';

            return new string(lcRow);
        }
    }
}
