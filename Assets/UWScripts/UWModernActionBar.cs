using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S ACTION BAR (decided per user 2026-10-03, look per mockup the same day):
/// ten slots at the bottom centre on the keys 1 to 9 and 0.
///
///   - A slot is bound to THE SINGLE PIECE, not to a kind of thing: the piece stays where it lies
///     in the inventory, the slot only points at it. It shows nothing while the piece is not
///     carried (used up, dropped), dimmed while it hangs on the pointer. Merged into a stack, the
///     slot follows the stack (UWInventoryModel.ItemMerged).
///   - It takes weapons, what is used at once (potions, food, scrolls, wands, the fishing pole,
///     lights ...) and the tools that act on something in the world (the pole, the rock hammer,
///     keys ...); NOT armour and rings, containers, nor what is applied to another inventory item
///     (the oil flask, the anvil) - per user.
///   - Its key, or a click on it with the free pointer: a weapon goes into the weapon hand (what
///     was there to the weapon's place, UWModernBags.EquipCarried) - and draws or puts away when
///     it is there already, marked with a ring while drawn; a tool acts on the crosshair's target
///     (UWItemDrag.UseItemOnCrosshair); anything else is used (UWItemDrag.UseCarriedItem).
///   - Filled by dropping a thing from the pointer onto a slot (UWModernBags; the thing goes
///     back to its place). With the free pointer a slot's link can be dragged to another slot
///     (they swap) or off the bar (the slot empties).
///   - Kept with the save game: each slot as the PATH of its piece in the inventory ("B3.1" =
///     backpack slot 3, inside it the second thing), written into UWR.json (UWHelpNotes) and
///     resolved after loading.
///   - SPELLS (stage 3, per user 2026-10-04): dragged from the rune panel (UWModernRunePanel) a
///     slot holds a rune sequence instead - its picture from SPELLS.GR where the spell has one,
///     else its runes; grey while it cannot be cast (a rune not in the bag, above the circle, too
///     little mana). Its key casts it by the original's rules (UWHudRunes.CastSequence) without
///     touching the hollow; saved as "S" and the sequence.
/// </summary>
public class UWModernActionBar : MonoBehaviour
{
    public static UWModernActionBar Instance { get; private set; }

    private const int SlotCount = UWControls.ModernSlotCount;

    /// <summary>The layout in original pixels, as the mockup.</summary>
    private const int Pad = 4;

    private const int SlotGap = 2;

    private const int Margin = 4;

    private const float ClickMoveThreshold = 6f;

    private const float DimAlpha = 0.35f;

    private const int LastMeleeWeaponId = 0x0F;

    private const int FirstLauncherId = 0x18;

    private const int LastLauncherId = 0x1F;

    private static readonly Color msGold = new Color(0.925f, 0.77f, 0.44f, 1f);

    private static readonly Color msKey = new Color(1f, 0.94f, 0.78f);

    private UWGameUI mOUi;

    private UWControlScheme mOScheme;

    private Interaction mOInteraction;

    private Font mOFont;

    private Canvas mOCanvas;

    private RawImage mOBack;

    private RawImage[] mOCircles;

    private RawImage[] mOIcons;

    private RawImage[] mORings;

    private Text[] mOKeys;

    private Text[] mOCounts;

    private RawImage mOLinkIcon;

    private Texture2D mOBackTexture;

    private Texture2D mOSlotTexture;

    private Texture2D mORingTexture;

    /// <summary>THE GAMEPAD'S SLOT (UWGamepad): LB and RB pick it, up on the d-pad uses it; its
    /// pale ring shows while the gamepad was used last.</summary>
    private int miPadSlot;

    private RawImage mOPadRing;

    /// <summary>The gamepad's hints at the bar (per user, 2026-10-08): LB and RB at its ends, the
    /// slot's button above the chosen slot - while the pad is in use and the hints are on; the
    /// digits go meanwhile.</summary>
    private RawImage mOPadPrevious;

    private RawImage mOPadNext;

    private RawImage mOPadUse;

    private Texture2D mOPadRingTexture;

    private static readonly Color msPadRing = new Color(0.85f, 0.92f, 1f, 1f);

    private int miArtVersion = -1;

    private readonly Dictionary<UWTexture, Texture2D> mOIconTextures = new Dictionary<UWTexture, Texture2D>();

    private readonly UWObject[] mOSlots = new UWObject[SlotCount];

    /// <summary>The rune sequence a slot holds instead of a piece, -1 for none.</summary>
    private readonly int[] miSpells = new int[SlotCount];

    private Text[] mOSpellTexts;

    private Text mOLinkText;

    /// <summary>A spell link being dragged, -1 for none (mOLink is a piece's).</summary>
    private int miLinkSpell = -1;

    private readonly Rect[] mOSlotRects = new Rect[SlotCount];

    private float miScale = 1f;

    /// <summary>Where the bar is on the screen (pixels, bottom-left origin), empty while hidden -
    /// the bag windows keep clear of it (UWModernBags).</summary>
    public Rect ScreenRect { get; private set; }

    private int miLoadCount = -1;

    /// <summary>The inventory load the paths were last resolved against.</summary>
    private int miModelLoadCount = -1;

    private UWInventoryModel mOResolvedModel;

    private UWInventoryModel mOWatched;

    /// <summary>A press on a slot with the free pointer: a click uses, a drag takes the link.</summary>
    private int miPressSlot = -1;

    private Vector2 mOPressStart;

    /// <summary>A link being dragged: the piece and the slot it came from.</summary>
    private UWObject mOLink;

    private int miLinkFrom = -1;

    /// <summary>The frame a thing was dropped onto the bar (UWModernBags, TryBind): that click must
    /// not use it as well, whichever Update ran first.</summary>
    private int miDropFrame = -1;

    private void Awake()
    {
        Instance = this;

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
            miSpells[liSlot] = -1;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (mOWatched != null)
            mOWatched.ItemMerged -= fOnMerged;

        foreach (Texture2D lOTexture in new[] { mOBackTexture, mOSlotTexture, mORingTexture, mOPadRingTexture })
        {
            if (lOTexture != null)
                Destroy(lOTexture);
        }

        foreach (Texture2D lOIcon in mOIconTextures.Values)
            Destroy(lOIcon);
    }

    private void Start()
    {
        mOUi = GetComponent<UWGameUI>();
        mOInteraction = GetComponent<Interaction>();
        mOScheme = GetComponentInParent<UWControlScheme>();
        mOFont = Resources.Load<Font>("Fonts/LexendExa");
    }

    private bool fIsShown()
    {
        return mOScheme != null && mOScheme.Current == UWControlScheme.SchemeEnum.Modern
            && UWModernHud.Instance != null && UWModernHud.Instance.IsShowing
            && mOUi != null && mOUi.mOUWData != null && mOUi.mOInventory != null;
    }

    // ------------------------------------------------- Binding

    /// <summary>What may go onto the bar: not armour, rings or containers, nor what is applied to
    /// another inventory item (per user, 2026-10-03).</summary>
    public static bool CanBind(UWObject pOItem)
    {
        if (pOItem == null)
            return false;

        if (UWArmorItemMap.TryGet(pOItem.ID, out UWArmorItemMap.Entry _) || UWArmorItemMap.Fits(pOItem, UWArmorItemMap.BodySlot.LeftRing))
            return false;

        if (pOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
            return false;

        return pOItem.ID != UWObjectMechanics.OilFlaskObjectId && pOItem.ID != UWObjectMechanics.AnvilObjectId;
    }

    /// <summary>The bar slot under a screen point, while the bar shows.</summary>
    public bool TryGetSlotAt(Vector2 pOPointer, out int piSlot)
    {
        piSlot = -1;

        if (!fIsShown())
            return false;

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            if (mOSlotRects[liSlot].Contains(pOPointer))
            {
                piSlot = liSlot;
                return true;
            }
        }

        return false;
    }

    /// <summary>Binds a slot to a piece; a piece already on the bar moves there (its old slot
    /// empties). Refuses what does not belong on the bar.</summary>
    /// <summary>Q over the bar (Interaction.fUpdateModernActions): looks at the thing in the slot
    /// under the pointer. A spell or an empty slot: false.</summary>
    public bool TryLookAt(Vector2 pOPointer)
    {
        if (!TryGetSlotAt(pOPointer, out int liSlot) || miSpells[liSlot] >= 0)
            return false;

        UWObject lOItem = mOSlots[liSlot];

        if (lOItem == null || !fIsCarried(lOItem) || UWModernBags.Instance == null)
            return false;

        UWModernBags.Instance.LookAt(lOItem);
        return true;
    }

    /// <summary>E tapped over the bar (Interaction.fUpdateModernUseKey): the slot under the pointer
    /// is used as its number key uses it. False when no slot is there.</summary>
    public bool TryUseAt(Vector2 pOPointer)
    {
        if (!TryGetSlotAt(pOPointer, out int liSlot))
            return false;

        fUse(liSlot);
        return true;
    }

    public bool TryBind(int piSlot, UWObject pOItem)
    {
        if (piSlot < 0 || piSlot >= SlotCount || pOItem == null)
            return false;

        if (!CanBind(pOItem))
        {
            if (mOInteraction != null)
                mOInteraction.AddMessage("That does not go on the action bar.");

            return false;
        }

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            if (mOSlots[liSlot] == pOItem)
                mOSlots[liSlot] = null;
        }

        mOSlots[piSlot] = pOItem;
        miSpells[piSlot] = -1;
        miDropFrame = Time.frameCount;

        return true;
    }

    /// <summary>Binds a slot to a spell (its rune sequence) - dragged from the rune panel; the
    /// spell already on the bar moves there.</summary>
    public bool TryBindSpell(int piSlot, int piSequence)
    {
        if (piSlot < 0 || piSlot >= SlotCount || !UWRunicMagic.TryGetSpell(piSequence, out UWRunicMagic.Spell _))
            return false;

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            if (miSpells[liSlot] == piSequence)
                miSpells[liSlot] = -1;
        }

        mOSlots[piSlot] = null;
        miSpells[piSlot] = piSequence;
        miDropFrame = Time.frameCount;

        return true;
    }

    /// <summary>A piece merged into a stack: its slots follow the stack.</summary>
    private void fOnMerged(UWObject pOFrom, UWObject pOInto)
    {
        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            if (mOSlots[liSlot] == pOFrom)
                mOSlots[liSlot] = pOInto;
        }
    }

    private bool fIsCarried(UWObject pOItem)
    {
        if (pOItem == null || mOUi.mOInventory == null)
            return false;

        foreach (UWObject lOItem in mOUi.mOInventory.Model.EnumerateAll())
        {
            if (lOItem == pOItem)
                return true;
        }

        return false;
    }

    private static bool fIsWeapon(UWObject pOItem)
    {
        return pOItem.ID <= LastMeleeWeaponId || (pOItem.ID >= FirstLauncherId && pOItem.ID <= LastLauncherId);
    }

    // ------------------------------------------------- Saving and loading

    /// <summary>The bar's slots as inventory paths and object numbers into UWHelpNotes for saving
    /// (UWHelpNotes.SaveTo). A piece not carried at this moment is left out. Not resolved yet
    /// since the last load: what was read stays.</summary>
    public static void CaptureSlots()
    {
        UWModernActionBar lOBar = Instance;

        if (lOBar == null || lOBar.mOUi == null || lOBar.mOUi.mOInventory == null
            || lOBar.miLoadCount != UWHelpNotes.LoadCount)
            return;

        string[] lsPaths = new string[SlotCount];
        int[] liIds = new int[SlotCount];
        UWInventoryModel lOModel = lOBar.mOUi.mOInventory.Model;

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            if (lOBar.miSpells[liSlot] >= 0)
            {
                lsPaths[liSlot] = SpellPathPrefix + lOBar.miSpells[liSlot];
                liIds[liSlot] = -1;
                continue;
            }

            UWObject lOItem = lOBar.mOSlots[liSlot];
            string lsPath = UWInventoryPaths.PathOf(lOModel, lOItem);

            lsPaths[liSlot] = lsPath ?? string.Empty;
            liIds[liSlot] = lsPath != null ? lOItem.ID : -1;
        }

        UWHelpNotes.ActionBarPaths = lsPaths;
        UWHelpNotes.ActionBarIds = liIds;
    }

    /// <summary>A spell's entry in UWR.json: "S" and its rune sequence.</summary>
    private const string SpellPathPrefix = "S";

    /// <summary>After a load or a new character: the paths read into pieces once - for a save
    /// game only after its inventory has come in (the loads are not in a fixed order).</summary>
    private void fResolveIfLoaded()
    {
        if (miLoadCount == UWHelpNotes.LoadCount)
            return;

        UWInventoryModel lOModel = mOUi.mOInventory.Model;
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        string[] lsPaths = UWHelpNotes.ActionBarPaths ?? new string[0];
        bool lbAnyPath = System.Array.Exists(lsPaths, lsPath => !string.IsNullOrEmpty(lsPath));

        if (lbAnyPath && lOModel == mOResolvedModel && lOModel.LoadCount == miModelLoadCount)
            return;

        miLoadCount = UWHelpNotes.LoadCount;
        mOResolvedModel = lOModel;
        miModelLoadCount = lOModel.LoadCount;

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            miSpells[liSlot] = -1;

            // A spell: its sequence, if it still is one.
            if (liSlot < lsPaths.Length && lsPaths[liSlot] != null && lsPaths[liSlot].StartsWith(SpellPathPrefix))
            {
                mOSlots[liSlot] = null;

                if (int.TryParse(lsPaths[liSlot].Substring(SpellPathPrefix.Length), out int liSequence)
                    && UWRunicMagic.TryGetSpell(liSequence, out UWRunicMagic.Spell _)
                    && System.Array.IndexOf(miSpells, liSequence, 0, liSlot) < 0)
                    miSpells[liSlot] = liSequence;

                continue;
            }

            UWObject lOItem = liSlot < lsPaths.Length && !string.IsNullOrEmpty(lsPaths[liSlot])
                ? UWInventoryPaths.ResolveChecked(lOModel, lsPaths[liSlot], UWHelpNotes.IdAt(UWHelpNotes.ActionBarIds, liSlot),
                    UWHelpNotes.IsStale, lOLoader != null && lOLoader.CurrentLevel != null ? lOLoader.CurrentLevel.Masterlist : null)
                : null;

            // The same piece twice (two entries found the same one): only the first slot keeps it.
            if (lOItem != null && System.Array.IndexOf(mOSlots, lOItem, 0, liSlot) >= 0)
                lOItem = null;

            mOSlots[liSlot] = CanBind(lOItem) ? lOItem : null;
        }
    }

    // ------------------------------------------------- Using

    /// <summary>A slot's key or click: the weapon into the hand (or drawn and put away), a tool
    /// onto the crosshair's target, anything else used.</summary>
    private void fUse(int piSlot)
    {
        if (miSpells[piSlot] >= 0)
        {
            fCast(miSpells[piSlot]);
            return;
        }

        UWObject lOItem = mOSlots[piSlot];
        UWInventory lOInventory = mOUi.mOInventory;

        if (lOItem == null || lOInventory.CursorItem != null || !fIsCarried(lOItem) || mOInteraction == null)
            return;

        if (fIsWeapon(lOItem))
        {
            if (lOInventory.GetEquipped(lOInventory.MainHandSlot) == lOItem)
                mOInteraction.ToggleCombatMode();
            else if (UWModernBags.Instance != null)
                UWModernBags.Instance.EquipCarried(lOItem, lOInventory.MainHandSlot);

            return;
        }

        UWItemDrag lODrag = UWScene.ItemDrag;

        if (lODrag == null)
            return;

        if (lODrag.NeedsWorldTarget(lOItem))
            lODrag.UseItemOnCrosshair(lOItem);
        else
            lODrag.UseCarriedItem(lOItem);
    }

    /// <summary>A spell slot: cast by the original's rules - unless a rune is missing from the bag,
    /// which the original's hollow cannot even hold.</summary>
    private void fCast(int piSequence)
    {
        if (mOInteraction == null || mOUi.Runes == null)
            return;

        if (!UWModernRunePanel.HasRunes(piSequence))
        {
            mOInteraction.AddMessage("A rune of that spell is not in your bag.");
            return;
        }

        mOUi.Runes.CastSequence(piSequence);
    }

    // ------------------------------------------------- Input

    private void Update()
    {
        if (!fIsShown())
        {
            fDropLink();
            miPressSlot = -1;
            return;
        }

        fWatchModel();
        fResolveIfLoaded();

        if (UWModernHud.Instance.IsOpen || UWControls.IsTextEntryActive || UWHelpWindow.BlocksGameKeys
            || mOUi.IsMapVisible)
            return;

        UWControls lOControls = mOScheme.Controls;

        if (lOControls == null)
            return;

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            if (lOControls.Player.ModernSlots[liSlot].WasPressedThisFrame())
                fUse(liSlot);
        }

        // Not where the gamepad's pointer takes the button (UWGamepadPointer.Takes), nor in a
        // conversation, where the d-pad steps the answers (UWConversationScreen).
        bool lbPadFree = !UWConversationScreen.IsAnyOpen;

        if (lbPadFree && lOControls.Player.PadSlotPrevious.WasPressedThisFrame() && !UWGamepadPointer.Takes(lOControls.Player.PadSlotPrevious))
            fStepPadSlot(-1);
        else if (lbPadFree && lOControls.Player.PadSlotNext.WasPressedThisFrame() && !UWGamepadPointer.Takes(lOControls.Player.PadSlotNext))
            fStepPadSlot(1);

        if (lbPadFree && lOControls.Player.PadSlotUse.WasPressedThisFrame() && !UWGamepadPointer.Takes(lOControls.Player.PadSlotUse))
            fUse(miPadSlot);

        if (!mOScheme.IsPointerFree || Mouse.current == null)
            return;

        Vector2 lOPointer = Mouse.current.position.ReadValue();
        bool lbEmptyPointer = mOUi.mOInventory.CursorItem == null;

        if (lOControls.Player.CursorDrag.WasPressedThisFrame() && lbEmptyPointer && mOLink == null && miLinkSpell < 0
            && miDropFrame != Time.frameCount && TryGetSlotAt(lOPointer, out int liPressed))
        {
            miPressSlot = liPressed;
            mOPressStart = lOPointer;
        }

        // Held and moved off: the slot's link goes onto the pointer.
        if (miPressSlot >= 0 && mOLink == null && miLinkSpell < 0 && lOControls.Player.CursorDrag.IsPressed()
            && Vector2.Distance(lOPointer, mOPressStart) >= ClickMoveThreshold
            && (mOSlots[miPressSlot] != null || miSpells[miPressSlot] >= 0))
        {
            mOLink = mOSlots[miPressSlot];
            miLinkSpell = miSpells[miPressSlot];
            miLinkFrom = miPressSlot;
            mOSlots[miPressSlot] = null;
            miSpells[miPressSlot] = -1;
        }

        if (lOControls.Player.CursorDrag.WasReleasedThisFrame())
        {
            if (mOLink != null || miLinkSpell >= 0)
            {
                // Onto a slot: they swap; anywhere else the link is gone.
                if (TryGetSlotAt(lOPointer, out int liTarget))
                {
                    mOSlots[miLinkFrom] = mOSlots[liTarget];
                    miSpells[miLinkFrom] = miSpells[liTarget];
                    mOSlots[liTarget] = mOLink;
                    miSpells[liTarget] = miLinkSpell;
                }

                mOLink = null;
                miLinkSpell = -1;
                miLinkFrom = -1;
            }
            else if (miPressSlot >= 0 && TryGetSlotAt(lOPointer, out int liReleased) && liReleased == miPressSlot)
            {
                fUse(miPressSlot);
            }

            miPressSlot = -1;
        }

        // The right button on a slot uses it too, as on the bags.
        if (lOControls.Player.Interact.WasPressedThisFrame() && lbEmptyPointer
            && UWModernPointer.SwitchedFrame != Time.frameCount && TryGetSlotAt(lOPointer, out int liRight))
            fUse(liRight);
    }

    /// <summary>The gamepad's shoulders: the next slot that holds something that way round, or
    /// simply the next one while the bar is empty.</summary>
    private void fStepPadSlot(int piStep)
    {
        for (int liTry = 1; liTry <= SlotCount; liTry++)
        {
            int liSlot = (((miPadSlot + (piStep * liTry)) % SlotCount) + SlotCount) % SlotCount;

            if (mOSlots[liSlot] != null || miSpells[liSlot] >= 0)
            {
                miPadSlot = liSlot;
                return;
            }
        }

        miPadSlot = (((miPadSlot + piStep) % SlotCount) + SlotCount) % SlotCount;
    }

    private static void fShowPadGlyph(RawImage pOImage, Texture2D pOGlyph, float pfX, float pfY, float pfSize)
    {
        pOImage.enabled = pOGlyph != null;

        if (pOGlyph == null)
            return;

        pOImage.texture = pOGlyph;
        fSetRect(pOImage.rectTransform, pfX, pfY, pfSize, pfSize);
    }

    /// <summary>A link dragged while the bar goes away goes back to its slot.</summary>
    private void fDropLink()
    {
        if (miLinkFrom >= 0 && mOSlots[miLinkFrom] == null && miSpells[miLinkFrom] < 0)
        {
            mOSlots[miLinkFrom] = mOLink;
            miSpells[miLinkFrom] = miLinkSpell;
        }

        mOLink = null;
        miLinkSpell = -1;
        miLinkFrom = -1;
    }

    /// <summary>Follows merges in the inventory model (a new character or load brings a new one).</summary>
    private void fWatchModel()
    {
        UWInventoryModel lOModel = mOUi.mOInventory.Model;

        if (lOModel == mOWatched)
            return;

        if (mOWatched != null)
            mOWatched.ItemMerged -= fOnMerged;

        mOWatched = lOModel;

        if (mOWatched != null)
            mOWatched.ItemMerged += fOnMerged;
    }

    // ------------------------------------------------- Drawing

    private void LateUpdate()
    {
        if (!fIsShown())
        {
            if (mOCanvas != null)
                mOCanvas.enabled = false;

            for (int liSlot = 0; liSlot < SlotCount; liSlot++)
                mOSlotRects[liSlot] = Rect.zero;

            ScreenRect = Rect.zero;

            return;
        }

        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;
        // Its own size and place (UWModernLayout, the layout editor).
        miScale = UWModernHud.PixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.ActionBar);

        fEnsureArt();

        float liScale = miScale;
        int liWidth = (Pad * 2) + (SlotCount * UWModernHudArt.SlotSize) + ((SlotCount - 1) * SlotGap);
        int liHeight = (Pad * 2) + UWModernHudArt.SlotSize;
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.ActionBar,
            new Rect(Mathf.Round((Screen.width - (liWidth * liScale)) * 0.5f), Margin * UWModernHud.PixelScale, liWidth * liScale, liHeight * liScale));
        float lfLeft = lOPlaced.x;
        float lfBottom = lOPlaced.y;

        UWModernLayout.Report(UWModernLayout.ElementEnum.ActionBar, lOPlaced);

        fSetRect(mOBack.rectTransform, lfLeft, lfBottom, liWidth * liScale, liHeight * liScale);
        ScreenRect = new Rect(lfLeft, lfBottom, liWidth * liScale, liHeight * liScale);

        UWInventory lOInventory = mOUi.mOInventory;
        UWObject lODrawn = mOInteraction != null && mOInteraction.IsCombatModeActive
            ? lOInventory.GetEquipped(lOInventory.MainHandSlot) : null;
        float lfSlot = UWModernHudArt.SlotSize * liScale;

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            float lfX = lfLeft + ((Pad + (liSlot * (UWModernHudArt.SlotSize + SlotGap))) * liScale);
            float lfY = lfBottom + (Pad * liScale);

            mOSlotRects[liSlot] = new Rect(lfX, lfY, lfSlot, lfSlot);
            fSetRect(mOCircles[liSlot].rectTransform, lfX, lfY, lfSlot, lfSlot);

            UWObject lOItem = mOSlots[liSlot];
            bool lbOnPointer = lOItem != null && lOItem == lOInventory.CursorItem;
            bool lbShown = lOItem != null && (lbOnPointer || fIsCarried(lOItem));

            if (miSpells[liSlot] >= 0)
                fShowSpell(mOIcons[liSlot], mOSpellTexts[liSlot], miSpells[liSlot], lfX, lfY);
            else
            {
                mOSpellTexts[liSlot].enabled = false;
                fShowIcon(mOIcons[liSlot], lbShown ? lOItem : null, lfX, lfY, lbOnPointer ? DimAlpha : 1f);
            }

            int liCount = lbShown ? UWItemDescriptions.GetStackCount(lOItem) : 1;

            mOCounts[liSlot].enabled = liCount > 1;
            mOCounts[liSlot].text = liCount.ToString();
            mOCounts[liSlot].fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));
            fSetRect(mOCounts[liSlot].rectTransform, lfX, lfY - liScale, lfSlot - liScale, lfSlot * 0.6f);

            mOKeys[liSlot].fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));
            fSetRect(mOKeys[liSlot].rectTransform, lfX + liScale, lfY + (lfSlot * 0.45f), lfSlot, lfSlot * 0.55f);

            // The drawn weapon gets its ring.
            bool lbDrawn = lbShown && lOItem == lODrawn;
            float lfRing = (UWModernHudArt.SlotSize + 2) * liScale;

            mORings[liSlot].enabled = lbDrawn;
            fSetRect(mORings[liSlot].rectTransform, lfX - liScale, lfY - liScale, lfRing, lfRing);
        }

        // The gamepad's slot, a pale ring a little outside the gold one.
        Rect lOPadSlot = mOSlotRects[miPadSlot];
        float lfPadRing = (UWModernHudArt.SlotSize + 4) * liScale;

        mOPadRing.enabled = UWGamepad.IsActive;
        fSetRect(mOPadRing.rectTransform, lOPadSlot.x - (2f * liScale), lOPadSlot.y - (2f * liScale), lfPadRing, lfPadRing);

        // The hints: LB and RB beside the bar, the use button over the chosen slot.
        bool lbHints = UWUserSettings.ShowsPadHints;
        float lfGlyph = Mathf.Round(10f * liScale);

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
            mOKeys[liSlot].enabled = !lbHints;

        fShowPadGlyph(mOPadPrevious, lbHints ? UWGlyphs.ForEntry("PadSlotPrevious", 0) : null,
            lfLeft - lfGlyph - liScale, lfBottom + (((liHeight * liScale) - lfGlyph) * 0.5f), lfGlyph);
        fShowPadGlyph(mOPadNext, lbHints ? UWGlyphs.ForEntry("PadSlotNext", 0) : null,
            lfLeft + (liWidth * liScale) + liScale, lfBottom + (((liHeight * liScale) - lfGlyph) * 0.5f), lfGlyph);
        fShowPadGlyph(mOPadUse, lbHints ? UWGlyphs.ForEntry("PadSlotUse", 0) : null,
            lOPadSlot.center.x - (lfGlyph * 0.5f), lOPadSlot.yMax + (2f * liScale), lfGlyph);

        // The link being dragged follows the pointer.
        mOLinkText.enabled = false;

        if ((mOLink != null || miLinkSpell >= 0) && Mouse.current != null)
        {
            Vector2 lOPointer = Mouse.current.position.ReadValue();

            if (miLinkSpell >= 0)
                fShowSpell(mOLinkIcon, mOLinkText, miLinkSpell, lOPointer.x - (lfSlot * 0.5f), lOPointer.y - (lfSlot * 0.5f));
            else
                fShowIcon(mOLinkIcon, mOLink, lOPointer.x - (lfSlot * 0.5f), lOPointer.y - (lfSlot * 0.5f), 1f);
        }
        else
        {
            mOLinkIcon.enabled = false;
        }
    }

    /// <summary>A spell in a slot: its SPELLS.GR picture where it has one, else its runes; grey
    /// while it cannot be cast now (UWModernRunePanel.CanCastNow).</summary>
    private void fShowSpell(RawImage pOImage, Text pOText, int piSequence, float pfSlotX, float pfSlotY)
    {
        bool lbCastable = UWModernRunePanel.CanCastNow(piSequence);
        UWTexture lOSource = UWModernRunePanel.SpellIcon(piSequence);
        float liScale = miScale;

        if (lOSource != null)
        {
            Texture2D lOIcon = fGetIcon(lOSource);

            pOText.enabled = false;
            pOImage.texture = lOIcon;
            pOImage.enabled = true;
            pOImage.color = lbCastable ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
            fSetRect(pOImage.rectTransform, pfSlotX + (((UWModernHudArt.SlotSize - lOIcon.width) / 2) * liScale),
                pfSlotY + (((UWModernHudArt.SlotSize - lOIcon.height) / 2) * liScale), lOIcon.width * liScale, lOIcon.height * liScale);
            return;
        }

        string lsLabel = UWModernRunePanel.RuneLabel(piSequence);
        int liLines = lsLabel.Split('\n').Length;

        pOImage.enabled = false;
        pOText.enabled = true;
        pOText.text = lsLabel;
        pOText.color = lbCastable ? new Color(0.77f, 0.84f, 1f) : new Color(0.55f, 0.50f, 0.42f);
        pOText.fontSize = Mathf.Max(8, Mathf.RoundToInt((liLines > 2 ? 3f : 3.6f) * liScale));
        pOText.lineSpacing = 0.85f;
        fSetRect(pOText.rectTransform, pfSlotX, pfSlotY, UWModernHudArt.SlotSize * liScale, UWModernHudArt.SlotSize * liScale);
    }

    private void fShowIcon(RawImage pOImage, UWObject pOItem, float pfSlotX, float pfSlotY, float pfAlpha)
    {
        UWTexture lOSource = pOItem != null ? pOItem.Icon ?? pOItem.Texture : null;

        if (lOSource == null)
        {
            pOImage.enabled = false;
            return;
        }

        Texture2D lOIcon = fGetIcon(lOSource);
        float liScale = miScale;

        pOImage.texture = lOIcon;
        pOImage.enabled = true;
        pOImage.color = new Color(1f, 1f, 1f, pfAlpha);
        fSetRect(pOImage.rectTransform, pfSlotX + (((UWModernHudArt.SlotSize - lOIcon.width) / 2) * liScale),
            pfSlotY + (((UWModernHudArt.SlotSize - lOIcon.height) / 2) * liScale), lOIcon.width * liScale, lOIcon.height * liScale);
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
        if (miArtVersion == UWColourVision.Version && mOBackTexture != null)
            return;

        miArtVersion = UWColourVision.Version;

        if (mOBackTexture != null)
            Destroy(mOBackTexture);

        if (mOSlotTexture != null)
            Destroy(mOSlotTexture);

        UWTextures lOTextures = mOUi.mOUWData.Textures;
        int liWidth = (Pad * 2) + (SlotCount * UWModernHudArt.SlotSize) + ((SlotCount - 1) * SlotGap);
        int liHeight = (Pad * 2) + UWModernHudArt.SlotSize;

        mOBackTexture = UWModernHudArt.BuildLeather(lOTextures, liWidth, liHeight, mOUi.TextureFilterMode);
        mOSlotTexture = UWModernHudArt.BuildSlotCircle(lOTextures, mOUi.TextureFilterMode);

        if (mORingTexture == null)
            mORingTexture = BuildRing(128, 60f, 6f);

        if (mOPadRingTexture == null)
            mOPadRingTexture = BuildRing(128, 60f, 5f, msPadRing);

        mOPadRing.texture = mOPadRingTexture;

        mOBack.texture = mOBackTexture;

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            mOCircles[liSlot].texture = mOSlotTexture;
            mORings[liSlot].texture = mORingTexture;
        }
    }

    /// <summary>A gold ring, as the bags' hover ring - also the character panel's Sleep and Track
    /// buttons' (UWModernPanel).</summary>
    internal static Texture2D BuildRing(int piSize, float pfRadius, float pfRim)
    {
        return BuildRing(piSize, pfRadius, pfRim, msGold);
    }

    /// <summary>A ring of any colour (the gamepad's slot).</summary>
    internal static Texture2D BuildRing(int piSize, float pfRadius, float pfRim, Color pOColour)
    {
        Color32[] lyPixels = new Color32[piSize * piSize];
        float lfCentre = (piSize - 1) * 0.5f;

        for (int y = 0; y < piSize; y++)
        {
            for (int x = 0; x < piSize; x++)
            {
                float lfDistance = Mathf.Sqrt(((x - lfCentre) * (x - lfCentre)) + ((y - lfCentre) * (y - lfCentre)));
                float lfAlpha = Mathf.Clamp01(lfDistance - (pfRadius - pfRim) + 0.5f) * (1f - Mathf.Clamp01(lfDistance - pfRadius + 0.5f));
                Color lOColour = pOColour;

                lOColour.a = lfAlpha;
                lyPixels[(y * piSize) + x] = lOColour;
            }
        }

        Texture2D lOTexture = new Texture2D(piSize, piSize, TextureFormat.RGBA32, false);
        lOTexture.name = "UWModernActionBar ring";
        lOTexture.filterMode = FilterMode.Bilinear;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(lyPixels);
        lOTexture.Apply();

        return lOTexture;
    }

    private static void fSetRect(RectTransform pORect, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        pORect.anchoredPosition = new Vector2(pfX, pfY);
        pORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        GameObject lORoot = new GameObject("Modern action bar", typeof(Canvas), typeof(CanvasScaler));
        lORoot.transform.SetParent(transform, false);

        mOCanvas = lORoot.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mOCanvas.sortingOrder = 41;

        CanvasScaler lOScaler = lORoot.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        lOScaler.scaleFactor = 1f;

        RectTransform lORootRect = (RectTransform)lORoot.transform;

        mOBack = fCreateRawImage(lORootRect, "Leather");
        mOBack.enabled = true;
        UWPixelArtUI.Apply(mOBack);

        mOCircles = new RawImage[SlotCount];
        mOIcons = new RawImage[SlotCount];
        mORings = new RawImage[SlotCount];
        mOKeys = new Text[SlotCount];
        mOCounts = new Text[SlotCount];
        mOSpellTexts = new Text[SlotCount];

        for (int liSlot = 0; liSlot < SlotCount; liSlot++)
        {
            mOCircles[liSlot] = fCreateRawImage(lORootRect, "Slot " + (liSlot + 1));
            mOCircles[liSlot].enabled = true;
            UWPixelArtUI.Apply(mOCircles[liSlot]);

            mOIcons[liSlot] = fCreateRawImage(lORootRect, "Icon");
            UWIconPalette.ApplySmooth(mOIcons[liSlot]);

            mOSpellTexts[liSlot] = fCreateText(lORootRect, "Spell runes", TextAnchor.MiddleCenter, Color.white);

            mORings[liSlot] = fCreateRawImage(lORootRect, "Drawn");

            mOKeys[liSlot] = fCreateText(lORootRect, "Key", TextAnchor.UpperLeft, msKey);
            mOKeys[liSlot].text = ((liSlot + 1) % 10).ToString();

            mOCounts[liSlot] = fCreateText(lORootRect, "Count", TextAnchor.LowerRight, Color.white);
        }

        mOPadRing = fCreateRawImage(lORootRect, "Gamepad slot");
        mOPadPrevious = fCreateRawImage(lORootRect, "Gamepad previous slot");
        mOPadNext = fCreateRawImage(lORootRect, "Gamepad next slot");
        mOPadUse = fCreateRawImage(lORootRect, "Gamepad use slot");

        mOLinkIcon = fCreateRawImage(lORootRect, "Dragged link");
        UWIconPalette.ApplySmooth(mOLinkIcon);
        mOLinkText = fCreateText(lORootRect, "Dragged spell", TextAnchor.MiddleCenter, Color.white);
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

    private Text fCreateText(Transform pOParent, string psName, TextAnchor peAlignment, Color pOColour)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Text), typeof(Outline));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        Text lOText = lOObject.GetComponent<Text>();
        lOText.font = mOFont != null ? mOFont : UWInterfaceFont.Font;
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
