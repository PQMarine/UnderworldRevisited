using UnityEngine;

/// <summary>
/// What happens to a door when you bash it in.
///
/// Checked by user in the original (2026-08-27): a door has hit points, looking at it
/// shows its condition (sturdy, badly damaged, broken), and at "broken" it opens by itself
/// AND is no longer locked afterwards - it can be freely opened and closed.
/// That is exactly what this class implements.
///
/// It sits on the door object next to UWDamageable and only reacts to its
/// destruction notification; the damage calculation itself is none of its business.
/// </summary>
[RequireComponent(typeof(UWDamageable))]
public class UWDoorDamage : MonoBehaviour
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
        // Remove the lock first, then open: otherwise a locked door would be stopped
        // by the lock check while springing open.
        UWDoorLock lOLock = GetComponentInParent<UWDoorLock>();

        if (lOLock != null)
            lOLock.RemoveLock();

        IUsableDoor lIDoor = GetComponentInParent<IUsableDoor>();

        if (lIDoor != null && lIDoor.IsClosed)
            lIDoor.StartUsingDoor();
    }
}
