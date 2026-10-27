using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE GAMEPAD (per user, 2026-10-07; tested with an Xbox controller). Stage 1 plays in the world
/// in the modern scheme, the bindings in UWControls (Xbox names):
///
///   left stick walk and strafe (analog), right stick look, A use (held: use directly), X look,
///   B close what is open (bags, panels, the menu, a waiting spell; the back chain, UWBackChain), Y jump
///   (since 2026-10-07, per user; B jumped too before), d-pad down tapped the character panel with
///   the bags (the bags alone while the panel is pinned), held every carried bag, View tapped the
///   map, held the help, RT draws the weapon and strikes (held charges), L3 draws
///   or puts it away, LT tapped casts what lies in the hollow, held opens the rune panel, LB and RB
///   pick a slot of the action bar, up on the d-pad uses it, down the map, left and right sink and
///   rise while flying, Start the menu, View the help.
///
/// Stage 2, the pointer on the right stick (R3), is UWGamepadPointer, the letter grid for typing
/// UWLetterGrid; stage 3 brings the hints - with the button names
/// chosen in the menu, never detected (per user: a PlayStation controller often reports as an
/// Xbox one through Steam or DS4Windows).
/// </summary>
public static class UWGamepad
{
    /// <summary>Which names the buttons get on screen (UWUserSettings.GamepadButtonStyle).</summary>
    public enum ButtonStyleEnum
    {
        Xbox,
        PlayStation,
        Nintendo,

        /// <summary>The Steam Deck (2026-10-27, ROADMAP "Next"): Xbox's places, the Deck's printed
        /// marks - A B X Y in one colour, L1 R1 L2 R2, L3 R3. Steam Input hands the game an Xbox pad.</summary>
        SteamDeck
    }

    public static ButtonStyleEnum ButtonStyle => (ButtonStyleEnum)Mathf.Clamp(UWUserSettings.GamepadButtonStyle, 0, (int)ButtonStyleEnum.SteamDeck);

    // The buttons' names per style and the controls by place: UWBindingInfo.ButtonName since cleanup
    // step 7.2 (2026-10-25; ButtonName here until then).

    /// <summary>The last action came from a gamepad - the action bar shows its chosen slot only
    /// then. Any mouse or keyboard action turns it off again.</summary>
    public static bool IsActive { get; private set; }

    // AFTER THE SCENE LOAD: the Input System clears its global action callbacks at BeforeSceneLoad
    // in a player (InputActionState.InitializeGlobalActionState), so a hook made earlier was gone
    // and the action bar's ring never showed (per user, 2026-10-07: "LB und RB funktionieren nicht").
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void fInitialise()
    {
        IsActive = false;
        InputSystem.onActionChange -= fOnActionChange;
        InputSystem.onActionChange += fOnActionChange;

        // THE STICKS' OWN DEADZONE OFF: every stick control applies the settings' deadzone (0.125 to
        // 0.925) before any binding sees it, so a smaller one per stick could not be set. The
        // bindings carry their own instead (UWKeyBindings.fApplyStickSwap, per user 2026-10-08). A
        // copy of the settings, so the editor's asset is not touched.
        InputSettings lOSettings = Object.Instantiate(InputSystem.settings);
        lOSettings.defaultDeadzoneMin = 0f;
        lOSettings.defaultDeadzoneMax = 1f;
        InputSystem.settings = lOSettings;
        UWControls.ApplyKeyBindings();
    }

    private static void fOnActionChange(object pOObject, InputActionChange peChange)
    {
        if (peChange != InputActionChange.ActionPerformed || !(pOObject is InputAction lOAction))
            return;

        InputControl lOControl = lOAction.activeControl;

        if (lOControl == null)
            return;

        // A SYSTEM SHORTCUT is not the player taking up the keyboard (per user, 2026-10-08: no
        // screenshot with the pad in use - Win+Shift+S switched to keyboard and mouse).
        if (lOControl.device is Keyboard && IsSystemShortcut)
            return;

        // The pad's own pointer clicks its own mouse (UWPointer.PadButton, cleanup step 7.7B) and moves
        // the system pointer in the menus (UWGamepadPointer): that is not the player taking up the mouse.
        if (UWPointer.IsPadMouse(lOControl.device))
            return;

        if (lOControl.device is Mouse && Time.unscaledTime < msfMouseIgnoredUntil)
            return;

        // NOR WHERE THE PAD PUT THE POINTER, however late Windows reports the warp (per user,
        // 2026-10-08: the bar's pale ring flickered in the game menu) - the real mouse has to move
        // away from there to count.
        if (lOControl.device is Mouse lOMouse && UWPointer.PadPlaced
            && (lOMouse.position.ReadValue() - UWPointer.PadPosition).sqrMagnitude < 9f)
            return;

        IsActive = lOControl.device is Gamepad;
    }

    private static float msfMouseIgnoredUntil;

    /// <summary>The Windows key is held or was just pressed, or Print - a system shortcut such as
    /// Win+Shift+S for a screenshot, whose keys mean nothing to the game here.</summary>
    public static bool IsSystemShortcut
    {
        get
        {
            Keyboard lOKeyboard = Keyboard.current;

            return lOKeyboard != null && (lOKeyboard.leftMetaKey.isPressed || lOKeyboard.rightMetaKey.isPressed
                || lOKeyboard.leftMetaKey.wasReleasedThisFrame || lOKeyboard.rightMetaKey.wasReleasedThisFrame
                || lOKeyboard.printScreenKey.wasPressedThisFrame);
        }
    }

    /// <summary>The gamepad's pointer moved the system pointer just now (a warp in the menus,
    /// UWPointer.MoveTo; its presses are its own mouse's since cleanup step 7.7B).</summary>
    public static void NoteSyntheticMouse()
    {
        msfMouseIgnoredUntil = Time.unscaledTime + 0.3f;
    }

    /// <summary>
    /// A gamepad button pressed this frame - for what waits for "any key" (a window picture, the
    /// next page of a long text, a conversation's text, the intro; per user, 2026-10-07: the pad
    /// did not close them with the pointer locked). With pbSticks a stick pushed out counts too.
    /// Whether one of them owns the press, so that the pad's pointer does not click with it as well,
    /// UWPadOwners decides (cleanup step 7.8; who took it called ConsumePress until then).
    /// </summary>
    public static bool AnyPressed(bool pbSticks)
    {
        Gamepad lOPad = Gamepad.current;

        if (lOPad == null)
            return false;

        if (lOPad.buttonSouth.wasPressedThisFrame || lOPad.buttonEast.wasPressedThisFrame
            || lOPad.buttonWest.wasPressedThisFrame || lOPad.buttonNorth.wasPressedThisFrame
            || lOPad.leftShoulder.wasPressedThisFrame || lOPad.rightShoulder.wasPressedThisFrame
            || lOPad.leftTrigger.wasPressedThisFrame || lOPad.rightTrigger.wasPressedThisFrame
            || lOPad.leftStickButton.wasPressedThisFrame || lOPad.rightStickButton.wasPressedThisFrame
            || lOPad.startButton.wasPressedThisFrame || lOPad.selectButton.wasPressedThisFrame
            || lOPad.dpad.up.wasPressedThisFrame || lOPad.dpad.down.wasPressedThisFrame
            || lOPad.dpad.left.wasPressedThisFrame || lOPad.dpad.right.wasPressedThisFrame)
            return true;

        return pbSticks && (fStickPushed(lOPad.leftStick) || fStickPushed(lOPad.rightStick));
    }

    private static bool fStickPushed(UnityEngine.InputSystem.Controls.StickControl pOStick)
    {
        return pOStick.up.wasPressedThisFrame || pOStick.down.wasPressedThisFrame
            || pOStick.left.wasPressedThisFrame || pOStick.right.wasPressedThisFrame;
    }

    /// <summary>Whether an action's current press comes from a gamepad.</summary>
    public static bool IsFromGamepad(InputAction pOAction)
    {
        return pOAction != null && pOAction.activeControl != null && pOAction.activeControl.device is Gamepad;
    }
}
