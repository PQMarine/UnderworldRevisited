namespace UWDataImport.UWData
{
	/// <summary>
	/// The few pieces of world state that live neither in the level nor in the player's
	/// numbers but in PLAYER.DAT's flag bytes: the two endgame bits of 0x62, the Cup of
	/// Wonder bit of 0x60 and the silver tree nibble of 0x5E. Until 2026-09-18 they were
	/// statics on UWEndgame and UWSilverTreeRules, each seeded and saved separately;
	/// now one place is seeded from a save game or a new character and read back by the
	/// save game writer.
	/// </summary>
	public static class UWGameFlags
	{
		/// <summary>PLAYER.DAT 0x62 bit 2: talismans can be destroyed in the volcano.</summary>
		public static bool TalismansDestroyable { get; set; }

		/// <summary>PLAYER.DAT 0x62 bit 3: Garamon is buried.</summary>
		public static bool GaramonBuried { get; set; }

		/// <summary>PLAYER.DAT 0x60 bit 7: the Cup of Wonder has appeared (see UWInstrumentPlayer).</summary>
		public static bool CupOfWonderFound { get; set; }

		/// <summary>PLAYER.DAT 0x60 bit 6: the Key of Truth has been handed over at the shrine
		/// and is not given a second time (UWShrineRules, the FANLO mantra).</summary>
		public static bool KeyOfTruthGiven { get; set; }

		/// <summary>PLAYER.DAT 0x60 bit 5: Tybal's orb is destroyed (UWTybalOrbRules).</summary>
		public static bool OrbDestroyed { get; set; }

		/// <summary>PLAYER.DAT 0xB0: the maximum mana kept aside in Tybal's lair (UWTybalOrbRules).</summary>
		public static int OrbManaBackup { get; set; }

		/// <summary>Dungeon level (1-based) of the planted silver tree, 0 for none - PLAYER.DAT
		/// 0x5E, upper nibble.</summary>
		public static int SilverTreeLevel { get; set; }

		/// <summary>Dungeon level (1-based) the moonstone lies on, 0 when unknown - PLAYER.DAT
		/// 0x5E, lower nibble (UWMoonstoneRules).</summary>
		public static int MoonstoneLevel { get; set; }

		/// <summary>From a loaded save game or a new character; null clears everything.</summary>
		public static void SeedFrom(UWPlayerData pOPlayer)
		{
			TalismansDestroyable = pOPlayer != null && pOPlayer.TalismansDestroyable;
			GaramonBuried = pOPlayer != null && pOPlayer.GaramonBuried;
			CupOfWonderFound = pOPlayer != null && pOPlayer.CupOfWonderFound;
			KeyOfTruthGiven = pOPlayer != null && pOPlayer.KeyOfTruthGiven;
			OrbDestroyed = pOPlayer != null && pOPlayer.OrbDestroyed;
			OrbManaBackup = pOPlayer != null ? pOPlayer.OrbManaBackup : 0;
			SilverTreeLevel = pOPlayer != null ? pOPlayer.SilverTreeLevel : 0;
			MoonstoneLevel = pOPlayer != null ? pOPlayer.MoonstoneLevel : 0;
		}
	}
}
