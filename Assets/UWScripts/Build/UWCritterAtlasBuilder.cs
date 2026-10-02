using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// Builds the image atlas of a critter animation (see UWCritterAtlas).
    ///
    /// All pages of the animation are built. Page 0 holds the motions that relate to
    /// the player (combat stance 0x00, the three attacks 0x01 to 0x03, walking towards the
    /// player 0x07, dying 0x0C), page 1 the eight facing directions standing
    /// (0x20 to 0x27) and walking (0x80 to 0x87). Both are needed: a creature that
    /// is not attacking the player does not stand in combat stance in the original, but faces
    /// its own direction.
    /// </summary>
    public static class UWCritterAtlasBuilder
    {
        private const int Padding = 2;

        private const int MaxAtlasSize = 2048;

        /// <summary>Main palette index 0 is the transparent colour everywhere in the game. This means
        /// the index AFTER the auxiliary palette, not the image index before it.</summary>
        private const byte TransparentIndex = 0;

        /// <summary>No animation has more pages; beyond that the search would only hit nothing.</summary>
        private const int MaxPages = 4;

        public static UWCritterAtlas Build(DataImport pOData, int piAnimation, int piAuxPalette, FilterMode peFilterMode)
        {
            if (pOData == null || pOData.CritterAnimations == null || !pOData.CritterAnimations.IsLoaded)
                return null;

            UWPalette lOPalette = pOData.Palettes.GetPalette(0);

            List<Texture2D> lOPieces = new List<Texture2D>();
            List<Texture2D> lOIndexPieces = new List<Texture2D>();
            List<int> lOFrameOfPiece = new List<int>();

            List<Vector2Int> lOSizes = new List<Vector2Int>();
            List<Vector2Int> lOHotspots = new List<Vector2Int>();

            UWCritterAtlas lOAtlas = new UWCritterAtlas();

            for (int liPage = 0; liPage < MaxPages; liPage++)
            {
                UWCritterAnimations.Page lOPage = pOData.CritterAnimations.GetPage(piAnimation, liPage);

                if (lOPage == null || lOPage.Frames == null || lOPage.Frames.Length == 0)
                    continue;

                byte[] lyAuxPalette = fGetAuxPalette(lOPage, piAuxPalette);

                if (lyAuxPalette == null)
                    continue;

                // The frame numbers in the segments restart from zero per page - in the atlas
                // the pages lie one after another, hence the offset.
                int liFrameOffset = lOSizes.Count;

                for (int liFrame = 0; liFrame < lOPage.Frames.Length; liFrame++)
                {
                    UWCritterAnimations.Frame lOFrame = lOPage.Frames[liFrame];

                    lOSizes.Add(lOFrame == null ? Vector2Int.zero : new Vector2Int(lOFrame.Width, lOFrame.Height));
                    lOHotspots.Add(lOFrame == null ? Vector2Int.zero : new Vector2Int(lOFrame.HotspotX, lOFrame.HotspotY));

                    if (lOFrame == null || lOFrame.Width <= 0 || lOFrame.Height <= 0 || lOFrame.Pixels == null)
                        continue;

                    bool lbTranslucent;

                    Texture2D lOIndexPiece;

                    Texture2D lOPiece = fCreatePiece(pOData, lOFrame, lyAuxPalette, lOPalette, out lbTranslucent, out lOIndexPiece);

                    lOAtlas.IsTranslucent |= lbTranslucent;
                    lOAtlas.MaxSize = new Vector2Int(Mathf.Max(lOAtlas.MaxSize.x, lOFrame.Width),
                        Mathf.Max(lOAtlas.MaxSize.y, lOFrame.Height));

                    lOPieces.Add(lOPiece);
                    lOIndexPieces.Add(lOIndexPiece);
                    lOFrameOfPiece.Add(liFrameOffset + liFrame);
                }

                fAddSlots(lOAtlas, lOPage, liFrameOffset);
            }

            if (lOPieces.Count == 0)
                return null;

            Texture2D lOTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            lOTexture.name = string.Format("UWCritter {0} Palette {1}", piAnimation, piAuxPalette);

            Rect[] lORects = lOTexture.PackTextures(lOPieces.ToArray(), Padding, MaxAtlasSize, false);

            lOTexture.filterMode = peFilterMode;
            lOTexture.wrapMode = TextureWrapMode.Clamp;
            lOTexture.Apply(false, false);

            lOAtlas.Sizes = lOSizes.ToArray();
            lOAtlas.Hotspots = lOHotspots.ToArray();
            lOAtlas.UvRects = new Rect[lOSizes.Count];

            for (int i = 0; i < lOFrameOfPiece.Count && i < lORects.Length; i++)
                lOAtlas.UvRects[lOFrameOfPiece[i]] = lORects[i];

            lOAtlas.IndexTexture = UWIndexAtlas.Assemble(lOTexture, lOIndexPieces, lORects,
                lOTexture.name + " (Indices)");

            for (int i = 0; i < lOPieces.Count; i++)
                Object.DestroyImmediate(lOPieces[i]);

            for (int i = 0; i < lOIndexPieces.Count; i++)
                Object.DestroyImmediate(lOIndexPieces[i]);

            lOAtlas.Texture = lOTexture;

            return lOAtlas;
        }

        /// <summary>
        /// Resolves the slots of a page: the slot points to a segment, the
        /// segment holds up to eight frame numbers, padded with 0xFF.
        /// </summary>
        private static void fAddSlots(UWCritterAtlas pOAtlas, UWCritterAnimations.Page pOPage, int piFrameOffset)
        {
            if (pOPage.SlotSegments == null || pOPage.Segments == null)
                return;

            for (int liSlot = 0; liSlot < pOPage.SlotSegments.Length; liSlot++)
            {
                byte lySegment = pOPage.SlotSegments[liSlot];

                if (lySegment == 0xFF || lySegment >= pOPage.Segments.Length)
                    continue;

                byte[] lyFrames = pOPage.Segments[lySegment];

                if (lyFrames == null)
                    continue;

                List<int> lOFrames = new List<int>(lyFrames.Length);

                foreach (byte lyFrame in lyFrames)
                {
                    if (lyFrame == 0xFF)
                        break;

                    lOFrames.Add(piFrameOffset + lyFrame);
                }

                if (lOFrames.Count == 0)
                    continue;

                pOAtlas.SlotFrames[pOPage.SlotBase + liSlot] = lOFrames.ToArray();
            }
        }

        /// <summary>
        /// The auxiliary palette of a creature. It selects from the page's palettes; if the
        /// requested one does not exist, the first is taken - better wrong colours than no image.
        /// </summary>
        private static byte[] fGetAuxPalette(UWCritterAnimations.Page pOPage, int piAuxPalette)
        {
            if (pOPage.AuxPalettes == null || pOPage.AuxPalettes.Length == 0)
                return null;

            if (piAuxPalette >= 0 && piAuxPalette < pOPage.AuxPalettes.Length && pOPage.AuxPalettes[piAuxPalette] != null)
                return pOPage.AuxPalettes[piAuxPalette];

            return pOPage.AuxPalettes[0];
        }

        private static Texture2D fCreatePiece(DataImport pOData, UWCritterAnimations.Frame pOFrame, byte[] pyAuxPalette, UWPalette pOPalette, out bool pbTranslucent, out Texture2D pOIndexPiece)
        {
            pbTranslucent = false;

            Texture2D lOPiece = new Texture2D(pOFrame.Width, pOFrame.Height, TextureFormat.RGBA32, false);
            Color32[] lOPixels = new Color32[pOFrame.Width * pOFrame.Height];

            // The same image with the palette index in the red channel and the opacity in the
            // alpha channel - for the palette renderer, in the same pass.
            Color32[] lOIndexPixels = new Color32[pOFrame.Width * pOFrame.Height];

            for (int y = 0; y < pOFrame.Height; y++)
            {
                // Row 0 of the source is at the top, Unity starts at the bottom.
                int liSourceRow = (pOFrame.Height - 1 - y) * pOFrame.Width;
                int liTargetRow = y * pOFrame.Width;

                for (int x = 0; x < pOFrame.Width; x++)
                {
                    int liSource = liSourceRow + x;

                    if (liSource >= pOFrame.Pixels.Length)
                        continue;

                    byte lyIndex = pOFrame.Pixels[liSource];

                    // Transparent is NOT image index 0, but the pixel whose
                    // auxiliary palette entry points to main palette index 0. For creatures that
                    // is image index 1: the goblin palette starts with 1, 0, 229, ... -
                    // so index 0 points to main colour 1, and that is black, namely the
                    // outline of the figure. The first attempt threw away exactly this outline
                    // and so punched holes into every creature (per user, 2026-08-30).
                    int liMainIndex = lyIndex < pyAuxPalette.Length ? pyAuxPalette[lyIndex] : 0;

                    if (liMainIndex == TransparentIndex)
                    {
                        lOPixels[liTargetRow + x] = new Color32(0, 0, 0, 0);
                        lOIndexPixels[liTargetRow + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Translucent pixels - exactly the case that uw-formats.txt describes for
                    // creatures (ghosts). See UWTransparencyTables.
                    UWTransparencyTables.FadeTable leFade;

                    if (UWTransparencyTables.TryGetFadeTable(liMainIndex, out leFade))
                    {
                        UWColor32 lOBlend;
                        float lfAlpha;

                        if (pOData.TransparencyTables != null && pOData.TransparencyTables.IsLoaded
                            && pOData.TransparencyTables.TryGetBlend(leFade, pOPalette, out lOBlend, out lfAlpha))
                        {
                            byte lyAlpha = (byte)Mathf.Clamp(Mathf.RoundToInt(lfAlpha * 255f), 0, 255);

                            lOPixels[liTargetRow + x] = new Color32(lOBlend.R, lOBlend.G, lOBlend.B, lyAlpha);

                            // In the index path there is no blended colour, only palette slots -
                            // the nearest one is taken (see
                            // UWPalette.GetNearestIndex). GREEN carries the table number plus
                            // one, for the palette renderer to apply the real table to the
                            // background (UWBillboardPalette, since 2026-09-27).
                            lOIndexPixels[liTargetRow + x] = new Color32(
                                pOPalette.GetNearestIndex(lOBlend.R, lOBlend.G, lOBlend.B),
                                (byte)(1 + (int)leFade), 0, lyAlpha);

                            pbTranslucent = true;
                            continue;
                        }
                    }

                    UWColor32 lOColour = pOPalette.GetUWColor(liMainIndex);

                    lOPixels[liTargetRow + x] = new Color32(lOColour.R, lOColour.G, lOColour.B, 255);
                    lOIndexPixels[liTargetRow + x] = new Color32((byte)liMainIndex, 0, 0, 255);
                }
            }

            lOPiece.SetPixels32(lOPixels);
            lOPiece.Apply(false, false);

            // linear = true: this holds an index, not a colour.
            pOIndexPiece = new Texture2D(pOFrame.Width, pOFrame.Height, TextureFormat.RGBA32, false, true);
            pOIndexPiece.SetPixels32(lOIndexPixels);
            pOIndexPiece.Apply(false, false);

            return lOPiece;
        }
    }
}
