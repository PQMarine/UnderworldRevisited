using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE WORLD OF THE FLYING OBJECTS (stage 1 of the motion rework, 2026-10-05): every thrown
/// thing, arrow, spell missile, trap bolt and boulder is a mobile record of the original
/// (UWMobileRecord) that the engine-free motion core steps on the world clock
/// (UWMobileObjectMotion, 16 ticks per unit, as ProcessMobileObjects does). This component is
/// the core's host: it answers for the tiles and for the objects of a tile - the lying things of
/// the level, the creatures where their records stand, the player at his feet, the other
/// flights - and carries out what a hit, a landing or a removal does in the game. Unity shows
/// the result: UWProjectileFlight moves the picture between the steps.
///
/// Lives on the level object beside UWCritterDriver, which runs the due steps right after the
/// creatures' (Ensure).
///
/// THE SPLIT (cleanup step 3.5, 2026-10-17): this was UWProjectileWorld. The motion core's host
/// stayed here - the two interfaces IUWMotionWorld and IUWMobileObjectHost, unchanged, the
/// flights' list and their clock, the transient indices of the collision scans and the probes
/// over the same bodies (the closing door, the player's blow). What the flights do in the game -
/// the launches, a damage trap's strike, the hits, the blast and the impact - went to UWMissiles
/// (Missiles), which this host calls back. The thin UWProjectileWorld facade left for the trap
/// and spell hosts went in step 3.3 (2026-10-17), when they were rerouted.
/// </summary>
public class UWMotionHost : MonoBehaviour, IUWMotionWorld, IUWMobileObjectHost
{
    public static UWMotionHost Instance { get; private set; }

    /// <summary>The indices the flights and the lying objects are known by in the collision
    /// scans - transient, the level's slots are not taken (a deviation noted in Todo.md 11.6).</summary>
    private const int FirstFlightIndex = 1024;

    private const int FirstStaticIndex = 4096;

    private UWLevelLoader mOLoader;

    private UWMobileObjectMotion mOMotion;

    private UWMissiles mOMissiles;

    private readonly List<UWProjectileFlight> mOFlights = new List<UWProjectileFlight>();

    private readonly List<UWProjectileFlight> mOWalk = new List<UWProjectileFlight>();

    private readonly Dictionary<UWObject, int> mOStaticIndex = new Dictionary<UWObject, int>();

    private readonly Dictionary<int, UWObject> mOStaticByIndex = new Dictionary<int, UWObject>();

    private int miNextFlightIndex = FirstFlightIndex;

    private int miNextStaticIndex = FirstStaticIndex;

    /// <summary>The component on the level object, made when it is missing.</summary>
    public static UWMotionHost Ensure(UWLevelLoader pOLoader)
    {
        if (Instance == null && pOLoader != null)
        {
            Instance = pOLoader.GetComponent<UWMotionHost>();

            if (Instance == null)
                Instance = pOLoader.gameObject.AddComponent<UWMotionHost>();
        }

        return Instance;
    }

    private void Awake()
    {
        Instance = this;
        mOLoader = GetComponent<UWLevelLoader>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private UWMobileObjectMotion fMotion()
    {
        if (mOMotion == null && mOLoader != null && mOLoader.UWDataImporter != null)
            mOMotion = new UWMobileObjectMotion(this, this, mOLoader.UWDataImporter.CommonObjectProperties);

        return mOMotion;
    }

    /// <summary>The flying objects' motion (UWMobileObjectMotion), made when the level's data is
    /// there; null before. UWMissiles launches on it.</summary>
    public UWMobileObjectMotion ObjectMotion => fMotion();

    /// <summary>What the flights do in the game - launches, strikes, blast, impact (cleanup step
    /// 3.5, 2026-10-17).</summary>
    public UWMissiles Missiles
    {
        get
        {
            if (mOMissiles == null)
                mOMissiles = new UWMissiles(this);

            return mOMissiles;
        }
    }

    private UWCreatureMotion mOCreatureMotion;

    private UWTileRoute mOTileRoute;

    private UWTileTraverse mOTileTraverse;

    /// <summary>The creatures' tile routes on the same world (stage 2): the straight line and
    /// the path search of UW.EXE, shared by every creature (UWCritter sets its context on the
    /// traverse before a route runs).</summary>
    public UWTileRoute TileRoute
    {
        get
        {
            if (mOTileRoute == null)
                mOTileRoute = new UWTileRoute();

            return mOTileRoute;
        }
    }

    public UWTileTraverse TileTraverse
    {
        get
        {
            if (mOTileTraverse == null && mOLoader != null && mOLoader.UWDataImporter != null)
                mOTileTraverse = new UWTileTraverse(this, mOLoader.UWDataImporter.CommonObjectProperties, TileRoute);

            return mOTileTraverse;
        }
    }

    /// <summary>The creatures' motion on the same world (stage 2 of the motion rework):
    /// UWCritter.RunMotion steps its record on it.</summary>
    public UWCreatureMotion CreatureMotion
    {
        get
        {
            if (mOCreatureMotion == null && mOLoader != null && mOLoader.UWDataImporter != null)
                mOCreatureMotion = new UWCreatureMotion(this, mOLoader.UWDataImporter.CommonObjectProperties);

            return mOCreatureMotion;
        }
    }

    /// <summary>The COMOBJ entry of an object id, or the empty entry before the data is there.</summary>
    public UWCommonObjectProperties.Entry Entry(int piItemId)
    {
        UWCommonObjectProperties.Entry lOEntry;

        if (mOLoader != null && mOLoader.UWDataImporter != null && mOLoader.UWDataImporter.CommonObjectProperties != null
            && mOLoader.UWDataImporter.CommonObjectProperties.TryGet(piItemId, out lOEntry))
            return lOEntry;

        return default;
    }

    // ------------------------------------------------- The clock

    /// <summary>The due steps of every flight at this clock (ProcessMobileObjects, after the
    /// creatures). A flight that ends in its step has already been taken out.</summary>
    public void RunDue(UWCritterClock pOClock)
    {
        UWMobileObjectMotion lOMotion = fMotion();

        if (lOMotion == null)
            return;

        mOWalk.Clear();
        mOWalk.AddRange(mOFlights);

        foreach (UWProjectileFlight lOFlight in mOWalk)
        {
            if (lOFlight == null)
            {
                mOFlights.Remove(lOFlight);

                continue;
            }

            int liPhase = lOFlight.Record.Phase;

            if (lOMotion.RunDueSteps(lOFlight.Record, pOClock) && lOFlight.Record.Phase != liPhase)
                lOFlight.OnStepped(UWCritterDriver.PlayerHasSpeed);
        }
    }

    // ------------------------------------------------- The flights' list

    /// <summary>The world point of a record's fine position - its bottom.</summary>
    public static Vector3 WorldPosition(UWMobileRecord pORecord)
    {
        return UWViewpoint.OriginalToWorld(pORecord.FineX, pORecord.FineY, pORecord.FineZ);
    }

    /// <summary>The next transient index of a flight (from FirstFlightIndex up).</summary>
    public int NewFlightIndex()
    {
        return miNextFlightIndex++;
    }

    /// <summary>A flight joins the list the clock steps and the collision scans ask.</summary>
    public void AddFlight(UWProjectileFlight pOFlight)
    {
        mOFlights.Add(pOFlight);
    }

    /// <summary>The flight of this record is over: out of the list, its picture gone.</summary>
    public void EndFlight(UWMobileRecord pORecord)
    {
        for (int liAt = mOFlights.Count - 1; liAt >= 0; liAt--)
        {
            UWProjectileFlight lOFlight = mOFlights[liAt];

            if (lOFlight == null || lOFlight.Record == pORecord)
            {
                mOFlights.RemoveAt(liAt);

                if (lOFlight != null)
                    lOFlight.End();
            }
        }
    }

    /// <summary>The flight with this transient index, or null.</summary>
    public UWProjectileFlight FlightAt(int piIndex)
    {
        foreach (UWProjectileFlight lOFlight in mOFlights)
        {
            if (lOFlight != null && lOFlight.Record.Index == piIndex)
                return lOFlight;
        }

        return null;
    }

    private static UWCritter fCritter(int piIndex)
    {
        if (piIndex <= 1 || piIndex >= FirstFlightIndex)
            return null;

        // The registry by index (cleanup step 4.8, 2026-10-18; a walk of UWCritter.Active until then).
        return UWCritter.AtIndex(piIndex);
    }

    /// <summary>A collision index of a lying object (the transient static indices begin at 4096;
    /// below are the level's own indices of creatures and the player, above 1024 the flights).</summary>
    public static bool IsStaticIndex(int piIndex)
    {
        return piIndex >= FirstStaticIndex;
    }

    /// <summary>The lying object behind a transient static index, or null.</summary>
    public UWObject StaticObjectAt(int piIndex)
    {
        UWObject lOObject;

        return mOStaticByIndex.TryGetValue(piIndex, out lOObject) ? lOObject : null;
    }

    /// <summary>The level the transient static indices belong to; another level starts them
    /// afresh (the objects of the old one are gone, and the maps grew without bound until
    /// 2026-10-11).</summary>
    private UWLevel mOIndexedLevel;

    private int fStaticIndex(UWObject pOObject)
    {
        int liIndex;
        UWLevel lOLevel = mOLoader != null ? mOLoader.CurrentLevel : null;

        if (lOLevel != mOIndexedLevel)
        {
            mOStaticIndex.Clear();
            mOStaticByIndex.Clear();
            miNextStaticIndex = FirstStaticIndex;
            mOIndexedLevel = lOLevel;
        }

        if (!mOStaticIndex.TryGetValue(pOObject, out liIndex))
        {
            liIndex = miNextStaticIndex++;
            mOStaticIndex[pOObject] = liIndex;
            mOStaticByIndex[liIndex] = pOObject;
        }

        return liIndex;
    }

    /// <summary>A core of its own for the doorway test, so no running step's records are touched.</summary>
    private UWMotionCore mODoorCore;

    /// <summary>
    /// check_door and toast_animobj (EFFECT.C): a closing door tests every frame, and once more
    /// when it would shut, whether the CLOSED door fits where it stands - can_place with the
    /// closed id (0x140 + the door's kind and state), its own object number skipped, at its fine
    /// spot, its z less the 0x18 that opening raised it (not a portcullis), as a flier, range 8.
    /// Anything solid in the box - a creature, the player, a barrel - turns it round. Ours
    /// watched the player alone and shut on a goblin standing in the doorway (per user,
    /// 2026-10-13). The test is the motion core's CheckIfItemFitsInTile (can_place), over the
    /// bodies this world reports: the creatures' live records and the player.
    /// </summary>
    public bool ClosedDoorBlocked(UWObject pODoor)
    {
        UWCommonObjectProperties lOProperties = mOLoader != null && mOLoader.UWDataImporter != null
            ? mOLoader.UWDataImporter.CommonObjectProperties : null;

        if (pODoor == null || lOProperties == null)
            return false;

        int liId = pODoor.ID;

        if (liId < UWDoorFirstId || liId > UWDoorLastId)
            return false;

        int liClosedId = liId & ~UWDoorOpenBit;
        int liZ = pODoor.ZPos;

        if ((liId & UWDoorOpenBit) != 0 && (liId & 7) != UWPortcullisKind)
            liZ -= UWDoorOpenRise;

        if (mODoorCore == null)
            mODoorCore = new UWMotionCore(this, lOProperties);

        int liIndex = fStaticIndex(pODoor);

        mODoorCore.Mover = new UWMotionBody
        {
            Index = liIndex,
            ItemId = liClosedId,
            XPos = pODoor.XPos,
            YPos = pODoor.YPos,
            ZPos = liZ,
            Direction = pODoor.Heading & 7
        };

        return !mODoorCore.CheckIfItemFitsInTile(liClosedId, liIndex, (pODoor.TileX << 3) + pODoor.XPos,
            (pODoor.TileY << 3) + pODoor.YPos, liZ, true, DoorwayRange, out int _, out int _);
    }

    private const int UWDoorFirstId = 0x140;
    private const int UWDoorLastId = 0x14F;

    /// <summary>Ids 0x148-0x14F are the open doors of 0x140-0x147.</summary>
    private const int UWDoorOpenBit = 8;

    /// <summary>Kind 6 is the portcullis, which rises without its z changing.</summary>
    private const int UWPortcullisKind = 6;

    /// <summary>OpenDoor raises a door's z by this; closing takes it off again.</summary>
    private const int UWDoorOpenRise = 0x18;

    /// <summary>can_place's range in check_door.</summary>
    private const int DoorwayRange = 8;

    /// <summary>A core of its own for the blow's probe, so no running step's records are touched.</summary>
    private UWMotionCore mOBlowCore;

    /// <summary>
    /// WHAT THE PLAYER'S BLOW STRIKES (resolve_attack, UWPlayerAttack.PickBlowTarget) over the
    /// bodies this world reports: the lying objects of the level data, the creatures' live records.
    /// The probe in the original's units: centre in eighths, z, radius and height. Returns
    /// PickBlowTarget's answer; on a hit pOObject is the lying object or pOCritter the creature.
    /// </summary>
    public int FindBlowTarget(int piX8, int piY8, int piZ, int piRadius, int piHeight, int piAttackerX8, int piAttackerY8,
        out UWObject pOObject, out UWCritter pOCritter)
    {
        pOObject = null;
        pOCritter = null;

        UWCommonObjectProperties lOProperties = mOLoader != null && mOLoader.UWDataImporter != null
            ? mOLoader.UWDataImporter.CommonObjectProperties : null;

        if (lOProperties == null)
            return UWPlayerAttack.BlowFoundNothing;

        if (mOBlowCore == null)
            mOBlowCore = new UWMotionCore(this, lOProperties);

        // The player is the mover: object 127, a creature (ObjectCheck's isnpc).
        mOBlowCore.Mover = new UWMotionBody
        {
            Index = 1,
            ItemId = UWObjectMechanics.AdventurerObjectId,
            IsMobile = true,
            IsCreature = true
        };
        mOBlowCore.SetCalc(piX8, piY8, piZ, piRadius, piHeight, 1);

        int liFound = UWPlayerAttack.PickBlowTarget(mOBlowCore, piAttackerX8, piAttackerY8, pOBody =>
        {
            UWCritter lOCritter = fCritter(pOBody.Index);

            return lOCritter != null && lOCritter.IsAlly;
        });

        if (liFound < 0)
            return liFound;

        int liIndex = mOBlowCore.Records[liFound].Body.Index;

        if (IsStaticIndex(liIndex))
            pOObject = StaticObjectAt(liIndex);
        else
            pOCritter = fCritter(liIndex);

        return liFound;
    }

    // ------------------------------------------------- IUWMotionWorld

    public bool TryGetTile(int piTileX, int piTileY, out int piType, out int piFloorNibble, out int piTerrain)
    {
        piType = 0;
        piFloorNibble = 0;
        piTerrain = 0;

        UWTile lOTile = mOLoader != null && mOLoader.CurrentLevel != null ? mOLoader.CurrentLevel.GetTile(piTileX, piTileY) : null;

        if (lOTile == null)
            return false;

        piType = (int)lOTile.TileType & 0xF;
        piFloorNibble = (lOTile.FloorHeight >> 4) & 0xF;

        UWTerrain lOTerrain = mOLoader.UWDataImporter != null ? mOLoader.UWDataImporter.Terrain : null;

        if (lOTerrain != null)
            piTerrain = ((int)lOTerrain.GetFloorTerrain(lOTile.TextureFloor) & 0xFF) >> 4;

        return true;
    }

    public void GetBodiesInTile(int piTileX, int piTileY, List<UWMotionBody> pOInto)
    {
        UWTile lOTile = mOLoader != null && mOLoader.CurrentLevel != null ? mOLoader.CurrentLevel.GetTile(piTileX, piTileY) : null;

        if (lOTile == null)
            return;

        // The lying things of the tile; the creatures come from their live records below.
        foreach (UWObject lOObject in lOTile.ObjectsInTile)
        {
            if (lOObject == null || lOObject is UWNpc)
                continue;

            pOInto.Add(new UWMotionBody
            {
                Index = fStaticIndex(lOObject),
                ItemId = lOObject.ID,
                XPos = lOObject.XPos,
                YPos = lOObject.YPos,
                ZPos = lOObject.ZPos,
                Direction = lOObject.Heading & 7
            });
        }

        foreach (UWCritter lOCritter in UWCritter.Active)
        {
            UWCritterRecord lORecord = lOCritter != null ? lOCritter.Record : null;

            if (lORecord == null || lORecord.TileX != piTileX || lORecord.TileY != piTileY)
                continue;

            pOInto.Add(new UWMotionBody
            {
                Index = lOCritter.Index,
                ItemId = lORecord.ItemId,
                XPos = lORecord.FineX,
                YPos = lORecord.FineY,
                ZPos = lORecord.ZPos,
                IsMobile = true,
                IsCreature = true,
                Direction = lORecord.FacingEighth
            });
        }

        // THE PLAYER: on the motion core (stage 3) his params block says where he is; before
        // it, the camera's feet.
        UWPlayerMovement lOMovement = UWScene.PlayerMovement;

        if (lOMovement != null && lOMovement.UsesMotionCore)
        {
            if (lOMovement.Motion.TileX == piTileX && lOMovement.Motion.TileY == piTileY)
                pOInto.Add(lOMovement.Motion.Body);
        }
        else
        {
            Camera lOCamera = Camera.main;

            if (lOCamera != null)
            {
                Vector3 lOFeet = UWPlayerThrow.GetFeet(lOCamera.transform);
                int liX8 = UWViewpoint.WorldToOriginalX(lOFeet.x) >> 5;
                int liY8 = UWViewpoint.WorldToOriginalY(lOFeet.z) >> 5;

                if ((liX8 >> 3) == piTileX && (liY8 >> 3) == piTileY)
                {
                    pOInto.Add(new UWMotionBody
                    {
                        Index = 1,
                        ItemId = UWObjectMechanics.AdventurerObjectId,
                        XPos = liX8 & 7,
                        YPos = liY8 & 7,
                        ZPos = UWViewpoint.WorldToOriginalZ(lOFeet.y) >> 3,
                        IsMobile = true,
                        IsCreature = true
                    });
                }
            }
        }

        foreach (UWProjectileFlight lOFlight in mOFlights)
        {
            if (lOFlight != null && lOFlight.Record.TileX == piTileX && lOFlight.Record.TileY == piTileY)
                pOInto.Add(lOFlight.Record.ToBody());
        }
    }

    /// <summary>The original's rand, 0..0x7FFF, from the session's generator (cleanup step 2.8,
    /// 2026-10-14).</summary>
    public int Random()
    {
        return UWGameSession.Current.Random.Next(UWOriginalRandom.Range);
    }

    public void UseOnMover(int piOtherIndex, int piMoverIndex, int piHitTileX, int piHitTileY)
    {
        // The glowing rock joining a carried one is the player's (UWTouchGather); a flying
        // thing passing it does nothing in the original either (the user is not the player).
    }

    /// <summary>
    /// A mover's box met a record of class 6 (CollideObjects_seg029_29EE_173 -> Trigger_ovr153_3B).
    /// The a_move trigger has COMOBJ height 32 and radius 2 and is not solid, so it enters every
    /// scan - the comment here said "height 0, never" until 2026-10-06. The original fires it on
    /// EVERY scan in which the boxes overlap, so on every motion frame while the mover moves in
    /// its reach, not on entering (per user on the original the same day: the text of the poison
    /// needles on level 3 "comes very often" on entering).
    ///
    /// WHO SETS IT OFF (Trigger_ovr153_3B labels EC to 1A7): the player (index 1) when the
    /// trigger's flags bit 2 (word 0 bit 0x800) is set - so never the ward rune's (flags 0); a
    /// creature when bit 3 (0x1000) is set; a thing - thrown, shot, knocked loose - unless bit 3
    /// is set without bit 2 (per user on the original the same day: creatures and thrown things
    /// close the grate before Drog, flags 14). Queued here and fired after the motion: inside
    /// the step a teleport would be overwritten by the step's own write-back. The player's by
    /// FirePendingPlayerTriggers after his frame, the others in LateUpdate.
    /// </summary>
    public int TriggerMove(int piMoverIndex, int piOtherIndex, int piHitTileX, int piHitTileY)
    {
        if (!IsStaticIndex(piOtherIndex))
            return 2;

        UWObject lOTrigger = StaticObjectAt(piOtherIndex);

        if (lOTrigger == null || lOTrigger.ID != UWObjectMechanics.MoveTriggerId)
            return 2;

        if (piMoverIndex == 1)
        {
            if (UWTrapRules.MaySetOff(lOTrigger, UWTrapRules.Setter.Player)
                && !mOPendingPlayerTriggers.Contains(lOTrigger))
                mOPendingPlayerTriggers.Add(lOTrigger);

            return 2;
        }

        bool lbCreature = fCritter(piMoverIndex) != null;

        if (!UWTrapRules.MaySetOff(lOTrigger, lbCreature ? UWTrapRules.Setter.Creature : UWTrapRules.Setter.Thing))
            return 2;

        foreach (PendingTrigger lOPending in mOPendingOtherTriggers)
        {
            if (lOPending.Mover == piMoverIndex && lOPending.Trigger == lOTrigger)
                return 2;
        }

        mOPendingOtherTriggers.Add(new PendingTrigger { Mover = piMoverIndex, Trigger = lOTrigger, IsCreature = lbCreature });

        return 2;
    }

    private struct PendingTrigger
    {
        public int Mover;

        public UWObject Trigger;

        public bool IsCreature;
    }

    private readonly List<PendingTrigger> mOPendingOtherTriggers = new List<PendingTrigger>();

    /// <summary>When each mover's trigger last fired, on the game's clock (UWTriggerPacer, since
    /// 2026-10-17; a Time.time dictionary before).</summary>
    private readonly UWTriggerPacer mOOtherTriggerPacer = new UWTriggerPacer(256);

    /// <summary>The triggers creatures and things met this frame, fired after all their steps
    /// with the same spacing as the player's, with the mover as the one struck (damage and
    /// teleport go to it, see UWTrapRules).</summary>
    private void LateUpdate()
    {
        if (mOPendingOtherTriggers.Count == 0 || mOLoader == null)
            return;

        PendingTrigger[] lOPending = mOPendingOtherTriggers.ToArray();
        UWLevel lOLevel = mOLoader.CurrentLevel;
        Interaction lOInteraction = UWScene.Interaction;
        long llNow = UWGameSession.Current.Schedule.Now;

        mOPendingOtherTriggers.Clear();
        mOOtherTriggerPacer.BeginBatch();

        foreach (PendingTrigger lOAt in lOPending)
        {
            if (mOLoader.CurrentLevel != lOLevel)
                break;

            if (!mOOtherTriggerPacer.IsReady(lOAt.Trigger, lOAt.Mover, llNow))
                continue;

            UWCritter lOCritter = lOAt.IsCreature ? fCritter(lOAt.Mover) : null;

            if ((lOAt.IsCreature && lOCritter == null) || !fStillListed(lOAt.Trigger))
                continue;

            mOOtherTriggerPacer.MarkFired(lOAt.Trigger, lOAt.Mover, llNow);
            UWTriggerSystem.TryFireMoveTriggerBy(lOAt.Trigger, mOLoader, lOInteraction, lOCritter,
                lOAt.IsCreature ? 0 : lOAt.Mover);
        }
    }

    private readonly List<UWObject> mOPendingPlayerTriggers = new List<UWObject>();

    /// <summary>How often one trigger fires at most while the player moves in its reach: once per
    /// clock unit (UWTriggerPacer, which carries the reasoning; PlayerTriggerSpacingSeconds and
    /// Time.time until 2026-10-17).</summary>
    private readonly UWTriggerPacer mOPlayerTriggerPacer = new UWTriggerPacer(64);

    /// <summary>The player's index as a mover (TriggerMove), the pacer's key.</summary>
    private const int PlayerMover = 1;

    /// <summary>The move triggers the player's motion met since the last call (TriggerMove),
    /// each fired once - unless it fired in the same clock unit (UWTriggerPacer). A level change
    /// by one of them drops the rest.</summary>
    public void FirePendingPlayerTriggers()
    {
        if (mOPendingPlayerTriggers.Count == 0 || mOLoader == null)
            return;

        UWObject[] lOTriggers = mOPendingPlayerTriggers.ToArray();
        UWLevel lOLevel = mOLoader.CurrentLevel;
        Interaction lOInteraction = UWScene.Interaction;
        long llNow = UWGameSession.Current.Schedule.Now;

        mOPendingPlayerTriggers.Clear();
        mOPlayerTriggerPacer.BeginBatch();

        foreach (UWObject lOTrigger in lOTriggers)
        {
            if (mOLoader.CurrentLevel != lOLevel)
                break;

            if (!mOPlayerTriggerPacer.IsReady(lOTrigger, PlayerMover, llNow))
                continue;

            if (!fStillListed(lOTrigger))
                continue;

            mOPlayerTriggerPacer.MarkFired(lOTrigger, PlayerMover, llNow);
            UWTriggerSystem.TryFireMoveTrigger(lOTrigger, mOLoader, lOInteraction);
        }
    }

    /// <summary>
    /// Whether a queued trigger still lies in its tile's list. A one-shot trigger that an earlier
    /// firing of the same batch took out (its own, or another mover's) is not met any more - the
    /// original fires each inside its mover's own step, after the cleanup of the one before. Since
    /// 2026-10-12, for the rune of warding: two creatures over it in one frame were both hit.
    /// </summary>
    private bool fStillListed(UWObject pOTrigger)
    {
        UWTile lOTile = mOLoader != null && mOLoader.CurrentLevel != null
            ? mOLoader.CurrentLevel.GetTile(pOTrigger.TileX, pOTrigger.TileY) : null;

        return lOTile != null && lOTile.ObjectsInTile != null && lOTile.ObjectsInTile.Contains(pOTrigger);
    }

    public bool MissileHits(int piMoverIndex, int piOtherIndex, int piMoverTileX, int piMoverTileY)
    {
        UWProjectileFlight lOFlight = FlightAt(piMoverIndex);

        if (lOFlight == null)
            return false;

        if (piOtherIndex == 1)
            return Missiles.StrikePlayer(lOFlight);

        UWCritter lOCritter = fCritter(piOtherIndex);

        if (lOCritter != null)
            return Missiles.StrikeCreature(lOFlight, lOCritter);

        UWObject lOObject;

        if (mOStaticByIndex.TryGetValue(piOtherIndex, out lOObject))
            return Missiles.StrikeObject(lOFlight, lOObject);

        return false;
    }

    /// <summary>The momentum transfer (seg029_29EE_3): a creature's record takes the heading,
    /// the speed byte 5 and the vertical speed (UWCritter.Push); the player's record is written
    /// but never read by his motion in the original; a lying object is KNOCKED LOOSE (stage 2,
    /// 2026-10-05): it becomes a flight again - MakeMobile with the transferred heading, speed
    /// and vertical speed and no gravity bit - and slides, drops off its ledge or bounces as a
    /// thrown thing does, to lie where it stops.</summary>
    public void PushObject(int piOtherIndex, int piHeading, int piSpeed, int piVz, int piHitTileX, int piHitTileY)
    {
        UWCritter lOCritter = fCritter(piOtherIndex);

        if (lOCritter != null)
        {
            lOCritter.Push(piHeading, piSpeed, piVz);

            return;
        }

        UWObject lOObject;

        if (piOtherIndex < FirstStaticIndex || fMotion() == null || mOLoader == null
            || !mOStaticByIndex.TryGetValue(piOtherIndex, out lOObject) || lOObject == null)
            return;

        UWMobileRecord lORecord = new UWMobileRecord
        {
            Object = lOObject,
            Index = NewFlightIndex(),
            XPos = lOObject.XPos & 7,
            YPos = lOObject.YPos & 7,
            ZPos = lOObject.ZPos & 0x7F,
            Octant = lOObject.Heading & 7,
            Hp = lOObject.Quality & 0x3F
        };

        fMotion().MakeMobile(lORecord, lOObject.TileX, lOObject.TileY);

        // The write-back of the transferred params (ApplyProjectileMotion on the struck
        // record): heading, speed byte, vertical speed field; a static object's gravity is 0.
        lORecord.HeadingByte = (piHeading >> 8) & 0xFF;
        lORecord.SpeedByte = (piSpeed / 0x2F) & 0x7F;
        lORecord.VzField = Mathf.Clamp((piVz / 64) + 16, 0, 31);
        lORecord.GravityBit = false;
        lORecord.StaticHeading = lOObject.Heading & 7;

        if (!mOLoader.RemoveObjectFromWorld(lOObject))
            return;

        // Both maps: the thing comes back to rest as the same object and gets a fresh index then
        // (until 2026-10-11 only the reverse map forgot it, so its old index answered nothing).
        mOStaticByIndex.Remove(piOtherIndex);
        mOStaticIndex.Remove(lOObject);
        Missiles.KnockLoose(lORecord);
    }

    public void PlaySoundAt(int piSound, int piX8, int piY8, int piVolume)
    {
        Vector3 lOAt = UWViewpoint.OriginalToWorld((piX8 << 5) + 16, (piY8 << 5) + 16, 0);

        if (mOLoader != null)
            lOAt.y = mOLoader.GetFloorHeightAt(lOAt);

        // The volume argument is the original's offset on the effect's loudness (a landing is
        // louder for a heavier thing, see UWMobileObjectMotion); dropped until 2026-10-11.
        UWSoundEffects.PlayAt(piSound, lOAt, piVolume);
    }

    // ------------------------------------------------- IUWMobileObjectHost

    public int DungeonLevel => mOLoader != null ? mOLoader.CurrentLevelIndex + 1 : 0;

    public int ClockPhase => UWScene.CritterClock != null ? UWScene.CritterClock.Phase : 0;

    public void MoveToTile(UWMobileRecord pORecord, int piOldTileX, int piOldTileY, int piNewTileX, int piNewTileY)
    {
        // A flight is in no tile list; the scans ask the flights themselves.
    }

    public bool DamageSelf(UWMobileRecord pORecord, int piDamage, int piType)
    {
        UWCommonObjectProperties.Entry lOEntry = Entry(pORecord.ItemId);
        int liScaled = UWDamageTypes.Scale(lOEntry.Resistances, piDamage, piType == 8 ? UWDamageTypes.PlainFire : UWDamageTypes.None);
        int liNewHp;

        if (!UWObjectDamageRules.Wear(pORecord.Hp, liScaled, lOEntry.QualityClass, false, out liNewHp))
        {
            pORecord.Hp = liNewHp;

            return false;
        }

        pORecord.Hp = 0;
        EndFlight(pORecord);

        return true;
    }

    public void PlaySoundAtObject(int piSound, UWMobileRecord pORecord, int piVolume)
    {
        UWSoundEffects.PlayAt(piSound, WorldPosition(pORecord), piVolume);
    }

    public void Splash(UWMobileRecord pORecord)
    {
        // The picture alone: the core has played the water sound at the impact (DoCollision).
        if (mOLoader != null)
            mOLoader.SpawnEffectAt(UWObjectMechanics.SplashEffectObjectId, WorldPosition(pORecord), UWLevelLoader.SplashEffectSeconds);
    }

    public bool TalismanBurns(UWMobileRecord pORecord, int piTileX, int piTileY)
    {
        return mOLoader != null && UWEndgame.TryDestroyTalismanInLava(mOLoader, pORecord.Object, piTileX, piTileY,
            WorldPosition(pORecord));
    }

    public void OnDemoted(UWMobileRecord pORecord)
    {
        if (mOLoader != null && mOLoader.UWDataImporter != null)
            UWObjectMechanics.ExtinguishInWorld(pORecord.Object, mOLoader.UWDataImporter.Textures);
    }

    public int Detonate(UWMobileRecord pORecord, int piTileX, int piTileY, int piOwner)
    {
        UWProjectileFlight lOFlight = FlightAt(pORecord.Index);

        // DetonateProjectile at rest: the blast on the tile and the explosion's picture.
        if (lOFlight != null)
            Missiles.ApplyBlast(lOFlight, piTileX, piTileY, piOwner);

        return 0;
    }

    public void Remove(UWMobileRecord pORecord)
    {
        EndFlight(pORecord);
    }

    public void PlaceAtRest(UWMobileRecord pORecord, int piOctant, int piQuality)
    {
        EndFlight(pORecord);

        UWObject lOObject = pORecord.Object;

        if (lOObject == null || mOLoader == null)
            return;

        lOObject.Quality = (ushort)(piQuality & 0x3F);
        lOObject.Heading = (ushort)(piOctant & 7);
        lOObject.TileX = pORecord.TileX;
        lOObject.TileY = pORecord.TileY;
        lOObject.XPos = (ushort)pORecord.XPos;
        lOObject.YPos = (ushort)pORecord.YPos;
        lOObject.ZPos = pORecord.ZPos;

        // mob_to_static's Obj_Add: at the head of the tile's list (2026-10-18).
        mOLoader.PlaceRestingObject(lOObject, pORecord.TileX, pORecord.TileY, UWWorldEdit.Arrival.Landed);
    }

    public UWLevelLoader Loader => mOLoader;
}
