using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
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

    /// <summary>Range of the trigger objects. 0x01A0 is the move trigger.</summary>
    private const int TriggerFirstId = UWTrapRules.FirstTriggerId;
    private const int TriggerLastId = UWTrapRules.LastTriggerId;

    /// <summary>Lockout after a transition, so that a way back does not fire again immediately.</summary>
    private const float TransitionCooldown = 0.75f;

    [Header("Throwing (original)")]
    [SerializeField]
    [Tooltip("Original: flight time of a thrown item in seconds, see SpawnDroppedObject/UWThrownItemFlight.")]
    private float mfThrowFlightDuration = 0.4f;

    [SerializeField]
    [Tooltip("Original: how high a thrown item arcs above the straight line from start to target.")]
    private float mfThrowFlightArcHeight = 24f;

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
    /// (see UWPaletteRenderToggle).</summary>
    public Material ModelSolidMaterial
    {
        get { return mOModelSolidMaterial; }
    }

    /// <summary>The object atlas, so that the palette renderer can get at its index
    /// version (see UWPaletteRenderToggle).</summary>
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

    /// <summary>The material of floor, ceiling and walls - for UWRemasterRenderer, which
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

    /// <summary>See fRebuildEntityRegistry/TryGetEntity.</summary>
    private Dictionary<UWObject, UWEntityInfo> mEntityByData;

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

        piLevelIndex = ((piLevelIndex % UWDataImporter.Levels.Count) + UWDataImporter.Levels.Count) % UWDataImporter.Levels.Count;

        UWWorldSync.Capture(this);

        miLevelIndex = piLevelIndex;
        fBuildLevel();
    }

    /// <summary>All world objects of the current level with their data - for UWWorldSync. Also
    /// disabled ones, so that nothing currently hidden slips through.</summary>
    public UWEntityInfo[] GetWorldEntities()
    {
        return mOObjectRoot != null
            ? mOObjectRoot.GetComponentsInChildren<UWEntityInfo>(true)
            : new UWEntityInfo[0];
    }

    /// <summary>Public form of fGetSubTilePos - for UWWorldSync.</summary>
    public static ushort GetSubTilePos(float pfWorldCoordinate, int piTileIndex)
    {
        return fGetSubTilePos(pfWorldCoordinate, piTileIndex);
    }

    /// <summary>
    /// Removes an object from its tile's list WITHOUT touching the display - for a
    /// creature that dies: its death animation is still running, but in the data it is already
    /// gone. Otherwise it would be back on re-entering the level and in the save game.
    /// </summary>
    public bool ForgetObjectData(UWObject pOObject)
    {
        bool lbForgotten = TileQueries.ForgetObjectData(pOObject);

        // A dead creature gives its slot back (UWLevel.ReleaseMobileSlot, AddToFreeObjectsList).
        if (lbForgotten && pOObject is UWNpc && CurrentLevel != null)
            CurrentLevel.ReleaseMobileSlot(pOObject);

        return lbForgotten;
    }

    /// <summary>
    /// Moves an object into the list of another tile - for UWWorldSync, when a
    /// creature has walked on. If it stays on its tile, the order stays as well.
    /// Whatever is in no list is not re-inserted.
    /// </summary>
    public void MoveObjectData(UWObject pOObject, int piTileX, int piTileZ)
    {
        TileQueries.MoveObjectData(pOObject, piTileX, piTileZ);
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

        if (!fLoadData())
        {
            // Otherwise the screen would stay black forever. No game data: the setup screen asks
            // for the GOG folder and starts the scene again (per user, 2026-09-17).
            UWScreenUi.HideLoadingCover();
            UWSetupMenu.ShowFirstRun();
            return;
        }

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

            UWAudioEngine.Ensure(UWDataImporter);
            UWMusic.Init(UWDataImporter);
            UWSoundEffects.Init(UWDataImporter);

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

        if (lbFromSave || (myNewCharacter != null && lOPlayer != null))
        {
            // The new character exactly like a loaded one: via its tile (32/2) and the
            // tile floor, not via the fixed start point in the Inspector.
            fPlacePlayerAtSavedPosition(lOPlayer.PositionX, lOPlayer.PositionY, lOPlayer.ZPosition);
            fSetPlayerHeading(lOPlayer.Heading * 360f / HeadingSteps);

            // The carried objects take their slots in this level, as in the original.
            List<UWObject> lOCarried = new List<UWObject>(lOPlayer.Equipment);
            lOCarried.AddRange(lOPlayer.Backpack);
            fMakeRoomForInventory(UWObjectLimitRules.CountObjects(lOCarried),
                new UWTilePos(lOPlayer.PositionX >> 8, lOPlayer.PositionY >> 8));

            // ... in the order of their chain in PLAYER.DAT (RestoreGame, see UWCarriedSlots).
            UWCarriedSlots.TakeSlots(CurrentLevel, lOPlayer.GetCarriedChain());
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
        UWAudioEngine.Ensure(UWDataImporter);
        UWMusic.Init(UWDataImporter);
        UWSoundEffects.Init(UWDataImporter);

        if (!UWMainMenu.WillAutoShow && !UWMusic.IsPlaying)
            UWMusic.PickLevelTheme();

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

        // Write what happened here into the level's data before its objects disappear - otherwise
        // everything would be as before on coming back (see UWWorldSync).
        UWWorldSync.Capture(this);

        // On leaving a level the original tidies up among its creatures: home to
        // the home tile, wounds partly healed, allies independent again, the
        // attitude per race balanced (see UWLevelExit). A jump WITHIN the level
        // does not count - the original only knows this step on a real transition.
        if (piLevelIndex != miLevelIndex)
            UWLevelExit.LeaveLevel(TileQueries);

        // Tybal's orb drains the mana in its lair and gives it back on leaving (UWTybalOrbRules) -
        // on a real level change only, like the tidying above; loading a save keeps what it has.
        if (piLevelIndex != miLevelIndex && UWScene.Character != null)
            UWTybalOrbRules.OnLevelChange(UWScene.Character.Vitals, miLevelIndex, piLevelIndex);

        bool lbOtherLevel = piLevelIndex != miLevelIndex;

        // The carried objects leave the old level's static slots (ClearObjectList_ovr118_4BE)
        // and take new ones in the next (ovr118_3CB) - see UWCarriedSlots.
        List<UWObject> lOCarriedChain = lbOtherLevel ? fCarriedChain() : null;

        if (lOCarriedChain != null)
            UWCarriedSlots.ReturnSlots(CurrentLevel, lOCarriedChain);

        miLevelIndex = piLevelIndex;

        fBuildLevel();

        if (lOCarriedChain != null)
            UWCarriedSlots.TakeSlots(CurrentLevel, lOCarriedChain);

        // The palette renderer's material swap remembers the level number. If it stays
        // the same - a jump within the same level - it would otherwise miss the rebuild
        // and the fresh geometry would stay on URP.
        if (mOPaletteToggle == null)
            mOPaletteToggle = GetComponent<UWPaletteRenderToggle>();

        if (mOPaletteToggle != null)
            mOPaletteToggle.Reapply();

        fPlacePlayerAtTile(piTileX, piTileZ);

        if (lbOtherLevel)
            fMakeRoomForInventory(fCarriedObjectCount());

        Debug.Log(string.Format("Entered level {0}, tile ({1},{2})", miLevelIndex + 1, piTileX, piTileZ));
    }

    /// <summary>
    /// Static game data and the resources built from it, kept across scene reloads. Loading a
    /// save game or starting a new character reloads the scene (see UWSavegameSlots,
    /// UWCharacterCreationScreen); without this cache every start would parse all data files
    /// and rebuild the object atlas and texture array again. They are prepared while the main
    /// menu is up and reused by the game that is started from it (per user, 2026-09-14).
    /// Objects referenced from static fields survive Resources.UnloadUnusedAssets.
    /// </summary>
    private static DataImport msOSharedData;

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

        if (msOSharedData == null || msOSharedData.DataPath != lsPath)
        {
            msOSharedData = new DataImport(lsPath, lsSavegame);
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
                mODungeonMaterial.SetFloat("_AmbientFloor", fGetAmbientFloor());
                mODungeonMaterial.SetFloat("_PointLightCap", fGetPointLightCap());
            }

            fStartTextureAnimation();
        }

        fCreateObjectMaterials();

        // The palette renderer attaches itself as soon as everything is in place - see
        // UWPaletteRenderToggle.fAutoStart. Only provide the component here, so that it
        // is not created on the first press of F6.
        if (GetComponent<UWPaletteRenderToggle>() == null)
            gameObject.AddComponent<UWPaletteRenderToggle>();

        // The same for the regular refilling of monsters.
        if (GetComponent<UWCreatureRespawner>() == null)
            gameObject.AddComponent<UWCreatureRespawner>();

        // And for the frame tick of the creatures: the 16-slot clock and the walk over every
        // creature that is due (see UWCritterDriver).
        if (GetComponent<UWCritterDriver>() == null)
            gameObject.AddComponent<UWCritterDriver>();

        // And for the Remastered render mode, which only builds its normal array when it
        // is needed.
        if (GetComponent<UWRemasterRenderer>() == null)
            gameObject.AddComponent<UWRemasterRenderer>();

        // The additional light sources of the same mode.
        if (GetComponent<UWRemasterLights>() == null)
            gameObject.AddComponent<UWRemasterLights>();

        // Death and resurrection at the silver tree, see UWDeath.
        if (GetComponent<UWDeath>() == null)
            gameObject.AddComponent<UWDeath>();

        // The effects of the Ethereal Void on level 9, see UWVoidEffects.
        if (GetComponent<UWVoidEffects>() == null)
            gameObject.AddComponent<UWVoidEffects>();

        // Monitoring of graphics resources, see UWResourceWatch.
        if (GetComponent<UWResourceWatch>() == null)
            gameObject.AddComponent<UWResourceWatch>();

        // And the ground shadows under creatures and items.
        if (GetComponent<UWRemasterGroundShadows>() == null)
            gameObject.AddComponent<UWRemasterGroundShadows>();

        if (mO3DModels == null)
        {
            UWSettings lOSettings = UWSettings.Instance;

            if (msOShared3DModels == null && lOSettings != null && lOSettings.IsExePathValid)
            {
                // The model import reads UW.EXE at offsets of the GOG build only.
                if (UWGogInstall.IsSupportedExe(lOSettings.ExePath))
                    msOShared3DModels = new UW3DModelImport(lOSettings.ExePath);
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
    /// object table (UWTileMap.ResetMap). For us these are two things: the
    /// tile lists of the level data and the display objects below.
    /// </summary>
    public void ApplyArmageddon()
    {
        ArmageddonActive = true;

        // UW.EXE MajorSpellClassB clears the silver tree nibble along with the objects.
        UWSilverTree.TreeLevel = 0;

        fWipeObjects();
    }

    private void fWipeObjects()
    {
        if (CurrentLevel != null && CurrentLevel.TileData != null)
        {
            for (int liAt = 0; liAt < CurrentLevel.TileData.Length; liAt++)
            {
                UWTile lOTile = CurrentLevel.TileData[liAt];

                if (lOTile != null && lOTile.ObjectsInTile != null)
                    lOTile.ObjectsInTile.Clear();
            }
        }

        if (mOObjectRoot != null)
        {
            for (int liChild = mOObjectRoot.transform.childCount - 1; liChild >= 0; liChild--)
                Destroy(mOObjectRoot.transform.GetChild(liChild).gameObject);
        }

        if (mEntityByData != null)
            mEntityByData.Clear();
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
    /// Called every frame and only does something if something really has
    /// changed - the switch costs a rebuild of the geometry.
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

    private void fBuildLevel()
    {
        UWCritter.ResetAlarm();

        // The carried objects take their numbers from this level (UWCarriedSlots).
        UWCarriedSlots.Level = CurrentLevel;

        if (mOLevelRoot != null)
        {
            // Deactivate first, then destroy: Destroy only takes effect at the end of the frame, until then
            // the old transition triggers would still fire.
            mOLevelRoot.SetActive(false);
            Destroy(mOLevelRoot);
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
        fSpawnTransitions(mOLevelRoot.transform);

        // After Armageddon every level is empty, even one never entered - see
        // ArmageddonActive. Clearing happens after the build instead of before: only then are the tile lists
        // complete, and the transitions do not hang on objects.
        if (ArmageddonActive)
            fWipeObjects();
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
            Destroy(lOExisting.gameObject);
        }

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

        fRebuildEntityRegistry();
    }

    private UWPaletteRenderToggle mOPaletteToggle;

    /// <summary>
    /// Applies the currently active render path to everything added under the object node
    /// since piChildCountBefore.
    ///
    /// The palette renderer's material swap runs once per level over everything that
    /// exists then (see UWPaletteRenderToggle.ApplyToNewObject). Without this follow-up
    /// every object created at runtime stays on the old path - visible because
    /// it is the only thing the player light falls on.
    /// </summary>
    private void fApplyRenderModeFrom(int piChildCountBefore)
    {
        if (mOObjectRoot == null)
            return;

        if (mOPaletteToggle == null)
            mOPaletteToggle = GetComponent<UWPaletteRenderToggle>();

        if (mOPaletteToggle == null)
            return;

        for (int liChild = piChildCountBefore; liChild < mOObjectRoot.transform.childCount; liChild++)
            mOPaletteToggle.ApplyToNewObject(mOObjectRoot.transform.GetChild(liChild).gameObject);
    }

    /// <summary>The same for a single freshly built node - the geometry after
    /// a rebuild, for example.</summary>
    private void fApplyRenderModeTo(GameObject pOBuilt)
    {
        if (pOBuilt == null)
            return;

        if (mOPaletteToggle == null)
            mOPaletteToggle = GetComponent<UWPaletteRenderToggle>();

        if (mOPaletteToggle != null)
            mOPaletteToggle.ApplyToNewObject(pOBuilt);
    }

    /// <summary>Data-driven triggers (UWTriggerSystem) have - unlike the player
    /// via raycast - no click hit through which they could find a UWEntityInfo for the target tile
    /// (all objects hang flat under mOObjectRoot, not grouped per
    /// tile). Built once after each spawn, replaces itself on every
    /// level change/rebuild.</summary>
    private void fRebuildEntityRegistry()
    {
        mEntityByData = new Dictionary<UWObject, UWEntityInfo>();

        if (mOObjectRoot == null)
            return;

        UWEntityInfo[] lyEntities = mOObjectRoot.GetComponentsInChildren<UWEntityInfo>(true);

        foreach (UWEntityInfo lOEntity in lyEntities)
        {
            if (lOEntity.ObjectData != null)
                mEntityByData[lOEntity.ObjectData] = lOEntity;
        }
    }

    /// <summary>For UWTriggerSystem (door trap: find the door GameObject for a UWObject found
    /// via the target tile, without a player click hit).</summary>
    public bool TryGetEntity(UWObject pOData, out UWEntityInfo pOEntity)
    {
        if (mEntityByData != null && pOData != null)
            return mEntityByData.TryGetValue(pOData, out pOEntity);

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
    /// <param name="piQuantity">Count for a stack, or zero for a
    /// single item. A quantity field only holds what has the quantity bit set - see
    /// UWObject.HasQuantity.</param>
    /// <param name="piQuality">Condition from zero to 63, or minus one for
    /// unchanged.</param>
    public bool SpawnObjectById(int piObjectId, int piTileX, int piTileZ,
        int piQuantity = 0, int piQuality = -1)
    {
        return SpawnObjectById(piObjectId, piTileX, piTileZ, piQuantity, piQuality, null);
    }

    /// <summary>As above, but at a specific world position within the tile - for
    /// blood and remains that lie where the creature died. Null means tile centre.
    /// </summary>
    public bool SpawnObjectById(int piObjectId, int piTileX, int piTileZ,
        int piQuantity, int piQuality, Vector3? pOWorldPosition, bool pbScatter = false)
    {
        if (pbScatter && CurrentLevel != null)
            pOWorldPosition = GetRandomPositionInTile(piTileX, piTileZ, piObjectId);

        UWObject lOObject = fBuildObject(piObjectId, piQuantity, piQuality);

        if (lOObject == null)
            return false;

        // Centre of the tile: the sub-tile fields of a freshly built object are 0, and
        // that would be the tile corner.
        lOObject.XPos = 3;
        lOObject.YPos = 3;
        lOObject.ZPos = CurrentLevel.TileData[(piTileZ * UWLevelMeshBuilder.TilesPerAxis) + piTileX].FloorHeight / 2;

        if (pOWorldPosition.HasValue)
            return CurrentLevel != null && mOObjectRoot != null
                && fSpawnObjectAt(lOObject, piTileX, piTileZ, pOWorldPosition.Value, false, null);

        return SpawnObjectInTile(lOObject, piTileX, piTileZ);
    }

    /// <summary>A new object of this kind with its picture, or null without data.</summary>
    private UWObject fBuildObject(int piObjectId, int piQuantity, int piQuality)
    {
        if (UWDataImporter == null || CurrentLevel == null)
            return null;

        UWObject lOObject = new UWObject((ushort)piObjectId);

        if (piQuantity > 0)
        {
            lOObject.HasQuantity = true;
            lOObject.Quantity = (ushort)piQuantity;
        }

        if (piQuality >= 0)
            lOObject.Quality = (ushort)piQuality;

        try
        {
            lOObject.Texture = UWDataImporter.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, piObjectId);
        }
        catch
        {
            return null;
        }

        return lOObject.Texture != null ? lOObject : null;
    }

    /// <summary>A new object of this kind put down around a point - see
    /// SpawnScatteredAround.</summary>
    public bool SpawnObjectByIdAround(int piObjectId, int piQuantity, int piQuality, Vector3 pOCentre,
        int piRadiusEighths, UWTilePos? pOFallbackTile = null)
    {
        UWObject lOObject = fBuildObject(piObjectId, piQuantity, piQuality);

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
    /// pOFallbackTile: where the object goes, scattered in that tile, if even the point itself
    /// is refused (a body standing over a wall's tile) - so that nothing is lost that the
    /// culling test did not take.
    ///
    /// pbAvoidWorldContainers: a spot must keep the two radii away from any barrel, chest or
    /// nightstand in its tile - the one piece of the original's object collision taken over
    /// (per user, 2026-09-16: nothing from a spilled barrel comes to rest on the next one).
    /// </summary>
    public bool SpawnScatteredAround(UWObject pOObject, Vector3 pOCentre, int piRadiusEighths,
        UWTilePos? pOFallbackTile = null, bool pbAvoidWorldContainers = false)
    {
        if (pOObject == null || CurrentLevel == null || mOObjectRoot == null)
            return false;

        int liCentreX = UWTileQueries.WorldToEighths(pOCentre.x);
        int liCentreY = UWTileQueries.WorldToEighths(pOCentre.z);

        int liX;
        int liY;

        if (!UWScatterRules.TryPickSpot(liCentreX, liCentreY, piRadiusEighths,
            (piX, piY) => fFootprintFitsAtEighths(piX, piY, fGetObjectRadius(pOObject.ID), pOCentre.y)
                && (!pbAvoidWorldContainers || fClearOfWorldContainers(piX, piY, pOObject.ID)), out liX, out liY))
        {
            if (TileQueries.Culls(pOObject.ID, pOObject.HasQuantity && pOObject.Quantity < UWObjectMechanics.SpecialPropertyThreshold ? pOObject.Quantity : 1))
                return false;
        }

        Vector3 lOSpot = new Vector3(UWTileQueries.EighthsToWorld(liX), pOCentre.y, UWTileQueries.EighthsToWorld(liY));

        if (fSpawnObjectAt(pOObject, liX >> 3, liY >> 3, lOSpot, false, null))
            return true;

        return pOFallbackTile.HasValue
            && SpawnObjectInTileScattered(pOObject, pOFallbackTile.Value.X, pOFallbackTile.Value.Y);
    }

    /// <summary>Whether the last object that tried to join the level was refused for want of
    /// a slot - so a caller does not try again elsewhere (see fReserveSlot).</summary>
    public bool LastSpawnRefusedForSlot { get; private set; }

    /// <summary>
    /// GetFreeObject for an object about to join the level (UWObjectLimitRules): an object
    /// already in a tile list keeps its slot; otherwise, if its kind has no free slot, far
    /// objects are culled - range 3, five at most for a creature, ten for anything else - and
    /// the object is refused when that freed nothing. Culling frees the slots of whatever it
    /// takes, so three creatures culled for a static slot leave the static list empty and the
    /// object is refused all the same - as in the original.
    /// </summary>
    private bool fReserveSlot(UWObject pOObject)
    {
        LastSpawnRefusedForSlot = false;

        if (pOObject == null || CurrentLevel == null || TileQueries.FindTileHolding(pOObject) != null)
            return true;

        bool lbMobile = UWObjectLimitRules.NeedsMobileSlot(pOObject);

        if (UWObjectLimitRules.HasFreeSlot(CurrentLevel, lbMobile, fCarriedObjectCount()))
            return true;

        UWTilePos lOPlayer = UWScene.Character != null
            ? WorldPositionToTile(UWScene.Character.transform.position)
            : new UWTilePos(-100, -100);

        int liCulled = UWObjectLimitRules.Cull(CurrentLevel, lOPlayer.X, lOPlayer.Y, UWObjectLimitRules.CullRange,
            lbMobile ? UWObjectLimitRules.MobileCullLimit : UWObjectLimitRules.StaticCullLimit,
            fCullsForSpace, fRemoveCulled);

        bool lbFree = UWObjectLimitRules.HasFreeSlot(CurrentLevel, lbMobile, fCarriedObjectCount());

        Debug.Log(string.Format("Level {0} full ({1} slots): culled {2}, {3}", CurrentLevel.LevelNumber,
            lbMobile ? "mobile" : "static", liCulled, lbFree ? "slot free" : "object " + pOObject.ID + " refused"));

        LastSpawnRefusedForSlot = !lbFree;

        return lbFree;
    }

    /// <summary>The culling test with the range GetFreeObject uses.</summary>
    private bool fCullsForSpace(UWObject pOCandidate)
    {
        return UWObjectLimitRules.Culls(pOCandidate, UWObjectLimitRules.CullRange,
            UWDataImporter != null ? UWDataImporter.CommonObjectProperties : null, CurrentLevel.Masterlist);
    }

    private void fRemoveCulled(UWObject pOCulled, int piTileX, int piTileY)
    {
        pOCulled.TileX = piTileX;
        pOCulled.TileY = piTileY;
        RemoveObjectFromWorld(pOCulled);
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
    /// (UWObjectLimitRules.MakeRoomForInventory): if level and inventory together exceed the
    /// static slots, far objects are culled in steps of ten until they fit.
    /// </summary>
    private void fMakeRoomForInventory(int piCarriedObjects, UWTilePos? pOPlayerTile = null)
    {
        if (CurrentLevel == null || piCarriedObjects <= 0)
            return;

        UWTilePos lOPlayer = pOPlayerTile.HasValue ? pOPlayerTile.Value : UWScene.Character != null
            ? WorldPositionToTile(UWScene.Character.transform.position)
            : new UWTilePos(-100, -100);

        int liCulled = UWObjectLimitRules.MakeRoomForInventory(CurrentLevel, lOPlayer.X, lOPlayer.Y, piCarriedObjects,
            fCullsForSpace, fRemoveCulled);

        if (liCulled > 0)
            Debug.Log(string.Format("Level {0}: {1} carried objects arrived in a full level, culled {2}",
                CurrentLevel.LevelNumber, piCarriedObjects, liCulled));
    }

    /// <summary>
    /// THE OBJECT'S FOOTPRINT, not only its point (per user, 2026-09-28: "the loot lies too close
    /// to walls at times"): the original's fit test runs the motion test with the object's own
    /// radius (COMOBJ, in eighths of a tile), so a wall or a higher floor within that radius
    /// refuses the spot. Ours checked the spot alone, and an item could lie on the last eighth
    /// against a wall. Now every eighth of the square the radius spans has to pass the tile test;
    /// whether the original draws the line one eighth nearer or further is not read.
    /// </summary>
    private bool fFootprintFitsAtEighths(int piX, int piY, int piRadius, float pfPointHeight)
    {
        for (int liDy = -piRadius; liDy <= piRadius; liDy++)
        {
            for (int liDx = -piRadius; liDx <= piRadius; liDx++)
            {
                if (!fFitsAtEighths(piX + liDx, piY + liDy, pfPointHeight))
                    return false;
            }
        }

        return true;
    }

    /// <summary>The tile part of CheckIfItemFitsInTile_seg026_1008 - see SpawnScatteredAround.</summary>
    private bool fFitsAtEighths(int piX, int piY, float pfPointHeight)
    {
        int liTileX = piX >> 3;
        int liTileY = piY >> 3;

        if (piX < 0 || piY < 0 || liTileX >= UWLevelMeshBuilder.TilesPerAxis || liTileY >= UWLevelMeshBuilder.TilesPerAxis)
            return false;

        UWTile lOTile = CurrentLevel.TileData[(liTileY * UWLevelMeshBuilder.TilesPerAxis) + liTileX];

        if (lOTile == null || !UWTileQueries.IsSubTileInOpenHalf(lOTile.TileType, piX & 7, piY & 7))
            return false;

        float lfFloor = fGetFloorHeightAt(lOTile, UWTileQueries.EighthsToWorld(piX), UWTileQueries.EighthsToWorld(piY),
            liTileX, liTileY);

        return lfFloor <= pfPointHeight + FloorHeightTolerance;
    }

    /// <summary>Whether a spot in eighths keeps clear of the world containers in its tile - see
    /// SpawnScatteredAround.</summary>
    private bool fClearOfWorldContainers(int piX, int piY, int piObjectId)
    {
        Vector3 lOSpot = new Vector3(UWTileQueries.EighthsToWorld(piX), 0f, UWTileQueries.EighthsToWorld(piY));

        return fGetDistanceToWorldContainer(piX >> 3, piY >> 3, lOSpot) >= fGetContainerClearance(piObjectId);
    }

    /// <summary>World units a floor may lie above the point and still count as not higher -
    /// rounding between the body's height and the floor's.</summary>
    private const float FloorHeightTolerance = 1f;

    /// <summary>
    /// Places an object at a RANDOM spot of its tile - that is how the original spreads the
    /// loot of a dead creature (the reference: container.SpillWorldContainer with
    /// tilemap.GetRandomXYZForTile). Before, everything lay stacked exactly in the tile centre
    /// (per user, 2026-09-13: "The original spreads it").
    /// </summary>
    public bool SpawnObjectInTileScattered(UWObject pOObject, int piTileX, int piTileZ,
        bool pbAvoidWorldContainers = false)
    {
        if (pOObject == null || CurrentLevel == null || mOObjectRoot == null)
            return false;

        Vector3 lOSpot = pbAvoidWorldContainers
            ? fGetPositionClearOfContainers(piTileX, piTileZ, pOObject.ID)
            : GetRandomPositionInTile(piTileX, piTileZ, pOObject.ID);

        return fSpawnObjectAt(pOObject, piTileX, piTileZ, lOSpot, false, null);
    }

    /// <summary>How many spots are tried before the one with the largest distance is
    /// taken. The tile is eight by eight sub-tiles, so a handful of tries is plenty.</summary>
    private const int ContainerClearanceTries = 12;

    /// <summary>
    /// A spot in the tile that does NOT lie on a barrel or chest standing there.
    ///
    /// Only for the contents of a spilled container (per user, 2026-09-16: "when spilling
    /// we should make sure that no item lands on the same spot as chests or barrels").
    /// THROWN items are not affected - in the original one can very well come to rest on a
    /// barrel.
    ///
    /// The clearance is the two radii from COMOBJ.DAT added together, in eighths of a
    /// tile, which is the same measure the wall clearance uses (see KeepClearOfEdges).
    /// If every try lands on a container - a tile can be full of them - the spot with the
    /// largest distance is taken; nothing is ever left unplaced.
    /// </summary>
    private Vector3 fGetPositionClearOfContainers(int piTileX, int piTileZ, int piObjectId)
    {
        float lfNeeded = fGetContainerClearance(piObjectId);

        Vector3 lOBest = Vector3.zero;
        float lfBest = -1f;

        for (int liTry = 0; liTry < ContainerClearanceTries; liTry++)
        {
            Vector3 lOSpot = GetRandomPositionInTile(piTileX, piTileZ, piObjectId);
            float lfDistance = fGetDistanceToWorldContainer(piTileX, piTileZ, lOSpot);

            if (lfDistance >= lfNeeded)
                return lOSpot;

            if (lfDistance > lfBest)
            {
                lfBest = lfDistance;
                lOBest = lOSpot;
            }
        }

        return lOBest;
    }

    /// <summary>The radius of the item plus that of a container, in world units (the same
    /// measure as fGetTriggerReach uses for the player).</summary>
    private float fGetContainerClearance(int piObjectId)
    {
        int liItem = Mathf.Clamp(fGetObjectRadius(piObjectId), 1, 3);
        int liContainer = Mathf.Clamp(fGetObjectRadius(UWObjectMechanics.BarrelObjectId), 1, 3);

        return (liItem + liContainer) * EighthTile;
    }

    /// <summary>Distance to the nearest barrel, chest or nightstand in this tile, measured
    /// horizontally. Huge if there is none.</summary>
    private float fGetDistanceToWorldContainer(int piTileX, int piTileZ, Vector3 pOSpot)
    {
        if (CurrentLevel == null || CurrentLevel.TileData == null)
            return float.MaxValue;

        UWTile lOTile = CurrentLevel.TileData[(piTileZ * UWLevelMeshBuilder.TilesPerAxis) + piTileX];

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
    /// A random sub-tile spot as a world position, like GetRandomXYZForTile of the
    /// reference: on open and sloped tiles 0 to 7 in both directions, on
    /// diagonal ones only in the open half. The tile types are numbered the same there
    /// (SE 2, SW 3, NE 4, NW 5). The height - also on slopes - is computed by fSpawnObjectAt from
    /// the spot, which is why it stays zero here.
    ///
    /// DISTANCE TO THE WALL (per user, 2026-09-13: "It must also keep the distance rule to walls
    /// like dropped items do"): if next to the tile there is a solid one or
    /// one with a higher floor (step), the spot is pulled away from it by the item's radius from
    /// COMOBJ.DAT (KeepClearOfEdges), in
    /// eighths of the tile and at least one - a thrown item keeps that much distance too
    /// (UWItemDrag, 8 world units). The reference checks the same with the
    /// radius when dropping (pickup.DropObjectByPlayer, motion.TestIfObjectFitsInTile). Several pieces
    /// on the same spot are allowed - in the original they lie on top of each other when space
    /// is short (per user, 2026-09-13).
    /// </summary>
    public Vector3 GetRandomPositionInTile(int piTileX, int piTileZ, int piObjectId = -1)
    {
        UWTile lOTile = CurrentLevel.TileData[(piTileZ * UWLevelMeshBuilder.TilesPerAxis) + piTileX];
        int liX;
        int liY;

        // Random.Range with integers excludes the upper bound, like Next(a, b).
        switch (lOTile != null ? lOTile.TileType : UWTile.TileTypeEnum.open)
        {
            case UWTile.TileTypeEnum.diagonal_ne:
                liX = Random.Range(1, 8);
                liY = Random.Range(7 - liX, 8);
                break;

            case UWTile.TileTypeEnum.diagonal_se:
                liX = Random.Range(1, 8);
                liY = Random.Range(1, liX);
                break;

            case UWTile.TileTypeEnum.diagonal_nw:
                liX = Random.Range(1, 8);
                liY = Random.Range(liX, 8);
                break;

            case UWTile.TileTypeEnum.diagonal_sw:
                liX = Random.Range(1, 8);
                liY = Random.Range(0, 8 - liX);
                break;

            default:
                liX = Random.Range(0, 8);
                liY = Random.Range(0, 8);
                break;
        }

        int liRadius = 1;
        UWCommonObjectProperties.Entry lOEntry;

        if (piObjectId >= 0 && UWDataImporter != null && UWDataImporter.CommonObjectProperties != null
            && UWDataImporter.CommonObjectProperties.TryGet(piObjectId, out lOEntry))
            liRadius = Mathf.Clamp(lOEntry.Radius, 1, 3);

        KeepClearOfEdges(piTileX, piTileZ, ref liX, ref liY, liRadius, true);

        return new Vector3(
            UWViewpoint.TileToWorldAxis(piTileX) + (liX * UWObjectSpawner.SubTileScale) + UWObjectSpawner.SubTileOffset,
            0f,
            UWViewpoint.TileToWorldAxis(piTileZ) + (liY * UWObjectSpawner.SubTileScale) + UWObjectSpawner.SubTileOffset);
    }

    /// <summary>
    /// For a_create_object trap: the copy of the template keeps the template's position in the tile and HEIGHT
    /// and only moves to the new tile. That is what UW.EXE does (TriggerCreateObjectTrap,
    /// case 7: CopyArray, then MoveObjectToCoordinates with tile times 8 plus xpos/ypos of the
    /// template and its zpos). The reference rolls the spot anew (GetRandomXYZForTile)
    /// and puts everything on the floor - it deviates there. Noticed with Garamon's ghost, whose
    /// template stands at ZPos 24 and floats in the original (per user, 2026-09-13). Nothing ends up
    /// below the floor.
    /// </summary>
    public bool SpawnObjectInTileAtTemplatePosition(UWObject pOObject, int piTileX, int piTileZ)
    {
        if (pOObject == null || CurrentLevel == null || mOObjectRoot == null)
            return false;

        int liTemplateZ = pOObject.ZPos;

        Vector3 lOPosition = new Vector3(
            UWViewpoint.TileToWorldAxis(piTileX) + (pOObject.XPos * UWObjectSpawner.SubTileScale) + UWObjectSpawner.SubTileOffset,
            0f,
            UWViewpoint.TileToWorldAxis(piTileZ) + (pOObject.YPos * UWObjectSpawner.SubTileScale) + UWObjectSpawner.SubTileOffset);

        return fSpawnObjectAt(pOObject, piTileX, piTileZ, lOPosition, false, null, null, 0f, true, liTemplateZ);
    }

    /// <summary>Places an object in the centre of a tile - for create traps that give a
    /// target tile instead of a world position (see UWTriggerSystem).
    ///
    /// Does not go through SpawnDroppedObject: its path leads through WorldPositionToTile,
    /// and the tile is known here anyway. The original reason - the conversion was off by
    /// half a tile, which is why the first spawned monster got the floor height of the
    /// neighbouring tile (user, 2026-08-29) - has since been fixed at the root,
    /// see WorldPositionToTile.</summary>
    public bool SpawnObjectInTile(UWObject pOObject, int piTileX, int piTileZ)
    {
        if (pOObject == null || CurrentLevel == null || mOObjectRoot == null)
            return false;

        return fSpawnObjectAt(pOObject, piTileX, piTileZ, new Vector3(
            piTileX * UWLevelMeshBuilder.TileSpacing,
            0f,
            piTileZ * UWLevelMeshBuilder.TileSpacing), false, null);
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
        if (CurrentLevel == null || CurrentLevel.TileData == null || mOObjectRoot == null
            || piTileX < 0 || piTileX >= UWLevelMeshBuilder.TilesPerAxis
            || piTileZ < 0 || piTileZ >= UWLevelMeshBuilder.TilesPerAxis)
            return 0;

        UWTile lOTile = CurrentLevel.TileData[(piTileZ * UWLevelMeshBuilder.TilesPerAxis) + piTileX];

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid
            || lOTile.ObjectsInTile == null || lOTile.ObjectsInTile.Count == 0)
            return 0;

        UWObjectSpawner lOSpawner = fGetRuntimeSpawner();

        // Via a copy: a spawner can add something to the tile list.
        UWObject[] lOObjects = lOTile.ObjectsInTile.ToArray();

        int liChildCountBefore = mOObjectRoot.transform.childCount;

        foreach (UWObject lOObject in lOObjects)
            lOSpawner.SpawnSingleObject(piTileX, piTileZ, lOTile, lOObject, mOObjectRoot.transform);

        fApplyRenderModeFrom(liChildCountBefore);
        fRebuildEntityRegistry();

        return lOObjects.Length;
    }

    /// <summary>
    /// Removes an object from the world for good - from the object list of its tile and
    /// together with its display. For the a_delete object trap (see UWTriggerSystem).
    ///
    /// UWInventory.RemoveWorldObject does the same on pickup, but starts from the clicked
    /// UWEntityInfo; here only the data object is known.
    /// </summary>
    public bool RemoveObjectFromWorld(UWObject pOObject)
    {
        if (pOObject == null || CurrentLevel == null || CurrentLevel.TileData == null
            || pOObject.TileX < 0 || pOObject.TileX >= UWLevelMeshBuilder.TilesPerAxis
            || pOObject.TileY < 0 || pOObject.TileY >= UWLevelMeshBuilder.TilesPerAxis)
            return false;

        UWTile lOTile = CurrentLevel.TileData[(pOObject.TileY * UWLevelMeshBuilder.TilesPerAxis) + pOObject.TileX];

        bool lbRemoved = lOTile != null && lOTile.ObjectsInTile.Remove(pOObject);

        if (TryGetEntity(pOObject, out UWEntityInfo lOEntity) && lOEntity != null)
        {
            Destroy(lOEntity.gameObject);
            mEntityByData.Remove(pOObject);

            lbRemoved = true;
        }

        // A creature gone for good gives its slot back (UWLevel.ReleaseMobileSlot). Only
        // creatures: an item that leaves the world here may live on elsewhere - in an NPC's
        // inventory after a conversation - and the original counts carried things against the
        // level's slots anyway.
        if (lbRemoved && pOObject is UWNpc)
            CurrentLevel.ReleaseMobileSlot(pOObject);

        return lbRemoved;
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

        UWObject lOObject = new UWObject((ushort)piObjectId);

        try
        {
            lOObject.Texture = UWDataImporter.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, piObjectId);
        }
        catch
        {
            return null;
        }

        if (lOObject.Texture == null)
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
        // UWSpellProjectile), and a body of its own would get in the way.
        Collider lOCollider = lOSpawned.GetComponent<Collider>();

        if (lOCollider != null)
            lOCollider.enabled = false;

        fApplyRenderModeFrom(liBefore);

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

        UWObject lOObject = new UWObject((ushort)piObjectId);

        try
        {
            lOObject.Texture = UWDataImporter.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, piObjectId);
        }
        catch
        {
            return null;
        }

        if (lOObject.Texture == null)
            return null;

        int liBefore = mOObjectRoot.transform.childCount;

        // A projectile does not fall into the lava it starts above - see
        // fSpawnObjectAt.
        if (!SpawnDroppedObject(lOObject, pOWorldPosition, false, null, null, false))
            return null;

        UWTile lOTile = CurrentLevel.TileData[(lOObject.TileY * UWLevelMeshBuilder.TilesPerAxis) + lOObject.TileX];

        if (lOTile != null)
            lOTile.ObjectsInTile.Remove(lOObject);

        GameObject lOSpawned = null;

        for (int liChild = liBefore; liChild < mOObjectRoot.transform.childCount; liChild++)
        {
            lOSpawned = mOObjectRoot.transform.GetChild(liChild).gameObject;

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
    public bool SpawnEffectAt(int piObjectId, Vector3 pOWorldPosition, float pfSeconds)
    {
        if (UWDataImporter == null || mOObjectRoot == null)
            return false;

        UWObject lOObject = new UWObject((ushort)piObjectId);

        try
        {
            lOObject.Texture = UWDataImporter.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, piObjectId);
        }
        catch
        {
            return false;
        }

        if (lOObject.Texture == null)
            return false;

        int liBefore = mOObjectRoot.transform.childCount;

        if (!SpawnDroppedObject(lOObject, pOWorldPosition, false))
            return false;

        // An effect does NOT belong in the tile's object list - it disappears again right
        // away, and an entry without a GameObject would remain there.
        UWTile lOTile = CurrentLevel.TileData[(lOObject.TileY * UWLevelMeshBuilder.TilesPerAxis) + lOObject.TileX];

        if (lOTile != null)
            lOTile.ObjectsInTile.Remove(lOObject);

        // The spawner appends the new object at the end - everything that was added gets
        // its expiry time.
        for (int liChild = liBefore; liChild < mOObjectRoot.transform.childCount; liChild++)
        {
            GameObject lOSpawned = mOObjectRoot.transform.GetChild(liChild).gameObject;

            // Bring back to the given height: fSpawnObjectAt puts every object at
            // the floor height of its tile, because it is meant for dropped items.
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
    /// target tile (see UWObjectSpawner.HeightScale) - so the object lies
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
    /// pOFlightStartPosition (only set when throwing, see UWItemDrag): the object does land
    /// immediately with the correct end data in the tile object list, but additionally
    /// gets a UWThrownItemFlight attached, which makes it fly visibly from there to the
    /// already computed target position (confirmed from the original: a
    /// thrown item flies visibly through the air instead of appearing instantly).
    /// </summary>
    public bool SpawnDroppedObject(UWObject pOObject, Vector3 pOWorldPosition, bool pbRequireFlatFloor,
        Vector3? pOFlightStartPosition = null, int? piHeading = null, bool pbCanSinkInLiquid = true,
        bool pbRestOnBridge = false)
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

        return fSpawnObjectAt(pOObject, lOTargetTile.X, lOTargetTile.Y, pOWorldPosition,
            pbRequireFlatFloor, pOFlightStartPosition, null, 0f, pbCanSinkInLiquid,
            pbRestOnBridge ? fGetBridgeZPosBelow(pOWorldPosition) : -1);
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
            UWLevelMeshBuilder.CeilingHeight, ~0, QueryTriggerInteraction.Ignore);

        float lfBest = float.MinValue;

        foreach (RaycastHit lOHit in lOHits)
        {
            UWEntityInfo lOInfo = lOHit.collider.GetComponentInParent<UWEntityInfo>();

            if (lOInfo == null || lOInfo.ObjectData == null || lOInfo.ObjectData.ID != BridgeObjectId)
                continue;

            if (lOHit.point.y > lfBest)
                lfBest = lOHit.point.y;
        }

        return lfBest > float.MinValue ? Mathf.CeilToInt(lfBest / UWObjectSpawner.HeightScale) : -1;
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

        return Mathf.RoundToInt(Mathf.Repeat(lfYaw, 360f) / 45f) & 7;
    }

    /// <summary>
    /// Lets an item fall STRAIGHT down to the floor on the spot and
    /// leaves it lying there - for a projectile that has hit.
    ///
    /// Original (user, 2026-09-07): on a hit the crossbow bolt stays in
    /// the air for a very short moment, then falls vertically and can be picked up afterwards. The
    /// reference says the same - a projectile there is a perfectly normal object in the
    /// tile list (motion_projectile.PrepareProjectileObject inserts it there), not an
    /// effect that disappears on impact.
    ///
    /// Hence the same path as for a thrown item, only WITHOUT an arc: the trajectory
    /// goes from the impact point vertically down to the floor of the same tile.
    ///
    /// THE VALUES WERE READ FROM THE SAVE GAME, not guessed. The user picked up the bolt in the
    /// original, dropped it on tile 50/30 of the eighth level and saved
    /// (2026-09-07). In SAVE3 it is stored there as
    ///
    ///     idx 837  Id 17  is_quant 1  quantity 1  quality 63  zpos 96  owner 0
    ///
    /// with a tile floor height of 12, i.e. zpos 96 - a lying projectile sits exactly
    /// on the floor. Incidentally the index shows that the original reused the freed slot
    /// of the one-shot arrow trap; 837 was the trap itself before.
    ///
    /// THE QUALITY IS 63, NOT 0x28. ObjectCreator.PrepareNewObject does set 0x28,
    /// but InitMobileObject overwrites npc_hp with 0x3F for a mobile object, and
    /// on moving back into the static list that becomes the quality again
    /// (motion.cs: "if (IsStatic) quality = hp_1b"). A landed projectile is therefore
    /// always in full condition.
    ///
    /// THE TEXTURE MUST COME ALONG. A freshly created UWObject has none, and the spawner
    /// silently bails out for an object without texture (UWObjectSpawner, first line of
    /// SpawnSingleObject). Objects from the level data get it when loading; whoever
    /// creates one must fetch it themselves - SpawnFlyingObject does exactly that.
    /// Without it the bolt vanished without a trace on impact (per user, 2026-09-07).
    /// </summary>
    public bool DropObjectAt(int piObjectId, Vector3 pOWorldPosition, int piHeading)
    {
        if (CurrentLevel == null || mOObjectRoot == null || UWDataImporter == null)
            return false;

        // A lying projectile points in its direction of flight: the original continuously writes the
        // projectile's heading back into the object during flight
        // (motion.cs, ProjectileHeading), and when it comes to rest the heading stays.
        UWObject lOObject = new UWObject((ushort)piObjectId)
        {
            Quality = LandedProjectileQuality,
            HasQuantity = true,
            Quantity = 1,
            Heading = (ushort)(piHeading & 7)
        };

        try
        {
            lOObject.Texture = UWDataImporter.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, piObjectId);
        }
        catch
        {
            return false;
        }

        if (lOObject.Texture == null)
            return false;

        UWTilePos lOTargetTile = WorldPositionToTile(pOWorldPosition);

        return fSpawnObjectAt(lOObject, lOTargetTile.X, lOTargetTile.Y, pOWorldPosition, false,
            pOWorldPosition, 0f, mfImpactHoverSeconds, true, fGetBridgeZPosBelow(pOWorldPosition));
    }

    /// <summary>
    /// Like DropObjectAt, but for an item the player has thrown: the item itself lands,
    /// with everything it carries (see UWSpellProjectile.DropItemOnImpact).
    ///
    /// A BURNING LIGHT GOES OUT ON LANDING, not on the throw (per user, checked in the
    /// original, 2026-09-05). UW.EXE does the same when dropping: object class 9, index 4
    /// to 6 loses 4 from its number (DropOrThrowByPlayer_seg025_355, seg025_6C1).
    ///
    /// The direction is only written where it is visible - see SpawnDroppedObject.
    /// </summary>
    /// <summary>
    /// AN ITEM AT REST ON AN EDGE goes onto the tile that carries it (per user on the original,
    /// 2026-09-28: "items that fall like that snap onto the higher tile"). Ours slides with its
    /// radius over a ledge and can come to rest with its centre above the lower tile while it
    /// still lies at the height of the higher one; it was then given to the lower tile, hovered
    /// and fell after a pause - and showed over the higher floor meanwhile. Now, when the point
    /// lies more than a step above its own tile's floor, the nearest neighbour tile whose floor
    /// is at that height and within pfRadius takes it: the point moves just inside that tile.
    /// Returns false when no neighbour carries it (then it falls as before).
    /// </summary>
    public bool TrySnapOntoCarryingTile(ref Vector3 pOPoint, float pfRadius)
    {
        if (CurrentLevel == null)
            return false;

        const float lfStep = 4f;

        if (pOPoint.y - GetFloorHeightAt(pOPoint) <= lfStep)
            return false;

        UWTilePos lOAt = WorldPositionToTile(pOPoint);
        float lfHalf = UWLevelMeshBuilder.TileHalfSize;
        float lfInset = UWDataImport.UWData.UWWorldScale.SubTileStep * 0.5f;
        float lfBest = pfRadius + lfInset;
        bool lbFound = false;
        Vector3 lOBest = pOPoint;

        for (int liDy = -1; liDy <= 1; liDy++)
        {
            for (int liDx = -1; liDx <= 1; liDx++)
            {
                if (liDx == 0 && liDy == 0)
                    continue;

                int liX = lOAt.X + liDx;
                int liY = lOAt.Y + liDy;
                UWTile lOTile = CurrentLevel.GetTile(liX, liY);

                if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
                    continue;

                float lfCentreX = liX * UWLevelMeshBuilder.TileSpacing;
                float lfCentreZ = liY * UWLevelMeshBuilder.TileSpacing;
                Vector3 lOInside = new Vector3(
                    Mathf.Clamp(pOPoint.x, lfCentreX - lfHalf + lfInset, lfCentreX + lfHalf - lfInset),
                    pOPoint.y,
                    Mathf.Clamp(pOPoint.z, lfCentreZ - lfHalf + lfInset, lfCentreZ + lfHalf - lfInset));

                float lfDistance = Vector2.Distance(new Vector2(pOPoint.x, pOPoint.z), new Vector2(lOInside.x, lOInside.z));

                if (lfDistance > lfBest || Mathf.Abs(GetFloorHeightAt(lOInside) - pOPoint.y) > lfStep
                    || IsInsideRock(lOInside, lOInside, out Vector3 _))
                    continue;

                lfBest = lfDistance;
                lOBest = lOInside;
                lbFound = true;
            }
        }

        if (lbFound)
            pOPoint = lOBest;

        return lbFound;
    }

    public bool DropThrownItemAt(UWObject pOItem, Vector3 pOWorldPosition, int piHeading, bool pbAtRest = false)
    {
        if (pOItem == null || CurrentLevel == null || mOObjectRoot == null)
            return false;

        UWTilePos lOTargetTile = WorldPositionToTile(pOWorldPosition);
        UWTile lOTile = CurrentLevel.TileData[(lOTargetTile.Y * UWLevelMeshBuilder.TilesPerAxis) + lOTargetTile.X];

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
            return false;

        // Not into the solid half of a diagonal tile either - it would lie inside the wall.
        Vector3 lONormal;

        if (IsInsideRock(pOWorldPosition, pOWorldPosition, out lONormal))
            return false;

        // The item is stored on the eighth-tile grid, and the nearest grid point can lie just
        // across a diagonal wall even when the landing point does not. Then take the nearest
        // grid point of the tile that is free.
        if (!fTrySnapOutOfRock(ref pOWorldPosition, lOTargetTile.X, lOTargetTile.Y, pOItem.ID))
            return false;

        UWObjectMechanics.Extinguish(pOItem, UWDataImporter != null ? UWDataImporter.Textures : null);

        if (!UWLoreCheck.CanBeIdentified(pOItem,
                UWDataImporter != null ? UWDataImporter.CommonObjectProperties : null))
            pOItem.Heading = (ushort)(piHeading & 7);

        // AT REST it already lies on the ground: no hover, no gliding onto the sub-tile grid
        // (per user, 2026-09-15: "the moving into position looks odd"). Only the snap to the
        // grid remains - the original stores a resting object in eighth tiles, too.
        return fSpawnObjectAt(pOItem, lOTargetTile.X, lOTargetTile.Y, pOWorldPosition, false,
            pOWorldPosition, 0f, pbAtRest ? 0f : mfImpactHoverSeconds, true, fGetBridgeZPosBelow(pOWorldPosition),
            !pbAtRest);
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

        int liX = Mathf.RoundToInt(pOPoint.x / UWLevelMeshBuilder.TileSpacing);
        int liZ = Mathf.RoundToInt(pOPoint.z / UWLevelMeshBuilder.TileSpacing);

        UWTilePos lOFrom = WorldPositionToTile(pOFrom);

        if (liX < 0 || liX >= UWLevelMeshBuilder.TilesPerAxis || liZ < 0 || liZ >= UWLevelMeshBuilder.TilesPerAxis)
        {
            pONormal = fCrossedSide(lOFrom, liX, liZ);

            return true;
        }

        UWTile lOTile = CurrentLevel.TileData[(liZ * UWLevelMeshBuilder.TilesPerAxis) + liX];

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
        {
            pONormal = fCrossedSide(lOFrom, liX, liZ);

            return true;
        }

        // Position in the tile from -1 to 1, centre at zero, +z is north.
        float lfX = (pOPoint.x - (liX * UWLevelMeshBuilder.TileSpacing)) / UWLevelMeshBuilder.TileHalfSize;
        float lfZ = (pOPoint.z - (liZ * UWLevelMeshBuilder.TileSpacing)) / UWLevelMeshBuilder.TileHalfSize;

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
        int liRadius = 1;
        UWCommonObjectProperties.Entry lOEntry;

        if (UWDataImporter != null && UWDataImporter.CommonObjectProperties != null
            && UWDataImporter.CommonObjectProperties.TryGet(piObjectId, out lOEntry))
            liRadius = Mathf.Clamp(lOEntry.Radius, 1, 3);

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

    /// <summary>The world point of a sub-tile grid position - the inverse of fGetSubTilePos.</summary>
    private static Vector3 fGridPointToWorld(int piX, int piZ, int piTileX, int piTileZ, float pfHeight)
    {
        return new Vector3(
            UWViewpoint.TileToWorldAxis(piTileX) + (piX * UWObjectSpawner.SubTileScale) + UWObjectSpawner.SubTileOffset + 0.1f,
            pfHeight,
            UWViewpoint.TileToWorldAxis(piTileZ) + (piZ * UWObjectSpawner.SubTileScale) + UWObjectSpawner.SubTileOffset + 0.1f);
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
        UWTile lOAt = CurrentLevel.TileData[(lOTile.Y * UWLevelMeshBuilder.TilesPerAxis) + lOTile.X];

        if (lOAt == null || lOAt.TileType == UWTile.TileTypeEnum.solid)
            return false;

        return !pbRequireFlatFloor || !fIsUnevenFloor(lOAt.TileType);
    }

    /// <summary>Quality of a landed projectile - see DropObjectAt.</summary>
    private const ushort LandedProjectileQuality = 0x3F;

    [SerializeField]
    [Tooltip("How long a projectile that has hit hangs in the air before it falls.")]
    private float mfImpactHoverSeconds = 0.25f;

    /// <summary>Shared core of SpawnDroppedObject, SpawnObjectInTile and the other Spawn/Drop
    /// methods that put an object into a tile list (SpawnObjectById, SpawnObjectInTileScattered,
    /// SpawnObjectInTileAtTemplatePosition, DropObjectAt). The target tile
    /// is PASSED IN instead of being determined here from the world position: whoever already
    /// knows the tile should not have to recompute it via a round trip (see
    /// SpawnObjectInTile).</summary>
    private bool fSpawnObjectAt(UWObject pOObject, int liTileX, int liTileZ, Vector3 pOWorldPosition,
        bool pbRequireFlatFloor, Vector3? pOFlightStartPosition, float? pfFlightArcHeight = null,
        float pfFlightDelay = 0f, bool pbCanSinkInLiquid = true, int piMinimumZPos = -1, bool pbAnimateFlight = true)
    {
        if (liTileX < 0 || liTileX >= UWLevelMeshBuilder.TilesPerAxis
            || liTileZ < 0 || liTileZ >= UWLevelMeshBuilder.TilesPerAxis)
            return false;

        UWTile lOTile = CurrentLevel.TileData[(liTileZ * UWLevelMeshBuilder.TilesPerAxis) + liTileX];

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
            return false;

        if (pbRequireFlatFloor && fIsUnevenFloor(lOTile.TileType))
            return false;

        // A SLOT FOR IT, as GetFreeObject hands one out - culling far objects when the level is
        // full, and refusing the object when even that frees nothing (UWObjectLimitRules).
        if (!fReserveSlot(pOObject))
            return false;

        // A creature made at runtime (a trap's copy, a summoning) takes its index now, as the
        // original's GetFreeObject hands it out - see UWLevel.TryPlaceInFreeMobileSlot.
        if (pOObject is UWNpc && CurrentLevel.Masterlist != null && CurrentLevel.Masterlist.IndexOf(pOObject) < 0)
            CurrentLevel.TryPlaceInFreeMobileSlot(pOObject, out int _);

        // A flight start still counts as "thrown" (talisman in lava), but an item that has
        // already come to rest does not hover and glide to its spot any more - see
        // DropThrownItemAt.
        bool lbAnimate = pOFlightStartPosition.HasValue && pbAnimateFlight;

        pOObject.TileX = liTileX;
        pOObject.TileY = liTileZ;
        pOObject.XPos = fGetSubTilePos(pOWorldPosition.x, liTileX);
        pOObject.YPos = fGetSubTilePos(pOWorldPosition.z, liTileZ);
        pOObject.ZPos = Mathf.RoundToInt(fGetFloorHeightAt(lOTile, pOWorldPosition.x, pOWorldPosition.z, liTileX, liTileZ) / UWObjectSpawner.HeightScale);

        // Keep a template's height if it lies above the floor (see
        // SpawnObjectInTileAtTemplatePosition), or a bridge's (see fGetBridgeZPosBelow).
        bool lbAboveFloor = piMinimumZPos > pOObject.ZPos;

        if (lbAboveFloor)
            pOObject.ZPos = piMinimumZPos;

        // In water and lava only what actually sinks is lost - see
        // UWCommonObjectProperties.Entry.SinksInLiquid. The entry is still added first
        // and removed afterwards, so that the spawner sees the same tile list
        // as usual - just like with the short-lived effects in
        // SpawnEffectAt.
        // A FLYING PROJECTILE DOES NOT SINK, even if it starts above lava: it
        // does not lie there, it only flies over it. Without this exception the tile swallowed
        // the fire elemental's fireball the moment it was thrown - over lava it seemingly
        // did not throw at all, one tile further it did (per user, 2026-09-10).
        // Nothing sinks that lies ABOVE the floor - on a bridge over the water it stays dry.
        // LAVA SPARES quality class 3 and fire-resistant objects before it culls (keys, incense,
        // strong thread - Tybal died on lava in his lair and took them with him, per user
        // 2026-09-24); see UWLiquidCulling.SparedByLava.
        // A THROWN object is tested twice in water, the landing and the collision after it
        // (UWLiquidCulling, settled per user 2026-10-03 with stacks of emeralds).
        bool lbSinks = pbCanSinkInLiquid && !lbAboveFloor
            && TileQueries.TileSwallows(lOTile, pOObject, pOFlightStartPosition.HasValue);

        // ENDGAME: a talisman that falls into the volcano's lava on level 8 perishes -
        // otherwise a talisman never sinks (UWTalismans). Then it disappears like a
        // sinking item, with a trajectory only on impact (see UWEndgame).
        // ONLY WHEN THROWN: dropped, it stays lying in the lava and nothing happens (per
        // user in the original, 2026-09-14). In UW.EXE the check hangs on the impact of a
        // projectile (ObjectHitsFloorTileDestroyTalismans_seg029_C6F).
        if (pbCanSinkInLiquid && !lbSinks && pOFlightStartPosition.HasValue && IsLavaTile(lOTile)
            && UWEndgame.TryDestroyTalismanInLava(this, pOObject, liTileX, liTileZ, pOWorldPosition,
                lbAnimate ? mfThrowFlightDuration + pfFlightDelay : 0f))
            lbSinks = true;

        lOTile.ObjectsInTile.Add(pOObject);

        int liChildCountBefore = mOObjectRoot.transform.childCount;

        UWObjectSpawner lOSpawner = fGetRuntimeSpawner();
        lOSpawner.SpawnSingleObject(liTileX, liTileZ, lOTile, pOObject, mOObjectRoot.transform);

        // SpawnSingleObject always appends new objects as the last child via SetParent -
        // no return value on the actual spawn path is needed to get at the freshly created
        // GameObject.
        Transform lOSpawnedTransform = mOObjectRoot.transform.childCount > liChildCountBefore
            ? mOObjectRoot.transform.GetChild(mOObjectRoot.transform.childCount - 1)
            : null;

        fApplyRenderModeFrom(liChildCountBefore);

        // Into the registry (TryGetEntity) - otherwise after a conversation nobody finds the newly
        // created creature, and it never reads its new attitude: Garamon stayed friendly
        // instead of mellow (per user, compared with SAVE4, 2026-09-13).
        if (lOSpawnedTransform != null && mEntityByData != null)
        {
            foreach (UWEntityInfo lOEntity in lOSpawnedTransform.GetComponentsInChildren<UWEntityInfo>(true))
            {
                if (lOEntity.ObjectData != null)
                    mEntityByData[lOEntity.ObjectData] = lOEntity;
            }
        }

        // THE SPLASH belongs to what FALLS into the water, not to what is put down there.
        // The original spawns it in the landing of a flying object
        // (ObjectHitsFloorTileDestroyTalismans_seg029_C6F, labels C96 to CC8: the object's tile
        // state, byte 0x0A bits 4-6, equal to 1 gives the class-7 object with offset 6), while
        // remains and loot are put down by the motion code and go without one. That is the
        // user's measurement of 2026-09-20: a drowning gave exactly ONE splash, the creature's
        // own, although what it left behind went into the water as well.
        //
        // LAVA GETS NONE: there the original spawns no picture, it burns what it does not spare
        // (value 6 of PlacedObjectCollison_seg029_104D: class 3 and fire-resistant objects
        // stay, the rest is culled - see UWLiquidCulling.SparedByLava).
        bool lbSplashes = pOFlightStartPosition.HasValue && IsWaterTile(lOTile);

        if (lOSpawnedTransform != null && lbAnimate)
        {
            lOSpawnedTransform.gameObject.AddComponent<UWThrownItemFlight>()
                .Begin(pOFlightStartPosition.Value, lOSpawnedTransform.position, mfThrowFlightDuration,
                    pfFlightArcHeight ?? mfThrowFlightArcHeight, lbSinks, pfFlightDelay,
                    lbSplashes ? (System.Action<Vector3>)fSplashAt : null);
        }
        else if (lbSplashes)
        {
            // Without a trajectory the landing is NOW; with one the flight reports it.
            fSplashAt(lOSpawnedTransform != null ? lOSpawnedTransform.position : pOWorldPosition);
        }

        if (lbSinks)
        {
            lOTile.ObjectsInTile.Remove(pOObject);

            if (lOSpawnedTransform != null && !lbAnimate)
                Destroy(lOSpawnedTransform.gameObject);
        }

        return true;
    }

    /// <summary>
    /// The splash of something that falls into water: the class-7 picture 0x1C6 ("a_splash",
    /// UWObjectMechanics.SplashEffectObjectId) at the landing point and effect 5 with it.
    ///
    /// THE SOUND took three goes and in the end the user's ear settled it (2026-09-21, from
    /// the rendered effects of a sound dump tool): not effect 0, which is the rushing one
    /// hears while IN the water, and not the bump 0x0F, but effect 5 - which is where UW.EXE
    /// plays it, for an object that comes to rest while falling (seg030_2B26, labels D3E to
    /// D7F). The branch that draws the picture itself calls no sound at all.
    ///
    /// IT BELONGS TO THE LANDING, NOT TO THE SINKING: the original spawns the picture as soon
    /// as a flying object comes to rest on a water tile
    /// (ObjectHitsFloorTileDestroyTalismans_seg029_C6F, labels C96 to CC8), before the culling
    /// decides whether the object survives - so something that stays afloat splashes too.
    /// </summary>
    public void SplashAt(Vector3 pOAt)
    {
        fSplashAt(pOAt);
    }

    private void fSplashAt(Vector3 pOAt)
    {
        UWSoundEffects.PlayAt(UWSoundEffects.Splash, pOAt);

        SpawnEffectAt(UWObjectMechanics.SplashEffectObjectId, pOAt, SplashEffectSeconds);
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
    /// "tile*TileSpacing - TileHalfSize + 0.1 + subPos*SubTileScale + SubTileOffset" formula: which of the 8
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

        for (int z = 0; z < UWLevelMeshBuilder.TilesPerAxis; z++)
        {
            for (int x = 0; x < UWLevelMeshBuilder.TilesPerAxis; x++)
            {
                UWTile lOTile = lOLevel.TileData[(z * UWLevelMeshBuilder.TilesPerAxis) + x];

                for (int i = 0; i < lOTile.ObjectsInTile.Count; i++)
                {
                    UWObject lOTrigger = lOTile.ObjectsInTile[i];

                    if (lOTrigger == null)
                        continue;

                    // CREATE TRAPS get NOTHING here. Until 2026-09-06 there was a
                    // tile volume here that fired on entering - that does not exist in the original.
                    // Whoever has a trigger is addressed through it; the
                    // rest are refilled by UWCreatureRespawner on the clock.
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
    /// Places a WARD RUNE in a tile - the Rune of Warding spell (see
    /// UWSummonSpell).
    ///
    /// IT CONSISTS OF TWO OBJECTS, just as in the reference: a move trigger
    /// (0x1A0) with EMPTY FLAGS, so that the player does not set it off himself, and attached to it
    /// via the special link an a_ward trap (0x189) with quality 0x3F, i.e. "on every
    /// creature". Because both are in the level data and not only in the display,
    /// the rune survives a rebuild of the level.
    ///
    /// THE TRAP IS APPENDED TO THE END OF THE MASTER LIST and lies in no tile - the
    /// reference does the same (it takes a free slot from its object table
    /// and enters it nowhere). That the index can grow beyond the 1023 of the original file
    /// has no consequences: we do not write the object lists back anywhere.
    /// Side effect of the same construction: the trap stays, even though the empty flags of the
    /// trigger actually mark it as one-shot - the cleanup only works within a
    /// tile list (see UWTriggerSystem.fRemoveOneShotTrap). So the rune goes off
    /// multiple times.
    /// </summary>
    public bool SpawnWardRune(int piTileX, int piTileZ)
    {
        if (CurrentLevel == null || CurrentLevel.TileData == null || CurrentLevel.Masterlist == null
            || mOLevelRoot == null
            || piTileX < 0 || piTileX >= UWLevelMeshBuilder.TilesPerAxis
            || piTileZ < 0 || piTileZ >= UWLevelMeshBuilder.TilesPerAxis)
            return false;

        UWTile lOTile = CurrentLevel.TileData[(piTileZ * UWLevelMeshBuilder.TilesPerAxis) + piTileX];

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
            return false;

        UWObject lOTrap = new UWObject((ushort)UWObjectMechanics.WardTrapId);

        lOTrap.Quality = UWObjectMechanics.WardTrapAnyClass;
        lOTrap.XPos = 3;
        lOTrap.YPos = 3;

        CurrentLevel.Masterlist.Add(lOTrap);

        UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.MoveTriggerId);

        lOTrigger.Flags = 0;
        lOTrigger.Quantity = (ushort)(CurrentLevel.Masterlist.Count - 1);

        // The trigger's target tile is its own - the reference sets it that way too.
        lOTrigger.Quality = (ushort)piTileX;
        lOTrigger.Owner = (ushort)piTileZ;

        if (!SpawnObjectInTile(lOTrigger, piTileX, piTileZ))
            return false;

        Transform lOTriggerRoot = mOLevelRoot.transform.Find("Transitions");

        if (lOTriggerRoot == null)
            return false;

        fCreateMoveTrigger(lOTriggerRoot, piTileX, piTileZ, lOTile, lOTrigger);

        return true;
    }

    /// <summary>Trigger volume for a move trigger, level transitions included (see
    /// fSpawnTransitions). The volume sits around the trigger's sub-tile point, not over the
    /// whole tile - see fCreateTriggerVolume.</summary>
    private void fCreateMoveTrigger(Transform pOParent, int xPos, int zPos, UWTile pOTile, UWObject pOTriggerData)
    {
        GameObject lOObject = fCreateTriggerVolume(pOParent, xPos, zPos, pOTile, pOTriggerData,
            string.Format("Move trigger ({0},{1})", xPos, zPos));

        lOObject.AddComponent<UWMoveTrigger>().Initialise(this, pOTriggerData,
            fGetTriggerReach(pOTriggerData.ID));
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
            pOTile.FloorHeight + UWLevelMeshBuilder.TileHalfSize,
            lOAt.z);

        float lfReach = fGetTriggerReach(pOTriggerData.ID);

        BoxCollider lOCollider = lOObject.AddComponent<BoxCollider>();
        lOCollider.isTrigger = true;
        lOCollider.size = new Vector3(lfReach * 2f, UWLevelMeshBuilder.TileSpacing, lfReach * 2f);

        return lOObject;
    }

    /// <summary>One eighth-tile in world units - the unit in which comobj.dat stores radii,
    /// and the same one in which the sub-tile position counts (see
    /// UWObjectSpawner.SubTileScale).</summary>
    private const float EighthTile = UWObjectSpawner.SubTileScale;

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
        mOBillboardMaterial.SetFloat("_AmbientFloor", lfAmbient);
        mOBillboardMaterial.SetFloat("_PointLightCap", lfPointLightCap);

        // Translucent sprites (mist, ghosts - see UWTransparencyTables) use
        // the same shader, only with a different blend state: alpha blending instead of a hard
        // cutoff, no depth writing, and drawn in the transparent queue, so that the
        // wall behind is already there. The cutoff stays just above zero, so that fully
        // transparent pixels are still discarded.
        mOTranslucentBillboardMaterial = new Material(lOBillboardShader);
        mOTranslucentBillboardMaterial.name = "UW Objects (translucent)";
        mOTranslucentBillboardMaterial.mainTexture = mOObjectAtlas.Atlas;
        mOTranslucentBillboardMaterial.SetFloat("_AmbientFloor", lfAmbient);
        mOTranslucentBillboardMaterial.SetFloat("_PointLightCap", lfPointLightCap);
        mOTranslucentBillboardMaterial.SetFloat("_Cutoff", 0.01f);
        mOTranslucentBillboardMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mOTranslucentBillboardMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mOTranslucentBillboardMaterial.SetFloat("_ZWrite", 0f);
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
        mOModelSolidMaterial.SetFloat("_AmbientFloor", lfAmbient);
        mOModelSolidMaterial.SetFloat("_PointLightCap", lfPointLightCap);

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
        mOPortcullisMaterial.SetFloat("_AmbientFloor", lfAmbient);
        mOPortcullisMaterial.SetFloat("_PointLightCap", lfPointLightCap);
        mOPortcullisMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);

        mODecalMaterial = new Material(lODecalShader);
        mODecalMaterial.name = "UW Decal";
        mODecalMaterial.mainTexture = mOObjectAtlas.Atlas;
        mODecalMaterial.SetFloat("_AmbientFloor", lfAmbient);
        mODecalMaterial.SetFloat("_PointLightCap", lfPointLightCap);

        // THE PALETTE INDEX FOR REMASTERED (UWHallucination.hlsl): the hallucination's pictures and
        // the ghosts' tables need it there too - from the index atlas for sprites and decals, from
        // the vertex for the model faces and the portcullis. The palette path swaps these
        // materials and sets its own.
        foreach (Material lOMaterial in new[] { mOBillboardMaterial, mOTranslucentBillboardMaterial, mODecalMaterial })
            lOMaterial.SetTexture("_IndexTex", mOObjectAtlas.IndexAtlas != null ? mOObjectAtlas.IndexAtlas : Texture2D.blackTexture);

        mOModelSolidMaterial.SetFloat("_UWVertexIndex", 1f);
        mOPortcullisMaterial.SetFloat("_UWVertexIndex", 1f);
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
        int liTileX = Mathf.Clamp(piPositionX >> 8, 0, UWLevelMeshBuilder.TilesPerAxis - 1);
        int liTileZ = Mathf.Clamp(piPositionY >> 8, 0, UWLevelMeshBuilder.TilesPerAxis - 1);

        float lfFine = UWLevelMeshBuilder.TileSpacing / 256f;

        Vector3 lOAt = new Vector3(
            UWViewpoint.TileToWorldAxis(liTileX) + ((piPositionX & 0xFF) * lfFine),
            0f,
            UWViewpoint.TileToWorldAxis(liTileZ) + ((piPositionY & 0xFF) * lfFine));

        float lfFloor = GetFloorHeightAt(lOAt);
        float lfSaved = UWDataImport.UWData.UWUnits.ZPosToWorld(piZPosition);
        float lfStand = lfSaved > lfFloor && lfSaved - lfFloor <= SavedHeightTolerance ? lfSaved : lfFloor;

        fPlacePlayerOnFloor(lOAt, lfStand);
    }

    /// <summary>One floor step, eight zpos units - see fPlacePlayerAtSavedPosition.</summary>
    private const float SavedHeightTolerance = 8f * UWDataImport.UWData.UWWorldScale.ZPosStep;

    private void fPlacePlayerAtTile(int piTileX, int piTileZ)
    {
        piTileX = Mathf.Clamp(piTileX, 0, UWLevelMeshBuilder.TilesPerAxis - 1);
        piTileZ = Mathf.Clamp(piTileZ, 0, UWLevelMeshBuilder.TilesPerAxis - 1);

        UWTile lOTile = CurrentLevel.TileData[(piTileZ * UWLevelMeshBuilder.TilesPerAxis) + piTileX];

        Vector3 lOWallSetback = fGetSpawnSetbackFromWall(lOTile, piTileX, piTileZ);

        fPlacePlayerOnFloor(new Vector3(
            (piTileX * UWLevelMeshBuilder.TileSpacing) + lOWallSetback.x,
            0f,
            (piTileZ * UWLevelMeshBuilder.TileSpacing) + lOWallSetback.z));
    }

    /// <summary>
    /// Places the character so that the bottom of its CharacterController stands exactly on the
    /// floor - the floor from the tile data, interpolated on slopes
    /// (GetFloorHeightAt), the dimensions from the controller itself.
    ///
    /// Until 2026-09-11 there was a fixed offset of 34 above the tile floor here. But the
    /// controller has its centre at the origin, the feet lie half a height below,
    /// and the character fell a bit after every load and level change (per user). As long as
    /// a lock held gravity, it even hung there in mid-air (see
    /// UWPlayerMovement.Update).
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
        float lfFootOffset = 0f;

        if (lOPlayer != null)
        {
            CharacterController lOController = lOPlayer.GetComponent<CharacterController>();

            if (lOController != null)
            {
                float lfBottom = lOController.center.y
                    - Mathf.Max(lOController.height * 0.5f, lOController.radius);

                lfFootOffset = (-lfBottom * lOPlayer.transform.lossyScale.y) + lOController.skinWidth;
            }
        }

        pOPosition.y = pfStandHeight + lfFootOffset;

        fPlacePlayer(pOPosition);
    }

    /// <summary>
    /// When landing on a transition tile, one would otherwise stand in the centre - i.e. right at
    /// the wall to the (unwalkable) trap tile, much too far forward. A small offset away from
    /// this wall gives distance, as when leaving a staircase in the original.
    /// </summary>
    private Vector3 fGetSpawnSetbackFromWall(UWTile pOTile, int piTileX, int piTileZ)
    {
        const float lfSpawnSetback = 16f;

        System.Collections.Generic.List<UWObject> lOMaster = CurrentLevel.Masterlist;

        for (int i = 0; i < pOTile.ObjectsInTile.Count; i++)
        {
            UWObject lOTrigger = pOTile.ObjectsInTile[i];

            if (lOTrigger == null || lOTrigger.ID < TriggerFirstId || lOTrigger.ID > TriggerLastId)
                continue;

            int liLink = lOTrigger.Quantity;

            if (liLink <= 0 || liLink >= lOMaster.Count)
                continue;

            UWObject lOTrap = lOMaster[liLink];

            if (lOTrap == null || lOTrap.ID != TeleportTrapId)
                continue;

            int liDx = System.Math.Sign(lOTrap.TileX - piTileX);
            int liDz = System.Math.Sign(lOTrap.TileY - piTileZ);

            return new Vector3(-liDx * lfSpawnSetback, 0f, -liDz * lfSpawnSetback);
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Takes over quest flags and game variables from the save game.
    ///
    /// Without this every loaded game starts with empty puzzles: the lever positions
    /// come from LEV.ARK and are correct, but the variables behind them were at zero.
    /// A solved puzzle thus counted as unsolved (per user, 2026-09-07, at the three
    /// levers on level 5, tile 6/29).
    ///
    /// Both stores are cleared first - otherwise the state of a previously
    /// loaded game would mix in.
    /// </summary>
    private static void fSeedStoredState(UWPlayerData pOPlayer)
    {
        UWQuestFlags.Clear();
        UWGameVariables.Clear();
        UWGameFlags.SeedFrom(pOPlayer);
        UWEndgame.SeedFrom(pOPlayer);

        // Music and sound belong to the SAVE in the original, not to the program - a fresh
        // character carries them too, both on (UWPlayerData.SettingsOffset, NewSettings 0x35).
        if (pOPlayer.IsLoaded)
            UWSoundOptions.ApplyFromSave(pOPlayer.MusicEnabled, pOPlayer.SoundEnabled);

        for (int liAt = 0; liAt < pOPlayer.QuestFlags; liAt++)
        {
            int liValue = pOPlayer.GetQuestFlag(liAt);

            if (liValue != 0)
                UWQuestFlags.Set(liAt, liValue);
        }

        for (int liAt = 0; liAt < pOPlayer.GameVariables; liAt++)
        {
            int liValue = pOPlayer.GetGameVariable(liAt);

            if (liValue != 0)
                UWGameVariables.Set(liAt, liValue);
        }
    }

    /// <summary>A full circle in the heading of PLAYER.DAT - see
    /// UWPlayerData.WritePosition.</summary>
    private const float HeadingSteps = 65536f;

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

        if (lOMovement != null)
            lOMovement.ResetVerticalVelocity();
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
            + UWGameUI.ProgressDots
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
