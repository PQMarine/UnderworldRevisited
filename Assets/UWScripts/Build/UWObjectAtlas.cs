using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// Object graphics in one shared texture, so that all objects of a level
    /// can use the same material.
    ///
    /// Addressed by texture source and index, not by object id. Objects
    /// take their graphics from different files: doors from DOORS.GR,
    /// switches from TMFLAT.GR, pillars and decorations from TMOBJ.GR or OBJECTS.GR.
    /// Access by object id hits the wrong entry in all these cases.
    ///
    /// Wall and floor textures are deliberately not included - they already live in the
    /// Texture2DArray of the dungeon geometry.
    /// </summary>
    public sealed class UWObjectAtlas : ScriptableObject
    {
        /// <summary>Texture files that end up in the atlas.</summary>
        public static readonly UWTexture.TextureTypes[] PackedSources = new UWTexture.TextureTypes[]
        {
            UWTexture.TextureTypes.OBJECTS,
            // ANIMO.GR holds the frames of the animated objects (fountain, fire,
            // silver tree) - which frames belong to which object is stated in the
            // animation table of OBJECTS.DAT (UWObjectClassProperties.AnimationObject).
            UWTexture.TextureTypes.ANIMO,
            UWTexture.TextureTypes.DOORS,
            UWTexture.TextureTypes.TMOBJ,
            UWTexture.TextureTypes.TMFLAT
        };

        [System.Serializable]
        public struct Entry
        {
            public int Source;
            public int Index;
            public Rect UvRect;
            public Vector2Int Size;

            /// <summary>Lower left corner of the opaque area, in pixels.</summary>
            public Vector2Int OpaqueMin;

            /// <summary>Size of the opaque area, in pixels.</summary>
            public Vector2Int OpaqueSize;

            /// <summary>
            /// Pixel with the most frequent colour of the graphic. Needed where a surface
            /// should be a single colour - e.g. the narrow sides of a door leaf, which in the
            /// original are plain brown instead of showing the door graphic.
            /// </summary>
            public Vector2Int SolidPixel;

            /// <summary>The graphic contains pixels that tint the background instead of
            /// overwriting it (XFER.DAT, see UWTransparencyTables) - so it must be
            /// blended instead of drawn with a hard alpha threshold.</summary>
            public bool IsTranslucent;
        }

        /// <summary>
        /// Reserved Source value for the 256 palette colour swatches (see
        /// UWObjectAtlasBuilder) that carry the real palette colours of the baked-in 3D models
        /// (UW3DModelImport colour records) - negative, so that it never collides with a real
        /// TextureTypes value (always >= 0).
        /// </summary>
        public const int PaletteSwatchSource = -1;

        /// <summary>Index of a neutral white swatch beyond the 256 real
        /// palette colours (see UWObjectAtlasBuilder.fAddPaletteSwatches) - reference pixel
        /// for Gouraud surfaces that carry their colour as vertex colour instead of UV, see
        /// UWObjectSpawner.fSpawn3DModel.</summary>
        public const int WhitePaletteIndex = 256;

        public Texture2D Atlas;

        /// <summary>
        /// The same atlas, but with PALETTE INDICES instead of colours - red channel the index,
        /// alpha channel the opacity. For the palette renderer (see UWShadePalette).
        ///
        /// From THE SAME packing run as Atlas, so the entries and their UV rects apply
        /// unchanged to both. A second packing run could arrange things differently.
        /// </summary>
        public Texture2D IndexAtlas;

        public Entry[] Entries;

        private Dictionary<long, int> mOLookup;

        public static bool IsPacked(UWTexture.TextureTypes peSource)
        {
            for (int i = 0; i < PackedSources.Length; i++)
            {
                if (PackedSources[i] == peSource)
                    return true;
            }

            return false;
        }

        private static long fGetKey(int piSource, int piIndex)
        {
            return ((long)piSource << 32) | (uint)piIndex;
        }

        private void fEnsureLookup()
        {
            if (mOLookup != null || Entries == null)
                return;

            mOLookup = new Dictionary<long, int>(Entries.Length);

            for (int i = 0; i < Entries.Length; i++)
                mOLookup[fGetKey(Entries[i].Source, Entries[i].Index)] = i;
        }

        public bool TryGet(UWTexture.TextureTypes peSource, int piIndex, out Rect pOUvRect, out Vector2Int pOSize)
        {
            pOUvRect = default(Rect);
            pOSize = default(Vector2Int);

            fEnsureLookup();

            if (mOLookup == null)
                return false;

            int liEntry;

            if (!mOLookup.TryGetValue(fGetKey((int)peSource, piIndex), out liEntry))
                return false;

            pOUvRect = Entries[liEntry].UvRect;
            pOSize = Entries[liEntry].Size;

            return pOSize.x > 0 && pOSize.y > 0;
        }

        /// <summary>
        /// Returns the complete entry. Needed where the opaque
        /// area matters - e.g. for doors, whose graphic occupies only the lower part of the
        /// texture.
        /// </summary>
        public bool TryGetEntry(UWTexture pOTexture, out Entry pOEntry)
        {
            pOEntry = default(Entry);

            if (pOTexture == null)
                return false;

            fEnsureLookup();

            if (mOLookup == null)
                return false;

            int liEntry;

            if (!mOLookup.TryGetValue(fGetKey((int)pOTexture.TextureType, pOTexture.Index), out liEntry))
                return false;

            pOEntry = Entries[liEntry];

            return pOEntry.Size.x > 0 && pOEntry.Size.y > 0;
        }

        /// <summary>Texture coordinate of the single-colour spot of an entry, in atlas space.</summary>
        public static Vector2 GetSolidUv(Entry pOEntry)
        {
            float lfU = (pOEntry.SolidPixel.x + 0.5f) / Mathf.Max(1, pOEntry.Size.x);
            float lfV = (pOEntry.SolidPixel.y + 0.5f) / Mathf.Max(1, pOEntry.Size.y);

            return new Vector2(
                pOEntry.UvRect.x + lfU * pOEntry.UvRect.width,
                pOEntry.UvRect.y + lfV * pOEntry.UvRect.height);
        }

        /// <summary>
        /// Texture coordinate of the swatch with the real palette colour piPaletteIndex - for the
        /// single-colour surfaces of the baked-in 3D models (shrine/ankh, boulder, ...).
        /// </summary>
        public bool TryGetPaletteUv(int piPaletteIndex, out Vector2 pOUv)
        {
            pOUv = default(Vector2);

            fEnsureLookup();

            if (mOLookup == null)
                return false;

            int liEntry;

            if (!mOLookup.TryGetValue(fGetKey(PaletteSwatchSource, piPaletteIndex), out liEntry))
                return false;

            Entry lOEntry = Entries[liEntry];

            if (lOEntry.Size.x <= 0 || lOEntry.Size.y <= 0)
                return false;

            pOUv = new Vector2(
                lOEntry.UvRect.x + (lOEntry.UvRect.width * 0.5f),
                lOEntry.UvRect.y + (lOEntry.UvRect.height * 0.5f));

            return true;
        }

        public bool TryGet(UWTexture pOTexture, out Rect pOUvRect, out Vector2Int pOSize)
        {
            pOUvRect = default(Rect);
            pOSize = default(Vector2Int);

            if (pOTexture == null)
                return false;

            return TryGet(pOTexture.TextureType, pOTexture.Index, out pOUvRect, out pOSize);
        }
    }
}
