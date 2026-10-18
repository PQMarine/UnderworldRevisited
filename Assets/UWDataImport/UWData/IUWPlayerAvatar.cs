namespace UWDataImport.UWData
{
	/// <summary>
	/// The player's body as the rules see it: whether there is one, where it stands, where the eye
	/// is and which way it looks. Engine-free; the Unity side is UWPlayerAvatar, reached through
	/// IUWWorld.Player.
	///
	/// WHY (cleanup step 3.1, 2026-10-17): the trap, spell, sleep, conversation and shrine hosts
	/// each worked out "where is the player" for themselves - from Camera.main, from the
	/// character's transform, from the feet of the CharacterController, from the controller's
	/// transform - with different fallbacks (section 4.2 of Docs/CLEANUP-PLAN.md). On the Unity
	/// side all of these share one horizontal position: UWCharacter sits on the Main Camera, a child
	/// of the controller at local x/z 0 that only ever moves up and down (head bob, swimming). They
	/// differ in the HEIGHT they mean, and that is what the two spots below keep apart until the
	/// motion core's player record becomes the one spot (phase 4).
	///
	/// SINCE CLEANUP STEP 3.6 (2026-10-17) it is the player as the rules see him altogether - the
	/// motion state the sleep asks (swimming, lava, on the ground) and what he carries - so that a
	/// rule that needs the player takes the world and no host of its own (the sleep's IUWSleepHost
	/// is gone).
	/// </summary>
	public interface IUWPlayerAvatar
	{
		/// <summary>Whether there is a body to ask - false in the main menu or a test without a
		/// scene. Without one the spots are UWSpot.Nowhere.</summary>
		bool HasBody { get; }

		/// <summary>The eighth cell under the eye, at the height of the FLOOR there (zpos, clamped
		/// to 0..127) - what the trespass trap hands the search as the player's spot, and whose tile
		/// the trap, spell, sleep and shrine hosts call the player's tile.</summary>
		UWSpot FloorSpot { get; }

		/// <summary>The eighth cell of the feet, at the height of the FEET (zpos, not clamped) - the
		/// sleeper's height (UWSleepRules hands its FloorLevel to the ambush).</summary>
		UWSpot FeetSpot { get; }

		/// <summary>Where the eye is, east and north in world units; 0/0 without a body.</summary>
		void GetEyePosition(out float pfX, out float pfZ);

		/// <summary>The view direction, flat and normalised, east and north; straight north when
		/// there is no view or it looks straight up or down.</summary>
		void GetViewDirection(out float pfX, out float pfZ);

		/// <summary>The view yaw in degrees, 0 north, clockwise; 0 without a body.</summary>
		float ViewYawDegrees { get; }

		// ------------------------------------------------- The motion state (cleanup step 3.6)

		/// <summary>The original's MS_SWIM: the motion core's swimming state, which it takes in water
		/// and at the water's edge unless the player walks on water; without the core, the terrain's
		/// swimming. False without a body.</summary>
		bool IsSwimming { get; }

		/// <summary>The original's MS_LAVA: standing on lava, not floating above it. False without
		/// a body.</summary>
		bool IsOnLava { get; }

		/// <summary>On the ground: not jumping, falling or hovering. True when there is no movement
		/// to ask (as the sleep's host answered).</summary>
		bool IsOnGround { get; }

		// ------------------------------------------------- What he carries (cleanup step 3.6)

		/// <summary>The player's inventory, or null without one.</summary>
		UWInventoryModel Inventory { get; }

		/// <summary>The player's numbers - hit points, mana, skills, experience - or null without a
		/// character.</summary>
		UWPlayerVitals Vitals { get; }

		/// <summary>EndCombat on the player's side: the combat mode (the drawn weapon) ends - the
		/// golem standing down after his defeat.</summary>
		void LeaveCombatMode();

		/// <summary>The player's damage-type immunities of the moment - worn enchantments and
		/// spells (the status pass), the resist byte check_res reads; 0 without a character.</summary>
		int DamageTypeProof { get; }

		/// <summary>Throws the player up (BouncePlayer_seg008_D9E) - the quake trap's second bit.
		/// Nothing at an intensity of 0 or less.</summary>
		void BounceUp(int piIntensity);
	}
}
