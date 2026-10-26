using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UWDataImport.Install;
using UWDataImport.UWData;

/// <summary>
/// THE LANGUAGE'S FONT IN UNITY (translation wave 6, 2026-10-26): what the engine-free self-check
/// (LanguageFont) cannot see - UWFallbackFont rasterising through TextCore and the modern fonts falling
/// back through Unity's dynamic fonts. With the made-up language xz of Tools/Fixtures/i18n in force
/// (its font list: xz.otf, which is nowhere, then os:Microsoft JhengHei for the ideographs and os:Arial
/// for the Cyrillic, which Windows 11 has):
///   - the list as UWFallbackFont found it (the file left out, the family loaded);
///   - every character of xz's texts beyond ASCII drawn in a made-up six-row font with an H: a glyph of
///     the font's rows with ink, monochrome; ASCII the font's own;
///   - the modern font (Lexend Exa) gets the family behind its own name and draws an ideograph with it
///     (its character info equals that of the family made as an OS font);
///   - English again: no fallback, Lexend Exa's own names back.
/// Without Microsoft JhengHei (another system) the checks that need it are skipped, not failed.
///
/// UW_GAME=&lt;the GOG installation or game.gog&gt; adds the player's FONT5X6P and FONTBIG and, with
/// UW_FONT_PREVIEW_OUT=&lt;folder&gt;, a preview PNG of xz's texts in each (four times enlarged) - for the
/// eye; nothing of it is kept in the project.
///
/// Menu: UW / Check font fallback. Batch: -executeMethod UWFontFallbackCheck.Run (exit 1 on a failure).
/// The language in force before is put back; nothing is saved.
/// </summary>
public static class UWFontFallbackCheck
{
    private const string Fixture = "Tools/Fixtures/i18n/xz.tsv";

    private const string Family = "Microsoft JhengHei";

    private static int miFailures;

    private static readonly StringBuilder msReport = new StringBuilder();

    [MenuItem("UW/Check font fallback")]
    public static void Run()
    {
        miFailures = 0;
        msReport.Length = 0;

        try
        {
            fRun();
        }
        catch (Exception lOError)
        {
            fFail("the check threw - " + lOError);
        }
        finally
        {
            UWText.UseEnglish();
            UWTextLine.UseEnglish();
            UWFallbackFont.Apply();
        }

        string lsReport = "Font fallback: " + (miFailures == 0 ? "every check passed" : miFailures + " failed") + "\n" + msReport;

        if (miFailures == 0)
            Debug.Log(lsReport);
        else
            Debug.LogError(lsReport);

        if (Application.isBatchMode)
            EditorApplication.Exit(miFailures == 0 ? 0 : 1);
    }

    private static void fRun()
    {
        string lsRoot = Directory.GetParent(Application.dataPath).FullName;
        string lsFixture = Path.Combine(lsRoot, Fixture.Replace('/', Path.DirectorySeparatorChar));
        bool lbInstalled = Array.IndexOf(Font.GetOSInstalledFontNames(), Family) >= 0;

        UWTextLanguageFile lOFile = UWText.LoadLanguage("xz", lsFixture);

        fExpect("xz read without a skipped line", lOFile != null && lOFile.Problems.Count == 0, lOFile == null ? "unreadable" : string.Join("; ", lOFile.Problems));

        UWTextLine.UseLanguage(UWGrammar.For("xz"));
        UWFallbackFont.Apply();

        fExpect("xz: the input method wanted", UWTextLine.ImeOn, UWTextLine.ImeOn.ToString());

        string lsFound = UWFallbackFont.Describe();

        fNote("the list as found: " + lsFound);
        fExpect("xz.otf left out (no font file in the project)", lsFound.Contains("xz.otf (not found)"), lsFound);

        if (!lbInstalled)
        {
            fNote(Family + " is not installed here - the rasteriser and the modern fallback are not checked");

            return;
        }

        fExpect(Family + " loaded", lsFound.Contains("os:" + Family) && !lsFound.Contains("os:" + Family + " (not found)"), lsFound);

        // The classic fonts: every character of xz's texts beyond ASCII from the language's font.
        StringBuilder lOCharacters = new StringBuilder();

        foreach (string lsText in lOFile.Texts.Values)
        {
            foreach (char lcChar in lsText)
            {
                if (lcChar > '~' && lOCharacters.ToString().IndexOf(lcChar) < 0)
                    lOCharacters.Append(lcChar);
            }
        }

        UWFont lOMadeUp = fMadeUpFont();
        List<string> lsBad = new List<string>();

        foreach (char lcChar in lOCharacters.ToString())
        {
            if (!lOMadeUp.TryGetGlyph(lcChar, out UWFont.Glyph lOGlyph))
                lsBad.Add("U+" + ((int)lcChar).ToString("X4") + " not in the language's fonts");
            else if (lOGlyph.Width <= 0 || lOGlyph.Pixels.Length != lOGlyph.Width * lOMadeUp.Height || fNotMonochrome(lOGlyph.Pixels))
                lsBad.Add("U+" + ((int)lcChar).ToString("X4") + " malformed");
            else if (Array.IndexOf(lOGlyph.Pixels, (byte)1) < 0)
                lsBad.Add("U+" + ((int)lcChar).ToString("X4") + " without ink, " + lOGlyph.Width + " wide");
        }

        fExpect("made-up 6-row font: " + lOCharacters.Length + " characters of xz drawn from the list, inked, monochrome",
            lsBad.Count == 0, string.Join(", ", lsBad));
        fExpect("made-up font: ASCII its own", lOMadeUp.TryGetGlyph('A', out UWFont.Glyph lOA) && lOMadeUp.TryGetOwnGlyph('A', out UWFont.Glyph lOOwnA)
            && lOA.Pixels == lOOwnA.Pixels, "A");

        foreach (char lcChar in "山、Б")
        {
            lOMadeUp.TryGetGlyph(lcChar, out UWFont.Glyph lOShown);
            fNote("U+" + ((int)lcChar).ToString("X4") + " rasterised at " + UWFallbackGlyphs.StartSize(lOMadeUp, 0.8f) + " (about): "
                + UWFallbackFont.DescribeRaster(lcChar, UWFallbackGlyphs.StartSize(lOMadeUp, 0.8f)) + "\nin the made-up font:\n" + fRows(lOShown, lOMadeUp.Height));
        }

        // The modern font: the family behind Lexend Exa's own name, and an ideograph drawn with it.
        Font lOModern = UWInterfaceFont.Modern;

        if (lOModern == null)
        {
            fFail("no Lexend Exa under Resources");

            return;
        }

        fExpect("Lexend Exa falls back to " + Family, Array.IndexOf(lOModern.fontNames, Family) > 0, string.Join(", ", lOModern.fontNames));

        Font lOOs = Font.CreateDynamicFontFromOSFont(Family, 64);
        string lsLexend = fInfo(lOModern, '雲', 64);
        string lsOs = fInfo(lOOs, '雲', 64);

        fExpect("Lexend Exa draws U+96F2 as " + Family + " does (" + lsOs + ")", lsLexend == lsOs, lsLexend);

        fPreview(lOFile);

        UWText.UseEnglish();
        UWTextLine.UseEnglish();
        UWFallbackFont.Apply();

        fExpect("English again: no fallback", UWFont.Fallback == null && !lOMadeUp.TryGetGlyph('山', out _), "still one");
        fExpect("English again: Lexend Exa's own names", lOModern.fontNames.Length > 0 && Array.IndexOf(lOModern.fontNames, Family) < 0,
            string.Join(", ", lOModern.fontNames));
        fExpect("English again: no input method", !UWTextLine.ImeOn, "on");
    }

    /// <summary>A six-row font of 128 blank characters, four wide, with an H of five rows (the original's
    /// font5x6p has its capitals so) - made up, nothing of the game.</summary>
    private static UWFont fMadeUpFont()
    {
        const int liHeight = 6;
        byte[] lyData = new byte[12 + (128 * (liHeight + 1))];
        string[] lsH = { "#...#.", "#...#.", "#####.", "#...#.", "#...#.", "......" };

        lyData[0] = 1;
        lyData[2] = liHeight;
        lyData[4] = 3;
        lyData[6] = liHeight;
        lyData[8] = 1;
        lyData[10] = 6;

        for (int liCode = 0; liCode < 128; liCode++)
            lyData[12 + (liCode * (liHeight + 1)) + liHeight] = 4;

        int liOffset = 12 + ('H' * (liHeight + 1));

        for (int y = 0; y < liHeight; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                if (lsH[y][x] == '#')
                    lyData[liOffset + y] |= (byte)(0x80 >> x);
            }
        }

        lyData[liOffset + liHeight] = 6;

        return new UWFont(lyData);
    }

    private static bool fNotMonochrome(byte[] pyPixels)
    {
        foreach (byte lyPixel in pyPixels)
        {
            if (lyPixel > 1)
                return true;
        }

        return false;
    }

    private static string fInfo(Font pOFont, char pcChar, int piSize)
    {
        pOFont.RequestCharactersInTexture(pcChar.ToString(), piSize);

        if (!pOFont.GetCharacterInfo(pcChar, out CharacterInfo lOInfo, piSize))
            return "none";

        return "advance " + lOInfo.advance + ", x " + lOInfo.minX + ".." + lOInfo.maxX + ", y " + lOInfo.minY + ".." + lOInfo.maxY;
    }

    private static string fRows(UWFont.Glyph pOGlyph, int piHeight)
    {
        StringBuilder lORows = new StringBuilder();

        for (int y = 0; y < piHeight && pOGlyph.Pixels != null; y++)
        {
            for (int x = 0; x < pOGlyph.Width; x++)
                lORows.Append(pOGlyph.Pixels[(y * pOGlyph.Width) + x] != 0 ? '#' : '.');

            lORows.Append('\n');
        }

        return lORows.ToString();
    }

    /// <summary>xz's texts in the player's FONT5X6P and FONTBIG (UW_GAME), as PNGs in UW_FONT_PREVIEW_OUT.</summary>
    private static void fPreview(UWTextLanguageFile pOFile)
    {
        string lsImage = UWGogInstall.FindImage(Environment.GetEnvironmentVariable("UW_GAME"));
        string lsOut = Environment.GetEnvironmentVariable("UW_FONT_PREVIEW_OUT");

        if (lsImage == null)
        {
            fNote("UW_GAME not set or no game.gog there - the player's fonts not tried");

            return;
        }

        foreach (UWFonts.FontType leType in new[] { UWFonts.FontType.Normal, UWFonts.FontType.Big })
        {
            byte[] lyBytes = UWGogInstall.ReadDataFile(lsImage, UWFonts.FileName(leType));

            if (lyBytes == null)
            {
                fFail(UWFonts.FileName(leType) + " not in " + lsImage);

                continue;
            }

            UWFont lOFont = new UWFont(lyBytes);
            List<string> lsLines = new List<string>();

            foreach (KeyValuePair<string, string> lOPair in pOFile.Texts)
            {
                if (!lOPair.Key.StartsWith("grammar.rule.", StringComparison.Ordinal))
                    lsLines.AddRange(UWTextLayout.WrapText(lOFont, lOPair.Value.Replace("{0}", "Hxy"), 160));
            }

            UWPicture lOPicture = UWTextRaster.Render(lOFont, lsLines, 170, new[] { new UWColor32(60, 40, 20, 255) },
                UWTextRaster.ColoursEnum.Codes, false);

            fExpect(UWFonts.FileName(leType) + ": xz's texts drawn, " + lsLines.Count + " lines", lOPicture != null, "nothing drawn");

            if (lOPicture == null || string.IsNullOrEmpty(lsOut))
                continue;

            const int liScale = 4;
            Texture2D lOTexture = new Texture2D(lOPicture.Width * liScale, lOPicture.Height * liScale, TextureFormat.RGBA32, false);
            Color32[] lyPixels = new Color32[lOTexture.width * lOTexture.height];

            for (int y = 0; y < lOTexture.height; y++)
            {
                for (int x = 0; x < lOTexture.width; x++)
                {
                    UWColor32 lOColour = lOPicture.Get(x / liScale, y / liScale);

                    // Paper behind the ink; the texture's rows run bottom-up.
                    lyPixels[((lOTexture.height - 1 - y) * lOTexture.width) + x] = lOColour.A > 0
                        ? new Color32(lOColour.R, lOColour.G, lOColour.B, 255) : new Color32(222, 205, 170, 255);
                }
            }

            lOTexture.SetPixels32(lyPixels);
            lOTexture.Apply();
            Directory.CreateDirectory(lsOut);

            string lsFile = Path.Combine(lsOut, "xz-" + leType + ".png");

            File.WriteAllBytes(lsFile, ImageConversion.EncodeToPNG(lOTexture));
            UnityEngine.Object.DestroyImmediate(lOTexture);
            fNote("preview " + lsFile);
        }
    }

    private static void fExpect(string psName, bool pbOk, string psDetail)
    {
        if (pbOk)
            msReport.Append("  OK   ").Append(psName).Append('\n');
        else
            fFail(psName + ": " + psDetail);
    }

    private static void fFail(string psWhat)
    {
        miFailures++;
        msReport.Append("  FAIL ").Append(psWhat).Append('\n');
    }

    private static void fNote(string psWhat)
    {
        msReport.Append("  note ").Append(psWhat).Append('\n');
    }
}
