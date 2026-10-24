using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The map's pictures as textures (cleanup step 5.11, 2026-10-19): the tiles of a level, the blank
/// parchment, the notes. What they show is UWAutomapView's and UWMapNotesModel's (engine-free);
/// this class only finds the level, the character's tile and the font and uploads the result. It
/// serves the classic map (UWHudMap, also the modern scheme's big map), the help window's map tab
/// and the modern minimap - those two asked the classic map class for these pictures until then
/// (UWHudMap.BuildMinimapTiles, GetMinimapParchment, GetMinimapNotes, TryGetMinimapPlayerPixel).
/// Owned by UWGameUI (AutomapPictures).
/// </summary>
public sealed class UWAutomapPictures
{
    private readonly UWGameUI mOUi;

    internal UWAutomapPictures(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    /// <summary>The ink of the notes and the level number (UWClassicLayout.Map.NoteColour).</summary>
    internal static readonly Color32 NoteColour = new Color32(UWClassicLayout.Map.NoteColour.R,
        UWClassicLayout.Map.NoteColour.G, UWClassicLayout.Map.NoteColour.B, UWClassicLayout.Map.NoteColour.A);

    /// <summary>The seed of the map's random steps, taken once per run (UWAutomapView.Random): the
    /// same on every redraw and every opening of the map, in all three places that show it.</summary>
    private static readonly uint msSeed = UWAutomapView.SeedFrom(System.Environment.TickCount);

    /// <summary>Where the tiles sit on the 320x200 map screen (top-left, screen pixels).</summary>
    internal static Vector2 TilesTopLeft => new Vector2(UWAutomapView.BlockLeft, UWAutomapView.BlockTop);

    /// <summary>The side of the tiles' block in map screen pixels.</summary>
    internal const int TilesSize = UWAutomapView.BlockSize;

    /// <summary>How large one tile is, in map screen pixels.</summary>
    internal const int TilePixels = UWAutomapView.TileSize;

    private UWLevelLoader mOLevelLoader;

    private bool fEnsureLevelLoader()
    {
        if (mOLevelLoader == null)
            mOLevelLoader = UWScene.LevelLoader;

        return mOLevelLoader != null;
    }

    /// <summary>
    /// The level's map into pyRgba (UWAutomapView.BufferLength bytes, bottom row first): the
    /// discovered tiles and - with pbPlayerMark, on the level the character really stands on - his
    /// mark. False when there is no level loader.
    /// </summary>
    internal bool Compose(byte[] pyRgba, int piLevelIndex, bool pbPlayerMark)
    {
        if (!fEnsureLevelLoader())
            return false;

        bool lbMark = pbPlayerMark && mOUi.mCharacter != null && piLevelIndex == mOLevelLoader.CurrentLevelIndex;
        UWTilePos lOTile = lbMark ? mOLevelLoader.WorldPositionToTile(mOUi.mCharacter.transform.position) : default(UWTilePos);

        UWAutomapView.Compose(pyRgba, mOUi.mOUWData, piLevelIndex, msSeed, UWColourVision.Filter, lbMark, lOTile.X, lOTile.Y);

        return true;
    }

    /// <summary>The tiles with the player's mark [1] and without [0] - each a texture of its own,
    /// as the help's map tab and the modern minimap both hold theirs.</summary>
    private readonly Texture2D[] mOTiles = new Texture2D[2];

    private readonly byte[][] myTiles = new byte[2][];

    /// <summary>The discovered tiles of the level the character stands on, freshly drawn into the
    /// texture of this kind (the map's own stays untouched). pbPlayerMark: the original's cross
    /// where the character stands - the modern minimap leaves it out, its arrow says it (per user,
    /// 2026-10-04).</summary>
    internal Texture2D BuildTiles(bool pbPlayerMark = true)
    {
        if (!fEnsureLevelLoader())
            return null;

        int liKind = pbPlayerMark ? 1 : 0;

        if (myTiles[liKind] == null)
            myTiles[liKind] = new byte[UWAutomapView.BufferLength];

        Compose(myTiles[liKind], mOLevelLoader.CurrentLevelIndex, pbPlayerMark);

        if (mOTiles[liKind] == null)
            mOTiles[liKind] = UWTextureFactory.Upload(myTiles[liKind], UWAutomapView.BlockSize, UWAutomapView.BlockSize,
                FilterMode.Point, TextureWrapMode.Clamp, "UWAutomapPictures tiles");
        else
            UWTextureFactory.Reupload(mOTiles[liKind], myTiles[liKind]);

        return mOTiles[liKind];
    }

    private Texture2D mOParchment;

    /// <summary>The blank map (BLNKMAP.GR, 320x200) as a texture of its own, for the help
    /// window's map tab and the modern minimap.</summary>
    internal Texture2D GetParchment()
    {
        if (mOParchment != null || mOUi.mOUWData == null)
            return mOParchment;

        UWTexture lOSource;

        if (!mOUi.mOUWData.Textures.BitmapFiles.TryGetValue(UWTexture.TextureTypes.BLNKMAP, out lOSource)
            || lOSource == null)
            return null;

        mOParchment = UWTextureFactory.FromTexture(lOSource, FilterMode.Point, "UWAutomapPictures parchment", TextureWrapMode.Clamp);

        return mOParchment;
    }

    /// <summary>One note of the map as the help window and the minimap draw it: the rendered text
    /// and its top-left corner on the 320x200 map screen.</summary>
    internal struct Note
    {
        public Texture2D Texture;
        public Vector2 TopLeft;
    }

    private readonly List<Note> mONotes = new List<Note>();

    /// <summary>The notes' texts, held from UWTextTextures (cleanup step 5.5).</summary>
    private readonly List<UWTextTextures.Handle> mONoteTexts = new List<UWTextTextures.Handle>();

    private readonly List<UWMapNotesModel.ShownNote> mOLayout = new List<UWMapNotesModel.ShownNote>();

    private string msNotesKey;

    /// <summary>The notes of the level the character stands on, rendered as on the map (small
    /// font, dark brown), laid out by UWMapNotesModel. Only rebuilt when a note changed. Read-only -
    /// notes are written and erased on the real map alone (per user, 2026-09-26).</summary>
    internal List<Note> GetNotes()
    {
        if (!fEnsureLevelLoader() || mOUi.mOUWData == null)
            return mONotes;

        int liLevel = mOLevelLoader.CurrentLevelIndex;
        List<UWLevel.MapNote> lONotes = mOUi.mOUWData.GetMapNotes(liLevel);
        string lsKey = UWMapNotesModel.Key(liLevel, lONotes);

        if (lsKey == msNotesKey)
            return mONotes;

        msNotesKey = lsKey;

        foreach (UWTextTextures.Handle lOOld in mONoteTexts)
            UWTextTextures.Release(lOOld);

        mONoteTexts.Clear();
        mONotes.Clear();

        UWFont lOFont = mOUi.mOUWData.Fonts.Get(UWFonts.FontType.Small);

        if (lONotes == null || lOFont == null)
            return mONotes;

        UWMapNotesModel.Layout(lONotes, psText => MeasureNote(lOFont, psText), mOLayout);

        foreach (UWMapNotesModel.ShownNote lONote in mOLayout)
        {
            // The same picture the map's label asks for (UWHudMap.fRefreshMapNotes): the note's text
            // in the small font, in the note ink, as wide as the label.
            UWTextTextures.Handle lOText = UWTextTextures.Acquire(lOFont, new[] { lONote.Text }, lONote.Width,
                new[] { NoteColour }, UWTextRaster.ColoursEnum.Codes, false, FilterMode.Point);

            if (lOText == null)
                continue;

            mONoteTexts.Add(lOText);
            mONotes.Add(new Note
            {
                Texture = lOText.Texture,
                TopLeft = new Vector2(lONote.Left, lONote.Top)
            });
        }

        return mONotes;
    }

    /// <summary>The middle of the character's tile on the 320x200 map screen (top-left
    /// origin), or false when there is no level.</summary>
    internal bool TryGetPlayerPixel(out Vector2 pOPixel)
    {
        pOPixel = Vector2.zero;

        if (!fEnsureLevelLoader() || mOUi.mCharacter == null)
            return false;

        UWTilePos lOTile = mOLevelLoader.WorldPositionToTile(mOUi.mCharacter.transform.position);

        UWAutomapView.PlayerPixel(lOTile.X, lOTile.Y, out float lfX, out float lfY);
        pOPixel = new Vector2(lfX, lfY);

        return true;
    }

    /// <summary>string_width of the notes' small font: the sum of the glyph widths.</summary>
    internal static int MeasureNote(UWFont pOFont, string psText)
    {
        return pOFont != null ? pOFont.MeasureText(psText, 0, UWFontRenderer.CharacterSpacing) : 0;
    }
}
