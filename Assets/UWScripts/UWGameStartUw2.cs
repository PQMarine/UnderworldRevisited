using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// STARTING AND LEAVING A GAME OF THE SECOND GAME (Docs/UW2-PLAN.md phase 2 step 3, 2026-10-29; named
/// UWWalkUw2 until 2026-11-23, from the time the second game was only a walk through its world): its
/// main menu's Create Character (UWCharacterCreationScreenUw2) and Journey Onward ask here, the scene
/// starts again and the level loader builds the session from the new character or the save
/// (UWLevelLoader.fStartUw2). Quit in the options panel leaves it for the main menu the same way.
/// IsRunning says whether the running scene is such a session, not the second game's main menu.
/// The first game has no counterpart: its level loader starts a game by itself.
///
/// The request crosses the scene restart as a static, as a new character's data does
/// (UWCharacterCreationScreen.PendingPlayerData).
/// </summary>
public static class UWGameStartUw2
{
    /// <summary>Whether the next scene start builds the session.</summary>
    public static bool IsPending { get; private set; }

    /// <summary>Whether the running scene is a session of the second game (set by the level loader when it builds one).</summary>
    public static bool IsRunning { get; set; }

    /// <summary>The save the next session starts from (step 6: the main menu's Journey Onward), null for a
    /// new one.</summary>
    public static string PendingSavePath { get; private set; }

    /// <summary>The save taken once by the level loader with the session.</summary>
    public static string TakeSavePath()
    {
        string lsPath = PendingSavePath;

        PendingSavePath = null;

        return lsPath;
    }

    /// <summary>Starts the session - from the save in psSavePath, or new: black at once, the scene again.</summary>
    public static void Request(string psSavePath = null)
    {
        IsPending = true;
        PendingSavePath = psSavePath;

        Debug.Log("[Session] Starting a game of " + UWDataImport.UWData.UWGameProfiles.Uw2.Title + ".");

        UWScreenUi.ShowLoadingCover();
        UWGameClock.ReleaseAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Leaves the session for the second game's main menu (the options' Quit, UW2 plan phase 3 step
    /// 4): the scene again without a session.</summary>
    public static void Leave()
    {
        IsPending = false;
        PendingSavePath = null;

        Debug.Log("[Session] Leaving the game for the main menu.");

        // The held water voice outlives the scene (UWAudioMixer); the new scene has no swimmer to end it.
        UWAudioService.StopWaterLoop();
        UWScreenUi.ShowLoadingCover();
        UWGameClock.ReleaseAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>The pending request, taken once by the level loader's start.</summary>
    public static bool Take()
    {
        bool lbPending = IsPending;

        IsPending = false;

        return lbPending;
    }
}
