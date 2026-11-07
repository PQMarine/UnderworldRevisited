using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S MAP (UW2 plan phase 3 step 6), as Underworld Exhumed's uw2 ui/AUTOMAP.C draws it:
/// BYT.ARK's screen 0 in palette 1, every seen tile three pixels a tile (UWAutomapPainter with the second
/// game's byte), the gem with the worlds visited and the world shown, the player's arrow on the own level,
/// the number in the world. The picture is built as palette indices, as the original draws into its
/// screen, and only then turned into colours. Its input is UWAutomapScreenUw2's.
///
/// The notes are the first game's (UWMapNotesModel, UWMapNoteRules, the same in ManageDungeonMap but for
/// where a note starts); the clicks that reach them are this screen's. The pointer is the game screen's,
/// which shows PointerTexture while the map lies open (UWGameScreenUw2.fMapPointer): the whole screen runs
/// on the map's palette, so the cursors do too. The level it opens on and the arrow ask UWAutomapStateUw2:
/// the automap can be off.
/// </summary>
public sealed class UWMapScreenUw2 : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    /// <summary>A note is being typed: the keys are the note's.</summary>
    public static bool IsWriting { get; private set; }

    /// <summary>The eraser is on the pointer: the next click erases.</summary>
    public bool IsErasing { get; private set; }

    private DataImport mOData;

    private Canvas mOCanvas;

    private RectTransform mOFrame;

    private RawImage mOPicture;

    private RawImage mOQuill;

    private RawImage mOLevelNumber;

    private readonly System.Collections.Generic.List<RawImage> mONoteImages = new System.Collections.Generic.List<RawImage>();

    private UWMapNotesModel mONotes;

    private UWTextEntry mOTextEntry;

    private Texture2D mOTexture;

    /// <summary>The pen and the eraser in the map's palette at the pointer's scale.</summary>
    private readonly Texture2D[] mOPointers = new Texture2D[2];

    private int miPointerScale;

    /// <summary>The original's level number shown, 1 to 80.</summary>
    private int miLevel;

    private static readonly uint msSeed = UWAutomapView.SeedFrom(System.Environment.TickCount);

    /// <summary>Opens on the player's own level.</summary>
    public void Open(DataImport pOData)
    {
        mOData = pOData;

        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (mOData == null || lOLoader == null)
            return;

        fEnsureCanvas();
        fReleasePointers();
        mOCanvas.gameObject.SetActive(true);
        IsOpen = true;
        fShow(UWAutomapStateUw2.OpeningLevel(fPlayerFile(), lOLoader.CurrentLevelIndex + 1));
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        fEndNote();
        UWAutomapStateUw2.LeaveMap(fPlayerFile(), miLevel);
        IsErasing = false;
        IsOpen = false;

        if (mOCanvas != null)
            mOCanvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        IsOpen = false;
        IsWriting = false;
        fReleasePointers();
    }

    /// <summary>The pointer's picture, piScale screen pixels a pixel: the pen, or the eraser on the pointer.</summary>
    public Texture2D PointerTexture(int piScale)
    {
        if (miPointerScale != piScale)
        {
            fReleasePointers();
            miPointerScale = piScale;
        }

        int liAt = IsErasing ? 1 : 0;

        if (mOPointers[liAt] == null)
        {
            Texture2D lOPicture = fInMapPalette(IsErasing ? UWClassicLayoutUw2.Map.EraserCursor : UWClassicLayoutUw2.Map.PenCursor);

            if (lOPicture == null)
                return null;

            mOPointers[liAt] = UWGameScreenUw2.ScaledCopy(lOPicture, piScale, "UW2 map pointer");
            Destroy(lOPicture);
        }

        return mOPointers[liAt];
    }

    private void fReleasePointers()
    {
        for (int liAt = 0; liAt < mOPointers.Length; liAt++)
        {
            if (mOPointers[liAt] != null)
                Destroy(mOPointers[liAt]);

            mOPointers[liAt] = null;
        }
    }

    /// <summary>A picture of CURSORS.GR in the map's palette, colour 0 clear.</summary>
    private Texture2D fInMapPalette(int piCursor)
    {
        UWTexture lOPicture = fPicture(UWTexture.TextureTypes.CURSORS, piCursor);

        if (lOPicture == null)
            return null;

        byte[] lyIndices = lOPicture.GetMainPaletteIndices();
        int liWidth = lOPicture.Width;
        int liHeight = lOPicture.Height;
        int liPalette = UWScreensUw2.PaletteOf(UWScreensUw2.ScreenEnum.Automap);
        Color32[] lyPixels = new Color32[liWidth * liHeight];

        for (int liRow = 0; liRow < liHeight; liRow++)
        {
            for (int liColumn = 0; liColumn < liWidth; liColumn++)
            {
                byte lyIndex = lyIndices[(liRow * liWidth) + liColumn];
                Color32 lOColour = UWColourVision.Apply(UWScreenUi.GetColour(mOData.Palettes, liPalette, lyIndex));

                lOColour.a = lyIndex != 0 ? (byte)255 : (byte)0;
                lyPixels[((liHeight - 1 - liRow) * liWidth) + liColumn] = lOColour;
            }
        }

        return UWTextureFactory.FromColours(lyPixels, liWidth, liHeight, FilterMode.Point, TextureWrapMode.Clamp, "UW2 map cursor " + piCursor);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        Rect lOScreen = UWScreenUi.ScreenRectOf(mOPicture);

        if (lOScreen.width <= 0f || lOScreen.height <= 0f)
            return;

        if (IsWriting)
        {
            fUpdateWriting();

            return;
        }

        Vector2 lOAt = UWPointer.Position;
        int liX = Mathf.FloorToInt((lOAt.x - lOScreen.xMin) / lOScreen.width * UWScreenUi.ScreenWidth);
        int liY = Mathf.FloorToInt((lOAt.y - lOScreen.yMin) / lOScreen.height * UWScreenUi.ScreenHeight);

        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();

            return;
        }

        // Either button acts (ManageDungeonMap: any command below 4).
        if (UWPointer.LeftPressed || UWPointer.RightPressed)
            fClick(liX, liY);
    }

    /// <summary>The keys of the note: Enter or a click ends it, Escape keeps it too and closes the map.</summary>
    private void fUpdateWriting()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();

            return;
        }

        if (UWTextEntry.EnterPressed || UWPointer.LeftPressed || UWPointer.RightPressed)
        {
            fEndNote();

            return;
        }

        if (UWTextEntry.BackspacePressed && mONotes.Backspace())
            fShowNotes();

        mOTextEntry.ReadKeys();
    }

    private void fOnTyped(char pcTyped)
    {
        if (mONotes.Type(pcTyped))
            fShowNotes();
    }

    /// <summary>The note being typed ends, kept if it holds text. While typing the quill hangs on the text's
    /// end, not on the mouse.</summary>
    private void fEndNote()
    {
        if (!mONotes.End(true))
            return;

        IsWriting = false;
        UWPointer.IsShown = true;
        mOTextEntry.End();
        fShowNotes();
    }

    private void fClick(int piX, int piY)
    {
        // The eraser takes the next click, wherever it lands, and is put down again.
        if (IsErasing)
        {
            IsErasing = false;

            if (mONotes.EraseAt(piX, piY - UWClassicLayoutUw2.Map.EraserRowsBelow))
                fShowNotes();

            return;
        }

        switch (UWAutomapScreenUw2.ClickAt(piX, piY, out int liGem))
        {
            case UWAutomapScreenUw2.ClickEnum.Note:
                if (mONotes.Begin(piX + UWClassicLayoutUw2.Map.NoteOffset, piY + UWClassicLayoutUw2.Map.NoteOffset))
                {
                    IsWriting = true;
                    UWPointer.IsShown = false;
                    mOTextEntry.Begin();
                    fShowNotes();
                }

                break;

            case UWAutomapScreenUw2.ClickEnum.Erase:
                IsErasing = true;
                break;

            case UWAutomapScreenUw2.ClickEnum.Close:
                Close();
                break;

            case UWAutomapScreenUw2.ClickEnum.PreviousLevel:
                fShow(UWAutomapScreenUw2.Previous(miLevel));
                break;

            case UWAutomapScreenUw2.ClickEnum.NextLevel:
                fShow(UWAutomapScreenUw2.Next(miLevel));
                break;

            case UWAutomapScreenUw2.ClickEnum.Gem:
                fShow(UWAutomapScreenUw2.InWorld(miLevel, liGem));
                break;
        }
    }

    /// <summary>ShowAutoMapLevel: the screen of the level, built in palette indices.</summary>
    private void fShow(int piLevel)
    {
        miLevel = piLevel;
        mONotes.Show(piLevel - 1);

        byte[] lyScreen = ComposeScreen(mOData, piLevel, out _);

        if (lyScreen == null)
            return;

        fUpload(lyScreen);
        fShowNotes();
        fShowLevelNumber(piLevel);
    }

    /// <summary>
    /// THE MAP'S PICTURE AS PALETTE INDICES, top row first: BYT.ARK's blank map, the seen tiles, the gem and, on the
    /// player's own level with the automap on, his arrow (UWAutomapStateUw2). pOPlayer: the middle of the arrow's
    /// place on the 320x200 screen from the top-left, also when the arrow is not drawn - the help's Map tab centres
    /// on it (UWHelpMapUw2). Null without the blank map. Shared by this screen and the help (per user, 2026-11-07).
    /// </summary>
    public static byte[] ComposeScreen(DataImport pOData, int piLevel, out Vector2 pOPlayer)
    {
        pOPlayer = new Vector2(UWScreenUi.ScreenWidth / 2f, UWScreenUi.ScreenHeight / 2f);

        UWTexture lOBlank = fPictureOf(pOData, UWTexture.TextureTypes.BLNKMAP, 0);

        if (lOBlank == null || lOBlank.PaletteIndices == null)
            return null;

        byte[] lyScreen = (byte[])lOBlank.PaletteIndices.Clone();
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWLevel lOLevel = fLevel(piLevel);

        if (lOLevel != null && lOLevel.AutomapTiles != null)
        {
            int liShown = piLevel - 1;

            UWAutomapPainter.Paint(lyScreen, lOLevel.AutomapTiles,
                (piX, piY, peUse) => UWAutomapView.Random(msSeed, liShown, piX, piY, peUse), UWGame.Uw2);
        }

        fShowGem(pOData, lyScreen, piLevel);

        Camera lOCamera = Camera.main;

        if (lOLoader != null && lOCamera != null && piLevel == lOLoader.CurrentLevelIndex + 1)
        {
            UWTilePos lOAt = UWTileQueries.WorldToTile(lOCamera.transform.position.x, lOCamera.transform.position.z);
            UWTexture lOArrow = fPictureOf(pOData, UWTexture.TextureTypes.BUTTONS, UWClassicLayoutUw2.Map.PlayerPicture);
            int liLeft = ((lOAt.X - 1) * 3) + UWClassicLayoutUw2.Map.PlayerOffsetX;
            int liTop = ((lOAt.Y - 1) * 3) + UWClassicLayoutUw2.Map.PlayerOffsetY;

            // fOverlay's rows start at ScreenHeight - 1 - piTop, counted from the top.
            if (lOArrow != null)
                pOPlayer = new Vector2(liLeft + (lOArrow.Width / 2f), UWScreenUi.ScreenHeight - 1 - liTop + (lOArrow.Height / 2f));

            if (UWAutomapStateUw2.IsOn(fPlayerFile()))
                fOverlay(lyScreen, lOArrow, liLeft, liTop);
        }

        return lyScreen;
    }

    /// <summary>The indices in the map's palette as a texture, top row first in pyScreen, opaque; pOOld is
    /// destroyed.</summary>
    public static Texture2D UploadScreen(DataImport pOData, byte[] pyScreen, Texture2D pOOld, string psName)
    {
        int liWidth = UWScreenUi.ScreenWidth;
        int liHeight = UWScreenUi.ScreenHeight;
        byte[] lyRgba = new byte[liWidth * liHeight * UWUploadPixels.BytesPerPixel];
        int liPalette = UWScreensUw2.PaletteOf(UWScreensUw2.ScreenEnum.Automap);

        for (int liRow = 0; liRow < liHeight; liRow++)
        {
            for (int liColumn = 0; liColumn < liWidth; liColumn++)
            {
                Color32 lOColour = UWColourVision.Apply(UWScreenUi.GetColour(pOData.Palettes, liPalette, pyScreen[(liRow * liWidth) + liColumn]));
                int liAt = (((liHeight - 1 - liRow) * liWidth) + liColumn) * UWUploadPixels.BytesPerPixel;

                lyRgba[liAt] = lOColour.r;
                lyRgba[liAt + 1] = lOColour.g;
                lyRgba[liAt + 2] = lOColour.b;
                lyRgba[liAt + 3] = 255;
            }
        }

        if (pOOld != null)
            Destroy(pOOld);

        return UWTextureFactory.Upload(lyRgba, liWidth, liHeight, FilterMode.Point, TextureWrapMode.Clamp, psName);
    }

    /// <summary>The notes' ink in the map's palette.</summary>
    public static Color32 InkOf(DataImport pOData)
    {
        return UWColourVision.Apply(UWScreenUi.GetColour(pOData.Palettes, UWScreensUw2.PaletteOf(UWScreensUw2.ScreenEnum.Automap),
            UWClassicLayoutUw2.Map.InkColour));
    }

    /// <summary>RedisplayStrings: the notes of the page in the small font, and the quill behind the one
    /// being typed.</summary>
    private void fShowNotes()
    {
        foreach (RawImage lOOld in mONoteImages)
            UWTextLabel.Hide(lOOld);

        UWFont lOFont = fFont(UWFonts.FontType.Small);

        if (lOFont != null)
        {
            foreach (UWMapNotesModel.ShownNote lONote in mONotes.Layout())
            {
                while (mONoteImages.Count <= lONote.Index)
                    mONoteImages.Add(null);

                if (mONoteImages[lONote.Index] == null)
                    mONoteImages[lONote.Index] = fImage("Note" + lONote.Index);

                RawImage lOImage = mONoteImages[lONote.Index];

                UWTextLabel.Get(lOImage).SetLine(lOFont, lONote.Text, lONote.Width, fInk(), false, FilterMode.Point);
                UWScreenUi.Place(lOImage, lONote.Left, lONote.Top);
            }
        }

        if (mOQuill == null)
            return;

        mOQuill.enabled = mONotes.TryGetQuill(out int liLeft, out int liTop);

        if (mOQuill.enabled)
        {
            // The pointer stands at the picture's middle.
            UWScreenUi.Place(mOQuill, liLeft + UWClassicLayoutUw2.Map.QuillMiddleX - (mOQuill.texture.width / 2),
                liTop + UWClassicLayoutUw2.Map.NoteOffset - UWClassicLayoutUw2.Map.QuillMiddleY - (mOQuill.texture.height / 2));
            mOQuill.transform.SetAsLastSibling();
        }
    }

    /// <summary>The level's number in the world, in the big font.</summary>
    private void fShowLevelNumber(int piLevel)
    {
        UWFont lOFont = fFont(UWFonts.FontType.Big);

        if (lOFont == null)
            return;

        string lsNumber = UWAutomapScreenUw2.NumberInWorld(piLevel).ToString();

        UWTextLabel.Get(mOLevelNumber).SetLine(lOFont, lsNumber, UWTextRaster.TightWidth(lOFont, lsNumber), fInk(), false, FilterMode.Point);
        UWScreenUi.Place(mOLevelNumber, UWClassicLayoutUw2.Map.LevelNumberX, UWScreenUi.ScreenHeight - UWClassicLayoutUw2.Map.LevelNumberY);
    }

    private UWFont fFont(UWFonts.FontType peType)
    {
        return mOData.Fonts != null ? mOData.Fonts.Get(peType) : null;
    }

    private Color32 fInk()
    {
        return InkOf(mOData);
    }

    private static UWPlayerFile fPlayerFile()
    {
        return UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;
    }

    private static UWLevel fLevel(int piLevel)
    {
        UWGameSession lOSession = UWGameSession.Current;

        if (lOSession == null || lOSession.World == null || lOSession.World.Levels == null || !UWAutomapScreenUw2.HasMap(piLevel)
            || piLevel > lOSession.World.Levels.Count)
            return null;

        return lOSession.World.Levels[piLevel - 1];
    }

    /// <summary>show_gem_parts: the facets of the worlds visited, the world shown over them.</summary>
    private static void fShowGem(DataImport pOData, byte[] pyScreen, int piLevel)
    {
        UWGameSession lOSession = UWGameSession.Current;
        byte[] lyRecord = lOSession != null && lOSession.PlayerFileUw2 != null ? lOSession.PlayerFileUw2.Record : null;
        int liVisited = lyRecord != null ? lyRecord[UWPlayerRecordUw2.WorldsVisitedOffset] : 0;

        for (int liFacet = 0; liFacet < UWClassicLayoutUw2.Map.MiddleFacet; liFacet++)
        {
            if ((liVisited & (1 << liFacet)) != 0)
                fFacet(pOData, pyScreen, liFacet, liFacet);
        }

        int liShown = UWAutomapScreenUw2.FacetOf(piLevel);

        fFacet(pOData, pyScreen, liShown, liShown + UWClassicLayoutUw2.Map.CurrentFacetStep);
    }

    private static void fFacet(DataImport pOData, byte[] pyScreen, int piFacet, int piPicture)
    {
        fOverlay(pyScreen, fPictureOf(pOData, UWTexture.TextureTypes.GEMPT, piPicture),
            UWClassicLayoutUw2.Map.FacetX[piFacet] + UWClassicLayoutUw2.Map.FacetLeft, UWClassicLayoutUw2.Map.FacetY[piFacet]);
    }

    /// <summary>pic_to_screen with Transparency: colour 0 leaves the screen; piTop the top row counted
    /// from the bottom.</summary>
    private static void fOverlay(byte[] pyScreen, UWTexture pOPicture, int piLeft, int piTop)
    {
        if (pOPicture == null)
            return;

        byte[] lyIndices = pOPicture.GetMainPaletteIndices();

        for (int liRow = 0; liRow < pOPicture.Height; liRow++)
        {
            int liScreenRow = UWScreenUi.ScreenHeight - 1 - piTop + liRow;

            if (liScreenRow < 0 || liScreenRow >= UWScreenUi.ScreenHeight)
                continue;

            for (int liColumn = 0; liColumn < pOPicture.Width; liColumn++)
            {
                int liX = piLeft + liColumn;
                byte lyIndex = lyIndices[(liRow * pOPicture.Width) + liColumn];

                if (lyIndex != 0 && liX >= 0 && liX < UWScreenUi.ScreenWidth)
                    pyScreen[(liScreenRow * UWScreenUi.ScreenWidth) + liX] = lyIndex;
            }
        }
    }

    private UWTexture fPicture(UWTexture.TextureTypes peType, int piIndex)
    {
        return fPictureOf(mOData, peType, piIndex);
    }

    private static UWTexture fPictureOf(DataImport pOData, UWTexture.TextureTypes peType, int piIndex)
    {
        try
        {
            return pOData != null && pOData.Textures != null ? pOData.Textures.GetTextureByType(peType, piIndex) : null;
        }
        catch (System.ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>The indices in the map's palette onto the screen's picture (UploadScreen).</summary>
    private void fUpload(byte[] pyScreen)
    {
        mOTexture = UploadScreen(mOData, pyScreen, mOTexture, "UW2 map");
        UWScreenUi.SetTexture(mOPicture, mOTexture);
    }

    private void fEnsureCanvas()
    {
        if (mOCanvas != null)
            return;

        mOCanvas = UWScreenUi.CreateCanvas(transform, "UW2 Map", UWUiSorting.CharacterCreation, out mOFrame);

        GameObject lOObject = new GameObject("Map", typeof(RectTransform));

        lOObject.transform.SetParent(mOFrame, false);
        mOPicture = lOObject.AddComponent<RawImage>();
        mOPicture.raycastTarget = false;
        UWScreenUi.Place(mOPicture, 0, 0);

        mONotes = new UWMapNotesModel(piLevel => mOData.GetMapNotes(piLevel),
            psText => UWAutomapPictures.MeasureNote(fFont(UWFonts.FontType.Small), psText));
        mOTextEntry = new UWTextEntry(fOnTyped);
        mOLevelNumber = fImage("Level");

        Texture2D lOQuill = fInMapPalette(UWClassicLayoutUw2.Map.QuillCursor);

        if (lOQuill != null)
        {
            mOQuill = fImage("Quill");
            UWScreenUi.SetTexture(mOQuill, lOQuill);
            mOQuill.enabled = false;
        }

        mOCanvas.gameObject.SetActive(false);
    }

    /// <summary>A picture on the screen, placed by its top-left corner.</summary>
    private RawImage fImage(string psName)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform));

        lOObject.transform.SetParent(mOFrame, false);

        RawImage lOImage = lOObject.AddComponent<RawImage>();
        RectTransform lORect = (RectTransform)lOObject.transform;

        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lOImage.raycastTarget = false;

        return lOImage;
    }
}
