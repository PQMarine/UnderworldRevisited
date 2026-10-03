using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;
using UnderworldRevisited;
using UnderworldRevisited.Build;

/// <summary>
/// Water and lava under the player's feet.
///
/// Which tile carries what is stored in TERRAIN.DAT (see UWLevelLoader.IsWaterTile and
/// IsLavaTile). What happens then, however, is not recorded anywhere in the data and comes
/// from the user's description (2026-09-01):
///
///   WATER: you swim. The view is an estimated 16 to 32 above the floor height of the
///   tile (since readjusted, see UWSettings.SwimViewHeight) and sways. The swimming skill makes you faster and reduces the
///   sway. Staying in the water too long without training causes damage - the user was
///   not sure about this last point, so it can be switched off in the
///   settings.
///
///   LAVA: you walk normally, only slower, and take damage.
///
/// Swimming is implemented via the camera height, not via the body: the
/// CharacterController cannot sink into the floor, so a lowered body would
/// push against the floor. The body therefore keeps standing on the ground, and only
/// the view slides to the height the user gave - visually it is the same.
///
/// Creatures are deliberately not affected here. They do not avoid lava and take no
/// damage there either, see UWCritter.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class UWPlayerTerrain : MonoBehaviour
{
    private UWLevelLoader mOLevelLoader;
    private CharacterController mOController;
    private UWPlayerMovement mOMovement;
    private UWCharacter mOPlayer;
    private Transform mOCamera;

    private float mfBaseCameraLocalY;
    private float mfAppliedCameraOffset;
    private float mfAppliedCameraRoll;

    private readonly UWTickClock mOSwimCheckTick = new UWTickClock(20f);
    private bool mbWasSwimming;
    private UWPlayerData mOSwimCounterSource;
    private UWInventory mOInventory;

    /// <summary>PLAYER.DAT 0xB9, see fUpdateSwimCounter. Public for the save game.</summary>
    public int SwimCounter { get; private set; }

    public const int SwimCounterOnEntering = 0x60;

    private const int SwimCounterCheckThreshold = 0x50;

    private const int SwimCounterRiseLimit = 0x8C;

    private const int SwimCounterDrownThreshold = 0x78;

    /// <summary>A counter unit is an eighth of a zpos step, a zpos step two world units.</summary>
    private const float SwimCounterWorldUnit = 0.25f;

    private const int DrownFlashColour = 0xC6;

    private const float DrownFlashSeconds = 0.1f;
    private float mfNextLavaDamageTime;

    public bool IsSwimming { get; private set; }

    public bool IsOnLava { get; private set; }

    public void Init(UWLevelLoader pOLevelLoader, Transform pOCamera)
    {
        mOLevelLoader = pOLevelLoader;
        mOCamera = pOCamera;

        if (mOCamera != null)
            mfBaseCameraLocalY = mOCamera.localPosition.y;

        mOPlayer = mOCamera != null ? mOCamera.GetComponent<UWCharacter>() : null;
    }

    private void Awake()
    {
        mOController = GetComponent<CharacterController>();
        mOMovement = GetComponent<UWPlayerMovement>();
    }

    private void Update()
    {
        UWSettings lOSettings = UWSettings.Instance;

        UWTile lOTile;
        float lfTileFloor = 0f;

        bool lbInLiquid = fTryGetLiquidTile(out lOTile, out lfTileFloor);

        // Water walking keeps you on top: the reference then does not let swimming
        // start at all. Lava is not affected by this.
        bool lbWaterWalk = mOMovement != null
            && (mOMovement.MagicalMotionAbilities & UWCharacter.WaterWalkBit) != 0;

        IsSwimming = lbInLiquid && !lbWaterWalk && mOLevelLoader.IsWaterTile(lOTile);
        IsOnLava = lbInLiquid && mOLevelLoader.IsLavaTile(lOTile);

        if (mOMovement != null)
        {
            mOMovement.IsInLiquid = lbInLiquid;
            mOMovement.IsOnLava = IsOnLava;
        }

        // NO SOUND ON ENTERING. Until 2026-09-21 the water sound was played here; the user
        // listened for it in the original and there is none - walking into water is silent.
        // The sound belongs to what falls IN: the player's own jump (UWPlayerMovement.fLand)
        // and anything that lands in the water (UWSpellProjectile, UWLevelLoader.fSplashAt).
        // What one DOES hear in the water is the rushing: effect 0 held for as long as one is
        // in it (UWSoundEffects.UpdateWaterLoop, built 2026-09-21 after the user described it).
        UWSoundEffects.UpdateWaterLoop(IsSwimming);

        fUpdateHeightAboveFloor();

        fUpdateSwimCounter(lOSettings);
        fUpdateSpeed(lOSettings);
        fUpdateCamera(lOSettings, lfTileFloor);
        fUpdateFlow(lOSettings, lOTile);
        fUpdateDamage(lOSettings);
        fUpdateAutomap();
    }

    /// <summary>
    /// How high the feet are above the tile floor.
    ///
    /// Needed by levitation (see UWPlayerMovement.fIsAirborne): the CharacterController's
    /// ground contact keeps dropping out briefly on stairs and slopes, the distance
    /// to the tile floor does not. The floor height comes from the tile data and is
    /// correctly interpolated on a slope (UWLevelLoader.GetFloorHeightAt).
    ///
    /// Lives here because this class already works with the level loader and tile every frame.
    /// </summary>
    /// <summary>How far below the feet the next walkable surface lies. The tile floor is the
    /// fallback - what counts is what one actually stands on: a bridge over water lies well
    /// above the water tile's floor, and measured against the tile floor the Ring of Levitation
    /// switched the hover on in the middle of the bridge (isGrounded drops out for single frames
    /// while walking) - at bridge height, without a visible lift, only the speed fell to the
    /// levitation factor until a swim reset it (per user on level 2, 2026-09-18).</summary>
    private void fUpdateHeightAboveFloor()
    {
        if (mOMovement == null || mOLevelLoader == null || mOLevelLoader.CurrentLevel == null)
            return;

        float lfFloor = mOLevelLoader.GetFloorHeightAt(transform.position);
        Bounds lOBounds = mOController.bounds;
        RaycastHit lOHit;

        // From inside the capsule downwards: the own collider is not reported by a ray that
        // starts inside it. Only as far as the hover decisions look (see UWPlayerMovement).
        if (Physics.Raycast(lOBounds.center, Vector3.down, out lOHit,
            lOBounds.extents.y + GroundProbeDepth, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && lOHit.point.y > lfFloor)
            lfFloor = lOHit.point.y;

        mOMovement.HeightAboveFloor = lOBounds.min.y - lfFloor;
    }

    /// <summary>How far below the feet the ground probe looks - more than the walking
    /// clearance of the hover (one and a half steps), so that a step down still reads as floor.</summary>
    private const float GroundProbeDepth = UWTile.HeightLevel * 3f;

    /// <summary>The tile entered last. As long as you stay on it, there is nothing to
    /// do.</summary>
    private UWTilePos mOLastAutomapTile = new UWTilePos(-1, -1);

    /// <summary>Object number of the bridge - it has its own symbol on the map.</summary>
    private const int BridgeObjectId = UWObjectMechanics.BridgeObjectId;

    /// <summary>
    /// Enters the tile stepped on into the automap.
    ///
    /// Lives here because this class already computes every frame which tile the
    /// player stands on - a separate component for it would be a second path to the same number.
    /// </summary>
    private void fUpdateAutomap()
    {
        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null)
            return;

        // In the Void the map stays empty (per user in the original, 2026-09-14). UW.EXE counts
        // no exploration experience there either (seg017_1FDD_C5 checks DungeonLevel 9).
        if (mOLevelLoader.CurrentLevelIndex == UWEndgame.VoidLevelIndex)
            return;

        UWTilePos lOTileIndex = mOLevelLoader.WorldPositionToTile(transform.position);

        if (lOTileIndex.X < 0 || lOTileIndex.Y < 0
            || lOTileIndex.X >= UWLevelMeshBuilder.TilesPerAxis
            || lOTileIndex.Y >= UWLevelMeshBuilder.TilesPerAxis)
            return;

        if (lOTileIndex != mOLastAutomapTile)
        {
            mOLastAutomapTile = lOTileIndex;

            // THE TILE ONE STANDS ON, and nothing more. Until 2026-09-23 its eight neighbours
            // were discovered here as well, light or not; the original does not do that - in
            // the dark it reveals only the tiles walked on, and what a light adds comes out of
            // the render band (UWExplorationRules.EvaluateSweep, per user with four runs
            // against the original). The band holds the own tile too; this stays for the
            // moment a level is entered, before the band has run.
            fMarkVisited(lOTileIndex.X, lOTileIndex.Y);
        }

        fUpdateRenderBand();
    }

    /// <summary>The level's tiles changed (a lever, a change terrain trap): the band runs again
    /// on the next frame even where the player stands still, as the original's does every frame,
    /// so the map takes the new state of what is in view.</summary>
    public void InvalidateRenderBand()
    {
        miLastBandX = -1;
    }

    /// <summary>The original's sweep, kept between frames for its buffers.</summary>
    private readonly UWRenderSweep mOSweep = new UWRenderSweep();

    private int miLastBandX = -1;

    private int miLastBandY = -1;

    private int miLastBandHeading = -1;

    private int miLastBandLight = -1;

    private UWLighting mOLighting;

    /// <summary>
    /// The tile band the original renderer passes over every frame, and the exploration
    /// experience from it - the rules live in UWExplorationRules and UWRenderSweep. Here: the
    /// player's position in the original's units, the view's heading in its 16 bits, the light
    /// level, and the memo that skips a sweep for the same position, heading and light - the
    /// original sweeps every frame, but a repeated sweep changes nothing.
    /// </summary>
    private void fUpdateRenderBand()
    {
        if (mOLevelLoader.UWDataImporter == null || !mOLevelLoader.HasWorld)
            return;

        // The position is the player's (PositionCameraAtObject_seg034_2F89_B99 takes the player
        // object), the heading the view's.
        Transform lOView = mOCamera != null ? mOCamera : transform;
        int liX = UWUnits.WorldAxisToOriginal(transform.position.x);
        int liY = UWUnits.WorldAxisToOriginal(transform.position.z);
        int liHeading = UWUnits.DegreesToAngle(lOView.eulerAngles.y) & 0xFFFF;
        int liLight = fGetLightLevel();

        // The light is part of the memo: lighting a torch on the spot discovers the ring
        // around, as in the original.
        if (liX == miLastBandX && liY == miLastBandY && liHeading == miLastBandHeading && liLight == miLastBandLight)
            return;

        miLastBandX = liX;
        miLastBandY = liY;
        miLastBandHeading = liHeading;
        miLastBandLight = liLight;

        UWShades lOShades = mOLevelLoader.UWDataImporter.Shades;

        // The table WITH the doubled distance - the one seg031_4AB writes into the renderer's
        // grid, see UWExplorationRules.EvaluateSweep.
        byte[] lyShadeTable = lOShades != null && liLight >= 0 ? lOShades.GetShadeTable(liLight, true) : null;

        UWLevel lOLevel = mOLevelLoader.CurrentLevel;

        mOSweep.Run(lOLevel, liX, liY, liHeading, lyShadeTable);

        int liCounter = UWExplorationRules.EvaluateSweep(lOLevel, mOSweep, fGetMapDisplayTypeAt);

        fAwardExplorationExperience(liCounter);
    }

    /// <summary>The viewing distance of the current light level in tile rows: how deep the
    /// render band goes, and with it how far one can see at all - what is not drawn cannot be
    /// looked at either (see Interaction.fIsWithinSight).</summary>
    public int ViewingDistance
    {
        get { return fGetViewingDistance(); }
    }

    /// <summary>The viewing distance of the current light level (SHADES.DAT), the most rows
    /// the band reaches; without a lighting component the full depth.</summary>
    private int fGetViewingDistance()
    {
        if (mOLighting == null)
            mOLighting = UWScene.Lighting;

        UWShades.Entry lOShade;

        if (mOLighting != null && mOLevelLoader.UWDataImporter.Shades != null
            && mOLevelLoader.UWDataImporter.Shades.TryGet(mOLighting.LightLevel, out lOShade))
            return lOShade.ViewingDistance;

        return UWExplorationRules.RenderBandMaxDepth;
    }

    /// <summary>The light level the band's shade table is built for; -1 without a lighting
    /// component, and then every visible cell counts as bright.</summary>
    private int fGetLightLevel()
    {
        if (mOLighting == null)
            mOLighting = UWScene.Lighting;

        return mOLighting != null ? mOLighting.LightLevel : -1;
    }

    /// <summary>Awards the exploration experience of one evaluation - see UWExplorationRules.</summary>
    private void fAwardExplorationExperience(int piCounter)
    {
        if (mOPlayer == null)
            return;

        int liDungeonLevel = mOLevelLoader.CurrentLevelIndex + 1;
        int liExperience = UWExplorationRules.GetExperience(piCounter, liDungeonLevel, mOPlayer.Level);

        if (liExperience > 0)
            mOPlayer.ChangeExperience(liExperience, liDungeonLevel);
    }

    private bool fIsOpenTile(int piTileX, int piTileY)
    {
        UWTile lOTile = mOLevelLoader.CurrentLevel.GetTile(piTileX, piTileY);
        return lOTile != null && lOTile.TileType != UWTile.TileTypeEnum.solid;
    }

    /// <summary>Marks a tile as discovered; true if it was not discovered before.</summary>
    private bool fMarkVisited(int piTileX, int piTileY)
    {
        if (!fIsOpenTile(piTileX, piTileY))
            return false;

        UWTile lOTile = mOLevelLoader.CurrentLevel.TileData[(piTileY * UWLevelMeshBuilder.TilesPerAxis) + piTileX];

        return mOLevelLoader.CurrentLevel.MarkTileVisited(piTileX, piTileY, fGetMapDisplayType(lOTile));
    }

    /// <summary>The same by tile position - what the band hands to
    /// UWExplorationRules.EvaluateSweep for the tiles it discovers.</summary>
    private int fGetMapDisplayTypeAt(int piTileX, int piTileY)
    {
        UWLevel lOLevel = mOLevelLoader != null ? mOLevelLoader.CurrentLevel : null;

        return fGetMapDisplayType(lOLevel != null ? lOLevel.GetTile(piTileX, piTileY) : null);
    }

    /// <summary>How the tile is drawn on the map: the terrain of its floor (water, lava) and the
    /// marker of what lies on it (bridge, door, stair) together, as the automap byte holds them -
    /// seg017_1FDD_DBC takes the terrain from the floor texture and ORs in the marker the object
    /// renderer set. Until 2026-10-01 one of them won (water before a door), and a bridge was
    /// always written over water.</summary>
    private int fGetMapDisplayType(UWTile pOTile)
    {
        if (pOTile == null)
            return UWLevel.MapDisplayClear;

        int liTerrain = mOLevelLoader.IsWaterTile(pOTile) ? UWLevel.MapDisplayWater
            : mOLevelLoader.IsLavaTile(pOTile) ? UWLevel.MapDisplayLava
            : UWLevel.MapDisplayClear;

        int liMarker = fHasBridge(pOTile) ? UWLevel.MapDisplayBridgeMarker
            : pOTile.HasDoor ? UWLevel.MapDisplayDoor
            : fHasStair(pOTile) ? UWLevel.MapDisplayStair
            : UWLevel.MapDisplayClear;

        return liMarker | liTerrain;
    }

    /// <summary>
    /// A staircase is a move trigger that points to a teleport trap with a height value
    /// - then it leads to another level. Without a height the teleport stays
    /// within the level and is not a staircase.
    ///
    /// UWLevelLoader already evaluates the same chain for the level change.
    /// </summary>
    private bool fHasStair(UWTile pOTile)
    {
        if (pOTile.ObjectsInTile == null || mOLevelLoader.CurrentLevel == null)
            return false;

        System.Collections.Generic.List<UWObject> lOMaster = mOLevelLoader.CurrentLevel.Masterlist;

        if (lOMaster == null)
            return false;

        foreach (UWObject lOTrigger in pOTile.ObjectsInTile)
        {
            if (lOTrigger == null || lOTrigger.ID != UWLevelLoader.MoveTriggerId)
                continue;

            int liLink = lOTrigger.Quantity;

            if (liLink <= 0 || liLink >= lOMaster.Count)
                continue;

            UWObject lOTrap = lOMaster[liLink];

            if (lOTrap == null || lOTrap.ID != UWLevelLoader.TeleportTrapId || lOTrap.ZPos == 0)
                continue;

            return true;
        }

        return false;
    }

    private static bool fHasBridge(UWTile pOTile)
    {
        if (pOTile.ObjectsInTile == null)
            return false;

        // ONLY A VISIBLE BRIDGE WITH A BRIDGE TEXTURE (per user, 2026-09-29: the plate over the Wine
        // of Compassion - a bridge with flags 2, textured like the floor - is plain floor on the
        // map). A texture value of 2 and more takes a floor texture (UWLevel.load_object_list), so
        // the plate looks like floor and the map draws it so. The reference (visionlos.cs, the
        // renderer's automap marks) asks for an invisible bit clear and a value up to 2; 2 is taken
        // as floor here, after the user's account of the original.
        foreach (UWObject lOObject in pOTile.ObjectsInTile)
        {
            if (lOObject != null && lOObject.ID == BridgeObjectId && !lOObject.IsHidden
                && lOObject.Flags < BridgeFloorTextureFlags)
                return true;
        }

        return false;
    }

    /// <summary>From this texture value on a bridge carries a floor texture (see fHasBridge).</summary>
    private const int BridgeFloorTextureFlags = 2;

    /// <summary>
    /// Is the player really IN the liquid, or only above it?
    ///
    /// A bridge can lie over a water tile - then you walk across it
    /// dry. This is decided at the feet: only someone down at tile floor height
    /// stands in the water.
    /// </summary>
    private bool fTryGetLiquidTile(out UWTile pOTile, out float pfTileFloor)
    {
        pOTile = null;
        pfTileFloor = 0f;

        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null)
            return false;

        UWTilePos lOTileIndex = mOLevelLoader.WorldPositionToTile(transform.position);

        if (lOTileIndex.X < 0 || lOTileIndex.Y < 0
            || lOTileIndex.X >= UWLevelMeshBuilder.TilesPerAxis || lOTileIndex.Y >= UWLevelMeshBuilder.TilesPerAxis)
            return false;

        pOTile = mOLevelLoader.CurrentLevel.TileData[(lOTileIndex.Y * UWLevelMeshBuilder.TilesPerAxis) + lOTileIndex.X];

        if (pOTile == null)
            return false;

        pfTileFloor = pOTile.FloorHeight;

        if (!mOLevelLoader.IsDestructiveTile(pOTile))
            return false;

        UWSettings lOSettings = UWSettings.Instance;
        float lfTolerance = lOSettings != null ? lOSettings.LiquidEntryTolerance : 8f;

        return mOController.bounds.min.y <= pfTileFloor + lfTolerance;
    }

    private void fUpdateSpeed(UWSettings pOSettings)
    {
        if (mOMovement == null)
            return;

        if (IsSwimming)
        {
            float lfSlow = pOSettings != null ? pOSettings.SwimSpeedUnskilled : 0.35f;
            float lfFast = pOSettings != null ? pOSettings.SwimSpeedSkilled : 0.8f;

            mOMovement.SpeedMultiplier = Mathf.Lerp(lfSlow, lfFast, fGetSwimSkillFraction(pOSettings));

            return;
        }

        mOMovement.SpeedMultiplier = IsOnLava
            ? (pOSettings != null ? pOSettings.LavaSpeedFactor : 0.5f)
            : 1f;
    }

    /// <summary>
    /// While swimming the view sinks to the given height above the tile floor and
    /// sways around it. The calculation uses the camera's world position, so it does not
    /// matter how deep the camera hangs below the player object.
    /// </summary>
    private void fUpdateCamera(UWSettings pOSettings, float pfTileFloor)
    {
        if (mOCamera == null)
            return;

        float lfWanted = 0f;

        if (IsSwimming)
        {
            float lfHeight = pOSettings != null ? pOSettings.SwimViewHeight : 17f;

            // SwimViewHeight is the view at the counter of 0x60 set on entering; every failed
            // swimming check lowers it further (see fUpdateSwimCounter).
            lfHeight -= (SwimCounter - SwimCounterOnEntering) * SwimCounterWorldUnit;
            float lfRough = pOSettings != null ? pOSettings.SwimSwayUnskilled : 4f;
            float lfSteady = pOSettings != null ? pOSettings.SwimSwaySkilled : 1f;
            float lfFrequency = pOSettings != null ? pOSettings.SwimSwayFrequency : 1.1f;

            float lfSway = Mathf.Sin(Time.time * lfFrequency * Mathf.PI * 2f)
                * Mathf.Lerp(lfRough, lfSteady, fGetSwimSkillFraction(pOSettings));

            // The rigid distance between player object and camera, without our own offset.
            float lfRigidOffset = mOCamera.position.y - transform.position.y - mfAppliedCameraOffset;

            lfWanted = pfTileFloor + lfHeight + lfSway - (transform.position.y + lfRigidOffset);
        }

        float lfSpeed = pOSettings != null ? pOSettings.SwimViewFollowSpeed : 60f;

        mfAppliedCameraOffset = Mathf.MoveTowards(mfAppliedCameraOffset, lfWanted, lfSpeed * Time.deltaTime);

        Vector3 lOLocal = mOCamera.localPosition;
        lOLocal.y = mfBaseCameraLocalY + mfAppliedCameraOffset + fGetHeadBob();

        mOCamera.localPosition = lOLocal;
    }

    private float mfHeadBobPhase;

    /// <summary>
    /// THE HEAD BOB, in world units (UWHeadBobRules): the phase runs on real time like the
    /// original's PIT counter, the offset comes in eighths of a zpos step, the same unit as the
    /// swim counter. Not in water or in the air. The menu bar's three-way switch
    /// (UWUserSettings.HeadBobMode, per user): Off - "there are enough people who cannot take
    /// it" -, Original with its steps, or Smooth, our curve through the same table.
    /// </summary>
    private float fGetHeadBob()
    {
        mfHeadBobPhase = UWHeadBobRules.AdvancePhase(mfHeadBobPhase, Time.deltaTime);

        UWHeadBobRules.ModeEnum leMode = UWUserSettings.HeadBobMode;

        if (leMode == UWHeadBobRules.ModeEnum.Off || mOMovement == null || IsSwimming || !mOMovement.IsWalkingOnGround)
            return 0f;

        float lfEighths = leMode == UWHeadBobRules.ModeEnum.Smooth
            ? UWHeadBobRules.GetSmoothOffset(mfHeadBobPhase, mOMovement.SmoothedHorizontalSpeed, mOMovement.FullForwardSpeed)
                * UWUserSettings.HeadBobStrength
            : UWHeadBobRules.GetOffset(mfHeadBobPhase, mOMovement.SmoothedHorizontalSpeed, mOMovement.FullForwardSpeed);

        return lfEighths * SwimCounterWorldUnit;
    }

    /// <summary>
    /// The sideways tilt. It sits in LateUpdate because UWPlayerLook sets the camera rotation
    /// completely anew in Update - a roll angle set earlier would be gone again
    /// every frame. The angle is set absolutely, not added up, so that nothing
    /// builds up.
    /// </summary>
    private void LateUpdate()
    {
        if (mOCamera == null)
            return;

        UWSettings lOSettings = UWSettings.Instance;

        float lfWanted = 0f;

        if (IsSwimming)
        {
            float lfRough = lOSettings != null ? lOSettings.SwimRollUnskilled : 1.5f;
            float lfSteady = lOSettings != null ? lOSettings.SwimRollSkilled : 0.3f;
            float lfFrequency = lOSettings != null ? lOSettings.SwimRollFrequency : 2.5f;

            lfWanted = Mathf.Sin(Time.time * lfFrequency * Mathf.PI * 2f)
                * Mathf.Lerp(lfRough, lfSteady, fGetSwimSkillFraction(lOSettings));
        }

        mfAppliedCameraRoll = Mathf.MoveTowards(mfAppliedCameraRoll, lfWanted, 30f * Time.deltaTime);

        Vector3 lOAngles = mOCamera.localEulerAngles;
        lOAngles.z = mfAppliedCameraRoll;

        mOCamera.localEulerAngles = lOAngles;
    }

    /// <summary>
    /// Current.
    ///
    /// The user observes in the original that at one spot you swim noticeably faster
    /// west than in other directions (level 1, tile 13/26). The tile data
    /// shows nothing there - the tile is flat, without slope, without an object, and its four
    /// neighbours likewise. TERRAIN.DAT does not help at first either: the file does know
    /// current directions (0x48, 0x50, 0x58, 0x60 - uw-formats.txt misreads these values
    /// as swamp and ice), but none of them occurs in UW1.
    ///
    /// The solution lies in the original's movement routine: it does not read the
    /// whole terrain value, only its bits 3 to 5, and turns them into one of four
    /// slide directions. Ordinary water has the value 0x10, so (0x10 & 0x38) >> 3 = 2,
    /// and the branch runs as soon as the swim bit is set.
    ///
    /// From this follows something easily overlooked: EVERY water in UW1 flows, and
    /// everywhere in the same direction - all water tiles of the game carry the same value.
    /// The user's spot is therefore nothing special, it is merely the one where it
    /// was noticed.
    ///
    /// It acts as a FACTOR on your own movement, not as a push: whoever stands still does
    /// not drift (checked by user in the original, 2026-09-01). This is computed in
    /// UWPlayerMovement, where the movement direction is available.
    ///
    /// Which direction it is, the data do not say unambiguously. The routine knows only four
    /// axes, but the user observed in the middle part of level 1 that it goes faster to the west
    /// AND to the south (2026-09-01) - that is a diagonal, not an axis.
    /// So the direction is stored here as a free angle: 225 degrees, south-west.
    ///
    /// RESOLVED, and against us (2026-09-03): the same south-west drift occurs in the
    /// original while LEVITATING too and, as the user checked afterwards, even when
    /// just walking on dry ground. So it has nothing to do with water - we
    /// attributed a current that does not exist to a bug in the original. That is exactly why
    /// the diagonal could not be explained.
    ///
    /// The strength is therefore set to zero, which switches the whole thing off. The mechanism
    /// stays in place in case real per-tile currents turn up after all; we do NOT
    /// replicate the original's bug.
    /// </summary>
    private void fUpdateFlow(UWSettings pOSettings, UWTile pOTile)
    {
        if (mOMovement == null)
            return;

        float lfStrength = pOSettings != null ? pOSettings.WaterFlowStrength : 0.5f;

        if (!IsSwimming || lfStrength <= 0f)
        {
            mOMovement.FlowStrength = 0f;
            mOMovement.FlowDirection = Vector3.zero;

            return;
        }

        float lfHeading = pOSettings != null ? pOSettings.WaterFlowHeadingDegrees : 225f;

        // 0 degrees points to +Z, clockwise like a compass.
        float lfRadians = lfHeading * Mathf.Deg2Rad;

        mOMovement.FlowDirection = new Vector3(Mathf.Sin(lfRadians), 0f, Mathf.Cos(lfRadians));
        mOMovement.FlowStrength = lfStrength;
    }

    private void fUpdateDamage(UWSettings pOSettings)
    {
        if (mOPlayer == null)
            return;

        // THE DRAGON SKIN BOOTS KEEP OUT THE LAVA - and only the lava. The reference checks
        // their flag right before the lava damage (motion_player) and nowhere else;
        // a fireball still hits. Consistent with that, the boots themselves carry the
        // fire resistance in comobj.dat.
        if (IsOnLava && !mOPlayer.HasDragonSkinBoots)
        {
            float lfInterval = pOSettings != null ? pOSettings.LavaDamageInterval : 0.5f;
            int liDamage = pOSettings != null ? pOSettings.LavaDamage : 4;

            float lfChance = pOSettings != null ? pOSettings.LavaDamageChance : 0.2f;

            lfInterval = Mathf.Max(0.005f, lfInterval);

            // Several rolls per rendered frame are possible: 1/32 s is shorter than a frame at
            // low frame rates. After a pause on the lava the backlog is dropped.
            if (mfNextLavaDamageTime < Time.time - 0.25f)
                mfNextLavaDamageTime = Time.time;

            while (Time.time >= mfNextLavaDamageTime)
            {
                mfNextLavaDamageTime += lfInterval;

                // In the original a roll is made per game frame (see LavaDamageInterval) and only in one of
                // five a single point is subtracted. So lava hurts, but does not kill
                // at the first step.
                // Lava burns without being magical - the reference gives damage type 0x08 here
                // (motion_player). Whoever has Flameproof active stands in it unharmed.
                if (UWRandom.NextDouble() < lfChance)
                    mOPlayer.ApplyDamage(liDamage, UWDamageTypes.PlainFire);
            }
        }
        else
        {
            mfNextLavaDamageTime = 0f;
        }

    }

    /// <summary>
    /// THE SWIM COUNTER of the original, PLAYER.DAT 0xB9 (per user, 2026-09-17: "the view really
    /// sinks", checked in the original). Replaces the guessed exhaustion (fixed time, then damage
    /// every two seconds) that stood here before.
    ///
    /// UW.EXE:
    ///   - Entering water sets it to 0x60 (SetPlayerDataOxB9_seg008_B), leaving it to 0.
    ///   - The camera sits 0xA4 above the body minus the counter (seg034_2F89_971), so it is
    ///     the counter that lowers the view in the water at all. One unit is an eighth of a zpos
    ///     step, a quarter of our world unit.
    ///   - In the periodic player update (seg028_2985_24C), while the counter is above 0x50,
    ///     SwimSkillCheck_seg028_4AE runs: swimming skill against (carried weight * 32) / carrying
    ///     capacity. On a failure it rises by (3 - result) four-sided dice - three on a failure,
    ///     four on a critical one - but only while it is still below 0x8C.
    ///   - Above 0x78 a second check follows; everything but a critical success costs
    ///     (2 - result) + 2 sided dice twice in hit points with a flash in palette colour 0xC6 (blue in game).
    /// So the view sinks with every failed check, and a heavily loaded, unpractised swimmer
    /// drowns sooner. How often the periodic update runs is not read out of the clock code;
    /// it runs on the player tick, because the reference has this check in exactly that
    /// periodic loop (UWPlayerTick; the interval stays a setting, SwimCheckInterval).
    /// </summary>
    private void fUpdateSwimCounter(UWSettings pOSettings)
    {
        fLoadSwimCounter();

        if (IsSwimming != mbWasSwimming)
        {
            SwimCounter = IsSwimming ? SwimCounterOnEntering : 0;
            mbWasSwimming = IsSwimming;
        }

        float lfInterval = pOSettings != null ? pOSettings.SwimCheckInterval : UWPlayerTick.Seconds;

        // Elapsed time in, whole checks out (UWTickClock); the interval is a setting.
        mOSwimCheckTick.IntervalSeconds = lfInterval;

        int liChecks = mOSwimCheckTick.Advance(Time.deltaTime);

        for (int liCheck = 0; liCheck < liChecks; liCheck++)
        {
            if (SwimCounter > SwimCounterCheckThreshold)
                fSwimSkillCheck();
        }
    }

    private void fSwimSkillCheck()
    {
        int liTarget = fGetLoadForSwimming();
        int liSkill = fGetSwimSkill();

        UWSkillCheck.ResultEnum leResult = UWSkillCheck.Check(liSkill, liTarget);

        if (!UWSkillCheck.IsSuccess(leResult) && SwimCounter < SwimCounterRiseLimit)
            SwimCounter = (SwimCounter + UWRandom.RollDice(3 - (int)leResult, 4)) & 0xFF;

        if (SwimCounter <= SwimCounterDrownThreshold || mOPlayer == null)
            return;

        int liSeverity = 2 - (int)UWSkillCheck.Check(liSkill, liTarget);

        if (liSeverity == 0)
            return;

        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi != null)
            lOUi.FlashWindowColour(DrownFlashColour, DrownFlashSeconds);

        mOPlayer.ApplyDamage(UWRandom.RollDice(2, liSeverity + 2));
    }

    /// <summary>(carried weight * 32) / carrying capacity, both in tenth-stones as the display
    /// computes them (UWGameUI.fRefreshWeight).</summary>
    private int fGetLoadForSwimming()
    {
        DataImport lOData = mOLevelLoader != null ? mOLevelLoader.UWDataImporter : null;
        UWPlayerData lOPlayer = lOData != null ? lOData.InitialPlayer : null;

        if (lOPlayer == null || lOPlayer.MaxWeight == 0)
            return 0;

        if (mOInventory == null)
            mOInventory = UWScene.Inventory;

        int liCarried = mOInventory != null ? mOInventory.GetCarriedTenthStones(lOData.CommonObjectProperties) : 0;

        return (Mathf.Max(0, liCarried) << 5) / lOPlayer.MaxWeight;
    }

    /// <summary>Takes the counter from a loaded save game once, so a character saved while
    /// swimming keeps its sunken view and does not start over at 0x60.</summary>
    private void fLoadSwimCounter()
    {
        DataImport lOData = mOLevelLoader != null ? mOLevelLoader.UWDataImporter : null;
        UWPlayerData lOPlayer = lOData != null ? lOData.InitialPlayer : null;

        if (lOPlayer == mOSwimCounterSource)
            return;

        mOSwimCounterSource = lOPlayer;
        SwimCounter = lOPlayer != null && lOPlayer.IsLoaded ? lOPlayer.SwimCounter : 0;
        mbWasSwimming = SwimCounter > 0;
    }

    /// <summary>N plus N rolls from zero to the range minus one, as DiceRoll in UW.EXE.</summary>
    private int fGetSwimSkill()
    {
        return mOPlayer != null ? mOPlayer.GetSkill(UWPlayerData.Skill.Swimming) : 0;
    }

    private float fGetSwimSkillFraction(UWSettings pOSettings)
    {
        float lfFull = pOSettings != null ? pOSettings.SwimSkillForFullEffect : 15f;

        return lfFull <= 0f ? 1f : Mathf.Clamp01(fGetSwimSkill() / lfFull);
    }
}
