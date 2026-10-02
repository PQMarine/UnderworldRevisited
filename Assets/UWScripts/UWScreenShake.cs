using UnityEngine;

/// <summary>
/// The shaking of the screen - in the original an additional angle on yaw, pitch and roll of the
/// player camera (the reference: motion_player.WalkOnSurfaceType, its section Seg35_B88, and
/// playerdatcamera.PositionPlayerCamera lines 161-166).
///
/// TWO CHANNELS that add up. They are kept in separate counters, and each channel has its
/// own upper limit:
///
///   0x20  the small shake  strength = duration / 10, at most 3
///   0x40  the large shake  strength = duration / 10, at most 8
///
/// If only one of the two is running, the other stands at ONE instead of zero - the reference
/// sets both counter values to 1 at the start of its routine and overwrites only the channel
/// that is actually running. So a shake is never really weak.
///
/// (0x80 exists only in uw2; SetScreenShake bails out before it for uw1.)
///
/// WHAT IT IS CALLED FOR IN uw1 - the five callers of SetScreenShake_seg034_2F89_B6A (checked
/// 2026-09-25; the "Mind Blast of class 13" listed here before does not exist, class 13 knows
/// only the bullfrog reset and hallucination) - and which of those we have:
///
///   Hit on the player         channel 0x20, duration damage level times five   WE HAVE IT
///   Drunkenness               channel 0x40, duration 10 + intoxication/6       WE HAVE IT
///   Tremor (spell)            channel 0x40, duration 0x28                      WE HAVE IT
///   The Ethereal Void         EtherealVoidSpecialEffects_seg008_150            WE HAVE IT (UWVoidEffects)
///   Earthquake trap           channel 0x40, duration 0x1E                      WE HAVE IT, no reachable instance in uw1
///
/// The shaker is therefore built as a separate component, not as part of combat: every
/// caller hooks in with one line.
/// </summary>
public sealed class UWScreenShake : MonoBehaviour
{
    /// <summary>The small channel - in the original bit 0x20 of the tile state.</summary>
    public const int SmallChannel = 0;

    /// <summary>The large channel - in the original bit 0x40.</summary>
    public const int LargeChannel = 1;

    /// <summary>
    /// How often the counter goes down. Once per tick each running channel counts down by one,
    /// and the duration is given in ticks - so the tick alone decides how long
    /// a shake lasts in seconds. It has no influence on the STRENGTH, which is computed
    /// from the counter value.
    ///
    /// TWENTY PER SECOND, MEASURED AGAINST THE ORIGINAL: a melee hit shakes very briefly there,
    /// about a quarter of a second (per user, 2026-09-09). A hit of damage level one
    /// has duration five, which at twenty ticks makes exactly these 0.25 seconds.
    ///
    /// The reference ticks its main loop at 0.097659 seconds, i.e. 10.24 times per second -
    /// that would make it half a second, twice as long. But its own source code describes
    /// this tick as an estimate from breakpoints, and the observation wins.
    /// </summary>
    [SerializeField]
    [Tooltip("Ticks per second at which the shake counts down. 20 gives a quarter second for a light hit - measured against the original.")]
    private float mfTicksPerSecond = 20f;

    /// <summary>
    /// The three amplitudes per strength level, in original angle units - as given in the
    /// reference:
    ///
    ///   Yaw    strength * (random &amp; 0xFF  - 128)   i.e. +-128
    ///   Pitch  strength * (random &amp; 0x7F  -  64)   i.e. +-64
    ///   Roll   strength * (random &amp; 0x1FF - 256)   i.e. +-256
    ///
    /// A full circle is 65536 units (see UWViewpoint), so one strength level gives
    /// at most 0.70 degrees yaw, 0.35 degrees pitch and 1.41 degrees roll. At the highest
    /// strength - both channels full, 11 together - that is 7.7, 3.9 and 15.5 degrees.
    ///
    /// ROLL IS DRAWN, and YAW CERTAINLY NOT: the user checked with the fireball
    /// of an elemental that the view direction does not change
    /// (2026-09-09). That rules out precisely the axis the walking direction
    /// depends on.
    ///
    /// PITCH IS STILL OPEN. For an ordinary hit it would be 0.7 degrees - that
    /// is not visible. On the large channel drunkenness stays below strength three; only Tremor
    /// or Mind Blast go higher, and pitch has not been checked against the original with those
    /// yet. That is why pitch is a switch rather than a decision - see mbApplyPitch.
    /// </summary>
    private const int YawAmplitude = 128;

    private const int PitchAmplitude = 64;

    private const int RollAmplitude = 256;

    /// <summary>
    /// Whether the screen also PITCHES while shaking. Off, as it is unresolved so far - see the
    /// amplitudes above. Testing needs a strong shake: the large channel with
    /// a duration of thirty or more, which together with the idle small channel gives strength
    /// four or more, i.e. well over one degree of pitch (Tremor, 0x28, gives five).
    /// </summary>
    [SerializeField]
    [Tooltip("Does the screen pitch while shaking? Not confirmed in the original - turn on for testing.")]
    private bool mbApplyPitch;

    /// <summary>Upper limits of the two channels - see class comment.</summary>
    private const int SmallMaximum = 3;

    private const int LargeMaximum = 8;

    /// <summary>The value a channel that is NOT running stands at.</summary>
    private const int IdleStrength = 1;

    private readonly int[] miDuration = new int[2];

    private float mfNextTick;

    /// <summary>The additional angle we added in the last frame - exactly this one
    /// is subtracted again. See LateUpdate.</summary>
    private Vector3 mOAppliedEuler;

    private bool mbApplied;

    /// <summary>Is a shake currently running?</summary>
    public bool IsShaking
    {
        get { return miDuration[SmallChannel] > 0 || miDuration[LargeChannel] > 0; }
    }

    /// <summary>Finds the shaker on the player camera or attaches one.</summary>
    public static UWScreenShake Ensure()
    {
        UWScreenShake lOFound = FindAnyObjectByType<UWScreenShake>();

        if (lOFound != null)
            return lOFound;

        Camera lOCamera = Camera.main;

        return lOCamera != null ? lOCamera.gameObject.AddComponent<UWScreenShake>() : null;
    }

    /// <summary>
    /// Triggers the shake. A running channel is NOT extended but set to the
    /// larger of the two values - otherwise many small hits could stack
    /// an endless shake, and the reference likewise only writes the counter.
    /// </summary>
    public void Shake(int piChannel, int piDuration)
    {
        if (piChannel < 0 || piChannel > LargeChannel || piDuration <= 0)
            return;

        if (piDuration > miDuration[piChannel])
            miDuration[piChannel] = piDuration;
    }

    /// <summary>Convenience path for combat: shake by damage level, channel and duration as
    /// in the reference (combat.cs line 651).</summary>
    public static void ShakeOnDamage(int piDamageLevel)
    {
        if (piDamageLevel <= 0)
            return;

        UWScreenShake lOShake = Ensure();

        if (lOShake != null)
            lOShake.Shake(SmallChannel, piDamageLevel * DamageDurationFactor);
    }

    /// <summary>From the reference: duration equals damage level times five.</summary>
    private const int DamageDurationFactor = 5;

    /// <summary>
    /// LATE, so the additional angle is not overwritten again in the same frame: the
    /// camera's pitch angle is set by UWPlayerLook in Update.
    ///
    /// EXACTLY WHAT WE ADDED IS TAKEN BACK - component by component on the
    /// Euler angles, at the start of the next frame.
    ///
    /// A FIRST ATTEMPT INSTEAD REMEMBERED THE WHOLE ROTATION FROM BEFORE and restored
    /// it, but only if nobody else had touched it since. That went wrong:
    /// UWPlayerTerrain reads the camera rotation, changes ONLY the roll angle and writes it
    /// back (in its LateUpdate). To the comparison that looked like a foreign writer,
    /// the restore was skipped - and the shake angle stayed, frame by
    /// frame a bit more. It became visible in the yaw: the walking direction drifted
    /// permanently (per user, 2026-09-09).
    ///
    /// Subtracting component by component is independent of that, because here each writer only
    /// touches ITS axis - UWPlayerLook the pitch, UWPlayerTerrain the roll, the yaw sits
    /// on the body anyway. And because the amplitudes are small, the arithmetic on the
    /// Euler angles is clean here.
    /// </summary>
    private void LateUpdate()
    {
        if (mbApplied)
        {
            transform.localEulerAngles = transform.localEulerAngles - mOAppliedEuler;

            mOAppliedEuler = Vector3.zero;
            mbApplied = false;
        }

        fTick();

        if (!IsShaking)
            return;

        int liStrength = fGetStrength();

        if (liStrength <= 0)
            return;

        // ROLL always, PITCH only on request, YAW never - see RollAmplitude and
        // mbApplyPitch.
        mOAppliedEuler = new Vector3(
            mbApplyPitch ? fGetAngle(liStrength, PitchAmplitude) : 0f,
            0f,
            fGetAngle(liStrength, RollAmplitude));

        transform.localEulerAngles = transform.localEulerAngles + mOAppliedEuler;

        mbApplied = true;
    }

    /// <summary>Counts the running channels down - once per tick, regardless of whether
    /// the character is moving.</summary>
    private void fTick()
    {
        if (mfTicksPerSecond <= 0f || Time.time < mfNextTick)
            return;

        mfNextTick = Time.time + (1f / mfTicksPerSecond);

        for (int liAt = 0; liAt < miDuration.Length; liAt++)
        {
            if (miDuration[liAt] > 0)
                miDuration[liAt]--;
        }
    }

    /// <summary>The strength from both channels. An idle channel contributes one, not
    /// zero - see class comment.</summary>
    private int fGetStrength()
    {
        int liLarge = miDuration[LargeChannel] > 0
            ? Mathf.Min(miDuration[LargeChannel] / 10, LargeMaximum) : IdleStrength;

        int liSmall = miDuration[SmallChannel] > 0
            ? Mathf.Min(miDuration[SmallChannel] / 10, SmallMaximum) : IdleStrength;

        return liLarge + liSmall;
    }

    /// <summary>One deflection in degrees: strength times a random value in the axis range,
    /// converted from original angle units.</summary>
    private static float fGetAngle(int piStrength, int piAmplitude)
    {
        int liUnits = piStrength * Random.Range(-piAmplitude, piAmplitude);

        return liUnits * 360f / UWViewpoint.FullCircleUnits;
    }
}
