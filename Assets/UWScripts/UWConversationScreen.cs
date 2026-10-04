using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The conversation screen of the original.
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
/// SetupConversationUI).
///
/// FONT: every text field is a UWTextLabel and shows the original font or the
/// modern one, depending on the choice made with key F (see UWGameUI.fCheckFontToggle).
///
/// Selection is made with the number keys 1 to 9 or by mouse click on the line.
///
/// THE MODERN SCHEME (stage 4, per user on a mockup, 2026-10-04) keeps this screen as the
/// conversation's driver - the session, the answers by key, the typed answer, the end - but hides
/// its canvas: UWModernConversation draws the conversation instead (portraits, trade areas, the
/// history on leather, the answers) and the modern bags do the trading. There the text never
/// pauses for [MORE] (the history scrolls), and a click chooses on the modern answer rows.
/// </summary>
public class UWConversationScreen : MonoBehaviour
{
    /// <summary>Reference resolution of the original.</summary>
    private const int ScreenWidth = 320;

    private const int ScreenHeight = 200;

    // --- Parchment
    private const int HistoryLeft = 59;

    private const int HistoryTop = 53;

    /// <summary>Up to just before the right vine edge (220). 159, not 158 (per user with a
    /// screenshot of the original, 2026-09-30): Ketchaval's "the last holding of the noble
    /// Goblins," is 159 pixels in font5x6p and stays on one line there, while every line the
    /// original wrapped would have run to 160 or more with its next word.</summary>
    private const int HistoryWidth = 159;

    /// <summary>Thirteen six-pixel lines, down to row 130 - the lower edge starts at 127,
    /// so the last line already reaches into it. The file's sample does not get that far (its
    /// fifth line does not), but the reference's template goes down to 130.</summary>
    private const int HistoryHeight = 78;

    // --- Scroll (same measurements as the message box in UWGameUI)
    private const int ChoicesLeft = 15;

    private const int ChoicesTop = 169;

    private const int ChoicesWidth = 291;

    private const int ChoicesHeight = 30;

    // --- Names and portraits
    private const int NpcNameLeft = 48;

    private const int PlayerNameLeft = 144;

    private const int NameTop = 2;

    private const int NameWidth = 88;

    private const int NpcPortraitLeft = 45;

    private const int PlayerPortraitLeft = 197;

    private const int PortraitTop = 11;

    // --- Vine edges
    private const int EdgeLeft = 52;

    private const int EdgeRight = 220;

    private static readonly int[] miEdgeTops = { 51, 78, 105 };

    /// <summary>How many frames the vine edges run through - UWScrollEdgeRules holds the
    /// strips and the count, the original counts them in dseg_5c99_A96.</summary>
    private const int EdgeFrames = UWScrollEdgeRules.ConversationFrames;

    /// <summary>Palette colour of the names (reference: uimanager.CharNameColour, 0x65).</summary>
    private const int NameColourIndex = 0x65;

    /// <summary>Text colour of the parchment - the sample text in CONV.BYT is index 0x2E.</summary>
    private static readonly Color32 msNpcColour = new Color32(0x3C, 0x28, 0x20, 255);

    /// <summary>The player's own answers, reddish brown (reference: MessageDisplay PC_SAY #883E14).
    /// </summary>
    private static readonly Color32 msPlayerColour = new Color32(0x88, 0x3E, 0x14, 255);

    /// <summary>Narrator text (print), black as in the reference.</summary>
    private static readonly Color32 msNarratorColour = new Color32(0, 0, 0, 255);

    /// <summary>UI hints, white like [MORE] in the message box.</summary>
    private static readonly Color32 msUiColour = new Color32(255, 255, 255, 255);

    /// <summary>Characters with their own face in CHARHEAD.GR.</summary>
    private const int LastNamedPortrait = 28;

    /// <summary>Female bodies lie in HEADS.GR five places after the male ones.
    /// </summary>
    private const int FemalePortraitOffset = 5;

    private DataImport mOData;
    private UWConversationSession mOSession;
    private UWGameUI mOGameUi;
    private UWControlScheme mOControlScheme;

    private Canvas mOCanvas;
    private RectTransform mOFrame;

    private RawImage mONpcNameImage;
    private RawImage mOPlayerNameImage;
    private Image mONpcPortrait;
    private Image mOPlayerPortrait;
    private readonly Image[] mOEdges = new Image[2 * UWScrollEdgeRules.ConversationRows];

    /// <summary>The frame of the vine edges, as dseg_5c99_A96 counts it in the original: one
    /// step per scroll of the parchment, never reset, so the phase carries from one
    /// conversation into the next.</summary>
    private int miEdgeStep;

    /// <summary>The lines the parchment showed last time - see
    /// UWScrollEdgeRules.GetScrollAmount.</summary>
    private List<string> mOLastShownLines = new List<string>();

    // --- Trade areas (CONVERSE.GR image 1, 55x38, at 82/9 and 139/9; the circles in them
    //     are 16 wide, the crosses at the edges 3 by 3). THE SLOTS ARE THE ORIGINAL'S since
    //     2026-09-26: its tables in the data segment (0xD54 the player's, 0xD74 the partner's,
    //     read from UW.EXE) put both lower rows on row 29 - the partner's lay on 28 - and one
    //     slot answers a click on 17 by 17 pixels, both edges included (ovr095_52F, ovr095_4D3).
    //     The crosses were left: the tables at 0xD64 and 0xD84 would put them one pixel right
    //     and down, but where the original uses them is not found.
    private static readonly Vector2Int[] miNpcTradeSlots =
    {
        new Vector2Int(91, 11), new Vector2Int(112, 11), new Vector2Int(91, 29), new Vector2Int(112, 29)
    };

    private static readonly Vector2Int[] miPlayerTradeSlots =
    {
        new Vector2Int(148, 11), new Vector2Int(169, 11), new Vector2Int(148, 29), new Vector2Int(169, 29)
    };

    private static readonly Vector2Int[] miNpcTradeCrosses =
    {
        new Vector2Int(87, 17), new Vector2Int(129, 17), new Vector2Int(87, 35), new Vector2Int(129, 35)
    };

    private static readonly Vector2Int[] miPlayerTradeCrosses =
    {
        new Vector2Int(144, 17), new Vector2Int(186, 17), new Vector2Int(144, 35), new Vector2Int(186, 35)
    };

    private const int TradeSlotSize = 16;

    /// <summary>Three by three pixels, as in the original (per user, 2026-09-18; until then five, the
    /// positions above moved one pixel in so that the centre stays).</summary>
    private const int TradeCrossSize = 3;

    /// <summary>A bit more generous than the cross itself, so that it can be hit.</summary>
    private const int TradeCrossHit = 4;

    private readonly Image[] mONpcTradeImages = new Image[UWConversationTrade.SlotCount];
    private readonly Image[] mOPlayerTradeImages = new Image[UWConversationTrade.SlotCount];
    private readonly Image[] mONpcTradeCrossImages = new Image[UWConversationTrade.SlotCount];
    private readonly Image[] mOPlayerTradeCrossImages = new Image[UWConversationTrade.SlotCount];

    /// <summary>The count on a stack, top left of the place as in the backpack (see
    /// UWGameUI.fSetStackLabel).</summary>
    private readonly RawImage[] mONpcTradeCounts = new RawImage[UWConversationTrade.SlotCount];
    private readonly RawImage[] mOPlayerTradeCounts = new RawImage[UWConversationTrade.SlotCount];
    private Sprite mOCrossSprite;
    private int miTradeVersionShown = -1;

    private Interaction mOInteraction;
    private bool mbChoicesHidden;

    /// <summary>The running conversation - for UWItemDrag (trade area).</summary>
    public UWConversationSession Session
    {
        get { return mOSession; }
    }

    private RawImage mOHistoryImage;
    private RawImage mOChoiceImage;
    private Texture2D mOHistoryTexture;
    private Texture2D mOChoiceTexture;

    private readonly List<RectTransform> mOChoiceHitboxes = new List<RectTransform>();

    /// <summary>Is a conversation running right now? While it is, it blocks world interaction.</summary>
    public bool IsOpen
    {
        get { return mOSession != null; }
    }

    /// <summary>Is a conversation running anywhere? In the original the world stands still
    /// meanwhile - the creatures follow this as well (see UWCritter).</summary>
    public static bool IsAnyOpen { get; private set; }

    /// <summary>Is the player currently typing an answer (babl_ask)? Then the keys belong
    /// to the text, not to the commands (see UWGameUI.fCheckFontToggle).</summary>
    public static bool IsTypingAnswer { get; private set; }

    /// <summary>The typed text, while the question is open.</summary>
    private string msTypedAnswer = string.Empty;

    private bool mbTypingHooked;

    private const int TypedAnswerMaxLength = 40;

    private const string TypedAnswerPrompt = ">";

    /// <summary>Starts a conversation with this NPC.</summary>
    public bool Begin(DataImport pOData, UWNpc pONpc, int piSlot)
    {
        if (pOData == null || pOData.Conversations == null || !pOData.Conversations.IsLoaded)
            return false;

        mOData = pOData;
        // The session is engine-free; UWConversationHost hands it the player, the world and
        // the inventory from the scene (P3 of the engine separation, 2026-09-18).
        mOSession = new UWConversationSession(pOData, pONpc, piSlot, new UWConversationHost());

        if (mOSession.IsFinished && mOSession.Lines.Count == 0)
        {
            mOSession = null;
            return false;
        }

        fEnsureCanvas();
        fResetHistory();

        mOCanvas.gameObject.SetActive(true);

        IsAnyOpen = true;
        mbClosing = false;
        mbPlayerAnswered = false;
        miOpenedFrame = Time.frameCount;

        // THE WORLD STANDS STILL: in the original the conversation is a modal loop and the game
        // loop does not run until it returns - the palette cycling of the spell icons stops, the
        // water, the creatures, the weapon (per user on the original, 2026-09-18). One hold on the
        // game clock does all of that; this screen runs on the unscaled clock.
        UWGameClock.Hold(UWGameClock.ConversationHold);

        if (mOGameUi == null)
            mOGameUi = GetComponent<UWGameUI>();

        if (mOGameUi != null)
            mOGameUi.SetConversationFrame(true);

        // Pointer free, movement and look off - but the inventory stays usable, as in the
        // original (trading). Without the lock the player would keep walking down the corridor
        // during the conversation.
        if (mOControlScheme == null)
            mOControlScheme = GetComponentInParent<UWControlScheme>();

        if (mOControlScheme != null)
            mOControlScheme.IsConversationOpen = true;

        fShowPortraits(pONpc, piSlot);
        fShowNames();
        fDrawTrade();

        // "Maps & Legends" plays during conversations (reference: conversationinitialisation).
        UWMusic.ChangeTheme(UWMusic.MapsAndLegendsTheme);

        mOSession.Advance();
        fRedraw();

        return true;
    }

    private bool mbClosing;

    /// <summary>Did the conversation offer the player an answer (menu or typed text) at any
    /// point? Decides how it ends, see fCloseWithFade.</summary>
    private bool mbPlayerAnswered;

    private const float EndHoldSeconds = 2f;

    private const float FadeSeconds = 1f;

    private System.Collections.IEnumerator fCloseWithFade()
    {
        // A click or a key triggers the fade immediately (per user on the original,
        // 2026-09-12); otherwise the waiting time runs out.
        //
        // WITHOUT ANY ANSWER THERE IS NO WAITING TIME: a conversation that never offered the
        // player a choice stays open until a key or click (per user on the original,
        // 2026-09-14, Biden handing over the scroll).
        for (float lfAt = 0f; !mbPlayerAnswered || lfAt < EndHoldSeconds; lfAt += Time.unscaledDeltaTime)
        {
            yield return null;

            if (fAnyInput())
                break;
        }

        UWScreenUi.ShowLoadingCover();

        for (float lfAt = 0f; lfAt < FadeSeconds; lfAt += Time.unscaledDeltaTime)
        {
            UWScreenUi.SetLoadingCoverAlpha(lfAt / FadeSeconds);
            yield return null;
        }

        UWScreenUi.SetLoadingCoverAlpha(1f);

        Close();

        // The box is empty after the conversation.
        if (mOInteraction == null)
            mOInteraction = GetComponent<Interaction>();

        if (mOInteraction != null)
            mOInteraction.ResetMessages();

        // While the main display comes back, controls stay off - Close released
        // them, here the conversation still counts as open.
        if (mOControlScheme != null)
            mOControlScheme.IsConversationOpen = true;

        for (float lfAt = 0f; lfAt < FadeSeconds; lfAt += Time.unscaledDeltaTime)
        {
            UWScreenUi.SetLoadingCoverAlpha(1f - (lfAt / FadeSeconds));
            yield return null;
        }

        UWScreenUi.HideLoadingCover();

        if (mOControlScheme != null)
            mOControlScheme.IsConversationOpen = false;

        mbClosing = false;
    }

    public void Close()
    {
        if (IsTypingAnswer)
            fEndTyping();

        if (mOSession != null)
        {
            mOSession.Abort();
            fRefreshCritterAfterTalk(mOSession.Npc);
        }

        mOSession = null;
        IsAnyOpen = false;
        UWGameClock.Release(UWGameClock.ConversationHold);

        // Afterwards a level theme again (reference: conversationvm, end of conversation).
        UWMusic.PickLevelTheme();

        if (mOControlScheme != null)
            mOControlScheme.IsConversationOpen = false;

        if (mOGameUi != null)
            mOGameUi.SetConversationFrame(false);

        if (mOCanvas != null)
            mOCanvas.gameObject.SetActive(false);
    }

    /// <summary>After the conversation the creature rereads attitude and goal from its data -
    /// the conversation may have set both (see UWConversationSession.fWriteBackGlobals).
    /// </summary>
    private static void fRefreshCritterAfterTalk(UWNpc pONpc)
    {
        if (pONpc == null)
            return;

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWEntityInfo lOEntity;

        if (lOLoader == null || !lOLoader.TryGetEntity(pONpc, out lOEntity) || lOEntity == null)
            return;

        UWCritter lOCritter = lOEntity.GetComponentInParent<UWCritter>();

        if (lOCritter != null)
            lOCritter.RefreshFromData();
    }

    private void Update()
    {
        fFollowHelpLayout();

        // The modern scheme draws the conversation itself (UWModernConversation).
        if (mOCanvas != null)
            mOCanvas.enabled = !fIsModern();

        if (mOSession == null)
            return;

        if (mOSession.Trade != null && mOSession.Trade.Version != miTradeVersionShown)
            fDrawTrade();

        if (mOSession.Choices.Count > 0 || mOSession.IsAskingText)
            mbPlayerAnswered = true;

        // The look text gives way after its two seconds - or at once on a left click, which
        // only brings the answers back and chooses nothing (per user on the original,
        // 2026-09-18).
        // THE PRESS THAT SHOWED IT does not cut it short: the modern context menu's Look acts on the
        // press, and its release came a frame later and took the text away at once (per user,
        // 2026-10-04).
        bool lbLookRelease = mfLookTextUntil >= 0f && UWMouseButtons.LeftReleased;

        if (lbLookRelease && mbLookIgnoresRelease)
        {
            mbLookIgnoresRelease = false;
            lbLookRelease = false;
        }

        bool lbLookTextCutShort = lbLookRelease;

        if (lbLookTextCutShort)
            miLookTextCutFrame = Time.frameCount;

        if (mfLookTextUntil >= 0f && (Time.unscaledTime >= mfLookTextUntil || lbLookTextCutShort))
        {
            mfLookTextUntil = -1f;

            UWFont lOLookFont = mOData.Fonts.Get(UWFonts.FontType.Normal);

            if (lOLookFont != null && !mbChoicesHidden)
                fDrawChoices(lOLookFont);
        }

        if (lbLookTextCutShort)
            return;

        // During an input in the scroll ("Move how many?") the answers give way;
        // afterwards they come back. Meanwhile no selection runs either.
        if (mOInteraction == null)
            mOInteraction = GetComponent<Interaction>();

        bool lbPrompt = mOInteraction != null && mOInteraction.IsPromptActive;

        if (lbPrompt != mbChoicesHidden)
        {
            mbChoicesHidden = lbPrompt;

            if (lbPrompt)
                UWTextLabel.Hide(mOChoiceImage);
            else
            {
                UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Normal);

                if (lOFont != null)
                    fDrawChoices(lOFont);
            }
        }

        if (lbPrompt)
            return;

        if (mOSession.IsAskingText)
        {
            fUpdateTypedAnswer();
            return;
        }

        if (IsTypingAnswer)
            fEndTyping();

        // "[MORE]": any key or mouse button fetches the rest of the text.
        if (mbMorePending)
        {
            if (fAnyInput())
                fContinueAfterMore();

            return;
        }

        // A finished conversation stays up for about two seconds, then the picture fades
        // to black in one second, the conversation window closes, and in another
        // second the main display comes back out of the black - without a click, and the
        // message box is empty afterwards (per user on the original, 2026-09-12).
        if (mOSession.IsFinished && mOSession.Choices.Count == 0)
        {
            if (!mbClosing)
            {
                mbClosing = true;
                StartCoroutine(fCloseWithFade());
            }

            return;
        }

        int liChoice = fReadChoice();

        if (liChoice <= 0)
            return;

        mOSession.Choose(liChoice);
        fRedraw();
    }

    // ------------------------------------------------------------------
    // Typed answer (babl_ask)
    // ------------------------------------------------------------------

    /// <summary>
    /// The conversation asks for a text - a password, a name. The scroll shows
    /// ">" followed by the typed text with an underscore as cursor, as with the
    /// name entry when saving. Enter submits, Escape submits empty (the
    /// conversation has to go on after all), Backspace deletes.
    /// </summary>
    private void fUpdateTypedAnswer()
    {
        Keyboard lOKeyboard = Keyboard.current;

        if (!IsTypingAnswer)
        {
            IsTypingAnswer = true;
            msTypedAnswer = string.Empty;

            if (lOKeyboard != null && !mbTypingHooked)
            {
                lOKeyboard.onTextInput += fOnAnswerTextInput;
                mbTypingHooked = true;
            }

            fDrawTypedAnswer();
        }

        if (lOKeyboard == null || IsKeyboardTaken)
            return;

        if (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame)
        {
            string lsAnswer = msTypedAnswer;

            fEndTyping();
            mOSession.SupplyText(lsAnswer);
            fRedraw();
            return;
        }

        if (lOKeyboard.escapeKey.wasPressedThisFrame)
        {
            fEndTyping();
            mOSession.SupplyText(string.Empty);
            fRedraw();
            return;
        }

        if (lOKeyboard.backspaceKey.wasPressedThisFrame && msTypedAnswer.Length > 0)
        {
            msTypedAnswer = msTypedAnswer.Substring(0, msTypedAnswer.Length - 1);
            fDrawTypedAnswer();
        }
    }

    private void fOnAnswerTextInput(char pcChar)
    {
        if (!IsTypingAnswer || IsKeyboardTaken || pcChar < 0x20 || pcChar > 0x7E
            || msTypedAnswer.Length >= TypedAnswerMaxLength)
            return;

        msTypedAnswer += pcChar;
        fDrawTypedAnswer();
    }

    private void fEndTyping()
    {
        IsTypingAnswer = false;

        if (mbTypingHooked && Keyboard.current != null)
            Keyboard.current.onTextInput -= fOnAnswerTextInput;

        mbTypingHooked = false;
    }

    private void fDrawTypedAnswer()
    {
        UWFont lOFont = mOData != null ? mOData.Fonts.Get(UWFonts.FontType.Normal) : null;

        if (lOFont == null)
            return;

        fBuildHitboxes(lOFont, new List<int>());
        fRenderInto(ref mOChoiceTexture, mOChoiceImage, lOFont,
            new List<string> { TypedAnswerPrompt + msTypedAnswer + "_" },
            new List<Color32> { msNpcColour }, ChoicesWidth);
    }

    /// <summary>
    /// THE HELP STAYS OPEN IN A CONVERSATION (per user, 2026-09-26). Its frame then slides left
    /// with the game frame, every frame, like the gargoyle eyes (the 4:3 squeeze comes through
    /// UWPixelAspectFrame); clicks follow by themselves through ScreenPointToLocalPointInRectangle.
    /// </summary>
    private void fFollowHelpLayout()
    {
        if (mOFrame != null)
            mOFrame.anchoredPosition = new Vector2(UWHelpLayout.FrameShift(UWUiFit.CanvasWidth), 0f);
    }

    /// <summary>While the help has the keyboard (typing in its notes, reading the manual) the
    /// conversation hears no key - a digit in the notes must not choose an answer.</summary>
    private static bool IsKeyboardTaken => UWHelpWindow.BlocksGameKeys;

    /// <summary>Number key or click on an answer line. 0 means: nothing selected.</summary>
    private int fReadChoice()
    {
        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard != null && !IsKeyboardTaken)
        {
            for (int liIndex = 0; liIndex < mOSession.Choices.Count && liIndex < 9; liIndex++)
            {
                if (lOKeyboard[Key.Digit1 + liIndex].wasPressedThisFrame)
                    return liIndex + 1;
            }
        }

        Mouse lOMouse = Mouse.current;

        // ON THE RELEASE of either button, like everything during a conversation (per user on
        // the original, 2026-09-18: the right button chooses an answer too) - and not in the
        // frame the conversation opened in, that release belongs to the drag that started it.
        if (lOMouse == null || !(UWMouseButtons.LeftReleased || UWMouseButtons.RightReleased)
            || miOpenedFrame == Time.frameCount)
            return 0;

        Vector2 lOPosition = lOMouse.position.ReadValue();

        // The modern scheme's rows (UWModernConversation): the left button only - the right one
        // is the bags' there (look, the containers).
        if (fIsModern())
        {
            UWModernConversation lOModern = UWModernConversation.Instance;

            return lOModern != null && UWMouseButtons.LeftReleased ? lOModern.ChoiceAt(lOPosition) : 0;
        }

        for (int liIndex = 0; liIndex < mOChoiceHitboxes.Count; liIndex++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(mOChoiceHitboxes[liIndex], lOPosition, null))
                return liIndex + 1;
        }

        return 0;
    }

    /// <summary>The frame the conversation opened in: the release of the drag that started it
    /// must not count as input here.</summary>
    private int miOpenedFrame = -1;

    private bool fAnyInput()
    {
        Keyboard lOKeyboard = Keyboard.current;

        // The F belongs to the font toggle (see UWGameUI) and should not click the
        // conversation away along with it; Tab opens and closes the help.
        if (lOKeyboard != null && !IsKeyboardTaken && lOKeyboard.anyKey.wasPressedThisFrame
            && !lOKeyboard.fKey.wasPressedThisFrame && !lOKeyboard.tabKey.wasPressedThisFrame)
            return true;

        Mouse lOMouse = Mouse.current;

        // The mouse acts on the release during a conversation (per user on the original, 2026-09-18).
        return lOMouse != null && miOpenedFrame != Time.frameCount
            && (UWMouseButtons.LeftReleased || UWMouseButtons.RightReleased);
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    private void fRedraw()
    {
        UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Normal);

        if (lOFont == null)
            return;

        fDrawHistory(lOFont);

        // While the text is waiting there is nothing to choose - the answers only come
        // once everything has been said.
        if (mbMorePending)
            UWTextLabel.Hide(mOChoiceImage);
        else
            fDrawChoices(lOFont);
    }

    /// <summary>The colour of a history entry: partner, player or narrator.</summary>
    private static Color32 fGetLineColour(UWConversationSession.Line pOLine)
    {
        if (pOLine.IsNpc)
            return msNpcColour;

        return pOLine.IsPlayer ? msPlayerColour : msNarratorColour;
    }

    // ------------------------------------------------------------------
    // History with [MORE]
    // ------------------------------------------------------------------

    /// <summary>A line that is not yet in the parchment.</summary>
    private struct PendingLine
    {
        public string Text;
        public Color32 Colour;

        /// <summary>After this line the text pauses - a "\m" in the original.</summary>
        public bool BreakAfter;
    }

    /// <summary>What is in the parchment, line by line, oldest first.</summary>
    private readonly List<string> mOShownLines = new List<string>();

    private readonly List<Color32> mOShownColours = new List<Color32>();

    private readonly Queue<PendingLine> mOPendingLines = new Queue<PendingLine>();

    /// <summary>How many history lines have already been processed.</summary>
    private int miLinesConsumed;

    /// <summary>Lines since the last pause within ONE text.</summary>
    private int miLinesSincePause;

    /// <summary>"[MORE]" is shown at the bottom, the text is waiting for a click.</summary>
    private bool mbMorePending;

    private const string MoreMarker = "[MORE]";

    /// <summary>The markers in the text at which the original pauses: "\m" is documented
    /// (reference MessageDisplay), "\p" appears alongside it in the data (Ben on level 2) and
    /// is treated the same way - whether the original pauses there is not documented.</summary>
    private static readonly string[] msPageBreaks = { "\\m", "\\p" };

    private static readonly string[] msLineBreaks = { "\r\n", "\n" };

    /// <summary>
    /// A LINE BREAK IN THE TEXT STARTS A NEW LINE, and what follows keeps its leading space (per
    /// user with a screenshot of the original, 2026-09-30: Ketchaval's "the Gray tribe.\n Years
    /// ago," - the original ends the line after "tribe." and indents " Years ago," by that
    /// space). Ours wrapped over it, the original font drawing the break as nothing.
    /// </summary>
    private static List<string> fWrapWithBreaks(UWFont pOFont, string psText)
    {
        List<string> lOLines = new List<string>();

        foreach (string lsPart in psText.Split(msLineBreaks, System.StringSplitOptions.None))
            lOLines.AddRange(UWFontRenderer.WrapText(pOFont, lsPart, HistoryWidth));

        return lOLines;
    }

    /// <summary>
    /// Shows the history like the original: the parchment holds thirteen lines and pushes
    /// older ones out at the top. If ONE text needs more than twelve lines, "[MORE]" appears after
    /// twelve and the rest waits for a click - the same at every "\m" in the
    /// text (reference: MessageDisplay.AddText, Rows 13). Until 2026-09-11 the history showed
    /// only the last lines, and for long speeches the beginning was missing.
    /// </summary>
    private void fDrawHistory(UWFont pOFont)
    {
        fQueueNewLines(pOFont);
        fRevealLines(pOFont);
        fRenderHistory(pOFont);
    }

    private void fQueueNewLines(UWFont pOFont)
    {
        for (; miLinesConsumed < mOSession.Lines.Count; miLinesConsumed++)
        {
            UWConversationSession.Line lOLine = mOSession.Lines[miLinesConsumed];
            Color32 lOColour = fGetLineColour(lOLine);

            string[] lsSegments = (lOLine.Text ?? string.Empty).Split(msPageBreaks, System.StringSplitOptions.None);

            for (int liSegment = 0; liSegment < lsSegments.Length; liSegment++)
            {
                List<string> lOWrapped = fWrapWithBreaks(pOFont, lsSegments[liSegment].Trim());

                for (int liAt = 0; liAt < lOWrapped.Count; liAt++)
                {
                    mOPendingLines.Enqueue(new PendingLine
                    {
                        Text = lOWrapped[liAt],
                        Colour = lOColour,
                        BreakAfter = liAt == lOWrapped.Count - 1 && liSegment < lsSegments.Length - 1
                    });
                }
            }

            // A new text counts its lines from the start; the limit applies per text.
            mOPendingLines.Enqueue(new PendingLine { Text = null });
        }
    }

    /// <summary>Brings lines into the parchment until a pause is due.</summary>
    private void fRevealLines(UWFont pOFont)
    {
        int liCapacity = fGetHistoryCapacity(pOFont);

        while (!mbMorePending && mOPendingLines.Count > 0)
        {
            PendingLine lOLine = mOPendingLines.Dequeue();

            // End of text: the counter starts over.
            if (lOLine.Text == null)
            {
                miLinesSincePause = 0;
                continue;
            }

            // The modern history scrolls: nothing waits for a click there.
            if (miLinesSincePause >= liCapacity - 1 && !fIsModern())
            {
                // Back to the front of the queue - only the click fetches it.
                fRequeueFront(lOLine);
                mbMorePending = true;
                break;
            }

            mOShownLines.Add(lOLine.Text);
            mOShownColours.Add(lOLine.Colour);
            miLinesSincePause++;

            if (lOLine.BreakAfter && !fIsModern())
                mbMorePending = true;
        }

        if (mbMorePending && mOPendingLines.Count == 0)
            mbMorePending = false;
    }

    private void fRequeueFront(PendingLine pOLine)
    {
        List<PendingLine> lORest = new List<PendingLine>(mOPendingLines);

        mOPendingLines.Clear();
        mOPendingLines.Enqueue(pOLine);

        foreach (PendingLine lOOther in lORest)
            mOPendingLines.Enqueue(lOOther);
    }

    private int fGetHistoryCapacity(UWFont pOFont)
    {
        int liLineHeight = pOFont.Height + UWFontRenderer.LineSpacing;

        return Mathf.Max(2, HistoryHeight / liLineHeight);
    }

    private void fRenderHistory(UWFont pOFont)
    {
        int liCapacity = fGetHistoryCapacity(pOFont);

        List<string> lOLines = new List<string>(mOShownLines);
        List<Color32> lOColours = new List<Color32>(mOShownColours);

        if (mbMorePending)
        {
            lOLines.Add(MoreMarker);
            lOColours.Add(msUiColour);
        }

        while (lOLines.Count > liCapacity)
        {
            lOLines.RemoveAt(0);
            lOColours.RemoveAt(0);
        }

        fRollEdges(lOLines);

        fRenderInto(ref mOHistoryTexture, mOHistoryImage, pOFont, lOLines, lOColours, HistoryWidth);
    }

    /// <summary>The click on "[MORE]": continue with the text.</summary>
    private void fContinueAfterMore()
    {
        mbMorePending = false;
        miLinesSincePause = 0;

        fRedraw();
    }

    private void fResetHistory()
    {
        mOShownLines.Clear();
        mOShownColours.Clear();
        mOPendingLines.Clear();
        miLinesConsumed = 0;
        miLinesSincePause = 0;
        mbMorePending = false;

        // Clearing the parchment is worth ONE step in the original as well: a conversation
        // starts through seg043_37F0_8EB, and that ends in the same fork as a scroll.
        mOLastShownLines.Clear();
        miEdgeStep = UWScrollEdgeRules.Advance(miEdgeStep, EdgeFrames);

        fApplyEdges();
    }

    /// <summary>
    /// LOOKING AT A TRADE ITEM shows its description in the answer scroll for about two seconds,
    /// then the answers come back (per user from the original, 2026-09-17). Until then the text
    /// went into the conversation history, where nothing seemed to happen and it only turned up
    /// in black after the trade.
    /// </summary>
    public void ShowLookText(string psText)
    {
        UWFont lOFont = mOData != null ? mOData.Fonts.Get(UWFonts.FontType.Normal) : null;

        if (lOFont == null || string.IsNullOrEmpty(psText))
            return;

        List<string> lOLines = new List<string>();
        List<Color32> lOColours = new List<Color32>();

        foreach (string lsWrapped in UWFontRenderer.WrapText(lOFont, psText, ChoicesWidth))
        {
            lOLines.Add(lsWrapped);
            lOColours.Add(msNpcColour);
        }

        fRenderInto(ref mOChoiceTexture, mOChoiceImage, lOFont, lOLines, lOColours, ChoicesWidth);
        mfLookTextUntil = Time.unscaledTime + LookTextSeconds;
        msLookText = psText;
        mbLookIgnoresRelease = UWMouseButtons.LeftHeld;
    }

    /// <summary>The look text came from a left press still held - its release is not the click
    /// that cuts the text short.</summary>
    private bool mbLookIgnoresRelease;

    /// <summary>How long a look text replaces the answers.</summary>
    private const float LookTextSeconds = 2f;

    /// <summary>Until when a look text is shown; below zero none is.</summary>
    private float mfLookTextUntil = -1f;

    /// <summary>The frame in which a left click cut the look text short.</summary>
    private int miLookTextCutFrame = -1;

    /// <summary>The left click that cuts the look text short is used up: it chooses no answer
    /// and nothing on the trade areas either (per user on the original, 2026-09-18). True while
    /// the text shows and in the frame it was cut - UWItemDrag asks before it acts on a
    /// release, whichever of the two Updates runs first.</summary>
    public bool ConsumesLeftClick
    {
        get { return mfLookTextUntil >= 0f || miLookTextCutFrame == Time.frameCount; }
    }

    private void fDrawChoices(UWFont pOFont)
    {
        List<string> lOLines = new List<string>();
        List<Color32> lOColours = new List<Color32>();
        List<int> lOLineOfChoice = new List<int>();

        for (int liIndex = 0; liIndex < mOSession.Choices.Count; liIndex++)
        {
            lOLineOfChoice.Add(lOLines.Count);

            foreach (string lsWrapped in UWFontRenderer.WrapText(pOFont, fFormatChoice(liIndex), ChoicesWidth))
            {
                lOLines.Add(lsWrapped);
                lOColours.Add(msNpcColour);
            }
        }

        fRenderInto(ref mOChoiceTexture, mOChoiceImage, pOFont, lOLines, lOColours, ChoicesWidth);
        fBuildHitboxes(pOFont, lOLineOfChoice);
    }

    /// <summary>The original numbers with a period: "1. Don't mention it." (that is how it appears in the
    /// sample in CONV.BYT).</summary>
    private string fFormatChoice(int piIndex)
    {
        return (piIndex + 1) + ". " + mOSession.Choices[piIndex];
    }


    /// <summary>
    /// Click areas over the answer lines. They lie invisibly on the choice display;
    /// an answer that runs over two lines gets a correspondingly tall area.
    /// </summary>
    private void fBuildHitboxes(UWFont pOFont, List<int> pOLineOfChoice)
    {
        while (mOChoiceHitboxes.Count > pOLineOfChoice.Count)
        {
            RectTransform lOLast = mOChoiceHitboxes[mOChoiceHitboxes.Count - 1];

            mOChoiceHitboxes.RemoveAt(mOChoiceHitboxes.Count - 1);

            if (lOLast != null)
                Destroy(lOLast.gameObject);
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

            int liNextLine = liIndex + 1 < pOLineOfChoice.Count
                ? pOLineOfChoice[liIndex + 1]
                : pOLineOfChoice[liIndex] + 1;

            RectTransform lORect = mOChoiceHitboxes[liIndex];

            lORect.sizeDelta = new Vector2(0f, (liNextLine - pOLineOfChoice[liIndex]) * liLineHeight);
            lORect.anchoredPosition = new Vector2(0f, -pOLineOfChoice[liIndex] * liLineHeight);
        }
    }

    private void fRenderInto(ref Texture2D pOTexture, RawImage pOImage, UWFont pOFont,
        List<string> pOLines, List<Color32> pOColours, int piWidth)
    {
        if (pOImage == null)
            return;

        if (pOLines.Count == 0)
        {
            UWTextLabel.Hide(pOImage);
            return;
        }

        // UWFontRenderer draws a whole list in ONE colour. For multi-coloured output
        // it is therefore drawn line by line and stacked vertically.
        int liLineHeight = pOFont.Height + UWFontRenderer.LineSpacing;
        int liHeight = Mathf.Max(1, pOLines.Count * liLineHeight);

        if (pOTexture == null || pOTexture.width != piWidth || pOTexture.height != liHeight)
        {
            if (pOTexture != null)
                Destroy(pOTexture);

            pOTexture = new Texture2D(piWidth, liHeight, TextureFormat.RGBA32, false);
            pOTexture.name = "UWConversationScreen.cs:870";
            pOTexture.filterMode = FilterMode.Point;
            pOTexture.wrapMode = TextureWrapMode.Clamp;
        }

        Color32[] lOPixels = new Color32[piWidth * liHeight];

        for (int liLine = 0; liLine < pOLines.Count; liLine++)
        {
            Texture2D lOLine = UWFontRenderer.RenderLines(pOFont,
                new List<string> { pOLines[liLine] }, piWidth, pOColours[liLine], FilterMode.Point, false);

            if (lOLine == null)
                continue;

            Color32[] lOSource = lOLine.GetPixels32();

            // The first line belongs at the top: Unity counts from the bottom.
            int liTargetTop = liHeight - ((liLine + 1) * liLineHeight);

            for (int y = 0; y < lOLine.height && y < liLineHeight; y++)
            {
                int liTargetY = liTargetTop + y;

                if (liTargetY < 0 || liTargetY >= liHeight)
                    continue;

                for (int x = 0; x < lOLine.width && x < piWidth; x++)
                {
                    Color32 lOPixel = lOSource[(y * lOLine.width) + x];

                    if (lOPixel.a > 0)
                        lOPixels[(liTargetY * piWidth) + x] = lOPixel;
                }
            }

            Destroy(lOLine);
        }

        pOTexture.SetPixels32(lOPixels);
        pOTexture.Apply(false, false);

        UWTextLabel.Get(pOImage).Set(pOFont, pOTexture, pOLines, pOColours, false);
    }

    // ------------------------------------------------------------------
    // Names, portraits, edges
    // ------------------------------------------------------------------

    private void fShowNames()
    {
        UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Normal);

        if (lOFont == null)
            return;

        Color32 lOColour = fGetPaletteColour(NameColourIndex);

        string lsPlayer = mOData.InitialPlayer != null && !string.IsNullOrEmpty(mOData.InitialPlayer.Name)
            ? mOData.InitialPlayer.Name
            : string.Empty;

        fSetNameImage(mONpcNameImage, lOFont, mOSession.PartnerName, lOColour);
        fSetNameImage(mOPlayerNameImage, lOFont, lsPlayer, lOColour);
    }

    private static void fSetNameImage(RawImage pOImage, UWFont pOFont, string psName, Color32 pOColour)
    {
        if (pOImage.texture != null)
            Destroy(pOImage.texture);

        if (string.IsNullOrEmpty(psName))
        {
            UWTextLabel.Hide(pOImage);
            return;
        }

        Texture2D lOTexture = UWFontRenderer.RenderLines(pOFont, new List<string> { psName }, NameWidth, pOColour, FilterMode.Point, false);

        if (lOTexture == null)
        {
            UWTextLabel.Hide(pOImage);
            return;
        }

        UWTextLabel.Get(pOImage).Set(pOFont, lOTexture, new List<string> { psName }, new List<Color32> { pOColour }, false);
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

    /// <summary>The partner's portrait (CHARHEAD or GENHEAD) and the player's (HEADS) - for the
    /// modern conversation (UWModernConversation).</summary>
    public UWTexture GetPortrait(bool pbPlayer)
    {
        if (mOData == null || mOData.Textures == null)
            return null;

        try
        {
            return pbPlayer
                ? mOData.Textures.GetTextureByType(UWTexture.TextureTypes.HEADS, miPlayerPortrait)
                : mOData.Textures.GetTextureByType(mePartnerPortraitType, miPartnerPortrait);
        }
        catch
        {
            return null;
        }
    }

    private UWTexture.TextureTypes mePartnerPortraitType = UWTexture.TextureTypes.GENHEAD;

    private int miPartnerPortrait;

    private int miPlayerPortrait;

    /// <summary>The player character's name, as the classic screen shows it.</summary>
    public string PlayerName => mOData != null && mOData.InitialPlayer != null && !string.IsNullOrEmpty(mOData.InitialPlayer.Name)
        ? mOData.InitialPlayer.Name : string.Empty;

    /// <summary>The typed answer so far (babl_ask), while IsTypingAnswer.</summary>
    public string TypedAnswer => msTypedAnswer;

    /// <summary>The look text replacing the answers for its two seconds (ShowLookText), else null.</summary>
    public string LookText => mfLookTextUntil >= 0f ? msLookText : null;

    private string msLookText;

    private bool fIsModern()
    {
        if (mOControlScheme == null)
            mOControlScheme = GetComponentInParent<UWControlScheme>();

        return mOControlScheme != null && mOControlScheme.Current == UWControlScheme.SchemeEnum.Modern;
    }

    private void fShowPortraits(UWNpc pONpc, int piSlot)
    {
        UWTexture.TextureTypes leType;
        int liIndex;

        if (piSlot > 0 && piSlot <= LastNamedPortrait)
        {
            leType = UWTexture.TextureTypes.CHARHEAD;
            liIndex = piSlot - 1;
        }
        else
        {
            leType = UWTexture.TextureTypes.GENHEAD;
            liIndex = pONpc != null ? (pONpc.ID & 0x3F) : 0;
        }

        fSetSprite(mONpcPortrait, leType, liIndex);
        mePartnerPortraitType = leType;
        miPartnerPortrait = liIndex;

        UWPlayerData lOPlayer = mOData.InitialPlayer;
        int liBody = lOPlayer != null ? lOPlayer.Body + (lOPlayer.IsFemale ? FemalePortraitOffset : 0) : 0;

        miPlayerPortrait = liBody;

        fSetSprite(mOPlayerPortrait, UWTexture.TextureTypes.HEADS, liBody);
    }

    // ------------------------------------------------------------------
    // Trade areas
    // ------------------------------------------------------------------

    private void fDrawTrade()
    {
        UWConversationTrade lOTrade = mOSession != null ? mOSession.Trade : null;

        miTradeVersionShown = lOTrade != null ? lOTrade.Version : -1;

        for (int liSlot = 0; liSlot < UWConversationTrade.SlotCount; liSlot++)
        {
            fSetTradeItem(mONpcTradeImages[liSlot], miNpcTradeSlots[liSlot], lOTrade != null ? lOTrade.NpcItems[liSlot] : null);
            fSetTradeItem(mOPlayerTradeImages[liSlot], miPlayerTradeSlots[liSlot], lOTrade != null ? lOTrade.PlayerItems[liSlot] : null);

            fSetTradeCount(mONpcTradeCounts[liSlot], lOTrade != null ? lOTrade.NpcItems[liSlot] : null);
            fSetTradeCount(mOPlayerTradeCounts[liSlot], lOTrade != null ? lOTrade.PlayerItems[liSlot] : null);

            mONpcTradeCrossImages[liSlot].enabled = lOTrade != null && lOTrade.NpcSelected[liSlot];
            mOPlayerTradeCrossImages[liSlot].enabled = lOTrade != null && lOTrade.PlayerSelected[liSlot];
        }
    }

    /// <summary>The image of a trade item, centred in its circle.</summary>
    private void fSetTradeItem(Image pOImage, Vector2Int pOSlot, UWObject pOItem)
    {
        UWTexture lOSource = pOItem != null ? (pOItem.Icon ?? pOItem.Texture) : null;

        if (lOSource == null)
        {
            pOImage.enabled = false;
            return;
        }

        fSetSpriteFromTexture(pOImage, lOSource);

        ((RectTransform)pOImage.transform).anchoredPosition = new Vector2(
            pOSlot.x + ((TradeSlotSize - lOSource.Width) / 2),
            -(pOSlot.y + ((TradeSlotSize - lOSource.Height) / 2)));
    }

    /// <summary>The count of a stack, like UWGameUI.fSetStackLabel: small font,
    /// white, bold in the modern font. Nothing for a single piece.</summary>
    private void fSetTradeCount(RawImage pOLabel, UWObject pOItem)
    {
        if (pOLabel == null)
            return;

        int liCount = pOItem != null && pOItem.HasQuantity && pOItem.Quantity < 512 ? pOItem.Quantity : 1;

        if (liCount <= 1)
        {
            UWTextLabel.Hide(pOLabel);
            return;
        }

        UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Small);

        if (lOFont == null)
            return;

        string lsText = liCount.ToString();

        if (pOLabel.texture != null)
            Destroy(pOLabel.texture);

        Texture2D lOTexture = UWFontRenderer.RenderLines(lOFont, new List<string> { lsText },
            lsText.Length * (lOFont.Height + 2), Color.white, FilterMode.Point);

        if (lOTexture == null)
            return;

        UWTextLabel.Get(pOLabel).Set(lOFont, lOTexture, new List<string> { lsText },
            new List<Color32> { Color.white }, true, FontStyle.Bold);
    }

    /// <summary>Which place of the player's area lies under the pointer - the circle or the
    /// cross next to it. -1 if none.</summary>
    public int GetPlayerTradeSlotAt(Vector2 pOScreenPosition, out bool pbOnCross)
    {
        return fGetTradeSlotAt(pOScreenPosition, miPlayerTradeSlots, miPlayerTradeCrosses, out pbOnCross);
    }

    public int GetNpcTradeSlotAt(Vector2 pOScreenPosition, out bool pbOnCross)
    {
        return fGetTradeSlotAt(pOScreenPosition, miNpcTradeSlots, miNpcTradeCrosses, out pbOnCross);
    }

    private int fGetTradeSlotAt(Vector2 pOScreenPosition, Vector2Int[] pOSlots, Vector2Int[] pOCrosses, out bool pbOnCross)
    {
        pbOnCross = false;

        if (mOSession == null || mOFrame == null)
            return -1;

        Vector2 lOLocal;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mOFrame, pOScreenPosition, null, out lOLocal))
            return -1;

        // The frame has its pivot in the centre; the measurements count from the top left.
        float lfX = lOLocal.x + (ScreenWidth / 2f);
        float lfY = (ScreenHeight / 2f) - lOLocal.y;

        for (int liSlot = 0; liSlot < pOSlots.Length; liSlot++)
        {
            // 17 by 17, both edges included, as the original's ovr095_52F tests.
            if (lfX >= pOSlots[liSlot].x && lfX < pOSlots[liSlot].x + TradeSlotSize + 1
                && lfY >= pOSlots[liSlot].y && lfY < pOSlots[liSlot].y + TradeSlotSize + 1)
                return liSlot;

            if (lfX >= pOCrosses[liSlot].x - TradeCrossHit && lfX < pOCrosses[liSlot].x + TradeCrossSize + TradeCrossHit
                && lfY >= pOCrosses[liSlot].y - TradeCrossHit && lfY < pOCrosses[liSlot].y + TradeCrossSize + TradeCrossHit)
            {
                pbOnCross = true;
                return liSlot;
            }
        }

        return -1;
    }

    /// <summary>The cross that shows a marked trade item - a plus in white, five pixels
    /// in size, like the marks in the template.</summary>
    private Sprite fGetCrossSprite()
    {
        if (mOCrossSprite != null)
            return mOCrossSprite;

        Texture2D lOTexture = new Texture2D(TradeCrossSize, TradeCrossSize, TextureFormat.RGBA32, false);
        lOTexture.name = "UWConversationScreen.cs:1124";
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

    /// <summary>
    /// THE VINE EDGES ROLL WITH THE PARCHMENT, one frame per scroll (UWScrollEdgeRules). Until
    /// 2026-09-22 they advanced with every redraw and each of the three heights carried a phase
    /// of its own; the original (seg043_37F0_163) draws all three with the SAME frame and steps
    /// only when the text area has scrolled.
    /// </summary>
    private void fRollEdges(List<string> pOShownLines)
    {
        int liScrolled = UWScrollEdgeRules.GetScrollAmount(mOLastShownLines, pOShownLines);

        mOLastShownLines = new List<string>(pOShownLines);

        if (liScrolled <= 0)
            return;

        miEdgeStep = UWScrollEdgeRules.Advance(miEdgeStep, EdgeFrames, liScrolled);

        fApplyEdges();
    }

    private void fApplyEdges()
    {
        for (int liAt = 0; liAt < UWScrollEdgeRules.ConversationRows; liAt++)
        {
            fSetSprite(mOEdges[liAt], UWTexture.TextureTypes.SCRLEDGE,
                UWScrollEdgeRules.GetConversationLeftImage(miEdgeStep));
            fSetSprite(mOEdges[UWScrollEdgeRules.ConversationRows + liAt], UWTexture.TextureTypes.SCRLEDGE,
                UWScrollEdgeRules.GetConversationRightImage(miEdgeStep));
        }
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

        if (pOImage.sprite != null && pOImage.sprite.texture != null)
            Destroy(pOImage.sprite.texture);

        Texture2D lOTexture = UWIconTextureBuilder.Build(lOSource,
            mOGameUi != null ? mOGameUi.TextureFilterMode : FilterMode.Point);

        pOImage.sprite = Sprite.Create(lOTexture,
            new Rect(0f, 0f, lOTexture.width, lOTexture.height), new Vector2(0.5f, 0.5f), 1f);
        pOImage.color = Color.white;
        UWIconPalette.Apply(pOImage);
        ((RectTransform)pOImage.transform).sizeDelta = new Vector2(lOTexture.width, lOTexture.height);
        pOImage.enabled = true;
    }

    // ------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------

    private void fEnsureCanvas()
    {
        if (mOCanvas != null)
            return;

        GameObject lOCanvasObject = new GameObject("UW Conversation", typeof(Canvas), typeof(CanvasScaler));
        lOCanvasObject.transform.SetParent(transform, false);

        mOCanvas = lOCanvasObject.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mOCanvas.sortingOrder = 90;

        CanvasScaler lOScaler = lOCanvasObject.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        lOScaler.referenceResolution = new Vector2(ScreenWidth, ScreenHeight);
        lOScaler.matchWidthOrHeight = 1f;

        // Fitted whole into the window, bars over and under it when narrow (UWUiFit).
        lOCanvasObject.AddComponent<UWFitCanvas>();

        // The same centred 320x200 frame as UWGameUI.fBuildCanvas - so the
        // parts lie pixel-exactly on CONV.BYT.
        GameObject lOFrameObject = new GameObject("Frame", typeof(RectTransform));
        lOFrameObject.transform.SetParent(lOCanvasObject.transform, false);

        mOFrame = (RectTransform)lOFrameObject.transform;
        lOFrameObject.AddComponent<UWPixelAspectFrame>();
        mOFrame.anchorMin = new Vector2(0.5f, 0.5f);
        mOFrame.anchorMax = new Vector2(0.5f, 0.5f);
        mOFrame.pivot = new Vector2(0.5f, 0.5f);
        mOFrame.sizeDelta = new Vector2(ScreenWidth, ScreenHeight);
        mOFrame.anchoredPosition = Vector2.zero;

        for (int liAt = 0; liAt < UWScrollEdgeRules.ConversationRows; liAt++)
        {
            mOEdges[liAt] = fCreateImage("Edge left " + (liAt + 1), EdgeLeft, miEdgeTops[liAt]);
            mOEdges[UWScrollEdgeRules.ConversationRows + liAt] =
                fCreateImage("Edge right " + (liAt + 1), EdgeRight, miEdgeTops[liAt]);
        }

        // The strips start on the frame the counter stands at; it keeps running across
        // conversations, as dseg_5c99_A96 does.
        fApplyEdges();

        mONpcPortrait = fCreateImage("Portrait partner", NpcPortraitLeft, PortraitTop);
        mOPlayerPortrait = fCreateImage("Portrait player", PlayerPortraitLeft, PortraitTop);

        for (int liSlot = 0; liSlot < UWConversationTrade.SlotCount; liSlot++)
        {
            mONpcTradeImages[liSlot] = fCreateImage("Trade item partner " + (liSlot + 1), miNpcTradeSlots[liSlot].x, miNpcTradeSlots[liSlot].y);
            mOPlayerTradeImages[liSlot] = fCreateImage("Trade item player " + (liSlot + 1), miPlayerTradeSlots[liSlot].x, miPlayerTradeSlots[liSlot].y);

            mONpcTradeCrossImages[liSlot] = fCreateImage("Mark partner " + (liSlot + 1), miNpcTradeCrosses[liSlot].x, miNpcTradeCrosses[liSlot].y);
            mOPlayerTradeCrossImages[liSlot] = fCreateImage("Mark player " + (liSlot + 1), miPlayerTradeCrosses[liSlot].x, miPlayerTradeCrosses[liSlot].y);

            foreach (Image lOCross in new[] { mONpcTradeCrossImages[liSlot], mOPlayerTradeCrossImages[liSlot] })
            {
                lOCross.sprite = fGetCrossSprite();
                lOCross.color = Color.white;
                ((RectTransform)lOCross.transform).sizeDelta = new Vector2(TradeCrossSize, TradeCrossSize);
            }

            // The number one pixel right of the place corner, as in the backpack.
            mONpcTradeCounts[liSlot] = fCreateRawImage("Count partner " + (liSlot + 1), mOFrame, miNpcTradeSlots[liSlot].x + 1, miNpcTradeSlots[liSlot].y);
            mOPlayerTradeCounts[liSlot] = fCreateRawImage("Count player " + (liSlot + 1), mOFrame, miPlayerTradeSlots[liSlot].x + 1, miPlayerTradeSlots[liSlot].y);
        }

        mONpcNameImage = fCreateRawImage("Name partner", mOFrame, NpcNameLeft, NameTop);
        mOPlayerNameImage = fCreateRawImage("Name player", mOFrame, PlayerNameLeft, NameTop);

        // Parchment and scroll: one clipping area each, so that nothing reaches over the edge.
        RectTransform lOHistoryArea = fCreateArea("History", HistoryLeft, HistoryTop, HistoryWidth, HistoryHeight);
        mOHistoryImage = fCreateRawImage("Text", lOHistoryArea, 0, 0);

        RectTransform lOChoiceArea = fCreateArea("Answers", ChoicesLeft, ChoicesTop, ChoicesWidth, ChoicesHeight);
        mOChoiceImage = fCreateRawImage("Text", lOChoiceArea, 0, 0);
    }

    /// <summary>An area with clipping - whatever reaches beyond it is not drawn.
    /// </summary>
    private RectTransform fCreateArea(string psName, int piLeft, int piTop, int piWidth, int piHeight)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RectMask2D));
        lOObject.transform.SetParent(mOFrame, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(piLeft, -piTop);
        lORect.sizeDelta = new Vector2(piWidth, piHeight);

        return lORect;
    }

    private Image fCreateImage(string psName, int piLeft, int piTop)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObject.transform.SetParent(mOFrame, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(piLeft, -piTop);

        Image lOImage = lOObject.GetComponent<Image>();
        lOImage.raycastTarget = false;
        lOImage.enabled = false;

        return lOImage;
    }

    private static RawImage fCreateRawImage(string psName, RectTransform pOParent, int piLeft, int piTop)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(piLeft, -piTop);

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;
        lOImage.enabled = false;

        return lOImage;
    }

}
