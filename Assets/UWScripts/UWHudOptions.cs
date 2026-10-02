using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The HUD section "the options panel with save, restore, music, sound, detail and quit".
/// Its own class since 2026-09-18 (stage two of the HUD rebuild); the owner hands in the game
/// data, the frame, the character, the interaction and the inventory through mOUi. UWGameUI
/// keeps the public entry points as forwards.
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
    private const float optionsButtonLeft = 8f;

    private const float optionsButtonTop = 10f;

    private const float optionsPanelLeft = 4f;

    private const float optionsPanelTop = 10f;

    private const float optionsEntryLeft = 5f;

    private const float optionsEntryTop = 12f;

    private const float optionsEntryPitch = 15f;

    private const int optionsEntryCount = 7;

    // Picture numbers in OPTBTNS.GR. The order is that of the reference
    // (uimanager_options.OptionButtonIndices); for uw1 they are directly the
    // picture numbers, only uw2 first cuts its buttons to shape.
    private const int optAllOptionButtons = 1;

    private const int optAllSaveButtons = 2;

    private const int optSaveGame = 6;

    private const int optRestoreGame = 8;

    private const int optMusic = 10;

    private const int optSound = 12;

    private const int optDetail = 14;

    private const int optDetailButtons = 5;

    private const int optLowDetail = 38;

    /// <summary>"QUIT GAME: ARE YOU SURE?" with its two buttons - YES on slot 2, NO on slot 3
    /// (matched against the panel, OPTBTNS 3).</summary>
    private const int optQuitBackground = 3;

    private const int optYes = 57;

    private const int optNo = 59;

    /// <summary>"DETAIL LEVEL IS: LOW" - the three others follow (53 to 56). 34 by 18, one pixel
    /// further up and left than the slot grid (matched against the detail panel, OPTBTNS 5).</summary>
    private const int optDetailLabel = 53;

    private const int optReturnToGame = 16;

    private const int optQuitGame = 18;

    private const int optCancel = 24;

    // THE MUSIC AND SOUND SCREENS (read 2026-09-26, ovr130_2AA and the entry table from
    // 0x1A6D): background 4, the state at the top ("MUSIC IS ON." / "... OFF.", "SOUND IS
    // ON." / "... OFF"), under it "TURN MUSIC:" / "TURN SOUND:", then ON, OFF and DONE.

    private const int optOnOffBackground = 4;

    private const int optOn = 20;

    private const int optOff = 22;

    /// <summary>The DONE of the music and sound screens; the detail screen has its own
    /// (optDetailDone).</summary>
    private const int optOnOffDone = 26;

    private const int optMusicIsOn = 47;

    private const int optMusicIsOff = 48;

    private const int optSoundIsOn = 49;

    private const int optSoundIsOff = 50;

    private const int optTurnMusic = 51;

    private const int optTurnSound = 52;

    /// <summary>The detail screen's DONE: picture 28 in the original's table, not 26.</summary>
    private const int optDetailDone = 28;

    private const int optSave1 = 30;

    private const int optRestoreGameLabel = 46;

    /// <summary>Each button has two pictures: the idle one and, one further, the
    /// highlighted one.</summary>
    private const int optHighlightOffset = 1;

    /// <summary>No picture at mOUi slot.</summary>
    private const int optNone = -1;

    private enum OptionsMenuEnum
    {
        Closed,
        Top,
        Restore,
        Save,
        Detail,
        Quit,
        Music,
        Sound
    }

    /// <summary>The panel was opened by a Ctrl shortcut (the original's flag 0x1A34): then the
    /// music and sound screens close the whole panel after their choice instead of going back
    /// to the top menu.</summary>
    private bool mbOpenedByShortcut;

    /// <summary>
    /// THE HIGHLIGHT A SCREEN STARTS WITH (per user on the original, 2026-09-26: the top entry
    /// is lit at once; read the same day in the screens' drawing routines): the top menu
    /// SAVE, the save and load screens the first slot, the detail screen the current level,
    /// the quit question NO, the music and sound screens the button of the current state.
    /// </summary>
    private void fSetKeyHighlight(int piEntry)
    {
        miKeyEntry = piEntry;
        meKeyMenu = meOptionsMenu;
    }

    /// <summary>Into which slot (1 to 4) a name is currently being typed, or 0 for none.
    /// </summary>
    internal int miSaveSlotTyping;

    private string msSaveTypedName = string.Empty;

    /// <summary>The original cuts off anything longer.</summary>
    private const int SaveNameMaxLength = 30;

    /// <summary>
    /// The save screen: heading, the four slots, cancel - the same layout as
    /// for loading, only with "Save Game" as heading. The reference puts exactly the
    /// picture of the topmost button on the first slot for it.
    /// </summary>
    private void fShowSaveMenu()
    {
        meOptionsMenu = OptionsMenuEnum.Save;
        miSaveSlotTyping = 0;

        fSetOptionsEntries(optAllSaveButtons, new int[]
        {
            optSaveGame,
            optSave1, optSave1 + 2, optSave1 + 4, optSave1 + 6,
            optCancel,
            optNone
        });

        fSetKeyHighlight(1);
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
        string lsRoot = UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;

        miSaveSlotTyping = piSlot;
        msSaveTypedName = UWSavegameSlots.GetName(lsRoot, piSlot);

        if (msSaveTypedName.Length > SaveNameMaxLength)
            msSaveTypedName = msSaveTypedName.Substring(0, SaveNameMaxLength);

        // The character must not start walking while typing - the same lock as for the
        // input line of the debug console.
        if (mOUi.mControlSchemeRef != null)
            mOUi.mControlSchemeRef.Controls.Disable();

        if (UnityEngine.InputSystem.Keyboard.current != null)
            UnityEngine.InputSystem.Keyboard.current.onTextInput += fOnSaveNameTextInput;

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

        Cursor.visible = false;

        fRefreshSaveNamePrompt();
    }

    private bool mbSaveNameFresh;

    private void fEndSaveName()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null)
            UnityEngine.InputSystem.Keyboard.current.onTextInput -= fOnSaveNameTextInput;

        if (mOUi.mControlSchemeRef != null)
            mOUi.mControlSchemeRef.Controls.Enable();

        miSaveSlotTyping = 0;

        if (mOUi.mOInteraction != null)
            mOUi.mOInteraction.EndPromptMessage();

        Cursor.visible = true;
    }

    private void fOnSaveNameTextInput(char pcChar)
    {
        if (miSaveSlotTyping == 0 || pcChar < 0x20 || pcChar > 0x7E)
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
    /// Escape), without "Saving Game" and dots - and back into the game, not into the
    /// save screen.
    /// </summary>
    private void fFinishSave(string psError, bool pbCancelled)
    {
        if (mOUi.mOInteraction != null && mOUi.mOUWData != null)
        {
            mOUi.mOInteraction.ResetMessages();

            mOUi.mOInteraction.AddMessage(mOUi.mOUWData.GetGeneralMessage(psError == null && !pbCancelled
                ? SaveGameSucceededMessage
                : SaveGameFailedMessage));

            if (psError != null)
                mOUi.mOInteraction.AddMessage(psError);
        }

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
        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (lOMouse != null && (UWMouseButtons.LeftPressed || UWMouseButtons.RightPressed))
        {
            fConfirmSaveName();

            return true;
        }

        UnityEngine.InputSystem.Keyboard lOKeyboard = UnityEngine.InputSystem.Keyboard.current;

        if (lOKeyboard == null)
            return true;

        if (lOKeyboard.escapeKey.wasPressedThisFrame)
        {
            fEndSaveName();
            fFinishSave(null, true);

            return true;
        }

        if (lOKeyboard.backspaceKey.wasPressedThisFrame)
        {
            mbSaveNameFresh = false;

            if (msSaveTypedName.Length > 0)
                msSaveTypedName = msSaveTypedName.Substring(0, msSaveTypedName.Length - 1);

            fRefreshSaveNamePrompt();

            return true;
        }

        if (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame)
            fConfirmSaveName();

        return true;
    }

    /// <summary>Enter or mouse button: the name applies, saving happens.</summary>
    private void fConfirmSaveName()
    {
        int liSlot = miSaveSlotTyping;
        string lsName = msSaveTypedName;

        fEndSaveName();
        fFinishSave(UWSavegameWriter.Save(liSlot, lsName), false);
    }

    private OptionsMenuEnum meOptionsMenu = OptionsMenuEnum.Closed;

    private Image mOptionsButtonImage;

    private Image mOptionsPanelImage;

    private Image[] mOptionsEntryImages;

    /// <summary>Which picture lies on which slot - for highlighting and to
    /// know whether a slot is clickable at all.</summary>
    private int[] miOptionsEntryArt;

    internal void Build()
    {
        mOptionsButtonImage = UWGameUI.fCreateImage("OptionsButton", mOUi.mGameFrame,
            optionsButtonLeft, -optionsButtonTop, 1f, 1f, Color.white);

        fSetUiSprite(mOptionsButtonImage, UWTexture.TextureTypes.LFTI, 0);

        mOptionsPanelImage = UWGameUI.fCreateImage("OptionsPanel", mOUi.mGameFrame,
            optionsPanelLeft, -optionsPanelTop, 1f, 1f, Color.white);

        mOptionsPanelImage.enabled = false;

        mOptionsEntryImages = new Image[optionsEntryCount];
        miOptionsEntryArt = new int[optionsEntryCount];

        for (int liAt = 0; liAt < optionsEntryCount; liAt++)
        {
            mOptionsEntryImages[liAt] = UWGameUI.fCreateImage("OptionsEntry" + liAt, mOUi.mGameFrame,
                optionsEntryLeft, -(optionsEntryTop + (liAt * optionsEntryPitch)), 1f, 1f, Color.white);

            mOptionsEntryImages[liAt].enabled = false;
            miOptionsEntryArt[liAt] = optNone;
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

        UWTexture lOSource;

        try
        {
            lOSource = mOUi.mOUWData.Textures.GetTextureByType(peType, piIndex);
        }
        catch
        {
            return;
        }

        if (lOSource == null)
            return;

        Texture2D lOTexture = UWIconTextureBuilder.Build(lOSource, mOUi.TextureFilterMode);

        // Release the previous picture of mOUi Image - it belongs only to it.
        if (pOImage.sprite != null && mOUiSpriteShown.ContainsKey(pOImage))
        {
            Texture2D lOOld = pOImage.sprite.texture;
            Sprite lOOldSprite = pOImage.sprite;

            pOImage.sprite = null;
            Object.Destroy(lOOldSprite);

            if (lOOld != null)
                Object.Destroy(lOOld);
        }

        mOUiSpriteShown[pOImage] = liKey;

        pOImage.sprite = Sprite.Create(lOTexture,
            new Rect(0f, 0f, lOTexture.width, lOTexture.height), new Vector2(0.5f, 0.5f), 1f);
        pOImage.color = Color.white;
        UWIconPalette.Apply(pOImage);
        ((RectTransform)pOImage.transform).sizeDelta = new Vector2(lOTexture.width, lOTexture.height);
    }

    /// <summary>Fills the seven slots. Minus one leaves a slot empty.</summary>
    private void fSetOptionsEntries(int piBackground, int[] piEntries)
    {
        if (mOptionsPanelImage == null)
            return;

        if (piBackground == optNone)
        {
            mOptionsPanelImage.enabled = false;
            mOUi.Commands.SetVisible(true);
        }
        else
        {
            fSetUiSprite(mOptionsPanelImage, UWTexture.TextureTypes.OPTBTNS, piBackground);
            mOptionsPanelImage.enabled = true;

            // The panel covers the whole icon column in the original (35 by 108 at 4/10); ours
            // drew the command icons over it, because they are built after the panel (per
            // user, 2026-09-19).
            mOUi.Commands.SetVisible(false);
        }

        for (int liAt = 0; liAt < optionsEntryCount; liAt++)
        {
            fPlaceOptionsEntry(liAt, 0f, 0f);

            miOptionsEntryArt[liAt] = piEntries[liAt];

            if (piEntries[liAt] == optNone)
            {
                mOptionsEntryImages[liAt].enabled = false;

                continue;
            }

            fSetUiSprite(mOptionsEntryImages[liAt], UWTexture.TextureTypes.OPTBTNS, piEntries[liAt]);
            mOptionsEntryImages[liAt].enabled = true;
        }
    }

    /// <summary>The top screen: seven buttons, as in the original.</summary>
    private void fShowTopOptionsMenu()
    {
        meOptionsMenu = OptionsMenuEnum.Top;

        fSetOptionsEntries(optAllOptionButtons, new int[]
        {
            optSaveGame, optRestoreGame, optMusic, optSound, optDetail, optReturnToGame, optQuitGame
        });

        fSetKeyHighlight(0);
    }

    /// <summary>
    /// The music or the sound screen (ovr130_2AA): the state and "TURN ...:" over ON, OFF and
    /// DONE, the button of the current state lit. Until 2026-09-26 the top menu's entry
    /// switched at once with a line in the scroll (per user: the original has these screens).
    /// </summary>
    private void fShowOnOffMenu(bool pbMusic)
    {
        meOptionsMenu = pbMusic ? OptionsMenuEnum.Music : OptionsMenuEnum.Sound;

        bool lbOn = pbMusic ? UWSoundOptions.MusicEnabled : UWSoundOptions.SoundEnabled;
        int liState = pbMusic ? (lbOn ? optMusicIsOn : optMusicIsOff) : (lbOn ? optSoundIsOn : optSoundIsOff);

        fSetOptionsEntries(optOnOffBackground, new int[]
        {
            liState, pbMusic ? optTurnMusic : optTurnSound,
            optOn, optOff, optOnOffDone,
            optNone, optNone
        });

        fSetKeyHighlight(lbOn ? 2 : 3);
    }

    /// <summary>
    /// A choice on the music or sound screen (ovr130_3B5, ovr130_40D): ON and OFF switch and
    /// draw the screen anew, DONE switches nothing. Opened by a Ctrl shortcut, every one of
    /// the three closes the panel; otherwise ON and OFF stay on the screen and DONE goes back
    /// to the top menu.
    /// </summary>
    private void fClickOnOffEntry(int piEntry)
    {
        bool lbMusic = meOptionsMenu == OptionsMenuEnum.Music;

        if (piEntry == 2 || piEntry == 3)
        {
            if (lbMusic)
                UWSoundOptions.MusicEnabled = piEntry == 2;
            else
                UWSoundOptions.SoundEnabled = piEntry == 2;
        }

        if (mbOpenedByShortcut)
        {
            fCloseOptionsMenu();
            return;
        }

        if (piEntry == 4)
            fShowTopOptionsMenu();
        else
            fShowOnOffMenu(lbMusic);
    }

    /// <summary>
    /// The detail screen of the original, with new meanings (per user, 2026-09-17): LOW the
    /// palette renderer, MEDIUM Remastered without effects, HIGH Remastered with all of them,
    /// VERY HIGH the viewer's own values on the effects screen (see UWGraphicsDetail). The
    /// panel carries "SET NEW LEVEL TO:" itself; the label at the top names the current level.
    /// </summary>
    private void fShowDetailMenu()
    {
        meOptionsMenu = OptionsMenuEnum.Detail;

        fSetOptionsEntries(optDetailButtons, new int[]
        {
            optDetailLabel + (int)UWGraphicsDetail.CurrentLevel,
            optNone,
            optLowDetail, optLowDetail + 2, optLowDetail + 4, optLowDetail + 6,
            optDetailDone
        });

        fPlaceOptionsEntry(0, -1f, -1f);
        fSetKeyHighlight(2 + (int)UWGraphicsDetail.CurrentLevel);
    }

    /// <summary>Moves a slot away from its grid position; every other screen uses the grid,
    /// so the slot goes back there as soon as another screen fills the entries.</summary>
    private void fPlaceOptionsEntry(int piEntry, float pfDeltaX, float pfDeltaY)
    {
        ((RectTransform)mOptionsEntryImages[piEntry].transform).anchoredPosition = new Vector2(
            optionsEntryLeft + pfDeltaX, -(optionsEntryTop + (piEntry * optionsEntryPitch) + pfDeltaY));
    }

    private UWEffectsScreen mOEffectsScreen;

    private void fClickDetailEntry(int piEntry)
    {
        switch (piEntry)
        {
            case 2:
            case 3:
            case 4:
                UWGraphicsDetail.SetLevel((UWGraphicsDetail.LevelEnum)(piEntry - 2));
                fShowDetailMenu();
                return;

            case 5:
                UWGraphicsDetail.SetLevel(UWGraphicsDetail.LevelEnum.VeryHigh);
                fShowDetailMenu();

                if (mOEffectsScreen == null)
                    mOEffectsScreen = new UWEffectsScreen(mOUi.mOUWData, mOUi.mGameFrame);

                mOEffectsScreen.Open();
                return;

            case 6:
                fShowTopOptionsMenu();
                return;
        }
    }

    /// <summary>
    /// The load screen: a heading, the four slots and cancel.
    ///
    /// The NAMES are not on the buttons but in the scroll - exactly that is
    /// how the reference does it (listsaves), and therefore the slots only have one
    /// picture each with "I" to "IV".
    /// </summary>
    private void fShowRestoreMenu()
    {
        meOptionsMenu = OptionsMenuEnum.Restore;

        fSetOptionsEntries(optAllSaveButtons, new int[]
        {
            optRestoreGameLabel,
            optSave1, optSave1 + 2, optSave1 + 4, optSave1 + 6,
            optCancel,
            optNone
        });

        fSetKeyHighlight(1);
        fListSaveGames();
    }

    /// <summary>Writes the four names into the scroll, with Roman numerals in front -
    /// that is also how the reference lists them.</summary>
    private void fListSaveGames()
    {
        if (mOUi.mOInteraction == null)
            return;

        string[] lsNumerals = { "I", "II", "III", "IV" };
        string lsRoot = UnderworldRevisited.UWSettings.Instance != null
            ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;

        mOUi.mOInteraction.ResetMessages();

        // The heading is in no string block; the original writes it in green
        // (colour code 6, see fGetLogColourTable) and indented by five places, and the
        // names below stay green (per user, 2026-09-12).
        mOUi.mOInteraction.AddMessage(SaveListHeading);

        for (int liAt = 1; liAt <= UWSavegameSlots.SlotCount; liAt++)
            mOUi.mOInteraction.AddMessage(lsNumerals[liAt - 1] + "- "
                + UWSavegameSlots.GetName(lsRoot, liAt));
    }

    private const string SaveListHeading = "\\6     Save Game Descriptions";

    /// <summary>Block 1: "\\6     Please enter a save file description:" - green, and the
    /// typed line below stays green (per user, 2026-09-12).</summary>
    private const int SaveDescriptionPromptMessage = 169;

    private const int SaveGameFailedMessage = 165;

    private const int SaveGameSucceededMessage = 166;

    /// <summary>In the original the dots fill up during writing; in our version that is
    /// finished immediately, so they are there right away. Twelve, as in the user's picture.</summary>
    public const string ProgressDots = "............";

    private void fCloseOptionsMenu()
    {
        meOptionsMenu = OptionsMenuEnum.Closed;
        mbOpenedByShortcut = false;

        if (mOEffectsScreen != null)
            mOEffectsScreen.Close();

        fSetOptionsEntries(optNone, new int[]
        {
            optNone, optNone, optNone, optNone, optNone, optNone, optNone
        });

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
    internal bool IsOpen => meOptionsMenu != OptionsMenuEnum.Closed;

    /// <summary>F1, the original's first function key (the command column's sixth step, see
    /// UWControls.KeyOptions): opens the panel as the button does.</summary>
    internal void OpenByKey()
    {
        if (IsOpen || mOptionsButtonImage == null || mOUi.mGameFrame == null)
            return;

        if (fRefusesToOpen())
            return;

        fOpenOptions();
    }

    /// <summary>"Impossible, you are between worlds." (block 1, 0x9F in UW.EXE's numbering).</summary>
    private const int BetweenWorldsMessage = 160;

    /// <summary>"You cannot select options partway through an action." (0xA0).</summary>
    private const int PartwayMessage = 161;

    /// <summary>
    /// THE OPTIONS REFUSE TO OPEN (seg024_24DC_1627, the opener behind the button, F1 and the
    /// Ctrl shortcuts): partway through an action, and on level 9 at all - "Impossible, you are
    /// between worlds." wins over the other (per user on the original, 2026-09-29: no saving in
    /// the Void; ours let the panel open). "Partway" is IsPlayerDoingSomething, whose states are
    /// not all read - APPROXIMATED as an object on the pointer or a spell or shot waiting for its
    /// target. True when it refused, the message is written.
    /// </summary>
    private bool fRefusesToOpen()
    {
        Interaction lOInteraction = mOUi.mOInteraction;
        int liMessage = 0;

        if ((mOUi.mOInventory != null && mOUi.mOInventory.CursorItem != null)
            || (lOInteraction != null && (lOInteraction.IsSpellTargeting || lOInteraction.IsRangedTargeting)))
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

    /// <summary>The entries of the top menu, counted from the top (the original counts them
    /// from the bottom: quit 0 ... save 6).</summary>
    internal const int TopSave = 0;

    internal const int TopRestore = 1;

    internal const int TopMusic = 2;

    internal const int TopSound = 3;

    internal const int TopDetail = 4;

    internal const int TopQuit = 6;

    /// <summary>
    /// THE ORIGINAL'S CTRL SHORTCUTS (read 2026-09-26: the key table sends Ctrl+S, R, M, F, D
    /// and Q to ovr130_7E8, which sets the top menu and has ovr130_6F2 choose its entry through
    /// ovr130_689 - s 6, r 5, m 4, f 3, d 2, q 0 from the bottom): they open the options and
    /// choose that entry as a click would, and the panel stays open on that entry's screen -
    /// the options loop ovr130_0 runs on. The flag 0x1A34 stays set meanwhile, so the music
    /// and sound screens then close the whole panel after their choice (mbOpenedByShortcut).
    /// </summary>
    internal void ChooseByShortcut(int piTopEntry)
    {
        if (IsOpen || mOptionsButtonImage == null || mOUi.mGameFrame == null)
            return;

        if (fRefusesToOpen())
            return;

        fOpenOptions();
        mbOpenedByShortcut = true;
        fClickOptionsEntry(piTopEntry);
    }

    private void fOpenOptions()
    {
        if (mOUi.mControlSchemeRef != null)
            mOUi.mControlSchemeRef.HoldUiModal("options");

        mbOpenedByShortcut = false;
        fHoldTimeForOptions(true);
        fShowTopOptionsMenu();
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

        // The effects screen lies over the whole frame and has the mouse to itself.
        if (mOEffectsScreen != null && mOEffectsScreen.IsOpen)
        {
            mOEffectsScreen.Update();

            return true;
        }

        if (mOptionsButtonImage == null || mOUi.mGameFrame == null)
            return false;

        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (lOMouse == null)
            return meOptionsMenu != OptionsMenuEnum.Closed;

        Vector2 lOPosition = lOMouse.position.ReadValue();
        bool lbClicked = UWMouseButtons.LeftPressed;

        if (meOptionsMenu == OptionsMenuEnum.Closed)
        {
            // THE SIXTH STEP OF THE COMMAND COLUMN (2026-09-26): the original has no area of its
            // own for the button - the column's handler opens the options at (y + 2) / 18 = 5,
            // rows 11 to 28 (UWClickRules.CommandIconAt). Ours used the picture.
            bool lbOnButton = mOUi.IsPointerInOriginalArea(lOPosition, UWClickRules.CommandIcons, out int _, out int liColumnY)
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

            if (fRefusesToOpen())
                return false;

            fOpenOptions();

            return true;
        }

        int liHovered = -1;

        for (int liAt = 0; liAt < optionsEntryCount; liAt++)
        {
            if (miOptionsEntryArt[liAt] == optNone || !mOptionsEntryImages[liAt].enabled)
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
        // chooses the entry under it.
        if (meKeyMenu != meOptionsMenu)
        {
            meKeyMenu = meOptionsMenu;
            miKeyEntry = -1;
        }

        bool lbChooseByKey = false;
        UnityEngine.InputSystem.Keyboard lOKeyboard = UnityEngine.InputSystem.Keyboard.current;

        if (lOKeyboard != null && miSaveSlotTyping <= 0)
        {
            if (lOKeyboard.escapeKey.wasPressedThisFrame)
            {
                fCloseOptionsMenu();

                return true;
            }

            int liStep = lOKeyboard.upArrowKey.wasPressedThisFrame ? -1
                : lOKeyboard.downArrowKey.wasPressedThisFrame || lOKeyboard.spaceKey.wasPressedThisFrame ? 1
                : 0;

            if (liStep != 0)
                miKeyEntry = fStepKeyEntry(miKeyEntry, liStep);

            lbChooseByKey = (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame)
                && miKeyEntry >= 0;
        }

        // A CLICK MOVES THE HIGHLIGHT onto the entry it chooses, so the chosen save game stays lit
        // while its name is typed or it loads (per user, 2026-09-27: since the hover went, the
        // chosen slot was no longer lit when saving or loading).
        bool lbChooseByClick = !lbChooseByKey && lbClicked && liHovered >= 0 && fIsOptionsEntryClickable(liHovered);

        if (lbChooseByClick)
            miKeyEntry = liHovered;

        int liLit = miKeyEntry;

        // Only one is ever highlighted, and the heading never - it has no
        // second picture at all.
        for (int liAt = 0; liAt < optionsEntryCount; liAt++)
        {
            if (miOptionsEntryArt[liAt] == optNone)
                continue;

            bool lbHighlight = liAt == liLit && fIsOptionsEntryClickable(liAt);

            fSetUiSprite(mOptionsEntryImages[liAt], UWTexture.TextureTypes.OPTBTNS,
                miOptionsEntryArt[liAt] + (lbHighlight ? optHighlightOffset : 0));
        }

        if (lbChooseByKey && fIsOptionsEntryClickable(miKeyEntry))
            fClickOptionsEntry(miKeyEntry);
        else if (lbChooseByClick)
            fClickOptionsEntry(liHovered);

        return true;
    }

    /// <summary>The entry the keys have highlighted, -1 for none (then the mouse's counts).
    /// </summary>
    private int miKeyEntry = -1;

    /// <summary>The menu the key highlight belongs to - a new menu starts without one.</summary>
    private OptionsMenuEnum meKeyMenu = OptionsMenuEnum.Closed;

    /// <summary>One step of the key highlight (entries counted from the top). Without a
    /// highlight yet, Down starts on the first entry that can be chosen and Up on the last;
    /// otherwise it moves one entry and stays where it is at the end or before an entry that
    /// cannot be chosen, as the original's ovr130_6F2 does.</summary>
    private int fStepKeyEntry(int piFrom, int piStep)
    {
        if (piFrom < 0)
        {
            for (int liAt = piStep > 0 ? 0 : optionsEntryCount - 1; liAt >= 0 && liAt < optionsEntryCount; liAt += piStep)
            {
                if (fIsKeyEntryUsable(liAt))
                    return liAt;
            }

            return -1;
        }

        int liTo = piFrom + piStep;

        return liTo >= 0 && liTo < optionsEntryCount && fIsKeyEntryUsable(liTo) ? liTo : piFrom;
    }

    private bool fIsKeyEntryUsable(int piEntry)
    {
        return miOptionsEntryArt[piEntry] != optNone && mOptionsEntryImages[piEntry].enabled
            && fIsOptionsEntryClickable(piEntry);
    }

    /// <summary>The heading of the load and save screens is not a button.</summary>
    private bool fIsOptionsEntryClickable(int piEntry)
    {
        if (meOptionsMenu == OptionsMenuEnum.Detail)
            return piEntry >= 2;

        if (meOptionsMenu == OptionsMenuEnum.Quit)
            return piEntry == 2 || piEntry == 3;

        if (meOptionsMenu == OptionsMenuEnum.Music || meOptionsMenu == OptionsMenuEnum.Sound)
            return piEntry >= 2 && piEntry <= 4;

        return !((meOptionsMenu == OptionsMenuEnum.Restore || meOptionsMenu == OptionsMenuEnum.Save)
            && piEntry == 0);
    }

    /// <summary>String block 1: "No save game there." The reference gives 161 for uw1,
    /// checked against the printout of the block.</summary>
    private const int NoSaveGameThereMessage = 162;

    private void fClickOptionsEntry(int piEntry)
    {
        if (meOptionsMenu == OptionsMenuEnum.Top)
        {
            switch (piEntry)
            {
                case 0:
                    fShowSaveMenu();
                    return;

                case 1:
                    fShowRestoreMenu();
                    return;

                case 5:
                    fCloseOptionsMenu();
                    return;

                case 6:
                    fShowQuitMenu();
                    return;

                // Music and sound: the original's own screens since 2026-09-26 (until then
                // the entry switched at once with a line in the scroll).
                case 2:
                    fShowOnOffMenu(true);
                    return;

                case 3:
                    fShowOnOffMenu(false);
                    return;

                case 4:
                    fShowDetailMenu();
                    return;
            }

            return;
        }

        if (meOptionsMenu == OptionsMenuEnum.Detail)
        {
            fClickDetailEntry(piEntry);

            return;
        }

        if (meOptionsMenu == OptionsMenuEnum.Music || meOptionsMenu == OptionsMenuEnum.Sound)
        {
            fClickOnOffEntry(piEntry);

            return;
        }

        if (meOptionsMenu == OptionsMenuEnum.Quit)
        {
            if (piEntry == 2)
                fQuitToMainMenu();
            else if (piEntry == 3)
                fShowTopOptionsMenu();

            return;
        }

        if (meOptionsMenu == OptionsMenuEnum.Save)
        {
            if (piEntry == 5)
                fShowTopOptionsMenu();
            else if (piEntry >= 1 && piEntry <= UWSavegameSlots.SlotCount)
                fBeginSaveName(piEntry);

            return;
        }

        if (meOptionsMenu != OptionsMenuEnum.Restore)
            return;

        if (piEntry == 5)
        {
            fShowTopOptionsMenu();

            return;
        }

        string lsRoot = UnderworldRevisited.UWSettings.Instance != null
            ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;

        if (UWSavegameSlots.Restore(lsRoot, piEntry))
            return;

        // Nothing on the slot - message, and stay on the load screen, just as in the original.
        if (mOUi.mOInteraction != null)
            mOUi.mOInteraction.AddGeneralMessage(NoSaveGameThereMessage);
    }

    /// <summary>
    /// "Quit Game" asks first, as in the original, and then goes BACK TO THE MAIN MENU instead of
    /// ending the program (per user, 2026-09-17) - from there one can load, start a new character
    /// or really quit. The panel is the same in both control schemes.
    /// </summary>
    private void fShowQuitMenu()
    {
        meOptionsMenu = OptionsMenuEnum.Quit;

        fSetOptionsEntries(optQuitBackground, new int[]
        {
            optNone, optNone, optYes, optNo, optNone, optNone, optNone
        });

        fSetKeyHighlight(3);
    }

    private void fQuitToMainMenu()
    {
        fCloseOptionsMenu();

        UWMainMenu lOMenu = UWScene.MainMenu;

        if (lOMenu != null)
        {
            lOMenu.Show();

            return;
        }

        // No menu in the scene (should not happen): then at least end the program, as before.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Is the mouse pointer over mOUi area? The same test as for the
    /// rune shelf.</summary>
    internal static bool fIsInRect(Vector2 pOScreenPos, RectTransform pORect)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(pORect, pOScreenPos, null);
    }
}
