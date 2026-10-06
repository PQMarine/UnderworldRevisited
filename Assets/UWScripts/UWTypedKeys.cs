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
/// bar a blank. Enough for a name, a mantra, an answer and a save name - the typing sites
/// feed the result to their text handlers (fOnTextInput and the like), which filter as before.
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

        if (msHooked != lOKeyboard)
        {
            if (msHooked != null)
                msHooked.onTextInput -= fOnTextInput;

            lOKeyboard.onTextInput += fOnTextInput;
            msHooked = lOKeyboard;
        }

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

            if (lcChar < 0x20 || lcChar > 0x7E)
                continue;

            if (char.IsLetter(lcChar))
                lcChar = lbShift ? char.ToUpperInvariant(lcChar) : char.ToLowerInvariant(lcChar);

            msBuffer.Append(lcChar);
        }

        return msBuffer.ToString();
    }

    private static void fOnTextInput(char pcChar)
    {
        miLastTextFrame = Time.frameCount;
    }
}
