using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Lock of a door GameObject (normal door, secret door, portcullis), see Interaction.fTryUseDoor.
/// Most doors have no lock at all. The state and its rules live engine-free in UWLockState
/// (since 2026-09-17, P1 of the engine separation); this component only attaches it to the door.
/// </summary>
public class UWDoorLock : MonoBehaviour
{
    private readonly UWLockState mOState = new UWLockState();

    private static readonly List<UWDoorLock> msAll = new List<UWDoorLock>();

    /// <summary>Every door of the level - for a creature's blow, which treats a door as an
    /// object in its tile (UWCritter.fTryDoorInReach).</summary>
    public static IReadOnlyList<UWDoorLock> All => msAll;

    /// <summary>Where the door object stands: the centre of the closed leaf, taken when the
    /// component is added (the spawner has placed the door by then; opening turns the leaf about
    /// its hinge, the door object stays).</summary>
    public Vector3 Centre { get; private set; }

    /// <summary>The door object the lock hangs on, from the first EnsureResolved.</summary>
    private UWObject mODoorData;

    private bool mbWriting;

    private void Awake()
    {
        Centre = transform.position;
        msAll.Add(this);
        mOState.Changed += fWriteStateToData;
    }

    /// <summary>
    /// THE DATA FOLLOWS THE LOCK (2026-10-05, per user: write every game-visible state into the
    /// level data the moment it changes): every change of the lock state - by key, pick, spell,
    /// the door trap, a smashed door - goes into the door's lock object at once through
    /// UWWorldCapture.CaptureLock, which the save alone used to call. The capture itself locks
    /// and unlocks the state while it copies a trap's template, hence the guard.
    /// </summary>
    private void fWriteStateToData()
    {
        if (mbWriting)
            return;

        if (mODoorData == null)
        {
            UWEntityInfo lOInfo = GetComponentInParent<UWEntityInfo>();

            mODoorData = lOInfo != null ? lOInfo.ObjectData : null;
        }

        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (mODoorData == null || lOLoader == null || lOLoader.CurrentLevel == null)
            return;

        mbWriting = true;

        try
        {
            UWWorldCapture.CaptureLock(lOLoader.CurrentLevel, mODoorData, mOState);
        }
        finally
        {
            mbWriting = false;
        }
    }

    private void OnDestroy()
    {
        msAll.Remove(this);
    }

    /// <summary>The engine-free lock state.</summary>
    public UWLockState State => mOState;

    public bool HasLock => mOState.HasLock;

    public bool IsLocked => mOState.IsLocked;

    public bool IsResolved => mOState.IsResolved;

    public UWObject LockObject => mOState.LockObject;

    public int LockId => mOState.LockId;

    public int LockDifficulty => mOState.LockDifficulty;

    public void EnsureResolved(UWObject pODoorData, List<UWObject> pOMasterlist)
    {
        if (pODoorData != null)
            mODoorData = pODoorData;

        mOState.EnsureResolved(pODoorData, pOMasterlist);
    }

    public void Unlock()
    {
        mOState.Unlock();
    }

    public void Lock()
    {
        mOState.Lock();
    }

    public void RemoveLock()
    {
        mOState.RemoveLock();
    }

    public void ApplyLock(UWObject pOTemplate)
    {
        mOState.ApplyLock(pOTemplate);
    }
}
