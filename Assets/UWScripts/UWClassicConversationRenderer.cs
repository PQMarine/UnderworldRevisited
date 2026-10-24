using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The conversation screen of the original, drawn (cleanup step 5.12, 2026-10-20: split out of
/// UWConversationScreen, which became UWConversationController; what is shown is
/// UWConversationView's, where it is shown UWConversationLayout's and UWClassicLayout's).
///
/// The frame switches to CONV.BYT (see UWGameUI.SetConversationFrame). On top of it,
/// on a canvas of its own with the same reference resolution 320x200: top left the name and
/// portrait of the conversation partner, top right those of the player character, in the parchment the
/// history, in the scroll at the bottom the answer options. The vine edges of the parchment
/// (SCRLEDGE.GR 10 to 21) ROLL: one frame per scroll of the parchment, all three heights
/// showing the same frame, read out of the original 2026-09-22 - see UWScrollEdgeRules.
///
/// ALL MEASUREMENTS ARE TAKEN FROM CONV.BYT (2026-09-11): the file carries a sample conversation,
/// and its pixels tell where the original writes - text in the parchment from 59/53 in
/// six-pixel lines, answers in the scroll from 15/169, names from 48/2 and 144/2 in
/// palette colour 0x65, the edges at 52 and 220 at heights 51, 78 and 105.
///
/// The portraits: characters with their own face (whoami 1 to 28) come from CHARHEAD.GR, all
/// others from GENHEAD.GR by their object number, the player character from HEADS.GR by
/// body type and gender (reference: conversationinitialisation.NPCPortrait and
/// SetupConversationUI) - UWConversationLayout.PartnerPortrait and PlayerPortrait.
///
/// FONT: every text field is a UWTextLabel and shows the original font or the
/// modern one, depending on the choice made with key F (see UWGameUI.fCheckFontToggle).
///
/// The modern scheme hides this canvas (UWModernConversation draws the conversation there); the
/// controller keeps drawing into it, so a switch of the scheme shows it as it stands.
/// </summary>
public sealed class UWClassicConversationRenderer : IUWConversationChoiceRows
{
    private const int ScreenWidth = UWClassicLayout.Screen.Width;

    private const int ScreenHeight = UWClassicLayout.Screen.Height;

    // --- Trade areas (CONVERSE.GR image 1, 55x38, at 82/9 and 139/9; the circles in them
    //     are 16 wide, the crosses at the edges 3 by 3). THE SLOTS ARE THE ORIGINAL'S since
    //     2026-09-26: its tables in the data segment (0xD54 the player's, 0xD74 the partner's,
    //     read from UW.EXE) put both lower rows on row 29 - the partner's lay on 28 - and one
    //     slot answers a click on 17 by 17 pixels, both edges included (ovr095_52F, ovr095_4D3).
    //     The crosses were left: the tables at 0xD64 and 0xD84 would put them one pixel right
    //     and down, but where the original uses them is not found.
    private static readonly (int X, int Y)[] myNpcTradeSlots = UWClassicLayout.Conversation.NpcTradeSlots;

    private static readonly (int X, int Y)[] myPlayerTradeSlots = UWClassicLayout.Conversation.PlayerTradeSlots;

    private static readonly (int X, int Y)[] myNpcTradeCrosses = UWClassicLayout.Conversation.NpcTradeCrosses;

    private static readonly (int X, int Y)[] myPlayerTradeCrosses = UWClassicLayout.Conversation.PlayerTradeCrosses;

    /// <summary>Three by three pixels, as in the original (per user, 2026-09-18; until then five, the
    /// positions above moved one pixel in so that the centre stays).</summary>
    private const int TradeCrossSize = UWClassicLayout.Conversation.TradeCrossSize;

    private readonly Transform mOParent;

    private readonly UWGameUI mOGameUi;

    private DataImport mOData;

    private Canvas mOCanvas;

    private RectTransform mOFrame;

    private RawImage mONpcNameImage;

    private RawImage mOPlayerNameImage;

    private Image mONpcPortrait;

    private Image mOPlayerPortrait;

    private readonly Image[] mOEdges = new Image[2 * UWScrollEdgeRules.ConversationRows];

    private readonly Image[] mONpcTradeImages = new Image[UWConversationTrade.SlotCount];

    private readonly Image[] mOPlayerTradeImages = new Image[UWConversationTrade.SlotCount];

    private readonly Image[] mONpcTradeCrossImages = new Image[UWConversationTrade.SlotCount];

    private readonly Image[] mOPlayerTradeCrossImages = new Image[UWConversationTrade.SlotCount];

    /// <summary>The count on a stack, top left of the place as in the backpack (see
    /// UWGameUI.fSetStackLabel).</summary>
    private readonly RawImage[] mONpcTradeCounts = new RawImage[UWConversationTrade.SlotCount];

    private readonly RawImage[] mOPlayerTradeCounts = new RawImage[UWConversationTrade.SlotCount];

    private Sprite mOCrossSprite;

    private RawImage mOHistoryImage;

    private RawImage mOChoiceImage;

    private readonly List<RectTransform> mOChoiceHitboxes = new List<RectTransform>();

    private readonly List<string> mOLines = new List<string>();

    private readonly List<UWConversationLayout.RoleEnum> mORoles = new List<UWConversationLayout.RoleEnum>();

    private readonly List<int> mOFirstLines = new List<int>();

    /// <summary>The trade's version the slots show, -1 for none.</summary>
    public int TradeVersionShown { get; private set; } = -1;

    public UWClassicConversationRenderer(Transform pOParent, UWGameUI pOGameUi)
    {
        mOParent = pOParent;
        mOGameUi = pOGameUi;
    }

    /// <summary>A conversation opens: the canvas (built once), the edges at the view's frame, the
    /// portraits, the names and the trade areas.</summary>
    public void Open(DataImport pOData, UWConversationView pOView)
    {
        mOData = pOData;

        fEnsureCanvas(pOView);
        ApplyEdges(pOView);

        mOCanvas.gameObject.SetActive(true);

        fShowPortraits(pOView);
        fShowNames(pOView);
        DrawTrade(pOView);
    }

    /// <summary>The conversation is over: the canvas goes.</summary>
    public void Close()
    {
        if (mOCanvas != null)
            mOCanvas.gameObject.SetActive(false);
    }

    /// <summary>
    /// Every frame: the canvas only in the classic scheme (the modern one draws the conversation
    /// itself, UWModernConversation), and THE HELP STAYS OPEN IN A CONVERSATION (per user,
    /// 2026-09-26). Its frame then slides left with the game frame, every frame, like the gargoyle
    /// eyes (the 4:3 squeeze comes through UWPixelAspectFrame); clicks follow by themselves through
    /// ScreenPointToLocalPointInRectangle.
    /// </summary>
    public void Tick(bool pbCanvasShown)
    {
        if (mOFrame != null)
            mOFrame.anchoredPosition = new Vector2(UWHelpLayout.FrameShift(UWUiFit.CanvasWidth), 0f);

        if (mOCanvas != null)
            mOCanvas.enabled = pbCanvasShown;
    }

    // ------------------------------------------------------------------
    // The parchment
    // ------------------------------------------------------------------

    /// <summary>The parchment as the view has it: its lines in their colours, [MORE] under them
    /// while the text waits; the vine edges step when it scrolled.</summary>
    public void DrawHistory(UWConversationView pOView, UWFont pOFont, UWConversationView.Policy pyPolicy)
    {
        pOView.VisibleHistory(pyPolicy, mOLines, mORoles);

        if (pOView.RollEdges(mOLines))
            ApplyEdges(pOView);

        Color32[] lyColours = new Color32[mORoles.Count];

        for (int liAt = 0; liAt < lyColours.Length; liAt++)
            lyColours[liAt] = fColour(UWConversationLayout.ClassicColour(mORoles[liAt]));

        fRenderInto(mOHistoryImage, pOFont, mOLines, lyColours, UWConversationLayout.HistoryWidth);
    }

    // ------------------------------------------------------------------
    // The scroll
    // ------------------------------------------------------------------

    /// <summary>The answers, numbered, with their click rows.</summary>
    public void DrawChoices(UWConversationView pOView, UWFont pOFont)
    {
        pOView.ChoiceLines(pOFont, mOLines, mOFirstLines);

        fRenderInto(mOChoiceImage, pOFont, mOLines, fNpcColours(mOLines.Count), UWConversationLayout.ChoicesWidth);
        fBuildHitboxes(pOFont, mOFirstLines);
    }

    /// <summary>
    /// The conversation asks for a text - a password, a name. The scroll shows
    /// ">" followed by the typed text with an underscore as cursor, as with the
    /// name entry when saving. No answer can be clicked meanwhile.
    /// </summary>
    public void DrawTypedAnswer(UWConversationView pOView, UWFont pOFont)
    {
        mOFirstLines.Clear();
        fBuildHitboxes(pOFont, mOFirstLines);

        mOLines.Clear();
        mOLines.Add(pOView.TypedAnswerLine);

        fRenderInto(mOChoiceImage, pOFont, mOLines, fNpcColours(1), UWConversationLayout.ChoicesWidth);
    }

    /// <summary>A look text in place of the answers (UWConversationView.ShowLookText); the click
    /// rows stay as they were.</summary>
    public void DrawLookText(UWFont pOFont, List<string> pOLines)
    {
        fRenderInto(mOChoiceImage, pOFont, pOLines, fNpcColours(pOLines.Count), UWConversationLayout.ChoicesWidth);
    }

    /// <summary>The scroll empty: while the text waits for [MORE], during a prompt.</summary>
    public void HideAnswers()
    {
        UWTextLabel.Hide(mOChoiceImage);
    }

    private static Color32[] fNpcColours(int piCount)
    {
        Color32[] lyColours = new Color32[piCount];
        Color32 lOColour = fColour(UWClassicLayout.Conversation.NpcText);

        for (int liAt = 0; liAt < piCount; liAt++)
            lyColours[liAt] = lOColour;

        return lyColours;
    }

    private static Color32 fColour(UWColor32 pOColour)
    {
        return new Color32(pOColour.R, pOColour.G, pOColour.B, pOColour.A);
    }

    /// <summary>
    /// Click areas over the answer lines. They lie invisibly on the choice display;
    /// an answer that runs over two lines gets a correspondingly tall area
    /// (UWConversationLayout.ChoiceRows).
    /// </summary>
    private void fBuildHitboxes(UWFont pOFont, List<int> pOLineOfChoice)
    {
        while (mOChoiceHitboxes.Count > pOLineOfChoice.Count)
        {
            RectTransform lOLast = mOChoiceHitboxes[mOChoiceHitboxes.Count - 1];

            mOChoiceHitboxes.RemoveAt(mOChoiceHitboxes.Count - 1);

            if (lOLast != null)
                Object.Destroy(lOLast.gameObject);
        }

        int liLineHeight = pOFont.Height + UWFontRenderer.LineSpacing;

        for (int liIndex = 0; liIndex < pOLineOfChoice.Count; liIndex++)
        {
            if (liIndex >= mOChoiceHitboxes.Count)
            {
                GameObject lOHitbox = new GameObject("Answer " + (liIndex + 1), typeof(RectTransform));
                lOHitbox.transform.SetParent(mOChoiceImage.transform, false);

                RectTransform lONew = (RectTransform)lOHitbox.transform;
                lONew.anchorMin = new Vector2(0f, 1f);
                lONew.anchorMax = new Vector2(1f, 1f);
                lONew.pivot = new Vector2(0.5f, 1f);

                mOChoiceHitboxes.Add(lONew);
            }

            UWConversationLayout.ChoiceRows(pOLineOfChoice, liIndex, out int liTopLine, out int liLineCount);

            RectTransform lORect = mOChoiceHitboxes[liIndex];

            lORect.sizeDelta = new Vector2(0f, liLineCount * liLineHeight);
            lORect.anchoredPosition = new Vector2(0f, -liTopLine * liLineHeight);
        }
    }

    /// <summary>The answers' click rows in screen pixels, in their order - for the gamepad's
    /// stepping.</summary>
    public void GetChoiceRects(List<Rect> pORects)
    {
        pORects.Clear();

        Vector3[] lyCorners = new Vector3[4];

        foreach (RectTransform lOHitbox in mOChoiceHitboxes)
        {
            if (lOHitbox == null)
                continue;

            lOHitbox.GetWorldCorners(lyCorners);
            pORects.Add(Rect.MinMaxRect(lyCorners[0].x, lyCorners[0].y, lyCorners[2].x, lyCorners[2].y));
        }
    }

    /// <summary>The answer under a screen point, 1 for the first; 0 for none.</summary>
    public int ChoiceAt(Vector2 pOPointer)
    {
        for (int liIndex = 0; liIndex < mOChoiceHitboxes.Count; liIndex++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(mOChoiceHitboxes[liIndex], pOPointer, null))
                return liIndex + 1;
        }

        return 0;
    }

    /// <summary>
    /// Lines in their own colour each, left-aligned (UWTextRaster's PerLine colours: a line's
    /// colour codes are skipped, as when every line was drawn on its own). Until 2026-10-19 every
    /// line was rendered into a temporary texture, read back and stacked into one (cleanup step
    /// 5.5). The picture's height is the lines' (with LineSpacing 0 the same as the line count
    /// times the line height used here before), so the answers' click rows (fBuildHitboxes, children
    /// of the picture) stay where they were.
    /// </summary>
    private static void fRenderInto(RawImage pOImage, UWFont pOFont, List<string> pOLines, Color32[] pyColours, int piWidth)
    {
        if (pOImage == null)
            return;

        if (pOLines.Count == 0)
        {
            UWTextLabel.Hide(pOImage);
            return;
        }

        UWTextLabel.Get(pOImage).SetText(pOFont, pOLines, piWidth, pyColours, UWTextRaster.ColoursEnum.PerLine, false,
            FilterMode.Point);
    }

    // ------------------------------------------------------------------
    // Names, portraits, edges
    // ------------------------------------------------------------------

    private void fShowNames(UWConversationView pOView)
    {
        UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Normal);

        if (lOFont == null)
            return;

        // The names' palette colour (reference: uimanager.CharNameColour, 0x65).
        Color32 lOColour = fGetPaletteColour(UWClassicLayout.Conversation.NameColourIndex);

        fSetNameImage(mONpcNameImage, lOFont, pOView.PartnerName, lOColour,
            UWConversationLayout.NameRoom(UWClassicLayout.Conversation.NpcNameLeft));
        fSetNameImage(mOPlayerNameImage, lOFont, pOView.PlayerName, lOColour,
            UWConversationLayout.NameRoom(UWClassicLayout.Conversation.PlayerNameLeft));
    }

    private static void fSetNameImage(RawImage pOImage, UWFont pOFont, string psName, Color32 pOColour, int piWidth)
    {
        if (string.IsNullOrEmpty(psName))
        {
            UWTextLabel.Hide(pOImage);
            return;
        }

        UWTextLabel.Get(pOImage).SetLine(pOFont, psName, piWidth, pOColour, false, FilterMode.Point);
    }

    private Color32 fGetPaletteColour(int piIndex)
    {
        try
        {
            byte[] lyBgra = mOData.Palettes.GetMainPaletteBGRA(0, piIndex);

            return new Color32(lyBgra[2], lyBgra[1], lyBgra[0], 255);
        }
        catch
        {
            return new Color32(255, 255, 255, 255);
        }
    }

    private void fShowPortraits(UWConversationView pOView)
    {
        fSetSprite(mONpcPortrait, pOView.PartnerPortraitType, pOView.PartnerPortraitIndex);
        fSetSprite(mOPlayerPortrait, UWTexture.TextureTypes.HEADS, pOView.PlayerPortraitIndex);
    }

    /// <summary>The vine edges at the view's frame, all three heights alike (seg043_37F0_163).</summary>
    public void ApplyEdges(UWConversationView pOView)
    {
        for (int liAt = 0; liAt < UWScrollEdgeRules.ConversationRows; liAt++)
        {
            fSetSprite(mOEdges[liAt], UWTexture.TextureTypes.SCRLEDGE,
                UWScrollEdgeRules.GetConversationLeftImage(pOView.EdgeFrame));
            fSetSprite(mOEdges[UWScrollEdgeRules.ConversationRows + liAt], UWTexture.TextureTypes.SCRLEDGE,
                UWScrollEdgeRules.GetConversationRightImage(pOView.EdgeFrame));
        }
    }

    // ------------------------------------------------------------------
    // Trade areas
    // ------------------------------------------------------------------

    /// <summary>The traded things, their counts and the crosses of the marked ones.</summary>
    public void DrawTrade(UWConversationView pOView)
    {
        TradeVersionShown = pOView.TradeVersion;

        for (int liSlot = 0; liSlot < UWConversationTrade.SlotCount; liSlot++)
        {
            UWConversationView.TradeSlot lONpc = pOView.TradeSlotOf(true, liSlot);
            UWConversationView.TradeSlot lOPlayer = pOView.TradeSlotOf(false, liSlot);

            fSetTradeItem(mONpcTradeImages[liSlot], myNpcTradeSlots[liSlot], lONpc.Item);
            fSetTradeItem(mOPlayerTradeImages[liSlot], myPlayerTradeSlots[liSlot], lOPlayer.Item);

            fSetTradeCount(mONpcTradeCounts[liSlot], lONpc.Count);
            fSetTradeCount(mOPlayerTradeCounts[liSlot], lOPlayer.Count);

            mONpcTradeCrossImages[liSlot].enabled = lONpc.Marked;
            mOPlayerTradeCrossImages[liSlot].enabled = lOPlayer.Marked;
        }
    }

    /// <summary>The image of a trade item, centred in its circle.</summary>
    private void fSetTradeItem(Image pOImage, (int X, int Y) pySlot, UWObject pOItem)
    {
        UWTexture lOSource = pOItem != null ? (pOItem.Icon ?? pOItem.Texture) : null;

        if (lOSource == null)
        {
            pOImage.enabled = false;
            return;
        }

        fSetSpriteFromTexture(pOImage, lOSource);

        (int X, int Y) lyPlace = UWConversationLayout.TradeItemPlace(pySlot, lOSource.Width, lOSource.Height);

        ((RectTransform)pOImage.transform).anchoredPosition = new Vector2(lyPlace.X, -lyPlace.Y);
    }

    /// <summary>The count of a stack, like UWGameUI.fSetStackLabel: small font,
    /// white, bold in the modern font. Nothing for a single piece.</summary>
    private void fSetTradeCount(RawImage pOLabel, int piCount)
    {
        if (pOLabel == null)
            return;

        if (piCount <= 1)
        {
            UWTextLabel.Hide(pOLabel);
            return;
        }

        UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Small);

        if (lOFont == null)
            return;

        string lsText = piCount.ToString();

        UWTextLabel.Get(pOLabel).SetLine(lOFont, lsText, lsText.Length * (lOFont.Height + 2), Color.white, true, FilterMode.Point,
            FontStyle.Bold);
    }

    /// <summary>A screen point in the original's pixels of the conversation's 320x200 frame (y
    /// from the top) - for the trade slots' hit test (UWConversationLayout.TradeSlotAt).</summary>
    public bool TryGetOriginalPoint(Vector2 pOScreenPosition, out float pfX, out float pfY)
    {
        pfX = 0f;
        pfY = 0f;

        Vector2 lOLocal;

        if (mOFrame == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(mOFrame, pOScreenPosition, null, out lOLocal))
            return false;

        // The frame has its pivot in the centre; the measurements count from the top left.
        pfX = lOLocal.x + (ScreenWidth / 2f);
        pfY = (ScreenHeight / 2f) - lOLocal.y;

        return true;
    }

    /// <summary>The cross that shows a marked trade item - a plus in white, five pixels
    /// in size, like the marks in the template.</summary>
    private Sprite fGetCrossSprite()
    {
        if (mOCrossSprite != null)
            return mOCrossSprite;

        Texture2D lOTexture = new Texture2D(TradeCrossSize, TradeCrossSize, TextureFormat.RGBA32, false);
        lOTexture.name = "UWClassicConversationRenderer trade cross";
        lOTexture.filterMode = FilterMode.Point;

        Color32[] lOPixels = new Color32[TradeCrossSize * TradeCrossSize];
        int liMiddle = TradeCrossSize / 2;

        for (int liY = 0; liY < TradeCrossSize; liY++)
        {
            for (int liX = 0; liX < TradeCrossSize; liX++)
            {
                bool lbSet = liX == liMiddle || liY == liMiddle;

                lOPixels[(liY * TradeCrossSize) + liX] = lbSet ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        lOTexture.SetPixels32(lOPixels);
        lOTexture.Apply();

        mOCrossSprite = Sprite.Create(lOTexture, new Rect(0f, 0f, TradeCrossSize, TradeCrossSize), new Vector2(0.5f, 0.5f), 1f);

        return mOCrossSprite;
    }

    /// <summary>Like UWGameUI.fSetUiSprite: palette indices in the texture, colour in the shader.
    /// </summary>
    private void fSetSprite(Image pOImage, UWTexture.TextureTypes peType, int piIndex)
    {
        if (pOImage == null || mOData == null || mOData.Textures == null)
            return;

        UWTexture lOSource;

        try
        {
            lOSource = mOData.Textures.GetTextureByType(peType, piIndex);
        }
        catch
        {
            pOImage.enabled = false;
            return;
        }

        fSetSpriteFromTexture(pOImage, lOSource);
    }

    private void fSetSpriteFromTexture(Image pOImage, UWTexture lOSource)
    {
        if (lOSource == null)
        {
            pOImage.enabled = false;
            return;
        }

        // Shared (UWIconCache, since 2026-10-19): the old sprite of every edge, portrait and
        // trade item used to stay behind with each change (B33).
        Sprite lOSprite = UWIconCache.Sprite(lOSource, mOGameUi != null ? mOGameUi.TextureFilterMode : FilterMode.Point);

        pOImage.sprite = lOSprite;
        pOImage.color = Color.white;
        UWIconPalette.Apply(pOImage);
        ((RectTransform)pOImage.transform).sizeDelta = new Vector2(lOSprite.texture.width, lOSprite.texture.height);
        pOImage.enabled = true;
    }

    // ------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------

    private void fEnsureCanvas(UWConversationView pOView)
    {
        if (mOCanvas != null)
            return;

        // Fitted whole into the window, bars over and under it when narrow (UWUiFit).
        mOCanvas = UWUiKit.CreateClassicCanvas(mOParent, "UW Conversation", UWUiSorting.ConversationScreen);

        // The same centred 320x200 frame as UWGameUI.fBuildCanvas - so the
        // parts lie pixel-exactly on CONV.BYT.
        mOFrame = UWUiKit.CreateClassicFrame(mOCanvas.transform, "Frame");

        int[] liEdgeTops = UWClassicLayout.Conversation.EdgeTops;

        for (int liAt = 0; liAt < UWScrollEdgeRules.ConversationRows; liAt++)
        {
            mOEdges[liAt] = UWUiKit.CreateImageAt(mOFrame, "Edge left " + (liAt + 1), UWClassicLayout.Conversation.EdgeLeft, liEdgeTops[liAt]);
            mOEdges[UWScrollEdgeRules.ConversationRows + liAt] =
                UWUiKit.CreateImageAt(mOFrame, "Edge right " + (liAt + 1), UWClassicLayout.Conversation.EdgeRight, liEdgeTops[liAt]);
        }

        // The strips start on the frame the counter stands at; it keeps running across
        // conversations, as dseg_5c99_A96 does.
        ApplyEdges(pOView);

        mONpcPortrait = UWUiKit.CreateImageAt(mOFrame, "Portrait partner", UWClassicLayout.Conversation.NpcPortraitLeft,
            UWClassicLayout.Conversation.PortraitTop);
        mOPlayerPortrait = UWUiKit.CreateImageAt(mOFrame, "Portrait player", UWClassicLayout.Conversation.PlayerPortraitLeft,
            UWClassicLayout.Conversation.PortraitTop);

        for (int liSlot = 0; liSlot < UWConversationTrade.SlotCount; liSlot++)
        {
            mONpcTradeImages[liSlot] = UWUiKit.CreateImageAt(mOFrame, "Trade item partner " + (liSlot + 1), myNpcTradeSlots[liSlot].X, myNpcTradeSlots[liSlot].Y);
            mOPlayerTradeImages[liSlot] = UWUiKit.CreateImageAt(mOFrame, "Trade item player " + (liSlot + 1), myPlayerTradeSlots[liSlot].X, myPlayerTradeSlots[liSlot].Y);

            mONpcTradeCrossImages[liSlot] = UWUiKit.CreateImageAt(mOFrame, "Mark partner " + (liSlot + 1), myNpcTradeCrosses[liSlot].X, myNpcTradeCrosses[liSlot].Y);
            mOPlayerTradeCrossImages[liSlot] = UWUiKit.CreateImageAt(mOFrame, "Mark player " + (liSlot + 1), myPlayerTradeCrosses[liSlot].X, myPlayerTradeCrosses[liSlot].Y);

            foreach (Image lOCross in new[] { mONpcTradeCrossImages[liSlot], mOPlayerTradeCrossImages[liSlot] })
            {
                lOCross.sprite = fGetCrossSprite();
                lOCross.color = Color.white;
                ((RectTransform)lOCross.transform).sizeDelta = new Vector2(TradeCrossSize, TradeCrossSize);
            }

            // The number one pixel right of the place corner, as in the backpack.
            mONpcTradeCounts[liSlot] = UWUiKit.CreateRawImageAt(mOFrame, "Count partner " + (liSlot + 1), myNpcTradeSlots[liSlot].X + 1, myNpcTradeSlots[liSlot].Y);
            mOPlayerTradeCounts[liSlot] = UWUiKit.CreateRawImageAt(mOFrame, "Count player " + (liSlot + 1), myPlayerTradeSlots[liSlot].X + 1, myPlayerTradeSlots[liSlot].Y);
        }

        mONpcNameImage = UWUiKit.CreateRawImageAt(mOFrame, "Name partner", UWClassicLayout.Conversation.NpcNameLeft, UWClassicLayout.Conversation.NameTop);
        mOPlayerNameImage = UWUiKit.CreateRawImageAt(mOFrame, "Name player", UWClassicLayout.Conversation.PlayerNameLeft, UWClassicLayout.Conversation.NameTop);

        // Parchment and scroll: one clipping area each, so that nothing reaches over the edge.
        RectTransform lOHistoryArea = fCreateArea("History", UWConversationLayout.HistoryLeft, UWConversationLayout.HistoryTop,
            UWConversationLayout.HistoryWidth, UWConversationLayout.HistoryHeight);
        mOHistoryImage = UWUiKit.CreateRawImageAt(lOHistoryArea, "Text", 0, 0);

        RectTransform lOChoiceArea = fCreateArea("Answers", UWConversationLayout.ChoicesLeft, UWConversationLayout.ChoicesTop,
            UWConversationLayout.ChoicesWidth, UWConversationLayout.ChoicesHeight);
        mOChoiceImage = UWUiKit.CreateRawImageAt(lOChoiceArea, "Text", 0, 0);
    }

    /// <summary>An area with clipping - whatever reaches beyond it is not drawn.
    /// </summary>
    private RectTransform fCreateArea(string psName, int piLeft, int piTop, int piWidth, int piHeight)
    {
        RectTransform lORect = UWUiKit.CreateClipArea(mOFrame, psName, UWUiKit.TopLeft);
        UWUiKit.SetRect(lORect, piLeft, -piTop, piWidth, piHeight);

        return lORect;
    }
}
