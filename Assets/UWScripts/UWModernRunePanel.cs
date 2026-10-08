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
///     lays a rune into the hollow below (the fourth pushes out the first, UWHudRunes.AddToShelf),
///     the right button looks at it, the strip at the bottom clears the hollow. The hollow IS the
///     game's rune shelf - the classic scheme sees the same runes, the save game keeps them. A
///     click on it casts; dragged by it, the spell goes onto the action bar (UWModernActionBar).
///   - SPELLS: every rune spell circle by circle, grey what cannot be cast now (a rune missing,
///     above the character's circle); a click lays the runes into the hollow (as the help's list),
///     dragged it goes onto the action bar too.
///   - The Runes tab is only as tall as it needs, the Spells tab takes the rows down to the
///     message box; the message box moves right of the panel while it covers it (UWModernHud).
/// </summary>
public class UWModernRunePanel : MonoBehaviour
{
    public enum TabEnum
    {
        Runes,
        Spells
    }

    public static UWModernRunePanel Instance { get; private set; }

    public bool IsOpen => mbOpen;

    public static bool Pinned => UWUserSettings.ModernRunesPinned;

    /// <summary>Whether Escape has something to close here: open and not pinned.</summary>
    public bool IsClosable => mbOpen && !Pinned;

    /// <summary>The panel with its handle on the screen (pixels, bottom-left origin), empty while
    /// nothing of it shows.</summary>
    public Rect ScreenRect { get; private set; }

    /// <summary>The layout in original pixels.</summary>
    private const int PanelWidth = 96;

    private const int TopMargin = 14;

    private const int HandleWidth = 9;

    private const int HandleHeight = 40;

    private const int HandleTop = 104;

    private const int PeekWidth = 15;

    private const int PeekReach = 14;

    private const int TabHeaderWidth = 30;

    private const int TabHeaderHeight = 9;

    private const int PinWidth = 11;

    private const int ContentPad = 6;

    private const int TitleTop = 4;

    /// <summary>The rune shelf picture, centred in the frame, and its grid (UWHudPanel's).</summary>
    private const int TabletLeft = 7;

    private const int TabletTop = 15;

    private const int RuneLeft = 8;

    private const int RuneTop = 5;

    private const int RunePitchX = 18;

    private const int RunePitchY = 15;

    private const int RuneColumns = 4;

    private const int RuneCell = 14;

    private const int ClearTop = 93;

    private const int ClearBottom = 110;

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

    private const float SlideRate = 14f;

    private const float ClickMoveThreshold = 6f;

    private static readonly Color msText = new Color(0.94f, 0.87f, 0.71f);

    private static readonly Color msDim = new Color(0.78f, 0.71f, 0.57f);

    private static readonly Color msGrey = new Color(0.55f, 0.50f, 0.42f);

    private static readonly Color msRunesText = new Color(0.77f, 0.84f, 1f);

    private static readonly Color msGold = new Color(0.925f, 0.77f, 0.44f, 1f);

    private static readonly Color msRule = new Color(0.47f, 0.28f, 0.12f, 1f);

    private static readonly Color msHollowFill = new Color(0.14f, 0.08f, 0.03f, 0.8f);

    private UWGameUI mOUi;

    private UWControlScheme mOScheme;

    private Font mOFont;

    private Canvas mOCanvas;

    private RectTransform mORoot;

    private RawImage mOBack;

    private RawImage mOHandle;

    private Text mOHandleText;

    private RawImage[] mOTabBacks;

    private Text[] mOTabTexts;

    private RawImage mOPin;

    private RawImage mOPinIcon;

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

    private Texture2D mOBackTexture;

    private Vector2Int mOBackSize;

    private Texture2D mOHandleTexture;

    private Texture2D mOTabTexture;

    private Texture2D mOPinTexture;

    private Texture2D mOPinIconTexture;

    private Texture2D mOTabletTexture;

    private Texture2D mOGhostTexture;

    private Texture2D[] mORuneTextures;

    private readonly Dictionary<UWTexture, Texture2D> mOIconTextures = new Dictionary<UWTexture, Texture2D>();

    private int miArtVersion = -1;

    private bool mbOpen;

    private bool mbPeek;

    private TabEnum meTab = TabEnum.Runes;

    private float mfShown;

    private float mfRows;

    private float miScale = 1f;

    /// <summary>The spell list scrolled down this many original rows.</summary>
    private float mfScroll;

    private float mfListRows;

    // Screen rectangles of the last layout, for the clicks.
    private Rect mOPanelRect;

    private Rect mOHandleRect;

    private Rect mOPinRect;

    private readonly Rect[] mOTabRects = new Rect[2];

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
        mOFont = Resources.Load<Font>("Fonts/LexendExa");
    }

    private void fDestroyTextures()
    {
        foreach (Texture2D lOTexture in new[] { mOBackTexture, mOHandleTexture, mOTabTexture, mOPinTexture, mOPinIconTexture,
            mOTabletTexture, mOGhostTexture })
        {
            if (lOTexture != null)
                Destroy(lOTexture);
        }

        mOBackTexture = null;
        mOHandleTexture = null;
        mOTabTexture = null;
        mOPinTexture = null;
        mOPinIconTexture = null;
        mOTabletTexture = null;
        mOGhostTexture = null;

        if (mORuneTextures != null)
        {
            foreach (Texture2D lORune in mORuneTextures)
            {
                if (lORune != null)
                    Destroy(lORune);
            }

            mORuneTextures = null;
        }

        foreach (Texture2D lOIcon in mOIconTextures.Values)
            Destroy(lOIcon);

        mOIconTextures.Clear();
    }

    private bool fIsModern()
    {
        return mOScheme != null && mOScheme.Current == UWControlScheme.SchemeEnum.Modern;
    }

    private bool fIsShown()
    {
        return fIsModern() && UWModernHud.Instance != null && UWModernHud.Instance.IsShowing
            && mOUi != null && mOUi.mOUWData != null && mOUi.mCharacter != null;
    }

    private DataImport fData()
    {
        return mOUi != null ? mOUi.mOUWData : null;
    }

    /// <summary>Whether a screen point lies on the panel, its handle, tabs or pin - nothing goes
    /// into the world through it.</summary>
    public bool Contains(Vector2 pOPointer)
    {
        return fIsShown() && (mOPanelRect.Contains(pOPointer) || mOHandleRect.Contains(pOPointer)
            || mOTabRects[0].Contains(pOPointer) || mOTabRects[1].Contains(pOPointer) || mOPinRect.Contains(pOPointer));
    }

    // ------------------------------------------------- The spells, for the panel and the bar

    /// <summary>The runes of a sequence, first to last, empty slots left out.</summary>
    public static List<int> RunesOf(int piSequence)
    {
        List<int> lORunes = new List<int>(3);

        for (int liShift = 10; liShift >= 0; liShift -= 5)
        {
            int liRune = (piSequence >> liShift) & 0x1F;

            if (liRune < UWRunicMagic.EmptyRune)
                lORunes.Add(liRune);
        }

        return lORunes;
    }

    public static int SequenceOf(IReadOnlyList<int> pORunes)
    {
        int liFirst = pORunes != null && pORunes.Count > 0 ? pORunes[0] : UWRunicMagic.EmptyRune;
        int liSecond = pORunes != null && pORunes.Count > 1 ? pORunes[1] : UWRunicMagic.EmptyRune;
        int liThird = pORunes != null && pORunes.Count > 2 ? pORunes[2] : UWRunicMagic.EmptyRune;

        return UWRunicMagic.GetSequence(liFirst, liSecond, liThird);
    }

    /// <summary>Whether every rune of the sequence is in the bag.</summary>
    public static bool HasRunes(int piSequence)
    {
        UWGameUI lOUi = UWScene.GameUi;
        UWPlayerData lOPlayer = lOUi != null && lOUi.mOUWData != null ? lOUi.mOUWData.InitialPlayer : null;

        if (lOPlayer == null)
            return false;

        foreach (int liRune in RunesOf(piSequence))
        {
            if (!lOPlayer.HasRune((UWPlayerData.Rune)liRune))
                return false;
        }

        return true;
    }

    /// <summary>The highest circle the character can cast: its level halved, rounded up
    /// (UWSpellCasting.CastRunes).</summary>
    public static int CharacterCircle
    {
        get
        {
            UWGameUI lOUi = UWScene.GameUi;

            return lOUi != null && lOUi.mCharacter != null ? (lOUi.mCharacter.Level + 1) / 2 : 0;
        }
    }

    /// <summary>Whether the spell could be cast now: a spell, its runes in the bag, within the
    /// character's circle, enough mana. The skill check still decides.</summary>
    public static bool CanCastNow(int piSequence)
    {
        UWGameUI lOUi = UWScene.GameUi;

        if (!UWRunicMagic.TryGetSpell(piSequence, out UWRunicMagic.Spell lOSpell) || lOUi == null || lOUi.mCharacter == null)
            return false;

        return HasRunes(piSequence) && lOSpell.Level <= CharacterCircle && lOUi.mCharacter.CurrentMana >= lOSpell.ManaCost;
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
        mbOpen = true;
        meTab = peTab;
    }

    public void Close()
    {
        if (Pinned)
            return;

        mbOpen = false;
        mbDragging = false;
        miPressSequence = -1;
    }

    private static void fSetPinned(bool pbPinned)
    {
        UWUserSettings.ModernRunesPinned = pbPinned;
        UWUserSettings.Save();
    }

    // ------------------------------------------------- Input

    /// <summary>How long the gamepad's left trigger is held to open the panel instead of casting.</summary>
    private const float PadCastHoldSeconds = 0.35f;

    /// <summary>Seconds the left trigger is held, -1 when it is not.</summary>
    private float mfPadCastHeld = -1f;

    /// <summary>
    /// THE GAMEPAD'S LEFT TRIGGER (UWGamepad): TAPPED it casts what lies in the hollow, as F8 does in
    /// the original (UWHudRunes.CastByKey); HELD it opens or closes the panel, which is how runes
    /// get into the hollow.
    /// </summary>
    private void fUpdatePadCast(UWControls pOControls)
    {
        UnityEngine.InputSystem.InputAction lOTrigger = pOControls.Player.PadCast;

        // While the gamepad's pointer drives, LT is its right button (UWGamepadPointer.Takes).
        if (lOTrigger.WasPressedThisFrame())
        {
            mfPadCastHeld = UWGamepadPointer.Takes(lOTrigger) ? -1f : 0f;
            return;
        }

        if (mfPadCastHeld < 0f)
            return;

        if (lOTrigger.IsPressed())
        {
            mfPadCastHeld += Time.unscaledDeltaTime;

            if (mfPadCastHeld >= PadCastHoldSeconds)
            {
                mfPadCastHeld = -1f;

                if (Pinned)
                    return;

                if (mbOpen)
                    Close();
                else
                    Open(meTab);
            }

            return;
        }

        mfPadCastHeld = -1f;

        if (mOUi.Runes != null)
            mOUi.Runes.CastByKey();
    }

    private void Update()
    {
        if (!fIsShown())
        {
            if (mbOpen && !fIsModern())
                mbOpen = false;

            mbPeek = false;
            mbDragging = false;
            miPressSequence = -1;
            return;
        }

        if (Pinned && !mbOpen)
            mbOpen = true;

        UWModernHud lOHud = UWModernHud.Instance;

        if (lOHud.IsOpen || UWControls.IsTextEntryActive || UWHelpWindow.BlocksGameKeys || mOUi.IsMapVisible)
        {
            mbDragging = false;
            miPressSequence = -1;
            return;
        }

        UWControls lOControls = mOScheme.Controls;

        if (lOControls == null)
            return;

        if (lOControls.Player.ModernRunes.WasPressedThisFrame() && !Pinned)
        {
            if (mbOpen)
                Close();
            else
                Open(meTab);
        }

        fUpdatePadCast(lOControls);

        Mouse lOMouse = Mouse.current;

        mbPeek = false;

        if (lOMouse == null || !UWModernPointer.IsFree)
        {
            mbDragging = false;
            miPressSequence = -1;
            return;
        }

        Vector2 lOPointer = lOMouse.position.ReadValue();
        Rect lONear = new Rect(mOHandleRect.xMin, mOHandleRect.yMin - (4 * miScale),
            mOHandleRect.width + (PeekReach * miScale), mOHandleRect.height + (8 * miScale));

        mbPeek = !mbOpen && !UWModernLayout.IsPlaced(UWModernLayout.ElementEnum.RunePanel)
            && (lONear.Contains(lOPointer) || mOPanelRect.Contains(lOPointer));

        fUpdateDrag(lOControls, lOPointer);

        float lfWheel = lOMouse.scroll.ReadValue().y;

        if (mbOpen && meTab == TabEnum.Spells && Mathf.Abs(lfWheel) > 0.01f && mOListRect.Contains(lOPointer))
            mfScroll -= Mathf.Sign(lfWheel) * RowRows * 2;

        bool lbLeft = lOControls.Player.CursorDrag.WasPressedThisFrame();
        bool lbRight = lOControls.Player.Interact.WasPressedThisFrame() && UWModernPointer.SwitchedFrame != Time.frameCount;

        if (!lbLeft && !lbRight)
            return;

        // A thing on the pointer is not put down through the panel (UWModernBags asks Contains).
        if (mOUi.mOInventory != null && mOUi.mOInventory.CursorItem != null)
            return;

        if (lbLeft && mOPinRect.Contains(lOPointer))
        {
            fSetPinned(!Pinned);
            return;
        }

        if (lbLeft && mOHandleRect.Contains(lOPointer))
        {
            if (mbOpen)
                Close();
            else
                Open(meTab);

            return;
        }

        if (!mbOpen)
        {
            if (lbLeft && mOPanelRect.Contains(lOPointer))
                Open(meTab);

            return;
        }

        for (int liTab = 0; lbLeft && liTab < mOTabRects.Length; liTab++)
        {
            if (mOTabRects[liTab].Contains(lOPointer))
            {
                meTab = (TabEnum)liTab;
                return;
            }
        }

        if (meTab == TabEnum.Runes)
            fClickRunes(lOPointer, lbLeft);
        else if (lbLeft)
            fClickSpells(lOPointer);
    }

    private void fClickRunes(Vector2 pOPointer, bool pbLeft)
    {
        UWHudRunes lORunes = mOUi.Runes;

        if (lORunes == null)
            return;

        // The clear strip at the bottom of the shelf.
        if (pbLeft && mOTabletRect.Contains(pOPointer))
        {
            float lfRow = (mOTabletRect.yMax - pOPointer.y) / miScale;

            if (lfRow >= ClearTop && lfRow <= ClearBottom)
            {
                lORunes.ClearRuneShelf();
                return;
            }
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
            miPressSequence = SequenceOf(lORunes.SelectedRunes);
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
                lORunes.CastShelf();
        }
        else if (HasRunes(liSequence))
        {
            lORunes.PutOnShelf(RunesOf(liSequence));
        }
    }

    private int fRuneAt(Vector2 pOPointer)
    {
        UWPlayerData lOPlayer = fData().InitialPlayer;

        if (lOPlayer == null || !mOTabletRect.Contains(pOPointer))
            return -1;

        float lfX = (pOPointer.x - mOTabletRect.xMin) / miScale;
        float lfY = (mOTabletRect.yMax - pOPointer.y) / miScale;

        for (int liRune = 0; liRune < UWPlayerData.RuneCount; liRune++)
        {
            float lfLeft = RuneLeft + ((liRune % RuneColumns) * RunePitchX);
            float lfTop = RuneTop + ((liRune / RuneColumns) * RunePitchY);

            if (lfX >= lfLeft && lfX < lfLeft + RuneCell && lfY >= lfTop && lfY < lfTop + RuneCell)
                return lOPlayer.HasRune((UWPlayerData.Rune)liRune) ? liRune : -1;
        }

        return -1;
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
        miScale = UWModernHud.PixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.RunePanel);

        fEnsureArt();

        float liScale = miScale;
        bool lbPinned = Pinned;

        // The rows: down to the message box at most (UWModernHud.LeftColumnBottom); the Runes tab
        // only what it needs.
        float lfFloor = UWModernHud.Instance.LeftColumnBottom;
        int liRoom = Mathf.Max(60, Mathf.FloorToInt((Screen.height - (TopMargin * liScale) - lfFloor) / liScale));
        int liTargetRows = meTab == TabEnum.Runes ? Mathf.Min(liRoom, fRunesRows()) : liRoom;
        // Moved in the layout editor it is a free window: no handle, no peeking, no sliding.
        bool lbFree = UWModernLayout.IsPlaced(UWModernLayout.ElementEnum.RunePanel);
        float lfTargetShown = mbOpen ? PanelWidth * liScale : mbPeek && !lbFree ? PeekWidth * liScale : 0f;
        float lfStep = 1f - Mathf.Exp(-SlideRate * Time.unscaledDeltaTime);

        if (mfRows <= 0f)
            mfRows = liTargetRows;

        mfShown = lbFree || Mathf.Abs(mfShown - lfTargetShown) < 1f ? lfTargetShown : Mathf.Lerp(mfShown, lfTargetShown, lfStep);
        mfRows = Mathf.Abs(mfRows - liTargetRows) < 0.5f ? liTargetRows : Mathf.Lerp(mfRows, liTargetRows, lfStep);

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

        if (mOBackTexture == null || mOBackSize.y != liHeight)
        {
            if (mOBackTexture != null)
                Destroy(mOBackTexture);

            mOBackTexture = UWModernHudArt.BuildLeather(fData().Textures, PanelWidth, liHeight, mOUi.TextureFilterMode);
            mOBackSize = new Vector2Int(PanelWidth, liHeight);
            mOBack.texture = mOBackTexture;
        }

        mOBack.enabled = mfShown > 0.5f;
        fSetRect(mOBack.rectTransform, lfLeft, lfBottom, lfWidth, liHeight * liScale);

        // The handle out of the right edge, mirrored, over the frame there - not when pinned.
        Rect lOHandle = new Rect(lfLeft + lfWidth, Screen.height - ((HandleTop + HandleHeight) * liScale),
            HandleWidth * liScale, HandleHeight * liScale);

        mOHandleRect = lbPinned || lbFree ? Rect.zero : lOHandle;
        mOHandle.enabled = !lbPinned && !lbFree;
        mOHandleText.enabled = !lbPinned && !lbFree;
        fSetRect(mOHandle.rectTransform, lOHandle.x - (UWModernHudArt.LeatherLeft * liScale), lOHandle.y,
            (HandleWidth + UWModernHudArt.LeatherLeft) * liScale, lOHandle.height);

        RectTransform lOLabel = mOHandleText.rectTransform;
        lOLabel.pivot = new Vector2(0.5f, 0.5f);
        lOLabel.anchoredPosition = lOHandle.center;
        lOLabel.sizeDelta = new Vector2(lOHandle.height, lOHandle.width);
        lOLabel.localRotation = Quaternion.Euler(0f, 0f, -90f);
        mOHandleText.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));

        // The tab headers and the pin over the top edge, once the panel is out.
        bool lbOut = mbOpen && mfShown > PeekWidth * liScale;

        for (int liTab = 0; liTab < mOTabBacks.Length; liTab++)
        {
            Rect lOTab = new Rect(lfLeft + ((ContentPad + (liTab * (TabHeaderWidth + 2))) * liScale), lfTop - (2 * liScale),
                TabHeaderWidth * liScale, TabHeaderHeight * liScale);

            mOTabRects[liTab] = lbOut ? lOTab : Rect.zero;
            mOTabBacks[liTab].enabled = lbOut;
            mOTabTexts[liTab].enabled = lbOut;
            mOTabBacks[liTab].color = (int)meTab == liTab ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
            mOTabTexts[liTab].color = (int)meTab == liTab ? msText : msDim;
            mOTabTexts[liTab].fontSize = Mathf.Max(9, Mathf.RoundToInt(4.2f * liScale));
            fSetRect(mOTabBacks[liTab].rectTransform, lOTab.x, lOTab.y, lOTab.width, lOTab.height);
            fSetRect(mOTabTexts[liTab].rectTransform, lOTab.x, lOTab.y + liScale, lOTab.width, lOTab.height);
        }

        Rect lOPin = new Rect(lfLeft + ((PanelWidth - ContentPad - PinWidth) * liScale), lfTop - (2 * liScale),
            PinWidth * liScale, TabHeaderHeight * liScale);

        mOPinRect = lbOut ? lOPin : Rect.zero;
        mOPin.enabled = lbOut;
        mOPinIcon.enabled = lbOut;
        mOPin.color = lbPinned ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
        mOPinIcon.color = lbPinned ? Color.white : new Color(0.78f, 0.78f, 0.78f, 1f);
        fSetRect(mOPin.rectTransform, lOPin.x, lOPin.y, lOPin.width, lOPin.height);

        RectTransform lOPinIcon = mOPinIcon.rectTransform;
        lOPinIcon.pivot = new Vector2(0.5f, 0.5f);
        lOPinIcon.anchoredPosition = lOPin.center + new Vector2(0f, 0.5f * liScale);
        lOPinIcon.sizeDelta = new Vector2(7f * liScale, 7f * liScale);
        lOPinIcon.localRotation = Quaternion.Euler(0f, 0f, lbPinned ? 0f : -35f);

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
        fSetRect(mOTitle.rectTransform, lfInnerX, lfTop - ((TitleTop + 9) * liScale), lfInner, 9 * liScale);
        fSetRect(mOTitleRight.rectTransform, lfInnerX, lfTop - ((TitleTop + 9) * liScale), lfInner - liScale, 9 * liScale);

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

    private void fLayoutRunes(float pfLeft, float pfTop, int piFont, int piSmall)
    {
        float liScale = miScale;
        UWPlayerData lOPlayer = fData().InitialPlayer;
        UWHudRunes lORunes = mOUi.Runes;
        int liOwned = 0;

        mOTitle.text = "Rune bag";

        // The shelf with the runes of the bag.
        mOTabletRect = new Rect(pfLeft + (TabletLeft * liScale), pfTop - ((TabletTop + UWTextures.PanelHeight) * liScale),
            UWTextures.PanelWidth * liScale, UWTextures.PanelHeight * liScale);
        fSetRect(mOTablet.rectTransform, mOTabletRect.x, mOTabletRect.y, mOTabletRect.width, mOTabletRect.height);

        for (int liRune = 0; liRune < mORunes.Length; liRune++)
        {
            bool lbOwned = lOPlayer != null && lOPlayer.HasRune((UWPlayerData.Rune)liRune);
            Texture2D lOTexture = mORuneTextures[liRune];

            if (lbOwned)
                liOwned++;

            mORunes[liRune].enabled = lbOwned && lOTexture != null;

            if (!mORunes[liRune].enabled)
                continue;

            float lfX = mOTabletRect.xMin + ((RuneLeft + ((liRune % RuneColumns) * RunePitchX)) * liScale);
            float lfRowTop = mOTabletRect.yMax - ((RuneTop + ((liRune / RuneColumns) * RunePitchY)) * liScale);

            fSetRect(mORunes[liRune].rectTransform, lfX, lfRowTop - (lOTexture.height * liScale),
                lOTexture.width * liScale, lOTexture.height * liScale);
        }

        mOTitleRight.text = liOwned + " of " + UWPlayerData.RuneCount;

        // The hollow with the chosen runes.
        float lfHollowTop = pfTop - ((TabletTop + UWTextures.PanelHeight + HollowGap) * liScale);

        mOHollowRect = new Rect(pfLeft + (((PanelWidth - HollowWidth) / 2) * liScale), lfHollowTop - (HollowHeight * liScale),
            HollowWidth * liScale, HollowHeight * liScale);
        fSetRect(mOHollow.rectTransform, mOHollowRect.x, mOHollowRect.y, mOHollowRect.width, mOHollowRect.height);

        IReadOnlyList<int> lOShelf = lORunes != null ? lORunes.SelectedRunes : null;
        int liSequence = SequenceOf(lOShelf);
        bool lbSpell = UWRunicMagic.TryGetSpell(liSequence, out UWRunicMagic.Spell lOSpell);
        bool lbHover = Mouse.current != null && UWModernPointer.IsFree && mOHollowRect.Contains(Mouse.current.position.ReadValue());

        for (int liAt = 0; liAt < mOHollowRunes.Length; liAt++)
        {
            int liRune = lOShelf != null && liAt < lOShelf.Count ? lOShelf[liAt] : -1;
            Texture2D lOTexture = liRune >= 0 && liRune < mORuneTextures.Length ? mORuneTextures[liRune] : null;

            mOHollowRunes[liAt].enabled = lOTexture != null;

            if (lOTexture != null)
            {
                mOHollowRunes[liAt].texture = lOTexture;
                fSetRect(mOHollowRunes[liAt].rectTransform, mOHollowRect.x + ((3 + (liAt * HollowRunePitch)) * liScale),
                    mOHollowRect.yMax - ((3 + lOTexture.height) * liScale), lOTexture.width * liScale, lOTexture.height * liScale);
            }
        }

        // The frame: gold under the pointer when it holds a spell - take it by it.
        Color lOFrame = lbSpell && (lbHover || mbDragging) ? msGold : msRule;

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
            bool lbCastable = HasRunes(liSequence) && lOSpell.Level <= liCircle;

            mOSpellName.text = SpellName(liSequence);
            mOSpellName.color = lbCastable ? msText : msGrey;
            mOSpellCircle.text = "Circle " + lOSpell.Level + "  -  " + lOSpell.ManaCost + " mana";
            mOHint.text = lOSpell.Level > liCircle ? "above your circle" : "drag onto the bar, click to cast";
        }
        else
        {
            mOSpellName.text = lOShelf != null && lOShelf.Count > 0 ? "Not a spell" : "No runes chosen";
            mOSpellName.color = msGrey;
            mOSpellCircle.text = string.Empty;
            mOHint.text = "click runes on the shelf";
        }

        fSetRect(mOSpellName.rectTransform, pfLeft, lfInfoTop - (9 * liScale), lfWidth, 9 * liScale);
        fSetRect(mOSpellCircle.rectTransform, pfLeft, lfInfoTop - (16 * liScale), lfWidth, 7 * liScale);
        fSetRect(mOHint.rectTransform, pfLeft, lfInfoTop - (24 * liScale), lfWidth, 7 * liScale);
    }

    private void fLayoutSpells(float pfLeft, float pfTop, float pfBottom, int piFont, int piSmall)
    {
        float liScale = miScale;
        int liCircle = CharacterCircle;

        mOTitle.text = "Spells";
        mOTitleRight.text = liCircle > 0 ? "up to circle " + liCircle : string.Empty;

        if (mOEntries.Count == 0)
            fBuildEntries();

        mOListRect = new Rect(pfLeft + (UWModernHudArt.LeatherLeft * liScale), pfBottom + (UWModernHudArt.LeatherBottom * liScale),
            (PanelWidth - UWModernHudArt.LeatherLeft - UWModernHudArt.LeatherRight) * liScale,
            pfTop - (ListTop * liScale) - (pfBottom + (UWModernHudArt.LeatherBottom * liScale)));
        fSetRect(mOListViewport, mOListRect.x, mOListRect.y, mOListRect.width, Mathf.Max(0f, mOListRect.height));

        float lfViewRows = mOListRect.height / liScale;

        mfScroll = Mathf.Clamp(mfScroll, 0f, Mathf.Max(0f, mfListRows - lfViewRows));

        float lfContentTop = mOListRect.height;
        float lfInner = mOListRect.width;
        Vector2 lOPointer = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
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
                lOEntry.Name.color = lOEntry.Circle <= liCircle ? msDim : msGrey;
                fSetRect(lOEntry.Name.rectTransform, lfX, lfTop - (8 * liScale), lfInner, 7 * liScale);
                fSetRect(lOEntry.Rule.rectTransform, lfX, lfTop - (9 * liScale), lfInner - (4 * liScale), Mathf.Max(2f, liScale * 0.5f));
                continue;
            }

            bool lbOwned = lOPlayer != null && HasRunes(lOEntry.Sequence);
            bool lbCastable = lbOwned && lOEntry.Spell.Level <= liCircle;
            float lfTextX = 13 * liScale;

            lOEntry.Name.fontSize = piSmall + 1;
            lOEntry.Name.color = lbCastable ? msText : msGrey;
            fSetRect(lOEntry.Name.rectTransform, lfTextX, lfTop - (6.5f * liScale), lfInner - lfTextX, 6 * liScale);

            lOEntry.Runes.fontSize = Mathf.Max(8, piSmall - 1);
            lOEntry.Runes.color = lbCastable ? msRunesText : msGrey;
            fSetRect(lOEntry.Runes.rectTransform, lfTextX, lfTop - (11.5f * liScale), lfInner - lfTextX, 5 * liScale);

            if (lOEntry.Icon != null)
            {
                lOEntry.Icon.enabled = true;
                lOEntry.Icon.color = lbCastable ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
                fSetRect(lOEntry.Icon.rectTransform, lfX, lfTop - (10.5f * liScale), 9 * liScale, 9 * liScale);
            }

            if (lOEntry == lOHovered || (mbDragging && !mbPressHollow && lOEntry.Sequence == miPressSequence))
            {
                mORowHighlight.enabled = true;
                fSetRect(mORowHighlight.rectTransform, liScale, lfTop - (RowRows * liScale), lfInner - (2 * liScale), RowRows * liScale);
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

                lOHeader.Name = fCreateText(mOListContent, "Circle", TextAnchor.LowerLeft, msDim);
                lOHeader.Name.text = "Circle " + liCircle + "  -  " + (liCircle * 3) + " mana";
                lOHeader.Rule = fCreateImage(mOListContent, "Rule", msRule);
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

            lOEntry.Name = fCreateText(mOListContent, "Spell", TextAnchor.MiddleLeft, msText);
            lOEntry.Name.text = UWRunicMagic.GetName(lOSpell, lOData.Strings);
            lOEntry.Runes = fCreateText(mOListContent, "Runes", TextAnchor.MiddleLeft, msRunesText);
            lOEntry.Runes.text = UWRunicMagic.DescribeRunes(lOSpell.RuneSequence);

            UWTexture lOIcon = SpellIcon(lOSpell.RuneSequence);

            if (lOIcon != null)
            {
                lOEntry.Icon = fCreateRawImage(mOListContent, "Icon");
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
        bool lbShow = mbDragging && miPressSequence >= 0 && Mouse.current != null;

        mOGhostCanvas.enabled = lbShow;

        if (!lbShow)
            return;

        float liScale = miScale;
        float lfSlot = UWModernHudArt.SlotSize * liScale;
        Vector2 lOPointer = Mouse.current.position.ReadValue();
        UWTexture lOIcon = SpellIcon(miPressSequence);

        fSetRect(mOGhostBack.rectTransform, lOPointer.x - (lfSlot * 0.5f), lOPointer.y - (lfSlot * 0.5f), lfSlot, lfSlot);

        mOGhostIcon.enabled = lOIcon != null;
        mOGhostText.enabled = lOIcon == null;

        if (lOIcon != null)
        {
            Texture2D lOTexture = fGetIcon(lOIcon);

            mOGhostIcon.texture = lOTexture;
            fSetRect(mOGhostIcon.rectTransform, lOPointer.x - (lOTexture.width * liScale * 0.5f), lOPointer.y - (lOTexture.height * liScale * 0.5f),
                lOTexture.width * liScale, lOTexture.height * liScale);
        }
        else
        {
            mOGhostText.text = RuneLabel(miPressSequence);
            mOGhostText.fontSize = Mathf.Max(9, Mathf.RoundToInt(3.6f * liScale));
            fSetRect(mOGhostText.rectTransform, lOPointer.x - (lfSlot * 0.5f), lOPointer.y - (lfSlot * 0.5f), lfSlot, lfSlot);
        }
    }

    private void fSetFrame(Image[] pOLines, Rect pORect, Color pOColour)
    {
        float lfLine = Mathf.Max(2f, miScale * 0.6f);

        fSetRect(pOLines[0].rectTransform, pORect.xMin - lfLine, pORect.yMax, pORect.width + (2 * lfLine), lfLine);
        fSetRect(pOLines[1].rectTransform, pORect.xMin - lfLine, pORect.yMin - lfLine, pORect.width + (2 * lfLine), lfLine);
        fSetRect(pOLines[2].rectTransform, pORect.xMin - lfLine, pORect.yMin, lfLine, pORect.height);
        fSetRect(pOLines[3].rectTransform, pORect.xMax, pORect.yMin, lfLine, pORect.height);

        foreach (Image lOLine in pOLines)
            lOLine.color = pOColour;
    }

    private Texture2D fGetIcon(UWTexture pOSource)
    {
        if (!mOIconTextures.TryGetValue(pOSource, out Texture2D lOIcon) || lOIcon == null)
        {
            lOIcon = UWIconTextureBuilder.Build(pOSource, mOUi.TextureFilterMode);
            mOIconTextures[pOSource] = lOIcon;
        }

        return lOIcon;
    }

    private void fEnsureArt()
    {
        if (miArtVersion == UWColourVision.Version && mOTabletTexture != null)
            return;

        miArtVersion = UWColourVision.Version;
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

        mOHandleTexture = UWModernHudArt.BuildLeatherTab(lOTextures, HandleWidth + UWModernHudArt.LeatherLeft, HandleHeight, leFilter);
        mOTabTexture = UWModernHudArt.BuildLeather(lOTextures, TabHeaderWidth, TabHeaderHeight, leFilter);
        mOPinTexture = UWModernHudArt.BuildLeather(lOTextures, PinWidth, TabHeaderHeight, leFilter);
        mOPinIconTexture = UWModernPanel.BuildPinIcon(48);
        mOGhostTexture = UWModernHudArt.BuildSlotCircle(lOTextures, leFilter);

        List<UWTexture> lOPanels = lOTextures.GetTexturesByType(UWTexture.TextureTypes.PANELS);

        if (lOPanels != null && lOPanels.Count > 1)
        {
            mOTabletTexture = new Texture2D(lOPanels[1].Width, lOPanels[1].Height, TextureFormat.RGBA32, false);
            mOTabletTexture.name = "UWModernRunePanel shelf";
            mOTabletTexture.filterMode = leFilter;
            mOTabletTexture.wrapMode = TextureWrapMode.Clamp;
            mOTabletTexture.SetPixels32(UWGameUI.fGetTextureInvert(lOPanels[1]));
            mOTabletTexture.Apply(false, false);
        }

        mORuneTextures = new Texture2D[UWPlayerData.RuneCount];

        for (int liRune = 0; liRune < mORuneTextures.Length; liRune++)
        {
            UWTexture lOSource = lOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, UWPlayerData.FirstRuneObjectId + liRune);

            mORuneTextures[liRune] = lOSource != null ? UWIconTextureBuilder.Build(lOSource, leFilter) : null;
            mORunes[liRune].texture = mORuneTextures[liRune];
        }

        mOHandle.texture = mOHandleTexture;
        mOPin.texture = mOPinTexture;
        mOPinIcon.texture = mOPinIconTexture;
        mOTablet.texture = mOTabletTexture;
        mOTablet.enabled = mOTabletTexture != null;
        mOGhostBack.texture = mOGhostTexture;

        foreach (RawImage lOTab in mOTabBacks)
            lOTab.texture = mOTabTexture;

        mOBackSize = Vector2Int.zero;
    }

    private static void fSetRect(RectTransform pORect, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        pORect.anchoredPosition = new Vector2(pfX, pfY);
        pORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        GameObject lORoot = new GameObject("Modern rune panel", typeof(Canvas), typeof(CanvasScaler));
        lORoot.transform.SetParent(transform, false);

        mOCanvas = lORoot.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mOCanvas.sortingOrder = 41;

        CanvasScaler lOScaler = lORoot.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        lOScaler.scaleFactor = 1f;

        mORoot = (RectTransform)lORoot.transform;

        mOTabBacks = new RawImage[2];
        mOTabTexts = new Text[2];

        string[] lsTabs = { "Runes", "Spells" };

        for (int liTab = 0; liTab < 2; liTab++)
        {
            mOTabBacks[liTab] = fCreateRawImage(mORoot, "Tab " + lsTabs[liTab]);
            UWPixelArtUI.Apply(mOTabBacks[liTab]);
            mOTabTexts[liTab] = fCreateText(mORoot, lsTabs[liTab], TextAnchor.MiddleCenter, msText);
            mOTabTexts[liTab].text = lsTabs[liTab];
        }

        mOPin = fCreateRawImage(mORoot, "Pin");
        UWPixelArtUI.Apply(mOPin);
        mOPinIcon = fCreateRawImage(mORoot, "Pin icon");

        mOBack = fCreateRawImage(mORoot, "Leather");
        UWPixelArtUI.Apply(mOBack);

        // The handle is the character panel's, mirrored: its open side joins the panel.
        mOHandle = fCreateRawImage(mORoot, "Handle");
        UWPixelArtUI.Apply(mOHandle);
        mOHandle.uvRect = new Rect(1f, 0f, -1f, 1f);
        mOHandleText = fCreateText(mORoot, "Handle label", TextAnchor.MiddleCenter, msText);
        mOHandleText.text = "Runes";

        mOTitle = fCreateText(mORoot, "Title", TextAnchor.MiddleLeft, msText);
        mOTitleRight = fCreateText(mORoot, "Title right", TextAnchor.MiddleRight, msDim);

        // The Runes tab.
        mORunesContent = fCreateGroup(mORoot, "Runes");
        mOTablet = fCreateRawImage(mORunesContent, "Rune shelf");
        UWPixelArtUI.Apply(mOTablet);

        mORunes = new RawImage[UWPlayerData.RuneCount];

        for (int liRune = 0; liRune < mORunes.Length; liRune++)
        {
            mORunes[liRune] = fCreateRawImage(mORunesContent, UWRunicMagic.RuneNames[liRune]);
            UWIconPalette.ApplySmooth(mORunes[liRune]);
        }

        mOHollow = fCreateImage(mORunesContent, "Hollow", msHollowFill);
        mOHollowFrame = new Image[4];

        for (int liLine = 0; liLine < mOHollowFrame.Length; liLine++)
            mOHollowFrame[liLine] = fCreateImage(mORunesContent, "Frame", msRule);

        mOHollowRunes = new RawImage[UWHudRunes.SelectedRuneCount];

        for (int liAt = 0; liAt < mOHollowRunes.Length; liAt++)
        {
            mOHollowRunes[liAt] = fCreateRawImage(mORunesContent, "Chosen rune");
            UWIconPalette.ApplySmooth(mOHollowRunes[liAt]);
        }

        mOSpellName = fCreateText(mORunesContent, "Spell", TextAnchor.MiddleCenter, msText);
        mOSpellCircle = fCreateText(mORunesContent, "Circle", TextAnchor.MiddleCenter, msDim);
        mOHint = fCreateText(mORunesContent, "Hint", TextAnchor.MiddleCenter, msDim);

        // The Spells tab: a clipped viewport with the rows inside.
        GameObject lOViewport = new GameObject("Spell list", typeof(RectTransform), typeof(RectMask2D));
        lOViewport.transform.SetParent(mORoot, false);
        mOListViewport = (RectTransform)lOViewport.transform;
        mOListViewport.anchorMin = Vector2.zero;
        mOListViewport.anchorMax = Vector2.zero;
        mOListViewport.pivot = Vector2.zero;

        mOListContent = fCreateGroup(mOListViewport, "Rows");
        mORowHighlight = fCreateImage(mOListContent, "Hover", new Color(msGold.r, msGold.g, msGold.b, 0.18f));

        // The dragged spell over everything, the action bar included.
        GameObject lOGhost = new GameObject("Dragged spell", typeof(RectTransform), typeof(Canvas));
        lOGhost.transform.SetParent(mORoot, false);
        mOGhostCanvas = lOGhost.GetComponent<Canvas>();
        mOGhostCanvas.overrideSorting = true;
        mOGhostCanvas.sortingOrder = 43;

        RectTransform lOGhostRect = (RectTransform)lOGhost.transform;
        lOGhostRect.anchorMin = Vector2.zero;
        lOGhostRect.anchorMax = Vector2.zero;
        lOGhostRect.pivot = Vector2.zero;

        mOGhostBack = fCreateRawImage(lOGhostRect, "Slot");
        mOGhostBack.enabled = true;
        mOGhostBack.color = new Color(1f, 1f, 1f, 0.85f);
        UWPixelArtUI.Apply(mOGhostBack);
        mOGhostIcon = fCreateRawImage(lOGhostRect, "Icon");
        UWIconPalette.ApplySmooth(mOGhostIcon);
        mOGhostText = fCreateText(lOGhostRect, "Runes", TextAnchor.MiddleCenter, msRunesText);
        mOGhostCanvas.enabled = false;
    }

    private static RectTransform fCreateGroup(Transform pOParent, string psName)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        return lORect;
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
        lOImage.enabled = false;

        return lOImage;
    }

    private static Image fCreateImage(Transform pOParent, string psName, Color pOColour)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        Image lOImage = lOObject.GetComponent<Image>();
        lOImage.color = pOColour;
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
