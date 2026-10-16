namespace UWDataImport.UWData
{
	/// <summary>
	/// What the player's vitals (UWPlayerVitals) tell the game around them: the flasks want
	/// redrawing, the view shakes or flashes, a message is due, the active spells changed and
	/// the camera, the light and the movement have to follow. The Unity side is UWCharacter
	/// itself.
	/// </summary>
	public interface IUWVitalsHost
	{
		/// <summary>Health, mana or poison changed - the flasks show it.</summary>
		void OnVitalsChanged();

		/// <summary>A hit or a curse was just recorded in LastDamage - the host stamps the time.</summary>
		void OnDamageRecorded();

		/// <summary>A blow or a missile shakes the view (UWPlayerVitals.ShakeForCombatHit): the
		/// combat shake is set to level times five, so level 0 ends a running one.</summary>
		void ShakeOnDamage(int piLevel);

		/// <summary>A failed sip: the view shakes for this many ticks on the large channel.</summary>
		void ShakeUnsteady(int piDuration);

		void AddMessage(string psMessage);

		/// <summary>A creature was slain: the dragon nods, and if a reward is due the fanfare
		/// plays. Called before the experience changes.</summary>
		void OnKillRewarded(bool pbRewarded);

		/// <summary>
		/// The original would run its status pass now (UnderworldExhumed PLAYDATA.C
		/// FixPlayerEquips): a spell began or ended, the mushroom began or wore off, the active
		/// spells were cleared for sleep, a mantra was chanted. That pass is where a worn cursed
		/// item strikes (class 9, backfire) - the host runs it (UWCharacter). Since 2026-10-12;
		/// until then the curse struck on every player tick (OnCurseStruck, WornCurseDice).
		/// </summary>
		void OnStatusPassDue();

		/// <summary>
		/// The level's share of the five-minute block (duration_check, UnderworldExhumed
		/// PLAYTIME.C, every 24th player tick): the wandering monsters' one-in-four roll
		/// (UWRespawnRules.RollOpportunity) and the creatures' checkup (UWLevelExit.YearlyCheckup).
		/// Called after hunger and intoxication and before the strength roll, in the original's
		/// order of draws. Since 2026-10-15.
		/// </summary>
		void OnFiveMinuteBlock();

		/// <summary>Lore rose: every item in the world may be identified anew.</summary>
		void ResetIdentification();

		/// <summary>
		/// The set of active spells changed. What class 11 contributes is handed over
		/// decoded; everything that can also sit on equipment is recomputed by the host's
		/// status pass afterwards.
		/// </summary>
		void OnActiveSpellsChanged(bool pbHastened, bool pbRoamingSight, bool pbTimeFrozen, bool pbTelekinesis);

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
