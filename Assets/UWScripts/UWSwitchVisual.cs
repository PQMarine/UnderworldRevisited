using UnityEngine;

/// <summary>
/// Original (per user, tested in the original, 2026-08-28): switches/buttons (category
/// Switches, 368-383) have two images of the same switch, offset by 8 in TMFLAT.GR
/// (#0/#8, #1/#9, ... #7/#15). Lower half (0-7) = unpressed, upper half (8-15) = pressed.
///
/// Every use flips the image, STARTING FROM THE ONE STORED IN THE LEVEL (the object ID), as the
/// reference does it (button.ToggleItemID: pressed ids drop by 8, others rise by 8).
///
/// Until 2026-09-17 every switch started logically unpressed, from an observation on the
/// 377 buttons of level 1 (2026-08-28: "the original apparently thinks all buttons start
/// unpressed", the first use showed no change). The middle button of the level 3 puzzle
/// (5/7, also 377) contradicts that: in the original its image changes on the first press
/// (per user, 2026-09-17), and UW.EXE settles it: the switch case of the use routine
/// (seg040_352B_1B35) writes (index + 8) &amp; 0xF into the stored id before the trigger runs.
///
/// The logical state belongs to the switch alone and has NO relation to the state of the
/// controlled object - whoever opens/closes the door by hand in between permanently
/// desynchronises the two (user: "By fiddling around manually you can mess everything
/// up"). Also faithful to the original.
///
/// Sits next to the MeshFilter (see UWObjectSpawner.fSpawnDecal). Initialise deliberately sets
/// NO mesh - until the first use the image from the level state stays in place.
/// </summary>
public class UWSwitchVisual : MonoBehaviour
{
    private MeshFilter mFilter;
    private Mesh mUnpressedMesh;
    private Mesh mPressedMesh;

    /// <summary>Switch state. Starts as the stored image shows it.</summary>
    public bool IsPressed { get; private set; }

    /// <summary>pOUnpressedMesh = image from the lower TMFLAT half, pOPressedMesh = the one from
    /// the upper half. The caller decides which of them comes from the object ID.</summary>
    public void Initialise(MeshFilter pOFilter, Mesh pOUnpressedMesh, Mesh pOPressedMesh, bool pbStartsPressed)
    {
        IsPressed = pbStartsPressed;
        mFilter = pOFilter;
        mUnpressedMesh = pOUnpressedMesh;
        mPressedMesh = pOPressedMesh;
    }

    /// <summary>Whether the switch has been used since it was built. Only then does UWWorldSync
    /// write its image back into the object number.</summary>
    public bool WasToggled { get; private set; }

    /// <summary>For UWTriggerSystem.TryFireSwitch - every use flips the logical
    /// state; the image follows this state, not the controlled object.</summary>
    public void Toggle()
    {
        WasToggled = true;
        IsPressed = !IsPressed;

        if (mFilter != null)
            mFilter.sharedMesh = IsPressed ? mPressedMesh : mUnpressedMesh;
    }
}
