using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S SCHEDULES ON THE UNITY SIDE (UW2 plan G6 part 1, 2026-11-16): what UWScheduleRulesUw2 asks of the game
/// (IUWScheduleHost) and the moments the original brings them up to date - after a conversation (free_converse), a level
/// change (ChangeLevel), passing time (pass_time) and on the day clock's step (duration_check's hour). The schedules
/// themselves are the session's (UWScheduleBookUw2.Of).
/// </summary>
public sealed class UWScheduleHostUw2 : IUWScheduleHost
{
    private readonly UWLevelLoader mOLoader;

    private readonly UWObject mOTalkingTo;

    private IUWTrapHost mITraps;

    private UWScheduleHostUw2(UWLevelLoader pOLoader, UWObject pOTalkingTo)
    {
        mOLoader = pOLoader;
        mOTalkingTo = pOTalkingTo;
    }

    /// <summary>A host for the castle hacks (36, 37) fired from a trap; null outside the second game's session.</summary>
    public static UWScheduleHostUw2 ForTraps(UWLevelLoader pOLoader)
    {
        return fCanRun(pOLoader) ? new UWScheduleHostUw2(pOLoader, null) : null;
    }

    /// <summary>Sched_SetAllClocks(1): every schedule up to date for the current level. pOTalkingTo: the partner of the
    /// conversation that just ended, whom a kill row spares.</summary>
    public static void RunAll(UWObject pOTalkingTo = null)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWScheduleBookUw2 lOBook = UWScheduleBookUw2.Of(UWGameSession.Current);

        if (lOBook == null || !fCanRun(lOLoader))
            return;

        int liError = UWScheduleRulesUw2.SetAllClocks(lOBook, new UWScheduleHostUw2(lOLoader, pOTalkingTo));

        if (liError != 0)
            UnityEngine.Debug.Log("[Session] The schedules stopped with " + liError + ".");
    }

    /// <summary>duration_check's hour: block 0 after X clock 0 stepped on.</summary>
    public static void StepDay()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWScheduleBookUw2 lOBook = UWScheduleBookUw2.Of(UWGameSession.Current);

        if (lOBook != null && fCanRun(lOLoader))
            UWScheduleRulesUw2.StepDay(lOBook, new UWScheduleHostUw2(lOLoader, null));
    }

    private static bool fCanRun(UWLevelLoader pOLoader)
    {
        return UWGameStartUw2.IsRunning && pOLoader != null && pOLoader.CurrentLevel != null && UWGameSession.Current != null
            && UWGameSession.Current.PlayerFileUw2 != null && UWWorld.Of(pOLoader) != null;
    }

    // ------------------------------------------------- IUWScheduleHost

    public UWPlayerFile Record => UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;

    public int LevelNumber => mOLoader.CurrentLevelIndex + 1;

    public IUWWorld World => UWWorld.Of(mOLoader);

    public IUWTrapHost Traps => mITraps ?? (mITraps = UWTriggerSystem.ScheduleTrapHostUw2(mOLoader));

    public UWObjectClassProperties Properties => mOLoader.UWDataImporter != null ? mOLoader.UWDataImporter.ObjectClassProperties : null;

    public UWObject TalkingTo => mOTalkingTo;

    public int PlayerX8
    {
        get
        {
            UWSpot lOSpot = World.Player.FeetSpot;

            return (lOSpot.Tile.X << 3) + lOSpot.FineX;
        }
    }

    public int PlayerY8
    {
        get
        {
            UWSpot lOSpot = World.Player.FeetSpot;

            return (lOSpot.Tile.Y << 3) + lOSpot.FineY;
        }
    }

    public int PlayerFacingEighth => (UWHeadings.DegreesToHeadingByte(World.Player.ViewYawDegrees) >> 5) & 7;
}
