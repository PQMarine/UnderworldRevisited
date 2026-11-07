using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN HUD'S MESSAGES (out of UWModernHud, cleanup 5.16, 2026-10-22): above the flasks the
/// last messages, as many as the box has lines (Interaction.ModernMessageCount), colour codes
/// turned into colours, [MORE] while the game waits for a page - or the original's scroll in
/// their place (UWModernScroll), which draws and places itself. The box's dark back gives way to
/// the part's generated back where one is chosen (UWModernPartBacks).
/// </summary>
public class UWModernMessageBox : UWModernPart
{
    private Image mOMessageBack;

    private Text mOMessages;

    /// <summary>The original's scroll (UWModernLayout.IsScrollShown), the HUD's.</summary>
    private readonly UWModernScroll mOScroll;

    /// <summary>The least brightness a message colour keeps on the dark leather.</summary>
    private const float MessageColourLuma = 140f;

    /// <summary>The dark back's own alpha, faded with the newest line (GitHub issue 18).</summary>
    private const float BackAlpha = 0.55f;

    public UWModernMessageBox(UWModernHud pOHud, UWModernScroll pOScroll) : base(pOHud)
    {
        mOScroll = pOScroll;
    }

    /// <summary>Where the message box starts above the flasks (screen pixels from the bottom) -
    /// the rune panel (UWModernRunePanel) reaches down to here at most.</summary>
    public float LeftColumnBottom => mfLeftColumnBottom;

    private float mfLeftColumnBottom;

    /// <summary>The box's dark back is on (it has text, or the layout is edited).</summary>
    public bool IsBoxShown => mOMessageBack.enabled;

    /// <summary>The part's generated back lies in the dark box's place (UWModernPartBacks).</summary>
    public void HideBox()
    {
        mOMessageBack.enabled = false;
    }

    public override void Build(Transform pORoot)
    {
        mOMessageBack = UWUiKit.CreateImage(pORoot, "Message back", new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Color(0.05f, 0.05f, 0.06f, BackAlpha));
        mOMessages = UWUiKit.CreateText(pORoot, "Messages", new Vector2(0f, 0f), new Vector2(0f, 0f), TextAnchor.LowerLeft, UWModernPalette.HudText, mOUi, UWUiKit.HudText);
    }

    /// <summary>The box's size and place at the UI's scale, and where the left column starts.</summary>
    public override void OnPixelScale(float pfPixelScale)
    {
        base.OnPixelScale(pfPixelScale);

        float liScale = pfPixelScale;
        float lfMargin = 6f * liScale;
        int liFontSize = FontSize;

        mOMessages.fontSize = liFontSize;

        float lfMessageWidth = Mathf.Min(Screen.width * 0.45f, liFontSize * 36f);
        float lfMessageHeight = liFontSize * 1.35f * Interaction.ModernMessageCount + (2f * lfMargin);
        float lfFlaskRow = ((UWModernVitals.ShelfMargin + 42) * liScale) + (liFontSize * 1.5f) + lfMargin;

        mfLeftColumnBottom = lfFlaskRow;

        ((RectTransform)mOMessageBack.transform).anchoredPosition = new Vector2(lfMargin, lfFlaskRow);
        ((RectTransform)mOMessageBack.transform).sizeDelta = new Vector2(lfMessageWidth, lfMessageHeight);
        ((RectTransform)mOMessages.transform).anchoredPosition = new Vector2(2f * lfMargin, lfFlaskRow + lfMargin);
        ((RectTransform)mOMessages.transform).sizeDelta = new Vector2(lfMessageWidth - (2f * lfMargin), lfMessageHeight - (2f * lfMargin));
    }

    protected override void fUpdate()
    {
        fUpdateMessages();
        fPlaceMessages();
    }

    /// <summary>The message box moves right of the rune panel while the panel covers it (per user
    /// on the mockup, 2026-10-04).</summary>
    private void fPlaceMessages()
    {
        if (UWModernLayout.IsScrollShown)
            return;

        // Its own size and place (UWModernLayout); by default above the shelf, right of the rune
        // panel while that covers it.
        float lfScale = UWModernLayout.Scale(UWModernLayout.ElementEnum.Messages);
        float lfMargin = 6f * miPixelScale * lfScale;
        int liFontSize = Mathf.Max(9, Mathf.RoundToInt(FontSize * lfScale));
        float lfWidth = Mathf.Min(Screen.width * 0.45f, liFontSize * 36f);
        float lfHeight = (liFontSize * 1.35f * Interaction.ModernMessageCount) + (2f * lfMargin);

        // As tall as its text needs at least - long lines wrap, and the font's lines are taller
        // than 1.35 of its size (per user, 2026-10-04: the text stood out over the box's top).
        mOMessages.fontSize = liFontSize;
        ((RectTransform)mOMessages.transform).sizeDelta = new Vector2(lfWidth - (2f * lfMargin), 10f);
        lfHeight = Mathf.Max(lfHeight, Mathf.Ceil(mOMessages.preferredHeight) + (2f * lfMargin));

        float lfX = 6f * miPixelScale;
        float lfY = mfLeftColumnBottom;
        RectTransform lOBack = (RectTransform)mOMessageBack.transform;
        RectTransform lOText = (RectTransform)mOMessages.transform;
        UWModernRunePanel lORunes = UWModernRunePanel.Instance;

        if (lORunes != null && lORunes.ScreenRect.width > 0f && !UWModernLayout.IsPlaced(UWModernLayout.ElementEnum.Messages))
        {
            Rect lOPanel = lORunes.ScreenRect;

            if (lOPanel.yMin < lfY + lfHeight && lOPanel.yMax > lfY && lOPanel.xMax > lfX)
                lfX = Mathf.Round(lOPanel.xMax + lfX);
        }

        Rect lOPlaced = fPlaceAndReport(UWModernLayout.ElementEnum.Messages, new Rect(lfX, lfY, lfWidth, lfHeight));

        mOMessages.fontSize = liFontSize;
        lOBack.anchoredPosition = lOPlaced.position;
        lOBack.sizeDelta = lOPlaced.size;
        lOText.anchoredPosition = lOPlaced.position + new Vector2(lfMargin, lfMargin);
        lOText.sizeDelta = lOPlaced.size - new Vector2(2f * lfMargin, 2f * lfMargin);

        // While the layout is edited the box shows, empty or not.
        if (UWModernLayout.IsEditing)
            mOMessageBack.enabled = true;
    }

    /// <summary>
    /// The last messages, as many as the box has lines (Interaction.ModernMessageCount). THE FADE (GitHub
    /// issue 18, 2026-11-07; the layout editor's "Fade out", UWUserSettings.ModernMessageFade): each line fades
    /// out that many seconds after it was written (UWMessageLogModel.LineAlpha), a gone line leaves the box,
    /// and the dark back fades with the newest. Nothing fades while the game waits for a page or while the
    /// layout is edited; typed input (a mantra, "Move how many?") keeps its own line while it runs, the older
    /// lines fade on (per user 2026-11-07: the modern mantra is typed in this box; until the same day a running
    /// input held them all, so typing brought faded lines back). The original's scroll never fades.
    /// </summary>
    private void fUpdateMessages()
    {
        // The original's scroll instead, when switched on: it draws and places itself.
        if (mOScroll != null && UWModernLayout.IsScrollShown)
        {
            mOMessages.enabled = false;
            mOMessageBack.enabled = false;
            mOScroll.Update(mOInteraction, miPixelScale, new Vector2(6f * miPixelScale, mfLeftColumnBottom));
            return;
        }

        if (mOScroll != null)
            mOScroll.Hide();

        mOMessages.enabled = true;

        System.Collections.Generic.IReadOnlyList<string> lOLines = mOInteraction.ActiveStrings;
        System.Collections.Generic.IReadOnlyList<float> lOTimes = mOInteraction.ActiveStringTimes;
        System.Text.StringBuilder lOText = new System.Text.StringBuilder();
        int liColour = 0;
        int liFade = mOInteraction.IsWaitingForPage || UWModernLayout.IsEditing ? 0 : UWUserSettings.ModernMessageFade;
        int liPrompt = mOInteraction.PromptLineIndex;
        float lfNow = Time.unscaledTime;
        float lfNewest = 0f;
        bool lbAny = false;

        // The list keeps more for the tall scroll; the box shows the newest of its lines.
        int liFirst = lOLines != null ? Mathf.Max(0, lOLines.Count - Interaction.ModernMessageCount) : 0;

        for (int liAt = liFirst; lOLines != null && liAt < lOLines.Count; liAt++)
        {
            float lfAlpha = liAt != liPrompt && lOTimes != null && liAt < lOTimes.Count
                ? UWMessageLogModel.LineAlpha(lfNow - lOTimes[liAt], liFade) : 1f;

            // The colour codes run on through a faded line, as through a shown one.
            if (lfAlpha <= 0f)
            {
                liColour = UWMessageLogModel.ColourCodeAfter(lOLines[liAt], liColour);
                continue;
            }

            if (lbAny)
                lOText.Append('\n');

            lbAny = true;
            lfNewest = lfAlpha;
            fAppendColoured(lOText, UWFontRenderer.CleanText(lOLines[liAt]), ref liColour, lfAlpha);
        }

        // The input box at the one rate of both schemes (decision 17, 2026-10-20; until then the
        // first half of every second).
        if (mOInteraction.ShowsPromptTextCursor && UWMessageLogModel.IsCursorOn(Time.unscaledTime))
            lOText.Append('_');

        if (mOInteraction.IsWaitingForPage)
            lOText.Append("  ").Append(UWMessageLogModel.MoreMarkerText);

        mOMessages.text = lOText.ToString();
        mOMessageBack.enabled = lOText.Length > 0;

        Color lOBack = mOMessageBack.color;

        lOBack.a = BackAlpha * (lbAny ? lfNewest : 1f);
        mOMessageBack.color = lOBack;
    }

    /// <summary>
    /// THE GAME'S COLOUR CODES in the messages (per user, 2026-10-05: "\6Save Game Succeeded.\0"
    /// showed its codes): a backslash and a digit switch the colour until the next code, across
    /// lines as in the original (UWTextRaster); the codes are never shown. The
    /// colours are the classic message box's (UWMessageLogModel.ColourIndices), 0 the modern
    /// text colour - on the dark leather the darker ones are lightened towards white until they
    /// read (fReadable). Codes beyond the table leave the colour as it is.
    /// </summary>
    private void fAppendColoured(System.Text.StringBuilder pOText, string psLine, ref int piColour, float pfAlpha)
    {
        // A fading line: the plain text colour with the line's alpha around it, every code's colour too.
        bool lbFaded = pfAlpha < 1f;

        if (lbFaded)
        {
            Color lOPlain = UWModernPalette.HudText;

            lOPlain.a *= pfAlpha;
            pOText.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(lOPlain)).Append('>');
        }

        bool lbOpen = fOpenColour(pOText, piColour, pfAlpha);

        for (int liAt = 0; liAt < psLine.Length; liAt++)
        {
            if (!UWFont.IsColourCode(psLine, liAt))
            {
                pOText.Append(psLine[liAt]);
                continue;
            }

            int liCode = psLine[liAt + 1] - '0';

            liAt++;

            if (liCode >= UWMessageLogModel.ColourCodeCount || liCode == piColour)
                continue;

            if (lbOpen)
                pOText.Append("</color>");

            piColour = liCode;
            lbOpen = fOpenColour(pOText, piColour, pfAlpha);
        }

        if (lbOpen)
            pOText.Append("</color>");

        if (lbFaded)
            pOText.Append("</color>");
    }

    private bool fOpenColour(System.Text.StringBuilder pOText, int piColour, float pfAlpha)
    {
        if (piColour <= 0 || mOUi == null || mOUi.mOUWData == null || mOUi.mOUWData.Palettes == null)
            return false;

        Color32 lOColour = fReadable(UWScreenUi.GetColour(mOUi.mOUWData.Palettes, 0, UWMessageLogModel.ColourIndices[piColour]));

        if (pfAlpha < 1f)
        {
            lOColour.a = (byte)Mathf.RoundToInt(lOColour.a * pfAlpha);
            pOText.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(lOColour)).Append('>');
        }
        else
            pOText.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(lOColour)).Append('>');

        return true;
    }

    /// <summary>A colour lightened towards white until its brightness is MessageColourLuma (the
    /// classic red 91 becomes a soft red, the green 142 stays; the black of code 2 a grey).</summary>
    private static Color32 fReadable(Color32 pOColour)
    {
        float lfLuma = (0.299f * pOColour.r) + (0.587f * pOColour.g) + (0.114f * pOColour.b);

        if (lfLuma >= MessageColourLuma)
            return pOColour;

        float lfTowardsWhite = (MessageColourLuma - lfLuma) / (255f - lfLuma);

        return Color32.Lerp(pOColour, new Color32(255, 255, 255, 255), lfTowardsWhite);
    }
}
