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

    /// <summary>Put the pointer onto the first entry once the entries are there.</summary>
    public bool SnapPending;

    private bool mbStickHeld;

    /// <summary>Asks for the snap when the gamepad was used last (a menu opening).</summary>
    public void Opened()
    {
        SnapPending = UWGamepad.IsActive;
        mbStickHeld = true;
    }

    /// <summary>Once a frame while the menu is open, with its entries' screen rectangles (pixels,
    /// bottom-left origin) in their order.</summary>
    public void Update(IReadOnlyList<Rect> pOEntries)
    {
        msiLastFrame = Time.frameCount;

        Gamepad lOPad = Gamepad.current;
        Mouse lOMouse = Mouse.current;

        if (lOPad == null || lOMouse == null || pOEntries == null || pOEntries.Count == 0)
            return;

        if (SnapPending)
        {
            SnapPending = false;
            fPut(pOEntries[0].center);
            return;
        }

        int liStep = lOPad.dpad.down.wasPressedThisFrame ? 1 : lOPad.dpad.up.wasPressedThisFrame ? -1 : 0;

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

        Vector2 lOPointer = UWGamepadPointer.OwnsPosition ? UWGamepadPointer.Position : lOMouse.position.ReadValue();
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

    private static void fPut(Vector2 pOAt)
    {
        Mouse.current.WarpCursorPosition(pOAt);
        UWGamepadPointer.PlaceAt(pOAt);
        UWGamepad.NoteSyntheticMouse();
    }
}
