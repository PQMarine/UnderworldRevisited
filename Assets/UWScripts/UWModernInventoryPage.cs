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
/// on the fingers (UWModernPanel's msIconSlots), and the eight backpack slots from the original's
/// table of the page (UWClickRules.InventorySpotAt). All of it at the original's pixel proportion
/// (UWModernHudArt.PixelAspectX), as the other original pieces.
///
/// The things are taken and put down by UWModernBags, which asks this page for its slots
/// (TryGetEquipSlotAt, BackpackSlotAt, SpotAt), as it asks the modern character panel; while the
/// page shows, the bags' own windows stay shut (UWModernBags.fPageShown).
///
/// THE OPEN CONTAINER as in the original (UWInventoryModel.OpenContainer, one at a time): its
/// things in the eight slots from the scroll offset on, on the container view's back (INV.GR 6
/// over the backpack's place), the container's own icon above the slots at the left (a click
/// closes it, one level up; a thing dropped on it goes one level up), the two arrows at the right
/// (BUTTONS.GR 27 and 28, shown only where they can scroll; a row per click) - the classic
/// UWHudInventory's places.
/// </summary>
public class UWModernInventoryPage
{
    public static UWModernInventoryPage Instance { get; private set; }

    /// <summary>Where the page lies on the classic 320 x 200 frame (UWHudPanel's PanelLeft/PanelTop).</summary>
    private const int PageX = 236;

    private const int PageY = 7;

    /// <summary>Where the original draws the body and the armour (UWModernPanel's), MAIN.BYT pixels.</summary>
    private static readonly Vector2Int msBodyAt = new Vector2Int(260, 11);

    private static readonly Vector2Int msHelmetAt = new Vector2Int(267, 10);

    private static readonly Vector2Int msGlovesAt = new Vector2Int(261, 42);

    private static readonly Vector2Int msLegsAt = new Vector2Int(268, 24);

    private static readonly Vector2Int msChestAt = new Vector2Int(262, 22);

    private static readonly Vector2Int msBootsAt = new Vector2Int(266, 66);

    /// <summary>The six slots that show a thing's own icon, as the classic page places them
    /// (UWModernPanel.msIconSlots): top-left corner and size; the rings' 4 x 4 carries a 16 x 16
    /// icon centred, 0.8 up.</summary>
    private static readonly (UWArmorItemMap.BodySlot Slot, float X, float Y, float Size)[] msIconSlots =
    {
        (UWArmorItemMap.BodySlot.LeftRing, 291.03f, 56.9f, 4f),
        (UWArmorItemMap.BodySlot.RightRing, 260f, 56.9f, 4f),
        (UWArmorItemMap.BodySlot.RightHandSlot, 242f, 34.82566f, 16f),
        (UWArmorItemMap.BodySlot.LeftHandSlot, 296f, 34.826f, 16f),
        (UWArmorItemMap.BodySlot.LeftOffHandSlot, 294f, 12.792999f, 16f),
        (UWArmorItemMap.BodySlot.RightOffHandSlot, 244.9f, 12.793f, 16f),
    };

    private const float RingIconYOffset = 0.8f;

    private readonly UWGameUI mOUi;

    private RawImage mOPage;

    private Texture2D mOPageTexture;

    private int miArtVersion = -1;

    private RawImage[] mOArmour;

    private RawImage[] mOIcons;

    private RawImage[] mOBackpack;

    /// <summary>The stack counts: the eight slots, then the six equipment slots (per user,
    /// 2026-10-10: counts were not shown) - in the bags' look (UWModernBags' slot count).</summary>
    private Text[] mOCounts;

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

    private readonly Dictionary<UWTexture, Texture2D> mOIconTextures = new Dictionary<UWTexture, Texture2D>();

    private float miScale = 1f;

    /// <summary>Its own canvas (above the HUD, under the bags' pointer item and menu): it shows in
    /// a conversation as well, for the trade, where the HUD's canvas is off.</summary>
    private Canvas mOCanvas;

    /// <summary>Where it was drawn (bottom-left origin); empty while hidden.</summary>
    public Rect ScreenRect { get; private set; }

    /// <summary>Whether the page shows (the bags take clicks then, UWModernBags.fIsActive).</summary>
    public bool IsShown => ScreenRect.width > 0f;

    public UWModernInventoryPage(UWGameUI pOUi)
    {
        mOUi = pOUi;
        Instance = this;
    }

    private RectTransform mOContent;

    public void Build(Transform pOParent)
    {
        GameObject lOCanvasObject = new GameObject("Original character page", typeof(Canvas), typeof(CanvasScaler));
        lOCanvasObject.transform.SetParent(pOParent, false);

        mOCanvas = lOCanvasObject.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mOCanvas.sortingOrder = 41;
        mOCanvas.enabled = false;

        CanvasScaler lOScaler = lOCanvasObject.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        lOScaler.scaleFactor = 1f;

        // The page's parts in one group, which the classic frame's panel turn squeezes
        // (UWModernClassicFrame.ApplySquash).
        GameObject lOContent = new GameObject("Content", typeof(RectTransform));
        lOContent.transform.SetParent(lOCanvasObject.transform, false);
        mOContent = (RectTransform)lOContent.transform;
        mOContent.anchorMin = Vector2.zero;
        mOContent.anchorMax = Vector2.zero;
        mOContent.pivot = Vector2.zero;
        mOContent.sizeDelta = Vector2.zero;

        Transform pORoot = mOContent;

        mOPage = fCreateImage(pORoot, "Inventory page");
        UWPixelArtUI.Apply(mOPage);

        mOArmour = new RawImage[6];

        for (int liAt = 0; liAt < mOArmour.Length; liAt++)
        {
            mOArmour[liAt] = fCreateImage(pORoot, "Paperdoll " + liAt);
            UWPixelArtUI.Apply(mOArmour[liAt]);
        }

        mOIcons = new RawImage[msIconSlots.Length];

        for (int liAt = 0; liAt < mOIcons.Length; liAt++)
        {
            mOIcons[liAt] = fCreateImage(pORoot, "Equipped " + msIconSlots[liAt].Slot);
            UWIconPalette.Apply(mOIcons[liAt]);
        }

        mOContainerBack = fCreateImage(pORoot, "Container back");
        UWPixelArtUI.Apply(mOContainerBack);

        mOBackpack = new RawImage[UWInventoryModel.BackpackSize];

        for (int liAt = 0; liAt < mOBackpack.Length; liAt++)
        {
            mOBackpack[liAt] = fCreateImage(pORoot, "Backpack " + liAt);
            UWIconPalette.Apply(mOBackpack[liAt]);
        }

        mOCounts = new Text[UWInventoryModel.BackpackSize + msIconSlots.Length];

        for (int liAt = 0; liAt < mOCounts.Length; liAt++)
            mOCounts[liAt] = fCreateText(pORoot, "Count " + liAt);

        mOContainerIcon = fCreateImage(pORoot, "Open container");
        UWIconPalette.Apply(mOContainerIcon);
        mOScrollUp = fCreateImage(pORoot, "Container scroll up");
        UWPixelArtUI.Apply(mOScrollUp);
        mOScrollDown = fCreateImage(pORoot, "Container scroll down");
        UWPixelArtUI.Apply(mOScrollDown);
    }

    /// <summary>Once a frame while the modern HUD shows; pfPixelScale is UWModernHud.PixelScale.</summary>
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

        if (mOPageTexture == null || miArtVersion != UWColourVision.Version)
        {
            if (mOPageTexture != null)
                Object.Destroy(mOPageTexture);

            mOPageTexture = UWModernHudArt.BuildPanelPage(mOUi.mOUWData.Textures, 0, mOUi.TextureFilterMode);
            fRebuild(ref mOContainerBackTexture, UWModernHudArt.BuildPicture(mOUi.mOUWData.Textures, UWTexture.TextureTypes.INV, 6, mOUi.TextureFilterMode));
            fRebuild(ref mOScrollUpTexture, UWModernHudArt.BuildPicture(mOUi.mOUWData.Textures, UWTexture.TextureTypes.BUTTONS, 27, mOUi.TextureFilterMode));
            fRebuild(ref mOScrollDownTexture, UWModernHudArt.BuildPicture(mOUi.mOUWData.Textures, UWTexture.TextureTypes.BUTTONS, 28, mOUi.TextureFilterMode));
            miArtVersion = UWColourVision.Version;
        }

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
        fSetRect(mOPage.rectTransform, lOPlaced.x, lOPlaced.y, lOPlaced.width, lOPlaced.height);

        UWCharacter lOCharacter = mOUi.mCharacter;

        fPlace(mOArmour[0], lOCharacter.CharTexture, msBodyAt);
        fPlace(mOArmour[1], lOCharacter.HelmetTexture, msHelmetAt);
        fPlace(mOArmour[2], lOCharacter.GlovesTexture, msGlovesAt);
        // The legs' picture carries its drop itself (UWCharacter.LegsDrop).
        fPlace(mOArmour[3], lOCharacter.LegsArmorTexture, msLegsAt);
        fPlace(mOArmour[4], lOCharacter.ChestArmorTexture, msChestAt);
        fPlace(mOArmour[5], lOCharacter.BootsTexture, msBootsAt);

        UWInventory lOInventory = mOUi.mOInventory;

        for (int liSlot = 0; liSlot < msIconSlots.Length; liSlot++)
        {
            (UWArmorItemMap.BodySlot leSlot, float lfX, float lfY, float lfSize) = msIconSlots[liSlot];
            UWObject lOItem = lOInventory != null ? lOInventory.GetEquipped(leSlot) : null;

            fPlaceIcon(mOIcons[liSlot], lOItem, lfX + (lfSize * 0.5f), lfY + (lfSize * 0.5f) - (lfSize < 16f ? RingIconYOffset : 0f));
            fPlaceCount(mOCounts[UWInventoryModel.BackpackSize + liSlot], lOItem, lfX + Mathf.Max(lfSize, 16f), lfY + Mathf.Max(lfSize, 16f));
        }

        // The backpack's things in the eight circles - the slots' areas from the original's table
        // (y counted from the bottom of the frame) - or the open container's from its offset on.
        UWInventoryModel lOModel = lOInventory != null ? lOInventory.Model : null;
        UWObject lOOpen = lOModel != null ? lOModel.OpenContainer : null;

        fPlaceContainerView(lOModel, lOOpen);

        for (int liSlot = 0; liSlot < mOBackpack.Length; liSlot++)
        {
            UWObject lOItem = lOModel == null ? null : lOOpen != null ? lOModel.GetContainerItem(liSlot) : lOModel.Backpack[liSlot];
            UWClickRules.Area lOArea = fBackpackArea(liSlot);

            fPlaceIcon(mOBackpack[liSlot], lOItem, (lOArea.X0 + lOArea.X1 + 1) * 0.5f, 199.5f - ((lOArea.Y0 + lOArea.Y1) * 0.5f));
            fPlaceCount(mOCounts[liSlot], lOItem, lOArea.X1 + 1, 200 - lOArea.Y0);
        }
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

        mOContainerBack.enabled = false;
        mOContainerIcon.enabled = false;
        mOScrollUp.enabled = false;
        mOScrollDown.enabled = false;
    }

    /// <summary>The open container's back, icon and arrows at the classic frame's places, or none.</summary>
    private void fPlaceContainerView(UWInventoryModel pOModel, UWObject pOOpen)
    {
        bool lbOpen = pOOpen != null;
        float lfAspect = miScale * UWModernHudArt.PixelAspectX;

        mOContainerBack.enabled = lbOpen && mOContainerBackTexture != null;

        if (mOContainerBack.enabled)
        {
            mOContainerBack.texture = mOContainerBackTexture;
            fSetRect(mOContainerBack.rectTransform, ScreenRect.x + ((ContainerBackX - PageX) * lfAspect),
                ScreenRect.yMax - ((ContainerBackY - PageY + mOContainerBackTexture.height) * miScale),
                mOContainerBackTexture.width * lfAspect, mOContainerBackTexture.height * miScale);
        }

        UWTexture lOSource = lbOpen ? pOOpen.Icon ?? pOOpen.Texture : null;

        if (lOSource != null)
        {
            Texture2D lOIcon = fIcon(lOSource);

            mOContainerIcon.texture = lOIcon;
            mOContainerIcon.enabled = true;
            fSetRect(mOContainerIcon.rectTransform, ScreenRect.x + ((ContainerIconX - PageX) * lfAspect),
                ScreenRect.yMax - ((ContainerIconBottom - PageY) * miScale), lOIcon.width * lfAspect, lOIcon.height * miScale);
        }
        else
            mOContainerIcon.enabled = false;

        bool lbUp = lbOpen && pOModel.CanIncreaseContainerScrollOffset && mOScrollUpTexture != null;
        bool lbDown = lbOpen && pOModel.CanDecreaseContainerScrollOffset && mOScrollDownTexture != null;
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
        fSetRect(pOImage.rectTransform, ScreenRect.x + ((pfRight - pOTexture.width - PageX) * lfAspect),
            ScreenRect.yMax - ((ScrollBottom - PageY) * miScale), pOTexture.width * lfAspect, pOTexture.height * miScale);
    }

    private static void fRebuild(ref Texture2D pOTexture, Texture2D pONew)
    {
        if (pOTexture != null)
            Object.Destroy(pOTexture);

        pOTexture = pONew;
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

        UWArmorItemMap.BodySlot? leSlot = UWHudInventory.GetSlotOfSpot(leSpot);

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

        UWClickRules.Area lOArea = fBackpackArea(piSlot);
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

    /// <summary>The spot of the page under a screen point, in the frame's coordinates.</summary>
    private bool fSpotAt(Vector2 pOPointer, out UWClickRules.InventorySpot peSpot)
    {
        peSpot = UWClickRules.InventorySpot.None;

        if (!Contains(pOPointer))
            return false;

        int liX = PageX + Mathf.FloorToInt((pOPointer.x - ScreenRect.x) / (miScale * UWModernHudArt.PixelAspectX));
        int liRow = PageY + Mathf.FloorToInt((ScreenRect.yMax - pOPointer.y) / miScale);

        peSpot = UWClickRules.InventorySpotAt(liX, 199 - liRow);

        return peSpot != UWClickRules.InventorySpot.None;
    }

    /// <summary>The original's area of a backpack slot.</summary>
    private static UWClickRules.Area fBackpackArea(int piSlot)
    {
        // The table's own areas: the first row from x 240 in steps of 19, the second below it.
        int liX0 = 240 + ((piSlot % 4) * 19);
        int liY0 = piSlot < 4 ? 101 : 83;

        return new UWClickRules.Area(liX0, liY0, liX0 + 17, liY0 + 17);
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
        fSetRect(pOImage.rectTransform, ScreenRect.x + ((pOAt.x - PageX) * lfAspect),
            ScreenRect.yMax - ((pOAt.y - PageY + pOTexture.height) * miScale), pOTexture.width * lfAspect, pOTexture.height * miScale);
    }

    /// <summary>A thing's icon centred on a point of the frame (MAIN.BYT pixels, rows from the top).</summary>
    private void fPlaceIcon(RawImage pOImage, UWObject pOItem, float pfCentreX, float pfCentreRow)
    {
        UWTexture lOSource = pOItem != null ? pOItem.Icon ?? pOItem.Texture : null;

        if (lOSource == null)
        {
            pOImage.enabled = false;
            return;
        }

        Texture2D lOTexture = fIcon(lOSource);
        float lfAspect = miScale * UWModernHudArt.PixelAspectX;

        pOImage.texture = lOTexture;
        pOImage.enabled = true;
        fSetRect(pOImage.rectTransform, Mathf.Round(ScreenRect.x + ((pfCentreX - PageX - (lOTexture.width * 0.5f)) * lfAspect)),
            Mathf.Round(ScreenRect.yMax - ((pfCentreRow - PageY + (lOTexture.height * 0.5f)) * miScale)),
            lOTexture.width * lfAspect, lOTexture.height * miScale);
    }

    /// <summary>A stack's count at the bottom right of its slot (frame pixels: the slot's right
    /// edge and bottom row), none for a single thing.</summary>
    private void fPlaceCount(Text pOText, UWObject pOItem, float pfRight, float pfBottomRow)
    {
        int liCount = pOItem != null ? UWItemDescriptions.GetStackCount(pOItem) : 0;

        pOText.enabled = liCount > 1;

        if (liCount <= 1)
            return;

        float lfAspect = miScale * UWModernHudArt.PixelAspectX;
        float lfHeight = 10f * miScale;

        pOText.text = liCount.ToString();
        pOText.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * miScale));
        fSetRect(pOText.rectTransform, ScreenRect.x + ((pfRight - PageX) * lfAspect) - (40f * miScale) + miScale,
            ScreenRect.yMax - ((pfBottomRow - PageY) * miScale) - miScale, 40f * miScale, lfHeight);
    }

    private Text fCreateText(Transform pOParent, string psName)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Text), typeof(Outline));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        Text lOText = lOObject.GetComponent<Text>();
        lOText.font = UWInterfaceFont.Font;
        UWUiFonts.Register(lOText, mOUi);
        lOText.alignment = TextAnchor.LowerRight;
        lOText.color = new Color(0.86f, 0.86f, 0.84f);
        lOText.raycastTarget = false;
        lOText.horizontalOverflow = HorizontalWrapMode.Overflow;
        lOText.verticalOverflow = VerticalWrapMode.Overflow;
        lOText.enabled = false;

        Outline lOOutline = lOObject.GetComponent<Outline>();
        lOOutline.effectColor = new Color(0.16f, 0.09f, 0.04f, 0.9f);
        lOOutline.effectDistance = new Vector2(1.5f, -1.5f);

        return lOText;
    }

    /// <summary>An icon (palette indices, UWIconPalette), built once.</summary>
    private Texture2D fIcon(UWTexture pOSource)
    {
        if (!mOIconTextures.TryGetValue(pOSource, out Texture2D lOTexture) || lOTexture == null)
        {
            lOTexture = UWIconTextureBuilder.Build(pOSource, mOUi.TextureFilterMode);
            mOIconTextures[pOSource] = lOTexture;
        }

        return lOTexture;
    }

    private static RawImage fCreateImage(Transform pOParent, string psName)
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

    private static void fSetRect(RectTransform pORect, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        pORect.anchoredPosition = new Vector2(pfX, pfY);
        pORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }
}
