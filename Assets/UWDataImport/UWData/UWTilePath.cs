namespace UWDataImport.UWData
{
	/// <summary>
	/// A path across the tiles - for everyone who cannot reach the target in a straight line.
	///
	/// WHY NOT THE REFERENCE: its pathfinding is two thousand lines of disassembly with
	/// its own tables, and it belongs to the part that the reference project itself lists as
	/// uncertain. So what is rebuilt is not its method but its RESULT: a
	/// path across walkable neighbouring tiles. What walkable means is decided by the caller - a
	/// swimming creature sees the world differently from a walking one.
	///
	/// The search is breadth-first, starting from the TARGET. Then every reached tile records how
	/// far it is from the target, and the next step is simply the neighbour with the
	/// smallest value. That is cheaper than a search per creature with backtracking and
	/// yields the same.
	///
	/// READ IN THE ORIGINAL 2026-09-21, and it is the same kind of search:
	/// PathFindBetweenTiles_seg006_1477_12BB (43491-43843) clears a buffer of five bytes per
	/// tile (0x5000 for 64 by 64), seeds it with the start tile and works a queue of tiles
	/// (two arrays of coordinates in seg058 and seg059, a write and a read cursor), taking
	/// FOUR neighbours from the table at 0xAC - no diagonals, exactly as here - and writing
	/// into each reached tile the one it came from. TraverseMultipleTiles decides whether the
	/// step is possible, which is what IWalker does here.
	///
	/// SO THERE IS NO MEMORY FOR DEAD ENDS in the original, and none is needed in either
	/// version: a breadth-first search never walks into one, it only fails to leave it. The
	/// entry in the Todo had assumed a creature that gropes its way along; what a creature
	/// does when it bumps regardless is the blocked bit of NPC_Goto (UWCritterBrain), not a
	/// matter for the search.
	///
	/// WHAT WAS REALLY MISSING was the SEARCH WINDOW - see SearchMargin.
	///
	/// The arrays are static and reused: the search runs on the main thread,
	/// and a fresh array per call would be pure garbage for the garbage collector. Instead of
	/// clearing them, each pass writes its own number.
	/// </summary>
	public static class UWTilePath
	{
		/// <summary>Whoever searches for the path says which tile they can enter.</summary>
		public interface IWalker
		{
			bool CanEnterTile(int piFromX, int piFromY, int piToX, int piToY);
		}

		private const int GridSize = UWWorldScale.TilesPerAxis;

		private const int CellCount = GridSize * GridSize;

		/// <summary>Number of the pass in which this tile was last reached.</summary>
		private static readonly int[] miStamp = new int[CellCount];

		/// <summary>Distance to the target in tiles, valid only with a matching number.</summary>
		private static readonly int[] miDistance = new int[CellCount];

		private static readonly int[] miQueue = new int[CellCount];

		private static int miCurrentStamp;

		private static readonly int[] miNeighbourX = { 0, 1, 0, -1 };

		private static readonly int[] miNeighbourY = { 1, 0, -1, 0 };

		/// <summary>
		/// How far beyond the two end tiles the search may reach, in tiles
		/// (PathFindBetweenTiles_seg006_1477_12BB, labels 12F9 to 13C8): the box around start
		/// and target, five tiles wider on every side, the lower edge never below tile 1 and
		/// the upper one never beyond the map. Every tile taken out of the queue whose
		/// neighbour falls outside is skipped (labels 15DE to 15F8, four comparisons).
		///
		/// THIS IS THE REAL LIMIT of the original's search and it replaced ours on
		/// 2026-09-21, a ceiling of 400 examined tiles that had no model in the original. The
		/// difference shows in two directions: a way round that leaves the box is not found
		/// even when it is short, and within the box the search may cost far more than 400
		/// tiles - up to the whole level when start and target are far apart.
		/// </summary>
		public const int SearchMargin = 5;

		/// <summary>
		/// THE LONGEST PATH, in tiles (PathFindBetweenTiles_seg006_1477_12BB, label 182F: the
		/// waves stop when their count reaches 0x20). Found 2026-09-29 (per user: Biden, sent
		/// home to 3/16 from 18/43 on level 4, never got there in the original - long pauses and
		/// wandering - while ours walked on until it stuck): home was at least 42 steps away, so
		/// the original's search always failed, the blocked bit came and the wander step with it.
		/// Ours had no limit and found the way. Leaving the level sends the creature home
		/// (UWLevelExit), which the original evidently relies on.
		/// </summary>
		public const int MaxPathLength = 0x20;

		/// <summary>
		/// The widest wave, in tiles (label 17D8: while the next wave holds 0x40 entries the
		/// current one is not expanded further). Ours searches from the target, the original
		/// from the start, so which tiles fall off a full wave may differ; the cap is the same.
		/// </summary>
		public const int MaxWaveWidth = 0x40;

		/// <summary>Tiles written into each wave of the current search.</summary>
		private static readonly int[] miWaveCount = new int[MaxPathLength + 2];

		/// <summary>The lowest tile the search may enter - the original clamps its lower
		/// window edge at 1, and row and column 0 are the solid border of the map.</summary>
		public const int FirstSearchTile = 1;

		/// <summary>
		/// Finds the next step from pOFrom towards pOTo.
		///
		/// The search stays inside the window of SearchMargin. The visited counter is only a
		/// last stop against a broken walker; with the window it never comes into play.
		///
		/// Returns false if there is no path inside the window.
		/// </summary>
		public static bool TryGetNextStep(IWalker pOWalker, UWTilePos pOFrom, UWTilePos pOTo,
			out UWTilePos pONextStep)
		{
			pONextStep = pOFrom;

			if (!fSearch(pOWalker, pOFrom, pOTo))
				return false;

			return fTryPickNeighbour(pOWalker, pOFrom, out pONextStep);
		}

		/// <summary>
		/// The whole path from pOFrom to pOTo, both ends included, into pOPath (cleared first) -
		/// the original's path buffer, which the sleep ambush walks tile by tile
		/// (UWSleepRules.AmbushPlan). False if there is no path inside the window.
		/// </summary>
		public static bool TryGetPath(IWalker pOWalker, UWTilePos pOFrom, UWTilePos pOTo,
			System.Collections.Generic.List<UWTilePos> pOPath)
		{
			if (pOPath == null)
				return false;

			pOPath.Clear();

			if (!fSearch(pOWalker, pOFrom, pOTo))
				return false;

			UWTilePos lOAt = pOFrom;

			pOPath.Add(lOAt);

			while (lOAt != pOTo)
			{
				UWTilePos lONext;

				if (!fTryPickNeighbour(pOWalker, lOAt, out lONext) || pOPath.Count > CellCount)
				{
					pOPath.Clear();

					return false;
				}

				lOAt = lONext;
				pOPath.Add(lOAt);
			}

			return true;
		}

		/// <summary>The breadth-first search from the target, inside the window - see the class
		/// comment and SearchMargin. True when pOFrom was reached.</summary>
		private static bool fSearch(IWalker pOWalker, UWTilePos pOFrom, UWTilePos pOTo)
		{
			if (pOWalker == null || !fIsInside(pOFrom.X, pOFrom.Y) || !fIsInside(pOTo.X, pOTo.Y))
				return false;

			if (pOFrom == pOTo)
				return false;

			int liMinX = pOFrom.X < pOTo.X ? pOFrom.X : pOTo.X;
			int liMaxX = pOFrom.X > pOTo.X ? pOFrom.X : pOTo.X;
			int liMinY = pOFrom.Y < pOTo.Y ? pOFrom.Y : pOTo.Y;
			int liMaxY = pOFrom.Y > pOTo.Y ? pOFrom.Y : pOTo.Y;

			liMinX = liMinX - SearchMargin < FirstSearchTile ? FirstSearchTile : liMinX - SearchMargin;
			liMinY = liMinY - SearchMargin < FirstSearchTile ? FirstSearchTile : liMinY - SearchMargin;
			liMaxX = liMaxX + SearchMargin > GridSize - 1 ? GridSize - 1 : liMaxX + SearchMargin;
			liMaxY = liMaxY + SearchMargin > GridSize - 1 ? GridSize - 1 : liMaxY + SearchMargin;

			miCurrentStamp++;
			System.Array.Clear(miWaveCount, 0, miWaveCount.Length);

			int liHead = 0;
			int liTail = 0;
			int liVisited = 0;

			int liTarget = (pOTo.Y * GridSize) + pOTo.X;

			miStamp[liTarget] = miCurrentStamp;
			miDistance[liTarget] = 0;
			miQueue[liTail++] = liTarget;

			bool lbReached = false;

			while (liHead < liTail && liVisited < CellCount)
			{
				int liCell = miQueue[liHead++];

				liVisited++;

				int liX = liCell % GridSize;
				int liY = liCell / GridSize;

				if (liX == pOFrom.X && liY == pOFrom.Y)
				{
					lbReached = true;
					break;
				}

				for (int liAt = 0; liAt < miNeighbourX.Length; liAt++)
				{
					int liNextX = liX + miNeighbourX[liAt];
					int liNextY = liY + miNeighbourY[liAt];

					if (liNextX < liMinX || liNextX > liMaxX || liNextY < liMinY || liNextY > liMaxY)
						continue;

					int liNext = (liNextY * GridSize) + liNextX;

					if (miStamp[liNext] == miCurrentStamp)
						continue;

					// THE CHECK IS IN WALKING DIRECTION, i.e. from the neighbour towards the already
					// reached tile - the search runs backwards, the step later runs forwards.
					if (!pOWalker.CanEnterTile(liNextX, liNextY, liX, liY))
						continue;

					// THE LENGTH AND THE WIDTH OF THE WAVES (see MaxPathLength, MaxWaveWidth).
					int liWave = miDistance[liCell] + 1;

					if (liWave > MaxPathLength || miWaveCount[liWave] >= MaxWaveWidth)
						continue;

					miWaveCount[liWave]++;
					miStamp[liNext] = miCurrentStamp;
					miDistance[liNext] = miDistance[liCell] + 1;
					miQueue[liTail++] = liNext;
				}
			}

			return lbReached;
		}

		/// <summary>The neighbour closest to the target.</summary>
		private static bool fTryPickNeighbour(IWalker pOWalker, UWTilePos pOFrom,
			out UWTilePos pONextStep)
		{
			pONextStep = pOFrom;

			int liBest = int.MaxValue;

			for (int liAt = 0; liAt < miNeighbourX.Length; liAt++)
			{
				int liX = pOFrom.X + miNeighbourX[liAt];
				int liY = pOFrom.Y + miNeighbourY[liAt];

				if (!fIsInside(liX, liY))
					continue;

				int liCell = (liY * GridSize) + liX;

				if (miStamp[liCell] != miCurrentStamp || miDistance[liCell] >= liBest)
					continue;

				if (!pOWalker.CanEnterTile(pOFrom.X, pOFrom.Y, liX, liY))
					continue;

				liBest = miDistance[liCell];
				pONextStep = new UWTilePos(liX, liY);
			}

			return liBest != int.MaxValue;
		}

		private static bool fIsInside(int piX, int piY)
		{
			return piX >= 0 && piY >= 0 && piX < GridSize && piY < GridSize;
		}

		/// <summary>
		/// Whether one can step from a tile onto the adjacent one, as far as the TILE SHAPE
		/// is concerned - heights and water are checked by the creatures themselves.
		///
		/// A diagonal tile is only half floor and therefore lets through only two of its four
		/// sides. Which ones is given by its type; the map draws with the same
		/// mapping (UWGameUI.fHasEdge).
		/// </summary>
		public static bool AllowsEdge(UWTile pOTile, int piSide)
		{
			if (pOTile == null)
				return false;

			switch (pOTile.TileType)
			{
				case UWTile.TileTypeEnum.solid:
					return false;

				case UWTile.TileTypeEnum.diagonal_se:
					return piSide == SideEast || piSide == SideSouth;

				case UWTile.TileTypeEnum.diagonal_sw:
					return piSide == SideSouth || piSide == SideWest;

				case UWTile.TileTypeEnum.diagonal_ne:
					return piSide == SideNorth || piSide == SideEast;

				case UWTile.TileTypeEnum.diagonal_nw:
					return piSide == SideNorth || piSide == SideWest;
			}

			return true;
		}

		/// <summary>
		/// THE SIGHT LINE on the tile map - TestBetweenPoints_seg006_1477_1BD1 (44792-45426) with
		/// TestTileTraversal_seg006_1056 (43103-43485). A line in eighths from one point to the
		/// other with the height interpolated; doors are objects and do not stop it, which is how
		/// the goblin in front of the storage room notices a theft through a closed door (per user,
		/// 2026-09-16). What stops it:
		///
		///   a solid tile anywhere on the line
		///   the line below the floor of a tile it is in (zpos / 8 against the floor height)
		///   AT EVERY TILE CHANGE the side it leaves by and the side it enters by, when closed
		///
		/// The closed sides come from the original's table TileTraverseFlags_dseg_5c99_1D8A, one
		/// byte per tile type: 0x02 west, 0x04 east, 0x08 south, 0x10 north closed - solid 0x1E,
		/// open 0, the four diagonals 0x13, 0x15, 0x0B, 0x0D (plus bit 0 for "diagonal"), the
		/// slopes 0x20 with no side closed. That is exactly what AllowsEdge already said for the
		/// path search, so the table is not repeated here. The diagonal wall INSIDE a tile is not
		/// tested by the original, only the sides: a line that enters and leaves a diagonal tile
		/// by its open sides passes.
		///
		/// Until 2026-09-23 the host (UWCritter) walked the line itself and read a diagonal tile
		/// as an open one (deviation 23); the old HasClearTileLine, which no one called any more,
		/// went at the same time.
		/// </summary>
		public static bool TestBetweenPoints(UWTile[] pOTiles, int piX0, int piY0, int piZ0,
			int piX1, int piY1, int piZ1)
		{
			if (pOTiles == null || pOTiles.Length < CellCount)
				return false;

			int liDx = piX1 - piX0;
			int liDy = piY1 - piY0;
			int liSteps = System.Math.Max(System.Math.Abs(liDx), System.Math.Abs(liDy));

			int liTileX = piX0 >> 3;
			int liTileY = piY0 >> 3;

			for (int liStep = 1; liStep <= liSteps; liStep++)
			{
				int liX = piX0 + (liDx * liStep / liSteps);
				int liY = piY0 + (liDy * liStep / liSteps);
				int liZ = piZ0 + ((piZ1 - piZ0) * liStep / liSteps);

				int liNextX = liX >> 3;
				int liNextY = liY >> 3;

				UWTile lOTile = fGetTile(pOTiles, liNextX, liNextY);

				if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
					return false;

				if (liNextX != liTileX || liNextY != liTileY)
				{
					if (!fCanSightCross(pOTiles, liTileX, liTileY, liNextX, liNextY))
						return false;

					liTileX = liNextX;
					liTileY = liNextY;
				}

				if ((liZ >> 3) < (lOTile.FloorHeight >> 4))
					return false;
			}

			return true;
		}

		/// <summary>
		/// One tile change of the sight line: out of the one tile by a side that is open, into
		/// the next by a side that is open (TestTileTraversal_seg006_1056, both halves). A step
		/// through a CORNER - both coordinates change at once, which the original splits into its
		/// two boundary crossings - passes if either way round the corner is open.
		/// </summary>
		private static bool fCanSightCross(UWTile[] pOTiles, int piFromX, int piFromY, int piToX, int piToY)
		{
			if (piFromX == piToX || piFromY == piToY)
			{
				int liSide = GetSide(piFromX, piFromY, piToX, piToY);

				return AllowsEdge(fGetTile(pOTiles, piFromX, piFromY), liSide)
					&& AllowsEdge(fGetTile(pOTiles, piToX, piToY), GetOppositeSide(liSide));
			}

			return (fCanSightCross(pOTiles, piFromX, piFromY, piToX, piFromY)
					&& fCanSightCross(pOTiles, piToX, piFromY, piToX, piToY))
				|| (fCanSightCross(pOTiles, piFromX, piFromY, piFromX, piToY)
					&& fCanSightCross(pOTiles, piFromX, piToY, piToX, piToY));
		}

		private static UWTile fGetTile(UWTile[] pOTiles, int piX, int piY)
		{
			return fIsInside(piX, piY) ? pOTiles[(piY * GridSize) + piX] : null;
		}

		public const int SideNorth = 0;

		public const int SideEast = 1;

		public const int SideSouth = 2;

		public const int SideWest = 3;

		/// <summary>Which side of the from tile faces the to tile.</summary>
		public static int GetSide(int piFromX, int piFromY, int piToX, int piToY)
		{
			if (piToY > piFromY)
				return SideNorth;

			if (piToY < piFromY)
				return SideSouth;

			return piToX > piFromX ? SideEast : SideWest;
		}

		/// <summary>Which side of the neighbouring tile is the same edge.</summary>
		public static int GetOppositeSide(int piSide)
		{
			return (piSide + 2) % 4;
		}
	}
}
