using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// What a door does when someone operates it - by hand, by a creature, by the door trap, by
	/// gronk_door in a conversation, by a bash that breaks it, by the night. Engine-free since
	/// 2026-10-12 (cleanup step 2.5): until then the decisions sat in Interaction.fTryUseDoor,
	/// UWCritter.UseDoor, UWTriggerSystem (TrySetDoorState, FireDoorTrap), UWDoorDamage and
	/// UWSleep, and each decided a little differently (CLEANUP-PLAN B4, B10, B11). The Unity
	/// UWDoor component asks here and moves the leaf.
	///
	/// THE ORIGINAL HAS THREE ENTRY POINTS, read in UnderworldExhumed obj/USEITEMS.C on 2026-10-12:
	/// OpenDoor, CloseDoor and ToggleDoor. A door at rest is a door object (0x140-0x14F); while it
	/// swings it is the moving door 0x1CF with its direction in its owner field. OpenDoor acts on
	/// a door that is closed or still closing (it turns the swing round), does nothing on one that
	/// is open or opening; CloseDoor the other way round. So every caller decides on WHERE THE
	/// DOOR IS HEADING (IUsableDoor.IsHeadingClosed), not on where the leaf stands right now - the
	/// talking door of level 6 closes itself and opens again in the same breath (per user,
	/// 2026-10-01), and the close trap before Drog's grate (level 1, 15/33) fires again while the
	/// grate is on its way down and must not turn it round (per user, 2026-10-06).
	///
	/// WHAT OPENING DOES BESIDES THE SWING (OpenDoor): a door opened from rest loses its spike
	/// bit and fires its own trigger chain with the open event (checkTrap kind 7) - whoever opens
	/// it, the player, a creature, the trap, gronk_door or the blow that breaks it. A swing turned
	/// round fires nothing. Until 2026-10-12 ours fired the open trigger for the player and the
	/// creatures only (B10).
	/// </summary>
	public static class UWDoorRules
	{
		/// <summary>What the door's own "use" decides (UseRect in USEITEMS.C, the door case).</summary>
		public enum UseOutcome
		{
			/// <summary>The leaf moves: a closed door opens, an open one closes, a swing turns round.</summary>
			Toggle,

			/// <summary>A locked door stays shut; the player reads "The door is locked".</summary>
			Locked,

			/// <summary>A spiked door stays shut for a creature.</summary>
			Spiked,

			/// <summary>The modern scheme's convenience: a fitting key in the backpack unlocks and
			/// opens in one step (per user; the classic scheme has no such automatic check).</summary>
			UnlockAndOpen
		}

		/// <summary>
		/// Using a door (UseRect): a door that is not at rest and closed simply moves - closing
		/// needs no key, and a swing is turned round with no questions asked (UseObj on the moving
		/// door 0x1CF). A closed door asks its lock first (checkLock with key 0: locked is locked,
		/// whoever asks), then the spike: "a critter cannot open one whose owner bit 0, the spike,
		/// is set" - THE PLAYER CAN. The spike keeps creatures out, not the player; OpenDoor then
		/// takes the spike bit off. Until 2026-10-12 ours refused the player too, with "The door is
		/// spiked." (the reference's reading; the original prints that line only when looking,
		/// SpecialLook in LOOK.C).
		/// </summary>
		public static UseOutcome Use(bool pbClosedAtRest, bool pbSpiked, bool pbLocked, bool pbByPlayer, bool pbKeyAtHand)
		{
			if (!pbClosedAtRest)
				return UseOutcome.Toggle;

			if (pbLocked)
				return pbByPlayer && pbKeyAtHand ? UseOutcome.UnlockAndOpen : UseOutcome.Locked;

			if (pbSpiked && !pbByPlayer)
				return UseOutcome.Spiked;

			return UseOutcome.Toggle;
		}

		/// <summary>OpenDoor moves the leaf when the door is closed or closing.</summary>
		public static bool OpenActs(bool pbHeadingClosed)
		{
			return pbHeadingClosed;
		}

		/// <summary>CloseDoor moves the leaf when the door is open or opening.</summary>
		public static bool CloseActs(bool pbHeadingClosed)
		{
			return !pbHeadingClosed;
		}

		/// <summary>
		/// ToggleDoor looks at the object id alone: a closed door (0x140-0x147) gets OpenDoor,
		/// anything else CloseDoor - and the moving door 0x1CF is "anything else". So a door at
		/// rest always moves, a swing is turned round only while it is OPENING; a closing door
		/// goes on closing.
		/// </summary>
		public static bool ToggleActs(bool pbHeadingClosed, bool pbMoving)
		{
			return !pbMoving || !pbHeadingClosed;
		}

		/// <summary>
		/// gronk_door (conv/BABLHACK.C): mode 0 open, 1 close, 2 toggle - the three entry points
		/// above, which is why a door still swinging shut is opened by "open" and left alone by
		/// "close" (B10: Gronk and the door trap now decide alike, on the heading). Any other mode
		/// does nothing. Returns whether the leaf is to be set in motion.
		/// </summary>
		public static bool GronkTogglesDoor(int piMode, bool pbHeadingClosed, bool pbMoving)
		{
			switch (piMode)
			{
				case UWConversationSession.GronkDoorOpen: return OpenActs(pbHeadingClosed);
				case UWConversationSession.GronkDoorClose: return CloseActs(pbHeadingClosed);
				case UWConversationSession.GronkDoorToggle: return ToggleActs(pbHeadingClosed, pbMoving);
				default: return false;
			}
		}

		/// <summary>What one firing of the a_door trap does to its door.</summary>
		public struct TrapOutcome
		{
			/// <summary>The lock on the door goes and a copy of the trap's template takes its place
			/// (also when there is no template: the door is then lockless).</summary>
			public bool ReplaceLock;

			/// <summary>The leaf is to be set in motion.</summary>
			public bool Toggle;
		}

		/// <summary>
		/// The a_door trap (TRAP_DOOR in event/TRIGGER.C): on a door AT REST the lock is replaced
		/// first - taken off and a copy of the trap's linked lock put on, the same lock however
		/// often it fires -, then quality 1 opens, 2 closes, 3 toggles and any other quality leaves
		/// the leaf alone. On a MOVING door the lock is not touched; "close" and "toggle" turn an
		/// opening door round, "open" and "toggle" a closing one, nothing else happens. Until
		/// 2026-10-12 ours skipped a moving door altogether, after an IDA reading that missed the
		/// second branch (per the notes of 2026-10-06); the byte-exact decompilation has it.
		/// </summary>
		public static TrapOutcome DoorTrap(int piAction, bool pbHeadingClosed, bool pbMoving)
		{
			TrapOutcome lOOutcome = new TrapOutcome();

			if (pbMoving)
			{
				lOOutcome.Toggle = pbHeadingClosed
					? piAction == UWObjectMechanics.DoorTrapOpenAction || piAction == UWObjectMechanics.DoorTrapToggleAction
					: piAction == UWObjectMechanics.DoorTrapCloseAction || piAction == UWObjectMechanics.DoorTrapToggleAction;

				return lOOutcome;
			}

			lOOutcome.ReplaceLock = true;

			switch (piAction)
			{
				case UWObjectMechanics.DoorTrapOpenAction: lOOutcome.Toggle = OpenActs(pbHeadingClosed); break;
				case UWObjectMechanics.DoorTrapCloseAction: lOOutcome.Toggle = CloseActs(pbHeadingClosed); break;
				case UWObjectMechanics.DoorTrapToggleAction: lOOutcome.Toggle = true; break;
			}

			return lOOutcome;
		}

		/// <summary>
		/// A door that is open AT REST on a tile without the tile's door bit is a candidate for the
		/// night (DoClosingDoors in event/TRIGGER.C walks every door object of the level - a moving
		/// door is an animation object then and is not found - with an id of the open range, the
		/// portcullis 0x14E and the secret door 0x14F included: B11, decided 2026-10-12. Until then
		/// ours closed DoorMover doors only, never a portcullis).
		/// </summary>
		public static bool IsNightCandidate(bool pbOpen, bool pbMoving, bool pbTileHasDoorBit)
		{
			return pbOpen && !pbMoving && !pbTileHasDoorBit;
		}

		/// <summary>The night's roll for one candidate: rand() * 10 / 0x8000 below the chance in
		/// ten (3 for sleep, UWSleepRules). The host draws the roll only for a candidate, so the
		/// random sequence is the same as before 2026-10-12.</summary>
		public static bool NightCloses(bool pbOpen, bool pbMoving, bool pbTileHasDoorBit, int piRoll, int piChanceInTen)
		{
			return IsNightCandidate(pbOpen, pbMoving, pbTileHasDoorBit) && piRoll < piChanceInTen;
		}

		/// <summary>
		/// A door broken by damage (remove_object in combat/DAMAGE.C, the door branch): a closed
		/// one springs open - OpenDoor, with the open event and the spike bit off -, and every lock
		/// goes, open or closed; the door itself stays (its debris is "leave it"). Checked by the
		/// user in the original on 2026-08-27: a broken door opens by itself and is no longer
		/// locked afterwards. Returns whether the leaf is to open.
		/// </summary>
		public static bool BurstOpens(bool pbClosedAtRest)
		{
			return pbClosedAtRest;
		}

		/// <summary>OpenDoor takes the spike bit off a door it opens from rest (owner &amp; 0xFFFE;
		/// the strength bits above it are left as they are, a fresh spike overwrites them anyway).</summary>
		public static void ClearSpike(UWObject pODoor)
		{
			if (pODoor != null)
				pODoor.Owner = (ushort)(pODoor.Owner & 0xFFFE);
		}

		/// <summary>
		/// The lock a door carries: the first lock object (0x10F) in the door's chain
		/// (checkLock walks the door's link for a MAJOR_SPEC minor 0 item 0xF). UWLockState
		/// resolves it the same way; this is the one place that asks for a door's lock, resolved at
		/// spawn by UWDoor (until 2026-10-12 seven sites resolved it lazily on first use).
		/// </summary>
		public static UWLockState ResolveLock(UWObject pODoor, List<UWObject> pOMasterlist)
		{
			UWLockState lOState = new UWLockState();

			lOState.EnsureResolved(pODoor, pOMasterlist);

			return lOState;
		}

		/// <summary>Every door of a level that carries a lock - for the self-check, which wants a
		/// real one.</summary>
		public static List<UWObject> FindDoorsWithLock(UWLevel pOLevel)
		{
			List<UWObject> lODoors = new List<UWObject>();

			if (pOLevel == null || pOLevel.Masterlist == null)
				return lODoors;

			foreach (UWObject lOObject in pOLevel.Masterlist)
			{
				if (lOObject == null || !UWObjectIds.IsDoor(lOObject.ID))
					continue;

				lOObject.EnsureContentsLoaded(pOLevel.Masterlist);

				if (lOObject.Contents != null && lOObject.Contents.Count > 0
					&& lOObject.Contents[0] != null && lOObject.Contents[0].ID == UWObjectMechanics.LockObjectId)
					lODoors.Add(lOObject);
			}

			return lODoors;
		}
	}
}
