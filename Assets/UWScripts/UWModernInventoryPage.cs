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
/// </summary>
public class UWModernInventoryPage : IUWModernPart
{
    public static UWModernInventoryPage Instance { get; private set; }

    /// <summary>Where the page lies on the classic 320 x 200 frame (UWHudPanel's PanelLeft/PanelTop).</summary>
    private const int PageX = 236;

    private const int PageY = 7;

    /// <summary>Where the original draws the body and the armour (UWPaperdollLayout), MAIN.BYT pixels.</summary>
    private static readonly Vector2Int msBodyAt = new Vector2Int(UWPaperdollLayout.BodyX, UWPaperdollLayout.BodyY);

    private static readonly Vector2Int msHelmetAt = new Vector2Int(UWPaperdollLayout.HelmetX, UWPaperdollLayout.HelmetY);

    private static readonly Vector2Int msGlovesAt = new Vector2Int(UWPaperdollLayout.GlovesX, UWPaperdollLayout.GlovesY);

    private static readonly Vector2Int msLegsAt = new Vector2Int(UWPaperdollLayout.LegsX, UWPaperdollLayout.LegsY);

    private static readonly Vector2Int msChestAt = new Vector2Int(UWPaperdollLayout.ChestX, UWPaperdollLayout.ChestY);

    private static readonly Vector2Int msBootsAt = new Vector2Int(UWPaperdollLayout.BootsX, UWPaperdollLayout.BootsY);

    private readonly UWGameUI mOUi;

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
    /// weight) - its centre at the frame's 305/62 (UWClassicLayout.Backpack.WeightCentre), in its colour.</summary>
    private Text mOWeight;

    /// <summary>The open container's back, its icon and the two scroll arrows.</summary>
    private RawImage mOContainerBack;

    private RawImage mOContainerIcon;

    private RawImage mOScrollUp;

    private RawImage mOScrollDown;

    private Texture2D mOContainerBackTexture;

    private Texture2D mOScrollUpTexture;

    private Texture2D mOScrollDownTexture;

    /// <summary>The container view in the frame (UWHudInventory): its back's top left, the icon's
    /// bottom left, the arrows' bottom right (the up arrow left of the down one, 3 apart).</summary>
    private const int ContainerBackX = 236;

    private const int ContainerBackY = 80;

    private const int ContainerIconX = 241;

    private const int ContainerIconBottom = 80;

    private const int ScrollRight = 314;

    private const int ScrollBottom = 80;

    private const int ScrollGap = 3;

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

    public UWModernInventoryPage(UWGameUI pOUi)
    {
        mOUi = pOUi;
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
            mOCounts[liAt] = UWUiKit.CreateText(pORoot, "Count " + liAt, TextAnchor.LowerRight, UWModernPalette.HudText, mOUi, UWUiKit.PageText);

        mOWeight = UWUiKit.CreateText(pORoot, "Weight", TextAnchor.LowerRight, UWModernPalette.HudText, mOUi, UWUiKit.PageText);
        mOWeight.alignment = TextAnchor.MiddleCenter;
        mOWeight.color = UWUiKit.Colour(UWClassicLayout.Backpack.WeightColour);

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
        if (!UWModernLayout.IsCharacterPageShown || mOUi == null || mOUi.mOUWData == null || mOUi.mCharacter == null
            || (UWModernClassicFrame.IsActive && UWModernClassicFrame.Page != UWModernClassicFrame.PageEnum.Inventory))
        {
            Hide();
            return;
        }

        UWModernClassicFrame.ApplySquash(mOContent);

        // The original's pictures, shared and following the colour help in place (UWGrTextures).
        UWTextures lOTextures = mOUi.mOUWData.Textures;

        mOPageTexture = UWGrTextures.Get(lOTextures, UWTexture.TextureTypes.PANELS, 0, mOUi.TextureFilterMode, UWGrTextures.ModeEnum.Picture);
        mOContainerBackTexture = UWGrTextures.Get(lOTextures, UWTexture.TextureTypes.INV, 6, mOUi.TextureFilterMode, UWGrTextures.ModeEnum.Picture);
        mOScrollUpTexture = UWGrTextures.Get(lOTextures, UWTexture.TextureTypes.BUTTONS, 27, mOUi.TextureFilterMode, UWGrTextures.ModeEnum.Picture);
        mOScrollDownTexture = UWGrTextures.Get(lOTextures, UWTexture.TextureTypes.BUTTONS, 28, mOUi.TextureFilterMode, UWGrTextures.ModeEnum.Picture);

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

        UWCharacter lOCharacter = mOUi.mCharacter;

        fPlace(mOArmour[0], lOCharacter.Paperdoll.CharTexture, msBodyAt);
        fPlace(mOArmour[1], lOCharacter.Paperdoll.HelmetTexture, msHelmetAt);
        fPlace(mOArmour[2], lOCharacter.Paperdoll.GlovesTexture, msGlovesAt);
        // The legs' picture carries its drop itself (UWPaperdollView.LegsDrop).
        fPlace(mOArmour[3], lOCharacter.Paperdoll.LegsArmorTexture, msLegsAt);
        fPlace(mOArmour[4], lOCharacter.Paperdoll.ChestArmorTexture, msChestAt);
        fPlace(mOArmour[5], lOCharacter.Paperdoll.BootsTexture, msBootsAt);

        UWInventory lOInventory = mOUi.mOInventory;

        mOView.Refresh(lOInventory != null ? lOInventory.Model : null, mOUi.mOUWData);

        for (int liSlot = 0; liSlot < UWPaperdollLayout.IconSlots.Length; liSlot++)
        {
            (UWArmorItemMap.BodySlot _, float lfX, float lfY, float lfSize) = UWPaperdollLayout.IconSlots[liSlot];
            UWInventoryView.Slot lOSlot = mOView.Hands[liSlot];

            fPlaceIcon(mOIcons[liSlot], lOSlot.Picture, lfX + (lfSize * 0.5f), lfY + (lfSize * 0.5f) - (lfSize < 16f ? UWPaperdollLayout.RingIconYOffset : 0f));
            fPlaceCount(mOCounts[UWInventoryModel.BackpackSize + liSlot], lOSlot, lfX + Mathf.Max(lfSize, 16f), lfY + Mathf.Max(lfSize, 16f));
        }

        // The backpack's things in the eight circles - the slots' areas from the original's table
        // (y counted from the bottom of the frame) - or the open container's from its offset on.
        fPlaceContainerView();

        for (int liSlot = 0; liSlot < mOBackpack.Length; liSlot++)
        {
            UWInventoryView.Slot lOSlot = mOView.PageSlot(liSlot);
            UWClickRules.Area lOArea = UWClickRules.BackpackArea(liSlot);

            fPlaceIcon(mOBackpack[liSlot], lOSlot.Picture, (lOArea.X0 + lOArea.X1 + 1) * 0.5f, 199.5f - ((lOArea.Y0 + lOArea.Y1) * 0.5f));
            fPlaceCount(mOCounts[liSlot], lOSlot, lOArea.X1 + 1, 200 - lOArea.Y0);
        }

        fPlaceWeight(lOInventory);
    }

    /// <summary>The remaining carrying capacity (maximum weight minus the load, at least 0) in
    /// whole stones, centred in the circle - as UWHudInventory.fRefreshWeight shows it, both from
    /// UWInventoryWeight.RemainingStones since 2026-10-20 (UWInventoryView.RemainingStones since
    /// cleanup step 5.8).</summary>
    private void fPlaceWeight(UWInventory pOInventory)
    {
        UWPlayerData lOPlayer = mOUi.mOUWData.InitialPlayer;

        if (pOInventory == null || lOPlayer == null)
        {
            mOWeight.enabled = false;
            return;
        }

        int liStones = mOView.RemainingStones;
        Vector2 lOCentre = new Vector2(UWClassicLayout.Backpack.WeightCentreX, UWClassicLayout.Backpack.WeightCentreY);
        float lfAspect = miScale * UWModernHudArt.PixelAspectX;
        float lfWidth = 30f * lfAspect;
        float lfHeight = 10f * miScale;

        mOWeight.enabled = true;
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
            UWUiKit.SetRect(mOContainerBack.rectTransform, ScreenRect.x + ((ContainerBackX - PageX) * lfAspect),
                ScreenRect.yMax - ((ContainerBackY - PageY + mOContainerBackTexture.height) * miScale),
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
            UWUiKit.SetRect(mOContainerIcon.rectTransform, ScreenRect.x + ((ContainerIconX - PageX) * lfAspect),
                ScreenRect.yMax - ((ContainerIconBottom - PageY) * miScale), lOIcon.width * lfAspect, lOIcon.height * miScale);
        }
        else
            mOContainerIcon.enabled = false;

        bool lbUp = mOView.ShowsScrollUp && mOScrollUpTexture != null;
        bool lbDown = mOView.ShowsScrollDown && mOScrollDownTexture != null;
        float lfDownWidth = mOScrollDownTexture != null ? mOScrollDownTexture.width : 0f;

        fPlaceArrow(mOScrollDown, mOScrollDownTexture, lbDown, ScrollRight);
        fPlaceArrow(mOScrollUp, mOScrollUpTexture, lbUp, ScrollRight - lfDownWidth - ScrollGap);
    }

    /// <summary>An arrow by its right edge and its bottom row on the frame.</summary>
    private void fPlaceArrow(RawImage pOImage, Texture2D pOTexture, bool pbShown, float pfRight)
    {
        pOImage.enabled = pbShown;

        if (!pbShown)
            return;

        float lfAspect = miScale * UWModernHudArt.PixelAspectX;

        pOImage.texture = pOTexture;
        UWUiKit.SetRect(pOImage.rectTransform, ScreenRect.x + ((pfRight - pOTexture.width - PageX) * lfAspect),
            ScreenRect.yMax - ((ScrollBottom - PageY) * miScale), pOTexture.width * lfAspect, pOTexture.height * miScale);
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

    /// <summary>The equipment slot under a screen point, by the original's table of the page
    /// (UWClickRules.InventorySpotAt), as UWModernPanel.TryGetEquipSlotAt.</summary>
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

        UWClickRules.Area lOArea = UWClickRules.BackpackArea(piSlot);
        float lfAspect = miScale * UWModernHudArt.PixelAspectX;

        return new Rect(ScreenRect.x + ((lOArea.X0 - PageX) * lfAspect), ScreenRect.yMax - ((200 - lOArea.Y0 - PageY) * miScale),
            (lOArea.X1 + 1 - lOArea.X0) * lfAspect, (lOArea.Y1 + 1 - lOArea.Y0) * miScale);
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
    /// The spot of the page under a screen point, in the frame's coordinates. NONE WHILE THE
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

        peSpot = UWClickRules.InventorySpotAt(liX, 199 - liRow);

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
        return UWIconCache.Texture(pOSource, mOUi.TextureFilterMode);
    }
}
