using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE GAME'S PICTURES AS SHARED TEXTURES (cleanup step 5.4, 2026-10-19): one texture per
/// picture (.GR type and index), filter and look, made once and kept for as long as the game's
/// data is loaded (the data import itself survives a scene restart, UWLevelLoader).
///
/// THREE LOOKS, each the rule its callers used before:
///   Icon    - palette indices for the shader UW/IconPalette (UWIconCache): inventory icons,
///             runes, spells, the options buttons, the scroll edges.
///   Picture - colours with the colour help, transparent pixels cleared, clamped: the modern
///             pages (UWModernHudArt.BuildPicture and BuildPanelPage until 2026-10-19).
///   Colours - colours with the colour help, transparent pixels keeping theirs, clamped: the
///             original's panel pages as the modern rune tablet showed them.
///
/// THE COLOUR HELP is followed IN PLACE: when UWColourVision.Version has changed since a
/// colour picture was made, the next Get uploads its pixels anew into the same texture, so
/// whoever shows it never holds a destroyed one. A caller that sets a picture once and never
/// asks again must therefore not take a colour picture from here if it is to keep the colours
/// it was built with - the classic panels, the compass, the map and the frame make theirs with
/// UWTextureFactory and own them (they follow the colour help when they are built, as before).
///
/// THE CACHE OWNS what it hands out: nobody destroys these textures.
/// </summary>
public static class UWGrTextures
{
    public enum ModeEnum
    {
        Icon = 0,
        Picture = 1,
        Colours = 2
    }

    private sealed class Entry
    {
        public Texture2D Texture;

        public int ColourVersion;
    }

    private static readonly Dictionary<(UWTexture, FilterMode, ModeEnum), Entry> msEntries =
        new Dictionary<(UWTexture, FilterMode, ModeEnum), Entry>();

    /// <summary>The palette set the cached pictures were made from (one per data import).</summary>
    private static UWPalettes msPalettes;

    /// <summary>A picture of the game's files, or null without it (a missing index, a file with
    /// fewer pictures, an empty picture).</summary>
    public static UWTexture Source(UWTextures pOData, UWTexture.TextureTypes peType, int piIndex)
    {
        if (pOData == null)
            return null;

        UWTexture lOSource;

        try
        {
            lOSource = pOData.GetTextureByType(peType, piIndex);
        }
        catch
        {
            return null;
        }

        return lOSource != null && lOSource.Width > 0 && lOSource.Height > 0 ? lOSource : null;
    }

    /// <summary>A picture of the game's files in a look; null without it.</summary>
    public static Texture2D Get(UWTextures pOData, UWTexture.TextureTypes peType, int piIndex, FilterMode peFilter, ModeEnum peMode)
    {
        UWTexture lOSource = Source(pOData, peType, piIndex);

        if (lOSource == null)
            return null;

        if (peMode == ModeEnum.Icon)
            return UWIconCache.Texture(lOSource, peFilter);

        fForgetOtherData(lOSource);

        (UWTexture, FilterMode, ModeEnum) lKey = (lOSource, peFilter, peMode);
        int liVersion = UWColourVision.Version;

        if (msEntries.TryGetValue(lKey, out Entry lOEntry) && lOEntry.Texture != null)
        {
            if (lOEntry.ColourVersion != liVersion)
            {
                UWTextureFactory.Reupload(lOEntry.Texture, fPixels(lOSource, peMode));
                lOEntry.ColourVersion = liVersion;
            }

            return lOEntry.Texture;
        }

        Texture2D lOTexture = UWTextureFactory.Upload(fPixels(lOSource, peMode), lOSource.Width, lOSource.Height, peFilter,
            TextureWrapMode.Clamp, "UWGrTextures " + peType + " " + piIndex + " " + peMode);

        msEntries[lKey] = new Entry { Texture = lOTexture, ColourVersion = liVersion };

        return lOTexture;
    }

    /// <summary>A picture of the game's files as an icon sprite for an Image (UWIconCache.Sprite);
    /// null without it.</summary>
    public static Sprite IconSprite(UWTextures pOData, UWTexture.TextureTypes peType, int piIndex, FilterMode peFilter)
    {
        return UWIconCache.Sprite(Source(pOData, peType, piIndex), peFilter);
    }

    private static byte[] fPixels(UWTexture pOSource, ModeEnum peMode)
    {
        return peMode == ModeEnum.Picture
            ? UWUploadPixels.FromPicture(UWPicture.From(pOSource), UWColourVision.Filter)
            : UWUploadPixels.FromTexture(pOSource, UWColourVision.Filter);
    }

    /// <summary>Pictures of another data import (a new game folder): the old ones can never be
    /// asked for again.</summary>
    private static void fForgetOtherData(UWTexture pOSource)
    {
        if (pOSource.Palettes == msPalettes)
            return;

        foreach (Entry lOEntry in msEntries.Values)
        {
            if (lOEntry.Texture != null)
                Object.Destroy(lOEntry.Texture);
        }

        msEntries.Clear();
        msPalettes = pOSource.Palettes;
    }

    /// <summary>With the editor's domain reload off the statics survive a play session.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void fReset()
    {
        msEntries.Clear();
        msPalettes = null;
    }
}
