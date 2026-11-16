using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// A YES/NO QUESTION ON A PROMPT LINE, the input side (moved out of UWPromptController for the second game's trap question,
/// G3 part H2, 2026-11-13): the keys, buttons and pad presses that mean yes or no, the asked frame and the hidden pointer;
/// the question itself is UWYesNoQuestion's. Where the line shows is the owner's: the first game's prompt line
/// (Interaction), the second game's scroll (UWGameScreenUw2).
/// </summary>
public sealed class UWYesNoPrompt
{
    private readonly Action<string> mFShow;

    private readonly Action mFEnd;

    public UWYesNoPrompt(Action<string> pFShow, Action pFEnd)
    {
        mFShow = pFShow;
        mFEnd = pFEnd;
    }

    private readonly UWYesNoQuestion mOQuestion = new UWYesNoQuestion();

    /// <summary>The question hid the pointer (the classic scheme only), so the answer shows it again.</summary>
    private bool mbYesNoHidCursor;

    /// <summary>Frame in which the question was asked.</summary>
    private int miYesNoAskedFrame = -1;

    /// <summary>Whether the pointer was visible before the question hid it (the Modern scheme hides
    /// it anyway).</summary>
    private bool mbYesNoCursorWasVisible = true;

    public void Ask(string psQuestion, System.Action<bool> pOAnswered)
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
        mFShow(mOQuestion.PromptText);
    }

    /// <summary>Returns true while the question is running; the rest of the flow is paused
    /// then.</summary>
    public bool Update()
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
    public bool IsOpen => mOQuestion.IsOpen;

    public string Question => mOQuestion.Question;

    /// <summary>The modern question box's OK (true) or Cancel (false).</summary>
    public void AnswerNow(bool pbYes)
    {
        if (mOQuestion.IsOpen)
            fFinishYesNo(pbYes);
    }

    private void fFinishYesNo(bool lbYes)
    {
        if (mbYesNoHidCursor)
            UWPointer.IsShown = mbYesNoCursorWasVisible;

        // The answer stays in the log: a cancel shows "No".
        mFShow(mOQuestion.PromptTextFor(lbYes));
        mFEnd();

        // Closes the question, then hands the answer to the asker.
        mOQuestion.Answer(lbYes);
    }

    private void fShowYesNoAnswer(bool pbYes)
    {
        mOQuestion.ShowAnswer(pbYes);
        mFShow(mOQuestion.PromptText);
    }
}
