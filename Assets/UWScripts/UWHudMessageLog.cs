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
    private const float messageLogLeft = UWClassicLayout.MessageLog.Left;
    internal const float messageLogTop = UWClassicLayout.MessageLog.Top;
    private const float messageLogWidth = UWClassicLayout.MessageLog.Width;
    private const float messageLogHeight = UWClassicLayout.MessageLog.Height;

    /// <summary>The log in the original font (font5x6p, the message font of the original; per
    /// user, 2026-09-03).</summary>
    private RawImage mMessageLogImage;

    // Decorated edges left/right of the message log (SCRLEDGE.GR, placed per user
    // in the former UI prefab, loaded in Init). Centred
    // pivot, since they flank the log box vertically centred instead of aligning to it.
    // The positions hold up against the original: seg043_37F0_11E draws at x 11 and x 306 on
    // EXE y 30, and since that y is the top counted from the bottom of the 200 line screen
    // (199 - 30), it is our 169 - exactly the top edge of the box. THEY ROLL, see
    // UWScrollEdgeRules and UWMessageLogModel.EdgeFrame.
    private const float scrledgeLeft = UWClassicLayout.MessageLog.EdgeLeft;
    private const float scrledgeRight = UWClassicLayout.MessageLog.EdgeRight;
    private const float scrledgeTop = UWClassicLayout.MessageLog.EdgeCentreY;
    private const float scrledgeWidth = UWClassicLayout.MessageLog.EdgeWidth;
    private const float scrledgeHeight = UWClassicLayout.MessageLog.EdgeHeight;

    /// <summary>What the box shows (cleanup step 5.7, 2026-10-20): the wrapped lines, [MORE], the
    /// input box and the frame of the rolling edges, counted like dseg_5c99_A94 in the original: one
    /// step per scroll of the box, wrapping at UWScrollEdgeRules.MainFrames, never reset.</summary>
    private readonly UWMessageLogModel mOModel = new UWMessageLogModel();

    private UWFont fFont()
    {
        return mOUi.mOUWData != null ? mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Normal) : null;
    }

    /// <summary>How many lines fit into the message box. Computed from its height and the
    /// font height, not hard-coded (UWMessageLogModel.CapacityOf).</summary>
    public int LogLineCount
    {
        get { return UWMessageLogModel.CapacityOf(fFont(), (int)messageLogHeight); }
    }

    /// <summary>Wraps text the way the message box would show it. For everything that
    /// needs to know how many lines a text occupies - such as the page-by-page display of long
    /// texts in Interaction (UWMessageLogModel.WrapForPaging).</summary>
    public System.Collections.Generic.List<string> WrapLogText(string psText)
    {
        return UWMessageLogModel.WrapForPaging(fFont(), psText, fLogWidth());
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
        if (UWControlScheme.Ui.Policy.ClassicScreenOnlyUnderMap
            && mOUi.Map.IsMapVisible)
        {
            UWTextLabel.Hide(mMessageLogImage);
            UWTextLabel.Hide(mMoreMarkerImage);

            mOModel.Invalidate();

            return;
        }

        // In conversation the scroll belongs to the answers (see UWClassicConversationRenderer).
        // The key is cleared so that the log is redrawn afterwards.
        // EXCEPTION: a running input ("Move how many?" when splitting a stack) must
        // be visible - while it runs, the conversation takes its answers away (per
        // user, 2026-09-11: the question did not come up in our version).
        if (mOUi.Conversation.mbConversationFrame && !mOUi.mOInteraction.IsPromptActive)
        {
            UWTextLabel.Hide(mMessageLogImage);
            UWTextLabel.Hide(mMoreMarkerImage);

            mOModel.Invalidate();

            return;
        }

        UWFont lOFont = fFont();

        if (lOFont == null)
            return;

        // The input box blinks at the one rate of both schemes (decision 17, UWMessageLogModel).
        bool lbCursor = mOUi.mOInteraction.ShowsPromptTextCursor && UWMessageLogModel.IsCursorOn(Time.unscaledTime);

        // The text cursor belongs only on the line currently running. It is therefore
        // not in the stored text but only in the model's comparison key - otherwise
        // the finished lines keep it. Drawn anew only on a real change.
        if (!mOModel.Update(lOFont, mOUi.mOInteraction.ActiveStrings, fLogWidth(), LogLineCount,
                mOUi.mOInteraction.IsWaitingForPage, lbCursor))
            return;

        if (mOModel.Scrolled > 0)
            fApplyEdges();

        fDrawMessageLog(lOFont);
        fDrawMoreMarker(lOFont, mOModel.MoreShown);
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
    private void fDrawMoreMarker(UWFont pOFont, bool pbVisible)
    {
        if (!pbVisible)
        {
            UWTextLabel.Hide(mMoreMarkerImage);

            return;
        }

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

            UWTextLabel.Get(mMoreMarkerImage).SetLine(pOFont, UWMessageLogModel.MoreMarkerText,
                UWTextRaster.TightWidth(pOFont, UWMessageLogModel.MoreMarkerText),
                Color.white, false, mOUi.TextureFilterMode, FontStyle.Normal, UWTextLabel.MoreMarkerRaise);

            if (UWTextLabel.Get(mMoreMarkerImage).Picture == null)
                return;
        }

        int liLineHeight = pOFont.Height + UWFontRenderer.LineSpacing;

        // The scroll's last row (UWMessageLogModel.MoreRow), the one kept free of text.
        ((RectTransform)mMoreMarkerImage.transform).anchoredPosition = new Vector2(
            messageLogLeft, -(messageLogTop + (mOModel.MoreRowShown * liLineHeight)));

        UWTextLabel.Get(mMoreMarkerImage).Show();
    }

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
    /// Unity cut off at the bottom and the newest message disappeared. The lines, the width they
    /// were wrapped to and the input box come from the model (UWMessageLogModel.Update).
    /// </summary>
    private void fDrawMessageLog(UWFont pOFont)
    {
        fBuildMessageLogImage();

        if (mMessageLogImage == null)
            return;

        // The blinking box behind the running input is part of the picture asked for (in the colour
        // in effect at the last line's start; the modern font gets it as a character); the modern
        // font gets, per line, the colour that was in effect at the line start; the codes
        // themselves are removed by UWTextLabel. The pictures with and without the box both stay
        // with UWTextTextures while it blinks.
        UWTextLabel.Get(mMessageLogImage).SetText(pOFont, mOModel.ShownLines, mOModel.Width, fGetLogColourTable(),
            UWTextRaster.ColoursEnum.Codes, false, mOUi.TextureFilterMode, mOModel.CursorOn);
    }

    private Color32[] myLogColourTable;

    private Color32[] fGetLogColourTable()
    {
        return LogColourTable(mOUi.mOUWData, ref myLogColourTable);
    }

    /// <summary>The log's colours by code (UWMessageLogModel.ColourTable, the palette indices
    /// UWMessageLogModel.ColourIndices) as Unity colours. Only remembered in pyCache once the palette
    /// was there - otherwise the fallback colour would stay forever. The classic box and the modern
    /// scroll both draw with it.</summary>
    internal static Color32[] LogColourTable(DataImport pOData, ref Color32[] pyCache)
    {
        if (pyCache != null)
            return pyCache;

        UWColor32[] lyTable = UWMessageLogModel.ColourTable(pOData != null ? pOData.Palettes : null, out bool lbFromPalette);
        Color32[] lyColours = new Color32[lyTable.Length];

        for (int liAt = 0; liAt < lyColours.Length; liAt++)
            lyColours[liAt] = new Color32(lyTable[liAt].R, lyTable[liAt].G, lyTable[liAt].B, lyTable[liAt].A);

        if (lbFromPalette)
            pyCache = lyColours;

        return lyColours;
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
        // Original colours CONFIRMED per user test: #8C6854 background, #3D1C08 text (the text
        // colour is UWClassicLayout.MessageLog.FallbackColour, used while no palette is loaded -
        // UWMessageLogModel.ColourTable).
        Color lOLogBackgroundColor;
        ColorUtility.TryParseHtmlString("#8C6854", out lOLogBackgroundColor);

        Image lOMessageLogBackground = UWUiKit.CreateFrameImage(mOUi.mGameFrame, "MessageLogBackground", messageLogLeft, -messageLogTop, messageLogWidth, messageLogHeight, lOLogBackgroundColor);

        UWClassicWide.Widened(lOMessageLogBackground.rectTransform);

        // The strips themselves are set in Init and then with every scroll.
        mOUi.Classic.fCreateCenterPivotImage("ScrledgeLeft", scrledgeLeft, scrledgeTop, scrledgeWidth, scrledgeHeight);
        mOUi.Classic.fCreateCenterPivotImage("ScrledgeRight", scrledgeRight, scrledgeTop, scrledgeWidth, scrledgeHeight);
    }

    /// <summary>The rolling edges of the message log (SCRLEDGE.GR, left strip 0 to 4, right
    /// strip 5 to 9, identified per user), loaded once in Init at frame zero.</summary>
    internal void LoadSprites()
    {
        fApplyEdges();
    }

    /// <summary>The strips at the model's frame: it turns them ONE STEP PER SCROLL, as in the
    /// original (UWMessageLogModel.Update, UWScrollEdgeRules).</summary>
    private void fApplyEdges()
    {
        if (mOUi == null || mOUi.mGameFrame == null)
            return;

        mOUi.Classic.fSetStaticSprite(mOUi.mGameFrame.Find("ScrledgeLeft"), UWTexture.TextureTypes.SCRLEDGE,
            UWScrollEdgeRules.GetMainLeftImage(mOModel.EdgeFrame));
        mOUi.Classic.fSetStaticSprite(mOUi.mGameFrame.Find("ScrledgeRight"), UWTexture.TextureTypes.SCRLEDGE,
            UWScrollEdgeRules.GetMainRightImage(mOModel.EdgeFrame));
    }
}
