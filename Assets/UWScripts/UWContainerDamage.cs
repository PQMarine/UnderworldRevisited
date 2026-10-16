using UnityEngine;
using UnderworldRevisited.Build;
using UWDataImport.UWData;

/// <summary>
/// What happens to a barrel, chest or nightstand when you smash it in - and, since 2026-10-14,
/// to every other model a blow can wear out (a bench): the general branch of remove_object.
///
/// The reference (damage.cs, item ids 0x15B and 0x15D) removes the lock and then uses the
/// container, so its contents end up on the tile - exactly what happens when you use an
/// unlocked one (see Interaction.SpillWorldContainer). Afterwards the container object itself
/// BECOMES the debris (damage.cs: objToDestroy.item_id = Debris), so the barrel or chest is
/// gone and a pile of debris lies in its place, which unlike blood can be picked up
/// (per user, 2026-09-16: "a pile of debris stays behind at the chest. Unlike the blood
/// pools it can be picked up", and the same for barrels).
///
/// THE NIGHTSTAND IS NOT USED (remove_object, UnderworldExhumed combat/DAMAGE.C, read
/// 2026-10-12): only the chest and the barrel go through remove_lock and UseCont; the
/// nightstand falls into the branch of every other object, whose chain is freed - what lay in it
/// is gone with it, and it becomes debris too. Its use triggers still fire first. Until
/// 2026-10-12 ours spilled it like a chest. The chest and barrel lose one lock and are used, so
/// a second, locked lock keeps their contents (UWObjectDamageRules.IsUsedWhenDestroyed).
///
/// UNDER FIRE THE NIGHTSTAND SMOKES one time in four, as every object of that branch
/// (put_effect 8, UWObjectDamageRules.DecideRemains); the chest and the barrel never do. Until
/// 2026-10-13 ours never smoked here - the damage type did not reach this component
/// (UWDamageable.LastDamageType; reference audit, finding of the fix agents).
///
/// Sits next to UWDamageable and only reacts to its destruction notification, like
/// UWDoor for doors.
/// </summary>
[RequireComponent(typeof(UWDamageable))]
public class UWContainerDamage : MonoBehaviour
{
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

        bool lbUsed = lOEntity.ObjectData != null && UWObjectDamageRules.IsUsedWhenDestroyed(lOEntity.ObjectData.ID);

        if (lOInteraction != null && lOEntity.ObjectData != null)
        {
            if (lbUsed)
                lOInteraction.SpillWorldContainer(lOEntity, false, false, true);
            else
                lOInteraction.FireDestroyedUseTriggers(lOEntity.ObjectData);
        }

        if (!lbUsed && lOEntity.ObjectData != null
            && UWObjectDamageRules.DecideRemains(lOEntity.ObjectData.ID, pODamageable.LastDamageType)
                == UWObjectDamageRules.Remains.DebrisWithSmoke)
            fRaiseSmoke(lOEntity);

        fLeaveDebris(lOEntity);
    }

    /// <summary>The smoke where the object stood, placed as UWTileBlast places it for a loose
    /// object (SpawnClass7Object copies the object's position).</summary>
    private void fRaiseSmoke(UWEntityInfo pOEntity)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader == null)
            return;

        UWTilePos lOTile = lOLoader.WorldPositionToTile(pOEntity.transform.position);

        lOLoader.SpawnEffectAt(UWObjectDamageRules.SmokeObjectId,
            UWObjectSpawner.GetObjectWorldPosition(lOTile.X, lOTile.Y, pOEntity.ObjectData)
                + new Vector3(0f, UWObjectSpawner.AnimatedHeightOffset, 0f),
            UWObjectDamageRules.RollSmokeSeconds());
    }

    /// <summary>
    /// The smashed container disappears and leaves a pile of debris where it stood -
    /// same tile, same spot within it, so the pile does not jump to the tile centre. The
    /// remove and the pile are UWDebris, the same path as the blast's (cleanup 2.6,
    /// 2026-10-12; until 2026-10-11 a second roll on Unity's random source here).
    /// </summary>
    private void fLeaveDebris(UWEntityInfo pOEntity)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader == null)
            return;

        Vector3 lOPosition = pOEntity.transform.position;

        UWDebris.ReplaceWithDebris(lOLoader, pOEntity, pOEntity.ObjectData, lOLoader.WorldPositionToTile(lOPosition),
            lOPosition);
    }
}
