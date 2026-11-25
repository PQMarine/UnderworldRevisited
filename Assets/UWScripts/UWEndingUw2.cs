using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S END (uw2 game/SKILLS.C player_won_game, from cs_check; UW2 plan phase 5 part D): the world stands,
/// the menu's theme starts (show_cutscene(2)'s load_new_music), the ending cutscene 2 plays (Escape does not end it),
/// then the two end screens - BYT.ARK 8 in palette 7 and 9 with game_stats (UWVictoryScreens) - each until a key or a
/// click, the credits (10), and back to the main menu (real_death(0): no final death cutscene).
/// </summary>
public static class UWEndingUw2
{
    public static bool IsRunning { get; private set; }

    public static void Play()
    {
        UWIntroPlayer lOPlayer = UWScene.IntroPlayer;

        if (lOPlayer == null)
        {
            UWGameStartUw2.Leave();

            return;
        }

        IsRunning = true;
        UnityEngine.Debug.Log("[Session] The game is won: the ending.");
        UWGameClock.Hold(UWGameClock.DeathHold);
        UWAudioService.Uw2Load(UWMusicSelectorUw2.MenuTheme);

        lOPlayer.PlayCutscene(UWStoryRulesUw2.EndingCutscene,
            () => lOPlayer.PlayVictoryScreens(
                () => lOPlayer.PlayCutscene(UWStoryRulesUw2.CreditsCutscene, fBackToMenu)));
    }

    private static void fBackToMenu()
    {
        IsRunning = false;
        UWGameStartUw2.Leave();
    }
}
