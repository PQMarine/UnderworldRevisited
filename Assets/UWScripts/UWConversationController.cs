using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The answers' click rows of a conversation renderer, in screen pixels: the classic scroll
/// (UWClassicConversationRenderer) or the modern rows (UWModernConversation) - for the gamepad's
/// stepping and the click that chooses.
/// </summary>
public interface IUWConversationChoiceRows
{
    /// <summary>The answers' screen rectangles in their order; empty while none stand there.</summary>
    void GetChoiceRects(List<Rect> pORects);

    /// <summary>The answer under a screen point, 1 for the first; 0 for none.</summary>
    int ChoiceAt(Vector2 pOPointer);
}

/// <summary>
/// THE CONVERSATION'S DRIVER for both schemes (cleanup step 5.12, 2026-10-20: the input half of
/// what was UWConversationScreen): it starts and ends the session, holds the game clock, plays
/// "Maps & Legends", takes the keys, the clicks and the gamepad (on the release, with the guards of
/// the frame the conversation opened in), the typed answer (babl_ask), [MORE], the look text's
/// two seconds and the fade at the end. What is shown is UWConversationView's; the classic scheme
/// draws it with UWClassicConversationRenderer, the modern one with UWModernConversation, which
/// reads the same view.
///
/// Selection is made with the number keys (1 to 4 classic, 1 to 9 modern, see fReadChoice) or
/// by mouse click on the line.
///
/// THE MODERN SCHEME (stage 4, per user on a mockup, 2026-10-04) keeps this as the
/// conversation's driver - the session, the answers by key, the typed answer, the end - but hides
/// the classic canvas: UWModernConversation draws the conversation instead (portraits, trade areas,
/// the history on leather, the answers) and the modern bags do the trading. There the text never
/// pauses for [MORE] (the history scrolls), and a click chooses on the modern answer rows.
/// </summary>
public class UWConversationController : MonoBehaviour
{
    private DataImport mOData;
    private UWGameUI mOGameUi;
    private UWControlScheme mOControlScheme;

    /// <summary>What the conversation shows - one for the controller's life, so the vine edges'
    /// frame carries from one conversation into the next.</summary>
    private readonly UWConversationView mOView = new UWConversationView();

    private UWClassicConversationRenderer mORenderer;

    private Interaction mOInteraction;
    private bool mbChoicesHidden;

    /// <summary>The running conversation - for UWItemDrag (trade area).</summary>
    public UWConversationSession Session
    {
        get { return mOView.Session; }
    }

    /// <summary>What the conversation shows, for the renderers (UWModernConversation).</summary>
    public UWConversationView View
    {
        get { return mOView; }
    }

    /// <summary>Is a conversation running right now? While it is, it blocks world interaction.</summary>
    public bool IsOpen
    {
        get { return Session != null; }
    }

    /// <summary>Is a conversation running anywhere? In the original the world stands still
    /// meanwhile - the creatures follow this as well (see UWCritter).</summary>
    public static bool IsAnyOpen { get; private set; }

    /// <summary>Is the player currently typing an answer (babl_ask)? Then the keys belong
    /// to the text, not to the commands (see UWGameUI.fCheckFontToggle).</summary>
    public static bool IsTypingAnswer { get; private set; }

    /// <summary>The answer's typing (UWTextEntry since 2026-10-19: the text events, the keys where
    /// none arrive, one filter).</summary>
    private UWTextEntry mOAnswerEntry;

    private UWTextEntry fAnswerEntry()
    {
        if (mOAnswerEntry == null)
            mOAnswerEntry = new UWTextEntry(fOnAnswerTextInput);

        return mOAnswerEntry;
    }

    private UWFont fFont()
    {
        return mOData != null ? mOData.Fonts.Get(UWFonts.FontType.Normal) : null;
    }

    /// <summary>Starts a conversation with this NPC.</summary>
    public bool Begin(DataImport pOData, UWNpc pONpc, int piSlot)
    {
        if (pOData == null || pOData.Conversations == null || !pOData.Conversations.IsLoaded)
            return false;

        mOData = pOData;
        // The session is engine-free; UWConversationHost hands it the player, the world and
        // the inventory from the scene (P3 of the engine separation, 2026-09-18).
        UWConversationSession lOSession = new UWConversationSession(pOData, pONpc, piSlot, new UWConversationHost(),
            UWGameSession.Current);

        if (lOSession.IsFinished && lOSession.Lines.Count == 0)
        {
            mOView.End();
            return false;
        }

        if (mOGameUi == null)
            mOGameUi = GetComponent<UWGameUI>();

        if (mORenderer == null)
            mORenderer = new UWClassicConversationRenderer(transform, mOGameUi);

        mOView.Begin(lOSession, pONpc, piSlot, pOData.InitialPlayer);
        mOTypedLines.Clear();

        IsAnyOpen = true;
        mbClosing = false;
        mbEndHoldWaits = false;
        miOpenedFrame = Time.frameCount;

        // THE WORLD STANDS STILL: in the original the conversation is a modal loop and the game
        // loop does not run until it returns - the palette cycling of the spell icons stops, the
        // water, the creatures, the weapon (per user on the original, 2026-09-18). One hold on the
        // game clock does all of that; this screen runs on the unscaled clock.
        UWGameClock.Hold(UWGameClock.ConversationHold);

        if (mOGameUi != null)
            mOGameUi.Conversation.SetConversationFrame(true);

        // Pointer free, movement and look off - but the inventory stays usable, as in the
        // original (trading). Without the lock the player would keep walking down the corridor
        // during the conversation.
        if (mOControlScheme == null)
            mOControlScheme = GetComponentInParent<UWControlScheme>();

        if (mOControlScheme != null)
            mOControlScheme.IsConversationOpen = true;

        mORenderer.Open(pOData, mOView);

        // "Maps & Legends" plays during conversations (reference: conversationinitialisation).
        UWAudioService.ChangeTheme(UWMusicSelector.MapsAndLegendsTheme);

        lOSession.Advance();
        fRedraw();

        return true;
    }

    private bool mbClosing;

    /// <summary>The conversation has ended and closes: what is said now goes to the message box (U8).</summary>
    public bool IsClosing => mbClosing;

    /// <summary>The last line's hold before the fade waits for a key or a click (fCloseWithFade).</summary>
    private bool mbEndHoldWaits;

    /// <summary>Converse's scroll_wait(0x1F4) when the player spoke last (CONVERSE.C): ticks of the
    /// game clock TICKS.ASM keeps at 256 a second (SCROLLIO.C scroll_wait, GAME_TIME), so 500 / 256,
    /// just under two seconds. Ours took 2 s until 2026-10-17 (audit round 2 B5, settled from the
    /// code; the 47 ms are not visible).</summary>
    private const float EndHoldSeconds = 0x1F4 / 256f;

    private const float FadeSeconds = 1f;

    private System.Collections.IEnumerator fCloseWithFade()
    {
        // A click or a key triggers the fade immediately (per user on the original,
        // 2026-09-12); otherwise the waiting time runs out.
        //
        // THE TIMED PAUSE ONLY WHEN THE PLAYER SPOKE LAST and the partner was not left hostile
        // (UWConversationSession.EndsWithTimedPause, Converse in CONVERSE.C); otherwise the last
        // line stays until a key or click - the partner's farewell, Biden handing over the scroll
        // without any answer (per user on the original, 2026-09-14). Until 2026-10-15 ours timed
        // every conversation that had offered an answer.
        bool lbTimed = Session != null && Session.EndsWithTimedPause;

        mbEndHoldWaits = true;

        for (float lfAt = 0f; !lbTimed || lfAt < EndHoldSeconds; lfAt += Time.unscaledDeltaTime)
        {
            yield return null;

            if (fAnyInput())
                break;
        }

        mbEndHoldWaits = false;

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

        UWConversationSession lOSession = Session;

        if (lOSession != null)
        {
            lOSession.Abort();
            fRefreshCritterAfterTalk(lOSession.Npc);
        }

        mOView.End();
        IsAnyOpen = false;
        UWGameClock.Release(UWGameClock.ConversationHold);

        // Afterwards a level theme again (reference: conversationvm, end of conversation).
        UWAudioService.PickLevelTheme();

        if (mOControlScheme != null)
            mOControlScheme.IsConversationOpen = false;

        if (mOGameUi != null)
            mOGameUi.Conversation.SetConversationFrame(false);

        if (mORenderer != null)
            mORenderer.Close();
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
        // The help's layout and the scheme: the classic canvas follows both every frame.
        if (mORenderer != null)
            mORenderer.Tick(UWControlScheme.Ui.Policy.ConversationOnFrame);

        UWConversationSession lOSession = Session;

        if (lOSession == null)
            return;

        if (lOSession.Trade != null && lOSession.Trade.Version != mORenderer.TradeVersionShown)
            mORenderer.DrawTrade(mOView);

        // The look text gives way after its two seconds - or at once on a left click, which
        // only brings the answers back and chooses nothing (per user on the original,
        // 2026-09-18).
        // THE PRESS THAT SHOWED IT does not cut it short: the modern context menu's Look acts on the
        // press, and its release came a frame later and took the text away at once (per user,
        // 2026-10-04).
        // A LONG LOOK WAITS WITH [MORE] in the answer scroll, then for the buttons to come up (U8): nothing else runs.
        if (mOView.IsLookTextShown && fUpdateLookPaging())
            return;

        bool lbLookRelease = mOView.IsLookTextShown && UWMouseButtons.LeftReleased;

        if (lbLookRelease && mbLookIgnoresRelease)
        {
            mbLookIgnoresRelease = false;
            lbLookRelease = false;
        }

        bool lbLookTextCutShort = lbLookRelease;

        if (lbLookTextCutShort)
            miLookTextCutFrame = Time.frameCount;

        if (mOView.IsLookTextShown && (mOView.IsLookTextOver(Time.unscaledTime) || lbLookTextCutShort))
        {
            mOView.EndLookText();

            UWFont lOLookFont = mOData.Fonts.Get(UWFonts.FontType.Normal);

            if (lOLookFont != null && !mbChoicesHidden)
                mORenderer.DrawChoices(mOView, lOLookFont);
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
                mORenderer.HideAnswers();
            else
            {
                UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Normal);

                if (lOFont != null)
                    mORenderer.DrawChoices(mOView, lOFont);
            }
        }

        if (lbPrompt)
            return;

        if (lOSession.IsAskingText)
        {
            fUpdateTypedAnswer();
            return;
        }

        if (IsTypingAnswer)
            fEndTyping();

        // npc_say's \\P (uw1 conv/CONVERSE.C:510-519): scroll_wait(0xC8) before each line of the partner, which a key or a
        // click cuts short (cleanup plan 2, U3, per user 2026-11-27 on the original - as the second game's screen).
        if (mOView.HoldPending)
        {
            if (Time.unscaledTime >= mfHoldUntil || fAnyInput())
            {
                miTextWaitEndedFrame = Time.frameCount;
                mOView.HoldOver();
                fRedraw();
            }

            return;
        }

        // "[MORE]": any key or mouse button fetches the rest of the text.
        if (mOView.MorePending)
        {
            if (fAnyInput())
            {
                miTextWaitEndedFrame = Time.frameCount;
                fContinueAfterMore();
            }

            return;
        }

        // A finished conversation stays up for about two seconds (or until a key, see
        // fCloseWithFade), then the picture fades
        // to black in one second, the conversation window closes, and in another
        // second the main display comes back out of the black - without a click, and the
        // message box is empty afterwards (per user on the original, 2026-09-12).
        if (lOSession.IsFinished && lOSession.Choices.Count == 0)
        {
            if (!mbClosing)
            {
                mbClosing = true;

                if (!string.IsNullOrEmpty(lOSession.Error))
                    Debug.LogWarning("Conversation ended by an error: " + lOSession.Error);
                StartCoroutine(fCloseWithFade());
            }

            return;
        }

        fStepChoicesByPad();

        int liChoice = fReadChoice();

        if (liChoice <= 0)
            return;

        lOSession.Choose(liChoice);
        fRedraw();
    }

    /// <summary>The answers' rows of the scheme shown: the modern ones (UWModernConversation) or
    /// the classic scroll's lines (IUWUiScheme since cleanup 5.19).</summary>
    private IUWConversationChoiceRows fChoiceRows()
    {
        return UWControlScheme.Ui.ConversationChoiceRows;
    }

    /// <summary>The classic scroll's answer lines, for UWClassicUiScheme; null before the first
    /// conversation built the renderer.</summary>
    internal IUWConversationChoiceRows ClassicChoiceRows => mORenderer;

    /// <summary>The gamepad steps through the answers (UWPadEntryStepper).</summary>
    private readonly UWPadEntryStepper mOChoiceStepper = new UWPadEntryStepper();

    private readonly List<Rect> mOChoiceRects = new List<Rect>();

    /// <summary>The answers the stepper last saw - new ones snap the pointer onto the first.</summary>
    private string msSteppedChoices;

    /// <summary>
    /// THE ANSWERS ON THE D-PAD AND THE LEFT STICK (per user, 2026-10-08: as in the game menu and the
    /// context menu): they put the pointer onto the next answer, A (the pad's click) chooses it; a
    /// new set of answers snaps it onto the first while the pad is in use. The modern rows
    /// (UWModernConversation) or the classic scroll's lines.
    /// </summary>
    private void fStepChoicesByPad()
    {
        // A thing's menu in the bags beside the conversation has the d-pad for its entries (per user 2026-11-26: the
        // pointer jumped to the answers instead).
        if (UWModernBags.IsMenuOpen || UWModernBags.IsSplitting)
            return;

        UWConversationSession lOSession = Session;

        if (lOSession == null || lOSession.Choices.Count == 0)
        {
            msSteppedChoices = null;
            return;
        }

        IUWConversationChoiceRows lORows = fChoiceRows();

        if (lORows == null)
            return;

        lORows.GetChoiceRects(mOChoiceRects);

        if (mOChoiceRects.Count == 0)
            return;

        string lsChoices = lOSession.Choices.Count + "|" + lOSession.Choices[0];

        if (lsChoices != msSteppedChoices)
        {
            msSteppedChoices = lsChoices;
            mOChoiceStepper.Opened();
        }

        // The modern history scrolls with the stick while the pointer is on it (UWPadEntryStepper).
        mOChoiceStepper.Update(mOChoiceRects, UWControlScheme.Ui.ConversationHistoryRect);
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
            miTypingButtonsUpFrame = -1;
            mOView.BeginTyping();

            if (lOKeyboard != null)
                fAnswerEntry().Begin();

            fDrawTypedAnswer();
        }

        if (IsKeyboardTaken)
            return;

        // WDIALOG ENDS ON ENTER, ESCAPE OR A CLICK (cleanup plan 2, U3, 2026-11-27; uw1 conv/CONVERSE.C:580, SCROLLIO.C:505-631,
        // as the second game's UWConversationScreenUw2.fUpdateTyping): the ">answer" line stays in the scroll until the next
        // answers come, Escape answers nothing and leaves ">-". Until then a click did nothing, Escape showed no dash and the
        // line went at once.
        bool lbEscape = UWBack.Takes(UWBackOwner.ConversationAnswer);

        if (UWTextEntry.EnterPressed || lbEscape || fTypingButtonReleased())
        {
            string lsAnswer = lbEscape ? string.Empty : mOView.TypedAnswer;

            mOTypedLines.Add(UWConversationLayout.TypedAnswerPrompt + (lbEscape ? EscapedAnswer : lsAnswer));
            fEndTyping();
            Session.SupplyText(lsAnswer);
            fRedraw();
            return;
        }

        if (lOKeyboard == null)
            return;

        if (UWTextEntry.BackspacePressed && mOView.Backspace())
            fDrawTypedAnswer();

        // Where no text events arrive (Linux), the keys themselves (UWTypedKeys).
        fAnswerEntry().ReadKeys();
    }

    private void fOnAnswerTextInput(char pcChar)
    {
        if (!IsTypingAnswer || IsKeyboardTaken)
            return;

        if (mOView.TryType(pcChar, fFont()))
            fDrawTypedAnswer();
    }

    private void fEndTyping()
    {
        IsTypingAnswer = false;
        mOView.EndTyping();

        fAnswerEntry().End();
    }

    private void fDrawTypedAnswer()
    {
        UWFont lOFont = fFont();

        if (lOFont == null)
            return;

        mORenderer.DrawTypedAnswer(mOView, lOFont, mOTypedLines);
    }

    /// <summary>The answers typed since the last menu, ">answer" or ">-" for Escape: they stay in the scroll until the next
    /// answers (U3).</summary>
    private readonly List<string> mOTypedLines = new List<string>();

    /// <summary>When the running hold before a partner's line is over, and whether it has been timed.</summary>
    private float mfHoldUntil;

    private bool mbHoldTimed;

    /// <summary>wdialog's Escape answer as the scroll shows it.</summary>
    private const string EscapedAnswer = "-";

    /// <summary>The frame both buttons were up since the typing began, -1 before: the click that chose the answer
    /// leading here does not end the typing when it comes up.</summary>
    private int miTypingButtonsUpFrame = -1;

    /// <summary>A button came up that went down while typing (wdialog's mouse codes 1 to 3).</summary>
    private bool fTypingButtonReleased()
    {
        if (!UWPointer.HasMouse)
            return false;

        if (miTypingButtonsUpFrame < 0 && !UWMouseButtons.LeftHeld && !UWMouseButtons.RightHeld)
            miTypingButtonsUpFrame = Time.frameCount;

        return miTypingButtonsUpFrame >= 0 && Time.frameCount > miTypingButtonsUpFrame
            && (UWMouseButtons.LeftReleased || UWMouseButtons.RightReleased);
    }

    /// <summary>While the help has the keyboard (typing in its notes, reading the manual), or
    /// anything else takes typed keys - the modern split box, the bags' menu, a count prompt
    /// (UWControls.IsTextEntryActive, which UWTextEntryState keeps for all of them) - the conversation
    /// hears no key: a digit typed there must not choose an answer (per user, 2026-10-14: the
    /// split box's digits chose answers). Its own typed answer is the one entry it keeps.</summary>
    private static bool IsKeyboardTaken => UWHelpWindow.BlocksGameKeys
        || (UWControls.IsTextEntryActive && !IsTypingAnswer);

    /// <summary>Number key or click on an answer line. 0 means: nothing selected. The number keys
    /// are UWConversationView.ChoiceKeys (1 to 4 classic, 1 to 9 modern).</summary>
    private int fReadChoice()
    {
        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard != null && !IsKeyboardTaken)
        {
            int liKeys = UWControlScheme.Ui.Policy.ConversationChoiceKeys;

            for (int liIndex = 0; liIndex < Session.Choices.Count && liIndex < liKeys; liIndex++)
            {
                if (lOKeyboard[Key.Digit1 + liIndex].wasPressedThisFrame)
                    return liIndex + 1;
            }
        }

        // ON THE RELEASE of either button, like everything during a conversation (per user on
        // the original, 2026-09-18: the right button chooses an answer too) - and not in the
        // frame the conversation opened in, that release belongs to the drag that started it.
        if (!UWPointer.HasMouse || !(UWMouseButtons.LeftReleased || UWMouseButtons.RightReleased)
            || miOpenedFrame == Time.frameCount)
            return 0;

        Vector2 lOPosition = UWPointer.Position;

        // The modern scheme's rows (UWModernConversation): the left button only - the right one
        // is the bags' there (look, the containers).
        if (!UWControlScheme.Ui.Policy.ConversationRightClickChooses && !UWMouseButtons.LeftReleased)
            return 0;

        IUWConversationChoiceRows lORows = fChoiceRows();

        return lORows != null ? lORows.ChoiceAt(lOPosition) : 0;
    }

    /// <summary>The frame the conversation opened in: the release of the drag that started it
    /// must not count as input here.</summary>
    private int miOpenedFrame = -1;

    /// <summary>A pad button counts as "any input" now (fAnyInput): the keyboard is not taken, the
    /// letter grid is not up, the conversation did not open in this frame.</summary>
    private bool fPadAnyInputOpen => !IsKeyboardTaken && !UWLetterGrid.IsShown && miOpenedFrame != Time.frameCount;

    /// <summary>
    /// "[MORE]" WAITS FOR ANY PRESS and Update gets to it (cleanup step 7.8): a session, no input line
    /// in the scroll, no typed answer asked, more text pending - a pad button fetches it (fAnyInput).
    /// UWPadOwners asks when the frame begins, so that the pad's pointer does not click with that press.
    /// </summary>
    internal bool MoreTakesPad
    {
        get
        {
            UWConversationSession lOSession = Session;

            return lOSession != null && fPadAnyInputOpen && mOView.MorePending && !lOSession.IsAskingText
                && !(mOInteraction != null && mOInteraction.IsPromptActive);
        }
    }

    /// <summary>THE END'S HOLD WAITS FOR ANY PRESS (cleanup step 7.8): the last line before the fade
    /// (fCloseWithFade) - a pad button ends the wait. Its coroutine read the press after every Update,
    /// so the pad's pointer had clicked with it already; UWPadOwners keeps it from the pointer now.</summary>
    internal bool EndTakesPad => mbEndHoldWaits && mbClosing && IsAnyOpen && fPadAnyInputOpen;

    private bool fAnyInput()
    {
        Keyboard lOKeyboard = Keyboard.current;

        // The F belongs to the font toggle (see UWGameUI) and should not click the
        // conversation away along with it; Tab opens and closes the help.
        if (lOKeyboard != null && !IsKeyboardTaken && UWTextEntry.AnyKeyPressed
            && !lOKeyboard.fKey.wasPressedThisFrame && !lOKeyboard.tabKey.wasPressedThisFrame)
            return true;

        // Any gamepad button as well, but not while the letter grid types (UWGamepad). The press is
        // ours: the pad's pointer does not click with it (UWPadOwners asks MoreTakesPad, EndTakesPad;
        // UWGamepad.ConsumePress here until cleanup step 7.8).
        if (fPadAnyInputOpen && UWGamepad.AnyPressed(false))
            return true;

        // The mouse acts on the release during a conversation (per user on the original, 2026-09-18).
        return UWPointer.HasMouse && miOpenedFrame != Time.frameCount
            && (UWMouseButtons.LeftReleased || UWMouseButtons.RightReleased);
    }

    // ------------------------------------------------------------------
    // Drawing
    // ------------------------------------------------------------------

    /// <summary>The history brought up to the session and drawn; then the answers - while the text
    /// is waiting there is nothing to choose, the answers only come once everything has been
    /// said.</summary>
    private void fRedraw()
    {
        UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Normal);

        if (lOFont == null)
            return;

        UWConversationView.Policy lyPolicy = UWConversationView.Policy.For(lOFont, UWControlScheme.Ui.Policy);

        mOView.Refresh(lOFont, lyPolicy);
        mORenderer.DrawHistory(mOView, lOFont, lyPolicy);

        // A hold that begins now runs its 0xC8 ticks from here.
        if (mOView.HoldPending && !mbHoldTimed)
            mfHoldUntil = Time.unscaledTime + mOView.HoldSeconds;

        mbHoldTimed = mOView.HoldPending;

        // The answers come once everything has been said (conv_choice prints them when the script reaches them, after
        // every say with its \\P); until then the typed lines stay in the scroll - [MORE] and the holds included - or it
        // is empty (per user 2026-11-27 with a picture of the original, test 255.3).
        bool lbTextWaits = mOView.MorePending || mOView.HoldPending || mOView.PendingPicture != 0;

        if (mOTypedLines.Count > 0 && (lbTextWaits || (Session != null && Session.Choices.Count == 0)))
            mORenderer.DrawTypedLines(lOFont, mOTypedLines);
        else if (lbTextWaits)
            mORenderer.HideAnswers();
        else
        {
            // The next answers take the scroll: the typed lines go (U3, as the second game's fRedraw).
            if (Session != null && Session.Choices.Count > 0)
                mOTypedLines.Clear();

            mORenderer.DrawChoices(mOView, lOFont);
        }
    }

    /// <summary>The click on "[MORE]": continue with the text.</summary>
    private void fContinueAfterMore()
    {
        mOView.ContinueAfterMore();

        fRedraw();
    }

    /// <summary>
    /// LOOKING AT A TRADE ITEM shows its description in the answer scroll for about two seconds
    /// (UWConversationView.ShowLookText), then the answers come back.
    /// </summary>
    public void ShowLookText(string psText)
    {
        UWFont lOFont = fFont();

        if (lOFont == null || string.IsNullOrEmpty(psText) || mORenderer == null)
            return;

        // THE LINES OF ONE LOOK STAND TOGETHER (cleanup plan 2, U8, 2026-11-27; as the second game's
        // UWConversationScreenUw2.fShowLook): what is said in the same frame joins the look, "You see a book." and what
        // follows it. Until then each line replaced the one before.
        fStartOrJoinLook(psText);
        mOLookLines.AddRange(UWConversationView.LookLines(lOFont, psText));
        fDrawLook();
    }

    /// <summary>
    /// A TEXT IN PLACE OF THE ANSWERS, PAGED (cleanup plan 2, U8, 2026-11-27): a book or a scroll read in a conversation
    /// (UWMessageLog.ShowPagedText while a conversation is open). psLeading come before it as printed lines; a text
    /// longer than the answer scroll waits with [MORE] there - four lines a page, the scroll's five less the [MORE] row,
    /// the last line joining a page as in the message scroll (scroll_print3) - and the look's moment starts with the last
    /// page. The modern answers show it whole. As the second game's fShowLookPaged.
    /// </summary>
    public void ShowLookPaged(string psText, params string[] psLeading)
    {
        UWFont lOFont = fFont();

        if (lOFont == null || string.IsNullOrEmpty(psText) || mORenderer == null)
            return;

        // The leading lines belong to the look's text too: the modern answers show the whole of it.
        string lsWhole = psText;

        if (psLeading != null)
        {
            for (int liAt = psLeading.Length - 1; liAt >= 0; liAt--)
            {
                if (!string.IsNullOrEmpty(psLeading[liAt]))
                    lsWhole = psLeading[liAt] + "\n" + lsWhole;
            }
        }

        fStartOrJoinLook(lsWhole);

        if (psLeading != null)
        {
            foreach (string lsLeading in psLeading)
            {
                if (!string.IsNullOrEmpty(lsLeading))
                    mOLookLines.AddRange(UWConversationView.LookLines(lOFont, lsLeading));
            }
        }

        if (UWControlScheme.IsModern)
        {
            mOLookLines.AddRange(UWConversationView.LookLines(lOFont, psText));
            fDrawLook();

            return;
        }

        mOLookPending.AddRange(UWConversationView.LookLines(lOFont, psText));
        msPagedLookText = psText;
        fNextLookPage();
    }

    /// <summary>The frame of the look shown last and its text: what is said in the same frame joins it.</summary>
    private int miLookFrame = -1;

    private string msLookText;

    /// <summary>The look's lines printed so far, and those waiting for [MORE].</summary>
    private readonly List<string> mOLookLines = new List<string>();

    private readonly List<string> mOLookPending = new List<string>();

    /// <summary>The text being paged (its last piece decides the last line's join).</summary>
    private string msPagedLookText;

    /// <summary>After the last page the buttons must come up first (scroll_wait's mouse_release).</summary>
    private bool mbLookPageRelease;

    private int miLookPageFrame = -1;

    /// <summary>A new look, or more of the one of this frame.</summary>
    private void fStartOrJoinLook(string psText)
    {
        if (miLookFrame != Time.frameCount || msLookText == null || !mOView.IsLookTextShown)
        {
            mOLookLines.Clear();
            mOLookPending.Clear();
            mbLookPageRelease = false;
            msLookText = psText;
            mbLookIgnoresRelease = UWMouseButtons.LeftHeld;
        }
        else
            msLookText = msLookText + "\n" + psText;

        miLookFrame = Time.frameCount;
    }

    /// <summary>The answer scroll's lines (five in the classic scroll).</summary>
    private int fLookCapacity()
    {
        return UWMessageLogModel.CapacityOf(fFont(), UWConversationLayout.ChoicesHeight);
    }

    private void fNextLookPage()
    {
        int liPerPage = UWMessageLogModel.LinesAboveMore(fLookCapacity());

        for (int liAt = 0; liAt < liPerPage && mOLookPending.Count > 0; liAt++)
        {
            mOLookLines.Add(mOLookPending[0]);
            mOLookPending.RemoveAt(0);
        }

        if (mOLookPending.Count == 1 && UWMessageLogModel.LastLineJoinsPage(msPagedLookText, mOLookPending[0]))
        {
            mOLookLines.Add(mOLookPending[0]);
            mOLookPending.RemoveAt(0);
        }

        miLookPageFrame = Time.frameCount;

        if (mOLookPending.Count == 0 && miLookFrame != Time.frameCount)
            mbLookPageRelease = true;

        fDrawLook();
    }

    /// <summary>The last lines of the look as the scroll holds them, [MORE] in its last row while lines wait; the look's
    /// moment starts anew with every page.</summary>
    private void fDrawLook()
    {
        UWFont lOFont = fFont();

        if (lOFont == null)
            return;

        int liCapacity = fLookCapacity();
        bool lbMore = mOLookPending.Count > 0;
        int liShown = lbMore ? UWMessageLogModel.LinesAboveMore(liCapacity) : liCapacity;
        List<string> lOLines = new List<string>();

        for (int liAt = System.Math.Max(0, mOLookLines.Count - liShown); liAt < mOLookLines.Count; liAt++)
            lOLines.Add(mOLookLines[liAt]);

        if (lbMore)
            lOLines.Add(UWConversationView.MoreMarker);

        mORenderer.DrawLookText(lOFont, lOLines, lbMore);
        mOView.ShowLookText(msLookText, Time.unscaledTime);
    }

    /// <summary>scroll_wait for the look's [MORE]: a key, a mouse button or a pad button brings the next page, then the
    /// buttons must come up. True while the look is not to be ended or cut short.</summary>
    private bool fUpdateLookPaging()
    {
        if (mOLookPending.Count == 0)
        {
            if (mbLookPageRelease && !UWMouseButtons.LeftHeld && !UWMouseButtons.RightHeld)
            {
                mbLookPageRelease = false;

                return true;
            }

            return mbLookPageRelease;
        }

        if (Time.frameCount == miLookPageFrame)
            return true;

        if (UWPageInput.TryConfirm(!IsKeyboardTaken))
            fNextLookPage();

        return true;
    }

    /// <summary>
    /// THE TEXT WAITS - the partner's [MORE], a hold before a line or a \\p, a look's [MORE] - or its wait ended in this
    /// frame: the panels take no click (per user 2026-11-27 on the original: while the partner's long text waits with
    /// [MORE], a right click looks at nothing; only when the answers stand). The original's scroll_wait and scroll_more are
    /// loops of their own that read nothing else (uw1 ui/SCROLLIO.C:102-120). UWItemDrag and the modern bags ask.
    /// </summary>
    public bool IsTextWaiting => IsAnyOpen && mOView != null
        && (mOView.MorePending || mOView.HoldPending || IsLookPaging || miTextWaitEndedFrame == Time.frameCount);

    /// <summary>The frame a key or a click ended a wait of the text.</summary>
    private int miTextWaitEndedFrame = -1;

    /// <summary>A look's [MORE] waits, or its last page's click is still held: the session's panels take no click.</summary>
    public bool IsLookPaging => mOView.IsLookTextShown && (mOLookPending.Count > 0 || mbLookPageRelease);

    /// <summary>The look text came from a left press still held - its release is not the click
    /// that cuts the text short.</summary>
    private bool mbLookIgnoresRelease;

    /// <summary>The frame in which a left click cut the look text short.</summary>
    private int miLookTextCutFrame = -1;

    /// <summary>The left click that cuts the look text short is used up: it chooses no answer
    /// and nothing on the trade areas either (per user on the original, 2026-09-18). True while
    /// the text shows and in the frame it was cut - UWItemDrag asks before it acts on a
    /// release, whichever of the two Updates runs first.</summary>
    public bool ConsumesLeftClick
    {
        get { return mOView.IsLookTextShown || miLookTextCutFrame == Time.frameCount; }
    }

    // ------------------------------------------------------------------
    // Trade areas
    // ------------------------------------------------------------------

    /// <summary>Which place of the player's area lies under the pointer, -1 if none
    /// (UWConversationLayout.TradeSlotAt: only the slot square).</summary>
    public int GetPlayerTradeSlotAt(Vector2 pOScreenPosition)
    {
        return fGetTradeSlotAt(pOScreenPosition, false);
    }

    public int GetNpcTradeSlotAt(Vector2 pOScreenPosition)
    {
        return fGetTradeSlotAt(pOScreenPosition, true);
    }

    private int fGetTradeSlotAt(Vector2 pOScreenPosition, bool pbNpc)
    {
        if (Session == null || mORenderer == null || !mORenderer.TryGetOriginalPoint(pOScreenPosition, out float lfX, out float lfY))
            return -1;

        return UWConversationLayout.TradeSlotAt(pbNpc, lfX, lfY);
    }
}
