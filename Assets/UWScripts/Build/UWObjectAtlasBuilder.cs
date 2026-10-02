using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// Packs the object graphics from OBJECTS.GR, DOORS.GR, TMOBJ.GR and TMFLAT.GR into one
    /// shared texture.
    /// </summary>
    public static class UWObjectAtlasBuilder
    {
        /// <summary>Border between the graphics so that nothing bleeds in when filtering.</summary>
        private const int Padding = 2;

        private const int MaxAtlasSize = 4096;

        /// <summary>Edge length of the 256 palette colour swatches (UWObjectAtlas.PaletteSwatchSource).
        /// Larger than 1x1 so that nothing bleeds in from the neighbouring pixel even with
        /// bilinear filtering, in case TextureFilterMode is ever not Point.</summary>
        private const int PaletteSwatchSize = 4;

        /// <summary>
        /// Palette index for pure black. NOT zero: index 0 is (0,0,4), i.e. the
        /// transparency marker (see UWPalette). Black is at 1.
        ///
        /// Needed where the colour path writes literal black instead of a
        /// palette colour - for doors, whose border and joints are black.
        /// </summary>
        private const byte BlackPaletteIndex = 1;

        /// <summary>Palette index for white (252,252,252). For the reference swatch of the
        /// Gouraud faces.</summary>
        private const byte WhitePaletteIndexInPalette = 11;

        public static UWObjectAtlas Build(DataImport pOData, FilterMode peFilterMode = FilterMode.Point)
        {
            // The cache is valid per data set - discard it on rebuild.
            mOBlendCache = null;

            List<Texture2D> lOPieces = new List<Texture2D>();
            List<Texture2D> lOIndexPieces = new List<Texture2D>();
            List<UWObjectAtlas.Entry> lOEntries = new List<UWObjectAtlas.Entry>();

            for (int s = 0; s < UWObjectAtlas.PackedSources.Length; s++)
            {
                UWTexture.TextureTypes leSource = UWObjectAtlas.PackedSources[s];
                List<UWTexture> lOTextures = fGetTextures(pOData, leSource);

                if (lOTextures == null)
                    continue;

                for (int i = 0; i < lOTextures.Count; i++)
                {
                    UWTexture lOTexture = lOTextures[i];

                    if (lOTexture == null)
                        continue;

                    Vector2Int lOOpaqueMin;
                    Vector2Int lOOpaqueSize;

                    bool lbTranslucent;

                    Texture2D lOIndexPiece;

                    Texture2D lOPiece = fCreatePiece(pOData, lOTexture, leSource, out lOOpaqueMin, out lOOpaqueSize, out lbTranslucent, out lOIndexPiece);

                    Vector2Int lOSolidPixel = fFindSolidPixel(lOPiece);

                    UWObjectAtlas.Entry lOEntry = new UWObjectAtlas.Entry();
                    lOEntry.Source = (int)leSource;
                    lOEntry.Index = lOTexture.Index;
                    lOEntry.Size = new Vector2Int(lOPiece.width, lOPiece.height);
                    lOEntry.OpaqueMin = lOOpaqueMin;
                    lOEntry.OpaqueSize = lOOpaqueSize;
                    lOEntry.SolidPixel = lOSolidPixel;
                    lOEntry.IsTranslucent = lbTranslucent;

                    lOPieces.Add(lOPiece);
                    lOIndexPieces.Add(lOIndexPiece);
                    lOEntries.Add(lOEntry);
                }
            }

            fAddPaletteSwatches(pOData, lOPieces, lOIndexPieces, lOEntries);

            Texture2D lOAtlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            lOAtlas.name = "UWObjectAtlas";

            Rect[] lORects = lOAtlas.PackTextures(lOPieces.ToArray(), Padding, MaxAtlasSize, false);

            UWObjectAtlas lOResult = ScriptableObject.CreateInstance<UWObjectAtlas>();
            lOResult.name = "UWObjectAtlas";

            UWObjectAtlas.Entry[] lOFinal = lOEntries.ToArray();

            for (int i = 0; i < lOFinal.Length && i < lORects.Length; i++)
                lOFinal[i].UvRect = lORects[i];

            lOResult.Entries = lOFinal;

            lOAtlas.filterMode = peFilterMode;
            lOAtlas.wrapMode = TextureWrapMode.Clamp;
            lOAtlas.Apply(false, false);

            lOResult.IndexAtlas = UWIndexAtlas.Assemble(lOAtlas, lOIndexPieces, lORects,
                "UWObjectAtlas (indices)");

            for (int i = 0; i < lOPieces.Count; i++)
                Object.DestroyImmediate(lOPieces[i]);

            for (int i = 0; i < lOIndexPieces.Count; i++)
                Object.DestroyImmediate(lOIndexPieces[i]);

            lOResult.Atlas = lOAtlas;

            return lOResult;
        }

        /// <summary>
        /// Also packs all 256 entries of the main palette (palette 0, the same one the object
        /// graphics already use) into the atlas as single-colour swatches. Needed for the
        /// single-colour faces of the baked-in 3D models (shrine/ankh, boulder, ...), which
        /// show a real palette colour in the original instead of a pixel from a sprite
        /// graphic - see UW3DModelImport (colour records from UW.EXE).
        /// </summary>
        private static void fAddPaletteSwatches(DataImport pOData, List<Texture2D> pOPieces, List<Texture2D> pOIndexPieces, List<UWObjectAtlas.Entry> pOEntries)
        {
            UWPalette lOPalette = pOData.Palettes.GetPalette(0);

            for (int p = 0; p < 256; p++)
            {
                UWColor32 lOColor = lOPalette.GetUWColor(p);
                fAddSwatch(pOPieces, pOIndexPieces, pOEntries, p, new Color32(lOColor.R, lOColor.G, lOColor.B, 255), (byte)p);
            }

            // Neutral white swatch beyond the 256 real palette colours (index
            // UWObjectAtlas.WhitePaletteIndex) - reference pixel for Gouraud faces (see
            // UWObjectSpawner.fSpawn3DModel): they carry their actual colour (mapped via
            // LIGHT.DAT) as vertex colour instead of as UV, because two different
            // palette swatches per triangle would otherwise need a UV linearly interpolated
            // between them - it would run straight through the atlas and let completely unrelated
            // neighbouring graphics show through. With a constant white UV the
            // multiplication with the vertex colour yields exactly its value, without any UV interpolation.
            // OPEN for the palette renderer: the Gouraud faces carry their colour as
            // vertex colour and multiply it with this white swatch. An index cannot
            // be multiplied - in the palette path they need a path of their own,
            // e.g. the brightness level per vertex instead of the colour. Until then this holds
            // the brightest palette index so that the face at least does not turn black.
            fAddSwatch(pOPieces, pOIndexPieces, pOEntries, UWObjectAtlas.WhitePaletteIndex, new Color32(255, 255, 255, 255), WhitePaletteIndexInPalette);
        }

        private static void fAddSwatch(List<Texture2D> pOPieces, List<Texture2D> pOIndexPieces, List<UWObjectAtlas.Entry> pOEntries, int piIndex, Color32 pOColor, byte pyPaletteIndex)
        {
            Texture2D lOSwatch = new Texture2D(PaletteSwatchSize, PaletteSwatchSize, TextureFormat.RGBA32, false);
            Color32[] lOSwatchPixels = new Color32[PaletteSwatchSize * PaletteSwatchSize];

            for (int i = 0; i < lOSwatchPixels.Length; i++)
                lOSwatchPixels[i] = pOColor;

            lOSwatch.SetPixels32(lOSwatchPixels);
            lOSwatch.Apply(false, false);

            Texture2D lOIndexSwatch = new Texture2D(PaletteSwatchSize, PaletteSwatchSize, TextureFormat.RGBA32, false, true);
            Color32[] lOIndexPixels = new Color32[PaletteSwatchSize * PaletteSwatchSize];
            Color32 lOIndexPixel = new Color32(pyPaletteIndex, 0, 0, 255);

            for (int i = 0; i < lOIndexPixels.Length; i++)
                lOIndexPixels[i] = lOIndexPixel;

            lOIndexSwatch.SetPixels32(lOIndexPixels);
            lOIndexSwatch.Apply(false, false);

            UWObjectAtlas.Entry lOEntry = new UWObjectAtlas.Entry();
            lOEntry.Source = UWObjectAtlas.PaletteSwatchSource;
            lOEntry.Index = piIndex;
            lOEntry.Size = new Vector2Int(PaletteSwatchSize, PaletteSwatchSize);
            lOEntry.OpaqueMin = Vector2Int.zero;
            lOEntry.OpaqueSize = lOEntry.Size;
            lOEntry.SolidPixel = Vector2Int.zero;

            pOPieces.Add(lOSwatch);
            pOIndexPieces.Add(lOIndexSwatch);
            pOEntries.Add(lOEntry);
        }

        private static List<UWTexture> fGetTextures(DataImport pOData, UWTexture.TextureTypes peSource)
        {
            try
            {
                return pOData.Textures.GetTexturesByType(peSource);
            }
            catch
            {
                Debug.LogWarning("UWObjectAtlas: texture source " + peSource + " not available.");
                return null;
            }
        }

        /// <summary>
        /// The index in the main palette. For 4-bit graphics PaletteIndices holds the
        /// 4-bit value, which only points to the main palette via the auxiliary palette - and only
        /// there can a transparency trigger be recognised.
        /// </summary>
        private static int fGetMainPaletteIndex(byte[] pyIndices, int piSource, UWAuxPalette pOAuxPalette)
        {
            if (pyIndices == null || piSource < 0 || piSource >= pyIndices.Length)
                return -1;

            if (pOAuxPalette == null)
                return pyIndices[piSource];

            return pOAuxPalette.GetMainPaletteIndex(pyIndices[piSource]);
        }

        /// <summary>Overlay colour and opacity of a lookup table, computed once per
        /// table (see UWTransparencyTables.TryGetBlend).</summary>
        private static bool fTryGetBlend(DataImport pOData, UWTransparencyTables.FadeTable peTable, out UWColor32 pOColour, out float pfAlpha)
        {
            pOColour = default(UWColor32);
            pfAlpha = 0f;

            if (pOData == null || pOData.TransparencyTables == null || !pOData.TransparencyTables.IsLoaded)
                return false;

            int liTable = (int)peTable;

            if (mOBlendCache == null)
                mOBlendCache = new Dictionary<int, KeyValuePair<UWColor32, float>>();

            KeyValuePair<UWColor32, float> lOCached;

            if (mOBlendCache.TryGetValue(liTable, out lOCached))
            {
                pOColour = lOCached.Key;
                pfAlpha = lOCached.Value;

                return pfAlpha > 0f;
            }

            UWColor32 lOColour;
            float lfAlpha;

            bool lbOk = pOData.TransparencyTables.TryGetBlend(peTable, pOData.Palettes.GetPalette(0), out lOColour, out lfAlpha);

            if (!lbOk)
                lfAlpha = 0f;

            mOBlendCache[liTable] = new KeyValuePair<UWColor32, float>(lOColour, lfAlpha);

            pOColour = lOColour;
            pfAlpha = lfAlpha;

            return lbOk;
        }

        /// <summary>Overlay colour and opacity per lookup table - the least-squares fit
        /// runs over 256 entries and should not be repeated per pixel.</summary>
        private static Dictionary<int, KeyValuePair<UWColor32, float>> mOBlendCache;

        /// <summary>
        /// Sources in which palette index 0 does not mean transparency but
        /// black colour. Door textures have a black border and black joints
        /// between the planks; rendered as transparent, you can see through the door.
        /// </summary>
        private static bool fIsSolidSource(UWTexture.TextureTypes peSource)
        {
            return peSource == UWTexture.TextureTypes.DOORS;
        }

        /// <summary>
        /// Sources whose graphics are stored mirrored. For doors the hinges
        /// would otherwise be on the side where the door does not pivot at all.
        /// </summary>
        private static bool fIsMirroredSource(UWTexture.TextureTypes peSource)
        {
            return peSource == UWTexture.TextureTypes.DOORS;
        }

        /// <summary>
        /// Converts a graphic into a Texture2D and measures its extent at the same time.
        ///
        /// The rows are flipped because row 0 of the source is at the top, while Unity
        /// starts at the bottom.
        ///
        /// The measured outline comes from the pixels with an index other than 0 and is
        /// then expanded by one pixel - for doors the black border still belongs
        /// to the door leaf. This turns 51 measured rows into the 52
        /// units the door is actually tall.
        /// </summary>
        private static Texture2D fCreatePiece(DataImport pOData, UWTexture pOTexture, UWTexture.TextureTypes peSource, out Vector2Int pOOpaqueMin, out Vector2Int pOOpaqueSize, out bool pbTranslucent, out Texture2D pOIndexPiece)
        {
            pbTranslucent = false;

            int liWidth = Mathf.Max(1, pOTexture.Width);
            int liHeight = Mathf.Max(1, pOTexture.Height);

            bool lbSolid = fIsSolidSource(peSource);
            bool lbMirrored = fIsMirroredSource(peSource);

            Texture2D lOPiece = new Texture2D(liWidth, liHeight, TextureFormat.RGBA32, false);
            Color32[] lOPixels = new Color32[liWidth * liHeight];

            // The same image once more, but with the palette index in the red channel and the
            // opacity in the alpha channel - for the palette renderer. Both are created in the
            // same pass so that the decoding runs only once.
            Color32[] lOIndexPixels = new Color32[liWidth * liHeight];

            UWColor32[] lOColors = null;
            byte[] lyIndices = null;

            try
            {
                lOColors = pOTexture.GetUWColor32();
                lyIndices = pOTexture.PaletteIndices;
            }
            catch
            {
                lOColors = null;
            }

            // Translucent pixels: in the original, certain palette indices do not overwrite the
            // background but tint it (XFER.DAT, see
            // UWTransparencyTables). The mist cloud consists exclusively of such
            // pixels - without this handling it becomes a dark red blob, because the
            // trigger index is then read as an ordinary palette colour.
            UWAuxPalette lOAuxPalette = null;

            if (pOData != null && pOTexture.AuxPaletteIndex != 255)
            {
                try
                {
                    lOAuxPalette = pOData.Palettes.GetAuxPalette(pOTexture.AuxPaletteIndex);
                }
                catch
                {
                    lOAuxPalette = null;
                }
            }

            int liMinX = liWidth;
            int liMaxX = -1;
            int liMinY = liHeight;
            int liMaxY = -1;

            if (lOColors != null)
            {
                for (int y = 0; y < liHeight; y++)
                {
                    int liSourceRow = (liHeight - 1 - y) * liWidth;
                    int liTargetRow = y * liWidth;

                    for (int x = 0; x < liWidth; x++)
                    {
                        int liSource = liSourceRow + (lbMirrored ? liWidth - 1 - x : x);

                        if (liSource >= lOColors.Length)
                            continue;

                        bool lbIndexZero = lyIndices != null && liSource < lyIndices.Length && lyIndices[liSource] == 0;

                        if (!lbIndexZero)
                        {
                            if (x < liMinX) liMinX = x;
                            if (x > liMaxX) liMaxX = x;
                            if (y < liMinY) liMinY = y;
                            if (y > liMaxY) liMaxY = y;
                        }

                        if (lbIndexZero)
                        {
                            lOPixels[liTargetRow + x] = lbSolid
                                ? new Color32(0, 0, 0, 255)
                                : new Color32(0, 0, 0, 0);

                            // Black is index 1, not 0 - zero carries the
                            // transparency marker (see BlackPaletteIndex).
                            lOIndexPixels[liTargetRow + x] = lbSolid
                                ? new Color32(BlackPaletteIndex, 0, 0, 255)
                                : new Color32(0, 0, 0, 0);

                            continue;
                        }

                        int liMainIndex = fGetMainPaletteIndex(lyIndices, liSource, lOAuxPalette);

                        UWTransparencyTables.FadeTable leFade;

                        if (UWTransparencyTables.TryGetFadeTable(liMainIndex, out leFade))
                        {
                            UWColor32 lOBlendColour;
                            float lfAlpha;

                            if (fTryGetBlend(pOData, leFade, out lOBlendColour, out lfAlpha))
                            {
                                byte lyAlpha = (byte)Mathf.Clamp(Mathf.RoundToInt(lfAlpha * 255f), 0, 255);

                                lOPixels[liTargetRow + x] = new Color32(lOBlendColour.R, lOBlendColour.G,
                                    lOBlendColour.B, lyAlpha);

                                // The index path has no blended colour - there are only
                                // palette slots. The nearest one is taken so that
                                // it looks as before. This is an approximation: the
                                // original computes the blend on what is already in the
                                // image (XFER.DAT), and would land on a different slot
                                // depending on the background. GREEN carries the table
                                // number plus one: the palette renderer applies the table
                                // to the real background itself (UWBillboardPalette, since
                                // 2026-09-27); red and alpha remain its fallback.
                                lOIndexPixels[liTargetRow + x] = new Color32(
                                    pOData.Palettes.GetPalette(0).GetNearestIndex(
                                        lOBlendColour.R, lOBlendColour.G, lOBlendColour.B),
                                    (byte)(1 + (int)leFade), 0, lyAlpha);

                                pbTranslucent = true;
                                continue;
                            }
                        }

                        UWColor32 lOColor = lOColors[liSource];
                        lOPixels[liTargetRow + x] = new Color32(lOColor.R, lOColor.G, lOColor.B, 255);

                        lOIndexPixels[liTargetRow + x] = new Color32(
                            (byte)Mathf.Clamp(liMainIndex, 0, 255), 0, 0, 255);
                    }
                }
            }

            lOPiece.SetPixels32(lOPixels);
            lOPiece.Apply(false, false);

            // linear = true: this holds an index, not a colour.
            pOIndexPiece = new Texture2D(liWidth, liHeight, TextureFormat.RGBA32, false, true);
            pOIndexPiece.SetPixels32(lOIndexPixels);
            pOIndexPiece.Apply(false, false);

            if (liMaxX < 0)
            {
                pOOpaqueMin = Vector2Int.zero;
                pOOpaqueSize = new Vector2Int(liWidth, liHeight);
                return lOPiece;
            }

            // Expand by one pixel so that the black border is included.
            liMinX = Mathf.Max(0, liMinX - 1);
            liMinY = Mathf.Max(0, liMinY - 1);
            liMaxX = Mathf.Min(liWidth - 1, liMaxX + 1);
            liMaxY = Mathf.Min(liHeight - 1, liMaxY + 1);

            pOOpaqueMin = new Vector2Int(liMinX, liMinY);
            pOOpaqueSize = new Vector2Int(liMaxX - liMinX + 1, liMaxY - liMinY + 1);

            return lOPiece;
        }

        /// <summary>
        /// Finds a pixel with the most frequent colour of the graphic. Faces that are meant
        /// to be single-coloured get all their corners placed on exactly this pixel.
        /// </summary>
        private static Vector2Int fFindSolidPixel(Texture2D pOPiece)
        {
            Color32[] lOPixels = pOPiece.GetPixels32();

            Dictionary<int, int> lOCounts = new Dictionary<int, int>();

            int liBestKey = -1;
            int liBestCount = 0;

            for (int i = 0; i < lOPixels.Length; i++)
            {
                Color32 lOPixel = lOPixels[i];

                if (lOPixel.a == 0)
                    continue;

                int liKey = (lOPixel.r << 16) | (lOPixel.g << 8) | lOPixel.b;

                int liCount;
                lOCounts.TryGetValue(liKey, out liCount);
                liCount++;
                lOCounts[liKey] = liCount;

                if (liCount > liBestCount)
                {
                    liBestCount = liCount;
                    liBestKey = liKey;
                }
            }

            if (liBestKey < 0)
                return Vector2Int.zero;

            for (int i = 0; i < lOPixels.Length; i++)
            {
                Color32 lOPixel = lOPixels[i];

                if (lOPixel.a == 0)
                    continue;

                if (((lOPixel.r << 16) | (lOPixel.g << 8) | lOPixel.b) == liBestKey)
                    return new Vector2Int(i % pOPiece.width, i / pOPiece.width);
            }

            return Vector2Int.zero;
        }
    }
}
