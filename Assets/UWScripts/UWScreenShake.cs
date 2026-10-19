using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The shaking of the screen - in the original an additional angle on yaw, pitch and roll of the
/// player camera (the reference: motion_player.WalkOnSurfaceType, its section Seg35_B88, and
/// playerdatcamera.PositionPlayerCamera lines 161-166).
///
/// TWO CHANNELS that add up, counted in rounds by UWShakeRounds (since 2026-10-19 the original's
/// own rule, PLAYMOVE.C):
///
///   0x20  the small shake  strength = counter / 8, at most 3
///   0x40  the large shake  strength = counter / 10, at most 8
///
/// If only one of the two is running, the other stands at ONE instead of zero. The round in
/// which a counter runs out sees it wrapped to 255 - every shake ends with a strong jolt, and a
/// hit of level 0 is that jolt alone (per user on the original, 2026-10-19). Until then ours
/// divided both by 10 and stopped at 0, so a light hit showed nothing.
///
/// (0x80 exists only in uw2; SetScreenShake bails out before it for uw1.)
///
/// WHAT IT IS CALLED FOR IN uw1 - the five callers of SetScreenShake_seg034_2F89_B6A (checked
/// 2026-09-25; the "Mind Blast of class 13" listed here before does not exist, class 13 knows
/// only the bullfrog reset and hallucination) - and which of those we have:
///
///   Hit on the player         channel 0x20, duration damage level times five   WE HAVE IT
///                             (a blow or a missile only - since 2026-10-12, see ShakeOnDamage)
///   Drunkenness               channel 0x40, duration 10 + intoxication/6       WE HAVE IT
///   Tremor (spell)            channel 0x40, duration 0x28                      WE HAVE IT
///   The Ethereal Void         EtherealVoidSpecialEffects_seg008_150            WE HAVE IT (UWVoidEffects)
///   Earthquake trap           channel 0x40, duration 0x1E                      WE HAVE IT, no reachable instance in uw1
///
/// The shaker is therefore built as a separate component, not as part of combat: every
/// caller hooks in with one line.
/// </summary>
[DefaultExecutionOrder(UWUpdateOrder.Shake)]
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

    /// <summary>The two channels' counters and the original's round (UWShakeRounds).</summary>
    private readonly UWShakeRounds mORounds = new UWShakeRounds();

    /// <summary>The strength of the round now shown, 0 for none; held until the next tick.</summary>
    private int miRoundStrength;

    private float mfNextTick;

    /// <summary>The additional angle we added in the last frame - exactly this one
    /// is subtracted again. See LateUpdate.</summary>
    private Vector3 mOAppliedEuler;

    /// <summary>The angles we left on the camera: an axis that still holds them is ours to take
    /// back; one another script has set anew since (the terrain's roll, the look's pitch, each
    /// set absolutely) is clean already and stays (2026-10-19).</summary>
    private Vector3 mOWrittenEuler;

    private bool mbApplied;

    /// <summary>Is a shake currently running?</summary>
    public bool IsShaking
    {
        get { return mORounds.AnyRunning || miRoundStrength > 0; }
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
    /// Triggers the shake. THE NEW VALUE OVERWRITES THE CHANNEL'S COUNTER, as the original's
    /// set_effect does (UnderworldExhumed PLAYMOVE.C) - a later small hit shortens a running
    /// shake, and a duration of zero ends it. Until 2026-10-12 ours kept the larger of the two
    /// values, which the original does not.
    /// </summary>
    public void Shake(int piChannel, int piDuration)
    {
        if (piChannel < 0 || piChannel > LargeChannel)
            return;

        // set_effect: also 0 starts the channel - its last round is the jolt.
        mORounds.Start(piChannel == SmallChannel ? UWShakeRounds.CombatChannel : UWShakeRounds.TremorChannel,
            Mathf.Max(0, piDuration));
    }

    /// <summary>
    /// A blow or a missile hit the player (UnderworldExhumed COMBAT.C do_damage): the small
    /// channel for damage level times five - the level from the damage after the armour and
    /// before the Easy halving, at most three, see UWPlayerVitals.ShakeForCombatHit. Level zero
    /// sets zero and so ends a running combat shake, as set_effect(0x20, 0) does. Nothing else
    /// shakes the view on damage (since 2026-10-12, the user's decision; before that every
    /// damage did, poison, falls, lava and traps included).
    /// </summary>
    public static void ShakeOnDamage(int piDamageLevel)
    {
        UWScreenShake lOShake = Ensure();

        if (lOShake != null)
            lOShake.Shake(SmallChannel, Mathf.Max(0, piDamageLevel) * DamageDurationFactor);
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
            Vector3 lONow = transform.localEulerAngles;

            lONow.x = fTakeBack(lONow.x, mOWrittenEuler.x, mOAppliedEuler.x);
            lONow.y = fTakeBack(lONow.y, mOWrittenEuler.y, mOAppliedEuler.y);
            lONow.z = fTakeBack(lONow.z, mOWrittenEuler.z, mOAppliedEuler.z);
            transform.localEulerAngles = lONow;

            mOAppliedEuler = Vector3.zero;
            mbApplied = false;
        }

        fTick();

        int liStrength = miRoundStrength;

        if (liStrength <= 0)
            return;

        // ROLL always, PITCH only on request, YAW never - see RollAmplitude and
        // mbApplyPitch.
        mOAppliedEuler = new Vector3(
            mbApplyPitch ? fGetAngle(liStrength, PitchAmplitude) : 0f,
            0f,
            fGetAngle(liStrength, RollAmplitude));

        transform.localEulerAngles = transform.localEulerAngles + mOAppliedEuler;
        mOWrittenEuler = transform.localEulerAngles;

        mbApplied = true;
    }

    /// <summary>One round of the original per tick, regardless of whether the character is
    /// moving: its strength is shown until the next tick (UWShakeRounds).</summary>
    private void fTick()
    {
        if (mfTicksPerSecond <= 0f || Time.time < mfNextTick)
            return;

        mfNextTick = Time.time + (1f / mfTicksPerSecond);
        miRoundStrength = mORounds.Round();
    }

    /// <summary>Takes our offset back from an axis that still holds what we wrote; an axis
    /// another script has set anew keeps its value.</summary>
    private static float fTakeBack(float pfNow, float pfWritten, float pfOffset)
    {
        return Mathf.Abs(Mathf.DeltaAngle(pfNow, pfWritten)) < 0.0001f ? pfNow - pfOffset : pfNow;
    }

    /// <summary>One deflection in degrees: strength times a random value in the axis range,
    /// converted from original angle units.</summary>
    private static float fGetAngle(int piStrength, int piAmplitude)
    {
        int liUnits = piStrength * Random.Range(-piAmplitude, piAmplitude);

        return liUnits * 360f / UWWorldScale.FullCircleUnits;
    }
}
