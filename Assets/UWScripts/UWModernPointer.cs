using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE MODERN SCHEME'S POINTER (per user, 2026-10-03: "we are very close to the MMO controls"):
/// one state, free or locked for the mouse look, shared by everything that frees it.
///
///   - The RIGHT BUTTON switches it - by default a click toggles (per user: holding a button all
///     the time is odd in a first-person game). As an option of the game menu, the MMO way round
///     (UWUserSettings.ModernPointerHold, per user): the pointer is free and the view turns only
///     while the right button is held; the left button on the world outside the windows then
///     strikes as with the locked pointer (WorldTakesClicks). Over a window of the modern UI the
///     right button stays theirs (use).
///   - Nothing else switches it (per user, 2026-10-04): the bags (B), the character panel (C, its
///     handle, its pin) and the help (Tab) open and close without touching it.
///   - Locking with a thing on the pointer first puts it back (UWModernBags.ReturnCursorItem).
///
/// The look moved to Q (Interaction), and the spells will be cast from the action bar instead of
/// a held right button.
/// </summary>
public static class UWModernPointer
{
    private const string Hold = "modern pointer";

    /// <summary>The frame the right button switched the pointer: that press uses nothing.</summary>
    public static int SwitchedFrame { get; private set; } = -1;

    /// <summary>Holding the right button locked the pointer to look around; it frees again on the
    /// release.</summary>
    private static bool msbHeldLook;

    /// <summary>The MMO way round: free by default, the view only while the right button is held.</summary>
    public static bool HoldToLook => UWUserSettings.ModernPointerHold;

    private static UWControlScheme fScheme()
    {
        return UWScene.ControlScheme;
    }

    public static bool IsFree
    {
        get
        {
            UWControlScheme lOScheme = fScheme();

            return lOScheme != null && lOScheme.IsPointerFree;
        }
    }

    public static void Free()
    {
        UWControlScheme lOScheme = fScheme();

        if (lOScheme != null)
            lOScheme.HoldPointer(Hold);
    }

    /// <summary>Locks the pointer for the mouse look - unless a thing hangs on it and finds no
    /// place to go back to (false).</summary>
    public static bool Lock()
    {
        UWModernBags lOBags = UWModernBags.Instance;

        if (lOBags != null)
            lOBags.CancelUse();

        if (lOBags != null && !lOBags.ReturnCursorItem())
            return false;

        UWControlScheme lOScheme = fScheme();

        if (lOScheme != null)
            lOScheme.ReleasePointer(Hold);

        return true;
    }

    /// <summary>Leaving the modern scheme: the hold goes, whatever hangs on the pointer stays
    /// for the classic drag (UWItemDrag).</summary>
    public static void Release()
    {
        UWControlScheme lOScheme = fScheme();

        if (lOScheme != null)
            lOScheme.ReleasePointer(Hold);

        msbHeldLook = false;
    }

    /// <summary>With the right button held to look and the pointer free otherwise, the left button
    /// on the world strikes as with the locked pointer - not over a window, not with a thing on
    /// the pointer (that one is dropped or thrown, UWModernBags).</summary>
    public static bool WorldTakesClicks()
    {
        return HoldToLook && FreePointerOnWorld();
    }

    /// <summary>The free pointer lies on the world: not on a window, nothing on it, no split box,
    /// menu, question or use mode taking the click. With the weapon drawn the left button strikes
    /// there (Interaction.fMouseInWorld, per user 2026-10-04).</summary>
    public static bool FreePointerOnWorld()
    {
        // The split box's clicks are its own (UWModernBags.StackSplit), and so is the use mode's.
        if (!IsFree || Mouse.current == null || UWModernBags.IsSplitting || UWModernBags.IsMenuOpen
            || UWModernQuestion.IsBlocking
            || (UWModernBags.Instance != null && UWModernBags.Instance.IsUsing))
            return false;

        UWInventory lOInventory = UWScene.GameUi != null ? UWScene.GameUi.mOInventory : null;

        if (lOInventory != null && lOInventory.CursorItem != null)
            return false;

        return !fIsOverUi(Mouse.current.position.ReadValue());
    }

    /// <summary>Whether a screen point lies on a window of the modern UI (for Interaction's aim).</summary>
    public static bool IsOverUi(Vector2 pOPointer)
    {
        return fIsOverUi(pOPointer);
    }

    /// <summary>Whether a screen point lies on a window of the modern UI - there the right button
    /// uses instead of switching.</summary>
    private static bool fIsOverUi(Vector2 pOPointer)
    {
        return (UWModernBags.Instance != null && UWModernBags.Instance.IsOverWindow(pOPointer))
            || (UWModernPanel.Instance != null && UWModernPanel.Instance.Contains(pOPointer))
            || (UWModernRunePanel.Instance != null && UWModernRunePanel.Instance.Contains(pOPointer))
            || (UWModernQuestion.Instance != null && UWModernQuestion.Instance.Contains(pOPointer))
            || (UWModernHud.Instance != null && UWModernHud.Instance.IsOverSpellIcons(pOPointer))
            || (UWModernMinimap.Instance != null && UWModernMinimap.Instance.Contains(pOPointer))
            || (UWModernActionBar.Instance != null && UWModernActionBar.Instance.ScreenRect.Contains(pOPointer));
    }

    /// <summary>The right button, read once a frame by UWModernHud.</summary>
    public static void Tick(bool pbActive)
    {
        UWControlScheme lOScheme = fScheme();

        if (lOScheme == null)
            return;

        if (lOScheme.Current != UWControlScheme.SchemeEnum.Modern)
        {
            Release();
            return;
        }

        // The question box takes the right button as its Cancel (UWModernQuestion).
        if (!pbActive || lOScheme.IsUIModalOpen || lOScheme.IsConversationOpen || lOScheme.IsInputHeld || Mouse.current == null
            || UWModernQuestion.IsBlocking || UWModernBags.IsMenuOpen)
            return;

        UWControls lOControls = lOScheme.Controls;

        if (lOControls == null)
            return;

        UnityEngine.InputSystem.InputAction lOButton = lOControls.Player.Interact;
        Vector2 lOPointer = Mouse.current.position.ReadValue();
        bool lbOverUi = IsFree && fIsOverUi(lOPointer);

        if (HoldToLook)
        {
            if (msbHeldLook)
            {
                if (!lOButton.IsPressed())
                {
                    msbHeldLook = false;
                    Free();
                }

                return;
            }

            // Free is the rest state.
            if (!IsFree)
                Free();

            if (lOButton.WasPressedThisFrame() && !lbOverUi && Lock())
            {
                msbHeldLook = true;
                SwitchedFrame = Time.frameCount;
            }

            return;
        }

        msbHeldLook = false;

        if (!lOButton.WasPressedThisFrame() || lbOverUi)
            return;

        SwitchedFrame = Time.frameCount;

        if (IsFree)
            Lock();
        else
            Free();
    }
}
