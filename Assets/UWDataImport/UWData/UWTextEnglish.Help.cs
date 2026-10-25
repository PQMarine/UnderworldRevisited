namespace UWDataImport.UWData
{
	public static partial class UWTextEnglish
	{
		/// <summary>The help window (cleanup step 8.5, 2026-10-24): the tab buttons, each tab's own
		/// words, our notes on the spells and mantras (UWHelpContent) and the controls guide
		/// (UWHelpControls), in the order they appear. Spell, rune, skill and mantra names come from
		/// the game's data and are not here.</summary>
		private static void fHelp()
		{
			// The tab buttons.
			fAdd("help.tab.stats", "Stats");
			fAdd("help.tab.map", "Map");
			fAdd("help.tab.notes", "Notes");
			fAdd("help.tab.spells", "Spells");
			fAdd("help.tab.mantras", "Mantras");
			fAdd("help.tab.manual", "Manual");
			fAdd("help.tab.controls", "Controls");

			// The Stats tab.
			fAdd("help.stats.title", "Stats");
			fAdd("help.stats.no_character", "No character is loaded.");
			fAdd("help.stats.attributes", "Attributes");
			fAdd("help.stats.can_carry", "Can still carry");
			fAdd("help.stats.stones", "{0} stones");
			fAdd("help.stats.skills", "Skills");
			fAdd("help.stats.hide_hidden", "Hide the hidden values");
			fAdd("help.stats.show_hidden", "Show the hidden values");
			fAdd("help.stats.hidden_intro", "The game keeps these, but shows them only as words or not at all.");
			fAdd("help.stats.food", "Food");
			fAdd("help.stats.food_note", "255 is full; the status line changes every 30.");
			fAdd("help.stats.fatigue", "Fatigue");
			fAdd("help.stats.fatigue_note", "0 is rested; the status line changes every 23.");
			fAdd("help.stats.poison", "Poison");
			fAdd("help.stats.hallucination", "Hallucination");
			fAdd("help.stats.hallucination.scrambled", "scrambled textures");
			fAdd("help.stats.hallucination.palette", "palette {0}");
			fAdd("help.stats.hallucination.light_table", "dark light table");
			fAdd("help.stats.hallucination_note", "A mushroom adds one, the potion sets three; one wears off every 21 seconds.");
			fAdd("help.stats.skill_points", "Unspent skill points");
			fAdd("help.stats.skill_points_note", "A mantra at a shrine spends them. One more every 3000 experience and every level.");
			fAdd("help.stats.experience", "Exact experience");
			fAdd("help.stats.next_level", "Next level");
			fAdd("help.stats.next_level_none", "none, this is the highest");
			fAdd("help.stats.next_level_at", "Next level at");
			fAdd("help.stats.next_level_value", "{0} ({1} to go)");
			fAdd("help.stats.load", "Load");
			fAdd("help.stats.load_value", "{0} of {1} stones");
			fAdd("help.stats.worn_magic", "Worn magic");
			fAdd("help.stats.no_inventory", "No inventory.");
			fAdd("help.stats.curse", "Curse");
			fAdd("help.stats.when_striking", "{0} (when striking)");
			fAdd("help.stats.not_in_effect", "{0} (not in effect here)");
			fAdd("help.stats.nothing_enchanted", "Nothing worn is enchanted.");
			fAdd("help.stats.together", "Together, without cast spells:");
			fAdd("help.stats.protection", "Protection");
			fAdd("help.stats.protection_value", "head {0}, body {1}, hands {2}, legs {3}");
			fAdd("help.stats.resistance", "Resistance");
			fAdd("help.stats.light", "Light");
			fAdd("help.stats.movement", "Movement");
			fAdd("help.stats.stealth", "Stealth");
			fAdd("help.stats.immunity", "Immunity");
			fAdd("help.stats.regeneration", "Regeneration");
			fAdd("help.stats.curse_value", "{0} dice of eight, now and then");
			fAdd("help.stats.maze", "Maze navigation");
			fAdd("help.stats.maze_value", "shows the way in Tybal's maze");
			fAdd("help.stats.dragon_boots", "Dragon skin boots");
			fAdd("help.stats.dragon_boots_value", "no harm from lava");
			fAdd("help.stats.slot.weapon_hand", "Weapon hand");
			fAdd("help.stats.slot.shield_hand", "Shield hand");
			fAdd("help.stats.slot.left_ring", "Left ring");
			fAdd("help.stats.slot.right_ring", "Right ring");
			fAdd("help.stats.slot.helmet", "Helmet");
			fAdd("help.stats.slot.chest", "Chest");
			fAdd("help.stats.slot.gloves", "Gloves");
			fAdd("help.stats.slot.legs", "Legs");
			fAdd("help.stats.slot.boots", "Boots");

			// The Map tab.
			fAdd("help.map.title", "Map");
			fAdd("help.map.no_level", "No level is loaded.");
			fAdd("help.map.no_map", "You carry no map. Pick it up again to see it here.");
			fAdd("help.map.level", "Level {0}. The wheel zooms; notes are written on the map itself.");

			// The Notes tab.
			fAdd("help.notes.intro", "Your own notes, kept with the save game.");

			// The Spells tab; the notes are our own words (UWHelpContent), not the manual's.
			fAdd("help.spells.title", "Spells");
			fAdd("help.spells.intro", "Put the runes on the shelf from the rune bag, then click the shelf to cast. "
				+ "A spell costs three mana per circle, and your level halved (rounded up) must reach its circle. "
				+ "Click a spell here to lay its runes into the hollow; grey runes are not in your bag yet.");
			fAdd("help.spells.hide_more", "Hide the spells the manual does not name");
			fAdd("help.spells.show_more", "Show the spells the manual does not name");
			fAdd("help.spells.circle", "Circle {0}  -  {1} mana");
			fAdd("help.spells.in_hollow", "(in the hollow)");
			fAdd("help.spells.note.0", "Lights up the surroundings for a while.");
			fAdd("help.spells.note.1", "Protects against blows like a full suit of armour, for a while.");
			fAdd("help.spells.note.2", "Shoots a magic missile at a target.");
			fAdd("help.spells.note.3", "Conjures a meal.");
			fAdd("help.spells.note.4", "Muffles your steps for a short time, so creatures notice you less.");
			fAdd("help.spells.note.7", "Lets you drift down gently for a short time.");
			fAdd("help.spells.note.8", "Heals light wounds.");
			fAdd("help.spells.note.9", "Tells you of creatures nearby that you cannot see.");
			fAdd("help.spells.note.10", "May make a foe lose heart and run.");
			fAdd("help.spells.note.11", "Lays a ward that tells you when something disturbs it.");
			fAdd("help.spells.note.12", "You move faster than your foes, for a while.");
			fAdd("help.spells.note.13", "Makes you harder to see for a short time.");
			fAdd("help.spells.note.14", "Lets you see in the dark, for a while.");
			fAdd("help.spells.note.15", "Hurls a lightning bolt at a target.");
			fAdd("help.spells.note.16", "Spikes a door shut.");
			fAdd("help.spells.note.19", "Heals serious wounds.");
			fAdd("help.spells.note.20", "Lifts you straight up into the air for a short time.");
			fAdd("help.spells.note.21", "Poisons a foe.");
			fAdd("help.spells.note.22", "Gives some protection from fire for a short time.");
			fAdd("help.spells.note.23", "Disarms a trap you aim at.");
			fAdd("help.spells.note.24", "Hurls a fireball at a target.");
			fAdd("help.spells.note.26", "Tells you what magic an object holds.");
			fAdd("help.spells.note.27", "Makes you proof against missiles, for a while.");
			fAdd("help.spells.note.28", "Unlocks a locked door or chest.");
			fAdd("help.spells.note.29", "Cures poison.");
			fAdd("help.spells.note.30", "Heals you completely.");
			fAdd("help.spells.note.32", "Takes you to your moonstone at once.");
			fAdd("help.spells.note.33", "Holds a foe in place.");
			fAdd("help.spells.note.34", "Bright light for a long time.");
			fAdd("help.spells.note.35", "Lets you take and use a single object from afar, for a while.");
			fAdd("help.spells.note.36", "Lets you fly for a while, then glide to the ground.");
			fAdd("help.spells.note.37", "Turns a creature to fight on your side.");
			fAdd("help.spells.note.39", "Makes you nearly invisible, for a while.");
			fAdd("help.spells.note.40", "Makes the foes around you stagger as if drunk.");
			fAdd("help.spells.note.41", "Reveals hidden objects and secret ways around you.");
			fAdd("help.spells.note.42", "Greatly hardens you against damage, for a while.");
			fAdd("help.spells.note.43", "Makes the ground quake and the rocks burst.");
			fAdd("help.spells.note.44", "Lets you see the world from above, for a while.");
			fAdd("help.spells.note.45", "Rains flaming missiles on the area.");
			fAdd("help.spells.note.46", "Stops time for everyone but you, for a while.");

			// The Mantras tab.
			fAdd("help.mantras.title", "Mantras");
			fAdd("help.mantras.intro", "Talk to an ankh shrine and type a mantra. It raises your skills only when you "
				+ "have earned a skill point.");
			fAdd("help.mantras.group", "{0} ({1})");
			fAdd("help.mantras.note.combat", "Improves some of your combat skills.");
			fAdd("help.mantras.note.magic", "Improves some of your magic skills.");
			fAdd("help.mantras.note.other", "Improves some of your other skills.");
			fAdd("help.mantras.note.cup_of_wonder", "Tells you where the Cup of Wonder lies.");
			fAdd("help.mantras.note.key_of_truth", "Gives you the Key of Truth.");
			fAdd("help.mantras.hide_more", "Hide the mantras the manual does not name");
			fAdd("help.mantras.show_more", "Show the mantras the manual does not name");
			fAdd("help.mantras.single", "One skill each, raised greatly");
			fAdd("help.mantras.quests", "Quests");

			// The Manual tab and its reading mode (UWManual's problems too).
			fAdd("help.manual.title", "Manual");
			fAdd("help.manual.cannot_show", "The manual cannot be shown.");
			fAdd("help.manual.not_found", "The manual was not found. The GOG version brings it as {0} in the folder of game.gog.");
			fAdd("help.manual.not_opened", "The manual could not be opened: {0}");
			fAdd("help.manual.unavailable", "Showing the manual is not available on this system yet.");
			fAdd("help.manual.page", "Page {0} of {1}");
			fAdd("help.manual.pages", "Pages {0} and {1} of {2}");
			fAdd("help.manual.click_to_read", "Click the page to read it large.");
			fAdd("help.manual.reading_keys", "Arrow keys or the wheel turn the pages, Esc or a click on the page closes");

			// The Controls tab (UWHelpControls): key names written out.
			fAdd("help.controls.key.left_stick", "Left stick");
			fAdd("help.controls.key.right_stick", "Right stick");
			fAdd("help.controls.key.dpad", "D-pad");
			fAdd("help.controls.key.left_button", "Left button");
			fAdd("help.controls.key.right_button", "Right button");
			fAdd("help.controls.key.left_shift", "Left Shift");
			fAdd("help.controls.key.left_alt", "Left Alt");
			fAdd("help.controls.key.left_ctrl", "Left Ctrl");
			fAdd("help.controls.key.space", "Space");
			fAdd("help.controls.key.mouse", "Mouse");
			fAdd("help.controls.key.escape", "Escape");
			fAdd("help.controls.key.shift_plus", "Shift + {0}");

			// The gamepad's sections.
			fAdd("help.controls.pad.title", "Gamepad");
			fAdd("help.controls.pad.intro", "Changeable in the Controls dialog of the menu bar, its Gamepad tab - the button names too.");
			fAdd("help.controls.pad.walk", "Walk and step sideways.");
			fAdd("help.controls.pad.look_around", "Look around.");
			fAdd("help.controls.pad.use", "Use what you aim at; held: use it directly.");
			fAdd("help.controls.pad.look", "Look at it.");
			fAdd("help.controls.pad.jump", "Jump.");
			fAdd("help.controls.pad.back", "Close what is open.");
			fAdd("help.controls.pad.attack", "Draw the weapon; held: charge, let go to strike.");
			fAdd("help.controls.pad.ready", "Put the weapon away.");
			fAdd("help.controls.pad.cast", "Cast the runes in the hollow; held: the rune panel.");
			fAdd("help.controls.pad.slot_pick", "Pick a slot of the action bar.");
			fAdd("help.controls.pad.slot_use", "Use the slot.");
			fAdd("help.controls.pad.character_panel", "The character panel; held: every bag.");
			fAdd("help.controls.pad.sink_rise", "Sink and rise while flying.");
			fAdd("help.controls.pad.map", "The map; held: this help.");
			fAdd("help.controls.pad.menu", "The game menu.");
			fAdd("help.controls.pad.pointer", "Free the pointer (onto the backpack) or lock it again.");
			fAdd("help.controls.pad.right_button", "The right mouse button.");
			fAdd("help.controls.pad_pointer.title", "Gamepad: the free pointer");
			fAdd("help.controls.pad_pointer.intro", "With the pointer free the pad is the mouse. Under a window that holds the game - the menu, a "
				+ "conversation, the map - the pointer is free by itself.");
			fAdd("help.controls.pad_pointer.move", "Move the pointer.");
			fAdd("help.controls.pad_pointer.click", "The left mouse button; held: drag.");
			fAdd("help.controls.pad_pointer.scroll", "Over a window: scroll. In a menu: the next entry.");
			fAdd("help.controls.pad_pointer.dpad", "In a menu: the next entry.");
			fAdd("help.controls.classic_pad.intro", "The pad's buttons are the mouse's buttons, where the pointer is. Changeable in the Controls "
				+ "dialog of the menu bar, its Gamepad tab.");
			fAdd("help.controls.classic_pad.walk", "Walk; pushed well sideways: step sideways.");
			fAdd("help.controls.classic_pad.turn", "Turn; up and down tilt the view a step.");
			fAdd("help.controls.classic_pad.left_button", "The left mouse button.");
			fAdd("help.controls.classic_pad.pointer", "The stick moves the pointer instead, or turns again.");
			fAdd("help.controls.classic_pad.scroll", "With the pointer on the stick, over the frame: scroll.");

			// The modern scheme.
			fAdd("help.controls.modern.title", "Modern controls");
			fAdd("help.controls.modern.pointer.title", "The pointer");
			fAdd("help.controls.modern.pointer.intro", "The mouse either turns the view (pointer locked, a crosshair in the middle) or moves a free "
				+ "pointer over the windows and the world. Only the right button switches between the two.");
			fAdd("help.controls.modern.pointer.right", "Free the pointer, or lock it again to look around. The game menu has the other way "
				+ "round as an option: the pointer stays free and the view turns while the button is held.");
			fAdd("help.controls.modern.pointer.locked", "Locked");
			fAdd("help.controls.modern.pointer.locked_text", "The crosshair aims: {0}, {1} and the left button act on what it is on.");
			fAdd("help.controls.modern.pointer.free", "Free");
			fAdd("help.controls.modern.pointer.free_text", "The windows take the clicks; {0} and {1} act on what is under the pointer, in the "
				+ "world as in the bags, the character panel and the action bar (holding {0} does nothing over a window).");
			fAdd("help.controls.modern.moving.title", "Moving");
			fAdd("help.controls.modern.moving.walk", "Walk forward and back, step sideways.");
			fAdd("help.controls.modern.moving.walk_slow", "Held: walk slowly instead of running. The Controls dialog can make it switch on and off.");
			fAdd("help.controls.modern.moving.easy", "Easy movement: one step forward or back, or a turn "
				+ "of 45 degrees; held, it repeats. X steps forward without stepping off a ledge.");
			fAdd("help.controls.modern.moving.mouse", "Turn and look up and down (pointer locked).");
			fAdd("help.controls.modern.moving.jump", "Jump.");
			fAdd("help.controls.modern.moving.hover", "While flying or levitating: rise and sink.");
			fAdd("help.controls.modern.acting.title", "Acting");
			fAdd("help.controls.modern.acting.tap", "{0} (tap)");
			fAdd("help.controls.modern.acting.tap_text", "The usual thing for what you aim at: pick it up, talk, open a door, pull a lever, use it.");
			fAdd("help.controls.modern.acting.hold", "{0} (hold)");
			fAdd("help.controls.modern.acting.hold_text", "Use it directly, even what could be picked up - a sack is emptied instead of taken.");
			fAdd("help.controls.modern.acting.look", "Look at it.");
			fAdd("help.controls.modern.acting.free_pointer", "{0} (free pointer)");
			fAdd("help.controls.modern.acting.free_pointer_text", "On a thing in the world: its menu - Talk, Use, Look, Pick up. Held and "
				+ "dragged: take it onto the pointer.");
			fAdd("help.controls.modern.combat.title", "Combat");
			fAdd("help.controls.modern.combat.ready", "Draw or put away the weapon in your hand.");
			fAdd("help.controls.modern.combat.held", "{0} (held)");
			fAdd("help.controls.modern.combat.held_text", "Charge the weapon; let go to strike. The power gem shows the charge.");
			fAdd("help.controls.modern.combat.strike", "The strike");
			fAdd("help.controls.modern.combat.strike_text", "Pointer locked: look up for an overhead bash, down for a thrust, straight ahead for "
				+ "a slash. Pointer free: the upper third of the screen bashes, the lower third thrusts, the middle slashes.");
			fAdd("help.controls.modern.combat.bow", "Bow, sling");
			fAdd("help.controls.modern.combat.bow_text", "Charge as above; the red target circle shows when the charge is enough. The shot "
				+ "flies to the free pointer, else to the crosshair.");
			fAdd("help.controls.modern.magic.title", "Magic");
			fAdd("help.controls.modern.magic.runes", "The rune panel. Its handle at the left edge "
				+ "opens it as well, and its pin keeps it out.");
			fAdd("help.controls.modern.magic.runes_tab", "Runes tab");
			fAdd("help.controls.modern.magic.runes_tab_text", "Click a rune to lay it in the hollow (the fourth pushes out the first), right click "
				+ "to look at it, the strip below the hollow clears it. Click the hollow to cast, or drag it onto "
				+ "the action bar.");
			fAdd("help.controls.modern.magic.spells_tab", "Spells tab");
			fAdd("help.controls.modern.magic.spells_tab_text", "Every spell by circle, grey what you cannot cast now. Click one to lay its runes, "
				+ "drag it onto the action bar.");
			fAdd("help.controls.modern.magic.targets", "Targets");
			fAdd("help.controls.modern.magic.targets_text", "A spell that needs a target flies at once at the crosshair with the pointer locked. "
				+ "With the pointer free the target cursor waits for a left click; Escape lets the spell go.");
			fAdd("help.controls.modern.magic.active", "Active spells");
			fAdd("help.controls.modern.magic.active_text", "Top right. Left click: what it does; right click: end it.");
			fAdd("help.controls.modern.bags.title", "The bags and the character");
			fAdd("help.controls.modern.bags.bags", "Open or close the bags. The backpack always shows at the bottom right.");
			fAdd("help.controls.modern.bags.panel", "The character panel: the paperdoll, your attributes and skills. Its pin keeps it out.");
			fAdd("help.controls.modern.bags.left", "Take a thing and put it down - drag it, or click and click. On a container it goes "
				+ "inside; outside the windows it is dropped (low on the screen) or thrown (higher).");
			fAdd("help.controls.modern.bags.split", "Split a stack.");
			fAdd("help.controls.modern.bags.right", "A thing's menu: Use, Equip, Look, Pick up, Split. A container opens or closes at once "
				+ "(Shift for its menu). What is used on something else - a key, the oil flask - then waits for "
				+ "a left click on its target.");
			fAdd("help.controls.modern.bags.head", "The head");
			fAdd("help.controls.modern.bags.head_text", "Food and drink put down on the paperdoll's head are eaten.");
			fAdd("help.controls.modern.bags.action_bar", "The action bar. Drop a thing or drag a spell onto a slot; the key or a click uses "
				+ "it, a weapon goes into your hand. Drag a slot off the bar to empty it.");
			fAdd("help.controls.modern.bags.sleep_track", "Sleep, Track");
			fAdd("help.controls.modern.bags.sleep_track_text", "The bedroll and the boots beside the paperdoll's feet - or {0} and "
				+ "{1}.");
			fAdd("help.controls.modern.conversation.title", "Conversation");
			fAdd("help.controls.modern.conversation.answer", "Choose an answer - or click it. The mouse wheel scrolls the conversation.");
			fAdd("help.controls.modern.conversation.trading", "Trading");
			fAdd("help.controls.modern.conversation.trading_text", "Drag your things from the bags onto \"Your offer\". A left click on a trade slot marks "
				+ "it for the deal (a gold ring), a right click looks at it; drag your offer back out when you "
				+ "change your mind.");
			fAdd("help.controls.modern.rest.title", "Map, menu and the rest");
			fAdd("help.controls.modern.rest.map", "The big map, with the map in your pack. The minimap top right: + and - or the mouse "
				+ "wheel zoom it, a click opens the big map.");
			fAdd("help.controls.modern.rest.help", "This help, in the character panel.");
			fAdd("help.controls.modern.rest.escape", "Closes what is open - a waiting spell, the bags, the panels - then the game menu ("
				+ "{0} as well).");
			fAdd("help.controls.modern.rest.save_keys", "Right Ctrl + S / R");
			fAdd("help.controls.modern.rest.save", "Save, restore.");
			fAdd("help.controls.modern.rest.scheme", "Switch to the classic controls.");

			// The classic scheme.
			fAdd("help.controls.classic.title", "Classic controls");
			fAdd("help.controls.classic.mouse.title", "The mouse");
			fAdd("help.controls.classic.mouse.intro", "The controls of the original: the pointer stays free, the view lies in the frame.");
			fAdd("help.controls.classic.mouse.left", "In the view: walk towards the pointer, held. On the frame: "
				+ "the icons, the runes, the compass, and taking and putting down things.");
			fAdd("help.controls.classic.mouse.right", "Do what the lit icon says - look by default; talk, get, use "
				+ "after its icon. With the weapon drawn: held to charge, let go to strike; the third of the view "
				+ "the pointer is in chooses the strike.");
			fAdd("help.controls.classic.mouse.scheme", "Switch to the modern controls.");
			fAdd("help.controls.classic.keys.title", "Keys");
			fAdd("help.controls.classic.keys.intro", "Changeable under Controls in the menu bar at the top edge.");
		}
	}
}
