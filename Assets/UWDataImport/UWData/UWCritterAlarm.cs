namespace UWDataImport.UWData
{
	/// <summary>
	/// The kin alarm: the globals DamageNPC_seg007_1798_3622 writes (54655-54700) when the
	/// damage source resolves to the player and the victim's attitude is not locked - the
	/// victim's kind (table byte 9), its index, its tile, its floor level and the game clock.
	/// NPCBehaviours reads them on every update of every non-passive creature (53514-53600):
	/// same kind (or an ally), within the hearing range in Manhattan tiles, and no older than
	/// 0x200 PIT ticks - then hostile, target confirmed, goal 5 towards the victim's tile.
	///
	/// One instance per level, held by the host (ICritterHost.Alarm) rather than static, so a
	/// level change resets it (341140: index 0, kind 0xFF) and the self-check can build its
	/// own. The original saves it with the game (PLAYER.DAT 0xBA-0xC1, 341052-341140) and
	/// restores it on loading - see SaveInto and RestoreFrom.
	/// </summary>
	public sealed class UWCritterAlarm
	{
		/// <summary>The kind after a reset: matches no creature.</summary>
		public const int NoKind = 0xFF;

		/// <summary>The index of the victim, 0 none.</summary>
		public int Index;

		/// <summary>Table byte 9 of the victim, 0xFF none.</summary>
		public int Kind = NoKind;

		public int TileX;

		public int TileY;

		/// <summary>The victim's zpos &gt;&gt; 3.</summary>
		public int FloorLevel;

		/// <summary>The game clock in PIT ticks at the blow.</summary>
		public long Clock;

		public void Reset()
		{
			Index = 0;
			Kind = NoKind;
			TileX = 0;
			TileY = 0;
			FloorLevel = 0;
			Clock = 0;
		}

		/// <summary>What DamageNPC writes for a blow by the player.</summary>
		public void Record(int piIndex, int piKind, int piTileX, int piTileY, int piFloorLevel, long piClock)
		{
			Index = piIndex;
			Kind = piKind;
			TileX = piTileX;
			TileY = piTileY;
			FloorLevel = piFloorLevel;
			Clock = piClock;
		}

		/// <summary>
		/// What a save keeps of the alarm (SaveGame calls set_creatures_to_saved_game first,
		/// CRITTIME.C): victim at 0xBA, kind at 0xBB, the time at 0xBC as a game clock value,
		/// the tile at 0xC0/0xC1. Our Clock counts on the critter clock, which starts at 0 with
		/// every level build, so the time is written as the game clock minus the alarm's age.
		/// Without a victim only victim and kind are written; the original keeps the stale
		/// time and tile there too (init_level_creature_stuff clears only crithit and typehit).
		/// The victim's height is not saved (hitpz is not in the player record).
		/// </summary>
		public void SaveInto(UWPlayerData.SavedState pOState, long piCritterClock, int piGameClock)
		{
			pOState.AlarmIndex = Index & 0xFF;
			pOState.AlarmKind = Index != 0 ? Kind & 0xFF : NoKind;

			if (Index == 0)
				return;

			pOState.AlarmTime = (uint)(piGameClock - (piCritterClock - Clock));
			pOState.AlarmTileX = TileX & 0xFF;
			pOState.AlarmTileY = TileY & 0xFF;
		}

		/// <summary>
		/// The reverse on loading (RestoreGame calls set_creatures_from_saved_game after GetLevel,
		/// GAMEWRAP.C): the alarm of the save, aged as far as it was against the loaded game clock.
		/// A blow right before saving makes its kin attack at once after loading (per user in the
		/// original, 2026-10-17, audit round 2 C7). Until then the port reset the alarm on loading,
		/// after a rat attacked at once on 2026-09-12 - that was the original's own behaviour.
		///
		/// piFloorLevel stands in for hitpz, which the save does not keep: the original goes on with
		/// whatever the global last held; the floor of the victim's tile is the closest guess.
		/// </summary>
		public void RestoreFrom(UWPlayerData pOPlayer, long piCritterClock, int piFloorLevel)
		{
			Reset();

			if (pOPlayer == null || !pOPlayer.IsLoaded || pOPlayer.AlarmIndex == 0)
				return;

			long liAge = (long)pOPlayer.ClockValue - pOPlayer.AlarmTime;

			Record(pOPlayer.AlarmIndex, pOPlayer.AlarmKind, pOPlayer.AlarmTileX, pOPlayer.AlarmTileY,
				piFloorLevel, piCritterClock - liAge);
		}

		/// <summary>Still within the 0x200 PIT tick window at this clock (53553).</summary>
		public bool IsFresh(long piClock)
		{
			return Index != 0 && piClock <= Clock + UWCritterRules.KinAlarmPitTicks;
		}
	}
}
