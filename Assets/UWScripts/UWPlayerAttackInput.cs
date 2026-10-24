using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE WEAPON BUTTON (cleanup step 5.14, 2026-10-22: out of Interaction's Update and its attack
/// fields, which keeps the public surface and forwards): the press that begins a blow - where it
/// went down, from the pad or the mouse -, the kind of blow and the clicked ninth, the charge
/// after the wind-up, the release that becomes a strike or a shot, and the hit frame that resolves
/// it (UWPlayerMelee, UWPlayerMissiles). The state is the engine-free UWPlayerAttackState.
/// Interaction ticks UpdatePress before its modal gates and UpdateReleaseOrCharge at the right
/// button's place behind them, as the lines ran there.
/// </summary>
public sealed class UWPlayerAttackInput
{
    private readonly Interaction mOInteraction;

    private readonly UWPlayerAttackState mOState;

    private readonly UWPlayerMelee mOMelee;

    private readonly UWPlayerMissiles mOMissiles;

    public UWPlayerAttackInput(Interaction pOInteraction, UWPlayerAttackState pOState, UWPlayerMelee pOMelee,
        UWPlayerMissiles pOMissiles)
    {
        mOInteraction = pOInteraction;
        mOState = pOState;
        mOMelee = pOMelee;
        mOMissiles = pOMissiles;
    }

    /// <summary>The attack state: press, kind, charge and what the release kept.</summary>
    public UWPlayerAttackState State => mOState;

    private UWDataImport.DataImport fData()
    {
        return mOInteraction.GameData;
    }

    private UWCharacter fCharacter()
    {
        return mOInteraction.Character;
    }

    private UWInventory fInventory()
    {
        return mOInteraction.Inventory;
    }

    private UWGameUI fUi()
    {
        return mOInteraction.GameUi;
    }

    /// <summary>
    /// The aiming cursor when shooting.
    ///
    /// A LAUNCHER HAS NO CHARGE, BY THE USER'S DECISION OF 2026-10-12 TO FOLLOW THE ORIGINAL
    /// (combat C2): in fire mode player_attack (COMBAT.C) waits for the same wind-up as any
    /// weapon; when the weapon reaches its hold frame (weap_frame 3) the gem jumps to frame 9 and
    /// the crosshair appears, and only a release after that shoots - released earlier, nothing
    /// flies. The user's "about half a second, bow and sling alike" (2026-09-10) is that
    /// wind-up. Until then ours took the reference's reading - the crosshair at the masked melee
    /// entry's minimum charge, 0.05 to 0.08 s after the press - so arrows went off at the
    /// clicking speed.
    /// </summary>
    public bool IsRangedTargeting
    {
        get
        {
            UWCharacter lOCharacter = fCharacter();

            return mOInteraction.IsCombatModeActive && mOState.IsAttacking && lOCharacter != null && lOCharacter.Weapon.HasRangedWeapon
                && lOCharacter.Weapon.IsWindupComplete;
        }
    }

    /// <summary>The button that charges and releases the weapon: the right one in the original,
    /// the left one in the modern scheme - or the gamepad's right trigger (ModernAttack).</summary>
    public InputAction AttackButton()
    {
        return UWControlScheme.Ui.Policy.RightButtonAttacks ? mOInteraction.Input.Interact : mOInteraction.Input.ModernAttack;
    }

    /// <summary>A launcher without ammunition refused this press (fBeginAttack); it stays refused
    /// until the button is up.</summary>
    private bool mbRefusedUntilRelease;

    /// <summary>
    /// The press, before Interaction's modal gates, and the weapon in hand following the
    /// equipment.
    /// </summary>
    public void UpdatePress()
    {
        UWCharacter lOCharacter = fCharacter();
        UWControlScheme lOScheme = mOInteraction.ControlScheme;

        // Pressing again during the strike animation starts the next strike afterwards
        // (per user, 2026-08-30). Instead of keeping a queue, it simply
        // starts as soon as the weapon is ready again and the button is still held.
        // THIS BLOCK COMES BEFORE THE MODAL CHECK further down and therefore needs it
        // itself: without it, a still-held Interact button during a
        // detached camera still triggered a strike including the weapon animation.
        // WHERE THE RIGHT BUTTON WENT DOWN decides for the whole time it is held (per user on the
        // original, 2026-09-19): a press over the UI never charges the weapon, not even while
        // the button stays down and the weapon becomes ready. Read on the press frame, so the
        // held-button path below and the press path further down agree.
        // THE WEAPON BUTTON: the right one in the original, the left one in the modern scheme
        // (concept per user, 2026-10-03), where the right button looks.
        InputAction lOAttackButton = AttackButton();

        // The press a launcher without ammunition refused ends with the button (fBeginAttack).
        if (mbRefusedUntilRelease && !lOAttackButton.IsPressed())
            mbRefusedUntilRelease = false;

        if (lOAttackButton.WasPressedThisFrame())
        {
            // While the gamepad's pointer drives, its trigger is the left mouse button
            // (UWGamepadPointer.Takes): the strike goes where the pointer is, as with the mouse.
            mOState.FromPad = UWGamepad.IsFromGamepad(lOAttackButton) && !UWGamepadPointer.Takes(lOAttackButton);

            // In the classic view window; the modern scheme: on the world, not on a window of its
            // UI (a drag from a bag onto the world must not strike) - IUWUiScheme.PressAimsAtWorld.
            mOState.PressInViewport = UWControlScheme.Ui.PressAimsAtWorld(mOState.FromPad);
        }

        if (mOInteraction.IsCombatModeActive && !mOState.IsAttacking && !mOState.BlowPending && mOState.PressInViewport
            && lOCharacter != null && lOCharacter.Weapon.CanAttack && lOAttackButton.IsPressed() && !UWPlayerInput.ConsumesRightClick()
            && (lOScheme == null || !lOScheme.IsWorldInputBlocked) && (mOState.FromPad || mOInteraction.MouseInWorld()))
            fBeginAttack();

        // The weapon in hand follows the equipment. This is checked continuously because the
        // weapon can be changed in the original even in the middle of combat mode - then first the
        // old one is sheathed and then the new one drawn (see
        // UWWeaponSwing.SetWeaponType). The call is cheap: it only does something when the
        // weapon has actually changed.
        if (mOInteraction.IsCombatModeActive)
            fUpdateWeaponType();
    }

    /// <summary>
    /// The classic scheme's right press in the viewport with the weapon drawn.
    /// IN COMBAT MODE THE RIGHT BUTTON IS THE WEAPON AND NOTHING ELSE (per user, 2026-09-18):
    /// no look, no use - and no striking while sheathing or drawing either, in the original
    /// the draw motion must finish first (per user, 2026-08-30).
    /// </summary>
    public void BeginFromRightPress()
    {
        UWCharacter lOCharacter = fCharacter();

        // THE WEAPON CHARGES ONLY FROM A PRESS IN THE VIEWPORT (per user on the original,
        // 2026-09-19, correcting the first reading that combat mode locked everything):
        // over the UI the right button does what it always does there, in combat mode too.
        if (mOState.PressInViewport && (lOCharacter == null || lOCharacter.Weapon.CanAttack))
            fBeginAttack();
    }

    /// <summary>
    /// Behind Interaction's modal gates, when the frame brought no classic right press: the
    /// release that becomes a strike or a shot, the charge while the button is held, and a blow
    /// forgotten when the button is up. pbRightClickFree: no cursor movement used the click up
    /// (UWPlayerInput.ConsumesRightClick).
    /// </summary>
    public void UpdateReleaseOrCharge(bool pbRightClickFree)
    {
        InputAction lOAttackButton = AttackButton();
        UWCharacter lOCharacter = fCharacter();

        if (pbRightClickFree && lOAttackButton.WasReleasedThisFrame() && mOInteraction.IsCombatModeActive && mOState.IsAttacking)
        {
            // Releasing starts the strike animation; whether it hits is only decided
            // when it reaches its hit frame (see UWWeaponSwing.AttackConnected).
            // The charge and the attack type must therefore be kept until then -
            // both are reset just below.
            // The charge counter is passed on unchanged; the conversion to the
            // weapon's damage factor only happens on the hit (UWCombat.MapCharge).
            // Previously this additionally counted from the minimum charge here - that counted the
            // factor twice.
            //
            // Whether the release becomes a strike depends solely on the windup:
            // if it has completed, the strike happens, otherwise it plays backwards
            // and the weapon returns to ready (checked by user in the original,
            // 2026-09-01). The minimum charge does NOT decide this - it is the
            // lower bound of the damage factor. A strike from a barely charged weapon
            // does happen, but does little.
            mOState.Release(lOCharacter == null || lOCharacter.Weapon.IsWindupComplete);

            // A LAUNCHER SHOOTS ON RELEASE, not at a hit frame: PlayerCombat_seg022_11BA calls
            // MissileRelease_seg025_9F the moment the button comes up, with the pointer in the
            // view window (seg013_1CC9_115) - twang, launch sound and the no-room message come at
            // once (per user on the original, 2026-09-24; ours came a little late, with the
            // strike animation). But only once the aiming cursor is up (IsRangedTargeting, the
            // same state that switches the cursor there: the wind-up is through); released
            // earlier, nothing is shot (per user on the original; COMBAT.C player_attack).
            if (mOMissiles.HoldsLauncher())
            {
                bool lbDrawn = IsRangedTargeting;

                mOState.BlowPending = false;

                if (lbDrawn)
                    fResolveAttack();
            }

            // The character only learns THAT the button was released. Whether that becomes a retraction
            // or a strike depends on its windup state; whether the
            // strike hits is decided by the pending blow here.
            if (lOCharacter != null)
                lOCharacter.Weapon.ReleaseAttack();

            mOState.IsAttacking = false;
            mOState.ClearCharge();
            mOInteraction.CancelWorldPointer();
        }
        else if (lOAttackButton.IsPressed())
        {
            fUpdateCharge(Time.deltaTime);
        }
        else if (!lOAttackButton.IsPressed())
        {
            if (mOState.IsAttacking)
            {
                mOState.ClearCharge();
                mOState.IsAttacking = false;
            }
        }
    }

    /// <summary>
    /// player_attack's charge while the button is held: nothing during the wind-up; on the
    /// first frame after it the clock starts (elapsed -1 to 0), then every
    /// AttackChargeStepSeconds the weapon's speed is added, capped at 100. A launcher never
    /// charges (fire mode returns before this). The steps are UWPlayerAttackState.StepCharge.
    /// </summary>
    private void fUpdateCharge(float pfDeltaSeconds)
    {
        UWCharacter lOCharacter = fCharacter();

        if (!mOState.IsAttacking || (lOCharacter != null && (!lOCharacter.Weapon.IsWindupComplete || lOCharacter.Weapon.HasRangedWeapon)))
            return;

        float lfStep = UnderworldRevisited.UWSettings.Instance != null
            ? UnderworldRevisited.UWSettings.Instance.AttackChargeStepSeconds
            : UWPlayerAttackState.DefaultChargeStepSeconds;

        mOState.StepCharge(pfDeltaSeconds, lfStep);
    }

    /// <summary>The strike animation has reached its hit frame - now it is checked whether
    /// something is in range.</summary>
    public void OnAttackConnected()
    {
        if (!mOState.TakePendingBlow())
            return;

        fResolveAttack();
    }

    /// <summary>
    /// Evaluates a strike at its hit frame, or a shot on the release: with a launcher the
    /// missile (UWPlayerMissiles.TryShoot), else the blow (UWPlayerMelee.Strike).
    /// </summary>
    private void fResolveAttack()
    {
        if (fData() == null || mOState.PendingKind == AttackTypes.Ready)
            return;

        // WITH BOW, CROSSBOW OR SLING you shoot instead of strike - and that
        // works completely differently: no ray at a target, but a projectile that flies.
        if (mOMissiles.TryShoot())
            return;

        mOMelee.Strike();
    }

    /// <summary>
    /// Begins a strike: the wind-up starts, the charge waits for it (fUpdateCharge, by the
    /// weapon's speed, fGetChargeSpeed).
    /// </summary>
    private void fBeginAttack()
    {
        // A LAUNCHER WITHOUT AMMUNITION does not begin at all (UWPlayerMissiles.CheckAmmunitionToBegin):
        // the message at once, and the press is spent until the button comes up, as check_ammo's
        // caller does with mouse_release - the held button would else ask again every frame.
        if (mbRefusedUntilRelease)
            return;

        if (!mOMissiles.CheckAmmunitionToBegin())
        {
            mbRefusedUntilRelease = true;
            return;
        }

        fChooseAttackKind();

        mOState.Begin(fGetChargeSpeed());
    }

    /// <summary>
    /// What one charge step adds for the weapon in hand: the melee table's speed (wd->speed in
    /// player_attack, COMBAT.C), the fist's for anything that is not a melee weapon
    /// (GetPlayerWeapon). A heavy weapon charges slowly to a high maximum, a light one quickly
    /// to a low one:
    ///
    ///   a_dagger        speed 30  MaxCharge 145   -> short
    ///   a_longsword     speed 15  MaxCharge 190
    ///   a_battle axe    speed  5  MaxCharge 220   -> long
    ///
    /// THE TIMING FOLLOWS THE ORIGINAL BY THE USER'S DECISION OF 2026-10-12 (combat C1, C2): the
    /// charge starts at 0 after the wind-up (four frames of 0.125 s) and gains the speed every
    /// UWSettings.AttackChargeStepSeconds; a launcher does not charge at all. Until then a fixed
    /// base of 0.73 s plus the steps was counted from the press, fitted to the user's stopwatch
    /// (2026-08-30, press to green: dagger 0.9, fist 1.2, Sword of Justice 1.4, broadsword 1.4 -
    /// 1.5, battle axe 2.05 s). The step of 0.072 s from that fit stays: wind-up 0.5 s plus the
    /// steps to frame 9 meets the same measurements within a reaction time (dagger 0.79, battle
    /// axe 1.94). The original's step is 0x10 ticks of its game clock, 0.0625 s if that is the
    /// 1/256 s clock (not settled).
    /// </summary>
    private int fGetChargeSpeed()
    {
        UWInventory lOInventory = fInventory();
        UWDataImport.DataImport lOData = fData();
        UWObject lOWeapon = lOInventory == null ? null : lOInventory.GetEquipped(lOInventory.MainHandSlot);
        int liWeaponId = lOWeapon == null || lOWeapon.ID > UWPlayerCritterRow.MeleeLastId ? UWCombat.FistObjectId : lOWeapon.ID;

        UWObjectClassProperties.MeleeWeapon lOMelee;

        if (lOData == null || lOData.ObjectClassProperties == null
            || !lOData.ObjectClassProperties.TryGetMeleeWeapon(liWeaponId, out lOMelee)
            || lOMelee.AttackSpeed <= 0)
            return UWPlayerAttackState.FallbackChargeSpeed;

        return lOMelee.AttackSpeed;
    }

    /// <summary>
    /// Chooses the attack type from the pointer position in the view window: upper third
    /// overhead strike, lower third stab, in between the sideways slash (per user,
    /// 2026-08-30). The same division into thirds that movement also follows.
    ///
    /// Both are set: the type for the damage calculation (UWPlayerAttackState.Kind, see
    /// UWPlayerAttackState.CombatKindOf) and the one for the weapon animation (UWWeaponSwing.AttackType).
    ///
    /// Unarmed there is only ONE animation - the weapon data list the same coordinates for the fist
    /// for all three types. The damage values of the melee table do differ there
    /// though (slash 2, bash 4, stab 3), so the
    /// type is evaluated with bare hands too. Whether the original does the same is open.
    /// </summary>
    private void fChooseAttackKind()
    {
        // THE MODERN SCHEME takes the view's height for the pointer's third (per user, 2026-10-03,
        // after trying the mouse movement while winding up - the view turned with it and the kind
        // came out unreliably): looking up an overhead bash, down a thrust, straight a slash,
        // fixed when the button goes down, as in the original.
        //
        // WITH THE POINTER FREE the original's thirds again, of the screen - the modern view fills
        // it (per user, 2026-10-04): upper third an overhead bash, lower a thrust, between a slash.
        //
        // Which of the three the scheme takes is its answer (IUWUiScheme.AttackKindSource since
        // cleanup 5.19; UWModernUiScheme holds the two modern cases).
        UWAttackKindSource leSource = UWControlScheme.Ui.AttackKindSource;

        if (leSource == UWAttackKindSource.PointerThirdsOnScreen)
        {
            float lfAt = Mouse.current.position.ReadValue().y / Mathf.Max(1f, Screen.height);

            fSetAttackKind(UWPlayerAttackState.KindOfThird(lfAt));

            return;
        }

        if (leSource == UWAttackKindSource.ViewPitch)
        {
            fSetAttackKind(UWPlayerAttackState.KindOfPitch(fGetViewPitchUp()));

            return;
        }

        UWGameUI lOUi = fUi();

        float lfFraction = lOUi != null
            ? lOUi.ClassicScreen.GetVerticalFractionInGameArea(Mouse.current.position.ReadValue())
            : 0.5f;

        fSetAttackKind(UWPlayerAttackState.KindOfThird(lfFraction));

        // THE COLUMN COUNTS TOO (by the user's decision of 2026-10-12 to follow the original,
        // combat C6): the original's swing is the clicked ninth, 1 to 9 from the bottom left, and
        // resolve_attack raises the blow by towhere / 3 thirds of the body - so the right column
        // (3, 6, 9) sits a third higher than the rest of its row. Until then ours took the middle
        // column for every row. The modern scheme keeps the middle column (fSetAttackKind).
        float lfAcross = lOUi != null
            ? lOUi.ClassicScreen.GetHorizontalFractionInGameArea(Mouse.current.position.ReadValue())
            : 0.5f;

        mOState.SetColumn(UWPlayerAttackState.ColumnOfThird(lfAcross));
    }

    /// <summary>Sets both the kind for the damage (UWPlayerAttackState.Kind) and the one for the weapon
    /// animation (UWWeaponSwing.AttackType, read every frame, so a change while the weapon
    /// winds up shows at once). The middle column of the kind's row; the classic scheme sets the
    /// clicked column after.</summary>
    private void fSetAttackKind(AttackTypes peKind)
    {
        mOState.SetKind(peKind);

        UWCharacter lOCharacter = fCharacter();

        if (lOCharacter != null)
            lOCharacter.Weapon.AttackType = peKind;
    }

    /// <summary>The camera's pitch in degrees, positive looking up.</summary>
    private float fGetViewPitchUp()
    {
        Camera lOCamera = Camera.main;

        return lOCamera != null ? -Mathf.DeltaAngle(0f, lOCamera.transform.eulerAngles.x) : 0f;
    }

    /// <summary>
    /// Takes the weapon from the main hand into the display. Called every frame while
    /// combat mode is active, because the weapon can be changed in the middle of combat
    /// mode (see UpdatePress and UWWeaponSwing.SetWeaponType); it only does something when the
    /// weapon has actually changed.
    ///
    /// Which animation belongs to which weapon is given by the SkillType field of the
    /// melee table - the same source as for the skill.
    /// </summary>
    private void fUpdateWeaponType()
    {
        UWCharacter lOCharacter = fCharacter();
        UWInventory lOInventory = fInventory();
        UWDataImport.DataImport lOData = fData();

        if (lOCharacter == null || lOData == null)
            return;

        UWObject lOWeapon = lOInventory == null ? null : lOInventory.GetEquipped(lOInventory.MainHandSlot);
        int liWeaponId = lOWeapon == null ? UWCombat.FistObjectId : lOWeapon.ID;

        // WITH BOW OR SLING NOTHING IS VISIBLE - see UWWeaponSwing.HasRangedWeapon.
        int liAmmunitionId;

        // What was in view a moment ago decides whether the change needs the two motions -
        // see UWWeaponSwing.SetWeaponType. With a bow in hand nothing was.
        bool lbNothingWasVisible = lOCharacter.Weapon.HasRangedWeapon;

        lOCharacter.Weapon.HasRangedWeapon = mOMissiles.TryGetAmmunitionId(liWeaponId, out liAmmunitionId);

        if (lOCharacter.Weapon.HasRangedWeapon)
            return;

        UWObjectClassProperties.MeleeWeapon lOMelee;
        WeaponTypes leType = WeaponTypes.RightHandFist;

        if (lOData.ObjectClassProperties.TryGetMeleeWeapon(liWeaponId, out lOMelee))
            leType = UWCombat.GetWeaponTypeForWeapon(lOMelee, lOInventory != null && lOInventory.IsLeftHanded);
        else if (lOInventory != null && lOInventory.IsLeftHanded)
            leType = WeaponTypes.LeftHandFist;

        // AN EMPTY HAND IS THE FIST, not whatever was held before. Without this the weapon
        // type simply kept its last value, which is how an axe could turn up in a hand that
        // held none (per user, 2026-09-20).
        lOCharacter.Weapon.SetWeaponType(leType, lbNothingWasVisible);
    }
}
