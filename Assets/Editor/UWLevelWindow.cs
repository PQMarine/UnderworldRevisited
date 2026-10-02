using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UWDataImport;
using UnderworldRevisited;
using UnderworldRevisited.Build;

/// <summary>
/// Tool window for the level geometry: preview in the editor and baking into prefabs.
///
/// Previously the entire architecture was only created at runtime in Start(), so it was
/// invisible in the editor and not accessible for NavMesh, light or occlusion baking.
/// </summary>
public sealed class UWLevelWindow : EditorWindow
{
    private const string PreviewRootName = "UW Level Preview";
    private const string BakeFolder = "Assets/UWBaked";

    private static DataImport mOCachedData;
    private static string msCachedDataPath;

    private UWSettings mOSettings;
    private int miLevelIndex;
    private string msStatus = string.Empty;
    private bool mbAddColliders = true;

    [MenuItem("Underworld Revisited/Level Tools")]
    public static void ShowWindow()
    {
        GetWindow<UWLevelWindow>("UW Level");
    }

    private void OnEnable()
    {
        fFindSettings();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Data source", EditorStyles.boldLabel);

        mOSettings = (UWSettings)EditorGUILayout.ObjectField("Settings", mOSettings, typeof(UWSettings), false);

        if (mOSettings == null)
        {
            EditorGUILayout.HelpBox("No UWSettings found. One will be created under Assets/Resources.", MessageType.Info);

            if (GUILayout.Button("Create UWSettings"))
                fCreateSettings();

            return;
        }

        EditorGUI.BeginChangeCheck();
        string lsPath = EditorGUILayout.TextField("Data directory", mOSettings.DataPath);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(mOSettings, "Change data directory");
            mOSettings.DataPath = lsPath;
            EditorUtility.SetDirty(mOSettings);
        }

        if (!mOSettings.IsDataPathValid)
        {
            EditorGUILayout.HelpBox("There is no LEV.ARK in the given directory.", MessageType.Error);

            if (GUILayout.Button("Search automatically"))
            {
                string lsFound = UWSettings.AutoDetectDataPath();

                if (lsFound != null)
                {
                    Undo.RecordObject(mOSettings, "Data directory detected");
                    mOSettings.DataPath = lsFound;
                    EditorUtility.SetDirty(mOSettings);
                    msStatus = "Found: " + lsFound;
                }
                else
                    msStatus = "Nothing found in the usual locations.";
            }

            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);

        string[] lsLevelNames = new string[9];
        for (int i = 0; i < 9; i++)
            lsLevelNames[i] = "Level " + (i + 1);

        miLevelIndex = EditorGUILayout.Popup("Level", miLevelIndex, lsLevelNames);
        mbAddColliders = EditorGUILayout.Toggle("Create colliders", mbAddColliders);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("The preview is not saved in the scene.", MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create preview"))
                fBuildPreview();

            if (GUILayout.Button("Remove preview"))
                fClearPreview();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Bake this level"))
                fBakeLevel(miLevelIndex);

            if (GUILayout.Button("Bake all 9 levels"))
                fBakeAll();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Without an assignment the loader recomputes the geometry on every level change (about half a second).", MessageType.None);

        if (GUILayout.Button("Connect baked levels to the loader in the scene"))
            fAssignBakedLevels();

        if (!string.IsNullOrEmpty(msStatus))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(msStatus, MessageType.Info);
        }
    }

    private void fFindSettings()
    {
        if (mOSettings != null)
            return;

        string[] lsGuids = AssetDatabase.FindAssets("t:UWSettings");

        if (lsGuids.Length > 0)
            mOSettings = AssetDatabase.LoadAssetAtPath<UWSettings>(AssetDatabase.GUIDToAssetPath(lsGuids[0]));
    }

    private void fCreateSettings()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        UWSettings lOSettings = CreateInstance<UWSettings>();

        string lsDetected = UWSettings.AutoDetectDataPath();
        if (lsDetected != null)
            lOSettings.DataPath = lsDetected;

        AssetDatabase.CreateAsset(lOSettings, "Assets/Resources/" + UWSettings.ResourceName + ".asset");
        AssetDatabase.SaveAssets();

        mOSettings = lOSettings;
        msStatus = "UWSettings created.";
    }

    /// <summary>
    /// Reading the original files takes noticeably long because all textures are
    /// unpacked. Within one editor session a single pass is enough.
    /// </summary>
    private DataImport fGetData()
    {
        if (mOCachedData != null && msCachedDataPath == mOSettings.DataPath)
            return mOCachedData;

        try
        {
            EditorUtility.DisplayProgressBar("UW data", "Reading original files...", 0.5f);
            mOCachedData = new DataImport(mOSettings.DataPath);
            msCachedDataPath = mOSettings.DataPath;
        }
        catch (System.Exception lOError)
        {
            mOCachedData = null;
            msStatus = "Data could not be read: " + lOError.Message;
            Debug.LogException(lOError);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        return mOCachedData;
    }

    private void fClearPreview()
    {
        GameObject lOExisting = GameObject.Find(PreviewRootName);

        while (lOExisting != null)
        {
            DestroyImmediate(lOExisting);
            lOExisting = GameObject.Find(PreviewRootName);
        }

        msStatus = "Preview removed.";
    }

    private void fBuildPreview()
    {
        DataImport lOData = fGetData();

        if (lOData == null)
            return;

        fClearPreview();

        System.Diagnostics.Stopwatch lOWatch = System.Diagnostics.Stopwatch.StartNew();

        int liFloorOffset = UWTextureArrayBuilder.GetFloorSliceOffset(lOData);

        UWChunkGeometry[] lOChunks = UWLevelMeshBuilder.Build(lOData.Levels[miLevelIndex], liFloorOffset);
        Texture2DArray lOTextures = UWTextureArrayBuilder.Build(lOData, mOSettings.TextureFilterMode);
        Material lOMaterial = UWLevelAssembler.CreateMaterial(lOTextures, "UW Preview");

        if (lOMaterial == null)
            return;

        lOMaterial.SetFloat("_AmbientFloor", mOSettings.AmbientFloor);

        GameObject lORoot = UWLevelAssembler.CreateLevelRoot(PreviewRootName, lOChunks, lOMaterial, miLevelIndex, mbAddColliders);

        // So that the preview does not end up in the scene and disappears
        // without a trace on the next load.
        fMarkAsPreview(lORoot);
        fMarkAsPreview(lOTextures);
        fMarkAsPreview(lOMaterial);

        lOWatch.Stop();

        msStatus = string.Format("Level {0}: {1} chunks, {2} triangles, {3} ms.",
            miLevelIndex + 1, lORoot.transform.childCount, UWLevelAssembler.CountTriangles(lOChunks), lOWatch.ElapsedMilliseconds);

        Selection.activeGameObject = lORoot;
        SceneView.FrameLastActiveSceneView();
    }

    private static void fMarkAsPreview(Object pOObject)
    {
        pOObject.hideFlags = HideFlags.DontSave;

        GameObject lOGameObject = pOObject as GameObject;

        if (lOGameObject == null)
            return;

        foreach (Transform lOChild in lOGameObject.transform)
            fMarkAsPreview(lOChild.gameObject);

        MeshFilter lOFilter = lOGameObject.GetComponent<MeshFilter>();

        if (lOFilter != null && lOFilter.sharedMesh != null)
            lOFilter.sharedMesh.hideFlags = HideFlags.DontSave;
    }

    /// <summary>
    /// Texture array and material are shared by all nine levels. The texture pool
    /// is global, one set per level would be nine nearly identical copies - on the first
    /// attempt that was 2.7 MB per level instead of once for all.
    /// </summary>
    private Material fGetOrCreateSharedMaterial(DataImport pOData)
    {
        string lsMaterialPath = BakeFolder + "/Material.mat";
        Material lOExisting = AssetDatabase.LoadAssetAtPath<Material>(lsMaterialPath);

        if (lOExisting != null)
            return lOExisting;

        Texture2DArray lOTextures = UWTextureArrayBuilder.Build(pOData, mOSettings.TextureFilterMode);
        AssetDatabase.CreateAsset(lOTextures, BakeFolder + "/Textures.asset");

        Material lOMaterial = UWLevelAssembler.CreateMaterial(lOTextures, "UW Dungeon");

        if (lOMaterial == null)
            return null;

        lOMaterial.SetFloat("_AmbientFloor", mOSettings.AmbientFloor);
        AssetDatabase.CreateAsset(lOMaterial, lsMaterialPath);

        return lOMaterial;
    }

    private void fBakeAll()
    {
        for (int i = 0; i < 9; i++)
        {
            if (!fBakeLevel(i, false))
                break;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private bool fBakeLevel(int piLevelIndex, bool pbRefresh = true)
    {
        DataImport lOData = fGetData();

        if (lOData == null)
            return false;

        fClearPreview();

        if (!AssetDatabase.IsValidFolder(BakeFolder))
            AssetDatabase.CreateFolder("Assets", "UWBaked");

        Material lOMaterial = fGetOrCreateSharedMaterial(lOData);

        if (lOMaterial == null)
            return false;

        string lsLevelFolder = string.Format("{0}/Level{1}", BakeFolder, piLevelIndex + 1);

        if (AssetDatabase.IsValidFolder(lsLevelFolder))
            AssetDatabase.DeleteAsset(lsLevelFolder);

        AssetDatabase.CreateFolder(BakeFolder, "Level" + (piLevelIndex + 1));

        System.Diagnostics.Stopwatch lOWatch = System.Diagnostics.Stopwatch.StartNew();

        UWChunkGeometry[] lOChunks = UWLevelMeshBuilder.Build(lOData.Levels[piLevelIndex], UWTextureArrayBuilder.GetFloorSliceOffset(lOData));

        GameObject lORoot = UWLevelAssembler.CreateLevelRoot("Level" + (piLevelIndex + 1), lOChunks, lOMaterial, piLevelIndex, mbAddColliders);

        // The meshes must exist as assets before the prefab is created - otherwise
        // the prefab points to objects that exist only in memory.
        MeshFilter[] lOFilters = lORoot.GetComponentsInChildren<MeshFilter>();

        for (int i = 0; i < lOFilters.Length; i++)
        {
            Mesh lOMesh = lOFilters[i].sharedMesh;

            if (lOMesh == null)
                continue;

            AssetDatabase.CreateAsset(lOMesh, string.Format("{0}/{1}.asset", lsLevelFolder, lOMesh.name));
        }

        string lsPrefabPath = string.Format("{0}/Level{1}.prefab", lsLevelFolder, piLevelIndex + 1);
        PrefabUtility.SaveAsPrefabAsset(lORoot, lsPrefabPath);
        DestroyImmediate(lORoot);

        lOWatch.Stop();

        if (pbRefresh)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        msStatus = string.Format("Level {0} baked: {1} chunks, {2} triangles, {3} ms.\n{4}",
            piLevelIndex + 1, lOFilters.Length, UWLevelAssembler.CountTriangles(lOChunks), lOWatch.ElapsedMilliseconds, lsPrefabPath);

        Debug.Log("UWBAKE: " + msStatus);

        return true;
    }
    public static void BakeAllFromCommandLine()
    {
        UWLevelWindow lOWindow = CreateInstance<UWLevelWindow>();

        lOWindow.fFindSettings();

        if (lOWindow.mOSettings == null)
            lOWindow.fCreateSettings();

        if (lOWindow.mOSettings == null || !lOWindow.mOSettings.IsDataPathValid)
        {
            Debug.LogError("UWBAKE: no valid data directory.");
            return;
        }

        lOWindow.fBakeAll();
        DestroyImmediate(lOWindow);
    }

    /// <summary>
    /// Enters the baked prefabs and the dungeon material into the
    /// UWLevelLoader of the open scene. Without this the loader recomputes the geometry
    /// on every level change.
    /// </summary>
    private void fAssignBakedLevels()
    {
        UWLevelLoader lOLoader = Object.FindAnyObjectByType<UWLevelLoader>();

        if (lOLoader == null)
        {
            msStatus = "There is no UWLevelLoader in the open scene.";
            return;
        }

        SerializedObject lOSerialised = new SerializedObject(lOLoader);
        SerializedProperty lOLevels = lOSerialised.FindProperty("mOBakedLevels");

        lOLevels.arraySize = 9;

        int liFound = 0;

        for (int i = 0; i < 9; i++)
        {
            string lsPath = string.Format("{0}/Level{1}/Level{1}.prefab", BakeFolder, i + 1);
            GameObject lOPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(lsPath);

            lOLevels.GetArrayElementAtIndex(i).objectReferenceValue = lOPrefab;

            if (lOPrefab != null)
                liFound++;
        }

        Material lOMaterial = AssetDatabase.LoadAssetAtPath<Material>(BakeFolder + "/Material.mat");

        if (lOMaterial != null)
            lOSerialised.FindProperty("mODungeonMaterial").objectReferenceValue = lOMaterial;

        lOSerialised.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(lOLoader.gameObject.scene);

        msStatus = string.Format("{0} of 9 baked levels assigned.{1}",
            liFound, lOMaterial == null ? " Material not found - bake first." : string.Empty);
    }
}
