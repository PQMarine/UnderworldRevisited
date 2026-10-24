using UnityEngine;

/// <summary>
/// THE MODERN SCHEME'S HUD AND GAME MENU (stage 1 of the modern controls, concept per user
/// 2026-10-03: "an own modern interface, more a modern MMORPG UI", backgrounds and frames out of
/// the original graphics where possible). The classic frame is off in the modern scheme
/// (UWGameUI.Update), and with it the weapon, the flasks, the power gem, the active spells and
/// the message scroll; their STATE kept running (UWCharacter, Interaction). This class draws
/// them again on a full-screen canvas of its own:
///
///   - the drawn weapon, from the same frames and Weapons.dat offsets as the classic view, the
///     original's 172 x 112 viewport mapped onto the screen (WeaponScale; UWModernWeaponView);
///   - bottom left the two flasks with their values and the power gem between them, standing
///     free on the original's stone shelf (UWModernHudArt.BuildShelf, look per user 2026-10-03;
///     UWModernVitals), and the stone shelf as a deco panel (UWModernStoneShelf);
///   - top left the active spells (SPELLS.GR through the palette shader, as on the classic shelf;
///     UWModernActiveSpells);
///   - top centre the heading, sixteen points as the compass needle has (UWModernHeadingStrip),
///     the original's compass (UWModernCompass) and the rune hollow (UWModernRuneHollow);
///   - above the flasks the last five messages, colour codes stripped, [MORE] while the game
///     waits for a page (UWModernMessageBox, the original's scroll UWModernScroll), the parts'
///     backs (UWModernPartBacks);
///   - the crosshair with the name of what it is on (Interaction.GetCrosshairTargetName;
///     UWModernCrosshair), the view's pictures (UWModernViewPicture);
///   - the original's stats and character pages (UWModernStatsPage, UWModernInventoryPage) and
///     the Classic Wide frame (UWModernClassicFrame).
///
/// The images keep the original pixels at an integer scale (PixelScale). The eyes already have
/// their own canvas (UWEyesDisplay).
///
/// THE GAME MENU (Escape, F1, the Ctrl shortcuts) is UWModernGameMenu since cleanup 5.16
/// (2026-10-22): the HUD owns it and drives it - its pad stepping first in Update, its pages in
/// OnGUI - as the HUD drew it before.
///
/// SPLIT IN CLEANUP 5.16 (2026-10-22; 2,440 lines until then): each piece is a part
/// (IUWModernPart, UWModernPart) this component builds onto its canvas in the order the images lie
/// in (fBuild) and lays out every frame in a fixed order (LateUpdate); the clicks on them are
/// UWModernHudInput's. What the pieces do, where they stand and how they look did not change.
/// </summary>
public class UWModernHud : MonoBehaviour
{
    /// <summary>The one HUD of the scene, for UWGameUI's key routing.</summary>
    public static UWModernHud Instance { get; private set; }

    /// <summary>The game menu (UWModernGameMenu), made with the HUD.</summary>
    public UWModernGameMenu Menu { get; private set; }

    /// <summary>The game menu is open (UWModernGameMenu.IsOpen) - for UWModernUi.IsMenuOpen and the
    /// HUD's own parts; the bags ask UWModernUi since cleanup 5.17 (2026-10-23).</summary>
    public bool IsOpen => Menu != null && Menu.IsOpen;

    /// <summary>The scene's UI, character, interaction and control scheme, found in Start - for the
    /// game menu and the HUD's parts.</summary>
    internal UWGameUI Ui => mOUi;

    internal UWCharacter Character => mOCharacter;

    internal Interaction Interaction => mOInteraction;

    internal UWControlScheme Scheme => mOScheme;

    /// <summary>Whether the HUD is on screen: the modern scheme, no conversation, no screen menu
    /// or cutscene over it. The bags (UWModernBags) show with it.</summary>
    public bool IsShowing => UWModernUi.IsActive && mOUi != null && mOUi.mOUWData != null && mOCharacter != null
        && mOInteraction != null && !fIsCovered() && !UWConversationController.IsAnyOpen;

    /// <summary>A conversation in the modern scheme (UWModernConversation): the HUD hides, the bags
    /// and the character panel stay for the trade.</summary>
    public bool IsTalking => UWModernUi.IsActive && mOUi != null && mOUi.mOUWData != null && mOCharacter != null
        && mOInteraction != null && !fIsCovered() && UWConversationController.IsAnyOpen;

    /// <summary>How large the weapon is drawn against the screen: 1 maps the original's viewport
    /// height (112 rows) onto the screen height.</summary>
    [SerializeField]
    private float mfWeaponScale = 0.8f;

    /// <summary>Where the message box starts above the flasks (screen pixels from the bottom) -
    /// the rune panel (UWModernRunePanel) reaches down to here at most (UWModernMessageBox).</summary>
    public float LeftColumnBottom => mOMessages != null ? mOMessages.LeftColumnBottom : 0f;

    private UWGameUI mOUi;

    private UWCharacter mOCharacter;

    private Interaction mOInteraction;

    private UWControlScheme mOScheme;

    private Canvas mOCanvas;

    private float miPixelScale = -1f;

    // ------------------------------------------------- The parts (made in fBuild)

    private UWModernWeaponView mOWeapon;

    private UWModernStoneShelf mOStoneShelf;

    private UWModernPartBacks mOBacks;

    private UWModernMessageBox mOMessages;

    private UWModernVitals mOVitals;

    private UWModernRuneHollow mORuneHollow;

    private UWModernActiveSpells mOSpells;

    private UWModernHeadingStrip mOHeading;

    private UWModernCompass mOCompass;

    /// <summary>The original's stats panel (UWModernLayout.IsStatsPanelShown).</summary>
    private UWModernStatsPage mOStats;

    /// <summary>The original's character panel with its inventory (UWModernLayout.IsCharacterPageShown).</summary>
    private UWModernInventoryPage mOInventoryPage;

    private UWModernScroll mOScroll;

    /// <summary>The Classic Wide frame (UWModernClassicFrame), on a canvas of its own under this one.</summary>
    private UWModernClassicFrame mOClassicFrame;

    private UWModernCrosshair mOCrosshair;

    private UWModernViewPicture mOPicture;

    /// <summary>What a click on the parts does (UWModernHudInput).</summary>
    private UWModernHudInput mOInput;

    private void Awake()
    {
        Instance = this;
        Menu = new UWModernGameMenu(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        Menu.ReleaseHolds();

        if (mOVitals != null)
            mOVitals.Release();

        mOScroll?.Release();
    }

    private void Start()
    {
        mOUi = GetComponent<UWGameUI>();
        mOCharacter = GetComponent<UWCharacter>();
        mOInteraction = GetComponent<Interaction>();
        mOScheme = GetComponentInParent<UWControlScheme>();
    }

    // ------------------------------------------------- The HUD

    private bool fIsCovered()
    {
        UWIntroPlayer lOIntro = UWScene.IntroPlayer;

        // The big map covers the screen as in the original (UWGameUI shows it on the classic frame).
        return UWScreenUi.IsScreenMenuOpen || (lOIntro != null && lOIntro.IsPlaying) || (mOUi != null && mOUi.Map.IsMapVisible);
    }

    /// <summary>The menu's pad stepping, the right button's pointer switch, then the parts' clicks
    /// (UWModernHudInput) - in this order, as before the split.</summary>
    private void Update()
    {
        Menu.StepByPad();

        // The right button switches the pointer (UWModernPointer) - not under the menu.
        UWModernPointer.Tick(IsShowing && !IsOpen && !UWControls.IsTextEntryActive);

        // Made with the parts (fBuild); until then no part takes a click.
        if (mOInput != null)
            mOInput.Update();
    }

    /// <summary>The game menu's pages (UWModernGameMenu.OnGUI), drawn by this component as before.</summary>
    private void OnGUI()
    {
        Menu.OnGUI();
    }

    /// <summary>
    /// THE PARTS' ORDER, fixed here (cleanup 5.16, 2026-10-22; until then a list of calls in one
    /// method of the same order): what a part reads of another is that part's rect of THIS frame
    /// where it comes earlier, of the last frame where it comes later -
    ///   - the heading strip reads the compass's rect of the last frame (the compass comes after);
    ///   - the compass, then the heading's text gives way to it (UWModernHeadingStrip.AfterCompass);
    ///   - the rune hollow stands beside this frame's compass, or under this frame's heading;
    ///   - the messages keep beside the rune panel's rect (UWModernRunePanel, its own component);
    ///   - the backs lie on the rects every part reported before them this frame;
    ///   - the crosshair last, over everything but the view's pictures' own canvas.
    /// </summary>
    private void LateUpdate()
    {
        bool lbModern = UWModernUi.IsActive && mOUi != null && mOUi.mOUWData != null && mOCharacter != null
            && mOInteraction != null;

        // Leaving the modern scheme (Shift+F2) or a screen menu taking over closes the menu.
        if (IsOpen && (!lbModern || fIsCovered()))
            Menu.CloseMenu();

        bool lbShow = lbModern && !fIsCovered() && !UWConversationController.IsAnyOpen;

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

        mOWeapon.WeaponScale = mfWeaponScale;
        mOWeapon.Update(miPixelScale);
        mOVitals.Update(miPixelScale);
        mOSpells.Update(miPixelScale);
        mOHeading.Update(miPixelScale);
        fUpdateCompass();
        mORuneHollow.Update(miPixelScale);

        if (mOStats != null)
            mOStats.Update(miPixelScale);

        if (mOInventoryPage != null)
            mOInventoryPage.Update(miPixelScale);

        mOMessages.Update(miPixelScale);
        mOStoneShelf.Update(miPixelScale);
        mOBacks.Update(miPixelScale);
        mOPicture.Update(miPixelScale);
        mOCrosshair.Update(miPixelScale);
    }

    /// <summary>
    /// The canvas and the parts on it, in the order their images lie (each part's Build appends
    /// its own): the weapon, the stone shelf under everything else so things can stand on it, the
    /// backs before the art they lie under, the messages, the vitals' shelf, the rune hollow, the
    /// flasks and the gem, the active spells, the heading strip, the compass, the stats page, the
    /// scroll, the crosshair - the order of the single build method before the split. The
    /// character page and the Classic Wide frame have canvases of their own under the HUD.
    /// </summary>
    private void fBuild()
    {
        mOCanvas = UWUiKit.CreateOverlayCanvas(transform, "Modern HUD", UWUiSorting.ModernHud);

        Transform lORoot = mOCanvas.transform;

        // The original's compass as an element of its own (UWModernCompass), and its message
        // scroll in the messages' place (UWModernScroll).
        mOCompass = new UWModernCompass(mOUi);
        mOScroll = new UWModernScroll(mOUi);
        mOWeapon = new UWModernWeaponView(this);
        mOStoneShelf = new UWModernStoneShelf(this);
        mOMessages = new UWModernMessageBox(this, mOScroll);
        mOBacks = new UWModernPartBacks(this, mOMessages);
        mOVitals = new UWModernVitals(this);
        mOHeading = new UWModernHeadingStrip(this, mOCompass);
        mORuneHollow = new UWModernRuneHollow(this, mOCompass, mOHeading);
        mOSpells = new UWModernActiveSpells(this);
        mOCrosshair = new UWModernCrosshair(this);
        mOPicture = new UWModernViewPicture(this);

        mOWeapon.Build(lORoot);
        mOStoneShelf.Build(lORoot);
        mOBacks.Build(lORoot);
        mOMessages.Build(lORoot);
        mOVitals.Build(lORoot);
        mORuneHollow.Build(lORoot);
        mOVitals.BuildFlasks(lORoot);
        mOSpells.Build(lORoot);
        mOHeading.Build(lORoot);

        mOCompass.Build(lORoot);
        mOStats = new UWModernStatsPage(mOUi);
        mOStats.Build(lORoot);
        mOInventoryPage = new UWModernInventoryPage(mOUi);
        mOInventoryPage.Build(transform);
        mOScroll.Build(lORoot);

        GameObject lOFrame = new GameObject("Classic wide");
        lOFrame.transform.SetParent(transform, false);
        mOClassicFrame = lOFrame.AddComponent<UWModernClassicFrame>();
        mOClassicFrame.Init(mOUi);

        mOCrosshair.Build(lORoot);
        mOPicture.Build(lORoot);

        mOInput = new UWModernHudInput(this, mOSpells, mOHeading, mOCompass, mORuneHollow, mOStats);
    }

    /// <summary>The integer scale of the original images and the sizes that follow from it -
    /// set again only when the screen height changes (each part's OnPixelScale).</summary>
    private void fApplyPixelScale()
    {
        float liScale = UWModernUi.PixelScale;

        if (liScale == miPixelScale)
            return;

        miPixelScale = liScale;

        mOMessages.OnPixelScale(liScale);
        mOVitals.OnPixelScale(liScale);
        mOHeading.OnPixelScale(liScale);
        mOCrosshair.OnPixelScale(liScale);
        mOSpells.OnPixelScale(liScale);
    }

    /// <summary>The original's compass, and the heading's text giving way to it while it shows.</summary>
    private void fUpdateCompass()
    {
        if (mOCompass == null)
            return;

        mOCompass.Update(miPixelScale);
        mOHeading.AfterCompass();
    }

    /// <summary>Whether a screen point lies on an active spell (UWModernPointer: the right button
    /// ends it there instead of switching the pointer) - or on the heading, the compass, the rune
    /// hollow, the original's stats or character page, which count as interface the same way
    /// (UWModernUi.IsOverUi). IsOverSpellIcons until 2026-10-22, the same list.</summary>
    public bool IsOverParts(Vector2 pOPointer)
    {
        return (mOSpells != null && mOSpells.SpellAt(pOPointer) >= 0) || (mOHeading != null && mOHeading.ScreenRect.Contains(pOPointer))
            || (mOCompass != null && mOCompass.ScreenRect.Contains(pOPointer))
            || (mORuneHollow != null && mORuneHollow.ScreenRect.Contains(pOPointer))
            || (mOStats != null && mOStats.ScreenRect.Contains(pOPointer))
            || (mOInventoryPage != null && mOInventoryPage.Contains(pOPointer));
    }
}
