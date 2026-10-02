using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Window or borderless fullscreen, and the resolution (per user, 2026-09-26, after the first
/// look at a build, which came up as a fixed 1280x720 window). Chosen in the Display dialog of
/// the menu bar (UWSetupMenu), kept in settings.json (UWUserSettings) and put in place before
/// the first scene loads. A first start goes borderless at the desktop's size.
///
/// ONLY IN A BUILT GAME: in the editor the Game view decides the size, so nothing is applied
/// there; the dialog says so.
///
/// Borderless fullscreen only offers resolutions of the desktop's shape - any other would be
/// stretched or framed by the system. A window offers every size up to the desktop's.
/// </summary>
public static class UWDisplayMode
{
    public enum ModeEnum
    {
        Window = 0,
        BorderlessFullscreen = 1
    }

    /// <summary>Smaller resolutions are not offered.</summary>
    private const int MinWidth = 640;

    private const int MinHeight = 480;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void fApplyAtStartup()
    {
        if (Application.isEditor)
            return;

        Vector2Int lOSize = SavedResolution;

        Screen.SetResolution(lOSize.x, lOSize.y, fUnityMode(SavedMode));

        Application.quitting += fRememberWindow;
    }

    /// <summary>
    /// THE WINDOW CAN BE DRAGGED TO ANY SIZE (per user, 2026-09-26; PlayerSettings
    /// resizableWindow). The size it had at the end is kept for the next start, so dragging
    /// counts like a choice in the dialog. Such a size is usually not in the dialog's list,
    /// which then marks nothing.
    /// </summary>
    private static void fRememberWindow()
    {
        if (CurrentMode != ModeEnum.Window)
            return;

        Vector2Int lOSize = CurrentResolution;

        if (lOSize.x < MinWidth || lOSize.y < MinHeight
            || (lOSize.x == UWUserSettings.ResolutionWidth && lOSize.y == UWUserSettings.ResolutionHeight))
            return;

        UWUserSettings.DisplayMode = (int)ModeEnum.Window;
        UWUserSettings.ResolutionWidth = lOSize.x;
        UWUserSettings.ResolutionHeight = lOSize.y;
        UWUserSettings.Save();
    }

    /// <summary>The mode from the file; not chosen yet means borderless fullscreen.</summary>
    public static ModeEnum SavedMode => UWUserSettings.DisplayMode == (int)ModeEnum.Window
        ? ModeEnum.Window : ModeEnum.BorderlessFullscreen;

    /// <summary>The resolution from the file, the desktop's when none is kept or the kept one
    /// no longer fits the screen (another monitor).</summary>
    public static Vector2Int SavedResolution
    {
        get
        {
            Vector2Int lODesktop = Desktop;
            int liWidth = UWUserSettings.ResolutionWidth;
            int liHeight = UWUserSettings.ResolutionHeight;

            if (liWidth < MinWidth || liHeight < MinHeight || liWidth > lODesktop.x || liHeight > lODesktop.y)
                return lODesktop;

            return new Vector2Int(liWidth, liHeight);
        }
    }

    /// <summary>What the game shows in right now.</summary>
    public static ModeEnum CurrentMode => Screen.fullScreenMode == FullScreenMode.Windowed
        ? ModeEnum.Window : ModeEnum.BorderlessFullscreen;

    public static Vector2Int CurrentResolution => new Vector2Int(Screen.width, Screen.height);

    /// <summary>The desktop's resolution on the game's monitor.</summary>
    public static Vector2Int Desktop
    {
        get
        {
            Display lODisplay = Display.main;

            if (lODisplay != null && lODisplay.systemWidth > 0 && lODisplay.systemHeight > 0)
                return new Vector2Int(lODisplay.systemWidth, lODisplay.systemHeight);

            Resolution lOCurrent = Screen.currentResolution;

            return new Vector2Int(lOCurrent.width, lOCurrent.height);
        }
    }

    /// <summary>The resolutions to choose from in a mode, largest first, the desktop's always
    /// among them.</summary>
    public static List<Vector2Int> GetResolutions(ModeEnum peMode)
    {
        Vector2Int lODesktop = Desktop;
        List<Vector2Int> lOResult = new List<Vector2Int> { lODesktop };
        float lfDesktopShape = (float)lODesktop.x / lODesktop.y;

        foreach (Resolution lOMode in Screen.resolutions)
        {
            Vector2Int lOSize = new Vector2Int(lOMode.width, lOMode.height);

            if (lOSize.x < MinWidth || lOSize.y < MinHeight || lOSize.x > lODesktop.x || lOSize.y > lODesktop.y)
                continue;

            if (peMode == ModeEnum.BorderlessFullscreen
                && Mathf.Abs(((float)lOSize.x / lOSize.y) - lfDesktopShape) > 0.01f)
                continue;

            if (!lOResult.Contains(lOSize))
                lOResult.Add(lOSize);
        }

        lOResult.Sort((pOA, pOB) => pOA.x != pOB.x ? pOB.x.CompareTo(pOA.x) : pOB.y.CompareTo(pOA.y));

        return lOResult;
    }

    /// <summary>Switches at once and keeps the choice. A resolution the mode does not offer
    /// becomes the desktop's.</summary>
    public static void Apply(ModeEnum peMode, Vector2Int pOSize)
    {
        if (!GetResolutions(peMode).Contains(pOSize))
            pOSize = Desktop;

        UWUserSettings.DisplayMode = (int)peMode;
        UWUserSettings.ResolutionWidth = pOSize.x;
        UWUserSettings.ResolutionHeight = pOSize.y;
        UWUserSettings.Save();

        if (!Application.isEditor)
            Screen.SetResolution(pOSize.x, pOSize.y, fUnityMode(peMode));
    }

    private static FullScreenMode fUnityMode(ModeEnum peMode)
    {
        return peMode == ModeEnum.Window ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
    }
}
