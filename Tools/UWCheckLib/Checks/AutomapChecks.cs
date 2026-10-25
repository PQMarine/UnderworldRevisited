using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The automap: how far it discovers and how UW.EXE paints it.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// HOW FAR THE AUTOMAP DISCOVERS (UWExplorationRules.IsBrightEnoughToDiscover on the shade of
        /// UWRenderSweep's grid): a drawn cell only below shade 8 of the light level's table with the
        /// doubled distance. Measured against
        /// the original on 2026-09-23 (four runs from the start of level 1): in the dark only the
        /// tile walked on, with a torch (light level 2) also the ring at distance 1.
        /// </summary>
        private static void fCheckMapReveal(DataImport pOData)
        {
            Check.Group("Map reveal");

            byte[] lyDark = pOData.Shades != null ? pOData.Shades.GetShadeTable(0, true) : null;
            byte[] lyTorch = pOData.Shades != null ? pOData.Shades.GetShadeTable(2, true) : null;
            byte[] lyBright = pOData.Shades != null ? pOData.Shades.GetShadeTable(7, true) : null;

            if (lyDark == null || lyTorch == null || lyBright == null)
            {
                fFail("map reveal", "SHADES.DAT not loaded");
                return;
            }

            fExpectBool("map reveal: dark, the own tile", fIsBright(lyDark, 0, 0), true);
            fExpectBool("map reveal: dark, not the tile ahead", fIsBright(lyDark, 1, 0), false);
            fExpectBool("map reveal: torch, the tile ahead", fIsBright(lyTorch, 1, 0), true);
            fExpectBool("map reveal: torch, diagonal ahead", fIsBright(lyTorch, 1, 1), true);
            fExpectBool("map reveal: torch, not two ahead", fIsBright(lyTorch, 2, 0), false);
            fExpectBool("map reveal: torch, not two aside", fIsBright(lyTorch, 0, 2), false);
            fExpectBool("map reveal: brightest, two ahead", fIsBright(lyBright, 2, 0), true);
            fExpectBool("map reveal: brightest, not three ahead", fIsBright(lyBright, 3, 0), false);

            // THE ORIGINAL'S SWEEP (UWRenderSweep, seg031_121A). Positions in the original's units,
            // the tile centre 128/128.
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;
            UWLevel lOLevel6 = pOData.Levels != null && pOData.Levels.Count > 5 ? pOData.Levels[5] : null;

            if (lOLevel == null || lOLevel6 == null)
            {
                fFail("map reveal", "levels 1 and 6 not loaded");
                return;
            }

            UWRenderSweep lOSweep = new UWRenderSweep();

            // Level 6, 5/54 looking north with a lantern (per user, 2026-10-01, measured against
            // the original): ahead the diagonal 5/55 turns its closed back, so the view ends in the
            // own row - and in that row the original discovers the diagonal 4/54 beside the player,
            // whose open side faces him. The grid line of sight before the sweep missed it.
            lOSweep.Run(lOLevel6, (5 << 8) | 128, (54 << 8) | 128, 0, lyTorch);
            fExpectInt("map reveal: level 6, 5/54 north, the view ends in the own row", lOSweep.Depth, 0);
            fExpectBool("map reveal: level 6, 5/54 north, the own tile is drawn", lOSweep.IsDrawn(0, 0), true);
            fExpectBool("map reveal: level 6, 5/54 north, the diagonal 4/54 beside is drawn", lOSweep.IsDrawn(0, -1), true);
            fExpectBool("map reveal: level 6, 5/54 north, the rock 6/54 is not", lOSweep.IsDrawn(0, 1), false);

            // The walled chamber of level 1 (29..34 / 29..34): a solid ring with diagonal corners
            // open to the corridor, the chamber's own corners diagonals open to the inside. Seen
            // from the corridor corner 28/35 looking south in the brightest light (right is west,
            // so east is a negative column), nothing of the chamber may show (per user, 2026-09-26).
            lOSweep.Run(lOLevel, (28 << 8) | 128, (35 << 8) | 128, 0x8000, lyBright);
            fExpectBool("map reveal: level 1 chamber, the corridor ahead is drawn", lOSweep.IsDrawn(4, 0), true);
            fExpectBool("map reveal: level 1 chamber, the ring's diagonal corner is drawn", lOSweep.IsDrawn(1, -1), true);
            fExpectBool("map reveal: level 1 chamber, not the chamber's diagonal 30/33", lOSweep.IsDrawn(2, -2), false);
            fExpectBool("map reveal: level 1 chamber, not the chamber 31/32", lOSweep.IsDrawn(3, -3), false);
            fExpectBool("map reveal: level 1 chamber, not the chamber 30/31", lOSweep.IsDrawn(4, -2), false);

            // The dark run of 2026-09-23 (per user, original and ours from the start of level 1):
            // straight north from 32/2 to 32/10, the original discovered from y 6 on only x 32.
            // Replayed in steps of 4 fine units on an empty map, the sweep matches all 4096 bytes
            // of the original's save; here the corridor part of it.
            lOLevel.EnsureAutomap();

            byte[] lyKept = (byte[])lOLevel.AutomapTiles.Clone();

            Array.Clear(lOLevel.AutomapTiles, 0, lOLevel.AutomapTiles.Length);

            for (int liY = (2 << 8) | 128; liY <= ((10 << 8) | 191); liY += 4)
            {
                lOSweep.Run(lOLevel, (32 << 8) | 128, liY, 0, lyDark);
                UWExplorationRules.EvaluateSweep(lOLevel, lOSweep, (piX, piY) => 0);
            }

            string lsDiscovered = string.Empty;

            for (int liY = 6; liY <= 10; liY++)
            {
                for (int liX = 28; liX <= 36; liX++)
                {
                    byte lyByte = lOLevel.AutomapTiles[(liY * 64) + liX];

                    if (lyByte != 0 && (lyByte & 0xF) < 0xA)
                        lsDiscovered += liX + "/" + liY + " ";
                }
            }

            Array.Copy(lyKept, lOLevel.AutomapTiles, lyKept.Length);
            const string DarkRunExpected = "32/6 32/7 32/8 32/9 32/10";

            if (lsDiscovered.Trim() == DarkRunExpected)
                fPass("map reveal: the dark run discovers only the corridor walked", DarkRunExpected);
            else
                fFail("map reveal: the dark run discovers only the corridor walked",
                    "got " + lsDiscovered.Trim() + ", expected " + DarkRunExpected);
        }

        /// <summary>The shade test for one cell of the grid, the shades alone (no level: the sweep
        /// draws nothing, its shade grid is filled all the same).</summary>
        private static bool fIsBright(byte[] pyShadeTable, int piRow, int piColumn)
        {
            UWRenderSweep lOSweep = new UWRenderSweep();

            lOSweep.Run(null, 0, 0, 0, pyShadeTable);

            return UWExplorationRules.IsBrightEnoughToDiscover(lOSweep.GetShade(piRow, piColumn));
        }

        /// <summary>
        /// THE MAP IS PAINTED AS UW.EXE PAINTS IT (UWAutomapPainter, ovr092_1E5 and helpers):
        /// on palette indices, each pixel the parchment plus a step. With every random value at
        /// 16383: floor +3, a wall border or a diagonal's wall pixel +7, a border towards
        /// undiscovered open floor +2, a corner +4 per step. A tile's cell starts at (3x + 7, 3y + 4) from
        /// the bottom of the 320x200 screen.
        /// </summary>
        private static void fCheckMapPainter()
        {
            Check.Group("Map painting");

            // One open tile at 10/10, undiscovered open floor north of it, nothing elsewhere.
            byte[] lyAutomap = new byte[64 * 64];
            lyAutomap[(10 * 64) + 10] = 0x01;
            lyAutomap[(11 * 64) + 10] = 0x0B;

            byte[] lyScreen = fPaint(lyAutomap);

            fExpectInt("map paint: floor is parchment +2..4", fScreenAt(lyScreen, 38, 35), 3);
            fExpectInt("map paint: border towards undiscovered floor +0..3", fScreenAt(lyScreen, 38, 37), 2);
            fExpectInt("map paint: border towards a wall +6..7", fScreenAt(lyScreen, 40, 35), 7);
            // A lone tile: north+east AND east+south both step the north-east corner, so twice.
            fExpectInt("map paint: north-east corner stepped twice", fScreenAt(lyScreen, 40, 37), 8);
            fExpectInt("map paint: south-west corner stepped twice", fScreenAt(lyScreen, 36, 33), 8);
            fExpectInt("map paint: no south-east corner (the original's offset)", fScreenAt(lyScreen, 40, 33), 0);

            // Two diagonals side by side: open SE at 10/10, open NW at 11/10. No border between
            // them, and both solid halves stay parchment - no knob (per user, 2026-09-26).
            lyAutomap = new byte[64 * 64];
            lyAutomap[(10 * 64) + 10] = 0x02;
            lyAutomap[(10 * 64) + 11] = 0x05;
            lyScreen = fPaint(lyAutomap);

            fExpectInt("map paint: SE diagonal, wall pixel on the diagonal", fScreenAt(lyScreen, 37, 34), 7);
            fExpectInt("map paint: SE diagonal, its solid corner untouched", fScreenAt(lyScreen, 37, 36), 0);
            fExpectInt("map paint: NW diagonal, its solid corner untouched", fScreenAt(lyScreen, 42, 34), 0);
            fExpectInt("map paint: SE diagonal, no west border", fScreenAt(lyScreen, 36, 35), 0);
            fExpectInt("map paint: SE diagonal, south border", fScreenAt(lyScreen, 38, 33), 7);
        }

        private static byte[] fPaint(byte[] pyAutomap)
        {
            byte[] lyScreen = new byte[UWAutomapPainter.ScreenWidth * UWAutomapPainter.ScreenHeight];

            UWAutomapPainter.Paint(lyScreen, pyAutomap, (piX, piY, peUse) => 16383);

            return lyScreen;
        }

        /// <summary>A pixel of the painted screen, y from the bottom as the painter counts.</summary>
        private static int fScreenAt(byte[] pyScreen, int piX, int piY)
        {
            return pyScreen[((UWAutomapPainter.ScreenHeight - 1 - piY) * UWAutomapPainter.ScreenWidth) + piX];
        }
    }
}
