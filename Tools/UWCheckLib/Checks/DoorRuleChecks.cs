using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// UWDoorRules (cleanup step 2.5, 2026-10-12) against UnderworldExhumed: UseRect and
        /// checkLock for the use, OpenDoor/CloseDoor/ToggleDoor for gronk_door and the a_door
        /// trap (TRAP_DOOR), DoClosingDoors for the night, remove_object for the breaking blow -
        /// and the lock resolution on the real level 1, which has several doors with a lock.
        /// </summary>
        private static void fCheckDoorRules(DataImport pOData)
        {
            Check.Group("Doors (UWDoorRules: use, gronk_door, door trap, night, burst, the lock on level 1)");

            // Using a door (UseRect): the lock first, then the spike - for a creature only.
            Check.Int("use: an open door closes, whatever lock or spike",
                (int)UWDoorRules.Use(false, true, true, false, false), (int)UWDoorRules.UseOutcome.Toggle);
            Check.Int("use: a closed plain door opens",
                (int)UWDoorRules.Use(true, false, false, true, false), (int)UWDoorRules.UseOutcome.Toggle);
            Check.Int("use: a locked door refuses the player",
                (int)UWDoorRules.Use(true, false, true, true, false), (int)UWDoorRules.UseOutcome.Locked);
            Check.Int("use: ... and a creature",
                (int)UWDoorRules.Use(true, false, true, false, true), (int)UWDoorRules.UseOutcome.Locked);
            Check.Int("use: the modern scheme's key in the backpack unlocks and opens",
                (int)UWDoorRules.Use(true, false, true, true, true), (int)UWDoorRules.UseOutcome.UnlockAndOpen);
            Check.Int("use: a spiked door keeps a creature out",
                (int)UWDoorRules.Use(true, true, false, false, false), (int)UWDoorRules.UseOutcome.Spiked);
            Check.Int("use: ... not the player (UseRect: who == ThePlayer || !(owner & 1))",
                (int)UWDoorRules.Use(true, true, false, true, false), (int)UWDoorRules.UseOutcome.Toggle);
            Check.Int("use: a locked AND spiked door says locked first",
                (int)UWDoorRules.Use(true, true, true, false, false), (int)UWDoorRules.UseOutcome.Locked);

            // The three entry points decide on the heading.
            Check.Bool("open: acts on a closed door", UWDoorRules.OpenActs(true), true);
            Check.Bool("open: turns a closing door round", UWDoorRules.OpenActs(true), true);
            Check.Bool("open: leaves an open or opening door alone", UWDoorRules.OpenActs(false), false);
            Check.Bool("close: acts on an open door", UWDoorRules.CloseActs(false), true);
            Check.Bool("close: leaves a closed or closing door alone", UWDoorRules.CloseActs(true), false);
            Check.Bool("toggle: a door at rest always moves", UWDoorRules.ToggleActs(true, false) && UWDoorRules.ToggleActs(false, false), true);
            Check.Bool("toggle: an opening door is turned round (0x1CF counts as open)", UWDoorRules.ToggleActs(false, true), true);
            Check.Bool("toggle: a closing door goes on closing", UWDoorRules.ToggleActs(true, true), false);

            // gronk_door (B10: the talking door closes itself and opens again at once).
            Check.Bool("gronk: open on a door still swinging shut turns it round",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorOpen, true, true), true);
            Check.Bool("gronk: open on a door opening does nothing",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorOpen, false, true), false);
            Check.Bool("gronk: open on a closed door at rest",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorOpen, true, false), true);
            Check.Bool("gronk: close on a closing door does nothing",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorClose, true, true), false);
            Check.Bool("gronk: close on an open door at rest",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorClose, false, false), true);
            Check.Bool("gronk: toggle at rest",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorToggle, false, false), true);
            Check.Bool("gronk: toggle on a closing door goes on closing",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorToggle, true, true), false);
            Check.Bool("gronk: an unknown mode does nothing", UWDoorRules.GronkTogglesDoor(7, true, false), false);

            // The a_door trap: at rest the lock is replaced and the quality decides; in motion the
            // lock stays and only a swing against the wish is turned round.
            UWDoorRules.TrapOutcome lOTrap = UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapOpenAction, true, false);

            Check.Bool("trap: open on a closed door at rest replaces the lock", lOTrap.ReplaceLock, true);
            Check.Bool("trap: ... and opens it", lOTrap.Toggle, true);
            Check.Bool("trap: open on an open door leaves the leaf",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapOpenAction, false, false).Toggle, false);
            Check.Bool("trap: ... but still replaces the lock",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapOpenAction, false, false).ReplaceLock, true);
            Check.Bool("trap: close on an open door closes it",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapCloseAction, false, false).Toggle, true);
            Check.Bool("trap: close on a closed door leaves it",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapCloseAction, true, false).Toggle, false);
            Check.Bool("trap: toggle at rest always moves",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapToggleAction, true, false).Toggle
                && UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapToggleAction, false, false).Toggle, true);
            Check.Bool("trap: quality 0 replaces the lock and leaves the leaf",
                UWDoorRules.DoorTrap(0, true, false).ReplaceLock && !UWDoorRules.DoorTrap(0, true, false).Toggle, true);
            Check.Bool("trap: a moving door keeps its lock",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapCloseAction, true, true).ReplaceLock, false);
            Check.Bool("trap: close on a closing grate does nothing (Drog's grate, level 1 15/33)",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapCloseAction, true, true).Toggle, false);
            Check.Bool("trap: close on an opening door turns it round",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapCloseAction, false, true).Toggle, true);
            Check.Bool("trap: open on a closing door turns it round",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapOpenAction, true, true).Toggle, true);
            Check.Bool("trap: open on an opening door does nothing",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapOpenAction, false, true).Toggle, false);
            Check.Bool("trap: toggle turns a moving door round either way",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapToggleAction, true, true).Toggle
                && UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapToggleAction, false, true).Toggle, true);

            // The night (DoClosingDoors): open at rest, no tile door bit, roll below the chance.
            Check.Bool("night: an open door at rest is a candidate", UWDoorRules.IsNightCandidate(true, false, false), true);
            Check.Bool("night: a closed door is not", UWDoorRules.IsNightCandidate(false, false, false), false);
            Check.Bool("night: a moving door is not (an animation object then)", UWDoorRules.IsNightCandidate(true, true, false), false);
            Check.Bool("night: the tile's door bit spares it", UWDoorRules.IsNightCandidate(true, false, true), false);
            Check.Bool("night: roll 2 of chance 3 closes", UWDoorRules.NightCloses(true, false, false, 2, 3), true);
            Check.Bool("night: roll 3 does not", UWDoorRules.NightCloses(true, false, false, 3, 3), false);
            Check.Bool("night: the raised portcullis is a door object of the open range (B11)",
                UWObjectIds.IsDoor(UWObjectIds.OpenPortcullis) && UWObjectIds.IsOpenDoor(UWObjectIds.OpenPortcullis), true);

            // The breaking blow (remove_object) and the spike OpenDoor takes off.
            Check.Bool("burst: a closed door springs open", UWDoorRules.BurstOpens(true), true);
            Check.Bool("burst: an open door only loses its lock", UWDoorRules.BurstOpens(false), false);

            UWObject lOSpiked = new UWObject((ushort)UWObjectIds.FirstClosedDoor) { Owner = UWObjectMechanics.FreshDoorSpikeOwner };

            UWDoorRules.ClearSpike(lOSpiked);
            Check.Bool("spike: OpenDoor takes the bit off", UWObjectMechanics.IsDoorSpiked(lOSpiked), false);
            Check.Int("spike: ... the strength bits above it stay (owner & 0xFFFE)", lOSpiked.Owner, UWObjectMechanics.FreshDoorSpikeOwner - 1);

            // The lock resolution, synthetic and on the real level 1.
            UWLockState lOSynthetic = fMakeDoorLock(5, 2, true, true, out UWObject lOSyntheticDoor);

            Check.Bool("lock: a synthetic door's lock resolves locked",
                UWDoorRules.ResolveLock(lOSyntheticDoor, new List<UWObject>()).IsLocked && lOSynthetic.LockId == 5, true);
            Check.Bool("lock: a door without a chain has none",
                UWDoorRules.ResolveLock(new UWObject((ushort)UWObjectIds.FirstClosedDoor), new List<UWObject>()).HasLock, false);

            UWLevel lOLevel = pOData != null && pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;
            List<UWObject> lODoors = UWDoorRules.FindDoorsWithLock(lOLevel);

            Check.Bool("lock: level 1 has doors with a lock", lODoors.Count > 0, true);

            bool lbAllResolve = lODoors.Count > 0;
            int liLocked = 0;

            foreach (UWObject lODoor in lODoors)
            {
                UWLockState lOState = UWDoorRules.ResolveLock(lODoor, lOLevel.Masterlist);
                UWObject lOLock = lODoor.Contents[0];

                lbAllResolve &= lOState.IsResolved && lOState.HasLock && lOState.LockObject == lOLock
                    && lOState.IsLocked == UWObjectMechanics.IsLockLocked(lOLock)
                    && lOState.LockId == UWObjectMechanics.GetLockId(lOLock)
                    && lOState.LockDifficulty == lOLock.ZPos;

                if (lOState.IsLocked)
                    liLocked++;
            }

            Check.Bool("lock: every one of them resolves to its first chain object (" + lODoors.Count + " doors, "
                + liLocked + " locked)", lbAllResolve, true);
        }
    }
}
