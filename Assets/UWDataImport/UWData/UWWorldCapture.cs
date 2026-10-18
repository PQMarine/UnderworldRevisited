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
		/// <summary>Doors: 320-327 closed, 328-335 open (the reference: door.ToggleDoor;
		/// UWObjectIds since 2026-10-11).</summary>
		public const int FirstClosedDoorId = UWObjectIds.FirstClosedDoor;

		public const int FirstOpenDoorId = UWObjectIds.FirstOpenDoor;

		public const int LastOpenDoorId = UWObjectIds.LastOpenDoor;

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

		/// <summary>Bit 12 within the four flag bits - bit 3 of UWObject.Flags, which
		/// IsEnchanted views; since 2026-10-12 the writer takes all four bits from Flags.</summary>
		private const int DoorStateHighBit = UWObjectRecordCodec.EnchantedFlag;

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
		/// A creature, FROM ITS RECORD (cleanup step 4.8, 2026-10-18; section 7.4 of
		/// Docs/CLEANUP-PLAN.md): the record is the creature's one store of hit points, attitude,
		/// goal and position, so what is captured is what the brain and the motion core left there.
		/// Until then the host handed in the body's hit points and the body's world point, which this
		/// converted back to tile, eighth and zpos - the same numbers, except where the body had not
		/// followed the record yet (a floor trap that lifted the creature, a flier set down by goal 3)
		/// or a swimmer was drawn lower (UWSettings.SwimmerHeightOffset).
		///
		/// A dying one stays with 0 hit points; hit points are written when they changed or the data
		/// carried some; attitude and goal always; the tile list and the wall distance only when the
		/// creature has moved since it was set up (pbMoved), so whoever stood still keeps its place
		/// exactly as it was loaded. Returns whether the position was captured.
		/// </summary>
		public static bool CaptureCritter(UWTileQueries pOQueries, UWCritterRecord pORecord, bool pbDestroyed,
			int piInitialHealth, bool pbMoved)
		{
			UWNpc pONpc = pORecord != null ? pORecord.Npc : null;

			if (pOQueries == null || pONpc == null)
				return false;

			// DYING, the death animation still running: it stays in the data with 0 hit points and
			// the dying sequence its record holds, as the original keeps a dying critter in its tile
			// list until frame 3 - after loading or on re-entry the death finishes and the loot and
			// remains come (critter_ai, UnderworldExhumed AI.C). Until 2026-10-15 it was dropped
			// here, and with it its loot (audit round 2, A25 / KR N2). The remains take it out of
			// the data at the end of the death (UWCritterRemains).
			if (pbDestroyed)
			{
				pONpc.HitPoints = 0;
				pONpc.NPC_HP = 0;

				return false;
			}

			// Hit points are stored as a full byte in the record (npc_hp); a record holding 0 keeps
			// it from setup on as the original does (UWCritter.Initialise, 2026-10-18).
			// AS THEY ARE, 0 INCLUDED (B23, cleanup step 4.8, 2026-10-18): the hit points come from the
			// record now (UWHealth.KeepHitPointsIn), and a living creature holds 0 only when its
			// death was refused - the golem until his script sets 125, as in the original. Until then
			// they came from the body's own copy, clamped to at least 1, so he saved with 1.
			int liHitPoints = pORecord.HitPoints;

			if (pONpc.HitPoints > 0 || liHitPoints != piInitialHealth)
				pONpc.HitPoints = (byte)Math.Clamp(liHitPoints, 0, MaxHitPoints);

			pONpc.NPCAttitude = (byte)pORecord.Attitude;
			pONpc.NPCGoal = (byte)pORecord.Goal;

			if (!pbMoved)
				return false;

			// The word at 0x16 (uw-formats calls it npc_xhome/npc_yhome) is the CURRENT tile of the
			// creature: in every LEV.ARK written by the original it matches, for all creatures, the
			// tile in whose list they hang (SAVE1-4 and DATA checked, 2026-09-11). The original unlinks
			// a creature that moves on from its old tile via this word. If the load tile was still
			// stored here, our entry stayed in the new tile and the player saw the creature twice (per
			// user: two Bragits). The home is stored in quality and owner. The record's TileX/TileY
			// ARE that word (written through to NPCXHome/NPCYHome), and the fine position and zpos
			// are the UWNpc's own, so only the list has to be made sure of.
			UWTilePos lOTile = new UWTilePos(pORecord.TileX, pORecord.TileY);

			pOQueries.MoveObjectData(pONpc, lOTile.X, lOTile.Y);

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
