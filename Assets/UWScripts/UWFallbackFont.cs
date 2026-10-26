using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UWDataImport.UWData;
using Object = UnityEngine.Object;

/// <summary>
/// THE LANGUAGE'S FONT (translation wave 6, 2026-10-26): the fonts a language names in its header
/// (grammar.rule.font, UWLanguageFont) draw what the port's own fonts lack - Chinese, Japanese, Korean,
/// Cyrillic, Greek. No font is shipped with the game; a translator puts one beside his texts
/// (Languages/zh-TW.otf) or names one the system has (os:Microsoft JhengHei). Put in force with the
/// language (UWLanguage.Apply calls Apply), so the switch of language takes it at once.
///
/// THE CLASSIC INTERFACE (the original's FONT*.SYS, drawn engine-free by UWTextRaster): this class is
/// the fonts' fallback (UWFont.Fallback, IUWGlyphSource). A character beyond ASCII that a font neither
/// has nor got added is rasterised by the first entry of the list that has it, MONOCHROME (TextCore's
/// RASTER_HINTED: every pixel ink or not, no grey to threshold), at the size where the entry's capitals
/// are as tall as the original font's, and fitted into its rows by the engine-free rule
/// (UWFallbackGlyphs) - in the original's colour, as UWTextRaster draws every glyph. The rasteriser is
/// TextCore's FontAsset, made at run time from the file (FontAsset.CreateFontAsset(path, face, size,
/// padding, mode, width, height)) or from an installed family (CreateFontAsset(family, style, size,
/// padding, mode) - the family as FontEngine.GetSystemFontNames lists it, "Microsoft JhengHei - Regular";
/// "Regular" is asked first, then the family's first style); its atlas is readable (a texture made at
/// run time), the glyph is read from it and the atlas is only a scratch pad - when it is full a fresh
/// one is made. The FontAsset makes itself a material with Unity's built-in
/// Hidden/Internal-GUITextureClipText (measured in the editor); nothing here draws with it. A file or a
/// family that is not there, or a FontAsset that cannot be made, is skipped (logged once); ASCII and
/// the German letters never come here, so English and German draw byte for byte as before.
///
/// THE MODERN INTERFACE (uGUI's Text with Lexend Exa, Liberation Sans and Unity's LegacyRuntime - the
/// IMGUI windows are gone since cleanup 8b, the only OnGUI left is the editor's level window): these
/// are Unity's DYNAMIC fonts, which draw a character they lack with another font: first the names in
/// Font.fontNames (the import's own name), then fonts of the system Unity finds itself (measured
/// 2026-10-26 on Windows 11: Lexend Exa drew an ideograph with Microsoft YaHei). The os: entries are put
/// after the font's own names (Follow, Apply), so the language's choice comes first; setting
/// fontNames at run time takes effect for the characters drawn after it (measured in the editor: a
/// character not drawn before came from the newly named font, one drawn before kept its earlier
/// font - Unity keeps a drawn character in the font's texture; a restart clears that). A FONT FILE
/// CANNOT BE A DYNAMIC FONT'S FALLBACK: Unity makes a dynamic font only from an imported asset or an
/// installed font (Font.CreateDynamicFontFromOSFont) - a file in Languages reaches the classic
/// interface only; the modern one takes what the system has. Fonts the port made itself (UWUiFonts'
/// original face) are not dynamic: a text with a character they lack keeps the modern font.
///
/// HOW UNITY FINDS AN INSTALLED FONT: by the family name in the system's font folders - on Windows the
/// fonts under C:\Windows\Fonts (Font.GetPathsToOSFonts listed 155 files there on the user's machine,
/// none from the per-user font folder %LOCALAPPDATA%\Microsoft\Windows\Fonts, which holds one), on Linux
/// the folders of the system's font configuration
/// (/usr/share/fonts, ~/.local/share/fonts; not tested here). Font.GetOSInstalledFontNames and
/// FontEngine.GetSystemFontNames list what it sees.
/// </summary>
public sealed class UWFallbackFont : IUWGlyphSource
{
    private const GlyphRenderMode RenderMode = GlyphRenderMode.RASTER_HINTED;

    /// <summary>The atlas of a file's font per size; a fresh one when it is full.</summary>
    private const int AtlasSize = 512;

    private const int AtlasPadding = 1;

    /// <summary>The spec in force (UWLanguageFont.Sources joined), "" for none.</summary>
    private static string msSpec = string.Empty;

    private static UWFallbackFont msCurrent;

    /// <summary>The port's dynamic fonts and their own fallback names, as they came (Follow).</summary>
    private static readonly Dictionary<Font, string[]> msFollowed = new Dictionary<Font, string[]>();

    /// <summary>The os: families of the language in force, in order.</summary>
    private static readonly List<string> msOsFamilies = new List<string>();

    private static bool mbQuitHooked;

    // ------------------------------------------------- Choosing

    /// <summary>
    /// The language's fonts in force (UWLanguage.Apply, after the language's texts): the classic fonts'
    /// fallback (UWFont.Fallback) and the modern fonts' fallback names. Nothing changes while the list
    /// stays the same; none (English, German) leaves everything as it was before this wave.
    /// </summary>
    public static void Apply()
    {
        List<UWLanguageFont.Source> lOSources = UWLanguageFont.Sources;
        string lsSpec = string.Join(", ", lOSources);

        if (lsSpec == msSpec)
            return;

        msSpec = lsSpec;

        if (msCurrent != null)
            msCurrent.fRelease();

        msCurrent = lOSources.Count > 0 ? new UWFallbackFont(lOSources) : null;
        UWFont.Fallback = msCurrent;

        msOsFamilies.Clear();

        foreach (UWLanguageFont.Source lOSource in lOSources)
        {
            if (lOSource.IsOs)
                msOsFamilies.Add(lOSource.Name);
        }

        foreach (KeyValuePair<Font, string[]> lOPair in msFollowed)
            fSetNames(lOPair.Key, lOPair.Value);

        if (lOSources.Count > 0)
            Debug.Log("UWFallbackFont: the language's fonts " + lsSpec + " (classic: in this order; modern: the installed ones "
                + (msOsFamilies.Count == 0 ? "- none named" : string.Join(", ", msOsFamilies)) + ")");
    }

    /// <summary>A dynamic font of the port's (UWInterfaceFont, the letter grid's): it gets the language's
    /// installed fonts behind its own names from now on. Its own names are kept and come back with a
    /// language without a font, and when the game ends (in the editor the fonts are assets).</summary>
    public static void Follow(Font pOFont)
    {
        if (pOFont == null || !pOFont.dynamic || msFollowed.ContainsKey(pOFont))
            return;

        string[] lsOwn = pOFont.fontNames ?? new string[0];

        msFollowed[pOFont] = lsOwn;

        if (msOsFamilies.Count > 0)
            fSetNames(pOFont, lsOwn);

        if (!mbQuitHooked)
        {
            Application.quitting += fRestoreNames;
            mbQuitHooked = true;
        }
    }

    private static void fSetNames(Font pOFont, string[] psOwn)
    {
        if (pOFont == null)
            return;

        if (msOsFamilies.Count == 0)
        {
            pOFont.fontNames = psOwn;

            return;
        }

        List<string> lsNames = new List<string>(psOwn);

        foreach (string lsFamily in msOsFamilies)
        {
            if (!lsNames.Contains(lsFamily))
                lsNames.Add(lsFamily);
        }

        pOFont.fontNames = lsNames.ToArray();
    }

    private static void fRestoreNames()
    {
        foreach (KeyValuePair<Font, string[]> lOPair in msFollowed)
        {
            if (lOPair.Key != null)
                lOPair.Key.fontNames = lOPair.Value;
        }
    }

    /// <summary>The font file a language names, in Languages beside the game.</summary>
    public static string FileOf(string psName)
    {
        return Path.Combine(UWLanguage.GameFolder, UWLanguage.FolderName, psName);
    }

    // ------------------------------------------------- The classic fonts' fallback

    /// <summary>One entry of the list: its rasterisers per size, made when first asked.</summary>
    private sealed class Entry
    {
        public UWLanguageFont.Source Source;

        /// <summary>False once it was not found (the file missing, the family not installed).</summary>
        public bool Usable = true;

        /// <summary>The capital height per size (FaceInfo.capLine / pointSize), once known.</summary>
        public float CapRatio = -1f;

        /// <summary>The style of an installed family that loads ("Regular", or its first).</summary>
        public string Style;

        public readonly Dictionary<int, FontAsset> Sizes = new Dictionary<int, FontAsset>();
    }

    private readonly List<Entry> mOEntries = new List<Entry>();

    private UWFallbackFont(List<UWLanguageFont.Source> pOSources)
    {
        foreach (UWLanguageFont.Source lOSource in pOSources)
            mOEntries.Add(new Entry { Source = lOSource });
    }

    public bool TryGetGlyph(UWFont pOFont, int piCode, out UWFont.Glyph pOGlyph)
    {
        pOGlyph = default;

        if (pOFont == null || piCode <= 0x7E || piCode > 0xFFFF)
            return false;

        foreach (Entry lOEntry in mOEntries)
        {
            if (!lOEntry.Usable)
                continue;

            float lfRatio = fCapRatio(lOEntry);

            if (!lOEntry.Usable)
                continue;

            if (UWFallbackGlyphs.Build(pOFont, lfRatio, piSize => fRaster(lOEntry, (char)piCode, piSize), out pOGlyph))
                return true;
        }

        return false;
    }

    /// <summary>The entry's capital height per size, asked of its rasteriser at the original's size of
    /// font5x6p's capitals - a plain number, the same at every size.</summary>
    private float fCapRatio(Entry pOEntry)
    {
        if (pOEntry.CapRatio > 0f)
            return pOEntry.CapRatio;

        FontAsset lOAsset = fAsset(pOEntry, 32);

        if (lOAsset == null)
            return UWFallbackGlyphs.DefaultCapRatio;

        FaceInfo lOFace = lOAsset.faceInfo;

        pOEntry.CapRatio = lOFace.pointSize > 0f && lOFace.capLine > 0f ? lOFace.capLine / lOFace.pointSize : UWFallbackGlyphs.DefaultCapRatio;

        return pOEntry.CapRatio;
    }

    /// <summary>The entry's rasteriser at a size, made when first asked; null (and the entry unusable)
    /// when its file or family is not there.</summary>
    private static FontAsset fAsset(Entry pOEntry, int piSize)
    {
        if (pOEntry.Sizes.TryGetValue(piSize, out FontAsset lOAsset) && lOAsset != null)
            return lOAsset;

        lOAsset = null;

        try
        {
            if (pOEntry.Source.IsOs)
            {
                if (pOEntry.Style == null)
                    pOEntry.Style = fStyleOf(pOEntry.Source.Name);

                if (pOEntry.Style != null)
                    lOAsset = FontAsset.CreateFontAsset(pOEntry.Source.Name, pOEntry.Style, piSize, AtlasPadding, RenderMode);
            }
            else
            {
                string lsPath = FileOf(pOEntry.Source.Name);

                if (File.Exists(lsPath))
                    lOAsset = FontAsset.CreateFontAsset(lsPath, 0, piSize, AtlasPadding, RenderMode, AtlasSize, AtlasSize);
            }
        }
        catch (Exception lOError)
        {
            Debug.LogWarning("UWFallbackFont: " + pOEntry.Source + " could not be loaded: " + lOError.Message);
            lOAsset = null;
        }

        if (lOAsset == null)
        {
            if (pOEntry.Usable)
                Debug.LogWarning("UWFallbackFont: " + pOEntry.Source + (pOEntry.Source.IsOs ? " is not installed" : " is not in " + FileOf(string.Empty))
                    + " - left out");

            pOEntry.Usable = false;

            return null;
        }

        lOAsset.name = "UWFallbackFont " + pOEntry.Source + " " + piSize;
        pOEntry.Sizes[piSize] = lOAsset;

        return lOAsset;
    }

    /// <summary>The style an installed family is loaded with: "Regular" where the system lists it, else
    /// the family's first; null when the system lists no such family.</summary>
    private static string fStyleOf(string psFamily)
    {
        string[] lsFonts = FontEngine.GetSystemFontNames();
        string lsFirst = null;

        if (lsFonts == null)
            return null;

        foreach (string lsFont in lsFonts)
        {
            // "Microsoft JhengHei - Regular"
            int liDash = lsFont.LastIndexOf(" - ", StringComparison.Ordinal);

            if (liDash <= 0 || !string.Equals(lsFont.Substring(0, liDash), psFamily, StringComparison.OrdinalIgnoreCase))
                continue;

            string lsStyle = lsFont.Substring(liDash + 3);

            if (string.Equals(lsStyle, "Regular", StringComparison.OrdinalIgnoreCase))
                return lsStyle;

            lsFirst = lsFirst ?? lsStyle;
        }

        return lsFirst;
    }

    /// <summary>A character of the entry at a size, monochrome, rows top-down (UWRasterGlyph); not Found
    /// when the font has no such character.
    ///
    /// FontAsset.characterLookupTable is marked obsolete in Unity 6.6 ("the Advanced Text Generator no
    /// longer requires character data") but is the only public way to a glyph's rectangle in the atlas:
    /// FontEngine's own rendering is internal, TextMeshPro's font asset wants its shaders from the TMP
    /// Essential Resources (not in the project), and the legacy dynamic Font only renders anti-aliased
    /// (thresholded at 7 pixels an ideograph broke apart - measured 2026-10-26). Should the table go, the
    /// classic fallback goes with it and this is where to look; nothing else depends on it.</summary>
#pragma warning disable CS0618
    private static UWRasterGlyph fRaster(Entry pOEntry, char pcChar, int piSize)
    {
        for (int liTry = 0; liTry < 2; liTry++)
        {
            FontAsset lOAsset = fAsset(pOEntry, piSize);

            if (lOAsset == null)
                return default;

            bool lbHad = lOAsset.characterLookupTable.ContainsKey(pcChar);

            if (!lbHad)
                lOAsset.TryAddCharacters(pcChar.ToString(), out string _, false);

            if (lOAsset.characterLookupTable.TryGetValue(pcChar, out Character lOCharacter) && lOCharacter != null && lOCharacter.glyph != null)
                return fRead(lOAsset, lOCharacter.glyph);

            // Not added: the font lacks it - or the atlas is full. A fresh atlas tells which.
            if (liTry > 0 || lOAsset.characterLookupTable.Count == 0)
                break;

            pOEntry.Sizes.Remove(piSize);
            fDestroy(lOAsset);
        }

        return default;
    }
#pragma warning restore CS0618

    /// <summary>A glyph's ink out of its atlas: TextCore's rectangle (rows bottom-up) and metrics.</summary>
    private static UWRasterGlyph fRead(FontAsset pOAsset, Glyph pOGlyph)
    {
        GlyphRect lORect = pOGlyph.glyphRect;
        GlyphMetrics lOMetrics = pOGlyph.metrics;
        UWRasterGlyph lOGlyph = new UWRasterGlyph
        {
            Found = true,
            BearingX = Mathf.RoundToInt(lOMetrics.horizontalBearingX),
            BearingY = Mathf.RoundToInt(lOMetrics.horizontalBearingY),
            Advance = Mathf.RoundToInt(lOMetrics.horizontalAdvance)
        };

        Texture2D lOAtlas = pOAsset.atlasTexture;

        if (lORect.width <= 0 || lORect.height <= 0 || lOAtlas == null || !lOAtlas.isReadable)
            return lOGlyph;

        Color[] lOColours = lOAtlas.GetPixels(lORect.x, lORect.y, lORect.width, lORect.height);
        byte[] lyPixels = new byte[lORect.width * lORect.height];

        for (int y = 0; y < lORect.height; y++)
        {
            int liSource = (lORect.height - 1 - y) * lORect.width;

            for (int x = 0; x < lORect.width; x++)
                lyPixels[(y * lORect.width) + x] = (byte)(lOColours[liSource + x].a >= 0.5f ? 1 : 0);
        }

        lOGlyph.Width = lORect.width;
        lOGlyph.Height = lORect.height;
        lOGlyph.Pixels = lyPixels;

        return lOGlyph;
    }

    private void fRelease()
    {
        foreach (Entry lOEntry in mOEntries)
        {
            foreach (FontAsset lOAsset in lOEntry.Sizes.Values)
                fDestroy(lOAsset);

            lOEntry.Sizes.Clear();
        }
    }

    private static void fDestroy(FontAsset pOAsset)
    {
        if (pOAsset == null)
            return;

        foreach (Texture2D lOAtlas in pOAsset.atlasTextures ?? new Texture2D[0])
        {
            if (lOAtlas != null)
                fDestroyObject(lOAtlas);
        }

        fDestroyObject(pOAsset);
    }

    private static void fDestroyObject(Object pOObject)
    {
        if (Application.isPlaying)
            Object.Destroy(pOObject);
        else
            Object.DestroyImmediate(pOObject);
    }

    // ------------------------------------------------- For the editor check

    /// <summary>A character as the first entry that has it rasterises it at a size: its bitmap's size,
    /// bearings, advance and ink, the rows drawn - for UWFontFallbackCheck.</summary>
    public static string DescribeRaster(char pcChar, int piSize)
    {
        if (msCurrent == null)
            return "no language font";

        foreach (Entry lOEntry in msCurrent.mOEntries)
        {
            if (!lOEntry.Usable)
                continue;

            UWRasterGlyph lOGlyph = fRaster(lOEntry, pcChar, piSize);

            if (!lOGlyph.Found)
                continue;

            StringBuilder lOText = new StringBuilder();
            int liInk = 0;

            lOText.Append(lOEntry.Source).Append(" at ").Append(piSize).Append(": ").Append(lOGlyph.Width).Append('x').Append(lOGlyph.Height)
                .Append(", bearing ").Append(lOGlyph.BearingX).Append('/').Append(lOGlyph.BearingY).Append(", advance ").Append(lOGlyph.Advance);

            for (int y = 0; y < lOGlyph.Height && lOGlyph.Pixels != null; y++)
            {
                lOText.Append('\n');

                for (int x = 0; x < lOGlyph.Width; x++)
                {
                    bool lbInk = lOGlyph.Pixels[(y * lOGlyph.Width) + x] != 0;

                    liInk += lbInk ? 1 : 0;
                    lOText.Append(lbInk ? '#' : '.');
                }
            }

            return lOText.Insert(0, liInk + " ink, ").ToString();
        }

        return "in none of the language's fonts";
    }

    /// <summary>The entries of the language in force that load, in order ("os:Microsoft JhengHei"), and
    /// those that do not - for UWFontFallbackCheck and the log.</summary>
    public static string Describe()
    {
        if (msCurrent == null)
            return "none";

        List<string> lsParts = new List<string>();

        foreach (Entry lOEntry in msCurrent.mOEntries)
        {
            msCurrent.fCapRatio(lOEntry);
            lsParts.Add(lOEntry.Source + (lOEntry.Usable ? string.Empty : " (not found)"));
        }

        return string.Join(", ", lsParts);
    }
}
