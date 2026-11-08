using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE WORDS PAINTED INTO THE PICTURES (UWPictureWords, Docs/PICTURE-WORDS-PLAN.md, 2026-11-08): English
    /// hands out every picture unchanged; the English words written anew over their own lettering give the
    /// original pictures back (where the original placed them by the rule); a translated text changes the
    /// picture and fits or is reported.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckPictureWords(DataImport pOData)
        {
            Check.Group("Words painted into the pictures (UWPictureWords - Docs/PICTURE-WORDS-PLAN.md)");

            UWPictureWords lOWords = pOData?.Textures?.Words;

            if (lOWords == null)
            {
                Check.Fail("picture words", "no textures or no words");

                return;
            }

            UWText.UseEnglish();
            fCheckPicturePack(pOData);

            // English: the same objects as the files hold.
            int liPictures = 0;
            List<string> lsChanged = new List<string>();

            foreach ((UWTexture.TextureTypes Type, int Index) lPicture in lOWords.Pictures)
            {
                liPictures++;

                if (!ReferenceEquals(pOData.Textures.GetTextureByType(lPicture.Type, lPicture.Index),
                        pOData.Textures.GetOriginalTextureByType(lPicture.Type, lPicture.Index)))
                    lsChanged.Add(lPicture.Type + " " + lPicture.Index.ToString("X2"));
            }

            Check.Str("English: every picture with words handed out as the files hold it (" + liPictures + " pictures)", string.Join(", ", lsChanged),
                string.Empty);

            // The English written anew over its own lettering: which pictures come back pixel for pixel.
            List<string> lsDiffer = new List<string>();
            int liSame = 0;

            foreach ((UWTexture.TextureTypes Type, int Index) lPicture in lOWords.Pictures)
            {
                UWTexture lOOriginal = pOData.Textures.GetOriginalTextureByType(lPicture.Type, lPicture.Index);
                UWTexture lOWritten = lOWords.Write(lOOriginal, lOWords.BoxesOf(lPicture.Type, lPicture.Index), UWText.English, psId => string.Empty);
                int liDiffering = 0;

                for (int liAt = 0; lOWritten != null && liAt < lOOriginal.PaletteIndices.Length; liAt++)
                {
                    if (lOWritten.PaletteIndices[liAt] != lOOriginal.PaletteIndices[liAt])
                        liDiffering++;
                }

                if (liDiffering == 0)
                    liSame++;
                else
                    lsDiffer.Add((lPicture.Type == UWTexture.TextureTypes.OPTBTNS ? string.Empty : lPicture.Type + "-") + lPicture.Index.ToString("X2") + ":"
                        + liDiffering);
            }

            lsDiffer.Sort(System.StringComparer.Ordinal);
            Check.Str("the English written anew: " + liSame + " of " + liPictures + " pictures pixel for pixel, the others as pinned (picture:pixels)",
                string.Join(" ", lsDiffer), msPictureWordsDiffering);

            // A translated text changes the picture, broken to fit the button.
            UWPictureWords.Box[] laSave = lOWords.BoxesOf(UWTexture.TextureTypes.OPTBTNS, 0x06);
            UWTexture lOSave = pOData.Textures.GetOriginalTextureByType(UWTexture.TextureTypes.OPTBTNS, 0x06);
            UWTexture lOGerman = lOWords.Write(lOSave, laSave, psId => "Spiel sichern", UWText.English);

            Check.Bool("a translated text: the picture changes", lOGerman != null && !ReferenceEquals(lOGerman, lOSave), true);
            Check.Str("... its lines", string.Join("|", lOWords.Lines("Spiel sichern", laSave[0].Width)), "SPIEL|SICHERN");
            Check.Bool("... it fits the button", lOWords.Fits(laSave[0], "Spiel sichern", out int _, out int _), true);
            Check.Bool("... a word too wide does not (SPEICHERN, 37 pixels in 29)", lOWords.Fits(laSave[0], "Spiel speichern", out int _, out int _), false);
            Check.Bool("... nor three lines in a button", lOWords.Fits(laSave[0], "Zurueck zum Spiel", out int _, out int _), false);

            // The German file's words fit their buttons.
            string lsRoot = CheckContext.ProjectRoot;
            string lsGerman = lsRoot == null ? null : System.IO.Path.Combine(lsRoot, "Languages", "de.tsv");

            if (lsGerman == null || !System.IO.File.Exists(lsGerman))
            {
                Check.Pass("German: the pictures' words fit", "skipped - no source tree");

                return;
            }

            UWText.UseLanguage("de", UWText.LoadLanguage("de", lsGerman));

            List<string> lsOver = new List<string>();
            int liMeasured = 0;

            foreach (UWClassicFit.Entry lOEntry in UWClassicFit.MeasurePictures(lOWords))
            {
                liMeasured++;

                if (lOEntry.IsOver)
                    lsOver.Add(lOEntry.Key + " \"" + lOEntry.Text + "\"");
            }

            UWText.UseEnglish();
            Check.Str("German: the pictures' words fit (" + liMeasured + " boxes measured)", string.Join(", ", lsOver), string.Empty);
            Check.Bool("... every word of the pictures has a German one", liMeasured >= 32, true);
        }

        /// <summary>A language's pictures for the checks: named files with their pixels.</summary>
        private sealed class FakePictureFiles : IUWPictureFiles
        {
            public readonly Dictionary<string, (int W, int H, UWColor32[] Pixels)> Files = new Dictionary<string, (int, int, UWColor32[])>();

            public bool Has(string psFileName)
            {
                return Files.ContainsKey(psFileName);
            }

            public bool TryRead(string psFileName, out int piWidth, out int piHeight, out UWColor32[] pyPixels)
            {
                bool lbFound = Files.TryGetValue(psFileName, out (int W, int H, UWColor32[] Pixels) lFile);

                piWidth = lFile.W;
                piHeight = lFile.H;
                pyPixels = lFile.Pixels;

                return lbFound;
            }
        }

        /// <summary>A LANGUAGE'S OWN PICTURES (UWPicturePack, step 3): the names as uwdump exports them, the
        /// PNG size from its header, the colours taken back to the picture's palette, and a picture of the
        /// language winning over the words written anew - one of another size left out.</summary>
        private static void fCheckPicturePack(DataImport pOData)
        {
            Check.Str("pack: names of a picture and a screen", UWPicturePack.FileName(UWTexture.TextureTypes.OPBTN, 0) + " "
                + UWPicturePack.FileName(UWTexture.TextureTypes.WIN1, 0) + " " + UWPicturePack.FileName(UWTexture.TextureTypes.OPTBTNS, 0x3C),
                "OPBTN_00.png WIN1.png OPTBTNS_3C.png");

            bool lbOne = UWPicturePack.TryParse("optbtns_3c.png", out UWTexture.TextureTypes leOne, out int liOne);
            bool lbTwo = UWPicturePack.TryParse("WIN1.png", out UWTexture.TextureTypes leTwo, out int liTwo);

            Check.Str("pack: names read back; a screen with a number, a name of nothing and no PNG refused",
                lbOne + " " + leOne + " " + liOne + " " + lbTwo + " " + leTwo + " " + liTwo + " " + UWPicturePack.TryParse("WIN1_00.png", out _, out _) + " "
                + UWPicturePack.TryParse("NOTHING_00.png", out _, out _) + " " + UWPicturePack.TryParse("OPBTN_00.bmp", out _, out _),
                "True OPTBTNS 60 True WIN1 0 False False False");

            byte[] lyHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 1, 0x40, 0, 0, 0, 0xC8 };

            Check.Str("pack: a PNG's size from its header", UWPicturePack.TryReadPngSize(lyHeader, out int liW, out int liH) + " " + liW + "x" + liH, "True 320x200");

            UWTexture lOMenu = pOData.Textures.GetOriginalTextureByType(UWTexture.TextureTypes.OPBTN, 2);
            UWColor32[] lyColours = lOMenu.GetUWColor32();
            byte[] lyBack = UWPicturePack.ToIndices(lOMenu, lyColours);
            UWColor32[] lyPalette = lOMenu.PaletteColours();
            int liDiffering = 0;

            for (int liAt = 0; liAt < lyColours.Length; liAt++)
            {
                UWColor32 lOBack = lyPalette[lyBack[liAt]];

                if (lyColours[liAt].A >= 128 && (lOBack.R != lyColours[liAt].R || lOBack.G != lyColours[liAt].G || lOBack.B != lyColours[liAt].B))
                    liDiffering++;
            }

            Check.Int("pack: the main menu's caption taken back to its palette, every colour its own", liDiffering, 0);

            // A picture of the language wins over the words; another size is left out.
            UWPictureWords lOWords = pOData.Textures.Words;
            FakePictureFiles lOFiles = new FakePictureFiles();
            UWColor32[] lyRed = new UWColor32[lOMenu.Width * lOMenu.Height];

            for (int liAt = 0; liAt < lyRed.Length; liAt++)
                lyRed[liAt] = new UWColor32(255, 0, 0, 255);

            lOFiles.Files["OPBTN_02.png"] = (lOMenu.Width, lOMenu.Height, lyRed);
            lOFiles.Files["OPBTN_03.png"] = (lOMenu.Width + 1, lOMenu.Height, lyRed);

            UWPictureWords.Files = lOFiles;

            try
            {
                UWTexture lOShown = pOData.Textures.GetTextureByType(UWTexture.TextureTypes.OPBTN, 2);
                UWTexture lOWrongSize = pOData.Textures.GetTextureByType(UWTexture.TextureTypes.OPBTN, 3);
                UWColor32[] lyShown = lOShown.GetUWColor32();
                bool lbUniform = System.Array.TrueForAll(lyShown, lOColour => lOColour.R == lyShown[0].R && lOColour.G == lyShown[0].G && lOColour.B == lyShown[0].B);

                Check.Str("pack: the language's picture shown (all red, so one palette colour); another size left as the original",
                    (!ReferenceEquals(lOShown, lOMenu)) + " " + lbUniform + " "
                    + ReferenceEquals(lOWrongSize, pOData.Textures.GetOriginalTextureByType(UWTexture.TextureTypes.OPBTN, 3)),
                    "True True True");
                Check.Bool("... and named in the problems", UWPictureWords.FileProblems.Exists(psProblem => psProblem.StartsWith("OPBTN_03.png")), true);
            }
            finally
            {
                UWPictureWords.Files = null;
            }

            Check.Bool("pack: none in force, the original again", ReferenceEquals(pOData.Textures.GetTextureByType(UWTexture.TextureTypes.OPBTN, 2), lOMenu), true);
        }

        /// <summary>The pictures the English written anew does not give back pixel for pixel, with the pixels
        /// that differ - PINNED (2026-11-08), so a change of the finding, painting or writing shows here. The
        /// original's lettering is not quite FONT4X5P: its O is three columns wide, not four, its blanks vary,
        /// some single lines sit a row higher (MUSIC) and the headings are spaced by hand; the backgrounds
        /// (01 to 05) hold several boxes. Looked at against the originals the same day: the same style.</summary>
        private const string msPictureWordsDiffering = "01:278 02:53 03:240 04:204 05:509 08:37 09:37 0A:72 0B:72 0C:21 0D:21 0E:82 0F:82 10:66 11:66 "
            + "14:10 15:10 16:10 17:10 1A:40 1B:40 1C:40 1D:40 26:22 27:22 2E:167 2F:146 30:153 31:127 32:95 33:147 34:167 35:152 36:100 37:164 38:164 "
            + "3B:31 3C:31 BLNKMAP-00:206 PANELS-02:170";
    }
}
