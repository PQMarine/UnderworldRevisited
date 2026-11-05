using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE ORIGINAL'S MESSAGE SCROLL IN THE MODERN INTERFACE (per user, 2026-10-09: the second of the
/// original's pieces freed as elements; it takes the messages' place and layout element when
/// switched on in the layout editor, UWModernLayout.IsScrollShown).
///
/// The frame picture (MAIN.BYT) only draws the scroll's OUTLINE - the wooden rollers at both ends
/// with their knobs, the rims above and below; the paper between them the game fills with one
/// colour, #8C6854 (UWHudMessageLog). So the picture (built engine-free by UWScrollPaperBuilder
/// since cleanup 9.16) is the frame's rows 167-199 over its whole width, the frame's dark ground
/// around the ends left transparent, the paper filled - and TALLER on request
/// (UWUserSettings.ModernScrollLines, Shift+wheel in the layout editor; per user, the same day:
/// "extending makes sense"): a row of the rollers' plain middle (183) is repeated, a line's
/// height per line more, and the rolling edges (SCRLEDGE.GR, 4x29 at x 11 and 306) get the same
/// rows in their middle. The rest as the classic box: the original font (FONT5X6P) in the log's
/// colours, the newest lines kept, the blinking input box, [MORE] in white at the last line's
/// start, and the edges turning one frame per scroll (UWScrollEdgeRules) - by the same rules,
/// UWMessageLogModel (cleanup step 5.7, 2026-10-20). A long text pages by this scroll's lines
/// while it is shown (decision 17, UWGameUI.ShownLogLines).
///
/// THE FONT KEY IN THE CLASSIC PRESET (per user, 2026-11-05): there the text and [MORE] follow F
/// as the classic screen's box does - its fields (UWTextLabel) at the scroll's scale. In the
/// other presets they keep the original font (decided with the presets, 2026-10-09).
/// </summary>
public class UWModernScroll
{
    public const int MinLines = 2;

    public const int MaxLines = 8;

    private readonly UWGameUI mOUi;

    private RawImage mOPaper;

    private RawImage mOText;

    private RawImage mOMore;

    /// <summary>The papers by width, extra rows and edge frame, each built for the colours
    /// (UWArtCache).</summary>
    private readonly UWArtCache<int> mOPapers = new UWArtCache<int>();

    /// <summary>What the scroll shows (cleanup step 5.7, 2026-10-20): the lines, [MORE], the input
    /// box and the edges' frame, by the rules the classic box follows too.</summary>
    private readonly UWMessageLogModel mOModel = new UWMessageLogModel();

    /// <summary>The text and [MORE] as the classic box's fields (UWTextLabel, since 2026-11-05: the
    /// font key in the Classic preset); their pictures are held from UWTextTextures by the fields.</summary>
    private UWTextLabel mOTextLabel;

    private UWTextLabel mOMoreLabel;

    private Color32[] myColourTable;

    public UWModernScroll(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    /// <summary>Lines of text the scroll holds: the original's four unless made taller.</summary>
    public static int Lines => UWModernClassicFrame.IsActive ? 4
        : UWModernClassicPlus.IsActive ? UWModernClassicPlus.ScrollLines
        : Mathf.Clamp(UWUserSettings.ModernScrollLines > 0 ? UWUserSettings.ModernScrollLines : 4, MinLines, MaxLines);

    /// <summary>
    /// THE ROWS THE PAPER HOLDS: Lines and one more - the paper is the original's box of 30 rows
    /// (five lines) at four Lines and grows or shrinks a line with each. Shown and paged by these
    /// rows, as the classic box (per user, 2026-10-20: in Classic Wide [MORE] stood in the fourth
    /// row, the fifth empty, where the classic box has it in the fifth).
    /// </summary>
    public static int Rows => Lines + 1;

    /// <summary>Classic Wide: the columns the frame's scroll is wider than the original's.</summary>
    private static int fWide()
    {
        return UWModernClassicFrame.IsActive ? UWModernClassicFrame.Extra : 0;
    }

    /// <summary>
    /// The paper's width for the text: the original's 291 columns, in Classic Wide as many more as
    /// the frame was widened by (fBuildPaper fills the paper that wide, up to the right edge strip).
    /// The text is wrapped AND drawn at it (B27, fixed 2026-10-20: until then it was wrapped this wide
    /// but drawn into a texture 291 wide, so a long line lost its end in Classic Wide).
    /// </summary>
    private static int TextWidth => UWScrollPaperBuilder.PaperWidth + fWide();

    public void Build(Transform pORoot)
    {
        mOPaper = fCreate(pORoot, "Scroll");
        mOText = fCreate(pORoot, "Scroll text");
        mOMore = fCreate(pORoot, "Scroll more");
        mOTextLabel = UWTextLabel.Get(mOText);
        mOMoreLabel = UWTextLabel.Get(mOMore);
    }

    /// <summary>A field at the scroll's scale, following the font key only in the Classic preset.</summary>
    private static void fScale(UWTextLabel pOLabel, float pfScale)
    {
        pOLabel.UnitScale = pfScale;
        pOLabel.UnitAspect = UWModernHudArt.PixelAspectX;
        pOLabel.OriginalOnly = !UWModernClassicFrame.IsActive;
    }

    private static RawImage fCreate(Transform pORoot, string psName)
    {
        RawImage lOImage = UWUiKit.CreateRawImage(pORoot, psName);
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

        int liLines = Lines;
        int liLineHeight = lOFont.Height + UWFontRenderer.LineSpacing;
        int liExtra = (liLines - 4) * liLineHeight;

        // The messages wrapped to the paper, the newest that fit, [MORE]'s row kept free while it
        // waits, the input box blinking at the one rate of both schemes (decision 17); the edges turn
        // one frame per line that left the top, as in the classic box (UWMessageLogModel).
        bool lbCursor = pOInteraction.ShowsPromptTextCursor && UWMessageLogModel.IsCursorOn(Time.unscaledTime);

        mOModel.Update(lOFont, pOInteraction.ActiveStrings, TextWidth, Rows, pOInteraction.IsWaitingForPage, lbCursor);

        int liEdgeFrame = mOModel.EdgeFrame;
        int liKey = (((fWide() * 1000) + liExtra) * 100) + liEdgeFrame;

        UWArtStamp lOStamp = UWVersionedTexture.Colours();

        if (!mOPapers.TryGet(liKey, lOStamp, out Texture2D lOPaper))
            lOPaper = mOPapers.Set(liKey, lOStamp, fBuildPaper(liExtra, liEdgeFrame));

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

        // The text: newest lines from the paper's top, the input box blinking; drawn as wide as it
        // was wrapped (B27). The field sizes itself (UWTextLabel: the picture's size at the scale).
        fScale(mOTextLabel, lfScale);
        mOTextLabel.SetText(lOFont, mOModel.ShownLines, mOModel.Width, UWHudMessageLog.LogColourTable(mOUi.mOUWData, ref myColourTable),
            UWTextRaster.ColoursEnum.Codes, false, mOUi.TextureFilterMode, mOModel.CursorOn);

        Texture2D lOTextTexture = mOTextLabel.IsShown ? mOTextLabel.Picture : null;

        // The paper's top-left in the picture: x 15, row 2 of it (169 - 167).
        float lfPaperLeft = lOPlaced.x + (UWScrollPaperBuilder.PaperLeft * lfScaleX);
        float lfPaperTop = lOPlaced.yMax - ((UWScrollPaperBuilder.PaperTop - UWScrollPaperBuilder.SourceTop) * lfScale);

        if (lOTextTexture != null)
            ((RectTransform)mOText.transform).anchoredPosition = new Vector2(lfPaperLeft, lfPaperTop - (lOTextTexture.height * lfScale));

        // [MORE] in white at the last line's start, as the classic box shows it (its row,
        // UWMessageLogModel.MoreRow; the picture's bottom edge one line below that row's top).
        if (!mOModel.MoreShown)
        {
            mOMoreLabel.Hide();
            return;
        }

        string lsMore = UWMessageLogModel.MoreMarkerText;

        fScale(mOMoreLabel, lfScale);
        mOMoreLabel.SetLine(lOFont, lsMore, UWTextRaster.TightWidth(lOFont, lsMore), Color.white, false, mOUi.TextureFilterMode,
            FontStyle.Normal, UWTextLabel.MoreMarkerRaise);

        Texture2D lOMoreTexture = mOMoreLabel.IsShown ? mOMoreLabel.Picture : null;

        if (lOMoreTexture != null)
            ((RectTransform)mOMore.transform).anchoredPosition = new Vector2(lfPaperLeft,
                lfPaperTop - (((mOModel.MoreRowShown + 1) * liLineHeight) * lfScale));
    }

    /// <summary>Destroys the papers (the HUD goes); the fields give their texts back themselves.</summary>
    public void Release()
    {
        mOPapers.ReleaseAll();
    }

    public void Hide()
    {
        if (mOPaper != null)
            mOPaper.enabled = false;

        UWTextLabel.Hide(mOText);
        UWTextLabel.Hide(mOMore);
    }

    /// <summary>
    /// The scroll's picture, piExtra rows taller than the frame's, its edges at piEdgeFrame: built
    /// engine-free (UWScrollPaperBuilder, cleanup 9.16) - in Classic Wide from the widened frame's own
    /// bottom rows -, uploaded here with the colour help.
    /// </summary>
    private Texture2D fBuildPaper(int piExtra, int piEdgeFrame)
    {
        UWPicture lOFrame = UWModernClassicFrame.IsActive && UWModernClassicFrame.Instance != null ? UWModernClassicFrame.Instance.Picture : null;
        UWPicture lOPaper = UWScrollPaperBuilder.Build(mOUi.mOUWData.Textures, lOFrame, piExtra, piEdgeFrame);

        return lOPaper != null ? UWTextureFactory.FromPicture(lOPaper, mOUi.TextureFilterMode, "UWModernScroll " + piExtra) : null;
    }
}
