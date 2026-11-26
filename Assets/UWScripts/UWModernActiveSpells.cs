using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN HUD'S ACTIVE SPELLS (SPELLS.GR through the palette shader, as on the classic shelf),
/// a buff row at the top right (fPlaceSpells). Out of UWModernHud since cleanup 5.16
/// (2026-10-22); the clicks on them are the HUD's routing (UWModernHudInput).
///
/// IN THE HOLLOW (UW2 plan stage U6 step 2c, UWModernLayout.IsSpellHollowShown): the second game's left hollow of its
/// marble ledge as the element's picture, the spells at the frame's places in it (the first at the right), the
/// hollow's outline as the rune hollow's.
/// </summary>
public class UWModernActiveSpells : UWModernPart
{
    private RawImage[] mOSpellIcons;

    private Texture2D[] mOSpellTextures;

    /// <summary>The active spells' places this frame (screen pixels), and the hover text.</summary>
    private readonly Rect[] mOSpellRects = new Rect[UWPlayerVitals.MaxActiveSpells];

    private Text mOSpellTip;

    /// <summary>The spell under the free pointer, -1 for none (UWModernHudInput sets it).</summary>
    public int HoveredSpell { get; set; } = -1;

    private int miSpellVersion = -1;

    public UWModernActiveSpells(UWModernHud pOHud) : base(pOHud)
    {
    }

    /// <summary>The hollow behind the spells (IsSpellHollowShown), under the icons.</summary>
    private RawImage mOHollow;

    private readonly UWVersionedTexture mOHollowArt = new UWVersionedTexture();

    public override void Build(Transform pORoot)
    {
        mOHollow = UWUiKit.CreateRawImage(pORoot, "Spell hollow", Vector2.zero, Vector2.zero);
        UWPixelArtUI.Apply(mOHollow);
        mOHollow.enabled = false;

        mOSpellIcons = new RawImage[UWPlayerVitals.MaxActiveSpells];
        mOSpellTextures = new Texture2D[UWPlayerVitals.MaxActiveSpells];

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
            mOSpellIcons[liSlot] = UWUiKit.CreateRawImage(pORoot, "Spell " + liSlot, Vector2.zero, Vector2.zero);

        // The hover text on a canvas of its own over the Classic frame's dragons (UWUiSorting.ModernSpellTip).
        Canvas lOTipLayer = UWUiKit.CreateLayerCanvas(pORoot, "Spell hover layer", UWUiSorting.ModernSpellTip);
        RectTransform lOTipSpace = (RectTransform)lOTipLayer.transform;

        lOTipSpace.anchorMin = Vector2.zero;
        lOTipSpace.anchorMax = Vector2.one;
        lOTipSpace.offsetMin = Vector2.zero;
        lOTipSpace.offsetMax = Vector2.zero;
        mOSpellTip = UWUiKit.CreateText(lOTipSpace, "Spell hover", Vector2.zero, new Vector2(1f, 1f), TextAnchor.UpperRight, UWModernPalette.HudText, mOSession.TextUi, UWUiKit.HudText);
    }

    /// <summary>A new scale builds the icons again.</summary>
    public override void OnPixelScale(float pfPixelScale)
    {
        base.OnPixelScale(pfPixelScale);
        miSpellVersion = -1;
    }

    protected override void fUpdate()
    {
        fUpdateSpells();
        fPlaceSpells();
    }

    /// <summary>The active spells, rebuilt when they change (IUWModernMagic.ActiveSpellVersion), from the
    /// pictures the game's host names (the first game's as the classic shelf builds them, UWHudRunes).</summary>
    private void fUpdateSpells()
    {
        IUWModernMagic lOMagic = mOSession.Magic;

        if (miSpellVersion == lOMagic.ActiveSpellVersion)
            return;

        miSpellVersion = lOMagic.ActiveSpellVersion;

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
        {
            RawImage lOImage = mOSpellIcons[liSlot];

            mOSpellTextures[liSlot] = null;

            UWTexture lOSource = null;

            if (liSlot < lOMagic.ActiveSpellCount)
            {
                int liIcon = lOMagic.ActiveSpellPicture(liSlot);

                if (liIcon >= 0)
                    lOSource = mOSession.Data.Textures.GetTextureByType(UWTexture.TextureTypes.SPELLS, liIcon);
            }

            if (lOSource == null)
            {
                lOImage.enabled = false;
                continue;
            }

            UWIconPalette.ApplySmooth(lOImage);
            mOSpellTextures[liSlot] = UWIconCache.Texture(lOSource, mOSession.Filter);
            lOImage.texture = mOSpellTextures[liSlot];
            lOImage.enabled = true;
        }
    }

    /// <summary>
    /// THE ACTIVE SPELLS (per user, 2026-10-04: the rune panel now covers the top left): at the top
    /// right, left of the minimap - or of the character panel while it is out - as a buff row;
    /// the pointer over one shows its name and state.
    /// </summary>
    private void fPlaceSpells()
    {
        if (UWModernLayout.IsSpellHollowShown && fPlaceInHollow())
            return;

        mOHollow.enabled = false;

        float liScale = fScaleOf(UWModernLayout.ElementEnum.Spells);
        float lfMargin = 4f * miPixelScale;
        float lfRight = Screen.width - lfMargin;
        UWModernMinimap lOMap = UWModernMinimap.Instance;
        UWModernPanel lOPanel = UWModernPanel.Instance;

        if (lOMap != null && lOMap.ScreenRect.width > 0f)
            lfRight = Mathf.Min(lfRight, lOMap.ScreenRect.xMin - lfMargin);

        float lfTop = Screen.height - lfMargin;

        // Left of the open character panel only where it reaches up to the row (per user, 2026-11-09:
        // the Modern preset stands it on the bottom, far below).
        Rect lOOpen = lOPanel != null ? lOPanel.OpenRect : Rect.zero;

        if (lOOpen.width > 0f && lOOpen.yMax + (12f * miPixelScale) > lfTop - (17f * liScale))
            lfRight = Mathf.Min(lfRight, Mathf.Min(lOOpen.xMin, lOPanel.LeftEdge) - lfMargin);
        float lfX = lfRight;

        // Right-aligned, the first spell leftmost as on the classic page.
        float lfWidth = 0f;

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
        {
            if (mOSpellIcons[liSlot].enabled && mOSpellTextures[liSlot] != null)
                lfWidth += ((mOSpellTextures[liSlot].width * UWModernHudArt.PixelAspectX) + 3) * liScale;
        }

        // Its own place (UWModernLayout): right-aligned at its anchor, as wide as the spells - three
        // places' room while the layout is edited without any.
        float lfRowHeight = 17f * liScale;
        float lfRowWidth = lfWidth > 0f ? lfWidth - (3 * liScale) : (UWModernLayout.IsEditing ? 3f * 20f * liScale : 0f);

        if (lfRowWidth > 0f)
        {
            Rect lODefault = new Rect(lfRight - lfRowWidth, lfTop - lfRowHeight, lfRowWidth, lfRowHeight);
            Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Spells, lODefault);

            lfRight = lOPlaced.xMax;
            lfTop = lOPlaced.yMax;
            UWModernLayout.Report(UWModernLayout.ElementEnum.Spells, lOPlaced);
        }

        lfX = Mathf.Round(lfRight - lfWidth + (3 * liScale));

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
        {
            RawImage lOImage = mOSpellIcons[liSlot];
            Texture2D lOTexture = mOSpellTextures[liSlot];

            if (!lOImage.enabled || lOTexture == null)
            {
                mOSpellRects[liSlot] = Rect.zero;
                continue;
            }

            Rect lORect = new Rect(lfX, lfTop - (lOTexture.height * liScale), lOTexture.width * liScale * UWModernHudArt.PixelAspectX, lOTexture.height * liScale);

            // In the classic frame each icon at the original's slot, the first at the right.
            if (UWModernClassicFrame.TryGetSpellIcon(liSlot, lOTexture.width, lOTexture.height, out Rect lOSlot))
                lORect = lOSlot;

            mOSpellRects[liSlot] = lORect;
            ((RectTransform)lOImage.transform).anchoredPosition = lORect.position;
            ((RectTransform)lOImage.transform).sizeDelta = lORect.size;
            lfX += ((lOTexture.width * UWModernHudArt.PixelAspectX) + 3) * liScale;
        }

        // The hover text below the row, right-aligned to it.
        string lsTip = HoveredSpell >= 0 ? mOSession.Magic.ActiveSpellText(HoveredSpell) : string.Empty;

        mOSpellTip.enabled = !string.IsNullOrEmpty(lsTip);
        mOSpellTip.text = lsTip;
        mOSpellTip.fontSize = fFontOf(UWModernLayout.ElementEnum.Spells);

        fPlaceTip(lfRight, lfTop, lfTop - (17f * liScale), 2f * liScale);
    }

    /// <summary>
    /// The hover text right-aligned to the spells, below them - or above them where the screen has no room below (per user
    /// at the test of 202, 2026-11-24: the hollow at the bottom of the screen showed its text under the screen's edge).
    /// </summary>
    private void fPlaceTip(float pfRight, float pfTop, float pfBottom, float pfGap)
    {
        RectTransform lOTip = (RectTransform)mOSpellTip.transform;
        float lfHeight = FontSize * 2f;
        bool lbAbove = pfBottom - pfGap - lfHeight < 0f;

        // KEPT ON THE SCREEN (per user 2026-11-26, Classic Wide and 4:3 at narrow resolutions: a long text such as "... is
        // unstable" ran out at the left): right-aligned to the spells while it fits, else pushed right until its start
        // is on the screen, never past the right edge.
        UWUiFonts.ApplyNow(mOSpellTip);

        RectTransform lOSpace = lOTip.parent as RectTransform;
        float lfWidth = mOSpellTip.preferredWidth;
        float lfRight = pfRight;

        if (lOSpace != null && lfWidth > 0f)
            lfRight = Mathf.Min(Mathf.Max(pfRight, lfWidth + pfGap), Mathf.Max(lfWidth + pfGap, lOSpace.rect.width - pfGap));

        mOSpellTip.alignment = lbAbove ? TextAnchor.LowerRight : TextAnchor.UpperRight;
        lOTip.anchoredPosition = new Vector2(lfRight, lbAbove ? pfTop + pfGap + lfHeight : pfBottom - pfGap);
        lOTip.sizeDelta = new Vector2(600f, lfHeight);
    }

    /// <summary>
    /// The spells in the hollow: the hollow's picture as the element (by default where the row stands, at the top right),
    /// each spell at its frame place in it; false without the picture (the row then).
    /// </summary>
    private bool fPlaceInHollow()
    {
        bool lbOutline = UWModernClassicPlus.RuneHollowOutline;
        UWArtStamp lOStamp = UWVersionedTexture.Colours(lbOutline ? 1 : 0);

        if (!mOHollowArt.IsCurrent(lOStamp))
        {
            UWPicture lOPicture = UWDataImport.Presentation.UWFrameCutBuilder.BuildSpellHollow(mOSession.Data.Textures, lbOutline);

            mOHollowArt.Set(lOStamp, lOPicture != null ? UWTextureFactory.FromPicture(lOPicture, mOSession.Filter, "UWModernActiveSpells hollow") : null);
        }

        Texture2D lOHollow = mOHollowArt.Texture;

        if (lOHollow == null)
            return false;

        int liPad = lbOutline ? 1 : 0;
        float liScale = fScaleOf(UWModernLayout.ElementEnum.Spells);
        float lfAspect = liScale * UWModernHudArt.PixelAspectX;
        float lfMargin = 4f * miPixelScale;
        float lfWidth = lOHollow.width * lfAspect;
        float lfHeight = lOHollow.height * liScale;
        float lfRight = Screen.width - lfMargin;
        UWModernMinimap lOMap = UWModernMinimap.Instance;

        if (lOMap != null && lOMap.ScreenRect.width > 0f)
            lfRight = Mathf.Min(lfRight, lOMap.ScreenRect.xMin - lfMargin);

        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Spells,
            new Rect(lfRight - lfWidth, Screen.height - lfMargin - lfHeight, lfWidth, lfHeight));

        UWModernLayout.Report(UWModernLayout.ElementEnum.Spells, lOPlaced);

        mOHollow.texture = lOHollow;
        mOHollow.enabled = true;
        ((RectTransform)mOHollow.transform).anchoredPosition = lOPlaced.position;
        ((RectTransform)mOHollow.transform).sizeDelta = lOPlaced.size;

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
        {
            RawImage lOImage = mOSpellIcons[liSlot];
            Texture2D lOTexture = mOSpellTextures[liSlot];

            if (!lOImage.enabled || lOTexture == null || liSlot >= UWClassicLayoutUw2.Runes.SpellX.Length)
            {
                mOSpellRects[liSlot] = Rect.zero;
                continue;
            }

            UWDataImport.Presentation.UWFrameCutBuilder.SpellHollowIcon(liSlot, out int liX, out int liY);

            Rect lORect = new Rect(lOPlaced.x + ((liPad + liX) * lfAspect), lOPlaced.yMax - ((liPad + liY + lOTexture.height) * liScale),
                lOTexture.width * lfAspect, lOTexture.height * liScale);

            mOSpellRects[liSlot] = lORect;
            ((RectTransform)lOImage.transform).anchoredPosition = lORect.position;
            ((RectTransform)lOImage.transform).sizeDelta = lORect.size;
        }

        // The hover text below the hollow, right-aligned to it - above it at the screen's bottom.
        string lsTip = HoveredSpell >= 0 ? mOSession.Magic.ActiveSpellText(HoveredSpell) : string.Empty;

        mOSpellTip.enabled = !string.IsNullOrEmpty(lsTip);
        mOSpellTip.text = lsTip;
        mOSpellTip.fontSize = fFontOf(UWModernLayout.ElementEnum.Spells);

        fPlaceTip(lOPlaced.xMax, lOPlaced.yMax, lOPlaced.y, 2f * liScale);

        return true;
    }

    /// <summary>The active spell under a screen point, -1 for none.</summary>
    public int SpellAt(Vector2 pOPointer)
    {
        for (int liSlot = 0; liSlot < mOSpellRects.Length; liSlot++)
        {
            if (mOSpellRects[liSlot].width > 0f && mOSpellRects[liSlot].Contains(pOPointer))
                return liSlot;
        }

        return -1;
    }
}
