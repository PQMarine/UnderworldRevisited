using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S RUNE PANEL (stage 3; idea per user, look per mockup, 2026-10-04): at the
/// left edge, the mirror of the character panel (UWModernPanel) - a handle at its right edge, it
/// peeks out near the pointer and opens on a click; Y (the key left of X, Z on an English
/// keyboard) opens and closes it; pinned by the pin on its tab row it stays out. Escape closes it
/// after the bags and the character panel. Nothing here frees or locks the pointer.
///
///   - RUNES: the original's rune shelf (PANELS.GR picture 1) with the runes of the bag; a click
///     lays a rune into the hollow below (the fourth pushes out the first, UWRuneShelfModel.Add),
///     the right button looks at it, the strip at the bottom clears the hollow. The hollow IS the
///     game's rune shelf - the classic scheme sees the same runes, the save game keeps them. A
///     click on it casts; dragged by it, the spell goes onto the action bar (UWModernActionBar).
///   - SPELLS: every rune spell circle by circle, grey what cannot be cast now (a rune missing,
///     above the character's circle); a click lays the runes into the hollow (as the help's list),
///     dragged it goes onto the action bar too.
///   - The Runes tab is only as tall as it needs, the Spells tab takes the rows down to the
///     message box; the message box moves right of the panel while it covers it (UWModernHud).
/// </summary>
public class UWModernRunePanel : UWModernDockedPanel
{
    public enum TabEnum
    {
        Runes,
        Spells
    }

    public static UWModernRunePanel Instance { get; private set; }

    public static bool Pinned => UWUserSettings.ModernRunesPinned;

    /// <summary>Whether Escape has something to close here: open and not pinned.</summary>
    /// <summary>Whether Escape has it to close: not pinned, and not the original's tablet alone,
    /// which is always out (else Escape closed it, it opened again the next frame and the menu
    /// never came - per user, 2026-10-10).</summary>
    public override bool IsClosable => mbOpen && !Pinned && !UWModernLayout.IsRuneTabletShown;

    /// <summary>At the left edge, its handle at its right - the character panel's mirror
    /// (UWModernDockedPanel).</summary>
    protected override bool DockedLeft => true;

    protected override UWModernLayout.ElementEnum Element => UWModernLayout.ElementEnum.RunePanel;

    protected override bool IsPinned => Pinned;

    /// <summary>The panel with its handle on the screen (pixels, bottom-left origin), empty while
    /// nothing of it shows.</summary>
    public Rect ScreenRect { get; private set; }

    /// <summary>The layout in original pixels (the docked panel's measures are UWModernDockedPanel's).</summary>
    private const int PanelWidth = 96;

    private const int TitleTop = 4;

    /// <summary>The rune shelf picture, centred in the frame; its grid, the compartments' hit test
    /// and the clear strip are UWRuneShelfModel's (the classic shelf's).</summary>
    private const int TabletLeft = 7;

    private const int TabletTop = 15;

    /// <summary>The hollow with the chosen runes below the shelf.</summary>
    private const int HollowGap = 4;

    private const int HollowWidth = 52;

    private const int HollowHeight = 20;

    private const int HollowRunePitch = 16;

    /// <summary>Below the hollow: the spell's name, its circle and a hint.</summary>
    private const int InfoRows = 3 + 9 + 8 + 8;

    private const int BottomPad = 5;

    /// <summary>The spell list.</summary>
    private const int ListTop = 17;

    private const int HeaderRows = 11;

    private const int RowRows = 12;

    private const float ClickMoveThreshold = 6f;

    private static readonly Color msHollowFill = new Color(0.14f, 0.08f, 0.03f, 0.8f);

    private Text mOTitle;

    private Text mOTitleRight;

    // The Runes tab.
    private RectTransform mORunesContent;

    private RawImage mOTablet;

    private RawImage[] mORunes;

    private Image mOHollow;

    private Image[] mOHollowFrame;

    private RawImage[] mOHollowRunes;

    private Text mOSpellName;

    private Text mOSpellCircle;

    private Text mOHint;

    // The Spells tab.
    private RectTransform mOListViewport;

    private RectTransform mOListContent;

    private Image mORowHighlight;

    private readonly List<ListEntry> mOEntries = new List<ListEntry>();

    // The spell being dragged onto the action bar.
    private Canvas mOGhostCanvas;

    private RawImage mOGhostBack;

    private RawImage mOGhostIcon;

    private Text mOGhostText;

    /// <summary>The handle, built for the backs' and colours' version with the tab, pin and ghost
    /// (fEnsureArt) - its stamp says whether they are current. The leather, the tab's and the
    /// pin's pictures are UWModernDockedPanel's.</summary>
    private readonly UWVersionedTexture mOHandleArt = new UWVersionedTexture();

    private Texture2D mOTabletTexture;

    private Texture2D mOGhostTexture;

    private Texture2D[] mORuneTextures;

    private TabEnum meTab = TabEnum.Runes;

    /// <summary>The spell list scrolled down this many original rows.</summary>
    private float mfScroll;

    private float mfListRows;

    // Screen rectangles of the last layout, for the clicks (the panel's, the handle's, the pin's
    // and the tabs' are UWModernDockedPanel's).
    private Rect mOTabletRect;

    private Rect mOHollowRect;

    private Rect mOListRect;

    /// <summary>A press on the hollow or a list row: a click acts, a drag takes the spell to the bar.</summary>
    private int miPressSequence = -1;

    private bool mbPressHollow;

    private Vector2 mOPressStart;

    private bool mbDragging;

    private sealed class ListEntry
    {
        /// <summary>A circle's heading (Sequence -1) or a spell.</summary>
        public int Circle;

        public int Sequence = -1;

        public UWRunicMagic.Spell Spell;

        public Text Name;

        public Text Runes;

        public RawImage Icon;

        public Image Rule;

        /// <summary>Its top in original rows from the list's top, and its height.</summary>
        public int Top;

        public int Rows;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        fDestroyTextures();
    }

    private void Start()
    {
        mOUi = GetComponent<UWGameUI>();
        mOScheme = GetComponentInParent<UWControlScheme>();
    }

    private void fDestroyTextures()
    {
        // The tablet and the runes are shared (UWGrTextures, UWIconCache) and not ours to destroy.
        fDestroyChromeArt();
        mOHandleArt.Release();

        if (mOGhostTexture != null)
            Destroy(mOGhostTexture);

        mOTabletTexture = null;
        mOGhostTexture = null;
        mORuneTextures = null;
    }

    protected override bool fIsShown()
    {
        // In Classic Wide only as the frame's tablet page (UWModernClassicFrame), never as a panel.
        return UWModernUi.IsActive && UWModernUi.IsShowing
            && mOUi != null && mOUi.mOUWData != null && mOUi.mCharacter != null
            && !(UWModernLayout.IsClassic && !UWModernLayout.IsRuneTabletShown);
    }

    private DataImport fData()
    {
        return mOUi != null ? mOUi.mOUWData : null;
    }


    // ------------------------------------------------- The spells, for the panel and the bar

    /// <summary>The bag of the game's character, null without the game's data.</summary>
    private static UWPlayerData fBag()
    {
        UWGameUI lOUi = UWScene.GameUi;

        return lOUi != null && lOUi.mOUWData != null ? lOUi.mOUWData.InitialPlayer : null;
    }

    /// <summary>Whether every rune of the sequence is in the bag (UWRuneShelfModel.HasRunes).</summary>
    public static bool HasRunes(int piSequence)
    {
        return UWRuneShelfModel.HasRunes(piSequence, fBag());
    }

    /// <summary>The highest circle the character can cast: its level halved, rounded up
    /// (UWSpellCasting.CircleOf).</summary>
    public static int CharacterCircle
    {
        get
        {
            UWGameUI lOUi = UWScene.GameUi;

            return lOUi != null && lOUi.mCharacter != null ? UWSpellCasting.CircleOf(lOUi.mCharacter.Vitals.Level) : 0;
        }
    }

    /// <summary>
    /// Whether the spell could be cast now, and if not why: UWSpellCasting.CanCast with the
    /// character's bag, level and whole mana (cleanup 5.10, 2026-10-20; until then the panel
    /// wrote the rules out itself). The skill check still decides. Without the game's data a rune
    /// counts as missing, without a character the circle is none. The bar greys a slot short of
    /// Ok; the panel shows Ok and NotEnoughMana as castable (fIsCastableOnPanel).
    /// </summary>
    public static UWSpellCasting.CastCheckEnum CastCheck(int piSequence)
    {
        UWGameUI lOUi = UWScene.GameUi;
        UWPlayerData lOBag = fBag();

        if (lOBag == null)
            return UWSpellCasting.CastCheckEnum.RuneMissing;

        UWCharacter lOCharacter = lOUi.mCharacter;

        return UWSpellCasting.CanCast(piSequence, lOCharacter != null ? lOCharacter.Vitals.Level : 0,
            lOCharacter != null ? Mathf.FloorToInt(lOCharacter.Vitals.CurrentMana) : 0, lOBag);
    }

    /// <summary>The panel's grey: a spell whose runes are in the bag and whose circle the
    /// character reaches - the mana left out (CastCheck).</summary>
    private static bool fIsCastableOnPanel(int piSequence)
    {
        UWSpellCasting.CastCheckEnum leCheck = CastCheck(piSequence);

        return leCheck == UWSpellCasting.CastCheckEnum.Ok || leCheck == UWSpellCasting.CastCheckEnum.NotEnoughMana;
    }

    public static string SpellName(int piSequence)
    {
        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi == null || lOUi.mOUWData == null || !UWRunicMagic.TryGetSpell(piSequence, out UWRunicMagic.Spell lOSpell))
            return string.Empty;

        return UWRunicMagic.GetName(lOSpell, lOUi.mOUWData.Strings);
    }

    /// <summary>The spell's own picture in SPELLS.GR (only the lasting effects have one), a
    /// projectile's missile, else null.</summary>
    public static UWTexture SpellIcon(int piSequence)
    {
        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi == null || lOUi.mOUWData == null || !UWRunicMagic.TryGetSpell(piSequence, out UWRunicMagic.Spell lOSpell))
            return null;

        // The table's minor class carries the duration in its upper bits; the picture goes by the
        // effect alone (Light: 0x0E + 3 = 17, the torch). SPELLS.GR has only 21 pictures.
        // A projectile shows its missile (per user, 2026-10-04): the fireball, the lightning bolt,
        // the magic missile from OBJECTS.GR.
        if (lOSpell.MajorClass == UWSpellCasting.ProjectileMajorClass)
        {
            int liMissile = UWRunicMagic.GetProjectileId(UWRunicMagic.GetEffectMinor(lOSpell.MinorClass));

            return liMissile >= 0 ? lOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, liMissile) : null;
        }

        int liIcon = UWRunicMagic.GetIconIndex(lOSpell.MajorClass, fEffectMinor(lOSpell));
        List<UWTexture> lOIcons = lOUi.mOUWData.Textures.GetTexturesByType(UWTexture.TextureTypes.SPELLS);

        return liIcon < UWRunicMagic.NoIcon && lOIcons != null && liIcon >= 0 && liIcon < lOIcons.Count ? lOIcons[liIcon] : null;
    }

    /// <summary>
    /// The minor class the spell's lasting effect shows its picture by. Class 11's minor class in
    /// the table is the spell's own number (UWMiscSpellRules), not an effect: only four of them
    /// last and have a picture (Speed, Roaming Sight, Telekinesis, Freeze Time, by the effect
    /// they add); the rest have none (per user, 2026-10-04: Remove Trap, Open, Cure Poison ...
    /// showed wrong ones).
    /// </summary>
    private static int fEffectMinor(UWRunicMagic.Spell pOSpell)
    {
        int liMinor = UWRunicMagic.GetEffectMinor(pOSpell.MinorClass);

        if (pOSpell.MajorClass != UWMiscSpellRules.MajorClass)
            return liMinor;

        switch (liMinor)
        {
            case UWMiscSpellRules.SpeedSpell: return UWMiscSpellRules.SpeedEffectMinor;
            case UWMiscSpellRules.RoamingSightSpell: return UWMiscSpellRules.RoamingSightEffectMinor;
            case UWMiscSpellRules.TelekinesisSpell: return UWMiscSpellRules.TelekinesisEffectMinor;
            case UWMiscSpellRules.FreezeTimeSpell: return UWMiscSpellRules.FreezeTimeEffectMinor;
            default: return UWRunicMagic.NoIcon;
        }
    }

    /// <summary>The runes as a short label, one per line ("In|Lor" as "In\nLor").</summary>
    public static string RuneLabel(int piSequence)
    {
        return UWRunicMagic.DescribeRunes(piSequence).Replace(' ', '\n');
    }

    // ------------------------------------------------- Opening and closing

    public void Open(TabEnum peTab)
    {
        // Classic Wide: the frame's panel turns to the tablet (per user, 2026-10-10: the rune bag
        // did not open).
        if (UWModernLayout.IsClassic)
        {
            UWModernClassicFrame.TurnTo(UWModernClassicFrame.PageEnum.Runes);
            return;
        }

        mbOpen = true;
        meTab = peTab;
    }

    public override void Close()
    {
        if (Pinned)
            return;

        mbOpen = false;
        mbDragging = false;
        miPressSequence = -1;
    }

    protected override void fSetPinnedSetting(bool pbPinned)
    {
        UWUserSettings.ModernRunesPinned = pbPinned;
        UWUserSettings.Save();
    }

    /// <summary>The handle or the closed panel's edge opens it on the tab it last showed.</summary>
    protected override void fOpenByClick()
    {
        Open(meTab);
    }

    // ------------------------------------------------- Input

    /// <summary>How long the gamepad's left trigger is held to open the panel instead of casting.</summary>
    private const float PadCastHoldSeconds = 0.35f;

    /// <summary>The left trigger's tap and hold (UWHoldTap since cleanup step 7.3, 2026-10-25; the
    /// seconds held, added up frame by frame, here until then): the hold due PadCastHoldSeconds after
    /// the press by the unscaled clock ends the press; the trigger up before that taps.</summary>
    private readonly UWHoldTap mOPadCast = new UWHoldTap(PadCastHoldSeconds, UWHoldTap.PadButton);

    /// <summary>
    /// THE GAMEPAD'S LEFT TRIGGER (UWGamepad): TAPPED it casts what lies in the hollow, as F8 does in
    /// the original (UWGameActions.CastShelf); HELD it opens or closes the panel, which is how runes
    /// get into the hollow.
    /// </summary>
    private void fUpdatePadCast(UWControls pOControls)
    {
        UnityEngine.InputSystem.InputAction lOTrigger = pOControls.Player.PadCast;

        // While the gamepad's pointer drives, LT is its right button (the PadPointer context,
        // UWInput.PadPointerTakes).
        if (lOTrigger.WasPressedThisFrame())
        {
            if (UWInput.PadPointerTakes(lOTrigger))
                mOPadCast.Cancel();
            else
                mOPadCast.Press(Time.unscaledTime);

            return;
        }

        switch (mOPadCast.Update(lOTrigger.IsPressed(), lOTrigger.WasReleasedThisFrame(), Time.unscaledTime))
        {
            case UWHoldTap.ResultEnum.Hold:
                if (Pinned)
                    return;

                if (mbOpen)
                    Close();
                else
                    Open(meTab);

                break;
            case UWHoldTap.ResultEnum.Tap:
                if (mOUi.Runes != null)
                    mOUi.Actions.CastShelf();

                break;
        }
    }

    private void Update()
    {
        if (!fIsShown())
        {
            if (mbOpen && !UWModernUi.IsActive)
                mbOpen = false;

            mbPeek = false;
            mbDragging = false;
            miPressSequence = -1;
            return;
        }

        // The original's tablet alone (UWModernLayout.IsRuneTabletShown) is always out on its runes.
        bool lbTablet = UWModernLayout.IsRuneTabletShown;

        if ((Pinned || lbTablet) && !mbOpen)
            mbOpen = true;

        if (lbTablet)
            meTab = TabEnum.Runes;

        if (UWModernUi.IsMenuOpen || UWControls.IsTextEntryActive || UWHelpWindow.BlocksGameKeys || mOUi.Map.IsMapVisible)
        {
            mbDragging = false;
            miPressSequence = -1;
            return;
        }

        UWControls lOControls = mOScheme.Controls;

        if (lOControls == null)
            return;

        if (lOControls.Player.ModernRunes.WasPressedThisFrame() && !Pinned && !lbTablet && !UWModernLayout.IsClassic)
        {
            if (mbOpen)
                Close();
            else
                Open(meTab);
        }

        fUpdatePadCast(lOControls);

        mbPeek = false;

        if (!UWPointer.HasMouse || !UWModernPointer.IsFree)
        {
            mbDragging = false;
            miPressSequence = -1;
            return;
        }

        Vector2 lOPointer = UWPointer.Position;

        fUpdatePeek(lOPointer);

        fUpdateDrag(lOControls, lOPointer);

        float lfWheel = UWPointer.Scroll.y;

        if (mbOpen && meTab == TabEnum.Spells && Mathf.Abs(lfWheel) > 0.01f && mOListRect.Contains(lOPointer))
            mfScroll -= Mathf.Sign(lfWheel) * RowRows * 2;

        bool lbLeft = lOControls.Player.CursorDrag.WasPressedThisFrame();
        bool lbRight = UWInput.PressedUnheld(lOControls, UWAct.Interact);

        if (!lbLeft && !lbRight)
            return;

        // THE TABLET TAKES NO CLICKS WHILE THE CLASSIC WIDE PANEL TURNS (per user, 2026-10-20),
        // as the classic shelf (UWHudRunes asks UWPanelTurn.IsAtRestOn): it stands squeezed then.
        if (lbTablet && UWModernClassicFrame.IsTurning)
            return;

        // A thing on the pointer is not put down through the panel (UWModernBags asks Contains).
        if (mOUi.mOInventory != null && mOUi.mOInventory.CursorItem != null)
            return;

        // The pin, the handle, the closed panel's edge (UWModernDockedPanel).
        if (lbLeft && fClickChrome(lOPointer))
            return;

        if (!mbOpen)
            return;

        int liTab = lbLeft ? fTabAt(lOPointer) : -1;

        if (liTab >= 0)
        {
            meTab = (TabEnum)liTab;
            return;
        }

        if (meTab == TabEnum.Runes)
            fClickRunes(lOPointer, lbLeft);
        else if (lbLeft)
            fClickSpells(lOPointer);
    }

    private void fClickRunes(Vector2 pOPointer, bool pbLeft)
    {
        UWHudRunes lORunes = mOUi.Runes;

        // Another input mode holds the runes (mous_in_rune, UWHudRunes.InAnotherInputMode): no
        // clearing, no rune, no look, no cast (per user, 2026-10-13: the strip still cleared).
        if (lORunes == null || lORunes.InAnotherInputMode)
            return;

        // The clear strip at the bottom of the shelf (UWRuneShelfModel.IsClearStrip).
        if (pbLeft && mOTabletRect.Contains(pOPointer)
            && UWRuneShelfModel.IsClearStrip((mOTabletRect.yMax - pOPointer.y) / miScale))
        {
            lORunes.ClearRuneShelf();
            return;
        }

        int liRune = fRuneAt(pOPointer);

        if (liRune >= 0)
        {
            if (pbLeft)
                lORunes.AddToShelf(liRune);
            else
                lORunes.LookAtRune(liRune);

            return;
        }

        if (pbLeft && mOHollowRect.Contains(pOPointer))
        {
            miPressSequence = lORunes.Shelf.Sequence;
            mbPressHollow = true;
            mOPressStart = pOPointer;
        }
    }

    private void fClickSpells(Vector2 pOPointer)
    {
        ListEntry lOEntry = fEntryAt(pOPointer);

        if (lOEntry == null)
            return;

        miPressSequence = lOEntry.Sequence;
        mbPressHollow = false;
        mOPressStart = pOPointer;
    }

    /// <summary>A press on the hollow or a row: moved far enough it becomes a drag to the action
    /// bar; released in place it casts (the hollow) or lays the runes into the hollow (a row).</summary>
    private void fUpdateDrag(UWControls pOControls, Vector2 pOPointer)
    {
        if (miPressSequence < 0)
            return;

        if (!mbDragging && pOControls.Player.CursorDrag.IsPressed()
            && Vector2.Distance(pOPointer, mOPressStart) >= ClickMoveThreshold
            && UWRunicMagic.TryGetSpell(miPressSequence, out UWRunicMagic.Spell _))
            mbDragging = true;

        if (!pOControls.Player.CursorDrag.WasReleasedThisFrame())
            return;

        int liSequence = miPressSequence;
        bool lbHollow = mbPressHollow;
        bool lbDragged = mbDragging;

        miPressSequence = -1;
        mbDragging = false;

        if (lbDragged)
        {
            UWModernActionBar lOBar = UWModernActionBar.Instance;

            if (lOBar != null && lOBar.TryGetSlotAt(pOPointer, out int liSlot))
                lOBar.TryBindSpell(liSlot, liSequence);

            return;
        }

        UWHudRunes lORunes = mOUi.Runes;

        if (lORunes == null)
            return;

        if (lbHollow)
        {
            if (mOHollowRect.Contains(pOPointer))
                mOUi.Actions.CastShelf();
        }
        else if (HasRunes(liSequence))
        {
            lORunes.PutOnShelf(UWRuneShelfModel.RunesOf(liSequence));
        }
    }

    /// <summary>The rune of the bag under the pointer on the tablet, at the original's pixel
    /// proportion: the tablet is the page, so the original's gapless cells lie around its
    /// compartments as on the classic shelf (UWRuneShelfModel.RuneCellAt, decision 27,
    /// 2026-10-20; until then the 14 x 14 compartments alone, with gaps between).</summary>
    private int fRuneAt(Vector2 pOPointer)
    {
        UWPlayerData lOPlayer = fData().InitialPlayer;

        if (lOPlayer == null || !mOTabletRect.Contains(pOPointer))
            return -1;

        float lfX = (pOPointer.x - mOTabletRect.xMin) / (miScale * UWModernHudArt.PixelAspectX);
        float lfY = (mOTabletRect.yMax - pOPointer.y) / miScale;

        return UWRuneShelfModel.RuneCellAt(lfX, lfY,liRune => lOPlayer.HasRune((UWPlayerData.Rune)liRune));
    }

    private ListEntry fEntryAt(Vector2 pOPointer)
    {
        if (!mOListRect.Contains(pOPointer))
            return null;

        float lfRow = ((mOListRect.yMax - pOPointer.y) / miScale) + mfScroll;

        foreach (ListEntry lOEntry in mOEntries)
        {
            if (lOEntry.Sequence >= 0 && lfRow >= lOEntry.Top && lfRow < lOEntry.Top + lOEntry.Rows)
                return lOEntry;
        }

        return null;
    }

    // ------------------------------------------------- Drawing

    private void LateUpdate()
    {
        if (!fIsShown())
        {
            if (mOCanvas != null)
            {
                mOCanvas.enabled = false;
                mOGhostCanvas.enabled = false;
            }

            ScreenRect = Rect.zero;
            mOPanelRect = Rect.zero;
            mOHandleRect = Rect.zero;
            mOPinRect = Rect.zero;
            return;
        }

        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;
        // Its own size (UWModernLayout - docked, never moved).
        miScale = UWModernUi.PixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.RunePanel);

        fEnsureArt();

        UWModernClassicFrame.ApplySquash(mORoot);

        if (UWModernLayout.IsRuneTabletShown)
        {
            fLayoutTabletOnly();
            return;
        }

        float liScale = miScale;
        bool lbPinned = Pinned;

        // The rows: down to the message box at most (UWModernHud.LeftColumnBottom); the Runes tab
        // only what it needs.
        float lfFloor = UWModernHud.Instance.LeftColumnBottom;
        int liRoom = Mathf.Max(60, Mathf.FloorToInt((Screen.height - (TopMargin * liScale) - lfFloor) / liScale));
        int liTargetRows = meTab == TabEnum.Runes ? Mathf.Min(liRoom, fRunesRows()) : liRoom;
        // Moved in the layout editor it is a free window: no handle, no peeking, no sliding.
        bool lbFree = UWModernLayout.IsPlaced(UWModernLayout.ElementEnum.RunePanel);
        float lfTargetShown = fTargetShown(PanelWidth * liScale, lbFree);
        float lfStep = fSlideStep();

        if (mfRows <= 0f)
            mfRows = liTargetRows;

        mfShown = lbFree ? lfTargetShown : fGlide(mfShown, lfTargetShown, lfStep, 1f);
        mfRows = fGlide(mfRows, liTargetRows, lfStep, 0.5f);

        int liHeight = Mathf.Max(60, Mathf.RoundToInt(mfRows));
        float lfLeft = Mathf.Round(mfShown - (PanelWidth * liScale));
        float lfTop = Mathf.Round(Screen.height - (TopMargin * liScale));
        float lfBottom = lfTop - (liHeight * liScale);
        float lfWidth = PanelWidth * liScale;
        float lfTabRise = (TabHeaderHeight - 2) * liScale;

        if (lbFree)
        {
            // Where it was put (UWModernLayout), its tab row above it kept on the screen.
            Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.RunePanel,
                new Rect(0f, lfBottom, lfWidth, (liHeight * liScale) + lfTabRise));

            lfLeft = lOPlaced.x;
            lfBottom = lOPlaced.y;
            lfTop = lOPlaced.yMax - lfTabRise;
        }

        mOPanelRect = mfShown > 0.5f ? new Rect(lfLeft, lfBottom, lfWidth, liHeight * liScale) : Rect.zero;

        // The leather (UWModernDockedPanel).
        fLayoutBack(PanelWidth, liHeight, lfLeft, lfBottom);

        // The handle out of the right edge, mirrored, over the frame there - not when pinned.
        Rect lOHandle = new Rect(lfLeft + lfWidth, Screen.height - ((HandleTop + HandleHeight) * liScale),
            HandleWidth * liScale, HandleHeight * liScale);

        fLayoutHandle(lOHandle, lbPinned, lbFree);

        // The tab headers and the pin over the top edge, once the panel is out (UWModernDockedPanel).
        fLayoutTabRow(lfLeft, lfTop, PanelWidth, (int)meTab, lbPinned);

        ScreenRect = lbFree
            ? (mOPanelRect.width > 0f ? Rect.MinMaxRect(mOPanelRect.xMin, mOPanelRect.yMin, mOPanelRect.xMax, lfTop + lfTabRise) : Rect.zero)
            : mOPanelRect.width > 0f
            ? Rect.MinMaxRect(mOPanelRect.xMin, Mathf.Min(mOPanelRect.yMin, lOHandle.yMin), lOHandle.xMax, lfTop + (7 * liScale))
            : lOHandle;

        if (ScreenRect.width > 0f)
            UWModernLayout.Report(UWModernLayout.ElementEnum.RunePanel, ScreenRect);

        // The title row and the tab's content.
        bool lbContent = mfShown > 0.5f;
        float lfInnerX = lfLeft + (ContentPad * liScale);
        float lfInner = (PanelWidth - (2 * ContentPad)) * liScale;
        int liFont = Mathf.Max(10, Mathf.RoundToInt(4.4f * liScale));
        int liSmall = Mathf.Max(9, Mathf.RoundToInt(3.4f * liScale));

        mOTitle.enabled = lbContent;
        mOTitleRight.enabled = lbContent;
        mOTitle.fontSize = Mathf.Max(11, Mathf.RoundToInt(5.2f * liScale));
        mOTitleRight.fontSize = liSmall;
        UWUiKit.SetRect(mOTitle.rectTransform, lfInnerX, lfTop - ((TitleTop + 9) * liScale), lfInner, 9 * liScale);
        UWUiKit.SetRect(mOTitleRight.rectTransform, lfInnerX, lfTop - ((TitleTop + 9) * liScale), lfInner - liScale, 9 * liScale);

        mORunesContent.gameObject.SetActive(lbContent && meTab == TabEnum.Runes);
        mOListViewport.gameObject.SetActive(lbContent && meTab == TabEnum.Spells);
        mOTabletRect = Rect.zero;
        mOHollowRect = Rect.zero;
        mOListRect = Rect.zero;

        if (lbContent && meTab == TabEnum.Runes)
            fLayoutRunes(lfLeft, lfTop, liFont, liSmall);
        else if (lbContent)
            fLayoutSpells(lfLeft, lfTop, lfBottom, liFont, liSmall);

        fLayoutGhost();
    }

    /// <summary>The rows the Runes tab needs, the frame included.</summary>
    private static int fRunesRows()
    {
        return TabletTop + UWTextures.PanelHeight + HollowGap + HollowHeight + InfoRows + BottomPad;
    }

    /// <summary>
    /// THE ORIGINAL'S RUNE TABLET ALONE (Classic+, per user 2026-10-10: the original panels "each a
    /// single panel", "always visible"): only the page with the runes of the bag, where the layout
    /// editor puts it (UWModernLayout.ElementEnum.RuneTablet; by default at the right edge, at mid
    /// height) - no leather, handle, tabs, pin, hollow or list; its clicks are the Runes tab's
    /// (fClickRunes: a rune onto the shelf, the right button looks, the clear area empties it).
    /// </summary>
    private void fLayoutTabletOnly()
    {
        // Its own element's size, not the rune panel's (per user, 2026-10-10: it did not size).
        miScale = UWModernUi.PixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.RuneTablet);

        float liScale = miScale;
        float lfWidth = UWTextures.PanelWidth * liScale * UWModernHudArt.PixelAspectX;
        float lfHeight = UWTextures.PanelHeight * liScale;
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.RuneTablet,
            new Rect(Screen.width - lfWidth - (4f * liScale), (Screen.height - lfHeight) * 0.5f, lfWidth, lfHeight));

        UWModernLayout.Report(UWModernLayout.ElementEnum.RuneTablet, lOPlaced);

        mOBack.enabled = false;
        mOHandle.enabled = false;
        mOHandleText.enabled = false;
        mOPin.enabled = false;
        mOPinIcon.enabled = false;
        mOTitle.enabled = false;
        mOTitleRight.enabled = false;

        for (int liTab = 0; liTab < mOTabBacks.Length; liTab++)
        {
            mOTabBacks[liTab].enabled = false;
            mOTabTexts[liTab].enabled = false;
            mOTabRects[liTab] = Rect.zero;
        }

        mOHandleRect = Rect.zero;
        mOPinRect = Rect.zero;
        mOHollowRect = Rect.zero;
        mOListRect = Rect.zero;
        mORunesContent.gameObject.SetActive(true);
        mOListViewport.gameObject.SetActive(false);
        fShowHollow(false);

        mOPanelRect = lOPlaced;
        ScreenRect = lOPlaced;
        fPlaceTablet(lOPlaced);
        fLayoutGhost();
    }

    /// <summary>The hollow, its frame, runes and the spell's lines - not with the tablet alone.</summary>
    private void fShowHollow(bool pbShown)
    {
        mOHollow.enabled = pbShown;

        foreach (Image lOLine in mOHollowFrame)
            lOLine.enabled = pbShown;

        if (!pbShown)
        {
            foreach (RawImage lORune in mOHollowRunes)
                lORune.enabled = false;
        }

        mOSpellName.enabled = pbShown;
        mOSpellCircle.enabled = pbShown;
        mOHint.enabled = pbShown;
    }

    /// <summary>The tablet at a rect and the runes of the bag on it; how many there are.</summary>
    private int fPlaceTablet(Rect pORect)
    {
        float liScale = miScale;
        UWPlayerData lOPlayer = fData().InitialPlayer;
        int liOwned = 0;

        mOTabletRect = pORect;
        UWUiKit.SetRect(mOTablet.rectTransform, mOTabletRect.x, mOTabletRect.y, mOTabletRect.width, mOTabletRect.height);

        for (int liRune = 0; liRune < mORunes.Length; liRune++)
        {
            bool lbOwned = lOPlayer != null && lOPlayer.HasRune((UWPlayerData.Rune)liRune);
            Texture2D lOTexture = mORuneTextures[liRune];

            if (lbOwned)
                liOwned++;

            mORunes[liRune].enabled = lbOwned && lOTexture != null;

            if (!mORunes[liRune].enabled)
                continue;

            float lfX = mOTabletRect.xMin + (UWRuneShelfModel.SlotLeft(liRune) * liScale * UWModernHudArt.PixelAspectX);
            float lfRowTop = mOTabletRect.yMax - (UWRuneShelfModel.SlotTop(liRune) * liScale);

            UWUiKit.SetRect(mORunes[liRune].rectTransform, lfX, lfRowTop - (lOTexture.height * liScale),
                lOTexture.width * liScale * UWModernHudArt.PixelAspectX, lOTexture.height * liScale);
        }

        return liOwned;
    }

    private void fLayoutRunes(float pfLeft, float pfTop, int piFont, int piSmall)
    {
        float liScale = miScale;
        UWHudRunes lORunes = mOUi.Runes;

        mOTitle.text = UWText.T("hud.runes.title");
        fShowHollow(true);

        // The shelf with the runes of the bag.
        // At the original's pixel proportion (UWModernHudArt.PixelAspectX), in the middle of its place.
        int liOwned = fPlaceTablet(new Rect(pfLeft + ((TabletLeft + (UWTextures.PanelWidth * (1f - UWModernHudArt.PixelAspectX) * 0.5f)) * liScale),
            pfTop - ((TabletTop + UWTextures.PanelHeight) * liScale), UWTextures.PanelWidth * liScale * UWModernHudArt.PixelAspectX, UWTextures.PanelHeight * liScale));

        mOTitleRight.text = UWText.F("hud.runes.owned", liOwned, UWPlayerData.RuneCount);

        // The hollow with the chosen runes.
        float lfHollowTop = pfTop - ((TabletTop + UWTextures.PanelHeight + HollowGap) * liScale);

        mOHollowRect = new Rect(pfLeft + (((PanelWidth - HollowWidth) / 2) * liScale), lfHollowTop - (HollowHeight * liScale),
            HollowWidth * liScale, HollowHeight * liScale);
        UWUiKit.SetRect(mOHollow.rectTransform, mOHollowRect.x, mOHollowRect.y, mOHollowRect.width, mOHollowRect.height);

        IReadOnlyList<int> lOShelf = lORunes != null ? lORunes.Shelf.Selected : null;
        int liSequence = UWRuneShelfModel.SequenceOf(lOShelf);
        bool lbSpell = UWRunicMagic.TryGetSpell(liSequence, out UWRunicMagic.Spell lOSpell);
        bool lbHover = UWPointer.HasMouse && UWModernPointer.IsFree && mOHollowRect.Contains(UWPointer.Position);

        for (int liAt = 0; liAt < mOHollowRunes.Length; liAt++)
        {
            int liRune = lOShelf != null && liAt < lOShelf.Count ? lOShelf[liAt] : -1;
            Texture2D lOTexture = liRune >= 0 && liRune < mORuneTextures.Length ? mORuneTextures[liRune] : null;

            mOHollowRunes[liAt].enabled = lOTexture != null;

            if (lOTexture != null)
            {
                mOHollowRunes[liAt].texture = lOTexture;
                UWUiKit.SetRect(mOHollowRunes[liAt].rectTransform,
                    mOHollowRect.x + ((3 + (liAt * HollowRunePitch) + (lOTexture.width * (1f - UWModernHudArt.PixelAspectX) * 0.5f)) * liScale),
                    mOHollowRect.yMax - ((3 + lOTexture.height) * liScale), lOTexture.width * liScale * UWModernHudArt.PixelAspectX, lOTexture.height * liScale);
            }
        }

        // The frame: gold under the pointer when it holds a spell - take it by it.
        Color lOFrame = lbSpell && (lbHover || mbDragging) ? UWModernPalette.Gold : UWModernPalette.Rule;

        fSetFrame(mOHollowFrame, mOHollowRect, lOFrame);

        // The spell's name, circle and mana, and what to do with it.
        float lfInfoTop = mOHollowRect.yMin - (3 * liScale);
        float lfWidth = PanelWidth * liScale;
        int liCircle = CharacterCircle;

        mOSpellName.fontSize = piFont;
        mOSpellCircle.fontSize = piSmall;
        mOHint.fontSize = piSmall;

        if (lbSpell)
        {
            bool lbCastable = fIsCastableOnPanel(liSequence);

            mOSpellName.text = SpellName(liSequence);
            mOSpellName.color = lbCastable ? UWModernPalette.Text : UWModernPalette.Grey;
            mOSpellCircle.text = UWText.F("hud.runes.circle", lOSpell.Level, lOSpell.ManaCost);
            mOHint.text = lOSpell.Level > liCircle ? UWText.T("hud.runes.above_circle") : UWText.T("hud.runes.drag_or_cast");
        }
        else
        {
            mOSpellName.text = lOShelf != null && lOShelf.Count > 0 ? UWText.T("hud.runes.not_a_spell") : UWText.T("hud.runes.none_chosen");
            mOSpellName.color = UWModernPalette.Grey;
            mOSpellCircle.text = string.Empty;
            mOHint.text = UWText.T("hud.runes.click_runes");
        }

        UWUiKit.SetRect(mOSpellName.rectTransform, pfLeft, lfInfoTop - (9 * liScale), lfWidth, 9 * liScale);
        UWUiKit.SetRect(mOSpellCircle.rectTransform, pfLeft, lfInfoTop - (16 * liScale), lfWidth, 7 * liScale);
        UWUiKit.SetRect(mOHint.rectTransform, pfLeft, lfInfoTop - (24 * liScale), lfWidth, 7 * liScale);
    }

    private void fLayoutSpells(float pfLeft, float pfTop, float pfBottom, int piFont, int piSmall)
    {
        float liScale = miScale;
        int liCircle = CharacterCircle;

        mOTitle.text = UWText.T("hud.spells.title");
        mOTitleRight.text = liCircle > 0 ? UWText.F("hud.spells.up_to_circle", liCircle) : string.Empty;

        if (mOEntries.Count == 0)
            fBuildEntries();

        mOListRect = new Rect(pfLeft + (UWModernHudArt.LeatherLeft * liScale), pfBottom + (UWModernHudArt.LeatherBottom * liScale),
            (PanelWidth - UWModernHudArt.LeatherLeft - UWModernHudArt.LeatherRight) * liScale,
            pfTop - (ListTop * liScale) - (pfBottom + (UWModernHudArt.LeatherBottom * liScale)));
        UWUiKit.SetRect(mOListViewport, mOListRect.x, mOListRect.y, mOListRect.width, Mathf.Max(0f, mOListRect.height));

        float lfViewRows = mOListRect.height / liScale;

        mfScroll = Mathf.Clamp(mfScroll, 0f, Mathf.Max(0f, mfListRows - lfViewRows));

        float lfContentTop = mOListRect.height;
        float lfInner = mOListRect.width;
        Vector2 lOPointer = UWPointer.HasMouse ? UWPointer.Position : new Vector2(-1f, -1f);
        ListEntry lOHovered = UWModernPointer.IsFree ? fEntryAt(lOPointer) : null;
        UWPlayerData lOPlayer = fData().InitialPlayer;

        mORowHighlight.enabled = false;

        foreach (ListEntry lOEntry in mOEntries)
        {
            float lfTop = lfContentTop - ((lOEntry.Top - mfScroll) * liScale);
            bool lbVisible = lfTop > -lOEntry.Rows * liScale && lfTop - (lOEntry.Rows * liScale) < mOListRect.height;

            lOEntry.Name.enabled = lbVisible;

            if (lOEntry.Runes != null)
                lOEntry.Runes.enabled = lbVisible;

            if (lOEntry.Rule != null)
                lOEntry.Rule.enabled = lbVisible;

            if (!lbVisible)
            {
                if (lOEntry.Icon != null)
                    lOEntry.Icon.enabled = false;

                continue;
            }

            float lfX = 2 * liScale;

            if (lOEntry.Sequence < 0)
            {
                lOEntry.Name.fontSize = piSmall;
                lOEntry.Name.color = lOEntry.Circle <= liCircle ? UWModernPalette.Dim : UWModernPalette.Grey;
                UWUiKit.SetRect(lOEntry.Name.rectTransform, lfX, lfTop - (8 * liScale), lfInner, 7 * liScale);
                UWUiKit.SetRect(lOEntry.Rule.rectTransform, lfX, lfTop - (9 * liScale), lfInner - (4 * liScale), Mathf.Max(2f, liScale * 0.5f));
                continue;
            }

            bool lbCastable = lOPlayer != null && fIsCastableOnPanel(lOEntry.Sequence);
            float lfTextX = 13 * liScale;

            lOEntry.Name.fontSize = piSmall + 1;
            lOEntry.Name.color = lbCastable ? UWModernPalette.Text : UWModernPalette.Grey;
            UWUiKit.SetRect(lOEntry.Name.rectTransform, lfTextX, lfTop - (6.5f * liScale), lfInner - lfTextX, 6 * liScale);

            lOEntry.Runes.fontSize = Mathf.Max(8, piSmall - 1);
            lOEntry.Runes.color = lbCastable ? UWModernPalette.RunesText : UWModernPalette.Grey;
            UWUiKit.SetRect(lOEntry.Runes.rectTransform, lfTextX, lfTop - (11.5f * liScale), lfInner - lfTextX, 5 * liScale);

            if (lOEntry.Icon != null)
            {
                lOEntry.Icon.enabled = true;
                lOEntry.Icon.color = lbCastable ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
                UWUiKit.SetRect(lOEntry.Icon.rectTransform, lfX + (9 * (1f - UWModernHudArt.PixelAspectX) * 0.5f * liScale), lfTop - (10.5f * liScale), 9 * liScale * UWModernHudArt.PixelAspectX,
                    9 * liScale);
            }

            if (lOEntry == lOHovered || (mbDragging && !mbPressHollow && lOEntry.Sequence == miPressSequence))
            {
                mORowHighlight.enabled = true;
                UWUiKit.SetRect(mORowHighlight.rectTransform, liScale, lfTop - (RowRows * liScale), lfInner - (2 * liScale), RowRows * liScale);
            }
        }
    }

    private void fBuildEntries()
    {
        int liCircle = 0;
        int liTop = 0;
        DataImport lOData = fData();

        foreach (UWRunicMagic.Spell lOSpell in UWRunicMagic.AllSpells)
        {
            if (lOSpell.RuneSequence == UWRunicMagic.EmptySequence)
                continue;

            if (lOSpell.Level != liCircle)
            {
                liCircle = lOSpell.Level;

                ListEntry lOHeader = new ListEntry { Circle = liCircle, Top = liTop, Rows = HeaderRows };

                lOHeader.Name = UWUiKit.CreateText(mOListContent, "Circle", TextAnchor.LowerLeft, UWModernPalette.Dim, mOUi);
                lOHeader.Name.text = UWText.F("hud.runes.circle", liCircle, lOSpell.ManaCost);
                lOHeader.Rule = UWUiKit.CreateImage(mOListContent, "Rule", UWModernPalette.Rule);
                mOEntries.Add(lOHeader);
                liTop += HeaderRows;
            }

            ListEntry lOEntry = new ListEntry
            {
                Circle = liCircle,
                Sequence = lOSpell.RuneSequence,
                Spell = lOSpell,
                Top = liTop,
                Rows = RowRows
            };

            lOEntry.Name = UWUiKit.CreateText(mOListContent, "Spell", TextAnchor.MiddleLeft, UWModernPalette.Text, mOUi);
            lOEntry.Name.text = UWRunicMagic.GetName(lOSpell, lOData.Strings);
            lOEntry.Runes = UWUiKit.CreateText(mOListContent, "Runes", TextAnchor.MiddleLeft, UWModernPalette.RunesText, mOUi);
            lOEntry.Runes.text = UWRunicMagic.DescribeRunes(lOSpell.RuneSequence);

            UWTexture lOIcon = SpellIcon(lOSpell.RuneSequence);

            if (lOIcon != null)
            {
                lOEntry.Icon = UWUiKit.CreateRawImage(mOListContent, "Icon");
                lOEntry.Icon.texture = fGetIcon(lOIcon);
                UWIconPalette.ApplySmooth(lOEntry.Icon);
            }

            mOEntries.Add(lOEntry);
            liTop += RowRows;
        }

        mfListRows = liTop + 2;
        mORowHighlight.transform.SetAsFirstSibling();
    }

    /// <summary>The spell being dragged follows the pointer: its picture, else its runes.</summary>
    private void fLayoutGhost()
    {
        bool lbShow = mbDragging && miPressSequence >= 0 && UWPointer.HasMouse;

        mOGhostCanvas.enabled = lbShow;

        if (!lbShow)
            return;

        float liScale = miScale;
        float lfSlot = UWModernHudArt.SlotSize * liScale;
        Vector2 lOPointer = UWPointer.Position;
        UWTexture lOIcon = SpellIcon(miPressSequence);

        UWUiKit.SetRect(mOGhostBack.rectTransform, lOPointer.x - (lfSlot * 0.5f), lOPointer.y - (lfSlot * 0.5f), lfSlot, lfSlot);

        mOGhostIcon.enabled = lOIcon != null;
        mOGhostText.enabled = lOIcon == null;

        if (lOIcon != null)
        {
            Texture2D lOTexture = fGetIcon(lOIcon);

            mOGhostIcon.texture = lOTexture;
            UWUiKit.SetRect(mOGhostIcon.rectTransform, lOPointer.x - (lOTexture.width * liScale * UWModernHudArt.PixelAspectX * 0.5f),
                lOPointer.y - (lOTexture.height * liScale * 0.5f), lOTexture.width * liScale * UWModernHudArt.PixelAspectX, lOTexture.height * liScale);
        }
        else
        {
            mOGhostText.text = RuneLabel(miPressSequence);
            mOGhostText.fontSize = Mathf.Max(9, Mathf.RoundToInt(3.6f * liScale));
            UWUiKit.SetRect(mOGhostText.rectTransform, lOPointer.x - (lfSlot * 0.5f), lOPointer.y - (lfSlot * 0.5f), lfSlot, lfSlot);
        }
    }

    private void fSetFrame(Image[] pOLines, Rect pORect, Color pOColour)
    {
        float lfLine = Mathf.Max(2f, miScale * 0.6f);

        UWUiKit.SetRect(pOLines[0].rectTransform, pORect.xMin - lfLine, pORect.yMax, pORect.width + (2 * lfLine), lfLine);
        UWUiKit.SetRect(pOLines[1].rectTransform, pORect.xMin - lfLine, pORect.yMin - lfLine, pORect.width + (2 * lfLine), lfLine);
        UWUiKit.SetRect(pOLines[2].rectTransform, pORect.xMin - lfLine, pORect.yMin, lfLine, pORect.height);
        UWUiKit.SetRect(pOLines[3].rectTransform, pORect.xMax, pORect.yMin, lfLine, pORect.height);

        foreach (Image lOLine in pOLines)
            lOLine.color = pOColour;
    }

    private Texture2D fGetIcon(UWTexture pOSource)
    {
        return UWIconCache.Texture(pOSource, mOUi.TextureFilterMode);
    }

    private void fEnsureArt()
    {
        UWArtStamp lOStamp = UWVersionedTexture.Backs();

        if (mOHandleArt.IsCurrent(lOStamp) && mOTabletTexture != null)
            return;

        fDestroyTextures();

        // The list's icons were built with the old colours.
        foreach (ListEntry lOEntry in mOEntries)
        {
            if (lOEntry.Icon != null)
                lOEntry.Icon.texture = fGetIcon(SpellIcon(lOEntry.Sequence));
        }

        DataImport lOData = fData();
        UWTextures lOTextures = lOData.Textures;
        FilterMode leFilter = mOUi.TextureFilterMode;

        mOHandleArt.Set(lOStamp, UWModernHudArt.BuildPartLeatherTab(UWModernLayout.ElementEnum.RunePanel, lOTextures,
            HandleWidth + UWModernHudArt.LeatherLeft, HandleHeight, leFilter));

        // The tab headers, the pin and its icon (UWModernDockedPanel).
        fBuildChromeArt(lOTextures, leFilter);
        mOGhostTexture = UWModernHudArt.BuildPartSlot(UWModernLayout.ElementEnum.RunePanel, lOTextures, leFilter);

        // The original's rune tablet (PANELS.GR 1), its transparent pixels keeping their colours;
        // it follows the colour help in place (UWGrTextures).
        mOTabletTexture = UWGrTextures.Get(lOTextures, UWTexture.TextureTypes.PANELS, 1, leFilter, UWGrTextures.ModeEnum.Colours);

        mORuneTextures = new Texture2D[UWPlayerData.RuneCount];

        for (int liRune = 0; liRune < mORuneTextures.Length; liRune++)
        {
            UWTexture lOSource = lOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, UWPlayerData.FirstRuneObjectId + liRune);

            mORuneTextures[liRune] = UWIconCache.Texture(lOSource, leFilter);
            mORunes[liRune].texture = mORuneTextures[liRune];
        }

        mOHandle.texture = mOHandleArt.Texture;
        mOTablet.texture = mOTabletTexture;
        mOTablet.enabled = mOTabletTexture != null;
        mOGhostBack.texture = mOGhostTexture;
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        mOCanvas = UWUiKit.CreateOverlayCanvas(transform, "Modern rune panel", UWUiSorting.ModernRunePanel);

        // Everything in a content group, which the classic frame's panel turn squeezes
        // (UWModernClassicFrame.ApplySquash).
        mORoot = UWUiKit.CreateGroup(mOCanvas.transform, "Content");
        mORoot.sizeDelta = Vector2.zero;

        // The tab headers, the pin, the leather and the handle, mirrored (UWModernDockedPanel).
        fBuildChrome(mORoot, new[] { UWText.T("hud.runes.tab.runes"), UWText.T("hud.runes.tab.spells") }, UWText.T("hud.runes.handle"));

        mOTitle = UWUiKit.CreateText(mORoot, "Title", TextAnchor.MiddleLeft, UWModernPalette.Text, mOUi);
        mOTitleRight = UWUiKit.CreateText(mORoot, "Title right", TextAnchor.MiddleRight, UWModernPalette.Dim, mOUi);

        // The Runes tab.
        mORunesContent = UWUiKit.CreateGroup(mORoot, "Runes");
        mOTablet = UWUiKit.CreateRawImage(mORunesContent, "Rune shelf");
        UWPixelArtUI.Apply(mOTablet);

        mORunes = new RawImage[UWPlayerData.RuneCount];

        for (int liRune = 0; liRune < mORunes.Length; liRune++)
        {
            mORunes[liRune] = UWUiKit.CreateRawImage(mORunesContent, UWRunicMagic.RuneNames[liRune]);
            UWIconPalette.ApplySmooth(mORunes[liRune]);
        }

        mOHollow = UWUiKit.CreateImage(mORunesContent, "Hollow", msHollowFill);
        mOHollowFrame = new Image[4];

        for (int liLine = 0; liLine < mOHollowFrame.Length; liLine++)
            mOHollowFrame[liLine] = UWUiKit.CreateImage(mORunesContent, "Frame", UWModernPalette.Rule);

        mOHollowRunes = new RawImage[UWRuneShelfModel.Capacity];

        for (int liAt = 0; liAt < mOHollowRunes.Length; liAt++)
        {
            mOHollowRunes[liAt] = UWUiKit.CreateRawImage(mORunesContent, "Chosen rune");
            UWIconPalette.ApplySmooth(mOHollowRunes[liAt]);
        }

        mOSpellName = UWUiKit.CreateText(mORunesContent, "Spell", TextAnchor.MiddleCenter, UWModernPalette.Text, mOUi);
        mOSpellCircle = UWUiKit.CreateText(mORunesContent, "Circle", TextAnchor.MiddleCenter, UWModernPalette.Dim, mOUi);
        mOHint = UWUiKit.CreateText(mORunesContent, "Hint", TextAnchor.MiddleCenter, UWModernPalette.Dim, mOUi);

        // The Spells tab: a clipped viewport with the rows inside.
        mOListViewport = UWUiKit.CreateClipArea(mORoot, "Spell list");

        mOListContent = UWUiKit.CreateGroup(mOListViewport, "Rows");
        mORowHighlight = UWUiKit.CreateImage(mOListContent, "Hover", UWModernPalette.WithAlpha(UWModernPalette.Gold, 0.18f));

        // The dragged spell over everything, the action bar included.
        mOGhostCanvas = UWUiKit.CreateLayerCanvas(mORoot, "Dragged spell", UWUiSorting.ModernRuneGhost);

        RectTransform lOGhostRect = (RectTransform)mOGhostCanvas.transform;
        lOGhostRect.anchorMin = Vector2.zero;
        lOGhostRect.anchorMax = Vector2.zero;
        lOGhostRect.pivot = Vector2.zero;

        mOGhostBack = UWUiKit.CreateRawImage(lOGhostRect, "Slot", true);
        mOGhostBack.color = new Color(1f, 1f, 1f, 0.85f);
        UWPixelArtUI.Apply(mOGhostBack);
        mOGhostIcon = UWUiKit.CreateRawImage(lOGhostRect, "Icon");
        UWIconPalette.ApplySmooth(mOGhostIcon);
        mOGhostText = UWUiKit.CreateText(lOGhostRect, "Runes", TextAnchor.MiddleCenter, UWModernPalette.RunesText, mOUi);
        mOGhostCanvas.enabled = false;
    }
}
