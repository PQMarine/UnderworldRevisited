using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE GAMEPAD STEPPING THROUGH A MENU'S ENTRIES (per user, 2026-10-08: the d-pad and the left stick
/// move through buttons and options, in the context menu and the game menu alike): up and down on
/// the d-pad, or a push of the walk stick, put the pointer onto the next entry - A then chooses it
/// with the pad's click, the hover follows as with the mouse. Opened with the pad in use, the
/// pointer goes onto the first entry. While a stepper runs the stick neither walks nor scrolls
/// (IsActive, UWGamepadPointer).
/// </summary>
public sealed class UWPadEntryStepper
{
    private static int msiLastFrame = -10;

    /// <summary>A menu stepped by the pad is open (this frame or the last).</summary>
    public static bool IsActive => Time.frameCount - msiLastFrame <= 1;

    private static int msiYieldFrame = -10;

    /// <summary>The walk stick is the wheel's, not the stepper's (this frame or the last): the
    /// pointer stands on the area Update was given for scrolling (UWGamepadPointer turns the wheel).</summary>
    public static bool StickYields => Time.frameCount - msiYieldFrame <= 1;

    /// <summary>Put the pointer onto the first entry once the entries are there.</summary>
    public bool SnapPending;

    private bool mbStickHeld;

    /// <summary>Asks for the snap when the gamepad was used last (a menu opening).</summary>
    public void Opened()
    {
        SnapPending = UWGamepad.IsActive;
        mbStickHeld = true;
    }

    /// <summary>
    /// Once a frame while the menu is open, with its entries' screen rectangles (pixels,
    /// bottom-left origin) in their order. pOWheelArea (screen pixels, empty for none): WITH THE
    /// POINTER ON IT THE WALK STICK SCROLLS instead of stepping, the d-pad still steps (per user,
    /// 2026-10-20: a long speech in the modern conversation could not be scrolled with the pad -
    /// the stick over the history scrolls it, over the answers it steps them).
    /// </summary>
    public void Update(IReadOnlyList<Rect> pOEntries, Rect pOWheelArea = default)
    {
        msiLastFrame = Time.frameCount;

        Gamepad lOPad = Gamepad.current;

        if (lOPad == null || !UWPointer.HasMouse || pOEntries == null || pOEntries.Count == 0)
            return;

        if (SnapPending)
        {
            SnapPending = false;
            fPut(pOEntries[0].center);
            return;
        }

        int liStep = lOPad.dpad.down.wasPressedThisFrame ? 1 : lOPad.dpad.up.wasPressedThisFrame ? -1 : 0;
        Vector2 lOPointer = UWPointer.Position;
        bool lbWheel = pOWheelArea.width > 0f && pOWheelArea.Contains(lOPointer);

        // On the wheel's area the stick counts as held, so that leaving it with the stick still
        // pushed does not step at once.
        if (lbWheel)
        {
            msiYieldFrame = Time.frameCount;
            mbStickHeld = true;
        }

        // THE WALK STICK READ ON THE PAD ITSELF: the context menu holds the keys (UWControls.
        // SetTextEntryActive), and Move, having keys, is switched off with them - the stick stepped
        // nothing there (per user, 2026-10-08). Swapped sticks swap it.
        float lfStick = (UWUserSettings.GamepadSwapSticks ? lOPad.rightStick : lOPad.leftStick).ReadValue().y;

        if (Mathf.Abs(lfStick) < 0.3f)
            mbStickHeld = false;
        else if (!mbStickHeld && Mathf.Abs(lfStick) >= 0.6f)
        {
            mbStickHeld = true;

            if (liStep == 0)
                liStep = lfStick < 0f ? 1 : -1;
        }

        if (liStep == 0)
            return;

        int liAt = -1;

        for (int liEntry = 0; liEntry < pOEntries.Count; liEntry++)
        {
            if (pOEntries[liEntry].Contains(lOPointer))
                liAt = liEntry;
        }

        // Off the entries: down starts at the first, up at the last.
        liAt = liAt < 0 ? (liStep > 0 ? 0 : pOEntries.Count - 1) : (liAt + liStep + pOEntries.Count) % pOEntries.Count;

        fPut(pOEntries[liAt].center);
    }

    /// <summary>The pointer onto an entry (UWPointer.MoveTo: ours in the game, the system's by a warp in
    /// the menus; a warp and UWGamepadPointer.PlaceAt until cleanup step 7.7B).</summary>
    private static void fPut(Vector2 pOAt)
    {
        UWPointer.MoveTo(pOAt, UWPointer.SourceEnum.Pad);
    }
}
