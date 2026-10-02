using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// What happens to a barrel, chest or nightstand when you smash it in.
///
/// The reference (damage.cs, item ids 0x15B and 0x15D) removes the lock and then uses the
/// container, so its contents end up on the tile - exactly what happens when you use an
/// unlocked one (see Interaction.SpillWorldContainer). Afterwards the container object itself
/// BECOMES the debris (damage.cs: objToDestroy.item_id = Debris), so the barrel or chest is
/// gone and a pile of debris lies in its place, which unlike blood can be picked up
/// (per user, 2026-09-16: "a pile of debris stays behind at the chest. Unlike the blood
/// pools it can be picked up", and the same for barrels).
///
/// Sits next to UWDamageable and only reacts to its destruction notification, like
/// UWDoorDamage for doors.
/// </summary>
[RequireComponent(typeof(UWDamageable))]
public class UWContainerDamage : MonoBehaviour
{
    /// <summary>First of the two debris objects, "a_pile of debris" (0xD5). The original
    /// picks one of the two at random (reference: damage.cs, uw1 branch
    /// "Debris = 0xD5 + Rng.r.Next(0, 2)"). Its uw2-only GetObjectTypeDebris would give
    /// wood chips (0xDB) for these containers; what the user sees in UW1 is the pile of
    /// debris.</summary>
    private const int FirstDebrisObjectId = 0xD5;

    private const int DebrisVariants = 2;

    private UWDamageable mODamageable;

    private void Awake()
    {
        mODamageable = GetComponent<UWDamageable>();
        mODamageable.Destroyed += fOnDestroyed;
    }

    private void OnDestroy()
    {
        if (mODamageable != null)
            mODamageable.Destroyed -= fOnDestroyed;
    }

    private void fOnDestroyed(UWDamageable pODamageable)
    {
        UWEntityInfo lOEntity = GetComponentInParent<UWEntityInfo>();
        Interaction lOInteraction = UWScene.Interaction;

        if (lOEntity == null)
            return;

        if (lOInteraction != null)
            lOInteraction.SpillWorldContainer(lOEntity, true, false);

        fLeaveDebris(lOEntity);
    }

    /// <summary>
    /// The smashed container disappears and leaves a pile of debris where it stood -
    /// same tile, same spot within it, so the pile does not jump to the tile centre.
    /// </summary>
    private void fLeaveDebris(UWEntityInfo pOEntity)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader == null)
            return;

        Vector3 lOPosition = pOEntity.transform.position;
        UWTilePos lOTile = lOLoader.WorldPositionToTile(lOPosition);

        if (!lOLoader.RemoveObjectFromWorld(pOEntity.ObjectData))
        {
            lOLoader.ForgetObjectData(pOEntity.ObjectData);
            Destroy(pOEntity.gameObject);
        }

        lOLoader.SpawnObjectById(FirstDebrisObjectId + Random.Range(0, DebrisVariants),
            lOTile.X, lOTile.Y, 0, -1, new Vector3(lOPosition.x, 0f, lOPosition.z));
    }
}
