using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnderworldRevisited.Build;

/// <summary>
/// The "Remastered" render mode: hard pixels as in the original, but modern lighting.
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
/// (_UW_REMASTER), a second texture array and a few sliders. The level loader attaches
/// this component itself at startup.
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
/// as much memory as the colour set and would be built for nothing in the palette path.
/// </summary>
public class UWRemasterRenderer : MonoBehaviour
{
    private const string RemasterKeyword = "_UW_REMASTER";

    private const string NormalArrayProperty = "_NormalArray";

    private const string IndexArrayProperty = "_IndexArray";

    private const string MaterialLutProperty = "_MaterialLut";

    /// <summary>High enough to lie above all Volumes added later.</summary>
    private const float VolumePriority = 1000f;

    private UWLevelLoader mOLevelLoader;

    private Texture2DArray mONormalArray;

    /// <summary>The palette indices per pixel - they tell what kind of material a
    /// spot is (lava, water, stone).</summary>
    private Texture2DArray mOIndexArray;

    /// <summary>Gloss and glow for each palette index.</summary>
    private Texture2D mOMaterialLut;

    /// <summary>The relief strength the array was built with - if it changes, the array
    /// has to be rebuilt.</summary>
    private float mfBuiltHeightStrength = -1f;

    private Material mOAppliedMaterial;

    private bool mbApplied;

    private Volume mOVolume;

    private Bloom mOBloom;

    private Tonemapping mOTonemapping;

    private ScreenSpaceAmbientOcclusion mOOcclusion;

    private bool mbSearchedOcclusion;

    private Camera mOCamera;

    /// <summary>How the camera had it before switching - restored when switching
    /// back.</summary>
    private bool mbCameraPostProcessing;

    private bool mbCameraKnown;

    private void Update()
    {
        if (mOLevelLoader == null)
            mOLevelLoader = GetComponent<UWLevelLoader>();

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        if (mOLevelLoader == null || lOSettings == null)
            return;

        Material lOMaterial = mOLevelLoader.DungeonMaterial;

        if (lOMaterial == null)
            return;

        bool lbWanted = lOSettings.IsRemasteredActive;

        if (!lbWanted)
        {
            if (mbApplied)
                fRemove(lOMaterial);

            return;
        }

        // A new material (different level build) is noticed here and gets set up again.
        if (!mbApplied || lOMaterial != mOAppliedMaterial
            || !Mathf.Approximately(mfBuiltHeightStrength, lOSettings.RemasterHeightStrength))
            fApply(lOMaterial, lOSettings);

        lOMaterial.SetFloat("_NormalStrength", lOSettings.RemasterNormalStrength);
        lOMaterial.SetFloat("_SpecularStrength", lOSettings.RemasterSpecularStrength);
        lOMaterial.SetFloat("_SpecularPower", lOSettings.RemasterSpecularPower);
        lOMaterial.SetFloat("_EmissiveStrength", lOSettings.RemasterEmissiveStrength);
        lOMaterial.SetFloat("_Roughness", lOSettings.RemasterRoughness);
        lOMaterial.SetFloat("_SpecularBase", lOSettings.RemasterSpecularBase);
        lOMaterial.SetFloat("_WrapLighting", lOSettings.RemasterWrapLighting);
        lOMaterial.SetFloat("_ParallaxDepth", lOSettings.RemasterParallaxDepth);
        lOMaterial.SetFloat("_ParallaxLevels", lOSettings.RemasterParallaxLevels);
        lOMaterial.SetFloat("_ParallaxSteps", lOSettings.RemasterParallaxSteps);
        lOMaterial.SetFloat("_SelfShadowStrength", lOSettings.RemasterSelfShadow);
        lOMaterial.SetFloat("_SelfShadowDepth", lOSettings.RemasterSelfShadowDepth);
        lOMaterial.SetFloat("_SelfShadowReach", lOSettings.RemasterSelfShadowReach);
        lOMaterial.SetFloat("_SelfShadowSoftness", lOSettings.RemasterSelfShadowSoftness);
        lOMaterial.SetFloat("_CavityStrength", lOSettings.RemasterCavity);
        lOMaterial.SetFloat("_ParallaxSign", lOSettings.RemasterParallaxInvert ? -1f : 1f);

        fUpdatePostProcessing(lOSettings);
    }

    private void fApply(Material pOMaterial, UnderworldRevisited.UWSettings pOSettings)
    {
        if (mONormalArray == null
            || !Mathf.Approximately(mfBuiltHeightStrength, pOSettings.RemasterHeightStrength))
        {
            if (mOLevelLoader.UWDataImporter == null)
                return;

            if (mONormalArray != null)
                Destroy(mONormalArray);

            float lfStart = Time.realtimeSinceStartup;

            mONormalArray = UWTextureArrayBuilder.BuildNormals(mOLevelLoader.UWDataImporter,
                pOSettings.RemasterHeightStrength);

            mfBuiltHeightStrength = pOSettings.RemasterHeightStrength;

            Debug.Log(string.Format("Remastered: built normals from {0} textures in {1:0} ms.",
                mONormalArray.depth, (Time.realtimeSinceStartup - lfStart) * 1000f));
        }

        if (mOIndexArray == null)
        {
            mOIndexArray = UWTextureArrayBuilder.BuildIndexed(mOLevelLoader.UWDataImporter);
            mOMaterialLut = UWTextureArrayBuilder.BuildMaterialLookup();
        }

        pOMaterial.SetTexture(NormalArrayProperty, mONormalArray);
        pOMaterial.SetTexture(IndexArrayProperty, mOIndexArray);
        pOMaterial.SetTexture(MaterialLutProperty, mOMaterialLut);
        pOMaterial.EnableKeyword(RemasterKeyword);

        mOAppliedMaterial = pOMaterial;
        mbApplied = true;

        fSetCameraPostProcessing(true);
    }

    private void fRemove(Material pOMaterial)
    {
        pOMaterial.DisableKeyword(RemasterKeyword);

        if (mOVolume != null)
            mOVolume.gameObject.SetActive(false);

        fSetOcclusion(false);
        fSetCameraPostProcessing(false);

        mOAppliedMaterial = null;
        mbApplied = false;
    }

    // ------------------------------------------------- Post-processing

    /// <summary>
    /// Bloom, tonemapping and occlusion according to the settings. The Volume is created the
    /// first time and then stays; setting its values costs nothing compared to
    /// rebuilding.
    /// </summary>
    private void fUpdatePostProcessing(UnderworldRevisited.UWSettings pOSettings)
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
                != UnderworldRevisited.UWSettings.RemasterTonemappingEnum.Off;

            mOTonemapping.mode.value = pOSettings.RemasterTonemapping
                == UnderworldRevisited.UWSettings.RemasterTonemappingEnum.Aces
                    ? TonemappingMode.ACES : TonemappingMode.Neutral;
        }

        fUpdateColourVision();

        fSetOcclusion(pOSettings.RemasterAmbientOcclusion);
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

            mOColourAdjustments.active = UWColourVision.IsActive;
            mOColourAdjustments.postExposure.value = Mathf.Log(lfBrightness, 2f);
            mOColourAdjustments.contrast.value = Mathf.Clamp((UWColourVision.Contrast - 1f) * 100f, -100f, 100f);
        }
    }

    private void fEnsureVolume()
    {
        if (mOVolume != null)
            return;

        GameObject lOHolder = new GameObject("UW Remastered Volume");

        lOHolder.transform.SetParent(transform, false);

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
    /// The occlusion feature is a sub-asset of the renderer asset. From outside
    /// it can only be reached through the loaded objects - there is no public list of
    /// renderer features at runtime.
    /// </summary>
    private void fSetOcclusion(bool pbOn)
    {
        if (!mbSearchedOcclusion)
        {
            mbSearchedOcclusion = true;

            ScreenSpaceAmbientOcclusion[] lOFound =
                Resources.FindObjectsOfTypeAll<ScreenSpaceAmbientOcclusion>();

            if (lOFound != null && lOFound.Length > 0)
                mOOcclusion = lOFound[0];
            else
                Debug.LogWarning("Remastered: no occlusion feature found - "
                    + "run \"Underworld Revisited/Remastered/Set up URP\" once.");
        }

        if (mOOcclusion != null && mOOcclusion.isActive != pbOn)
            mOOcclusion.SetActive(pbOn);
    }

    private void fSetCameraPostProcessing(bool pbOn)
    {
        if (mOCamera == null)
            mOCamera = Camera.main;

        if (mOCamera == null)
            return;

        UniversalAdditionalCameraData lOData = mOCamera.GetUniversalAdditionalCameraData();

        if (lOData == null)
            return;

        if (pbOn)
        {
            if (!mbCameraKnown)
            {
                // The world at the original's resolution forces it on as well (UWWorldResolution);
                // that is not the camera's own value.
                mbCameraPostProcessing = lOData.renderPostProcessing && !UWWorldResolution.Enabled;
                mbCameraKnown = true;
            }

            lOData.renderPostProcessing = true;

            return;
        }

        if (mbCameraKnown)
        {
            lOData.renderPostProcessing = mbCameraPostProcessing || UWWorldResolution.Enabled;
            mbCameraKnown = false;
        }
    }

    private void OnDestroy()
    {
        if (mONormalArray != null)
            Destroy(mONormalArray);

        if (mOIndexArray != null)
            Destroy(mOIndexArray);

        if (mOMaterialLut != null)
            Destroy(mOMaterialLut);

        if (mOVolume != null && mOVolume.profile != null)
            Destroy(mOVolume.profile);

        fSetOcclusion(false);
    }
}
