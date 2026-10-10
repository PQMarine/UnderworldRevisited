using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The HUD section "the rotatable side panel with the chain, the stats page and the rune panel".
/// Its own class since 2026-09-18 (stage two of the HUD rebuild); the owner hands in the game
/// data, the frame, the character, the interaction and the inventory through mOUi. UWGameUI
/// keeps the public entry points as forwards.
/// </summary>
public sealed class UWHudPanel
{
    private readonly UWGameUI mOUi;

    internal UWHudPanel(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    // ---------------------------------------------------------------- Panel

    /// <summary>
    /// The rotatable panel on the right. At rest it shows the inventory; a click on the
    /// chain between the flasks turns it to the stats page.
    ///
    /// POSITION: 236 / 7, size 83 x 114. Not estimated but measured - PANELS.GR picture 0
    /// is the inventory back side, and the same pixels are in the main screen. When
    /// sliding it over, ALL samples match at mOUi spot (see the removed tool UWPanelFit).
    ///
    /// ROTATION: eight steps (PanelRotationSteps), at step four you see the panel from the side and thus
    /// only a line (per user, 2026-09-02). Implemented as squashing the width by the
    /// cosine, the content is squashed along with it and stays recognisable.
    /// </summary>
    private const float PanelLeft = 236f;

    private const float PanelTop = 7f;

    internal const float PanelWidth = 83f;

    private const float PanelHeight = 114f;

    private const int PanelRotationSteps = 8;

    /// <summary>Width of the panel when seen from the side.</summary>
    private const float PanelEdgeWidth = 3f;

    internal const float FrameWidth = 320f;

    internal const float FrameHeight = 200f;

    /// <summary>Node above everything belonging to the panel - only it gets squashed.</summary>
    private RectTransform mPanelPivot;

    /// <summary>The inventory page as its own node - see fBuildPanelPivot.</summary>
    private RectTransform mInventorySide;

    private RawImage mStatsPanelImage;

    private RectTransform mStatsRoot;

    /// <summary>The rune shelf, PANELS.GR picture 1 - the third page of the panel. It is reached
    /// not via the chain but via a click on the rune bag in the backpack
    /// (per user, 2026-09-03).</summary>
    internal RectTransform mRuneRoot;

    private RawImage mRunePanelImage;

    /// <summary>Grid of the rune shelf, measured on the picture - see fBuildRuneSlots.</summary>
    private const int RuneColumns = 4;

    // One pixel right and one down since 2026-09-26 (per user, against the original).
    private const float RuneSlotLeft = 8f;

    private const float RuneSlotTop = 5f;

    private const float RuneSlotPitchX = 18f;

    private const float RuneSlotPitchY = 15f;

    internal RawImage[] mRuneSlotImages;

    /// <summary>Which of the three pages the panel can show. Until 2026-09-03 there were two,
    /// as a bool - the rune bag added a third.</summary>
    internal enum PanelSideEnum
    {
        Inventory,
        Status,
        Runes
    }

    internal PanelSideEnum mePanelSide = PanelSideEnum.Inventory;

    private PanelSideEnum mePanelTargetSide;

    internal int miPanelStep = -1;

    private float mfPanelStepUntil;

    /// <summary>The page the panel showed before a conversation began; null outside one.</summary>
    private PanelSideEnum? mePanelSideBeforeConversation;

    /// <summary>Ends a turn under way at once, as the last step of fUpdatePanelRotation does.</summary>
    private void fCancelPanelTurn()
    {
        if (miPanelStep < 0)
            return;

        miPanelStep = -1;

        if (mPanelPivot != null)
            mPanelPivot.localScale = Vector3.one;

        if (mPanelEdgeImage != null)
            mPanelEdgeImage.enabled = false;

        fSetChainFrame(0);
    }

    /// <summary>
    /// Is the inventory page currently accepting clicks?
    ///
    /// Needed because RectTransformUtility.RectangleContainsScreenPoint also hits
    /// DISABLED objects. Without mOUi lock the hit zones of the
    /// slots keep reacting when the panel is turned to stats or runes - you could then
    /// blindly take off worn things (reported per user, 2026-09-03).
    ///
    /// Also locked during a running rotation: the panel stands askew then and
    /// the zones no longer lie where they appear.
    /// </summary>
    internal bool fIsInventoryClickable()
    {
        // The open map lies over everything - nothing behind it may hit any more (per
        // user, 2026-09-03; the same case as with the stats page).
        return mePanelSide == PanelSideEnum.Inventory && miPanelStep < 0 && !mOUi.Map.IsMapVisible;
    }

    /// <summary>
    /// Puts everything lying in the panel area under a common node.
    ///
    /// In our version the inventory elements sit directly on the game frame; without mOUi intermediate step
    /// there would be nothing that could be squashed. Selection is by POSITION, not by
    /// name - that way something added later or coming from the prefab
    /// is captured too.
    ///
    /// The node covers the whole frame so that the children keep their coordinates;
    /// only its pivot lies in the centre of the panel, so that the squashing starts there.
    /// </summary>
    private void fBuildPanelPivot()
    {
        if (mPanelPivot != null || mOUi.mGameFrame == null)
            return;

        // The widened classic frame moved the parts already: back to their original places, so
        // the area check below reads those and the pivot moves them (UWClassicWide; per user,
        // 2026-10-10: the inventory went away on the first turn - moved twice, off the screen).
        UWClassicWide.ReleaseAll();

        GameObject lOPivot = new GameObject("PanelPivot", typeof(RectTransform));
        lOPivot.transform.SetParent(mOUi.mGameFrame, false);

        mPanelPivot = (RectTransform)lOPivot.transform;
        mPanelPivot.anchorMin = Vector2.zero;
        mPanelPivot.anchorMax = Vector2.one;
        mPanelPivot.offsetMin = Vector2.zero;
        mPanelPivot.offsetMax = Vector2.zero;

        // In the widened classic frame it stays the original's 320 wide, moved with the panel
        // (UWClassicWide).
        UWClassicWide.FrameSized(mPanelPivot, (int)PanelLeft, (int)PanelTop);

        // Pivot to the centre of the panel, in fractions of the frame.
        mPanelPivot.pivot = new Vector2(
            (PanelLeft + (PanelWidth * 0.5f)) / FrameWidth,
            1f - ((PanelTop + (PanelHeight * 0.5f)) / FrameHeight));

        // The inventory page gets its own node, just like stats and runes. Until
        // 2026-09-03 its elements lay directly under the rotation node and were switched
        // on and off individually when turning the page - which lost their own state.
        // This became visible on the container grid, which occupies the same area and
        // is normally switched off: after a rotation it lay over the backpack, as
        // if a bag were open. It could not be noticed by itself, because
        // fRefreshContainerVisibility only intervenes on a real change (reported per user,
        // 2026-09-03).
        GameObject lOSide = new GameObject("InventorySide", typeof(RectTransform));
        lOSide.transform.SetParent(mPanelPivot, false);

        mInventorySide = (RectTransform)lOSide.transform;
        mInventorySide.anchorMin = Vector2.zero;
        mInventorySide.anchorMax = Vector2.one;
        mInventorySide.offsetMin = Vector2.zero;
        mInventorySide.offsetMax = Vector2.zero;

        System.Collections.Generic.List<Transform> lOToMove = new System.Collections.Generic.List<Transform>();

        for (int liChild = 0; liChild < mOUi.mGameFrame.childCount; liChild++)
        {
            RectTransform lOChild = mOUi.mGameFrame.GetChild(liChild) as RectTransform;

            if (lOChild == null || lOChild == mPanelPivot)
                continue;

            if (fIsInPanelArea(lOChild))
                lOToMove.Add(lOChild);
        }

        // The container window belongs to the inventory and rotates along (per user, 2026-09-03),
        // but fails the position check: it is anchored frame-filling, so its
        // anchor point does not lie in the panel area. Therefore added explicitly.
        if (mOUi.Inventory.mContainerPanelRect != null && !lOToMove.Contains(mOUi.Inventory.mContainerPanelRect))
            lOToMove.Add(mOUi.Inventory.mContainerPanelRect);

        foreach (Transform lOChild in lOToMove)
            lOChild.SetParent(mInventorySide, true);

        // The container window is the background of its slots and must go below them. When
        // reparenting, it otherwise ends up as the last child on top and covers the icons (reported per
        // user, 2026-09-03: "they are there and clickable, just invisible"). Before
        // it lay outside the rotation node and thus below everything inside it.
        // The panel background later slides in front of it (see fBuildPanelBackdrop), so
        // that the order reads wooden back side, container window, rest.
        if (mOUi.Inventory.mContainerPanelRect != null)
            mOUi.Inventory.mContainerPanelRect.SetAsFirstSibling();

        System.Text.StringBuilder lOMoved = new System.Text.StringBuilder();

        foreach (Transform lOChild in lOToMove)
            lOMoved.Append(lOChild.name).Append("  ");

        System.Text.StringBuilder lOStayed = new System.Text.StringBuilder();

        for (int liChild = 0; liChild < mOUi.mGameFrame.childCount; liChild++)
        {
            Transform lOChild = mOUi.mGameFrame.GetChild(liChild);

            if (lOChild != mPanelPivot)
                lOStayed.Append(lOChild.name).Append("  ");
        }

        Debug.Log(string.Format("[Panel] Rotating along ({0}): {1} | Staying: {2}",
            lOToMove.Count, lOMoved.ToString(), lOStayed.ToString()));
    }

    /// <summary>Does the anchor point of an element lie in the panel area? Computed from the
    /// top-left corner of the frame, as with all positions here.</summary>
    private static bool fIsInPanelArea(RectTransform pOChild)
    {
        Vector2 lOPosition = pOChild.anchoredPosition;

        float lfX = lOPosition.x;
        float lfY = -lOPosition.y;

        return lfX >= PanelLeft - 2f && lfX <= PanelLeft + PanelWidth + 2f
            && lfY >= PanelTop - 2f && lfY <= PanelTop + PanelHeight + 2f;
    }

    /// <summary>
    /// The chain between the flasks. It rotates with the panel, but is not
    /// squashed - it has its own pictures.
    ///
    /// CHAINS.GR brings sixteen of them: eight positions at 10x18 for the chain itself and
    /// eight at 10x4 for the short piece. Eight positions for half a revolution match
    /// the eight rotation steps.
    ///
    /// POSITION: 272 / 121. Measured as with the panel - picture 0 slid over the main screen,
    /// transparent pixels skipped, and at mOUi spot ALL
    /// set pixels match.
    ///
    /// As with the panel there is a black area underneath: the rest position is baked into the
    /// main screen and would otherwise shine through behind the other
    /// positions.
    /// </summary>
    private const float ChainLeft = 272f;

    private const float ChainTop = 121f;

    private const int ChainFrameCount = 8;

    /// <summary>The short piece at the top of the panel where the chain attaches. Measured the same way:
    /// 272 / 3, all sixteen set pixels match.</summary>
    private const float ShortChainTop = 3f;

    private const int ShortChainFirstFrame = 8;

    private RawImage mChainImage;

    private RawImage mShortChainImage;

    private Texture2D[] mChainFrames;

    private Texture2D[] mShortChainFrames;

    private int miChainFrame = -1;

    private void fBuildChain()
    {
        if (mChainImage != null || mOUi.mGameFrame == null || mOUi.mOUWData == null)
            return;

        System.Collections.Generic.List<UWTexture> lOChains =
            mOUi.mOUWData.Textures.GetTexturesByType(UWTexture.TextureTypes.CHAINS);

        if (lOChains == null || lOChains.Count < ChainFrameCount)
            return;

        if (lOChains.Count < ShortChainFirstFrame + ChainFrameCount)
            return;

        mChainFrames = fBuildChainFrames(lOChains, 0);
        mShortChainFrames = fBuildChainFrames(lOChains, ShortChainFirstFrame);

        mChainImage = fBuildChainImage("Chain", mChainFrames, ChainTop);
        mShortChainImage = fBuildChainImage("ShortChain", mShortChainFrames, ShortChainTop);

        fSetChainFrame(0);
    }

    private Texture2D[] fBuildChainFrames(System.Collections.Generic.List<UWTexture> pOChains, int piFirst)
    {
        Texture2D[] lOFrames = new Texture2D[ChainFrameCount];

        for (int liFrame = 0; liFrame < ChainFrameCount; liFrame++)
        {
            UWTexture lOSource = pOChains[piFirst + liFrame];

            Texture2D lOTexture = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.RGBA32, false);
            lOTexture.name = "UWGameUI.cs:2298";
            lOTexture.filterMode = mOUi.TextureFilterMode;
            lOTexture.wrapMode = TextureWrapMode.Clamp;
            lOTexture.SetPixels32(UWGameUI.fGetTextureInvert(lOSource));
            lOTexture.Apply(false, false);

            lOFrames[liFrame] = lOTexture;
        }

        return lOFrames;
    }

    private RawImage fBuildChainImage(string psName, Texture2D[] pOFrames, float pfTop)
    {
        UWGameUI.fCreateImage(psName + "Blackout", mOUi.mGameFrame, ChainLeft, -pfTop,
            pOFrames[0].width, pOFrames[0].height, Color.black);

        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(mOUi.mGameFrame, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(ChainLeft, -pfTop);
        lORect.sizeDelta = new Vector2(pOFrames[0].width, pOFrames[0].height);

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;

        return lOImage;
    }

    private void fSetChainFrame(int piFrame)
    {
        if (mChainFrames == null)
            return;

        int liFrame = Mathf.Clamp(piFrame, 0, mChainFrames.Length - 1);

        if (liFrame == miChainFrame)
            return;

        miChainFrame = liFrame;

        fApplyChainFrame(mChainImage, mChainFrames, liFrame);
        fApplyChainFrame(mShortChainImage, mShortChainFrames, liFrame);
    }

    private static void fApplyChainFrame(RawImage pOImage, Texture2D[] pOFrames, int piFrame)
    {
        if (pOImage == null || pOFrames == null || piFrame < 0 || piFrame >= pOFrames.Length)
            return;

        pOImage.texture = pOFrames[piFrame];
        ((RectTransform)pOImage.transform).sizeDelta =
            new Vector2(pOFrames[piFrame].width, pOFrames[piFrame].height);
    }

    /// <summary>The narrow side of the panel, visible in the middle step of the rotation. It
    /// stands outside the rotation node, so it is not squashed along.</summary>
    private RawImage mPanelEdgeImage;

    private void fBuildPanelEdge()
    {
        if (mPanelEdgeImage != null || mOUi.mGameFrame == null || mOUi.mOUWData == null)
            return;

        System.Collections.Generic.List<UWTexture> lOPanels =
            mOUi.mOUWData.Textures.GetTexturesByType(UWTexture.TextureTypes.PANELS);

        if (lOPanels == null || lOPanels.Count < 4)
            return;

        UWTexture lOSource = lOPanels[3];

        GameObject lOObject = new GameObject("PanelEdge", typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(mOUi.mGameFrame, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0.5f, 1f);
        lORect.anchoredPosition = new Vector2(PanelLeft + (PanelWidth * 0.5f), -PanelTop);
        lORect.sizeDelta = new Vector2(lOSource.Width, lOSource.Height);

        Texture2D lOTexture = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.RGBA32, false);
        lOTexture.name = "UWGameUI.cs:2384";
        lOTexture.filterMode = mOUi.TextureFilterMode;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(UWGameUI.fGetTextureInvert(lOSource));
        lOTexture.Apply(false, false);

        mPanelEdgeImage = lOObject.GetComponent<RawImage>();
        mPanelEdgeImage.texture = lOTexture;
        mPanelEdgeImage.raycastTarget = false;
        mPanelEdgeImage.enabled = false;
    }

    /// <summary>
    /// Black area behind the rotatable panel, and on it the inventory back side from
    /// PANELS.GR.
    ///
    /// Needed because the inventory circles are BAKED INTO the main screen (checked per user in the
    /// prefab, 2026-09-02). Without the cover they would stay visible while rotating,
    /// although the panel turns away. The original does it the same way: area black, panel
    /// on top.
    ///
    /// The black area stays OUTSIDE the rotation node, the panel picture inside it - so
    /// the squashing gradually reveals the black, as on the user's screenshot.
    /// </summary>
    private void fBuildPanelBackdrop()
    {
        if (mPanelPivot == null || mOUi.mOUWData == null || mPanelBlackout != null)
            return;

        Image lOBlackout = UWGameUI.fCreateImage("PanelBlackout", mOUi.mGameFrame,
            PanelLeft, -PanelTop, PanelWidth, PanelHeight, Color.black);

        mPanelBlackout = lOBlackout.rectTransform;

        // Directly under the rotation node, so that it lies above the main screen but behind the
        // panel.
        mPanelBlackout.SetSiblingIndex(mPanelPivot.GetSiblingIndex());

        System.Collections.Generic.List<UWTexture> lOPanels =
            mOUi.mOUWData.Textures.GetTexturesByType(UWTexture.TextureTypes.PANELS);

        if (lOPanels == null || lOPanels.Count < 1)
            return;

        GameObject lOBackground = new GameObject("InventoryPanel", typeof(RectTransform), typeof(RawImage));
        lOBackground.transform.SetParent(mInventorySide, false);
        lOBackground.transform.SetAsFirstSibling();

        RectTransform lORect = (RectTransform)lOBackground.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(PanelLeft, -PanelTop);
        lORect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Texture2D lOTexture = new Texture2D(lOPanels[0].Width, lOPanels[0].Height, TextureFormat.RGBA32, false);
        lOTexture.name = "UWGameUI.cs:2439";
        lOTexture.filterMode = mOUi.TextureFilterMode;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(UWGameUI.fGetTextureInvert(lOPanels[0]));
        lOTexture.Apply(false, false);

        RawImage lOImage = lOBackground.GetComponent<RawImage>();
        lOImage.texture = lOTexture;
        lOImage.raycastTarget = false;

        mInventoryPanelImage = lOImage;
    }

    private RectTransform mPanelBlackout;

    private RawImage mInventoryPanelImage;

    /// <summary>Creates the stats page: background from PANELS.GR picture 2, the values on top.</summary>
    private void fBuildStatsPanel()
    {
        if (mStatsRoot != null || mPanelPivot == null || mOUi.mOUWData == null)
            return;

        System.Collections.Generic.List<UWTexture> lOPanels =
            mOUi.mOUWData.Textures.GetTexturesByType(UWTexture.TextureTypes.PANELS);

        if (lOPanels == null || lOPanels.Count < 3)
            return;

        GameObject lORoot = new GameObject("StatsPanel", typeof(RectTransform), typeof(RawImage));
        lORoot.transform.SetParent(mPanelPivot, false);

        mStatsRoot = (RectTransform)lORoot.transform;
        mStatsRoot.anchorMin = new Vector2(0f, 1f);
        mStatsRoot.anchorMax = new Vector2(0f, 1f);
        mStatsRoot.pivot = new Vector2(0f, 1f);
        mStatsRoot.anchoredPosition = new Vector2(PanelLeft, -PanelTop);
        mStatsRoot.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Texture2D lOBackground = new Texture2D(lOPanels[2].Width, lOPanels[2].Height, TextureFormat.RGBA32, false);
        lOBackground.name = "UWGameUI.cs:2478";
        lOBackground.filterMode = mOUi.TextureFilterMode;
        lOBackground.wrapMode = TextureWrapMode.Clamp;
        lOBackground.SetPixels32(UWGameUI.fGetTextureInvert(lOPanels[2]));
        lOBackground.Apply(false, false);

        mStatsPanelImage = lORoot.GetComponent<RawImage>();
        mStatsPanelImage.texture = lOBackground;
        mStatsPanelImage.raycastTarget = false;

        mStatsRoot.gameObject.SetActive(false);
    }

    /// <summary>Creates the rune shelf: background from PANELS.GR picture 1, the rune slots
    /// on top (see fBuildRuneSlots).</summary>
    private void fBuildRunePanel()
    {
        if (mRuneRoot != null || mPanelPivot == null || mOUi.mOUWData == null)
            return;

        System.Collections.Generic.List<UWTexture> lOPanels =
            mOUi.mOUWData.Textures.GetTexturesByType(UWTexture.TextureTypes.PANELS);

        if (lOPanels == null || lOPanels.Count < 2)
            return;

        GameObject lORoot = new GameObject("RunePanel", typeof(RectTransform), typeof(RawImage));
        lORoot.transform.SetParent(mPanelPivot, false);

        mRuneRoot = (RectTransform)lORoot.transform;
        mRuneRoot.anchorMin = new Vector2(0f, 1f);
        mRuneRoot.anchorMax = new Vector2(0f, 1f);
        mRuneRoot.pivot = new Vector2(0f, 1f);
        mRuneRoot.anchoredPosition = new Vector2(PanelLeft, -PanelTop);
        mRuneRoot.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Texture2D lOBackground = new Texture2D(lOPanels[1].Width, lOPanels[1].Height, TextureFormat.RGBA32, false);
        lOBackground.name = "UWGameUI.cs:2515";
        lOBackground.filterMode = mOUi.TextureFilterMode;
        lOBackground.wrapMode = TextureWrapMode.Clamp;
        lOBackground.SetPixels32(UWGameUI.fGetTextureInvert(lOPanels[1]));
        lOBackground.Apply(false, false);

        mRunePanelImage = lORoot.GetComponent<RawImage>();
        mRunePanelImage.texture = lOBackground;
        mRunePanelImage.raycastTarget = false;

        mRuneRoot.gameObject.SetActive(false);

        fBuildRuneSlots();
    }

    /// <summary>
    /// The twenty-four slots on the shelf, four columns by six rows.
    ///
    /// POSITION: not counted off but measured on the picture (see the removed tool UWRuneShelfDump). The grooves
    /// of the grid are dips in the mean brightness per column and row; they lie
    /// horizontally at 7, 25, 43, 61 and vertically at 4, 19, 34, 49, 64, 79, each with the
    /// opposite edge 13 pixels further. That matches the rune pictures to the pixel, whose
    /// visible part is 14x14 in size.
    ///
    /// The pictures are the inventory icons of objects 232 to 255 from OBJECTS.GR. They are
    /// 16x16 in size, the last two rows at right and bottom are transparent - therefore
    /// the corner of the picture sits exactly on the corner of the cell.
    /// </summary>
    private void fBuildRuneSlots()
    {
        if (mRuneSlotImages != null || mRuneRoot == null || mOUi.mOUWData == null)
            return;

        mRuneSlotImages = new RawImage[UWPlayerData.RuneCount];

        for (int liRune = 0; liRune < mRuneSlotImages.Length; liRune++)
        {
            UWTexture lOSource = mOUi.mOUWData.Textures.GetTextureByType(
                UWTexture.TextureTypes.OBJECTS, UWPlayerData.FirstRuneObjectId + liRune);

            if (lOSource == null)
                continue;

            GameObject lOObject = new GameObject("Rune" + liRune, typeof(RectTransform), typeof(RawImage));
            lOObject.transform.SetParent(mRuneRoot, false);

            RectTransform lORect = (RectTransform)lOObject.transform;
            lORect.anchorMin = new Vector2(0f, 1f);
            lORect.anchorMax = new Vector2(0f, 1f);
            lORect.pivot = new Vector2(0f, 1f);
            lORect.anchoredPosition = new Vector2(
                RuneSlotLeft + ((liRune % RuneColumns) * RuneSlotPitchX),
                -(RuneSlotTop + ((liRune / RuneColumns) * RuneSlotPitchY)));
            lORect.sizeDelta = new Vector2(lOSource.Width, lOSource.Height);

            RawImage lOImage = lOObject.GetComponent<RawImage>();
            lOImage.texture = UWIconTextureBuilder.Build(lOSource, mOUi.TextureFilterMode);
            lOImage.raycastTarget = false;
            UWIconPalette.Apply(lOImage);
            lOImage.enabled = false;

            mRuneSlotImages[liRune] = lOImage;
        }

        fRefreshRunes();
    }

    /// <summary>Shows only the runes that are in the bag. The rest of the compartments stays empty
    /// - the shelf always has all twenty-four compartments.</summary>
    internal void fRefreshRunes()
    {
        if (mRuneSlotImages == null || mOUi.mOUWData == null)
            return;

        UWPlayerData lOPlayer = mOUi.mOUWData.InitialPlayer;

        for (int liRune = 0; liRune < mRuneSlotImages.Length; liRune++)
        {
            if (mRuneSlotImages[liRune] == null)
                continue;

            mRuneSlotImages[liRune].enabled =
                lOPlayer != null && lOPlayer.HasRune((UWPlayerData.Rune)liRune);
        }
    }

    /// <summary>Starts the rotation if none is already running.</summary>
    public void TogglePanelSide()
    {
        // The chain leads from the inventory to the stats page - and from every other page
        // back to the inventory. From the rune shelf it therefore does NOT go on to the stats
        // (per user, 2026-09-03): as soon as the inventory is turned away, the chain brings it
        // back.
        fStartPanelTurn(mePanelSide == PanelSideEnum.Inventory
            ? PanelSideEnum.Status
            : PanelSideEnum.Inventory);
    }

    /// <summary>Turns to the rune shelf, or from there back to the inventory. Hooked to the
    /// click on the rune bag (see UWItemDrag).</summary>
    public void ToggleRunePanel()
    {
        fStartPanelTurn(mePanelSide == PanelSideEnum.Runes
            ? PanelSideEnum.Inventory
            : PanelSideEnum.Runes);
    }

    /// <summary>Turns to the given page. During a running rotation nothing
    /// happens, likewise if the page is already at the front.</summary>
    private void fStartPanelTurn(PanelSideEnum peTarget)
    {
        if (miPanelStep >= 0 || peTarget == mePanelSide)
            return;

        fBuildPanelPivot();
        fBuildPanelBackdrop();
        fBuildPanelEdge();
        fBuildChain();
        fBuildStatsPanel();
        fBuildRunePanel();

        if (mPanelPivot == null || mStatsRoot == null || mRuneRoot == null)
            return;

        mePanelTargetSide = peTarget;
        miPanelStep = 0;
        mfPanelStepUntil = Time.unscaledTime;
    }

    /// <summary>One rotation step per time tick. The width follows the cosine over 180 degrees, in
    /// the middle the page is switched.</summary>
    private void fUpdatePanelRotation()
    {
        if (miPanelStep < 0 || mPanelPivot == null)
            return;

        if (Time.unscaledTime < mfPanelStepUntil)
            return;

        // The last step needs its own dwell time. Until 2026-09-02 the reset happened directly
        // after it, in the same pass - it was set and immediately
        // overwritten again and thus never visible (reported per user, first for
        // step 6, after the switch to eight steps for step 7).
        if (miPanelStep >= PanelRotationSteps)
        {
            miPanelStep = -1;
            mPanelPivot.localScale = Vector3.one;

            if (mPanelEdgeImage != null)
                mPanelEdgeImage.enabled = false;

            fSetChainFrame(0);

            return;
        }

        mfPanelStepUntil = Time.unscaledTime + Mathf.Max(0.01f, mOUi.mfPanelStepSeconds);

        // Eight steps, as many as the chain has pictures (per user, 2026-09-02). The
        // angle runs in eighths over 180 degrees, the edge-on position thus falls on
        // step 4 - exactly where the chain also has its edge picture.
        float lfAngle = (miPanelStep / (float)PanelRotationSteps) * Mathf.PI;

        float lfScale = Mathf.Abs(Mathf.Cos(lfAngle));

        // In the middle step the panel stands on its edge: instead of squashing it to zero
        // its own edge picture from PANELS.GR is shown.
        bool lbOnEdge = miPanelStep == PanelRotationSteps / 2;

        mPanelPivot.localScale = new Vector3(lbOnEdge ? 0f : lfScale, 1f, 1f);

        if (mPanelEdgeImage != null)
            mPanelEdgeImage.enabled = lbOnEdge;

        // The chain rotates along, but in its own pictures instead of by squashing - one picture per
        // step, without skipping.
        fSetChainFrame(miPanelStep);

        // Halfway the content flips over.
        if (lbOnEdge)
            fShowPanelSide(mePanelTargetSide);

        miPanelStep++;
    }

    /// <summary>Turns the page. Only the three page nodes are switched - what is visible within
    /// a page is still decided by the page itself.</summary>
    private void fShowPanelSide(PanelSideEnum peSide)
    {
        mePanelSide = peSide;

        if (mInventorySide != null)
            mInventorySide.gameObject.SetActive(peSide == PanelSideEnum.Inventory);

        if (mStatsRoot != null)
            mStatsRoot.gameObject.SetActive(peSide == PanelSideEnum.Status);

        if (mRuneRoot != null)
            mRuneRoot.gameObject.SetActive(peSide == PanelSideEnum.Runes);

        if (peSide == PanelSideEnum.Status)
            fRefreshStats();
        else if (peSide == PanelSideEnum.Runes)
            fRefreshRunes();
    }

    /// <summary>
    /// Writes name, class, level and the six values onto the stats page.
    ///
    /// The labels STR to EXP are already in the background; their rows are measured on the
    /// picture (see the removed tool UWStatsLayoutDump): they begin at Y 21 and are spaced
    /// seven pixels apart. The values stand right-aligned next to them.
    ///
    /// Font is font5x6i, the italic one - in the original the font of the character values.
    /// </summary>
    private void fRefreshStats()
    {
        if (mStatsRoot == null || mOUi.mOUWData == null || mOUi.mCharacter == null)
            return;

        UWPlayerData lOPlayer = mOUi.mOUWData.InitialPlayer;

        fSetStatsLine(0, fGetCharacterName(lOPlayer), mOUi.mOStatsNameY, StatsAlignEnum.Centred, 0f);
        fSetStatsLine(1, fGetClassName(lOPlayer), mOUi.mOStatsClassY, StatsAlignEnum.Left, 0f);
        fSetStatsLine(2, fGetLevelText(), mOUi.mOStatsClassY, StatsAlignEnum.Right, mOUi.mOStatsLevelRight);

        string[] lsValues =
        {
            mOUi.mCharacter.Strength.ToString(),
            mOUi.mCharacter.Dexterity.ToString(),
            mOUi.mCharacter.Intelligence.ToString(),
            Mathf.RoundToInt(mOUi.mCharacter.CurrentHP) + "/" + Mathf.RoundToInt(mOUi.mCharacter.MaxHP),
            Mathf.RoundToInt(mOUi.mCharacter.CurrentMana) + "/" + Mathf.RoundToInt(mOUi.mCharacter.MaxMana),
            fGetExperienceText()
        };

        for (int liRow = 0; liRow < lsValues.Length; liRow++)
        {
            fSetStatsLine(3 + liRow, lsValues[liRow],
                mOUi.mOStatsFirstRowY + (liRow * mOUi.mOStatsRowHeight), StatsAlignEnum.Right,
                mOUi.mOStatsValueRight);
        }

        fRefreshSkillList();
    }

    /// <summary>Signature of the values shown on the stats page when it was last drawn.</summary>
    private int miStatsSignature;

    /// <summary>
    /// Redraws the open stats page as soon as a shown value changes. In the original it
    /// updates while open - experience from exploring, hit points, mana (per user, 2026-09-14);
    /// here it was only drawn when the page was opened. The signature keeps the text textures
    /// from being rebuilt every frame.
    /// </summary>
    internal void fRefreshStatsIfChanged()
    {
        if (mePanelSide != PanelSideEnum.Status || mOUi.mCharacter == null)
            return;

        unchecked
        {
            int liSignature = 17;

            liSignature = (liSignature * 31) + mOUi.mCharacter.Strength;
            liSignature = (liSignature * 31) + mOUi.mCharacter.Dexterity;
            liSignature = (liSignature * 31) + mOUi.mCharacter.Intelligence;
            liSignature = (liSignature * 31) + Mathf.RoundToInt(mOUi.mCharacter.CurrentHP);
            liSignature = (liSignature * 31) + Mathf.RoundToInt(mOUi.mCharacter.MaxHP);
            liSignature = (liSignature * 31) + Mathf.RoundToInt(mOUi.mCharacter.CurrentMana);
            liSignature = (liSignature * 31) + Mathf.RoundToInt(mOUi.mCharacter.MaxMana);
            liSignature = (liSignature * 31) + (mOUi.mCharacter.Experience / 10);
            liSignature = (liSignature * 31) + mOUi.mCharacter.Level;
            liSignature = (liSignature * 31) + mOUi.mCharacter.SkillPoints;

            for (int liSkill = 0; liSkill < UWCharacter.SkillNumberCount; liSkill++)
                liSignature = (liSignature * 31) + mOUi.mCharacter.GetSkillByNumber(liSkill);

            if (liSignature == miStatsSignature)
                return;

            miStatsSignature = liSignature;
        }

        fRefreshStats();
    }

    /// <summary>
    /// Experience is stored in tenths in the file - whole points are shown, so
    /// divided by ten and truncated. Internally calculation continues with the tenths, the
    /// thresholds in UWExperience are in mOUi unit too.
    /// </summary>
    private string fGetExperienceText()
    {
        return (mOUi.mCharacter.Experience / 10).ToString();
    }

    /// <summary>
    /// The skill list in the lower box of the stats page.
    ///
    /// Measured on the panel picture: the recessed box reaches from Y 63 to 104, that is
    /// exactly six rows in the same seven-pixel grid as the values above. The two
    /// paging buttons sit below at about Y 105 to 112.
    ///
    /// Twenty entries are shown: Attack and Defense, then the eighteen skills.
    /// Their names are in string block 2 from entry 32 on and match in order
    /// with UWPlayerData.Skill.
    /// </summary>
    private const float StatsSkillFirstRowY = 64f;

    private const int StatsSkillRows = 6;

    private const int StatsSkillCount = 20;

    private const int StatsSkillNameString = UWPlayerData.SkillNameStringIndex;

    /// <summary>
    /// The two paging buttons. The VISIBLE symbols sit at about X 30 and 45,
    /// but the whole width up to the panel edge is clickable (tested per user in the original,
    /// 2026-09-03) - the left area reaches out to the left, the right one
    /// to the right.
    ///
    /// READ 2026-09-25 (ovr145_445, the stats panel's click, whole): a click in the bottom
    /// 8 rows of the panel pages - left of x 37 BACK, from 37 on FORWARD, one skill per click,
    /// the offset clamped to 0..14. Ours had the direction right and split at 43 over the
    /// bottom 10 rows.
    /// </summary>
    private static readonly Rect mOStatsScrollUpRect = new Rect(0f, 106f, 37f, 8f);

    private static readonly Rect mOStatsScrollDownRect = new Rect(37f, 106f, 46f, 8f);

    private int miStatsSkillOffset;

    /// <summary>Name and value of a list entry.</summary>
    private void fGetSkillEntry(int piEntry, out string psName, out string psValue)
    {
        psName = string.Empty;
        psValue = string.Empty;

        if (piEntry < 0 || piEntry >= StatsSkillCount || mOUi.mOUWData == null)
            return;

        try
        {
            psName = mOUi.mOUWData.Strings.Blocks[2].Strings[StatsSkillNameString + piEntry].Trim();
        }
        catch
        {
            return;
        }

        if (piEntry == 0)
        {
            psValue = mOUi.mCharacter != null ? mOUi.mCharacter.Attack.ToString() : string.Empty;

            return;
        }

        if (piEntry == 1)
        {
            // The live value (UWPlayerVitals.Defence). Until 2026-10-01 this still read the
            // loaded save game, from before we kept our own - a mantra at a shrine raised it
            // ("You have advanced greatly in defense") and the page kept the old number (per
            // user, ANRA on SAVE1).
            psValue = mOUi.mCharacter != null ? mOUi.mCharacter.Defence.ToString() : string.Empty;

            return;
        }

        if (mOUi.mCharacter != null)
            psValue = mOUi.mCharacter.GetSkill((UWPlayerData.Skill)(piEntry - 2)).ToString();
    }

    private void fRefreshSkillList()
    {
        for (int liRow = 0; liRow < StatsSkillRows; liRow++)
        {
            int liEntry = miStatsSkillOffset + liRow;

            string lsName;
            string lsValue;

            fGetSkillEntry(liEntry, out lsName, out lsValue);

            float lfY = StatsSkillFirstRowY + (liRow * mOUi.mOStatsRowHeight);

            fSetStatsLine(9 + liRow, lsName, lfY, StatsAlignEnum.Left, 0f, mOUi.mOStatsSkillColour);
            fSetStatsLine(15 + liRow, lsValue, lfY, StatsAlignEnum.Right, mOUi.mOStatsValueRight,
                mOUi.mOStatsSkillColour);
        }
    }

    /// <summary>Click on the two buttons below the list. Paging is by one
    /// row, as the original does (ovr145_445).</summary>
    private void fUpdateStatsScrollClicks()
    {
        if (mePanelSide != PanelSideEnum.Status || miPanelStep >= 0 || mStatsRoot == null)
            return;

        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (lOMouse == null || !UWMouseButtons.LeftPressed)
            return;

        Vector2 lOPosition = lOMouse.position.ReadValue();

        int liStep = 0;

        if (fIsInStatsRect(mOStatsScrollUpRect, lOPosition))
            liStep = -1;
        else if (fIsInStatsRect(mOStatsScrollDownRect, lOPosition))
            liStep = 1;

        if (liStep == 0)
            return;

        int liMax = Mathf.Max(0, StatsSkillCount - StatsSkillRows);

        miStatsSkillOffset = Mathf.Clamp(miStatsSkillOffset + liStep, 0, liMax);

        fRefreshSkillList();
    }

    /// <summary>Is the pointer in a field of the stats page? Computed in
    /// panel coordinates, because the buttons have no hit areas of their own.</summary>
    private bool fIsInStatsRect(Rect pORect, Vector2 pOScreenPos)
    {
        Vector2 lOLocal;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mStatsRoot, pOScreenPos, null, out lOLocal))
            return false;

        float lfY = -lOLocal.y;

        return lOLocal.x >= pORect.x && lOLocal.x <= pORect.x + pORect.width
            && lfY >= pORect.y && lfY <= pORect.y + pORect.height;
    }

    private enum StatsAlignEnum
    {
        Left,
        Right,
        Centred
    }

    /// <summary>One line on the stats page. The pictures are kept and only
    /// redrawn on changed text.</summary>
    private void fSetStatsLine(int piIndex, string psText, float pfY, StatsAlignEnum peAlign, float pfRight)
    {
        fSetStatsLine(piIndex, psText, pfY, peAlign, pfRight, mOUi.mOStatsColour);
    }

    private void fSetStatsLine(int piIndex, string psText, float pfY, StatsAlignEnum peAlign,
        float pfRight, Color pOColour)
    {
        if (mStatsLines == null)
        {
            mStatsLines = new RawImage[StatsLineCount];
            msStatsLineTexts = new string[StatsLineCount];
        }

        if (piIndex < 0 || piIndex >= mStatsLines.Length)
            return;

        if (mStatsLines[piIndex] == null)
        {
            GameObject lOObject = new GameObject("StatsLine" + piIndex, typeof(RectTransform), typeof(RawImage));
            lOObject.transform.SetParent(mStatsRoot, false);

            RectTransform lORect = (RectTransform)lOObject.transform;
            lORect.anchorMin = new Vector2(0f, 1f);
            lORect.anchorMax = new Vector2(0f, 1f);

            mStatsLines[piIndex] = lOObject.GetComponent<RawImage>();
            mStatsLines[piIndex].raycastTarget = false;
        }

        RawImage lOLine = mStatsLines[piIndex];

        if (string.IsNullOrEmpty(psText))
        {
            msStatsLineTexts[piIndex] = null;
            UWTextLabel.Hide(lOLine);
            return;
        }

        if (msStatsLineTexts[piIndex] != psText)
        {
            msStatsLineTexts[piIndex] = psText;

            UWFont lOFont = mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Italic);

            if (lOFont == null)
                return;

            // The texture exactly as wide as the text. Previously there was an estimate here
            // (character count times font height plus two), and because UWFontRenderer CENTRES the line
            // within the given width, the padding right and left depended on
            // the character count. What got right-aligned was thus the texture, not
            // the text - every line needed its own edge. Recalculated against the original
            // (2026-09-03): for "18" it was 18 pixels for 11 pixels of text.
            //
            // MeasureText counts one more spacing after the last character, which
            // is subtracted here - otherwise one pixel of padding would remain.
            int liWidth = Mathf.Max(1,
                lOFont.MeasureText(psText, 0, UWFontRenderer.CharacterSpacing)
                - UWFontRenderer.CharacterSpacing);

            Texture2D lOTexture = UWFontRenderer.RenderLines(lOFont,
                new System.Collections.Generic.List<string> { psText },
                liWidth, pOColour, mOUi.TextureFilterMode);

            if (lOTexture == null)
                return;

            UWTextLabel.Get(lOLine).Set(lOFont, lOTexture,
                new System.Collections.Generic.List<string> { psText },
                new System.Collections.Generic.List<Color32> { pOColour }, true, FontStyle.Italic);
        }

        RectTransform lOLineRect = (RectTransform)lOLine.transform;

        switch (peAlign)
        {
            case StatsAlignEnum.Right:
                lOLineRect.pivot = new Vector2(1f, 1f);
                lOLineRect.anchoredPosition = new Vector2(pfRight, -pfY);
                break;

            case StatsAlignEnum.Centred:
                lOLineRect.pivot = new Vector2(0.5f, 1f);
                lOLineRect.anchoredPosition = new Vector2(PanelWidth * 0.5f, -pfY);
                break;

            default:
                lOLineRect.pivot = new Vector2(0f, 1f);
                lOLineRect.anchoredPosition = new Vector2(mOUi.mOStatsLabelLeft, -pfY);
                break;
        }

        UWTextLabel.Get(lOLine).Show();
    }

    private string fGetCharacterName(UWPlayerData pOPlayer)
    {
        return pOPlayer != null && !string.IsNullOrEmpty(pOPlayer.Name) ? pOPlayer.Name.ToUpper() : "AVATAR";
    }

    /// <summary>Class name from string block 2 from 0x18 on.</summary>
    private string fGetClassName(UWPlayerData pOPlayer)
    {
        if (pOPlayer == null)
            return string.Empty;

        try
        {
            // Fighter is at 24, not at 23 - the export in AllStrings.txt writes the
            // raw runtime indices, so there is no offset here. With the old base
            // every class was off by one (reported per user, 2026-09-03).
            return mOUi.mOUWData.Strings.Blocks[2].Strings[0x18 + pOPlayer.CharacterClass].Trim().ToUpper();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Level as ordinal number, as in the original: 1ST, 2ND, 3RD, 4TH ...</summary>
    private string fGetLevelText()
    {
        int liLevel = mOUi.mCharacter.Level;
        int liLastTwo = liLevel % 100;
        int liLast = liLevel % 10;

        string lsSuffix = "TH";

        if (liLastTwo < 11 || liLastTwo > 13)
        {
            if (liLast == 1)
                lsSuffix = "ST";
            else if (liLast == 2)
                lsSuffix = "ND";
            else if (liLast == 3)
                lsSuffix = "RD";
        }

        return liLevel + lsSuffix;
    }

    /// <summary>Three header lines, six values, plus six skill names and their six
    /// values.</summary>
    private const int StatsLineCount = 21;

    private RawImage[] mStatsLines;

    private string[] msStatsLineTexts;

    /// <summary>Panel: the clicks on it and the running rotation. THE CHAIN HAS NO AREA OF ITS
    /// OWN (2026-09-26): in the original it turns the panel from the flask area's middle
    /// columns (UWClickRules.FlaskAt, UWHudRunes.fUpdateFlaskClicks) and from F7 (key 0x86
    /// in the key table) - our own estimated area lay wholly inside the flask area's part.</summary>
    internal void Update()
    {
        // A CUTSCENE IS A MODAL LOOP in the original: nothing of the panel reacts. Ours cast the
        // prepared spell from a click on the rune shelf while a cutscene ran (per user,
        // 2026-10-02) - the cutscene's input lock reached the world and the backpack, not the
        // panel.
        UWIntroPlayer lOCutscene = UWScene.IntroPlayer;

        if (lOCutscene != null && lOCutscene.IsPlaying)
            return;

        // As long as the map is open it is modal - no click gets past it.
        if (mOUi.Map.IsMapVisible)
        {
            mOUi.Map.Update();

            return;
        }

        // IN A CONVERSATION THE PANEL RESTS: the options button, the chain, the runes, the
        // spell icons, the flasks and the stats scroll do not react (per user on the original,
        // 2026-09-18; the compass is already out through Interaction's conversation gate).
        // Only a turn that is under way finishes; the backpack stays UWItemDrag's, trading
        // needs it.
        if (UWConversationScreen.IsAnyOpen)
        {
            // THE INVENTORY SHOWS DURING A CONVERSATION whatever page was up before - the stats
            // page or the rune shelf - and afterwards that page is back (per user on the
            // original, 2026-09-18). Switched without the turn, a turn under way is cut short.
            if (!mePanelSideBeforeConversation.HasValue)
            {
                mePanelSideBeforeConversation = miPanelStep >= 0 ? mePanelTargetSide : mePanelSide;
                fCancelPanelTurn();

                if (mePanelSide != PanelSideEnum.Inventory)
                    fShowPanelSide(PanelSideEnum.Inventory);
            }

            return;
        }

        if (mePanelSideBeforeConversation.HasValue)
        {
            if (mePanelSideBeforeConversation.Value != mePanelSide)
                fShowPanelSide(mePanelSideBeforeConversation.Value);

            mePanelSideBeforeConversation = null;
        }

        // BEFORE ANYTHING ELSE: as long as the options panel is open, the mouse belongs to it.
        if (mOUi.Options.Update())
            return;

        if (mOUi.Commands.Update())
            return;

        mOUi.Runes.fUpdateRuneClicks();
        mOUi.Runes.fUpdateSpellCast();
        mOUi.Runes.fUpdateSpellIconClicks();
        mOUi.Runes.fUpdateFlaskClicks();
        fUpdateStatsScrollClicks();
        fUpdatePanelRotation();
    }
}
