namespace UWDataImport.UWData
{
	public static partial class UWTextEnglish
	{
		/// <summary>The gamepad (cleanup step 8.5, 2026-10-24): its changeable buttons
		/// (UWKeyBindings.PadEntries) in the Controls dialog's order, the button names of the three
		/// styles, and the hints at the screen's edge.</summary>
		private static void fPad()
		{
			fAdd("pad.binding.use", "Use (held: use directly)");
			fAdd("pad.binding.look", "Look");
			fAdd("pad.binding.back", "Close what is open");
			fAdd("pad.binding.jump", "Jump");
			fAdd("pad.binding.character_panel", "Character panel (held: every bag)");
			fAdd("pad.binding.attack", "Draw the weapon and strike");
			fAdd("pad.binding.ready", "Draw or put away the weapon");
			fAdd("pad.binding.cast", "Cast the runes (held: rune panel)");
			fAdd("pad.binding.slot_previous", "Action bar: previous slot");
			fAdd("pad.binding.slot_next", "Action bar: next slot");
			fAdd("pad.binding.slot_use", "Action bar: use the slot");
			fAdd("pad.binding.map", "Open the map (tapped)");
			fAdd("pad.binding.sink", "Sink while flying");
			fAdd("pad.binding.rise", "Rise while flying");
			fAdd("pad.binding.menu", "Menu");
			fAdd("pad.binding.help", "Help (held)");
			fAdd("pad.binding.pointer", "Pointer on the stick, on or off");
			fAdd("pad.binding.click", "Pointer: click (held: drag)");
			fAdd("pad.binding.click_second", "Pointer: click, second button");
			fAdd("pad.binding.context", "Pointer: right button");
			fAdd("pad.binding.context_second", "Pointer: right button, second button");

			fPadMore();
		}
	}
}
