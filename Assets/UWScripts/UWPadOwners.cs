using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE PAD PRESS'S OWNERS, THE UNITY SIDE (cleanup step 7.8, 2026-10-25; UWGamepad.ConsumePress until
/// then, a frame mark each place set when it took a pad press). The places that wait for a pad press -
/// the window picture, the paging, the conversation's "[MORE]" and end, the classic count prompt and
/// yes/no, the modern split box - read the pad as before. Whether one of them owns the frame's press,
/// so that the pad's pointer (UWGamepadPointer, the last script of the frame) sends no click with it,
/// UWPressOwners decides: from the places that wait when the frame begins (Tick, called by
/// UWPointerDriver before every other script - the state when the press came, before any of them has
/// acted on it) and the frame's presses (asked when the pointer asks).
/// </summary>
public static class UWPadOwners
{
    private static int msiFrame = -1;

    private static UWPadPressState meStates;

    /// <summary>Once a frame before every other script (UWPointerDriver): the places that wait for a
    /// pad press as they stand now - only in a frame with a pad button pressed, where they matter.</summary>
    public static void Tick()
    {
        msiFrame = Time.frameCount;
        meStates = UWGamepad.AnyPressed(false) ? fStates() : UWPadPressState.None;
    }

    /// <summary>The pad's pointer may click with this frame's press: no place that waits for a pad
    /// press owns it (UWPressOwners.PointerClicks).</summary>
    public static bool PointerClicks
    {
        get
        {
            // Without the driver's tick this frame (it runs in every scene): the places as they are now.
            if (msiFrame != Time.frameCount)
                Tick();

            if (meStates == UWPadPressState.None)
                return true;

            return UWPressOwners.PointerClicks(fPresses(), meStates, leOwner => UWBack.Takes(leOwner, UWBackKey.PadButton));
        }
    }

    /// <summary>The frame's presses: a pad button (Tick found one), the prompts' A and X as they are
    /// bound (PadClick, PadContext, their first binding - what the prompts read), and RT/LT (their
    /// second binding, the count prompt's mouse buttons).</summary>
    private static UWPadPress fPresses()
    {
        UWPadPress lePresses = UWPadPress.Button;

        if (UWInput.PadPressed(UWAct.PadClick, 0, "<Gamepad>/buttonSouth"))
            lePresses |= UWPadPress.Click;

        if (UWInput.PadPressed(UWAct.PadContext, 0, "<Gamepad>/buttonWest"))
            lePresses |= UWPadPress.Context;

        if (UWInput.PadPressed(UWAct.PadClick, 1, "<Gamepad>/rightTrigger") || UWInput.PadPressed(UWAct.PadContext, 1, "<Gamepad>/leftTrigger"))
            lePresses |= UWPadPress.Triggers;

        return lePresses;
    }

    /// <summary>The places that wait for a pad press, each as its own reader tests it.</summary>
    private static UWPadPressState fStates()
    {
        UWPadPressState leStates = UWPadPressState.None;
        Interaction lOInteraction = UWScene.Interaction;
        UWItemDrag lODrag = UWScene.ItemDrag;
        UWConversationController lOConversation = UWScene.ConversationController;
        UWModernBags lOBags = UWModernBags.Instance;

        // The yes/no as the pad's pointer stands aside for it (UWGamepadPointer, PadPointerDrives);
        // the paging and the window picture as the back chain asks them (Interaction gets to them).
        fSet(ref leStates, UWPadPressState.YesNo, lOInteraction != null && lOInteraction.IsAskingYesNo
            && UWControlScheme.Ui.Policy.PadButtonsAnswerYesNo);
        fSet(ref leStates, UWPadPressState.Paging, lOInteraction != null && lOInteraction.PagingTakesKeys);
        fSet(ref leStates, UWPadPressState.WindowPicture, lOInteraction != null && lOInteraction.PictureTakesKeys);
        fSet(ref leStates, UWPadPressState.ConversationMore, lOConversation != null && lOConversation.MoreTakesPad);
        fSet(ref leStates, UWPadPressState.ConversationEnd, lOConversation != null && lOConversation.EndTakesPad);
        fSet(ref leStates, UWPadPressState.CountPrompt, lODrag != null && lODrag.IsAskingCount);
        fSet(ref leStates, UWPadPressState.BagSplit, lOBags != null && lOBags.SplitTakesPad);

        return leStates;
    }

    private static void fSet(ref UWPadPressState peStates, UWPadPressState peState, bool pbHolds)
    {
        if (pbHolds)
            peStates |= peState;
    }
}
