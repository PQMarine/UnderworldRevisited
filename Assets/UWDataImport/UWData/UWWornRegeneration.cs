namespace UWDataImport.UWData
{
	/// <summary>
	/// The two worn enchantments that act over TIME instead of on a status value:
	/// Regeneration and Mana Regeneration. The Ring of Regeneration is the item that carries
	/// the first one.
	///
	/// THE RULE, read out of the disassembly (2026-09-20)
	///
	///   Both are ordinary item enchantments of MAJOR CLASS 11, minor class 0xE for health
	///   and 0xF for mana (ActiveEnchantedItem_Spells_ovr133_347, branches
	///   HealthRegeneration_ovr133_45D and ManaRegeneration_ovr133_468, 381629-381646).
	///   The name in string block 6 confirms the split: entry 0xBE is "Regeneration",
	///   0xBF "Mana Regeneration" - exactly (major &lt;&lt; 4) | minor.
	///
	///   The status pass does NOT apply them. It only sets two bits in one byte
	///   (ManaHealthRegeneration_dseg_5c99_3F8, 280689): bit 0 health, bit 1 mana. The byte is
	///   cleared at the start of every pass (ResetPlayerStatusValues_ovr133_1E4, label 255) and
	///   rebuilt from what is worn, so taking the ring off stops the effect at once.
	///
	///   THE EFFECT FIRES ON THE PLAYER TICK, not on a timer of its own
	///   (PlayerUpdates_seg028_2985_13D, label 213, lines 98605-98624): with bit 0 set it calls
	///   PlayerHPRegenerationChange_seg038_3307_322 with -1, with bit 1 set
	///   ManaChange_seg038_2B6 with -1. In both routines a NEGATIVE argument is the plain
	///   branch - new value = current - argument, i.e. exactly PLUS ONE, with no die roll - and
	///   the result is capped at the maximum (labels 377-3AE and 2FE-31A). A positive argument
	///   would take the other branch, the percentage-of-maximum healing that the five-minute
	///   block uses (see UWPlayerVitals.fTickFiveMinutes).
	///
	///   HOW OFTEN: the player tick is paced in PlayerUpdateTick_seg024_24DC_3A4 (labels 485
	///   to 4E8, lines 85797-85864). It takes the free-running PIT counter
	///   (PITTimerGlobal_dseg_5c99_2364), shifts it right by 8 to get whole SECONDS, adds the
	///   seconds passed since the last frame to an accumulator (dseg_5c99_28D) and fires when
	///   the accumulator is greater than 0x14. So the interval is TWENTY-ONE seconds, or
	///   21 * 256 = 5376 PIT ticks. The user measured "about one hit point every twenty
	///   seconds" in the original on 2026-09-20, which is this.
	///
	///   REAL TIME, NOT GAME TIME: the accumulator hangs on the PIT counter, not on the game
	///   clock in PLAYER.DAT 0xCE. Sleeping therefore brings no regeneration ticks - it adds
	///   hours to the game clock (Sleep_ovr143_D1F, label DD8) and has its own recovery
	///   formula, while the PIT has only advanced by the seconds the sleep cutscene really
	///   took. A PAUSE has the opposite effect but only once: the seconds keep accumulating,
	///   the accumulator crosses the threshold, and the first tick afterwards fires EXACTLY
	///   ONCE, because it is reset to zero and not reduced by the interval.
	///
	///   NO OTHER WORN ENCHANTMENT IS PACED BY THIS. The remaining class 11 minor classes are
	///   flags the status pass sets and something else reads every frame (0 Freeze Time,
	///   1 Roaming Sight, 2 Speed, 3 Telekinesis - see UWMiscSpellRules), and every other
	///   major class ends up in a status value (see UWArmourProtection). The only other thing
	///   the same tick does to the worn equipment is burn down torches (see
	///   UWInventoryModel.BurnTick), which is not an enchantment.
	///
	///   NEITHER OF THE TWO CAN BE CAST. There is no runic spell that produces class 11 minor
	///   0xE or 0xF (UWMiscSpellRules.Cast covers 0 to 0xD), so in uw1 the bits can only come
	///   from a worn item. The original still runs its active spells through the same case
	///   distinction without a slot check, and so do we.
	/// </summary>
	public static class UWWornRegeneration
	{
		/// <summary>Spell class 11, the same bag of special cases as UWMiscSpellRules.</summary>
		public const int MajorClass = UWMiscSpellRules.MajorClass;

		/// <summary>"Regeneration" - string block 6, entry 0xBE.</summary>
		public const int HealthMinorClass = 0xE;

		/// <summary>"Mana Regeneration" - string block 6, entry 0xBF.</summary>
		public const int ManaMinorClass = 0xF;

		/// <summary>Bit 0 of ManaHealthRegeneration_dseg_5c99_3F8: health.</summary>
		public const int HealthBit = 1;

		/// <summary>Bit 1 of the same byte: mana.</summary>
		public const int ManaBit = 2;

		/// <summary>
		/// One point per tick, for health as for mana. It is not a die roll: the original
		/// hands -1 to routines that then subtract the argument from the current value.
		/// </summary>
		public const int Amount = 1;

		/// <summary>The PIT rate the whole schedule is measured in - see
		/// Docs/AI/creature-ai.md section 1.1.</summary>
		public const int PitTicksPerSecond = UWPlayerTick.PitTicksPerSecond;

		/// <summary>
		/// Twenty-one seconds, because the accumulator fires at GREATER THAN 0x14 and is then
		/// reset to zero. That is the shared player tick, so the number is taken from
		/// UWPlayerTick and not written out a second time.
		///
		/// The port ran that tick at twenty seconds until 2026-09-21, one second short, so a
		/// ring healed about five per cent fast. Settled that day.
		/// </summary>
		public const int IntervalSeconds = UWPlayerTick.Seconds;

		/// <summary>The same interval in PIT ticks: 21 * 256.</summary>
		public const int IntervalPitTicks = UWPlayerTick.PitTicks;

		/// <summary>
		/// Which bits an enchantment contributes, or zero. Written as a bit field and not as
		/// two flags because the original keeps exactly this byte and two rings can set both
		/// bits at once.
		/// </summary>
		public static int GetBits(int piMajorClass, int piMinorClass)
		{
			if (piMajorClass != MajorClass)
				return 0;

			if (piMinorClass == HealthMinorClass)
				return HealthBit;

			return piMinorClass == ManaMinorClass ? ManaBit : 0;
		}
	}
}
