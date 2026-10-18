using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The door of a tile as one thing: the leaf or grate that moves (IUsableDoor - DoorMover for a
/// door and a secret door, PortcullisMover for the grate), the lock it carries (UWLockState), its
/// hit points (UWDamageable; the wooden doors only, see UWObjectSpawner.fSpawnPortcullis) and the
/// door object in the level data (UWEntityInfo.ObjectData).
///
/// Since 2026-10-12 (cleanup step 2.5) in place of UWDoorLock plus UWDoorDamage: the lock is
/// resolved ONCE here when the door starts, instead of at seven sites on first use; every change
/// of the door - leaf, lock, hit points - goes into the level data through the ONE
/// WriteStateToData below (the write-through rule, per user 2026-10-05: everything game-visible
/// goes into the data the moment it changes, not at the save - doors bit twice before, 2026-09-11
/// and 2026-10-05); and everyone who operates a door - the player, a creature, the door trap,
/// gronk_door, the blow that breaks it, the night - calls Toggle, Open, Close, ReplaceLock or
/// BurstOpen and lets UWDoorRules decide what the original would do.
///
/// The rules reach it as an IUWDoor through IUWWorld.Doors since cleanup step 3.6 (2026-10-17):
/// the night's closing doors are a rule over those (UWSleepRules.CloseDoorsAtNight).
/// </summary>
public class UWDoor : MonoBehaviour, IUWDoor
{
    private static readonly List<UWDoor> msAll = new List<UWDoor>();

    /// <summary>Every door of the level - for a creature's blow, which treats a door as an
    /// object in its tile (UWCritter.fTryDoorInReach), and for the night (UWSleep).</summary>
    public static IReadOnlyList<UWDoor> All => msAll;

    /// <summary>Where the door object stands: the centre of the closed leaf, taken when the
    /// component is added (the spawner has placed the door by then; opening turns the leaf about
    /// its hinge or lifts the grate, the door object stays).</summary>
    public Vector3 Centre { get; private set; }

    private IUsableDoor mIMover;

    /// <summary>The mover as a behaviour, for didStart: before its Start the door does not know
    /// whether it is open, and the data keeps what it loaded.</summary>
    private MonoBehaviour mOMoverBehaviour;

    private UWEntityInfo mOInfo;

    private UWDamageable mODamageable;

    private UWLockState mOLock;

    private bool mbWriting;

    private void Awake()
    {
        Centre = transform.position;
        msAll.Add(this);

        // The mover is added before this component (UWObjectSpawner.fSpawnDoor, fSpawnPortcullis,
        // fSpawnSecretDoor), so it is already here.
        mIMover = GetComponent<IUsableDoor>();
        mOMoverBehaviour = mIMover as MonoBehaviour;

        if (mIMover != null)
        {
            mIMover.StateChanged += WriteStateToData;
            mIMover.ClosingBlocked = fClosedLeafBlocked;
        }
    }

    /// <summary>Whether the closed leaf would not fit where it stands - see
    /// UWMotionHost.ClosedDoorBlocked.</summary>
    private bool fClosedLeafBlocked()
    {
        UWObject lOData = Data;
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOData == null || lOLoader == null)
            return false;

        UWMotionHost lOWorld = UWMotionHost.Ensure(lOLoader);

        return lOWorld != null && lOWorld.ClosedDoorBlocked(lOData);
    }

    private void Start()
    {
        fEnsureLock();

        UWDamageable lODamageable = Damageable;

        if (lODamageable != null)
        {
            lODamageable.Destroyed += fOnDestroyed;
            lODamageable.Damaged += fOnDamaged;
            lODamageable.TakesBlowFirst = fTakeBlowOnSpike;

            // A broken door is still struck, and a closed one bursts open (UWDamageable).
            lODamageable.TakesBlowsWhenBroken = true;
        }
    }

    private void OnDestroy()
    {
        msAll.Remove(this);

        if (mIMover != null)
            mIMover.StateChanged -= WriteStateToData;

        if (mODamageable != null)
        {
            mODamageable.Destroyed -= fOnDestroyed;
            mODamageable.Damaged -= fOnDamaged;
        }
    }

    // ------------------------------------------------- What the door is

    /// <summary>The door object in the level data.</summary>
    public UWObject Data
    {
        get
        {
            if (mOInfo == null)
                mOInfo = GetComponentInParent<UWEntityInfo>();

            return mOInfo != null ? mOInfo.ObjectData : null;
        }
    }

    /// <summary>The hit points, or null for a door that has none (the portcullis). Asked for
    /// lazily: the spawner adds UWDamageable after this component.</summary>
    public UWDamageable Damageable
    {
        get
        {
            if (mODamageable == null)
                mODamageable = GetComponent<UWDamageable>();

            return mODamageable;
        }
    }

    /// <summary>The engine-free lock state, resolved from the door's chain (UWDoorRules.ResolveLock).
    /// Never null; a door without a lock answers HasLock false.</summary>
    public UWLockState LockState
    {
        get
        {
            fEnsureLock();

            return mOLock;
        }
    }

    /// <summary>
    /// THE ONE PLACE THAT ASKS FOR A DOOR'S LOCK. Normally once, in Start; a caller that comes
    /// earlier (a capture right after the load) or while the level is still being built gets it
    /// resolved on the spot, and an unresolved attempt (no masterlist yet) is simply made again.
    /// </summary>
    private void fEnsureLock()
    {
        if (mOLock != null && mOLock.IsResolved)
            return;

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        List<UWObject> lOMasterlist = lOLoader != null && lOLoader.CurrentLevel != null
            ? lOLoader.CurrentLevel.Masterlist
            : null;

        if (mOLock == null)
        {
            mOLock = new UWLockState();
            mOLock.Changed += WriteStateToData;
        }

        mOLock.EnsureResolved(Data, lOMasterlist);
    }

    public bool HasLock => LockState.HasLock;

    public bool IsLocked => LockState.IsLocked;

    public int LockId => LockState.LockId;

    /// <summary>At rest and shut. False while the leaf moves, either way.</summary>
    public bool IsClosed => mIMover != null && mIMover.IsClosed;

    public bool IsMoving => mIMover != null && mIMover.IsMoving;

    /// <summary>Shut, or on its way to shut - what OpenDoor and CloseDoor of the original decide
    /// on (see UWDoorRules).</summary>
    public bool IsHeadingClosed => mIMover != null && mIMover.IsHeadingClosed;

    /// <summary>Bit 0 of the door's owner field (UWObjectMechanics.IsDoorSpiked).</summary>
    public bool IsSpiked => Data != null && UWObjectMechanics.IsDoorSpiked(Data);

    // ------------------------------------------------- Operating it

    /// <summary>
    /// Sets the leaf in motion, whatever the lock says: a closed door opens, an open one closes, a
    /// swing is turned round - the same entry point a click uses, so the leaf swings, the sound
    /// plays and the tile blocker comes back exactly as it does otherwise. The caller has asked
    /// UWDoorRules whether the door is to move at all.
    ///
    /// A door opened FROM REST loses its spike bit and offers its own trigger chain the open
    /// event, whoever opened it (OpenDoor in USEITEMS.C: the trap, gronk_door and the breaking blow
    /// go through it as the player and the creatures do - B10, 2026-10-12; a swing turned round
    /// fires nothing). Whether the trigger takes it depends on the opener (UWTrapRules.MaySetOff,
    /// the same evening). An an_open trigger hangs in the door's chain and fires only on OPENING,
    /// so the decision is taken before the leaf moves.
    /// </summary>
    public void Toggle(UWTrapRules.Setter peOpener)
    {
        if (mIMover == null)
            return;

        bool lbOpensFromRest = mIMover.IsClosed;

        mIMover.StartUsingDoor();

        if (!lbOpensFromRest)
            return;

        UWObject lOData = Data;

        // The owner field is the level data itself: nothing to write through.
        UWDoorRules.ClearSpike(lOData);

        UWTriggerSystem.TryFireOpenTrigger(lOData, UWScene.LevelLoader, UWScene.Interaction, peOpener);
    }

    /// <summary>OpenDoor: moves the leaf when the door is closed or closing, else nothing.</summary>
    public void Open(UWTrapRules.Setter peOpener)
    {
        if (UWDoorRules.OpenActs(IsHeadingClosed))
            Toggle(peOpener);
    }

    /// <summary>CloseDoor: moves the leaf when the door is open or opening, else nothing.</summary>
    public void Close()
    {
        // Never opens from rest, so the opener does not matter.
        if (UWDoorRules.CloseActs(IsHeadingClosed))
            Toggle(UWTrapRules.Setter.Thing);
    }

    /// <summary>The lock springs open and stays on the door (the modern scheme's key in the
    /// backpack; key, pick and spell go through UWLockRules on LockState).</summary>
    public void Unlock()
    {
        LockState.Unlock();
    }

    /// <summary>Removes the lock entirely (see UWLockState.RemoveLock).</summary>
    public void RemoveLock()
    {
        LockState.RemoveLock();
    }

    /// <summary>
    /// The door trap's lock side (UWDoorRules.DoorTrap): the lock on the door is taken off and a
    /// copy of the template the trap links to is put on - the same lock however often it fires.
    /// Until 2026-10-06 a lock was removed when there was one and the template applied when there
    /// was none, which a repeated close trap turned into locked, unlocked, locked. Without a
    /// template the door is lockless afterwards.
    /// </summary>
    public void ReplaceLock(UWObject pOTemplate)
    {
        UWLockState lOLock = LockState;

        if (lOLock.HasLock)
            lOLock.RemoveLock();

        lOLock.ApplyLock(pOTemplate);
    }

    /// <summary>
    /// What happens to a door when you bash it in (UWDoorRules.BurstOpens, the original's
    /// remove_object). Checked by user in the original (2026-08-27): a door has hit points,
    /// looking at it shows its condition (sturdy, badly damaged, broken), and at "broken" it opens
    /// by itself AND is no longer locked afterwards - it can be freely opened and closed.
    /// </summary>
    public void BurstOpen()
    {
        // damage_object passes the attacker to OpenDoor; UWDamageable does not keep it. Every
        // open trigger of UW1 answers the player and a thing alike (UWTrapRules.TryFireOpenTrigger),
        // so only a creature breaking such a door would differ.
        if (UWDoorRules.BurstOpens(IsClosed))
            Toggle(UWTrapRules.Setter.Thing);

        RemoveLock();
    }

    /// <summary>
    /// Takes durability off the spike of a spiked, closed door (UWItemApplications.TryDamageDoorSpike).
    /// Returns true if the blow is used up by that - then the door itself takes nothing. Every
    /// blow on the door's UWDamageable passes here first (TakesBlowFirst), so a creature's bash,
    /// a missile, a blast and a spell meet the spike as the player's melee does - B4, 2026-10-12;
    /// until then only the player's own strike honoured it (damage_object in DAMAGE.C redirects
    /// the damage on a closed spiked door 0x140-0x147 to the spike's strength). Since the same
    /// day the damage arrives after the quality class shift and never from a protected or class
    /// 3 door (UWDamageable.ApplyDamage), and the player's blow no longer goes to the spike
    /// beforehand in full.
    /// </summary>
    public bool TryDamageSpike(int piDamage)
    {
        UWObject lOData = Data;

        // The spike's strength sits in the owner field of the level data itself.
        return lOData != null && UWItemApplications.TryDamageDoorSpike(lOData, IsClosed, piDamage);
    }

    private bool fTakeBlowOnSpike(int piDamage, int piDamageType)
    {
        return TryDamageSpike(piDamage);
    }

    private void fOnDestroyed(UWDamageable pODamageable)
    {
        BurstOpen();
    }

    private void fOnDamaged(UWDamageable pODamageable)
    {
        WriteStateToData();
    }

    // ------------------------------------------------- The data

    /// <summary>
    /// THE DATA FOLLOWS THE DOOR: the open state (an open door's object is lifted 24 above the
    /// floor and is no obstacle to the motion core, a closed one stands in the way - found when
    /// goblins walked through a door the player had closed, 2026-10-05), the condition from the hit
    /// points and the lock, all through UWWorldCapture.CaptureDoor, which the save alone used to
    /// call. Raised by the mover on every change of IsClosed, by the lock state on every change,
    /// by every blow, and by UWWorldSync before a save or a level change. Before the mover's Start
    /// the open state is the data's own. The capture itself locks and unlocks the state while it
    /// copies a trap's template, hence the guard.
    /// </summary>
    public void WriteStateToData()
    {
        if (mbWriting)
            return;

        UWObject lOData = Data;

        if (lOData == null)
            return;

        fEnsureLock();

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWDamageable lODamageable = Damageable;
        bool lbStateKnown = mIMover != null && mOMoverBehaviour != null && mOMoverBehaviour.didStart;

        mbWriting = true;

        try
        {
            UWWorldCapture.CaptureDoor(lOLoader != null ? lOLoader.CurrentLevel : null, lOData, lbStateKnown,
                lbStateKnown && !mIMover.IsClosed,
                lODamageable != null, lODamageable != null ? lODamageable.CurrentHealth : 0,
                lODamageable != null ? lODamageable.InitialHealth : 0,
                mOLock);
        }
        finally
        {
            mbWriting = false;
        }
    }

    // ------------------------------------------------- Lock lookup for the player's tools

    /// <summary>
    /// The lock a key, a lockpick or the Unlock spell is applied to: a door's (the UWDoor on the
    /// target), else the lock object in the chain of a barrel or chest (UWLockRules.FindContainerLock)
    /// - the one lookup the three tools share (per user, 2026-09-16: the lockpick could not be
    /// applied to the chest at all before the container branch existed).
    /// </summary>
    public static void ResolveLockTarget(UWEntityInfo pOTarget, UWLevel pOLevel,
        out UWLockState pODoorLock, out UWObject pOContainerLock)
    {
        pODoorLock = null;
        pOContainerLock = null;

        if (pOTarget == null)
            return;

        UWDoor lODoor = pOTarget.GetComponentInParent<UWDoor>();

        if (lODoor != null)
            pODoorLock = lODoor.LockState;
        else if (pOLevel != null)
            pOContainerLock = UWLockRules.FindContainerLock(pOTarget.ObjectData, pOLevel.Masterlist);
    }
}
