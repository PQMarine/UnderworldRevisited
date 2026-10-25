using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S CONVERSATION (stage 4, decided per user on a mockup, 2026-10-04): the
/// controller (UWConversationController, the classic screen's driver until cleanup step 5.12) keeps
/// driving the conversation, the classic canvas hidden; this draws the same UWConversationView over
/// the dimmed world, left of the character panel and the bags:
///
///   - at the top a leather block: the partner's portrait and name at the left, the player's at
///     the right, between them the two trade areas, "...'s goods" and "Your offer", ALWAYS shown
///     (per user: also outside a trade), 2 by 2 slots each; a thing marked for the deal wears a
///     gold ring instead of the original's cross;
///   - in the middle the history on leather in light, readable text - the partner's words light,
///     the player's answers gold and indented, the narrator dimmer; it scrolls with the wheel and
///     never waits for [MORE];
///   - below the answers, numbered, gold under the pointer; a left click or the keys 1 to 9
///     choose (UWConversationController.fReadChoice); a typed answer shows as a field with a caret, a
///     look at a thing replaces the answers for its two seconds, as on the classic scroll;
///   - the bags open and the character panel shows its Character tab for the trade, the pointer
///     is free meanwhile; all of it goes back as it was when the conversation ends. The trading
///     itself is the bags' (UWModernBagTrade): drag into "Your offer", click to mark,
///     right button to look.
///
/// The block keeps left of the panel and the bag windows (per user, the same day: room for the
/// paperdoll and the containers) and is centred in what is left.
///
/// THE LAYOUT EDITOR moves and sizes it as a part of its own (UWModernLayout, per user the same day),
/// with a PREVIEW of sample text while its Dialog button is on (Previewing) - nothing of a real
/// conversation, no dimming, the bags and panels left as they are.
///
/// THE TEXT SIZE of the history and the answers has its own setting (per user, the same day: at
/// small UI sizes there is much room), 70 to 200 percent, kept in the settings
/// (UWUserSettings.ConversationTextPercent) - set in the layout editor's inspector only, no more in
/// the game (per user, 2026-10-10).
///
/// RICH TEXT ESCAPED (B31 of the cleanup plan, fixed by cleanup step 5.12, 2026-10-20): every text
/// shown here - the history, the answers, a typed answer, a look text, the names and the trade
/// area's title - goes through UWRichText.Escape, so a typed answer or a name with "&lt;" cannot
/// open a tag and break the layout.
/// </summary>
public class UWModernConversation : MonoBehaviour, IUWConversationChoiceRows
{
    public static UWModernConversation Instance { get; private set; }

    /// <summary>The layout in original pixels.</summary>
    private const int MaxWidth = 236;

    private const int MinWidth = 200;

    private const int Margin = 4;

    private const int BlockGap = 3;

    private const int TopHeight = 58;

    private const int Pad = 8;

    private const int FramePad = 4;

    private const int TradeWidth = 48;

    private const int TradeHeight = 50;

    private const int TradeGap = 6;

    private const int SlotPitch = 19;

    private const int MinHistoryHeight = 40;

    private const int RowGap = 2;

    private const int MaxRows = 9;

    /// <summary>The history's and the answers' text, in original pixels (per user, 2026-10-04:
    /// larger than the bags' 4.6 at 100 %), times the player's own size (TextPercent).</summary>
    private const float BaseTextSize = 5.4f;

    private static float TextSize => BaseTextSize * TextPercent / 100f;

    /// <summary>The text size chosen in the layout editor's inspector, in percent.</summary>
    public static int TextPercent
    {
        get
        {
            int liPercent = UWUserSettings.ConversationTextPercent;

            return liPercent <= 0 ? 100 : Mathf.Clamp(liPercent, MinTextPercent, MaxTextPercent);
        }
    }

    public const int MinTextPercent = 70;

    public const int MaxTextPercent = 200;

    public const int TextPercentStep = 10;

    /// <summary>The room above the history's text, original pixels.</summary>
    private const int HistoryTopPad = 6;

    private static readonly Color msNpc = new Color32(240, 222, 180, 255);

    private static readonly Color msTitle = new Color32(150, 138, 116, 255);

    private const string NpcHex = "#F0DEB4";

    private const string PlayerHex = "#ECBA6E";

    private const string NarratorHex = "#BEB096";

    private const string NumberHex = "#ECC470";

    // --- THE ORIGINAL'S LOOK (UWModernLayout.IsConversationOriginal; per user, 2026-10-10: "free
    //     the conversation UI"; the vines beside the parchment hang on the view's bars and stay
    //     with the dragons; the player's lines get a parchment of their own, always the same size;
    //     the history scrolls, no [MORE]). Original pixels from the block's top left, which is
    //     CONV.BYT's 43/0 (UWHudArt.ConversationHeader); everything at the original's proportion.
    private const int OrigWidth = UWClassicLayout.Conversation.HeaderWidth;

    private const int OrigHeaderRows = UWClassicLayout.Conversation.HeaderHeight;

    private const int OrigParchmentX = UWClassicLayout.Conversation.ParchmentLeft - OrigLeft;

    private const int OrigParchmentTop = UWClassicLayout.Conversation.ParchmentTop;

    private const int OrigParchmentRows = UWClassicLayout.Conversation.ParchmentHeight;

    /// <summary>The block's left in CONV.BYT, which every Orig place counts from.</summary>
    private const int OrigLeft = UWClassicLayout.Conversation.HeaderLeft;

    private const int OrigAnswersTop = OrigParchmentTop + OrigParchmentRows - 2;

    /// <summary>The answers' parchment's height - always the same, the answers' text made to fit
    /// (fLayoutOriginal; per user, 2026-10-10: the block keeps the size it is set to).</summary>
    private const int OrigAnswersRows = 52;

    /// <summary>The rows under the answers kept free of text (the lower rollers' rim), and the
    /// smallest the answers' text is made.</summary>
    private const int OrigAnswerBottom = 8;

    private const int MinAnswerFont = 6;


    /// <summary>The history's place on the parchment (the classic conversation: 59/53, 159 x 78).</summary>
    private const int OrigTextX = UWClassicLayout.Conversation.HistoryLeft - OrigLeft;

    private const int OrigTextTop = UWClassicLayout.Conversation.HistoryTop;

    private const int OrigTextWidth = UWClassicLayout.Conversation.HistoryWidth;

    private const int OrigTextRows = UWClassicLayout.Conversation.HistoryHeight;

    /// <summary>The answers' first row on their parchment.</summary>
    private const int OrigAnswerTextTop = OrigAnswersTop + 7;

    /// <summary>The names on the plates (the classic conversation: 48/2 and 144/2, 88 wide).</summary>
    private static readonly int[] miOrigNameX =
        { UWClassicLayout.Conversation.NpcNameLeft - OrigLeft, UWClassicLayout.Conversation.PlayerNameLeft - OrigLeft };

    private const int OrigNameWidth = UWClassicLayout.Conversation.NameWidth;

    /// <summary>The portraits (the classic conversation: 45/11 and 197/11).</summary>
    private static readonly int[] miOrigPortraitX =
        { UWClassicLayout.Conversation.NpcPortraitLeft - OrigLeft, UWClassicLayout.Conversation.PlayerPortraitLeft - OrigLeft };

    private const int OrigPortraitTop = UWClassicLayout.Conversation.PortraitTop;

    /// <summary>The trade slots (the classic conversation's tables: the partner's from 91/11, the
    /// player's from 148/11, 21 apart, the lower row on 29), 16 wide, a click answering on 17.</summary>
    private static readonly Vector2Int[] miOrigSlots = fOrigSlots();

    private static Vector2Int[] fOrigSlots()
    {
        List<Vector2Int> lOSlots = new List<Vector2Int>();

        foreach ((int X, int Y) lySlot in UWClassicLayout.Conversation.NpcTradeSlots)
            lOSlots.Add(new Vector2Int(lySlot.X - OrigLeft, lySlot.Y));

        foreach ((int X, int Y) lySlot in UWClassicLayout.Conversation.PlayerTradeSlots)
            lOSlots.Add(new Vector2Int(lySlot.X - OrigLeft, lySlot.Y));

        return lOSlots.ToArray();
    }

    private const int OrigSlotSize = UWClassicLayout.Conversation.TradeSlotSize;

    /// <summary>The original's colours: the names (#8C8CA8, CONV.BYT's sample), the partner's text
    /// (#3C2820), the player's (#883E14), the narrator's (black) - UWConversationLayout.ClassicColour.</summary>
    private static readonly Color msOrigName = fColour(UWClassicLayout.Conversation.NameColour);

    private static readonly string OrigNpcHex = fHex(UWClassicLayout.Conversation.NpcText);

    private static readonly string OrigPlayerHex = fHex(UWClassicLayout.Conversation.PlayerText);

    private static readonly string OrigNarratorHex = fHex(UWClassicLayout.Conversation.NarratorText);

    private static readonly Color msOrigText = fColour(UWClassicLayout.Conversation.NpcText);

    private static readonly Color msOrigHover = fColour(UWClassicLayout.Conversation.PlayerText);

    private static Color32 fColour(UWColor32 pOColour)
    {
        return new Color32(pOColour.R, pOColour.G, pOColour.B, pOColour.A);
    }

    /// <summary>"#RRGGBB" for rich text.</summary>
    private static string fHex(UWColor32 pOColour)
    {
        return "#" + pOColour.R.ToString("X2") + pOColour.G.ToString("X2") + pOColour.B.ToString("X2");
    }

    private Texture2D mOOrigHeader;

    private Texture2D mOOrigParchment;

    private Texture2D mOOrigAnswers;

    /// <summary>Whether the pieces were built with their outline; they are a pixel larger each way
    /// (UWHudArt.PadAndOutline), so they lie a pixel up and left.</summary>
    private bool mbOrigOutline;

    private int miOrigAnswersRows = -1;

    /// <summary>The answers' one text size in the original look, measured on the worst case, and
    /// what it was measured for (text size, width, room, scale, face).</summary>
    private int miOrigAnswerSize;

    private string msOrigWorstKey;

    /// <summary>Whether the history text was last built in the original's colours.</summary>
    private bool mbHistoryOriginal;

    private UWGameUI mOUi;

    private UWControlScheme mOScheme;

    private Canvas mOCanvas;

    private Image mODim;

    // --- The top block
    private RawImage mOTop;

    private readonly RawImage[] mOFrames = new RawImage[2];

    private readonly RawImage[] mOPortraits = new RawImage[2];

    private readonly Text[] mONames = new Text[2];

    private readonly RawImage[] mOTradeBacks = new RawImage[2];

    private readonly Text[] mOTradeTitles = new Text[2];

    /// <summary>The partner's four slots, then the player's four.</summary>
    private readonly RawImage[] mOCircles = new RawImage[2 * UWConversationTrade.SlotCount];

    private readonly RawImage[] mOIcons = new RawImage[2 * UWConversationTrade.SlotCount];

    private readonly Text[] mOCounts = new Text[2 * UWConversationTrade.SlotCount];

    private readonly RawImage[] mORings = new RawImage[2 * UWConversationTrade.SlotCount];

    private readonly Rect[] mOSlotRects = new Rect[2 * UWConversationTrade.SlotCount];

    // --- The history
    private RawImage mOHistoryBack;

    private RectTransform mOViewport;

    private Text mOHistory;

    private Image mOScrollTrack;

    private Image mOScrollThumb;

    private Rect mOHistoryRect;

    /// <summary>The walk stick's up-and-down glyph at the history's bottom right while the pad's
    /// pointer is on it (fPlaceScrollHint).</summary>
    private RawImage mOScrollHint;

    /// <summary>Where the history's text stands on the screen (the glyph sits on its bottom line).</summary>
    private Rect mOHistoryTextRect;

    /// <summary>The glyph's right edge on the screen: left of the modern scroll bar, centred in the
    /// original parchment's margin (fPlaceScrollHint).</summary>
    private float mfScrollHintRight;

    /// <summary>How far left of the middle of the original parchment's right margin the glyph
    /// stands, in original pixels (per user's screenshot, 2026-10-20: centred there it sat on the
    /// parchment's edge).</summary>
    private const int ScrollHintShift = 8;

    /// <summary>Where the history lies on the screen (the wheel scrolls it there), empty while the
    /// conversation is not shown.</summary>
    public Rect HistoryScreenRect => mbActive ? mOHistoryRect : default;

    private float mfScroll;

    private int miLinesShown = -1;

    private int miHistoryFontSize = -1;

    /// <summary>The conversation (its session) or the preview last drawn - a new one starts at the
    /// newest line with its portraits.</summary>
    private object mOSessionShown;

    /// <summary>The layout editor's preview is on (UWModernLayoutEditor's Dialog button).</summary>
    public static bool Previewing { get; set; }

    private static readonly object msPreviewKey = new object();

    /// <summary>What is drawn (UWConversationView, the controller's or the editor's preview) keyed
    /// by its conversation: the running session, or one key for the preview.</summary>
    private static object fKeyOf(UWConversationView pOView)
    {
        return pOView.Session != null ? pOView.Session : msPreviewKey;
    }

    private static readonly UWConversationSession.Line[] msPreviewLines =
    {
        new UWConversationSession.Line { IsNpc = true, Text = "Ah, the bold one returns. What brings thee to my forge this time?" },
        new UWConversationSession.Line { IsPlayer = true, Text = "This is in need of repair. Can thou repair it?" },
        new UWConversationSession.Line { IsNpc = true, Text = "Let me have a look at it... Aye, that I can do, but my work is not free." },
        new UWConversationSession.Line { Text = "Shak turns the blade over in his hands." }
    };

    /// <summary>
    /// The preview's answers: placeholder text as long as the original's longest ones, so the
    /// block can be sized for the worst case (per user, 2026-10-10; Lorem ipsum, not the real
    /// lines, to spoil nothing). From the menu lists built before every babl_menu / babl_fmenu
    /// call: the longest answer has 155 characters (conv 4), the longest of four in one menu 58,
    /// 52, 52 and 26 (conv 3). Here the longest answer with the three next of that four-answer
    /// menu - a bit more than any real menu, so whatever fits here fits in the game.
    /// </summary>
    private static readonly string[] msPreviewChoices =
    {
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minima veniam, quis.",
        "Duis aute irure dolor in reprehenderit in voluptate velit.",
        "Sed ut perspiciatis unde omnis iste natus error sit.",
        "Excepteur sint occaecat cupidatat non proident, sed."
    };

    /// <summary>The preview: Shak at his forge (CHARHEAD 1), the player's own portrait and name.</summary>
    private UWConversationView fPreviewView()
    {
        UWPlayerData lOPlayer = mOUi.mOUWData.InitialPlayer;
        string lsPlayer = lOPlayer != null && !string.IsNullOrEmpty(lOPlayer.Name) ? lOPlayer.Name : "Avatar";
        int liPlayerPortrait = UWConversationLayout.PlayerPortrait(lOPlayer);

        // Made anew only when the player changed (a loaded game) - not every frame.
        if (mOPreview == null || mOPreview.PlayerName != lsPlayer || mOPreview.PlayerPortraitIndex != liPlayerPortrait)
        {
            mOPreview = UWConversationView.Preview(msPreviewLines, msPreviewChoices, "Shak", lsPlayer,
                UWTexture.TextureTypes.CHARHEAD, 1, liPlayerPortrait);
        }

        return mOPreview;
    }

    private UWConversationView mOPreview;

    /// <summary>The worst case the original look's answers are measured on: the preview's answers
    /// alone.</summary>
    private static readonly UWConversationView msWorstView = UWConversationView.Preview(null, msPreviewChoices, null, null,
        UWTexture.TextureTypes.CHARHEAD, 1, 0);

    private bool fIsPreviewing()
    {
        return Previewing && UWModernLayout.IsEditing && !UWConversationController.IsAnyOpen
            && mOScheme != null && UWModernUi.IsActive;
    }

    // --- The answers
    private RawImage mOAnswersBack;

    private readonly Image[] mOHighlights = new Image[MaxRows];

    private readonly Text[] mORows = new Text[MaxRows];

    private readonly Rect[] mORowRects = new Rect[MaxRows];

    private int miRowsShown;

    private Image mOInputBack;

    // --- Art
    /// <summary>The panels' leathers by width, height and variant, each built for the backs' and
    /// colours' version (UWArtCache; a string key per call until 2026-10-19).</summary>
    private readonly UWArtCache<(int, int, int)> mOLeather = new UWArtCache<(int, int, int)>();

    private readonly Texture2D[] mOPortraitTextures = new Texture2D[2];

    private Texture2D mOSlotTexture;

    private Texture2D mORingTexture;

    private int miArtVersion = -1;

    // --- What the conversation opened and freed, to give back
    private bool mbActive;

    private bool mbFreedPointer;

    private bool mbOpenedBags;

    private bool mbPanelWasOpen;

    private UWModernPanel.TabEnum mePanelTab;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        fForgetArt();
    }

    private void Start()
    {
        mOUi = GetComponent<UWGameUI>();
        mOScheme = GetComponentInParent<UWControlScheme>();
    }

    /// <summary>The answers' screen rectangles in their order, for the gamepad's stepping
    /// (UWConversationController); empty while none stand there.</summary>
    public void GetChoiceRects(List<Rect> pORects)
    {
        pORects.Clear();

        if (!mbActive)
            return;

        for (int liRow = 0; liRow < miRowsShown; liRow++)
            pORects.Add(mORowRects[liRow]);
    }

    /// <summary>The answer under a screen point, 1 for the first; 0 for none (or while a look text
    /// or a typed answer stands there).</summary>
    public int ChoiceAt(Vector2 pOPointer)
    {
        if (!mbActive)
            return 0;

        for (int liRow = 0; liRow < miRowsShown; liRow++)
        {
            if (mORowRects[liRow].Contains(pOPointer))
                return liRow + 1;
        }

        return 0;
    }

    /// <summary>The trade slot under a screen point - the partner's (pbNpc) or the player's.</summary>
    public bool TryGetTradeSlotAt(Vector2 pOPointer, out bool pbNpc, out int piSlot)
    {
        pbNpc = false;
        piSlot = -1;

        if (!mbActive)
            return false;

        for (int liAt = 0; liAt < mOSlotRects.Length; liAt++)
        {
            if (!mOSlotRects[liAt].Contains(pOPointer))
                continue;

            pbNpc = liAt < UWConversationTrade.SlotCount;
            piSlot = liAt % UWConversationTrade.SlotCount;
            return true;
        }

        return false;
    }

    private static UWConversationController fScreen()
    {
        UWConversationController lOScreen = UWScene.ConversationController;

        return lOScreen != null && lOScreen.IsOpen && lOScreen.Session != null ? lOScreen : null;
    }

    // ------------------------------------------------- Opening and closing

    private void Update()
    {
        bool lbTalking = UWModernUi.IsTalking && fScreen() != null;

        if (lbTalking && !mbActive)
            fBegin();
        else if (!lbTalking && mbActive)
            fEnd();

        if (!mbActive || !UWPointer.HasMouse)
            return;

        // The wheel over the history scrolls it (older lines upwards).
        Vector2 lOPointer = UWPointer.Position;

        float lfWheel = UWPointer.Scroll.y;

        if (Mathf.Abs(lfWheel) > 0.01f && mOHistoryRect.Contains(lOPointer) && mOHistory != null)
            mfScroll += Mathf.Sign(lfWheel) * 3f * mOHistory.fontSize * 1.2f;
    }

    /// <summary>The bags open, the panel on its Character tab, the pointer free - remembered, to
    /// give back at the end.</summary>
    private void fBegin()
    {
        mbActive = true;
        mOSessionShown = null;

        mbFreedPointer = !UWModernPointer.IsFree;

        if (mbFreedPointer)
            UWModernPointer.Free();

        UWModernBags lOBags = UWModernBags.Instance;

        mbOpenedBags = lOBags != null && !lOBags.IsOpen;

        if (mbOpenedBags)
            lOBags.Open();

        UWModernPanel lOPanel = UWModernPanel.Instance;

        if (lOPanel != null)
        {
            mbPanelWasOpen = lOPanel.IsOpen;
            mePanelTab = lOPanel.Tab;

            if (!lOPanel.IsOpen || lOPanel.Tab != UWModernPanel.TabEnum.Character)
                lOPanel.Open(UWModernPanel.TabEnum.Character);
        }
    }

    private void fEnd()
    {
        mbActive = false;
        mOSessionShown = null;
        miRowsShown = 0;

        for (int liAt = 0; liAt < mOSlotRects.Length; liAt++)
            mOSlotRects[liAt] = Rect.zero;

        UWModernBags lOBags = UWModernBags.Instance;

        if (mbOpenedBags && lOBags != null && lOBags.IsOpen)
            lOBags.Close();

        UWModernPanel lOPanel = UWModernPanel.Instance;

        if (lOPanel != null)
        {
            if (!mbPanelWasOpen && !UWModernPanel.Pinned)
                lOPanel.Close();
            else if (mePanelTab != UWModernPanel.TabEnum.Character)
                lOPanel.Open(mePanelTab);
        }

        // Locked again only where the conversation freed it - the screen fades meanwhile and shows
        // the pointer until it is back (UWControlScheme.IsConversationOpen).
        if (mbFreedPointer)
            UWModernPointer.Lock();

        mbFreedPointer = false;
        mbOpenedBags = false;
    }

    private void OnDisable()
    {
        if (mbActive)
            fEnd();
    }

    // ------------------------------------------------- Drawing

    private void LateUpdate()
    {
        UWConversationController lOScreen = mbActive ? fScreen() : null;
        UWConversationView lOView = mOUi == null || mOUi.mOUWData == null ? null
            : lOScreen != null ? lOScreen.View : fIsPreviewing() ? fPreviewView() : null;

        if (lOView == null)
        {
            if (mOCanvas != null)
                mOCanvas.enabled = false;

            return;
        }

        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;

        // The editor's preview over every other part (per user, 2026-10-10), the real
        // conversation under the panel and the bags.
        int liSorting = lOScreen == null ? UWUiSorting.ModernConversationPreview : UWUiSorting.ModernConversation;

        if (mOCanvas.sortingOrder != liSorting)
            mOCanvas.sortingOrder = liSorting;

        if (miArtVersion != UWModernBacks.ArtVersion)
        {
            fForgetArt();
            miArtVersion = UWModernBacks.ArtVersion;
        }

        if (fKeyOf(lOView) != mOSessionShown)
        {
            mOSessionShown = fKeyOf(lOView);
            miLinesShown = -1;
            mfScroll = 0f;
            fShowPortraits(lOView);
        }

        // The world dims only for a real conversation, not under the editor's preview.
        mODim.enabled = lOScreen != null;

        // Its own size (UWModernLayout) on top of the UI size; the margins stay the UI size's.
        float lfScale = UWModernUi.PixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.Conversation);
        // The gamepad's letter grid at the bottom: the answers stand above it (UWLetterGrid).
        float lfScreenRows = (Screen.height - UWLetterGrid.ReservedPixels) / lfScale;

        // The room left of the character panel and the bag windows.
        float lfRight = Screen.width;

        if (UWModernPanel.Instance != null && UWModernPanel.Instance.IsOpen)
            lfRight = Mathf.Min(lfRight, UWModernPanel.Instance.LeftEdge);

        if (UWModernBags.Instance != null)
            lfRight = Mathf.Min(lfRight, UWModernBags.Instance.LeftEdge);

        float lfLeft = Margin * UWModernUi.PixelScale;

        if (UWModernLayout.IsConversationOriginal)
        {
            fLayoutOriginal(lOView, lfLeft, lfRight - (Margin * UWModernUi.PixelScale), lfScale);
            fPlaceScrollHint(lOScreen != null, lfScale);
            return;
        }

        fShowModernParts(true);

        float lfAvailable = lfRight - (Margin * UWModernUi.PixelScale) - lfLeft;
        int liWidth = Mathf.Clamp(Mathf.FloorToInt(lfAvailable / lfScale), MinWidth, MaxWidth);
        float lfX = Mathf.Round(lfLeft + Mathf.Max(0f, (lfAvailable - (liWidth * lfScale)) * 0.5f));

        // Its own place (UWModernLayout): the block from the top margin to the bottom one.
        Rect lOBlock = UWModernLayout.Place(UWModernLayout.ElementEnum.Conversation,
            new Rect(lfX, Margin * lfScale, liWidth * lfScale, Screen.height - (2 * Margin * lfScale)));

        lfX = lOBlock.x;
        UWModernLayout.Report(UWModernLayout.ElementEnum.Conversation, lOBlock);

        UWUiKit.SetRect(mODim.rectTransform, 0f, 0f, Screen.width, Screen.height);

        // The answers first: their height decides the history's.
        float lfAnswersHeight = fLayoutAnswerTexts(lOView, liWidth, lfScale);
        int liAnswersRows = Mathf.CeilToInt(lfAnswersHeight / lfScale) + 10;
        int liHistoryRows = Mathf.Max(MinHistoryHeight,
            Mathf.FloorToInt(lfScreenRows) - (2 * Margin) - TopHeight - (2 * BlockGap) - liAnswersRows);

        // Screen pixels from the top.
        float lfTop = Margin * lfScale;

        fLayoutTop(lOView, lfX, lfTop, liWidth, lfScale);
        lfTop += (TopHeight + BlockGap) * lfScale;

        fLayoutHistory(lOView, lfX, lfTop, liWidth, liHistoryRows, lfScale);
        lfTop += (liHistoryRows + BlockGap) * lfScale;

        fLayoutAnswers(lfX, lfTop, liWidth, liAnswersRows, lfScale);
        fPlaceScrollHint(lOScreen != null, lfScale);
    }

    /// <summary>
    /// THE STICK'S HINT ON THE HISTORY (per user, 2026-10-20, like a mouse-over): while the pad is
    /// in use and its pointer stands on the history, the walk stick scrolls it instead of stepping
    /// the answers (UWPadEntryStepper) - its up-and-down glyph at the history's bottom right says
    /// so. Not under the editor's preview, not with the mouse.
    /// </summary>
    private void fPlaceScrollHint(bool pbReal, float pfScale)
    {
        Vector2 lOPointer = UWPointer.HasMouse ? UWPointer.Position : Vector2.negativeInfinity;
        bool lbShown = pbReal && UWGamepad.IsActive && mOHistoryRect.Contains(lOPointer);

        mOScrollHint.enabled = lbShown;

        if (!lbShown)
            return;

        // The swapped sticks swap the walk stick (UWPadEntryStepper reads it the same way).
        mOScrollHint.texture = UWGlyphs.Tiles(UWUserSettings.GamepadSwapSticks ? UWGlyphs.RightStickVerticalTile : UWGlyphs.LeftStickVerticalTile);

        // INSIDE AT THE BOTTOM RIGHT (per user, 2026-10-20, after trying it outside the roll and
        // larger): the pad hint strip's size, on the text's bottom line, its right edge just left
        // of the modern scroll bar (in the original look ScrollHintShift left of its margin's middle) - off the
        // frame, and it covers at most the very end of a last line that runs full width.
        float lfGlyph = Mathf.Round(UWModernUi.FontSize * 1.5f);

        UWUiKit.SetRect(mOScrollHint.rectTransform, Mathf.Round(mfScrollHintRight - lfGlyph), mOHistoryTextRect.yMin, lfGlyph, lfGlyph);
    }

    // --- The top: portraits, names, trade areas

    private void fLayoutTop(UWConversationView pOView, float pfX, float pfTop, int piWidth, float pfScale)
    {
        fSetLeather(mOTop, piWidth, TopHeight, 1);
        fPlace(mOTop.rectTransform, pfX, pfTop, piWidth, TopHeight, pfScale);

        string[] lsNames = { pOView.PartnerName ?? string.Empty, pOView.PlayerName ?? string.Empty };

        for (int liSide = 0; liSide < 2; liSide++)
        {
            Texture2D lOPortrait = mOPortraitTextures[liSide];
            // The portrait at the original's pixel proportion (UWModernHudArt.PixelAspectX), its
            // frame as narrow around it.
            int liInner = lOPortrait != null ? lOPortrait.width : 34;
            int liInnerHigh = lOPortrait != null ? lOPortrait.height : 34;
            int liFrame = Mathf.RoundToInt(liInner * UWModernHudArt.PixelAspectX) + (2 * FramePad);
            int liFrameHigh = liInnerHigh + (2 * FramePad);
            int liFrameX = liSide == 0 ? 6 : piWidth - 6 - liFrame;

            fSetLeather(mOFrames[liSide], liFrame, liFrameHigh);
            fPlace(mOFrames[liSide].rectTransform, pfX + (liFrameX * pfScale), pfTop + (12 * pfScale), liFrame, liFrameHigh, pfScale);

            mOPortraits[liSide].texture = lOPortrait;
            mOPortraits[liSide].enabled = lOPortrait != null;

            if (lOPortrait != null)
                fPlace(mOPortraits[liSide].rectTransform, pfX + ((liFrameX + FramePad) * pfScale), pfTop + ((12 + FramePad) * pfScale),
                    lOPortrait.width * UWModernHudArt.PixelAspectX, lOPortrait.height, pfScale);

            fLayoutName(mONames[liSide], lsNames[liSide], liSide, pfX, pfTop, piWidth, liFrameX + (liFrame * 0.5f), pfScale);
        }

        // The two trade areas between the portraits.
        UWConversationTrade lOTrade = pOView.Trade;
        int liTradeX = fTradeLeft(piWidth);
        string lsPartner = pOView.PartnerName ?? string.Empty;
        string[] lsTitles =
        {
            lsPartner.Length > 0 && lsPartner.Length <= 9 ? UWText.F("hud.trade.partner_goods", UWRichText.Escape(lsPartner)) : UWText.T("hud.trade.goods"),
            UWText.T("hud.trade.your_offer")
        };

        for (int liSide = 0; liSide < 2; liSide++)
        {
            float lfBoxX = pfX + ((liTradeX + (liSide * (TradeWidth + TradeGap))) * pfScale);
            float lfBoxTop = pfTop + (4 * pfScale);

            fSetLeather(mOTradeBacks[liSide], TradeWidth, TradeHeight);
            fPlace(mOTradeBacks[liSide].rectTransform, lfBoxX, lfBoxTop, TradeWidth, TradeHeight, pfScale);

            mOTradeTitles[liSide].text = lsTitles[liSide];
            mOTradeTitles[liSide].fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * pfScale));
            fPlace(mOTradeTitles[liSide].rectTransform, lfBoxX - (6 * pfScale), lfBoxTop + (1.5f * pfScale), TradeWidth + 12, 8, pfScale);

            for (int liSlot = 0; liSlot < UWConversationTrade.SlotCount; liSlot++)
            {
                int liAt = (liSide * UWConversationTrade.SlotCount) + liSlot;
                float lfSlotX = lfBoxX + ((6 + ((liSlot % 2) * SlotPitch)) * pfScale);
                float lfSlotTop = lfBoxTop + ((11 + ((liSlot / 2) * SlotPitch)) * pfScale);
                UWObject lOItem = lOTrade == null ? null : liSide == 0 ? lOTrade.NpcItems[liSlot] : lOTrade.PlayerItems[liSlot];
                bool lbMarked = lOTrade != null && (liSide == 0 ? lOTrade.NpcSelected[liSlot] : lOTrade.PlayerSelected[liSlot]);

                mOCircles[liAt].texture = fSlotTexture();
                fPlace(mOCircles[liAt].rectTransform, lfSlotX, lfSlotTop, UWModernHudArt.SlotSize, UWModernHudArt.SlotSize, pfScale);
                mOSlotRects[liAt] = fScreenRect(lfSlotX, lfSlotTop, UWModernHudArt.SlotSize * pfScale, UWModernHudArt.SlotSize * pfScale);

                fShowItem(liAt, lOItem, pfScale);

                // The mark lies on the slot's own circle (per user, 2026-10-04: one size larger it
                // did not meet it).
                mORings[liAt].enabled = lbMarked && lOItem != null;
                mORings[liAt].texture = fRingTexture();
                UWUiKit.SetRect(mORings[liAt].rectTransform, 0f, 0f, UWModernHudArt.SlotSize * pfScale, UWModernHudArt.SlotSize * pfScale);
            }
        }
    }

    /// <summary>The thing's picture centred in its circle (it is a child of the circle, as in
    /// the bags), with the count of a stack.</summary>
    /// <summary>Where the partner's trade area begins, original pixels from the block's left.</summary>
    private static int fTradeLeft(int piWidth)
    {
        return (piWidth - ((2 * TradeWidth) + TradeGap)) / 2;
    }

    /// <summary>
    /// A name over its portrait, centred there while it fits; a longer one takes the whole room
    /// between the block's edge and the trade area, and one longer still is drawn smaller (per
    /// user, 2026-10-04: "Lakshi Longtooth" ran out over the block's edge).
    /// </summary>
    private void fLayoutName(Text pOText, string psName, int piSide, float pfX, float pfTop, int piWidth, float pfCentre, float pfScale)
    {
        int liFontSize = Mathf.Max(10, Mathf.RoundToInt(5.4f * pfScale));

        pOText.text = UWRichText.Escape(psName);
        pOText.color = piSide == 0 ? msNpc : (Color)new Color32(236, 186, 110, 255);
        pOText.fontSize = liFontSize;
        pOText.resizeTextForBestFit = false;
        pOText.horizontalOverflow = HorizontalWrapMode.Overflow;

        // The room on the name's side: from the block's edge (its frame) to the trade area.
        float lfEdge = 3f;
        float lfInner = fTradeLeft(piWidth) - 2f;
        float lfLeft = piSide == 0 ? lfEdge : piWidth - lfInner;
        float lfRight = piSide == 0 ? lfInner : piWidth - lfEdge;
        float lfCentreRoom = Mathf.Min(pfCentre - lfLeft, lfRight - pfCentre);
        float lfWidth = pOText.preferredWidth / pfScale;

        if (lfWidth <= 2f * lfCentreRoom)
        {
            fPlace(pOText.rectTransform, pfX + ((pfCentre - 40f) * pfScale), pfTop + pfScale, 80, 10, pfScale);
            return;
        }

        pOText.resizeTextForBestFit = true;
        pOText.resizeTextMinSize = Mathf.Max(8, liFontSize / 2);
        pOText.resizeTextMaxSize = liFontSize;
        pOText.horizontalOverflow = HorizontalWrapMode.Wrap;
        fPlace(pOText.rectTransform, pfX + (lfLeft * pfScale), pfTop + pfScale, lfRight - lfLeft, 10, pfScale);
    }

    private void fShowItem(int piAt, UWObject pOItem, float pfScale)
    {
        UWTexture lOSource = pOItem != null ? pOItem.Icon ?? pOItem.Texture : null;

        if (lOSource == null)
        {
            mOIcons[piAt].enabled = false;
            mOCounts[piAt].enabled = false;
            return;
        }

        Texture2D lOIcon = fIcon(lOSource);

        mOIcons[piAt].texture = lOIcon;
        mOIcons[piAt].enabled = true;
        UWUiKit.SetRect(mOIcons[piAt].rectTransform, (UWModernHudArt.SlotSize - (lOIcon.width * UWModernHudArt.PixelAspectX)) * 0.5f * pfScale,
            (UWModernHudArt.SlotSize - lOIcon.height) * 0.5f * pfScale, lOIcon.width * UWModernHudArt.PixelAspectX * pfScale, lOIcon.height * pfScale);

        int liCount = UWStackRules.CountAtLeastOne(pOItem);

        mOCounts[piAt].enabled = liCount > 1;

        if (liCount > 1)
        {
            mOCounts[piAt].text = liCount.ToString();
            mOCounts[piAt].fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * pfScale));
            UWUiKit.SetRect(mOCounts[piAt].rectTransform, 0f, -pfScale, (UWModernHudArt.SlotSize + 1) * pfScale,
                UWModernHudArt.SlotSize * 0.6f * pfScale);
        }
    }

    // --- The history

    private void fLayoutHistory(UWConversationView pOView, float pfX, float pfTop, int piWidth, int piHeight, float pfScale)
    {
        fSetLeather(mOHistoryBack, piWidth, piHeight, 2);
        fPlace(mOHistoryBack.rectTransform, pfX, pfTop, piWidth, piHeight, pfScale);

        float lfViewWidth = (piWidth - (2 * Pad) - 4) * pfScale;
        float lfViewHeight = (piHeight - HistoryTopPad - 6) * pfScale;
        float lfViewX = pfX + (Pad * pfScale);
        float lfViewTop = pfTop + (HistoryTopPad * pfScale);

        UWUiKit.SetRect(mOViewport, lfViewX, Screen.height - lfViewTop - lfViewHeight, lfViewWidth, lfViewHeight);
        mOHistoryRect = fScreenRect(pfX, pfTop, piWidth * pfScale, piHeight * pfScale);
        mOHistoryTextRect = new Rect(lfViewX, Screen.height - lfViewTop - lfViewHeight, lfViewWidth, lfViewHeight);
        mfScrollHintRight = pfX + ((piWidth - Pad + 1) * pfScale);

        int liFontSize = Mathf.Max(10, Mathf.RoundToInt(TextSize * pfScale));

        fFillHistory(pOView, liFontSize, false);

        mOHistory.rectTransform.sizeDelta = new Vector2(lfViewWidth, 10f);

        float lfContent = mOHistory.preferredHeight;
        float lfMaxScroll = Mathf.Max(0f, lfContent - lfViewHeight);

        mfScroll = Mathf.Clamp(mfScroll, 0f, lfMaxScroll);

        float lfY = lfContent <= lfViewHeight ? lfViewHeight - lfContent : -mfScroll;

        mOHistory.rectTransform.anchoredPosition = new Vector2(0f, lfY);
        mOHistory.rectTransform.sizeDelta = new Vector2(lfViewWidth, lfContent);

        // The scroll bar, only when there is more than fits.
        bool lbBar = lfMaxScroll > 0f;

        mOScrollTrack.enabled = lbBar;
        mOScrollThumb.enabled = lbBar;

        if (lbBar)
        {
            float lfBarX = pfX + ((piWidth - Pad + 2) * pfScale);
            float lfBarWidth = Mathf.Max(2f, pfScale);
            float lfThumb = Mathf.Max(6f * pfScale, lfViewHeight * (lfViewHeight / lfContent));
            float lfViewBottom = Screen.height - lfViewTop - lfViewHeight;
            float lfThumbY = lfViewBottom + ((lfViewHeight - lfThumb) * (mfScroll / lfMaxScroll));

            UWUiKit.SetRect(mOScrollTrack.rectTransform, lfBarX, lfViewBottom, lfBarWidth, lfViewHeight);
            UWUiKit.SetRect(mOScrollThumb.rectTransform, lfBarX, lfThumbY, lfBarWidth, lfThumb);
        }
    }

    /// <summary>The history's text anew when lines came, the size or the look changed.</summary>
    private void fFillHistory(UWConversationView pOView, int piFontSize, bool pbOriginal)
    {
        if (pOView.Lines.Count == miLinesShown && piFontSize == miHistoryFontSize && pbOriginal == mbHistoryOriginal)
            return;

        // New lines: back to the newest.
        if (pOView.Lines.Count != miLinesShown)
            mfScroll = 0f;

        miLinesShown = pOView.Lines.Count;
        miHistoryFontSize = piFontSize;
        mbHistoryOriginal = pbOriginal;
        mOHistory.fontSize = piFontSize;
        mOHistory.text = fHistoryText(pOView.Lines, piFontSize, pbOriginal);
    }

    /// <summary>The history as rich text: each speaker in its colour, the player's answers
    /// indented, a little room between the entries; the original's page marks go
    /// (UWConversationLayout.WithoutPageBreaks), and what rich text would read as a tag is escaped
    /// (UWRichText.Escape, B31).</summary>
    private static string fHistoryText(IReadOnlyList<UWConversationSession.Line> pOLines, int piFontSize, bool pbOriginal = false)
    {
        StringBuilder lOText = new StringBuilder();
        string lsGap = "\n<size=" + Mathf.Max(2, piFontSize / 3) + "> </size>\n";

        for (int liLine = 0; liLine < pOLines.Count; liLine++)
        {
            UWConversationSession.Line lOLine = pOLines[liLine];
            string lsText = UWRichText.Escape(UWConversationLayout.WithoutPageBreaks(lOLine.Text));

            if (lsText.Length == 0)
                continue;

            if (lOText.Length > 0)
                lOText.Append(lsGap);

            string lsHex = pbOriginal
                ? (lOLine.IsNpc ? OrigNpcHex : lOLine.IsPlayer ? OrigPlayerHex : OrigNarratorHex)
                : (lOLine.IsNpc ? NpcHex : lOLine.IsPlayer ? PlayerHex : NarratorHex);

            if (lOLine.IsPlayer)
                lsText = "    " + lsText.Replace("\n", "\n    ");

            lOText.Append("<color=").Append(lsHex).Append('>').Append(lsText).Append("</color>");
        }

        return lOText.ToString();
    }

    // --- The answers

    /// <summary>Fills the rows - the answers, a typed answer or a look text - and returns their
    /// height in screen pixels.</summary>
    private float fLayoutAnswerTexts(UWConversationView pOView, int piWidth, float pfScale, bool pbOriginal = false, float pfRowWidth = -1f,
        int piFontSize = -1)
    {
        float lfRowWidth = pfRowWidth > 0f ? pfRowWidth : (piWidth - (2 * Pad) - 4) * pfScale;
        string lsNumber = pbOriginal ? OrigNpcHex : NumberHex;
        string lsLookHex = pbOriginal ? OrigNpcHex : NpcHex;
        int liFontSize = piFontSize > 0 ? piFontSize : Mathf.Max(10, Mathf.RoundToInt(TextSize * pfScale));
        string lsLook = pOView.LookText;
        IReadOnlyList<string> lOChoices = pOView.Choices;
        List<string> lOTexts = new List<string>();

        mOInputBack.enabled = false;
        miRowsShown = 0;

        if (lsLook != null)
        {
            lOTexts.Add("<color=" + lsLookHex + ">" + UWRichText.Escape(lsLook) + "</color>");
        }
        else if (pOView.IsTyping)
        {
            // At the one rate of both schemes (decision 17, 2026-10-20; until then the first half
            // of every second).
            bool lbCaret = UWMessageLogModel.IsCursorOn(Time.unscaledTime);

            lOTexts.Add("<color=" + lsNumber + ">></color>  " + UWRichText.Escape(pOView.TypedAnswer) + (lbCaret ? "_" : " "));
            mOInputBack.enabled = true;
        }
        else
        {
            for (int liChoice = 0; liChoice < lOChoices.Count && liChoice < MaxRows; liChoice++)
                lOTexts.Add("<color=" + lsNumber + ">" + (liChoice + 1) + ".</color>  " + UWRichText.Escape(lOChoices[liChoice]));

            miRowsShown = lOTexts.Count;
        }

        float lfHeight = 0f;

        for (int liRow = 0; liRow < MaxRows; liRow++)
        {
            bool lbUsed = liRow < lOTexts.Count;

            mORows[liRow].enabled = lbUsed;
            mOHighlights[liRow].enabled = false;

            if (!lbUsed)
            {
                mORowRects[liRow] = Rect.zero;
                continue;
            }

            mORows[liRow].text = lOTexts[liRow];
            mORows[liRow].fontSize = liFontSize;
            UWUiFonts.ApplyNow(mORows[liRow]);
            mORows[liRow].rectTransform.sizeDelta = new Vector2(lfRowWidth, 10f);

            float lfRow = Mathf.Ceil(mORows[liRow].preferredHeight);

            mORows[liRow].rectTransform.sizeDelta = new Vector2(lfRowWidth, lfRow);
            lfHeight += lfRow + (liRow > 0 ? RowGap * pfScale : 0f);
        }

        // At least one row's room, so the block keeps its place while the partner speaks.
        return Mathf.Max(lfHeight, liFontSize * 1.3f);
    }

    private void fLayoutAnswers(float pfX, float pfTop, int piWidth, int piHeight, float pfScale)
    {
        fSetLeather(mOAnswersBack, piWidth, piHeight, 3);
        fPlace(mOAnswersBack.rectTransform, pfX, pfTop, piWidth, piHeight, pfScale);

        Vector2 lOPointer = UWPointer.HasMouse ? UWPointer.Position : new Vector2(-1f, -1f);
        float lfRowX = pfX + (Pad * pfScale);
        float lfY = Screen.height - pfTop - (5 * pfScale);

        for (int liRow = 0; liRow < MaxRows; liRow++)
        {
            if (!mORows[liRow].enabled)
                continue;

            RectTransform lORect = mORows[liRow].rectTransform;
            float lfRow = lORect.sizeDelta.y;

            lfY -= lfRow;
            lORect.anchoredPosition = new Vector2(lfRowX, lfY);

            Rect lOHit = new Rect(pfX + (4 * pfScale), lfY - pfScale, (piWidth - 9) * pfScale, lfRow + (2 * pfScale));
            bool lbChoice = liRow < miRowsShown;
            bool lbHover = lbChoice && lOHit.Contains(lOPointer);

            mORowRects[liRow] = lbChoice ? lOHit : Rect.zero;
            mOHighlights[liRow].enabled = lbHover;
            mORows[liRow].color = lbHover ? UWModernPalette.LightText : msNpc;

            if (lbHover)
                UWUiKit.SetRect(mOHighlights[liRow].rectTransform, lOHit.x, lOHit.y, lOHit.width, lOHit.height);

            if (mOInputBack.enabled && liRow == 0)
                UWUiKit.SetRect(mOInputBack.rectTransform, lOHit.x, lOHit.y, lOHit.width, lOHit.height);

            lfY -= RowGap * pfScale;
        }
    }

    // ------------------------------------------------- Art

    private void fShowPortraits(UWConversationView pOView)
    {
        for (int liSide = 0; liSide < 2; liSide++)
        {
            UWTexture lOSource = pOView.PortraitTexture(mOUi.mOUWData.Textures, liSide == 1);

            mOPortraitTextures[liSide] = UWIconCache.Texture(lOSource, mOUi.TextureFilterMode);
        }
    }

    /// <summary>A panel's leather; piVariant keeps the three large panels' clouds apart.</summary>
    private void fSetLeather(RawImage pOImage, int piWidth, int piHeight, int piVariant = 0)
    {
        (int, int, int) lKey = (piWidth, piHeight, piVariant);
        UWArtStamp lOStamp = UWVersionedTexture.Backs();

        if (!mOLeather.TryGet(lKey, lOStamp, out Texture2D lOTexture))
        {
            lOTexture = mOLeather.Set(lKey, lOStamp, UWModernHudArt.BuildPartLeather(UWModernLayout.ElementEnum.Conversation, mOUi.mOUWData.Textures,
                piWidth, piHeight, mOUi.TextureFilterMode, piVariant));
        }

        pOImage.texture = lOTexture;
    }

    private Texture2D fIcon(UWTexture pOSource)
    {
        return UWIconCache.Texture(pOSource, mOUi.TextureFilterMode);
    }

    private Texture2D fSlotTexture()
    {
        if (mOSlotTexture == null)
            mOSlotTexture = UWModernHudArt.BuildPartSlot(UWModernLayout.ElementEnum.Conversation, mOUi.mOUWData.Textures, mOUi.TextureFilterMode);

        return mOSlotTexture;
    }

    private Texture2D fRingTexture()
    {
        if (mORingTexture == null)
            mORingTexture = UWModernActionBar.BuildRing(128, 63f, 8f);

        return mORingTexture;
    }

    /// <summary>Drops the textures built from the palette - a new colour vision builds them anew.
    /// The portraits stay: they go through the palette shader.</summary>
    private void fForgetArt()
    {
        mOLeather.ReleaseAll();

        if (mOSlotTexture != null)
            Destroy(mOSlotTexture);

        mOSlotTexture = null;

        foreach (Texture2D lOTexture in new[] { mOOrigHeader, mOOrigParchment, mOOrigAnswers })
        {
            if (lOTexture != null)
                Destroy(lOTexture);
        }

        mOOrigHeader = null;
        mOOrigParchment = null;
        mOOrigAnswers = null;
        miOrigAnswersRows = -1;
    }

    // ------------------------------------------------- The original's look

    /// <summary>The modern look's own parts on or off (the original's look leaves them out).</summary>
    private void fShowModernParts(bool pbShown)
    {
        for (int liSide = 0; liSide < 2; liSide++)
        {
            mOFrames[liSide].enabled = pbShown;
            mOTradeBacks[liSide].enabled = pbShown;
            mOTradeTitles[liSide].enabled = pbShown;
            mONames[liSide].alignment = pbShown ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
        }

        foreach (RawImage lOCircle in mOCircles)
            lOCircle.color = pbShown ? Color.white : Color.clear;

        // The texts on the parchment flat, as in the original: the modern dark outline smeared the
        // dark letters on it (per user, 2026-10-10: "the normal text can hardly be read").
        mOHistory.GetComponent<Outline>().enabled = pbShown;

        foreach (Text lOText in mORows)
            lOText.GetComponent<Outline>().enabled = pbShown;

        if (pbShown)
            mOInputBack.color = UWModernPalette.InputFill;

        if (!pbShown)
        {
            mOScrollTrack.enabled = false;
            mOScrollThumb.enabled = false;
        }
    }

    /// <summary>A part of the original's look at its place in the block (original pixels from the
    /// block's top left; x and widths at the original's proportion).</summary>
    private void fPlaceOrig(RectTransform pORect, Rect pOBlock, float pfX, float pfTop, float pfWidth, float pfHeight, float pfScale)
    {
        float lfAspect = pfScale * UWModernHudArt.PixelAspectX;

        UWUiKit.SetRect(pORect, pOBlock.x + (pfX * lfAspect), pOBlock.yMax - ((pfTop + pfHeight) * pfScale), pfWidth * lfAspect, pfHeight * pfScale);
    }

    private Rect fOrigRect(Rect pOBlock, float pfX, float pfTop, float pfWidth, float pfHeight, float pfScale)
    {
        float lfAspect = pfScale * UWModernHudArt.PixelAspectX;

        return new Rect(pOBlock.x + (pfX * lfAspect), pOBlock.yMax - ((pfTop + pfHeight) * pfScale), pfWidth * lfAspect, pfHeight * pfScale);
    }

    /// <summary>
    /// THE CONVERSATION IN THE ORIGINAL'S LOOK: CONV.BYT's top (UWHudArt.BuildConversationHeader)
    /// with the names on the plates, the portraits in their frames and the traded things in the
    /// two areas' circles at the original's places; the parchment under it with the history (the
    /// original's colours, scrolling with the wheel, no [MORE]); a lower parchment of a fixed size
    /// under that with the answers. The block where the layout editor puts it - by default
    /// centred in the room left of the panels, at the top.
    /// </summary>
    private void fLayoutOriginal(UWConversationView pOView, float pfLeft, float pfRight, float pfScale)
    {
        fShowModernParts(false);

        UWDataImport.DataImport lOData = mOUi.mOUWData;
        bool lbOutline = UWModernClassicPlus.ConversationOutline;

        if (lbOutline != mbOrigOutline)
        {
            foreach (Texture2D lOOld in new[] { mOOrigHeader, mOOrigParchment, mOOrigAnswers })
            {
                if (lOOld != null)
                    Destroy(lOOld);
            }

            mOOrigHeader = null;
            mOOrigParchment = null;
            mOOrigAnswers = null;
            miOrigAnswersRows = -1;
            mbOrigOutline = lbOutline;
        }

        if (mOOrigHeader == null)
            mOOrigHeader = UWModernHudArt.BuildConversationHeader(lOData.Textures, mOUi.TextureFilterMode, lbOutline);

        if (mOOrigParchment == null)
            mOOrigParchment = UWModernHudArt.BuildConversationParchment(lOData.Textures, OrigParchmentRows, mOUi.TextureFilterMode, false, lbOutline);

        float lfAspect = pfScale * UWModernHudArt.PixelAspectX;
        float lfRowWidth = OrigTextWidth * lfAspect;

        // THE BLOCK KEEPS ITS SIZE (per user, 2026-10-10: "in this look one sets the size and it
        // stays"): the answers' parchment is always OrigAnswersRows high, and the answers' text
        // is made smaller, a step at a time, until they all fit in it - never larger than the
        // conversation's text size. ONE SIZE FOR EVERY MENU (per user, the same day: the size
        // changing with the answers offered was not liked; this look only, the modern one keeps
        // growing with its answers): measured on the worst case, the preview's answers
        // (msPreviewChoices, a bit more than any real menu), so every real menu fits at it. Only
        // what is longer still (a look text) is made smaller again.
        int liFontSize = Mathf.Max(10, Mathf.RoundToInt(TextSize * pfScale));
        int liAnswersRows = OrigAnswersRows;
        float lfRoom = (liAnswersRows - (OrigAnswerTextTop - OrigAnswersTop) - OrigAnswerBottom) * pfScale;
        string lsWorstKey = liFontSize + "/" + lfRowWidth + "/" + lfRoom + "/" + pfScale + "/" + UWUiFonts.Face;

        if (lsWorstKey != msOrigWorstKey)
        {
            UWConversationView lOWorst = msWorstView;

            miOrigAnswerSize = liFontSize;

            while (fLayoutAnswerTexts(lOWorst, OrigWidth, pfScale, true, lfRowWidth, miOrigAnswerSize) > lfRoom && miOrigAnswerSize > MinAnswerFont)
                miOrigAnswerSize--;

            msOrigWorstKey = lsWorstKey;
        }

        int liAnswerSize = miOrigAnswerSize;

        while (fLayoutAnswerTexts(pOView, OrigWidth, pfScale, true, lfRowWidth, liAnswerSize) > lfRoom && liAnswerSize > MinAnswerFont)
            liAnswerSize--;

        if (mOOrigAnswers == null || miOrigAnswersRows != liAnswersRows)
        {
            if (mOOrigAnswers != null)
                Destroy(mOOrigAnswers);

            // The message scroll's knob rollers below, where the original's answers stand (per user).
            mOOrigAnswers = UWModernHudArt.BuildConversationParchment(lOData.Textures, liAnswersRows, mOUi.TextureFilterMode, true, lbOutline);
            miOrigAnswersRows = liAnswersRows;
        }

        float lfWidth = OrigWidth * lfAspect;
        float lfHeight = (OrigAnswersTop + liAnswersRows) * pfScale;
        Rect lODefault = new Rect(pfLeft + Mathf.Max(0f, ((pfRight - pfLeft) - lfWidth) * 0.5f), Screen.height - (Margin * pfScale) - lfHeight,
            lfWidth, lfHeight);
        Rect lOBlock = UWModernLayout.Place(UWModernLayout.ElementEnum.Conversation, lODefault);

        UWModernLayout.Report(UWModernLayout.ElementEnum.Conversation, lOBlock);
        UWUiKit.SetRect(mODim.rectTransform, 0f, 0f, Screen.width, Screen.height);

        // The top: plates, portraits, trade areas.
        mOTop.texture = mOOrigHeader;
        // The pieces are a pixel larger each way (their outline's room): a pixel up and left.
        fPlaceOrig(mOTop.rectTransform, lOBlock, -1f, -1f, mOOrigHeader.width, mOOrigHeader.height, pfScale);

        string[] lsNames = { pOView.PartnerName ?? string.Empty, pOView.PlayerName ?? string.Empty };
        int liNameSize = Mathf.Max(8, Mathf.RoundToInt(6.8f * pfScale));

        for (int liSide = 0; liSide < 2; liSide++)
        {
            Texture2D lOPortrait = mOPortraitTextures[liSide];

            mOPortraits[liSide].texture = lOPortrait;
            mOPortraits[liSide].enabled = lOPortrait != null;

            if (lOPortrait != null)
                fPlaceOrig(mOPortraits[liSide].rectTransform, lOBlock, miOrigPortraitX[liSide], OrigPortraitTop, lOPortrait.width, lOPortrait.height, pfScale);

            Text lOName = mONames[liSide];

            lOName.text = UWRichText.Escape(lsNames[liSide]);
            lOName.color = msOrigName;
            lOName.fontSize = liNameSize;
            lOName.resizeTextForBestFit = false;
            lOName.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Names hold up to 29 characters since 2026-10-12: smaller until it fits its plate.
            while (lOName.fontSize > 8 && lOName.preferredWidth > OrigNameWidth * pfScale * UWModernHudArt.PixelAspectX)
                lOName.fontSize--;

            fPlaceOrig(lOName.rectTransform, lOBlock, miOrigNameX[liSide], 1f, OrigNameWidth, 7f, pfScale);
        }

        UWConversationTrade lOTrade = pOView.Trade;

        for (int liAt = 0; liAt < miOrigSlots.Length; liAt++)
        {
            int liSide = liAt / UWConversationTrade.SlotCount;
            int liSlot = liAt % UWConversationTrade.SlotCount;
            UWObject lOItem = lOTrade == null ? null : liSide == 0 ? lOTrade.NpcItems[liSlot] : lOTrade.PlayerItems[liSlot];
            bool lbMarked = lOTrade != null && (liSide == 0 ? lOTrade.NpcSelected[liSlot] : lOTrade.PlayerSelected[liSlot]);

            fPlaceOrig(mOCircles[liAt].rectTransform, lOBlock, miOrigSlots[liAt].x, miOrigSlots[liAt].y, OrigSlotSize, OrigSlotSize, pfScale);
            mOSlotRects[liAt] = fOrigRect(lOBlock, miOrigSlots[liAt].x, miOrigSlots[liAt].y, OrigSlotSize + 1, OrigSlotSize + 1, pfScale);

            fShowItem(liAt, lOItem, pfScale);

            mORings[liAt].enabled = lbMarked && lOItem != null;
            mORings[liAt].texture = fRingTexture();
            UWUiKit.SetRect(mORings[liAt].rectTransform, 0f, 0f, OrigSlotSize * lfAspect, OrigSlotSize * pfScale);
        }

        // The parchment with the history.
        mOHistoryBack.texture = mOOrigParchment;
        fPlaceOrig(mOHistoryBack.rectTransform, lOBlock, OrigParchmentX - 1, OrigParchmentTop - 1, mOOrigParchment.width, mOOrigParchment.height, pfScale);
        mOHistoryRect = fOrigRect(lOBlock, OrigParchmentX - 1, OrigParchmentTop - 1, mOOrigParchment.width, mOOrigParchment.height, pfScale);

        Rect lOView = fOrigRect(lOBlock, OrigTextX, OrigTextTop, OrigTextWidth, OrigTextRows, pfScale);

        UWUiKit.SetRect(mOViewport, lOView.x, lOView.y, lOView.width, lOView.height);
        mOHistoryTextRect = lOView;
        mfScrollHintRight = Mathf.Round(lOView.xMax + ((mOHistoryRect.xMax - lOView.xMax) * 0.5f) + (Mathf.Round(UWModernUi.FontSize * 1.5f) * 0.5f)
            - (ScrollHintShift * lfAspect));
        fFillHistory(pOView, liFontSize, true);

        mOHistory.rectTransform.sizeDelta = new Vector2(lOView.width, 10f);

        float lfContent = mOHistory.preferredHeight;
        float lfMaxScroll = Mathf.Max(0f, lfContent - lOView.height);

        mfScroll = Mathf.Clamp(mfScroll, 0f, lfMaxScroll);
        mOHistory.rectTransform.anchoredPosition = new Vector2(0f, lfContent <= lOView.height ? lOView.height - lfContent : -mfScroll);
        mOHistory.rectTransform.sizeDelta = new Vector2(lOView.width, lfContent);

        // The answers on their own parchment, from its top down.
        mOAnswersBack.texture = mOOrigAnswers;
        fPlaceOrig(mOAnswersBack.rectTransform, lOBlock, OrigParchmentX - 1, OrigAnswersTop - 1, mOOrigAnswers.width, mOOrigAnswers.height, pfScale);

        Vector2 lOPointer = UWPointer.HasMouse ? UWPointer.Position : new Vector2(-1f, -1f);
        float lfRowX = lOBlock.x + (OrigTextX * lfAspect);
        float lfY = lOBlock.yMax - (OrigAnswerTextTop * pfScale);

        for (int liRow = 0; liRow < MaxRows; liRow++)
        {
            if (!mORows[liRow].enabled)
                continue;

            RectTransform lORect = mORows[liRow].rectTransform;
            float lfRow = lORect.sizeDelta.y;

            lfY -= lfRow;
            lORect.anchoredPosition = new Vector2(lfRowX, lfY);

            Rect lOHit = new Rect(lfRowX - (2 * lfAspect), lfY - pfScale, lfRowWidth + (4 * lfAspect), lfRow + (2 * pfScale));
            bool lbChoice = liRow < miRowsShown;
            bool lbHover = lbChoice && lOHit.Contains(lOPointer);

            mORowRects[liRow] = lbChoice ? lOHit : Rect.zero;
            mOHighlights[liRow].enabled = false;
            mORows[liRow].color = lbHover ? msOrigHover : msOrigText;

            // The typed answer's field: a shade of the parchment, not the modern dark box (per
            // user, 2026-10-10: it looked strange there).
            if (mOInputBack.enabled && liRow == 0)
            {
                mOInputBack.color = new Color(0.24f, 0.14f, 0.08f, 0.22f);
                UWUiKit.SetRect(mOInputBack.rectTransform, lOHit.x, lOHit.y, lOHit.width, lOHit.height);
            }

            lfY -= RowGap * pfScale;
        }
    }

    // ------------------------------------------------- Placing

    /// <summary>A rect from its top left in screen pixels (y from the top), sized in original
    /// pixels.</summary>
    private static void fPlace(RectTransform pORect, float pfX, float pfTop, float pfWidth, float pfHeight, float pfScale)
    {
        float lfWidth = pfWidth * pfScale;
        float lfHeight = pfHeight * pfScale;

        UWUiKit.SetRect(pORect, pfX, Screen.height - pfTop - lfHeight, lfWidth, lfHeight);
    }

    /// <summary>The same rect in the mouse's coordinates (y from the bottom).</summary>
    private static Rect fScreenRect(float pfX, float pfTop, float pfWidth, float pfHeight)
    {
        return new Rect(pfX, Screen.height - pfTop - pfHeight, pfWidth, pfHeight);
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        // Under the character panel (41), the bags (42) and the thing on the pointer; the editor's
        // preview over all parts (LateUpdate).
        mOCanvas = UWUiKit.CreateOverlayCanvas(transform, "Modern conversation", UWUiSorting.ModernConversation);

        RectTransform lORootRect = (RectTransform)mOCanvas.transform;

        mODim = UWUiKit.CreateImage(lORootRect, "Dim", new Color(0f, 0f, 0f, 0.55f));

        mOTop = fCreateLeather(lORootRect, "Top");

        for (int liSide = 0; liSide < 2; liSide++)
        {
            string lsSide = liSide == 0 ? "partner" : "player";

            mOFrames[liSide] = fCreateLeather(lORootRect, "Frame " + lsSide);
            mOPortraits[liSide] = UWUiKit.CreateRawImage(lORootRect, "Portrait " + lsSide);
            UWIconPalette.ApplySmooth(mOPortraits[liSide]);
            mONames[liSide] = UWUiKit.CreateText(lORootRect, "Name " + lsSide, TextAnchor.MiddleCenter, msNpc, mOUi);

            mOTradeBacks[liSide] = fCreateLeather(lORootRect, "Trade " + lsSide);
            mOTradeTitles[liSide] = UWUiKit.CreateText(lORootRect, "Trade title " + lsSide, TextAnchor.MiddleCenter, msTitle, mOUi);
        }

        for (int liAt = 0; liAt < mOCircles.Length; liAt++)
        {
            mOCircles[liAt] = UWUiKit.CreateRawImage(lORootRect, "Slot " + liAt, true);
            UWPixelArtUI.Apply(mOCircles[liAt]);

            RectTransform lOCircle = mOCircles[liAt].rectTransform;

            mOIcons[liAt] = UWUiKit.CreateRawImage(lOCircle, "Item " + liAt);
            UWIconPalette.ApplySmooth(mOIcons[liAt]);

            mOCounts[liAt] = UWUiKit.CreateText(lOCircle, "Count " + liAt, TextAnchor.LowerRight, Color.white, mOUi);
            mORings[liAt] = UWUiKit.CreateRawImage(lOCircle, "Mark " + liAt);
        }

        mOHistoryBack = fCreateLeather(lORootRect, "History");

        mOViewport = UWUiKit.CreateClipArea(lORootRect, "History view");

        mOHistory = UWUiKit.CreateText(mOViewport, "History text", TextAnchor.UpperLeft, msNpc, mOUi);
        mOHistory.horizontalOverflow = HorizontalWrapMode.Wrap;
        mOHistory.supportRichText = true;

        mOScrollTrack = UWUiKit.CreateImage(lORootRect, "Scroll track", new Color(0.24f, 0.20f, 0.16f, 0.7f));
        mOScrollThumb = UWUiKit.CreateImage(lORootRect, "Scroll thumb", UWModernPalette.WithAlpha(UWModernPalette.Accent, 0.9f));

        // Over the history, so created after it.
        mOScrollHint = UWUiKit.CreateRawImage(lORootRect, "Stick scroll hint");

        mOAnswersBack = fCreateLeather(lORootRect, "Answers");

        mOInputBack = UWUiKit.CreateImage(lORootRect, "Typed answer", UWModernPalette.InputFill);
        mOInputBack.enabled = false;

        for (int liRow = 0; liRow < MaxRows; liRow++)
        {
            mOHighlights[liRow] = UWUiKit.CreateImage(lORootRect, "Hover " + liRow, UWModernPalette.WithAlpha(UWModernPalette.Gold, 0.2f));
            mOHighlights[liRow].enabled = false;
        }

        for (int liRow = 0; liRow < MaxRows; liRow++)
        {
            mORows[liRow] = UWUiKit.CreateText(lORootRect, "Answer " + (liRow + 1), TextAnchor.UpperLeft, msNpc, mOUi);
            mORows[liRow].horizontalOverflow = HorizontalWrapMode.Wrap;
            mORows[liRow].supportRichText = true;
        }
    }

    private RawImage fCreateLeather(Transform pOParent, string psName)
    {
        RawImage lOImage = UWUiKit.CreateRawImage(pOParent, psName, true);

        UWPixelArtUI.Apply(lOImage);

        return lOImage;
    }
}
