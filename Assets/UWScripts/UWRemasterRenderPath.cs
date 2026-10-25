using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnderworldRevisited;
using UnderworldRevisited.Build;

/// <summary>
/// The "Remastered" render mode: hard pixels as in the original, but modern lighting.
///
/// A RENDER PATH SINCE CLEANUP STEP 6.5 (IUWRenderPath, 2026-10-24): until then UWRemasterRenderer,
/// a component that asked the render mode every frame, beside UWRemasterLights and
/// UWRemasterGroundShadows doing the same. UWRenderPathHost switches and ticks it now; the lights
/// and the ground shadows are its children (IUWRemasterFeature), ticked while it is in force and
/// cleared when it goes. The camera's post-processing switch and the occlusion feature are the
/// host's: this path says it wants them (WantsPostProcessing, WantsOcclusion).
///
/// STAGES 1 TO 4 OF THE PLAN ARE BUILT (see Todo.md):
///
///   1. Height from brightness, normals from that, and with them normal mapping in the dungeon shader
///      including specular highlights from torch light.
///   2. Bloom, occlusion and tonemapping.
///   3. Gloss and glow via the palette ranges: whatever is painted in lava colours (16-23)
///      glows and barely reflects; whatever is painted in water colours (48-63) reflects.
///      Metal is left out - there is no established range for it in the palette.
///   4. Parallax with stepped height: the view ray marches over the height map, joints
///      and stones shift against each other. The height is rounded to a few steps,
///      which suits the hard pixels.
///
/// HOW IT IS HOOKED IN: not as a separate build path and not as a material swap as
/// in the palette path, but as a switch on the EXISTING URP material. Both
/// share shader, meshes, UVs and slices; the difference is a keyword
/// (_UW_REMASTER), a second texture array and a few sliders.
///
/// THE POST-PROCESSING hangs on a separate Volume that is created here at runtime and
/// is only active in this mode; likewise the post-processing switch on the
/// camera. With the effects off and in the palette path both stay off, the image is unchanged.
///
/// ONE-TIME SETUP: the URP assets need postProcessData and the
/// occlusion feature - see UWRemasterSetup ("Underworld Revisited/Remastered/Set up URP"),
/// already done. The STRENGTH of the occlusion is set in the renderer asset
/// (UWUniversalRenderer, feature "ScreenSpaceAmbientOcclusion"), not here: URP keeps its
/// settings internal, from outside only the whole feature can be switched on and off.
///
/// THE NORMAL ARRAY is only created when the mode is selected for the first time - it costs
/// as much memory as the colour set and would be built for nothing in the palette path. Since
/// 2026-10-24 (cleanup step 6.2) it is UWTextureCache's, the same array the palette effects'
/// hollows and depth read.
/// </summary>
public sealed class UWRemasterRenderPath : IUWRenderPath
{
    /// <summary>High enough to lie above all Volumes added later.</summary>
    private const float VolumePriority = 1000f;

    private readonly UWRenderPathHost mOHost;

    /// <summary>The normals and heights for the relief strength - UWTextureCache owns the array
    /// (cleanup step 6.2, 2026-10-24; until then this class built its own copy, the palette
    /// effects another). If the strength changes, the cache builds it again.</summary>
    private Texture2DArray mONormalArray;

    /// <summary>The palette indices per pixel - they tell what kind of material a
    /// spot is (lava, water, stone). UWTextureCache's, shared with the palette path.</summary>
    private Texture2DArray mOIndexArray;

    /// <summary>Gloss and glow for each palette index (UWTextureCache).</summary>
    private Texture2D mOMaterialLut;

    private Material mOAppliedMaterial;

    private bool mbApplied;

    /// <summary>The material and the settings revision (UWSettings.Revision) the sliders were last
    /// pushed for: until 2026-10-24 sixteen material floats were set every frame.</summary>
    private Material mOPushedMaterial;

    private int miPushedRevision;

    private Volume mOVolume;

    private Bloom mOBloom;

    private Tonemapping mOTonemapping;

    /// <summary>Remastered's ghosts mix through the raw XFER tables this frame.</summary>
    private bool mbXfer;

    /// <summary>The placed lights and the ground shadows (IUWRemasterFeature).</summary>
    private readonly List<IUWRemasterFeature> mOFeatures = new List<IUWRemasterFeature>();

    public UWRemasterRenderPath(UWRenderPathHost pOHost)
    {
        mOHost = pOHost;

        mOFeatures.Add(new UWRemasterLights(pOHost));
        mOFeatures.Add(new UWRemasterGroundShadows(pOHost));
    }

    public UWSettings.RenderModeEnum Mode => UWSettings.RenderModeEnum.Remastered;

    /// <summary>Whether the keyword and the arrays are on the dungeon material.</summary>
    public bool IsApplied => mbApplied;

    /// <summary>The ghosts' XFER mixing reads the picture behind them (UWHallucination.hlsl).</summary>
    public bool WantsSceneColour => mbXfer;

    public bool WantsSceneDepth => false;

    /// <summary>The Volume needs the camera's post-processing while this path is applied.</summary>
    public bool WantsPostProcessing => mbApplied;

    public bool WantsOcclusion
    {
        get
        {
            UWSettings lOSettings = UWSettings.Instance;

            return mbApplied && lOSettings != null && lOSettings.RemasterAmbientOcclusion;
        }
    }

    /// <summary>Nothing to do at once: the material is set up in the next Tick, as soon as the
    /// level loader's dungeon material is there.</summary>
    public void Activate()
    {
    }

    /// <summary>
    /// Off: the keyword goes, the Volume goes off, the lights and the ground shadows go, and the
    /// raw tables only this path's shaders read are taken away (cleanup step 6.5, 2026-10-24;
    /// until then they stayed set in the palette path).
    /// </summary>
    public void Deactivate()
    {
        if (mbApplied)
            fRemove();

        foreach (IUWRemasterFeature lOFeature in mOFeatures)
            lOFeature.Clear();

        mbXfer = false;
        UWShaderGlobals.SetFloat(UWShaderIds.RemasterXfer, 0f);
        UWShaderGlobals.ClearTexture(UWPaletteCommon.Raw.ColoursId);
        UWShaderGlobals.ClearTexture(UWPaletteCommon.Raw.LightEffectId);
        UWShaderGlobals.ClearTexture(UWPaletteCommon.Raw.InverseLookupId);
        UWShaderGlobals.ClearTexture(UWPaletteCommon.Raw.XferColoursId);
    }

    public void Tick()
    {
        UWLevelLoader lOLoader = mOHost.LevelLoader;
        UWSettings lOSettings = UWSettings.Instance;

        if (lOLoader == null || lOSettings == null)
            return;

        // Remastered's ghosts through the palette's XFER tables, in raw colours
        // (UWHallucination.hlsl) - on the whole URP path, every detail level above Low, whose
        // sprites read the same tables. Only while the hallucination does not swap the palette.
        mbXfer = mOHost.TablesReady && UWPaletteCommon.EnsureRaw(lOLoader.UWDataImporter)
            && mOHost.Common.HallucinationTable == null;

        UWShaderGlobals.SetFloat(UWShaderIds.RemasterXfer, mbXfer ? 1f : 0f);

        Material lOMaterial = lOLoader.DungeonMaterial;

        if (lOMaterial != null)
        {
            // A new material (different level build) is noticed here and gets set up again.
            if (!mbApplied || lOMaterial != mOAppliedMaterial
                || !UWTextureCache.IsCurrentNormalArray(mONormalArray, lOLoader.UWDataImporter, lOSettings.RemasterHeightStrength))
                fApply(lOMaterial, lOSettings);

            fPushMaterialValues(lOMaterial, lOSettings);
            fUpdatePostProcessing(lOSettings);
        }

        foreach (IUWRemasterFeature lOFeature in mOFeatures)
            lOFeature.Tick(lOSettings);
    }

    public void LateTick()
    {
    }

    /// <summary>A rebuilt level shows itself here as another material or another level index
    /// (the lights) - nothing to forget.</summary>
    public void OnLevelBuilt()
    {
    }

    /// <summary>Remastered draws the URP materials as they are; the ground shadows find a new
    /// object with their next scan.</summary>
    public void OnObjectSpawned(GameObject pOSpawned)
    {
    }

    /// <summary>The source material itself: this path switches the material, it does not swap it.</summary>
    public Material GetMaterial(Material pOSource)
    {
        return null;
    }

    private void fApply(Material pOMaterial, UWSettings pOSettings)
    {
        UWLevelLoader lOLoader = mOHost.LevelLoader;

        if (lOLoader.UWDataImporter == null)
            return;

        // Built when first asked for, the normals again when the strength changed (the cache
        // logs how long that took).
        mONormalArray = UWTextureCache.GetNormalArray(lOLoader.UWDataImporter, pOSettings.RemasterHeightStrength);
        mOIndexArray = UWTextureCache.GetIndexArray(lOLoader.UWDataImporter);
        mOMaterialLut = UWTextureCache.GetMaterialLookup();

        pOMaterial.SetTexture(UWShaderIds.NormalArray, mONormalArray);
        pOMaterial.SetTexture(UWShaderIds.IndexArray, mOIndexArray);
        pOMaterial.SetTexture(UWShaderIds.MaterialLut, mOMaterialLut);
        pOMaterial.EnableKeyword(UWShaderIds.RemasterKeyword);

        mOAppliedMaterial = pOMaterial;
        mbApplied = true;
    }

    private void fRemove()
    {
        if (mOAppliedMaterial != null)
            mOAppliedMaterial.DisableKeyword(UWShaderIds.RemasterKeyword);

        if (mOVolume != null)
            mOVolume.gameObject.SetActive(false);

        mOAppliedMaterial = null;
        mbApplied = false;
    }

    /// <summary>
    /// The sliders onto the dungeon material - when the material is another one or the settings
    /// changed (UWSettings.Revision: the detail level writes them, an Inspector edit marks them),
    /// not every frame as until 2026-10-24.
    /// </summary>
    private void fPushMaterialValues(Material pOMaterial, UWSettings pOSettings)
    {
        if (pOMaterial == mOPushedMaterial && pOSettings.Revision == miPushedRevision)
            return;

        mOPushedMaterial = pOMaterial;
        miPushedRevision = pOSettings.Revision;

        pOMaterial.SetFloat(UWShaderIds.NormalStrength, pOSettings.RemasterNormalStrength);
        pOMaterial.SetFloat(UWShaderIds.SpecularStrength, pOSettings.RemasterSpecularStrength);
        pOMaterial.SetFloat(UWShaderIds.SpecularPower, pOSettings.RemasterSpecularPower);
        pOMaterial.SetFloat(UWShaderIds.EmissiveStrength, pOSettings.RemasterEmissiveStrength);
        pOMaterial.SetFloat(UWShaderIds.Roughness, pOSettings.RemasterRoughness);
        pOMaterial.SetFloat(UWShaderIds.SpecularBase, pOSettings.RemasterSpecularBase);
        pOMaterial.SetFloat(UWShaderIds.WrapLighting, pOSettings.RemasterWrapLighting);
        pOMaterial.SetFloat(UWShaderIds.ParallaxDepth, pOSettings.RemasterParallaxDepth);
        pOMaterial.SetFloat(UWShaderIds.ParallaxLevels, pOSettings.RemasterParallaxLevels);
        pOMaterial.SetFloat(UWShaderIds.ParallaxSteps, pOSettings.RemasterParallaxSteps);
        pOMaterial.SetFloat(UWShaderIds.SelfShadowStrength, pOSettings.RemasterSelfShadow);
        pOMaterial.SetFloat(UWShaderIds.SelfShadowDepth, pOSettings.RemasterSelfShadowDepth);
        pOMaterial.SetFloat(UWShaderIds.SelfShadowReach, pOSettings.RemasterSelfShadowReach);
        pOMaterial.SetFloat(UWShaderIds.SelfShadowSoftness, pOSettings.RemasterSelfShadowSoftness);
        pOMaterial.SetFloat(UWShaderIds.CavityStrength, pOSettings.RemasterCavity);
        pOMaterial.SetFloat(UWShaderIds.ParallaxSign, pOSettings.RemasterParallaxInvert ? -1f : 1f);
    }

    // ------------------------------------------------- Post-processing

    /// <summary>
    /// Bloom, tonemapping and the colour help according to the settings. The Volume is created
    /// the first time and then stays; setting its values costs nothing compared to rebuilding.
    /// The occlusion is the host's (WantsOcclusion).
    /// </summary>
    private void fUpdatePostProcessing(UWSettings pOSettings)
    {
        fEnsureVolume();

        if (mOVolume == null)
            return;

        mOVolume.gameObject.SetActive(true);

        if (mOBloom != null)
        {
            mOBloom.active = pOSettings.RemasterBloomIntensity > 0f;
            mOBloom.intensity.value = pOSettings.RemasterBloomIntensity;
            mOBloom.threshold.value = pOSettings.RemasterBloomThreshold;
            mOBloom.scatter.value = pOSettings.RemasterBloomScatter;
        }

        if (mOTonemapping != null)
        {
            mOTonemapping.active = pOSettings.RemasterTonemapping
                != UWSettings.RemasterTonemappingEnum.Off;

            mOTonemapping.mode.value = pOSettings.RemasterTonemapping
                == UWSettings.RemasterTonemappingEnum.Aces
                    ? TonemappingMode.ACES : TonemappingMode.Neutral;
        }

        fUpdateColourVision();
    }

    private ChannelMixer mOChannelMixer;

    private ColorAdjustments mOColourAdjustments;

    /// <summary>The colour matrix, brightness and contrast of the colour help, in the units of
    /// the pipeline: the mixer counts in percent, the exposure in stops.</summary>
    private void fUpdateColourVision()
    {
        float[] lyMatrix = UWColourVision.Filter.Matrix;

        if (mOChannelMixer != null)
        {
            mOChannelMixer.active = UWColourVision.Filter.HasMatrix;

            mOChannelMixer.redOutRedIn.value = lyMatrix[0] * 100f;
            mOChannelMixer.redOutGreenIn.value = lyMatrix[1] * 100f;
            mOChannelMixer.redOutBlueIn.value = lyMatrix[2] * 100f;
            mOChannelMixer.greenOutRedIn.value = lyMatrix[3] * 100f;
            mOChannelMixer.greenOutGreenIn.value = lyMatrix[4] * 100f;
            mOChannelMixer.greenOutBlueIn.value = lyMatrix[5] * 100f;
            mOChannelMixer.blueOutRedIn.value = lyMatrix[6] * 100f;
            mOChannelMixer.blueOutGreenIn.value = lyMatrix[7] * 100f;
            mOChannelMixer.blueOutBlueIn.value = lyMatrix[8] * 100f;
        }

        if (mOColourAdjustments != null)
        {
            float lfBrightness = Mathf.Max(0.05f, UWColourVision.Brightness);
            bool lbNightVision = fIsNightVision();

            mOColourAdjustments.active = UWColourVision.IsActive || lbNightVision;
            mOColourAdjustments.postExposure.value = Mathf.Log(lfBrightness, 2f);
            mOColourAdjustments.contrast.value = Mathf.Clamp((UWColourVision.Contrast - 1f) * 100f, -100f, 100f);
            mOColourAdjustments.saturation.value = lbNightVision ? -100f : 0f;
        }
    }

    /// <summary>
    /// NIGHT VISION IN THIS PATH TOO (per user, 2026-10-24: grey in Palette, in colour here): at
    /// light level 5 the original loads MONO.DAT as its light table and the view turns grey
    /// (UWRenderPathHost, OpenAndApplyShadesDat_ovr142_0). This path draws the world in its
    /// raw colours, so the camera's own grading takes the colour out instead - the world only,
    /// the interface lies on its own canvases above the camera and keeps its colours, as in the
    /// palette path. The full-brightness key gives the highest level, so it ends the grey here as
    /// it does there.
    /// </summary>
    private static bool fIsNightVision()
    {
        UWLighting lOLighting = UWScene.Lighting;

        return lOLighting != null && lOLighting.LightLevel == UWShadePalette.MonochromeLightLevel;
    }

    private void fEnsureVolume()
    {
        if (mOVolume != null)
            return;

        GameObject lOHolder = new GameObject("UW Remastered Volume");

        lOHolder.transform.SetParent(mOHost.transform, false);

        mOVolume = lOHolder.AddComponent<Volume>();
        mOVolume.isGlobal = true;
        mOVolume.priority = VolumePriority;
        mOVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        mOVolume.profile.name = "UW Remastered";

        // With true all parameters of the component are marked as "overridden" - otherwise
        // the default value stays and setting it has no effect.
        mOBloom = mOVolume.profile.Add<Bloom>(true);
        mOTonemapping = mOVolume.profile.Add<Tonemapping>(true);

        // The colour help for colour vision deficiencies: in this path the world is not drawn
        // through the palette, so the pipeline's own channel mixer carries the same matrix
        // (see UWColourVision). The interface above it goes through the palette and is already
        // corrected there.
        mOChannelMixer = mOVolume.profile.Add<ChannelMixer>(true);
        mOColourAdjustments = mOVolume.profile.Add<ColorAdjustments>(true);
    }

    /// <summary>
    /// The scene goes: the lights with it, and the Volume's profile. NOT the three arrays: they
    /// are UWTextureCache's and kept across scenes (until 2026-10-24 UWRemasterRenderer still
    /// destroyed them here, so a save loaded in Remastered built them again).
    /// </summary>
    public void Dispose()
    {
        foreach (IUWRemasterFeature lOFeature in mOFeatures)
            lOFeature.Dispose();

        if (mOVolume != null && mOVolume.profile != null)
            Object.Destroy(mOVolume.profile);
    }
}
