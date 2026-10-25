using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE DISTANCE ACTUALLY TRAVELLED and what hangs on it: the footsteps, the momentum the noise
/// reads, and the measured speed of the head bob and the weapon's jitter. Out of UWPlayerMovement
/// in cleanup step 4.7 (2026-10-18), a plain part the movement owns and drives - the footsteps at
/// the end of its Update, the speed in its LateUpdate - so both see the frame's move. The step's
/// formula is UWFootstepRules' (engine-free).
/// </summary>
public sealed class UWPlayerFootsteps
{
    /// <summary>
    /// How fast the character REALLY moved over the ground in the last frame - measured from
    /// the position, not the wanted velocity, so that walking into a wall counts as standing.
    /// For the head bob and the weapon's jitter (UWHeadBobRules). The original uses its motion
    /// speed there, which it does not clear against a north or east wall - a slip of its own we
    /// do not copy (per user, 2026-09-23).
    /// </summary>
    public float ActualHorizontalSpeed { get; private set; }

    /// <summary>
    /// ActualHorizontalSpeed smoothed over about a tenth of a second - what the head bob and the
    /// weapon read. Measured from frame to frame the speed wobbles with the physics steps, and
    /// the bob's factor wobbled with it; the user saw the view grow shakier than with the
    /// wanted velocity (2026-09-23). The original's motion speed changes smoothly.
    /// </summary>
    public float SmoothedHorizontalSpeed { get; private set; }

    /// <summary>The time constant of that smoothing, in seconds.</summary>
    private const float SpeedSmoothingSeconds = 0.1f;

    private Vector3 mOLastFramePosition;

    private bool mbHasLastFramePosition;

    /// <summary>Measures ActualHorizontalSpeed after the frame's move. A jump in position
    /// (teleport, level change) reads as one fast frame; the readers clamp it.</summary>
    public void MeasureSpeed(Vector3 pOPosition, float pfNormalForwardSpeed)
    {
        Vector3 lOPosition = pOPosition;

        if (mbHasLastFramePosition && Time.deltaTime > 0f)
        {
            Vector3 lOMoved = lOPosition - mOLastFramePosition;
            lOMoved.y = 0f;

            ActualHorizontalSpeed = lOMoved.magnitude / Time.deltaTime;
        }
        else if (Time.deltaTime > 0f)
            ActualHorizontalSpeed = 0f;

        mOLastFramePosition = lOPosition;
        mbHasLastFramePosition = true;

        if (Time.deltaTime > 0f)
        {
            // A jump in position (teleport, level change) must not kick the smoothed value.
            float lfRaw = Mathf.Min(ActualHorizontalSpeed, pfNormalForwardSpeed * 2f);

            SmoothedHorizontalSpeed = Mathf.Lerp(SmoothedHorizontalSpeed, lfRaw,
                1f - Mathf.Exp(-Time.deltaTime / SpeedSmoothingSeconds));
        }
    }

    /// <summary>When the next step sounds.</summary>
    private float mfNextStepTime;

    private bool mbStepToggle;

    /// <summary>
    /// How much of the full forward speed is actually being made, 0 to 1 - the original's
    /// Player_MotionArray_unk_14 over its cap, which is PlayerActualForwardSpeed.
    /// Measured from the distance covered, exactly like the footsteps beside it, so turning
    /// on the spot counts as standing. The noise reads it (UWPlayerVitals.TickQuietness);
    /// it is kept up to date even in water and in the air, where the footsteps stay silent.
    /// </summary>
    public float MomentumFraction { get; private set; }

    /// <summary>The original's PIT counter runs at 256 Hz (reference: main.cs; UWPlayerTick is
    /// the home).</summary>
    private const float PitTicksPerSecond = UWPlayerTick.PitTicksPerSecond;

    /// <summary>Where the character stood in the previous frame - for the distance actually
    /// travelled.</summary>
    private Vector3 mOLastStepPosition;

    /// <summary>Without a world: no momentum, and the next frame measures from here.</summary>
    public void Stand(Vector3 pOPosition)
    {
        MomentumFraction = 0f;
        mOLastStepPosition = pOPosition;
    }

    /// <summary>
    /// Footsteps alternating between the two sounds (UWFootstepRules). Nothing in water or in the
    /// air: pbOnFoot is the movement's word that the body walks on the ground of a world.
    /// pfFullSpeed is the faster of the two schemes' forward speeds, in world units a second.
    /// </summary>
    public void Step(Vector3 pOPosition, float pfFullSpeed, bool pbOnFoot)
    {
        Vector3 lOPosition = pOPosition;
        Vector3 lODelta = lOPosition - mOLastStepPosition;
        mOLastStepPosition = lOPosition;

        float lfFullSpeed = pfFullSpeed
            / UWWorldScale.TileSize;
        float lfSpeed = Time.deltaTime > 0f
            ? new Vector3(lODelta.x, 0f, lODelta.z).magnitude / Time.deltaTime
                / UWWorldScale.TileSize
            : 0f;

        MomentumFraction = lfFullSpeed > 0f ? Mathf.Clamp01(lfSpeed / lfFullSpeed) : 0f;

        // No step without a world (the main menu at the start), and none for a jump of a tile
        // or more in one frame - the player being placed, not walking (per user, 2026-10-08:
        // a few steps were heard at the start).
        bool lbPlaced = new Vector3(lODelta.x, 0f, lODelta.z).magnitude >= UWWorldScale.TileSize;

        if (!pbOnFoot || Time.deltaTime <= 0f || lbPlaced)
        {
            mfNextStepTime = Time.time + 0.2f;
            return;
        }

        if (lfSpeed < UWFootstepRules.StepSpeedThreshold)
        {
            mfNextStepTime = Time.time + 0.2f;
            return;
        }

        if (Time.time < mfNextStepTime)
            return;

        int liMomentum = UWFootstepRules.Momentum(lfSpeed, lfFullSpeed);

        UWAudioService.PlayEffect(mbStepToggle ? UWSoundEffectId.FootstepA : UWSoundEffectId.FootstepB,
            UWFootstepRules.NoteOffset(liMomentum));
        mbStepToggle = !mbStepToggle;

        int liTicks = UWFootstepRules.IntervalTicks(liMomentum);

        mfNextStepTime = Time.time + (liTicks / PitTicksPerSecond);
    }
}
