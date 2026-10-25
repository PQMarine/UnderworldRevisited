using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>The help's STATS tab - see OnFlow. Moved out of UWHelpWindow (cleanup 8.6,
/// 2026-10-24).</summary>
public sealed class UWHelpStatsTab : UWHelpTab
{
    public override UWHelpTabId Id => UWHelpTabId.Stats;

    public override string TitleId => "help.tab.stats";

    /// <summary>The panel with the values the original never shows as numbers - closed until
    /// asked for (per user, 2026-09-26).</summary>
    private bool mbShowHiddenStats;

    /// <summary>What the tab shows of the character (UWCharacterSheet, readable).</summary>
    private readonly UWCharacterSheet mOSheet = new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Readable);

    /// <summary>The list being drawn, in either form (IUWHelpFlow, cleanup 8b.3).</summary>
    private IUWHelpFlow mOFlow;

    /// <summary>
    /// THE STATS TAB (feature wish of the user, 2026-09-26): what the character panel shows, in
    /// readable type and all twenty skills at once, live while the help is open. Below it,
    /// folded away, the values the game keeps but only hints at: food, fatigue, poison, the
    /// unspent skill points, the exact experience and the next level's threshold, the load.
    /// </summary>
    protected override void OnFlow(IUWHelpFlow pOFlow)
    {
        mOFlow = pOFlow;

        UWCharacter lOCharacter = UWScene.Character;
        UWGameUI lOUi = UWScene.GameUi;
        DataImport lOData = fData();

        if (lOCharacter == null || lOData == null)
        {
            mOFlow.Line(UWText.T("help.stats.title"), UWHelpLookEnum.Heading);
            mOFlow.Line(UWText.T("help.stats.no_character"), UWHelpLookEnum.Dim);

            return;
        }

        UWPlayerData lOPlayer = lOData.InitialPlayer;

        // What the panel shows, readable (UWCharacterSheet: title case, "a / b"; cleanup 5.9,
        // 2026-10-20).
        UWCharacterSheet lOSheet = mOSheet.Bind(lOData.Strings, lOPlayer, lOCharacter.Vitals);

        mOFlow.Line(lOSheet.Name, UWHelpLookEnum.Heading);
        mOFlow.Line(lOSheet.LevelWithClass, UWHelpLookEnum.Text);

        string lsCondition = UWStatusReport.GetConditionLine(lOData, lOCharacter.Vitals.Hunger, lOCharacter.Vitals.Fatigue);

        if (!string.IsNullOrEmpty(lsCondition))
            mOFlow.Line(lsCondition.Trim(), UWHelpLookEnum.Dim);

        mOFlow.Space(10f);
        mOFlow.Line(UWText.T("help.stats.attributes"), UWHelpLookEnum.SubHeading);

        for (int liRow = 0; liRow < UWCharacterSheet.ValueCount; liRow++)
            fStatRow(UWCharacterSheet.ValueLabel(liRow), lOSheet.Value(liRow));


        int liCarried = -1;

        if (lOUi != null && lOUi.mOInventory != null)
        {
            liCarried = lOUi.mOInventory.GetCarriedTenthStones(lOData.CommonObjectProperties);

            // What the circle below the paper doll shows: the capacity left, in whole stones.
            fStatRow(UWText.T("help.stats.can_carry"), UWText.F("help.stats.stones", UWInventoryWeight.RemainingStones(lOPlayer, liCarried)));
        }

        mOFlow.Space(10f);
        mOFlow.Line(UWText.T("help.stats.skills"), UWHelpLookEnum.SubHeading);

        // Attack and Defense first, then the eighteen skills - the order of the panel's list
        // (UWCharacterSheet.TryGetSkill).
        for (int liEntry = 0; liEntry < UWCharacterSheet.SkillEntries; liEntry++)
        {
            lOSheet.TryGetSkill(liEntry, out string lsName, out string lsValue);
            fStatRow(lsName, lsValue);
        }

        mOFlow.Space(16f);

        if (mOFlow.Fold(mbShowHiddenStats ? UWText.T("help.stats.hide_hidden") : UWText.T("help.stats.show_hidden")))
            mbShowHiddenStats = !mbShowHiddenStats;

        if (!mbShowHiddenStats)
            return;

        mOFlow.Space(10f);
        mOFlow.Line(UWText.T("help.stats.hidden_intro"), UWHelpLookEnum.Dim);
        mOFlow.Space(4f);

        fStatRow(UWText.T("help.stats.food"), lOCharacter.Vitals.Hunger + " / " + UWPlayerVitals.FullHunger);
        mOFlow.Line(UWText.T("help.stats.food_note"), UWHelpLookEnum.Dim);
        fStatRow(UWText.T("help.stats.fatigue"), lOCharacter.Vitals.Fatigue + " / 255");
        mOFlow.Line(UWText.T("help.stats.fatigue_note"), UWHelpLookEnum.Dim);
        fStatRow(UWText.T("help.stats.poison"), lOCharacter.Vitals.Poison + " / 15");
        fStatRow(UWText.T("help.stats.hallucination"), lOCharacter.Vitals.Hallucination + " / " + UWPlayerVitals.MaxHallucination
            + fHallucinationEffectText());
        mOFlow.Line(UWText.T("help.stats.hallucination_note"),
            UWHelpLookEnum.Dim);
        fStatRow(UWText.T("help.stats.skill_points"), lOCharacter.Vitals.SkillPoints.ToString());
        mOFlow.Line(UWText.T("help.stats.skill_points_note"),
            UWHelpLookEnum.Dim);

        fStatRow(UWText.T("help.stats.experience"), fTenths(lOCharacter.Vitals.Experience));

        int liNext = UWExperience.GetNextLevelExperience(lOCharacter.Vitals.Level);

        if (liNext < 0)
            fStatRow(UWText.T("help.stats.next_level"), UWText.T("help.stats.next_level_none"));
        else
            fStatRow(UWText.T("help.stats.next_level_at"), UWText.F("help.stats.next_level_value", liNext / 10,
                fTenths(Mathf.Max(0, liNext - lOCharacter.Vitals.Experience))));

        if (liCarried >= 0 && lOPlayer != null)
            fStatRow(UWText.T("help.stats.load"), UWText.F("help.stats.load_value", fTenths(liCarried), fTenths(lOPlayer.MaxWeight)));

        fDrawWornMagic(lOData, lOUi != null ? lOUi.mOInventory : null);
    }

    /// <summary>The worn equipment's own effects, without cast spells - see fDrawWornMagic.</summary>
    private readonly UWArmourProtection.Status mOWornStatus = new UWArmourProtection.Status();

    /// <summary>
    /// WHAT THE WORN MAGIC DOES (per user, 2026-10-02: "show the effect of the equipped magic
    /// items in the hidden values"). First every enchanted item on the paper doll with the name
    /// of its enchantment - whether identified or not -, then the sum of them as the status pass
    /// counts it (UWArmourProtection.Compute without the active spells): only the five armour
    /// pieces, the two rings and a shield cast theirs; a weapon's acts only when it strikes.
    /// </summary>
    private void fDrawWornMagic(DataImport pOData, UWInventory pOInventory)
    {
        mOFlow.Space(10f);
        mOFlow.Line(UWText.T("help.stats.worn_magic"), UWHelpLookEnum.SubHeading);

        if (pOInventory == null)
        {
            mOFlow.Line(UWText.T("help.stats.no_inventory"), UWHelpLookEnum.Dim);
            return;
        }

        bool lbLeftHanded = pOInventory.IsLeftHanded;
        UWArmorItemMap.BodySlot leWeaponSlot = UWArmourProtection.GetWeaponSlot(lbLeftHanded);
        UWArmorItemMap.BodySlot leShieldSlot = UWArmourProtection.GetShieldSlot(lbLeftHanded);
        bool lbAny = false;

        foreach (UWArmorItemMap.BodySlot leSlot in msWornMagicSlots)
        {
            UWObject lOItem = pOInventory.GetEquipped(leSlot);

            if (lOItem == null)
                continue;

            UWEnchantment.Result lOEnchantment = UWEnchantment.Get(lOItem, null, pOData.Strings);

            if (!lOEnchantment.HasEnchantment)
                continue;

            string lsEffect = lOEnchantment.IsCursed ? UWText.T("help.stats.curse") : lOEnchantment.Name;

            if (leSlot == leWeaponSlot)
                lsEffect = UWText.F("help.stats.when_striking", lsEffect);
            else if (leSlot == leShieldSlot && !UWPlayerCritterRow.IsWornInShieldHand(lOItem.ID))
                lsEffect = UWText.F("help.stats.not_in_effect", lsEffect);

            // The slot alone, not the item's name - it saves the room (per user, 2026-10-02).
            fStatRow(fSlotName(leSlot, leWeaponSlot, leShieldSlot), lsEffect);
            lbAny = true;
        }

        if (!lbAny)
        {
            mOFlow.Line(UWText.T("help.stats.nothing_enchanted"), UWHelpLookEnum.Dim);
            return;
        }

        UWArmourProtection.Compute(pOInventory.Model, pOData, null, mOWornStatus);

        mOFlow.Space(6f);
        mOFlow.Line(UWText.T("help.stats.together"), UWHelpLookEnum.Dim);

        int[] liProtection = mOWornStatus.Protection;

        if (liProtection[UWArmourProtection.PartBody] + liProtection[UWArmourProtection.PartHands]
            + liProtection[UWArmourProtection.PartLegs] + liProtection[UWArmourProtection.PartHead] > 0)
            fStatRow(UWText.T("help.stats.protection"), UWText.F("help.stats.protection_value",
                liProtection[UWArmourProtection.PartHead], liProtection[UWArmourProtection.PartBody],
                liProtection[UWArmourProtection.PartHands], liProtection[UWArmourProtection.PartLegs]));

        if (mOWornStatus.DamageResistance > 0)
            fStatRow(UWText.T("help.stats.resistance"), fSpellName(pOData, ResistanceClass, mOWornStatus.DamageResistance));

        if (mOWornStatus.Brightness > 0)
            fStatRow(UWText.T("help.stats.light"), fSpellName(pOData, LightClass, mOWornStatus.Brightness));

        fBitRow(pOData, UWText.T("help.stats.movement"), MotionClass, 1, 5, mOWornStatus.MotionAbilities, piMinor => 1 << (piMinor - 1));
        fBitRow(pOData, UWText.T("help.stats.stealth"), BonusClass, 2, 4, mOWornStatus.StealthBonus, piMinor => 1 << (piMinor - 1));
        fBitRow(pOData, UWText.T("help.stats.immunity"), BonusClass, 5, 9, mOWornStatus.DamageTypeProof, piMinor => msProofBitOfMinor[piMinor - 5]);
        fBitRow(pOData, UWText.T("help.stats.regeneration"), UWWornRegeneration.MajorClass, 0xE, 0xF, mOWornStatus.RegenerationBits,
            piMinor => UWWornRegeneration.GetBits(UWWornRegeneration.MajorClass, piMinor));

        if (mOWornStatus.CurseDice > 0)
            fStatRow(UWText.T("help.stats.curse"), UWText.F("help.stats.curse_value", mOWornStatus.CurseDice));

        if (mOWornStatus.HasMazeNavigation)
            fStatRow(UWText.T("help.stats.maze"), UWText.T("help.stats.maze_value"));

        if (mOWornStatus.HasDragonSkinBoots)
            fStatRow(UWText.T("help.stats.dragon_boots"), UWText.T("help.stats.dragon_boots_value"));
    }

    /// <summary>The slots that can carry worn magic: the five armour pieces, the rings and the
    /// two front hands.</summary>
    private static readonly UWArmorItemMap.BodySlot[] msWornMagicSlots =
    {
        UWArmorItemMap.BodySlot.Helmet, UWArmorItemMap.BodySlot.Chest, UWArmorItemMap.BodySlot.Gloves,
        UWArmorItemMap.BodySlot.Legs, UWArmorItemMap.BodySlot.Boots, UWArmorItemMap.BodySlot.LeftRing,
        UWArmorItemMap.BodySlot.RightRing, UWArmorItemMap.BodySlot.RightHandSlot, UWArmorItemMap.BodySlot.LeftHandSlot
    };

    // The enchantment classes the summary names (UWArmourProtection.fApplyEnchantment).
    private const int LightClass = 0;

    private const int MotionClass = 1;

    private const int ResistanceClass = 2;

    private const int BonusClass = 3;

    /// <summary>The immunities' bits, minor class 5 to 9 (UWArmourProtection.msProofBits).</summary>
    private static readonly int[] msProofBitOfMinor = { 0x40, 0x08, 0x10, 0x01, 0x02 };

    /// <summary>One row naming every enchantment of a class whose bit is set.</summary>
    private void fBitRow(DataImport pOData, string psLabel, int piMajor, int piFirstMinor, int piLastMinor,
        int piBits, System.Func<int, int> pFBitOf)
    {
        List<string> lONames = new List<string>();

        for (int liMinor = piFirstMinor; liMinor <= piLastMinor; liMinor++)
        {
            if ((piBits & pFBitOf(liMinor)) != 0)
                lONames.Add(fSpellName(pOData, piMajor, liMinor));
        }

        if (lONames.Count > 0)
            fStatRow(psLabel, string.Join(", ", lONames));
    }

    /// <summary>A spell name from string block 6, (major &lt;&lt; 4) | minor plus the project's one.</summary>
    private static string fSpellName(DataImport pOData, int piMajor, int piMinor)
    {
        try
        {
            return pOData.Strings.Blocks[UWEnchantment.SpellStringBlock].Strings[((piMajor << 4) | piMinor) + 1].Trim();
        }
        catch
        {
            return "?";
        }
    }

    private static string fSlotName(UWArmorItemMap.BodySlot peSlot, UWArmorItemMap.BodySlot peWeaponSlot,
        UWArmorItemMap.BodySlot peShieldSlot)
    {
        if (peSlot == peWeaponSlot)
            return UWText.T("help.stats.slot.weapon_hand");

        if (peSlot == peShieldSlot)
            return UWText.T("help.stats.slot.shield_hand");

        switch (peSlot)
        {
            case UWArmorItemMap.BodySlot.LeftRing: return UWText.T("help.stats.slot.left_ring");
            case UWArmorItemMap.BodySlot.RightRing: return UWText.T("help.stats.slot.right_ring");
            case UWArmorItemMap.BodySlot.Helmet: return UWText.T("help.stats.slot.helmet");
            case UWArmorItemMap.BodySlot.Chest: return UWText.T("help.stats.slot.chest");
            case UWArmorItemMap.BodySlot.Gloves: return UWText.T("help.stats.slot.gloves");
            case UWArmorItemMap.BodySlot.Legs: return UWText.T("help.stats.slot.legs");
            case UWArmorItemMap.BodySlot.Boots: return UWText.T("help.stats.slot.boots");
            default: return peSlot.ToString();
        }
    }

    /// <summary>Which of the three pictures the hallucination shows (UWHallucinationState), or
    /// nothing while none runs.</summary>
    private static string fHallucinationEffectText()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWRenderPathHost lOPaths = lOLoader != null ? lOLoader.GetComponent<UWRenderPathHost>() : null;

        if (lOPaths == null)
            return string.Empty;

        switch (lOPaths.HallucinationEffect)
        {
            case UWDataImport.UWData.UWHallucinationState.ScrambleEffect:
                return ", " + UWText.T("help.stats.hallucination.scrambled");

            case UWDataImport.UWData.UWHallucinationState.PaletteEffect:
                return ", " + UWText.F("help.stats.hallucination.palette", lOPaths.HallucinationPalette);

            case UWDataImport.UWData.UWHallucinationState.LightTableEffect:
                return ", " + UWText.T("help.stats.hallucination.light_table");

            default:
                return string.Empty;
        }
    }

    /// <summary>A label on the left, its value on the right (IUWHelpFlow.StatRow; the IMGUI form's
    /// calls are UWHelpImguiFlow's since cleanup 8b.3).</summary>
    private void fStatRow(string psLabel, string psValue)
    {
        mOFlow.StatRow(psLabel, psValue);
    }

    /// <summary>A value kept in tenths, with its decimal place.</summary>
    private static string fTenths(int piTenths)
    {
        return (piTenths / 10) + "." + Mathf.Abs(piTenths % 10);
    }
}
