namespace UWDataImport.UWData
{
	/// <summary>
	/// THE DUNGEON LEVELS THE RULES KNOW BY NUMBER, in one engine-free place (cleanup phase 1,
	/// 2026-10-11). All ZERO-BASED, as the port counts its levels (UWLevelLoader.CurrentLevelIndex,
	/// DataImport.Levels). The original and PLAYER.DAT 0x5C count from one; a rule that mirrors
	/// such a comparison takes Number(index). Until then the Void was 8 in three files and 9 in
	/// two, the cup's level 2 in one file and 3 in another.
	/// </summary>
	public static class UWLevelIds
	{
		/// <summary>The 1-based level number of a 0-based index - the form the original, the
		/// save game and the hosts that document themselves as one-based use.</summary>
		public static int Number(int piIndex)
		{
			return piIndex + 1;
		}

		/// <summary>Level 2: a new game's moonstone lies there (PLAYER.DAT keeps the level
		/// one-based, see UWPlayerData.MoonstoneLevel).</summary>
		public const int NewMoonstone = 1;

		/// <summary>Level 3 holds the Cup of Wonder (UWQuestPlaces.CupTileX/Y): the mandolin's
		/// melody and the shrine's direction both point there.</summary>
		public const int CupOfWonder = 2;

		/// <summary>Level 4 holds the bullfrog field (UWQuestPlaces.BullfrogGridOrigin).</summary>
		public const int Bullfrog = 3;

		/// <summary>Level 7: Tyball's lair and maze. The reference checks dungeon_level == 7 and
		/// counts from one.</summary>
		public const int Lair = 6;

		/// <summary>Level 8 holds the volcano (UWQuestPlaces.VolcanoCentre).</summary>
		public const int Volcano = 7;

		/// <summary>Level 9 is the Ethereal Void: no magic, no sleep, drawn unlit, nothing
		/// comes to rest there.</summary>
		public const int Void = 8;
	}
}
