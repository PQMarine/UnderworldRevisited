using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE CLASSIC "Move how many?" PROMPT (cleanup step 5.13, 2026-10-22: out of UWItemDrag, on the
/// engine-free UWCountInput the modern split box reads as well): the line in the message scroll,
/// the keys, the mouse and the gamepad that answer it. UWItemDrag opens it on a stack (Begin) and
/// acts on how it ended (Update's Outcome: the count taken, or nothing).
///
/// The quantity prompt, as in the original.
///
/// When a drag starts on a stack, "Move how many? 1" appears with a blinking
/// square after it. Enter accepts the shown number, typing replaces it, a
/// left click takes one immediately, a right click takes all (per user, 2026-09-01).
///
/// Whether the drag was started with left or right does not matter.
/// </summary>
public sealed class UWCountPrompt
{
    /// <summary>The question in the scroll; the typed number follows it.</summary>
    public const string Question = "Move how many? ";

    /// <summary>How an Update ended: the prompt still runs, a count was taken, or nothing.</summary>
    public enum Outcome
    {
        Running,
        Taken,
        Cancelled
    }

    private readonly UWCountInput mOInput = new UWCountInput(UWCountInput.ClassicPrompt);

    private readonly Interaction mOInteraction;

    /// <summary>The digits typed, by the key's place (UWTextEntry.Digits, cleanup step 7.1, 2026-10-25;
    /// the prompt read the digit row and the keypad itself until then, the same keys).</summary>
    private readonly UWTextEntry mODigits;

    public UWCountPrompt(Interaction pOInteraction)
    {
        mOInteraction = pOInteraction;
        mODigits = UWTextEntry.Digits(pcDigit => mOInput.TypeDigit(pcDigit - '0'));
    }

    /// <summary>The prompt is running (the gamepad's pointer and letter grid stand aside,
    /// UWGamepadPointer, UWTextEntryState.IsTypingText).</summary>
    public bool IsAsking => mOInput.IsOpen;

    /// <summary>The count the prompt took, between 1 and the stack - read after Outcome.Taken.</summary>
    public int TakenCount => mOInput.TakenCount;

    /// <summary>Opens the prompt on a stack of piMaximum: "Move how many? 1".</summary>
    public void Begin(int piMaximum)
    {
        mOInput.Begin(piMaximum);
        mODigits.Begin();

        // During the prompt the pointer is hidden (per user, 2026-09-01).
        UWPointer.IsShown = false;

        fRefresh();
    }

    private void fRefresh()
    {
        if (mOInteraction == null)
            return;

        // Without the cursor - UWGameUI appends it to the last line while the prompt is running.
        mOInteraction.SetPromptMessage(Question + mOInput.Text);
    }

    /// <summary>
    /// Ends the prompt and picks up the quantity - the line keeps the number as it was answered,
    /// the count taken is clamped to the stack (UWCountInput.Commit).
    ///
    /// AFTERWARDS THE ITEM STICKS TO THE POINTER, it no longer depends on a held button.
    /// Otherwise releasing the button just used to answer would already count as
    /// placing: a right click for "all" picked up the stack and threw it away again
    /// in the same move (per user, 2026-09-07). UWItemDrag lets the held drag go on Taken.
    ///
    /// When answering via the keyboard there would be the same problem from the other side:
    /// if the button is released during the prompt, that release falls into a frame
    /// in which the drag flow does not run at all - the held drag would then wait for a
    /// release that never comes.
    /// </summary>
    private Outcome fEnd(int piCount)
    {
        if (mOInteraction != null)
        {
            mOInteraction.SetPromptMessage(Question + piCount);
            mOInteraction.EndPromptMessage();
        }

        mOInput.Commit(piCount);
        mODigits.End();

        return Outcome.Taken;
    }

    /// <summary>
    /// Cancel without picking up. On Esc the original writes a dash as the chosen number,
    /// on Enter with an empty field, however, nothing at all - the line simply stays
    /// (per user, 2026-09-01). The pointer the prompt hid comes back.
    /// </summary>
    private Outcome fCancel(bool pbShowDash)
    {
        if (mOInteraction != null)
        {
            mOInteraction.SetPromptMessage(Question + (pbShowDash ? "-" : string.Empty));
            mOInteraction.EndPromptMessage();
        }

        mOInput.Cancel();
        mODigits.End();
        UWPointer.IsShown = true;

        return Outcome.Cancelled;
    }

    /// <summary>Keyboard, mouse and gamepad during the prompt, once a frame while IsAsking: Running
    /// while it goes on - then the rest of the drag flow must not run - else how it ended.</summary>
    public Outcome Update()
    {
        if (!mOInput.IsOpen)
            return Outcome.Cancelled;

        fRefresh();

        if (UWPointer.HasMouse && UWMouseButtons.LeftPressed)
            return fEnd(1);

        if (UWPointer.HasMouse && UWMouseButtons.RightPressed)
            return fEnd(mOInput.Maximum);

        Outcome leByPad = fUpdatePad();

        if (leByPad != Outcome.Running)
            return leByPad;

        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null)
            return Outcome.Running;

        if (UWTextEntry.EnterPressed)
        {
            // Enter with an empty field cancels, but leaves no dash - unlike
            // Esc (per user, 2026-09-01). The line shows the number as typed, the count
            // taken is clamped (fEnd).
            if (!mOInput.HasTypedNumber)
                return fCancel(false);

            return fEnd(mOInput.Value);
        }

        if (UWBack.Takes(UWBackOwner.CountPrompt, UWBackKey.Escape))
            return fCancel(true);

        // The preset 1 disappears on the FIRST key press, whichever key. If it was
        // not a digit, the field stays empty and the cursor keeps blinking (per user,
        // 2026-09-01). This is also why the first digit replaces the 1 instead of
        // appending to it - it is the same rule, not two.
        if (lOKeyboard.anyKey.wasPressedThisFrame)
            mOInput.FirstKeyPressed();

        if (UWTextEntry.BackspacePressed)
            mOInput.Backspace();

        mODigits.ReadKeys();

        return Outcome.Running;
    }

    /// <summary>
    /// THE GAMEPAD ON THE COUNT (per user, 2026-10-07: no letter grid, the d-pad turns the number):
    /// up and down step it, round from the maximum to 1 and back, held faster and after two
    /// seconds by ten (the repeat and the round are UWCountInput's); A takes that many, X all,
    /// B cancels. Running while the prompt goes on.
    /// </summary>
    private Outcome fUpdatePad()
    {
        Gamepad lOPad = Gamepad.current;

        if (lOPad == null)
            return Outcome.Running;

        // THE PRESS IS THE PROMPT'S: A and X are the classic scheme's mouse buttons as well, and
        // the pad's pointer runs later in the frame, when the prompt is already over - it clicked
        // with the same press and put the count straight back into its slot (per user, 2026-10-22).
        // The pointer leaves A, X and the prompt's B alone while the prompt was open when the frame
        // began (UWPadOwners, the owner table UWPressOwners; UWGamepad.ConsumePress here until
        // cleanup step 7.8). The split box does the same. A, X and B ARE THE POINTER'S CLICK, ITS
        // RIGHT BUTTON AND BACK as they are bound (PadClick, PadContext, PadBack, their first
        // binding; cleanup step 7.6 - read by their place until then); the d-pad by its place.
        if (UWInput.PadPressed(UWAct.PadClick, 0, "<Gamepad>/buttonSouth"))
            return fEnd(mOInput.Value > 0 ? mOInput.Value : 1);

        if (UWInput.PadPressed(UWAct.PadContext, 0, "<Gamepad>/buttonWest"))
            return fEnd(mOInput.Maximum);

        // RT AND LT ARE THE MOUSE'S BUTTONS HERE (per user, 2026-10-25): the classic scheme's pad
        // clicks with them, but the pointer has no buttons while the prompt runs - so they answer as
        // the left and the right button above do, one and all (PadClick, PadContext, their second
        // binding).
        if (UWInput.PadPressed(UWAct.PadClick, 1, "<Gamepad>/rightTrigger"))
            return fEnd(1);

        if (UWInput.PadPressed(UWAct.PadContext, 1, "<Gamepad>/leftTrigger"))
            return fEnd(mOInput.Maximum);

        if (UWBack.Takes(UWBackOwner.CountPrompt, UWBackKey.PadButton))
            return fCancel(true);

        int liDirection = (lOPad.dpad.up.isPressed ? 1 : 0) - (lOPad.dpad.down.isPressed ? 1 : 0);

        if (mOInput.HoldDirection(liDirection, Time.unscaledTime))
            fRefresh();

        return Outcome.Running;
    }
}
