using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE BAGS' WINDOWS AND PAGES (cleanup 5.17, 2026-10-23: out of UWModernBags, as they were): the
/// backpack's window and one window per open bag, 4 x 2 slots and more for a fuller bag, with the
/// number badge - the close button - in the title, the scroll arrows, the frame while the pointer
/// is on the window or its container's slot, and the window growing out of its slot when it opens.
/// Also the hit tests (which slot, which window, which badge lies under the pointer) and what lies
/// in a slot.
///
/// THE ORIGINAL'S PAGE (Classic Wide, Classic Modular; UWModernInventoryPage): its eight slots
/// are the backpack's, or the open container's from its offset on - window 0 stands in for them,
/// never shown (PageWindow); the bag windows stay shut while it shows (IsPageShown).
/// </summary>
internal sealed class UWModernBagPages
{
    public UWModernBagPages(UWModernBags pOBags)
    {
        mOBags = pOBags;
    }

    private readonly UWModernBags mOBags;

    internal const int Columns = 4;

    /// <summary>The layout in original pixels: the slot pitch is the original's 19.</summary>
    private const int SlotGap = 2;

    private const int SidePad = 6;

    private const int TitleRows = 12;

    private const int BottomPad = 6;

    internal sealed class Slot
    {
        public RawImage Circle;

        public RawImage Icon;

        public Text Count;

        public Rect ScreenRect;

        public UWTexture ShownTexture;

        /// <summary>The ring while the pointer is on this slot's open container or its window.</summary>
        public RawImage Ring;

        /// <summary>The number badge of an open container lying here.</summary>
        public RawImage Badge;

        public Text BadgeText;
    }

    internal sealed class Window
    {
        public RectTransform Root;

        public RawImage Back;

        /// <summary>The window's leather, built for its size and the colours (UWVersionedTexture).</summary>
        public readonly UWVersionedTexture BackArt = new UWVersionedTexture();

        public Text Title;

        public Text Weight;

        public RawImage Badge;

        public Text BadgeText;

        /// <summary>Where the badge - the close button - is on the screen.</summary>
        public Rect BadgeRect;

        /// <summary>The frame while the pointer is on the window or its container's slot.</summary>
        public Image[] Frame;

        /// <summary>Fades the window in while it grows out of its slot.</summary>
        public CanvasGroup Group;

        public readonly List<Slot> Slots = new List<Slot>();

        public Rect ScreenRect;

        /// <summary>Null for the backpack.</summary>
        public UWObject Container;

        /// <summary>The contents index of the first slot (scrolled rows times four).</summary>
        public int FirstIndex;

        /// <summary>The arrows in the right margin while rows lie hidden above or below
        /// (fShowScrollArrows).</summary>
        public RawImage ScrollUp;

        public RawImage ScrollDown;

        public int SlotCount;
    }

    private readonly List<Window> mOWindows = new List<Window>();

    /// <summary>Where each open bag's slot is on the screen this frame, if it is visible.</summary>
    private readonly Dictionary<UWObject, Rect> mOSourceRects = new Dictionary<UWObject, Rect>();

    /// <summary>How long a window takes to grow out of its slot.</summary>
    private const float OpenSeconds = 0.25f;

    /// <summary>The scroll arrow, pointing up (drawn flipped for down).</summary>
    private Texture2D mOArrowTexture;

    /// <summary>The windows made so far, the backpack's first; the unused ones inactive.</summary>
    internal List<Window> Windows => mOWindows;

    /// <summary>Where each open bag's slot is on the screen this frame (cleared by the layout,
    /// filled by the slots as they are laid out).</summary>
    internal Dictionary<UWObject, Rect> SourceRects => mOSourceRects;

    internal void Release()
    {
        if (mOArrowTexture != null)
            UnityEngine.Object.Destroy(mOArrowTexture);

        foreach (Window lOWindow in mOWindows)
            lOWindow.BackArt.Release();
    }

    // ------------------------------------------------- The original's page

    internal static bool IsPageShown
    {
        get
        {
            // In Classic Wide the frame's panel is the inventory even while it shows the stats or the
            // tablet: no bag window then either (per user's screenshot, 2026-10-10: the chain brought
            // up our backpack).
            return (UWModernInventoryPage.Instance != null && UWModernInventoryPage.Instance.IsShown) || UWModernClassicFrame.IsActive;
        }
    }

    /// <summary>The paperdoll slot under the pointer, if the panel or the original's page shows
    /// one there.</summary>
    internal static bool PanelSlotAt(Vector2 pOPointer, out UWArmorItemMap.BodySlot peSlot)
    {
        peSlot = UWArmorItemMap.BodySlot.Helmet;

        if (UWModernPanel.Instance != null && UWModernPanel.Instance.TryGetEquipSlotAt(pOPointer, out peSlot))
            return true;

        return IsPageShown && UWModernInventoryPage.Instance.TryGetEquipSlotAt(pOPointer, out peSlot);
    }

    /// <summary>A slot's place on the screen - the original page's for window 0 standing in for it.</summary>
    internal Rect SlotRect(Window pOWindow, int piSlot)
    {
        if (IsPageShown && pOWindow == GetWindow(0))
            return UWModernInventoryPage.Instance.BackpackSlotRect(piSlot);

        return pOWindow != null && piSlot >= 0 && piSlot < pOWindow.Slots.Count ? pOWindow.Slots[piSlot].ScreenRect : Rect.zero;
    }

    /// <summary>Whether a container is open: in a bag window, or with the original's page in its
    /// slots (the model's one open container).</summary>
    internal bool IsBagOpen(UWObject pOContainer, UWInventory pOInventory)
    {
        if (IsPageShown)
            return pOInventory != null && pOInventory.Model != null && pOInventory.Model.OpenContainer == pOContainer;

        return mOBags.OpenBags.Bags.Contains(pOContainer);
    }

    /// <summary>Window 0 standing in for the original page's eight slots: the backpack, or the
    /// open container from its offset on.</summary>
    internal Window PageWindow()
    {
        Window lOWindow = GetWindow(0);
        UWInventory lOInventory = mOBags.Inventory;
        UWInventoryModel lOModel = lOInventory != null ? lOInventory.Model : null;

        lOWindow.Container = lOModel != null ? lOModel.OpenContainer : null;
        lOWindow.FirstIndex = lOModel != null && lOModel.OpenContainer != null ? lOModel.ContainerScrollOffset : 0;

        return lOWindow;
    }

    /// <summary>
    /// The original page's container controls, as the classic frame's (UWItemDrag): the open
    /// container's icon closes it, one level up - or, with a thing on the pointer, takes the thing
    /// one level up (into the container it was opened from, else the first free backpack slot);
    /// the arrows scroll a row (the left one raises the offset), the thing staying on the pointer.
    /// True when the press was theirs. The meaning of the spots, the step and the "one level up"
    /// are UWInventoryView's, shared with the classic frame (cleanup step 5.8, 2026-10-20).
    /// </summary>
    internal bool PageControls(Vector2 pOPointer, UWInventory pOInventory)
    {
        UWModernInventoryPage lOPage = UWModernInventoryPage.Instance;
        UWInventoryModel lOModel = pOInventory.Model;

        if (!IsPageShown || lOModel == null || lOModel.OpenContainer == null)
            return false;

        UWInventoryView.ControlEnum leControl = UWInventoryView.ControlOf(lOPage.SpotAt(pOPointer));

        if ((leControl == UWInventoryView.ControlEnum.ScrollUp && lOPage.IsScrollUpShown)
            || (leControl == UWInventoryView.ControlEnum.ScrollDown && lOPage.IsScrollDownShown))
        {
            lOModel.ScrollContainer(UWInventoryView.ScrollDeltaOf(leControl));
            return true;
        }

        if (leControl != UWInventoryView.ControlEnum.ContainerIcon || !lOPage.IsContainerIconShown)
            return false;

        if (pOInventory.CursorItem == null)
        {
            lOModel.CloseContainer();
            return true;
        }

        // NO PLACE says so, as the classic frame does (message 259); until 2026-10-20 the page read
        // the stale container rejection here and mostly said nothing (found in cleanup step 5.8).
        UWInventoryView.PutOutcomeEnum leOutcome = UWInventoryView.PutOneLevelUp(lOModel);

        if (leOutcome == UWInventoryView.PutOutcomeEnum.NoPlace)
        {
            if (mOBags.Interaction != null)
                mOBags.Interaction.AddGeneralMessage(UWInventoryView.NoPlaceMessage);
        }
        else if (leOutcome != UWInventoryView.PutOutcomeEnum.Placed)
            mOBags.Drag.ReportRejection(pOInventory);

        return true;
    }

    // ------------------------------------------------- What lies where

    internal UWObject GetItem(Window pOWindow, int piSlot)
    {
        UWInventory lOInventory = mOBags.Inventory;

        if (pOWindow.Container == null)
            return piSlot >= 0 && piSlot < lOInventory.Backpack.Length ? lOInventory.Backpack[piSlot] : null;

        List<UWObject> lOContents = pOWindow.Container.Contents;
        int liIndex = pOWindow.FirstIndex + piSlot;

        return lOContents != null && liIndex >= 0 && liIndex < lOContents.Count ? lOContents[liIndex] : null;
    }

    internal void Scroll(Vector2 pOPointer, int piRows)
    {
        Dictionary<UWObject, int> lOScroll = mOBags.OpenBags.Scroll;

        foreach (Window lOWindow in mOWindows)
        {
            if (lOWindow.Container == null || !lOWindow.Root.gameObject.activeSelf
                || !lOWindow.ScreenRect.Contains(pOPointer))
                continue;

            lOScroll.TryGetValue(lOWindow.Container, out int liRows);
            lOScroll[lOWindow.Container] = Mathf.Max(0, liRows + piRows);
            return;
        }
    }

    // ------------------------------------------------- Hit tests

    /// <summary>The middle of the backpack's first slot on the screen (pixels, bottom-left origin) -
    /// where the freed pointer appears (UWModernPointer). False while the bags are not laid out.</summary>
    internal bool TryGetFirstBackpackSlot(out Vector2 pOCentre)
    {
        foreach (Window lOWindow in mOWindows)
        {
            if (lOWindow.Container != null || !lOWindow.Root.gameObject.activeSelf || lOWindow.SlotCount <= 0)
                continue;

            Rect lORect = lOWindow.Slots[0].ScreenRect;

            if (lORect.width <= 0f)
                break;

            pOCentre = lORect.center;
            return true;
        }

        pOCentre = Vector2.zero;
        return false;
    }

    internal bool FindSlot(Vector2 pOPointer, out Window pOWindow, out int piSlot)
    {
        // The original's page: its eight slots are the backpack's, or the open container's from
        // its offset on (window 0 standing in for them, never shown).
        if (IsPageShown)
        {
            piSlot = UWModernInventoryPage.Instance.BackpackSlotAt(pOPointer);
            pOWindow = piSlot >= 0 ? PageWindow() : null;

            return piSlot >= 0;
        }

        foreach (Window lOWindow in mOWindows)
        {
            if (!lOWindow.Root.gameObject.activeSelf)
                continue;

            for (int liSlot = 0; liSlot < lOWindow.SlotCount; liSlot++)
            {
                if (lOWindow.Slots[liSlot].ScreenRect.Contains(pOPointer))
                {
                    pOWindow = lOWindow;
                    piSlot = liSlot;
                    return true;
                }
            }
        }

        pOWindow = null;
        piSlot = -1;
        return false;
    }

    /// <summary>The close button of a bag window under the pointer - only with nothing on the
    /// pointer; with a thing there a click on the window puts it in.</summary>
    internal bool FindCloseBadge(Vector2 pOPointer, out Window pOWindow)
    {
        UWInventory lOInventory = mOBags.Inventory;

        if (lOInventory != null && lOInventory.CursorItem == null)
        {
            foreach (Window lOWindow in mOWindows)
            {
                if (lOWindow.Container != null && lOWindow.Root.gameObject.activeSelf && lOWindow.BadgeRect.Contains(pOPointer))
                {
                    pOWindow = lOWindow;
                    return true;
                }
            }
        }

        pOWindow = null;
        return false;
    }

    internal bool FindWindow(Vector2 pOPointer, out Window pOWindow)
    {
        foreach (Window lOWindow in mOWindows)
        {
            if (lOWindow.Root.gameObject.activeSelf && lOWindow.ScreenRect.Contains(pOPointer))
            {
                pOWindow = lOWindow;
                return true;
            }
        }

        pOWindow = null;
        return false;
    }

    // ------------------------------------------------- Laying a window out

    internal static int WindowRows(int piRows)
    {
        return TitleRows + (piRows * UWModernHudArt.SlotSize) + ((piRows - 1) * SlotGap) + BottomPad;
    }

    internal static int WindowWidth()
    {
        return (2 * SidePad) + (Columns * UWModernHudArt.SlotSize) + ((Columns - 1) * SlotGap);
    }

    /// <summary>Lays a window out with its bottom right corner at the given screen point and
    /// fills its slots.</summary>
    internal void LayoutWindow(Window pOWindow, int piRows, float pfRight, float pfBottom, int piNumber, string psTitle,
        string psWeight)
    {
        int liWidth = WindowWidth();
        int liHeight = WindowRows(piRows);
        float liScale = mOBags.View.Scale;

        pOWindow.Root.gameObject.SetActive(true);

        float lfLeft = pfRight - (liWidth * liScale);

        UWUiKit.SetRect(pOWindow.Root, lfLeft, pfBottom, liWidth * liScale, liHeight * liScale);
        pOWindow.Root.localScale = Vector3.one;
        pOWindow.Group.alpha = 1f;
        pOWindow.ScreenRect = new Rect(lfLeft, pfBottom, liWidth * liScale, liHeight * liScale);

        UWArtStamp lOBackStamp = UWVersionedTexture.Backs(liWidth, liHeight);

        if (!pOWindow.BackArt.IsCurrent(lOBackStamp))
        {
            pOWindow.Back.texture = pOWindow.BackArt.Set(lOBackStamp,
                UWModernHudArt.BuildPartLeather(UWModernLayout.ElementEnum.Bags, mOBags.Data.Textures, liWidth, liHeight, mOBags.Ui.TextureFilterMode));
        }

        int liTitleSize = Mathf.Max(10, Mathf.RoundToInt(4.5f * liScale));

        // The number badge left of the title, which moves over for it.
        bool lbBadge = piNumber > 0;
        float lfTitleLeft = (lbBadge ? SidePad + TitleBadgeShift : SidePad) * liScale;

        fShowBadge(pOWindow.Badge, pOWindow.BadgeText, lbBadge ? piNumber : -1,
            (SidePad + 3f) * liScale, (liHeight - 6f) * liScale, TitleBadgeSize * liScale);

        // One original pixel more around it to hit.
        float lfHit = (TitleBadgeSize + 2f) * liScale;

        pOWindow.BadgeRect = lbBadge
            ? new Rect(lfLeft + ((SidePad + 3f) * liScale) - (lfHit * 0.5f), pfBottom + ((liHeight - 6f) * liScale) - (lfHit * 0.5f), lfHit, lfHit)
            : Rect.zero;

        pOWindow.Title.fontSize = liTitleSize;
        pOWindow.Title.text = psTitle;
        UWUiKit.SetRect(pOWindow.Title.rectTransform, lfTitleLeft, (liHeight - TitleRows) * liScale,
            (liWidth * liScale) - lfTitleLeft - (SidePad * liScale), (TitleRows - 2) * liScale);

        pOWindow.Weight.fontSize = Mathf.Max(9, Mathf.RoundToInt(3.6f * liScale));
        pOWindow.Weight.text = psWeight;
        UWUiKit.SetRect(pOWindow.Weight.rectTransform, SidePad * liScale, (liHeight - TitleRows) * liScale, (liWidth - (2 * SidePad)) * liScale, (TitleRows - 2) * liScale);

        int liSlots = piRows * Columns;

        while (pOWindow.Slots.Count < liSlots)
            pOWindow.Slots.Add(fCreateSlot(pOWindow.Root));

        pOWindow.SlotCount = liSlots;

        for (int liSlot = 0; liSlot < pOWindow.Slots.Count; liSlot++)
        {
            Slot lOSlot = pOWindow.Slots[liSlot];
            bool lbUsed = liSlot < liSlots;

            lOSlot.Circle.gameObject.SetActive(lbUsed);

            if (!lbUsed)
                continue;

            int liColumn = liSlot % Columns;
            int liRow = liSlot / Columns;
            float lfX = (SidePad + (liColumn * (UWModernHudArt.SlotSize + SlotGap))) * liScale;
            float lfY = (liHeight - TitleRows - ((liRow + 1) * UWModernHudArt.SlotSize) - (liRow * SlotGap)) * liScale;

            UWUiKit.SetRect(lOSlot.Circle.rectTransform, lfX, lfY, UWModernHudArt.SlotSize * liScale, UWModernHudArt.SlotSize * liScale);
            lOSlot.Circle.texture = mOBags.View.SlotArt.Texture;
            lOSlot.ScreenRect = new Rect(lfLeft + lfX, pfBottom + lfY, UWModernHudArt.SlotSize * liScale, UWModernHudArt.SlotSize * liScale);

            fShowItem(lOSlot, GetItem(pOWindow, liSlot));
        }

        fShowScrollArrows(pOWindow, piRows, liWidth, liHeight);

        float lfLine = Mathf.Max(2f, mOBags.View.Scale * 0.75f);

        fSetFrame(pOWindow.Frame, -lfLine, -lfLine, (liWidth * liScale) + (2f * lfLine), (liHeight * liScale) + (2f * lfLine), lfLine);
    }

    /// <summary>The scroll arrow in original pixels: five wide, three high.</summary>
    private const int ArrowWidth = 5;

    private const int ArrowHeight = 3;

    /// <summary>
    /// THE SCROLL ARROWS (per user, 2026-10-08: show when a bag can be scrolled): in the right margin
    /// beside the top row while rows lie hidden above, beside the bottom row while more follows
    /// below - the same reckoning as the layout's (a spare empty row at the end, fLayout).
    /// </summary>
    private void fShowScrollArrows(Window pOWindow, int piRows, int piWidth, int piHeight)
    {
        int liScrolled = pOWindow.FirstIndex / Columns;
        int liNeeded = pOWindow.Container != null && pOWindow.Container.Contents != null
            ? (pOWindow.Container.Contents.Count / Columns) + 1
            : 0;
        bool lbUp = pOWindow.Container != null && liScrolled > 0;
        bool lbDown = pOWindow.Container != null && liScrolled < liNeeded - piRows;

        if (mOArrowTexture == null)
            mOArrowTexture = fBuildArrow();

        float liScale = mOBags.View.Scale;
        float lfX = (piWidth - SidePad + ((SidePad - ArrowWidth) * 0.5f)) * liScale;
        float lfTopY = (piHeight - TitleRows - ArrowHeight) * liScale;
        float lfBottomY = BottomPad * liScale;

        pOWindow.ScrollUp.texture = mOArrowTexture;
        pOWindow.ScrollUp.enabled = lbUp;
        UWUiKit.SetRect(pOWindow.ScrollUp.rectTransform, lfX, lfTopY, ArrowWidth * liScale, ArrowHeight * liScale);

        pOWindow.ScrollDown.texture = mOArrowTexture;
        pOWindow.ScrollDown.enabled = lbDown;
        UWUiKit.SetRect(pOWindow.ScrollDown.rectTransform, lfX, lfBottomY, ArrowWidth * liScale, ArrowHeight * liScale);
    }

    /// <summary>A gold triangle pointing up, five by three pixels, drawn sharp.</summary>
    private static Texture2D fBuildArrow()
    {
        Color32[] lyPixels = new Color32[ArrowWidth * ArrowHeight];
        Color32 lOGold = UWModernPalette.Gold;

        // Row 0 is the bottom: five wide, then three, then the tip.
        for (int y = 0; y < ArrowHeight; y++)
        {
            for (int x = 0; x < ArrowWidth; x++)
                lyPixels[(y * ArrowWidth) + x] = Mathf.Abs(x - (ArrowWidth / 2)) <= (ArrowHeight - 1 - y) ? lOGold : new Color32(0, 0, 0, 0);
        }

        return UWTextureFactory.FromColours(lyPixels, ArrowWidth, ArrowHeight, FilterMode.Point, TextureWrapMode.Clamp, "UWModernBags scroll arrow");
    }

    /// <summary>Badge sizes and places, in original pixels.</summary>
    private const float TitleBadgeSize = 6f;

    private const float TitleBadgeShift = 8f;

    private const float SlotBadgeSize = 6.5f;

    /// <summary>A number badge centred on a point, or hidden for a number below 1.</summary>
    private void fShowBadge(RawImage pOBadge, Text pOText, int piNumber, float pfCentreX, float pfCentreY, float pfSize)
    {
        bool lbShow = piNumber > 0;

        pOBadge.enabled = lbShow;
        pOText.enabled = lbShow;

        if (!lbShow)
            return;

        pOBadge.texture = mOBags.View.DiscTexture;
        UWUiKit.SetRect(pOBadge.rectTransform, pfCentreX - (pfSize * 0.5f), pfCentreY - (pfSize * 0.5f), pfSize, pfSize);

        pOText.text = piNumber.ToString();
        pOText.fontSize = Mathf.Max(9, Mathf.RoundToInt(pfSize * 0.66f));
        UWUiKit.SetRect(pOText.rectTransform, pfCentreX - (pfSize * 0.5f), pfCentreY - (pfSize * 0.5f), pfSize, pfSize);
    }

    /// <summary>The four lines of a frame around a rectangle.</summary>
    private static void fSetFrame(Image[] pOFrame, float pfX, float pfY, float pfWidth, float pfHeight, float pfLine)
    {
        UWUiKit.SetRect(pOFrame[0].rectTransform, pfX, pfY, pfWidth, pfLine);
        UWUiKit.SetRect(pOFrame[1].rectTransform, pfX, pfY + pfHeight - pfLine, pfWidth, pfLine);
        UWUiKit.SetRect(pOFrame[2].rectTransform, pfX, pfY, pfLine, pfHeight);
        UWUiKit.SetRect(pOFrame[3].rectTransform, pfX + pfWidth - pfLine, pfY, pfLine, pfHeight);
    }

    /// <summary>
    /// The window grows out of its container's slot when it opens: from the slot's rectangle to
    /// its own over OpenSeconds, easing out, fading in from a third. Without a visible slot it is
    /// simply there.
    /// </summary>
    internal void AnimateOpening(Window pOWindow, UWObject pOBag)
    {
        Dictionary<UWObject, float> lOOpenedAt = mOBags.OpenBags.OpenedAt;

        if (!lOOpenedAt.TryGetValue(pOBag, out float lfOpened) || !mOSourceRects.TryGetValue(pOBag, out Rect lOFrom))
            return;

        float lfTime = (Time.unscaledTime - lfOpened) / OpenSeconds;

        if (lfTime >= 1f)
        {
            lOOpenedAt.Remove(pOBag);
            return;
        }

        float lfEase = 1f - Mathf.Pow(1f - Mathf.Clamp01(lfTime), 3f);
        Rect lOTo = pOWindow.ScreenRect;

        float lfX = Mathf.Lerp(lOFrom.xMin, lOTo.xMin, lfEase);
        float lfY = Mathf.Lerp(lOFrom.yMin, lOTo.yMin, lfEase);
        float lfWidth = Mathf.Lerp(lOFrom.width, lOTo.width, lfEase);
        float lfHeight = Mathf.Lerp(lOFrom.height, lOTo.height, lfEase);

        pOWindow.Root.anchoredPosition = new Vector2(lfX, lfY);
        pOWindow.Root.localScale = new Vector3(lfWidth / lOTo.width, lfHeight / lOTo.height, 1f);
        pOWindow.Group.alpha = Mathf.Min(1f, 0.35f + lfTime);
    }

    private void fShowItem(Slot pOSlot, UWObject pOItem)
    {
        UWTexture lOSource = null;
        bool lbOpen = mOBags.IsOpen;
        UWModernOpenBags lOOpen = mOBags.OpenBags;

        if (pOItem != null)
        {
            lOSource = pOItem.Icon ?? pOItem.Texture;

            // An open bag shows its open picture, as the original's open container does (one rule
            // with the classic frame and the modern original page since 2026-10-20,
            // UWObjectAppearance.OpenContainerPicture; urn, quiver and bowl get their own picture).
            if (lbOpen && lOOpen.Bags.Contains(pOItem))
                lOSource = UWObjectAppearance.OpenContainerPicture(mOBags.Data.Textures, pOItem) ?? lOSource;
        }

        float liScale = mOBags.View.Scale;
        int liNumber = -1;

        if (lbOpen && pOItem != null && lOOpen.Numbers.TryGetValue(pOItem, out int liOpenNumber))
        {
            liNumber = liOpenNumber;
            mOSourceRects[pOItem] = pOSlot.ScreenRect;
        }

        fShowBadge(pOSlot.Badge, pOSlot.BadgeText, liNumber, 2.5f * liScale,
            (UWModernHudArt.SlotSize - 2.5f) * liScale, SlotBadgeSize * liScale);

        float lfRing = (UWModernHudArt.SlotSize + 2) * liScale;

        pOSlot.Ring.texture = mOBags.View.RingTexture;
        UWUiKit.SetRect(pOSlot.Ring.rectTransform, -liScale, -liScale, lfRing, lfRing);

        if (lOSource == null)
        {
            pOSlot.Icon.enabled = false;
            pOSlot.Count.enabled = false;
            pOSlot.ShownTexture = null;
            return;
        }

        Texture2D lOIcon = UWIconCache.Texture(lOSource, mOBags.Ui.TextureFilterMode);

        pOSlot.Icon.texture = lOIcon;
        pOSlot.Icon.enabled = true;
        pOSlot.ShownTexture = lOSource;

        UWUiKit.SetRect(pOSlot.Icon.rectTransform, (UWModernHudArt.SlotSize - (lOIcon.width * UWModernHudArt.PixelAspectX)) * 0.5f * liScale,
            ((UWModernHudArt.SlotSize - lOIcon.height) / 2) * liScale, lOIcon.width * UWModernHudArt.PixelAspectX * liScale, lOIcon.height * liScale);

        int liCount = UWStackRules.CountAtLeastOne(pOItem);

        pOSlot.Count.enabled = liCount > 1;

        if (liCount > 1)
        {
            pOSlot.Count.text = liCount.ToString();
            pOSlot.Count.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));
            UWUiKit.SetRect(pOSlot.Count.rectTransform, 0f, -liScale, (UWModernHudArt.SlotSize + 1) * liScale, UWModernHudArt.SlotSize * liScale * 0.6f);
        }
    }

    internal string Title(UWObject pOItem)
    {
        return UWText.Capitalised(UWText.ObjectName(mOBags.Data, pOItem.ID));
    }

    /// <summary>What a bag holds against what it takes; a bag without a limit (quiver, rune
    /// bag) shows only its contents.</summary>
    internal static string BagWeight(UWObject pOBag, DataImport pOData)
    {
        int liHeld = UWInventoryWeight.GetTotalTenthStones(pOBag.Contents, pOData.CommonObjectProperties);

        if (pOData.ObjectClassProperties != null
            && pOData.ObjectClassProperties.TryGetContainer(pOBag.ID, out UWObjectClassProperties.Container lOEntry)
            && lOEntry.CapacityTenthStones > 0)
            return UWText.F("hud.bag.weight_of", UWInventoryWeight.Format(liHeld), UWInventoryWeight.Format(lOEntry.CapacityTenthStones));

        return UWText.F("hud.bag.weight", UWInventoryWeight.Format(liHeld));
    }

    // ------------------------------------------------- Building

    internal Window GetWindow(int piIndex)
    {
        while (mOWindows.Count <= piIndex)
        {
            Window lOWindow = new Window();
            UWGameUI lOUi = mOBags.Ui;

            lOWindow.Root = UWUiKit.CreateGroup(mOBags.View.Root, "Bag " + mOWindows.Count);

            lOWindow.Back = UWUiKit.CreateRawImage(lOWindow.Root, "Leather", true);
            UWPixelArtUI.Apply(lOWindow.Back);
            UWUiKit.Stretch(lOWindow.Back.rectTransform);

            lOWindow.Title = UWUiKit.CreateText(lOWindow.Root, "Title", TextAnchor.MiddleLeft, UWModernPalette.HudText, lOUi);
            lOWindow.Title.color = UWModernPalette.Text;
            lOWindow.Weight = UWUiKit.CreateText(lOWindow.Root, "Weight", TextAnchor.MiddleRight, UWModernPalette.HudText, lOUi);
            lOWindow.Weight.color = new Color(0.90f, 0.84f, 0.69f);

            lOWindow.ScrollUp = UWUiKit.CreateRawImage(lOWindow.Root, "Scroll up");
            lOWindow.ScrollDown = UWUiKit.CreateRawImage(lOWindow.Root, "Scroll down");
            lOWindow.ScrollDown.uvRect = new Rect(0f, 1f, 1f, -1f);

            lOWindow.Badge = UWUiKit.CreateRawImage(lOWindow.Root, "Badge");
            lOWindow.BadgeText = UWUiKit.CreateText(lOWindow.Root, "Badge number", TextAnchor.MiddleCenter, UWModernPalette.HudText, lOUi);
            // The number in the disc's middle by its ink, whatever the font (UWCentredGlyph).
            lOWindow.BadgeText.gameObject.AddComponent<UWCentredGlyph>();
            lOWindow.BadgeText.color = UWModernPalette.LightText;

            lOWindow.Frame = new Image[4];

            for (int liLine = 0; liLine < lOWindow.Frame.Length; liLine++)
            {
                lOWindow.Frame[liLine] = UWUiKit.CreateImage(lOWindow.Root, "Frame", UWModernPalette.Gold);
                lOWindow.Frame[liLine].enabled = false;
            }

            lOWindow.Group = lOWindow.Root.gameObject.AddComponent<CanvasGroup>();
            lOWindow.Group.interactable = false;
            lOWindow.Group.blocksRaycasts = false;

            mOWindows.Add(lOWindow);

            // The hover line, the pointer item and the tooltip stay on top of every window.
            mOBags.View.KeepOnTop();
        }

        return mOWindows[piIndex];
    }

    private Slot fCreateSlot(RectTransform pOParent)
    {
        Slot lOSlot = new Slot();
        UWGameUI lOUi = mOBags.Ui;

        lOSlot.Circle = UWUiKit.CreateRawImage(pOParent, "Slot", true);
        UWPixelArtUI.Apply(lOSlot.Circle);

        lOSlot.Icon = UWUiKit.CreateRawImage(lOSlot.Circle.rectTransform, "Icon");
        UWIconPalette.ApplySmooth(lOSlot.Icon);

        lOSlot.Count = UWUiKit.CreateText(lOSlot.Circle.rectTransform, "Count", TextAnchor.LowerRight, UWModernPalette.HudText, lOUi);
        lOSlot.Count.color = Color.white;

        lOSlot.Ring = UWUiKit.CreateRawImage(lOSlot.Circle.rectTransform, "Ring");
        lOSlot.Badge = UWUiKit.CreateRawImage(lOSlot.Circle.rectTransform, "Badge");
        lOSlot.BadgeText = UWUiKit.CreateText(lOSlot.Circle.rectTransform, "Badge number", TextAnchor.MiddleCenter, UWModernPalette.HudText, lOUi);
        lOSlot.BadgeText.gameObject.AddComponent<UWCentredGlyph>();
        lOSlot.BadgeText.color = UWModernPalette.LightText;

        return lOSlot;
    }
}
