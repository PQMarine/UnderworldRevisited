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

                    Texture2D lOPiece = fCreatePiece(pOData, lOFrame.Width, lOFrame.Height, lOFrame.Pixels, lyAuxPalette, lOPalette,
                        out lbTranslucent, out lOIndexPiece);

                    lOAtlas.IsTranslucent |= lbTranslucent;
                    lOAtlas.MaxSize = new Vector2Int(Mathf.Max(lOAtlas.MaxSize.x, lOFrame.Width),
                        Mathf.Max(lOAtlas.MaxSize.y, lOFrame.Height));

                    lOPieces.Add(lOPiece);
                    lOIndexPieces.Add(lOIndexPiece);
                    lOFrameOfPiece.Add(liFrameOffset + liFrame);
                }

                fAddSlots(lOAtlas, lOPage, liFrameOffset);
            }

            return fPack(lOAtlas, lOPieces, lOIndexPieces, lOFrameOfPiece, lOSizes, lOHotspots,
                string.Format("UWCritter {0} Palette {1}", piAnimation, piAuxPalette), peFilterMode);
        }

        /// <summary>
        /// THE SECOND GAME'S CREATURE ART (UW2 plan phase 2 step 3, 2026-10-29): every frame of a critter
        /// map from its pages (UWCritterArtUw2), in one atlas whose image numbers ARE the map's frame
        /// numbers (0 to 255, the numbers CR.AN's sequences name); its slots are sequence x 8 + view, each
        /// the sequence's frames in that view up to its count (byte 7). The pixels go through the page's
        /// palette piPalette, as the first game's through its auxiliary palette.
        /// </summary>
        public static UWCritterAtlas BuildUw2(DataImport pOData, int piMap, int piPalette, FilterMode peFilterMode)
        {
            UWCritterArtUw2 lOArt = pOData != null && pOData.GameData != null ? pOData.GameData.CritterArtUw2 : null;

            if (lOArt == null || !lOArt.IsLoaded)
                return null;

            UWPalette lOPalette = pOData.Palettes.GetPalette(0);
            UWCritterAtlas lOAtlas = new UWCritterAtlas();
            List<Texture2D> lOPieces = new List<Texture2D>();
            List<Texture2D> lOIndexPieces = new List<Texture2D>();
            List<int> lOFrameOfPiece = new List<int>();
            Vector2Int[] lOSizes = new Vector2Int[UWCritterArtUw2.FrameNumbers];
            Vector2Int[] lOHotspots = new Vector2Int[UWCritterArtUw2.FrameNumbers];

            for (int liPage = 0; liPage < UWCritterArtUw2.PagesPerMap; liPage++)
            {
                UWCritterArtUw2.Page lOPage = lOArt.GetPage(piMap, liPage);

                if (lOPage == null)
                    continue;

                byte[] lyPalette = piPalette >= 0 && piPalette < lOPage.Palettes.Length ? lOPage.Palettes[piPalette] : lOPage.Palettes[0];

                foreach (KeyValuePair<int, UWCritterArtUw2.Frame> lOPair in lOPage.Frames)
                {
                    UWCritterArtUw2.Frame lOFrame = lOPair.Value;

                    // The frame from the page the renderer takes it from (PageOfFrame), once.
                    if (lOArt.PageOfFrame(piMap, lOPair.Key) != liPage || lOFrame.Pixels == null || lOFrame.Width <= 0 || lOFrame.Height <= 0)
                        continue;

                    lOSizes[lOPair.Key] = new Vector2Int(lOFrame.Width, lOFrame.Height);
                    lOHotspots[lOPair.Key] = new Vector2Int(lOFrame.HotspotX, lOFrame.HotspotY);

                    Texture2D lOPiece = fCreatePiece(pOData, lOFrame.Width, lOFrame.Height, lOFrame.Pixels, lyPalette, lOPalette,
                        out bool lbTranslucent, out Texture2D lOIndexPiece);

                    lOAtlas.IsTranslucent |= lbTranslucent;
                    lOAtlas.MaxSize = new Vector2Int(Mathf.Max(lOAtlas.MaxSize.x, lOFrame.Width), Mathf.Max(lOAtlas.MaxSize.y, lOFrame.Height));

                    lOPieces.Add(lOPiece);
                    lOIndexPieces.Add(lOIndexPiece);
                    lOFrameOfPiece.Add(lOPair.Key);
                }
            }

            for (int liSequence = 0; liSequence < UWCritterArtUw2.SequenceCount; liSequence++)
            {
                for (int liView = 0; liView < UWCritterArtUw2.AngleCount; liView++)
                {
                    byte[] lySteps = lOArt.SequenceBytes(piMap, liSequence, liView);
                    int liLength = Mathf.Min(lySteps[UWCritterArtUw2.SequenceLengthByte], UWCritterArtUw2.SequenceLengthByte);
                    List<int> lOFrames = new List<int>(liLength);

                    for (int liStep = 0; liStep < liLength && lySteps[liStep] != UWCritterArtUw2.NoPicture; liStep++)
                        lOFrames.Add(lySteps[liStep]);

                    if (lOFrames.Count > 0)
                        lOAtlas.SlotFrames[Uw2Slot(liSequence, liView)] = lOFrames.ToArray();
                }
            }

            return fPack(lOAtlas, lOPieces, lOIndexPieces, lOFrameOfPiece, new List<Vector2Int>(lOSizes), new List<Vector2Int>(lOHotspots),
                string.Format("UWCritter UW2 map {0} Palette {1}", piMap, piPalette), peFilterMode);
        }

        /// <summary>The atlas slot of a second-game sequence in a view.</summary>
        public static int Uw2Slot(int piSequence, int piView)
        {
            return (piSequence * UWCritterArtUw2.AngleCount) + piView;
        }

        /// <summary>Packs the pieces into the atlas's texture and index texture (both games).</summary>
        private static UWCritterAtlas fPack(UWCritterAtlas lOAtlas, List<Texture2D> lOPieces, List<Texture2D> lOIndexPieces,
            List<int> lOFrameOfPiece, List<Vector2Int> lOSizes, List<Vector2Int> lOHotspots, string psName, FilterMode peFilterMode)
        {
            if (lOPieces.Count == 0)
                return null;

            Texture2D lOTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            lOTexture.name = psName;

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

        private static Texture2D fCreatePiece(DataImport pOData, int piWidth, int piHeight, byte[] pyFramePixels, byte[] pyAuxPalette, UWPalette pOPalette, out bool pbTranslucent, out Texture2D pOIndexPiece)
        {
            pbTranslucent = false;

            Texture2D lOPiece = new Texture2D(piWidth, piHeight, TextureFormat.RGBA32, false);
            Color32[] lOPixels = new Color32[piWidth * piHeight];

            // The same image with the palette index in the red channel and the opacity in the
            // alpha channel - for the palette renderer, in the same pass.
            Color32[] lOIndexPixels = new Color32[piWidth * piHeight];

            for (int y = 0; y < piHeight; y++)
            {
                // Row 0 of the source is at the top, Unity starts at the bottom.
                int liSourceRow = (piHeight - 1 - y) * piWidth;
                int liTargetRow = y * piWidth;

                for (int x = 0; x < piWidth; x++)
                {
                    int liSource = liSourceRow + x;

                    if (liSource >= pyFramePixels.Length)
                        continue;

                    byte lyIndex = pyFramePixels[liSource];

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
            pOIndexPiece = new Texture2D(piWidth, piHeight, TextureFormat.RGBA32, false, true);
            pOIndexPiece.SetPixels32(lOIndexPixels);
            pOIndexPiece.Apply(false, false);

            return lOPiece;
        }
    }
}
