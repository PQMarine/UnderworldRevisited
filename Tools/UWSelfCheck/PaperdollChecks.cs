using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.6 (2026-10-18): the paper doll's choice of pictures moved out of UWCharacter
    /// into UWPaperdollRules. Pinned: the four conditions as quarters of the quality, the empty slot,
    /// the men's plate legs two rows lower, and the body index's fallback.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckPaperdoll()
        {
            Check.Group("Paper doll (UWPaperdollRules - cleanup step 4.6)");

            fExpectBool("paperdoll: quality 0 to 15 is damaged",
                UWPaperdollRules.GetArmorCondition(0) == UWWearables.ArmorConditions.Damaged
                && UWPaperdollRules.GetArmorCondition(15) == UWWearables.ArmorConditions.Damaged, true);
            fExpectBool("paperdoll: a breastplate at quality 20 is worn, as in the original",
                UWPaperdollRules.GetArmorCondition(20) == UWWearables.ArmorConditions.Worn, true);
            fExpectBool("paperdoll: 32 to 47 serviceable, 48 to 63 excellent",
                UWPaperdollRules.GetArmorCondition(32) == UWWearables.ArmorConditions.Servicable
                && UWPaperdollRules.GetArmorCondition(47) == UWWearables.ArmorConditions.Servicable
                && UWPaperdollRules.GetArmorCondition(48) == UWWearables.ArmorConditions.Excellent
                && UWPaperdollRules.GetArmorCondition(63) == UWWearables.ArmorConditions.Excellent, true);

            UWPaperdollRules.SlotLook lOEmpty = UWPaperdollRules.GetSlotLook(null, UWWearables.Sex.Male);

            fExpectBool("paperdoll: an empty slot shows nothing and drops nothing",
                lOEmpty.Type == UWWearables.ArmorTypes.None && lOEmpty.Drop == 0, true);

            UWPaperdollRules.SlotLook lOVest = UWPaperdollRules.GetSlotLook(new UWObject(32) { Quality = 20 }, UWWearables.Sex.Male);

            fExpectBool("paperdoll: a leather vest at 20 is the worn leather chest",
                lOVest.Type == UWWearables.ArmorTypes.Chest && lOVest.Material == UWWearables.ArmorMaterials.Leather
                && lOVest.Condition == UWWearables.ArmorConditions.Worn && lOVest.Drop == 0, true);

            fExpectInt("paperdoll: a man's plate leggings sit two rows lower",
                UWPaperdollRules.GetSlotLook(new UWObject(37) { Quality = 40 }, UWWearables.Sex.Male).Drop, 2);
            fExpectInt("paperdoll: a woman's do not",
                UWPaperdollRules.GetSlotLook(new UWObject(37) { Quality = 40 }, UWWearables.Sex.Female).Drop, 0);
            fExpectBool("paperdoll: a sword is no armour",
                UWPaperdollRules.GetSlotLook(new UWObject(4) { Quality = 40 }, UWWearables.Sex.Male).Type == UWWearables.ArmorTypes.None, true);

            fExpectInt("paperdoll: without player data the Inspector's body", UWPaperdollRules.GetBodyIndex(null, 3), 3);
            fExpectInt("paperdoll: an out-of-range body falls back to 9", UWPaperdollRules.GetBodyIndex(null, 12), 9);
            fExpectBool("paperdoll: the first five bodies are men",
                UWPaperdollRules.GetSex(4) == UWWearables.Sex.Male && UWPaperdollRules.GetSex(5) == UWWearables.Sex.Female, true);
        }
    }
}
