using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The music on the Unity side. Which theme plays when is engine-free in UWMusicSelector
/// since 2026-09-18 (P3 of the engine separation); this class keeps the old static entry
/// points, hands the selector the game time and plays the pieces through UWAudioEngine.
/// </summary>
public static class UWMusic
{
    public const int IntroTheme = UWMusicSelector.IntroTheme;

    public const int FirstLevelTheme = UWMusicSelector.FirstLevelTheme;

    public const int LastLevelTheme = UWMusicSelector.LastLevelTheme;

    public const int FirstCombatTheme = UWMusicSelector.FirstCombatTheme;

    public const int LastCombatTheme = UWMusicSelector.LastCombatTheme;

    public const int ArmedTheme = UWMusicSelector.ArmedTheme;

    public const int FanfareTheme = UWMusicSelector.FanfareTheme;

    public const int DeathTheme = UWMusicSelector.DeathTheme;

    public const int FleeingTheme = UWMusicSelector.FleeingTheme;

    public const int MapsAndLegendsTheme = UWMusicSelector.MapsAndLegendsTheme;

    public const int WinningCombatTheme = UWMusicSelector.WinningCombatTheme;

    public const int EvenCombatTheme = UWMusicSelector.EvenCombatTheme;

    public const int LosingCombatTheme = UWMusicSelector.LosingCombatTheme;

    private static readonly UWMusicOutput msOOutput = new UWMusicOutput();

    private static readonly UWMusicSelector msOSelector = new UWMusicSelector(msOOutput);

    public static int CurrentTheme => msOSelector.CurrentTheme;

    public static int NewTheme => msOSelector.NewTheme;

    /// <summary>At the start of a scene. If a piece from the previous one is still playing - the sound
    /// survives the restart (see UWAudioEngine.Ensure) -, it stays, theme included.</summary>
    public static void Init(DataImport pOData)
    {
        msOOutput.Data = pOData;
        msOSelector.Reset();
    }

    /// <summary>Is a piece currently playing?</summary>
    public static bool IsPlaying => msOOutput.IsPlaying;

    /// <summary>Request a theme. Not possible during the fanfare.</summary>
    public static void ChangeTheme(int piTheme)
    {
        msOSelector.ChangeTheme(piTheme);
    }

    /// <summary>The weapon is drawn - see UWMusicSelector.OnWeaponDrawn.</summary>
    public static void OnWeaponDrawn()
    {
        msOSelector.OnWeaponDrawn();
    }

    /// <summary>Combat mode left - see UWMusicSelector.OnCombatModeLeft.</summary>
    public static void OnCombatModeLeft()
    {
        msOSelector.OnCombatModeLeft();
    }

    /// <summary>One of the three level themes, at random.</summary>
    public static void PickLevelTheme()
    {
        msOSelector.PickLevelTheme();
    }

    /// <summary>After map, conversation or weapon put away: Armed with a drawn weapon, otherwise
    /// a level theme.</summary>
    public static void ResumeAfterInterruption(bool pbWeaponDrawn)
    {
        msOSelector.ResumeAfterInterruption(pbWeaponDrawn);
    }

    /// <summary>A blow has been struck - the combat theme keeps playing.</summary>
    public static void MarkCombat()
    {
        msOSelector.MarkCombat(Time.time);
    }

    /// <summary>The player was hit: if still doing well, the undecided combat theme,
    /// otherwise the losing one (reference: damage.cs).</summary>
    public static void OnPlayerDamaged(float pfCurrentHealth, float pfMaxHealth)
    {
        msOSelector.OnPlayerDamaged(pfCurrentHealth, pfMaxHealth, Time.time);
    }

    /// <summary>Every frame: reconcile the request with what is playing (reference: RefreshMusic).</summary>
    public static void Refresh(bool pbWeaponDrawn)
    {
        msOSelector.Refresh(pbWeaponDrawn, Time.time);
    }

    /// <summary>The pieces: the AdLib version "AW" plus the theme number in octal, played
    /// through the audio engine.</summary>
    private sealed class UWMusicOutput : IUWMusicOutput
    {
        public DataImport Data;

        public bool IsReady => UWAudioEngine.Instance != null && UWAudioEngine.Instance.IsReady && Data != null;

        public bool IsPlaying => UWAudioEngine.Instance != null && UWAudioEngine.Instance.IsMusicPlaying;

        public void Play(int piTheme)
        {
            UWAudioEngine lOEngine = UWAudioEngine.Instance;

            if (lOEngine == null)
                return;

            UWXmi lOXmi = Data != null && Data.Sound != null
                ? Data.Sound.GetMusic(UWMusicSelector.GetFileNumber(piTheme), true) : null;

            if (lOXmi == null || !lOXmi.IsLoaded || lOXmi.Sequences.Count == 0)
            {
                lOEngine.StopMusic();
                return;
            }

            lOEngine.PlayMusic(lOXmi.Sequences[0], false);
        }

        public void Stop()
        {
            if (UWAudioEngine.Instance != null)
                UWAudioEngine.Instance.StopMusic();
        }
    }
}
