using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The HUD section "the paper doll slots, the backpack grid, the container view and the number labels".
/// Its own class since 2026-09-18 (stage two of the HUD rebuild); the owner hands in the game
/// data, the frame, the character, the interaction and the inventory through mOUi. UWGameUI
/// keeps the public entry points as forwards.
/// </summary>
public sealed class UWHudInventory
{
    private readonly UWGameUI mOUi;

    internal UWHudInventory(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private Image[] mBackpackSlotImages;
    internal UWObject[] mBackpackItemsShown;

    /// <summary>The picture currently shown per backpack slot - an item can change its
    /// picture without changing slot (combining, lighting). See
    /// fRefreshBackpackSlots.</summary>
    internal UWTexture[] mBackpackSourcesShown;

    // Original: opened container (see UWInventory.OpenContainer) - own panel,
    // laid by the user in the prefab editor exactly over the backpack grid (see
    // fBuildCanvas for the values taken over 1:1 there). Replaces the backpack visually AND
    // as hit target while a container is open (see fRefreshContainerVisibility/
    // GetBackpackSlotUnderMouse).
    // Public, since UWItemDrag derives the scroll step from it (a click on a
    // scroll arrow pages by one whole row, see UWItemDrag/UWInventory.ScrollContainer).
    public const int ContainerColumns = BackpackColumns;
    internal const int ContainerSlotCount = UWInventory.ContainerSlotCount;
    internal RectTransform mContainerPanelRect;
    private Image[] mContainerSlotImages;
    internal UWObject[] mContainerItemsShown;

    internal UWTexture[] mContainerSourcesShown;

    // Original: the picture of the open container, above and left-aligned to the container panel.
    // Left click on it closes (see UWItemDrag/IsContainerOpenIconUnderMouse). The
    // containers 128 to 139 show their open variant, urn, quiver and bowl (140 to
    // 142) have none and show themselves - see fGetContainerOpenIconId.
    private const float containerOpenIconLeft = 241f;
    private const float containerOpenIconTop = 80f;
    private Image mContainerOpenIconImage;
    private UWObject mContainerOpenIconShownFor;

    // Original: two scroll arrows at the container panel page its 8-slot window (see
    // UWInventory.ScrollContainer) - for containers with more than 8 contents (from the
    // original level state, not reachable by dropping, see DropCursorItemInContainer).
    // The graphic comes from BUTTONS.GR (27 up, 28 down, identified per user, loaded in Init);
    // fBuildCanvas only creates placeholder squares for the prefab. Position
    // CONFIRMED per user: same height as the container icon (containerOpenIconTop), but
    // right-aligned with the same ~5px margin as its left alignment, only measured from the right
    // panel edge (panel edge lies at about X 321, see ContainerInventory rect
    // in fBuildCanvas) - both side by side (not stacked), down arrow at the edge,
    // up arrow directly next to it.
    internal const float containerScrollTop = containerOpenIconTop;
    private const float containerScrollDownRight = 314f;
    internal const float containerScrollSize = 8f;

    // UpArrow needs, beyond merely leaning on "directly left of the DownArrow" (see Init),
    // some additional spacing to the left (CONFIRMED per user).
    private const float containerScrollUpExtraLeftOffset = 3f;
    private Image mContainerScrollUpImage;
    private Image mContainerScrollDownImage;

    // Fixed, texture-independent hit zones for equipping/unequipping by dragging (see
    // GetArmorHitZoneUnderMouse) - unlike the armour pictures themselves, these must
    // not shrink to the currently displayed texture. Roughly estimated (head/torso/
    // hands/legs/feet based on the character silhouette), not yet measured against the
    // original.
    //
    // From index ArmorSlotCount on (2026-08-25): ring/hand/use slots - unlike the first
    // five, these have no body-fitted paper doll sprite but show their own
    // item icon directly (like a backpack slot) - hit zone and display are
    // the same element for them, see mEquipSlotIcons/fRefreshEquipSlotIcons. Positions are pure
    // placeholders, not yet measured against the original.
    internal const int ArmorSlotCount = 5;
    private RectTransform[] mArmorHitZones;
    private Image[] mEquipSlotIcons;
    internal UWObject[] mEquipIconItemsShown;

    /// <summary>Additionally the shown PICTURE: a light source changes its picture when lit
    /// without the object changing - comparing only the object left
    /// the slot unchanged then (per user, 2026-09-05).</summary>
    internal UWTexture[] mEquipIconSourcesShown;
    internal static readonly UWArmorItemMap.BodySlot[] ArmorHitZoneSlots =
    {
        UWArmorItemMap.BodySlot.Helmet,
        UWArmorItemMap.BodySlot.Chest,
        UWArmorItemMap.BodySlot.Gloves,
        UWArmorItemMap.BodySlot.Legs,
        UWArmorItemMap.BodySlot.Boots,
        UWArmorItemMap.BodySlot.LeftRing,
        UWArmorItemMap.BodySlot.RightRing,
        UWArmorItemMap.BodySlot.RightHandSlot,    // was MainHand
        UWArmorItemMap.BodySlot.LeftHandSlot,     // was OffHand
        UWArmorItemMap.BodySlot.LeftOffHandSlot,  // was LeftUse
        UWArmorItemMap.BodySlot.RightOffHandSlot, // was RightUse
    };

    // Sits in the paper doll panel itself (4 columns x 2 rows, baked as circles into the
    // original background picture), not as a separate bar - roughly measured per
    // DOSBox screenshot comparison (window ~2x native 320x200,
    // content starts about 20px below the title bar), not yet pixel-exact -
    // to be corrected per further screenshot comparison.
    private const int BackpackColumns = 4;
    private const int BackpackRows = 2;
    internal const int BackpackSlotCount = UWInventory.BackpackSize;
    private const float backpackSlotSize = 14f;
    private const float backpackSlotSpacingX = 5f;
    private const float backpackSlotSpacingY = 4f;
    private const float backpackGridTop = 83f;
    private const float backpackGridLeft = 242f;

    /// <summary>
    /// Three edge conversions were tried here and discarded again (2026-08-26)
    /// - but the actual bug was not in mOUi method, it was one level deeper:
    /// UWPalette recognised the transparency marker (identified per user pixel inspection as dark blue,
    /// HSV 240/100/1) wrongly via "red channel == 0" - that also applies
    /// to real edge pixels deliberately drawn black, so that real black
    /// AND the marker alike became transparent (see UWPalette.cs constructor,
    /// now corrected to "red=green=0 AND blue&gt;0"). With the now correct alpha source
    /// the icon carries its edge by itself - UWIconTextureBuilder no longer adds an
    /// extra outline (see there), identical to all other
    /// inventory icons.
    /// </summary>
    private void fSetContainerScrollArrowSprite(Image pOImage, UWTexture pOSource)
    {
        if (pOImage == null || pOSource == null)
            return;

        Texture2D lOTexture = UWIconTextureBuilder.Build(pOSource, mOUi.TextureFilterMode);

        pOImage.sprite = Sprite.Create(lOTexture, new Rect(0f, 0f, lOTexture.width, lOTexture.height), new Vector2(0.5f, 0.5f), 1f);
        pOImage.color = Color.white;
        UWIconPalette.Apply(pOImage);
        ((RectTransform)pOImage.transform).sizeDelta = new Vector2(lOTexture.width, lOTexture.height);
    }

    /// <summary>Occupied ring/hand/use slots as icons - exactly like fRefreshBackpackSlots,
    /// only via UWInventory.GetEquipped instead of Backpack[i].</summary>
    private void fRefreshEquipSlotIcons()
    {
        if (mOUi.mOInventory == null || mEquipSlotIcons == null)
            return;

        for (int i = 0; i < mEquipSlotIcons.Length; i++)
        {
            UWObject lOItem = mOUi.mOInventory.GetEquipped(ArmorHitZoneSlots[ArmorSlotCount + i]);

            // The stack count depends on the QUANTITY of the item, and that changes without
            // a different item lying in the slot. It is therefore updated BEFORE the shortcut
            // further below (per user: in the hand slots it was missing entirely,
            // 2026-09-05).
            fRefreshEquipStackCount(i, lOItem);

            UWTexture lOSource = lOItem?.Icon ?? lOItem?.Texture;

            if (lOItem == mEquipIconItemsShown[i] && lOSource == mEquipIconSourcesShown[i])
                continue;

            mEquipIconItemsShown[i] = lOItem;
            mEquipIconSourcesShown[i] = lOSource;

            Image lOImage = mEquipSlotIcons[i];

            if (lOSource == null)
            {
                lOImage.sprite = null;
                lOImage.enabled = false;
                continue;
            }

            // A burning torch or candle flickers - not as a picture sequence, but because
            // the original rotates colour ranges in a circle (see UWPaletteRotation). The lantern
            // drops out by itself, its colours lie outside.
            Texture2D lOTexture = UWIconTextureBuilder.Build(lOSource, mOUi.TextureFilterMode);

            UWIconPalette.Apply(lOImage);

            lOImage.sprite = Sprite.Create(lOTexture, new Rect(0f, 0f, lOTexture.width, lOTexture.height), new Vector2(0.5f, 0.5f), 1f);
            lOImage.enabled = true;
        }
    }

    /// <summary>Original: hides/shows backpack and container grid mutually, depending
    /// on whether UWInventory.OpenContainer is currently set - both grids lie
    /// congruently on top of each other (see fBuildCanvas), so never both
    /// active at once. Only switched on an actual state change (SetActive per
    /// frame is otherwise needless).</summary>
    private void fRefreshContainerVisibility()
    {
        if (mContainerPanelRect == null || mBackpackSlotImages == null)
            return;

        bool lbOpen = mOUi.mOInventory != null && mOUi.mOInventory.OpenContainer != null;

        if (lbOpen == mContainerPanelRect.gameObject.activeSelf)
            return;

        mContainerPanelRect.gameObject.SetActive(lbOpen);

        foreach (Image lOIcon in mContainerSlotImages)
            lOIcon.transform.parent.gameObject.SetActive(lbOpen);

        foreach (Image lOIcon in mBackpackSlotImages)
            lOIcon.transform.parent.gameObject.SetActive(!lbOpen);
    }

    /// <summary>Occupied container slots as icons - exactly like fRefreshBackpackSlots, only
    /// via UWInventory.GetContainerItem instead of Backpack[i]. With the container closed it keeps
    /// its last state (slots are invisible then anyway, see
    /// fRefreshContainerVisibility) instead of clearing - saves needless sprite changes
    /// on the next opening of the same container.</summary>
    private void fRefreshContainerSlots()
    {
        if (mOUi.mOInventory == null || mContainerSlotImages == null || mOUi.mOInventory.OpenContainer == null)
            return;

        for (int i = 0; i < mContainerSlotImages.Length; i++)
        {
            UWObject lODisplayItem = mOUi.mOInventory.GetContainerItem(i);

            fRefreshContainerStackCount(i, lODisplayItem);

            UWTexture lOSource = lODisplayItem?.Icon ?? lODisplayItem?.Texture;

            if (lODisplayItem == mContainerItemsShown[i] && lOSource == mContainerSourcesShown[i])
                continue;

            mContainerItemsShown[i] = lODisplayItem;
            mContainerSourcesShown[i] = lOSource;

            Image lOImage = mContainerSlotImages[i];

            if (lOSource == null)
            {
                lOImage.sprite = null;
                lOImage.enabled = false;
                continue;
            }

            Texture2D lOTexture = UWIconTextureBuilder.Build(lOSource, mOUi.TextureFilterMode);

            UWIconPalette.Apply(lOImage);

            lOImage.sprite = Sprite.Create(lOTexture, new Rect(0f, 0f, lOTexture.width, lOTexture.height), new Vector2(0.5f, 0.5f), 1f);
            lOImage.enabled = true;
        }
    }

    /// <summary>For UWItemDrag: which container slot (if any) lies under the
    /// given screen position - returns -1 as long as no container is open (even
    /// if the hit zones themselves, congruent with the backpack, would hit
    /// geometrically).</summary>
    public int GetContainerSlotUnderMouse(Vector2 pOScreenPos)
    {
        // Turned away: see fIsInventoryClickable.
        if (!mOUi.Panel.fIsInventoryClickable())
            return -1;

        if (mContainerSlotImages == null || mOUi.mOInventory == null || mOUi.mOInventory.OpenContainer == null)
            return -1;

        // The container's slots lie on the backpack's (the same records of the table).
        int liSlot = UWClickRules.BackpackSlotOf(fSpotAt(pOScreenPos));

        return liSlot < mContainerSlotImages.Length ? liSlot : -1;
    }

    /// <summary>
    /// THE SPOT UNDER THE POINTER from the original's table of the inventory page
    /// (UWClickRules.InventorySpotAt, read 2026-09-26) - it replaced our hit areas, which
    /// were drawn after screenshots and the paper doll's pictures: the equipment areas were
    /// estimates, the rings 4 by 4 pixels, the backpack circles 14 wide where the original
    /// takes 18.
    /// </summary>
    private UWClickRules.InventorySpot fSpotAt(Vector2 pOScreenPos)
    {
        if (!mOUi.TryGetOriginalPoint(pOScreenPos, out int liX, out int liY)
            || !UWClickRules.InventoryPage.Contains(liX, liY))
            return UWClickRules.InventorySpot.None;

        return UWClickRules.InventorySpotAt(liX, liY);
    }

    /// <summary>For UWItemDrag: the container item at a given slot in the currently
    /// open container.</summary>
    public UWObject GetContainerItem(int piSlot)
    {
        return mOUi.mOInventory?.GetContainerItem(piSlot);
    }

    /// <summary>Last container with its own open variant: sack to gold coffer, 128 to
    /// 139. For these, bit 0 of the object number means "open".</summary>
    private const int LastContainerWithOpenVariant = 139;

    /// <summary>
    /// Which picture stands above the open container - as in the reference
    /// (container.Use): the containers 128 to 139 show their open variant (bit 0 set),
    /// urn, quiver and bowl (140 to 142) have none and show themselves.
    ///
    /// Until 2026-09-11 the picture stayed invisible for these three. But it is also the
    /// button for closing - you could no longer get out of an opened bowl (per
    /// user). And instead of "+ 1" it now says "| 1": an already open number would otherwise
    /// not stay itself but jump to the next container.
    /// </summary>
    private static int fGetContainerOpenIconId(int piObjectId)
    {
        return piObjectId <= LastContainerWithOpenVariant ? piObjectId | 0x1 : piObjectId;
    }

    /// <summary>Original: the picture of the currently open container (OBJECTS.GR, see
    /// fGetContainerOpenIconId) - only reloaded on an actual change, as with the
    /// other icon refreshes. If an entry is missing, the icon stays invisible instead of
    /// crashing.</summary>
    private void fRefreshContainerOpenIcon()
    {
        if (mContainerOpenIconImage == null)
            return;

        UWObject lOContainer = mOUi.mOInventory?.OpenContainer;

        if (lOContainer == mContainerOpenIconShownFor)
            return;

        mContainerOpenIconShownFor = lOContainer;

        UWTexture lOSource = null;

        if (lOContainer != null && mOUi.mOUWData != null)
        {
            try
            {
                lOSource = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS,
                    fGetContainerOpenIconId(lOContainer.ID));
            }
            catch
            {
                lOSource = null;
            }
        }

        if (lOSource == null)
        {
            mContainerOpenIconImage.enabled = false;
            return;
        }

        Texture2D lOTexture = UWIconTextureBuilder.Build(lOSource, mOUi.TextureFilterMode);

        mContainerOpenIconImage.sprite = Sprite.Create(lOTexture, new Rect(0f, 0f, lOTexture.width, lOTexture.height), new Vector2(0.5f, 0.5f), 1f);
        UWIconPalette.Apply(mContainerOpenIconImage);
        ((RectTransform)mContainerOpenIconImage.transform).sizeDelta = new Vector2(lOTexture.width, lOTexture.height);
        mContainerOpenIconImage.enabled = true;
    }

    /// <summary>For UWItemDrag: does the given screen position lie on the
    /// container "opened" icon - only if it is actually visible right now (otherwise
    /// its rect, which persists independent of visibility, would wrongly
    /// hit).</summary>
    public bool IsContainerOpenIconUnderMouse(Vector2 pOScreenPos)
    {
        // Turned away: see fIsInventoryClickable.
        if (!mOUi.Panel.fIsInventoryClickable())
            return false;

        if (mContainerOpenIconImage == null || !mContainerOpenIconImage.enabled)
            return false;

        return fSpotAt(pOScreenPos) == UWClickRules.InventorySpot.ContainerIcon;
    }

    /// <summary>Original: the scroll arrows are only visible/active as long as there is anything
    /// left to page in the respective direction. The UpArrow INCREASES the
    /// offset (see UWInventory.ScrollContainer/CanIncreaseContainerScrollOffset), so it shows
    /// when more content remains afterwards - not to be confused with a
    /// literal "CanScrollUp" reading. Only switched anew on an actual change.</summary>
    private void fRefreshContainerScrollButtons()
    {
        if (mContainerScrollUpImage == null || mContainerScrollDownImage == null || mOUi.mOInventory == null)
            return;

        bool lbCanUp = mOUi.mOInventory.OpenContainer != null && mOUi.mOInventory.CanIncreaseContainerScrollOffset;
        bool lbCanDown = mOUi.mOInventory.OpenContainer != null && mOUi.mOInventory.CanDecreaseContainerScrollOffset;

        if (mContainerScrollUpImage.gameObject.activeSelf != lbCanUp)
            mContainerScrollUpImage.gameObject.SetActive(lbCanUp);

        if (mContainerScrollDownImage.gameObject.activeSelf != lbCanDown)
            mContainerScrollDownImage.gameObject.SetActive(lbCanDown);
    }

    /// <summary>For UWItemDrag - only if the arrow is actually visible/active right now
    /// (see fRefreshContainerScrollButtons), same pattern as
    /// IsContainerOpenIconUnderMouse.</summary>
    public bool IsContainerScrollUpUnderMouse(Vector2 pOScreenPos)
    {
        // Turned away: see fIsInventoryClickable.
        if (!mOUi.Panel.fIsInventoryClickable())
            return false;

        if (mContainerScrollUpImage == null || !mContainerScrollUpImage.gameObject.activeSelf)
            return false;

        // The up arrow is the left one of the two (see BuildContainerView).
        return fSpotAt(pOScreenPos) == UWClickRules.InventorySpot.ScrollLeft;
    }

    public bool IsContainerScrollDownUnderMouse(Vector2 pOScreenPos)
    {
        // Turned away: see fIsInventoryClickable.
        if (!mOUi.Panel.fIsInventoryClickable())
            return false;

        if (mContainerScrollDownImage == null || !mContainerScrollDownImage.gameObject.activeSelf)
            return false;

        return fSpotAt(pOScreenPos) == UWClickRules.InventorySpot.ScrollRight;
    }

    /// <summary>Number labels at the backpack slots for stacks, and the total load.</summary>
    private RawImage[] mBackpackCountLabels;

    private RawImage[] mContainerCountLabels;

    private RawImage mWeightLabel;

    /// <summary>
    /// Writes the item count into the corner of a backpack slot. Single items get
    /// no number - only what is there multiple times.
    /// </summary>
    private void fRefreshStackCount(int piSlot, UWObject pOItem)
    {
        if (mBackpackSlotImages == null || piSlot < 0 || piSlot >= mBackpackSlotImages.Length)
            return;

        if (mBackpackCountLabels == null)
            mBackpackCountLabels = new RawImage[mBackpackSlotImages.Length];

        fSetStackLabel(ref mBackpackCountLabels[piSlot], mBackpackSlotImages[piSlot], pOItem);
    }

    /// <summary>The same for the hand and ring slots - a stack can lie there too,
    /// such as torches in the hand.</summary>
    private void fRefreshEquipStackCount(int piSlot, UWObject pOItem)
    {
        if (mEquipSlotIcons == null || piSlot < 0 || piSlot >= mEquipSlotIcons.Length)
            return;

        if (mEquipCountLabels == null)
            mEquipCountLabels = new RawImage[mEquipSlotIcons.Length];

        fSetStackLabel(ref mEquipCountLabels[piSlot], mEquipSlotIcons[piSlot], pOItem);
    }

    private RawImage[] mEquipCountLabels;

    /// <summary>The same for the slots of an opened container - that is where
    /// stacked things mostly lie.</summary>
    private void fRefreshContainerStackCount(int piSlot, UWObject pOItem)
    {
        if (mContainerSlotImages == null || piSlot < 0 || piSlot >= mContainerSlotImages.Length)
            return;

        if (mContainerCountLabels == null)
            mContainerCountLabels = new RawImage[mContainerSlotImages.Length];

        fSetStackLabel(ref mContainerCountLabels[piSlot], mContainerSlotImages[piSlot], pOItem);
    }

    private void fSetStackLabel(ref RawImage pOLabel, Image pOSlotIcon, UWObject pOItem)
    {
        int liCount = fGetStackCount(pOItem);

        RawImage lOLabel = pOLabel;

        if (liCount <= 1)
        {
            UWTextLabel.Hide(lOLabel);

            return;
        }

        if (lOLabel == null)
        {
            lOLabel = fCreateNumberLabel(pOSlotIcon.transform.parent);
            pOLabel = lOLabel;
        }

        // The small font (font4x5p) - in the original the number on a stack looks
        // superscript, not like normal text (per user, 2026-09-01).
        fSetNumberLabel(lOLabel, liCount.ToString(), UWFonts.FontType.Small);

        lOLabel.transform.SetAsLastSibling();
    }

    /// <summary>Item count of a stack. From 512 on the field is a special property.</summary>
    private static int fGetStackCount(UWObject pOItem)
    {
        if (pOItem == null || !pOItem.HasQuantity || pOItem.Quantity >= 512)
            return 1;

        return pOItem.Quantity < 1 ? 1 : pOItem.Quantity;
    }

    /// <summary>
    /// The remaining carrying capacity (maximum weight minus carried load), in whole stones.
    /// Counted as load are equipment, backpack and the contents of containers - see
    /// UWInventoryWeight.
    /// </summary>
    private void fRefreshWeight()
    {
        if (mOUi.mOInventory == null || mOUi.mOUWData == null || mOUi.mGameFrame == null)
            return;

        // What is shown is the remaining carrying capacity, not the carried load.
        //
        // Computed from the inventory, so that the number follows every move. The
        // difference to the load the save game keeps lies in
        // UWInventory.WeightOffsetTenthStones - see there for why.
        UWPlayerData lOPlayer = mOUi.mOUWData.InitialPlayer;

        int liCarried = mOUi.mOInventory.GetCarriedTenthStones(mOUi.mOUWData.CommonObjectProperties);

        int liTenthStones = (lOPlayer != null ? lOPlayer.MaxWeight : 0) - liCarried;

        if (liTenthStones < 0)
            liTenthStones = 0;

        if (mWeightLabel == null)
        {
            mWeightLabel = fCreateNumberLabel(mOUi.mGameFrame);

            // Centred reference point: in the original the number stands centred in the circle, so a
            // two-digit one grows to both sides (per user, 2026-09-01).
            // mOWeightLabelPosition is therefore the CENTRE, not the top-left corner.
            RectTransform lORect = (RectTransform)mWeightLabel.transform;
            lORect.anchorMin = new Vector2(0f, 1f);
            lORect.anchorMax = new Vector2(0f, 1f);
            lORect.pivot = new Vector2(0.5f, 0.5f);
            lORect.anchoredPosition = new Vector2(mOUi.mOWeightLabelPosition.x, -mOUi.mOWeightLabelPosition.y);
        }

        // The original shows whole stones, no decimal place (per screenshot from the
        // user, 2026-09-01: it shows 7 there).
        fSetNumberLabel(mWeightLabel, (liTenthStones / 10).ToString(), UWFonts.FontType.Normal, mOUi.mOWeightColour,
            WeightRaise);
    }

    private RawImage fCreateNumberLabel(Transform pOParent)
    {
        GameObject lOObject = new GameObject("Number", typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pOParent, false);

        // Top left at the slot, as in the original.
        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);

        // One point to the right, otherwise the number sticks to the edge. Was two while the
        // character spacing was one point too wide (per user, 2026-09-01 and 2026-09-03).
        lORect.anchoredPosition = new Vector2(1f, 0f);

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;

        return lOImage;
    }

    private void fSetNumberLabel(RawImage pOLabel, string psText, UWFonts.FontType peFont)
    {
        fSetNumberLabel(pOLabel, psText, peFont, mOUi.mOCountColour, QuantityRaise);
    }

    /// <summary>What a number label currently shows - text, font, colour - and its texture.
    /// Carrying capacity and stack counts are set every frame; without mOUi cache
    /// every frame created a new texture that was never released (per user, 2026-09-13: over
    /// 900,000 textures 28x17 and 8x6 in the log of UWResourceWatch, the game ran
    /// increasingly unevenly).</summary>
    private readonly System.Collections.Generic.Dictionary<RawImage, System.Collections.Generic.KeyValuePair<string, Texture2D>>
        mONumberLabelCache = new System.Collections.Generic.Dictionary<RawImage, System.Collections.Generic.KeyValuePair<string, Texture2D>>();

    /// <summary>The numbers' modern font against the other modern fields, judged by eye (per
    /// user, 2026-09-28): the weight two canvas units higher, the quantities at the same height
    /// (two higher was too much for them), both one to the left. The weight was judged right
    /// after loading, when the capital gap was still measured coarsely (see UWTextLabel.
    /// fGetCapitalTopGap) and came out about three quarters of a unit lower than at every later
    /// change - so lower now that the measurement is the same at every moment: 1.25 was still one
    /// unit too high (per user), 0.25 it is.</summary>
    private const float WeightRaise = 0.25f;

    private const float QuantityRaise = 0f;

    private const float NumberShiftRight = -1f;

    private void fSetNumberLabel(RawImage pOLabel, string psText, UWFonts.FontType peFont, Color pOColour,
        float pfRaise)
    {
        UWFont lOFont = mOUi.mOUWData != null ? mOUi.mOUWData.Fonts.Get(peFont) : null;

        if (lOFont == null || pOLabel == null)
            return;

        string lsKey = psText + "|" + (int)peFont + "|" + ColorUtility.ToHtmlStringRGBA(pOColour)
            + "|" + (int)mOUi.TextureFilterMode;

        System.Collections.Generic.KeyValuePair<string, Texture2D> lOCached;
        bool lbCached = mONumberLabelCache.TryGetValue(pOLabel, out lOCached);

        if (lbCached && lOCached.Key == lsKey && lOCached.Value != null && pOLabel.texture == lOCached.Value)
        {
            UWTextLabel.Get(pOLabel).Show();
            return;
        }

        Texture2D lOTexture = UWFontRenderer.RenderLines(lOFont,
            new System.Collections.Generic.List<string> { psText },
            psText.Length * (lOFont.Height + 2), pOColour, mOUi.TextureFilterMode);

        if (lOTexture == null)
            return;

        if (lbCached && lOCached.Value != null && lOCached.Value != lOTexture)
            Object.Destroy(lOCached.Value);

        mONumberLabelCache[pOLabel] = new System.Collections.Generic.KeyValuePair<string, Texture2D>(lsKey, lOTexture);

        // Bold in the modern font - the numbers are small, mOUi keeps them readable
        // (per user, 2026-09-11). Moved by pfRaise and NumberShiftRight (see WeightRaise).
        UWTextLabel.Get(pOLabel).Set(lOFont, lOTexture,
            new System.Collections.Generic.List<string> { psText },
            new System.Collections.Generic.List<Color32> { pOColour }, true, FontStyle.Bold,
            UWTextLabel.ModernExtraRaise + pfRaise, NumberShiftRight);
    }

    /// <summary>Occupied backpack slots as icons - unlike fUpdateSprite the
    /// RectTransform size is NOT set to the texture size, the slots keep their
    /// fixed tile size (backpackSlotSize).</summary>
    private void fRefreshBackpackSlots()
    {
        if (mOUi.mOInventory == null || mBackpackSlotImages == null)
            return;

        for (int i = 0; i < mBackpackSlotImages.Length; i++)
        {
            // An item currently being dragged is already removed from UWInventory.Backpack (see
            // UWInventory.CursorItem/BeginDragFrom*), no hiding per reference comparison
            // needed here - the array is the complete truth during a drag.
            UWObject lODisplayItem = i < mOUi.mOInventory.Backpack.Length ? mOUi.mOInventory.Backpack[i] : null;

            fRefreshStackCount(i, lODisplayItem);

            UWTexture lOSource = lODisplayItem?.Icon ?? lODisplayItem?.Texture;

            if (lODisplayItem == mBackpackItemsShown[i] && lOSource == mBackpackSourcesShown[i])
                continue;

            mBackpackItemsShown[i] = lODisplayItem;
            mBackpackSourcesShown[i] = lOSource;

            Image lOImage = mBackpackSlotImages[i];

            if (lOSource == null)
            {
                lOImage.sprite = null;
                lOImage.enabled = false;
                continue;
            }

            Texture2D lOTexture = UWIconTextureBuilder.Build(lOSource, mOUi.TextureFilterMode);

            UWIconPalette.Apply(lOImage);

            lOImage.sprite = Sprite.Create(lOTexture, new Rect(0f, 0f, lOTexture.width, lOTexture.height), new Vector2(0.5f, 0.5f), 1f);
            lOImage.enabled = true;
        }
    }

    /// <summary>For UWItemDrag: which backpack slot (if any) lies under the
    /// given screen position - returns -1 as long as a container is open (its
    /// panel lies congruently above, see GetContainerSlotUnderMouse), even if the
    /// backpack hit zones themselves (only hidden via SetActive, see
    /// fRefreshContainerVisibility) would still hit geometrically.</summary>
    public int GetBackpackSlotUnderMouse(Vector2 pOScreenPos)
    {
        // Turned away: see fIsInventoryClickable.
        if (!mOUi.Panel.fIsInventoryClickable())
            return -1;

        if (mBackpackSlotImages == null || (mOUi.mOInventory != null && mOUi.mOInventory.OpenContainer != null))
            return -1;

        int liSlot = UWClickRules.BackpackSlotOf(fSpotAt(pOScreenPos));

        return liSlot < mBackpackSlotImages.Length ? liSlot : -1;
    }

    /// <summary>
    /// For UWItemDrag: which equipment slot (if any) lies under the
    /// given screen position - via fixed hit zones (mArmorHitZones), not
    /// via the individual armour pictures themselves: their RectTransform size is set by
    /// fUpdateSprite to the currently displayed texture, and for an
    /// empty slot that is the tiny 1x1 placeholder texture ("no item") - the hit area
    /// would thus be practically unhittable until something is already equipped there.
    /// </summary>
    public UWArmorItemMap.BodySlot? GetArmorHitZoneUnderMouse(Vector2 pOScreenPos)
    {
        // Turned away: see fIsInventoryClickable.
        if (!mOUi.Panel.fIsInventoryClickable())
            return null;

        if (mArmorHitZones == null)
            return null;

        return GetSlotOfSpot(fSpotAt(pOScreenPos));
    }

    /// <summary>The equipment slot a spot of the inventory page stands for, or null - also for
    /// the modern character panel (UWModernPanel), which shows the same page.</summary>
    internal static UWArmorItemMap.BodySlot? GetSlotOfSpot(UWClickRules.InventorySpot peSpot)
    {
        // By where the spot lies: "Left" is the screen's left, the character's right - the
        // same sides as our slots (RightHandSlot is drawn on the left, see BuildEquipmentSlots).
        switch (peSpot)
        {
            case UWClickRules.InventorySpot.Helmet: return UWArmorItemMap.BodySlot.Helmet;
            case UWClickRules.InventorySpot.Chest: return UWArmorItemMap.BodySlot.Chest;
            case UWClickRules.InventorySpot.Gloves: return UWArmorItemMap.BodySlot.Gloves;
            case UWClickRules.InventorySpot.Legs: return UWArmorItemMap.BodySlot.Legs;
            case UWClickRules.InventorySpot.Boots: return UWArmorItemMap.BodySlot.Boots;
            case UWClickRules.InventorySpot.ShoulderLeft: return UWArmorItemMap.BodySlot.RightOffHandSlot;
            case UWClickRules.InventorySpot.ShoulderRight: return UWArmorItemMap.BodySlot.LeftOffHandSlot;
            case UWClickRules.InventorySpot.HandLeft: return UWArmorItemMap.BodySlot.RightHandSlot;
            case UWClickRules.InventorySpot.HandRight: return UWArmorItemMap.BodySlot.LeftHandSlot;
            case UWClickRules.InventorySpot.RingLeft: return UWArmorItemMap.BodySlot.RightRing;
            case UWClickRules.InventorySpot.RingRight: return UWArmorItemMap.BodySlot.LeftRing;
            default: return null;
        }
    }

    /// <summary>For UWItemDrag: the backpack item at a given backpack slot.</summary>
    public UWObject GetBackpackItem(int piSlot)
    {
        if (mOUi.mOInventory == null || piSlot < 0 || piSlot >= mOUi.mOInventory.Backpack.Length)
            return null;

        return mOUi.mOInventory.Backpack[piSlot];
    }

    /// <summary>The eight backpack slots: invisible hit areas (the circles are in the panel picture) with an icon each.</summary>
    internal void BuildBackpackSlots()
    {
        mBackpackSlotImages = new Image[BackpackSlotCount];

        for (int i = 0; i < BackpackSlotCount; i++)
        {
            int liCol = i % BackpackColumns;
            int liRow = i / BackpackColumns;
            float lfX = backpackGridLeft + (liCol * (backpackSlotSize + backpackSlotSpacingX));
            float lfY = backpackGridTop + (liRow * (backpackSlotSize + backpackSlotSpacingY));

            // Invisible (alpha 0) - the circle graphic is already in the original panel
            // background, mOUi rectangle now serves only as hit area.
            Image lOSlotBackground = UWGameUI.fCreateImage($"BackpackSlot{i}", mOUi.mGameFrame, lfX, -lfY, backpackSlotSize, backpackSlotSize, new Color(1f, 1f, 1f, 0f));

            GameObject lOIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            lOIcon.transform.SetParent(lOSlotBackground.transform, false);

            RectTransform lOIconRect = (RectTransform)lOIcon.transform;
            lOIconRect.anchorMin = Vector2.zero;
            lOIconRect.anchorMax = Vector2.one;
            lOIconRect.offsetMin = Vector2.zero;
            lOIconRect.offsetMax = Vector2.zero;

            Image lOIconImage = lOIcon.GetComponent<Image>();
            lOIconImage.enabled = false;
            mBackpackSlotImages[i] = lOIconImage;
        }
    }

    /// <summary>The container view over the backpack: the panel, its slots, the opened-container icon and the scroll arrows.</summary>
    internal void BuildContainerView()
    {
        // ContainerInventory: placed by the user in the former prefab editor directly over the
        // backpack panel, taken over 1:1 as read from the prefab (same procedure as for
        // lyEquipIconSlots). Starts inactive - only becomes visible when
        // UWInventory.OpenContainer != null (see fRefreshContainerVisibility). The background
        // sprite (INV.GR image 6) is set in Init.
        Image lOContainerPanel = UWGameUI.fCreateImage("ContainerInventory", mOUi.mGameFrame, 0f, 0f, 0f, 0f, Color.white);
        mContainerPanelRect = (RectTransform)lOContainerPanel.transform;
        mContainerPanelRect.anchorMin = Vector2.zero;
        mContainerPanelRect.anchorMax = Vector2.one;
        mContainerPanelRect.pivot = new Vector2(0.5f, 0.5f);
        mContainerPanelRect.anchoredPosition = new Vector2(118.51324f, -0.50367f);
        mContainerPanelRect.sizeDelta = new Vector2(-234.98553f, -158.99f);

        // Anchored to the frame's corners: in the widened classic frame it keeps its size, moved
        // with the panel (UWClassicWide; per user's screenshot, 2026-10-10: it stretched left).
        UWClassicWide.FrameSized(mContainerPanelRect, 236, 80);
        lOContainerPanel.gameObject.SetActive(false);

        // Hit zones lie directly in the GameFrame (not in the ContainerInventory panel
        // itself) - congruent with the backpack grid (see above), an own parent position
        // would only be needless conversion.
        mContainerSlotImages = new Image[ContainerSlotCount];

        for (int i = 0; i < ContainerSlotCount; i++)
        {
            int liCol = i % ContainerColumns;
            int liRow = i / ContainerColumns;
            float lfX = backpackGridLeft + (liCol * (backpackSlotSize + backpackSlotSpacingX));
            float lfY = backpackGridTop + (liRow * (backpackSlotSize + backpackSlotSpacingY));

            Image lOSlotBackground = UWGameUI.fCreateImage($"ContainerSlot{i}", mOUi.mGameFrame, lfX, -lfY, backpackSlotSize, backpackSlotSize, new Color(1f, 1f, 1f, 0f));
            lOSlotBackground.gameObject.SetActive(false);

            GameObject lOIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            lOIcon.transform.SetParent(lOSlotBackground.transform, false);

            RectTransform lOIconRect = (RectTransform)lOIcon.transform;
            lOIconRect.anchorMin = Vector2.zero;
            lOIconRect.anchorMax = Vector2.one;
            lOIconRect.offsetMin = Vector2.zero;
            lOIconRect.offsetMax = Vector2.zero;

            Image lOIconImage = lOIcon.GetComponent<Image>();
            lOIconImage.enabled = false;
            mContainerSlotImages[i] = lOIconImage;
        }

        // Container "opened" icon: above and left-aligned to the panel, grows upwards
        // from a firmly anchored lower edge (pivot bottom-left) - so it stays anchored at
        // the same spot no matter how big the actual icon texture is
        // (varies per container, only known when displayed, see
        // fRefreshContainerOpenIcon). Position is a first estimate (derived from the
        // panel position), not yet confirmed per screenshot.
        GameObject lOOpenIconObj = new GameObject("ContainerOpenIcon", typeof(RectTransform), typeof(Image));
        lOOpenIconObj.transform.SetParent(mOUi.mGameFrame, false);

        RectTransform lOOpenIconRect = (RectTransform)lOOpenIconObj.transform;
        lOOpenIconRect.anchorMin = new Vector2(0f, 1f);
        lOOpenIconRect.anchorMax = new Vector2(0f, 1f);
        lOOpenIconRect.pivot = new Vector2(0f, 0f);
        lOOpenIconRect.anchoredPosition = new Vector2(containerOpenIconLeft, -containerOpenIconTop);
        lOOpenIconRect.sizeDelta = Vector2.zero;

        mContainerOpenIconImage = lOOpenIconObj.GetComponent<Image>();
        mContainerOpenIconImage.raycastTarget = false;
        mContainerOpenIconImage.enabled = false;

        // Scroll arrows (see field comment above) - placeholder squares (sprite only comes
        // at runtime via Init, see fSetContainerScrollArrowSprite), visible/
        // invisible depending on fRefreshContainerScrollButtons. Alpha 0.4 instead of 0 (unlike
        // pure hit zones such as the backpack slots), since without a
        // real graphic nothing at all would be visible to calibrate them. Pivot
        // bottom-right (not via fCreateImage, which only knows top-left) - both hang
        // on the same right-aligned anchor line, grow upwards, down arrow at the edge,
        // up arrow directly left of it.
        mContainerScrollDownImage = mOUi.fCreateScrollArrowPlaceholder("ContainerScrollDown", containerScrollDownRight);
        mContainerScrollUpImage = mOUi.fCreateScrollArrowPlaceholder("ContainerScrollUp", containerScrollDownRight - containerScrollSize);
    }

    /// <summary>The armour hit zones on the paper doll and the ring and hand slots with their icons.</summary>
    internal void BuildEquipmentSlots()
    {
        // x/y/width/height, roughly estimated from the character silhouette (head at top,
        // torso/hands in the middle, legs/feet at bottom) - placeholders, to be corrected per screenshot
        // comparison.
        (float x, float y, float w, float h)[] lyZones =
        {
            (272.02747f, 13.586487f, 11.9724f, 12.41351f),  // Helmet
            (270.5f, 25.999992f, 14.5f, 19.1f),              // Chest
            (262f, 48.187496f, 32f, 7.0205f),                // Gloves (both hands)
            (270.5f, 45.1f, 14.5f, 28.305f),                 // Legs
            (265.99997f, 73.40498f, 22.743f, 6.59502f),      // Boots
        };

        mArmorHitZones = new RectTransform[ArmorHitZoneSlots.Length];

        for (int i = 0; i < lyZones.Length; i++)
        {
            // Positions are confirmed - invisible (alpha 0), pure hit zones without
            // own visuals.
            Image lOZoneImage = UWGameUI.fCreateImage($"ArmorHitZone_{ArmorHitZoneSlots[i]}", mOUi.mGameFrame, lyZones[i].x, -lyZones[i].y, lyZones[i].w, lyZones[i].h, Color.clear);
            mArmorHitZones[i] = lOZoneImage.rectTransform;
        }

        // Ring/hand slots: positions and sizes set per user in the prefab editor and
        // taken over here (same procedure as for lyZones above) - rings much smaller
        // (4x4) than the four hand slots (16x16), since ring-shaped items are visually much smaller.
        // They show their own item icon (like a backpack slot), no body-fitted
        // sprite - hit zone and display are the same element. Order must match
        // ArmorHitZoneSlots (see there).
        (float x, float y, float size)[] lyEquipIconSlots =
        {
            (291.03f, 56.9f, 4f),      // LeftRing
            (260f, 56.9f, 4f),         // RightRing
            (242f, 34.82566f, 16f),    // RightHandSlot
            (296f, 34.826f, 16f),      // LeftHandSlot
            (294f, 12.792999f, 16f),   // LeftOffHandSlot
            (244.9f, 12.793f, 16f),    // RightOffHandSlot
        };

        // Ring icons are themselves 16x16 despite the tiny 4x4 hit zone (see above) - at their
        // natural size from OBJECTS.GR, squashed onto the hit zone per stretch fill,
        // the ring icon did not sit right (CONFIRMED per user test). Centred instead of
        // stretched, with a small Y offset - values taken over 1:1 from the result the user
        // found in the prefab editor.
        const float RingIconSize = 16f;
        const float RingIconYOffset = 0.8f;

        mEquipSlotIcons = new Image[ArmorHitZoneSlots.Length - ArmorSlotCount];

        for (int i = 0; i < mEquipSlotIcons.Length; i++)
        {
            UWArmorItemMap.BodySlot leSlot = ArmorHitZoneSlots[ArmorSlotCount + i];
            Image lOSlotBackground = UWGameUI.fCreateImage($"EquipIcon_{leSlot}", mOUi.mGameFrame, lyEquipIconSlots[i].x, -lyEquipIconSlots[i].y, lyEquipIconSlots[i].size, lyEquipIconSlots[i].size, new Color(1f, 1f, 1f, 0f));

            GameObject lOIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            lOIcon.transform.SetParent(lOSlotBackground.transform, false);

            RectTransform lOIconRect = (RectTransform)lOIcon.transform;
            bool lbIsRing = leSlot == UWArmorItemMap.BodySlot.LeftRing || leSlot == UWArmorItemMap.BodySlot.RightRing;

            if (lbIsRing)
            {
                lOIconRect.anchorMin = new Vector2(0.5f, 0.5f);
                lOIconRect.anchorMax = new Vector2(0.5f, 0.5f);
                lOIconRect.sizeDelta = new Vector2(RingIconSize, RingIconSize);
                lOIconRect.anchoredPosition = new Vector2(0f, RingIconYOffset);
            }
            else
            {
                lOIconRect.anchorMin = Vector2.zero;
                lOIconRect.anchorMax = Vector2.one;
                lOIconRect.offsetMin = Vector2.zero;
                lOIconRect.offsetMax = Vector2.zero;
            }

            Image lOIconImage = lOIcon.GetComponent<Image>();
            lOIconImage.enabled = false;

            mArmorHitZones[ArmorSlotCount + i] = lOSlotBackground.rectTransform;
            mEquipSlotIcons[i] = lOIconImage;
        }
    }

    /// <summary>The fixed graphics of the container view, loaded once in Init: the background of an opened container (INV.GR image 6, identified per user) and the scroll arrows.</summary>
    internal void LoadSprites()
    {
        mOUi.fSetStaticSprite(mContainerPanelRect, UWTexture.TextureTypes.INV, 6);

        // Original: scroll arrows at the container panel (identified per user) - unlike
        // backpack/container icons a fixed, never changing graphic, therefore loaded here
        // once instead of via an Update() refresh like the item icons.
        UWTexture lOScrollDownSource = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.BUTTONS, 28);
        UWTexture lOScrollUpSource = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.BUTTONS, 27);

        fSetContainerScrollArrowSprite(mContainerScrollDownImage, lOScrollDownSource);
        fSetContainerScrollArrowSprite(mContainerScrollUpImage, lOScrollUpSource);

        // Position set here once more explicitly from the code constants (dates from the former
        // canvas prefab, whose baked values once made a position change seem ineffective).
        if (mContainerScrollDownImage != null)
        {
            RectTransform lODownRect = (RectTransform)mContainerScrollDownImage.transform;
            lODownRect.anchoredPosition = new Vector2(containerScrollDownRight, -containerScrollTop);
        }

        // Up arrow left of the down arrow, based on its ACTUAL width (not the
        // placeholder assumption from fBuildCanvas) plus an additional spacing (CONFIRMED per user,
        // containerScrollUpExtraLeftOffset) - no longer pure alignment.
        if (lOScrollDownSource != null && mContainerScrollUpImage != null)
        {
            RectTransform lOUpRect = (RectTransform)mContainerScrollUpImage.transform;
            float lfUpRight = containerScrollDownRight - lOScrollDownSource.Width - containerScrollUpExtraLeftOffset;
            lOUpRect.anchoredPosition = new Vector2(lfUpRight, -containerScrollTop);
        }
    }

    /// <summary>The per-frame refresh of the container view, the backpack, the weight and the equipment icons.</summary>
    internal void Update()
    {
        fRefreshContainerVisibility();
        fRefreshBackpackSlots();
        fRefreshWeight();

        fRefreshEquipSlotIcons();
        fRefreshContainerSlots();
        fRefreshContainerOpenIcon();
        fRefreshContainerScrollButtons();
    }
}
