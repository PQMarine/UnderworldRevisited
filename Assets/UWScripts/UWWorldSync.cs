using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Carries the runtime state of the current level over into its data - before saving and before
/// every level change (see UWSavegameWriter and UWLevelLoader.TravelTo). The rules of what is
/// written live engine-free in UWWorldCapture (since 2026-09-17, P2 of the engine separation);
/// this class reads the components of creatures, doors and switches and hands their values over.
/// </summary>
public static class UWWorldSync
{
    /// <summary>
    /// From this distance from its start position on, a creature counts as having moved. The
    /// conversion back to the eighths grid is not lossless - whoever stood still should keep their
    /// position exactly as it was loaded. One grid step is eight units.
    /// </summary>
    private const float MovedThreshold = 1f;

    public static void Capture(UWLevelLoader pOLoader)
    {
        if (pOLoader == null || pOLoader.CurrentLevel == null)
            return;

        foreach (UWEntityInfo lOEntity in pOLoader.GetWorldEntities())
        {
            if (lOEntity == null || lOEntity.ObjectData == null)
                continue;

            UWCritter lOCritter = lOEntity.GetComponent<UWCritter>();

            if (lOCritter != null)
            {
                fCaptureCritter(pOLoader, lOEntity, lOCritter);
                continue;
            }

            if (lOEntity.IsDoor)
            {
                fCaptureDoor(pOLoader, lOEntity);
                continue;
            }

            UWSwitchVisual lOSwitch = lOEntity.GetComponent<UWSwitchVisual>();

            if (lOSwitch != null && lOSwitch.WasToggled)
                UWWorldCapture.CaptureSwitch(lOEntity.ObjectData, lOSwitch.IsPressed);
        }
    }

    private static void fCaptureCritter(UWLevelLoader pOLoader, UWEntityInfo pOEntity, UWCritter pOCritter)
    {
        UWNpc lONpc = pOEntity.ObjectData as UWNpc;

        if (lONpc == null)
            return;

        UWDamageable lODamageable = pOEntity.GetComponent<UWDamageable>();

        // THE LOGICAL BODY, not the picture: since the rebuild the transform only shows the
        // picture catching up with the last step (UWCritter.BodyPosition), and the record
        // already holds this position - the capture keeps the two exactly in step.
        Vector3 lOAt = pOCritter.BodyPosition;
        bool lbMoved = (lOAt - pOCritter.SpawnPosition).sqrMagnitude > MovedThreshold * MovedThreshold;

        // The facing needs no write of its own any more: the creature record
        // (UWCritterRecord) writes the heading eighth and the fine residual through to the
        // UWNpc as the brain turns the creature (contract section 6).
        UWWorldCapture.CaptureCritter(pOLoader.TileQueries, lONpc,
            lODamageable != null && lODamageable.IsDestroyed,
            lODamageable != null, lODamageable != null ? lODamageable.CurrentHealth : 0,
            lODamageable != null ? lODamageable.InitialHealth : 0,
            pOCritter.Attitude, pOCritter.Goal, lbMoved, lOAt.x, lOAt.y, lOAt.z);
    }

    private static void fCaptureDoor(UWLevelLoader pOLoader, UWEntityInfo pOEntity)
    {
        IUsableDoor lIDoor = pOEntity.GetComponentInParent<IUsableDoor>();
        MonoBehaviour lODoorBehaviour = lIDoor as MonoBehaviour;

        // Before Start the door does not know its state yet (see DoorMover.Start).
        bool lbStateKnown = lIDoor != null && lODoorBehaviour != null && lODoorBehaviour.didStart;

        UWDamageable lODamageable = pOEntity.GetComponent<UWDamageable>();
        UWDoorLock lOLock = pOEntity.GetComponentInParent<UWDoorLock>();

        UWWorldCapture.CaptureDoor(pOLoader.CurrentLevel, pOEntity.ObjectData, lbStateKnown,
            lbStateKnown && !lIDoor.IsClosed,
            lODamageable != null, lODamageable != null ? lODamageable.CurrentHealth : 0,
            lODamageable != null ? lODamageable.InitialHealth : 0,
            lOLock != null ? lOLock.State : null);
    }
}
