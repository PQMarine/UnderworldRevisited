using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE SECOND GAME'S TARGET SPELLS ON A CREATURE (UWTargetSpellRulesUw2, UW2 plan G1 part D1, 2026-11-12): the
        /// handlers of area1_spells 0..6 on a fake world with the data's creatures - what they deal, set and show, and
        /// whether they took (the mana is paid only then).
        /// </summary>
        private static void fCheckTargetSpellsUw2(DataImport pOData)
        {
            Check.Group("UW2 target spells (UWTargetSpellRulesUw2 - part D1)");

            int liBleeder = -1;
            int liBloodless = -1;

            for (int liId = UWObjectIds.FirstCreature; liId < UWObjectIds.FirstCreature + 0x3F; liId++)
            {
                if (!pOData.ObjectClassProperties.TryGetCritter(liId, out UWObjectClassProperties.Critter lORow)
                    || !pOData.CommonObjectProperties.TryGet(liId, out UWCommonObjectProperties.Entry lOEntry))
                    continue;

                bool lbPlain = !UWDamageTypes.IsUndead(lOEntry.Resistances) && (lOEntry.Resistances & 3) == 0;

                if (liBleeder < 0 && lbPlain && lORow.BleedsOnHit)
                    liBleeder = liId;
                else if (liBloodless < 0 && lbPlain && !lORow.BleedsOnHit)
                    liBloodless = liId;
            }

            fExpectBool("uw2 target: the data have a bleeding and a bloodless creature without resistances", liBleeder > 0 && liBloodless > 0, true);

            UWRandom.Use(new FixedRandomSource(false));

            try
            {
                FakeWorld lOWorld = fCastWorld();
                FakeSpellHost lOHost = new FakeSpellHost(lOWorld, UWCastContext.ThePlayer);
                string lsBy = " by " + UWCritterBrain.PlayerIndex;

                lOWorld.Data = pOData;

                UWNpc lOA = lOWorld.PutCreature(10, 11, "A", liBleeder);
                UWNpc lOB = lOWorld.PutCreature(10, 12, "B", liBloodless);

                fExpectBool("uw2 bleeding: takes on a bleeding creature", UWTargetSpellRulesUw2.Cast(UWTargetSpellRulesUw2.Bleeding, lOA, lOHost), true);
                fExpectBool("uw2 bleeding: Casting / 2 + 10 of type 4, wound_foe's picture at two eighths",
                    lOWorld.Blows.Exists(psBlow => psBlow.EndsWith("takes 25 type 4" + lsBy))
                    && lOWorld.Log.Exists(psLine => psLine.StartsWith("effect " + UWTargetSpellRulesUw2.WoundEffectId + " at A eighths 2")), true);
                fExpectBool("uw2 bleeding: a bloodless creature does not take it (the mana stays)",
                    UWTargetSpellRulesUw2.Cast(UWTargetSpellRulesUw2.Bleeding, lOB, lOHost), false);
                fExpectBool("uw2 smite foe: Casting * 3 + 110 of type 4",
                    UWTargetSpellRulesUw2.Cast(UWTargetSpellRulesUw2.SmiteFoe, lOA, lOHost)
                    && lOWorld.Blows.Exists(psBlow => psBlow.EndsWith("takes 200 type 4" + lsBy)), true);

                lOWorld.Log.Clear();
                fExpectBool("uw2 poison: takes, with the sparkle and poison and magic",
                    UWTargetSpellRulesUw2.Cast(UWTargetSpellRulesUw2.Poison, lOB, lOHost)
                    && lOWorld.Blows.Exists(psBlow => psBlow.Contains(" type " + UWTargetSpellRules.PoisonDamageType + lsBy)), true);

                lOWorld.Log.Clear();
                fExpectBool("uw2 cause fear: the attitude at once, then goal 6 for target 1",
                    UWTargetSpellRulesUw2.Cast(UWTargetSpellRulesUw2.CauseFear, lOA, lOHost)
                    && fIndexOfSequence(lOWorld.Log, "goal -1 gtarg -1 attitude 1 on A", "goal 6 gtarg 1 attitude -1 on A") == 0, true);

                lOWorld.Log.Clear();
                fExpectBool("uw2 paralyze: held, goal 15 for 16 + (rand & 15) * Casting / 3, attitude 1",
                    UWTargetSpellRulesUw2.Cast(UWTargetSpellRulesUw2.Paralyze, lOA, lOHost)
                    && lOWorld.Log.Exists(psLine => psLine.StartsWith("goal 15 gtarg ") && psLine.EndsWith(" attitude 1 on A")), true);

                fExpectBool("uw2 smite undead: a living creature does not take it", UWTargetSpellRulesUw2.Cast(UWTargetSpellRulesUw2.SmiteUndead, lOA, lOHost), false);
                fExpectBool("uw2 target: an object is no target", UWTargetSpellRulesUw2.Cast(UWTargetSpellRulesUw2.Poison, new UWObject(0x01), lOHost), false);
                fExpectBool("uw2 target: built 0 to 6, Study Monster (7) and the object spells later",
                    UWTargetSpellRulesUw2.IsBuilt(0) && UWTargetSpellRulesUw2.IsBuilt(6) && !UWTargetSpellRulesUw2.IsBuilt(7) && !UWTargetSpellRulesUw2.IsBuilt(8), true);
            }
            finally
            {
                UWRandom.Use(null);
            }

            // remove_opponent: a fighter leaves, and with one or none left the player is out of the pits.
            UWPlayerFile lOFile = UWPlayerRecordUw2.NewWalker();

            lOFile.Record[UWPlayerRecordUw2.PitsByteOffset] |= UWPlayerRecordUw2.PitsBit;
            lOFile.Record[0x360] = 20;
            lOFile.Record[0x361] = 21;
            fExpectBool("uw2 charm: remove_opponent takes the fighter out, the pits stay with two counted",
                UWPlayerRecordUw2.RemovePitOpponent(lOFile, 20) && lOFile.Record[0x360] == 0 && lOFile.Record[0x361] == 21, true);
        }
    }
}
