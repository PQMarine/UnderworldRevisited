using System.Collections.Generic;
using UnityEngine;
using UnderworldRevisited.Build;
using UWDataImport.UWData;

/// <summary>
/// Switches between the palette renderer and the earlier URP lighting (key F6).
///
/// THE PALETTE PATH IS NOW THE DEFAULT - UWSettings.RenderMode is set to Palette,
/// and the level loader attaches this component itself on start. F6 now only serves for
/// comparison with the old path.
///
/// Implemented as a material swap after the fact and not as a separate build path: the swap
/// is needed for F6 anyway, and both paths share meshes, UVs and
/// slice numbers. A second build path would be double work with no gain.
///
/// WHAT GETS SWITCHED: everything that runs on the dungeon shader. That is the
/// geometry chunks and everything sharing their material - door frame narrow sides,
/// secret doors and the wall-textured faces of real 3D models (see UWObjectSpawner).
///
/// SPRITES AND CREATURES are switched as well: they run on UW/Billboard and
/// get the index version of their atlas (see UWObjectAtlasBuilder and
/// UWCritterAtlasBuilder, both from the same packing run as the colour atlas, so the UVs
/// remain valid unchanged). Inscriptions on walls (UW/Decal) get UW/DecalPalette the same way.
///
/// THE SOLID-COLOUR FACES of the 3D models (UWLevelLoader.ModelSolidMaterial) and the
/// portcullis (UWLevelLoader.PortcullisMaterial) get UW/ModelPalette: in the colour path
/// they carry their colour as vertex colour and multiply it with a white tile - an index
/// cannot be multiplied, so the palette version reads the index from the vertex instead.
///
/// The UI is not switched here: the icons always go through the palette anyway
/// (see UWIconPalette).
///
/// THE PLAYER LIGHT IS OFF in the palette path (see fSetSideEffects). The palette shader
/// calculates only from light level and distance, an additional URP light would take effect twice.
///
/// What TO EXPECT, so that bugs can be told apart from intent:
///
///   - hard brightness steps instead of a smooth falloff, in rings around the player. That is
///     correct; the original switches in sixteen steps via a table.
///   - a fixed view distance beyond which it turns black - three tiles without light, seven
///     with the brightest light source.
///   - no effect at all of wall torches on the walls. In the original they light up nothing.
///   - flickering on distant floors, because index textures cannot have mipmaps.
///   - water and lava keep moving, but now via the rotation table instead of
///     pre-baked images.
/// </summary>
public class UWPaletteRenderToggle : MonoBehaviour
{
    /// <summary>Names of the shaders whose material is swapped, and their
    /// palette version.</summary>
    private const string DungeonShaderName = "UW/Dungeon";

    private const string BillboardShaderName = "UW/Billboard";

    private const string PaletteBillboardShaderName = "UW/BillboardPalette";

    private const string PaletteModelShaderName = "UW/ModelPalette";

    private const string DecalShaderName = "UW/Decal";

    private const string PaletteDecalShaderName = "UW/DecalPalette";

    /// <summary>
    /// Use intermediate steps between two whole tile distances instead of taking the step of
    /// the whole tile.
    ///
    /// ON BY DEFAULT: the shading table jumps in steps of four, taken raw this
    /// produces rings with hard edges. In the original the transitions on the floor are
    /// visibly softer (per user in an image comparison, 2026-09-05). Turn off only for
    /// comparison.
    /// </summary>
    [Tooltip("Use intermediate steps between two tile distances. Off gives hard rings - for comparison only.")]
    [SerializeField]
    private bool mbInterpolateLevels = true;

    /// <summary>
    /// Dither the intermediate step instead of rounding it.
    ///
    /// ON BY DEFAULT. An 8-bit renderer cannot paint intermediate brightness; the
    /// original therefore spreads the two neighbouring steps across the surface, which gives the
    /// textures a rougher tone (per user, 2026-09-05). Off gives smooth but
    /// flatter surfaces - for comparison.
    /// </summary>
    [Tooltip("Dither intermediate steps instead of rounding. Gives the textures the rougher tone of the original.")]
    [SerializeField]
    private bool mbDither = true;

    /// <summary>
    /// Use an ordered 4 by 4 matrix instead of the checkerboard.
    ///
    /// OFF BY DEFAULT. The user took a close-up of the original and sees
    /// a clean checkerboard pattern there (2026-09-05), i.e. exactly one intermediate step.
    /// The matrix gives sixteen gradations and looks grainy - for comparison only.
    /// </summary>
    [Tooltip("Ordered 4x4 matrix instead of checkerboard. Finer gradation, but grainy instead of checkered - for comparison only.")]
    [SerializeField]
    private bool mbOrderedMatrix;

    /// <summary>
    /// Size of one cell of the dither pattern in screen pixels. Zero means: calculate from the
    /// window height, so that one cell corresponds to exactly one original pixel.
    /// </summary>
    [Tooltip("Cell size of the dither pattern in screen pixels. 0 = calculate from the window height (one original pixel).")]
    [SerializeField]
    private float mfDitherScale;

    /// <summary>
    /// Stretch the distance by the square root of two before shading, as the reference does.
    ///
    /// OFF BY DEFAULT. With the stretch the light reaches about one tile less far
    /// than in the original (per user in an image comparison, 2026-09-05). See
    /// UWShades.GetShadeTable.
    /// </summary>
    [Tooltip("Stretch the distance by the square root of two before shading, as in the reference. Off matches the original better.")]
    [SerializeField]
    private bool mbDiagonalStretch;

    private UWLevelLoader mOLevelLoader;
    private UWLighting mOLighting;

    private Texture2DArray mOIndexArray;
    private Texture2D mOColourTable;

    /// <summary>Which state of the colour help the table was built from.</summary>
    private int miColourVisionVersion = -1;
    private Texture2D mOShadeTable;
    private Texture2D mORotationTable;

    /// <summary>XFER.DAT as the original applies it: the picture back to its indices, and what
    /// every table makes of them (UWShadePalette.BuildInverseLookup, BuildXferColours). Built
    /// with the colour table, as they follow the colour help too.</summary>
    private Texture3D mOInverseLookup;

    private Texture2D mOXferColours;

    private Material mOPaletteMaterial;

    private Material mOModelMaterial;

    private Material mOPortcullisMaterial;

    private readonly List<MeshRenderer> mORenderers = new List<MeshRenderer>();

    /// <summary>The original material list per renderer. A LIST, because the
    /// 3D models carry three materials - wall texture, atlas graphic and solid-colour
    /// faces - and swapping only the first would mean forgetting two thirds.</summary>
    private readonly List<Material[]> mOOriginals = new List<Material[]>();

    /// <summary>For each colour texture its index version. Fetched anew on every apply,
    /// because creature atlases only come into being when the first creature of their kind appears.
    /// </summary>
    private readonly Dictionary<Texture, Texture> mOIndexTextures = new Dictionary<Texture, Texture>();

    /// <summary>For each sprite material its palette version, so that not every
    /// apply creates new materials.</summary>
    private readonly Dictionary<Material, Material> mOPaletteMaterials = new Dictionary<Material, Material>();

    private bool mbActive;

    /// <summary>Whether the palette path is currently running - for UWSettings.IsRemasteredActive.</summary>
    public static bool IsPaletteActive { get; private set; }

    private int miAppliedLevel = -1;

    /// <summary>Number of creature atlases at the last swap - see Update.</summary>
    private int miAppliedAtlasCount = -1;
    private bool mbStarted;

    /// <summary>Switch on by itself at start if the settings ask for it. Only in
    /// Update, because the level loader builds its materials and atlases in its own Start
    /// - before that there would be nothing to swap.</summary>
    private void fAutoStart()
    {
        if (mbStarted || mbActive)
            return;

        if (UnderworldRevisited.UWSettings.Instance == null
            || UnderworldRevisited.UWSettings.Instance.RenderMode != UnderworldRevisited.UWSettings.RenderModeEnum.Palette)
            return;

        if (mOLevelLoader == null)
            mOLevelLoader = GetComponent<UWLevelLoader>();

        if (mOLevelLoader == null || mOLevelLoader.ObjectAtlas == null)
            return;

        mbStarted = true;

        Toggle();
    }

    /// <summary>
    /// Everything at brightness step zero - the original colours without any shading.
    ///
    /// BOUND TO THE SAME KEY as the full brightness of the old render path (B, see
    /// UWLighting.ToggleDebugBrightness). A separate switch would only be confusing: the
    /// old path brightens via the ambient light, the palette path cannot do that - it
    /// knows no light sources but calculates from light level and distance (see
    /// UWShades). The highest light level alone is not enough for this: it is bright
    /// up close but still darkens with distance. That is why full bright bypasses the calculation
    /// in the shader entirely and takes step zero.
    /// </summary>
    public bool FullBright
    {
        get
        {
            if (mOLighting == null)
                mOLighting = GetComponent<UWLighting>();

            return mOLighting != null && mOLighting.IsBright;
        }
    }

    /// <summary>
    /// Forgets what was swapped last - the swap then runs over everything again on the next
    /// frame.
    ///
    /// Called after every rebuild of a level. Until 2026-09-06 the swap depended
    /// only on the LEVEL NUMBER: if you jumped with F7 to a tile on the SAME level,
    /// the loader rebuilt everything, but the number stayed the same - and the fresh geometry
    /// was left on the URP materials. It looked as if the jump had switched the
    /// render path (per user, jump to 7,40,20).
    /// </summary>
    public void Reapply()
    {
        miAppliedLevel = -1;
        miAppliedAtlasCount = -1;
    }

    public bool IsActive
    {
        get { return mbActive; }
    }

    /// <summary>When the scene is rebuilt (loading a save game) the toggle disappears - the
    /// static state must not outlive it.</summary>
    private void OnDestroy()
    {
        if (mbActive)
            IsPaletteActive = false;
    }

    /// <summary>Toggle. On the first switch-on the tables are built, afterwards
    /// they stay around - a switch then costs nothing more.</summary>
    public void Toggle()
    {
        if (mbActive)
        {
            fRestore();

            mbActive = false;
            IsPaletteActive = false;
            mbStarted = true;

            fSetSideEffects(false);

            Debug.Log("Render path: URP lighting");

            return;
        }

        if (!fEnsureTables())
        {
            Debug.LogError("Render path: palette tables could not be built");
            return;
        }

        mbActive = true;
        IsPaletteActive = true;
        mbStarted = true;
        miAppliedLevel = -1;

        fSetSideEffects(true);

        Debug.Log("Render path: Palette (SHADES.DAT and LIGHT.DAT)");
    }

    /// <summary>
    /// What else besides the materials depends on the render path.
    ///
    /// POINT LIGHT: in the palette path there are no local light sources, the brightness
    /// comes only from light level and distance. An additional URP light would take effect
    /// twice.
    ///
    /// TEXTURE ANIMATION: UWAnimatedTextures copies pre-baked water and lava images
    /// into the slices of the COLOUR array every frame. The palette path does not use this array
    /// at all - it rotates the whole palette via a single number in the shader. The copying
    /// would be pure waste.
    /// </summary>
    private void fSetSideEffects(bool pbPalette)
    {
        if (mOLighting == null)
            mOLighting = GetComponent<UWLighting>();

        if (mOLighting != null)
            mOLighting.SuppressPlayerLight = pbPalette;

        UWAnimatedTextures lOAnimator = GetComponent<UWAnimatedTextures>();

        if (lOAnimator != null)
            lOAnimator.enabled = !pbPalette;
    }

    private void Update()
    {
        fAutoStart();

        if (mOLevelLoader == null)
            mOLevelLoader = GetComponent<UWLevelLoader>();

        if (mOLighting == null)
            mOLighting = GetComponent<UWLighting>();

        // The lookup textures are set IN THE OLD RENDER PATH AS WELL: the
        // UI icons always go through the palette (see UWIconPalette), even
        // when the world is currently lit with URP. Without this their rotation froze
        // as soon as you switched with F6.
        if (!fEnsureTables())
            return;

        // After a level change there are new chunks that carry the old material again.
        // Only in the palette path - in the old path nothing is to be swapped.
        //
        // ADDITIONALLY for a new CREATURE KIND: its atlas only comes into being when the first
        // creature of this kind appears (see UWCritterAnimator). If one joins after the swap
        // - behind a secret wall just opened, for example -, the
        // colour atlas -> index atlas mapping does not know its atlas yet, the swap fails
        // silently, and exactly this creature stays on the old path. It stands out immediately,
        // because the player light is off in the palette path: the creature then appears dark.
        int liAtlasCount = UWCritterAnimator.AtlasCount;

        if (mbActive && mOLevelLoader != null
            && (mOLevelLoader.CurrentLevelIndex != miAppliedLevel || liAtlasCount != miAppliedAtlasCount))
        {
            fApply();

            miAppliedLevel = mOLevelLoader.CurrentLevelIndex;
            miAppliedAtlasCount = liAtlasCount;
        }

        int liLightLevel = mOLighting != null ? mOLighting.LightLevel : 0;

        float lfSteps = UnderworldRevisited.UWSettings.Instance != null
            ? UnderworldRevisited.UWSettings.Instance.PaletteRotationStepsPerSecond
            : 4f;

        fFollowHallucination();

        // The hallucination's globals are set only when its effect changes - once at the start as
        // well, or a scene loaded after one with an effect would keep showing it.
        if (!mbHallucinationGlobalsSet)
        {
            mbHallucinationGlobalsSet = true;
            fApplyHallucinationGlobals();
        }

        UWShadePalette.ApplyGlobals(mOHallucinationTable != null ? mOHallucinationTable : mOColourTable,
            mOShadeTable, mORotationTable, liLightLevel,
            Mathf.FloorToInt(Time.time * lfSteps),
            UWShadePalette.GetCutoffDistance(mOLevelLoader != null ? mOLevelLoader.UWDataImporter : null,
                liLightLevel),
            mbInterpolateLevels, mbDither, mbOrderedMatrix,
            mfDitherScale > 0f ? mfDitherScale : UWShadePalette.GetOriginalPixelSize(),
            FullBright && mbActive);

        // NIGHT VISION: at light level 5 the original loads MONO.DAT as its light table
        // (OpenAndApplyShadesDat_ovr142_0, per user 2026-10-01: the view turns grey in the
        // original, not in ours). For the world only - the interface keeps its colours.
        Texture2D lOWorldTable = mOHallucinationTable != null ? mOHallucinationTable : mOColourTable;

        if (liLightLevel == UWShadePalette.MonochromeLightLevel && !(FullBright && mbActive))
            lOWorldTable = fGetMonochromeTable() ?? lOWorldTable;

        Shader.SetGlobalTexture(UWShadePalette.WorldColourTableProperty, lOWorldTable);

        // Only while the hallucination does not swap the palette: the lookups are built from
        // palette 0.
        bool lbXfer = mbActive && mOHallucinationTable == null;

        UWShadePalette.ApplyXferGlobals(mOInverseLookup, mOXferColours, lbXfer);

        // Remastered's ghosts through the same tables, in raw colours (UWHallucination.hlsl) - on
        // the whole URP path, F6's comparison included, whose sprites read the same tables. Not in
        // the frames before the palette path switches itself on (fAutoStart).
        bool lbUrpPath = !mbActive && (mbStarted || UnderworldRevisited.UWSettings.Instance == null
            || UnderworldRevisited.UWSettings.Instance.RenderMode != UnderworldRevisited.UWSettings.RenderModeEnum.Palette);
        bool lbRemasterXfer = lbUrpPath && fEnsureRemasterTables() && mOHallucinationTable == null;

        Shader.SetGlobalFloat(RemasterXferProperty, lbRemasterXfer ? 1f : 0f);
        fRequireSceneColour(lbXfer || lbRemasterXfer, mbActive);

        // The hallucination's palette reaches the whole interface, as the VGA palette does - in
        // either render path, the interface goes through the palette in both.
        UWUiPaletteSwap.Follow(mOHallucinationTable != null && mOInverseLookup != null);
    }

    private const string RemasterXferProperty = "_UWRemasterXfer";

    /// <summary>The world's colour table from MONO.DAT, in the palette on screen (the
    /// hallucination may swap it) and with the colour help; built when first needed and again
    /// when either changes.</summary>
    private Texture2D fGetMonochromeTable()
    {
        int liPalette = mOHallucination.Effect == UWHallucinationState.PaletteEffect ? mOHallucination.Palette : 0;

        if (mOMonochromeTable != null && miMonochromePalette == liPalette && miMonochromeVision == UWColourVision.Version)
            return mOMonochromeTable;

        if (mOMonochromeTable != null)
            Destroy(mOMonochromeTable);

        mOMonochromeTable = mOLevelLoader != null && mOLevelLoader.UWDataImporter != null
            ? UWShadePalette.BuildColourTable(mOLevelLoader.UWDataImporter, liPalette, true)
            : null;
        miMonochromePalette = liPalette;
        miMonochromeVision = UWColourVision.Version;

        return mOMonochromeTable;
    }

    private Texture2D mOMonochromeTable;

    private int miMonochromePalette = -1;

    private int miMonochromeVision = -1;

    /// <summary>
    /// REMASTERED'S TABLES for the hallucination and the ghosts (UWHallucination.hlsl): the eight
    /// palettes, the light table effect, the inverse cube and the XFER colours, all in RAW colours -
    /// Remastered draws the world without the colour help and puts it on in its post-processing
    /// (UWRemasterRenderer). They depend on the game data only, so they are kept across scenes
    /// (the toggle is rebuilt with every loaded save; the cube alone takes a moment to build).
    /// True when the XFER lookups are there.
    /// </summary>
    private bool fEnsureRemasterTables()
    {
        UWDataImport.DataImport lOData = mOLevelLoader != null ? mOLevelLoader.UWDataImporter : null;

        if (lOData == null)
            return false;

        if (msORawData != lOData)
        {
            fDestroyRawTables();
            msORawData = lOData;
        }

        if (msORawPalettes == null)
            msORawPalettes = UWShadePalette.BuildRawPalettes(lOData);

        if (msORawLightEffect == null)
            msORawLightEffect = UWShadePalette.BuildLightTableEffect(lOData, false);

        if (msORawInverseLookup == null)
            msORawInverseLookup = UWShadePalette.BuildInverseLookup(lOData, false);

        if (msORawXferColours == null)
            msORawXferColours = UWShadePalette.BuildXferColours(lOData, false);

        if (msORawPalettes != null)
            Shader.SetGlobalTexture("_UWRemasterPalettes", msORawPalettes);

        if (msORawLightEffect != null)
            Shader.SetGlobalTexture("_UWRemasterLightEffect", msORawLightEffect);

        if (msORawInverseLookup != null)
            Shader.SetGlobalTexture("_UWRemasterInverseLookup", msORawInverseLookup);

        if (msORawXferColours != null)
            Shader.SetGlobalTexture("_UWRemasterXferColours", msORawXferColours);

        return msORawInverseLookup != null && msORawXferColours != null;
    }

    private static void fDestroyRawTables()
    {
        foreach (Texture lOTable in new Texture[] { msORawPalettes, msORawLightEffect, msORawInverseLookup, msORawXferColours })
        {
            if (lOTable != null)
                Destroy(lOTable);
        }

        msORawPalettes = null;
        msORawLightEffect = null;
        msORawInverseLookup = null;
        msORawXferColours = null;
    }

    private static UWDataImport.DataImport msORawData;

    private static Texture2D msORawPalettes;

    private static Texture2D msORawLightEffect;

    private static Texture3D msORawInverseLookup;

    private static Texture2D msORawXferColours;

    private bool mbHallucinationGlobalsSet;

    /// <summary>
    /// The translucent pixels read what lies behind them (URP's opaque texture, a copy of the
    /// picture after everything solid is drawn - UWBillboardPalette). The copy costs a pass, so
    /// the camera asks for it only while the palette path mixes that way.
    ///
    /// The DEPTH texture the whole time the palette path is on: the renderer draws it in a
    /// prepass (UWUniversalRenderer, copy depth mode "force prepass"), and the sprites read the
    /// geometry behind them from it (UWPainterOrder.hlsl, UWPainterSpriteVisible).
    /// </summary>
    private void fRequireSceneColour(bool pbRequired, bool pbDepth)
    {
        fRequireSceneColour(Camera.main, pbRequired, pbDepth);

        // The remote view (Roaming Sight, the camera trap) is a second camera, and its
        // translucent pixels need the copy of ITS picture.
        if (UWRemoteCamera.Current != null)
            fRequireSceneColour(UWRemoteCamera.Current.ViewCamera, pbRequired, pbDepth);
    }

    private static void fRequireSceneColour(Camera pOCamera, bool pbRequired, bool pbDepth)
    {
        Camera lOCamera = pOCamera;

        if (lOCamera == null)
            return;

        UnityEngine.Rendering.Universal.UniversalAdditionalCameraData lOData =
            lOCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

        if (lOData == null)
            return;

        UnityEngine.Rendering.Universal.CameraOverrideOption leWanted = pbRequired
            ? UnityEngine.Rendering.Universal.CameraOverrideOption.On
            : UnityEngine.Rendering.Universal.CameraOverrideOption.UsePipelineSettings;

        if (lOData.requiresColorOption != leWanted)
            lOData.requiresColorOption = leWanted;

        UnityEngine.Rendering.Universal.CameraOverrideOption leDepth = pbDepth
            ? UnityEngine.Rendering.Universal.CameraOverrideOption.On
            : UnityEngine.Rendering.Universal.CameraOverrideOption.UsePipelineSettings;

        if (lOData.requiresDepthOption != leDepth)
            lOData.requiresDepthOption = leDepth;
    }

    /// <summary>The hallucination's picture (UWHallucinationState): while its palette effect is
    /// on, the colours come from a table built from that palette instead of palette 0.</summary>
    private void fFollowHallucination()
    {
        UWCharacter lOCharacter = UWScene.Character;

        if (!mOHallucination.Update(lOCharacter != null ? lOCharacter.Hallucination : 0))
            return;

        if (mOHallucinationTable != null)
        {
            Destroy(mOHallucinationTable);
            mOHallucinationTable = null;
        }

        if (mOHallucination.Effect == UWHallucinationState.PaletteEffect && mOHallucination.Palette != 0
            && mOLevelLoader != null && mOLevelLoader.UWDataImporter != null)
            mOHallucinationTable = UWShadePalette.BuildColourTable(mOLevelLoader.UWDataImporter, mOHallucination.Palette);

        fApplyHallucinationGlobals();
    }

    /// <summary>
    /// The two effects that act in the 3D view only (UWHallucinationState): the scrambled
    /// texture mapper - the masks for the 64 pixel walls and the 32 pixel floors and ceilings -
    /// and the light table that blackens nearly every index. Both are read by the palette
    /// shaders (UWDungeonPalette, UWPaletteLookup.hlsl) and by Remastered's (UWHallucination.hlsl,
    /// since 2026-09-30, with the palette effect's row as well); the interface keeps its colours,
    /// as the original's does (it draws without the light table and the mapper).
    /// </summary>
    private void fApplyHallucinationGlobals()
    {
        int[] liMasks = mOHallucination.ScrambleMasks;
        bool lbScramble = mOHallucination.Effect == UWHallucinationState.ScrambleEffect;

        Shader.SetGlobalVector(ScrambleProperty, new Vector4(liMasks[4], liMasks[3], lbScramble ? 1f : 0f, 0f));

        if (mOLevelLoader != null && mOLevelLoader.UWDataImporter != null && mOLevelLoader.UWDataImporter.Textures != null)
        {
            Shader.SetGlobalFloat("_UWWallSlices",
                mOLevelLoader.UWDataImporter.Textures.GetTexturesByType(UWDataImport.UWData.UWTexture.TextureTypes.WALL).Count);
            Shader.SetGlobalFloat("_UWFloorSlices",
                mOLevelLoader.UWDataImporter.Textures.GetTexturesByType(UWDataImport.UWData.UWTexture.TextureTypes.FLOOR).Count);
        }

        if (mOLightEffectTable == null && mOLevelLoader != null && mOLevelLoader.UWDataImporter != null)
            mOLightEffectTable = UWShadePalette.BuildLightTableEffect(mOLevelLoader.UWDataImporter);

        if (mOLightEffectTable != null)
            Shader.SetGlobalTexture("_UWLightEffectTable", mOLightEffectTable);
        Shader.SetGlobalFloat("_UWLightEffect",
            mOHallucination.Effect == UWHallucinationState.LightTableEffect ? 1f : 0f);

        // Remastered's other palette: the row of the raw palettes its surfaces take their colours
        // from (UWHallucination.hlsl), 0 while the effect is off.
        Shader.SetGlobalFloat("_UWHallucinationPalette",
            mOHallucination.Effect == UWHallucinationState.PaletteEffect ? mOHallucination.Palette : 0f);
    }

    private const string ScrambleProperty = "_UWScramble";

    /// <summary>The hallucination effect on screen now (UWHallucinationState), for the help
    /// window's hidden values - NoEffect while none runs.</summary>
    public int HallucinationEffect => mOHallucination.Effect;

    /// <summary>The palette of the palette effect.</summary>
    public int HallucinationPalette => mOHallucination.Palette;

    private Texture2D mOLightEffectTable;

    private readonly UWHallucinationState mOHallucination = new UWHallucinationState();

    private Texture2D mOHallucinationTable;

    private bool fEnsureTables()
    {
        if (mOLevelLoader == null)
            mOLevelLoader = GetComponent<UWLevelLoader>();

        if (mOLevelLoader == null || mOLevelLoader.UWDataImporter == null)
            return false;

        // The colour help is baked into the table, so a change means building it again
        // (see UWColourVision).
        if (mOColourTable != null && miColourVisionVersion != UWColourVision.Version)
        {
            Destroy(mOColourTable);
            mOColourTable = null;

            if (mOInverseLookup != null)
                Destroy(mOInverseLookup);

            if (mOXferColours != null)
                Destroy(mOXferColours);

            mOInverseLookup = null;
            mOXferColours = null;
        }

        if (mOColourTable == null)
        {
            mOColourTable = UWShadePalette.BuildColourTable(mOLevelLoader.UWDataImporter);
            miColourVisionVersion = UWColourVision.Version;
        }

        if (mOInverseLookup == null)
            mOInverseLookup = UWShadePalette.BuildInverseLookup(mOLevelLoader.UWDataImporter);

        if (mOXferColours == null)
            mOXferColours = UWShadePalette.BuildXferColours(mOLevelLoader.UWDataImporter);

        if (mOShadeTable == null)
            mOShadeTable = UWShadePalette.BuildShadeTable(mOLevelLoader.UWDataImporter,
                mbDiagonalStretch);

        if (mORotationTable == null)
            mORotationTable = UWShadePalette.BuildRotationTable();

        if (mOIndexArray == null)
            mOIndexArray = UWTextureArrayBuilder.BuildIndexed(mOLevelLoader.UWDataImporter);

        if (mOColourTable == null || mOShadeTable == null || mORotationTable == null
            || mOIndexArray == null)
            return false;

        if (mOPaletteMaterial == null)
            mOPaletteMaterial = UWLevelAssembler.CreateMaterial(mOIndexArray, "UW Dungeon (Palette)",
                UnderworldRevisited.UWSettings.RenderModeEnum.Palette);

        return mOPaletteMaterial != null;
    }

    /// <summary>Swaps the material on everything that has a palette version (see
    /// fGetPaletteMaterial), and remembers what was on it before.</summary>
    private void fApply()
    {
        fRestore();
        fCollectIndexTextures();

        foreach (MeshRenderer lORenderer in GetComponentsInChildren<MeshRenderer>(true))
            fSwapRenderer(lORenderer);
    }

    /// <summary>
    /// Puts an object created AFTER THE FACT onto the palette path.
    ///
    /// fApply runs once per level over everything present at that time. Whatever appears later -
    /// a dropped item, a hit effect, the remains of a creature,
    /// the secret door behind an opened wall - otherwise kept the URP materials
    /// and was the only thing lit by the player light. It looked as if exactly this
    /// object was still running on the old renderer, and that is exactly what it did (per user,
    /// 2026-09-06, at the secret door on level 3).
    ///
    /// It was only noticed now because before it only affected short-lived things: a
    /// blood splatter is visible for less than a second.
    ///
    /// In the old render path a no-op - nothing is to be swapped there.
    /// </summary>
    public void ApplyToNewObject(GameObject pOSpawned)
    {
        if (pOSpawned == null || !mbActive || mOPaletteMaterial == null)
            return;

        // Anew EVERY TIME: a freshly created creature can be the first of its kind, and
        // then its atlas has only just come into being. The mapping is then outdated,
        // even if it is not empty. The cost is two small caches.
        fCollectIndexTextures();

        foreach (MeshRenderer lORenderer in pOSpawned.GetComponentsInChildren<MeshRenderer>(true))
            fSwapRenderer(lORenderer);
    }

    /// <summary>Swaps the materials of ONE renderer and remembers what was on it
    /// before - so that F6 back restores the old state.</summary>
    private void fSwapRenderer(MeshRenderer pORenderer)
    {
        if (pORenderer == null)
            return;

        Material[] lOMaterials = pORenderer.sharedMaterials;

        if (lOMaterials == null || lOMaterials.Length == 0)
            return;

        Material[] lOReplacements = new Material[lOMaterials.Length];
        bool lbAny = false;

        for (int liAt = 0; liAt < lOMaterials.Length; liAt++)
        {
            lOReplacements[liAt] = lOMaterials[liAt];

            Material lOSwapped = fGetPaletteMaterial(lOMaterials[liAt]);

            if (lOSwapped == null)
                continue;

            lOReplacements[liAt] = lOSwapped;
            lbAny = true;
        }

        if (!lbAny)
            return;

        mORenderers.Add(pORenderer);
        mOOriginals.Add(lOMaterials);

        pORenderer.sharedMaterials = lOReplacements;
    }

    /// <summary>
    /// The palette version of a material, or null if there is none.
    ///
    /// The solid-colour model faces and the portcullis are checked FIRST: in the colour path
    /// they run on the same shader as the wall inscriptions (UW/Decal), so they differ only by
    /// the material itself (see UWLevelLoader.ModelSolidMaterial and PortcullisMaterial).
    /// </summary>
    private Material fGetPaletteMaterial(Material pOMaterial)
    {
        if (pOMaterial == null || pOMaterial.shader == null)
            return null;

        if (mOLevelLoader != null && pOMaterial == mOLevelLoader.ModelSolidMaterial)
            return fGetPaletteModel(ref mOModelMaterial, "UW Model Faces (Palette)",
                UnityEngine.Rendering.CullMode.Off, true);

        if (mOLevelLoader != null && pOMaterial == mOLevelLoader.PortcullisMaterial)
            return fGetPaletteModel(ref mOPortcullisMaterial, "UW Portcullis (Palette)",
                UnityEngine.Rendering.CullMode.Back, false);

        if (pOMaterial.shader.name == DungeonShaderName)
            return mOPaletteMaterial;

        if (pOMaterial.shader.name == BillboardShaderName)
            return fGetPaletteBillboard(pOMaterial, PaletteBillboardShaderName);

        if (pOMaterial.shader.name == DecalShaderName)
            return fGetPaletteBillboard(pOMaterial, PaletteDecalShaderName);

        return null;
    }

    /// <summary>The material of the solid-colour model faces or of the portcullis, cached in
    /// pOCache. It needs no texture - the index is stored on the vertex.
    ///
    /// pbModelDepth: the model faces are not hidden by the floor and the ceiling of their own
    /// tile (UWOwnTile). The palette path builds its OWN material here, so the keyword
    /// of the colour path's material (UWLevelLoader) does not reach it - the first version had
    /// it in the colour path only (per user, 2026-09-22: "in the palette renderer the shrine is
    /// not drawn over floor and ceiling"). Not for the portcullis.</summary>
    private Material fGetPaletteModel(ref Material pOCache, string psName,
        UnityEngine.Rendering.CullMode peCull, bool pbModelDepth)
    {
        if (pOCache != null)
            return pOCache;

        Shader lOShader = Shader.Find(PaletteModelShaderName);

        if (lOShader == null)
        {
            Debug.LogError("Render path: shader " + PaletteModelShaderName + " not found");
            return null;
        }

        pOCache = new Material(lOShader);
        pOCache.name = psName;
        pOCache.SetFloat("_Cull", (float)peCull);

        if (pbModelDepth)
            UWOwnTile.Enable(pOCache);

        return pOCache;
    }

    /// <summary>Fetches the colour texture to index texture mapping for objects and
    /// creatures. Must be done anew on every apply: creature atlases only come into being
    /// when the first creature of their kind appears.</summary>
    private void fCollectIndexTextures()
    {
        mOIndexTextures.Clear();

        if (mOLevelLoader != null && mOLevelLoader.ObjectAtlas != null
            && mOLevelLoader.ObjectAtlas.Atlas != null
            && mOLevelLoader.ObjectAtlas.IndexAtlas != null)
            mOIndexTextures[mOLevelLoader.ObjectAtlas.Atlas] = mOLevelLoader.ObjectAtlas.IndexAtlas;

        UWCritterAnimator.CollectIndexTextures(mOIndexTextures);
    }

    /// <summary>
    /// The palette version of a sprite or decal material. Alpha cutoff, depth bias and the
    /// blend state are carried over - only the texture is swapped for the index version
    /// and the shader changed.
    ///
    /// Returns null if there is no index version of this texture. Then the
    /// material stays as it is - better a sprite on the old path than a wrongly
    /// coloured one.
    /// </summary>
    private Material fGetPaletteBillboard(Material pOSource, string psShaderName)
    {
        Material lOResult;

        if (mOPaletteMaterials.TryGetValue(pOSource, out lOResult))
            return lOResult;

        Texture lOIndexTexture;

        if (pOSource.mainTexture == null
            || !mOIndexTextures.TryGetValue(pOSource.mainTexture, out lOIndexTexture))
            return null;

        Shader lOShader = Shader.Find(psShaderName);

        if (lOShader == null)
        {
            Debug.LogError("Render path: shader " + psShaderName + " not found");
            return null;
        }

        lOResult = new Material(lOShader);
        lOResult.name = pOSource.name + " (Palette)";

        lOResult.SetTexture("_IndexTex", lOIndexTexture);

        // Only carry over what both sides know. The decal shader has neither
        // depth bias nor an adjustable blend state.
        fCopyFloat(pOSource, lOResult, "_Cutoff");
        fCopyFloat(pOSource, lOResult, "_DepthBias");
        fCopyFloat(pOSource, lOResult, "_SrcBlend");
        fCopyFloat(pOSource, lOResult, "_DstBlend");
        fCopyFloat(pOSource, lOResult, "_ZWrite");
        fCopyFloat(pOSource, lOResult, "_Cull");
        fCopyFloat(pOSource, lOResult, UWOwnTile.BigRadiusProperty);
        // Creatures and items: their own tile does not cover them (UWOwnTile).
        UWOwnTile.CopyTo(pOSource, lOResult);

        lOResult.renderQueue = pOSource.renderQueue;

        mOPaletteMaterials[pOSource] = lOResult;

        return lOResult;
    }

    /// <summary>Copies a float value if both materials know it.</summary>
    private static void fCopyFloat(Material pOFrom, Material pOTo, string psName)
    {
        if (pOFrom.HasFloat(psName) && pOTo.HasFloat(psName))
            pOTo.SetFloat(psName, pOFrom.GetFloat(psName));
    }

    private void fRestore()
    {
        for (int liAt = 0; liAt < mORenderers.Count; liAt++)
        {
            if (mORenderers[liAt] != null)
                mORenderers[liAt].sharedMaterials = mOOriginals[liAt];
        }

        mORenderers.Clear();
        mOOriginals.Clear();

        miAppliedLevel = -1;
    }
}
