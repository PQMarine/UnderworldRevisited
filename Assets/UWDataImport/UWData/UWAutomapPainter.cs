using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Paints the discovered tiles onto the blank map the way UW.EXE does it - read 2026-09-26 in
	/// the automap overlay (ovr092), after the user found a "knob" where two diagonals meet that
	/// the original does not have.
	///
	/// THE PICTURE IS PALETTE INDICES, not colours: the screen starts as BLNKMAP.BYT (palette 1)
	/// and almost every pixel is "the parchment under it plus a small random step"
	/// (ovr092_41F: pixel + base + RNG / (0x7FFF / range)). Where the parchment is lighter or
	/// darker, so is the map. Only water, lava and bridges take fixed palette entries.
	///
	/// ovr092_1E5, per tile from 1 to 62 on both axes (the outer ring is never drawn), rows from
	/// the south: only discovered tiles that are not rock (type 1 to 9) are painted.
	///   1. ovr092_48B fills the 3x3 cell from a pattern per type (table ACA): 1 floor, 2 the
	///      diagonal's wall pixel, 0 nothing - so the solid half of a diagonal stays parchment.
	///      Slopes count as open floor. Floor is parchment +2..4, the wall pixel +6..7, water
	///      0xB1 + RNG % 2, lava 0xB5 + RNG % 2. Then the display: a door (ovr092_66F), a bridge
	///      (0xE9 + 0..2 over the whole cell), the third kind (display 0xC, our stairs: +6..8 over
	///      the whole cell).
	///   2. ovr092_327 draws a 3 pixel border just OUTSIDE each side whose neighbour is NOT a
	///      discovered non-rock tile: +0..3 towards undiscovered open floor (marker 0xB), +6..7
	///      towards anything else. A discovered neighbour of any kind 1 to 9 - a diagonal
	///      included - gets no border, whatever half faces it. A diagonal is bordered on the two
	///      sides of its open half only (table AF9).
	///   3. Where two bordered sides meet, one corner pixel +3..4 - but only at the north-east
	///      corner (north+east and east+south) and the south-west corner (south+west and
	///      west+north); the original's offset (side & 2) * 2 never reaches the other two.
	///
	/// Coordinates here are the original's: x from the left, y from the BOTTOM of the 320x200
	/// screen; a tile's cell starts at (3x + 7, 3y + 4).
	/// </summary>
	public static class UWAutomapPainter
	{
		public const int ScreenWidth = 320;

		public const int ScreenHeight = 200;

		public const int TilesPerAxis = 64;

		public const int TileSize = 3;

		public const int CellLeft = 7;

		public const int CellBottom = 4;

		/// <summary>Where a random value is drawn, so that a caller can keep each one stable
		/// (the port rolls once per pixel instead of on every drawing, per user 2026-09-26).</summary>
		public enum RandomUse
		{
			Floor,
			Wall,
			Water,
			Lava,
			Bridge,
			Stairs,
			Door,
			Border,
			Corner
		}

		/// <summary>Table ACA: 3x3 per type (open, then the diagonals SE, SW, NE, NW), rows from
		/// the bottom, columns from the left.</summary>
		private static readonly byte[] myPatterns =
		{
			1, 1, 1, 1, 1, 1, 1, 1, 1,
			2, 1, 1, 0, 2, 1, 0, 0, 2,
			1, 1, 2, 1, 2, 0, 2, 0, 0,
			0, 0, 2, 0, 2, 1, 2, 1, 1,
			2, 0, 0, 1, 2, 0, 1, 1, 2
		};

		/// <summary>Table AF9: the first of the two bordered sides of a diagonal (types 2 to 5);
		/// the second is the next one clockwise. Sides: 0 north, 1 east, 2 south, 3 west.</summary>
		private static readonly int[] myDiagonalFirstSide = { 1, 2, 0, 3 };

		/// <summary>Tables AFD / B01: the neighbours ovr092_66F looks at for a door's
		/// direction, in this order.</summary>
		private static readonly int[] myDoorDy = { -1, 0, -1, 1 };

		private static readonly int[] myDoorDx = { 0, -1, -1, -1 };

		private const int WaterIndex = 0xB1;

		private const int LavaIndex = 0xB5;

		private const int BridgeIndex = 0xE9;

		/// <summary>
		/// Paints pyAutomap (64x64 bytes, row 0 south) into pyScreen (320x200 palette indices,
		/// row 0 at the TOP, as BLNKMAP.BYT is stored). pFRandom(x, y, use) returns 0..0x7FFF like
		/// UW.EXE's random generator.
		/// </summary>
		public static void Paint(byte[] pyScreen, byte[] pyAutomap, Func<int, int, RandomUse, int> pFRandom)
		{
			if (pyScreen == null || pyScreen.Length < ScreenWidth * ScreenHeight
				|| pyAutomap == null || pyAutomap.Length < TilesPerAxis * TilesPerAxis || pFRandom == null)
				return;

			bool[] lbBordered = new bool[4];

			for (int liY = 1; liY < TilesPerAxis - 1; liY++)
			{
				for (int liX = 1; liX < TilesPerAxis - 1; liX++)
				{
					int liType = pyAutomap[(liY * TilesPerAxis) + liX] & 0xF;

					if (liType <= 0 || liType >= 0xA)
						continue;

					fFillCell(pyScreen, pyAutomap, pFRandom, liType, liX, liY);

					Array.Clear(lbBordered, 0, lbBordered.Length);

					if (liType >= 2 && liType <= 5)
					{
						int liSide = myDiagonalFirstSide[liType - 2];

						lbBordered[liSide] = fBorder(pyScreen, pyAutomap, pFRandom, liSide, liX, liY);
						liSide = (liSide + 1) & 3;
						lbBordered[liSide] = fBorder(pyScreen, pyAutomap, pFRandom, liSide, liX, liY);
					}
					else
					{
						for (int liSide = 0; liSide < 4; liSide++)
							lbBordered[liSide] = fBorder(pyScreen, pyAutomap, pFRandom, liSide, liX, liY);
					}

					int liCornerX = (liX * TileSize) + CellLeft + TileSize;
					int liCornerY = (liY * TileSize) + CellBottom + TileSize;

					for (int liSide = 0; liSide < 4; liSide++)
					{
						if (!lbBordered[liSide] || !lbBordered[(liSide + 1) & 3])
							continue;

						int liBack = (liSide & 2) << 1;

						fStep(pyScreen, pFRandom, RandomUse.Corner, liCornerX - liBack, liCornerY - liBack, 3, 2);
					}
				}
			}
		}

		/// <summary>ovr092_48B: the cell's pattern, then what the display adds.</summary>
		private static void fFillCell(byte[] pyScreen, byte[] pyAutomap, Func<int, int, RandomUse, int> pFRandom,
			int piType, int piX, int piY)
		{
			int liDisplay = pyAutomap[(piY * TilesPerAxis) + piX] >> 4;
			int liLiquid = liDisplay & 3;
			int liKind = liDisplay & 0xC;
			int liLeft = (piX * TileSize) + CellLeft;
			int liBottom = (piY * TileSize) + CellBottom;
			int liPattern = (piType >= 6 ? 1 : piType) - 1;

			for (int liColumn = 0; liColumn < TileSize; liColumn++)
			{
				for (int liRow = 0; liRow < TileSize; liRow++)
				{
					int liX = liLeft + liColumn;
					int liY = liBottom + liRow;

					switch (myPatterns[(liPattern * 9) + (liRow * 3) + liColumn])
					{
						case 1:
							if (liLiquid == 0)
								fStep(pyScreen, pFRandom, RandomUse.Floor, liX, liY, 2, 3);
							else if (liLiquid == 1)
								fSet(pyScreen, liX, liY, WaterIndex + (pFRandom(liX, liY, RandomUse.Water) % 2));
							else if (liLiquid == 2)
								fSet(pyScreen, liX, liY, LavaIndex + (pFRandom(liX, liY, RandomUse.Lava) % 2));
							break;

						case 2:
							fStep(pyScreen, pFRandom, RandomUse.Wall, liX, liY, 6, 2);
							break;
					}
				}
			}

			switch (liKind)
			{
				case 4:
					fDoor(pyScreen, pyAutomap, pFRandom, piX, piY, liLeft + 1, liBottom + 1);
					break;

				case 8:
					for (int liColumn = 0; liColumn < TileSize; liColumn++)
					{
						for (int liRow = 0; liRow < TileSize; liRow++)
						{
							int liX = liLeft + liColumn;
							int liY = liBottom + liRow;

							fSet(pyScreen, liX, liY, BridgeIndex + ((pFRandom(liX, liY, RandomUse.Bridge) * 3) >> 15));
						}
					}

					break;

				case 0xC:
					for (int liColumn = 0; liColumn < TileSize; liColumn++)
					{
						for (int liRow = 0; liRow < TileSize; liRow++)
							fStep(pyScreen, pFRandom, RandomUse.Stairs, liLeft + liColumn, liBottom + liRow, 6, 3);
					}

					break;
			}
		}

		/// <summary>ovr092_66F: the middle pixel dark, and two more across the passage - the
		/// first of four neighbour pairs with open floor (type 1 exactly) on either side
		/// decides the direction.</summary>
		private static void fDoor(byte[] pyScreen, byte[] pyAutomap, Func<int, int, RandomUse, int> pFRandom,
			int piX, int piY, int piCentreX, int piCentreY)
		{
			fStep(pyScreen, pFRandom, RandomUse.Door, piCentreX, piCentreY, 6, 3);

			for (int liPair = 0; liPair < myDoorDy.Length; liPair++)
			{
				int liDy = myDoorDy[liPair];
				int liDx = myDoorDx[liPair];

				if (fTypeAt(pyAutomap, piX + liDx, piY + liDy) != 1 && fTypeAt(pyAutomap, piX - liDx, piY - liDy) != 1)
					continue;

				// Across the passage: the step along y moves x and the other way round.
				fStep(pyScreen, pFRandom, RandomUse.Door, piCentreX + liDy, piCentreY + liDx, 6, 3);
				fStep(pyScreen, pFRandom, RandomUse.Door, piCentreX - liDy, piCentreY - liDx, 6, 3);

				return;
			}
		}

		/// <summary>ovr092_327: the border outside one side, unless the neighbour is a
		/// discovered tile of type 1 to 9. True when it was drawn.</summary>
		private static bool fBorder(byte[] pyScreen, byte[] pyAutomap, Func<int, int, RandomUse, int> pFRandom,
			int piSide, int piX, int piY)
		{
			int liNeighbourX = piX + (piSide == 1 ? 1 : piSide == 3 ? -1 : 0);
			int liNeighbourY = piY + (piSide == 0 ? 1 : piSide == 2 ? -1 : 0);
			int liType = fTypeAt(pyAutomap, liNeighbourX, liNeighbourY);

			if (liType > 0 && liType < 0xA)
				return false;

			int liBase = liType == 0xB ? 0 : 6;
			int liRange = liType == 0xB ? 4 : 2;
			int liLeft = (piX * TileSize) + CellLeft;
			int liBottom = (piY * TileSize) + CellBottom;

			for (int liAt = 0; liAt < TileSize; liAt++)
			{
				switch (piSide)
				{
					case 0: fStep(pyScreen, pFRandom, RandomUse.Border, liLeft + liAt, liBottom + TileSize, liBase, liRange); break;
					case 1: fStep(pyScreen, pFRandom, RandomUse.Border, liLeft + TileSize, liBottom + liAt, liBase, liRange); break;
					case 2: fStep(pyScreen, pFRandom, RandomUse.Border, liLeft + liAt, liBottom - 1, liBase, liRange); break;
					default: fStep(pyScreen, pFRandom, RandomUse.Border, liLeft - 1, liBottom + liAt, liBase, liRange); break;
				}
			}

			return true;
		}

		private static int fTypeAt(byte[] pyAutomap, int piX, int piY)
		{
			if (piX < 0 || piY < 0 || piX >= TilesPerAxis || piY >= TilesPerAxis)
				return 0;

			return pyAutomap[(piY * TilesPerAxis) + piX] & 0xF;
		}

		/// <summary>ovr092_41F: the pixel as it is plus piBase plus RNG / (0x7FFF / piRange).</summary>
		private static void fStep(byte[] pyScreen, Func<int, int, RandomUse, int> pFRandom, RandomUse peUse,
			int piX, int piY, int piBase, int piRange)
		{
			int liAt = fIndex(piX, piY);

			if (liAt < 0)
				return;

			int liDivisor = 0x7FFF / piRange;

			pyScreen[liAt] = (byte)(pyScreen[liAt] + piBase + (pFRandom(piX, piY, peUse) / liDivisor));
		}

		private static void fSet(byte[] pyScreen, int piX, int piY, int piIndex)
		{
			int liAt = fIndex(piX, piY);

			if (liAt >= 0)
				pyScreen[liAt] = (byte)piIndex;
		}

		/// <summary>The byte of a bottom-up screen point in a top-down buffer, or -1 off screen.</summary>
		private static int fIndex(int piX, int piY)
		{
			if (piX < 0 || piY < 0 || piX >= ScreenWidth || piY >= ScreenHeight)
				return -1;

			return ((ScreenHeight - 1 - piY) * ScreenWidth) + piX;
		}
	}
}
