using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The original's character creation - the display for UWCharacterGeneration.
///
/// CHARGEN.BYT (palette 3) is the book: on the left the page with name, sex and class
/// in the upper box, below that body and attributes, at the bottom the skills; on the right the
/// empty page for question and answers. The answers are buttons from CHRBTNS.GR (image
/// 0, 67x16, under the pointer the focus frame image 2 on top), the faces are frames (image 3,
/// focus image 5) with the portraits 7
/// to 16, the bodies images 17 to 26, the name box image 6. The font is
/// FONTCHAR.SYS.
///
/// THE LAYOUT OF THE PARTS comes from the reference scene (Underworld.tscn, 1280x800, so
/// divided by four) and its button formula (uimanager.CalculateChargenButtonPosition):
/// one column at x 212, two columns at 175 and 250, row pitch 17.5 around the centre 100;
/// the faces at x 212 with pitch 36 around the centre 75. The text colour is palette 3,
/// index 0x49 - the reference names a palette 9, which does not exist.
///
/// At the end comes "Keep this character?": Yes builds the PLAYER.DAT (UWPlayerData.BuildNewCharacter),
/// stores it statically here and restarts the scene - UWLevelLoader picks it up on
/// start and places the character in the starting room. No intro follows (per user,
/// 2026-09-14: the original does not play it after character creation).
/// No starts over. Escape goes back to the main menu.
/// </summary>
public class UWCharacterCreationScreen : MonoBehaviour
{
    private const int ScreenPalette = 3;

    private const int TextColourIndex = 0x49;

    // CHRBTNS.GR
    //
    // THE FOCUS IS A FRAME, NOT A DIFFERENT IMAGE. Image 0 is the whole button, stone with
    // frame and black outline. Image 1 is the same frame alone, index 0 inside; image 2
    // a frame in other colours (4F/57/89 instead of 5C/5E), likewise index 0 inside. Shown
    // under the pointer is image 2 over the button, with a transparent interior - in the
    // user's photos of the original (DOSBox-Staging, 2026-09-11) barely visible, in the
    // old DOSBox more clearly. Until then we swapped to image 1 and got a
    // black box. The face frames the same way: 3 the frame, 5 the focus.
    private const int ButtonImage = 0;

    private const int ButtonFocusImage = 2;

    private const int PortraitFrameImage = 3;

    private const int PortraitFrameFocusImage = 5;

    private const int NameBoxImage = 6;

    private const int MalePortraitImage = 7;

    private const int FemalePortraitImage = 12;

    private const int MaleBodyImage = 17;

    private const int FemaleBodyImage = 22;

    /// <summary>The boxes of the texts are UWClassicFit.Creation's (translation 5.2), so the fit check
    /// (uwdump fit) measures against the very widths drawn here.</summary>
    private const int ButtonWidth = UWClassicFit.Creation.ButtonWidth;

    private const int ButtonHeight = 16;

    private const int PortraitSize = 35;

    // Layout (reference scene divided by four)
    private const int NameLeft = 23;

    private const int NameTop = 10;

    private const int SexLeft = 18;

    private const int ClassLeft = 88;

    private const int SexClassTop = 24;

    private const int BodyLeft = 33;

    private const int BodyTop = 44;

    private const int StatLabelLeft = 87;

    private const int StatValueLeft = 130;

    private const int StatTop = 49;

    private const int SkillLabelLeft = 18;

    private const int SkillValueLeft = 125;

    private const int SkillTop = 130;

    private const int NameBoxLeft = 167;

    private const int NameBoxTop = 25;

    private const int NamePromptLeft = 5;

    private const int NameInputLeft = 41;

    private const int NameTextTop = 4;

    private const int SingleColumnLeft = 212;

    private const int FirstColumnLeft = 175;

    private const int SecondColumnLeft = 250;

    private const float RowPitch = 17.5f;

    private const int RowCentre = 100;

    private const int PortraitPitch = 36;

    private const int PortraitCentre = 75;

    /// <summary>The question is centred above the column, as wide as the right page.</summary>
    private const int QuestionLeft = 176;

    private const int QuestionWidth = UWClassicFit.Creation.QuestionWidth;

    private const int LabelWidth = UWClassicFit.Creation.LabelWidth;

    /// <summary>Up to this many answers one column, above that two (reference).</summary>
    private const int SingleColumnLimit = 8;

    /// <summary>The new character that survives the scene restart - see UWLevelLoader.</summary>
    public static byte[] PendingPlayerData { get; private set; }

    /// <summary>We are currently starting with a freshly created character. This decides that the
    /// main menu does not come up again.</summary>
    public static bool IsStartingNewGame { get; private set; }

    private static bool mbIntroRequested;

    /// <summary>Is the player currently typing the name? Then the F belongs to the text (see
    /// UWGameUI.fCheckFontToggle).</summary>
    public static bool IsTypingName { get; private set; }

    /// <summary>Is the character creation currently shown? See UWScreenUi.IsScreenMenuOpen.
    /// </summary>
    public static bool IsAnyOpen { get; private set; }

    public static byte[] TakePendingPlayerData()
    {
        byte[] lyResult = PendingPlayerData;

        PendingPlayerData = null;

        return lyResult;
    }

    /// <summary>Once after creation: the intro is due.</summary>
    public static bool TakeIntroRequest()
    {
        bool lbResult = mbIntroRequested;

        mbIntroRequested = false;

        return lbResult;
    }

    public bool IsOpen { get; private set; }

    private class Button
    {
        public RawImage Image;

        public RawImage Label;

        public RawImage Portrait;

        /// <summary>The focus frame, topmost.</summary>
        public RawImage Focus;
    }

    private DataImport mOData;
    private UWCharacterGeneration mOGeneration;
    private Canvas mOCanvas;
    private RectTransform mOFrame;
    private RawImage mOBackground;
    private RawImage mOQuestion;
    private RawImage mOName;
    private RawImage mOSex;
    private RawImage mOClass;
    private RawImage mOBody;
    private RawImage mOStatLabels;
    private RawImage mOStatValues;
    private RawImage mOSkillLabels;
    private RawImage mOSkillValues;
    private RawImage mONameBox;
    private RawImage mONamePrompt;
    private RawImage mONameInput;
    private readonly List<Button> mOButtons = new List<Button>();
    private readonly Dictionary<int, Texture2D> mOImages = new Dictionary<int, Texture2D>();
    private UWFont mOFont;
    private Color32 mOTextColour;
    private FilterMode meFilter = FilterMode.Point;
    private int miHover = -1;
    private string msTyped = string.Empty;
    private UWControlScheme mOControls;

    public void Init(DataImport pOData)
    {
        // Static, so it survives the scene restart - freshly loaded, nothing is open.
        IsAnyOpen = false;
        IsTypingName = false;

        mOData = pOData;
        mOGeneration = new UWCharacterGeneration(pOData.Strings, pOData.MiscDataFiles);
        mOTextColour = UWScreenUi.GetColour(pOData.Palettes, ScreenPalette, TextColourIndex);

        if (UnderworldRevisited.UWSettings.Instance != null)
            meFilter = UnderworldRevisited.UWSettings.Instance.TextureFilterMode;
    }

    public void Show()
    {
        if (mOData == null || mOGeneration == null || !mOGeneration.IsLoaded)
        {
            Debug.LogWarning("Character creation: CHRGEN.DAT or SKILLS.DAT is missing.");
            return;
        }

        fEnsureCanvas();

        // The start of the LAST new game is long done by now. The flag is static and survives
        // the scene restart on purpose (UWMainMenu.WillAutoShow), but left standing it made the
        // main menu believe that this creation, too, had started a game when Escape closed it:
        // the menu hid itself and stayed away - new character, Quit, create again, Escape
        // (per user, 2026-09-23).
        IsStartingNewGame = false;

        mOGeneration.ManualAttributes = UWUserSettings.ManualAttributes;
        mOGeneration.Begin();
        mOCanvas.gameObject.SetActive(true);
        IsOpen = true;
        IsAnyOpen = true;
        miHover = -1;

        fSetModal(true);
        UWGameClock.Hold(UWGameClock.CharacterCreationHold);

        fRefresh();
    }

    public void Hide()
    {
        fEndTyping();

        IsOpen = false;
        IsAnyOpen = false;

        if (mOCanvas != null)
            mOCanvas.gameObject.SetActive(false);

        // The main menu takes over the lock and the time again right away, if it is still there.
        fSetModal(false);
        UWGameClock.Release(UWGameClock.CharacterCreationHold);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        Keyboard lOKeyboard = Keyboard.current;

        if (mOGeneration.CurrentStage == UWCharacterGeneration.Stage.Name)
        {
            fUpdateTyping(lOKeyboard);
            return;
        }

        if (IsTypingName)
            fEndTyping();

        // B as Escape (UWScreenUi.WasPadBackPressed until cleanup step 7.5; the chain, UWBack).
        if (UWBack.Takes(UWBackOwner.CharacterCreation))
        {
            Hide();
            return;
        }

        fStepByPad();

        int liHover = -1;

        for (int liAt = 0; liAt < mOButtons.Count; liAt++)
        {
            if (UWScreenUi.IsMouseOver(mOButtons[liAt].Image))
            {
                liHover = liAt;
                break;
            }
        }

        if (liHover != miHover)
        {
            miHover = liHover;

            for (int liAt = 0; liAt < mOButtons.Count; liAt++)
            {
                if (mOButtons[liAt].Focus != null)
                    mOButtons[liAt].Focus.enabled = liAt == miHover;
            }
        }

        if (miHover < 0 || !UWScreenUi.WasLeftClicked())
            return;

        fChoose(miHover);
    }

    /// <summary>The gamepad steps through the choices (UWPadEntryStepper, per user 2026-10-08: as
    /// in the other menus); every new stage snaps the pointer onto its first choice.</summary>
    private readonly UWPadEntryStepper mOPadStepper = new UWPadEntryStepper();

    private readonly List<Rect> mOPadRects = new List<Rect>();

    private string msSteppedStage;

    private void fStepByPad()
    {
        mOPadRects.Clear();

        foreach (Button lOButton in mOButtons)
        {
            Rect lORect = UWScreenUi.ScreenRectOf(lOButton.Image);

            if (lORect.width > 0f)
                mOPadRects.Add(lORect);
        }

        // A NEW QUESTION snaps onto the first choice: the stage and its choices. Not in the manual
        // dealing of the attribute points (per user, 2026-10-08), whose plus and minus are pressed
        // again and again - its choices change with every point, the pointer stays where it is.
        string lsStage = mOGeneration.CurrentStage == UWCharacterGeneration.Stage.Attributes
            ? mOGeneration.CurrentStage.ToString()
            : mOGeneration.CurrentStage + "|" + string.Join("|", mOGeneration.Choices);

        if (lsStage != msSteppedStage)
        {
            msSteppedStage = lsStage;
            mOPadStepper.Opened();
            return;
        }

        mOPadStepper.Update(mOPadRects);
    }

    private void fChoose(int piChoice)
    {
        UWCharacterGeneration.Stage leBefore = mOGeneration.CurrentStage;

        mOGeneration.Choose(piChoice);

        if (leBefore == UWCharacterGeneration.Stage.Confirm && mOGeneration.CurrentStage == UWCharacterGeneration.Stage.Done)
        {
            fStartGame();
            return;
        }

        fRefresh();
    }

    /// <summary>Yes at "Keep this character?": build the character, restart the scene - see class comment.</summary>
    private void fStartGame()
    {
        PendingPlayerData = mOGeneration.BuildPlayerData();
        IsStartingNewGame = true;

        // No intro after creating a character: in the original the game starts directly
        // (per user, 2026-09-14). The intro is only played from the main menu.
        mbIntroRequested = false;

        Debug.Log(string.Format("[Creation] {0}, {1} {2}, Str {3} Dex {4} Int {5} - rebuilding scene.",
            mOGeneration.Name, mOGeneration.SexName, mOGeneration.ClassName,
            mOGeneration.Strength, mOGeneration.Dexterity, mOGeneration.Intelligence));

        // Black already in THIS frame, before the rebuild - otherwise you see the
        // old world for one frame (see UWScreenUi.ShowLoadingCover).
        UWScreenUi.ShowLoadingCover();

        Hide();
        UWGameClock.ReleaseAll();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ------------------------------------------------------------------
    // Name
    // ------------------------------------------------------------------

    private void fUpdateTyping(Keyboard pOKeyboard)
    {
        if (!IsTypingName)
        {
            IsTypingName = true;
            msTyped = string.Empty;

            if (pOKeyboard != null)
                fNameEntry().Begin();

            fDrawNameInput();
        }

        if (pOKeyboard == null)
            return;

        if (UWBack.Takes(UWBackOwner.CharacterName))
        {
            Hide();
            return;
        }

        if (UWTextEntry.EnterPressed)
        {
            string lsName = msTyped.Trim();

            if (lsName.Length == 0)
                return;

            fEndTyping();
            mOGeneration.SetName(lsName);
            fRefresh();
            return;
        }

        if (UWTextEntry.BackspacePressed && msTyped.Length > 0)
        {
            msTyped = msTyped.Substring(0, msTyped.Length - 1);
            fDrawNameInput();
        }

        // Where no text events arrive (Linux), the keys themselves (UWTypedKeys).
        fNameEntry().ReadKeys();
    }

    /// <summary>The name's typing (UWTextEntry since 2026-10-19: the text events, the keys where
    /// none arrive, one filter).</summary>
    private UWTextEntry mONameEntry;

    private UWTextEntry fNameEntry()
    {
        if (mONameEntry == null)
            // Printable ASCII and, since translation 5.1, the language's letters a save keeps (the
            // German umlauts and sharp s as their Latin-1 byte in PLAYER.DAT, as the save name and the
            // map notes keep them; the letter grid offers them). In English printable ASCII as before.
            mONameEntry = new UWTextEntry(fOnTextInput, UWTextEntry.IsTypeableAndStorable);

        return mONameEntry;
    }

    private void fOnTextInput(char pcChar)
    {
        if (!IsTypingName)
            return;

        // Up to 29 characters while the pen is still inside the box (pickopt, since 2026-10-12;
        // until then a flat 14). The width is the prompt's and the typed text's, as the original
        // pen moves from string_width(prompt) + 0xA8.
        int liWidth = mOFont != null
            ? mOFont.MeasureText(mOGeneration.QuestionText + msTyped, 0, UWFontRenderer.CharacterSpacing)
            : 0;

        if (!UWCharacterGeneration.AcceptsNameCharacter(msTyped.Length, liWidth))
            return;

        msTyped += pcChar;
        fDrawNameInput();
    }

    private void fEndTyping()
    {
        IsTypingName = false;

        fNameEntry().End();
    }

    private void fDrawNameInput()
    {
        UWScreenUi.SetText(mONameInput, mOFont, msTyped + "_", mOTextColour, LabelWidth, false);
    }

    // ------------------------------------------------------------------
    // Display
    // ------------------------------------------------------------------

    /// <summary>Everything anew after each answer: left page and the question on the right.</summary>
    private void fRefresh()
    {
        fClearButtons();
        miHover = -1;

        UWCharacterGeneration.Stage leStage = mOGeneration.CurrentStage;

        // Left page
        UWScreenUi.SetText(mOName, mOFont, mOGeneration.Name, mOTextColour, LabelWidth, false);
        UWScreenUi.SetText(mOSex, mOFont, leStage > UWCharacterGeneration.Stage.Sex ? mOGeneration.SexName : null,
            mOTextColour, UWClassicFit.Creation.SexClassWidth, false);
        UWScreenUi.SetText(mOClass, mOFont, mOGeneration.ClassName, mOTextColour, UWClassicFit.Creation.SexClassWidth, false);

        if (leStage > UWCharacterGeneration.Stage.Portrait)
        {
            int liBody = (mOGeneration.IsFemale ? FemaleBodyImage : MaleBodyImage) + mOGeneration.Body;
            UWScreenUi.SetTexture(mOBody, fGetImage(liBody, true));
        }
        else
        {
            mOBody.enabled = false;
        }

        if (mOGeneration.CharacterClass >= 0)
        {
            List<string> lOLabels = new List<string>();
            List<string> lOValues = new List<string>();

            for (int liAt = 0; liAt < 4; liAt++)
                lOLabels.Add(mOGeneration.GetAttributeLabel(liAt));

            lOValues.Add(mOGeneration.Strength.ToString());
            lOValues.Add(mOGeneration.Dexterity.ToString());
            lOValues.Add(mOGeneration.Intelligence.ToString());
            lOValues.Add(mOGeneration.MaxVitality.ToString());

            UWScreenUi.SetText(mOStatLabels, mOFont, lOLabels, mOTextColour, UWClassicFit.Creation.StatLabelWidth, false);
            UWScreenUi.SetText(mOStatValues, mOFont, lOValues, mOTextColour, UWClassicFit.Creation.ValueWidth, false);

            List<string> lOSkillLabels = new List<string>();
            List<string> lOSkillValues = new List<string>();

            for (int liSkill = 0; liSkill < UWCharacterGeneration.SkillNumberCount; liSkill++)
            {
                int liValue = mOGeneration.GetSkill(liSkill);

                if (liValue == 0)
                    continue;

                lOSkillLabels.Add(mOGeneration.GetSkillName(liSkill));
                lOSkillValues.Add(liValue.ToString());
            }

            UWScreenUi.SetText(mOSkillLabels, mOFont, lOSkillLabels, mOTextColour, UWClassicFit.Creation.SkillLabelWidth, false);
            UWScreenUi.SetText(mOSkillValues, mOFont, lOSkillValues, mOTextColour, UWClassicFit.Creation.ValueWidth, false);
        }
        else
        {
            UWTextLabel.Hide(mOStatLabels);
            UWTextLabel.Hide(mOStatValues);
            UWTextLabel.Hide(mOSkillLabels);
            UWTextLabel.Hide(mOSkillValues);
        }

        // Right page
        mONameBox.enabled = false;
        UWTextLabel.Hide(mONamePrompt);
        UWTextLabel.Hide(mONameInput);

        switch (leStage)
        {
            case UWCharacterGeneration.Stage.Portrait:
                UWTextLabel.Hide(mOQuestion);
                fBuildPortraitButtons();
                break;

            case UWCharacterGeneration.Stage.Name:
                UWTextLabel.Hide(mOQuestion);
                UWScreenUi.SetTexture(mONameBox, fGetImage(NameBoxImage, false));
                UWScreenUi.SetText(mONamePrompt, mOFont, mOGeneration.QuestionText, mOTextColour, UWClassicFit.Creation.NamePromptWidth, false);

                // THE TYPED NAME STARTS WHERE THE PROMPT ENDS (2026-10-06, per user: "der Text ist
                // zu nah am Doppelpunkt"): the prompt string of the game is "Name: " with its own
                // trailing space, so the input goes right after the measured prompt - as if both
                // were one line, which is how the original draws them - instead of the fixed 41.
                UWScreenUi.Place(mONameInput,
                    NamePromptLeft + mOFont.MeasureText(mOGeneration.QuestionText, 0, UWFontRenderer.CharacterSpacing), NameTextTop);
                fDrawNameInput();
                break;

            case UWCharacterGeneration.Stage.Done:
                UWTextLabel.Hide(mOQuestion);
                break;

            default:
                fBuildChoiceButtons();
                break;
        }
    }

    private void fBuildChoiceButtons()
    {
        int liCount = mOGeneration.Choices.Count;
        int liColumns = liCount <= SingleColumnLimit ? 1 : 2;
        int liRows = liCount <= SingleColumnLimit ? liCount : liCount / 2;

        for (int liAt = 0; liAt < liCount; liAt++)
        {
            int liLeft = liColumns == 1 ? SingleColumnLeft : (liAt % liColumns == 0 ? FirstColumnLeft : SecondColumnLeft);
            int liTop = fRowTop(liRows, liAt / liColumns);

            Button lOButton = fCreateButton(liLeft, liTop, ButtonImage);

            lOButton.Label = UWUiKit.CreateRawImageAt((RectTransform)lOButton.Image.transform, "Text",
                0, (ButtonHeight - mOFont.Height) / 2);
            UWScreenUi.SetText(lOButton.Label, mOFont, mOGeneration.Choices[liAt], mOTextColour, ButtonWidth, true);

            fAddFocus(lOButton, ButtonFocusImage);
            mOButtons.Add(lOButton);
        }

        UWScreenUi.Place(mOQuestion, QuestionLeft, fRowTop(liRows, -1));
        UWScreenUi.SetText(mOQuestion, mOFont, mOGeneration.QuestionText, mOTextColour, QuestionWidth, true);
    }

    /// <summary>The reference's row formula, rounded.</summary>
    private static int fRowTop(int piRows, int piRow)
    {
        return Mathf.RoundToInt(RowCentre - (((piRows / 2) - piRow) * RowPitch));
    }

    private void fBuildPortraitButtons()
    {
        int liFirst = mOGeneration.IsFemale ? FemalePortraitImage : MalePortraitImage;

        for (int liAt = 0; liAt < UWCharacterGeneration.PortraitCount; liAt++)
        {
            int liTop = PortraitCentre - ((2 - liAt) * PortraitPitch);

            Button lOButton = fCreateButton(SingleColumnLeft, liTop, PortraitFrameImage);

            lOButton.Portrait = UWUiKit.CreateRawImageAt((RectTransform)lOButton.Image.transform, "Face", 0, 0);
            UWScreenUi.SetTexture(lOButton.Portrait, fGetImage(liFirst + liAt, true));

            fAddFocus(lOButton, PortraitFrameFocusImage);
            mOButtons.Add(lOButton);
        }
    }

    /// <summary>The button itself, opaque - the black outline is part of it.</summary>
    private Button fCreateButton(int piLeft, int piTop, int piImage)
    {
        Button lOButton = new Button
        {
            Image = UWUiKit.CreateRawImageAt(mOFrame, "Answer", piLeft, piTop)
        };

        UWScreenUi.SetTexture(lOButton.Image, fGetImage(piImage, false));

        return lOButton;
    }

    /// <summary>The focus frame as the last child, so it lies above text and face.
    /// Transparent inside, visible only under the pointer.</summary>
    private void fAddFocus(Button pOButton, int piImage)
    {
        pOButton.Focus = UWUiKit.CreateRawImageAt((RectTransform)pOButton.Image.transform, "Focus", 0, 0);
        UWScreenUi.SetTexture(pOButton.Focus, fGetImage(piImage, true));
        pOButton.Focus.enabled = false;
    }

    private void fClearButtons()
    {
        // The labels' texts go back to UWTextTextures with the labels (UWTextLabel.OnDestroy); the
        // service owns the textures, so they are not destroyed here (cleanup step 5.5, 2026-10-19).
        foreach (Button lOButton in mOButtons)
            Destroy(lOButton.Image.gameObject);

        mOButtons.Clear();
    }

    /// <summary>An image from CHRBTNS.GR, built once per image and mode - opaque, or with
    /// transparent index 0 (see UWScreenUi.BuildTexture).</summary>
    private Texture2D fGetImage(int piIndex, bool pbZeroTransparent)
    {
        int liKey = (piIndex * 2) + (pbZeroTransparent ? 1 : 0);
        Texture2D lOTexture;

        if (mOImages.TryGetValue(liKey, out lOTexture))
            return lOTexture;

        lOTexture = UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.CHRBTNS, piIndex, meFilter, pbZeroTransparent);
        mOImages[liKey] = lOTexture;

        return lOTexture;
    }

    private void fSetModal(bool pbModal)
    {
        if (mOControls == null)
            mOControls = GetComponentInParent<UWControlScheme>();

        if (mOControls == null)
            mOControls = UWScene.ControlScheme;

        if (mOControls != null)
            mOControls.SetUiModal("character creation", pbModal);

        if (pbModal)
            UWPointer.IsShown = true;
    }

    private void fEnsureCanvas()
    {
        if (mOCanvas != null)
            return;

        mOFont = mOData.Fonts.Get(UWFonts.FontType.CharacterGeneration);

        mOCanvas = UWScreenUi.CreateCanvas(transform, "UW Character Creation", UWUiSorting.CharacterCreation, out mOFrame);

        mOBackground = UWUiKit.CreateRawImageAt(mOFrame, "Book", 0, 0);
        UWScreenUi.SetTexture(mOBackground, UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.CHARGEN, 0, meFilter));

        mOName = UWUiKit.CreateRawImageAt(mOFrame, "Name", NameLeft, NameTop);
        mOSex = UWUiKit.CreateRawImageAt(mOFrame, "Sex", SexLeft, SexClassTop);
        mOClass = UWUiKit.CreateRawImageAt(mOFrame, "Class", ClassLeft, SexClassTop);
        mOBody = UWUiKit.CreateRawImageAt(mOFrame, "Body", BodyLeft, BodyTop);
        mOStatLabels = UWUiKit.CreateRawImageAt(mOFrame, "Attributes", StatLabelLeft, StatTop);
        mOStatValues = UWUiKit.CreateRawImageAt(mOFrame, "AttributeValues", StatValueLeft, StatTop);
        mOSkillLabels = UWUiKit.CreateRawImageAt(mOFrame, "Skills", SkillLabelLeft, SkillTop);
        mOSkillValues = UWUiKit.CreateRawImageAt(mOFrame, "SkillValues", SkillValueLeft, SkillTop);

        mOQuestion = UWUiKit.CreateRawImageAt(mOFrame, "Question", QuestionLeft, 0);

        mONameBox = UWUiKit.CreateRawImageAt(mOFrame, "NameBox", NameBoxLeft, NameBoxTop);
        mONamePrompt = UWUiKit.CreateRawImageAt((RectTransform)mONameBox.transform, "Name:", NamePromptLeft, NameTextTop);
        mONameInput = UWUiKit.CreateRawImageAt((RectTransform)mONameBox.transform, "Input", NameInputLeft, NameTextTop);

        mOCanvas.gameObject.SetActive(false);
    }
}
