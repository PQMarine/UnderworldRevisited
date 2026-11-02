using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE PLAYER'S CAST ON THE SECOND GAME (UW2 plan phase 4, list item 3): what the spell rules ask of the caster,
/// from UWVitalsUw2 and the walk, where UWCharacter and Interaction stand idle. Built per cast by the game screen.
/// The spells that wait for a click, and those that move the world, come with their classes.
/// </summary>
public sealed class UWSpellHostUw2 : IUWSpellHost
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

    public void BeginRoamingSightWithoutSpell()
    {
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

    public void BeginProjectileSpell(int piProjectileId, int piDamage, int piSpeed, int piManaCost)
    {
    }

    public void BeginTargetSpell(int piMajorClass, int piMinorClass)
    {
    }

    public void FirePendingSpell()
    {
    }

    public void Armageddon()
    {
    }

    public void FireBullfrog(int piMode)
    {
    }

    public bool FireLookTrigger(UWObject pOObject, int piSearchSkill)
    {
        return false;
    }
}
