using UnityEngine;

/// <summary>
/// A billboard whose image cycles - fountains, fire, the silver tree and the
/// other moving objects (id 0x01C0 to 0x01CF).
///
/// The individual frames are in ANIMO.GR; which of them belong to which object is given in the
/// animation table of OBJECTS.DAT (UWObjectClassProperties.AnimationObject: start frame and
/// count). They are built when the object is placed (UWObjectSpawner.fSpawnBillboard); here they
/// are only advanced.
///
/// The frame rate is NOT in the data - none of the tables for these objects contains
/// a time value. FramesPerSecond is therefore a chosen value, deliberately public
/// for fine-tuning, like the project's other calibration values.
/// </summary>
public class UWAnimatedBillboard : MonoBehaviour
{
    /// <summary>Invented, not from the data - see class comment. The value is set
    /// on placement from UWSettings.AnimatedObjectFramesPerSecond.</summary>
    public float FramesPerSecond = 4f;

    private MeshFilter mOFilter;
    private Mesh[] mOFrames;
    private float mfPhase;
    private int miFrame;

    /// <summary>Sets the frame sequence. The first frame is then applied to the mesh immediately.</summary>
    public void Initialise(MeshFilter pOFilter, Mesh[] pOFrames, float pfStartOffset)
    {
        mOFilter = pOFilter;
        mOFrames = pOFrames;
        mfPhase = pfStartOffset;
        miFrame = 0;

        if (mOFilter != null && mOFrames != null && mOFrames.Length > 0)
            mOFilter.sharedMesh = mOFrames[0];
    }

    private void Update()
    {
        if (mOFilter == null || mOFrames == null || mOFrames.Length < 2 || FramesPerSecond <= 0f)
            return;

        // Absolute game time instead of accumulated frame times: this way all animated
        // objects and the palette rotation of walls and floors run at the same beat, no matter when
        // an object was created (per user, 2026-08-30: in the original, fountains and
        // water share the same beat).
        int liWanted = Mathf.FloorToInt((Time.time + mfPhase) * FramesPerSecond) % mOFrames.Length;

        if (liWanted == miFrame)
            return;

        miFrame = liWanted;
        mOFilter.sharedMesh = mOFrames[liWanted];
    }
}
