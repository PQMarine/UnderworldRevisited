using UWDataImport.UWData;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The crown's maze navigation - the only spell of class 0xD that Underworld 1
	/// knows (the reference: playerdatloop.ApplyMazeNavigation).
	///
	/// WHAT IT DOES: in Tybal's maze on level 7 it recolours ONE floor texture. Exactly the
	/// tiles that form the path carry their own texture table entry there; while the crown
	/// is worn, that entry shows a different image, and the path stands out from the rest.
	/// Otherwise the spell does nothing - no protection, no light, no map.
	///
	/// THE NUMBERS WERE CHECKED IN THE DATA (2026-09-10). The reference swaps the material
	/// of texture table entry 52 between the global textures 224 and 222. Its loader
	/// splits wall from floor textures at 210, so these are floor textures 14 and 12. In
	/// LEV.ARK, entry 52 of level 7 does indeed hold 14.
	///
	/// AND THAT IS WHY THE TEXTURE VALUE ALONE IS NOT ENOUGH: the texture table of level 7 lists
	/// 14 TWICE, in entry 52 and in entry 54. Going by the texture alone would also recolour
	/// the tiles of the second entry and thus show a wrong path.
	/// What counts is the ENTRY NUMBER stored in the tile itself (bits 10 to 13 of the
	/// tile data) - see UWTile.
	/// </summary>
	public static class UWMazeNavigation
	{
		/// <summary>Tybal's maze, level 7 - counted zero-based here. The reference
		/// checks dungeon_level == 7 and counts from one.</summary>
		public const int LevelIndex = 6;

		/// <summary>The floor entry of the texture table that the spell swaps. The reference
		/// names table slot 52; floor entries start at 48, so this is the
		/// fifth.</summary>
		private const int FloorSlot = 4;

		/// <summary>First floor entry in a level's texture table.</summary>
		private const int FirstFloorSlot = 48;

		/// <summary>What the entry shows while the crown is worn - global 222.</summary>
		private const int RevealedFloorTexture = 12;

		/// <summary>Mask and shift of the floor entry in the tile data.</summary>
		private const uint FloorSlotMask = 0x3C00;

		private const int FloorSlotShift = 10;

		/// <summary>A level has this many floor entries. A larger value in the tile
		/// counts as zero - exactly what UWTile does when reading.</summary>
		private const int FloorSlotCount = 10;

		/// <summary>
		/// Recolours the path tiles or restores them.
		///
		/// Changes only the tile data. It becomes visible only once the geometry is rebuilt
		/// - UWLevelLoader takes care of that.
		/// </summary>
		public static void Apply(UWLevel pOLevel, bool pbOn)
		{
			if (pOLevel == null || pOLevel.TileData == null || pOLevel.TextureInfos == null)
				return;

			int liNormal = pOLevel.TextureInfos[FirstFloorSlot + FloorSlot];
			int liWanted = pbOn ? RevealedFloorTexture : liNormal;

			for (int liAt = 0; liAt < pOLevel.TileData.Length; liAt++)
			{
				UWTile lOTile = pOLevel.TileData[liAt];

				if (lOTile == null || fGetFloorSlot(lOTile) != FloorSlot)
					continue;

				lOTile.TextureFloor = (ushort)liWanted;
			}
		}

		/// <summary>Which floor entry this tile uses.</summary>
		private static int fGetFloorSlot(UWTile pOTile)
		{
			int liSlot = (int)((pOTile.RawTileData & FloorSlotMask) >> FloorSlotShift);

			return liSlot >= FloorSlotCount ? 0 : liSlot;
		}
	}
}
