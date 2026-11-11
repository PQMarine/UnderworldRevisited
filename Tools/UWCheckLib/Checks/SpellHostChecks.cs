using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 3.6 (2026-10-17): the spell rules on the fake world. The spell host shrank to
    /// the caster (FakeSpellHost below), the creature caster a value (UWCastContext). Checked here
    /// for the first time without a scene: Confusion's walk over the targets, Sheet Lightning's
    /// strikes charged to the caster, the class 7 spell on the first creature in process_area's
    /// order, Detect Monsters on the world's creatures, and Tremor's boulders, shake and rumble.
    /// Since cleanup step 4.0 (2026-10-18) the area spells find the creatures in the fake level's
    /// tile lists (FakeWorld.PutCreature) instead of a list of physics hits; the walk itself is
    /// checked in AreaWalkChecks.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckSpellHost()
        {
            Check.Group("Spells on the world (cleanup step 3.6)");

            fCheckCastContext();

            UWRandom.Use(new FixedRandomSource(true));

            try
            {
                fCheckAreaConfusion();
                fCheckTargetSpellInArea();
                fCheckDetectOnWorld();
            }
            finally
            {
                UWRandom.Use(null);
            }

            UWRandom.Use(new FixedRandomSource(false));

            try
            {
                fCheckSheetLightningCaster();
                fCheckTremorOnWorld();
                fCheckAreaSpellsUw2();
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>The caster as a value: the player's blasts are his, a creature's its own.</summary>
        private static void fCheckCastContext()
        {
            fExpectBool("cast: the player is no creature", UWCastContext.ThePlayer.ByCreature, false);
            fExpectInt("cast: the player's blasts are charged to him", UWCastContext.ThePlayer.AttackerIndex,
                UWCritterBrain.PlayerIndex);
            fExpectBool("cast: a creature casts", UWCastContext.Creature(7, UWSpot.Nowhere, 0).ByCreature, true);
            fExpectInt("cast: a creature's blasts are charged to it", UWCastContext.Creature(7, UWSpot.Nowhere, 0).AttackerIndex, 7);
        }

        /// <summary>
        /// THE SECOND GAME'S AREA SPELLS (UWAreaSpellRulesUw2, stage 2 part B, 2026-11-11) with every roll at 0: Shockwave
        /// around the player strikes the creature beside him with Casting / 2 + 15 of magic, not the one far off, and
        /// splashes the empty squares; Flame Wind strikes one square per stroke, its count of 10 (no cross); Frost
        /// gives the creature 10 of magic and cold and lays frost on the empty squares without using its count.
        /// </summary>
        private static void fCheckAreaSpellsUw2()
        {
            FakeWorld lOWorld = fCastWorld();
            FakeSpellHost lOHost = new FakeSpellHost(lOWorld, UWCastContext.ThePlayer);

            lOWorld.PutCreature(11, 10, "A");
            lOWorld.PutCreature(15, 15, "B");
            UWAreaSpellRulesUw2.Cast(0x86, lOHost);

            fExpectInt("uw2 shockwave: the creature beside the player takes Casting / 2 + 15 of magic",
                lOWorld.Blows.FindAll(psBlow => psBlow.EndsWith("takes 30 type 3 by " + UWCritterBrain.PlayerIndex)).Count, 1);
            fExpectBool("uw2 shockwave: the empty squares splash", lOWorld.Shown.Log.Exists(psLine => psLine.StartsWith("effect 459 ")), true);

            lOWorld = fCastWorld();
            lOHost = new FakeSpellHost(lOWorld, UWCastContext.ThePlayer);
            UWAreaSpellRulesUw2.Cast(0x84, lOHost);

            fExpectInt("uw2 flame wind: ten strokes of 10d6 fire, each on its own square",
                lOWorld.Log.FindAll(psLine => psLine.StartsWith("strike ") && psLine.Contains(" 10d6 type 11")).Count, 10);

            lOWorld = fCastWorld();
            lOHost = new FakeSpellHost(lOWorld, UWCastContext.ThePlayer);
            lOWorld.PutCreature(10, 13, "F");
            UWAreaSpellRulesUw2.Cast(0x87, lOHost);

            fExpectInt("uw2 frost: the creature takes 10 of magic and cold",
                lOWorld.Blows.FindAll(psBlow => psBlow.EndsWith("takes 10 type 35 by " + UWCritterBrain.PlayerIndex)).Count, 1);
            fExpectBool("uw2 frost: frost on the empty squares", lOWorld.Shown.Log.Exists(psLine => psLine.StartsWith("effect 458 ")), true);
        }

        /// <summary>A world for one cast: the player on 10/10 looking north on an open level.</summary>
        private static FakeWorld fCastWorld()
        {
            FakeWorld lOWorld = fNightWorld();

            lOWorld.Body.Floor = new UWSpot(new UWTilePos(10, 10), 4, 4, 16);

            return lOWorld;
        }

        /// <summary>Confusion (minor 3, creatures only) at 20/20: the creature in the area is
        /// confused and shows the sparkle; an object in the area and a creature outside it are
        /// left.</summary>
        private static void fCheckAreaConfusion()
        {
            FakeWorld lOWorld = fCastWorld();
            UWObject lOItem = new UWObject(0x01) { TileX = 20, TileY = 20 };

            lOWorld.PutCreature(19, 20, "A");
            lOWorld.Level.GetTile(20, 20).ObjectsInTile.Add(lOItem);
            lOWorld.Names[lOItem] = "B";
            lOWorld.PutCreature(30, 30, "C");

            fExpectBool("confusion: cast", UWAreaSpellRules.CastAt(3, 0x00, new UWTilePos(20, 20),
                new FakeSpellHost(lOWorld, UWCastContext.ThePlayer)), true);
            fExpectInt("confusion: the creature in the area is confused and sparkles",
                fIndexOfSequence(lOWorld.Log, "confuse A", "effect 455 at A"), 0);
            fExpectBool("confusion: the object in the area is left", lOWorld.Log.Contains("confuse B"), false);
            fExpectBool("confusion: the creature outside is left", lOWorld.Log.Contains("confuse C"), false);

            // Resisted: it counts, without the picture.
            lOWorld = fCastWorld();
            lOWorld.TargetAnswer = UWTargetSpellRules.Result.Spent;
            lOWorld.PutCreature(19, 20, "A");
            UWAreaSpellRules.CastAt(3, 0x00, new UWTilePos(20, 20), new FakeSpellHost(lOWorld, UWCastContext.ThePlayer));
            fExpectBool("confusion: resisted, no sparkle", lOWorld.Log.Contains("confuse A")
                && !lOWorld.Log.Contains("effect 455 at A"), true);
        }

        /// <summary>A class 7 spell in the area (process_area's order): of two creatures on one row
        /// the western one takes it, and only one.</summary>
        private static void fCheckTargetSpellInArea()
        {
            FakeWorld lOWorld = fCastWorld();
            FakeSpellHost lOHost = new FakeSpellHost(lOWorld, UWCastContext.ThePlayer);
            UWTilePos lOCentre = UWAreaSpellRules.GetCentreTile(lOHost);

            lOWorld.PutCreature(lOCentre.X + 1, lOCentre.Y, "E");
            lOWorld.PutCreature(lOCentre.X - 1, lOCentre.Y, "W");

            fExpectBool("target spell in the area: cast", UWAreaSpellRules.CastTargetSpellInArea(6, lOHost), true);
            fExpectBool("target spell in the area: the western creature takes it",
                lOWorld.Log.Contains("target spell 6 on W") && lOWorld.Log.Contains("effect 455 at W"), true);
            fExpectBool("target spell in the area: exactly one", lOWorld.Log.Contains("target spell 6 on E"), false);
            fExpectInt("target spell in the area: the centre is four tiles north of 10/10", lOCentre.Y, 14);
        }

        /// <summary>Detect Monsters asks the world's creature search: nobody there, "no activity".</summary>
        private static void fCheckDetectOnWorld()
        {
            FakeWorld lOWorld = fCastWorld();

            fExpectBool("detect: cast", UWMiscSpellRules.Track(new FakeSpellHost(lOWorld, UWCastContext.ThePlayer), 0x2D), true);
            fExpectBool("detect: nobody near says so", lOWorld.Shown.Log.Contains("general 63"), true);
        }

        /// <summary>Sheet Lightning (minor 2, tiles) with the lowest rolls: three strokes on the
        /// first open tiles of the area, 6d5 pure magic each, charged to the caster.</summary>
        private static void fCheckSheetLightningCaster()
        {
            FakeWorld lOWorld = fCastWorld();

            UWAreaSpellRules.CastAt(2, 0x40, new UWTilePos(20, 20), new FakeSpellHost(lOWorld, UWCastContext.ThePlayer));

            string lsFirst = "strike 18/18 6d5 type " + UWDamageTypes.Magic + " by " + UWCritterBrain.PlayerIndex;

            fExpectBool("sheet lightning: the player's stroke is his (" + lsFirst + ")", lOWorld.Log.Contains(lsFirst), true);
            fExpectInt("sheet lightning: three strokes with the lowest roll",
                lOWorld.Log.FindAll(psLine => psLine.StartsWith("strike")).Count, 3);
            fExpectInt("sheet lightning: a flash for each", lOWorld.Shown.Log.FindAll(psLine => psLine.StartsWith("effect 453")).Count, 3);

            lOWorld = fCastWorld();
            UWAreaSpellRules.CastAt(2, 0x40, new UWTilePos(20, 20), new FakeSpellHost(lOWorld, UWCastContext.Creature(7, UWSpot.Nowhere, 0)));
            fExpectBool("sheet lightning: a creature's stroke is charged to it",
                lOWorld.Log.Contains("strike 18/18 6d5 type " + UWDamageTypes.Magic + " by 7"), true);
        }

        /// <summary>Tremor: boulders through the world, then the shake and the rumble.</summary>
        private static void fCheckTremorOnWorld()
        {
            FakeWorld lOWorld = fCastWorld();

            fExpectBool("tremor: cast", UWMiscSpellRules.Cast(UWMiscSpellRules.TremorSpell, 0,
                new FakeSpellHost(lOWorld, UWCastContext.ThePlayer)), true);
            fExpectBool("tremor: boulders fall", lOWorld.Log.Exists(psLine => psLine.StartsWith("boulder")), true);
            fExpectInt("tremor: then the shake (0x28) and the rumble (0x12)",
                fIndexOfSequence(lOWorld.Shown.Log, "shake 40", "sound 18"), 0);
        }
    }
}
