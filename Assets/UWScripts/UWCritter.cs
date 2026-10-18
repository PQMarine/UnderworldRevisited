using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;
using UnderworldRevisited;
using UnderworldRevisited.Build;

/// <summary>
/// The Unity host of one creature (rebuilt 2026-09-20 around the brain): the body, the
/// physics substitute and the engine work around the engine-free creature mind
/// UWCritterBrain.
///
/// NOTHING HERE DECIDES. The brain (Assets/UWDataImport/UWData/UWCritterBrain.cs) runs
/// NPCInitialProcessing_seg007_2488 of UW.EXE on the creature's record (UWCritterRecord, the
/// 27-byte mobile object record of its UWNpc) and returns a Motion: heading, speed, facing,
/// animation slot and frame, the pitch of a flier and when it is next due on the level's
/// 16-slot clock (UWScene.CritterClock). This class
///
///   - is the ICritterHost the brain asks about the world (tiles, other objects, line of
///     sight, the path search, doors, the ammunition table) and raises its events to (the
///     blow, missiles, spells, talk, sounds, music, death);
///   - applies the Motion when the creature is next due (UWCritterDriver walks every creature
///     once per frame): one displacement along the fine heading through the clearance
///     probes, then reports what the step did as a StepResult;
///   - keeps the record's position fields current and relinks the object's tile list;
///   - interpolates the picture between two due updates so a walking creature does not jump
///     every quarter second (Docs/AI/brain-and-host.md section 3);
///   - keeps the public members the rest of the port reads (State, FacingDegrees, Goal,
///     Attitude, ...) as thin adapters over the record.
///
/// The contract between the two halves is Docs/AI/brain-and-host.md; the rules behind the
/// brain are Docs/AI/creature-ai.md. Everything the brain owns in the record (section 6
/// of the contract) is never written here, with the two exceptions the contract names: the
/// position after a step and the goal a conversation or spell script sets.
/// </summary>
[RequireComponent(typeof(UWDamageable))]
public class UWCritter : MonoBehaviour, ICritterHost, IUWAttackDefender
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

    private const int WanderGoal = 2;

    private const int GoalFollow = 3;

    private const int GoalWithdraw = 6;

    private const int GoalParalysed = 7;

    private const int GoalTalk = 10;

    /// <summary>The player's object index and item id as the brain names them.</summary>
    private const int PlayerIndex = UWCritterBrain.PlayerIndex;

    /// <summary>Layer of the sprites - the same number as in UWObjectSpawner.SpriteLayer. It is
    /// left out when searching for the floor and for a line of sight.</summary>
    private const int SpriteLayer = 8;

    /// <summary>
    /// What the blow probe of fScanForDefender may meet: EVERY layer. Until 2026-09-27 the
    /// sprite layer was left out, so that items lying about would not catch a blow - but the
    /// creatures' bodies are on that layer too (UWObjectSpawner gives every object's collider
    /// the sprite layer), so a creature's blow could never find another creature: "nothing in
    /// reach" with the reaper right in front of a summoned feral troll, and the reaper never
    /// turned on it (per user). The probe now filters instead: on the sprite layer only a
    /// living creature counts, as defender and as obstacle alike.
    /// </summary>
    private const int SweepLayerMask = Physics.AllLayers;

    /// <summary>Fallback height in case a creature has no collider from which its centre could
    /// be read.</summary>
    private const float FallbackEyeHeight = 20f;

    /// <summary>The original's timer ticks per second (spec 1.1; UWPlayerTick is the home).</summary>
    public const float PitTicksPerSecond = UWPlayerTick.PitTicksPerSecond;

    /// <summary>How many due updates one creature may run in one frame. The clock never
    /// advances more than four slots per frame, so an interval-1 creature needs at most four;
    /// the margin catches a record whose interval loaded as 0.</summary>
    private const int MaxUpdatesPerFrame = 8;

    /// <summary>The splash of a drowning creature: the class-7 object with offset 6
    /// (seg006_1477_476, asm line 41433 "push 6"), which is 0x1C6 "a_splash". The id and the
    /// evidence are in UWObjectMechanics.SplashEffectObjectId, because an object falling into
    /// the water uses the same one (UWLevelLoader.fSplashAt).</summary>
    private const int SplashEffectObjectId = UWObjectMechanics.SplashEffectObjectId;

    /// <summary>Radius and height from COMOBJ, in case the table is missing: creatures 2 and
    /// 0x20.</summary>
    private const int FallbackCritterRadius = 2;

    private const int FallbackCritterHeight = 0x20;

    /// <summary>First ammunition object: the arrow. The critter table counts from here.</summary>
    private const int FirstAmmunitionObjectId = UWObjectMechanics.FirstAmmunitionObjectId;

    /// <summary>How far in front of the surface a blow's impact sits, in world units.</summary>
    private const float BlowEffectSetback = 4f;

    private const float BlowEffectSeconds = 0.6f;

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
        if (miIndex > PlayerIndex && isActiveAndEnabled)
            msByIndex[miIndex] = this;
    }

    private void fUnregisterIndex()
    {
        if (miIndex > PlayerIndex && msByIndex.TryGetValue(miIndex, out UWCritter lOCritter) && lOCritter == this)
            msByIndex.Remove(miIndex);
    }

    // ------------------------------------------------- Components and data

    private UWDamageable mODamageable;
    private UWLevelLoader mOLevelLoader;
    private UWCharacter mOPlayer;
    private Transform mOPlayerTransform;

    private UWObjectClassProperties.Critter mOStats;
    private bool mbHasStats;
    private bool mbFlying;
    private bool mbSwimming;

    /// <summary>Its own object data; the record lies over its RawNpcBytes.</summary>
    private UWNpc mONpc;

    private UWCritterRecord mORecord;

    private UWCritterBrain mOBrain;

    /// <summary>The object's index in the level's master list (1 is the player); 0 when the
    /// object is not in the list.</summary>
    private int miIndex;

    // ------------------------------------------------- The body

    /// <summary>The LOGICAL position: where the body is after the last applied step. The
    /// transform shows the picture, which catches up over the interval (section 3 of the
    /// contract). Every probe, distance and record write uses this one.</summary>
    private Vector3 mOBody;

    private Vector3 mOPictureFrom;

    private float mfPictureStart;

    private float mfPictureSeconds;

    /// <summary>What the last step on the motion core left behind (the F1 overlay).</summary>
    private StepResult mOLastStep;

    private bool mbRemoved;

    // ------------------------------------------------- Damage bookkeeping

    /// <summary>This creature's index in the level's mobile list - who it is to everyone
    /// else's damage attribution.</summary>
    public int Index
    {
        get { return miIndex; }
    }

    /// <summary>The hit points this creature takes blows on - the body a damage site hands to
    /// IUWWorld.Damage (UWWorld.TargetOf). Damage that nobody dealt (the lava, a damage trap, the
    /// Rune of Warding) is UWBlow.Trap, Lava or Ward since cleanup step 3.3 (2026-10-17); until then
    /// it was ApplyDamageWithoutAttacker here, whose reasons moved to UWBlow.Trap.</summary>
    public UWDamageable Damageable => mODamageable;

    /// <summary>A blow of this creature or on it, through the world (cleanup step 3.3, 2026-10-17).
    /// The level object's world; the scene's while this creature has no loader.</summary>
    private void fDamage(UWDamageTarget pOTarget, UWBlow pOBlow)
    {
        UWWorld lOWorld = mOLevelLoader != null ? UWWorld.Of(mOLevelLoader) : UWScene.World;

        if (lOWorld != null)
            lOWorld.Damage(pOTarget, pOBlow);
    }

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
            if (mORecord == null || (mODamageable != null && mODamageable.IsDestroyed))
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

    /// <summary>Hostile (attitude 0). A hit makes any creature hostile through the brain's
    /// damage reaction; the trespass trap through Anger.</summary>
    public bool IsAggressive => mORecord != null && mORecord.Attitude == HostileAttitude;

    /// <summary>The attitude as it belongs in the object data, 0 hostile to 3 friendly.</summary>
    public int Attitude => mORecord != null ? mORecord.Attitude : HostileAttitude;

    /// <summary>The goal of the object data.</summary>
    public int Goal => mORecord != null ? mORecord.Goal : 0;

    /// <summary>Where the record stood at build time: tile, eighth and zpos (cleanup step 4.8,
    /// 2026-10-18; the body's world point until then).</summary>
    private int miSpawnTileX;

    private int miSpawnTileY;

    private int miSpawnFineX;

    private int miSpawnFineY;

    private int miSpawnZPos;

    /// <summary>The record has left the spot it was set up on - UWWorldSync only relinks it and
    /// pulls it clear of the walls then, so whoever stood still keeps its place exactly as loaded.
    /// Any change of tile, eighth or zpos counts, as any of them moved the body by more than the
    /// one unit the old test asked for (an eighth is 8 units, a zpos step 2).</summary>
    public bool HasMoved => mORecord != null && mONpc != null
        && (mORecord.TileX != miSpawnTileX || mORecord.TileY != miSpawnTileY || mORecord.FineX != miSpawnFineX
            || mORecord.FineY != miSpawnFineY || mORecord.ZPos != miSpawnZPos);

    /// <summary>The logical position of the body (the picture may still be catching up): the
    /// record's spot as a world point - for the view, the effects and the remains. The record is
    /// the position (UWWorldSync captures it since cleanup step 4.8, 2026-10-18).</summary>
    public Vector3 BodyPosition => mOBody;

    /// <summary>Is this a water creature? For UWCritterAnimator, which has to lift its image
    /// back up by exactly what the body was lowered into the water.</summary>
    public bool IsSwimming => mbSwimming;

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
    public int AttackIndex => miAttackIndex;

    private int miAttackIndex;

    /// <summary>The brain, for tools that want to look at the record (F1 overlay).</summary>
    public UWCritterBrain Brain => mOBrain;

    /// <summary>
    /// Takes one step of goodwill away from the creature, never below zero, which is hostile
    /// (UWCritterRules.LowerAttitudeForTheft, AngerNPCByIllegalAction_ovr104_C37). Used by the
    /// theft notice, a_do trap 5 (trespass) and a conversation. Returns the NEW attitude, which
    /// picks the message - or -1 for a creature that is gone. A creature already hostile stays
    /// so and still returns 0: in the original it says "is angered" again (read 2026-09-23;
    /// until then it stayed silent here).
    /// </summary>
    public int Anger()
    {
        if (mORecord == null || (mODamageable != null && mODamageable.IsDestroyed))
            return -1;

        mORecord.Attitude = UWCritterRules.LowerAttitudeForTheft(mORecord.Attitude);
        fTrace("angered: attitude {0}", mORecord.Attitude);

        return mORecord.Attitude;
    }

    /// <summary>Byte 0x0A bit 7: the attitude is locked. Such a creature minds a theft only
    /// when the owner has bit 5 (UWCritterRules.MindsTheft).</summary>
    public bool IsAttitudeLocked => mORecord != null && mORecord.AttitudeLocked;

    // ------------------------------------------------- Setup

    public void Initialise(UWNpc pONpc, UWObjectClassProperties.Critter pOStats, bool pbHasStats, UWLevelLoader pOLevelLoader)
    {
        mOStats = pOStats;
        mbHasStats = pbHasStats;
        mOLevelLoader = pOLevelLoader;

        // Flier and swimmer come from table byte 0x0A bits 7 and 6 (spec 2, deviation 42),
        // not from the category byte any more.
        mbFlying = pbHasStats && pOStats.IsFlier;
        mbSwimming = pbHasStats && pOStats.IsSwimmer;

        // A creature without data still gets a record over a scratch UWNpc so the brain can
        // run; nothing of it is saved.
        mONpc = pONpc ?? new UWNpc((ushort)(pbHasStats ? UWObjectClassProperties.CritterFirstId : 0));
        mORecord = new UWCritterRecord(mONpc);

        // clear_paths with every level build (init_level_creature_stuff): no creature holds a
        // stored path - the slots are new (UWGameSession.ResetCritterServices).
        mORecord.HasPath = false;

        miIndex = mOLevelLoader != null && mOLevelLoader.CurrentLevel != null && pONpc != null
            ? Mathf.Max(0, mOLevelLoader.CurrentLevel.Masterlist.IndexOf(pONpc))
            : 0;

        mOBrain = new UWCritterBrain(mORecord, pOStats, miIndex);

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

        miSpawnTileX = mORecord.TileX;
        miSpawnTileY = mORecord.TileY;
        miSpawnFineX = mORecord.FineX;
        miSpawnFineY = mORecord.FineY;
        miSpawnZPos = mORecord.ZPos;

        // THE RECORD'S HIT POINTS ARE THE BRAIN'S. Until 2026-10-18 a record holding 0 got the
        // table value here (the spawner's UWDamageable started there), so that the first scratch
        // would not kill. THE ORIGINAL KEEPS THE 0: nothing fills a creature's hit points on
        // loading (UnderworldExhumed has no such step), and a living creature with 0 is one
        // whose death was refused - the golem saved before his script sets 125 - and dies at the
        // next blow (per user's decision the same day, cleanup 4.8). The shipped levels hold 0
        // only in five 0x7D/0x7E records of level 9, whose table value is 0 as well.
        // A creature SAVED WHILE DYING (animation 0x0C, hp 0) loads dead: its hit points stay 0 and
        // the body counts as destroyed, without a second death (2026-10-17, B11). Before, the
        // spawner's table value revived it, and one that never finishes dying could be killed
        // again for a second share of experience.
        // FROM HERE ON THE RECORD IS THE BODY'S ONLY STORE (cleanup step 4.8, 2026-10-18, B23): the
        // UWDamageable reads and writes byte 8 (UWHealth.KeepHitPointsIn), so nothing has to keep
        // two counts in step any more.
        bool lbLoadedDying = pONpc != null && mORecord.HitPoints == 0 && mORecord.Animation == UWCritterBrain.AnimDying;

        if (mODamageable != null)
        {
            mODamageable.Health.KeepHitPointsIn(mORecord);

            if (lbLoadedDying)
                mODamageable.MarkDestroyed();

            // Blows land on a dying creature: its record stays in the tile list until frame 3
            // (Obj_Rem in critter_ai), the blow finds it and damage_critter runs - crit_die only
            // refuses a second death. The brain does the same (fDeath); the body has to let the
            // blow through (per user in the original 2026-10-17: hit effect and red eyes).
            mODamageable.TakesBlowsWhenBroken = true;
        }

        // The saved animation and frame are shown by UWCritterAnimator.Initialise, which
        // UWObjectSpawner attaches after this component and which asks TryGetStoredPicture.

        fTrace("initialised: goal {0} gtarg {1} attitude {2} slot {3} interval {4} anim 0x{5:X2} facing {6} (+{7}) heading {8}",
            mORecord.Goal, mORecord.GTarg, mORecord.Attitude, mORecord.DueSlot, mORecord.Interval,
            mORecord.Animation, mORecord.FacingEighth, mORecord.FacingResidual, mORecord.FineHeading);
    }

    /// <summary>
    /// After a conversation: what the script imported into the UWNpc (attitude, goal,
    /// target, ally bit, hit points - see UWConversationSession) is carried into the record,
    /// whose properties keep the two in step from then on. The original's scripts write the
    /// same fields (contract section 6).
    /// </summary>
    public void RefreshFromData()
    {
        if (mONpc == null || mORecord == null)
            return;

        if (mORecord.Attitude != mONpc.NPCAttitude)
            mORecord.Attitude = mONpc.NPCAttitude;

        if (mORecord.Goal != mONpc.NPCGoal)
            mORecord.Goal = mONpc.NPCGoal;

        if (mORecord.GTarg != mONpc.NPCGTarg)
            mORecord.GTarg = mONpc.NPCGTarg;

        if (mORecord.IsAlly != mONpc.NPCIsAlly)
            mORecord.IsAlly = mONpc.NPCIsAlly;

        // The body reads the record's hit points (cleanup step 4.8, 2026-10-18), so it follows by
        // itself (the golem's script sets 125 after he was beaten, 2026-09-28) - all 125 of them:
        // until then the body's copy stopped at its maximum, the larger of the table's and the
        // loaded hit points.
        if (mONpc.HitPoints > 0 && mORecord.HitPoints != mONpc.HitPoints)
            mORecord.HitPoints = mONpc.HitPoints;

        fTrace("refreshed from data: goal {0} gtarg {1} attitude {2}", mORecord.Goal, mORecord.GTarg, mORecord.Attitude);
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

    /// <summary>
    /// Called by UWCritterDriver once per frame after the level clock advanced: while the
    /// creature is due, the brain runs one update - the physics of the previous decision
    /// through RunMotion in the middle of it, then the mind - and the picture takes the
    /// result. The order is the original's (spec 1.4): the body moves with the values the
    /// LAST goal routine left in the record, then the mind runs.
    /// </summary>
    public void RunDueUpdates(UWCritterClock pOClock)
    {
        if (mOBrain == null || mbRemoved || pOClock == null)
            return;

        fEnsurePlayer();

        for (int liGuard = 0; liGuard < MaxUpdatesPerFrame && pOClock.IsDue(mORecord.DueSlot); liGuard++)
        {
            int liSlotBefore = mORecord.DueSlot;

            UWCritterBrain.Motion lOMotion = mOBrain.Update(this);

            fShowMotion(lOMotion);

            if (lOMotion.Removed)
            {
                mbRemoved = true;

                return;
            }

            // A record whose interval loaded as 0 would stay due for ever; one slot on is
            // the host's guard, not the original's (its interval field is never 0 in play).
            if (mORecord.DueSlot == liSlotBefore && !lOMotion.Culled)
                mORecord.DueSlot = UWCritterClock.NextSlot(liSlotBefore, 1);
        }
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
    private void fShowMotion(UWCritterBrain.Motion pOMotion)
    {
        if (pOMotion.Culled)
            return;

        UWCritterAnimator lOAnimator = GetComponent<UWCritterAnimator>();

        if (pOMotion.Animation >= 1 && pOMotion.Animation <= 3)
            miAttackIndex = pOMotion.Animation - 1;

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
    /// glided to its end - see fShowMotion.</summary>
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

    /// <summary>What the animator shows (the walk while a step still glides, see fShowMotion),
    /// and the decided picture waiting for the glide's end.</summary>
    private int miShownAnimation = -1;

    private int miShownFrame;

    private bool mbHeldMotion;

    private int miHeldAnimation;

    private int miHeldFrame;

    // ------------------------------------------------- The physics (contract section 3)

    /// <summary>
    /// The step of the previous decision on the motion core (UWCreatureMotion.Step, stage 2 of
    /// the motion rework, 2026-10-05), called by the brain in the middle of its update: the
    /// record moves - eighth, height, tile, heading byte, speed, gravity, pitch, contact
    /// state - and the body follows the record. The tile list is relinked when the tile
    /// changed. The lava burn of the write-back (1 in 5 per moving update) is 1 point of
    /// PLAIN fire on the UWDamageable, charged to nobody (attacker 0); the resistances of
    /// COMOBJ decide in UWHealth. The walls, doors, the player, other creatures, bridges,
    /// ledges, water and lava are all the core's now - the clearance probes and sweeps of the
    /// old substitute are gone.
    /// </summary>
    StepResult ICritterHost.RunMotion()
    {
        mOLastStep = default(StepResult);

        if (mORecord == null || mbRemoved || mOLevelLoader == null)
            return mOLastStep;

        UWMotionHost lOWorld = UWMotionHost.Ensure(mOLevelLoader);
        UWCreatureMotion lOMotion = lOWorld != null ? lOWorld.CreatureMotion : null;

        if (lOMotion == null)
            return mOLastStep;

        // The interval that has just elapsed: the picture glides over it.
        int liInterval = mORecord.Interval;

        mOLastStep = lOMotion.Step(mORecord, miIndex, mbHasStats ? UWCreatureMotion.KindOf(mOStats) : UWCreatureMotion.Kind.Land);

        // A LYING OBJECT in the collision scan carries a transient index of the world (4096 and
        // up); the brain's door actions and GetObject want the level's master-list index, as a
        // creature's index is. Found 2026-10-05 (goblins stood at closed doors without opening
        // them: UseDoor on the transient index found no door).
        if (mOLastStep.HitObject && UWMotionHost.IsStaticIndex(mOLastStep.HitObjectIndex))
        {
            UWObject lOHit = lOWorld.StaticObjectAt(mOLastStep.HitObjectIndex);

            mOLastStep.HitObjectIndex = lOHit != null ? fIndexOf(lOHit) : 0;
        }

        if (mOLastStep.TileChanged && mONpc != null)
            mOLevelLoader.MoveObjectData(mONpc, mORecord.TileX, mORecord.TileY);

        if (mOLastStep.LavaBurn)
            fDamage(UWWorld.TargetOf(this, mODamageable), UWBlow.Lava(1));

        if (mOLastStep.Collided)
            fTrace("step: collided{0}{1}{2}{3}{4}", mOLastStep.HitClosedDoor ? ", closed door" : mOLastStep.HitObject ? " with object " + mOLastStep.HitObjectIndex : "",
                mOLastStep.HeadingDeflected ? ", deflected to " + mORecord.FineHeading : "", mOLastStep.Stuck ? ", stuck" : "",
                mOLastStep.Drowned ? ", drowned" : "", mOLastStep.TouchedCeiling ? ", ceiling" : "");

        fPlaceBodyFromRecord(liInterval);

        return mOLastStep;
    }

    /// <summary>The body at the record's eighth and height - the picture glides there from
    /// where it is over the interval that has just elapsed (deviation 17, the user's decision
    /// to interpolate).</summary>
    private void fPlaceBodyFromRecord(int piInterval)
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
    private void fSetBodyNow()
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

    // ------------------------------------------------- ICritterHost: time and chance

    long ICritterHost.Clock => UWScene.CritterClock.Clock;

    /// <summary>The session's generator (cleanup step 2.8, 2026-10-14) - one sequence for the
    /// brain, combat, loot and the motion core, as the original has one rand.</summary>
    int ICritterHost.Random(int piN)
    {
        return piN <= 1 ? 0 : UWGameSession.Current.Random.Next(piN);
    }

    UWCritterAlarm ICritterHost.Alarm => UWScene.CritterAlarm;

    // ------------------------------------------------- ICritterHost: the level

    int ICritterHost.FloorLevelAt(int piTileX, int piTileY)
    {
        UWTile lOTile = fGetTileAt(piTileX, piTileY);

        return lOTile != null ? lOTile.FloorHeight >> 4 : 0;
    }

    /// <summary>The mind's question before a cast: the anti-magic bit alone, the Void left to the
    /// cast's refusal (UWEndgameRules.IsAntiMagicTile, cleanup step 4.10).</summary>
    bool ICritterHost.IsMagicBlockedAt(int piTileX, int piTileY)
    {
        return mOLevelLoader != null
            && UWEndgameRules.IsAntiMagicTile(mOLevelLoader.CurrentLevel, new UWTilePos(piTileX, piTileY));
    }

    /// <summary>Tybal's rule: his lair, level 7, while the orb stands (UWTybalOrbRules, the bit
    /// PLAYER.DAT 0x60 bit 5). Until 2026-09-23 this asked for level index 7 - level 8 - and
    /// never for the bit.</summary>
    int ICritterHost.DungeonLevel => mOLevelLoader != null ? mOLevelLoader.CurrentLevelIndex + 1 : 0;

    bool ICritterHost.TybalOrbStands =>mOLevelLoader != null && UWTybalOrbRules.OrbStands(mOLevelLoader.CurrentLevelIndex);

    int ICritterHost.OwnObjectHeight
    {
        get
        {
            int liRadius;
            int liHeight;
            fGetOwnSize(out liRadius, out liHeight);

            return liHeight;
        }
    }

    // ------------------------------------------------- ICritterHost: other objects

    CritterTarget ICritterHost.GetObject(int piIndex)
    {
        return piIndex == PlayerIndex ? fSnapshotPlayer() : fSnapshotObject(piIndex);
    }

    /// <summary>The remote camera's tile while it shows (camera trap, Roaming Sight), else the
    /// player's - see ICritterHost.ViewTile.</summary>
    UWTilePos ICritterHost.ViewTile
    {
        get
        {
            UWRemoteCamera lORemote = UWRemoteCamera.Current;

            if (lORemote != null && lORemote.IsActive && lORemote.ViewCamera != null)
            {
                Vector3 lOEye = lORemote.ViewCamera.transform.position;

                return UWTileQueries.WorldToTile(lOEye.x, lOEye.z);
            }

            if (!fEnsurePlayer())
                return new UWTilePos(-100, -100);

            return UWTileQueries.WorldToTile(mOPlayerTransform.position.x, mOPlayerTransform.position.z);
        }
    }

    /// <summary>The player as the brain sees him: position in eighths from the camera's
    /// horizontal position, feet in zpos, the facing eighth from the yaw, loudness and
    /// visibility nibbles as UWPlayerVitals keeps them (deviation 24), the drawn weapon from
    /// the interaction's combat mode (PlayerData 0x5F bit 1 in the original).</summary>
    private CritterTarget fSnapshotPlayer()
    {
        CritterTarget lOTarget = new CritterTarget();

        if (!fEnsurePlayer())
            return lOTarget;

        Vector3 lOAt = mOPlayerTransform.position;
        UWTilePos lOTile = UWTileQueries.WorldToTile(lOAt.x, lOAt.z);

        lOTarget.Exists = true;
        lOTarget.Index = PlayerIndex;
        lOTarget.IsPlayer = true;
        lOTarget.IsCreature = true;
        lOTarget.ItemId = UWCritterBrain.PlayerItemId;
        lOTarget.X = UWTileQueries.WorldToEighths(lOAt.x);
        lOTarget.Y = UWTileQueries.WorldToEighths(lOAt.z);
        lOTarget.ZPos = Mathf.Clamp(mOPlayer.GetFeetZPos(), 0, 0x7F);
        lOTarget.TileX = lOTile.X;
        lOTarget.TileY = lOTile.Y;
        lOTarget.ObjectHeight = mOPlayer.GetBodyHeightZ();
        lOTarget.FacingEighth = UWPlayerThrow.GetPlayerHeading(mOPlayerTransform) >> 5;
        lOTarget.Loudness = mOPlayer.Vitals.Quietness;
        lOTarget.Visibility = mOPlayer.Vitals.Visibility;
        lOTarget.HitPoints = mOPlayer.Vitals.CurrentHP > 0f ? Mathf.Max(1, (int)mOPlayer.Vitals.CurrentHP) : 0;
        lOTarget.Goal = 0;
        lOTarget.IsAlly = false;
        lOTarget.Kind = 0;

        Interaction lOInteraction = UWScene.Interaction;

        lOTarget.WeaponDrawn = lOInteraction != null && lOInteraction.IsCombatModeActive;

        return lOTarget;
    }

    /// <summary>Any other object of the master list by index: a creature from its host's
    /// record and table row, a door or item from its data.</summary>
    private CritterTarget fSnapshotObject(int piIndex)
    {
        CritterTarget lOTarget = new CritterTarget();
        UWObject lOObject = fObjectAt(piIndex);

        if (lOObject == null)
            return lOTarget;

        UWCritter lOOther = fCritterOf(lOObject);

        if (lOOther != null && lOOther.mORecord != null)
        {
            UWCritterRecord lORecord = lOOther.mORecord;

            lOTarget.Exists = !lOOther.mbRemoved;
            lOTarget.Index = piIndex;
            lOTarget.IsCreature = true;
            lOTarget.ItemId = lORecord.ItemId;
            lOTarget.X = lORecord.X;
            lOTarget.Y = lORecord.Y;
            lOTarget.ZPos = lORecord.ZPos;
            lOTarget.TileX = lORecord.TileX;
            lOTarget.TileY = lORecord.TileY;
            lOTarget.FacingEighth = lORecord.FacingEighth;
            lOTarget.HitPoints = lOOther.mODamageable != null && lOOther.mODamageable.IsDestroyed ? 0 : lORecord.HitPoints;
            lOTarget.Goal = lORecord.Goal;
            lOTarget.IsAlly = lORecord.IsAlly;

            int liRadius;
            int liHeight;
            lOOther.fGetOwnSize(out liRadius, out liHeight);
            lOTarget.ObjectHeight = liHeight;

            if (lOOther.mbHasStats)
            {
                lOTarget.Loudness = lOOther.mOStats.LoudnessAsTarget;
                lOTarget.Visibility = lOOther.mOStats.VisibilityAsTarget;
                lOTarget.Kind = lOOther.mOStats.GeneralType;
            }

            return lOTarget;
        }

        lOTarget.Exists = true;
        lOTarget.Index = piIndex;
        lOTarget.ItemId = lOObject.ID & 0x1FF;
        lOTarget.IsCreature = UWObjectIds.IsCreature(lOObject.ID);
        lOTarget.TileX = lOObject.TileX;
        lOTarget.TileY = lOObject.TileY;
        lOTarget.X = (lOObject.TileX << 3) + (lOObject.XPos & 7);
        lOTarget.Y = (lOObject.TileY << 3) + (lOObject.YPos & 7);
        lOTarget.ZPos = lOObject.ZPos & 0x7F;
        lOTarget.FacingEighth = lOObject.Heading & 7;
        lOTarget.HitPoints = 0;

        return lOTarget;
    }

    private UWObject fObjectAt(int piIndex)
    {
        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null || piIndex <= 0)
            return null;

        List<UWObject> lOList = mOLevelLoader.CurrentLevel.Masterlist;

        return lOList != null && piIndex < lOList.Count ? lOList[piIndex] : null;
    }

    private int fIndexOf(UWObject pOObject)
    {
        if (pOObject == null || mOLevelLoader == null || mOLevelLoader.CurrentLevel == null)
            return 0;

        int liIndex = mOLevelLoader.CurrentLevel.Masterlist.IndexOf(pOObject);

        return liIndex < 0 ? 0 : liIndex;
    }

    private UWCritter fCritterOf(UWObject pOObject)
    {
        UWEntityInfo lOEntity;

        if (mOLevelLoader == null || !mOLevelLoader.TryGetEntity(pOObject, out lOEntity) || lOEntity == null)
            return null;

        return lOEntity.GetComponentInParent<UWCritter>();
    }

    /// <summary>
    /// TestBetweenPoints (spec 3.1) over the tile map - the rule is engine-free in
    /// UWTilePath.TestBetweenPoints since 2026-09-23, with the heights and the closed sides of
    /// every tile the line passes, the diagonals included (deviation 23 closed). Doors are
    /// objects and do not block - that is how the goblin in front of the storage room notices a
    /// theft through a closed door (per user, 2026-09-16).
    /// </summary>
    bool ICritterHost.HasLineOfSight(int piX0, int piY0, int piZ0, int piX1, int piY1, int piZ1)
    {
        return fHasLineOfSight(piX0, piY0, piZ0, piX1, piY1, piZ1);
    }

    private bool fHasLineOfSight(int piX0, int piY0, int piZ0, int piX1, int piY1, int piZ1)
    {
        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null)
            return false;

        return UWTilePath.TestBetweenPoints(mOLevelLoader.CurrentLevel.TileData,
            piX0, piY0, piZ0, piX1, piY1, piZ1);
    }

    // ------------------------------------------------- ICritterHost: movement

    /// <summary>
    /// seg006_1477_1938: the straight tile line from the creature's tile to the destination,
    /// walked one axis at a time (the original tests the three tiles around every step of a
    /// Bresenham walk), each step through the creature's own passability - walker, flier or
    /// swimmer, water, lava against the range budget, closed doors when it could open them.
    /// </summary>
     /// <summary>
    /// The straight tile line seg006_1477_1938 of the original (UWTileRoute.StraightLine with
    /// this creature's triple test, since 2026-10-05): every tile of the line tested as the
    /// middle of a triple, the destination included, with budget 0 - so water on the way or at
    /// the destination fails it for a land creature, a drop of two levels fails it, a closed
    /// door across the way fails it. Until then the host's UWTilePath walked the line with its
    /// own per-tile rules (deviation 14).
    /// </summary>
    bool ICritterHost.IsStraightLineClear(int piToTileX, int piToTileY)
    {
        if (mORecord == null || !fPrepareRoute())
            return false;

        UWTile lOTile = fGetTileAt(mORecord.TileX, mORecord.TileY);
        int liStart = lOTile != null ? lOTile.FloorHeight >> 4 : mORecord.FloorLevel;

        // CLEAR ONLY WHEN CLEAR (crit_head_for_loc: beeline(...) == 1): the line to the own tile
        // (LineSameTile, -1) is none, which a creature arriving home meets (A20, 2026-10-15).
        return mORoute.StraightLine(mORecord.TileX, mORecord.TileY, piToTileX, piToTileY, liStart, mOTraverse.Triple)
            == UWTileRoute.LineClear;
    }

    private UWTileRoute mORoute;

    private UWTileTraverse mOTraverse;

    /// <summary>The level's shared route and triple test with this creature's context: the
    /// handler masks of its kind, its COMOBJ height, its jump bit (table byte 0x0A bit 5).</summary>
    private bool fPrepareRoute()
    {
        UWMotionHost lOWorld = mOLevelLoader != null ? UWMotionHost.Ensure(mOLevelLoader) : null;

        if (lOWorld == null || lOWorld.TileTraverse == null)
            return false;

        mORoute = lOWorld.TileRoute;
        mOTraverse = lOWorld.TileTraverse;

        int liRadius;
        int liHeight;
        fGetOwnSize(out liRadius, out liHeight);

        mOTraverse.SetCreature(mbHasStats ? UWCreatureMotion.KindOf(mOStats) : UWCreatureMotion.Kind.Land, liHeight,
            mbHasStats && (mOStats.RowByte(0x0A) & UWTileTraverse.JumpRowBit) != 0);

        return true;
    }

    /// <summary>
    /// PathFindBetweenTiles of the original (flood_path; UWTileRoute.FindPath with this creature's
    /// triple test, since 2026-10-05): the breadth-first search in the box with the range budget,
    /// the goal next to the start accepted untested, a farther goal tested as the middle of the
    /// final triple; the squares and their jump flags go to the brain, which stores them in one of
    /// the level's sixteen slots and follows them (since 2026-10-15; until then this answered the
    /// next tile and reused it for half a second, deviation 14). Every closed door blocks by its
    /// orientation whatever the creature's door skill (UWTileTraverse), as in the original.
    /// </summary>
    bool ICritterHost.TryFindPath(int piFromX, int piFromY, int piFromHeight, int piToTileX, int piToTileY, int piToHeight,
        int piRangeBudget, System.Collections.Generic.List<UWTilePos> pOSquares, System.Collections.Generic.List<bool> pOJumps)
    {
        pOSquares.Clear();
        pOJumps.Clear();

        if (mORecord == null || mOLevelLoader == null || !fPrepareRoute())
            return false;

        if (!mORoute.FindPath(piFromX, piFromY, piFromHeight, piToTileX, piToTileY, piToHeight,
            piRangeBudget, mOTraverse.Triple) || mORoute.Tiles.Count < 2)
            return false;

        pOSquares.AddRange(mORoute.Tiles);
        pOJumps.AddRange(mORoute.Jumps);

        return true;
    }

    /// <summary>The level's sixteen stored creature paths (the session's, cleared with every
    /// level build).</summary>
    UWCritterPathSlots ICritterHost.PathSlots => UWDataImport.UWData.UWGameSession.Current.CritterPaths;

    /// <summary>
    /// Goal 3's relocation: the record onto the tile centre at floor height, relinked, the body
    /// there at once. UW.EXE checks nothing; here a solid tile or one outside the map refuses
    /// (deviation 37, kept for safety) - a creature inside a wall would be worse than a skipped
    /// jump.
    ///
    /// ONE WRITE, HERE (cleanup step 4.8, 2026-10-18): seg007_1798_3E4 (UnderworldExhumed AI.C)
    /// moves the follower to the middle of the tile at the tile's height times eight, a flier too.
    /// Until then this wrote the record with a flier's height above the floor kept and the brain
    /// wrote it again at floor height after the call, so the record ended at the floor while the
    /// body showed the flier at its old height above it until the next step.
    /// </summary>
    bool ICritterHost.Teleport(int piTileX, int piTileY)
    {
        UWTile lOTile = fGetTileAt(piTileX, piTileY);

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
            return false;

        if (!mbFlying && mbSwimming != fIsWater(lOTile))
            return false;

        fRelocate(piTileX, piTileY, 4, 4, (lOTile.FloorHeight >> 4) << 3);
        fTrace("teleported to {0}/{1}", piTileX, piTileY);

        return true;
    }

    /// <summary>The record onto a tile, eighth and height, the tile list relinked, the body
    /// there at once.</summary>
    private void fRelocate(int piTileX, int piTileY, int piFineX, int piFineY, int piZPos)
    {
        mORecord.TileX = piTileX;
        mORecord.TileY = piTileY;
        mORecord.FineX = piFineX;
        mORecord.FineY = piFineY;
        mORecord.ZPos = piZPos;

        if (mOLevelLoader != null && mONpc != null)
            mOLevelLoader.MoveObjectData(mONpc, piTileX, piTileY);

        fSetBodyNow();
    }

    // ------------------------------------------------- ICritterHost: doors (spec 7.5)

    /// <summary>ObjectUse on the door: opens it unless it is locked or spiked (UWDoorRules.Use
    /// with the creature as the user - UseRect refuses a creature the spiked door, the player
    /// not). True when it is open afterwards.</summary>
    bool ICritterHost.UseDoor(int piDoorIndex)
    {
        UWObject lODoorData = fObjectAt(piDoorIndex);
        UWEntityInfo lOEntity;

        if (lODoorData == null || mOLevelLoader == null || !mOLevelLoader.TryGetEntity(lODoorData, out lOEntity) || lOEntity == null)
            return false;

        UWDoor lODoor = lOEntity.GetComponentInParent<UWDoor>();

        if (lODoor == null)
            return false;

        if (!lODoor.IsClosed)
            return true;

        if (UWDoorRules.Use(true, lODoor.IsSpiked, lODoor.IsLocked, false, false) != UWDoorRules.UseOutcome.Toggle)
            return false;

        lODoor.Toggle(UWTrapRules.Setter.Creature);
        fTrace("opened door {0}", piDoorIndex);

        return !lODoor.IsClosed;
    }

    /// <summary>
    /// UnlockDoor_seg040_352B_1D3B with minus the skill: a lockpick attempt by the creature,
    /// UWLockRules.Unlock with the pick path - read 2026-09-25: the skill against three times
    /// the lock difficulty, and a lock without its keep bit is gone afterwards.
    /// A SUCCESS ONLY UNLOCKS (try_to_open_door and checkLock, UnderworldExhumed PATHFIND.C and
    /// USEITEMS.C): no sound, and the door stays shut until the creature's next bump uses it.
    /// Until 2026-10-15 ours played the lockpick sound and opened the door at once (audit round
    /// 2, A19 / KR D19). checkLock's door trap (checkTrap with trigger type 6, an unlock trigger)
    /// has nothing to fire: no level of the game holds one (TRIAGE B12, 2026-10-15).
    /// </summary>
    void ICritterHost.PickDoor(int piDoorIndex, int piSkill)
    {
        UWObject lODoorData = fObjectAt(piDoorIndex);
        UWEntityInfo lOEntity;

        if (lODoorData == null || mOLevelLoader == null || mOLevelLoader.CurrentLevel == null
            || !mOLevelLoader.TryGetEntity(lODoorData, out lOEntity) || lOEntity == null)
            return;

        UWDoor lODoor = lOEntity.GetComponentInParent<UWDoor>();

        if (lODoor == null || !lODoor.IsLocked)
            return;

        bool lbUnlocked = UWLockRules.Unlock(lODoor.LockState, null, null, !lODoor.IsClosed, -piSkill)
            == UWLockRules.Outcome.Unlocked;

        fTrace("picks door {0}: {1}", piDoorIndex, lbUnlocked ? "unlocked" : "failed");
    }

    /// <summary>DamageObject type 4 on the door: its UWDamageable takes the bash - the spike of
    /// a spiked door first (UWDoor.TryDamageSpike through UWDamageable.TakesBlowFirst, B4) - and
    /// UWDoor bursts it open at zero.</summary>
    void ICritterHost.BashDoor(int piDoorIndex, int piDamage)
    {
        UWObject lODoorData = fObjectAt(piDoorIndex);
        UWEntityInfo lOEntity;

        if (lODoorData == null || mOLevelLoader == null || !mOLevelLoader.TryGetEntity(lODoorData, out lOEntity) || lOEntity == null)
            return;

        UWDoor lODoor = lOEntity.GetComponentInParent<UWDoor>();
        UWDamageable lODamageable = lODoor != null ? lODoor.Damageable : null;

        if (lODamageable == null)
            return;

        fTrace("bashes door {0} for {1}", piDoorIndex, piDamage);
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, lOEntity.transform.position, piDamage << 2);
        fDamage(UWWorld.TargetOf(lODamageable), UWBlow.CreatureBlow(piDamage, miIndex));
    }

    // ------------------------------------------------- ICritterHost: missiles

    int ICritterHost.MissileVelocity(int piAmmoIndex)
    {
        if (mOLevelLoader == null || mOLevelLoader.UWDataImporter == null
            || mOLevelLoader.UWDataImporter.ObjectProperties == null)
            return 15;

        return mOLevelLoader.UWDataImporter.ObjectProperties.GetRangedSpeed(FirstAmmunitionObjectId + piAmmoIndex);
    }

    // ------------------------------------------------- ICritterHost: events

    void ICritterHost.OnSwingStarted(int piAttack)
    {
        miAttackIndex = Mathf.Clamp(piAttack, 0, 2);
        fEvent("swing {0} started", piAttack);
    }

    /// <summary>
    /// The blow at frame 4 - NPCExecuteAttack_seg022_15DE (83437), spec 8.3. The reach scan
    /// of CheckForAttackHit_seg022_466 (80356) is a collision scan from the attacker along
    /// its facing over 2 + 3 = 5 eighths with radius 2 + 1 = 3, and the NEAREST collision is
    /// the defender - not necessarily the goal target. The host does it as a capsule probe
    /// over the body height (fScanForDefender): the player, a creature, or something of the
    /// world. Nothing in reach is a plain miss with sound 0x0A at the attacker (label C34);
    /// a wall in the way gets the impact animation as well (SpawnImpactAnimo). The
    /// arithmetic - score, skill check, critical, dice, charge, flank, armour, difficulty -
    /// is UWCritterCombat's. Creature against creature is refused for the same ally bit
    /// (labels DBB-E19). A miss with 0 damage still reaches the defender's OnDamaged, so it
    /// remembers the attacker.
    /// </summary>
    void ICritterHost.OnBlow(int piTargetIndex, int piAttack, int piCharge, int piSwingType, int piFlank)
    {
        miAttackIndex = Mathf.Clamp(piAttack, 0, 2);

        RaycastHit lOHit;
        UWCritter lODefender;
        bool lbPlayer;

        bool lbFound = fScanForDefender(out lOHit, out lODefender, out lbPlayer);

        // A DOOR NEARER THAN THAT IS THE DEFENDER (per user, 2026-09-29, SAVE3 of the original: a
        // goblin standing in the frame of an OPEN door strikes the door, not the player behind
        // it). The door is an object of COMOBJ radius 3 in its tile, open or closed; the open
        // door's colliders sit raised above the head (see UWObjectSpawner, OpenDoorRaise), so
        // the physics probe never sees it - the door is tested in the plane.
        float lfDefenderDistance = float.MaxValue;

        if (lbFound)
        {
            Vector3 lODefenderAt = lbPlayer && fEnsurePlayer() ? mOPlayerTransform.position
                : (lODefender != null ? lODefender.mOBody : lOHit.point);

            lfDefenderDistance = fFlatDistance(mOBody, lODefenderAt);
        }

        if (fTryDoorInReach(lfDefenderDistance, out UWDoor lODoor))
        {
            fStrikeObject(lODoor.Damageable, lODoor.Centre, piAttack, piCharge, piFlank);

            return;
        }

        if (!lbFound)
        {
            fEvent("blow {0}: nothing in reach", piAttack);
            UWSoundEffects.PlayAt(UWSoundEffects.Miss, mOBody);

            return;
        }

        if (lbPlayer)
        {
            fStrikePlayer(piAttack, piCharge, piFlank);

            return;
        }

        if (lODefender != null)
        {
            fStrikeCreature(lODefender.miIndex, lODefender, piAttack, piCharge, piFlank);

            return;
        }

        // AN OBJECT IN THE WAY IS THE DEFENDER (per user, 2026-09-29: a goblin striking at the
        // player through an open door knocked the door from quality 40 to 33 in the original,
        // ours never touched it). The nearest collision is the defender whatever it is;
        // CalculateAttackResults_seg022_230E_6B9 rolls no check against an object that is not a
        // creature, and AttackerAppliesFinalDamage_seg022_8A5 subtracts armour only for
        // creatures - the rolled damage, charge and flank go into DamageObject with type 4.
        UWDamageable lOObject = lOHit.collider != null ? lOHit.collider.GetComponentInParent<UWDamageable>() : null;

        // A frame post or a lintel belongs to its door (UWEntityInfo.PartOf, as for missiles).
        UWEntityInfo lOHitInfo = lOHit.collider != null ? lOHit.collider.GetComponentInParent<UWEntityInfo>() : null;

        if (lOObject == null && lOHitInfo != null && lOHitInfo.PartOf != null)
            lOObject = lOHitInfo.PartOf.GetComponent<UWDamageable>();

        if (lOObject != null && lOObject.TakesBlows && lOObject.GetComponentInParent<UWCritter>() == null)
        {
            fStrikeObject(lOObject, lOHit.point, piAttack, piCharge, piFlank);

            return;
        }

        // The world is in the way: the impact on it, then the miss (CheckForAttackHit's wall
        // branch and CombatMissImpactSound(0)). The lurker in the water striking at the bank
        // shows the same flash as the player's blow on a barrel (per user, 2026-09-16).
        fEvent("blow {0}: hits the world at {1}", piAttack, lOHit.point);
        fShowImpactOnWorld(lOHit);
        UWSoundEffects.PlayAt(UWSoundEffects.Miss, mOBody);
    }

    /// <summary>
    /// A blow on an object that is no creature - a door, a chest, a barrel: no check
    /// (CalculateAttackResults_seg022_230E_6B9 rolls none against an object) and no armour
    /// (AttackerAppliesFinalDamage_seg022_8A5 subtracts it only for creatures); the rolled
    /// damage, charge and flank go into DamageObject with type 4. A door without hit points
    /// (the portcullis) takes the blow and nothing happens to it. The numbers are
    /// UWAttackResolution's, a thing as the defender (cleanup step 4.9, 2026-10-18).
    /// </summary>
    private void fStrikeObject(UWDamageable pOObject, Vector3 pOAt, int piAttack, int piCharge, int piFlank)
    {
        UWAttackOutcome lOOutcome = UWAttackResolution.Resolve(fGetAttacker(piAttack, piCharge, piFlank),
            UWAttackResolution.ObjectDefender, UWAttackResolution.NoPart);

        fEvent("blow {0}: hits {1} for {2}", piAttack, pOObject != null ? pOObject.name : "an object", lOOutcome.Rolled);

        // THE IMPACT AND THE SOUND AT THE HEIGHT OF THE BLOW (per user, 2026-09-29: the door was
        // hit without either). The original plays sound 4 at the object with volume damage * 4
        // and puts the impact animation 0x1CB where the blow met it, at the blow's height and at
        // least 2 above the door's own base. A door object stands at height 0 - its mesh carries
        // the floor - so the point takes the creature's eye height and moves a little towards it
        // to be seen in front of the leaf.
        Vector3 lOEye = fGetEyePosition();
        Vector3 lOAt = new Vector3(pOAt.x, lOEye.y, pOAt.z);

        // A DOOR lifts the impact to its own height + 2 (AttackerAppliesFinalDamage, door
        // branch before the impact): an open door object stands 24 above its floor, so the flash
        // shows at the underside of the lintel (per user, 2026-09-29, in the original).
        UWDoor lODoor = pOObject != null ? pOObject.GetComponent<UWDoor>() : null;

        if (lODoor != null && mOLevelLoader != null)
        {
            int liDoorZ = (!lODoor.IsClosed ? OpenDoorRaiseZ : 0) + DoorImpactAboveZ;

            lOAt.y = Mathf.Max(lOAt.y, mOLevelLoader.GetFloorHeightAt(pOAt) + (liDoorZ * UWWorldScale.ZPosStep));
        }

        // TOWARDS THE VIEWER, not the attacker (per user, 2026-09-29: in the original the
        // impact on a door is always seen, even with the goblin on the far side of the lintel -
        // as if the player had struck the door himself). Moved to the attacker's side it hid
        // behind the leaf and the lintel.
        Camera lOCamera = Camera.main;
        Vector3 lOViewer = lOCamera != null ? lOCamera.transform.position : lOEye;
        Vector3 lOTowards = new Vector3(lOViewer.x - lOAt.x, 0f, lOViewer.z - lOAt.z);

        if (lOTowards.sqrMagnitude > 0.01f)
            lOAt += lOTowards.normalized * Mathf.Min(ObjectImpactSetback, lOTowards.magnitude);

        if (mOLevelLoader != null)
            mOLevelLoader.SpawnEffectAt(UWObjectMechanics.FlashEffectObjectId, lOAt, BlowEffectSeconds);

        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, lOAt, lOOutcome.HitVolume);

        if (pOObject != null && pOObject.TakesBlows)
            fDamage(UWWorld.TargetOf(pOObject), lOOutcome.Blow);
    }

    /// <summary>COMOBJ radius of every door, open or closed, portcullis included (0x140-0x14F).</summary>
    private const int DoorRadiusEighths = 3;

    /// <summary>An open door object stands this much above its floor, in zpos (UWWorldCapture,
    /// the original's data).</summary>
    private const int OpenDoorRaiseZ = 24;

    /// <summary>The impact on a door sits this much above the door object, in zpos.</summary>
    private const int DoorImpactAboveZ = 2;

    /// <summary>World units the impact on an object is moved towards the viewer - 2 per user,
    /// 2026-09-29 (4, the setback on the world, was tried first).</summary>
    private const float ObjectImpactSetback = 2f;

    /// <summary>
    /// The nearest door the blow's probe meets in the plane: the probe of radius 3 eighths
    /// moved 5 eighths along the facing from the creature's centre (CheckForAttackHit_seg022_466)
    /// against the door object of radius 3 at its centre, on the creature's floor. Nearer than
    /// pfLimit (the distance of what the physics probe found), else none.
    /// </summary>
    private bool fTryDoorInReach(float pfLimit, out UWDoor pODoor)
    {
        pODoor = null;

        Vector3 lODirection = UWViewpoint.HeadingByteToDirection(mORecord.FullFacing);
        float lfReach = UWCritterRules.AttackReachEighths * UWWorldScale.SubTileStep;
        float lfTouch = (UWCritterRules.AttackReachRadius + DoorRadiusEighths) * UWWorldScale.SubTileStep;
        float lfBest = pfLimit;

        foreach (UWDoor lOCandidate in UWDoor.All)
        {
            // The door object stands at height 0 - its mesh carries the floor - so its floor is
            // the tile's.
            if (lOCandidate == null || mOLevelLoader == null
                || Mathf.Abs(mOLevelLoader.GetFloorHeightAt(lOCandidate.Centre) - mOBody.y) > UWWorldScale.TileSize)
                continue;

            Vector3 lOOffset = lOCandidate.Centre - mOBody;

            lOOffset.y = 0f;

            float lfAlong = Mathf.Clamp(Vector3.Dot(lOOffset, lODirection), 0f, lfReach);

            if ((lOOffset - (lODirection * lfAlong)).magnitude > lfTouch)
                continue;

            float lfDistance = lOOffset.magnitude;

            if (lfDistance >= lfBest)
                continue;

            lfBest = lfDistance;
            pODoor = lOCandidate;
        }

        return pODoor != null;
    }

    private static float fFlatDistance(Vector3 pOA, Vector3 pOB)
    {
        return new Vector2(pOA.x - pOB.x, pOA.z - pOB.z).magnitude;
    }

    /// <summary>
    /// The reach scan as a capsule probe: from the creature's feet to its COMOBJ height,
    /// radius 3 eighths, cast 5 eighths along the fine heading (UWCritterRules.AttackReach*,
    /// one eighth = UWWorldScale.SubTileStep world units), sprites and triggers ignored, the
    /// creature's own colliders skipped. True with the nearest hit: the player, a creature,
    /// or the world (both out parameters null). The original's height band per swing type
    /// (attacker zpos + height * (swingType / 3) / 3, label 4CE) is replaced by the whole
    /// body: what the band chose is what PickBodyPart chooses from the heights below.
    /// </summary>
    private bool fScanForDefender(out RaycastHit pOHit, out UWCritter pODefender, out bool pbPlayer)
    {
        pOHit = default;
        pODefender = null;
        pbPlayer = false;

        int liOwnRadius;
        int liOwnHeight;
        fGetOwnSize(out liOwnRadius, out liOwnHeight);

        float lfRadius = UWCritterRules.AttackReachRadius * UWWorldScale.SubTileStep;
        float lfHeight = Mathf.Max(liOwnHeight * UWWorldScale.ZPosStep, 2f * lfRadius);

        // THE BLOW GOES WHERE THE CREATURE LOOKS, not where its feet carry it. The original
        // hands GetCoordinateInDirection the FULL FACING - word 2 bits 7-9 times 32 plus byte
        // 0x18 bits 0-4 - not the movement heading of byte 9 (CheckForAttackHit_seg022_466,
        // labels 581-5B3). The two differ exactly in melee: a creature that is too close backs
        // off, so its feet point AWAY from the player while it faces and strikes him. We
        // searched along the feet and found nothing, which is why a reaper standing right next
        // to the player reported "nothing in reach" on every swing (per user, 2026-09-20).
        Vector3 lODirection = UWViewpoint.HeadingByteToDirection(mORecord.FullFacing);
        Vector3 lOFeet = mOBody + (Vector3.up * lfRadius);
        Vector3 lOTop = mOBody + (Vector3.up * (lfHeight - lfRadius));

        // A BODY THAT ALREADY TOUCHES THE PROBE IS THE DEFENDER, and it has to be looked for
        // separately: Unity's sweeps do not report a collider that overlaps the capsule at its
        // start. The original moves a body of this radius FROM the attacker's centre
        // (GetCoordinateInDirection, then ScanForCollisions_seg026_BF6), so whoever stands
        // against a WIDE creature collides on the first step and sorts first. Without this a
        // reaper never reached the player while the original hit him from the same spot (per
        // user, 2026-09-20: "nothing in reach" where the original lands its blows).
        Collider[] lOTouching = Physics.OverlapCapsule(lOFeet, lOTop, lfRadius,
            SweepLayerMask, QueryTriggerInteraction.Ignore);

        float lfNearestCentre = float.MaxValue;
        bool lbTouched = false;

        foreach (Collider lOCandidate in lOTouching)
        {
            UWCritter lOTouchingCritter;
            bool lbTouchingPlayer;

            if (!fIsDefenderCollider(lOCandidate, out lOTouchingCritter, out lbTouchingPlayer))
                continue;

            float lfCentre = Vector3.Distance(mOBody, lOCandidate.bounds.center);

            if (lbTouched && lfCentre >= lfNearestCentre)
                continue;

            lfNearestCentre = lfCentre;
            pODefender = lOTouchingCritter;
            pbPlayer = lbTouchingPlayer;
            lbTouched = true;
        }

        if (lbTouched)
            return true;

        RaycastHit[] lOHits = Physics.CapsuleCastAll(lOFeet, lOTop, lfRadius, lODirection,
            UWCritterRules.AttackReachEighths * UWWorldScale.SubTileStep, SweepLayerMask,
            QueryTriggerInteraction.Ignore);

        bool lbFound = false;
        bool lbFoundBody = false;
        float lfNearest = float.MaxValue;

        foreach (RaycastHit lOCandidate in lOHits)
        {
            if (lOCandidate.collider == null || lOCandidate.distance > lfNearest)
                continue;

            // A projectile or an effect in flight is neither a defender nor an obstacle
            // (UWProjectileFlight skips them the same way).
            if (lOCandidate.collider.GetComponentInParent<UWProjectileFlight>() != null)
                continue;

            UWCritter lOCritter;
            bool lbPlayerBody;
            bool lbBody = fIsDefenderCollider(lOCandidate.collider, out lOCritter, out lbPlayerBody);

            // A creature that is dead, this creature itself, or something in flight is no
            // defender - and no obstacle either (fIsDefenderCollider).
            if (lOCritter != null && !lbBody)
                continue;

            // An item on the floor (sprite layer, no body) is neither defender nor obstacle.
            if (!lbBody && lOCandidate.collider.gameObject.layer == SpriteLayer)
                continue;

            // THE FLOOR IS NOT IN THE WAY. The capsule starts at the feet, so the floor (and
            // on a slope the tile the creature stands on) overlaps its start and reports
            // distance 0 with no point - and won every blow against the player, who stood a
            // few units further ("hits the world" on every strike, never any damage, per user
            // 2026-09-20). The original's scan starts inside the attacker's own body, never
            // inside a wall or a floor: an overlap at the start and any floor or ceiling
            // (a mostly vertical normal) count for nothing unless they are a body.
            if (!lbBody && (lOCandidate.distance <= 0f || Mathf.Abs(lOCandidate.normal.y) > 0.5f))
                continue;

            // A collider that overlaps the capsule's start reports distance 0 for a body and a
            // wall alike; on that tie the body is the defender, as the original's scan finds
            // the creature before the tile behind it.
            if (lOCandidate.distance == lfNearest && (lbFoundBody || !lbBody))
                continue;

            lfNearest = lOCandidate.distance;
            pOHit = lOCandidate;
            pODefender = lOCritter;
            pbPlayer = lbPlayerBody;
            lbFound = true;
            lbFoundBody = lbBody;
        }

        return lbFound;
    }

    /// <summary>
    /// Is this collider a body the blow can land on - a living creature other than this one,
    /// or the player? False for walls, floors, doors and anything in flight.
    /// </summary>
    private bool fIsDefenderCollider(Collider pOCollider, out UWCritter pOCritter, out bool pbPlayer)
    {
        pOCritter = pOCollider != null ? pOCollider.GetComponentInParent<UWCritter>() : null;
        pbPlayer = false;

        if (pOCollider == null || pOCritter == this)
            return false;

        if (pOCollider.GetComponentInParent<UWProjectileFlight>() != null)
            return false;

        if (pOCritter != null)
            return pOCritter.mODamageable != null && !pOCritter.mODamageable.IsDestroyed;

        pbPlayer = fIsPlayerCollider(pOCollider);

        return pbPlayer;
    }

    /// <summary>The impact flash and hit sound where the blow met the world. A collider that
    /// overlapped the capsule's start has no point (Unity reports zero); the impact then sits
    /// one radius ahead of the creature.</summary>
    private void fShowImpactOnWorld(RaycastHit pOHit)
    {
        if (mOLevelLoader == null)
            return;

        Vector3 lOAt = pOHit.distance > 0f
            ? pOHit.point + (pOHit.normal * BlowEffectSetback)
            : fGetEyePosition() + (UWViewpoint.HeadingByteToDirection(mORecord.FullFacing)
                * (UWCritterRules.AttackReachRadius * UWWorldScale.SubTileStep));

        mOLevelLoader.SpawnEffectAt(UWObjectMechanics.FlashEffectObjectId, lOAt, BlowEffectSeconds);
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, lOAt);
    }

    /// <summary>The score and the base damage of this attack (NPCExecuteAttack labels
    /// 1629-1679): the table's to-hit and damage bytes of attack i, half the signed byte
    /// 0x11 and a fifth of byte 5, the strong individual's 7 + RNG(6) and 4 + RNG(12) - with the
    /// charge, the flank, this creature's index and its row's kinds and poison as one attacker for
    /// UWAttackResolution (UWAttackerStats.Creature, cleanup step 4.9, 2026-10-18;
    /// fGetAttackNumbers until then, the same draws).</summary>
    private UWAttackerStats fGetAttacker(int piAttack, int piCharge, int piFlank)
    {
        return UWAttackerStats.Creature(mOStats, mbHasStats, piAttack, mbHasStats && mORecord.IsStrong, miIndex,
            piCharge, piFlank);
    }

    /// <summary>This creature's blow on a body - the player or another creature - whose feet and
    /// top (zpos) are given: the ninth, the strong bonus, the part, the check, in the original's
    /// order (UWAttackResolution.CreatureBlow, cleanup step 4.9b, 2026-10-18).</summary>
    private UWAttackOutcome fStrikeBody(int piAttack, int piCharge, int piFlank, int piOwnHeight, IUWAttackDefender pIDefender,
        int piDefenderFeet, int piDefenderTop, out UWAttackerStats pOAttacker)
    {
        return UWAttackResolution.CreatureBlow(mOStats, mbHasStats, piAttack, mbHasStats && mORecord.IsStrong, miIndex,
            piCharge, piFlank, mORecord.ZPos, piOwnHeight, pIDefender, piDefenderFeet, piDefenderTop, out pOAttacker);
    }

    /// <summary>
    /// The blow against the player, in the original's order (CalculateAttackResults 80688,
    /// AttackerAppliesFinalDamage 81050): the part from the heights, the enchantment
    /// protection of that part off the score, the skill check against his defence with
    /// the flank bonus; a critical doubles the base 1 in 2 and flashes red (0xB8); the dice,
    /// the charge over 128 plus the flank; the hit sound with THAT value; his armour of the
    /// part; half on the easy difficulty; then the damage, the shake (in the vitals) and
    /// the poison. A miss is DamageObject with 0 on the player, i.e. nothing.
    /// </summary>
    private void fStrikePlayer(int piAttack, int piCharge, int piFlank)
    {
        if (!fEnsurePlayer())
            return;

        mOPlayer.RefreshStatus();

        int liOwnRadius;
        int liOwnHeight;
        fGetOwnSize(out liOwnRadius, out liOwnHeight);

        int liPlayerFeet = mOPlayer.GetFeetZPos();

        // THE PART FROM THE BLOW'S PROBE, a random third of this body (critter_attack's wsize 2
        // and the ninth rand() % 9 from AI.C, into resolve_attack and pickloc) - C3, by the user's
        // decision of 2026-10-12; until then this whole body, which sent tall creatures' blows
        // at the head far less often than the original does.
        //
        // THE CHECK, THE CRITICAL AND THE DICE are UWAttackResolution's since cleanup step 4.9
        // (2026-10-18), the player's defence and protection of the part UWPlayerCombatant's
        // (UWCharacter.Combatant): the check value is his row byte 0x12, his protection of the
        // part comes off the score; a critical doubles the base 1 in 2 and wears the piece of the
        // part (UWPlayerCombatant.OnCriticalHit) before the dice.
        //
        // IN THE ORIGINAL'S ORDER since cleanup step 4.9b (2026-10-18, by the user's decision): the
        // ninth, the strong creature's bonus, the part, the check (UWAttackResolution.CreatureBlow;
        // ours drew the part before the strong bonus until then).
        UWPlayerCombatant lOPlayerBody = mOPlayer.Combatant;
        UWAttackOutcome lOOutcome = fStrikeBody(piAttack, piCharge, piFlank, liOwnHeight, lOPlayerBody, liPlayerFeet,
            liPlayerFeet + mOPlayer.GetBodyHeightZ(), out UWAttackerStats lOAttacker);
        int liPart = lOOutcome.Part;

        if (!lOOutcome.Hit)
        {
            fEvent("blow {0} {1} (score {2} + flank {3} vs defence {4})", piAttack,
                lOOutcome.Result == UWSkillCheck.ResultEnum.CriticalFailure ? "missed badly" : "missed",
                lOAttacker.Score, piFlank, lOPlayerBody.Defence);

            // Blocked: 7 or 8 at the player, by this creature's weapon kind and the piece he wears
            // on the struck part (UWCritterCombat.GetBlockedSound, through UWAttackResolution).
            UWSoundEffects.PlayAt(lOOutcome.BlockedSound, mOPlayer.transform.position);

            // A MISS STILL STARTS THE COMBAT MUSIC: do_attack hands the player damage_item with
            // 0 (COMBAT.C), and damage_critter picks MUSIC_DANGER or MUSIC_COMBAT for the player
            // whenever the attacker is known (critter/AI.C). Until 2026-10-12 only a hit did.
            // Through the world as the miss's request since cleanup step 4.2 (2026-10-17;
            // UWDamageRequest.CreatureMiss, the outcome's blow since step 4.9).
            fDamage(UWDamageTarget.ThePlayer, lOOutcome.Blow);

            return;
        }

        // THE RED FLASH (0xB8) comes with the damage request (UWDamageRequest.CreatureBlow,
        // cleanup step 4.2, 2026-10-17); it was flashed here until then.

        // Sound 3 at the avatar, volume damage * 4 - before the armour, so an absorbed blow
        // still sounds (label 938).
        UWSoundEffects.PlayAtAvatar(UWSoundEffects.HitPlayer, lOOutcome.HitVolume);

        fEvent("blow {0} {1} part {2} for {3} before the armour (charge {4}, flank {5})", piAttack,
            lOOutcome.Critical ? "critical" : "hit", liPart, lOOutcome.Rolled, piCharge, piFlank);

        // A BLOW IS BLUNT DAMAGE: DamageObject with type 4 (label A3D). The armour of the part, half
        // on the easy difficulty, the combat shake from the damage before the Easy halving (C5,
        // 2026-10-12), the critical's flash and the combat music - all in the damage pipeline
        // since cleanup step 4.2 (2026-10-17; UWDamageRequest.CreatureBlow, which the outcome's
        // blow carries since step 4.9).
        fDamage(UWDamageTarget.ThePlayer, lOOutcome.Blow);

        fTryPoisonPlayer(lOAttacker, lOOutcome);
    }

    /// <summary>
    /// The blow against another creature: refused for the same ally bit (ExecuteAttack
    /// labels DBB-E19), else the same path as against the player without enchantment
    /// protection, flash, difficulty or poison: the skill check against its row byte 0x12,
    /// the dice, the charge and flank, sound 4 at the object with the pre-armour value, the
    /// armour of the part with the strong defender's 5/3, then DamageObject charged to this
    /// creature's index - with 0 on a miss, so the defender still records the attacker. A
    /// damaging hit leaves the blood splat or the flash at HitZ of the part (labels B49-C17).
    /// </summary>
    private void fStrikeCreature(int piTargetIndex, UWCritter pOOther, int piAttack, int piCharge, int piFlank)
    {
        if (pOOther == null || pOOther.mODamageable == null || pOOther.mODamageable.IsDestroyed)
            return;

        // The same side is spared (UWAttackResolution.SparesSameSide, do_attack).
        if (pOOther.mORecord != null && UWAttackResolution.SparesSameSide(false, UWDefenderKind.Creature,
            mORecord.IsAlly, pOOther.mORecord.IsAlly))
        {
            fEvent("blow {0} refused: same side as {1}", piAttack, piTargetIndex);

            return;
        }

        int liOwnRadius;
        int liOwnHeight;
        fGetOwnSize(out liOwnRadius, out liOwnHeight);

        // THE PART BEFORE THE CHECK, as the original's resolve_attack picks it before frp_check
        // (since cleanup step 4.9b, 2026-10-18, by the user's decision; ours rolled the check first
        // and the probe and the part after it until then): the blow's probe, a random third of this
        // body (C3, 2026-10-12, as in fStrikePlayer), the strong creature's bonus, the part, then the
        // check (UWAttackResolution.CreatureBlow). A hit: the critical, the dice, the charge and the
        // flank, then the armour of the part with the strong defender's 5/3 (pOOther as
        // IUWAttackDefender).
        int liOtherRadius;
        int liOtherHeight;
        pOOther.fGetOwnSize(out liOtherRadius, out liOtherHeight);

        UWAttackOutcome lOOutcome = fStrikeBody(piAttack, piCharge, piFlank, liOwnHeight, pOOther, pOOther.mORecord.ZPos,
            pOOther.mORecord.ZPos + liOtherHeight, out _);
        int liPart = lOOutcome.Part;
        UWSkillCheck.ResultEnum leResult = lOOutcome.Result;

        if (lOOutcome.Hit)
            UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, pOOther.mOBody, lOOutcome.HitVolume);
        else
        {
            // Blocked: 7 or 8 at the defender - the armour kind read from THIS creature's row, as
            // do_miss does (UWCritterCombat.GetBlockedSound, through UWAttackResolution).
            UWSoundEffects.PlayAt(lOOutcome.BlockedSound, pOOther.mOBody);
        }

        fEvent("blow {0} at creature {1}: {2} part {3} for {4}", piAttack, piTargetIndex, leResult, liPart, lOOutcome.Damage);

        // Charged to this creature (UWBlow.CreatureBlow), 0 on a miss.
        fDamage(UWWorld.TargetOf(pOOther, pOOther.mODamageable), lOOutcome.Blow);

        if (lOOutcome.LeavesTrace)
            pOOther.fShowHitEffect(liPart, lOOutcome.Damage);
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
    private void fShowHitEffect(int piPart, int piDamage, int piMissileZ = 0)
    {
        if (mOLevelLoader == null || !mbHasStats)
            return;

        float lfHeight = GetHitEffectHeight(piPart, piMissileZ);

        mOLevelLoader.SpawnEffectAt(UWCritterCombat.GetHitEffectObjectId(mOStats),
            mOBody + (Vector3.up * lfHeight), BlowEffectSeconds);
    }

    /// <summary>The height of the blood or flash above this creature's feet, in world units:
    /// HitZ of the part (a part without armour of its own the body's) in eighths of the body, or
    /// a missile's own height piMissileZ (zpos) for a missile's part - see fShowHitEffect.</summary>
    public float GetHitEffectHeight(int piPart, int piMissileZ = 0)
    {
        if (!mbHasStats)
            return 0f;

        int liRadius;
        int liHeight;
        fGetOwnSize(out liRadius, out liHeight);

        int liPart = UWCritterCombat.GetEffectPart(mOStats, piPart);

        if (liPart >= UWCritterCombat.MissilePartOffset)
            return piMissileZ > 0 ? (piMissileZ - mORecord.ZPos) * UWWorldScale.ZPosStep : 0f;

        return liHeight * UWWorldScale.ZPosStep * UWCritterCombat.GetHitZ(liPart) / 8f;
    }

    /// <summary>The part a blow whose probe spans these heights (zpos) hits on this body -
    /// pickloc (UWArmourProtection.PickBodyPart); the player's blow asks it with its probe.</summary>
    public int PickPartHitBy(int piProbeBottom, int piProbeTop)
    {
        return fPickPartHitBy(piProbeBottom, piProbeTop);
    }

    /// <summary>PickBodyHitPoint over this body and the attacker's heights in zpos.</summary>
    private int fPickPartHitBy(int piAttackerFeet, int piAttackerTop)
    {
        int liRadius;
        int liHeight;
        fGetOwnSize(out liRadius, out liHeight);

        return UWArmourProtection.PickBodyPart(mORecord.ZPos, mORecord.ZPos + liHeight, piAttackerFeet, piAttackerTop);
    }

    /// <summary>The armour of one part of this creature: row[part % 4], a 0xFF byte falling
    /// back to byte 0, times 5 / 3 for a strong individual (AttackerAppliesFinalDamage labels
    /// 990-A02). A creature without table data has none.</summary>
    public int GetArmourOfPart(int piPart)
    {
        return mbHasStats ? UWCritterCombat.GetCreatureArmour(mOStats, piPart, mORecord != null && mORecord.IsStrong) : 0;
    }

    /// <summary>
    /// The part a missile from these heights (zpos, the projectile's z and top) strikes on this
    /// body: the part PickBodyHitPoint picks, + 4 for a missile (MissileAttackHit label 158B).
    /// Its armour (GetArmourOfPart, this creature as IUWAttackDefender) replaces the
    /// UWDamageable's single value for projectiles (UWMissiles.StrikeCreature) - deviation 49.
    /// PICKED ONCE for the armour and the effect's place, as missile_thwack picks hitloc once
    /// (UnderworldExhumed combat/COMBAT.C:772; cleanup step 4.9b, 2026-10-18, by the user's
    /// decision to follow the original): until then GetArmourAgainst and ShowHitEffect each
    /// picked it, and the blood could sit at another part than the one whose armour counted.
    /// </summary>
    public int PickMissilePart(int piMissileZ, int piMissileTop)
    {
        return UWCritterCombat.GetMissileBodyPart(fPickPartHitBy(piMissileZ, piMissileTop));
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
        fShowHitEffect(piPart, piDamage, piMissileZ);
    }

    /// <summary>
    /// The poison of a hit on the player (NPCExecuteAttack labels 16A4-16EC, deviation 48):
    /// after ExecuteAttack returned 1 - a hit, even one the armour swallowed - his poison
    /// nibble must be below the row's byte 0x0F and a Poison Resistance must not scale a
    /// test damage of type 0x10 to zero; then the nibble becomes byte 0x0F &amp; 0xF. No
    /// roll, no armour hurdle. (UWAttackResolution.PoisonNibble since cleanup step 4.9,
    /// 2026-10-18, with the row's byte in the attacker.)
    /// </summary>
    private void fTryPoisonPlayer(in UWAttackerStats pOAttacker, in UWAttackOutcome pOOutcome)
    {
        int liNibble = UWAttackResolution.PoisonNibble(pOAttacker, pOOutcome, mOPlayer.Vitals.Poison, mOPlayer.DamageTypeProof);

        if (liNibble < 0)
            return;

        fEvent("poisons the player with {0}", liNibble);
        mOPlayer.Vitals.ApplyPoison(liNibble);
    }

    // ------------------------------------------------- IUWAttackDefender (cleanup step 4.9)

    /// <summary>This creature as the defender of a blow (UWAttackResolution, cleanup step 4.9,
    /// 2026-10-18): a creature.</summary>
    UWDefenderKind IUWAttackDefender.DefenderKind => UWDefenderKind.Creature;

    /// <summary>Its row byte 0x12 (frp_check's cr->defence) - the body's Defence without table
    /// data, as the creature's blow read it (the player's blow read the body's, which the spawner
    /// fills from the same byte).</summary>
    int IUWAttackDefender.Defence => mbHasStats ? mOStats.DefensePower : (mODamageable != null ? mODamageable.Defence : 0);

    /// <summary>A creature has no enchantment protection (frp_check asks the player's only).</summary>
    int IUWAttackDefender.ProtectionAt(int piPart) => 0;

    /// <summary>GetArmourOfPart. A part that was never picked (the player's blow without a
    /// character to measure the probe from) keeps the body's one value, as that blow did.</summary>
    int IUWAttackDefender.ArmourAt(int piPart)
    {
        if (piPart < 0)
            return mODamageable != null ? mODamageable.Armour : 0;

        return GetArmourOfPart(piPart);
    }

    /// <summary>Never asked: do_miss reads the ATTACKER's row for a creature as target.</summary>
    bool IUWAttackDefender.IsArmourMetalAt(int piPart) => false;

    /// <summary>Nothing of a creature wears on a critical hit (frp_check: the player only).</summary>
    void IUWAttackDefender.OnCriticalHit(int piPart)
    {
    }

    /// <summary>
    /// NPCMissileLaunch at frame 4 of animation 5: the ammunition of the table (object 0x10 +
    /// ammo) from the creature's eye, aimed at its target. The damage is rolled with charge
    /// 0x80 and flank 0 (deviation 46). The original computes the pitch from the height
    /// difference over the missile's velocity; the port aims the same way it did before -
    /// straight at the target with the drop of a ballistic missile added - so the brain's
    /// pitch is only traced here (TODO: use it once the projectile flies in the original.s units);
    /// a SPELL missile uses it since 2026-09-27, see fGetSpellLaunch.
    /// </summary>
    void ICritterHost.OnMissileLaunched(int piAmmoIndex, int piPitch)
    {
        fEvent("missile {0} launched, pitch {1}", piAmmoIndex, piPitch);

        CritterTarget lOTarget = ((ICritterHost)this).GetObject(mORecord.GTarg);

        if (!lOTarget.Exists)
            return;

        fLaunchProjectile(FirstAmmunitionObjectId + piAmmoIndex, UWDamageTypes.Missile, piPitch);
    }

    /// <summary>
    /// SpellTrapWandCast at frame 4 of animation 0x0D. Until 2026-09-27 ONLY THE MISSILE SPELLS
    /// WERE BUILT (major class 5): magic missile, lightning, fireball and acid cover imp,
    /// gazer, mage, fire elemental and acid slug; the area and special spells of trolls and
    /// wisps were left for the port's spell host.
    ///
    /// THROUGH THE ONE EFFECT PATH since cleanup step 4.10 (2026-10-18): critter_ai casts
    /// cast(spells[i], critter, 0) (UnderworldExhumed AI.C), which is do_spell's dispatch for every
    /// caster - here UWSpellCasting.CastSpell with this creature's UWCreatureSpellHost (its record's
    /// spot and heading, and fReleaseSpellMissile as its launcher). The area spells and Summon
    /// Monster (since 2026-09-27) and the missile spells had dispatches of their own here until
    /// then, and only the first two refused in the Void (B2); do_spell's refusal now holds for all
    /// (UWEndgameRules.IsMagicBlocked), while the mind still starts a cast there (it tests the
    /// anti-magic bit alone, as the original's). The comments of the two paths it replaced:
    ///
    /// A CREATURE'S AREA SPELL (2026-09-27): SpellTrapWandCast_seg038_27 hands the creature's
    /// spell to CastSpells_seg038_3307_78 with the creature as the caster, and class 6 goes to
    /// Class6Spells with it - the area lies four tiles ahead ALONG THE CREATURE'S HEADING, the
    /// hits and the damage are the player's spell's (UWAreaSpellRules.CastAt), and the damage
    /// is charged to the creature. Nothing on a no-magic tile or in the void, as CastSpells
    /// refuses there. Until then a creature's area spell did nothing at all: a summoned mage
    /// cast Flame Wind at a reaper without any effect (per user). The other classes a creature
    /// holds - heals of class 4, which CastSpells refuses without a target (SpellTrapWandCast
    /// passes none), and Light and Resist Blows, which it runs for the player only - do
    /// nothing in the original either, nor does class 0xB (Tremor, Local Teleport), which
    /// MajorSpellClassB runs for the player only. Summoning (class 8): fSummonMonster.
    ///
    /// A CREATURE SUMMONS (2026-09-27, mage 106's third spell): Class8Spells with the creature
    /// as caster - see UWSummonSpellRules.SummonByCreature. The spot lies along the creature's
    /// facing from its body; the new creature is hostile and heads for the player. Nothing on a
    /// no-magic tile or in the void, as CastSpells refuses there.
    /// </summary>
    void ICritterHost.OnSpellCast(int piSpellIndex, int piPitch)
    {
        fEvent("spell {0} cast, pitch {1}", piSpellIndex, piPitch);

        UWWorld lOWorld = mOLevelLoader != null ? UWWorld.Of(mOLevelLoader) : null;

        if (lOWorld == null || lOWorld.Level == null || lOWorld.Data == null)
            return;

        UWCastContext lOCast = UWCastContext.Creature(Index,
            new UWSpot(new UWTilePos(mORecord.TileX, mORecord.TileY), mORecord.FineX, mORecord.FineY, mORecord.ZPos),
            mORecord.FullFacing);

        UWCreatureSpellHost lOHost = new UWCreatureSpellHost(lOWorld, lOCast,
            piProjectile => fReleaseSpellMissile(piProjectile, piPitch));

        if (!UWSpellCasting.CastSpell(piSpellIndex, lOHost))
            fEvent("spell {0}: did not take (no magic here)", piSpellIndex);
    }

    /// <summary>A spell missile of the creature (release_missile -> spell_fire): at the target
    /// along its facing at the brain's pitch, its damage type from the projectile table. Only
    /// while the creature has a target, as before cleanup step 4.10 (2026-10-18).</summary>
    private void fReleaseSpellMissile(int piProjectile, int piPitch)
    {
        CritterTarget lOTarget = ((ICritterHost)this).GetObject(mORecord.GTarg);

        if (!lOTarget.Exists)
            return;

        fLaunchProjectile(piProjectile, UWRunicMagic.GetProjectileDamageType(piProjectile,
            mOLevelLoader != null && mOLevelLoader.UWDataImporter != null ? mOLevelLoader.UWDataImporter.ObjectProperties : null), piPitch);
    }

    /// <summary>
    /// Sends a missile at the target along the creature's facing at the brain's pitch
    /// (GetPitchToGTarg): a mobile record on the motion core since 2026-10-05
    /// (UWMissiles.LaunchFromCreature). It starts radii + 4 eighths ahead at five sixths
    /// of the creature's height plus two per pitch step; NO ROOM there (seg025_B51: a wall, a
    /// step, a creature in melee contact) means no missile, as in the original.
    /// </summary>
    private void fLaunchProjectile(int piObjectId, int piDamageType, int piPitch)
    {
        if (mOLevelLoader == null || mOLevelLoader.UWDataImporter == null
            || mOLevelLoader.UWDataImporter.ObjectProperties == null)
            return;

        // The ammunition table's damage through the dice with charge 0x80 and flank 0
        // (MissileAttackHit_seg022_14BF, deviation 46); the target's armour of the part hit
        // and the difficulty are subtracted on impact (UWMissiles.StrikePlayer). The dice are
        // rolled at the hit since cleanup step 4.9b (2026-10-18, by the user's decision to follow
        // the original's missile_thwack -> do_damage; UWAttackResolution.Missile); the flight
        // carries the base.
        int liBase = mOLevelLoader.UWDataImporter.ObjectProperties.GetRangedDamage(piObjectId);

        UWProjectileFlight lOFlight = UWMissiles.Ensure(mOLevelLoader).LaunchFromCreature(this, piObjectId, piPitch,
            liBase, piDamageType, UWRunicMagic.GetProjectileImpactId(piObjectId));

        if (lOFlight == null)
        {
            fEvent("missile {0}: no room at the start, none made", piObjectId);

            return;
        }

        miShotsFired++;
    }

    /// <summary>How often this creature has shot or cast - only for the F1 overlay.</summary>
    private int miShotsFired;

    /// <summary>
    /// Goal 10: the player looked back within reach - TalkTo, with the goal untouched (crit_talk,
    /// UnderworldExhumed AI.C): the script imports npc_goal 10 and the write-back keeps whatever it
    /// leaves (CONVVARS.C, RefreshFromData), so a script that sets no goal leaves the creature
    /// waiting to talk, and it starts again when the player faces it within reach. Until
    /// 2026-10-15 ours cleared the goal to 0 first, so such a creature stood for good (audit
    /// round 2, A13 / KR D13, GH D11).
    /// </summary>
    void ICritterHost.OnTalk()
    {
        if (UWConversationScreen.IsAnyOpen || mONpc == null)
            return;

        Interaction lOInteraction = UWScene.Interaction;

        if (lOInteraction == null)
            return;

        fEvent("talks");
        lOInteraction.TryTalkToNpcData(mONpc);
    }

    /// <summary>LoadSoundAtCoordinate at the creature: the numbers are the original's, which
    /// UWSoundEffects plays directly (footsteps 1, 2, 5, 0x0D, 0x0E, 0x17; death 6).</summary>
    void ICritterHost.PlaySound(int piSound)
    {
        UWSoundEffects.PlayAt(piSound, mOBody);
    }

    /// <summary>ChangeThemeMusic with the original's theme numbers (5 the hurt combat theme, 6
    /// combat), and the combat music timer reset. At a swing's frame 0 the brain asks for 6
    /// even while a combat theme already plays - then only the timer is reset (53136-53160).</summary>
    void ICritterHost.PlayMusic(int piTheme)
    {
        bool lbCombatPlaying = UWMusic.CurrentTheme >= UWMusic.FirstCombatTheme && UWMusic.CurrentTheme <= UWMusic.LastCombatTheme;

        if (!(piTheme == UWCritterRules.CombatTheme && lbCombatPlaying))
            UWMusic.ChangeTheme(piTheme);

        UWMusic.MarkCombat();
    }

    /// <summary>
    /// A land creature ended its step in deep water (seg006_1477_476, asm lines 41417-41460):
    /// the class-7 object with offset 6 (0x1C6 "a_splash") at its position.
    ///
    /// THE ONE SPLASH OF A DROWNING IS THIS ONE, the creature itself. The death removal that
    /// follows drops loot and remains as after any other death; what lands on the water tile
    /// is removed SILENTLY there (UWLevelLoader.fSpawnObjectAt), which is why the user counted
    /// exactly one splash although the goblin left nothing behind (2026-09-20). Only an object
    /// that FALLS into water splashes on landing - see Todo.md section 4.
    ///
    /// The sound: the branch itself calls no LoadSoundAtCoordinate, so the number is not in
    /// the disassembly. The user heard a splash (2026-09-20), and sound 0 is the game's water
    /// sound - the one the player's own step into water plays (UWPlayerTerrain).
    /// </summary>
    void ICritterHost.OnDrowned()
    {
        fEvent("drowned");

        UWSoundEffects.PlayAt(UWSoundEffects.Water, mOBody);

        if (mOLevelLoader != null)
            mOLevelLoader.SpawnEffectAt(SplashEffectObjectId, mOBody, UWLevelLoader.SplashEffectSeconds);
    }

    /// <summary>
    /// Death began in the brain (animation 0x0C, frame 0, interval 4): the experience for a
    /// kill by the player (AwardKillEXP; the formula lives in UWPlayerVitals), and the
    /// UWDamageable marked destroyed (cleanup step 4.8, 2026-10-18; drained to zero until then) -
    /// so UWCritterRemains and the rest of the port see the creature as destroyed. The dragon nod
    /// comes with the kill experience (UWCharacter.OnKillRewarded).
    /// </summary>
    void ICritterHost.OnDeathStarted(bool pbByPlayer)
    {
        fEvent("dying{0}", pbByPlayer ? " by the player" : "");

        if (pbByPlayer && mbHasStats && fEnsurePlayer() && mOLevelLoader != null)
            mOPlayer.Vitals.AwardKillExperience(mOStats, mORecord.IsStrong, mOLevelLoader.CurrentLevelIndex + 1);

        fMarkDestroyed();
    }

    /// <summary>
    /// SpecialDeathCases(obj, 0) for a creature with a whoami - the moment at which the
    /// original lets a named creature refuse its own death (UWSpecialDeaths, built
    /// 2026-09-21). Only two do: the nameless 0x0B and the metal golem, who both talk instead
    /// of dying.
    /// </summary>
    bool ICritterHost.VetoDeath()
    {
        int liWhoami = mONpc != null ? mONpc.NPCwhoami : 0;

        if (!UWSpecialDeaths.HasSpecialDeath(liWhoami))
            return false;

        // The world and the creature itself since cleanup step 3.6 (2026-10-17); a host of its own
        // (SpecialDeathHost, here) until then.
        // A refused death must not kill the body (per user, 2026-09-28: the golem fell to dust
        // after his conversation). Nothing to do for it since cleanup step 4.8 (2026-10-18): the
        // body is destroyed only when the death is committed (UWHealth.MarkDestroyed), not when its
        // hit points reach 0, so the golem lives on with his record's 0, as in the original, until
        // his script sets 125. Until then the body counted its own copy, which had reached 0 when
        // the brain asked, and got at least 1 back (fKeepBodyAlive) - the 1 the save wrote (B23).
        return !UWSpecialDeaths.Apply(liWhoami, UWSpecialDeaths.Stage.Dying, UWWorld.Of(mOLevelLoader),
            UWWorld.RefOf(this));
    }

    /// <summary>
    /// SpecialDeathCases(obj, 1): what follows a named creature's death. The original asks
    /// this from NPCInitialProcessing, that is when a dead one is next processed; we ask it
    /// the moment the death is committed, which is the same creature in the same state.
    /// </summary>
    private void fApplySpecialDeath()
    {
        int liWhoami = mONpc != null ? mONpc.NPCwhoami : 0;

        if (UWSpecialDeaths.HasSpecialDeath(liWhoami))
            UWSpecialDeaths.Apply(liWhoami, UWSpecialDeaths.Stage.Dead, UWWorld.Of(mOLevelLoader), UWWorld.RefOf(this));
    }

    /// <summary>Frame 3 of animation 0x0C: the body goes. UWCritterRemains leaves the remains
    /// and the loot and frees the object; the animator is told the death pictures are over.</summary>
    void ICritterHost.OnDeathFinished()
    {
        fEvent("death finished");
        fMarkDestroyed();

        // SpecialDeathCases with mode 1 - what follows a named creature death. The original
        // asks it from NPCInitialProcessing, when a dead one is next processed; the moment the
        // body goes is the closest one we have (see fApplySpecialDeath).
        fApplySpecialDeath();

        // The remains go where the creature LOGICALLY stands, not where its picture happens
        // to have got to - the picture lags behind by up to one interval (fApplyMotion), and
        // for a creature that drowned one interval ago that is the difference between the
        // water tile and the bank. UWCritterRemains reads transform.position.
        transform.position = mOBody;
        mfPictureSeconds = 0f;

        UWCritterAnimator lOAnimator = GetComponent<UWCritterAnimator>();

        if (lOAnimator != null)
            lOAnimator.MarkDeathFinished();

        UWCritterRemains lORemains = GetComponent<UWCritterRemains>();

        if (lORemains != null)
            lORemains.CompleteDeath();
        else
            Destroy(gameObject);
    }

    /// <summary>The body counts as destroyed from now on (UWDamageable.MarkDestroyed raises
    /// Destroyed once).</summary>
    private void fMarkDestroyed()
    {
        if (mODamageable != null)
            mODamageable.MarkDestroyed();
    }

    void ICritterHost.OnGoalChanged(int piOldGoal, int piNewGoal, int piGTarg)
    {
        fTrace("goal {0} -> {1} (gtarg {2})", piOldGoal, piNewGoal, piGTarg);
    }

    // ------------------------------------------------- Damage intake

    /// <summary>
    /// Every blow, missile, spell or burn the UWDamageable took, as DamageNPC, with the attacker
    /// the blow names (UWDamageable.CreatureTakesBlow, cleanup step 4.8, 2026-10-18): a creature's
    /// index, the player, or nobody - a miss of the player's blow too, which lands with 0. The
    /// brain updates its bytes 0x11, 0x12 and 8, writes the kin alarm, starts the death and
    /// switches the music; the mind reacts at its next update.
    ///
    /// THE AMOUNT IS WHAT THE HIT POINTS LOSE, at most the record's hit points: the tally of byte
    /// 0x11 grows by that, as it did when the amount was the drop of the body's own copy (until
    /// 2026-10-18; damage_critter, UnderworldExhumed AI.C, adds the whole damage). A creature
    /// whose death was refused holds 0 and so tallies 0 - the copy's 1 is gone (B23).
    /// </summary>
    private void fTakeBlow(int piDamage, int piAttacker)
    {
        if (mOBrain == null)
            return;

        int liAmount = Mathf.Max(0, Mathf.Min(piDamage, mORecord.HitPoints));

        fTrace("damaged {0} by {1} (hp {2})", liAmount, piAttacker == PlayerIndex ? "the player" : piAttacker.ToString(),
            mORecord.HitPoints - liAmount);
        mOBrain.OnDamaged(this, piAttacker, liAmount);
    }

    // ------------------------------------------------- Spells on the creature

    /// <summary>Confusion: goal 2, attitude 1, the player as target - the three fields the
    /// original's spell writes (deviation 39 adopted), after the resistance check
    /// (fPassesSpellResistance). No duration.</summary>
    public UWTargetSpellRules.Result Confuse()
    {
        if (mORecord == null || mODamageable == null || mODamageable.IsDestroyed)
            return UWTargetSpellRules.Result.Missed;

        if (!fPassesSpellResistance("confusion"))
            return UWTargetSpellRules.Result.Spent;

        fTrace("confused");
        mORecord.Goal = WanderGoal;
        mORecord.GTarg = PlayerIndex;
        mORecord.Attitude = 1;

        return UWTargetSpellRules.Result.Shown;
    }

    /// <summary>
    /// THE RESISTANCE CHECK before Cause Fear, Confusion, Paralyse and Ally, built 2026-09-23
    /// (per user, it had been left out since the spells were rebuilt, deviation 39): the
    /// original's Ally_seg038_3307_8F8 and the shared routine of the other three ask
    /// ScaleDamageAgainstObject whether one point of MAGIC damage (type 3) gets through
    /// (UWDamageTypes.Scale). Magic resistance 0 - the low two bits of COMOBJ byte 8 - lets
    /// every spell through, 1 turns one in three away, 2 two in three, 3 all. A spell turned
    /// away is spent and does nothing, as in the original, and the caller shows no message - nor
    /// the spell picture (UWTargetSpellRules.Result.Spent, since 2026-10-12).
    /// </summary>
    private bool fPassesSpellResistance(string psSpell)
    {
        if (UWDamageTypes.Scale(mODamageable.Resistances, 1, UWDamageTypes.Magic) != 0)
            return true;

        fTrace("resists the {0} spell", psSpell);

        return false;
    }

    /// <summary>Is this creature Tybal? For breaking the orb (UWTybalOrbRules.TybalWhoAmI).</summary>
    public bool IsTybal => mORecord != null && mORecord.WhoAmI == UWTybalOrbRules.TybalWhoAmI;

    /// <summary>The creature's record - for the sleep ambush (UWSleepRules.TryAmbush).</summary>
    public UWCritterRecord Record => mORecord;

    /// <summary>Its hit points are gone - it may still be falling.</summary>
    public bool IsDead => mODamageable != null && mODamageable.IsDestroyed;

    /// <summary>Dying with a goal that skips the animation machine: the death never reaches
    /// frame 3, so nothing may remove the body (UWCritterRules.SkipsAnimationMachine).</summary>
    public bool NeverFinishesDying => mORecord != null && mORecord.Animation == UWCritterBrain.AnimDying
        && UWCritterRules.SkipsAnimationMachine(mORecord.Goal);

    /// <summary>Critter table byte 0x1C, upper nibble; 0 without a table row.</summary>
    public int TravelRange => mbHasStats ? mOStats.TravelRange : 0;

    /// <summary>The ally bit of the mobile record - the player's blow passes over an ally
    /// (Interaction.fFindClassicMeleeTarget, set_hitobj).</summary>
    public bool IsAlly => mORecord != null && mORecord.IsAlly;

    /// <summary>The table's average hit points (critter byte 4, avghit) - what the eyes measure
    /// against (UWCombat.GetEyesGroup); 0 without a table row.</summary>
    public int TableVitality => mbHasStats ? mOStats.Vitality : 0;

    /// <summary>Whether the player's critical failure against this creature wears the weapon
    /// (UWEquipmentWear).</summary>
    public bool WearsWeaponOnCriticalFailure => mbHasStats && UWEquipmentWear.WearsWeaponOnCriticalFailure(mOStats);

    /// <summary>
    /// The sleep ambush's path (wander_that_monster, UnderworldExhumed CRITTIME.C): set_critter_vars
    /// for this creature, then flood_path from its tile and height to pOTo at piToHeight with
    /// range 0 - the brain's own search (UWTileRoute.FindPath with this creature's triple test)
    /// with no danger allowed: no drop of more than a level, no costly terrain, a closed door
    /// blocked by its orientation alone, whatever the creature's door skill. pOPath gets the
    /// tiles, start first. Until 2026-10-15 this was the old walker (UWTilePath.TryGetPath) with
    /// the door and lava terms left over from the last brain search (audit round 2, A11 / KR D11).
    /// </summary>
    public bool TryGetPathTo(UWTilePos pOTo, int piToHeight, System.Collections.Generic.List<UWTilePos> pOPath)
    {
        pOPath.Clear();

        if (mORecord == null || !fPrepareRoute())
            return false;

        if (!mORoute.FindPath(mORecord.TileX, mORecord.TileY, mORecord.FloorLevel, pOTo.X, pOTo.Y, piToHeight,
            AmbushPathRange, mOTraverse.Triple))
            return false;

        pOPath.AddRange(mORoute.Tiles);

        return true;
    }

    /// <summary>flood_path's range for the ambush: no danger at all.</summary>
    private const int AmbushPathRange = 0;

    /// <summary>
    /// Sets the creature down on a sub-tile spot (0..7 each) of a tile - the sleep ambush
    /// (UWSleepRules.TryAmbush). Refused on a solid tile or the wrong ground for its kind; the
    /// motion core sorts out whatever else stands there on its first step (until stage 2 the
    /// host's probes also refused a door, the player, a creature and a force field).
    /// </summary>
    public bool PlaceAtSpot(UWTilePos pOTile, int piSubX, int piSubY)
    {
        UWTile lOTile = fGetTileAt(pOTile.X, pOTile.Y);

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid || mORecord == null)
            return false;

        if (!mbFlying && mbSwimming != fIsWater(lOTile))
            return false;

        // On the floor, fliers too: the ambush hands the tile's floor height times eight as zpos
        // (ovr104_71C) - unlike the level exit, which lifts a flier halfway up.
        fRelocate(pOTile.X, pOTile.Y, piSubX & 7, piSubY & 7, (lOTile.FloorHeight >> 4) << 3);
        fTrace("set down by the sleep ambush on {0}/{1} at {2}/{3}", pOTile.X, pOTile.Y, piSubX, piSubY);

        return true;
    }

    /// <summary>
    /// The night's time pass rewrote the record (UWLevelExit.PassTime, 2026-10-15): the body
    /// stands at the record's spot and facing at once. The pass heals a wounded creature half way
    /// to its kind's average in the record, which the health reads (cleanup step 4.8, 2026-10-18;
    /// copied over until then).
    /// </summary>
    public void FollowRecord()
    {
        if (mORecord == null || mODamageable == null || mODamageable.IsDestroyed)
            return;

        fSetBodyNow();

        fTrace("time passed: tile {0}/{1}, facing {2}, hp {3}", mORecord.TileX, mORecord.TileY,
            mORecord.FacingEighth, mORecord.HitPoints);
    }

    /// <summary>
    /// The orb is broken: Tybal's hit points are halved plus one and he no longer heals when
    /// the player leaves the level (UWTybalOrbRules.WeakenedHitPoints, word 0x0D bit 9).
    /// </summary>
    public void WeakenByBrokenOrb()
    {
        if (mORecord == null || mODamageable == null || mODamageable.IsDestroyed)
            return;

        // Into the record, through the health's 1-to-maximum bound (one write since cleanup step
        // 4.8, 2026-10-18; the health's copy and the record until then).
        mODamageable.Health.SetCurrentHealth(UWTybalOrbRules.WeakenedHitPoints(mODamageable.CurrentHealth));
        mORecord.NoLevelExitHealing = true;

        fTrace("weakened by the broken orb: {0} hit points", mORecord.HitPoints);
    }

    /// <summary>
    /// ALLY, the spell (Ally_seg038_3307_8F8, read 2026-09-23): unless it is an ally already,
    /// goal 2 with gtarg 0 - wandering, bound to nobody; then byte 0x19 bit 6 and attitude 3.
    /// For good, no duration. From then on it fights for the player the original's way: the
    /// kin alarm sends it at whatever the player strikes, and whoever strikes it is its target
    /// (UWCritterBrain). Until 2026-09-23 this was Confusion (goal 2, attitude 1, the player
    /// as target), because the port had no allies.
    /// </summary>
    public UWTargetSpellRules.Result Befriend()
    {
        if (mORecord == null || mODamageable == null || mODamageable.IsDestroyed)
            return UWTargetSpellRules.Result.Missed;

        if (!fPassesSpellResistance("ally"))
            return UWTargetSpellRules.Result.Spent;

        fTrace("befriended");

        if (!mORecord.IsAlly)
        {
            mORecord.Goal = WanderGoal;
            mORecord.GTarg = 0;
        }

        mORecord.IsAlly = true;
        mORecord.Attitude = 3;

        return UWTargetSpellRules.Result.Shown;
    }

    /// <summary>Paralyze: goal 7 (stand still) and attitude 1, after the resistance check
    /// (fPassesSpellResistance), without a duration as in the uw1 branch of the reference.
    /// A hit wakes it: the damage reaction sets goal 5.</summary>
    public UWTargetSpellRules.Result Paralyse()
    {
        if (mORecord == null || mODamageable == null || mODamageable.IsDestroyed)
            return UWTargetSpellRules.Result.Missed;

        if (!fPassesSpellResistance("paralysis"))
            return UWTargetSpellRules.Result.Spent;

        fTrace("paralysed");
        mORecord.Goal = GoalParalysed;
        mORecord.Attitude = 1;

        return UWTargetSpellRules.Result.Shown;
    }

    /// <summary>Cause Fear: goal 6 against the player, without a morale roll.</summary>
    public UWTargetSpellRules.Result Flee()
    {
        if (mORecord == null || mODamageable == null || mODamageable.IsDestroyed || !fEnsurePlayer())
            return UWTargetSpellRules.Result.Missed;

        if (!fPassesSpellResistance("fear"))
            return UWTargetSpellRules.Result.Spent;

        fTrace("flees (spell)");
        mORecord.SetNewGoal(GoalWithdraw, PlayerIndex);

        return UWTargetSpellRules.Result.Shown;
    }

    // ------------------------------------------------- Theft notice (spec 4.5)

    /// <summary>
    /// Does this creature SEE a theft on that tile? For UWTriggerSystem.AngerRaceAround. The
    /// original checks the distance to the ITEM against its own sight range from the critter
    /// table - not against the player's stealth - and a clear line between the two with the
    /// heights of TestBetweenPoints, which a closed door does not block (per user,
    /// 2026-09-16).
    /// </summary>
    /// <remarks>The point is the object's spot in eighths and its sight height
    /// (UWCritterRules.GetTheftSightZ) - since 2026-09-23; before, the floor of the tile
    /// centre.</remarks>
    public bool CanSeeTheftAt(int piX8, int piY8, int piZ)
    {
        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null || mORecord == null)
            return false;

        int liRange = mbHasStats ? mOStats.SightRange : 8;

        if (!UWCritterRules.IsWithinTheftSight(mORecord.X - piX8, mORecord.Y - piY8, liRange))
        {
            fTrace("theft at {0}/{1} z {2}: out of sight range {3} (from {4}/{5})", piX8, piY8, piZ, liRange, mORecord.X, mORecord.Y);
            return false;
        }

        int liOwnRadius;
        int liOwnHeight;
        fGetOwnSize(out liOwnRadius, out liOwnHeight);

        bool lbSees = fHasLineOfSight(mORecord.X, mORecord.Y, mORecord.ZPos + liOwnHeight, piX8, piY8, piZ);

        fTrace("theft at {0}/{1} z {2}: {3} (from {4}/{5} z {6})", piX8, piY8, piZ,
            lbSees ? "sees it" : "line blocked", mORecord.X, mORecord.Y, mORecord.ZPos + liOwnHeight);

        return lbSees;
    }

    // ------------------------------------------------- The kin alarm

    /// <summary>When building a level: a fresh clock and alarm (UWScene), see
    /// UWGameSession.ResetCritterServices. Loading a save restores the save's alarm afterwards
    /// (UWLevelLoader.fRestoreKinAlarm).</summary>
    public static void ResetAlarm()
    {
        UWScene.ResetCritterServices();
    }

    // ------------------------------------------------- Push

    /// <summary>
    /// A shove - the player walking into the creature (UWPlayerMovement, the bridge until stage
    /// 3), a missile or thrown thing striking it (UWMotionHost.PushObject): the momentum
    /// transfer's write-back on the record (UWCreatureMotion.Push) - the pusher's 16-bit
    /// heading, the speed and the vertical speed in the stepper's units. The creature's next
    /// update moves it that way before its mind decides anew; in the original an opponent can
    /// be pushed away by walking into it (per user, 2026-09-05), and the core keeps it out of
    /// walls and water on that step.
    /// </summary>
    public void Push(int piHeading, int piSpeed, int piVz)
    {
        if (mbRemoved || mORecord == null)
            return;

        UWCreatureMotion.Push(mORecord, piHeading, piSpeed, piVz);
        fTrace("pushed: heading {0}, speed {1}", mORecord.FineHeading, mORecord.Speed);
    }

    private bool fIsWater(UWTile pOTileData)
    {
        if (pOTileData == null || mOLevelLoader == null || mOLevelLoader.UWDataImporter == null)
            return false;

        UWTerrain lOTerrain = mOLevelLoader.UWDataImporter.Terrain;

        return lOTerrain != null && lOTerrain.IsWaterFloor(pOTileData.TextureFloor);
    }

    /// <summary>Radius and height from COMOBJ.DAT, in the original's units - eighths of a tile
    /// and zpos steps.</summary>
    private void fGetOwnSize(out int piRadius, out int piHeight)
    {
        piRadius = FallbackCritterRadius;
        piHeight = FallbackCritterHeight;

        UWCommonObjectProperties lOCommon = fCommonProperties();

        if (lOCommon != null && mONpc != null && lOCommon.TryGet(mONpc.ID, out UWCommonObjectProperties.Entry lOOwn))
        {
            piRadius = lOOwn.Radius;
            piHeight = lOOwn.Height;
        }
    }

    private UWCommonObjectProperties fCommonProperties()
    {
        return mOLevelLoader != null && mOLevelLoader.UWDataImporter != null
            ? mOLevelLoader.UWDataImporter.CommonObjectProperties : null;
    }

    // ------------------------------------------------- Tiles and heights

    private UWTile fGetTileAt(Vector3 pOWorldPosition)
    {
        UWTilePos lOTile = UWTileQueries.WorldToTile(pOWorldPosition.x, pOWorldPosition.z);

        return fGetTileAt(lOTile.X, lOTile.Y);
    }

    private UWTile fGetTileAt(int piTileX, int piTileZ)
    {
        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null
            || mOLevelLoader.CurrentLevel.TileData == null)
            return null;

        return mOLevelLoader.CurrentLevel.GetTile(piTileX, piTileZ);
    }

    /// <summary>How deep a WATER creature lies below the water level, in world units - see
    /// the placement in Initialise. Zero for everyone else.</summary>
    private float fGetSwimmerOffset()
    {
        if (!mbSwimming)
            return 0f;

        UWSettings lOSettings = UWSettings.Instance;

        return lOSettings != null ? lOSettings.SwimmerHeightOffset : 0f;
    }

    /// <summary>Where the creature looks from and shoots from: the centre of its body, not its
    /// feet - otherwise every floor step blocks the view.</summary>
    private Vector3 fGetEyePosition()
    {
        Collider lOCollider = GetComponent<Collider>();

        return lOCollider != null
            ? new Vector3(mOBody.x, lOCollider.bounds.center.y, mOBody.z)
            : mOBody + (Vector3.up * FallbackEyeHeight);
    }

    /// <summary>Is this the player's own body? Recognised by the CharacterController, which
    /// only he has - NOT by UWCharacter, which sits on the CAMERA (2026-09-16).</summary>
    private static bool fIsPlayerCollider(Collider pOCollider)
    {
        return pOCollider != null && pOCollider.GetComponent<CharacterController>() != null;
    }

    private bool fEnsurePlayer()
    {
        if (mOPlayerTransform != null)
            return true;

        if (mOPlayer == null)
            mOPlayer = UWScene.Character;

        if (mOPlayer == null)
            return false;

        mOPlayerTransform = mOPlayer.transform;

        return true;
    }

    // ------------------------------------------------- Trace of the decisions

    private void fTrace(string psFormat, params object[] pOArguments)
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
    private void fEvent(string psFormat, params object[] pOArguments)
    {
        msLastEvent = pOArguments == null || pOArguments.Length == 0 ? psFormat : string.Format(psFormat, pOArguments);
        fTrace(msLastEvent);
    }

    /// <summary>The trace, oldest line first. Empty while tracing is off.</summary>
    public string GetTrace()
    {
        System.Text.StringBuilder lOText = new System.Text.StringBuilder();

        for (int liAt = 0; liAt < TraceLength; liAt++)
        {
            string lsLine = msTrace[(miTraceNext + liAt) % TraceLength];

            if (!string.IsNullOrEmpty(lsLine))
                lOText.Append("\n    ").Append(lsLine);
        }

        return lOText.ToString();
    }

    /// <summary>The record as the F1 overlay shows it (see UWDebugOverlay.fAppendNearestCritter):
    /// goal, target, slot and interval, animation and frame, the temper bits, the last event.</summary>
    public string DescribeCombatStats()
    {
        if (mORecord == null)
            return "  (no record)";

        System.Text.StringBuilder lOText = new System.Text.StringBuilder();

        lOText.AppendFormat("\n  Goal {0}  gtarg {1}  backup {2}  attitude {3}  hp {4}  index {5}",
            mORecord.Goal, mORecord.GTarg, mORecord.BackupGoal, mORecord.Attitude, mORecord.HitPoints, miIndex);

        // The kin alarm as this creature sees it - added 2026-09-27 for the summoned ally that
        // stayed mellow next to a creature the player hit (Todo "Summon Monster").
        UWCritterAlarm lOAlarm = UWScene.CritterAlarm;
        long liClock = UWScene.CritterClock != null ? UWScene.CritterClock.Clock : 0;

        lOText.AppendFormat("\n  Ally {0}  passive {1}  hearing {2}  kind {3}",
            mORecord.IsAlly ? 1 : 0, mOStats.IsPassive ? 1 : 0, mOStats.NoiseRange, mOStats.GeneralType);

        if (lOAlarm != null && lOAlarm.Index != 0)
            lOText.AppendFormat("\n  Alarm: victim {0} kind {1} at {2}/{3}, age {4} of {5}, fresh {6}, manhattan {7}",
                lOAlarm.Index, lOAlarm.Kind, lOAlarm.TileX, lOAlarm.TileY, liClock - lOAlarm.Clock,
                UWCritterRules.KinAlarmPitTicks, lOAlarm.IsFresh(liClock) ? 1 : 0,
                Mathf.Abs(mORecord.TileX - lOAlarm.TileX) + Mathf.Abs(mORecord.TileY - lOAlarm.TileY));
        else
            lOText.Append("\n  Alarm: none");

        lOText.AppendFormat("\n  Slot {0} of phase {1}  interval {2}  anim 0x{3:X2} frame {4}  speed {5}  heading {6}  facing {7}+{8}",
            mORecord.DueSlot, UWScene.CritterClock.Phase, mORecord.Interval, mORecord.Animation, mORecord.Frame,
            mORecord.Speed, mORecord.FineHeading, mORecord.FacingEighth, mORecord.FacingResidual);

        lOText.AppendFormat("\n  Confirmed {0}  heard {1}  blocked {2}  path {3}  straight {4}  stand {5}  relentless {6}  charge {7}  pitch {8}",
            mORecord.TargetConfirmed ? 1 : 0, mORecord.HeardSomething ? 1 : 0, mORecord.Blocked ? 1 : 0,
            mORecord.HasPath ? 1 : 0, mORecord.StraightLineKnown ? 1 : 0, mORecord.MadeStand ? 1 : 0,
            mORecord.Relentless ? 1 : 0, mORecord.ChargeIndex, mORecord.Pitch);

        lOText.AppendFormat("\n  Destination {0}/{1} h{2}  tile {3}/{4} fine {5}/{6} z {7}  shots {8}",
            mORecord.DestinationX, mORecord.DestinationY, mORecord.DestinationHeight,
            mORecord.TileX, mORecord.TileY, mORecord.FineX, mORecord.FineY, mORecord.ZPos, miShotsFired);

        if (mbHasStats)
            lOText.AppendFormat("\n  Vitality {0}  morale {1}  initiative {2}  hear {3}  see {4}  door skill {5}  {6}{7}",
                mOStats.Vitality, mOStats.Morale, mOStats.Initiative, mOStats.NoiseRange, mOStats.SightRange,
                mOStats.DoorSkill, mbFlying ? "flier " : (mbSwimming ? "swimmer " : "walker "),
                mOStats.IsCaster ? "caster" : "");

        if (mOLastStep.Collided)
            lOText.AppendFormat("\n  Last step collided: object {0} item 0x{1:X3}{2}",
                mOLastStep.HitObjectIndex, mOLastStep.HitObjectItemId, mOLastStep.HitClosedDoor ? " (closed door)" : "");

        UWCritterAnimator lOAnimator = GetComponent<UWCritterAnimator>();
        UWTile lOTile = fGetTileAt(mOBody);

        lOText.AppendFormat("\n  y {0:F0}  tile floor {1}  picture slot 0x{2:X2} frame {3}  last event: {4}",
            mOBody.y, lOTile == null ? -1 : lOTile.FloorHeight,
            lOAnimator == null ? -1 : lOAnimator.CurrentSlot,
            lOAnimator == null ? -1 : lOAnimator.CurrentFrameInSegment, msLastEvent);

        return lOText.ToString();
    }
}
