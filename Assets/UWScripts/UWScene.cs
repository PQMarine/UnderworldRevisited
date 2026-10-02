using UnityEngine;

/// <summary>
/// The one place that knows where the scene's services are. Until 2026-09-18 every class
/// searched for them itself - GameObject.Find("Level") in ten files, FindAnyObjectByType in
/// twenty, UWItemDrag alone thirteen times, some of them on every call - which hid who
/// depends on whom and cost a scene search each time.
///
/// The lookups are cached and repeated only when the cached object is gone or inactive, so
/// they answer exactly what FindAnyObjectByType and GameObject.Find answered: the active
/// instance, or null. A scene reload therefore needs no reset - the old objects are
/// destroyed, the next read finds the new ones.
/// </summary>
public static class UWScene
{
    /// <summary>The object carrying UWLevelLoader, UWLighting and the palette renderer.</summary>
    public const string LevelObjectName = "Level";

    /// <summary>The player body with its CharacterController.</summary>
    public const string PlayerObjectName = "First Person Controller";

    /// <summary>The free camera of the debug tools.</summary>
    public const string SpectatorCameraTag = "SpectatorCam";

    private static GameObject msLevelObject;
    private static GameObject msPlayer;
    private static GameObject msSpectatorCamera;

    private static UWLevelLoader msLevelLoader;
    private static UWCharacter msCharacter;
    private static UWInventory msInventory;
    private static UWGameUI msGameUi;
    private static Interaction msInteraction;
    private static UWItemDrag msItemDrag;
    private static UWIntroPlayer msIntroPlayer;
    private static UWControlScheme msControlScheme;
    private static UWLighting msLighting;
    private static UWPlayerTerrain msPlayerTerrain;
    private static UWPlayerMovement msPlayerMovement;
    private static UWMainMenu msMainMenu;
    private static UWConversationScreen msConversationScreen;

    public static GameObject LevelObject
    {
        get
        {
            if (!fIsLive(msLevelObject))
                msLevelObject = GameObject.Find(LevelObjectName);

            return msLevelObject;
        }
    }

    public static GameObject Player
    {
        get
        {
            if (!fIsLive(msPlayer))
                msPlayer = GameObject.Find(PlayerObjectName);

            return msPlayer;
        }
    }

    public static GameObject SpectatorCamera
    {
        get
        {
            if (!fIsLive(msSpectatorCamera))
                msSpectatorCamera = GameObject.FindGameObjectWithTag(SpectatorCameraTag);

            return msSpectatorCamera;
        }
    }

    /// <summary>The camera that shows the world: the main camera, else the spectator camera,
    /// else null.</summary>
    public static Camera ActiveCamera
    {
        get
        {
            if (Camera.main != null)
                return Camera.main;

            GameObject lOSpectator = SpectatorCamera;

            return lOSpectator != null ? lOSpectator.GetComponent<Camera>() : null;
        }
    }

    public static UWLevelLoader LevelLoader => fGet(ref msLevelLoader);

    public static UWCharacter Character => fGet(ref msCharacter);

    public static UWInventory Inventory => fGet(ref msInventory);

    public static UWGameUI GameUi => fGet(ref msGameUi);

    public static Interaction Interaction => fGet(ref msInteraction);

    public static UWItemDrag ItemDrag => fGet(ref msItemDrag);

    public static UWIntroPlayer IntroPlayer => fGet(ref msIntroPlayer);

    public static UWControlScheme ControlScheme => fGet(ref msControlScheme);

    public static UWLighting Lighting => fGet(ref msLighting);

    public static UWPlayerTerrain PlayerTerrain => fGet(ref msPlayerTerrain);

    public static UWPlayerMovement PlayerMovement => fGet(ref msPlayerMovement);

    public static UWMainMenu MainMenu => fGet(ref msMainMenu);

    public static UWConversationScreen ConversationScreen => fGet(ref msConversationScreen);

    /// <summary>All active creatures. Still a scene search - creatures come and go - but
    /// only this one place does it.</summary>
    public static UWCritter[] FindCritters()
    {
        return Object.FindObjectsByType<UWCritter>(FindObjectsInactive.Exclude);
    }

    private static UWDataImport.UWData.UWCritterClock msCritterClock = new UWDataImport.UWData.UWCritterClock();

    private static UWDataImport.UWData.UWCritterAlarm msCritterAlarm = new UWDataImport.UWData.UWCritterAlarm();

    /// <summary>The 16-slot clock of the level's creatures (Docs/AI/brain-and-host.md
    /// section 1). UWCritterDriver advances it once per frame; every UWCritter asks it
    /// whether it is due. One per level: ResetCritterServices replaces it when a level is
    /// built.</summary>
    public static UWDataImport.UWData.UWCritterClock CritterClock => msCritterClock;

    /// <summary>The kin alarm of the level (UWCritterBrain.OnDamaged writes it, every hostile
    /// creature reads it). The original saves it with the game (PLAYER.DAT 0xBA-0xC1); the
    /// port starts it fresh with every level build.</summary>
    public static UWDataImport.UWData.UWCritterAlarm CritterAlarm => msCritterAlarm;

    /// <summary>A fresh clock and alarm for a newly built level - so nothing of the old level
    /// survives loading a savegame (per user, 2026-09-12: a rat attacked at once after
    /// loading because the alarm was still fresh).</summary>
    public static void ResetCritterServices()
    {
        msCritterClock = new UWDataImport.UWData.UWCritterClock();
        msCritterAlarm = new UWDataImport.UWData.UWCritterAlarm();
    }

    private static T fGet<T>(ref T poCache) where T : Component
    {
        if (poCache == null || !poCache.gameObject.activeInHierarchy)
            poCache = Object.FindAnyObjectByType<T>();

        return poCache;
    }

    private static bool fIsLive(GameObject pOObject)
    {
        return pOObject != null && pOObject.activeInHierarchy;
    }
}
