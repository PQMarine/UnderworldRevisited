namespace UWDataImport.UWData
{
	/// <summary>
	/// What the player's vitals (UWPlayerVitals) ASK the game around them: the dungeon level, the
	/// regeneration of the worn equipment, and how the player moves - for the noise. The Unity
	/// side is UWCharacter itself.
	///
	/// WHAT THE VITALS TELL - the flasks want redrawing, the view shakes, a message is due, the
	/// status pass, the five-minute block, the active spells changed - were callbacks here until
	/// cleanup step 3.6 (2026-10-17) and are events on UWPlayerVitals since (VitalsChanged,
	/// DamageRecorded, CombatShakeDue, UnsteadyShakeDue, MessageDue, KillRewarded, StatusPassDue,
	/// FiveMinuteBlockDue, IdentificationReset, ActiveSpellsChanged).
	/// </summary>
	public interface IUWVitalsHost
	{
		/// <summary>The dungeon level the player is on, zero-based (-1 unknown) - where a new
		/// maximum mana goes depends on it (UWTybalOrbRules.KeepsMaxManaAside).</summary>
		int DungeonLevelIndex { get; }

		/// <summary>
		/// The regeneration bits of the worn equipment on the tick - bit 0 health, bit 1 mana
		/// (UWArmourProtection.Status.RegenerationBits). Comes from the host because the status
		/// pass runs every frame there, while the effect belongs on the tick. See
		/// UWWornRegeneration.
		/// </summary>
		int WornRegeneration { get; }

		/// <summary>For the noise: the player walks, is in a motion state, winds up a blow.</summary>
		bool IsMoving { get; }

		/// <summary>How much of the full forward speed the player is actually making, 0 to 1 -
		/// the original's momentum (Player_MotionArray_unk_14) over its cap, which is the
		/// forward speed. See UWPlayerVitals.TickQuietness.</summary>
		float MovementFraction { get; }

		/// <summary>
		/// The original's motion_state is not zero (UnderworldExhumed player.h): the player swims,
		/// stands on lava, floats in the air with Levitate or Fly, or the view still shakes (the
		/// combat shake or the tremor). Any of them makes him four louder (PLAYMOVE.C set_sound).
		/// Until 2026-10-12 only the liquid counted (IsInLiquid).
		/// </summary>
		bool IsInMotionState { get; }

		bool IsChargingAttack { get; }
	}
}
