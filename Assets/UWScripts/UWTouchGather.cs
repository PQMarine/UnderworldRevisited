using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// A glowing rock lying in the world, gathered by walking over it (UWGlowingRockRules): when the
/// player's body touches it while moving and he carries a glowing rock, the touched one joins it
/// and leaves the world. Put on the rock's sprite by UWObjectSpawner; its collider is a trigger,
/// as the rock is not solid.
///
/// THE TOUCH IS THE ORIGINAL'S COLLISION TEST, done here and not by Unity's contact: the two
/// boxes overlap per axis - horizontally within the rock's radius plus the player's (COMOBJ.DAT,
/// 1 + 2 eighths, UWTileQueries.TriggerReach), vertically between the feet and the top of the
/// body against the rock's height. The original tests it during a motion step, so only while the
/// player moves: standing on a rock and then picking up another by hand gathers the first on the
/// next step, not at once.
/// </summary>
public class UWTouchGather : MonoBehaviour
{
    /// <summary>Below this the feet count as standing still (world units).</summary>
    private const float MoveThreshold = 0.01f;

    private UWEntityInfo mOEntity;

    private Vector3 mOLastFeet;

    private bool mbHasLastFeet;

    private void Update()
    {
        UWCharacter lOCharacter = UWScene.Character;
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWInventory lOInventory = UWScene.Inventory;

        if (lOCharacter == null || lOLoader == null || lOInventory == null)
            return;

        if (mOEntity == null)
            mOEntity = GetComponent<UWEntityInfo>();

        UWObject lOData = mOEntity != null ? mOEntity.ObjectData : null;
        CharacterController lOBody = lOCharacter.GetComponentInParent<CharacterController>();

        if (lOData == null || lOBody == null)
            return;

        Vector3 lOFeet = new Vector3(lOBody.bounds.center.x, lOBody.bounds.min.y, lOBody.bounds.center.z);
        bool lbMoved = !mbHasLastFeet || (lOFeet - mOLastFeet).sqrMagnitude > MoveThreshold * MoveThreshold;

        mOLastFeet = lOFeet;
        mbHasLastFeet = true;

        if (!lbMoved || !fTouches(lOLoader, lOCharacter, lOData, lOFeet))
            return;

        if (!UWGlowingRockRules.TryGather(lOData, lOInventory.Model, lOLoader.CurrentLevel))
            return;

        lOLoader.RemoveObjectFromWorld(lOData);
        lOInventory.Model.NotifyChanged();
    }

    private bool fTouches(UWLevelLoader pOLoader, UWCharacter pOCharacter, UWObject pOData, Vector3 pOFeet)
    {
        float lfReach = pOLoader.GetTriggerReach(pOData.ID);

        if (Mathf.Abs(pOFeet.x - transform.position.x) > lfReach
            || Mathf.Abs(pOFeet.z - transform.position.z) > lfReach)
            return false;

        int liHeight = 0;
        UWCommonObjectProperties lOCommon = pOLoader.UWDataImporter != null
            ? pOLoader.UWDataImporter.CommonObjectProperties : null;

        if (lOCommon != null && lOCommon.TryGet(pOData.ID, out UWCommonObjectProperties.Entry lOEntry))
            liHeight = lOEntry.Height;

        int liBottom = UWUnits.RoundToInt(transform.position.y / UWWorldScale.ZPosStep);
        int liFeet = pOCharacter.GetFeetZPos();

        return liFeet <= liBottom + liHeight && liFeet + pOCharacter.GetBodyHeightZ() >= liBottom;
    }
}
