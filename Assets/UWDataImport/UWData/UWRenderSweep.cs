using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ORIGINAL'S VISIBILITY SWEEP (seg031_121A: seg031_5B4, seg031_115E with seg031_C99,
	/// seg031_1010, seg031_AF1 and the classification seg031_6CB; ported 2026-10-03, per user:
	/// "let us start with the automap"). The renderer decides every frame which cells of a grid
	/// ahead of the player it draws, and the automap takes exactly that (seg017_1FDD_DBC, see
	/// UWExplorationRules.EvaluateSweep).
	///
	/// THE GRID: 17 rows ahead (row 0 holds the player) times 33 columns (column 0 is the view
	/// axis, positive to the right), turned to the nearest of the four directions
	/// (PositionCamera_seg031_396: quadrant ((heading &gt;&gt; 13) + 1) &amp; 7 &gt;&gt; 1). The player's fine
	/// position inside the tile is turned into that frame too, and the heading becomes relative
	/// to the quadrant. Per cell: the "drawn" flags of table 4A5 and the shade level of
	/// seg031_4AB (the light level's table at the whole-tile distance, 15 beyond the light).
	///
	/// TWO EDGES, ONE SPAN: seg031_5B4 starts a left and a right edge in the player's cell, with
	/// rays at the relative heading -0x2040 and +0x2040 (the sine table, see seg019_A38 in UWMotionTables, shifted
	/// right by 4). Row by row, seg031_C99 steps each edge sideways along its ray until the ray
	/// leaves the row at its far side, and stops it at a side wall (TileTraverseFlags 1D8A) or
	/// where the light ends. seg031_1010 classifies every cell between the two edges, slides an
	/// edge along a wall that closes the view ahead, and seg031_AF1 carries the span into the next
	/// row, moving each edge inwards past cells beyond the light and laying its ray anew through
	/// the corner it now stands at, with a small inset (|dx| / 50 + 2). The list holds one span
	/// only - nothing in the code splits one - so a pillar inside the view is not drawn itself
	/// but hides nothing behind it; only the span's two edges are clipped. The sweep ends when
	/// the edges cross, the light ends across the span, or after row 16.
	///
	/// THE TILE MAP IS ADDRESSED LINEARLY (y * 64 + x): a column beyond the map's east or west
	/// edge lands in the next or previous row, as in UW.EXE. Tiles outside the 4096 are taken as
	/// solid here (the original reads whatever memory lies there).
	///
	/// Not taken over: the faces the classification drops (0x10/0x20/0x08) and the height steps it
	/// marks - they only steer the drawing, never the "drawn" bit. The grid starts empty every
	/// sweep; the original's keeps the previous frame's values in cells no step reaches, which can
	/// only happen in row 16.
	/// </summary>
	public sealed class UWRenderSweep
	{
		public const int Rows = 17;

		public const int Columns = 33;

		public const int HalfWidth = 16;

		/// <summary>The shade level of a cell beyond the light.</summary>
		public const int ShadeBeyondLight = 15;

		/// <summary>The marker of an empty list (low nibble 0xF).</summary>
		private const int EndOfList = 0xF;

		/// <summary>Padding around the grid, so pointer steps past its edges read nothing.</summary>
		private const int GridPadding = Columns * 2;

		/// <summary>Table 464: the tile type turned into the view frame, per quadrant.</summary>
		private static readonly int[,] msViewTileType =
		{
			{ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
			{ 0, 1, 4, 2, 5, 3, 9, 8, 6, 7 },
			{ 0, 1, 5, 4, 3, 2, 7, 6, 9, 8 },
			{ 0, 1, 3, 5, 2, 4, 8, 9, 7, 6 }
		};

		/// <summary>TileTraverseFlags 1D8A per view tile type: 0x10 far wall, 8 near wall, 4 right
		/// wall, 2 left wall, 1 diagonal, 0x20 slope.</summary>
		private static readonly int[] msTraverseFlags = { 0x1E, 0x00, 0x13, 0x15, 0x0B, 0x0D, 0x20, 0x20, 0x20, 0x20 };

		private const int FarWall = 0x10;

		private const int NearWall = 8;

		private const int Diagonal = 1;

		/// <summary>Table 539: the own side wall in the direction of a step (index 0 left, 1 right).</summary>
		private static readonly int[] msSideWall = { 2, 4 };

		/// <summary>Table 53D: the diagonal whose wall a step meets at the row's far side.</summary>
		private static readonly int[] msDiagonalAhead = { 2, 3 };

		/// <summary>Table 541: the column step of a direction.</summary>
		private static readonly int[] msStep = { -1, 1 };

		/// <summary>Table 535.</summary>
		private static readonly int[] msFarWallOf = { FarWall, 0 };

		/// <summary>Tables 444 and 446 as linear tile steps: right and ahead per quadrant.</summary>
		private static readonly int[] msRightStep = { 1, -64, -1, 64 };

		private static readonly int[] msAheadStep = { 64, 1, -64, -1 };

		/// <summary>The sine table of seg063:4E0, 256 steps to the circle plus a quarter for the
		/// cosine: round(32768 * sin), 32767 at the top - checked against UW.EXE entry by entry.</summary>

		/// <summary>One edge record (0x11 bytes at 0x2D00).</summary>
		private sealed class Edge
		{
			public int Next;

			public bool IsLeft;

			public int Dx;

			public int Dy;

			public int Column;

			public int FineX;

			public int Row;

			public int FineY;

			public int Tile;

			public int Cell;

			public int Saved;

			public Edge Clone()
			{
				return (Edge)MemberwiseClone();
			}
		}

		private readonly Edge[] mOEdges = { new Edge(), new Edge() };

		private int miHead;

		private readonly byte[] myFlags = new byte[(Rows * Columns) + (GridPadding * 2)];

		private readonly byte[] myShade = new byte[(Rows * Columns) + (GridPadding * 2)];

		private UWTile[] mOTiles;

		private int miQuadrant;

		private int miCameraX;

		private int miCameraY;

		/// <summary>The last row the sweep built (dseg 2CF2) - StartRendering walks rows 0 to this.</summary>
		public int Depth { get; private set; }

		/// <summary>The view quadrant of the last sweep: 0 north, 1 east, 2 south, 3 west.</summary>
		public int Quadrant
		{
			get { return miQuadrant; }
		}

		/// <summary>The linear tile index (y * 64 + x) the last sweep started from.</summary>
		public int PlayerTileIndex { get; private set; }

		public int RightStep
		{
			get { return msRightStep[miQuadrant]; }
		}

		public int AheadStep
		{
			get { return msAheadStep[miQuadrant]; }
		}

		/// <summary>Whether the renderer draws the cell (bit 0x80 of its flags).</summary>
		public bool IsDrawn(int piRow, int piColumn)
		{
			return (fFlags(fCellIndex(piRow, piColumn)) & 0x80) != 0;
		}

		/// <summary>The shade level of the cell, 15 beyond the light.</summary>
		public int GetShade(int piRow, int piColumn)
		{
			return fShade(fCellIndex(piRow, piColumn));
		}

		/// <summary>
		/// One sweep. piX/piY: the player's position in the original's units (tile &lt;&lt; 8 plus
		/// the fine position, UWUnits.WorldAxisToOriginal); piHeading: 0 north, clockwise, a full
		/// circle in 16 bits; pyShadeTable: the light level's table with the doubled distance
		/// (UWShades.GetShadeTable(level, true)), null for full light.
		/// </summary>
		public void Run(UWLevel pOLevel, int piX, int piY, int piHeading, byte[] pyShadeTable)
		{
			mOTiles = pOLevel != null ? pOLevel.TileData : null;

			int liHeading = piHeading & 0xFFFF;

			miQuadrant = (((liHeading >> 13) + 1) & 7) >> 1;

			int liFineX = piX & 0xFF;
			int liFineY = piY & 0xFF;

			switch (miQuadrant)
			{
				case 1: miCameraX = 0xFF - liFineY; miCameraY = liFineX; break;
				case 2: miCameraX = 0xFF - liFineX; miCameraY = 0xFF - liFineY; break;
				case 3: miCameraX = liFineY; miCameraY = 0xFF - liFineX; break;
				default: miCameraX = liFineX; miCameraY = liFineY; break;
			}

			int liRelative = (liHeading - (miQuadrant << 14)) & 0xFFFF;

			PlayerTileIndex = (((piY >> 8) & 0xFF) * 64) + ((piX >> 8) & 0xFF);

			fFillShades(pyShadeTable);
			Array.Clear(myFlags, 0, myFlags.Length);

			fStart(liRelative);
			fBuildRows();
		}

		// --- seg031_4AB: the shade level per cell ---------------------------------------------

		private void fFillShades(byte[] pyShadeTable)
		{
			for (int liRow = 0; liRow < Rows; liRow++)
			{
				for (int liColumn = -HalfWidth; liColumn <= HalfWidth; liColumn++)
				{
					int liShade = 0;

					if (pyShadeTable != null)
					{
						int liDistance = UWVanillaMath.Sqrt((liColumn * liColumn) + (liRow * liRow));

						liShade = liDistance < pyShadeTable.Length ? pyShadeTable[liDistance] : ShadeBeyondLight;
					}

					myShade[GridPadding + fCellIndex(liRow, liColumn)] = (byte)liShade;
				}
			}
		}

		// --- seg031_5B4: the two edges in the player's cell ----------------------------------

		private void fStart(int piRelativeHeading)
		{
			if (fTileType(PlayerTileIndex) == 0)
			{
				miHead = EndOfList;
				return;
			}

			miHead = 0;

			Edge lOLeft = mOEdges[0];
			Edge lORight = mOEdges[1];

			fPlace(lOLeft, true, 1);
			fPlace(lORight, false, EndOfList);

			fSinCos(piRelativeHeading - 0x2040, out lOLeft.Dx, out lOLeft.Dy);
			fSinCos(piRelativeHeading + 0x2040, out lORight.Dx, out lORight.Dy);

			lOLeft.Dx >>= 4;
			lOLeft.Dy >>= 4;
			lORight.Dx >>= 4;
			lORight.Dy >>= 4;
		}

		private void fPlace(Edge pOEdge, bool pbLeft, int piNext)
		{
			pOEdge.Next = piNext;
			pOEdge.IsLeft = pbLeft;
			pOEdge.Column = 0;
			pOEdge.Row = 0;
			pOEdge.FineX = miCameraX;
			pOEdge.FineY = miCameraY;
			pOEdge.Tile = PlayerTileIndex;
			pOEdge.Cell = fCellIndex(0, 0);
			pOEdge.Saved = 0;
			pOEdge.Dx = 0;
			pOEdge.Dy = 0;
		}

		// --- seg031_115E: row by row ------------------------------------------------------------

		private void fBuildRows()
		{
			Depth = -1;

			int liRowCell = 0;

			do
			{
				Depth++;

				for (int liAt = miHead; (liAt & 0xF) != EndOfList; liAt = mOEdges[liAt & 0xF].Next)
					fStepEdge(mOEdges[liAt & 0xF]);

				// -1 stands for the list head, otherwise the record whose Next leads on.
				int liOwner = -1;
				int liRowEnd = liRowCell + Columns;

				while ((fNextOf(liOwner) & 0xF) != EndOfList)
				{
					while (mOEdges[fNextOf(liOwner) & 0xF].Saved > liRowCell)
					{
						fSetFlags(liRowCell, 0);
						liRowCell++;
					}

					fSpan(ref liOwner, ref liRowCell);
				}

				while (liRowCell < liRowEnd)
				{
					fSetFlags(liRowCell, 0);
					liRowCell++;
				}
			}
			while ((miHead & 0xF) != EndOfList);
		}

		private int fNextOf(int piOwner)
		{
			return piOwner < 0 ? miHead : mOEdges[piOwner].Next;
		}

		private void fSetNextOf(int piOwner, int piNext)
		{
			if (piOwner < 0)
				miHead = piNext;
			else
				mOEdges[piOwner].Next = piNext;
		}

		// --- seg031_C99: one edge along its ray, inside its row ---------------------------------

		private void fStepEdge(Edge pOEdge)
		{
			int liDir = pOEdge.Dx >= 0 ? 1 : 0;
			int liStep = msStep[liDir];
			int liBack = (liDir + 1) % 2;
			int liDistance = liDir == 1 ? 0x100 - pOEdge.FineX : pOEdge.FineX;

			// The edge moves into its span (a left edge to the right, a right edge to the left):
			// the span in this row starts where the edge stands now, and every cell it leaves is
			// classified. Moving outwards, the span reaches to where the edge ends.
			bool lbInwards = (liDir == 1) == pOEdge.IsLeft;

			if (lbInwards)
				pOEdge.Saved = pOEdge.Cell;

			bool lbCrossesSide = pOEdge.Dy == 0 || (pOEdge.Dx != 0 && fCrossesSideFirst(pOEdge, liStep, liDistance));

			// A ray with neither component cannot occur (the inset keeps a new ray off zero
			// sideways, and its forward part is zero only for a right edge in row 1, whose
			// sideways part is then at least 2); the guard only keeps the division safe.
			while (lbCrossesSide && pOEdge.Dx != 0)
			{
				int liType = fViewType(pOEdge.Tile);
				int liNeighbour = pOEdge.Tile + (liStep * msRightStep[miQuadrant]);

				if ((msTraverseFlags[liType] & msSideWall[liDir]) != 0
					|| (msTraverseFlags[fViewType(liNeighbour)] & msSideWall[liBack]) != 0)
				{
					// A side wall: the edge stays in this cell, at the row's far side.
					pOEdge.FineY = 0xFF;

					if ((msTraverseFlags[liType] & msSideWall[liDir]) == msSideWall[liDir]
						&& liType == msDiagonalAhead[liDir])
						pOEdge.FineX = liBack * 0xFF;
					else
						pOEdge.FineX = liDir * 0xFF;

					if (!lbInwards)
						pOEdge.Saved = pOEdge.Cell;

					return;
				}

				pOEdge.FineY = (pOEdge.FineY + (int)(((long)pOEdge.Dy * liDistance) / ((long)liStep * pOEdge.Dx))) & 0xFF;
				pOEdge.FineX = liBack * 0xFF;
				liDistance = 0x100;

				if (lbInwards)
					fClassify(pOEdge, 0, 0);

				fMoveSideways(pOEdge, liStep);

				if (fShade(pOEdge.Cell) == ShadeBeyondLight || Math.Abs(pOEdge.Column) > HalfWidth)
				{
					// Beyond the light or the grid: one back, at the row's far corner.
					fMoveSideways(pOEdge, msStep[liBack]);
					pOEdge.FineX = liDir * 0xFF;
					pOEdge.FineY = 0xFF;

					if (!lbInwards)
						pOEdge.Saved = pOEdge.Cell;

					return;
				}

				lbCrossesSide = pOEdge.Dy == 0 || fCrossesSideFirst(pOEdge, liStep, liDistance);
			}

			// The ray leaves the row at its far side inside this cell.
			long llAcross = ((long)liStep * pOEdge.Dx) * (0xFF - pOEdge.FineY);

			if (pOEdge.Dy != 0)
				pOEdge.FineX = (pOEdge.FineX + (liStep * (int)(llAcross / pOEdge.Dy))) & 0xFF;
			pOEdge.FineY = 0xFF;

			if (!lbInwards)
				pOEdge.Saved = pOEdge.Cell;
		}

		/// <summary>Whether the ray reaches the cell's side before the row's far side.</summary>
		private static bool fCrossesSideFirst(Edge pOEdge, int piStep, int piDistance)
		{
			long llSide = ((long)piStep * pOEdge.Dx) * (0x100 - pOEdge.FineY);
			long llFar = (long)piDistance * pOEdge.Dy;

			return llSide > llFar;
		}

		// --- seg031_1010: one span ----------------------------------------------------------

		private void fSpan(ref int piOwner, ref int piRowCell)
		{
			Edge lOLeft = mOEdges[fNextOf(piOwner) & 0xF];
			int liRightIndex = lOLeft.Next & 0xF;
			Edge lORight = mOEdges[liRightIndex];

			piRowCell = lORight.Saved + 1;

			while (fClassify(lOLeft, 1, NearWall))
			{
				if (((short)((lOLeft.Column << 8) + lOLeft.FineX)) > ((short)((lORight.Column << 8) + lORight.FineX)))
				{
					fUnlink(piOwner, lOLeft, lORight);
					return;
				}
			}

			if (lOLeft.Column < lORight.Column)
			{
				while (fClassify(lORight, -1, NearWall))
				{
				}
			}

			Edge lOCopy = lOLeft.Clone();

			if (!fAdvance(lOLeft, lORight))
			{
				fUnlink(piOwner, lOLeft, lORight);
				return;
			}

			piOwner = liRightIndex;

			if (lOLeft.Column == lORight.Column)
				return;

			// The cells between the edges, from the old left edge to the new right one - with
			// the two kinds of slide alternating, as the compiled loop has them.
			fMoveSideways(lOCopy, 1);

			while (lORight.Column > lOCopy.Column)
			{
				if (fClassify(lOCopy, 1, 0))
					continue;

				if (lORight.Column <= lOCopy.Column)
					break;

				fMoveSideways(lOCopy, 1);

				while (fClassify(lOCopy, 1, NearWall) && lORight.Column > lOCopy.Column)
				{
				}

				fMoveSideways(lOCopy, 1);
			}
		}

		private void fUnlink(int piOwner, Edge pOLeft, Edge pORight)
		{
			fSetNextOf(piOwner, (fNextOf(piOwner) & 0xF0) + (pORight.Next & 0xF));
			pOLeft.Next = 0;
			pOLeft.IsLeft = false;
			pORight.Next = 0;
			pORight.IsLeft = false;
		}

		// --- seg031_AF1: the span into the next row -----------------------------------------

		private bool fAdvance(Edge pOLeft, Edge pORight)
		{
			pOLeft.Row = (pOLeft.Row + 1) & 0xFF;

			if ((sbyte)pOLeft.Row > HalfWidth)
				return false;

			while (fShade(pOLeft.Cell + Columns) == ShadeBeyondLight)
			{
				fMoveSideways(pOLeft, 1);
				pOLeft.FineX = 0;
				fClassify(pOLeft, 0, 0);

				if (pOLeft.Column > pORight.Column)
					return false;
			}

			if (fRelays(pOLeft))
			{
				int liDx = (short)((pOLeft.Column << 8) + pOLeft.FineX - miCameraX);

				pOLeft.Dx = (short)(liDx - ((Math.Abs(liDx) / 50) + 2));
				pOLeft.Dy = (short)(((sbyte)pOLeft.Row << 8) - miCameraY);
			}

			pOLeft.Tile += msAheadStep[miQuadrant];
			pOLeft.Cell += Columns;
			pOLeft.FineY = 0;

			pORight.Row = (pORight.Row + 1) & 0xFF;

			while (fShade(pORight.Cell + Columns) == ShadeBeyondLight)
			{
				fMoveSideways(pORight, -1);
				pORight.FineX = 0xFF;
				fClassify(pORight, 0, 0);

				if (pOLeft.Column > pORight.Column)
					return false;
			}

			if (fRelays(pORight))
			{
				int liDx = (short)((pORight.Column << 8) + pORight.FineX - miCameraX);

				pORight.Dx = (short)(liDx + ((Math.Abs(liDx) / 50) + 2));
				pORight.Dy = (short)(((sbyte)pORight.Row << 8) - miCameraY - 1);
			}

			pORight.FineY = 0;
			pORight.Cell += Columns;
			pORight.Tile += msAheadStep[miQuadrant];

			return true;
		}

		/// <summary>An edge gets a new ray from row 2 on, or where it stands more than 16 fine
		/// units (Manhattan) from the player.</summary>
		private bool fRelays(Edge pOEdge)
		{
			return (sbyte)pOEdge.Row > 1
				|| Math.Abs(pOEdge.FineX - miCameraX) + Math.Abs(pOEdge.FineY - miCameraY) > 0x10;
		}

		// --- seg031_67F / seg031_6A5 -----------------------------------------------------------

		private void fMoveSideways(Edge pOEdge, int piStep)
		{
			pOEdge.Column = (sbyte)(pOEdge.Column + piStep);
			pOEdge.Tile += piStep * msRightStep[miQuadrant];
			pOEdge.Cell += piStep;
		}

		// --- seg031_6CB: classify the edge's cell, maybe slide along a wall ------------------

		/// <summary>Writes the cell's flags; with piSlide (+1 or -1) moves the edge one column
		/// along a wall and returns true where the tiles say so. piAheadWall is 8 or 0: whether
		/// the slide is for a closed or an open tile ahead.</summary>
		private bool fClassify(Edge pOEdge, int piSlide, int piAheadWall)
		{
			int liType = fViewType(pOEdge.Tile);
			int liClass = 0;

			if (pOEdge.Column != 0)
			{
				int liColumn = Math.Abs((int)(sbyte)pOEdge.Column);
				int liRow = Math.Abs((int)(sbyte)pOEdge.Row);

				liClass = pOEdge.Column > 0 ? 2 : 1;

				if (liColumn > liRow)
					liClass += 2;

				if (liColumn == liRow)
					liClass += 4;
			}

			if (!fIsDrawnType(liType, liClass))
			{
				fSetFlags(pOEdge.Cell, 0);
				return false;
			}

			fSetFlags(pOEdge.Cell, 0x80);

			if (piSlide == 0)
				return false;

			int liOwn = msTraverseFlags[liType];
			int liAhead = msTraverseFlags[fViewType(pOEdge.Tile + msAheadStep[miQuadrant])] & NearWall;
			bool lbSlide = liAhead == piAheadWall && msFarWallOf[piAheadWall == NearWall ? 1 : 0] == (liOwn & FarWall);

			if (!lbSlide)
			{
				if ((liOwn & Diagonal) != Diagonal)
					return false;

				if (msFarWallOf[piAheadWall == 0 ? 1 : 0] != (liOwn & FarWall))
					return false;
			}

			if (piSlide == 1)
			{
				fMoveSideways(pOEdge, 1);
				pOEdge.FineX = 0;
			}
			else
			{
				fMoveSideways(pOEdge, -1);
				pOEdge.FineX = 0xFF;
			}

			return true;
		}

		/// <summary>Table 4A5, zero or not: solid rock is never drawn, and the two diagonals whose
		/// open half points ahead are not drawn on the side they open to (classes 2, 4, 6 right,
		/// 1, 3, 5 left) - they show only their back.</summary>
		private static bool fIsDrawnType(int piViewType, int piClass)
		{
			switch (piViewType)
			{
				case 0: return false;
				case 4: return piClass == 0 || (piClass & 1) != 0;
				case 5: return piClass == 0 || (piClass & 1) == 0;
				default: return true;
			}
		}

		// --- helpers ---------------------------------------------------------------------------

		private static int fCellIndex(int piRow, int piColumn)
		{
			return (piRow * Columns) + piColumn + HalfWidth;
		}

		private int fFlags(int piCell)
		{
			int liAt = piCell + GridPadding;

			return liAt >= 0 && liAt < myFlags.Length ? myFlags[liAt] : 0;
		}

		private void fSetFlags(int piCell, int piFlags)
		{
			int liAt = piCell + GridPadding;

			if (liAt >= 0 && liAt < myFlags.Length)
				myFlags[liAt] = (byte)piFlags;
		}

		private int fShade(int piCell)
		{
			int liAt = piCell + GridPadding;

			return liAt >= 0 && liAt < myShade.Length ? myShade[liAt] : 0;
		}

		private int fTileType(int piTile)
		{
			if (mOTiles == null || piTile < 0 || piTile >= mOTiles.Length || mOTiles[piTile] == null)
				return 0;

			return (int)mOTiles[piTile].TileType;
		}

		private int fViewType(int piTile)
		{
			return msViewTileType[miQuadrant, fTileType(piTile)];
		}

		/// <summary>The sine and cosine of a 16-bit angle, interpolated over the low byte - the
		/// binary's own table since 2026-10-05 (UWMotionTables.SinCos; until then a rounded sine
		/// scaled to 32768, which differs from it by one in most entries).</summary>
		private static void fSinCos(int piAngle, out int piSin, out int piCos)
		{
			UWMotionTables.SinCos(piAngle & 0xFFFF, out piSin, out piCos);
		}
	}
}
