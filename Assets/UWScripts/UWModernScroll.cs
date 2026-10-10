using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// THE ORIGINAL'S MESSAGE SCROLL IN THE MODERN INTERFACE (per user, 2026-10-09: the second of the
/// original's pieces freed as elements; it takes the messages' place and layout element when
/// switched on in the layout editor, UWModernLayout.IsScrollShown).
///
/// The frame picture (MAIN.BYT) only draws the scroll's OUTLINE - the wooden rollers at both ends
/// with their knobs, the rims above and below; the paper between them the game fills with one
/// colour, #8C6854 (UWHudMessageLog). So the picture here is the frame's rows 166-199 over its
/// whole width, index 0 left transparent, the paper filled - and TALLER on request
/// (UWUserSettings.ModernScrollLines, Shift+wheel in the layout editor; per user, the same day:
/// "extending makes sense"): a row of the rollers' plain middle (183) is repeated, a line's
/// height per line more, and the rolling edges (SCRLEDGE.GR, 4x29 at x 11 and 306) get the same
/// rows in their middle. The rest as the classic box: the original font (FONT5X6P) in the log's
/// colours, the newest lines kept, the blinking input box, [MORE] in white at the last line's
/// start, and the edges turning one frame per scroll (UWScrollEdgeRules).
/// </summary>
public class UWModernScroll
{
    /// <summary>The frame picture's rows of the scroll, the rim's top to the bottom. Row 166 above
    /// it belongs to the frame hanging over the scroll - the view's wooden bar and the compass's
    /// pedestal (per user, 2026-10-09: "at the top edge some of the UI still shows").</summary>
    private const int SourceTop = 167;

    private const int SourceRows = 33;

    /// <summary>Where the pedestal's outline still lies on the rim's top row (x 131 to 145): the rim
    /// there is taken from the same row this far to the left.</summary>
    private const int PedestalLeft = 131;

    private const int PedestalRight = 145;

    private const int PedestalPatch = 15;

    /// <summary>The upper rods' knobs (rows 167 to 175, x 0-8 and 310-319) lie under the shelves'
    /// dithered shadow; they are drawn as the lower knobs, this many rows down, which are clean
    /// and of the same shape.</summary>
    private const int UpperKnobLast = 175;

    private const int KnobLeftEnd = 8;

    private const int KnobRightStart = 310;

    private const int LowerKnobOffset = 22;

    /// <summary>The frame's dark ground around the scroll's ends (0/0/4) - not black, which outlines
    /// the rods' knobs (per user: the scroll was not freed cleanly; the ground stood as dark boxes
    /// around the knobs while their black outline, palette index 0, was taken for transparent).</summary>
    private static bool fIsGround(UWColor32 pOColour)
    {
        return pOColour.R == 0 && pOColour.G == 0 && pOColour.B == 4;
    }

    /// <summary>The row of the plain middle that is repeated to make it taller.</summary>
    private const int RepeatRow = 183;

    /// <summary>The paper (the classic box): from x 15, rows 169-198.</summary>
    private const int PaperLeft = 15;

    private const int PaperTop = 169;

    private const int PaperWidth = 291;

    private const int PaperRows = 30;

    private const int EdgeLeftX = 11;

    private const int EdgeRightX = 306;

    private const int EdgeRows = 29;

    /// <summary>The edge strips' row repeated for a taller scroll - their middle.</summary>
    private const int EdgeRepeatRow = 14;

    public const int MinLines = 2;

    public const int MaxLines = 8;

    private static readonly Color32 msPaper = new Color32(0x8C, 0x68, 0x54, 255);

    private const string MoreMarkerText = "[MORE]";

    private readonly UWGameUI mOUi;

    private RawImage mOPaper;

    private RawImage mOText;

    private RawImage mOMore;

    private readonly Dictionary<int, Texture2D> mOPapers = new Dictionary<int, Texture2D>();

    private int miArtVersion = -1;

    private int miEdgeFrame;

    private List<string> mOLastShownLines = new List<string>();

    private string msTextKey;

    private Texture2D mOTextTexture;

    private Texture2D mOMoreTexture;

    private Color32[] myColourTable;

    public UWModernScroll(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    /// <summary>Lines of text the scroll holds: the original's four unless made taller.</summary>
    public static int Lines => UWModernClassicFrame.IsActive ? 4
        : Mathf.Clamp(UWUserSettings.ModernScrollLines > 0 ? UWUserSettings.ModernScrollLines : 4, MinLines, MaxLines);

    /// <summary>Classic Wide: the columns the frame's scroll is wider than the original's.</summary>
    private static int fWide()
    {
        return UWModernClassicFrame.IsActive ? UWModernClassicFrame.Extra : 0;
    }

    public void Build(Transform pORoot)
    {
        mOPaper = fCreate(pORoot, "Scroll");
        mOText = fCreate(pORoot, "Scroll text");
        mOMore = fCreate(pORoot, "Scroll more");
    }

    private static RawImage fCreate(Transform pORoot, string psName)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pORoot, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;
        lOImage.enabled = false;
        UWPixelArtUI.Apply(lOImage);

        return lOImage;
    }

    /// <summary>
    /// Once a frame while shown: the messages (Interaction.ActiveStrings), the input box's blink and
    /// [MORE]; placed as the messages element (pODefaultBottomLeft its default corner).
    /// </summary>
    public void Update(Interaction pOInteraction, float pfPixelScale, Vector2 pODefaultBottomLeft)
    {
        if (mOPaper == null || mOUi == null || mOUi.mOUWData == null || pOInteraction == null)
            return;

        UWFont lOFont = mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Normal);

        if (lOFont == null)
            return;

        if (miArtVersion != UWColourVision.Version)
        {
            foreach (Texture2D lOOld in mOPapers.Values)
            {
                if (lOOld != null)
                    Object.Destroy(lOOld);
            }

            mOPapers.Clear();
            miArtVersion = UWColourVision.Version;
        }

        int liLines = Lines;
        int liLineHeight = lOFont.Height + UWFontRenderer.LineSpacing;
        int liExtra = (liLines - 4) * liLineHeight;
        List<string> lOShown = fShownLines(pOInteraction, lOFont, liLines);

        // The edges turn one frame per line that left the top, as in the classic box.
        int liScrolled = UWScrollEdgeRules.GetScrollAmount(mOLastShownLines, lOShown);

        mOLastShownLines = new List<string>(lOShown);

        if (liScrolled > 0)
            miEdgeFrame = UWScrollEdgeRules.Advance(miEdgeFrame, UWScrollEdgeRules.MainFrames, liScrolled);

        int liKey = (((fWide() * 1000) + liExtra) * 100) + miEdgeFrame;

        if (!mOPapers.TryGetValue(liKey, out Texture2D lOPaper) || lOPaper == null)
        {
            lOPaper = fBuildPaper(liExtra, miEdgeFrame);
            mOPapers[liKey] = lOPaper;
        }

        if (lOPaper == null)
            return;

        float lfScale = pfPixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.Messages);
        // Across at the original's pixel proportion (UWModernHudArt.PixelAspectX), paper and text alike.
        float lfScaleX = lfScale * UWModernHudArt.PixelAspectX;
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Messages,
            new Rect(pODefaultBottomLeft.x, pODefaultBottomLeft.y, lOPaper.width * lfScaleX, lOPaper.height * lfScale));

        UWModernLayout.Report(UWModernLayout.ElementEnum.Messages, lOPlaced);

        mOPaper.texture = lOPaper;
        mOPaper.enabled = true;
        ((RectTransform)mOPaper.transform).anchoredPosition = lOPlaced.position;
        ((RectTransform)mOPaper.transform).sizeDelta = lOPlaced.size;

        // The text: newest lines from the paper's top, the input box blinking.
        bool lbCursor = pOInteraction.ShowsPromptTextCursor && Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
        string lsKey = string.Join("\n", lOShown) + (lbCursor ? "|_" : "|");

        if (lsKey != msTextKey)
        {
            msTextKey = lsKey;

            if (mOTextTexture != null)
                Object.Destroy(mOTextTexture);

            List<Color32> lOLineColours = new List<Color32>();

            mOTextTexture = lOShown.Count == 0 ? null
                : UWFontRenderer.RenderLines(lOFont, lOShown, PaperWidth, fColourTable(), mOUi.TextureFilterMode, false, lOLineColours);

            if (mOTextTexture != null && lbCursor)
                UWHudMessageLog.fDrawPromptCursor(mOTextTexture, lOFont, lOShown,
                    lOLineColours.Count > 0 ? lOLineColours[lOLineColours.Count - 1] : fColourTable()[0]);
        }

        // The paper's top-left in the picture: x 15, row 2 of it (169 - 167).
        float lfPaperLeft = lOPlaced.x + (PaperLeft * lfScaleX);
        float lfPaperTop = lOPlaced.yMax - ((PaperTop - SourceTop) * lfScale);

        mOText.enabled = mOTextTexture != null;

        if (mOText.enabled)
        {
            mOText.texture = mOTextTexture;
            ((RectTransform)mOText.transform).anchoredPosition = new Vector2(lfPaperLeft, lfPaperTop - (mOTextTexture.height * lfScale));
            ((RectTransform)mOText.transform).sizeDelta = new Vector2(mOTextTexture.width * lfScaleX, mOTextTexture.height * lfScale);
        }

        // [MORE] in white at the last line's start, as the classic box shows it.
        mOMore.enabled = pOInteraction.IsWaitingForPage && fEnsureMore(lOFont);

        if (mOMore.enabled)
        {
            mOMore.texture = mOMoreTexture;
            ((RectTransform)mOMore.transform).anchoredPosition = new Vector2(lfPaperLeft,
                lfPaperTop - ((liLines * liLineHeight) * lfScale));
            ((RectTransform)mOMore.transform).sizeDelta = new Vector2(mOMoreTexture.width * lfScaleX, mOMoreTexture.height * lfScale);
        }
    }

    public void Hide()
    {
        if (mOPaper != null)
            mOPaper.enabled = false;

        if (mOText != null)
            mOText.enabled = false;

        if (mOMore != null)
            mOMore.enabled = false;
    }

    /// <summary>The messages wrapped to the paper, the newest that fit; while [MORE] waits its line
    /// is kept free.</summary>
    private static List<string> fShownLines(Interaction pOInteraction, UWFont pOFont, int piLines)
    {
        List<string> lOLines = new List<string>();
        IReadOnlyList<string> lOMessages = pOInteraction.ActiveStrings;

        for (int liAt = 0; lOMessages != null && liAt < lOMessages.Count; liAt++)
        {
            string lsLine = lOMessages[liAt];

            if (string.IsNullOrEmpty(lsLine))
            {
                lOLines.Add(string.Empty);
                continue;
            }

            lOLines.AddRange(UWFontRenderer.WrapText(pOFont, lsLine, PaperWidth + fWide()));
        }

        int liRoom = pOInteraction.IsWaitingForPage ? piLines - 1 : piLines;

        while (lOLines.Count > Mathf.Max(1, liRoom))
            lOLines.RemoveAt(0);

        return lOLines;
    }

    private bool fEnsureMore(UWFont pOFont)
    {
        if (mOMoreTexture != null)
            return true;

        int liWidth = Mathf.Max(1, pOFont.MeasureText(MoreMarkerText, 0, UWFontRenderer.CharacterSpacing) - UWFontRenderer.CharacterSpacing);

        mOMoreTexture = UWFontRenderer.RenderLines(pOFont, new List<string> { MoreMarkerText }, liWidth, Color.white,
            mOUi.TextureFilterMode, false);

        return mOMoreTexture != null;
    }

    /// <summary>The log's colours by code (UWHudMessageLog.LogColourIndices) from the palette.</summary>
    private Color32[] fColourTable()
    {
        if (myColourTable != null)
            return myColourTable;

        Color32[] lyTable = new Color32[UWHudMessageLog.LogColourIndices.Length];

        for (int liAt = 0; liAt < lyTable.Length; liAt++)
            lyTable[liAt] = UWScreenUi.GetColour(mOUi.mOUWData.Palettes, 0, UWHudMessageLog.LogColourIndices[liAt]);

        myColourTable = lyTable;

        return lyTable;
    }

    /// <summary>
    /// The scroll's picture, piExtra rows taller than the frame's: rows 166-199 of MAIN.BYT over
    /// the full width with the middle row repeated, index 0 transparent, the paper filled, the
    /// edge strips of the frame laid on, their middle row repeated as well.
    /// </summary>
    private Texture2D fBuildPaper(int piExtra, int piEdgeFrame)
    {
        UWColor32[] lOColours;
        int liWidth;
        int liWide = 0;
        UWPicture lOFrame = UWModernClassicFrame.IsActive && UWModernClassicFrame.Instance != null ? UWModernClassicFrame.Instance.Picture : null;

        if (lOFrame != null)
        {
            // CLASSIC WIDE: the widened frame's own bottom rows; the right knobs, the right edge and
            // the pedestal's patch lie where its parts moved.
            lOColours = lOFrame.Pixels;
            liWidth = lOFrame.Width;
            liWide = liWidth - 320;
        }
        else
        {
            UWTexture lOMain;

            try
            {
                lOMain = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0);
            }
            catch
            {
                return null;
            }

            if (lOMain == null || lOMain.Width != 320 || lOMain.Height < SourceTop + SourceRows)
                return null;

            lOColours = lOMain.GetUWColor32();
            liWidth = lOMain.Width;
        }

        int liShift = liWide / 2;
        int liHeight = SourceRows + piExtra;
        Color32[] lOTopDown = new Color32[liWidth * liHeight];
        Color32 lOPaper = UWColourVision.Apply(msPaper);

        for (int liRow = 0; liRow < liHeight; liRow++)
        {
            int liSource = fSourceRow(liRow, SourceTop, RepeatRow, piExtra);

            for (int liX = 0; liX < liWidth; liX++)
            {
                int liFrom = liSource == SourceTop && liX >= PedestalLeft + liShift && liX <= PedestalRight + liShift ? liX - PedestalPatch : liX;
                int liFromRow = liSource <= UpperKnobLast && (liX <= KnobLeftEnd || liX >= KnobRightStart + liWide) ? liSource + LowerKnobOffset : liSource;
                int liAt = (liFromRow * liWidth) + liFrom;
                UWColor32 lOColour = lOColours[liAt];

                if (fIsGround(lOColour))
                    continue;

                lOTopDown[(liRow * liWidth) + liX] = UWColourVision.Apply(new Color32(lOColour.R, lOColour.G, lOColour.B, 255));
            }
        }

        for (int liRow = PaperTop - SourceTop; liRow < PaperTop - SourceTop + PaperRows + piExtra; liRow++)
        {
            for (int liX = PaperLeft; liX < PaperLeft + PaperWidth + liWide; liX++)
                lOTopDown[(liRow * liWidth) + liX] = lOPaper;
        }

        fLayEdge(lOTopDown, liWidth, UWScrollEdgeRules.GetMainLeftImage(piEdgeFrame), EdgeLeftX, piExtra);
        fLayEdge(lOTopDown, liWidth, UWScrollEdgeRules.GetMainRightImage(piEdgeFrame), EdgeRightX + liWide, piExtra);

        // Unity's rows run bottom-up.
        Color32[] lOBottomUp = new Color32[lOTopDown.Length];

        for (int liRow = 0; liRow < liHeight; liRow++)
            System.Array.Copy(lOTopDown, liRow * liWidth, lOBottomUp, (liHeight - 1 - liRow) * liWidth, liWidth);

        Texture2D lOTexture = new Texture2D(liWidth, liHeight, TextureFormat.RGBA32, false);
        lOTexture.name = "UWModernScroll " + piExtra;
        lOTexture.filterMode = mOUi.TextureFilterMode;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(lOBottomUp);
        lOTexture.Apply(false, false);

        return lOTexture;
    }

    /// <summary>Output row to source row: up to the repeated row as they are, then that row for
    /// the extra ones, then the rest shifted.</summary>
    private static int fSourceRow(int piRow, int piTop, int piRepeat, int piExtra)
    {
        int liRepeatAt = piRepeat - piTop;

        if (piRow <= liRepeatAt)
            return piTop + piRow;

        if (piRow <= liRepeatAt + piExtra)
            return piRepeat;

        return piTop + piRow - piExtra;
    }

    private void fLayEdge(Color32[] pOTopDown, int piWidth, int piImage, int piX, int piExtra)
    {
        UWTexture lOEdge;

        try
        {
            lOEdge = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.SCRLEDGE, piImage);
        }
        catch
        {
            return;
        }

        if (lOEdge == null || lOEdge.Width <= 0 || lOEdge.Height <= 0)
            return;

        UWColor32[] lOPixels = lOEdge.GetUWColor32();
        int liTop = PaperTop - SourceTop;

        for (int liRow = 0; liRow < lOEdge.Height + piExtra; liRow++)
        {
            int liSource = fSourceRow(liRow, 0, Mathf.Min(EdgeRepeatRow, lOEdge.Height - 1), piExtra);

            for (int liX = 0; liX < lOEdge.Width; liX++)
            {
                UWColor32 lOPixel = lOPixels[(liSource * lOEdge.Width) + liX];

                if (lOPixel.A == 0)
                    continue;

                int liTarget = ((liTop + liRow) * piWidth) + piX + liX;

                if (liTarget >= 0 && liTarget < pOTopDown.Length)
                    pOTopDown[liTarget] = UWColourVision.Apply(new Color32(lOPixel.R, lOPixel.G, lOPixel.B, 255));
            }
        }
    }
}
