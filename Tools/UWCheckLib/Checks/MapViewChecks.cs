using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.11 (2026-10-19): what the map shows (UWAutomapView) and the notes with the
    /// map's clicks (UWMapNotesModel) left the classic map class (UWHudMap), so the classic map, the
    /// help window's map tab and the modern minimap read one source. Pinned: the geometry the three
    /// places share, the per-run random hash, the click order of ManageDungeonMap as the map had
    /// it (writing ends first, eraser, arrows, CLOSE, a new note), the note's start three pixels left
    /// of and below the click, Backspace, an empty line dropped, the layout and the quill; against the
    /// real data, the composed tiles and the player's mark byte for byte as the old Unity loops made
    /// them.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckAutomapView()
        {
            Check.Group("Automap view (UWAutomapView - cleanup step 5.11)");

            fExpectInt("map view: the block is 64 tiles of 3 pixels", UWAutomapView.BlockSize, 192);
            fExpectInt("map view: its left edge on the map screen", UWAutomapView.BlockLeft, 7);
            fExpectInt("map view: its top edge (200 - 4 - 192)", UWAutomapView.BlockTop, 4);
            fExpectInt("map view: the buffer, four bytes a pixel", UWAutomapView.BufferLength, 192 * 192 * 4);

            UWAutomapView.PlayerPixel(10, 10, out float lfX, out float lfY);
            fExpectBool("map view: the middle of tile 10/10 (y from the top, rows from the south)",
                lfX == 38.5f && lfY == 164.5f, true);

            UWAutomapView.PlayerPixel(0, 63, out lfX, out lfY);
            fExpectBool("map view: the north-west tile", lfX == 8.5f && lfY == 5.5f, true);

            uint luSeed = UWAutomapView.SeedFrom(12345);
            int liRoll = UWAutomapView.Random(luSeed, 2, 40, 50, UWAutomapPainter.RandomUse.Floor);

            fExpectBool("map view: a roll lies in 0..0x7FFF", liRoll >= 0 && liRoll <= 0x7FFF, true);
            fExpectInt("map view: the same pixel rolls the same on every redraw",
                UWAutomapView.Random(luSeed, 2, 40, 50, UWAutomapPainter.RandomUse.Floor), liRoll);
            fExpectBool("map view: what it is drawn for changes the roll",
                UWAutomapView.Random(luSeed, 2, 40, 50, UWAutomapPainter.RandomUse.Border) != liRoll
                || UWAutomapView.Random(luSeed, 2, 40, 50, UWAutomapPainter.RandomUse.Corner) != liRoll, true);

            // A parchment of index 5 everywhere, a palette whose entry n is n, n + 1, n + 2, one open
            // tile at 10/10.
            byte[] lyParchment = new byte[UWAutomapPainter.ScreenWidth * UWAutomapPainter.ScreenHeight];

            for (int liAt = 0; liAt < lyParchment.Length; liAt++)
                lyParchment[liAt] = 5;

            byte[] lyPaletteData = new byte[256 * 3];

            for (int liAt = 0; liAt < 64; liAt++)
            {
                lyPaletteData[liAt * 3] = (byte)liAt;
                lyPaletteData[(liAt * 3) + 1] = (byte)(liAt + 1);
                lyPaletteData[(liAt * 3) + 2] = (byte)(liAt + 2);
            }

            UWPalette lOPalette = new UWPalette(lyPaletteData);
            byte[] lyAutomap = new byte[64 * 64];
            lyAutomap[(10 * 64) + 10] = 0x01;

            byte[] lyRgba = new byte[UWAutomapView.BufferLength];

            UWAutomapView.PaintTiles(lyRgba, lyParchment, lOPalette, lyAutomap, (piX, piY, peUse) => 0x7FFF, null);

            // Floor is parchment + 2..4; the palette's entry n is 4n, 4n + 4, 4n + 8.
            int liFloor = fMapPixel(31, 31);

            fExpectBool("map view: a floor pixel of tile 10/10 is opaque in a palette colour above the parchment's",
                lyRgba[liFloor + 3] == 255 && lyRgba[liFloor + 1] == lyRgba[liFloor] + 4 && lyRgba[liFloor] > 5 * 4, true);
            fExpectInt("map view: the parchment elsewhere stays clear", lyRgba[fMapPixel(100, 100) + 3], 0);

            UWColourFilter lOFilter = new UWColourFilter();
            byte[] lyFiltered = new byte[UWAutomapView.BufferLength];

            UWAutomapView.PaintTiles(lyFiltered, lyParchment, lOPalette, lyAutomap, (piX, piY, peUse) => 0x7FFF, lOFilter);
            fExpectBool("map view: an inactive colour help changes nothing", fSameBytes(lyRgba, lyFiltered), true);

            lOFilter.Set(UWColourFilter.ModeEnum.Red, 1f, 1f, 1f);
            UWAutomapView.PaintTiles(lyFiltered, lyParchment, lOPalette, lyAutomap, (piX, piY, peUse) => 0x7FFF, lOFilter);

            byte lyRed = lyRgba[liFloor];
            byte lyGreen = lyRgba[liFloor + 1];
            byte lyBlue = lyRgba[liFloor + 2];

            lOFilter.Apply(ref lyRed, ref lyGreen, ref lyBlue);
            fExpectBool("map view: the colour help runs over the tiles",
                lyFiltered[liFloor] == lyRed && lyFiltered[liFloor + 1] == lyGreen && lyFiltered[liFloor + 2] == lyBlue, true);

            // A 3 by 2 mark: index 0 clear, its dagger column (1) on the tile's middle column, its
            // lowest row on the tile's lowest row.
            System.Array.Clear(lyRgba, 0, lyRgba.Length);
            UWAutomapView.DrawMarkPicture(lyRgba, 10, 10, new byte[] { 0, 7, 0, 8, 9, 0 }, 3, 2, lOPalette);

            fExpectInt("map view: the mark's top (index 7) above the tile's lowest row",
                lyRgba[fMapPixel(31, 31)], lOPalette.Red[7]);
            fExpectInt("map view: its bottom row on the tile's lowest row (index 9)", lyRgba[fMapPixel(31, 30)], lOPalette.Red[9]);
            fExpectInt("map view: index 0 stays clear", lyRgba[fMapPixel(30, 31) + 3], 0);

            // At the map's edge the mark is cut, not wrapped: on tile 63/63 only the lowest gold pixel
            // of its middle column (x 190, y 189 + 2) stays on the block.
            System.Array.Clear(lyRgba, 0, lyRgba.Length);
            UWAutomapView.DrawHandMadeMark(lyRgba, 63, 63);
            fExpectInt("map view: the hand-made mark's gold on the middle column", lyRgba[fMapPixel(190, 191)], 230);
        }

        /// <summary>The byte index of map pixel x/y (y from the bottom).</summary>
        private static int fMapPixel(int piX, int piY)
        {
            return ((piY * UWAutomapView.BlockSize) + piX) * UWUploadPixels.BytesPerPixel;
        }

        private static bool fSameBytes(byte[] pyA, byte[] pyB)
        {
            if (pyA.Length != pyB.Length)
                return false;

            for (int liAt = 0; liAt < pyA.Length; liAt++)
            {
                if (pyA[liAt] != pyB[liAt])
                    return false;
            }

            return true;
        }

        private static void fCheckMapNotesModel()
        {
            Check.Group("Map notes model (UWMapNotesModel - cleanup step 5.11)");

            Dictionary<int, List<UWLevel.MapNote>> lOPages = new Dictionary<int, List<UWLevel.MapNote>>();
            System.Func<int, List<UWLevel.MapNote>> lfNotes = piLevel =>
            {
                if (!lOPages.TryGetValue(piLevel, out List<UWLevel.MapNote> lONotes))
                    lOPages[piLevel] = lONotes = new List<UWLevel.MapNote>();

                return lONotes;
            };

            UWMapNotesModel lOModel = new UWMapNotesModel(lfNotes, psText => psText.Length * 4);
            UWRectInt lOErase = UWClassicLayout.Map.Erase;
            UWRectInt lOClose = UWClassicLayout.Map.Close;
            UWRectInt lOUp = UWClassicLayout.Map.PageUp;
            UWRectInt lODown = UWClassicLayout.Map.PageDown;

            lOModel.Show(3);
            fExpectInt("map notes model: the map opens on the given page", lOModel.ShownLevel, 3);

            // A note: its text starts at the clicked column, its top four rows above the click (y
            // from the bottom; AUTOMAP.C note->x = mx, note->y = my + 4, since 2026-10-23).
            fExpectBool("map notes model: a click on the parchment starts a note",
                lOModel.Click(100f, 50f) == UWMapNotesModel.ClickEnum.NoteStarted && lOModel.IsWriting, true);
            fExpectBool("map notes model: ... at x and (200 - y) + 4",
                lfNotes(3)[0].X == 100 && lfNotes(3)[0].Y == 154 && lfNotes(3)[0].Text == string.Empty, true);

            int liLeft;
            int liTop;

            fExpectBool("map notes model: the quill of an empty line stands at its start",
                lOModel.TryGetQuill(out liLeft, out liTop) && liLeft == 100 && liTop == 46, true);
            fExpectBool("map notes model: typing upper-cases", lOModel.Type('a') && lOModel.Type('b') && lfNotes(3)[0].Text == "AB", true);
            fExpectBool("map notes model: the quill right behind the text",
                lOModel.TryGetQuill(out liLeft, out liTop) && liLeft == 100 + 8 && liTop == 46, true);
            fExpectBool("map notes model: '{' is refused", lOModel.Type('{'), false);
            fExpectBool("map notes model: Backspace takes the last character", lOModel.Backspace() && lfNotes(3)[0].Text == "A", true);

            // Any click while writing only ends the note - also one on CLOSE.
            fExpectBool("map notes model: a click while writing only ends the note",
                lOModel.Click(lOClose.X + 1, lOClose.Y + 1) == UWMapNotesModel.ClickEnum.NoteEnded && !lOModel.IsWriting
                && lfNotes(3).Count == 1, true);

            List<UWMapNotesModel.ShownNote> lOLayout = lOModel.Layout();

            fExpectBool("map notes model: the layout gives the label's top-left (y from the top) and width",
                lOLayout.Count == 1 && lOLayout[0].Index == 0 && lOLayout[0].Left == 100 && lOLayout[0].Top == 46
                && lOLayout[0].Width == 4, true);

            // An empty line is dropped, also when confirmed.
            lOModel.Click(150f, 60f);
            fExpectBool("map notes model: an empty line is not kept", lOModel.End(true) && lfNotes(3).Count == 1, true);

            lOModel.Click(150f, 60f);
            lOModel.Type('x');
            fExpectBool("map notes model: pbKeep false drops a written line", lOModel.End(false) && lfNotes(3).Count == 1, true);

            // The eraser: armed by ERASE, it erases on the SHOWN page and is put down after one click.
            fExpectBool("map notes model: ERASE arms the eraser",
                lOModel.Click(lOErase.X, lOErase.Y + lOErase.Height) == UWMapNotesModel.ClickEnum.EraseArmed && lOModel.IsErasing, true);
            fExpectBool("map notes model: a miss puts the eraser down anyway",
                lOModel.Click(10f, 10f) == UWMapNotesModel.ClickEnum.Erased && !lOModel.IsErasing && lfNotes(3).Count == 1, true);

            lOModel.Click(lOErase.X + 2, lOErase.Y + 2);
            fExpectBool("map notes model: the armed eraser takes the note under it",
                lOModel.Click(101f, 48f) == UWMapNotesModel.ClickEnum.Erased && lfNotes(3).Count == 0, true);

            // Paging, clamped to the note pages the game keeps.
            fExpectBool("map notes model: the top corner pages one level up",
                lOModel.Click(lOUp.X + 1, lOUp.Y + 1) == UWMapNotesModel.ClickEnum.Paged && lOModel.ShownLevel == 2, true);
            fExpectBool("map notes model: the bottom corner one down",
                lOModel.Click(lODown.X + 1, lODown.Y + 1) == UWMapNotesModel.ClickEnum.Paged && lOModel.ShownLevel == 3, true);

            lOModel.Show(0);
            fExpectBool("map notes model: no page above the first",
                lOModel.Click(lOUp.X + 1, lOUp.Y + 1) == UWMapNotesModel.ClickEnum.None && lOModel.ShownLevel == 0, true);

            lOModel.Show(DataImport.MapNoteLevelCount - 1);
            fExpectBool("map notes model: none below the last note page", lOModel.PageBy(1), false);

            fExpectBool("map notes model: CLOSE closes", lOModel.Click(lOClose.X + lOClose.Width, lOClose.Y) == UWMapNotesModel.ClickEnum.Close, true);

            // Closing keeps the note being written and puts the eraser down.
            lOModel.Show(5);
            lOModel.Click(60f, 60f);
            lOModel.Type('q');
            fExpectBool("map notes model: closing the map keeps the note being written",
                lOModel.Close() && lfNotes(5).Count == 1 && lfNotes(5)[0].Text == "Q", true);

            lOModel.Click(lOErase.X + 2, lOErase.Y + 2);
            lOModel.Close();
            fExpectBool("map notes model: ... and puts the eraser down", lOModel.IsErasing, false);

            // A hundred notes fill the page.
            for (int liAt = lfNotes(6).Count; liAt < UWMapNoteRules.MaxNotesPerLevel; liAt++)
                lfNotes(6).Add(new UWLevel.MapNote { Text = "N", X = 10, Y = 10 });

            lOModel.Show(6);
            fExpectBool("map notes model: a full page starts no note",
                lOModel.Click(60f, 60f) == UWMapNotesModel.ClickEnum.None && !lOModel.IsWriting, true);

            // The pointer is rounded as Mathf.RoundToInt does (half to even).
            lOModel.Show(7);
            lOModel.Click(20.5f, 50f);
            fExpectInt("map notes model: x 20.5 rounds to 20", lfNotes(7)[0].X, 20);
            lOModel.End(false);

            List<UWMapNotesModel.ShownNote> lOShown = new List<UWMapNotesModel.ShownNote>();

            UWMapNotesModel.Layout(new List<UWLevel.MapNote>
            {
                new UWLevel.MapNote { Text = string.Empty, X = 1, Y = 1 },
                new UWLevel.MapNote { Text = "AB", X = 30, Y = 120 }
            }, psText => 0, lOShown);

            fExpectBool("map notes model: an empty line has no label, a label is at least one pixel wide",
                lOShown.Count == 1 && lOShown[0].Index == 1 && lOShown[0].Width == 1 && lOShown[0].Top == 80, true);
            fExpectBool("map notes model: the key follows a note's text",
                UWMapNotesModel.Key(2, new List<UWLevel.MapNote> { new UWLevel.MapNote { Text = "A", X = 1, Y = 2 } })
                != UWMapNotesModel.Key(2, new List<UWLevel.MapNote> { new UWLevel.MapNote { Text = "B", X = 1, Y = 2 } }), true);

            // The original's leftovers after a note's zero (a SAVE0 it wrote, 2026-10-23: "A", 0,
            // then 01 F1 F1 01 F1 ...) go back unchanged; a changed text is written clean.
            byte[] lyRecord = new byte[54];
            byte[] lyLeftover = { 0x41, 0x00, 0x01, 0xF1, 0xF1, 0x01, 0xF1, 0xF1, 0xF1, 0xF1, 0xF1, 0xF1, 0xF1, 0xF1, 0xF1, 0xF1, 0xF1, 0xF1 };
            System.Array.Copy(lyLeftover, lyRecord, lyLeftover.Length);
            lyRecord[0x32] = 228;
            lyRecord[0x34] = 103;

            // Behind two bytes: offset 0 means "no block" to the reader.
            byte[] lyFile = new byte[2 + lyRecord.Length];
            System.Array.Copy(lyRecord, 0, lyFile, 2, lyRecord.Length);

            List<UWLevel.MapNote> lORead = UWLevel.ParseMapNotes(lyFile, 2, lyFile.Length);

            fExpectBool("map note block: a note the original wrote comes back byte for byte",
                lORead.Count == 1 && lORead[0].Text == "A" && fSameRecord(UWSaveCodec.BuildMapNoteBlock(lORead), lyRecord), true);

            UWLevel.MapNote lOChanged = lORead[0];
            lOChanged.Text = "AB";
            lORead[0] = lOChanged;
            byte[] lyChanged = UWSaveCodec.BuildMapNoteBlock(lORead);

            fExpectBool("map note block: a changed text is written clean after its zero",
                lyChanged[0] == 0x41 && lyChanged[1] == 0x42 && lyChanged[2] == 0 && lyChanged[3] == 0 && lyChanged[0x32] == 228, true);
        }

        private static bool fSameRecord(byte[] pyLeft, byte[] pyRight)
        {
            if (pyLeft == null || pyLeft.Length != pyRight.Length)
                return false;

            for (int liAt = 0; liAt < pyLeft.Length; liAt++)
                if (pyLeft[liAt] != pyRight[liAt])
                    return false;

            return true;
        }

        /// <summary>The composed tiles and the original's mark on the real parchment, palette and
        /// BUTTONS.GR, against the loops UWHudMap ran until 2026-10-19 (fComposeMapTiles,
        /// fDrawOriginalPlayerMark), written out here as they were.</summary>
        private static void fCheckAutomapViewData(DataImport pOData)
        {
            Check.Group("Automap view on the data (UWAutomapView - cleanup step 5.11)");

            UWTexture lOParchment = pOData.Textures.BitmapFiles[UWTexture.TextureTypes.BLNKMAP];
            UWPalette lOPalette = lOParchment.Palettes.GetPalette(lOParchment.MainPaletteIndex);
            uint luSeed = UWAutomapView.SeedFrom(4711);
            System.Random lORandom = new System.Random(5);
            byte[] lyAutomap = new byte[64 * 64];

            // Every tile type and display, discovered or not, at random.
            for (int liAt = 0; liAt < lyAutomap.Length; liAt++)
                lyAutomap[liAt] = (byte)lORandom.Next(256);

            byte[] lyNew = new byte[UWAutomapView.BufferLength];

            UWAutomapView.PaintTiles(lyNew, lOParchment.PaletteIndices, lOPalette, lyAutomap,
                (piX, piY, peUse) => UWAutomapView.Random(luSeed, 4, piX, piY, peUse), null);

            // The old loop, Color32 as four bytes, row 0 at the bottom.
            byte[] lyScreen = (byte[])lOParchment.PaletteIndices.Clone();

            UWAutomapPainter.Paint(lyScreen, lyAutomap, (piX, piY, peUse) => UWAutomapView.Random(luSeed, 4, piX, piY, peUse));

            byte[] lyOld = new byte[UWAutomapView.BufferLength];
            int liSize = 64 * 3;
            int liPainted = 0;

            for (int liY = 0; liY < liSize; liY++)
            {
                int liRow = (UWAutomapPainter.ScreenHeight - 1 - (liY + UWAutomapPainter.CellBottom)) * UWAutomapPainter.ScreenWidth;

                for (int liX = 0; liX < liSize; liX++)
                {
                    int liAt = liRow + liX + UWAutomapPainter.CellLeft;
                    byte lyIndex = lyScreen[liAt];

                    if (lyIndex == lOParchment.PaletteIndices[liAt])
                        continue;

                    UWColor32 lOColour = lOPalette.GetUWColor(lyIndex);
                    int liOut = ((liY * liSize) + liX) * 4;

                    lyOld[liOut] = lOColour.R;
                    lyOld[liOut + 1] = lOColour.G;
                    lyOld[liOut + 2] = lOColour.B;
                    lyOld[liOut + 3] = 255;
                    liPainted++;
                }
            }

            fExpectBool("map view data: something was painted", liPainted > 1000, true);
            fExpectBool("map view data: the tiles byte for byte as UWHudMap.fComposeMapTiles made them", fSameBytes(lyNew, lyOld), true);

            // The mark (BUTTONS.GR 0x3F in palette 1) on tile 20/30, against fDrawOriginalPlayerMark.
            byte[] lyMark = new byte[UWAutomapView.BufferLength];

            fExpectBool("map view data: the original's mark is found", UWAutomapView.DrawPlayerMark(lyMark, 20, 30, pOData), true);

            UWTexture lOMark = pOData.Textures.GetTextureByType(UWTexture.TextureTypes.BUTTONS, 0x3F);
            byte[] laIndices = lOMark.GetMainPaletteIndices();
            UWPalette lOMapPalette = pOData.Palettes.GetPalette(1);
            byte[] lyOldMark = new byte[UWAutomapView.BufferLength];
            int liLeft = (20 * 3) + (3 / 2) - 1;
            int liBottom = 30 * 3;

            for (int liRow = 0; liRow < lOMark.Height; liRow++)
            {
                for (int liColumn = 0; liColumn < lOMark.Width; liColumn++)
                {
                    int liAt = (liRow * lOMark.Width) + liColumn;

                    if (laIndices[liAt] == 0)
                        continue;

                    byte[] laRgb = lOMapPalette.GetRGB(laIndices[liAt]);
                    int liOut = (((liBottom + (lOMark.Height - 1 - liRow)) * liSize) + liLeft + liColumn) * 4;

                    lyOldMark[liOut] = laRgb[0];
                    lyOldMark[liOut + 1] = laRgb[1];
                    lyOldMark[liOut + 2] = laRgb[2];
                    lyOldMark[liOut + 3] = 255;
                }
            }

            fExpectBool("map view data: the mark byte for byte as fDrawOriginalPlayerMark drew it", fSameBytes(lyMark, lyOldMark), true);

            // Compose: level 1 with and without the mark differ only where the mark is.
            byte[] lyWith = new byte[UWAutomapView.BufferLength];
            byte[] lyWithout = new byte[UWAutomapView.BufferLength];

            UWAutomapView.Compose(lyWith, pOData, 0, luSeed, null, true, 20, 30);
            UWAutomapView.Compose(lyWithout, pOData, 0, luSeed, null, false, 0, 0);

            int liDiffer = 0;

            for (int liAt = 0; liAt < lyWith.Length; liAt++)
            {
                if (lyWith[liAt] != lyWithout[liAt])
                    liDiffer++;
            }

            fExpectBool("map view data: the mark changes only a few pixels", liDiffer > 0 && liDiffer <= lOMark.Width * lOMark.Height * 4, true);
        }
    }
}
