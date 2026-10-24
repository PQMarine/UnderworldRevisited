using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.4 (2026-10-19): the pixels of every picture the interface shows are made
    /// engine-free (UWUploadPixels) and Unity only uploads them (UWTextureFactory). Pinned: the row
    /// flip, both transparency rules (Keep: a transparent pixel keeps its colour and the colour help
    /// runs over it, as UWGameUI.fGetTextureInvert did; Clear: 0,0,0,0, as UWModernHudArt did), the
    /// keep mask (a mask of the wrong size changes nothing), and against the real data the icon
    /// indices and the eyes' index-0 keying as the old Unity loops made them.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckUploadPixels()
        {
            Check.Group("Upload pixels (UWUploadPixels - cleanup step 5.4)");

            // Two by two, rows top down: an opaque red and a transparent green on top, an opaque
            // blue and a transparent grey below.
            UWColor32[] lyTopDown =
            {
                new UWColor32(200, 0, 0, 255), new UWColor32(0, 200, 0, 0),
                new UWColor32(0, 0, 200, 255), new UWColor32(90, 90, 90, 0)
            };

            byte[] lyKeep = UWUploadPixels.FromColours(lyTopDown, 2, 2, null, UWUploadPixels.TransparentEnum.Keep);

            fExpectInt("upload: four bytes per pixel", lyKeep.Length, 16);
            fExpectBool("upload: the bottom row comes first (blue, then the grey)",
                lyKeep[2] == 200 && lyKeep[3] == 255 && lyKeep[4] == 90 && lyKeep[7] == 0, true);
            fExpectBool("upload: Keep leaves a transparent pixel its colour",
                lyKeep[12] == 0 && lyKeep[13] == 200 && lyKeep[15] == 0, true);

            byte[] lyClear = UWUploadPixels.FromColours(lyTopDown, 2, 2, null, UWUploadPixels.TransparentEnum.Clear);

            fExpectBool("upload: Clear makes a transparent pixel 0,0,0,0",
                lyClear[12] == 0 && lyClear[13] == 0 && lyClear[14] == 0 && lyClear[15] == 0
                && lyClear[4] == 0 && lyClear[5] == 0, true);
            fExpectBool("upload: Clear keeps the opaque ones", lyClear[8] == 200 && lyClear[11] == 255, true);

            UWColourFilter lOFilter = new UWColourFilter();

            lOFilter.Set(UWColourFilter.ModeEnum.Red, 1f, 1f, 1f);

            byte lyRed = 200;
            byte lyGreen = 0;
            byte lyBlue = 0;

            lOFilter.Apply(ref lyRed, ref lyGreen, ref lyBlue);

            byte[] lyFiltered = UWUploadPixels.FromColours(lyTopDown, 2, 2, lOFilter, UWUploadPixels.TransparentEnum.Keep);

            fExpectBool("upload: the colour help runs over the opaque red",
                lyFiltered[8] == lyRed && lyFiltered[9] == lyGreen && lyFiltered[10] == lyBlue, true);

            byte lyGreyRed = 90;
            byte lyGreyGreen = 90;
            byte lyGreyBlue = 90;

            lOFilter.Apply(ref lyGreyRed, ref lyGreyGreen, ref lyGreyBlue);

            fExpectBool("upload: under Keep it runs over the transparent ones too",
                lyFiltered[4] == lyGreyRed && lyFiltered[5] == lyGreyGreen && lyFiltered[6] == lyGreyBlue, true);

            byte[] lyFilteredClear = UWUploadPixels.FromColours(lyTopDown, 2, 2, lOFilter, UWUploadPixels.TransparentEnum.Clear);

            fExpectBool("upload: under Clear a transparent pixel stays 0,0,0,0 with the help on",
                lyFilteredClear[4] == 0 && lyFilteredClear[5] == 0 && lyFilteredClear[6] == 0, true);

            UWColourFilter lOOff = new UWColourFilter();

            fExpectBool("upload: an inactive filter changes nothing",
                System.Linq.Enumerable.SequenceEqual(UWUploadPixels.FromColours(lyTopDown, 2, 2, lOOff, UWUploadPixels.TransparentEnum.Keep),
                    lyKeep), true);

            // The keep mask, top down: keep only the top-left pixel (the red, bottom-up the third).
            byte[] lyMasked = UWUploadPixels.Keep(lyKeep, 2, 2, new[] { true, false, false, false });

            fExpectBool("upload: the mask keeps the top-left pixel", lyMasked[8] == 200 && lyMasked[11] == 255, true);
            fExpectBool("upload: and clears the bottom-left one", lyMasked[0] == 0 && lyMasked[2] == 0 && lyMasked[3] == 0, true);
            fExpectBool("upload: the mask works on a copy", lyKeep[2] == 200, true);
            fExpectBool("upload: a mask of the wrong size changes nothing",
                System.Linq.Enumerable.SequenceEqual(UWUploadPixels.Keep(lyKeep, 2, 2, new[] { false }), lyKeep), true);

            // The plate legs' drop (UWPaperdollView, 2026-10-19): one column, three rows bottom up
            // 1, 2, 3 moved down by one - 2, 3 and a cleared top row.
            byte[] lyColumn = { 1, 1, 1, 255, 2, 2, 2, 255, 3, 3, 3, 255 };
            byte[] lyMoved = UWUploadPixels.MoveDown(lyColumn, 1, 3, 1);

            fExpectBool("upload: moved down, the bottom row shows the one above it", lyMoved[0] == 2 && lyMoved[3] == 255, true);
            fExpectBool("upload: moved down, the middle row shows the top one", lyMoved[4] == 3, true);
            fExpectBool("upload: moved down, the top row is 0,0,0,0",
                lyMoved[8] == 0 && lyMoved[9] == 0 && lyMoved[10] == 0 && lyMoved[11] == 0, true);
            fExpectBool("upload: moving works on a copy", lyColumn[0] == 1, true);
            fExpectBool("upload: moving by no rows changes nothing",
                System.Linq.Enumerable.SequenceEqual(UWUploadPixels.MoveDown(lyColumn, 1, 3, 0), lyColumn), true);
            fExpectBool("upload: moving by more rows than the picture has clears it",
                System.Linq.Enumerable.SequenceEqual(UWUploadPixels.MoveDown(lyColumn, 1, 3, 5), new byte[12]), true);
        }

        private static void fCheckUploadPixelsData(DataImport pOData)
        {
            Check.Group("Upload pixels on the data (UWUploadPixels - cleanup step 5.4)");

            // An object icon (4-bit, auxiliary palette) as UWIconTextureBuilder built it.
            UWTexture lOIcon = pOData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, 232);
            UWColor32[] lyColours = lOIcon.GetUWColor32();
            byte[] lyIndices = lOIcon.GetMainPaletteIndices();
            byte[] lyIcon = UWUploadPixels.IconIndices(lOIcon);
            bool lbSame = lyIcon.Length == lOIcon.Width * lOIcon.Height * 4;
            int liOut = 0;

            for (int y = lOIcon.Height - 1; y >= 0 && lbSame; y--)
            {
                for (int x = 0; x < lOIcon.Width; x++)
                {
                    int liAt = (y * lOIcon.Width) + x;
                    byte lyIndex = lyIndices != null && liAt < lyIndices.Length ? lyIndices[liAt] : (byte)0;
                    bool lbOpaque = lyColours[liAt].A != 0;

                    lbSame &= lyIcon[liOut] == (lbOpaque ? lyIndex : 0) && lyIcon[liOut + 1] == 0 && lyIcon[liOut + 2] == 0
                        && lyIcon[liOut + 3] == (lbOpaque ? 255 : 0);
                    liOut += 4;
                }
            }

            fExpectBool("upload: a rune icon's indices and opacity as the icon builder made them", lbSame, true);

            // The eyes: index 0 transparent and black, the rest opaque in its colour.
            UWTexture lOEyes = pOData.Textures.GetTextureByType(UWTexture.TextureTypes.EYES, 0);
            UWColor32[] lyEyeColours = lOEyes.GetUWColor32();
            byte[] lyEyes = UWUploadPixels.FromIndexZeroKeyed(lOEyes, null);
            bool lbEyes = true;

            for (int y = 0; y < lOEyes.Height; y++)
            {
                for (int x = 0; x < lOEyes.Width; x++)
                {
                    int liSource = ((lOEyes.Height - 1 - y) * lOEyes.Width) + x;
                    int liAt = ((y * lOEyes.Width) + x) * 4;
                    bool lbClear = lOEyes.PaletteIndices[liSource] == 0;

                    lbEyes &= lbClear
                        ? lyEyes[liAt] == 0 && lyEyes[liAt + 1] == 0 && lyEyes[liAt + 2] == 0 && lyEyes[liAt + 3] == 0
                        : lyEyes[liAt] == lyEyeColours[liSource].R && lyEyes[liAt + 1] == lyEyeColours[liSource].G
                            && lyEyes[liAt + 2] == lyEyeColours[liSource].B && lyEyes[liAt + 3] == 255;
                }
            }

            fExpectBool("upload: the eyes keyed on index 0 as UWEyesDisplay made them", lbEyes, true);
        }
    }
}
