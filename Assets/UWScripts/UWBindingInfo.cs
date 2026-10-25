using System.Collections.Generic;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// WHICH KEY OR BUTTON AN ACTION HAS, AND WHAT IT IS CALLED (cleanup step 7.2, 2026-10-25): the one
/// place that answers it - for the Controls window and its gamepad tab (UWSetupMenu), the help's
/// Controls tab (UWHelpControls), the pad's hint strip, the crosshair's and the action bar's pad hints
/// (through UWGlyphs.ForEntry), the letter grid's hint line, the world's mouse buttons (UWMouseButtons)
/// and the easy steps (UWKeyBindings.ApplyTo, decision 18). Until then five loops looked an entry up
/// by its action and binding (UWHelpControls three times, UWGlyphs, UWMouseButtons), UWKeyBindings
/// named keys and UWGamepad buttons, and UWGamepad and UWGlyphs kept two copies of the gamepad's
/// controls by place for their names and their pictures.
///
/// What a key is CALLED is unchanged: a keyboard key or mouse button by the Input System's readable
/// path without the device, a key bound by its character ("#(f)") without the quotes; a gamepad
/// control by the chosen style's mark or name (UWGamepad.ButtonStyle), anything else by its readable
/// path. Which binding is IN FORCE is unchanged as well: the player's override from the settings
/// (UWKeyBindings), else the built-in one of UWControls, a gamepad stick's press following the stick
/// swap. The table of the changeable bindings, their storing and their applying stay UWKeyBindings'.
/// </summary>
public static class UWBindingInfo
{
    /// <summary>A gamepad path's prefix, as UWKeyBindings stores it.</summary>
    public const string PadPrefix = "<Gamepad>/";

    /// <summary>The gamepad's controls by their place on the pad (the Input System's names): the one
    /// list that the names below and the pictures of UWGlyphs are in the order of.</summary>
    private static readonly string[] msPadControls =
    {
        "buttonSouth", "buttonEast", "buttonWest", "buttonNorth", "leftShoulder", "rightShoulder",
        "leftTrigger", "rightTrigger", "leftStickPress", "rightStickPress", "start", "select",
        "dpad/up", "dpad/down", "dpad/left", "dpad/right"
    };

    /// <summary>The names per style (UWGamepad.ButtonStyleEnum) in the order of the controls, as TEXT,
    /// not pictures (the PlayStation symbols are Sony's mark): the printed marks (A, LB, L1, ZL, +) as
    /// they are, a name in words as its id in the text table (PadNameIdPrefix, cleanup step 8.5,
    /// 2026-10-24). Nintendo's face buttons are swapped against the place: its south button is B.</summary>
    private static readonly string[][] msPadNames =
    {
        new[] { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "LS", "RS", "pad.button.menu", "pad.button.view",
            "pad.button.dpad_up", "pad.button.dpad_down", "pad.button.dpad_left", "pad.button.dpad_right" },
        new[] { "pad.button.cross", "pad.button.circle", "pad.button.square", "pad.button.triangle", "L1", "R1", "L2", "R2", "L3", "R3",
            "pad.button.options", "pad.button.share",
            "pad.button.dpad_up", "pad.button.dpad_down", "pad.button.dpad_left", "pad.button.dpad_right" },
        new[] { "B", "A", "Y", "X", "L", "R", "ZL", "ZR", "pad.button.left_stick", "pad.button.right_stick", "+", "-",
            "pad.button.dpad_up", "pad.button.dpad_down", "pad.button.dpad_left", "pad.button.dpad_right" }
    };

    private const string PadNameIdPrefix = "pad.button.";

    /// <summary>The place of a gamepad path's control in the list of controls by place
    /// ("&lt;Gamepad&gt;/buttonSouth" is 0), -1 for none of them.</summary>
    public static int PadControlIndex(string psPath)
    {
        if (string.IsNullOrEmpty(psPath))
            return -1;

        string lsControl = psPath.StartsWith(PadPrefix) ? psPath.Substring(PadPrefix.Length) : psPath;

        return System.Array.IndexOf(msPadControls, lsControl);
    }

    /// <summary>The number of controls by place (UWGlyphs' tables have one picture for each).</summary>
    public static int PadControlCount => msPadControls.Length;

    // ------------------------------------------------- The entries

    /// <summary>The changeable keyboard and mouse binding of an action (UWKeyBindings.Entries), or null.</summary>
    public static UWKeyBindings.Entry Find(string psAction, int piBinding)
    {
        return fFind(UWKeyBindings.Entries, psAction, piBinding);
    }

    /// <summary>The changeable gamepad binding of an action (UWKeyBindings.PadEntries), or null.</summary>
    public static UWKeyBindings.Entry FindPad(string psAction, int piBinding)
    {
        return fFind(UWKeyBindings.PadEntries, psAction, piBinding);
    }

    private static UWKeyBindings.Entry fFind(IReadOnlyList<UWKeyBindings.Entry> pOEntries, string psAction, int piBinding)
    {
        foreach (UWKeyBindings.Entry lOEntry in pOEntries)
        {
            if (lOEntry.Action == psAction && lOEntry.Binding == piBinding)
                return lOEntry;
        }

        return null;
    }

    // ------------------------------------------------- Paths

    /// <summary>The path in force: the player's own if there is one, otherwise the built-in one from
    /// UWControls (a gamepad stick's press following the stick swap); null where there is neither.</summary>
    public static string Path(UWKeyBindings.Entry pOEntry)
    {
        if (pOEntry == null)
            return null;

        string lsOverride = UWKeyBindings.FindOverride(pOEntry.Key);

        if (lsOverride != null)
            return lsOverride;

        UWControls lOControls = UWControls.AnyInstance;
        InputAction lOAction = lOControls == null ? null : lOControls.FindAction(pOEntry.Map, pOEntry.Action);

        if (lOAction == null || pOEntry.Binding >= lOAction.bindings.Count)
            return null;

        string lsBuiltIn = lOAction.bindings[pOEntry.Binding].path;

        return pOEntry.Gamepad ? UWKeyBindings.SwappedPress(lsBuiltIn) : lsBuiltIn;
    }

    /// <summary>The path in force of a gamepad entry, or null.</summary>
    public static string PadPath(string psAction, int piBinding)
    {
        UWKeyBindings.Entry lOEntry = FindPad(psAction, piBinding);

        return lOEntry != null ? Path(lOEntry) : null;
    }

    /// <summary>The path in force of the Player map's first changeable keyboard and mouse entry of an
    /// action - psDefault without one, or where it has no path (the world's mouse buttons).</summary>
    public static string PathOr(string psAction, string psDefault)
    {
        foreach (UWKeyBindings.Entry lOEntry in UWKeyBindings.Entries)
        {
            if (lOEntry.Action != psAction || lOEntry.Map != "Player")
                continue;

            string lsPath = Path(lOEntry);

            return string.IsNullOrEmpty(lsPath) ? psDefault : lsPath;
        }

        return psDefault;
    }

    // ------------------------------------------------- The easy steps (decision 18)

    /// <summary>The walk or turn entry an easy step takes its key from in the scheme pbModern
    /// (UWEasyMovement.FollowedKeys), or null.</summary>
    public static UWKeyBindings.Entry EasyStepSource(UWEasyMovement.FollowedKey pOFollowed, bool pbModern)
    {
        if (pOFollowed == null)
            return null;

        return pbModern ? Find(pOFollowed.ModernAction, pOFollowed.ModernBinding) : Find(pOFollowed.ClassicAction, pOFollowed.ClassicBinding);
    }

    /// <summary>The key in force an easy step lies on in the scheme pbModern: its walk or turn key's.</summary>
    public static string EasyStepPath(UWEasyMovement.FollowedKey pOFollowed, bool pbModern)
    {
        UWKeyBindings.Entry lOSource = EasyStepSource(pOFollowed, pbModern);

        return lOSource != null ? Path(lOSource) : null;
    }

    /// <summary>The easy steps' keys as the help and the Controls window name them, forward, turn
    /// left, back and turn right - "W A S D" with the built-in keys; X, the step with a key of its
    /// own, the callers add.</summary>
    public static string EasyStepKeyNames(bool pbModern)
    {
        return fEasyStepName("EasyForward", pbModern, "W") + " " + fEasyStepName("EasyTurnLeft", pbModern, "A")
            + " " + fEasyStepName("EasyBack", pbModern, "S") + " " + fEasyStepName("EasyTurnRight", pbModern, "D");
    }

    private static string fEasyStepName(string psEasyAction, bool pbModern, string psFallback)
    {
        foreach (UWEasyMovement.FollowedKey lOFollowed in UWEasyMovement.FollowedKeys)
        {
            if (lOFollowed.EasyAction != psEasyAction)
                continue;

            UWKeyBindings.Entry lOSource = EasyStepSource(lOFollowed, pbModern);

            return lOSource != null ? KeyName(lOSource.Action, lOSource.Binding, psFallback) : psFallback;
        }

        return psFallback;
    }

    // ------------------------------------------------- Names

    /// <summary>What the key of an entry is called on screen, e.g. "W", "Page Up" or "A" (the pad's
    /// south button in the Xbox style); "-" for a keyboard entry without a path.</summary>
    public static string Name(UWKeyBindings.Entry pOEntry)
    {
        string lsPath = Path(pOEntry);

        if (pOEntry != null && pOEntry.Gamepad)
            return ButtonName(lsPath);

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

    /// <summary>The name of an action's changeable keyboard or mouse binding as it is now, or
    /// psFallback - without such an entry, or where it has no key.</summary>
    public static string KeyName(string psAction, int piBinding, string psFallback)
    {
        UWKeyBindings.Entry lOEntry = Find(psAction, piBinding);

        if (lOEntry == null)
            return psFallback;

        string lsName = Name(lOEntry);

        return string.IsNullOrEmpty(lsName) || lsName == "-" ? psFallback : lsName;
    }

    /// <summary>The name of a gamepad entry's button as it is bound now, "-" without the entry.</summary>
    public static string PadName(string psAction, int piBinding)
    {
        UWKeyBindings.Entry lOEntry = FindPad(psAction, piBinding);

        return lOEntry != null ? Name(lOEntry) : "-";
    }

    /// <summary>A binding path's button name in the chosen style ("&lt;Gamepad&gt;/buttonSouth" is A,
    /// Cross or B); a control not on the list by its readable path, "-" for no path.</summary>
    public static string ButtonName(string psPath)
    {
        if (string.IsNullOrEmpty(psPath))
            return "-";

        int liAt = PadControlIndex(psPath);

        if (liAt >= 0)
        {
            string lsMark = msPadNames[(int)UWGamepad.ButtonStyle][liAt];

            return lsMark.StartsWith(PadNameIdPrefix) ? UWText.T(lsMark) : lsMark;
        }

        string lsName = InputControlPath.ToHumanReadableString(psPath, InputControlPath.HumanReadableStringOptions.OmitDevice);

        return string.IsNullOrEmpty(lsName) ? psPath : lsName;
    }
}
