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
	/// own. The original saves it with the game (PLAYER.DAT 0xBA-0xC1, 341052-341140).
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

		/// <summary>Still within the 0x200 PIT tick window at this clock (53553).</summary>
		public bool IsFresh(long piClock)
		{
			return Index != 0 && piClock <= Clock + UWCritterRules.KinAlarmPitTicks;
		}
	}
}
