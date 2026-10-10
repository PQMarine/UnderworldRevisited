using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S HUD AND GAME MENU (stage 1 of the modern controls, concept per user
/// 2026-10-03: "an own modern interface, more a modern MMORPG UI", backgrounds and frames out of
/// the original graphics where possible). The classic frame is off in the modern scheme
/// (UWGameUI.Update), and with it the weapon, the flasks, the power gem, the active spells and
/// the message scroll; their STATE kept running (UWCharacter, Interaction). This class draws
/// them again on a full-screen canvas of its own:
///
///   - the drawn weapon, from the same frames and Weapons.dat offsets as the classic view, the
///     original's 172 x 112 viewport mapped onto the screen (WeaponScale);
///   - bottom left the two flasks with their values and the power gem between them, standing
///     free on the original's stone shelf (UWModernHudArt.BuildShelf, look per user 2026-10-03);
///   - top left the active spells (SPELLS.GR through the palette shader, as on the classic shelf);
///   - top centre the heading, sixteen points as the compass needle has;
///   - above the flasks the last five messages, colour codes stripped, [MORE] while the game
///     waits for a page;
///   - the crosshair with the name of what it is on (Interaction.GetCrosshairTargetName).
///
/// The images keep the original pixels at an integer scale (PixelScale). The eyes already have
/// their own canvas (UWEyesDisplay).
///
/// THE GAME MENU (Escape, F1, the Ctrl shortcuts - routed by UWGameUI.fCheckModernKeys) does what
/// the original's options do, through the same work: UWSavegameWriter.Save,
/// UWSavegameSlots.Restore, UWSoundOptions, UWGraphicsDetail, the main menu. It refuses where
/// they refuse (UWHudOptions.RefusesToOpen) and stops time as they do.
/// </summary>
public class UWModernHud : MonoBehaviour
{
    public enum PageEnum
    {
        Closed,
        Main,
        Save,
        SaveName,
        Load,
        Detail,

        /// <summary>The layout editor (UWModernLayoutEditor) - no page of this menu is drawn.</summary>
        Layout
    }

    public PageEnum Page => mePage;

    /// <summary>The one HUD of the scene, for UWGameUI's key routing.</summary>
    public static UWModernHud Instance { get; private set; }

    /// <summary>Whether a save game name is being typed - the game's keys stay silent then
    /// (UWGameUI.IsTextEntryActive).</summary>
    public static bool IsTypingName => Instance != null && Instance.mePage == PageEnum.SaveName;

    public bool IsOpen => mePage != PageEnum.Closed;

    /// <summary>A page of the menu is drawn (not the layout editor) - the menu bar of the setup
    /// shows itself over it then (UWSetupMenu, per user 2026-10-06).</summary>
    public bool ShowsMenuPage => mePage != PageEnum.Closed && mePage != PageEnum.Layout;

    /// <summary>Whether the HUD is on screen: the modern scheme, no conversation, no screen menu
    /// or cutscene over it. The bags (UWModernBags) show with it.</summary>
    public bool IsShowing => fIsModern() && mOUi != null && mOUi.mOUWData != null && mOCharacter != null
        && mOInteraction != null && !fIsCovered() && !UWConversationScreen.IsAnyOpen;

    /// <summary>A conversation in the modern scheme (UWModernConversation): the HUD hides, the bags
    /// and the character panel stay for the trade.</summary>
    public bool IsTalking => fIsModern() && mOUi != null && mOUi.mOUWData != null && mOCharacter != null
        && mOInteraction != null && !fIsCovered() && UWConversationScreen.IsAnyOpen;

    /// <summary>How large the weapon is drawn against the screen: 1 maps the original's viewport
    /// height (112 rows) onto the screen height.</summary>
    [SerializeField]
    private float mfWeaponScale = 0.8f;

    /// <summary>Screen rows per original pixel step of the HUD images: the integer scale is the
    /// screen height divided by this.</summary>
    private const float PixelScaleRows = 270f;

    /// <summary>Original pixels the bottom row needs across: the shelf (4 + 106), the action bar
    /// (196), the backpack with the panel's handle (86 + 9 + margins) and the gaps.</summary>
    private const float BottomRowWidth = 425f;

    /// <summary>
    /// The scale the screen gives (per user, 2026-10-03): one original pixel per 270 rows, but no
    /// more than lets the bottom row fit across - so a 4:3 or 5:4 screen gets a smaller one than a
    /// wide screen of the same height.
    /// </summary>
    public static float AutoPixelScale => Mathf.Max(1f, Mathf.Min(Screen.height / PixelScaleRows, Screen.width / BottomRowWidth));

    /// <summary>
    /// THE MODERN UI'S SCALE - screen pixels per original pixel, for the HUD, the bags, the
    /// character panel, the minimap and the action bar alike: the automatic one times the
    /// player's choice in the game menu (UWUserSettings.ModernUiPercent, 50 to 200 %). FREE, not
    /// only whole numbers: the pictures go through the pixel-art shaders (UWPixelArtUI,
    /// UWIconPalette.ApplySmooth), which keep every texel a hard square at any scale.
    /// </summary>
    public static float PixelScale => AutoPixelScale * (UiPercent / 100f);

    /// <summary>The chosen size, 100 when none was chosen.</summary>
    public static int UiPercent
    {
        get
        {
            int liPercent = UWUserSettings.ModernUiPercent;

            return liPercent <= 0 ? 100 : Mathf.Clamp(liPercent, MinUiPercent, MaxUiPercent);
        }
    }

    public const int MinUiPercent = 50;

    public const int MaxUiPercent = 200;

    public const int UiPercentStep = 10;

    /// <summary>The text size that goes with the scale - 20 at 1080 lines, as before.</summary>
    public static int FontSize => Mathf.Max(10, Mathf.RoundToInt(5f * PixelScale));

    /// <summary>The original viewport: its left edge in the frame (52, as UWGameUI places the
    /// weapon), its width and height.</summary>
    private const float ViewportLeft = 52f;

    private const float ViewportWidth = 172f;

    private const float ViewportHeight = 112f;

    private const int MessageLines = 5;

    private const string MenuModalHold = "modern menu";

    private const string MenuClockHold = "modern menu";

    private UWGameUI mOUi;

    private UWCharacter mOCharacter;

    private Interaction mOInteraction;

    private UWControlScheme mOScheme;

    private Canvas mOCanvas;

    private RawImage mOWeapon;

    private RawImage mOHealthFlask;

    private RawImage mOManaFlask;

    private RawImage mOPowerGem;

    /// <summary>The stone shelf under the flasks and the gem (UWModernHudArt).</summary>
    private RawImage mOShelf;

    private Texture2D mOShelfTexture;

    private UWModernHudArt.ShelfLayout mOShelfLayout;

    /// <summary>Which pixels of the flask and gem pictures to keep - their black backdrop goes.</summary>
    private bool[] mbFlaskMask;

    private bool[] mbGemMask;

    /// <summary>The flasks standing free (UWModernLayout.IsFlasksApart): which pixels to keep, and
    /// the keyed copies.</summary>
    private bool[] mbFreeFlaskMask;

    private readonly System.Collections.Generic.Dictionary<Texture2D, Texture2D> mOKeyedFree
        = new System.Collections.Generic.Dictionary<Texture2D, Texture2D>();

    /// <summary>The stone shelf deco panel (UWModernLayout.IsStoneShelfShown), its picture and the
    /// width it was built at.</summary>
    private RawImage mOStoneShelf;

    private Texture2D mOStoneShelfTexture;

    private string msStoneShelfKey;

    /// <summary>The stone shelf's default width in its own pixels: as wide as the flasks' shelf.</summary>
    public const int StoneShelfDefaultWidth = 81;

    /// <summary>The stone shelf's width as set, in its own pixels.</summary>
    public static int StoneShelfWidth => UWUserSettings.ModernStoneShelfWidth > 0 ? UWUserSettings.ModernStoneShelfWidth : StoneShelfDefaultWidth;

    /// <summary>The widest stone shelf: the whole screen at its size (per user, 2026-10-10: "one
    /// should be able to pull the shelf across the whole screen"; before, 320).</summary>
    public static int StoneShelfMaxWidth => Mathf.Max(UWDataImport.UWData.UWHudArt.StoneShelfMinWidth,
        Mathf.CeilToInt(Screen.width / Mathf.Max(0.01f, PixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.StoneShelf) * UWModernHudArt.PixelAspectX)));

    /// <summary>The power gem apart (UWModernLayout.IsGemApart): its stand, and where the gem lies on it.</summary>
    private RawImage mOGemStand;

    private Texture2D mOGemStandTexture;

    private Vector2Int mOGemOnStand;

    private bool mbGemApart;

    /// <summary>The keyed copies of the flask and gem pictures, by the picture UWCharacter
    /// shows (it caches those itself, so this stays small).</summary>
    private readonly System.Collections.Generic.Dictionary<Texture2D, Texture2D> mOKeyed
        = new System.Collections.Generic.Dictionary<Texture2D, Texture2D>();

    /// <summary>The colour help the frames were built with (UWColourVision.Version).</summary>
    private int miArtVersion = -1;

    /// <summary>Original pixels between the shelf and the screen's edges.</summary>
    private const int ShelfMargin = 4;

    private RawImage[] mOSpellIcons;

    private Texture2D[] mOSpellTextures;

    /// <summary>The active spells' places this frame (screen pixels), and the hover text.</summary>
    private readonly Rect[] mOSpellRects = new Rect[UWCharacter.MaxActiveSpells];

    private Text mOSpellTip;

    private int miHoveredSpell = -1;

    /// <summary>The heading at the top as a button (per user, 2026-10-04): the classic compass's
    /// click reports the status (UWGameUI.ReportStatus); here a click on the heading does.</summary>
    private Rect mOHeadingRect;

    private bool mbHeadingHovered;

    private static readonly Color msHeadingHover = new Color(0.925f, 0.77f, 0.44f, 1f);

    private Image mOMessageBack;

    /// <summary>
    /// THE BACKS BEHIND THE PARTS (per user, 2026-10-09: "the back behind" for the parts with art of
    /// their own): the heading's strip, the vitals' shelf, the messages and the active spells keep
    /// their art, a generated back (UWModernBacks) lies under it, the part's rect and a few of its
    /// pixels more all round. For the messages it stands in the dark box's place. The compass draws
    /// its own (UWModernCompass); the leather parts take theirs in the leather's place. The power
    /// gem's stand joined on 2026-10-10 (per user: the background could be chosen for the then
    /// movement arrows but did not show), the flasks apart the same day; the stone shelf, a deco
    /// panel itself, offers none (UWModernLayoutEditor).
    /// </summary>
    private static readonly UWModernLayout.ElementEnum[] mePartsWithBackBehind =
    {
        UWModernLayout.ElementEnum.Heading, UWModernLayout.ElementEnum.Vitals, UWModernLayout.ElementEnum.Messages,
        UWModernLayout.ElementEnum.Spells, UWModernLayout.ElementEnum.PowerGem,
        UWModernLayout.ElementEnum.HealthFlask, UWModernLayout.ElementEnum.ManaFlask, UWModernLayout.ElementEnum.RuneHollow
    };

    /// <summary>The room around a part's rect, in its own pixels.</summary>
    private const int PartBackPad = 3;

    private readonly RawImage[] mOPartBacks = new RawImage[8];

    private readonly Texture2D[] mOPartBackTextures = new Texture2D[8];

    private readonly string[] msPartBackKeys = new string[8];

    private Text mOMessages;

    private Text mOHealthText;

    private Text mOManaText;

    private Text mOHeading;

    private Text mOTarget;

    private Image mOCrosshair;

    /// <summary>
    /// THE VIEW'S PICTURES (per user, 2026-10-04): a window's look into the depths, a gravestone,
    /// a picture scroll - the original shows them in its view window, which the modern scheme
    /// hides; here they lie in a leather frame in the middle of the screen, at the UI's size, over
    /// everything. Any key or click hides them, as in the original (Interaction).
    /// </summary>
    private Canvas mOPictureCanvas;

    private RawImage mOPictureFrame;

    private Image mOPicture;

    private Texture2D mOPictureFrameTexture;

    private Vector2Int mOPictureFrameSize;

    private int miPictureArtVersion = -1;

    private const int PictureBorder = 3;

    /// <summary>In the crosshair's place while a spell waits for its target and the pointer is
    /// locked: the original's target pointer (per user, 2026-10-04).</summary>
    private RawImage mOTargetCursor;

    /// <summary>The bar under the crosshair while E is held for the direct use.</summary>
    private Image mOHoldBar;

    private Font mOFont;

    private float miPixelScale = -1f;

    private int miSpellVersion = -1;

    private float mfTargetTimer;

    private PageEnum mePage = PageEnum.Closed;

    private int miSaveSlot;

    private string msSaveName = string.Empty;

    private bool mbFocusName;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        fReleaseHolds();

        if (mOShelfTexture != null)
            Destroy(mOShelfTexture);

        foreach (Texture2D lOOld in mOKeyed.Values)
            Destroy(lOOld);

        foreach (Texture2D lOOld in mOKeyedFree.Values)
            Destroy(lOOld);
    }

    private void Start()
    {
        mOUi = GetComponent<UWGameUI>();
        mOCharacter = GetComponent<UWCharacter>();
        mOInteraction = GetComponent<Interaction>();
        mOScheme = GetComponentInParent<UWControlScheme>();
        mOFont = Resources.Load<Font>("Fonts/LexendExa");
    }

    // ------------------------------------------------- The HUD

    private bool fIsModern()
    {
        return mOScheme != null && mOScheme.Current == UWControlScheme.SchemeEnum.Modern;
    }

    private bool fIsCovered()
    {
        UWIntroPlayer lOIntro = UWScene.IntroPlayer;

        // The big map covers the screen as in the original (UWGameUI shows it on the classic frame).
        return UWScreenUi.IsScreenMenuOpen || (lOIntro != null && lOIntro.IsPlaying) || (mOUi != null && mOUi.IsMapVisible);
    }

    private void Update()
    {
        fStepMenuByPad();

        // The right button switches the pointer (UWModernPointer) - not under the menu.
        UWModernPointer.Tick(IsShowing && !IsOpen && !UWControls.IsTextEntryActive);

        if (mOSpellIcons != null)
            fUpdateSpellClicks();

        fUpdateMoveArrowClicks();
        fUpdateRuneHollowClicks();

        if (mOStats != null && IsShowing && !IsOpen && !UWControls.IsTextEntryActive && mOScheme != null)
            mOStats.UpdateClicks(mOScheme.Controls);
    }

    private void LateUpdate()
    {
        bool lbModern = fIsModern() && mOUi != null && mOUi.mOUWData != null && mOCharacter != null
            && mOInteraction != null;

        // Leaving the modern scheme (Shift+F2) or a screen menu taking over closes the menu.
        if (IsOpen && (!lbModern || fIsCovered()))
            CloseMenu();

        bool lbShow = lbModern && !fIsCovered() && !UWConversationScreen.IsAnyOpen;

        if (!lbShow)
        {
            if (mOCanvas != null)
                mOCanvas.enabled = false;

            // The original's character page stays for the trade in a conversation (per user,
            // 2026-10-10: it was not to be seen there); its canvas is its own.
            if (mOInventoryPage != null)
            {
                if (IsTalking)
                    mOInventoryPage.Update(miPixelScale);
                else
                    mOInventoryPage.Hide();
            }

            // The classic frame stays in a conversation, as the original's does.
            if (mOClassicFrame != null)
                mOClassicFrame.Tick(IsTalking);

            return;
        }

        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;

        fApplyPixelScale();

        if (mOClassicFrame != null)
            mOClassicFrame.Tick(true);

        fUpdateWeapon();
        fUpdateVitals();
        fUpdateSpells();
        fPlaceSpells();
        fUpdateHeading();
        fUpdateStrip();
        fUpdateCompass();
        fUpdateRuneHollow();

        if (mOStats != null)
            mOStats.Update(miPixelScale);

        if (mOInventoryPage != null)
            mOInventoryPage.Update(miPixelScale);

        fUpdateMessages();
        fPlaceMessages();
        fUpdateStoneShelf();
        fUpdateBacks();
        fUpdatePicture();
        fUpdateTarget();
    }

    /// <summary>The Classic Wide frame (UWModernClassicFrame), on a canvas of its own under this one.</summary>
    private UWModernClassicFrame mOClassicFrame;

    private void fBuild()
    {
        GameObject lORoot = new GameObject("Modern HUD", typeof(Canvas), typeof(CanvasScaler));
        lORoot.transform.SetParent(transform, false);

        mOCanvas = lORoot.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mOCanvas.sortingOrder = 40;

        CanvasScaler lOScaler = lORoot.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        lOScaler.scaleFactor = 1f;

        mOWeapon = fCreateRawImage(lORoot.transform, "Weapon", new Vector2(0f, 0f), new Vector2(0f, 1f));

        // The stone shelf deco panel, under everything else, so things can stand on it.
        mOStoneShelf = fCreateRawImage(lORoot.transform, "Stone shelf", new Vector2(0f, 0f), new Vector2(0f, 0f));
        UWPixelArtUI.Apply(mOStoneShelf);

        // The backs behind the parts drawn here (fUpdateBacks), before their art so they lie under it.
        for (int liAt = 0; liAt < mePartsWithBackBehind.Length; liAt++)
        {
            mOPartBacks[liAt] = fCreateRawImage(lORoot.transform, "Back " + mePartsWithBackBehind[liAt], Vector2.zero, Vector2.zero);
            UWPixelArtUI.Apply(mOPartBacks[liAt]);
        }

        mOMessageBack = fCreateImage(lORoot.transform, "Message back", new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Color(0.05f, 0.05f, 0.06f, 0.55f));
        mOMessages = fCreateText(lORoot.transform, "Messages", new Vector2(0f, 0f), new Vector2(0f, 0f), TextAnchor.LowerLeft);

        mOShelf = fCreateRawImage(lORoot.transform, "Shelf", new Vector2(0f, 0f), new Vector2(0f, 0f));
        mOGemStand = fCreateRawImage(lORoot.transform, "Power gem stand", new Vector2(0f, 0f), new Vector2(0f, 0f));
        UWPixelArtUI.Apply(mOGemStand);
        mORuneHollow = fCreateRawImage(lORoot.transform, "Rune hollow", new Vector2(0f, 0f), new Vector2(0f, 0f));
        UWPixelArtUI.Apply(mORuneHollow);

        for (int liAt = 0; liAt < mORuneHollowRunes.Length; liAt++)
        {
            mORuneHollowRunes[liAt] = fCreateRawImage(lORoot.transform, "Prepared rune", new Vector2(0f, 0f), new Vector2(0f, 0f));
            UWIconPalette.Apply(mORuneHollowRunes[liAt]);
        }
        mOHealthFlask = fCreateRawImage(lORoot.transform, "Vitality", new Vector2(0f, 0f), new Vector2(0f, 0f));
        mOPowerGem = fCreateRawImage(lORoot.transform, "Power gem", new Vector2(0f, 0f), new Vector2(0f, 0f));
        mOManaFlask = fCreateRawImage(lORoot.transform, "Mana", new Vector2(0f, 0f), new Vector2(0f, 0f));

        // The free scale: hard texels at any size (UWPixelArtUI).
        UWPixelArtUI.Apply(mOShelf);
        UWPixelArtUI.Apply(mOHealthFlask);
        UWPixelArtUI.Apply(mOPowerGem);
        UWPixelArtUI.Apply(mOManaFlask);
        mOHealthText = fCreateText(lORoot.transform, "Vitality value", new Vector2(0f, 0f), new Vector2(0.5f, 0f), TextAnchor.LowerCenter);
        mOManaText = fCreateText(lORoot.transform, "Mana value", new Vector2(0f, 0f), new Vector2(0.5f, 0f), TextAnchor.LowerCenter);

        mOSpellIcons = new RawImage[UWCharacter.MaxActiveSpells];
        mOSpellTextures = new Texture2D[UWCharacter.MaxActiveSpells];

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
            mOSpellIcons[liSlot] = fCreateRawImage(lORoot.transform, "Spell " + liSlot, Vector2.zero, Vector2.zero);

        mOSpellTip = fCreateText(lORoot.transform, "Spell hover", Vector2.zero, new Vector2(1f, 1f), TextAnchor.UpperRight);

        // The heading strip under the heading (fUpdateStrip).
        mOStrip = fCreateRawImage(lORoot.transform, "Heading strip", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        UWPixelArtUI.Apply(mOStrip);
        mOStripEyes = fCreateRawImage(lORoot.transform, "Gargoyle eyes", new Vector2(0.5f, 1f), new Vector2(0f, 1f));
        UWPixelArtUI.Apply(mOStripEyes);
        mOFoeName = fCreateText(lORoot.transform, "Foe", new Vector2(0.5f, 1f), new Vector2(0f, 1f), TextAnchor.MiddleLeft);
        mOFoeState = fCreateText(lORoot.transform, "Foe state", new Vector2(0.5f, 1f), new Vector2(0f, 1f), TextAnchor.MiddleLeft);

        mOHeading = fCreateText(lORoot.transform, "Heading", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), TextAnchor.MiddleCenter);

        // The original's compass as an element of its own (UWModernCompass), and its message
        // scroll in the messages' place (UWModernScroll).
        mOCompass = new UWModernCompass(mOUi);
        mOCompass.Build(lORoot.transform);
        mOStats = new UWModernStatsPage(mOUi);
        mOStats.Build(lORoot.transform);
        mOInventoryPage = new UWModernInventoryPage(mOUi);
        mOInventoryPage.Build(transform);
        mOScroll = new UWModernScroll(mOUi);
        mOScroll.Build(lORoot.transform);

        GameObject lOFrame = new GameObject("Classic wide");
        lOFrame.transform.SetParent(transform, false);
        mOClassicFrame = lOFrame.AddComponent<UWModernClassicFrame>();
        mOClassicFrame.Init(mOUi);

        mOCrosshair = fCreateImage(lORoot.transform, "Crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Color(1f, 1f, 1f, 0.85f));
        mOTargetCursor = fCreateRawImage(lORoot.transform, "Spell target", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        UWPixelArtUI.Apply(mOTargetCursor);
        mOTargetCursor.enabled = false;
        mOTarget = fCreateText(lORoot.transform, "Target", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), TextAnchor.UpperCenter);
        mOHoldBar = fCreateImage(lORoot.transform, "Hold ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Color(0.80f, 0.64f, 0.34f, 0.9f));
        mOHoldBar.enabled = false;

        // A ring that fills clockwise from the top (fUpdateTarget).
        Texture2D lORing = UWModernActionBar.BuildRing(128, 60f, 7f);

        mOHoldBar.sprite = Sprite.Create(lORing, new Rect(0f, 0f, lORing.width, lORing.height), new Vector2(0.5f, 0.5f));
        mOHoldBar.color = Color.white;
        mOHoldBar.type = Image.Type.Filled;
        mOHoldBar.fillMethod = Image.FillMethod.Radial360;
        mOHoldBar.fillOrigin = (int)Image.Origin360.Top;
        mOHoldBar.fillClockwise = true;
    }

    private RawImage fCreateRawImage(Transform pOParent, string psName, Vector2 pOAnchor, Vector2 pOPivot)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = pOAnchor;
        lORect.anchorMax = pOAnchor;
        lORect.pivot = pOPivot;

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;
        lOImage.enabled = false;

        return lOImage;
    }

    private Image fCreateImage(Transform pOParent, string psName, Vector2 pOAnchor, Vector2 pOPivot, Color pOColour)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = pOAnchor;
        lORect.anchorMax = pOAnchor;
        lORect.pivot = pOPivot;

        Image lOImage = lOObject.GetComponent<Image>();
        lOImage.color = pOColour;
        lOImage.raycastTarget = false;

        return lOImage;
    }

    private Text fCreateText(Transform pOParent, string psName, Vector2 pOAnchor, Vector2 pOPivot, TextAnchor peAlignment)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Text), typeof(Shadow));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = pOAnchor;
        lORect.anchorMax = pOAnchor;
        lORect.pivot = pOPivot;

        Text lOText = lOObject.GetComponent<Text>();
        lOText.font = mOFont != null ? mOFont : UWInterfaceFont.Font;
        UWUiFonts.Register(lOText, mOUi);
        lOText.alignment = peAlignment;
        lOText.color = new Color(0.86f, 0.86f, 0.84f);
        lOText.raycastTarget = false;
        lOText.horizontalOverflow = HorizontalWrapMode.Wrap;
        lOText.verticalOverflow = VerticalWrapMode.Overflow;

        lOObject.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);

        return lOText;
    }

    /// <summary>The integer scale of the original images and the sizes that follow from it -
    /// set again only when the screen height changes.</summary>
    private void fApplyPixelScale()
    {
        float liScale = PixelScale;

        if (liScale == miPixelScale)
            return;

        miPixelScale = liScale;

        float lfMargin = 6f * liScale;
        int liFontSize = FontSize;

        mOMessages.fontSize = liFontSize;
        mOHealthText.fontSize = liFontSize;
        mOManaText.fontSize = liFontSize;
        mOHeading.fontSize = Mathf.RoundToInt(liFontSize * 1.1f);
        mOTarget.fontSize = liFontSize;

        float lfMessageWidth = Mathf.Min(Screen.width * 0.45f, liFontSize * 36f);
        float lfMessageHeight = liFontSize * 1.35f * MessageLines + (2f * lfMargin);
        float lfFlaskRow = ((ShelfMargin + 42) * liScale) + (liFontSize * 1.5f) + lfMargin;

        mfLeftColumnBottom = lfFlaskRow;

        ((RectTransform)mOMessageBack.transform).anchoredPosition = new Vector2(lfMargin, lfFlaskRow);
        ((RectTransform)mOMessageBack.transform).sizeDelta = new Vector2(lfMessageWidth, lfMessageHeight);
        ((RectTransform)mOMessages.transform).anchoredPosition = new Vector2(2f * lfMargin, lfFlaskRow + lfMargin);
        ((RectTransform)mOMessages.transform).sizeDelta = new Vector2(lfMessageWidth - (2f * lfMargin), lfMessageHeight - (2f * lfMargin));

        ((RectTransform)mOHeading.transform).anchoredPosition = new Vector2(0f, -lfMargin);
        ((RectTransform)mOHeading.transform).sizeDelta = new Vector2(400f, liFontSize * 2f);

        float lfCross = Mathf.Max(2f, liScale);

        ((RectTransform)mOCrosshair.transform).sizeDelta = new Vector2(lfCross * 2f, lfCross * 2f);
        ((RectTransform)mOTarget.transform).anchoredPosition = new Vector2(0f, -4f * liScale);
        ((RectTransform)mOTarget.transform).sizeDelta = new Vector2(600f, liFontSize * 2f);

        miSpellVersion = -1;
    }

    /// <summary>The drawn weapon: frame, offset and raise from UWCharacter, the sway from
    /// UWGameUI, laid out as the classic view lays it out (UWGameUI.Update) with the original
    /// viewport's bottom on the screen's bottom and its centre on the screen's centre.</summary>
    private void fUpdateWeapon()
    {
        Texture2D lOTexture = mOCharacter.CurrentWeaponTexture;

        if (!mOCharacter.DrawWWeapon || lOTexture == null)
        {
            mOWeapon.enabled = false;
            return;
        }

        float lfScale = Screen.height / ViewportHeight * mfWeaponScale;
        float lfCentreX = Screen.width * 0.5f;
        float lfBottom = 0f;

        // CLASSIC WIDE: the weapon at the frame's pixel size, its offsets from row 131 as the
        // classic scheme's (UWGameUI: wTopOffset), the view's middle across; it lies under the
        // frame (UWModernClassicFrame.WeaponLayer), which covers what reaches below the hole (per
        // user's screenshot, 2026-10-10: it sat too high at the hole's bottom).
        UWModernClassicFrame lOFrame = UWModernClassicFrame.Instance;
        Transform lOParent = UWModernClassicFrame.IsActive && lOFrame != null && lOFrame.WeaponLayer != null
            ? lOFrame.WeaponLayer : mOCanvas.transform;

        if (mOWeapon.transform.parent != lOParent)
            mOWeapon.transform.SetParent(lOParent, false);

        if (UWModernClassicFrame.IsActive)
        {
            lfScale = UWModernClassicFrame.RowScale;
            lfCentreX = UWModernClassicFrame.Hole.center.x;
            lfBottom = Screen.height - (UWModernClassicFrame.WeaponRow * UWModernClassicFrame.RowScale);
        }

        WeaponCoordinate lOOffset = mOCharacter.GetWeaponOffset();
        float lfLeft = lfCentreX
            + ((ViewportLeft + lOOffset.X + mOUi.GetWeaponSway() - (ViewportLeft + (ViewportWidth * 0.5f))) * lfScale * UWModernHudArt.PixelAspectX);
        float lfTop = lfBottom + ((lOOffset.Y - (lOTexture.height * (1f - mOCharacter.CurrentReadyWeaponTime))) * lfScale);

        mOWeapon.texture = lOTexture;
        mOWeapon.enabled = true;

        RectTransform lORect = (RectTransform)mOWeapon.transform;
        lORect.anchoredPosition = new Vector2(lfLeft, lfTop);
        lORect.sizeDelta = new Vector2(lOTexture.width * lfScale * UWModernHudArt.PixelAspectX, lOTexture.height * lfScale);
    }

    /// <summary>The flasks with their values and the power gem between them, bottom left on
    /// the stone shelf.</summary>
    private void fUpdateVitals()
    {
        fEnsureArt();

        // Its own size and place (UWModernLayout): the shelf with the values above it.
        float liScale = miPixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.Vitals);
        float lfMargin = ShelfMargin * liScale;
        UWModernHudArt.ShelfLayout lOLayout = mOShelfLayout;
        int liTextSize = Mathf.Max(9, Mathf.RoundToInt(FontSize * UWModernLayout.Scale(UWModernLayout.ElementEnum.Vitals)));
        float lfTextHeight = liTextSize * 1.5f;
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Vitals, new Rect(lfMargin, lfMargin, lOLayout.Width * liScale * UWModernHudArt.PixelAspectX,
            ((lOLayout.Height + 1) * liScale) + lfTextHeight));
        Vector2 lOOrigin = lOPlaced.position;
        bool lbFlasksApart = UWModernLayout.IsFlasksApart;

        // The flasks apart: no shelf, and the vitals' place only gives the flasks' default places.
        if (!lbFlasksApart)
            UWModernLayout.Report(UWModernLayout.ElementEnum.Vitals, lOPlaced);

        mOShelf.texture = mOShelfTexture;
        mOShelf.enabled = mOShelfTexture != null && !lbFlasksApart;

        RectTransform lOShelfRect = (RectTransform)mOShelf.transform;
        lOShelfRect.anchoredPosition = lOOrigin;
        lOShelfRect.sizeDelta = new Vector2(lOLayout.Width * liScale * UWModernHudArt.PixelAspectX, lOLayout.Height * liScale);

        if (lbFlasksApart)
        {
            fUpdateFlaskApart(UWModernLayout.ElementEnum.HealthFlask, mOHealthFlask, mOCharacter.HealthFlaskTeture, mOHealthText,
                Mathf.RoundToInt(mOCharacter.CurrentHP) + "/" + Mathf.RoundToInt(mOCharacter.MaxHP),
                new Vector2(lOOrigin.x + (lOLayout.HealthFlask.x * liScale * UWModernHudArt.PixelAspectX), lOOrigin.y));
            fUpdateFlaskApart(UWModernLayout.ElementEnum.ManaFlask, mOManaFlask, mOCharacter.ManaFlaskTeture, mOManaText,
                Mathf.RoundToInt(mOCharacter.CurrentMana) + "/" + Mathf.RoundToInt(mOCharacter.MaxMana),
                new Vector2(lOOrigin.x + (lOLayout.ManaFlask.x * liScale * UWModernHudArt.PixelAspectX), lOOrigin.y));
        }
        else
        {
            fPlaceOnShelf(mOHealthFlask, fKeyed(mOCharacter.HealthFlaskTeture, mbFlaskMask), lOLayout.HealthFlask, lOOrigin, liScale);
            fPlaceOnShelf(mOManaFlask, fKeyed(mOCharacter.ManaFlaskTeture, mbFlaskMask), lOLayout.ManaFlask, lOOrigin, liScale);
        }

        if (mbGemApart)
            fUpdateGemStand(lOPlaced);
        else
        {
            mOGemStand.enabled = false;
            fPlaceOnShelf(mOPowerGem, fKeyed(mOCharacter.PowerGemTeture, mbGemMask), lOLayout.Gem, lOOrigin, liScale);
        }

        if (lbFlasksApart)
            return;

        mOHealthText.enabled = true;
        mOManaText.enabled = true;
        mOHealthText.fontSize = liTextSize;
        mOManaText.fontSize = liTextSize;

        float lfTextY = lOOrigin.y + ((lOLayout.Height + 1) * liScale);
        float lfHealthCentre = lOOrigin.x + ((lOLayout.HealthFlask.x + (fWidth(mOCharacter.HealthFlaskTeture) * 0.5f)) * liScale * UWModernHudArt.PixelAspectX);
        float lfManaCentre = lOOrigin.x + ((lOLayout.ManaFlask.x + (fWidth(mOCharacter.ManaFlaskTeture) * 0.5f)) * liScale * UWModernHudArt.PixelAspectX);

        mOHealthText.text = Mathf.RoundToInt(mOCharacter.CurrentHP) + "/" + Mathf.RoundToInt(mOCharacter.MaxHP);
        mOManaText.text = Mathf.RoundToInt(mOCharacter.CurrentMana) + "/" + Mathf.RoundToInt(mOCharacter.MaxMana);

        ((RectTransform)mOHealthText.transform).anchoredPosition = new Vector2(lfHealthCentre, lfTextY);
        ((RectTransform)mOHealthText.transform).sizeDelta = new Vector2(120f, mOHealthText.fontSize * 1.5f);
        ((RectTransform)mOManaText.transform).anchoredPosition = new Vector2(lfManaCentre, lfTextY);
        ((RectTransform)mOManaText.transform).sizeDelta = new Vector2(120f, mOManaText.fontSize * 1.5f);
    }

    /// <summary>
    /// A FLASK APART (Classic+, per user 2026-10-10: "the flasks freed one by one"): an element of
    /// its own with the flask standing free on its gold foot (UWHudArt.BuildFreeFlaskMask) and its
    /// value above it, as on the shelf; by default where the shelf would hold it (pODefault, its
    /// bottom left).
    /// </summary>
    private void fUpdateFlaskApart(UWModernLayout.ElementEnum peElement, RawImage pOImage, Texture2D pOSource, Text pOText,
        string psValue, Vector2 pODefault)
    {
        Texture2D lOFlask = fKeyed(pOSource, mbFreeFlaskMask, mOKeyedFree);

        if (lOFlask == null)
        {
            pOImage.enabled = false;
            pOText.enabled = false;
            return;
        }

        float lfScale = miPixelScale * UWModernLayout.Scale(peElement);
        int liTextSize = Mathf.Max(9, Mathf.RoundToInt(FontSize * UWModernLayout.Scale(peElement)));
        float lfFlaskWidth = lOFlask.width * lfScale * UWModernHudArt.PixelAspectX;
        float lfFlaskHeight = lOFlask.height * lfScale;
        Rect lOPlaced = UWModernLayout.Place(peElement,
            new Rect(pODefault.x, pODefault.y, lfFlaskWidth, lfFlaskHeight + lfScale + (liTextSize * 1.5f)));

        UWModernLayout.Report(peElement, lOPlaced);

        pOImage.texture = lOFlask;
        pOImage.enabled = true;

        RectTransform lORect = (RectTransform)pOImage.transform;
        lORect.anchoredPosition = lOPlaced.position;
        lORect.sizeDelta = new Vector2(lfFlaskWidth, lfFlaskHeight);

        // No values in Classic Wide: the original shows none (the stats page has them).
        pOText.enabled = !UWModernClassicFrame.IsActive;
        pOText.fontSize = liTextSize;
        pOText.text = psValue;
        ((RectTransform)pOText.transform).anchoredPosition = new Vector2(lOPlaced.center.x, lOPlaced.y + lfFlaskHeight + lfScale);
        ((RectTransform)pOText.transform).sizeDelta = new Vector2(120f, liTextSize * 1.5f);
    }

    /// <summary>
    /// THE STONE SHELF, A DECO PANEL (per user, 2026-10-09: "the stone shelf freely placed and
    /// sized, without a function - one can put the power gem or the flasks on it"): the original's
    /// ledge at the width chosen in the inspector (UWUserSettings.ModernStoneShelfWidth), placed and
    /// sized like any part, drawn under all of them; by default bottom left above the shelf.
    /// </summary>
    private void fUpdateStoneShelf()
    {
        if (!UWModernLayout.IsStoneShelfShown)
        {
            mOStoneShelf.enabled = false;
            return;
        }

        int liWidth = Mathf.Clamp(StoneShelfWidth, UWDataImport.UWData.UWHudArt.StoneShelfMinWidth, StoneShelfMaxWidth);
        string lsKey = liWidth + "/" + UWColourVision.Version;

        if (mOStoneShelfTexture == null || msStoneShelfKey != lsKey)
        {
            if (mOStoneShelfTexture != null)
                Destroy(mOStoneShelfTexture);

            mOStoneShelfTexture = UWModernHudArt.BuildStoneShelf(mOUi.mOUWData.Textures, liWidth, mOUi.TextureFilterMode);
            msStoneShelfKey = lsKey;
        }

        float liScale = miPixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.StoneShelf);
        float lfWidth = mOStoneShelfTexture.width * liScale * UWModernHudArt.PixelAspectX;
        float lfHeight = mOStoneShelfTexture.height * liScale;
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.StoneShelf,
            new Rect(ShelfMargin * miPixelScale, Screen.height * 0.35f, lfWidth, lfHeight));

        UWModernLayout.Report(UWModernLayout.ElementEnum.StoneShelf, lOPlaced);

        mOStoneShelf.texture = mOStoneShelfTexture;
        mOStoneShelf.enabled = true;
        ((RectTransform)mOStoneShelf.transform).anchoredPosition = lOPlaced.position;
        ((RectTransform)mOStoneShelf.transform).sizeDelta = lOPlaced.size;
    }

    /// <summary>
    /// THE POWER GEM APART (Classic+, per user, 2026-10-09: "in the original the gem stands alone" -
    /// we had put it between the flasks): its own element (UWModernLayout.ElementEnum.PowerGem) on
    /// the original's stone stand, by default right of the shelf, the gem picture (inactive,
    /// charging, full) laid on the stand as on the shelf.
    /// </summary>
    private void fUpdateGemStand(Rect pOVitals)
    {
        float liScale = miPixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.PowerGem);
        float lfWidth = mOGemStandTexture.width * liScale * UWModernHudArt.PixelAspectX;
        float lfHeight = mOGemStandTexture.height * liScale;
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.PowerGem,
            new Rect(pOVitals.xMax + (ShelfMargin * miPixelScale), pOVitals.y, lfWidth, lfHeight));

        UWModernLayout.Report(UWModernLayout.ElementEnum.PowerGem, lOPlaced);

        mOGemStand.texture = mOGemStandTexture;
        mOGemStand.enabled = true;
        ((RectTransform)mOGemStand.transform).anchoredPosition = lOPlaced.position;
        ((RectTransform)mOGemStand.transform).sizeDelta = lOPlaced.size;

        Texture2D lOGem = fKeyed(mOCharacter.PowerGemTeture, mbGemMask);

        if (lOGem == null)
        {
            mOPowerGem.enabled = false;
            return;
        }

        mOPowerGem.texture = lOGem;
        mOPowerGem.enabled = true;

        RectTransform lORect = (RectTransform)mOPowerGem.transform;
        lORect.anchoredPosition = new Vector2(lOPlaced.x + (mOGemOnStand.x * liScale * UWModernHudArt.PixelAspectX),
            lOPlaced.y + ((mOGemStandTexture.height - mOGemOnStand.y - lOGem.height) * liScale));
        lORect.sizeDelta = new Vector2(lOGem.width * liScale * UWModernHudArt.PixelAspectX, lOGem.height * liScale);
    }

    /// <summary>The shelf and the masks, built once and again when the colour help changes or the
    /// gem goes apart or back.</summary>
    private void fEnsureArt()
    {
        bool lbGemApart = UWModernLayout.IsGemApart;

        if (miArtVersion == UWColourVision.Version && mOShelfTexture != null && mbGemApart == lbGemApart)
            return;

        miArtVersion = UWColourVision.Version;
        mbGemApart = lbGemApart;

        if (mOShelfTexture != null)
            Destroy(mOShelfTexture);

        if (mOGemStandTexture != null)
            Destroy(mOGemStandTexture);

        foreach (Texture2D lOOld in mOKeyed.Values)
            Destroy(lOOld);

        mOKeyed.Clear();

        foreach (Texture2D lOOld in mOKeyedFree.Values)
            Destroy(lOOld);

        mOKeyedFree.Clear();

        DataImport lOData = mOUi.mOUWData;

        mOShelfTexture = UWModernHudArt.BuildShelf(lOData.Textures, mOUi.TextureFilterMode, out mOShelfLayout, mbGemApart);
        mOGemStandTexture = UWModernHudArt.BuildGemStand(lOData.Textures, mOUi.TextureFilterMode, out mOGemOnStand);
        mbFlaskMask = UWModernHudArt.BuildFlaskMask(lOData.Flasks);
        mbFreeFlaskMask = UWModernHudArt.BuildFreeFlaskMask(lOData.Flasks);
        mbGemMask = UWModernHudArt.BuildGemMask(lOData.PowerGem);
    }

    /// <summary>A flask or gem picture with its backdrop keyed out, kept per picture (in
    /// pOCache, mOKeyed unless given - the free flasks keep theirs apart).</summary>
    private Texture2D fKeyed(Texture2D pOSource, bool[] pbMask,
        System.Collections.Generic.Dictionary<Texture2D, Texture2D> pOCache = null)
    {
        if (pOSource == null)
            return null;

        System.Collections.Generic.Dictionary<Texture2D, Texture2D> lOCache = pOCache ?? mOKeyed;
        Texture2D lOKeyed;

        if (lOCache.TryGetValue(pOSource, out lOKeyed) && lOKeyed != null)
            return lOKeyed;

        // UWCharacter rebuilds its pictures when the colour help changes, so old keys die off;
        // a bound keeps them from piling up in between.
        if (lOCache.Count > 256)
        {
            foreach (Texture2D lOOld in lOCache.Values)
                Destroy(lOOld);

            lOCache.Clear();
        }

        lOKeyed = UWModernHudArt.ApplyMask(pOSource, pbMask);
        lOCache[pOSource] = lOKeyed;

        return lOKeyed;
    }

    /// <summary>Lays a picture onto the shelf, its top-left corner at a shelf pixel (rows from
    /// the top).</summary>
    private void fPlaceOnShelf(RawImage pOImage, Texture2D pOTexture, Vector2Int pOAt, Vector2 pOOrigin, float liScale)
    {
        if (pOTexture == null)
        {
            pOImage.enabled = false;
            return;
        }

        pOImage.texture = pOTexture;
        pOImage.enabled = true;

        RectTransform lORect = (RectTransform)pOImage.transform;
        lORect.anchoredPosition = new Vector2(pOOrigin.x + (pOAt.x * liScale * UWModernHudArt.PixelAspectX),
            pOOrigin.y + ((mOShelfLayout.Height - pOAt.y - pOTexture.height) * liScale));
        lORect.sizeDelta = new Vector2(pOTexture.width * liScale * UWModernHudArt.PixelAspectX, pOTexture.height * liScale);
    }

    private static float fWidth(Texture2D pOTexture) => pOTexture != null ? pOTexture.width : 0f;

    /// <summary>The active spells top left, rebuilt when they change (ActiveSpellVersion), as the
    /// classic shelf builds them (UWHudRunes).</summary>
    private void fUpdateSpells()
    {
        if (miSpellVersion == mOCharacter.ActiveSpellVersion)
            return;

        miSpellVersion = mOCharacter.ActiveSpellVersion;

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
        {
            RawImage lOImage = mOSpellIcons[liSlot];

            if (mOSpellTextures[liSlot] != null)
            {
                Destroy(mOSpellTextures[liSlot]);
                mOSpellTextures[liSlot] = null;
            }

            UWTexture lOSource = null;

            if (liSlot < mOCharacter.ActiveSpells.Count)
            {
                UWActiveSpellEffect lOSpell = mOCharacter.ActiveSpells[liSlot];
                int liIcon = UWRunicMagic.GetIconIndex(lOSpell.MajorClass, lOSpell.MinorClass);

                if (liIcon < UWRunicMagic.NoIcon)
                    lOSource = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.SPELLS, liIcon);
            }

            if (lOSource == null)
            {
                lOImage.enabled = false;
                continue;
            }

            UWIconPalette.ApplySmooth(lOImage);
            mOSpellTextures[liSlot] = UWIconTextureBuilder.Build(lOSource, mOUi.TextureFilterMode);
            lOImage.texture = mOSpellTextures[liSlot];
            lOImage.enabled = true;
        }
    }

    /// <summary>
    /// THE ACTIVE SPELLS (per user, 2026-10-04: the rune panel now covers the top left): at the top
    /// right, left of the minimap - or of the character panel while it is out - as a buff row;
    /// the pointer over one shows its name and state.
    /// </summary>
    private void fPlaceSpells()
    {
        float liScale = miPixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.Spells);
        float lfMargin = 4f * miPixelScale;
        float lfRight = Screen.width - lfMargin;
        UWModernMinimap lOMap = UWModernMinimap.Instance;
        UWModernPanel lOPanel = UWModernPanel.Instance;

        if (lOMap != null && lOMap.ScreenRect.width > 0f)
            lfRight = Mathf.Min(lfRight, lOMap.ScreenRect.xMin - lfMargin);

        if (lOPanel != null && lOPanel.IsOpen)
            lfRight = Mathf.Min(lfRight, lOPanel.LeftEdge - lfMargin);

        float lfTop = Screen.height - lfMargin;
        float lfX = lfRight;

        // Right-aligned, the first spell leftmost as on the classic page.
        float lfWidth = 0f;

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
        {
            if (mOSpellIcons[liSlot].enabled && mOSpellTextures[liSlot] != null)
                lfWidth += ((mOSpellTextures[liSlot].width * UWModernHudArt.PixelAspectX) + 3) * liScale;
        }

        // Its own place (UWModernLayout): right-aligned at its anchor, as wide as the spells - three
        // places' room while the layout is edited without any.
        float lfRowHeight = 17f * liScale;
        float lfRowWidth = lfWidth > 0f ? lfWidth - (3 * liScale) : (UWModernLayout.IsEditing ? 3f * 20f * liScale : 0f);

        if (lfRowWidth > 0f)
        {
            Rect lODefault = new Rect(lfRight - lfRowWidth, lfTop - lfRowHeight, lfRowWidth, lfRowHeight);
            Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Spells, lODefault);

            lfRight = lOPlaced.xMax;
            lfTop = lOPlaced.yMax;
            UWModernLayout.Report(UWModernLayout.ElementEnum.Spells, lOPlaced);
        }

        lfX = Mathf.Round(lfRight - lfWidth + (3 * liScale));

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
        {
            RawImage lOImage = mOSpellIcons[liSlot];
            Texture2D lOTexture = mOSpellTextures[liSlot];

            if (!lOImage.enabled || lOTexture == null)
            {
                mOSpellRects[liSlot] = Rect.zero;
                continue;
            }

            Rect lORect = new Rect(lfX, lfTop - (lOTexture.height * liScale), lOTexture.width * liScale * UWModernHudArt.PixelAspectX, lOTexture.height * liScale);

            mOSpellRects[liSlot] = lORect;
            ((RectTransform)lOImage.transform).anchoredPosition = lORect.position;
            ((RectTransform)lOImage.transform).sizeDelta = lORect.size;
            lfX += ((lOTexture.width * UWModernHudArt.PixelAspectX) + 3) * liScale;
        }

        // The hover text below the row, right-aligned to it.
        string lsTip = miHoveredSpell >= 0 && mOUi.Runes != null ? mOUi.Runes.ActiveSpellText(miHoveredSpell) : string.Empty;

        mOSpellTip.enabled = !string.IsNullOrEmpty(lsTip);
        mOSpellTip.text = lsTip;
        mOSpellTip.fontSize = Mathf.Max(9, Mathf.RoundToInt(FontSize * UWModernLayout.Scale(UWModernLayout.ElementEnum.Spells)));

        RectTransform lOTip = (RectTransform)mOSpellTip.transform;
        lOTip.anchoredPosition = new Vector2(lfRight, lfTop - (19f * liScale));
        lOTip.sizeDelta = new Vector2(600f, FontSize * 2f);
    }

    /// <summary>Whether a screen point lies on an active spell (UWModernPointer: the right button
    /// ends it there instead of switching the pointer).</summary>
    public bool IsOverSpellIcons(Vector2 pOPointer)
    {
        return fSpellAt(pOPointer) >= 0 || mOHeadingRect.Contains(pOPointer)
            || (mOCompass != null && mOCompass.ScreenRect.Contains(pOPointer))
            || mORuneHollowRect.Contains(pOPointer)
            || (mOStats != null && mOStats.ScreenRect.Contains(pOPointer))
            || (mOInventoryPage != null && mOInventoryPage.Contains(pOPointer));
    }

    private int fSpellAt(Vector2 pOPointer)
    {
        for (int liSlot = 0; liSlot < mOSpellRects.Length; liSlot++)
        {
            if (mOSpellRects[liSlot].width > 0f && mOSpellRects[liSlot].Contains(pOPointer))
                return liSlot;
        }

        return -1;
    }

    /// <summary>The pointer free over an active spell: the left button names it in the messages,
    /// the right button ends it (as one takes a buff away in an MMO; the classic page has the two
    /// the other way round).</summary>
    private void fUpdateSpellClicks()
    {
        miHoveredSpell = -1;
        mbHeadingHovered = false;

        if (!IsShowing || IsOpen || UWControls.IsTextEntryActive || !UWModernPointer.IsFree || UnityEngine.InputSystem.Mouse.current == null
            || mOScheme == null || mOScheme.Controls == null || mOUi.Runes == null)
            return;

        Vector2 lOAt = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        // The heading: the status, as the classic compass's click - and the original's compass
        // the same (UWModernCompass).
        mbHeadingHovered = mOHeadingRect.Contains(lOAt);

        // On the stone disc's arrows the press walks instead (fUpdateMoveArrowClicks).
        bool lbOnCompass = mOCompass != null && mOCompass.ScreenRect.Contains(lOAt) && mOCompass.ArrowAt(lOAt) < 0;

        if ((mbHeadingHovered || lbOnCompass) && mOScheme.Controls.Player.CursorDrag.WasPressedThisFrame())
        {
            mOUi.ReportStatus();
            return;
        }

        int liSlot = fSpellAt(lOAt);

        if (liSlot < 0)
            return;

        miHoveredSpell = liSlot;

        UWControls lOControls = mOScheme.Controls;

        if (lOControls.Player.CursorDrag.WasPressedThisFrame())
            mOUi.Runes.DescribeActiveSpell(liSlot);
        else if (lOControls.Player.Interact.WasPressedThisFrame())
            mOUi.Runes.CancelActiveSpell(liSlot);
    }

    private UWModernCompass mOCompass;

    /// <summary>The original's stats panel (UWModernLayout.IsStatsPanelShown).</summary>
    private UWModernStatsPage mOStats;

    /// <summary>The original's character panel with its inventory (UWModernLayout.IsCharacterPageShown).</summary>
    private UWModernInventoryPage mOInventoryPage;

    // ------------------------------------------------- The rune hollow

    /// <summary>The hollow with the prepared runes (UWModernLayout.IsRuneHollowShown): its picture,
    /// the three runes in it, the rune icons (palette indices, UWIconPalette), where it was drawn.</summary>
    private RawImage mORuneHollow;

    private Texture2D mORuneHollowTexture;

    private int miRuneHollowVersion = -1;

    private bool mbRuneHollowOutline;

    private readonly RawImage[] mORuneHollowRunes = new RawImage[UWHudRunes.SelectedRuneCount];

    private readonly Texture2D[] mORuneIcons = new Texture2D[UWPlayerData.RuneCount];

    private Rect mORuneHollowRect;

    /// <summary>
    /// THE RUNE HOLLOW AS AN ELEMENT (Classic+, per user 2026-10-10: "the hollow with the
    /// prepared runes"): the frame's recess (UWHudArt.BuildRuneHollow) with the prepared runes at
    /// the frame's places in it, placed by UWModernLayout - by default right of the compass, under
    /// the heading without it. A click casts as on the frame (fUpdateRuneHollowClicks).
    /// </summary>
    private void fUpdateRuneHollow()
    {
        mORuneHollowRect = Rect.zero;

        if (!UWModernLayout.IsRuneHollowShown)
        {
            mORuneHollow.enabled = false;

            foreach (RawImage lORune in mORuneHollowRunes)
                lORune.enabled = false;

            return;
        }

        bool lbOutline = UWUserSettings.ModernRuneHollowOutline;

        if (mORuneHollowTexture == null || miRuneHollowVersion != UWColourVision.Version || mbRuneHollowOutline != lbOutline)
        {
            if (mORuneHollowTexture != null)
                Destroy(mORuneHollowTexture);

            mORuneHollowTexture = UWModernHudArt.BuildRuneHollow(mOUi.mOUWData.Textures, lbOutline, mOUi.TextureFilterMode);
            miRuneHollowVersion = UWColourVision.Version;
            mbRuneHollowOutline = lbOutline;
        }

        // The outline puts the hollow a pixel in.
        int liPad = lbOutline ? 1 : 0;

        float lfScale = miPixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.RuneHollow);
        float lfAspect = lfScale * UWModernHudArt.PixelAspectX;
        float lfWidth = mORuneHollowTexture.width * lfAspect;
        float lfHeight = mORuneHollowTexture.height * lfScale;
        bool lbCompass = mOCompass != null && mOCompass.ScreenRect.width > 0f;
        Rect lODefault = lbCompass
            ? new Rect(mOCompass.ScreenRect.xMax + (2f * miPixelScale), mOCompass.ScreenRect.center.y - (lfHeight * 0.5f), lfWidth, lfHeight)
            : new Rect(mOHeadingRect.center.x - (lfWidth * 0.5f), mOHeadingRect.y - (2f * miPixelScale) - lfHeight, lfWidth, lfHeight);
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.RuneHollow, lODefault);

        UWModernLayout.Report(UWModernLayout.ElementEnum.RuneHollow, lOPlaced);

        mORuneHollow.texture = mORuneHollowTexture;
        mORuneHollow.enabled = true;
        ((RectTransform)mORuneHollow.transform).anchoredPosition = lOPlaced.position;
        ((RectTransform)mORuneHollow.transform).sizeDelta = lOPlaced.size;
        mORuneHollowRect = lOPlaced;

        System.Collections.Generic.IReadOnlyList<int> lOShelf = mOUi.Runes != null ? mOUi.Runes.SelectedRunes : null;

        for (int liAt = 0; liAt < mORuneHollowRunes.Length; liAt++)
        {
            RawImage lOImage = mORuneHollowRunes[liAt];
            int liRune = lOShelf != null && liAt < lOShelf.Count ? lOShelf[liAt] : -1;
            Texture2D lOIcon = fRuneIcon(liRune);

            lOImage.enabled = lOIcon != null;

            if (lOIcon == null)
                continue;

            float lfIconHeight = lOIcon.height * lfScale;

            lOImage.texture = lOIcon;
            ((RectTransform)lOImage.transform).anchoredPosition = new Vector2(
                lOPlaced.x + ((liPad + UWHudArt.RuneHollowRuneX + (liAt * UWHudArt.RuneHollowPitch)) * lfAspect),
                lOPlaced.yMax - ((liPad + UWHudArt.RuneHollowRuneY) * lfScale) - lfIconHeight);
            ((RectTransform)lOImage.transform).sizeDelta = new Vector2(lOIcon.width * lfAspect, lfIconHeight);
        }
    }

    /// <summary>A rune's icon (OBJECTS.GR from the first rune on), built once.</summary>
    private Texture2D fRuneIcon(int piRune)
    {
        if (piRune < 0 || piRune >= mORuneIcons.Length)
            return null;

        if (mORuneIcons[piRune] == null)
        {
            UWTexture lOSource = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, UWPlayerData.FirstRuneObjectId + piRune);

            if (lOSource != null)
                mORuneIcons[piRune] = UWIconTextureBuilder.Build(lOSource, mOUi.TextureFilterMode);
        }

        return mORuneIcons[piRune];
    }

    /// <summary>A left click anywhere in the hollow casts what lies in it, as on the frame
    /// (UWHudRunes.fUpdateSpellCast: the right button does nothing, not while the view roams) -
    /// with the pointer free.</summary>
    private void fUpdateRuneHollowClicks()
    {
        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (!IsShowing || IsOpen || UWControls.IsTextEntryActive || !UWModernPointer.IsFree || lOMouse == null
            || mOScheme == null || mOScheme.Controls == null || mORuneHollowRect.width <= 0f || mOUi.Runes == null
            || UWRoamingSight.IsAnyRunning)
            return;

        if (mOScheme.Controls.Player.CursorDrag.WasPressedThisFrame() && mORuneHollowRect.Contains(lOMouse.position.ReadValue()))
            mOUi.Runes.CastShelf();
    }

    // ------------------------------------------------- The movement arrows on the compass's disc

    /// <summary>Whether the button held on the compass disc's arrows was pressed there.</summary>
    private bool mbArrowPress;

    /// <summary>The command of each arrow, as the classic frame's (UWHudCompass).</summary>
    private static readonly int[] miArrowCommands =
    {
        UWEasyMovement.CommandTurnLeft,
        UWEasyMovement.CommandStepForward,
        UWEasyMovement.CommandTurnRight
    };

    /// <summary>
    /// THE MOVEMENT ARROWS ON THE COMPASS'S STONE DISC (UWModernCompass.ArrowAt) work as on the
    /// classic frame (Interaction.fTryEasyMovement): either mouse button held on one steps or
    /// turns and repeats (UWPlayerMovement.HoldEasyMovement) - with the pointer free, and only
    /// for a press that began on the arrows, so a drag from elsewhere passing over them does not
    /// walk. (The arrows as an element of their own, 2026-10-10, went again the same day, per
    /// user: with the disc they are not needed, and alone they looked lost.)
    /// </summary>
    private void fUpdateMoveArrowClicks()
    {
        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (!IsShowing || IsOpen || UWControls.IsTextEntryActive || !UWModernPointer.IsFree || lOMouse == null
            || mOScheme == null || mOScheme.Controls == null || mOCompass == null)
        {
            mbArrowPress = false;
            return;
        }

        UWControls lOControls = mOScheme.Controls;
        bool lbHeld = lOControls.Player.CursorDrag.IsPressed() || lOControls.Player.Interact.IsPressed();
        int liArrow = mOCompass.ArrowAt(lOMouse.position.ReadValue());

        if (!lbHeld)
        {
            mbArrowPress = false;
            return;
        }

        if (lOControls.Player.CursorDrag.WasPressedThisFrame() || lOControls.Player.Interact.WasPressedThisFrame())
            mbArrowPress = liArrow >= 0;

        if (!mbArrowPress || liArrow < 0 || UWScene.PlayerMovement == null)
            return;

        UWScene.PlayerMovement.HoldEasyMovement(miArrowCommands[liArrow]);
    }

    private UWModernScroll mOScroll;

    /// <summary>The original's compass, and the heading's text giving way to it while it shows.</summary>
    private void fUpdateCompass()
    {
        if (mOCompass == null)
            return;

        mOCompass.Update(miPixelScale);
        mOHeading.enabled = mOCompass.ScreenRect.width <= 0f;
    }

    /// <summary>CLASSIC WIDE: the gargoyle is the frame's; the strip, the heading and the foe's
    /// name stay away, and the eyes alone are drawn on its head, lit as the strip would show them.</summary>
    private void fUpdateStripClassic()
    {
        mOStrip.enabled = false;
        mOHeading.enabled = false;
        mOFoeName.enabled = false;
        mOFoeState.enabled = false;
        mOHeadingRect = Rect.zero;

        Texture2D lOEyes = mOEyes != null && mOEyes.IsLit ? mOEyes.CurrentFrame : null;

        mOStripEyes.enabled = lOEyes != null;

        if (lOEyes == null)
            return;

        Rect lORect = UWModernClassicFrame.EyesRect(lOEyes.width, lOEyes.height);
        RectTransform lOTransform = (RectTransform)mOStripEyes.transform;

        mOStripEyes.texture = lOEyes;
        mOStripEyes.color = new Color(1f, 1f, 1f, mOEyes.Alpha);
        // Anchored at the top centre, its pivot top left (fBuild).
        lOTransform.anchoredPosition = new Vector2(lORect.x - (Screen.width * 0.5f), -(Screen.height - lORect.yMax));
        lOTransform.sizeDelta = lORect.size;
    }

    private static readonly string[] msHeadings =
    {
        "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"
    };

    /// <summary>The heading in sixteen points, as many as the compass needle has (UWHudCompass).</summary>
    private void fUpdateHeading()
    {
        Camera lOCamera = Camera.main;

        if (lOCamera == null)
            return;

        // The classic compass's step (UWHudCompass.CurrentStep): the view's in sixteen points, and
        // in the void the frozen one, as the original's compass shows it - until 2026-10-09 the
        // heading followed the view there.
        int liStep = mOUi.Compass.CurrentStep;

        mOHeading.text = msHeadings[liStep];

        // Its button area around the word, and gold under the free pointer.
        float lfHeadingWidth = Mathf.Max(mOHeading.preferredWidth, FontSize * 2f) + FontSize;
        float lfHeadingHeight = FontSize * 1.8f;

        mOHeadingRect = new Rect((Screen.width - lfHeadingWidth) * 0.5f, Screen.height - (6f * miPixelScale) - lfHeadingHeight,
            lfHeadingWidth, lfHeadingHeight);
        mOHeading.color = mbHeadingHovered ? msHeadingHover : new Color(0.86f, 0.86f, 0.84f);
    }

    // ------------------------------------------------- The heading strip

    private RawImage mOStrip;

    private RawImage mOStripEyes;

    private Text mOFoeName;

    private Text mOFoeState;

    private UWEyesDisplay mOEyes;

    /// <summary>0 closed (the heading alone) to 1 open (the gargoyle and the foe).</summary>
    private float mfStripOpen;

    /// <summary>The open strip's half width, original pixels - from the foe's name, fixed while
    /// the strip moves so the brand stands still.</summary>
    private int miStripOpenHalf = UWModernHudArt.StripMinOpenHalf;

    /// <summary>The foe's text, from the heading's centre.</summary>
    private const float FoeFromCentre = 14f;

    /// <summary>Per width and open half (width * 1000 + half).</summary>
    private readonly System.Collections.Generic.Dictionary<int, Texture2D> mOStripTextures =
        new System.Collections.Generic.Dictionary<int, Texture2D>();

    private int miStripArtVersion = -1;

    /// <summary>Seconds the strip takes to open or close.</summary>
    private const float StripSeconds = 0.25f;

    private static readonly Color[] msFoeColours =
    {
        new Color32(120, 210, 110, 255), new Color32(230, 200, 90, 255), new Color32(230, 90, 70, 255)
    };

    private static readonly string[] msFoeStates = { "Healthy", "Wounded", "Critical" };

    /// <summary>
    /// THE HEADING STRIP (UWModernHudArt.BuildHeadingStrip, per user on mockups, 2026-10-04):
    /// closed around the heading; when a hit lights the gargoyle's eyes (UWEyesDisplay) it opens
    /// to both sides - the burned gargoyle with the original's eyes at the left, the foe's name and
    /// condition at the right - and closes again once they have faded. A click on it reports the
    /// status, as the heading did.
    /// </summary>
    private void fUpdateStrip()
    {
        if (mOEyes == null)
            mOEyes = GetComponent<UWEyesDisplay>();

        if (miStripArtVersion != UWColourVision.Version)
        {
            foreach (Texture2D lOOld in mOStripTextures.Values)
            {
                if (lOOld != null)
                    Destroy(lOOld);
            }

            mOStripTextures.Clear();
            miStripArtVersion = UWColourVision.Version;
        }

        if (UWModernClassicFrame.IsActive)
        {
            fUpdateStripClassic();
            return;
        }

        bool lbLit = mOEyes != null && mOEyes.IsLit;
        float lfElement = UWModernLayout.Scale(UWModernLayout.ElementEnum.Heading);
        float lfScale = miPixelScale * lfElement;
        string lsName = lbLit ? mOEyes.TargetName : null;
        int liGroup = mOEyes != null ? Mathf.Clamp(mOEyes.Group, 0, 2) : 0;

        // The texts first: their width decides how far the strip opens - decided while it is
        // closed, so the brand does not move while it opens.
        mOFoeName.text = lsName ?? string.Empty;
        mOFoeName.fontSize = Mathf.Max(9, Mathf.RoundToInt(FontSize * lfElement));
        mOFoeState.text = msFoeStates[liGroup];
        mOFoeState.fontSize = Mathf.Max(9, Mathf.RoundToInt(FontSize * 0.75f * lfElement));
        mOHeading.fontSize = Mathf.RoundToInt(FontSize * 1.1f * lfElement);

        if (lbLit && mfStripOpen <= 0f)
        {
            float lfText = Mathf.Max(mOFoeName.preferredWidth, mOFoeState.preferredWidth) / lfScale;

            miStripOpenHalf = Mathf.Clamp(Mathf.CeilToInt(FoeFromCentre + lfText + 6f), UWModernHudArt.StripMinOpenHalf, 100);
        }

        mfStripOpen = Mathf.MoveTowards(mfStripOpen, lbLit ? 1f : 0f, Time.unscaledDeltaTime / StripSeconds);

        float lfEase = mfStripOpen * mfStripOpen * (3f - (2f * mfStripOpen));
        int liWidth = Mathf.RoundToInt(Mathf.Lerp(UWModernHudArt.StripClosedWidth, 2 * miStripOpenHalf, lfEase));

        // WHILE THE COMPASS STANDS IN FOR THE TEXT (per user, 2026-10-09: the closed strip was
        // empty then) the gargoyle sits in the closed strip's middle and moves to its place at the
        // left as the strip opens; without it the brand shows only on the open strip, as before.
        // The compass's rect is the last frame's (fUpdateCompass runs after this).
        bool lbCompass = mOCompass != null && mOCompass.ScreenRect.width > 0f;

        // THE GARGOYLE AT THE ORIGINAL'S PIXEL PROPORTION (UWModernHudArt.PixelAspectX): the strip
        // is built that much wider in its own pixels and shown that much narrower, so it keeps its
        // width on the screen (the text's room) while the head and its eyes are narrowed.
        int liTexWidth = Mathf.RoundToInt(liWidth / UWModernHudArt.PixelAspectX);
        int liTexOpenHalf = Mathf.RoundToInt(miStripOpenHalf / UWModernHudArt.PixelAspectX);
        int liBrandLeft = UWModernHudArt.BrandLeft(liTexWidth, liTexOpenHalf);

        if (lbCompass)
            liBrandLeft = Mathf.RoundToInt(Mathf.Lerp(UWHudArt.BrandCentredLeft(liTexWidth), liBrandLeft, lfEase));

        // The brand's place follows from the width, the open half and the compass.
        int liKey = (((liTexWidth * 1000) + liTexOpenHalf) * 2) + (lbCompass ? 1 : 0);

        if (!mOStripTextures.TryGetValue(liKey, out Texture2D lOTexture) || lOTexture == null)
        {
            lOTexture = lbCompass
                ? UWModernHudArt.BuildHeadingStripWithBrandAt(mOUi.mOUWData.Textures, liTexWidth, liBrandLeft, mOUi.TextureFilterMode)
                : UWModernHudArt.BuildHeadingStrip(mOUi.mOUWData.Textures, liTexWidth, liTexOpenHalf, mOUi.TextureFilterMode,
                    Mathf.RoundToInt(UWModernHudArt.StripClosedWidth / UWModernHudArt.PixelAspectX));
            mOStripTextures[liKey] = lOTexture;
        }

        float lfTop = 4f * miPixelScale;
        float lfHeight = UWModernHudArt.StripHeight * lfScale;
        float lfWidth = liTexWidth * lfScale * UWModernHudArt.PixelAspectX;

        // Its own place (UWModernLayout): the strip's rect, the heading's offset from the top
        // centre all its parts are anchored at.
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Heading,
            new Rect((Screen.width - lfWidth) * 0.5f, Screen.height - lfTop - lfHeight, lfWidth, lfHeight));
        float lfShiftX = Mathf.Round(lOPlaced.center.x - (Screen.width * 0.5f));

        lfTop = Screen.height - lOPlaced.yMax;
        UWModernLayout.Report(UWModernLayout.ElementEnum.Heading, lOPlaced);

        mOStrip.texture = lOTexture;
        mOStrip.enabled = true;
        ((RectTransform)mOStrip.transform).anchoredPosition = new Vector2(lfShiftX, -lfTop);
        ((RectTransform)mOStrip.transform).sizeDelta = new Vector2(lfWidth, lfHeight);

        // The heading in the strip's middle, and its click area the strip.
        ((RectTransform)mOHeading.transform).anchoredPosition = new Vector2(lfShiftX, -lfTop);
        ((RectTransform)mOHeading.transform).sizeDelta = new Vector2(UWModernHudArt.StripClosedWidth * lfScale, lfHeight);
        mOHeadingRect = lOPlaced;

        // The eyes in the brand, once the strip shows them.
        Texture2D lOEyes = lbLit ? mOEyes.CurrentFrame : null;
        int liEyesX = liBrandLeft + UWModernHudArt.EyesLeftInBrand;

        mOStripEyes.enabled = lOEyes != null && liEyesX >= UWModernHudArt.LeatherLeft;

        if (mOStripEyes.enabled)
        {
            mOStripEyes.texture = lOEyes;
            mOStripEyes.color = new Color(1f, 1f, 1f, mOEyes.Alpha);
            ((RectTransform)mOStripEyes.transform).anchoredPosition = new Vector2(lfShiftX + ((liEyesX - (liTexWidth * 0.5f)) * lfScale * UWModernHudArt.PixelAspectX),
                -lfTop - ((UWModernHudArt.BrandTop + UWModernHudArt.EyesTopInBrand) * lfScale));
            ((RectTransform)mOStripEyes.transform).sizeDelta = new Vector2(lOEyes.width * lfScale * UWModernHudArt.PixelAspectX, lOEyes.height * lfScale);
        }

        // The foe at the right, once the strip is open.
        float lfFoe = Mathf.InverseLerp(0.85f, 1f, mfStripOpen) * (mOEyes != null ? mOEyes.Alpha : 0f);
        float lfFoeX = lfShiftX + (FoeFromCentre * lfScale);

        mOFoeName.enabled = lfFoe > 0f && !string.IsNullOrEmpty(lsName);
        mOFoeState.enabled = mOFoeName.enabled;

        if (mOFoeName.enabled)
        {
            mOFoeName.color = new Color(0.94f, 0.87f, 0.71f, lfFoe);
            ((RectTransform)mOFoeName.transform).anchoredPosition = new Vector2(lfFoeX, -lfTop - (3f * lfScale));
            ((RectTransform)mOFoeName.transform).sizeDelta = new Vector2((miStripOpenHalf - FoeFromCentre) * lfScale, 9f * lfScale);

            Color lOState = msFoeColours[liGroup];

            lOState.a = lfFoe;
            mOFoeState.color = lOState;
            ((RectTransform)mOFoeState.transform).anchoredPosition = new Vector2(lfFoeX, -lfTop - (12f * lfScale));
            ((RectTransform)mOFoeState.transform).sizeDelta = new Vector2((miStripOpenHalf - FoeFromCentre) * lfScale, 8f * lfScale);
        }
    }

    private void fUpdatePicture()
    {
        Sprite lOSprite = mOUi != null ? mOUi.CurrentWindowPicture : null;

        if (lOSprite == null)
        {
            if (mOPictureCanvas != null)
                mOPictureCanvas.enabled = false;

            return;
        }

        if (mOPictureCanvas == null)
        {
            GameObject lORoot = new GameObject("View picture", typeof(RectTransform), typeof(Canvas));
            lORoot.transform.SetParent(mOCanvas.transform, false);

            RectTransform lORootRect = (RectTransform)lORoot.transform;
            lORootRect.anchorMin = Vector2.zero;
            lORootRect.anchorMax = Vector2.one;
            lORootRect.offsetMin = Vector2.zero;
            lORootRect.offsetMax = Vector2.zero;

            mOPictureCanvas = lORoot.GetComponent<Canvas>();
            mOPictureCanvas.overrideSorting = true;
            mOPictureCanvas.sortingOrder = 45;

            mOPictureFrame = fCreateRawImage(lORoot.transform, "Leather", Vector2.zero, Vector2.zero);
            UWPixelArtUI.Apply(mOPictureFrame);
            mOPictureFrame.enabled = true;
            mOPicture = fCreateImage(lORoot.transform, "Picture", Vector2.zero, Vector2.zero, Color.white);
        }

        mOPictureCanvas.enabled = true;

        float liScale = miPixelScale;
        Vector2 lOSize = lOSprite.rect.size;
        int liFrameWidth = Mathf.RoundToInt(lOSize.x * UWModernHudArt.PixelAspectX) + UWModernHudArt.LeatherLeft + UWModernHudArt.LeatherRight + (2 * PictureBorder);
        int liFrameHeight = Mathf.RoundToInt(lOSize.y) + UWModernHudArt.LeatherTop + UWModernHudArt.LeatherBottom + (2 * PictureBorder);

        if (mOPictureFrameTexture == null || mOPictureFrameSize.x != liFrameWidth || mOPictureFrameSize.y != liFrameHeight
            || miPictureArtVersion != UWColourVision.Version)
        {
            if (mOPictureFrameTexture != null)
                Destroy(mOPictureFrameTexture);

            mOPictureFrameTexture = UWModernHudArt.BuildLeather(mOUi.mOUWData.Textures, liFrameWidth, liFrameHeight, mOUi.TextureFilterMode);
            mOPictureFrameSize = new Vector2Int(liFrameWidth, liFrameHeight);
            miPictureArtVersion = UWColourVision.Version;
            mOPictureFrame.texture = mOPictureFrameTexture;
        }

        float lfWidth = liFrameWidth * liScale;
        float lfHeight = liFrameHeight * liScale;
        float lfX = Mathf.Round((Screen.width - lfWidth) * 0.5f);
        float lfY = Mathf.Round((Screen.height * 0.55f) - (lfHeight * 0.5f));
        RectTransform lOFrame = (RectTransform)mOPictureFrame.transform;

        lOFrame.anchoredPosition = new Vector2(lfX, lfY);
        lOFrame.sizeDelta = new Vector2(lfWidth, lfHeight);

        mOPicture.sprite = lOSprite;

        RectTransform lOPicture = (RectTransform)mOPicture.transform;

        lOPicture.anchoredPosition = new Vector2(lfX + ((UWModernHudArt.LeatherLeft + PictureBorder) * liScale),
            lfY + ((UWModernHudArt.LeatherBottom + PictureBorder) * liScale));
        lOPicture.sizeDelta = new Vector2(lOSize.x * UWModernHudArt.PixelAspectX, lOSize.y) * liScale;
    }

    /// <summary>Where the message box starts above the flasks (screen pixels from the bottom) -
    /// the rune panel (UWModernRunePanel) reaches down to here at most.</summary>
    public float LeftColumnBottom => mfLeftColumnBottom;

    private float mfLeftColumnBottom;

    /// <summary>The message box moves right of the rune panel while the panel covers it (per user
    /// on the mockup, 2026-10-04).</summary>
    private void fPlaceMessages()
    {
        if (UWModernLayout.IsScrollShown)
            return;

        // Its own size and place (UWModernLayout); by default above the shelf, right of the rune
        // panel while that covers it.
        float lfScale = UWModernLayout.Scale(UWModernLayout.ElementEnum.Messages);
        float lfMargin = 6f * miPixelScale * lfScale;
        int liFontSize = Mathf.Max(9, Mathf.RoundToInt(FontSize * lfScale));
        float lfWidth = Mathf.Min(Screen.width * 0.45f, liFontSize * 36f);
        float lfHeight = (liFontSize * 1.35f * MessageLines) + (2f * lfMargin);

        // As tall as its text needs at least - long lines wrap, and the font's lines are taller
        // than 1.35 of its size (per user, 2026-10-04: the text stood out over the box's top).
        mOMessages.fontSize = liFontSize;
        ((RectTransform)mOMessages.transform).sizeDelta = new Vector2(lfWidth - (2f * lfMargin), 10f);
        lfHeight = Mathf.Max(lfHeight, Mathf.Ceil(mOMessages.preferredHeight) + (2f * lfMargin));

        float lfX = 6f * miPixelScale;
        float lfY = mfLeftColumnBottom;
        RectTransform lOBack = (RectTransform)mOMessageBack.transform;
        RectTransform lOText = (RectTransform)mOMessages.transform;
        UWModernRunePanel lORunes = UWModernRunePanel.Instance;

        if (lORunes != null && lORunes.ScreenRect.width > 0f && !UWModernLayout.IsPlaced(UWModernLayout.ElementEnum.Messages))
        {
            Rect lOPanel = lORunes.ScreenRect;

            if (lOPanel.yMin < lfY + lfHeight && lOPanel.yMax > lfY && lOPanel.xMax > lfX)
                lfX = Mathf.Round(lOPanel.xMax + lfX);
        }

        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Messages, new Rect(lfX, lfY, lfWidth, lfHeight));

        UWModernLayout.Report(UWModernLayout.ElementEnum.Messages, lOPlaced);

        mOMessages.fontSize = liFontSize;
        lOBack.anchoredPosition = lOPlaced.position;
        lOBack.sizeDelta = lOPlaced.size;
        lOText.anchoredPosition = lOPlaced.position + new Vector2(lfMargin, lfMargin);
        lOText.sizeDelta = lOPlaced.size - new Vector2(2f * lfMargin, 2f * lfMargin);

        // While the layout is edited the box shows, empty or not.
        if (UWModernLayout.IsEditing)
            mOMessageBack.enabled = true;
    }

    /// <summary>The backs behind the heading, the vitals, the messages and the spells (see
    /// mePartsWithBackBehind): placed on the parts' rects of this frame, rebuilt when the size, the
    /// back or the colours change.</summary>
    private void fUpdateBacks()
    {
        // Classic Wide: the frame is the back of everything.
        bool lbNone = UWModernClassicFrame.IsActive;

        for (int liAt = 0; liAt < mePartsWithBackBehind.Length; liAt++)
        {
            if (lbNone)
            {
                mOPartBacks[liAt].enabled = false;
                continue;
            }

            UWModernLayout.ElementEnum leElement = mePartsWithBackBehind[liAt];
            RawImage lOImage = mOPartBacks[liAt];
            UWModernBacks.Back lOBack = UWModernBacks.Get(leElement);
            Rect lORect = Rect.zero;
            bool lbShown = lOBack.Shape != UWDataImport.UWData.UWBackdropArt.ShapeEnum.None
                && UWModernLayout.TryGetRect(leElement, out lORect);

            if (leElement == UWModernLayout.ElementEnum.Messages && lbShown)
            {
                // In the dark box's place: shown when the box would be, or under the scroll.
                lbShown = mOMessageBack.enabled || UWModernLayout.IsScrollShown;
                mOMessageBack.enabled = false;
            }

            if (!lbShown)
            {
                lOImage.enabled = false;
                continue;
            }

            float lfScale = Mathf.Max(1f, miPixelScale * UWModernLayout.Scale(leElement));
            int liWidth = Mathf.CeilToInt(lORect.width / lfScale) + (2 * PartBackPad);
            int liHeight = Mathf.CeilToInt(lORect.height / lfScale) + (2 * PartBackPad);
            string lsKey = lOBack.Key + "/" + liWidth + "x" + liHeight + "/" + UWModernBacks.ArtVersion;

            if (msPartBackKeys[liAt] != lsKey || mOPartBackTextures[liAt] == null)
            {
                if (mOPartBackTextures[liAt] != null)
                    Destroy(mOPartBackTextures[liAt]);

                mOPartBackTextures[liAt] = UWModernHudArt.BuildBackdrop(mOUi.mOUWData.Textures, liWidth, liHeight, lOBack,
                    mOUi.TextureFilterMode, liAt + 1);
                msPartBackKeys[liAt] = lsKey;
                lOImage.texture = mOPartBackTextures[liAt];
            }

            RectTransform lOTransform = (RectTransform)lOImage.transform;

            lOTransform.anchoredPosition = new Vector2(lORect.center.x - (liWidth * lfScale * 0.5f), lORect.center.y - (liHeight * lfScale * 0.5f));
            lOTransform.sizeDelta = new Vector2(liWidth * lfScale, liHeight * lfScale);
            lOImage.enabled = true;
        }
    }

    /// <summary>The last five messages, as the scroll holds them (Interaction.ActiveStrings).</summary>
    private void fUpdateMessages()
    {
        // The original's scroll instead, when switched on: it draws and places itself.
        if (mOScroll != null && UWModernLayout.IsScrollShown)
        {
            mOMessages.enabled = false;
            mOMessageBack.enabled = false;
            mOScroll.Update(mOInteraction, miPixelScale, new Vector2(6f * miPixelScale, mfLeftColumnBottom));
            return;
        }

        if (mOScroll != null)
            mOScroll.Hide();

        mOMessages.enabled = true;

        System.Collections.Generic.IReadOnlyList<string> lOLines = mOInteraction.ActiveStrings;
        System.Text.StringBuilder lOText = new System.Text.StringBuilder();
        int liColour = 0;

        // The list keeps more for the tall scroll; the box shows the newest five.
        int liFirst = lOLines != null ? Mathf.Max(0, lOLines.Count - Interaction.ModernMessageCount) : 0;

        for (int liAt = liFirst; lOLines != null && liAt < lOLines.Count; liAt++)
        {
            if (liAt > liFirst)
                lOText.Append('\n');

            fAppendColoured(lOText, UWFontRenderer.CleanText(lOLines[liAt]), ref liColour);
        }

        if (mOInteraction.ShowsPromptTextCursor && Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f)
            lOText.Append('_');

        if (mOInteraction.IsWaitingForPage)
            lOText.Append("  [MORE]");

        mOMessages.text = lOText.ToString();
        mOMessageBack.enabled = lOText.Length > 0;
    }

    /// <summary>
    /// THE GAME'S COLOUR CODES in the messages (per user, 2026-10-05: "\6Save Game Succeeded.\0"
    /// showed its codes): a backslash and a digit switch the colour until the next code, across
    /// lines as in the original (UWFontRenderer.RenderLines); the codes are never shown. The
    /// colours are the classic message box's (UWHudMessageLog.LogColourIndices), 0 the modern
    /// text colour - on the dark leather the darker ones are lightened towards white until they
    /// read (fReadable). Codes beyond the table leave the colour as it is.
    /// </summary>
    private void fAppendColoured(System.Text.StringBuilder pOText, string psLine, ref int piColour)
    {
        bool lbOpen = fOpenColour(pOText, piColour);

        for (int liAt = 0; liAt < psLine.Length; liAt++)
        {
            if (!UWFont.IsColourCode(psLine, liAt))
            {
                pOText.Append(psLine[liAt]);
                continue;
            }

            int liCode = psLine[liAt + 1] - '0';

            liAt++;

            if (liCode >= UWHudMessageLog.LogColourIndices.Length || liCode == piColour)
                continue;

            if (lbOpen)
                pOText.Append("</color>");

            piColour = liCode;
            lbOpen = fOpenColour(pOText, piColour);
        }

        if (lbOpen)
            pOText.Append("</color>");
    }

    private bool fOpenColour(System.Text.StringBuilder pOText, int piColour)
    {
        if (piColour <= 0 || mOUi == null || mOUi.mOUWData == null || mOUi.mOUWData.Palettes == null)
            return false;

        Color32 lOColour = fReadable(UWScreenUi.GetColour(mOUi.mOUWData.Palettes, 0, UWHudMessageLog.LogColourIndices[piColour]));

        pOText.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(lOColour)).Append('>');

        return true;
    }

    /// <summary>The least brightness a message colour keeps on the dark leather.</summary>
    private const float MessageColourLuma = 140f;

    /// <summary>A colour lightened towards white until its brightness is MessageColourLuma (the
    /// classic red 91 becomes a soft red, the green 142 stays; the black of code 2 a grey).</summary>
    private static Color32 fReadable(Color32 pOColour)
    {
        float lfLuma = (0.299f * pOColour.r) + (0.587f * pOColour.g) + (0.114f * pOColour.b);

        if (lfLuma >= MessageColourLuma)
            return pOColour;

        float lfTowardsWhite = (MessageColourLuma - lfLuma) / (255f - lfLuma);

        return Color32.Lerp(pOColour, new Color32(255, 255, 255, 255), lfTowardsWhite);
    }

    /// <summary>The crosshair and the name of its target, asked ten times a second.</summary>
    private void fUpdateTarget()
    {
        bool lbCross = !IsOpen && (mOScheme == null || !mOScheme.IsUIModalOpen);

        // The view's middle against the screen's: in Classic Wide the hole's (UWModernClassicFrame).
        Vector2 lOOffset = UWModernClassicFrame.ViewCentre - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        ((RectTransform)mOCrosshair.transform).anchoredPosition = lOOffset;
        ((RectTransform)mOTarget.transform).anchoredPosition = lOOffset + new Vector2(0f, -4f * miPixelScale);

        // A spell waiting for its target (UWHudRunes.fAfterModernCast) shows the original's target
        // pointer instead of the crosshair - the free pointer turns into it (UWGameUI).
        // A ranged weapon charged to its minimum likewise (Interaction.IsRangedTargeting, per user,
        // 2026-10-04: the red circle shows when the charge suffices).
        //
        // THE CROSSHAIR IS THE GAME'S OWN POINTER (per user, 2026-10-04: the normal cursor instead of
        // the dot) - the cross the free pointer shows as well (UWGameUI), so locking and freeing
        // look the same; with the pointer free that one stands for it and the middle stays empty.
        bool lbLocked = lbCross && !UWModernPointer.IsFree;
        bool lbWaiting = lbLocked && (mOInteraction.IsSpellTargeting || mOInteraction.IsRangedTargeting);
        int liCursor = !lbWaiting ? (int)UWCursors.CursorEnum.Center
            : mOInteraction.IsSpellTargeting && mOInteraction.IsTargetSpellPending
            ? UWHudRunes.TargetSpellCursor : UWHudRunes.SpellTargetCursor;
        Texture2D lOCursor = lbLocked && mOUi.mOCursorTextures != null && liCursor < mOUi.mOCursorTextures.Count
            ? mOUi.mOCursorTextures[liCursor] : null;

        mOTargetCursor.enabled = lOCursor != null;
        // The dot only where the pointer pictures are missing.
        mOCrosshair.enabled = lbLocked && lOCursor == null;

        if (lOCursor != null)
        {
            mOTargetCursor.texture = lOCursor;
            ((RectTransform)mOTargetCursor.transform).anchoredPosition = lOOffset;
            ((RectTransform)mOTargetCursor.transform).sizeDelta = new Vector2(lOCursor.width * miPixelScale * UWModernHudArt.PixelAspectX, lOCursor.height * miPixelScale);
        }

        // The hold of E for the direct use (Interaction.ModernUseHoldProgress): a ring filling
        // clockwise around the pointer (per user, 2026-10-04 - a bar under the dot before); with
        // the pointer free around the pointer, where E acts then.
        float lfHold = mOInteraction.ModernUseHoldProgress;

        mOHoldBar.enabled = lbCross && lfHold >= 0f;

        if (mOHoldBar.enabled)
        {
            float lfRing = 24f * miPixelScale;
            RectTransform lORect = (RectTransform)mOHoldBar.transform;
            Vector2 lOAt = lOOffset;

            if (UWModernPointer.IsFree && UnityEngine.InputSystem.Mouse.current != null)
                lOAt = UnityEngine.InputSystem.Mouse.current.position.ReadValue() - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            lORect.anchoredPosition = lOAt;
            lORect.sizeDelta = new Vector2(lfRing, lfRing);
            mOHoldBar.fillAmount = lfHold;
        }

        mfTargetTimer -= Time.unscaledDeltaTime;

        if (mfTargetTimer > 0f)
            return;

        mfTargetTimer = 0.1f;
        mOTarget.text = lbCross ? mOInteraction.GetCrosshairTargetName() ?? string.Empty : string.Empty;

        fUpdatePadPrompt(lbCross && !UWModernPointer.IsFree);
    }

    private RawImage mOPadUseGlyph;

    private RawImage mOPadLookGlyph;

    private Text mOPadUseText;

    private Text mOPadLookText;

    /// <summary>
    /// THE GAMEPAD'S BUTTON HINT under the crosshair's target name (per user, 2026-10-08: "my wife
    /// would not manage without glyphs"): A with what it does there (Interaction.GetCrosshairPadVerb),
    /// X to look - while the pad is in use and the hints are on (UWUserSettings.ShowsPadHints), with
    /// the pointer locked. The glyphs of the buttons as bound now, their names where a glyph is
    /// missing.
    /// </summary>
    private void fUpdatePadPrompt(bool pbLocked)
    {
        string lsVerb = pbLocked && UWUserSettings.ShowsPadHints && mOInteraction != null ? mOInteraction.GetCrosshairPadVerb() : null;

        if (mOPadUseGlyph == null)
        {
            if (lsVerb == null)
                return;

            Transform lOParent = mOTarget.transform.parent;

            mOPadUseGlyph = fCreateRawImage(lOParent, "Pad use glyph", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f));
            mOPadLookGlyph = fCreateRawImage(lOParent, "Pad look glyph", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f));
            mOPadUseText = fCreateText(lOParent, "Pad use", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), TextAnchor.MiddleLeft);
            mOPadLookText = fCreateText(lOParent, "Pad look", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), TextAnchor.MiddleLeft);
        }

        bool lbShow = lsVerb != null;

        mOPadUseGlyph.enabled = lbShow;
        mOPadLookGlyph.enabled = lbShow;
        mOPadUseText.enabled = lbShow;
        mOPadLookText.enabled = lbShow;

        if (!lbShow)
            return;

        Texture2D lOUse = UWGlyphs.ForEntry("ModernUse", 1);
        Texture2D lOLook = UWGlyphs.ForEntry("ModernLook", 1);
        int liFontSize = mOTarget.fontSize;
        float lfGlyph = Mathf.Round(liFontSize * 1.5f);
        float lfGap = Mathf.Round(lfGlyph * 0.2f);

        mOPadUseText.fontSize = liFontSize;
        mOPadLookText.fontSize = liFontSize;
        mOPadUseText.text = lOUse != null ? lsVerb : UWGamepad.ButtonName(UWGlyphs.PadPath("ModernUse", 1)) + " " + lsVerb;
        mOPadLookText.text = lOLook != null ? "Look" : UWGamepad.ButtonName(UWGlyphs.PadPath("ModernLook", 1)) + " Look";

        float lfUseWidth = mOPadUseText.preferredWidth;
        float lfLookWidth = mOPadLookText.preferredWidth;
        float lfUseGlyph = lOUse != null ? lfGlyph + lfGap : 0f;
        float lfLookGlyph = lOLook != null ? lfGlyph + lfGap : 0f;
        float lfTotal = lfUseGlyph + lfUseWidth + lfGlyph + lfLookGlyph + lfLookWidth;
        float lfX = -lfTotal * 0.5f;
        float lfY = -(4f * miPixelScale) - (liFontSize * 1.5f);

        mOPadUseGlyph.texture = lOUse;
        mOPadUseGlyph.enabled = lOUse != null;
        fPlace(mOPadUseGlyph.transform, lfX, lfY, lfGlyph, lfGlyph);
        fPlace(mOPadUseText.transform, lfX + lfUseGlyph, lfY, lfUseWidth + 2f, lfGlyph);

        lfX += lfUseGlyph + lfUseWidth + lfGlyph;

        mOPadLookGlyph.texture = lOLook;
        mOPadLookGlyph.enabled = lOLook != null;
        fPlace(mOPadLookGlyph.transform, lfX, lfY, lfGlyph, lfGlyph);
        fPlace(mOPadLookText.transform, lfX + lfLookGlyph, lfY, lfLookWidth + 2f, lfGlyph);
    }

    private static void fPlace(Transform pOTransform, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        RectTransform lORect = (RectTransform)pOTransform;

        lORect.anchoredPosition = new Vector2(pfX, pfY);
        lORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }

    // ------------------------------------------------- The game menu

    /// <summary>Escape and F1: open the menu, or close it - from a sub page back to the main one.</summary>
    public void ToggleMenu()
    {
        if (mePage == PageEnum.Closed)
            OpenMenu(PageEnum.Main);
        else if (mePage == PageEnum.Main)
            CloseMenu();
        else
            mePage = PageEnum.Main;
    }

    /// <summary>Opens the menu on a page - refused as the original's options refuse (partway
    /// through an action, between worlds).</summary>
    public void OpenMenu(PageEnum pePage)
    {
        if (pePage == PageEnum.Closed)
        {
            CloseMenu();
            return;
        }

        if (mePage == PageEnum.Closed)
        {
            if (mOUi != null && mOUi.Options != null && mOUi.Options.RefusesToOpen())
                return;

            if (mOScheme != null)
                mOScheme.HoldUiModal(MenuModalHold);

            UWGameClock.Hold(MenuClockHold);
        }

        mePage = pePage;
    }

    public void CloseMenu()
    {
        mePage = PageEnum.Closed;
        fReleaseHolds();
    }

    private void fReleaseHolds()
    {
        if (mOScheme != null)
            mOScheme.ReleaseUiModal(MenuModalHold);

        UWGameClock.Release(MenuClockHold);
    }

    private static string fSavegameRoot()
    {
        return UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;
    }

    private string fMessage(int piIndex)
    {
        return mOUi != null && mOUi.mOUWData != null ? mOUi.mOUWData.GetGeneralMessage(piIndex) : string.Empty;
    }

    private void fSave()
    {
        string lsError = UWSavegameWriter.Save(miSaveSlot, msSaveName.Trim());

        CloseMenu();

        if (mOInteraction == null)
            return;

        mOInteraction.ResetMessages();
        mOInteraction.AddMessage(fMessage(lsError == null ? UWHudOptions.SaveGameSucceededMessage : UWHudOptions.SaveGameFailedMessage));

        if (lsError != null)
            mOInteraction.AddMessage(lsError);
    }

    private void fLoad(int piSlot)
    {
        string lsRoot = fSavegameRoot();

        if (!UWSavegameSlots.Exists(lsRoot, piSlot))
        {
            if (mOInteraction != null)
                mOInteraction.AddGeneralMessage(UWHudOptions.NoSaveGameThereMessage);

            return;
        }

        CloseMenu();
        UWSavegameSlots.Restore(lsRoot, piSlot);
    }

    private void fQuitToMainMenu()
    {
        CloseMenu();

        UWMainMenu lOMenu = UWScene.MainMenu;

        if (lOMenu != null)
        {
            lOMenu.Show();
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------- Drawing the menu (IMGUI, as the help window)

    private static readonly Color msBackground = new Color(0.08f, 0.08f, 0.09f, 0.96f);

    private static readonly Color msButton = new Color(0.14f, 0.14f, 0.16f, 1f);

    private static readonly Color msButtonHover = new Color(0.22f, 0.22f, 0.26f, 1f);

    private static readonly Color msText = new Color(0.86f, 0.86f, 0.84f, 1f);

    private static readonly Color msAccent = new Color(0.80f, 0.64f, 0.34f, 1f);

    /// <summary>The reference height the menu is laid out for; it scales with the screen.</summary>
    private const float MenuReferenceHeight = 720f;

    private GUIStyle mOTitleStyle;

    private GUIStyle mOButtonStyle;
    private GUIStyle mOCurrentPresetStyle;
    private GUIStyle mOSectionStyle;

    private GUIStyle mOLabelStyle;

    private GUIStyle mOFieldStyle;

    private Texture2D mOBackgroundTexture;

    private Texture2D mOButtonTexture;

    private Texture2D mOButtonHoverTexture;

    private Texture2D mOFieldTexture;

    private static Texture2D fTexture(Color pOColour)
    {
        Texture2D lOTexture = new Texture2D(1, 1);
        lOTexture.SetPixel(0, 0, pOColour);
        lOTexture.Apply();

        return lOTexture;
    }

    private void fEnsureStyles()
    {
        if (mOTitleStyle != null)
            return;

        mOBackgroundTexture = fTexture(msBackground);
        mOButtonTexture = fTexture(msButton);
        mOButtonHoverTexture = fTexture(msButtonHover);
        mOFieldTexture = fTexture(new Color(0.05f, 0.05f, 0.06f, 1f));

        Font lOFont = mOFont != null ? mOFont : UWInterfaceFont.Font;

        mOTitleStyle = new GUIStyle { font = lOFont, fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        mOTitleStyle.normal.textColor = msAccent;

        mOLabelStyle = new GUIStyle { font = lOFont, fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        mOLabelStyle.normal.textColor = msText;


        mOButtonStyle = new GUIStyle { font = lOFont, fontSize = 16, alignment = TextAnchor.MiddleCenter };
        mOButtonStyle.normal.background = mOButtonTexture;
        mOButtonStyle.normal.textColor = msText;
        mOButtonStyle.hover.background = mOButtonHoverTexture;
        mOButtonStyle.hover.textColor = msAccent;
        mOButtonStyle.active.background = mOButtonHoverTexture;
        mOButtonStyle.active.textColor = msAccent;

        mOCurrentPresetStyle = new GUIStyle(mOButtonStyle);
        mOCurrentPresetStyle.normal.textColor = msAccent;

        mOSectionStyle = new GUIStyle { font = lOFont, fontSize = 16, alignment = TextAnchor.MiddleLeft };
        mOSectionStyle.normal.textColor = msAccent;

        mOFieldStyle = new GUIStyle { font = lOFont, fontSize = 16, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 0, 0) };
        mOFieldStyle.normal.background = mOFieldTexture;
        mOFieldStyle.normal.textColor = msText;
        mOFieldStyle.focused.background = mOFieldTexture;
        mOFieldStyle.focused.textColor = msText;
    }

    private void OnGUI()
    {
        if (mePage == PageEnum.Closed || mePage == PageEnum.Layout)
            return;

        // The setup bar's menus and dialogs lie over this menu while it is open (per user,
        // 2026-10-06: the bar in the in-game menu, so nobody has to go to the main menu); while
        // one of them is open this menu neither draws nor takes clicks.
        if (UWSetupMenu.HasOpenPanel)
            return;

        fEnsureStyles();

        float lfScale = Screen.height / MenuReferenceHeight;
        Matrix4x4 lOMatrix = GUI.matrix;

        GUI.matrix = Matrix4x4.Scale(new Vector3(lfScale, lfScale, 1f));

        float lfWidth = Screen.width / lfScale;
        Rect lOPanel = new Rect((lfWidth - 380f) * 0.5f, 90f, 380f, 560f);

        GUI.DrawTexture(lOPanel, mOBackgroundTexture);
        GUILayout.BeginArea(new Rect(lOPanel.x + 24f, lOPanel.y + 18f, lOPanel.width - 48f, lOPanel.height - 36f));

        // The buttons' places for the gamepad (fRecordEntry), gathered in the repaint.
        mOAreaAt = new Vector2(lOPanel.x + 24f, lOPanel.y + 18f);
        mfMenuScale = lfScale;

        if (Event.current.type == EventType.Repaint)
            mOEntryRectsDrawing.Clear();

        switch (mePage)
        {
            case PageEnum.Main: fDrawMain(); break;
            case PageEnum.Save: fDrawSlots(true); break;
            case PageEnum.SaveName: fDrawSaveName(); break;
            case PageEnum.Load: fDrawSlots(false); break;
            case PageEnum.Detail: fDrawDetail(); break;
        }

        GUILayout.EndArea();
        GUI.matrix = lOMatrix;

        if (Event.current.type == EventType.Repaint)
        {
            mOEntryRects.Clear();
            mOEntryRects.AddRange(mOEntryRectsDrawing);
        }
    }

    /// <summary>The UI size in percent of the automatic one - the layout editor's - and +.</summary>
    public static void SetUiPercent(int piPercent)
    {
        UWUserSettings.ModernUiPercent = Mathf.Clamp(piPercent, MinUiPercent, MaxUiPercent);
        UWUserSettings.Save();
    }

    private bool fButton(string psText)
    {
        return fButton(new GUIContent(psText));
    }

    /// <summary>One preset under "Edit layout": indented and lower than the other buttons, the one
    /// in force in gold. A press chooses it; true for the press (Customize then opens the editor
    /// even when it is already in force).</summary>
    private bool fPresetButton(string psText, UWModernLayout.PresetEnum pePreset, UWModernLayout.PresetEnum peCurrent, bool pbBuilt)
    {
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.Space(28f);

        bool lbEnabled = GUI.enabled;

        GUI.enabled = lbEnabled && pbBuilt;

        bool lbPressed = GUILayout.Button(psText, pePreset == peCurrent ? mOCurrentPresetStyle : mOButtonStyle, GUILayout.Height(30f));

        fRecordEntry();
        GUI.enabled = lbEnabled;
        GUILayout.EndHorizontal();

        if (lbPressed && pePreset != peCurrent)
            UWModernLayout.ChoosePreset(pePreset);

        return lbPressed;
    }

    private bool fButton(GUIContent pOContent)
    {
        GUILayout.Space(6f);

        bool lbPressed = GUILayout.Button(pOContent, mOButtonStyle, GUILayout.Height(38f));

        fRecordEntry();

        return lbPressed;
    }

    // ------------------------------------------------- The gamepad in the menu

    /// <summary>The gamepad steps through the buttons (UWPadEntryStepper, per user 2026-10-08:
    /// as in the context menu).</summary>
    private readonly UWPadEntryStepper mOMenuStepper = new UWPadEntryStepper();

    /// <summary>The buttons' screen rectangles of the last drawn frame, and those being drawn.</summary>
    private readonly List<Rect> mOEntryRects = new List<Rect>();

    private readonly List<Rect> mOEntryRectsDrawing = new List<Rect>();

    /// <summary>The menu's area and scale while it draws, for the buttons' screen places.</summary>
    private Vector2 mOAreaAt;

    private float mfMenuScale = 1f;

    private PageEnum meSteppedPage = PageEnum.Closed;

    /// <summary>The button just laid out, as a screen rectangle (pixels, bottom-left origin) - in
    /// the repaint only, where GUILayout knows its place.</summary>
    private void fRecordEntry()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Rect lORect = GUILayoutUtility.GetLastRect();
        float lfX = (mOAreaAt.x + lORect.x) * mfMenuScale;
        float lfTop = (mOAreaAt.y + lORect.y) * mfMenuScale;

        mOEntryRectsDrawing.Add(new Rect(lfX, Screen.height - lfTop - (lORect.height * mfMenuScale),
            lORect.width * mfMenuScale, lORect.height * mfMenuScale));
    }

    /// <summary>Once a frame while the menu is up: a new page (or the menu opened) snaps the pointer
    /// onto its first button when the pad was used last.</summary>
    private void fStepMenuByPad()
    {
        if (!IsOpen || mePage == PageEnum.Layout || UWSetupMenu.HasOpenPanel)
        {
            meSteppedPage = PageEnum.Closed;
            return;
        }

        if (meSteppedPage != mePage)
        {
            meSteppedPage = mePage;
            mOMenuStepper.Opened();
            return;
        }

        mOMenuStepper.Update(mOEntryRects);
    }

    private void fDrawMain()
    {
        GUILayout.Label("Game menu", mOTitleStyle, GUILayout.Height(40f));

        if (fButton("Resume"))
            CloseMenu();

        if (fButton("Save game"))
            mePage = PageEnum.Save;

        if (fButton("Restore game"))
            mePage = PageEnum.Load;

        // Music, sound and the detail level left this menu on 2026-10-06 (per user): the setup
        // bar at the top offers them in full while the menu is open (UWSetupMenu).
        // THE MOUSE'S RIGHT BUTTON AS A GLYPH (per user, 2026-10-08: not the pad's - "Right button"
        // said nothing of which; Kenney's pixel prompts, UWGlyphs), "RMB" if it is missing.
        Texture2D lOMouseRight = UWGlyphs.MouseRight;
        string lsPointerOption = UWUserSettings.ModernPointerHold ? "hold to look around" : "toggles the pointer";

        if (fButton(lOMouseRight != null ? new GUIContent("  " + lsPointerOption, lOMouseRight) : new GUIContent("RMB: " + lsPointerOption)))
        {
            UWUserSettings.ModernPointerHold = !UWUserSettings.ModernPointerHold;
            UWUserSettings.Save();
        }

        // The mouse look's speed left this menu on 2026-10-08 (per user): the Controls dialog of
        // the setup bar has it as a slider, reachable while the menu is open.

        // THE INTERFACE PRESETS under "Edit layout" as a heading (per user, 2026-10-09): Classic,
        // Classic+, Modern and Customize, the last opening the layout editor (UWModernLayoutEditor,
        // per user 2026-10-04; the UI size lives in its strip) on the own layout, which the other
        // presets leave alone. Classic and Classic+ are planned - greyed until they are built.
        GUILayout.Space(10f);
        GUILayout.Label("Edit layout", mOSectionStyle, GUILayout.Height(24f));

        UWModernLayout.PresetEnum lePreset = UWModernLayout.Preset;

        fPresetButton("Classic Wide", UWModernLayout.PresetEnum.Classic, lePreset, true);
        fPresetButton("Classic+", UWModernLayout.PresetEnum.ClassicPlus, lePreset, false);
        fPresetButton("Modern", UWModernLayout.PresetEnum.Modern, lePreset, true);

        if (fPresetButton("Customize", UWModernLayout.PresetEnum.Custom, lePreset, true))
            mePage = PageEnum.Layout;

        // The minimap's north up / turning left this menu on 2026-10-09 (per user): the layout
        // editor's inspector has it on the minimap ("North").

        // Quit at the very bottom of the panel, set apart from the settings (per user,
        // 2026-10-06: "das Quit nach unten schieben") - since music, sound and detail left the
        // menu it had moved up with the rest.
        GUILayout.FlexibleSpace();

        if (fButton("Quit to the main menu"))
            fQuitToMainMenu();
    }

    private void fDrawSlots(bool pbSave)
    {
        GUILayout.Label(pbSave ? "Save game" : "Restore game", mOTitleStyle, GUILayout.Height(40f));

        string lsRoot = fSavegameRoot();

        for (int liSlot = 1; liSlot <= UWSavegameSlots.SlotCount; liSlot++)
        {
            string lsName = UWSavegameSlots.GetName(lsRoot, liSlot);

            if (!fButton(liSlot + ": " + lsName))
                continue;

            if (pbSave)
            {
                miSaveSlot = liSlot;
                msSaveName = UWSavegameSlots.Exists(lsRoot, liSlot) ? lsName : string.Empty;
                mbFocusName = true;
                mePage = PageEnum.SaveName;
            }
            else
            {
                fLoad(liSlot);
            }
        }

        GUILayout.Space(12f);

        if (fButton("Back"))
            mePage = PageEnum.Main;
    }

    private void fDrawSaveName()
    {
        GUILayout.Label("Save game " + miSaveSlot, mOTitleStyle, GUILayout.Height(40f));
        GUILayout.Label("Name of the save game:", mOLabelStyle);

        Event lOEvent = Event.current;
        bool lbEnter = lOEvent.type == EventType.KeyDown
            && (lOEvent.keyCode == KeyCode.Return || lOEvent.keyCode == KeyCode.KeypadEnter);
        bool lbEscape = lOEvent.type == EventType.KeyDown && lOEvent.keyCode == KeyCode.Escape;

        GUI.SetNextControlName("UWModernSaveName");
        msSaveName = GUILayout.TextField(msSaveName ?? string.Empty, 30, mOFieldStyle, GUILayout.Height(36f));

        if (mbFocusName)
        {
            GUI.FocusControl("UWModernSaveName");
            mbFocusName = false;
        }

        if (lbEnter)
        {
            lOEvent.Use();
            fSave();
            return;
        }

        if (lbEscape)
        {
            lOEvent.Use();
            mePage = PageEnum.Save;
            return;
        }

        if (fButton("Save"))
            fSave();

        if (fButton("Cancel"))
            mePage = PageEnum.Save;
    }

    private void fDrawDetail()
    {
        GUILayout.Label("Detail", mOTitleStyle, GUILayout.Height(40f));

        foreach (UWGraphicsDetail.LevelEnum leLevel in new[]
        {
            UWGraphicsDetail.LevelEnum.Low, UWGraphicsDetail.LevelEnum.Medium,
            UWGraphicsDetail.LevelEnum.High, UWGraphicsDetail.LevelEnum.VeryHigh
        })
        {
            string lsMark = leLevel == UWGraphicsDetail.CurrentLevel ? "> " : string.Empty;

            if (fButton(lsMark + leLevel))
                UWGraphicsDetail.SetLevel(leLevel);
        }

        GUILayout.Space(12f);

        if (fButton("Back"))
            mePage = PageEnum.Main;
    }
}
