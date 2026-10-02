using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sets up the URP settings that the "Remastered" render mode needs (stage 2 of the
/// plan: bloom, occlusion, tonemapping). Once run, it is stored in the project.
///
/// THREE THINGS WERE MISSING, all to be set only once and all without effect on the other
/// two render modes:
///
///   1. The renderer had NO postProcessData - without it URP runs no
///      post-processing at all, not even bloom. The default version from the URP package
///      is hooked in.
///   2. It had no occlusion feature (SSAO). It is created, but SWITCHED OFF -
///      UWRemasterRenderer only switches it on in Remastered mode.
///   3. Colour grading was set to LDR. Tonemapping is meant for HDR; since
///      post-processing is completely off in the other modes, the change makes no
///      difference there.
///
/// Added later (see the comments in the code): the occlusion radius is scaled to our
/// world (fTuneAmbientOcclusion), the renderer is switched to Forward+ for the torch
/// lights, and the pipeline gets point light shadows, soft shadows and a larger shadow
/// distance.
///
/// The camera switches its post-processing on and off by itself at runtime, see
/// UWRemasterRenderer - in Modern and Palette it stays off so the image there
/// remains unchanged.
/// </summary>
public static class UWRemasterSetup
{
    private const string RendererPath = "Assets/Settings/UWUniversalRenderer.asset";

    private const string PipelinePath = "Assets/Settings/UWRenderPipeline.asset";

    private const string PostProcessDataPath =
        "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

    [MenuItem("Underworld Revisited/Remastered/Set up URP")]
    public static void Run()
    {
        bool lbChanged = fSetUpRenderer();

        lbChanged |= fSetUpPipeline();

        if (lbChanged)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log(lbChanged ? "Remastered: URP set up." : "Remastered: URP was already set up.");
    }

    private static bool fSetUpRenderer()
    {
        UniversalRendererData lOData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);

        if (lOData == null)
        {
            Debug.LogError("Remastered: " + RendererPath + " not found.");
            return false;
        }

        SerializedObject lOSerialized = new SerializedObject(lOData);
        bool lbChanged = false;

        SerializedProperty lOPostProcess = lOSerialized.FindProperty("postProcessData");

        if (lOPostProcess != null && lOPostProcess.objectReferenceValue == null)
        {
            Object lOAsset = AssetDatabase.LoadAssetAtPath<Object>(PostProcessDataPath);

            if (lOAsset == null)
                Debug.LogError("Remastered: " + PostProcessDataPath + " not found - without it there is no post-processing.");
            else
            {
                lOPostProcess.objectReferenceValue = lOAsset;
                lbChanged = true;
            }
        }

        if (!fHasAmbientOcclusion(lOData))
        {
            fAddAmbientOcclusion(lOData, lOSerialized);
            lbChanged = true;
        }

        lbChanged |= fTuneAmbientOcclusion(lOData);

        // FORWARD+ for the torch light (UWRemasterLights): in plain Forward each object
        // gets at most four additional lights, and in our port an object is a whole
        // chunk of 16 by 16 tiles. Forward+ looks up the lights per screen cluster. The
        // three lit shaders got the cluster loop for this; the palette path
        // uses no lights at all and is unaffected.
        SerializedProperty lOMode = lOSerialized.FindProperty("m_RenderingMode");

        if (lOMode != null && lOMode.intValue != 2)
        {
            lOMode.intValue = 2;
            lbChanged = true;
        }

        if (lbChanged)
        {
            lOSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(lOData);
        }

        return lbChanged;
    }

    private static bool fHasAmbientOcclusion(UniversalRendererData pOData)
    {
        if (pOData.rendererFeatures == null)
            return false;

        for (int liAt = 0; liAt < pOData.rendererFeatures.Count; liAt++)
        {
            if (pOData.rendererFeatures[liAt] is ScreenSpaceAmbientOcclusion)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Creates the occlusion feature - exactly the way the inspector does it (see
    /// ScriptableRendererDataEditor.AddComponent): as a sub-object of the renderer asset,
    /// entered in both lists, the second one holding the local file identifier.
    /// </summary>
    private static void fAddAmbientOcclusion(UniversalRendererData pOData, SerializedObject pOSerialized)
    {
        ScreenSpaceAmbientOcclusion lOFeature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();

        lOFeature.name = "ScreenSpaceAmbientOcclusion";
        lOFeature.hideFlags |= HideFlags.HideInHierarchy;

        // Off until Remastered mode switches it on.
        lOFeature.SetActive(false);

        AssetDatabase.AddObjectToAsset(lOFeature, pOData);

        string lsGuid;
        long llLocalId;

        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(lOFeature, out lsGuid, out llLocalId);

        SerializedProperty lOFeatures = pOSerialized.FindProperty("m_RendererFeatures");
        SerializedProperty lOMap = pOSerialized.FindProperty("m_RendererFeatureMap");

        lOFeatures.arraySize++;
        lOFeatures.GetArrayElementAtIndex(lOFeatures.arraySize - 1).objectReferenceValue = lOFeature;

        lOMap.arraySize++;
        lOMap.GetArrayElementAtIndex(lOMap.arraySize - 1).longValue = llLocalId;
    }

    /// <summary>
    /// Brings the occlusion to OUR SCALE. URP ships a radius of 0.035 -
    /// that suits a world in metres. In our port a tile is 64 units wide and
    /// a figure about 30 tall; with the default value the shadow in the corner would be
    /// thinner than a texture pixel and simply invisible.
    ///
    /// It is only touched while the radius is still the tiny default value - whoever set it
    /// in the inspector should keep it.
    /// </summary>
    private static bool fTuneAmbientOcclusion(UniversalRendererData pOData)
    {
        for (int liAt = 0; liAt < pOData.rendererFeatures.Count; liAt++)
        {
            ScreenSpaceAmbientOcclusion lOFeature = pOData.rendererFeatures[liAt] as ScreenSpaceAmbientOcclusion;

            if (lOFeature == null)
                continue;

            SerializedObject lOSerialized = new SerializedObject(lOFeature);
            SerializedProperty lORadius = lOSerialized.FindProperty("m_Settings.Radius");

            if (lORadius == null || lORadius.floatValue >= 1f)
                return false;

            lORadius.floatValue = 4f;

            SerializedProperty lOIntensity = lOSerialized.FindProperty("m_Settings.Intensity");

            if (lOIntensity != null)
                lOIntensity.floatValue = 1.5f;

            lOSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(lOFeature);

            return true;
        }

        return false;
    }

    /// <summary>Colour grading to HDR - tonemapping is made for it. Also switches on
    /// point light and soft shadows and raises the shadow distance to ShadowDistance.</summary>
    private static bool fSetUpPipeline()
    {
        UniversalRenderPipelineAsset lOPipeline =
            AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);

        if (lOPipeline == null)
        {
            Debug.LogError("Remastered: " + PipelinePath + " not found.");
            return false;
        }

        SerializedObject lOSerialized = new SerializedObject(lOPipeline);
        bool lbChanged = false;

        lbChanged |= fSetInt(lOSerialized, "m_ColorGradingMode", 1);

        // Shadows for point lights: the player light and the torches of Remastered mode.
        // In the other modes no light casts shadows, so it costs nothing there.
        lbChanged |= fSetInt(lOSerialized, "m_AdditionalLightShadowsSupported", 1);
        lbChanged |= fSetInt(lOSerialized, "m_SoftShadowsSupported", 1);

        // The shadow distance was set to 50 - URP works in metres, for us that is less than
        // one tile. Shadows beyond it simply disappeared.
        SerializedProperty lODistance = lOSerialized.FindProperty("m_ShadowDistance");

        if (lODistance != null && lODistance.floatValue < ShadowDistance)
        {
            lODistance.floatValue = ShadowDistance;
            lbChanged = true;
        }

        if (!lbChanged)
            return false;

        lOSerialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(lOPipeline);

        return true;
    }

    /// <summary>Sixteen tiles - no torch reaches further.</summary>
    private const float ShadowDistance = 1024f;

    private static bool fSetInt(SerializedObject pOSerialized, string psName, int piValue)
    {
        SerializedProperty lOProperty = pOSerialized.FindProperty(psName);

        if (lOProperty == null || lOProperty.intValue == piValue)
            return false;

        lOProperty.intValue = piValue;

        return true;
    }
}
