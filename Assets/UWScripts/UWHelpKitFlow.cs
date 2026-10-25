using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// THE HELP'S LISTS ON THE KIT (cleanup 8b.3, 2026-10-24; phase 8b): IUWHelpFlow laid out top to bottom
/// with UWKitRows' help pieces, as GUILayout laid out the IMGUI form - the same sizes (UWGuiTheme's help
/// measures), the same room (Space), the headings' margins taken together with their neighbours' as
/// GUILayout does (the larger of the two, room given by Space not counted), each line as high as its
/// text wrapped at the list's width. Pixel differences between IMGUI's and uGUI's text are accepted
/// (phase 8b's rule).
///
/// SIZED BY TEXT (decided per user 2026-10-24): a stat row whose label and value do not fit side by side
/// puts its value on a line of its own below, and a fold-out's text wraps inside its button, so a longer
/// language (-language pseudo) or a narrow help does not run texts into each other.
/// </summary>
public sealed class UWHelpKitFlow : IUWHelpFlow
{
    private readonly UWKitRows mOKit;

    private float mfLeft;

    private float mfWidth;

    private float mfY;

    private float mfBottom;

    /// <summary>The bottom margin of the last entry that counts for margins (GUILayout collapses the
    /// margins between neighbours to the larger one).</summary>
    private int miLastMargin;

    private bool mbFirst;

    public UWHelpKitFlow(UWKitRows pOKit)
    {
        mOKit = pOKit;
    }

    /// <summary>The kit's pieces, for a tab's own drawing (the map, the manual, the notes).</summary>
    public UWKitRows Kit => mOKit;

    /// <summary>Where the next entry goes.</summary>
    public float Y => mfY;

    public float Left => mfLeft;

    public float Width => mfWidth;

    /// <summary>Starts a list at the top-left corner, pfWidth wide; pfBottom is where the room ends
    /// (for Rest).</summary>
    public void Begin(float pfLeft, float pfTop, float pfWidth, float pfBottom)
    {
        mfLeft = pfLeft;
        mfY = pfTop;
        mfWidth = pfWidth;
        mfBottom = pfBottom;
        miLastMargin = 0;
        mbFirst = true;
    }

    // ------------------------------------------------- The looks

    /// <summary>UWGuiTheme.Help's looks as the kit's: Text (15, wrapped, the tags read), DimText (13),
    /// Heading (20, bold, accent) and SubHeading (16, bold, accent).</summary>
    public static UWKitRows.HelpLook Look(UWHelpLookEnum peLook)
    {
        switch (peLook)
        {
            case UWHelpLookEnum.Dim:
                return fLook(UWGuiTheme.HelpDimSize, UWGuiTheme.DimText, false);

            case UWHelpLookEnum.Heading:
                return fLook(UWGuiTheme.HelpHeadingSize, UWGuiTheme.Accent, true);

            case UWHelpLookEnum.SubHeading:
                return fLook(UWGuiTheme.HelpSubHeadingSize, UWGuiTheme.Accent, true);

            default:
                return fLook(UWGuiTheme.HelpTextSize, UWGuiTheme.Text, false);
        }
    }

    private static UWKitRows.HelpLook fLook(int piSize, Color pOColour, bool pbBold)
    {
        return new UWKitRows.HelpLook { Size = piSize, Colour = pOColour, Anchor = TextAnchor.UpperLeft, Wraps = true, Bold = pbBold, Rich = true };
    }

    /// <summary>A look on one line (the stat rows' Label and Value, Centred).</summary>
    public static UWKitRows.HelpLook OneLine(UWHelpLookEnum peLook, TextAnchor peAnchor)
    {
        UWKitRows.HelpLook lOLook = Look(peLook);

        lOLook.Wraps = false;
        lOLook.Anchor = peAnchor;

        return lOLook;
    }

    /// <summary>The fold-outs' and the tabs' text (Tab, Foldout): one line, centred.</summary>
    public static UWKitRows.HelpLook ButtonLook(int piSize)
    {
        return new UWKitRows.HelpLook { Size = piSize, Colour = UWGuiTheme.Text, Anchor = TextAnchor.MiddleCenter, Rich = true };
    }

    // ------------------------------------------------- Placing

    /// <summary>The next entry's top, by GUILayout's rule: the larger of the last entry's bottom margin
    /// and this one's top margin between them (none above the first); then the entry's height.</summary>
    private float fPlace(int piMarginTop, int piMarginBottom, float pfHeight)
    {
        if (mbFirst)
            mbFirst = false;
        else
            mfY += Mathf.Max(miLastMargin, piMarginTop);

        float lfTop = mfY;

        mfY += pfHeight;
        miLastMargin = piMarginBottom;

        return lfTop;
    }

    /// <summary>A strip of the list pfHeight high, for a tab's own pieces (the manual's arrows).</summary>
    public Rect Take(float pfHeight)
    {
        return new Rect(mfLeft, fPlace(0, 0, pfHeight), mfWidth, pfHeight);
    }

    /// <summary>The room left below the list (GUILayoutUtility.GetRect expanding: the map, the page,
    /// the notes).</summary>
    public Rect Rest()
    {
        float lfTop = fPlace(0, 0, 0f);

        return new Rect(mfLeft, lfTop, mfWidth, Mathf.Max(0f, mfBottom - lfTop));
    }

    // ------------------------------------------------- IUWHelpFlow

    public void Line(string psText, UWHelpLookEnum peLook)
    {
        UWKitRows.HelpLook lOLook = Look(peLook);
        float lfHeight = mOKit.HelpTextHeight(psText, lOLook, mfWidth);
        bool lbSub = peLook == UWHelpLookEnum.SubHeading;
        float lfTop = fPlace(lbSub ? UWGuiTheme.HelpSubHeadingMarginTop : 0, lbSub ? UWGuiTheme.HelpSubHeadingMarginBottom : 0, lfHeight);

        mOKit.HelpText(new Rect(mfLeft, lfTop, mfWidth, lfHeight), psText, lOLook);
    }

    /// <summary>GUILayout.Space: room that does not count for the margins.</summary>
    public void Space(float pfUnits)
    {
        mfY += pfUnits;
    }

    public void StatRow(string psLabel, string psValue)
    {
        UWKitRows.HelpLook lOLabel = OneLine(UWHelpLookEnum.Text, TextAnchor.UpperLeft);
        UWKitRows.HelpLook lOValue = OneLine(UWHelpLookEnum.Text, TextAnchor.UpperRight);
        string lsValue = UWHelpFlowText.Accent(psValue);
        float lfLine = mOKit.HelpTextHeight(psLabel, lOLabel, mfWidth);
        bool lbBeside = mOKit.HelpTextWidth(psLabel, lOLabel) + UWDataImport.UWData.UWRowSizing.Margin
            + mOKit.HelpTextWidth(lsValue, lOValue) <= mfWidth;
        float lfTop = fPlace(0, 0, lbBeside ? lfLine : 2f * lfLine);

        mOKit.HelpText(new Rect(mfLeft, lfTop, mfWidth, lfLine), psLabel, lOLabel);
        mOKit.HelpText(new Rect(mfLeft, lbBeside ? lfTop : lfTop + lfLine, mfWidth, lfLine), lsValue, lOValue);
    }

    public bool Fold(string psText)
    {
        UWKitRows.HelpLook lOLook = ButtonLook(UWGuiTheme.HelpFoldoutSize);
        float lfInner = Mathf.Max(1f, mfWidth - (2f * UWGuiTheme.HelpFoldoutPaddingX));

        lOLook.Wraps = mOKit.HelpTextWidth(psText, lOLook) > lfInner;

        float lfHeight = mOKit.HelpTextHeight(psText, lOLook, lfInner) + (2f * UWGuiTheme.HelpFoldoutPaddingY);
        Rect lORect = new Rect(mfLeft, fPlace(0, 0, lfHeight), mfWidth, lfHeight);

        return mOKit.HelpButton(lORect, psText, lOLook, UWGuiTheme.Panel, UWGuiTheme.Hover, UWGuiTheme.Text);
    }

    /// <summary>The rune pictures 22 units square, 3 apart (the Icon style's margin), then 8 and the
    /// margin before the text - GUILayout's horizontal row; the row as high as the taller of the two.</summary>
    public bool RuneRow(IReadOnlyList<Texture2D> pOIcons, string psText, bool pbClickable)
    {
        const float Size = UWHelpFlowText.RuneIconSize;
        float lfPitch = Size + UWGuiTheme.HelpIconMarginRight;
        float lfTextLeft = (pOIcons.Count * lfPitch) + UWHelpFlowText.RuneTextGap;
        float lfTextWidth = Mathf.Max(1f, mfWidth - lfTextLeft);
        UWKitRows.HelpLook lOLook = Look(UWHelpLookEnum.Text);
        float lfTextHeight = mOKit.HelpTextHeight(psText, lOLook, lfTextWidth);
        float lfHeight = Mathf.Max(Size, lfTextHeight);
        float lfTop = fPlace(0, 0, lfHeight);

        for (int liAt = 0; liAt < pOIcons.Count; liAt++)
        {
            Texture2D lOIcon = pOIcons[liAt];

            if (lOIcon != null)
                mOKit.Picture(fFit(new Rect(mfLeft + (liAt * lfPitch), lfTop, Size, Size), lOIcon), lOIcon);
        }

        mOKit.HelpText(new Rect(mfLeft + lfTextLeft, lfTop, lfTextWidth, lfTextHeight), psText, lOLook);

        return pbClickable && mOKit.PressedIn(new Rect(mfLeft, lfTop, mfWidth, lfHeight));
    }

    public void KeyRow(string psKeys, string psText)
    {
        const float KeyWidth = UWHelpFlowText.ControlsKeyWidth;
        UWKitRows.HelpLook lOLook = Look(UWHelpLookEnum.Text);
        float lfTextWidth = Mathf.Max(1f, mfWidth - KeyWidth);
        float lfKeysHeight = mOKit.HelpTextHeight(psKeys, lOLook, KeyWidth);
        float lfTextHeight = mOKit.HelpTextHeight(psText, lOLook, lfTextWidth);
        float lfTop = fPlace(0, 0, Mathf.Max(lfKeysHeight, lfTextHeight));

        mOKit.HelpText(new Rect(mfLeft, lfTop, KeyWidth, lfKeysHeight), psKeys, lOLook);
        mOKit.HelpText(new Rect(mfLeft + KeyWidth, lfTop, lfTextWidth, lfTextHeight), psText, lOLook);
    }

    /// <summary>The glyphs from the column's left, centred in the row's height; the text beside them
    /// centred on them (TextMiddle, at least a glyph high).</summary>
    public void GlyphRow(IReadOnlyList<Texture2D> pOGlyphs, string psText)
    {
        const float KeyWidth = UWHelpFlowText.ControlsKeyWidth;
        const float Glyph = UWHelpFlowText.ControlsGlyphSize;
        UWKitRows.HelpLook lOLook = Look(UWHelpLookEnum.Text);
        float lfTextWidth = Mathf.Max(1f, mfWidth - KeyWidth);
        float lfHeight = Mathf.Max(Glyph, mOKit.HelpTextHeight(psText, lOLook, lfTextWidth));
        float lfTop = fPlace(0, 0, lfHeight);

        for (int liAt = 0; liAt < pOGlyphs.Count; liAt++)
        {
            if (pOGlyphs[liAt] != null)
                mOKit.Picture(new Rect(mfLeft + (liAt * Glyph), lfTop + ((lfHeight - Glyph) / 2f), Glyph, Glyph), pOGlyphs[liAt]);
        }

        lOLook.Anchor = TextAnchor.MiddleLeft;
        mOKit.HelpText(new Rect(mfLeft + KeyWidth, lfTop, lfTextWidth, lfHeight), psText, lOLook);
    }

    /// <summary>A picture inside a square as large as it fits keeping its sides (ScaleMode.ScaleToFit).</summary>
    private static Rect fFit(Rect pORect, Texture pOTexture)
    {
        if (pOTexture.width <= 0 || pOTexture.height <= 0)
            return pORect;

        float lfScale = Mathf.Min(pORect.width / pOTexture.width, pORect.height / pOTexture.height);
        float lfWidth = pOTexture.width * lfScale;
        float lfHeight = pOTexture.height * lfScale;

        return new Rect(pORect.x + ((pORect.width - lfWidth) / 2f), pORect.y + ((pORect.height - lfHeight) / 2f), lfWidth, lfHeight);
    }
}
