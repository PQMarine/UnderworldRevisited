using System;
using UnityEngine;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// TEXTURES BY KEY, EACH BUILT FOR A STAMP (cleanup step 5.4, 2026-10-19; UWStampedCache on the
/// engine-free side) - the modern parts that keep several of a kind: Get(key, stamp, build), or
/// TryGet and Set on the paths that run every frame. ReleaseAll destroys them all.
/// </summary>
public sealed class UWArtCache<TKey>
{
    private readonly UWStampedCache<TKey, Texture2D> mOCache =
        new UWStampedCache<TKey, Texture2D>(UWVersionedTexture.fDestroy, UWVersionedTexture.fIsAlive);

    public int Count => mOCache.Count;

    public bool TryGet(TKey pOKey, UWArtStamp pOStamp, out Texture2D pOTexture)
    {
        return mOCache.TryGet(pOKey, pOStamp, out pOTexture);
    }

    public Texture2D Set(TKey pOKey, UWArtStamp pOStamp, Texture2D pOTexture)
    {
        return mOCache.Set(pOKey, pOStamp, pOTexture);
    }

    public Texture2D Get(TKey pOKey, UWArtStamp pOStamp, Func<Texture2D> pOBuild)
    {
        return mOCache.Get(pOKey, pOStamp, pOBuild);
    }

    public void ReleaseAll()
    {
        mOCache.ReleaseAll();
    }
}
