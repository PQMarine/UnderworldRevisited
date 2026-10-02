/// <summary>
/// The 4:3 switch: the classic 320x200 screen shown at the proportion of the CRT the game was made
/// for, every original pixel 1.2 times taller than wide, as DOSBox shows it (see
/// UWGameUI.HorizontalPixelFactor).
///
/// WHY A CLASS OF ITS OWN, the same split as UWSoundOptions: the live value sits in UWSettings,
/// a ScriptableObject, so that the Inspector shows and changes it while playing - but a build
/// never writes that asset back, so the lasting copy lives in UWUserSettings (settings.json).
/// Everything that reads the switch reads it here, which puts the saved value in place before
/// the first frame is drawn. Since 2026-09-22 it is in the Graphics menu of the bar (per user).
/// </summary>
public static class UWDisplayAspect
{
    public static bool Enabled
    {
        get
        {
            fEnsureLoaded();

            UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

            return lOSettings != null && lOSettings.DisplayAs4By3;
        }

        set
        {
            fEnsureLoaded();

            UWUserSettings.DisplayAs4By3 = value;
            UWUserSettings.Save();

            fApply();
        }
    }

    private static bool mbLoaded;

    private static void fEnsureLoaded()
    {
        if (mbLoaded)
            return;

        mbLoaded = true;

        fApply();
    }

    private static void fApply()
    {
        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        if (lOSettings != null)
            lOSettings.DisplayAs4By3 = UWUserSettings.DisplayAs4By3;
    }
}
