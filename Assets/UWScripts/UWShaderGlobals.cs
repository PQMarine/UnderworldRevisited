using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// THE ONE OWNER OF THE GLOBAL SHADER VALUES (cleanup step 6.1, 2026-10-24): every
/// Shader.SetGlobal* of the game goes through here, by an id of UWShaderIds. It remembers the
/// last value written per id and writes only when the value changes, so the callers may keep
/// handing over their values every frame - what does not change costs a lookup, not a write - and
/// what really changes every frame (the light level, the rotation step, the shadow array, the
/// light map of the palette effects) still reaches the shaders that frame.
///
/// Values are compared exactly (the bits of a float, each component of a vector, the reference of
/// a texture), never approximately: a skipped write must leave exactly the value the old code
/// would have written. A texture filled anew in place (SetPixelData and Apply on the same object,
/// as the sweep mask and the light map do) needs no new write - the global holds the object.
///
/// STALE CACHE (Docs/CLEANUP-PLAN.md section 11, "Palette path"): a texture that is destroyed
/// while it is set must be dropped from the cache with Invalidate before or when it goes, so the
/// next set writes again whatever it is. The cache also starts empty with every play session
/// (SubsystemRegistration), as the editor may keep statics with domain reload off.
/// </summary>
public static class UWShaderGlobals
{
    private static readonly Dictionary<int, float> msFloats = new Dictionary<int, float>();

    private static readonly Dictionary<int, Vector4> msVectors = new Dictionary<int, Vector4>();

    private static readonly Dictionary<int, Texture> msTextures = new Dictionary<int, Texture>();

    /// <summary>A copy of the last array written per id (Unity copies it as well, so the caller
    /// may fill the same array again).</summary>
    private static readonly Dictionary<int, Vector4[]> msVectorArrays = new Dictionary<int, Vector4[]>();

    private static readonly List<int> msScratch = new List<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void fResetCache()
    {
        InvalidateAll();
    }

    public static void SetFloat(int piId, float pfValue)
    {
        if (msFloats.TryGetValue(piId, out float lfLast) && fSame(lfLast, pfValue))
            return;

        msFloats[piId] = pfValue;
        Shader.SetGlobalFloat(piId, pfValue);
    }

    public static void SetVector(int piId, Vector4 pOValue)
    {
        if (msVectors.TryGetValue(piId, out Vector4 lOLast) && fSame(lOLast, pOValue))
            return;

        msVectors[piId] = pOValue;
        Shader.SetGlobalVector(piId, pOValue);
    }

    public static void SetTexture(int piId, Texture pOValue)
    {
        if (msTextures.TryGetValue(piId, out Texture lOLast) && ReferenceEquals(lOLast, pOValue))
            return;

        msTextures[piId] = pOValue;
        Shader.SetGlobalTexture(piId, pOValue);
    }

    public static void SetVectorArray(int piId, Vector4[] pOValues)
    {
        if (pOValues == null)
            return;

        msVectorArrays.TryGetValue(piId, out Vector4[] lOLast);

        if (lOLast != null && lOLast.Length == pOValues.Length && fSame(lOLast, pOValues))
            return;

        if (lOLast == null || lOLast.Length != pOValues.Length)
        {
            lOLast = new Vector4[pOValues.Length];
            msVectorArrays[piId] = lOLast;
        }

        Array.Copy(pOValues, lOLast, pOValues.Length);
        Shader.SetGlobalVectorArray(piId, pOValues);
    }

    /// <summary>
    /// Takes a texture global away (none). For a render path that goes and leaves the globals
    /// only it reads clean (IUWRenderPath.Deactivate, cleanup step 6.5, 2026-10-24): the dump
    /// then shows no texture there, and the next set writes the texture again.
    /// </summary>
    public static void ClearTexture(int piId)
    {
        SetTexture(piId, null);
    }

    /// <summary>
    /// Sets a vector array global to zeros, at the length it was written with (Unity keeps an
    /// array global's length from its first write, so it cannot be taken away). Nothing happens for
    /// an array never written. For a render path that goes, like ClearTexture.
    /// </summary>
    public static void ClearVectorArray(int piId)
    {
        if (!msVectorArrays.TryGetValue(piId, out Vector4[] lOLast))
            return;

        bool lbZero = true;

        for (int liAt = 0; lbZero && liAt < lOLast.Length; liAt++)
            lbZero = fSame(lOLast[liAt], Vector4.zero);

        if (lbZero)
            return;

        Array.Clear(lOLast, 0, lOLast.Length);
        Shader.SetGlobalVectorArray(piId, lOLast);
    }

    /// <summary>Forgets what was written for this id: the next set writes again.</summary>
    public static void Invalidate(int piId)
    {
        msFloats.Remove(piId);
        msVectors.Remove(piId);
        msTextures.Remove(piId);
        msVectorArrays.Remove(piId);
    }

    /// <summary>Forgets every id that holds this texture - for a texture about to be destroyed.</summary>
    public static void Invalidate(Texture pOTexture)
    {
        if (ReferenceEquals(pOTexture, null))
            return;

        msScratch.Clear();

        foreach (KeyValuePair<int, Texture> lOEntry in msTextures)
        {
            if (ReferenceEquals(lOEntry.Value, pOTexture))
                msScratch.Add(lOEntry.Key);
        }

        foreach (int liId in msScratch)
            msTextures.Remove(liId);
    }

    public static void InvalidateAll()
    {
        msFloats.Clear();
        msVectors.Clear();
        msTextures.Clear();
        msVectorArrays.Clear();
    }

    /// <summary>
    /// The check for the dump (UWShaderGlobalsDump): every remembered value against what Unity
    /// holds now, one line per id that differs - none when the cache is right. A line here means a
    /// global was written past this owner, or a destroyed texture was not invalidated. Returns
    /// how many differ.
    /// </summary>
    public static int AppendStale(StringBuilder pOText)
    {
        int liStale = 0;

        foreach (KeyValuePair<int, float> lOEntry in msFloats)
        {
            float lfNow = Shader.GetGlobalFloat(lOEntry.Key);

            if (!fSame(lfNow, lOEntry.Value))
                liStale += fAppendStale(pOText, lOEntry.Key, lOEntry.Value.ToString("R"), lfNow.ToString("R"));
        }

        foreach (KeyValuePair<int, Vector4> lOEntry in msVectors)
        {
            Vector4 lONow = Shader.GetGlobalVector(lOEntry.Key);

            if (!fSame(lONow, lOEntry.Value))
                liStale += fAppendStale(pOText, lOEntry.Key, lOEntry.Value.ToString("R"), lONow.ToString("R"));
        }

        foreach (KeyValuePair<int, Texture> lOEntry in msTextures)
        {
            Texture lONow = Shader.GetGlobalTexture(lOEntry.Key);

            // Unity's equality: a destroyed texture counts as null on both sides.
            if (lONow != lOEntry.Value)
                liStale += fAppendStale(pOText, lOEntry.Key, fTextureName(lOEntry.Value), fTextureName(lONow));
        }

        foreach (KeyValuePair<int, Vector4[]> lOEntry in msVectorArrays)
        {
            Vector4[] lONow = Shader.GetGlobalVectorArray(lOEntry.Key);
            bool lbSame = lONow != null && lONow.Length >= lOEntry.Value.Length;

            for (int liAt = 0; lbSame && liAt < lOEntry.Value.Length; liAt++)
                lbSame = fSame(lONow[liAt], lOEntry.Value[liAt]);

            if (!lbSame)
                liStale += fAppendStale(pOText, lOEntry.Key, "vectors[" + lOEntry.Value.Length + "]",
                    lONow != null ? "vectors[" + lONow.Length + "], other values" : "none");
        }

        return liStale;
    }

    private static int fAppendStale(StringBuilder pOText, int piId, string psCached, string psNow)
    {
        pOText.Append("stale in UWShaderGlobals: ").Append(UWShaderIds.GlobalName(piId) ?? piId.ToString())
            .Append(" cached ").Append(psCached).Append(", set ").Append(psNow).Append('\n');

        return 1;
    }

    private static string fTextureName(Texture pOTexture)
    {
        return pOTexture == null ? "none" : "\"" + pOTexture.name + "\"";
    }

    private static bool fSame(float pfA, float pfB)
    {
        return BitConverter.SingleToInt32Bits(pfA) == BitConverter.SingleToInt32Bits(pfB);
    }

    private static bool fSame(Vector4 pOA, Vector4 pOB)
    {
        return fSame(pOA.x, pOB.x) && fSame(pOA.y, pOB.y) && fSame(pOA.z, pOB.z) && fSame(pOA.w, pOB.w);
    }

    private static bool fSame(Vector4[] pOA, Vector4[] pOB)
    {
        for (int liAt = 0; liAt < pOA.Length; liAt++)
        {
            if (!fSame(pOA[liAt], pOB[liAt]))
                return false;
        }

        return true;
    }
}
