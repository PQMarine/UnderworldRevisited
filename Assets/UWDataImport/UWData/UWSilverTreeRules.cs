namespace UWDataImport.UWData
{
	/// <summary>
	/// The silver seed and the silver tree of Ultima Underworld 1, after UW.EXE (per user,
	/// 2026-09-14: "build the silver tree after UW.EXE"). The rules - where the seed takes root,
	/// the stored level - are engine-free since 2026-09-18 (P3 of the engine separation);
	/// UWSilverTree on the Unity side spawns the tree and the seed.
	///
	///   Planting (Class_4_2_ObjectUsage, case 0x122, PlantSilverSeed_ovr143_12FB): on level 9
	///   "The seed vanishes." and the seed is gone. Otherwise the spot 11/8 tile ahead in the view
	///   direction must be an open tile with a dirt or rough floor (floor textures 5-11, 18-22,
	///   27-31, 35-40) and room for the tree; then the tree (0x1CA) grows there, the level is
	///   stored in PLAYER.DAT 0x5E (upper nibble), "You plant the seed, and a small silver tree
	///   quickly grows." and the seed is used up. Otherwise "The seed does not seem to find space
	///   for roots, and remains inert." and the seed stays.
	///   Taking the tree (seg040 SilverTree): the tree withers, "As you reach for the tree, it
	///   withers away, revealing a silver seed.", a seed goes to the hand and the stored level is
	///   cleared.
	///   Death uses the stored level - see UWDeath.
	/// </summary>
	public static class UWSilverTreeRules
	{
		public const int SeedObjectId = UWObjectIds.SilverSeed;

		public const int TreeObjectId = UWObjectIds.SilverTree;

		/// <summary>Block 1 messages; the game numbers them 9 to 12, our export is one higher.</summary>
		public const int WithersMessage = 9 + 1;

		public const int NoRootsMessage = 10 + 1;

		public const int VanishesMessage = 11 + 1;

		public const int PlantedMessage = 12 + 1;

		/// <summary>Distance of the planting spot ahead of the player: 0x0B eighths of a tile
		/// (GetCoordinateInDirection with 0x0B). The unit is assumed, not verified.</summary>
		public const float PlantDistanceTiles = 11f / 8f;

		/// <summary>Dungeon level (1-based) of the planted tree, 0 for none - kept in UWGameFlags
		/// (PLAYER.DAT 0x5E, upper nibble).</summary>
		public static int TreeLevel
		{
			get { return UWGameFlags.SilverTreeLevel; }
			set { UWGameFlags.SilverTreeLevel = value; }
		}

		/// <summary>Floor textures the seed takes root in (PlantSilverSeed: terrain 5-11, 18-22,
		/// 27-31, 35-40 - all dirt and rough floors).</summary>
		public static bool IsSoil(int piFloorTexture)
		{
			return (piFloorTexture >= 5 && piFloorTexture <= 11)
				|| (piFloorTexture >= 18 && piFloorTexture <= 22)
				|| (piFloorTexture >= 27 && piFloorTexture <= 31)
				|| (piFloorTexture >= 35 && piFloorTexture <= 40);
		}

		/// <summary>
		/// The tile half of plant_seed (SKILLS.C): an open tile with soil. The other half is
		/// can_place for the tree at the exact spot (the host's fit test, not a tile scan): per user
		/// in the original 2026-10-17 (audit round 2 B10, level 1, a barrel at 56/60), the seed aimed
		/// at the barrel "does not seem to find space for roots", a step aside it grows beside it.
		/// Until then ours refused a whole tile with a creature or tree and planted into furniture.
		/// </summary>
		public static bool CanTakeRoot(UWLevel pOLevel, UWTilePos pOTile)
		{
			UWTile lOTile = pOLevel?.GetTile(pOTile.X, pOTile.Y);

			return lOTile != null && lOTile.TileType == UWTile.TileTypeEnum.open && IsSoil(lOTile.TextureFloor);
		}

		/// <summary>The height plant_seed places the tree at and asks can_place with: the tile's
		/// floor (height &lt;&lt; 3).</summary>
		public static int PlantZ(UWTile pOTile)
		{
			return pOTile != null ? (pOTile.FloorHeight >> 4) << 3 : 0;
		}
	}
}
