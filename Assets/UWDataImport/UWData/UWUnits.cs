using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Conversions between the original's units, tiles and our world units, per axis and without
	/// any engine vector type (P0 of the engine separation, 2026-09-17). UWViewpoint builds its
	/// Unity vectors from these.
	///
	/// THE AXES: the original counts X to the east and Y to the north; in our world north is +Z
	/// and height is +Y. The functions here are per axis, so the caller decides which world axis
	/// a value lands on.
	///
	/// ROUNDING: to the nearest integer with halves to even, exactly what Unity's
	/// Mathf.RoundToInt did before (Math.Round). A C++ port has to reproduce that on purpose,
	/// std::round rounds halves away from zero.
	/// </summary>
	public static class UWUnits
	{
		/// <summary>The world coordinate of the lower tile corner on one axis - the zero point from
		/// which the sub-tile position counts.</summary>
		public static float TileToWorldAxis(int piTile)
		{
			return (piTile * UWWorldScale.TileSize) - UWWorldScale.TileHalfSize;
		}

		/// <summary>The world coordinate of a tile centre on one axis.</summary>
		public static float TileCentreToWorldAxis(int piTile)
		{
			return piTile * UWWorldScale.TileSize;
		}

		/// <summary>Which tile a world coordinate lies in, on one axis. Not clamped to the map.</summary>
		public static int WorldAxisToTile(float pfWorld)
		{
			return (int)Math.Floor((pfWorld + UWWorldScale.TileHalfSize) / UWWorldScale.TileSize);
		}

		/// <summary>Where an object with this tile and sub-tile position (0 to 7) stands on one
		/// axis, including the offset within its eighth.</summary>
		public static float SubTileToWorldAxis(int piTile, int piSubTile)
		{
			return TileToWorldAxis(piTile) + (piSubTile * UWWorldScale.SubTileStep) + UWWorldScale.SubTileOffset;
		}

		/// <summary>Height of a zpos value (0 to 127).</summary>
		public static float ZPosToWorld(int piZPos)
		{
			return piZPos * UWWorldScale.ZPosStep;
		}

		/// <summary>A horizontal coordinate of the original (tile &lt;&lt; 8 plus fine position) on
		/// one axis.</summary>
		public static float OriginalToWorldAxis(int piOriginal)
		{
			return (piOriginal * UWWorldScale.UnitsPerOriginalUnit) - UWWorldScale.TileHalfSize;
		}

		/// <summary>A height of the original.</summary>
		public static float OriginalToWorldHeight(int piOriginal)
		{
			return piOriginal * UWWorldScale.UnitsPerOriginalUnit;
		}

		public static int WorldAxisToOriginal(float pfWorld)
		{
			return RoundToInt((pfWorld + UWWorldScale.TileHalfSize) / UWWorldScale.UnitsPerOriginalUnit);
		}

		public static int WorldHeightToOriginal(float pfWorld)
		{
			return RoundToInt(pfWorld / UWWorldScale.UnitsPerOriginalUnit);
		}

		/// <summary>A heading value of the original in degrees, read without sign: heading 4
		/// (4 &lt;&lt; 13) wraps to -32768 in a short, as an angle that is the same 180 degrees.</summary>
		public static float AngleToDegrees(int piAngle)
		{
			return ((ushort)piAngle) * 360f / UWWorldScale.FullCircleUnits;
		}

		public static short DegreesToAngle(float pfDegrees)
		{
			return (short)(int)(Repeat(pfDegrees, 360f) * UWWorldScale.FullCircleUnits / 360f);
		}

		/// <summary>A pitch value of the original in degrees, sign inverted: the original looks up
		/// with a positive value, our camera looks down with a positive angle.</summary>
		public static float PitchToDegrees(int piPitch)
		{
			return -piPitch * 360f / UWWorldScale.FullCircleUnits;
		}

		/// <summary>Round to the nearest integer, halves to even - see class comment.</summary>
		public static int RoundToInt(float pfValue)
		{
			return (int)Math.Round(pfValue);
		}

		/// <summary>Wraps a value into 0 to length, also for negative values.</summary>
		public static float Repeat(float pfValue, float pfLength)
		{
			float lfResult = pfValue - ((float)Math.Floor(pfValue / pfLength) * pfLength);

			return lfResult < 0f ? 0f : (lfResult > pfLength ? pfLength : lfResult);
		}
	}
}
