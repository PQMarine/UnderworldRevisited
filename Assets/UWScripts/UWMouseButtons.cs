using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// The two world buttons, wherever they currently sit (2026-09-17).
///
/// WHY: whoever cannot press a right button - a one-button mouse, a trackpad, the mouse driven
/// from the keyboard - puts "Look and use" onto a key in the menu bar (UWKeyBindings). That only
/// helps if the interface follows as well, and the panels read the mouse directly (inventory
/// slots, paper doll, options panel, map, conversation). They ask here instead.
///
/// The control is looked up from the binding in force - by default that is the left or right
/// mouse button, so nothing changes until the player moves it. The lookup is cached and thrown
/// away when a binding changes (UWKeyBindings.Set and ResetAll).
/// </summary>
public static class UWMouseButtons
{
    private const string LeftAction = "CursorDrag";

    private const string RightAction = "Interact";

    private static ButtonControl mOLeft;

    private static ButtonControl mORight;

    private static string msLeftPath;

    private static string msRightPath;

    // WHILE THE RIGHT BUTTON IS HELD A LEFT CLICK IS IGNORED - everywhere, over the UI as well,
    // in every mode except the combat mode (per user on the original, 2026-09-19, found while
    // testing the talk mode). The right-button
    // gesture keeps the mouse to itself. A press that fell into that window is forgotten for
    // good: neither its hold nor its release counts, otherwise a drop or a cursor walk could
    // start from a click that never happened. Read here, once, by everyone who asks for the
    // left button in the classic scheme - the movement, the inventory, the HUD.
    public static bool LeftPressed => !fIsLeftIgnored() && fPressed(fGetLeft());

    public static bool LeftReleased => !fIsLeftIgnored() && fReleased(fGetLeft());

    public static bool LeftHeld => !fIsLeftIgnored() && fHeld(fGetLeft());

    private static int msiLeftIgnoredFrame = -1;

    private static bool msbLeftIgnored;

    /// <summary>Latched per frame: a press under a held right button marks the whole press as
    /// ignored until the left button is up again.</summary>
    private static bool fIsLeftIgnored()
    {
        if (msiLeftIgnoredFrame != UnityEngine.Time.frameCount)
        {
            msiLeftIgnoredFrame = UnityEngine.Time.frameCount;

            ButtonControl lOLeft = fGetLeft();

            // EXCEPT IN COMBAT MODE (per user on the original, 2026-09-19): there the right button
            // is the weapon, and a left click beside it still counts.
            if (fPressed(lOLeft))
                msbLeftIgnored = fHeld(fGetRight())
                    && !(UWScene.Interaction != null && UWScene.Interaction.IsCombatModeActive);
            else if (!fHeld(lOLeft) && !fReleased(lOLeft))
                msbLeftIgnored = false;
        }

        return msbLeftIgnored;
    }

    public static bool RightPressed => fPressed(fGetRight());

    public static bool RightReleased => fReleased(fGetRight());

    public static bool RightHeld => fHeld(fGetRight());

    /// <summary>Either of the two, plus the middle mouse button - for "any click" cases.</summary>
    public static bool AnyPressed
    {
        get
        {
            return LeftPressed || RightPressed || (UWPointer.HasMouse && UWPointer.MiddlePressed);
        }
    }

    /// <summary>After a change in the menu bar the controls are looked up again.</summary>
    public static void Forget()
    {
        mOLeft = null;
        mORight = null;
        msLeftPath = null;
        msRightPath = null;
    }

    private static ButtonControl fGetLeft()
    {
        string lsPath = fGetPath(LeftAction, "<Mouse>/leftButton");

        if (mOLeft == null || lsPath != msLeftPath)
        {
            msLeftPath = lsPath;
            mOLeft = InputSystem.FindControl(lsPath) as ButtonControl;
        }

        return mOLeft;
    }

    private static ButtonControl fGetRight()
    {
        string lsPath = fGetPath(RightAction, "<Mouse>/rightButton");

        if (mORight == null || lsPath != msRightPath)
        {
            msRightPath = lsPath;
            mORight = InputSystem.FindControl(lsPath) as ButtonControl;
        }

        return mORight;
    }

    /// <summary>The action's key or button in force (UWBindingInfo.PathOr since cleanup step 7.2,
    /// 2026-10-25; a lookup of its own here until then).</summary>
    private static string fGetPath(string psAction, string psDefault)
    {
        return UWBindingInfo.PathOr(psAction, psDefault);
    }

    /// <summary>No button reaches the game - the help window has the mouse (UWHelpWindow: while
    /// reading the manual, or over its panel), a typed input holds it (the mantra prompt,
    /// Interaction) or the pointer is on the debug panel (UWDebugOverlay, cleanup 8c: a click on a
    /// section's heading must not walk, use or strike). Three owners, so that one does not lift
    /// another's block.</summary>
    public static bool IsBlocked => IsBlockedByHelp || IsBlockedByPrompt || IsBlockedByDebug;

    public static bool IsBlockedByHelp { get; set; }

    public static bool IsBlockedByPrompt { get; set; }

    public static bool IsBlockedByDebug { get; set; }

    // THE PAD'S CLICKS COUNT AS THE MOUSE'S (cleanup step 7.7B, 2026-10-25): the pad presses a mouse of
    // its own (UWPointer.PadButton), and the binding's path finds the system mouse's control - so a mouse
    // button is asked on both (UWPointer.Pressed/Held/Released), as the pad's system clicks counted before.

    private static bool fPressed(ButtonControl pOControl)
    {
        return !IsBlocked && pOControl != null && UWPointer.Pressed(pOControl);
    }

    private static bool fReleased(ButtonControl pOControl)
    {
        return !IsBlocked && pOControl != null && UWPointer.Released(pOControl);
    }

    private static bool fHeld(ButtonControl pOControl)
    {
        return !IsBlocked && pOControl != null && UWPointer.Held(pOControl);
    }
}
