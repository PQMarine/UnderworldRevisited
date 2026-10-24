using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE WEAPON PICTURE'S SWAY WITH THE MOVEMENT (cleanup 5.18, 2026-10-23: out of UWGameUI, the
/// code as it was): the original's jitter of the weapon while walking (UWHeadBobRules), rolled
/// and drawn here for the classic frame (UWGameUI.Update) and the modern weapon view
/// (UWModernWeaponView) alike - one state, asked once a frame by whichever view shows the weapon.
/// </summary>
public sealed class UWWeaponSway
{
    public UWWeaponSway(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private readonly UWGameUI mOUi;

    /// <summary>The weapon's jitter: the rolled offset and the drawn one - the same under
    /// Original, the drawn one gliding after the rolled one under Smooth.</summary>
    private float mfWeaponJitterTarget;

    private float mfWeaponJitter;

    private float mfWeaponJitterRollTimer;

    /// <summary>
    /// The weapon's jitter while moving, in pixels of the 320x200 frame, always to the left -
    /// the original's rule (UWHeadBobRules: the level from the speed, 0 to 4 or 0 to 9 pixels,
    /// kept above 85 per cent, 0 while swinging). Its own three-way switch
    /// (UWUserSettings.WeaponJitterMode, per user): Original rolls twenty times a second and
    /// jumps - the original as at twenty frames a second; Smooth rolls six times a second,
    /// glides and is scaled by its strength slider; Off draws the weapon still. Both are the
    /// same at every frame rate of ours.
    /// </summary>
    internal float Offset()
    {
        UWPlayerMovement lOMovement = UWScene.PlayerMovement;
        UWHeadBobRules.ModeEnum leMode = UWUserSettings.WeaponJitterMode;

        if (leMode == UWHeadBobRules.ModeEnum.Off || lOMovement == null || mOUi.mCharacter == null || !mOUi.mCharacter.Weapon.CanAttack)
        {
            mfWeaponJitterTarget = 0f;
            mfWeaponJitter = 0f;
            mfWeaponJitterRollTimer = 0f;
            return 0f;
        }

        bool lbSmooth = leMode == UWHeadBobRules.ModeEnum.Smooth;
        float lfRolls = lbSmooth ? UWHeadBobRules.WeaponSmoothRollsPerSecond : UWHeadBobRules.WeaponRollsPerSecond;

        mfWeaponJitterRollTimer -= Time.deltaTime;

        if (mfWeaponJitterRollTimer <= 0f)
        {
            mfWeaponJitterRollTimer += 1f / lfRolls;

            if (mfWeaponJitterRollTimer < 0f)
                mfWeaponJitterRollTimer = 0f;

            float lfFraction = lOMovement.NormalForwardSpeed > 0f && lOMovement.IsWalkingOnGround
                ? lOMovement.SmoothedHorizontalSpeed / lOMovement.NormalForwardSpeed
                : 0f;

            if (lfFraction < UWHeadBobRules.StandingFraction)
                lfFraction = 0f;

            int liRange = UWHeadBobRules.GetWeaponJitterRange(UWHeadBobRules.GetWeaponJitterLevel(lfFraction));

            if (liRange == 0)
                mfWeaponJitterTarget = 0f;
            else if (liRange > 0)
                mfWeaponJitterTarget = -UWRandom.Next(liRange) * (lbSmooth ? UWUserSettings.WeaponJitterStrength : 1f);
        }

        mfWeaponJitter = lbSmooth
            ? Mathf.Lerp(mfWeaponJitter, mfWeaponJitterTarget,
                1f - Mathf.Exp(-UWHeadBobRules.WeaponSmoothGlidePerSecond * Time.deltaTime))
            : mfWeaponJitterTarget;

        return mfWeaponJitter;
    }
}
