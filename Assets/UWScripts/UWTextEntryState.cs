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
///     UWControls.SetTextEntryActive once a frame, which disables the LISTED ACTIONS below on every
///     live UWControls (the hand-written lock list, moved here verbatim) and the whole debug map;
///   - IsTypingText: text in the narrow sense, for the gamepad's letter grid (UWLetterGrid).
///
/// This is not yet the UWInputContext stack of section 8.5 of the cleanup plan (World, UiModal,
/// TextEntry, PadPointer, Cutscene) - that is a later phase; this class only gives the present
/// rule one home.
/// </summary>
public sealed class UWTextEntryState
{
    public UWTextEntryState(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private readonly UWGameUI mOUi;

    /// <summary>The Player map's actions that stay silent while typing: everything tied to the
    /// keyboard (per user, 2026-09-13: "during text input while chanting a mantra, switch off
    /// the special keys. B N F and whatever else comes along"). WalkSlow (Left Shift) and
    /// EasyModifier (Left Alt) are in the list (issue 4, 2026-10-21). Mouse and clicks remain.
    /// UWControls.fApplyTextEntry walks the list.</summary>
    public static readonly string[] LockedPlayerActions =
    {
        "Move", "ToggleScheme", "ToggleInventory", "HoverHeight", "Turn", "Strafe", "Jump",
        "LookDown", "LookReset", "LookUp", "ToggleMap", "ToggleFont", "KeyOptions", "KeyTalk",
        "KeyGet", "KeyLook", "KeyFight", "KeyUse", "KeyPanel", "KeyCast", "KeyTrack",
        "KeyCamp", "OptSave", "OptRestore", "OptMusic", "OptSound", "OptDetail", "OptQuit",
        "EasyForward", "EasyWalkForward", "EasyBack", "EasyTurnLeft", "EasyTurnRight",
        "ModernUse", "ModernReady", "WalkSlow", "EasyModifier", "ModernHover", "Menu",
        "ModernBags", "ModernLook", "ModernRunes", "ModernSlot1", "ModernSlot2", "ModernSlot3",
        "ModernSlot4", "ModernSlot5", "ModernSlot6", "ModernSlot7", "ModernSlot8",
        "ModernSlot9", "ModernSlot10"
    };

    /// <summary>The Noclip map's actions that stay silent while typing.</summary>
    public static readonly string[] LockedNoclipActions =
    {
        "Move", "Up", "Down", "Sprint"
    };

    /// <summary>The lock UWGameUI pushes into UWControls once a frame: text entry anywhere, or the
    /// debug console's input line (UWDebugTools.IsTyping).</summary>
    public bool LocksKeys => IsTextEntryActive || UWDebugTools.IsTyping;

    /// <summary>Is typing going on anywhere - map note, save game name, answer in
    /// conversation, name, mantra, help notes or an input line? Then letter keys belong to the
    /// text: F does not toggle a font, J does not jump.</summary>
    public bool IsTextEntryActive
    {
        get
        {
            Interaction lOInteraction = mOUi.fFindInteraction();

            return mOUi.Map.IsWritingNote || mOUi.Options.miSaveSlotTyping > 0 || UWModernGameMenu.IsTypingName || UWConversationController.IsTypingAnswer
                || UWCharacterCreationScreen.IsTypingName
                || UWInstrumentPlayer.IsPlaying || UWHelpWindow.BlocksGameKeys || UWModernBags.IsSplitting || UWModernBags.IsMenuOpen
                || (lOInteraction != null && (lOInteraction.IsChanting || lOInteraction.IsPromptActive));
        }
    }

    /// <summary>
    /// Text is being typed - a map note, a save game's name, a mantra or the input line, a
    /// conversation's answer - unlike IsTextEntryActive, which also holds the keys for the help,
    /// the instrument, the split box and the bags' menu. The gamepad's letter grid comes up for
    /// these (UWLetterGrid; the character's name it asks of the creation screen itself).
    /// </summary>
    public bool IsTypingText
    {
        get
        {
            Interaction lOInteraction = mOUi.fFindInteraction();

            // Not the yes/no question (answered by A and B, Interaction) nor the count prompt
            // (the d-pad turns the number, UWItemDrag; per user, 2026-10-07: no grid there).
            bool lbCountPrompt = UWScene.ItemDrag != null && UWScene.ItemDrag.IsAskingCount;

            return mOUi.Map.IsWritingNote || mOUi.Options.miSaveSlotTyping > 0 || UWModernGameMenu.IsTypingName || UWConversationController.IsTypingAnswer
                || (lOInteraction != null && (lOInteraction.IsChanting || (lOInteraction.ShowsPromptTextCursor && !lbCountPrompt)));
        }
    }
}
