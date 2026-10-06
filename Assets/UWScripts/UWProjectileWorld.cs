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
/// </summary>
public class UWProjectileWorld : MonoBehaviour, IUWMotionWorld, IUWMobileObjectHost
{
    public static UWProjectileWorld Instance { get; private set; }

    /// <summary>The indices the flights and the lying objects are known by in the collision
    /// scans - transient, the level's slots are not taken (a deviation noted in Todo.md 11.6).</summary>
    private const int FirstFlightIndex = 1024;

    private const int FirstStaticIndex = 4096;

    /// <summary>The boulder of the quake: on sub-position 3/3 of its tile, just below the
    /// ceiling (QuakeSpell_seg038_3307_1211).</summary>
    private const int BoulderSpawnZPos = 0x6E;

    private const int BoulderSubTile = 3;

    /// <summary>How long a missile's impact picture stays.</summary>
    private const float ImpactSeconds = 0.7f;

    private UWLevelLoader mOLoader;

    private UWMobileObjectMotion mOMotion;

    private readonly List<UWProjectileFlight> mOFlights = new List<UWProjectileFlight>();

    private readonly List<UWProjectileFlight> mOWalk = new List<UWProjectileFlight>();

    private readonly Dictionary<UWObject, int> mOStaticIndex = new Dictionary<UWObject, int>();

    private readonly Dictionary<int, UWObject> mOStaticByIndex = new Dictionary<int, UWObject>();

    private int miNextFlightIndex = FirstFlightIndex;

    private int miNextStaticIndex = FirstStaticIndex;

    /// <summary>The component on the level object, made when it is missing.</summary>
    public static UWProjectileWorld Ensure(UWLevelLoader pOLoader)
    {
        if (Instance == null && pOLoader != null)
        {
            Instance = pOLoader.GetComponent<UWProjectileWorld>();

            if (Instance == null)
                Instance = pOLoader.gameObject.AddComponent<UWProjectileWorld>();
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

    private UWCommonObjectProperties.Entry fEntry(int piItemId)
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

    // ------------------------------------------------- The launches

    /// <summary>The heading (1/256 of a turn, 0 north) and the pitch of a missile flying along
    /// a direction at this speed byte: the pitch is the vertical part in units of 64 against the
    /// horizontal speed byte * 0x2F (UWPlayerThrow.GetMissileDirection the other way round).</summary>
    public static void AimFromDirection(Vector3 pODirection, int piSpeedByte, out int piHeading, out int piPitch)
    {
        float lfHorizontal = Mathf.Sqrt((pODirection.x * pODirection.x) + (pODirection.z * pODirection.z));

        piHeading = lfHorizontal > 0.0001f
            ? Mathf.RoundToInt(Mathf.Atan2(pODirection.x, pODirection.z) * Mathf.Rad2Deg * 256f / 360f) & 0xFF
            : 0;

        if (lfHorizontal > 0.0001f)
            piPitch = Mathf.RoundToInt(pODirection.y / lfHorizontal * piSpeedByte * 0x2F / 64f);
        else
            piPitch = pODirection.y > 0f ? 15 : -16;

        piPitch = Mathf.Clamp(piPitch, -16, 15);
    }

    /// <summary>
    /// The player's missile: a shot, a throw (pOItem is the thrown thing itself) or a spell
    /// projectile, at an absolute heading and a pitch (UWPlayerThrow.GetMissileAim), with the
    /// speed byte of the ammunition table (0x0F for a throw). Null when there is no room in
    /// front of the player. piHp is the item's quality for a thrown thing or an arrow, 0x3F
    /// for a spell.
    /// </summary>
    public UWProjectileFlight LaunchFromPlayer(UWObject pOItem, int piItemId, int piHeading, int piPitch, int piSpeedByte,
        int piDamage, int piDamageType, int piImpactId, Interaction pOInteraction, int piHp)
    {
        Camera lOCamera = Camera.main;

        if (lOCamera == null || fMotion() == null)
            return null;

        Vector3 lOFeet = UWPlayerThrow.GetFeet(lOCamera.transform);
        int liX8 = UWViewpoint.WorldToOriginalX(lOFeet.x) >> 5;
        int liY8 = UWViewpoint.WorldToOriginalY(lOFeet.z) >> 5;
        UWCommonObjectProperties.Entry lOPlayer = fEntry(UWObjectMechanics.AdventurerObjectId);
        UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

        UWMobileObjectMotion.Launcher lOLauncher = new UWMobileObjectMotion.Launcher
        {
            Index = 1,
            IsCreature = true,
            IsPlayer = true,
            TileX = liX8 >> 3,
            TileY = liY8 >> 3,
            XPos = liX8 & 7,
            YPos = liY8 & 7,
            ZPos = UWViewpoint.WorldToOriginalZ(lOFeet.y) >> 3,
            Octant = (piHeading >> 5) & 7,
            FineHeadingBits = piHeading & 0x1F,
            Height = lOPlayer.Height,
            Radius = lOPlayer.Radius,
            SwimCounter = lOTerrain != null ? lOTerrain.SwimCounter : 0
        };

        return fLaunch(pOItem, piItemId, lOLauncher, 0, piPitch, piSpeedByte, piDamage, piDamageType, piImpactId,
            pOInteraction, null, piHp);
    }

    /// <summary>A creature's missile or spell projectile along its facing, at the pitch of its
    /// aim (GetPitchToGTarg). Null when the start has no room - the original makes no missile then.</summary>
    public UWProjectileFlight LaunchFromCreature(UWCritter pOCritter, int piItemId, int piPitch, int piDamage,
        int piDamageType, int piImpactId)
    {
        if (pOCritter == null || pOCritter.Record == null || fMotion() == null)
            return null;

        UWCritterRecord lORecord = pOCritter.Record;
        UWCommonObjectProperties.Entry lOOwn = fEntry(lORecord.ItemId);

        UWMobileObjectMotion.Launcher lOLauncher = new UWMobileObjectMotion.Launcher
        {
            Index = pOCritter.Index,
            IsCreature = true,
            TileX = lORecord.TileX,
            TileY = lORecord.TileY,
            XPos = lORecord.FineX,
            YPos = lORecord.FineY,
            ZPos = lORecord.ZPos,
            Octant = lORecord.FacingEighth & 7,
            FineHeadingBits = lORecord.FullFacing & 0x1F,
            Height = lOOwn.Height,
            Radius = lOOwn.Radius
        };

        return fLaunch(null, piItemId, lOLauncher, 0, piPitch, piSpeedByte: fSpeedByte(piItemId), piDamage: piDamage,
            piDamageType: piDamageType, piImpactId: piImpactId, pOInteraction: null, pOCritter: pOCritter, piHp: 0x3F);
    }

    /// <summary>A missile from a spot without a body of its own - the arrow trap, the spell
    /// trap: launched at the spot itself, no height offset, no placement test (a launcher of
    /// height 0).</summary>
    public UWProjectileFlight LaunchFromSpot(int piItemId, int piTileX, int piTileY, int piXPos, int piYPos, int piZPos,
        int piHeading, int piPitch, int piSpeedByte, int piDamage, int piDamageType, int piImpactId, Interaction pOInteraction)
    {
        if (fMotion() == null)
            return null;

        UWMobileObjectMotion.Launcher lOLauncher = new UWMobileObjectMotion.Launcher
        {
            Index = 0,
            TileX = piTileX,
            TileY = piTileY,
            XPos = piXPos,
            YPos = piYPos,
            ZPos = piZPos,
            Octant = (piHeading >> 5) & 7,
            FineHeadingBits = piHeading & 0x1F,
            Height = 0,
            Radius = 0
        };

        return fLaunch(null, piItemId, lOLauncher, 0, piPitch, piSpeedByte, piDamage, piDamageType, piImpactId,
            pOInteraction, null, 0x3F);
    }

    /// <summary>The ammunition table's speed byte of a missile.</summary>
    private int fSpeedByte(int piItemId)
    {
        return mOLoader != null && mOLoader.UWDataImporter != null && mOLoader.UWDataImporter.ObjectProperties != null
            ? mOLoader.UWDataImporter.ObjectProperties.GetRangedSpeed(piItemId)
            : 0;
    }

    /// <summary>A missile made for the flight (an arrow shot, a spell, a trap's bolt): a fresh
    /// object with its picture - the spawner bails out silently for one without (per user,
    /// 2026-10-05: shot arrows vanished where they fell) - and a stack of one when its kind
    /// counts (PrepareNewObjectProps_seg035_3E, as a landed projectile has it).</summary>
    private UWObject fNewObject(int piItemId)
    {
        UWObject lOObject = new UWObject((ushort)piItemId);
        UWCommonObjectProperties.Entry lOEntry = fEntry(piItemId);

        if (lOEntry.StartsWithQuantity)
        {
            lOObject.HasQuantity = true;
            lOObject.Quantity = 1;
        }

        if (mOLoader != null && mOLoader.UWDataImporter != null && mOLoader.UWDataImporter.Textures != null)
        {
            try
            {
                lOObject.Texture = mOLoader.UWDataImporter.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, piItemId);
            }
            catch
            {
                lOObject.Texture = null;
            }
        }

        return lOObject;
    }

    private UWProjectileFlight fLaunch(UWObject pOItem, int piItemId, UWMobileObjectMotion.Launcher pOLauncher, int piMissileHeading,
        int piPitch, int piSpeedByte, int piDamage, int piDamageType, int piImpactId, Interaction pOInteraction,
        UWCritter pOCritter, int piHp)
    {
        UWMobileRecord lORecord = new UWMobileRecord
        {
            Object = pOItem ?? fNewObject(piItemId),
            Index = miNextFlightIndex++
        };

        if (!fMotion().TryLaunch(lORecord, pOLauncher, true, piMissileHeading, piPitch, piSpeedByte))
            return null;

        lORecord.Hp = piHp;

        // The thing lies in its launch direction when it comes to rest (per user, 2026-09-07:
        // arrows and bolts lie in the facing).
        lORecord.StaticHeading = lORecord.Octant;

        return fSpawnFlight(lORecord, piDamage, piDamageType, piImpactId, pOInteraction, pOCritter, pOInteraction != null && pOCritter == null);
    }

    /// <summary>The quake's boulder: a piece of debris with a random small speed and schedule
    /// (QuakeSpell_seg038_3307_1211), falling from just below the ceiling.</summary>
    public UWProjectileFlight DropBoulder(int piItemId, int piTileX, int piTileY)
    {
        if (fMotion() == null)
            return null;

        UWCritterClock lOClock = UWScene.CritterClock;

        UWMobileRecord lORecord = new UWMobileRecord
        {
            Object = fNewObject(piItemId),
            Index = miNextFlightIndex++,
            TileX = piTileX,
            TileY = piTileY,
            XPos = BoulderSubTile,
            YPos = BoulderSubTile,
            ZPos = BoulderSpawnZPos,
            FineX = (piTileX << 8) + (BoulderSubTile << 5) + 0xF,
            FineY = (piTileY << 8) + (BoulderSubTile << 5) + 0xF,
            FineZ = BoulderSpawnZPos << 3,
            HeadingByte = Random() & 0xFF,
            SpeedByte = (Random() & 3) + 2,
            GravityBit = !fEntry(piItemId).IsWeightless,
            VzField = 16,
            Period = (Random() % 3) + 1,
            Phase = ((lOClock != null ? lOClock.Phase : 0) + (Random() & 3)) & 0xF,
            Hp = 0x3F
        };

        lORecord.Octant = lORecord.HeadingByte >> 5;
        lORecord.StaticHeading = lORecord.Octant;

        return fSpawnFlight(lORecord, 0, UWDamageTypes.Physical, -1, null, null, false);
    }

    private UWProjectileFlight fSpawnFlight(UWMobileRecord pORecord, int piDamage, int piDamageType, int piImpactId,
        Interaction pOInteraction, UWCritter pOCritter, bool pbPlayersOwn, bool pbLaunchSound = true)
    {
        Vector3 lOAt = WorldPosition(pORecord);
        GameObject lOSpawned = mOLoader.SpawnProjectile(pORecord.ItemId, lOAt, UWProjectileFlight.HeadingToDirection(pORecord.HeadingByte));

        if (lOSpawned == null)
            return null;

        UWProjectileFlight lOFlight = lOSpawned.AddComponent<UWProjectileFlight>();

        lOFlight.Begin(this, pORecord, piDamage, piDamageType, piImpactId, pOInteraction, pOCritter, pbPlayersOwn);
        mOFlights.Add(lOFlight);

        // The launch sound of every missile (PrepareProjectileObject, effect 0x0A); a thing
        // knocked loose makes none.
        if (pbLaunchSound)
            UWSoundEffects.PlayAt(UWSoundEffects.Miss, lOAt);

        return lOFlight;
    }

    /// <summary>The world point of a record's fine position - its bottom.</summary>
    public static Vector3 WorldPosition(UWMobileRecord pORecord)
    {
        return UWViewpoint.OriginalToWorld(pORecord.FineX, pORecord.FineY, pORecord.FineZ);
    }

    /// <summary>
    /// A damage trap's quality on a thing in flight that set it off (DamageTrap_ovr107_CB2 ->
    /// DamageObject with type 4 -> DamageObjectAndDoors_seg023_3E7 on a mobile object): word 0
    /// bit 13 protects, the resistances of COMOBJ byte 8 scale it, the quality class halves it,
    /// and it comes off byte 8 - the thrown item's quality, which it keeps when it comes to
    /// rest. At 0 it is gone, as after a hard impact (DamageSelf). A thing that came to rest in
    /// the same frame is not found and left alone.
    /// </summary>
    public void DamageThing(int piIndex, int piDamage)
    {
        UWProjectileFlight lOFlight = fFlight(piIndex);

        if (lOFlight == null || lOFlight.Record == null || piDamage <= 0)
            return;

        UWMobileRecord lORecord = lOFlight.Record;
        UWCommonObjectProperties.Entry lOEntry = fEntry(lORecord.ItemId);
        int liScaled = UWDamageTypes.Scale(lOEntry.Resistances, piDamage, UWDamageTypes.Physical);
        bool lbProtected = lORecord.Object != null && lORecord.Object.DoorDirection;
        int liNewHp;

        if (!UWObjectDamageRules.Wear(lORecord.Hp, liScaled, lOEntry.QualityClass, lbProtected, out liNewHp))
        {
            lORecord.Hp = liNewHp;

            return;
        }

        lORecord.Hp = 0;
        fEndFlight(lORecord);
    }

    private void fEndFlight(UWMobileRecord pORecord)
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

    private UWProjectileFlight fFlight(int piIndex)
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

        foreach (UWCritter lOCritter in UWCritter.Active)
        {
            if (lOCritter != null && lOCritter.Index == piIndex)
                return lOCritter;
        }

        return null;
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

    private int fStaticIndex(UWObject pOObject)
    {
        int liIndex;

        if (!mOStaticIndex.TryGetValue(pOObject, out liIndex))
        {
            liIndex = miNextStaticIndex++;
            mOStaticIndex[pOObject] = liIndex;
            mOStaticByIndex[liIndex] = pOObject;
        }

        return liIndex;
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

    public int Random()
    {
        return UWRandom.Next(0x8000);
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

        bool lbPlayerFlag = (lOTrigger.Flags & PlayerSetsOffFlag) != 0;
        bool lbOthersFlag = (lOTrigger.Flags & OthersSetOffFlag) != 0;

        if (piMoverIndex == 1)
        {
            if (lbPlayerFlag && !mOPendingPlayerTriggers.Contains(lOTrigger))
                mOPendingPlayerTriggers.Add(lOTrigger);

            return 2;
        }

        bool lbCreature = fCritter(piMoverIndex) != null;

        if (lbCreature ? !lbOthersFlag : (lbOthersFlag && !lbPlayerFlag))
            return 2;

        foreach (PendingTrigger lOPending in mOPendingOtherTriggers)
        {
            if (lOPending.Mover == piMoverIndex && lOPending.Trigger == lOTrigger)
                return 2;
        }

        mOPendingOtherTriggers.Add(new PendingTrigger { Mover = piMoverIndex, Trigger = lOTrigger, IsCreature = lbCreature });

        return 2;
    }

    /// <summary>Flags bit 2 of a trigger (word 0 bit 0x800): the player sets it off
    /// (Trigger_ovr153_3B label F5).</summary>
    public const int PlayerSetsOffFlag = 4;

    /// <summary>Flags bit 3 (word 0 bit 0x1000): creatures set it off (labels 145 to 16E); for a
    /// thing it is the one bit that, without bit 2, keeps it out (labels 174 to 1A7).</summary>
    public const int OthersSetOffFlag = 8;

    private struct PendingTrigger
    {
        public int Mover;

        public UWObject Trigger;

        public bool IsCreature;
    }

    private readonly List<PendingTrigger> mOPendingOtherTriggers = new List<PendingTrigger>();

    private readonly Dictionary<long, float> mOOtherTriggerFiredAt = new Dictionary<long, float>();

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
        float lfNow = Time.time;

        mOPendingOtherTriggers.Clear();

        if (mOOtherTriggerFiredAt.Count > 256)
            mOOtherTriggerFiredAt.Clear();

        foreach (PendingTrigger lOAt in lOPending)
        {
            if (mOLoader.CurrentLevel != lOLevel)
                break;

            long llKey = ((long)lOAt.Mover << 32) | (uint)lOAt.Trigger.GetHashCode();
            float lfLast;

            if (mOOtherTriggerFiredAt.TryGetValue(llKey, out lfLast) && lfNow - lfLast < PlayerTriggerSpacingSeconds)
                continue;

            UWCritter lOCritter = lOAt.IsCreature ? fCritter(lOAt.Mover) : null;

            if (lOAt.IsCreature && lOCritter == null)
                continue;

            mOOtherTriggerFiredAt[llKey] = lfNow;
            UWTriggerSystem.TryFireMoveTriggerBy(lOAt.Trigger, mOLoader, lOInteraction, lOCritter,
                lOAt.IsCreature ? 0 : lOAt.Mover);
        }
    }

    /// <summary>
    /// How often one trigger fires at most while the player moves in its reach: once per 16 PIT
    /// ticks, the original's frame at the user's DOSBox reference. The original fires per scan,
    /// so its rate follows its frame rate; ours runs the motion once per rendered frame in Smooth
    /// mode, and at 144 frames a second a damage trigger would hurt nine times as often.
    /// </summary>
    public const float PlayerTriggerSpacingSeconds = 16f / 256f;

    private readonly List<UWObject> mOPendingPlayerTriggers = new List<UWObject>();

    private readonly Dictionary<UWObject, float> mOPlayerTriggerFiredAt = new Dictionary<UWObject, float>();

    /// <summary>The move triggers the player's motion met since the last call (TriggerMove),
    /// each fired once - unless it fired less than PlayerTriggerSpacingSeconds ago. A level change
    /// by one of them drops the rest.</summary>
    public void FirePendingPlayerTriggers()
    {
        if (mOPendingPlayerTriggers.Count == 0 || mOLoader == null)
            return;

        UWObject[] lOTriggers = mOPendingPlayerTriggers.ToArray();
        UWLevel lOLevel = mOLoader.CurrentLevel;
        Interaction lOInteraction = UWScene.Interaction;
        float lfNow = Time.time;

        mOPendingPlayerTriggers.Clear();

        if (mOPlayerTriggerFiredAt.Count > 64)
            mOPlayerTriggerFiredAt.Clear();

        foreach (UWObject lOTrigger in lOTriggers)
        {
            if (mOLoader.CurrentLevel != lOLevel)
                break;

            float lfLast;

            if (mOPlayerTriggerFiredAt.TryGetValue(lOTrigger, out lfLast) && lfNow - lfLast < PlayerTriggerSpacingSeconds)
                continue;

            mOPlayerTriggerFiredAt[lOTrigger] = lfNow;
            UWTriggerSystem.TryFireMoveTrigger(lOTrigger, mOLoader, lOInteraction);
        }
    }

    public bool MissileHits(int piMoverIndex, int piOtherIndex, int piMoverTileX, int piMoverTileY)
    {
        UWProjectileFlight lOFlight = fFlight(piMoverIndex);

        if (lOFlight == null)
            return false;

        if (piOtherIndex == 1)
            return lOFlight.StrikePlayer();

        UWCritter lOCritter = fCritter(piOtherIndex);

        if (lOCritter != null)
            return lOFlight.StrikeCreature(lOCritter);

        UWObject lOObject;

        if (mOStaticByIndex.TryGetValue(piOtherIndex, out lOObject))
            return lOFlight.StrikeObject(lOObject);

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
            Index = miNextFlightIndex++,
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

        mOStaticByIndex.Remove(piOtherIndex);
        fSpawnFlight(lORecord, 0, UWDamageTypes.Physical, -1, null, null, false, false);
    }

    public void PlaySoundAt(int piSound, int piX8, int piY8, int piVolume)
    {
        Vector3 lOAt = UWViewpoint.OriginalToWorld((piX8 << 5) + 16, (piY8 << 5) + 16, 0);

        if (mOLoader != null)
            lOAt.y = mOLoader.GetFloorHeightAt(lOAt);

        UWSoundEffects.PlayAt(piSound, lOAt);
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
        UWCommonObjectProperties.Entry lOEntry = fEntry(pORecord.ItemId);
        int liScaled = UWDamageTypes.Scale(lOEntry.Resistances, piDamage, piType == 8 ? UWDamageTypes.PlainFire : UWDamageTypes.None);
        int liNewHp;

        if (!UWObjectDamageRules.Wear(pORecord.Hp, liScaled, lOEntry.QualityClass, false, out liNewHp))
        {
            pORecord.Hp = liNewHp;

            return false;
        }

        pORecord.Hp = 0;
        fEndFlight(pORecord);

        return true;
    }

    public void PlaySoundAtObject(int piSound, UWMobileRecord pORecord, int piVolume)
    {
        UWSoundEffects.PlayAt(piSound, WorldPosition(pORecord));
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
            WorldPosition(pORecord), 0f);
    }

    public void OnDemoted(UWMobileRecord pORecord)
    {
        if (mOLoader != null && mOLoader.UWDataImporter != null)
            UWObjectMechanics.Extinguish(pORecord.Object, mOLoader.UWDataImporter.Textures);
    }

    public int Detonate(UWMobileRecord pORecord, int piTileX, int piTileY, int piOwner)
    {
        UWProjectileFlight lOFlight = fFlight(pORecord.Index);

        if (lOFlight != null)
            lOFlight.Detonate(piTileX, piTileY, piOwner);

        return 0;
    }

    public void Remove(UWMobileRecord pORecord)
    {
        fEndFlight(pORecord);
    }

    public void PlaceAtRest(UWMobileRecord pORecord, int piOctant, int piQuality)
    {
        fEndFlight(pORecord);

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

        mOLoader.PlaceRestingObject(lOObject, pORecord.TileX, pORecord.TileY);
    }

    /// <summary>The blast of a detonating missile, its picture and its sound.</summary>
    public void ApplyBlast(UWProjectileFlight pOFlight, int piTileX, int piTileY, int piOwner)
    {
        if (mOLoader == null)
            return;

        int liCount;
        int liRange;
        int liType;

        if (UWObjectDamageRules.TryGetBlast(pOFlight.Record.ItemId, out liCount, out liRange, out liType))
        {
            int liAttacker = piOwner == 1 ? UWCritterBrain.PlayerIndex : piOwner > 1 ? piOwner : -1;

            UWTileBlast.Strike(mOLoader, new UWTilePos(piTileX, piTileY), liCount, liRange, liType, liAttacker,
                pOFlight.PlayersOwn ? pOFlight.Interaction : null);
        }

        ShowImpact(pOFlight, WorldPosition(pOFlight.Record));
    }

    /// <summary>The impact picture of a missile that has one, and the player's own impact sound
    /// (SpawnImpactAnimo_seg022_2D2 plays effect 7 only for the player's).</summary>
    public void ShowImpact(UWProjectileFlight pOFlight, Vector3 pOAt)
    {
        if (mOLoader == null || pOFlight.ImpactId < 0)
            return;

        mOLoader.SpawnEffectAt(pOFlight.ImpactId, pOAt + (Vector3.up * 2f), ImpactSeconds);

        if (pOFlight.PlayersOwn)
            UWSoundEffects.PlayAt(UWSoundEffects.SpellImpact, pOAt);
    }

    public UWLevelLoader Loader => mOLoader;
}
