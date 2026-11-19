namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ETHEREAL VOID (UW2 plan G6, the rest after part 5, 2026-11-17), from Underworld Exhumed's uw2 game/SKILLS.C
	/// (dream, go_void, punt_void, do_dreamret, player_sleep, player_is_dead), game/PLAYTIME.C (duration_check),
	/// obj/USEITEMS.C (the dream plant), event/WORLDEV.C (do_teleport) and ui (the asleep player's locks).
	///
	/// Eating the dream plant (0x114) outside the Void sets sleepbits to 2 to 5; the next night's dream is then the Void:
	/// "Your dreams are vivid...", quest 48, and go_void stores where the player lay (dream_x, dream_y, dream_pos = level
	/// &lt;&lt; 8 plus the heading byte), draws sleepbits anew and sends him to level 69 - to 32/28 five times in eight, else
	/// one of three squares moved by up to one. Every duration check counts sleepbits down; at 0 in the Void, sleeping in
	/// the Void, or dying there, punt_void wakes him: "You awaken after a night of extremely lifelike dreams...", back to
	/// the stored square and heading. While asleep in the Void (sleepbits and in_void) the inventory, the panel and the
	/// fight are shut and no teleport leads out of the Void's world.
	///
	/// KILLORN'S COUNTDOWN lives here too, as it shares duration_check's spot: with quest 50 set and 54 not, every check
	/// shakes the screen (do_sfx(4, 0x2C)) and, time running, counts quest byte 6 down; past 0 the keep crashes
	/// (Killorn_just_crashed(0)) and the player takes 0xFF.
	///
	/// NOT HERE: do_dreamret's music (10, UWSleepUw2.WakeFromVoid); the Void's motion state (motionbits 0x10, the physics' frame-rate
	/// state 5).
	/// </summary>
	public static class UWVoidRulesUw2
	{
		public const int VoidLevel = 0x45;

		public const int VoidWorld = 8;

		public const int DreamPlantId = 0x114;

		/// <summary>quests[12] bit 0: the Void dreamt.</summary>
		public const int VoidDreamtQuest = 48;

		/// <summary>"Your dreams are vivid, showing a shifting colored scene..." and "You awaken after a night of extremely
		/// lifelike dreams..." (block 1, the original's numbers).</summary>
		public const int VividDreamString = 0x18;

		public const int AwakenString = 0x19;

		/// <summary>The word at 0x62: sleepbits in bits 6 to 8.</summary>
		public const int SleepWordOffset = 0x62;

		public const int SleepBitsShift = 6;

		public const int SleepBitsMask = 7;

		/// <summary>dream_x, dream_y, dream_pos (level &lt;&lt; 8 plus the heading byte).</summary>
		public const int DreamXOffset = 0x2FB;

		public const int DreamYOffset = 0x2FD;

		public const int DreamPosOffset = 0x2FF;

		/// <summary>void_tiles: where go_void puts the player on level 69.</summary>
		public static readonly int[][] VoidTiles = { new[] { 32, 28 }, new[] { 25, 13 }, new[] { 38, 32 }, new[] { 14, 40 } };

		// ------------------------------------------------- The record

		public static int SleepBits(UWPlayerFile pOFile) => pOFile == null ? 0 : (UWBinary.ReadU16(pOFile.Record, SleepWordOffset) >> SleepBitsShift) & SleepBitsMask;

		public static void SetSleepBits(UWPlayerFile pOFile, int piValue)
		{
			int liWord = UWBinary.ReadU16(pOFile.Record, SleepWordOffset) & ~(SleepBitsMask << SleepBitsShift);

			UWBinary.Write16(pOFile.Record, SleepWordOffset, liWord | ((piValue & SleepBitsMask) << SleepBitsShift));
		}

		public static void SetInVoid(UWPlayerFile pOFile, bool pbIn)
		{
			if (pbIn)
				pOFile.Record[UWPlayerRecordUw2.PitsByteOffset] |= UWPlayerRecordUw2.VoidBit;
			else
				pOFile.Record[UWPlayerRecordUw2.PitsByteOffset] &= unchecked((byte)~UWPlayerRecordUw2.VoidBit);
		}

		/// <summary>sleepbits and in_void: asleep in the Void - the panel, the inventory and the fight are shut.</summary>
		public static bool IsAsleepInVoid(UWPlayerFile pOFile) => SleepBits(pOFile) != 0 && UWPlayerRecordUw2.IsInVoid(pOFile);

		public static int DreamX(UWPlayerFile pOFile) => (short)UWBinary.ReadU16(pOFile.Record, DreamXOffset);

		public static int DreamY(UWPlayerFile pOFile) => (short)UWBinary.ReadU16(pOFile.Record, DreamYOffset);

		public static int DreamLevel(UWPlayerFile pOFile) => (UWBinary.ReadU16(pOFile.Record, DreamPosOffset) >> 8) & 0xFF;

		public static int DreamHeadingByte(UWPlayerFile pOFile) => UWBinary.ReadU16(pOFile.Record, DreamPosOffset) & 0xFF;

		// ------------------------------------------------- The rules

		/// <summary>The dream plant eaten outside the Void: sleepbits 2 to 5 (piRoll is rand()).</summary>
		public static bool EatDreamPlant(UWPlayerFile pOFile, int piRoll)
		{
			if (pOFile == null || UWPlayerRecordUw2.IsInVoid(pOFile))
				return false;

			SetSleepBits(pOFile, (piRoll % 4) + 2);

			return true;
		}

		/// <summary>dream: the night's dream is the Void when sleepbits are set.</summary>
		public static bool DreamIsVoid(UWPlayerFile pOFile) => SleepBits(pOFile) != 0;

		/// <summary>
		/// The Void's dream and go_void: quest 48, sleepbits drawn anew (piRoll1 % 4 + 2), in the Void, the place stored
		/// (piLevel, piTileX/Y, piHeadingByte), then the square on level 69 - void_tiles[piRoll2 &amp; 7], 0 from 4 up, the
		/// others moved by piRoll3 &amp; 1 and piRoll4 &amp; 1. Returns the square.
		/// </summary>
		public static void GoVoid(UWPlayerFile pOFile, int piLevel, int piTileX, int piTileY, int piHeadingByte,
			int piRoll1, int piRoll2, int piRoll3, int piRoll4, out int piX, out int piY)
		{
			UWPlayerRecordUw2.SetQuest(pOFile, VoidDreamtQuest, 1);
			SetSleepBits(pOFile, (piRoll1 % 4) + 2);
			SetInVoid(pOFile, true);
			UWBinary.Write16(pOFile.Record, DreamXOffset, piTileX & 0xFFFF);
			UWBinary.Write16(pOFile.Record, DreamYOffset, piTileY & 0xFFFF);
			UWBinary.Write16(pOFile.Record, DreamPosOffset, ((piLevel & 0xFF) << 8) | (piHeadingByte & 0xFF));

			int liN = piRoll2 & 7;

			if (liN >= 4)
				liN = 0;

			piX = VoidTiles[liN][0];
			piY = VoidTiles[liN][1];

			if (liN != 0)
			{
				piX += piRoll3 & 1;
				piY += piRoll4 & 1;
			}
		}

		/// <summary>duration_check: sleepbits one down; true when they reached 0 in the Void (punt_void is due).</summary>
		public static bool TickSleep(UWPlayerFile pOFile)
		{
			int liBits = SleepBits(pOFile);

			if (liBits == 0)
				return false;

			SetSleepBits(pOFile, liBits - 1);

			return liBits - 1 == 0 && UWPlayerRecordUw2.IsInVoid(pOFile);
		}

		/// <summary>player_sleep in the Void's world while in the Void: no night, punt_void.</summary>
		public static bool SleepWakes(UWPlayerFile pOFile, int piLevel)
		{
			return (piLevel - 1) / UWLevelHacksUw2.LevelsPerWorld == VoidWorld && UWPlayerRecordUw2.IsInVoid(pOFile);
		}

		/// <summary>punt_void's record part: out of the Void, sleepbits 0. The way back is DreamLevel, DreamX/Y and the
		/// heading byte.</summary>
		public static void Wake(UWPlayerFile pOFile)
		{
			SetInVoid(pOFile, false);
			SetSleepBits(pOFile, 0);
		}

		/// <summary>do_teleport: asleep in the Void, a teleport of the player to a level outside the Void's world is refused
		/// (a destination 0, this level, is not).</summary>
		public static bool TeleportRefused(UWPlayerFile pOFile, int piDestLevel)
		{
			return IsAsleepInVoid(pOFile) && piDestLevel != 0 && (piDestLevel - 1) / UWLevelHacksUw2.LevelsPerWorld != VoidWorld;
		}

		// ------------------------------------------------- Killorn's countdown

		/// <summary>quests[13] bit 2 (quest 54) and the countdown's quest byte 6 (quest 128 + 6).</summary>
		public const int CountdownQuest = 0x80 + 6;

		public enum CountdownEnum
		{
			None,

			Shake,

			Crash
		}

		/// <summary>
		/// duration_check's Killorn block: with quest 50 and not 54 the screen shakes (do_sfx(4, 0x2C)); with time running
		/// quest byte 6 counts down, and when it was 0 already (the original's post-decrement, which leaves 0xFF) the keep
		/// crashes: Killorn_just_crashed(0) - quest 54, XC_CHANGED, the schedules - and 0xFF damage to the player, the
		/// caller's.
		/// </summary>
		public static CountdownEnum KillornCountdown(UWPlayerFile pOFile, bool pbTimeStopped)
		{
			if (pOFile == null || UWPlayerRecordUw2.GetQuest(pOFile, UWLevelHacksUw2.KeepFallsQuest) == 0
				|| UWPlayerRecordUw2.GetQuest(pOFile, UWLevelHacksUw2.KeepCrashedQuest) != 0)
				return CountdownEnum.None;

			if (pbTimeStopped)
				return CountdownEnum.Shake;

			int liCount = UWPlayerRecordUw2.GetQuest(pOFile, CountdownQuest);

			UWPlayerRecordUw2.SetQuest(pOFile, CountdownQuest, (liCount - 1) & 0xFF);

			if (liCount != 0)
				return CountdownEnum.Shake;

			UWLevelHacksUw2.KeepCrashed(pOFile);

			return CountdownEnum.Crash;
		}

		/// <summary>do_sfx(4, 0x2C): the shake of the countdown and of the counted one's death.</summary>
		public const int CountdownShake = 0x2C << 1;

		/// <summary>damage_item(ThePlayer, 0, 0, 0, 0xFF, 0) when the keep crashes.</summary>
		public const int CrashDamage = 0xFF;
	}
}
