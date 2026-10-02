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

		/// <summary>A hit shakes the view, the level from the damage (0 does not shake).</summary>
		void ShakeOnDamage(int piLevel);

		/// <summary>A failed sip: the view shakes for this many ticks on the large channel.</summary>
		void ShakeUnsteady(int piDuration);

		void AddMessage(string psMessage);

		/// <summary>A creature was slain: the dragon nods, and if a reward is due the fanfare
		/// plays. Called before the experience changes.</summary>
		void OnKillRewarded(bool pbRewarded);

		/// <summary>The curse of the worn equipment struck on the tick - the view flashes.</summary>
		void OnCurseStruck();

		/// <summary>Lore rose: every item in the world may be identified anew.</summary>
		void ResetIdentification();

		/// <summary>
		/// The set of active spells changed. What class 11 contributes is handed over
		/// decoded; everything that can also sit on equipment is recomputed by the host's
		/// status pass afterwards.
		/// </summary>
		void OnActiveSpellsChanged(bool pbHastened, bool pbRoamingSight, bool pbTimeFrozen, bool pbTelekinesis);

		/// <summary>The curse of the worn equipment strikes on the tick: how many dice it
		/// rolls (UWArmourProtection.Status.CurseDice).</summary>
		int WornCurseDice { get; }

		/// <summary>The dungeon level the player is on, zero-based (-1 unknown) - where a new
		/// maximum mana goes depends on it (UWTybalOrbRules.KeepsMaxManaAside).</summary>
		int DungeonLevelIndex { get; }

		/// <summary>
		/// The regeneration bits of the worn equipment on the tick - bit 0 health, bit 1 mana
		/// (UWArmourProtection.Status.RegenerationBits). Comes from the host for the same
		/// reason as WornCurseDice: the status pass runs every frame there, the effect belongs
		/// on the tick. See UWWornRegeneration.
		/// </summary>
		int WornRegeneration { get; }

		/// <summary>For the noise: the player walks, stands in water, winds up a blow.</summary>
		bool IsMoving { get; }

		/// <summary>How much of the full forward speed the player is actually making, 0 to 1 -
		/// the original's momentum (Player_MotionArray_unk_14) over its cap, which is the
		/// forward speed. See UWPlayerVitals.TickQuietness.</summary>
		float MovementFraction { get; }

		bool IsInLiquid { get; }

		bool IsChargingAttack { get; }
	}
}
