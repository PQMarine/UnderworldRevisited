namespace UWDataImport.UWData
{
	/// <summary>
	/// The measures of our world, in one engine-free place (P0 of the engine separation,
	/// 2026-09-17). Until then they lived on UWLevelMeshBuilder and UWObjectSpawner, so every
	/// rule that needed a tile size had to reach into the Unity build code.
	///
	/// OUR WORLD UNIT is a quarter of the original's: a tile is 64 of ours and 256 of the
	/// original, a sub-tile step 8 against 32, a zpos step 2 against 8. See UWUnits for the
	/// conversions.
	/// </summary>
	public static class UWWorldScale
	{
		/// <summary>Tiles per map axis.</summary>
		public const int TilesPerAxis = 64;

		/// <summary>Edge length of a tile, and distance between two tile centres.</summary>
		public const float TileSize = 64f;

		/// <summary>Half the edge length of a tile. Tile n has its centre at n * TileSize, so its
		/// lower corner lies half a tile before that.</summary>
		public const float TileHalfSize = 32f;

		/// <summary>Height of the ceiling above height zero.</summary>
		public const float CeilingHeight = 256f;

		/// <summary>One sub-tile step (xpos/ypos 0 to 7): an eighth of a tile. The highest value
		/// does not reach the tile edge, 7 * 8 is 56 of 64.</summary>
		public const float SubTileStep = 8f;

		/// <summary>Where an object stands within its eighth: 0xF of the 32 original units of a
		/// step. The original computes a position as (tile &lt;&lt; 8) + (xpos &lt;&lt; 5) + 0xF.</summary>
		public const float SubTileOffset = 3.75f;

		/// <summary>One zpos step (0 to 127). A floor height level is 8 zpos steps, 16 units.</summary>
		public const float ZPosStep = 2f;

		/// <summary>One unit of the original in our units.</summary>
		public const float UnitsPerOriginalUnit = 0.25f;

		/// <summary>A full circle in the original's angle measure.</summary>
		public const float FullCircleUnits = 65536f;
	}
}
