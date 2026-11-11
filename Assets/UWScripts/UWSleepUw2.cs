using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S SLEEP (UW2 plan phase 4; player_sleep, uw2 game/SKILLS.C:421): the night is UWSleepRules', the
/// one the first game runs. This is its host on the walk's side: the three ways in (the sleep key and the pack's
/// bedroll, the bed in the world, passing out drunk), the numbers into the record at once, and the night's
/// presentation. The view goes black on a canvas of its own over the view window, the scene's UWGameUI stands idle.
/// The dreams are cutscenes and the night's music a theme, both of phase 5; until then the night shows its black.
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

    private float mfBlackLeft;

    private float mfBlackTotal;

    /// <summary>What a blink does once the view is black (Blink), null when none waits.</summary>
    private System.Action mOAtBlack;

    private GameObject mOBlack;

    private RawImage mOBlackImage;

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

        IUWWorld lONight = UWScopedWorld.With(lOWorld, new NightPresentation(this, lOWorld.Presentation, lOWorld.Data));

        UWSleepRules.Sleep(lOPlayer.Vitals, UWWorldLevels.NumberOr(lOWorld, 1), lONight, pbPassedOut, pbBedroll, pbBed);

        // At once: the record is what comes back before the next tick (UWVitalsUw2.fTakeFromRecord).
        lOPlayer.WriteToRecord();

        if (UWGameSession.Current != null && UWGameSession.Current.PlayerFileUw2 != null)
            UWPlayerRecordUw2.SetDayClockFromClock(UWGameSession.Current.PlayerFileUw2);

        // Passing out waits for no dream, only the fades.
        if (pbPassedOut && lOPlayer.Vitals.CurrentHP > 0)
            fShowBlack(0f);
    }

    private void Update()
    {
        if (mfBlackLeft <= 0f)
            return;

        mfBlackLeft -= Time.unscaledDeltaTime;

        // A blink's action at its middle, the full black.
        if (mOAtBlack != null && mfBlackLeft <= mfBlackTotal * 0.5f)
        {
            System.Action lOAction = mOAtBlack;

            mOAtBlack = null;
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

    /// <summary>The night's lines go to the second game's scroll by its own block, the black to the walk's view; no
    /// cutscene and no theme yet (phase 5). The rest is the world's.</summary>
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

        public void PlaySoundAtPlayer(int piEffect)
        {
            mOInner.PlaySoundAtPlayer(piEffect);
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
        }

        public void ShowNightBlack(float pfSeconds)
        {
            mOOwner.fShowBlack(pfSeconds);
        }

        public bool PlayCutscene(int piCutscene)
        {
            return false;
        }

        public void ChangeMusicTheme(int piTheme)
        {
        }
    }
}
