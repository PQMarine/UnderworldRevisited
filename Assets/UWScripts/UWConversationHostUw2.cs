using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The Unity side of a conversation in the second game (UW2 plan phase 3 step 7): its player is the record,
/// not UWCharacter. What the rules of phase 4 will do (experience, a skill's advance) is logged.
/// </summary>
public sealed class UWConversationHostUw2 : IUWConversationHost
{
    private static UWPlayerFile fFile => UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;

    public IUWWorld World => UWWorld.Of(UWScene.LevelLoader);

    public bool HasPlayer => fFile != null;

    public int PlayerHunger => fFile != null ? fFile.Record[UWPlayerRecordUw2.HungerOffset] : 0;

    public int PlayerHitPoints => fFile != null ? UWPlayerRecordUw2.Vitality(fFile) : 0;

    public int PlayerMaxHitPoints => fFile != null ? UWPlayerRecordUw2.MaxVitality(fFile) : 0;

    public int PlayerMana => fFile != null ? UWPlayerRecordUw2.Mana(fFile) : 0;

    public int PlayerAttack => GetPlayerSkillByNumber(0);

    public int PlayerStrength => fFile != null ? UWPlayerRecordUw2.Strength(fFile) : 0;

    public int PlayerDexterity => fFile != null ? UWPlayerRecordUw2.Dexterity(fFile) : 0;

    public int PlayerLevel => fFile != null ? UWPlayerRecordUw2.CharacterLevel(fFile) : 0;

    public int PlayerClock => fFile != null ? UWPlayerRecordUw2.GameClock(fFile) : 0;

    public int PlayerPoison => fFile != null ? UWPlayerRecordUw2.Poison(fFile) : 0;

    public bool PlayerWeaponDrawn => fFile != null && UWPlayerRecordUw2.IsWeaponDrawn(fFile);

    /// <summary>The first game's named skills count from the third of the save's (UWPlayerData.Skill).</summary>
    public int GetPlayerSkill(UWPlayerData.Skill peSkill)
    {
        return GetPlayerSkillByNumber((int)peSkill + UWPlayerRecordUw2.NamedSkillsFrom);
    }

    public int GetPlayerSkillByNumber(int piSkillNumber)
    {
        return fFile != null && piSkillNumber >= 0 && piSkillNumber <= UWPlayerRecordUw2.SwimmingSkill ? UWPlayerRecordUw2.Skill(fFile, piSkillNumber) : 0;
    }

    public void SetPlayerSkillByNumber(int piSkillNumber, int piValue)
    {
        if (fFile != null && piSkillNumber >= 0 && piSkillNumber <= UWPlayerRecordUw2.SwimmingSkill)
            fFile.Record[UWPlayerRecordUw2.SkillsOffset + piSkillNumber] = (byte)piValue;
    }

    public bool TryIncreasePlayerSkill(int piSkillNumber)
    {
        Debug.Log("[Walk] A conversation would raise skill " + piSkillNumber + " (its rules come in phase 4).");

        return false;
    }

    public void ApplyConversationValues(int piHunger, int piHitPoints, int piMana, int piPoison)
    {
        UWPlayerRecordUw2.SetConversationValues(fFile, piHunger, piHitPoints, piMana, piPoison);
    }

    public void ChangeExperience(int piAmount, int piDungeonLevel)
    {
        Debug.Log("[Walk] A conversation would give " + piAmount + " experience (its rules come in phase 4).");
    }

    /// <summary>The walk's look: the name in the first game's sentence (phase 4 has the second game's).</summary>
    public string DescribeItem(UWObject pOItem, int piDetail = UWItemDescriptions.DetailFromLoreCheck)
    {
        DataImport lOData = UWScene.LevelLoader != null ? UWScene.LevelLoader.UWDataImporter : null;

        return pOItem != null ? UWItemDescriptions.DescribeScenery(UWText.RawObjectName(lOData, pOItem.ID), null, pOItem, lOData) : null;
    }

    public bool ShowLookText(string psText)
    {
        return UWConversationScreenUw2.ShowLook(psText);
    }
}
