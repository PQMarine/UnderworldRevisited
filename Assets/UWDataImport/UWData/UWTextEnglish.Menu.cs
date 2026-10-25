namespace UWDataImport.UWData
{
	public static partial class UWTextEnglish
	{
		/// <summary>The modern game menu (UWModernGameMenu, cleanup step 8.5, 2026-10-24): the main
		/// page top down, the save and restore pages, the save name, the detail page.</summary>
		private static void fMenu()
		{
			fAdd("menu.title", "Game menu");
			fAdd("menu.resume", "Resume");
			fAdd("menu.save_game", "Save game");
			fAdd("menu.restore_game", "Restore game");
			fAdd("menu.pointer.hold", "hold to look around");
			fAdd("menu.pointer.toggle", "toggles the pointer");
			fAdd("menu.pointer.rmb", "RMB: {0}");
			fAdd("menu.edit_layout", "Edit layout");
			fAdd("menu.preset.classic_wide", "Classic Wide");
			fAdd("menu.preset.classic_modular", "Classic Modular");
			fAdd("menu.preset.modern", "Modern");
			fAdd("menu.preset.customize", "Customize");
			fAdd("menu.quit", "Quit to the main menu");
			fAdd("menu.back", "Back");

			fAdd("menu.save_slot_title", "Save game {0}");
			fAdd("menu.save_name", "Name of the save game:");
			fAdd("menu.save", "Save");
			fAdd("menu.cancel", "Cancel");

			fAdd("menu.detail.title", "Detail");
			fAdd("menu.detail.low", "Low");
			fAdd("menu.detail.medium", "Medium");
			fAdd("menu.detail.high", "High");
			// VeryHigh, the viewer's own values: a line, no button (cleanup 8d.3).
			fAdd("menu.detail.custom", "Custom");
		}
	}
}
