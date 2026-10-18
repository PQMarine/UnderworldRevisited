using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Move trigger (a_move trigger, 0x01A0): lies in a tile itself and fires
/// when the player bumps into it. Unlike use/look triggers it is not attached to any
/// object that could be clicked.
///
/// It is a POINT with a radius, not a tile area. Where it sits and how far it reaches is
/// in its own data (see UWLevelLoader.fCreateTriggerVolume): the
/// sub-tile position XPos/YPos gives the point, comobj.dat the radius.
///
/// THIS CLASS DOES THE TEST ITSELF, not Unity's contact calculation. The original
/// compares two boxes PER AXIS - contact happens at a distance less than or equal to
/// (trigger radius + player radius). If this were left to the collider, the
/// reach would additionally depend on the size of the player capsule, which has nothing
/// to do with the original data. The box on the GameObject is therefore only a pre-filter; it is
/// deliberately a bit too generous, so that OnTriggerStay reliably runs whenever the test
/// can succeed at all.
///
/// For the a_move trigger, trigger and player are two eighth-tiles thick each, so together
/// 32 world units per side. The zone is thus a whole tile wide - you cannot
/// cross it without touching the trigger.
///
/// LEVEL TRANSITIONS also run through this. Until 2026-09-06 they had their own zone,
/// built on the assumption that the teleport trap is not addressed by any trigger -
/// but counting showed that all 62 teleport traps have a trigger in their tile.
/// </summary>
public class UWMoveTrigger : MonoBehaviour
{
    private UWLevelLoader mOLoader;
    private UWObject mOTriggerData;
    private Interaction mOInteraction;
    private Transform mOPlayer;

    /// <summary>Reach per axis in world units - trigger radius plus player radius; the
    /// volume's pre-filter.</summary>
    private float mfReach;

    /// <summary>The original's reach in eighth cells per axis (UWTileQueries.TriggerReachCells):
    /// the test since 2026-10-06, after the motion core showed how ScanForCollisions counts.</summary>
    private int miReachCells;

    /// <summary>Was the player already in reach in the last frame? It only fires
    /// on ENTERING, otherwise the trigger would fire again every frame.</summary>
    private bool mbInside;

    public void Initialise(UWLevelLoader pOLoader, UWObject pOTriggerData, float pfReach, int piReachCells = 4)
    {
        mOLoader = pOLoader;
        mOTriggerData = pOTriggerData;
        mfReach = pfReach;
        miReachCells = piReachCells;
    }

    /// <summary>Fires on EVERY entry, not just the first. For a while this latched
    /// after the first time, for fear of a door that toggles with every step.
    /// That fear was unfounded: the action is in the trap's "quality" field,
    /// and the two move triggers on level 1 only close (quality 2). Repeated
    /// firing therefore has no effect, and the user test in the original shows exactly that
    /// behaviour - the portcullis drops when entering the tile (2026-08-29). A
    /// latching trigger would wrongly do nothing on the second visit.
    ///
    /// Whether a trap fires a second time at all is decided by the trigger itself anyway,
    /// via bit 1 of its flags (see UWTriggerSystem).</summary>
    private void OnTriggerStay(Collider pOOther)
    {
        if (mOLoader == null)
            return;

        // WHO MAY SET IT OFF (UWTrapRules.MaySetOff, UseTrigger): the player only with flags bit 2 -
        // never the rune of warding's trigger (flags 8). Until 2026-10-12 this path told the rune
        // apart by its flags 0 and searched the creatures for it itself; the creatures' motion
        // sets it off now (UWMotionHost.TriggerMove).
        if (!UWTrapRules.MaySetOff(mOTriggerData, UWTrapRules.Setter.Player))
            return;

        // Only the player fires it, not items lying around.
        UWPlayerMovement lOMovement = pOOther.GetComponentInParent<UWPlayerMovement>();

        if (lOMovement == null)
            return;

        // ON THE MOTION CORE the player's collision fires the trigger as the original does - on
        // every motion frame in reach, not on entering (UWMotionHost.TriggerMove,
        // 2026-10-06). This volume served the CharacterController path only, which went in
        // cleanup 4.1 (2026-10-18): with a world the motion always exists, so this returns.
        if (lOMovement.UsesMotionCore)
            return;

        mOPlayer = lOMovement.transform;

        // Interaction sits on the camera BELOW the body that touches the volume, so looking only
        // upwards found nothing - and a text trap behind a move trigger stayed silent: the
        // poison needles on level 3, 1/7 and 1/9 (per user on the original, 2026-09-17).
        if (mOInteraction == null)
            mOInteraction = lOMovement.GetComponentInChildren<Interaction>();

        bool lbInside = fIsInReach(mOPlayer.position);

        if (lbInside && !mbInside)
        {
            // A one-shot trigger is removed from its tile after firing
            // (UWTriggerSystem.fRemoveOneShotTrap). This GameObject stayed, however, and
            // fired the trap again on every entry - arrow trap and fire elemental on
            // level 5 around 19/50 (per user, 2026-09-13). If it is no longer in the tile,
            // it is gone.
            if (!fIsStillInTile())
            {
                Destroy(gameObject);
                return;
            }

            UWTriggerSystem.TryFireMoveTrigger(mOTriggerData, mOLoader, mOInteraction);

            if (!fIsStillInTile())
            {
                Destroy(gameObject);
                return;
            }
        }

        mbInside = lbInside;
    }

    /// <summary>Is the trigger still in any tile of its level? The search covers
    /// all tiles, because the position of the volume is not necessarily its tile.</summary>
    private bool fIsStillInTile()
    {
        if (mOTriggerData == null || mOLoader == null || mOLoader.CurrentLevel == null
            || mOLoader.CurrentLevel.TileData == null)
            return true;

        UWTilePos lOTile = mOLoader.WorldPositionToTile(transform.position);
        UWTile lOAt = mOLoader.CurrentLevel.GetTile(lOTile.X, lOTile.Y);

        if (lOAt != null && lOAt.ObjectsInTile != null && lOAt.ObjectsInTile.Contains(mOTriggerData))
            return true;

        foreach (UWTile lOOther in mOLoader.CurrentLevel.TileData)
        {
            if (lOOther != null && lOOther.ObjectsInTile != null && lOOther.ObjectsInTile.Contains(mOTriggerData))
                return true;
        }

        return false;
    }

    private void OnTriggerExit(Collider pOOther)
    {
        if (pOOther.GetComponentInParent<UWPlayerMovement>() != null)
            mbInside = false;
    }

    /// <summary>The original's test: PER AXIS separately, not by distance. That
    /// gives a square around the trigger point, not a circle.</summary>
    /// <summary>The original's box test in eighth cells: the player's cell against the trigger's
    /// (its tile times eight plus its sub-tile position), at most miReachCells apart per axis.
    /// Until 2026-10-06 a world distance of trigger radius plus player radius, which reached one
    /// eighth farther than the original on each side.</summary>
    private bool fIsInReach(Vector3 pOPosition)
    {
        if (mOTriggerData == null)
        {
            return Mathf.Abs(pOPosition.x - transform.position.x) <= mfReach
                && Mathf.Abs(pOPosition.z - transform.position.z) <= mfReach;
        }

        int liPlayerX = UWViewpoint.WorldToOriginalX(pOPosition.x) >> 5;
        int liPlayerY = UWViewpoint.WorldToOriginalY(pOPosition.z) >> 5;
        int liTriggerX = (mOTriggerData.TileX << 3) + (mOTriggerData.XPos & 7);
        int liTriggerY = (mOTriggerData.TileY << 3) + (mOTriggerData.YPos & 7);

        return Mathf.Abs(liPlayerX - liTriggerX) <= miReachCells && Mathf.Abs(liPlayerY - liTriggerY) <= miReachCells;
    }
}
