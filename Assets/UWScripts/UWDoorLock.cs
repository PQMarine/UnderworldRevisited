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

    private void Awake()
    {
        Centre = transform.position;
        msAll.Add(this);
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
