using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A tile on the map: X to the east, Y to the north, as the original counts them (P0 of the
	/// engine separation, 2026-09-17). Replaces Unity's Vector2Int for tile coordinates, so the
	/// rules can pass tiles around without an engine type. Pixel sizes keep their engine types.
	/// </summary>
	public readonly struct UWTilePos : IEquatable<UWTilePos>
	{
		public readonly int X;

		public readonly int Y;

		public UWTilePos(int piX, int piY)
		{
			X = piX;
			Y = piY;
		}

		/// <summary>Whether the tile lies on the map.</summary>
		public bool IsOnMap
		{
			get
			{
				return X >= 0 && Y >= 0 && X < UWWorldScale.TilesPerAxis && Y < UWWorldScale.TilesPerAxis;
			}
		}

		/// <summary>Index into a level's tile array (row by row, Y then X).</summary>
		public int Index
		{
			get { return (Y * UWWorldScale.TilesPerAxis) + X; }
		}

		public static UWTilePos operator +(UWTilePos pOA, UWTilePos pOB)
		{
			return new UWTilePos(pOA.X + pOB.X, pOA.Y + pOB.Y);
		}

		public static UWTilePos operator -(UWTilePos pOA, UWTilePos pOB)
		{
			return new UWTilePos(pOA.X - pOB.X, pOA.Y - pOB.Y);
		}

		public static UWTilePos operator *(UWTilePos pOA, int piFactor)
		{
			return new UWTilePos(pOA.X * piFactor, pOA.Y * piFactor);
		}

		public static bool operator ==(UWTilePos pOA, UWTilePos pOB)
		{
			return pOA.X == pOB.X && pOA.Y == pOB.Y;
		}

		public static bool operator !=(UWTilePos pOA, UWTilePos pOB)
		{
			return !(pOA == pOB);
		}

		public bool Equals(UWTilePos pOOther)
		{
			return this == pOOther;
		}

		public override bool Equals(object pOOther)
		{
			return pOOther is UWTilePos lOTile && this == lOTile;
		}

		public override int GetHashCode()
		{
			return (X * 397) ^ Y;
		}

		public override string ToString()
		{
			return X + "/" + Y;
		}
	}
}
