using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The HUD section "the full-screen automap with tiles, borders, doors, notes, paging and erasing".
/// Its own class since 2026-09-18 (stage two of the HUD rebuild); the owner hands in the game
/// data, the frame, the character, the interaction and the inventory through mOUi. UWGameUI
/// keeps the public entry points as forwards.
/// </summary>
public sealed class UWHudMap
{
    private readonly UWGameUI mOUi;

    internal UWHudMap(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    // ------------------------------------------------- Full-screen map

    /// <summary>
    /// The map on the parchment, BLNKMAP.BYT.
    ///
    /// Text, compass rose, ERASE and CLOSE are already contained in the background picture - only
    /// the map itself is drawn.
    ///
    /// Tile size and origin: three pixels a tile, the cell of tile x/y at (3x + 7, 3y + 4)
    /// from the bottom - estimated from a screenshot at first, READ 2026-09-26 in ovr092_48B.
    /// </summary>
    private const int MapTileSize = 3;

    private const float MapLeft = 7f;

    private const float MapTop = 4f;

    private const int MapTilesPerAxis = 64;

    /// <summary>
    /// ROLLED ONCE (per user, 2026-09-26): the original rolls each pixel's small colour step anew
    /// on every drawing, but the help window's map tab redraws the tiles every half second, and a
    /// fresh roll each time made the parchment shimmer. So each random value is a hash of the
    /// pixel, the level, what it is drawn for and a seed taken once per run: random-looking, but
    /// the same on every redraw and every opening of the map. The drawing itself is
    /// UWAutomapPainter's, after UW.EXE (since 2026-09-26).
    /// </summary>
    private static readonly uint msMapSeed = unchecked((uint)System.Environment.TickCount * 2654435761u);

    private static int fMapRandom(int piLevelIndex, int piX, int piY, UWAutomapPainter.RandomUse peUse)
    {
        unchecked
        {
            uint luHash = msMapSeed ^ ((uint)(piLevelIndex + 1) * 83492791u) ^ ((uint)piX * 73856093u)
                ^ ((uint)piY * 19349663u) ^ ((uint)((int)peUse + 1) * 2246822519u);

            luHash ^= luHash >> 13;
            luHash *= 0x5BD1E995u;
            luHash ^= luHash >> 15;

            return (int)(luHash & 0x7FFF);
        }
    }

    internal RawImage mMapBackgroundImage;

    private RawImage mMapTilesImage;

    /// <summary>The level whose map should be shown - the one the character stands on.
    /// </summary>
    public int CurrentMapLevelIndex
    {
        // Look up the loader node first: previously it was not yet found on the first opening of the map,
        // and the map always showed level 1 (per user, 2026-09-13).
        get { return fEnsureLevelLoader() ? mOLevelLoader.CurrentLevelIndex : 0; }
    }

    public bool IsMapVisible
    {
        get { return mMapBackgroundImage != null && mMapBackgroundImage.gameObject.activeSelf; }
    }

    /// <summary>Shows the map of the given level. Returns false if the parchment or
    /// the map data are missing.</summary>
    public bool ShowMap(int piLevelIndex)
    {
        miMapLevel = piLevelIndex;

        fBuildMapScreen();

        if (mMapBackgroundImage == null)
            return false;

        if (!fBuildMapTiles(piLevelIndex))
            return false;

        fBuildMapCloseDebug();
        fRefreshMapLevelNumber(piLevelIndex);
        fRefreshMapNotes(miMapLevel);

        mMapBackgroundImage.gameObject.SetActive(true);
        mMapBackgroundImage.transform.SetAsLastSibling();

        fSetMapModal(true);

        // With the map "Maps & Legends" plays (reference: map.cs).
        UWMusic.ChangeTheme(UWMusic.MapsAndLegendsTheme);

        // NO LEVEL LINE IN THE SCROLL (per user on the original, 2026-09-29; read the same day:
        // UseReadable_seg040_352B_19A8 -> ShowMapAndOtherFullScreenUIs_ovr109_2FF ->
        // OpenAutomap_ovr092_0 write nothing to the scroll - "You are on the ... level" comes only
        // from the compass, PrintCompassStatusMessage_seg024_32). Ours wrote it since 2026-09-07,
        // on a report that probably meant the compass. The level number stays on the map itself.
        return true;
    }

    public void HideMap()
    {
        // NOTHING is written here any more. Until 2026-09-07 notes and automap went
        // into the save game on closing the map - a stopgap while there was no
        // saving of our own. The original writes them only when saving the
        // save game, and that is exactly where they are now in our version too (see
        // UWSavegameWriter).

        if (mMapBackgroundImage != null)
            mMapBackgroundImage.gameObject.SetActive(false);

        // Back to the level - or to Armed if the weapon is drawn (reference:
        // uimanager_map).
        UWMusic.ResumeAfterInterruption(mOUi.mOInteraction != null && mOUi.mOInteraction.IsCombatModeActive);

        fEndMapNote(true);

        mbMapEraseMode = false;

        fSetMapModal(false);
    }

    /// <summary>Reports the open map as a modal window - then the character stands still
    /// and view as well as interaction pause.</summary>
    private void fSetMapModal(bool pbOpen)
    {
        if (mOUi.mOControlScheme == null)
            mOUi.mOControlScheme = UWScene.ControlScheme;

        if (mOUi.mOControlScheme != null)
            mOUi.mOControlScheme.SetUiModal("map", pbOpen);
    }

    /// <summary>
    /// The marker on the own tile.
    ///
    /// A small gold cross with a dark shadow and two grey pixels below it (a small dagger),
    /// drawn by hand after the original (per user, 2026-09-03). Which graphic the original
    /// uses for it is still open - the reference hooks in a hand-assigned picture there that
    /// cannot be derived from the game data.
    /// </summary>
    private void fDrawPlayerMark(Color32[] pOPixels, int piSize, int piLevelIndex)
    {
        if (mOLevelLoader == null || mOUi.mCharacter == null)
            return;

        // Only on the level the character really stands on.
        if (piLevelIndex != mOLevelLoader.CurrentLevelIndex)
            return;

        UWTilePos lOTile = mOLevelLoader.WorldPositionToTile(mOUi.mCharacter.transform.position);

        if (fDrawOriginalPlayerMark(pOPixels, piSize, lOTile))
            return;

        // Gold with a dark shadow offset by one pixel - that is how it looks in the original
        // (per user with cutout, 2026-09-03).
        Color32 lOMark = new Color32(230, 180, 60, 255);
        Color32 lOShadow = new Color32(40, 25, 15, 255);

        // Below the golden part sit two grey pixels, light and a bit darker - which gives the
        // impression of a small dagger (per user, 2026-09-03).
        Color32 lOLightGrey = new Color32(200, 200, 205, 255);
        Color32 lODarkGrey = new Color32(140, 140, 145, 255);

        int liLeft = lOTile.X * MapTileSize;
        int liMiddle = MapTileSize / 2;

        // The two grey pixels attach at the bottom - so that the marker still sits on its
        // tile, the golden part and its shadow move up by two pixels
        // (per user, 2026-09-03).
        int liBottom = (lOTile.Y * MapTileSize) + 2;

        // The shadow lies offset one pixel to the right and down and reaches as far
        // as the marker itself, so down below the grey pixels.
        for (int liAt = 0; liAt < MapTileSize; liAt++)
        {
            fSetPixel(pOPixels, piSize, liLeft + liAt + 1, liBottom + liMiddle - 1, lOShadow);
            fSetPixel(pOPixels, piSize, liLeft + liMiddle + 1, liBottom + liAt - 1, lOShadow);
        }

        fSetPixel(pOPixels, piSize, liLeft + liMiddle + 1, liBottom - 2, lOShadow);
        fSetPixel(pOPixels, piSize, liLeft + liMiddle + 1, liBottom - 3, lOShadow);

        for (int liAt = 0; liAt < MapTileSize; liAt++)
        {
            fSetPixel(pOPixels, piSize, liLeft + liAt, liBottom + liMiddle, lOMark);
            fSetPixel(pOPixels, piSize, liLeft + liMiddle, liBottom + liAt, lOMark);
        }

        fSetPixel(pOPixels, piSize, liLeft + liMiddle, liBottom - 1, lOLightGrey);
        fSetPixel(pOPixels, piSize, liLeft + liMiddle, liBottom - 2, lODarkGrey);
    }

    /// <summary>
    /// THE ORIGINAL'S OWN PICTURE (read 2026-09-30, per user: "the shadow of the cross is not
    /// right yet"): ovr092_F66 draws the mark through seg009_265 with graphic 0x103F, which is
    /// BUTTONS.GR image 0x3F (0x1000 + n counts from the file after ANIMO.GR, loaded at
    /// ovr115_8B3). It is 8 by 5: a small gold dagger in the first three columns - point
    /// at the top, a guard of three shades of gold, grey blade below - and its dark blue
    /// shadow falling to the right. Index 0 is clear. The dagger's column sits on the tile's
    /// middle column and its lowest pixel on the tile's lowest row, where the hand-drawn mark
    /// had them (per user, 2026-09-03); the picture replaces its colours and shadow.
    /// </summary>
    private bool fDrawOriginalPlayerMark(Color32[] pOPixels, int piSize, UWTilePos pOTile)
    {
        UWTexture lOMark = mOUi.mOUWData != null && mOUi.mOUWData.Textures != null
            ? mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.BUTTONS, PlayerMarkImage)
            : null;

        if (lOMark == null || lOMark.Width <= 0 || lOMark.Height <= 0)
            return false;

        byte[] laIndices = lOMark.GetMainPaletteIndices();

        if (laIndices == null || laIndices.Length < lOMark.Width * lOMark.Height || mOUi.mOUWData.Palettes == null)
            return false;

        // IN THE MAP'S PALETTE, not the game's (per user, 2026-09-30: the shadow is grey-black in
        // the original, ours dark blue): ovr092_F66 switches to palette 1 for the whole map
        // screen, the parchment's palette, and the same indices are brown and dull gold there.
        UWPalette lOPalette = mOUi.mOUWData.Palettes.GetPalette(MapPaletteIndex);

        if (lOPalette == null)
            return false;

        int liLeft = (pOTile.X * MapTileSize) + (MapTileSize / 2) - PlayerMarkDaggerColumn;
        int liBottom = pOTile.Y * MapTileSize;

        for (int liRow = 0; liRow < lOMark.Height; liRow++)
        {
            for (int liColumn = 0; liColumn < lOMark.Width; liColumn++)
            {
                int liAt = (liRow * lOMark.Width) + liColumn;

                if (laIndices[liAt] == 0)
                    continue;

                byte[] laRgb = lOPalette.GetRGB(laIndices[liAt]);

                // The picture's first row is its top; the map texture counts from the bottom.
                fSetPixel(pOPixels, piSize, liLeft + liColumn, liBottom + (lOMark.Height - 1 - liRow),
                    new Color32(laRgb[0], laRgb[1], laRgb[2], 255));
            }
        }

        return true;
    }

    /// <summary>The palette of the map screen (ovr092_F66, GetPalette(1)).</summary>
    private const int MapPaletteIndex = 1;

    /// <summary>BUTTONS.GR image of the player mark - see fDrawOriginalPlayerMark.</summary>
    private const int PlayerMarkImage = 0x3F;

    /// <summary>The picture's column that holds the dagger.</summary>
    private const int PlayerMarkDaggerColumn = 1;

    /// <summary>
    /// The level number next to the baked-in word "Level".
    ///
    /// The word itself is in the background picture, only the number after it is drawn - and
    /// CENTRED, so that a two-digit number does not wander to the right (per user,
    /// 2026-09-03).
    /// </summary>
    private const float MapLevelNumberCentre = 289f;

    private const float MapLevelNumberTop = 5f;

    private RawImage mMapLevelImage;

    private int miMapLevelShown = -1;

    private void fRefreshMapLevelNumber(int piLevelIndex)
    {
        if (mMapBackgroundImage == null || mOUi.mOUWData == null || piLevelIndex == miMapLevelShown)
            return;

        UWFont lOFont = mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Big);

        if (lOFont == null)
            return;

        miMapLevelShown = piLevelIndex;

        string lsText = (piLevelIndex + 1).ToString();

        int liWidth = Mathf.Max(1,
            lOFont.MeasureText(lsText, 0, UWFontRenderer.CharacterSpacing)
            - UWFontRenderer.CharacterSpacing);

        Texture2D lOTexture = UWFontRenderer.RenderLines(lOFont,
            new System.Collections.Generic.List<string> { lsText },
            liWidth, new Color32(60, 40, 25, 255), mOUi.TextureFilterMode, false);

        if (lOTexture == null)
            return;

        if (mMapLevelImage == null)
        {
            GameObject lOObject = new GameObject("MapLevelNumber", typeof(RectTransform), typeof(RawImage));
            lOObject.transform.SetParent(mMapBackgroundImage.transform, false);

            RectTransform lORect = (RectTransform)lOObject.transform;
            lORect.anchorMin = new Vector2(0f, 1f);
            lORect.anchorMax = new Vector2(0f, 1f);
            lORect.pivot = new Vector2(0.5f, 1f);
            lORect.anchoredPosition = new Vector2(MapLevelNumberCentre, -MapLevelNumberTop);

            mMapLevelImage = lOObject.GetComponent<RawImage>();
            mMapLevelImage.raycastTarget = false;
        }

        UWTextLabel.Get(mMapLevelImage).Set(lOFont, lOTexture,
            new System.Collections.Generic.List<string> { lsText },
            new System.Collections.Generic.List<Color32> { new Color32(60, 40, 25, 255) }, false);
    }

    /// <summary>
    /// The labels on the map.
    ///
    /// Their position is stored as a SCREEN pixel in the save game, not as a tile - and Y counts from
    /// the bottom. Cross-checked against the user's save game: 104/162 on level 4 stands in the original
    /// at top centre (2026-09-03).
    ///
    /// Written with font4x5p, the small font - in the original the letters are
    /// four pixels tall (per user, 2026-09-03). At first font5x6p was here and thus
    /// two pixels too big.
    /// </summary>
    private System.Collections.Generic.List<RawImage> mOMapNoteImages;

    private void fRefreshMapNotes(int piLevelIndex)
    {
        if (mMapBackgroundImage == null || mOUi.mOUWData == null)
            return;

        if (mOMapNoteImages == null)
            mOMapNoteImages = new System.Collections.Generic.List<RawImage>();

        foreach (RawImage lOOld in mOMapNoteImages)
            UWTextLabel.Hide(lOOld);

        System.Collections.Generic.List<UWLevel.MapNote> lONotes =
            mOUi.mOUWData.GetMapNotes(piLevelIndex);

        if (lONotes == null)
            return;

        UWFont lOFont = mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Small);

        if (lOFont == null)
            return;

        for (int liNote = 0; liNote < lONotes.Count; liNote++)
        {
            UWLevel.MapNote lONote = lONotes[liNote];

            if (string.IsNullOrEmpty(lONote.Text))
            {
                // A still empty line shows only the quill.
                if (mbMapWriting && liNote == miMapWriteNote)
                    fRefreshMapWriteQuill(lONote.X, UWHudPanel.FrameHeight - lONote.Y, lOFont.Height);

                continue;
            }

            while (mOMapNoteImages.Count <= liNote)
                mOMapNoteImages.Add(null);

            if (mOMapNoteImages[liNote] == null)
            {
                GameObject lOObject = new GameObject("MapNote" + liNote,
                    typeof(RectTransform), typeof(RawImage));
                lOObject.transform.SetParent(mMapBackgroundImage.transform, false);

                RectTransform lORect = (RectTransform)lOObject.transform;
                lORect.anchorMin = new Vector2(0f, 1f);
                lORect.anchorMax = new Vector2(0f, 1f);
                lORect.pivot = new Vector2(0f, 1f);

                mOMapNoteImages[liNote] = lOObject.GetComponent<RawImage>();
                mOMapNoteImages[liNote].raycastTarget = false;
            }

            int liWidth = Mathf.Max(1,
                lOFont.MeasureText(lONote.Text, 0, UWFontRenderer.CharacterSpacing)
                - UWFontRenderer.CharacterSpacing);

            Texture2D lOTexture = UWFontRenderer.RenderLines(lOFont,
                new System.Collections.Generic.List<string> { lONote.Text },
                liWidth, new Color32(60, 40, 25, 255), mOUi.TextureFilterMode, false);

            if (lOTexture == null)
                continue;

            RawImage lOImage = mOMapNoteImages[liNote];

            // Release the previous texture of the note - otherwise one stays behind on every redraw
            // (leak, 2026-09-13).
            if (lOImage.texture != null && lOImage.texture != lOTexture)
                Object.Destroy(lOImage.texture);

            UWTextLabel.Get(lOImage).Set(lOFont, lOTexture,
                new System.Collections.Generic.List<string> { lONote.Text },
                new System.Collections.Generic.List<Color32> { new Color32(60, 40, 25, 255) }, false);

            RectTransform lONoteRect = (RectTransform)lOImage.transform;

            lONoteRect.anchoredPosition = new Vector2(
                lONote.X, -(UWHudPanel.FrameHeight - lONote.Y));

            // While writing, the quill stands right behind the text.
            if (mbMapWriting && liNote == miMapWriteNote)
            {
                fRefreshMapWriteQuill(lONote.X + lOTexture.width,
                    UWHudPanel.FrameHeight - lONote.Y, lOTexture.height);
            }
        }
    }

    private void fBuildMapScreen()
    {
        if (mMapBackgroundImage != null || mOUi.mGameFrame == null || mOUi.mOUWData == null)
            return;

        UWTexture lOSource;

        if (!mOUi.mOUWData.Textures.BitmapFiles.TryGetValue(UWTexture.TextureTypes.BLNKMAP, out lOSource)
            || lOSource == null)
            return;

        GameObject lOObject = new GameObject("MapScreen", typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(mOUi.mGameFrame, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = Vector2.zero;
        lORect.sizeDelta = new Vector2(UWHudPanel.FrameWidth, UWHudPanel.FrameHeight);

        Texture2D lOTexture = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.RGBA32, false);
        lOTexture.name = "UWGameUI.cs:4476";
        lOTexture.filterMode = mOUi.TextureFilterMode;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(UWGameUI.fGetTextureInvert(lOSource));
        lOTexture.Apply(false, false);

        mMapBackgroundImage = lOObject.GetComponent<RawImage>();
        mMapBackgroundImage.texture = lOTexture;
        mMapBackgroundImage.raycastTarget = false;

        GameObject lOTiles = new GameObject("MapTiles", typeof(RectTransform), typeof(RawImage));
        lOTiles.transform.SetParent(lOObject.transform, false);

        RectTransform lOTileRect = (RectTransform)lOTiles.transform;
        lOTileRect.anchorMin = new Vector2(0f, 1f);
        lOTileRect.anchorMax = new Vector2(0f, 1f);
        lOTileRect.pivot = new Vector2(0f, 1f);
        lOTileRect.anchoredPosition = new Vector2(MapLeft, -MapTop);
        lOTileRect.sizeDelta = new Vector2(
            MapTilesPerAxis * MapTileSize, MapTilesPerAxis * MapTileSize);

        mMapTilesImage = lOTiles.GetComponent<RawImage>();
        mMapTilesImage.raycastTarget = false;

        lOObject.SetActive(false);
    }

    /// <summary>
    /// Draws the discovered tiles.
    ///
    /// One byte per tile: at the bottom the tile type, at the top the display type. In UW1
    /// top 1 means water, 2 lava, 4 door, 9 and 10 bridge, 12 stairs - DIFFERENT from
    /// uw-formats.txt, which has the Underworld 2 version. A tile type above 9 means
    /// undiscovered; there the parchment stays visible.
    ///
    /// The colours are taken over from the reference, which took them from the original.
    /// </summary>
    private bool fBuildMapTiles(int piLevelIndex)
    {
        if (mMapTilesImage == null)
            return false;

        if (!fEnsureLevelLoader())
            return false;

        int liSize = MapTilesPerAxis * MapTileSize;

        Color32[] lOPixels = new Color32[liSize * liSize];

        fComposeMapTiles(piLevelIndex, lOPixels, liSize);
        fApplyMapTexture(lOPixels, liSize);

        return true;
    }

    /// <summary>
    /// The discovered tiles of a level as pixels (MapTilesPerAxis * MapTileSize square, row 0
    /// at the bottom) and - on the level the character stands on - his mark. Transparent where the
    /// parchment stays as it is. Shared by the map and the help window's map tab
    /// (BuildMinimapTiles).
    ///
    /// SINCE 2026-09-26 PAINTED AS UW.EXE PAINTS IT (UWAutomapPainter, after ovr092): on the
    /// parchment's palette indices, each pixel the parchment plus a small random step, borders
    /// only outside the sides whose neighbour is not a discovered tile, a diagonal bordered on its
    /// open half's two sides only. Until then the look came from the reference's fixed colour
    /// tones plus rules tuned by eye (a floor overhang into undiscovered neighbours, lines on a
    /// diagonal's solid half towards open neighbours) - the latter made the "knob" where two
    /// diagonals meet (per user, 2026-09-26).
    /// </summary>
    private void fComposeMapTiles(int piLevelIndex, Color32[] lOPixels, int liSize)
    {
        UWLevel lOLevel = mOUi.mOUWData != null && mOUi.mOUWData.Levels != null
            && piLevelIndex >= 0 && piLevelIndex < mOUi.mOUWData.Levels.Count
            ? mOUi.mOUWData.Levels[piLevelIndex] : null;

        UWTexture lOParchment;

        // Levels 10 to 99 have no automap, only notes - the parchment then stays
        // empty, fRefreshMapNotes still draws the labels.
        if (lOLevel == null || lOLevel.AutomapTiles == null
            || !mOUi.mOUWData.Textures.BitmapFiles.TryGetValue(UWTexture.TextureTypes.BLNKMAP, out lOParchment)
            || lOParchment == null || lOParchment.PaletteIndices == null
            || lOParchment.PaletteIndices.Length < UWAutomapPainter.ScreenWidth * UWAutomapPainter.ScreenHeight)
        {
            fDrawPlayerMark(lOPixels, liSize, piLevelIndex);
            return;
        }

        byte[] lyScreen = (byte[])lOParchment.PaletteIndices.Clone();

        UWAutomapPainter.Paint(lyScreen, lOLevel.AutomapTiles,
            (piX, piY, peUse) => fMapRandom(piLevelIndex, piX, piY, peUse));

        UWPalette lOPalette = lOParchment.Palettes.GetPalette(lOParchment.MainPaletteIndex);

        for (int liY = 0; liY < liSize; liY++)
        {
            // The painter's screen is stored from the top, its y counts from the bottom.
            int liRow = (UWAutomapPainter.ScreenHeight - 1 - (liY + UWAutomapPainter.CellBottom)) * UWAutomapPainter.ScreenWidth;

            for (int liX = 0; liX < liSize; liX++)
            {
                int liAt = liRow + liX + UWAutomapPainter.CellLeft;
                byte lyIndex = lyScreen[liAt];

                if (lyIndex == lOParchment.PaletteIndices[liAt])
                    continue;

                UWColor32 lOColour = lOPalette.GetUWColor(lyIndex);

                lOPixels[(liY * liSize) + liX] = UWColourVision.Apply(new Color32(lOColour.R, lOColour.G, lOColour.B, 255));
            }
        }

        fDrawPlayerMark(lOPixels, liSize, piLevelIndex);
    }

    // ------------------------------------------------- The help window's map tab

    /// <summary>Where the tiles sit on the 320x200 map screen (top-left, screen pixels) and
    /// how large one tile is - for the help window's map tab.</summary>
    internal static Vector2 TilesTopLeft => new Vector2(MapLeft, MapTop);

    internal const int TilePixels = MapTileSize;

    internal const int TilesPerAxis = MapTilesPerAxis;

    private Texture2D mOMinimapTiles;

    private Color32[] mOMinimapPixels;

    /// <summary>The discovered tiles of the level the character stands on, freshly drawn
    /// into the help window's own texture (the map's own stays untouched).</summary>
    internal Texture2D BuildMinimapTiles()
    {
        if (!fEnsureLevelLoader())
            return null;

        int liSize = MapTilesPerAxis * MapTileSize;

        if (mOMinimapPixels == null || mOMinimapPixels.Length != liSize * liSize)
            mOMinimapPixels = new Color32[liSize * liSize];
        else
            System.Array.Clear(mOMinimapPixels, 0, mOMinimapPixels.Length);

        fComposeMapTiles(mOLevelLoader.CurrentLevelIndex, mOMinimapPixels, liSize);

        if (mOMinimapTiles == null)
        {
            mOMinimapTiles = new Texture2D(liSize, liSize, TextureFormat.RGBA32, false);
            mOMinimapTiles.name = "UWHudMap minimap tiles";
            mOMinimapTiles.filterMode = FilterMode.Point;
            mOMinimapTiles.wrapMode = TextureWrapMode.Clamp;
        }

        mOMinimapTiles.SetPixels32(mOMinimapPixels);
        mOMinimapTiles.Apply(false, false);

        return mOMinimapTiles;
    }

    private Texture2D mOMinimapParchment;

    /// <summary>The blank map (BLNKMAP.GR, 320x200) as a texture of its own, for the help
    /// window's map tab.</summary>
    internal Texture2D GetMinimapParchment()
    {
        if (mOMinimapParchment != null || mOUi.mOUWData == null)
            return mOMinimapParchment;

        UWTexture lOSource;

        if (!mOUi.mOUWData.Textures.BitmapFiles.TryGetValue(UWTexture.TextureTypes.BLNKMAP, out lOSource)
            || lOSource == null)
            return null;

        mOMinimapParchment = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.RGBA32, false);
        mOMinimapParchment.name = "UWHudMap minimap parchment";
        mOMinimapParchment.filterMode = FilterMode.Point;
        mOMinimapParchment.wrapMode = TextureWrapMode.Clamp;
        mOMinimapParchment.SetPixels32(UWGameUI.fGetTextureInvert(lOSource));
        mOMinimapParchment.Apply(false, false);

        return mOMinimapParchment;
    }

    /// <summary>One note of the map as the help window draws it: the rendered text and its
    /// top-left corner on the 320x200 map screen.</summary>
    internal struct MinimapNote
    {
        public Texture2D Texture;
        public Vector2 TopLeft;
    }

    private readonly System.Collections.Generic.List<MinimapNote> mOMinimapNotes =
        new System.Collections.Generic.List<MinimapNote>();

    private string msMinimapNotesKey;

    /// <summary>The notes of the level the character stands on, rendered as on the map (small
    /// font, dark brown). Only rebuilt when a note changed. Read-only - notes are written and
    /// erased on the real map alone (per user, 2026-09-26).</summary>
    internal System.Collections.Generic.List<MinimapNote> GetMinimapNotes()
    {
        if (!fEnsureLevelLoader() || mOUi.mOUWData == null)
            return mOMinimapNotes;

        int liLevel = mOLevelLoader.CurrentLevelIndex;
        System.Collections.Generic.List<UWLevel.MapNote> lONotes = mOUi.mOUWData.GetMapNotes(liLevel);

        System.Text.StringBuilder lOKey = new System.Text.StringBuilder().Append(liLevel);

        if (lONotes != null)
        {
            foreach (UWLevel.MapNote lONote in lONotes)
                lOKey.Append('|').Append(lONote.X).Append(',').Append(lONote.Y).Append(',').Append(lONote.Text);
        }

        string lsKey = lOKey.ToString();

        if (lsKey == msMinimapNotesKey)
            return mOMinimapNotes;

        msMinimapNotesKey = lsKey;

        foreach (MinimapNote lOOld in mOMinimapNotes)
        {
            if (lOOld.Texture != null)
                Object.Destroy(lOOld.Texture);
        }

        mOMinimapNotes.Clear();

        UWFont lOFont = mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Small);

        if (lONotes == null || lOFont == null)
            return mOMinimapNotes;

        foreach (UWLevel.MapNote lONote in lONotes)
        {
            if (string.IsNullOrEmpty(lONote.Text))
                continue;

            int liWidth = Mathf.Max(1,
                lOFont.MeasureText(lONote.Text, 0, UWFontRenderer.CharacterSpacing) - UWFontRenderer.CharacterSpacing);

            Texture2D lOTexture = UWFontRenderer.RenderLines(lOFont,
                new System.Collections.Generic.List<string> { lONote.Text },
                liWidth, new Color32(60, 40, 25, 255), FilterMode.Point, false);

            if (lOTexture == null)
                continue;

            mOMinimapNotes.Add(new MinimapNote
            {
                Texture = lOTexture,
                TopLeft = new Vector2(lONote.X, UWHudPanel.FrameHeight - lONote.Y)
            });
        }

        return mOMinimapNotes;
    }

    /// <summary>The middle of the character's tile on the 320x200 map screen (top-left
    /// origin), or false when there is no level.</summary>
    internal bool TryGetMinimapPlayerPixel(out Vector2 pOPixel)
    {
        pOPixel = Vector2.zero;

        if (!fEnsureLevelLoader() || mOUi.mCharacter == null)
            return false;

        UWTilePos lOTile = mOLevelLoader.WorldPositionToTile(mOUi.mCharacter.transform.position);

        // The tiles' rows count from the bottom (row 0 is the south edge).
        pOPixel = new Vector2(
            MapLeft + (lOTile.X * MapTileSize) + (MapTileSize / 2f),
            MapTop + ((MapTilesPerAxis - lOTile.Y) * MapTileSize) - (MapTileSize / 2f));

        return true;
    }

    private void fApplyMapTexture(Color32[] pOPixels, int piSize)
    {
        if (mMapTilesTexture == null)
        {
            mMapTilesTexture = new Texture2D(piSize, piSize, TextureFormat.RGBA32, false);
            mMapTilesTexture.name = "UWGameUI.cs:4580";
            mMapTilesTexture.filterMode = mOUi.TextureFilterMode;
            mMapTilesTexture.wrapMode = TextureWrapMode.Clamp;
        }

        mMapTilesTexture.SetPixels32(pOPixels);
        mMapTilesTexture.Apply(false, false);

        mMapTilesImage.texture = mMapTilesTexture;
    }

    private Texture2D mMapTilesTexture;

    internal UWLevelLoader mOLevelLoader;

    /// <summary>Looks up the loader node, if not done yet. It hangs on the object
    /// "Level" - the same spot DebugFunctions uses.</summary>
    internal bool fEnsureLevelLoader()
    {
        if (mOLevelLoader != null)
            return true;

        mOLevelLoader = UWScene.LevelLoader;

        return mOLevelLoader != null;
    }

    /// <summary>Sets a pixel, provided it lies on the picture. The player mark can reach
    /// beyond the tile edge and fall out at the map edge.</summary>
    private static void fSetPixel(Color32[] pOPixels, int piSize, int piX, int piY, Color32 pOColour)
    {
        if (piX < 0 || piY < 0 || piX >= piSize || piY >= piSize)
            return;

        pOPixels[(piY * piSize) + piX] = pOColour;
    }

    /// <summary>The CLOSE button at bottom right on the parchment, estimated on the background
    /// picture. A click on it closes the map.</summary>
    private static readonly Rect mOMapCloseRect = new Rect(263f, 150f, 47f, 26f);

    private Image mMapCloseDebugImage;

    private void fBuildMapCloseDebug()
    {
        if (!mOUi.mbShowMapCloseArea || mMapCloseDebugImage != null || mMapBackgroundImage == null)
            return;

        mMapCloseDebugImage = UWGameUI.fCreateImage("MapCloseArea",
            (RectTransform)mMapBackgroundImage.transform, mOMapCloseRect.x, -mOMapCloseRect.y,
            mOMapCloseRect.width, mOMapCloseRect.height,
            new Color(0f, 1f, 0f, 0.35f));
    }

    /// <summary>
    /// The currently shown level. On opening it is the own one, with the two side corners
    /// on the right it can be changed.
    /// </summary>
    private int miMapLevel;

    /// <summary>The corners at top and bottom right of the parchment, estimated on the background
    /// picture. The top one goes one level up, the bottom one down.</summary>
    private static readonly Rect mOMapPageUpRect = new Rect(298f, 2f, 22f, 18f);

    private static readonly Rect mOMapPageDownRect = new Rect(298f, 178f, 22f, 20f);

    private UWLevel fGetMapLevel()
    {
        if (mOUi.mOUWData == null || mOUi.mOUWData.Levels == null
            || miMapLevel < 0 || miMapLevel >= mOUi.mOUWData.Levels.Count)
            return null;

        return mOUi.mOUWData.Levels[miMapLevel];
    }

    /// <summary>Pages one level further if one of the two corners was hit.
    /// Returns true if the click is consumed.</summary>
    private bool fUpdateMapPaging(Vector2 pOLocal)
    {
        int liStep = 0;

        if (fIsInMapRect(mOMapPageUpRect, pOLocal))
            liStep = -1;
        else if (fIsInMapRect(mOMapPageDownRect, pOLocal))
            liStep = 1;

        if (liStep == 0)
            return false;

        // Up to level 99: that many note pages the game keeps, even if only the
        // first nine have a map.
        int liCount = DataImport.MapNoteLevelCount;

        int liNew = Mathf.Clamp(miMapLevel + liStep, 0, liCount - 1);

        if (liNew == miMapLevel)
            return true;

        miMapLevel = liNew;

        fBuildMapTiles(miMapLevel);
        fRefreshMapLevelNumber(miMapLevel);
        fRefreshMapNotes(miMapLevel);

        return true;
    }

    private static bool fIsInMapRect(Rect pORect, Vector2 pOLocal)
    {
        float lfY = -pOLocal.y;

        return pOLocal.x >= pORect.x && pOLocal.x <= pORect.x + pORect.width
            && lfY >= pORect.y && lfY <= pORect.y + pORect.height;
    }

    /// <summary>The pointers of the map view from CURSORS.GR: 12 is the flat-lying
    /// quill, 13 the eraser stone - the same one that is also depicted on the parchment
    /// (2026-09-03 picked out from an extract of all nineteen pointers).</summary>
    internal const int MapQuillCursor = 12;

    internal const int MapEraseCursor = 13;

    /// <summary>The ERASE area, estimated on the background picture: the stone including
    /// the label below it.</summary>
    private static readonly Rect mOMapEraseRect = new Rect(274f, 117f, 28f, 32f);

    internal bool mbMapEraseMode;

    /// <summary>
    /// A click on ERASE switches to erase mode - the pointer becomes the eraser stone.
    /// The next click, with either button, erases the note below, if one lies there, and
    /// brings the quill back (per user in the original, 2026-09-03).
    /// </summary>
    private bool fUpdateMapErase(Vector2 pOLocal, bool pbLeft)
    {
        // Both buttons switch on erase mode and thus also erase (per user
        // on the original, 2026-09-03). The parameter stays because the caller knows it.
        float lfY = -pOLocal.y;

        if (!mbMapEraseMode)
        {
            if (pOLocal.x < mOMapEraseRect.x || pOLocal.x > mOMapEraseRect.x + mOMapEraseRect.width
                || lfY < mOMapEraseRect.y || lfY > mOMapEraseRect.y + mOMapEraseRect.height)
                return false;

            mbMapEraseMode = true;

            return true;
        }

        fEraseNoteAt(pOLocal);

        mbMapEraseMode = false;

        return true;
    }

    /// <summary>Erases the note under the pointer. If you miss, erase mode ends
    /// anyway.</summary>
    private void fEraseNoteAt(Vector2 pOLocal)
    {
        if (mOMapNoteImages == null || mOUi.mOUWData == null)
            return;

        // The notes of the SHOWN level, not of the one the character stands on.
        System.Collections.Generic.List<UWLevel.MapNote> lONotes = mOUi.mOUWData.GetMapNotes(miMapLevel);

        if (lONotes == null)
            return;

        float lfY = -pOLocal.y;

        for (int liNote = 0; liNote < mOMapNoteImages.Count && liNote < lONotes.Count; liNote++)
        {
            RawImage lOImage = mOMapNoteImages[liNote];

            if (lOImage == null || !UWTextLabel.Get(lOImage).IsShown)
                continue;

            RectTransform lORect = (RectTransform)lOImage.transform;

            float lfLeft = lORect.anchoredPosition.x;
            float lfTop = -lORect.anchoredPosition.y;

            if (pOLocal.x < lfLeft || pOLocal.x > lfLeft + lORect.sizeDelta.x
                || lfY < lfTop || lfY > lfTop + lORect.sizeDelta.y)
                continue;

            lONotes.RemoveAt(liNote);

            fRefreshMapNotes(miMapLevel);

            return;
        }
    }

    /// <summary>
    /// The upright quill that sticks to the right of the text while writing.
    ///
    /// CURSORS.GR 14 - the same quill as when pointing, only steeper. It moves on with every
    /// character (per user on the original, 2026-09-03), so it hangs on the text end and not
    /// on the mouse pointer.
    /// </summary>
    private const int MapWriteCursor = 14;

    private RawImage mMapWriteQuillImage;

    /// <summary>
    /// Where the quill sits relative to the text end, in pixels.
    ///
    /// Negative X moves it to the left, positive Y down.
    ///
    /// Both are at zero: the lower left corner of the quill sits exactly at the text end, and
    /// that fits (per user, 2026-09-03). The compensation is done instead via the
    /// text start, which lies three pixels left of and below the click.
    ///
    /// CAUTION: should the quill pointer ever be enlarged - see the open question
    /// that the pointers look bigger in the original -, mOUi offset must follow.
    /// </summary>
    private const float MapWriteQuillOffsetX = 0f;

    private const float MapWriteQuillOffsetY = 0f;

    private void fRefreshMapWriteQuill(float pfLeft, float pfTop, float pfHeight)
    {
        if (mMapBackgroundImage == null || mOUi.mOCursorTextures == null
            || MapWriteCursor >= mOUi.mOCursorTextures.Count)
            return;

        if (mMapWriteQuillImage == null)
        {
            GameObject lOObject = new GameObject("MapWriteQuill", typeof(RectTransform), typeof(RawImage));
            lOObject.transform.SetParent(mMapBackgroundImage.transform, false);

            RectTransform lOQuillRect = (RectTransform)lOObject.transform;
            lOQuillRect.anchorMin = new Vector2(0f, 1f);
            lOQuillRect.anchorMax = new Vector2(0f, 1f);
            lOQuillRect.pivot = new Vector2(0f, 0f);

            mMapWriteQuillImage = lOObject.GetComponent<RawImage>();
            mMapWriteQuillImage.raycastTarget = false;
            mMapWriteQuillImage.texture = mOUi.mOCursorTextures[MapWriteCursor];
            mMapWriteQuillImage.SetNativeSize();
        }

        ((RectTransform)mMapWriteQuillImage.transform).anchoredPosition = new Vector2(
            pfLeft + MapWriteQuillOffsetX,
            -(pfTop + pfHeight + MapWriteQuillOffsetY));

        mMapWriteQuillImage.enabled = true;
        mMapWriteQuillImage.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Writing a note.
    ///
    /// A click with the quill on the parchment sets the start, then you type. The
    /// line holds 46 characters - measured on the original, although the field has 50 bytes. Enter
    /// finishes the line, Escape keeps it too and closes the map (see fUpdateMapWriting).
    ///
    /// The position is stored as a SCREEN pixel, with Y from the bottom - just as the save game
    /// keeps it.
    /// </summary>
    private const int MapNoteMaxLength = 46;

    internal bool mbMapWriting;

    private int miMapWriteNote = -1;

    private void fBeginMapNote(Vector2 pOLocal)
    {
        System.Collections.Generic.List<UWLevel.MapNote> lONotes = mOUi.mOUWData.GetMapNotes(miMapLevel);

        if (lONotes == null)
            return;

        lONotes.Add(new UWLevel.MapNote
        {
            Text = string.Empty,
            // The text starts three pixels left of and three below the click - so the
            // quill tip lands exactly on the clicked pixel again (per user on the original,
            // 2026-09-03). Y counts from the bottom, so lower means smaller.
            X = Mathf.RoundToInt(pOLocal.x) - 3,
            Y = Mathf.RoundToInt(UWHudPanel.FrameHeight + pOLocal.y) - 3
        });

        miMapWriteNote = lONotes.Count - 1;

        if (!mbMapWriting)
        {
            mbMapWriting = true;

            // While writing, the quill sticks to the text and no longer follows the mouse.
            // The system pointer therefore goes away, the quill is drawn by us.
            Cursor.visible = false;

            if (UnityEngine.InputSystem.Keyboard.current != null)
                UnityEngine.InputSystem.Keyboard.current.onTextInput += fOnMapTextInput;
        }

        // Draw ONLY here: the quill depends on the writing state, and that is set one line
        // earlier. Before mOUi it did not appear (per user, 2026-09-03).
        fRefreshMapNotes(miMapLevel);
    }

    private void fEndMapNote(bool pbKeep)
    {
        if (!mbMapWriting)
            return;

        mbMapWriting = false;

        Cursor.visible = true;

        if (mMapWriteQuillImage != null)
            mMapWriteQuillImage.enabled = false;

        if (UnityEngine.InputSystem.Keyboard.current != null)
            UnityEngine.InputSystem.Keyboard.current.onTextInput -= fOnMapTextInput;

        System.Collections.Generic.List<UWLevel.MapNote> lONotes = mOUi.mOUWData.GetMapNotes(miMapLevel);

        // An empty line is not kept - not even when confirming.
        if (lONotes != null && miMapWriteNote >= 0 && miMapWriteNote < lONotes.Count
            && (!pbKeep || string.IsNullOrEmpty(lONotes[miMapWriteNote].Text)))
        {
            lONotes.RemoveAt(miMapWriteNote);
        }

        miMapWriteNote = -1;

        fRefreshMapNotes(miMapLevel);
    }

    private void fOnMapTextInput(char pcChar)
    {
        if (!mbMapWriting || pcChar < 32 || pcChar > 126)
            return;

        // CAPITAL LETTERS: font4x5p knows no lower case ones, and in the original all notes are
        // in capitals. Typed in lower case, nothing arrived at all otherwise (per user, 2026-09-03).
        fSetMapNoteText(fGetMapNoteText() + char.ToUpperInvariant(pcChar));
    }

    private string fGetMapNoteText()
    {
        System.Collections.Generic.List<UWLevel.MapNote> lONotes = mOUi.mOUWData.GetMapNotes(miMapLevel);

        return lONotes != null && miMapWriteNote >= 0 && miMapWriteNote < lONotes.Count
            ? lONotes[miMapWriteNote].Text
            : string.Empty;
    }

    private void fSetMapNoteText(string psText)
    {
        System.Collections.Generic.List<UWLevel.MapNote> lONotes = mOUi.mOUWData.GetMapNotes(miMapLevel);

        if (lONotes == null || miMapWriteNote < 0 || miMapWriteNote >= lONotes.Count)
            return;

        if (psText.Length > MapNoteMaxLength)
            psText = psText.Substring(0, MapNoteMaxLength);

        UWLevel.MapNote lONote = lONotes[miMapWriteNote];

        lONote.Text = psText;

        lONotes[miMapWriteNote] = lONote;

        fRefreshMapNotes(miMapLevel);
    }

    /// <summary>Backspace, Enter and Escape. The characters themselves come via
    /// onTextInput.</summary>
    private bool fUpdateMapWriting()
    {
        if (!mbMapWriting)
            return false;

        UnityEngine.InputSystem.Keyboard lOKeyboard = UnityEngine.InputSystem.Keyboard.current;

        if (lOKeyboard == null)
            return true;

        // Escape takes over the text AND closes the map immediately (per user on the
        // original, 2026-09-03) - so it does not discard.
        if (lOKeyboard.escapeKey.wasPressedThisFrame)
        {
            fEndMapNote(true);
            HideMap();

            return true;
        }

        if (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame)
        {
            fEndMapNote(true);

            return true;
        }

        if (lOKeyboard.backspaceKey.wasPressedThisFrame)
        {
            string lsText = fGetMapNoteText();

            if (lsText.Length > 0)
                fSetMapNoteText(lsText.Substring(0, lsText.Length - 1));
        }

        return true;
    }

    /// <summary>As long as the map is open, nothing happens in the world. The click on CLOSE
    /// closes it.</summary>
    internal void Update()
    {
        if (!IsMapVisible)
            return;

        // While writing, the keyboard belongs to the line.
        bool lbWriting = fUpdateMapWriting();

        if (UnityEngine.InputSystem.Mouse.current == null)
            return;

        bool lbLeft = UWMouseButtons.LeftPressed;
        bool lbRight = UWMouseButtons.RightPressed;

        if (!lbLeft && !lbRight)
            return;

        Vector2 lOLocal;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)mMapBackgroundImage.transform, 
            UnityEngine.InputSystem.Mouse.current.position.ReadValue(), null, out lOLocal))
            return;

        // Erase mode swallows the click.
        if (fUpdateMapErase(lOLocal, lbLeft))
            return;

        if (lbLeft && fUpdateMapPaging(lOLocal))
            return;

        // CLOSE and ERASE listen to BOTH buttons (per user on the original, 2026-09-03).
        float lfY = -lOLocal.y;

        if (lOLocal.x >= mOMapCloseRect.x && lOLocal.x <= mOMapCloseRect.x + mOMapCloseRect.width
            && lfY >= mOMapCloseRect.y && lfY <= mOMapCloseRect.y + mOMapCloseRect.height)
        {
            HideMap();

            return;
        }

        // Both buttons start a line and both commit it again (per user
        // on the original). A click while writing only finishes and does
        // NOT immediately start the next one.
        if (lbLeft || lbRight)
        {
            if (lbWriting)
                fEndMapNote(true);
            else
                fBeginMapNote(lOLocal);
        }
    }
}
