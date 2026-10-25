using UnityEngine;
using UWDataImport;
using UnderworldRevisited.Build;

/// <summary>
/// THE ONE OWNER OF THE WORLD'S DERIVED TEXTURE ARRAYS (cleanup step 6.2, 2026-10-24): the index
/// array (UWTextureArrayBuilder.BuildIndexed, the palette index per pixel of every wall and floor),
/// the normal and height array (BuildNormals) and Remastered's material lookup. Until 2026-10-24
/// the palette toggle and UWRemasterRenderer each built an index array of their own, and
/// UWPaletteEffects and UWRemasterRenderer each a normal array - the same function with the same
/// strength, twice the memory, and rebuilt with every loaded save. Now there is one of each,
/// shared by both render paths, kept across scenes (they depend on the game data only).
///
/// OWNERSHIP: the cache creates and destroys these textures, nobody else does - the callers hold
/// references only (Docs/CLEANUP-PLAN.md section 11, "Texture ownership": moved in one step, or
/// a texture is destroyed twice). Before it destroys one it drops it from UWShaderGlobals
/// (Invalidate), since the palette effects set the normal array as a global.
///
/// STALENESS: the normal array is built for one height strength (UWSettings
/// RemasterHeightStrength); asked for another, it is built again (B25 - the palette effects'
/// copy stayed at the first strength until 2026-10-11). Both arrays are built again for another
/// game data set (another DATA folder), and dropped with the old one.
/// </summary>
public static class UWTextureCache
{
    private static DataImport msOData;

    private static Texture2DArray msOIndexArray;

    private static Texture2DArray msONormalArray;

    /// <summary>The height strength the normal array was built with.</summary>
    private static float msfNormalStrength;

    private static Texture2D msOMaterialLookup;

    /// <summary>A new play session starts with an empty cache (the editor may keep statics with
    /// domain reload off; the textures of the last session are gone then).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void fReset()
    {
        msOData = null;
        msOIndexArray = null;
        msONormalArray = null;
        msfNormalStrength = 0f;
        msOMaterialLookup = null;
    }

    /// <summary>The palette indices of every wall and floor (R8, linear, no mipmaps, Point - see
    /// UWTextureArrayBuilder.BuildIndexed), built when first asked for.</summary>
    public static Texture2DArray GetIndexArray(DataImport pOData)
    {
        if (pOData == null)
            return null;

        fFollowData(pOData);

        if (msOIndexArray == null)
            msOIndexArray = UWTextureArrayBuilder.BuildIndexed(pOData);

        return msOIndexArray;
    }

    /// <summary>The normals and heights for this height strength, built when first asked for and
    /// again when the strength changes - the old array is destroyed then, so a caller must ask
    /// again (IsCurrentNormalArray) instead of keeping what it got.</summary>
    public static Texture2DArray GetNormalArray(DataImport pOData, float pfHeightStrength)
    {
        if (pOData == null)
            return null;

        fFollowData(pOData);

        if (msONormalArray != null && Mathf.Approximately(msfNormalStrength, pfHeightStrength))
            return msONormalArray;

        fDestroy(ref msONormalArray);

        float lfStart = Time.realtimeSinceStartup;

        msONormalArray = UWTextureArrayBuilder.BuildNormals(pOData, pfHeightStrength);
        msfNormalStrength = pfHeightStrength;

        Debug.Log(string.Format("Texture cache: built normals from {0} textures in {1:0} ms.",
            msONormalArray.depth, (Time.realtimeSinceStartup - lfStart) * 1000f));

        return msONormalArray;
    }

    /// <summary>Whether this array is the one GetNormalArray would hand out for this data and
    /// strength now - false for a destroyed one.</summary>
    public static bool IsCurrentNormalArray(Texture2DArray pOArray, DataImport pOData, float pfHeightStrength)
    {
        return pOArray != null && pOArray == msONormalArray && ReferenceEquals(pOData, msOData)
            && Mathf.Approximately(msfNormalStrength, pfHeightStrength);
    }

    /// <summary>Remastered's gloss and glow per palette index (UWTextureFactory.MaterialLookup);
    /// depends on no data.</summary>
    public static Texture2D GetMaterialLookup()
    {
        if (msOMaterialLookup == null)
            msOMaterialLookup = UWTextureFactory.MaterialLookup();

        return msOMaterialLookup;
    }

    /// <summary>Another data set: what was built from the old one goes.</summary>
    private static void fFollowData(DataImport pOData)
    {
        if (ReferenceEquals(pOData, msOData))
            return;

        fDestroy(ref msOIndexArray);
        fDestroy(ref msONormalArray);
        msOData = pOData;
    }

    private static void fDestroy(ref Texture2DArray pOArray)
    {
        if (pOArray != null)
        {
            UWShaderGlobals.Invalidate(pOArray);
            Object.Destroy(pOArray);
        }

        pOArray = null;
    }
}
