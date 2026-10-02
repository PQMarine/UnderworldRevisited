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

    /// <summary>Reach per axis in world units - trigger radius plus player radius.</summary>
    private float mfReach;

    /// <summary>Was the player already in reach in the last frame? It only fires
    /// on ENTERING, otherwise the trigger would fire again every frame.</summary>
    private bool mbInside;

    public void Initialise(UWLevelLoader pOLoader, UWObject pOTriggerData, float pfReach)
    {
        mOLoader = pOLoader;
        mOTriggerData = pOTriggerData;
        mfReach = pfReach;
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

        // The ward rune searches by itself (see fUpdateWard) - for it the player is not a
        // trigger source.
        if (fIsWardTrigger())
            return;

        // Only the player fires it, not items lying around.
        UWPlayerMovement lOMovement = pOOther.GetComponentInParent<UWPlayerMovement>();

        if (lOMovement == null)
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
        UWTile[] lOTiles = mOLoader.CurrentLevel.TileData;
        int liAt = (lOTile.Y * 64) + lOTile.X;

        if (liAt >= 0 && liAt < lOTiles.Length && lOTiles[liAt] != null
            && lOTiles[liAt].ObjectsInTile != null && lOTiles[liAt].ObjectsInTile.Contains(mOTriggerData))
            return true;

        foreach (UWTile lOAt in lOTiles)
        {
            if (lOAt != null && lOAt.ObjectsInTile != null && lOAt.ObjectsInTile.Contains(mOTriggerData))
                return true;
        }

        return false;
    }

    private void OnTriggerExit(Collider pOOther)
    {
        if (pOOther.GetComponentInParent<UWPlayerMovement>() != null)
            mbInside = false;
    }

    /// <summary>
    /// A MOVE TRIGGER WITH ZERO FLAGS IS A WARD RUNE.
    ///
    /// No trigger in the shipped data has empty flags - the value only arises when
    /// the player places a ward rune (spell class 8, subclass 3). The reference sets
    /// it there explicitly and notes that the player therefore does NOT fire this
    /// trigger ("with this value set the player will not activate this move
    /// trigger"). After all it is meant to catch others, not the one who placed it.
    ///
    /// Because the mark is stored in the object itself and not in our display, the
    /// rune survives a rebuild of the level: on the next build fSpawnTransitions finds the
    /// trigger in the tile again and sets up its volume once more.
    /// </summary>
    private bool fIsWardTrigger()
    {
        return mOTriggerData != null && mOTriggerData.Flags == 0;
    }

    /// <summary>Who is currently standing in the rune - so that it fires once per creature
    /// on walking in and not on every tick.</summary>
    private System.Collections.Generic.HashSet<UWCritter> mOInsideCritters;

    /// <summary>
    /// How often the ward rune checks who is standing on it.
    ///
    /// NO UNITY TRIGGER EVENT. A creature in our port only carries a box and no
    /// rigidbody; between two such bodies Unity reports nothing at all. For the player
    /// it works because his CharacterController counts as a moving body. The rune therefore
    /// checks by itself - five times per second is more than enough, a creature
    /// takes considerably longer to cross a tile.
    /// </summary>
    private const float WardCheckInterval = 0.2f;

    private float mfWardTimer;

    private void Update()
    {
        if (!fIsWardTrigger() || mOLoader == null)
            return;

        mfWardTimer += Time.deltaTime;

        if (mfWardTimer < WardCheckInterval)
            return;

        mfWardTimer = 0f;

        fUpdateWard();
    }

    private void fUpdateWard()
    {
        if (mOInsideCritters == null)
            mOInsideCritters = new System.Collections.Generic.HashSet<UWCritter>();

        UWCritter[] lOCritters = UWScene.FindCritters();

        if (lOCritters == null)
            return;

        // Whoever has left counts again the next time they walk in.
        mOInsideCritters.RemoveWhere(pOAt => pOAt == null || !fIsInReach(pOAt.transform.position));

        for (int liAt = 0; liAt < lOCritters.Length; liAt++)
        {
            UWCritter lOCritter = lOCritters[liAt];

            if (lOCritter == null || !fIsInReach(lOCritter.transform.position))
                continue;

            if (!mOInsideCritters.Add(lOCritter))
                continue;

            if (mOInteraction == null)
                mOInteraction = UWScene.Interaction;

            UWTriggerSystem.TryFireWardTrigger(mOTriggerData, lOCritter, mOLoader, mOInteraction);
        }
    }

    /// <summary>The original's test: PER AXIS separately, not by distance. That
    /// gives a square around the trigger point, not a circle.</summary>
    private bool fIsInReach(Vector3 pOPosition)
    {
        return Mathf.Abs(pOPosition.x - transform.position.x) <= mfReach
            && Mathf.Abs(pOPosition.z - transform.position.z) <= mfReach;
    }
}
