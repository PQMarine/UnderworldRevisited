namespace UWDataImport.UWData
{
	public static partial class UWTextEnglish
	{
		/// <summary>The layout editor (UWModernLayoutEditor and its inspector, cleanup step 8.5,
		/// 2026-10-24): the window top down, the save question, the inspector's own rows, and the
		/// interface's parts by name (UWModernLayout.NameIds, in ElementEnum's order).</summary>
		private static void fEditor()
		{
			fAdd("editor.title", "Edit layout");
			fAdd("editor.hint", "Click a part for its settings. Drag it to move, wheel to size, right click to reset. Drag this title to move the window.");
			fAdd("editor.preset", "Preset");
			fAdd("editor.panels_out", "Panels out");
			fAdd("editor.dialog", "Dialog");
			fAdd("editor.snap", "Snap");
			fAdd("editor.reset_all", "Reset all");
			fAdd("editor.done", "Done");
			fAdd("editor.no_selection", "No part selected");

			fAdd("editor.ask.title", "Save the layout?");
			fAdd("editor.ask.text", "The layout was changed. Save it as your own layout, or discard the changes?");
			fAdd("editor.ask.save", "Save");
			fAdd("editor.ask.discard", "Discard");
			fAdd("editor.ask.back", "Back");

			// The inspector's own rows (the settings' rows read setting.*).
			fAdd("editor.overview_hint", "Click a part to see its settings.");
			fAdd("editor.show", "Show");
			fAdd("editor.size", "Size");
			fAdd("editor.stands", "Stands");
			fAdd("editor.stands.apart", "Apart");
			fAdd("editor.stands.on_shelf", "On shelf");
			fAdd("editor.direction", "Direction");
			fAdd("editor.direction.text", "Text");
			fAdd("editor.direction.compass", "Compass");
			fAdd("editor.window", "Window");
			fAdd("editor.window.dock", "Dock");
			fAdd("editor.window.docked", "Docked");
			fAdd("editor.back", "Background");
			fAdd("editor.back.default", "Default");
			fAdd("editor.back.custom", "Custom");
			fAdd("editor.back.none", "None");
			fAdd("editor.back.oval", "Oval");
			fAdd("editor.back.rect", "Rect");
			fAdd("editor.back.colour", "Colour");
			fAdd("editor.back.pattern", "Pattern");
			fAdd("editor.back.border", "Border");
			fAdd("editor.back.border.on", "On");
			fAdd("editor.back.border.off", "Off");
			fAdd("editor.reset_part", "Reset part");

			// The parts, on their tags and as the inspector's heading.
			fAdd("editor.part.docked", "{0} (docked)");
			fAdd("editor.part.action_bar", "Action bar");
			fAdd("editor.part.minimap", "Minimap");
			fAdd("editor.part.heading", "Heading");
			fAdd("editor.part.active_spells", "Active spells");
			fAdd("editor.part.messages", "Messages");
			fAdd("editor.part.vitals", "Vitality and mana");
			fAdd("editor.part.bags", "Bags");
			fAdd("editor.part.character_panel", "Character panel");
			fAdd("editor.part.rune_panel", "Rune panel");
			fAdd("editor.part.conversation", "Conversation");
			fAdd("editor.part.compass", "Compass");
			fAdd("editor.part.power_gem", "Power gem");
			fAdd("editor.part.stone_shelf", "Stone shelf");
			fAdd("editor.part.movement_arrows", "Movement arrows");
			fAdd("editor.part.vitality_flask", "Vitality flask");
			fAdd("editor.part.mana_flask", "Mana flask");
			fAdd("editor.part.rune_hollow", "Rune hollow");
			fAdd("editor.part.rune_tablet", "Rune tablet");
			fAdd("editor.part.stats_panel", "Stats panel");
			fAdd("editor.part.character_page", "Character page");
		}
	}
}
