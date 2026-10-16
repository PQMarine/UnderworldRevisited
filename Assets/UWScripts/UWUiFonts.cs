using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN INTERFACE'S FONT, chosen in one place (per user, 2026-10-09: "the original font as
/// an option, and plan other fonts for the translations"). The texts of the modern scheme's parts
/// register here when they are made (Register); just before the canvases are drawn each gets the
/// font chosen (UWUserSettings.InterfaceFont):
///   - MODERN: the font its part gave it (Lexend Exa), untouched.
///   - ORIGINAL: the game's FONT5X6P, built as a Unity font from the player's own FONT*.SYS, its
///     capitals (5 rows) as high as the modern font's at the text's fontSize (about 0.7 of it) -
///     at the size of the original's other pictures it was far too big for the parts (per user's
///     screenshots, 2026-10-09) -, 1/1.2 as wide like them (UWModernHudArt.PixelAspectX). A text
///     with a character the original lacks keeps the modern font (the original's fonts are ASCII).
/// The menu bar, the layout editor and the help window are the port's tools and keep the modern
/// font (per user). Later the translations add a language's own font here (Noto Sans TC, say),
/// chosen the same way - the parts ask nothing but this class.
///
/// A font built at run time has no line height (Unity keeps it in the asset only), so each is a
/// copy of an empty template with the right one (Resources/UWFontTemplates, Editor/UWFontTemplates),
/// filled with the glyphs; one per size, cached.
/// </summary>
public static class UWUiFonts
{
    public enum FaceEnum
    {
        Modern = 0,
        Original = 1
    }

    public static FaceEnum Face => UWUserSettings.InterfaceFont == 1 ? FaceEnum.Original : FaceEnum.Modern;

    /// <summary>How many of the template's line heights exist (Editor/UWFontTemplates).</summary>
    private const int MaxLineHeight = 96;

    private sealed class Entry
    {
        public Text Text;

        /// <summary>The font the part gave the text - what it shows in the modern face.</summary>
        public Font Modern;

        /// <summary>A lone sign in a disc (UWCentredGlyph): its size rounded down, so it keeps clear
        /// of the ring (per user's screenshot, 2026-10-09: the bags' slot numbers touched it).</summary>
        public bool Centred;

        /// <summary>Puts the text on the modern font's line whatever its font (UWFontBaseline).</summary>
        public UWFontBaseline Baseline;
    }

    private static readonly List<Entry> msTexts = new List<Entry>();

    /// <summary>Typographic characters the original lacks, drawn with its ASCII ones (per user,
    /// 2026-10-09: the minimap's "−" stayed modern).</summary>
    private static readonly Dictionary<char, char> msAliases = new Dictionary<char, char>
    {
        { '−', '-' }, { '–', '-' }, { '—', '-' }, { '‘', '\'' }, { '’', '\'' }, { '“', '"' },
        { '”', '"' }, { '×', 'x' }
    };

    private static readonly Dictionary<int, Font> msOriginalFonts = new Dictionary<int, Font>();

    private static readonly HashSet<Font> msOwnFonts = new HashSet<Font>();

    private static UWGameUI msUi;

    private static UWFont msGlyphs;

    private static Texture2D msAtlas;

    private static int miAtlasColumns;

    private static int miCellWidth;

    private static int miCellHeight;

    private static bool mbHooked;

    /// <summary>A text of the modern interface: it follows the font chosen from now on.</summary>
    public static void Register(Text pOText, UWGameUI pOUi)
    {
        if (pOText == null)
            return;

        if (pOUi != null)
            msUi = pOUi;

        if (!mbHooked)
        {
            Canvas.willRenderCanvases += fApply;
            mbHooked = true;
        }

        msTexts.Add(new Entry { Text = pOText, Modern = pOText.font, Baseline = pOText.gameObject.AddComponent<UWFontBaseline>() });
    }

    /// <summary>
    /// Gives a registered text at once the font it gets at its current fontSize and text, for a
    /// part that MEASURES it in the same frame (preferredHeight). Else it measures with last
    /// frame's font: the original font's sizes are steps and ignore fontSize, so a part fitting
    /// its text by making it smaller saw no change, shrank to the end, got the small font, then
    /// fit at full size, got the large one again - every frame (per user, 2026-10-10: "the
    /// original font flickers like mad when made larger", the conversation's answers).
    /// </summary>
    public static void ApplyNow(Text pOText)
    {
        if (pOText == null)
            return;

        foreach (Entry lOEntry in msTexts)
        {
            if (lOEntry.Text != pOText)
                continue;

            if (pOText.font != null && !msOwnFonts.Contains(pOText.font))
                lOEntry.Modern = pOText.font;

            bool lbOriginal = Face == FaceEnum.Original && fEnsureGlyphs();
            Font lOWanted = lbOriginal && fCovers(pOText.text, pOText.supportRichText)
                ? fOriginalFont(pOText.fontSize, lOEntry.Centred) : lOEntry.Modern;

            if (lOWanted != null && pOText.font != lOWanted)
                pOText.font = lOWanted;

            return;
        }
    }

    private static void fApply()
    {
        bool lbOriginal = Face == FaceEnum.Original && fEnsureGlyphs();

        for (int liAt = msTexts.Count - 1; liAt >= 0; liAt--)
        {
            Entry lOEntry = msTexts[liAt];
            Text lOText = lOEntry.Text;

            if (lOText == null)
            {
                msTexts.RemoveAt(liAt);
                continue;
            }

            lOEntry.Centred = lOText.GetComponent<UWCentredGlyph>() != null;

            if (lOEntry.Baseline != null)
            {
                bool lbOn = !lOEntry.Centred;

                if (lOEntry.Baseline.Reference != lOEntry.Modern || lOEntry.Baseline.enabled != lbOn)
                {
                    lOEntry.Baseline.Reference = lOEntry.Modern;
                    lOEntry.Baseline.enabled = lbOn;
                    lOText.SetVerticesDirty();
                }
            }

            // A part may change its text's font itself; that is the modern one from then on.
            if (lOText.font != null && !msOwnFonts.Contains(lOText.font))
                lOEntry.Modern = lOText.font;

            Font lOWanted = lbOriginal && fCovers(lOText.text, lOText.supportRichText)
                ? fOriginalFont(lOText.fontSize, lOEntry.Centred) : lOEntry.Modern;

            if (lOWanted != null && lOText.font != lOWanted)
                lOText.font = lOWanted;
        }
    }

    /// <summary>Whether the original font has every character of the text - the rich text's tags
    /// (the message colours' &lt;color&gt;) not counted, they are not drawn.</summary>
    private static bool fCovers(string psText, bool pbRichText)
    {
        if (string.IsNullOrEmpty(psText))
            return true;

        bool lbInTag = false;

        foreach (char lcChar in psText)
        {
            if (pbRichText && lcChar == '<')
                lbInTag = true;

            if (lbInTag)
            {
                if (lcChar == '>')
                    lbInTag = false;

                continue;
            }

            if (lcChar == ' ' || lcChar == '\n' || lcChar == '\r')
                continue;

            char lcDrawn = msAliases.TryGetValue(lcChar, out char lcAlias) ? lcAlias : lcChar;

            if (!msGlyphs.TryGetGlyph(lcDrawn, out UWFont.Glyph lOGlyph) || lOGlyph.Width <= 0)
                return false;
        }

        return true;
    }

    /// <summary>The game's FONT5X6P and its glyphs in one texture, once the data is loaded.</summary>
    private static bool fEnsureGlyphs()
    {
        if (msAtlas != null)
            return true;

        if (msUi == null || msUi.mOUWData == null || msUi.mOUWData.Fonts == null)
            return false;

        UWFont lOFont = msUi.mOUWData.Fonts.Get(UWFonts.FontType.Normal);

        if (lOFont == null || !lOFont.IsLoaded || lOFont.GlyphCount == 0)
            return false;

        int liMaxWidth = 1;

        for (int liCode = 0; liCode < lOFont.GlyphCount; liCode++)
        {
            if (lOFont.TryGetGlyph(liCode, out UWFont.Glyph lOGlyph))
                liMaxWidth = Mathf.Max(liMaxWidth, lOGlyph.Width);
        }

        miCellWidth = liMaxWidth + 1;
        miCellHeight = lOFont.Height + 1;
        miAtlasColumns = 16;

        int liRows = (lOFont.GlyphCount + miAtlasColumns - 1) / miAtlasColumns;
        Color32[] lyPixels = new Color32[miAtlasColumns * miCellWidth * liRows * miCellHeight];
        int liAtlasWidth = miAtlasColumns * miCellWidth;
        int liAtlasHeight = liRows * miCellHeight;

        for (int liCode = 0; liCode < lOFont.GlyphCount; liCode++)
        {
            if (!lOFont.TryGetGlyph(liCode, out UWFont.Glyph lOGlyph))
                continue;

            int liCellX = (liCode % miAtlasColumns) * miCellWidth;
            int liCellY = (liCode / miAtlasColumns) * miCellHeight;

            for (int y = 0; y < lOFont.Height; y++)
            {
                for (int x = 0; x < lOGlyph.Width; x++)
                {
                    if (lOGlyph.Pixels[(y * lOGlyph.Width) + x] == 0)
                        continue;

                    // The texture's rows run bottom-up, the glyph's top-down.
                    int liTextureY = liCellY + (lOFont.Height - 1 - y);

                    lyPixels[(liTextureY * liAtlasWidth) + liCellX + x] = new Color32(255, 255, 255, 255);
                }
            }
        }

        msAtlas = new Texture2D(liAtlasWidth, liAtlasHeight, TextureFormat.RGBA32, false);
        msAtlas.name = "UWUiFonts original atlas";
        msAtlas.filterMode = FilterMode.Point;
        msAtlas.wrapMode = TextureWrapMode.Clamp;
        msAtlas.SetPixels32(lyPixels);
        msAtlas.Apply(false, false);
        msGlyphs = lOFont;

        return true;
    }

    /// <summary>The original font for a text of this fontSize: its five capital rows as high as a
    /// modern capital (0.7 of the fontSize), in whole screen pixels per font pixel - rounded up from
    /// a quarter, so the small texts (counts, weights, the bar's numbers) get two pixels, not one
    /// (per user's screenshot, 2026-10-09: tiny), and two at least: at one the character panel's
    /// labels were hard to read, and at two the original is still narrower than Lexend Exa.</summary>
    private static Font fOriginalFont(int piFontSize, bool pbCentred = false)
    {
        float lfScale = piFontSize * 0.7f / 5f;
        int liScale = Mathf.Max(2, pbCentred ? Mathf.FloorToInt(lfScale) : Mathf.RoundToInt(lfScale + 0.25f));

        if (msOriginalFonts.TryGetValue(liScale, out Font lOFont) && lOFont != null)
            return lOFont;

        int liLineHeight = Mathf.Clamp((msGlyphs.Height + 1) * liScale, 1, MaxLineHeight);
        Font lOTemplate = Resources.Load<Font>("UWFontTemplates/LineHeight" + liLineHeight);

        if (lOTemplate == null)
            return null;

        lOFont = Object.Instantiate(lOTemplate);
        lOFont.name = "UWUiFonts original x" + liScale;

        Material lOMaterial = new Material(Graphic.defaultGraphicMaterial);

        lOMaterial.mainTexture = msAtlas;
        lOFont.material = lOMaterial;

        float lfAcross = liScale * UWModernHudArt.PixelAspectX;
        List<CharacterInfo> lOInfos = new List<CharacterInfo>();

        for (int liCode = 0; liCode < msGlyphs.GlyphCount; liCode++)
        {
            if (liCode == ' ')
            {
                lOInfos.Add(new CharacterInfo { index = ' ', advance = Mathf.Max(1, Mathf.RoundToInt(msGlyphs.SpaceWidth * lfAcross)) });
                continue;
            }

            if (!msGlyphs.TryGetGlyph(liCode, out UWFont.Glyph lOGlyph) || lOGlyph.Width <= 0)
                continue;

            int liWidth = Mathf.Max(1, Mathf.RoundToInt(lOGlyph.Width * lfAcross));
            // Above the baseline, the line's top at the ascent (the template's line height), the
            // capitals (all rows but the last, the descenders' row) in the line's middle - with
            // the whole glyph centred a "+" sat high (per user, 2026-10-09).
            int liTop = liLineHeight - ((liLineHeight - ((msGlyphs.Height - 1) * liScale)) / 2);

            // The quad on the glyph's lit pixels only, not its whole cell with the spacing column
            // and the empty rows (the advance keeps the cell): a sign centred by its ink
            // (UWCentredGlyph) then sits in the middle exactly.
            int liLeft = int.MaxValue, liRight = -1, liRowTop = int.MaxValue, liRowBottom = -1;

            for (int y = 0; y < msGlyphs.Height; y++)
            {
                for (int x = 0; x < lOGlyph.Width; x++)
                {
                    if (lOGlyph.Pixels[(y * lOGlyph.Width) + x] == 0)
                        continue;

                    liLeft = Mathf.Min(liLeft, x);
                    liRight = Mathf.Max(liRight, x);
                    liRowTop = Mathf.Min(liRowTop, y);
                    liRowBottom = Mathf.Max(liRowBottom, y);
                }
            }

            if (liRight < 0)
            {
                lOInfos.Add(new CharacterInfo { index = liCode, advance = liWidth });
                continue;
            }

            int liCellX = (liCode % miAtlasColumns) * miCellWidth;
            int liCellY = (liCode / miAtlasColumns) * miCellHeight;
            // The atlas's rows run bottom-up: glyph row y is texture row cell + Height - 1 - y.
            float lfU0 = (liCellX + liLeft) / (float)msAtlas.width;
            float lfU1 = (liCellX + liRight + 1) / (float)msAtlas.width;
            float lfV0 = (liCellY + msGlyphs.Height - 1 - liRowBottom) / (float)msAtlas.height;
            float lfV1 = (liCellY + msGlyphs.Height - liRowTop) / (float)msAtlas.height;

            lOInfos.Add(new CharacterInfo
            {
                index = liCode,
                uvBottomLeft = new Vector2(lfU0, lfV0),
                uvBottomRight = new Vector2(lfU1, lfV0),
                uvTopLeft = new Vector2(lfU0, lfV1),
                uvTopRight = new Vector2(lfU1, lfV1),
                minX = Mathf.RoundToInt(liLeft * lfAcross),
                maxX = Mathf.RoundToInt((liRight + 1) * lfAcross),
                minY = liTop - ((liRowBottom + 1) * liScale),
                maxY = liTop - (liRowTop * liScale),
                advance = liWidth
            });
        }

        // The typographic characters as their ASCII ones.
        foreach (KeyValuePair<char, char> lOAlias in msAliases)
        {
            CharacterInfo lOSame = lOInfos.Find(lOInfo => lOInfo.index == lOAlias.Value);

            if (lOSame.advance > 0)
            {
                lOSame.index = lOAlias.Key;
                lOInfos.Add(lOSame);
            }
        }

        lOFont.characterInfo = lOInfos.ToArray();
        msOriginalFonts[liScale] = lOFont;
        msOwnFonts.Add(lOFont);

        return lOFont;
    }
}
