using UWDataImport.UWData;

/// <summary>
/// The Unity side of IUWPresentation (cleanup step 3.1, 2026-10-17): the message scroll of an
/// Interaction, the sound effects at the avatar, effect pictures through the level loader, the
/// screen shake, the flash and black of the view window (UWGameUI) and the cutscene player. Until
/// then the trap, spell, sleep, item-use and vitals hosts and the special-death host each had
/// these bodies of their own.
///
/// THE MESSAGE CHANNEL: the world's own presentation (UWWorld.Presentation) writes to the scene's
/// Interaction, looked up on every call. A host that was handed an Interaction of its own - a trap
/// firing, a cast, the item use, the vitals, the sleep - takes For(world, that one), which writes
/// exactly there, or the silent scope when it has none. There is one Interaction in the scene, so
/// both reach the same scroll; keeping the handed one keeps each host's old "no channel, no
/// message" decision exactly.
///
/// THE EFFECTS go through the world's level loader; without a world (a host built without a
/// loader) there is none, as the spell host had none then.
///
/// Holds no state but the world and the channel, so it is a plain object (see UWWorld).
/// </summary>
public sealed class UWPresentation : IUWPresentation
{
    private readonly UWWorld mOWorld;

    private readonly Interaction mOChannel;

    private readonly bool mbSceneChannel;

    internal UWPresentation(UWWorld pOWorld, Interaction pOChannel, bool pbSceneChannel)
    {
        mOWorld = pOWorld;
        mOChannel = pOChannel;
        mbSceneChannel = pbSceneChannel;
    }

    /// <summary>The presentation whose lines go to this Interaction - the silent scope without
    /// one. pOWorld may be null (a host built without a level loader): then the messages, sounds,
    /// shake, flash and cutscenes work as before and only the effects have nowhere to stand.</summary>
    public static IUWPresentation For(UWWorld pOWorld, Interaction pOChannel)
    {
        return pOChannel != null ? new UWPresentation(pOWorld, pOChannel, false) : UWSilentPresentation.Of(Of(pOWorld));
    }

    /// <summary>The world's presentation (lines to the scene's Interaction); without a world one
    /// that has no effects but plays everything else.</summary>
    public static IUWPresentation Of(UWWorld pOWorld)
    {
        return pOWorld != null ? pOWorld.Presentation : new UWPresentation(null, null, true);
    }

    private Interaction fChannel => mbSceneChannel ? UWScene.Interaction : mOChannel;

    // ------------------------------------------------- Messages

    public bool ShowsMessages => fChannel != null;

    public void AddMessage(string psMessage)
    {
        Interaction lOChannel = fChannel;

        if (lOChannel != null)
            lOChannel.AddMessage(psMessage);
    }

    public void AddGeneralMessage(int piIndex)
    {
        Interaction lOChannel = fChannel;

        if (lOChannel != null)
            lOChannel.AddGeneralMessage(piIndex);
    }

    public string GetGeneralMessage(int piIndex)
    {
        Interaction lOChannel = fChannel;

        return lOChannel != null ? lOChannel.GetGeneralMessage(piIndex) : string.Empty;
    }

    // ------------------------------------------------- Sound, effects, the view

    public void PlaySoundAtPlayer(int piEffect)
    {
        UWSoundEffects.PlayAtAvatar(piEffect);
    }

    /// <summary>The picture stands at the spot's eighths and height; sprites have their pivot at
    /// the bottom edge, so at a floor's height they stand on it instead of in it. Moved from
    /// UWSpellHost.SpawnEffectInTile, with its guards.</summary>
    public void ShowEffect(int piEffectObjectId, UWSpot pOAt, float pfSeconds, int piStartFrame = 0)
    {
        UWLevelLoader lOLoader = mOWorld != null ? mOWorld.Loader : null;

        if (lOLoader == null || lOLoader.CurrentLevel == null || lOLoader.CurrentLevel.TileData == null
            || !pOAt.IsOnMap)
            return;

        lOLoader.SpawnEffectAt(piEffectObjectId, UWWorld.PointOf(pOAt), pfSeconds, 0f, piStartFrame);
    }

    public void ShakeScreen(int piDuration)
    {
        UWScreenShake lOShake = UWScreenShake.Ensure();

        if (lOShake != null)
            lOShake.Shake(UWScreenShake.LargeChannel, piDuration);
    }

    public void FlashWindow(int piPaletteColour, float pfSeconds)
    {
        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi != null)
            lOUi.FlashWindowColour(piPaletteColour, pfSeconds);
    }

    public void ShowNightBlack(float pfSeconds)
    {
        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi != null)
            lOUi.FlashWindowBlack(pfSeconds);
    }

    public bool PlayCutscene(int piCutscene)
    {
        UWIntroPlayer lOPlayer = UWScene.IntroPlayer;

        if (lOPlayer == null)
            return false;

        lOPlayer.PlayCutscene(piCutscene);

        return true;
    }

    /// <summary>UWMusic.ChangeTheme - the night's "Maps &amp; Legends", which the sleep's host asked
    /// for itself until cleanup step 3.6 (2026-10-17).</summary>
    public void ChangeMusicTheme(int piTheme)
    {
        UWMusic.ChangeTheme(piTheme);
    }
}
