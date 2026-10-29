namespace UWDataImport.UWData
{
	public static partial class UWTextEnglish
	{
		/// <summary>The modern interface's own words (cleanup step 8.5, 2026-10-24): the bags' menu,
		/// the rune panel, the character panel, the trade areas, the minimap, the crosshair's pad
		/// hint, the heading strip, the question and split buttons, the bags' weights and tooltips;
		/// then the few messages the port adds to the message scroll itself (prompt.*).</summary>
		private static void fHud()
		{
			// The menu on a thing (UWModernBagMenu), one text per entry.
			fAdd("hud.menu.use", "Use");
			fAdd("hud.menu.open", "Open");
			fAdd("hud.menu.close", "Close");
			fAdd("hud.menu.open_runes", "Open");
			fAdd("hud.menu.equip", "Equip");
			fAdd("hud.menu.take_off", "Take off");
			fAdd("hud.menu.look", "Look");
			fAdd("hud.menu.pick_up", "Pick up");
			fAdd("hud.menu.split", "Split");
			fAdd("hud.menu.cancel", "Cancel");
			fAdd("hud.menu.talk", "Talk");
			fAdd("hud.menu.world_use", "Use");
			fAdd("hud.menu.world_look", "Look");
			fAdd("hud.menu.world_pick_up", "Pick up");

			// The rune panel (UWModernRunePanel).
			fAdd("hud.runes.tab.runes", "Runes");
			fAdd("hud.runes.tab.spells", "Spells");
			fAdd("hud.runes.handle", "Runes");
			fAdd("hud.runes.title", "Rune bag");
			fAdd("hud.runes.owned", "{0} of {1}");
			fAdd("hud.runes.circle", "Circle {0}  -  {1} mana");
			fAdd("hud.runes.above_circle", "above your circle");
			fAdd("hud.runes.drag_or_cast", "drag onto the bar, click to cast");
			fAdd("hud.runes.not_a_spell", "Not a spell");
			fAdd("hud.runes.none_chosen", "No runes chosen");
			fAdd("hud.runes.click_runes", "click runes on the shelf");
			fAdd("hud.spells.title", "Spells");
			fAdd("hud.spells.up_to_circle", "up to circle {0}");

			// The character panel (UWModernPanel).
			fAdd("hud.panel.tab.character", "Character");
			fAdd("hud.panel.tab.help", "Help");
			fAdd("hud.panel.handle", "Character");
			fAdd("hud.panel.skills", "Skills");
			fAdd("hud.panel.sleep", "Sleep");
			fAdd("hud.panel.track", "Track");

			// The trade areas of the conversation (UWModernConversation).
			fAdd("hud.trade.partner_goods", "{0}'s goods");
			fAdd("hud.trade.goods", "Goods");
			fAdd("hud.trade.your_offer", "Your offer");

			// The minimap (UWModernMinimap).
			fAdd("hud.minimap.level", "Level {0}");
			fAdd("hud.minimap.no_map", "No map");

			// The pad's hint under the crosshair (UWModernWorldInput, UWModernCrosshair).
			fAdd("hud.crosshair.pick_up", "Pick up");
			fAdd("hud.crosshair.talk", "Talk");
			fAdd("hud.crosshair.use", "Use");
			fAdd("hud.crosshair.look", "Look");

			// The heading strip (UWModernHeadingStrip): the foe's state and the sixteen headings.
			fAdd("hud.foe.healthy", "Healthy");
			fAdd("hud.foe.wounded", "Wounded");
			fAdd("hud.foe.critical", "Critical");
			fAdd("hud.heading.n", "N");
			fAdd("hud.heading.nne", "NNE");
			fAdd("hud.heading.ne", "NE");
			fAdd("hud.heading.ene", "ENE");
			fAdd("hud.heading.e", "E");
			fAdd("hud.heading.ese", "ESE");
			fAdd("hud.heading.se", "SE");
			fAdd("hud.heading.sse", "SSE");
			fAdd("hud.heading.s", "S");
			fAdd("hud.heading.ssw", "SSW");
			fAdd("hud.heading.sw", "SW");
			fAdd("hud.heading.wsw", "WSW");
			fAdd("hud.heading.w", "W");
			fAdd("hud.heading.wnw", "WNW");
			fAdd("hud.heading.nw", "NW");
			fAdd("hud.heading.nnw", "NNW");

			// The question's and the split's buttons (UWModernQuestion, UWModernBagSplit).
			fAdd("hud.question.ok", "OK");
			fAdd("hud.question.cancel", "Cancel");
			fAdd("hud.split.ok", "OK");

			// The bags (UWModernBagView, UWModernBagPages): weights in stones, the tooltip's known magic.
			fAdd("hud.bag.backpack", "Backpack");
			fAdd("hud.bag.weight", "{0} st");
			fAdd("hud.bag.weight_of", "{0} / {1} st");
			fAdd("hud.item.cursed", "Cursed {0}");
			fAdd("hud.item.of", "of {0}");
			fAdd("hud.item.no_charges", "with no full charges");
			fAdd("hud.item.one_charge", "with 1 full charge");
			fAdd("hud.item.charges", "with {0} full charges");

			// Messages the port adds to the scroll. "Use ... on what?" is the original's sentence,
			// assembled by UW.EXE from pieces, not a line of STRINGS.PAK (UWClassicUseMode).
			fAdd("prompt.use_on_what", "Use {0} on what?");
			fAdd("prompt.not_on_action_bar", "That does not go on the action bar.");
			fAdd("prompt.choose_target", "Choose the target with the left button.");

			// The character sheet's own words (UWCharacterSheet, readable spelling): the modern
			// panel and the help window's Stats tab.
			fAdd("hud.sheet.strength", "Strength");
			fAdd("hud.sheet.dexterity", "Dexterity");
			fAdd("hud.sheet.intelligence", "Intelligence");
			fAdd("hud.sheet.vitality", "Vitality");
			fAdd("hud.sheet.mana", "Mana");
			fAdd("hud.sheet.experience", "Experience");
			fAdd("hud.sheet.class_level", "{0}, level {1}");
			fAdd("hud.sheet.level_class", "Level {0} {1}");
			fAdd("hud.sheet.point_to_spend", "1 point to spend");
			fAdd("hud.sheet.points_to_spend", "{0} points to spend");

			// The walk through the second game (UW2 plan phase 3 step 1): its message scroll's own lines
			// until the game's rules say them.
			fAdd("hud.walk.not_yet", "Not yet in the walk through Ultima Underworld II.");
			fAdd("hud.walk.door_locked", "The door is locked.");
			fAdd("hud.walk.nothing", "There is nothing there.");

			// The second game's statistics page: the level with its ordinal (STATS.C sp_hdr: ST, ND, RD,
			// TH), and a flask's value after the game's "Your current vitality is" (flask_info).
			fAdd("hud.uw2.level_1", "{0}ST");
			fAdd("hud.uw2.level_2", "{0}ND");
			fAdd("hud.uw2.level_3", "{0}RD");
			fAdd("hud.uw2.level_n", "{0}TH");
			fAdd("hud.uw2.flask_value", "{0} {1} out of {2}");

			// The second game's save slots in the scroll (GAMEWRAP.C ShowSaveRest's labels "I- " to "IV- ").
			fAdd("hud.uw2.save_slot_1", "I- {0}");
			fAdd("hud.uw2.save_slot_2", "II- {0}");
			fAdd("hud.uw2.save_slot_3", "III- {0}");
			fAdd("hud.uw2.save_slot_4", "IV- {0}");
		}

	}
}
