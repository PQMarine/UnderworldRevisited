using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

    private static readonly Color msText = new Color(0.94f, 0.87f, 0.71f);

    private static readonly Color msGold = new Color(0.925f, 0.77f, 0.44f, 1f);

    private static readonly Color msHover = new Color(1f, 0.92f, 0.69f, 1f);

    private UWGameUI mOUi;

    private Interaction mOInteraction;

    private UWControlScheme mOScheme;

    private Font mOFont;

    private Canvas mOCanvas;

    private RawImage mOBack;

    private Text mOQuestion;

    private RawImage[] mOButtons;

    private Text[] mOButtonTexts;

    private Texture2D mOBackTexture;

    private Vector2Int mOBackSize;

    private Texture2D mOButtonTexture;

    private int miArtVersion = -1;

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

        foreach (Texture2D lOTexture in new[] { mOBackTexture, mOButtonTexture })
        {
            if (lOTexture != null)
                Destroy(lOTexture);
        }
    }

    private void Start()
    {
        mOUi = GetComponent<UWGameUI>();
        mOInteraction = GetComponent<Interaction>();
        mOScheme = GetComponentInParent<UWControlScheme>();
        mOFont = Resources.Load<Font>("Fonts/LexendExa");
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
        return mOScheme != null && mOScheme.Current == UWControlScheme.SchemeEnum.Modern && mOInteraction != null
            && UWModernHud.Instance != null && UWModernHud.Instance.IsShowing;
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

            if (lOKeyboard.escapeKey.wasPressedThisFrame || lOKeyboard.nKey.wasPressedThisFrame)
            {
                fAnswer(false);
                return;
            }
        }

        if (Mouse.current == null)
            return;

        UWControls lOControls = mOScheme.Controls;
        Vector2 lOPointer = Mouse.current.position.ReadValue();

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

        float liScale = UWModernHud.PixelScale;

        if (miArtVersion != UWColourVision.Version || mOButtonTexture == null)
        {
            miArtVersion = UWColourVision.Version;

            if (mOButtonTexture != null)
                Destroy(mOButtonTexture);

            mOButtonTexture = UWModernHudArt.BuildLeather(mOUi.mOUWData.Textures, ButtonWidth, ButtonHeight, mOUi.TextureFilterMode);
            mOBackSize = Vector2Int.zero;

            foreach (RawImage lOButton in mOButtons)
                lOButton.texture = mOButtonTexture;
        }

        // The question, wrapped to the box's width; the box as tall as it needs.
        float lfInner = (BoxWidth - (2 * Pad)) * liScale;

        mOQuestion.text = mbMantra ? UWShrine.Prompt.Trim() : (mOInteraction.YesNoQuestion ?? string.Empty).Trim();
        mOQuestion.fontSize = Mathf.Max(10, Mathf.RoundToInt(4.4f * liScale));
        mOQuestion.rectTransform.sizeDelta = new Vector2(lfInner, 10f);

        int liTextRows = Mathf.CeilToInt(mOQuestion.preferredHeight / liScale);
        int liInputRows = mbMantra ? InputHeight + 4 : 0;
        int liHeight = Pad + liTextRows + 6 + liInputRows + ButtonHeight + Pad;

        if (mOBackTexture == null || mOBackSize.y != liHeight)
        {
            if (mOBackTexture != null)
                Destroy(mOBackTexture);

            mOBackTexture = UWModernHudArt.BuildLeather(mOUi.mOUWData.Textures, BoxWidth, liHeight, mOUi.TextureFilterMode);
            mOBackSize = new Vector2Int(BoxWidth, liHeight);
            mOBack.texture = mOBackTexture;
        }

        float lfWidth = BoxWidth * liScale;
        float lfHeight = liHeight * liScale;
        float lfX = Mathf.Round((Screen.width - lfWidth) * 0.5f);
        float lfY = Mathf.Round((Screen.height * 0.55f) - (lfHeight * 0.5f));

        mORect = new Rect(lfX, lfY, lfWidth, lfHeight);
        fSetRect(mOBack.rectTransform, lfX, lfY, lfWidth, lfHeight);
        fSetRect(mOQuestion.rectTransform, lfX + (Pad * liScale), lfY + lfHeight - ((Pad + liTextRows) * liScale), lfInner, liTextRows * liScale);

        // The mantra's field below the prompt, with a blinking caret.
        mOInputBack.enabled = mbMantra;
        mOInputText.enabled = mbMantra;

        if (mbMantra)
        {
            float lfInputY = lfY + lfHeight - ((Pad + liTextRows + 4 + InputHeight) * liScale);
            float lfInputWidth = InputWidth * liScale;
            float lfInputX = lfX + ((lfWidth - lfInputWidth) * 0.5f);
            bool lbCaret = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;

            fSetRect(mOInputBack.rectTransform, lfInputX, lfInputY, lfInputWidth, InputHeight * liScale);
            mOInputText.text = (mOInteraction.MantraText ?? string.Empty) + (lbCaret ? "_" : " ");
            mOInputText.fontSize = Mathf.Max(10, Mathf.RoundToInt(4.6f * liScale));
            fSetRect(mOInputText.rectTransform, lfInputX + (3 * liScale), lfInputY + (0.5f * liScale), lfInputWidth - (6 * liScale),
                InputHeight * liScale);
        }

        // OK and Cancel side by side at the bottom.
        Vector2 lOPointer = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
        float lfButtonsWidth = ((2 * ButtonWidth) + ButtonGap) * liScale;
        float lfButtonX = lfX + ((lfWidth - lfButtonsWidth) * 0.5f);
        float lfButtonY = lfY + (Pad * liScale);

        for (int liButton = 0; liButton < 2; liButton++)
        {
            Rect lOButton = new Rect(Mathf.Round(lfButtonX + (liButton * (ButtonWidth + ButtonGap) * liScale)), lfButtonY,
                ButtonWidth * liScale, ButtonHeight * liScale);

            mOButtonRects[liButton] = lOButton;
            fSetRect(mOButtons[liButton].rectTransform, lOButton.x, lOButton.y, lOButton.width, lOButton.height);
            fSetRect(mOButtonTexts[liButton].rectTransform, lOButton.x, lOButton.y + (0.5f * liScale), lOButton.width, lOButton.height);
            mOButtonTexts[liButton].fontSize = Mathf.Max(10, Mathf.RoundToInt(4.4f * liScale));
            mOButtonTexts[liButton].color = lOButton.Contains(lOPointer) ? msHover : msGold;
        }
    }

    private static void fSetRect(RectTransform pORect, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        pORect.anchoredPosition = new Vector2(pfX, pfY);
        pORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        GameObject lORoot = new GameObject("Modern question", typeof(Canvas), typeof(CanvasScaler));
        lORoot.transform.SetParent(transform, false);

        mOCanvas = lORoot.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mOCanvas.sortingOrder = 44;

        CanvasScaler lOScaler = lORoot.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        lOScaler.scaleFactor = 1f;

        RectTransform lORootRect = (RectTransform)lORoot.transform;

        mOBack = fCreateRawImage(lORootRect, "Leather");
        UWPixelArtUI.Apply(mOBack);

        mOQuestion = fCreateText(lORootRect, "Question", TextAnchor.UpperCenter, msText);
        mOQuestion.horizontalOverflow = HorizontalWrapMode.Wrap;

        GameObject lOInput = new GameObject("Mantra field", typeof(RectTransform), typeof(Image));
        lOInput.transform.SetParent(lORootRect, false);
        RectTransform lOInputRect = (RectTransform)lOInput.transform;
        lOInputRect.anchorMin = Vector2.zero;
        lOInputRect.anchorMax = Vector2.zero;
        lOInputRect.pivot = Vector2.zero;
        mOInputBack = lOInput.GetComponent<Image>();
        mOInputBack.color = new Color(0.10f, 0.06f, 0.03f, 0.85f);
        mOInputBack.raycastTarget = false;

        mOInputText = fCreateText(lORootRect, "Mantra", TextAnchor.MiddleLeft, msText);

        mOButtons = new RawImage[2];
        mOButtonTexts = new Text[2];

        string[] lsButtons = { "OK", "Cancel" };

        for (int liButton = 0; liButton < 2; liButton++)
        {
            mOButtons[liButton] = fCreateRawImage(lORootRect, lsButtons[liButton]);
            UWPixelArtUI.Apply(mOButtons[liButton]);
            mOButtonTexts[liButton] = fCreateText(lORootRect, lsButtons[liButton] + " text", TextAnchor.MiddleCenter, msGold);
            mOButtonTexts[liButton].text = lsButtons[liButton];
        }
    }

    private static RawImage fCreateRawImage(Transform pOParent, string psName)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;

        return lOImage;
    }

    private Text fCreateText(Transform pOParent, string psName, TextAnchor peAlignment, Color pOColour)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Text), typeof(Outline));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        Text lOText = lOObject.GetComponent<Text>();
        lOText.font = mOFont != null ? mOFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lOText.alignment = peAlignment;
        lOText.color = pOColour;
        lOText.raycastTarget = false;
        lOText.horizontalOverflow = HorizontalWrapMode.Overflow;
        lOText.verticalOverflow = VerticalWrapMode.Overflow;

        Outline lOOutline = lOObject.GetComponent<Outline>();
        lOOutline.effectColor = new Color(0.16f, 0.09f, 0.04f, 0.9f);
        lOOutline.effectDistance = new Vector2(1.5f, -1.5f);

        return lOText;
    }
}
