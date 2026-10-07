using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// GROUNDWORK FOR THE PALETTE RENDERER. Builds the lookup textures such a
    /// renderer needs. They are used by UWPaletteRenderToggle, which is the default render
    /// path (UWSettings.RenderMode Palette); F6 switches back to the URP lighting for comparison.
    ///
    /// HOW THE ORIGINAL CALCULATES
    ///
    /// There are no local light sources in the dungeon. A wall torch is a bright image,
    /// it lights up nothing. All lighting depends on two quantities:
    ///
    ///   1. the player's light level, 0 to 7 - the brightest carried light source
    ///      or the light spell, whichever is brighter. Exactly the value that
    ///      UWLighting already forms from carried light and spell light.
    ///   2. the distance of the pixel from the player.
    ///
    /// Together, via SHADES.DAT, both give a brightness step 0 to 15 (see
    /// UWShades.GetShadeTable). With it LIGHT.DAT is looked up for what a
    /// palette index becomes at this brightness, and only this result becomes the colour.
    ///
    /// That is the reason why the URP lighting never feels quite right: URP
    /// calculates point lights with distance squared and a soft falloff, the original switches
    /// in sixteen steps via a table. That is also why the range has to be
    /// quadrupled for us to get close (see UWLighting).
    ///
    /// INTERMEDIATE STEPS, NOT JUMPS
    ///
    /// The shading table gives the brightness step only at whole tile distances,
    /// and in coarse steps at that - at light level 4 roughly 0, 4, 9, 14. Taken like that,
    /// you get rings with hard edges around the player. In the original, however, the transitions
    /// on the floor are visibly softer (per user in an image comparison, 2026-09-05): it carries the
    /// step across the surface and also uses the intermediate steps.
    ///
    /// That is why the STEP is interpolated between two tile distances and only then
    /// looked up - not the colour. The difference is essential: a blended colour
    /// is not in the palette and would be impossible in the original, an intermediate step is not.
    ///
    /// WHAT IS BUILT HERE
    ///
    /// Three textures, all built once at load time and unchanged afterwards:
    ///
    ///   colour table   256 wide, 16 high. x is the palette index, y the brightness step.
    ///                  The pixel is the final colour. Nothing but LIGHT.DAT,
    ///                  already run through the palette.
    ///
    ///   shade table    16 wide, 8 high. x is the tile distance, y the player's
    ///                  light level, the red value the brightness step. That is SHADES.DAT,
    ///                  computed out.
    ///
    ///   rotation table 256 wide, 60 high. x is the palette index, y the step of the
    ///                  palette rotation, the red value the rotated index. 60 is the least
    ///                  common multiple of the lava and water cycles.
    ///
    /// The rotation table incidentally solves a problem of the URP path: there water and lava
    /// are pre-baked as finished images per step and copied into the slices at runtime
    /// (UWTextureArrayBuilder.BuildAnimatedSlices, UWAnimatedTextures). In the
    /// palette renderer ONE number as a uniform is enough, and all the water of the level rotates
    /// along - no copying and no pre-baking. The UI icons rotate along too, since
    /// they go through UW/IconPalette; previously UWCycledIcon pre-baked all steps for this.
    ///
    /// OPACITY IS NOT HANDLED HERE
    ///
    /// All three tables are opaque. Which pixel is transparent is decided by a different rule
    /// depending on its origin, and a table only knows the index:
    ///
    ///   walls and floors    always opaque.
    ///   sprites             RAW INDEX ZERO is transparent, i.e. the value BEFORE the conversion
    ///                       via the auxiliary palette (see UWObjectAtlasBuilder,
    ///                       lbIndexZero). Afterwards it can no longer be recognised, because
    ///                       auxiliary index 0 points to an ordinary main index - so the
    ///                       opacity MUST be recorded at build time.
    ///   icons, font         the palette's transparency marker (see UWPalette).
    ///   fog, ghosts         partial opacity from XFER.DAT (see UWTransparencyTables).
    ///
    /// It therefore belongs in the alpha channel of the source texture. Still OPEN are the
    /// blend indices from XFER.DAT: they are not colours but calculation rules applied to
    /// what is already in the image. As long as we approximate them as today with a fixed blend colour
    /// (UWObjectAtlasBuilder.fTryGetBlend), their colour does not match the lighting.
    ///
    /// COLOUR SPACE
    ///
    /// The source textures hold the palette index in the red channel, not a colour. If
    /// Unity treated them as sRGB, it would apply a gamma curve when reading and
    /// index 100 would turn into something else. Index, shade and rotation tables must therefore
    /// be created with linear = true and read with FilterMode.Point.
    ///
    /// The COLOUR table is the exception: it holds real colours, so like the
    /// previous colour array it belongs in sRGB. Today this has no consequence, because the project runs in
    /// gamma colour space and Unity converts nothing at all - it only matters if the project is ever
    /// switched to linear. Then a colour table created as linear would immediately be too bright,
    /// and the bug would look like a bug in the lighting.
    /// </summary>
    public static class UWShadePalette
    {
        /// <summary>Width of the colour and rotation tables - one pixel per palette index.</summary>
        public const int PaletteSize = 256;

        /// <summary>Brightness steps, i.e. rows of the colour table. Corresponds to the blocks in
        /// LIGHT.DAT.</summary>
        public const int ShadeLevelCount = UWLightLevels.LevelCount;

        /// <summary>Tile distances, i.e. columns of the shade table.</summary>
        public const int DistanceCount = UWShades.ShadeTableLength;

        /// <summary>Player light levels, i.e. rows of the shade table.</summary>
        public const int LightLevelCount = UWShades.EntryCount;

        /// <summary>Height of the rotation table - least common multiple of both
        /// rotation cycles.</summary>
        public static int RotationStepCount
        {
            get { return UWPaletteRotation.GetCycleLength(true, true); }
        }

        /// <summary>Name of the colour table in the shader.</summary>
        public const string ColourTableProperty = "_UWColourTable";

        /// <summary>The colour table the 3D view shades through (UWPaletteLookup.hlsl) - the same as
        /// _UWColourTable except at Night Vision, where MONO.DAT replaces LIGHT.DAT for the world
        /// only: the interface draws without the light table in the original and stays in colour.</summary>
        public const string WorldColourTableProperty = "_UWWorldColourTable";

        /// <summary>The light level at which the original loads MONO.DAT as its light table
        /// (OpenAndApplyShadesDat_ovr142_0, read 2026-10-01): 5, the level of Night Vision - light
        /// sources reach 4 at most.</summary>
        public const int MonochromeLightLevel = 5;

        /// <summary>Name of the shade table in the shader.</summary>
        public const string ShadeTableProperty = "_UWShadeTable";

        /// <summary>Name of the rotation table in the shader.</summary>
        public const string RotationTableProperty = "_UWRotationTable";

        /// <summary>
        /// The colour table: for each palette index the colour it takes on at a given
        /// brightness step. Row 0 is the original colours, row 15 is black.
        /// </summary>
        public static Texture2D BuildColourTable(DataImport pOData, int piPalette = 0, bool pbMonochrome = false)
        {
            if (pOData == null || pOData.Palettes == null || pOData.LightLevels == null)
                return null;

            // Another palette only for the hallucination's colours (UWHallucinationState).
            UWPalette lOPalette;

            try
            {
                lOPalette = pOData.Palettes.GetPalette(piPalette);
            }
            catch (System.Exception)
            {
                lOPalette = pOData.Palettes.GetPalette(0);
            }

            if (lOPalette == null)
                return null;

            bool lbMono = pbMonochrome && pOData.MiscDataFiles != null && pOData.MiscDataFiles.Monochrome != null
                && pOData.MiscDataFiles.Monochrome.IsLoaded;

            // linear = false: real colours, see class comment.
            Texture2D lOTable = new Texture2D(PaletteSize, ShadeLevelCount,
                TextureFormat.RGBA32, false, false);

            lOTable.name = lbMono ? "UWColourTable (mono)" : "UWColourTable";
            lOTable.filterMode = FilterMode.Point;
            lOTable.wrapMode = TextureWrapMode.Clamp;

            Color32[] lOPixels = new Color32[PaletteSize * ShadeLevelCount];

            for (int liLevel = 0; liLevel < ShadeLevelCount; liLevel++)
            {
                for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
                {
                    // MONO.DAT instead of LIGHT.DAT at light level 5, Night Vision
                    // (OpenAndApplyShadesDat_ovr142_0) - see MonochromeLightLevel.
                    byte lyShaded = lbMono
                        ? pOData.MiscDataFiles.Monochrome.Remap(liLevel, liIndex)
                        : pOData.LightLevels.Remap(liLevel, liIndex);

                    UWColor32 lOColour = lOPalette.GetUWColor(lyShaded);

                    // Colour help for colour vision deficiencies - the interface and the palette
                    // renderer both read their colours from this table (see UWColourVision).
                    lOPixels[(liLevel * PaletteSize) + liIndex] =
                        UWColourVision.Apply(new Color32(lOColour.R, lOColour.G, lOColour.B, 255));
                }
            }

            lOTable.SetPixels32(lOPixels);
            lOTable.Apply(false, false);

            return lOTable;
        }

        /// <summary>
        /// The shade table: for each player light level and each whole tile distance
        /// the brightness step. That is SHADES.DAT, computed out - see
        /// UWShades.GetShadeTable.
        ///
        /// The shader interpolates between two columns, which is why the step is stored here
        /// raw and not already as a colour.
        /// </summary>
        public static Texture2D BuildShadeTable(DataImport pOData, bool pbDiagonalStretch = false)
        {
            if (pOData == null || pOData.Shades == null || !pOData.Shades.IsLoaded)
                return null;

            Texture2D lOTable = new Texture2D(DistanceCount, LightLevelCount,
                TextureFormat.R8, false, true);

            lOTable.name = "UWShadeTable";
            lOTable.filterMode = FilterMode.Point;
            lOTable.wrapMode = TextureWrapMode.Clamp;

            byte[] lyPixels = new byte[DistanceCount * LightLevelCount];

            for (int liLight = 0; liLight < LightLevelCount; liLight++)
            {
                byte[] lyShades = pOData.Shades.GetShadeTable(liLight, pbDiagonalStretch);

                for (int liDistance = 0; liDistance < DistanceCount; liDistance++)
                    lyPixels[(liLight * DistanceCount) + liDistance] =
                        lyShades != null ? lyShades[liDistance] : (byte)0;
            }

            lOTable.SetPixelData(lyPixels, 0);
            lOTable.Apply(false, false);

            return lOTable;
        }

        /// <summary>
        /// The rotation table: for each palette index and each step of the palette rotation the
        /// index currently visible there. Indices outside the lava and
        /// water ranges stand still, their row is the same in every step.
        ///
        /// The value is stored in the red channel as index divided by 255. The shader multiplies it
        /// back by 255 and rounds.
        /// </summary>
        public static Texture2D BuildRotationTable()
        {
            int liSteps = RotationStepCount;

            Texture2D lOTable = new Texture2D(PaletteSize, liSteps, TextureFormat.RGBA32,
                false, true);

            lOTable.name = "UWRotationTable";
            lOTable.filterMode = FilterMode.Point;
            lOTable.wrapMode = TextureWrapMode.Clamp;

            Color32[] lOPixels = new Color32[PaletteSize * liSteps];

            for (int liStep = 0; liStep < liSteps; liStep++)
            {
                for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
                {
                    byte lySource = (byte)UWPaletteRotation.GetSourceIndex(liIndex, liStep);

                    lOPixels[(liStep * PaletteSize) + liIndex] = new Color32(lySource, 0, 0, 255);
                }
            }

            lOTable.SetPixels32(lOPixels);
            lOTable.Apply(false, false);

            return lOTable;
        }

        /// <summary>
        /// Sets everything the palette shaders need per frame as global shader values -
        /// so not a single material has to know them.
        ///
        /// Meant as the ONLY hookup of the palette renderer: whoever switches over calls this
        /// once per frame and is done. The three tables never change and are built once;
        /// the per-frame values are light level, rotation step, cutoff distance and the switches.
        ///
        /// In the original the rotation step advances at a fixed speed, independent
        /// of the frame rate - the caller calculates it from UWSettings.PaletteRotationStepsPerSecond
        /// (see UWPaletteRenderToggle.Update), the same way UWAnimatedTextures does.
        /// </summary>
        public static void ApplyGlobals(Texture2D pOColourTable, Texture2D pOShadeTable,
            Texture2D pORotationTable, int piLightLevel, int piRotationStep,
            float pfCutoffDistance, bool pbInterpolateLevels, bool pbDither,
            bool pbOrderedMatrix, float pfDitherScale, bool pbFullBright = false)
        {
            if (pOColourTable != null)
                Shader.SetGlobalTexture(ColourTableProperty, pOColourTable);

            if (pOShadeTable != null)
                Shader.SetGlobalTexture(ShadeTableProperty, pOShadeTable);

            if (pORotationTable != null)
                Shader.SetGlobalTexture(RotationTableProperty, pORotationTable);

            int liSteps = RotationStepCount;

            Shader.SetGlobalFloat("_UWLightLevel",
                Mathf.Clamp(piLightLevel, 0, LightLevelCount - 1));
            Shader.SetGlobalFloat("_UWRotationStep", ((piRotationStep % liSteps) + liSteps) % liSteps);
            Shader.SetGlobalFloat("_UWRotationStepCount", liSteps);
            Shader.SetGlobalFloat("_UWTileSpacing", UWLevelMeshBuilder.TileSpacing);
            Shader.SetGlobalFloat("_UWCutoffDistance", pfCutoffDistance);
            Shader.SetGlobalFloat("_UWInterpolateLevels", pbInterpolateLevels ? 1f : 0f);
            Shader.SetGlobalFloat("_UWDither", pbDither ? 1f : 0f);
            Shader.SetGlobalFloat("_UWDitherPattern", pbOrderedMatrix ? 1f : 0f);
            Shader.SetGlobalFloat("_UWDitherScale", Mathf.Max(1f, pfDitherScale));
            Shader.SetGlobalFloat("_UWFullBright", pbFullBright ? 1f : 0f);
        }

        /// <summary>Names of the XFER.DAT lookups in the shader (UWPaletteLookup.hlsl).</summary>
        public const string InverseLookupProperty = "_UWInverseLookup";

        public const string XferColourProperty = "_UWXferColours";

        public const string XferReadyProperty = "_UWXferReady";

        /// <summary>
        /// THE TRANSLUCENT COLOURS AS THE ORIGINAL MIXES THEM (audit row 12, 2026-09-27): the
        /// cube that turns a colour of the picture back into its palette index
        /// (UWTransparencyTables.BuildInverseLookup), built from the colours exactly as the
        /// colour table shows them - colour help included, so the picture's own colours are
        /// found again.
        /// </summary>
        public static Texture3D BuildInverseLookup(DataImport pOData, bool pbColourHelp = true)
        {
            if (pOData == null || pOData.Palettes == null)
                return null;

            UWPalette lOPalette = pOData.Palettes.GetPalette(0);
            UWColor32[] lOColours = new UWColor32[PaletteSize];

            for (int i = 0; i < PaletteSize; i++)
                lOColours[i] = fShown(lOPalette, i, pbColourHelp);

            byte[] lyCube = UWTransparencyTables.BuildInverseLookup(lOColours);
            int liSize = UWTransparencyTables.InverseLookupSize;

            // linear = true: it holds indices, not colours.
            Texture3D lOCube = new Texture3D(liSize, liSize, liSize, TextureFormat.R8, false);

            lOCube.name = pbColourHelp ? "UWInverseLookup" : "UWInverseLookup (raw)";
            lOCube.filterMode = FilterMode.Point;
            lOCube.wrapMode = TextureWrapMode.Clamp;
            lOCube.SetPixelData(lyCube, 0);
            lOCube.Apply(false, true);

            return lOCube;
        }

        /// <summary>
        /// For every table of XFER.DAT (rows) and every background index (columns) the colour the
        /// table turns it into - the table and the palette in one step, so the shader needs one
        /// lookup after the inverse one.
        /// </summary>
        public static Texture2D BuildXferColours(DataImport pOData, bool pbColourHelp = true)
        {
            if (pOData == null || pOData.Palettes == null || pOData.TransparencyTables == null
                || !pOData.TransparencyTables.IsLoaded)
                return null;

            UWPalette lOPalette = pOData.Palettes.GetPalette(0);
            int liTables = UWTransparencyTables.Count;

            Texture2D lOTable = new Texture2D(PaletteSize, liTables, TextureFormat.RGBA32, false, false);

            lOTable.name = pbColourHelp ? "UWXferColours" : "UWXferColours (raw)";
            lOTable.filterMode = FilterMode.Point;
            lOTable.wrapMode = TextureWrapMode.Clamp;

            Color32[] lOPixels = new Color32[PaletteSize * liTables];

            for (int liTable = 0; liTable < liTables; liTable++)
            {
                for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
                {
                    byte lyResult = pOData.TransparencyTables.Apply((UWTransparencyTables.FadeTable)liTable, liIndex);
                    UWColor32 lOColour = fShown(lOPalette, lyResult, pbColourHelp);

                    lOPixels[(liTable * PaletteSize) + liIndex] = new Color32(lOColour.R, lOColour.G, lOColour.B, 255);
                }
            }

            lOTable.SetPixels32(lOPixels);
            lOTable.Apply(false, false);

            return lOTable;
        }

        /// <summary>
        /// The hallucination's light table effect (UWHallucinationState.LightTableEffect) as a
        /// colour table like BuildColourTable, but three times as tall: sixteen levels each for
        /// everything else, floors and walls (UWPaletteLookup's UWSurfacePart picks the part). For
        /// every index and level the colour of the index UWHallucinationState.BuildLightTableEffect
        /// maps it to - bands of green, grey and purple by distance (an approximation after the
        /// user's screenshots of the original).
        /// </summary>
        public static Texture2D BuildLightTableEffect(DataImport pOData, bool pbColourHelp = true)
        {
            if (pOData == null || pOData.Palettes == null)
                return null;

            byte[] lyTable = UWHallucinationState.BuildLightTableEffect();
            int liRows = lyTable.Length / PaletteSize;

            UWPalette lOPalette = pOData.Palettes.GetPalette(0);
            Texture2D lOTable = new Texture2D(PaletteSize, liRows, TextureFormat.RGBA32, false, false);

            lOTable.name = pbColourHelp ? "UWLightTableEffect" : "UWLightTableEffect (raw)";
            lOTable.filterMode = FilterMode.Point;
            lOTable.wrapMode = TextureWrapMode.Clamp;

            Color32[] lOPixels = new Color32[PaletteSize * liRows];

            for (int liLevel = 0; liLevel < liRows; liLevel++)
            {
                for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
                {
                    UWColor32 lOColour = fShown(lOPalette, lyTable[(liLevel * PaletteSize) + liIndex], pbColourHelp);

                    lOPixels[(liLevel * PaletteSize) + liIndex] = new Color32(lOColour.R, lOColour.G, lOColour.B, 255);
                }
            }

            lOTable.SetPixels32(lOPixels);
            lOTable.Apply(false, false);

            return lOTable;
        }

        /// <summary>A palette colour as the colour table shows it (colour help applied), or raw -
        /// Remastered draws the world in raw colours and puts the colour help on afterwards, in its
        /// post-processing (UWRemasterRenderer).</summary>
        private static UWColor32 fShown(UWPalette pOPalette, int piIndex, bool pbColourHelp = true)
        {
            UWColor32 lOColour = pOPalette.GetUWColor(piIndex);

            if (!pbColourHelp)
                return new UWColor32 { R = lOColour.R, G = lOColour.G, B = lOColour.B, A = 255 };
            Color32 lOShown = UWColourVision.Apply(new Color32(lOColour.R, lOColour.G, lOColour.B, 255));

            return new UWColor32 { R = lOShown.r, G = lOShown.g, B = lOShown.b, A = 255 };
        }

        /// <summary>
        /// The eight palettes as they are, one row each (256 by 8), for Remastered while the
        /// hallucination swaps the palette (UWHallucinationState.PaletteEffect): its surfaces know
        /// their palette index (the index array, the index atlases, the model faces' vertex) and
        /// take the colour of that index from the row of the hallucination's palette, then light it
        /// as usual. Raw colours - Remastered puts the colour help on in its post-processing.
        /// </summary>
        public static Texture2D BuildRawPalettes(DataImport pOData)
        {
            if (pOData == null || pOData.Palettes == null)
                return null;

            Texture2D lOTable = new Texture2D(PaletteSize, RawPaletteCount, TextureFormat.RGBA32, false, false);

            lOTable.name = "UWRawPalettes";
            lOTable.filterMode = FilterMode.Point;
            lOTable.wrapMode = TextureWrapMode.Clamp;

            Color32[] lOPixels = new Color32[PaletteSize * RawPaletteCount];

            for (int liPalette = 0; liPalette < RawPaletteCount; liPalette++)
            {
                UWPalette lOPalette;

                try
                {
                    lOPalette = pOData.Palettes.GetPalette(liPalette);
                }
                catch (System.Exception)
                {
                    lOPalette = pOData.Palettes.GetPalette(0);
                }

                for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
                {
                    UWColor32 lOColour = lOPalette.GetUWColor(liIndex);

                    lOPixels[(liPalette * PaletteSize) + liIndex] = new Color32(lOColour.R, lOColour.G, lOColour.B, 255);
                }
            }

            lOTable.SetPixels32(lOPixels);
            lOTable.Apply(false, false);

            return lOTable;
        }

        /// <summary>The palettes of PALS.DAT, the hallucination's choice (RNG &amp; 7).</summary>
        public const int RawPaletteCount = 8;

        /// <summary>Hands the shader the XFER.DAT lookups; pbReady off falls back to the fixed
        /// blend of the index atlas (what the colour path always uses).</summary>
        public static void ApplyXferGlobals(Texture3D pOInverseLookup, Texture2D pOXferColours, bool pbReady)
        {
            if (pOInverseLookup != null)
                Shader.SetGlobalTexture(InverseLookupProperty, pOInverseLookup);

            if (pOXferColours != null)
                Shader.SetGlobalTexture(XferColourProperty, pOXferColours);

            Shader.SetGlobalFloat(XferReadyProperty,
                pbReady && pOInverseLookup != null && pOXferColours != null ? 1f : 0f);
        }

        /// <summary>
        /// How many screen pixels one pixel of the original covers.
        ///
        /// The same calculation the UI is scaled with (see UWGameUI): the
        /// original draws at 320 by 200 and is fitted to the HEIGHT. The value is needed
        /// for the size of one cell in the dither pattern.
        ///
        /// With the world at the original's resolution (UWWorldResolution) one rendered pixel IS
        /// an original pixel, so a cell is one pixel - the window's factor made the checkerboard
        /// that much too coarse (per user, 2026-10-07, a big checkerboard on a dark wall). At a
        /// multiple of it the cell stays one original pixel, its multiple of rendered pixels, as
        /// the original would have dithered on a 320 by 200 screen.
        /// </summary>
        public static float GetOriginalPixelSize()
        {
            if (UWWorldResolution.Enabled)
                return UWWorldResolution.EffectiveFactor;

            return Mathf.Max(1f, Screen.height / 200f);
        }

        /// <summary>
        /// How far you can see at this light level, in world units. Beyond that everything is
        /// black; the original does not draw there at all.
        /// </summary>
        public static float GetCutoffDistance(DataImport pOData, int piLightLevel)
        {
            UWShades.Entry lOEntry;

            if (pOData == null || pOData.Shades == null
                || !pOData.Shades.TryGet(piLightLevel, out lOEntry))
                return UWLevelMeshBuilder.TileSpacing * 4f;

            return lOEntry.ViewingDistance * UWLevelMeshBuilder.TileSpacing;
        }
    }
}
