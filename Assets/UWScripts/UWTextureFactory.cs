using UnityEngine;
using UWDataImport;
using UWDataImport.Presentation;
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
///
/// Since cleanup step 6.2 (2026-10-24) the renderer's lookup tables are made here as well (see
/// "Lookup tables" below); the world's texture arrays come from UWTextureCache.
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

    // ------------------------------------------------- Lookup tables

    // THE RENDERER'S LOOKUP TABLES (cleanup step 6.2, 2026-10-24): the bytes come engine-free
    // from UWLookupTables, the texture settings of every table live here - so the frozen flags
    // (Docs/CLEANUP-PLAN.md section 11: colour tables linear = false, index tables linear = true,
    // no mipmaps on index textures) hold by construction. Until 2026-10-24 UWShadePalette,
    // UWTextureArrayBuilder.BuildMaterialLookup and UWPaletteEffects each built theirs with their
    // own "new Texture2D, Point, Clamp, fill, Apply". Every table is Point, Clamp, without mipmaps
    // and stays readable (the inverse cube excepted, as before). The caller owns what it gets:
    // UWPaletteCommon the palette tables, UWTextureCache the material lookup, UWPaletteEffects the
    // grime tint.
    //
    // COLOUR SPACE (see UWShadePalette's class comment): a COLOUR table holds real colours and is
    // sRGB (linear = false); an INDEX table holds palette indices or steps and is linear, so no
    // gamma curve would ever touch an index. Today the project runs in gamma colour space and
    // Unity converts nothing - it only matters if the project is ever switched to linear.

    /// <summary>The colour table (UWLookupTables.ColourTable): LIGHT.DAT, or MONO.DAT for Night
    /// Vision, through palette piPalette (the hallucination's other palette) with the colour
    /// help.</summary>
    public static Texture2D ColourTable(DataImport pOData, int piPalette = 0, bool pbMonochrome = false)
    {
        byte[] lyPixels = UWLookupTables.ColourTable(pOData, piPalette, pbMonochrome, UWColourVision.Filter, out bool lbMono);

        return lyPixels == null ? null : fColourTable(lbMono ? "UWColourTable (mono)" : "UWColourTable", UWLookupTables.PaletteSize,
            UWLookupTables.ShadeLevelCount, lyPixels);
    }

    /// <summary>The shade table (UWLookupTables.ShadeTable), one byte per cell.</summary>
    public static Texture2D ShadeTable(DataImport pOData, bool pbDiagonalStretch = false)
    {
        byte[] lyPixels = UWLookupTables.ShadeTable(pOData, pbDiagonalStretch);

        return lyPixels == null ? null : fIndexTable("UWShadeTable", UWLookupTables.DistanceCount, UWLookupTables.LightLevelCount,
            TextureFormat.R8, lyPixels);
    }

    /// <summary>The rotation table (UWLookupTables.RotationTable): indices in red.</summary>
    public static Texture2D RotationTable()
    {
        return fIndexTable("UWRotationTable", UWLookupTables.PaletteSize, UWLookupTables.RotationStepCount, TextureFormat.RGBA32,
            UWLookupTables.RotationTable());
    }

    /// <summary>The inverse cube of the XFER mixing (UWLookupTables.InverseLookup): indices, so
    /// linear (a Texture3D is), and no longer readable after the upload, as it always was.</summary>
    public static Texture3D InverseLookup(DataImport pOData, bool pbColourHelp = true)
    {
        byte[] lyCube = UWLookupTables.InverseLookup(pOData, pbColourHelp ? UWColourVision.Filter : null);

        if (lyCube == null)
            return null;

        int liSize = UWTransparencyTables.InverseLookupSize;
        Texture3D lOCube = new Texture3D(liSize, liSize, liSize, TextureFormat.R8, false);

        lOCube.name = pbColourHelp ? "UWInverseLookup" : "UWInverseLookup (raw)";
        lOCube.filterMode = FilterMode.Point;
        lOCube.wrapMode = TextureWrapMode.Clamp;
        lOCube.SetPixelData(lyCube, 0);
        lOCube.Apply(false, true);

        return lOCube;
    }

    /// <summary>What every table of XFER.DAT makes of every index (UWLookupTables.XferColours).</summary>
    public static Texture2D XferColours(DataImport pOData, bool pbColourHelp = true)
    {
        byte[] lyPixels = UWLookupTables.XferColours(pOData, pbColourHelp ? UWColourVision.Filter : null);

        return lyPixels == null ? null : fColourTable(pbColourHelp ? "UWXferColours" : "UWXferColours (raw)",
            UWLookupTables.PaletteSize, UWTransparencyTables.Count, lyPixels);
    }

    /// <summary>The hallucination's light table effect (UWLookupTables.LightTableEffect).</summary>
    public static Texture2D LightTableEffect(DataImport pOData, bool pbColourHelp = true)
    {
        byte[] lyPixels = UWLookupTables.LightTableEffect(pOData, pbColourHelp ? UWColourVision.Filter : null);

        return lyPixels == null ? null : fColourTable(pbColourHelp ? "UWLightTableEffect" : "UWLightTableEffect (raw)",
            UWLookupTables.PaletteSize, lyPixels.Length / (UWLookupTables.PaletteSize * UWLookupTables.BytesPerColour), lyPixels);
    }

    /// <summary>The eight palettes in raw colours (UWLookupTables.RawPalettes), for Remastered.</summary>
    public static Texture2D RawPalettes(DataImport pOData)
    {
        byte[] lyPixels = UWLookupTables.RawPalettes(pOData);

        return lyPixels == null ? null : fColourTable("UWRawPalettes", UWLookupTables.PaletteSize, UWLookupTables.RawPaletteCount,
            lyPixels);
    }

    /// <summary>The grime's colour tables (UWLookupTables.GrimeTint): palette indices.</summary>
    public static Texture2D GrimeTintTable(UWPalette pOPalette)
    {
        return fIndexTable("UW Grime Tint", UWGrimeTint.PaletteSize, UWGrimeTint.ToneCount, TextureFormat.R8,
            UWLookupTables.GrimeTint(pOPalette));
    }

    /// <summary>Remastered's gloss and glow per palette index (UWLookupTables.MaterialLookup) -
    /// looked up by index, so linear like an index table.</summary>
    public static Texture2D MaterialLookup()
    {
        return fIndexTable("UWSurfaces", UWLookupTables.PaletteSize, 1, TextureFormat.RGBA32, UWLookupTables.MaterialLookup());
    }

    /// <summary>A table of real colours: RGBA, sRGB (linear = false).</summary>
    private static Texture2D fColourTable(string psName, int piWidth, int piHeight, byte[] pyRgba)
    {
        return fTable(psName, piWidth, piHeight, TextureFormat.RGBA32, false, pyRgba);
    }

    /// <summary>A table of indices or steps: linear, whatever its format.</summary>
    private static Texture2D fIndexTable(string psName, int piWidth, int piHeight, TextureFormat peFormat, byte[] pyBytes)
    {
        return fTable(psName, piWidth, piHeight, peFormat, true, pyBytes);
    }

    private static Texture2D fTable(string psName, int piWidth, int piHeight, TextureFormat peFormat, bool pbLinear, byte[] pyBytes)
    {
        Texture2D lOTable = new Texture2D(piWidth, piHeight, peFormat, false, pbLinear);

        lOTable.name = psName;
        lOTable.filterMode = FilterMode.Point;
        lOTable.wrapMode = TextureWrapMode.Clamp;
        lOTable.SetPixelData(pyBytes, 0);
        lOTable.Apply(false, false);

        return lOTable;
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
