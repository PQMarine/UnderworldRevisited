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

        // Free is out of the look (UWPointer.IsLookLocked): the system pointer stays captured in the running game, ours
        // is the pointer.
        Ray lORay = !UWPointer.IsLookLocked && UWPointer.HasMouse && MeleeCamera != null
            ? MeleeCamera.ScreenPointToRay(UWPointer.Position) : InteractionRay();

        UWMissiles.AimFromDirection(lORay.direction, piSpeedByte, out piHeading, out piPitch);
    }

    public void AddGeneralMessage(int piIndex)
    {
        AddMessage(UWText.Block(GameData, 1, piIndex, string.Empty));
    }

    // ------------------------------------------------- A missile spell waiting for its aim (UW2 plan G1 part C1)

    /// <summary>release_missile's "There is not enough room to release that spell." (string 0x10F of block 1).</summary>
    private const int NoRoomForSpellString = 0x10F + 1;

    /// <summary>spell_fire's sound on the launched missile: 0x28, the acid and the snowball 0xA (uw2 MISSILE.C).</summary>
    private const int SpellLaunchSound = 0x28;

    private readonly UWPendingSpellState mOSpell = new UWPendingSpellState();

    private int miSpellClickFrame = -1;

    /// <summary>The frame the waiting missile began: the click on the hollow that cast it does not release it too.</summary>
    private int miSpellBeganFrame = -1;

    /// <summary>A missile spell waits for its aim: do_spell's GameInputMode 3 with the aiming pointer
    /// (force_mouse_cursor(0x1075), the picture the drawn bow has).</summary>
    public bool IsSpellWaiting => mOSpell.ProjectileId >= 0;

    /// <summary>The click belongs to the waiting spell: while it waits, and in the frame its click released it - no
    /// blow and no look start from that click.</summary>
    public bool SpellTookClick => IsSpellWaiting || IsTargetSpellWaiting || miSpellClickFrame == Time.frameCount;

    // ------------------------------------------------- A target spell waiting for its target (UW2 plan G1 part D)

    /// <summary>"You cannot reach that." (string 0x6B of block 1, mous_in_3d's target mode).</summary>
    private const int CannotReachString = 0x6B + 1;

    private int miTargetMinor = -1;

    private int miTargetCost;

    private IUWSpellHost mITargetHost;

    /// <summary>nail_1area's target mode (GameInputMode 2, force_mouse_cursor(0x1076)): a class 7 spell waits for the
    /// clicked thing.</summary>
    public bool IsTargetSpellWaiting => miTargetMinor >= 0;

    /// <summary>A missile or a target spell waits.</summary>
    public bool IsAnySpellWaiting => IsSpellWaiting || IsTargetSpellWaiting;

    /// <summary>nail_1area for the player: the spell, its held mana (mspell_mused) and the host that casts it.</summary>
    public void BeginSpellTarget(int piMinor, int piManaCost, IUWSpellHost pIHost)
    {
        miTargetMinor = piMinor;
        miTargetCost = piManaCost;
        mITargetHost = pIHost;
        miSpellBeganFrame = Time.frameCount;
    }

    /// <summary>Escape lets a waiting target spell go, its mana unpaid.</summary>
    public void CancelSpellTarget()
    {
        miTargetMinor = -1;
        miTargetCost = 0;
        mITargetHost = null;
    }

    /// <summary>
    /// mous_in_3d's target mode (uw2 ui/INTERACT.C case 2): the thing under the pointer (pick_3d; ours UWWorldPicker's
    /// spell target, the pickable thing first) - nothing there and the spell keeps waiting; out of the reach
    /// (InPickRange with PickDist, which Telekinesis lifts) "You cannot reach that." and it keeps waiting; else
    /// target_spells: the handler, the mana paid when it took, and the mode ends either way.
    /// </summary>
    private void fApplyTargetAt(Ray pORay)
    {
        UWEntityInfo lOTarget = UWWorldPicker.Measure(pORay).SpellTarget;

        if (lOTarget == null || lOTarget.ObjectData == null)
            return;

        Camera lOCamera = UWViewpoint.Current;
        Vector3 lOFeet = lOCamera != null ? UWPlayerThrow.GetFeet(lOCamera.transform) : Vector3.zero;
        bool lbTelekinesis = MeleeVitals != null && MeleeVitals.HasTelekinesis;
        UWObject lOData = lOTarget.ObjectData;
        int liTargetX, liTargetY, liTargetZ;

        // WHERE THE THING IS, as InPickRange measures it: a creature where it walks (its node), anything else by its
        // record - tile, fine place and height, as the first game's reach (Interaction.fGetTargetGridPosition) and the
        // walk's take. A door's node stands at its hinge, so Open said "You cannot reach that." in front of it (per user,
        // 2026-11-12, test list 30.5).
        if (UWObjectIds.Major(lOData.ID) == UWObjectIds.CreatureMajorClass)
        {
            Vector3 lOAt = lOTarget.transform.position;

            liTargetX = UWTileQueries.WorldToEighths(lOAt.x);
            liTargetY = UWTileQueries.WorldToEighths(lOAt.z);
            liTargetZ = UWUnits.WorldToZPos(lOAt.y);
        }
        else
        {
            liTargetX = UWReachRules.ToEighths(lOData.TileX, lOData.XPos);
            liTargetY = UWReachRules.ToEighths(lOData.TileY, lOData.YPos);
            liTargetZ = lOData.ZPos;
        }

        if (!UWReachRules.CanReach(UWTileQueries.WorldToEighths(lOFeet.x), UWTileQueries.WorldToEighths(lOFeet.z), UWUnits.WorldToZPos(lOFeet.y),
            liTargetX, liTargetY, liTargetZ, UWReachRules.GetDistanceSquared(false, lbTelekinesis), false))
        {
            AddGeneralMessage(CannotReachString);

            return;
        }

        fCastTargetOn(lOTarget.ObjectData, lOTarget, false);
    }

    /// <summary>
    /// mous_in_inv's case 2 (uw2 ui/INTERACT.C): the waiting spell on a thing in the inventory, clicked on the classic page
    /// or in the modern bags - no reach to test. A creature spell finds no creature there and ends unpaid; the thing spells
    /// work on it as in the world (UWObjectSpellUw2, carried). False when no target spell waits.
    /// </summary>
    public bool ApplyTargetToItem(UWObject pOItem)
    {
        if (!IsTargetSpellWaiting || pOItem == null)
            return false;

        fCastTargetOn(pOItem, null, true);

        return true;
    }

    /// <summary>target_spells (minors 0 to 7) or obj_spells (8 to 15) on the thing; the mode ends either way, the mana is
    /// paid when the handler took.</summary>
    private void fCastTargetOn(UWObject pOTarget, UWEntityInfo pOEntity, bool pbCarried)
    {
        int liMinor = miTargetMinor;
        int liCost = miTargetCost;
        IUWSpellHost lIHost = mITargetHost;

        miSpellClickFrame = Time.frameCount;
        CancelSpellTarget();

        bool lbTook;

        if (liMinor == UWObjectSpellRulesUw2.StudyMonster)
            lbTook = fStudyMonster(pOTarget, lIHost);
        else if (liMinor < UWTargetSpellRulesUw2.LastCreatureSpell)
            lbTook = UWTargetSpellRulesUw2.Cast(liMinor, pOTarget, lIHost);
        else
            lbTook = UWObjectSpellUw2.Cast(liMinor, pOTarget, pOEntity, pbCarried, this, lIHost);

        if (lbTook && liCost > 0 && lIHost != null)
            lIHost.SpendMana(liCost);
    }

    /// <summary>sp_study_monster: its lines on a creature, a skull or a wisp (UWObjectSpellRulesUw2.StudyLines); anything
    /// else and it does not take.</summary>
    private bool fStudyMonster(UWObject pOTarget, IUWSpellHost pIHost)
    {
        int liHitPoints = pIHost != null && pIHost.World != null && UWObjectIds.Major(pOTarget.ID) == UWObjectIds.CreatureMajorClass
            ? pIHost.World.CreatureHitPoints(pOTarget) : 0;
        System.Collections.Generic.List<string> lOLines = UWObjectSpellRulesUw2.StudyLines(GameData, pOTarget, liHitPoints);

        if (lOLines == null)
            return false;

        foreach (string lsLine in lOLines)
            AddMessage(lsLine);

        return true;
    }

    /// <summary>The ray the waiting spell's click goes along: the classic pointer in the view, the modern free pointer, or
    /// the crosshair in the look.</summary>
    private Ray fSpellRay()
    {
        Camera lOCamera = MeleeCamera;

        if (lOCamera == null)
            return InteractionRay();

        if (!UWGameScreenUw2.IsModernScheme || (!UWPointer.IsLookLocked && UWPointer.HasMouse))
            return lOCamera.ScreenPointToRay(UWPointer.Position);

        return InteractionRay();
    }

    /// <summary>
    /// do_spell's class 5 for the player (uw2 SPELLS.C:113): the missile waits for the click that aims it, its mana
    /// held until it is released (mspell_mused). The classic scheme's right button in the view releases it
    /// (mous_in_3d's GameInputMode 3, BlastFunction), the modern one's attack button under the free pointer or
    /// through the crosshair; with the pointer locked it flies at once along the crosshair, as on the first game
    /// (UWHudRunes.fAfterModernCast).
    /// </summary>
    public void BeginSpellMissile(int piProjectileId, int piBaseDamage, int piSpeedByte, int piManaCost)
    {
        mOSpell.BeginProjectile(piProjectileId, piBaseDamage, piSpeedByte, piManaCost);
        miSpellBeganFrame = Time.frameCount;

        if (UWGameScreenUw2.IsModernScheme && UWPointer.IsLookLocked)
            FireSpellMissile();
    }

    /// <summary>Escape in the modern scheme lets a waiting missile go, its mana unpaid.</summary>
    public void CancelSpellMissile()
    {
        mOSpell.Cancel();
    }

    /// <summary>
    /// release_missile for the player (uw2 SPELLS.C:246, MISSILE.C spell_fire): the missile leaves along the aim
    /// (player_settr, AimMissile), and only then is the mana paid; no room gives string 0x10F and costs nothing.
    /// Either way the spell no longer waits.
    /// </summary>
    public void FireSpellMissile()
    {
        if (!IsSpellWaiting)
            return;

        miSpellClickFrame = Time.frameCount;

        int liProjectile = mOSpell.ProjectileId;
        int liSpeed = mOSpell.ProjectileSpeedByte;
        int liBase = mOSpell.ProjectileBaseDamage;
        int liCost = mOSpell.TakeManaCost();
        UWLevelLoader lOLoader = LevelLoader;
        DataImport lOData = GameData;

        mOSpell.Cancel();

        if (lOLoader == null || lOData == null)
            return;

        AimMissile(liSpeed, out int liHeading, out int liPitch);

        int liSound = UWGameSession.Current != null ? UWGameSession.Current.Game.Combat.SpellMissileLaunchSound(liProjectile) : SpellLaunchSound;

        UWProjectileFlight lOFlight = UWMissiles.Ensure(lOLoader).LaunchFromPlayer(null, liProjectile, liHeading, liPitch, liSpeed,
            liBase, UWRunicMagic.GetProjectileDamageType(liProjectile, lOData.ObjectProperties), UWRunicMagic.GetProjectileImpactId(liProjectile),
            this, 0x3F, liSound);

        if (lOFlight == null)
        {
            AddGeneralMessage(NoRoomForSpellString);

            return;
        }

        if (liCost > 0 && MeleeVitals != null)
            MeleeVitals.SpendMana(liCost);
    }

    /// <summary>The click that releases a waiting missile spell (BeginSpellMissile).</summary>
    private void fUpdateSpellAim(UWGameScreenUw2 pOScreen)
    {
        if (!IsAnySpellWaiting || fBusy || miSpellBeganFrame == Time.frameCount)
            return;

        if (IsTargetSpellWaiting)
        {
            fUpdateTargetClick(pOScreen);

            return;
        }

        if (UWGameScreenUw2.IsModernScheme)
        {
            // THE LEFT BUTTON UNDER THE FREE POINTER, as on the first game (UWModernUiScheme.TryGetSpellTargetPoint;
            // per user 2026-11-12: cast from the hollow with the runes open, the click did not release it - the
            // runes and the bags hold the world's attack button) - or the attack button in the look.
            bool lbFree = !UWPointer.IsLookLocked && UWPointer.HasMouse;
            bool lbPressed = lbFree ? UWMouseButtons.LeftPressed : fModernAttack != null && UWMouseButtons.PressedFree(fModernAttack);

            // On a window the window has the click: the rune page and the hollow, the bags, the bar.
            if (!lbPressed || (lbFree && pOScreen.IsOverModernWindow(UWPointer.Position)))
                return;

            FireSpellMissile();

            return;
        }

        Mouse lOMouse = UWMouseButtons.GameMouse;

        if (lOMouse != null && lOMouse.rightButton.wasPressedThisFrame && pOScreen.IsInView(UWPointer.Position))
            FireSpellMissile();
    }

    /// <summary>The click a waiting target spell takes: the classic right button in the view, the modern left button
    /// under the free pointer (not on a window) or the attack button through the crosshair.</summary>
    private void fUpdateTargetClick(UWGameScreenUw2 pOScreen)
    {
        if (UWGameScreenUw2.IsModernScheme)
        {
            bool lbFree = !UWPointer.IsLookLocked && UWPointer.HasMouse;
            bool lbPressed = lbFree ? UWMouseButtons.LeftPressed : fModernAttack != null && UWMouseButtons.PressedFree(fModernAttack);

            if (!lbPressed || (lbFree && pOScreen.IsOverModernWindow(UWPointer.Position)))
                return;

            fApplyTargetAt(fSpellRay());

            return;
        }

        Mouse lOMouse = UWMouseButtons.GameMouse;

        if (lOMouse != null && lOMouse.rightButton.wasPressedThisFrame && pOScreen.IsInView(UWPointer.Position))
            fApplyTargetAt(fSpellRay());
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

    private static bool fBusy => UWConversationScreenUw2.IsOpen || UWMapScreenUw2.IsOpen || UWGameScreenUw2.IsOptionsOpen || UWGameScreenUw2.IsAskingCount
        || UWRoamingSight.IsAnyRunning || UWVitalsUw2.PlayerParalysed;

    /// <summary>player_attack, over the step flags of the weapon's animation: the lead-in gives way when the button
    /// comes up, the hold charges while it stays down, and the strike's step lands the blow.</summary>
    private void Update()
    {
        UWGameScreenUw2 lOScreen = UWGameScreenUw2.Current;
        UWVitalsUw2 lOVitals = UWVitalsUw2.Current;

        if (lOScreen == null || lOVitals == null || lOVitals.Vitals == null)
            return;

        fUpdateSpellAim(lOScreen);

        if (fWentDown())
            mbAimed = !SpellTookClick && fCanAim(lOScreen) && fPressed(lOScreen);

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
        return !fBusy && !pOScreen.IsPanelOpen && pOScreen.Inventory != null && pOScreen.Inventory.CursorItem == null;
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
