using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// THE WALK THROUGH THE SECOND GAME'S WORLD (Docs/UW2-PLAN.md phase 2 step 3, 2026-10-29): the second
/// game has no character, no rules and no interface in the port yet, so its main menu's Create
/// Character starts a walk instead - the scene starts again and the level loader builds the level a
/// new game starts on (UWGameProfileUw2.NewGame...) with nothing of the first game's play behind it:
/// no player, no creature minds, no HUD. The view is the noclip camera at the start tile, the player
/// object stays off; moving with the second game's motion is step 4.
///
/// The request crosses the scene restart as a static, as a new character's data does
/// (UWCharacterCreationScreen.PendingPlayerData).
/// </summary>
public static class UWWalkUw2
{
    /// <summary>Whether the next scene start builds the walk.</summary>
    public static bool IsPending { get; private set; }

    /// <summary>Whether the running scene is a walk (set by the level loader when it builds one).</summary>
    public static bool IsWalking { get; set; }

    /// <summary>The save the next walk starts from (step 6: the main menu's Journey Onward), null for a
    /// new one.</summary>
    public static string PendingSavePath { get; private set; }

    /// <summary>The save taken once by the level loader with the walk.</summary>
    public static string TakeSavePath()
    {
        string lsPath = PendingSavePath;

        PendingSavePath = null;

        return lsPath;
    }

    /// <summary>Starts the walk - from the save in psSavePath, or new: black at once, the scene again.</summary>
    public static void Request(string psSavePath = null)
    {
        IsPending = true;
        PendingSavePath = psSavePath;

        Debug.Log("[Walk] Starting a walk through " + UWDataImport.UWData.UWGameProfiles.Uw2.Title + ".");

        UWScreenUi.ShowLoadingCover();
        UWGameClock.ReleaseAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Leaves the walk for the second game's main menu (the options' Quit, UW2 plan phase 3 step
    /// 4): the scene again without a walk.</summary>
    public static void Leave()
    {
        IsPending = false;
        PendingSavePath = null;

        Debug.Log("[Walk] Leaving the walk for the main menu.");

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
