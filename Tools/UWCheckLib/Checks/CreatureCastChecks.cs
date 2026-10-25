using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.10 (2026-10-18): a creature's spell through the one effect path
    /// (UWSpellCasting.CastSpell with a UWCreatureSpellHost) on the fake world - do_spell's refusal
    /// on an anti-magic square and in the Void (B2), the missile released at once, the area measured
    /// from the creature (B3), the summoning from its spot, and the player's own classes doing
    /// nothing and drawing nothing. Needs the installation's object tables (the projectile ids and
    /// the creature table).
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>Fireball, Flame Wind, Summon Monster, Tremor, a heal and Light - the spells[]
        /// entries creatures hold (OBJECTS.DAT's creature table).</summary>
        private const int CreatureFireballSpell = 0x18;

        private const int CreatureFlameWindSpell = 0x2D;

        private const int CreatureSummonSpell = 0x26;

        private const int CreatureTremorSpell = 0x2B;

        private const int CreatureHealSpell = 0x13;

        private const int CreatureLightSpell = 0x00;

        private static void fCheckCreatureCasting(DataImport pOData)
        {
            Check.Group("Creature casting (cleanup step 4.10)");

            fCheckMagicRefusalRule();

            if (pOData == null || pOData.ObjectProperties == null || pOData.ObjectClassProperties == null)
            {
                fFail("creature casting", "no object tables");

                return;
            }

            UWRandom.Use(new FixedRandomSource(false));

            try
            {
                fCheckCreatureMissile(pOData);
                fCheckCreatureRefusal(pOData);
                fCheckCreatureArea(pOData);
                fCheckCreatureSummon(pOData);
            }
            finally
            {
                UWRandom.Use(null);
            }

            fCheckCreaturePlayerClasses(pOData);
        }

        /// <summary>The one refusal: the anti-magic bit alone for the mind, the bit or the Void for
        /// the cast.</summary>
        private static void fCheckMagicRefusalRule()
        {
            UWLevel lOLevel = fNightWorld().Level;
            UWTilePos lOBlocked = new UWTilePos(5, 5);

            lOLevel.GetTile(lOBlocked.X, lOBlocked.Y).NoMagicAllowed = true;

            fExpectBool("refusal: the anti-magic bit", UWEndgameRules.IsAntiMagicTile(lOLevel, lOBlocked), true);
            fExpectBool("refusal: an open tile has none", UWEndgameRules.IsAntiMagicTile(lOLevel, new UWTilePos(6, 5)), false);
            fExpectBool("refusal: off the map, none", UWEndgameRules.IsAntiMagicTile(lOLevel, new UWTilePos(-1, 70)), false);
            fExpectBool("refusal: the cast is refused in the Void (do_spell, PlayerLevel 9)",
                UWEndgameRules.IsMagicBlocked(lOLevel, UWEndgameRules.VoidLevelIndex, new UWTilePos(6, 5)), true);
            fExpectBool("refusal: ... and on the anti-magic bit",
                UWEndgameRules.IsMagicBlocked(lOLevel, 0, lOBlocked), true);
            fExpectBool("refusal: ... and nowhere else", UWEndgameRules.IsMagicBlocked(lOLevel, 0, new UWTilePos(6, 5)), false);
        }

        /// <summary>A world with data: the player on 10/10 looking north, an open level.</summary>
        private static FakeWorld fCreatureCastWorld(DataImport pOData, int piLevelIndex)
        {
            FakeWorld lOWorld = fCastWorld();

            lOWorld.Data = pOData;
            lOWorld.LevelIndex = piLevelIndex;

            return lOWorld;
        }

        /// <summary>Creature 7 on 20/20, in the middle of its eighth, looking along piHeading; the
        /// missiles it releases go into pOReleased.</summary>
        private static UWCreatureSpellHost fCreatureCaster(FakeWorld pOWorld, int piHeading, List<int> pOReleased)
        {
            return new UWCreatureSpellHost(pOWorld,
                UWCastContext.Creature(7, new UWSpot(new UWTilePos(20, 20), 4, 4, 16), piHeading),
                piProjectile => pOReleased.Add(piProjectile));
        }

        /// <summary>release_missile: the fireball goes off at once, the fireball's object.</summary>
        private static void fCheckCreatureMissile(DataImport pOData)
        {
            List<int> lOReleased = new List<int>();
            FakeWorld lOWorld = fCreatureCastWorld(pOData, 0);
            int liFireball = UWRunicMagic.GetProjectileId(
                UWRunicMagic.GetEffectMinor(UWRunicMagic.AllSpells[CreatureFireballSpell].MinorClass));

            fExpectBool("creature missile: cast", UWSpellCasting.CastSpell(CreatureFireballSpell,
                fCreatureCaster(lOWorld, 0, lOReleased)), true);
            fExpectBool("creature missile: the fireball is released at once",
                lOReleased.Count == 1 && lOReleased[0] == liFireball && liFireball >= 0, true);
            fExpectBool("creature missile: no spell past the list",
                UWSpellCasting.CastSpell(UWRunicMagic.AllSpells.Count, fCreatureCaster(lOWorld, 0, lOReleased)), false);
        }

        /// <summary>B2: nothing a creature casts works in the Void or on an anti-magic square - the
        /// missile, the area and the summoning alike.</summary>
        private static void fCheckCreatureRefusal(DataImport pOData)
        {
            foreach (bool lbVoid in new[] { true, false })
            {
                string lsWhere = lbVoid ? "in the Void" : "on an anti-magic tile";
                List<int> lOReleased = new List<int>();
                FakeWorld lOWorld = fCreatureCastWorld(pOData, lbVoid ? UWEndgameRules.VoidLevelIndex : 0);

                if (!lbVoid)
                    lOWorld.Level.GetTile(20, 20).NoMagicAllowed = true;

                UWCreatureSpellHost lOHost = fCreatureCaster(lOWorld, 0, lOReleased);

                fExpectBool("creature refused " + lsWhere + ": the host says so", lOHost.IsMagicBlocked, true);
                fExpectBool("creature refused " + lsWhere + ": the fireball does not take",
                    UWSpellCasting.CastSpell(CreatureFireballSpell, lOHost), false);
                fExpectBool("creature refused " + lsWhere + ": Flame Wind does not take",
                    UWSpellCasting.CastSpell(CreatureFlameWindSpell, lOHost), false);
                fExpectBool("creature refused " + lsWhere + ": Summon Monster does not take",
                    UWSpellCasting.CastSpell(CreatureSummonSpell, lOHost), false);
                fExpectBool("creature refused " + lsWhere + ": nothing released, struck or made",
                    lOReleased.Count == 0 && lOWorld.Log.Count == 0, true);
            }
        }

        /// <summary>B3: a creature's Flame Wind lies four tiles along ITS heading from ITS tile - east
        /// of 20/20 at 24/20 - and is charged to it; the player's area (10/14) is not touched.</summary>
        private static void fCheckCreatureArea(DataImport pOData)
        {
            List<int> lOReleased = new List<int>();
            FakeWorld lOWorld = fCreatureCastWorld(pOData, 0);
            UWCreatureSpellHost lOHost = fCreatureCaster(lOWorld, 0x40, lOReleased);

            fExpectInt("creature area: the centre is four tiles east of the creature (x)",
                UWAreaSpellRules.GetCentreTile(lOHost).X, 24);
            fExpectInt("creature area: the centre is four tiles east of the creature (y)",
                UWAreaSpellRules.GetCentreTile(lOHost).Y, 20);
            fExpectBool("creature area: Flame Wind takes", UWSpellCasting.CastSpell(CreatureFlameWindSpell, lOHost), true);

            // The lowest rolls: the first open square of the walk, centre - 2 on both axes.
            string lsFirst = "strike 22/18 10d6 type " + UWDamageTypes.Fire + " by 7";

            fExpectBool("creature area: the first stroke from the creature's centre, charged to it (" + lsFirst + ")",
                lOWorld.Log.Contains(lsFirst), true);
            fExpectBool("creature area: every stroke is the creature's",
                lOWorld.Log.FindAll(psLine => psLine.StartsWith("strike")).TrueForAll(psLine => psLine.EndsWith(" by 7")), true);
            fExpectBool("creature area: nothing around the player's centre",
                lOWorld.Log.Exists(psLine => psLine.StartsWith("strike 8/12 ") || psLine.StartsWith("strike 10/14 ")), false);
        }

        /// <summary>A creature's Summon Monster: one creature dropped twelve eighths along its
        /// heading (east of it), none for the player's message channel.</summary>
        private static void fCheckCreatureSummon(DataImport pOData)
        {
            List<int> lOReleased = new List<int>();
            FakeWorld lOWorld = fCreatureCastWorld(pOData, 6);

            fExpectBool("creature summons: cast", UWSpellCasting.CastSpell(CreatureSummonSpell,
                fCreatureCaster(lOWorld, 0x40, lOReleased)), true);

            List<string> lODrops = lOWorld.Log.FindAll(psLine => psLine.StartsWith("drop "));
            float lfCreatureX = UWUnits.SubTileToWorldAxis(20, 4);
            bool lbEast = lODrops.Count == 1 && float.Parse(lODrops[0].Substring(lODrops[0].IndexOf(" at ") + 4).Split('/')[0],
                System.Globalization.CultureInfo.InvariantCulture) > lfCreatureX;

            fExpectBool("creature summons: one creature, east of the caster", lbEast, true);
            fExpectInt("creature summons: no message", lOWorld.Shown.Log.Count, 0);
        }

        /// <summary>The player's own classes - Tremor (xt_spells), a heal with no target, Light -
        /// do nothing for a creature and draw nothing.</summary>
        private static void fCheckCreaturePlayerClasses(DataImport pOData)
        {
            CountingRandomSource lOCounter = new CountingRandomSource();

            UWRandom.Use(lOCounter);

            try
            {
                List<int> lOReleased = new List<int>();
                FakeWorld lOWorld = fCreatureCastWorld(pOData, 0);

                foreach (int liSpell in new[] { CreatureTremorSpell, CreatureHealSpell, CreatureLightSpell })
                    UWSpellCasting.CastSpell(liSpell, fCreatureCaster(lOWorld, 0, lOReleased));

                fExpectInt("creature's Tremor, heal and Light: no draw", lOCounter.Draws, 0);
                fExpectBool("creature's Tremor, heal and Light: nothing happens",
                    lOWorld.Log.Count == 0 && lOWorld.Shown.Log.Count == 0 && lOReleased.Count == 0, true);
            }
            finally
            {
                UWRandom.Use(null);
            }
        }
    }
}
