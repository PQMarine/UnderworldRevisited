using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Writes the runtime state of creatures, doors, locks and switches back into the level data -
	/// before saving and before every level change. Engine-free since 2026-09-17 (P2 of the engine
	/// separation): the host (UWWorldSync) reads the values from its components and hands them in
	/// as plain numbers.
	///
	/// WHY THIS IS NEEDED: items already enter their changes into the data themselves (picking up,
	/// dropping, casting, levers, terrain traps). Creatures, door state, locks and switches live only
	/// in the host's components. Without this, the writer would write back the state from loading,
	/// and on re-entering a level everything would be as before - slain monsters included.
	///
	/// Only what really changed at runtime is modified. A level on which nothing happened comes out
	/// byte-identical, as in the writer's round-trip test.
	/// </summary>
	public static class UWWorldCapture
	{
		/// <summary>Doors: 320-327 closed, 328-335 open (the reference: door.ToggleDoor).</summary>
		public const int FirstClosedDoorId = 320;

		public const int FirstOpenDoorId = 328;

		public const int LastOpenDoorId = 335;

		private const int DoorOpenOffset = 8;

		private const int OpenPortcullisId = 334;

		/// <summary>
		/// The flags of a door per state, namely ALL FOUR BITS 9 to 12 of the first word. Measured on
		/// about 800 doors from nine LEV.ARK files (2026-09-11), without exception: closed 0, open 13,
		/// open portcullis 12.
		///
		/// For doors bit 12 is NOT an enchantment but part of the state. At first only the lower three
		/// bits were written and bit 12 left as it was. A door closed in our game then carried 8 - in
		/// the original it snapped open and shut several times without animation when opened and
		/// afterwards sat in the floor (per user, 2026-09-11).
		/// </summary>
		private const int OpenDoorFlags = 13;

		private const int OpenPortcullisFlags = 12;

		private const int ClosedDoorFlags = 0;

		private const int DoorFlagsMask = 0xF;

		/// <summary>Bit 12 within the four flag bits - kept as UWObject.IsEnchanted, and the writer
		/// takes it from there.</summary>
		private const int DoorStateHighBit = 0x8;

		/// <summary>
		/// This much higher stands an open door. The reference calculates this way (door.OpenDoor and
		/// CloseDoor), and the original's data confirms it without exception: on about 800 doors every
		/// open one stands 24 above the floor of its tile, almost every closed one exactly on it -
		/// portcullises included (2026-09-11).
		/// </summary>
		private const int OpenDoorRaise = 24;

		/// <summary>Switches: the image with bit 8 set is the pressed one.</summary>
		private const int PressedSwitchBit = 8;

		/// <summary>Locked bit of the lock object (see UWObjectMechanics.IsLockLocked).</summary>
		private const int LockedFlag = 0x1;

		private const int MaxZPos = 127;

		private const int MaxQuality = UWObjectMechanics.MaxQuality;

		private const int MaxHitPoints = UWNpc.MaxHitPoints;

		/// <summary>Radius of every creature in COMOBJ.DAT, in eighths of the tile.</summary>
		private const int OriginalCritterRadius = 2;

		/// <summary>
		/// A creature. Dead ones leave the level data; hit points are written when they changed or
		/// the data carried some; attitude and goal always; the position only when the creature has
		/// moved (pbMoved), because the conversion back to the eighths grid is not lossless. Returns
		/// whether the position was written - the host then writes the facing.
		/// </summary>
		public static bool CaptureCritter(UWTileQueries pOQueries, UWNpc pONpc, bool pbDestroyed,
			bool pbHasHealth, int piCurrentHealth, int piInitialHealth, int piAttitude, int piGoal,
			bool pbMoved, float pfWorldX, float pfWorldY, float pfWorldZ)
		{
			if (pOQueries == null || pONpc == null)
				return false;

			// Dead, only the death animation is still running. The remains already remove it on
			// death; this only catches whatever would have slipped through.
			if (pbDestroyed)
			{
				pOQueries.ForgetObjectData(pONpc);
				return false;
			}

			// Hit points are stored as a full byte in the record (npc_hp). A creature whose data held
			// 0 got the table values on setup - those stay untouched as long as it has not been hurt.
			if (pbHasHealth && (pONpc.HitPoints > 0 || piCurrentHealth != piInitialHealth))
				pONpc.HitPoints = (byte)Math.Clamp(piCurrentHealth, 1, MaxHitPoints);

			pONpc.NPCAttitude = (byte)piAttitude;
			pONpc.NPCGoal = (byte)piGoal;

			if (!pbMoved)
				return false;

			UWTilePos lOTile = UWTileQueries.WorldToTile(pfWorldX, pfWorldZ);

			pOQueries.MoveObjectData(pONpc, lOTile.X, lOTile.Y);

			// The word at 0x16 (uw-formats calls it npc_xhome/npc_yhome) is the CURRENT tile of the
			// creature: in every LEV.ARK written by the original it matches, for all creatures, the
			// tile in whose list they hang (SAVE1-4 and DATA checked, 2026-09-11). The original unlinks
			// a creature that moves on from its old tile via this word. If the load tile was still
			// stored here, our entry stayed in the new tile and the player saw the creature twice (per
			// user: two Bragits). The home is stored in quality and owner.
			pONpc.NPCXHome = (byte)lOTile.X;
			pONpc.NPCYHome = (byte)lOTile.Y;

			pONpc.XPos = UWTileQueries.WorldToSubTile(pfWorldX, lOTile.X);
			pONpc.YPos = UWTileQueries.WorldToSubTile(pfWorldZ, lOTile.Y);
			pONpc.ZPos = Math.Clamp(UWUnits.RoundToInt(pfWorldY / UWWorldScale.ZPosStep), 0, MaxZPos);

			fKeepClearOfWalls(pOQueries, pONpc, lOTile);

			return true;
		}

		/// <summary>
		/// The original checks a creature with its radius from COMOBJ.DAT (2 for all) in EIGHTHS of
		/// the tile: the four corners at sub-position plus and minus 2 must not lie in any solid tile
		/// (the reference: motion_calc, seg028_2941_449). Next to a wall only eighths 2 to 5 are thus
		/// allowed. Our wall distance allows eighth 6, and anyone already closer may move freely - that
		/// is how a rat stood in the savegame at eighth 7 next to an east wall and was stuck in the
		/// original (per user, 2026-09-12). When writing, the sub-position is therefore pulled into
		/// the allowed band; the tile stays.
		/// </summary>
		private static void fKeepClearOfWalls(UWTileQueries pOQueries, UWNpc pONpc, UWTilePos pOTile)
		{
			int liX = pONpc.XPos;
			int liY = pONpc.YPos;

			// The same rule as for loot, here only with walls - confirmed for writing the savegame
			// (per user, 2026-09-12).
			pOQueries.KeepClearOfEdges(pOTile.X, pOTile.Y, ref liX, ref liY, OriginalCritterRadius, false);

			pONpc.XPos = (ushort)liX;
			pONpc.YPos = (ushort)liY;
		}

		/// <summary>
		/// A door: its open state when the host knows it (pbKnown - before the door has started it
		/// does not), its condition from the hit points, and its lock.
		/// </summary>
		public static void CaptureDoor(UWLevel pOLevel, UWObject pOData, bool pbStateKnown, bool pbOpen,
			bool pbHasHealth, int piCurrentHealth, int piInitialHealth, UWLockState pOLock)
		{
			if (pOData == null)
				return;

			if (pbStateKnown && pOData.ID >= FirstClosedDoorId && pOData.ID <= LastOpenDoorId)
			{
				bool lbDataOpen = pOData.ID >= FirstOpenDoorId;

				if (pbOpen != lbDataOpen)
				{
					pOData.ID = (ushort)(pOData.ID + (pbOpen ? DoorOpenOffset : -DoorOpenOffset));

					int liFlags = pbOpen
						? (pOData.ID == OpenPortcullisId ? OpenPortcullisFlags : OpenDoorFlags)
						: ClosedDoorFlags;

					pOData.Flags = (ushort)((pOData.Flags & ~DoorFlagsMask) | liFlags);
					pOData.IsEnchanted = (liFlags & DoorStateHighBit) != 0;

					// An open door stands 24 higher than a closed one (see OpenDoor_seg040_352B_21E6 in UWConversationSession: it adds
					// 0x18 to the zpos, CloseDoor takes it off). Without this, the original showed a
					// door closed in our game as a leaf in the air above the doorway (per user,
					// 2026-09-11) - and since the motion core (2026-10-05) the lifted object is what
					// makes an open door passable: its collision record sits above every mover's
					// head. The movers write the state here the moment it changes (DoorMover,
					// PortcullisMover), not only at the save.
					//
					// THE PORTCULLIS TOO (2026-10-06, per user: a portcullis opened by a conversation
					// could not be passed, one worked by a switch could always be passed, closed or
					// not). OpenDoor itself skips the 0x18 for id low bits 6, which this code copied -
					// but only because the original turns the door into an ANIMO (id 0x1CF,
					// seg040_352B_20CD, ProbablyCreateAnimo type 4 for the portcullis, 5 for a door)
					// whose animation RAISES the grate by the same 0x18 over its frames, and the
					// animo has no height, so the doorway is free while it moves. The level data
					// says the same: every open portcullis of the levels sits 24 above its floor
					// (level 1 tile 9/35: floor 96, grate 120), every closed one on the floor. So the
					// end state is one rule for both kinds.
					pOData.ZPos = Math.Clamp(pOData.ZPos + (pbOpen ? OpenDoorRaise : -OpenDoorRaise), 0, MaxZPos);
				}
			}

			// The condition of a door is its quality.
			if (pbHasHealth && (pOData.Quality > 0 || piCurrentHealth != piInitialHealth))
				pOData.Quality = (ushort)Math.Clamp(piCurrentHealth, 0, MaxQuality);

			CaptureLock(pOLevel, pOData, pOLock);
		}

		/// <summary>
		/// The lock hangs on the door as an object (its special link). As long as nobody has asked
		/// for it, it is untouched and stays as it is in the data.
		/// </summary>
		public static void CaptureLock(UWLevel pOLevel, UWObject pODoor, UWLockState pOLock)
		{
			if (pOLock == null || !pOLock.IsResolved || pOLevel == null)
				return;

			List<UWObject> lOMaster = pOLevel.Masterlist;

			pODoor.EnsureContentsLoaded(lOMaster);

			if (!pOLock.HasLock)
			{
				// Smashed or removed by the door trap: the lock disappears from the door's chain and
				// thus from the level (the reference: door.cs likewise unlinks it when breaking).
				pODoor.Contents.RemoveAll(lOItem => lOItem != null && lOItem.ID == UWObjectMechanics.LockObjectId);
				return;
			}

			UWObject lOLockObject = pOLock.LockObject;

			if (lOLockObject == null)
				return;

			if (!pODoor.Contents.Contains(lOLockObject))
			{
				// Created by the door trap from a template. The template hangs on the trap and stays
				// there; the door gets a copy, as in the reference (a_door_trap, UW2 branch).
				bool lbLocked = pOLock.IsLocked;

				UWObject lOCopy = UWObject.Clone(lOLockObject);
				lOCopy.Link = 0;

				pODoor.Contents.RemoveAll(lOItem => lOItem != null && lOItem.ID == UWObjectMechanics.LockObjectId);
				pODoor.Contents.Insert(0, lOCopy);

				pOLock.ApplyLock(lOCopy);

				if (lbLocked)
					pOLock.Lock();
				else
					pOLock.Unlock();

				lOLockObject = lOCopy;
			}

			lOLockObject.Flags = (ushort)((lOLockObject.Flags & ~LockedFlag) | (pOLock.IsLocked ? LockedFlag : 0));
		}

		/// <summary>
		/// A used switch gets the number of the image it currently shows. Unused ones stay as they
		/// are - including the three buttons 377 that start already pressed.
		/// </summary>
		public static void CaptureSwitch(UWObject pOData, bool pbPressed)
		{
			if (pOData == null)
				return;

			int liId = pbPressed ? (pOData.ID | PressedSwitchBit) : (pOData.ID & ~PressedSwitchBit);

			pOData.ID = (ushort)liId;
		}
	}
}
