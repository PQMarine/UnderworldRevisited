using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE PLAYER'S BLOW ON THE SECOND GAME (UW2 plan phase 4): the first game's button, wind-up, charge and
/// resolution (UWPlayerAttackState, UWPlayerAttack.ResolveMelee) over the second game's player, as UWPlayerMelee and
/// UWPlayerAttackInput do over Interaction, which does not run here. Classic: Fight chosen, the right button in the
/// view; the pointer's ninth picks the kind and the height. Modern: the attack button, the view's pitch picks it. The
/// blow itself is UWPlayerMelee's, this class its host: the target, the wall, the wear, the blood and the eyes. A bow
/// or sling in the hand does not charge: the idle steps to the hold, the gem at once full and the aiming pointer, and
/// a release in the view shoots (player_attack's fire_mode, UWPlayerMissiles). The special weapons' powers are still
/// to come.
/// </summary>
public sealed class UWAttackUw2 : MonoBehaviour, IUWLookHost
{
    public static UWAttackUw2 Current { get; private set; }

    /// <summary>How far a blow reaches in the modern scheme (Interaction's mfAttackRange).</summary>
    private const float Reach = 48f;

    private readonly UWPlayerAttackState mOState = new UWPlayerAttackState();

    private UWPlayerMelee mOMelee;

    private UWPlayerMissiles mOMissiles;

    private UWHitFeedback mOFeedback;

    /// <summary>The look, the first game's over this host.</summary>
    public UWLookService Look { get; private set; }

    private void Awake()
    {
        Current = this;
        mOFeedback = new UWHitFeedback(this);
        mOMelee = new UWPlayerMelee(this, mOState, mOFeedback);
        mOMissiles = new UWPlayerMissiles(this);
        Look = new UWLookService(this);
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    // ------------------------------------------------- IUWMeleeHost

    public DataImport GameData => UWScene.LevelLoader != null ? UWScene.LevelLoader.UWDataImporter : null;

    public UWLevelLoader LevelLoader => UWScene.LevelLoader;

    public CharacterController MeleeController => UWScene.PlayerMovement != null ? UWScene.PlayerMovement.GetComponentInParent<CharacterController>() : null;

    public Transform MeleeFacing => UWViewpoint.Current != null ? UWViewpoint.Current.transform : transform;

    public Camera MeleeCamera => UWViewpoint.Current;

    public float AttackRange => Reach;

    public Ray InteractionRay()
    {
        return new Ray(MeleeFacing.position, MeleeFacing.forward);
    }

    public bool MeleeByHitArea => !UWGameScreenUw2.IsModernScheme;

    public UWPlayerVitals MeleeVitals => UWVitalsUw2.Current != null ? UWVitalsUw2.Current.Vitals : null;

    public int MeleeFeetZPos => UWVitalsUw2.Current != null ? UWVitalsUw2.Current.FeetZPos : 0;

    public UWInventoryModel MeleeInventory => UWGameScreenUw2.Current != null ? UWGameScreenUw2.Current.Inventory : null;

    public int MeleeAttackBonus => UWVitalsUw2.Current != null ? UWVitalsUw2.Current.Valor : 0;

    public int PoisonWeaponShare => UWVitalsUw2.Current != null ? UWVitalsUw2.Current.PoisonWeaponShare : 0;

    public void AddMessage(string psMessage)
    {
        UWGameScreenUw2.Say(psMessage);
    }

    public void EquipmentWorn()
    {
        if (UWVitalsUw2.Current != null)
            UWVitalsUw2.Current.RefreshStatus();
    }

    public bool ShowsFoeCondition => UWGameScreenUw2.Current != null;

    public void ShowFoeCondition(int piHitPoints, int piTableHitPoints, string psName)
    {
        UWGameScreenUw2.Current.ShowEyes(piHitPoints, piTableHitPoints);
    }

    // ------------------------------------------------- IUWMissileHost

    /// <summary>player_settr in the classic scheme; the modern one shoots where the free pointer points, else
    /// along the crosshair, as on the first game.</summary>
    public void AimMissile(int piSpeedByte, out int piHeading, out int piPitch)
    {
        UWGameScreenUw2 lOScreen = UWGameScreenUw2.Current;

        if (!UWGameScreenUw2.IsModernScheme && lOScreen != null)
        {
            lOScreen.PointerInMissileWindow(UWPointer.Position, out int liX, out int liY);
            UWPlayerThrow.GetMissileAimUw2(liX, liY, Camera.main.transform, out piHeading, out piPitch);

            return;
        }

        Mouse lOMouse = Mouse.current;
        Ray lORay = Cursor.lockState != CursorLockMode.Locked && lOMouse != null && MeleeCamera != null
            ? MeleeCamera.ScreenPointToRay(lOMouse.position.ReadValue()) : InteractionRay();

        UWMissiles.AimFromDirection(lORay.direction, piSpeedByte, out piHeading, out piPitch);
    }

    public void AddGeneralMessage(int piIndex)
    {
        AddMessage(UWText.Block(GameData, 1, piIndex, string.Empty));
    }

    public void ReportSpellHit(UWEntityInfo pOTarget, UWDamageable pODamageable, Vector3 pOPoint)
    {
        mOFeedback.ReportSpellHit(pOTarget, pODamageable, pOPoint);
    }

    public int GetThrownMissileBaseDamage(int piObjectId)
    {
        return mOMissiles.GetThrownMissileBaseDamage(piObjectId);
    }

    public int GetThrownMissileDamageType(int piObjectId)
    {
        return mOMissiles.GetThrownMissileDamageType(piObjectId);
    }

    // ------------------------------------------------- IUWLookHost

    public void AddLookLine(string psLine)
    {
        AddMessage(psLine);
    }

    public string GetGeneralMessage(int piIndex)
    {
        return UWText.Block(GameData, 1, piIndex, string.Empty);
    }

    public void ShowPagedText(string psText, params string[] psLeading)
    {
        foreach (string lsLine in psLeading)
        {
            if (!string.IsNullOrEmpty(lsLine))
                AddMessage(lsLine);
        }

        AddMessage(psText);
    }

    /// <summary>A critical hit on the player wears the piece of the part.</summary>
    public void WearOnCriticalHit(int piPart)
    {
        mOMelee.WearOnCriticalHit(piPart);
    }

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

    /// <summary>The button went down where a blow is meant: in the view, with the game free. That decides for
    /// as long as it stays down, as on the first game (UWPlayerAttackInput).</summary>
    private bool mbAimed;

    /// <summary>A launcher is in this swing (fire_mode).</summary>
    private bool mbFireMode;

    /// <summary>The bow is drawn: the aiming pointer, and a release shoots.</summary>
    public bool IsAiming { get; private set; }

    /// <summary>A launcher without ammunition refused the press; spent until the button is up (mouse_release).</summary>
    private bool mbRefused;

    private static bool fBusy => UWConversationScreenUw2.IsOpen || UWMapScreenUw2.IsOpen || UWGameScreenUw2.IsOptionsOpen || UWGameScreenUw2.IsAskingCount;

    /// <summary>player_attack, over the step flags of the weapon's animation: the lead-in gives way when the button
    /// comes up, the hold charges while it stays down, and the strike's step lands the blow.</summary>
    private void Update()
    {
        UWGameScreenUw2 lOScreen = UWGameScreenUw2.Current;
        UWVitalsUw2 lOVitals = UWVitalsUw2.Current;

        if (lOScreen == null || lOVitals == null || lOVitals.Vitals == null)
            return;

        if (fWentDown())
            mbAimed = fCanAim(lOScreen) && fPressed(lOScreen);

        if (mbRefused && !fHeld())
            mbRefused = false;

        // PRESSED AGAIN DURING A BLOW, the next starts when the weapon is ready and the button still down (as on
        // the first game, per user 2026-08-30).
        if (mePhase == PhaseEnum.None)
        {
            if (fCanBegin(lOScreen) && mbAimed && fHeld() && !mbRefused)
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

        if (mbFireMode)
        {
            fUpdateShot(lOScreen, liFlag, lbHeld);

            return;
        }

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
                fStrike(lOVitals);
        }
    }

    /// <summary>player_attack's fire_mode: let go before the hold, nothing flies; at the hold the gem goes full
    /// and the aiming pointer comes up; the release after it shoots, if the pointer stands in the view.</summary>
    private void fUpdateShot(UWGameScreenUw2 pOScreen, int piFlag, bool pbHeld)
    {
        if (piFlag < UWClassicLayoutUw2.Weapon.HoldFlag)
        {
            if (!pbHeld)
            {
                pOScreen.CancelSwing();
                fFinish(pOScreen);
            }

            return;
        }

        if (!IsAiming)
        {
            IsAiming = true;
            pOScreen.ShowPower(UWClassicLayoutUw2.PowerGem.FullStep);

            return;
        }

        if (pbHeld)
            return;

        if (!fBusy && (UWGameScreenUw2.IsModernScheme || pOScreen.IsInView(UWPointer.Position)))
            mOMissiles.TryShoot();

        pOScreen.StopSwing();
        fFinish(pOScreen);
    }

    /// <summary>clear_fight_state: a blow under way ends here.</summary>
    public void ClearFight()
    {
        mbAimed = false;

        if (mePhase == PhaseEnum.None || UWGameScreenUw2.Current == null)
            return;

        UWGameScreenUw2.Current.StopSwing();
        fFinish(UWGameScreenUw2.Current);
    }

    private void fFinish(UWGameScreenUw2 pOScreen)
    {
        mePhase = PhaseEnum.None;
        mbFireMode = false;
        IsAiming = false;
        mOState.IsAttacking = false;
        mOState.ClearCharge();
        pOScreen.ShowPower(0);
    }

    private static bool fCanAim(UWGameScreenUw2 pOScreen)
    {
        return !fBusy && !pOScreen.IsBagsOpen && pOScreen.Inventory != null && pOScreen.Inventory.CursorItem == null;
    }

    private static bool fCanBegin(UWGameScreenUw2 pOScreen)
    {
        return fCanAim(pOScreen) && pOScreen.IsWeaponReady;
    }

    private static InputAction fModernAttack => UWControlScheme.SharedControls != null ? UWControlScheme.SharedControls[UWAct.ModernAttack] : null;

    private static bool fWentDown()
    {
        if (UWGameScreenUw2.IsModernScheme)
            return fModernAttack != null && UWMouseButtons.PressedFree(fModernAttack);

        return UWMouseButtons.GameMouse != null && UWMouseButtons.GameMouse.rightButton.wasPressedThisFrame;
    }

    private static bool fPressed(UWGameScreenUw2 pOScreen)
    {
        if (UWGameScreenUw2.IsModernScheme)
            return fModernAttack != null && UWMouseButtons.PressedFree(fModernAttack);

        Mouse lOMouse = UWMouseButtons.GameMouse;

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
        mbFireMode = mOMissiles.HoldsLauncher();

        // A launcher without its ammunition says so and does not begin (GetPlayerWeapon).
        if (mbFireMode && !mOMissiles.CheckAmmunitionToBegin())
        {
            mbFireMode = false;
            mbRefused = true;

            return;
        }

        Camera lOCamera = UWViewpoint.Current;
        AttackTypes leKind = UWGameScreenUw2.IsModernScheme && lOCamera != null
            ? UWPlayerAttackState.KindOfPitch(-Mathf.DeltaAngle(0f, lOCamera.transform.eulerAngles.x))
            : UWPlayerAttackState.KindOfThird(pOScreen.ViewHeightOf(UWPointer.Position));

        mOState.SetKind(leKind);

        // The clicked ninth, the right column a third higher (resolve_attack's towhere / 3); modern the middle's.
        if (!UWGameScreenUw2.IsModernScheme)
            mOState.SetColumn(UWPlayerAttackState.ColumnOfThird(pOScreen.ViewAcrossOf(UWPointer.Position)));

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

    private void fStrike(UWVitalsUw2 pOPlayer)
    {
        pOPlayer.RefreshStatus();
        mOMelee.Strike();
    }
}
