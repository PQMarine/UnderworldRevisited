using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE SAVE, LOAD AND QUIT FLOWS, one for both menus (cleanup 5.18, 2026-10-23): the classic
/// options panel (UWHudOptions) and the modern game menu (UWModernGameMenu) each had a copy of
/// writing a game into a slot and reporting it, restoring a slot or saying nothing is there, and
/// going back to the main menu. The copies are here; each menu keeps its own order of closing
/// itself around these calls (the modern menu closes between the write and the report, the
/// classic panel after it), its name rules (the classic panel passes the typed name untrimmed,
/// the modern menu trimmed), its refusals (UWHudOptions.RefusesToOpen/RefusesToSave) and its holds.
///
/// The messages stay as they were: the result alone in the top line, as the original does (per
/// user, 2026-09-12) - green "Save Game Succeeded.", red "Save Game Failed." (also on Escape),
/// without "Saving Game" and dots - and "No save game there." for an empty slot.
/// </summary>
public static class UWSaveFlow
{
    /// <summary>The savegame folder from the settings; null without them (both menus' read).</summary>
    public static string SavegameRoot => UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;

    /// <summary>The name shown for a slot, "&lt;not used yet&gt;" for an empty one (UWSavegameSlots).</summary>
    public static string SlotName(int piSlot)
    {
        return UWSavegameSlots.GetName(SavegameRoot, piSlot);
    }

    /// <summary>Whether a game lies in the slot.</summary>
    public static bool Exists(int piSlot)
    {
        return UWSavegameSlots.Exists(SavegameRoot, piSlot);
    }

    /// <summary>Writes the game into the slot under the name as given; the writer's error, null
    /// when it succeeded (UWSavegameWriter).</summary>
    public static string Write(int piSlot, string psName)
    {
        return UWSavegameWriter.Save(piSlot, psName);
    }

    /// <summary>
    /// The report of a save, as in the original (per user, 2026-09-12): ONLY the result in the
    /// top line - green "Save Game Succeeded.", red "Save Game Failed." (also when cancelled by
    /// Escape) - and the writer's error below it, if any. Through UWText since 2026-10-11, so the
    /// message is trimmed of its CR/LF like every other general message.
    /// </summary>
    public static void ReportSave(Interaction pOInteraction, DataImport pOData, string psError, bool pbCancelled)
    {
        if (pOInteraction == null || pOData == null)
            return;

        pOInteraction.ResetMessages();
        pOInteraction.AddMessage(UWText.General(pOData, psError == null && !pbCancelled
            ? UWStringIds.SaveGameSucceededMessage
            : UWStringIds.SaveGameFailedMessage));

        if (psError != null)
            pOInteraction.AddMessage(psError);
    }

    /// <summary>Loads the slot (the scene restarts, UWSavegameSlots.Restore); false with "No save
    /// game there." when it is empty - the caller stays where it is, just as in the original.</summary>
    public static bool Restore(Interaction pOInteraction, int piSlot)
    {
        if (UWSavegameSlots.Restore(SavegameRoot, piSlot))
            return true;

        ReportNoSaveGame(pOInteraction);

        return false;
    }

    /// <summary>"No save game there." (UWStringIds.NoSaveGameThereMessage).</summary>
    public static void ReportNoSaveGame(Interaction pOInteraction)
    {
        if (pOInteraction != null)
            pOInteraction.AddGeneralMessage(UWStringIds.NoSaveGameThereMessage);
    }

    /// <summary>
    /// "Quit Game" goes BACK TO THE MAIN MENU instead of ending the program (per user, 2026-09-17)
    /// - from there one can load, start a new character or really quit. The menu that asked has
    /// closed itself before. No menu in the scene (should not happen): then at least end the
    /// program, as before.
    /// </summary>
    public static void QuitToMainMenu()
    {
        UWMainMenu lOMenu = UWScene.MainMenu;

        if (lOMenu != null)
        {
            lOMenu.Show();

            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
