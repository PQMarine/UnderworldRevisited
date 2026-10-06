using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE CREATURE'S TILE TESTS of UW.EXE, engine-free (read 2026-10-05 for stage 2 of the
	/// motion rework): the straight tile line seg006_1477_1938 with its helper seg006_1477_2041,
	/// and the breadth-first path search PathFindBetweenTiles_seg006_1477_12BB with the path
	/// walk seg006_1477_1843. Both test every tile as the MIDDLE of a triple (previous, tile,
	/// next) through one routine (see TraverseMultipleTiles_seg006_1477_6F6 in UWTileTraverse),
	/// which the host hands in as the Triple delegate. Reading aid: the private notes
	/// motion-path.md.
	///
	/// The straight line walks the tiles of the line and tests each as it becomes the middle;
	/// the destination is tested last, with no next tile, and the budget is zero. The path
	/// search tests the start tile as the middle in its first wave and accepts a goal that is
	/// a direct neighbour of the start WITHOUT testing it; a goal reached in a later wave is
	/// tested as the middle of the final triple.
	/// </summary>
	public sealed class UWTileRoute
	{
		/// <summary>
		/// TraverseMultipleTiles: is the middle tile B passable from A towards C? A.x == 0 means
		/// no previous tile (the start), C.x == 0 no next tile (the destination). piHeightIn is
		/// the floor level the mover arrives with; piHeightOut the level it stands at in B; the
		/// cost accumulates drops and terrain against the budget (UWTileRoute.Budget while the
		/// test runs). pbClimb is the routine's dseg_244F: the step is a climb.
		/// </summary>
		public delegate bool TripleDelegate(int piAx, int piAy, int piBx, int piBy, int piCx, int piCy,
			int piHeightIn, ref int piHeightOut, ref int piCost, out bool pbClimb);

		/// <summary>The straight line's result: the line is clear.</summary>
		public const int LineClear = 1;

		public const int LineBlocked = 0;

		/// <summary>The straight line from a tile to itself.</summary>
		public const int LineSameTile = -1;

		/// <summary>The tile list seg056 has 0x40 entries; a line that long is blocked (2041).</summary>
		public const int MaxLineTiles = 0x3F;

		/// <summary>The search box reaches 5 tiles beyond the smaller and the larger coordinate,
		/// clamped to 1..0x40 (12BB, lines 43551-43678).</summary>
		public const int SearchMargin = 5;

		/// <summary>Two alternating wave lists of 64 entries (seg058, seg059).</summary>
		public const int MaxWaveWidth = 64;

		/// <summary>The depth limit of the search (line 44239: depth 0x20).</summary>
		public const int MaxDepth = 0x20;

		/// <summary>dseg_2476: the cost budget of the running test - 0 for the straight line,
		/// the creature's range for the path search.</summary>
		public int Budget { get; private set; }

		/// <summary>The tiles of the last straight line or path, start first.</summary>
		public readonly List<UWTilePos> Tiles = new List<UWTilePos>();

		/// <summary>A cell of the search grid seg049 (5 bytes: prev x, prev y, height, cost &lt;&lt; 1 |
		/// climb, depth). Visited = prev x != 0 (the original's test, line 44077), which is why the
		/// box starts at 1.</summary>
		private struct Cell
		{
			public int PrevX;

			public int PrevY;

			public int Height;

			public int Cost;

			public bool Climb;

			public int Depth;
		}

		private readonly Cell[,] mOCells = new Cell[65, 65];

		private readonly List<UWTilePos> mOWave = new List<UWTilePos>(MaxWaveWidth);

		private readonly List<UWTilePos> mONext = new List<UWTilePos>(MaxWaveWidth);

		/// <summary>The four neighbour offsets of the search (table dseg_AC/AD; order GUESS: as
		/// the port's UWTilePath walks them until the data bytes are read).</summary>
		private static readonly int[] NeighbourDx = { 1, 0, -1, 0 };

		private static readonly int[] NeighbourDy = { 0, 1, 0, -1 };

		// ------------------------------------------------- seg006_1477_1938 (line 44397)

		/// <summary>
		/// The straight tile line from (x0, y0) to (x1, y1): a DDA over the dominant axis with
		/// the minor axis carried by a 1/128 slope accumulator that starts at 0x40, every tile
		/// appended and tested as it becomes the middle of a triple, the destination tested last
		/// against no next tile. The budget is 0: any cost blocks. Returns LineClear, LineBlocked
		/// or LineSameTile.
		/// </summary>
		public int StraightLine(int piX0, int piY0, int piX1, int piY1, int piStartHeight, TripleDelegate pOTriple)
		{
			Budget = 0;
			Tiles.Clear();
			mOHeights.Clear();

			int liDx = (sbyte)(piX1 - piX0);
			int liDy = (sbyte)(piY1 - piY0);

			if (liDx == 0 && liDy == 0)
				return LineSameTile;

			// The quadrant: which axis is dominant and its direction, the minor's slope in 1/128
			// and its direction (lines 44430-44560).
			bool lbDominantX;
			int liDominantStep;
			int liMinorStep;
			int liSlope;

			if (liDx >= liDy)
			{
				if (liDx >= -liDy)
				{
					lbDominantX = true;
					liDominantStep = 1;
					liSlope = (liDy << 7) / liDx;
					liMinorStep = liDy > 0 ? 1 : -1;
				}
				else
				{
					lbDominantX = false;
					liDominantStep = -1;
					liSlope = (liDx << 7) / liDy;
					liMinorStep = liDx > 0 ? 1 : -1;
				}
			}
			else
			{
				if (liDx >= -liDy)
				{
					lbDominantX = false;
					liDominantStep = 1;
					liSlope = (liDx << 7) / liDy;
					liMinorStep = liDx > 0 ? 1 : -1;
				}
				else
				{
					lbDominantX = true;
					liDominantStep = -1;
					liSlope = (liDy << 7) / liDx;
					liMinorStep = liDy > 0 ? 1 : -1;
				}
			}

			int liX = piX0;
			int liY = piY0;
			int liAccumulator = 0x40;

			fAppend(liX, liY, piStartHeight);

			for (;;)
			{
				if (lbDominantX)
					liX += liDominantStep;
				else
					liY += liDominantStep;

				if (!fAppendAndTest(liX, liY, pOTriple))
					return LineBlocked;

				// The byte accumulator: bit 7 carries one minor step (lines 44600-44640).
				liAccumulator = (liAccumulator + liSlope) & 0xFF;

				if ((liAccumulator & 0x80) != 0)
				{
					liAccumulator &= 0x7F;

					if (lbDominantX)
						liY += liMinorStep;
					else
						liX += liMinorStep;

					if (!fAppendAndTest(liX, liY, pOTriple))
						return LineBlocked;
				}

				if (liX == piX1 && liY == piY1)
					break;
			}

			// The destination as the middle of the last triple, no next tile (lines 44700-44780).
			int liCount = Tiles.Count;
			int liHeightOut = mOHeights[liCount - 2];
			int liCost = 0;
			bool lbClimb;
			UWTilePos lOPrev = Tiles[liCount - 2];
			UWTilePos lODest = Tiles[liCount - 1];

			return pOTriple(lOPrev.X, lOPrev.Y, lODest.X, lODest.Y, 0, 0, mOHeights[liCount - 2], ref liHeightOut, ref liCost, out lbClimb)
				? LineClear : LineBlocked;
		}

		/// <summary>The heights the triple test writes per tile (seg056 byte 2).</summary>
		private readonly List<int> mOHeights = new List<int>();

		private void fAppend(int piX, int piY, int piHeight)
		{
			Tiles.Add(new UWTilePos(piX, piY));
			mOHeights.Add(piHeight);
		}

		/// <summary>seg006_1477_2041 (line 45432): appends the tile and tests the previous one as
		/// the middle of the last three (the second tile tests the start against no previous).
		/// A climb step fails the line (dseg_244F).</summary>
		private bool fAppendAndTest(int piX, int piY, TripleDelegate pOTriple)
		{
			fAppend(piX, piY, 0);

			int liCount = Tiles.Count;

			if (liCount > MaxLineTiles)
				return false;

			int liCost = 0;
			bool lbClimb;
			bool lbOk;

			if (liCount == 2)
			{
				int liHeightOut = mOHeights[1];

				lbOk = pOTriple(0, 0, Tiles[0].X, Tiles[0].Y, Tiles[1].X, Tiles[1].Y, mOHeights[0], ref liHeightOut, ref liCost, out lbClimb);
				mOHeights[1] = liHeightOut;
			}
			else
			{
				int liHeightOut = mOHeights[liCount - 2];

				lbOk = pOTriple(Tiles[liCount - 3].X, Tiles[liCount - 3].Y, Tiles[liCount - 2].X, Tiles[liCount - 2].Y,
					Tiles[liCount - 1].X, Tiles[liCount - 1].Y, mOHeights[liCount - 3], ref liHeightOut, ref liCost, out lbClimb);
				mOHeights[liCount - 2] = liHeightOut;
			}

			return lbOk && !lbClimb;
		}

		// ------------------------------------------------- PathFindBetweenTiles_seg006_1477_12BB (line 43491)

		/// <summary>
		/// The breadth-first search from the start to the goal within the box, at most 64 tiles
		/// per wave and 32 waves. The first wave tests the start as the middle towards each
		/// neighbour and returns at once when a neighbour IS the goal - untested. Later waves
		/// test each wave tile as the middle towards its neighbours (never back to its own
		/// previous tile); a neighbour already visited is taken over only from a shallower
		/// depth with a height nearer the target's; the goal, once reached, is tested as the
		/// middle of the final triple with no next tile. Tiles holds the path, start first.
		/// </summary>
		public bool FindPath(int piX0, int piY0, int piStartHeight, int piX1, int piY1, int piTargetHeight, int piRange,
			TripleDelegate pOTriple)
		{
			Budget = piRange & 0xFF;
			Tiles.Clear();
			Array.Clear(mOCells, 0, mOCells.Length);

			int liMinX = Math.Max(1, Math.Min(piX0, piX1) - SearchMargin);
			int liMaxX = Math.Min(0x40, Math.Max(piX0, piX1) + SearchMargin);
			int liMinY = Math.Max(1, Math.Min(piY0, piY1) - SearchMargin);
			int liMaxY = Math.Min(0x40, Math.Max(piY0, piY1) + SearchMargin);

			if (!fInGrid(piX0, piY0) || !fInGrid(piX1, piY1))
				return false;

			mOCells[piX0, piY0] = new Cell { PrevX = piX0, Height = piStartHeight };

			mOWave.Clear();
			mONext.Clear();

			// The first wave (lines 43700-43760).
			for (int liAt = 0; liAt < 4; liAt++)
			{
				int liNx = piX0 + NeighbourDx[liAt];
				int liNy = piY0 + NeighbourDy[liAt];

				if (!fInGrid(liNx, liNy))
					continue;

				int liHeightOut = mOCells[liNx, liNy].Height;
				int liCost = 0;
				bool lbClimb;

				if (!pOTriple(0, 0, piX0, piY0, liNx, liNy, piStartHeight, ref liHeightOut, ref liCost, out lbClimb))
					continue;

				if (liNx == piX1 && liNy == piY1)
				{
					// THE UNTESTED GOAL: a neighbour of the start is the whole path.
					Tiles.Add(new UWTilePos(piX0, piY0));
					Tiles.Add(new UWTilePos(piX1, piY1));

					return true;
				}

				mOCells[liNx, liNy] = new Cell { PrevX = piX0, PrevY = piY0, Height = liHeightOut, Cost = liCost & 0x7F, Depth = 1 };

				if (mONext.Count < MaxWaveWidth)
					mONext.Add(new UWTilePos(liNx, liNy));
			}

			int liDepth = 1;

			while (mONext.Count > 0 && liDepth < MaxDepth)
			{
				mOWave.Clear();
				mOWave.AddRange(mONext);
				mONext.Clear();

				for (int liAt = 0; liAt < mOWave.Count; liAt++)
				{
					UWTilePos lOCur = mOWave[liAt];
					Cell lOCurCell = mOCells[lOCur.X, lOCur.Y];

					for (int liN = 0; liN < 4; liN++)
					{
						int liNx = lOCur.X + NeighbourDx[liN];
						int liNy = lOCur.Y + NeighbourDy[liN];

						if (liNx < liMinX || liNx > liMaxX || liNy < liMinY || liNy > liMaxY || !fInGrid(liNx, liNy))
							continue;

						// Never back to the tile this one came from.
						if (lOCurCell.PrevX == liNx && lOCurCell.PrevY == liNy)
							continue;

						int liHeightOut = 0;
						int liCost = lOCurCell.Cost;
						bool lbClimb;

						if (!pOTriple(lOCurCell.PrevX, lOCurCell.PrevY, lOCur.X, lOCur.Y, liNx, liNy, lOCurCell.Height,
							ref liHeightOut, ref liCost, out lbClimb))
							continue;

						Cell lONCell = mOCells[liNx, liNy];
						bool lbFresh = lONCell.PrevX == 0;

						if (!lbFresh && !(lOCurCell.Depth < lONCell.Depth
							&& Math.Abs(piTargetHeight - liHeightOut) < Math.Abs(piTargetHeight - lONCell.Height)))
							continue;

						mOCells[liNx, liNy] = new Cell { PrevX = lOCur.X, PrevY = lOCur.Y, Height = liHeightOut, Cost = liCost & 0x7F, Depth = liDepth + 1 };
						lOCurCell.Climb = lbClimb;
						mOCells[lOCur.X, lOCur.Y] = lOCurCell;

						if (lbFresh && mONext.Count < MaxWaveWidth)
							mONext.Add(new UWTilePos(liNx, liNy));

						if (liNx == piX1 && liNy == piY1)
						{
							// The goal as the middle of the final triple, no next tile.
							int liGoalHeight = liHeightOut;
							int liGoalCost = liCost;
							bool lbGoalClimb;

							if (!pOTriple(lOCur.X, lOCur.Y, piX1, piY1, 0, 0, liHeightOut, ref liGoalHeight, ref liGoalCost, out lbGoalClimb))
								continue;

							fBuildPath(piX1, piY1, liDepth);

							return true;
						}
					}
				}

				liDepth++;
			}

			return false;
		}

		/// <summary>seg006_1477_1843: the path from the goal back along the previous pointers, start first.</summary>
		private void fBuildPath(int piGoalX, int piGoalY, int piDepth)
		{
			Tiles.Clear();

			int liX = piGoalX;
			int liY = piGoalY;

			for (int liAt = 0; liAt <= piDepth + 1 && liAt < MaxLineTiles; liAt++)
			{
				Tiles.Insert(0, new UWTilePos(liX, liY));

				Cell lOCell = mOCells[liX, liY];

				if (lOCell.Depth == 0)
					break;

				liX = lOCell.PrevX;
				liY = lOCell.PrevY;
			}
		}

		private bool fInGrid(int piX, int piY)
		{
			return piX >= 0 && piY >= 0 && piX < 65 && piY < 65;
		}
	}
}
