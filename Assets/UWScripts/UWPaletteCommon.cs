using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// WHAT BOTH RENDER PATHS ALWAYS NEED OF THE PALETTE (cleanup step 6.2, 2026-10-24): the lookup
/// tables and the hallucination feed. The interface icons (UWIconPalette), the hallucination,
/// the ghosts and Night Vision read these tables in Remastered too, so they live apart from the
/// palette path's material swap. Taken out of UWPaletteRenderToggle; UWRenderPathHost ticks it
/// every frame in both paths (since cleanup step 6.5, 2026-10-24).
///
/// TWO TABLE SETS (TableSet): SHOWN, in the colours on screen with the colour help baked in - the
/// palette path and the interface; RAW, without it - Remastered draws the world in raw colours
/// and puts the colour help on in its post-processing (UWRemasterRenderPath). Each set is the same
/// four tables with its own four global names; until 2026-10-24 they were eight loose fields.
///
/// LIFETIMES, as they were: the shown set, the shade, rotation, hallucination and Night Vision
/// tables belong to one toggle (one scene), the shown colour tables are built again when the
/// colour help changes; the raw set depends on the game data only and is kept across scenes (the
/// toggle is rebuilt with every loaded save; the cube alone takes a moment to build). The textures
/// are made by UWTextureFactory; whatever is destroyed here is dropped from UWShaderGlobals first.
/// </summary>
public sealed class UWPaletteCommon
{
    /// <summary>
    /// One set of the palette's colour lookups: the colours per palette index (Shown: the colour
    /// table of palette 0, LIGHT.DAT's sixteen rows; Raw: the eight palettes as they are), the
    /// hallucination's light table effect, and the two XFER.DAT lookups (the inverse cube and
    /// what every table makes of every index), with the shader globals each one goes to.
    /// </summary>
    public sealed class TableSet
    {
        public readonly bool ColourHelp;

        public readonly int ColoursId;

        public readonly int LightEffectId;

        public readonly int InverseLookupId;

        public readonly int XferColoursId;

        public Texture2D Colours { get; private set; }

        public Texture2D LightEffect { get; private set; }

        /// <summary>XFER.DAT as the original applies it: the picture back to its indices, and what
        /// every table makes of them (UWTextureFactory.InverseLookup, XferColours).</summary>
        public Texture3D InverseLookup { get; private set; }

        public Texture2D XferColours { get; private set; }

        public TableSet(bool pbColourHelp, int piColoursId, int piLightEffectId, int piInverseLookupId, int piXferColoursId)
        {
            ColourHelp = pbColourHelp;
            ColoursId = piColoursId;
            LightEffectId = piLightEffectId;
            InverseLookupId = piInverseLookupId;
            XferColoursId = piXferColoursId;
        }

        public void EnsureColours(DataImport pOData)
        {
            if (Colours == null)
                Colours = ColourHelp ? UWTextureFactory.ColourTable(pOData) : UWTextureFactory.RawPalettes(pOData);
        }

        public void EnsureLightEffect(DataImport pOData)
        {
            if (LightEffect == null)
                LightEffect = UWTextureFactory.LightTableEffect(pOData, ColourHelp);
        }

        public void EnsureXfer(DataImport pOData)
        {
            if (InverseLookup == null)
                InverseLookup = UWTextureFactory.InverseLookup(pOData, ColourHelp);

            if (XferColours == null)
                XferColours = UWTextureFactory.XferColours(pOData, ColourHelp);
        }

        /// <summary>The XFER lookups are there.</summary>
        public bool HasXfer => InverseLookup != null && XferColours != null;

        /// <summary>The tables the colour help is baked into, dropped to be built again - the
        /// colour table and the XFER lookups. The light table effect follows through
        /// DropLightEffect, from the hallucination feed that sets its global again.</summary>
        public void DropColourHelpTables()
        {
            Colours = fDestroy(Colours);
            InverseLookup = fDestroy(InverseLookup);
            XferColours = fDestroy(XferColours);
        }

        /// <summary>The light table effect dropped to be built again (UWPaletteCommon.FollowHallucination).</summary>
        public void DropLightEffect()
        {
            LightEffect = fDestroy(LightEffect);
        }

        public void DestroyAll()
        {
            DropColourHelpTables();
            LightEffect = fDestroy(LightEffect);
        }

        private static T fDestroy<T>(T pOTexture) where T : Texture
        {
            if (pOTexture != null)
            {
                UWShaderGlobals.Invalidate(pOTexture);
                Object.Destroy(pOTexture);
            }

            return null;
        }
    }

    /// <summary>The colours on screen, with the colour help: the palette path's world, the
    /// interface's icons, the palette path's ghosts.</summary>
    public readonly TableSet Shown = new TableSet(true, UWShaderIds.ColourTable, UWShaderIds.LightEffectTable,
        UWShaderIds.InverseLookup, UWShaderIds.XferColours);

    /// <summary>
    /// REMASTERED'S TABLES for the hallucination and the ghosts (UWHallucination.hlsl): the eight
    /// palettes, the light table effect, the inverse cube and the XFER colours, all in RAW colours -
    /// Remastered draws the world without the colour help and puts it on in its post-processing
    /// (UWRemasterRenderPath). They depend on the game data only, so they are kept across scenes
    /// (the toggle is rebuilt with every loaded save; the cube alone takes a moment to build).
    /// </summary>
    public static TableSet Raw { get; } = new TableSet(false, UWShaderIds.RemasterPalettes, UWShaderIds.RemasterLightEffect,
        UWShaderIds.RemasterInverseLookup, UWShaderIds.RemasterXferColours);

    private static DataImport msORawData;

    public Texture2D ShadeTable { get; private set; }

    public Texture2D RotationTable { get; private set; }

    /// <summary>Which state of the colour help the shown tables were built from.</summary>
    private int miColourVisionVersion = -1;

    /// <summary>
    /// The shown tables and the shade and rotation tables, built when missing; the shown colour
    /// tables again when the colour help changed (it is baked into them, see UWColourVision).
    /// True when the colour, shade and rotation tables are there (the XFER lookups may be
    /// missing - the ghosts then keep the fixed blend).
    /// </summary>
    public bool EnsureTables(DataImport pOData, bool pbDiagonalStretch)
    {
        if (Shown.Colours != null && miColourVisionVersion != UWColourVision.Version)
            Shown.DropColourHelpTables();

        if (Shown.Colours == null)
        {
            Shown.EnsureColours(pOData);
            miColourVisionVersion = UWColourVision.Version;
        }

        Shown.EnsureXfer(pOData);

        if (ShadeTable == null)
            ShadeTable = UWTextureFactory.ShadeTable(pOData, pbDiagonalStretch);

        if (RotationTable == null)
            RotationTable = UWTextureFactory.RotationTable();

        return Shown.Colours != null && ShadeTable != null && RotationTable != null;
    }

    /// <summary>
    /// The raw set, built when missing (for another data set anew), and handed to the shaders.
    /// True when the XFER lookups are there.
    /// </summary>
    public static bool EnsureRaw(DataImport pOData)
    {
        if (pOData == null)
            return false;

        if (msORawData != pOData)
        {
            Raw.DestroyAll();
            msORawData = pOData;
        }

        Raw.EnsureColours(pOData);
        Raw.EnsureLightEffect(pOData);
        Raw.EnsureXfer(pOData);

        if (Raw.Colours != null)
            UWShaderGlobals.SetTexture(Raw.ColoursId, Raw.Colours);

        if (Raw.LightEffect != null)
            UWShaderGlobals.SetTexture(Raw.LightEffectId, Raw.LightEffect);

        if (Raw.InverseLookup != null)
            UWShaderGlobals.SetTexture(Raw.InverseLookupId, Raw.InverseLookup);

        if (Raw.XferColours != null)
            UWShaderGlobals.SetTexture(Raw.XferColoursId, Raw.XferColours);

        return Raw.HasXfer;
    }

    // ------------------------------------------------- Hallucination feed

    private readonly UWHallucinationState mOHallucination = new UWHallucinationState();

    /// <summary>The hallucination's colour table while its palette effect shows another palette,
    /// else null.</summary>
    public Texture2D HallucinationTable { get; private set; }

    private bool mbHallucinationGlobalsSet;

    /// <summary>Which state of the colour help the hallucination's tables were built from.</summary>
    private int miHallucinationVision = -1;

    /// <summary>The colour table on screen now: the hallucination's, or the shown one.</summary>
    public Texture2D ShownColours => HallucinationTable != null ? HallucinationTable : Shown.Colours;

    /// <summary>The hallucination effect on screen now (UWHallucinationState), for the help
    /// window's hidden values - NoEffect while none runs.</summary>
    public int HallucinationEffect => mOHallucination.Effect;

    /// <summary>The palette of the palette effect.</summary>
    public int HallucinationPalette => mOHallucination.Palette;

    /// <summary>
    /// The hallucination's picture (UWHallucinationState): while its palette effect is on, the
    /// colours come from a table built from that palette instead of palette 0. Its globals are
    /// set only when its effect changes - once at the start as well, or a scene loaded after one
    /// with an effect would keep showing it.
    /// </summary>
    public void FollowHallucination(int piHallucination, DataImport pOData)
    {
        // THE COLOUR HELP IS BAKED INTO BOTH OF THE HALLUCINATION'S TABLES - the other palette's
        // colour table and the light table effect. Changed during a hallucination, they kept
        // the old help until the scene was rebuilt (found in cleanup 6.2, fixed per user
        // 2026-10-24); now both are built again at once, as the shown colour table is.
        bool lbVisionChanged = miHallucinationVision != UWColourVision.Version;

        if (lbVisionChanged)
        {
            miHallucinationVision = UWColourVision.Version;
            Shown.DropLightEffect();
        }

        if (mOHallucination.Update(piHallucination) || lbVisionChanged)
        {
            if (HallucinationTable != null)
            {
                UWShaderGlobals.Invalidate(HallucinationTable);
                Object.Destroy(HallucinationTable);
                HallucinationTable = null;
            }

            if (mOHallucination.Effect == UWHallucinationState.PaletteEffect && mOHallucination.Palette != 0 && pOData != null)
                HallucinationTable = UWTextureFactory.ColourTable(pOData, mOHallucination.Palette);

            fApplyHallucinationGlobals(pOData);
        }

        if (!mbHallucinationGlobalsSet)
        {
            mbHallucinationGlobalsSet = true;
            fApplyHallucinationGlobals(pOData);
        }
    }

    /// <summary>
    /// The two effects that act in the 3D view only (UWHallucinationState): the scrambled
    /// texture mapper - the masks for the 64 pixel walls and the 32 pixel floors and ceilings -
    /// and the light table that blackens nearly every index. Both are read by the palette
    /// shaders (UWDungeonPalette, UWPaletteLookup.hlsl) and by Remastered's (UWHallucination.hlsl,
    /// since 2026-09-30, with the palette effect's row as well); the interface keeps its colours,
    /// as the original's does (it draws without the light table and the mapper).
    /// </summary>
    private void fApplyHallucinationGlobals(DataImport pOData)
    {
        int[] liMasks = mOHallucination.ScrambleMasks;
        bool lbScramble = mOHallucination.Effect == UWHallucinationState.ScrambleEffect;

        UWShaderGlobals.SetVector(UWShaderIds.Scramble, new Vector4(liMasks[4], liMasks[3], lbScramble ? 1f : 0f, 0f));

        if (pOData != null && pOData.Textures != null)
        {
            UWShaderGlobals.SetFloat(UWShaderIds.WallSlices,
                pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.WALL).Count);
            UWShaderGlobals.SetFloat(UWShaderIds.FloorSlices,
                pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.FLOOR).Count);
        }

        if (pOData != null)
            Shown.EnsureLightEffect(pOData);

        if (Shown.LightEffect != null)
            UWShaderGlobals.SetTexture(Shown.LightEffectId, Shown.LightEffect);
        UWShaderGlobals.SetFloat(UWShaderIds.LightEffect,
            mOHallucination.Effect == UWHallucinationState.LightTableEffect ? 1f : 0f);

        // Remastered's other palette: the row of the raw palettes its surfaces take their colours
        // from (UWHallucination.hlsl), 0 while the effect is off.
        UWShaderGlobals.SetFloat(UWShaderIds.HallucinationPalette,
            mOHallucination.Effect == UWHallucinationState.PaletteEffect ? mOHallucination.Palette : 0f);
    }

    // ------------------------------------------------- Night Vision

    private Texture2D mOMonochromeTable;

    private int miMonochromePalette = -1;

    private int miMonochromeVision = -1;

    /// <summary>The world's colour table from MONO.DAT, in the palette on screen (the
    /// hallucination may swap it) and with the colour help; built when first needed and again
    /// when either changes.</summary>
    public Texture2D GetMonochromeTable(DataImport pOData)
    {
        int liPalette = mOHallucination.Effect == UWHallucinationState.PaletteEffect ? mOHallucination.Palette : 0;

        if (mOMonochromeTable != null && miMonochromePalette == liPalette && miMonochromeVision == UWColourVision.Version)
            return mOMonochromeTable;

        if (mOMonochromeTable != null)
        {
            UWShaderGlobals.Invalidate(mOMonochromeTable);
            Object.Destroy(mOMonochromeTable);
        }

        mOMonochromeTable = pOData != null ? UWTextureFactory.ColourTable(pOData, liPalette, true) : null;
        miMonochromePalette = liPalette;
        miMonochromeVision = UWColourVision.Version;

        return mOMonochromeTable;
    }
}
