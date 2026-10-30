using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnderworldRevisited;
using UnderworldRevisited.Build;
using UWDataImport.UWData;

/// <summary>
/// THE ONE OWNER OF THE RENDER MODE (cleanup step 6.5, 2026-10-24): holds both render paths
/// (IUWRenderPath - UWPaletteRenderPath, UWRemasterRenderPath), switches between them and ticks
/// the one in force. Mode is the answer to "which path draws now" for everyone else, OnChanged
/// tells them when it changes. Until this step there were two sources of truth (the setting
/// UWSettings.RenderMode and the toggle's IsPaletteActive), and nine places asked them every
/// frame and built or tore down on their own.
///
/// The switch is the detail level (UWGraphicsDetail.SetLevel -> Switch); the host follows the
/// setting by itself as well, so a scene started in either mode, or a mode set while no level
/// existed, comes up in the right path. The palette path starts on the first frame the level's
/// atlas is there (UWPaletteRenderPath.TryStart).
///
/// ALWAYS ON, in both paths: the palette's tables and the hallucination (UWPaletteCommon, ticked
/// here every frame - the interface icons, the hallucination, the ghosts and Night Vision read
/// them in Remastered too) and the grime (UWPaletteEffects.ApplyShared).
///
/// THE ONE WRITER OF THE CAMERA'S FLAGS AND OF URP'S OCCLUSION FEATURE: the colour and depth
/// textures (until this step the palette toggle's), the post-processing switch (Remastered's
/// renderer and the world resolution wrote it before, three writers) and the occlusion feature
/// (Remastered's renderer and the palette effects, each with its own search and an ownership
/// flag). Each frame in LateUpdate the path in force says what it wants (IUWRenderPath.Wants*)
/// and the host writes what differs.
///
/// Lives on the level object, next to the level loader, and goes with its scene.
/// </summary>
public sealed class UWRenderPathHost : MonoBehaviour
{
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

    /// <summary>The host of the scene, null while no level object has started.</summary>
    public static UWRenderPathHost Current { get; private set; }

    /// <summary>The path in force changed: the new mode. Raised after the old path went and the
    /// new one was switched on.</summary>
    public static event Action<UWSettings.RenderModeEnum> OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void fReset()
    {
        Current = null;
        OnChanged = null;
    }

    /// <summary>
    /// Which path draws now. Without a host (no level object yet) the setting's: Palette for
    /// Palette, Remastered for anything else.
    /// </summary>
    public static UWSettings.RenderModeEnum Mode => Current != null ? Current.meMode : fWantedMode();

    /// <summary>Whether the palette path is on with its materials swapped (for the dump).</summary>
    public static bool IsPaletteApplied => Current != null && Current.mOPalette != null && Current.mOPalette.IsApplied;

    private UWSettings.RenderModeEnum meMode;

    private UWPaletteRenderPath mOPalette;

    private UWRemasterRenderPath mORemaster;

    /// <summary>The palette's tables were there this frame (EnsureTables in Update).</summary>
    public bool TablesReady { get; private set; }

    private ScreenSpaceAmbientOcclusion mOOcclusion;

    private bool mbSearchedOcclusion;

    /// <summary>The player camera's own post-processing switch, before anyone set it.</summary>
    private bool mbCameraPostProcessing;

    private bool mbCameraKnown;

    /// <summary>The world resolution or the gamma forced the post-processing last frame.</summary>
    private bool mbPostProcessingForced;

    /// <summary>The palette's tables and the hallucination feed, needed in both render paths
    /// (cleanup step 6.2, 2026-10-24 - until then fields and methods of the toggle). The XFER.DAT
    /// lookups are built with the colour table, as they follow the colour help too.</summary>
    private readonly UWPaletteCommon mOCommon = new UWPaletteCommon();

    private UWLevelLoader mOLevelLoader;

    private UWLighting mOLighting;

    /// <summary>The cameras' URP data, looked up once per camera (cleanup step 6.3, 2026-10-24).</summary>
    private readonly UWCameraDataSlot mOMainCameraData = new UWCameraDataSlot();

    private readonly UWCameraDataSlot mORemoteCameraData = new UWCameraDataSlot();

    public UWPaletteCommon Common => mOCommon;

    public UWLevelLoader LevelLoader
    {
        get
        {
            if (mOLevelLoader == null)
                mOLevelLoader = GetComponent<UWLevelLoader>();

            return mOLevelLoader;
        }
    }

    public UWLighting Lighting
    {
        get
        {
            if (mOLighting == null)
                mOLighting = GetComponent<UWLighting>();

            return mOLighting;
        }
    }

    private void Awake()
    {
        Current = this;
        mOPalette = new UWPaletteRenderPath(this);
        mORemaster = new UWRemasterRenderPath(this);
        meMode = fWantedMode();
    }

    private void OnDestroy()
    {
        mOPalette.Dispose();
        mORemaster.Dispose();
        UWViewGamma.Dispose();

        // As UWRemasterRenderer did when it went with its scene.
        fSetOcclusion(false);

        if (Current == this)
            Current = null;
    }

    /// <summary>The mode the setting asks for. Anything that is not the palette path is the URP
    /// path: a settings asset saved before 2026-09-16 may still hold the value of the removed
    /// "Modern" mode, and it should land there rather than nowhere.</summary>
    private static UWSettings.RenderModeEnum fWantedMode()
    {
        UWSettings lOSettings = UWSettings.Instance;

        return lOSettings == null || lOSettings.RenderMode == UWSettings.RenderModeEnum.Palette
            ? UWSettings.RenderModeEnum.Palette : UWSettings.RenderModeEnum.Remastered;
    }

    /// <summary>
    /// Switches to a path right away (UWGraphicsDetail.SetLevel, after it wrote the setting).
    /// Without a level object nothing happens: the next scene's host starts in the setting's mode.
    /// </summary>
    public static void Switch(UWSettings.RenderModeEnum peMode)
    {
        UWRenderPathHost lOHost = Current;

        if (lOHost == null)
        {
            GameObject lOLevel = UWScene.LevelObject;

            if (lOLevel == null)
                return;

            lOHost = lOLevel.GetComponent<UWRenderPathHost>();

            if (lOHost == null)
                lOHost = lOLevel.AddComponent<UWRenderPathHost>();
        }

        lOHost.fSwitch(peMode);
    }

    private IUWRenderPath fPath(UWSettings.RenderModeEnum peMode)
    {
        return peMode == UWSettings.RenderModeEnum.Palette ? (IUWRenderPath)mOPalette : mORemaster;
    }

    /// <summary>The old path goes (its Deactivate leaves the field clean), the new one comes, and
    /// then the others hear of it. The mode is the new one already while the old path goes, so
    /// what it sets up again on its way out (the player light) is set up for the new one.</summary>
    private void fSwitch(UWSettings.RenderModeEnum peMode)
    {
        if (peMode == meMode)
        {
            // The same mode again: the palette path starts if it has not yet (Low chosen once more
            // retries a start that failed, as the toggle did).
            if (peMode == UWSettings.RenderModeEnum.Palette && !mOPalette.IsApplied)
                mOPalette.Activate();

            return;
        }

        IUWRenderPath lOOld = fPath(meMode);

        meMode = peMode;

        lOOld.Deactivate();
        fPath(peMode).Activate();

        OnChanged?.Invoke(peMode);
    }

    private void Update()
    {
        UWSettings.RenderModeEnum leWanted = fWantedMode();

        if (leWanted != meMode)
            fSwitch(leWanted);
        else if (meMode == UWSettings.RenderModeEnum.Palette)
            mOPalette.TryStart();

        // The lookup textures are set IN THE OLD RENDER PATH AS WELL: the
        // UI icons always go through the palette (see UWIconPalette), even
        // when the world is currently lit with URP. Without this their rotation froze
        // as soon as the detail level left the palette path.
        TablesReady = EnsureTables();

        if (TablesReady)
        {
            if (meMode == UWSettings.RenderModeEnum.Palette)
                mOPalette.Tick();

            fTickCommon();
        }

        if (meMode == UWSettings.RenderModeEnum.Remastered)
            mORemaster.Tick();
    }

    private void LateUpdate()
    {
        UWPaletteEffects.ApplyShared();

        IUWRenderPath lOPath = fPath(meMode);

        lOPath.LateTick();

        UWViewGamma.Tick(transform);
        fApplyCameraFlags(lOPath);
        fSetOcclusion(lOPath.WantsOcclusion);
    }

    /// <summary>
    /// The palette's tables in both paths, and the palette path's index array and dungeon
    /// material. True when all are there.
    /// </summary>
    public bool EnsureTables()
    {
        UWLevelLoader lOLoader = LevelLoader;

        if (lOLoader == null || lOLoader.UWDataImporter == null)
            return false;

        // The colour help is baked into the tables, so a change means building them again
        // (see UWColourVision, UWPaletteCommon.EnsureTables).
        bool lbTables = mOCommon.EnsureTables(lOLoader.UWDataImporter, mbDiagonalStretch);
        bool lbIndexArray = mOPalette.EnsureIndexArray(lOLoader.UWDataImporter);

        if (!lbTables || !lbIndexArray)
            return false;

        return mOPalette.EnsureMaterial();
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
            UWLighting lOLighting = Lighting;

            return lOLighting != null && lOLighting.IsBright;
        }
    }

    /// <summary>The hallucination effect on screen now (UWHallucinationState), for the help
    /// window's hidden values - NoEffect while none runs.</summary>
    public int HallucinationEffect => mOCommon.HallucinationEffect;

    /// <summary>The palette of the palette effect.</summary>
    public int HallucinationPalette => mOCommon.HallucinationPalette;

    /// <summary>The palette's globals of this frame, in both paths.</summary>
    private void fTickCommon()
    {
        UWLevelLoader lOLoader = LevelLoader;
        UWLighting lOLighting = Lighting;
        bool lbPaletteOn = mOPalette.IsApplied;
        int liLightLevel = lOLighting != null ? lOLighting.LightLevel : 0;

        float lfSteps = UWSettings.Instance != null
            ? UWSettings.Instance.PaletteRotationStepsPerSecond
            : 4f;

        UWDataImport.DataImport lOData = lOLoader != null ? lOLoader.UWDataImporter : null;
        UWCharacter lOCharacter = UWScene.Character;

        // The hallucination's globals are set only when its effect changes - once at the start as
        // well, or a scene loaded after one with an effect would keep showing it.
        mOCommon.FollowHallucination(lOCharacter != null ? lOCharacter.Vitals.Hallucination : 0, lOData);

        UWShadePalette.ApplyGlobals(mOCommon.ShownColours,
            mOCommon.ShadeTable, mOCommon.RotationTable, liLightLevel,
            UWFlicker.RotationStep(Time.time, lfSteps),
            mbInterpolateLevels, mbDither, mbOrderedMatrix,
            mfDitherScale > 0f ? mfDitherScale : UWShadePalette.GetDitherCell(),
            (FullBright || fIsUnlitLevel()) && lbPaletteOn);

        // The polygon corners' shade (UWCornerLevel, UWPaletteLookup.hlsl) of this light level.
        if (lOData != null && lOData.Shades != null && lOData.Shades.TryGet(liLightLevel, out UWShades.Entry lOShade))
        {
            UWShaderGlobals.SetVector(UWShaderIds.ShadeFormula, new Vector4(lOShade.Shading, lOShade.DistanceBeforeShading,
                lOShade.StartingLightLevel, lOShade.ViewingDistance));
            UWShaderGlobals.SetVector(UWShaderIds.ViewScale, new Vector4(UWClassicLayout.Frame.ViewXScale / 32768f,
                UWClassicLayout.Frame.ViewZoom / 32768f, 0f, 0f));
        }

        // NIGHT VISION: at light level 5 the original loads MONO.DAT as its light table
        // (OpenAndApplyShadesDat_ovr142_0, per user 2026-10-01: the view turns grey in the
        // original, not in ours). For the world only - the interface keeps its colours.
        Texture2D lOWorldTable = mOCommon.ShownColours;

        if (liLightLevel == UWShadePalette.MonochromeLightLevel && !(FullBright && lbPaletteOn))
            lOWorldTable = mOCommon.GetMonochromeTable(lOData) ?? lOWorldTable;

        UWShaderGlobals.SetTexture(UWShaderIds.WorldColourTable, lOWorldTable);

        // Only while the hallucination does not swap the palette: the lookups are built from
        // palette 0. (Remastered's own XFER mixing is UWRemasterRenderPath's.)
        UWShadePalette.ApplyXferGlobals(mOCommon.Shown.InverseLookup, mOCommon.Shown.XferColours,
            mOPalette.WantsSceneColour);

        // The hallucination's palette reaches the whole interface, as the VGA palette does - in
        // either render path, the interface goes through the palette in both.
        UWUiPaletteSwap.Follow(mOCommon.HallucinationTable != null && mOCommon.Shown.InverseLookup != null);
    }

    /// <summary>
    /// THE VOID IS DRAWN UNLIT (process_grid, UnderworldExhumed GRIDDB.C, read 2026-10-11; the
    /// Exhumed audit's row 25): on level 9 the original sets lighton to 0 for the frame, so every
    /// wall, floor, ceiling, model face and sprite takes row 0 of the light table - the full
    /// colours, no distance shading, no shade dither - and the special shading mode is off. The
    /// light table itself stays the one in force (night vision's grey still applies). Until
    /// 2026-10-11 ours shaded the Void like any level.
    /// </summary>
    private bool fIsUnlitLevel()
    {
        UWLevelLoader lOLoader = LevelLoader;

        if (lOLoader == null)
            return false;

        // THE SECOND GAME (UW2 plan phase 2 step 3): process_grid turns lighting off on levels 65 to
        // 72 other than 68, (PlayerLevel - 1) / 8 == 8 (Underworld Exhumed uw2 3d/GRIDDB.C:202-210). Its
        // session has no rule set yet, so the first game's Void from the rules cannot be asked.
        if (UWSettings.Game.Game == UWDataImport.UWData.UWGame.Uw2)
            return UWDataImport.UWData.UWDarknessUw2.IsUnlitLevel(lOLoader.CurrentLevelIndex + 1);

        return lOLoader.CurrentLevelIndex == UWRules.Current.Levels.Void;
    }

    /// <summary>
    /// The camera's flags as the path in force wants them, on the player camera and on the remote
    /// view (Roaming Sight, the camera trap), which draws in its place.
    ///
    /// The translucent pixels read what lies behind them (URP's opaque texture, a copy of the
    /// picture after everything solid is drawn - UWBillboardPalette). The copy costs a pass, so
    /// the camera asks for it only while the palette path mixes that way. The remote view is a
    /// second camera, and its translucent pixels need the copy of ITS picture.
    ///
    /// The DEPTH texture the whole time the palette path is on: the renderer draws it in a
    /// prepass (UWUniversalRenderer, copy depth mode "force prepass"), and the sprites read the
    /// geometry behind them from it (UWPainterOrder.hlsl, UWPainterSpriteVisible).
    ///
    /// THE POST-PROCESSING while Remastered's Volume is on, and while the world is drawn at the
    /// original's resolution (UWWorldResolution.ForcesPostProcessing: URP applies the
    /// nearest-neighbour filter only in its post-processing passes); otherwise the camera keeps
    /// its own value, remembered before anyone set it (the world resolution's forcing is not the
    /// camera's own). The remote view gets it from the world resolution only, and when that goes
    /// off, what the render mode wants - as UWWorldResolution did until this step. The 3D view's
    /// gamma (UWViewGamma.ForcesPostProcessing, issue 9, 2026-10-30) forces it the same way, in
    /// both paths.
    /// </summary>
    private void fApplyCameraFlags(IUWRenderPath pOPath)
    {
        bool lbColour = pOPath.WantsSceneColour;
        bool lbDepth = pOPath.WantsSceneDepth;
        bool lbForced = UWWorldResolution.ForcesPostProcessing || UWViewGamma.ForcesPostProcessing;

        UniversalAdditionalCameraData lOMain = mOMainCameraData.Get(Camera.main);

        if (lOMain != null)
        {
            fRequireSceneColour(lOMain, lbColour, lbDepth);

            if (!mbCameraKnown)
            {
                mbCameraPostProcessing = lOMain.renderPostProcessing && !lbForced;
                mbCameraKnown = true;
            }

            fSetPostProcessing(lOMain, mbCameraPostProcessing || pOPath.WantsPostProcessing || lbForced);
        }

        if (UWRemoteCamera.Current != null)
        {
            UniversalAdditionalCameraData lORemote = mORemoteCameraData.Get(UWRemoteCamera.Current.ViewCamera);

            fRequireSceneColour(lORemote, lbColour, lbDepth);

            if (lbForced)
                fSetPostProcessing(lORemote, true);
            else if (mbPostProcessingForced)
                fSetPostProcessing(lORemote, meMode == UWSettings.RenderModeEnum.Remastered);
        }

        mbPostProcessingForced = lbForced;
    }

    private static void fSetPostProcessing(UniversalAdditionalCameraData pOData, bool pbOn)
    {
        if (pOData != null && pOData.renderPostProcessing != pbOn)
            pOData.renderPostProcessing = pbOn;
    }

    /// <summary>
    /// URP's occlusion feature as the path in force wants it: the palette ambient occlusion reads
    /// it, Remastered draws with it. The feature is a sub-asset of the renderer asset. From outside
    /// it can only be reached through the loaded objects - there is no public list of renderer
    /// features at runtime.
    /// </summary>
    private void fSetOcclusion(bool pbOn)
    {
        if (!mbSearchedOcclusion)
        {
            mbSearchedOcclusion = true;

            ScreenSpaceAmbientOcclusion[] lOFound = Resources.FindObjectsOfTypeAll<ScreenSpaceAmbientOcclusion>();

            if (lOFound != null && lOFound.Length > 0)
                mOOcclusion = lOFound[0];
            else
                Debug.LogWarning("Remastered: no occlusion feature found - "
                    + "run \"Underworld Revisited/Remastered/Set up URP\" once.");
        }

        if (mOOcclusion != null && mOOcclusion.isActive != pbOn)
            mOOcclusion.SetActive(pbOn);
    }

    private static void fRequireSceneColour(UniversalAdditionalCameraData pOData, bool pbRequired, bool pbDepth)
    {
        if (pOData == null)
            return;

        CameraOverrideOption leWanted = pbRequired ? CameraOverrideOption.On : CameraOverrideOption.UsePipelineSettings;

        if (pOData.requiresColorOption != leWanted)
            pOData.requiresColorOption = leWanted;

        CameraOverrideOption leDepth = pbDepth ? CameraOverrideOption.On : CameraOverrideOption.UsePipelineSettings;

        if (pOData.requiresDepthOption != leDepth)
            pOData.requiresDepthOption = leDepth;
    }

    /// <summary>After every build of the level (UWLevelLoader): a jump within the same level
    /// rebuilds everything as well.</summary>
    public void OnLevelBuilt()
    {
        mOPalette.OnLevelBuilt();
        mORemaster.OnLevelBuilt();
    }

    /// <summary>A node built after the level (UWLevelLoader, through UWWorldView): onto the path
    /// in force.</summary>
    public void OnObjectSpawned(GameObject pOSpawned)
    {
        fPath(meMode).OnObjectSpawned(pOSpawned);
    }
}
