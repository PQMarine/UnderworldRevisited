namespace UWDataImport.UWData
{
	/// <summary>
	/// THE AUTOMAP BYTE OF EACH GAME (phase 1 step 2; UE FORMATS.md "The automap and map notes"): bits
	/// 0-3 the tile type in both (0 never seen, 1-9 the type, 0xA-0xF in view but not seen); the upper
	/// two fields the games keep the other way round - the first game has the floor's terrain (1 water,
	/// 2 lava) in bits 4-5 and the kind (1 door, 2 bridge, 3 stairs) in 6-7 (UW1 GRIDDB.C:562, :641;
	/// UWLevel's MapDisplay values), the second the kind in 4-5 and the terrain class (1 water, 2 lava,
	/// 3 ice) in 6-7 (GRIDDB.C:542, :621).
	/// </summary>
	public static class UWAutomapCode
	{
		public const int KindDoor = 1;

		public const int KindBridge = 2;

		public const int KindStairs = 3;

		/// <summary>The second game's terrain marks of a stair texture (UWTerrainUw2.Mark).</summary>
		public const int Uw2StairsMarkUp = 3;

		public const int Uw2StairsMarkDown = 4;

		/// <summary>
		/// THE BYTE AS EACH GAME WRITES IT (UW2 plan phase 2 step 4): the tile type in bits 0-3, and the
		/// kind (1 door, 2 bridge, 3 stairs) and the floor's terrain (1 water, 2 lava; the second game's 3
		/// ice) in the upper nibble - the first game the terrain in bits 4-5 and the kind in 6-7, the
		/// second the other way round (Underworld Exhumed docs/FORMATS.md "The automap and map notes";
		/// uw2 3d/GRIDDB.C:542, :621).
		/// </summary>
		public static byte Compose(int piType, int piKind, int piTerrain, UWGame peGame)
		{
			int liUpper = peGame == UWGame.Uw1 ? (piTerrain & 3) | ((piKind & 3) << 2) : (piKind & 3) | ((piTerrain & 3) << 2);

			return (byte)((liUpper << 4) | (piType & 0xF));
		}

		public static int TileType(int piByte) => piByte & 0xF;

		/// <summary>1 door, 2 bridge, 3 stairs, 0 none.</summary>
		public static int Kind(int piByte, UWGame peGame) => peGame == UWGame.Uw1 ? (piByte >> 6) & 3 : (piByte >> 4) & 3;

		/// <summary>The floor's terrain: 1 water, 2 lava, and in the second game 3 ice; 0 none.</summary>
		public static int Terrain(int piByte, UWGame peGame) => peGame == UWGame.Uw1 ? (piByte >> 4) & 3 : (piByte >> 6) & 3;
	}
}
