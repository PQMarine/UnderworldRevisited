using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE [MORE] WAIT'S INPUT, ONE ROUTINE FOR BOTH GAMES (UW2 plan stage U6, 2026-11-24, per user: on UW2 one click went
/// over more than one [MORE] - "a candidate for a common routine"). The first game's log (UWMessageLog.UpdatePaging),
/// the second game's screen (UWGameScreenUw2.fUpdatePaging) and its conversation's answer scroll (UWConversationScreenUw2
/// .fUpdateLookPaging) each read the press themselves until then. Here:
///   - any key, mouse button or pad button pages on, as scroll_wait;
///   - ONE PAGE PER FRAME, whoever asks how often;
///   - after a mouse press the buttons must come up before the next one pages (the original's mouse_release), so a
///     click's press and its release are one page;
///   - ConsumedThisFrame tells the others (the inventory's drag, the pad's owners, the back chain) that this frame's
///     press was the paging's.
/// </summary>
public static class UWPageInput
{
    private static int miConsumedFrame = -1;

    private static bool mbWaitRelease;

    /// <summary>The press of this frame was the paging's.</summary>
    public static bool ConsumedThisFrame => miConsumedFrame == Time.frameCount;

    /// <summary>
    /// Whether the text pages on now: true at most once a frame. pbKeyboard: the keyboard counts (not while a text field
    /// or the help window has it, for the conversation).
    /// </summary>
    public static bool TryConfirm(bool pbKeyboard = true)
    {
        if (ConsumedThisFrame)
            return false;

        bool lbHeld = UWMouseButtons.LeftHeld || UWMouseButtons.RightHeld;

        // The press that paged is let go first.
        if (mbWaitRelease)
        {
            if (lbHeld)
                return false;

            mbWaitRelease = false;
        }

        Keyboard lOKeyboard = Keyboard.current;
        bool lbMouse = UWPointer.HasMouse && (UWMouseButtons.LeftPressed || UWMouseButtons.RightPressed);
        bool lbConfirm = lbMouse || (pbKeyboard && lOKeyboard != null && lOKeyboard.anyKey.wasPressedThisFrame) || UWGamepad.AnyPressed(false);

        if (!lbConfirm)
            return false;

        miConsumedFrame = Time.frameCount;
        mbWaitRelease = lbMouse;

        return true;
    }
}
