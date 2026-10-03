using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The tile band the original renderer passes over every frame, and the exploration
	/// experience that comes from it - after UW.EXE (per user, 2026-09-14: a new character
	/// walking from the start to the wall got 2 EXP, turning around on the spot 1 more; the
	/// panel shows raw / 10). Engine-free since 2026-09-18 (P3 of the engine separation), out
	/// of UWPlayerTerrain, which keeps the view direction, the light level and the memo of
	/// the last band.
	///
	///   Grid (seg031_121A, StartRendering_seg017_526): 33 columns centred on the player times
	///   rows 0..D ahead, oriented along the view direction snapped to the nearest of four
	///   directions. D is the depth the view reaches: rows end where the view spans close
	///   against walls, and at most the viewing distance of the current light level
	///   (SHADES.DAT, 3 in the dark to 7) and 16.
	///   seg017_1FDD_DBC for every cell: a still empty automap byte gets the undiscovered
	///   marker of the tile type. Only cells that are NOT visible (solid, behind a wall, outside
	///   the view's span) add to the counter - drawn ones never do.
	///   A visible cell is DISCOVERED only while it is bright enough: the renderer's grid
	///   carries a shade level per cell (see UWRenderSweep for SomethingToDoWithShadeCalcs_seg031_4AB, from
	///   SHADES.DAT of the light level), and DBC writes the full value only below 8
	///   (DiscoverShadeLimit). In the dark that is the player's own tile alone, with a torch
	///   the ring at distance 1, with the strongest light spells distance 2 - measured against
	///   the original on 2026-09-23, see EvaluateSweep.
	///   The row behind the player (33 cells) is marked the same way, without experience.
	///   seg017_1FDD_C5: EXPChange(counter * dungeon level / 10), truncated per frame, only for
	///   character levels 1 to 15 and not on level 9.
	///
	/// WHICH CELLS ARE DRAWN comes from the original's own sweep since 2026-10-03 (UWRenderSweep,
	/// seg031_121A ported in full, per user: "let us start with the automap"). Until then a grid
	/// line of sight from the tile centre inside a 90 degree cone stood in for it (2026-09-22 to
	/// 2026-10-01, corrected step by step for diagonals, closed corners and the depth); the sweep
	/// casts its two edge rays from the player's FINE position and exact heading, clips them at
	/// tile corners and classifies every cell between them by table 4A5. Like the original it is
	/// run again whenever position, heading, light or the level's tiles change - a repeated sweep
	/// adds nothing, the bytes are no longer empty.
	/// </summary>
	public static class UWExplorationRules
	{
		/// <summary>Half the width of the render band: 33 columns, the player in column 16.</summary>
		public const int RenderBandHalfWidth = 16;

		/// <summary>Most rows the renderer builds ahead of the player.</summary>
		public const int RenderBandMaxDepth = 16;

		/// <summary>A visible cell is discovered only while its shade level is below this -
		/// the test after label E1B of seg017_1FDD_DBC. At 8 and above it keeps what it has, or
		/// gets the undiscovered outline if it had nothing, and counts no experience.</summary>
		public const int DiscoverShadeLimit = 8;

		/// <summary>Character levels from this one on get no exploration experience.</summary>
		public const int ExplorationMaxCharacterLevel = 16;

		/// <summary>The forward step of a view quadrant: 0 north, 1 east, 2 south, 3 west.</summary>
		public static UWTilePos ForwardOfQuadrant(int piQuadrant)
		{
			switch (piQuadrant & 3)
			{
				case 0: return new UWTilePos(0, 1);
				case 1: return new UWTilePos(1, 0);
				case 2: return new UWTilePos(0, -1);
				default: return new UWTilePos(-1, 0);
			}
		}

		/// <summary>The step to the right of a forward step.</summary>
		public static UWTilePos RightOf(UWTilePos pOForward)
		{
			return new UWTilePos(pOForward.Y, -pOForward.X);
		}

		/// <summary>
		/// Walks the band and does the two things seg017_1FDD_DBC does per cell.
		///
		/// A CELL THE RENDERER REALLY DRAWS is DISCOVERED - the full value goes into the
		/// automap byte (label DFD onwards: the tile type with its terrain, written back at
		/// label 141A). A cell it does not draw - solid, behind a wall, outside the span - only
		/// gets the undiscovered outline, and only while its byte is still empty, and THAT is
		/// what counts for the experience (label DD5). A visible cell never counts.
		///
		/// BUT ONLY WHERE IT IS BRIGHT ENOUGH. The first reading (2026-09-22) had the whole view
		/// discovered, as deep as the light reaches; four runs straight ahead from the start of
		/// level 1 (per user, 2026-09-23, original and ours, with and without a torch) showed the
		/// original revealing far less: in the dark only the tiles walked on, with a torch those
		/// and their open neighbours - the open room beside the end of the corridor stayed
		/// undiscovered although it is in plain view. The reason is the shade level of the
		/// renderer's grid (see UWRenderSweep): SomethingToDoWithShadeCalcs_seg031_4AB writes, for every cell, the
		/// shade of its whole-tile distance from the player (the root of column squared plus row
		/// squared) out of a table built from SHADES.DAT - with the distance doubled before the
		/// root, so UWShades.GetShadeTable with pbDiagonalStretch true - and 15 beyond the
		/// light's viewing distance. DBC discovers a visible cell only below DiscoverShadeLimit.
		/// A visible cell that is too dark gets the outline if its byte is empty, and never
		/// counts.
		///
		/// The cells, their "drawn" bit and their shade come from the sweep (UWRenderSweep.Run);
		/// rows 0 to its depth and all 33 columns are walked as StartRendering_seg017_526 walks them,
		/// then the row behind the player, which only gets outlines. The tile of a cell is found
		/// linearly, as UW.EXE finds it (y * 64 + x, any index outside the 4096 skipped): a column past
		/// the map's east or west edge lands in the neighbouring row.
		///
		/// pFGetDisplayType tells what to draw in a discovered tile - water, lava, a door, a
		/// stair, a bridge - which only the host can know, because it takes the objects on the
		/// tile. Without it the band only outlines.
		/// </summary>
		public static int EvaluateSweep(UWLevel pOLevel, UWRenderSweep pOSweep, Func<int, int, int> pFGetDisplayType = null)
		{
			if (pOLevel == null || pOSweep == null)
				return 0;

			int liCounter = 0;

			for (int liRow = -1; liRow <= pOSweep.Depth; liRow++)
			{
				for (int liColumn = -RenderBandHalfWidth; liColumn <= RenderBandHalfWidth; liColumn++)
				{
					int liIndex = pOSweep.PlayerTileIndex + (liRow * pOSweep.AheadStep) + (liColumn * pOSweep.RightStep);

					if ((liIndex & ~0xFFF) != 0)
						continue;

					int liX = liIndex & 63;
					int liY = liIndex >> 6;

					if (liRow >= 0 && pOSweep.IsDrawn(liRow, liColumn))
					{
						if (pFGetDisplayType != null && IsBrightEnoughToDiscover(pOSweep.GetShade(liRow, liColumn)))
							pOLevel.MarkTileVisited(liX, liY, pFGetDisplayType(liX, liY));
						else
							pOLevel.MarkTileSeen(liX, liY);

						continue;
					}

					if (!pOLevel.MarkTileSeen(liX, liY))
						continue;

					if (liRow >= 0)
						liCounter++;
				}
			}

			return liCounter;
		}

		/// <summary>The shade test of DBC for a drawn cell: its shade level in the sweep's grid
		/// (UWRenderSweep.GetShade) below DiscoverShadeLimit.</summary>
		public static bool IsBrightEnoughToDiscover(int piShade)
		{
			return piShade < DiscoverShadeLimit;
		}

		/// <summary>The exploration experience of one evaluation - truncated like UW.EXE, no
		/// remainder is carried. Zero for character levels outside 1 to 15.</summary>
		public static int GetExperience(int piCounter, int piDungeonLevel, int piCharacterLevel)
		{
			if (piCounter <= 0 || piCharacterLevel < 1 || piCharacterLevel >= ExplorationMaxCharacterLevel)
				return 0;

			return (piCounter * piDungeonLevel) / 10;
		}
	}
}
