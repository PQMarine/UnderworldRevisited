using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// ONE ICON PER PICTURE AND FILTER (cleanup step 5.4, 2026-10-19). An icon is a picture of the
/// game's files as palette indices for the shader UW/IconPalette (UWUploadPixels.IconIndices) -
/// it does not depend on the colour help or the palette rotation, both live in the shader's
/// tables, so it never has to be rebuilt.
///
/// Until 2026-10-19 six modern classes kept a dictionary of their own for the same icons, and
/// the classic HUD built a new texture (and sprite) on every change and never destroyed it: the
/// inventory slots, the open container, the scroll arrows, the selected runes and the active
/// spells (B33 of the cleanup plan), the conversation's edges and pictures, the options
/// buttons, the icon on the pointer. They all ask here now.
///
/// THE CACHE OWNS what it hands out: nobody destroys an icon or its sprite. The icons live as
/// long as the game's data (the data import survives a scene restart, UWLevelLoader) and are
/// forgotten when it is read anew from another folder (another palette set behind the pictures).
/// </summary>
public static class UWIconCache
{
    private struct Entry
    {
        public Texture2D Texture;

        public Sprite Sprite;
    }

    private static readonly Dictionary<(UWTexture, FilterMode), Entry> msEntries = new Dictionary<(UWTexture, FilterMode), Entry>();

    /// <summary>The palette set the cached icons were made from (one per data import).</summary>
    private static UWPalettes msPalettes;

    /// <summary>The icon texture of a picture (null for none).</summary>
    public static Texture2D Texture(UWTexture pOSource, FilterMode peFilter)
    {
        if (pOSource == null)
            return null;

        fForgetOtherData(pOSource);

        (UWTexture, FilterMode) lKey = (pOSource, peFilter);

        if (msEntries.TryGetValue(lKey, out Entry lOEntry) && lOEntry.Texture != null)
            return lOEntry.Texture;

        lOEntry.Texture = UWIconTextureBuilder.Build(pOSource, peFilter);
        lOEntry.Sprite = null;
        msEntries[lKey] = lOEntry;

        return lOEntry.Texture;
    }

    /// <summary>The icon as a sprite for an Image: the whole picture, pivot in the middle, one
    /// pixel per unit - what every classic Image made of an icon used.</summary>
    public static Sprite Sprite(UWTexture pOSource, FilterMode peFilter)
    {
        Texture2D lOTexture = Texture(pOSource, peFilter);

        if (lOTexture == null)
            return null;

        (UWTexture, FilterMode) lKey = (pOSource, peFilter);
        Entry lOEntry = msEntries[lKey];

        if (lOEntry.Sprite == null)
        {
            lOEntry.Sprite = UnityEngine.Sprite.Create(lOTexture, new Rect(0f, 0f, lOTexture.width, lOTexture.height), new Vector2(0.5f, 0.5f), 1f);
            msEntries[lKey] = lOEntry;
        }

        return lOEntry.Sprite;
    }

    /// <summary>Destroys every icon and sprite (when the data changes).</summary>
    private static void fClear()
    {
        foreach (Entry lOEntry in msEntries.Values)
        {
            if (lOEntry.Sprite != null)
                Object.Destroy(lOEntry.Sprite);

            if (lOEntry.Texture != null)
                Object.Destroy(lOEntry.Texture);
        }

        msEntries.Clear();
        msPalettes = null;
    }

    /// <summary>Pictures of another data import: the old icons can never be asked for again.</summary>
    private static void fForgetOtherData(UWTexture pOSource)
    {
        if (pOSource.Palettes == msPalettes)
            return;

        if (msPalettes != null)
            fClear();

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
