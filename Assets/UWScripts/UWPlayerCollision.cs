using UnityEngine;

/// <summary>
/// Disables collision of individual colliders WITH THE PLAYER without touching them otherwise:
/// for open doors and raised portcullises (confirmed by user, 2026-08-28: "Open doors have
/// no player collision. But you must still be able to interact.").
///
/// Deliberately via Physics.IgnoreCollision and not via the obvious alternatives:
/// - Collider.enabled = false would also block the interaction rays, and exactly those
///   must keep hitting (close the door, tap a switch).
/// - isTrigger only works on a MeshCollider with convex = true; the door/portcullis meshes
///   are not convex, Unity would report that as an error.
/// - A dedicated layer would need an entry in the TagManager and would disable collision with
///   everything on that layer, not just with the player.
/// Physics.IgnoreCollision, by contrast, does exactly what the user described: no
/// PLAYER collision, everything else (rays, thrown items) unchanged.
/// </summary>
public static class UWPlayerCollision
{
    private static CharacterController mOPlayer;

    /// <summary>pbIgnore = true: the player walks through this collider.</summary>
    public static void SetIgnored(Collider pOCollider, bool pbIgnore)
    {
        if (pOCollider == null)
            return;

        CharacterController lOPlayer = fGetPlayer();

        if (lOPlayer == null)
            return;

        Physics.IgnoreCollision(pOCollider, lOPlayer, pbIgnore);
    }

    /// <summary>Same lookup as UWLevelLoader.fPlacePlayer. Cached, but thanks to Unity's
    /// overloaded == comparison resolved again after the player has been destroyed.</summary>
    /// <summary>Does the player's body overlap this box? Asked for the door's tile when a door
    /// tries to close (DoorMover, PortcullisMover). Works with the box switched off, because
    /// it looks with Physics.OverlapBox at the box's place and not at the box.</summary>
    public static bool OverlapsPlayer(BoxCollider pOBox)
    {
        CharacterController lOPlayer = fGetPlayer();

        if (pOBox == null || lOPlayer == null)
            return false;

        Transform lOTransform = pOBox.transform;
        Vector3 lOCentre = lOTransform.TransformPoint(pOBox.center);
        Vector3 lOHalf = Vector3.Scale(pOBox.size, lOTransform.lossyScale) * 0.5f;

        foreach (Collider lOHit in Physics.OverlapBox(lOCentre, lOHalf, lOTransform.rotation, Physics.AllLayers))
        {
            if (lOHit == lOPlayer)
                return true;
        }

        return false;
    }

    private static CharacterController fGetPlayer()
    {
        if (mOPlayer != null)
            return mOPlayer;

        GameObject lOPlayer = UWScene.Player;
        mOPlayer = lOPlayer != null ? lOPlayer.GetComponent<CharacterController>() : null;

        return mOPlayer;
    }
}
