using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.0 (2026-10-18, decision 11 of Docs/CLEANUP-PLAN.md): who an area spell or a
    /// blast strikes comes from the squares and their object lists, as process_area and
    /// damage_square choose it (UnderworldExhumed SPELLS.C) - no physics. Checked on the fake
    /// world's level: the walk order (x, then y, then the list), the square reaching centre +
    /// radius + 1, the caster left out, the count that ends the walk in the middle of a square,
    /// the class 7 spell on the first creature, AREA_RANDOM's roll, its five passes and the
    /// clipping at the map's edge, and the blast's order on one square. On the real level 1: the
    /// tile lists hold the creatures, and the caster's own index is left out.
    /// </summary>
    public static partial class Program
    {
        /// <summary>A scripted source: the values in order, then the highest of each range; every
        /// range asked for is noted.</summary>
        private sealed class ScriptedAreaRandom : IUWRandom
        {
            private readonly Queue<int> mOValues;

            public readonly List<int> Ranges = new List<int>();

            public ScriptedAreaRandom(params int[] piValues)
            {
                mOValues = new Queue<int>(piValues);
            }

            public int Next(int piMaxExclusive)
            {
                Ranges.Add(piMaxExclusive);

                return mOValues.Count > 0 ? mOValues.Dequeue() : System.Math.Max(0, piMaxExclusive - 1);
            }

            public int Next(int piMin, int piMaxExclusive)
            {
                return piMin + Next(piMaxExclusive - piMin);
            }

            public double NextDouble()
            {
                return 0d;
            }
        }

        private static void fCheckAreaWalk()
        {
            Check.Group("Area walk (UWAreaWalk: process_area, damage_square - cleanup step 4.0)");

            fCheckAreaWalkOrder();
            fCheckAreaWalkCount();
            fCheckAreaWalkClassSeven();
            fCheckAreaWalkConfusionCount();
            fCheckAreaWalkRandom();
            fCheckAreaWalkMapEdge();
            fCheckDamageSquare();
        }

        /// <summary>The fake world of a cast with a handler that notes every creature it is handed
        /// and never counts, so the whole walk shows.</summary>
        private static List<string> fWalkAll(FakeWorld pOWorld, UWTilePos pOCentre, int piRadius, int piType)
        {
            List<string> lOSeen = new List<string>();

            UWAreaWalk.Square(pOWorld.Level, pOCentre, piRadius, 99, UWCritterBrain.PlayerIndex, piType,
                (pOTile, pOTarget) =>
                {
                    lOSeen.Add(pOWorld.NameOf(pOTarget) + "@" + pOTile.X + "/" + pOTile.Z);

                    return false;
                });

            return lOSeen;
        }

        /// <summary>x ascending, within it y ascending, within a square the list order; the square
        /// from centre - 2 to centre + 3 both ways; AREA_CRITTERS hands over no item.</summary>
        private static void fCheckAreaWalkOrder()
        {
            FakeWorld lOWorld = fCastWorld();

            lOWorld.PutCreature(21, 19, "A");
            lOWorld.PutCreature(19, 21, "B");
            lOWorld.PutCreature(19, 19, "C");
            lOWorld.PutCreature(20, 20, "D");

            UWObject lOItem = new UWObject(0x01) { TileX = 20, TileY = 20 };

            lOWorld.Level.GetTile(20, 20).ObjectsInTile.Add(lOItem);
            lOWorld.Names[lOItem] = "item";
            lOWorld.PutCreature(20, 20, "E");
            lOWorld.PutCreature(23, 23, "F");
            lOWorld.PutCreature(17, 20, "G");
            lOWorld.PutCreature(20, 17, "H");

            List<string> lOSeen = fWalkAll(lOWorld, new UWTilePos(20, 20), 2, UWAreaWalk.Critters);

            fExpectBool("area walk: creatures by x, then y, then the square's list (" + string.Join(", ", lOSeen) + ")",
                string.Join(", ", lOSeen) == "C@19/19, B@19/21, D@20/20, E@20/20, A@21/19, F@23/23", true);
            fExpectBool("area walk: centre - 3 lies outside, on both axes",
                !lOSeen.Exists(psSeen => psSeen.StartsWith("G@") || psSeen.StartsWith("H@")), true);

            lOSeen = fWalkAll(lOWorld, new UWTilePos(20, 20), 2, UWAreaWalk.Objects);

            fExpectBool("area walk: AREA_OBJECTS hands over the item in its place in the list",
                lOSeen.IndexOf("item@20/20") == lOSeen.IndexOf("D@20/20") + 1
                && lOSeen.IndexOf("E@20/20") == lOSeen.IndexOf("item@20/20") + 1, true);
        }

        /// <summary>The count ends the walk at once, in the middle of a square; a call that does not
        /// take effect uses none; the caster's own index is no target (shown on level 1 below).</summary>
        private static void fCheckAreaWalkCount()
        {
            FakeWorld lOWorld = fCastWorld();

            lOWorld.PutCreature(20, 20, "D");
            lOWorld.PutCreature(20, 20, "E");
            lOWorld.PutCreature(20, 20, "F");
            lOWorld.PutCreature(21, 20, "G");

            List<string> lOHit = new List<string>();

            UWAreaWalk.Square(lOWorld.Level, new UWTilePos(20, 20), 2, 2, UWCritterBrain.PlayerIndex, UWAreaWalk.Critters,
                (pOTile, pOTarget) =>
                {
                    lOHit.Add(lOWorld.NameOf(pOTarget));

                    // D finds nothing to act on and passes it on (Smite Undead on the living).
                    return lOWorld.NameOf(pOTarget) != "D";
                });

            fExpectBool("area walk: count 2 ends after E and F, inside the square (" + string.Join(", ", lOHit) + ")",
                string.Join(", ", lOHit) == "D, E, F", true);
        }

        /// <summary>A class 7 spell (count 1): the first creature in the walk takes it; one that
        /// finds nothing to act on passes it on; of two on one square the first in the list; the
        /// tile right in front of the player lies outside the area.</summary>
        private static void fCheckAreaWalkClassSeven()
        {
            // The player on 10/10 looking north: the centre is 10/14, the square 8..13 / 12..17.
            FakeWorld lOWorld = fCastWorld();

            lOWorld.PutCreature(10, 11, "near");
            lOWorld.PutCreature(10, 13, "first");
            lOWorld.PutCreature(10, 13, "second");

            UWAreaSpellRules.CastTargetSpellInArea(4, new FakeSpellHost(lOWorld, UWCastContext.ThePlayer));

            fExpectBool("class 7: of two on one square the first in the list takes it",
                lOWorld.Log.Contains("target spell 4 on first") && !lOWorld.Log.Contains("target spell 4 on second"), true);
            fExpectBool("class 7: the tile right in front of the player is not in the area",
                lOWorld.Log.Contains("target spell 4 on near"), false);

            // Smite Undead (minor 2) on a living creature: Missed, the next one takes it.
            lOWorld = fCastWorld();
            UWNpc lOLiving = lOWorld.PutCreature(9, 14, "living");

            lOWorld.PutCreature(11, 14, "undead");
            lOWorld.Answers[lOLiving] = UWTargetSpellRules.Result.Missed;

            UWAreaSpellRules.CastTargetSpellInArea(2, new FakeSpellHost(lOWorld, UWCastContext.ThePlayer));

            fExpectInt("class 7: Smite Undead passes the living on to the undead behind it",
                fIndexOfSequence(lOWorld.Log, "target spell 2 on living", "target spell 2 on undead"), 0);
            fExpectBool("class 7: ... the living one shows no picture", lOWorld.Log.Contains("effect 455 at living"), false);

            // A resisted spell (Spent) still uses the cast: nobody after it.
            lOWorld = fCastWorld();
            UWNpc lOResists = lOWorld.PutCreature(9, 14, "resists");

            lOWorld.PutCreature(11, 14, "behind");
            lOWorld.Answers[lOResists] = UWTargetSpellRules.Result.Spent;

            UWAreaSpellRules.CastTargetSpellInArea(5, new FakeSpellHost(lOWorld, UWCastContext.ThePlayer));

            fExpectBool("class 7: a resisted spell is used up on the first",
                lOWorld.Log.Contains("target spell 5 on resists") && !lOWorld.Log.Contains("target spell 5 on behind"), true);
        }

        /// <summary>Confusion's count is 3d4 creatures: with the lowest roll three of five on one
        /// square, the first three of the list.</summary>
        private static void fCheckAreaWalkConfusionCount()
        {
            UWRandom.Use(new FixedRandomSource(false));

            try
            {
                FakeWorld lOWorld = fCastWorld();

                foreach (string lsName in new[] { "c1", "c2", "c3", "c4", "c5" })
                    lOWorld.PutCreature(20, 20, lsName);

                UWAreaSpellRules.CastAt(3, UWAreaWalk.Critters, new UWTilePos(20, 20),
                    new FakeSpellHost(lOWorld, UWCastContext.ThePlayer));

                fExpectBool("confusion: 3d4 at its lowest confuses the first three of the square",
                    lOWorld.Log.Contains("confuse c3") && !lOWorld.Log.Contains("confuse c4"), true);
                fExpectInt("confusion: ... three in all", lOWorld.Log.FindAll(psLine => psLine.StartsWith("confuse")).Count, 3);
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>AREA_RANDOM: a square is struck when rand() % (w * h + 3) lies below the count
        /// left; solid squares draw no roll; up to five passes; the count ends the walk.</summary>
        private static void fCheckAreaWalkRandom()
        {
            FakeWorld lOWorld = fCastWorld();
            ScriptedAreaRandom lORandom = new ScriptedAreaRandom();
            List<string> lOStruck = new List<string>();

            UWRandom.Use(lORandom);

            try
            {
                // Radius 1: w = h = 3, the walk 19..22 on both axes (16 squares), the roll % 12.
                UWAreaWalk.Square(lOWorld.Level, new UWTilePos(20, 20), 1, 2, UWCritterBrain.PlayerIndex,
                    UWAreaWalk.RandomSquares, (pOTile, pOTarget) => { lOStruck.Add(pOTile.X + "/" + pOTile.Z); return true; });

                fExpectInt("AREA_RANDOM: nothing below the count - five passes of 16 rolls", lORandom.Ranges.Count, 80);
                fExpectBool("AREA_RANDOM: each roll % (w * h + 3) = 12", lORandom.Ranges.TrueForAll(piRange => piRange == 12), true);
                fExpectInt("AREA_RANDOM: ... and nothing struck", lOStruck.Count, 0);

                // Rolls 1 and 0: 1 below the two left, then 0 below the one left - the count ends
                // the walk.
                lORandom = new ScriptedAreaRandom(1, 0);
                lOStruck.Clear();
                lOWorld.Level.GetTile(19, 19).TileType = UWTile.TileTypeEnum.solid;
                UWRandom.Use(lORandom);

                UWAreaWalk.Square(lOWorld.Level, new UWTilePos(20, 20), 1, 2, UWCritterBrain.PlayerIndex,
                    UWAreaWalk.RandomSquares, (pOTile, pOTarget) => { lOStruck.Add(pOTile.X + "/" + pOTile.Z); return true; });

                fExpectBool("AREA_RANDOM: the solid square draws no roll, the next two are struck (" + string.Join(", ", lOStruck) + ")",
                    string.Join(", ", lOStruck) == "19/20, 19/21", true);
                fExpectInt("AREA_RANDOM: the count ends the walk at once", lORandom.Ranges.Count, 2);

                // A stroke that does not take effect uses no count: the second pass goes on.
                lORandom = new ScriptedAreaRandom(0);
                lOStruck.Clear();
                UWRandom.Use(lORandom);

                UWAreaWalk.Square(lOWorld.Level, new UWTilePos(20, 20), 1, 1, UWCritterBrain.PlayerIndex,
                    UWAreaWalk.RandomSquares, (pOTile, pOTarget) => { lOStruck.Add(pOTile.X + "/" + pOTile.Z); return false; });

                fExpectInt("AREA_RANDOM: a stroke without effect leaves the count, all five passes run",
                    lORandom.Ranges.Count, 75);
                fExpectInt("AREA_RANDOM: ... the one low roll struck once", lOStruck.Count, 1);
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>process_area clips the area to the map first; the clipped size is the roll's,
        /// and the walk still runs to x0 + w inclusive.</summary>
        private static void fCheckAreaWalkMapEdge()
        {
            FakeWorld lOWorld = fCastWorld();
            ScriptedAreaRandom lORandom = new ScriptedAreaRandom();

            UWRandom.Use(lORandom);

            try
            {
                // Centre 62/62, radius 2: x0 60 + 5 = 65 clips both sizes to 4; the walk 60..63.
                UWAreaWalk.Square(lOWorld.Level, new UWTilePos(62, 62), 2, 1, UWCritterBrain.PlayerIndex,
                    UWAreaWalk.RandomSquares, (pOTile, pOTarget) => false);

                fExpectInt("area walk at the far edge: both sizes clipped to 4 - the roll % 19",
                    lORandom.Ranges.Count > 0 ? lORandom.Ranges[0] : -1, 19);
                fExpectInt("area walk at the far edge: 60..63 on both axes, five passes", lORandom.Ranges.Count, 5 * 16);

                // Centre 1/1, radius 2: x0 and y0 -1 clip both to 0 and the sizes to 4.
                lORandom = new ScriptedAreaRandom();
                UWRandom.Use(lORandom);

                UWAreaWalk.Square(lOWorld.Level, new UWTilePos(1, 1), 2, 1, UWCritterBrain.PlayerIndex,
                    UWAreaWalk.RandomSquares, (pOTile, pOTarget) => false);

                fExpectInt("area walk at the corner 0/0: both sizes 4 - the roll % 19",
                    lORandom.Ranges.Count > 0 ? lORandom.Ranges[0] : -1, 19);
                fExpectInt("area walk at the corner 0/0: 0..4 on both axes, five passes", lORandom.Ranges.Count, 5 * 25);
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>damage_square: the player first, then the list as it was - what leaves the square
        /// meanwhile is passed over, what joins it is not struck.</summary>
        private static void fCheckDamageSquare()
        {
            FakeWorld lOWorld = fCastWorld();
            UWTile lOTile = lOWorld.Level.GetTile(20, 20);

            lOWorld.PutCreature(20, 20, "D");

            UWObject lOItem = new UWObject(0x01) { TileX = 20, TileY = 20 };

            lOTile.ObjectsInTile.Add(lOItem);
            lOWorld.Names[lOItem] = "item";
            lOWorld.PutCreature(20, 20, "E");

            List<string> lOStruck = new List<string>();

            UWAreaWalk.DamageSquare(lOTile, () => lOStruck.Add("player"), pOObject =>
            {
                lOStruck.Add(lOWorld.NameOf(pOObject));

                // D dies at once: its remains join the square, and the item burns with it.
                if (lOWorld.NameOf(pOObject) == "D")
                {
                    lOTile.ObjectsInTile.Remove(lOItem);
                    lOTile.ObjectsInTile.Add(new UWObject(0x01) { TileX = 20, TileY = 20 });
                }
            });

            fExpectBool("damage_square: the player, then the list; gone ones passed over, new ones not struck ("
                + string.Join(", ", lOStruck) + ")", string.Join(", ", lOStruck) == "player, D, E", true);
        }

        /// <summary>On level 1 as loaded: the tile lists hold the creatures, the walk finds the one
        /// on a square, and leaves it out when it is the caster.</summary>
        private static void fCheckAreaWalkOnLevel(DataImport pOData)
        {
            Check.Group("Area walk on level 1 (cleanup step 4.0)");

            UWLevel lOLevel = pOData != null && pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;

            if (lOLevel == null || lOLevel.TileData == null)
            {
                Check.Bool("area walk on level 1: the level loads", false, true);

                return;
            }

            UWTile lOTile = null;
            UWObject lOCreature = null;

            foreach (UWTile lOCandidate in lOLevel.TileData)
            {
                lOCreature = lOCandidate != null && lOCandidate.ObjectsInTile != null
                    ? lOCandidate.ObjectsInTile.Find(pOObject => pOObject != null
                        && UWObjectIds.Major(pOObject.ID) == UWObjectIds.CreatureMajorClass)
                    : null;

                if (lOCreature != null)
                {
                    lOTile = lOCandidate;

                    break;
                }
            }

            fExpectBool("area walk on level 1: a tile list holds a creature", lOCreature != null, true);

            if (lOCreature == null)
                return;

            int liIndex = UWAreaWalk.MobileIndexOf(lOLevel, lOCreature);
            UWTilePos lOAt = new UWTilePos(lOTile.X, lOTile.Z);
            List<UWObject> lOFound = new List<UWObject>();
            UWAreaWalk.Handler lONote = (pOTileData, pOTarget) =>
            {
                lOFound.Add(pOTarget);

                return false;
            };

            fExpectBool("area walk on level 1: the creature has a mobile index (" + liIndex + ")",
                liIndex > UWCritterBrain.PlayerIndex && liIndex < 256, true);

            UWAreaWalk.Square(lOLevel, lOAt, 0, 99, UWCritterBrain.PlayerIndex, UWAreaWalk.Critters, lONote);
            fExpectBool("area walk on level 1: the player's walk finds it", lOFound.Contains(lOCreature), true);

            lOFound.Clear();
            UWAreaWalk.Square(lOLevel, lOAt, 0, 99, liIndex, UWAreaWalk.Critters, lONote);
            fExpectBool("area walk on level 1: its own walk leaves it out", lOFound.Contains(lOCreature), false);
        }
    }
}
