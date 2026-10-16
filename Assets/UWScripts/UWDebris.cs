using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE ONE REMOVE PATH for a destroyed static object and the pile of debris it leaves
/// (UWObjectDamageRules; cleanup step 2.6, 2026-10-12 - until then UWTileBlast.fDestroy and
/// UWContainerDamage.fLeaveDebris each had the remove-or-forget lines and their own quality
/// value for the pile). The original turns the object itself into the debris (remove_object's
/// SET_ITEM); ours takes the object out and puts a fresh pile on the same spot, so the pile
/// does not jump to the tile centre.
/// </summary>
public static class UWDebris
{
    /// <summary>Takes the object out of the world: from its tile's list and its display, or -
    /// when the list does not know it any more - the data alone with the entity destroyed by
    /// hand.</summary>
    public static void Remove(UWLevelLoader pOLoader, UWEntityInfo pOEntity, UWObject pOObject)
    {
        if (pOLoader == null || pOObject == null)
            return;

        if (!pOLoader.RemoveObjectFromWorld(pOObject) && pOEntity != null)
        {
            pOLoader.ForgetObjectData(pOObject);
            Object.Destroy(pOEntity.gameObject);
        }
    }

    /// <summary>Removes the object and puts one of the two piles (0xD5, 0xD6, one roll on
    /// UWRandom) where it stood - same tile, same spot, quality
    /// UWObjectDamageRules.DebrisQuality.</summary>
    public static void ReplaceWithDebris(UWLevelLoader pOLoader, UWEntityInfo pOEntity, UWObject pOObject,
        UWTilePos pOTile, Vector3 pOPosition)
    {
        Remove(pOLoader, pOEntity, pOObject);

        if (pOLoader == null)
            return;

        pOLoader.SpawnObjectById(UWObjectDamageRules.RollDebrisObjectId(), pOTile.X, pOTile.Y, 0,
            UWObjectDamageRules.DebrisQuality, new Vector3(pOPosition.x, 0f, pOPosition.z));
    }
}
