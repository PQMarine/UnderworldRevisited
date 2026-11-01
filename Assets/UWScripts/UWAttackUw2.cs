using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE PLAYER'S BLOW ON THE SECOND GAME (UW2 plan phase 4): the first game's button, wind-up, charge and
/// resolution (UWPlayerAttackState, UWPlayerAttack.ResolveMelee) over the second game's player, as UWPlayerMelee and
/// UWPlayerAttackInput do over Interaction, which does not run here. Classic: Fight chosen, the right button in the
/// view; the pointer's third picks the kind. Modern: the attack button, the view's pitch picks it. The blow finds
/// what is before the eye (the classic scheme's box probe on the level data needs the first game's character).
/// Missiles, the weapon's wear, the blood and the eyes are still to come.
/// </summary>
public sealed class UWAttackUw2 : MonoBehaviour
{
    /// <summary>How far a blow reaches, and the close range's sphere (Interaction, UWPlayerMelee).</summary>
    private const float Reach = 48f;

    private const float CloseRadius = 12f;

    private readonly UWPlayerAttackState mOState = new UWPlayerAttackState();

    private enum PhaseEnum
    {
        None,

        /// <summary>The lead-in and the hold, the button down.</summary>
        Raising,

        /// <summary>The button came up on the hold, the weapon goes on to the strike's step.</summary>
        Released,

        /// <summary>The blow is done, the weapon comes back.</summary>
        Struck
    }

    private PhaseEnum mePhase = PhaseEnum.None;

    private static bool fBusy => UWConversationScreenUw2.IsOpen || UWMapScreenUw2.IsOpen || UWGameScreenUw2.IsOptionsOpen || UWGameScreenUw2.IsAskingCount;

    /// <summary>player_attack, over the step flags of the weapon's animation: the lead-in gives way when the button
    /// comes up, the hold charges while it stays down, and the strike's step lands the blow.</summary>
    private void Update()
    {
        UWGameScreenUw2 lOScreen = UWGameScreenUw2.Current;
        UWVitalsUw2 lOVitals = UWVitalsUw2.Current;

        if (lOScreen == null || lOVitals == null || lOVitals.Vitals == null)
            return;

        if (mePhase == PhaseEnum.None)
        {
            if (fCanBegin(lOScreen) && fPressed(lOScreen))
                fBegin(lOScreen);

            return;
        }

        if (!lOScreen.IsSwinging)
        {
            fFinish(lOScreen);

            return;
        }

        int liFlag = lOScreen.SwingFlag;
        bool lbHeld = !fBusy && fHeld();

        if (mePhase == PhaseEnum.Raising)
        {
            if (liFlag < UWClassicLayoutUw2.Weapon.HoldFlag)
            {
                if (!lbHeld)
                {
                    lOScreen.CancelSwing();
                    fFinish(lOScreen);
                }

                return;
            }

            if (liFlag == UWClassicLayoutUw2.Weapon.HoldFlag)
            {
                if (lbHeld)
                {
                    lOVitals.Vitals.MakeNoise(UWPlayerVitals.ChargingNoise);
                    mOState.StepCharge(Time.deltaTime, UWRules.Current.Combat.Player.ChargeStepMilliseconds / 1000f);
                    lOScreen.ShowPower((mOState.PlayPow / UWClassicLayoutUw2.PowerGem.ChargeDivisor) + 1);

                    return;
                }

                mOState.Release(true);
                mePhase = PhaseEnum.Released;
                lOScreen.ResumeSwing();

                return;
            }
        }

        if (liFlag == UWClassicLayoutUw2.Weapon.StrikeFlag && mePhase != PhaseEnum.Struck)
        {
            if (mePhase == PhaseEnum.Raising)
                mOState.Release(true);

            mePhase = PhaseEnum.Struck;
            lOScreen.ShowPower(0);
            lOVitals.Vitals.MakeNoise(UWPlayerVitals.StrikeNoise);

            if (mOState.TakePendingBlow())
                fStrike(lOScreen, lOVitals);
        }
    }

    private void fFinish(UWGameScreenUw2 pOScreen)
    {
        mePhase = PhaseEnum.None;
        mOState.IsAttacking = false;
        mOState.ClearCharge();
        pOScreen.ShowPower(0);
    }

    private static bool fCanBegin(UWGameScreenUw2 pOScreen)
    {
        return !fBusy && !pOScreen.IsBagsOpen && pOScreen.Inventory != null && pOScreen.Inventory.CursorItem == null && pOScreen.IsWeaponReady && pOScreen.CanSwing;
    }

    private static InputAction fModernAttack => UWControlScheme.SharedControls != null ? UWControlScheme.SharedControls[UWAct.ModernAttack] : null;

    private static bool fPressed(UWGameScreenUw2 pOScreen)
    {
        if (UWGameScreenUw2.IsModernScheme)
            return fModernAttack != null && fModernAttack.WasPressedThisFrame();

        Mouse lOMouse = Mouse.current;

        return UWGameScreenUw2.Command == UWGameScreenUw2.CommandEnum.Fight && lOMouse != null && lOMouse.rightButton.wasPressedThisFrame
            && pOScreen.IsInView(UWPointer.Position);
    }

    private static bool fHeld()
    {
        if (UWGameScreenUw2.IsModernScheme)
            return fModernAttack != null && fModernAttack.IsPressed();

        return Mouse.current != null && Mouse.current.rightButton.isPressed;
    }

    private void fBegin(UWGameScreenUw2 pOScreen)
    {
        Camera lOCamera = UWViewpoint.Current;
        AttackTypes leKind = UWGameScreenUw2.IsModernScheme && lOCamera != null
            ? UWPlayerAttackState.KindOfPitch(-Mathf.DeltaAngle(0f, lOCamera.transform.eulerAngles.x))
            : UWPlayerAttackState.KindOfThird(pOScreen.ViewHeightOf(UWPointer.Position));

        mOState.SetKind(leKind);
        mOState.Begin(fChargeSpeed(pOScreen));
        mePhase = PhaseEnum.Raising;
        pOScreen.BeginSwing(leKind);
        pOScreen.ShowPower(1);
    }

    /// <summary>What one charge step adds: the weapon's speed in the melee table, the fist's otherwise.</summary>
    private static int fChargeSpeed(UWGameScreenUw2 pOScreen)
    {
        DataImport lOData = UWScene.LevelLoader != null ? UWScene.LevelLoader.UWDataImporter : null;

        if (lOData == null || lOData.ObjectClassProperties == null
            || !lOData.ObjectClassProperties.TryGetMeleeWeapon(fWeaponId(pOScreen), out UWObjectClassProperties.MeleeWeapon lOMelee)
            || lOMelee.AttackSpeed <= 0)
            return UWPlayerAttackState.FallbackChargeSpeed;

        return lOMelee.AttackSpeed;
    }

    private static UWObject fWeapon(UWGameScreenUw2 pOScreen)
    {
        return pOScreen.Inventory != null ? pOScreen.Inventory.GetEquipped(UWInventoryModel.FromSavegameSlot(pOScreen.WeaponHandSlot)) : null;
    }

    private static int fWeaponId(UWGameScreenUw2 pOScreen)
    {
        UWObject lOWeapon = fWeapon(pOScreen);

        return lOWeapon == null || lOWeapon.ID > UWPlayerCritterRow.MeleeLastId ? UWCombat.FistObjectId : lOWeapon.ID;
    }

    private void fStrike(UWGameScreenUw2 pOScreen, UWVitalsUw2 pOPlayer)
    {
        Camera lOCamera = UWViewpoint.Current;
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOCamera == null || lOLoader == null)
            return;

        Ray lORay = new Ray(lOCamera.transform.position, lOCamera.transform.forward);
        UWEntityInfo lOTarget = fFirstTarget(lORay, out Vector3 lOPoint);
        UWDamageable lOBody = lOTarget != null ? lOTarget.GetComponentInParent<UWDamageable>() : null;

        if (lOBody == null || !lOBody.TakesBlows)
        {
            UWAudioService.PlayEffect(UWSoundEffectId.Miss);

            return;
        }

        pOPlayer.RefreshStatus();

        UWPlayerVitals lOVitals = pOPlayer.Vitals;
        UWCritter lOCritter = lOTarget.GetComponentInParent<UWCritter>();
        IUWAttackDefender lIDefender = lOCritter != null ? lOCritter : UWAttackResolution.ObjectDefender;
        int liPart = lOCritter != null ? lOCritter.PickPartHitBy(pOPlayer.FeetZPos, pOPlayer.FeetZPos + pOPlayer.BodyHeightZ) : UWAttackResolution.NoPart;
        int liHeading = UWPlayerThrow.GetPlayerHeading(lOCamera.transform) >> 5;
        int liFlank = lOCritter != null ? UWCritterRules.GetFlankingBonus(liHeading, lOCritter.FacingEighth) : 0;

        UWPlayerAttack.GetWeaponEnchantment(fWeapon(pOScreen), null, out int liAccuracy, out int liDamage);

        UWPlayerAttack.MeleeResult lOResult = UWPlayerAttack.ResolveMelee(lOLoader.UWDataImporter, fWeaponId(pOScreen), UWPlayerAttackState.CombatKindOf(mOState.PendingKind),
            mOState.PendingCharge, lOVitals.Strength, lOVitals.Attack, lOVitals.Dexterity, lOVitals.GetSkill(UWPlayerData.Skill.Unarmed), lOVitals.GetSkill, lIDefender, liPart,
            lOVitals.IsEasyDifficulty, liFlank, liAccuracy, liDamage, lOTarget.ObjectData != null ? lOTarget.ObjectData.ID : -1, false, null);

        if (lOResult.NoWeapon)
            return;

        UWWorld lOWorld = UWWorld.Of(lOLoader);
        var lOAim = lOCritter != null ? UWWorld.TargetOf(lOCritter, lOBody) : UWWorld.TargetOf(lOBody);

        if (!lOResult.Hit)
            UWAudioService.PlayEffectAt(lOResult.Outcome.BlockedSound, lOPoint);
        else
            UWAudioService.PlayEffectAt(UWSoundEffectId.HitCritter, lOPoint, lOResult.Outcome.HitVolume);

        // A miss lands too, with nothing: the creature notices the blow.
        if (lOWorld != null && (lOResult.Hit || lOCritter != null))
            lOWorld.Damage(lOAim, lOResult.Outcome.Blow);
    }

    /// <summary>The nearest thing with object data before the eye that takes blows; a wall in the way ends the
    /// search. The close range's sphere when the ray finds nothing, for a blow a little beside its mark.</summary>
    private static UWEntityInfo fFirstTarget(Ray pORay, out Vector3 pOPoint)
    {
        UWEntityInfo lOFound = fFirstIn(Physics.RaycastAll(pORay, Reach, ~0, QueryTriggerInteraction.Collide), out pOPoint);

        return lOFound != null ? lOFound : fFirstIn(Physics.SphereCastAll(pORay, CloseRadius, Reach, ~0, QueryTriggerInteraction.Collide), out pOPoint);
    }

    private static UWEntityInfo fFirstIn(RaycastHit[] paHits, out Vector3 pOPoint)
    {
        System.Array.Sort(paHits, (pOA, pOB) => pOA.distance.CompareTo(pOB.distance));

        foreach (RaycastHit lOHit in paHits)
        {
            UWEntityInfo lOInfo = lOHit.collider.GetComponentInParent<UWEntityInfo>();

            if (lOInfo != null && lOInfo.PartOf != null)
                lOInfo = lOInfo.PartOf;

            if (lOInfo != null && lOInfo.GetComponentInParent<UWDamageable>() != null)
            {
                pOPoint = lOHit.point;

                return lOInfo;
            }

            if (lOInfo == null && lOHit.collider.GetComponent<CharacterController>() == null && !lOHit.collider.isTrigger)
                break;
        }

        pOPoint = Vector3.zero;

        return null;
    }
}
