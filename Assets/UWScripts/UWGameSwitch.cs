using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnderworldRevisited;
using UWDataImport.Install;
using UWDataImport.UWData;

/// <summary>
/// SWITCHING BETWEEN THE GAMES (Docs/UW2-PLAN.md section 4.1, phase 0 step 5, 2026-10-28): the
/// installation's game.gog holds both (UWGogInstall.HasGame); the Game window of the menu bar offers
/// the ones it holds, and a switch is the same as a new start - the choice is kept in settings.json
/// (UWUserSettings.Game) and the scene starts again, so the level loader builds the chosen game on
/// the one path every start takes (UWLevelLoader.Start: Chosen, then the data of that game, or its
/// main menu screen only while the port cannot play it yet, IUWGameProfile.IsPlayable).
///
/// Offered over the main menu only, as the game folder is: in a running game a switch would end it
/// without the question whether to save, which the original's Quit does not ask either.
/// </summary>
public static class UWGameSwitch
{
    private static string msOfferedFor;

    private static List<IUWGameProfile> mOOffered;

    /// <summary>
    /// The games the installation holds, the first first: those of its game.gog whose folder and
    /// program are there. Without an image (a developer's DATA folder, UW_DATA naming one) the first
    /// only. Asked of the image once per image path.
    /// </summary>
    public static IReadOnlyList<IUWGameProfile> Offered()
    {
        string lsImage = UWSettings.Instance != null ? UWSettings.Instance.FindGogImage() : null;
        string lsKey = lsImage ?? string.Empty;

        if (mOOffered != null && msOfferedFor == lsKey)
            return mOOffered;

        msOfferedFor = lsKey;
        mOOffered = new List<IUWGameProfile>();

        foreach (IUWGameProfile lOGame in UWGameProfiles.All)
        {
            if (lOGame == UWGameProfiles.Uw1 || (lsImage != null && UWGogInstall.HasGame(lsImage, lOGame)))
                mOOffered.Add(lOGame);
        }

        return mOOffered;
    }

    /// <summary>The game this start builds: the one chosen last (settings.json's Game) when the
    /// installation holds it, else the first.</summary>
    public static IUWGameProfile Chosen()
    {
        IUWGameProfile lOStored = fParse(UWUserSettings.Game);

        if (lOStored == null || lOStored == UWGameProfiles.Uw1)
            return UWGameProfiles.Uw1;

        foreach (IUWGameProfile lOGame in Offered())
        {
            if (lOGame == lOStored)
                return lOGame;
        }

        Debug.LogWarning("[Game] " + lOStored.Title + " was chosen last, but the installation does not hold it - starting "
            + UWGameProfiles.Uw1.Title + ".");

        return UWGameProfiles.Uw1;
    }

    /// <summary>Keeps pOGame as the chosen one and starts the scene again, which builds it. Nothing
    /// for the game already running.</summary>
    public static void Switch(IUWGameProfile pOGame)
    {
        if (pOGame == null || pOGame == UWSettings.Game)
            return;

        UWUserSettings.Game = pOGame == UWGameProfiles.Uw1 ? string.Empty : pOGame.Game.ToString();
        UWUserSettings.Save();

        Debug.Log("[Game] Switching to " + pOGame.Title + ".");

        // Time must run again before the restart - otherwise the scene starts frozen (as for a load).
        UWGameClock.ReleaseAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>The profile a stored id names ("Uw2"), or null for an empty or unknown one.</summary>
    private static IUWGameProfile fParse(string psId)
    {
        if (string.IsNullOrEmpty(psId) || !Enum.TryParse(psId, true, out UWGame leGame))
            return null;

        return UWGameProfiles.Of(leGame);
    }
}
