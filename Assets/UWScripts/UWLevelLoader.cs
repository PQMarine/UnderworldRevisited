using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.Install;
using UWDataImport.UWData;
using UnderworldRevisited;
using UnderworldRevisited.Build;

/// <summary>
/// Builds a level, keeps the loaded game data ready for the other components
/// and switches between levels.
///
/// Replaces the earlier construction, which created the complete architecture face by face
/// as individual GameObjects. The static geometry now either comes from a
/// baked prefab or is computed as chunk meshes; only the movable things
/// are still created individually.
/// </summary>
public class UWLevelLoader : MonoBehaviour
{
    /// <summary>a_teleport trap. Carries the target level in ZPos and the target tile in Quality/Owner.</summary>
    public const int TeleportTrapId = UWTrapRules.TeleportTrapId;

    /// <summary>The move trigger, object 6-2-0. It is also the first of the
    /// triggers - a staircase carries exactly this one, linked to a teleport trap.
    /// </summary>
    public const int MoveTriggerId = UWObjectMechanics.MoveTriggerId;

    /// <summary>Lockout after a transition, so that a way back does not fire again immediately.</summary>
    private const float TransitionCooldown = 0.75f;

    [Header("Level")]
    [SerializeField]
    [Tooltip("Level 1 to 9 of the Abyss, zero-based.")]
    private int miLevelIndex = 0;

    // In the original the a_create object trap rolls whether it creates anything at all (see
    // UWTriggerSystem). That is annoying for testing - four of the five generators on level 1
    // only hit in 20 to 50 percent of cases.
    [SerializeField]
    [Tooltip("Create object traps (a_create object trap) ALWAYS create, instead of rolling as in the original. Testing only.")]
    private bool mbAlwaysFireCreateObjectTraps;

    /// <summary>See mbAlwaysFireCreateObjectTraps - test switch, the original
    /// rolls.</summary>
    public bool AlwaysFireCreateObjectTraps
    {
        get { return mbAlwaysFireCreateObjectTraps; }
    }

    [SerializeField]
    [Tooltip("Start position of the player on the first load.")]
    private Vector3 mOPlayerStart = new Vector3(2020f, 230f, 128f);

    // The height of the start point no longer counts - the character is placed on the floor
    // (see fPlacePlayerOnFloor). The fixed offset mfPlayerHeightOffset is gone as well.

    [SerializeField]
    [Tooltip("Baked object atlas. Leave empty and it is built at start.")]
    private UWObjectAtlas mOObjectAtlas;

    private Material mOModelSolidMaterial;

    private Material mOPortcullisMaterial;

    /// <summary>The material of the portcullis - like that of the model faces, only with
    /// inverted culling (see fCreateObjectMaterials).</summary>
    public Material PortcullisMaterial
    {
        get { return mOPortcullisMaterial; }
    }

    /// <summary>The material of the solid-colour model faces. The palette renderer recognises
    /// them by it - they carry their index at the vertex and need their own shader
    /// (see UWPaletteRenderPath).</summary>
    public Material ModelSolidMaterial
    {
        get { return mOModelSolidMaterial; }
    }

    /// <summary>The object atlas, so that the palette renderer can get at its index
    /// version (see UWPaletteRenderPath).</summary>
    public UWObjectAtlas ObjectAtlas
    {
        get { return mOObjectAtlas; }
    }

    /// <summary>Like mOBillboardMaterial, but with alpha blending - for sprites with
    /// translucent pixels (see UWTransparencyTables).</summary>
    private Material mOTranslucentBillboardMaterial;

    [SerializeField]
    [Tooltip("Material for the dungeon geometry. Leave empty and it is created at start.")]
    private Material mODungeonMaterial;

    /// <summary>The material of floor, ceiling and walls - for UWRemasterRenderPath, which
    /// uses it to switch the relief on and off.</summary>
    public Material DungeonMaterial
    {
        get { return mODungeonMaterial; }
    }

    private Material mOBillboardMaterial;
    private Material mODecalMaterial;
    private Texture2DArray mOTextureArray;

    /// <summary>3D models interpreted from uw.exe (shrine/ankh, ...), see
    /// UW3DModelImport. Stays null if no .exe is found.</summary>
    private UW3DModelImport mO3DModels;

    private GameObject mOLevelRoot;
    private GameObject mOObjectRoot;
    private float mfTransitionBlockedUntil;

    /// <summary>The pictures of the objects and the registry from data to picture (TryGetEntity);
    /// one per level build, attached to WorldEdit - see UWWorldView.</summary>
    private UWWorldView mOWorldView;

    private UWWorldEdit mOWorldEdit;

    /// <summary>
    /// The one place where the current level's object lists change - placing, removing, moving,
    /// culling, Armageddon (UWWorldEdit, engine-free since 2026-10-12). Rebuilt with TileQueries
    /// when the level or the data change; the view of the level build follows it.
    /// </summary>
    public UWWorldEdit WorldEdit
    {
        get
        {
            if (mOWorldEdit == null || mOWorldEdit.Queries != TileQueries)
            {
                mOWorldEdit = new UWWorldEdit(TileQueries)
                {
                    CarriedObjectCount = fCarriedObjectCount,
                    PlayerTile = fPlayerTile,
                    Log = psMessage => Debug.Log(psMessage)
                };

                if (mOWorldView != null)
                    mOWorldView.Attach(mOWorldEdit);
            }

            return mOWorldEdit;
        }
    }

    /// <summary>Loaded game data. Used by Interaction, UWCharacter and UWGameUI.</summary>
    public DataImport UWDataImporter { get; private set; }

    public int CurrentLevelIndex
    {
        get { return miLevelIndex; }
    }

    /// <summary>The engine-free tile questions for the current level (P2 of the engine
    /// separation, 2026-09-17). Rebuilt when the level or the data change.</summary>
    public UWTileQueries TileQueries
    {
        get
        {
            if (mOTileQueries == null || mOTileQueries.Level != CurrentLevel || mOTileQueriesData != UWDataImporter)
            {
                mOTileQueries = new UWTileQueries(CurrentLevel, UWDataImporter);
                mOTileQueriesData = UWDataImporter;
            }

            return mOTileQueries;
        }
    }

    private UWTileQueries mOTileQueries;

    private DataImport mOTileQueriesData;

    public UWLevel CurrentLevel
    {
        get { return UWDataImporter != null ? UWDataImporter.Levels[miLevelIndex] : null; }
    }

    /// <summary>
    /// Counts every build and rebuild of the level's geometry (fBuildLevel, RebuildGeometry), the
    /// one exit after the tiles changed (a terrain trap, a lever). The palette path's sweep mask
    /// reads the tile types, so it is built again when this moves (UWSweepMaskDriver; cleanup step
    /// 6.3, 2026-10-24 - until then a standing eye rebuilt it twice a second just in case).
    /// </summary>
    public int GeometryRevision { get; private set; }

    public int LevelCount
    {
        get { return UWDataImporter != null ? UWDataImporter.Levels.Count : 0; }
    }

    /// <summary>
    /// Loads a level directly, without setting a player position - meant for
    /// noclip exploration while debugging, not for real level transitions (see TravelTo).
    /// </summary>
    public void DebugLoadLevel(int piLevelIndex)
    {
        if (UWDataImporter == null)
            return;

        int liCount = UWDataImporter.Levels.Count;
        int liStep = piLevelIndex < miLevelIndex ? -1 : 1;

        piLevelIndex = ((piLevelIndex % liCount) + liCount) % liCount;

        // The second game's world has places without a level (UWSessionWorld.Levels, null): on in the
        // same direction to the next that has one.
        for (int liTried = 0; liTried < liCount && UWDataImporter.Levels[piLevelIndex] == null; liTried++)
            piLevelIndex = (((piLevelIndex + liStep) % liCount) + liCount) % liCount;

        if (UWDataImporter.Levels[piLevelIndex] == null)
            return;

        UWWorldSync.Capture(this);

        miLevelIndex = piLevelIndex;
        fBuildLevel();
    }

    /// <summary>All world objects of the current level with their data - for UWWorldSync. Also
    /// disabled ones, so that nothing currently hidden slips through.</summary>
    public UWEntityInfo[] GetWorldEntities()
    {
        return mOWorldView != null ? mOWorldView.GetWorldEntities() : new UWEntityInfo[0];
    }

    /// <summary>
    /// Removes an object from its tile's list WITHOUT touching the display - for a
    /// creature that dies: its death animation is still running, but in the data it is already
    /// gone. A dead creature gives its slot back. UWWorldEdit.Forget.
    /// </summary>
    public bool ForgetObjectData(UWObject pOObject)
    {
        return WorldEdit.Forget(pOObject);
    }

    /// <summary>
    /// Moves an object into the list of another tile - for UWWorldSync, when a
    /// creature has walked on. If it stays on its tile, the order stays as well.
    /// Whatever is in no list is not re-inserted. UWWorldEdit.Move.
    /// </summary>
    public void MoveObjectData(UWObject pOObject, int piTileX, int piTileZ)
    {
        WorldEdit.Move(pOObject, piTileX, piTileZ);
    }

    /// <summary>A freshly created character (see UWCharacterCreationScreen), or null.
    /// Collected before loading, because it decides where the world comes from.</summary>
    private byte[] myNewCharacter;

    private void Start()
    {
        // BLACK FROM THE FIRST FRAME when the main menu is going to come: otherwise the game UI
        // showed until the menu or the opening took over (per user, 2026-09-22). The first logo
        // or UWMainMenu.Show takes it down; the branch without game data below as well.
        if (UWMainMenu.WillAutoShow)
            UWScreenUi.ShowLoadingCover();

        // The clock holds are static and survive every scene change. Whoever held the clock
        // before a rebuild - main menu, character creation, [MORE] - would have frozen the
        // new scene: black intro, character in mid-air, no movement (per user, 2026-09-11).
        UWGameClock.ReleaseAll();

        // The detail level from the options panel, before the palette renderer reads the render
        // mode (see UWGraphicsDetail).
        UWGraphicsDetail.EnsureApplied();

        myNewCharacter = UWCharacterCreationScreen.TakePendingPlayerData();

        // THE GAME THIS START BUILDS (UW2 plan phase 0 step 5, 2026-10-28): the one chosen last when
        // the installation holds it - the first start and every switch (UWGameSwitch) come through
        // here. A game the port cannot play yet shows its main menu screen and nothing more.
        UWSettings.Game = UWGameSwitch.Chosen();

        // The second game (UW2 plan phase 2 step 3): its main menu with its buttons, or the walk its
        // Create Character asked for (UWWalkUw2) - the screen alone where its data cannot be read.
        bool lbWalk = UWWalkUw2.Take();

        UWWalkUw2.IsWalking = false;

        if (UWSettings.Game.Game == UWGame.Uw2 && fStartUw2(lbWalk))
            return;

        if (!UWSettings.Game.IsPlayable && fShowScreenOnly())
            return;

        if (!fLoadData())
        {
            // Otherwise the screen would stay black forever. No game data: the setup screen asks
            // for the GOG folder and starts the scene again (per user, 2026-09-17).
            UWScreenUi.HideLoadingCover();
            UWSetupMenu.ShowFirstRun();
            return;
        }

        // EVERY START IS A NEW SESSION (cleanup step 2.7, 2026-10-14): a new character, a loaded
        // save game and the main menu at launch each get a session of their own, so quest flags,
        // game variables, story flags, the creatures' clock and alarm, the lava quirk, the cast
        // pause, the light colours and the trap log of the previous game cannot leak into this one.
        // fLoadData just loaded it (DataImport.LoadGame -> UWSaveCodec.Load, since step 2.9).
        UWGameSession.MakeCurrent(UWDataImporter.Session);

        // THE PLAYER'S RULE SET (cleanup phase 10, 2026-10-25): an optional rules.json in the game's
        // folder beside settings.json (UW1/rules.json since 2026-10-27, UWGameFolder; one left beside
        // settings.json by 0.9.2 to 0.10.0 moves there), applied to every new session before anything reads a rule - so a key taken
        // out of the file is the original's again with the next start or load. No file is the
        // original's rules; the game never writes one.
        fApplyRules();

        // Character creation leaves a black cover up during the rebuild. Until 2026-09-14 the
        // intro that followed removed it; without the intro the screen stayed black (per user).
        // Start builds the world in this same frame, so nothing half-built becomes visible.
        if (myNewCharacter != null)
            UWScreenUi.HideLoadingCover();

        fPrepareShared();
        fInitialiseLighting();
        fInitialiseDebugOverlay();

        // LAUNCH: no world, only the main menu (per user, 2026-09-14: the last save game is
        // no longer loaded at start, loading happens from the menu only). The static data,
        // atlas and texture array are ready by now and survive the scene reload that starts
        // the chosen game. The UI is still initialised - the main menu lives there.
        if (myNewCharacter == null && !UWSavegameSlots.IsRestoring)
        {
            HasWorld = false;

            // Nothing may move until a game is started - the player has no floor to stand on.
            // The logos, the intro and the menu run on the unscaled clock.
            UWGameClock.Hold(UWGameClock.NoWorldHold);

            fInitialiseUi();

            UWAudioService.Init(UWDataImporter);

            return;
        }

        HasWorld = true;

        // From a save game the character goes where it was saved - otherwise to
        // the fixed starting place. The level is one-based in the save game.
        UWPlayerData lOPlayer = UWDataImporter.InitialPlayer;

        bool lbFromSave = UWDataImporter.IsSavegame && lOPlayer != null && lOPlayer.IsLoaded
            && lOPlayer.DungeonLevel >= 1 && lOPlayer.DungeonLevel <= UWDataImporter.Levels.Count;

        if (lbFromSave)
        {
            miLevelIndex = lOPlayer.DungeonLevel - 1;

            fSeedStoredState(lOPlayer);
        }
        else if (myNewCharacter != null && lOPlayer != null)
        {
            // A new character starts at the top, with the initial values of its PLAYER.DAT - eight
            // talismans, the bullfrog attempts (see UWPlayerData.BuildNewCharacter).
            miLevelIndex = 0;

            fSeedStoredState(lOPlayer);
        }

        fBuildLevel();

        // The level build cleared the kin alarm; a save brings its own back, as RestoreGame
        // calls set_creatures_from_saved_game after GetLevel (since 2026-10-17, audit round 2 C7).
        if (lbFromSave)
            fRestoreKinAlarm(lOPlayer);

        if (lbFromSave || (myNewCharacter != null && lOPlayer != null))
        {
            // The new character exactly like a loaded one: via its tile (32/2) and the
            // tile floor, not via the fixed start point in the Inspector.
            fPlacePlayerAtSavedPosition(lOPlayer.PositionX, lOPlayer.PositionY, lOPlayer.ZPosition);
            fSetPlayerHeading(UWUnits.AngleToDegrees(lOPlayer.Heading));

            // The carried objects take their slots in this level, as in the original, in the
            // order of their chain in PLAYER.DAT (RestoreGame, see UWCarriedSlots) - then room
            // for them if the level is full (UWLevelChangeRules, the same order as TravelTo).
            List<UWObject> lOCarried = new List<UWObject>(lOPlayer.Equipment);
            lOCarried.AddRange(lOPlayer.Backpack);
            fArriveWithInventory(lOPlayer.GetCarriedChain(), UWObjectLimitRules.CountObjects(lOCarried),
                new UWTilePos(lOPlayer.PositionX >> 8, lOPlayer.PositionY >> 8));
        }
        else
        {
            fPlacePlayerOnFloor(mOPlayerStart);
        }

        fInitialiseUi();
        fReportRestore();

        // The sound: chips, theme logic, sound effects (since 2026-09-11). The level theme
        // starts right away - unless the main menu comes (it brings its own) or
        // a piece from the previous scene is still playing: that plays to the end, like the
        // intro music after loading in the original (per user, 2026-09-11).
        UWAudioService.Init(UWDataImporter);

        if (!UWMainMenu.WillAutoShow && !UWAudioService.IsMusicPlaying)
            UWAudioService.PickLevelTheme();

        // Physics.IgnoreLayerCollision(9, 8) used to be here - the sprite layer was
        // excluded wholesale from player collision, so that one would not get stuck on items
        // lying around. But that ALSO switched off collision with creatures,
        // which is why one could walk through enemies (per user, 2026-08-30).
        //
        // The rule has been unnecessary since 30.08.: which sprite stops the player
        // is now decided by COMOBJ.DAT. Height 0 means "no collision", and exactly the things
        // one walks over - bones, sacks, poles, campfires - are at 0 there
        // and get a trigger instead of a solid body (see
        // UWObjectSpawner.fHasNoCollision). Creatures and massive scenery have a height and
        // block.
    }

    /// <summary>
    /// THE SECOND GAME'S TELEPORT IN ITS WALK (UW2 plan phase 2 step 5; new_player_pos, see
    /// UWTeleportUw2): the level the target names (0 the one the player is on), the first free tile near
    /// the target by the strict pass of find_good_x_and_y (the loose pass clears objects, the first
    /// game's rule - not run; without a free tile the target itself), the player set up there - near the
    /// ceiling and falling when the trap says so - and turned to its heading. A level that is not in the
    /// world leaves the player where he is.
    /// </summary>
    public void WalkTeleport(UWTeleportUw2.Target pOTarget)
    {
        int liLevel = UWTeleportUw2.LevelOf(pOTarget, miLevelIndex + 1) - 1;

        if (liLevel < 0 || liLevel >= UWDataImporter.Levels.Count || UWDataImporter.Levels[liLevel] == null)
        {
            Debug.LogWarning("[Walk] A teleport to level " + (liLevel + 1) + ", which is not in this world.");

            return;
        }

        if (liLevel != miLevelIndex)
        {
            // The doors as they stand into the level's data, as a level change does (UWWorldSync).
            UWWorldSync.Capture(this);
            miLevelIndex = liLevel;
            fBuildLevel();
        }

        int liX = pOTarget.TileX;
        int liY = pOTarget.TileY;

        if (fFindArrivalTile(pOTarget.TileX, pOTarget.TileY, false, out int liFoundX, out int liFoundY))
        {
            liX = liFoundX;
            liY = liFoundY;
        }

        if (pOTarget.HasHeading)
            fSetPlayerHeading(UWUnits.AngleToDegrees(pOTarget.Heading));

        fPlacePlayerAtTile(liX, liY);

        // player_setup with how 0: near the ceiling, 1000 less the player's height in fine z (zpos * 8),
        // when that is above where he stands.
        if (pOTarget.Falls && UWDataImporter.CommonObjectProperties != null
            && UWDataImporter.CommonObjectProperties.TryGet(UWObjectIds.LastCreature, out UWCommonObjectProperties.Entry lOPlayerProperties))
        {
            float lfHigh = UWUnits.ZPosToWorld(TeleportCeilingZPos - lOPlayerProperties.Height);
            Vector3 lOAt = new Vector3(UWUnits.TileCentreToWorldAxis(liX), 0f, UWUnits.TileCentreToWorldAxis(liY));

            if (lfHigh > GetFloorHeightAt(lOAt))
                fPlacePlayerOnFloor(lOAt, lfHigh);
        }

        Debug.Log(string.Format("[Walk] Teleport to level {0}, tile {1}/{2}{3}.", miLevelIndex + 1, liX, liY, pOTarget.Falls ? ", falling" : string.Empty));
    }

    /// <summary>player_setup's ceiling for a falling arrival: 1000 in fine z, 125 in zpos.</summary>
    private const int TeleportCeilingZPos = 1000 >> 3;

    /// <summary>
    /// Switches to another level and puts the player on the given tile.
    /// </summary>
    public void TravelTo(int piLevelIndex, int piTileX, int piTileZ)
    {
        if (UWDataImporter == null || Time.time < mfTransitionBlockedUntil)
            return;

        if (piLevelIndex < 0 || piLevelIndex >= UWDataImporter.Levels.Count)
        {
            Debug.LogWarning("Level " + (piLevelIndex + 1) + " does not exist.");
            return;
        }

        mfTransitionBlockedUntil = Time.time + TransitionCooldown;

        // THE SECOND GAME'S WALK (UW2 plan phase 2 step 4, for the debug jump to its ice and water):
        // the level built and the player on the tile, nothing of the first game's level change
        // (creatures, carried objects, arrival rules) - the second game's is step 5.
        if (UWWalkUw2.IsWalking)
        {
            if (UWDataImporter.Levels[piLevelIndex] == null)
            {
                Debug.LogWarning("[Walk] Level " + (piLevelIndex + 1) + " is not in this world.");
                return;
            }

            if (piLevelIndex != miLevelIndex)
            {
                UWWorldSync.Capture(this);
                miLevelIndex = piLevelIndex;
                fBuildLevel();
            }

            fPlacePlayerAtTile(piTileX, piTileZ);
            Debug.Log(string.Format("[Walk] Level {0}, tile {1}/{2}.", piLevelIndex + 1, piTileX, piTileZ));

            return;
        }

        // Write what happened here into the level's data before its objects disappear - otherwise
        // everything would be as before on coming back (see UWWorldSync).
        UWWorldSync.Capture(this);

        // On leaving a level the original tidies up among its creatures: home to
        // the home tile, wounds partly healed, allies independent again, the
        // attitude per race balanced (see UWLevelExit). A jump WITHIN the level
        // does not count - the original only knows this step on a real transition.
        if (piLevelIndex != miLevelIndex)
            UWLevelExit.LeaveLevel(WorldEdit);

        // Tybal's orb drains the mana in its lair and gives it back on leaving (UWTybalOrbRules) -
        // on a real level change only, like the tidying above; loading a save keeps what it has.
        if (piLevelIndex != miLevelIndex && UWScene.Character != null)
            UWTybalOrbRules.OnLevelChange(UWScene.Character.Vitals, miLevelIndex, piLevelIndex);

        bool lbOtherLevel = piLevelIndex != miLevelIndex;

        // The carried objects leave the old level's static slots (ClearObjectList_ovr118_4BE),
        // after the running effects gave back theirs, and take new ones in the next
        // (ovr118_3CB) - see UWCarriedSlots and UWLevelChangeRules.
        List<UWObject> lOCarriedChain = lbOtherLevel ? fCarriedChain() : null;

        UWLevelChangeRules.Leave(WorldEdit, lOCarriedChain);

        miLevelIndex = piLevelIndex;

        fBuildLevel();

        // GetLevel's order (decision 9): the level is built, the slots are taken, room is made
        // only for what they did not cover - around the tile the player arrives on.
        if (lOCarriedChain != null)
            fArriveWithInventory(lOCarriedChain, fCarriedObjectCount(),
                new UWTilePos(Mathf.Clamp(piTileX, 0, UWWorldScale.TilesPerAxis - 1),
                    Mathf.Clamp(piTileZ, 0, UWWorldScale.TilesPerAxis - 1)));

        // The palette renderer's material swap remembers the level number. If it stays
        // the same - a jump within the same level - it would otherwise miss the rebuild
        // and the fresh geometry would stay on URP.
        if (mORenderPaths == null)
            mORenderPaths = GetComponent<UWRenderPathHost>();

        if (mORenderPaths != null)
            mORenderPaths.OnLevelBuilt();

        // WHERE HE ARRIVES (new_player_pos, UWArrivalRules): the nearest tile with room, then the
        // same search clearing what stands in the way, then - nowhere at all - death.
        int liArriveX;
        int liArriveY;
        bool lbFound = fFindArrivalTile(piTileX, piTileZ, false, out liArriveX, out liArriveY)
            || fFindArrivalTile(piTileX, piTileZ, true, out liArriveX, out liArriveY);

        fPlacePlayerAtTile(lbFound ? liArriveX : piTileX, lbFound ? liArriveY : piTileZ);

        if (!lbFound && UWScene.Character != null)
            UWScene.Character.Damage(UWDamageRequest.ArrivalNowhere(ArrivalNowhereDamage));

        Debug.Log(string.Format("Entered level {0}, tile ({1},{2}) for ({3},{4})", miLevelIndex + 1,
            liArriveX, liArriveY, piTileX, piTileZ));
    }

    /// <summary>More than any hit points: new_player_pos sets them to 0 when no tile has room.</summary>
    private const int ArrivalNowhereDamage = 0x7FFF;

    /// <summary>
    /// A teleport trap (do_teleport, event/WORLDEV.C:175-200): on the SAME level the target must
    /// have room by the first search before anyone moves - without it nothing happens (no
    /// clearing, no death); the coordinate 0x3F skips that. Then the move goes like every other
    /// (TravelTo), whose own search finds the tile at once. Another level is not searched here.
    /// </summary>
    public void TeleportTo(int piLevelIndex, int piTileX, int piTileZ)
    {
        if (piLevelIndex == miLevelIndex && CurrentLevel != null
            && piTileX != UWArrivalRules.NoSearchCoordinate && piTileZ != UWArrivalRules.NoSearchCoordinate)
        {
            int liX;
            int liY;

            if (!fFindArrivalTile(piTileX, piTileZ, false, out liX, out liY))
            {
                Debug.Log(string.Format("Teleport to ({0},{1}): no room there, nothing happens", piTileX, piTileZ));

                return;
            }

            piTileX = liX;
            piTileZ = liY;
        }

        TravelTo(piLevelIndex, piTileX, piTileZ);
    }

    /// <summary>A motion core of its own for the arrival test, so no running step's records are
    /// touched; made anew when the level's world is.</summary>
    private UWMotionCore mOArrivalCore;

    /// <summary>The extra test of a scatter spot: the container clearance, can_place, both or none.</summary>
    private System.Func<int, int, bool> fScatterAlsoFits(int piItemId, bool pbAvoidWorldContainers, bool pbFitsLikeCanPlace,
        int piZ)
    {
        if (!pbAvoidWorldContainers && !pbFitsLikeCanPlace)
            return null;

        return (piX, piY) => (!pbAvoidWorldContainers || fClearOfWorldContainers(piX, piY, piItemId))
            && (!pbFitsLikeCanPlace || ObjectFitsAt(piItemId, piX, piY, piZ));
    }

    /// <summary>
    /// can_place as drop_around_place asks it (UnderworldExhumed motion/COLLIDE.C): the object at
    /// x, y in eighths and the coarse height z, as a flier, range 0 - the terrain and every solid
    /// object around, the player and the creatures included (UWMotionCore.CheckIfItemFitsInTile
    /// over UWMotionHost's bodies). The object is not in the world yet: index 0.
    /// </summary>
    public bool ObjectFitsAt(int piItemId, int piX8, int piY8, int piZ)
    {
        return ObjectFitsAt(piItemId, piX8, piY8, piZ, true, 0);
    }

    /// <summary>can_place with its last two arguments: pbFlier lets the object hang above what it
    /// would rest on, piRange is how far it may (plant_seed asks 0 and 0).</summary>
    public bool ObjectFitsAt(int piItemId, int piX8, int piY8, int piZ, bool pbFlier, int piRange)
    {
        if (!fEnsureArrivalCore())
            return false;

        mOArrivalCore.Mover = new UWMotionBody
        {
            Index = 0,
            ItemId = piItemId,
            XPos = piX8 & 7,
            YPos = piY8 & 7,
            ZPos = piZ
        };

        return mOArrivalCore.CheckIfItemFitsInTile(piItemId, 0, piX8, piY8, piZ, pbFlier, piRange, out int _, out int _);
    }

    /// <summary>The core for the fit tests over the current world (arrival, scatter).</summary>
    private bool fEnsureArrivalCore()
    {
        UWCommonObjectProperties lOProperties = UWDataImporter != null ? UWDataImporter.CommonObjectProperties : null;
        UWMotionHost lOWorld = UWMotionHost.Ensure(this);

        if (CurrentLevel == null || lOProperties == null || lOWorld == null)
            return false;

        if (mOArrivalCore == null || mOArrivalWorld != lOWorld)
        {
            mOArrivalWorld = lOWorld;
            mOArrivalCore = new UWMotionCore(lOWorld, lOProperties);
        }

        return true;
    }

    private UWMotionHost mOArrivalWorld;

    /// <summary>find_good_x_and_y over the current level (UWArrivalRules): pbClear is the second
    /// pass, which removes from every tile it tries what UWArrivalRules.IsClearedForArrival names.
    /// </summary>
    private bool fFindArrivalTile(int piTileX, int piTileY, bool pbClear, out int piFoundX, out int piFoundY)
    {
        piFoundX = piTileX;
        piFoundY = piTileY;

        UWLevel lOLevel = CurrentLevel;
        UWCommonObjectProperties lOProperties = UWDataImporter != null ? UWDataImporter.CommonObjectProperties : null;

        if (!fEnsureArrivalCore())
            return false;

        System.Action<int, int> lfClear = null;

        if (pbClear)
        {
            lfClear = (piX, piY) =>
            {
                UWTile lOTile = lOLevel.GetTile(piX, piY);

                if (lOTile == null || lOTile.ObjectsInTile == null)
                    return;

                foreach (UWObject lOObject in new List<UWObject>(lOTile.ObjectsInTile))
                {
                    if (UWArrivalRules.IsClearedForArrival(lOObject, lOProperties))
                        RemoveObjectFromWorld(lOObject);
                }
            };
        }

        return UWArrivalRules.FindGoodXAndY(piTileX, piTileY,
            (piX, piY) =>
            {
                UWTile lOTile = lOLevel.GetTile(piX, piY);

                return lOTile != null ? lOTile.TileType : UWTile.TileTypeEnum.solid;
            },
            (piX, piY) => UWArrivalRules.AdventurerFits(mOArrivalCore, lOLevel.GetTile(piX, piY), piX, piY),
            lfClear, out piFoundX, out piFoundY);
    }

    /// <summary>
    /// Static game data and the resources built from it, kept across scene reloads. Loading a
    /// save game or starting a new character reloads the scene (see UWSavegameSlots,
    /// UWCharacterCreationScreen); without this cache every start would parse all data files
    /// and rebuild the object atlas and texture array again. They are prepared while the main
    /// menu is up and reused by the game that is started from it (per user, 2026-09-14).
    /// Objects referenced from static fields survive Resources.UnloadUnusedAssets.
    ///
    /// ONE GAME'S (UW2 plan phase 0 step 3, 2026-10-28): the data is read for the running game
    /// (UWSettings.Game) and read anew when another game runs; the atlas, the texture array and the
    /// 3D models built from it go with it (fLoadData).
    /// </summary>
    private static DataImport msOSharedData;

    /// <summary>The data loaded so far (null before the first load) - for the language switched
    /// while the game runs (UWLanguage.Switch, translation step 3R), which lays the game's texts over
    /// it in place.</summary>
    public static DataImport SharedData => msOSharedData;

    private static UWObjectAtlas msOSharedAtlas;

    private static Texture2DArray msOSharedTextureArray;

    private static UW3DModelImport msOShared3DModels;

    /// <summary>Filter mode the cached atlas and texture array were built with.</summary>
    private static FilterMode meSharedFilterMode;

    /// <summary>
    /// Is a world built? False while the game has just been launched and only the main menu
    /// is shown: there is no save game to continue any more, the world is loaded from the menu
    /// only (per user, 2026-09-14).
    /// </summary>
    public bool HasWorld { get; private set; }

    /// <summary>The player's rules.json onto the current session (UWGameSession.ApplyRulesFile): what
    /// it changed goes to the log, every problem as a warning - a malformed file leaves the
    /// original's rules. The debug panel's Place section shows the summary (UWDebugOverlay).</summary>
    private static void fApplyRules()
    {
        string lsRules = UWGameFolder.AdoptLegacyFile(UWUserSettings.PathBeside(string.Empty), UWGameSession.Current.Game, UWRulesJson.FileName,
            psMessage => Debug.Log("[Rules] " + psMessage), psMessage => Debug.LogWarning("[Rules] " + psMessage));
        UWRulesReport lOReport = UWGameSession.Current.ApplyRulesFile(lsRules);

        if (!lOReport.Found)
            return;

        if (lOReport.HasProblems)
            Debug.LogWarning("[Rules] " + lOReport.Summary);
        else
            Debug.Log("[Rules] " + lOReport.Summary);

        if (lOReport.Applied.Count > 0)
            Debug.Log("[Rules] taken from " + lOReport.Source + ": " + string.Join(", ", lOReport.Applied));

        foreach (string lsProblem in lOReport.Problems)
            Debug.LogWarning("[Rules] " + lsProblem);
    }

    private bool fLoadData()
    {
        UWSettings lOSettings = UWSettings.Instance;

        string lsPath = lOSettings != null ? lOSettings.DataPath : null;

        if (!UWSettings.IsValidDataPath(lsPath))
        {
            string lsDetected = UWSettings.AutoDetectDataPath();

            if (lsDetected == null)
            {
                // Not an error any more: the setup screen asks for the GOG folder (UWSetupMenu).
                Debug.Log("No game data found - showing the setup screen.");
                return false;
            }

            lsPath = lsDetected;
        }

        // A save game only when one was chosen in the menu (see UWSavegameSlots). A freshly
        // created character starts in the untouched world of the data folder, and so does the
        // data loaded for the main menu at launch (no world is built then, see Start).
        string lsSavegame = myNewCharacter != null ? null : UWSavegameSlots.PendingPath;

        // The game's texts of the language in force (UWLanguage, chosen at startup) are laid over the
        // English strings as the data is read; the shared data is read anew if they differ.
        string lsGameText = UWLanguage.GameTextFile;

        IUWGameProfile lOGame = UWSettings.Game;

        if (msOSharedData == null || msOSharedData.DataPath != lsPath || msOSharedData.GameData.GameTextFile != lsGameText
            || msOSharedData.GameData.Profile != lOGame)
        {
            // Another game's 3D models are another program's: they go with the data they came with.
            if (msOSharedData != null && msOSharedData.GameData.Profile != lOGame)
                msOShared3DModels = null;

            msOSharedData = new DataImport(lsPath, lsSavegame, lsGameText, lOGame);
            UWLanguage.LogGameText(msOSharedData.GameText, lsGameText);
            msOSharedAtlas = null;
            msOSharedTextureArray = null;
        }
        else
        {
            msOSharedData.LoadGame(lsSavegame);
        }

        UWDataImporter = msOSharedData;

        // The help's notes belong to the save game; a new character starts without any.
        if (UWDataImporter.IsSavegame)
            UWHelpNotes.LoadFrom(UWDataImporter.SavegamePath);
        else
            UWHelpNotes.Clear();

        UWHelpWindow.RestoreAfterLoad();

        if (myNewCharacter != null)
        {
            UWDataImporter.ReplaceInitialPlayer(UWPlayerData.FromPlain(myNewCharacter));

            Debug.Log(string.Format("[Creation] New character {0} on level 1.", UWDataImporter.InitialPlayer.Name));
        }

        if (UWDataImporter.IsSavegame)
            Debug.Log(string.Format("[Savegame] {0} loaded: {1}, level {2}, tile {3}/{4}.",
                UWDataImporter.SavegamePath, UWDataImporter.InitialPlayer.Name,
                UWDataImporter.InitialPlayer.DungeonLevel,
                UWDataImporter.InitialPlayer.TileX, UWDataImporter.InitialPlayer.TileY));

        return true;
    }

    /// <summary>
    /// THE SECOND GAME'S START (UW2 plan phase 2 step 3, 2026-10-29): its data read (DataImport with its
    /// profile) and its session current; then either its main menu (UWMainMenu.InitWalkMenu) or the
    /// walk - the level a new game starts on, built as the first game's are, seen through the noclip
    /// camera at the start tile (UWGameProfileUw2.NewGame...). Nothing of the first game's play runs:
    /// no rules file (the session has no rule set), no player (it stays off), no HUD, no creature
    /// driver, death or Void (UW1 components that read the rules). HasWorld stays false for them.
    /// False when the data cannot be read: the screen alone then (fShowScreenOnly).
    /// </summary>
    private bool fStartUw2(bool pbWalk)
    {
        // A first-game character waiting from its creation does not belong to this game.
        myNewCharacter = null;

        try
        {
            if (!fLoadData())
                return false;

            // A save chosen in the walk menu's Journey Onward (step 6): its world and its PLAYER.DAT.
            string lsSave = pbWalk ? UWWalkUw2.TakeSavePath() : null;

            if (lsSave != null)
                UWDataImporter.LoadGame(lsSave);

            // A character just created (step 5 part B): the new walk is its game.
            UWPlayerData.NewCharacter lONewCharacter = UWCharacterCreationScreenUw2.TakePendingCharacter();

            if (pbWalk && lsSave == null && lONewCharacter != null)
                UWSaveCodec.StartNewCharacterUw2(UWDataImporter.Session, lONewCharacter);
        }
        catch (System.Exception lOError)
        {
            Debug.LogWarning("[Game] " + UWSettings.Game.Title + ": its data could not be read - " + lOError.Message);

            return false;
        }

        UWGameSession.MakeCurrent(UWDataImporter.Session);
        HasWorld = false;
        UWAudioService.StopMusic();

        if (!pbWalk)
        {
            UWGameClock.Hold(UWGameClock.NoWorldHold);

            Camera lOCamera = Camera.main;
            UWMainMenu lOMenu = lOCamera != null ? lOCamera.GetComponent<UWMainMenu>() : null;

            if (lOCamera != null && lOMenu == null)
                lOMenu = lOCamera.gameObject.AddComponent<UWMainMenu>();

            if (lOMenu == null)
                return false;

            lOMenu.InitWalkMenu(UWDataImporter);
            Debug.Log("[Game] " + UWSettings.Game.Title + ": its main menu (Create Character starts a walk through its world).");

            return true;
        }

        UWWalkUw2.IsWalking = true;

        fPrepareShared();
        fInitialiseLighting();

        // From a save: where it was made (UWPlayerRecordUw2); else where a new game starts.
        UWPlayerFile lOPlayer = UWDataImporter.Session.PlayerFileUw2;
        int liSavedLevel = lOPlayer != null ? UWPlayerRecordUw2.Level(lOPlayer) - 1 : -1;

        miLevelIndex = liSavedLevel >= 0 && liSavedLevel < UWDataImporter.Levels.Count && UWDataImporter.Levels[liSavedLevel] != null
            ? liSavedLevel : UWGameProfileUw2.NewGameLevel - 1;

        if (CurrentLevel == null)
        {
            Debug.LogWarning("[Walk] Level " + UWGameProfileUw2.NewGameLevel + " is missing from LEV.ARK.");

            return false;
        }

        fBuildLevel();

        if (lOPlayer != null && liSavedLevel == miLevelIndex)
        {
            fSetPlayerHeading(UWUnits.AngleToDegrees(UWPlayerRecordUw2.Heading(lOPlayer)));
            fPlacePlayerAtSavedPosition(UWPlayerRecordUw2.PositionX(lOPlayer), UWPlayerRecordUw2.PositionY(lOPlayer), UWPlayerRecordUw2.ZPosition(lOPlayer));

            if (GetComponent<UWWalkUw2View>() == null)
                gameObject.AddComponent<UWWalkUw2View>();

            Debug.Log(string.Format("[Walk] From {0}: {1}, level {2}, tile {3}/{4}.", UWDataImporter.SavegamePath, lOPlayer.Name,
                miLevelIndex + 1, UWPlayerRecordUw2.PositionX(lOPlayer) >> 8, UWPlayerRecordUw2.PositionY(lOPlayer) >> 8));
        }
        else
        {
            fEnterWalkView(UWGameProfileUw2.NewGameTileX, UWGameProfileUw2.NewGameTileY, UWGameProfileUw2.NewGameHeading);
        }

        // The debug jump (Shift+F7, "level,X,Y") for the walk's tests, on the debug panel; the panel's
        // report (F1) reads the first game's character and is not for the walk.
        fInitialiseDebugOverlay();

        UWSetupMenu.EnsureInGame(UWDataImporter);
        UWScreenUi.HideLoadingCover();

        if (lOPlayer == null || liSavedLevel != miLevelIndex)
            Debug.Log(string.Format("[Walk] {0}: level {1}, tile {2}/{3} - walking with its motion (N for noclip, Page Up/Down there change the level).",
                UWSettings.Game.Title, miLevelIndex + 1, UWGameProfileUw2.NewGameTileX, UWGameProfileUw2.NewGameTileY));

        return true;
    }

    /// <summary>
    /// The walk's start: the player in the tile's centre on its floor, facing piHeading (the original's
    /// 16-bit heading, 0 north), as player_setup puts a new game's (step 4: the player walks with the
    /// second game's motion; step 3 had the noclip camera here). The second game's light of the level
    /// follows the view (UWWalkUw2View).
    /// </summary>
    private void fEnterWalkView(int piTileX, int piTileY, int piHeading)
    {
        fSetPlayerHeading(UWUnits.AngleToDegrees(piHeading));
        fPlacePlayerAtTile(piTileX, piTileY);

        if (GetComponent<UWWalkUw2View>() == null)
            gameObject.AddComponent<UWWalkUw2View>();
    }

    /// <summary>
    /// The main menu screen of a game the port cannot play yet (UW2 plan phase 0 step 5): no world, no
    /// data read beyond the screen, the menu bar over it to switch back. False when the screen cannot
    /// be read - the first game starts instead.
    /// </summary>
    private bool fShowScreenOnly()
    {
        UWSettings lOSettings = UWSettings.Instance;
        string lsData = lOSettings != null ? lOSettings.DataPath : null;
        UWPicture lOScreen = null;

        try
        {
            if (UWSettings.IsValidDataPath(lsData))
                lOScreen = UWScreensUw2.MainMenu(lsData);
        }
        catch (System.Exception lOError)
        {
            Debug.LogWarning("[Game] " + UWSettings.Game.Title + ": " + lOError.Message);
        }

        Camera lOCamera = Camera.main;

        if (lOScreen == null || lOCamera == null)
        {
            Debug.LogWarning("[Game] No main menu screen of " + UWSettings.Game.Title + " in " + (lsData ?? "(no data)")
                + " - starting " + UWGameProfiles.Uw1.Title + ".");
            UWSettings.Game = UWGameProfiles.Uw1;

            return false;
        }

        HasWorld = false;
        UWGameClock.Hold(UWGameClock.NoWorldHold);

        // The first game's music may still play from the menu the switch came from; the second's is
        // not read yet.
        UWAudioService.StopMusic();

        UWMainMenu lOMenu = lOCamera.GetComponent<UWMainMenu>();

        if (lOMenu == null)
            lOMenu = lOCamera.gameObject.AddComponent<UWMainMenu>();

        FilterMode leFilter = lOSettings != null ? lOSettings.TextureFilterMode : FilterMode.Point;

        lOMenu.ShowScreenOnly(UWTextureFactory.FromPicture(lOScreen, leFilter, "UW2 main menu"));

        Debug.Log("[Game] " + UWSettings.Game.Title + ": its main menu screen (the port cannot play it yet).");

        return true;
    }

    /// <summary>
    /// Texture atlas, texture array and materials apply to all levels and are only
    /// built once - otherwise every level change would cost unnecessary time.
    /// </summary>
    private void fPrepareShared()
    {
        if (meSharedFilterMode != fGetFilterMode())
        {
            msOSharedAtlas = null;
            msOSharedTextureArray = null;
            meSharedFilterMode = fGetFilterMode();
        }

        if (mOObjectAtlas == null)
        {
            if (msOSharedAtlas == null)
                msOSharedAtlas = UWObjectAtlasBuilder.Build(UWDataImporter, fGetFilterMode());

            mOObjectAtlas = msOSharedAtlas;
        }

        if (mODungeonMaterial == null)
        {
            if (msOSharedTextureArray == null)
                msOSharedTextureArray = UWTextureArrayBuilder.Build(UWDataImporter, fGetFilterMode());

            mOTextureArray = msOSharedTextureArray;
            mODungeonMaterial = UWLevelAssembler.CreateMaterial(mOTextureArray, "UW Dungeon");

            if (mODungeonMaterial != null)
            {
                mODungeonMaterial.SetFloat(UWShaderIds.AmbientFloor, fGetAmbientFloor());
                mODungeonMaterial.SetFloat(UWShaderIds.PointLightCap, fGetPointLightCap());
            }

            fStartTextureAnimation();
        }

        fCreateObjectMaterials();

        // The render paths (UWRenderPathHost): the palette path starts by itself as soon as
        // everything is in place (UWPaletteRenderPath.TryStart), Remastered as soon as the
        // dungeon material is there. Only provide the component here; otherwise
        // UWGraphicsDetail.SetLevel would add it on the first switch of the detail level.
        if (GetComponent<UWRenderPathHost>() == null)
            gameObject.AddComponent<UWRenderPathHost>();

        // And for the frame tick of the creatures: the 16-slot clock and the walk over every
        // creature that is due (see UWCritterDriver). Not on the second game's walk, whose creatures
        // stand and whose session has no rules (UW2 plan phase 2 step 3) - nor death and the Void
        // below.
        bool lbFirstGamePlay = !UWWalkUw2.IsWalking;

        if (lbFirstGamePlay && GetComponent<UWCritterDriver>() == null)
            gameObject.AddComponent<UWCritterDriver>();

        // The setup menu's bar, which folds out over the in-game menus (UWSetupMenu.EnsureInGame):
        // a loaded game reloads the scene and the main menu's instance is gone with it.
        if (UWDataImporter != null)
            UWSetupMenu.EnsureInGame(UWDataImporter);

        // The Remastered render mode, its light sources and ground shadows are UWRenderPathHost's
        // (UWRemasterRenderPath) since cleanup step 6.5 (2026-10-24).

        // Death and resurrection at the silver tree, see UWDeath.
        if (lbFirstGamePlay && GetComponent<UWDeath>() == null)
            gameObject.AddComponent<UWDeath>();

        // The effects of the Ethereal Void on level 9, see UWVoidEffects.
        if (lbFirstGamePlay && GetComponent<UWVoidEffects>() == null)
            gameObject.AddComponent<UWVoidEffects>();

        // Monitoring of graphics resources, see UWResourceWatch.
        if (GetComponent<UWResourceWatch>() == null)
            gameObject.AddComponent<UWResourceWatch>();

        if (mO3DModels == null)
        {
            UWSettings lOSettings = UWSettings.Instance;

            if (msOShared3DModels == null && lOSettings != null && lOSettings.IsExePathValid)
            {
                // The model import reads UW.EXE at offsets of the GOG build only.
                if (UWGogInstall.IsSupportedExe(lOSettings.ExePath, UWSettings.Game))
                    msOShared3DModels = new UW3DModelImport(lOSettings.ExePath, UWSettings.Game.Models);
                else
                    Debug.LogWarning("UWLevelLoader: " + lOSettings.ExePath + " is not the supported UW.EXE (GOG version) - 3D models fall back to sprites.");
            }

            mO3DModels = msOShared3DModels;
        }
    }

    /// <summary>
    /// Armageddon has been cast (see UWMiscSpell).
    ///
    /// The flag stays set for the rest of the game, just as in the original: there it is
    /// stored in the save game, and on entering EVERY level its object list is cleared
    /// (the reference: teleportation.cs, branch "entering a level under the influence of
    /// armageddon"). So the Abyss stays empty forever.
    /// </summary>
    public bool ArmageddonActive { get; private set; }

    /// <summary>
    /// Deletes all objects of the current level - items, creatures, doors,
    /// triggers, everything. Also sets the flag, so that every further level fares the
    /// same on entering.
    ///
    /// The reference clears the object lists of all tiles for this and frees the whole
    /// object table (UWTileMap.ResetMap), as the original's Map_ObjFix does. For us these are
    /// two things: the level data - tile lists and free lists (UWWorldEdit.WipeAll, since
    /// 2026-10-12 with the slots) - and the display objects below.
    /// </summary>
    public void ApplyArmageddon()
    {
        ArmageddonActive = true;

        // UW.EXE MajorSpellClassB clears the silver tree nibble along with the objects.
        UWSilverTree.TreeLevel = 0;

        fWipeObjects();
    }

    /// <summary>The tile lists in UWWorldEdit.WipeAll, the pictures and the registry in the
    /// view's answer to LevelCleared.</summary>
    private void fWipeObjects()
    {
        // The things in flight go with the rest (UWLevel.ResetObjectStore empties their list).
        if (UWMotionHost.Instance != null)
            UWMotionHost.Instance.EndAllFlights();

        WorldEdit.WipeAll();
    }

    /// <summary>Whether the player currently wears the crown of maze navigation - set via
    /// SetMazeNavigation by UWCharacter.RefreshStatus.
    /// </summary>
    private bool mbMazeNavigationWanted;

    /// <summary>Whether the tile data of level 7 is currently recoloured.</summary>
    private bool mbMazeNavigationApplied;

    /// <summary>
    /// Reports whether the crown of maze navigation is worn (see UWMazeNavigation).
    ///
    /// Called on every status refresh (every frame until cleanup step 4.6, 2026-10-18) and only
    /// does something if something really has changed - the switch costs a rebuild of the geometry.
    /// </summary>
    public void SetMazeNavigation(bool pbOn)
    {
        if (pbOn == mbMazeNavigationWanted)
            return;

        mbMazeNavigationWanted = pbOn;

        fApplyMazeNavigation(true);
    }

    /// <summary>
    /// Brings the tile data of the maze level into the wanted state.
    ///
    /// Only on the level where the enchantment acts: if one enters another, the
    /// tiles are reset, so that they are not wrongly recoloured on the next
    /// visit.
    /// </summary>
    private void fApplyMazeNavigation(bool pbRebuild)
    {
        if (UWDataImporter == null || UWDataImporter.Levels == null
            || UWMazeNavigation.LevelIndex >= UWDataImporter.Levels.Count)
            return;

        bool lbWanted = mbMazeNavigationWanted && miLevelIndex == UWMazeNavigation.LevelIndex;

        if (lbWanted == mbMazeNavigationApplied)
            return;

        UWMazeNavigation.Apply(UWDataImporter.Levels[UWMazeNavigation.LevelIndex], lbWanted);

        mbMazeNavigationApplied = lbWanted;

        if (pbRebuild)
            RebuildGeometry();
    }

    /// <summary>
    /// THE MESHES OF A LEVEL LEFT (found 2026-10-21 in Player.log: some hundred more meshes after
    /// every level change, textures flat): the level's chunks, objects and creature frames are
    /// meshes made at runtime (UWChunkGeometry, UWObjectSpawner, UWCritterAnimator), assets of
    /// their own that outlive the destroyed root - only a scene load (a save game) unloaded them.
    /// One frame after the old root is gone - Destroy takes effect at the frame's end - the
    /// unused assets are unloaded, as the scene load does; what the scripts still hold (the
    /// caches) stays. Several rebuilds in one frame ask once.
    /// </summary>
    private void fUnloadUnusedSoon()
    {
        if (mbUnloadPending)
            return;

        mbUnloadPending = true;
        StartCoroutine(fUnloadUnusedNextFrame());
    }

    private bool mbUnloadPending;

    private System.Collections.IEnumerator fUnloadUnusedNextFrame()
    {
        yield return null;

        mbUnloadPending = false;
        Resources.UnloadUnusedAssets();
    }

    /// <summary>The meshes of a geometry built for it alone (RebuildGeometry's chunks), destroyed
    /// with it at once - a lever or a terrain trap rebuilds in play, where the full unload would
    /// stall a frame.</summary>
    private static void fDestroyMeshes(GameObject pORoot)
    {
        foreach (MeshFilter lOFilter in pORoot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (lOFilter.sharedMesh != null)
                Destroy(lOFilter.sharedMesh);
        }
    }

    private void fBuildLevel()
    {
        GeometryRevision++;

        UWCritter.ResetAlarm();

        // The carried objects take their numbers from this level (UWCarriedSlots).
        UWCarriedSlots.Level = CurrentLevel;

        // The flights of the level left stay in its data (UWLevel.InFlight) and are not stepped any
        // more; their pictures go with the old root (2026-10-19).
        if (UWMotionHost.Instance != null)
            UWMotionHost.Instance.ForgetFlights();

        if (mOLevelRoot != null)
        {
            // Deactivate first, then destroy: Destroy only takes effect at the end of the frame, until then
            // the old transition triggers would still fire.
            mOLevelRoot.SetActive(false);
            Destroy(mOLevelRoot);
            fUnloadUnusedSoon();
        }

        mOLevelRoot = new GameObject("Level " + (miLevelIndex + 1));
        mOLevelRoot.transform.SetParent(transform, false);

        // Entering means: the level has an automap (see UWLevel.EnsureAutomap).
        CurrentLevel.EnsureAutomap();

        // Before the geometry, so that the recoloured tiles are built right away instead of
        // costing a second build.
        fApplyMazeNavigation(false);

        fBuildGeometry(mOLevelRoot.transform);
        fSpawnObjects(mOLevelRoot.transform);

        // The second game's stairs and teleports come with step 5 of its plan.
        if (!UWWalkUw2.IsWalking)
            fSpawnTransitions(mOLevelRoot.transform);

        // After Armageddon every level is empty, even one never entered - see
        // ArmageddonActive. Clearing happens after the build instead of before: only then are the tile lists
        // complete, and the transitions do not hang on objects.
        if (ArmageddonActive)
            fWipeObjects();

        // The things in flight of the level data fly on - a save's, or the ones left in the air
        // when the player went elsewhere (UWMissiles.ResumeFlights, 2026-10-19).
        UWMissiles lOMissiles = UWMissiles.Ensure(this);

        if (lOMissiles != null)
            lOMissiles.ResumeFlights(CurrentLevel);
    }

    /// <summary>
    /// Takes the baked prefab if one is assigned. Otherwise the geometry is
    /// computed - that takes about half a second per level.
    /// </summary>
    private void fBuildGeometry(Transform pOParent)
    {
        // The floor heights the sprite rule reads (UWOwnTile.SetFloorHeights).
        UWOwnTile.SetFloorHeights(CurrentLevel);

        // THE BAKED PREFABS (mOBakedLevels) ARE NOT USED ANY MORE (2026-09-28): the palette path
        // paints in the original's order and needs the tile and kind of every face in the mesh
        // (UV channel 1, UWChunkGeometry.TileInfo), which the prefabs baked before do not carry.
        // The geometry is built from the data at every load, as it already was after every lever
        // and for the maze navigation.
        UWChunkGeometry[] lOChunks = UWLevelMeshBuilder.Build(CurrentLevel, UWTextureArrayBuilder.GetFloorSliceOffset(UWDataImporter));

        GameObject lORoot = UWLevelAssembler.CreateLevelRoot("Geometry", lOChunks, mODungeonMaterial, miLevelIndex, true);
        lORoot.transform.SetParent(pOParent, false);
    }

    /// <summary>For UWHeightLever/UWTriggerSystem (original: a lever raises its own
    /// tile, verified live per user): rebuilds the level geometry after a
    /// runtime change has mutated `UWTile.FloorHeight`. Deliberately does NOT use the
    /// baked prefab path (mOBakedLevels) like the first load (fBuildGeometry) - that
    /// would ignore the mutation and bring back the original, static
    /// state. Replaces only the "Geometry" child, not the whole level (objects/
    /// player stay untouched).</summary>
    public void RebuildGeometry()
    {
        if (mOLevelRoot == null)
            return;

        Transform lOExisting = mOLevelRoot.transform.Find("Geometry");

        // TAKEN OUT AT ONCE, not only destroyed: Destroy waits for the end of the frame, and a
        // second rebuild in the same frame found the OLD geometry again by name and left the
        // first rebuild standing - over the third. The lever on level 3, 52/13, fires three
        // change terrain traps in a row, and the floor showed the state after the first (per
        // user, 2026-10-01: "the floor stayed visibly at the old height").
        if (lOExisting != null)
        {
            lOExisting.gameObject.SetActive(false);
            lOExisting.SetParent(null, false);
            fDestroyMeshes(lOExisting.gameObject);
            Destroy(lOExisting.gameObject);
        }

        GeometryRevision++;

        UWChunkGeometry[] lOChunks = UWLevelMeshBuilder.Build(CurrentLevel, UWTextureArrayBuilder.GetFloorSliceOffset(UWDataImporter));
        GameObject lORebuilt = UWLevelAssembler.CreateLevelRoot("Geometry", lOChunks, mODungeonMaterial, miLevelIndex, true);
        lORebuilt.transform.SetParent(mOLevelRoot.transform, false);

        // The fresh chunks carry the URP material again. Without this follow-up the
        // WHOLE level falls back to the old render path as soon as a lever or a
        // terrain trap rebuilds the geometry - the material swap itself only runs on
        // a level change (per user, 2026-09-06).
        fApplyRenderModeTo(lORebuilt);

        // A lever or a terrain trap changed heights - the sprite rule reads them too.
        UWOwnTile.SetFloorHeights(CurrentLevel);

        // ... and the map writes what is in view anew (UWLevel.MarkTileVisited).
        UWScene.PlayerTerrain?.InvalidateRenderBand();
    }

    private UWObjectSpawner mORuntimeSpawner;

    /// <summary>
    /// ONE spawner per level build. Before, a new one was created for every dropped item,
    /// every blood stain, every projectile - with its own quad cache, so
    /// a new mesh per object that was never released (per user, 2026-09-13: meshes in the
    /// UWResourceWatch log growing steadily). It is rebuilt when the atlas
    /// or materials have changed.
    /// </summary>
    private UWObjectSpawner fGetRuntimeSpawner()
    {
        if (mORuntimeSpawner == null || !mORuntimeSpawner.Uses(mOObjectAtlas, mOBillboardMaterial,
                mODecalMaterial, mODungeonMaterial, mOTranslucentBillboardMaterial, mOModelSolidMaterial,
                mOPortcullisMaterial))
        {
            if (mORuntimeSpawner != null)
                mORuntimeSpawner.DestroyCachedMeshes();

            mORuntimeSpawner = new UWObjectSpawner(UWDataImporter, mOObjectAtlas, mOBillboardMaterial,
                mODecalMaterial, mODungeonMaterial, UWTextureArrayBuilder.GetFloorSliceOffset(UWDataImporter),
                mO3DModels, mOTranslucentBillboardMaterial, this, mOModelSolidMaterial, mOPortcullisMaterial);
        }

        return mORuntimeSpawner;
    }

    private void fSpawnObjects(Transform pOParent)
    {
        GameObject lOObjectRoot = new GameObject("Objects");
        lOObjectRoot.transform.SetParent(pOParent, false);
        mOObjectRoot = lOObjectRoot;

        // The spawner of this build stays for everything created later - see
        // fGetRuntimeSpawner. The quads of the previous build are released.
        if (mORuntimeSpawner != null)
            mORuntimeSpawner.DestroyCachedMeshes();

        mORuntimeSpawner = null;

        UWObjectSpawner lOSpawner = fGetRuntimeSpawner();

        // The doors register their planes while they are spawned (UWOwnTile.ClearDoors).
        UWOwnTile.ClearDoors();
        lOSpawner.Spawn(CurrentLevel, lOObjectRoot.transform);
        UWOwnTile.ApplyDoors();

        if (lOSpawner.UnresolvedCount > 0)
            Debug.LogWarning(lOSpawner.UnresolvedCount + " objects without texture on level " + (miLevelIndex + 1));

        // The view of this build follows the level's data from now on (UWWorldView).
        if (mOWorldView != null)
            mOWorldView.Detach();

        mOWorldView = new UWWorldView(lOObjectRoot.transform, fGetRuntimeSpawner, fApplyRenderModeTo);
        mOWorldView.Attach(WorldEdit);
        mOWorldView.RebuildRegistry();
    }

    private UWRenderPathHost mORenderPaths;

    /// <summary>
    /// Applies the currently active render path to a freshly built node - the geometry after a
    /// rebuild, and every object picture through UWWorldView, the one exit for those.
    ///
    /// The palette renderer's material swap runs once per level over everything that
    /// exists then (see UWPaletteRenderPath.OnObjectSpawned). Without this follow-up
    /// every object created at runtime stays on the old path - visible because
    /// it is the only thing the player light falls on.
    /// </summary>
    private void fApplyRenderModeTo(GameObject pOBuilt)
    {
        if (pOBuilt == null)
            return;

        if (mORenderPaths == null)
            mORenderPaths = GetComponent<UWRenderPathHost>();

        if (mORenderPaths != null)
            mORenderPaths.OnObjectSpawned(pOBuilt);
    }

    /// <summary>For UWTriggerSystem (door trap: find the door GameObject for a UWObject found
    /// via the target tile, without a player click hit) - UWWorldView's registry.</summary>
    public bool TryGetEntity(UWObject pOData, out UWEntityInfo pOEntity)
    {
        if (mOWorldView != null)
            return mOWorldView.TryGetEntity(pOData, out pOEntity);

        pOEntity = null;
        return false;
    }

    /// <summary>For UWInventory (original: drop/throw an item from the hand) - which
    /// tile lies under a world position (e.g. the player position itself).</summary>
    public UWTilePos WorldPositionToTile(Vector3 pOWorldPosition)
    {
        return UWTileQueries.WorldToTile(pOWorldPosition.x, pOWorldPosition.z);
    }

    /// <summary>
    /// Places a new object of this kind in the centre of a tile. Needed for things that
    /// only come into being during play and for which there is no template in the object list - the
    /// remains of a killed creature, for example.
    /// </summary>
    /// <param name="piQuantity">Count for a stack, or zero for CreateObj's own rule (a stack
    /// of one for a stackable class, else none - UWObjectFactory). A quantity field only holds
    /// what has the quantity bit set - see UWObject.HasQuantity.</param>
    /// <param name="piQuality">Condition from zero to 63, or minus one for CreateObj's 40
    /// (since 2026-10-12; before that minus one left it at 0).</param>
    /// <param name="peArrival">Where in the tile's list it goes (UWWorldEdit.Arrival).</param>
    public bool SpawnObjectById(int piObjectId, int piTileX, int piTileZ, UWWorldEdit.Arrival peArrival,
        int piQuantity = 0, int piQuality = -1)
    {
        return SpawnObjectById(piObjectId, piTileX, piTileZ, piQuantity, piQuality, null, peArrival);
    }

    /// <summary>As above, but at a specific world position within the tile - for
    /// blood and remains that lie where the creature died. Null means tile centre.
    /// </summary>
    /// <param name="peArrival">Where in the tile's list it goes (UWWorldEdit.Arrival).</param>
    /// <param name="pOPrepare">Sets fields on the fresh object before it is placed (the planted
    /// silver tree's tenacious bit, for one).</param>
    /// <param name="piInPlaceAt">The list index for UWWorldEdit.Arrival.InPlace.</param>
    public bool SpawnObjectById(int piObjectId, int piTileX, int piTileZ,
        int piQuantity, int piQuality, Vector3? pOWorldPosition, UWWorldEdit.Arrival peArrival,
        System.Action<UWObject> pOPrepare = null, int piInPlaceAt = -1)
    {
        UWObject lOObject = fBuildObject(piObjectId, piQuantity, piQuality);

        if (lOObject == null)
            return false;

        pOPrepare?.Invoke(lOObject);

        // Centre of the tile: the sub-tile fields of a freshly built object are 0, and
        // that would be the tile corner.
        lOObject.XPos = 3;
        lOObject.YPos = 3;
        lOObject.ZPos = CurrentLevel.GetTile(piTileX, piTileZ).FloorHeight / 2;

        if (pOWorldPosition.HasValue)
            return CurrentLevel != null && mOObjectRoot != null
                && fSpawnObjectAt(lOObject, piTileX, piTileZ, pOWorldPosition.Value, peArrival, false,
                    piInPlaceAt: piInPlaceAt);

        return SpawnObjectInTile(lOObject, piTileX, piTileZ, peArrival);
    }

    /// <summary>A new object of this kind with its picture, not yet in the world, or null without
    /// data - the same object SpawnObjectByIdAround puts down (a dead creature's loot, rolled into
    /// its chain before the spill since cleanup step 4.12b, 2026-10-19; UWCritterRemains).</summary>
    public UWObject BuildObject(int piObjectId, int piQuantity, int piQuality)
    {
        return fBuildObject(piObjectId, piQuantity, piQuality);
    }

    /// <summary>A new object of this kind with its picture, or null without data.</summary>
    private UWObject fBuildObject(int piObjectId, int piQuantity, int piQuality)
    {
        if (UWDataImporter == null || CurrentLevel == null)
            return null;

        // CreateObj (UWObjectFactory): a caller's count or quality is kept, anything else is
        // CreateObj's own - quality 40 and the stack rule of COMOBJ. Until 2026-10-12 a quality
        // of minus one meant 0 and a count of 0 no quantity bit, so remains, bones, debris, the
        // moongate and the Vas rune came into being at quality 0 and without the bit the
        // original gives their classes.
        UWObject lOObject = UWObjectFactory.Create(piObjectId, UWDataImporter.CommonObjectProperties,
            piQuantity > 0 ? piQuantity : UWObjectFactory.Unset, piQuality >= 0 ? piQuality : UWObjectFactory.Unset);

        // AN ANIMATED OBJECT CARRIES ITS FRAME IN THE OWNER FIELD: the original's add_animobj
        // writes the class's first ANIMO.GR frame there when it starts the animation, and its
        // renderer draws that frame. Ours draws from the table, but a save must hold the number:
        // a silver tree planted here and loaded in the original showed frame 0, the smoke (per
        // user, 2026-10-11, SAVE4 against the original's SAVE3).
        UWObjectClassProperties.AnimationObject lOAnimation;

        if (UWDataImporter.ObjectClassProperties != null
            && UWDataImporter.ObjectClassProperties.TryGetAnimationObject(piObjectId, out lOAnimation))
            lOObject.Owner = lOAnimation.StartFrame;

        return UWObjectAppearance.Apply(lOObject, UWDataImporter.Textures, CurrentLevel.TextureInfos) ? lOObject : null;
    }

    /// <summary>A new object of this kind put down around a point - see
    /// SpawnScatteredAround.</summary>
    public bool SpawnObjectByIdAround(int piObjectId, int piQuantity, int piQuality, Vector3 pOCentre,
        int piRadiusEighths, UWTilePos? pOFallbackTile = null, int piOwner = -1)
    {
        UWObject lOObject = fBuildObject(piObjectId, piQuantity, piQuality);

        // An owner given (the kind of a corpse, build_corpse) goes into word 3 bits 0-5.
        if (lOObject != null && piOwner >= 0)
            lOObject.Owner = (ushort)(piOwner & 0x3F);

        return lOObject != null && SpawnScatteredAround(lOObject, pOCentre, piRadiusEighths, pOFallbackTile);
    }

    /// <summary>
    /// Puts an object down AROUND a point as the original spills a creature's loot and remains
    /// (UWScatterRules): up to 24 spots within the radius in eighths, each axis rolled on its
    /// own, the first that fits - which may lie in a neighbouring tile; if none fits, the
    /// culling test either takes the object or it lies on the point itself. Returns false when
    /// the object is gone.
    ///
    /// FITS here is the tile part of the original's test: inside the level, not solid, in the
    /// open half of a diagonal, and a floor no higher than the point (a step up refuses it).
    ///
    /// pOFallbackTile: where the object goes - the middle of that tile - if even the point itself
    /// is refused. The original has no such case (put_at adds the object at the point whatever
    /// lies there); ours can meet it when a body's position stands over a wall's tile. Until
    /// 2026-10-12 a random spot of the tile was rolled there, a roll the original never makes
    /// (per user: "as in the original").
    ///
    /// pbStayCentered, pbNoCull: drop_around_place's stay_centered (the point is tried first)
    /// and put_at's nocull (the object is never culled) - place_object at the player's feet.
    ///
    /// THE ORDER IN THE LIST (2026-10-18): a spot found joins its tile's list at the tail, as
    /// drop_around_place's Obj_AddEnd; the point itself, when no spot fitted, at the head, as
    /// put_at's Obj_Add (UWWorldEdit.Arrival PutDown, PutAtPoint).
    ///
    /// pbAvoidWorldContainers: a spot must keep the two radii away from any barrel, chest or
    /// nightstand in its tile - the one piece of the original's object collision taken over
    /// (per user, 2026-09-16: nothing from a spilled barrel comes to rest on the next one).
    ///
    /// pbFitsLikeCanPlace: the whole of drop_around_place's test, can_place against every solid
    /// object (ObjectFitsAt) at piZ - for the rock hammer's pieces, where the original's test
    /// decides how many survive (per user on the original 2026-10-15: "often no piece at all,
    /// it seems to depend on the room").
    /// </summary>
    public bool SpawnScatteredAround(UWObject pOObject, Vector3 pOCentre, int piRadiusEighths,
        UWTilePos? pOFallbackTile = null, bool pbAvoidWorldContainers = false, bool pbStayCentered = false,
        bool pbNoCull = false, bool pbFitsLikeCanPlace = false, int piZ = 0)
    {
        if (pOObject == null || CurrentLevel == null || mOObjectRoot == null)
            return false;

        int liCentreX = UWTileQueries.WorldToEighths(pOCentre.x);
        int liCentreY = UWTileQueries.WorldToEighths(pOCentre.z);

        int liX;
        int liY;

        // The rolls and the tile fit are UWPlacement's (engine-free since 2026-10-12); only the
        // container clearance, which reads the spawned bodies, stays here.
        UWWorldEdit.Arrival leArrival = UWWorldEdit.Arrival.PutDown;

        if (!UWPlacement.TryScatter(TileQueries, liCentreX, liCentreY, piRadiusEighths, fGetObjectRadius(pOObject.ID),
            pOCentre.y, fScatterAlsoFits(pOObject.ID, pbAvoidWorldContainers, pbFitsLikeCanPlace, piZ),
            out liX, out liY, pbStayCentered))
        {
            if (!pbNoCull && TileQueries.Culls(pOObject.ID, UWStackRules.Count(pOObject)))
                return false;

            leArrival = UWWorldEdit.Arrival.PutAtPoint;
        }

        Vector3 lOSpot = new Vector3(UWUnits.EighthsToWorldAxis(liX), pOCentre.y, UWUnits.EighthsToWorldAxis(liY));

        if (fSpawnObjectAt(pOObject, liX >> 3, liY >> 3, lOSpot, leArrival, false))
            return true;

        return pOFallbackTile.HasValue
            && SpawnObjectInTile(pOObject, pOFallbackTile.Value.X, pOFallbackTile.Value.Y, UWWorldEdit.Arrival.PutAtPoint);
    }

    /// <summary>The player's tile, the centre of the culling (UWWorldEdit.PlayerTile); far off
    /// the map without a character.</summary>
    private UWTilePos fPlayerTile()
    {
        return UWScene.Character != null
            ? WorldPositionToTile(UWScene.Character.transform.position)
            : new UWTilePos(-100, -100);
    }

    /// <summary>What the player carries, counted as the original counts it against the level's
    /// static slots (UWObjectLimitRules.MakeRoomForInventory): equipment, backpack and what hangs
    /// at the pointer, with the contents of containers.</summary>
    private static int fCarriedObjectCount()
    {
        UWInventory lOInventory = UWScene.Inventory;

        if (lOInventory == null || lOInventory.Model == null)
            return 0;

        UWInventoryModel lOModel = lOInventory.Model;
        List<UWObject> lORoots = new List<UWObject>(lOModel.EquipSlots);

        lORoots.AddRange(lOModel.Backpack);
        lORoots.Add(lOModel.CursorItem);

        return UWObjectLimitRules.CountObjects(lORoots);
    }

    /// <summary>
    /// The carried objects as one chain for a level change: the order the save game writer uses
    /// (backpack, then equipment), the object in the hand last as in ovr118_573. The original's
    /// own chain order after items were added during play is not followed (UWCarriedSlots).
    /// </summary>
    private List<UWObject> fCarriedChain()
    {
        List<UWObject> lOChain = new List<UWObject>();
        UWInventory lOInventory = UWScene.Inventory;

        if (lOInventory == null || lOInventory.Model == null)
            return lOChain;

        UWInventoryModel lOModel = lOInventory.Model;

        foreach (UWObject lOObject in lOModel.Backpack)
        {
            if (lOObject != null)
                lOChain.Add(lOObject);
        }

        foreach (UWObject lOObject in lOModel.EquipSlots)
        {
            if (lOObject != null)
                lOChain.Add(lOObject);
        }

        if (lOModel.CursorItem != null)
            lOChain.Add(lOModel.CursorItem);

        return lOChain;
    }

    /// <summary>
    /// The inventory arrives in the level - on loading a game and on entering another level
    /// (UWLevelChangeRules.Arrive): the carried objects take their slots in the order of the
    /// chain, then, if level and inventory together exceed the static slots, far objects are
    /// culled in steps of ten until they fit (UWObjectLimitRules.MakeRoomForInventory).
    /// </summary>
    private void fArriveWithInventory(List<UWObject> pOCarriedChain, int piCarriedObjects, UWTilePos pOPlayerTile)
    {
        if (CurrentLevel == null)
            return;

        int liCulled = UWLevelChangeRules.Arrive(WorldEdit, pOCarriedChain, piCarriedObjects, pOPlayerTile.X, pOPlayerTile.Y);

        // RestorePlayerInv ends with FixPlayerEquips - a worn curse strikes on arrival
        // (UWCharacter.OnStatusPassDue, 2026-10-12).
        UWCharacter lOCharacter = UWScene.Character;

        if (lOCharacter != null)
            lOCharacter.OnStatusPassDue();

        if (liCulled > 0)
            Debug.Log(string.Format("Level {0}: {1} carried objects arrived in a full level, culled {2}",
                CurrentLevel.LevelNumber, piCarriedObjects, liCulled));
    }

    /// <summary>Whether a spot in eighths keeps clear of the world containers in its tile - see
    /// SpawnScatteredAround. The footprint's tile fit is UWPlacement.FootprintFits.</summary>
    private bool fClearOfWorldContainers(int piX, int piY, int piObjectId)
    {
        Vector3 lOSpot = new Vector3(UWUnits.EighthsToWorldAxis(piX), 0f, UWUnits.EighthsToWorldAxis(piY));

        return fGetDistanceToWorldContainer(piX >> 3, piY >> 3, lOSpot) >= fGetContainerClearance(piObjectId);
    }

    /// <summary>The radius of the item plus that of a container, in world units (the same
    /// measure as fGetTriggerReach uses for the player).</summary>
    private float fGetContainerClearance(int piObjectId)
    {
        int liItem = UWPlacement.PlacementRadius(TileQueries, piObjectId);
        int liContainer = UWPlacement.PlacementRadius(TileQueries, UWObjectMechanics.BarrelObjectId);

        // In eighths of a tile, the unit comobj.dat stores radii in.
        return (liItem + liContainer) * UWWorldScale.SubTileStep;
    }

    /// <summary>Distance to the nearest barrel, chest or nightstand in this tile, measured
    /// horizontally. Huge if there is none.</summary>
    private float fGetDistanceToWorldContainer(int piTileX, int piTileZ, Vector3 pOSpot)
    {
        if (CurrentLevel == null || CurrentLevel.TileData == null)
            return float.MaxValue;

        UWTile lOTile = CurrentLevel.GetTile(piTileX, piTileZ);

        if (lOTile == null || lOTile.ObjectsInTile == null)
            return float.MaxValue;

        float lfNearest = float.MaxValue;

        foreach (UWObject lOOther in lOTile.ObjectsInTile)
        {
            if (lOOther == null || !UWObjectMechanics.IsWorldContainer(lOOther.ID))
                continue;

            Vector3 lOAt = UWViewpoint.SubTileToWorld(piTileX, piTileZ, lOOther);

            float lfDistance = new Vector2(lOAt.x - pOSpot.x, lOAt.z - pOSpot.z).magnitude;

            if (lfDistance < lfNearest)
                lfNearest = lfDistance;
        }

        return lfNearest;
    }

    /// <summary>
    /// Pulls a sub-tile spot (eighths 0 to 7) piRadius eighths away from the edges
    /// behind which something is in the way. The ONE place for this rule - before, it was in
    /// UWWorldSync for creatures and a second time here for loot (per user, 2026-09-13:
    /// "Didn't we already have that rule somewhere else?").
    ///
    /// In the way is always a solid tile or the edge of the map. With pbIncludeSteps also
    /// a neighbour whose floor at the shared edge lies HIGHER than its own - a
    /// stair step or a ledge. Otherwise loot lay half inside the step (per user,
    /// 2026-09-13: "otherwise the items lie in stairs or higher ground"). Slopes are measured at
    /// the edge, so a ramp that joins flush does not count.
    ///
    /// If a corner reaches into a blocked diagonal neighbour, the spot moves aside on the
    /// X axis (like the original does for creatures, see UWWorldSync).
    /// </summary>
    public void KeepClearOfEdges(int piTileX, int piTileZ, ref int piX, ref int piY, int piRadius, bool pbIncludeSteps)
    {
        TileQueries.KeepClearOfEdges(piTileX, piTileZ, ref piX, ref piY, piRadius, pbIncludeSteps);
    }

    /// <summary>
    /// For a_create_object trap: the copy of the template keeps the template's position in the tile and HEIGHT
    /// and only moves to the new tile. That is what UW.EXE does (TriggerCreateObjectTrap,
    /// case 7: CopyArray, then MoveObjectToCoordinates with tile times 8 plus xpos/ypos of the
    /// template and its zpos). The reference rolls the spot anew (GetRandomXYZForTile)
    /// and puts everything on the floor - it deviates there. Noticed with Garamon's ghost, whose
    /// template stands at ZPos 24 and floats in the original (per user, 2026-09-13). Nothing ends up
    /// below the floor.
    ///
    /// AT THE TAIL of the tile's list (UWWorldEdit.Arrival.PutDown): the trap's copy goes through
    /// put_at with stay_centered, whose first spot is this one (drop_around_place, Obj_AddEnd), and
    /// the conversation's place_object at a square appends as well (BABLHACK.C, Obj_AddEnd).
    /// </summary>
    public bool SpawnObjectInTileAtTemplatePosition(UWObject pOObject, int piTileX, int piTileZ)
    {
        if (pOObject == null || CurrentLevel == null || mOObjectRoot == null)
            return false;

        int liTemplateZ = pOObject.ZPos;

        Vector3 lOPosition = new Vector3(
            UWUnits.SubTileToWorldAxis(piTileX, pOObject.XPos),
            0f,
            UWUnits.SubTileToWorldAxis(piTileZ, pOObject.YPos));

        return fSpawnObjectAt(pOObject, piTileX, piTileZ, lOPosition, UWWorldEdit.Arrival.PutDown, false, true,
            liTemplateZ);
    }

    /// <summary>A thing come to rest on the motion core (UWMotionHost.PlaceAtRest): at the
    /// record's eighth and height, as a lying object - the core has already decided that the
    /// liquid did not take it, so no sinking here. peArrival: Landed for the motion core
    /// (mob_to_static's Obj_Add, the head), InPlace with piInPlaceAt for a thing that only
    /// changed its id where it lay.</summary>
    public bool PlaceRestingObject(UWObject pOObject, int piTileX, int piTileZ, UWWorldEdit.Arrival peArrival,
        int piInPlaceAt = -1)
    {
        if (pOObject == null || CurrentLevel == null || mOObjectRoot == null)
            return false;

        Vector3 lOPosition = new Vector3(
            UWUnits.SubTileToWorldAxis(piTileX, pOObject.XPos),
            0f,
            UWUnits.SubTileToWorldAxis(piTileZ, pOObject.YPos));

        return fSpawnObjectAt(pOObject, piTileX, piTileZ, lOPosition, peArrival, false, false, pOObject.ZPos,
            piInPlaceAt);
    }

    /// <summary>Places an object in the centre of a tile - for create traps that give a
    /// target tile instead of a world position (see UWTriggerSystem).
    ///
    /// Does not go through SpawnDroppedObject: its path leads through WorldPositionToTile,
    /// and the tile is known here anyway. The original reason - the conversion was off by
    /// half a tile, which is why the first spawned monster got the floor height of the
    /// neighbouring tile (user, 2026-08-29) - has since been fixed at the root,
    /// see WorldPositionToTile.</summary>
    public bool SpawnObjectInTile(UWObject pOObject, int piTileX, int piTileZ, UWWorldEdit.Arrival peArrival)
    {
        if (pOObject == null || CurrentLevel == null || mOObjectRoot == null)
            return false;

        return fSpawnObjectAt(pOObject, piTileX, piTileZ, new Vector3(
            UWUnits.TileCentreToWorldAxis(piTileX),
            0f,
            UWUnits.TileCentreToWorldAxis(piTileZ)), peArrival, false);
    }

    /// <summary>
    /// Makes the objects of a tile visible that were skipped during the level
    /// build.
    ///
    /// The spawner skips SOLID tiles - what is inside a wall cannot be seen.
    /// If a terrain trap opens such a tile, that has to be caught up: behind the
    /// secret wall on level 3 (tile 48/54) there is a secret door that would otherwise stay
    /// invisible (see UWTriggerSystem).
    ///
    /// ONLY to be called for tiles that were solid before - otherwise the already
    /// visible objects would be created a second time. The entries of the tile list stay untouched,
    /// only the display is added.
    /// </summary>
    /// <returns>How many objects were created.</returns>
    public int SpawnPendingObjectsInTile(int piTileX, int piTileZ)
    {
        if (CurrentLevel == null || mOObjectRoot == null)
            return 0;

        UWTile lOTile = CurrentLevel.GetTile(piTileX, piTileZ);

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid
            || lOTile.ObjectsInTile == null || lOTile.ObjectsInTile.Count == 0 || mOWorldView == null)
            return 0;

        return mOWorldView.SpawnTileObjects(piTileX, piTileZ, lOTile);
    }

    /// <summary>
    /// Removes an object from the world for good - from the object list of its tile and
    /// together with its display: the a_delete object trap (see UWTriggerSystem), a pickup
    /// (UWInventory.RemoveWorldObject, folded in on 2026-10-12 - it had its own copy that left
    /// the registry stale), the culling. UWWorldEdit.Remove; true also when only the picture
    /// still held the object.
    /// </summary>
    public bool RemoveObjectFromWorld(UWObject pOObject)
    {
        return WorldEdit.Remove(pOObject, mOWorldView != null && mOWorldView.Holds(pOObject));
    }

    /// <summary>
    /// Like SpawnFlyingObject, but as a real 3D MODEL from uw.exe instead of a flat
    /// sprite - for arrows and crossbow bolts (model 0x08, see
    /// UW3DModelImport.ModelIndex.Arrow).
    ///
    /// The rotation comes from the caller: the model lies along its own Y axis, so a
    /// flying arrow must point this axis in the direction of flight.
    ///
    /// Returns null if there is no uw.exe or the model does not load - then the
    /// caller falls back to the sprite.
    /// </summary>
    /// <summary>
    /// EVERYTHING THAT FLIES IS PUT INTO THE WORLD HERE - the player's shot and throw, a
    /// creature's missile, a spell, an arrow trap. The original creates them all through the
    /// one routine PrepareProjectileObject_seg025_791 (callers: MissileRelease_seg025_9F for
    /// the bow, DropOrThrowByPlayer_seg025_355 for the throw, NPCMissileLaunch_seg025_262 for
    /// a creature, ProjectileSpell_seg025_2B1 for a spell), and a shot arrow looks exactly
    /// like a thrown one there. Ours had the choice in three places and the bow's was the odd
    /// one out, which is why a shot arrow flew as a flat picture with its tip to the left
    /// (per user, 2026-09-20). An arrow or a bolt takes the 3D model aimed along the flight,
    /// everything else the object's own picture.
    /// </summary>
    public GameObject SpawnProjectile(int piObjectId, Vector3 pOAt, Vector3 pODirection)
    {
        GameObject lOFlying = null;

        if ((piObjectId == UWObjectMechanics.ArrowObjectId
                || piObjectId == UWObjectMechanics.CrossbowBoltObjectId)
            && pODirection.sqrMagnitude > 0.0001f)
            lOFlying = SpawnFlyingModel(piObjectId, (int)UWDataImport.UW3DModelImport.ModelIndex.Arrow,
                pOAt, Quaternion.LookRotation(pODirection.normalized));

        return lOFlying != null ? lOFlying : SpawnFlyingObject(piObjectId, pOAt);
    }

    public GameObject SpawnFlyingModel(int piObjectId, int piModelIndex, Vector3 pOWorldPosition, Quaternion pORotation)
    {
        if (UWDataImporter == null || mOObjectRoot == null || mO3DModels == null)
            return null;

        // A view-only stand-in from the factory; it enters no list and no save.
        UWObject lOObject = UWObjectFactory.Create(piObjectId, UWDataImporter);

        if (lOObject == null)
            return null;

        int liBefore = mOObjectRoot.transform.childCount;

        UWObjectSpawner lOSpawner = fGetRuntimeSpawner();

        // THE MODEL NEEDS NO CORRECTION: its arrow points along its own +Z, so a caller's
        // LookRotation of the flight direction is already right. A quarter turn tried here on
        // 2026-09-20 made the tip point to the RIGHT; the sideways tip the user saw came from
        // the bow shot, which was a flat picture at the time (see SpawnProjectile).
        GameObject lOSpawned = lOSpawner.SpawnModelAt(lOObject, piModelIndex, pOWorldPosition,
            pORotation, mOObjectRoot.transform);

        if (lOSpawned == null)
            return null;

        UWEntityInfo lOInfo = lOSpawned.GetComponent<UWEntityInfo>();

        if (lOInfo != null)
        {
            lOInfo.CanBePickedUp = false;
            lOInfo.Description = string.Empty;
        }

        // A projectile should not intercept anything - it checks its path itself (see
        // UWProjectileFlight), and a body of its own would get in the way.
        Collider lOCollider = lOSpawned.GetComponent<Collider>();

        if (lOCollider != null)
            lOCollider.enabled = false;

        if (mOWorldView != null)
            mOWorldView.ApplyRenderModeFrom(liBefore);

        return lOSpawned;
    }

    /// <summary>
    /// Places an object at a world position and returns it, so that the caller can
    /// move it - a spell projectile, for example.
    ///
    /// Like SpawnEffectAt it does NOT belong in the tile's object list and can neither be
    /// picked up nor looked at. Unlike there it does not disappear by itself; whoever
    /// makes it fly takes care of that.
    /// </summary>
    public GameObject SpawnFlyingObject(int piObjectId, Vector3 pOWorldPosition)
    {
        if (UWDataImporter == null || mOObjectRoot == null || CurrentLevel == null)
            return null;

        // A view-only stand-in from the factory; it enters no tile list.
        UWObject lOObject = UWObjectFactory.Create(piObjectId, UWDataImporter);

        if (lOObject == null)
            return null;

        // A projectile does not fall into the lava it starts above - see UWWorldEdit.Place.
        List<GameObject> lONew = fSpawnUnlistedAt(lOObject, pOWorldPosition);

        if (lONew == null)
            return null;

        GameObject lOSpawned = null;

        foreach (GameObject lONode in lONew)
        {
            lOSpawned = lONode;

            lOSpawned.transform.position = pOWorldPosition;

            UWEntityInfo lOInfo = lOSpawned.GetComponent<UWEntityInfo>();

            if (lOInfo != null)
            {
                lOInfo.CanBePickedUp = false;
                lOInfo.Description = string.Empty;
            }
        }

        return lOSpawned;
    }

    /// <summary>
    /// Places a short-lived effect at a world position - a blood splatter on a hit,
    /// for example. The effect disappears by itself after pfSeconds and does not end up in
    /// the tile's object list; it is not an item one could pick up or look
    /// at.
    /// </summary>
    private static readonly int DepthBiasId = UWShaderIds.DepthBias;

    /// <summary>
    /// An effect picture at a world position for some seconds. pfDepthBias, when above 0,
    /// sorts the picture as if it stood that many world units nearer the camera (the billboard
    /// shaders' _DepthBias): the flash on a struck chest stands at the chest's own spot as in
    /// the original, and still draws over the model.
    ///
    /// OFF THE MODEL PATH since 2026-10-12 (decision 8 of Docs/CLEANUP-PLAN.md): the effect holds
    /// a static slot while it lives, as put_effect's CreateObj takes one (UWWorldEdit.
    /// TryTakeEffectSlot), and there is no effect when the level has none free - no culling for
    /// it, no tile list, no liquid swallowing it, no registry entry; UWEffectLifetime gives the
    /// slot back when the picture goes.
    /// </summary>
    public bool SpawnEffectAt(int piObjectId, Vector3 pOWorldPosition, float pfSeconds, float pfDepthBias = 0f,
        int piStartFrame = 0)
    {
        if (UWDataImporter == null || mOObjectRoot == null || CurrentLevel == null || mOWorldView == null)
            return false;

        // A view-only stand-in from the factory, as put_effect makes one with CreateObj
        // (EFFECT.C); it enters no list and no save.
        UWObject lOObject = UWObjectFactory.Create(piObjectId, UWDataImporter);

        UWTilePos lOAt;
        UWTile lOTile;

        if (lOObject == null || !fFindUnlistedTile(pOWorldPosition, out lOAt, out lOTile))
            return false;

        int liToken;

        if (!WorldEdit.TryTakeEffectSlot(out liToken))
            return false;

        fSetUnlistedRecord(lOObject, pOWorldPosition, lOAt, lOTile);

        List<GameObject> lONew = mOWorldView.SpawnUnlisted(lOObject, lOAt.X, lOAt.Y, lOTile, false);

        if (lONew.Count == 0)
            WorldEdit.ReleaseEffectSlot(liToken);

        // Everything that was built gets its expiry time.
        foreach (GameObject lOSpawned in lONew)
        {
            // Bring back to the given height: the record lies at the floor height of its
            // tile, as for a dropped item.
            // A blood splatter belongs where the hit happened, though - in the original
            // it appears on the enemy, not at its feet (per user, 2026-08-30).
            lOSpawned.transform.position = pOWorldPosition;

            UWEntityInfo lOInfo = lOSpawned.GetComponent<UWEntityInfo>();

            // No look, no pickup: a blood splatter is not an item.
            if (lOInfo != null)
            {
                lOInfo.CanBePickedUp = false;
                lOInfo.Description = string.Empty;
            }

            Collider lOCollider = lOSpawned.GetComponent<Collider>();

            if (lOCollider != null)
                lOCollider.enabled = false;

            if (pfDepthBias > 0f)
            {
                foreach (Renderer lORenderer in lOSpawned.GetComponentsInChildren<Renderer>())
                {
                    MaterialPropertyBlock lOBlock = new MaterialPropertyBlock();

                    lORenderer.GetPropertyBlock(lOBlock);
                    lOBlock.SetFloat(DepthBiasId, pfDepthBias);
                    lORenderer.SetPropertyBlock(lOBlock);
                }
            }

            // add_animobj's start picture (a flame of fireball_effect starts on 0 to 2).
            if (piStartFrame > 0)
            {
                UWAnimatedBillboard lOAnimated = lOSpawned.GetComponentInChildren<UWAnimatedBillboard>();

                if (lOAnimated != null)
                    lOAnimated.StartAt(piStartFrame);
            }

            lOSpawned.AddComponent<UWEffectLifetime>().Hold(WorldEdit, liToken);

            Destroy(lOSpawned, pfSeconds);
        }

        return true;
    }

    /// <summary>
    /// For UWInventory (original: drop/throw an item from the hand, see UWItemDrag) -
    /// converts a CONTINUOUS world position into its tile, adds the object there
    /// to the tile object list (so it persists across the next level change) and
    /// immediately creates the visible GameObject, just as in the normal level build
    /// (fSpawnObjects) - only for a single object. ZPos is computed back from the floor height of the
    /// target tile (see UWWorldScale.ZPosStep) - so the object lies
    /// flat on the floor, without throw physics/arc. XPos/YPos (position within the
    /// tile, 0..7) are computed back from THE SAME world position instead of being fixed to the
    /// centre.
    ///
    /// Deliberately continuous instead of "player tile + a step direction snapped to 8
    /// directions": if the player stands close to a tile boundary, "his own
    /// (roughly rounded) tile plus one direction step" deviates slightly from "where the actual
    /// view direction points from his REAL position" - confirmed by a user test on exactly
    /// such a case (player at x=31.56, looking almost south with a slight
    /// west component -> correct would be the diagonal SW neighbour tile, but the tile-step variant
    /// landed on the purely southern neighbour tile).
    ///
    /// Returns false (item stays stuck to the cursor, see UWItemDrag) if the target tile
    /// is solid - a throw into a wall does not simply fall to the floor in front of it. With
    /// pbRequireFlatFloor a sloped tile (slope_n/e/s/w) or a
    /// diagonal tile (half wall, half floor) is also invalid - confirmed from the original:
    /// dropping (not throwing) does not work there, otherwise the object could get stuck in the wall part of the
    /// tile.
    ///
    /// A THROWN item does not come through here any more: it flies on the motion core and
    /// lands via PlaceRestingObject (UWMotionHost.PlaceAtRest, since 2026-10-05).
    ///
    /// peArrival: PutDown for what the player puts down (ReturnObject's Obj_AddEnd, the tail),
    /// Created for what a spell conjures on the spot (creat_spell's Obj_Add, the head).
    /// </summary>
    public bool SpawnDroppedObject(UWObject pOObject, Vector3 pOWorldPosition, bool pbRequireFlatFloor,
        UWWorldEdit.Arrival peArrival, int? piHeading = null, bool pbCanSinkInLiquid = true, bool pbRestOnBridge = false)
    {
        if (pOObject == null || CurrentLevel == null || mOObjectRoot == null)
            return false;

        // A thrown item lies in the view direction - see HeadingFromDirection.
        //
        // ONLY FOR WHAT REALLY NEEDS ITS DIRECTION. For everything else the same field
        // holds what the lore check found out about the item (see
        // UWLoreCheck) - dropping would overwrite that and randomly identify a piece
        // or make it unidentified again. The reference does not set the direction on
        // dropping at all; it only does so for creatures, projectiles and bridges.
        if (piHeading.HasValue
            && !UWLoreCheck.CanBeIdentified(pOObject,
                UWDataImporter != null ? UWDataImporter.CommonObjectProperties : null))
            pOObject.Heading = (ushort)(piHeading.Value & 7);

        UWTilePos lOTargetTile = WorldPositionToTile(pOWorldPosition);

        // Put down by the player: the same wall distance rule as a thrown item - see
        // fTrySnapOutOfRock.
        if (pbRestOnBridge && CurrentLevel != null && CurrentLevel.TileData != null
            && !fTrySnapOutOfRock(ref pOWorldPosition, lOTargetTile.X, lOTargetTile.Y, pOObject.ID))
            return false;

        return fSpawnObjectAt(pOObject, lOTargetTile.X, lOTargetTile.Y, pOWorldPosition, peArrival,
            pbRequireFlatFloor, pbCanSinkInLiquid, pbRestOnBridge ? fGetBridgeZPosBelow(pOWorldPosition) : -1);
    }

    private const int BridgeObjectId = UWObjectMechanics.BridgeObjectId;

    /// <summary>
    /// The zpos of the highest BRIDGE surface below a point, or -1 if there is none.
    ///
    /// A bridge is an object above the tile floor, not part of it, so the floor height alone
    /// put every dropped or landing item under it (per user, 2026-09-14). Searched with a ray
    /// down from the point and only bridge bodies count - otherwise an item would come to rest
    /// on top of another item's pick-up box. Rounded up, so it lies on the planks, not in them.
    /// </summary>
    private int fGetBridgeZPosBelow(Vector3 pOFrom)
    {
        RaycastHit[] lOHits = Physics.RaycastAll(pOFrom + Vector3.up, Vector3.down,
            UWWorldScale.CeilingHeight, ~0, QueryTriggerInteraction.Ignore);

        float lfBest = float.MinValue;

        foreach (RaycastHit lOHit in lOHits)
        {
            UWEntityInfo lOInfo = lOHit.collider.GetComponentInParent<UWEntityInfo>();

            if (lOInfo == null || lOInfo.ObjectData == null || lOInfo.ObjectData.ID != BridgeObjectId)
                continue;

            if (lOHit.point.y > lfBest)
                lfBest = lOHit.point.y;
        }

        return lfBest > float.MinValue ? Mathf.CeilToInt(lfBest / UWWorldScale.ZPosStep) : -1;
    }

    /// <summary>
    /// Converts a world direction into the eight headings of the original.
    ///
    /// Heading 0 is north, counted clockwise - the same count with which
    /// the object build rotates a model (UWObjectSpawner: heading times 45 degrees) and with which
    /// the arrow trap aims.
    /// </summary>
    public static int HeadingFromDirection(Vector3 pODirection)
    {
        Vector3 lOFlat = new Vector3(pODirection.x, 0f, pODirection.z);

        if (lOFlat.sqrMagnitude < 0.0001f)
            return 0;

        float lfYaw = Mathf.Atan2(lOFlat.x, lOFlat.z) * Mathf.Rad2Deg;

        return UWHeadings.DegreesToEighth(lfYaw);
    }

    /// <summary>
    /// Whether a point lies in rock: in a solid tile, in the solid half of a diagonal tile, or
    /// outside the level. Answered from the tile data, not from the collision meshes - a
    /// sweep ignores surfaces it already touches, and a thrown item that slipped through a
    /// diagonal wall that way fell out of the level (per user, 2026-09-15).
    ///
    /// pONormal: the face of the rock the point came through from pOFrom, horizontal - the
    /// diagonal itself, or the side of the solid tile that was crossed.
    /// </summary>
    public bool IsInsideRock(Vector3 pOPoint, Vector3 pOFrom, out Vector3 pONormal)
    {
        pONormal = Vector3.zero;

        if (CurrentLevel == null || CurrentLevel.TileData == null)
            return false;

        // The floor, as the original takes a tile (pos >> 8) and as WorldToTile does - NOT
        // clamped: outside the map there is no tile, and that counts as rock below. Until
        // 2026-10-12 this rounded (decision 20 of Docs/CLEANUP-PLAN.md).
        int liX = UWUnits.WorldAxisToTile(pOPoint.x);
        int liZ = UWUnits.WorldAxisToTile(pOPoint.z);

        UWTilePos lOFrom = WorldPositionToTile(pOFrom);

        // Outside the level (no tile) counts as rock, like a solid tile.
        UWTile lOTile = CurrentLevel.GetTile(liX, liZ);

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
        {
            pONormal = fCrossedSide(lOFrom, liX, liZ);

            return true;
        }

        // Position in the tile from -1 to 1, centre at zero, +z is north.
        float lfX = (pOPoint.x - UWUnits.TileCentreToWorldAxis(liX)) / UWWorldScale.TileHalfSize;
        float lfZ = (pOPoint.z - UWUnits.TileCentreToWorldAxis(liZ)) / UWWorldScale.TileHalfSize;

        // The open half is named by the type - the floor triangle lies there (UWLevelMeshBuilder).
        Vector3 lOOpen;

        switch (lOTile.TileType)
        {
            case UWTile.TileTypeEnum.diagonal_ne: lOOpen = new Vector3(1f, 0f, 1f); break;
            case UWTile.TileTypeEnum.diagonal_nw: lOOpen = new Vector3(-1f, 0f, 1f); break;
            case UWTile.TileTypeEnum.diagonal_se: lOOpen = new Vector3(1f, 0f, -1f); break;
            case UWTile.TileTypeEnum.diagonal_sw: lOOpen = new Vector3(-1f, 0f, -1f); break;
            default: return false;
        }

        if ((lfX * lOOpen.x) + (lfZ * lOOpen.z) >= 0f)
            return false;

        pONormal = lOOpen.normalized;

        return true;
    }

    /// <summary>
    /// Puts a world position onto the grid point the item will be stored at (fGetSubTilePos), so
    /// that it keeps the WALL DISTANCE RULE (KeepClearOfEdges, item radius, at least one eighth,
    /// steps included - the same as scattered loot) and does not lie in rock (the solid half of a
    /// diagonal tile). If the pulled-in point is still in rock, the nearest grid point of the tile
    /// that satisfies both is taken. Returns false if the tile has none.
    /// </summary>
    private bool fTrySnapOutOfRock(ref Vector3 pOWorldPosition, int piTileX, int piTileZ, int piObjectId)
    {
        int liRadius = UWPlacement.PlacementRadius(TileQueries, piObjectId);

        int liGridX = fGetSubTilePos(pOWorldPosition.x, piTileX);
        int liGridZ = fGetSubTilePos(pOWorldPosition.z, piTileZ);

        KeepClearOfEdges(piTileX, piTileZ, ref liGridX, ref liGridZ, liRadius, true);

        Vector3 lONormal;
        Vector3 lOSnapped = fGridPointToWorld(liGridX, liGridZ, piTileX, piTileZ, pOWorldPosition.y);

        if (!IsInsideRock(lOSnapped, lOSnapped, out lONormal))
        {
            pOWorldPosition = lOSnapped;

            return true;
        }

        bool lbFound = false;
        float lfBest = float.MaxValue;
        Vector3 lOBest = pOWorldPosition;

        for (int liX = 0; liX < 8; liX++)
        {
            for (int liZ = 0; liZ < 8; liZ++)
            {
                int liClearX = liX;
                int liClearZ = liZ;

                KeepClearOfEdges(piTileX, piTileZ, ref liClearX, ref liClearZ, liRadius, true);

                if (liClearX != liX || liClearZ != liZ)
                    continue;

                Vector3 lOAt = fGridPointToWorld(liX, liZ, piTileX, piTileZ, pOWorldPosition.y);

                if (IsInsideRock(lOAt, lOAt, out lONormal))
                    continue;

                float lfDistance = (lOAt - pOWorldPosition).sqrMagnitude;

                if (lfDistance < lfBest)
                {
                    lfBest = lfDistance;
                    lOBest = lOAt;
                    lbFound = true;
                }
            }
        }

        pOWorldPosition = lOBest;

        return lbFound;
    }

    /// <summary>The world point of a sub-tile grid position - the inverse of fGetSubTilePos, on
    /// the one eighth-spot formula. Until 2026-10-12 this added the spawner's 0.1 draw offset
    /// (the +3.85 copy); fGetSubTilePos rounds to the nearest spot, so the stored position is
    /// the same without it, and the drawn body gets its offset from the spawner alone.</summary>
    private static Vector3 fGridPointToWorld(int piX, int piZ, int piTileX, int piTileZ, float pfHeight)
    {
        return new Vector3(
            UWUnits.SubTileToWorldAxis(piTileX, piX),
            pfHeight,
            UWUnits.SubTileToWorldAxis(piTileZ, piZ));
    }

    /// <summary>The side of tile (piX, piZ) that faces the tile the point came from.</summary>
    private static Vector3 fCrossedSide(UWTilePos pOFrom, int piX, int piZ)
    {
        int liDx = piX - pOFrom.X;
        int liDz = piZ - pOFrom.Y;

        if (Mathf.Abs(liDx) >= Mathf.Abs(liDz) && liDx != 0)
            return new Vector3(-Mathf.Sign(liDx), 0f, 0f);

        if (liDz != 0)
            return new Vector3(0f, 0f, -Mathf.Sign(liDz));

        return Vector3.zero;
    }

    /// <summary>
    /// Whether an item may be put down at this spot - the tile checks of
    /// SpawnDroppedObject without spawning anything, so that the caller can still change the
    /// item (put out a light) before it appears in the world.
    /// </summary>
    public bool CanDropAt(Vector3 pOWorldPosition, bool pbRequireFlatFloor)
    {
        if (CurrentLevel == null || CurrentLevel.TileData == null)
            return false;

        UWTilePos lOTile = WorldPositionToTile(pOWorldPosition);
        UWTile lOAt = CurrentLevel.GetTile(lOTile.X, lOTile.Y);

        if (lOAt == null || lOAt.TileType == UWTile.TileTypeEnum.solid)
            return false;

        return !pbRequireFlatFloor || !fIsUnevenFloor(lOAt.TileType);
    }

    /// <summary>Shared core of SpawnDroppedObject, SpawnObjectInTile and the other Spawn/Drop
    /// methods that put an object into a tile list (SpawnObjectById, SpawnScatteredAround,
    /// SpawnObjectInTileAtTemplatePosition, PlaceRestingObject). The target tile
    /// is PASSED IN instead of being determined here from the world position: whoever already
    /// knows the tile should not have to recompute it via a round trip (see
    /// SpawnObjectInTile). The fine position and the height come from the world position here;
    /// the slot, the lists and the liquid are UWWorldEdit.Place's, the picture UWWorldView's; where
    /// in the tile's list it goes is peArrival's (UWWorldEdit.Arrival, piInPlaceAt for InPlace).</summary>
    private bool fSpawnObjectAt(UWObject pOObject, int liTileX, int liTileZ, Vector3 pOWorldPosition,
        UWWorldEdit.Arrival peArrival, bool pbRequireFlatFloor, bool pbCanSinkInLiquid = true, int piMinimumZPos = -1,
        int piInPlaceAt = -1)
    {
        UWTile lOTile = CurrentLevel.GetTile(liTileX, liTileZ);

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
            return false;

        if (pbRequireFlatFloor && fIsUnevenFloor(lOTile.TileType))
            return false;

        int liZPos = UWUnits.WorldToZPos(fGetFloorHeightAt(lOTile, pOWorldPosition.x, pOWorldPosition.z, liTileX, liTileZ));

        // Keep a template's height if it lies above the floor (see
        // SpawnObjectInTileAtTemplatePosition), or a bridge's (see fGetBridgeZPosBelow).
        bool lbAboveFloor = piMinimumZPos > liZPos;

        if (lbAboveFloor)
            liZPos = piMinimumZPos;

        // Nothing sinks that lies ABOVE the floor - on a bridge over the water it stays dry.
        return WorldEdit.Place(pOObject, liTileX, liTileZ, fGetSubTilePos(pOWorldPosition.x, liTileX),
            fGetSubTilePos(pOWorldPosition.z, liTileZ), liZPos, pbCanSinkInLiquid && !lbAboveFloor, peArrival,
            piInPlaceAt) != UWWorldEdit.PlaceResult.Refused;
    }

    /// <summary>
    /// The picture of a flying projectile at a world position: a slot as for a placed object
    /// (UWWorldEdit.ReserveSlot, culling in a full level), the record's place as fSpawnObjectAt
    /// gives it, but no tile list, no animation list and so no save; the picture is drawn as if
    /// appended to its tile (UWWorldView.SpawnUnlisted) and registered. It does not sink - it
    /// does not lie there. Null when the point lies in a solid tile or no slot was free.
    /// </summary>
    private List<GameObject> fSpawnUnlistedAt(UWObject pOObject, Vector3 pOWorldPosition)
    {
        UWTilePos lOAt;
        UWTile lOTile;

        if (pOObject == null || mOWorldView == null || !fFindUnlistedTile(pOWorldPosition, out lOAt, out lOTile))
            return null;

        if (!WorldEdit.ReserveSlot(pOObject))
            return null;

        fSetUnlistedRecord(pOObject, pOWorldPosition, lOAt, lOTile);

        return mOWorldView.SpawnUnlisted(pOObject, lOAt.X, lOAt.Y, lOTile, true);
    }

    /// <summary>The tile under a world point, unless it is solid or off the level.</summary>
    private bool fFindUnlistedTile(Vector3 pOWorldPosition, out UWTilePos pOAt, out UWTile pOTile)
    {
        pOAt = WorldPositionToTile(pOWorldPosition);
        pOTile = CurrentLevel != null ? CurrentLevel.GetTile(pOAt.X, pOAt.Y) : null;

        return pOTile != null && pOTile.TileType != UWTile.TileTypeEnum.solid;
    }

    /// <summary>A view-only stand-in's record: its tile, the nearest eighth spot and the floor
    /// height there, as fSpawnObjectAt gives a placed object.</summary>
    private void fSetUnlistedRecord(UWObject pOObject, Vector3 pOWorldPosition, UWTilePos pOAt, UWTile pOTile)
    {
        pOObject.TileX = pOAt.X;
        pOObject.TileY = pOAt.Y;
        pOObject.XPos = fGetSubTilePos(pOWorldPosition.x, pOAt.X);
        pOObject.YPos = fGetSubTilePos(pOWorldPosition.z, pOAt.Y);
        pOObject.ZPos = UWUnits.WorldToZPos(fGetFloorHeightAt(pOTile, pOWorldPosition.x, pOWorldPosition.z, pOAt.X, pOAt.Y));
    }

    /// <summary>How long the splash picture stays. The original spawns the class-7 object with
    /// an animation duration of 3 and has no seconds anywhere; the port has no frame timing
    /// for effects, so this is the length of the blow effects.</summary>
    public const float SplashEffectSeconds = 0.6f;

    /// <summary>
    /// Whether this tile holds water.
    ///
    /// The tile data only know their floor texture; which texture is water is stored in
    /// TERRAIN.DAT (uw-formats.txt 4.9, value 0x0010). In UW1 these are floor textures
    /// 16 and 17.
    /// </summary>
    public bool IsWaterTile(UWTile pOTile)
    {
        return TileQueries.IsWaterTile(pOTile);
    }

    /// <summary>Whether this tile carries lava - TERRAIN.DAT value 0x0020, in UW1 the
    /// floor textures 23 to 25 on levels 6 to 9.</summary>
    public bool IsLavaTile(UWTile pOTile)
    {
        return TileQueries.IsLavaTile(pOTile);
    }

    /// <summary>Floor that can swallow a dropped item.</summary>
    public bool IsDestructiveTile(UWTile pOTile)
    {
        return IsWaterTile(pOTile) || IsLavaTile(pOTile);
    }

    /// <summary>
    /// Floor height under a world position, correctly interpolated on a slope.
    ///
    /// Public version of fGetFloorHeightAt for everyone who only has a point and
    /// no tile: the save game writer, for example (see
    /// UWSavegameWriter.Save). If the point lies outside the level or on a
    /// solid tile, zero is returned.
    /// </summary>
    public float GetFloorHeightAt(Vector3 pOWorldPosition)
    {
        return TileQueries.FloorHeightAt(pOWorldPosition.x, pOWorldPosition.z);
    }

    /// <summary>Inverse of the object placement in UWObjectSpawner.fGetObjectPosition
    /// (UWViewpoint.SubTileToWorld plus the 0.1 draw offset), i.e. the
    /// "tile*TileSize - TileHalfSize + 0.1 + subPos*SubTileStep + SubTileOffset" formula: which of the 8
    /// fixed grid spots (0..7) within the tile is closest to a world
    /// coordinate.</summary>
    private static ushort fGetSubTilePos(float pfWorldCoordinate, int piTileIndex)
    {
        return UWTileQueries.WorldToSubTile(pfWorldCoordinate, piTileIndex);
    }

    /// <summary>
    /// Floor height at a specific world position within a tile - on a
    /// slope (slope_n/e/s/w) the real height varies between FloorHeight (low
    /// edge) and FloorHeight+Slope (high edge), linearly interpolated across the tile,
    /// just as UWLevelMeshBuilder.fCreateVertex does for the floor mesh itself. A
    /// thrown item that is simply placed at the flat FloorHeight
    /// would otherwise be stuck in the floor on a slope depending on its landing position - confirmed from the
    /// original: it must lie correctly on the slope.
    /// </summary>
    private static float fGetFloorHeightAt(UWTile pOTile, float pfWorldX, float pfWorldZ, int piTileX, int piTileZ)
    {
        return UWTileQueries.FloorHeightAt(pOTile, pfWorldX, pfWorldZ, piTileX, piTileZ);
    }

    private static bool fIsUnevenFloor(UWTile.TileTypeEnum peType)
    {
        return UWTileQueries.IsUnevenFloor(peType);
    }

    /// <summary>
    /// Creates trigger volumes for the move triggers, level transitions included (see the
    /// comment in the loop). The GameObject node is still called "Transitions".
    ///
    /// The teleport trap itself always lies on a solid tile, on which the player
    /// cannot stand at all. It becomes reachable via a trigger object that sits in a
    /// walkable neighbouring tile and points to the trap via its special link.
    /// So the volume belongs on the trigger's tile, not on the trap's.
    ///
    /// Iteration runs over the tiles and not over the master list: that also contains
    /// free list slots with random values, which would otherwise pass as transitions.
    /// </summary>
    private void fSpawnTransitions(Transform pOParent)
    {
        GameObject lORoot = new GameObject("Transitions");
        lORoot.transform.SetParent(pOParent, false);

        UWLevel lOLevel = CurrentLevel;
        System.Collections.Generic.List<UWObject> lOMaster = lOLevel.Masterlist;
        int liCount = 0;

        for (int z = 0; z < UWWorldScale.TilesPerAxis; z++)
        {
            for (int x = 0; x < UWWorldScale.TilesPerAxis; x++)
            {
                UWTile lOTile = lOLevel.GetTile(x, z);

                for (int i = 0; i < lOTile.ObjectsInTile.Count; i++)
                {
                    UWObject lOTrigger = lOTile.ObjectsInTile[i];

                    if (lOTrigger == null)
                        continue;

                    // CREATE TRAPS get NOTHING here. Until 2026-09-06 there was a
                    // tile volume here that fired on entering - that does not exist in the original.
                    // Whoever has a trigger is addressed through it; the
                    // rest are refilled by the respawn on the player tick (UWTriggerSystem.RespawnCreatures).
                    // ONLY MOVE TRIGGERS get a volume. The filter used to let every trigger
                    // id through (0x1A0 to 0x1BF, so pick-up, use and look triggers too),
                    // which built volumes that UWTriggerSystem.TryFireMoveTrigger rejects
                    // anyway and inflated the count in the log below.
                    if (lOTrigger.ID != UWObjectMechanics.MoveTriggerId)
                        continue;

                    // The special link lies in the Quantity field and refers as an index into
                    // the master list.
                    int liLink = lOTrigger.Quantity;

                    if (liLink <= 0 || liLink >= lOMaster.Count)
                        continue;

                    UWObject lOTrap = lOMaster[liLink];

                    if (lOTrap == null)
                        continue;

                    // ALL move triggers alike, level transitions included: the
                    // teleport trap hangs on a perfectly normal a_move trigger and therefore runs
                    // through the same trap chain as everything else (see
                    // UWTriggerSystem). Until 2026-09-06 it got its own
                    // zone instead (UWLevelTransition), built on the assumption that no
                    // trigger addresses it - but counted up, ALL 62 teleport traps
                    // that lie in a tile have a trigger pointing to them.
                    fCreateMoveTrigger(lORoot.transform, x, z, lOTile, lOTrigger);
                    liCount++;
                }
            }
        }

        Debug.Log(liCount + " transitions on level " + (miLevelIndex + 1));
    }

    /// <summary>
    /// Places a WARD RUNE in a tile - the Rune of Warding spell (see UWSummonSpellRules): the
    /// original's cast_trap_spell, a move trigger and an a_ward trap in the tile's list
    /// (UWTrapRules.CastTrapSpell). Any tile, a wall included. False when the level had no slot.
    ///
    /// NO TRIGGER VOLUME since 2026-10-12: the trigger carries flags 8, so the creatures' motion
    /// sets it off as any move trigger (UWMotionHost.TriggerMove), and the player never
    /// does. Until then the trigger had flags 0 and UWMoveTrigger polled the creatures around it.
    /// </summary>
    public bool SpawnWardRune(int piTileX, int piTileZ)
    {
        if (CurrentLevel == null || WorldEdit == null)
            return false;

        return UWTrapRules.CastTrapSpell(WorldEdit, UWDataImporter != null ? UWDataImporter.CommonObjectProperties : null,
            piTileX, piTileZ, UWObjectMechanics.WardTrapId);
    }

    /// <summary>Trigger volume for a move trigger, level transitions included (see
    /// fSpawnTransitions). The volume sits around the trigger's sub-tile point, not over the
    /// whole tile - see fCreateTriggerVolume.</summary>
    private void fCreateMoveTrigger(Transform pOParent, int xPos, int zPos, UWTile pOTile, UWObject pOTriggerData)
    {
        GameObject lOObject = fCreateTriggerVolume(pOParent, xPos, zPos, pOTile, pOTriggerData,
            string.Format("Move trigger ({0},{1})", xPos, zPos));

        lOObject.AddComponent<UWMoveTrigger>().Initialise(this, pOTriggerData,
            fGetTriggerReach(pOTriggerData.ID), TileQueries.TriggerReachCells(pOTriggerData.ID));
    }


    /// <summary>
    /// The volume of a move trigger.
    ///
    /// A trigger is a POINT in its tile, not an area. It sits at its
    /// sub-tile position (XPos/YPos, zero to seven each) and carries in comobj.dat a
    /// RADIUS in eighth-tiles - for the a_move trigger that is two, i.e. 16 world units.
    /// The original fires when the player COLLIDES with it, not when he enters the
    /// tile (motion_collisions calls RunTrigger from within the collision, and
    /// only for triggers - traps carry radius zero and cannot be bumped into at all).
    ///
    /// Until 2026-09-06 the zone covered the whole tile. That this is too coarse can be seen
    /// at the level transitions: there the trigger sits visibly offset towards the stairs
    /// (e.g. on level 1 at 27/20 at XPos 5, while the stairs lie to the east).
    /// Exactly that is where the earlier workaround came from of shrinking the transition zone to a narrow
    /// strip on the wall side - the sub-tile position gives the same,
    /// only from the data instead of guessed from the neighbour direction.
    ///
    /// THE PLAYER COUNTS WITH HIS OWN RADIUS. The original checks two boxes for
    /// overlap, separately per axis - so contact happens at a distance less than or
    /// equal to (trigger radius + player radius). The player is object 63 and also carries
    /// radius two in comobj.dat (checked at object slot 1 of all nine levels, in the
    /// shipped data as well as in the save game).
    ///
    /// Two plus two eighth-tiles are 32 units per side, so the zone is exactly
    /// ONE TILE wide - around the trigger point, not around the tile centre. So one cannot
    /// cross the tile without touching the trigger, and where it sits towards the edge
    /// the zone reaches one eighth-tile into the neighbouring tile.
    ///
    /// The box only serves as a pre-filter - Unity adds the extent of the player body
    /// on top. The exact test is done by UWMoveTrigger itself, with the same calculation as
    /// the original.
    ///
    /// VERTICALLY it stays the full tile height - the trigger should not fail because
    /// the player stands a little higher or lower.
    /// </summary>
    private GameObject fCreateTriggerVolume(Transform pOParent, int xPos, int zPos, UWTile pOTile, UWObject pOTriggerData, string psName)
    {
        GameObject lOObject = new GameObject(psName);
        lOObject.transform.SetParent(pOParent, false);

        // The height here does NOT come from the trigger's zpos, but from the tile floor -
        // the volume should catch the player even when he stands a little higher.
        Vector3 lOAt = UWViewpoint.SubTileToWorld(xPos, zPos, pOTriggerData);

        lOObject.transform.localPosition = new Vector3(
            lOAt.x,
            pOTile.FloorHeight + UWWorldScale.TileHalfSize,
            lOAt.z);

        float lfReach = fGetTriggerReach(pOTriggerData.ID);

        BoxCollider lOCollider = lOObject.AddComponent<BoxCollider>();
        lOCollider.isTrigger = true;
        lOCollider.size = new Vector3(lfReach * 2f, UWWorldScale.TileSize, lfReach * 2f);

        return lOObject;
    }

    /// <summary>The player is an object himself - number 63, checked at object slot 1
    /// of all nine levels. His radius is in comobj.dat like everyone else's.</summary>
    public const int PlayerObjectId = UWTileQueries.PlayerObjectId;

    /// <summary>
    /// How far a trigger reaches, per axis and in world units: its own radius plus
    /// that of the player. For the a_move trigger that is (2 + 2) * 8 = 32, i.e. half a
    /// tile in each direction.
    ///
    /// Without an entry it stays at half a tile - better too generous than a
    /// trigger that never fires.
    /// </summary>
    public float GetTriggerReach(int piObjectId)
    {
        return fGetTriggerReach(piObjectId);
    }

    private float fGetTriggerReach(int piObjectId)
    {
        return TileQueries.TriggerReach(piObjectId);
    }

    private int fGetObjectRadius(int piObjectId)
    {
        return TileQueries.ObjectRadius(piObjectId);
    }

    private void fCreateObjectMaterials()
    {
        if (mOObjectAtlas == null)
            return;

        float lfAmbient = fGetAmbientFloor();
        float lfPointLightCap = fGetObjectPointLightCap();

        Shader lOBillboardShader = Shader.Find("UW/Billboard");
        Shader lODecalShader = Shader.Find("UW/Decal");

        if (lOBillboardShader == null || lODecalShader == null)
        {
            Debug.LogError("UW object shaders not found.");
            return;
        }

        mOBillboardMaterial = new Material(lOBillboardShader);

        // Item sprites are not covered by their own tile either - a thrown item does not vanish
        // into the wall in the original (per user, 2026-09-22; see UWOwnTile).
        UWOwnTile.Enable(mOBillboardMaterial);
        UWOwnTile.SetGlobals();
        mOBillboardMaterial.name = "UW Objects";
        mOBillboardMaterial.mainTexture = mOObjectAtlas.Atlas;
        mOBillboardMaterial.SetFloat(UWShaderIds.AmbientFloor, lfAmbient);
        mOBillboardMaterial.SetFloat(UWShaderIds.PointLightCap, lfPointLightCap);

        // Translucent sprites (mist, ghosts - see UWTransparencyTables) use
        // the same shader, only with a different blend state: alpha blending instead of a hard
        // cutoff, no depth writing, and drawn in the transparent queue, so that the
        // wall behind is already there. The cutoff stays just above zero, so that fully
        // transparent pixels are still discarded.
        mOTranslucentBillboardMaterial = new Material(lOBillboardShader);
        mOTranslucentBillboardMaterial.name = "UW Objects (translucent)";
        mOTranslucentBillboardMaterial.mainTexture = mOObjectAtlas.Atlas;
        mOTranslucentBillboardMaterial.SetFloat(UWShaderIds.AmbientFloor, lfAmbient);
        mOTranslucentBillboardMaterial.SetFloat(UWShaderIds.PointLightCap, lfPointLightCap);
        mOTranslucentBillboardMaterial.SetFloat(UWShaderIds.Cutoff, 0.01f);
        mOTranslucentBillboardMaterial.SetFloat(UWShaderIds.SrcBlend, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mOTranslucentBillboardMaterial.SetFloat(UWShaderIds.DstBlend, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mOTranslucentBillboardMaterial.SetFloat(UWShaderIds.ZWrite, 0f);
        mOTranslucentBillboardMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        // THE OWN TILE DOES NOT COVER THEM EITHER (2026-09-28): the solid sprites and every
        // creature, ghosts included, had the rule since 2026-09-22 - the translucent ITEMS (the
        // mist, the smoke) did not, and a cloud beside a wall lost the part that reached into it.
        // Without depth writing the depth the rule gives still decides the depth test.
        UWOwnTile.Enable(mOTranslucentBillboardMaterial);

        // The solid-colour faces of the 3D models. In the colour path a copy of the
        // decal material, only under its own name - the separation is only needed
        // in the palette renderer, where these faces have their own shader
        // (UW/ModelPalette).
        //
        // UW/DECAL AND NOT UW/BILLBOARD. The billboard shader turns towards the camera in the vertex stage
        // and reads no vertex colour; set on it the models rotated
        // like sprites and turned white, because the solid-colour faces carry their colour exactly there
        // (per user, 2026-09-05). These faces always went through the
        // decal material - see UWObjectSpawner.fResolve, where pbBillboard is false for
        // 3D models.
        mOModelSolidMaterial = new Material(lODecalShader);
        mOModelSolidMaterial.name = "UW Model Faces";
        mOModelSolidMaterial.mainTexture = mOObjectAtlas.Atlas;
        mOModelSolidMaterial.SetFloat(UWShaderIds.AmbientFloor, lfAmbient);
        mOModelSolidMaterial.SetFloat(UWShaderIds.PointLightCap, lfPointLightCap);

        // A model's own tile does not hide it (UWOwnTile, per user 2026-09-22, the shrine) - only
        // on this material, not on the portcullis or any decal.
        UWOwnTile.Enable(mOModelSolidMaterial);

        // The portcullis. So far like the model faces; it only has its own
        // material because a question is still open there.
        //
        // BACK FACES ARE CULLED, unlike everything else on this shader.
        // A bar consists of two opposing faces; drawn double-sided one always saw
        // both, and the one facing away lay next to it as a second edge (per user,
        // 2026-09-05). With culling exactly one remains per view direction.
        //
        // STILL OPEN is a finer observation: in the original, for ONE of the
        // bar colours one sees the face turned away instead of the one facing the viewer, so its winding there
        // is reversed. That cannot be handled in the material, because it affects all faces
        // equally - for that the triangles of exactly this colour would have to be flipped.
        // CullMode.Front would reproduce the coarse version, in case the whole grid
        // is affected after all.
        mOPortcullisMaterial = new Material(lODecalShader);
        mOPortcullisMaterial.name = "UW Portcullis";
        mOPortcullisMaterial.mainTexture = mOObjectAtlas.Atlas;
        mOPortcullisMaterial.SetFloat(UWShaderIds.AmbientFloor, lfAmbient);
        mOPortcullisMaterial.SetFloat(UWShaderIds.PointLightCap, lfPointLightCap);
        mOPortcullisMaterial.SetFloat(UWShaderIds.Cull, (float)UnityEngine.Rendering.CullMode.Back);

        mODecalMaterial = new Material(lODecalShader);
        mODecalMaterial.name = "UW Decal";
        mODecalMaterial.mainTexture = mOObjectAtlas.Atlas;
        mODecalMaterial.SetFloat(UWShaderIds.AmbientFloor, lfAmbient);
        mODecalMaterial.SetFloat(UWShaderIds.PointLightCap, lfPointLightCap);

        // THE PALETTE INDEX FOR REMASTERED (UWHallucination.hlsl): the hallucination's pictures and
        // the ghosts' tables need it there too - from the index atlas for sprites and decals, from
        // the vertex for the model faces and the portcullis. The palette path swaps these
        // materials and sets its own.
        foreach (Material lOMaterial in new[] { mOBillboardMaterial, mOTranslucentBillboardMaterial, mODecalMaterial })
            lOMaterial.SetTexture(UWShaderIds.IndexTex, mOObjectAtlas.IndexAtlas != null ? mOObjectAtlas.IndexAtlas : Texture2D.blackTexture);

        mOModelSolidMaterial.SetFloat(UWShaderIds.VertexIndex, 1f);
        mOPortcullisMaterial.SetFloat(UWShaderIds.VertexIndex, 1f);
    }

    private static FilterMode fGetFilterMode()
    {
        return UWSettings.Instance != null ? UWSettings.Instance.TextureFilterMode : FilterMode.Point;
    }

    private static float fGetAmbientFloor()
    {
        return UWSettings.Instance != null ? UWSettings.Instance.AmbientFloor : 0.08f;
    }

    private static float fGetPointLightCap()
    {
        return UWSettings.Instance != null ? UWSettings.Instance.PointLightCap : 1f;
    }

    private static float fGetObjectPointLightCap()
    {
        return UWSettings.Instance != null ? UWSettings.Instance.ObjectPointLightCap : 0.6f;
    }

    /// <summary>
    /// Starts the palette rotation for lava and water (see UWAnimatedTextures). Runs
    /// once, as soon as the texture array exists - the texture set applies to all levels.
    /// </summary>
    private void fStartTextureAnimation()
    {
        if (mOTextureArray == null)
            return;

        List<UWTextureArrayBuilder.AnimatedSlice> lOSlices =
            UWTextureArrayBuilder.BuildAnimatedSlices(UWDataImporter, fGetFilterMode());

        if (lOSlices.Count == 0)
            return;

        UWAnimatedTextures lOAnimator = GetComponent<UWAnimatedTextures>();

        if (lOAnimator == null)
            lOAnimator = gameObject.AddComponent<UWAnimatedTextures>();

        lOAnimator.Initialise(mOTextureArray, lOSlices, fGetPaletteRotationSteps());
    }

    private static float fGetPaletteRotationSteps()
    {
        return UWSettings.Instance != null ? UWSettings.Instance.PaletteRotationStepsPerSecond : 4f;
    }

    /// <summary>
    /// Places the character where the save game has it: tile in the upper byte, position within
    /// the tile in the lower byte, 256 steps per tile, as UWSavegameWriter writes it.
    ///
    /// Until 2026-09-17 a loaded game went through fPlacePlayerAtTile and stood in the TILE
    /// CENTRE, the fine position was dropped. Found in a comparison with the original in front
    /// of Drog (per user with screenshots, SAVE4 level 1, 15/36 at fine 130/251): the original
    /// stood at the far edge of the tile, ours half a tile closer, so everything looked a third
    /// larger. A new character carries 0x80, the centre, so nothing changes for it.
    ///
    /// AND AT THE SAVED HEIGHT when that lies a little above the floor under the centre
    /// (per user, 2026-09-22, shrine on level 3, 28/44): the original stands the player on the
    /// HIGHEST floor under the four corners of the footprint (ProcessMotionTileHeights_seg026_379,
    /// see Todo "Standing height at a step"), so one saved an eighth of a tile before a step
    /// stands ON it, at height 13 over a floor of 12. Taking the floor under the centre put the
    /// eye a whole step lower, and the capsule into the riser. Only within one floor step - more
    /// than that is not a step but a mistake in the data, and then the floor wins.
    /// </summary>
    private void fPlacePlayerAtSavedPosition(int piPositionX, int piPositionY, int piZPosition)
    {
        int liTileX = Mathf.Clamp(piPositionX >> 8, 0, UWWorldScale.TilesPerAxis - 1);
        int liTileZ = Mathf.Clamp(piPositionY >> 8, 0, UWWorldScale.TilesPerAxis - 1);

        Vector3 lOAt = new Vector3(
            UWUnits.TileToWorldAxis(liTileX) + ((piPositionX & 0xFF) * UWWorldScale.UnitsPerOriginalUnit),
            0f,
            UWUnits.TileToWorldAxis(liTileZ) + ((piPositionY & 0xFF) * UWWorldScale.UnitsPerOriginalUnit));

        float lfFloor = GetFloorHeightAt(lOAt);
        float lfSaved = UWDataImport.UWData.UWUnits.ZPosToWorld(piZPosition);
        float lfStand = lfSaved > lfFloor && lfSaved - lfFloor <= SavedHeightTolerance ? lfSaved : lfFloor;

        fPlacePlayerOnFloor(lOAt, lfStand);
    }

    /// <summary>One floor step, eight zpos units - see fPlacePlayerAtSavedPosition.</summary>
    private const float SavedHeightTolerance = 8f * UWDataImport.UWData.UWWorldScale.ZPosStep;

    private void fPlacePlayerAtTile(int piTileX, int piTileZ)
    {
        piTileX = Mathf.Clamp(piTileX, 0, UWWorldScale.TilesPerAxis - 1);
        piTileZ = Mathf.Clamp(piTileZ, 0, UWWorldScale.TilesPerAxis - 1);

        // THE TILE CENTRE, as player_setup puts him. Until 2026-10-17 a landing on a tile with a
        // trigger to a teleport was set 16 units away from it (2026-08-23); no teleport of the
        // original data lands on such a tile (all 60 checked), and the user's comparison of the
        // level 1 -> 2 stairs (35/20) showed the same spot in both (audit round 2 C8).
        fPlacePlayerOnFloor(new Vector3(
            UWUnits.TileCentreToWorldAxis(piTileX),
            0f,
            UWUnits.TileCentreToWorldAxis(piTileZ)));
    }

    /// <summary>
    /// Places the character so that the bottom of its CharacterController stands exactly on the
    /// floor - the floor from the tile data, interpolated on slopes
    /// (GetFloorHeightAt), the dimensions from the controller itself.
    ///
    /// Until 2026-09-11 there was a fixed offset of 34 above the tile floor here. But the
    /// controller has its centre at the origin, the feet lie half a height below,
    /// and the character fell a bit after every load and level change (per user). As long as
    /// a lock held gravity, it even hung there in mid-air. The offset is
    /// UWPlayerMovement.FootOffset, the one the motion core's write-back uses (this method had a
    /// copy until cleanup 4.1, 2026-10-18).
    /// </summary>
    private void fPlacePlayerOnFloor(Vector3 pOPosition)
    {
        fPlacePlayerOnFloor(pOPosition, GetFloorHeightAt(pOPosition));
    }

    /// <summary>As above, on a given standing height instead of the floor under the centre.
    /// </summary>
    private void fPlacePlayerOnFloor(Vector3 pOPosition, float pfStandHeight)
    {
        GameObject lOPlayer = UWScene.Player;
        UWPlayerMovement lOMovement = lOPlayer != null ? lOPlayer.GetComponent<UWPlayerMovement>() : null;

        pOPosition.y = pfStandHeight + (lOMovement != null ? lOMovement.FootOffset : 0f);

        fPlacePlayer(pOPosition);
    }

    /// <summary>The save's kin alarm, its height taken from the floor of the victim's tile
    /// (UWCritterAlarm.RestoreFrom).</summary>
    private void fRestoreKinAlarm(UWPlayerData pOPlayer)
    {
        UWTile lOTile = CurrentLevel != null ? CurrentLevel.GetTile(pOPlayer.AlarmTileX, pOPlayer.AlarmTileY) : null;
        int liFloorLevel = lOTile != null ? lOTile.FloorHeight >> 4 : 0;

        UWGameSession.Current.CritterAlarm.RestoreFrom(pOPlayer, UWScene.CritterClock.Clock, liFloorLevel);
    }

    /// <summary>
    /// Takes over quest flags and game variables from the save game.
    ///
    /// Without this every loaded game starts with empty puzzles: the lever positions
    /// come from LEV.ARK and are correct, but the variables behind them were at zero.
    /// A solved puzzle thus counted as unsolved (per user, 2026-09-07, at the three
    /// levers on level 5, tile 6/29).
    ///
    /// The stores belong to the session Start began (UWGameSession, since 2026-10-14), which
    /// starts empty - so the state of a previously loaded game cannot mix in, and the per-session
    /// quirks (the creatures' lava quirk, the cast pause, the light colours; they leaked across
    /// New Game and Load until 2026-10-11) start afresh with it.
    /// </summary>
    private static void fSeedStoredState(UWPlayerData pOPlayer)
    {
        UWGameSession.Current.SeedFrom(pOPlayer);
        UWEndgame.SeedFrom(pOPlayer);

        // Music and sound belong to the SAVE in the original, not to the program - a fresh
        // character carries them too, both on (UWPlayerData.SettingsOffset, NewSettings 0x35).
        if (pOPlayer.IsLoaded)
            UWSoundOptions.ApplyFromSave(pOPlayer.MusicEnabled, pOPlayer.SoundEnabled);
    }

    /// <summary>
    /// Turns the character to a heading, in degrees clockwise from north.
    ///
    /// The rotation about the vertical axis sits on the character itself, the pitch on the camera
    /// below it (see UWPlayerLook) - so only the character is meant here. Set instead of
    /// rotated: UWPlayerLook works with Rotate, i.e. relatively, and does not store the
    /// direction separately.
    /// </summary>
    private void fSetPlayerHeading(float pfDegrees)
    {
        GameObject lOPlayer = UWScene.Player;

        if (lOPlayer == null)
            return;

        lOPlayer.transform.rotation = Quaternion.Euler(0f, pfDegrees, 0f);
    }

    private void fPlacePlayer(Vector3 pOPosition)
    {
        GameObject lOPlayer = UWScene.Player;

        if (lOPlayer == null)
            return;

        // The CharacterController must be off while moving, otherwise it pulls the
        // player back to the old spot in the same frame.
        CharacterController lOController = lOPlayer.GetComponent<CharacterController>();
        bool lbWasEnabled = lOController != null && lOController.enabled;

        if (lOController != null)
            lOController.enabled = false;

        lOPlayer.transform.position = pOPosition;

        if (lOController != null)
            lOController.enabled = lbWasEnabled;

        UWPlayerMovement lOMovement = lOPlayer.GetComponent<UWPlayerMovement>();

        // The motion core takes the spot (stage 3): PlaceAt with the feet's fine position. PlaceAt
        // clears the motion's speeds, so nothing of a fall carries over to the new spot - the
        // CharacterController path's ResetVerticalVelocity did that until cleanup 4.1 (2026-10-18).
        if (lOMovement != null)
            lOMovement.PlaceFromTransform();
    }

    /// <summary>
    /// The lighting hangs on the level object and persists across level
    /// changes - in UW1 the light comes from the player, not from the level.
    /// </summary>
    private void fInitialiseLighting()
    {
        UWLighting lOLighting = GetComponent<UWLighting>();

        if (lOLighting == null)
            lOLighting = gameObject.AddComponent<UWLighting>();

        lOLighting.Init(UWDataImporter);
    }

    /// <summary>
    /// Overlay with location and view target, so that places from screenshots
    /// can be found again.
    /// </summary>
    private void fInitialiseDebugOverlay()
    {
        UWDebugOverlay lOOverlay = GetComponent<UWDebugOverlay>();

        if (lOOverlay == null)
            lOOverlay = gameObject.AddComponent<UWDebugOverlay>();

        lOOverlay.Init(this);

        // Jump box and the display of invisible objects - see UWDebugTools.
        UWDebugTools lOTools = GetComponent<UWDebugTools>();

        if (lOTools == null)
            lOTools = gameObject.AddComponent<UWDebugTools>();

        lOTools.Init(this);
    }

    /// <summary>
    /// After loading a save game the original shows "Restoring Game ", then the dots
    /// fill up, then "Restore Game Complete." - all on one line (per user,
    /// 2026-09-12). For us loading is already over, so it appears in full right away.
    /// </summary>
    private void fReportRestore()
    {
        if (UWDataImporter == null || !UWDataImporter.IsSavegame)
            return;

        Interaction lOInteraction = UWScene.Interaction;

        if (lOInteraction == null)
            return;

        lOInteraction.ResetMessages();
        lOInteraction.AddMessage(UWDataImporter.GetGeneralMessage(RestoringGameMessage)
            + UWHudOptions.ProgressDots
            + UWDataImporter.GetGeneralMessage(RestoreGameCompleteMessage).TrimEnd('\r', '\n'));
    }

    private const int RestoringGameMessage = 167;

    private const int RestoreGameCompleteMessage = 163;

    private void fInitialiseUi()
    {
        Camera lOCamera = Camera.main;

        if (lOCamera == null)
            return;

        UWCharacter lOCharacter = lOCamera.GetComponent<UWCharacter>();

        if (lOCharacter != null)
            lOCharacter.Init(UWDataImporter);

        UWGameUI lOGameUi = lOCamera.GetComponent<UWGameUI>();

        if (lOGameUi != null)
            lOGameUi.Init(UWDataImporter);

        // No manual wiring in the scene needed - same self-creation pattern
        // as UWDebugOverlay in fInitialiseDebugOverlay(). (The modern scheme's armour panel
        // UWInventoryUI was created here until 2026-10-03; the character panel UWModernPanel
        // replaced it.)
        if (lOCamera.GetComponent<UWItemDrag>() == null)
            lOCamera.gameObject.AddComponent<UWItemDrag>();

        // Main menu and character creation - both only show when they are
        // needed (see UWMainMenu.fAutoShow).
        UWCharacterCreationScreen lOCreation = lOCamera.GetComponent<UWCharacterCreationScreen>();

        if (lOCreation == null)
            lOCreation = lOCamera.gameObject.AddComponent<UWCharacterCreationScreen>();

        lOCreation.Init(UWDataImporter);

        UWMainMenu lOMenu = lOCamera.GetComponent<UWMainMenu>();

        if (lOMenu == null)
            lOMenu = lOCamera.gameObject.AddComponent<UWMainMenu>();

        lOMenu.Init(UWDataImporter);

        // The detached camera creates itself - it hangs on no scene object,
        // so that it does not accidentally inherit the debug tool on the observer camera.
        UWRemoteCamera.Ensure();

        fInitialisePlayerTerrain(lOCamera);
        UWInventory lOInventory = lOCamera.GetComponentInChildren<UWInventory>();

        if (lOInventory == null)
            lOInventory = lOCamera.GetComponentInParent<UWInventory>();

        // Handedness applies to a new character too: it has no save game path, so the
        // branch below is skipped, and a left-hander still fought with the right hand
        // (per user, 2026-09-14).
        if (lOInventory != null && UWDataImporter.InitialPlayer != null && UWDataImporter.InitialPlayer.IsLoaded)
            lOInventory.IsLeftHanded = UWDataImporter.InitialPlayer.IsLeftHanded;

        // Equipment and backpack from the save game. Without a save game the inventory stays
        // empty, as before.
        if (UWDataImporter.IsSavegame && lOInventory != null)
            lOInventory.LoadFromPlayerData(UWDataImporter.InitialPlayer);
    }

    /// <summary>Water and lava under the player's feet. Hangs on the body, not
    /// on the camera - that is where the CharacterController sits.</summary>
    private void fInitialisePlayerTerrain(Camera pOCamera)
    {
        CharacterController lOBody = pOCamera.GetComponentInParent<CharacterController>();

        if (lOBody == null)
            return;

        UWPlayerTerrain lOTerrain = lOBody.GetComponent<UWPlayerTerrain>();

        if (lOTerrain == null)
            lOTerrain = lOBody.gameObject.AddComponent<UWPlayerTerrain>();

        lOTerrain.Init(this, pOCamera.transform);
    }
}
