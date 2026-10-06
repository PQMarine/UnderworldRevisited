using UnityEngine;

/// <summary>
/// Portcullis (326, 0x0146) does not open like a normal door by swinging,
/// according to game knowledge (not docs), but is pulled vertically up into the ceiling.
/// Same operating scheme as DoorMover (see IUsableDoor), but vertical
/// translation instead of rotation around the hinge.
/// </summary>
public class PortcullisMover : MonoBehaviour, IUsableDoor
{
    /// <summary>Distance the grate travels upwards when opening (90% of the grate's own
    /// height, see UWObjectSpawner.fSpawnPortcullis) - set on creation.</summary>
    public float LiftHeight { get; set; }

    /// <summary>As with DoorMover: 334 is the portcullis that is already raised.</summary>
    public bool StartsOpen { get; set; }

    private const float OpenDuration = 1.5f;

    /// <summary>The remaining lift of the current motion: negative while going up, positive
    /// while coming down (Update moves by the opposite sign).</summary>
    private float mCurrentLift;

    /// <summary>How far the grate is raised right now, 0 closed to LiftHeight open - the
    /// motions are counted from here (see DoorMover.mfOpenNow).</summary>
    private float mfLiftNow;

    /// <summary>The current motion lowers the grate (see DoorMover.mbClosing).</summary>
    private bool mbClosing;

    // As with DoorMover: a grate does not come down onto the player. The tile is watched for
    // the whole descent, the final frame included, and the collision returns only with the
    // grate completely down. CONFIRMED by the user in game for the bounce itself (2026-09-19).

    /// <summary>As with DoorMover.FirstStep: someone already in the tile sees the grate come
    /// down this much of the lift before it goes back up.</summary>
    private const float FirstStepFraction = 0.1f;

    /// <summary>How far the grate was raised when the current motion began.</summary>
    private float mfMotionStartLift;

    private BoxCollider mOTileBlocker;

    /// <summary>All colliders of the grate - the collision mesh for rays and the
    /// blocking box for the player (see UWObjectSpawner.fSpawnPortcullis).</summary>
    private Collider[] mOColliders;

    private bool mbIsClosed;

    /// <summary>As with DoorMover (see there): a raised grate has no
    /// player collision. With the portcullis this matters especially - even fully raised
    /// it still reaches into the player capsule (lift is 90% of its own height, see
    /// UWObjectSpawner.fSpawnPortcullis), so with collision you could not get through at all (reported
    /// per user, 2026-08-28: "But I can't get through portcullises"). Opening takes the
    /// collision away at once, closing brings it back only when the grate is down (per user
    /// on the original, 2026-09-19).</summary>
    public bool IsClosed
    {
        get { return mbIsClosed; }
        set
        {
            mbIsClosed = value;

            if (mOColliders == null)
                return;

            foreach (Collider lOCollider in mOColliders)
                UWPlayerCollision.SetIgnored(lOCollider, !mbIsClosed);

            foreach (Collider lOCoupled in mOCoupledColliders)
                UWPlayerCollision.SetIgnored(lOCoupled, !mbIsClosed);

            if (mOTileBlocker != null)
                mOTileBlocker.enabled = mbIsClosed;

            fWriteStateToData();
        }
    }

    /// <summary>
    /// THE DATA FOLLOWS THE LEAF (2026-10-05, found when goblins walked through a door the
    /// player had closed): the motion core reads the door from the level data - an open door's
    /// object is lifted 24 above the floor and is no obstacle, a closed one stands in the way -
    /// so every state change is written into the object at once (UWWorldCapture.CaptureDoor,
    /// which the save used to call alone). Before Start the state is the data's own.
    /// </summary>
    private void fWriteStateToData()
    {
        if (!didStart)
            return;

        UWEntityInfo lOInfo = GetComponentInParent<UWEntityInfo>();
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOInfo == null || lOInfo.ObjectData == null)
            return;

        UWDataImport.UWData.UWWorldCapture.CaptureDoor(lOLoader != null ? lOLoader.CurrentLevel : null, lOInfo.ObjectData,
            true, !mbIsClosed, false, 0, 0, null);
    }

    public void SetTileBlocker(BoxCollider pOBlocker)
    {
        mOTileBlocker = pOBlocker;

        if (pOBlocker != null && didStart)
            pOBlocker.enabled = mbIsClosed;
    }

    /// <summary>The frame - see IUsableDoor.AddCoupledCollider. Kept separate from
    /// mOColliders because it is not a child of the grate.</summary>
    private readonly System.Collections.Generic.List<Collider> mOCoupledColliders = new System.Collections.Generic.List<Collider>();

    public void AddCoupledCollider(Collider pOCollider)
    {
        if (pOCollider == null)
            return;

        mOCoupledColliders.Add(pOCollider);

        // Before Start, Start sets the state for all of them anyway; afterwards, right here.
        if (didStart)
            UWPlayerCollision.SetIgnored(pOCollider, !mbIsClosed);
    }

    private void Awake()
    {
        // The children too: the blocking box hangs below the grate.
        mOColliders = GetComponentsInChildren<Collider>(true);
    }

    public bool IsMoving
    {
        get { return mCurrentLift != 0f; }
    }

    public bool IsHeadingClosed
    {
        get { return IsMoving ? mbClosing : IsClosed; }
    }

    void Start()
    {
        if (StartsOpen)
        {
            transform.position += Vector3.up * LiftHeight;
            mfLiftNow = LiftHeight;
            IsClosed = false;
        }
        else
        {
            mfLiftNow = 0f;
            IsClosed = true;
        }

        mCurrentLift = 0f;
        mbClosing = false;
    }

    void Update()
    {
        if (mCurrentLift == 0f)
            return;

        float lfSpeed = Mathf.Max(1f, LiftHeight) / OpenDuration;
        float lfStep = (mCurrentLift > 0f ? -lfSpeed : lfSpeed) * Time.deltaTime;

        if (mCurrentLift > 0f)
        {
            mCurrentLift += lfStep;

            if (mCurrentLift < 0f)
            {
                lfStep += -mCurrentLift;
                mCurrentLift = 0f;
            }
        }
        else
        {
            mCurrentLift += lfStep;

            if (mCurrentLift > 0f)
            {
                lfStep -= -mCurrentLift;
                mCurrentLift = 0f;
            }
        }

        transform.Translate(0f, lfStep, 0f, Space.World);
        mfLiftNow = Mathf.Clamp(mfLiftNow + lfStep, 0f, LiftHeight);

        // COMING DOWN ONTO THE PLAYER: watched for the whole descent, the final frame
        // included; whoever stands in the tile sends the grate back up (see DoorMover).
        if (mbClosing && mfMotionStartLift - mfLiftNow >= LiftHeight * FirstStepFraction
            && UWPlayerCollision.OverlapsPlayer(mOTileBlocker))
        {
            mbClosing = false;
            mCurrentLift = -(LiftHeight - mfLiftNow);
        }

        if (mCurrentLift == 0f && mbClosing)
        {
            mbClosing = false;
            IsClosed = true;
        }
    }

    public void StartUsingDoor()
    {
        UWSoundEffects.PlayAt(UWSoundEffects.Portcullis, transform.position);

        mfMotionStartLift = mfLiftNow;

        if (IsMoving)
        {
            mbClosing = !mbClosing;
            mCurrentLift = mbClosing ? mfLiftNow : -(LiftHeight - mfLiftNow);

            fShutIfNothingToLower();

            return;
        }

        if (IsClosed)
        {
            // The collision goes at once, the grate follows.
            IsClosed = false;
            mbClosing = false;
            mCurrentLift = -(LiftHeight - mfLiftNow);
        }
        else
        {
            mbClosing = true;
            mCurrentLift = mfLiftNow;

            fShutIfNothingToLower();
        }
    }

    /// <summary>A closing with nothing left to lower is shut at once - see
    /// DoorMover.fShutIfNothingToSwing.</summary>
    private void fShutIfNothingToLower()
    {
        if (mbClosing && mCurrentLift == 0f)
        {
            mbClosing = false;
            IsClosed = true;
        }
    }
}
