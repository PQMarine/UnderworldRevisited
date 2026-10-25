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

            fExpectInt("grid: rune 5's left", UWRuneShelfModel.SlotLeft(5), 26);
            fExpectInt("grid: rune 5's top", UWRuneShelfModel.SlotTop(5), 20);

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
            fExpectInt("cell: the first compartment's corner", UWRuneShelfModel.RuneCellAt(8f, 5f, lfAll), 0);
            fExpectInt("cell: the region's top-left pixel", UWRuneShelfModel.RuneCellAt(4f, 3f, lfAll), 0);
            fExpectInt("cell: above the region nothing", UWRuneShelfModel.RuneCellAt(10f, 2.9f, lfAll), -1);
            fExpectInt("cell: left of the region nothing", UWRuneShelfModel.RuneCellAt(3.9f, 10f, lfAll), -1);
            fExpectInt("cell: the first column ends on x 24", UWRuneShelfModel.RuneCellAt(24.9f, 10f, lfAll), 0);
            fExpectInt("cell: the second begins on 25", UWRuneShelfModel.RuneCellAt(25f, 10f, lfAll), 1);
            fExpectInt("cell: the third begins on 43", UWRuneShelfModel.RuneCellAt(43f, 10f, lfAll), 2);
            fExpectInt("cell: the fourth begins on 61", UWRuneShelfModel.RuneCellAt(61f, 10f, lfAll), 3);
            fExpectInt("cell: ... and ends on 78", UWRuneShelfModel.RuneCellAt(78.9f, 10f, lfAll), 3);
            fExpectInt("cell: x 79 is column 4, the next row's first rune", UWRuneShelfModel.RuneCellAt(79f, 10f, lfAll), 4);
            fExpectInt("cell: ... below the last row nothing", UWRuneShelfModel.RuneCellAt(79f, 85f, lfAll), -1);
            fExpectInt("cell: right of the region nothing", UWRuneShelfModel.RuneCellAt(80f, 10f, lfAll), -1);
            fExpectInt("cell: the first row ends on row 17", UWRuneShelfModel.RuneCellAt(10f, 17.9f, lfAll), 0);
            fExpectInt("cell: the second begins on 18, the compartment's bottom row", UWRuneShelfModel.RuneCellAt(10f, 18f, lfAll), 4);
            fExpectInt("cell: the gap between two compartments", UWRuneShelfModel.RuneCellAt(23f, 19f, lfAll), 4);
            fExpectInt("cell: the last row begins on 78", UWRuneShelfModel.RuneCellAt(70f, 78f, lfAll), 23);
            fExpectInt("cell: ... and ends on 92", UWRuneShelfModel.RuneCellAt(70f, 92.9f, lfAll), 23);
            fExpectInt("cell: the clear strip is no cell", UWRuneShelfModel.RuneCellAt(70f, 93f, lfAll), -1);
            fExpectInt("cell: a rune not in the bag", UWRuneShelfModel.RuneCellAt(8f, 5f, lfNone), -1);
            fExpectInt("cell: ... leaves its cell to nobody", UWRuneShelfModel.RuneCellAt(10f, 17f, lfNotFirst), -1);

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
                    int liRune = UWRuneShelfModel.RuneCellAt(liX + 0.5f, liRow + 0.5f, lfAll);
                    bool lbClear = UWRuneShelfModel.IsClearStrip(liRow);

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
            fExpectBool("clear strip: row 93", UWRuneShelfModel.IsClearStrip(93f), true);
            fExpectBool("clear strip: row 110", UWRuneShelfModel.IsClearStrip(110f), true);
            fExpectBool("clear strip: above it", UWRuneShelfModel.IsClearStrip(92.9f), false);
            fExpectBool("clear strip: below it", UWRuneShelfModel.IsClearStrip(110.1f), false);
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
