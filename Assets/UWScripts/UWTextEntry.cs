using System;
using UnityEngine.InputSystem;

/// <summary>
/// THE ONE TEXT INPUT (cleanup step 5.11, 2026-10-19) for the five places that take typed text:
/// the map note (UWHudMap), the save game's name (UWHudOptions), the conversation's typed answer
/// (UWConversationController), the shrine's mantra (Interaction) and the character's name
/// (UWCharacterCreationScreen). Each hooked and unhooked Keyboard.onTextInput itself, filtered the
/// characters in its own words and called the key fallback on its own - and the map note never
/// called it, so map notes could not be typed on Linux, where onTextInput stays silent (B26 of the
/// cleanup plan).
///
/// Here: the text events while the entry is open, plus the keys themselves where no text event
/// comes (UWTypedKeys, ReadKeys once a frame), both through ONE filter (printable ASCII, 0x20 to
/// 0x7E) to the owner's handler. Backspace, Enter and Escape are read in one place as well; the
/// order in which a place looks at them and what it does with them stays its own (a save name
/// confirms by mouse too, the map closes on Escape, the conversation answers empty), as do its
/// length and width limits.
/// </summary>
public sealed class UWTextEntry
{
    private readonly Action<char> mFOnCharacter;

    private Keyboard mOHooked;

    /// <summary>pFOnCharacter receives every typed character that passes the filter.</summary>
    public UWTextEntry(Action<char> pFOnCharacter)
    {
        mFOnCharacter = pFOnCharacter;
    }

    /// <summary>Whether the text events are hooked (between Begin and End).</summary>
    public bool IsOpen => mOHooked != null;

    /// <summary>Starts listening to the text events; a second call changes nothing. The key
    /// fallback hooks its own listener here too, so that a text event in the very next frame
    /// already tells it that the events work.</summary>
    public void Begin()
    {
        UWTypedKeys.Hook();

        if (mOHooked != null)
            return;

        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null)
            return;

        lOKeyboard.onTextInput += fOnTextInput;
        mOHooked = lOKeyboard;
    }

    /// <summary>Stops listening.</summary>
    public void End()
    {
        if (mOHooked == null)
            return;

        mOHooked.onTextInput -= fOnTextInput;
        mOHooked = null;
    }

    /// <summary>Where no text events arrive (Linux), the keys themselves (UWTypedKeys): call once a
    /// frame while the entry is open, after Backspace.</summary>
    public void ReadKeys()
    {
        foreach (char lcChar in UWTypedKeys.ReadTyped())
            fOnTextInput(lcChar);
    }

    /// <summary>The one filter: printable ASCII only, no control codes.</summary>
    public static bool IsTypeable(char pcChar)
    {
        return pcChar >= 0x20 && pcChar <= 0x7E;
    }

    /// <summary>Backspace went down this frame.</summary>
    public static bool BackspacePressed => Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame;

    /// <summary>Enter or the keypad's Enter went down this frame.</summary>
    public static bool EnterPressed => Keyboard.current != null
        && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);

    /// <summary>Escape went down this frame.</summary>
    public static bool EscapePressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

    private void fOnTextInput(char pcChar)
    {
        if (IsTypeable(pcChar))
            mFOnCharacter(pcChar);
    }
}
