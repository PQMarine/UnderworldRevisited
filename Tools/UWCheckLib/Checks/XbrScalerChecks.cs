using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE TEXTURES' xBR SCALER (UWXbrScaler, per user 2026-11-22): twice the size per pass; a flat image stays
        /// flat; a hard diagonal edge gets half-blended corners on the edge and nowhere else; the textures repeat, so
        /// the right edge reads the left.
        /// </summary>
        private static void fCheckXbrScaler()
        {
            Check.Group("The textures' xBR scaler (level 1)");

            const uint Black = 0xFF000000u;
            const uint White = 0xFFFFFFFFu;
            const uint Grey = 0xFF808080u;

            uint[] lOFlat = new uint[16];

            for (int i = 0; i < lOFlat.Length; i++)
                lOFlat[i] = Grey;

            uint[] lOFlat4 = UWXbrScaler.Scale(lOFlat, 4, 4, 2);
            bool lbFlat = lOFlat4.Length == 256;

            foreach (uint luPixel in lOFlat4)
                lbFlat &= luPixel == Grey;

            Check.Bool("xbr: two passes give 4x and a flat image stays flat", lbFlat, true);

            // An 8x8 image, white below the diagonal x + y >= 8, black above it.
            uint[] lODiagonal = new uint[64];

            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    lODiagonal[y * 8 + x] = x + y >= 8 ? White : Black;

            uint[] lOScaled = UWXbrScaler.Scale2x(lODiagonal, 8, 8);
            int liBlended = 0;
            int liOffEdge = 0;

            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    uint luPixel = lOScaled[y * 16 + x];

                    if (luPixel != Black && luPixel != White)
                    {
                        liBlended++;

                        // A blended quarter lies at a source pixel next to the edge; the border rows and columns
                        // are left out, as the image repeats and meets itself there.
                        int liSum = (x / 2) + (y / 2);
                        bool lbInside = x / 2 > 0 && x / 2 < 7 && y / 2 > 0 && y / 2 < 7;

                        if (lbInside && (liSum < 6 || liSum > 9))
                            liOffEdge++;
                    }
                }

            Check.Bool("xbr: a diagonal edge is blended along it and nowhere else (" + liBlended + " blended, " + liOffEdge + " off the edge)",
                liBlended > 0 && liOffEdge == 0, true);

            // THE SPRITES (UWXbrScaler.ScaleSprite, per user 2026-11-22, the hard edge): a diamond of opaque grey on clear,
            // 9x9. Scaled 4x every pixel is shown or clear (no half opacity), the shown ones keep their colour or a mix of
            // greys only (no seam towards black), the middle stays shown, the corners of the picture stay clear, and the
            // steps of the outline are rounded: more shown pixels than the plain 4x, within one source pixel of it.
            const int Size = 9;
            uint[] lODiamond = new uint[Size * Size];

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    lODiamond[y * Size + x] = System.Math.Abs(x - 4) + System.Math.Abs(y - 4) <= 3 ? Grey : 0u;

            uint[] lOSprite = UWXbrScaler.ScaleSprite(lODiamond, Size, Size, 2);
            int liShown = 0;
            bool lbHard = lOSprite.Length == Size * Size * 16;
            bool lbNoSeam = true;

            foreach (uint luPixel in lOSprite)
            {
                uint luAlpha = luPixel >> 24;

                lbHard &= luAlpha == 0 || luAlpha == 255;

                if (luAlpha == 255)
                {
                    liShown++;
                    lbNoSeam &= luPixel == Grey;
                }
            }

            int liPlain = 0;

            foreach (uint luPixel in lODiamond)
                if (luPixel != 0)
                    liPlain += 16;

            int liWide = Size * 4;

            Check.Bool("xbr sprite: 4x, every pixel shown or clear", lbHard, true);
            Check.Bool("xbr sprite: no darker seam against the clear", lbNoSeam, true);
            Check.Bool("xbr sprite: the middle shown, the picture's corners clear",
                lOSprite[(liWide / 2) * liWide + liWide / 2] == Grey && lOSprite[0] == 0u && lOSprite[liWide * liWide - 1] == 0u, true);
            Check.Bool("xbr sprite: the outline's steps rounded (" + liShown + " shown against " + liPlain + " plain)",
                liShown != liPlain && System.Math.Abs(liShown - liPlain) < liPlain / 4, true);

            // A picture without clear pixels (a door) with its edges clamped: no corner rounds off into a hole.
            uint[] lOSolid = new uint[36];

            for (int i = 0; i < lOSolid.Length; i++)
                lOSolid[i] = (i % 6) < 3 ? Grey : White;

            bool lbWhole = true;

            foreach (uint luPixel in UWXbrScaler.ScaleSprite(lOSolid, 6, 6, 2, true))
                lbWhole &= luPixel >> 24 == 255;

            Check.Bool("xbr sprite: a picture without clear pixels, edges clamped, stays whole", lbWhole, true);

            // A figure with a translucent shadow under it (the second game's people): the diamond as above, its bottom row
            // replaced by a half-clear shadow. The figure is rounded as without the shadow, the shadow's pixels are there,
            // enlarged, where the figure leaves them.
            const uint Shadow = 0x80202020u;
            uint[] lOFigure = (uint[])lODiamond.Clone();

            for (int x = 0; x < Size; x++)
                lOFigure[(Size - 1) * Size + x] = Shadow;

            uint[] lOWithShadow = UWXbrScaler.ScaleSpriteKeepingTranslucent(lOFigure, Size, Size, 2);
            int liShadowPixels = 0;
            bool lbFigureKept = true;

            for (int i = 0; i < lOWithShadow.Length; i++)
            {
                if (lOWithShadow[i] == Shadow)
                    liShadowPixels++;
                else if (i < (Size - 1) * 4 * liWide)
                    lbFigureKept &= lOWithShadow[i] == lOSprite[i];
            }

            Check.Bool("xbr sprite: a translucent shadow enlarged beside the rounded figure (" + liShadowPixels + " shadow pixels)",
                lbFigureKept && liShadowPixels > 0 && liShadowPixels <= Size * 16, true);
        }
    }
}
