using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE PROMPTS OF THE MESSAGE SCROLL (cleanup step 5.13, 2026-10-22: out of Interaction, which keeps
/// the public surface and forwards): the yes/no question on the engine-free UWYesNoQuestion - the
/// anvil's repair, the trap found on a look - and the shrine's mantra on UWTextEntry, with the
/// modal hold of a typed input and the swallow of the mouse button that ended it. Both read their
/// keys, mouse and pad here; Interaction ticks them in its Update's order (the mantra before every
/// modal gate, the question after the conversation's). The modern scheme's box (UWModernQuestion)
/// answers both through Interaction.
/// </summary>
public sealed class UWPromptController
{
    private readonly Interaction mOInteraction;

    public UWPromptController(Interaction pOInteraction)
    {
        mOInteraction = pOInteraction;
    }

    // ------------------------------------------------- Shrine

    private bool mbChanting;

    /// <summary>Whether a mantra is currently being typed - then the letters belong to the
    /// shrine, not to the key commands (see UWGameUI, key F).</summary>
    public bool IsChanting
    {
        get { return mbChanting; }
    }

    private string msMantra = string.Empty;

    /// <summary>
    /// The shrine asks for the mantra. Typing works as for a map note - the
    /// characters come via onTextInput, backspace and enter are handled here.
    ///
    /// AS THE ORIGINAL'S INPUT LINE (ProcessTypedInput_seg043_37F0_A5E, read 2026-09-26; per
    /// user on the original the same day: the pointer is gone and a mouse button ends it):
    /// the input holds everything - the pointer is hidden, the world stands still and no
    /// click reaches the game - and it ends on Enter, Escape or any mouse button. A mouse
    /// button takes the typed text as Enter does (the shrine does not look at how it ended);
    /// Escape puts "-" behind the prompt and chants the empty text, which is "That is not a
    /// mantra.". At most ten characters (the shrine's call passes 0x0A).
    /// </summary>
    public void BeginMantra()
    {
        if (mbChanting)
            return;

        mbChanting = true;
        msMantra = string.Empty;

        fMantraEntry().Begin();

        fHoldForTypedInput(true);
        fSetMantraLine(UWShrine.Prompt);
    }

    /// <summary>
    /// The running line "Chant the mantra: ..." in the message scroll - the CLASSIC scheme's only:
    /// the modern scheme shows the prompt and the typed text in its box (UWModernQuestion), and
    /// the line stood in its message log as well, up to 0.8.4 (per user, 2026-10-22: only the box).
    /// With no line written, nothing is left to drop or end there.
    /// </summary>
    private void fSetMantraLine(string psLine)
    {
        if (UWControlScheme.Ui.Policy.MantraLineInScroll)
            mOInteraction.SetPromptMessage(psLine);
    }

    /// <summary>The longest mantra that can be typed.</summary>
    private const int MantraMaxLength = 10;

    /// <summary>The modal hold of a typed input (UWControlScheme.HoldUiModal).</summary>
    private const string TypedInputHold = "typed input";

    /// <summary>Set when an input ended by a mouse button: the hold and the mouse block stay
    /// until every button is up, so neither the release nor the held button acts in the game.
    /// </summary>
    private bool mbTypedInputSwallow;

    private void fHoldForTypedInput(bool pbHold)
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;

        if (lOScheme != null)
            lOScheme.SetUiModal(TypedInputHold, pbHold);

        UWMouseButtons.IsBlockedByPrompt = pbHold;

        // The modern scheme's box shows the pointer (UWModernQuestion) - hidden here it stayed
        // invisible under it (as with the yes/no questions).
        if (UWControlScheme.Ui.Policy.PromptHidesPointer)
            UWPointer.IsShown = !pbHold;
    }

    /// <summary>For the modern scheme's mantra box (UWModernQuestion): the typed text so far.</summary>
    public string MantraText => msMantra;

    /// <summary>The mantra box's OK (as Enter) or Cancel (as Escape).</summary>
    public void FinishMantra(bool pbCancel)
    {
        if (mbChanting)
            fFinishMantra(pbCancel, false);
    }

    /// <summary>Lifts the hold of an input that a mouse button ended, once no button is held.
    /// Called before the mantra's Update (Interaction.Update).</summary>
    public void UpdateTypedInputSwallow()
    {
        if (!mbTypedInputSwallow)
            return;

        if (UWPointer.HasMouse && (UWPointer.LeftHeld || UWPointer.RightHeld
            || UWPointer.MiddleHeld))
            return;

        mbTypedInputSwallow = false;
        fHoldForTypedInput(false);
    }

    /// <summary>The mantra's typing (UWTextEntry since 2026-10-19: the text events, the keys
    /// where none arrive, one filter).</summary>
    private UWTextEntry mOMantraEntry;

    private UWTextEntry fMantraEntry()
    {
        if (mOMantraEntry == null)
            mOMantraEntry = new UWTextEntry(fOnMantraInput);

        return mOMantraEntry;
    }

    private void fOnMantraInput(char pcCharacter)
    {
        if (!mbChanting || msMantra.Length >= MantraMaxLength)
            return;

        msMantra += pcCharacter;

        fSetMantraLine(UWShrine.Prompt + msMantra);
    }

    /// <summary>The mantra's keys and mouse, once a frame: true while the mantra is typed - then
    /// nothing else of the world's input runs (Interaction.Update).</summary>
    public bool UpdateMantra()
    {
        if (!mbChanting)
            return false;

        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null)
            return true;

        if (UWTextEntry.BackspacePressed && msMantra.Length > 0)
        {
            msMantra = msMantra.Substring(0, msMantra.Length - 1);

            fSetMantraLine(UWShrine.Prompt + msMantra);
        }

        // Where no text events arrive (Linux), the keys themselves (UWTypedKeys).
        fMantraEntry().ReadKeys();

        // Read straight from the mouse: UWMouseButtons is blocked for the game meanwhile.
        // The modern scheme's box takes the mouse: OK and Cancel (UWModernQuestion).
        bool lbByMouse = UWControlScheme.Ui.Policy.MouseButtonEndsMantra && UWPointer.HasMouse && (UWPointer.LeftPressed
            || UWPointer.RightPressed || UWPointer.MiddlePressed);

        bool lbDone = UWTextEntry.EnterPressed || lbByMouse;

        bool lbCancel = !lbDone && UWBack.Takes(UWBackOwner.Mantra);

        if (!lbDone && !lbCancel)
            return true;

        fFinishMantra(lbCancel, lbByMouse);

        return true;
    }

    private void fFinishMantra(bool lbCancel, bool lbByMouse)
    {
        mbChanting = false;

        fMantraEntry().End();

        // THE MODERN SCHEME: cancelling (Cancel, the right button, Escape) leaves no trace -
        // nothing is chanted (per user, 2026-10-04; since 2026-10-22 its messages never held
        // the line, fSetMantraLine); the classic scheme keeps the original's dash and "That is
        // not a mantra."
        if (lbCancel && !UWControlScheme.Ui.Policy.CancelledMantraChantsDash)
        {
            msMantra = string.Empty;
            fHoldForTypedInput(false);
            return;
        }

        // Escape: the original writes a dash for the answer and chants the empty text.
        if (lbCancel)
        {
            msMantra = string.Empty;
            fSetMantraLine(UWShrine.Prompt + "-");
        }

        mOInteraction.EndPromptMessage();

        if (lbByMouse)
            mbTypedInputSwallow = true;
        else
            fHoldForTypedInput(false);

        UWShrine.Chant(msMantra, mOInteraction.Character, mOInteraction, mOInteraction.GameData);

        msMantra = string.Empty;
    }

    // ------------------------------------------------- Yes/no questions

    /// <summary>
    /// A YES/NO QUESTION IN THE MESSAGE SCROLL - repairing and the trap found on a look ask the
    /// same way (per user from the original, 2026-09-17 on repairing, 2026-09-30: "it must work
    /// exactly like there" for the trap): the pointer disappears while the question stands;
    /// "Yes" is preset; Y (and J) and N only switch the shown answer, Enter confirms it, Escape
    /// answers no; a RIGHT click confirms "Yes", a LEFT click turns it into "No" and cancels -
    /// in the modern scheme a box with OK and Cancel takes the mouse (UWModernQuestion).
    /// Not in the frame the question was asked, whose click chose the item.
    ///
    /// The question, the shown answer, the both-in-one-frame rule and the asker's callback are
    /// UWYesNoQuestion's (engine-free); the keys, buttons and pad presses that mean yes or no,
    /// the asked frame and the hidden pointer are read and kept here.
    /// </summary>
    private readonly UWYesNoQuestion mOQuestion = new UWYesNoQuestion();

    /// <summary>The question hid the pointer (the classic scheme only), so the answer shows it again.</summary>
    private bool mbYesNoHidCursor;

    /// <summary>Frame in which the question was asked.</summary>
    private int miYesNoAskedFrame = -1;

    /// <summary>Whether the pointer was visible before the question hid it (the Modern scheme hides
    /// it anyway).</summary>
    private bool mbYesNoCursorWasVisible = true;

    public void AskYesNo(string psQuestion, System.Action<bool> pOAnswered)
    {
        miYesNoAskedFrame = Time.frameCount;

        // The modern scheme's box shows the pointer itself (UWModernQuestion, under a modal hold);
        // hidden here and shown again by the hold in the same moment, Windows kept it invisible
        // until it left the window (per user, 2026-10-04).
        mbYesNoHidCursor = UWControlScheme.Ui.Policy.PromptHidesPointer;

        if (mbYesNoHidCursor)
        {
            mbYesNoCursorWasVisible = UWPointer.IsShown;
            UWPointer.IsShown = false;
        }

        mOQuestion.Ask(psQuestion, pOAnswered);
        mOInteraction.SetPromptMessage(mOQuestion.PromptText);
    }

    /// <summary>Returns true while the question is running; the rest of the flow is paused
    /// then.</summary>
    public bool UpdateYesNo()
    {
        if (!mOQuestion.IsOpen)
            return false;

        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null)
            return true;

        if (lOKeyboard.yKey.wasPressedThisFrame || lOKeyboard.jKey.wasPressedThisFrame)
            fShowYesNoAnswer(true);
        else if (lOKeyboard.nKey.wasPressedThisFrame)
            fShowYesNoAnswer(false);

        bool lbYes = false;
        bool lbNo = UWBack.Takes(UWBackOwner.YesNo, UWBackKey.Escape);

        // THE GAMEPAD in the classic scheme (per user, 2026-10-07): A yes, B no - the pad's pointer
        // stands aside meanwhile (UWGamepadPointer). The modern box is clicked with the pointer.
        Gamepad lOPad = Gamepad.current;

        if (lOPad != null && Time.frameCount != miYesNoAskedFrame && UWControlScheme.Ui.Policy.PadButtonsAnswerYesNo)
        {
            // A and B as they are bound (PadClick, PadBack: their first binding, cleanup step 7.6 -
            // by their place until then).
            bool lbPadYes = UWInput.PadPressed(UWAct.PadClick, 0, "<Gamepad>/buttonSouth");
            bool lbPadNo = UWBack.Takes(UWBackOwner.YesNo, UWBackKey.PadButton);

            // The answering press clicks nothing more: A is the classic scheme's left button too,
            // and the pad's pointer runs after the question has closed (as the count prompt,
            // 2026-10-22) - it leaves A and the question's B alone while the question was open when
            // the frame began (UWPadOwners; UWGamepad.ConsumePress here until cleanup step 7.8).
            lbYes |= lbPadYes;
            lbNo |= lbPadNo;
        }

        if (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame)
        {
            lbYes = mOQuestion.ShownAnswer;
            lbNo = !mOQuestion.ShownAnswer;
        }

        // THE MODERN SCHEME answers with the mouse in its own box (UWModernQuestion, per user
        // 2026-10-04: OK and Cancel, every question there alike; a right click both answered and
        // switched the pointer).
        if (UWPointer.HasMouse && Time.frameCount != miYesNoAskedFrame && UWControlScheme.Ui.Policy.MouseButtonsAnswerYesNo)
        {
            lbYes |= UWMouseButtons.RightPressed;
            lbNo |= UWMouseButtons.LeftPressed;
        }

        // Both in one frame answer no (UWYesNoQuestion.DecideAnswer); the line and the pointer
        // come before the asker runs.
        if (!UWYesNoQuestion.DecideAnswer(lbYes, lbNo, out bool lbAnswer))
            return true;

        fFinishYesNo(lbAnswer);

        return true;
    }

    /// <summary>For the modern scheme's question box (UWModernQuestion): a question is open, and
    /// its text.</summary>
    public bool IsAskingYesNo => mOQuestion.IsOpen;

    public string YesNoQuestion => mOQuestion.Question;

    /// <summary>The modern question box's OK (true) or Cancel (false).</summary>
    public void AnswerYesNo(bool pbYes)
    {
        if (mOQuestion.IsOpen)
            fFinishYesNo(pbYes);
    }

    private void fFinishYesNo(bool lbYes)
    {
        if (mbYesNoHidCursor)
            UWPointer.IsShown = mbYesNoCursorWasVisible;

        // The answer stays in the log: a cancel shows "No".
        mOInteraction.SetPromptMessage(mOQuestion.PromptTextFor(lbYes));
        mOInteraction.EndPromptMessage();

        // Closes the question, then hands the answer to the asker.
        mOQuestion.Answer(lbYes);
    }

    private void fShowYesNoAnswer(bool pbYes)
    {
        mOQuestion.ShowAnswer(pbYes);
        mOInteraction.SetPromptMessage(mOQuestion.PromptText);
    }
}
