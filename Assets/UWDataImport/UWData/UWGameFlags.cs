namespace UWDataImport.UWData
{
	/// <summary>
	/// The few pieces of world state that live neither in the level nor in the player's
	/// numbers but in PLAYER.DAT's flag bytes: the two endgame bits of 0x62, the Cup of
	/// Wonder bit of 0x60 and the silver tree nibble of 0x5E. Until 2026-09-18 they were
	/// statics on UWEndgame and UWSilverTreeRules, each seeded and saved separately;
	/// now one place is seeded from a save game or a new character and read back by the
	/// save game writer.
	///
	/// SINCE 2026-10-14 (cleanup step 2.7) the flags belong to the session
	/// (UWGameSession.Story, a UWStoryFlags); this class forwards to the current session until
	/// its callers take the session themselves.
	/// </summary>
	public static class UWGameFlags
	{
		/// <summary>PLAYER.DAT 0x62 bit 2: talismans can be destroyed in the volcano.</summary>
		public static bool TalismansDestroyable
		{
			get { return UWGameSession.Current.Story.TalismansDestroyable; }
			set { UWGameSession.Current.Story.TalismansDestroyable = value; }
		}

		/// <summary>PLAYER.DAT 0x62 bit 3: Garamon is buried.</summary>
		public static bool GaramonBuried
		{
			get { return UWGameSession.Current.Story.GaramonBuried; }
			set { UWGameSession.Current.Story.GaramonBuried = value; }
		}

		/// <summary>PLAYER.DAT 0x60 bit 7: the Cup of Wonder has appeared.</summary>
		public static bool CupOfWonderFound
		{
			get { return UWGameSession.Current.Story.CupOfWonderFound; }
			set { UWGameSession.Current.Story.CupOfWonderFound = value; }
		}

		/// <summary>PLAYER.DAT 0x60 bit 6: the Key of Truth has been handed over.</summary>
		public static bool KeyOfTruthGiven
		{
			get { return UWGameSession.Current.Story.KeyOfTruthGiven; }
			set { UWGameSession.Current.Story.KeyOfTruthGiven = value; }
		}

		/// <summary>PLAYER.DAT 0x60 bit 5: Tybal's orb is destroyed.</summary>
		public static bool OrbDestroyed
		{
			get { return UWGameSession.Current.Story.OrbDestroyed; }
			set { UWGameSession.Current.Story.OrbDestroyed = value; }
		}

		/// <summary>PLAYER.DAT 0xB0: the maximum mana kept aside in Tybal's lair.</summary>
		public static int OrbManaBackup
		{
			get { return UWGameSession.Current.Story.OrbManaBackup; }
			set { UWGameSession.Current.Story.OrbManaBackup = value; }
		}

		/// <summary>Dungeon level (1-based) of the planted silver tree, 0 for none.</summary>
		public static int SilverTreeLevel
		{
			get { return UWGameSession.Current.Story.SilverTreeLevel; }
			set { UWGameSession.Current.Story.SilverTreeLevel = value; }
		}

		/// <summary>Dungeon level (1-based) the moonstone lies on, 0 when unknown.</summary>
		public static int MoonstoneLevel
		{
			get { return UWGameSession.Current.Story.MoonstoneLevel; }
			set { UWGameSession.Current.Story.MoonstoneLevel = value; }
		}

		/// <summary>From a loaded save game or a new character; null clears everything.</summary>
		public static void SeedFrom(UWPlayerData pOPlayer)
		{
			UWGameSession.Current.Story.SeedFrom(pOPlayer);
		}
	}
}
