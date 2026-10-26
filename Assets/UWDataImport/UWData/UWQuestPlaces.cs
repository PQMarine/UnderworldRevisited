namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIXED PLACES OF THE QUESTS - tiles the original hands its routines as plain numbers -
	/// in one engine-free place (cleanup phase 1, 2026-10-11), next to their levels in
	/// UWLevelIds. Until then the cup's tile stood in two files and the bullfrog puzzle's numbers
	/// in two. Tile coordinates are 0 to 63 on both axes, as in the level data.
	/// </summary>
	public static class UWQuestPlaces
	{
		// ------------------------------------------------- Level 3: the Cup of Wonder

		/// <summary>Where the Cup of Wonder lies on UWLevelIds.CupOfWonder, handed to the
		/// shrine's direction routine as fixed numbers (label 5AA pushes 4, 3, 0x2D, 0x18).</summary>
		public const int CupTileX = 24;

		public const int CupTileY = 45;

		// ------------------------------------------------- Level 4: the bullfrog puzzle

		/// <summary>The bullfrog field on UWLevelIds.Bullfrog spans 48 to 55 on both axes.</summary>
		public const int BullfrogGridOrigin = 48;

		public const int BullfrogGridSize = 8;

		/// <summary>Game variables of the bullfrog puzzle: 24 the X choice, 25 the Y choice, 26 the
		/// remaining attempts. A fresh game starts with 53 attempts - the only
		/// game variable the original initialises to a non-zero value at all.</summary>
		public const int BullfrogVarX = 24;

		public const int BullfrogVarY = 25;

		public const int BullfrogVarRetries = 26;

		public const int BullfrogRetriesAtStart = 53;

		public const int BullfrogRetriesOnReset = 0x3F;

		// ------------------------------------------------- Level 7: Tyball

		/// <summary>The tile of Tyball's prison, whose move triggers go with him when he dies.</summary>
		public const int TyballPrisonTileX = 0x17;

		public const int TyballPrisonTileY = 0x38;

		// ------------------------------------------------- Level 8: the volcano

		/// <summary>The volcano's centre on UWLevelIds.Volcano, at 32/32, the lava within six
		/// tiles of it (Manhattan distance).</summary>
		public const int VolcanoCentre = 32;

		public const int VolcanoRadius = 6;

		// ------------------------------------------------- The burial of Garamon

		/// <summary>The trigger that fires after the burial conversation, tile 54/52.</summary>
		public const int BurialTriggerTileX = 0x36;

		public const int BurialTriggerTileY = 0x34;

		// ------------------------------------------------- Level 9: the void

		/// <summary>Where the moongate puts the player down in the void, 27/23.</summary>
		public const int VoidTileX = 0x1B;

		public const int VoidTileY = 0x17;

		// ------------------------------------------------- Level 1: the start

		/// <summary>Where a new character starts - the middle of the starting room on level 1
		/// (UWPlayerData.NewCharacter).</summary>
		public const int StartTileX = 32;

		public const int StartTileY = 2;
	}
}
