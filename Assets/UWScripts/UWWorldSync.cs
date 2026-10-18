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

    /// <summary>
    /// THE RECORD, not the body (cleanup step 4.8, 2026-10-18): hit points, attitude, goal and
    /// position are what the creature's record holds (UWWorldCapture.CaptureCritter). Until then
    /// this handed over the body's hit points and its logical world point (UWCritter.BodyPosition),
    /// which the capture converted back, and "moved" was that point more than one unit away from
    /// where the body was set up. The facing needs no write of its own: the record writes the
    /// heading eighth and the fine residual through to the UWNpc as the brain turns the creature
    /// (contract section 6).
    /// </summary>
    private static void fCaptureCritter(UWLevelLoader pOLoader, UWEntityInfo pOEntity, UWCritter pOCritter)
    {
        UWCritterRecord lORecord = pOCritter.Record;

        if (!(pOEntity.ObjectData is UWNpc) || lORecord == null || lORecord.Npc != pOEntity.ObjectData)
            return;

        UWDamageable lODamageable = pOCritter.Damageable;

        UWWorldCapture.CaptureCritter(pOLoader.TileQueries, lORecord, lODamageable != null && lODamageable.IsDestroyed,
            lODamageable != null ? lODamageable.InitialHealth : 0, pOCritter.HasMoved);
    }

    /// <summary>The door writes its own state (leaf, hit points, lock) the moment it changes;
    /// this is the same write once more before the save or the level change (UWDoor, 2026-10-12).</summary>
    private static void fCaptureDoor(UWLevelLoader pOLoader, UWEntityInfo pOEntity)
    {
        UWDoor lODoor = pOEntity.GetComponentInParent<UWDoor>();

        if (lODoor != null)
            lODoor.WriteStateToData();
    }
}
