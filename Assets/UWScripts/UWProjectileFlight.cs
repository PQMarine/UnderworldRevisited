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
/// into a missile went to UWMissiles in cleanup step 3.5 (2026-10-17), the hit maths to
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

    /// <summary>The player's own - his impact sound, his hits reported to the eyes: the record's
    /// owner byte (last_hit) is his index, 1. ONE RULE BY THE OWNER BYTE since 2026-10-19 (the
    /// user's decision "as the original"): a trap's missile is nobody's, its owner 0 (missile_fire,
    /// UnderworldExhumed combat/MISSILE.C:245-300); until then a flight launched with the
    /// Interaction counted as the player's, and the traps passed it.</summary>
    public bool PlayersOwn => Record != null && UWMissileHitRules.IsPlayersOwn(Record.Owner);

    /// <summary>Read from the level data - a save, a level entered again (UWMissiles.ResumeFlights,
    /// 2026-10-19): nothing of its launch is known but the record, so its hit takes the base, the
    /// type and the attacker from the record's item and owner byte, as missile_newhit does
    /// (UnderworldExhumed motion/OBJPHYS.C:102-132).</summary>
    public bool FromLevelData { get; private set; }

    /// <summary>The player touched it at the launch: that contact does not strike until he is out
    /// of reach (UWMissiles.UpdatePlayerTouch) - the first stretch of flight the test waited for
    /// until 2026-10-19 never came while Freeze Time held the missile.</summary>
    public bool TouchingPlayer { get; set; }

    /// <summary>The game clock (PIT ticks) of this missile's last hit on the player, -1 for none:
    /// a walk-in strikes again only after UWMissileHitRules.WalkInHitIntervalTicks (2026-10-19).</summary>
    public long LastPlayerHitTick { get; set; } = -1;

    private UWMissiles mOMissiles;

    private Vector3 mOFrom;

    private Vector3 mOTo;

    private float mfSince;

    private float mfInterval;

    private int miShownHeading = -1;

    private bool mbIsModel;

    private bool mbEnded;

    public void Begin(UWMissiles pOMissiles, UWMobileRecord pORecord, int piBaseDamage, int piDamageType, int piImpactId,
        Interaction pOInteraction, UWCritter pOLauncher, bool pbFromLevelData)
    {
        mOMissiles = pOMissiles;
        Record = pORecord;
        BaseDamage = piBaseDamage;
        DamageType = piDamageType;
        ImpactId = piImpactId;
        Interaction = pOInteraction;
        Launcher = pOLauncher;
        FromLevelData = pbFromLevelData;

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

        fFace();
    }

    private float fStepSeconds(bool pbPlayerHasSpeed)
    {
        float lfSeconds = (Record.Period & 7) * UWCritterRules.SlotPitTicks / UWCritter.PitTicksPerSecond;

        return pbPlayerHasSpeed ? lfSeconds * 2f : lfSeconds;
    }

    /// <summary>An arrow's model points along its heading - the record's heading byte, also for a
    /// flight read from the level data and while Freeze Time holds it (2026-10-19; such a record
    /// was a lying arrow in its static octant until then); a picture faces the camera by itself.</summary>
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
            mOMissiles.UpdatePlayerTouch(this);
    }

    /// <summary>The flight is over: the picture goes.</summary>
    public void End()
    {
        mbEnded = true;
        Destroy(gameObject);
    }
}
