using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UWDataImport.UWData;

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
    /// <summary>Which scheme a key belongs to, said after its name: "Turn left (classic)".</summary>
    public enum SchemeEnum
    {
        Both,
        Classic,
        Modern
    }

    /// <summary>One changeable key.</summary>
    public class Entry
    {
        public string Map;
        public string Action;
        public int Binding;

        /// <summary>The name's id in the text table (cleanup step 8.5, 2026-10-24): "keys." for the
        /// keyboard's, "pad.binding." for the gamepad's.</summary>
        public string LabelId;

        public SchemeEnum Scheme;

        /// <summary>Filled into the name where it has a {0} (the action bar's slots).</summary>
        public int Number;

        /// <summary>Filled into the name where it has a {0}, asked anew each time (the easy movement's
        /// modifier names the keys of the steps, which follow the walk keys - decision 18).</summary>
        public System.Func<string> FillWith;

        /// <summary>The name without the scheme: the help's classic key list, which shows only the
        /// classic ones (until step 8.5 it cut " (classic)" off the label).</summary>
        public string PlainLabel => FillWith != null ? UWText.F(LabelId, FillWith())
            : Number > 0 ? UWText.F(LabelId, Number) : UWText.T(LabelId);

        /// <summary>The name as the Controls dialog lists it, with its scheme after it.</summary>
        public string Label => Scheme == SchemeEnum.Both ? PlainLabel
            : UWText.F(Scheme == SchemeEnum.Classic ? "keys.scheme.classic" : "keys.scheme.modern", PlainLabel);

        /// <summary>One of the gamepad's (PadEntries): takes a gamepad button, named by the chosen
        /// style (UWBindingInfo.ButtonName).</summary>
        public bool Gamepad;

        /// <summary>Map, action and binding as it is stored - the first three fields of a line
        /// in the settings.</summary>
        public string Key => Map + "|" + Action + "|" + Binding;
    }

    private static readonly Entry[] mOEntries =
    {
        fEntry("Player", "Move", 1, "keys.walk_forward"),
        fEntry("Player", "Move", 2, "keys.walk_backward"),
        fEntry("Player", "Move", 3, "keys.left"),
        fEntry("Player", "Move", 4, "keys.right"),
        fEntry("Player", "Turn", 1, "keys.turn_left", SchemeEnum.Classic),
        fEntry("Player", "Turn", 2, "keys.turn_right", SchemeEnum.Classic),
        fEntry("Player", "Strafe", 1, "keys.strafe_left", SchemeEnum.Classic),
        fEntry("Player", "Strafe", 2, "keys.strafe_right", SchemeEnum.Classic),
        fEntry("Player", "Jump", 0, "keys.jump"),
        fEntry("Player", "Jump", 1, "keys.jump_second"),
        fEntry("Player", "WalkSlow", 0, "keys.walk_slowly", SchemeEnum.Modern),
        fFilled(fEntry("Player", "EasyModifier", 0, "keys.easy_movement", SchemeEnum.Modern), () => UWBindingInfo.EasyStepKeyNames(true)),
        fEntry("Player", "HoverHeight", 1, "keys.sink", SchemeEnum.Classic),
        fEntry("Player", "HoverHeight", 2, "keys.rise", SchemeEnum.Classic),
        fEntry("Player", "ModernHover", 1, "keys.sink", SchemeEnum.Modern),
        fEntry("Player", "ModernHover", 2, "keys.rise", SchemeEnum.Modern),
        fEntry("Player", "LookDown", 0, "keys.look_down", SchemeEnum.Classic),
        fEntry("Player", "LookReset", 0, "keys.look_straight", SchemeEnum.Classic),
        fEntry("Player", "LookUp", 0, "keys.look_up", SchemeEnum.Classic),
        fEntry("Player", "ToggleInventory", 0, "keys.character_panel", SchemeEnum.Modern),
        fEntry("Player", "ModernBags", 0, "keys.bags", SchemeEnum.Modern),
        fEntry("Player", "ModernLook", 0, "keys.look", SchemeEnum.Modern),
        fEntry("Player", "ModernRunes", 0, "keys.runes", SchemeEnum.Modern),
        fEntry("Player", "ModernUse", 0, "keys.use", SchemeEnum.Modern),
        fEntry("Player", "ModernReady", 0, "keys.ready", SchemeEnum.Modern),
        fEntry("Player", "ModernSlot1", 0, "keys.slot", SchemeEnum.Modern, 1),
        fEntry("Player", "ModernSlot2", 0, "keys.slot", SchemeEnum.Modern, 2),
        fEntry("Player", "ModernSlot3", 0, "keys.slot", SchemeEnum.Modern, 3),
        fEntry("Player", "ModernSlot4", 0, "keys.slot", SchemeEnum.Modern, 4),
        fEntry("Player", "ModernSlot5", 0, "keys.slot", SchemeEnum.Modern, 5),
        fEntry("Player", "ModernSlot6", 0, "keys.slot", SchemeEnum.Modern, 6),
        fEntry("Player", "ModernSlot7", 0, "keys.slot", SchemeEnum.Modern, 7),
        fEntry("Player", "ModernSlot8", 0, "keys.slot", SchemeEnum.Modern, 8),
        fEntry("Player", "ModernSlot9", 0, "keys.slot", SchemeEnum.Modern, 9),
        fEntry("Player", "ModernSlot10", 0, "keys.slot", SchemeEnum.Modern, 10),
        fEntry("Player", "ToggleScheme", 0, "keys.switch_scheme"),
        fEntry("Player", "CursorDrag", 0, "keys.cursor_drag"),
        fEntry("Player", "Interact", 0, "keys.interact"),
        fEntry("Player", "ToggleMap", 0, "keys.map"),
        fEntry("Player", "ToggleFont", 0, "keys.font"),
        fEntry("Player", "ToggleHelp", 0, "keys.help"),
        fEntry("Player", "KeyOptions", 0, "keys.options"),
        fEntry("Player", "KeyTalk", 0, "keys.talk"),
        fEntry("Player", "KeyGet", 0, "keys.get"),
        fEntry("Player", "KeyLook", 0, "keys.look_mode"),
        fEntry("Player", "KeyFight", 0, "keys.fight"),
        fEntry("Player", "KeyUse", 0, "keys.use_mode"),
        fEntry("Player", "KeyPanel", 0, "keys.panel"),
        fEntry("Player", "KeyCast", 0, "keys.cast"),
        fEntry("Player", "KeyTrack", 0, "keys.track"),
        fEntry("Player", "KeyCamp", 0, "keys.camp")
    };

    /// <summary>The gamepad's (UWGamepad): the index is that of the gamepad binding in UWControls -
    /// for ModernHover the parts of its second composite.</summary>
    private static readonly Entry[] mOPadEntries =
    {
        fPadEntry("ModernUse", 1, "pad.binding.use"),
        fPadEntry("ModernLook", 1, "pad.binding.look"),
        fPadEntry("PadBack", 0, "pad.binding.back"),
        fPadEntry("PadJump", 0, "pad.binding.jump"),
        fPadEntry("ToggleInventory", 1, "pad.binding.character_panel"),
        fPadEntry("ModernAttack", 1, "pad.binding.attack"),
        fPadEntry("ModernReady", 1, "pad.binding.ready"),
        fPadEntry("PadCast", 0, "pad.binding.cast"),
        fPadEntry("PadSlotPrevious", 0, "pad.binding.slot_previous"),
        fPadEntry("PadSlotNext", 0, "pad.binding.slot_next"),
        fPadEntry("PadSlotUse", 0, "pad.binding.slot_use"),
        fPadEntry("ToggleMap", 1, "pad.binding.map"),
        fPadEntry("ModernHover", 4, "pad.binding.sink"),
        fPadEntry("ModernHover", 5, "pad.binding.rise"),
        fPadEntry("Menu", 1, "pad.binding.menu"),
        fPadEntry("ToggleHelp", 1, "pad.binding.help"),
        fPadEntry("PadPointer", 0, "pad.binding.pointer"),
        fPadEntry("PadClick", 0, "pad.binding.click"),
        fPadEntry("PadClick", 1, "pad.binding.click_second"),
        fPadEntry("PadContext", 0, "pad.binding.context"),
        fPadEntry("PadContext", 1, "pad.binding.context_second")
    };

    private static Entry fEntry(string psMap, string psAction, int piBinding, string psLabelId,
        SchemeEnum peScheme = SchemeEnum.Both, int piNumber = 0)
    {
        return new Entry { Map = psMap, Action = psAction, Binding = piBinding, LabelId = psLabelId, Scheme = peScheme, Number = piNumber };
    }

    private static Entry fFilled(Entry pOEntry, System.Func<string> pFFillWith)
    {
        pOEntry.FillWith = pFFillWith;

        return pOEntry;
    }

    private static Entry fPadEntry(string psAction, int piBinding, string psLabelId)
    {
        return new Entry { Map = "Player", Action = psAction, Binding = piBinding, LabelId = psLabelId, Gamepad = true };
    }

    /// <summary>The keyboard and mouse entries.</summary>
    public static IReadOnlyList<Entry> Entries => mOEntries;

    /// <summary>The gamepad's entries.</summary>
    public static IReadOnlyList<Entry> PadEntries => mOPadEntries;

    // Which key an entry has in force and what it is called: UWBindingInfo.Path and Name since cleanup
    // step 7.2 (2026-10-25; GetPath and GetKeyName here until then).

    private const string LeftStickPress = "<Gamepad>/leftStickPress";

    private const string RightStickPress = "<Gamepad>/rightStickPress";

    /// <summary>With the sticks swapped, a stick's press goes with its stick - for the buttons the
    /// player has not changed (per user, 2026-10-07: "swap L3 and R3 too unless they were changed").</summary>
    internal static string SwappedPress(string psPath)
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

            if (string.Equals(UWBindingInfo.Path(lOOther), psPath, System.StringComparison.OrdinalIgnoreCase))
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
            if (FindOverride(lOEntry.Key) != null)
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

            string lsOverride = FindOverride(lOEntry.Key);

            // An action has to be disabled while its bindings change.
            bool lbWasEnabled = lOAction.enabled;

            if (lbWasEnabled)
                lOAction.Disable();

            // AN EMPTY PATH IS NOT "DEFAULT" BUT "NO BINDING": applying "" to every unchanged entry
            // switched the whole control off, and "Back to default" left nothing working at all
            // (per user, 2026-09-17). Without an override the override has to be REMOVED.
            // An unchanged gamepad button on a stick's press follows the stick swap (SwappedPress).
            string lsBuiltIn = lOAction.bindings[lOEntry.Binding].path;

            if (lsOverride == null && lOEntry.Gamepad && SwappedPress(lsBuiltIn) != lsBuiltIn)
                lsOverride = SwappedPress(lsBuiltIn);

            if (lsOverride == null)
                lOAction.RemoveBindingOverride(lOEntry.Binding);
            else
                lOAction.ApplyBindingOverride(lOEntry.Binding, lsOverride);

            if (lbWasEnabled)
                lOAction.Enable();
        }

        ApplyEasySteps(pOControls, UWControlScheme.IsModern);
        fApplyStickSwap(pOControls);
    }

    /// <summary>
    /// THE EASY STEPS FOLLOW THE WALK AND TURN KEYS (decision 18, per user 2026-10-25;
    /// UWEasyMovement.FollowedKeys): each step's action (EasyForward, EasyBack, EasyTurnLeft,
    /// EasyTurnRight) gets the key in force of the walk or turn binding it belongs to in the scheme
    /// pbModern, as an override of its one binding - none where that key is its built-in one, so with
    /// the built-in keys nothing changes. Shift (classic) or EasyModifier (modern) is still asked for
    /// in UWPlayerInput. EasyWalkForward (X) keeps its own key.
    ///
    /// The steps have no entries of their own, so a line for them in an old settings.json
    /// ("Player|EasyForward|0|...") was never applied and still is not: it stays in the file as it
    /// is and does nothing. An action's override is changed only when it differs from what it holds -
    /// a change has to switch the action off and on, which drops a press already held. Called with
    /// the bindings (ApplyTo) and when the scheme changes (UWControlScheme.Apply).
    /// </summary>
    public static void ApplyEasySteps(UWControls pOControls, bool pbModern)
    {
        if (pOControls == null)
            return;

        foreach (UWEasyMovement.FollowedKey lOFollowed in UWEasyMovement.FollowedKeys)
        {
            InputAction lOAction = pOControls.FindAction("Player", lOFollowed.EasyAction);

            if (lOAction == null || lOAction.bindings.Count == 0)
                continue;

            string lsPath = UWBindingInfo.EasyStepPath(lOFollowed, pbModern);
            string lsOverride = lsPath != null && lsPath != lOAction.bindings[0].path ? lsPath : null;

            if (lOAction.bindings[0].overridePath == lsOverride)
                continue;

            bool lbWasEnabled = lOAction.enabled;

            if (lbWasEnabled)
                lOAction.Disable();

            if (lsOverride == null)
                lOAction.RemoveBindingOverride(0);
            else
                lOAction.ApplyBindingOverride(0, lsOverride);

            if (lbWasEnabled)
                lOAction.Enable();
        }
    }

    private const string LeftStick = "<Gamepad>/leftStick";

    private const string RightStick = "<Gamepad>/rightStick";

    /// <summary>
    /// The sticks swapped or back: Move's stick binding and LookStick's trade their paths. Every
    /// reader follows the actions - the modern walk and look, the classic turn, strafe and tilt
    /// (UWPlayerInput.fClassicTurn, UWPlayerLook) -, so nothing else needs to know.
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

    /// <summary>The player's own path for a stored key ("Map|Action|Binding"), or null.</summary>
    internal static string FindOverride(string psKey)
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
        fListenFrom(Time.unscaledTime, fHoldKeysUntil(pODone));
    }

    /// <summary>
    /// THE WAIT HOLDS THE KEYS (cleanup 7, wave 2b, per user 2026-10-25: one Escape closes only the
    /// topmost): while the Controls window waits for a key or a button, and in the frame the wait
    /// ended, the press is the wait's - the Escape that cancels it closed the modern game menu with
    /// the window, the key that became the binding fired its old hotkey. The back chain (UWBack, its
    /// Rebind row) and every hotkey site (UWHotkeyHost, the Rebinding gate) ask this. The wait reads
    /// its press in the input update, before any Update of the same frame.
    /// </summary>
    public static bool HoldsKeys => msbListening || msiListenEndedFrame == Time.frameCount;

    private static bool msbListening;

    private static int msiListenEndedFrame = -1;

    /// <summary>Marks the wait begun, and ended when pODone is called.</summary>
    private static System.Action<string> fHoldKeysUntil(System.Action<string> pODone)
    {
        msbListening = true;

        return psPath =>
        {
            msbListening = false;
            msiListenEndedFrame = Time.frameCount;
            pODone(psPath);
        };
    }

    /// <summary>
    /// Waits for the next gamepad button and hands its path over ("&lt;Gamepad&gt;/buttonSouth",
    /// "&lt;Gamepad&gt;/dpad/up"). Escape on the keyboard cancels; the sticks' directions do not
    /// count (the sticks are fixed), nor do keys and mouse buttons.
    /// </summary>
    public static void ListenGamepad(System.Action<string> pODone)
    {
        fListenGamepad(fHoldKeysUntil(pODone));
    }

    private static void fListenGamepad(System.Action<string> pODone)
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

            fListenGamepad(pODone);
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
