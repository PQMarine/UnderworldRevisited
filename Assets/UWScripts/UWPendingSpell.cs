using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// A SPELL WAITING FOR ITS TARGET, the Unity side (cleanup step 5.14, 2026-10-22: out of
/// Interaction, which keeps the public surface and forwards): the click that gives a projectile
/// its direction or a target spell its object, the aim and the reach, the launch, the cast on an
/// item clicked in the inventory (B17's one rule, UWPendingSpellState.InventoryPolicy), the target
/// pointer, and Escape letting the spell go. The state itself is the engine-free
/// UWPendingSpellState. Interaction ticks UpdateTargeting in its Update's order (after the use
/// mode, before the compass).
/// </summary>
public sealed class UWPendingSpell
{
    private readonly Interaction mOInteraction;

    private readonly UWPendingSpellState mOState = new UWPendingSpellState();

    public UWPendingSpell(Interaction pOInteraction)
    {
        mOInteraction = pOInteraction;
    }

    /// <summary>Message when the projectile has nowhere to go. String block 1.</summary>
    private const int NoRoomForSpellMessage = 256;

    /// <summary>Is a spell currently running and waiting for its target? This applies to both
    /// kinds: a projectile waits for the DIRECTION, a class 7 spell for a
    /// clicked OBJECT.</summary>
    public bool IsTargeting => mOState.IsTargeting;

    /// <summary>Is a spell waiting for an object instead of a direction? The cursor is
    /// a different one then - see Cursor.</summary>
    public bool IsTargetSpellPending => mOState.IsTargetSpellPending;

    /// <summary>The original's pointer while a spell waits: the target pointer for an object, the
    /// aiming pointer for a direction (UWGameUI and UWModernHud asked IsTargetSpellPending
    /// each for themselves until 2026-10-22). A drawn launcher shows the aiming one too, which
    /// is the callers' business.</summary>
    public int Cursor => mOState.IsTargetSpellPending ? UWHudRunes.TargetSpellCursor : UWHudRunes.SpellTargetCursor;

    /// <summary>Sends a waiting projectile spell off immediately, in the direction the
    /// pointer is currently pointing. For wands: the click that applied the wand
    /// already gives the direction.</summary>
    public void Fire()
    {
        if (!IsTargeting)
            return;

        fLaunchSpellProjectile();

        mOState.ForgetProjectile();
    }

    /// <summary>
    /// Sends the queued projectile off from a trap's spot - the a_spelltrap, which has no click
    /// that could give it the direction (see UWTriggerSystem.FireSpellTrap). spell_fire for an
    /// object caster (UnderworldExhumed MISSILE.C): from the map square the trigger names, at
    /// the trap's fine position and height, with missile_arc and missile_try 0 - flat, along
    /// the heading given (OBJ_HEADING(trap) &lt;&lt; 5); a launcher of height 0, so no offset
    /// and no placement test. Until 2026-10-12 the caller aimed it at the camera.
    /// </summary>
    public bool LaunchFromSpot(int piTileX, int piTileY, int piXPos, int piYPos, int piZPos, int piHeading)
    {
        UWLevelLoader lOLoader = mOInteraction.LevelLoader;

        if (mOState.ProjectileId < 0 || lOLoader == null)
            return false;

        int liProjectile = mOState.ProjectileId;

        mOState.ForgetProjectile();

        return UWMissiles.Ensure(lOLoader).LaunchFromSpot(liProjectile, piTileX, piTileY, piXPos, piYPos,
            piZPos, piHeading, 0, mOState.ProjectileSpeedByte, mOState.ProjectileBaseDamage,
            UWRunicMagic.GetProjectileDamageType(liProjectile, fObjectProperties), UWRunicMagic.GetProjectileImpactId(liProjectile)) != null;
    }

    /// <summary>The OBJECTS.DAT tables, for a spell projectile's damage type.</summary>
    private UWObjectProperties fObjectProperties => mOInteraction.GameData != null ? mOInteraction.GameData.ObjectProperties : null;

    /// <summary>The modern scheme's aim after a cast from runes or an item (UWHudRunes.fAfterModernCast,
    /// UWCarriedItemUse): a projectile with the pointer locked flies at once towards the
    /// crosshair; free, or a target spell, waits for the left button. Where the scheme's cast waits
    /// for the click that aims it (the classic one, UWUiSchemePolicy), nothing happens here.</summary>
    public void AfterModernCast()
    {
        if (!UWControlScheme.Ui.Policy.ProjectileFliesWhenPointerLocked || !IsTargeting)
            return;

        if (IsTargetSpellPending || UWControlScheme.Ui.IsPointerUnlocked)
            mOInteraction.AddMessage("Choose the target with the left button.");
        else
            Fire();
    }

    /// <summary>Escape in the modern scheme: a waiting spell is let go (a projectile's mana was not
    /// paid yet, a target spell's was).</summary>
    public void Cancel()
    {
        mOState.Cancel();
    }

    /// <summary>Discards a queued projectile without throwing it - for a spell trap
    /// whose projectile could not take off. Otherwise the player would be left with it as an aiming cursor.
    /// </summary>
    public void CancelProjectile()
    {
        mOState.ForgetProjectile();
    }

    /// <summary>
    /// Remembers the spell until the player clicks the direction.
    ///
    /// THE TABLE VALUE IS THE BASE VALUE FOR THE ROLL, not the damage - for the fireball
    /// twenty-eight, so fifteen on average. The reference sends a projectile hit
    /// through the same calculation as a sword swing (combat.MissileImpact ->
    /// AttackerAppliesFinalDamage), and that starts with the roll. For creature
    /// projectiles this has been in place since 2026-09-10, here is the other half.
    ///
    /// WITH NEUTRAL CHARGE: for a creature it is its strike charge, for the player
    /// the reference takes the value of his power gem at this point - but that belongs to
    /// melee and, when casting, holds whatever the last swing left behind. That
    /// is evidently arbitrary; we calculate with the plain value.
    ///
    /// THE ROLL AT THE HIT since cleanup step 4.9b (2026-10-18, by the user's decision to follow
    /// the original: missile_thwack -> do_damage rolls it, UWAttackResolution.Missile, with charge
    /// 0x80): the table value is kept as the flight's base; it was rolled here until then.
    /// </summary>
    public void BeginProjectile(int piProjectileId, int piDamage, int piSpeed, int piManaCost)
    {
        mOState.BeginProjectile(piProjectileId, piDamage, piSpeed, piManaCost);
    }

    /// <summary>Remembers a spell until the player clicks its target. This exists in
    /// two major classes: 7 targets creatures (see UWTargetSpell), 11 items
    /// and doors (see UWMiscSpell).</summary>
    public void BeginTarget(int piMajorClass, int piMinorClass)
    {
        mOState.BeginTarget(piMajorClass, piMinorClass);
    }

    /// <summary>Message when the spell hits nothing: string block 1, our 272, "The spell has no
    /// discernable effect." Until 2026-09-14 this was 143, which is "You cannot repair that."
    /// (UW.EXE ItemRepair_ovr107_1754 prints it as 0x8E); a "Nothing happens." does not exist in
    /// block 1. 272 is what UW.EXE DoorAndTrapSpells_seg038_3307_1560 prints (0x10F) when a
    /// class 11 target spell does not take effect.</summary>
    private const int NothingHappensMessage = UWStringIds.SpellNoEffectMessage;

    /// <summary>
    /// The waiting target spell onto what lies under a screen point - the pointer, or in the
    /// modern scheme the crosshair while the pointer is locked.
    ///
    /// AS THE ORIGINAL'S TARGET MODE (mous_in_3d, GIM_TARGET, UnderworldExhumed INTERACT.C, read
    /// 2026-10-12): a click on no object does nothing and the spell keeps waiting; an object out
    /// of reach - the use reach, InPickRange with PickDist, which Telekinesis lifts - gives "You
    /// cannot reach that." and the spell keeps waiting; only an object in reach takes the spell
    /// (obj_spells), which then prints its own outcome. Until 2026-10-12 every click used the
    /// spell up, from as far as one could look, with "The spell has no discernable effect." for
    /// a miss. The original's BlockingTerrain test is not built here, as on the use path.
    /// </summary>
    private void fApplyTargetSpell(Vector2 pOAt)
    {
        // The pickable thing first - behind a blood pool if need be -, else whatever the look
        // hits: a creature, a door (UWWorldPicker.Measurement.SpellTarget).
        UWEntityInfo lOTarget = Camera.main != null
            ? UWWorldPicker.Measure(Camera.main.ScreenPointToRay(pOAt)).SpellTarget
            : null;

        if (lOTarget == null)
            return;

        if (!mOInteraction.CanReach(lOTarget, UWReachRules.GetDistanceSquared(false, mOInteraction.HasTelekinesis), false))
        {
            mOInteraction.AddGeneralMessage(UWMiscSpellRules.TargetOutOfReachMessage);

            return;
        }

        mOState.TakeTarget(out int liSpell, out int liMajor);

        if (liMajor == UWMiscSpell.MajorClass)
            UWMiscSpell.CastOnTarget(liSpell, lOTarget, mOInteraction, mOInteraction.LevelLoader);
        else if (UWTargetSpell.Cast(liSpell, lOTarget) == UWTargetSpellRules.Result.Missed)
            mOInteraction.AddGeneralMessage(NothingHappensMessage);
    }

    /// <summary>
    /// A click into the inventory while a target spell waits - B17's one rule
    /// (UWPendingSpellState.DecideInventoryClick) with the policy of the inventory that was
    /// clicked: the classic one (UWItemDrag) or the modern bags (UWModernBags). Until 2026-10-22
    /// each had its own path; both behaviours stay, as decided per user the same day.
    ///
    /// A TARGET SPELL ALSO WORKS ON THE BACKPACK (per user, 2026-09-10): Name Enchantment
    /// identifies an item you carry - you do not have to drop it first.
    ///
    /// THE SPELL IS GONE AFTERWARDS IN ANY CASE, even if the click hit nothing valid
    /// - that is how the original behaves with both mouse buttons (per user,
    /// 2026-09-10). Only in the view window does it keep waiting, because there the left button
    /// drives movement. (The classic policy; the modern bags keep it waiting on no item.)
    ///
    /// IF IT MISSES, NOTHING IS SHOWN - no "The spell has no discernable effect." as when missing in
    /// the world (per user, 2026-09-10). What you read on a hit is the
    /// description the spell itself outputs (see UWLoreCheck and IdentifyItem) -
    /// with both buttons, including the left one, which otherwise does not look in the inventory.
    /// ONCE, not twice: the normal look is not added on top (checked in the original,
    /// per user, 2026-09-10).
    ///
    /// This is called from UWItemDrag, not from here: there the click afterwards
    /// still has its usual effect, and the original does both - the container closes
    /// AND the spell is used up.
    /// </summary>
    public UWPendingSpellState.InventoryClickOutcome ResolveInInventory(UWObject pOItem, UWPendingSpellState.InventoryPolicy pePolicy)
    {
        UWPendingSpellState.InventoryClickOutcome leOutcome = mOState.ResolveInventoryClick(pePolicy, pOItem != null,
            out int liSpell, out int liMajor);

        if (leOutcome != UWPendingSpellState.InventoryClickOutcome.CastOnItem || liMajor != UWMiscSpell.MajorClass)
            return leOutcome;

        UWMiscSpell.CastOnItem(liSpell, pOItem, mOInteraction);

        return leOutcome;
    }

    /// <summary>Intercepts the click that gives the direction. Returns true as long as the
    /// spell claims the click for itself.</summary>
    public bool UpdateTargeting()
    {
        if (!IsTargeting)
            return false;

        bool lbRightButton = mOInteraction.Input.Interact.WasPressedThisFrame();

        if (!lbRightButton && !UWMouseButtons.LeftPressed)
            return true;

        // THE CLICK THAT AIMS is the scheme's question (IUWUiScheme.TryGetSpellTargetPoint since
        // cleanup 5.19, the two rules moved there as they were): the classic scheme's right button
        // in the view window - with the left one you keep walking, and the waiting spell keeps
        // waiting -, the modern scheme's left button under the free pointer or through the
        // crosshair (per user, 2026-10-04), not on a window of its UI.
        if (!UWControlScheme.Ui.TryGetSpellTargetPoint(lbRightButton, UWMouseButtons.LeftPressed, out Vector2 lOAt))
            return true;

        if (mOState.IsTargetSpellPending)
        {
            fApplyTargetSpell(lOAt);

            return true;
        }

        // A projectile flies towards the click (UWHudRunes.fAfterModernCast: it waits only
        // when cast with the pointer free); the classic scheme aims from the pointer in the view
        // window and leaves the ray unused (fLaunchSpellProjectile).
        fLaunchSpellProjectile(Camera.main != null ? Camera.main.ScreenPointToRay(lOAt) : (Ray?)null);

        mOState.ForgetProjectile();

        return true;
    }

    /// <summary>Sends the waiting projectile off - along pOAim when given (the modern scheme's
    /// click under the free pointer), else along the interaction ray. The original's aim from
    /// the pointer in the view window (InitPlayerProjectilesValues_seg025_D) in the original
    /// scheme; the start radii + 4 eighths ahead of the feet, five sixths of the height up - and
    /// no room there means no spell (per user, 2026-09-24: in a narrow corridor one has to aim
    /// nearer the middle in the original). On the motion core since 2026-10-05.</summary>
    private void fLaunchSpellProjectile(Ray? pOAim = null)
    {
        UWLevelLoader lOLoader = mOInteraction.LevelLoader;
        UWGameUI lOUi = mOInteraction.GameUi;

        if (lOLoader == null || Camera.main == null)
            return;

        int liHeading;
        int liPitch;

        if (UWControlScheme.Ui.Policy.AimSource == UWAimSource.PointerInView && lOUi != null)
        {
            UWPlayerThrow.GetPointerInView(lOUi, Mouse.current.position.ReadValue(), out int liX, out int liY);
            UWPlayerThrow.GetMissileAim(liX, liY, Camera.main.transform, out liHeading, out liPitch);
        }
        else
        {
            Ray lORay = pOAim ?? mOInteraction.InteractionRay();

            UWMissiles.AimFromDirection(lORay.direction, mOState.ProjectileSpeedByte, out liHeading, out liPitch);
        }

        int liProjectile = mOState.ProjectileId;

        UWProjectileFlight lOFlight = UWMissiles.Ensure(lOLoader).LaunchFromPlayer(null, liProjectile,
            liHeading, liPitch, mOState.ProjectileSpeedByte, mOState.ProjectileBaseDamage,
            UWRunicMagic.GetProjectileDamageType(liProjectile, fObjectProperties), UWRunicMagic.GetProjectileImpactId(liProjectile),
            mOInteraction, 0x3F);

        if (lOFlight == null)
        {
            mOInteraction.AddGeneralMessage(NoRoomForSpellMessage);

            return;
        }

        // Released: now the rune spell's mana is paid (see UWPendingSpellState.ManaCost). The
        // mana a rune-cast projectile costs, paid only when it is released - the
        // original keeps it in SpellManaCost and subtracts it after PrepareProjectileObject
        // succeeded (Class5ProjectileSpells_seg038_4EE); when there is no room nothing is paid.
        // Zero for a wand or a trap.
        int liManaCost = mOState.TakeManaCost();
        UWCharacter lOCharacter = mOInteraction.Character;

        if (liManaCost > 0 && lOCharacter != null)
            lOCharacter.Vitals.SpendMana(liManaCost);
    }
}
