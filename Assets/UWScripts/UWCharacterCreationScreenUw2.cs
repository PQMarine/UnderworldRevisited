using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S CHARACTER CREATION (UW2 plan phase 3 step 5, 2026-10-29), as Underworld Exhumed's
/// uw2 game/CHARGEN.C draws it: BYT.ARK's screen 1 in palette 3, the question and its answers on the right
/// page (CHRBTNS.GR 0, lit 1; the portraits in frame 3, lit 4), laid out by drawopt's rule
/// (UWChargenLayoutUw2), what has been chosen on the left page (sex and class on one line, the
/// attributes, up to six skills, the body, the name), FONTCHAR.SYS in colour 0xC4. The questions and the
/// dice are the first game's model (UWCharacterGeneration): both games share CHRGEN.DAT byte for byte and
/// SKILLS.DAT's form, and add_to_skill, roll_stats and player_compute are the same rules (SKILLS.C,
/// CHARGEN.C). The second game's handedness answers are Right, Left, and it stores lefty = 1 - choice
/// (CHARGEN.C:591) - the model's IsLeftHanded, which is choice 0, is that bit.
///
/// The input as pickopt and mousopt: a press follows the pointer and lights the answer under it, letting
/// go on one chooses it; the cursor keys move the light (up and down by a row), Home and Page Up to the
/// first, End and Page Down to the last, Enter chooses; Escape (Alt+X) starts again from the first
/// question, or leaves for the main menu from there. The name takes printable characters up to 29 while
/// they fit the box, no leading space, Backspace, Enter with a name - no Escape, no cursor (pickopt
/// draws none). "Yes" to keeping the character shows its name and "awakens . . ." on the book and starts
/// the walk with that character (part B: UWSaveCodec.StartNewCharacterUw2 builds its PLAYER.DAT).
/// </summary>
public class UWCharacterCreationScreenUw2 : MonoBehaviour
{
    public static bool IsAnyOpen { get; private set; }

    /// <summary>The name is typed: Escape and the letter keys are the name's (UWBack, UWLetterGrid).</summary>
    public static bool IsTypingName { get; private set; }

    /// <summary>"Yes" started the walk: the main menu stays away while the scene starts again.</summary>
    public static bool IsStartingWalk { get; private set; }

    /// <summary>The character "Yes" kept, across the scene's start (part B): the walk takes it once
    /// (UWLevelLoader, UWSaveCodec.StartNewCharacterUw2).</summary>
    private static UWPlayerData.NewCharacter msPendingCharacter;

    public static UWPlayerData.NewCharacter TakePendingCharacter()
    {
        UWPlayerData.NewCharacter lOCharacter = msPendingCharacter;

        msPendingCharacter = null;

        return lOCharacter;
    }

    public bool IsOpen { get; private set; }

    /// <summary>How long the book shows the name and "awakens . . ." before the walk loads - the original
    /// shows it while it loads the level.</summary>
    public const float AwakensSeconds = 1.5f;

    private class Answer
    {
        public RawImage Image;

        public RawImage Label;

        public RawImage Portrait;
    }

    private DataImport mOData;

    private UWCharacterGeneration mOGeneration;

    private Canvas mOCanvas;

    private RectTransform mOFrame;

    private RawImage mOBackground;

    private RawImage mOQuestion;

    private RawImage mONameBox;

    private RawImage mONamePrompt;

    private RawImage mONameTyped;

    private RawImage mOSex;

    private RawImage mOClass;

    private RawImage mOName;

    private RawImage mOBody;

    private readonly RawImage[] mOAttributeLabels = new RawImage[UWCharacterGeneration.AttributeLabelCount];

    private readonly RawImage[] mOAttributeValues = new RawImage[UWCharacterGeneration.AttributeLabelCount];

    private readonly RawImage[] mOSkillLabels = new RawImage[UWClassicLayoutUw2.Chargen.SkillRows];

    private readonly RawImage[] mOSkillValues = new RawImage[UWClassicLayoutUw2.Chargen.SkillRows];

    private RawImage mOAwakensName;

    private RawImage mOAwakens;

    private readonly List<Answer> mOAnswers = new List<Answer>();

    private readonly Dictionary<int, Texture2D> mOImages = new Dictionary<int, Texture2D>();

    private UWFont mOFont;

    private Color32 mOTextColour;

    private FilterMode meFilter = FilterMode.Point;

    private UWChargenLayoutUw2.Arrangement mOArrangement;

    /// <summary>The lit answer, and the stage it belongs to (a new stage lights its first).</summary>
    private int miSelected;

    private bool mbPress;

    private string msTyped = string.Empty;

    private UWTextEntry mONameEntry;

    private float mfAwakensTime = -1f;

    private UWControlScheme mOControls;

    public void Init(DataImport pOData)
    {
        IsAnyOpen = false;
        IsTypingName = false;

        mOData = pOData;
        mOGeneration = new UWCharacterGeneration(pOData.Strings, pOData.MiscDataFiles);
        mOTextColour = UWScreenUi.GetColour(pOData.Palettes, UWClassicLayoutUw2.Chargen.Palette, UWClassicLayoutUw2.Chargen.TextColour);

        if (UnderworldRevisited.UWSettings.Instance != null)
            meFilter = UnderworldRevisited.UWSettings.Instance.TextureFilterMode;
    }

    public void Show()
    {
        if (mOData == null || mOGeneration == null || !mOGeneration.IsLoaded)
        {
            Debug.LogWarning("[Creation] CHRGEN.DAT or SKILLS.DAT of the second game is missing.");
            return;
        }

        fEnsureCanvas();

        IsStartingWalk = false;
        mfAwakensTime = -1f;

        // The first game's deviation (per user, 2026-10-01): the bonus points dealt by hand when chosen.
        mOGeneration.ManualAttributes = UWUserSettings.ManualAttributes;
        mOGeneration.Begin();
        mOCanvas.gameObject.SetActive(true);
        IsOpen = true;
        IsAnyOpen = true;

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

        fSetModal(false);
        UWGameClock.Release(UWGameClock.CharacterCreationHold);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (mfAwakensTime >= 0f)
        {
            if (Time.unscaledTime - mfAwakensTime >= AwakensSeconds)
                fStartWalk();

            return;
        }

        if (mOGeneration.CurrentStage == UWCharacterGeneration.Stage.Name)
        {
            fUpdateTyping();

            return;
        }

        if (IsTypingName)
            fEndTyping();

        Keyboard lOKeys = Keyboard.current;

        if (UWBack.Takes(UWBackOwner.CharacterCreation) || (lOKeys != null && lOKeys.altKey.isPressed && lOKeys.xKey.wasPressedThisFrame))
        {
            fBack();

            return;
        }

        if (fFollowPress())
            return;

        if (lOKeys == null || mOArrangement.Count == 0)
            return;

        int liColumns = mOArrangement.Columns;
        int liSelected = miSelected;

        if (lOKeys.downArrowKey.wasPressedThisFrame)
            liSelected = UWChargenLayoutUw2.Move(mOArrangement, miSelected, liColumns);
        else if (lOKeys.upArrowKey.wasPressedThisFrame)
            liSelected = UWChargenLayoutUw2.Move(mOArrangement, miSelected, -liColumns);
        else if (lOKeys.rightArrowKey.wasPressedThisFrame)
            liSelected = UWChargenLayoutUw2.Move(mOArrangement, miSelected, 1);
        else if (lOKeys.leftArrowKey.wasPressedThisFrame)
            liSelected = UWChargenLayoutUw2.Move(mOArrangement, miSelected, -1);
        else if (lOKeys.homeKey.wasPressedThisFrame || lOKeys.pageUpKey.wasPressedThisFrame)
            liSelected = 0;
        else if (lOKeys.endKey.wasPressedThisFrame || lOKeys.pageDownKey.wasPressedThisFrame)
            liSelected = mOArrangement.Count - 1;
        else if (UWTextEntry.EnterPressed)
        {
            fChoose(miSelected);

            return;
        }

        fLight(liSelected);
    }

    /// <summary>Escape (gen_char's choice -1): from the first question back to the main menu, from any
    /// other the creation starts again with the left page cleared.</summary>
    private void fBack()
    {
        if (mOGeneration.CurrentStage == UWCharacterGeneration.Stage.Sex)
        {
            Hide();

            return;
        }

        mOGeneration.Begin();
        fRefresh();
    }

    /// <summary>
    /// mousopt: a press lights the answer under the pointer while it is held (the last one lit stays lit
    /// off the answers), and letting go over an answer chooses it. True while a press is followed.
    /// </summary>
    private bool fFollowPress()
    {
        Mouse lOMouse = Mouse.current;

        if (lOMouse == null || !UWPointer.HasMouse)
            return false;

        if (!mbPress && (lOMouse.leftButton.wasPressedThisFrame || lOMouse.rightButton.wasPressedThisFrame || lOMouse.middleButton.wasPressedThisFrame))
            mbPress = true;

        if (!mbPress)
            return false;

        int liAnswer = fAnswerUnderPointer();

        if (liAnswer >= 0)
            fLight(liAnswer);

        if (lOMouse.leftButton.isPressed || lOMouse.rightButton.isPressed || lOMouse.middleButton.isPressed)
            return true;

        mbPress = false;

        if (liAnswer >= 0)
            fChoose(liAnswer);

        return true;
    }

    private int fAnswerUnderPointer()
    {
        if (mOArrangement.Count == 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(mOFrame, UWPointer.Position, null, out Vector2 lOLocal))
            return -1;

        Rect lORect = mOFrame.rect;
        int liX = Mathf.FloorToInt(lOLocal.x - lORect.xMin);
        int liRow = Mathf.FloorToInt(lORect.yMax - lOLocal.y);

        return UWChargenLayoutUw2.AnswerAt(mOArrangement, liX, UWScreenUi.ScreenHeight - 1 - liRow);
    }

    private void fChoose(int piChoice)
    {
        UWCharacterGeneration.Stage leBefore = mOGeneration.CurrentStage;

        mOGeneration.Choose(piChoice);

        if (leBefore == UWCharacterGeneration.Stage.Confirm && mOGeneration.CurrentStage == UWCharacterGeneration.Stage.Done)
        {
            fShowAwakens();

            return;
        }

        fRefresh();
    }

    /// <summary>The book alone, the name and "awakens . . ." centred on the right page (gen_char's end),
    /// for AwakensSeconds.</summary>
    private void fShowAwakens()
    {
        fClearAnswers();
        fClearLeftPage();
        UWTextLabel.Hide(mOQuestion);
        mONameBox.enabled = false;
        UWTextLabel.Hide(mONamePrompt);
        UWTextLabel.Hide(mONameTyped);

        string lsAwakens = UWText.Block(mOData, 1, UWClassicLayoutUw2.Chargen.AwakensString + 1, string.Empty);
        int liRightCentre = UWClassicLayoutUw2.Chargen.RightPage + (UWClassicLayoutUw2.Chargen.RightPage / 2);

        fText(mOAwakensName, mOGeneration.Name, liRightCentre - (fWidth(mOGeneration.Name) / 2), UWClassicLayoutUw2.Chargen.AwakensY + mOFont.Height);
        fText(mOAwakens, lsAwakens, liRightCentre - (fWidth(lsAwakens) / 2), UWClassicLayoutUw2.Chargen.AwakensY);

        mfAwakensTime = Time.unscaledTime;
    }

    private void fStartWalk()
    {
        Debug.Log(string.Format("[Creation] {0}, {1} {2}, Str {3} Dex {4} Int {5}, vitality {6}, mana {7} - starting the walk.",
            mOGeneration.Name, mOGeneration.SexName, mOGeneration.ClassName,
            mOGeneration.Strength, mOGeneration.Dexterity, mOGeneration.Intelligence, mOGeneration.MaxVitality, mOGeneration.MaxMana));

        IsStartingWalk = true;
        msPendingCharacter = mOGeneration.Result();
        mfAwakensTime = -1f;

        Hide();
        UWWalkUw2.Request();
    }

    // ------------------------------------------------- The name

    private void fUpdateTyping()
    {
        if (!IsTypingName)
        {
            IsTypingName = true;
            msTyped = string.Empty;
            fNameEntry().Begin();
            fShowTyped();
        }

        // pickopt reads no Escape in the name: the press is the name's and goes nowhere.
        UWBack.Takes(UWBackOwner.CharacterName);

        if (UWTextEntry.EnterPressed)
        {
            if (msTyped.Length == 0)
                return;

            fEndTyping();
            mOGeneration.SetName(msTyped);
            fRefresh();

            return;
        }

        if (UWTextEntry.BackspacePressed && msTyped.Length > 0)
        {
            msTyped = msTyped.Substring(0, msTyped.Length - 1);
            fShowTyped();
        }

        // Where no text events arrive (Linux), the keys themselves (UWTypedKeys).
        fNameEntry().ReadKeys();
    }

    private UWTextEntry fNameEntry()
    {
        if (mONameEntry == null)
            mONameEntry = new UWTextEntry(fOnTyped, UWTextEntry.IsTypeableAndStorable);

        return mONameEntry;
    }

    /// <summary>A typed character (pickopt): no space before the first, up to 29 while the pen stays left of
    /// 0x12E (UWCharacterGeneration.AcceptsNameCharacter).</summary>
    private void fOnTyped(char pcChar)
    {
        if (!IsTypingName || (msTyped.Length == 0 && char.IsWhiteSpace(pcChar)))
            return;

        if (!UWCharacterGeneration.AcceptsNameCharacter(msTyped.Length, fWidth(mOGeneration.QuestionText + msTyped)))
            return;

        msTyped += pcChar;
        fShowTyped();
    }

    private void fShowTyped()
    {
        int liY = fNameBoxY() - UWClassicLayoutUw2.Chargen.TextDrop;

        fText(mONameTyped, msTyped, UWCharacterGeneration.NameInputLeftX + fWidth(mOGeneration.QuestionText), liY);
    }

    private void fEndTyping()
    {
        IsTypingName = false;

        if (mONameEntry != null)
            mONameEntry.End();
    }

    /// <summary>The name box's top: drawopt's y for a question without answers (0x6B).</summary>
    private static int fNameBoxY()
    {
        return UWChargenLayoutUw2.Arrange(0, 1, UWClassicLayoutUw2.Chargen.AnswerHeight, true).QuestionY;
    }

    // ------------------------------------------------- Drawing

    /// <summary>The left page as chosen so far and the right page's question (gen_char's loop).</summary>
    private void fRefresh()
    {
        fClearAnswers();
        fShowLeftPage();

        UWCharacterGeneration.Stage leStage = mOGeneration.CurrentStage;

        mONameBox.enabled = false;
        UWTextLabel.Hide(mONamePrompt);
        UWTextLabel.Hide(mONameTyped);
        UWTextLabel.Hide(mOQuestion);
        mOArrangement = default(UWChargenLayoutUw2.Arrangement);

        if (leStage == UWCharacterGeneration.Stage.Name)
        {
            // The name box (CHRBTNS.GR 6) where the question stands, "Name:" four columns in.
            int liY = fNameBoxY();

            fPicture(mONameBox, UWClassicLayoutUw2.Chargen.NameBoxPicture, false, UWClassicLayoutUw2.Chargen.QuestionLeft, liY);
            fText(mONamePrompt, mOGeneration.QuestionText, UWClassicLayoutUw2.Chargen.QuestionLeft + UWClassicLayoutUw2.Chargen.NamePromptIndent,
                liY - UWClassicLayoutUw2.Chargen.TextDrop);

            return;
        }

        if (leStage == UWCharacterGeneration.Stage.Done)
            return;

        bool lbPortraits = leStage == UWCharacterGeneration.Stage.Portrait;
        int liPicture = lbPortraits ? UWClassicLayoutUw2.Chargen.PortraitFrame : UWClassicLayoutUw2.Chargen.AnswerPicture;
        Texture2D lOPlain = fImage(liPicture, false);

        if (lOPlain == null)
            return;

        bool lbQuestion = !string.IsNullOrEmpty(mOGeneration.QuestionText);

        mOArrangement = UWChargenLayoutUw2.Arrange(mOGeneration.Choices.Count, lOPlain.width, lOPlain.height, lbQuestion);

        if (lbQuestion)
            fText(mOQuestion, mOGeneration.QuestionText,
                UWClassicLayoutUw2.Chargen.QuestionLeft + ((UWClassicLayoutUw2.Chargen.QuestionWidth - fWidth(mOGeneration.QuestionText)) / 2),
                mOArrangement.QuestionY - UWClassicLayoutUw2.Chargen.TextDrop);

        int liFirstPortrait = mOGeneration.IsFemale ? UWClassicLayoutUw2.Chargen.FemalePortraits : UWClassicLayoutUw2.Chargen.MalePortraits;

        for (int liAt = 0; liAt < mOArrangement.Count; liAt++)
        {
            UWChargenLayoutUw2.Place(mOArrangement, liAt, out int liX, out int liY);

            Answer lOAnswer = new Answer { Image = UWUiKit.CreateRawImageAt(mOFrame, "Answer", liX, fRow(liY)) };

            UWScreenUi.SetTexture(lOAnswer.Image, lOPlain);

            if (lbPortraits)
            {
                lOAnswer.Portrait = UWUiKit.CreateRawImageAt(lOAnswer.Image.transform, "Face", 0, 0);
                UWScreenUi.SetTexture(lOAnswer.Portrait, fImage(liFirstPortrait + liAt, true));
            }
            else
            {
                string lsText = mOGeneration.Choices[liAt];

                lOAnswer.Label = UWUiKit.CreateRawImageAt(lOAnswer.Image.transform, "Text",
                    (mOArrangement.Width - fWidth(lsText)) / 2, UWClassicLayoutUw2.Chargen.TextDrop);
                UWScreenUi.SetText(lOAnswer.Label, mOFont, lsText, mOTextColour, mOArrangement.Width, false);
            }

            mOAnswers.Add(lOAnswer);
        }

        // A new question lights its first answer (gen_char's selopt(&opts[stage], 0, 0xFF)); the manual
        // dealing keeps its light while its points change.
        miSelected = leStage == UWCharacterGeneration.Stage.Attributes ? Mathf.Clamp(miSelected, 0, mOArrangement.Count - 1) : 0;
        fLight(miSelected, true);
    }

    /// <summary>selopt: the old answer back to its plain picture, the new one lit.</summary>
    private void fLight(int piAnswer, bool pbForce = false)
    {
        if (piAnswer < 0 || piAnswer >= mOAnswers.Count || (piAnswer == miSelected && !pbForce))
            return;

        bool lbPortraits = mOGeneration.CurrentStage == UWCharacterGeneration.Stage.Portrait;
        int liPicture = lbPortraits ? UWClassicLayoutUw2.Chargen.PortraitFrame : UWClassicLayoutUw2.Chargen.AnswerPicture;

        if (miSelected >= 0 && miSelected < mOAnswers.Count)
            UWScreenUi.SetTexture(mOAnswers[miSelected].Image, fImage(liPicture, false));

        miSelected = piAnswer;
        UWScreenUi.SetTexture(mOAnswers[miSelected].Image, fImage(liPicture + UWClassicLayoutUw2.Chargen.LitPicture, false));
    }

    /// <summary>
    /// The left page (gen_char after each answer): the sex at the left of the top line and the class at
    /// its right, the attributes and vitality (show_atts), the skills held (show_skills, six at most), the
    /// body in its box, the name centred at the top.
    /// </summary>
    private void fShowLeftPage()
    {
        fClearLeftPage();

        UWCharacterGeneration.Stage leStage = mOGeneration.CurrentStage;

        if (leStage > UWCharacterGeneration.Stage.Sex)
            fText(mOSex, mOGeneration.SexName, UWClassicLayoutUw2.Chargen.SexLeft, UWClassicLayoutUw2.Chargen.SexClassY);

        if (mOGeneration.CharacterClass >= 0)
        {
            string lsClass = mOGeneration.ClassName;

            fText(mOClass, lsClass, UWClassicLayoutUw2.Chargen.ClassRight - fWidth(lsClass), UWClassicLayoutUw2.Chargen.SexClassY);

            int[] liValues = { mOGeneration.Strength, mOGeneration.Dexterity, mOGeneration.Intelligence, mOGeneration.MaxVitality };

            for (int liAt = 0; liAt < liValues.Length; liAt++)
            {
                int liY = UWClassicLayoutUw2.Chargen.AttributeY - (liAt * UWClassicLayoutUw2.Chargen.AttributeStep);
                string lsValue = liValues[liAt].ToString();

                fText(mOAttributeLabels[liAt], mOGeneration.GetAttributeLabel(liAt), UWClassicLayoutUw2.Chargen.AttributeLeft, liY);
                fText(mOAttributeValues[liAt], lsValue, UWClassicLayoutUw2.Chargen.AttributeRight - fWidth(lsValue), liY);
            }

            int liRow = 0;

            for (int liSkill = 0; liSkill < UWCharacterGeneration.SkillNumberCount && liRow < UWClassicLayoutUw2.Chargen.SkillRows; liSkill++)
            {
                int liValue = mOGeneration.GetSkill(liSkill);

                if (liValue == 0)
                    continue;

                int liY = UWClassicLayoutUw2.Chargen.SkillY - (liRow * UWClassicLayoutUw2.Chargen.SkillStep);
                string lsValue = liValue.ToString();

                fText(mOSkillLabels[liRow], mOGeneration.GetSkillName(liSkill), UWClassicLayoutUw2.Chargen.SkillLeft, liY);
                fText(mOSkillValues[liRow], lsValue, UWClassicLayoutUw2.Chargen.SkillRight - fWidth(lsValue), liY);
                liRow++;
            }
        }

        if (leStage > UWCharacterGeneration.Stage.Portrait)
        {
            Texture2D lOBody = fImage(UWClassicLayoutUw2.Chargen.FirstBody + ((mOGeneration.IsFemale ? 1 : 0) * UWCharacterGeneration.PortraitCount)
                + mOGeneration.Body, true);

            if (lOBody != null)
            {
                UWScreenUi.SetTexture(mOBody, lOBody);
                UWScreenUi.Place(mOBody, UWClassicLayoutUw2.Chargen.BodyLeft + ((UWClassicLayoutUw2.Chargen.BodyWidth - lOBody.width) / 2),
                    fRow(UWClassicLayoutUw2.Chargen.BodyTop - ((UWClassicLayoutUw2.Chargen.BodyHeight - lOBody.height) / 2)));
            }
        }

        if (!string.IsNullOrEmpty(mOGeneration.Name))
            fText(mOName, mOGeneration.Name,
                UWClassicLayoutUw2.Chargen.NameLeft + ((UWClassicLayoutUw2.Chargen.NameWidth - fWidth(mOGeneration.Name)) / 2), UWClassicLayoutUw2.Chargen.NameY);
    }

    private void fClearLeftPage()
    {
        UWTextLabel.Hide(mOSex);
        UWTextLabel.Hide(mOClass);
        UWTextLabel.Hide(mOName);
        mOBody.enabled = false;

        for (int liAt = 0; liAt < mOAttributeLabels.Length; liAt++)
        {
            UWTextLabel.Hide(mOAttributeLabels[liAt]);
            UWTextLabel.Hide(mOAttributeValues[liAt]);
        }

        for (int liAt = 0; liAt < mOSkillLabels.Length; liAt++)
        {
            UWTextLabel.Hide(mOSkillLabels[liAt]);
            UWTextLabel.Hide(mOSkillValues[liAt]);
        }

        UWTextLabel.Hide(mOAwakensName);
        UWTextLabel.Hide(mOAwakens);
    }

    private void fClearAnswers()
    {
        foreach (Answer lOAnswer in mOAnswers)
            Destroy(lOAnswer.Image.gameObject);

        mOAnswers.Clear();
        mbPress = false;
    }

    /// <summary>A text with its top left at x and y counted up (string_to_screen).</summary>
    private void fText(RawImage pOImage, string psText, int piX, int piY)
    {
        if (string.IsNullOrEmpty(psText))
        {
            UWTextLabel.Hide(pOImage);

            return;
        }

        UWScreenUi.Place(pOImage, piX, fRow(piY));
        UWScreenUi.SetText(pOImage, mOFont, psText, mOTextColour, fWidth(psText) + 1, false);
    }

    private void fPicture(RawImage pOImage, int piIndex, bool pbTransparent, int piX, int piY)
    {
        UWScreenUi.SetTexture(pOImage, fImage(piIndex, pbTransparent));
        UWScreenUi.Place(pOImage, piX, fRow(piY));
    }

    /// <summary>A y counted up as the frame's row from the top.</summary>
    private static int fRow(int piY)
    {
        return UWScreenUi.ScreenHeight - 1 - piY;
    }

    private int fWidth(string psText)
    {
        return mOFont != null && !string.IsNullOrEmpty(psText) ? mOFont.MeasureText(psText, 0, UWFontRenderer.CharacterSpacing) : 0;
    }

    /// <summary>A CHRBTNS.GR picture, built once per picture and mode: the buttons opaque (show with
    /// Transparency 0), the portraits and bodies with index 0 clear (Transparency 1).</summary>
    private Texture2D fImage(int piIndex, bool pbZeroTransparent)
    {
        int liKey = (piIndex * 2) + (pbZeroTransparent ? 1 : 0);

        if (mOImages.TryGetValue(liKey, out Texture2D lOTexture))
            return lOTexture;

        lOTexture = fBuild(UWTexture.TextureTypes.CHRBTNS, piIndex, pbZeroTransparent);
        mOImages[liKey] = lOTexture;

        return lOTexture;
    }

    /// <summary>
    /// A picture as the second game draws it (as UWGameScreenUw2.fPicture builds its own): every pixel
    /// opaque, or index 0 cleared where it is drawn transparent. The menu screens' builder
    /// (UWScreenUi.BuildTexture) takes each colour's alpha from the palette, which keeps the first game's
    /// transparent index - in the second game's art index 0 is black where a picture is drawn opaque (the
    /// buttons' outline), and parts of the book and the buttons showed the backdrop through them.
    /// </summary>
    private Texture2D fBuild(UWTexture.TextureTypes peType, int piIndex, bool pbZeroTransparent)
    {
        UWTexture lOPicture;

        try
        {
            lOPicture = mOData.Textures != null ? mOData.Textures.GetTextureByType(peType, piIndex) : null;
        }
        catch (System.ArgumentOutOfRangeException)
        {
            lOPicture = null;
        }

        if (lOPicture == null || lOPicture.PaletteIndices == null || lOPicture.Width <= 0 || lOPicture.Height <= 0)
            return null;

        byte[] lyRgba = UWUploadPixels.FromTexture(lOPicture, UWColourVision.Filter);
        byte[] lyIndices = lOPicture.PaletteIndices;
        int liWidth = lOPicture.Width;
        int liHeight = lOPicture.Height;

        for (int liRow = 0; liRow < liHeight; liRow++)
        {
            for (int liColumn = 0; liColumn < liWidth; liColumn++)
            {
                int liIndex = (liRow * liWidth) + liColumn;
                bool lbClear = pbZeroTransparent && liIndex < lyIndices.Length && lyIndices[liIndex] == 0;

                lyRgba[((((liHeight - 1 - liRow) * liWidth) + liColumn) * UWUploadPixels.BytesPerPixel) + 3] = lbClear ? (byte)0 : (byte)255;
            }
        }

        return UWTextureFactory.Upload(lyRgba, liWidth, liHeight, meFilter, TextureWrapMode.Clamp, "UW2 creation " + peType + " " + piIndex);
    }

    private void fSetModal(bool pbModal)
    {
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
        mOCanvas = UWScreenUi.CreateCanvas(transform, "UW2 Character Creation", UWUiSorting.CharacterCreation, out mOFrame);

        mOBackground = UWUiKit.CreateRawImageAt(mOFrame, "Book", 0, 0);
        UWScreenUi.SetTexture(mOBackground, fBuild(UWTexture.TextureTypes.CHARGEN, 0, false));

        mOBody = UWUiKit.CreateRawImageAt(mOFrame, "Body", 0, 0);
        mOSex = UWUiKit.CreateRawImageAt(mOFrame, "Sex", 0, 0);
        mOClass = UWUiKit.CreateRawImageAt(mOFrame, "Class", 0, 0);
        mOName = UWUiKit.CreateRawImageAt(mOFrame, "Name", 0, 0);

        for (int liAt = 0; liAt < mOAttributeLabels.Length; liAt++)
        {
            mOAttributeLabels[liAt] = UWUiKit.CreateRawImageAt(mOFrame, "Attribute", 0, 0);
            mOAttributeValues[liAt] = UWUiKit.CreateRawImageAt(mOFrame, "AttributeValue", 0, 0);
        }

        for (int liAt = 0; liAt < mOSkillLabels.Length; liAt++)
        {
            mOSkillLabels[liAt] = UWUiKit.CreateRawImageAt(mOFrame, "Skill", 0, 0);
            mOSkillValues[liAt] = UWUiKit.CreateRawImageAt(mOFrame, "SkillValue", 0, 0);
        }

        mOQuestion = UWUiKit.CreateRawImageAt(mOFrame, "Question", 0, 0);
        mONameBox = UWUiKit.CreateRawImageAt(mOFrame, "NameBox", 0, 0);
        mONamePrompt = UWUiKit.CreateRawImageAt(mOFrame, "NamePrompt", 0, 0);
        mONameTyped = UWUiKit.CreateRawImageAt(mOFrame, "NameTyped", 0, 0);
        mOAwakensName = UWUiKit.CreateRawImageAt(mOFrame, "AwakensName", 0, 0);
        mOAwakens = UWUiKit.CreateRawImageAt(mOFrame, "Awakens", 0, 0);

        mONameBox.enabled = false;
        mOBody.enabled = false;
        mOCanvas.gameObject.SetActive(false);
    }
}

