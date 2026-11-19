using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S SLEEP (UW2 plan phase 4; player_sleep, uw2 game/SKILLS.C:421): the night is UWSleepRules', the
/// one the first game runs. This is its host on the walk's side: the three ways in (the sleep key and the pack's
/// bedroll, the bed in the world, passing out drunk), the numbers into the record at once, and the night's
/// presentation. The view goes black on a canvas of its own over the view window, the scene's UWGameUI stands idle.
/// The night's music comes from UWAudioService (phase 5 part A); the dreams are cutscenes of phase 5 part D, until then
/// the night shows its black.
/// </summary>
public sealed class UWSleepUw2 : MonoBehaviour
{
    public static UWSleepUw2 Current { get; private set; }

    /// <summary>The bed (UseRect, USEITEMS.C:911).</summary>
    public const int BedObjectId = 0x167;

    /// <summary>The name of the hold while the night is black (UWControlScheme.HoldInput).</summary>
    private const string NightHold = "sleep";

    /// <summary>fadeout3d and fadein3d: twelve steps of the palette, their length by eye (per user, 2026-11-02: "nur
    /// kurz schwarz" when passing out, which has these two and no hold).</summary>
    public const float FadeSeconds = 0.3f;

    /// <summary>The fade's largest step a frame (a level load's long frame would skip the fade out).</summary>
    private const float MaxBlackStep = 1f / 30f;

    /// <summary>A blink's frame of full black has been shown; its action comes on the next.</summary>
    private bool mbFullBlackShown;

    private float mfBlackLeft;

    private float mfBlackTotal;

    /// <summary>What a blink does once the view is black (Blink), null when none waits.</summary>
    private System.Action mOAtBlack;

    private GameObject mOBlack;

    private RawImage mOBlackImage;

    /// <summary>fill_FB's flash over the view (2026-11-14; the second game's flashes were silent until then).</summary>
    private GameObject mOFlash;

    private RawImage mOFlashImage;

    private float mfFlashLeft;

    private void Awake()
    {
        Current = this;
    }

    private void OnDestroy()
    {
        fEndBlack();

        if (Current == this)
            Current = null;
    }

    /// <summary>True while the night is black: the world takes no input.</summary>
    public bool IsNight => mfBlackLeft > 0f;

    /// <summary>A blink has not yet reached its black: its action still waits.</summary>
    public bool IsBlinking => mOAtBlack != null;

    /// <summary>
    /// new_player_pos with NewPlyFade 3 (uw2 game/UWEDIT.C:466): fadeout3d, the move, fadein3d - the teleport of a
    /// trap, Portal and Gate Travel (per user 2026-11-11 on the original: a short black). The view fades out, the
    /// action runs at full black, the view fades in; the world takes no input meanwhile. False when the view is
    /// already black or another blink waits: the caller acts at once.
    /// </summary>
    public bool Blink(System.Action pOAtBlack)
    {
        if (pOAtBlack == null || IsNight)
            return false;

        mOAtBlack = pOAtBlack;
        fShowBlack(0f);

        return true;
    }

    /// <summary>player_key_sleep: in the bedroll when one is carried, else on the ground.</summary>
    public void Camp(UWInventoryModel pOInventory)
    {
        fSleep(false, pOInventory != null && pOInventory.FindCarried(pOItem => pOItem.ID == UWSleepRules.BedrollObjectId) != null, false);
    }

    /// <summary>player_sleep(0): camping on the ground, whatever is carried (the sleep trap, hack 44).</summary>
    public void OnTheGround()
    {
        fSleep(false, false, false);
    }

    public void InBedroll()
    {
        fSleep(false, true, false);
    }

    public void InBed()
    {
        fSleep(false, true, true);
    }

    /// <summary>player_sleep(-2): the alcohol asks nobody.</summary>
    public void PassOut()
    {
        fSleep(true, false, false);
    }

    private void fSleep(bool pbPassedOut, bool pbBedroll, bool pbBed)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;
        UWWorld lOWorld = UWWorld.Of(UWScene.LevelLoader);

        if (lOPlayer == null || lOPlayer.Vitals == null || lOWorld == null || IsNight)
            return;

        UWPlayerFile lOFile = UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;
        int liLevel = UWWorldLevels.NumberOr(lOWorld, 1);

        // player_sleep: asleep in the Void, sleeping there wakes (punt_void) and there is no night (G6, the Void).
        if (!pbPassedOut && lOFile != null && UWVoidRulesUw2.SleepWakes(lOFile, liLevel))
        {
            WakeFromVoid();

            return;
        }

        IUWWorld lONight = UWScopedWorld.With(lOWorld, new NightPresentation(this, lOWorld.Presentation, lOWorld.Data));

        bool lbSlept = UWSleepRules.Sleep(lOPlayer.Vitals, liLevel, lONight, pbPassedOut, pbBedroll, pbBed);

        // The night's music: the world's first walking theme as the night begins, a walking theme by chance on waking
        // (player_sleep, SKILLS.C:452, :487, :555; UW2 plan phase 5 part A). Ours sleeps in one step: both at once.
        if (lbSlept || pbPassedOut)
        {
            UWAudioService.Uw2Walking(UWMusicSelectorUw2.PickFirst, true);
            UWAudioService.Uw2Walking(UWMusicSelectorUw2.PickRandom);
        }

        // At once: the record is what comes back before the next tick (UWVitalsUw2.fTakeFromRecord).
        lOPlayer.WriteToRecord();

        // pass_time: the day clock from the clock, and the schedules catch up (WORLDEV.C:163, G6 part 1).
        if (UWGameSession.Current != null && UWGameSession.Current.PlayerFileUw2 != null)
        {
            UWPlayerRecordUw2.SetDayClockFromClock(UWGameSession.Current.PlayerFileUw2);
            UWScheduleHostUw2.RunAll();
        }

        // Passing out waits for no dream, only the fades.
        if (pbPassedOut && lOPlayer.Vitals.CurrentHP > 0)
            fShowBlack(0f);

        // dream: after the dream plant the night's dream is the Void (go_void); else a castle dream by the castle clock or
        // now and then one of the others (UWStoryRulesUw2, phase 5 part D), cutscene 0x18 + n.
        if (lbSlept && !pbPassedOut && lOFile != null && lOPlayer.Vitals.CurrentHP > 0)
        {
            if (UWVoidRulesUw2.DreamIsVoid(lOFile))
                fGoVoid(lOWorld, lOFile, liLevel);
            else
                fDream(lOFile);
        }
    }

    /// <summary>The night's dream, when one comes: its cutscene in the night's stead, its bit flipped.</summary>
    private static void fDream(UWPlayerFile pOFile)
    {
        int liDream = UWStoryRulesUw2.PickDream(pOFile, UWSleepRules.LastComfort, UWRandom.Next);
        UWIntroPlayer lOPlayer = UWScene.IntroPlayer;

        if (liDream < 0 || lOPlayer == null)
            return;

        Debug.Log("[Walk] The night's dream " + liDream + " (cutscene " + (UWStoryRulesUw2.FirstDreamCutscene + liDream) + ").");
        UWStoryRulesUw2.MarkDreamShown(pOFile, liDream);

        // The night's black lies over the cutscenes (UWUiSorting.NightUw2 above Intro): the dream takes its place, as
        // the original's full-screen cutscene covers the faded view (per user 2026-11-17: the view's window stayed
        // black in the dream's picture until the night's time ran out).
        if (Current != null)
            Current.fEndBlack();
        lOPlayer.PlayCutscene(UWStoryRulesUw2.FirstDreamCutscene + liDream);
    }

    /// <summary>go_void: "Your dreams are vivid...", quest 48, the place stored, sleepbits drawn anew, level 69.</summary>
    private static void fGoVoid(UWWorld pOWorld, UWPlayerFile pOFile, int piLevel)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWSpot lOSpot = pOWorld.Player.FeetSpot;

        if (lOLoader == null || !lOSpot.IsOnMap)
            return;

        UWGameScreenUw2.Say(UWText.Block(lOLoader.UWDataImporter, 1, UWVoidRulesUw2.VividDreamString + 1, string.Empty));
        UWVoidRulesUw2.GoVoid(pOFile, piLevel, lOSpot.Tile.X, lOSpot.Tile.Y, UWHeadings.DegreesToHeadingByte(pOWorld.Player.ViewYawDegrees),
            UWRandom.Next(UWOriginalRandom.Range), UWRandom.Next(UWOriginalRandom.Range), UWRandom.Next(UWOriginalRandom.Range),
            UWRandom.Next(UWOriginalRandom.Range), out int liX, out int liY);
        Debug.Log("[Walk] The dream plant's night: into the Ethereal Void at " + liX + "/" + liY + ".");
        lOLoader.WalkTeleport(new UWTeleportUw2.Target { Level = UWVoidRulesUw2.VoidLevel, TileX = liX, TileY = liY });
    }

    /// <summary>
    /// punt_void (uw2 game/SKILLS.C:747): out of the Void and sleepbits 0, the pointer's thing dropped, back to the
    /// square, level and heading go_void stored (do_dreamret, with its theme 10), "You awaken after a night of
    /// extremely lifelike dreams...". From the countdown of sleepbits, sleeping in the Void and dying there.
    /// </summary>
    public static void WakeFromVoid()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWPlayerFile lOFile = UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;

        if (lOLoader == null || lOFile == null)
            return;

        int liLevel = UWVoidRulesUw2.DreamLevel(lOFile);
        int liX = UWVoidRulesUw2.DreamX(lOFile);
        int liY = UWVoidRulesUw2.DreamY(lOFile);
        int liHeading = UWVoidRulesUw2.DreamHeadingByte(lOFile);

        UWVoidRulesUw2.Wake(lOFile);

        if (UWWalkUw2View.Current != null)
            UWWalkUw2View.Current.DropCursorItem();

        Debug.Log("[Walk] Awake from the Void: back to level " + liLevel + ", " + liX + "/" + liY + ".");
        lOLoader.WalkTeleport(new UWTeleportUw2.Target { Level = liLevel, TileX = liX, TileY = liY, HasHeading = true, Heading = liHeading << 8 });

        // do_dreamret's set_new_music(10) (SKILLS.C:717), after the level's walking choice (UW2 plan phase 5 part B).
        UWAudioService.Uw2Request(UWMusicSelectorUw2.FollowTheme);
        UWGameScreenUw2.Say(UWText.Block(lOLoader.UWDataImporter, 1, UWVoidRulesUw2.AwakenString + 1, string.Empty));
    }

    private void Update()
    {
        if (mfFlashLeft > 0f)
        {
            mfFlashLeft -= Time.unscaledDeltaTime;

            if (mfFlashLeft <= 0f && mOFlash != null)
                mOFlash.SetActive(false);
        }

        if (mfBlackLeft <= 0f)
            return;

        // A frame's step at most MaxBlackStep: the frame after a level load is long, and the fade out would be gone in it.
        mfBlackLeft -= Mathf.Min(Time.unscaledDeltaTime, MaxBlackStep);

        // A blink's action at its middle, the full black - ONE FRAME SHOWN FULLY BLACK FIRST (per user 2026-11-17: at a
        // level change the old picture still showed through the black while the new level loaded; the action ran in the
        // frame the timer passed the middle, the last frame shown had not reached full black).
        if (mOAtBlack != null && mfBlackLeft <= mfBlackTotal * 0.5f)
        {
            if (!mbFullBlackShown)
            {
                mbFullBlackShown = true;
                mfBlackLeft = mfBlackTotal * 0.5f;
                mOBlackImage.color = Color.black;

                return;
            }

            System.Action lOAction = mOAtBlack;

            mOAtBlack = null;
            mbFullBlackShown = false;
            lOAction();
        }

        if (mfBlackLeft <= 0f)
            fEndBlack();
        else
            fSetBlackAlpha();
    }

    /// <summary>In and out at both ends of the black.</summary>
    private void fSetBlackAlpha()
    {
        mOBlackImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(Mathf.Min(mfBlackLeft, mfBlackTotal - mfBlackLeft) / FadeSeconds));
    }

    private void fEndBlack()
    {
        mfBlackLeft = 0f;
        mOAtBlack = null;
        mbFullBlackShown = false;

        if (mOBlack != null)
            mOBlack.SetActive(false);

        if (UWControlScheme.Shared != null)
            UWControlScheme.Shared.ReleaseInput(NightHold);
    }

    /// <summary>Black over the view window: the whole screen in the modern scheme, whose view it is, the frame's
    /// hole in the classic one.</summary>
    private void fShowBlack(float pfSeconds)
    {
        Camera lOCamera = UWGameScreenUw2.Current != null ? UWGameScreenUw2.Current.ViewCamera : null;
        Rect lOView = lOCamera != null ? lOCamera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);

        if (mOBlack == null)
            fBuildBlack();

        RectTransform lOImage = (RectTransform)mOBlack.transform.GetChild(0);

        lOImage.anchorMin = Vector2.zero;
        lOImage.anchorMax = Vector2.zero;
        lOImage.pivot = Vector2.zero;
        lOImage.anchoredPosition = lOView.position;
        lOImage.sizeDelta = lOView.size;

        mOBlack.SetActive(true);
        mfBlackTotal = pfSeconds + (2f * FadeSeconds);
        mfBlackLeft = mfBlackTotal;
        fSetBlackAlpha();

        if (UWControlScheme.Shared != null)
            UWControlScheme.Shared.HoldInput(NightHold);
    }

    /// <summary>
    /// fill_FB(colour): the view filled with one palette colour for a moment, input left alone (the first game's
    /// UWHudPictures.FlashWindowColour). Where the black goes: the whole screen in the modern scheme, the frame's hole in
    /// the classic one.
    /// </summary>
    /// <summary>fill_FB: the second game's view in one palette colour for a moment - the night's flashes and, since 2026-11-17,
    /// every FlashWindow of the walk (UWPresentation: a critical hit, the special-effect trap, the swim check, spells).</summary>
    public void ShowFlash(DataImport pOData, int piPaletteIndex, float pfSeconds)
    {
        if (pfSeconds <= 0f || pOData == null || pOData.Palettes == null)
            return;

        Camera lOCamera = UWGameScreenUw2.Current != null ? UWGameScreenUw2.Current.ViewCamera : null;
        Rect lOView = lOCamera != null ? lOCamera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);

        if (mOFlash == null)
        {
            mOFlash = new GameObject("Flash", typeof(Canvas));

            Canvas lOCanvas = mOFlash.GetComponent<Canvas>();

            lOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            lOCanvas.sortingOrder = UWUiSorting.NightUw2;
            mOFlash.transform.SetParent(transform, false);

            GameObject lOImage = new GameObject("Colour", typeof(RawImage));

            lOImage.transform.SetParent(mOFlash.transform, false);
            mOFlashImage = lOImage.GetComponent<RawImage>();
            mOFlashImage.raycastTarget = false;
        }

        RectTransform lORect = (RectTransform)mOFlashImage.transform;

        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;
        lORect.anchoredPosition = lOView.position;
        lORect.sizeDelta = lOView.size;

        UWDataImport.UWData.UWColor32 lOColour = pOData.Palettes.GetPalette(0).GetUWColor(piPaletteIndex);

        mOFlashImage.color = new Color32(lOColour.R, lOColour.G, lOColour.B, 255);
        mOFlash.SetActive(true);
        mfFlashLeft = pfSeconds;
    }

    private void fBuildBlack()
    {
        mOBlack = new GameObject("Night", typeof(Canvas));

        Canvas lOCanvas = mOBlack.GetComponent<Canvas>();

        lOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        lOCanvas.sortingOrder = UWUiSorting.NightUw2;
        mOBlack.transform.SetParent(transform, false);

        GameObject lOImage = new GameObject("Black", typeof(RawImage));

        lOImage.transform.SetParent(mOBlack.transform, false);
        mOBlackImage = lOImage.GetComponent<RawImage>();
        mOBlackImage.raycastTarget = false;
    }

    /// <summary>The scroll's presentation of the second game for a cast as well.</summary>
    public IUWPresentation ScrollPresentation(UWWorld pOWorld)
    {
        return new NightPresentation(this, pOWorld.Presentation, pOWorld.Data);
    }

    /// <summary>The night's lines go to the second game's scroll by its own block, the black to the walk's view; a
    /// cutscene to the world's presentation; the first game's night theme is dropped, the second's comes from fSleep. The rest is the world's.</summary>
    private sealed class NightPresentation : IUWPresentation
    {
        private readonly UWSleepUw2 mOOwner;

        private readonly IUWPresentation mOInner;

        private readonly DataImport mOData;

        public NightPresentation(UWSleepUw2 pOOwner, IUWPresentation pOInner, DataImport pOData)
        {
            mOOwner = pOOwner;
            mOInner = pOInner;
            mOData = pOData;
        }

        public bool ShowsMessages => true;

        public void AddMessage(string psMessage)
        {
            UWGameScreenUw2.Say(psMessage);
        }

        public void AddGeneralMessage(int piIndex)
        {
            UWGameScreenUw2.Say(UWText.General(mOData, piIndex));
        }

        public string GetGeneralMessage(int piIndex)
        {
            return UWText.General(mOData, piIndex);
        }

        public void PlaySoundAtPlayer(int piEffect, int piVolume = 0)
        {
            mOInner.PlaySoundAtPlayer(piEffect, piVolume);
        }

        public void ShowEffect(int piEffectObjectId, UWSpot pOAt, float pfSeconds, int piStartFrame = 0)
        {
            mOInner.ShowEffect(piEffectObjectId, pOAt, pfSeconds, piStartFrame);
        }

        public void ShakeScreen(int piDuration)
        {
            mOInner.ShakeScreen(piDuration);
        }

        public void FlashWindow(int piPaletteColour, float pfSeconds)
        {
            mOOwner.ShowFlash(mOData, piPaletteColour, pfSeconds);
        }

        public void ShowNightBlack(float pfSeconds)
        {
            mOOwner.fShowBlack(pfSeconds);
        }

        /// <summary>
        /// A cutscene goes to the world's presentation (per user 2026-11-18: the Guardian's speech on level 26, a spell
        /// trap of class 14 behind an enter trigger, logged "NOT BUILT" while the original plays it). Every trap host of
        /// the second game takes this presentation while the sleep component is there, which is the whole walk, so the
        /// "no cutscene yet" of before phase 5 part D swallowed them all.
        /// </summary>
        public bool PlayCutscene(int piCutscene)
        {
            return mOInner.PlayCutscene(piCutscene);
        }

        public void ChangeMusicTheme(int piTheme)
        {
        }
    }
}
