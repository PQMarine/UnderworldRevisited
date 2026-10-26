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
