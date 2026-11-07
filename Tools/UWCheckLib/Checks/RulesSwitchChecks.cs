using System.Reflection;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// A CHANGED RULE REACHES THE GAME, wave 2 part B (cleanup phase 10, 2026-10-26): one call site
    /// per domain of trade, inventory, lights, story and levels, and each quirk and deviation
    /// switch both ways where an engine-free call shows it - first at the original's values, then
    /// with a rules.json on the current session. A rule declared but still read from its const
    /// somewhere, or a switch whose other branch is not reached, would show here.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>The game window's "Fix bugs of the original" (bug plan A): Judy takes no push with it on, every
        /// creature does with it off, a creature off the list always does, and a rules.json key decides one switch
        /// against the option.</summary>
        private static void fCheckOriginalBugFixes()
        {
            UWGameSession lOSession = new UWGameSession(null, 3);
            string lsNoFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "uwr-no-rules-here.json");

            UWGameSession.MakeCurrent(lOSession);

            lOSession.ApplyRulesFile(lsNoFile, true);

            string lsOn = UWCreatureActor.TakesPush(lOSession.Rules, 23) + " " + UWCreatureActor.TakesPush(lOSession.Rules, 6) + " "
                + lOSession.Rules.Deviations.ConversationPatches + " " + lOSession.Rules.Deviations.TidyLeakedObjects;

            lOSession.ApplyRulesFile(lsNoFile, false);

            string lsOff = UWCreatureActor.TakesPush(lOSession.Rules, 23) + " " + UWCreatureActor.TakesPush(lOSession.Rules, 6) + " "
                + lOSession.Rules.Deviations.ConversationPatches + " " + lOSession.Rules.Deviations.TidyLeakedObjects;

            Check.Str("bug fixes: Judy is not pushed, others are, the patches run, leaked copies are freed (option on / off)",
                lsOn + " / " + lsOff, "False True True True / True True False False");

            lOSession.ApplyRulesFile(lsNoFile, true);
            UWRulesJson.Apply(lOSession.Rules, "{ \"deviations.unpushableNpcs\": false }");

            Check.Str("bug fixes: a rules.json key takes back one switch, the other stays",
                UWCreatureActor.TakesPush(lOSession.Rules, 23) + " " + lOSession.Rules.Deviations.ConversationPatches, "True True");

            // "Remove exploits of the original" (2026-11-07): off by default, apart from the bug option.
            lOSession.ApplyRulesFile(lsNoFile);

            string lsDefault = lOSession.Rules.Deviations.DebrisLosesSpell.ToString();

            lOSession.ApplyRulesFile(lsNoFile, false, true);

            string lsRemoved = lOSession.Rules.Deviations.DebrisLosesSpell + " " + lOSession.Rules.Deviations.UnpushableNpcs;

            lOSession.ApplyRulesFile(lsNoFile, true, false);

            Check.Str("exploits: the debris spell stays by default, goes with the option alone, the bug option leaves it",
                lsDefault + " / " + lsRemoved + " / " + lOSession.Rules.Deviations.DebrisLosesSpell, "False / True False / False");

            UWGameSession.MakeCurrent(new UWGameSession(null, 3));
        }

        private static void fCheckRulesReachTheGameWave2B()
        {
            UWGameSession lOSession = new UWGameSession(null, 3);

            UWGameSession.MakeCurrent(lOSession);

            string lsOriginal = fWave2BSites();

            Check.Str("rules reach the game, wave 2 B: at the original's values"
                + " (appraisal, reach, light, start tile, volcano, lair, taper, orb mana)",
                lsOriginal, "68 / 144 / 3 False / 32 2 / Flame / False / False / False");

            UWRulesReport lOReport = UWRulesJson.Apply(lOSession.Rules,
                "{ \"trade.appraisal.base\": 25, \"inventory.reach.useDistanceSquared\": 200,"
                + " \"lights.sources.paletteLightLevel\": [2, 1, 5, 3, 3, 1, 1, 2, 2],"
                + " \"lights.sources.openFire\": [1, 1, 1, 1, 1, 0, 0, 0, 0],"
                + " \"story.start.tileX\": 10, \"story.volcano.centre\": 20, \"levels.lair\": 5,"
                + " \"quirks.litTaperStaysLit\": false, \"deviations.orbManaAsideOnlyWhileOrbStands\": false }");

            Check.Str("rules reach the game, wave 2 B: the file taken", lOReport.Summary, "rules.json: 9 rules taken");
            Check.Str("rules reach the game, wave 2 B: with a rules.json",
                fWave2BSites(), "113 / 200 / 5 True / 10 2 / NotApplicable / True / True / True");
        }

        /// <summary>The sites, as one line: the partner's appraisal error (trade.appraisal), the bare
        /// hand's pickup reach (inventory.reach), the lantern's palette level and open fire
        /// (lights.sources), a new character's start tile (story.start), a talisman at 32/32 on the
        /// volcano's level (story.volcano), the orb standing on level index 5 (levels.lair), a lit taper put into the
        /// world going out (quirks.litTaperStaysLit), and the mana held back on the lair's level
        /// after the orb broke (deviations.orbManaAsideOnlyWhileOrbStands).</summary>
        private static string fWave2BSites()
        {
            UWRules lORules = UWRules.Current;
            UWConversationTrade lOTrade = new UWConversationTrade(null, null, null, piLine => { }, psLine => { }, psText => 0,
                UWGameSession.Current);
            object lOAccuracy = typeof(UWConversationTrade)
                .GetField("miAccuracy", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lOTrade);

            UWLightSourceTable.TryGet(UWObjectIds.Lantern, out UWLightSourceTable.Row lOLantern);

            UWPlayerData.NewCharacter lOCharacter = new UWPlayerData.NewCharacter();
            int liTalismans = UWQuestFlags.Get(UWEndgameRules.TalismanQuestFlag);

            UWQuestFlags.Set(UWEndgameRules.TalismanQuestFlag, 2);

            string lsTalisman = UWEndgameRules.DropTalismanInLava(310, UWLevelIds.Volcano, 32, 32, true, true, out _).ToString();

            UWQuestFlags.Set(UWEndgameRules.TalismanQuestFlag, liTalismans);

            UWGameFlags.OrbDestroyed = false;

            bool lbOrbStands = UWTybalOrbRules.OrbStands(5);
            UWObject lOTaper = new UWObject(UWObjectIds.LitTaper);
            bool lbTaperOut = UWObjectMechanics.ExtinguishInWorld(lOTaper, null);

            UWGameFlags.OrbDestroyed = true;

            bool lbManaAside = UWTybalOrbRules.KeepsMaxManaAside(lORules.Levels.Lair);

            UWGameFlags.OrbDestroyed = false;

            return string.Join(" / ",
                lOAccuracy,
                UWReachRules.GetPickupDistanceSquared(false),
                (int)lOLantern.PaletteLightLevel + " " + lOLantern.OpenFire,
                lOCharacter.StartTileX + " " + lOCharacter.StartTileY,
                lsTalisman,
                lbOrbStands,
                lbTaperOut,
                lbManaAside);
        }
    }
}
