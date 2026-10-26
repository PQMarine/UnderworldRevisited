using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UWDataImport.Presentation;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// A LANGUAGE'S OWN FONT AND INPUT METHOD (translation wave 6, 2026-10-26), engine-free: the header
    /// (grammar.rule.font, grammar.rule.ime - UWLanguageFont) reads and is checked; the made-up language
    /// xz of Tools/Fixtures/i18n names a font and the input method and its text entries take what an input
    /// method commits; the rule that fits a rasterised character into an original font's rows
    /// (UWFallbackGlyphs) on a made-up font with a made-up rasteriser - an ideograph shrunk to the rows,
    /// a capital on the baseline at the capital height, a letter with a descender in the last row; the
    /// fonts' fallback (UWFont.Fallback) asked only beyond ASCII and once per character, forgotten when
    /// it changes; ASCII drawn pixel for pixel the same with and without it; and the wrapping between
    /// Chinese and Japanese characters. The Unity rasteriser (UWFallbackFont) is the editor's check
    /// (UW / Check font fallback).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckLanguageFont()
        {
            Check.Group("Language font and input method (UWLanguageFont, UWFallbackGlyphs - translation wave 6)");

            try
            {
                fCheckLanguageFontHeader();
                fCheckFallbackGlyphs();
            }
            finally
            {
                UWFont.Fallback = null;
                UWText.UseEnglish();
                UWTextLine.UseEnglish();
            }
        }

        private static void fCheckLanguageFontHeader()
        {
            List<string> lsProblems = new List<string>();
            List<UWLanguageFont.Source> lOSources = UWLanguageFont.Parse(" zh-TW.otf ,os:Microsoft JhengHei, a/b.ttf, x.png, os: ,font.TTC", lsProblems);

            Check.Str("header: the entries in order", string.Join(" | ", lOSources), "zh-TW.otf | os:Microsoft JhengHei | font.TTC");
            fExpectInt("header: a path, a picture, an empty family left out", lsProblems.Count, 3);
            fExpectInt("header: none is no font", UWLanguageFont.Parse("none", null).Count, 0);
            fExpectBool("English: no font, no input method", !UWLanguageFont.HasFont && !UWLanguageFont.ImeWanted, true);
            Check.Str("header: ime neither yes nor no", string.Join("; ", UWLanguageFont.CheckHeader(psId => psId == UWLanguageFont.ImeRuleId ? "ja" : null)),
                "grammar.rule.ime: ja is neither yes nor no");

            string lsRoot = CheckContext.ProjectRoot;
            string lsFixture = lsRoot == null ? null : Path.Combine(lsRoot, "Tools", "Fixtures", "i18n", "xz.tsv");

            if (lsFixture == null || !File.Exists(lsFixture))
            {
                Check.Pass("xz: the made-up language", "skipped - no source tree");

                return;
            }

            UWTextLanguageFile lOXz = UWText.LoadLanguage("xz", lsFixture);

            Check.Str("xz: every line read", lOXz == null ? "unreadable" : string.Join("; ", lOXz.Problems), string.Empty);
            Check.Str("xz: the header reads (no letter left out, no font missing)", string.Join("; ",
                UWGrammarGeneric.CheckHeader(psId => lOXz != null && lOXz.Texts.TryGetValue(psId, out string lsText) ? lsText : null)), string.Empty);
            Check.Str("xz: its fonts", string.Join(" | ", UWLanguageFont.Sources), "xz.otf | os:Microsoft JhengHei | os:Arial | os:Noto Sans CJK TC | os:DejaVu Sans");

            UWTextLine.UseLanguage(UWGrammar.For("xz"));

            fExpectBool("xz: the input method on", UWTextLine.ImeOn, true);
            fExpectBool("xz: an ideograph and a Cyrillic letter typeable", UWTextLine.IsTypeableInLanguage('山') && UWTextLine.IsTypeableInLanguage('Ж'), true);
            fExpectBool("xz: no control code, no half surrogate, not into a save", !UWTextLine.IsTypeableInLanguage('\u0085')
                && !UWTextLine.IsTypeableInLanguage('\uD83D') && !UWTextLine.IsTypeableAndStorable('山'), true);

            UWText.UseEnglish();
            UWTextLine.UseEnglish();

            fExpectBool("English again: no input method, no ideograph", !UWTextLine.ImeOn && !UWTextLine.IsTypeableInLanguage('山'), true);
        }

        /// <summary>A made-up font like the font glyphs' (six rows, 128 blank characters 4 wide), with an
        /// H whose five rows end above the last, the descenders' row.</summary>
        private static UWFont fMadeUpFontWithH()
        {
            byte[] lyData = fMadeUpFontBytes();
            string[] lsRows = { "#...#.", "#...#.", "#####.", "#...#.", "#...#.", "......" };
            int liOffset = 12 + ('H' * 7);

            for (int y = 0; y < 6; y++)
            {
                lyData[liOffset + y] = 0;

                for (int x = 0; x < 6; x++)
                {
                    if (lsRows[y][x] == '#')
                        lyData[liOffset + y] |= (byte)(0x80 >> x);
                }
            }

            lyData[liOffset + 6] = 6;

            return new UWFont(lyData);
        }

        /// <summary>A made-up rasteriser of a made-up font whose capitals are 0.75 of the size: an
        /// ideograph (U+5C71) a full box of the size, a capital (U+0416) at the capital height, a letter
        /// with a descender (U+0446), a full stop (U+3002); anything else it lacks.</summary>
        private static UWRasterGlyph fMadeUpRaster(char pcChar, int piSize)
        {
            int liWidth;
            int liHeight;
            int liBearingY;
            int liAdvance;

            switch (pcChar)
            {
                case '山':
                    liWidth = piSize - 1;
                    liHeight = piSize;
                    liBearingY = piSize - 1;
                    liAdvance = piSize;
                    break;
                case 'Ж':
                    liHeight = (int)Math.Round(0.75 * piSize, MidpointRounding.AwayFromZero);
                    liWidth = (int)Math.Round(0.6 * piSize, MidpointRounding.AwayFromZero);
                    liBearingY = liHeight;
                    liAdvance = liWidth + 1;
                    break;
                case 'ц':
                    liBearingY = (int)Math.Round(0.5 * piSize, MidpointRounding.AwayFromZero);
                    liHeight = liBearingY + 1;
                    liWidth = 3;
                    liAdvance = 4;
                    break;
                case '。':
                case '、':
                    liWidth = 2;
                    liHeight = 1;
                    liBearingY = 1;
                    liAdvance = 3;
                    break;
                default:
                    return new UWRasterGlyph();
            }

            byte[] lyPixels = new byte[liWidth * liHeight];

            // The comma vanishes below size 8, as a real one may at small sizes.
            for (int liAt = 0; liAt < lyPixels.Length; liAt++)
                lyPixels[liAt] = (byte)(pcChar == '、' && piSize < 8 ? 0 : 1);

            return new UWRasterGlyph
            {
                Found = true, Width = liWidth, Height = liHeight, BearingX = 0, BearingY = liBearingY, Advance = liAdvance, Pixels = lyPixels
            };
        }

        /// <summary>The made-up rasteriser as the fonts' fallback, counting what it is asked.</summary>
        private sealed class MadeUpGlyphSource : IUWGlyphSource
        {
            public int Asked;

            public bool TryGetGlyph(UWFont pOFont, int piCode, out UWFont.Glyph pOGlyph)
            {
                Asked++;

                return UWFallbackGlyphs.Build(pOFont, 0.75f, piSize => fMadeUpRaster((char)piCode, piSize), out pOGlyph);
            }
        }

        private static string fRows(UWFont.Glyph pOGlyph, int piHeight)
        {
            StringBuilder lORows = new StringBuilder();

            for (int y = 0; y < piHeight; y++)
            {
                if (y > 0)
                    lORows.Append('|');

                for (int x = 0; x < pOGlyph.Width; x++)
                    lORows.Append(pOGlyph.Pixels[(y * pOGlyph.Width) + x] != 0 ? '#' : '.');
            }

            return lORows.ToString();
        }

        private static void fCheckFallbackGlyphs()
        {
            UWFont lOFont = fMadeUpFontWithH();

            UWFallbackGlyphs.CapitalRows(lOFont, out int liTop, out int liBaseline);
            Check.Str("fit: the H's rows give the capitals and the baseline", liTop + ".." + liBaseline, "0..5");
            fExpectInt("fit: the first size puts capitals of 0.75 at the H's five rows", UWFallbackGlyphs.StartSize(lOFont, 0.75f), 7);

            fExpectBool("fit: an ideograph", UWFallbackGlyphs.Build(lOFont, 0.75f, piSize => fMadeUpRaster('山', piSize), out UWFont.Glyph lOIdeograph), true);
            Check.Str("fit: the ideograph one size smaller, filling the rows, a free column", fRows(lOIdeograph, 6),
                "#####.|#####.|#####.|#####.|#####.|#####.");
            UWFallbackGlyphs.Build(lOFont, 0.75f, piSize => fMadeUpRaster('Ж', piSize), out UWFont.Glyph lOCapital);
            Check.Str("fit: a capital on the baseline at the H's height", fRows(lOCapital, 6), "####.|####.|####.|####.|####.|.....");
            UWFallbackGlyphs.Build(lOFont, 0.75f, piSize => fMadeUpRaster('ц', piSize), out UWFont.Glyph lODescender);
            Check.Str("fit: a descender in the last row", fRows(lODescender, 6), "....|###.|###.|###.|###.|###.");
            UWFallbackGlyphs.Build(lOFont, 0.75f, piSize => fMadeUpRaster('、', piSize), out UWFont.Glyph lOComma);
            Check.Str("fit: a mark rasterised away at its size taken one size larger", fRows(lOComma, 6), "...|...|...|...|##.|...");
            fExpectBool("fit: a character the font lacks", UWFallbackGlyphs.Build(lOFont, 0.75f, piSize => fMadeUpRaster('ก', piSize), out _), false);

            // The fonts' fallback: beyond ASCII only, once per character, forgotten when it changes.
            MadeUpGlyphSource lOSource = new MadeUpGlyphSource();
            UWFont lOPlain = fMadeUpFontWithH();
            string lsAscii = string.Empty;

            for (char lcChar = ' '; lcChar <= '~'; lcChar++)
                lsAscii += lcChar;

            UWPicture lOWithout = UWTextRaster.Render(lOPlain, new List<string> { lsAscii }, 600, null, UWTextRaster.ColoursEnum.Codes, false);
            int liVersion = UWFont.FallbackVersion;

            UWFont.Fallback = lOSource;

            UWPicture lOWith = UWTextRaster.Render(lOPlain, new List<string> { lsAscii }, 600, null, UWTextRaster.ColoursEnum.Codes, false);

            fExpectBool("fallback: ASCII drawn pixel for pixel as without it", lOWith != null && lOWithout != null
                && lOWith.Width == lOWithout.Width && fSamePixels(lOWith, lOWithout), true);
            fExpectInt("fallback: ASCII never asks it", lOSource.Asked, 0);
            fExpectInt("fallback: setting it is counted", UWFont.FallbackVersion, liVersion + 1);
            fExpectBool("fallback: an ideograph from it", lOPlain.TryGetGlyph('山', out UWFont.Glyph lOFrom) && fRows(lOFrom, 6) == fRows(lOIdeograph, 6), true);
            lOPlain.TryGetGlyph('山', out _);
            lOPlain.TryGetGlyph('ก', out _);
            lOPlain.TryGetGlyph('ก', out _);
            fExpectInt("fallback: asked once per character, a missing one too", lOSource.Asked, 2);
            fExpectBool("fallback: never for the font's own glyph", lOPlain.TryGetOwnGlyph('山', out _), false);
            fExpectInt("measure: two ideographs", lOPlain.MeasureText("山山", 0, UWTextLayout.CharacterSpacing), 12);

            List<string> lOLines = UWTextLayout.WrapText(lOPlain, "山山山山。山", 18);

            Check.Str("wrap: between ideographs, never before the full stop", string.Join("|", lOLines), "山山山|山。山");
            Check.Str("wrap: blanks and Latin words as before", string.Join("|", UWTextLayout.WrapText(lOPlain, " ab cd 山山ef", 24)),
                " ab cd|山山ef");

            UWPicture lOIdeographs = UWTextRaster.Render(lOPlain, new List<string> { "山" }, 8, null, UWTextRaster.ColoursEnum.Codes, false);

            fExpectBool("draw: the ideograph inked in the text's colour", lOIdeographs != null && lOIdeographs.Get(0, 0).A == 255
                && lOIdeographs.Get(4, 5).A == 255 && lOIdeographs.Get(5, 0).A == 0, true);

            UWFont.Fallback = new MadeUpGlyphSource();
            fExpectBool("fallback: a new one is asked anew", lOPlain.TryGetGlyph('ก', out _) == false && ((MadeUpGlyphSource)UWFont.Fallback).Asked == 1, true);

            UWFont.Fallback = null;
            fExpectBool("fallback: none - no ideograph", lOPlain.TryGetGlyph('山', out _), false);
        }

        private static bool fSamePixels(UWPicture pOA, UWPicture pOB)
        {
            if (pOA.Width != pOB.Width || pOA.Height != pOB.Height)
                return false;

            for (int liAt = 0; liAt < pOA.Pixels.Length; liAt++)
            {
                UWColor32 lOA = pOA.Pixels[liAt];
                UWColor32 lOB = pOB.Pixels[liAt];

                if (lOA.R != lOB.R || lOA.G != lOB.G || lOA.B != lOB.B || lOA.A != lOB.A)
                    return false;
            }

            return true;
        }
    }
}
