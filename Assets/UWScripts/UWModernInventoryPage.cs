using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// THE ORIGINAL'S CHARACTER PANEL IN THE MODERN INTERFACE (Classic+, per user 2026-10-10: the
/// original panels "each a single panel", "always visible", and with the original character
/// panel "the original inventory"): the inventory page PANELS.GR 0 as an element of its own
/// (UWModernLayout.ElementEnum.CharacterPage), placed by the layout editor - by default at the
/// right edge, at the top.
///
/// On it, at the classic frame's places (the page lies at 236/7 of the 320 x 200 frame): the
/// body and the armour as UWCharacter draws them, the things in the hands, on the shoulders and
/// on the fingers (UWPaperdollLayout.IconSlots), and the eight backpack slots from the original's
/// table of the page (UWClickRules.InventorySpotAt). All of it at the original's pixel proportion
/// (UWModernHudArt.PixelAspectX), as the other original pieces.
///
/// The things are taken and put down by UWModernBags, which asks this page for its slots
/// (TryGetEquipSlotAt, BackpackSlotAt, SpotAt), as it asks the modern character panel; while the
/// page shows, the bags' own windows stay shut (UWModernBagPages.IsPageShown).
///
/// THE OPEN CONTAINER as in the original (UWInventoryModel.OpenContainer, one at a time): its
/// things in the eight slots from the scroll offset on, on the container view's back (INV.GR 6
/// over the backpack's place), the container's own icon above the slots at the left (a click
/// closes it, one level up; a thing dropped on it goes one level up), the two arrows at the right
/// (BUTTONS.GR 27 and 28, shown only where they can scroll; a row per click) - the classic
/// UWHudInventory's places.
///
/// PER GAME (UW2 plan stage U5 step 2, 2026-11-23): every place on the page is the running game's
/// (UWPaperdollGeometry): the page's place on the screen, the figure, the armour in the game's
/// drawing order, the six things, the backpack's boxes and middles, the weight with its colour, the
/// open bag's back, icon and arrows, and which spot a point is. The second game's page and the bag's
/// back are opaque everywhere, its arrows clear in their own colour 0, as its classic page draws them.
/// </summary>
public class UWModernInventoryPage : IUWModernPart
{
    public static UWModernInventoryPage Instance { get; private set; }

    /// <summary>The running game's places (stage U5 step 2), as of the last drawing; the screen's pixels.</summary>
    private UWPaperdollGeometry mOGeometry = UWPaperdollGeometry.Uw1;

    /// <summary>The geometry the armour's drawing order was set for.</summary>
    private UWPaperdollGeometry mOOrderedFor;

    /// <summary>Where the whole page lies on the game's screen (the first game's 236/7, the second game's 235/7).</summary>
    private int PageX => mOGeometry.PanelLeft;

    private int PageY => mOGeometry.PanelTop;

    private RawImage mOPage;

    private Texture2D mOPageTexture;

    private RawImage[] mOArmour;

    private RawImage[] mOIcons;

    private RawImage[] mOBackpack;

    /// <summary>The stack counts: the eight slots, then the six equipment slots (per user,
    /// 2026-10-10: counts were not shown) - in the bags' look (UWModernBags' slot count).</summary>
    private Text[] mOCounts;

    /// <summary>The remaining carrying capacity in whole stones, in the page's circle as the classic
    /// UWHudInventory shows it (per user, 2026-10-11: Classic Wide and Classic Modular showed no
    /// weight) - its centre at the frame's 305/62 (UWClassicLayout.Backpack.WeightCentre), in its colour;
    /// the second game's centred on 305 under row 56, in palette colour 0x88 (UWPaperdollGeometry).</summary>
    private Text mOWeight;

    /// <summary>The open container's back, its icon and the two scroll arrows.</summary>
    private RawImage mOContainerBack;

    private RawImage mOContainerIcon;

    private RawImage mOScrollUp;

    private RawImage mOScrollDown;

    private Texture2D mOContainerBackTexture;

    private Texture2D mOScrollUpTexture;

    private Texture2D mOScrollDownTexture;

    // The container view's places (its back, the icon, the arrows) are the game's: UWPaperdollGeometry.

    private float miScale = 1f;

    /// <summary>What the page shows, read once a frame while it shows (cleanup step 5.8,
    /// 2026-10-20): the same view the classic frame (UWHudInventory) draws.</summary>
    private readonly UWInventoryView mOView = new UWInventoryView();

    /// <summary>Its own canvas (above the HUD, under the bags' pointer item and menu): it shows in
    /// a conversation as well, for the trade, where the HUD's canvas is off.</summary>
    private Canvas mOCanvas;

    /// <summary>Where it was drawn (bottom-left origin); empty while hidden.</summary>
    public Rect ScreenRect { get; private set; }

    /// <summary>Whether the page shows (the bags take clicks then, UWModernBags.IsActive).</summary>
    public bool IsShown => ScreenRect.width > 0f;

    public UWModernInventoryPage()
    {
        Instance = this;
    }

    private RectTransform mOContent;

    public void Build(Transform pOParent)
    {
        mOCanvas = UWUiKit.CreateOverlayCanvas(pOParent, "Original character page", UWUiSorting.ModernInventoryPage);
        mOCanvas.enabled = false;

        // The page's parts in one group, which the classic frame's panel turn squeezes
        // (UWModernClassicFrame.ApplySquash).
        mOContent = UWUiKit.CreateGroup(mOCanvas.transform, "Content");
        mOContent.sizeDelta = Vector2.zero;

        Transform pORoot = mOContent;

        mOPage = UWUiKit.CreateRawImage(pORoot, "Inventory page");
        UWPixelArtUI.Apply(mOPage);

        mOArmour = new RawImage[6];

        for (int liAt = 0; liAt < mOArmour.Length; liAt++)
        {
            mOArmour[liAt] = UWUiKit.CreateRawImage(pORoot, "Paperdoll " + liAt);
            UWPixelArtUI.Apply(mOArmour[liAt]);
        }

        mOIcons = new RawImage[UWPaperdollLayout.IconSlots.Length];

        for (int liAt = 0; liAt < mOIcons.Length; liAt++)
        {
            mOIcons[liAt] = UWUiKit.CreateRawImage(pORoot, "Equipped " + UWPaperdollLayout.IconSlots[liAt].Slot);
            UWIconPalette.Apply(mOIcons[liAt]);
        }

        mOContainerBack = UWUiKit.CreateRawImage(pORoot, "Container back");
        UWPixelArtUI.Apply(mOContainerBack);

        mOBackpack = new RawImage[UWInventoryModel.BackpackSize];

        for (int liAt = 0; liAt < mOBackpack.Length; liAt++)
        {
            mOBackpack[liAt] = UWUiKit.CreateRawImage(pORoot, "Backpack " + liAt);
            UWIconPalette.Apply(mOBackpack[liAt]);
        }

        mOCounts = new Text[UWInventoryModel.BackpackSize + UWPaperdollLayout.IconSlots.Length];

        for (int liAt = 0; liAt < mOCounts.Length; liAt++)
            mOCounts[liAt] = UWUiKit.CreateText(pORoot, "Count " + liAt, TextAnchor.LowerRight, UWModernPalette.HudText, UWModernUi.TextUi, UWUiKit.PageText);

        mOWeight = UWUiKit.CreateText(pORoot, "Weight", TextAnchor.LowerRight, UWModernPalette.HudText, UWModernUi.TextUi, UWUiKit.PageText);
        mOWeight.alignment = TextAnchor.MiddleCenter;

        mOContainerIcon = UWUiKit.CreateRawImage(pORoot, "Open container");
        UWIconPalette.Apply(mOContainerIcon);
        mOScrollUp = UWUiKit.CreateRawImage(pORoot, "Container scroll up");
        UWPixelArtUI.Apply(mOScrollUp);
        mOScrollDown = UWUiKit.CreateRawImage(pORoot, "Container scroll down");
        UWPixelArtUI.Apply(mOScrollDown);
    }

    /// <summary>Once a frame while the modern HUD shows; pfPixelScale is UWModernUi.PixelScale.</summary>
    public void Update(float pfPixelScale)
    {
        ScreenRect = Rect.zero;

        if (mOPage == null)
            return;

        // In Classic Wide only while the frame's panel shows it (UWModernClassicFrame.Page).
        if (!UWModernLayout.IsCharacterPageShown || UWModernUi.Session == null || UWModernUi.Data == null
            || (UWModernClassicFrame.IsActive && UWModernClassicFrame.Page != UWModernClassicFrame.PageEnum.Inventory))
        {
            Hide();
            return;
        }

        UWModernClassicFrame.ApplySquash(mOContent);

        // The original's pictures, shared and following the colour help in place (UWGrTextures): the running game's,
        // the second game's page and bag back opaque, its arrows clear in their own colour 0 (stage U5 step 2).
        UWTextures lOTextures = UWModernUi.Data.Textures;

        mOGeometry = UWPaperdollGeometry.Of(lOTextures);

        UWPaperdollGeometry lOAt = mOGeometry;
        UWGrTextures.ModeEnum leOpaque = lOAt.IsSecondGame ? UWGrTextures.ModeEnum.Opaque : UWGrTextures.ModeEnum.Picture;
        UWGrTextures.ModeEnum leArrows = lOAt.IsSecondGame ? UWGrTextures.ModeEnum.ZeroClear : UWGrTextures.ModeEnum.Picture;

        mOPageTexture = UWGrTextures.Get(lOTextures, UWTexture.TextureTypes.PANELS, 0, UWModernUi.Filter, leOpaque);
        mOContainerBackTexture = UWGrTextures.Get(lOTextures, UWTexture.TextureTypes.INV, lOAt.BagBackPicture, UWModernUi.Filter, leOpaque);
        mOScrollUpTexture = UWGrTextures.Get(lOTextures, UWTexture.TextureTypes.BUTTONS, lOAt.UpArrowPicture, UWModernUi.Filter, leArrows);
        mOScrollDownTexture = UWGrTextures.Get(lOTextures, UWTexture.TextureTypes.BUTTONS, lOAt.DownArrowPicture, UWModernUi.Filter, leArrows);

        if (mOPageTexture == null)
        {
            Hide();
            return;
        }

        miScale = pfPixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.CharacterPage);

        float lfWidth = mOPageTexture.width * miScale * UWModernHudArt.PixelAspectX;
        float lfHeight = mOPageTexture.height * miScale;
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.CharacterPage,
            new Rect(Screen.width - lfWidth - (4f * pfPixelScale), Screen.height - lfHeight - (4f * pfPixelScale), lfWidth, lfHeight));

        UWModernLayout.Report(UWModernLayout.ElementEnum.CharacterPage, lOPlaced);
        ScreenRect = lOPlaced;
        mOCanvas.enabled = true;

        mOPage.texture = mOPageTexture;
        mOPage.enabled = true;
        UWUiKit.SetRect(mOPage.rectTransform, lOPlaced.x, lOPlaced.y, lOPlaced.width, lOPlaced.height);

        IUWModernArt lOArt = UWModernUi.Session.Art;

        fPlace(mOArmour[0], lOArt.FigureTexture(UWModernFigurePiece.Body), new Vector2Int(lOAt.BodyX, lOAt.BodyY));
        fPlace(mOArmour[1 + UWPaperdollGeometry.Helmet], lOArt.FigureTexture(UWModernFigurePiece.Helmet), fArmourAt(UWPaperdollGeometry.Helmet));
        fPlace(mOArmour[1 + UWPaperdollGeometry.Gloves], lOArt.FigureTexture(UWModernFigurePiece.Gloves), fArmourAt(UWPaperdollGeometry.Gloves));
        // The first game's legs' picture carries its drop itself (UWPaperdollView.LegsDrop).
        fPlace(mOArmour[1 + UWPaperdollGeometry.Legs], lOArt.FigureTexture(UWModernFigurePiece.Legs), fArmourAt(UWPaperdollGeometry.Legs));
        fPlace(mOArmour[1 + UWPaperdollGeometry.Chest], lOArt.FigureTexture(UWModernFigurePiece.Chest), fArmourAt(UWPaperdollGeometry.Chest));
        fPlace(mOArmour[1 + UWPaperdollGeometry.Boots], lOArt.FigureTexture(UWModernFigurePiece.Boots), fArmourAt(UWPaperdollGeometry.Boots));
        fOrderArmour();

        UWInventoryModel lOInventory = UWModernUi.Inventory;

        mOView.Refresh(lOInventory != null ? lOInventory : null, UWModernUi.Data, UWModernUi.Session.Character.MaxLoad);

        for (int liSlot = 0; liSlot < lOAt.IconSlots.Length; liSlot++)
        {
            (UWArmorItemMap.BodySlot _, float lfX, float lfY, float lfSize) = lOAt.IconSlots[liSlot];
            UWInventoryView.Slot lOSlot = mOView.Hands[liSlot];

            fPlaceIcon(mOIcons[liSlot], lOSlot.Picture, lfX + (lfSize * 0.5f), lfY + (lfSize * 0.5f) - (lfSize < 16f ? lOAt.RingIconYOffset : 0f));
            fPlaceCount(mOCounts[UWInventoryModel.BackpackSize + liSlot], lOSlot, lfX + Mathf.Max(lfSize, 16f), lfY + Mathf.Max(lfSize, 16f));
        }

        // The backpack's things in the eight places - the pictures centred on the game's middles, the counts at
        // the boxes' bottom right - or the open container's from its offset on.
        fPlaceContainerView();

        for (int liSlot = 0; liSlot < mOBackpack.Length; liSlot++)
        {
            UWInventoryView.Slot lOSlot = mOView.PageSlot(liSlot);
            UWRectInt lOBox = lOAt.BackpackBoxes[liSlot];

            fPlaceIcon(mOBackpack[liSlot], lOSlot.Picture, lOAt.BackpackCentreX[liSlot], lOAt.BackpackCentreRow[liSlot]);
            fPlaceCount(mOCounts[liSlot], lOSlot, lOBox.X + lOBox.Width, lOBox.Y + lOBox.Height);
        }

        fPlaceWeight(lOInventory);
    }

    /// <summary>A piece of armour's place (UWPaperdollGeometry.Helmet to Boots) on the game's screen.</summary>
    private Vector2Int fArmourAt(int piPiece)
    {
        return new Vector2Int(mOGeometry.ArmourX[piPiece], mOGeometry.ArmourY[piPiece]);
    }

    /// <summary>The armour drawn in the game's order (UWPaperdollGeometry.ArmourOrder: the first game's helmet to
    /// boots, the second game's ArmorSlots - leggings, chest, boots, gloves, helmet), right above the body.</summary>
    private void fOrderArmour()
    {
        if (mOOrderedFor == mOGeometry)
            return;

        mOOrderedFor = mOGeometry;

        int liBody = mOArmour[0].transform.GetSiblingIndex();

        for (int liAt = 0; liAt < mOGeometry.ArmourOrder.Length; liAt++)
            mOArmour[1 + mOGeometry.ArmourOrder[liAt]].transform.SetSiblingIndex(liBody + 1 + liAt);
    }

    /// <summary>The remaining carrying capacity (maximum weight minus the load, at least 0) in
    /// whole stones, centred in the circle - as UWHudInventory.fRefreshWeight shows it, both from
    /// UWInventoryWeight.RemainingStones since 2026-10-20 (UWInventoryView.RemainingStones since
    /// cleanup step 5.8).</summary>
    private void fPlaceWeight(UWInventoryModel pOInventory)
    {
        if (pOInventory == null || UWModernUi.Session == null || !UWModernUi.Session.Character.IsLoaded)
        {
            mOWeight.enabled = false;
            return;
        }

        int liStones = mOView.RemainingStones;
        Vector2 lOCentre = new Vector2(mOGeometry.WeightCentreX, mOGeometry.WeightCentreRow);
        float lfAspect = miScale * UWModernHudArt.PixelAspectX;
        float lfWidth = 30f * lfAspect;
        float lfHeight = 10f * miScale;

        mOWeight.enabled = true;
        mOWeight.color = UWUiKit.Colour(mOGeometry.WeightColour(UWModernUi.Data.Palettes));
        mOWeight.text = liStones.ToString();
        mOWeight.fontSize = Mathf.Max(9, Mathf.RoundToInt(5f * miScale));
        UWUiKit.SetRect(mOWeight.rectTransform, ScreenRect.x + ((lOCentre.x - PageX) * lfAspect) - (lfWidth * 0.5f),
            ScreenRect.yMax - ((lOCentre.y - PageY) * miScale) - (lfHeight * 0.5f), lfWidth, lfHeight);
    }

    public void Hide()
    {
        ScreenRect = Rect.zero;

        if (mOPage == null)
            return;

        mOPage.enabled = false;
        mOCanvas.enabled = false;

        foreach (RawImage lOImage in mOArmour)
            lOImage.enabled = false;

        foreach (RawImage lOImage in mOIcons)
            lOImage.enabled = false;

        foreach (RawImage lOImage in mOBackpack)
            lOImage.enabled = false;

        foreach (Text lOCount in mOCounts)
            lOCount.enabled = false;

        mOWeight.enabled = false;

        mOContainerBack.enabled = false;
        mOContainerIcon.enabled = false;
        mOScrollUp.enabled = false;
        mOScrollDown.enabled = false;
    }

    /// <summary>The open container's back, icon and arrows at the classic frame's places, or none.</summary>
    private void fPlaceContainerView()
    {
        bool lbOpen = mOView.ShowsContainer;
        float lfAspect = miScale * UWModernHudArt.PixelAspectX;

        mOContainerBack.enabled = lbOpen && mOContainerBackTexture != null;

        if (mOContainerBack.enabled)
        {
            mOContainerBack.texture = mOContainerBackTexture;
            UWUiKit.SetRect(mOContainerBack.rectTransform, ScreenRect.x + ((mOGeometry.BagBackLeft - PageX) * lfAspect),
                ScreenRect.yMax - ((mOGeometry.BagBackTop - PageY + mOContainerBackTexture.height) * miScale),
                mOContainerBackTexture.width * lfAspect, mOContainerBackTexture.height * miScale);
        }

        // The open variant, as the classic frame and the modern bags show it - until 2026-10-20
        // this took pOOpen.Icon ?? pOOpen.Texture and so the closed picture (B29 of the cleanup
        // plan, decided by the user: the open one, UWObjectAppearance.OpenContainerPicture; read
        // through UWInventoryView.ContainerPicture since cleanup step 5.8).
        UWTexture lOSource = mOView.ContainerPicture;

        if (lOSource != null)
        {
            Texture2D lOIcon = fIcon(lOSource);

            mOContainerIcon.texture = lOIcon;
            mOContainerIcon.enabled = true;
            UWUiKit.SetRect(mOContainerIcon.rectTransform, ScreenRect.x + ((mOGeometry.BagIconLeft - PageX) * lfAspect),
                ScreenRect.yMax - ((mOGeometry.BagIconBottom - PageY) * miScale), lOIcon.width * lfAspect, lOIcon.height * miScale);
        }
        else
            mOContainerIcon.enabled = false;

        bool lbUp = mOView.ShowsScrollUp && mOScrollUpTexture != null;
        bool lbDown = mOView.ShowsScrollDown && mOScrollDownTexture != null;
        int liUpWidth = mOScrollUpTexture != null ? mOScrollUpTexture.width : 0;
        int liDownWidth = mOScrollDownTexture != null ? mOScrollDownTexture.width : 0;

        fPlaceArrow(mOScrollDown, mOScrollDownTexture, lbDown, mOGeometry.ArrowLeft(false, liUpWidth, liDownWidth));
        fPlaceArrow(mOScrollUp, mOScrollUpTexture, lbUp, mOGeometry.ArrowLeft(true, liUpWidth, liDownWidth));
    }

    /// <summary>An arrow by its left edge and the game's row below the arrows (UWPaperdollGeometry.ArrowBottom).</summary>
    private void fPlaceArrow(RawImage pOImage, Texture2D pOTexture, bool pbShown, int piLeft)
    {
        pOImage.enabled = pbShown;

        if (!pbShown)
            return;

        float lfAspect = miScale * UWModernHudArt.PixelAspectX;

        pOImage.texture = pOTexture;
        UWUiKit.SetRect(pOImage.rectTransform, ScreenRect.x + ((piLeft - PageX) * lfAspect),
            ScreenRect.yMax - ((mOGeometry.ArrowBottom - PageY) * miScale), pOTexture.width * lfAspect, pOTexture.height * miScale);
    }

    /// <summary>Whether the up arrow (it raises the offset, UWInventoryModel) and the down arrow show.</summary>
    public bool IsScrollUpShown => mOScrollUp != null && mOScrollUp.enabled;

    public bool IsScrollDownShown => mOScrollDown != null && mOScrollDown.enabled;

    public bool IsContainerIconShown => mOContainerIcon != null && mOContainerIcon.enabled;

    /// <summary>Whether a screen point lies on the page - nothing goes into the world through it.</summary>
    public bool Contains(Vector2 pOPointer)
    {
        return ScreenRect.width > 0f && ScreenRect.Contains(pOPointer);
    }

    /// <summary>The equipment slot under a screen point, by the game's table of the page
    /// (UWPaperdollGeometry.SpotAt), as UWModernPanel.TryGetEquipSlotAt.</summary>
    public bool TryGetEquipSlotAt(Vector2 pOPointer, out UWArmorItemMap.BodySlot peSlot)
    {
        peSlot = UWArmorItemMap.BodySlot.Helmet;

        if (!fSpotAt(pOPointer, out UWClickRules.InventorySpot leSpot))
            return false;

        UWArmorItemMap.BodySlot? leSlot = UWClickRules.BodySlotOf(leSpot);

        if (!leSlot.HasValue)
            return false;

        peSlot = leSlot.Value;
        return true;
    }

    /// <summary>A slot's place on the screen (for the bags' split box and menu), empty while hidden.</summary>
    public Rect BackpackSlotRect(int piSlot)
    {
        if (!IsShown || piSlot < 0 || piSlot >= UWInventoryModel.BackpackSize)
            return Rect.zero;

        UWRectInt lOBox = mOGeometry.BackpackBoxes[piSlot];
        float lfAspect = miScale * UWModernHudArt.PixelAspectX;

        return new Rect(ScreenRect.x + ((lOBox.X - PageX) * lfAspect), ScreenRect.yMax - ((lOBox.Y + lOBox.Height - PageY) * miScale),
            lOBox.Width * lfAspect, lOBox.Height * miScale);
    }

    /// <summary>The backpack slot (0 to 7) under a screen point, -1 for none.</summary>
    public int BackpackSlotAt(Vector2 pOPointer)
    {
        return fSpotAt(pOPointer, out UWClickRules.InventorySpot leSpot) ? UWClickRules.BackpackSlotOf(leSpot) : -1;
    }

    /// <summary>The spot of the page under a screen point (the original's table), None for none.</summary>
    public UWClickRules.InventorySpot SpotAt(Vector2 pOPointer)
    {
        return fSpotAt(pOPointer, out UWClickRules.InventorySpot leSpot) ? leSpot : UWClickRules.InventorySpot.None;
    }

    /// <summary>
    /// The spot of the page under a screen point, in the game's screen coordinates (the game's table,
    /// UWPaperdollGeometry.SpotAt). NONE WHILE THE
    /// CLASSIC WIDE PANEL TURNS (per user, 2026-10-20: turning to the stats or the rune tablet,
    /// the page's slots, paperdoll and arrows still took clicks), as the classic panel locks its
    /// slots during the turn (UWHudPanel.fIsInventoryClickable): the page stands squeezed then and
    /// the spots no longer lie where they appear. The page still counts as UI (Contains), so
    /// nothing goes into the world through it meanwhile.
    /// </summary>
    private bool fSpotAt(Vector2 pOPointer, out UWClickRules.InventorySpot peSpot)
    {
        peSpot = UWClickRules.InventorySpot.None;

        if (!Contains(pOPointer) || UWModernClassicFrame.IsTurning)
            return false;

        int liX = PageX + Mathf.FloorToInt((pOPointer.x - ScreenRect.x) / (miScale * UWModernHudArt.PixelAspectX));
        int liRow = PageY + Mathf.FloorToInt((ScreenRect.yMax - pOPointer.y) / miScale);

        peSpot = mOGeometry.SpotAt(liX, liRow);

        return peSpot != UWClickRules.InventorySpot.None;
    }

    /// <summary>A picture of the figure at its place on the frame (MAIN.BYT pixels); an empty
    /// slot's placeholder (1 x 1) is not shown.</summary>
    private void fPlace(RawImage pOImage, Texture2D pOTexture, Vector2Int pOAt)
    {
        if (pOTexture == null || pOTexture.width <= 1)
        {
            pOImage.enabled = false;
            return;
        }

        float lfAspect = miScale * UWModernHudArt.PixelAspectX;

        pOImage.texture = pOTexture;
        pOImage.enabled = true;
        UWUiKit.SetRect(pOImage.rectTransform, ScreenRect.x + ((pOAt.x - PageX) * lfAspect),
            ScreenRect.yMax - ((pOAt.y - PageY + pOTexture.height) * miScale), pOTexture.width * lfAspect, pOTexture.height * miScale);
    }

    /// <summary>A thing's picture (UWInventoryView.Slot.Picture) centred on a point of the frame
    /// (MAIN.BYT pixels, rows from the top).</summary>
    private void fPlaceIcon(RawImage pOImage, UWTexture pOSource, float pfCentreX, float pfCentreRow)
    {
        UWTexture lOSource = pOSource;

        if (lOSource == null)
        {
            pOImage.enabled = false;
            return;
        }

        Texture2D lOTexture = fIcon(lOSource);
        float lfAspect = miScale * UWModernHudArt.PixelAspectX;

        pOImage.texture = lOTexture;
        pOImage.enabled = true;
        UWUiKit.SetRect(pOImage.rectTransform, Mathf.Round(ScreenRect.x + ((pfCentreX - PageX - (lOTexture.width * 0.5f)) * lfAspect)),
            Mathf.Round(ScreenRect.yMax - ((pfCentreRow - PageY + (lOTexture.height * 0.5f)) * miScale)),
            lOTexture.width * lfAspect, lOTexture.height * miScale);
    }

    /// <summary>A stack's count at the bottom right of its slot (frame pixels: the slot's right
    /// edge and bottom row), none for a single thing.</summary>
    private void fPlaceCount(Text pOText, UWInventoryView.Slot pOSlot, float pfRight, float pfBottomRow)
    {
        int liCount = pOSlot.Count;

        pOText.enabled = pOSlot.ShowsCount;

        if (!pOSlot.ShowsCount)
            return;

        float lfAspect = miScale * UWModernHudArt.PixelAspectX;
        float lfHeight = 10f * miScale;

        pOText.text = liCount.ToString();
        pOText.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * miScale));
        UWUiKit.SetRect(pOText.rectTransform, ScreenRect.x + ((pfRight - PageX) * lfAspect) - (40f * miScale) + miScale,
            ScreenRect.yMax - ((pfBottomRow - PageY) * miScale) - miScale, 40f * miScale, lfHeight);
    }

    /// <summary>An icon (palette indices, UWIconPalette), shared (UWIconCache).</summary>
    private Texture2D fIcon(UWTexture pOSource)
    {
        return UWIconCache.Texture(pOSource, UWModernUi.Filter);
    }
}
