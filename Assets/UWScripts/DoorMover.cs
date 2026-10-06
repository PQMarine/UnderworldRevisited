using UnityEngine;
using System.Collections;

public class DoorMover : MonoBehaviour, IUsableDoor
{
    public Vector3 RotationPoint { get; set; }
    public bool DoorDirection { get; set; }

    /// <summary>The angle by which a door swings open.</summary>
    public const float OpenAngle = 110f;

    /// <summary>Is this door open from the start? The game data has separate
    /// objects for that: 328-333 are open doors (see UWLevel), and without this flag
    /// they were closed anyway (per user, 2026-09-03). Must be set before Start.
    /// </summary>
    public bool StartsOpen { get; set; }

    /// <summary>Degrees per second of the swing.</summary>
    private const float SwingSpeed = 75f;

    /// <summary>The remaining rotation of the current motion, signed - see Start for the sign.</summary>
    private float mCurrentRotation;

    /// <summary>How far the leaf stands open right now, 0 closed to OpenAngle open. The
    /// motions are counted from here, so a reversal in mid-swing lands exactly at either end.
    /// </summary>
    private float mfOpenNow;

    /// <summary>The current motion closes the door. Only then is the tile watched for the
    /// player, and only at its end does the collision come back.</summary>
    private bool mbClosing;

    // A DOOR DOES NOT CLOSE ONTO THE PLAYER. Watched in the original (per user, 2026-09-19):
    // while the door is closing, anyone in its tile - on either side of the leaf, and also
    // someone who walks in during the swing - makes it spring back open, and the collision
    // comes back only with the door COMPLETELY shut. So the tile is watched for the whole
    // closing motion INCLUDING the final frame, and the leaf swings back from wherever it is.
    // A margin of ten degrees without the check stood here for an hour: with it the box could
    // switch on around a player still walking through, who then hung in the door (per user).

    /// <summary>... but not before the first step: someone already standing in the tile
    /// when the door starts to close sees it move about this much and then spring back (per
    /// user on the original, 2026-09-19: "the ten degrees are when you are already in the
    /// door while it closes"). The original's door animation runs in frames; this is one.</summary>
    private const float FirstStep = 10f;

    /// <summary>How far open the leaf stood when the current motion began - the first step
    /// is measured from here.</summary>
    private float mfMotionStartOpen;

    private BoxCollider mOTileBlocker;

    private Collider mOCollider;
    private bool mbIsClosed;

    /// <summary>Original (CONFIRMED per user, 2026-08-28): an OPEN door has no
    /// player collision, you simply walk through - but you must still be able to touch it
    /// (interaction ray) to close it again. Hence a property with a body instead of an
    /// auto-property: every state change should update the collision automatically, see
    /// UWPlayerCollision.
    ///
    /// WHEN THE COLLISION CHANGES (per user on the original, 2026-09-19): opening a closed
    /// door takes the collision away AT ONCE, with the first frame of the swing; closing
    /// brings it back only when the door has shut. In between the doorway is open.</summary>
    public bool IsClosed
    {
        get { return mbIsClosed; }
        set
        {
            mbIsClosed = value;
            UWPlayerCollision.SetIgnored(mOCollider, !mbIsClosed);

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

    /// <summary>The frame - see IUsableDoor.AddCoupledCollider.</summary>
    private readonly System.Collections.Generic.List<Collider> mOCoupledColliders = new System.Collections.Generic.List<Collider>();

    public void AddCoupledCollider(Collider pOCollider)
    {
        if (pOCollider == null)
            return;

        mOCoupledColliders.Add(pOCollider);

        // Before Start, Start sets the state for all of them anyway; afterwards right here.
        if (didStart)
            UWPlayerCollision.SetIgnored(pOCollider, !mbIsClosed);
    }

    private void Awake()
    {
        // The MeshCollider is added BEFORE this component on creation (see
        // UWObjectSpawner.fSpawnDoor/fSpawnSecretDoor), so it is already available here.
        mOCollider = GetComponent<Collider>();
    }

    public bool IsMoving
    {
        get { return mCurrentRotation != 0; }
    }

    public bool IsHeadingClosed
    {
        get { return IsMoving ? mbClosing : IsClosed; }
    }

    /// <summary>The sign of a rotation that OPENS the door - the remaining amount of an
    /// opening motion carries it, a closing motion the opposite.</summary>
    private float fOpeningSign()
    {
        return DoorDirection ? -1f : 1f;
    }

    // Use this for initialization
    void Start ()
    {
        // A door that is open from the start is not moved, but placed directly in its
        // end position - in the same direction in which it would otherwise swing open.
        //
        // SIGN: mCurrentRotation is a REMAINING AMOUNT, and Update rotates by the
        // opposite of it (with a positive remainder at -75 degrees per second). The end position is
        // therefore the negated value from StartUsingDoor - the other way round the door stands in the
        // wall instead of in the passage (per user, 2026-09-03).
        if (StartsOpen)
        {
            transform.RotateAround(RotationPoint, Vector3.up, DoorDirection ? OpenAngle : -OpenAngle);
            mfOpenNow = OpenAngle;
            IsClosed = false;
        }
        else
        {
            mfOpenNow = 0f;
            IsClosed = true;
        }

        mCurrentRotation = 0f;
        mbClosing = false;
    }

	// Update is called once per frame
	void Update ()
    {
	    if(mCurrentRotation != 0f)
        {
            float lRotation = (mCurrentRotation > 0 ? -SwingSpeed : SwingSpeed) * Time.deltaTime;

            if (mCurrentRotation > 0f)
            {
                mCurrentRotation += lRotation;

                if (mCurrentRotation < 0f)
                {
                    lRotation += mCurrentRotation * -1f;
                    mCurrentRotation = 0f;
                }
            }
            else
            {
                mCurrentRotation += lRotation;

                if (mCurrentRotation > 0f)
                {
                    lRotation -= mCurrentRotation * -1f;
                    mCurrentRotation = 0f;
                }
            }

            transform.RotateAround(RotationPoint, Vector3.up, lRotation);

            // The applied rotation is the opposite of the remaining sign, so an opening
            // motion adds -lRotation times the opening sign.
            mfOpenNow = Mathf.Clamp(mfOpenNow - (lRotation * fOpeningSign()), 0f, OpenAngle);

            // CLOSING ONTO THE PLAYER: watched for the whole swing, the final frame included;
            // whoever stands in the tile sends the leaf back to fully open. Only then, below,
            // does the door shut and the collision return.
            if (mbClosing && mfMotionStartOpen - mfOpenNow >= FirstStep
                && UWPlayerCollision.OverlapsPlayer(mOTileBlocker))
            {
                mbClosing = false;
                mCurrentRotation = fOpeningSign() * (OpenAngle - mfOpenNow);
            }

            if (mCurrentRotation == 0f && mbClosing)
            {
                mbClosing = false;
                IsClosed = true;
            }
        }
	}

    public void StartUsingDoor()
    {
        UWSoundEffects.PlayAt(UWSoundEffects.Door, transform.position);

        mfMotionStartOpen = mfOpenNow;

        if (IsMoving)
        {
            // Reversed in mid-swing: the new motion runs from where the leaf is to the other
            // end. Turning an opening into a closing keeps the doorway open until it shuts.
            mbClosing = !mbClosing;

            mCurrentRotation = mbClosing
                ? -fOpeningSign() * mfOpenNow
                : fOpeningSign() * (OpenAngle - mfOpenNow);

            fShutIfNothingToSwing();

            return;
        }

        if (IsClosed)
        {
            // The collision goes at once, the leaf follows.
            IsClosed = false;
            mbClosing = false;
            mCurrentRotation = fOpeningSign() * (OpenAngle - mfOpenNow);
        }
        else
        {
            mbClosing = true;
            mCurrentRotation = -fOpeningSign() * mfOpenNow;

            fShutIfNothingToSwing();
        }
    }

    /// <summary>
    /// A closing with nothing left to swing is shut at once. Update only finishes a motion
    /// that still has a remainder, so a door turned back before its first frame (the click
    /// opens it, the talking door's conversation closes it again in the same frame) kept
    /// "closing" with no motion: shut to the eye, open to every check, and a later open was
    /// ignored (per user, 2026-10-01).
    /// </summary>
    private void fShutIfNothingToSwing()
    {
        if (mbClosing && mCurrentRotation == 0f)
        {
            mbClosing = false;
            IsClosed = true;
        }
    }
}
