using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A spot in the level in the original's units: the tile, the eighth of a tile within it on each
	/// axis (0 to 7, as an object's xpos and ypos) and a height in zpos steps. Cleanup step 3.1
	/// (2026-10-17): until then every host derived "where the player is" on its own, from the camera,
	/// the character's transform, the feet or the controller (eight derivations, section 4.2 of
	/// Docs/CLEANUP-PLAN.md). IUWPlayerAvatar now hands them out as spots, so the rules see one
	/// shape; the motion core's own record becomes the single source in a later step.
	///
	/// ZPos is not clamped here: whoever makes a spot says which height it means (the floor under
	/// a point, the feet) and whether it clamps (see IUWPlayerAvatar).
	/// </summary>
	public readonly struct UWSpot : IEquatable<UWSpot>
	{
		/// <summary>No spot: tile -1/-1, as the trap host answered for the player's tile without a
		/// camera.</summary>
		public static readonly UWSpot Nowhere = new UWSpot(new UWTilePos(-1, -1), 0, 0, 0);

		public readonly UWTilePos Tile;

		/// <summary>The eighth within the tile towards the east, 0 to 7.</summary>
		public readonly int FineX;

		/// <summary>The eighth within the tile towards the north, 0 to 7.</summary>
		public readonly int FineY;

		/// <summary>The height in zpos steps (0 to 127 inside the level).</summary>
		public readonly int ZPos;

		public UWSpot(UWTilePos pOTile, int piFineX, int piFineY, int piZPos)
		{
			Tile = pOTile;
			FineX = piFineX & 7;
			FineY = piFineY & 7;
			ZPos = piZPos;
		}

		/// <summary>A spot from absolute eighths (tile times eight plus the fine position), the unit
		/// the original measures reach and closeness in.</summary>
		public static UWSpot FromEighths(int piEighthsX, int piEighthsY, int piZPos)
		{
			return new UWSpot(new UWTilePos(piEighthsX >> 3, piEighthsY >> 3), piEighthsX & 7, piEighthsY & 7, piZPos);
		}

		/// <summary>Whether the spot lies on the map - false for Nowhere.</summary>
		public bool IsOnMap => Tile.IsOnMap;

		/// <summary>Tile times eight plus the fine position, towards the east.</summary>
		public int EighthsX => (Tile.X << 3) + FineX;

		/// <summary>Tile times eight plus the fine position, towards the north.</summary>
		public int EighthsY => (Tile.Y << 3) + FineY;

		/// <summary>The height as a creature record keeps it, zpos &gt;&gt; 3.</summary>
		public int FloorLevel => ZPos >> 3;

		public static bool operator ==(UWSpot pOA, UWSpot pOB)
		{
			return pOA.Tile == pOB.Tile && pOA.FineX == pOB.FineX && pOA.FineY == pOB.FineY && pOA.ZPos == pOB.ZPos;
		}

		public static bool operator !=(UWSpot pOA, UWSpot pOB)
		{
			return !(pOA == pOB);
		}

		public bool Equals(UWSpot pOOther)
		{
			return this == pOOther;
		}

		public override bool Equals(object pOOther)
		{
			return pOOther is UWSpot lOSpot && this == lOSpot;
		}

		public override int GetHashCode()
		{
			return (((Tile.GetHashCode() * 397) ^ (FineX << 3) ^ FineY) * 397) ^ ZPos;
		}

		public override string ToString()
		{
			return Tile + " at " + FineX + "/" + FineY + " z " + ZPos;
		}
	}
}
