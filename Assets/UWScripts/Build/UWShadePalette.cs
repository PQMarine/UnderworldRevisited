using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// GROUNDWORK FOR THE PALETTE RENDERER. Builds the lookup textures such a
    /// renderer needs. They are used by UWRenderPathHost and the palette path, the default render
    /// path (UWSettings.RenderMode Palette); the detail level of the Graphics menu
    /// (UWGraphicsDetail.SetLevel, every level above Low) switches to the URP lighting.
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
    /// Since cleanup step 6.2 (2026-10-24) the tables are no longer built in this class: their
    /// bytes come engine-free from UWLookupTables, the textures from UWTextureFactory (which knows
    /// each table's colour space, see COLOUR SPACE below), and UWPaletteCommon owns them. What
    /// stays here is how the shaders get them (ApplyGlobals, ApplyXferGlobals) and the sizes.
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
        public const int PaletteSize = UWLookupTables.PaletteSize;

        /// <summary>Brightness steps, i.e. rows of the colour table. Corresponds to the blocks in
        /// LIGHT.DAT.</summary>
        public const int ShadeLevelCount = UWLookupTables.ShadeLevelCount;

        /// <summary>Tile distances, i.e. columns of the shade table.</summary>
        public const int DistanceCount = UWLookupTables.DistanceCount;

        /// <summary>Player light levels, i.e. rows of the shade table.</summary>
        public const int LightLevelCount = UWLookupTables.LightLevelCount;

        /// <summary>Height of the rotation table - least common multiple of both
        /// rotation cycles.</summary>
        public static int RotationStepCount
        {
            get { return UWLookupTables.RotationStepCount; }
        }

        // The names of the tables in the shader live in UWShaderIds since 2026-10-24 (ColourTable,
        // WorldColourTable, ShadeTable, RotationTable, the XFER lookups).

        /// <summary>The light level at which the original loads MONO.DAT as its light table
        /// (OpenAndApplyShadesDat_ovr142_0, read 2026-10-01): 5, the level of Night Vision - light
        /// sources reach 4 at most.</summary>
        public const int MonochromeLightLevel = 5;

        /// <summary>
        /// Sets everything the palette shaders need per frame as global shader values -
        /// so not a single material has to know them.
        ///
        /// Meant as the ONLY hookup of the palette renderer: whoever switches over calls this
        /// once per frame and is done. The three tables never change and are built once;
        /// the per-frame values are light level, rotation step and the switches.
        ///
        /// In the original the rotation step advances at a fixed speed, independent
        /// of the frame rate - the caller calculates it from UWSettings.PaletteRotationStepsPerSecond
        /// (see UWRenderPathHost.Update), the same way UWAnimatedTextures does.
        ///
        /// Through UWShaderGlobals since 2026-10-24 (cleanup step 6.1): called every frame as
        /// before, but only what changed is written - in a standing scene the rotation step and,
        /// with a flickering light, the light level.
        /// </summary>
        public static void ApplyGlobals(Texture2D pOColourTable, Texture2D pOShadeTable,
            Texture2D pORotationTable, int piLightLevel, int piRotationStep,
            bool pbInterpolateLevels, bool pbDither,
            bool pbOrderedMatrix, float pfDitherScale, bool pbFullBright = false)
        {
            if (pOColourTable != null)
                UWShaderGlobals.SetTexture(UWShaderIds.ColourTable, pOColourTable);

            if (pOShadeTable != null)
                UWShaderGlobals.SetTexture(UWShaderIds.ShadeTable, pOShadeTable);

            if (pORotationTable != null)
                UWShaderGlobals.SetTexture(UWShaderIds.RotationTable, pORotationTable);

            int liSteps = RotationStepCount;

            UWShaderGlobals.SetFloat(UWShaderIds.LightLevel,
                Mathf.Clamp(piLightLevel, 0, LightLevelCount - 1));
            UWShaderGlobals.SetFloat(UWShaderIds.RotationStep, ((piRotationStep % liSteps) + liSteps) % liSteps);
            UWShaderGlobals.SetFloat(UWShaderIds.RotationStepCount, liSteps);
            UWShaderGlobals.SetFloat(UWShaderIds.TileSpacing, UWWorldScale.TileSize);
            UWShaderGlobals.SetFloat(UWShaderIds.InterpolateLevels, pbInterpolateLevels ? 1f : 0f);
            UWShaderGlobals.SetFloat(UWShaderIds.Dither, pbDither ? 1f : 0f);
            UWShaderGlobals.SetFloat(UWShaderIds.DitherPattern, pbOrderedMatrix ? 1f : 0f);
            UWShaderGlobals.SetFloat(UWShaderIds.DitherScale, Mathf.Max(1f, pfDitherScale));
            UWShaderGlobals.SetFloat(UWShaderIds.FullBright, pbFullBright ? 1f : 0f);
        }

        /// <summary>Hands the shader the XFER.DAT lookups; pbReady off falls back to the fixed
        /// blend of the index atlas (what the colour path always uses).</summary>
        public static void ApplyXferGlobals(Texture3D pOInverseLookup, Texture2D pOXferColours, bool pbReady)
        {
            if (pOInverseLookup != null)
                UWShaderGlobals.SetTexture(UWShaderIds.InverseLookup, pOInverseLookup);

            if (pOXferColours != null)
                UWShaderGlobals.SetTexture(UWShaderIds.XferColours, pOXferColours);

            UWShaderGlobals.SetFloat(UWShaderIds.XferReady,
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

    }
}
