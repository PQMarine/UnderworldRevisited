using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE HELP'S CONTROLS TAB (per user, 2026-10-04: "with all the functions of the modern scheme
/// we need a guide of our own"): what the keys and the mouse do, for the scheme in force.
///
///   - THE MODERN SCHEME in sections - the pointer, moving, acting, combat, magic, the bags and
///     the character, the conversation, the map and the rest - each a list of keys and what they
///     do. Written from the code as it stands; change it with the scheme.
///   - THE CLASSIC SCHEME as the original's mouse in a few lines and the changeable keys with
///     their labels (UWKeyBindings).
///
/// The names of changeable keys come from the bindings in force (UWBindingInfo, cleanup step 7.2,
/// 2026-10-25; three lookups of their own here until then), so a key the player moved shows where it
/// is now; the fixed ones are written out.
///
/// THE TEXTS come from the text table (UWText, "help.controls.", cleanup step 8.5, 2026-10-24);
/// single key caps ("W", "F9", "1 ... 0") stay as they are.
/// </summary>
public static class UWHelpControls
{
    /// <summary>One line: the keys, and what they do.</summary>
    public struct Row
    {
        public string Keys;

        public string Text;

        /// <summary>Gamepad glyphs shown in the keys' column instead of Keys (UWGlyphs); Keys is
        /// what stands there if they are missing.</summary>
        public Texture2D[] Glyphs;
    }

    /// <summary>A heading with its lines; Intro is a dim line under the heading, or null.</summary>
    public class Section
    {
        public string Title;

        public string Intro;

        public readonly List<Row> Rows = new List<Row>();

        public Section Add(string psKeys, string psText)
        {
            Rows.Add(new Row { Keys = psKeys, Text = psText });
            return this;
        }

        /// <summary>A row of gamepad glyphs; a missing one leaves the row to its text name.</summary>
        public Section AddPad(string psFallback, string psText, params Texture2D[] pyGlyphs)
        {
            bool lbAll = pyGlyphs != null && pyGlyphs.Length > 0 && System.Array.TrueForAll(pyGlyphs, lOGlyph => lOGlyph != null);

            Rows.Add(new Row { Keys = psFallback, Text = psText, Glyphs = lbAll ? pyGlyphs : null });
            return this;
        }
    }

    /// <summary>The sections for the scheme in force - the gamepad's with them, first when the pad
    /// was used last (UWGamepad.IsActive), last otherwise; only with a pad connected.</summary>
    public static List<Section> Build(bool pbModern)
    {
        List<Section> lOSections = pbModern ? fModern() : fClassic();

        if (Gamepad.current == null)
            return lOSections;

        List<Section> lOPad = pbModern ? fModernPad() : fClassicPad();

        if (UWGamepad.IsActive)
        {
            lOPad.AddRange(lOSections);
            return lOPad;
        }

        lOSections.AddRange(lOPad);
        return lOSections;
    }

    /// <summary>The glyph of a gamepad entry's binding as it is now (UWGlyphs.ForEntry).</summary>
    private static Texture2D fPad(string psAction, int piBinding)
    {
        return UWGlyphs.ForEntry(psAction, piBinding);
    }

    /// <summary>The name of a gamepad entry's button, for a row whose glyph is missing.</summary>
    private static string fPadName(string psAction, int piBinding)
    {
        return UWBindingInfo.PadName(psAction, piBinding);
    }

    private static string fWalkStickName => UWUserSettings.GamepadSwapSticks ? UWText.T("help.controls.key.right_stick") : UWText.T("help.controls.key.left_stick");

    private static string fLookStickName => UWUserSettings.GamepadSwapSticks ? UWText.T("help.controls.key.left_stick") : UWText.T("help.controls.key.right_stick");

    private static Texture2D fWalkStick => UWGlyphs.Tiles(UWUserSettings.GamepadSwapSticks ? UWGlyphs.RightStickTile : UWGlyphs.LeftStickTile);

    private static Texture2D fLookStick => UWGlyphs.Tiles(UWUserSettings.GamepadSwapSticks ? UWGlyphs.LeftStickTile : UWGlyphs.RightStickTile);

    /// <summary>THE GAMEPAD IN THE MODERN SCHEME (stage 3 of the gamepad, per user 2026-10-08): the
    /// glyphs of the bindings in force, in the chosen button style.</summary>
    private static List<Section> fModernPad()
    {
        List<Section> lOSections = new List<Section>();

        lOSections.Add(new Section
        {
            Title = UWText.T("help.controls.pad.title"),
            Intro = UWText.T("help.controls.pad.intro")
        }
            .AddPad(fWalkStickName, UWText.T("help.controls.pad.walk"), fWalkStick)
            .AddPad(fLookStickName, UWText.T("help.controls.pad.look_around"), fLookStick)
            .AddPad(fPadName("ModernUse", 1), UWText.T("help.controls.pad.use"), fPad("ModernUse", 1))
            .AddPad(fPadName("ModernLook", 1), UWText.T("help.controls.pad.look"), fPad("ModernLook", 1))
            .AddPad(fPadName("PadJump", 0), UWText.T("help.controls.pad.jump"), fPad("PadJump", 0))
            .AddPad(fPadName("PadBack", 0), UWText.T("help.controls.pad.back"), fPad("PadBack", 0))
            .AddPad(fPadName("ModernAttack", 1), UWText.T("help.controls.pad.attack"), fPad("ModernAttack", 1))
            .AddPad(fPadName("ModernReady", 1), UWText.T("help.controls.pad.ready"), fPad("ModernReady", 1))
            .AddPad(fPadName("PadCast", 0), UWText.T("help.controls.pad.cast"), fPad("PadCast", 0))
            .AddPad(fPadName("PadSlotPrevious", 0) + " " + fPadName("PadSlotNext", 0), UWText.T("help.controls.pad.slot_pick"),
                fPad("PadSlotPrevious", 0), fPad("PadSlotNext", 0))
            .AddPad(fPadName("PadSlotUse", 0), UWText.T("help.controls.pad.slot_use"), fPad("PadSlotUse", 0))
            .AddPad(fPadName("ToggleInventory", 1), UWText.T("help.controls.pad.character_panel"), fPad("ToggleInventory", 1))
            .AddPad(fPadName("ModernHover", 4) + " " + fPadName("ModernHover", 5), UWText.T("help.controls.pad.sink_rise"),
                fPad("ModernHover", 4), fPad("ModernHover", 5))
            .AddPad(fPadName("ToggleMap", 1), UWText.T("help.controls.pad.map"), fPad("ToggleMap", 1))
            .AddPad(fPadName("Menu", 1), UWText.T("help.controls.pad.menu"), fPad("Menu", 1))
            .AddPad(fPadName("PadPointer", 0), UWText.T("help.controls.pad.pointer"), fPad("PadPointer", 0)));

        lOSections.Add(new Section
        {
            Title = UWText.T("help.controls.pad_pointer.title"),
            Intro = UWText.T("help.controls.pad_pointer.intro")
        }
            .AddPad(fLookStickName, UWText.T("help.controls.pad_pointer.move"), fLookStick)
            .AddPad(fPadName("PadClick", 0) + " " + fPadName("PadClick", 1), UWText.T("help.controls.pad_pointer.click"),
                fPad("PadClick", 0), fPad("PadClick", 1))
            .AddPad(fPadName("PadContext", 0) + " " + fPadName("PadContext", 1), UWText.T("help.controls.pad.right_button"),
                fPad("PadContext", 0), fPad("PadContext", 1))
            .AddPad(fWalkStickName, UWText.T("help.controls.pad_pointer.scroll"), fWalkStick)
            .AddPad(UWText.T("help.controls.key.dpad"), UWText.T("help.controls.pad_pointer.dpad"), UWGlyphs.Tiles(UWGlyphs.DpadVerticalTile)));

        return lOSections;
    }

    /// <summary>The gamepad in the classic scheme: the sticks drive, the buttons are the mouse's.</summary>
    private static List<Section> fClassicPad()
    {
        List<Section> lOSections = new List<Section>();

        lOSections.Add(new Section
        {
            Title = UWText.T("help.controls.pad.title"),
            Intro = UWText.T("help.controls.classic_pad.intro")
        }
            .AddPad(fWalkStickName, UWText.T("help.controls.classic_pad.walk"), fWalkStick)
            .AddPad(fLookStickName, UWText.T("help.controls.classic_pad.turn"), fLookStick)
            .AddPad(fPadName("PadClick", 0) + " " + fPadName("PadClick", 1), UWText.T("help.controls.classic_pad.left_button"),
                fPad("PadClick", 0), fPad("PadClick", 1))
            .AddPad(fPadName("PadContext", 0) + " " + fPadName("PadContext", 1), UWText.T("help.controls.pad.right_button"),
                fPad("PadContext", 0), fPad("PadContext", 1))
            .AddPad(fPadName("PadJump", 0), UWText.T("help.controls.pad.jump"), fPad("PadJump", 0))
            .AddPad(fPadName("ToggleMap", 1), UWText.T("help.controls.pad.map"), fPad("ToggleMap", 1))
            .AddPad(fPadName("PadPointer", 0), UWText.T("help.controls.classic_pad.pointer"), fPad("PadPointer", 0))
            .AddPad(fWalkStickName, UWText.T("help.controls.classic_pad.scroll"), fWalkStick));

        return lOSections;
    }

    /// <summary>The key of a changeable binding as it is now, or the fallback.</summary>
    private static string fKey(string psAction, int piBinding, string psFallback)
    {
        return UWBindingInfo.KeyName(psAction, piBinding, psFallback);
    }

    private static List<Section> fModern()
    {
        string lsLeft = fKey("CursorDrag", 0, UWText.T("help.controls.key.left_button"));
        string lsRight = fKey("Interact", 0, UWText.T("help.controls.key.right_button"));
        string lsMove = fKey("Move", 1, "W") + " " + fKey("Move", 3, "A") + " " + fKey("Move", 2, "S") + " " + fKey("Move", 4, "D");
        string lsLook = fKey("ModernLook", 0, "Q");
        string lsBags = fKey("ModernBags", 0, "B");
        string lsPanel = fKey("ToggleInventory", 0, "C");
        string lsRunes = fKey("ModernRunes", 0, "Z");
        string lsMap = fKey("ToggleMap", 0, "M");
        string lsHelp = fKey("ToggleHelp", 0, "Tab");
        string lsTrack = fKey("KeyTrack", 0, "F9");
        string lsCamp = fKey("KeyCamp", 0, "F10");
        string lsOptions = fKey("KeyOptions", 0, "F1");
        string lsScheme = fKey("ToggleScheme", 0, "F2");
        string lsWalkSlow = fKey("WalkSlow", 0, UWText.T("help.controls.key.left_shift"));
        string lsUse = fKey("ModernUse", 0, "E");

        List<Section> lOSections = new List<Section>();

        lOSections.Add(new Section
        {
            Title = UWText.T("help.controls.modern.pointer.title"),
            Intro = UWText.T("help.controls.modern.pointer.intro")
        }
            .Add(lsRight, UWText.T("help.controls.modern.pointer.right"))
            .Add(UWText.T("help.controls.modern.pointer.locked"), UWText.F("help.controls.modern.pointer.locked_text", lsUse, lsLook))
            .Add(UWText.T("help.controls.modern.pointer.free"), UWText.F("help.controls.modern.pointer.free_text", lsUse, lsLook)));

        lOSections.Add(new Section { Title = UWText.T("help.controls.modern.moving.title") }
            .Add(lsMove, UWText.T("help.controls.modern.moving.walk"))
            .Add(lsWalkSlow, UWText.T("help.controls.modern.moving.walk_slow"))
            .Add(fKey("EasyModifier", 0, UWText.T("help.controls.key.left_alt")) + " + " + UWBindingInfo.EasyStepKeyNames(true) + " / X",
                UWText.T("help.controls.modern.moving.easy"))
            .Add(UWText.T("help.controls.key.mouse"), UWText.T("help.controls.modern.moving.mouse"))
            .Add(fKey("Jump", 0, UWText.T("help.controls.key.space")), UWText.T("help.controls.modern.moving.jump"))
            .Add(fKey("ModernHover", 2, UWText.T("help.controls.key.space")) + " / " + fKey("ModernHover", 1, UWText.T("help.controls.key.left_ctrl")),
                UWText.T("help.controls.modern.moving.hover")));

        lOSections.Add(new Section { Title = UWText.T("help.controls.modern.acting.title") }
            .Add(UWText.F("help.controls.modern.acting.tap", lsUse), UWText.T("help.controls.modern.acting.tap_text"))
            .Add(UWText.F("help.controls.modern.acting.hold", lsUse), UWText.T("help.controls.modern.acting.hold_text"))
            .Add(lsLook, UWText.T("help.controls.modern.acting.look"))
            .Add(UWText.F("help.controls.modern.acting.free_pointer", lsLeft), UWText.T("help.controls.modern.acting.free_pointer_text")));

        lOSections.Add(new Section { Title = UWText.T("help.controls.modern.combat.title") }
            .Add(fKey("ModernReady", 0, "R"), UWText.T("help.controls.modern.combat.ready"))
            .Add(UWText.F("help.controls.modern.combat.held", lsLeft), UWText.T("help.controls.modern.combat.held_text"))
            .Add(UWText.T("help.controls.modern.combat.strike"), UWText.T("help.controls.modern.combat.strike_text"))
            .Add(UWText.T("help.controls.modern.combat.bow"), UWText.T("help.controls.modern.combat.bow_text")));

        lOSections.Add(new Section { Title = UWText.T("help.controls.modern.magic.title") }
            .Add(lsRunes, UWText.T("help.controls.modern.magic.runes"))
            .Add(UWText.T("help.controls.modern.magic.runes_tab"), UWText.T("help.controls.modern.magic.runes_tab_text"))
            .Add(UWText.T("help.controls.modern.magic.spells_tab"), UWText.T("help.controls.modern.magic.spells_tab_text"))
            .Add(UWText.T("help.controls.modern.magic.targets"), UWText.T("help.controls.modern.magic.targets_text"))
            .Add(UWText.T("help.controls.modern.magic.active"), UWText.T("help.controls.modern.magic.active_text")));

        lOSections.Add(new Section { Title = UWText.T("help.controls.modern.bags.title") }
            .Add(lsBags, UWText.T("help.controls.modern.bags.bags"))
            .Add(lsPanel, UWText.T("help.controls.modern.bags.panel"))
            .Add(lsLeft, UWText.T("help.controls.modern.bags.left"))
            .Add(UWText.F("help.controls.key.shift_plus", lsLeft), UWText.T("help.controls.modern.bags.split"))
            .Add(lsRight, UWText.T("help.controls.modern.bags.right"))
            .Add(UWText.T("help.controls.modern.bags.head"), UWText.T("help.controls.modern.bags.head_text"))
            .Add("1 ... 0", UWText.T("help.controls.modern.bags.action_bar"))
            .Add(UWText.T("help.controls.modern.bags.sleep_track"), UWText.F("help.controls.modern.bags.sleep_track_text", lsCamp, lsTrack)));

        lOSections.Add(new Section { Title = UWText.T("help.controls.modern.conversation.title") }
            .Add("1 ... 9", UWText.T("help.controls.modern.conversation.answer"))
            .Add(UWText.T("help.controls.modern.conversation.trading"), UWText.T("help.controls.modern.conversation.trading_text")));

        lOSections.Add(new Section { Title = UWText.T("help.controls.modern.rest.title") }
            .Add(lsMap, UWText.T("help.controls.modern.rest.map"))
            .Add(lsHelp, UWText.T("help.controls.modern.rest.help"))
            .Add(UWText.T("help.controls.key.escape"), UWText.F("help.controls.modern.rest.escape", lsOptions))
            .Add(UWText.T("help.controls.modern.rest.save_keys"), UWText.T("help.controls.modern.rest.save"))
            .Add(UWText.F("help.controls.key.shift_plus", lsScheme), UWText.T("help.controls.modern.rest.scheme")));

        return lOSections;
    }

    private static List<Section> fClassic()
    {
        List<Section> lOSections = new List<Section>();

        lOSections.Add(new Section
        {
            Title = UWText.T("help.controls.classic.mouse.title"),
            Intro = UWText.T("help.controls.classic.mouse.intro")
        }
            .Add(fKey("CursorDrag", 0, UWText.T("help.controls.key.left_button")), UWText.T("help.controls.classic.mouse.left"))
            .Add(fKey("Interact", 0, UWText.T("help.controls.key.right_button")), UWText.T("help.controls.classic.mouse.right"))
            .Add(UWText.F("help.controls.key.shift_plus", fKey("ToggleScheme", 0, "F2")), UWText.T("help.controls.classic.mouse.scheme")));

        Section lOKeys = new Section { Title = UWText.T("help.controls.classic.keys.title"), Intro = UWText.T("help.controls.classic.keys.intro") };

        // The modern scheme's keys are left out, the classic ones named without "(classic)"
        // (until step 8.5 by the label's words, now by the entry's scheme).
        foreach (UWKeyBindings.Entry lOEntry in UWKeyBindings.Entries)
        {
            if (lOEntry.Scheme == UWKeyBindings.SchemeEnum.Modern)
                continue;

            lOKeys.Add(UWBindingInfo.Name(lOEntry), lOEntry.PlainLabel);
        }

        lOSections.Add(lOKeys);

        return lOSections;
    }
}
