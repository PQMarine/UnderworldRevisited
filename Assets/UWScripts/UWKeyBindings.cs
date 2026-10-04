using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// The keys and mouse buttons the player may change (phase 3 of the menu bar, 2026-09-17). A
/// binding takes a keyboard key or a mouse button, either way round (per user): the two world
/// buttons may go onto keys, and a key may go onto a mouse button. Only the pointer movement is
/// fixed. A gamepad comes later.
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

    private static Entry fEntry(string psMap, string psAction, int piBinding, string psLabel)
    {
        return new Entry { Map = psMap, Action = psAction, Binding = piBinding, Label = psLabel };
    }

    public static IReadOnlyList<Entry> Entries => mOEntries;

    /// <summary>What the key of an entry is called on screen, e.g. "W" or "Page Up".</summary>
    public static string GetKeyName(Entry pOEntry)
    {
        string lsPath = GetPath(pOEntry);

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

        return lOAction != null && pOEntry.Binding < lOAction.bindings.Count
            ? lOAction.bindings[pOEntry.Binding].path : null;
    }

    /// <summary>Which other entry already uses this key, or null.</summary>
    public static Entry FindConflict(Entry pOEntry, string psPath)
    {
        foreach (Entry lOOther in mOEntries)
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

    public static void ResetAll()
    {
        UWUserSettings.KeyBindings.Clear();
        UWUserSettings.Save();

        UWControls.ApplyKeyBindings();
        UWMouseButtons.Forget();
    }

    public static bool HasChanges => UWUserSettings.KeyBindings.Count > 0;

    /// <summary>Puts the player's keys onto the maps of one UWControls. Called when it is built
    /// and after every change.</summary>
    public static void ApplyTo(UWControls pOControls)
    {
        if (pOControls == null)
            return;

        foreach (Entry lOEntry in mOEntries)
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
            if (lsOverride == null)
                lOAction.RemoveBindingOverride(lOEntry.Binding);
            else
                lOAction.ApplyBindingOverride(lOEntry.Binding, lsOverride);

            if (lbWasEnabled)
                lOAction.Enable();
        }
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
