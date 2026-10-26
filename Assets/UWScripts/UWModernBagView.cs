using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;
using Window = UWModernBagPages.Window;

/// <summary>
/// THE BAGS' CANVAS AND HOW THEY LIE ON THE SCREEN (cleanup 5.17, 2026-10-23: out of UWModernBags'
/// LateUpdate, as it was): bottom right the backpack, ALWAYS shown, with the load in the title and
/// as a bar above it; the open bags' windows stacked from it, left of the docked character panel
/// and around the minimap and the action bar (UWBagPlacement, engine-free since 2026-10-05); the
/// pointer on a bag window or the slot of an open bag frames the window, rings the slot and joins
/// them with a line; hovering shows the name and the weight (the tooltip). The windows themselves
/// are UWModernBagPages', the thing on the pointer UWModernBagDrag's, the split box and the menu
/// their own; UWModernBags lays them out in this order every frame.
/// </summary>
internal sealed class UWModernBagView
{
    public UWModernBagView(UWModernBags pOBags)
    {
        mOBags = pOBags;
    }

    private readonly UWModernBags mOBags;

    private const int MinRows = 2;

    private const int MaxRows = 6;

    private const int ScreenMargin = 4;

    private const int WindowGap = 4;

    private const int LoadBarRows = 2;

    private Canvas mOCanvas;

    private RectTransform mORoot;

    private Image mOLoadBack;

    private Image mOLoadFill;

    private Image mOTooltipBack;

    private Text mOTooltip;

    private readonly UWVersionedTexture mOSlotArt = new UWVersionedTexture();

    private Texture2D mODiscTexture;

    /// <summary>The badge under the pointer, lighter, as a button.</summary>
    private Texture2D mODiscHoverTexture;

    private static readonly Color msBadgeHoverFill = new Color(0.45f, 0.26f, 0.09f, 1f);

    private const string CloseMark = "×";

    private Texture2D mORingTexture;

    private Image mOHoverLine;

    /// <summary>The bags' scale this frame: the UI's times the element's own size (UWModernLayout).</summary>
    internal float Scale { get; private set; } = 1f;

    /// <summary>The bags' canvas, which their windows, the split box and the menu are made under.</summary>
    internal RectTransform Root => mORoot;

    /// <summary>A slot's circle, built for the colours and the backs.</summary>
    internal UWVersionedTexture SlotArt => mOSlotArt;

    /// <summary>The number badge's disc, and the lighter one under the pointer (also the split
    /// box's round buttons).</summary>
    internal Texture2D DiscTexture => mODiscTexture;

    internal Texture2D DiscHoverTexture => mODiscHoverTexture;

    /// <summary>The ring around an open bag's slot.</summary>
    internal Texture2D RingTexture => mORingTexture;

    internal void Release()
    {
        mOSlotArt.Release();

        if (mODiscTexture != null)
            UnityEngine.Object.Destroy(mODiscTexture);

        if (mODiscHoverTexture != null)
            UnityEngine.Object.Destroy(mODiscHoverTexture);

        if (mORingTexture != null)
            UnityEngine.Object.Destroy(mORingTexture);
    }

    /// <summary>Not shown: the canvas off, no edge for the conversation to keep left of.</summary>
    internal void Hide()
    {
        if (mOCanvas != null)
            mOCanvas.enabled = false;

        mOBags.LeftEdge = float.MaxValue;
    }

    /// <summary>The tooltip hidden under the split box or the menu.</summary>
    internal void HideTooltip()
    {
        mOTooltip.enabled = false;
        mOTooltipBack.enabled = false;
    }

    /// <summary>The hover line, the pointer item and the tooltip on top of every window.</summary>
    internal void KeepOnTop()
    {
        mOHoverLine.transform.SetAsLastSibling();
        mOBags.Drag.KeepPointerItemOnTop();
        mOTooltipBack.transform.SetAsLastSibling();
        mOTooltip.transform.SetAsLastSibling();
    }

    // ------------------------------------------------- Drawing

    /// <summary>The backpack and the open bags' windows where they go this frame.</summary>
    internal void Layout()
    {
        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;

        // Left of the docked character panel and its handle (UWModernPanel). Where the backpack
        // and the bag windows go is worked out engine-free in UWBagPlacement (since 2026-10-05).
        UWModernPanel lOPanel = UWModernPanel.Instance;

        // Its own size and place (UWModernLayout): the backpack, the bag windows stacking from it.
        Scale = UWModernUi.PixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.Bags);

        fEnsureArt();

        UWInventory lOInventory = mOBags.Inventory;
        DataImport lOData = mOBags.Data;
        UWModernBagPages lOPages = mOBags.Pages;
        UWModernOpenBags lOOpen = mOBags.OpenBags;
        List<Window> lOWindows = lOPages.Windows;

        lOPages.SourceRects.Clear();

        // With the original's character page the page is the backpack: no window of our own, no
        // bag windows (per user, 2026-10-10); window 0 stands in for the page's slots.
        if (UWModernBagPages.IsPageShown)
        {
            lOPages.PageWindow();

            foreach (Window lOHidden in lOWindows)
                lOHidden.Root.gameObject.SetActive(false);

            mOLoadBack.enabled = false;
            mOLoadFill.enabled = false;
            // The conversation keeps left of the page as of the bags (UWModernConversation).
            Rect lOPageRect = UWModernInventoryPage.Instance != null ? UWModernInventoryPage.Instance.ScreenRect : Rect.zero;

            mOBags.LeftEdge = lOPageRect.width > 0f ? Mathf.Min(Screen.width, lOPageRect.xMin) : Screen.width;
            return;
        }

        // The backpack, always.
        Window lOPack = lOPages.GetWindow(0);
        lOPack.Container = null;
        lOPack.FirstIndex = 0;

        float lfEdge = lOPanel != null ? Mathf.Min(Screen.width, lOPanel.LeftEdge) : Screen.width;
        float lfRight = lfEdge - (ScreenMargin * UWModernUi.PixelScale);
        float lfBottom = ScreenMargin * UWModernUi.PixelScale;
        bool lbFree = UWModernLayout.IsPlaced(UWModernLayout.ElementEnum.Bags);

        // The backpack with its load bar above it.
        float lfPackWidth = UWModernBagPages.WindowWidth() * Scale;
        float lfPackHeight = (UWModernBagPages.WindowRows(2) + LoadBarRows + 2) * Scale;
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Bags,
            new Rect(lfRight - lfPackWidth, lfBottom, lfPackWidth, lfPackHeight));

        // The open bags' rows (their scrolling kept within them) and heights, in their order.
        List<UWObject> lOShownBags = new List<UWObject>();
        List<int> liBagRows = new List<int>();
        List<int> liBagHeights = new List<int>();

        if (mOBags.IsOpen)
        {
            foreach (UWObject lOBag in lOOpen.Bags)
            {
                int liCount = lOBag.Contents != null ? lOBag.Contents.Count : 0;
                int liNeeded = (liCount / UWModernBagPages.Columns) + 1;
                int liRows = Mathf.Clamp(liNeeded, MinRows, MaxRows);

                lOOpen.Scroll.TryGetValue(lOBag, out int liScrolled);
                liScrolled = Mathf.Clamp(liScrolled, 0, Mathf.Max(0, liNeeded - liRows));
                lOOpen.Scroll[lOBag] = liScrolled;

                lOShownBags.Add(lOBag);
                liBagRows.Add(liRows);
                liBagHeights.Add(UWModernBagPages.WindowRows(liRows));
            }
        }

        // The backpack pushed out left of an open character panel lying on it, the bag windows
        // stacked from it around the panel, the minimap and the action bar (UWBagPlacement).
        UWModernActionBar lOBar = UWModernActionBar.Instance;
        UWBagPlacement.Result lOPlace = UWBagPlacement.Place(new UWBagPlacement.Input
        {
            ScreenWidth = Screen.width,
            ScreenHeight = Screen.height,
            Scale = Scale,
            PixelScale = UWModernUi.PixelScale,
            Pack = lOPlaced.ToUW(),
            PackPlaced = lbFree,
            PackWindowHeight = UWModernBagPages.WindowRows(2),
            WindowWidth = UWModernBagPages.WindowWidth(),
            BagHeights = liBagHeights.ToArray(),
            Panel = (lOPanel != null ? lOPanel.OpenRect : Rect.zero).ToUW(),
            Minimap = (UWModernMinimap.Instance != null ? UWModernMinimap.Instance.ScreenRect : Rect.zero).ToUW(),
            ActionBar = (lOBar != null ? lOBar.ScreenRect : Rect.zero).ToUW(),
            ScreenMargin = ScreenMargin,
            WindowGap = WindowGap,
            LoadBarRows = LoadBarRows
        });

        lOPlaced = lOPlace.Pack.ToUnity();
        lfRight = lOPlaced.xMax;
        lfBottom = lOPlaced.y;
        UWModernLayout.Report(UWModernLayout.ElementEnum.Bags, lOPlaced);

        int liCarried = lOInventory.GetCarriedTenthStones(lOData.CommonObjectProperties);
        int liMax = lOData.InitialPlayer != null ? lOData.InitialPlayer.MaxWeight : 0;
        string lsLoad = UWText.F("hud.bag.weight_of", UWInventoryWeight.Format(liCarried), UWInventoryWeight.Format(liMax));

        lOPages.LayoutWindow(lOPack, 2, lfRight, lfBottom, -1, UWText.T("hud.bag.backpack"), lsLoad);

        mOLoadBack.enabled = true;
        mOLoadFill.enabled = true;

        // The load bar over the backpack's inner width.
        float lfBarLeft = lOPack.ScreenRect.xMin + ((UWModernHudArt.LeatherLeft + 1) * Scale);
        float lfBarWidth = lOPack.ScreenRect.width - ((UWModernHudArt.LeatherLeft + UWModernHudArt.LeatherRight + 2) * Scale);
        float lfBarY = lOPack.ScreenRect.yMax + (2 * Scale);
        float lfLoad = liMax > 0 ? Mathf.Clamp01(liCarried / (float)liMax) : 0f;

        UWUiKit.SetRect(mOLoadBack.rectTransform, lfBarLeft, lfBarY, lfBarWidth, LoadBarRows * Scale);
        UWUiKit.SetRect(mOLoadFill.rectTransform, lfBarLeft, lfBarY, lfBarWidth * lfLoad, LoadBarRows * Scale);

        // The open bags where UWBagPlacement put them.
        int liWindow = 1;

        for (int liBag = 0; liBag < lOShownBags.Count; liBag++)
        {
            UWObject lOBag = lOShownBags[liBag];
            Window lOWindow = lOPages.GetWindow(liWindow++);

            lOOpen.Scroll.TryGetValue(lOBag, out int liScrolled);
            lOWindow.Container = lOBag;
            lOWindow.FirstIndex = liScrolled * UWModernBagPages.Columns;

            lOOpen.Numbers.TryGetValue(lOBag, out int liNumber);

            UWBagPlacement.Spot lOSpot = lOPlace.Bags[liBag];

            lOPages.LayoutWindow(lOWindow, liBagRows[liBag], lOSpot.Right, lOSpot.Bottom, liNumber, lOPages.Title(lOBag),
                UWModernBagPages.BagWeight(lOBag, lOData));
            lOPages.AnimateOpening(lOWindow, lOBag);
        }

        for (int liAt = liWindow; liAt < lOWindows.Count; liAt++)
            lOWindows[liAt].Root.gameObject.SetActive(false);

        float lfLeftEdge = Screen.width;

        foreach (Window lOShown in lOWindows)
        {
            if (lOShown.Root.gameObject.activeSelf && lOShown.ScreenRect.width > 0f)
                lfLeftEdge = Mathf.Min(lfLeftEdge, lOShown.ScreenRect.xMin);
        }

        mOBags.LeftEdge = lfLeftEdge;
    }

    /// <summary>
    /// The pointer on a bag window or on the slot of an open bag: the window gets its frame, the
    /// slot its ring, and a line joins them (3 of the look).
    /// </summary>
    internal void UpdateHover()
    {
        UWObject lOHovered = null;
        UWModernBagPages lOPages = mOBags.Pages;
        bool lbOpen = mOBags.IsOpen;

        if (lbOpen && UWModernPointer.IsFree && UWPointer.HasMouse)
        {
            Vector2 lOPointer = UWPointer.Position;

            if (lOPages.FindSlot(lOPointer, out Window lOSlotWindow, out int liSlot))
            {
                UWObject lOItem = lOPages.GetItem(lOSlotWindow, liSlot);

                if (lOItem != null && mOBags.OpenBags.Bags.Contains(lOItem))
                    lOHovered = lOItem;
            }

            if (lOHovered == null && lOPages.FindWindow(lOPointer, out Window lOWindow) && lOWindow.Container != null)
                lOHovered = lOWindow.Container;
        }

        Window lOHoveredWindow = null;
        Window lOCloseWindow = null;

        if (lbOpen && UWModernPointer.IsFree && UWPointer.HasMouse)
            lOPages.FindCloseBadge(UWPointer.Position, out lOCloseWindow);

        if (lOCloseWindow != null)
        {
            lOCloseWindow.Badge.texture = mODiscHoverTexture;
            lOCloseWindow.BadgeText.text = CloseMark;
        }

        foreach (Window lOWindow in lOPages.Windows)
        {
            bool lbMine = lOHovered != null && lOWindow.Container == lOHovered && lOWindow.Root.gameObject.activeSelf;

            foreach (Image lOLine in lOWindow.Frame)
                lOLine.enabled = lbMine;

            if (lbMine)
                lOHoveredWindow = lOWindow;

            for (int liSlot = 0; liSlot < lOWindow.Slots.Count; liSlot++)
            {
                lOWindow.Slots[liSlot].Ring.enabled = lOHovered != null && liSlot < lOWindow.SlotCount
                    && lOPages.GetItem(lOWindow, liSlot) == lOHovered;
            }
        }

        if (lOHoveredWindow == null || !lOPages.SourceRects.TryGetValue(lOHovered, out Rect lOSource))
        {
            mOHoverLine.enabled = false;
            return;
        }

        // From the ring's edge to the nearest point of the window's frame.
        Rect lOTarget = lOHoveredWindow.ScreenRect;
        Vector2 lOCentre = lOSource.center;
        Vector2 lONearest = new Vector2(Mathf.Clamp(lOCentre.x, lOTarget.xMin, lOTarget.xMax),
            Mathf.Clamp(lOCentre.y, lOTarget.yMin, lOTarget.yMax));
        Vector2 lODirection = lONearest - lOCentre;
        float lfRadius = (lOSource.width * 0.5f) + Scale;
        float lfLength = lODirection.magnitude - lfRadius;

        if (lfLength <= 1f)
        {
            mOHoverLine.enabled = false;
            return;
        }

        lODirection.Normalize();

        RectTransform lORect = mOHoverLine.rectTransform;
        lORect.anchoredPosition = lOCentre + (lODirection * lfRadius);
        lORect.sizeDelta = new Vector2(lfLength, Mathf.Max(2f, Scale * 0.75f));
        lORect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(lODirection.y, lODirection.x) * Mathf.Rad2Deg);
        mOHoverLine.enabled = true;
    }

    /// <summary>The name and the weight of the thing under the pointer.</summary>
    internal void UpdateTooltip(UWInventory pOInventory, DataImport pOData)
    {
        UWObject lOItem = null;
        Vector2 lOPointer = Vector2.zero;

        if (mOBags.IsActive && UWModernPointer.IsFree && pOInventory.CursorItem == null && UWPointer.HasMouse)
        {
            lOPointer = UWPointer.Position;

            if (UWModernBagPages.PanelSlotAt(lOPointer, out UWArmorItemMap.BodySlot leEquip))
                lOItem = pOInventory.GetEquipped(leEquip);
            else if (mOBags.Pages.FindSlot(lOPointer, out Window lOWindow, out int liSlot))
                lOItem = mOBags.Pages.GetItem(lOWindow, liSlot);
        }

        if (lOItem == null)
        {
            mOTooltip.enabled = false;
            mOTooltipBack.enabled = false;
            return;
        }

        int liCount = UWStackRules.CountAtLeastOne(lOItem);
        string lsEffect = fKnownEffect(lOItem, pOData, out string lsTalisman, out bool lbCursed);

        // The name as the language's grammar puts it on a label (translation step 3A): the count, the
        // plural, "Cursed" (IUWGrammar.ItemLabel); a talisman takes its own name.
        string lsName = lsTalisman != null ? UWGrammar.Of(pOData).LiteralLabel(lsTalisman) : UWText.ObjectLabel(pOData, lOItem.ID, liCount, lbCursed);

        if (lsTalisman != null && liCount > 1)
            lsName = liCount + " " + lsName;

        string lsWeight = UWText.F("hud.bag.weight", UWInventoryWeight.Format(UWInventoryWeight.GetItemTenthStones(lOItem, pOData.CommonObjectProperties)));

        int liFontSize = UWModernUi.FontSize;
        int liLines = string.IsNullOrEmpty(lsEffect) ? 2 : 3;

        mOTooltip.fontSize = liFontSize;
        mOTooltip.text = lsName + (string.IsNullOrEmpty(lsEffect) ? string.Empty : "\n" + lsEffect) + "\n" + lsWeight;

        float lfWidth = Mathf.Max(liFontSize * 6f, mOTooltip.preferredWidth + liFontSize);
        float lfHeight = liFontSize * (liLines + 1f);
        float lfX = Mathf.Clamp(lOPointer.x - lfWidth, 0f, Screen.width - lfWidth);
        float lfY = Mathf.Clamp(lOPointer.y + (4 * Scale), 0f, Screen.height - lfHeight);

        UWUiKit.SetRect(mOTooltipBack.rectTransform, lfX, lfY, lfWidth, lfHeight);
        UWUiKit.SetRect(mOTooltip.rectTransform, lfX + (liFontSize * 0.5f), lfY, lfWidth - liFontSize, lfHeight);

        mOTooltip.enabled = true;
        mOTooltipBack.enabled = true;
    }

    /// <summary>
    /// What is KNOWN of a thing (per user, 2026-10-04: an identified thing shows its full name, its
    /// magic on a line of its own): only with the full Lore result already in the thing - from an
    /// earlier look or Name Enchantment (UWLoreCheck: bits 0-1 of its heading, level 3); the
    /// tooltip rolls nothing. A talisman takes its own name, a cursed thing the word before its
    /// name; the enchantment ("of Very Great Damage") and a wand's charges are the second line,
    /// as the look words them (UWItemDescriptions.DescribeItem). Empty when nothing more is known.
    /// The talisman's name (psTalisman, else null) and the curse (pbCursed) go to the name's label.
    /// </summary>
    private string fKnownEffect(UWObject pOItem, DataImport pOData, out string psTalisman, out bool pbCursed)
    {
        psTalisman = null;
        pbCursed = false;

        if (!UWLoreCheck.CanBeIdentified(pOItem, pOData.CommonObjectProperties)
            || (pOItem.Heading & 0x3) != UWLoreCheck.ResultNamed)
            return string.Empty;

        string lsTalisman = UWTalismans.GetName(pOItem.ID, pOData.Strings);

        if (!string.IsNullOrEmpty(lsTalisman))
        {
            psTalisman = lsTalisman;
            return string.Empty;
        }

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        List<UWObject> lOObjects = pOData.IsSavegame && pOData.InitialPlayer != null ? pOData.InitialPlayer.InventoryRecords
            : lOLoader != null && lOLoader.CurrentLevel != null ? lOLoader.CurrentLevel.Masterlist : null;
        UWEnchantment.Result lOEnchantment = UWEnchantment.Get(pOItem, lOObjects, pOData.Strings);
        string lsEffect = string.Empty;

        if (lOEnchantment.IsCursed && !string.IsNullOrEmpty(UWText.ObjectName(pOData, pOItem.ID, UWStackRules.CountAtLeastOne(pOItem) > 1)))
            pbCursed = true;
        else if (!string.IsNullOrEmpty(lOEnchantment.Name))
            lsEffect = UWText.F("hud.item.of", lOEnchantment.Name);

        if (lOEnchantment.Charges >= 0)
        {
            string lsCharges = lOEnchantment.Charges == 0 ? UWText.T("hud.item.no_charges")
                : lOEnchantment.Charges == 1 ? UWText.T("hud.item.one_charge")
                : UWText.F("hud.item.charges", lOEnchantment.Charges);

            lsEffect = lsEffect.Length > 0 ? lsEffect + " " + lsCharges : lsCharges;
        }

        return lsEffect;
    }

    private void fEnsureArt()
    {
        UWArtStamp lOStamp = UWVersionedTexture.Backs();

        if (mOSlotArt.IsCurrent(lOStamp))
            return;

        mOSlotArt.Set(lOStamp, UWModernHudArt.BuildPartSlot(UWModernLayout.ElementEnum.Bags, mOBags.Data.Textures, mOBags.Ui.TextureFilterMode));

        if (mODiscTexture == null)
            mODiscTexture = fBuildCircle(64, 29f, 3.5f, UWModernPalette.BadgeFill, UWModernPalette.Gold);

        if (mODiscHoverTexture == null)
            mODiscHoverTexture = fBuildCircle(64, 29f, 3.5f, msBadgeHoverFill, UWModernPalette.Gold);

        if (mORingTexture == null)
            mORingTexture = fBuildCircle(128, 60f, 6f, Color.clear, UWModernPalette.Gold);
    }

    /// <summary>A smooth disc with a rim, for the number badges and the hover ring (the fill
    /// clear for the ring).</summary>
    private static Texture2D fBuildCircle(int piSize, float pfRadius, float pfRim, Color pOFill, Color pORim)
    {
        Color32[] lyPixels = new Color32[piSize * piSize];
        float lfCentre = (piSize - 1) * 0.5f;

        for (int y = 0; y < piSize; y++)
        {
            for (int x = 0; x < piSize; x++)
            {
                float lfDistance = Mathf.Sqrt(((x - lfCentre) * (x - lfCentre)) + ((y - lfCentre) * (y - lfCentre)));
                float lfOutside = Mathf.Clamp01(lfDistance - pfRadius + 0.5f);
                float lfInRim = Mathf.Clamp01(lfDistance - (pfRadius - pfRim) + 0.5f);
                Color lOColour = Color.Lerp(pOFill, pORim, lfInRim);

                lOColour.a *= 1f - lfOutside;
                lyPixels[(y * piSize) + x] = lOColour;
            }
        }

        return UWTextureFactory.FromColours(lyPixels, piSize, piSize, FilterMode.Bilinear, TextureWrapMode.Clamp, "UWModernBags circle");
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        // Above the character panel (41), whose backpack it draws.
        mOCanvas = UWUiKit.CreateOverlayCanvas(mOBags.transform, "Modern bags", UWUiSorting.ModernBags);

        mORoot = (RectTransform)mOCanvas.transform;

        mOLoadBack = UWUiKit.CreateImage(mORoot, "Load", new Color(0.24f, 0.20f, 0.16f, 0.9f));
        mOLoadFill = UWUiKit.CreateImage(mORoot, "Load fill", UWModernPalette.Accent);

        mOHoverLine = UWUiKit.CreateImage(mORoot, "Hover line", UWModernPalette.Gold);
        mOHoverLine.rectTransform.pivot = new Vector2(0f, 0.5f);
        mOHoverLine.enabled = false;

        mOBags.Drag.BuildPointerItem(mORoot);

        mOTooltipBack = UWUiKit.CreateImage(mORoot, "Tooltip", new Color(0.05f, 0.05f, 0.06f, 0.88f));
        mOTooltip = UWUiKit.CreateText(mORoot, "Tooltip text", TextAnchor.MiddleLeft, UWModernPalette.HudText, mOBags.Ui);
    }
}
