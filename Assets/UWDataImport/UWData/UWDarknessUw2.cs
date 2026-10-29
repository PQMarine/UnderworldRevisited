namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S DL.DAT (Docs/UW2-PLAN.md phase 1 step 4, 2026-10-29; Underworld Exhumed,
	/// docs/FORMATS.md "Palettes and light", PLAYDATA.C:432): a byte per level, 80; the value mod 10 is
	/// a light level, and 10 or more makes it the level's minimum light. The first game has no such file.
	/// </summary>
	public sealed class UWDarknessUw2
	{
		public const string FileName = "DL.DAT";

		public const int LevelCount = 80;

		/// <summary>From this value on, the light level is the level's minimum.</summary>
		public const int MinimumFrom = 10;

		private readonly byte[] myLevels = new byte[LevelCount];

		/// <summary>DL.DAT's bytes; a short file reads 0 for the levels it lacks.</summary>
		public UWDarknessUw2(byte[] pyFile)
		{
			for (int liAt = 0; liAt < LevelCount && pyFile != null && liAt < pyFile.Length; liAt++)
				myLevels[liAt] = pyFile[liAt];
		}

		/// <summary>The stored byte of a 0-based level.</summary>
		public int Raw(int piLevelIndex) => piLevelIndex >= 0 && piLevelIndex < LevelCount ? myLevels[piLevelIndex] : 0;

		public int LightLevel(int piLevelIndex) => Raw(piLevelIndex) % MinimumFrom;

		public bool IsMinimum(int piLevelIndex) => Raw(piLevelIndex) >= MinimumFrom;

		/// <summary>No minimum light (the original's loc_lght of 0xFF, -1 as a signed char).</summary>
		public const int NoMinimum = -1;

		/// <summary>
		/// THE MINIMUM LIGHT ON A TILE (UW2 plan phase 2 step 3; Underworld Exhumed uw2 game/PLAYDATA.C:428,
		/// load_dl, and motion/PHYSICS.C:801-808, change_GrSq): a level whose byte is 10 or more starts
		/// with its light level as the minimum, one below 10 with none. Where the level's light level
		/// is not 0, the tile's light bit (word 0, bit 8; UWTile.RawTileData) turns it round: the
		/// minimum holds on the tiles whose bit differs from the level's "10 or more", none on the
		/// others. The player's light counts where it is higher (set_light of the larger).
		/// </summary>
		public int TileMinimum(int piLevelIndex, bool pbTileLightBit)
		{
			int liLight = LightLevel(piLevelIndex);
			bool lbHigh = IsMinimum(piLevelIndex);

			if (liLight == 0)
				return lbHigh ? liLight : NoMinimum;

			return pbTileLightBit != lbHigh ? liLight : NoMinimum;
		}

		/// <summary>The tile word's light bit (word 0, bit 8; Underworld Exhumed docs/FORMATS.md "The tile").</summary>
		public static bool TileLightBit(uint piTileWord) => ((piTileWord >> 8) & 1) != 0;

		/// <summary>The levels drawn without lighting and ceiling: 65 to 72 other than 68, (level - 1) / 8
		/// == 8 (Underworld Exhumed uw2 3d/GRIDDB.C:202-210, process_grid). 1-based.</summary>
		public static bool IsUnlitLevel(int piLevel) => (piLevel - 1) / 8 == 8 && piLevel != 68;
	}
}
