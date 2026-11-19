using System.Collections.Generic;
using System.Linq;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S CONVERSATION EFFECTS ON THE PLAYER (G5 part 1, 2026-11-16): x_skills' values, the advance by a
    /// skill point (grant_skill_advance and get_skill, uw2 game/SKILLS.C:152-216), skill_rng, babl_hack 10's signet ring
    /// and babl_hack 7's charges (uw2 conv/BABLHACK.C).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2ConversationEffects()
        {
            Check.Group("UW2 conversation effects: x_skills, grant_skill_advance, the signet ring, a wand's charges");

            // x_skills' values: the trainers' 10001, get_skill's 10000, a set up to 30, the data's 9999 a read.
            string lsCalls = string.Join(" ", new[] { 10001, 10000, 30, 0, 31, 9999, -1 }
                .Select(piValue => UWConversationRulesUw2.SkillCall(piValue).ToString()));
            Check.Str("x_skills: above 10000 a point, 10000 get_skill, 0..30 set, else read", lsCalls,
                "AdvanceByPoint RaiseWithoutPoint Set Set Read Read Read");

            // skill_rng: the second game's ranges, the first game's beside them.
            Check.Str("skill_rng: Strength 25, Dexterity 30, Intelligence 15 (the first game's 25 40 10)",
                string.Join(" ", UWGameProfiles.Uw2.CreateOriginalRules().Experience.SkillRaise.ChanceRange) + " / "
                + string.Join(" ", UWGameProfiles.Uw1.CreateOriginalRules().Experience.SkillRaise.ChanceRange), "25 30 15 / 25 40 10");

            // The signet ring on either hand; another ring or none is not it.
            UWObject lOSignet = new UWObject(UWConversationRulesUw2.GuardianSignetRing);
            UWObject lOOther = new UWObject(UWConversationRulesUw2.GuardianSignetRing + 1);
            Check.Str("babl_hack 10: the Guardian's signet ring on the right, the left, another ring, none",
                UWConversationRulesUw2.WearsSignetRing(lOSignet, null) + " " + UWConversationRulesUw2.WearsSignetRing(lOOther, lOSignet) + " "
                + UWConversationRulesUw2.WearsSignetRing(lOOther, null) + " " + UWConversationRulesUw2.WearsSignetRing(null, null),
                "True True False False");

            // Merzan's recharge: useNSpellCharges(wand, -5) adds five, a full wand stops at 63; a wand without its spell gives 0.
            UWObject lOSpell = new UWObject(UWEnchantSpellRulesUw2.SpellObjectId) { Quality = 2, Quantity = 17, Flags = 0x4 };
            UWObject lOWand = new UWObject(UWCarriedSpellRulesUw2.FirstWand) { Quantity = 1 };
            lOWand.SetContents(new List<UWObject> { lOSpell });
            int liLeft = UWConversationRulesUw2.ChangeCharges(lOWand, -5);
            string lsAfterFive = liLeft + ":" + lOSpell.Quality;
            lOSpell.Quality = 60;
            UWConversationRulesUw2.ChangeCharges(lOWand, -5);
            UWObject lOEmpty = new UWObject(UWCarriedSpellRulesUw2.FirstWand) { Quantity = 1 };
            lOEmpty.SetContents(new List<UWObject>());
            Check.Str("babl_hack 7: -5 gives five charges, at most 63, nothing without the spell",
                lsAfterFive + " " + lOSpell.Quality + " " + UWConversationRulesUw2.ChangeCharges(lOEmpty, -5), "7:7 63 0");

            fCheckUw2SkillAdvance();
        }

        /// <summary>grant_skill_advance with scripted rolls: one skill (get_skill's +1 and the chance point), the cap at 30,
        /// the cap at twice the attribute, and the magic group (mana while below 8 on a roll with bit 2, then a random one
        /// of the three) with player_compute's mana afterwards.</summary>
        private static void fCheckUw2SkillAdvance()
        {
            UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

            lOStart.Strength = 20;
            lOStart.Dexterity = 15;
            lOStart.Intelligence = 24;
            lOStart.ManaSkill = 5;
            lOStart.CastingSkill = 4;

            ScriptedAreaRandom lORandom = new ScriptedAreaRandom(0, 2, 0, 2, 30);
            UWRandom.Use(lORandom);

            try
            {
                UWPlayerVitals lOVitals = new UWPlayerVitals(new FakeVitalsHost());
                lOVitals.Initialise(null, lOStart);

                // Attack 5, Strength 20: +1, no half point for Strength, the chance point on roll 0 (0 < 20 - 6).
                lOVitals.SetSkillByNumber(UWPlayerVitals.AttackSkillNumber, 5);
                bool lbAttack = lOVitals.GrantSkillAdvance(UWPlayerVitals.AttackSkillNumber);
                string lsAttack = lbAttack + ":" + lOVitals.GetSkillByNumber(UWPlayerVitals.AttackSkillNumber);

                // At 30 nothing rises (no roll drawn).
                lOVitals.SetSkillByNumber(UWPlayerVitals.DefenceSkillNumber, 30);
                string lsCapped = lOVitals.GrantSkillAdvance(UWPlayerVitals.DefenceSkillNumber) + ":" + lOVitals.GetSkillByNumber(UWPlayerVitals.DefenceSkillNumber);

                // The magic group: roll 2 picks mana (5 -> 6, the half point -> 7, roll 0 the chance point -> 8); mana is 8
                // now, so the second try rolls among the three: 2 is casting (4 -> 5, half point -> 6, roll 30 no point).
                bool lbMagic = lOVitals.GrantSkillAdvance(UWPlayerVitals.MagicSkillGroup);
                string lsMagic = lbMagic + ":" + lOVitals.GetSkillByNumber(UWPlayerVitals.ManaSkillNumber) + ":"
                    + lOVitals.GetSkillByNumber(UWPlayerVitals.ManaSkillNumber + 2) + ":" + lOVitals.MaxMana;

                Check.Str("grant_skill_advance: attack 5 -> 7, defence at 30 stays, the magic group mana 5 -> 8 and casting 4 -> 6, mana (8 + 1) * 24 / 8",
                    lsAttack + " " + lsCapped + " " + lsMagic, "True:7 False:30 True:8:6:27");

                // Twice the attribute is the other cap: Strength 4, attack 9.
                UWPlayerVitals.StartValues lOWeak = lOStart;
                lOWeak.Strength = 4;
                UWPlayerVitals lOWeakVitals = new UWPlayerVitals(new FakeVitalsHost());
                lOWeakVitals.Initialise(null, lOWeak);
                lOWeakVitals.SetSkillByNumber(UWPlayerVitals.AttackSkillNumber, 9);
                Check.Str("get_skill: nothing above twice the attribute (Strength 4, attack 9)",
                    lOWeakVitals.GrantSkillAdvance(UWPlayerVitals.AttackSkillNumber) + ":" + lOWeakVitals.GetSkillByNumber(UWPlayerVitals.AttackSkillNumber),
                    "False:9");
            }
            finally
            {
                UWRandom.Use(null);
            }
        }
    }
}
