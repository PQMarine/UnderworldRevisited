using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// The keys and mouse buttons the player may change (phase 3 of the menu bar, 2026-09-17). A
/// binding takes a keyboard key or a mouse button, either way round (per user): the two world
/// buttons may go onto keys, and a key may go onto a mouse button. Only the pointer movement is
/// fixed. THE GAMEPAD has entries of its own (PadEntries, per user 2026-10-07: "a controller menu
/// with the functions bound by the player"): its binding of the same actions, a gamepad button
/// each. THE STICKS are no buttons and so no entries: they can be SWAPPED as a whole (per user,
/// the same day, for left-handed players - UWUserSettings.GamepadSwapSticks, fApplyStickSwap).
///
/// WHAT A CHANGED MOUSE BUTTON REACHES: everything in the world runs through these actions
/// (UWItemDrag, UWModernBags). The interface panels - inventory slots, options panel, map,
/// conversation - read the physical buttons directly and keep them.
///
/// UWControls builds its action maps in code, so a binding is addressed by map, action and the
/// index of the binding within the action - for a composite that is the index of its part
/// (Up/Down/Left/Right). What the player changed is kept as a list of override paths in
/// UWUserSettings and applied to every live UWControls.
/// </summary>
public static class UWKeyBindings
{
    /// <summary>One changeable key.</summary>
    public class Entry
    {
        public string Map;
        public string Action;
        public int Binding;
        public string Label;

        /// <summary>One of the gamepad's (PadEntries): takes a gamepad button, named by the chosen
        /// style (UWGamepad.ButtonName).</summary>
        public bool Gamepad;

        /// <summary>Map, action and binding as it is stored - the first three fields of a line
        /// in the settings.</summary>
        public string Key => Map + "|" + Action + "|" + Binding;
    }

    private static readonly Entry[] mOEntries =
    {
        fEntry("Player", "Move", 1, "Walk forward"),
        fEntry("Player", "Move", 2, "Walk backward"),
        fEntry("Player", "Move", 3, "Left (modern: strafe)"),
        fEntry("Player", "Move", 4, "Right (modern: strafe)"),
        fEntry("Player", "Turn", 1, "Turn left (classic)"),
        fEntry("Player", "Turn", 2, "Turn right (classic)"),
        fEntry("Player", "Strafe", 1, "Strafe left (classic)"),
        fEntry("Player", "Strafe", 2, "Strafe right (classic)"),
        fEntry("Player", "Jump", 0, "Jump"),
        fEntry("Player", "Jump", 1, "Jump (second key)"),
        fEntry("Player", "HoverHeight", 1, "Sink while flying"),
        fEntry("Player", "HoverHeight", 2, "Rise while flying"),
        fEntry("Player", "LookDown", 0, "Look down (classic)"),
        fEntry("Player", "LookReset", 0, "Look straight (classic)"),
        fEntry("Player", "LookUp", 0, "Look up (classic)"),
        fEntry("Player", "ToggleInventory", 0, "Character panel (modern)"),
        fEntry("Player", "ModernBags", 0, "Bags (modern)"),
        fEntry("Player", "ModernLook", 0, "Look (modern)"),
        fEntry("Player", "ModernRunes", 0, "Runes and spells (modern)"),
        fEntry("Player", "ToggleScheme", 0, "Switch control scheme (with Shift)"),
        fEntry("Player", "CursorDrag", 0, "Walk, drag, attack"),
        fEntry("Player", "Interact", 0, "Look and use"),
        fEntry("Player", "ToggleMap", 0, "Open the map"),
        fEntry("Player", "ToggleFont", 0, "Switch the font"),
        fEntry("Player", "ToggleHelp", 0, "Open the help (not with Alt)"),
        fEntry("Player", "KeyOptions", 0, "Options"),
        fEntry("Player", "KeyTalk", 0, "Talk mode"),
        fEntry("Player", "KeyGet", 0, "Get mode"),
        fEntry("Player", "KeyLook", 0, "Look mode"),
        fEntry("Player", "KeyFight", 0, "Fight (draw the weapon)"),
        fEntry("Player", "KeyUse", 0, "Use mode"),
        fEntry("Player", "KeyPanel", 0, "Turn the panel"),
        fEntry("Player", "KeyCast", 0, "Cast the runes on the shelf"),
        fEntry("Player", "KeyTrack", 0, "Track (look for creatures nearby)"),
        fEntry("Player", "KeyCamp", 0, "Make camp")
    };

    /// <summary>The gamepad's (UWGamepad): the index is that of the gamepad binding in UWControls -
    /// for ModernHover the parts of its second composite.</summary>
    private static readonly Entry[] mOPadEntries =
    {
        fPadEntry("ModernUse", 1, "Use (held: use directly)"),
        fPadEntry("ModernLook", 1, "Look"),
        fPadEntry("PadBack", 0, "Close what is open"),
        fPadEntry("PadJump", 0, "Jump"),
        fPadEntry("ToggleInventory", 1, "Character panel (held: every bag)"),
        fPadEntry("ModernAttack", 1, "Draw the weapon and strike"),
        fPadEntry("ModernReady", 1, "Draw or put away the weapon"),
        fPadEntry("PadCast", 0, "Cast the runes (held: rune panel)"),
        fPadEntry("PadSlotPrevious", 0, "Action bar: previous slot"),
        fPadEntry("PadSlotNext", 0, "Action bar: next slot"),
        fPadEntry("PadSlotUse", 0, "Action bar: use the slot"),
        fPadEntry("ToggleMap", 1, "Open the map (tapped)"),
        fPadEntry("ModernHover", 4, "Sink while flying"),
        fPadEntry("ModernHover", 5, "Rise while flying"),
        fPadEntry("Menu", 1, "Menu"),
        fPadEntry("ToggleHelp", 1, "Help (held)"),
        fPadEntry("PadPointer", 0, "Pointer on the stick, on or off"),
        fPadEntry("PadClick", 0, "Pointer: click (held: drag)"),
        fPadEntry("PadClick", 1, "Pointer: click, second button"),
        fPadEntry("PadContext", 0, "Pointer: right button"),
        fPadEntry("PadContext", 1, "Pointer: right button, second button")
    };

    private static Entry fEntry(string psMap, string psAction, int piBinding, string psLabel)
    {
        return new Entry { Map = psMap, Action = psAction, Binding = piBinding, Label = psLabel };
    }

    private static Entry fPadEntry(string psAction, int piBinding, string psLabel)
    {
        return new Entry { Map = "Player", Action = psAction, Binding = piBinding, Label = psLabel, Gamepad = true };
    }

    /// <summary>The keyboard and mouse entries.</summary>
    public static IReadOnlyList<Entry> Entries => mOEntries;

    /// <summary>The gamepad's entries.</summary>
    public static IReadOnlyList<Entry> PadEntries => mOPadEntries;

    /// <summary>What the key of an entry is called on screen, e.g. "W" or "Page Up".</summary>
    public static string GetKeyName(Entry pOEntry)
    {
        string lsPath = GetPath(pOEntry);

        if (pOEntry.Gamepad)
            return UWGamepad.ButtonName(lsPath);

        if (string.IsNullOrEmpty(lsPath))
            return "-";

        string lsName = InputControlPath.ToHumanReadableString(lsPath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);

        if (string.IsNullOrEmpty(lsName))
            return lsPath;

        // A key bound by the character it types ("#(f)", the built-in letters, so they follow the
        // keyboard layout) reads 'F' with quotes - a key the player bound reads F (per user,
        // 2026-10-04: the quotes looked odd in the Controls tab).
        if (lsName.Length > 2 && lsName[0] == '\'' && lsName[lsName.Length - 1] == '\'')
            lsName = lsName.Substring(1, lsName.Length - 2).ToUpperInvariant();

        return lsName;
    }

    /// <summary>The path in force: the player's own if there is one, otherwise the built-in one
    /// from UWControls.</summary>
    public static string GetPath(Entry pOEntry)
    {
        string lsOverride = fFindOverride(pOEntry.Key);

        if (lsOverride != null)
            return lsOverride;

        InputAction lOAction = fFindAction(UWControls.AnyInstance, pOEntry);

        if (lOAction == null || pOEntry.Binding >= lOAction.bindings.Count)
            return null;

        string lsBuiltIn = lOAction.bindings[pOEntry.Binding].path;

        return pOEntry.Gamepad ? fSwappedPress(lsBuiltIn) : lsBuiltIn;
    }

    private const string LeftStickPress = "<Gamepad>/leftStickPress";

    private const string RightStickPress = "<Gamepad>/rightStickPress";

    /// <summary>With the sticks swapped, a stick's press goes with its stick - for the buttons the
    /// player has not changed (per user, 2026-10-07: "swap L3 and R3 too unless they were changed").</summary>
    private static string fSwappedPress(string psPath)
    {
        if (!UWUserSettings.GamepadSwapSticks)
            return psPath;

        if (psPath == LeftStickPress)
            return RightStickPress;

        if (psPath == RightStickPress)
            return LeftStickPress;

        return psPath;
    }

    /// <summary>Which other entry already uses this key, or null.</summary>
    public static Entry FindConflict(Entry pOEntry, string psPath)
    {
        foreach (Entry lOOther in pOEntry.Gamepad ? mOPadEntries : mOEntries)
        {
            if (lOOther == pOEntry)
                continue;

            if (string.Equals(GetPath(lOOther), psPath, System.StringComparison.OrdinalIgnoreCase))
                return lOOther;
        }

        return null;
    }

    public static void Set(Entry pOEntry, string psPath)
    {
        List<string> lsLines = UWUserSettings.KeyBindings;
        string lsPrefix = pOEntry.Key + "|";

        lsLines.RemoveAll(lsLine => lsLine != null && lsLine.StartsWith(lsPrefix));
        lsLines.Add(lsPrefix + psPath);

        UWUserSettings.Save();

        UWControls.ApplyKeyBindings();
        UWMouseButtons.Forget();
    }

    /// <summary>The keyboard and mouse back to the built-in keys - the gamepad's stay.</summary>
    public static void ResetAll()
    {
        fReset(mOEntries);
    }

    /// <summary>The gamepad back to the built-in buttons - the keys stay.</summary>
    public static void ResetGamepad()
    {
        fReset(mOPadEntries);
    }

    private static void fReset(Entry[] pOEntries)
    {
        foreach (Entry lOEntry in pOEntries)
        {
            string lsPrefix = lOEntry.Key + "|";

            UWUserSettings.KeyBindings.RemoveAll(lsLine => lsLine != null && lsLine.StartsWith(lsPrefix));
        }

        UWUserSettings.Save();

        UWControls.ApplyKeyBindings();
        UWMouseButtons.Forget();
    }

    public static bool HasChanges => fHasChanges(mOEntries);

    public static bool HasGamepadChanges => fHasChanges(mOPadEntries);

    private static bool fHasChanges(Entry[] pOEntries)
    {
        foreach (Entry lOEntry in pOEntries)
        {
            if (fFindOverride(lOEntry.Key) != null)
                return true;
        }

        return false;
    }

    /// <summary>Puts the player's keys onto the maps of one UWControls. Called when it is built
    /// and after every change.</summary>
    public static void ApplyTo(UWControls pOControls)
    {
        if (pOControls == null)
            return;

        foreach (Entry lOEntry in fAllEntries())
        {
            InputAction lOAction = fFindAction(pOControls, lOEntry);

            if (lOAction == null || lOEntry.Binding >= lOAction.bindings.Count)
                continue;

            string lsOverride = fFindOverride(lOEntry.Key);

            // An action has to be disabled while its bindings change.
            bool lbWasEnabled = lOAction.enabled;

            if (lbWasEnabled)
                lOAction.Disable();

            // AN EMPTY PATH IS NOT "DEFAULT" BUT "NO BINDING": applying "" to every unchanged entry
            // switched the whole control off, and "Back to default" left nothing working at all
            // (per user, 2026-09-17). Without an override the override has to be REMOVED.
            // An unchanged gamepad button on a stick's press follows the stick swap (fSwappedPress).
            string lsBuiltIn = lOAction.bindings[lOEntry.Binding].path;

            if (lsOverride == null && lOEntry.Gamepad && fSwappedPress(lsBuiltIn) != lsBuiltIn)
                lsOverride = fSwappedPress(lsBuiltIn);

            if (lsOverride == null)
                lOAction.RemoveBindingOverride(lOEntry.Binding);
            else
                lOAction.ApplyBindingOverride(lOEntry.Binding, lsOverride);

            if (lbWasEnabled)
                lOAction.Enable();
        }

        fApplyStickSwap(pOControls);
    }

    private const string LeftStick = "<Gamepad>/leftStick";

    private const string RightStick = "<Gamepad>/rightStick";

    /// <summary>
    /// The sticks swapped or back: Move's stick binding and LookStick's trade their paths. Every
    /// reader follows the actions - the modern walk and look, the classic turn, strafe and tilt
    /// (UWPlayerMovement.fClassicTurn, UWPlayerLook) -, so nothing else needs to know.
    /// </summary>
    private static void fApplyStickSwap(UWControls pOControls)
    {
        bool lbSwap = UWUserSettings.GamepadSwapSticks;

        // THE DEADZONE GOES WITH THE PHYSICAL STICK, swapped or not - the drift sits in the stick
        // (per user, 2026-10-08). The sticks' own deadzone is off for it (UWGamepad).
        fOverrideStick(pOControls.FindAction("Player", "Move"), LeftStick, lbSwap ? RightStick : null,
            lbSwap ? UWUserSettings.RightStickDeadzone : UWUserSettings.LeftStickDeadzone);
        fOverrideStick(pOControls.FindAction("Player", "LookStick"), RightStick, lbSwap ? LeftStick : null,
            lbSwap ? UWUserSettings.LeftStickDeadzone : UWUserSettings.RightStickDeadzone);
    }

    /// <summary>The upper end of the sticks' deflection as the Input System has it.</summary>
    private const float StickDeadzoneMax = 0.925f;

    /// <summary>The binding with the built-in path gets the other stick (or keeps its own) and the
    /// stick's deadzone. Written in the invariant culture - a German one would write a comma.</summary>
    private static void fOverrideStick(InputAction pOAction, string psBuiltIn, string psOverride, float pfDeadzone)
    {
        string lsProcessors = "stickDeadzone(min="
            + Mathf.Max(0.0001f, pfDeadzone).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
            + ",max=" + StickDeadzoneMax.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")";

        if (pOAction == null)
            return;

        for (int liAt = 0; liAt < pOAction.bindings.Count; liAt++)
        {
            if (pOAction.bindings[liAt].path != psBuiltIn)
                continue;

            bool lbWasEnabled = pOAction.enabled;

            if (lbWasEnabled)
                pOAction.Disable();

            pOAction.ApplyBindingOverride(liAt, new InputBinding { overridePath = psOverride, overrideProcessors = lsProcessors });

            if (lbWasEnabled)
                pOAction.Enable();

            return;
        }
    }

    private static IEnumerable<Entry> fAllEntries()
    {
        foreach (Entry lOEntry in mOEntries)
            yield return lOEntry;

        foreach (Entry lOEntry in mOPadEntries)
            yield return lOEntry;
    }

    private static InputAction fFindAction(UWControls pOControls, Entry pOEntry)
    {
        return pOControls == null ? null : pOControls.FindAction(pOEntry.Map, pOEntry.Action);
    }

    private static string fFindOverride(string psKey)
    {
        string lsPrefix = psKey + "|";

        foreach (string lsLine in UWUserSettings.KeyBindings)
        {
            if (lsLine != null && lsLine.StartsWith(lsPrefix))
                return lsLine.Substring(lsPrefix.Length);
        }

        return null;
    }

    /// <summary>
    /// Waits for the next key or mouse button and hands its path over. Gamepads are ignored,
    /// Escape cancels, and so is the pointer itself - only buttons arrive here anyway.
    ///
    /// A mouse button in the first quarter second does not count: that is still the click that
    /// started the listening.
    /// </summary>
    public static void Listen(System.Action<string> pODone)
    {
        fListenFrom(Time.unscaledTime, pODone);
    }

    /// <summary>
    /// Waits for the next gamepad button and hands its path over ("&lt;Gamepad&gt;/buttonSouth",
    /// "&lt;Gamepad&gt;/dpad/up"). Escape on the keyboard cancels; the sticks' directions do not
    /// count (the sticks are fixed), nor do keys and mouse buttons.
    /// </summary>
    public static void ListenGamepad(System.Action<string> pODone)
    {
        InputSystem.onAnyButtonPress.CallOnce(pOControl =>
        {
            if (pOControl.device is Keyboard && pOControl.name == "escape")
            {
                pODone(null);
                return;
            }

            if (pOControl.device is Gamepad && !(pOControl.parent is UnityEngine.InputSystem.Controls.StickControl))
            {
                pODone("<Gamepad>" + pOControl.path.Substring(pOControl.device.path.Length));
                return;
            }

            ListenGamepad(pODone);
        });
    }

    private static void fListenFrom(float pfSince, System.Action<string> pODone)
    {
        InputSystem.onAnyButtonPress.CallOnce(pOControl =>
        {
            if (pOControl.device is Keyboard)
            {
                if (pOControl.name == "escape")
                    pODone(null);
                else
                    pODone("<Keyboard>/" + pOControl.name);

                return;
            }

            if (pOControl.device is Mouse && Time.unscaledTime - pfSince > 0.25f)
            {
                pODone("<Mouse>/" + pOControl.name);

                return;
            }

            fListenFrom(pfSince, pODone);
        });
    }
}
