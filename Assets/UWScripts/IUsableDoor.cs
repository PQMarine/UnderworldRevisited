using UnityEngine;

/// <summary>Common interface for everything the player can use like a door
/// (normal door/secret door by rotation via DoorMover, portcullis by raising via
/// PortcullisMover) - see Interaction.cs.</summary>
public interface IUsableDoor
{
    void StartUsingDoor();

    /// <summary>For Interaction.fTryUseDoor (the lock check only applies to opening, not
    /// to closing). DoorMover/PortcullisMover already had this property before,
    /// just not as part of the interface.</summary>
    bool IsClosed { get; }

    /// <summary>The leaf or grate is on its way. The original has swapped the door for the
    /// moving door 0x1CF meanwhile (OpenDoor_seg040_352B_21E6), and looking says so.</summary>
    bool IsMoving { get; }

    /// <summary>Shut, or on its way to shut - where the door will end up. IsClosed alone stays
    /// false while a closing swing runs, so a gronk_door "open" right after a "close" was taken
    /// for a door already open (the talking door, per user, 2026-10-01).</summary>
    bool IsHeadingClosed { get; }

    /// <summary>Attaches a foreign collider to the door that blocks the player exactly
    /// when the door is closed - for the door frame (see UWObjectSpawner.fSpawnDoorFrames).
    /// </summary>
    void AddCoupledCollider(Collider pOCollider);

    /// <summary>The box that fills the door's tile while it is closed (see
    /// UWObjectSpawner.fAddClosedDoorBlocker). Unlike a coupled collider it is switched OFF
    /// while the door is open - rays and missiles pass - and it is the door's measure of
    /// "someone stands in my tile" when it tries to close.</summary>
    void SetTileBlocker(BoxCollider pOBlocker);
}
