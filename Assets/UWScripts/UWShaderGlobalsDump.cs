using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnderworldRevisited;
using UnityEngine;

/// <summary>
/// Writes every _UW* shader global, the resource counts and where the camera stands into a
/// text file (Shift+F12, see DebugFunctions). The oracle of the rendering steps of the cleanup
/// (Docs/CLEANUP-PLAN.md, phase 0 and 6): a dump taken at a known spot before a change and one
/// taken there after it must be identical, except for the two values that move with time
/// (_UWRotationStep and the shadow and light arrays while something moves).
///
/// The names are the ones the scripts set (grep SetGlobal) and the shaders read; a global that
/// is neither a texture nor set at all prints as 0, which is the point: a refactor that forgets
/// to set one shows up as a changed line.
///
/// Since 2026-10-24 (cleanup step 6.1) the names come from UWShaderIds, sorted as the list here
/// was, so a dump of the build before and one after the step compare line by line. At the end
/// it checks the cache of UWShaderGlobals against what Unity holds: a "stale in UWShaderGlobals"
/// line appears only when they differ (a global written past the owner, a destroyed texture not
/// invalidated), so a dump of a sound build is the same as before the owner existed.
/// </summary>
public static class UWShaderGlobalsDump
{
    /// <summary>Writes the dump next to the settings and returns its path, or null when it failed.</summary>
    public static string Dump()
    {
        StringBuilder lOText = new StringBuilder();

        lOText.Append("Underworld Revisited shader globals, ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
        fAppendWhere(lOText);
        lOText.Append('\n');

        List<string> lONames = new List<string>(UWShaderIds.GlobalNames);

        lONames.Sort(StringComparer.Ordinal);

        foreach (string lsName in lONames)
            lOText.Append(lsName).Append(" = ").Append(fDescribe(lsName)).Append('\n');

        lOText.Append('\n');
        fAppendResources(lOText);

        int liStale = UWShaderGlobals.AppendStale(lOText);

        try
        {
            string lsPath = Path.Combine(Application.persistentDataPath,
                "shader-globals-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");

            File.WriteAllText(lsPath, lOText.ToString());
            Debug.Log("Shader globals written to " + lsPath);

            if (liStale > 0)
                Debug.LogWarning(liStale + " shader globals differ from the cache of UWShaderGlobals, see " + lsPath);

            if (UWScene.Interaction != null)
                UWScene.Interaction.AddMessage("Shader globals written to " + Path.GetFileName(lsPath));

            return lsPath;
        }
        catch (Exception lOError)
        {
            Debug.LogWarning("Shader globals not written: " + lOError.Message);

            return null;
        }
    }

    private static void fAppendWhere(StringBuilder pOText)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        Camera lOCamera = Camera.main;

        pOText.Append("level = ").Append(lOLoader != null ? (lOLoader.CurrentLevelIndex + 1).ToString() : "none").Append('\n');

        if (lOCamera != null)
        {
            Vector3 lOPosition = lOCamera.transform.position;
            Vector3 lOAngles = lOCamera.transform.eulerAngles;

            pOText.Append("camera = ").Append(fVector(lOPosition)).Append(" angles ").Append(fVector(lOAngles)).Append('\n');
            UnityEngine.Rendering.Universal.UniversalAdditionalCameraData lOData =
                lOCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();

            if (lOData != null)
                pOText.Append("camera flags = colour ").Append(lOData.requiresColorOption)
                    .Append(", depth ").Append(lOData.requiresDepthOption)
                    .Append(", post ").Append(lOData.renderPostProcessing).Append('\n');
        }

        pOText.Append("render mode = ").Append(UWSettings.Instance != null ? UWSettings.Instance.RenderMode.ToString() : "?")
            .Append(", detail ").Append(UWGraphicsDetail.CurrentLevel)
            .Append(", palette active ").Append(UWRenderPathHost.IsPaletteApplied).Append('\n');
        pOText.Append("screen = ").Append(Screen.width).Append('x').Append(Screen.height).Append('\n');
    }

    private static string fDescribe(string psName)
    {
        Texture lOTexture = Shader.GetGlobalTexture(psName);

        if (lOTexture != null)
            return fTexture(lOTexture);

        Vector4[] lOVectors = Shader.GetGlobalVectorArray(psName);

        if (lOVectors != null && lOVectors.Length > 0)
        {
            StringBuilder lOText = new StringBuilder("vectors[" + lOVectors.Length + "]");
            int liCount = 0;

            foreach (Vector4 lOVector in lOVectors)
            {
                if (lOVector == Vector4.zero)
                    continue;

                liCount++;

                if (liCount <= 8)
                    lOText.Append(' ').Append(fVector(lOVector));
            }

            return lOText.Append(" (").Append(liCount).Append(" non-zero)").ToString();
        }

        float[] lfFloats = Shader.GetGlobalFloatArray(psName);

        if (lfFloats != null && lfFloats.Length > 0)
            return "floats[" + lfFloats.Length + "] " + string.Join(" ", Array.ConvertAll(lfFloats, pfAt => pfAt.ToString("R")));

        Vector4 lOValue = Shader.GetGlobalVector(psName);

        if (lOValue.y != 0f || lOValue.z != 0f || lOValue.w != 0f)
            return "vector " + fVector(lOValue);

        return "float " + Shader.GetGlobalFloat(psName).ToString("R");
    }

    private static string fTexture(Texture pOTexture)
    {
        StringBuilder lOText = new StringBuilder();

        lOText.Append(pOTexture.GetType().Name).Append(' ').Append(pOTexture.width).Append('x').Append(pOTexture.height);

        if (pOTexture is Texture3D lOVolume)
            lOText.Append('x').Append(lOVolume.depth).Append(' ').Append(lOVolume.format);
        else if (pOTexture is Texture2D lOFlat)
            lOText.Append(' ').Append(lOFlat.format);
        else if (pOTexture is Texture2DArray lOArray)
            lOText.Append(" slices ").Append(lOArray.depth).Append(' ').Append(lOArray.format);
        else if (pOTexture is RenderTexture lORender)
            lOText.Append(' ').Append(lORender.format);

        lOText.Append(" filter ").Append(pOTexture.filterMode).Append(" wrap ").Append(pOTexture.wrapMode);
        lOText.Append(" \"").Append(pOTexture.name).Append('"');

        try
        {
            if (pOTexture is Texture2D lOReadable && lOReadable.isReadable)
                lOText.Append(" crc ").Append(fCrc32(lOReadable.GetRawTextureData<byte>().ToArray()).ToString("X8"));
            else if (pOTexture is Texture3D lOReadableVolume && lOReadableVolume.isReadable)
                lOText.Append(" crc ").Append(fCrc32(lOReadableVolume.GetPixelData<byte>(0).ToArray()).ToString("X8"));
        }
        catch (Exception lOError)
        {
            lOText.Append(" (pixels not readable: ").Append(lOError.GetType().Name).Append(')');
        }

        return lOText.ToString();
    }

    private static void fAppendResources(StringBuilder pOText)
    {
        pOText.Append("textures = ").Append(Resources.FindObjectsOfTypeAll<Texture2D>().Length).Append('\n');
        pOText.Append("render textures = ").Append(Resources.FindObjectsOfTypeAll<RenderTexture>().Length).Append('\n');
        pOText.Append("meshes = ").Append(Resources.FindObjectsOfTypeAll<Mesh>().Length).Append('\n');
        pOText.Append("materials = ").Append(Resources.FindObjectsOfTypeAll<Material>().Length).Append('\n');
        pOText.Append("sprites = ").Append(Resources.FindObjectsOfTypeAll<Sprite>().Length).Append('\n');
    }

    private static string fVector(Vector4 pOVector)
    {
        return string.Format("({0:R}, {1:R}, {2:R}, {3:R})", pOVector.x, pOVector.y, pOVector.z, pOVector.w);
    }

    private static string fVector(Vector3 pOVector)
    {
        return string.Format("({0:R}, {1:R}, {2:R})", pOVector.x, pOVector.y, pOVector.z);
    }

    private static uint[] msCrcTable;

    private static uint fCrc32(byte[] pyBytes)
    {
        if (msCrcTable == null)
        {
            msCrcTable = new uint[256];

            for (uint luAt = 0; luAt < 256; luAt++)
            {
                uint luCrc = luAt;

                for (int liBit = 0; liBit < 8; liBit++)
                    luCrc = (luCrc & 1) != 0 ? 0xEDB88320u ^ (luCrc >> 1) : luCrc >> 1;

                msCrcTable[luAt] = luCrc;
            }
        }

        uint luResult = 0xFFFFFFFFu;

        foreach (byte lyByte in pyBytes)
            luResult = msCrcTable[(luResult ^ lyByte) & 0xFF] ^ (luResult >> 8);

        return luResult ^ 0xFFFFFFFFu;
    }
}
