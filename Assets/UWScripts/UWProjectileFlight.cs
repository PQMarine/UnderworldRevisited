using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// ONE FLYING OBJECT - a thrown thing, an arrow, a spell missile, a trap's bolt, the quake's
/// boulder (stage 1 of the motion rework, 2026-10-05, in place of UWSpellProjectile). Its motion
/// is the original's: a mobile record (UWMobileRecord) stepped by the engine-free core every
/// clock unit through UWMotionHost. This component shows it - the picture glides from the
/// last step's position to the new one over the step's interval, as a creature's does - and
/// carries the flight's data for its hits: the damage and its type, the impact picture, who
/// launched it.
///
/// What a hit does in the game - the damage of a missile, the strike on the player, the
/// smashing of Tybal's orb, the impact picture, the detonation's blast - and the player walking
/// into his own missile went to UWMissiles in cleanup step 3.5 (2026-10-17), the hit maths to
/// UWMissileHitRules; the touch test still runs from Update, after the picture moved.
/// </summary>
public class UWProjectileFlight : MonoBehaviour
{
    public UWMobileRecord Record { get; private set; }

    /// <summary>The missile's damage before its dice - the base the hit rolls
    /// (UWAttackResolution.Missile, missile_thwack -> do_damage; since cleanup step 4.9b,
    /// 2026-10-18, the rolled damage until then) or UWAttackResolution.NoMissileDamage - and its
    /// type.</summary>
    public int BaseDamage { get; private set; }

    public int DamageType { get; private set; }

    /// <summary>What a hit or the detonation shows, or -1 (UWRunicMagic.GetProjectileImpactId).</summary>
    public int ImpactId { get; private set; }

    public Interaction Interaction { get; private set; }

    /// <summary>The creature that launched it, or null for the player's and the traps'.</summary>
    public UWCritter Launcher { get; private set; }

    /// <summary>Launched by the player: his impact sound, his hits reported to the eyes, and
    /// the touch test for walking into it.</summary>
    public bool PlayersOwn { get; private set; }

    /// <summary>How far the picture has moved since the launch, in world units (the touch test
    /// waits for its first stretch, UWMissiles.UpdateSelfBump).</summary>
    public float Travelled { get; private set; }

    /// <summary>The player touches it: whoever stays in contact takes damage once (set by the
    /// strike on the player and by the touch test).</summary>
    public bool TouchingPlayer { get; set; }

    private UWMissiles mOMissiles;

    private Vector3 mOFrom;

    private Vector3 mOTo;

    private float mfSince;

    private float mfInterval;

    private int miShownHeading = -1;

    private bool mbIsModel;

    private bool mbEnded;

    public void Begin(UWMissiles pOMissiles, UWMobileRecord pORecord, int piBaseDamage, int piDamageType, int piImpactId,
        Interaction pOInteraction, UWCritter pOLauncher, bool pbPlayersOwn)
    {
        mOMissiles = pOMissiles;
        Record = pORecord;
        BaseDamage = piBaseDamage;
        DamageType = piDamageType;
        ImpactId = piImpactId;
        Interaction = pOInteraction;
        Launcher = pOLauncher;
        PlayersOwn = pbPlayersOwn;

        mOFrom = transform.position;
        mOTo = mOFrom;
        mfInterval = fStepSeconds(false);

        UWEntityInfo lOInfo = GetComponent<UWEntityInfo>();

        mbIsModel = lOInfo != null && lOInfo.IsModel;

        // A flying thing has no body of its own: the core tests its path itself.
        foreach (Collider lOCollider in GetComponentsInChildren<Collider>())
            lOCollider.enabled = false;

        fFace();
    }

    /// <summary>The record was stepped: the picture glides to the new position over the step's
    /// interval (the period in clock units of 62.5 ms, twice that under Speed).</summary>
    public void OnStepped(bool pbPlayerHasSpeed)
    {
        mOFrom = transform.position;
        mOTo = UWMotionHost.WorldPosition(Record);
        mfSince = 0f;
        mfInterval = fStepSeconds(pbPlayerHasSpeed);
        Travelled += Vector3.Distance(mOFrom, mOTo);

        fFace();
    }

    private float fStepSeconds(bool pbPlayerHasSpeed)
    {
        float lfSeconds = (Record.Period & 7) * UWCritterRules.SlotPitTicks / UWCritter.PitTicksPerSecond;

        return pbPlayerHasSpeed ? lfSeconds * 2f : lfSeconds;
    }

    /// <summary>An arrow's model points along its heading; a picture faces the camera by itself.</summary>
    private void fFace()
    {
        if (!mbIsModel || Record.HeadingByte == miShownHeading)
            return;

        miShownHeading = Record.HeadingByte;
        transform.rotation = Quaternion.LookRotation(UWViewpoint.HeadingByteToDirection(Record.HeadingByte));
    }

    private void Update()
    {
        if (mbEnded)
            return;

        mfSince += Time.deltaTime;

        float lfT = mfInterval > 0f ? Mathf.Clamp01(mfSince / mfInterval) : 1f;

        transform.position = Vector3.Lerp(mOFrom, mOTo, lfT);

        if (mOMissiles != null)
            mOMissiles.UpdateSelfBump(this);
    }

    /// <summary>The flight is over: the picture goes.</summary>
    public void End()
    {
        mbEnded = true;
        Destroy(gameObject);
    }
}
