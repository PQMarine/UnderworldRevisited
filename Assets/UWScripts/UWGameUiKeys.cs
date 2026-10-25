using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// UWGAMEUI'S KEY ROUTING (cleanup step 7.5, 2026-10-25: the if-chains of fCheckFontToggle,
/// fCheckModernKeys, fCheckOptionShortcuts and fCheckOriginalKeys became the rows of UWHotkeys; this
/// is their gates and handlers). Routed once a frame from UWGameUI.Update, where the chains ran.
///
///   - Our own keys, both schemes: F switches all fonts between original and modern (see
///     UWTextLabel), M opens the map. Both come from the Player map since 2026-09-17, so they can be
///     changed in the menu bar (UWKeyBindings). Not while typing anywhere - map note, save game name,
///     mantra, input line - there the letters belong to the text. In the main menu and in creation
///     the keys stay completely silent (per user, 2026-09-11), and while a sequence plays - or the key
///     that ended it is still down in this frame (UWIntroPlayer.HoldsKeys, 2026-10-13). Ctrl+F and
///     Ctrl+M are the original's option shortcuts (sound and music, with Ctrl+S, R, D and Q; per user
///     in the original, 2026-09-26: it is Ctrl, Alt does nothing) - not ours.
///   - THE MODERN SCHEME has no classic frame: the options, the map and the command icons live on it,
///     and opened there they were invisible and held the game (found 2026-10-03). There F1, Escape and
///     the Ctrl shortcuts open the modern game menu (UWModernGameMenu); F9 and F10 stay. THE RIGHT
///     CTRL ONLY: the left one sinks in the modern scheme (per user, 2026-10-04) - held while walking
///     back it would open the save page with S, with R the restore page.
///   - THE ORIGINAL'S CTRL SHORTCUTS (built 2026-09-26, see UWHudOptions.ChooseByShortcut): Ctrl+S
///     save, Ctrl+R restore, Ctrl+M music, Ctrl+F sound, Ctrl+D detail, Ctrl+Q quit. Normal play only,
///     as the original registers them; not while the view roams. Partway through an action the
///     options refuse to open (UWHudOptions.RefusesToOpen); on level 9 they open, only the Save entry
///     refuses (RefusesToSave).
///   - THE ORIGINAL'S FUNCTION KEYS (built 2026-09-26, see UWControls.KeyOptions): F1 options, F2 to
///     F6 the command modes as a click on their icon, F7 turns the panel, F8 casts, F9 tracks
///     (UWMiscSpellRules.Track, since 2026-09-27), F10 makes camp. Not with Shift (our own keys) nor
///     Alt (Alt+F4) nor Ctrl; not in a conversation - the original registers them for its normal mode
///     only -, not over the map and not while the options panel is up. WHILE THE VIEW ROAMS only F7
///     turns the panel - in the original too, as does the chain (per user, 2026-09-27: F1 to F6 and
///     F8 are refused there). F9 and F10 are refused as a DELIBERATE DEVIATION (decided per user the
///     same day): the original still tracks and makes camp, and sleeping leaves the spell gone from
///     the bar but the view stuck.
/// </summary>
internal sealed class UWGameUiKeys : UWHotkeyHost
{
    private readonly UWGameUI mOUi;

    public UWGameUiKeys(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    protected override UWControls Controls => mOUi.mControlSchemeRef != null ? mOUi.mControlSchemeRef.Controls : null;

    /// <summary>
    /// THE MODERN SCHEME'S BACK PRESS: Escape and Start (Menu), and the gamepad's B, which does what
    /// Escape does while something is open - not while the letter grid, the bags' menu or the split
    /// box have it. Escape first lets a waiting spell go (UWHudRunes.fAfterModernCast), then closes the
    /// bags (UWModernBags), the character panel (UWModernPanel; a pinned one only leaves its Help tab)
    /// and the rune panel (UWModernRunePanel); the menu comes after. The order is UWModernUi's closable
    /// chain since 2026-10-22; whose press it is, the map's, the chain's or the menu's, the Escape
    /// chain says since cleanup step 7.5 (UWBackChain: until then this routing worked it out, the pad's
    /// B through PadBackCloses).
    /// </summary>
    public override bool Fired(UWHotkeyRow pORow)
    {
        if (pORow.Trigger != UWHotkeyTrigger.Back)
            return base.Fired(pORow);

        return Controls != null && UWBack.Takes(pORow.BackOwner);
    }

    public override bool Gate(UWHotkeyGate peGate, UWHotkeyRow pORow)
    {
        switch (peGate)
        {
            case UWHotkeyGate.Typing: return mOUi.TextEntry.IsTextEntryActive;
            case UWHotkeyGate.ScreenMenu: return UWScreenUi.IsScreenMenuOpen;
            case UWHotkeyGate.Cutscene: return UWScene.IntroPlayer != null && UWScene.IntroPlayer.HoldsKeys;
            case UWHotkeyGate.ModernHud: return UWModernHud.Instance != null;
            case UWHotkeyGate.Conversation: return UWConversationController.IsAnyOpen;
            case UWHotkeyGate.Roaming: return UWRoamingSight.IsAnyRunning;
            case UWHotkeyGate.RoamingEndedByEscape: return UWRoamingSight.EndedByEscapeThisFrame;
            case UWHotkeyGate.Interaction: return mOUi.mOInteraction != null;
            case UWHotkeyGate.MapVisible: return mOUi.Map.IsMapVisible;
            case UWHotkeyGate.MenuOpen: return UWModernHud.Instance != null && UWModernHud.Instance.IsOpen;
            case UWHotkeyGate.OptionsOpen: return mOUi.Options.IsOpen;
            case UWHotkeyGate.CanCamp: return UWGameActions.CanCamp;

            // The gamepad's pointer takes the d-pad as its wheel - except to close the map again.
            case UWHotkeyGate.PadPointerTakes: return pORow != null && UWInput.PadPointerTakes(UWInput.Action(Controls, pORow.Act));

            default: return false;
        }
    }

    public override bool Run(UWHotkeyHandler peHandler)
    {
        UWModernHud lOHud = UWModernHud.Instance;
        Interaction lOInteraction = mOUi.mOInteraction;

        switch (peHandler)
        {
            case UWHotkeyHandler.None:
                return true;

            case UWHotkeyHandler.ToggleFont:
                UWTextLabel.UseModernFont = !UWTextLabel.UseModernFont;
                return true;

            // The big map open in the modern scheme: M or Escape close it (writing a note, the keys
            // are the note's); the gamepad's B closes it too (UWGamepad).
            case UWHotkeyHandler.HideMap:
                mOUi.Map.HideMap();
                return true;

            case UWHotkeyHandler.ModernMenuSave:
                lOHud.Menu.OpenMenu(UWModernGameMenu.PageEnum.Save);
                return true;

            case UWHotkeyHandler.ModernMenuLoad:
                lOHud.Menu.OpenMenu(UWModernGameMenu.PageEnum.Load);
                return true;

            case UWHotkeyHandler.ModernMenuMain:
                lOHud.Menu.OpenMenu(UWModernGameMenu.PageEnum.Main);
                return true;

            // Something of the chain was closed; the menu waits for the next press.
            case UWHotkeyHandler.CloseChain:
                return UWModernUi.CloseNext();

            case UWHotkeyHandler.ToggleModernMenu:
                lOHud.Menu.ToggleMenu();
                return true;

            case UWHotkeyHandler.Track:
                mOUi.Actions.Track();
                return true;

            case UWHotkeyHandler.Camp:
                mOUi.Actions.Camp();
                return true;

            case UWHotkeyHandler.ToggleMap:
                mOUi.fToggleMapByKey();
                return true;

            // "Partway" and "between worlds" are refused by the options themselves.
            case UWHotkeyHandler.ShortcutSave:
                mOUi.Options.ChooseByShortcut(UWOptionsMenuModel.TopSave);
                return true;

            case UWHotkeyHandler.ShortcutRestore:
                mOUi.Options.ChooseByShortcut(UWOptionsMenuModel.TopRestore);
                return true;

            case UWHotkeyHandler.ShortcutMusic:
                mOUi.Options.ChooseByShortcut(UWOptionsMenuModel.TopMusic);
                return true;

            case UWHotkeyHandler.ShortcutSound:
                mOUi.Options.ChooseByShortcut(UWOptionsMenuModel.TopSound);
                return true;

            case UWHotkeyHandler.ShortcutDetail:
                mOUi.Options.ChooseByShortcut(UWOptionsMenuModel.TopDetail);
                return true;

            case UWHotkeyHandler.ShortcutQuit:
                mOUi.Options.ChooseByShortcut(UWOptionsMenuModel.TopQuit);
                return true;

            case UWHotkeyHandler.TurnPanel:
                mOUi.Panel.TogglePanelSide();
                return true;

            case UWHotkeyHandler.OpenOptions:
                mOUi.Options.OpenByKey();
                return true;

            case UWHotkeyHandler.ModeTalk:
                lOInteraction.SetCommandMode(UWCommandMode.Talk);
                return true;

            case UWHotkeyHandler.ModeGet:
                lOInteraction.SetCommandMode(UWCommandMode.Get);
                return true;

            case UWHotkeyHandler.ModeLook:
                lOInteraction.SetCommandMode(UWCommandMode.Look);
                return true;

            case UWHotkeyHandler.ModeFight:
                lOInteraction.SetCommandMode(UWCommandMode.Fight);
                return true;

            case UWHotkeyHandler.ModeUse:
                lOInteraction.SetCommandMode(UWCommandMode.Use);
                return true;

            case UWHotkeyHandler.CastShelf:
                mOUi.Actions.CastShelf();
                return true;

            default:
                Debug.LogError("UWGameUiKeys: no handler " + peHandler);
                return false;
        }
    }
}
