using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE GAMEPAD (per user, 2026-10-07; tested with an Xbox controller). Stage 1 plays in the world
/// in the modern scheme, the bindings in UWControls (Xbox names):
///
///   left stick walk and strafe (analog), right stick look, A use (held: use directly), X look,
///   B close what is open (bags, panels, the menu, a waiting spell, UWGameUI.PadBackCloses), Y jump
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
        Nintendo
    }

    public static ButtonStyleEnum ButtonStyle => (ButtonStyleEnum)Mathf.Clamp(UWUserSettings.GamepadButtonStyle, 0, 2);

    /// <summary>The controls by their place on the pad (the Input System's names), and their names
    /// per style - as TEXT, not pictures (the PlayStation symbols are Sony's mark). Nintendo's face
    /// buttons are swapped against the place: its south button is B.</summary>
    private static readonly string[] msControls =
    {
        "buttonSouth", "buttonEast", "buttonWest", "buttonNorth", "leftShoulder", "rightShoulder",
        "leftTrigger", "rightTrigger", "leftStickPress", "rightStickPress", "start", "select",
        "dpad/up", "dpad/down", "dpad/left", "dpad/right"
    };

    private static readonly string[][] msNames =
    {
        new[] { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "LS", "RS", "Menu", "View",
            "D-pad up", "D-pad down", "D-pad left", "D-pad right" },
        new[] { "Cross", "Circle", "Square", "Triangle", "L1", "R1", "L2", "R2", "L3", "R3", "Options", "Share",
            "D-pad up", "D-pad down", "D-pad left", "D-pad right" },
        new[] { "B", "A", "Y", "X", "L", "R", "ZL", "ZR", "L stick", "R stick", "+", "-",
            "D-pad up", "D-pad down", "D-pad left", "D-pad right" }
    };

    /// <summary>A binding path's button name in the chosen style ("&lt;Gamepad&gt;/buttonSouth" is A,
    /// Cross or B).</summary>
    public static string ButtonName(string psPath)
    {
        if (string.IsNullOrEmpty(psPath))
            return "-";

        const string Prefix = "<Gamepad>/";
        string lsControl = psPath.StartsWith(Prefix) ? psPath.Substring(Prefix.Length) : psPath;
        int liAt = System.Array.IndexOf(msControls, lsControl);

        if (liAt >= 0)
            return msNames[(int)ButtonStyle][liAt];

        string lsName = InputControlPath.ToHumanReadableString(psPath, InputControlPath.HumanReadableStringOptions.OmitDevice);

        return string.IsNullOrEmpty(lsName) ? psPath : lsName;
    }

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

        // The pad's own pointer moves and clicks the real mouse (UWGamepadPointer): that is not
        // the player taking up the mouse.
        if (lOControl.device is Mouse && Time.unscaledTime < msfMouseIgnoredUntil)
            return;

        // NOR WHERE THE PAD PUT THE POINTER, however late Windows reports the warp (per user,
        // 2026-10-08: the bar's pale ring flickered in the game menu) - the real mouse has to move
        // away from there to count.
        if (lOControl.device is Mouse lOMouse && UWGamepadPointer.HasPlaced
            && (lOMouse.position.ReadValue() - UWGamepadPointer.Position).sqrMagnitude < 9f)
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

    /// <summary>The gamepad's pointer moved or pressed the real mouse just now.</summary>
    public static void NoteSyntheticMouse()
    {
        msfMouseIgnoredUntil = Time.unscaledTime + 0.3f;
    }

    private static int msiConsumedFrame = -1;

    /// <summary>
    /// A gamepad button pressed this frame - for what waits for "any key" (a window picture, the
    /// next page of a long text, a conversation's text, the intro; per user, 2026-10-07: the pad
    /// did not close them with the pointer locked). With pbSticks a stick pushed out counts too.
    /// Who takes it calls ConsumePress, so the pad's pointer does not click with it as well.
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

    /// <summary>This frame's pad press is used up (UWGamepadPointer sends no click for it).</summary>
    public static void ConsumePress()
    {
        msiConsumedFrame = Time.frameCount;
    }

    public static bool IsPressConsumed => msiConsumedFrame == Time.frameCount;

    /// <summary>Whether an action's current press comes from a gamepad.</summary>
    public static bool IsFromGamepad(InputAction pOAction)
    {
        return pOAction != null && pOAction.activeControl != null && pOAction.activeControl.device is Gamepad;
    }
}
