using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE HELP'S MAP TAB ON THE SECOND GAME (per user, 2026-11-07; set aside in G0 of the 2026-11-06 sweep): the
/// tab asked the first game's idle UWGameUI whether a map is carried and drew its automap pictures. The second
/// game's map has no map to carry - a level with a map opens (UWAutomapScreenUw2.HasMap) - and its picture is the
/// whole map screen as UWMapScreenUw2 composes it: the blank map, the seen tiles three pixels a tile, the gem and
/// the arrow (UWMapScreenUw2.ComposeScreen). The notes as on that screen, in its small font and ink, only shown -
/// they are written on the map itself. Built anew when the player moved or the refresh time passed, as the first
/// game's tab does.
/// </summary>
public sealed class UWHelpMapUw2
{
    /// <summary>One map tile in pixels of the 320x200 screen (UWMapScreenUw2's arrow steps).</summary>
    public const int TilePixels = 3;

    private Texture2D mOPicture;

    private int miPictureLevel = -1;

    private Vector2 mOPlayer;

    private readonly List<UWAutomapPictures.Note> mONotes = new List<UWAutomapPictures.Note>();

    private readonly List<UWTextTextures.Handle> mONoteTexts = new List<UWTextTextures.Handle>();

    private readonly List<UWMapNotesModel.ShownNote> mOLayout = new List<UWMapNotesModel.ShownNote>();

    private string msNotesKey;

    public Texture2D Picture => mOPicture;

    /// <summary>The middle of the player's place on the screen, from the top-left.</summary>
    public Vector2 PlayerPixel => mOPlayer;

    internal List<UWAutomapPictures.Note> Notes => mONotes;

    /// <summary>Whether the level has a map at all (the second game's levels without one, such as the Void's).</summary>
    public static bool HasMap(int piLevel)
    {
        return UWAutomapScreenUw2.HasMap(piLevel);
    }

    /// <summary>The picture and the notes of level piLevel (1-based) anew; false without them.</summary>
    public bool Build(DataImport pOData, int piLevel)
    {
        byte[] lyScreen = UWMapScreenUw2.ComposeScreen(pOData, piLevel, out Vector2 lOPlayer);

        if (lyScreen == null)
            return false;

        mOPicture = UWMapScreenUw2.UploadScreen(pOData, lyScreen, mOPicture, "UW2 help map");
        mOPlayer = lOPlayer;
        miPictureLevel = piLevel;
        fBuildNotes(pOData, piLevel);

        return true;
    }

    /// <summary>The level the picture shows, -1 before the first.</summary>
    public int Level => miPictureLevel;

    /// <summary>The notes as the map shows them (UWMapNotesModel's layout, the small font, the map's ink); only
    /// rebuilt when a note changed.</summary>
    private void fBuildNotes(DataImport pOData, int piLevel)
    {
        List<UWLevel.MapNote> lONotes = pOData.GetMapNotes(piLevel - 1);
        string lsKey = UWMapNotesModel.Key(piLevel - 1, lONotes);

        if (lsKey == msNotesKey)
            return;

        msNotesKey = lsKey;
        fReleaseNotes();

        UWFont lOFont = pOData.Fonts != null ? pOData.Fonts.Get(UWFonts.FontType.Small) : null;

        if (lONotes == null || lOFont == null)
            return;

        UWMapNotesModel.Layout(lONotes, psText => UWAutomapPictures.MeasureNote(lOFont, psText), mOLayout);

        Color32 lOInk = UWMapScreenUw2.InkOf(pOData);

        foreach (UWMapNotesModel.ShownNote lONote in mOLayout)
        {
            UWTextTextures.Handle lOText = UWTextTextures.Acquire(lOFont, new[] { lONote.Text }, lONote.Width,
                new[] { lOInk }, UWTextRaster.ColoursEnum.Codes, false, FilterMode.Point);

            if (lOText == null)
                continue;

            mONoteTexts.Add(lOText);
            mONotes.Add(new UWAutomapPictures.Note
            {
                Texture = lOText.Texture,
                TopLeft = new Vector2(lONote.Left, lONote.Top)
            });
        }
    }

    private void fReleaseNotes()
    {
        foreach (UWTextTextures.Handle lOOld in mONoteTexts)
            UWTextTextures.Release(lOOld);

        mONoteTexts.Clear();
        mONotes.Clear();
    }

    /// <summary>Everything made goes (the tab's window closes).</summary>
    public void Release()
    {
        fReleaseNotes();
        msNotesKey = null;

        if (mOPicture != null)
            Object.Destroy(mOPicture);

        mOPicture = null;
        miPictureLevel = -1;
    }
}
