using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Sleeping in the bedroll on the Unity side. The sequence, the recovery and the dreams are
/// engine-free in UWSleepRules since 2026-09-18 (P3 of the engine separation); this class
/// keeps the old entry point. Since cleanup step 3.6 (2026-10-17) the rules ask the world
/// (UWWorld) for everything around the sleeper - the ground, the creatures, the doors, the music,
/// the respawn, the dream and the night's black - and the sleep's own host (IUWSleepHost) is gone.
/// </summary>
public static class UWSleep
{
    /// <summary>The bedroll.</summary>
    public const int BedrollObjectId = UWSleepRules.BedrollObjectId;

    /// <summary>
    /// Goes to sleep. Returns false if it is not possible at all - then a message
    /// is already in the message scroll.
    /// </summary>
    public static bool Sleep(DataImport pOData, UWCharacter pOCharacter, Interaction pOInteraction,
        UWLevelLoader pOLevelLoader, bool pbPassedOut = false, bool pbBedroll = false)
    {
        if (pOData == null || pOCharacter == null || pOInteraction == null || pOLevelLoader == null)
            return false;

        UWWorld lOWorld = UWWorld.Of(pOLevelLoader);

        // The night's presentation writes to the Interaction Sleep was handed. That one is never
        // null here (Sleep refuses without it), which is why the night's AddGeneralMessage, alone
        // of the five copies the hosts had, never had a null check - never the silent scope.
        IUWWorld lONight = UWScopedWorld.With(lOWorld, UWPresentation.For(lOWorld, pOInteraction));

        // 1-based (the dungeon level the dreams are chosen by); the loader is there, so is the world.
        return UWSleepRules.Sleep(pOCharacter.Vitals, UWWorldLevels.NumberOr(lOWorld, 1), lONight, pbPassedOut,
            pbBedroll);
    }
}
