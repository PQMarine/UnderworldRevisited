using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S QUESTION BOX (per user, 2026-10-04: every question there alike, as the
/// split box): a yes/no question of the game (Interaction.fAskYesNo - the anvil's repair, the
/// trap found on a look) shows in a leather box in the middle of the screen, its text - the
/// repair's estimate - and OK and Cancel. The left button on a button answers, the right button
/// cancels; Enter, Y or J say OK, Escape or N cancel. The question and its answer still go into
/// the messages.
///
/// WHILE IT STANDS THE WORLD STANDS (per user, the same day: nobody walks away meanwhile): the
/// game clock is held and everything else is locked as under the game menu (UWGameClock,
/// UWControlScheme.HoldUiModal - which also shows the pointer). The click that answers does
/// nothing else - neither switches the pointer nor strikes (IsBlocking).
///
/// THE MANTRA likewise (per user, the same day): "Chant the mantra:" with a field of the typed
/// text, OK and Cancel - the typing stays the message scroll's (Interaction: ten characters,
/// Backspace, Enter, Escape); the mouse takes only the buttons here, the original's any-button
/// end would send a stray click's text.
/// </summary>
public class UWModernQuestion : MonoBehaviour
{
    public static UWModernQuestion Instance { get; private set; }

    /// <summary>The box takes the mouse - also in the frame it closed, so its click and its right
    /// button do nothing more (UWModernPointer).</summary>
    public static bool IsBlocking => Instance != null && (Instance.mbShown || Instance.miClosedFrame == Time.frameCount);

    /// <summary>The layout in original pixels.</summary>
    private const int BoxWidth = 150;

    private const int Pad = 8;

    private const int ButtonWidth = 34;

    private const int ButtonHeight = 11;

    private const int ButtonGap = 10;

    /// <summary>The mantra's field.</summary>
    private const int InputWidth = 80;

    private const int InputHeight = 10;

    private UWGameUI mOUi;

    private Interaction mOInteraction;

    private UWControlScheme mOScheme;

    private Canvas mOCanvas;

    private RawImage mOBack;

    private Text mOQuestion;

    private RawImage[] mOButtons;

    private Text[] mOButtonTexts;

    /// <summary>The box's leather (for its height) and the buttons', built for the colours
    /// (UWVersionedTexture).</summary>
    private readonly UWVersionedTexture mOBackArt = new UWVersionedTexture();

    private readonly UWVersionedTexture mOButtonArt = new UWVersionedTexture();

    private const string Hold = "modern question";

    /// <summary>Lock the pointer once answered - for the anvil, whose use mode freed it and would
    /// otherwise lock it the moment before the box frees it again (UWModernBags.CancelUse).</summary>
    private static bool msbLockAfter;

    public static void LockWhenAnswered()
    {
        msbLockAfter = true;
    }

    private bool mbShown;

    private int miClosedFrame = -1;

    private int miShownFrame = -1;

    private Rect mORect;

    private readonly Rect[] mOButtonRects = new Rect[2];

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        mOBackArt.Release();
        mOButtonArt.Release();
    }

    private void Start()
    {
        mOUi = GetComponent<UWGameUI>();
        mOInteraction = GetComponent<Interaction>();
        mOScheme = GetComponentInParent<UWControlScheme>();
    }

    /// <summary>Whether a screen point lies on the box.</summary>
    public bool Contains(Vector2 pOPointer)
    {
        return mbShown && mORect.Contains(pOPointer);
    }

    private bool fIsAsking()
    {
        return fIsModernShowing() && mOInteraction.IsAskingYesNo;
    }

    private bool fIsMantra()
    {
        return fIsModernShowing() && mOInteraction.IsChanting;
    }

    private bool fIsModernShowing()
    {
        return mOScheme != null && UWModernUi.IsActive && mOInteraction != null
            && UWModernUi.IsShowing;
    }

    private bool mbMantra;

    private Image mOInputBack;

    private Text mOInputText;

    private void Update()
    {
        mbMantra = fIsMantra();

        bool lbAsking = fIsAsking() || mbMantra;

        if (lbAsking && !mbShown)
        {
            mbShown = true;
            miShownFrame = Time.frameCount;
            UWGameClock.Hold(Hold);
            mOScheme.HoldUiModal(Hold);
        }

        // Answered by key, or the scheme changed.
        if (!lbAsking && mbShown)
            fClose();

        if (!mbShown || mOScheme.Controls == null || Time.frameCount == miShownFrame)
            return;

        // The keys - the world's input is held, so the message scroll's question reads none now.
        // The mantra's typing stays the Interaction's (it reads before the hold).
        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard != null && !mbMantra)
        {
            if (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame
                || lOKeyboard.yKey.wasPressedThisFrame || lOKeyboard.jKey.wasPressedThisFrame)
            {
                fAnswer(true);
                return;
            }

            if (UWBack.Takes(UWBackOwner.ModernQuestion) || lOKeyboard.nKey.wasPressedThisFrame)
            {
                fAnswer(false);
                return;
            }
        }

        if (!UWPointer.HasMouse)
            return;

        UWControls lOControls = mOScheme.Controls;
        Vector2 lOPointer = UWPointer.Position;

        if (lOControls.Player.Interact.WasPressedThisFrame())
            fAnswer(false);
        else if (lOControls.Player.CursorDrag.WasPressedThisFrame() && mOButtonRects[0].Contains(lOPointer))
            fAnswer(true);
        else if (lOControls.Player.CursorDrag.WasPressedThisFrame() && mOButtonRects[1].Contains(lOPointer))
            fAnswer(false);
    }

    private void fAnswer(bool pbYes)
    {
        if (mbMantra)
            mOInteraction.FinishMantra(!pbYes);
        else
            mOInteraction.AnswerYesNo(pbYes);

        fClose();
    }

    /// <summary>The box goes, and the world runs again.</summary>
    private void fClose()
    {
        mbShown = false;
        miClosedFrame = Time.frameCount;
        UWGameClock.Release(Hold);

        if (mOScheme != null)
            mOScheme.ReleaseUiModal(Hold);

        if (msbLockAfter)
        {
            msbLockAfter = false;
            UWModernPointer.Lock();
        }
    }

    private void OnDisable()
    {
        if (mbShown)
            fClose();
    }

    // ------------------------------------------------- Drawing

    private void LateUpdate()
    {
        if (!mbShown)
        {
            if (mOCanvas != null)
                mOCanvas.enabled = false;

            mORect = Rect.zero;
            mOButtonRects[0] = Rect.zero;
            mOButtonRects[1] = Rect.zero;
            return;
        }

        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;

        float liScale = UWModernUi.PixelScale;

        UWArtStamp lOButtonStamp = UWVersionedTexture.Colours();

        if (!mOButtonArt.IsCurrent(lOButtonStamp))
        {
            mOButtonArt.Set(lOButtonStamp, UWModernHudArt.BuildLeather(mOUi.mOUWData.Textures, ButtonWidth, ButtonHeight, mOUi.TextureFilterMode));

            foreach (RawImage lOButton in mOButtons)
                lOButton.texture = mOButtonArt.Texture;
        }

        // The question, wrapped to the box's width; the box as tall as it needs.
        float lfInner = (BoxWidth - (2 * Pad)) * liScale;

        mOQuestion.text = mbMantra ? UWShrine.Prompt.Trim() : (mOInteraction.YesNoQuestion ?? string.Empty).Trim();
        mOQuestion.fontSize = Mathf.Max(10, Mathf.RoundToInt(4.4f * liScale));
        mOQuestion.rectTransform.sizeDelta = new Vector2(lfInner, 10f);

        int liTextRows = Mathf.CeilToInt(mOQuestion.preferredHeight / liScale);
        int liInputRows = mbMantra ? InputHeight + 4 : 0;
        int liHeight = Pad + liTextRows + 6 + liInputRows + ButtonHeight + Pad;

        UWArtStamp lOBackStamp = UWVersionedTexture.Colours(liHeight);

        if (!mOBackArt.IsCurrent(lOBackStamp))
            mOBack.texture = mOBackArt.Set(lOBackStamp, UWModernHudArt.BuildLeather(mOUi.mOUWData.Textures, BoxWidth, liHeight, mOUi.TextureFilterMode));

        float lfWidth = BoxWidth * liScale;
        float lfHeight = liHeight * liScale;
        float lfX = Mathf.Round((Screen.width - lfWidth) * 0.5f);
        float lfY = Mathf.Round((Screen.height * 0.55f) - (lfHeight * 0.5f));

        mORect = new Rect(lfX, lfY, lfWidth, lfHeight);
        UWUiKit.SetRect(mOBack.rectTransform, lfX, lfY, lfWidth, lfHeight);
        UWUiKit.SetRect(mOQuestion.rectTransform, lfX + (Pad * liScale), lfY + lfHeight - ((Pad + liTextRows) * liScale), lfInner, liTextRows * liScale);

        // The mantra's field below the prompt, with a blinking caret.
        mOInputBack.enabled = mbMantra;
        mOInputText.enabled = mbMantra;

        if (mbMantra)
        {
            float lfInputY = lfY + lfHeight - ((Pad + liTextRows + 4 + InputHeight) * liScale);
            float lfInputWidth = InputWidth * liScale;
            float lfInputX = lfX + ((lfWidth - lfInputWidth) * 0.5f);
            // At the one rate of both schemes (decision 17, 2026-10-20; until then the first half
            // of every second).
            bool lbCaret = UWMessageLogModel.IsCursorOn(Time.unscaledTime);

            UWUiKit.SetRect(mOInputBack.rectTransform, lfInputX, lfInputY, lfInputWidth, InputHeight * liScale);
            mOInputText.text = (mOInteraction.MantraText ?? string.Empty) + (lbCaret ? "_" : " ");
            mOInputText.fontSize = Mathf.Max(10, Mathf.RoundToInt(4.6f * liScale));
            UWUiKit.SetRect(mOInputText.rectTransform, lfInputX + (3 * liScale), lfInputY + (0.5f * liScale), lfInputWidth - (6 * liScale),
                InputHeight * liScale);
        }

        // OK and Cancel side by side at the bottom.
        Vector2 lOPointer = UWPointer.HasMouse ? UWPointer.Position : new Vector2(-1f, -1f);
        float lfButtonsWidth = ((2 * ButtonWidth) + ButtonGap) * liScale;
        float lfButtonX = lfX + ((lfWidth - lfButtonsWidth) * 0.5f);
        float lfButtonY = lfY + (Pad * liScale);

        for (int liButton = 0; liButton < 2; liButton++)
        {
            Rect lOButton = new Rect(Mathf.Round(lfButtonX + (liButton * (ButtonWidth + ButtonGap) * liScale)), lfButtonY,
                ButtonWidth * liScale, ButtonHeight * liScale);

            mOButtonRects[liButton] = lOButton;
            UWUiKit.SetRect(mOButtons[liButton].rectTransform, lOButton.x, lOButton.y, lOButton.width, lOButton.height);
            UWUiKit.SetRect(mOButtonTexts[liButton].rectTransform, lOButton.x, lOButton.y + (0.5f * liScale), lOButton.width, lOButton.height);
            mOButtonTexts[liButton].fontSize = Mathf.Max(10, Mathf.RoundToInt(4.4f * liScale));
            mOButtonTexts[liButton].color = lOButton.Contains(lOPointer) ? UWModernPalette.LightText : UWModernPalette.Gold;
        }
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        mOCanvas = UWUiKit.CreateOverlayCanvas(transform, "Modern question", UWUiSorting.ModernQuestion);

        RectTransform lORootRect = (RectTransform)mOCanvas.transform;

        mOBack = UWUiKit.CreateRawImage(lORootRect, "Leather", true);
        UWPixelArtUI.Apply(mOBack);

        mOQuestion = UWUiKit.CreateText(lORootRect, "Question", TextAnchor.UpperCenter, UWModernPalette.Text, mOUi);
        mOQuestion.horizontalOverflow = HorizontalWrapMode.Wrap;

        mOInputBack = UWUiKit.CreateImage(lORootRect, "Mantra field", UWModernPalette.InputFill);

        mOInputText = UWUiKit.CreateText(lORootRect, "Mantra", TextAnchor.MiddleLeft, UWModernPalette.Text, mOUi);

        mOButtons = new RawImage[2];
        mOButtonTexts = new Text[2];

        string[] lsButtons = { "OK", "Cancel" };
        string[] lsButtonTextIds = { "hud.question.ok", "hud.question.cancel" };

        for (int liButton = 0; liButton < 2; liButton++)
        {
            mOButtons[liButton] = UWUiKit.CreateRawImage(lORootRect, lsButtons[liButton], true);
            UWPixelArtUI.Apply(mOButtons[liButton]);
            mOButtonTexts[liButton] = UWUiKit.CreateText(lORootRect, lsButtons[liButton] + " text", TextAnchor.MiddleCenter, UWModernPalette.Gold, mOUi);
            mOButtonTexts[liButton].text = UWText.T(lsButtonTextIds[liButton]);
        }
    }
}
