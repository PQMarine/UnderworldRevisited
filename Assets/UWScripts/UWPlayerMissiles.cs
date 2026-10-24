using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE PLAYER'S RANGED COMBAT (cleanup step 5.14, 2026-10-22: out of Interaction, which keeps the
/// public surface and forwards): bow, crossbow and sling - which ammunition a launcher takes, the
/// ammunition found in the pack and the bags, the shot on the release with its aim, room test,
/// twang and spent piece - and the base damage of a missile thrown by hand.
/// UWPlayerAttackInput asks HoldsLauncher on the release and TryShoot before a blow.
/// </summary>
public sealed class UWPlayerMissiles
{
    private readonly Interaction mOInteraction;

    public UWPlayerMissiles(Interaction pOInteraction)
    {
        mOInteraction = pOInteraction;
    }

    private UWDataImport.DataImport fData()
    {
        return mOInteraction.GameData;
    }

    private UWLevelLoader fLoader()
    {
        return mOInteraction.LevelLoader;
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

    /// <summary>First ammunition object - sling stone, bolt, arrow. The weapon names
    /// its ammunition type as a number counted from here.</summary>
    private const int FirstAmmunitionObjectId = UWObjectMechanics.FirstAmmunitionObjectId;

    /// <summary>Weapon type of physical projectiles. Whatever carries it is ammunition and
    /// not a weapon.</summary>
    private const int AmmunitionWeaponType = 0xC0;

    /// <summary>
    /// The message when out of ammunition.
    ///
    /// IT IS NOT IN STRINGS.PAK, BUT IN UW.EXE. The reference names
    /// string block 1, number 207 for it - for us that holds "You flip the switch.", and the whole
    /// block has no such sentence. I therefore searched the game binary itself
    /// and found the wording there (2026-09-10): "Sorry, you have no " is stored as raw
    /// text in the executable, the item name is appended at runtime.
    ///
    /// The user checked both in the original: "Sorry, you have no arrows." with the
    /// bow, "Sorry, you have no sling stones." with the sling. The full stop at the end
    /// comes from there.
    /// </summary>
    private const string NoAmmunitionMessage = "Sorry, you have no ";

    /// <summary>
    /// The shot with bow, crossbow or sling.
    ///
    /// WHICH AMMUNITION belongs to it is told by the weapon itself: its weapon type in the
    /// projectile table is the ammunition number, counted from the sling stone. The
    /// sling names 0, the crossbow 1, the bow 2 - so sling stone, bolt, arrow.
    /// Creatures use the same mapping for their thrown projectiles, and the loot of the
    /// sling goblins confirms it (see UWCritter).
    ///
    /// THE DAMAGE runs through the same chain as any other hit, with one addition
    /// that only exists here: the MISSILE skill stretches or squeezes it before
    /// the roll (reference: combat.MissileImpact). It enters as (skill times eight
    /// plus 192) divided by 256 - so at skill 0 three quarters remain, at 8 it becomes
    /// one and a half times. A check against ten subtracts another half from that or
    /// adds three quarters.
    ///
    /// Returns false if no shot happens at all - then the caller strikes as
    /// before.
    /// </summary>
    public bool TryShoot()
    {
        if (fInventory() == null || fData() == null
            || fData().ObjectProperties == null)
            return false;

        UWObject lOWeapon = fInventory().GetEquipped(fInventory().MainHandSlot);

        if (lOWeapon == null)
            return false;

        int liAmmunitionId;

        if (!TryGetAmmunitionId(lOWeapon.ID, out liAmmunitionId))
            return false;

        UWObject lOAmmunition = fFindAmmunition(liAmmunitionId);

        if (lOAmmunition == null)
        {
            // No shot, but no strike either: nobody strikes with a bow
            // in hand.
            mOInteraction.AddMessage(NoAmmunitionMessage + fGetPluralName(liAmmunitionId) + ".");

            return true;
        }

        // MissileRelease_seg025_9F: the ammunition is taken only when the missile could be
        // placed; blocked, it stays in the pack.
        if (fLaunchPlayerMissile(liAmmunitionId))
            fInventory().TryConsumeOne(lOAmmunition);

        // The twang: bow and crossbow only, never the sling - and also when the shot failed
        // for lack of room (both paths of MissileRelease_seg025_9F end in it).
        if (lOWeapon.ID == BowObjectId || lOWeapon.ID == CrossbowObjectId)
            UWSoundEffects.PlayAtAvatar(UWSoundEffects.BowTwang);

        return true;
    }

    /// <summary>
    /// THE AMMUNITION ASKED AT THE START OF THE SHOT (GetPlayerWeapon -> check_ammo, UnderworldExhumed
    /// COMBAT.C, read 2026-10-22): a launcher without its ammunition prints the message at once and
    /// the attack does not begin - no wind-up, no aim (per user on the original: "the charge does
    /// not start there"). Until then ours wound up and printed it on the release. True when the
    /// thing in hand is no launcher or its ammunition is there.
    /// </summary>
    public bool CheckAmmunitionToBegin()
    {
        if (fInventory() == null || fData() == null || fData().ObjectProperties == null)
            return true;

        UWObject lOWeapon = fInventory().GetEquipped(fInventory().MainHandSlot);

        if (lOWeapon == null || !TryGetAmmunitionId(lOWeapon.ID, out int liAmmunitionId))
            return true;

        if (fFindAmmunition(liAmmunitionId) != null)
            return true;

        mOInteraction.AddMessage(NoAmmunitionMessage + fGetPluralName(liAmmunitionId) + ".");

        return false;
    }

    /// <summary>The two launchers that twang (MissileRelease_seg025_9F: weapon index 9 and 10
    /// in the ranged table, which starts at 0x10).</summary>
    private const int BowObjectId = 0x19;
    private const int CrossbowObjectId = 0x1A;

    /// <summary>"You need more space to fire that weapon." (block 1).</summary>
    private const int NoRoomForWeaponMessage = 255;

    /// <summary>Whether a bow, crossbow or sling is in the main hand.</summary>
    public bool HoldsLauncher()
    {
        if (fInventory() == null || fData() == null || fData().ObjectProperties == null)
            return false;

        UWObject lOWeapon = fInventory().GetEquipped(fInventory().MainHandSlot);

        return lOWeapon != null && TryGetAmmunitionId(lOWeapon.ID, out _);
    }

    /// <summary>
    /// Which ammunition belongs to this weapon. Returns false if it is not a
    /// ranged weapon at all.
    ///
    /// The projectile table covers objects 0x10 to 0x1F. Everything in it that is not
    /// ammunition itself (weapon type 0xC0) and names a known ammunition type is a
    /// ranged weapon.
    /// </summary>
    public bool TryGetAmmunitionId(int piWeaponId, out int piAmmunitionId)
    {
        piAmmunitionId = -1;

        if (piWeaponId < FirstAmmunitionObjectId || piWeaponId > FirstAmmunitionObjectId + 0xF)
            return false;

        int liType = fData().ObjectProperties.GetRangedType(piWeaponId);

        if (liType == AmmunitionWeaponType || liType > 0xF)
            return false;

        piAmmunitionId = FirstAmmunitionObjectId + liType;

        return true;
    }

    /// <summary>
    /// The name of the ammunition in plural, without article - "arrows", "sling stones".
    ///
    /// The original simply appends an s to the bare name instead of taking the plural form from
    /// the string block (reference: GetSimpleObjectNameUW plus "s"). For the three
    /// ammunition types the result is the same.
    /// </summary>
    private string fGetPluralName(int piObjectId)
    {
        return UWText.ObjectName(fData(), piObjectId) + "s";
    }

    /// <summary>Matching ammunition anywhere on the player, bags included, in the original's
    /// order (CheckForAmmo_seg022_E78) - see UWInventoryModel.FindCarried.</summary>
    private UWObject fFindAmmunition(int piAmmunitionId)
    {
        return fInventory().Model.FindCarried(lOItem => lOItem.ID == piAmmunitionId);
    }

    /// <summary>
    /// Fires one piece of ammunition. False when there is no room for it in front of the player -
    /// then "You need more space to fire that weapon." (MissileRelease_seg025_9F), which in the
    /// original comes all the time with a creature close in front (per user, 2026-09-24). The
    /// arrow is a mobile record on the motion core since 2026-10-05 (UWMissiles): it
    /// starts radii + 4 eighths ahead, falls, bounces and lies where it comes to rest.
    /// </summary>
    private bool fLaunchPlayerMissile(int piAmmunitionId)
    {
        if (fLoader() == null || Camera.main == null)
            return false;

        int liSpeedByte = fData().ObjectProperties.GetRangedSpeed(piAmmunitionId);
        int liHeading;
        int liPitch;

        // The original's aim from the pointer in the view window; the modern scheme shoots where
        // the free pointer stands on the world, else along the crosshair (per user, 2026-10-04).
        if (UWControlScheme.Ui.Policy.AimSource == UWAimSource.PointerInView && fUi() != null)
        {
            UWPlayerThrow.GetPointerInView(fUi(), Mouse.current.position.ReadValue(), out int liX, out int liY);
            UWPlayerThrow.GetMissileAim(liX, liY, Camera.main.transform, out liHeading, out liPitch);
        }
        else
            UWMissiles.AimFromDirection(mOInteraction.ModernAimRay().direction, liSpeedByte, out liHeading, out liPitch);

        // A missile hit always counts with charge 0x80, however far the bow was drawn
        // (reference: combat.MissileAttackHit, "PlayerAttackCharge = 0x80"). The arrow's life
        // is the ammunition's quality (MissileRelease copies it from the piece). The flight carries
        // the base; its dice are rolled at the hit (since cleanup step 4.9b, 2026-10-18).
        UWObject lOAmmunition = fFindAmmunition(piAmmunitionId);

        UWProjectileFlight lOFlight = UWMissiles.Ensure(fLoader()).LaunchFromPlayer(null, piAmmunitionId,
            liHeading, liPitch, liSpeedByte, fGetMissileBaseDamage(piAmmunitionId),
            UWDamageTypes.Missile, -1, mOInteraction, lOAmmunition != null ? lOAmmunition.Quality : 0x3F);

        if (lOFlight == null)
        {
            mOInteraction.AddGeneralMessage(NoRoomForWeaponMessage);

            return false;
        }

        return true;
    }

    /// <summary>The base damage of a fired projectile: the Missile skill stretches or squeezes it -
    /// see UWPlayerAttack.GetMissileBaseDamage (the dice at the hit since cleanup step 4.9b,
    /// 2026-10-18).</summary>
    private int fGetMissileBaseDamage(int piAmmunitionId)
    {
        return UWPlayerAttack.GetMissileBaseDamage(fData(), piAmmunitionId,
            fCharacter() != null ? fCharacter().Vitals.GetSkill(UWPlayerData.Skill.Missile) : 0);
    }

    /// <summary>
    /// The base damage of a missile the player throws BY HAND - see UWPlayerThrow.
    ///
    /// Charge 0x80 at the hit, like every missile hit (missile_thwack), where the dice are rolled
    /// since cleanup step 4.9b (2026-10-18). The Missile skill scales only ammunition
    /// (UWPlayerAttack.GetThrownMissileBaseDamage, 2026-10-12).
    /// </summary>
    public int GetThrownMissileBaseDamage(int piObjectId)
    {
        if (fData() == null || fData().ObjectProperties == null)
            return UWAttackResolution.NoMissileDamage;

        return UWPlayerAttack.GetThrownMissileBaseDamage(fData(), piObjectId,
            fCharacter() != null ? fCharacter().Vitals.GetSkill(UWPlayerData.Skill.Missile) : 0);
    }

    /// <summary>The damage type of a class 1 object the player throws: -ammo of its missile
    /// table row (UWPlayerAttack.GetMissileDamageType) - 0x40 for ammunition.</summary>
    public int GetThrownMissileDamageType(int piObjectId)
    {
        return UWPlayerAttack.GetMissileDamageType(fData(), piObjectId);
    }
}
