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

    /// <summary>The record and the running numbers both (the vitals hold a copy of the skills for combat and magic).</summary>
    public void SetPlayerSkillByNumber(int piSkillNumber, int piValue)
    {
        if (fFile != null && piSkillNumber >= 0 && piSkillNumber <= UWPlayerRecordUw2.SwimmingSkill)
            fFile.Record[UWPlayerRecordUw2.SkillsOffset + piSkillNumber] = (byte)piValue;

        if (fVitals != null)
            fVitals.Vitals.SetSkillByNumber(piSkillNumber, piValue);
    }

    /// <summary>x_skills at 10000: get_skill without a point (no conversation of the second game asks it).</summary>
    public bool TryIncreasePlayerSkill(int piSkillNumber)
    {
        if (fVitals == null)
            return false;

        bool lbRose = fVitals.Vitals.TryIncreaseSkill(piSkillNumber);

        fVitals.WriteSkillsToRecord();

        return lbRose;
    }

    /// <summary>x_skills above 10000, the trainers (G5 part 1): a point left, grant_skill_advance, the point spent when
    /// anything rose (uw2 conv/BABLHACK.C:414).</summary>
    public bool AdvancePlayerSkillByPoint(int piWhich)
    {
        if (fVitals == null || fVitals.Vitals.SkillPoints <= 0)
            return false;

        bool lbRose = fVitals.Vitals.GrantSkillAdvance(piWhich);

        if (lbRose)
            fVitals.Vitals.SpendSkillPoint();

        fVitals.WriteSkillsToRecord();

        if (fVitals.Vitals.LogExperience)
            Debug.Log("[Experience] Skill point on " + piWhich + (lbRose ? ": raised to " + GetPlayerSkillByNumber(piWhich) : ": nothing rose")
                + ", " + fVitals.Vitals.SkillPoints + " points left.");

        return lbRose;
    }

    private static UWVitalsUw2 fVitals => UWVitalsUw2.Current != null && UWVitalsUw2.Current.Vitals != null ? UWVitalsUw2.Current : null;

    public void ApplyConversationValues(int piHunger, int piHitPoints, int piMana, int piPoison)
    {
        UWPlayerRecordUw2.SetConversationValues(fFile, piHunger, piHitPoints, piMana, piPoison);
    }

    public void ChangeExperience(int piAmount, int piDungeonLevel)
    {
        if (UWVitalsUw2.Current != null && UWVitalsUw2.Current.Vitals != null)
            UWVitalsUw2.Current.Vitals.ChangeExperience(piAmount, piDungeonLevel);
    }

    /// <summary>The trade slot's look (UWGameScreenUw2.LookInTrade): said straight into the answer scroll, so nothing is
    /// left to return - the session prints nothing more.</summary>
    public string DescribeItem(UWObject pOItem, int piDetail = UWItemDescriptions.DetailFromLoreCheck)
    {
        UWGameScreenUw2.LookInTrade(pOItem, piDetail);

        return null;
    }

    public bool ShowLookText(string psText)
    {
        return UWConversationScreenUw2.ShowLook(psText);
    }
}
