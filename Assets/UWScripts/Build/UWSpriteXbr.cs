using System.Collections.Generic;
using UnityEngine;
using UWDataImport.Presentation;
using UWDataImport.UWData;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// THE SPRITES' xBR (per user 2026-11-22 after comparison sheets: "Harter Rand. Der weiche Rand wirkt unscharf."): the
    /// pieces of the creature and object atlases scaled before packing, the colour by UWXbrScaler.ScaleSprite (round
    /// outline, hard edge), the index piece beside it by plain enlargement - so the palette path, which reads only the
    /// indices, draws exactly as before. Translucent pixels (a shadow, a ghost, the mist) are only enlarged. The atlases
    /// keep their sizes in original pixels (UWObjectAtlas.Entry.Size, UWCritterAtlas.Sizes) and their places as 0..1
    /// rectangles, so a scaled piece needs nothing else.
    /// </summary>
    public static class UWSpriteXbr
    {
        /// <summary>Two passes, 4x, as the textures.</summary>
        public const int Passes = 2;

        /// <summary>Scales pieces from piFirst on, colour and index alike, on every core; the old textures are destroyed.
        /// Translucent pixels are enlarged as they are beside the opaque part's xBR (UWXbrScaler.ScaleSpriteKeepingTranslucent;
        /// per user 2026-11-22, the second game's people with their shadows stayed pixelated while such pieces were left);
        /// a piece without clear pixels has its edges clamped.</summary>
        public static void ScalePieces(List<Texture2D> pOPieces, List<Texture2D> pOIndexPieces, int piPasses, int piFirst = 0)
        {
            if (piPasses <= 0 || pOPieces == null)
                return;

            int liCount = pOPieces.Count;
            uint[][] luyColour = new uint[liCount][];
            bool[] lbSolid = new bool[liCount];
            Color32[][] lOIndex = new Color32[liCount][];
            int[] liWidth = new int[liCount];
            int[] liHeight = new int[liCount];

            // Unity's reads on this thread.
            for (int i = piFirst; i < liCount; i++)
            {
                Texture2D lOPiece = pOPieces[i];

                if (lOPiece == null || !lOPiece.isReadable)
                    continue;

                Color32[] lOPixels = lOPiece.GetPixels32();

                luyColour[i] = new uint[lOPixels.Length];

                for (int liAt = 0; liAt < lOPixels.Length; liAt++)
                    luyColour[i][liAt] = ((uint)lOPixels[liAt].a << 24) | ((uint)lOPixels[liAt].b << 16) | ((uint)lOPixels[liAt].g << 8)
                        | lOPixels[liAt].r;

                lbSolid[i] = fNoClear(lOPixels);
                liWidth[i] = lOPiece.width;
                liHeight[i] = lOPiece.height;

                Texture2D lOIndexPiece = pOIndexPieces != null && i < pOIndexPieces.Count ? pOIndexPieces[i] : null;

                lOIndex[i] = lOIndexPiece != null && lOIndexPiece.isReadable ? lOIndexPiece.GetPixels32() : null;
            }

            int liScale = 1 << piPasses;

            System.Threading.Tasks.Parallel.For(piFirst, liCount, i =>
            {
                if (luyColour[i] == null)
                    return;

                luyColour[i] = UWXbrScaler.ScaleSpriteKeepingTranslucent(luyColour[i], liWidth[i], liHeight[i], piPasses, lbSolid[i]);

                if (lOIndex[i] != null)
                    lOIndex[i] = fEnlarged(lOIndex[i], liWidth[i], liHeight[i], liScale);
            });

            for (int i = piFirst; i < liCount; i++)
            {
                if (luyColour[i] == null)
                    continue;

                int liW = liWidth[i] * liScale;
                int liH = liHeight[i] * liScale;
                Color32[] lOPixels = new Color32[luyColour[i].Length];

                for (int liAt = 0; liAt < lOPixels.Length; liAt++)
                {
                    uint luPixel = luyColour[i][liAt];

                    lOPixels[liAt] = new Color32((byte)luPixel, (byte)(luPixel >> 8), (byte)(luPixel >> 16), (byte)(luPixel >> 24));
                }

                pOPieces[i] = fReplaced(pOPieces[i], liW, liH, lOPixels, false);

                if (lOIndex[i] != null)
                    pOIndexPieces[i] = fReplaced(pOIndexPieces[i], liW, liH, lOIndex[i], true);
            }
        }

        /// <summary>THE WEAPON IN THE HAND (per user 2026-11-22: "Die Waffen dann jetzt auch xBR."): scaled while the sprites'
        /// xBR was taken at the load (UWLevelLoader.SpriteScalePasses) and Remastered is the mode now - the palette modes
        /// keep the original's pixels, as they do for creatures and things.</summary>
        public static int WeaponPasses =>
            UWLevelLoader.SpriteScalePasses > 0 && UWGraphicsDetail.Mode == UWGraphicsDetail.ModeEnum.Remastered ? UWLevelLoader.SpriteScalePasses : 0;

        /// <summary>A game picture as UWTextureFactory.FromTexture makes it (the colour help applied), scaled piPasses times; 0
        /// gives FromTexture's own. The scaled one is not followed by the colour help in place - its owner rebuilds on the
        /// colour stamp (UWArtCache), as UWWeaponView does.</summary>
        public static Texture2D FromTexture(UWTexture pOTexture, FilterMode peFilter, string psName, int piPasses)
        {
            if (piPasses <= 0)
                return UWTextureFactory.FromTexture(pOTexture, peFilter, psName);

            byte[] lyRgba = UWUploadPixels.FromTexture(pOTexture, UWColourVision.Filter);
            int liScale = 1 << piPasses;

            return UWTextureFactory.Upload(ScaleRgba(lyRgba, pOTexture.Width, pOTexture.Height, piPasses), pOTexture.Width * liScale,
                pOTexture.Height * liScale, peFilter, TextureWrapMode.Repeat, psName + " (xBR)");
        }

        /// <summary>RGBA bytes (4 per pixel, alpha 0 or 255 or translucent) scaled as the atlas pieces are; the rows in
        /// either order (the rule is the same upside down). piPasses 0 returns them as they are.</summary>
        public static byte[] ScaleRgba(byte[] pyRgba, int piWidth, int piHeight, int piPasses)
        {
            if (piPasses <= 0 || pyRgba == null)
                return pyRgba;

            int liCount = piWidth * piHeight;
            uint[] luySource = new uint[liCount];
            bool lbSolid = true;

            for (int liAt = 0; liAt < liCount; liAt++)
            {
                int liByte = liAt * 4;

                luySource[liAt] = ((uint)pyRgba[liByte + 3] << 24) | ((uint)pyRgba[liByte + 2] << 16) | ((uint)pyRgba[liByte + 1] << 8)
                    | pyRgba[liByte];
                lbSolid &= pyRgba[liByte + 3] != 0;
            }

            uint[] luyScaled = UWXbrScaler.ScaleSpriteKeepingTranslucent(luySource, piWidth, piHeight, piPasses, lbSolid);
            byte[] lyOut = new byte[luyScaled.Length * 4];

            for (int liAt = 0; liAt < luyScaled.Length; liAt++)
            {
                uint luPixel = luyScaled[liAt];
                int liByte = liAt * 4;

                lyOut[liByte] = (byte)luPixel;
                lyOut[liByte + 1] = (byte)(luPixel >> 8);
                lyOut[liByte + 2] = (byte)(luPixel >> 16);
                lyOut[liByte + 3] = (byte)(luPixel >> 24);
            }

            return lyOut;
        }

        /// <summary>A picture without clear pixels (a door, a model's texture): its edges are clamped, not rounded off.</summary>
        private static bool fNoClear(Color32[] pOPixels)
        {
            foreach (Color32 lOPixel in pOPixels)
                if (lOPixel.a == 0)
                    return false;

            return true;
        }

        private static Color32[] fEnlarged(Color32[] pOPixels, int piWidth, int piHeight, int piScale)
        {
            int liWidth = piWidth * piScale;
            Color32[] lOOut = new Color32[liWidth * piHeight * piScale];

            for (int y = 0; y < piHeight * piScale; y++)
                for (int x = 0; x < liWidth; x++)
                    lOOut[y * liWidth + x] = pOPixels[(y / piScale) * piWidth + (x / piScale)];

            return lOOut;
        }

        /// <summary>A new piece at the scaled size with the old one's name and filter, linear as the index pieces are.</summary>
        private static Texture2D fReplaced(Texture2D pOOld, int piWidth, int piHeight, Color32[] pOPixels, bool pbLinear)
        {
            Texture2D lONew = new Texture2D(piWidth, piHeight, TextureFormat.RGBA32, false, pbLinear);

            lONew.name = pOOld.name;
            lONew.filterMode = pOOld.filterMode;
            lONew.wrapMode = pOOld.wrapMode;
            lONew.SetPixels32(pOPixels);
            lONew.Apply(false, false);
            Object.DestroyImmediate(pOOld);

            return lONew;
        }
    }
}
