using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UWDataImport.UWData;

/// <summary>
/// THE CHECK FOR A NEWER RELEASE (GitHub issue 36, 2026-11-11, per user): only while the Game window's "Check for
/// new versions" is on (off by default, no question asked), from the main menu, at most once a day
/// (UWUpdateCheck.IsDue). One GET of GitHub's latest release with a plain User-Agent; the answer is kept in
/// update-check.txt beside settings.json, so the main menu names a newer release between checks too. No download,
/// no install: the main menu's line opens the release page in the browser. A failure (offline, rate limit, a
/// timeout) only writes a line to the log and waits for the next day.
/// </summary>
public static class UWUpdateChecker
{
    /// <summary>The request's time limit in seconds.</summary>
    private const int TimeoutSeconds = 10;

    private static UWUpdateCheck.State msState;

    private static bool mbRunning;

    /// <summary>Counts up whenever what Newer says may have changed - the main menu redraws its line then.</summary>
    public static int Version { get; private set; }

    /// <summary>The newer release to name, null when there is none, the option is off or nothing is known.</summary>
    public static UWUpdateCheck.State Newer
    {
        get
        {
            if (!UWUserSettings.CheckForUpdates)
                return null;

            UWUpdateCheck.State lOState = fState();

            return UWUpdateCheck.IsNewer(Application.version, lOState.Tag) && UWUpdateCheck.IsReleasePage(lOState.Url) ? lOState : null;
        }
    }

    /// <summary>The main menu shows: a check if the option is on and one is due.</summary>
    public static void CheckIfDue()
    {
        if (!UWUserSettings.CheckForUpdates || mbRunning || !UWUpdateCheck.IsDue(fState().CheckedUtc, DateTime.UtcNow))
            return;

        mbRunning = true;

        UnityWebRequest lORequest = UnityWebRequest.Get(UWUpdateCheck.LatestReleaseUrl);

        lORequest.timeout = TimeoutSeconds;
        lORequest.SetRequestHeader("Accept", "application/vnd.github+json");
        lORequest.SetRequestHeader("User-Agent", "UnderworldRevisited/" + Application.version);
        lORequest.SendWebRequest().completed += _ => fDone(lORequest);
    }

    private static void fDone(UnityWebRequest pORequest)
    {
        mbRunning = false;

        try
        {
            UWUpdateCheck.State lOState = fState();

            // Checked either way: a failure waits for the next day too, nothing retries in a loop.
            lOState.CheckedUtc = DateTime.UtcNow;

            if (pORequest.result == UnityWebRequest.Result.Success
                && UWUpdateCheck.TryParseLatest(pORequest.downloadHandler.text, out string lsTag, out string lsTitle, out string lsUrl))
            {
                lOState.Tag = lsTag;
                lOState.Title = lsTitle;
                lOState.Url = lsUrl;
                Debug.Log("[Update] latest release " + lsTag + (UWUpdateCheck.IsNewer(Application.version, lsTag) ? ", newer than " : ", not newer than ") + Application.version);
            }
            else
            {
                Debug.Log("[Update] no answer (" + pORequest.result + " " + pORequest.responseCode + " " + pORequest.error + ")");
            }

            fSave(lOState);
            Version++;
        }
        finally
        {
            pORequest.Dispose();
        }
    }

    /// <summary>Opens the newer release's page - only one of the project's (UWUpdateCheck.IsReleasePage).</summary>
    public static void OpenReleasePage()
    {
        UWUpdateCheck.State lONewer = Newer;

        if (lONewer != null)
            Application.OpenURL(lONewer.Url);
    }

    private static string fPath()
    {
        return UWUserFolder.Combine(UWUpdateCheck.StateFileName);
    }

    private static UWUpdateCheck.State fState()
    {
        if (msState != null)
            return msState;

        try
        {
            string lsPath = fPath();

            msState = File.Exists(lsPath) ? UWUpdateCheck.Read(File.ReadAllText(lsPath)) : new UWUpdateCheck.State();
        }
        catch (Exception lOError) when (lOError is IOException || lOError is UnauthorizedAccessException)
        {
            msState = new UWUpdateCheck.State();
        }

        return msState;
    }

    private static void fSave(UWUpdateCheck.State pOState)
    {
        msState = pOState;

        try
        {
            File.WriteAllText(fPath(), UWUpdateCheck.Write(pOState));
        }
        catch (Exception lOError) when (lOError is IOException || lOError is UnauthorizedAccessException)
        {
            Debug.LogWarning("[Update] could not keep the check (" + lOError.Message + ")");
        }
    }
}
