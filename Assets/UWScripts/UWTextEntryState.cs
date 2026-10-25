using UWDataImport.UWData;

/// <summary>
/// WHO HAS THE KEYS WHILE TEXT IS TYPED (cleanup 5.18, 2026-10-23: one state with one query, out
/// of UWGameUI and UWControls, the answers as they were). Text is typed in many places - a map
/// note (UWHudMap), a save game's name in the classic options (UWHudOptions) or the modern menu
/// (UWModernGameMenu), a conversation's answer (UWConversationController), the character's name
/// (UWCharacterCreationScreen), a mantra, the "how many" prompt and the input line (Interaction) -
/// and some things hold the keys like typing: the instrument, the help window's notes, the bags'
/// split box and menu. Everyone who must stay silent meanwhile asks here:
///
///   - IsTextEntryActive: letter keys belong to the text - F does not toggle a font, J does not
///     jump (UWPlayerInput, UWPlayerLook, UWGameUI's key routing);
///   - LocksKeys: the same, or the debug console's input line - what UWGameUI pushes into
///     UWControls.SetTextEntryActive once a frame: the TextEntry input context, which silences its
///     actions on every live UWControls (UWInputContexts.TypingSilenced and the whole debug map);
///   - IsTypingText: text in the narrow sense, for the gamepad's letter grid (UWLetterGrid).
///
/// THE INPUT CONTEXTS (cleanup step 7.4, 2026-10-25): this class only gathers who is typing now
/// (Sources, one UWTypingSource per place); the three answers and the lock's set of actions are the
/// engine-free UWInputContexts', the rules as they stood here (the lock's set recorded before the
/// move, Tools/Fixtures/input/typing-lock.expected.txt).
/// </summary>
public sealed class UWTextEntryState
{
    public UWTextEntryState(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private readonly UWGameUI mOUi;

    /// <summary>Who types or holds the keys now, one flag per place (asked anew each time, as the
    /// three answers were).</summary>
    public UWTypingSource Sources
    {
        get
        {
            Interaction lOInteraction = mOUi.fFindInteraction();
            UWTypingSource leSources = UWTypingSource.None;

            if (mOUi.Map.IsWritingNote)
                leSources |= UWTypingSource.MapNote;

            if (mOUi.Options.miSaveSlotTyping > 0)
                leSources |= UWTypingSource.ClassicSaveName;

            if (UWModernGameMenu.IsTypingName)
                leSources |= UWTypingSource.ModernSaveName;

            if (UWConversationController.IsTypingAnswer)
                leSources |= UWTypingSource.ConversationAnswer;

            if (UWCharacterCreationScreen.IsTypingName)
                leSources |= UWTypingSource.CharacterName;

            // A text field of the windows on the kit (UWKitRows, cleanup 8b.1, 2026-10-24): the save
            // name and the folder path type there once their windows have moved.
            if (UWKitRows.IsTyping)
                leSources |= UWTypingSource.KitField;

            if (UWInstrumentPlayer.IsPlaying)
                leSources |= UWTypingSource.Instrument;

            if (UWHelpWindow.BlocksGameKeys)
                leSources |= UWTypingSource.HelpKeys;

            if (UWModernBags.IsSplitting)
                leSources |= UWTypingSource.BagSplit;

            if (UWModernBags.IsMenuOpen)
                leSources |= UWTypingSource.BagMenu;

            if (lOInteraction != null && lOInteraction.IsChanting)
                leSources |= UWTypingSource.Mantra;

            if (lOInteraction != null && lOInteraction.IsPromptActive)
                leSources |= UWTypingSource.Prompt;

            // Not the yes/no question (answered by A and B, Interaction) nor the count prompt
            // (the d-pad turns the number, UWItemDrag; per user, 2026-10-07: no grid there).
            if (lOInteraction != null && lOInteraction.ShowsPromptTextCursor
                && !(UWScene.ItemDrag != null && UWScene.ItemDrag.IsAskingCount))
                leSources |= UWTypingSource.PromptTyped;

            // The debug tools' tab open (UWDebugTools.IsTyping: its jump line and spell search).
            if (UWDebugTools.IsTyping)
                leSources |= UWTypingSource.DebugTools;

            return leSources;
        }
    }

    /// <summary>The lock UWGameUI pushes into UWControls once a frame: text entry anywhere, or the
    /// debug tools' tab open (UWInputContexts.LocksKeys).</summary>
    public bool LocksKeys => UWInputContexts.LocksKeys(Sources);

    /// <summary>Is typing going on anywhere - map note, save game name, answer in
    /// conversation, name, mantra, help notes or an input line? Then letter keys belong to the
    /// text: F does not toggle a font, J does not jump (UWInputContexts.IsTextEntry).</summary>
    public bool IsTextEntryActive => UWInputContexts.IsTextEntry(Sources);

    /// <summary>
    /// Text is being typed - a map note, a save game's name, a mantra or the input line, a
    /// conversation's answer - unlike IsTextEntryActive, which also holds the keys for the help,
    /// the instrument, the split box and the bags' menu. The gamepad's letter grid comes up for
    /// these (UWLetterGrid; the character's name it asks of the creation screen itself;
    /// UWInputContexts.IsTypingText).
    /// </summary>
    public bool IsTypingText => UWInputContexts.IsTypingText(Sources);
}
