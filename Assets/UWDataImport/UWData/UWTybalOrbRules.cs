namespace UWDataImport.UWData
{
	/// <summary>
	/// TYBAL'S ORB and what hangs on it, read 2026-09-23 after the marker audit found the
	/// orb's bit read and written nowhere (Todo.md section 0b, row 1).
	///
	/// THE BIT: PLAYER.DAT 0x60 bit 5, set when the orb rock breaks the orb (see
	/// UWItemApplications.UseOrbRock), cleared for a new character.
	///
	/// THE LAIR DRAINS MANA while the orb stands (LevelChangeEvents_ovr140_906, the level 7
	/// branch):
	///   entering level 7: the maximum mana goes into PLAYER.DAT 0xB0, maximum and current
	///   mana become 0 - no spell can be cast down there;
	///   leaving level 7: the maximum comes back from 0xB0, the current mana is a QUARTER of it
	///   (ovr140_9AD, sar 2 - per user, 2026-10-02: ours filled it up, the original does not);
	///   breaking the orb: the bit is set and the maximum AND the current mana come back from
	///   0xB0 (read wrong at first, the current one stayed empty - per user), and Tybal loses
	///   half his hit points for good (TybalWhoAmI).
	/// Once the orb is broken none of this happens any more.
	///
	/// THE MAGES OF THE LAIR (kind 0x13), while the orb stands and they are on level 7:
	/// stand-off 1 and no spells (the goal 5 attack and the two magic routines check the level,
	/// the bit and the kind) - the brain asks OrbStands through ICritterHost.TybalOrbStands.
	/// NOT TYBAL: he is kind 0x19 and casts with the orb whole, in the original as in ours
	/// (per user, 2026-09-23).
	///
	/// THE ORB IS BROKEN BY THROWING the orb rock at it (per user) - the collision uses the
	/// thrown object on what it hits (UWCommonObjectProperties.UsedWhenThrown); from the hand
	/// the rock asks for a target the same way.
	///
	/// Until 2026-09-23 ours had none of the mana drain, never set or read the bit, and
	/// asked for level index 7 - which is level 8, the original counts from 1.
	/// </summary>
	public static class UWTybalOrbRules
	{
		/// <summary>Tybal's whoami. Breaking the orb runs over every creature of the level with this
		/// number (RunFunctionOnWhoAmIList_seg038_3307_D4E): hit points halved plus one
		/// (WeakenedHitPoints) and word 0x0D bit 9, no healing on leaving the level
		/// (UWCritterRecord.NoLevelExitHealing) - he stays weak.</summary>
		public const int TybalWhoAmI = 231;

		public static int WeakenedHitPoints(int piHitPoints)
		{
			return (piHitPoints / 2) + 1;
		}

		/// <summary>Tybal's lair, level 7 - zero-based, as the port counts levels.</summary>
		public const int LairLevelIndex = 6;

		/// <summary>
		/// WHERE A NEW MAXIMUM MANA GOES (ovr143_95, read 2026-10-01; per user: on level 7 a
		/// mantra for Mana made the flask read "0 of 24" and the mana then regenerated, in the
		/// original it stays "0 of 0"). After a level-up and a skill gain UW.EXE recomputes the
		/// maximum, ((Mana + 1) * intelligence) >> 3, and writes it into PLAYER.DAT 0xB0, the
		/// backup, when the dungeon level is 7 - otherwise into the maximum. Leaving the lair
		/// or breaking the orb then hands it out. Ours wrote the maximum everywhere.
		///
		/// UW.EXE tests the level only, not the orb's bit: with the orb broken a gain on level 7
		/// would vanish into the backup until the next recomputation elsewhere. Ours keeps it
		/// aside only while the orb stands - a deliberate deviation (per user, 2026-10-02:
		/// "looks like a bug"; Todo.md section 8).
		/// </summary>
		public static bool KeepsMaxManaAside(int piLevelIndex)
		{
			return OrbStands(piLevelIndex);
		}

		/// <summary>Does the orb still stand, and is this its level? For Tybal's rule.</summary>
		public static bool OrbStands(int piLevelIndex)
		{
			return piLevelIndex == LairLevelIndex && !UWGameFlags.OrbDestroyed;
		}

		/// <summary>
		/// A real level change (not a jump within a level, not loading a save): leaving the lair
		/// gives the mana back, entering it takes it. Both only while the orb stands.
		/// </summary>
		public static void OnLevelChange(UWPlayerVitals pOVitals, int piFromIndex, int piToIndex)
		{
			if (pOVitals == null || UWGameFlags.OrbDestroyed || piFromIndex == piToIndex)
				return;

			if (piFromIndex == LairLevelIndex)
				pOVitals.SetMana(UWGameFlags.OrbManaBackup, ManaOnLeavingLair(UWGameFlags.OrbManaBackup));

			if (piToIndex == LairLevelIndex)
			{
				UWGameFlags.OrbManaBackup = (int)pOVitals.MaxMana;
				pOVitals.SetMana(0f, 0f);
			}
		}

		/// <summary>The current mana on leaving the lair: a quarter of the maximum given back
		/// (LevelChangeEvents_ovr140_906, label 9AD).</summary>
		public static int ManaOnLeavingLair(int piMaximum)
		{
			return piMaximum >> 2;
		}

		/// <summary>The orb is broken: the bit, and the mana back from the backup, maximum AND current
		/// (the orb routine writes 0xB0 into both, per user the mana did not come back otherwise).
		/// Tybal is weakened by the host, see WeakenedHitPoints.</summary>
		public static void OnOrbDestroyed(UWPlayerVitals pOVitals)
		{
			if (UWGameFlags.OrbDestroyed)
				return;

			UWGameFlags.OrbDestroyed = true;

			if (pOVitals != null)
				pOVitals.SetMana(UWGameFlags.OrbManaBackup, UWGameFlags.OrbManaBackup);
		}
	}
}
