using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE RULE SET (cleanup phase 10, 2026-10-25; UWRules, UWRulesJson). Four things are asked of
    /// it, as the cleanup plan's section 6 wants them:
    ///
    ///   - the original's set as a file (UWRulesJson.Write of CreateOriginal) equals the pinned
    ///     Pins/rules-original.json, so a default cannot change unnoticed (re-record with --record);
    ///   - every rule equals the const its attribute names, its anchor - so a field's initialiser
    ///     and the const the rule class always had cannot drift apart, and a table is a copy;
    ///   - a rules.json that sets EVERY key to another value is taken whole, key by key;
    ///   - the file's edges: no file, a malformed one, unknown keys, wrong kinds, values below a
    ///     rule's minimum, comments, the template itself - and the session that carries the set.
    ///
    /// Every other check of the self-check runs at the defaults, which is the proof that the moved
    /// call sites still compute the original's numbers; a few more here prove that a changed rule
    /// reaches the game.
    /// </summary>
    public static partial class SelfChecks
    {
        private const string RulesPinFile = "rules-original.json";

        /// <summary>A key: lowerCamel parts joined by dots, at least two of them.</summary>
        private static readonly Regex mORuleKey = new Regex(@"^[a-z][A-Za-z0-9]*(\.[a-z][A-Za-z0-9]*)+$");

        /// <summary>The game's folder (UWGameFolder, 2026-10-27): rules.json lives in UW1; one left
        /// beside settings.json by an earlier version moves there, but never over one already there.</summary>
        private static void fCheckRulesFileFolder()
        {
            string lsRoot = Path.Combine(Path.GetTempPath(), "UWCheckGameFolder_" + Guid.NewGuid().ToString("N"));
            List<string> lOLog = new List<string>();
            List<string> lOWarn = new List<string>();

            try
            {
                Directory.CreateDirectory(lsRoot);

                string lsTarget = Path.Combine(lsRoot, UWGameFolder.Uw1, UWRulesJson.FileName);

                Check.Str("game folder: rules.json lies in UW1",
                    UWGameFolder.AdoptLegacyFile(lsRoot, UWRulesJson.FileName, lOLog.Add, lOWarn.Add), lsTarget);
                Check.Int("game folder: ... nothing to move, nothing said", lOLog.Count + lOWarn.Count, 0);

                File.WriteAllText(Path.Combine(lsRoot, UWRulesJson.FileName), "{ \"magic.manaPerCircle\": 2 }");
                UWGameFolder.AdoptLegacyFile(lsRoot, UWRulesJson.FileName, lOLog.Add, lOWarn.Add);
                Check.Bool("game folder: an old rules.json beside settings.json moves into UW1, its text kept",
                    !File.Exists(Path.Combine(lsRoot, UWRulesJson.FileName)) && File.Exists(lsTarget)
                    && File.ReadAllText(lsTarget).Contains("manaPerCircle") && lOLog.Count == 1 && lOWarn.Count == 0, true);

                File.WriteAllText(Path.Combine(lsRoot, UWRulesJson.FileName), "{ }");
                Check.Str("game folder: with both, UW1's counts",
                    UWGameFolder.AdoptLegacyFile(lsRoot, UWRulesJson.FileName, lOLog.Add, lOWarn.Add), lsTarget);
                Check.Bool("game folder: ... neither is overwritten, and a warning names both",
                    File.ReadAllText(lsTarget).Contains("manaPerCircle") && File.ReadAllText(Path.Combine(lsRoot, UWRulesJson.FileName)) == "{ }"
                    && lOWarn.Count == 1, true);
            }
            finally
            {
                try
                {
                    Directory.Delete(lsRoot, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void fCheckRules(CheckContext pOContext)
        {
            Check.Group("Rule set (UWRules at the original's values, rules.json)");

            UWGameSession lOBefore = UWGameSession.Current;

            try
            {
                fCheckRulesPin(pOContext);
                fCheckRuleAnchors();
                fCheckRulesAllKeys();
                fCheckRulesFile();
                fCheckRulesFileFolder();
                fCheckRulesSession();
                fCheckRulesReachTheGame();
                fCheckRulesReachTheCreatures(pOContext);
                fCheckRulesReachTheGameWave2B();
            }
            finally
            {
                UWGameSession.MakeCurrent(lOBefore);
            }
        }

        /// <summary>The original's set, written, against the pin.</summary>
        private static void fCheckRulesPin(CheckContext pOContext)
        {
            string lsNow = UWRulesJson.Write(UWRules.CreateOriginal(), false);
            string lsFolder = fGetPinsFolder();

            if (lsFolder == null)
            {
                Check.Pass("rules: pin", "no source tree here, nothing to compare");

                return;
            }

            string lsPath = Path.Combine(lsFolder, RulesPinFile);

            if (pOContext.Record)
            {
                Directory.CreateDirectory(lsFolder);
                File.WriteAllText(lsPath, lsNow);
                Check.Pass("rules: recorded", UWRules.Entries.Count + " keys into " + PinsFolder + "/" + RulesPinFile);

                return;
            }

            if (!File.Exists(lsPath))
            {
                Check.Fail("rules: pin file " + PinsFolder + "/" + RulesPinFile, "missing - run once with --record");

                return;
            }

            string[] lsPinned = File.ReadAllText(lsPath).Replace("\r\n", "\n").Split('\n');
            string[] lsLines = lsNow.Split('\n');

            if (lsPinned.SequenceEqual(lsLines))
            {
                Check.Pass("rules: the original's set as pinned", UWRules.Entries.Count + " keys");

                return;
            }

            List<string> lsDiffer = new List<string>();

            foreach (string lsLine in lsLines.Except(lsPinned))
                lsDiffer.Add("now    " + lsLine.Trim());

            foreach (string lsLine in lsPinned.Except(lsLines))
                lsDiffer.Add("pinned " + lsLine.Trim());

            Check.Fail("rules: the original's set as pinned", lsDiffer.Count + " line(s) differ - re-record with --record if meant");

            for (int liAt = 0; liAt < lsDiffer.Count && liAt < 12; liAt++)
                Console.WriteLine("         " + lsDiffer[liAt]);
        }

        /// <summary>Every rule: a well-formed key, a meaning, an anchor that exists and equals the
        /// default, a table that is a copy of its anchor, a default not below the minimum.</summary>
        private static void fCheckRuleAnchors()
        {
            UWRules lORules = UWRules.CreateOriginal();
            List<string> lsWrong = new List<string>();
            SortedDictionary<string, int> lOPerDomain = new SortedDictionary<string, int>(StringComparer.Ordinal);

            foreach (UWRuleEntry lOEntry in UWRules.Entries)
            {
                string lsDomain = lOEntry.Key.Substring(0, lOEntry.Key.IndexOf('.'));

                lOPerDomain.TryGetValue(lsDomain, out int liCount);
                lOPerDomain[lsDomain] = liCount + 1;

                if (!mORuleKey.IsMatch(lOEntry.Key))
                    lsWrong.Add(lOEntry.Key + ": not a dotted lowerCamel key");

                if (lOEntry.Rule == null)
                {
                    lsWrong.Add(lOEntry.Key + ": no UWRule attribute");

                    continue;
                }

                if (string.IsNullOrWhiteSpace(lOEntry.Meaning) || lOEntry.Meaning.IndexOf('\n') >= 0)
                    lsWrong.Add(lOEntry.Key + ": no one-line meaning");

                object lOAnchor = lOEntry.GetAnchorValue();
                object lOValue = lOEntry.GetValue(lORules);

                if (lOAnchor == null)
                {
                    lsWrong.Add(lOEntry.Key + ": anchor " + lOEntry.AnchorText + " not found");

                    continue;
                }

                if (lOValue is int[] liTable)
                {
                    if (!(lOAnchor is int[] liAnchorTable) || !liTable.SequenceEqual(liAnchorTable))
                        lsWrong.Add(lOEntry.Key + ": " + fRuleText(lOValue) + ", anchor " + lOEntry.AnchorText + " " + fRuleText(lOAnchor));
                    else if (ReferenceEquals(liTable, lOAnchor))
                        lsWrong.Add(lOEntry.Key + ": the anchor's table itself, not a copy");

                    if (liTable.Any(liValue => liValue < lOEntry.Minimum))
                        lsWrong.Add(lOEntry.Key + ": a default below the minimum " + lOEntry.Minimum);

                    if (liTable.Any(liValue => liValue > lOEntry.Maximum))
                        lsWrong.Add(lOEntry.Key + ": a default above the maximum " + lOEntry.Maximum);
                }
                else
                {
                    if (!Equals(lOValue, lOAnchor))
                        lsWrong.Add(lOEntry.Key + ": " + fRuleText(lOValue) + ", anchor " + lOEntry.AnchorText + " " + fRuleText(lOAnchor));

                    if (lOValue is int liValue && liValue < lOEntry.Minimum)
                        lsWrong.Add(lOEntry.Key + ": the default below the minimum " + lOEntry.Minimum);

                    if (lOValue is int liTop && liTop > lOEntry.Maximum)
                        lsWrong.Add(lOEntry.Key + ": the default above the maximum " + lOEntry.Maximum);
                }
            }

            Check.Pass("rules: keys per domain", string.Join(", ", lOPerDomain.Select(lOPair => lOPair.Key + " " + lOPair.Value)));

            if (lsWrong.Count == 0)
                Check.Pass("rules: every rule at its anchor const", UWRules.Entries.Count + " of " + UWRules.Entries.Count);
            else
                foreach (string lsLine in lsWrong)
                    Check.Fail("rules: every rule at its anchor const", lsLine);
        }

        /// <summary>A file that sets every key to another value: each is taken, nothing is
        /// reported, the original's set stays untouched, and the changed set written and read again
        /// is the same.</summary>
        private static void fCheckRulesAllKeys()
        {
            UWRules lOOriginal = UWRules.CreateOriginal();
            StringBuilder lOJson = new StringBuilder("{\n");
            Dictionary<string, object> lOWanted = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (UWRuleEntry lOEntry in UWRules.Entries)
            {
                object lOOther = fOtherValue(lOEntry.GetValue(lOOriginal), lOEntry.Maximum);

                lOWanted[lOEntry.Key] = lOOther;
                lOJson.Append("  \"").Append(lOEntry.Key).Append("\": ").Append(fRuleText(lOOther)).Append(",\n");
            }

            lOJson.Append("}\n");

            UWRules lOChanged = UWRules.CreateOriginal();
            UWRulesReport lOReport = UWRulesJson.Apply(lOChanged, lOJson.ToString(), "every key");
            List<string> lsNotTaken = new List<string>();

            foreach (UWRuleEntry lOEntry in UWRules.Entries)
            {
                if (fRuleText(lOEntry.GetValue(lOChanged)) != fRuleText(lOWanted[lOEntry.Key]))
                    lsNotTaken.Add(lOEntry.Key);
            }

            Check.Str("rules: every key set to another value, nothing reported", fReportText(lOReport), "");
            Check.Int("rules: every key set to another value, each one taken", lOReport.Applied.Count, UWRules.Entries.Count);
            Check.Str("rules: every key set to another value, each one holds it", string.Join(", ", lsNotTaken), "");
            fExpectSameRules("rules: the original's set is untouched by it", UWRulesJson.Write(UWRules.CreateOriginal(), false),
                UWRulesJson.Write(lOOriginal, false));

            UWRules lOAgain = UWRules.CreateOriginal();

            UWRulesJson.Apply(lOAgain, UWRulesJson.Write(lOChanged, false));
            fExpectSameRules("rules: a changed set written and read again is the same", UWRulesJson.Write(lOAgain, false),
                UWRulesJson.Write(lOChanged, false));
        }

        /// <summary>The edges of a rules.json.</summary>
        private static void fCheckRulesFile()
        {
            string lsOriginal = UWRulesJson.Write(UWRules.CreateOriginal(), false);

            // The template (uwdump rules-template) is a valid file as it stands: comments and all.
            UWRules lORules = UWRules.CreateOriginal();
            UWRulesReport lOReport = UWRulesJson.Apply(lORules, UWRulesJson.Write(UWRules.CreateOriginal(), true), "template");

            Check.Str("rules file: the template reads cleanly", fReportText(lOReport), "");
            Check.Int("rules file: the template sets every key", lOReport.Applied.Count, UWRules.Entries.Count);
            fExpectSameRules("rules file: the template leaves the original's values", UWRulesJson.Write(lORules, false), lsOriginal);

            // Malformed: nothing of it counts.
            lORules = UWRules.CreateOriginal();
            lOReport = UWRulesJson.Apply(lORules, "{\n  \"combat.player.easyAttackBonus\": 4,\n  \"combat.skillCheck.rollRange\" 41\n}");
            Check.Str("rules file: a malformed file is reported with its line", lOReport.Error, "line 3: expected :");
            fExpectSameRules("rules file: a malformed file takes nothing", UWRulesJson.Write(lORules, false), lsOriginal);
            Check.Int("rules file: a malformed file applies no key", lOReport.Applied.Count, 0);

            lOReport = UWRulesJson.Apply(UWRules.CreateOriginal(), "{ \"combat.player.easyAttackBonus\": 4");
            Check.Str("rules file: a file that ends early", lOReport.Error,
                "the file ends too early (expected , or } after the value of combat.player.easyAttackBonus)");

            lOReport = UWRulesJson.Apply(UWRules.CreateOriginal(), "[1, 2]");
            Check.Str("rules file: a list instead of an object", lOReport.Error, "line 1: expected {");

            // The forgiving part: each bad key is reported and skipped, the good ones count.
            lORules = UWRules.CreateOriginal();
            lOReport = UWRulesJson.Apply(lORules,
                "// a comment\n{\n" +
                "  \"combat.player.easyAttackBonus\": 4, /* inline */\n" +
                "  \"combat.noSuchRule\": 1,\n" +
                "  \"Combat.Player.EasyAttackBonus\": 5,\n" +
                "  \"combat.skillCheck.rollRange\": \"41\",\n" +
                "  \"combat.damage.armedStrengthDivisor\": 0,\n" +
                "  \"combat.damage.unarmedBase\": 7.5,\n" +
                "  \"combat.skillCheck.successAtMost\": [1, 2, 3],\n" +
                "  \"combat.player.missileTarget\": 11,\n" +
                "  \"combat.player.missileTarget\": 12,\n" +
                "}\n");

            Check.Str("rules file: the keys taken", string.Join(", ", lOReport.Applied),
                "combat.player.easyAttackBonus, combat.player.missileTarget");
            Check.Str("rules file: the problems, in the file's order", string.Join(" | ", lOReport.Problems),
                "combat.noSuchRule: unknown key"
                + " | Combat.Player.EasyAttackBonus: unknown key (did you mean combat.player.easyAttackBonus?)"
                + " | combat.skillCheck.rollRange: wants a whole number - the default stays"
                + " | combat.damage.armedStrengthDivisor: must be at least 1 - the default stays"
                + " | combat.damage.unarmedBase: wants a whole number - the default stays"
                + " | combat.skillCheck.successAtMost: wants a whole number - the default stays"
                + " | combat.player.missileTarget: given twice, the last one counts");
            Check.Int("rules file: a taken key holds its value", lORules.Combat.Player.EasyAttackBonus, 4);
            Check.Int("rules file: the last of a doubled key counts", lORules.Combat.Player.MissileTarget, 12);
            Check.Int("rules file: a skipped key keeps its default", lORules.Combat.SkillCheck.RollRange, UWSkillCheck.RollRange);
            Check.Int("rules file: a value below the minimum keeps the default", lORules.Combat.Damage.ArmedStrengthDivisor,
                UWCombat.ArmedStrengthDivisor);
            Check.Str("rules file: the summary", lOReport.Summary, "rules.json: 2 rules taken, 7 problems");

            fCheckRulesTableLength();

            // THE 16-BIT CEILING (2026-10-26, per user: motion.speed.forward 1880 stopped the run):
            // a value above a rule's maximum is reported and the default stays.
            lORules = UWRules.CreateOriginal();
            lOReport = UWRulesJson.Apply(lORules,
                "{ \"motion.speed.forward\": 1880, \"motion.speed.tenthsByState\": [10, 3, 5, 10, 1, 7, 11],"
                + " \"motion.speed.slide\": 400 }");
            Check.Str("rules file: values above the maximum, the problems", string.Join(" | ", lOReport.Problems),
                "motion.speed.forward: must be at most " + UWRulesMotion.SpeedRules.MaxForward + " - the default stays"
                + " | motion.speed.tenthsByState: entry 6 must be at most 10 - the default stays");
            Check.Int("rules file: a value above the maximum keeps the default", lORules.Motion.Speed.Forward,
                UWPlayerMotion.BaseForwardSpeed);
            Check.Int("rules file: the run's ceiling holds in 16 bits",
                (UWPlayerMotion.WalkRun >> 2) * UWRulesMotion.SpeedRules.MaxForward <= short.MaxValue ? 1 : 0, 1);
            Check.Int("rules file: the good key beside them is taken", lORules.Motion.Speed.Slide, 400);

            // A file on disk, and none.
            string lsPath = Path.Combine(Path.GetTempPath(), "uwselfcheck-rules-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                File.WriteAllText(lsPath, "{ \"combat.damage.unarmedBase\": 9 }");
                lORules = UWRules.CreateOriginal();
                lOReport = UWRulesJson.ApplyFile(lORules, lsPath);
                Check.Str("rules file: a file on disk is read", lOReport.Summary + " / " + lORules.Combat.Damage.UnarmedBase,
                    "rules.json: 1 rule taken / 9");
            }
            finally
            {
                File.Delete(lsPath);
            }

            lORules = UWRules.CreateOriginal();
            lOReport = UWRulesJson.ApplyFile(lORules, lsPath);
            Check.Str("rules file: no file is the original's rules", lOReport.Summary, "the original's rules (no rules.json)");
            Check.Bool("rules file: no file is no problem", lOReport.HasProblems, false);
            fExpectSameRules("rules file: no file changes nothing", UWRulesJson.Write(lORules, false), lsOriginal);
        }

        /// <summary>The set on the session: a new one has the original's, two share nothing, Current
        /// forwards, and ApplyRulesFile starts from the original's each time.</summary>
        private static void fCheckRulesSession()
        {
            string lsOriginal = UWRulesJson.Write(UWRules.CreateOriginal(), false);
            UWGameSession lOFirst = new UWGameSession(null, 1);
            UWGameSession lOSecond = new UWGameSession(null, 1);

            fExpectSameRules("rules session: a new session has the original's rules", UWRulesJson.Write(lOFirst.Rules, false), lsOriginal);
            Check.Bool("rules session: two sessions share no rule set", ReferenceEquals(lOFirst.Rules, lOSecond.Rules)
                || ReferenceEquals(lOFirst.Rules.Combat, lOSecond.Rules.Combat), false);

            UWGameSession.MakeCurrent(lOFirst);
            Check.Bool("rules session: UWRules.Current is the current session's", ReferenceEquals(UWRules.Current, lOFirst.Rules), true);

            string lsPath = Path.Combine(Path.GetTempPath(), "uwselfcheck-rules-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                File.WriteAllText(lsPath, "{ \"combat.player.easyAttackBonus\": 5 }");
                lOFirst.ApplyRulesFile(lsPath);
                Check.Int("rules session: ApplyRulesFile takes the file", UWRules.Current.Combat.Player.EasyAttackBonus, 5);

                File.WriteAllText(lsPath, "{ \"combat.player.missileTarget\": 9 }");
                lOFirst.ApplyRulesFile(lsPath);
                Check.Str("rules session: a key taken out of the file goes back to its default",
                    UWRules.Current.Combat.Player.EasyAttackBonus + " / " + UWRules.Current.Combat.Player.MissileTarget,
                    UWPlayerAttack.EasyDifficultyAttackBonus + " / 9");
            }
            finally
            {
                File.Delete(lsPath);
            }

            UWRulesReport lOReport = lOFirst.ApplyRulesFile(lsPath);

            fExpectSameRules("rules session: without the file the original's rules again", UWRulesJson.Write(lOFirst.Rules, false), lsOriginal);
            Check.Bool("rules session: the report is kept on the session", ReferenceEquals(lOFirst.RulesReport, lOReport), true);
        }

        /// <summary>
        /// A CHANGED RULE REACHES THE GAME: one call site per domain, first at the original's values
        /// and then with a rules.json of the current session - so a rule that is declared but still
        /// read from its const somewhere would show here.
        /// </summary>
        private static void fCheckRulesReachTheGame()
        {
            UWGameSession lOSession = new UWGameSession(null, 3);

            UWGameSession.MakeCurrent(lOSession);

            UWRunicMagic.Spell lOCircleThree = new UWRunicMagic.Spell(12, 0, 0, 0);
            UWPlayerVitals lOVitals = new UWPlayerVitals(null);

            lOVitals.PoisonFromToadstool();

            string lsOriginal = string.Join(" / ",
                UWSkillCheck.GetResult(16),
                UWCombat.GetRawBaseDamage(0, true, 18, 10, 0),
                lOCircleThree.ManaCost,
                UWExperience.GetMaximumHealth(20, 5),
                UWExperience.GetNextLevelExperience(1),
                lOVitals.Poison);

            Check.Str("rules reach the game: at the original's values (band, unarmed base, mana, hit points, level, toadstool)",
                lsOriginal, "Success / 11 / 9 / 50 / 500 / 4");

            UWRulesJson.Apply(lOSession.Rules,
                "{ \"combat.skillCheck.failureAtMost\": 20, \"combat.damage.unarmedBase\": 10, \"magic.manaPerCircle\": 4,"
                + " \"experience.maxHealth.base\": 40, \"experience.pointsPerLevelUnit\": 1000, \"vitals.toadstool.poison\": 6,"
                + " \"magic.duration.longDice\": 0, \"magic.duration.longBonus\": 5 }");

            lOVitals = new UWPlayerVitals(null);
            lOVitals.PoisonFromToadstool();

            string lsChanged = string.Join(" / ",
                UWSkillCheck.GetResult(16),
                UWCombat.GetRawBaseDamage(0, true, 18, 10, 0),
                lOCircleThree.ManaCost,
                UWExperience.GetMaximumHealth(20, 5),
                UWExperience.GetNextLevelExperience(1),
                lOVitals.Poison);

            Check.Str("rules reach the game: with a rules.json", lsChanged, "Failure / 17 / 12 / 60 / 1000 / 6");
            Check.Int("rules reach the game: a long spell's duration from the set's dice", UWSpellCasting.RollStability(0x80), 5);
        }

        /// <summary>
        /// THE SAME FOR WAVE 2 (2026-10-26): one call site per domain of critter.*, respawn.* and
        /// motion.*, and the switch that left UWSettings - first at the original's values, then with a
        /// rules.json of the current session. The lava, the Void and the swing's charge step are read
        /// by the Unity hosts (UWPlayerTerrain, UWVoidEffects, UWPlayerAttackInput) and are the user
        /// test's.
        /// </summary>
        private static void fCheckRulesReachTheCreatures(CheckContext pOContext)
        {
            // The data first: loading it begins a session of its own.
            DataImport lOData = pOContext.Data;
            UWGameSession lOSession = new UWGameSession(null, 3);

            UWGameSession.MakeCurrent(lOSession);

            UWTilePos lOPlayerTile = new UWTilePos(10, 10);

            string lsOriginal = string.Join(" / ",
                UWCritterCombat.GetStrongScoreBonus(0),
                UWCritterCombat.HalveOnEasy(5, true),
                UWCritterCombat.ShouldDropCorpse(1, 6),
                UWCritterCombat.GetKillExperience(10, 4, 0),
                UWCritterRules.IsFarFromBoth(10, 1, 10, 1),
                UWPlayerVitals.GetMovingNoise(1f),
                UWRespawnRules.IsNearPlayer(lOPlayerTile, 18, 10),
                UWImpactRules.CausesDamage(4),
                UWImpactRules.ApplyAcrobat(3, 15, true),
                UWArmourProtection.ResistBlowsAddsArmour);

            Check.Str("rules reach the creatures: at the original's values (strong score, easy, corpse, strong kill, far,"
                + " noise, respawn near, impact, Acrobat, Resist Blows)",
                lsOriginal, "7 / 2 / True / 66 / True / 10 / False / True / 1 / False");

            UWRulesJson.Apply(lOSession.Rules,
                "{ \"critter.strong.toHitBase\": 10, \"critter.easyHalvesDamage\": false, \"critter.corpse.chance\": 3,"
                + " \"critter.strong.experienceBase\": 32, \"critter.sense.farTilesSquared\": 200, \"critter.noise.speedFactor\": 20,"
                + " \"respawn.playerNearTiles\": 9, \"motion.impact.damageThreshold\": 5, \"motion.impact.acrobatFull\": 60,"
                + " \"combat.resistBlowsAddsArmour\": true, \"motion.speed.forward\": 1000 }");

            string lsChanged = string.Join(" / ",
                UWCritterCombat.GetStrongScoreBonus(0),
                UWCritterCombat.HalveOnEasy(5, true),
                UWCritterCombat.ShouldDropCorpse(1, 6),
                UWCritterCombat.GetKillExperience(10, 4, 0),
                UWCritterRules.IsFarFromBoth(10, 1, 10, 1),
                UWPlayerVitals.GetMovingNoise(1f),
                UWRespawnRules.IsNearPlayer(lOPlayerTile, 18, 10),
                UWImpactRules.CausesDamage(4),
                UWImpactRules.ApplyAcrobat(3, 15, true),
                UWArmourProtection.ResistBlowsAddsArmour);

            Check.Str("rules reach the creatures: with a rules.json", lsChanged, "10 / 5 / False / 88 / False / 20 / True / False / 2 / True");

            // The motion core reads its speeds when the player is made and placed.
            if (lOData == null || lOData.CommonObjectProperties == null)
            {
                Check.Pass("rules reach the motion core", "no COMOBJ.DAT here, nothing to place");

                return;
            }

            UWPlayerMotion lOPlayer = new UWPlayerMotion(new FakeMotionWorld(), lOData.CommonObjectProperties);

            lOPlayer.PlaceInTile(8, 8);
            Check.Int("rules reach the motion core: a placed player's forward speed from the set", lOPlayer.ForwardSpeed, 1000);
        }

        /// <summary>A table keeps its length: the first table of the set given too short is reported
        /// and keeps its default (none before a domain brings one).</summary>
        private static void fCheckRulesTableLength()
        {
            UWRuleEntry lOTable = UWRules.Entries.FirstOrDefault(lOEntry => lOEntry.ValueType == typeof(int[]));

            if (lOTable == null)
                return;

            UWRules lORules = UWRules.CreateOriginal();
            int[] liDefault = (int[])lOTable.GetValue(lORules);
            UWRulesReport lOReport = UWRulesJson.Apply(lORules, "{ \"" + lOTable.Key + "\": [1, 2, 3] }");

            Check.Str("rules file: a table keeps its length", string.Join(" | ", lOReport.Problems),
                lOTable.Key + ": wants " + liDefault.Length + " numbers, not 3 - the default stays");
            Check.Str("rules file: a table of the wrong length keeps its default", fRuleText(lOTable.GetValue(lORules)),
                fRuleText(lOTable.GetAnchorValue()));
        }

        /// <summary>Another value of the same kind that every rule accepts: one more (a table: each
        /// entry one more), the other switch.</summary>
        /// <summary>Another value: one more, or one less where one more would pass the rule's
        /// maximum.</summary>
        private static object fOtherValue(object pOValue, int piMaximum)
        {
            switch (pOValue)
            {
                case bool lbValue:
                    return !lbValue;

                case int[] liTable:
                    return liTable.Select(liValue => liValue < piMaximum ? liValue + 1 : liValue - 1).ToArray();

                default:
                    return (int)pOValue < piMaximum ? (int)pOValue + 1 : (int)pOValue - 1;
            }
        }

        private static string fRuleText(object pOValue)
        {
            switch (pOValue)
            {
                case null:
                    return "null";

                case bool lbValue:
                    return lbValue ? "true" : "false";

                case int[] liTable:
                    return "[" + string.Join(", ", liTable.Select(liValue => liValue.ToString(CultureInfo.InvariantCulture))) + "]";

                default:
                    return Convert.ToString(pOValue, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Two sets written as files are the same; on a difference the first differing
        /// line (a whole file is too long for the report).</summary>
        private static void fExpectSameRules(string psName, string psActual, string psExpected)
        {
            if (psActual == psExpected)
            {
                Check.Pass(psName, "the same");

                return;
            }

            string[] lsActual = psActual.Split('\n');
            string[] lsExpected = psExpected.Split('\n');

            for (int liAt = 0; liAt < lsActual.Length || liAt < lsExpected.Length; liAt++)
            {
                string lsA = liAt < lsActual.Length ? lsActual[liAt] : "(end)";
                string lsE = liAt < lsExpected.Length ? lsExpected[liAt] : "(end)";

                if (lsA != lsE)
                {
                    Check.Fail(psName, "now " + lsA.Trim() + ", expected " + lsE.Trim());

                    return;
                }
            }
        }

        private static string fReportText(UWRulesReport pOReport)
        {
            return (pOReport.Error ?? string.Empty) + string.Join(" | ", pOReport.Problems);
        }
    }
}
