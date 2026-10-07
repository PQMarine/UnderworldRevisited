using System.Collections.Generic;

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
/// The names of changeable keys come from the bindings in force (UWKeyBindings.GetKeyName), so a
/// key the player moved shows where it is now; the fixed ones are written out.
/// </summary>
public static class UWHelpControls
{
    /// <summary>One line: the keys, and what they do.</summary>
    public struct Row
    {
        public string Keys;

        public string Text;
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
    }

    /// <summary>The sections for the scheme in force.</summary>
    public static List<Section> Build(bool pbModern)
    {
        return pbModern ? fModern() : fClassic();
    }

    /// <summary>The key of a changeable binding as it is now, or the fallback.</summary>
    private static string fKey(string psAction, int piBinding, string psFallback)
    {
        foreach (UWKeyBindings.Entry lOEntry in UWKeyBindings.Entries)
        {
            if (lOEntry.Action != psAction || lOEntry.Binding != piBinding)
                continue;

            string lsName = UWKeyBindings.GetKeyName(lOEntry);

            return string.IsNullOrEmpty(lsName) || lsName == "-" ? psFallback : lsName;
        }

        return psFallback;
    }

    private static List<Section> fModern()
    {
        string lsLeft = fKey("CursorDrag", 0, "Left button");
        string lsRight = fKey("Interact", 0, "Right button");
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

        List<Section> lOSections = new List<Section>();

        lOSections.Add(new Section
        {
            Title = "The pointer",
            Intro = "The mouse either turns the view (pointer locked, a crosshair in the middle) or moves a free "
                + "pointer over the windows and the world. Only the right button switches between the two."
        }
            .Add(lsRight, "Free the pointer, or lock it again to look around. The game menu has the other way "
                + "round as an option: the pointer stays free and the view turns while the button is held.")
            .Add("Locked", "The crosshair aims: E, " + lsLook + " and the left button act on what it is on.")
            .Add("Free", "The windows take the clicks; E and " + lsLook + " act on what is under the pointer, in the "
                + "world as in the bags, the character panel and the action bar (holding E does nothing over a window)."));

        lOSections.Add(new Section { Title = "Moving" }
            .Add(lsMove, "Walk forward and back, step sideways.")
            .Add("Mouse", "Turn and look up and down (pointer locked).")
            .Add(fKey("Jump", 0, "Space"), "Jump.")
            .Add("Space / Left Ctrl", "While flying or levitating: rise and sink."));

        lOSections.Add(new Section { Title = "Acting" }
            .Add("E (tap)", "The usual thing for what you aim at: pick it up, talk, open a door, pull a lever, use it.")
            .Add("E (hold)", "Use it directly, even what could be picked up - a sack is emptied instead of taken.")
            .Add(lsLook, "Look at it.")
            .Add(lsLeft + " (free pointer)", "On a thing in the world: its menu - Talk, Use, Look, Pick up. Held and "
                + "dragged: take it onto the pointer."));

        lOSections.Add(new Section { Title = "Combat" }
            .Add("R", "Draw or put away the weapon in your hand.")
            .Add(lsLeft + " (held)", "Charge the weapon; let go to strike. The power gem shows the charge.")
            .Add("The strike", "Pointer locked: look up for an overhead bash, down for a thrust, straight ahead for "
                + "a slash. Pointer free: the upper third of the screen bashes, the lower third thrusts, the middle slashes.")
            .Add("Bow, sling", "Charge as above; the red target circle shows when the charge is enough. The shot "
                + "flies to the free pointer, else to the crosshair."));

        lOSections.Add(new Section { Title = "Magic" }
            .Add(lsRunes, "The rune panel. Its handle at the left edge "
                + "opens it as well, and its pin keeps it out.")
            .Add("Runes tab", "Click a rune to lay it in the hollow (the fourth pushes out the first), right click "
                + "to look at it, the strip below the hollow clears it. Click the hollow to cast, or drag it onto "
                + "the action bar.")
            .Add("Spells tab", "Every spell by circle, grey what you cannot cast now. Click one to lay its runes, "
                + "drag it onto the action bar.")
            .Add("Targets", "A spell that needs a target flies at once at the crosshair with the pointer locked. "
                + "With the pointer free the target cursor waits for a left click; Escape lets the spell go.")
            .Add("Active spells", "Top right. Left click: what it does; right click: end it."));

        lOSections.Add(new Section { Title = "The bags and the character" }
            .Add(lsBags, "Open or close the bags. The backpack always shows at the bottom right.")
            .Add(lsPanel, "The character panel: the paperdoll, your attributes and skills. Its pin keeps it out.")
            .Add(lsLeft, "Take a thing and put it down - drag it, or click and click. On a container it goes "
                + "inside; outside the windows it is dropped (low on the screen) or thrown (higher).")
            .Add("Shift + " + lsLeft, "Split a stack.")
            .Add(lsRight, "A thing's menu: Use, Equip, Look, Pick up, Split. A container opens or closes at once "
                + "(Shift for its menu). What is used on something else - a key, the oil flask - then waits for "
                + "a left click on its target.")
            .Add("The head", "Food and drink put down on the paperdoll's head are eaten.")
            .Add("1 ... 0", "The action bar. Drop a thing or drag a spell onto a slot; the key or a click uses "
                + "it, a weapon goes into your hand. Drag a slot off the bar to empty it.")
            .Add("Sleep, Track", "The bedroll and the boots beside the paperdoll's feet - or " + lsCamp + " and "
                + lsTrack + "."));

        lOSections.Add(new Section { Title = "Conversation" }
            .Add("1 ... 9", "Choose an answer - or click it. The mouse wheel scrolls the conversation.")
            .Add("Trading", "Drag your things from the bags onto \"Your offer\". A left click on a trade slot marks "
                + "it for the deal (a gold ring), a right click looks at it; drag your offer back out when you "
                + "change your mind."));

        lOSections.Add(new Section { Title = "Map, menu and the rest" }
            .Add(lsMap, "The big map, with the map in your pack. The minimap top right: + and - or the mouse "
                + "wheel zoom it, a click opens the big map.")
            .Add(lsHelp, "This help, in the character panel.")
            .Add("Escape", "Closes what is open - a waiting spell, the bags, the panels - then the game menu ("
                + lsOptions + " as well).")
            .Add("Right Ctrl + S / R", "Save, restore.")
            .Add("Shift + " + lsScheme, "Switch to the classic controls."));

        return lOSections;
    }

    private static List<Section> fClassic()
    {
        List<Section> lOSections = new List<Section>();

        lOSections.Add(new Section
        {
            Title = "The mouse",
            Intro = "The controls of the original: the pointer stays free, the view lies in the frame."
        }
            .Add(fKey("CursorDrag", 0, "Left button"), "In the view: walk towards the pointer, held. On the frame: "
                + "the icons, the runes, the compass, and taking and putting down things.")
            .Add(fKey("Interact", 0, "Right button"), "Do what the lit icon says - look by default; talk, get, use "
                + "after its icon. With the weapon drawn: held to charge, let go to strike; the third of the view "
                + "the pointer is in chooses the strike.")
            .Add("Shift + " + fKey("ToggleScheme", 0, "F2"), "Switch to the modern controls."));

        Section lOKeys = new Section { Title = "Keys", Intro = "Changeable under Controls in the menu bar at the top edge." };

        foreach (UWKeyBindings.Entry lOEntry in UWKeyBindings.Entries)
        {
            if (lOEntry.Label.Contains("(modern)"))
                continue;

            lOKeys.Add(UWKeyBindings.GetKeyName(lOEntry), lOEntry.Label.Replace(" (classic)", string.Empty));
        }

        lOSections.Add(lOKeys);

        return lOSections;
    }
}
