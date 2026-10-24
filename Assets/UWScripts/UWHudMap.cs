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
        mONotes = new UWMapNotesModel(
            piLevel => mOUi.mOUWData != null ? mOUi.mOUWData.GetMapNotes(piLevel) : null, fMeasureNote);
        mOTextEntry = new UWTextEntry(fOnMapTextInput);
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
    /// What the tiles show is UWAutomapView's since 2026-10-19 (cleanup 5.11), drawn for all
    /// three places that show the map by UWAutomapPictures.
    /// </summary>
    private const float MapLeft = UWAutomapView.BlockLeft;

    private const float MapTop = UWAutomapView.BlockTop;

    private const int MapTilesSize = UWAutomapView.BlockSize;

    /// <summary>The ink of the notes and the level number (UWClassicLayout.Map.NoteColour).</summary>
    private static readonly Color32 msNoteColour = UWAutomapPictures.NoteColour;

    /// <summary>The notes, the shown level page, the eraser and what a click does (cleanup 5.11,
    /// 2026-10-19); this class keeps the screen, the pointer and the keyboard.</summary>
    private readonly UWMapNotesModel mONotes;

    /// <summary>The note's typing - the text events and, where none arrive (Linux), the keys
    /// themselves (B26, 2026-10-19: until then the map alone did not read the keys).</summary>
    private readonly UWTextEntry mOTextEntry;

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
        mONotes.Show(piLevelIndex);

        fBuildMapScreen();

        if (mMapBackgroundImage == null)
            return false;

        if (!fBuildMapTiles(piLevelIndex))
            return false;

        fRefreshMapLevelNumber(piLevelIndex);
        fRefreshMapNotes();

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

        // The note being written is kept, the eraser put down.
        if (mONotes.Close())
            fAfterNoteEnded();

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
    /// The level number next to the baked-in word "Level".
    ///
    /// The word itself is in the background picture, only the number after it is drawn - and
    /// CENTRED, so that a two-digit number does not wander to the right (per user,
    /// 2026-09-03).
    /// </summary>
    private const float MapLevelNumberCentre = UWClassicLayout.Map.LevelNumberCentre;

    private const float MapLevelNumberTop = UWClassicLayout.Map.LevelNumberTop;

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

        // The previous level's number is given back to UWTextTextures; until 2026-10-19 its
        // texture stayed behind on every level change (B33, cleanup step 5.5).
        UWTextLabel.Get(mMapLevelImage).SetLine(lOFont, lsText, UWTextRaster.TightWidth(lOFont, lsText), msNoteColour, false,
            mOUi.TextureFilterMode);
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
    ///
    /// Where each label stands and how wide it is comes from UWMapNotesModel.Layout - the same
    /// layout the help window's map tab and the modern minimap use (UWAutomapPictures.GetNotes,
    /// cleanup 5.11, 2026-10-19).
    /// </summary>
    private System.Collections.Generic.List<RawImage> mOMapNoteImages;

    private void fRefreshMapNotes()
    {
        if (mMapBackgroundImage == null || mOUi.mOUWData == null)
            return;

        if (mOMapNoteImages == null)
            mOMapNoteImages = new System.Collections.Generic.List<RawImage>();

        foreach (RawImage lOOld in mOMapNoteImages)
            UWTextLabel.Hide(lOOld);

        UWFont lOFont = mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Small);

        if (lOFont == null)
            return;

        foreach (UWMapNotesModel.ShownNote lONote in mONotes.Layout())
        {
            while (mOMapNoteImages.Count <= lONote.Index)
                mOMapNoteImages.Add(null);

            if (mOMapNoteImages[lONote.Index] == null)
            {
                GameObject lOObject = new GameObject("MapNote" + lONote.Index,
                    typeof(RectTransform), typeof(RawImage));
                lOObject.transform.SetParent(mMapBackgroundImage.transform, false);

                RectTransform lORect = (RectTransform)lOObject.transform;
                lORect.anchorMin = new Vector2(0f, 1f);
                lORect.anchorMax = new Vector2(0f, 1f);
                lORect.pivot = new Vector2(0f, 1f);

                mOMapNoteImages[lONote.Index] = lOObject.GetComponent<RawImage>();
                mOMapNoteImages[lONote.Index].raycastTarget = false;
            }

            RawImage lOImage = mOMapNoteImages[lONote.Index];
            UWTextLabel lOLabel = UWTextLabel.Get(lOImage);

            // The label gives the previous text of the note back to UWTextTextures - otherwise one
            // texture stayed behind on every redraw (leak, 2026-09-13; cleanup step 5.5).
            lOLabel.SetLine(lOFont, lONote.Text, lONote.Width, msNoteColour, false, mOUi.TextureFilterMode);

            Texture2D lOTexture = lOLabel.Picture;

            if (lOTexture == null)
                continue;

            RectTransform lONoteRect = (RectTransform)lOImage.transform;

            lONoteRect.anchoredPosition = new Vector2(lONote.Left, -lONote.Top);
        }

        // While writing, the quill stands right behind the text; a still empty line shows only
        // the quill.
        if (mONotes.TryGetQuill(out int liQuillLeft, out int liQuillTop))
            fRefreshMapWriteQuill(liQuillLeft, liQuillTop);
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

        Texture2D lOTexture = UWTextureFactory.FromTexture(lOSource, mOUi.TextureFilterMode, "UWHudMap map screen", TextureWrapMode.Clamp);

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
        lOTileRect.sizeDelta = new Vector2(MapTilesSize, MapTilesSize);

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

        if (myMapTiles == null)
            myMapTiles = new byte[UWAutomapView.BufferLength];

        // The discovered tiles and, on the level the character stands on, his mark - drawn as
        // for the help window's map tab and the modern minimap (UWAutomapView).
        if (!mOUi.AutomapPictures.Compose(myMapTiles, piLevelIndex, true))
            return false;

        if (mMapTilesTexture == null)
            mMapTilesTexture = UWTextureFactory.Upload(myMapTiles, MapTilesSize, MapTilesSize, mOUi.TextureFilterMode,
                TextureWrapMode.Clamp, "UWHudMap map tiles");
        else
            UWTextureFactory.Reupload(mMapTilesTexture, myMapTiles);

        mMapTilesImage.texture = mMapTilesTexture;

        return true;
    }

    private byte[] myMapTiles;

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

    /// <summary>The CLOSE button at bottom right on the parchment, estimated on the background
    /// picture. A click on it closes the map.</summary>
    private static readonly Rect mOMapCloseRect = fRect(UWClassicLayout.Map.Close);

    private static Rect fRect(UWRectInt pORect)
    {
        return new Rect(pORect.X, pORect.Y, pORect.Width, pORect.Height);
    }

    /// <summary>The pointers of the map view from CURSORS.GR: 12 is the flat-lying
    /// quill, 13 the eraser stone - the same one that is also depicted on the parchment
    /// (2026-09-03 picked out from an extract of all nineteen pointers).</summary>
    internal const int MapQuillCursor = 12;

    internal const int MapEraseCursor = 13;

    /// <summary>string_width of the notes' small font: the sum of the glyph widths.</summary>
    private int fMeasureNote(string psText)
    {
        return UWAutomapPictures.MeasureNote(mOUi.mOUWData != null ? mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Small) : null, psText);
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
    /// Where the quill stands: its lower left corner, the tip, at the text's end, its lowest row
    /// UWClassicLayout.Map.WritingQuillRowsBelowTop rows below the text's top - the row above
    /// the clicked one, where the original puts it (AUTOMAP.C mouse_putxy(curx + 10, my + 0x12)
    /// with the picture's middle as the hot spot; 2026-10-23). Until then the corner sat at the
    /// text's bottom, per user on the original 2026-09-03, with the text itself placed from a
    /// pointer the system drew too small.
    /// </summary>
    private void fRefreshMapWriteQuill(float pfLeft, float pfTop)
    {
        if (mMapBackgroundImage == null || mOUi.PointerCursor.Pictures == null
            || MapWriteCursor >= mOUi.PointerCursor.Pictures.Count)
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
            mMapWriteQuillImage.texture = mOUi.PointerCursor.Pictures[MapWriteCursor];
            mMapWriteQuillImage.SetNativeSize();
        }

        ((RectTransform)mMapWriteQuillImage.transform).anchoredPosition = new Vector2(
            pfLeft,
            -(pfTop + UWClassicLayout.Map.WritingQuillRowsBelowTop));

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
    /// keeps it. The note's text, its length and the click that starts it are UWMapNotesModel's
    /// since 2026-10-19 (cleanup 5.11).
    /// </summary>
    internal bool IsWritingNote => mONotes.IsWriting;

    /// <summary>The eraser is armed: the pointer is the eraser stone (UWGameUI's cursor).</summary>
    internal bool IsErasing => mONotes.IsErasing;

    /// <summary>A note has started at the click.</summary>
    private void fAfterNoteStarted()
    {
        // While writing, the quill sticks to the text and no longer follows the mouse.
        // The system pointer therefore goes away, the quill is drawn by us.
        Cursor.visible = false;

        mOTextEntry.Begin();

        // Draw ONLY here: the quill depends on the writing state, and that is set one line
        // earlier. Before mOUi it did not appear (per user, 2026-09-03).
        fRefreshMapNotes();
    }

    private void fEndMapNote(bool pbKeep)
    {
        if (mONotes.End(pbKeep))
            fAfterNoteEnded();
    }

    /// <summary>The note being written has ended - kept, or dropped when empty.</summary>
    private void fAfterNoteEnded()
    {
        Cursor.visible = true;

        if (mMapWriteQuillImage != null)
            mMapWriteQuillImage.enabled = false;

        mOTextEntry.End();

        fRefreshMapNotes();
    }

    private void fOnMapTextInput(char pcChar)
    {
        if (mONotes.Type(pcChar))
            fRefreshMapNotes();
    }

    /// <summary>Backspace, Enter and Escape; the characters themselves come via onTextInput,
    /// and where none come (Linux) from the keys (UWTextEntry.ReadKeys).</summary>
    private bool fUpdateMapWriting()
    {
        if (!mONotes.IsWriting)
            return false;

        if (UnityEngine.InputSystem.Keyboard.current == null)
            return true;

        // Escape takes over the text AND closes the map immediately (per user on the
        // original, 2026-09-03) - so it does not discard.
        if (UWTextEntry.EscapePressed)
        {
            fEndMapNote(true);
            HideMap();

            return true;
        }

        if (UWTextEntry.EnterPressed)
        {
            fEndMapNote(true);

            return true;
        }

        if (UWTextEntry.BackspacePressed && mONotes.Backspace())
            fRefreshMapNotes();

        // Where no text events arrive (Linux), the keys themselves (UWTypedKeys) - the four other
        // text inputs always read them, the map note did not until 2026-10-19 (B26).
        mOTextEntry.ReadKeys();

        return true;
    }

    /// <summary>As long as the map is open, nothing happens in the world. The click on CLOSE
    /// closes it. What a click does is UWMapNotesModel.Click's.</summary>
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

        // While writing at the start of the frame, any click only ends the note - also when Enter
        // ended it in this very frame.
        if (lbWriting)
        {
            fEndMapNote(true);

            return;
        }

        // Both buttons alike (per user on the original, 2026-09-03): the parchment's y counts
        // from the top.
        switch (mONotes.Click(lOLocal.x, -lOLocal.y))
        {
            case UWMapNotesModel.ClickEnum.NoteEnded:
                fAfterNoteEnded();
                break;

            case UWMapNotesModel.ClickEnum.Erased:
                fRefreshMapNotes();
                break;

            case UWMapNotesModel.ClickEnum.Paged:
                fBuildMapTiles(mONotes.ShownLevel);
                fRefreshMapLevelNumber(mONotes.ShownLevel);
                fRefreshMapNotes();
                break;

            case UWMapNotesModel.ClickEnum.Close:
                HideMap();
                break;

            case UWMapNotesModel.ClickEnum.NoteStarted:
                fAfterNoteStarted();
                break;
        }
    }
}
