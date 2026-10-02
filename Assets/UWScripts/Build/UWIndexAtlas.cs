using System.Collections.Generic;
using UnityEngine;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// Assembles the matching index atlas for a packed colour atlas - red channel the
    /// palette index, alpha channel the opacity. For the palette renderer, see
    /// UWShadePalette.
    ///
    /// THE SAME PACKING PASS. The index pieces are placed at exactly the spots the
    /// packing pass found for the colour pieces. That way the same UV rectangles apply to
    /// both atlases, and switching the render path is a pure texture swap - no
    /// recomputing of UVs, no risk that a second packing pass arranges things differently.
    ///
    /// Shared by the object and creature atlas, because both need the same
    /// procedure.
    /// </summary>
    public static class UWIndexAtlas
    {
        /// <summary>
        /// Builds the index atlas. pOPacked is the fully packed colour atlas, pORects are
        /// its UV rectangles in the same order as pOIndexPieces.
        ///
        /// LINEAR, not sRGB, and point filtered - otherwise the index turns into a
        /// gamma-corrected in-between colour.
        /// </summary>
        public static Texture2D Assemble(Texture2D pOPacked, List<Texture2D> pOIndexPieces,
            Rect[] pORects, string psName)
        {
            if (pOPacked == null || pOIndexPieces == null || pORects == null)
                return null;

            Texture2D lOAtlas = new Texture2D(pOPacked.width, pOPacked.height,
                TextureFormat.RGBA32, false, true);

            lOAtlas.name = psName;
            lOAtlas.filterMode = FilterMode.Point;
            lOAtlas.wrapMode = TextureWrapMode.Clamp;

            // Empty space between the graphics stays transparent.
            lOAtlas.SetPixels32(new Color32[pOPacked.width * pOPacked.height]);

            bool lbWarned = false;

            for (int liAt = 0; liAt < pOIndexPieces.Count && liAt < pORects.Length; liAt++)
            {
                Texture2D lOPiece = pOIndexPieces[liAt];

                if (lOPiece == null)
                    continue;

                int liWidth = Mathf.RoundToInt(pORects[liAt].width * pOPacked.width);
                int liHeight = Mathf.RoundToInt(pORects[liAt].height * pOPacked.height);

                // If the set does not fit into the permitted size, PackTextures shrinks
                // the pieces. Then the mapping no longer matches, and flying blind would be
                // worse than an empty area.
                if (liWidth != lOPiece.width || liHeight != lOPiece.height)
                {
                    if (!lbWarned)
                    {
                        Debug.LogWarning(psName
                            + ": packing pass shrank graphics - index atlas stays empty at these spots.");

                        lbWarned = true;
                    }

                    continue;
                }

                lOAtlas.SetPixels32(Mathf.RoundToInt(pORects[liAt].x * pOPacked.width),
                    Mathf.RoundToInt(pORects[liAt].y * pOPacked.height),
                    liWidth, liHeight, lOPiece.GetPixels32());
            }

            lOAtlas.Apply(false, false);

            return lOAtlas;
        }
    }
}
