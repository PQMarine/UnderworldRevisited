namespace UWDataImport.UWData
{
	/// <summary>
	/// The shared 16-slot clock of the mobile objects (Docs/AI/creature-ai.md 1.1-1.2).
	///
	/// The frame tick seg034_2F89_406 (114950) counts PIT ticks (about 256 Hz) and hands the
	/// world the number of 16-tick boundaries crossed since the last frame - at most 4, because
	/// an elapsed time above 64 ticks is clamped (114958-114990). ProcessMobileObjects
	/// (54860) then moves the phase from old to new = (old + units) &amp; 15 and runs every
	/// object whose due slot the phase has passed by 1..4 slots, looping while it stays due.
	/// So a creature with interval 4 runs every quarter second at any frame rate, and below 4
	/// frames per second the world slows down instead of skipping updates.
	///
	/// The player's Speed enchantment halves the units and keeps the odd one in the
	/// accumulator (114998-115008). Under Freeze Time the frame's units are consumed but
	/// ProcessMobileObjects is not called (115082): the phase stands still and nothing is due
	/// (since 2026-10-05; before, the phase ran on and the creatures caught up in a burst). The
	/// "easy move" fixed step adds 64 ticks, 4 units, halved under Speed as well (114747-114800).
	///
	/// The host owns one instance per level and calls Advance once per frame with the elapsed
	/// PIT ticks; the units returned say how many slots passed. The game clock (Clock) is the
	/// PIT tick total the kin alarm compares against.
	/// </summary>
	public sealed class UWCritterClock
	{
		/// <summary>The phase before this frame's advance ("old").</summary>
		public int OldPhase { get; private set; }

		/// <summary>The phase after it ("new"), 0..15.</summary>
		public int Phase { get; private set; }

		/// <summary>The slot units handed to the world this frame, 0..4 (0 with Speed on an
		/// odd frame or when no boundary was crossed).</summary>
		public int Units { get; private set; }

		/// <summary>The game clock in PIT ticks (PLAYER.DAT 0xCE advances by the elapsed ticks,
		/// label seg034_2F89_4C0).</summary>
		public long Clock { get; private set; }

		private int miAccumulator;

		public UWCritterClock(int piPhase = 0, long piClock = 0)
		{
			OldPhase = piPhase & 15;
			Phase = piPhase & 15;
			Clock = piClock;
		}

		/// <summary>
		/// One frame: piElapsedPitTicks since the last call. Returns the slot units handed to
		/// the world - 0 under Freeze Time, whose units are consumed and dropped. Elapsed 0 (or
		/// less) returns 0 at once, as the original does (114958).
		/// </summary>
		public int Advance(int piElapsedPitTicks, bool pbPlayerHasSpeed, bool pbTimeFrozen = false)
		{
			OldPhase = Phase;
			Units = 0;

			if (piElapsedPitTicks <= 0)
				return 0;

			int liUnits;

			if (piElapsedPitTicks > UWCritterRules.SlotPitTicks * UWCritterRules.MaxSlotsPerFrame)
			{
				piElapsedPitTicks = UWCritterRules.SlotPitTicks * UWCritterRules.MaxSlotsPerFrame;
				liUnits = UWCritterRules.MaxSlotsPerFrame;
			}
			else
				liUnits = (int)(((Clock + piElapsedPitTicks) >> 4) - (Clock >> 4));

			Clock += piElapsedPitTicks;
			miAccumulator += liUnits;

			if (pbPlayerHasSpeed)
			{
				Units = miAccumulator >> 1;
				miAccumulator &= 1;
			}
			else
			{
				Units = miAccumulator;
				miAccumulator = 0;
			}

			if (pbTimeFrozen)
				Units = 0;

			Phase = (OldPhase + Units) & 15;

			return Units;
		}

		/// <summary>The "easy move" fixed step (seg034_2F89_334, 114747-114800): 64 ticks, 4 units,
		/// halved under Speed like a frame.</summary>
		public int AdvanceFixedStep(bool pbPlayerHasSpeed, bool pbTimeFrozen = false)
		{
			return Advance(UWCritterRules.SlotPitTicks * UWCritterRules.MaxSlotsPerFrame, pbPlayerHasSpeed, pbTimeFrozen);
		}

		/// <summary>
		/// The due test seg007_1798_3825 (54807) for an object's due slot:
		/// (new &gt; slot and new - slot &lt;= 4) or (old &gt; new and slot &gt;= old).
		/// The second term covers the wrap of the 16-slot clock. The caller loops while this
		/// stays true and the object reschedules itself (slot += interval) on every update.
		/// </summary>
		public bool IsDue(int piSlot)
		{
			piSlot &= 15;

			if (Phase > piSlot && Phase - piSlot <= UWCritterRules.DueWindowSlots)
				return true;

			return OldPhase > Phase && piSlot >= OldPhase;
		}

		/// <summary>Rescheduling at the end of an update (53373): slot := (slot + interval) &amp; 15.</summary>
		public static int NextSlot(int piSlot, int piInterval)
		{
			return (piSlot + piInterval) & 15;
		}

		/// <summary>The distance cull (52715): the slot advances by 8, not by the interval.</summary>
		public static int FarSlot(int piSlot)
		{
			return (piSlot + UWCritterRules.FarRescheduleSlots) & 15;
		}
	}
}
