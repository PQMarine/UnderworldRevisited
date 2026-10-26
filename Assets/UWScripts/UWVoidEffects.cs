using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The effects of the Ethereal Void on level 9 (per user, 2026-09-13: "Do the effects
/// on level 9") - the rule, its odds and its order of draws are UWVoidRules' (cleanup step 4.5,
/// 2026-10-17); this shows them: the flash, the drain through the damage pipeline
/// (UWDamageRequest.VoidDrain), the quake, the compass.
///
/// THE COMPASS does not follow the view direction on level 9 (reference playerdatloop: UpdateCompass
/// only outside level 9). It stays where it was on entering and only jumps
/// on an effect.
///
/// THE TICKS come from the game's own clock (UWWorldScheduler.Now, PIT ticks; Time.deltaTime until
/// cleanup step 4.5) at the rule set's vitals.void.ticksPerSecond (UWSettings.VoidEffectTicksPerSecond
/// until cleanup phase 10, 2026-10-26), so no frame rate changes them.
/// </summary>
public class UWVoidEffects : MonoBehaviour
{
    /// <summary>Level 9, zero-based (the rule set's levels.void).</summary>
    private static int VoidLevelIndex => UWEndgame.VoidLevelIndex;

    private UWLevelLoader mOLevelLoader;

    private UWGameUI mOGameUi;

    private UWCharacter mOCharacter;

    private readonly UWVoidRules mORules = new UWVoidRules();

    /// <summary>The world's game time at the last frame, in PIT ticks (UWWorldScheduler.Now).</summary>
    private long mlLastWorldTicks;

    private void Update()
    {
        long llNow = UWGameSession.Current.Schedule.Now;

        // A new game or a loaded one starts its clock again: no time has passed.
        int liPitTicks = llNow >= mlLastWorldTicks ? (int)System.Math.Min(int.MaxValue, llNow - mlLastWorldTicks) : 0;

        mlLastWorldTicks = llNow;

        if (mOLevelLoader == null)
            mOLevelLoader = GetComponent<UWLevelLoader>();

        if (mOGameUi == null)
            mOGameUi = UWScene.GameUi;

        if (mOLevelLoader == null || mOGameUi == null)
            return;

        bool lbInVoid = mOLevelLoader.CurrentLevelIndex == VoidLevelIndex;

        mOGameUi.Compass.FreezeCompass(lbInVoid);

        // UI and shaking hang off the main camera; if it is off (noclip), the
        // effects rest as in a pause.
        if (!lbInVoid || liPitTicks <= 0 || !mOGameUi.isActiveAndEnabled)
        {
            if (!lbInVoid || !mOGameUi.isActiveAndEnabled)
                mORules.Reset();

            return;
        }

        int liTicks = mORules.Advance(liPitTicks, UWRules.Current.Vitals.Void.TicksPerSecond);

        if (mOCharacter == null)
            mOCharacter = UWScene.Character;

        for (int liTick = 0; liTick < liTicks; liTick++)
        {
            int liHp = mOCharacter != null ? mOCharacter.Vitals.CurrentHP : 0;

            if (UWVoidRules.Tick(liHp, out UWVoidRules.Effect lOEffect))
                fApplyEffects(lOEffect);
        }
    }

    private void fApplyEffects(UWVoidRules.Effect pOEffect)
    {
        mOGameUi.Pictures.FlashWindowColour(UWVoidRules.FlashColour, UWVoidRules.FlashSeconds);

        if (mOCharacter != null && pOEffect.Drain > 0)
            mOCharacter.Damage(UWDamageRequest.VoidDrain(pOEffect.Drain));

        if (pOEffect.QuakeTicks > 0)
        {
            UWScreenShake lOShake = UWScreenShake.Ensure();

            if (lOShake != null)
                lOShake.Shake(UWScreenShake.LargeChannel, pOEffect.QuakeTicks);
        }

        mOGameUi.Compass.SetFrozenCompassStep(pOEffect.CompassStep);
    }
}
