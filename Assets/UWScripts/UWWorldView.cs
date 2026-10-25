using System;
using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// THE PICTURE OF A LEVEL'S OBJECTS, following its data (cleanup step 2.12, 2026-10-12): the
/// engine-free UWWorldEdit changes the tile lists and raises events, this builds and destroys the
/// GameObjects for them - ObjectPlaced spawns the object through the spawner of the level build
/// (UWObjectSpawner), ObjectRemoved destroys its picture, LevelCleared all of them. Every object
/// it builds gets the active render path here, at the one exit (UWRenderPathHost.
/// OnObjectSpawned): without that, everything created at runtime stayed on the old path -
/// visible because it was the only thing the player light fell on.
///
/// It owns the registry from a data object to its picture (TryGetEntity), which UWLevelLoader
/// kept until then: data-driven triggers (UWTriggerSystem) have - unlike the player via raycast -
/// no click hit through which they could find a UWEntityInfo for the target tile, and all
/// objects hang flat under the object node, not grouped per tile.
///
/// A moved object needs nothing here: a creature's picture follows its own motion.
///
/// One per level build; UWLevelLoader makes it with the object node and attaches it to the
/// level's UWWorldEdit.
/// </summary>
public sealed class UWWorldView
{
    private readonly Transform mOObjectRoot;

    private readonly Func<UWObjectSpawner> mFSpawner;

    private readonly Action<GameObject> mOApplyRenderMode;

    private readonly Dictionary<UWObject, UWEntityInfo> mEntityByData = new Dictionary<UWObject, UWEntityInfo>();

    private UWWorldEdit mOEdit;

    /// <param name="pFSpawner">The spawner of the level build (UWLevelLoader keeps one per build
    /// and renews it when atlas or materials change).</param>
    /// <param name="pOApplyRenderMode">Puts a freshly built node on the active render path.</param>
    public UWWorldView(Transform pOObjectRoot, Func<UWObjectSpawner> pFSpawner, Action<GameObject> pOApplyRenderMode)
    {
        mOObjectRoot = pOObjectRoot;
        mFSpawner = pFSpawner;
        mOApplyRenderMode = pOApplyRenderMode;
    }

    /// <summary>The node every object's picture hangs under.</summary>
    public Transform ObjectRoot
    {
        get { return mOObjectRoot; }
    }

    /// <summary>Follows the changes of this level's data from now on (and no other's).</summary>
    public void Attach(UWWorldEdit pOEdit)
    {
        Detach();

        mOEdit = pOEdit;

        if (mOEdit == null)
            return;

        mOEdit.ObjectPlaced += fOnPlaced;
        mOEdit.ObjectRemoved += fOnRemoved;
        mOEdit.LevelCleared += fOnCleared;
    }

    public void Detach()
    {
        if (mOEdit == null)
            return;

        mOEdit.ObjectPlaced -= fOnPlaced;
        mOEdit.ObjectRemoved -= fOnRemoved;
        mOEdit.LevelCleared -= fOnCleared;
        mOEdit = null;
    }

    // ------------------------------------------------- The registry

    /// <summary>Every picture under the object node with its data, built anew - after the level
    /// build and after objects were caught up in a tile.</summary>
    public void RebuildRegistry()
    {
        mEntityByData.Clear();

        if (mOObjectRoot == null)
            return;

        foreach (UWEntityInfo lOEntity in mOObjectRoot.GetComponentsInChildren<UWEntityInfo>(true))
        {
            if (lOEntity.ObjectData != null)
                mEntityByData[lOEntity.ObjectData] = lOEntity;
        }
    }

    /// <summary>The picture of a data object (the door trap finds the door's GameObject for a
    /// UWObject found via the target tile, without a player click hit).</summary>
    public bool TryGetEntity(UWObject pOData, out UWEntityInfo pOEntity)
    {
        if (pOData != null)
            return mEntityByData.TryGetValue(pOData, out pOEntity);

        pOEntity = null;

        return false;
    }

    /// <summary>Whether a live picture of the object is registered.</summary>
    public bool Holds(UWObject pOData)
    {
        return TryGetEntity(pOData, out UWEntityInfo lOEntity) && lOEntity != null;
    }

    /// <summary>All pictures with their data - for UWWorldSync. Also disabled ones, so that
    /// nothing currently hidden slips through.</summary>
    public UWEntityInfo[] GetWorldEntities()
    {
        return mOObjectRoot != null
            ? mOObjectRoot.GetComponentsInChildren<UWEntityInfo>(true)
            : new UWEntityInfo[0];
    }

    /// <summary>Into the registry: every UWEntityInfo below a freshly built node. Otherwise after
    /// a conversation nobody finds the newly created creature, and it never reads its new
    /// attitude: Garamon stayed friendly instead of mellow (per user, compared with SAVE4,
    /// 2026-09-13).</summary>
    private void fRegister(Transform pOSpawned)
    {
        if (pOSpawned == null)
            return;

        foreach (UWEntityInfo lOEntity in pOSpawned.GetComponentsInChildren<UWEntityInfo>(true))
        {
            if (lOEntity.ObjectData != null)
                mEntityByData[lOEntity.ObjectData] = lOEntity;
        }
    }

    // ------------------------------------------------- Building

    /// <summary>The active render path for everything added under the object node since
    /// piChildCountBefore - the one exit every runtime picture passes.</summary>
    public void ApplyRenderModeFrom(int piChildCountBefore)
    {
        if (mOObjectRoot == null || mOApplyRenderMode == null)
            return;

        for (int liChild = piChildCountBefore; liChild < mOObjectRoot.childCount; liChild++)
            mOApplyRenderMode(mOObjectRoot.GetChild(liChild).gameObject);
    }

    /// <summary>
    /// Builds the pictures of the objects a tile's list already holds - for a tile that was
    /// solid at the level build (the spawner skips those; behind the secret wall on level 3,
    /// tile 48/54, a secret door would otherwise stay invisible). ONLY for such tiles, or the
    /// visible objects would be built a second time. Returns how many objects were handed to
    /// the spawner.
    /// </summary>
    public int SpawnTileObjects(int piTileX, int piTileZ, UWTile pOTile)
    {
        if (mOObjectRoot == null || pOTile == null || pOTile.ObjectsInTile == null)
            return 0;

        UWObjectSpawner lOSpawner = mFSpawner();

        // Via a copy: a spawner can add something to the tile list.
        UWObject[] lOObjects = pOTile.ObjectsInTile.ToArray();

        int liChildCountBefore = mOObjectRoot.childCount;

        foreach (UWObject lOObject in lOObjects)
            lOSpawner.SpawnSingleObject(piTileX, piTileZ, pOTile, lOObject, mOObjectRoot);

        ApplyRenderModeFrom(liChildCountBefore);
        RebuildRegistry();

        return lOObjects.Length;
    }

    /// <summary>
    /// The picture of an object that is in no tile list - a flying projectile, an effect: drawn
    /// as if appended to tile (x, z)'s list (UWObjectSpawner.SpawnUnlistedObject), on the
    /// active render path, registered when pbRegister. Returns the new nodes (normally one).
    /// </summary>
    public List<GameObject> SpawnUnlisted(UWObject pOObject, int piTileX, int piTileZ, UWTile pOTile, bool pbRegister)
    {
        List<GameObject> lONew = new List<GameObject>();

        if (mOObjectRoot == null || pOObject == null)
            return lONew;

        int liChildCountBefore = mOObjectRoot.childCount;

        mFSpawner().SpawnUnlistedObject(piTileX, piTileZ, pOTile, pOObject, mOObjectRoot);

        ApplyRenderModeFrom(liChildCountBefore);

        for (int liChild = liChildCountBefore; liChild < mOObjectRoot.childCount; liChild++)
            lONew.Add(mOObjectRoot.GetChild(liChild).gameObject);

        if (pbRegister && lONew.Count > 0)
            fRegister(lONew[lONew.Count - 1].transform);

        return lONew;
    }

    // ------------------------------------------------- The events of UWWorldEdit

    private void fOnPlaced(UWObject pOObject, int piTileX, int piTileZ)
    {
        if (mOObjectRoot == null || mOEdit == null || mOEdit.Level == null)
            return;

        int liChildCountBefore = mOObjectRoot.childCount;

        mFSpawner().SpawnSingleObject(piTileX, piTileZ, mOEdit.Level.GetTile(piTileX, piTileZ), pOObject, mOObjectRoot);

        // SpawnSingleObject always appends new objects as the last child via SetParent - no
        // return value on the actual spawn path is needed to get at the freshly created
        // GameObject.
        Transform lOSpawned = mOObjectRoot.childCount > liChildCountBefore
            ? mOObjectRoot.GetChild(mOObjectRoot.childCount - 1)
            : null;

        ApplyRenderModeFrom(liChildCountBefore);
        fRegister(lOSpawned);
    }

    private void fOnRemoved(UWObject pOObject)
    {
        if (!TryGetEntity(pOObject, out UWEntityInfo lOEntity))
            return;

        if (lOEntity != null)
            UnityEngine.Object.Destroy(lOEntity.gameObject);

        mEntityByData.Remove(pOObject);
    }

    private void fOnCleared()
    {
        if (mOObjectRoot != null)
        {
            for (int liChild = mOObjectRoot.childCount - 1; liChild >= 0; liChild--)
                UnityEngine.Object.Destroy(mOObjectRoot.GetChild(liChild).gameObject);
        }

        mEntityByData.Clear();
    }
}
