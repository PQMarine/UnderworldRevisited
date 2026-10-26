using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Typed characters when the Input System delivers none. On Linux (the user's Kubuntu VM,
/// 2026-10-06) Keyboard.onTextInput never fired, so the name in the character creation could
/// not be typed; the key presses themselves arrive. This reads the keys pressed in the frame
/// and turns them into characters by the key's display name (the layout's own label), but only
/// while no text event has come in this or the previous frame - where text events work
/// (Windows), they alone count, else every letter would arrive twice.
///
/// Letters follow Shift, digits and the punctuation keys give their unshifted label, the space
/// bar a blank. Enough for a name, a mantra, an answer, a save name and a map note - UWTextEntry
/// feeds the result through its one filter to the typing places' handlers (since 2026-10-19;
/// until then each place called it itself, and the map note did not, B26).
/// </summary>
public static class UWTypedKeys
{
    private static Keyboard msHooked;
    private static int miLastTextFrame = -10;
    private static readonly StringBuilder msBuffer = new StringBuilder();

    /// <summary>The characters typed this frame on the keys, or an empty string while the text
    /// events do their work.</summary>
    public static string ReadTyped()
    {
        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null)
            return string.Empty;

        Hook();

        int liFrame = Time.frameCount;

        // A text event in this frame or the one before: the events work, the keys stay quiet.
        if (liFrame - miLastTextFrame <= 1)
            return string.Empty;

        // Ctrl and Alt combinations are shortcuts, not text.
        if (lOKeyboard.ctrlKey.isPressed || lOKeyboard.altKey.isPressed)
            return string.Empty;

        msBuffer.Length = 0;

        bool lbShift = lOKeyboard.shiftKey.isPressed;

        foreach (KeyControl lOKey in lOKeyboard.allKeys)
        {
            if (!lOKey.wasPressedThisFrame)
                continue;

            if (lOKey.keyCode == Key.Space)
            {
                msBuffer.Append(' ');
                continue;
            }

            string lsName = lOKey.displayName;

            if (string.IsNullOrEmpty(lsName) || lsName.Length != 1)
                continue;

            char lcChar = lsName[0];

            // Printable ASCII, and the language's letters where the layout labels a key with one (the
            // German umlauts, translation wave 2; UWTextLine.LanguageLetters).
            if ((lcChar < 0x20 || lcChar > 0x7E) && !UWDataImport.UWData.UWTextLine.IsLanguageLetter(char.ToLowerInvariant(lcChar))
                && !UWDataImport.UWData.UWTextLine.IsLanguageLetter(lcChar))
                continue;

            if (char.IsLetter(lcChar))
                lcChar = lbShift ? char.ToUpperInvariant(lcChar) : char.ToLowerInvariant(lcChar);

            msBuffer.Append(lcChar);
        }

        return msBuffer.ToString();
    }

    /// <summary>Listens to the current keyboard's text events (again after a device change).
    /// UWTextEntry.Begin calls it, so the events are heard before the first key is read.</summary>
    public static void Hook()
    {
        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null || msHooked == lOKeyboard)
            return;

        if (msHooked != null)
            msHooked.onTextInput -= fOnTextInput;

        lOKeyboard.onTextInput += fOnTextInput;
        msHooked = lOKeyboard;
    }

    private static void fOnTextInput(char pcChar)
    {
        miLastTextFrame = Time.frameCount;
    }
}
