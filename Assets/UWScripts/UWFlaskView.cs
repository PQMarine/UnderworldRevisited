using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The health and mana flasks as textures (cleanup step 4.6, 2026-10-18; part of UWCharacter
/// until then): redrawn when the vitals change, stepped and bubbling every frame (the levels and the
/// bubbles are the engine-free UWFlaskGauge's), and the resurrection's slow fill. The classic HUD
/// and the modern scheme read them through UWCharacter.Flasks.
/// </summary>
public sealed class UWFlaskView
{
    private UWFlasks mFlasks;

    private UWPlayerVitals mOVitals;

    private FilterMode meFilterMode = FilterMode.Point;

    private readonly UWFlaskGauge mOGauge = new UWFlaskGauge();

    public Texture2D HealthFlaskTeture { get; private set; }

    public Texture2D ManaFlaskTeture { get; private set; }

    /// <summary>Levels of a flask picture (UWFlasks.FillLevels, twelve since 2026-10-12).</summary>
    private const float FlaskLevels = UWFlasks.FillLevels;

    /// <summary>Start of the running flask fill animation, below zero for none.</summary>
    private float mfFlaskFillStart = -1f;

    /// <summary>The flask pictures from the data and the vitals they show; drawn at once.</summary>
    public void Init(UWFlasks pOFlasks, FilterMode peFilterMode, UWPlayerVitals pOVitals)
    {
        mFlasks = pOFlasks;
        meFilterMode = peFilterMode;
        mOVitals = pOVitals;

        fUpdateFlasks();
    }

    /// <summary>The vitals changed: health, mana or poison - the flasks show it. Before Init has
    /// handed over the flask pictures there is nothing to draw yet.</summary>
    public void Refresh()
    {
        if (mFlasks != null)
            fUpdateFlasks();
    }

    /// <summary>One frame: the fill animation's redraw while it runs, then the levels' steps and
    /// the bubbling.</summary>
    public void Tick(float pfDeltaSeconds)
    {
        // The flask fill animation needs a redraw every frame while it runs.
        if (mfFlaskFillStart >= 0f)
            fUpdateFlasks();

        if (mOGauge.Advance(pfDeltaSeconds, (int)mOVitals.CurrentHP, (int)mOVitals.MaxHP,
            (int)mOVitals.CurrentMana, (int)mOVitals.MaxMana, mfFlaskFillStart >= 0f))
            fUpdateFlasks();
    }

    /// <summary>
    /// Lets the health and mana flasks fill up from empty to their current values, one level
    /// per UWSettings.FlaskFillStepSeconds. After the resurrection at the silver tree both
    /// flasks fill slowly in the original, one step every 0.25 s, while vitality and mana are
    /// already nearly full (per user, 2026-09-14) - only the pictures are animated.
    /// </summary>
    public void StartFlaskFill()
    {
        mfFlaskFillStart = Time.time;
        fUpdateFlasks();
    }

    private void fUpdateFlasks()
    {
        float lfCurrentHP = mOVitals.CurrentHP;
        float lfMaxHP = mOVitals.MaxHP;
        float lfCurrentMana = mOVitals.CurrentMana;
        float lfMaxMana = mOVitals.MaxMana;

        // The level that is currently shown, not the real one - it walks there step by step
        // (see UWFlaskGauge).
        int liShownHealth = mOGauge.ShownHealthLevel;
        int liShownMana = mOGauge.ShownManaLevel;
        float lfHealth = liShownHealth >= 0 ? UWFlaskGauge.GetShownFill(liShownHealth) : (lfMaxHP <= 0f ? 0f : lfCurrentHP / lfMaxHP);
        float lfMana = liShownMana >= 0 ? UWFlaskGauge.GetShownFill(liShownMana) : (lfMaxMana <= 0f ? 0f : lfCurrentMana / lfMaxMana);

        // While the fill animation runs the flasks show at most the animated level (see
        // StartFlaskFill); the real values are set already.
        if (mfFlaskFillStart >= 0f)
        {
            UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
            float lfStep = lOSettings != null ? lOSettings.FlaskFillStepSeconds : 0.25f;
            float lfShown = lfStep <= 0f ? 1f : Mathf.Floor((Time.time - mfFlaskFillStart) / lfStep) / FlaskLevels;

            if (lfShown >= lfHealth && lfShown >= lfMana)
                mfFlaskFillStart = -1f;

            lfHealth = Mathf.Min(lfHealth, lfShown);
            lfMana = Mathf.Min(lfMana, lfShown);
        }

        // From the cache: previously every change created a new texture that was
        // never released (see UWWeaponView.fGetCurrentWeaponTexture).
        // A POISONED CHARACTER HAS A GREEN FLASK. FLASKS.GR carries the green set, but only the red
        // one was ever used, so a poisoning could not be seen at all (per user on the poison
        // needles of level 3, 2026-09-17: "not poisoned", while the poison was set).
        //
        // WITH THE POISON MARK ON (colour help, see UWColourVision) the green flask is hatched as
        // well - red and green is exactly what a red-green deficiency cannot tell apart.
        bool lbPoisoned = mOVitals.Poison > 0;

        HealthFlaskTeture = fGetFlaskTexture(
            lbPoisoned ? UWFlasks.FlaskTypeEnum.Green : UWFlasks.FlaskTypeEnum.Red, lfHealth,
            lbPoisoned && UWUserSettings.MarkPoison, mOGauge.HealthBubbleFrame);

        // Until 2026-08-29 the mana flask computed with CurrentHP/MaxHP - so both flasks
        // showed the same value. It only went unnoticed because there was no mana at all.
        ManaFlaskTeture = fGetFlaskTexture(UWFlasks.FlaskTypeEnum.Blue, lfMana, false, mOGauge.ManaBubbleFrame);
    }

    /// <summary>
    /// A flask picture, by colour, fill level and hatching.
    ///
    /// ITS OWN CACHE, because UWFlasks builds a NEW UWTexture on every call: the cache over the
    /// texture object never found it again and kept a fresh Texture2D per change. The key is
    /// what the picture is made of, and the whole cache is thrown away when the colour help
    /// changes (see UWColourVision).
    /// </summary>
    private Texture2D fGetFlaskTexture(UWFlasks.FlaskTypeEnum peType, float pfFill, bool pbHatch, int piBubbleFrame)
    {
        int liLevel = Mathf.Clamp((int)(pfFill * FlaskLevels), 0, (int)FlaskLevels);
        int liKey = ((int)peType * 1000) + (liLevel * 2) + (pbHatch ? 1 : 0)
            + ((piBubbleFrame + 1) * 10000);

        if (miFlaskColourVersion != UWColourVision.Version)
        {
            foreach (Texture2D lOOld in mOFlaskCache.Values)
                Object.Destroy(lOOld);

            mOFlaskCache.Clear();

            miFlaskColourVersion = UWColourVision.Version;
        }

        Texture2D lOTexture;

        if (mOFlaskCache.TryGetValue(liKey, out lOTexture) && lOTexture != null)
            return lOTexture;

        UWTexture lOFlask = mFlasks.GetFlask(peType, pfFill, pbHatch, piBubbleFrame);

        lOTexture = new Texture2D(lOFlask.Width, lOFlask.Height, TextureFormat.ARGB32, false);
        lOTexture.name = "UWFlaskView flask";
        lOTexture.filterMode = meFilterMode;
        lOTexture.SetPixels32(UWGameUI.fGetTextureInvert(lOFlask));
        lOTexture.Apply();

        mOFlaskCache[liKey] = lOTexture;

        return lOTexture;
    }

    private readonly Dictionary<int, Texture2D> mOFlaskCache = new Dictionary<int, Texture2D>();

    private int miFlaskColourVersion = -1;
}
