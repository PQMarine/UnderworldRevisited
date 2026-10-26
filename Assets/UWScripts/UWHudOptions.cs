using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The HUD section "the options panel with save, restore, music, sound, detail and quit".
/// Its own class since 2026-09-18 (stage two of the HUD rebuild); the owner hands in the game
/// data, the frame, the character, the interaction and the inventory through mOUi.
///
/// THE STATE MACHINE IS UWOptionsMenuModel'S (engine-free, cleanup 5.18, 2026-10-23): the screens,
/// their pictures, the key highlight and what a choice does. This class draws the pictures,
/// reads the mouse and the keys, types the save game's name, holds time and input while the
/// panel is up, writes the messages and carries out the choices (the save, load and quit flows
/// of UWSaveFlow, shared with the modern game menu, UWSoundOptions, UWGraphicsDetail).
/// </summary>
public sealed class UWHudOptions
{
    private readonly UWGameUI mOUi;

    internal UWHudOptions(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    // ------------------------------------------------- Options panel and loading save games

    /// <summary>
    /// The options panel that opens in the left button bar in the original.
    ///
    /// ALL DIMENSIONS COME FROM THE REFERENCE SCENE (Underworld.tscn, stored there at four times
    /// the size and divided by four here) and match the picture sizes
    /// in OPTBTNS.GR: the background is 35 by 108 and sits at 4/10, the seven buttons
    /// are each 31 by 14 and stand from 5/12 on at a spacing of fifteen.
    ///
    /// THE BUTTON THAT OPENS IT comes from LFTI.GR - picture 0 is the idle one, picture 1 the
    /// pressed one. It is 28 by 17 in size and sits at 8/10. The remaining five buttons
    /// of that bar (talk, pick up, look, fight, use) do not exist in our version -
    /// the verbs run via the mouse buttons. Only mOUi one is needed.
    /// </summary>
    private const float optionsButtonLeft = UWClassicLayout.Options.ButtonLeft;

    private const float optionsButtonTop = UWClassicLayout.Options.ButtonTop;

    private const float optionsPanelLeft = UWClassicLayout.Options.PanelLeft;

    private const float optionsPanelTop = UWClassicLayout.Options.PanelTop;

    private const float optionsEntryLeft = UWClassicLayout.Options.EntryLeft;

    private const float optionsEntryTop = UWClassicLayout.Options.EntryTop;

    private const float optionsEntryPitch = UWClassicLayout.Options.EntryPitch;

    private const int optionsEntryCount = UWClassicLayout.Options.EntryCount;

    /// <summary>No picture at this slot (UWOptionsMenuModel.NoPicture).</summary>
    private const int optNone = UWOptionsMenuModel.NoPicture;

    /// <summary>The screens, their pictures, the key highlight, what a choice does.</summary>
    private readonly UWOptionsMenuModel mOModel = new UWOptionsMenuModel();

    /// <summary>Into which slot (1 to 4) a name is currently being typed, or 0 for none.
    /// </summary>
    internal int miSaveSlotTyping;

    private string msSaveTypedName = string.Empty;

    /// <summary>The original cuts off anything longer.</summary>
    private const int SaveNameMaxLength = 30;

    /// <summary>
    /// The save screen: heading, the four slots, cancel - the same layout as
    /// for loading, only with "Save Game" as heading (UWOptionsMenuModel.ShowSave).
    /// </summary>
    private void fShowSaveMenu()
    {
        miSaveSlotTyping = 0;
        mOModel.ShowSave();
        fApplyPage();
        fListSaveGames();
    }

    /// <summary>
    /// Starts entering the name.
    ///
    /// The line stands in the middle of the list at its own slot and starts with the name
    /// already there - for an empty slot thus with "&lt;not used yet&gt;". That is how
    /// the reference describes it, and that way a slot keeps its name if you only press
    /// Enter. Enter saves, Escape cancels.
    /// </summary>
    private void fBeginSaveName(int piSlot)
    {
        miSaveSlotTyping = piSlot;
        msSaveTypedName = UWSaveFlow.SlotName(piSlot);

        if (msSaveTypedName.Length > SaveNameMaxLength)
            msSaveTypedName = msSaveTypedName.Substring(0, SaveNameMaxLength);

        // The character must not start walking while typing - the same lock as for the
        // input line of the debug console.
        if (mOUi.mControlSchemeRef != null)
            mOUi.mControlSchemeRef.Controls.Disable();

        fSaveNameEntry().Begin();

        // As in the original (per user, 2026-09-12): the box is cleared, the green
        // prompt appears, below it ">" and the previous name with the blinking
        // text cursor of the quantity input. The pointer is hidden meanwhile.
        // The preset name is selected as a whole: the first typed character replaces
        // it, Backspace by contrast edits it (reference: SaveDescriptionPrompt).
        mbSaveNameFresh = true;

        if (mOUi.mOInteraction != null)
        {
            mOUi.mOInteraction.ResetMessages();
            mOUi.mOInteraction.AddGeneralMessage(SaveDescriptionPromptMessage);
        }

        UWPointer.IsShown = false;

        fRefreshSaveNamePrompt();
    }

    private bool mbSaveNameFresh;

    /// <summary>The name's typing (UWTextEntry since 2026-10-19: the text events, the keys where
    /// none arrive, one filter).</summary>
    private UWTextEntry mOSaveNameEntry;

    private UWTextEntry fSaveNameEntry()
    {
        if (mOSaveNameEntry == null)
            mOSaveNameEntry = new UWTextEntry(fOnSaveNameTextInput);

        return mOSaveNameEntry;
    }

    private void fEndSaveName()
    {
        fSaveNameEntry().End();

        if (mOUi.mControlSchemeRef != null)
            mOUi.mControlSchemeRef.Controls.Enable();

        miSaveSlotTyping = 0;

        if (mOUi.mOInteraction != null)
            mOUi.mOInteraction.EndPromptMessage();

        UWPointer.IsShown = true;
    }

    private void fOnSaveNameTextInput(char pcChar)
    {
        if (miSaveSlotTyping == 0)
            return;

        if (mbSaveNameFresh)
        {
            msSaveTypedName = string.Empty;
            mbSaveNameFresh = false;
        }

        if (msSaveTypedName.Length >= SaveNameMaxLength)
            return;

        msSaveTypedName += pcChar;

        fRefreshSaveNamePrompt();
    }

    /// <summary>The input line: ">" and the name; UWGameUI appends the text cursor to
    /// the running line while the question runs.</summary>
    private void fRefreshSaveNamePrompt()
    {
        if (mOUi.mOInteraction != null)
            mOUi.mOInteraction.SetPromptMessage(">" + msSaveTypedName);
    }

    /// <summary>
    /// The end of saving, as in the original (per user, 2026-09-12): ONLY the result in
    /// the top line - green "Save Game Succeeded.", red "Save Game Failed." (also on
    /// Escape), without "Saving Game" and dots (UWSaveFlow.ReportSave) - and back into the
    /// game, not into the save screen.
    /// </summary>
    private void fFinishSave(string psError, bool pbCancelled)
    {
        UWSaveFlow.ReportSave(mOUi.mOInteraction, mOUi.mOUWData, psError, pbCancelled);
        fCloseOptionsMenu();
    }

    /// <summary>
    /// Keys during name input. Returns true as long as typing is going on.
    /// </summary>
    private bool fUpdateSaveNameKeys()
    {
        if (miSaveSlotTyping == 0)
            return false;

        // Left as well as right mouse button confirm the name, although the pointer is not
        // visible - as with the quantity input (per user on the original, 2026-09-12). The
        // click that chose the slot was in the previous frame and no longer counts.
        if (UWPointer.HasMouse && (UWMouseButtons.LeftPressed || UWMouseButtons.RightPressed))
        {
            fConfirmSaveName();

            return true;
        }

        UnityEngine.InputSystem.Keyboard lOKeyboard = UnityEngine.InputSystem.Keyboard.current;

        if (lOKeyboard == null)
            return true;

        if (UWBack.Takes(UWBackOwner.ClassicSaveName))
        {
            fEndSaveName();
            fFinishSave(null, true);

            return true;
        }

        if (UWTextEntry.BackspacePressed)
        {
            mbSaveNameFresh = false;

            if (msSaveTypedName.Length > 0)
                msSaveTypedName = msSaveTypedName.Substring(0, msSaveTypedName.Length - 1);

            fRefreshSaveNamePrompt();

            return true;
        }

        if (UWTextEntry.EnterPressed)
        {
            fConfirmSaveName();

            return true;
        }

        // Where no text events arrive (Linux), the keys themselves (UWTypedKeys).
        fSaveNameEntry().ReadKeys();

        return true;
    }

    /// <summary>Enter or mouse button: the name applies, saving happens (UWSaveFlow.Write with
    /// the name as typed, untrimmed - the classic panel's rule).</summary>
    private void fConfirmSaveName()
    {
        int liSlot = miSaveSlotTyping;
        string lsName = msSaveTypedName;

        fEndSaveName();
        fFinishSave(UWSaveFlow.Write(liSlot, lsName), false);
    }

    private Image mOptionsButtonImage;

    private Image mOptionsPanelImage;

    private Image[] mOptionsEntryImages;

    internal void Build()
    {
        mOptionsButtonImage = UWUiKit.CreateFrameImage(mOUi.mGameFrame, "OptionsButton",
            optionsButtonLeft, -optionsButtonTop, 1f, 1f, Color.white);

        fSetUiSprite(mOptionsButtonImage, UWTexture.TextureTypes.LFTI, 0);

        mOptionsPanelImage = UWUiKit.CreateFrameImage(mOUi.mGameFrame, "OptionsPanel",
            optionsPanelLeft, -optionsPanelTop, 1f, 1f, Color.white);

        mOptionsPanelImage.enabled = false;

        mOptionsEntryImages = new Image[optionsEntryCount];

        for (int liAt = 0; liAt < optionsEntryCount; liAt++)
        {
            mOptionsEntryImages[liAt] = UWUiKit.CreateFrameImage(mOUi.mGameFrame, "OptionsEntry" + liAt,
                optionsEntryLeft, -(optionsEntryTop + (liAt * optionsEntryPitch)), 1f, 1f, Color.white);

            mOptionsEntryImages[liAt].enabled = false;
        }
    }

    /// <summary>Which picture an interface Image currently shows. fUpdateOptionsMenu sets the
    /// options button and the menu entries every frame - without mOUi cache every
    /// frame created a new texture plus sprite (per user, 2026-09-13: UWResourceWatch reported
    /// "UWIconTextureBuilder.cs:70 28x17", the LFTI button, by the tens of thousands).</summary>
    private readonly System.Collections.Generic.Dictionary<Image, long> mOUiSpriteShown =
        new System.Collections.Generic.Dictionary<Image, long>();

    /// <summary>Sets a picture from one of the interface graphics files and adjusts the
    /// size to the picture - the same chain as for the container arrows.</summary>
    internal void fSetUiSprite(Image pOImage, UWTexture.TextureTypes peType, int piIndex)
    {
        if (pOImage == null || mOUi.mOUWData == null || mOUi.mOUWData.Textures == null)
            return;

        long liKey = ((long)peType << 32) | (uint)piIndex;
        long liShown;

        if (pOImage.sprite != null && mOUiSpriteShown.TryGetValue(pOImage, out liShown) && liShown == liKey)
            return;

        // Shared (UWGrTextures, since 2026-10-19): nothing to release any more.
        Sprite lOSprite = UWGrTextures.IconSprite(mOUi.mOUWData.Textures, peType, piIndex, mOUi.TextureFilterMode);

        if (lOSprite == null)
            return;

        mOUiSpriteShown[pOImage] = liKey;

        pOImage.sprite = lOSprite;
        pOImage.color = Color.white;
        UWIconPalette.Apply(pOImage);
        ((RectTransform)pOImage.transform).sizeDelta = new Vector2(lOSprite.texture.width, lOSprite.texture.height);
    }

    /// <summary>Draws the model's screen: the background and the seven slots, each at its grid
    /// place or the model's nudge; an empty slot (NoPicture) stays dark.</summary>
    private void fApplyPage()
    {
        if (mOptionsPanelImage == null)
            return;

        int liBackground = mOModel.Background;

        if (liBackground == optNone)
        {
            mOptionsPanelImage.enabled = false;
            mOUi.Commands.SetVisible(true);
        }
        else
        {
            fSetUiSprite(mOptionsPanelImage, UWTexture.TextureTypes.OPTBTNS, liBackground);
            mOptionsPanelImage.enabled = true;

            // The panel covers the whole icon column in the original (35 by 108 at 4/10); ours
            // drew the command icons over it, because they are built after the panel (per
            // user, 2026-09-19).
            mOUi.Commands.SetVisible(false);
        }

        for (int liAt = 0; liAt < optionsEntryCount; liAt++)
        {
            (int DeltaX, int DeltaY) lOOffset = mOModel.EntryOffset(liAt);

            fPlaceOptionsEntry(liAt, lOOffset.DeltaX, lOOffset.DeltaY);

            int liArt = mOModel.EntryAt(liAt);

            if (liArt == optNone)
            {
                mOptionsEntryImages[liAt].enabled = false;

                continue;
            }

            fSetUiSprite(mOptionsEntryImages[liAt], UWTexture.TextureTypes.OPTBTNS, liArt);
            mOptionsEntryImages[liAt].enabled = true;
        }
    }

    /// <summary>Moves a slot away from its grid position; every other screen uses the grid,
    /// so the slot goes back there as soon as another screen fills the entries.</summary>
    private void fPlaceOptionsEntry(int piEntry, float pfDeltaX, float pfDeltaY)
    {
        ((RectTransform)mOptionsEntryImages[piEntry].transform).anchoredPosition = new Vector2(
            optionsEntryLeft + pfDeltaX, -(optionsEntryTop + (piEntry * optionsEntryPitch) + pfDeltaY));
    }

    /// <summary>The music and sound switches and the detail level the model draws its screens
    /// from.</summary>
    private static UWOptionsMenuModel.Settings fSettings()
    {
        return new UWOptionsMenuModel.Settings(UWSoundOptions.MusicEnabled, UWSoundOptions.SoundEnabled, (int)UWGraphicsDetail.CurrentLevel);
    }

    /// <summary>The restore screen (UWOptionsMenuModel.ShowRestore), with the games listed.</summary>
    private void fShowRestoreMenu()
    {
        mOModel.ShowRestore();
        fApplyPage();
        fListSaveGames();
    }

    /// <summary>Writes the four names into the scroll, with Roman numerals in front -
    /// that is also how the reference lists them.</summary>
    private void fListSaveGames()
    {
        if (mOUi.mOInteraction == null)
            return;

        string[] lsNumerals = { "I", "II", "III", "IV" };

        mOUi.mOInteraction.ResetMessages();

        // The heading is in no string block; the original writes it in green
        // (colour code 6, see fGetLogColourTable) and indented by five places, and the
        // names below stay green (per user, 2026-09-12).
        mOUi.mOInteraction.AddMessage(SaveListHeading);

        for (int liAt = 1; liAt <= UWSavegameSlots.SlotCount; liAt++)
            mOUi.mOInteraction.AddMessage(lsNumerals[liAt - 1] + "- "
                + UWSaveFlow.SlotName(liAt));
    }

    /// <summary>The heading over the save names, with the scroll's colour code (msg.save.list_heading).</summary>
    private static string SaveListHeading => UWText.T("msg.save.list_heading");

    /// <summary>Block 1: "\\6     Please enter a save file description:" - green, and the
    /// typed line below stays green (per user, 2026-09-12).</summary>
    private const int SaveDescriptionPromptMessage = 169;

    /// <summary>In the original the dots fill up during writing; in our version that is
    /// finished immediately, so they are there right away. Twelve, as in the user's picture.</summary>
    public const string ProgressDots = "............";

    private void fCloseOptionsMenu()
    {
        mOModel.Close();

        // The effects window of Very High is the setup bar's since cleanup 8d.3: it closes (and saves)
        // with the bar when the options go.
        fApplyPage();

        if (mOUi.mControlSchemeRef != null)
            mOUi.mControlSchemeRef.ReleaseUiModal("options");

        fHoldTimeForOptions(false);
        fSetUiSprite(mOptionsButtonImage, UWTexture.TextureTypes.LFTI, 0);
    }

    /// <summary>
    /// As soon as the options panel opens, everything stops in the original (per user,
    /// 2026-09-12) - the same time lock as when paging on (Interaction), a named hold on UWGameClock so
    /// that a different pause is not overwritten. The text cursor
    /// blinks by unscaled time, the music keeps running in the audio thread.
    /// </summary>
    private void fHoldTimeForOptions(bool pbHold)
    {
        if (pbHold == mbOptionsHoldTime)
            return;

        mbOptionsHoldTime = pbHold;

        if (pbHold)
            UWGameClock.Hold(UWGameClock.OptionsHold);
        else
            UWGameClock.Release(UWGameClock.OptionsHold);
    }

    private bool mbOptionsHoldTime;

    /// <summary>Whether the options panel is up.</summary>
    internal bool IsOpen => mOModel.IsOpen;

    /// <summary>F1, the original's first function key (the command column's sixth step, see
    /// UWControls.KeyOptions): opens the panel as the button does.</summary>
    internal void OpenByKey()
    {
        if (IsOpen || mOptionsButtonImage == null || mOUi.mGameFrame == null)
            return;

        if (RefusesToOpen())
            return;

        fOpenOptions();
    }

    /// <summary>"Impossible, you are between worlds." (block 1, 0x9F in UW.EXE's numbering).</summary>
    private const int BetweenWorldsMessage = 160;

    /// <summary>"You cannot select options partway through an action." (0xA0).</summary>
    private const int PartwayMessage = 161;

    /// <summary>
    /// THE OPTIONS REFUSE TO OPEN partway through an action (do_option_shortcut: "You cannot
    /// select options partway through an action."). "Partway" is GameInputMode, whose states are
    /// not all read - APPROXIMATED as an object on the pointer or a spell or shot waiting for its
    /// target. True when it refused, the message is written. The Void does NOT lock the panel:
    /// until 2026-10-12 it did ("Impossible, you are between worlds." at the opener, from a
    /// reading of 2026-09-29 that only knew saving was refused there); per user on the original
    /// the same day the panel opens in the Void and a game can be restored, only Save refuses -
    /// that is check_save, asked by the Save entry alone (RefusesToSave).
    /// </summary>
    internal bool RefusesToOpen()
    {
        Interaction lOInteraction = mOUi.mOInteraction;

        if (!fIsPartway())
            return false;

        if (lOInteraction != null)
            lOInteraction.AddGeneralMessage(PartwayMessage);

        return true;
    }

    /// <summary>
    /// SAVING REFUSES (check_save, INTERACT.C, asked when the Save entry is chosen - by a click,
    /// Enter or Ctrl+S): partway through an action with "You cannot select options partway
    /// through an action.", on level 9 with "Impossible, you are between worlds." - the Void wins
    /// when both hold. The panel stays open on its top page. Restoring never refuses (check_rest:
    /// an action in progress is simply abandoned). True when it refused, the message is written.
    /// </summary>
    internal bool RefusesToSave()
    {
        Interaction lOInteraction = mOUi.mOInteraction;
        int liMessage = 0;

        if (fIsPartway())
            liMessage = PartwayMessage;

        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader != null && lOLoader.CurrentLevelIndex == UWEndgame.VoidLevelIndex)
            liMessage = BetweenWorldsMessage;

        if (liMessage == 0)
            return false;

        if (lOInteraction != null)
            lOInteraction.AddGeneralMessage(liMessage);

        return true;
    }

    /// <summary>The approximation of the original's GameInputMode, see RefusesToOpen.</summary>
    private bool fIsPartway()
    {
        Interaction lOInteraction = mOUi.mOInteraction;

        return (mOUi.mOInventory != null && mOUi.mOInventory.CursorItem != null)
            || (lOInteraction != null && (lOInteraction.IsSpellTargeting || lOInteraction.IsRangedTargeting));
    }

    /// <summary>
    /// THE ORIGINAL'S CTRL SHORTCUTS (read 2026-09-26: the key table sends Ctrl+S, R, M, F, D
    /// and Q to ovr130_7E8, which sets the top menu and has ovr130_6F2 choose its entry through
    /// ovr130_689 - s 6, r 5, m 4, f 3, d 2, q 0 from the bottom): they open the options and
    /// choose that entry as a click would, and the panel stays open on that entry's screen -
    /// the options loop ovr130_0 runs on. The flag 0x1A34 stays set meanwhile, so the music
    /// and sound screens then close the whole panel after their choice
    /// (UWOptionsMenuModel.OpenedByShortcut). The entry is one of UWOptionsMenuModel.Top*.
    /// </summary>
    internal void ChooseByShortcut(int piTopEntry)
    {
        if (IsOpen || mOptionsButtonImage == null || mOUi.mGameFrame == null)
            return;

        if (RefusesToOpen())
            return;

        fOpenOptions();
        mOModel.MarkOpenedByShortcut();
        fChoose(piTopEntry);
    }

    /// <summary>The scheme changes while the panel is up (UWControlScheme.Apply): the panel
    /// closes, and its holds on time and input go with it; the modern menu takes its place.</summary>
    internal void CloseForSchemeChange()
    {
        if (IsOpen)
            fCloseOptionsMenu();
    }

    /// <summary>The scheme changes to the classic one while the modern menu is up: the panel
    /// opens in its place, so the player still stands in a menu and can close it.</summary>
    internal void OpenForSchemeChange()
    {
        if (!IsOpen && mOptionsButtonImage != null && mOUi.mGameFrame != null)
            fOpenOptions();
    }

    private void fOpenOptions()
    {
        if (mOUi.mControlSchemeRef != null)
            mOUi.mControlSchemeRef.HoldUiModal("options");

        fHoldTimeForOptions(true);
        mOModel.Open();
        fApplyPage();
    }

    /// <summary>
    /// Mouse in the options panel. Returns true as long as the panel is open - then nobody
    /// else may listen to the mouse.
    /// </summary>
    internal bool Update()
    {
        // While a save game name is being typed, the keyboard belongs to the input
        // and the mouse rests.
        if (fUpdateSaveNameKeys())
            return true;

        if (mOptionsButtonImage == null || mOUi.mGameFrame == null)
            return false;

        // The setup bar's menus and dialogs over the open panel (per user, 2026-10-06: the bar
        // shows itself in the options too): their clicks are theirs - the effects window of Very High
        // too, a window of the bar since cleanup 8d.3 (until then the options' own screen over the
        // frame, which had the mouse to itself here).
        if (UWSetupMenu.HasOpenPanel)
            return mOModel.IsOpen;

        if (!UWPointer.HasMouse)
            return mOModel.IsOpen;

        Vector2 lOPosition = UWPointer.Position;
        bool lbClicked = UWMouseButtons.LeftPressed;

        if (!mOModel.IsOpen)
        {
            // THE SIXTH STEP OF THE COMMAND COLUMN (2026-09-26): the original has no area of its
            // own for the button - the column's handler opens the options at (y + 2) / 18 = 5,
            // rows 11 to 28 (UWClickRules.CommandIconAt). Ours used the picture.
            bool lbOnButton = mOUi.ClassicScreen.IsPointerInOriginalArea(lOPosition, UWClickRules.CommandIcons, out int _, out int liColumnY)
                && UWClickRules.CommandIconAt(liColumnY) == UWCommandMode.Options;

            // NO HOVER in the original (per user, 2026-09-19): the button shows its idle picture until
            // the panel opens over it. Until then it lit up under the pointer.
            fSetUiSprite(mOptionsButtonImage, UWTexture.TextureTypes.LFTI, 0);

            if (!lbOnButton || !lbClicked)
                return false;

            // NOT WHILE THE VIEW ROAMS (per user, 2026-09-27: in the original the options cannot
            // be clicked during Roaming Sight; in ours they could).
            if (UWRoamingSight.IsAnyRunning)
                return false;

            if (RefusesToOpen())
                return false;

            fOpenOptions();

            return true;
        }

        int liHovered = -1;

        for (int liAt = 0; liAt < optionsEntryCount; liAt++)
        {
            if (mOModel.EntryAt(liAt) == optNone || !mOptionsEntryImages[liAt].enabled)
                continue;

            if (fIsInRect(lOPosition, (RectTransform)mOptionsEntryImages[liAt].transform))
                liHovered = liAt;
        }

        // THE KEYS (per user on the original, 2026-09-26: arrows and Enter work in the options;
        // read the same day in the options loop ovr130_0 and ovr130_6F2): Up and Down move the
        // highlight one entry - Space moves it down too -, Enter chooses it, Escape closes the
        // panel (ovr130_1BE). The highlight stops at the ends and does not step onto an entry
        // that cannot be chosen. NO MOUSE HOVER (per user on the original, 2026-09-26): only
        // this highlight is ever lit - the pointer over an entry lights nothing, a click just
        // chooses the entry under it. The stepping is UWOptionsMenuModel's.
        mOModel.ResetKeyEntryIfPageChanged();

        bool lbChooseByKey = false;
        UnityEngine.InputSystem.Keyboard lOKeyboard = UnityEngine.InputSystem.Keyboard.current;

        if (lOKeyboard != null && miSaveSlotTyping <= 0)
        {
            if (UWBack.Takes(UWBackOwner.ClassicOptions))
            {
                fCloseOptionsMenu();

                return true;
            }

        }

        // The help that has the keyboard (its notes typed, its manual read) and the setup bar's
        // keyboard navigation lie over the panel and take the arrows, Space and Enter - and Escape in
        // the back chain (cleanup 7, wave 2b: one key, one taker).
        if (lOKeyboard != null && miSaveSlotTyping <= 0 && !UWHelpWindow.BlocksGameKeys && !UWSetupMenu.IsKeyNavOn)
        {
            int liStep = lOKeyboard.upArrowKey.wasPressedThisFrame ? -1
                : lOKeyboard.downArrowKey.wasPressedThisFrame || lOKeyboard.spaceKey.wasPressedThisFrame ? 1
                : 0;

            if (liStep != 0)
                mOModel.StepKey(liStep);

            lbChooseByKey = (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame)
                && mOModel.KeyEntry >= 0;
        }

        // A CLICK MOVES THE HIGHLIGHT onto the entry it chooses, so the chosen save game stays lit
        // while its name is typed or it loads (per user, 2026-09-27: since the hover went, the
        // chosen slot was no longer lit when saving or loading).
        bool lbChooseByClick = !lbChooseByKey && lbClicked && liHovered >= 0 && mOModel.IsEntryClickable(liHovered);

        if (lbChooseByClick)
            mOModel.HighlightByClick(liHovered);

        // Only one is ever highlighted, and the heading never - it has no
        // second picture at all (UWOptionsMenuModel.PictureOf).
        for (int liAt = 0; liAt < optionsEntryCount; liAt++)
        {
            if (mOModel.EntryAt(liAt) == optNone)
                continue;

            fSetUiSprite(mOptionsEntryImages[liAt], UWTexture.TextureTypes.OPTBTNS, mOModel.PictureOf(liAt));
        }

        if (lbChooseByKey && mOModel.IsEntryClickable(mOModel.KeyEntry))
            fChoose(mOModel.KeyEntry);
        else if (lbChooseByClick)
            fChoose(liHovered);

        return true;
    }

    /// <summary>A click or Enter on an entry: the model changes the page and says what to carry
    /// out (UWOptionsMenuModel.Choose); the screen is drawn anew.</summary>
    private void fChoose(int piEntry)
    {
        UWOptionsMenuModel.Outcome lOOutcome = mOModel.Choose(piEntry, fSettings());

        switch (lOOutcome.Action)
        {
            case UWOptionsMenuModel.ActionEnum.AskSave:
                // The Void and "partway" refuse here, not at the opener (check_save).
                if (!RefusesToSave())
                    fShowSaveMenu();

                return;

            case UWOptionsMenuModel.ActionEnum.ListSaves:
                fApplyPage();
                fListSaveGames();
                return;

            case UWOptionsMenuModel.ActionEnum.BeginSaveName:
                fApplyPage();
                fBeginSaveName(lOOutcome.Value);
                return;

            case UWOptionsMenuModel.ActionEnum.Restore:
                fApplyPage();
                fRestore(lOOutcome.Value);
                return;

            case UWOptionsMenuModel.ActionEnum.SetMusic:
                UWSoundOptions.MusicEnabled = lOOutcome.Value != 0;
                break;

            case UWOptionsMenuModel.ActionEnum.SetSound:
                UWSoundOptions.SoundEnabled = lOOutcome.Value != 0;
                break;

            case UWOptionsMenuModel.ActionEnum.SetDetail:
            case UWOptionsMenuModel.ActionEnum.SetDetailAndOpenEffects:
                // The one path with the bar's Graphics menu (UWGraphicsDetail.Choose, cleanup step
                // 8.8): the level, this detail screen drawn anew, and for VERY HIGH (only that
                // entry gives SetDetailAndOpenEffects) the effects window - the setup bar's, the bar
                // with it, since cleanup 8d.3 (per user, 2026-10-25; the game's frame until then).
                UWGraphicsDetail.Choose((UWGraphicsDetail.LevelEnum)lOOutcome.Value, fShowDetailAnew, UWSetupMenu.OpenEffectsWindow);
                return;

            case UWOptionsMenuModel.ActionEnum.QuitToMainMenu:
                fQuitToMainMenu();
                return;
        }

        if (lOOutcome.ClosesPanel)
        {
            fCloseOptionsMenu();
            return;
        }

        fApplyPage();
    }

    /// <summary>The detail screen drawn anew with the level now set (UWGraphicsDetail.Choose).</summary>
    private void fShowDetailAnew()
    {
        mOModel.ShowDetail((int)UWGraphicsDetail.CurrentLevel);
        fApplyPage();
    }

    /// <summary>A slot of the restore screen: the game loads, or nothing is there - message, and
    /// stay on the load screen, just as in the original (UWSaveFlow.Restore).</summary>
    private void fRestore(int piSlot)
    {
        UWSaveFlow.Restore(mOUi.mOInteraction, piSlot);
    }

    /// <summary>
    /// "Quit Game" asks first, as in the original, and then goes BACK TO THE MAIN MENU instead of
    /// ending the program (per user, 2026-09-17; UWSaveFlow.QuitToMainMenu). The panel is the same
    /// in both control schemes.
    /// </summary>
    private void fQuitToMainMenu()
    {
        fCloseOptionsMenu();
        UWSaveFlow.QuitToMainMenu();
    }

    /// <summary>Is the mouse pointer over mOUi area? The same test as for the
    /// rune shelf.</summary>
    internal static bool fIsInRect(Vector2 pOScreenPos, RectTransform pORect)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(pORect, pOScreenPos, null);
    }
}
