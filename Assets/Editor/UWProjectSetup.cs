using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-time project setup: create and assign the URP assets, serialise assets as
/// text. Runs from the menu or via -executeMethod in batch mode.
/// </summary>
public static class UWProjectSetup
{
    private const string SettingsFolder = "Assets/Settings";
    private const string RendererPath = SettingsFolder + "/UWUniversalRenderer.asset";
    private const string PipelinePath = SettingsFolder + "/UWRenderPipeline.asset";

    [MenuItem("Underworld Revisited/Set up project (URP + text serialisation)")]
    public static void SetupAll()
    {
        ForceTextSerialization();
        SetupUniversalRenderPipeline();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Scene and ProjectSettings are stored in binary and therefore can be neither diffed nor
    /// sensibly merged. Switch to text and rewrite all assets once.
    /// </summary>
    public static void ForceTextSerialization()
    {
        if (EditorSettings.serializationMode != SerializationMode.ForceText)
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            Debug.Log("UWSETUP: serialisation mode set to ForceText");
        }

        AssetDatabase.ForceReserializeAssets();
        Debug.Log("UWSETUP: assets reserialised");
    }

    public static void SetupUniversalRenderPipeline()
    {
        if (!AssetDatabase.IsValidFolder(SettingsFolder))
            AssetDatabase.CreateFolder("Assets", "Settings");

        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);

        if (pipeline == null)
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);

            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                Debug.Log("UWSETUP: renderer data created -> " + RendererPath);
            }

            pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            Debug.Log("UWSETUP: pipeline asset created -> " + PipelinePath);
        }

        AssetDatabase.SaveAssets();

        GraphicsSettings.defaultRenderPipeline = pipeline;

        int previousQualityLevel = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(previousQualityLevel, false);

        AssetDatabase.SaveAssets();
        Debug.Log("UWSETUP: URP assigned as render pipeline (" + QualitySettings.names.Length + " quality levels)");
    }
}
