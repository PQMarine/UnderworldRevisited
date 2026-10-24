using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE ONE PLACE WHERE A PICTURE BECOMES A UNITY TEXTURE (cleanup step 5.4, 2026-10-19). The
/// pixels are prepared engine-free (UWUploadPixels: rows turned bottom up, the colour help, the
/// transparency rule, icon indices, keep masks - decision 26 of the cleanup plan: the interface
/// must not hang on Unity); here they are only uploaded. It replaced UWGameUI.fGetTextureInvert
/// with its 22 callers and eleven copies of "new Texture2D + SetPixels32 + Apply",
/// UWModernHudArt.fToTexture and ApplyMask, the body of UWIconTextureBuilder and the uploads of
/// the procedural pictures (rings, circles, arrows, discs).
///
/// Every texture is RGBA32 without mipmaps and stays readable (some callers read it back:
/// the cursor scaling, the plate legs' drop, the shelf's keyed flasks). A few callers used ARGB32
/// until 2026-10-19; the two formats sample the same colours. The filter, the wrap mode, the
/// colour space and the name stay each caller's own, as they were.
///
/// The caller owns what it gets here and destroys it; shared pictures come from UWGrTextures,
/// UWIconCache and UWArtCache, which own theirs.
/// </summary>
public static class UWTextureFactory
{
    /// <summary>Finished bytes (UWUploadPixels: RGBA, the bottom row first) as a new texture.</summary>
    public static Texture2D Upload(byte[] pyRgba, int piWidth, int piHeight, FilterMode peFilter, TextureWrapMode peWrap,
        string psName, bool pbLinear = false)
    {
        Texture2D lOTexture = new Texture2D(piWidth, piHeight, TextureFormat.RGBA32, false, pbLinear);
        lOTexture.name = psName;
        lOTexture.filterMode = peFilter;
        lOTexture.wrapMode = peWrap;
        lOTexture.SetPixelData(pyRgba, 0);
        lOTexture.Apply(false, false);

        return lOTexture;
    }

    /// <summary>New bytes into a texture of this size that already exists (a picture following
    /// the colour help in place, UWGrTextures).</summary>
    public static void Reupload(Texture2D pOTexture, byte[] pyRgba)
    {
        pOTexture.SetPixelData(pyRgba, 0);
        pOTexture.Apply(false, false);
    }

    /// <summary>A composed picture (UWHudArt and friends, rows top down) with the colour help,
    /// transparent pixels cleared - the modern HUD's art (UWModernHudArt.fToTexture until
    /// 2026-10-19).</summary>
    public static Texture2D FromPicture(UWPicture pOPicture, FilterMode peFilter, string psName)
    {
        return Upload(UWUploadPixels.FromPicture(pOPicture, UWColourVision.Filter), pOPicture.Width, pOPicture.Height, peFilter,
            TextureWrapMode.Clamp, psName);
    }

    /// <summary>A composed picture with the game pictures' transparency rule (colours kept) - the
    /// Classic Wide frame, MAIN.BYT widened.</summary>
    public static Texture2D FromPictureKeepingColours(UWPicture pOPicture, FilterMode peFilter, TextureWrapMode peWrap, string psName)
    {
        return Upload(UWUploadPixels.FromPicture(pOPicture, UWColourVision.Filter, UWUploadPixels.TransparentEnum.Keep),
            pOPicture.Width, pOPicture.Height, peFilter, peWrap, psName);
    }

    /// <summary>One of the game's pictures in its colours with the colour help (the rule of
    /// UWGameUI.fGetTextureInvert). The wrap mode is the caller's: Repeat is what a new texture
    /// had where a caller did not set one.</summary>
    public static Texture2D FromTexture(UWTexture pOTexture, FilterMode peFilter, string psName,
        TextureWrapMode peWrap = TextureWrapMode.Repeat)
    {
        return Upload(UWUploadPixels.FromTexture(pOTexture, UWColourVision.Filter), pOTexture.Width, pOTexture.Height, peFilter, peWrap,
            psName);
    }

    /// <summary>The same pixels as FromTexture as Unity colours (rows bottom up), for the few
    /// pictures still put together on the Unity side (the compass composite, the help window's
    /// grey runes).</summary>
    public static Color32[] Colours(UWTexture pOTexture)
    {
        return ToColours(UWUploadPixels.FromTexture(pOTexture, UWColourVision.Filter));
    }

    /// <summary>An icon for the palette shader UW/IconPalette: palette indices in red, opacity in
    /// alpha, LINEAR so that no gamma curve touches the index (see UWIconTextureBuilder).</summary>
    public static Texture2D FromIndices(UWTexture pOTexture, FilterMode peFilter, string psName)
    {
        return Upload(UWUploadPixels.IconIndices(pOTexture), pOTexture.Width, pOTexture.Height, peFilter, TextureWrapMode.Repeat,
            psName, true);
    }

    /// <summary>Colours made on the Unity side (rows bottom up) - the procedural rings, circles,
    /// arrows and discs of the modern scheme.</summary>
    public static Texture2D FromColours(Color32[] pyBottomUp, int piWidth, int piHeight, FilterMode peFilter, TextureWrapMode peWrap,
        string psName)
    {
        Texture2D lOTexture = new Texture2D(piWidth, piHeight, TextureFormat.RGBA32, false);
        lOTexture.name = psName;
        lOTexture.filterMode = peFilter;
        lOTexture.wrapMode = peWrap;
        lOTexture.SetPixels32(pyBottomUp);
        lOTexture.Apply(false, false);

        return lOTexture;
    }

    /// <summary>A copy of a texture with the pixels outside a top-down keep mask transparent
    /// (UWModernHudArt.ApplyMask until 2026-10-19); the source's filter, wrap Clamp.</summary>
    public static Texture2D ApplyMask(Texture2D pOSource, bool[] pbKeepTopDown)
    {
        int liWidth = pOSource.width;
        int liHeight = pOSource.height;
        byte[] lyKept = UWUploadPixels.Keep(ToBytes(pOSource.GetPixels32()), liWidth, liHeight, pbKeepTopDown);

        return Upload(lyKept, liWidth, liHeight, pOSource.filterMode, TextureWrapMode.Clamp, "UWModernHudArt keyed");
    }

    /// <summary>Upload bytes as Unity colours.</summary>
    public static Color32[] ToColours(byte[] pyRgba)
    {
        Color32[] lyOut = new Color32[pyRgba.Length / UWUploadPixels.BytesPerPixel];

        for (int liAt = 0, liByte = 0; liAt < lyOut.Length; liAt++, liByte += UWUploadPixels.BytesPerPixel)
            lyOut[liAt] = new Color32(pyRgba[liByte], pyRgba[liByte + 1], pyRgba[liByte + 2], pyRgba[liByte + 3]);

        return lyOut;
    }

    /// <summary>Unity colours as upload bytes.</summary>
    public static byte[] ToBytes(Color32[] pyColours)
    {
        byte[] lyOut = new byte[pyColours.Length * UWUploadPixels.BytesPerPixel];

        for (int liAt = 0, liByte = 0; liAt < pyColours.Length; liAt++, liByte += UWUploadPixels.BytesPerPixel)
        {
            lyOut[liByte] = pyColours[liAt].r;
            lyOut[liByte + 1] = pyColours[liAt].g;
            lyOut[liByte + 2] = pyColours[liAt].b;
            lyOut[liByte + 3] = pyColours[liAt].a;
        }

        return lyOut;
    }
}
