using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PALETTE RENDERER'S LIGHT SOURCES as a map of shade levels (per user, 2026-10-07, stage 2
	/// of the palette effects). Not the original's: in UW1 all light comes from the player, a wall
	/// torch or a campfire lights nothing. Off unless the player turns it on.
	///
	/// A SOURCE IS LIT LIKE THE PLAYER: it has a light level (0 to 7) and its light falls off by
	/// the same SHADES.DAT table as the player's own (UWShades.GetShadeTable), measured in tiles
	/// from the source - a campfire behaves as if someone stood there with a lantern. The level
	/// of a light level between two rows (a flickering fire) is interpolated between them, as the
	/// distance between two whole tiles is.
	///
	/// WALLS CAST SHADOWS: a point only takes a source's light when the straight line from the
	/// source to it crosses no solid tile (diagonal tiles count as open).
	///
	/// THE MAP: TexelsPerTile texels a tile, 64 tiles a side; a texel holds the lowest (brightest)
	/// level any source gives it, times 16 (255 = no light). Tile i spans i - 0.5 to i + 0.5 in
	/// tile coordinates, as the world's tile centres lie at tile * TileSize.
	///
	/// The view distance is not touched here: the shader takes these levels only within the
	/// player's own view distance, so a source does not show what the original leaves black
	/// (per user the same day: "dass man dadurch weiter sieht als es das Original zulaesst").
	/// </summary>
	public sealed class UWPaletteLightMap
	{
		public const int TexelsPerTile = 4;

		public const int Size = UWWorldScale.TilesPerAxis * TexelsPerTile;

		/// <summary>A texel's value for "no light from a source".</summary>
		public const byte Dark = 255;

		/// <summary>The highest shade level; a source's light ends where its table reaches it.
		/// </summary>
		public const int MaxLevel = 15;

		/// <summary>A light source: where (tile coordinates, the tile centre at whole numbers),
		/// its light level, and whether it changes from frame to frame (flicker, a moving
		/// creature) - those are not kept in the static part.</summary>
		public struct Source
		{
			public float X;

			public float Y;

			public float LightLevel;

			/// <summary>Levels added to the table's at every distance - a dull light with the same
			/// fall-off (lava, per user 2026-10-07: "Lava macht nur ein dumpfes Licht").</summary>
			public float Dim;

			public bool Changing;
		}

		/// <summary>The map, Size * Size, row y * Size + x.</summary>
		public readonly byte[] Map = new byte[Size * Size];

		private readonly byte[] myStatic = new byte[Size * Size];

		private readonly bool[] mbSolid = new bool[UWWorldScale.TilesPerAxis * UWWorldScale.TilesPerAxis];

		private readonly byte[][] myTables = new byte[UWShades.EntryCount][];

		private bool mbReady;

		/// <summary>Takes the level's walls and the shade tables; the map starts dark.</summary>
		public void SetLevel(UWLevel pOLevel, UWShades pOShades)
		{
			mbReady = pOLevel != null && pOLevel.TileData != null && pOShades != null && pOShades.IsLoaded;

			for (int liAt = 0; liAt < mbSolid.Length; liAt++)
			{
				UWTile lOTile = mbReady ? pOLevel.TileData[liAt] : null;

				mbSolid[liAt] = lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid;
			}

			for (int liLevel = 0; liLevel < myTables.Length; liLevel++)
				myTables[liLevel] = mbReady ? pOShades.GetShadeTable(liLevel) : null;

			for (int liAt = 0; liAt < myStatic.Length; liAt++)
				myStatic[liAt] = Dark;

			Array.Copy(myStatic, Map, Map.Length);
		}

		/// <summary>The steady sources (lava, a torch on the wall): drawn once into the static
		/// part.</summary>
		public void BuildStatic(IList<Source> pOSources)
		{
			for (int liAt = 0; liAt < myStatic.Length; liAt++)
				myStatic[liAt] = Dark;

			if (!mbReady || pOSources == null)
				return;

			foreach (Source lOSource in pOSources)
			{
				if (!lOSource.Changing)
					fDraw(myStatic, lOSource);
			}
		}

		/// <summary>The map for this frame: the static part and the changing sources on top.
		/// </summary>
		public void Compose(IList<Source> pOSources)
		{
			Array.Copy(myStatic, Map, Map.Length);

			if (!mbReady || pOSources == null)
				return;

			foreach (Source lOSource in pOSources)
			{
				if (lOSource.Changing)
					fDraw(Map, lOSource);
			}
		}

		/// <summary>The shade level a source of this light level gives at this tile distance,
		/// interpolated between the table's whole distances and its two neighbouring rows.
		/// </summary>
		public float LevelAt(float pfLightLevel, float pfDistance)
		{
			if (!mbReady)
				return MaxLevel;

			float lfLight = Math.Max(0f, Math.Min(UWShades.EntryCount - 1, pfLightLevel));
			int liLow = (int)Math.Floor(lfLight);
			int liHigh = Math.Min(liLow + 1, UWShades.EntryCount - 1);
			float lfMix = lfLight - liLow;

			return fRowLevel(myTables[liLow], pfDistance) * (1f - lfMix) + (fRowLevel(myTables[liHigh], pfDistance) * lfMix);
		}

		private static float fRowLevel(byte[] pyTable, float pfDistance)
		{
			if (pyTable == null)
				return MaxLevel;

			float lfDistance = Math.Max(0f, Math.Min(UWShades.ShadeTableLength - 1, pfDistance));
			int liLow = (int)Math.Floor(lfDistance);
			int liHigh = Math.Min(liLow + 1, UWShades.ShadeTableLength - 1);
			float lfMix = lfDistance - liLow;

			return (pyTable[liLow] * (1f - lfMix)) + (pyTable[liHigh] * lfMix);
		}

		/// <summary>How far a source of this light level reaches, in tiles: where its table
		/// first reaches the highest level.</summary>
		public float Reach(float pfLightLevel, float pfDim = 0f)
		{
			for (int liDistance = 0; liDistance < UWShades.ShadeTableLength; liDistance++)
			{
				if (LevelAt(pfLightLevel, liDistance) + pfDim >= MaxLevel - 0.01f)
					return liDistance;
			}

			return UWShades.ShadeTableLength;
		}

		private void fDraw(byte[] pyMap, Source pOSource)
		{
			float lfReach = Reach(pOSource.LightLevel, pOSource.Dim);

			if (lfReach <= 0f)
				return;

			// The texels whose centres lie within the reach.
			int liFromX = Math.Max(0, (int)Math.Floor((pOSource.X - lfReach + 0.5f) * TexelsPerTile));
			int liToX = Math.Min(Size - 1, (int)Math.Ceiling((pOSource.X + lfReach + 0.5f) * TexelsPerTile));
			int liFromY = Math.Max(0, (int)Math.Floor((pOSource.Y - lfReach + 0.5f) * TexelsPerTile));
			int liToY = Math.Min(Size - 1, (int)Math.Ceiling((pOSource.Y + lfReach + 0.5f) * TexelsPerTile));

			for (int liY = liFromY; liY <= liToY; liY++)
			{
				float lfCentreY = ((liY + 0.5f) / TexelsPerTile) - 0.5f;

				for (int liX = liFromX; liX <= liToX; liX++)
				{
					float lfCentreX = ((liX + 0.5f) / TexelsPerTile) - 0.5f;
					float lfDx = lfCentreX - pOSource.X;
					float lfDy = lfCentreY - pOSource.Y;
					float lfDistance = (float)Math.Sqrt((lfDx * lfDx) + (lfDy * lfDy));

					if (lfDistance >= lfReach)
						continue;

					int liAt = (liY * Size) + liX;
					float lfLevel = LevelAt(pOSource.LightLevel, lfDistance) + pOSource.Dim;
					int liValue = (int)Math.Round(lfLevel * 16f);

					if (liValue >= pyMap[liAt] || !fSeen(pOSource.X, pOSource.Y, lfCentreX, lfCentreY))
						continue;

					pyMap[liAt] = (byte)Math.Max(0, Math.Min(Dark, liValue));
				}
			}
		}

		/// <summary>Whether the straight line between two points in tile coordinates crosses
		/// no solid tile - a walk through the tiles it passes (the end tile itself included, so a
		/// texel inside a wall stays dark).</summary>
		private bool fSeen(float pfFromX, float pfFromY, float pfToX, float pfToY)
		{
			// In grid coordinates a tile spans whole numbers: tile i from i to i + 1.
			float lfX = pfFromX + 0.5f;
			float lfY = pfFromY + 0.5f;
			float lfEndX = pfToX + 0.5f;
			float lfEndY = pfToY + 0.5f;
			int liX = (int)Math.Floor(lfX);
			int liY = (int)Math.Floor(lfY);
			int liEndX = (int)Math.Floor(lfEndX);
			int liEndY = (int)Math.Floor(lfEndY);
			float lfDx = lfEndX - lfX;
			float lfDy = lfEndY - lfY;
			int liStepX = lfDx > 0f ? 1 : -1;
			int liStepY = lfDy > 0f ? 1 : -1;
			float lfDeltaX = lfDx != 0f ? Math.Abs(1f / lfDx) : float.MaxValue;
			float lfDeltaY = lfDy != 0f ? Math.Abs(1f / lfDy) : float.MaxValue;
			float lfNextX = lfDx != 0f ? (liStepX > 0 ? (liX + 1 - lfX) : (lfX - liX)) * lfDeltaX : float.MaxValue;
			float lfNextY = lfDy != 0f ? (liStepY > 0 ? (liY + 1 - lfY) : (lfY - liY)) * lfDeltaY : float.MaxValue;

			for (int liGuard = 0; liGuard < 64; liGuard++)
			{
				if (fIsSolid(liX, liY) && !(liX == (int)Math.Floor(lfX) && liY == (int)Math.Floor(lfY)))
					return false;

				if (liX == liEndX && liY == liEndY)
					return true;

				if (lfNextX < lfNextY)
				{
					lfNextX += lfDeltaX;
					liX += liStepX;
				}
				else
				{
					lfNextY += lfDeltaY;
					liY += liStepY;
				}
			}

			return true;
		}

		private bool fIsSolid(int piX, int piY)
		{
			if (piX < 0 || piY < 0 || piX >= UWWorldScale.TilesPerAxis || piY >= UWWorldScale.TilesPerAxis)
				return true;

			return mbSolid[(piY * UWWorldScale.TilesPerAxis) + piX];
		}
	}
}
