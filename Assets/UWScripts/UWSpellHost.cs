using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The Unity side of a cast: what the engine-free spell rules (UWSpellCasting and the
/// class rules next to it) ask for through IUWSpellHost, read from UWCharacter, Interaction
/// and the HUD. Built per cast by UWHudRunes for the player. Until cleanup step 4.10
/// (2026-10-18) also by UWCritter for a creature's area spell or summoning, with the creature as
/// the cast's caster; a creature casts through UWCreatureSpellHost since.
///
/// The boulders of Tremor are not physics objects: World.DropBoulder hands them to the motion
/// core (UWMissiles.DropBoulder, since 2026-10-05), which holds their start height and sub-tile
/// and lets them fall as mobile records.
///
/// THE WORLD OF THE CAST (cleanup step 3.6, 2026-10-17): the level, the travel, the player's
/// tile and view, the messages, the sounds, the shake and the effect pictures went to the world
/// (UWWorld), its avatar and its presentation in step 3.1; since step 3.6 the rules ask the
/// world for them themselves, together with the objects a spell makes, Detect Monsters'
/// creatures and the targets of an area spell (collected by the world, held by the rule as a
/// list). World is the world with this cast's presentation - its lines go to the Interaction
/// the cast was given, silent without one. The creature casting an area spell is the cast's
/// value (UWCastContext) instead of the mutable AreaCaster. This host keeps the caster: his
/// numbers, the pending projectile and target, the active spells and the icons.
///
/// PER CAST: it carries the caster, the Interaction and the HUD of this cast; the long-lived
/// world keeps none of it.
/// </summary>
public sealed class UWSpellHost : IUWSpellHost
{
    private readonly UWCharacter mOCharacter;

    private readonly Interaction mOInteraction;

    private readonly UWLevelLoader mOLoader;

    private readonly UWDataImport.DataImport mOData;

    private readonly UWGameUI mOUi;

    /// <summary>The world with this cast's presentation; null without a loader.</summary>
    private readonly IUWWorld mOWorld;

    public UWSpellHost(UWCharacter pOCharacter, Interaction pOInteraction, UWLevelLoader pOLoader,
        UWDataImport.DataImport pOData, UWGameUI pOUi)
    {
        mOCharacter = pOCharacter;
        mOInteraction = pOInteraction;
        mOLoader = pOLoader;
        mOData = pOData;
        mOUi = pOUi;

        UWWorld lOWorld = UWWorld.Of(pOLoader);

        // This cast's presentation: its lines go to pOInteraction, or nowhere without one.
        mOWorld = UWScopedWorld.With(lOWorld, UWPresentation.For(lOWorld, pOInteraction));
    }

    public IUWWorld World => mOWorld;

    /// <summary>The player casts (a creature's cast is UWCreatureSpellHost's since cleanup step
    /// 4.10, 2026-10-18).</summary>
    public UWCastContext Cast => UWCastContext.ThePlayer;

    /// <summary>The data the caster was handed (UWGameUI's, the creature's loader's) - kept as
    /// given, not the world's.</summary>
    public UWDataImport.DataImport Data => mOData;

    public bool IsMagicBlocked => mOCharacter != null && mOLoader != null
        && UWEndgame.IsMagicBlockedAt(mOLoader, mOCharacter.transform.position);

    // ------------------------------------------------- The caster

    public bool HasPlayer => mOCharacter != null;

    public int PlayerLevel => mOCharacter != null ? mOCharacter.Vitals.Level : 0;

    /// <summary>Mana ROUNDED DOWN for the spell rules (the cost check), where the conversation host
    /// rounds to the nearest (IUWConversationHost.PlayerMana) - two conventions on purpose, kept
    /// apart by cleanup step 3.1 (2026-10-17).</summary>
    public int PlayerMana => mOCharacter != null ? Mathf.FloorToInt(mOCharacter.Vitals.CurrentMana) : 0;

    public int PlayerCastingSkill => mOCharacter != null ? mOCharacter.Vitals.GetSkill(UWPlayerData.Skill.Casting) : 0;

    public void SpendMana(int piAmount)
    {
        if (mOCharacter != null)
            mOCharacter.Vitals.SpendMana(piAmount);
    }

    public double RealSeconds => Time.timeAsDouble;

    public void RestoreManaFromSpell(int piMinorClass)
    {
        if (mOCharacter != null)
            mOCharacter.Vitals.RestoreManaFromSpell(piMinorClass);
    }

    public void Heal(int piAmount)
    {
        if (mOCharacter != null)
            mOCharacter.Vitals.Heal(piAmount);
    }

    public void HealFully()
    {
        if (mOCharacter != null)
            mOCharacter.Vitals.HealFully();
    }

    public bool TryAddActiveSpell(int piMajorClass, int piMinorClass, int piStability)
    {
        return mOCharacter != null && mOCharacter.Vitals.TryAddActiveSpell(piMajorClass, piMinorClass, piStability);
    }

    public void BeginRoamingSightWithoutSpell()
    {
        if (mOCharacter == null)
            return;

        UWRoamingSight lOSight = mOCharacter.GetComponent<UWRoamingSight>();

        if (lOSight == null)
            lOSight = mOCharacter.gameObject.AddComponent<UWRoamingSight>();

        // From the body, as UWCharacter.fApplyRoamingSight starts it (home_cam at the player).
        CharacterController lOBody = mOCharacter.GetComponentInParent<CharacterController>();
        Transform lOFrom = lOBody != null ? lOBody.transform : mOCharacter.transform;

        lOSight.Begin(lOFrom.position, lOFrom.eulerAngles.y, true);
    }

    public void BouncePlayerUp()
    {
    // UWCharacter sits on the Main Camera, a child of the First Person Controller that carries
    // UWPlayerMovement - GetComponent on the character found nothing (until 2026-10-13).
        UWPlayerMovement lOMovement = mOCharacter != null ? mOCharacter.GetComponentInParent<UWPlayerMovement>() : null;

        if (lOMovement != null)
            lOMovement.SpellHop();
    }

    public void CurePoison()
    {
        if (mOCharacter != null)
            mOCharacter.Vitals.CurePoison();
    }

    public void StartHallucination()
    {
        if (mOCharacter != null)
            mOCharacter.Vitals.StartHallucination();
    }

    // ------------------------------------------------- Sounds and icons

    public void PlaySpellSound()
    {
        fPresentation.PlaySoundAtPlayer(UWSoundEffects.Spell);
    }

    public void PlaySpellFailureSound()
    {
        fPresentation.PlaySoundAtPlayer(UWSoundEffects.SpellFailure);
    }

    /// <summary>This cast's presentation - the world's, or for a cast without a level loader one
    /// of its own, as before (the sounds never depended on the loader).</summary>
    private IUWPresentation fPresentation => mOWorld != null
        ? mOWorld.Presentation
        : UWPresentation.For(null, mOInteraction);

    public void RefreshSpellIcons()
    {
        if (mOUi != null)
            mOUi.Runes.RefreshSpellIcons();
    }

    // ------------------------------------------------- Pending spells

    public void BeginProjectileSpell(int piProjectileId, int piDamage, int piSpeed, int piManaCost)
    {
        if (mOInteraction != null)
            mOInteraction.BeginSpellTargeting(piProjectileId, piDamage, piSpeed, piManaCost);
    }

    public void BeginTargetSpell(int piMajorClass, int piMinorClass)
    {
        if (mOInteraction != null)
            mOInteraction.BeginTargetSpell(piMajorClass, piMinorClass);
    }

    public void FirePendingSpell()
    {
        if (mOInteraction != null)
            mOInteraction.FirePendingSpell();
    }

    // ------------------------------------------------- What goes through this cast's channel

    /// <summary>Armageddon: the inventory, the rune shelf and the whole level are cleared;
    /// without equipment, armour, light and movement abilities are no longer correct, so the
    /// status is refreshed. The rune bag in the player data is cleared by the rules.</summary>
    public void Armageddon()
    {
        if (mOLoader == null)
            return;

        UWInventory lOInventory = UWScene.Inventory;

        if (lOInventory != null)
            lOInventory.ClearAll();

        if (mOUi != null)
            mOUi.Runes.ClearRuneShelf();

        mOLoader.ApplyArmageddon();

        if (mOCharacter != null)
            mOCharacter.RefreshStatus();
    }

    public void FireBullfrog(int piMode)
    {
        UWTriggerSystem.FireBullfrog(piMode, mOLoader, mOInteraction);
    }

    public bool FireLookTrigger(UWObject pOObject, int piSearchSkill)
    {
        return UWTriggerSystem.TryFireLookTrigger(pOObject, mOLoader, mOInteraction, piSearchSkill);
    }
}
