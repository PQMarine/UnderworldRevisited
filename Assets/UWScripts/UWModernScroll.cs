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

    /// <summary>The text and [MORE], held from UWTextTextures (cleanup step 5.5, 2026-10-19: until
    /// then a texture of their own, rebuilt on a key string made every frame, the input box drawn
    /// into the finished texture).</summary>
    private readonly UWTextTextures.Slot mOTextSlot = new UWTextTextures.Slot();

    private readonly UWTextTextures.Slot mOMoreSlot = new UWTextTextures.Slot();

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
        // was wrapped (B27).
        Texture2D lOTextTexture = mOTextSlot.Set(lOFont, mOModel.ShownLines, mOModel.Width,
            UWHudMessageLog.LogColourTable(mOUi.mOUWData, ref myColourTable), UWTextRaster.ColoursEnum.Codes, false,
            mOUi.TextureFilterMode, mOModel.CursorOn, out _)?.Texture;

        // The paper's top-left in the picture: x 15, row 2 of it (169 - 167).
        float lfPaperLeft = lOPlaced.x + (UWScrollPaperBuilder.PaperLeft * lfScaleX);
        float lfPaperTop = lOPlaced.yMax - ((UWScrollPaperBuilder.PaperTop - UWScrollPaperBuilder.SourceTop) * lfScale);

        mOText.enabled = lOTextTexture != null;

        if (mOText.enabled)
        {
            mOText.texture = lOTextTexture;
            ((RectTransform)mOText.transform).anchoredPosition = new Vector2(lfPaperLeft, lfPaperTop - (lOTextTexture.height * lfScale));
            ((RectTransform)mOText.transform).sizeDelta = new Vector2(lOTextTexture.width * lfScaleX, lOTextTexture.height * lfScale);
        }

        // [MORE] in white at the last line's start, as the classic box shows it (its row,
        // UWMessageLogModel.MoreRow; the picture's bottom edge one line below that row's top).
        mOMore.enabled = mOModel.MoreShown && fEnsureMore(lOFont);

        if (mOMore.enabled)
        {
            Texture2D lOMoreTexture = mOMoreSlot.Current.Texture;

            mOMore.texture = lOMoreTexture;
            ((RectTransform)mOMore.transform).anchoredPosition = new Vector2(lfPaperLeft,
                lfPaperTop - (((mOModel.MoreRowShown + 1) * liLineHeight) * lfScale));
            ((RectTransform)mOMore.transform).sizeDelta = new Vector2(lOMoreTexture.width * lfScaleX, lOMoreTexture.height * lfScale);
        }
    }

    /// <summary>Gives the texts back to UWTextTextures and destroys the papers (the HUD goes).</summary>
    public void Release()
    {
        mOTextSlot.Release();
        mOMoreSlot.Release();
        mOPapers.ReleaseAll();
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

    /// <summary>The marker as one line, its text taken anew when the language changed (a text id since
    /// translation step 3A).</summary>
    private static readonly string[] msMoreLines = { string.Empty };

    private static readonly Color32[] myMoreColours = { new Color32(255, 255, 255, 255) };

    private bool fEnsureMore(UWFont pOFont)
    {
        msMoreLines[0] = UWMessageLogModel.MoreMarkerText;

        return mOMoreSlot.Set(pOFont, msMoreLines, UWTextRaster.TightWidth(pOFont, UWMessageLogModel.MoreMarkerText), myMoreColours,
            UWTextRaster.ColoursEnum.Codes, false, mOUi.TextureFilterMode, false, out _)?.Texture != null;
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
