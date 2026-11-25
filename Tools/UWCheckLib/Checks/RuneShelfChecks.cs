using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.10 (2026-10-20): the rune selection and the rune bag page's hit tests moved
    /// out of the classic UWHudRunes (and the modern panel's copies) into UWRuneShelfModel, the
    /// castability checks into UWSpellCasting.CanCast. Pinned: append-then-trim, a cast starting a
    /// new shelf (also across a restore), Put and Load, the sequence helpers, the hit test of both
    /// schemes with its edges (the original's gapless 18 x 15 cells since decision 27, 2026-10-20;
    /// before that 14 x 14 compartments and 16 x 16 pictures), the clear strip, and the order of
    /// CanCast's reasons.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckRuneShelf()
        {
            Check.Group("Rune shelf (UWRuneShelfModel, UWSpellCasting.CanCast - cleanup step 5.10)");

            UWRuneShelfModel lOShelf = new UWRuneShelfModel();

            // Put clears the flag of a cast, so the checks below start from a known state.
            lOShelf.Put(null);
            lOShelf.Add(1);
            lOShelf.Add(2);
            lOShelf.Add(3);
            lOShelf.Add(4);
            Check.Str("shelf: the fourth rune pushes out the first", fRunes(lOShelf.Selected), "2 3 4");

            lOShelf.NoteCast();
            lOShelf.Add(14);
            Check.Str("shelf: the first rune after a cast starts a new shelf (mous_in_rune)", fRunes(lOShelf.Selected), "14");

            lOShelf.Add(8);
            Check.Str("shelf: ... and the next one appends again", fRunes(lOShelf.Selected), "14 8");

            lOShelf.NoteCast();
            lOShelf.Put(new[] { 8, 11, 14, 15 });
            Check.Str("shelf: Put replaces, at most three", fRunes(lOShelf.Selected), "8 11 14");
            lOShelf.Put(new[] { 8, 11 });
            lOShelf.Add(14);
            Check.Str("shelf: Put forgets the cast", fRunes(lOShelf.Selected), "8 11 14");

            lOShelf.Put(new[] { 8, 11 });
            fExpectInt("shelf: In Lor is sequence 8568", lOShelf.Sequence, 8568);
            fExpectInt("shelf: an empty slot reads as the empty rune", lOShelf.RuneInSlot(2), UWRunicMagic.EmptyRune);
            fExpectInt("shelf: ... and so does a slot past the list", lOShelf.RuneInSlot(-1), UWRunicMagic.EmptyRune);
            lOShelf.Clear();
            fExpectInt("shelf: Clear empties it", lOShelf.Selected.Count, 0);
            fExpectInt("shelf: the empty shelf is the empty sequence", lOShelf.Sequence, UWRunicMagic.EmptySequence);

            // Load: the save game's slots (PLAYER.DAT 0x47), an empty one skipped; the flag of a
            // cast before the restore survives it (per user on the original, 2026-10-13).
            byte[] lyPlain = UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            lyPlain[0x47] = 8;
            lyPlain[0x48] = UWPlayerData.SelectedRuneNone;
            lyPlain[0x49] = 11;

            UWPlayerData lOPlayer = UWPlayerData.FromPlain(lyPlain);

            lOPlayer.TryLearnRuneStone(UWPlayerData.FirstRuneObjectId + 8);
            lOPlayer.TryLearnRuneStone(UWPlayerData.FirstRuneObjectId + 11);

            lOShelf.Load(lOPlayer);
            Check.Str("shelf: Load skips the empty slot", fRunes(lOShelf.Selected), "8 11");

            UWRuneShelfModel lORestored = new UWRuneShelfModel();

            lOShelf.NoteCast();
            lORestored.Load(lOPlayer);
            lORestored.Add(14);
            Check.Str("shelf: a cast before a restore still empties the restored hollow", fRunes(lORestored.Selected), "14");

            // The sequence helpers.
            Check.Str("sequence: RunesOf In Lor", fRunes(UWRuneShelfModel.RunesOf(8568)), "8 11");
            fExpectInt("sequence: SequenceOf In Lor", UWRuneShelfModel.SequenceOf(new[] { 8, 11 }), 8568);
            fExpectBool("sequence: In Lor is in this bag", UWRuneShelfModel.HasRunes(8568, lOPlayer), true);
            fExpectBool("sequence: In Ort is not", UWRuneShelfModel.HasRunes(UWRuneShelfModel.SequenceOf(new[] { 8, 14 }), lOPlayer), false);
            fExpectBool("sequence: no bag, no runes", UWRuneShelfModel.HasRunes(8568, null), false);

            fCheckRuneShelfHits();
            fCheckCanCast(lOPlayer);
        }

        /// <summary>The page's grid (8/5, pitch 18/15) and the original's cells on it.</summary>
        private static void fCheckRuneShelfHits()
        {
            System.Func<int, bool> lfAll = liRune => true;
            System.Func<int, bool> lfNone = liRune => false;
            System.Func<int, bool> lfNotFirst = liRune => liRune != 0;
            UWRuneTabletGeometry lOUw1 = UWRuneTabletGeometry.Uw1;

            fExpectInt("grid: rune 5's left", lOUw1.SlotLeft(5), 26);
            fExpectInt("grid: rune 5's top", lOUw1.SlotTop(5), 20);

            // The click area: the panel's mouse region (BeginInventory, INVPANEL.C:161) on the page.
            fExpectBool("area: the panel's mouse region, x 0xF0 to 0x13B, y 0x52 to 0xBD from the bottom",
                UWClassicLayout.RuneShelf.HitLeft == 0xF0 - UWClassicLayout.Panel.Left
                && UWClassicLayout.RuneShelf.HitRight == 0x13B - UWClassicLayout.Panel.Left
                && UWClassicLayout.RuneShelf.HitTop == 199 - 0xBD - UWClassicLayout.Panel.Top
                && UWClassicLayout.RuneShelf.HitBottom == 199 - 0x52 - UWClassicLayout.Panel.Top, true);
            fExpectBool("area: the clear strip is its rows y < 0x12 (mous_in_rune)",
                UWClassicLayout.RuneShelf.ClearBottom == UWClassicLayout.RuneShelf.HitBottom
                && UWClassicLayout.RuneShelf.ClearTop == UWClassicLayout.RuneShelf.HitBottom - 0x12 + 1, true);

            // Both schemes, decision 27 (2026-10-20): the original's gapless 18 x 15 cells.
            fExpectInt("cell: the first compartment's corner", lOUw1.RuneCellAt(8f, 5f, lfAll), 0);
            fExpectInt("cell: the region's top-left pixel", lOUw1.RuneCellAt(4f, 3f, lfAll), 0);
            fExpectInt("cell: above the region nothing", lOUw1.RuneCellAt(10f, 2.9f, lfAll), -1);
            fExpectInt("cell: left of the region nothing", lOUw1.RuneCellAt(3.9f, 10f, lfAll), -1);
            fExpectInt("cell: the first column ends on x 24", lOUw1.RuneCellAt(24.9f, 10f, lfAll), 0);
            fExpectInt("cell: the second begins on 25", lOUw1.RuneCellAt(25f, 10f, lfAll), 1);
            fExpectInt("cell: the third begins on 43", lOUw1.RuneCellAt(43f, 10f, lfAll), 2);
            fExpectInt("cell: the fourth begins on 61", lOUw1.RuneCellAt(61f, 10f, lfAll), 3);
            fExpectInt("cell: ... and ends on 78", lOUw1.RuneCellAt(78.9f, 10f, lfAll), 3);
            fExpectInt("cell: x 79 is column 4, the next row's first rune", lOUw1.RuneCellAt(79f, 10f, lfAll), 4);
            fExpectInt("cell: ... below the last row nothing", lOUw1.RuneCellAt(79f, 85f, lfAll), -1);
            fExpectInt("cell: right of the region nothing", lOUw1.RuneCellAt(80f, 10f, lfAll), -1);
            fExpectInt("cell: the first row ends on row 17", lOUw1.RuneCellAt(10f, 17.9f, lfAll), 0);
            fExpectInt("cell: the second begins on 18, the compartment's bottom row", lOUw1.RuneCellAt(10f, 18f, lfAll), 4);
            fExpectInt("cell: the gap between two compartments", lOUw1.RuneCellAt(23f, 19f, lfAll), 4);
            fExpectInt("cell: the last row begins on 78", lOUw1.RuneCellAt(70f, 78f, lfAll), 23);
            fExpectInt("cell: ... and ends on 92", lOUw1.RuneCellAt(70f, 92.9f, lfAll), 23);
            fExpectInt("cell: the clear strip is no cell", lOUw1.RuneCellAt(70f, 93f, lfAll), -1);
            fExpectInt("cell: a rune not in the bag", lOUw1.RuneCellAt(8f, 5f, lfNone), -1);
            fExpectInt("cell: ... leaves its cell to nobody", lOUw1.RuneCellAt(10f, 17f, lfNotFirst), -1);

            // Every pixel of the region: one rune or the clear strip, each rune's cell 18 x 15
            // (the first column 21 wide), all within one rectangle; only x 79 is the original's odd
            // column above.
            int liMissed = 0;
            int liBoth = 0;
            int[] liCount = new int[UWPlayerData.RuneCount];

            for (int liRow = UWClassicLayout.RuneShelf.HitTop; liRow <= UWClassicLayout.RuneShelf.HitBottom; liRow++)
            {
                for (int liX = UWClassicLayout.RuneShelf.HitLeft; liX < UWClassicLayout.RuneShelf.HitRight; liX++)
                {
                    int liRune = lOUw1.RuneCellAt(liX + 0.5f, liRow + 0.5f, lfAll);
                    bool lbClear = lOUw1.IsClearStrip(liRow);

                    if (liRune < 0 && !lbClear)
                        liMissed++;
                    else if (liRune >= 0 && lbClear)
                        liBoth++;
                    else if (liRune >= 0)
                        liCount[liRune]++;
                }
            }

            fExpectInt("cells: no pixel of the region misses", liMissed, 0);
            fExpectInt("cells: no pixel is a rune and the clear strip", liBoth, 0);

            bool lbSizes = true;

            for (int liRune = 0; liRune < UWPlayerData.RuneCount; liRune++)
                lbSizes &= liCount[liRune] == (liRune % UWClassicLayout.RuneShelf.Columns == 0 ? 21 : 18) * 15;

            fExpectBool("cells: 18 x 15 each, the first column 21 x 15", lbSizes, true);

            // The clear strip, both edge rows in.
            fExpectBool("clear strip: row 93", lOUw1.IsClearStrip(93f), true);
            fExpectBool("clear strip: row 110", lOUw1.IsClearStrip(110f), true);
            fExpectBool("clear strip: above it", lOUw1.IsClearStrip(92.9f), false);
            fExpectBool("clear strip: below it", lOUw1.IsClearStrip(110.1f), false);

            fCheckRuneTabletUw2();
        }

        /// <summary>
        /// The second game's page (UW2 plan part U4c step 1, 2026-11-23): ShowRune's places, the panel's mouse
        /// region of BeginInventory, and for every pixel of it the same answer as mous_in_rune (uw2 RUNES.C:99-104),
        /// written out here as the classic page of UWGameScreenUw2 computes it on the screen.
        /// </summary>
        private static void fCheckRuneTabletUw2()
        {
            UWRuneTabletGeometry lOUw2 = UWRuneTabletGeometry.Uw2;
            System.Func<int, bool> lfAll = liRune => true;

            fExpectInt("uw2 page: 79 wide", lOUw2.PageWidth, 79);
            fExpectInt("uw2 page: 112 high", lOUw2.PageHeight, 112);
            fExpectInt("uw2 grid: rune 0's left, x 0xF1 on the screen", lOUw2.SlotLeft(0), 6);
            fExpectInt("uw2 grid: rune 0's top, y 0xBC from the bottom", lOUw2.SlotTop(0), 4);
            fExpectInt("uw2 grid: rune 5's left", lOUw2.SlotLeft(5), 24);
            fExpectInt("uw2 grid: rune 5's top", lOUw2.SlotTop(5), 19);
            fExpectInt("uw2 grid: rune 23's top, the last row", lOUw2.SlotTop(23), 79);
            fExpectBool("uw2 area: x 0xF0 to 0x13B, y 0x51 to 0xBE from the bottom",
                lOUw2.HitLeft == 5 && lOUw2.HitRight == 80 && lOUw2.HitTop == 2 && lOUw2.HitBottom == 111, true);
            fExpectBool("uw2 area: the clear strip its rows y < 0x12", lOUw2.ClearTop == 94 && lOUw2.ClearBottom == 111, true);
            fExpectBool("uw2 area: the page's game", lOUw2.IsSecondGame && !UWRuneTabletGeometry.Uw1.IsSecondGame, true);

            int liDiffer = 0;
            int liRunes = 0;
            int liClear = 0;

            for (int liRow = lOUw2.HitTop; liRow <= lOUw2.HitBottom; liRow++)
            {
                for (int liX = lOUw2.HitLeft; liX <= lOUw2.HitRight; liX++)
                {
                    // mous_in_rune on the screen: x and y from the bottom, relative to 0xF0 and 0x51.
                    int liScreenX = liX + UWClassicLayoutUw2.Panel.Left;
                    int liScreenY = UWClassicLayout.Screen.Height - 1 - (liRow + UWClassicLayoutUw2.Panel.Top);
                    int liRelX = liScreenX - UWClassicLayoutUw2.Statistics.Left;
                    int liRelY = liScreenY - UWClassicLayoutUw2.Statistics.AreaBottomY;
                    bool lbOriginalClear = liRelY < UWClassicLayoutUw2.Runes.ClearStrip;
                    int liOriginal = -1;

                    if (!lbOriginalClear)
                    {
                        liOriginal = UWClassicLayoutUw2.Runes.TopRowFirst
                            - (((liRelY - UWClassicLayoutUw2.Runes.ClearStrip) / UWClassicLayoutUw2.Runes.StepY) << 2)
                            + (liRelX / UWClassicLayoutUw2.Runes.StepX);

                        if (liOriginal < 0 || liOriginal >= UWPlayerData.RuneCount)
                            liOriginal = -1;
                    }

                    int liOurs = lOUw2.RuneCellAt(liX + 0.5f, liRow + 0.5f, lfAll);
                    bool lbOursClear = lOUw2.IsClearStrip(liRow);

                    if (liOurs != liOriginal || lbOursClear != lbOriginalClear)
                        liDiffer++;
                    else if (lbOursClear)
                        liClear++;
                    else if (liOurs >= 0)
                        liRunes++;
                }
            }

            fExpectInt("uw2 cells: every pixel of the region as mous_in_rune", liDiffer, 0);
            fExpectInt("uw2 cells: the clear strip's pixels (76 x 18)", liClear, 76 * 18);
            fExpectBool("uw2 cells: every row above it a rune or the region's odd last columns", liRunes > 0, true);
            fExpectInt("uw2 cell: off the region nothing", lOUw2.RuneCellAt(4.9f, 10f, lfAll), -1);
        }

        /// <summary>CanCast: the bag first, then lookup, circle and mana as player_cast.</summary>
        private static void fCheckCanCast(UWPlayerData pOBag)
        {
            int liCircleTwo = -1;

            foreach (UWRunicMagic.Spell lOSpell in UWRunicMagic.AllSpells)
            {
                if (lOSpell.Level == 2 && lOSpell.RuneSequence != UWRunicMagic.EmptySequence)
                {
                    liCircleTwo = lOSpell.RuneSequence;
                    break;
                }
            }

            int liNoSpell = UWRuneShelfModel.SequenceOf(new[] { 0 });

            fExpectInt("circle: level 1", UWSpellCasting.CircleOf(1), 1);
            fExpectInt("circle: level 2", UWSpellCasting.CircleOf(2), 1);
            fExpectInt("circle: level 3", UWSpellCasting.CircleOf(3), 2);
            fExpectInt("circle: level 16", UWSpellCasting.CircleOf(16), 8);

            fExpectInt("can cast: In Lor at level 1 with 3 mana",
                (int)UWSpellCasting.CanCast(8568, 1, 3, pOBag), (int)UWSpellCasting.CastCheckEnum.Ok);
            fExpectInt("can cast: 2 mana is too little",
                (int)UWSpellCasting.CanCast(8568, 1, 2, pOBag), (int)UWSpellCasting.CastCheckEnum.NotEnoughMana);
            fExpectInt("can cast: a circle-2 spell at level 2",
                (int)UWSpellCasting.CanCast(liCircleTwo, 2, 99, null), (int)UWSpellCasting.CastCheckEnum.NotExperienced);
            fExpectInt("can cast: ... at level 3",
                (int)UWSpellCasting.CanCast(liCircleTwo, 3, 99, null), (int)UWSpellCasting.CastCheckEnum.Ok);
            fExpectInt("can cast: An alone is no spell",
                (int)UWSpellCasting.CanCast(liNoSpell, 16, 99, null), (int)UWSpellCasting.CastCheckEnum.NotASpell);
            fExpectInt("can cast: a rune not in the bag comes first",
                (int)UWSpellCasting.CanCast(liNoSpell, 1, 0, pOBag), (int)UWSpellCasting.CastCheckEnum.RuneMissing);
            fExpectInt("can cast: without a bag the runes are not asked",
                (int)UWSpellCasting.CanCast(8568, 1, 3, null), (int)UWSpellCasting.CastCheckEnum.Ok);
        }

        private static string fRunes(IReadOnlyList<int> pORunes)
        {
            return string.Join(" ", pORunes);
        }
    }
}
