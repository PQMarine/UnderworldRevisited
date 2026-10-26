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
    /// ROTATION: eight steps (UWPanelTurn.Steps), at step four you see the panel from the side and thus
    /// only a line (per user, 2026-09-02). Implemented as squashing the width by the
    /// cosine, the content is squashed along with it and stays recognisable.
    /// </summary>
    private const float PanelLeft = UWClassicLayout.Panel.Left;

    private const float PanelTop = UWClassicLayout.Panel.Top;

    internal const float PanelWidth = UWClassicLayout.Panel.Width;

    private const float PanelHeight = UWClassicLayout.Panel.Height;

    /// <summary>Width of the panel when seen from the side.</summary>
    private const float PanelEdgeWidth = 3f;

    internal const float FrameWidth = UWClassicLayout.Screen.Width;

    internal const float FrameHeight = UWClassicLayout.Screen.Height;

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

    internal RawImage[] mRuneSlotImages;

    /// <summary>Which of the three pages the panel shows, the turn's steps and the conversation's
    /// hold on the inventory page: UWPanelTurn since cleanup 5.9 (2026-10-20). This class draws
    /// them and keeps the step timing (mfPanelStepUntil).</summary>
    private readonly UWPanelTurn mOTurn = new UWPanelTurn();

    internal UWPanelTurn Turn => mOTurn;

    private float mfPanelStepUntil;

    /// <summary>Puts the turn's pictures back at rest - the page at full width, no edge, the chain
    /// at picture 0 - after the last step or a turn cut short.</summary>
    private void fRestTurnPictures()
    {
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
        return mOTurn.IsAtRestOn(UWPanelTurn.SideEnum.Inventory) && !mOUi.Map.IsMapVisible;
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
    private const float ChainLeft = UWClassicLayout.Panel.ChainLeft;

    private const float ChainTop = UWClassicLayout.Panel.ChainTop;

    private const int ChainFrameCount = UWClassicLayout.Panel.ChainFrames;

    /// <summary>The short piece at the top of the panel where the chain attaches. Measured the same way:
    /// 272 / 3, all sixteen set pixels match.</summary>
    private const float ShortChainTop = UWClassicLayout.Panel.ShortChainTop;

    private const int ShortChainFirstFrame = UWClassicLayout.Panel.ShortChainFirstFrame;

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

            lOFrames[liFrame] = UWTextureFactory.FromTexture(lOSource, mOUi.TextureFilterMode, "UWHudPanel chain frame", TextureWrapMode.Clamp);
        }

        return lOFrames;
    }

    private RawImage fBuildChainImage(string psName, Texture2D[] pOFrames, float pfTop)
    {
        UWUiKit.CreateFrameImage(mOUi.mGameFrame, psName + "Blackout", ChainLeft, -pfTop,
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

        Texture2D lOTexture = UWTextureFactory.FromTexture(lOSource, mOUi.TextureFilterMode, "UWHudPanel panel edge", TextureWrapMode.Clamp);

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

        Image lOBlackout = UWUiKit.CreateFrameImage(mOUi.mGameFrame, "PanelBlackout",
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

        Texture2D lOTexture = UWTextureFactory.FromTexture(lOPanels[0], mOUi.TextureFilterMode, "UWHudPanel inventory page",
            TextureWrapMode.Clamp);

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

        Texture2D lOBackground = UWTextureFactory.FromTexture(lOPanels[2], mOUi.TextureFilterMode, "UWHudPanel stats page",
            TextureWrapMode.Clamp);

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

        Texture2D lOBackground = UWTextureFactory.FromTexture(lOPanels[1], mOUi.TextureFilterMode, "UWHudPanel rune shelf",
            TextureWrapMode.Clamp);

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
            // The grid of the rune shelf, measured on the picture (UWRuneShelfModel.SlotLeft and
            // SlotTop): one pixel right and one down since 2026-09-26 (per user, against the
            // original).
            lORect.anchoredPosition = new Vector2(UWRuneShelfModel.SlotLeft(liRune), -UWRuneShelfModel.SlotTop(liRune));
            lORect.sizeDelta = new Vector2(lOSource.Width, lOSource.Height);

            RawImage lOImage = lOObject.GetComponent<RawImage>();
            lOImage.texture = UWIconCache.Texture(lOSource, mOUi.TextureFilterMode);
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

    /// <summary>Starts the rotation if none is already running: the chain's page
    /// (UWPanelTurn.ChainTarget - from the rune shelf back to the inventory, not on to the
    /// stats; per user, 2026-09-03).</summary>
    public void TogglePanelSide()
    {
        fStartPanelTurn(mOTurn.ChainTarget);
    }

    /// <summary>Turns to the rune shelf, or from there back to the inventory. Hooked to the
    /// click on the rune bag (see UWItemDrag).</summary>
    public void ToggleRunePanel()
    {
        fStartPanelTurn(mOTurn.RuneBagTarget);
    }

    /// <summary>Turns to the given page. During a running rotation nothing
    /// happens, likewise if the page is already at the front.</summary>
    private void fStartPanelTurn(UWPanelTurn.SideEnum peTarget)
    {
        if (mOTurn.IsTurning || peTarget == mOTurn.Side)
            return;

        fBuildPanelPivot();
        fBuildPanelBackdrop();
        fBuildPanelEdge();
        fBuildChain();
        fBuildStatsPanel();
        fBuildRunePanel();

        if (mPanelPivot == null || mStatsRoot == null || mRuneRoot == null)
            return;

        mOTurn.Begin(peTarget);
        mfPanelStepUntil = Time.unscaledTime;
    }

    /// <summary>One rotation step per time tick (UWPanelTurn.Advance: the width follows the cosine
    /// over 180 degrees, in the middle the page is switched; the last step keeps its own dwell
    /// time).</summary>
    private void fUpdatePanelRotation()
    {
        if (!mOTurn.IsTurning || mPanelPivot == null)
            return;

        if (Time.unscaledTime < mfPanelStepUntil)
            return;

        UWPanelTurn.Frame lOFrame = mOTurn.Advance();

        if (lOFrame.Finished)
        {
            fRestTurnPictures();

            return;
        }

        mfPanelStepUntil = Time.unscaledTime + UWClassicLayout.Panel.TurnStepSeconds;

        // In the middle step the panel stands on its edge: instead of squashing it to zero
        // its own edge picture from PANELS.GR is shown.
        mPanelPivot.localScale = new Vector3(lOFrame.Scale, 1f, 1f);

        if (mPanelEdgeImage != null)
            mPanelEdgeImage.enabled = lOFrame.OnEdge;

        // The chain rotates along, but in its own pictures instead of by squashing - one picture per
        // step, without skipping.
        fSetChainFrame(lOFrame.ChainFrame);

        // Halfway the content flips over.
        if (lOFrame.Flipped)
            fShowPanelSide();
    }

    /// <summary>Shows the page UWPanelTurn.Side names. Only the three page nodes are switched -
    /// what is visible within a page is still decided by the page itself.</summary>
    private void fShowPanelSide()
    {
        UWPanelTurn.SideEnum leSide = mOTurn.Side;

        if (mInventorySide != null)
            mInventorySide.gameObject.SetActive(leSide == UWPanelTurn.SideEnum.Inventory);

        if (mStatsRoot != null)
            mStatsRoot.gameObject.SetActive(leSide == UWPanelTurn.SideEnum.Stats);

        if (mRuneRoot != null)
            mRuneRoot.gameObject.SetActive(leSide == UWPanelTurn.SideEnum.Runes);

        if (leSide == UWPanelTurn.SideEnum.Stats)
            fRefreshStats();
        else if (leSide == UWPanelTurn.SideEnum.Runes)
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
    ///
    /// What the lines say comes from UWCharacterSheet in the original's spelling (cleanup 5.9,
    /// 2026-10-20) - the name upper case and cut to fifteen, the class upper case, the level as
    /// an ordinal, "a/b"; the modern original stats page reads the same.
    /// </summary>
    private void fRefreshStats()
    {
        if (mStatsRoot == null || mOUi.mOUWData == null || mOUi.mCharacter == null)
            return;

        UWCharacterSheet lOSheet = fBindSheet();

        fSetStatsLine(0, lOSheet.Name, UWClassicLayout.Stats.NameRow, StatsAlignEnum.Centred, 0f);
        fSetStatsLine(1, lOSheet.ClassName, UWClassicLayout.Stats.ClassRow, StatsAlignEnum.Left, 0f);
        fSetStatsLine(2, lOSheet.LevelText, UWClassicLayout.Stats.ClassRow, StatsAlignEnum.Right, UWClassicLayout.Stats.LevelRight);

        for (int liRow = 0; liRow < UWCharacterSheet.ValueCount; liRow++)
        {
            fSetStatsLine(3 + liRow, lOSheet.Value(liRow),
                UWClassicLayout.Stats.FirstRow + (liRow * UWClassicLayout.Stats.RowHeight), StatsAlignEnum.Right,
                UWClassicLayout.Stats.ValueRight);
        }

        fRefreshSkillList();
    }

    /// <summary>The stats page's sheet: the original's spelling, six rows of the skill list and
    /// their paging.</summary>
    private readonly UWCharacterSheet mOSheet = new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Original, UWClassicLayout.Stats.SkillRows);

    /// <summary>The sheet bound to the game's strings, the player data and the live vitals.</summary>
    private UWCharacterSheet fBindSheet()
    {
        DataImport lOData = mOUi.mOUWData;

        return mOSheet.Bind(lOData != null ? lOData.Strings : null, lOData != null ? lOData.InitialPlayer : null,
            mOUi.mCharacter != null ? mOUi.mCharacter.Vitals : null);
    }

    /// <summary>Signature of the values shown on the stats page when it was last drawn.</summary>
    private int miStatsSignature;

    /// <summary>The language the stats page was last drawn in (UWText.LanguageVersion).</summary>
    private int miStatsLanguage = -1;

    /// <summary>
    /// Redraws the open stats page as soon as a shown value changes. In the original it
    /// updates while open - experience from exploring, hit points, mana (per user, 2026-09-14);
    /// here it was only drawn when the page was opened. The signature keeps the text textures
    /// from being rebuilt every frame.
    /// </summary>
    internal void fRefreshStatsIfChanged()
    {
        if (mOTurn.Side != UWPanelTurn.SideEnum.Stats || mOUi.mCharacter == null)
            return;

        // Every number the page shows (UWCharacterSheet.Signature); and its words, the class and skill
        // names of the game's strings, when the language changed (translation step 3R).
        int liSignature = fBindSheet().Signature;

        if (liSignature == miStatsSignature && miStatsLanguage == UWText.LanguageVersion)
            return;

        miStatsSignature = liSignature;
        miStatsLanguage = UWText.LanguageVersion;

        fRefreshStats();
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
    private const float StatsSkillFirstRowY = UWClassicLayout.Stats.SkillFirstRow;

    private const int StatsSkillRows = UWClassicLayout.Stats.SkillRows;

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
    private static readonly Rect mOStatsScrollUpRect = new Rect(UWClassicLayout.Stats.PageBack.X, UWClassicLayout.Stats.PageBack.Y,
        UWClassicLayout.Stats.PageBack.Width, UWClassicLayout.Stats.PageBack.Height);

    private static readonly Rect mOStatsScrollDownRect = new Rect(UWClassicLayout.Stats.PageForward.X, UWClassicLayout.Stats.PageForward.Y,
        UWClassicLayout.Stats.PageForward.Width, UWClassicLayout.Stats.PageForward.Height);

    private void fRefreshSkillList()
    {
        UWCharacterSheet lOSheet = fBindSheet();

        for (int liRow = 0; liRow < StatsSkillRows; liRow++)
        {
            string lsName;
            string lsValue;

            // Name and value of the entry at this row (UWCharacterSheet.TryGetSkill: the name
            // from UWCharacterText, the one home of the skill words; the live values).
            lOSheet.TryGetSkill(lOSheet.EntryAtRow(liRow), out lsName, out lsValue);

            float lfY = StatsSkillFirstRowY + (liRow * UWClassicLayout.Stats.RowHeight);
            Color lOSkillColour = UWUiKit.Colour(UWClassicLayout.Stats.SkillColour);

            fSetStatsLine(9 + liRow, lsName, lfY, StatsAlignEnum.Left, 0f, lOSkillColour);
            fSetStatsLine(15 + liRow, lsValue, lfY, StatsAlignEnum.Right, UWClassicLayout.Stats.ValueRight,
                lOSkillColour);
        }
    }

    /// <summary>Click on the two buttons below the list. Paging is by one
    /// row, as the original does (ovr145_445).</summary>
    private void fUpdateStatsScrollClicks()
    {
        if (!mOTurn.IsAtRestOn(UWPanelTurn.SideEnum.Stats) || mStatsRoot == null)
            return;

        if (!UWPointer.HasMouse || !UWMouseButtons.LeftPressed)
            return;

        Vector2 lOPosition = UWPointer.Position;

        int liStep = 0;

        if (fIsInStatsRect(mOStatsScrollUpRect, lOPosition))
            liStep = -1;
        else if (fIsInStatsRect(mOStatsScrollDownRect, lOPosition))
            liStep = 1;

        if (liStep == 0)
            return;

        // One entry per click, clamped to the list (UWCharacterSheet.PageSkills).
        mOSheet.PageSkills(liStep);

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
        fSetStatsLine(piIndex, psText, pfY, peAlign, pfRight, UWUiKit.Colour(UWClassicLayout.Stats.ValueColour));
    }

    private void fSetStatsLine(int piIndex, string psText, float pfY, StatsAlignEnum peAlign,
        float pfRight, Color pOColour)
    {
        if (mStatsLines == null)
        {
            mStatsLines = new RawImage[StatsLineCount];
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
            UWTextLabel.Hide(lOLine);
            return;
        }

        UWFont lOFont = mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Italic);

        if (lOFont == null)
            return;

        // The texture exactly as wide as the text. Previously there was an estimate here
        // (character count times font height plus two), and because the renderer CENTRES the line
        // within the given width, the padding right and left depended on
        // the character count. What got right-aligned was thus the texture, not
        // the text - every line needed its own edge. Recalculated against the original
        // (2026-09-03): for "18" it was 18 pixels for 11 pixels of text.
        //
        // MeasureText counts one more spacing after the last character, which
        // is subtracted (UWTextRaster.TightWidth) - otherwise one pixel of padding would remain.
        //
        // The label asks UWTextTextures for a new picture only when the text changed; until
        // 2026-10-19 every change (each HP or mana tick while the page was open) left the old
        // texture behind (B33, cleanup step 5.5).
        UWTextLabel.Get(lOLine).SetLine(lOFont, psText, UWTextRaster.TightWidth(lOFont, psText), pOColour, true,
            mOUi.TextureFilterMode, FontStyle.Italic);

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
                lOLineRect.anchoredPosition = new Vector2(UWClassicLayout.Stats.LabelLeft, -pfY);
                break;
        }

        UWTextLabel.Get(lOLine).Show();
    }

    /// <summary>Three header lines, six values, plus six skill names and their six
    /// values.</summary>
    private const int StatsLineCount = 21;

    private RawImage[] mStatsLines;

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
        if (UWConversationController.IsAnyOpen)
        {
            // THE INVENTORY SHOWS DURING A CONVERSATION whatever page was up before - the stats
            // page or the rune shelf - and afterwards that page is back (per user on the
            // original, 2026-09-18). Switched without the turn, a turn under way is cut short.
            // The hold and the page to give back are UWPanelTurn's (HoldForConversation).
            UWPanelTurn.ConversationChange lOChange = mOTurn.HoldForConversation();

            if (lOChange.TurnCut)
                fRestTurnPictures();

            if (lOChange.SideChanged)
                fShowPanelSide();

            return;
        }

        if (mOTurn.ReleaseAfterConversation())
            fShowPanelSide();

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
