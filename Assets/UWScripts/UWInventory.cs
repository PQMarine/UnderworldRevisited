using System;
using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The player's inventory on the Unity side. The data and rules - equipment slots, backpack,
/// pointer item, containers, stacking, combining, weight, burning down - live engine-free in
/// UWInventoryModel (since 2026-09-17, P2 of the engine separation). This component owns one,
/// forwards the API unchanged, drives the burn tick with Unity's clock, sets the carried light
/// and handles what touches the world: dropping the pointer item into it and picking things up
/// from it.
///
/// Sits on the same GameObject as Interaction (created from there via AddComponent when needed,
/// see Interaction.Awake) - no manual wiring in the scene required.
/// </summary>
public class UWInventory : MonoBehaviour, IUWEquipment
{
    public const int BackpackSize = UWInventoryModel.BackpackSize;

    public const int ContainerSlotCount = UWInventoryModel.ContainerSlotCount;

    private UWLevelLoader mOLevelLoader;

    private UWInventoryModel mOModel;

    // The light sources burn on the player tick since 2026-09-24 (UWCharacter.fTickGameClock ->
    // UWInventoryModel.BurnTick); the own 25-second clock that was fitted to the torch measurement
    // of 2026-09-05 is gone - 21 seconds with the torch's rate of 3 fit that measurement as well.

    /// <summary>The engine-free inventory.</summary>
    public UWInventoryModel Model
    {
        get
        {
            fEnsureModel();
            return mOModel;
        }
    }

    private void Awake()
    {
        fEnsureModel();
    }

    private void fEnsureModel()
    {
        if (mOModel != null)
            return;

        mOLevelLoader = UWScene.LevelLoader;

        mOModel = new UWInventoryModel(
            () => mOLevelLoader != null ? mOLevelLoader.UWDataImporter : null,
            () => mOLevelLoader != null ? mOLevelLoader.CurrentLevel : null);

        mOModel.CarriedLightChanged += fSetCarriedLight;
    }

    /// <summary>Turns the player light on for a burning light source, or off (0).</summary>
    private void fSetCarriedLight(int piObjectId)
    {
        UWLighting lOLighting = UWScene.Lighting;

        if (lOLighting != null)
            lOLighting.SetCarriedLight(piObjectId);
    }

    // ------------------------------------------------- State

    public UWObject[] EquipSlots => Model.EquipSlots;

    public UWObject[] Backpack => Model.Backpack;

    public UWArmorItemMap.BodySlot MainHandSlot => Model.MainHandSlot;

    public bool IsLeftHanded
    {
        get { return Model.IsLeftHanded; }
        set { Model.IsLeftHanded = value; }
    }

    public UWObject CursorItem => Model.CursorItem;

    public UWObject UseModeItem => Model.UseModeItem;

    public UWObject OpenContainer => Model.OpenContainer;

    public UWObject ParentContainer => Model.ParentContainer;

    public int ContainerScrollOffset => Model.ContainerScrollOffset;

    public int WeightOffsetTenthStones => Model.WeightOffsetTenthStones;

    public bool CanDecreaseContainerScrollOffset => Model.CanDecreaseContainerScrollOffset;

    public bool CanIncreaseContainerScrollOffset => Model.CanIncreaseContainerScrollOffset;

    public event Action InventoryChanged
    {
        add { Model.InventoryChanged += value; }
        remove { Model.InventoryChanged -= value; }
    }

    public event Action<UWObject> LightBurnedOut
    {
        add { Model.LightBurnedOut += value; }
        remove { Model.LightBurnedOut -= value; }
    }

    // ------------------------------------------------- Forwarded rules

    public void EnterUseMode(UWObject pOItem) { Model.EnterUseMode(pOItem); }

    public void CancelUseMode() { Model.CancelUseMode(); }

    public int GetCarriedTenthStones(UWCommonObjectProperties pOProperties) { return Model.GetCarriedTenthStones(pOProperties); }

    public bool CanCarry(UWObject pOItem, int piCount = 0) { return Model.CanCarry(pOItem, piCount); }

    public void LoadFromPlayerData(UWPlayerData pOPlayer) { Model.LoadFromPlayerData(pOPlayer); }

    public bool IsInHandSlot(UWObject pOItem) { return Model.IsInHandSlot(pOItem); }

    public void NotifyChanged() { Model.NotifyChanged(); }

    public void ClearAll() { Model.ClearAll(); }

    public void ApplyCarriedLight() { Model.ApplyCarriedLight(); }

    public UWObject[] GetSavegameEquipment() { return Model.GetSavegameEquipment(); }

    public bool TakeFromStack(UWObject pOSource, int piCount) { return Model.TakeFromStack(pOSource, piCount); }

    public bool TryPlaceInFreeHandSlot(UWObject pOItem) { return Model.TryPlaceInFreeHandSlot(pOItem); }

    public bool TryConsumeOne(UWObject pOItem) { return Model.TryConsumeOne(pOItem); }

    public UWObject GetEquipped(UWArmorItemMap.BodySlot peSlot) { return Model.GetEquipped(peSlot); }

    public bool TryAddToBackpack(UWObject pOItem) { return Model.TryAddToBackpack(pOItem); }

    public void RemoveFromBackpack(int piSlot) { Model.RemoveFromBackpack(piSlot); }

    public void RemoveFromEquip(UWArmorItemMap.BodySlot peSlot) { Model.RemoveFromEquip(peSlot); }

    public void RemoveFromContainer(int piSlot) { Model.RemoveFromContainer(piSlot); }

    public void BeginDragFromBackpack(int piSlot) { Model.BeginDragFromBackpack(piSlot); }

    public void BeginDragFromEquip(UWArmorItemMap.BodySlot peSlot) { Model.BeginDragFromEquip(peSlot); }

    public bool TryStackInto(UWObject pOTarget) { return Model.TryStackInto(pOTarget); }

    public static bool CanStack(UWObject pOFirst, UWObject pOSecond, UWCommonObjectProperties pOProperties = null) { return UWInventoryModel.CanStack(pOFirst, pOSecond, pOProperties); }

    public bool TryCombineInto(UWObject pOTarget) { return Model.TryCombineInto(pOTarget); }

    public bool DropCursorItemInBackpack(int piSlot) { return Model.DropCursorItemInBackpack(piSlot); }

    public bool DropCursorItemInFirstFreeBackpackSlot() { return Model.DropCursorItemInFirstFreeBackpackSlot(); }

    public bool DropCursorItemInEquip(UWArmorItemMap.BodySlot peSlot) { return Model.DropCursorItemInEquip(peSlot); }

    public IEnumerable<UWObject> EnumerateAll() { return Model.EnumerateAll(); }

    public bool RemoveItem(UWObject pOItem) { return Model.RemoveItem(pOItem); }

    public void BeginDragFromExternal(UWObject pOItem) { Model.BeginDragFromExternal(pOItem); }

    public UWObject TakeCursorItem() { return Model.TakeCursorItem(); }

    public void CancelCursorItem(int? piOriginBackpackSlot, UWArmorItemMap.BodySlot? peOriginEquipSlot)
    {
        Model.CancelCursorItem(piOriginBackpackSlot, peOriginEquipSlot);
    }

    public bool TryEquip(UWObject pOItem, UWArmorItemMap.BodySlot peSlot, out UWObject pODisplaced)
    {
        return Model.TryEquip(pOItem, peSlot, out pODisplaced);
    }

    public bool TryAutoEquip(UWObject pOItem) { return Model.TryAutoEquip(pOItem); }

    public bool TryUnequip(UWArmorItemMap.BodySlot peSlot) { return Model.TryUnequip(peSlot); }

    public bool TryUnequipAnywhere(UWArmorItemMap.BodySlot peSlot) { return Model.TryUnequipAnywhere(peSlot); }

    public bool TryStoreAnywhere(UWObject pOItem) { return Model.TryStoreAnywhere(pOItem); }

    public bool DropCursorItemAnywhere() { return Model.DropCursorItemAnywhere(); }

    public bool TryOpenContainer(UWObject pOContainer) { return Model.TryOpenContainer(pOContainer); }

    public void CloseContainer() { Model.CloseContainer(); }

    public void CloseAllContainers() { Model.CloseAllContainers(); }

    public bool DropCursorItemIntoContainerItem(UWObject pOContainer) { return Model.DropCursorItemIntoContainerItem(pOContainer); }

    public UWObject GetContainerItem(int piSlot) { return Model.GetContainerItem(piSlot); }

    public void BeginDragFromContainer(int piSlot) { Model.BeginDragFromContainer(piSlot); }

    public bool DropCursorItemInContainer(int piSlot) { return Model.DropCursorItemInContainer(piSlot); }

    public void BeginDragFromContainerItem(UWObject pOContainer, int piIndex) { Model.BeginDragFromContainerItem(pOContainer, piIndex); }

    public bool DropCursorItemInContainerAt(UWObject pOContainer, int piIndex) { return Model.DropCursorItemInContainerAt(pOContainer, piIndex); }

    public bool InsertCursorItemInContainer(UWObject pOContainer, int piIndex) { return Model.InsertCursorItemInContainer(pOContainer, piIndex); }

    /// <summary>Why the last attempt to put something into a container failed, see
    /// UWContainerCapacity.</summary>
    public UWContainerCapacity.ResultEnum LastContainerRejection => Model.LastContainerRejection;

    /// <summary>The container of the last rejection, for the message.</summary>
    public UWObject LastContainerRejectionContainer => Model.LastContainerRejectionContainer;

    public void ScrollContainer(int piDelta) { Model.ScrollContainer(piDelta); }

    // ------------------------------------------------- The world

    /// <summary>
    /// Drops the item at the pointer into the 3D world (original: release outside the backpack/
    /// paperdoll). False if nothing hangs at the pointer, no level is loaded, or the target tile is
    /// invalid (see UWLevelLoader.SpawnDroppedObject - solid, or with pbRequireFlatFloor
    /// sloped/diagonal). pOFlightStartPosition is only set when throwing (see UWItemDrag) - lets the
    /// object visibly fly from there to the target position instead of appearing immediately.
    /// </summary>
    public bool DropCursorItemInWorld(Vector3 pOWorldPosition, bool pbRequireFlatFloor,
        Vector3? pOFlightStartPosition = null, int? piHeading = null)
    {
        if (CursorItem == null || mOLevelLoader == null)
            return false;

        if (!mOLevelLoader.SpawnDroppedObject(CursorItem, pOWorldPosition, pbRequireFlatFloor,
            pOFlightStartPosition, piHeading, true, true))
            return false;

        Model.ReleaseCursorItem();
        return true;
    }

    /// <summary>Starts an original drag directly from the 3D world (right-click drag on a pickable
    /// object on the ground) - the object disappears IMMEDIATELY from the world (as with normal
    /// pickup, see RemoveWorldObject) and is then in CursorItem.</summary>
    public void BeginDragFromWorld(UWEntityInfo pOEntity)
    {
        if (pOEntity == null || pOEntity.ObjectData == null)
            return;

        Model.PickUpToCursor(pOEntity.ObjectData);
        RemoveWorldObject(pOEntity);
        Model.NotifyChanged();
    }

    /// <summary>
    /// Removes a picked-up object from the level world for good: from the object list of its tile
    /// (prevents it from reappearing the next time the level is entered - UWLevelLoader rebuilds
    /// the level from exactly this list) and destroys its GameObject.
    /// </summary>
    public void RemoveWorldObject(UWEntityInfo pOEntity)
    {
        if (pOEntity == null)
            return;

        UWObject lOData = pOEntity.ObjectData;

        if (lOData != null && mOLevelLoader != null && mOLevelLoader.CurrentLevel != null)
        {
            UWTile lOTile = mOLevelLoader.TileQueries.GetTile(lOData.TileX, lOData.TileY);
            lOTile?.ObjectsInTile.Remove(lOData);
        }

        Destroy(pOEntity.gameObject);

        // Only AFTERWARDS: some items carry an a_pick up trigger, and in the original it is an
        // ambush - the pouch on level 3 (49/52) carries a creation trap that places a headless at
        // 44/55. The item itself is already pocketed by then; the data object lives on and carries
        // the chain.
        if (lOData != null && mOLevelLoader != null)
            UWTriggerSystem.TryFirePickUpTrigger(lOData, mOLevelLoader, GetComponent<Interaction>());
    }
}
