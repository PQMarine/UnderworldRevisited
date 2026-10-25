using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE ESCAPE AND BACK CHAIN, THE UNITY SIDE (cleanup step 7.5, 2026-10-25): every consumer of Escape
/// and the pad's B asks Takes with its owner where it read the key itself before; UWBackChain decides.
/// Once a frame, at the first question after a press: the keys that came (the Escape key, the Menu
/// and PadBack acts of the player's controls, the pad's B by its place) and the states the chain asks
/// about, tested then - the state when the press came. A frame without a back press tests nothing.
/// </summary>
public static class UWBack
{
    private static int msiFrame = -1;

    private static UWBackKey meKeys;

    private static UWBackState meStates;

    /// <summary>Whether this frame's back press is the owner's - by any of its keys.</summary>
    public static bool Takes(UWBackOwner peOwner)
    {
        fDecide();

        return meKeys != UWBackKey.None && UWBackChain.Takes(peOwner, meKeys, meStates);
    }

    /// <summary>Whether this frame's back press is the owner's and came by one of peKeys (a place
    /// that reads the keyboard's Escape and the pad's B in different branches).</summary>
    public static bool Takes(UWBackOwner peOwner, UWBackKey peKeys)
    {
        fDecide();

        return (meKeys & peKeys) != 0 && UWBackChain.Takes(peOwner, meKeys & peKeys, meStates);
    }

    /// <summary>The keys of this frame and, when any came, the states - once a frame.</summary>
    private static void fDecide()
    {
        if (msiFrame == Time.frameCount)
            return;

        msiFrame = Time.frameCount;
        meKeys = fKeys();
        meStates = meKeys != UWBackKey.None ? fStates() : UWBackState.None;
    }

    private static UWBackKey fKeys()
    {
        UWBackKey leKeys = UWBackKey.None;
        Keyboard lOKeyboard = Keyboard.current;
        Gamepad lOPad = Gamepad.current;

        if (lOKeyboard != null && lOKeyboard.escapeKey.wasPressedThisFrame)
            leKeys |= UWBackKey.Escape;

        if (UWInput.Pressed(UWAct.Menu))
            leKeys |= UWBackKey.Menu;

        if (UWInput.Pressed(UWAct.PadBack))
            leKeys |= UWBackKey.PadBack;

        // The pad's B by its place (the letter grid), and the PadBack binding's button read on the pad
        // (cleanup step 7.6) - looked up only in a frame with a pad press.
        if (lOPad != null && lOPad.buttonEast.wasPressedThisFrame)
            leKeys |= UWBackKey.PadEast;

        if (lOPad != null && UWGamepad.AnyPressed(false) && UWInput.PadPressed(UWAct.PadBack, 0, "<Gamepad>/buttonEast"))
            leKeys |= UWBackKey.PadButton;

        return leKeys;
    }

    /// <summary>The states the chain asks about, as the places that read the keys tested them.</summary>
    private static UWBackState fStates()
    {
        UWBackState leStates = UWBackState.None;
        UWGameUI lOUi = UWScene.GameUi;
        Interaction lOInteraction = lOUi != null ? lOUi.fFindInteraction() : null;

        fSet(ref leStates, UWBackState.Cutscene, UWScene.IntroPlayer != null && UWScene.IntroPlayer.HoldsKeys);
        fSet(ref leStates, UWBackState.RoamingSight, UWRoamingSight.IsAnyRunning);
        fSet(ref leStates, UWBackState.DebugTools, UWDebugTools.IsTyping);
        fSet(ref leStates, UWBackState.SetupEffects, UWSetupMenu.IsEffectsScreenOpen);
        fSet(ref leStates, UWBackState.SetupCapturing, UWSetupMenu.IsCapturingInput);
        fSet(ref leStates, UWBackState.SetupPanel, UWSetupMenu.HasOpenPanel);
        fSet(ref leStates, UWBackState.KitText, UWKitRows.IsTyping);
        fSet(ref leStates, UWBackState.HelpKeys, UWHelpWindow.BlocksGameKeys);
        fSet(ref leStates, UWBackState.LetterGrid, UWLetterGrid.IsShown);
        fSet(ref leStates, UWBackState.CharacterName, UWCharacterCreationScreen.IsTypingName);
        fSet(ref leStates, UWBackState.MapNote, lOUi != null && lOUi.Map.IsWritingNote);
        fSet(ref leStates, UWBackState.ClassicSaveName, lOUi != null && lOUi.Options.miSaveSlotTyping > 0);
        fSet(ref leStates, UWBackState.Instrument, UWInstrumentPlayer.IsPlaying);
        fSet(ref leStates, UWBackState.ConversationAnswer, UWConversationController.IsTypingAnswer);
        fSet(ref leStates, UWBackState.PromptLine, lOInteraction != null && lOInteraction.IsPromptActive);
        fSet(ref leStates, UWBackState.Mantra, lOInteraction != null && lOInteraction.IsChanting);
        fSet(ref leStates, UWBackState.BagMenu, UWModernBags.IsMenuOpen);
        fSet(ref leStates, UWBackState.BagSplit, UWModernBags.IsSplitting);
        fSet(ref leStates, UWBackState.ModernSaveName, UWModernGameMenu.IsTypingName);
        fSet(ref leStates, UWBackState.ScreenMenu, UWScreenUi.IsScreenMenuOpen);
        fSet(ref leStates, UWBackState.Conversation, UWConversationController.IsAnyOpen);
        fSet(ref leStates, UWBackState.MapVisible, lOUi != null && lOUi.Map.IsMapVisible);
        fSet(ref leStates, UWBackState.MenuOpen, UWModernUi.IsMenuOpen);
        fSet(ref leStates, UWBackState.ModernClosable, UWModernUi.HasClosable);

        // The owners that are up and keep the press from those below (cleanup 7, wave 2b).
        fSet(ref leStates, UWBackState.Rebind, UWKeyBindings.HoldsKeys);
        fSet(ref leStates, UWBackState.SetupKeyNav, UWSetupMenu.IsKeyNavOn);
        fSet(ref leStates, UWBackState.LayoutAsk, UWModernLayoutEditor.IsAskingLeave);
        fSet(ref leStates, UWBackState.Paging, lOInteraction != null && lOInteraction.PagingTakesKeys);
        fSet(ref leStates, UWBackState.WindowPicture, lOInteraction != null && lOInteraction.PictureTakesKeys);

        return leStates;
    }

    private static void fSet(ref UWBackState peStates, UWBackState peState, bool pbHolds)
    {
        if (pbHolds)
            peStates |= peState;
    }
}
