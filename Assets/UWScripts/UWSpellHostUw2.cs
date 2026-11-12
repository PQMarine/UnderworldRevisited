using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE PLAYER'S CAST ON THE SECOND GAME (UW2 plan phase 4, list item 3): what the spell rules ask of the caster,
/// from UWVitalsUw2 and the walk, where UWCharacter and Interaction stand idle. Built per cast by the game screen.
/// The spells that wait for a click come with their classes; what the second game's own spells ask beyond the
/// shared host is IUWSpellHostUw2 (stage 2 part A).
/// </summary>
public sealed class UWSpellHostUw2 : IUWSpellHost, IUWSpellHostUw2
{
    private readonly UWVitalsUw2 mOPlayer;

    private readonly UWLevelLoader mOLoader;

    private readonly IUWWorld mOWorld;

    public UWSpellHostUw2(UWVitalsUw2 pOPlayer, UWLevelLoader pOLoader, IUWPresentation pOPresentation)
    {
        mOPlayer = pOPlayer;
        mOLoader = pOLoader;
        mOWorld = UWScopedWorld.With(UWWorld.Of(pOLoader), pOPresentation);
    }

    public IUWWorld World => mOWorld;

    public UWCastContext Cast => UWCastContext.ThePlayer;

    public DataImport Data => mOLoader != null ? mOLoader.UWDataImporter : null;

    /// <summary>anti_magic_p: the tile's bit alone, the second game has no void without magic (SPELLS.C:89).</summary>
    public bool IsMagicBlocked => mOLoader != null && mOLoader.CurrentLevel != null && UWViewpoint.Current != null
        && UWEndgameRules.IsAntiMagicTile(mOLoader.CurrentLevel, mOLoader.WorldPositionToTile(UWViewpoint.Current.transform.position));

    public bool HasPlayer => mOPlayer != null && mOPlayer.Vitals != null;

    public int PlayerLevel => mOPlayer.Vitals.Level;

    public int PlayerMana => mOPlayer.Vitals.CurrentMana;

    public int PlayerCastingSkill => mOPlayer.Vitals.GetSkill(UWPlayerData.Skill.Casting);

    public bool CanAct => !mOPlayer.IsParalysed;

    public void SpendMana(int piAmount)
    {
        mOPlayer.Vitals.SpendMana(piAmount);
    }

    public long GameTicks => UWGameSession.Current.Schedule.Now;

    public void RestoreManaFromSpell(int piMinorClass)
    {
        mOPlayer.Vitals.RestoreManaFromSpell(piMinorClass);
    }

    public void Heal(int piAmount)
    {
        mOPlayer.Vitals.Heal(piAmount);
    }

    public void HealFully()
    {
        mOPlayer.Vitals.HealFully();
    }

    public bool TryAddActiveSpell(int piMajorClass, int piMinorClass, int piStability)
    {
        return mOPlayer.Vitals.TryAddActiveSpell(piMajorClass, piMinorClass, piStability);
    }

    public void BouncePlayerUp()
    {
        if (UWScene.PlayerMovement != null)
            UWScene.PlayerMovement.SpellHop();
    }

    /// <summary>Roaming Sight with all slots taken: the view leaves all the same (UWExtraSpellRulesUw2).</summary>
    public void BeginRoamingSightWithoutSpell()
    {
        if (mOPlayer != null)
            mOPlayer.BeginRoamingSight(true);
    }

    public void CurePoison()
    {
        mOPlayer.Vitals.CurePoison();
    }

    public void StartHallucination()
    {
        mOPlayer.Vitals.StartHallucination();
    }

    public void PlaySpellSound()
    {
        mOWorld.Presentation.PlaySoundAtPlayer(UWSoundEffectId.Spell);
    }

    public void PlaySpellFailureSound()
    {
        mOWorld.Presentation.PlaySoundAtPlayer(UWSoundEffectId.SpellFailure);
    }

    public void RefreshSpellIcons()
    {
        mOPlayer.WriteToRecord();

        if (UWGameScreenUw2.Current != null)
            UWGameScreenUw2.Current.ShowRuneState();
    }

    /// <summary>The missile waits for its aim on the walk's attack host (UWAttackUw2.BeginSpellMissile).</summary>
    public void BeginProjectileSpell(int piProjectileId, int piDamage, int piSpeed, int piManaCost)
    {
        if (UWAttackUw2.Current != null)
            UWAttackUw2.Current.BeginSpellMissile(piProjectileId, piDamage, piSpeed, piManaCost);
    }

    /// <summary>nail_1area: the spell waits on the walk's attack host for its clicked target (UWAttackUw2.BeginSpellTarget),
    /// with this host to cast it and its mana held.</summary>
    public void BeginTargetSpell(int piMajorClass, int piMinorClass, int piManaCost = 0)
    {
        if (UWAttackUw2.Current != null)
            UWAttackUw2.Current.BeginSpellTarget(piMinorClass, piManaCost, this);
    }

    public void FirePendingSpell()
    {
        if (UWAttackUw2.Current != null)
            UWAttackUw2.Current.FireSpellMissile();
    }


    public void FireBullfrog(int piMode)
    {
    }

    public bool FireLookTrigger(UWObject pOObject, int piSearchSkill)
    {
        return false;
    }

    // ------------------------------------------------- IUWSpellHostUw2

    public UWPlayerFile Record => UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;

    public UWPlayerVitals Vitals => mOPlayer.Vitals;

    public bool PlayerFitsAt(UWTilePos pOTile)
    {
        return mOLoader != null && mOLoader.AdventurerFitsAt(pOTile.X, pOTile.Y);
    }

    public void MovePlayer(int piLevel, UWTilePos pOTile)
    {
        if (mOLoader != null)
            mOLoader.WalkTeleport(new UWTeleportUw2.Target { Level = piLevel, TileX = pOTile.X, TileY = pOTile.Y }, true);
    }

    /// <summary>FreePlayerInv and the weight (the model's ClearAll sets the running load to 0), clearobj (the
    /// level loader's wipe; ArmageddonActive reads the record's bit the rule sets), clear_shelf.</summary>
    public void Armageddon()
    {
        UWGameScreenUw2 lOScreen = UWGameScreenUw2.Current;

        if (lOScreen != null && lOScreen.Inventory != null)
            lOScreen.Inventory.ClearAll();

        if (lOScreen != null)
            lOScreen.ClearShelf();

        if (mOLoader != null)
            mOLoader.ApplyArmageddon();

        mOPlayer.RefreshStatus();
    }

    public void SwingView(int piSteps)
    {
        UWPlayerLook lOLook = UWScene.Player != null ? UWScene.Player.GetComponent<UWPlayerLook>() : null;

        if (lOLook != null)
            lOLook.Swing(piSteps);
    }
}
