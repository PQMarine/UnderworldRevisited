using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S CONVERSATION SCREEN (UW2 plan phase 3 step 7), as Underworld Exhumed's uw2 conv/CONVERSE.C
/// draws it: BYT.ARK's screen 2 over the game screen, but for the right panel, which stays; the portraits and
/// names, the parchment and the answers' scroll over it. It runs the first game's engine-free session
/// (UWConversationSession) and view (UWConversationView) with the second game's measures; the classic screen
/// of the first game (UWConversationController, UWClassicConversationRenderer) is tied to its frame and its
/// character, so this is its own class. Its input is called by UWGameScreenUw2 while it is open; the panel
/// on the right stays the game screen's, so the things to trade come on its pointer (UseTradeSlot). On the modern
/// scheme this class still runs the conversation, with the classic screen hidden: UWModernConversation draws its
/// view (UWModernConversationHostUw2) and tells where the pointer is on it.
///
/// NOT BUILT: EncumCheck on taking, the pause built-in's wait.
/// </summary>
public sealed class UWConversationScreenUw2 : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    /// <summary>The answer is being typed: the keys are the text's (UWGameScreenUw2's key lock).</summary>
    public static bool IsTyping { get; private set; }

    /// <summary>The conversation under way, for the modern conversation's host.</summary>
    public static UWConversationScreenUw2 Current => msOCurrent;

    /// <summary>The modern scheme draws the conversation.</summary>
    public static bool IsModern => UWGameScreenUw2.IsModernScheme;

    public DataImport Data => mOData;

    public UWConversationView View => mOView;

    /// <summary>Changes with a portrait (switch_pic).</summary>
    public int PortraitVersion { get; private set; }

    /// <summary>The names on the screen: switch_pic changes the partner's.</summary>
    public string ShownPartnerName { get; private set; } = string.Empty;

    public string ShownPlayerName { get; private set; } = string.Empty;

    public Texture2D PortraitPicture(bool pbPlayer)
    {
        return (pbPlayer ? mOPlayerPortrait : mONpcPortrait).texture as Texture2D;
    }

    private readonly Dictionary<int, Texture2D> mOSpeakerHeads = new Dictionary<int, Texture2D>();

    /// <summary>The head switch_pic shows for this number, for the modern history.</summary>
    public Texture2D SpeakerPortrait(int piPicture)
    {
        if (mOSpeakerHeads.TryGetValue(piPicture, out Texture2D lOHead))
            return lOHead;

        bool lbPerson = piPicture < UWTalkRulesUw2.GenericFirstSlot;

        lOHead = lbPerson && piPicture > 0 ? fOpaque(UWTexture.TextureTypes.CHARHEAD, piPicture - 1) : null;

        if (lOHead == null)
            lOHead = fOpaque(UWTexture.TextureTypes.GHED, lbPerson ? 0 : piPicture - UWTalkRulesUw2.GenericFirstSlot)
                ?? fOpaque(UWTexture.TextureTypes.GHED, 0);

        mOSpeakerHeads[piPicture] = lOHead;

        return lOHead;
    }

    public Texture2D ItemPicture(int piId)
    {
        return fItemPicture(piId);
    }

    private const int ScreenHeight = UWClassicLayout.Screen.Height;

    private DataImport mOData;

    private RectTransform mOFrame;

    private readonly UWConversationView mOView = new UWConversationView();

    private UWConversationSession mOSession;

    private readonly List<RawImage> mOPieces = new List<RawImage>();

    private RawImage mONpcPortrait;

    private RawImage mOPlayerPortrait;

    private RawImage mONpcName;

    private RawImage mOPlayerName;

    private RawImage mOHistory;

    private RawImage mOAnswers;

    private readonly RawImage[] mOLeftEdges = new RawImage[UWScrollEdgeRules.ConversationRows];

    private readonly RawImage[] mORightEdges = new RawImage[UWScrollEdgeRules.ConversationRows];

    private readonly Dictionary<int, Texture2D> mOEdgePictures = new Dictionary<int, Texture2D>();

    private readonly List<string> mOLines = new List<string>();

    private readonly List<UWConversationLayout.RoleEnum> mORoles = new List<UWConversationLayout.RoleEnum>();

    /// <summary>The scroll's rows and the answer on each, 0 for none (convo_line_option).</summary>
    private readonly List<int> mOAnswerOfRow = new List<int>();

    private readonly RawImage[] mOSlots = new RawImage[2 * UWConversationTrade.SlotCountUw2];

    private readonly RawImage[] mOCounts = new RawImage[2 * UWConversationTrade.SlotCountUw2];

    private readonly RawImage[] mOMarks = new RawImage[2 * UWConversationTrade.SlotCountUw2];

    private readonly Dictionary<int, Texture2D> mOItemPictures = new Dictionary<int, Texture2D>();

    private Texture2D mOMarkOn;

    private Texture2D mOMarkOff;

    private int miTradeVersion = -1;

    /// <summary>A left press on a slot, waiting for the release that marks or the drag that takes; -1 none.</summary>
    private int miPressedSlot = -1;

    private bool mbPressedPlayer;

    private Vector2 mOPressedAt;

    private static UWConversationScreenUw2 msOCurrent;

    private UWTextEntry mOEntry;

    private bool mbTyping;

    private bool mbClosing;

    private int miOpenedFrame;

    /// <summary>The answers typed since the last menu: wdialog writes into the answer scroll, and only a
    /// menu clears it.</summary>
    private readonly List<string> mOTypedLines = new List<string>();

    /// <summary>What wdialog writes after the prompt on Escape.</summary>
    private const string EscapedAnswer = "-";

    /// <summary>When the wait before the partner's line is over.</summary>
    private float mfHoldUntil;

    /// <summary>\\P's 200 ticks at 256 a second.</summary>
    private const float PartnerHoldSeconds = 0xC8 / 256f;

    private UWFont fFont => mOData.Fonts != null ? mOData.Fonts.Get(UWFonts.FontType.Normal) : null;

    private static UWConversationView.Policy fPolicy => new UWConversationView.Policy
    {
        PauseForMore = !IsModern,
        WrapWidth = UWClassicLayoutUw2.Conversation.HistoryWidth,
        LinesCapacity = UWClassicLayoutUw2.Conversation.HistoryLines,
        HoldBeforePartner = true
    };

    /// <summary>The conversation with this critter in its slot begins; false when there is none to run.</summary>
    public bool Open(DataImport pOData, RectTransform pOFrame, UWNpc pONpc, int piSlot)
    {
        mOData = pOData;
        mOFrame = pOFrame;

        UWConversationSession lOSession = new UWConversationSession(pOData, pONpc, piSlot, new UWConversationHostUw2(), UWGameSession.Current);

        if (lOSession.IsFinished && lOSession.Lines.Count == 0)
            return false;

        fEnsureBuilt();

        mOSession = lOSession;
        mOView.Begin(lOSession, pONpc, piSlot, null);
        IsOpen = true;
        msOCurrent = this;
        mbClosing = false;
        miOpenedFrame = Time.frameCount;
        miTradeVersion = -1;
        miPressedSlot = -1;
        miButtonsUpFrame = -1;
        mOTypedLines.Clear();

        foreach (RawImage lOPiece in mOPieces)
            lOPiece.gameObject.SetActive(true);

        fShowPartner(pONpc != null ? pONpc.NPCwhoami : piSlot, pONpc != null ? pONpc.ID & UWClassicLayoutUw2.Conversation.ClassMask : 0);
        fShowNames();
        fApplyEdges();

        lOSession.Advance();
        fRedraw();

        return true;
    }

    /// <summary>A frame of the conversation (UWGameScreenUw2.Update).</summary>
    public void Tick()
    {
        if (mOSession == null || mbClosing)
            return;

        // Before the wait, so the slots stand at once when the first line waits.
        if (mOSession.Trade != null && mOSession.Trade.Version != miTradeVersion)
            fDrawTrade();

        // switch_pic acts at once; the wait comes with the partner's next line.
        while (mOView.PendingPicture != 0)
        {
            fSwitchPicture(mOView.PendingPicture);
            mOView.PictureShown();
            fRedraw();
        }

        // npc_say's \\P: scroll_wait(200), which a key or a click cuts short.
        if (mOView.HoldPending)
        {
            if (Time.unscaledTime >= mfHoldUntil || fAnyInput())
            {
                mOView.HoldOver();
                fRedraw();
            }

            return;
        }

        fUpdateTrade();

        if (mOView.IsLookTextShown)
        {
            if (mOView.IsLookTextOver(Time.unscaledTime) || UWMouseButtons.LeftReleased)
            {
                mOView.EndLookText();
                fRedraw();
            }

            return;
        }

        if (mOSession.IsAskingText)
        {
            fUpdateTyping();

            return;
        }

        if (mOView.MorePending)
        {
            if (fAnyInput())
            {
                mOView.ContinueAfterMore();
                fRedraw();
            }

            return;
        }

        if (mOSession.IsFinished && mOSession.Choices.Count == 0)
        {
            mbClosing = true;

            if (!string.IsNullOrEmpty(mOSession.Error))
                Debug.LogWarning("Conversation ended by an error: " + mOSession.Error);

            StartCoroutine(fCloseWithFade());

            return;
        }

        int liChoice = fReadChoice();

        if (liChoice <= 0)
            return;

        mOSession.Choose(liChoice);
        fRedraw();
    }

    private bool fAnyInput()
    {
        if (UWTextEntry.AnyKeyPressed)
            return true;

        return fButtonReleased();
    }

    /// <summary>The frame both buttons were up for the first time since the opening, -1 before that.</summary>
    private int miButtonsUpFrame = -1;

    /// <summary>A button came up, not the one still held from the Talk that opened the conversation: that
    /// release ended a conversation that only says something, at once (per user on the original, 2026-10-31).</summary>
    private bool fButtonReleased()
    {
        Mouse lOMouse = Mouse.current;

        if (miButtonsUpFrame < 0 && (lOMouse == null || (!lOMouse.leftButton.isPressed && !lOMouse.rightButton.isPressed)))
            miButtonsUpFrame = Time.frameCount;

        return miButtonsUpFrame >= 0 && Time.frameCount > miButtonsUpFrame && (UWMouseButtons.LeftReleased || UWMouseButtons.RightReleased);
    }

    /// <summary>conv_play_menu: the keys 1 to 5, or a click on the scroll's row; both buttons, on the release as
    /// in the first game (per user on the original, 2026-09-18).</summary>
    private int fReadChoice()
    {
        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard != null)
        {
            for (int liAt = 0; liAt < UWClassicLayoutUw2.Conversation.ChoiceKeys && liAt < mOSession.Choices.Count; liAt++)
            {
                if (lOKeyboard[Key.Digit1 + liAt].wasPressedThisFrame)
                    return liAt + 1;
            }
        }

        if (!fButtonReleased())
            return 0;

        if (IsModern)
            return UWModernConversation.Instance != null ? UWModernConversation.Instance.ChoiceAt(UWPointer.Position) : 0;

        if (!fPoint(out int liX, out int liY))
            return 0;

        if (liX < UWClassicLayoutUw2.Conversation.AnswerClickLeft || liX > UWClassicLayoutUw2.Conversation.AnswerClickRight
            || liY < UWClassicLayoutUw2.Conversation.AnswerClickBottom || liY > UWClassicLayoutUw2.Conversation.AnswersTopRow)
            return 0;

        int liRow = (UWClassicLayoutUw2.Conversation.AnswersTopRow - liY) / UWClassicLayoutUw2.Conversation.LineHeight;

        return liRow < mOAnswerOfRow.Count ? mOAnswerOfRow[liRow] : 0;
    }

    /// <summary>babl_ask: the typed line stands in the answers' scroll; Enter or a click ends it, Escape gives
    /// the empty text (wdialog).</summary>
    private void fUpdateTyping()
    {
        if (!mbTyping)
        {
            mbTyping = true;
            IsTyping = true;
            mOView.BeginTyping();

            if (mOEntry == null)
                mOEntry = new UWTextEntry(fOnTyped);

            mOEntry.Begin();
            fDrawTyped();
        }

        bool lbEscape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        // wdialog ends on Enter, Escape or a click.
        if (UWTextEntry.EnterPressed || lbEscape || fButtonReleased())
        {
            string lsAnswer = lbEscape ? string.Empty : mOView.TypedAnswer;

            mOTypedLines.Add(UWConversationLayout.TypedAnswerPrompt + (lbEscape ? EscapedAnswer : lsAnswer));
            mbTyping = false;
            IsTyping = false;
            mOView.EndTyping();
            mOEntry.End();
            mOSession.SupplyText(lsAnswer);
            fRedraw();

            return;
        }

        if (UWTextEntry.BackspacePressed && mOView.Backspace())
            fDrawTyped();

        mOEntry.ReadKeys();
    }

    private void fOnTyped(char pcTyped)
    {
        if (mbTyping && mOView.TryType(pcTyped, fFont))
            fDrawTyped();
    }

    private void fDrawTyped()
    {
        mOLines.Clear();
        mOLines.AddRange(mOTypedLines);
        mOLines.Add(mOView.TypedAnswerLine);
        fDrawLines(mOAnswers, mOLines, null, UWClassicLayoutUw2.Conversation.AnswersWidth);
    }

    /// <summary>The history brought up to the session and drawn, then the answers; the answers only come once
    /// everything has been said.</summary>
    private void fRedraw()
    {
        UWFont lOFont = fFont;

        if (lOFont == null)
            return;

        mOView.Refresh(lOFont, fPolicy);
        mfHoldUntil = Time.unscaledTime + PartnerHoldSeconds;
        mOView.VisibleHistory(fPolicy, mOLines, mORoles);

        if (mOView.RollEdges(mOLines))
            fApplyEdges();

        fDrawLines(mOHistory, mOLines, mORoles, UWClassicLayoutUw2.Conversation.HistoryWidth);

        mOAnswerOfRow.Clear();
        mOLines.Clear();

        if (!mOView.MorePending && mOView.PendingPicture == 0 && !mOView.HoldPending)
        {
            for (int liAt = 0; liAt < mOSession.Choices.Count; liAt++)
            {
                List<string> lOWrapped = UWTextLayout.WrapText(lOFont, UWConversationLayout.FormatChoice(liAt, mOSession.Choices[liAt]),
                    UWClassicLayoutUw2.Conversation.AnswersWidth);

                mOLines.AddRange(lOWrapped);

                foreach (string lsLine in lOWrapped)
                    mOAnswerOfRow.Add(liAt + 1);
            }
        }

        if (mOLines.Count > 0)
            mOTypedLines.Clear();
        else
            mOLines.AddRange(mOTypedLines);

        fDrawLines(mOAnswers, mOLines, null, UWClassicLayoutUw2.Conversation.AnswersWidth);
    }

    private void fDrawLines(RawImage pOImage, List<string> pOLines, List<UWConversationLayout.RoleEnum> pORoles, int piWidth)
    {
        if (pOLines.Count == 0)
        {
            UWTextLabel.Hide(pOImage);

            return;
        }

        Color32[] lyColours = new Color32[pOLines.Count];

        for (int liAt = 0; liAt < lyColours.Length; liAt++)
            lyColours[liAt] = fPalette(fInk(pORoles != null ? pORoles[liAt] : UWConversationLayout.RoleEnum.Npc));

        UWTextLabel.Get(pOImage).SetText(fFont, pOLines, piWidth, lyColours, UWTextRaster.ColoursEnum.PerLine, false, FilterMode.Point);
    }

    /// <summary>spec_col: \0 the scroll's ink, \1 the player's, \2 the narrator's; [MORE] its own.</summary>
    private static int fInk(UWConversationLayout.RoleEnum peRole)
    {
        switch (peRole)
        {
            case UWConversationLayout.RoleEnum.Player:
                return UWClassicLayoutUw2.Conversation.PlayerInk;
            case UWConversationLayout.RoleEnum.Narrator:
                return UWClassicLayoutUw2.Conversation.NarratorInk;
            case UWConversationLayout.RoleEnum.Ui:
                return UWClassicLayoutUw2.Conversation.MoreInk;
            default:
                return UWClassicLayoutUw2.Conversation.NpcInk;
        }
    }

    private Color32 fPalette(int piIndex)
    {
        return UWColourVision.Apply(UWScreenUi.GetColour(mOData.Palettes, UWScreensUw2.PaletteOf(UWScreensUw2.ScreenEnum.Conversation), piIndex));
    }

    private void fApplyEdges()
    {
        for (int liAt = 0; liAt < UWScrollEdgeRules.ConversationRows; liAt++)
        {
            UWScreenUi.SetTexture(mOLeftEdges[liAt], fEdge(UWScrollEdgeRules.GetConversationLeftImage(mOView.EdgeFrame)));
            UWScreenUi.SetTexture(mORightEdges[liAt], fEdge(UWScrollEdgeRules.GetConversationRightImage(mOView.EdgeFrame)));
        }
    }

    private Texture2D fEdge(int piPicture)
    {
        if (!mOEdgePictures.TryGetValue(piPicture, out Texture2D lOPicture))
        {
            lOPicture = fOpaque(UWTexture.TextureTypes.SCRLEDGE, piPicture);
            mOEdgePictures[piPicture] = lOPicture;
        }

        return lOPicture;
    }

    private void fShowNames()
    {
        UWFont lOFont = fFont;
        UWPlayerFile lOFile = UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;
        string lsPlayer = lOFile != null ? UWPlayerRecordUw2.Name(lOFile) : UWPlayerRecordUw2.WalkerName;
        Color32 lOInk = fPalette(UWClassicLayoutUw2.Conversation.NameInk);

        if (lOFont == null)
            return;

        ShownPartnerName = mOView.PartnerName;
        ShownPlayerName = lsPlayer;
        fShowName(mONpcName, mOView.PartnerName, UWClassicLayoutUw2.Conversation.NpcNameLeft, lOInk, lOFont);

        int liWidth = lOFont.MeasureText(lsPlayer, 0, UWFontRenderer.CharacterSpacing);

        fShowName(mOPlayerName, lsPlayer, UWClassicLayoutUw2.Conversation.PlayerNameRight - liWidth, lOInk, lOFont);

        if (lOFile != null)
            fSetPicture(mOPlayerPortrait, UWTexture.TextureTypes.HEADS, UWPlayerRecordUw2.BodyPortrait(lOFile));
    }

    private static void fShowName(RawImage pOImage, string psName, int piLeft, Color32 pOInk, UWFont pOFont)
    {
        if (string.IsNullOrEmpty(psName))
        {
            UWTextLabel.Hide(pOImage);

            return;
        }

        UWTextLabel.Get(pOImage).SetLine(pOFont, psName, UWTextRaster.TightWidth(pOFont, psName), pOInk, false, FilterMode.Point);
        pOImage.rectTransform.anchoredPosition = new Vector2(piLeft, pOImage.rectTransform.anchoredPosition.y);
    }

    /// <summary>strt_converse: the whoami's head of CHARHEAD.GR, else the class's of GHED.GR, else GHED.GR's
    /// first.</summary>
    private void fShowPartner(int piWhoami, int piClass)
    {
        if (piWhoami > 0 && fSetPicture(mONpcPortrait, UWTexture.TextureTypes.CHARHEAD, piWhoami - 1))
            return;

        if (!fSetPicture(mONpcPortrait, UWTexture.TextureTypes.GHED, piClass))
            fSetPicture(mONpcPortrait, UWTexture.TextureTypes.GHED, 0);
    }

    /// <summary>switch_pic: another portrait and name. A number below 0x100 is a whoami, else a generic
    /// head.</summary>
    private void fSwitchPicture(int piWhich)
    {
        int liWhich = piWhich;

        if (liWhich < UWTalkRulesUw2.GenericFirstSlot)
        {
            fShowPartner(liWhich, 0);
            ShownPartnerName = UWText.Block(mOData, UWConversations.PartnerNameStringBlock, UWConversations.GetPartnerNameIndex(liWhich), string.Empty);

            if (fFont != null)
                fShowName(mONpcName, ShownPartnerName, UWClassicLayoutUw2.Conversation.NpcNameLeft, fPalette(UWClassicLayoutUw2.Conversation.NameInk), fFont);
        }
        else
            fShowPartner(0, liWhich - UWTalkRulesUw2.GenericFirstSlot);
    }

    /// <summary>A picture drawn opaque, as show and the screen's own are: colour 0 is black here, not a hole. Null
    /// when it is missing.</summary>
    private Texture2D fOpaque(UWTexture.TextureTypes peType, int piIndex)
    {
        UWTexture lOPicture;

        try
        {
            lOPicture = mOData.Textures != null ? mOData.Textures.GetTextureByType(peType, piIndex) : null;
        }
        catch (System.ArgumentOutOfRangeException)
        {
            return null;
        }

        if (lOPicture == null || lOPicture.IsEmptyEntry || lOPicture.PaletteIndices == null || lOPicture.Width <= 0 || lOPicture.Height <= 0)
            return null;

        byte[] lyRgba = UWUploadPixels.FromTexture(lOPicture, UWColourVision.Filter);

        for (int liAt = UWUploadPixels.BytesPerPixel - 1; liAt < lyRgba.Length; liAt += UWUploadPixels.BytesPerPixel)
            lyRgba[liAt] = 255;

        return UWTextureFactory.Upload(lyRgba, lOPicture.Width, lOPicture.Height, FilterMode.Point, TextureWrapMode.Clamp, "UW2 conversation " + peType + " " + piIndex);
    }

    private bool fSetPicture(RawImage pOImage, UWTexture.TextureTypes peType, int piIndex)
    {
        Texture2D lOOld = pOImage.texture as Texture2D;
        Texture2D lOPicture = fOpaque(peType, piIndex);

        if (lOPicture == null)
            return false;

        if (lOOld != null)
            Destroy(lOOld);

        UWScreenUi.SetTexture(pOImage, lOPicture);
        PortraitVersion++;

        return true;
    }

    /// <summary>A look text in place of the answers for its two seconds (UWConversationView.ShowLookText).</summary>
    public static bool ShowLook(string psText)
    {
        if (msOCurrent == null || !IsOpen)
            return false;

        msOCurrent.fShowLook(psText);

        return true;
    }

    private void fShowLook(string psText)
    {
        UWFont lOFont = fFont;

        if (lOFont == null || string.IsNullOrEmpty(psText))
            return;

        mOView.ShowLookText(psText, Time.unscaledTime);
        mOLines.Clear();
        mOLines.AddRange(UWTextLayout.WrapText(lOFont, psText, UWClassicLayoutUw2.Conversation.AnswersWidth));
        fDrawLines(mOAnswers, mOLines, null, UWClassicLayoutUw2.Conversation.AnswersWidth);
    }

    private static int fSlotLeft(bool pbPlayer, int piSlot)
    {
        return (pbPlayer ? UWClassicLayoutUw2.Conversation.PlayerSlotLeft : UWClassicLayoutUw2.Conversation.NpcSlotLeft)
            + ((piSlot & 1) * UWClassicLayoutUw2.Conversation.SlotColumnStep);
    }

    private static int fSlotTopY(int piSlot)
    {
        return UWClassicLayoutUw2.Conversation.SlotTopY - ((piSlot / 2) * UWClassicLayoutUw2.Conversation.SlotRowStep);
    }

    /// <summary>The slot under the point: play_slot_hit_abs, npc_slot_hit_abs; -1 none.</summary>
    private static int fSlotAt(bool pbPlayer, int piX, int piY)
    {
        for (int liSlot = 0; liSlot < UWConversationTrade.SlotCountUw2; liSlot++)
        {
            int liLeft = fSlotLeft(pbPlayer, liSlot);
            int liTop = fSlotTopY(liSlot);

            if (piX >= liLeft && piX <= liLeft + UWClassicLayoutUw2.Conversation.SlotSize
                && piY <= liTop && piY >= liTop - UWClassicLayoutUw2.Conversation.SlotSize)
                return liSlot;
        }

        return -1;
    }

    /// <summary>
    /// UseTradeSlot, npc_barter and play_barter. With a thing on the pointer a press puts it into the player's
    /// slot, marked; the partner's slots take nothing. Without one the right button looks, the left marks on the
    /// release, or takes when the pointer is dragged off: the player's own at any time, the partner's once a
    /// deal has left them to take.
    /// </summary>
    private void fUpdateTrade()
    {
        UWConversationTrade lOTrade = mOSession.Trade;
        UWInventoryModel lOInventory = UWGameScreenUw2.Current != null ? UWGameScreenUw2.Current.Inventory : null;

        if (lOTrade == null || lOInventory == null)
            return;

        if (miPressedSlot >= 0)
        {
            fFollowSlotPress(lOTrade, lOInventory);

            return;
        }

        Mouse lOMouse = UWMouseButtons.GameMouse;

        if (lOMouse == null || miOpenedFrame == Time.frameCount || !(lOMouse.leftButton.wasPressedThisFrame || lOMouse.rightButton.wasPressedThisFrame))
            return;

        int liPlayerSlot = -1;
        int liNpcSlot = -1;

        if (IsModern)
        {
            if (UWModernConversation.Instance == null || !UWModernConversation.Instance.TryGetTradeSlotAt(UWPointer.Position, out bool lbNpc, out int liAt))
                return;

            liPlayerSlot = lbNpc ? -1 : liAt;
            liNpcSlot = lbNpc ? liAt : -1;
        }
        else if (fPoint(out int liX, out int liY))
        {
            liPlayerSlot = fSlotAt(true, liX, liY);
            liNpcSlot = fSlotAt(false, liX, liY);
        }

        bool lbPlayer = liPlayerSlot >= 0;
        int liSlot = lbPlayer ? liPlayerSlot : liNpcSlot;

        if (liSlot < 0)
            return;

        if (lOInventory.CursorItem != null)
        {
            if (lbPlayer)
            {
                UWObject lOBack = lOTrade.DropOnPlayerSlot(liSlot, lOInventory.TakeCursorItem());

                if (lOBack != null)
                    lOInventory.PickUpToCursor(lOBack);
            }

            return;
        }

        UWObject lOItem = lbPlayer ? lOTrade.PlayerItems[liSlot] : lOTrade.NpcItems[liSlot];

        if (lOItem == null)
            return;

        if (lOMouse.rightButton.wasPressedThisFrame)
        {
            if (lbPlayer)
                mOSession.LookAtOwnTradeItem(lOItem);
            else
                mOSession.LookAtPartnerTradeItem(lOItem);

            return;
        }

        miPressedSlot = liSlot;
        mbPressedPlayer = lbPlayer;
        mOPressedAt = UWPointer.Position;
    }

    private void fFollowSlotPress(UWConversationTrade pOTrade, UWInventoryModel pOInventory)
    {
        Mouse lOMouse = Mouse.current;

        if (lOMouse == null || !lOMouse.leftButton.isPressed)
        {
            if (mbPressedPlayer)
                pOTrade.TogglePlayerSelected(miPressedSlot);
            else
                pOTrade.ToggleNpcSelected(miPressedSlot);

            miPressedSlot = -1;

            return;
        }

        if ((UWPointer.Position - mOPressedAt).magnitude < UWWalkUw2View.DragToTakePixels)
            return;

        int liSlot = miPressedSlot;
        bool lbPlayer = mbPressedPlayer;

        miPressedSlot = -1;

        // The partner's goods only once a deal has left them to take; a stack asks how many (UseTradeSlot).
        if (!lbPlayer && !(pOTrade.NpcGoodsBought && pOTrade.NpcBought[liSlot]))
            return;

        UWObject lOItem = lbPlayer ? pOTrade.PlayerItems[liSlot] : pOTrade.NpcItems[liSlot];

        if (UWGameScreenUw2.Current == null
            || !UWGameScreenUw2.Current.AskCount(lOItem, piCount => fTakeCountFromSlot(pOTrade, pOInventory, liSlot, lbPlayer, piCount)))
            fTakeCountFromSlot(pOTrade, pOInventory, liSlot, lbPlayer, UWStackRules.CountAtLeastOne(lOItem));
    }

    /// <summary>uw2 BARTER.C:328, "That is too heavy to take.": the first game's sentence has another number.</summary>
    private const int TooHeavyToTakeString = 0x10C;

    /// <summary>Part of a stack is counted off, the stack stays in the slot; the whole leaves it.</summary>
    private static void fTakeCountFromSlot(UWConversationTrade pOTrade, UWInventoryModel pOInventory, int piSlot, bool pbPlayer, int piCount)
    {
        UWObject lOItem = pbPlayer ? pOTrade.PlayerItems[piSlot] : pOTrade.NpcItems[piSlot];

        // UseTradeSlot's EncumCheck, on both sides and after the count; the good stays where it is.
        if (!UWPickupRules.CheckFromTrade(lOItem, piCount, pOInventory).CanTake)
        {
            ShowLook(UWText.Block(msOCurrent.mOData, 1, TooHeavyToTakeString + 1, string.Empty));

            return;
        }

        if (piCount < UWStackRules.CountAtLeastOne(lOItem) && pOInventory.TakeFromStack(lOItem, piCount))
        {
            if (pbPlayer)
                pOTrade.PlayerSlotPartTaken(piSlot);
            else
                pOTrade.NpcSlotChanged(piSlot);

            return;
        }

        UWObject lOTaken = pbPlayer ? pOTrade.TakeFromPlayerSlot(piSlot) : pOTrade.TakeFromNpcSlot(piSlot);

        if (lOTaken != null)
            pOInventory.PickUpToCursor(lOTaken);
    }

    /// <summary>"Move how many?" stands in the answers' scroll, which gives way to it; a menu comes back after.</summary>
    public void ShowCountLine(string psLine)
    {
        if (IsModern)
        {
            mOView.ShowLookText(psLine, Time.unscaledTime);

            return;
        }

        mOLines.Clear();
        mOLines.Add(psLine);
        fDrawLines(mOAnswers, mOLines, null, UWClassicLayoutUw2.Conversation.AnswersWidth);
    }

    public void EndCountLine()
    {
        if (IsModern)
            mOView.EndLookText();

        fRedraw();
    }

    /// <summary>The point under the pointer in the original's pixels, y from the bottom.</summary>
    private bool fPoint(out int piX, out int piY)
    {
        piX = 0;
        piY = 0;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mOFrame, UWPointer.Position, null, out Vector2 lOLocal))
            return false;

        piX = Mathf.FloorToInt(lOLocal.x + (UWClassicLayout.Screen.Width / 2f));
        piY = Mathf.FloorToInt(lOLocal.y + (ScreenHeight / 2f));

        return true;
    }

    /// <summary>drawTradeSlot and showSelection: both sides' things, counts and marks.</summary>
    private void fDrawTrade()
    {
        miTradeVersion = mOSession.Trade.Version;

        for (int liSlot = 0; liSlot < UWConversationTrade.SlotCountUw2; liSlot++)
        {
            fDrawSlot(false, liSlot);
            fDrawSlot(true, liSlot);
        }
    }

    private void fDrawSlot(bool pbPlayer, int piSlot)
    {
        UWConversationView.TradeSlot lOSlot = mOView.TradeSlotOf(!pbPlayer, piSlot);
        int liAt = (pbPlayer ? UWConversationTrade.SlotCountUw2 : 0) + piSlot;
        Texture2D lOPicture = lOSlot.Item != null ? fItemPicture(lOSlot.Item.ID) : null;

        if (lOPicture != null)
            UWScreenUi.SetTexture(mOSlots[liAt], lOPicture);
        else
            mOSlots[liAt].enabled = false;

        UWFont lOSmall = mOData.Fonts != null ? mOData.Fonts.Get(UWFonts.FontType.Small) : null;

        if (lOSlot.Count > 1 && lOSmall != null)
        {
            string lsCount = lOSlot.Count.ToString();

            UWTextLabel.Get(mOCounts[liAt]).SetLine(lOSmall, lsCount, UWTextRaster.TightWidth(lOSmall, lsCount), fPalette(UWClassicLayoutUw2.Conversation.CountInk), false, FilterMode.Point);
        }
        else
            UWTextLabel.Hide(mOCounts[liAt]);

        UWScreenUi.SetTexture(mOMarks[liAt], lOSlot.Marked ? mOMarkOn : mOMarkOff);
    }

    private Texture2D fItemPicture(int piId)
    {
        if (!mOItemPictures.TryGetValue(piId, out Texture2D lOPicture))
        {
            lOPicture = UWScreenUi.BuildObjectPicture(mOData.Textures, piId);
            mOItemPictures[piId] = lOPicture;
        }

        return lOPicture;
    }

    /// <summary>showSelection's cross, three wide and four high: its top pixel, its upper bar, its lower bar and
    /// its foot; off everything in colour 1.</summary>
    private Texture2D fMark(bool pbOn)
    {
        int liBar = pbOn ? UWClassicLayoutUw2.Conversation.MarkLit : UWClassicLayoutUw2.Conversation.MarkOff;
        int liSide = pbOn ? UWClassicLayoutUw2.Conversation.MarkLitSide : UWClassicLayoutUw2.Conversation.MarkOff;
        int liTop = pbOn ? UWClassicLayoutUw2.Conversation.MarkLitTop : UWClassicLayoutUw2.Conversation.MarkOff;
        Color32 lOClear = new Color32(0, 0, 0, 0);

        return UWTextureFactory.FromColours(new[]
        {
            lOClear, fPalette(liBar), lOClear,
            fPalette(liBar), fPalette(liBar), fPalette(liBar),
            fPalette(liSide), fPalette(liBar), fPalette(liSide),
            lOClear, fPalette(liTop), lOClear
        }, 3, 4, FilterMode.Point, TextureWrapMode.Clamp, "UW2 trade mark");
    }

    /// <summary>The last line's hold, the fade to black and back, as the first game's (UWConversationController):
    /// timed when the player spoke last and the partner was not left hostile, else until a key or a
    /// click.</summary>
    private IEnumerator fCloseWithFade()
    {
        bool lbTimed = mOSession.EndsWithTimedPause;

        for (float lfAt = 0f; !lbTimed || lfAt < UWClassicLayoutUw2.Conversation.EndHoldSeconds; lfAt += Time.unscaledDeltaTime)
        {
            yield return null;

            if (fAnyInput())
                break;
        }

        UWScreenUi.ShowLoadingCover();

        for (float lfAt = 0f; lfAt < UWClassicLayoutUw2.Conversation.FadeSeconds; lfAt += Time.unscaledDeltaTime)
        {
            UWScreenUi.SetLoadingCoverAlpha(lfAt / UWClassicLayoutUw2.Conversation.FadeSeconds);
            yield return null;
        }

        UWScreenUi.SetLoadingCoverAlpha(1f);
        fClose();

        for (float lfAt = 0f; lfAt < UWClassicLayoutUw2.Conversation.FadeSeconds; lfAt += Time.unscaledDeltaTime)
        {
            UWScreenUi.SetLoadingCoverAlpha(1f - (lfAt / UWClassicLayoutUw2.Conversation.FadeSeconds));
            yield return null;
        }

        UWScreenUi.HideLoadingCover();
    }

    /// <summary>free_converse: the screen goes, and the teleport the script asked for is done.</summary>
    private void fClose()
    {
        UWConversationSession lOSession = mOSession;

        lOSession.Abort();
        mOView.End();
        mOSession = null;
        IsOpen = false;
        mbClosing = false;

        foreach (RawImage lOPiece in mOPieces)
            lOPiece.gameObject.SetActive(false);

        foreach (string lsCall in lOSession.RulesToCome)
            Debug.Log("[Walk] The conversation called " + lsCall + " (its rules come in phase 4).");

        foreach (string lsCall in lOSession.MissingFunctions)
            Debug.Log("[Walk] The conversation called " + lsCall + ", which is not built.");

        if (lOSession.HasPlayerTeleport && UWScene.LevelLoader != null)
        {
            UWScene.LevelLoader.WalkTeleport(new UWTeleportUw2.Target
            {
                Level = lOSession.TeleportLevel,
                TileX = lOSession.TeleportX,
                TileY = lOSession.TeleportY
            });
        }
    }

    private void OnDestroy()
    {
        IsOpen = false;
        IsTyping = false;

        if (mbTyping && mOEntry != null)
            mOEntry.End();
    }

    private RawImage fPiece(string psName, int piLeft, int piTop)
    {
        RawImage lOImage = UWUiKit.CreateRawImageAt(mOFrame, psName, piLeft, piTop);

        lOImage.raycastTarget = false;
        mOPieces.Add(lOImage);

        return lOImage;
    }

    private void fEnsureBuilt()
    {
        if (mOPieces.Count > 0)
            return;

        Texture2D lOScreen = fOpaque(UWTexture.TextureTypes.CONV, 0);

        fBackPiece("Conversation left", lOScreen, 0, 0, UWClassicLayoutUw2.Conversation.PanelLeft, ScreenHeight);
        fBackPiece("Conversation bottom", lOScreen, UWClassicLayoutUw2.Conversation.PanelLeft, UWClassicLayoutUw2.Conversation.PanelBottom,
            UWClassicLayout.Screen.Width - UWClassicLayoutUw2.Conversation.PanelLeft, ScreenHeight - UWClassicLayoutUw2.Conversation.PanelBottom);

        mONpcPortrait = fPiece("Portrait partner", UWClassicLayoutUw2.Conversation.NpcPortraitLeft, ScreenHeight - 1 - UWClassicLayoutUw2.Conversation.PortraitY);
        mOPlayerPortrait = fPiece("Portrait player", UWClassicLayoutUw2.Conversation.PlayerPortraitLeft, ScreenHeight - 1 - UWClassicLayoutUw2.Conversation.PortraitY);
        mONpcName = fPiece("Name partner", UWClassicLayoutUw2.Conversation.NpcNameLeft, ScreenHeight - 1 - UWClassicLayoutUw2.Conversation.NpcNameY);
        mOPlayerName = fPiece("Name player", 0, ScreenHeight - 1 - UWClassicLayoutUw2.Conversation.PlayerNameY);

        for (int liAt = 0; liAt < UWScrollEdgeRules.ConversationRows; liAt++)
        {
            int liTop = ScreenHeight - 1 - UWClassicLayoutUw2.Conversation.EdgeFirstY + (liAt * UWClassicLayoutUw2.Conversation.EdgeHeight);

            mOLeftEdges[liAt] = fPiece("Edge left " + liAt, UWClassicLayoutUw2.Conversation.EdgeLeft, liTop);
            mORightEdges[liAt] = fPiece("Edge right " + liAt, UWClassicLayoutUw2.Conversation.EdgeRight, liTop);
        }

        mOMarkOn = fMark(true);
        mOMarkOff = fMark(false);

        for (int liSlot = 0; liSlot < UWConversationTrade.SlotCountUw2; liSlot++)
        {
            foreach (bool lbPlayer in new[] { false, true })
            {
                int liAt = (lbPlayer ? UWConversationTrade.SlotCountUw2 : 0) + liSlot;
                int liLeft = fSlotLeft(lbPlayer, liSlot);
                int liTop = ScreenHeight - 1 - fSlotTopY(liSlot);
                int liMark = ((liSlot & 1) != 0 ? UWClassicLayoutUw2.Conversation.MarkRightColumn : UWClassicLayoutUw2.Conversation.MarkLeftColumn)
                    + (lbPlayer ? UWClassicLayoutUw2.Conversation.MarkPlayerStep : 0);

                mOSlots[liAt] = fPiece("Slot " + liAt, liLeft, liTop);
                mOCounts[liAt] = fPiece("Count " + liAt, liLeft + UWClassicLayoutUw2.Conversation.CountRight, liTop + UWClassicLayoutUw2.Conversation.CountDown);

                // The cross's middle bar lies on the mark's row, its top two rows over it.
                mOMarks[liAt] = fPiece("Mark " + liAt, liMark - 1, ScreenHeight - 1 - (UWClassicLayoutUw2.Conversation.MarkTopY - ((liSlot / 2) * UWClassicLayoutUw2.Conversation.SlotRowStep)) - 2);
            }
        }

        mOHistory = fPiece("History", UWClassicLayoutUw2.Conversation.HistoryLeft, ScreenHeight - 1 - UWClassicLayoutUw2.Conversation.HistoryTopY);
        mOAnswers = fPiece("Answers", UWClassicLayoutUw2.Conversation.AnswersLeft, ScreenHeight - 1 - UWClassicLayoutUw2.Conversation.AnswersTopRow);
    }

    /// <summary>A rectangle of the screen's picture, top-left at (piLeft, piTop).</summary>
    private void fBackPiece(string psName, Texture2D pOScreen, int piLeft, int piTop, int piWidth, int piHeight)
    {
        RawImage lOImage = fPiece(psName, piLeft, piTop);

        lOImage.texture = pOScreen;
        lOImage.enabled = true;
        lOImage.rectTransform.sizeDelta = new Vector2(piWidth, piHeight);
        lOImage.uvRect = new Rect((float)piLeft / pOScreen.width, (float)(pOScreen.height - piTop - piHeight) / pOScreen.height,
            (float)piWidth / pOScreen.width, (float)piHeight / pOScreen.height);
    }
}
