using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 8b.3 (2026-10-24): the help notes' text on the kit (UWTextArea) - the notes' filter
    /// (umlauts and accents in, control codes out, as IMGUI's TextArea), line breaks from Enter and from
    /// a paste, the cursor, Backspace and Delete across a line break, a loaded text kept byte for byte.
    /// Data-free.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckTextArea()
        {
            Check.Group("Kit text area (UWTextArea - cleanup step 8b.3)");

            UWTextArea lOArea = new UWTextArea();

            lOArea.Set("Door\r\nkey");
            Check.Str("area: a loaded text stays as it was", lOArea.Text, "Door\r\nkey");
            Check.Int("area: the cursor starts after the text", lOArea.Caret, 9);

            lOArea.Set("Lever");
            Check.Bool("area: a letter goes in", lOArea.Insert('s'), true);
            lOArea.NewLine();
            Check.Bool("area: an umlaut goes in", lOArea.Insert('\u00e4'), true);
            Check.Str("area: Enter breaks the line", lOArea.Text, "Levers\n\u00e4");
            Check.Bool("filter: a typed line break is refused", lOArea.Insert('\n'), false);
            Check.Bool("filter: a tab is refused", lOArea.Insert('\t'), false);
            Check.Bool("filter: a carriage return is refused", lOArea.Insert('\r'), false);
            Check.Bool("filter: DEL is refused", lOArea.Insert('\u007f'), false);
            Check.Bool("filter: half a surrogate pair is refused", lOArea.Insert('\ud83d'), false);
            Check.Bool("filter: an accent is typeable", UWTextArea.IsTypeable('\u00e9'), true);
            Check.Bool("filter: the tilde is typeable", UWTextArea.IsTypeable('~'), true);

            lOArea.Left();
            Check.Bool("area: Backspace takes the line break", lOArea.Backspace(), true);
            Check.Str("area: the lines join", lOArea.Text, "Levers\u00e4");
            Check.Int("area: the cursor stays before the umlaut", lOArea.Caret, 6);
            Check.Bool("area: Delete takes the one after", lOArea.Delete(), true);
            Check.Str("area: the umlaut went", lOArea.Text, "Levers");

            lOArea.MoveTo(0);
            Check.Bool("area: Backspace at the start does nothing", lOArea.Backspace(), false);
            lOArea.MoveTo(99);
            Check.Int("area: a place beyond the end stops at it", lOArea.Caret, 6);
            Check.Bool("area: Delete at the end does nothing", lOArea.Delete(), false);
            lOArea.MoveTo(-3);
            Check.Int("area: a place before the start stops at it", lOArea.Caret, 0);

            int liPasted = lOArea.Paste("Room 3\r\nstair\tdown\r");
            Check.Int("paste: the tab is dropped, the breaks kept", liPasted, 17);
            Check.Str("paste: at the cursor, one break for CR LF", lOArea.Text, "Room 3\nstairdown\nLevers");
            Check.Int("paste: the cursor after it", lOArea.Caret, 17);

            lOArea.Set(null);
            Check.Str("area: nothing is an empty text", lOArea.Text, string.Empty);
        }
    }
}
