using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// Builds a single Texture2DArray with all wall and floor textures of the game.
    ///
    /// Previously every single face got its own Texture2D - with around 1800
    /// walkable tiles that meant thousands of copies, each with its own material and thus
    /// without any batching.
    ///
    /// Deliberately shared across all nine levels instead of one array per level: the
    /// texture pool is global, one array per level would be nine nearly identical copies.
    /// The slice index is therefore directly the global texture id, which also saves
    /// converting table indices.
    ///
    /// Array instead of atlas, because walls tile their texture across the wall height
    /// (UV greater than 1). In an atlas that would bleed into the neighbouring texture.
    /// </summary>
    public static class UWTextureArrayBuilder
    {
        /// <summary>Edge length of all slices. Floors are 32x32 in the original and get doubled.</summary>
        public const int SliceResolution = UWDataImport.UWData.UWHeightMap.Size;

        /// <summary>Number of palette colour slices, see GetPaletteSliceOffset.</summary>
        private const int PaletteSwatchCount = 256;

        /// <summary>
        /// The floor textures start at this slice. Equals the number of wall textures;
        /// the floor textures follow directly.
        /// </summary>
        public static int GetFloorSliceOffset(DataImport pOData)
        {
            return pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.WALL).Count;
        }

        /// <summary>
        /// From this slice on lie 256 solid-colour slices, one per palette index of the
        /// main palette (palette 0) - for parts that show a real palette colour in the
        /// original instead of a wall texture, but have to run on the same material/shader
        /// as the dungeon geometry: hand-built geometry like door frame narrow sides
        /// (see UWObjectSpawner.fSpawnFrame) and solid-colour faces of real 3D models whose
        /// other faces show a wall texture (see UWObjectSpawner.fSpawn3DModel - a textured
        /// and a solid-colour face in the same model must end up on the same material). Follows directly after the floor textures.
        /// SINCE 2026-11-22 NOT IN THE COLOUR ARRAY (Build): the dungeon shader takes their colour from the 256x1 table of
        /// BuildSwatchColours (AttachSwatches); the normal and index arrays still hold them as slices.
        /// </summary>
        public static int GetPaletteSliceOffset(DataImport pOData)
        {
            return GetFloorSliceOffset(pOData) + pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.FLOOR).Count;
        }

        /// <param name="piScalePasses">THE xBR TEXTURE SCALING (per user 2026-11-22): 0 the slices as they are, 2 every wall
        /// and floor scaled by xBR twice to 4x (UWXbrScaler). The shader reads the array by its 0..1 coordinate, so nothing
        /// else changes. Walls and floors only: the 256 palette colour slices, a third of the array at 4x, come from
        /// BuildSwatchColours since the same day (per user: "Ja, verkleinern").</param>
        public static Texture2DArray Build(DataImport pOData, FilterMode peFilterMode = FilterMode.Point, int piScalePasses = 0)
        {
            List<UWTexture> lOWalls = pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.WALL);
            List<UWTexture> lOFloors = pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.FLOOR);

            int liPaletteBase = lOWalls.Count + lOFloors.Count;
            int liSliceCount = liPaletteBase;
            int liSize = SliceResolution << piScalePasses;

            Texture2DArray lOArray = new Texture2DArray(liSize, liSize, liSliceCount, TextureFormat.RGBA32, true);
            lOArray.name = piScalePasses > 0 ? "UWTextures (xBR)" : "UWTextures";
            lOArray.filterMode = peFilterMode;
            lOArray.wrapMode = TextureWrapMode.Repeat;

            // The source pixels first (Unity's own calls stay on this thread), then the scaling on every core.
            Color32[][] lOSlices = new Color32[liPaletteBase][];

            for (int i = 0; i < lOWalls.Count; i++)
                lOSlices[i] = fGetPixels(lOWalls[i]);

            for (int i = 0; i < lOFloors.Count; i++)
                lOSlices[lOWalls.Count + i] = fGetPixels(lOFloors[i]);

            if (piScalePasses > 0)
                System.Threading.Tasks.Parallel.For(0, liPaletteBase, i => lOSlices[i] = Scaled(lOSlices[i], piScalePasses));

            for (int i = 0; i < liPaletteBase; i++)
                lOArray.SetPixels32(lOSlices[i], i);

            lOArray.Apply(true, false);

            return lOArray;
        }

        /// <summary>
        /// The normals for the "Remastered" render mode (see UWRemasterRenderPath).
        ///
        /// Same slice layout as Build, so the same meshes and UVs fit; the shader
        /// reads with the same coordinate. RGB holds the normal in tangent space,
        /// B the roughness (see fGetNormalPixels), A the height, which the parallax in
        /// UWDungeon.shader reads.
        ///
        /// NEW (2026-09-13): the height now comes from UWHeightMapBuilder - from the detected
        /// JOINTS, no longer from brightness. The following paragraph describes the old way;
        /// brightness now only determines roughness.
        ///
        /// WHERE THE HEIGHT CAME FROM: from the brightness of the pixel. The original has no
        /// height maps, and for masonry, rock and wood the assumption "bright is raised,
        /// dark is a joint" holds surprisingly well - the artists did darken the joints
        /// by hand. KNOWN LIMIT (already noted in the plan): dark PAINTED spots,
        /// such as lettering or wood grain, wrongly become recesses. Where that is a problem,
        /// an exception table per texture will be needed later.
        ///
        /// The Sobel operator WRAPS AROUND (modulo instead of clamping): the textures tile,
        /// a clamped edge would give a visible seam at every tile border.
        ///
        /// LINEAR, not sRGB - a normal is not a colour. Mipmaps, on the other hand, are
        /// wanted here: unlike the palette path, the average of two normals is a
        /// usable normal, and without mipmaps the relief shimmers in the distance.
        /// </summary>
        public static Texture2DArray BuildNormals(DataImport pOData, float pfStrength)
        {
            List<UWTexture> lOWalls = pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.WALL);
            List<UWTexture> lOFloors = pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.FLOOR);

            int liPaletteBase = lOWalls.Count + lOFloors.Count;
            int liSliceCount = liPaletteBase + PaletteSwatchCount;

            Texture2DArray lOArray = new Texture2DArray(SliceResolution, SliceResolution,
                liSliceCount, TextureFormat.RGBA32, true, true);

            lOArray.name = "UWTextures (normals)";
            lOArray.filterMode = FilterMode.Trilinear;
            lOArray.wrapMode = TextureWrapMode.Repeat;

            for (int i = 0; i < lOWalls.Count; i++)
                lOArray.SetPixels32(fGetNormalPixels(lOWalls[i], pfStrength, i), i);

            for (int i = 0; i < lOFloors.Count; i++)
                lOArray.SetPixels32(fGetNormalPixels(lOFloors[i], pfStrength, -1), lOWalls.Count + i);

            // The solid-colour palette slices are flat - a face without a pattern has no
            // relief, and a derived one would be pure noise.
            Color32[] lOFlat = fGetFlatNormalPixels();

            for (int i = 0; i < PaletteSwatchCount; i++)
                lOArray.SetPixels32(lOFlat, liPaletteBase + i);

            lOArray.Apply(true, false);

            return lOArray;
        }

        /// <summary>
        /// THE SECOND GAME'S ICE SLICES (UWHeightMap.IceTexturesUw2; per user 2026-11-22), for the shader's own relief depth
        /// there (_IceRelief): one texel per slice of the colour array (R8), 255 for an ice texture's wall or floor slice,
        /// 0 for anything else. In the second game a texture's number in either list is its T64.TR number. All 0 in the
        /// first game.
        /// </summary>
        public static Texture2D BuildIceSliceTable(DataImport pOData)
        {
            List<UWTexture> lOWalls = pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.WALL);
            List<UWTexture> lOFloors = pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.FLOOR);
            int liSliceCount = lOWalls.Count + lOFloors.Count + PaletteSwatchCount;
            byte[] lyTable = new byte[liSliceCount];

            if (UWGameSession.Current.Game.Game == UWGame.Uw2)
            {
                foreach (int liTexture in UWHeightMap.IceTexturesUw2)
                {
                    if (liTexture < lOWalls.Count)
                        lyTable[liTexture] = 255;

                    if (liTexture < lOFloors.Count)
                        lyTable[lOWalls.Count + liTexture] = 255;
                }
            }

            Texture2D lOTable = new Texture2D(liSliceCount, 1, TextureFormat.R8, false, true);

            lOTable.name = "UWTextures (ice slices)";
            lOTable.filterMode = FilterMode.Point;
            lOTable.wrapMode = TextureWrapMode.Clamp;
            lOTable.SetPixelData(lyTable, 0);
            lOTable.Apply(false, false);

            return lOTable;
        }

        /// <summary>The normal per pixel from the gradient of the height map
        /// (UWHeightMapBuilder); the blue channel carries the roughness from brightness.</summary>
        /// <param name="piWall">The wall's number (its slice), -1 for a floor (UWHeightMapBuilder).</param>
        private static Color32[] fGetNormalPixels(UWTexture pOTexture, float pfStrength, int piWall)
        {
            Color32[] lOColours = fGetPixels(pOTexture);
            Color32[] lOTarget = new Color32[lOColours.Length];

            // Brightness is only used for roughness now - the shape comes from the joints.
            float[] lfBrightness = new float[lOColours.Length];

            for (int i = 0; i < lOColours.Length; i++)
                lfBrightness[i] = ((lOColours[i].r * 0.299f) + (lOColours[i].g * 0.587f)
                    + (lOColours[i].b * 0.114f)) / 255f;

            float[] lfHeights = UWHeightMapBuilder.Build(lOColours, fGetIndexPixels(pOTexture), piWall).Heights;

            for (int y = 0; y < SliceResolution; y++)
            {
                for (int x = 0; x < SliceResolution; x++)
                {
                    float lfLeft = fSampleHeight(lfHeights, x - 1, y - 1) + (2f * fSampleHeight(lfHeights, x - 1, y))
                        + fSampleHeight(lfHeights, x - 1, y + 1);
                    float lfRight = fSampleHeight(lfHeights, x + 1, y - 1) + (2f * fSampleHeight(lfHeights, x + 1, y))
                        + fSampleHeight(lfHeights, x + 1, y + 1);
                    float lfDown = fSampleHeight(lfHeights, x - 1, y - 1) + (2f * fSampleHeight(lfHeights, x, y - 1))
                        + fSampleHeight(lfHeights, x + 1, y - 1);
                    float lfUp = fSampleHeight(lfHeights, x - 1, y + 1) + (2f * fSampleHeight(lfHeights, x, y + 1))
                        + fSampleHeight(lfHeights, x + 1, y + 1);

                    float lfSlopeX = (lfRight - lfLeft) * 0.25f;
                    float lfSlopeY = (lfUp - lfDown) * 0.25f;

                    Vector3 lONormal = new Vector3(-lfSlopeX * pfStrength, -lfSlopeY * pfStrength, 1f).normalized;

                    // The blue channel does NOT hold the normal's Z - the shader reconstructs
                    // it from X and Y, since the normal always points out of the surface.
                    // The slot carries the ROUGHNESS instead.
                    lOTarget[(y * SliceResolution) + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(((lONormal.x * 0.5f) + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(((lONormal.y * 0.5f) + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fGetRoughness(lfBrightness, x, y) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(lfHeights[(y * SliceResolution) + x] * 255f), 0, 255));
                }
            }

            return lOTarget;
        }

        /// <summary>
        /// How rough a spot is, measured by how much its nine neighbours differ from each other.
        ///
        /// WHY: a specular highlight that is equally strong everywhere makes every surface
        /// look WET (per user, 2026-09-12). Real stone only shines where it is smooth.
        /// A coarse-grained patch in the texture is rough stone, a calm area a polished
        /// or damp spot - the same brightness assumption the height used to rest on, just
        /// one derivative further. The values passed in are brightness, not heights.
        ///
        /// Nothing becomes fully smooth: the lower bound keeps even calm surfaces matte enough
        /// that they do not look like plastic.
        /// </summary>
        private static float fGetRoughness(float[] pfHeights, int piX, int piY)
        {
            float lfSum = 0f;

            for (int liY = -1; liY <= 1; liY++)
            {
                for (int liX = -1; liX <= 1; liX++)
                    lfSum += fSampleHeight(pfHeights, piX + liX, piY + liY);
            }

            float lfMean = lfSum / 9f;
            float lfDeviation = 0f;

            for (int liY = -1; liY <= 1; liY++)
            {
                for (int liX = -1; liX <= 1; liX++)
                    lfDeviation += Mathf.Abs(fSampleHeight(pfHeights, piX + liX, piY + liY) - lfMean);
            }

            lfDeviation /= 9f;

            return Mathf.Lerp(RoughnessFloor, 1f, Mathf.Clamp01(lfDeviation * RoughnessScale));
        }

        /// <summary>Even a completely calm spot is still this rough.</summary>
        private const float RoughnessFloor = 0.55f;

        /// <summary>From this brightness difference on, a spot counts as fully rough.</summary>
        private const float RoughnessScale = 8f;

        /// <summary>Wrap around instead of clamping - the textures tile.</summary>
        private static float fSampleHeight(float[] pfHeights, int piX, int piY)
        {
            int liX = ((piX % SliceResolution) + SliceResolution) % SliceResolution;
            int liY = ((piY % SliceResolution) + SliceResolution) % SliceResolution;

            return pfHeights[(liY * SliceResolution) + liX];
        }

        /// <summary>A flat surface: normal straight out, half height.</summary>
        private static Color32[] fGetFlatNormalPixels()
        {
            Color32[] lOTarget = new Color32[SliceResolution * SliceResolution];
            Color32 lOFlat = new Color32(128, 128, 255, 128);

            for (int i = 0; i < lOTarget.Length; i++)
                lOTarget[i] = lOFlat;

            return lOTarget;
        }

        /// <summary>
        /// FOR THE PALETTE RENDERER (used by UWPaletteRenderPath and UWRemasterRenderPath). Builds the same
        /// array as Build, but instead of finished colours each pixel holds the
        /// PALETTE INDEX in the red channel. The colour only arises in the shader via the
        /// colour table from UWShadePalette - only this makes the lighting of the
        /// original possible, which computes on indices and not on RGB.
        ///
        /// Same slice layout as Build, so GetFloorSliceOffset and
        /// GetPaletteSliceOffset apply unchanged. A palette colour slice here is simply
        /// its own index throughout.
        ///
        /// NO MIPMAPS, and that is deliberate: an averaged intermediate level of two
        /// indices is a third, completely unrelated index. The original had none
        /// anyway, it draws pixelated in the distance too. But in exchange we get
        /// shimmering on distant floors that does not exist today.
        ///
        /// LINEAR, not sRGB - otherwise Unity puts a gamma curve on the index.
        ///
        /// ONE BYTE per pixel (R8), not four: an index needs no more, and the
        /// whole array thus costs a quarter of the colour array. Walls and floors are always
        /// opaque, so no second channel for transparency is needed -
        /// for sprites that will be different, see UWShadePalette.
        /// </summary>
        public static Texture2DArray BuildIndexed(DataImport pOData)
        {
            List<UWTexture> lOWalls = pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.WALL);
            List<UWTexture> lOFloors = pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.FLOOR);

            int liPaletteBase = lOWalls.Count + lOFloors.Count;
            int liSliceCount = liPaletteBase + PaletteSwatchCount;

            Texture2DArray lOArray = new Texture2DArray(SliceResolution, SliceResolution,
                liSliceCount, TextureFormat.R8, false, true);

            lOArray.name = "UWTextures (indices)";
            lOArray.filterMode = FilterMode.Point;
            lOArray.wrapMode = TextureWrapMode.Repeat;

            for (int i = 0; i < lOWalls.Count; i++)
                lOArray.SetPixelData(fGetIndexPixels(lOWalls[i]), 0, i);

            for (int i = 0; i < lOFloors.Count; i++)
                lOArray.SetPixelData(fGetIndexPixels(lOFloors[i]), 0, lOWalls.Count + i);

            for (int i = 0; i < PaletteSwatchCount; i++)
                lOArray.SetPixelData(fGetConstantIndexPixels((byte)i), 0, liPaletteBase + i);

            lOArray.Apply(false, false);

            return lOArray;
        }

        /// <summary>Like fGetPixels, but the palette index is stored instead of its
        /// colour. Same flip and same upscaling of the 32x32 floors, so that the
        /// UVs fit unchanged.</summary>
        private static byte[] fGetIndexPixels(UWTexture pOTexture)
        {
            byte[] lOTarget = new byte[SliceResolution * SliceResolution];

            if (pOTexture == null)
                return lOTarget;

            byte[] lyIndices = pOTexture.GetMainPaletteIndices();

            int liWidth = pOTexture.Width;
            int liHeight = pOTexture.Height;

            if (lyIndices == null || liWidth <= 0 || liHeight <= 0
                || lyIndices.Length < liWidth * liHeight)
                return lOTarget;

            for (int y = 0; y < SliceResolution; y++)
            {
                int liSourceY = Mathf.Min(liHeight - 1, (SliceResolution - 1 - y) * liHeight / SliceResolution);

                for (int x = 0; x < SliceResolution; x++)
                {
                    int liSourceX = Mathf.Min(liWidth - 1, x * liWidth / SliceResolution);

                    lOTarget[y * SliceResolution + x] = lyIndices[liSourceY * liWidth + liSourceX];
                }
            }

            return lOTarget;
        }

        /// <summary>A slice holding the same palette index everywhere.</summary>
        private static byte[] fGetConstantIndexPixels(byte pyIndex)
        {
            byte[] lOTarget = new byte[SliceResolution * SliceResolution];

            for (int i = 0; i < lOTarget.Length; i++)
                lOTarget[i] = pyIndex;

            return lOTarget;
        }

        /// <summary>
        /// A texture that moves via palette rotation: its slice in the array and
        /// the finished images of all rotation steps.
        ///
        /// BECOMES OBSOLETE WITH THE PALETTE RENDERER: there the rotation table in
        /// UWShadePalette rotates the whole palette at once, and a single number as a
        /// uniform suffices instead of pre-baked images per texture.
        /// </summary>
        public struct AnimatedSlice
        {
            public int Slice;

            public Texture2D[] States;
        }

        /// <summary>
        /// Finds all wall and floor textures that use colours from the rotating
        /// palette ranges, and pre-builds the images of all rotation steps for each
        /// (see UWPaletteRotation). At runtime only the matching image is then
        /// copied into the slice - no computing per image.
        ///
        /// On level 1 these include the waterfalls, the lavafall and the water
        /// and lava floors; textures with just a few drops of the colour (the drain, for
        /// example) are included as well, because they rotate along in the original too.
        /// </summary>
        public static List<AnimatedSlice> BuildAnimatedSlices(DataImport pOData, FilterMode peFilterMode, int piScalePasses = 0)
        {
            List<AnimatedSlice> lOResult = new List<AnimatedSlice>();

            List<UWTexture> lOWalls = pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.WALL);
            List<UWTexture> lOFloors = pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.FLOOR);

            // Each state's pixels are gathered first and scaled on every core afterwards, as in Build: one state after the
            // other on this thread made the xBR switch stand still for seconds (2026-11-22, per user "ca. 5 Sekunden";
            // the second game has 62 moving textures with 358 states).
            List<Texture2D> lOPending = new List<Texture2D>();
            List<Color32[]> lOPendingPixels = new List<Color32[]>();

            for (int i = 0; i < lOWalls.Count; i++)
                fAddIfAnimated(lOWalls[i], i, peFilterMode, piScalePasses, lOResult, lOPending, lOPendingPixels);

            for (int i = 0; i < lOFloors.Count; i++)
                fAddIfAnimated(lOFloors[i], lOWalls.Count + i, peFilterMode, piScalePasses, lOResult, lOPending, lOPendingPixels);

            if (piScalePasses > 0)
                System.Threading.Tasks.Parallel.For(0, lOPendingPixels.Count, i => lOPendingPixels[i] = Scaled(lOPendingPixels[i], piScalePasses));

            for (int i = 0; i < lOPending.Count; i++)
            {
                lOPending[i].SetPixels32(lOPendingPixels[i]);
                lOPending[i].Apply(true, false);
            }

            return lOResult;
        }

        private static void fAddIfAnimated(UWTexture pOTexture, int piSlice, FilterMode peFilterMode, int piScalePasses,
            List<AnimatedSlice> pOResult, List<Texture2D> pOPending, List<Color32[]> pOPendingPixels)
        {
            if (pOTexture == null || pOTexture.PaletteIndices == null)
                return;

            bool lbLava = false;
            bool lbWater = false;

            for (int i = 0; i < pOTexture.PaletteIndices.Length; i++)
            {
                byte lyIndex = pOTexture.PaletteIndices[i];

                // The banks that rotate, by game (the second game's are no lava or water, UWPaletteRotation).
                if (UWPaletteRotation.IsUpIndex(lyIndex))
                    lbLava = true;
                else if (UWPaletteRotation.IsDownIndex(lyIndex))
                    lbWater = true;
            }

            int liCycle = UWPaletteRotation.GetCycleLength(lbLava, lbWater);

            if (liCycle <= 1)
                return;

            UWPalette lOPalette = pOTexture.Palettes.GetPalette(pOTexture.MainPaletteIndex);

            if (lOPalette == null)
                return;

            Texture2D[] lOStates = new Texture2D[liCycle];

            for (int liStep = 0; liStep < liCycle; liStep++)
            {
                int liSize = SliceResolution << piScalePasses;
                Texture2D lOState = new Texture2D(liSize, liSize, TextureFormat.RGBA32, true);
                lOState.name = string.Format("UWRotation {0}_{1} step {2}", pOTexture.TextureType, pOTexture.Index, liStep);
                lOState.filterMode = peFilterMode;
                lOState.wrapMode = TextureWrapMode.Repeat;
                pOPending.Add(lOState);
                pOPendingPixels.Add(fGetRotatedPixels(pOTexture, lOPalette, liStep));

                lOStates[liStep] = lOState;
            }

            pOResult.Add(new AnimatedSlice { Slice = piSlice, States = lOStates });
        }

        /// <summary>
        /// Like fGetPixels, but the colour does not come from the baked texture; it is
        /// taken fresh from the palette via the rotated palette index.
        /// </summary>
        private static Color32[] fGetRotatedPixels(UWTexture pOTexture, UWPalette pOPalette, int piStep)
        {
            Color32[] lOTarget = new Color32[SliceResolution * SliceResolution];

            int liWidth = pOTexture.Width;
            int liHeight = pOTexture.Height;
            byte[] lyIndices = pOTexture.PaletteIndices;

            if (liWidth <= 0 || liHeight <= 0 || lyIndices == null || lyIndices.Length < liWidth * liHeight)
                return lOTarget;

            for (int y = 0; y < SliceResolution; y++)
            {
                // Same flip as in fGetPixels - otherwise the texture would be upside down.
                int liSourceY = Mathf.Min(liHeight - 1, (SliceResolution - 1 - y) * liHeight / SliceResolution);

                for (int x = 0; x < SliceResolution; x++)
                {
                    int liSourceX = Mathf.Min(liWidth - 1, x * liWidth / SliceResolution);
                    int liIndex = lyIndices[liSourceY * liWidth + liSourceX];

                    UWColor32 lOPixel = pOPalette.GetUWColor(UWPaletteRotation.GetSourceIndex(liIndex, piStep));
                    lOTarget[y * SliceResolution + x] = new Color32(lOPixel.R, lOPixel.G, lOPixel.B, 255);
                }
            }

            return lOTarget;
        }

        /// <summary>Solid-colour slice pixels for a palette index.</summary>
        /// <summary>A slice's pixels (SliceResolution square) scaled by xBR piPasses times (UWXbrScaler); 0 returns
        /// them as they are. Alpha stays opaque, as every wall and floor slice is.</summary>
        public static Color32[] Scaled(Color32[] pOPixels, int piPasses)
        {
            if (piPasses <= 0 || pOPixels == null)
                return pOPixels;

            uint[] luySource = new uint[pOPixels.Length];

            for (int i = 0; i < pOPixels.Length; i++)
                luySource[i] = 0xFF000000u | ((uint)pOPixels[i].b << 16) | ((uint)pOPixels[i].g << 8) | pOPixels[i].r;

            uint[] luyScaled = UWXbrScaler.Scale(luySource, SliceResolution, SliceResolution, piPasses);
            Color32[] lOOut = new Color32[luyScaled.Length];

            for (int i = 0; i < luyScaled.Length; i++)
                lOOut[i] = new Color32((byte)luyScaled[i], (byte)(luyScaled[i] >> 8), (byte)(luyScaled[i] >> 16), 255);

            return lOOut;
        }

        /// <summary>The 256 palette colours of the main palette (palette 0) in a row, one texel each: the colour of the
        /// palette colour slices (GetPaletteSliceOffset), which the colour array no longer holds. sRGB like the array,
        /// point filtered.</summary>
        public static Texture2D BuildSwatchColours(DataImport pOData)
        {
            UWPalette lOPalette = pOData.Palettes.GetPalette(0);
            Color32[] lOPixels = new Color32[PaletteSwatchCount];

            for (int i = 0; i < PaletteSwatchCount; i++)
            {
                UWColor32 lOColor = lOPalette.GetUWColor(i);
                lOPixels[i] = new Color32(lOColor.R, lOColor.G, lOColor.B, 255);
            }

            Texture2D lOTable = new Texture2D(PaletteSwatchCount, 1, TextureFormat.RGBA32, false);
            lOTable.name = "UWSwatchColours";
            lOTable.filterMode = FilterMode.Point;
            lOTable.wrapMode = TextureWrapMode.Clamp;
            lOTable.SetPixels32(lOPixels);
            lOTable.Apply(false, true);

            return lOTable;
        }

        /// <summary>Hands the dungeon material (UW/Dungeon) the palette colour table and the slice it starts at
        /// (_SwatchColours, _SwatchBase); returns the table.</summary>
        public static Texture2D AttachSwatches(Material pOMaterial, DataImport pOData)
        {
            Texture2D lOTable = BuildSwatchColours(pOData);

            pOMaterial.SetTexture(UWShaderIds.SwatchColours, lOTable);
            pOMaterial.SetFloat(UWShaderIds.SwatchBase, GetPaletteSliceOffset(pOData));

            return lOTable;
        }

        /// <summary>The colours of a texture at slice size, as they lie in the array.</summary>
        public static Color32[] GetSlicePixels(UWTexture pOTexture)
        {
            return fGetPixels(pOTexture);
        }

        /// <summary>The palette indices of a texture at slice size.</summary>
        public static byte[] GetSliceIndices(UWTexture pOTexture)
        {
            return fGetIndexPixels(pOTexture);
        }

        /// <summary>
        /// Returns the pixels of a texture brought to slice size. Floor textures are
        /// 32x32 and are upscaled by pixel doubling, because all slices of an
        /// array must have the same size. With point filtering this is visually
        /// identical to the original.
        /// </summary>
        private static Color32[] fGetPixels(UWTexture pOTexture)
        {
            Color32[] lOTarget = new Color32[SliceResolution * SliceResolution];

            if (pOTexture == null)
                return lOTarget;

            UWColor32[] lOSource;

            try
            {
                lOSource = pOTexture.GetUWColor32();
            }
            catch
            {
                return lOTarget;
            }

            int liWidth = pOTexture.Width;
            int liHeight = pOTexture.Height;

            if (lOSource == null || liWidth <= 0 || liHeight <= 0 || lOSource.Length < liWidth * liHeight)
                return lOTarget;

            for (int y = 0; y < SliceResolution; y++)
            {
                // Row 0 of the source is at the top, Unity starts at the bottom. Without this
                // flip all wall and floor textures are upside down. With
                // masonry this is barely noticeable, with wall decorations like the
                // entrance door immediately - its stone arch ends up at the bottom.
                int liSourceY = Mathf.Min(liHeight - 1, (SliceResolution - 1 - y) * liHeight / SliceResolution);

                for (int x = 0; x < SliceResolution; x++)
                {
                    int liSourceX = Mathf.Min(liWidth - 1, x * liWidth / SliceResolution);
                    UWColor32 lOPixel = lOSource[liSourceY * liWidth + liSourceX];
                    lOTarget[y * SliceResolution + x] = new Color32(lOPixel.R, lOPixel.G, lOPixel.B, lOPixel.A);
                }
            }

            return lOTarget;
        }
    }
}
