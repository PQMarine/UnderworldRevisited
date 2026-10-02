using UnityEngine;

/// <summary>
/// The effects of the Ethereal Void on level 9 (per user, 2026-09-13: "Do the effects
/// on level 9").
///
/// FOLLOWING UW.EXE (UW1_asm.asm, EtherealVoidSpecialEffects_seg008_150, called from
/// PlayerUpdateTick_seg024_24DC_3A4): on every game tick on level 9, with a chance
/// of 1 in 32 (RNG and 0x1F equal to zero):
///   Flash         palette colour 0xB5 in the view window
///   Life drain    above 100 points 0 to 5, above 50 then 0 to 3, above 20 with chance 1 in 4
///                 0 to 2; otherwise, as long as more than 1 remains, with chance 1 in 8 exactly one
///   Quake         with chance 11 in 12, large channel, duration 15 plus 0 to 29
///   Compass       jumps to a random one of the 16 directions
/// The reference (etherealvoid.cs) ALWAYS quakes and, above 20 points, subtracts nothing
/// when the roll fails - there it deviates from the disassembly; UW.EXE is followed.
///
/// THE COMPASS does not follow the view direction on level 9 (reference playerdatloop: UpdateCompass
/// only outside level 9). It stays where it was on entering and only jumps
/// on an effect.
///
/// THE TICK RATE is not established: how often the game tick ran in the original depends on the machine,
/// and the reference itself notes it as unresolved. It is therefore kept in UWSettings.
/// </summary>
public class UWVoidEffects : MonoBehaviour
{
    /// <summary>Level 9, zero-based.</summary>
    private const int VoidLevelIndex = UWEndgame.VoidLevelIndex;

    private const int FlashPaletteIndex = 0xB5;

    private const float FlashSeconds = 0.08f;

    private const int CompassSteps = 16;

    private UWLevelLoader mOLevelLoader;

    private UWGameUI mOGameUi;

    private UWCharacter mOCharacter;

    private float mfTickAccumulator;

    private void Update()
    {
        if (mOLevelLoader == null)
            mOLevelLoader = GetComponent<UWLevelLoader>();

        if (mOGameUi == null)
            mOGameUi = UWScene.GameUi;

        if (mOLevelLoader == null || mOGameUi == null)
            return;

        bool lbInVoid = mOLevelLoader.CurrentLevelIndex == VoidLevelIndex;

        mOGameUi.FreezeCompass(lbInVoid);

        // UI and shaking hang off the main camera; if it is off (noclip), the
        // effects rest as in a pause.
        if (!lbInVoid || Time.deltaTime <= 0f || !mOGameUi.isActiveAndEnabled)
        {
            mfTickAccumulator = 0f;
            return;
        }

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
        float lfTicksPerSecond = lOSettings != null ? lOSettings.VoidEffectTicksPerSecond : 32f;

        if (lfTicksPerSecond <= 0f)
            return;

        mfTickAccumulator += Time.deltaTime * lfTicksPerSecond;

        while (mfTickAccumulator >= 1f)
        {
            mfTickAccumulator -= 1f;

            if ((Random.Range(0, 0x8000) & 0x1F) == 0)
                fApplyEffects();
        }
    }

    private void fApplyEffects()
    {
        mOGameUi.FlashWindowColour(FlashPaletteIndex, FlashSeconds);

        if (mOCharacter == null)
            mOCharacter = UWScene.Character;

        if (mOCharacter != null)
        {
            int liHp = Mathf.RoundToInt(mOCharacter.CurrentHP);
            int liNew = liHp;

            if (liHp > 0x64)
                liNew -= Random.Range(0, 6);
            else if (liHp > 0x32)
                liNew -= Random.Range(0, 4);
            else if (liHp > 0x14 && (Random.Range(0, 0x8000) & 3) < 1)
                liNew -= Random.Range(0, 3);
            else if (liHp > 1 && (Random.Range(0, 0x8000) & 7) == 0)
                liNew -= 1;

            if (liNew != liHp)
                mOCharacter.DrainVitality(liHp - liNew);
        }

        if (Random.Range(0, 12) != 0)
        {
            UWScreenShake lOShake = UWScreenShake.Ensure();

            if (lOShake != null)
                lOShake.Shake(UWScreenShake.LargeChannel, 0xF + Random.Range(0, 0x1E));
        }

        mOGameUi.SetFrozenCompassStep(Random.Range(0, CompassSteps));
    }
}
