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
	///   the 90 degree view) add to the counter - visible ones never do.
	///   A visible cell is DISCOVERED only while it is bright enough: the renderer's grid
	///   carries a shade level per cell (SomethingToDoWithShadeCalcs_seg031_4AB, from
	///   SHADES.DAT of the light level), and DBC writes the full value only below 8
	///   (DiscoverShadeLimit). In the dark that is the player's own tile alone, with a torch
	///   the ring at distance 1, with the strongest light spells distance 2 - measured against
	///   the original on 2026-09-23, see EvaluateRenderBand.
	///   The row behind the player (33 cells) is marked the same way, without experience.
	///   seg017_1FDD_C5: EXPChange(counter * dungeon level / 10), truncated per frame, only for
	///   character levels 1 to 15 and not on level 9.
	///
	/// APPROXIMATED: visibility and the depth come from a grid line of sight inside a 90 degree
	/// cone instead of the original's clipped edge rays (seg031_115E, table 4A5). READ 2026-09-26:
	/// the original sweeps spans row by row between two edge rays at about 45 degrees either side,
	/// cast from the player's FINE position and laid anew through a tile corner wherever a wall
	/// pushes an edge inwards (seg031_AF1); every cell between the edges is classified by
	/// seg031_6CB. Ours looks from the tile centre along one line per cell, so it can miss a cell
	/// seen only in part and, through the rounding of that line, pass a tile the true line
	/// crosses. Taken over exactly: the classification's "not drawn" (ShowsOnlyItsBack). Since 2026-09-26
	/// a diagonal tile passes the sight only on its open half and a corner closed by two solid
	/// tiles stops it (IsCellVisible); since 2026-10-01 a diagonal closes a corner that lies on its
	/// wall, and a row only counts for the depth with a cell the view enters through its open half
	/// (GetRenderBandDepth). STILL OPEN (Todo.md): the original's own sweep - it also discovers the
	/// neighbours of the player's tile in his own row where the view reaches them. The band only
	/// changes with tile, direction or depth, so it is evaluated then - a repeated frame adds
	/// nothing in the original either, the bytes are no longer empty.
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

		/// <summary>How many rows the view reaches: until no cell of a row is visible any more,
		/// capped by piMaxDepth - the viewing distance of the light level, at most
		/// RenderBandMaxDepth.
		///
		/// A ROW COUNTS ONLY WITH A CELL THE VIEW CAN ENTER (per user, 2026-10-01, measured on
		/// level 6 at 5/54 facing north, original and ours from the same save): ahead lies the
		/// diagonal 5/55, open to the north-east, which shows the player only its closed back,
		/// beside it rock and the corner to 6/55 closed by rock and the diagonal's wall. The
		/// original wrote nothing beyond the player's own row - its span sweep closes there -
		/// while ours took the diagonal as an open cell, went on two rows and discovered the
		/// secret passage behind it. A cell reached through its closed half may still be drawn
		/// inside the band (table 4A5 draws such a diagonal on the view axis), it only does not
		/// carry the view into its row.</summary>
		public static int GetRenderBandDepth(UWLevel pOLevel, UWTilePos pOTile, UWTilePos pOForward, UWTilePos pORight, int piMaxDepth)
		{
			int liMax = Math.Max(0, Math.Min(RenderBandMaxDepth, piMaxDepth));
			int liDepth = 0;

			for (int liRow = 1; liRow <= liMax; liRow++)
			{
				bool lbAny = false;

				for (int liColumn = -liRow; liColumn <= liRow && !lbAny; liColumn++)
				{
					UWTilePos lOCell = pOTile + (pOForward * liRow) + (pORight * liColumn);

					lbAny = IsCellVisible(pOLevel, pOTile, lOCell, liRow, liColumn)
						&& fEntersOpenHalf(pOLevel, pOTile, lOCell);
				}

				if (!lbAny)
					break;

				liDepth = liRow;
			}

			return liDepth;
		}

		/// <summary>
		/// Walks the band and does the two things seg017_1FDD_DBC does per cell.
		///
		/// A CELL THE RENDERER REALLY DRAWS is DISCOVERED - the full value goes into the
		/// automap byte (label DFD onwards: the tile type with its terrain, written back at
		/// label 141A). A cell it does not draw - solid, behind a wall, outside the cone - only
		/// gets the undiscovered outline, and only while its byte is still empty, and THAT is
		/// what counts for the experience (label DD5). A visible cell never counts.
		///
		/// BUT ONLY WHERE IT IS BRIGHT ENOUGH. The first reading (2026-09-22) had the whole view
		/// discovered, as deep as the light reaches; four runs straight ahead from the start of
		/// level 1 (per user, 2026-09-23, original and ours, with and without a torch) showed the
		/// original revealing far less: in the dark only the tiles walked on, with a torch those
		/// and their open neighbours - the open room beside the end of the corridor stayed
		/// undiscovered although it is in plain view. The reason is the shade level of the
		/// renderer's grid: SomethingToDoWithShadeCalcs_seg031_4AB writes, for every cell, the
		/// shade of its whole-tile distance from the player (the root of column squared plus row
		/// squared) out of a table built from SHADES.DAT - with the distance doubled before the
		/// root, so UWShades.GetShadeTable with pbDiagonalStretch true - and 15 beyond the
		/// light's viewing distance. DBC discovers a visible cell only below DiscoverShadeLimit.
		/// A visible cell that is too dark gets the outline if its byte is empty, and never
		/// counts.
		///
		/// pyShadeTable is that table for the current light level; null treats every visible
		/// cell as bright (the state before 2026-09-23).
		///
		/// pFGetDisplayType tells what to draw in a discovered tile - water, lava, a door, a
		/// stair, a bridge - which only the host can know, because it takes the objects on the
		/// tile. Without it the band only outlines, as it did before.
		/// </summary>
		public static int EvaluateRenderBand(UWLevel pOLevel, UWTilePos pOTile, UWTilePos pOForward,
			UWTilePos pORight, int piDepth, byte[] pyShadeTable, Func<int, int, int> pFGetDisplayType = null)
		{
			if (pOLevel == null)
				return 0;

			int liCounter = 0;

			for (int liRow = -1; liRow <= piDepth; liRow++)
			{
				for (int liColumn = -RenderBandHalfWidth; liColumn <= RenderBandHalfWidth; liColumn++)
				{
					UWTilePos lOCell = pOTile + (pOForward * liRow) + (pORight * liColumn);

					if (liRow >= 0 && IsCellVisible(pOLevel, pOTile, lOCell, liRow, liColumn)
						&& !ShowsOnlyItsBack(pOLevel.GetTile(lOCell.X, lOCell.Y), pOForward, pORight, liColumn))
					{
						if (pFGetDisplayType != null && IsBrightEnoughToDiscover(pyShadeTable, liRow, liColumn))
							pOLevel.MarkTileVisited(lOCell.X, lOCell.Y, pFGetDisplayType(lOCell.X, lOCell.Y));
						else
							pOLevel.MarkTileSeen(lOCell.X, lOCell.Y);

						continue;
					}

					if (!pOLevel.MarkTileSeen(lOCell.X, lOCell.Y))
						continue;

					if (liRow >= 0)
						liCounter++;
				}
			}

			return liCounter;
		}

		/// <summary>The shade test of DBC for a cell of the band: the whole-tile distance from the
		/// player, as seg031_4AB takes it (UWVanillaMath.Sqrt of column squared plus row
		/// squared), looked up in the light level's table, below DiscoverShadeLimit. A missing
		/// table counts as bright.</summary>
		public static bool IsBrightEnoughToDiscover(byte[] pyShadeTable, int piRow, int piColumn)
		{
			if (pyShadeTable == null)
				return true;

			int liDistance = UWVanillaMath.Sqrt((piRow * piRow) + (piColumn * piColumn));
			int liShade = liDistance < pyShadeTable.Length ? pyShadeTable[liDistance] : 15;

			return liShade < DiscoverShadeLimit;
		}

		/// <summary>The exploration experience of one evaluation - truncated like UW.EXE, no
		/// remainder is carried. Zero for character levels outside 1 to 15.</summary>
		public static int GetExperience(int piCounter, int piDungeonLevel, int piCharacterLevel)
		{
			if (piCounter <= 0 || piCharacterLevel < 1 || piCharacterLevel >= ExplorationMaxCharacterLevel)
				return 0;

			return (piCounter * piDungeonLevel) / 10;
		}

		/// <summary>A cell the renderer would draw: open, inside the 90 degree view and with a
		/// clear grid line of sight from the player's tile.</summary>
		public static bool IsCellVisible(UWLevel pOLevel, UWTilePos pOTile, UWTilePos pOCell, int piRow, int piColumn)
		{
			if (piRow < 0 || Math.Abs(piColumn) > piRow || !fIsOpenTile(pOLevel, pOCell.X, pOCell.Y))
				return false;

			int liSteps = Math.Max(Math.Abs(pOCell.X - pOTile.X), Math.Abs(pOCell.Y - pOTile.Y));
			int liPreviousX = pOTile.X;
			int liPreviousY = pOTile.Y;

			// OUT OF THE PLAYER'S OWN TILE ONLY THROUGH ITS OPEN HALF (per user, 2026-09-30: on
			// level 2, 46/2, a diagonal open to the north-west, the water on 46/1 south of it was
			// revealed; in the original it is not - the diagonal's wall stands between). The ray
			// leaves the tile where it crosses the tile's edge; that point must lie on the open
			// half, as for every tile it passes further on.
			if (liSteps > 0)
			{
				float lfExitU = 0.5f + ((pOCell.X - pOTile.X) * 0.5f / liSteps);
				float lfExitV = 0.5f + ((pOCell.Y - pOTile.Y) * 0.5f / liSteps);
				UWTile lOOwn = pOLevel.GetTile(pOTile.X, pOTile.Y);

				if (lOOwn != null && !fIsInOpenHalf(lOOwn.TileType, lfExitU, lfExitV))
					return false;
			}

			for (int liStep = 1; liStep <= liSteps; liStep++)
			{
				float lfT = liStep / (float)liSteps;
				float lfX = pOTile.X + ((pOCell.X - pOTile.X) * lfT);
				float lfY = pOTile.Y + ((pOCell.Y - pOTile.Y) * lfT);
				int liX = UWUnits.RoundToInt(lfX);
				int liY = UWUnits.RoundToInt(lfY);

				// THROUGH A CORNER ONLY IF IT IS NOT CLOSED (per user, 2026-09-26: "everywhere one
				// too far diagonally"): a step that changes both coordinates passes the corner the
				// four tiles share, and where the two tiles beside it are both solid their walls meet
				// there. Until then the cell diagonally ahead of a corridor bend counted as seen -
				// the last step was never checked at all. One open side is enough, the view then
				// grazes the wall.
				if (liX != liPreviousX && liY != liPreviousY)
				{
					int liCornerX = Math.Max(liX, liPreviousX);
					int liCornerY = Math.Max(liY, liPreviousY);

					if (!fIsOpenAtCorner(pOLevel, liX, liPreviousY, liCornerX, liCornerY)
						&& !fIsOpenAtCorner(pOLevel, liPreviousX, liY, liCornerX, liCornerY))
						return false;
				}

				liPreviousX = liX;
				liPreviousY = liY;

				// The cell itself is open (tested above); the ones in between must be too.
				if (liStep == liSteps)
					break;

				if (!fIsOpenTile(pOLevel, liX, liY))
					return false;

				// Where along the ray it crosses the tile, 0..1 from its south-west corner.
				if (!fIsInOpenHalf(pOLevel.GetTile(liX, liY).TileType, lfX - liX + 0.5f, lfY - liY + 0.5f))
					return false;
			}

			return true;
		}

		/// <summary>
		/// A DIAGONAL THAT TURNS ITS BACK IS NOT DRAWN (read 2026-09-26 after the user's "we reveal
		/// too much behind a wall"). The renderer classifies every cell of the band by table 4A5 of
		/// seg031_6CB: seven entries per tile type - the type first turned into the view's frame by
		/// table 464 - for the cell's side of the view axis (0 on it, odd left, even right; more
		/// for steep and exactly diagonal cells). Every non-zero entry carries bit 0x80, the "drawn"
		/// bit seg017_1FDD_DBC tests. Zero stand only at solid rock and at the two diagonals whose
		/// open half points AHEAD: the one open ahead-right on every right-hand entry, the one open
		/// ahead-left on every left-hand entry. Such a tile shows the viewer only its solid back, so
		/// it is never discovered from there - it gets the outline and counts for the experience
		/// like any hidden cell. On the view axis itself it is drawn.
		/// </summary>
		public static bool ShowsOnlyItsBack(UWTile pOTile, UWTilePos pOForward, UWTilePos pORight, int piColumn)
		{
			if (pOTile == null || piColumn == 0)
				return false;

			int liOpenX;
			int liOpenY;

			switch (pOTile.TileType)
			{
				case UWTile.TileTypeEnum.diagonal_ne: liOpenX = 1; liOpenY = 1; break;
				case UWTile.TileTypeEnum.diagonal_nw: liOpenX = -1; liOpenY = 1; break;
				case UWTile.TileTypeEnum.diagonal_se: liOpenX = 1; liOpenY = -1; break;
				case UWTile.TileTypeEnum.diagonal_sw: liOpenX = -1; liOpenY = -1; break;
				default: return false;
			}

			// Where the open half points in the view's frame (y counts north).
			int liAhead = (liOpenX * pOForward.X) + (liOpenY * pOForward.Y);
			int liAside = (liOpenX * pORight.X) + (liOpenY * pORight.Y);

			return liAhead > 0 && Math.Sign(liAside) == Math.Sign(piColumn);
		}

		/// <summary>
		/// A DIAGONAL TILE LETS THE SIGHT THROUGH ONLY ON ITS OPEN HALF (per user with a picture,
		/// 2026-09-26): on level 1 at 29..34 / 29..34 a chamber sits inside a ring of solid tiles
		/// whose four corners are diagonals, open towards the corridor outside, and the chamber's
		/// own corners are diagonals open towards the inside. Walking round the corridor revealed
		/// the chamber, because the grid line of sight took every diagonal as a whole open tile and
		/// looked straight through both corners. Now the ray's point in the tile must lie on the
		/// open half, strictly - on the diagonal itself it is the wall. The halves are those of
		/// UWTileQueries.IsSubTileInOpenHalf; y counts north.
		/// </summary>
		private static bool fIsInOpenHalf(UWTile.TileTypeEnum peType, float pfU, float pfV)
		{
			switch (peType)
			{
				case UWTile.TileTypeEnum.diagonal_se: return pfV < pfU;
				case UWTile.TileTypeEnum.diagonal_nw: return pfV > pfU;
				case UWTile.TileTypeEnum.diagonal_ne: return pfU + pfV > 1f;
				case UWTile.TileTypeEnum.diagonal_sw: return pfU + pfV < 1f;
				default: return true;
			}
		}

		/// <summary>
		/// Whether a tile beside a corner the view passes leaves that corner open. A diagonal
		/// counts as open only at the one corner inside its open half - the two on its diagonal
		/// are its wall (per user, 2026-10-01: on level 6 the corner from 5/54 to 6/55 lies on
		/// the wall of the diagonal 5/55 and beside rock, so it is closed; until then a diagonal
		/// counted as open at every corner). piCornerX/Y is the corner in tile units, the tile's
		/// own south-west corner being its X/Y.
		/// </summary>
		private static bool fIsOpenAtCorner(UWLevel pOLevel, int piTileX, int piTileY, int piCornerX, int piCornerY)
		{
			UWTile lOTile = pOLevel?.GetTile(piTileX, piTileY);

			if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
				return false;

			return fIsInOpenHalf(lOTile.TileType, piCornerX - piTileX, piCornerY - piTileY);
		}

		/// <summary>Whether the line from the player's tile enters the cell through its open half -
		/// the point where it crosses the cell's edge, as for the player's own tile on the way
		/// out (IsCellVisible). Strict: on a diagonal's wall it does not enter.</summary>
		private static bool fEntersOpenHalf(UWLevel pOLevel, UWTilePos pOTile, UWTilePos pOCell)
		{
			int liSteps = Math.Max(Math.Abs(pOCell.X - pOTile.X), Math.Abs(pOCell.Y - pOTile.Y));
			UWTile lOCell = pOLevel?.GetTile(pOCell.X, pOCell.Y);

			if (liSteps == 0 || lOCell == null)
				return true;

			float lfEntryU = 0.5f - ((pOCell.X - pOTile.X) * 0.5f / liSteps);
			float lfEntryV = 0.5f - ((pOCell.Y - pOTile.Y) * 0.5f / liSteps);

			return fIsInOpenHalf(lOCell.TileType, lfEntryU, lfEntryV);
		}

		private static bool fIsOpenTile(UWLevel pOLevel, int piTileX, int piTileY)
		{
			UWTile lOTile = pOLevel?.GetTile(piTileX, piTileY);

			return lOTile != null && lOTile.TileType != UWTile.TileTypeEnum.solid;
		}
	}
}
