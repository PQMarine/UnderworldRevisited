using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The Unity side of a conversation: what UWConversationSession and UWConversationTrade ask
/// for through IUWConversationHost, read from the components of the scene - UWCharacter for
/// the player, UWLevelLoader for the world, UWInventory for the backpack, Interaction for
/// the look descriptions and UWConversationScreen for the look text. Created per
/// conversation by UWConversationScreen; the components are looked up once when first needed.
///
/// THE WORLD (cleanup step 3.6, 2026-10-17): the level, its number, gronk_door's door, the
/// objects place_object puts down, the drops at the end of a barter, remove_talker and the
/// inventory are the world's (UWWorld), which the rules ask themselves; this host keeps the
/// player's numbers and the two text services. Per conversation: the long-lived world keeps
/// none of its state.
/// </summary>
public sealed class UWConversationHost : IUWConversationHost
{
    private UWCharacter mOCharacter;

    private Interaction mOInteraction;

    private UWConversationScreen mOScreen;

    private bool mbLookedUp;

    private void fLookUp()
    {
        if (mbLookedUp)
            return;

        mbLookedUp = true;

        mOCharacter = UWScene.Character;
        mOInteraction = UWScene.Interaction;
        mOScreen = UWScene.ConversationScreen;

        mOWorld = UWWorld.Of(UWScene.LevelLoader);
    }

    /// <summary>The world on the loader's object (cleanup step 3.1, 2026-10-17), looked up with the
    /// rest when first needed. Null without a loader.</summary>
    public IUWWorld World
    {
        get
        {
            fLookUp();
            return mOWorld;
        }
    }

    private UWWorld mOWorld;

    private UWCharacter fCharacter
    {
        get
        {
            fLookUp();
            return mOCharacter;
        }
    }


    // ------------------------------------------------- The player

    public bool HasPlayer => fCharacter != null;

    public int PlayerHunger => fCharacter != null ? fCharacter.Hunger : 0;

    public int PlayerHitPoints => fCharacter != null ? Mathf.RoundToInt(fCharacter.CurrentHP) : 0;

    public int PlayerMaxHitPoints => fCharacter != null ? Mathf.RoundToInt(fCharacter.MaxHP) : 0;

    /// <summary>Mana ROUNDED TO THE NEAREST, as the flasks show it - the spell host rounds down
    /// (IUWSpellHost.PlayerMana); two conventions on purpose, kept apart by cleanup step 3.1
    /// (2026-10-17).</summary>
    public int PlayerMana => fCharacter != null ? Mathf.RoundToInt(fCharacter.CurrentMana) : 0;

    public int PlayerAttack => fCharacter != null ? fCharacter.Attack : 0;

    public int PlayerStrength => fCharacter != null ? fCharacter.Strength : 0;

    public int PlayerDexterity => fCharacter != null ? fCharacter.Dexterity : 0;

    public int PlayerLevel => fCharacter != null ? fCharacter.Level : 0;

    public int PlayerClock => fCharacter != null ? fCharacter.ClockValue : 0;

    public int PlayerPoison => fCharacter != null ? fCharacter.Poison : 0;

    public bool PlayerWeaponDrawn => fCharacter != null && fCharacter.DrawWWeapon;

    public int GetPlayerSkill(UWPlayerData.Skill peSkill)
    {
        return fCharacter != null ? fCharacter.GetSkill(peSkill) : 0;
    }

    public int GetPlayerSkillByNumber(int piSkillNumber)
    {
        return fCharacter != null ? fCharacter.GetSkillByNumber(piSkillNumber) : 0;
    }

    public void SetPlayerSkillByNumber(int piSkillNumber, int piValue)
    {
        if (fCharacter != null)
            fCharacter.SetSkillByNumber(piSkillNumber, piValue);
    }

    public bool TryIncreasePlayerSkill(int piSkillNumber)
    {
        return fCharacter != null && fCharacter.TryIncreaseSkill(piSkillNumber);
    }

    public void ApplyConversationValues(int piHunger, int piHitPoints, int piMana, int piPoison)
    {
        if (fCharacter != null)
            fCharacter.ApplyConversationValues(piHunger, piHitPoints, piMana, piPoison);
    }

    public void ChangeExperience(int piAmount, int piDungeonLevel)
    {
        if (fCharacter != null)
            fCharacter.ChangeExperience(piAmount, piDungeonLevel);
    }

    // ------------------------------------------------- Text

    public string DescribeItem(UWObject pOItem, int piDetail = UWItemDescriptions.DetailFromLoreCheck)
    {
        fLookUp();

        return mOInteraction != null && pOItem != null ? mOInteraction.DescribeInventoryItem(pOItem, piDetail) : null;
    }

    public bool ShowLookText(string psText)
    {
        fLookUp();

        if (mOScreen == null)
            return false;

        mOScreen.ShowLookText(psText);

        return true;
    }
}
