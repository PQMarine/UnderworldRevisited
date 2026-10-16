using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The HUD section "the message log in the lower frame box".
/// Its own class since 2026-09-18 (stage two of the HUD rebuild); the owner hands in the game
/// data, the frame, the character, the interaction and the inventory through mOUi. UWGameUI
/// keeps the public entry points as forwards.
/// </summary>
public sealed class UWHudMessageLog
{
    private readonly UWGameUI mOUi;

    internal UWHudMessageLog(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    // Message log in the lower frame box (measured by flood fill on the dark area in
    // Assets/UWExportedTextures/Main_0.png: X 16-304, Y 169-198) - shows
    // Interaction.ActiveStrings (the same list AddMessage fills), one line per
    // message, newest at the bottom. Original: only the last 5, see Interaction.Update.
    // Fine-tuned per user test in the prefab.
    private const float messageLogLeft = 15f;
    internal const float messageLogTop = 169f;
    private const float messageLogWidth = 291f;
    private const float messageLogHeight = 30f;

    /// <summary>Font colour of the log, as previously taken from the original.</summary>
    private static readonly Color mOLogTextColour = new Color(61f / 255f, 28f / 255f, 8f / 255f, 1f);
    private Text mMessageLogText;

    /// <summary>The log in the original font. The text next to it (mMessageLogText)
    /// is the old Unity font and is only switched off now.</summary>
    private RawImage mMessageLogImage;

    private Texture2D mMessageLogTexture;

    // Decorated edges left/right of the message log (SCRLEDGE.GR, placed per user
    // in the former UI prefab, loaded in Init). Centred
    // pivot, since they flank the log box vertically centred instead of aligning to it.
    // The positions hold up against the original: seg043_37F0_11E draws at x 11 and x 306 on
    // EXE y 30, and since that y is the top counted from the bottom of the 200 line screen
    // (199 - 30), it is our 169 - exactly the top edge of the box. THEY ROLL, see
    // UWScrollEdgeRules and mEdgeFrame.
    private const float scrledgeLeft = 13f;
    private const float scrledgeRight = 308f;
    private const float scrledgeTop = 183.5f;
    private const float scrledgeWidth = 4f;
    private const float scrledgeHeight = 29f;

    /// <summary>The frame of the rolling edges, counted like dseg_5c99_A94 in the original: one
    /// step per scroll of the box, wrapping at UWScrollEdgeRules.MainFrames, never reset.
    /// </summary>
    private int miEdgeFrame;

    /// <summary>The lines the box showed last time, to see how far it has scrolled since - see
    /// UWScrollEdgeRules.GetScrollAmount.</summary>
    private System.Collections.Generic.List<string> mOLastShownLines
        = new System.Collections.Generic.List<string>();

    private string mLastMessageLogText;

    /// <summary>Blink duration of the text cursor in the message box.</summary>
    private const float PromptBlinkSeconds = 0.4f;

    /// <summary>How many lines fit into the message box. Computed from its height and the
    /// font height, not hard-coded.</summary>
    public int LogLineCount
    {
        get
        {
            UWFont lOFont = mOUi.mOUWData != null ? mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Normal) : null;

            if (lOFont == null || lOFont.Height <= 0)
                return 5;

            return Mathf.Max(1, (int)(messageLogHeight / (lOFont.Height + UWFontRenderer.LineSpacing)));
        }
    }

    /// <summary>Wraps text the way the message box would show it. For everything that
    /// needs to know how many lines a text occupies - such as the page-by-page display of long
    /// texts in Interaction.</summary>
    public System.Collections.Generic.List<string> WrapLogText(string psText)
    {
        System.Collections.Generic.List<string> lOLines =
            new System.Collections.Generic.List<string>();

        UWFont lOFont = mOUi.mOUWData != null ? mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Normal) : null;

        if (lOFont == null || string.IsNullOrEmpty(psText))
            return lOLines;

        foreach (string lsLine in psText.Split('\n'))
        {
            if (string.IsNullOrEmpty(lsLine.Trim()))
            {
                lOLines.Add(string.Empty);
                continue;
            }

            lOLines.AddRange(UWFontRenderer.WrapText(lOFont, lsLine, fLogWidth()));
        }

        return lOLines;
    }

    /// <summary>Original: message log in the lower frame box - one line per entry from
    /// Interaction.ActiveStrings, newest at the bottom (the list itself already holds only the
    /// last 5, see Interaction.Update). Only set anew on a real change, as with the
    /// icon refreshes.</summary>
    internal void Refresh()
    {
        if (mOUi.mOInteraction == null)
            mOUi.mOInteraction = mOUi.GetComponent<Interaction>();

        if (mOUi.mOInteraction == null || mOUi.mOUWData == null)
            return;

        // THE MODERN SCHEME'S BIG MAP shows the classic frame only for the map (UWGameUI): the
        // frame's scroll stays empty there - it held the lines of the moment, after a load
        // "Restoring Game ... Restore Game Complete." over the map (per user, 2026-10-04); the
        // modern message box shows them again once the map is closed.
        if (mOUi.mControlSchemeRef != null && mOUi.mControlSchemeRef.Current == UWControlScheme.SchemeEnum.Modern
            && mOUi.IsMapVisible)
        {
            UWTextLabel.Hide(mMessageLogImage);
            UWTextLabel.Hide(mMoreMarkerImage);

            mLastMessageLogText = null;

            return;
        }

        // In conversation the scroll belongs to the answers (see UWConversationScreen).
        // The key is cleared so that the log is redrawn afterwards.
        // EXCEPTION: a running input ("Move how many?" when splitting a stack) must
        // be visible - while it runs, the conversation takes its answers away (per
        // user, 2026-09-11: the question did not come up in our version).
        if (mOUi.Conversation.mbConversationFrame && !mOUi.mOInteraction.IsPromptActive)
        {
            UWTextLabel.Hide(mMessageLogImage);
            UWTextLabel.Hide(mMoreMarkerImage);

            mLastMessageLogText = null;

            return;
        }

        // The old Unity font stays silent; drawing is now done with font5x6p, the
        // message font of the original (per user, 2026-09-03).
        if (mMessageLogText != null && mMessageLogText.enabled)
        {
            mMessageLogText.text = string.Empty;
            mMessageLogText.enabled = false;
        }

        bool lbCursor = mOUi.mOInteraction.ShowsPromptTextCursor
            && ((int)(Time.unscaledTime / PromptBlinkSeconds) & 1) == 0;

        string lsText = string.Join("\n", mOUi.mOInteraction.ActiveStrings);

        // The text cursor belongs only on the line currently running. It is therefore
        // not in the stored text but only in the comparison key - otherwise
        // the finished lines keep it.
        string lsKey = lsText + (lbCursor ? "\u0001" : string.Empty)
            + (mOUi.mOInteraction.IsWaitingForPage ? "\u0002" : string.Empty) + "\u0003" + fLogWidth();

        if (lsKey == mLastMessageLogText)
            return;

        mLastMessageLogText = lsKey;

        fDrawMessageLog(lsText, lbCursor);
        fDrawMoreMarker(mOUi.mOInteraction.IsWaitingForPage);
    }

    /// <summary>
    /// The [MORE] hint on the last line of the box, as long as text is still pending.
    ///
    /// In WHITE, unlike the text itself, and with square brackets (per user on the
    /// original, 2026-09-03). The text is not in the game data - in the original it comes
    /// from the program, hence fixed here.
    ///
    /// Left-aligned like the remaining text. The position in the line is measured, the alignment
    /// within it guessed.
    /// </summary>
    private void fDrawMoreMarker(bool pbVisible)
    {
        if (!pbVisible)
        {
            UWTextLabel.Hide(mMoreMarkerImage);

            return;
        }

        UWFont lOFont = mOUi.mOUWData != null ? mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Normal) : null;

        if (lOFont == null)
            return;

        if (mMoreMarkerImage == null)
        {
            GameObject lOObject = new GameObject("MessageLogMore", typeof(RectTransform), typeof(RawImage));
            lOObject.transform.SetParent(mOUi.mGameFrame, false);

            RectTransform lORect = (RectTransform)lOObject.transform;
            lORect.anchorMin = new Vector2(0f, 1f);
            lORect.anchorMax = new Vector2(0f, 1f);
            lORect.pivot = new Vector2(0f, 1f);

            mMoreMarkerImage = lOObject.GetComponent<RawImage>();
            mMoreMarkerImage.raycastTarget = false;

            int liWidth = Mathf.Max(1,
                lOFont.MeasureText(MoreMarkerText, 0, UWFontRenderer.CharacterSpacing)
                - UWFontRenderer.CharacterSpacing);

            Texture2D lOTexture = UWFontRenderer.RenderLines(lOFont,
                new System.Collections.Generic.List<string> { MoreMarkerText },
                liWidth, Color.white, mOUi.TextureFilterMode, false);

            if (lOTexture == null)
                return;

            UWTextLabel.Get(mMoreMarkerImage).Set(lOFont, lOTexture,
                new System.Collections.Generic.List<string> { MoreMarkerText },
                new System.Collections.Generic.List<Color32> { Color.white }, false,
                FontStyle.Normal, UWTextLabel.MoreMarkerRaise);
        }

        int liLineHeight = lOFont.Height + UWFontRenderer.LineSpacing;

        ((RectTransform)mMoreMarkerImage.transform).anchoredPosition = new Vector2(
            messageLogLeft, -(messageLogTop + ((LogLineCount - 1) * liLineHeight)));

        UWTextLabel.Get(mMoreMarkerImage).Show();
    }

    private const string MoreMarkerText = "[MORE]";

    /// <summary>The box's width for the text: the original's, longer in the widened frame
    /// (UWClassicWide).</summary>
    private static int fLogWidth()
    {
        return (int)messageLogWidth + UWClassicWide.Extra;
    }

    private RawImage mMoreMarkerImage;

    /// <summary>
    /// Draws the log with the original font into a texture.
    ///
    /// Wrapping is done here, not by Unity - the renderer only sets finished lines.
    /// If more lines pile up than fit into the box, the NEWEST remain; previously
    /// Unity cut off at the bottom and the newest message disappeared.
    /// </summary>
    private void fDrawMessageLog(string psText, bool pbCursor)
    {
        fBuildMessageLogImage();

        if (mMessageLogImage == null)
            return;

        UWFont lOFont = mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Normal);

        if (lOFont == null)
            return;

        System.Collections.Generic.List<string> lOLines =
            new System.Collections.Generic.List<string>();

        foreach (string lsLine in psText.Split('\n'))
        {
            if (string.IsNullOrEmpty(lsLine))
            {
                lOLines.Add(string.Empty);
                continue;
            }

            lOLines.AddRange(UWFontRenderer.WrapText(lOFont, lsLine, fLogWidth()));
        }

        int liLineHeight = lOFont.Height + UWFontRenderer.LineSpacing;
        int liMaxLines = Mathf.Max(1, (int)((messageLogHeight + UWFontRenderer.LineSpacing) / liLineHeight));

        while (lOLines.Count > liMaxLines)
            lOLines.RemoveAt(0);

        fRollEdges(lOLines);

        if (mMessageLogTexture != null)
            Object.Destroy(mMessageLogTexture);

        System.Collections.Generic.List<Color32> lOLineColours = new System.Collections.Generic.List<Color32>();

        mMessageLogTexture = lOLines.Count == 0
            ? null
            : UWFontRenderer.RenderLines(lOFont, lOLines, fLogWidth(),
                fGetLogColourTable(), mOUi.TextureFilterMode, false, lOLineColours);

        if (mMessageLogTexture == null)
        {
            UWTextLabel.Hide(mMessageLogImage);
            return;
        }

        if (pbCursor)
        {
            fDrawPromptCursor(mMessageLogTexture, lOFont, lOLines,
                lOLineColours.Count > 0 ? lOLineColours[lOLineColours.Count - 1] : mOLogTextColour);

            // The modern font gets the cursor as a character.
            lOLines[lOLines.Count - 1] += "_";
        }

        // The modern font gets, per line, the colour that was in effect at the line start; the
        // codes themselves are removed by UWTextLabel.
        UWTextLabel.Get(mMessageLogImage).Set(lOFont, mMessageLogTexture, lOLines, lOLineColours, false);
    }

    /// <summary>
    /// The text colours of the message box by colour code 0 to 6, as palette indices.
    /// UW.EXE has no table for mOUi: the message scroll print routine (seg043_37F0_4BF, called
    /// from WriteMultipleStringsToMessageScroll_seg043_37F0_2EA) compares the character after a
    /// backslash and writes a fixed byte into the font colour for each code (GOG build: the
    /// immediate values at file 0x3976D-0x397AF), verified 2026-09-15. Codes 7 to 9 do not exist
    /// in uw1 and leave the colour unchanged (UWFontRenderer ignores codes beyond the table).
    /// 0 is the usual brown (index 46 = 3C 28 20, also the default at DS:0A73; mOLogTextColour,
    /// 3D 1C 08, is only the fallback while no palette is loaded), 4 the red of
    /// "Save Game Failed.", 6 the green of "Save Game Succeeded." and of the name prompt - that
    /// is how the user sees it in the original (2026-09-12). Code 3 (96) is also the colour of
    /// [MORE].
    /// </summary>
    public static readonly int[] LogColourIndices = { 46, 38, 241, 96, 180, 196, 212 };

    private Color32[] myLogColourTable;

    private Color32[] fGetLogColourTable()
    {
        if (myLogColourTable != null)
            return myLogColourTable;

        Color32[] lyTable = new Color32[LogColourIndices.Length];

        for (int liAt = 0; liAt < lyTable.Length; liAt++)
        {
            lyTable[liAt] = mOUi.mOUWData != null && mOUi.mOUWData.Palettes != null
                ? UWScreenUi.GetColour(mOUi.mOUWData.Palettes, 0, LogColourIndices[liAt])
                : (Color32)mOLogTextColour;
        }

        // Only remember once the palette was there - otherwise the fallback colour would stay forever.
        if (mOUi.mOUWData != null && mOUi.mOUWData.Palettes != null)
            myLogColourTable = lyTable;

        return lyTable;
    }

    /// <summary>The blinking box behind the running input. The font has no
    /// character for it, so it is filled by hand. Size estimated. Also the modern scheme's scroll
    /// (UWModernScroll).</summary>
    internal static void fDrawPromptCursor(Texture2D pOTexture, UWFont pOFont,
        System.Collections.Generic.List<string> pOLines, Color32 pOColour)
    {
        if (pOLines.Count == 0)
            return;

        int liLast = pOLines.Count - 1;
        int liLeft = pOFont.MeasureText(pOLines[liLast], 0, UWFontRenderer.CharacterSpacing);
        int liTop = liLast * (pOFont.Height + UWFontRenderer.LineSpacing);
        int liWide = Mathf.Max(2, pOFont.SpaceWidth);

        for (int liY = 0; liY < pOFont.Height; liY++)
        {
            // The texture runs from bottom to top, exactly as in UWFontRenderer.
            int liTargetY = pOTexture.height - 1 - (liTop + liY);

            for (int liX = 0; liX < liWide; liX++)
            {
                int liTargetX = liLeft + liX;

                if (liTargetX >= 0 && liTargetX < pOTexture.width && liTargetY >= 0 && liTargetY < pOTexture.height)
                    pOTexture.SetPixel(liTargetX, liTargetY, pOColour);
            }
        }

        pOTexture.Apply(false, false);
    }

    /// <summary>Own picture for the log, at the same spot as the old text.
    /// </summary>
    private void fBuildMessageLogImage()
    {
        if (mMessageLogImage != null || mOUi.mGameFrame == null)
            return;

        GameObject lOObject = new GameObject("MessageLogText", typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(mOUi.mGameFrame, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(messageLogLeft, -messageLogTop);

        mMessageLogImage = lOObject.GetComponent<RawImage>();
        mMessageLogImage.raycastTarget = false;
        mMessageLogImage.enabled = false;
    }

    /// <summary>The message log in the lower frame box with its decorated edges (the edge sprites come in Init, see fLoadMessageLogSprites).</summary>
    internal void Build()
    {
        // Original colours CONFIRMED per user test: #8C6854 background, #3D1C08 text.
        Color lOLogBackgroundColor;
        Color lOLogTextColor;
        ColorUtility.TryParseHtmlString("#8C6854", out lOLogBackgroundColor);
        ColorUtility.TryParseHtmlString("#3D1C08", out lOLogTextColor);

        Image lOMessageLogBackground = UWGameUI.fCreateImage("MessageLogBackground", mOUi.mGameFrame, messageLogLeft, -messageLogTop, messageLogWidth, messageLogHeight, lOLogBackgroundColor);

        UWClassicWide.Widened(lOMessageLogBackground.rectTransform);

        GameObject lOMessageLogObj = new GameObject("MessageLog", typeof(RectTransform), typeof(Text));
        lOMessageLogObj.transform.SetParent(mOUi.mGameFrame, false);

        RectTransform lOMessageLogRect = (RectTransform)lOMessageLogObj.transform;
        lOMessageLogRect.anchorMin = new Vector2(0f, 1f);
        lOMessageLogRect.anchorMax = new Vector2(0f, 1f);
        lOMessageLogRect.pivot = new Vector2(0f, 1f);
        lOMessageLogRect.anchoredPosition = new Vector2(messageLogLeft, -messageLogTop);
        lOMessageLogRect.sizeDelta = new Vector2(messageLogWidth, messageLogHeight);
        UWClassicWide.Widened(lOMessageLogRect);

        mMessageLogText = lOMessageLogObj.GetComponent<Text>();
        mMessageLogText.font = UWInterfaceFont.Font;
        mMessageLogText.fontSize = 5;
        mMessageLogText.alignment = TextAnchor.UpperLeft;
        mMessageLogText.color = lOLogTextColor;
        mMessageLogText.horizontalOverflow = HorizontalWrapMode.Wrap;
        mMessageLogText.verticalOverflow = VerticalWrapMode.Truncate;
        mMessageLogText.raycastTarget = false;
        mMessageLogText.text = string.Empty;

        // The strips themselves are set in Init and then with every scroll.
        mOUi.fCreateCenterPivotImage("ScrledgeLeft", scrledgeLeft, scrledgeTop, scrledgeWidth, scrledgeHeight);
        mOUi.fCreateCenterPivotImage("ScrledgeRight", scrledgeRight, scrledgeTop, scrledgeWidth, scrledgeHeight);
    }

    /// <summary>The rolling edges of the message log (SCRLEDGE.GR, left strip 0 to 4, right
    /// strip 5 to 9, identified per user), loaded once in Init at frame zero.</summary>
    internal void LoadSprites()
    {
        fApplyEdges();
    }

    /// <summary>
    /// ONE STEP PER SCROLL, as in the original (UWScrollEdgeRules): the frame only moves when
    /// lines have left the box at the top. While the box is still filling up, nothing scrolls
    /// and nothing turns; a blinking cursor redraws the same lines and leaves the strip alone.
    /// </summary>
    private void fRollEdges(System.Collections.Generic.List<string> pOShownLines)
    {
        int liScrolled = UWScrollEdgeRules.GetScrollAmount(mOLastShownLines, pOShownLines);

        mOLastShownLines = new System.Collections.Generic.List<string>(pOShownLines);

        if (liScrolled <= 0)
            return;

        miEdgeFrame = UWScrollEdgeRules.Advance(miEdgeFrame, UWScrollEdgeRules.MainFrames, liScrolled);

        fApplyEdges();
    }

    private void fApplyEdges()
    {
        if (mOUi == null || mOUi.mGameFrame == null)
            return;

        mOUi.fSetStaticSprite(mOUi.mGameFrame.Find("ScrledgeLeft"), UWTexture.TextureTypes.SCRLEDGE,
            UWScrollEdgeRules.GetMainLeftImage(miEdgeFrame));
        mOUi.fSetStaticSprite(mOUi.mGameFrame.Find("ScrledgeRight"), UWTexture.TextureTypes.SCRLEDGE,
            UWScrollEdgeRules.GetMainRightImage(miEdgeFrame));
    }
}
