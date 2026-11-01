using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S HUD ON THE SECOND GAME (UW2 plan phase 3 step 8): bottom left the two flasks with their
/// values, beside them the scroll's last lines, in the view's middle the game's own pointer and the name of what
/// it is on. The first game's UWModernHud stands on UWCharacter, Interaction and UWGameUI, which do not run here,
/// so this is its own class beside it, fed from the record by UWGameScreenUw2. The flasks are built as the
/// classic screen builds them: the empty flask and up to twelve slices at liquid_y's rows.
///
/// THE BAGS, bottom right: the eight places of the backpack always, opened (B) also the eleven of the paperdoll
/// and the open bag. Each cell is one of the classic page's displays: what it shows and what a click does are
/// UWGameScreenUw2's, the same code as on that page, so nothing about taking, stacking or containers differs.
///
/// THE MINIMAP, top right: the map screen as the big map paints it (UWAutomapPainter in the map's palette), a
/// window of it round the character, north up, repainted twice a second. Only with the map carried and the
/// automap on: with it off the player does not know where they are (UWAutomapStateUw2).
/// </summary>
public sealed class UWModernHudUw2 : MonoBehaviour
{
    /// <summary>The interface's pixels between the flasks and the screen's edges and between each other.</summary>
    private const float Margin = 4f;

    /// <summary>The lines of the scroll shown, how long, and how wide they wrap.</summary>
    private const int ShownLines = 3;

    private const float LineSeconds = 6f;

    private const int LineWidth = 300;

    /// <summary>The narrowest the lines wrap when the action bar leaves them little room.</summary>
    private const int LeastLineWidth = 100;

    /// <summary>How often the pointer's target is asked for, in seconds.</summary>
    private const float TargetSeconds = 0.1f;

    /// <summary>The rows between the pointer's middle and the target's name.</summary>
    private const float TargetBelow = 8f;

    private DataImport mOData;

    private Canvas mOCanvas;

    private readonly RawImage[] mOEmpty = new RawImage[2];

    private readonly RawImage[][] mOSlices = new RawImage[2][];

    private readonly RawImage[] mOValues = new RawImage[2];

    private RawImage mOLines;

    private RawImage mOPointer;

    private RawImage mOTarget;

    private readonly Dictionary<int, Texture2D> mOPictures = new Dictionary<int, Texture2D>();

    /// <summary>The minimap's side in the interface's pixels, the map pixels it shows across, its repaint.</summary>
    private const float MinimapSize = 96f;

    private const float MinimapWindow = 48f;

    private const float MinimapRefreshSeconds = 0.5f;

    private const float MinimapArrow = 6f;

    /// <summary>Where a tile's cell starts on the map screen, and its pixels (UWAutomapPainter).</summary>
    private const float MapCellLeft = 7f;

    private const float MapCellBottom = 4f;

    private const float MapPixelsATile = 3f;

    /// <summary>The original's position units to a tile.</summary>
    private const float UnitsATile = 256f;

    private static readonly uint msSeed = UWAutomapView.SeedFrom(System.Environment.TickCount);

    private Image mOMinimapFrame;

    private RawImage mOMinimap;

    private RawImage mOMinimapArrow;

    private Texture2D mOMapTexture;

    private Color32[] mOMapPixels;

    private Color32[] mOMapColours;

    private float mfNextMap;

    /// <summary>The level the minimap's picture shows.</summary>
    private UWLevel mOMapLevel;

    /// <summary>A cell's side and the gap, in the interface's pixels; columns of the grid.</summary>
    private const float CellSize = 20f;

    private const float CellGap = 1f;

    private const int CellColumns = 4;

    private const int PackCells = 8;

    private static readonly Color CellShut = new Color(0f, 0f, 0f, 0.45f);

    private static readonly Color CellOpen = new Color(0f, 0f, 0f, 0.75f);

    /// <summary>The cells' displays: the backpack's eight first, then the paperdoll's and the open bag.</summary>
    private readonly List<int> mODisplays = new List<int>();

    private readonly List<Image> mOCells = new List<Image>();

    private readonly List<RawImage> mOCellPictures = new List<RawImage>();

    private readonly List<RawImage> mOCellCounts = new List<RawImage>();

    private readonly List<Rect> mOCellRects = new List<Rect>();

    private readonly Dictionary<int, Texture2D> mOItemPictures = new Dictionary<int, Texture2D>();

    /// <summary>The action bar, made with the canvas.</summary>
    public UWModernBarUw2 Bar { get; private set; }

    /// <summary>The display of the cell under a screen point, 0 for none; only cells shown.</summary>
    public int DisplayAt(Vector2 pOPointer)
    {
        for (int liAt = 0; liAt < mOCellRects.Count; liAt++)
        {
            if (mOCells[liAt].enabled && mOCellRects[liAt].Contains(pOPointer))
                return mODisplays[liAt];
        }

        return 0;
    }

    private readonly List<string> mOText = new List<string>();

    private float mfNextTarget;

    private string msTarget = string.Empty;

    private Color32 fInk => UWModernPalette.HudText;

    public void SetShown(bool pbShown)
    {
        if (mOCanvas != null)
            mOCanvas.enabled = pbShown;
    }

    /// <summary>The HUD for this frame. pfSaidAgo: the seconds since the last line of the scroll.</summary>
    public void Tick(DataImport pOData, UWPlayerFile pOFile, List<string> pOLines, float pfSaidAgo, UWGameScreenUw2 pOScreen, bool pbBagsOpen)
    {
        mOData = pOData;

        if (mOData == null)
            return;

        fEnsureBuilt();

        float lfScale = UWModernUi.PixelScale;

        // The conversation (UWModernConversation) covers the world: only the bags stand beside it.
        if (UWConversationScreenUw2.IsOpen)
        {
            fHideForTalk();
            fShowBags(pOScreen, pbBagsOpen, lfScale);

            return;
        }

        float lfRight = fShowFlask(0, pOFile, Margin * lfScale, lfScale);

        lfRight = fShowFlask(1, pOFile, lfRight + (Margin * lfScale), lfScale);
        fShowLines(pOLines, pfSaidAgo, lfRight + (Margin * lfScale), lfScale);
        fShowPointer(lfScale);
        fShowBags(pOScreen, pbBagsOpen, lfScale);
        Bar.Tick(pOScreen != null ? pOScreen.Inventory : null, mOData, lfScale, fInk, fItemPicture);
        fShowMinimap(pOFile, pOScreen, lfScale);

        mOPointer.enabled = mOPointer.texture != null && !pbBagsOpen;
    }

    private void fHideForTalk()
    {
        for (int liFlask = 0; liFlask < 2; liFlask++)
        {
            mOEmpty[liFlask].enabled = false;
            UWTextLabel.Hide(mOValues[liFlask]);

            foreach (RawImage lOSlice in mOSlices[liFlask])
                lOSlice.enabled = false;
        }

        UWTextLabel.Hide(mOLines);
        UWTextLabel.Hide(mOTarget);
        mOPointer.enabled = false;
        mOMinimapFrame.enabled = false;
        mOMinimap.enabled = false;
        mOMinimapArrow.enabled = false;
        Bar.Hide();
    }

    /// <summary>The bags' left edge on the screen.</summary>
    public float BagsLeftEdge
    {
        get
        {
            float lfScale = UWModernUi.PixelScale;

            return Screen.width - (Margin * lfScale) - (CellColumns * (CellSize + CellGap) * lfScale);
        }
    }

    private void fShowMinimap(UWPlayerFile pOFile, UWGameScreenUw2 pOScreen, float pfScale)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        Camera lOCamera = UWViewpoint.Current;
        UWLevel lOLevel = lOLoader != null ? lOLoader.CurrentLevel : null;
        UWInventoryModel lOInventory = pOScreen != null ? pOScreen.Inventory : null;
        bool lbShown = lOLevel != null && lOLevel.AutomapTiles != null && lOCamera != null && lOInventory != null
            && UWAutomapStateUw2.IsOn(pOFile) && lOInventory.FindCarried(lOItem => lOItem.ID == UWClassicLayoutUw2.Inventory.MapObject) != null;

        mOMinimapFrame.enabled = lbShown;
        mOMinimap.enabled = lbShown;
        mOMinimapArrow.enabled = lbShown;

        if (!lbShown)
            return;

        // A new level at once: the window already follows the new position.
        if (mOMapTexture == null || Time.unscaledTime >= mfNextMap || lOLevel != mOMapLevel)
        {
            mOMapLevel = lOLevel;
            mfNextMap = Time.unscaledTime + MinimapRefreshSeconds;
            fPaintMap(lOLevel, lOLoader.CurrentLevelIndex);
        }

        if (mOMapTexture == null)
            return;

        float lfSize = MinimapSize * pfScale;
        float lfMargin = Margin * pfScale;
        Vector2 lOAt = new Vector2(Screen.width - lfMargin - lfSize, Screen.height - lfMargin - lfSize);

        mOMinimapFrame.rectTransform.sizeDelta = new Vector2(lfSize + (2f * pfScale), lfSize + (2f * pfScale));
        mOMinimapFrame.rectTransform.anchoredPosition = lOAt - new Vector2(pfScale, pfScale);
        mOMinimap.rectTransform.sizeDelta = new Vector2(lfSize, lfSize);
        mOMinimap.rectTransform.anchoredPosition = lOAt;

        Vector3 lOWhere = lOCamera.transform.position;
        float lfX = (UWUnits.WorldAxisToOriginal(lOWhere.x) / UnitsATile * MapPixelsATile) + MapCellLeft;
        float lfY = (UWUnits.WorldAxisToOriginal(lOWhere.z) / UnitsATile * MapPixelsATile) + MapCellBottom;
        float lfWidth = mOMapTexture.width;
        float lfHeight = mOMapTexture.height;

        mOMinimap.texture = mOMapTexture;
        mOMinimap.uvRect = new Rect((lfX - (MinimapWindow * 0.5f)) / lfWidth, (lfY - (MinimapWindow * 0.5f)) / lfHeight, MinimapWindow / lfWidth, MinimapWindow / lfHeight);

        float lfArrow = MinimapArrow * pfScale;

        mOMinimapArrow.rectTransform.sizeDelta = new Vector2(lfArrow, lfArrow);
        mOMinimapArrow.rectTransform.anchoredPosition = lOAt + new Vector2(lfSize * 0.5f, lfSize * 0.5f);
        mOMinimapArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -lOCamera.transform.eulerAngles.y);
    }

    /// <summary>ShowAutoMapLevel's picture: BLNKMAP's indices with the seen tiles painted, in the map's palette,
    /// rows from the top turned for the upload.</summary>
    private void fPaintMap(UWLevel pOLevel, int piLevelIndex)
    {
        UWTexture lOBlank = mOData.Textures != null ? mOData.Textures.GetTextureByType(UWTexture.TextureTypes.BLNKMAP, 0) : null;

        if (lOBlank == null || lOBlank.PaletteIndices == null)
            return;

        byte[] lyScreen = (byte[])lOBlank.PaletteIndices.Clone();
        int liWidth = UWAutomapPainter.ScreenWidth;
        int liHeight = UWAutomapPainter.ScreenHeight;

        UWAutomapPainter.Paint(lyScreen, pOLevel.AutomapTiles, (piX, piY, peUse) => UWAutomapView.Random(msSeed, piLevelIndex, piX, piY, peUse), UWGame.Uw2);

        if (mOMapColours == null)
        {
            int liPalette = UWScreensUw2.PaletteOf(UWScreensUw2.ScreenEnum.Automap);

            mOMapColours = new Color32[256];

            for (int liAt = 0; liAt < mOMapColours.Length; liAt++)
                mOMapColours[liAt] = UWColourVision.Apply(UWScreenUi.GetColour(mOData.Palettes, liPalette, liAt));

            mOMapPixels = new Color32[liWidth * liHeight];
            mOMapTexture = new Texture2D(liWidth, liHeight, TextureFormat.RGBA32, false);
            mOMapTexture.name = "UW2 minimap";
            mOMapTexture.filterMode = FilterMode.Point;
            mOMapTexture.wrapMode = TextureWrapMode.Clamp;
        }

        for (int liRow = 0; liRow < liHeight; liRow++)
        {
            for (int liColumn = 0; liColumn < liWidth; liColumn++)
                mOMapPixels[((liHeight - 1 - liRow) * liWidth) + liColumn] = mOMapColours[lyScreen[(liRow * liWidth) + liColumn]];
        }

        mOMapTexture.SetPixels32(mOMapPixels);
        mOMapTexture.Apply();
    }

    private void fShowBags(UWGameScreenUw2 pOScreen, bool pbOpen, float pfScale)
    {
        float lfCell = CellSize * pfScale;
        float lfStep = (CellSize + CellGap) * pfScale;
        float lfLeft = Screen.width - (Margin * pfScale) - (CellColumns * lfStep);
        UWFont lOSmall = mOData.Fonts != null ? mOData.Fonts.Get(UWFonts.FontType.Small) : null;

        mOCellRects.Clear();

        for (int liAt = 0; liAt < mODisplays.Count; liAt++)
        {
            bool lbShown = liAt < PackCells || pbOpen;
            int liRow = liAt < PackCells ? 1 - (liAt / CellColumns) : 2 + ((liAt - PackCells) / CellColumns);
            int liColumn = (liAt < PackCells ? liAt : liAt - PackCells) % CellColumns;
            Rect lORect = new Rect(lfLeft + (liColumn * lfStep), (Margin * pfScale) + (liRow * lfStep), lfCell, lfCell);

            mOCellRects.Add(lORect);
            mOCells[liAt].enabled = lbShown;
            mOCells[liAt].color = pbOpen ? CellOpen : CellShut;
            mOCells[liAt].rectTransform.sizeDelta = lORect.size;
            mOCells[liAt].rectTransform.anchoredPosition = lORect.position;

            UWObject lOItem = lbShown && pOScreen != null ? pOScreen.DisplayItem(mODisplays[liAt]) : null;
            Texture2D lOPicture = lOItem != null ? fItemPicture(lOItem.ID) : null;

            if (lOPicture != null)
                fPlace(mOCellPictures[liAt], lOPicture, lORect.x + ((lfCell - (lOPicture.width * UWModernHudArt.PixelAspectX * pfScale)) * 0.5f),
                    lORect.y + ((lfCell - (lOPicture.height * pfScale)) * 0.5f), pfScale);
            else
                mOCellPictures[liAt].enabled = false;

            int liCount = lOItem != null ? UWStackRules.Count(lOItem) : 0;

            if (lOSmall != null && liCount > 1)
            {
                string lsCount = liCount.ToString();

                UWTextLabel.Get(mOCellCounts[liAt]).SetLine(lOSmall, lsCount, UWTextRaster.TightWidth(lOSmall, lsCount), fInk, false, FilterMode.Point);
                mOCellCounts[liAt].rectTransform.localScale = new Vector3(pfScale, pfScale, 1f);
                mOCellCounts[liAt].rectTransform.anchoredPosition = new Vector2(lORect.x + pfScale, lORect.y + pfScale);
            }
            else
                UWTextLabel.Hide(mOCellCounts[liAt]);
        }
    }

    private Texture2D fItemPicture(int piId)
    {
        if (!mOItemPictures.TryGetValue(piId, out Texture2D lOPicture))
        {
            lOPicture = UWScreenUi.BuildObjectPicture(mOData.Textures, piId);
            mOItemPictures[piId] = lOPicture;
        }

        return lOPicture;
    }

    private Texture2D fFlaskPicture(int piIndex)
    {
        if (!mOPictures.TryGetValue(piIndex, out Texture2D lOPicture))
        {
            lOPicture = UWScreenUi.BuildPicture(mOData.Textures, UWTexture.TextureTypes.FLASKS, piIndex);
            mOPictures[piIndex] = lOPicture;
        }

        return lOPicture;
    }

    /// <summary>The widest value a flask shows: its place is as wide, so that two values stand apart whatever
    /// they read (per user 2026-10-31: they ran together).</summary>
    private const string WidestValue = "999/999";

    /// <summary>A flask in its place from pfPlace on the screen; the answer is the place's right edge.</summary>
    private float fShowFlask(int piFlask, UWPlayerFile pOFile, float pfPlace, float pfScale)
    {
        Texture2D lOEmpty = fFlaskPicture(UWClassicLayoutUw2.Flasks.Empty);

        if (lOEmpty == null)
            return pfPlace;

        UWFont lOFont = mOData.Fonts != null ? mOData.Fonts.Get(UWFonts.FontType.Normal) : null;
        float lfFlask = lOEmpty.width * UWModernHudArt.PixelAspectX;
        float lfPlace = lOFont != null ? Mathf.Max(lfFlask, UWTextRaster.TightWidth(lOFont, WidestValue)) : lfFlask;
        float lfLeft = pfPlace + ((lfPlace - lfFlask) * pfScale * 0.5f);
        int liValue = pOFile == null ? 0 : piFlask == 0 ? UWPlayerRecordUw2.Vitality(pOFile) : UWPlayerRecordUw2.Mana(pOFile);
        int liMaximum = pOFile == null ? 0 : piFlask == 0 ? UWPlayerRecordUw2.MaxVitality(pOFile) : UWPlayerRecordUw2.MaxMana(pOFile);
        int liFirst = piFlask != 0 ? UWClassicLayoutUw2.Flasks.ManaFirst
            : pOFile != null && UWPlayerRecordUw2.IsPoisoned(pOFile) ? UWClassicLayoutUw2.Flasks.PoisonFirst : UWClassicLayoutUw2.Flasks.HealthFirst;
        int liLevel = liMaximum > 0 ? Mathf.Min(UWClassicLayoutUw2.Flasks.Levels, liValue * UWClassicLayoutUw2.Flasks.Levels / liMaximum) : 0;
        float lfBottom = Margin * pfScale;

        fPlace(mOEmpty[piFlask], lOEmpty, lfLeft, lfBottom, pfScale);

        for (int liSlice = 0; liSlice < UWClassicLayoutUw2.Flasks.Levels; liSlice++)
        {
            Texture2D lOSlice = liSlice < liLevel ? fFlaskPicture(liFirst + liSlice) : null;

            if (lOSlice == null)
            {
                mOSlices[piFlask][liSlice].enabled = false;

                continue;
            }

            // How far the slice's top lies under the flask's, negative: liquid_y counts up from the bottom.
            int liRowsUp = UWClassicLayoutUw2.Flasks.Top - (UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Flasks.LiquidY[liSlice + 1]);

            fPlace(mOSlices[piFlask][liSlice], lOSlice, lfLeft, lfBottom + ((lOEmpty.height + liRowsUp - lOSlice.height) * pfScale), pfScale);
        }

        if (lOFont != null)
        {
            string lsValue = liValue + "/" + liMaximum;
            int liWidth = UWTextRaster.TightWidth(lOFont, lsValue);

            UWTextLabel.Get(mOValues[piFlask]).SetLine(lOFont, lsValue, liWidth, fInk, false, FilterMode.Point);
            mOValues[piFlask].rectTransform.localScale = new Vector3(pfScale, pfScale, 1f);
            mOValues[piFlask].rectTransform.anchoredPosition = new Vector2(lfLeft + ((lfFlask - liWidth) * pfScale * 0.5f),
                lfBottom + ((lOEmpty.height + 1) * pfScale));
        }

        return pfPlace + (lfPlace * pfScale);
    }

    private static void fPlace(RawImage pOImage, Texture2D pOPicture, float pfLeft, float pfBottom, float pfScale)
    {
        pOImage.texture = pOPicture;
        pOImage.enabled = true;
        pOImage.rectTransform.sizeDelta = new Vector2(pOPicture.width * UWModernHudArt.PixelAspectX * pfScale, pOPicture.height * pfScale);
        pOImage.rectTransform.anchoredPosition = new Vector2(pfLeft, pfBottom);
    }

    private void fShowLines(List<string> pOLines, float pfSaidAgo, float pfLeft, float pfScale)
    {
        UWFont lOFont = mOData.Fonts != null ? mOData.Fonts.Get(UWFonts.FontType.Normal) : null;

        if (lOFont == null || pOLines.Count == 0 || pfSaidAgo >= LineSeconds)
        {
            UWTextLabel.Hide(mOLines);

            return;
        }

        mOText.Clear();

        // They end where the action bar begins.
        int liWidth = Mathf.Clamp(Mathf.FloorToInt((UWModernBarUw2.LeftEdge(pfScale) - pfLeft) / pfScale) - (int)Margin, LeastLineWidth, LineWidth);

        for (int liAt = Mathf.Max(0, pOLines.Count - ShownLines); liAt < pOLines.Count; liAt++)
            mOText.AddRange(UWTextLayout.WrapText(lOFont, pOLines[liAt], liWidth));

        Color32[] lyColours = new Color32[mOText.Count];

        for (int liAt = 0; liAt < lyColours.Length; liAt++)
            lyColours[liAt] = fInk;

        UWTextLabel.Get(mOLines).SetText(lOFont, mOText, liWidth, lyColours, UWTextRaster.ColoursEnum.PerLine, false, FilterMode.Point);
        mOLines.rectTransform.localScale = new Vector3(pfScale, pfScale, 1f);
        mOLines.rectTransform.anchoredPosition = new Vector2(pfLeft, Margin * pfScale);
    }

    private void fShowPointer(float pfScale)
    {
        if (mOPointer.texture != null)
            mOPointer.rectTransform.sizeDelta = new Vector2(mOPointer.texture.width * UWModernHudArt.PixelAspectX * pfScale, mOPointer.texture.height * pfScale);

        UWFont lOFont = mOData.Fonts != null ? mOData.Fonts.Get(UWFonts.FontType.Normal) : null;

        if (Time.unscaledTime >= mfNextTarget)
        {
            mfNextTarget = Time.unscaledTime + TargetSeconds;

            UWEntityInfo lOThing = UWWalkUw2View.Current != null ? UWWalkUw2View.Current.PointedEntity() : null;

            msTarget = lOThing != null && lOThing.ObjectData != null ? UWText.Capitalised(UWText.ObjectName(mOData, lOThing.ObjectData.ID)) : string.Empty;
        }

        if (lOFont == null || string.IsNullOrEmpty(msTarget))
        {
            UWTextLabel.Hide(mOTarget);

            return;
        }

        int liWidth = UWTextRaster.TightWidth(lOFont, msTarget);

        UWTextLabel.Get(mOTarget).SetLine(lOFont, msTarget, liWidth, fInk, false, FilterMode.Point);
        mOTarget.rectTransform.localScale = new Vector3(pfScale, pfScale, 1f);
        mOTarget.rectTransform.anchoredPosition = new Vector2(-liWidth * pfScale * 0.5f, -TargetBelow * pfScale);
    }

    private RawImage fImage(string psName, Vector2 pOAnchor, Vector2 pOPivot)
    {
        RawImage lOImage = UWUiKit.CreateRawImage(mOCanvas.transform, psName, pOAnchor, pOPivot);

        lOImage.raycastTarget = false;

        return lOImage;
    }

    private void fEnsureBuilt()
    {
        if (mOCanvas != null)
            return;

        mOCanvas = UWUiKit.CreateOverlayCanvas(transform, "UW2 Modern HUD", UWUiSorting.ModernHud);

        for (int liFlask = 0; liFlask < 2; liFlask++)
        {
            mOEmpty[liFlask] = fImage("Flask " + liFlask, UWUiKit.BottomLeft, UWUiKit.BottomLeft);
            UWPixelArtUI.Apply(mOEmpty[liFlask]);
            mOSlices[liFlask] = new RawImage[UWClassicLayoutUw2.Flasks.Levels];

            for (int liSlice = 0; liSlice < UWClassicLayoutUw2.Flasks.Levels; liSlice++)
            {
                mOSlices[liFlask][liSlice] = fImage("Flask " + liFlask + " slice " + liSlice, UWUiKit.BottomLeft, UWUiKit.BottomLeft);
                UWPixelArtUI.Apply(mOSlices[liFlask][liSlice]);
            }

            mOValues[liFlask] = fImage("Value " + liFlask, UWUiKit.BottomLeft, UWUiKit.BottomLeft);
        }

        mOLines = fImage("Lines", UWUiKit.BottomLeft, UWUiKit.BottomLeft);

        mOMinimapFrame = UWUiKit.CreateImage(mOCanvas.transform, "Minimap frame", UWUiKit.BottomLeft, UWUiKit.BottomLeft, CellOpen);
        mOMinimapFrame.raycastTarget = false;
        mOMinimap = fImage("Minimap", UWUiKit.BottomLeft, UWUiKit.BottomLeft);
        mOMinimapArrow = fImage("Character", UWUiKit.BottomLeft, new Vector2(0.5f, 0.5f));
        mOMinimapArrow.texture = UWModernMinimap.BuildArrow(64);

        for (int liDisplay = 1; liDisplay < UWClassicLayoutUw2.Inventory.DisplayToSlot.Length && liDisplay < UWClassicLayoutUw2.Inventory.OpenBagDisplay; liDisplay++)
        {
            if (UWClassicLayoutUw2.Inventory.DisplayToSlot[liDisplay] >= UWInventoryWorkspace.EquipmentSlotCount)
                mODisplays.Add(liDisplay);
        }

        for (int liDisplay = 1; liDisplay < UWClassicLayoutUw2.Inventory.DisplayToSlot.Length && liDisplay < UWClassicLayoutUw2.Inventory.OpenBagDisplay; liDisplay++)
        {
            if (UWClassicLayoutUw2.Inventory.DisplayToSlot[liDisplay] < UWInventoryWorkspace.EquipmentSlotCount)
                mODisplays.Add(liDisplay);
        }

        mODisplays.Add(UWClassicLayoutUw2.Inventory.OpenBagDisplay);

        for (int liAt = 0; liAt < mODisplays.Count; liAt++)
        {
            Image lOCell = UWUiKit.CreateImage(mOCanvas.transform, "Cell " + liAt, UWUiKit.BottomLeft, UWUiKit.BottomLeft, CellShut);

            lOCell.raycastTarget = false;
            mOCells.Add(lOCell);

            RawImage lOPicture = fImage("Thing " + liAt, UWUiKit.BottomLeft, UWUiKit.BottomLeft);

            UWPixelArtUI.Apply(lOPicture);
            lOPicture.enabled = false;
            mOCellPictures.Add(lOPicture);
            mOCellCounts.Add(fImage("Count " + liAt, UWUiKit.BottomLeft, UWUiKit.BottomLeft));
        }

        Bar = new UWModernBarUw2(mOCanvas);

        Vector2 lOMiddle = new Vector2(0.5f, 0.5f);

        mOPointer = fImage("Pointer", lOMiddle, lOMiddle);
        mOPointer.texture = UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.CURSORS, (int)UWCursors.CursorEnum.Center, FilterMode.Point, true);
        mOPointer.enabled = false;
        UWPixelArtUI.Apply(mOPointer);
        mOTarget = fImage("Target", lOMiddle, new Vector2(0f, 1f));
    }
}
