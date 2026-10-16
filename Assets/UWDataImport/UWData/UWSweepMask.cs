using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHICH TILES THE ORIGINAL WOULD DRAW from an eye, as a 64 by 64 mask - for the palette path,
	/// whose sprites follow the original's draw order (UWPainterOrder.hlsl). That order is only
	/// sound inside the original's view cone: the original never draws a tile its sweep did not mark
	/// (seg031_115E and seg017_1FDD_DBC, see UWRenderSweep), so a thing behind a wall at the edge of
	/// the view simply is not there. Ours drew it, and at a diagonal view the order then put it over
	/// the nearer wall - it flashed through (per user, 2026-10-06, level 1, 6/31 looking north-west,
	/// at the right edge of the modern scheme's wider picture).
	///
	/// THE ORIGINAL'S SWEEP, SEVERAL TIMES: the cone of one sweep is the original's, +-0x2040 around
	/// the heading. A wider picture - the modern scheme on a wide screen, a field-of-view setting -
	/// would leave its edges undecided, so the sweep runs at the heading and turned by a quarter to
	/// either side (RunOffsets), +-135 degrees together. All runs start at the same eye, so a wall
	/// hides the same in each: a tile is VISIBLE when any run marks it, HIDDEN when it lies in the
	/// cone of a run and no run marks it - also beyond the sweep's grid (17 rows, 16 columns to each
	/// side), where the original draws nothing at all (per user the same day: specks far to the
	/// side still showed through the wall; the order clamps its columns at 16 as well) - and left to
	/// the order (Visible) otherwise, which is only what lies behind the eye.
	///
	/// THE ORDER ONLY IN THE ORIGINAL'S OWN CONE (per user, 2026-10-10: in the classic scheme's
	/// widened view a skeleton showed through a wall at the side): the painter's order is the
	/// heading's, so it is sound only for the tiles the heading's own run draws. A tile only a side
	/// run draws is TRUE DEPTH - its sprites go by the real depth against the walls, as the
	/// Remastered path does (UWPainterOrder.hlsl, UWPainterSpriteVisibleTrue).
	/// </summary>
	public sealed class UWSweepMask
	{
		public const int Size = 64;

		public const byte Visible = 255;

		public const byte Hidden = 0;

		/// <summary>Drawn only by a side run: the real depth decides, not the order.</summary>
		public const byte TrueDepth = 128;

		/// <summary>Half the cone of one sweep, the original's edge rays (seg031_5B4).</summary>
		public const int ConeHalfAngle = 0x2040;

		/// <summary>The headings of the runs relative to the eye's.</summary>
		public static readonly int[] RunOffsets = { 0, -0x4000, 0x4000 };

		/// <summary>Per tile, y * 64 + x: Visible or Hidden.</summary>
		public readonly byte[] Mask = new byte[Size * Size];

		private readonly UWRenderSweep mOSweep = new UWRenderSweep();

		private readonly bool[] mbDrawn = new bool[Size * Size];

		/// <summary>Drawn by the run at the heading itself.</summary>
		private readonly bool[] mbDrawnAhead = new bool[Size * Size];

		private readonly bool[] mbInCone = new bool[Size * Size];

		/// <summary>The angle from the eye to every tile corner (65 by 65), once per build.</summary>
		private readonly int[] miCornerAngle = new int[(Size + 1) * (Size + 1)];

		private readonly bool[] mbCornerAtEye = new bool[(Size + 1) * (Size + 1)];

		/// <summary>The mask for an eye at piX/piY (the original's units, tile &lt;&lt; 8 plus the fine
		/// position) looking along piHeading (0 north, clockwise, 16 bits).</summary>
		public void Build(UWLevel pOLevel, int piX, int piY, int piHeading)
		{
			Array.Clear(mbDrawn, 0, mbDrawn.Length);
			Array.Clear(mbDrawnAhead, 0, mbDrawnAhead.Length);
			Array.Clear(mbInCone, 0, mbInCone.Length);

			if (pOLevel != null && pOLevel.TileData != null)
			{
				fCornerAngles(piX, piY);

				foreach (int liOffset in RunOffsets)
					fRun(pOLevel, piX, piY, (piHeading + liOffset) & 0xFFFF, liOffset == 0);

				// Every tile not drawn: in the cone of a run, the grid's reach or not.
				for (int liAt = 0; liAt < Mask.Length; liAt++)
				{
					if (mbDrawn[liAt] || mbInCone[liAt])
						continue;

					foreach (int liOffset in RunOffsets)
					{
						if (fTouchesCone(liAt % Size, liAt / Size, (piHeading + liOffset) & 0xFFFF))
						{
							mbInCone[liAt] = true;
							break;
						}
					}
				}
			}

			for (int liAt = 0; liAt < Mask.Length; liAt++)
			{
				Mask[liAt] = mbDrawnAhead[liAt] ? Visible
					: mbDrawn[liAt] ? TrueDepth
					: mbInCone[liAt] ? Hidden
					: Visible;
			}
		}

		private void fRun(UWLevel pOLevel, int piX, int piY, int piHeading, bool pbAhead)
		{
			mOSweep.Run(pOLevel, piX, piY, piHeading, null);

			int liPlayerTile = mOSweep.PlayerTileIndex;
			int liPlayerX = liPlayerTile % Size;
			int liPlayerY = liPlayerTile / Size;
			int liAheadX;
			int liAheadY;
			int liRightX;
			int liRightY;

			fStepToAxes(mOSweep.AheadStep, out liAheadX, out liAheadY);
			fStepToAxes(mOSweep.RightStep, out liRightX, out liRightY);

			for (int liRow = 0; liRow < UWRenderSweep.Rows; liRow++)
			{
				for (int liColumn = -UWRenderSweep.HalfWidth; liColumn <= UWRenderSweep.HalfWidth; liColumn++)
				{
					int liTileX = liPlayerX + (liRow * liAheadX) + (liColumn * liRightX);
					int liTileY = liPlayerY + (liRow * liAheadY) + (liColumn * liRightY);

					if (liTileX < 0 || liTileY < 0 || liTileX >= Size || liTileY >= Size)
						continue;

					int liAt = (liTileY * Size) + liTileX;

					if (mOSweep.IsDrawn(liRow, liColumn))
					{
						mbDrawn[liAt] = true;
						mbDrawnAhead[liAt] |= pbAhead;
					}
				}
			}
		}

		/// <summary>The angle (0 north, clockwise, 16 bits) from the eye to every tile corner.</summary>
		private void fCornerAngles(int piEyeX, int piEyeY)
		{
			for (int liY = 0; liY <= Size; liY++)
			{
				for (int liX = 0; liX <= Size; liX++)
				{
					int liAt = (liY * (Size + 1)) + liX;
					int liDx = (liX << 8) - piEyeX;
					int liDy = (liY << 8) - piEyeY;

					mbCornerAtEye[liAt] = liDx == 0 && liDy == 0;
					miCornerAngle[liAt] = mbCornerAtEye[liAt] ? 0 : (int)Math.Round(Math.Atan2(liDx, liDy) * 0x8000 / Math.PI);
				}
			}
		}

		/// <summary>The tile index step of a sweep axis as a tile offset.</summary>
		private static void fStepToAxes(int piStep, out int piDx, out int piDy)
		{
			piDx = piStep == 1 ? 1 : piStep == -1 ? -1 : 0;
			piDy = piStep == Size ? 1 : piStep == -Size ? -1 : 0;
		}

		/// <summary>Whether any part of the tile lies inside the cone of a run: the angles of its
		/// corners from the eye, relative to the run's heading, against +-ConeHalfAngle - a corner
		/// inside, or the corners on both sides of it with the tile in front.</summary>
		private bool fTouchesCone(int piTileX, int piTileY, int piHeading)
		{
			int liMin = int.MaxValue;
			int liMax = int.MinValue;

			for (int liCorner = 0; liCorner < 4; liCorner++)
			{
				int liCornerAt = ((piTileY + (liCorner >> 1)) * (Size + 1)) + piTileX + (liCorner & 1);

				if (mbCornerAtEye[liCornerAt])
					return true;

				int liDelta = (short)(miCornerAngle[liCornerAt] - piHeading);

				if (liDelta >= -ConeHalfAngle && liDelta <= ConeHalfAngle)
					return true;

				liMin = Math.Min(liMin, liDelta);
				liMax = Math.Max(liMax, liDelta);
			}

			// A tile wider than the cone, straddling it in front; a spread over half a turn wraps
			// round behind the eye.
			return liMin < -ConeHalfAngle && liMax > ConeHalfAngle && liMax - liMin < 0x8000;
		}
	}
}
