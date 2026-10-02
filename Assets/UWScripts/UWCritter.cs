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
public class UWCritter : MonoBehaviour, UWTilePath.IWalker, ICritterHost
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

    private const int GoalStandStill = 0;

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

    /// <summary>World units per eighth of a tile: the sub-tile step of the object grid.</summary>
    private const float WorldUnitsPerEighth = UWWorldScale.SubTileStep;

    /// <summary>The original's timer ticks per second (spec 1.1).</summary>
    public const float PitTicksPerSecond = 256f;

    /// <summary>How many due updates one creature may run in one frame. The clock never
    /// advances more than four slots per frame, so an interval-1 creature needs at most four;
    /// the margin catches a record whose interval loaded as 0.</summary>
    private const int MaxUpdatesPerFrame = 8;

    /// <summary>How long a found next tile is reused for the same destination from the same
    /// tile (deviation 14: the host may cache a path and re-run it on its own schedule).</summary>
    private const float PathRefreshSeconds = 0.5f;

    /// <summary>The straight-line test walks at most this many tiles (spec 7.4).</summary>
    private const int StraightLineMaxTiles = UWCritterRules.StraightLineMaxTiles;

    /// <summary>The splash of a drowning creature: the class-7 object with offset 6
    /// (seg006_1477_476, asm line 41433 "push 6"), which is 0x1C6 "a_splash". The id and the
    /// evidence are in UWObjectMechanics.SplashEffectObjectId, because an object falling into
    /// the water uses the same one (UWLevelLoader.fSplashAt).</summary>
    private const int SplashEffectObjectId = UWObjectMechanics.SplashEffectObjectId;

    /// <summary>How far above the tile floor something still counts as "on the floor" when
    /// the drowning test asks whether the creature stands on an object (the original's tile
    /// state bit 0x80, see fIsDrowningAt).</summary>
    private const float StandingOnObjectTolerance = 1f;

    /// <summary>Radius and height from COMOBJ, in case the table is missing: creatures 2 and
    /// 0x20, force field 3 and 0x80.</summary>
    private const int FallbackCritterRadius = 2;

    private const int FallbackCritterHeight = 0x20;

    private const int FallbackForceFieldRadius = 3;

    private const int FallbackForceFieldHeight = 0x80;

    private const int ForceFieldObjectId = UWObjectMechanics.ForceFieldObjectId;

    /// <summary>First ammunition object: the arrow. The critter table counts from here.</summary>
    private const int FirstAmmunitionObjectId = UWObjectMechanics.FirstAmmunitionObjectId;

    /// <summary>Major class of the missile spells - the only creature spells built.</summary>
    private const int ProjectileSpellMajorClass = 5;

    /// <summary>Four tiles for missiles, eight for spells - the range a creature projectile
    /// flies before it vanishes (unchanged by the rebuild of 2026-09-20).</summary>
    private const float MissileRangeTiles = 4f;

    private const float SpellRangeTiles = 8f;

    /// <summary>How far in front of the surface a blow's impact sits, in world units.</summary>
    private const float BlowEffectSetback = 4f;

    private const float BlowEffectSeconds = 0.6f;

    // ------------------------------------------------- The registry the driver walks

    private static readonly List<UWCritter> msActive = new List<UWCritter>();

    /// <summary>Every enabled creature of the scene, for UWCritterDriver. A list kept by
    /// OnEnable/OnDisable instead of a scene search per frame.</summary>
    public static IReadOnlyList<UWCritter> Active => msActive;

    // ------------------------------------------------- Components and data

    private UWDamageable mODamageable;
    private UWLevelLoader mOLevelLoader;
    private UWCharacter mOPlayer;
    private Transform mOPlayerTransform;
    private CharacterController mOPlayerBody;

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

    /// <summary>The brain's last Motion, applied when the creature is next due.</summary>
    private UWCritterBrain.Motion mOMotion;

    private bool mbHasMotion;

    private StepResult mOLastStep;

    private bool mbRemoved;

    /// <summary>If the creature itself already stands too close to a wall or edge, the
    /// clearance rules are suspended for this step - otherwise it would never get away.</summary>
    private bool mbClearanceRelaxed;

    private int miStartClearanceFailures;

    private int miStartShoreFailures;

    private float mfNextBlockLogTime;

    // ------------------------------------------------- Damage bookkeeping

    /// <summary>Who the next Damaged event of the UWDamageable is charged to: 1 the player
    /// (the default - every blow, missile and spell of the port that is not a creature's or
    /// the lava's comes from him), a creature's index for its blow, 0 for lava.</summary>
    private int miPendingAttacker = PlayerIndex;

    /// <summary>This creature's index in the level's mobile list - who it is to everyone
    /// else's damage attribution.</summary>
    public int Index
    {
        get { return miIndex; }
    }

    /// <summary>
    /// WHO IS ABOUT TO HIT THIS CREATURE. DamageNPC (54563-54802) stores the attacker in byte
    /// 0x12 - one for the player, and FOR A PROJECTILE THE PROJECTILE'S OWN BYTE 0x12, which
    /// is its launcher. Only then can the damage reaction do its work: it ignores a hit that
    /// came from neither the player nor an ally of either side, and it makes the attacker its
    /// goal target (see UWCritterBrain.fDamageReaction).
    ///
    /// The announcement holds for the next hit only; afterwards it falls back to the player,
    /// which is what the world does when nobody says otherwise. Until 2026-09-22 a creature
    /// shot by another creature's arrow blamed the player.
    /// </summary>
    public void AnnounceAttacker(int piIndex)
    {
        miPendingAttacker = piIndex;
    }

    private int miLastHealth;

    /// <summary>Set while the host itself drains the UWDamageable to match a death the brain
    /// decided (drowning, hit points reaching zero in the record) - that event is not a blow.</summary>
    private bool mbSuppressDamageEvent;

    // ------------------------------------------------- Path cache

    private UWTilePos mOPathFrom;

    private UWTilePos mOPathTo;

    private UWTilePos mOPathNext;

    private bool mbPathFound;

    private float mfPathValidUntil = -1f;

    /// <summary>What CanEnterTile applies during a search: whether a closed door counts as
    /// passable and how much of the range budget (spec 7.4) a lava tile may cost.</summary>
    private bool mbPathMayOpenDoors;

    private int miPathBudget;

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
    public float FacingDegrees => (mORecord != null ? mORecord.FacingEighth : 0) * 45f;

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

    /// <summary>The facing eighth of an object record in degrees - for whoever holds only the
    /// data (UWWorldSync used to). Word 2 bits 7-9 are the eighth, the five bits of npc_heading
    /// the residual within it.</summary>
    public static float GetFacingDegrees(UWNpc pONpc)
    {
        return (pONpc.Heading & 7) * 45f;
    }

    /// <summary>Counterpart to GetFacingDegrees: distributes a facing over both fields. No
    /// caller inside the port any more (the record writes both fields itself); kept for
    /// tools and scripts that only hold the data.</summary>
    public static void SetFacingDegrees(UWNpc pONpc, float pfDegrees)
    {
        int liFull = Mathf.RoundToInt(Mathf.Repeat(pfDegrees, 360f) * 256f / 360f) & 0xFF;

        pONpc.Heading = (ushort)(liFull >> 5);
        pONpc.NPCHeading = (byte)((pONpc.NPCHeading & ~0x1F) | (liFull & 0x1F));
    }

    /// <summary>Hostile (attitude 0). A hit makes any creature hostile through the brain's
    /// damage reaction; the trespass trap through Anger.</summary>
    public bool IsAggressive => mORecord != null && mORecord.Attitude == HostileAttitude;

    /// <summary>The attitude as it belongs in the object data, 0 hostile to 3 friendly.</summary>
    public int Attitude => mORecord != null ? mORecord.Attitude : HostileAttitude;

    /// <summary>The goal of the object data.</summary>
    public int Goal => mORecord != null ? mORecord.Goal : 0;

    /// <summary>Where the creature stood at build time. UWWorldSync only writes a new position
    /// if it has moved away from it.</summary>
    public Vector3 SpawnPosition { get; private set; }

    /// <summary>The logical position of the body (the picture may still be catching up) - for
    /// UWWorldSync, which writes the position into the data.</summary>
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

        miIndex = mOLevelLoader != null && mOLevelLoader.CurrentLevel != null && pONpc != null
            ? Mathf.Max(0, mOLevelLoader.CurrentLevel.Masterlist.IndexOf(pONpc))
            : 0;

        mOBrain = new UWCritterBrain(mORecord, pOStats, miIndex);

        // A WATER CREATURE LIES IN THE WATER, not on it. A water tile's floor height is the
        // water LEVEL - the player swims at it - so a creature placed on that floor floats on
        // the surface like a boat. UWSettings.SwimmerHeightOffset lowers it (per user,
        // 2026-09-16).
        Vector3 lOAt = transform.position;

        if (mbSwimming)
            lOAt.y = fGetFloorHeight(lOAt) + fGetSwimmerOffset();

        transform.position = lOAt;
        mOBody = lOAt;
        mOPictureFrom = lOAt;
        mfPictureSeconds = 0f;
        SpawnPosition = lOAt;

        // THE RECORD'S HIT POINTS ARE THE BRAIN'S. Where the data held 0 the spawner gave the
        // UWDamageable the table value (UWObjectSpawner); the brain's DamageNPC compares the
        // record's byte 8 against the blow, so it must hold the same number - otherwise the
        // first scratch would kill. Written through to UWNpc.HitPoints, which is what the
        // writer saves (UWLevelWriter.fWriteNpcBytes).
        if (mODamageable != null)
        {
            miLastHealth = mODamageable.CurrentHealth;

            if (mORecord.HitPoints != mODamageable.CurrentHealth && mODamageable.CurrentHealth > 0)
                mORecord.HitPoints = mODamageable.CurrentHealth;
        }

        // The position fields the host owns (contract section 6) start from where the spawner
        // put the body; the tile stays the one the data hangs the object in.
        fWritePositionToRecord(false);

        // THE SAVED ANIMATION IS PART OF THE STATE. A creature loaded in the middle of a fight
        // carries its animation and frame in the record (a save written by the original next to
        // the player: animation 0x07, frame 3), and the original shows that picture at once.
        // Without this the creature stood in its compass picture until the brain's first tick
        // and only then turned towards the player (per user, 2026-09-20).
        UWCritterAnimator lOStartAnimator = GetComponent<UWCritterAnimator>();

        if (lOStartAnimator != null)
            lOStartAnimator.ShowMotion(mORecord.Animation, mORecord.Frame);

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

        if (mONpc.HitPoints > 0 && mORecord.HitPoints != mONpc.HitPoints)
            mORecord.HitPoints = mONpc.HitPoints;

        // The body follows (the golem's script sets 125 after he was beaten, 2026-09-28).
        if (mODamageable != null && !mODamageable.IsDestroyed && mORecord.HitPoints > 0
            && mODamageable.CurrentHealth != mORecord.HitPoints)
        {
            mODamageable.Health.SetCurrentHealth(mORecord.HitPoints);
            miLastHealth = mODamageable.CurrentHealth;
        }

        fTrace("refreshed from data: goal {0} gtarg {1} attitude {2}", mORecord.Goal, mORecord.GTarg, mORecord.Attitude);
    }

    private void Awake()
    {
        mODamageable = GetComponent<UWDamageable>();
        mODamageable.Damaged += fOnDamaged;
    }

    private void OnEnable()
    {
        if (!msActive.Contains(this))
            msActive.Add(this);
    }

    private void OnDisable()
    {
        msActive.Remove(this);
    }

    private void OnDestroy()
    {
        msActive.Remove(this);

        if (mODamageable != null)
            mODamageable.Damaged -= fOnDamaged;
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

            return;
        }

        transform.position = Vector3.Lerp(mOPictureFrom, mOBody, lfFraction);
    }

    // ------------------------------------------------- The loop (contract section 1)

    /// <summary>
    /// Called by UWCritterDriver once per frame after the level clock advanced: while the
    /// creature is due, apply the stored Motion, report the step, let the brain decide anew
    /// and store the next Motion. The order is the original's (spec 1.4): the body moves with
    /// the values the LAST goal routine left in the record, then the mind runs.
    /// </summary>
    public void RunDueUpdates(UWCritterClock pOClock)
    {
        if (mOBrain == null || mbRemoved || pOClock == null)
            return;

        fEnsurePlayer();

        for (int liGuard = 0; liGuard < MaxUpdatesPerFrame && pOClock.IsDue(mORecord.DueSlot); liGuard++)
        {
            int liSlotBefore = mORecord.DueSlot;

            fApplyMotion();

            UWCritterBrain.Motion lOMotion = mOBrain.Update(this);

            mOMotion = lOMotion;
            mbHasMotion = true;

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

    /// <summary>After an update: the picture's facing and frame. The animator shows exactly
    /// the Motion's slot and frame until the next update (deviation 20).</summary>
    private void fShowMotion(UWCritterBrain.Motion pOMotion)
    {
        if (pOMotion.Culled)
            return;

        UWCritterAnimator lOAnimator = GetComponent<UWCritterAnimator>();

        if (lOAnimator != null)
            lOAnimator.ShowMotion(pOMotion.Animation, pOMotion.Frame);

        if (pOMotion.Animation >= 1 && pOMotion.Animation <= 3)
            miAttackIndex = pOMotion.Animation - 1;
    }

    // ------------------------------------------------- Applying a Motion (contract section 3)

    /// <summary>
    /// The physics of the previous decision: one displacement of StepEighths along the fine
    /// heading (a flier also its vertical component), through the clearance probes. No
    /// sliding (deviation 13 as decided): a step that cannot be taken whole ends at the last
    /// good position along the line and the collision is reported; the brain then sets its
    /// blocked bit, tries the door or wanders. A wall hit at a slant turns the heading along
    /// the wall instead and the step goes on (fTryDeflectedStep, since 2026-09-28).
    /// </summary>
    private void fApplyMotion()
    {
        mOLastStep = default(StepResult);

        if (!mbHasMotion || mOMotion.Culled || !mOMotion.Moves)
        {
            fWritePositionToRecord(true);

            return;
        }

        Vector3 lOFrom = mOBody;
        float lfDistance = mOMotion.StepEighths * WorldUnitsPerEighth;
        bool lbMoved = false;

        if (lfDistance > 0.01f)
        {
            lbMoved = fStepAlong(lOFrom, mOMotion.FineHeading, lfDistance);
        }

        if (mbFlying)
            fApplyFlierHeight();

        // LAVA BURNS AT 1 IN 5 PER MOVING UPDATE, with 1 point of PLAIN fire
        // (ApplyProjectileMotion_seg029_29EE_61A, label 7F4: RNG % 5 == 0, DamageObject with 1
        // and type 8 - read 2026-10-02, per user: "align the creatures with the original").
        // Until then ours burned on every update with the magical fire 0x0B, five times as fast,
        // and a magic resistance rolled against it. The resistances of COMOBJ decide in UWHealth.
        // Charged to nobody (attacker 0).
        if (lbMoved && !mbFlying && fIsLava(fGetTileAt(mOBody)) && mODamageable != null && !mODamageable.IsDestroyed
            && UWRandom.Next(LavaBurnChance) == 0)
        {
            miPendingAttacker = 0;
            mODamageable.ApplyDamage(1, UWDamageTypes.PlainFire);
            miPendingAttacker = PlayerIndex;
        }

        // DROWNING, the counterpart of the lava burn above (seg006_1477_476, asm lines
        // 41417-41460): the land creature's motion callback reads the tile state, and when
        // the state masked with 0xF8 equals 0x10 - the water bit 0x10 set and the object bit
        // 0x80 clear, so water under the feet with nothing to stand on - the creature is
        // dead. Only in an update in which the physics ran, exactly like the lava damage.
        // Already dying is left alone, otherwise the next step would report it again and the
        // brain would never get past the death frame.
        if (lbMoved && !mbFlying && !mbSwimming && mORecord != null
            && mORecord.Animation != UWCritterBrain.AnimDying && fIsDrowningAt(mOBody))
            mOLastStep.Drowned = true;

        fWritePositionToRecord(true);

        // The picture: from where it is now to the new logical position over the interval.
        mOPictureFrom = transform.position;
        mfPictureStart = Time.time;
        mfPictureSeconds = fGetIntervalSeconds(mOMotion.Interval);

        if ((mOBody - mOPictureFrom).sqrMagnitude < 0.0001f)
        {
            transform.position = mOBody;
            mfPictureSeconds = 0f;
        }
    }

    /// <summary>The seconds one interval of slots lasts: 16 PIT ticks per slot at 256 per
    /// second, twice as long while the player's Speed halves the world's slot advances.</summary>
    private static float fGetIntervalSeconds(int piInterval)
    {
        float lfSeconds = Mathf.Max(1, piInterval) * UWCritterRules.SlotPitTicks / PitTicksPerSecond;

        return UWCritterDriver.PlayerHasSpeed ? lfSeconds * 2f : lfSeconds;
    }

    /// <summary>
    /// The step itself. First the whole displacement; if a probe refuses it, halves of it up
    /// to three times - the last good position along the line. Whatever refused the whole
    /// step is what the brain hears about (a door, the player, another creature, a wall).
    /// Returns whether the body moved at all.
    /// </summary>
    private bool fStepAlong(Vector3 pOFrom, int piFineHeading, float pfDistance)
    {
        miStartClearanceFailures = fClearanceFailures(pOFrom, mbFlying ? fGetFloorHeight(pOFrom) : pOFrom.y);
        miStartShoreFailures = fGetShoreFailures(pOFrom);
        mbClearanceRelaxed = miStartClearanceFailures > 0;

        Vector3 lOOffset = fHeadingToDirection(piFineHeading) * pfDistance;
        BlockInfo lOBlock;

        if (fTryStepTo(pOFrom, lOOffset, out lOBlock))
            return true;

        mOLastStep.Collided = true;
        fReportBlock(lOBlock);

        if (lOBlock.Kind == BlockKindEnum.Wall && fTryDeflectedStep(pOFrom, piFineHeading, pfDistance))
            return true;

        float lfFraction = 0.5f;
        bool lbMoved = false;

        for (int liAt = 0; liAt < 3 && !lbMoved; liAt++)
        {
            BlockInfo lOPartBlock;

            lbMoved = fTryStepTo(pOFrom, lOOffset * lfFraction, out lOPartBlock);
            lfFraction *= 0.5f;
        }

        return lbMoved;
    }

    /// <summary>
    /// THE WALL DEFLECTION (UWCritterRules.TryDeflectAlongWall, per user 2026-09-28: a
    /// creature wandering diagonally kept walking into the corridor walls - every quarter turn
    /// of the bump gave it another diagonal). The direction the wall runs is found from the two
    /// halves of the step: the east-west part refused and the north-south part free means a
    /// wall running north-south, and the other way round; both refused or both free (a corner)
    /// gives no direction and no deflection. A deflected step goes the whole distance along the
    /// wall; the new heading is written into byte 9 and reported, so the brain keeps it and
    /// sets no blocked bit. A diagonal wall gives its direction from its cut line (per user,
    /// 2026-09-28, the original slid Biden along one). The original finds the direction from
    /// the collision type (table 0x433 in seg030_2B26_BDF) and snaps a heading already along
    /// the wall to the tile edge; the snap is not built.
    /// </summary>
    private bool fTryDeflectedStep(Vector3 pOFrom, int piFineHeading, float pfDistance)
    {
        Vector3 lOOffset = fHeadingToDirection(piFineHeading) * pfDistance;
        BlockInfo lOIgnored;

        // Probes only (fCanStandAt); fTryStepTo would move the body.
        bool lbAcrossFree = Mathf.Abs(lOOffset.x) > 0.01f
            && fCanStandAt(pOFrom + new Vector3(lOOffset.x, 0f, 0f), out float lfIgnored, out lOIgnored);
        bool lbAlongFree = Mathf.Abs(lOOffset.z) > 0.01f
            && fCanStandAt(pOFrom + new Vector3(0f, 0f, lOOffset.z), out lfIgnored, out lOIgnored);

        // THE CANDIDATE WALL DIRECTIONS, most likely first (per user, 2026-09-28: Biden slid
        // along the diagonal wall now, but stuck where a straight wall runs into a diagonal one
        // and at a corner - two walls at once, and one direction with one whole step was not
        // enough; the original tries up to two further steps after a deflection). A diagonal
        // wall's cut line first, then the wall the two halves of the step point to, then the
        // other axis.
        List<int> lOWalls = new List<int>(3);

        if (fTryGetBlockingDiagonalDirection(pOFrom + lOOffset, out int liDiagonal))
            lOWalls.Add(liDiagonal);

        int liAxis = lbAlongFree && !lbAcrossFree ? 0 : UWCritterRules.QuarterTurn;

        lOWalls.Add(liAxis);
        lOWalls.Add(liAxis ^ UWCritterRules.QuarterTurn);

        foreach (int liWallDirection in lOWalls)
        {
            if (!UWCritterRules.TryDeflectAlongWall(piFineHeading, liWallDirection, out int liHeading))
                continue;

            // Along the wall the whole step, else the last good position of halves of it.
            Vector3 lODeflected = fHeadingToDirection(liHeading) * pfDistance;

            for (float lfFraction = 1f; lfFraction > 0.2f; lfFraction *= 0.5f)
            {
                if (!fTryStepTo(pOFrom, lODeflected * lfFraction, out lOIgnored))
                    continue;

                mORecord.FineHeading = liHeading;
                mOLastStep.HeadingDeflected = true;

                return true;
            }
        }

        return false;
    }

    /// <summary>What a refused step ran into, for the StepResult.</summary>
    private enum BlockKindEnum
    {
        None,
        Wall,
        Terrain,
        Door,
        Player,
        Creature,
        ForceField
    }

    private struct BlockInfo
    {
        public BlockKindEnum Kind;

        public UWObject Door;

        public UWCritter Creature;

        public string Reason;
    }

    /// <summary>Fills the StepResult from the refused step: the object hit and its item id
    /// (a door by class 0x14, the player 0x7F, a creature its id), the closed-door flag for
    /// land creatures only - fliers and swimmers have no door branch in the original (spec
    /// 7.5) and merely report the object.</summary>
    private void fReportBlock(BlockInfo pOBlock)
    {
        switch (pOBlock.Kind)
        {
            case BlockKindEnum.Door:
                mOLastStep.HitObject = true;
                mOLastStep.HitObjectIndex = fIndexOf(pOBlock.Door);
                mOLastStep.HitObjectItemId = pOBlock.Door != null ? pOBlock.Door.ID & 0x1FF : 0;
                mOLastStep.HitObjectIsCreature = false;
                mOLastStep.HitClosedDoor = !mbFlying && !mbSwimming;
                break;

            case BlockKindEnum.Player:
                mOLastStep.HitObject = true;
                mOLastStep.HitObjectIndex = PlayerIndex;
                mOLastStep.HitObjectItemId = UWCritterBrain.PlayerItemId;
                mOLastStep.HitObjectIsCreature = true;
                break;

            case BlockKindEnum.Creature:
                mOLastStep.HitObject = true;
                mOLastStep.HitObjectIndex = pOBlock.Creature != null ? pOBlock.Creature.miIndex : 0;
                mOLastStep.HitObjectItemId = pOBlock.Creature != null && pOBlock.Creature.mONpc != null
                    ? pOBlock.Creature.mONpc.ID & 0x1FF : 0;
                mOLastStep.HitObjectIsCreature = true;
                break;

            case BlockKindEnum.ForceField:
                mOLastStep.HitObject = true;
                mOLastStep.HitObjectIndex = 0;
                mOLastStep.HitObjectItemId = ForceFieldObjectId;
                mOLastStep.HitObjectIsCreature = false;
                break;
        }
    }

    private bool fTryStepTo(Vector3 pOFrom, Vector3 pOOffset, out BlockInfo pOBlock)
    {
        pOBlock = default(BlockInfo);

        if (pOOffset.sqrMagnitude <= 0f)
            return false;

        Vector3 lOTarget = pOFrom + pOOffset;
        float lfFloor;

        if (!fCanStandAt(lOTarget, out lfFloor, out pOBlock))
            return false;

        if (mbFlying)
            lOTarget.y = Mathf.Max(lfFloor, pOFrom.y);
        else
            lOTarget.y = lfFloor + fGetSwimmerOffset();

        // A STEP THAT CHANGES THE HEIGHT is worth a line: the user saw a lurker briefly at
        // y 54 instead of 16 while it was close to the wall (2026-09-16).
        if (!mbFlying && Mathf.Abs(lOTarget.y - pOFrom.y) > 4f)
        {
            UWTilePos lOTile = mOLevelLoader != null ? mOLevelLoader.WorldPositionToTile(lOTarget) : new UWTilePos(0, 0);
            UWTile lOTileData = fGetTileAt(lOTarget);

            fTrace("height {0:F0} -> {1:F0} on tile {2}/{3} (tile floor {4})",
                pOFrom.y, lOTarget.y, lOTile.X, lOTile.Y, lOTileData == null ? -1 : lOTileData.FloorHeight);
        }

        mOBody = lOTarget;

        return true;
    }

    /// <summary>
    /// A flier's height follows its pitch: (pitch - 16) * 64 sub-units per PIT tick over
    /// interval * 16 ticks (spec 7.6), which in world units is (pitch - 16) * interval per
    /// update. Clamped between the floor and the ceiling less the body; touching the ceiling
    /// is reported so the brain turns the pitch down.
    /// </summary>
    private void fApplyFlierHeight()
    {
        float lfClimb = (mOMotion.Pitch - UWCritterRules.FlierPitchLevel) * mOMotion.Interval;
        float lfFloor = fGetFloorHeight(mOBody);

        Collider lOCollider = GetComponent<Collider>();
        float lfBody = lOCollider != null ? lOCollider.bounds.size.y : 16f;
        float lfTop = UWLevelMeshBuilder.CeilingHeight - lfBody;

        float lfWanted = mOBody.y + lfClimb;

        if (lfWanted >= lfTop)
        {
            lfWanted = lfTop;
            mOLastStep.TouchedCeiling = true;
        }

        if (lfWanted < lfFloor)
            lfWanted = lfFloor;

        mOBody.y = lfWanted;
    }

    /// <summary>The 1 in 5 of the lava burn on a moving update (RNG % 5 == 0).</summary>
    private const int LavaBurnChance = 5;

    /// <summary>
    /// The record's position fields the host owns (contract section 6): fine position, zpos
    /// and the current tile in word 0x16, and the object's tile list relinked when the tile
    /// changed. The fine position is the sub-tile spot nearest the body, the same rounding
    /// UWWorldCapture uses when it saves.
    /// </summary>
    private void fWritePositionToRecord(bool pbRelink)
    {
        if (mORecord == null)
            return;

        UWTilePos lOTile = UWTileQueries.WorldToTile(mOBody.x, mOBody.z);

        if (pbRelink && mOLevelLoader != null && mONpc != null
            && (lOTile.X != mORecord.TileX || lOTile.Y != mORecord.TileY))
            mOLevelLoader.MoveObjectData(mONpc, lOTile.X, lOTile.Y);

        mORecord.TileX = lOTile.X;
        mORecord.TileY = lOTile.Y;
        mORecord.FineX = UWTileQueries.WorldToSubTile(mOBody.x, lOTile.X);
        mORecord.FineY = UWTileQueries.WorldToSubTile(mOBody.z, lOTile.Y);
        mORecord.ZPos = Mathf.Clamp(UWUnits.RoundToInt(mOBody.y / UWWorldScale.ZPosStep), 0, 0x7F);
    }

    /// <summary>
    /// Does this step bring the creature CLOSER to its target than it stands now? Only such a
    /// step may go over a water edge - see the drop rule in fCanStandAt. Without a target
    /// nothing may.
    /// </summary>
    private bool fLeadsTowardsTarget(Vector3 pOAt)
    {
        if (!fEnsurePlayer() || mOPlayerTransform == null)
            return false;

        // ONLY A CREATURE THAT IS AFTER THE PLAYER has him as its target (goal 5, target 1).
        // Until 2026-09-28 every creature measured itself against the player, whatever it was
        // doing: Hagbard, wandering in his room on level 1, stepped into the pool beside him
        // while the player swam in the river behind the wall, and drowned (per user: "I cannot
        // find Hagbard in SAVE1"; his record in the save had its id cleared). In the original a
        // creature without a path to the player never gets in - its path search treats water
        // as impassable (see fCanStandAt).
        if (mORecord == null || mORecord.Goal != UWNpc.GoalAttack || mORecord.GTarg != UWCritterBrain.PlayerIndex)
            return false;

        // AND THE TARGET MUST BE IN THE WATER ITSELF. The creature follows the player in; it
        // does not wade in on its own. The direction alone was not enough - ours still drowned
        // itself while backing off at the edge (per user, 2026-09-20, second report).
        if (!fIsWater(fGetTileAt(mOPlayerTransform.position)))
            return false;

        Vector3 lOTarget = mOPlayerTransform.position;

        lOTarget.y = 0f;

        Vector3 lOFrom = new Vector3(mOBody.x, 0f, mOBody.z);
        Vector3 lOTo = new Vector3(pOAt.x, 0f, pOAt.z);

        return (lOTo - lOTarget).sqrMagnitude < (lOFrom - lOTarget).sqrMagnitude;
    }

    /// <summary>A fine heading 0..255 (0 = +Z north, 64 = +X east) as a world direction.</summary>
    private static Vector3 fHeadingToDirection(int piFineHeading)
    {
        float lfRadians = (piFineHeading & 0xFF) * (2f * Mathf.PI / 256f);

        return new Vector3(Mathf.Sin(lfRadians), 0f, Mathf.Cos(lfRadians));
    }

    // ------------------------------------------------- ICritterHost: time and chance

    long ICritterHost.Clock => UWScene.CritterClock.Clock;

    int ICritterHost.Random(int piN)
    {
        return piN <= 1 ? 0 : Random.Range(0, piN);
    }

    UWCritterAlarm ICritterHost.Alarm => UWScene.CritterAlarm;

    // ------------------------------------------------- ICritterHost: the level

    int ICritterHost.FloorLevelAt(int piTileX, int piTileY)
    {
        UWTile lOTile = fGetTileAt(piTileX, piTileY);

        return lOTile != null ? lOTile.FloorHeight >> 4 : 0;
    }

    bool ICritterHost.IsMagicBlockedAt(int piTileX, int piTileY)
    {
        UWTile lOTile = fGetTileAt(piTileX, piTileY);

        return lOTile != null && lOTile.NoMagicAllowed;
    }

    /// <summary>Tybal's rule: his lair, level 7, while the orb stands (UWTybalOrbRules, the bit
    /// PLAYER.DAT 0x60 bit 5). Until 2026-09-23 this asked for level index 7 - level 8 - and
    /// never for the bit.</summary>
    bool ICritterHost.TybalOrbStands => mOLevelLoader != null && UWTybalOrbRules.OrbStands(mOLevelLoader.CurrentLevelIndex);

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
        lOTarget.Loudness = mOPlayer.Quietness;
        lOTarget.Visibility = mOPlayer.Visibility;
        lOTarget.HitPoints = mOPlayer.CurrentHP > 0f ? Mathf.Max(1, (int)mOPlayer.CurrentHP) : 0;
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
        lOTarget.IsCreature = ((lOObject.ID >> 6) & 7) == 1;
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
    bool ICritterHost.IsStraightLineClear(int piToTileX, int piToTileY)
    {
        if (mORecord == null)
            return false;

        int liX = mORecord.TileX;
        int liY = mORecord.TileY;
        int liDx = piToTileX - liX;
        int liDy = piToTileY - liY;
        int liSteps = Mathf.Abs(liDx) + Mathf.Abs(liDy);

        if (liSteps == 0)
            return true;

        if (liSteps > StraightLineMaxTiles)
            return false;

        fSetPathRules();

        int liStepX = liDx > 0 ? 1 : (liDx < 0 ? -1 : 0);
        int liStepY = liDy > 0 ? 1 : (liDy < 0 ? -1 : 0);
        int liErr = Mathf.Abs(liDx) - Mathf.Abs(liDy);

        while (liX != piToTileX || liY != piToTileY)
        {
            int liNextX = liX;
            int liNextY = liY;

            // The axis with the larger remaining error moves first - a staircase along the
            // line, each riser a four-neighbour step the passability rule understands.
            if (liErr >= 0 && liX != piToTileX)
            {
                liNextX += liStepX;
                liErr -= Mathf.Abs(liDy);
            }
            else
            {
                liNextY += liStepY;
                liErr += Mathf.Abs(liDx);
            }

            if (!((UWTilePath.IWalker)this).CanEnterTile(liX, liY, liNextX, liNextY))
                return false;

            liX = liNextX;
            liY = liNextY;
        }

        return true;
    }

    /// <summary>The door and budget rules the next search applies, from the brain's own
    /// table row (door skill nonzero) and the record (the range budget of spec 7.4).</summary>
    private void fSetPathRules()
    {
        mbPathMayOpenDoors = mbHasStats && mOStats.DoorSkill != 0;
        miPathBudget = mbHasStats
            ? UWCritterRules.GetPathRangeBudget(mORecord.Attitude, mOStats.Vitality, mORecord.HitPoints,
                mOStats.Morale, mORecord.ExcludedFromPathBudget)
            : 0;
    }

    /// <summary>
    /// The host's path search in place of PathFindBetweenTiles (deviation 14): UWTilePath's
    /// breadth-first search from the target, the next tile only. The found tile is reused for
    /// half a second while the creature stays on the same tile and aims at the same
    /// destination. The range budget and the door rule apply per edge in CanEnterTile.
    ///
    /// HOW FAR IT LOOKS is the original's window since 2026-09-21 (UWTilePath.SearchMargin),
    /// not our old ceiling of 400 examined tiles.
    /// </summary>
    bool ICritterHost.TryGetNextPathTile(int piToTileX, int piToTileY, int piRangeBudget, bool pbMayOpenDoors,
        out int piNextTileX, out int piNextTileY)
    {
        piNextTileX = piToTileX;
        piNextTileY = piToTileY;

        if (mORecord == null || mOLevelLoader == null)
            return false;

        UWTilePos lOFrom = new UWTilePos(mORecord.TileX, mORecord.TileY);
        UWTilePos lOTo = new UWTilePos(piToTileX, piToTileY);

        if (!(Time.time < mfPathValidUntil && lOFrom == mOPathFrom && lOTo == mOPathTo))
        {
            mbPathMayOpenDoors = pbMayOpenDoors;
            miPathBudget = piRangeBudget;

            mbPathFound = UWTilePath.TryGetNextStep(this, lOFrom, lOTo, out mOPathNext);
            mOPathFrom = lOFrom;
            mOPathTo = lOTo;
            mfPathValidUntil = Time.time + PathRefreshSeconds;
        }

        if (!mbPathFound)
            return false;

        piNextTileX = mOPathNext.X;
        piNextTileY = mOPathNext.Y;

        return true;
    }

    /// <summary>
    /// Whether this creature can step from one tile onto the adjacent one - for the path
    /// search and the straight-line test. The same rules as for the single step (see
    /// fCanStandAt), applied to whole tiles: shape, ground, height difference; plus the
    /// original's path terms (spec 7.4): a closed door is passable only for a creature that
    /// could open it, lava only while the range budget covers its cost of 2.
    /// </summary>
    bool UWTilePath.IWalker.CanEnterTile(int piFromX, int piFromY, int piToX, int piToY)
    {
        UWTile lOFrom = fGetTileAt(piFromX, piFromY);
        UWTile lOTo = fGetTileAt(piToX, piToY);

        if (lOFrom == null || lOTo == null)
            return false;

        int liSide = UWTilePath.GetSide(piFromX, piFromY, piToX, piToY);

        if (!UWTilePath.AllowsEdge(lOFrom, liSide)
            || !UWTilePath.AllowsEdge(lOTo, UWTilePath.GetOppositeSide(liSide)))
            return false;

        if (!mbFlying && mbSwimming != fIsWater(lOTo))
            return false;

        if (!mbPathMayOpenDoors && fFindClosedDoor(lOTo) != null)
            return false;

        if (!mbFlying && !mbSwimming && fIsLava(lOTo) && !fIsLava(lOFrom) && miPathBudget < 2 && !fIsFireResistant())
            return false;

        UWSettings lOSettings = UWSettings.Instance;
        float lfMaxStep = lOSettings != null ? lOSettings.CritterMaxStepHeight : 16f;

        return mbFlying || Mathf.Abs(lOTo.FloorHeight - lOFrom.FloorHeight) <= lfMaxStep;
    }

    StepResult ICritterHost.LastStep => mOLastStep;

    /// <summary>
    /// Goal 3's relocation: the body onto the tile centre at floor height, relinked. UW.EXE
    /// checks nothing; here a solid tile or one outside the map refuses (deviation 37, kept
    /// for safety) - a creature inside a wall would be worse than a skipped jump.
    /// </summary>
    bool ICritterHost.Teleport(int piTileX, int piTileY)
    {
        UWTile lOTile = fGetTileAt(piTileX, piTileY);

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
            return false;

        if (!mbFlying && mbSwimming != fIsWater(lOTile))
            return false;

        Vector3 lOCentre = new Vector3(UWUnits.TileCentreToWorldAxis(piTileX), 0f, UWUnits.TileCentreToWorldAxis(piTileY));

        lOCentre.y = lOTile.FloorHeight + (mbFlying ? Mathf.Max(0f, mOBody.y - fGetFloorHeight(mOBody)) : fGetSwimmerOffset());

        mOBody = lOCentre;
        transform.position = lOCentre;
        mfPictureSeconds = 0f;
        fWritePositionToRecord(true);
        fTrace("teleported to {0}/{1}", piTileX, piTileY);

        return true;
    }

    // ------------------------------------------------- ICritterHost: doors (spec 7.5)

    /// <summary>ObjectUse on the door: opens it unless it is spiked or locked (the original's
    /// ObjectUse fails on a locked door without a key). True when it is open afterwards.</summary>
    bool ICritterHost.UseDoor(int piDoorIndex)
    {
        UWObject lODoor = fObjectAt(piDoorIndex);
        UWEntityInfo lOEntity;

        if (lODoor == null || mOLevelLoader == null || !mOLevelLoader.TryGetEntity(lODoor, out lOEntity) || lOEntity == null)
            return false;

        IUsableDoor lIDoor = lOEntity.GetComponentInParent<IUsableDoor>();

        if (lIDoor == null)
            return false;

        if (!lIDoor.IsClosed)
            return true;

        if (UWObjectMechanics.IsDoorSpiked(lODoor))
            return false;

        UWDoorLock lOLock = lOEntity.GetComponentInParent<UWDoorLock>();

        if (lOLock != null && mOLevelLoader.CurrentLevel != null)
            lOLock.EnsureResolved(lODoor, mOLevelLoader.CurrentLevel.Masterlist);

        if (lOLock != null && lOLock.IsLocked)
            return false;

        lIDoor.StartUsingDoor();
        UWTriggerSystem.TryFireOpenTrigger(lODoor, mOLevelLoader, UWScene.Interaction);
        fTrace("opened door {0}", piDoorIndex);

        return !lIDoor.IsClosed;
    }

    /// <summary>
    /// UnlockDoor_seg040_352B_1D3B with minus the skill: a lockpick attempt by the creature,
    /// UWLockRules.Unlock with the pick path - read 2026-09-25: the skill against three times
    /// the lock difficulty, and a lock without its keep bit is gone afterwards. A success opens
    /// the door as ObjectUse would.
    /// </summary>
    void ICritterHost.PickDoor(int piDoorIndex, int piSkill)
    {
        UWObject lODoor = fObjectAt(piDoorIndex);
        UWEntityInfo lOEntity;

        if (lODoor == null || mOLevelLoader == null || mOLevelLoader.CurrentLevel == null
            || !mOLevelLoader.TryGetEntity(lODoor, out lOEntity) || lOEntity == null)
            return;

        IUsableDoor lIDoor = lOEntity.GetComponentInParent<IUsableDoor>();
        UWDoorLock lOLock = lOEntity.GetComponentInParent<UWDoorLock>();

        if (lIDoor == null || lOLock == null)
            return;

        lOLock.EnsureResolved(lODoor, mOLevelLoader.CurrentLevel.Masterlist);

        if (!lOLock.IsLocked)
            return;

        bool lbUnlocked = UWLockRules.Unlock(lOLock.State, null, null, !lIDoor.IsClosed, -piSkill)
            == UWLockRules.Outcome.Unlocked;

        fTrace("picks door {0}: {1}", piDoorIndex, lbUnlocked ? "unlocked" : "failed");

        if (!lbUnlocked)
            return;

        UWSoundEffects.PlayAt(UWSoundEffects.Lockpick, mOBody);

        if (lIDoor.IsClosed)
        {
            lIDoor.StartUsingDoor();
            UWTriggerSystem.TryFireOpenTrigger(lODoor, mOLevelLoader, UWScene.Interaction);
        }
    }

    /// <summary>DamageObject type 4 on the door: its UWDamageable takes the bash; UWDoorDamage
    /// bursts it open at zero.</summary>
    void ICritterHost.BashDoor(int piDoorIndex, int piDamage)
    {
        UWObject lODoor = fObjectAt(piDoorIndex);
        UWEntityInfo lOEntity;

        if (lODoor == null || mOLevelLoader == null || !mOLevelLoader.TryGetEntity(lODoor, out lOEntity) || lOEntity == null)
            return;

        UWDamageable lODamageable = lOEntity.GetComponentInParent<UWDamageable>();

        if (lODamageable == null)
            return;

        fTrace("bashes door {0} for {1}", piDoorIndex, piDamage);
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, lOEntity.transform.position, piDamage << 2);
        lODamageable.ApplyDamage(piDamage, UWDamageTypes.Physical);
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

        if (fTryDoorInReach(lfDefenderDistance, out UWDoorLock lODoor))
        {
            fStrikeObject(lODoor.GetComponent<UWDamageable>(), lODoor.Centre, piAttack, piCharge, piFlank);

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
            fStrikeCreature(fIndexOf(lODefender.mONpc), lODefender, piAttack, piCharge, piFlank);

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

        if (lOObject != null && !lOObject.IsDestroyed && lOObject.GetComponentInParent<UWCritter>() == null)
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
    /// (the portcullis) takes the blow and nothing happens to it.
    /// </summary>
    private void fStrikeObject(UWDamageable pOObject, Vector3 pOAt, int piAttack, int piCharge, int piFlank)
    {
        fGetAttackNumbers(piAttack, out int liUnusedScore, out int liBase);

        int liRolled = UWCritterCombat.ScaleByCharge(UWCritterCombat.RollDamage(liBase), piCharge, piFlank);

        fEvent("blow {0}: hits {1} for {2}", piAttack, pOObject != null ? pOObject.name : "an object", liRolled);

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
        UWDoorLock lODoor = pOObject != null ? pOObject.GetComponent<UWDoorLock>() : null;

        if (lODoor != null && mOLevelLoader != null)
        {
            IUsableDoor lIDoor = lODoor.GetComponent<IUsableDoor>();
            int liDoorZ = (lIDoor != null && !lIDoor.IsClosed ? OpenDoorRaiseZ : 0) + DoorImpactAboveZ;

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

        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, lOAt, liRolled << 2);

        if (pOObject != null && !pOObject.IsDestroyed)
            pOObject.ApplyDamage(liRolled, UWDamageTypes.Physical);
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
    private bool fTryDoorInReach(float pfLimit, out UWDoorLock pODoor)
    {
        pODoor = null;

        Vector3 lODirection = fHeadingToDirection(mORecord.FullFacing);
        float lfReach = UWCritterRules.AttackReachEighths * UWWorldScale.SubTileStep;
        float lfTouch = (UWCritterRules.AttackReachRadius + DoorRadiusEighths) * UWWorldScale.SubTileStep;
        float lfBest = pfLimit;

        foreach (UWDoorLock lOCandidate in UWDoorLock.All)
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
        Vector3 lODirection = fHeadingToDirection(mORecord.FullFacing);
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
            // (UWSpellProjectile skips them the same way).
            if (lOCandidate.collider.GetComponentInParent<UWSpellProjectile>() != null)
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

        if (pOCollider.GetComponentInParent<UWSpellProjectile>() != null)
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
            : fGetEyePosition() + (fHeadingToDirection(mORecord.FullFacing)
                * (UWCritterRules.AttackReachRadius * UWWorldScale.SubTileStep));

        mOLevelLoader.SpawnEffectAt(UWObjectMechanics.FlashEffectObjectId, lOAt, BlowEffectSeconds);
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, lOAt);
    }

    /// <summary>The score and the base damage of this attack (NPCExecuteAttack labels
    /// 1629-1679): the table's to-hit and damage bytes of attack i, half the signed byte
    /// 0x11 and a fifth of byte 5, the strong individual's 7 + RNG(6) and 4 + RNG(12).</summary>
    private void fGetAttackNumbers(int piAttack, out int piScore, out int piDamage)
    {
        if (!mbHasStats)
        {
            piScore = 0;
            piDamage = 3;

            return;
        }

        piScore = UWCritterCombat.GetAttackScore(mOStats, piAttack);
        piDamage = UWCritterCombat.GetAttackDamage(mOStats, piAttack);

        if (mORecord.IsStrong)
        {
            piScore += UWCritterCombat.GetStrongScoreBonus();
            piDamage += UWCritterCombat.GetStrongDamageBonus();
        }
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

        int liOwnFeet = mORecord.ZPos;
        int liPlayerFeet = mOPlayer.GetFeetZPos();

        int liPart = UWArmourProtection.PickBodyPart(liPlayerFeet, liPlayerFeet + mOPlayer.GetBodyHeightZ(),
            liOwnFeet, liOwnFeet + liOwnHeight);

        int liScore;
        int liBase;
        fGetAttackNumbers(piAttack, out liScore, out liBase);

        // THE CHECK VALUE IS THE PLAYER'S ROW BYTE 0x12, not his Defence skill: the original
        // puts the skill plus half the skill of the weapon in hand there
        // (PlayerStatusUpdate_ovr133_784, labels 86A-938, see UWPlayerCritterRow).
        int liDefence = mOPlayer.DefenceCheck;

        UWSkillCheck.ResultEnum leResult = UWCritterCombat.ResolveHit(liScore, piFlank,
            mOPlayer.GetProtectionAt(liPart), liDefence);

        if (!UWSkillCheck.IsSuccess(leResult))
        {
            fEvent("blow {0} {1} (score {2} + flank {3} vs defence {4})", piAttack,
                leResult == UWSkillCheck.ResultEnum.CriticalFailure ? "missed badly" : "missed",
                liScore, piFlank, liDefence);
            UWSoundEffects.PlayAt(UWSoundEffects.Miss, mOBody);

            return;
        }

        bool lbCritical = leResult == UWSkillCheck.ResultEnum.CriticalSuccess;

        if (lbCritical)
        {
            liBase *= UWCritterCombat.GetCriticalMultiplier();

            UWGameUI lOUi = UWScene.GameUi;

            if (lOUi != null)
                lOUi.FlashWindowColour(UWCritterCombat.CriticalFlashColour, CriticalFlashSeconds);

            // The piece in the slot of UWCritterCombat.GetCriticalEquipmentSlot wears by 2d4
            // (UWEquipmentWear, built 2026-09-28 - this said "only objects 0-15, nothing
            // happens", but the call uses the armour test).
            Interaction lOWearer = UWScene.Interaction;

            if (lOWearer != null)
                lOWearer.WearOnCriticalHit(liPart);
        }

        int liRolled = UWCritterCombat.ScaleByCharge(UWCritterCombat.RollDamage(liBase), piCharge, piFlank);

        // Sound 3 at the avatar, volume damage * 4 - before the armour, so an absorbed blow
        // still sounds (label 938).
        UWSoundEffects.PlayAtAvatar(UWSoundEffects.HitPlayer, liRolled << 2);

        int liDamage = UWCritterCombat.SubtractArmour(liRolled, mOPlayer.GetArmourAt(liPart));

        liDamage = UWCritterCombat.HalveOnEasy(liDamage, mOPlayer.IsEasyDifficulty);

        fEvent("blow {0} {1} part {2} for {3} of {4} (charge {5}, flank {6})", piAttack,
            lbCritical ? "critical" : "hit", liPart, liDamage, liRolled, piCharge, piFlank);

        // A BLOW IS BLUNT DAMAGE: DamageObject with type 4 (label A3D).
        mOPlayer.ApplyDamage(liDamage, UWDamageTypes.Physical);
        UWMusic.OnPlayerDamaged(mOPlayer.CurrentHP, mOPlayer.MaxHP);

        fTryPoisonPlayer();
    }

    /// <summary>The red flash of a critical hit on the player, as short as the curse's.</summary>
    private const float CriticalFlashSeconds = 0.1f;

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

        if (pOOther.mORecord != null && pOOther.mORecord.IsAlly == mORecord.IsAlly)
        {
            fEvent("blow {0} refused: same side as {1}", piAttack, piTargetIndex);

            return;
        }

        int liOwnRadius;
        int liOwnHeight;
        fGetOwnSize(out liOwnRadius, out liOwnHeight);

        int liScore;
        int liBase;
        fGetAttackNumbers(piAttack, out liScore, out liBase);

        int liDefence = pOOther.mbHasStats ? pOOther.mOStats.DefensePower : pOOther.mODamageable.Defence;
        UWSkillCheck.ResultEnum leResult = UWCritterCombat.ResolveHit(liScore, piFlank, 0, liDefence);
        int liDamage = 0;
        int liPart = pOOther.fPickPartHitBy(mORecord.ZPos, mORecord.ZPos + liOwnHeight);

        if (UWSkillCheck.IsSuccess(leResult))
        {
            if (leResult == UWSkillCheck.ResultEnum.CriticalSuccess)
                liBase *= UWCritterCombat.GetCriticalMultiplier();

            int liRolled = UWCritterCombat.ScaleByCharge(UWCritterCombat.RollDamage(liBase), piCharge, piFlank);

            UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, pOOther.mOBody, liRolled << 2);

            liDamage = UWCritterCombat.SubtractArmour(liRolled, pOOther.GetArmourOfPart(liPart));
        }
        else
            UWSoundEffects.PlayAt(UWSoundEffects.Miss, mOBody);

        fEvent("blow {0} at creature {1}: {2} part {3} for {4}", piAttack, piTargetIndex, leResult, liPart, liDamage);

        pOOther.miPendingAttacker = miIndex;
        pOOther.mODamageable.ApplyDamage(liDamage, UWDamageTypes.Physical);
        pOOther.miPendingAttacker = PlayerIndex;

        if (liDamage > 0)
            pOOther.fShowHitEffect(liPart, liDamage);
    }

    /// <summary>
    /// The aftermath of a damaging hit on this creature (AttackerAppliesFinalDamage labels
    /// B49-C17): table byte 8 bits 3-4 nonzero the blood splat (class 7 offset 0), else the
    /// impact flash (offset 0x0B), at HitZ[part] eighths of the body height above the feet.
    /// The original starts the animation at frame min(3, damage / 4); the port's effects
    /// play whole (SpawnEffectAt has no start frame), so a heavier blow shows the same splat.
    /// </summary>
    private void fShowHitEffect(int piPart, int piDamage)
    {
        if (mOLevelLoader == null || !mbHasStats)
            return;

        int liRadius;
        int liHeight;
        fGetOwnSize(out liRadius, out liHeight);

        float lfHeight = liHeight * UWWorldScale.ZPosStep * UWCritterCombat.GetHitZ(piPart) / 8f;

        mOLevelLoader.SpawnEffectAt(UWCritterCombat.GetHitEffectObjectId(mOStats),
            mOBody + (Vector3.up * lfHeight), BlowEffectSeconds);
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
    /// The armour a blow or a missile from these heights (zpos, feet and top of the
    /// attacker or of the projectile) meets: the part PickBodyHitPoint picks over this body,
    /// + 4 for a missile (MissileAttackHit label 158B), then GetArmourOfPart. Replaces the
    /// UWDamageable's single value for the player's blows (Interaction) and projectiles
    /// (UWSpellProjectile) - deviation 49.
    /// </summary>
    public int GetArmourAgainst(int piAttackerFeet, int piAttackerTop, bool pbMissile = false)
    {
        int liPart = fPickPartHitBy(piAttackerFeet, piAttackerTop);

        return GetArmourOfPart(pbMissile ? UWCritterCombat.GetMissileBodyPart(liPart) : liPart);
    }

    /// <summary>
    /// The blood or the flash of a hit that did NOT come from this creature's own melee -
    /// an arrow, a thrown rock, a bolt. The original runs the same AttackerAppliesFinalDamage
    /// for a missile (MissileAttackHit_seg022_14BF, label 158B), so the effect is the
    /// creature's own (row byte 8 bits 3-4) at the height of the part hit. Ours showed nothing
    /// at all for the player's arrow (per user, 2026-09-20).
    /// </summary>
    public void ShowHitEffect(int piAttackerFeet, int piAttackerTop, int piDamage, bool pbMissile)
    {
        int liPart = fPickPartHitBy(piAttackerFeet, piAttackerTop);

        fShowHitEffect(pbMissile ? UWCritterCombat.GetMissileBodyPart(liPart) : liPart, piDamage);
    }

    /// <summary>
    /// The poison of a hit on the player (NPCExecuteAttack labels 16A4-16EC, deviation 48):
    /// after ExecuteAttack returned 1 - a hit, even one the armour swallowed - his poison
    /// nibble must be below the row's byte 0x0F and a Poison Resistance must not scale a
    /// test damage of type 0x10 to zero; then the nibble becomes byte 0x0F &amp; 0xF. No
    /// roll, no armour hurdle.
    /// </summary>
    private void fTryPoisonPlayer()
    {
        int liPoison = mbHasStats ? mOStats.PoisonDamage : 0;

        if (!UWCritterCombat.ShouldPoison(mOPlayer.Poison, liPoison, mOPlayer.DamageTypeProof))
            return;

        fEvent("poisons the player with {0}", UWCritterCombat.GetPoisonNibble(liPoison));
        mOPlayer.ApplyPoison(UWCritterCombat.GetPoisonNibble(liPoison));
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

        fLaunchProjectile(FirstAmmunitionObjectId + piAmmoIndex, lOTarget, UWDamageTypes.Missile,
            MissileRangeTiles * UWLevelMeshBuilder.TileSpacing, piPitch);
    }

    /// <summary>
    /// SpellTrapWandCast at frame 4 of animation 0x0D. ONLY THE MISSILE SPELLS ARE BUILT
    /// (major class 5), as before: magic missile, lightning, fireball and acid cover imp,
    /// gazer, mage, fire elemental and acid slug. TODO: the area and special spells of trolls
    /// and wisps through the port's spell host.
    /// </summary>
    void ICritterHost.OnSpellCast(int piSpellIndex, int piPitch)
    {
        fEvent("spell {0} cast, pitch {1}", piSpellIndex, piPitch);

        if (piSpellIndex < 0 || piSpellIndex >= UWRunicMagic.AllSpells.Count)
            return;

        UWRunicMagic.Spell lOSpell = UWRunicMagic.AllSpells[piSpellIndex];

        if (lOSpell.MajorClass == AreaSpellMajorClass)
        {
            fCastAreaSpell(lOSpell);

            return;
        }

        if (lOSpell.MajorClass == UWSummonSpellRules.MajorClass
            && UWRunicMagic.GetEffectMinor(lOSpell.MinorClass) == SummonMonsterMinor)
        {
            fSummonMonster();

            return;
        }

        if (lOSpell.MajorClass != ProjectileSpellMajorClass)
            return;

        int liProjectile = UWRunicMagic.GetProjectileId(UWRunicMagic.GetEffectMinor(lOSpell.MinorClass));

        if (liProjectile < 0)
            return;

        CritterTarget lOTarget = ((ICritterHost)this).GetObject(mORecord.GTarg);

        if (!lOTarget.Exists)
            return;

        fLaunchProjectile(liProjectile, lOTarget, UWRunicMagic.GetProjectileDamageType(liProjectile),
            SpellRangeTiles * UWLevelMeshBuilder.TileSpacing, piPitch);
    }

    /// <summary>Spell class 6, the area spells (Sheet Lightning, Flame Wind).</summary>
    private const int AreaSpellMajorClass = 6;

    /// <summary>How far ahead the area's centre lies, in tiles (Class6Spells_seg038_3307_DBA
    /// passes distance 4 to RunCodeOnTargetsAroundObject).</summary>
    private const int AreaCentreTiles = 4;

    /// <summary>
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
    /// </summary>
    private void fCastAreaSpell(UWRunicMagic.Spell pOSpell)
    {
        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null || mOLevelLoader.UWDataImporter == null)
            return;

        if (((ICritterHost)this).IsMagicBlockedAt(mORecord.TileX, mORecord.TileY)
            || mOLevelLoader.CurrentLevelIndex == UWEndgame.VoidLevelIndex)
            return;

        Vector3 lODirection = fHeadingToDirection(mORecord.FullFacing);
        UWTilePos lOCentre = new UWTilePos(
            mORecord.TileX + Mathf.RoundToInt(lODirection.x * AreaCentreTiles),
            mORecord.TileY + Mathf.RoundToInt(lODirection.z * AreaCentreTiles));

        UWSpellHost lOHost = new UWSpellHost(UWScene.Character, UWScene.Interaction, mOLevelLoader,
            mOLevelLoader.UWDataImporter, UWScene.GameUi) { AreaCaster = this };

        fEvent("area spell {0} at {1}/{2}", UWRunicMagic.GetEffectMinor(pOSpell.MinorClass), lOCentre.X, lOCentre.Y);

        UWAreaSpellRules.CastAt(UWRunicMagic.GetEffectMinor(pOSpell.MinorClass),
            UWRunicMagic.GetAreaTargetType(pOSpell.MinorClass), lOCentre, lOHost);
    }

    /// <summary>Summon Monster's minor class in spell class 8.</summary>
    private const int SummonMonsterMinor = 4;

    /// <summary>
    /// A CREATURE SUMMONS (2026-09-27, mage 106's third spell): Class8Spells with the creature
    /// as caster - see UWSummonSpellRules.SummonByCreature. The spot lies along the creature's
    /// facing from its body; the new creature is hostile and heads for the player. Nothing on a
    /// no-magic tile or in the void, as CastSpells refuses there.
    /// </summary>
    private void fSummonMonster()
    {
        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null || mOLevelLoader.UWDataImporter == null)
            return;

        if (((ICritterHost)this).IsMagicBlockedAt(mORecord.TileX, mORecord.TileY)
            || mOLevelLoader.CurrentLevelIndex == UWEndgame.VoidLevelIndex)
            return;

        UWTilePos lOPlayerTile = fEnsurePlayer() && mOPlayerTransform != null
            ? mOLevelLoader.WorldPositionToTile(mOPlayerTransform.position)
            : new UWTilePos(mORecord.TileX, mORecord.TileY);

        UWSpellHost lOHost = new UWSpellHost(UWScene.Character, UWScene.Interaction, mOLevelLoader,
            mOLevelLoader.UWDataImporter, UWScene.GameUi);

        bool lbSummoned = UWSummonSpellRules.SummonByCreature(lOHost, mOBody.x, mOBody.z,
            (mORecord.FullFacing & 0xFF) * 360f / 256f, mOLevelLoader.CurrentLevelIndex + 1, lOPlayerTile);

        fEvent("summons: {0}", lbSummoned ? "a creature" : "no room or nothing fitting");
    }

    /// <summary>Sends a missile at the target - the same route the arrow trap takes.</summary>
    private void fLaunchProjectile(int piObjectId, CritterTarget pOTarget, int piDamageType, float pfRange, int piPitch)
    {
        if (mOLevelLoader == null || mOLevelLoader.UWDataImporter == null
            || mOLevelLoader.UWDataImporter.ObjectProperties == null)
            return;

        Vector3 lOStart = fGetEyePosition();

        float lfSpeed = mOLevelLoader.UWDataImporter.ObjectProperties.GetRangedSpeed(piObjectId)
            * UWSpellProjectile.SpeedFactor;

        bool lbWeightless = fIsWeightless(piObjectId);

        Vector3 lOToAim;

        if (lbWeightless)
        {
            // A SPELL MISSILE STARTS AND FLIES AS IN UW.EXE (2026-09-27; per user: ours flew over
            // small creatures, the original's at most past them): it left the body's centre
            // aimed at the player's eyes, so it climbed all the way and passed over anything low.
            if (!fGetSpellLaunch(pOTarget, piObjectId, piPitch, lfSpeed, out lOStart, out lOToAim))
            {
                fEvent("missile {0}: no room at the start, none made", piObjectId);

                return;
            }
        }
        else
        {
            lOToAim = fGetAimPoint(lOStart, pOTarget, false) - lOStart;

            // A MISSILE WITH WEIGHT MUST AIM HIGHER: whatever it drops in its flight time is
            // added beforehand. (Arrows and stones keep the port's aim for now; the original's
            // start and pitch below would serve them too, with the drop term of GetPitchToGTarg.)
            if (lfSpeed > 0f)
            {
                float lfTime = lOToAim.magnitude / lfSpeed;

                lOToAim.y += 0.5f * UWSpellProjectile.Gravity * lfTime * lfTime;
            }
        }

        GameObject lOProjectile = mOLevelLoader.SpawnProjectile(piObjectId, lOStart, lOToAim);

        if (lOProjectile == null)
            return;

        // The launch sound every missile makes (PrepareProjectileObject, effect 0x0A).
        UWSoundEffects.PlayAt(UWSoundEffects.Miss, lOStart);

        miShotsFired++;

        UWSpellProjectile lOFlight = lOProjectile.AddComponent<UWSpellProjectile>();

        // Its own body must not intercept its own missile - see SetLauncher.
        lOFlight.SetLauncher(gameObject);

        // The ammunition table's damage through the dice with charge 0x80 and flank 0
        // (MissileAttackHit_seg022_14BF, deviation 46); the target's armour of the part hit
        // and the difficulty are subtracted on impact (UWSpellProjectile).
        int liBase = mOLevelLoader.UWDataImporter.ObjectProperties.GetRangedDamage(piObjectId);
        int liDamage = UWCritterCombat.RollMissileDamage(liBase);

        // A CREATURE'S FIREBALL EXPLODES LIKE THE PLAYER'S: until 2026-09-27 its missiles
        // carried no impact at all, so there was neither the explosion picture nor the blast
        // on the tile (UWSpellProjectile.fApplyBlast).
        lOFlight.Begin(lOToAim.normalized, lfSpeed, liDamage, pfRange, null, mOLevelLoader,
            UWRunicMagic.GetProjectileImpactId(piObjectId), piDamageType);
        lOFlight.UseObjectRadius(piObjectId);

        if (!lbWeightless)
            lOFlight.MakeBallistic(UWSpellProjectile.Gravity);
    }

    /// <summary>
    /// Start and direction of a creature's spell missile, PrepareProjectileObject_seg025_791 as
    /// for the player (UWPlayerThrow.TryGetMissileStart): in front of the body by both radii
    /// plus four eighths, at the creature's zpos plus five sixths of its height plus two per
    /// pitch step; the pitch is the brain's GetPitchToGTarg - the difference of the FEET over
    /// the distance, so over level ground the missile flies level, at five sixths of the
    /// caster's height (a mage: 18 of 22 zpos), whatever the target's size. The horizontal
    /// direction points at the target as before.
    ///
    /// NO ROOM, NO MISSILE (seg025_B51, read 2026-09-27): the placement fails when the start
    /// lies in a wall or a step, or when any object's collision record overlaps it within the
    /// missile's height - a creature in melee contact above all, whose body the start lands in
    /// (7 eighths ahead for a mage). The original then makes no missile at all; ours launched
    /// it inside or beyond that body, so it flew through a man-sized opponent now and then
    /// (per user, 2026-09-27). A low creature does not block: the start is above it.
    /// </summary>
    private bool fGetSpellLaunch(CritterTarget pOTarget, int piObjectId, int piPitch, float pfSpeed,
        out Vector3 pOStart, out Vector3 pODirection)
    {
        Vector3 lOAt = fGetTargetWorldPosition(pOTarget);
        Vector3 lOFlat = new Vector3(lOAt.x - mOBody.x, 0f, lOAt.z - mOBody.z);

        if (lOFlat.sqrMagnitude < 0.0001f)
            lOFlat = fHeadingToDirection(mORecord.FullFacing);

        lOFlat.Normalize();

        fGetOwnSize(out int liRadius, out int liHeight);

        int liItemRadius = 0;
        UWCommonObjectProperties lOCommon = fCommonProperties();

        if (lOCommon != null && lOCommon.TryGet(piObjectId, out UWCommonObjectProperties.Entry lOItem))
            liItemRadius = lOItem.Radius;

        int liZPos = UWUnits.RoundToInt(mOBody.y / UWWorldScale.ZPosStep);

        if (liHeight != 0)
            liZPos += (((liHeight * 5) & 0xFF) / 6) + (piPitch << 1);

        pOStart = new Vector3(mOBody.x, liZPos * UWWorldScale.ZPosStep, mOBody.z)
            + (lOFlat * ((liRadius + liItemRadius + 4) * UWWorldScale.SubTileStep));

        int liHeading = UWUnits.RoundToInt(Mathf.Atan2(lOFlat.x, lOFlat.z) * Mathf.Rad2Deg * 256f / 360f) & 0xFF;

        pODirection = UWPlayerThrow.GetMissileDirection(liHeading, piPitch, pfSpeed);

        return fIsSpellStartFree(pOStart, Mathf.Max(1, liItemRadius) * UWWorldScale.SubTileStep);
    }

    /// <summary>The placement test of seg025_B51 for a creature's missile: open tile, above
    /// the floor, no rock between the body and the start, and no body or solid object
    /// overlapping the start.</summary>
    private bool fIsSpellStartFree(Vector3 pOStart, float pfRadius)
    {
        if (!mOLevelLoader.CanDropAt(pOStart, false) || pOStart.y <= mOLevelLoader.GetFloorHeightAt(pOStart))
            return false;

        Vector3 lOFrom = new Vector3(mOBody.x, pOStart.y, mOBody.z);

        for (int liSample = 1; liSample <= 4; liSample++)
        {
            if (mOLevelLoader.IsInsideRock(Vector3.Lerp(lOFrom, pOStart, liSample / 4f), mOBody, out Vector3 _))
                return false;
        }

        foreach (Collider lOCollider in Physics.OverlapSphere(pOStart, pfRadius, Physics.AllLayers,
                     QueryTriggerInteraction.Ignore))
        {
            if (lOCollider.transform.IsChildOf(transform)
                || lOCollider.GetComponentInParent<UWSpellProjectile>() != null)
                continue;

            if (fIsDefenderCollider(lOCollider, out UWCritter _, out bool _))
                return false;

            UWEntityInfo lOEntity = lOCollider.GetComponentInParent<UWEntityInfo>();
            UWCommonObjectProperties lOCommon = fCommonProperties();

            if (lOEntity != null && lOEntity.ObjectData != null && lOCommon != null
                && lOCommon.TryGet(lOEntity.ObjectData.ID, out UWCommonObjectProperties.Entry lOEntry)
                && lOEntry.Height > 0)
                return false;
        }

        return true;
    }

    /// <summary>How often this creature has shot or cast - only for the F1 overlay.</summary>
    private int miShotsFired;

    private bool fIsWeightless(int piObjectId)
    {
        UWCommonObjectProperties lOCommon = fCommonProperties();

        return lOCommon != null && lOCommon.IsWeightless(piObjectId);
    }

    /// <summary>
    /// Where a missile WITH WEIGHT aims (a spell missile: fGetSpellLaunch). Two observations by the user in the original (2026-09-10): an
    /// enemy FIREBALL arrives at eye height, the acid slug's missile at chest height. A spell
    /// missile flies weightless and straight at the eyes; a thrown one drops and leaves at
    /// the creature's own height, clamped into the target's body (centre to head).
    /// </summary>
    private Vector3 fGetAimPoint(Vector3 pOStart, CritterTarget pOTarget, bool pbWeightless)
    {
        if (!pOTarget.IsPlayer)
        {
            Vector3 lOAt = fGetTargetWorldPosition(pOTarget);

            return pbWeightless ? lOAt : new Vector3(lOAt.x, Mathf.Max(lOAt.y, Mathf.Min(pOStart.y, lOAt.y + 16f)), lOAt.z);
        }

        if (!fEnsurePlayer())
            return pOStart;

        // REALLY EYE HEIGHT: UWCharacter sits on the camera, so its position is where the
        // player looks from.
        Vector3 lOEyes = mOPlayer.transform.position;

        if (pbWeightless)
            return lOEyes;

        if (mOPlayerBody == null)
            mOPlayerBody = mOPlayer.GetComponentInParent<CharacterController>();

        if (mOPlayerBody == null)
            return lOEyes;

        Bounds lOBounds = mOPlayerBody.bounds;

        float lfHead = lOBounds.max.y - (lOBounds.size.y * 0.1f);
        float lfY = Mathf.Clamp(pOStart.y, lOBounds.center.y, lfHead);

        return new Vector3(lOBounds.center.x, lfY, lOBounds.center.z);
    }

    /// <summary>A target's body centre in the world: the player's collider, another
    /// creature's collider, an object's position.</summary>
    private Vector3 fGetTargetWorldPosition(CritterTarget pOTarget)
    {
        if (pOTarget.IsPlayer && fEnsurePlayer())
        {
            if (mOPlayerBody == null)
                mOPlayerBody = mOPlayer.GetComponentInParent<CharacterController>();

            return mOPlayerBody != null ? mOPlayerBody.bounds.center : mOPlayerTransform.position;
        }

        UWCritter lOOther = fCritterOf(fObjectAt(pOTarget.Index));

        if (lOOther != null)
        {
            Collider lOCollider = lOOther.GetComponent<Collider>();

            return lOCollider != null ? lOCollider.bounds.center : lOOther.mOBody + (Vector3.up * FallbackEyeHeight);
        }

        return new Vector3(UWUnits.SubTileToWorldAxis(pOTarget.TileX, pOTarget.X & 7),
            UWUnits.ZPosToWorld(pOTarget.ZPos) + FallbackEyeHeight,
            UWUnits.SubTileToWorldAxis(pOTarget.TileY, pOTarget.Y & 7));
    }

    /// <summary>Goal 10: the player looked back within reach - TalkTo. The goal is cleared to
    /// 0 before the screen opens, as the port did before, so the conversation does not start
    /// again the moment it ends; a script that sets a goal overrides it (RefreshFromData).</summary>
    void ICritterHost.OnTalk()
    {
        if (UWConversationScreen.IsAnyOpen || mONpc == null)
            return;

        Interaction lOInteraction = UWScene.Interaction;

        if (lOInteraction == null)
            return;

        fEvent("talks");
        mORecord.Goal = GoalStandStill;
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
    /// UWDamageable drained to zero if a brain-side death (drowning, a record that ran out
    /// first) did not come through it - so UWCritterRemains and the rest of the port see the
    /// creature as destroyed. The dragon nod comes with the kill experience (UWCharacter.OnKillRewarded).
    /// </summary>
    void ICritterHost.OnDeathStarted(bool pbByPlayer)
    {
        fEvent("dying{0}", pbByPlayer ? " by the player" : "");

        if (pbByPlayer && mbHasStats && fEnsurePlayer() && mOLevelLoader != null)
            mOPlayer.AwardKillExperience(mOStats, mORecord.IsStrong, mOLevelLoader.CurrentLevelIndex + 1);

        fEnsureDamageableDestroyed();
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

        bool lbVeto = !UWSpecialDeaths.Apply(liWhoami, UWSpecialDeaths.Stage.Dying, new SpecialDeathHost(this));

        if (lbVeto)
            fKeepBodyAlive();

        return lbVeto;
    }

    /// <summary>
    /// A refused death must not kill the body: the UWDamageable counts its own hit points and
    /// had already reached 0 when the brain asked, and at 0 it fires Destroyed and
    /// UWCritterRemains lets the creature crumble (per user, 2026-09-28: the golem fell to dust
    /// after his conversation). The body gets the record's hit points back, at least 1 - the
    /// golem's record stays at 0, as in the original, until his script sets 125.
    /// </summary>
    private void fKeepBodyAlive()
    {
        if (mODamageable == null || mORecord == null)
            return;

        mODamageable.Health.SetCurrentHealth(Mathf.Max(1, mORecord.HitPoints));
        miLastHealth = mODamageable.CurrentHealth;
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
            UWSpecialDeaths.Apply(liWhoami, UWSpecialDeaths.Stage.Dead, new SpecialDeathHost(this));
    }

    /// <summary>What UWSpecialDeaths needs from the scene.</summary>
    private sealed class SpecialDeathHost : UWSpecialDeaths.IHost
    {
        private readonly UWCritter mOCritter;

        public SpecialDeathHost(UWCritter pOCritter)
        {
            mOCritter = pOCritter;
        }

        public void TalkToThisCreature()
        {
            ((ICritterHost)mOCritter).OnTalk();
        }

        /// <summary>Out of the fight and standing: no attacker (byte 0x12), EndCombat,
        /// animation 0x20, frame 0 - the hit points stay as they are.</summary>
        public void StandDown()
        {
            mOCritter.mORecord.Attacker = 0;
            mOCritter.mORecord.Animation = UWCritterBrain.AnimStanding;
            mOCritter.mORecord.Frame = 0;

            Interaction lOInteraction = UWScene.Interaction;

            if (lOInteraction != null)
                lOInteraction.IsCombatModeActive = false;
        }

        public void SetHitPoints(int piHitPoints)
        {
            mOCritter.mORecord.HitPoints = piHitPoints;
        }

        /// <summary>The golem's marker: the original misuses the tile state bits of byte 0x0A
        /// for it, and so do we - they are recomputed by the physics anyway.</summary>
        public bool TryMarkRewarded()
        {
            if (mOCritter.mORecord.TileState != 0)
                return false;

            mOCritter.mORecord.TileState = 1;

            return true;
        }

        /// <summary>EXPChange, as the golem's reward - the dungeon level is what the player
        /// stands on, exactly as with a kill.</summary>
        public void AwardExperience(int piExperience)
        {
            UWCharacter lOCharacter = UWScene.Character;

            if (lOCharacter != null && mOCritter.mOLevelLoader != null)
                lOCharacter.ChangeExperience(piExperience, mOCritter.mOLevelLoader.CurrentLevelIndex + 1);
        }

        public void PlayCutscene(int piCutscene)
        {
            UWIntroPlayer lOPlayer = UWScene.IntroPlayer;

            if (lOPlayer != null)
                lOPlayer.PlayCutscene(piCutscene);
        }

        public void RemoveCreaturesByWhoami(int[] piWhoamis)
        {
            if (piWhoamis == null || mOCritter.mOLevelLoader == null)
                return;

            // ONE CREATURE PER ENTRY (RunFunctionOnWhoAmIList_seg038_3307_D4E with 0 for "go
            // on": it stops at the first match in the active mobile list). Per user,
            // 2026-09-29: the original removed nine of the fourteen - one of the three 209
            // guards, one of the two 219 and one of the three 220 - while ours removed all.
            // The first is the first in the level's list of allocated mobiles (UWLevel.
            // GetActiveMobileOrder, the file's list at 0x7AFC): the original's #222 before #223
            // and #200 is exactly that order.
            UWLevel lOLevel = mOCritter.mOLevelLoader.CurrentLevel;
            UWCritter[] lOCritters = Object.FindObjectsByType<UWCritter>(FindObjectsSortMode.None);

            foreach (int liWhoami in piWhoamis)
            {
                UWCritter lOFirst = null;
                int liFirstOrder = int.MaxValue;

                foreach (UWCritter lOCritter in lOCritters)
                {
                    if (lOCritter == null || lOCritter.mONpc == null || lOCritter.mONpc.NPCwhoami != liWhoami
                        || (lOCritter.mODamageable != null && lOCritter.mODamageable.IsDestroyed))
                        continue;

                    int liOrder = lOLevel != null ? lOLevel.GetActiveMobileOrder(lOCritter.miIndex) : int.MaxValue;

                    if (lOFirst == null || liOrder < liFirstOrder)
                    {
                        lOFirst = lOCritter;
                        liFirstOrder = liOrder;
                    }
                }

                if (lOFirst != null)
                    mOCritter.mOLevelLoader.RemoveObjectFromWorld(lOFirst.mONpc);
            }
        }

        public void RemoveObjectsInTile(int piTileX, int piTileY, int piObjectId)
        {
            if (mOCritter.mOLevelLoader == null || mOCritter.mOLevelLoader.CurrentLevel == null
                || mOCritter.mOLevelLoader.CurrentLevel.TileData == null)
                return;

            UWTile lOTile = mOCritter.mOLevelLoader.CurrentLevel.TileData[
                (piTileY * UnderworldRevisited.Build.UWLevelMeshBuilder.TilesPerAxis) + piTileX];

            if (lOTile == null || lOTile.ObjectsInTile == null)
                return;

            foreach (UWObject lOObject in new System.Collections.Generic.List<UWObject>(lOTile.ObjectsInTile))
            {
                if (lOObject != null && lOObject.ID == piObjectId)
                    mOCritter.mOLevelLoader.RemoveObjectFromWorld(lOObject);
            }
        }

        public void SetTalismansDestroyable(bool pbValue)
        {
            UWEndgame.TalismansDestroyable = pbValue;
        }
    }

    /// <summary>Frame 3 of animation 0x0C: the body goes. UWCritterRemains leaves the remains
    /// and the loot and frees the object; the animator is told the death pictures are over.</summary>
    void ICritterHost.OnDeathFinished()
    {
        fEvent("death finished");
        fEnsureDamageableDestroyed();

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

    private void fEnsureDamageableDestroyed()
    {
        if (mODamageable == null || mODamageable.IsDestroyed)
            return;

        mbSuppressDamageEvent = true;
        mODamageable.ApplyDamage(mODamageable.CurrentHealth, UWDamageTypes.None);
        mbSuppressDamageEvent = false;
        miLastHealth = 0;
    }

    void ICritterHost.OnGoalChanged(int piOldGoal, int piNewGoal, int piGTarg)
    {
        fTrace("goal {0} -> {1} (gtarg {2})", piOldGoal, piNewGoal, piGTarg);
    }

    // ------------------------------------------------- Damage intake

    /// <summary>
    /// Every blow, missile, spell or burn the UWDamageable took, as DamageNPC: the amount is
    /// what the health lost, the attacker whoever announced himself in miPendingAttacker -
    /// the player unless a creature's blow or the lava said otherwise. The brain updates its
    /// bytes 0x11, 0x12 and 8, writes the kin alarm, starts the death and switches the
    /// music; the mind reacts at its next update. If the UWDamageable died of a blow the
    /// record would have survived (they can drift through healing scripts), the record dies
    /// with it.
    /// </summary>
    private void fOnDamaged(UWDamageable pODamageable)
    {
        if (mbSuppressDamageEvent || mOBrain == null)
            return;

        int liNow = pODamageable != null ? pODamageable.CurrentHealth : 0;
        int liAmount = Mathf.Max(0, miLastHealth - liNow);

        miLastHealth = liNow;

        if (pODamageable != null && pODamageable.IsDestroyed && mORecord.HitPoints > liAmount)
            liAmount = mORecord.HitPoints;

        int liAttacker = miPendingAttacker;

        miPendingAttacker = PlayerIndex;

        fTrace("damaged {0} by {1} (hp {2})", liAmount, liAttacker == PlayerIndex ? "the player" : liAttacker.ToString(), liNow);
        mOBrain.OnDamaged(this, liAttacker, liAmount);
    }

    /// <summary>A blow of the player missed: DamageObject with zero - the creature still
    /// records the attacker and reacts (per user on the original, 2026-09-12).</summary>
    public void OnMissedByPlayer()
    {
        if (mOBrain == null || mODamageable == null || mODamageable.IsDestroyed)
            return;

        fTrace("missed by the player");
        mOBrain.OnDamaged(this, PlayerIndex, 0);
    }

    // ------------------------------------------------- Spells on the creature

    /// <summary>Confusion: goal 2, attitude 1, the player as target - the three fields the
    /// original's spell writes (deviation 39 adopted), after the resistance check
    /// (fPassesSpellResistance). No duration.</summary>
    public bool Confuse()
    {
        if (mORecord == null || mODamageable == null || mODamageable.IsDestroyed)
            return false;

        if (!fPassesSpellResistance("confusion"))
            return true;

        fTrace("confused");
        mORecord.Goal = WanderGoal;
        mORecord.GTarg = PlayerIndex;
        mORecord.Attitude = 1;

        return true;
    }

    /// <summary>
    /// THE RESISTANCE CHECK before Cause Fear, Confusion, Paralyse and Ally, built 2026-09-23
    /// (per user, it had been left out since the spells were rebuilt, deviation 39): the
    /// original's Ally_seg038_3307_8F8 and the shared routine of the other three ask
    /// ScaleDamageAgainstObject whether one point of MAGIC damage (type 3) gets through
    /// (UWDamageTypes.Scale). Magic resistance 0 - the low two bits of COMOBJ byte 8 - lets
    /// every spell through, 1 turns one in three away, 2 two in three, 3 all. A spell turned
    /// away is spent and does nothing, as in the original, and the caller shows no message.
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

    /// <summary>Critter table byte 0x1C, upper nibble; 0 without a table row.</summary>
    public int TravelRange => mbHasStats ? mOStats.TravelRange : 0;

    /// <summary>Whether the player's critical failure against this creature wears the weapon
    /// (UWEquipmentWear).</summary>
    public bool WearsWeaponOnCriticalFailure => mbHasStats && UWEquipmentWear.WearsWeaponOnCriticalFailure(mOStats);

    /// <summary>The whole path from the creature's tile to pOTo with its own idea of what it
    /// can enter (UWTilePath.TryGetPath).</summary>
    public bool TryGetPathTo(UWTilePos pOTo, System.Collections.Generic.List<UWTilePos> pOPath)
    {
        if (mORecord == null)
            return false;

        return UWTilePath.TryGetPath(this, new UWTilePos(mORecord.TileX, mORecord.TileY), pOTo, pOPath);
    }

    /// <summary>
    /// Sets the creature down on a sub-tile spot (0..7 each) of a tile - the sleep ambush
    /// (UWSleepRules.TryAmbush). Refused where it would not fit: a solid tile, the wrong ground
    /// for its kind, a closed door, the player, another creature, a force field - the blockers
    /// of its own step (fCanStandAt) without the height limit, since it does not step there.
    /// </summary>
    public bool PlaceAtSpot(UWTilePos pOTile, int piSubX, int piSubY)
    {
        UWTile lOTile = fGetTileAt(pOTile.X, pOTile.Y);

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
            return false;

        if (!mbFlying && mbSwimming != fIsWater(lOTile))
            return false;

        Vector3 lOSpot = new Vector3(UWUnits.SubTileToWorldAxis(pOTile.X, piSubX), 0f,
            UWUnits.SubTileToWorldAxis(pOTile.Y, piSubY));

        // On the floor, fliers too: the ambush hands the tile's floor height times eight as zpos
        // (ovr104_71C) - unlike the level exit, which lifts a flier halfway up.
        lOSpot.y = lOTile.FloorHeight + (mbFlying ? 0f : fGetSwimmerOffset());

        if (fBlockingDoor(lOSpot, lOTile) != null || fIsBlockedByPlayer(lOSpot)
            || fBlockingCreature(lOSpot) != null || fIsBlockedByForceField(lOSpot))
            return false;

        mOBody = lOSpot;
        transform.position = lOSpot;
        mfPictureSeconds = 0f;
        fWritePositionToRecord(true);
        fTrace("set down by the sleep ambush on {0}/{1} at {2}/{3}", pOTile.X, pOTile.Y, piSubX, piSubY);

        return true;
    }

    /// <summary>
    /// The orb is broken: Tybal's hit points are halved plus one and he no longer heals when
    /// the player leaves the level (UWTybalOrbRules.WeakenedHitPoints, word 0x0D bit 9).
    /// </summary>
    public void WeakenByBrokenOrb()
    {
        if (mORecord == null || mODamageable == null || mODamageable.IsDestroyed)
            return;

        int liHitPoints = UWTybalOrbRules.WeakenedHitPoints(mODamageable.CurrentHealth);

        mODamageable.Health.SetCurrentHealth(liHitPoints);
        mORecord.HitPoints = mODamageable.CurrentHealth;
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
    public bool Befriend()
    {
        if (mORecord == null || mODamageable == null || mODamageable.IsDestroyed)
            return false;

        if (!fPassesSpellResistance("ally"))
            return true;

        fTrace("befriended");

        if (!mORecord.IsAlly)
        {
            mORecord.Goal = WanderGoal;
            mORecord.GTarg = 0;
        }

        mORecord.IsAlly = true;
        mORecord.Attitude = 3;

        return true;
    }

    /// <summary>Paralyze: goal 7 (stand still) and attitude 1, after the resistance check
    /// (fPassesSpellResistance), without a duration as in the uw1 branch of the reference.
    /// A hit wakes it: the damage reaction sets goal 5.</summary>
    public bool Paralyse()
    {
        if (mORecord == null || mODamageable == null || mODamageable.IsDestroyed)
            return false;

        if (!fPassesSpellResistance("paralysis"))
            return true;

        fTrace("paralysed");
        mORecord.Goal = GoalParalysed;
        mORecord.Attitude = 1;

        return true;
    }

    /// <summary>Cause Fear: goal 6 against the player, without a morale roll.</summary>
    public bool Flee()
    {
        if (mORecord == null || mODamageable == null || mODamageable.IsDestroyed || !fEnsurePlayer())
            return false;

        if (!fPassesSpellResistance("fear"))
            return true;

        fTrace("flees (spell)");
        mORecord.SetNewGoal(GoalWithdraw, PlayerIndex);

        return true;
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

    /// <summary>When building a level: a fresh clock and alarm (UWScene), so nothing of the
    /// old level survives loading a savegame (per user, 2026-09-12: a rat attacked at once
    /// after loading because the static alarm was still fresh).</summary>
    public static void ResetAlarm()
    {
        UWScene.ResetCritterServices();
    }

    // ------------------------------------------------- Push

    /// <summary>
    /// Pushes the creature a bit aside - in the original an opponent can be pushed away by
    /// walking into it (per user, 2026-09-05). Checked like one of its own steps, including
    /// the fallback to the single axes - so you push it along a wall instead of into it. The
    /// picture follows at once; the record is updated so the brain sees the new place.
    /// </summary>
    public void Push(Vector3 pODirection, float pfDistance)
    {
        if (pfDistance <= 0f || mbRemoved)
            return;

        Vector3 lOFlat = new Vector3(pODirection.x, 0f, pODirection.z);

        if (lOFlat.sqrMagnitude <= 0.0001f)
            return;

        lOFlat = lOFlat.normalized * pfDistance;

        Vector3 lOStart = mOBody;

        miStartClearanceFailures = fClearanceFailures(lOStart, mbFlying ? fGetFloorHeight(lOStart) : lOStart.y);
        miStartShoreFailures = fGetShoreFailures(lOStart);
        mbClearanceRelaxed = miStartClearanceFailures > 0;

        BlockInfo lOBlock;

        bool lbMoved = fTryStepTo(lOStart, lOFlat, out lOBlock)
            || fTryStepTo(lOStart, new Vector3(lOFlat.x, 0f, 0f), out lOBlock)
            || fTryStepTo(lOStart, new Vector3(0f, 0f, lOFlat.z), out lOBlock);

        if (!lbMoved)
            return;

        transform.position = mOBody;
        mfPictureSeconds = 0f;
        fWritePositionToRecord(true);
    }

    // ------------------------------------------------- The probes (the physics substitute)

    /// <summary>
    /// Whether a walkable spot lies at this world position, and what refuses it. Solid
    /// tiles are excluded, closed doors (reported, not silently blocking), the player's body,
    /// force fields, the wrong ground for the creature's kind, and too large a height
    /// difference - otherwise creatures would walk up ledges the player cannot climb himself.
    /// </summary>
    private bool fCanStandAt(Vector3 pOWorldPosition, out float pfFloorHeight, out BlockInfo pOBlock)
    {
        pfFloorHeight = pOWorldPosition.y;
        pOBlock = default(BlockInfo);

        UWTile lOTileData;

        if (!fTryGetTile(pOWorldPosition, out lOTileData))
            return fBlocked(BlockKindEnum.Wall, "outside the map", pOWorldPosition, ref pOBlock);

        if (lOTileData.TileType == UWTile.TileTypeEnum.solid)
            return fBlocked(BlockKindEnum.Wall, "solid tile", pOWorldPosition, ref pOBlock);

        UWObject lODoor = fBlockingDoor(pOWorldPosition, lOTileData);

        if (lODoor != null)
        {
            pOBlock.Door = lODoor;

            return fBlocked(BlockKindEnum.Door, "closed door", pOWorldPosition, ref pOBlock);
        }

        if (fIsBlockedByPlayer(pOWorldPosition))
            return fBlocked(BlockKindEnum.Player, "player in the way", pOWorldPosition, ref pOBlock);

        UWCritter lOOther = fBlockingCreature(pOWorldPosition);

        if (lOOther != null)
        {
            pOBlock.Creature = lOOther;

            return fBlocked(BlockKindEnum.Creature, "creature in the way", pOWorldPosition, ref pOBlock);
        }

        if (fIsBlockedByForceField(pOWorldPosition))
            return fBlocked(BlockKindEnum.ForceField, "force field", pOWorldPosition, ref pOBlock);

        // A SWIMMER collides with plain floor: its motion handler carries the floor bit 0x8
        // in the callback mask (table 2880 = {0x0010, 0x1728, 0x10A8, 0x0000}, callback
        // seg006_1477_65E; Docs/AI/creature-ai.md section 7.6). A LAND CREATURE is NOT refused
        // the water here any more (deviation 43, built 2026-09-20): the original tests water
        // only in the TILE tests - the straight line and the path search, where water is
        // impassable terrain in handler word 2 = 0x1010 - and never in the step itself. The
        // step that ends in water drowns the creature instead (seg006_1477_476, asm lines
        // 41417-41460; see fIsDrowningAt). That matches what the user measured in the
        // original on 2026-09-20: a creature cannot be pushed into the water, it only gets
        // in while closing in on the player in melee, and then it is gone at once with a
        // splash. Lava is not refused either: the user proved that a troll stands in the
        // middle of lava (2026-09-01); it burns there instead (fApplyMotion).
        if (mbSwimming && !mbFlying && !fIsWater(lOTileData))
            return fBlocked(BlockKindEnum.Terrain, "no water", pOWorldPosition, ref pOBlock);

        // WATER IS REFUSED A LAND CREATURE unless it is following its target in. The height
        // rule below cannot do this job: one floor level is 16 units in the tile data and
        // UWSettings.CritterMaxStepHeight is 16 as well, so a step down to the water passes it
        // exactly. That is why the creature still drowned itself while backing off although
        // the drop was meant to be the gate (per user, 2026-09-20, three reports). The
        // original blocks the step on the dropping bit for a creature that holds no path
        // (Docs/AI/creature-ai.md section 7.3); ours uses the direction and the target's own tile,
        // see fLeadsTowardsTarget.
        if (!mbFlying && !mbSwimming && fIsWater(lOTileData) && !fLeadsTowardsTarget(pOWorldPosition))
            return fBlocked(BlockKindEnum.Terrain, "water, and not on the way to the target",
                pOWorldPosition, ref pOBlock);

        float lfTileFloor = lOTileData.FloorHeight;

        pfFloorHeight = lfTileFloor;

        UWSettings lOSettings = UWSettings.Instance;
        float lfMaxStep = lOSettings != null ? lOSettings.CritterMaxStepHeight : 16f;

        // What the creature currently stands on is its own height - not the floor height of
        // the tile. On a bridge those are two different things. A SWIMMER lies BELOW the water
        // level by its offset, so that offset has to come off again.
        float lfCurrentFloor = mbFlying
            ? fGetFloorHeight(mOBody)
            : mOBody.y - fGetSwimmerOffset();

        // THE PROBE IS FOR BRIDGES - something walkable above the tile floor. A SWIMMER has no
        // business up there (per user, 2026-09-16: the lurker climbed from 16 to 54 in three
        // steps).
        if (!mbFlying && !mbSwimming)
            pfFloorHeight = fProbeStandingHeight(pOWorldPosition, pfFloorHeight, lfCurrentFloor + lfMaxStep);

        bool lbCanStand = mbFlying || Mathf.Abs(pfFloorHeight - lfCurrentFloor) <= lfMaxStep;

        // A LAND CREATURE MAY DROP INTO WATER ON ITS WAY TO THE TARGET, and only there. In
        // the original's motion callback the WATER bit is tested before the dropping bit
        // (seg006_1477_46D at 41417, the dropping branch only at 50D), so a step that ends on
        // a water tile drowns the creature however far below the surface lies - but the
        // dropping bit blocks a creature that holds NO PATH (Docs/AI/creature-ai.md section 7.3),
        // and the melee footwork holds none. The user measured exactly that in the original
        // (2026-09-20): a creature cannot be pushed over the water's edge and does not back
        // off over it either, it only gets in while closing in on a player who stands IN the
        // water. We express the same by the direction of the step and by the target's own
        // tile, which is what the player sees; the original's path condition is not modelled.
        // Without it ours drowned itself while backing off. Only downwards: climbing OUT
        // keeps the rule.
        bool lbDropIntoWater = !mbFlying && !mbSwimming && pfFloorHeight <= lfCurrentFloor
            && fIsWater(lOTileData) && fLeadsTowardsTarget(pOWorldPosition);

        if (!lbCanStand && !lbDropIntoWater)
            return fBlocked(BlockKindEnum.Terrain, "step too high", pOWorldPosition, ref pOBlock);

        // The clearance is only checked once the step itself is possible - and only as long
        // as the creature currently keeps it itself. If it already stood too close (say
        // because it was placed that way), every step would fail and it would be nailed down
        // forever.
        int liFailures = fClearanceFailures(pOWorldPosition, pfFloorHeight);

        // The chasm part of that clearance is what a drop into water looks like, so the step
        // that drowns the creature is not weighed against it either.
        if (liFailures > 0 && !lbDropIntoWater
            && (!mbClearanceRelaxed || liFailures > miStartClearanceFailures))
            return fBlocked(BlockKindEnum.Wall, "wall or chasm clearance", pOWorldPosition, ref pOBlock);

        // THE SHORE IS A DIRECTION RULE, not a wall - see fGetShoreFailures.
        if (mbSwimming && fGetShoreFailures(pOWorldPosition) > miStartShoreFailures)
            return fBlocked(BlockKindEnum.Terrain, "would move closer to the shore", pOWorldPosition, ref pOBlock);

        return true;
    }

    /// <summary>
    /// Bridges, stairs and the like are separate objects lying ABOVE the tile floor - the
    /// tile data knows nothing about them. A ray from top to bottom finds what one actually
    /// stands on there (per user, 2026-08-30: opponents walk over bridges in the original).
    /// The sprite layer is left out, otherwise creatures would stand on other creatures.
    /// </summary>
    private float fProbeStandingHeight(Vector3 pOWorldPosition, float pfTileFloor, float pfProbeTop)
    {
        if (pfProbeTop <= pfTileFloor)
            return pfTileFloor;

        Vector3 lOOrigin = new Vector3(pOWorldPosition.x, pfProbeTop, pOWorldPosition.z);
        float lfLength = pfProbeTop - pfTileFloor;

        RaycastHit lOHit;

        if (!Physics.Raycast(lOOrigin, Vector3.down, out lOHit, lfLength, ~(1 << SpriteLayer)))
            return pfTileFloor;

        if (lOHit.point.y > pfTileFloor + 1f)
            fTrace("standing height raised to {0:F0} by {1}", lOHit.point.y,
                lOHit.collider == null ? "?" : lOHit.collider.name);

        return Mathf.Max(pfTileFloor, lOHit.point.y);
    }

    /// <summary>
    /// How many clearance probes fail at this spot: the diagonal, the four wall probes at
    /// wall clearance and the four chasm probes at chasm clearance. Zero means standing
    /// free. A creature's image turns towards the camera; standing close to a wall it sticks
    /// halfway into it (per user, 2026-08-30), at a chasm it hangs over it (2026-08-31). The
    /// number lets a creature that already stands too close take only steps that do not make
    /// it worse (per user, 2026-09-12).
    /// </summary>
    private int fClearanceFailures(Vector3 pOWorldPosition, float pfStandingHeight)
    {
        int liFailures = fKeepsDiagonalClearance(pOWorldPosition) ? 0 : 1;

        UWSettings lOSettings = UWSettings.Instance;
        float lfClearance = lOSettings != null ? lOSettings.CritterWallClearance : 16f;
        float lfDrop = lOSettings != null ? lOSettings.CritterDropClearance : 24f;

        if (lfClearance > 0f)
        {
            liFailures += fIsProbeClear(pOWorldPosition + new Vector3(lfClearance, 0f, 0f), pfStandingHeight) ? 0 : 1;
            liFailures += fIsProbeClear(pOWorldPosition - new Vector3(lfClearance, 0f, 0f), pfStandingHeight) ? 0 : 1;
            liFailures += fIsProbeClear(pOWorldPosition + new Vector3(0f, 0f, lfClearance), pfStandingHeight) ? 0 : 1;
            liFailures += fIsProbeClear(pOWorldPosition - new Vector3(0f, 0f, lfClearance), pfStandingHeight) ? 0 : 1;
        }

        if (lfDrop > lfClearance && !mbFlying)
        {
            liFailures += fIsProbeClearOfDrop(pOWorldPosition + new Vector3(lfDrop, 0f, 0f), pfStandingHeight) ? 0 : 1;
            liFailures += fIsProbeClearOfDrop(pOWorldPosition - new Vector3(lfDrop, 0f, 0f), pfStandingHeight) ? 0 : 1;
            liFailures += fIsProbeClearOfDrop(pOWorldPosition + new Vector3(0f, 0f, lfDrop), pfStandingHeight) ? 0 : 1;
            liFailures += fIsProbeClearOfDrop(pOWorldPosition - new Vector3(0f, 0f, lfDrop), pfStandingHeight) ? 0 : 1;
        }

        return liFailures;
    }

    /// <summary>A chasm point: the floor there must not lie lower than the creature can
    /// climb. Solid tiles are no failure here, the wall probes count those.</summary>
    private bool fIsProbeClearOfDrop(Vector3 pOWorldPosition, float pfStandingHeight)
    {
        UWTile lOTileData;

        if (!fTryGetTile(pOWorldPosition, out lOTileData) || lOTileData.TileType == UWTile.TileTypeEnum.solid)
            return true;

        UWSettings lOSettings = UWSettings.Instance;
        float lfMaxStep = lOSettings != null ? lOSettings.CritterMaxStepHeight : 16f;

        float lfProbeFloor = fProbeStandingHeight(pOWorldPosition, lOTileData.FloorHeight, pfStandingHeight + lfMaxStep);

        return pfStandingHeight - lfProbeFloor <= lfMaxStep;
    }

    /// <summary>
    /// A single clearance point: it must lie inside the map, no solid tile or closed door may
    /// lie there, and the floor there must not be clearly higher or lower than the one the
    /// creature wants to stand on. Flying creatures hover above all this; for them only the
    /// solid tile and the door count.
    /// </summary>
    private bool fIsProbeClear(Vector3 pOWorldPosition, float pfStandingHeight)
    {
        UWTile lOTileData;

        if (!fTryGetTile(pOWorldPosition, out lOTileData))
            return false;

        if (lOTileData.TileType == UWTile.TileTypeEnum.solid)
            return false;

        if (fBlockingDoor(pOWorldPosition, lOTileData) != null)
            return false;

        if (mbFlying)
            return true;

        UWSettings lOSettings = UWSettings.Instance;
        float lfMaxStep = lOSettings != null ? lOSettings.CritterMaxStepHeight : 16f;

        float lfProbeFloor = fProbeStandingHeight(pOWorldPosition, lOTileData.FloorHeight, pfStandingHeight + lfMaxStep);

        return Mathf.Abs(lfProbeFloor - pfStandingHeight) <= lfMaxStep;
    }

    /// <summary>
    /// Clearance to diagonal walls. A diagonal tile counts as open although half of it is
    /// solid - so the four clearance points miss it and the creature sticks in the slant (per
    /// user, 2026-08-31). Instead of probing, this computes: for every diagonal tile in the
    /// surroundings the perpendicular distance to its cut line (CritterDiagonalClearance).
    /// </summary>
    private bool fKeepsDiagonalClearance(Vector3 pOWorldPosition)
    {
        UWSettings lOSettings = UWSettings.Instance;
        float lfClearance = lOSettings != null ? lOSettings.CritterDiagonalClearance : 22f;

        if (lfClearance <= 0f || mOLevelLoader == null || mOLevelLoader.CurrentLevel == null)
            return true;

        UWTilePos lOCentre = mOLevelLoader.WorldPositionToTile(pOWorldPosition);

        for (int liOffsetY = -1; liOffsetY <= 1; liOffsetY++)
        {
            for (int liOffsetX = -1; liOffsetX <= 1; liOffsetX++)
            {
                int liX = lOCentre.X + liOffsetX;
                int liY = lOCentre.Y + liOffsetY;

                UWTile lOTileData = fGetTileAt(liX, liY);

                if (lOTileData == null)
                    continue;

                float lfDistance;

                if (!fTryGetDiagonalDistance(lOTileData.TileType, liX, liY, pOWorldPosition, out lfDistance))
                    continue;

                if (lfDistance < lfClearance)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The direction of the diagonal wall whose clearance this spot breaks - the nearest one:
    /// diagonal_se and diagonal_nw are cut along x = z (north-east, 0x20), diagonal_ne and
    /// diagonal_sw along x = -z (south-east, 0x60). For the wall deflection (fTryDeflectedStep).
    /// </summary>
    private bool fTryGetBlockingDiagonalDirection(Vector3 pOWorldPosition, out int piDirection)
    {
        piDirection = 0;

        UWSettings lOSettings = UWSettings.Instance;
        float lfClearance = lOSettings != null ? lOSettings.CritterDiagonalClearance : 22f;

        if (lfClearance <= 0f || mOLevelLoader == null || mOLevelLoader.CurrentLevel == null)
            return false;

        UWTilePos lOCentre = mOLevelLoader.WorldPositionToTile(pOWorldPosition);
        float lfNearest = lfClearance;
        bool lbFound = false;

        for (int liOffsetY = -1; liOffsetY <= 1; liOffsetY++)
        {
            for (int liOffsetX = -1; liOffsetX <= 1; liOffsetX++)
            {
                int liX = lOCentre.X + liOffsetX;
                int liY = lOCentre.Y + liOffsetY;

                UWTile lOTileData = fGetTileAt(liX, liY);

                if (lOTileData == null
                    || !fTryGetDiagonalDistance(lOTileData.TileType, liX, liY, pOWorldPosition, out float lfDistance)
                    || lfDistance >= lfNearest)
                    continue;

                lfNearest = lfDistance;
                lbFound = true;
                piDirection = lOTileData.TileType == UWTile.TileTypeEnum.diagonal_se
                    || lOTileData.TileType == UWTile.TileTypeEnum.diagonal_nw ? 0x20 : 0x60;
            }
        }

        return lbFound;
    }

    /// <summary>
    /// Perpendicular distance to the cut line of a diagonal tile, positive on the open side.
    /// Returns false if the tile is not a diagonal at all. Which half is open is defined in
    /// UWLevelMeshBuilder: for diagonal_se the corner -x/+z is missing, so the cut runs along
    /// +x/+z to -x/-z.
    /// </summary>
    private static bool fTryGetDiagonalDistance(UWTile.TileTypeEnum peTileType, int piTileX, int piTileY, Vector3 pOWorldPosition, out float pfDistance)
    {
        pfDistance = 0f;

        float lfX = pOWorldPosition.x - (piTileX * UWLevelMeshBuilder.TileSpacing);
        float lfZ = pOWorldPosition.z - (piTileY * UWLevelMeshBuilder.TileSpacing);

        const float DiagonalScale = 0.70710678f;
        float lfLine;

        switch (peTileType)
        {
            case UWTile.TileTypeEnum.diagonal_se:
                lfLine = (lfX - lfZ) * DiagonalScale;
                break;

            case UWTile.TileTypeEnum.diagonal_nw:
                lfLine = (lfZ - lfX) * DiagonalScale;
                break;

            case UWTile.TileTypeEnum.diagonal_ne:
                lfLine = (lfX + lfZ) * DiagonalScale;
                break;

            case UWTile.TileTypeEnum.diagonal_sw:
                lfLine = -(lfX + lfZ) * DiagonalScale;
                break;

            default:
                return false;
        }

        // The cut line is infinite, the solid triangle is not: whoever stands OUTSIDE the tile
        // on the solid side of the line is still at least as far away as from the tile
        // square (per user, 2026-09-12: the vampire bat never got out of its chamber).
        float lfOutsideX = Mathf.Max(0f, Mathf.Abs(lfX) - UWLevelMeshBuilder.TileHalfSize);
        float lfOutsideZ = Mathf.Max(0f, Mathf.Abs(lfZ) - UWLevelMeshBuilder.TileHalfSize);
        float lfBox = Mathf.Sqrt((lfOutsideX * lfOutsideX) + (lfOutsideZ * lfOutsideZ));

        pfDistance = Mathf.Max(lfLine, lfBox);

        return true;
    }

    /// <summary>
    /// How many of the four probes around a spot lie on DRY LAND - the measure of how close a
    /// water creature is to the bank. NOT A HARD RULE like walls and drops but a DIRECTION
    /// rule: a step must not bring the creature closer to the bank than it already is (per
    /// user on the original, 2026-09-16: it keeps its distance, but it moves).
    /// </summary>
    private int fGetShoreFailures(Vector3 pOWorldPosition)
    {
        UWSettings lOSettings = UWSettings.Instance;
        float lfShore = lOSettings != null ? lOSettings.CritterShoreClearance : 16f;

        if (!mbSwimming || lfShore <= 0f)
            return 0;

        // A HIGH EDGE COUNTS FROM FURTHER AWAY than a flat bank (per user, 2026-09-16).
        float lfHigh = lOSettings != null ? lOSettings.SwimmerHighEdgeClearance : 24f;

        int liFailures = fIsWaterAtProbe(pOWorldPosition, new Vector3(1f, 0f, 0f), lfShore, lfHigh) ? 0 : 1;

        liFailures += fIsWaterAtProbe(pOWorldPosition, new Vector3(-1f, 0f, 0f), lfShore, lfHigh) ? 0 : 1;
        liFailures += fIsWaterAtProbe(pOWorldPosition, new Vector3(0f, 0f, 1f), lfShore, lfHigh) ? 0 : 1;
        liFailures += fIsWaterAtProbe(pOWorldPosition, new Vector3(0f, 0f, -1f), lfShore, lfHigh) ? 0 : 1;

        return liFailures;
    }

    /// <summary>Water in this direction? Checked twice: at the normal shore distance, and at
    /// the larger one for a HIGH edge - a solid tile or a floor more than one height level
    /// above the water.</summary>
    private bool fIsWaterAtProbe(Vector3 pOWorldPosition, Vector3 pODirection, float pfShore, float pfHigh)
    {
        if (!fIsProbeInWater(pOWorldPosition + (pODirection * pfShore)))
            return false;

        if (pfHigh <= pfShore)
            return true;

        UWTile lOTile = fGetTileAt(pOWorldPosition + (pODirection * pfHigh));

        if (lOTile == null)
            return true;

        return lOTile.TileType != UWTile.TileTypeEnum.solid
            && lOTile.FloorHeight <= fGetFloorHeight(pOWorldPosition) + UWTile.HeightLevel;
    }

    /// <summary>Is there water at this spot? Outside the map or without level data the answer
    /// is yes, so a missing tile never pushes a creature around.</summary>
    private bool fIsProbeInWater(Vector3 pOWorldPosition)
    {
        UWTile lOTileData = fGetTileAt(pOWorldPosition);

        return lOTileData == null || fIsWater(lOTileData);
    }

    /// <summary>
    /// The drowning test of the land creature's motion callback (seg006_1477_476, asm lines
    /// 41418-41421: "mov ax, [si]; and ax, 0F8h; cmp ax, 10h"). Of the five terrain and
    /// footing bits only the water bit 0x10 may stand: 0x08 plain floor, 0x20 lava, 0x40 the
    /// fourth terrain and 0x80 "standing on an object" must all be clear. Here the tile says
    /// whether it is water, and the standing height says whether something carries the
    /// creature - the bridge probe of fCanStandAt raises it above the tile floor, which is
    /// this port's stand-in for bit 0x80: on a bridge over the water nothing drowns.
    ///
    /// Only land creatures ask. A flier's callback (seg006_1477_5FE) and a swimmer's
    /// (seg006_1477_65E) have no water branch at all - the swimmer's handler word 0 = 0x0010
    /// even drops the water bit before the callback is reached.
    /// </summary>
    private bool fIsDrowningAt(Vector3 pOWorldPosition)
    {
        UWTile lOTileData = fGetTileAt(pOWorldPosition);

        if (lOTileData == null)
            return false;

        bool lbOnObject = pOWorldPosition.y > lOTileData.FloorHeight + StandingOnObjectTolerance;

        return UWCritterRules.Drowns(fIsWater(lOTileData), lbOnObject, mbSwimming, mbFlying);
    }

    private bool fIsWater(UWTile pOTileData)
    {
        if (pOTileData == null || mOLevelLoader == null || mOLevelLoader.UWDataImporter == null)
            return false;

        UWTerrain lOTerrain = mOLevelLoader.UWDataImporter.Terrain;

        return lOTerrain != null && lOTerrain.IsWaterFloor(pOTileData.TextureFloor);
    }

    private bool fIsLava(UWTile pOTileData)
    {
        return pOTileData != null && mOLevelLoader != null && mOLevelLoader.IsLavaTile(pOTileData);
    }

    /// <summary>COMOBJ byte 8 bit 3 exempts a kind from lava (spec 1.4 step 3); the port keeps
    /// the resistances of COMOBJ in the UWDamageable.</summary>
    private bool fIsFireResistant()
    {
        return mODamageable != null && UWDamageTypes.Scale(mODamageable.Resistances, 1, UWDamageTypes.Fire) == 0;
    }

    /// <summary>
    /// THE PLAYER IS AN OBSTACLE - the pre-check only (deviation 10): creatures set their
    /// position directly; if one ran into the player, his CharacterController pushed him out
    /// of the overlap, at a wall even out of the level (per user, 2026-09-12). A creature
    /// never gets closer to the player than the sum of both radii; whoever is not fighting
    /// does not even enter his tile. Moving away is always allowed. The refused step reaches
    /// the brain as a collision with item 0x7F, which sets the blocked bit like a wall.
    /// </summary>
    private bool fIsBlockedByPlayer(Vector3 pOWorldPosition)
    {
        if (!fEnsurePlayer() || mOLevelLoader == null)
            return false;

        Vector3 lOPlayer = mOPlayerTransform.position;
        Vector3 lOToTarget = pOWorldPosition - lOPlayer;
        Vector3 lOToMe = mOBody - lOPlayer;
        lOToTarget.y = 0f;
        lOToMe.y = 0f;

        if (lOToTarget.sqrMagnitude >= lOToMe.sqrMagnitude)
            return false;

        int liGoal = mORecord != null ? mORecord.Goal : 0;
        bool lbFighting = liGoal == GoalAttack || liGoal == GoalDistanceAttack || liGoal == GoalWithdraw
            || liGoal == GoalFollow || liGoal == GoalTalk;

        if (!lbFighting
            && mOLevelLoader.WorldPositionToTile(pOWorldPosition) == mOLevelLoader.WorldPositionToTile(lOPlayer))
            return true;

        if (mOPlayerBody == null)
            mOPlayerBody = mOPlayerTransform.GetComponentInParent<CharacterController>();

        Collider lOMine = GetComponent<Collider>();
        float lfMine = lOMine != null ? Mathf.Max(lOMine.bounds.extents.x, lOMine.bounds.extents.z) : 12f;
        float lfTheirs = mOPlayerBody != null ? mOPlayerBody.radius : 12f;
        float lfMinimum = lfMine + lfTheirs;

        return lOToTarget.sqrMagnitude < lfMinimum * lfMinimum;
    }

    /// <summary>
    /// Another creature's body in the way (ScanForCollisions: every object with COMOBJ height
    /// counts, creatures included): the step is refused when it would bring this body closer
    /// than the sum of both COMOBJ radii to another creature that it is not already that
    /// close to. The brain then decides - two attackers bumping is nothing, anyone else sets
    /// the blocked bit (NPC_Goto 46880-46937).
    /// </summary>
    private UWCritter fBlockingCreature(Vector3 pOWorldPosition)
    {
        int liOwnRadius;
        int liOwnHeight;
        fGetOwnSize(out liOwnRadius, out liOwnHeight);

        for (int liAt = 0; liAt < msActive.Count; liAt++)
        {
            UWCritter lOOther = msActive[liAt];

            if (lOOther == null || lOOther == this || lOOther.mbRemoved
                || (lOOther.mODamageable != null && lOOther.mODamageable.IsDestroyed))
                continue;

            int liOtherRadius;
            int liOtherHeight;
            lOOther.fGetOwnSize(out liOtherRadius, out liOtherHeight);

            float lfMinimum = (liOwnRadius + liOtherRadius) * WorldUnitsPerEighth;

            Vector3 lOToTarget = pOWorldPosition - lOOther.mOBody;
            Vector3 lOToMe = mOBody - lOOther.mOBody;
            lOToTarget.y = 0f;
            lOToMe.y = 0f;

            if (lOToTarget.sqrMagnitude >= lfMinimum * lfMinimum || lOToTarget.sqrMagnitude >= lOToMe.sqrMagnitude)
                continue;

            if (Mathf.Abs(pOWorldPosition.y - lOOther.mOBody.y) > Mathf.Max(liOwnHeight, liOtherHeight) * UWWorldScale.ZPosStep)
                continue;

            return lOOther;
        }

        return null;
    }

    /// <summary>Does a force field block this step? Per UW.EXE every object with COMOBJ
    /// height gets a box of tile * 8 + sub-position +- radius; the force field's COMOBJ byte
    /// 3 bit 2 is not set, so it blocks creatures too. Only the step INTO it is blocked.</summary>
    private bool fIsBlockedByForceField(Vector3 pOWorldPosition)
    {
        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null)
            return false;

        return fOverlapsForceField(pOWorldPosition) && !fOverlapsForceField(mOBody);
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

    private bool fOverlapsForceField(Vector3 pOWorldPosition)
    {
        int liOwnRadius;
        int liOwnHeight;
        fGetOwnSize(out liOwnRadius, out liOwnHeight);

        UWCommonObjectProperties lOCommon = fCommonProperties();

        int liFineX = UWViewpoint.WorldToOriginalX(pOWorldPosition.x) >> 5;
        int liFineY = UWViewpoint.WorldToOriginalY(pOWorldPosition.z) >> 5;
        int liZ = Mathf.RoundToInt(pOWorldPosition.y / UWObjectSpawner.HeightScale);

        int liTileMinX = Mathf.Max(0, (liFineX - liOwnRadius) >> 3);
        int liTileMaxX = Mathf.Min(UWLevelMeshBuilder.TilesPerAxis - 1, (liFineX + liOwnRadius) >> 3);
        int liTileMinY = Mathf.Max(0, (liFineY - liOwnRadius) >> 3);
        int liTileMaxY = Mathf.Min(UWLevelMeshBuilder.TilesPerAxis - 1, (liFineY + liOwnRadius) >> 3);

        for (int liTileY = liTileMinY; liTileY <= liTileMaxY; liTileY++)
        {
            for (int liTileX = liTileMinX; liTileX <= liTileMaxX; liTileX++)
            {
                UWTile lOTile = fGetTileAt(liTileX, liTileY);

                if (lOTile == null || lOTile.ObjectsInTile == null)
                    continue;

                foreach (UWObject lOObject in lOTile.ObjectsInTile)
                {
                    if (lOObject == null || lOObject.ID != ForceFieldObjectId)
                        continue;

                    int liRadius = FallbackForceFieldRadius;
                    int liHeight = FallbackForceFieldHeight;

                    if (lOCommon != null && lOCommon.TryGet(lOObject.ID, out UWCommonObjectProperties.Entry lOField))
                    {
                        liRadius = lOField.Radius;
                        liHeight = lOField.Height;
                    }

                    int liFieldX = (liTileX * 8) + lOObject.XPos;
                    int liFieldY = (liTileY * 8) + lOObject.YPos;

                    bool lbHorizontal = liFineX + liOwnRadius >= liFieldX - liRadius
                        && liFineX - liOwnRadius <= liFieldX + liRadius
                        && liFineY + liOwnRadius >= liFieldY - liRadius
                        && liFineY - liOwnRadius <= liFieldY + liRadius;

                    bool lbVertical = liZ + liOwnHeight >= lOObject.ZPos && liZ <= lOObject.ZPos + liHeight;

                    if (lbHorizontal && lbVertical)
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// A CLOSED DOOR blocks its tile - reported to the brain as the object hit, which tries,
    /// picks or bashes it when hostile (spec 7.5; before the rebuild the door blocked everyone
    /// silently and no creature ever opened one). The tile the creature already stands on
    /// stays free, otherwise it would be stuck when the door closes behind it.
    /// </summary>
    private UWObject fBlockingDoor(Vector3 pOWorldPosition, UWTile pOTileData)
    {
        if (pOTileData == null || mOLevelLoader == null)
            return null;

        UWTilePos lOTarget = mOLevelLoader.WorldPositionToTile(pOWorldPosition);
        UWTilePos lOMine = mOLevelLoader.WorldPositionToTile(mOBody);

        if (lOTarget == lOMine)
            return null;

        return fFindClosedDoor(pOTileData);
    }

    /// <summary>The closed door among a tile's objects, or null. Open doors and open
    /// portcullises do not block.</summary>
    private UWObject fFindClosedDoor(UWTile pOTileData)
    {
        if (pOTileData == null || pOTileData.ObjectsInTile == null || mOLevelLoader == null)
            return null;

        foreach (UWObject lOObject in pOTileData.ObjectsInTile)
        {
            if (lOObject == null || lOObject.GetCategory() != UWObject.ObjectCategoryEnum.Doors)
                continue;

            UWEntityInfo lOEntity;

            if (!mOLevelLoader.TryGetEntity(lOObject, out lOEntity) || lOEntity == null)
                continue;

            IUsableDoor lIDoor = lOEntity.GetComponentInParent<IUsableDoor>();

            if (lIDoor != null && lIDoor.IsClosed)
                return lOObject;
        }

        return null;
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

        if (piTileX < 0 || piTileZ < 0 || piTileX >= UWLevelMeshBuilder.TilesPerAxis
            || piTileZ >= UWLevelMeshBuilder.TilesPerAxis)
            return null;

        return mOLevelLoader.CurrentLevel.TileData[(piTileZ * UWLevelMeshBuilder.TilesPerAxis) + piTileX];
    }

    private bool fTryGetTile(Vector3 pOWorldPosition, out UWTile pOTileData)
    {
        pOTileData = null;

        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null)
            return false;

        int liX = UWUnits.WorldAxisToTile(pOWorldPosition.x);
        int liZ = UWUnits.WorldAxisToTile(pOWorldPosition.z);

        if (liX < 0 || liZ < 0 || liX >= UWLevelMeshBuilder.TilesPerAxis || liZ >= UWLevelMeshBuilder.TilesPerAxis)
            return false;

        pOTileData = mOLevelLoader.CurrentLevel.TileData[(liZ * UWLevelMeshBuilder.TilesPerAxis) + liX];

        return pOTileData != null;
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

    private float fGetFloorHeight(Vector3 pOWorldPosition)
    {
        UWTile lOTile = fGetTileAt(pOWorldPosition);

        return lOTile != null ? lOTile.FloorHeight : pOWorldPosition.y;
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

    /// <summary>For debugging only (CritterLogBlockedSteps): why a step failed, at most once
    /// per second per creature. Always false, so a probe can return it.</summary>
    private bool fBlocked(BlockKindEnum peKind, string psReason, Vector3 pOWorldPosition, ref BlockInfo pOBlock)
    {
        pOBlock.Kind = peKind;
        pOBlock.Reason = psReason;

        fTrace("step refused: {0}", psReason);

        UWSettings lOSettings = UWSettings.Instance;

        if (lOSettings != null && lOSettings.CritterLogBlockedSteps && Time.time >= mfNextBlockLogTime && mOLevelLoader != null)
        {
            UWTilePos lOTile = mOLevelLoader.WorldPositionToTile(pOWorldPosition);
            UWTilePos lOMine = mOLevelLoader.WorldPositionToTile(mOBody);

            mfNextBlockLogTime = Time.time + 1f;

            Debug.Log(string.Format("[Critter] {0} (goal {1}, {2}) cannot get from tile {3}/{4} onto {5}/{6}: {7}.",
                name, mORecord != null ? mORecord.Goal : 0, mbFlying ? "flies" : "walks", lOMine.X, lOMine.Y, lOTile.X, lOTile.Y, psReason));
        }

        return false;
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
