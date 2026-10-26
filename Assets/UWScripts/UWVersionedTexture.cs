using System;
using UnityEngine;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// ONE TEXTURE BUILT FOR A STAMP (cleanup step 5.4, 2026-10-19; UWStampedSlot on the engine-free
/// side): a modern part asks IsCurrent(stamp) each frame and builds only when the colours, the
/// backs or its own size changed - Set destroys the texture it replaces. Allocation-free on the
/// path that runs every frame:
///
///     UWArtStamp lOStamp = UWVersionedTexture.Colours(liWidth, liHeight);
///     if (!mOBack.IsCurrent(lOStamp))
///         mOBackImage.texture = mOBack.Set(lOStamp, UWModernHudArt.BuildLeather(...));
///
/// It replaced the hand-kept "texture, version, size" field triples of the modern scheme
/// (CLEANUP-PLAN 8.1, D7 of the modern UI review).
/// </summary>
public sealed class UWVersionedTexture
{
    private readonly UWStampedSlot<Texture2D> mOSlot = new UWStampedSlot<Texture2D>(fDestroy, fIsAlive);

    public Texture2D Texture => mOSlot.Art;

    public bool IsCurrent(UWArtStamp pOStamp)
    {
        return mOSlot.IsCurrent(pOStamp);
    }

    /// <summary>Keeps a texture built for a stamp and destroys the one it replaces.</summary>
    public Texture2D Set(UWArtStamp pOStamp, Texture2D pOTexture)
    {
        return mOSlot.Set(pOStamp, pOTexture);
    }

    /// <summary>The texture for a stamp, built when stale (for paths that do not run every frame:
    /// the lambda allocates).</summary>
    public Texture2D Ensure(UWArtStamp pOStamp, Func<Texture2D> pOBuild)
    {
        return mOSlot.IsCurrent(pOStamp) ? mOSlot.Art : mOSlot.Set(pOStamp, pOBuild());
    }

    /// <summary>Destroys the texture (OnDestroy, or to force a rebuild).</summary>
    public void Release()
    {
        mOSlot.Release();
    }

    /// <summary>A stamp on the colour help alone (UWColourVision.Version) and up to three numbers.</summary>
    public static UWArtStamp Colours(int piA = 0, int piB = 0, int piC = 0)
    {
        return new UWArtStamp(UWColourVision.Version, piA, piB, piC);
    }

    /// <summary>A stamp on the colour help and the modern backs (UWModernBacks.ArtVersion) and up
    /// to three numbers.</summary>
    public static UWArtStamp Backs(int piA = 0, int piB = 0, int piC = 0)
    {
        return new UWArtStamp(UWModernBacks.ArtVersion, piA, piB, piC);
    }

    internal static void fDestroy(Texture2D pOTexture)
    {
        if (pOTexture != null)
            UnityEngine.Object.Destroy(pOTexture);
    }

    /// <summary>Unity's own null test: a destroyed texture is no texture.</summary>
    internal static bool fIsAlive(Texture2D pOTexture)
    {
        return pOTexture != null;
    }
}
