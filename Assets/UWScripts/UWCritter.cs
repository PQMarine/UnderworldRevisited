using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;
using UnderworldRevisited;
using UnderworldRevisited.Build;

/// <summary>
/// The Unity side of one creature (rebuilt 2026-09-20 around the brain; the creature's logic
/// moved out in cleanup step 4.11, 2026-10-18): the body, its picture and the registry the driver
/// walks, around the engine-free UWCreatureActor.
///
/// NOTHING HERE DECIDES. The brain (Assets/UWDataImport/UWData/UWCritterBrain.cs) runs
/// NPCInitialProcessing_seg007_2488 of UW.EXE on the creature's record (UWCritterRecord, the
/// 27-byte mobile object record of its UWNpc) and returns a Motion: heading, speed, facing,
/// animation slot and frame, the pitch of a flier and when it is next due on the level's
/// 16-slot clock (UWScene.CritterClock). The actor (UWCreatureActor) is the ICritterHost the brain
/// asks about the world and raises its events to, and makes the creature's decisions around the
/// mind; what it cannot answer from the data it asks the level's UWCreatureWorld. This class
///
///   - is the actor's ICreatureView: it moves the body to the record's spot and interpolates the
///     picture between two due updates so a walking creature does not jump every quarter second
///     (Docs/AI/brain-and-host.md section 3), shows the Motion on the animator, plays the sounds
///     and effects at the body, commits the death on its UWDamageable and keeps the trace;
///   - keeps the registry UWCritterDriver walks once per frame (Active) and the index lookup;
///   - keeps the public members the rest of the port reads (State, FacingDegrees, Goal,
///     Attitude, ...) as thin adapters over the actor and its record.
///
/// The contract between the two halves is Docs/AI/brain-and-host.md; the rules behind the
/// brain are Docs/AI/creature-ai.md. Everything the brain owns in the record (section 6
/// of the contract) is never written here or in the actor, with the two exceptions the contract
/// names: the position after a step and the goal a conversation or spell script sets.
/// </summary>
[RequireComponent(typeof(UWDamageable))]
public class UWCritter : MonoBehaviour, ICreatureView, IUWAttackDefender
{
    /// <summary>What the animation slot of the brain's Motion amounts to, for the callers
    /// that still ask (UWDebugOverlay). Kept as an adapter over the record: Pursuing and
    /// Wandering are the walking animation 0x2C in and out of goal 5, Attacking a swing, a
    /// shot or a spell, Ready the combat idle 0, Idle everything else.</summary>
    public enum StateEnum
    {
        Idle,
        Pursuing,
        Attacking,
        Ready,
        Wandering
    }

    /// <summary>Which attack picture is running - the animator used to pick its slot by it;
    /// now it reads the Motion's slot directly and this only serves the F1 overlay.</summary>
    public enum AttackKindEnum
    {
        Melee,
        Ranged,
        Magic
    }

    // ------------------------------------------------- Constants

    /// <summary>Attitude 0 of the object data.</summary>
    private const int HostileAttitude = UWNpc.AttitudeHostile;

    public const int GoalAttack = UWNpc.GoalAttack;

    /// <summary>Goal 9, holding its ground - public because UWCritterAnimator used to show
    /// the combat stance for it; the brain now writes animation 0 for it itself.</summary>
    public const int GoalDistanceAttack = 9;

    /// <summary>The player's object index and item id as the brain names them.</summary>
    private const int PlayerIndex = UWCritterBrain.PlayerIndex;

    /// <summary>Fallback height in case a creature has no collider from which its centre could
    /// be read.</summary>
    private const float FallbackEyeHeight = 20f;

    /// <summary>The original's timer ticks per second (spec 1.1; UWPlayerTick is the home).</summary>
    public const float PitTicksPerSecond = UWPlayerTick.PitTicksPerSecond;

    /// <summary>The splash of a drowning creature: the class-7 object with offset 6
    /// (seg006_1477_476, asm line 41433 "push 6"), which is 0x1C6 "a_splash". The id and the
    /// evidence are in UWObjectMechanics.SplashEffectObjectId, because an object falling into
    /// the water uses the same one (UWLevelLoader.fSplashAt).</summary>
    private const int SplashEffectObjectId = UWObjectMechanics.SplashEffectObjectId;

    /// <summary>How long a blow's impact flash, blood splat or hit flash shows - on a creature
    /// here, on an object or the world in UWCreatureWorld.</summary>
    public const float BlowEffectSeconds = 0.6f;

    // ------------------------------------------------- The registry the driver walks

    private static readonly List<UWCritter> msActive = new List<UWCritter>();

    /// <summary>Every enabled creature of the scene, for UWCritterDriver. A list kept by
    /// OnEnable/OnDisable instead of a scene search per frame.</summary>
    public static IReadOnlyList<UWCritter> Active => msActive;

    /// <summary>The enabled creatures by their master-list index (cleanup step 4.8, 2026-10-18),
    /// kept beside Active by the same OnEnable/OnDisable and by Initialise, which gives a creature
    /// its index after it was enabled.</summary>
    private static readonly Dictionary<int, UWCritter> msByIndex = new Dictionary<int, UWCritter>();

    /// <summary>
    /// The enabled creature with this master-list index, or null - index 0 (no data) and the
    /// player's index 1 included. ONE LOOKUP INSTEAD OF A WALK OF Active (cleanup step 4.8,
    /// 2026-10-18; section 7.4 of Docs/CLEANUP-PLAN.md): the blow's body (UWWorld), the motion
    /// core's mobiles (UWMotionHost) and a creature's defender each walked the list or the master
    /// list for it. When a slot is taken again in the frame its former creature is still being
    /// destroyed, the new creature is the one found (the walk found the former, first in Active).
    /// </summary>
    public static UWCritter AtIndex(int piIndex)
    {
        return piIndex > PlayerIndex && msByIndex.TryGetValue(piIndex, out UWCritter lOCritter) ? lOCritter : null;
    }

    private void fRegisterIndex()
    {
        if (Index > PlayerIndex && isActiveAndEnabled)
            msByIndex[Index] = this;
    }

    private void fUnregisterIndex()
    {
        if (Index > PlayerIndex && msByIndex.TryGetValue(Index, out UWCritter lOCritter) && lOCritter == this)
            msByIndex.Remove(Index);
    }

    // ------------------------------------------------- Components and data

    private UWDamageable mODamageable;
    private UWLevelLoader mOLevelLoader;

    /// <summary>The creature itself, engine-free (cleanup step 4.11, 2026-10-18): record, row,
    /// index, mind, and the decisions around the mind. Null until Initialise.</summary>
    private UWCreatureActor mOActor;

    /// <summary>The creature this component shows; null before Initialise.</summary>
    public UWCreatureActor Actor => mOActor;

    /// <summary>The record, null before Initialise.</summary>
    private UWCritterRecord mORecord => mOActor != null ? mOActor.Record : null;

    // ------------------------------------------------- The body

    /// <summary>The LOGICAL position: where the body is after the last applied step. The
    /// transform shows the picture, which catches up over the interval (section 3 of the
    /// contract). Every probe, distance and record write uses this one.</summary>
    private Vector3 mOBody;

    private Vector3 mOPictureFrom;

    private float mfPictureStart;

    private float mfPictureSeconds;

    // ------------------------------------------------- Damage bookkeeping

    /// <summary>This creature's index in the level's mobile list - who it is to everyone
    /// else's damage attribution.</summary>
    public int Index => mOActor != null ? mOActor.Index : 0;

    /// <summary>The hit points this creature takes blows on - the body a damage site hands to
    /// IUWWorld.Damage (UWWorld.TargetOf). Damage that nobody dealt (the lava, a damage trap, the
    /// Rune of Warding) is UWBlow.Trap, Lava or Ward since cleanup step 3.3 (2026-10-17); until then
    /// it was ApplyDamageWithoutAttacker here, whose reasons moved to UWBlow.Trap.</summary>
    public UWDamageable Damageable => mODamageable;

    // ------------------------------------------------- Trace

    private readonly string[] msTrace = new string[TraceLength];

    private int miTraceNext;

    private const int TraceLength = 8;

    private string msLastEvent = "";

    // ------------------------------------------------- Public adapters

    /// <summary>What the Motion's animation amounts to - see StateEnum.</summary>
    public StateEnum State
    {
        get
        {
            if (mORecord == null || IsDead)
                return StateEnum.Idle;

            switch (mORecord.Animation)
            {
                case UWCritterBrain.AnimWalking:
                    return mORecord.Goal == GoalAttack ? StateEnum.Pursuing : StateEnum.Wandering;

                case UWCritterBrain.AnimBackOff:
                    return StateEnum.Wandering;

                case UWCritterBrain.AnimCombatIdle:
                    return StateEnum.Ready;

                case UWCritterBrain.AnimMissile:
                case UWCritterBrain.AnimSpell:
                    return StateEnum.Attacking;

                default:
                    return mORecord.Animation >= 1 && mORecord.Animation <= 3 ? StateEnum.Attacking : StateEnum.Idle;
            }
        }
    }

    /// <summary>Facing in degrees around the vertical axis, same counting as the heading of the
    /// object data (0 = along the positive Z axis): the record's facing eighth times 45. Only
    /// the eighth counts for the picture (per user, 2026-09-14: the Slasher stood skewed with
    /// the fine value added); the fine part belongs to the turn limiter.</summary>
    public float FacingDegrees => UWHeadings.EighthToDegrees(mORecord != null ? mORecord.FacingEighth : 0);

    /// <summary>The facing eighth of the object data, 0 north, clockwise.</summary>
    public int FacingEighth => mORecord != null ? mORecord.FacingEighth : 0;

    /// <summary>
    /// The animation and frame the record holds right now. The ANIMATOR IS ADDED AFTER this
    /// creature (UWObjectSpawner), so it cannot be told during Initialise; it asks here
    /// instead, and a creature loaded in the middle of a fight shows its saved picture from
    /// the first frame rather than a standing one (per user, 2026-09-20: a loaded reaper kept
    /// glancing west for a moment).
    /// </summary>
    public bool TryGetStoredPicture(out int piAnimation, out int piFrame)
    {
        piAnimation = mORecord != null ? mORecord.Animation : -1;
        piFrame = mORecord != null ? mORecord.Frame : 0;

        return piAnimation >= 0;
    }

    /// <summary>Hostile (attitude 0) - UWCreatureActor.IsAggressive.</summary>
    public bool IsAggressive => mOActor != null && mOActor.IsAggressive;

    /// <summary>The attitude as it belongs in the object data, 0 hostile to 3 friendly.</summary>
    public int Attitude => mOActor != null ? mOActor.Attitude : HostileAttitude;

    /// <summary>The goal of the object data.</summary>
    public int Goal => mOActor != null ? mOActor.Goal : 0;

    /// <summary>The record has left the spot it was set up on - UWCreatureActor.HasMoved.</summary>
    public bool HasMoved => mOActor != null && mOActor.HasMoved;

    /// <summary>The logical position of the body (the picture may still be catching up): the
    /// record's spot as a world point - for the view, the effects and the remains. The record is
    /// the position (UWWorldSync captures it since cleanup step 4.8, 2026-10-18).</summary>
    public Vector3 BodyPosition => mOBody;

    /// <summary>Is this a water creature? For UWCritterAnimator, which has to lift its image
    /// back up by exactly what the body was lowered into the water.</summary>
    public bool IsSwimming => mOActor != null && mOActor.IsSwimming;

    public AttackKindEnum AttackKind
    {
        get
        {
            if (mORecord == null)
                return AttackKindEnum.Melee;

            if (mORecord.Animation == UWCritterBrain.AnimMissile)
                return AttackKindEnum.Ranged;

            return mORecord.Animation == UWCritterBrain.AnimSpell ? AttackKindEnum.Magic : AttackKindEnum.Melee;
        }
    }

    /// <summary>Which of the three melee attacks is running or ran last: zero the bash, one the
    /// slash, two the thrust - animation slot minus one.</summary>
    public int AttackIndex => mOActor != null ? mOActor.AttackIndex : 0;

    /// <summary>The brain, for tools that want to look at the record (F1 overlay).</summary>
    public UWCritterBrain Brain => mOActor != null ? mOActor.Brain : null;

    /// <summary>One step of goodwill less - UWCreatureActor.Anger; -1 for a creature that is
    /// gone.</summary>
    public int Anger()
    {
        return mOActor != null ? mOActor.Anger() : -1;
    }

    /// <summary>Byte 0x0A bit 7: the attitude is locked. Such a creature minds a theft only
    /// when the owner has bit 5 (UWCritterRules.MindsTheft).</summary>
    public bool IsAttitudeLocked => mOActor != null && mOActor.IsAttitudeLocked;

    // ------------------------------------------------- Setup

    public void Initialise(UWNpc pONpc, UWObjectClassProperties.Critter pOStats, bool pbHasStats, UWLevelLoader pOLevelLoader)
    {
        mOLevelLoader = pOLevelLoader;

        // The level's creature world; the scene's while this creature has no loader (a blow went
        // to UWScene.World then until cleanup step 4.11, 2026-10-18).
        UWWorld lOWorld = pOLevelLoader != null ? UWWorld.Of(pOLevelLoader) : UWScene.World;

        // The game's session is the creatures' services (random source, clock, alarm, path slots,
        // rule set) - read here, at the host, since cleanup phase 10 wave 4 (2026-10-26).
        mOActor = new UWCreatureActor(pONpc, pOStats, pbHasStats, lOWorld != null ? lOWorld.Creatures : null, this,
            mODamageable != null ? mODamageable.Health : null, UWGameSession.Current);

        fRegisterIndex();

        // THE RECORD IS THE POSITION (stage 2 of the motion rework, 2026-10-05): the body
        // stands at the record's eighth and zpos, as the original draws it, and the motion
        // core moves the record. A creature without data keeps the spot the spawner gave it.
        // A WATER CREATURE LIES IN THE WATER, not on it: a water tile's floor height is the
        // water LEVEL - the player swims at it - so UWSettings.SwimmerHeightOffset lowers the
        // body (per user, 2026-09-16); the animator lifts the picture back by the same.
        Vector3 lOAt = pONpc != null ? fBodyFromRecord() : transform.position;

        transform.position = lOAt;
        mOBody = lOAt;
        mOPictureFrom = lOAt;
        mfPictureSeconds = 0f;

        // The record's hit points, the creature saved while dying (its body committed by the actor's
        // death machine, through MarkDestroyed, since cleanup step 4.12, 2026-10-19; here until then)
        // and the record as the body's only store: see the UWCreatureActor constructor.
        if (mODamageable != null)
        {
            // Blows land on a dying creature: its record stays in the tile list until frame 3
            // (Obj_Rem in critter_ai), the blow finds it and damage_critter runs - crit_die only
            // refuses a second death. The brain does the same (fDeath); the body has to let the
            // blow through (per user in the original 2026-10-17: hit effect and red eyes).
            // Since cleanup step 4.12c (2026-10-19, "as the original") the body asks the actor's death
            // machine (UWCreatureActor.TakesBlows): every blow until the removal at frame 3, its
            // damage too - the world's routing asks the same (UWWorld's CreatureTakesBlows). Until then
            // TakesBlowsWhenBroken let the blow land and the routing dropped its damage.
            UWCreatureActor lOActor = mOActor;

            mODamageable.TakesBlowsRule = () => lOActor.TakesBlows;
        }

        // The saved animation and frame are shown by UWCritterAnimator.Initialise, which
        // UWObjectSpawner attaches after this component and which asks TryGetStoredPicture.

        UWCritterRecord lORecord = mOActor.Record;

        Trace("initialised: goal {0} gtarg {1} attitude {2} slot {3} interval {4} anim 0x{5:X2} facing {6} (+{7}) heading {8}",
            lORecord.Goal, lORecord.GTarg, lORecord.Attitude, lORecord.DueSlot, lORecord.Interval,
            lORecord.Animation, lORecord.FacingEighth, lORecord.FacingResidual, lORecord.FineHeading);
    }

    /// <summary>After a conversation: the script's fields carried into the record -
    /// UWCreatureActor.RefreshFromData.</summary>
    public void RefreshFromData()
    {
        if (mOActor != null)
            mOActor.RefreshFromData();
    }

    private void Awake()
    {
        mODamageable = GetComponent<UWDamageable>();
        mODamageable.CreatureTakesBlow += fTakeBlow;
    }

    private void OnEnable()
    {
        if (!msActive.Contains(this))
            msActive.Add(this);

        fRegisterIndex();
    }

    private void OnDisable()
    {
        msActive.Remove(this);
        fUnregisterIndex();
    }

    private void OnDestroy()
    {
        msActive.Remove(this);
        fUnregisterIndex();

        if (mODamageable != null)
            mODamageable.CreatureTakesBlow -= fTakeBlow;
    }

    /// <summary>Every blow the UWDamageable took, with its attacker - the actor's DamageNPC
    /// (UWCreatureActor.TakeBlow).</summary>
    private void fTakeBlow(int piDamage, int piAttacker)
    {
        if (mOActor != null)
            mOActor.TakeBlow(piDamage, piAttacker);
    }

    /// <summary>The picture between two due updates: the transform moves from where the
    /// previous step left it to the new logical position over the interval's duration
    /// (deviation 17, the user's decision to interpolate). The decision, the frame and the
    /// record change only at the update.</summary>
    private void Update()
    {
        if (mfPictureSeconds <= 0f || UWCharacter.TimeIsFrozen)
            return;

        float lfFraction = (Time.time - mfPictureStart) / mfPictureSeconds;

        if (lfFraction >= 1f)
        {
            transform.position = mOBody;
            mfPictureSeconds = 0f;
            fShowHeldMotion();

            return;
        }

        transform.position = Vector3.Lerp(mOPictureFrom, mOBody, lfFraction);
    }

    // ------------------------------------------------- The loop (contract section 1)

    /// <summary>Called by UWCritterDriver once per frame after the level clock advanced: the
    /// actor runs the creature's due updates (UWCreatureActor.RunDueUpdates) and this view shows
    /// each Motion.</summary>
    public void RunDueUpdates(UWCritterClock pOClock)
    {
        if (mOActor != null)
            mOActor.RunDueUpdates(pOClock);
    }

    /// <summary>
    /// After an update: the picture's facing and frame. The animator shows exactly the Motion's
    /// slot and frame until the next update (deviation 20) - with one exception.
    ///
    /// THE LAST STEP OF A WALK GLIDES WITH THE WALKING PICTURE (2026-10-06, the user's
    /// "sometimes they walk, sometimes they seem to slide", seen from behind on creatures moving
    /// away). The update moves the body FIRST, with the speed the last update left - the walk's -
    /// and the picture glides over that step (deviation 17); only then the mind decides to stand
    /// (wandering creatures do so all the time) or to get ready, and its standing picture came
    /// at once: a quarter of a second of a standing picture gliding along. The original has no
    /// glide - the body jumps at the update and shows the new picture there - so no moving
    /// standing picture ever appears in it. So while the body still glides a walking step, the
    /// walk goes on one frame, and the standing (or combat-ready) picture follows when the glide
    /// ends (fShowHeldMotion).
    /// </summary>
    public void ShowMotion(UWCritterBrain.Motion pOMotion)
    {
        if (pOMotion.Culled)
            return;

        UWCritterAnimator lOAnimator = GetComponent<UWCritterAnimator>();

        if (lOAnimator == null)
            return;

        bool lbGlidingAWalk = mfPictureSeconds > 0f && miShownAnimation == UWCritterBrain.AnimWalking
            && (pOMotion.Animation == UWCritterBrain.AnimStanding || pOMotion.Animation == UWCritterBrain.AnimCombatIdle);

        if (lbGlidingAWalk)
        {
            mbHeldMotion = true;
            miHeldAnimation = pOMotion.Animation;
            miHeldFrame = pOMotion.Frame;
            miShownFrame = (miShownFrame + 1) & 3;
            lOAnimator.ShowMotion(UWCritterBrain.AnimWalking, miShownFrame);

            return;
        }

        mbHeldMotion = false;
        miShownAnimation = pOMotion.Animation;
        miShownFrame = pOMotion.Frame;
        lOAnimator.ShowMotion(pOMotion.Animation, pOMotion.Frame);
    }

    /// <summary>The picture the last update decided, once the walking step it waited for has
    /// glided to its end - see ShowMotion.</summary>
    private void fShowHeldMotion()
    {
        if (!mbHeldMotion)
            return;

        mbHeldMotion = false;
        miShownAnimation = miHeldAnimation;
        miShownFrame = miHeldFrame;

        UWCritterAnimator lOAnimator = GetComponent<UWCritterAnimator>();

        if (lOAnimator != null)
            lOAnimator.ShowMotion(miHeldAnimation, miHeldFrame);
    }

    /// <summary>What the animator shows (the walk while a step still glides, see ShowMotion),
    /// and the decided picture waiting for the glide's end.</summary>
    private int miShownAnimation = -1;

    private int miShownFrame;

    private bool mbHeldMotion;

    private int miHeldAnimation;

    private int miHeldFrame;

    // ------------------------------------------------- The body (contract section 3)

    /// <summary>The body at the record's eighth and height - the picture glides there from
    /// where it is over the interval that has just elapsed (deviation 17, the user's decision
    /// to interpolate). After every step on the motion core (UWCreatureActor's RunMotion).</summary>
    public void PlaceBody(int piInterval)
    {
        Vector3 lOAt = fBodyFromRecord();

        if ((lOAt - mOBody).sqrMagnitude < 0.0001f)
        {
            mOBody = lOAt;
            transform.position = mOBody;
            mfPictureSeconds = 0f;

            return;
        }

        mOPictureFrom = transform.position;
        mfPictureStart = Time.time;
        mfPictureSeconds = fGetIntervalSeconds(piInterval);
        mOBody = lOAt;
    }

    /// <summary>The body at the record's position at once, picture included (a teleport, the
    /// sleep ambush).</summary>
    public void SetBodyNow()
    {
        mOBody = fBodyFromRecord();
        transform.position = mOBody;
        mfPictureSeconds = 0f;
        fShowHeldMotion();
    }

    /// <summary>The world point of the record: the centre of its eighth, its zpos, a swimmer
    /// lowered by its offset.</summary>
    private Vector3 fBodyFromRecord()
    {
        return new Vector3(
            UWUnits.SubTileToWorldAxis(mORecord.TileX, mORecord.FineX),
            UWUnits.ZPosToWorld(mORecord.ZPos) + fGetSwimmerOffset(),
            UWUnits.SubTileToWorldAxis(mORecord.TileY, mORecord.FineY));
    }

    /// <summary>The seconds one interval of slots lasts: 16 PIT ticks per slot at 256 per
    /// second, twice as long while the player's Speed halves the world's slot advances.</summary>
    private static float fGetIntervalSeconds(int piInterval)
    {
        float lfSeconds = Mathf.Max(1, piInterval) * UWCritterRules.SlotPitTicks / PitTicksPerSecond;

        return UWCritterDriver.PlayerHasSpeed ? lfSeconds * 2f : lfSeconds;
    }

    /// <summary>A world direction as the original's 16-bit heading (0 = +Z north, 0x4000 = +X
    /// east) - for the player's shove (UWPlayerMovement).</summary>
    public static int DirectionToHeading(Vector3 pODirection)
    {
        return Mathf.RoundToInt(Mathf.Atan2(pODirection.x, pODirection.z) / (2f * Mathf.PI) * 0x10000) & 0xFFFF;
    }

    /// <summary>How deep a WATER creature lies below the water level, in world units - see
    /// the placement in Initialise. Zero for everyone else.</summary>
    private float fGetSwimmerOffset()
    {
        if (!IsSwimming)
            return 0f;

        UWSettings lOSettings = UWSettings.Instance;

        return lOSettings != null ? lOSettings.SwimmerHeightOffset : 0f;
    }

    /// <summary>Where the creature looks from and shoots from: the centre of its body, not its
    /// feet - otherwise every floor step blocks the view.</summary>
    public Vector3 EyePosition
    {
        get
        {
            Collider lOCollider = GetComponent<Collider>();

            return lOCollider != null
                ? new Vector3(mOBody.x, lOCollider.bounds.center.y, mOBody.z)
                : mOBody + (Vector3.up * FallbackEyeHeight);
        }
    }

    // ------------------------------------------------- ICreatureView: the blow, the death

    /// <summary>This creature as the target of a blow, with its UWDamageable.</summary>
    public UWDamageTarget DamageTarget => UWWorld.TargetOf(this, mODamageable);

    /// <summary>This creature as the engine-free searches see it (UWWorld.RefOf).</summary>
    public UWCreatureRef Ref => UWWorld.RefOf(this);

    /// <summary>The body counts as destroyed from now on (UWDamageable.MarkDestroyed raises
    /// Destroyed once) - only the actor's death machine commits a death (cleanup step 4.12).</summary>
    public void MarkDestroyed()
    {
        if (mODamageable != null)
            mODamageable.MarkDestroyed();
    }

    /// <summary>LoadSoundAtCoordinate at the creature: the numbers are the original's, which
    /// UWAudioService plays directly (footsteps 1, 2, 5, 0x0D, 0x0E, 0x17; death 6).</summary>
    public void PlaySound(int piSound)
    {
        UWAudioService.PlayEffectAt(piSound, mOBody);
    }

    /// <summary>Nothing in reach, or the world in the way: sound 0x0A at the attacker (label
    /// C34, CombatMissImpactSound(0)).</summary>
    public void PlayMissSound()
    {
        UWAudioService.PlayEffectAt(UWSoundEffectId.Miss, mOBody);
    }

    /// <summary>Sound 4 at this defender with the pre-armour value on a hit; blocked: 7 or 8 at
    /// it - the armour kind read from the ATTACKER's row, as do_miss does
    /// (UWCritterCombat.GetBlockedSound, through UWAttackResolution).</summary>
    public void PlayStruckSound(in UWAttackOutcome pOOutcome)
    {
        if (pOOutcome.Hit)
            UWAudioService.PlayEffectAt(UWSoundEffectId.HitCritter, mOBody, pOOutcome.HitVolume);
        else
            UWAudioService.PlayEffectAt(pOOutcome.BlockedSound, mOBody);
    }

    /// <summary>
    /// The aftermath of a damaging hit on this creature (AttackerAppliesFinalDamage labels
    /// B49-C17): table byte 8 bits 3-4 nonzero the blood splat (class 7 offset 0), else the
    /// impact flash (offset 0x0B), at HitZ[part] eighths of the body height above the feet.
    /// The original starts the animation at frame min(3, damage / 4); the port's effects
    /// play whole (SpawnEffectAt has no start frame), so a heavier blow shows the same splat.
    ///
    /// WHERE IT SITS (do_damage, UnderworldExhumed combat/COMBAT.C, 2026-10-12): a part whose
    /// armour byte is 0xFF moves the effect to part 0's height, the body (hitloc &amp;= 4); a
    /// missile's part (4 and up) puts it at the missile's own middle (hitz, missile_thwack),
    /// piMissileZ in zpos. Until then ours kept the struck part's height and put a missile's
    /// effect at the feet.
    /// </summary>
    public void ShowHitEffect(int piPart, int piDamage, int piMissileZ)
    {
        if (mOLevelLoader == null || mOActor == null || !mOActor.HasStats)
            return;

        float lfHeight = GetHitEffectHeight(piPart, piMissileZ);

        mOLevelLoader.SpawnEffectAt(UWCritterCombat.GetHitEffectObjectId(mOActor.Stats),
            mOBody + (Vector3.up * lfHeight), BlowEffectSeconds);
    }

    /// <summary>The height of the blood or flash above this creature's feet, in world units:
    /// HitZ of the part (a part without armour of its own the body's) in eighths of the body, or
    /// a missile's own height piMissileZ (zpos) for a missile's part - see ShowHitEffect.</summary>
    public float GetHitEffectHeight(int piPart, int piMissileZ = 0)
    {
        if (mOActor == null || !mOActor.HasStats)
            return 0f;

        int liRadius;
        int liHeight;
        mOActor.GetOwnSize(out liRadius, out liHeight);

        int liPart = UWCritterCombat.GetEffectPart(mOActor.Stats, piPart);

        if (liPart >= UWCritterCombat.MissilePartOffset)
            return piMissileZ > 0 ? (piMissileZ - mORecord.ZPos) * UWWorldScale.ZPosStep : 0f;

        return liHeight * UWWorldScale.ZPosStep * UWCritterCombat.GetHitZ(liPart) / 8f;
    }

    /// <summary>
    /// The blood or the flash of a hit that did NOT come from this creature's own melee -
    /// an arrow, a thrown rock, a bolt. The original runs the same AttackerAppliesFinalDamage
    /// for a missile (MissileAttackHit_seg022_14BF, label 158B), so the effect is the
    /// creature's own (row byte 8 bits 3-4) at the height of the part hit. Ours showed nothing
    /// at all for the player's arrow (per user, 2026-09-20). The part is the one PickMissilePart
    /// picked for the armour (since cleanup step 4.9b, 2026-10-18); a missile's hitz is its z
    /// plus half its height (missile_thwack), piMissileZ.
    /// </summary>
    public void ShowMissileHitEffect(int piPart, int piDamage, int piMissileZ)
    {
        ShowHitEffect(piPart, piDamage, piMissileZ);
    }

    /// <summary>
    /// A land creature ended its step in deep water (UWCreatureActor's OnDrowned): the ONE splash
    /// at its position, and sound 0, the game's water sound - the branch itself calls no
    /// LoadSoundAtCoordinate, so the number is not in the disassembly; the user heard a splash
    /// (2026-09-20), and sound 0 is the one the player's own step into water plays
    /// (UWPlayerTerrain).
    /// </summary>
    public void ShowDrowning()
    {
        UWAudioService.PlayEffectAt(UWSoundEffectId.Water, mOBody);

        if (mOLevelLoader != null)
            mOLevelLoader.SpawnEffectAt(SplashEffectObjectId, mOBody, UWLevelLoader.SplashEffectSeconds);
    }

    /// <summary>Frame 3 of animation 0x0C, after the actor's death machine committed the death and
    /// applied a named creature's: the body goes, once. UWCritterRemains leaves the remains and the
    /// loot and frees the object. Until cleanup step 4.12 (2026-10-19) the animator was told here
    /// that the death pictures were over, for UWCritterRemains' per-frame wait; the machine is the
    /// only one that ends a death now.</summary>
    public void CompleteDeath()
    {
        // The remains go where the creature LOGICALLY stands, not where its picture happens
        // to have got to - the picture lags behind by up to one interval (fApplyMotion), and
        // for a creature that drowned one interval ago that is the difference between the
        // water tile and the bank. UWCritterRemains reads transform.position.
        transform.position = mOBody;
        mfPictureSeconds = 0f;

        UWCritterRemains lORemains = GetComponent<UWCritterRemains>();

        if (lORemains != null)
            lORemains.CompleteDeath();
        else
            Destroy(gameObject);
    }

    // ------------------------------------------------- IUWAttackDefender (the actor's)

    /// <summary>This creature as the defender of a blow - the actor's (UWCreatureActor); a
    /// creature never initialised answers as the body's single values.</summary>
    UWDefenderKind IUWAttackDefender.DefenderKind => UWDefenderKind.Creature;

    int IUWAttackDefender.Defence => mOActor != null ? ((IUWAttackDefender)mOActor).Defence
        : (mODamageable != null ? mODamageable.Defence : 0);

    int IUWAttackDefender.ProtectionAt(int piPart) => 0;

    int IUWAttackDefender.ArmourAt(int piPart)
    {
        if (mOActor != null)
            return ((IUWAttackDefender)mOActor).ArmourAt(piPart);

        return piPart < 0 && mODamageable != null ? mODamageable.Armour : 0;
    }

    bool IUWAttackDefender.IsArmourMetalAt(int piPart) => false;

    void IUWAttackDefender.OnCriticalHit(int piPart)
    {
    }

    /// <summary>The part a blow whose probe spans these heights (zpos) hits on this body -
    /// UWCreatureActor.PickPartHitBy.</summary>
    public int PickPartHitBy(int piProbeBottom, int piProbeTop)
    {
        return mOActor != null ? mOActor.PickPartHitBy(piProbeBottom, piProbeTop) : 0;
    }

    /// <summary>The armour of one part of this creature - UWCreatureActor.GetArmourOfPart.</summary>
    public int GetArmourOfPart(int piPart)
    {
        return mOActor != null ? mOActor.GetArmourOfPart(piPart) : 0;
    }

    /// <summary>The part a missile from these heights strikes on this body -
    /// UWCreatureActor.PickMissilePart.</summary>
    public int PickMissilePart(int piMissileZ, int piMissileTop)
    {
        return mOActor != null ? mOActor.PickMissilePart(piMissileZ, piMissileTop) : 0;
    }

    // ------------------------------------------------- What the port asks of the creature (the actor's)

    /// <summary>Confusion - UWCreatureActor.Confuse.</summary>
    public UWTargetSpellRules.Result Confuse()
    {
        return mOActor != null ? mOActor.Confuse() : UWTargetSpellRules.Result.Missed;
    }

    /// <summary>Ally - UWCreatureActor.Befriend.</summary>
    public UWTargetSpellRules.Result Befriend()
    {
        return mOActor != null ? mOActor.Befriend() : UWTargetSpellRules.Result.Missed;
    }

    /// <summary>Paralyze - UWCreatureActor.Paralyse.</summary>
    public UWTargetSpellRules.Result Paralyse()
    {
        return mOActor != null ? mOActor.Paralyse() : UWTargetSpellRules.Result.Missed;
    }

    /// <summary>Cause Fear - UWCreatureActor.Flee.</summary>
    public UWTargetSpellRules.Result Flee()
    {
        return mOActor != null ? mOActor.Flee() : UWTargetSpellRules.Result.Missed;
    }

    /// <summary>Is this creature Tybal? For breaking the orb (UWTybalOrbRules.TybalWhoAmI).</summary>
    public bool IsTybal => mOActor != null && mOActor.IsTybal;

    /// <summary>The creature's record - for the sleep ambush (UWSleepRules.TryAmbush).</summary>
    public UWCritterRecord Record => mORecord;

    /// <summary>Its death is committed - it may still be falling (the actor's death machine,
    /// UWCreatureActor.IsDead); the body's flag for a creature never initialised.</summary>
    public bool IsDead => mOActor != null ? mOActor.IsDead : mODamageable != null && mODamageable.IsDestroyed;

    /// <summary>Critter table byte 0x1C, upper nibble; 0 without a table row.</summary>
    public int TravelRange => mOActor != null ? mOActor.TravelRange : 0;

    /// <summary>The ally bit of the mobile record - the player's blow passes over an ally
    /// (UWPlayerMelee.fFindClassicMeleeTarget, set_hitobj).</summary>
    public bool IsAlly => mOActor != null && mOActor.IsAlly;

    /// <summary>The table's average hit points (critter byte 4, avghit) - what the eyes measure
    /// against (UWCombat.GetEyesGroup); 0 without a table row.</summary>
    public int TableVitality => mOActor != null ? mOActor.TableVitality : 0;

    /// <summary>Whether the player's critical failure against this creature wears the weapon
    /// (UWEquipmentWear).</summary>
    public bool WearsWeaponOnCriticalFailure => mOActor != null && mOActor.WearsWeaponOnCriticalFailure;

    /// <summary>The sleep ambush's path - UWCreatureActor.TryGetPathTo.</summary>
    public bool TryGetPathTo(UWTilePos pOTo, int piToHeight, List<UWTilePos> pOPath)
    {
        if (mOActor != null)
            return mOActor.TryGetPathTo(pOTo, piToHeight, pOPath);

        pOPath.Clear();

        return false;
    }

    /// <summary>The sleep ambush sets it down - UWCreatureActor.PlaceAtSpot.</summary>
    public bool PlaceAtSpot(UWTilePos pOTile, int piSubX, int piSubY)
    {
        return mOActor != null && mOActor.PlaceAtSpot(pOTile, piSubX, piSubY);
    }

    /// <summary>The night's time pass rewrote the record - UWCreatureActor.FollowRecord.</summary>
    public void FollowRecord()
    {
        if (mOActor != null)
            mOActor.FollowRecord();
    }

    /// <summary>The orb is broken - UWCreatureActor.WeakenByBrokenOrb.</summary>
    public void WeakenByBrokenOrb()
    {
        if (mOActor != null)
            mOActor.WeakenByBrokenOrb();
    }

    /// <summary>Does this creature see a theft on that spot? UWCreatureActor.CanSeeTheftAt.</summary>
    public bool CanSeeTheftAt(int piX8, int piY8, int piZ)
    {
        return mOActor != null && mOActor.CanSeeTheftAt(piX8, piY8, piZ);
    }

    /// <summary>A shove - UWCreatureActor.Push.</summary>
    public void Push(int piHeading, int piSpeed, int piVz)
    {
        if (mOActor != null)
            mOActor.Push(piHeading, piSpeed, piVz);
    }

    // ------------------------------------------------- The kin alarm

    /// <summary>When building a level: a fresh clock and alarm (UWScene), see
    /// UWGameSession.ResetCritterServices. Loading a save restores the save's alarm afterwards
    /// (UWLevelLoader.fRestoreKinAlarm).</summary>
    public static void ResetAlarm()
    {
        UWScene.ResetCritterServices();
    }

    // ------------------------------------------------- Trace of the decisions

    /// <summary>A line of the trace, while UWSettings.CritterTrace is on.</summary>
    public void Trace(string psFormat, params object[] pOArguments)
    {
        UWSettings lOSettings = UWSettings.Instance;

        if (lOSettings == null || !lOSettings.CritterTrace)
            return;

        // The message is formatted FIRST and the time put in front afterwards - putting the
        // time in as an extra argument shifted every placeholder by one (2026-09-16).
        string lsText = pOArguments == null || pOArguments.Length == 0
            ? psFormat
            : string.Format(psFormat, pOArguments);

        msTrace[miTraceNext] = string.Format("{0:F1}s {1}", Time.time, lsText);
        miTraceNext = (miTraceNext + 1) % TraceLength;
    }

    /// <summary>An event of the brain: the last one shows in the overlay line, all of them in
    /// the trace.</summary>
    public void Event(string psFormat, params object[] pOArguments)
    {
        msLastEvent = pOArguments == null || pOArguments.Length == 0 ? psFormat : string.Format(psFormat, pOArguments);
        Trace(msLastEvent);
    }

    /// <summary>
    /// The creature as the F1 panel shows it (UWDebugOverlay, cleanup 8c, 2026-10-25; until then
    /// DescribeCombatStats and GetTrace wrote its lines): the record's and the actor's numbers through
    /// the engine-free view (UWDebugCreature.FillRecord: goal, target, slot and interval, animation
    /// and frame, the temper bits, the kin alarm - added 2026-09-27 for the summoned ally that stayed
    /// mellow next to a creature the player hit), and the scene's part here - the index, the body's
    /// height and the floor under it, the picture's slot and frame, the last event and the trace.
    /// </summary>
    public void FillDebug(UWDataImport.UWData.UWDebugCreature pOView)
    {
        UWCritterClock lOClock = UWScene.CritterClock;

        pOView.FillRecord(mORecord, mOActor, UWScene.CritterAlarm, lOClock != null ? lOClock.Clock : 0, lOClock != null ? lOClock.Phase : 0);

        if (pOView.HasRecord)
        {
            UWCritterAnimator lOAnimator = GetComponent<UWCritterAnimator>();
            UWTile lOTile = fGetTileAt(mOBody);

            pOView.Index = Index;
            pOView.BodyY = mOBody.y;
            pOView.TileFloor = lOTile == null ? -1 : lOTile.FloorHeight;
            pOView.PictureSlot = lOAnimator == null ? -1 : lOAnimator.CurrentSlot;
            pOView.PictureFrame = lOAnimator == null ? -1 : lOAnimator.CurrentFrameInSegment;
            pOView.LastEvent = msLastEvent;
        }

        // The trace, oldest line first. Empty while tracing is off.
        for (int liAt = 0; liAt < TraceLength; liAt++)
        {
            string lsLine = msTrace[(miTraceNext + liAt) % TraceLength];

            if (!string.IsNullOrEmpty(lsLine))
                pOView.Trace.Add(lsLine);
        }
    }

    private UWTile fGetTileAt(Vector3 pOWorldPosition)
    {
        UWTilePos lOTile = UWTileQueries.WorldToTile(pOWorldPosition.x, pOWorldPosition.z);

        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null
            || mOLevelLoader.CurrentLevel.TileData == null)
            return null;

        return mOLevelLoader.CurrentLevel.GetTile(lOTile.X, lOTile.Y);
    }
}
