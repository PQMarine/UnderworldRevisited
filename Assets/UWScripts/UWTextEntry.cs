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
/// 0x7E) to the owner's handler. Backspace and Enter are read in one place as well, Escape in the
/// Escape chain since cleanup step 7.5 (UWBack.Takes with the place's owner); the
/// order in which a place looks at them and what it does with them stays its own (a save name
/// confirms by mouse too, the map closes on Escape, the conversation answers empty), as do its
/// length and width limits - most of them are widths in the original's font, measured by the place.
///
/// THE COUNTS (cleanup step 7.1, 2026-10-25): the classic "Move how many?" prompt (UWCountPrompt) and
/// the modern split box (UWModernBagSplit) read their digits here too, from Digits: by the KEY'S
/// PLACE, the digit row and the keypad, as they always did - not from the text events, which would
/// want Shift for a digit on a French keyboard, give a numpad digit only with Num Lock on, and turn
/// Shift and a digit into a sign. So no text events are hooked for them; Backspace and Enter they read
/// here as the others do, Escape through the chain.
///
/// Every place that takes typed text (cleanup step 7.1): the character's name, the save name in
/// both schemes (the modern one and the folder path and the debug panel's search and jump fields
/// through the kit's text field, UWKitRows.TextField), the mantra, the conversation's answer, the map
/// note, the help's notes (UWKitRows.TextArea, its own wider filter), the two counts (Digits). The
/// pad's letter grid (UWLetterGrid) types real keys (UWOsKeys), so it reaches whichever is open.
/// </summary>
public sealed class UWTextEntry
{
    private readonly Action<char> mFOnCharacter;

    /// <summary>The filter of this entry: the one filter (IsTypeable), or the help notes' own wider one
    /// (UWTextArea.IsTypeable, cleanup 8b.3, 2026-10-24: IMGUI's TextArea took umlauts and accents).</summary>
    private readonly Func<char, bool> mFFilter;

    private Keyboard mOHooked;

    /// <summary>The digits by the key's place, no text events (Digits).</summary>
    private readonly bool mbByKeyPlace;

    private bool mbKeyPlaceOpen;

    /// <summary>pFOnCharacter receives every typed character that passes the filter.</summary>
    public UWTextEntry(Action<char> pFOnCharacter)
        : this(pFOnCharacter, IsTypeable)
    {
    }

    /// <summary>pFOnCharacter receives every typed character that passes pFFilter.</summary>
    public UWTextEntry(Action<char> pFOnCharacter, Func<char, bool> pFFilter)
        : this(pFOnCharacter, pFFilter, false)
    {
    }

    private UWTextEntry(Action<char> pFOnCharacter, Func<char, bool> pFFilter, bool pbByKeyPlace)
    {
        mFOnCharacter = pFOnCharacter;
        mFFilter = pFFilter ?? IsTypeable;
        mbByKeyPlace = pbByKeyPlace;
    }

    /// <summary>An entry of digits, '0' to '9' to pFOnDigit, read by the key's place - the digit row
    /// and the keypad, whatever the layout and Num Lock say (the counts, see the class comment).</summary>
    public static UWTextEntry Digits(Action<char> pFOnDigit)
    {
        return new UWTextEntry(pFOnDigit, IsDigit, true);
    }

    /// <summary>The counts' filter.</summary>
    public static bool IsDigit(char pcChar)
    {
        return pcChar >= '0' && pcChar <= '9';
    }

    /// <summary>Whether the text events are hooked (between Begin and End); for Digits, between
    /// Begin and End.</summary>
    public bool IsOpen => mbByKeyPlace ? mbKeyPlaceOpen : mOHooked != null;

    /// <summary>Starts listening to the text events; a second call changes nothing. The key
    /// fallback hooks its own listener here too, so that a text event in the very next frame
    /// already tells it that the events work.</summary>
    public void Begin()
    {
        if (mbByKeyPlace)
        {
            mbKeyPlaceOpen = true;
            return;
        }

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
        mbKeyPlaceOpen = false;

        if (mOHooked == null)
            return;

        mOHooked.onTextInput -= fOnTextInput;
        mOHooked = null;
    }

    /// <summary>Where no text events arrive (Linux), the keys themselves (UWTypedKeys): call once a
    /// frame while the entry is open, after Backspace. For Digits the only source: each digit whose key
    /// went down this frame, on the digit row or the keypad, once, from 0 to 9.</summary>
    public void ReadKeys()
    {
        if (mbByKeyPlace)
        {
            fReadDigitKeys();
            return;
        }

        foreach (char lcChar in UWTypedKeys.ReadTyped())
            fOnTextInput(lcChar);
    }

    private static readonly Key[] msDigitKeys =
    {
        Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    private static readonly Key[] msNumpadKeys =
    {
        Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
        Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
    };

    private void fReadDigitKeys()
    {
        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null)
            return;

        for (int liDigit = 0; liDigit <= 9; liDigit++)
        {
            if (lOKeyboard[msDigitKeys[liDigit]].wasPressedThisFrame || lOKeyboard[msNumpadKeys[liDigit]].wasPressedThisFrame)
                fOnTextInput((char)('0' + liDigit));
        }
    }

    /// <summary>The one filter: printable ASCII only, no control codes (UWTextLine's since cleanup
    /// 8b.1, 2026-10-24, so the kit's engine-free text line keeps the same one).</summary>
    public static bool IsTypeable(char pcChar)
    {
        return UWDataImport.UWData.UWTextLine.IsTypeable(pcChar);
    }

    /// <summary>Backspace went down this frame.</summary>
    public static bool BackspacePressed => Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame;

    /// <summary>Enter or the keypad's Enter went down this frame.</summary>
    public static bool EnterPressed => Keyboard.current != null
        && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);

    // Escape: each text entry asks the Escape chain since cleanup step 7.5 (UWBack.Takes with its owner;
    // EscapePressed here until then), so that one press has one owner as before.

    private void fOnTextInput(char pcChar)
    {
        if (mFFilter(pcChar))
            mFOnCharacter(pcChar);
    }
}
