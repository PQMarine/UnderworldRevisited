using System.IO;
using UnityEngine;

/// <summary>
/// What the help window keeps with a save game (decided per user, 2026-09-24): a readable file
/// of our own in the save folder, SAVEn/UWR.json, which the original never reads. For now it
/// holds the player's free notes (the help's third tab). Known limit, accepted then: saving over
/// the slot in the original leaves our file at its old state.
///
/// Loaded with a save game (UWLevelLoader), written after every successful save
/// (UWSavegameWriter); a new character starts with empty notes.
/// </summary>
public static class UWHelpNotes
{
    public const string FileName = "UWR.json";

    /// <summary>The notes as the player wrote them.</summary>
    public static string Notes { get; set; } = string.Empty;

    [System.Serializable]
    private class Data
    {
        public string Notes = string.Empty;
    }

    /// <summary>Reads the file of a save folder; a folder without one gives empty notes.</summary>
    public static void LoadFrom(string psFolder)
    {
        Notes = string.Empty;

        if (string.IsNullOrEmpty(psFolder))
            return;

        string lsPath = Path.Combine(psFolder, FileName);

        if (!File.Exists(lsPath))
            return;

        try
        {
            Data lOData = JsonUtility.FromJson<Data>(File.ReadAllText(lsPath));
            Notes = lOData != null && lOData.Notes != null ? lOData.Notes : string.Empty;
        }
        catch (System.Exception lOError)
        {
            Debug.LogWarning("[Help] " + lsPath + " could not be read: " + lOError.Message);
        }
    }

    public static void SaveTo(string psFolder)
    {
        if (string.IsNullOrEmpty(psFolder) || !Directory.Exists(psFolder))
            return;

        try
        {
            File.WriteAllText(Path.Combine(psFolder, FileName), JsonUtility.ToJson(new Data { Notes = Notes ?? string.Empty }, true));
        }
        catch (System.Exception lOError)
        {
            Debug.LogWarning("[Help] notes could not be saved: " + lOError.Message);
        }
    }

    public static void Clear()
    {
        Notes = string.Empty;
    }
}
