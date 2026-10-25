using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.3 (2026-10-17): death as an event. Pinned: the vitals raise Died once, on the
    /// crossing to zero, from a hit, the Void's drain or a conversation's hit points, never from a
    /// backfire; UWDeathRules decides as player_is_dead (SKILLS.C) and restores as do_resurrect.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckDeath()
        {
            Check.Group("Death (UWPlayerVitals.Died, UWDeathRules - cleanup step 4.3)");

            List<string> lOTold = new List<string>();
            UWPlayerVitals lOVitals = fPipelineVitals(new FakeVitalsHost(), lOTold);
            int liDeaths = 0;

            lOVitals.Died += () => { liDeaths++; lOTold.Add("died"); };

            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Lava(29));
            fExpectInt("death: 29 of 30 is no death", liDeaths, 0);

            UWDamageOutcome lOOutcome = UWDamagePipeline.Apply(lOVitals, UWDamageRequest.PoisonTick(5, 0));
            fExpectBool("death: the crossing raises Died once, and the outcome says so", liDeaths == 1 && lOOutcome.Died, true);
            fExpectInt("death: after the flasks were told, before the word to the host",
                fIndexOfSequence(lOTold, "vitals", "died", "damaged PoisonTick landed"), 0);

            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Lava(3));
            fExpectInt("death: more damage at zero is no second death", liDeaths, 1);

            lOVitals.ApplyConversationValues(lOVitals.Hunger, UWDeathRules.SparedVitality, 0, 0);
            fExpectInt("death: spared, four hit points", (int)lOVitals.CurrentHP, 4);

            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Curse(30));
            fExpectInt("death: a backfire cannot kill", liDeaths, 1);

            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.VoidDrain(10));
            fExpectInt("death: the Void's drain to zero is a death", liDeaths, 2);

            lOVitals.ApplyConversationValues(lOVitals.Hunger, 10, 0, 0);
            lOVitals.ApplyConversationValues(lOVitals.Hunger, 0, 0, 0);
            fExpectInt("death: a conversation setting zero hit points is a death", liDeaths, 3);

            fExpectInt("rules: no talisman left, spared (player_is_dead)",
                (int)UWDeathRules.Decide(0, 3, 4, 9), (int)UWDeathRules.Outcome.Spared);
            fExpectInt("rules: a tree planted, back at it",
                (int)UWDeathRules.Decide(8, 3, 4, 9), (int)UWDeathRules.Outcome.Resurrect);
            fExpectInt("rules: no tree, final death", (int)UWDeathRules.Decide(8, 0, 4, 9), (int)UWDeathRules.Outcome.Final);
            fExpectInt("rules: in the Void the tree does not help",
                (int)UWDeathRules.Decide(8, 3, UWLevelIds.Void, 9), (int)UWDeathRules.Outcome.Final);
            fExpectInt("rules: a tree level beyond the levels counts as none",
                (int)UWDeathRules.Decide(8, 10, 4, 9), (int)UWDeathRules.Outcome.Final);
            fExpectInt("rules: no talisman left beats the Void", (int)UWDeathRules.Decide(0, 0, UWLevelIds.Void, 9),
                (int)UWDeathRules.Outcome.Spared);

            fExpectInt("rules: death costs an eighth of the experience", UWDeathRules.ExperienceLoss(800), 100);

            int liHp = UWDeathRules.ResurrectionHitPoints(40);

            fExpectBool("resurrection: the maximum less 2 to 4 above 8", liHp >= 36 && liHp <= 38, true);
            fExpectInt("resurrection: all of it up to 8", UWDeathRules.ResurrectionHitPoints(8), 8);
            fExpectInt("resurrection: mana less an eighth and 2 above 8", UWDeathRules.ResurrectionMana(40), 33);
            fExpectInt("resurrection: all the mana up to 8", UWDeathRules.ResurrectionMana(8), 8);
        }
    }
}
