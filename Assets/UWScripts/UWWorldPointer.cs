using UnityEngine;

/// <summary>
/// The right button in the 3D view, recognised once for everybody.
///
/// Until 2026-09-18 two classes read the same button: Interaction ran a raycast on the press and
/// called any mouse movement before the release a drag (use the door, throw the switch) and
/// none a click (look); UWItemDrag ran a second raycast for a pickable thing and used a
/// six-pixel threshold of its own. On small movements the two disagreed, and each carried rules
/// against the other (lbHandledByItemDrag, PageInputConsumedThisFrame for the view).
///
/// Now Interaction measures the aim once on the press (see Interaction.fBeginWorldPointer),
/// this class turns the button's movement into ONE gesture, and Interaction resolves the gesture
/// into an action (look, use, drag a pickable thing) - the same actions the command icons of the
/// original (Talk, Get, Look, Use) will call once they exist.
///
/// THE ORIGINAL (checked by user, 2026-09-18): in the view window the look comes on the press
/// of the right button; then the threshold decides - within it one may jitter, nothing starts.
/// A pickable thing follows the pointer from the sixth pixel on; anything else is USED ON THE
/// RELEASE after the threshold was crossed. Outside the view window, on the inventory, all
/// actions come on the release, and the drag starts from the sixth pixel there too.
/// </summary>
public sealed class UWWorldPointer
{
    /// <summary>Movement below this many screen pixels is a click, from it on a drag - the
    /// threshold UWItemDrag has used for the inventory since the beginning.</summary>
    public const float DragThreshold = 6f;

    public enum EventKind
    {
        None,

        /// <summary>The button is still held and has moved past the threshold - reported once.</summary>
        DragStarted,

        /// <summary>Released without ever passing the threshold.</summary>
        ReleasedAsClick,

        /// <summary>Released after the threshold was passed (in this frame or earlier).</summary>
        ReleasedAfterDrag
    }

    /// <summary>What lay under the pointer when the button went down.</summary>
    public struct Aim
    {
        /// <summary>The frontmost thing with entity info - null for wall, floor or nothing.</summary>
        public UWEntityInfo Frontmost;

        /// <summary>The frontmost PICKABLE thing along the same ray, behind a blood pool or bone
        /// pile if need be (see Interaction.TryGetPickupTarget) - null if there is none.</summary>
        public UWEntityInfo Pickable;

        /// <summary>The wall or floor texture name when nothing with entity info was hit.</summary>
        public string ChunkDescription;

        public Vector2 PressPosition;
    }

    private Aim mOAim;

    private bool mbPressed;

    private bool mbDragStarted;

    public bool IsPressed
    {
        get { return mbPressed; }
    }

    public Aim Current
    {
        get { return mOAim; }
    }

    /// <summary>A world drag took the thing onto the pointer: the release does nothing more.</summary>
    public bool DragConsumed { get; set; }

    public void Begin(Aim pOAim)
    {
        mOAim = pOAim;
        mbPressed = true;
        mbDragStarted = false;
        DragConsumed = false;
    }

    /// <summary>The press turned out to be something else (an attack) - forget it.</summary>
    public void Cancel()
    {
        mbPressed = false;
        mbDragStarted = false;
        DragConsumed = false;
    }

    /// <summary>Once per frame while pressed. pbReleased is the button's release in this frame.
    /// Crossing the threshold is reported even in the frame of the release - the original acts
    /// on the crossing, not on the release (per user, 2026-09-18).</summary>
    public EventKind Poll(Vector2 pOPosition, bool pbReleased)
    {
        if (!mbPressed)
            return EventKind.None;

        if (pbReleased)
            mbPressed = false;

        if (!mbDragStarted && Vector2.Distance(mOAim.PressPosition, pOPosition) >= DragThreshold)
        {
            mbDragStarted = true;

            return EventKind.DragStarted;
        }

        if (!pbReleased)
            return EventKind.None;

        return mbDragStarted ? EventKind.ReleasedAfterDrag : EventKind.ReleasedAsClick;
    }
}
