using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE NIGHT (player_sleep, SKILLS.C; 2026-10-15, reference audit round 2): what an undisturbed
    /// night does in which order, what passing out does on water and lava, how long the night's
    /// black lasts. Since cleanup step 3.6 (2026-10-17) the night runs on the fake world (FakeWorld,
    /// WorldChecks.cs) instead of a sleep host of its own, and the refusals, the night's doors and
    /// the ambush are checked as well.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>A world for one night: the player stands on 32/32 on an open level, with no
        /// creatures and no doors unless the check adds them.</summary>
        private static FakeWorld fNightWorld()
        {
            FakeWorld lOWorld = new FakeWorld { Level = new UWLevel(0) { TileData = fOpenMap() } };

            lOWorld.Body.Floor = new UWSpot(new UWTilePos(32, 32), 4, 4, 16);
            lOWorld.Body.Feet = new UWSpot(new UWTilePos(32, 32), 4, 4, 16);

            return lOWorld;
        }

        /// <summary>A whole map of open tiles at floor 2.</summary>
        private static UWTile[] fOpenMap()
        {
            UWTile[] lOMap = new UWTile[64 * 64];

            for (int liAt = 0; liAt < lOMap.Length; liAt++)
                lOMap[liAt] = fSightTile(liAt, UWTile.TileTypeEnum.open, 2);

            return lOMap;
        }

        private static int fCountBlows(FakeWorld pOWorld, string psStart)
        {
            return pOWorld.Blows.FindAll(psLine => psLine.StartsWith(psStart)).Count;
        }

        private static void fCheckNight()
        {
            Check.Group("The night (player_sleep)");

            bool lbBuried = UWGameFlags.GaramonBuried;

            // No dream: Garamon is buried.
            UWGameFlags.GaramonBuried = true;

            try
            {
                FakeWorld lOWorld = fNightWorld();

                UWSleepRules.Sleep(fNewSleeper(), 1, lOWorld, false, true);

                // SKILLS.C: update_all_critters_whilst_player_snoozes, then DoWanderingMonsters(0).
                int liCreatures = lOWorld.Log.IndexOf("time passes");

                fExpectBool("night: time passes for the creatures on an undisturbed night", liCreatures >= 0, true);
                fExpectBool("night: ... before the wandering monsters, with the distance test off",
                    liCreatures >= 0 && liCreatures < lOWorld.Log.IndexOf("respawn anywhere"), true);

                // Obj_GarbageCollect(1, 0x14) right after DoClosingDoors(0).
                fExpectInt("night: far objects culled with range 1, twenty at most, after the doors",
                    lOWorld.Log.IndexOf("cull 1/20"), lOWorld.Log.IndexOf("doors") + 1);

                // "Maps & Legends" once the night is sure (cleanup step 3.6: through the presentation).
                fExpectBool("night: the sleep theme plays", lOWorld.Shown.Log.Contains("music " + UWMusicSelector.MapsAndLegendsTheme), true);

                // dream: 0x180 ticks of the 1/256-second clock when no cutscene plays.
                fExpectBool("night: no dream holds 1.5 s of black", Math.Abs(lOWorld.Shown.LastBlackSeconds - 1.5f) < 0.001f, true);

                // drop_drunk_player: 255 when the motion state swims (water or shore) or is lava.
                lOWorld = fNightWorld();
                lOWorld.Body.OnLava = true;
                UWSleepRules.Sleep(fNewSleeper(), 1, lOWorld, true, false);
                fExpectInt("passing out: on lava 255", fCountBlows(lOWorld, "player takes 255"), 1);

                lOWorld = fNightWorld();
                lOWorld.Body.Swimming = true;
                UWSleepRules.Sleep(fNewSleeper(), 1, lOWorld, true, false);
                fExpectInt("passing out: swimming (water or its edge) 255", fCountBlows(lOWorld, "player takes 255"), 1);

                lOWorld = fNightWorld();
                UWSleepRules.Sleep(fNewSleeper(), 1, lOWorld, true, false);
                fExpectInt("passing out: on dry ground nothing", fCountBlows(lOWorld, "player takes"), 0);
                fExpectBool("passing out: no dream and no hold of black",
                    lOWorld.Shown.Log.Contains("black") || lOWorld.Shown.Log.Exists(psLine => psLine.StartsWith("cutscene")), false);

                // SKILLS.C player_sleep: damage_item(..., DMG_POISON) runs check_res, so Poison
                // Resistance wards the night's poison off (B5, decided by the original 2026-10-17).
                UWPlayerVitals lOWarded = fNewSleeper();

                lOWorld = fNightWorld();
                lOWorld.Body.Proof = UWDamageTypes.Poison;
                lOWarded.ApplyPoison(3);
                UWSleepRules.Sleep(lOWarded, 1, lOWorld, false, false);
                fExpectBool("night: Poison Resistance wards off the poison's 3 + 2 + 1, and it is cured",
                    lOWarded.LastDamage == 0 && lOWarded.Poison == 0, true);

                UWPlayerVitals lOPoisoned = fNewSleeper();

                lOPoisoned.ApplyPoison(3);
                UWSleepRules.Sleep(lOPoisoned, 1, fNightWorld(), false, false);
                fExpectBool("night: without it the poison takes 3 + 2 + 1 at once",
                    lOPoisoned.LastDamage == 6 && lOPoisoned.LastDamageType == UWDamageTypes.Poison, true);
            }
            finally
            {
                UWGameFlags.GaramonBuried = lbBuried;
            }

            fCheckSleepRefusals();
            fCheckNightDoors();
            fCheckAmbush();
        }

        /// <summary>
        /// THE REFUSALS of a chosen sleep (player_sleep): water, lava and the air give "You cannot
        /// sleep here" (0x15, our numbering), so does the Void; an attacker that knows where the
        /// player is, two tiles around, gives "There are hostile creatures near!" (0x0F). Whoever
        /// passes out is not asked. Cleanup step 3.6 (2026-10-17): these went through the sleep's
        /// host and had no check.
        /// </summary>
        private static void fCheckSleepRefusals()
        {
            Check.Group("The night: who may lie down (cleanup step 3.6)");

            FakeWorld lOWorld = fNightWorld();

            lOWorld.Body.Swimming = true;
            fExpectBool("refusal: swimming is refused", UWSleepRules.Sleep(fNewSleeper(), 1, lOWorld), false);
            fExpectBool("refusal: ... with \"You cannot sleep here\"", lOWorld.Shown.Log.Contains("general 21"), true);
            fExpectBool("refusal: ... and nothing else happens", lOWorld.Log.Contains("doors") || lOWorld.Shown.Log.Exists(
                psLine => psLine.StartsWith("music")), false);

            lOWorld = fNightWorld();
            lOWorld.Body.OnLava = true;
            fExpectBool("refusal: lava is refused", UWSleepRules.Sleep(fNewSleeper(), 1, lOWorld), false);

            lOWorld = fNightWorld();
            lOWorld.Body.OnGround = false;
            fExpectBool("refusal: off the ground is refused", UWSleepRules.Sleep(fNewSleeper(), 1, lOWorld), false);

            lOWorld = fNightWorld();
            fExpectBool("refusal: the Void is refused",
                UWSleepRules.Sleep(fNewSleeper(), UWLevelIds.Number(UWLevelIds.Void), lOWorld), false);
            fExpectBool("refusal: ... with \"You cannot sleep here\"", lOWorld.Shown.Log.Contains("general 21"), true);

            // An attacker two tiles east (the area reaches -2..+3) that knows where the player is.
            lOWorld = fNightWorld();

            UWCreatureRef lOAttacker = fFakeCreature(1, 34, 32, 34, 32);

            lOAttacker.Record.Goal = 5;
            lOAttacker.Record.TargetConfirmed = true;
            lOWorld.Creatures.Add(lOAttacker);

            fExpectBool("refusal: an attacker that knows the player keeps him awake",
                UWSleepRules.Sleep(fNewSleeper(), 1, lOWorld), false);
            fExpectBool("refusal: ... with \"There are hostile creatures near!\"", lOWorld.Shown.Log.Contains("general 15"), true);

            // The same attacker three tiles west lies outside -2..+3.
            lOWorld = fNightWorld();
            lOAttacker = fFakeCreature(1, 29, 32, 29, 32);
            lOAttacker.Record.Goal = 5;
            lOAttacker.Record.TargetConfirmed = true;
            lOWorld.Creatures.Add(lOAttacker);

            fExpectBool("refusal: three tiles west is not near (the area is -2..+3)",
                UWSleepRules.IsHostileNear(lOWorld), false);

            // Passing out on water is not refused - the ground has its say afterwards.
            lOWorld = fNightWorld();
            lOWorld.Body.Swimming = true;
            fExpectBool("refusal: whoever passes out is not asked", UWSleepRules.Sleep(fNewSleeper(), 1, lOWorld, true), true);
        }

        /// <summary>
        /// THE NIGHT'S DOORS (DoClosingDoors(0), TRIGGER.C): an open door at rest on a tile without
        /// the tile's door bit closes with the chance given; a closed one, a moving one and one on a
        /// door-bit tile stay as they are. The rule moved from the sleep's host into UWSleepRules in
        /// cleanup step 3.6 (2026-10-17).
        /// </summary>
        private static void fCheckNightDoors()
        {
            Check.Group("The night: the closing doors (cleanup step 3.6)");

            FakeWorld lOWorld = fNightWorld();

            lOWorld.Level.GetTile(14, 10).HasDoor = true;

            FakeDoor lOOpen = new FakeDoor(lOWorld.Log, fDoorAt(10, 10), false);
            FakeDoor lOClosed = new FakeDoor(lOWorld.Log, fDoorAt(11, 10), true);
            FakeDoor lOMoving = new FakeDoor(lOWorld.Log, fDoorAt(12, 10), false, true);
            FakeDoor lONoData = new FakeDoor(lOWorld.Log, null, false);
            FakeDoor lODoorBit = new FakeDoor(lOWorld.Log, fDoorAt(14, 10), false);

            lOWorld.DoorList.AddRange(new IUWDoor[] { lOOpen, lOClosed, lOMoving, lONoData, lODoorBit });

            UWSleepRules.CloseDoorsAtNight(lOWorld, 10);

            fExpectBool("night doors: at ten in ten the open door at rest closes", lOOpen.Closed, true);
            fExpectBool("night doors: the closed one is left", lOWorld.Log.Contains("door 11/10 opens"), false);
            fExpectBool("night doors: the moving one is left", lOMoving.Closed, false);
            fExpectBool("night doors: a door without data is left", lONoData.Closed, false);
            fExpectBool("night doors: a door on a tile with the door bit is left", lODoorBit.Closed, false);

            lOWorld = fNightWorld();
            lOOpen = new FakeDoor(lOWorld.Log, fDoorAt(10, 10), false);
            lOWorld.DoorList.Add(lOOpen);

            UWSleepRules.CloseDoorsAtNight(lOWorld, 0);

            fExpectBool("night doors: at none in ten nothing closes", lOOpen.Closed, false);
        }

        private static UWObject fDoorAt(int piTileX, int piTileY)
        {
            return new UWObject(0x140) { TileX = piTileX, TileY = piTileY };
        }

        /// <summary>
        /// THE AMBUSH (ovr104_71C): a hostile creature in reach whose path to the sleeper is two steps
        /// or more is set down on the tile two steps before him, on the tile type's spot, confirms its
        /// target and heads for him; the night is interrupted. Run with the highest rolls, so the coin
        /// comes up. Cleanup step 3.6 (2026-10-17): went through the sleep's host and had no check.
        /// </summary>
        private static void fCheckAmbush()
        {
            Check.Group("The night: the ambush (cleanup step 3.6)");

            UWRandom.Use(new FixedRandomSource(true));

            try
            {
                FakeWorld lOWorld = fNightWorld();
                UWCreatureRef lOFriend = fFakeCreature(1, 36, 32, 36, 32);
                UWCreatureRef lOHostile = fFakeCreature(2, 38, 32, 38, 32);

                lOFriend.Record.Attitude = 2;
                lOHostile.Record.Attitude = 0;
                lOWorld.Creatures.Add(lOHostile);
                lOWorld.Creatures.Add(lOFriend);
                lOWorld.TravelRanges[0x41] = 8;
                lOWorld.TravelRanges[0x42] = 8;
                lOWorld.Paths[0x41] = fPath(36, 35, 34, 33, 32);
                lOWorld.Paths[0x42] = fPath(38, 37, 36, 35, 34, 33, 32);

                fExpectBool("ambush: the hostile one comes", UWSleepRules.TryAmbush(lOWorld), true);
                fExpectBool("ambush: the friend was not asked for a path", lOWorld.Log.Exists(psLine => psLine.StartsWith("path of 65")), false);
                fExpectBool("ambush: its path goes to the sleeper's tile and the feet's height",
                    lOWorld.Log.Contains("path of 66 to 32/32 at 2"), true);
                fExpectBool("ambush: set down two steps before him, on the open tile's 4/4",
                    lOWorld.Log.Contains("place 66 on 34/32 at 4/4"), true);
                fExpectBool("ambush: its target is confirmed", lOHostile.Record.TargetConfirmed, true);

                // Out of reach: 3 * range^2 below the squared distance.
                lOWorld = fNightWorld();
                lOHostile = fFakeCreature(2, 38, 32, 38, 32);
                lOHostile.Record.Attitude = 0;
                lOWorld.Creatures.Add(lOHostile);
                lOWorld.TravelRanges[0x42] = 3;
                lOWorld.Paths[0x42] = fPath(38, 37, 36, 35, 34, 33, 32);

                fExpectBool("ambush: out of its reach nobody comes", UWSleepRules.TryAmbush(lOWorld), false);

                // A path of one step: standing next to him already, not moved.
                lOWorld = fNightWorld();
                lOHostile = fFakeCreature(2, 33, 32, 33, 32);
                lOHostile.Record.Attitude = 0;
                lOWorld.Creatures.Add(lOHostile);
                lOWorld.TravelRanges[0x42] = 8;
                lOWorld.Paths[0x42] = fPath(33, 32);

                fExpectBool("ambush: one step away is not moved", UWSleepRules.TryAmbush(lOWorld), false);
                fExpectBool("ambush: ... and nobody is set down", lOWorld.Log.Exists(psLine => psLine.StartsWith("place")), false);
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>A path along row 32, start first.</summary>
        private static List<UWTilePos> fPath(params int[] piXs)
        {
            List<UWTilePos> lOPath = new List<UWTilePos>();

            foreach (int liX in piXs)
                lOPath.Add(new UWTilePos(liX, 32));

            return lOPath;
        }

        /// <summary>Obj_GarbageCollect(1, 0x14) on level 1 with the lowest rolls: twenty objects go,
        /// none within nine tiles (taxicab) of the player.</summary>
        private static void fCheckNightCulling(UWDataImport.DataImport pOData)
        {
            Check.Group("The night's tidying (Obj_GarbageCollect)");

            UWLevel lOLevel = pOData != null && pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;

            if (lOLevel == null)
            {
                fExpectBool("night culling: level 1 loaded", false, true);
                return;
            }

            UWTilePos lOPlayer = new UWTilePos(32, 2);
            UWWorldEdit lOEdit = new UWWorldEdit(new UWTileQueries(lOLevel, pOData)) { PlayerTile = () => lOPlayer };
            int liNearBefore = fCountObjectsNear(lOLevel, lOPlayer, 9);

            // The group's seeded generator: the result is the same on every run.
            int liCulled = lOEdit.CollectGarbage(UWSleepRules.NightCullRange, UWSleepRules.NightCullLimit);

            fExpectBool("night culling: some objects went, twenty at most (" + liCulled + ")", liCulled > 0 && liCulled <= 20, true);
            fExpectInt("night culling: nothing within nine tiles", fCountObjectsNear(lOLevel, lOPlayer, 9), liNearBefore);
        }

        private static int fCountObjectsNear(UWLevel pOLevel, UWTilePos pOPlayer, int piDistance)
        {
            int liCount = 0;

            for (int liX = 0; liX < 64; liX++)
            {
                for (int liY = 0; liY < 64; liY++)
                {
                    UWTile lOTile = pOLevel.GetTile(liX, liY);

                    if (lOTile != null && lOTile.ObjectsInTile != null
                        && Math.Abs(liX - pOPlayer.X) + Math.Abs(liY - pOPlayer.Y) <= piDistance)
                        liCount += lOTile.ObjectsInTile.Count;
                }
            }

            return liCount;
        }

        private static UWPlayerVitals fNewSleeper()
        {
            UWPlayerVitals lOVitals = new UWPlayerVitals(new FakeVitalsHost());

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);

            return lOVitals;
        }

        private static void fCheckSleepCreatures()
        {
            Check.Group("Sleep: who keeps the player awake");

            // check_for_hostile_creature (CRITTIME.C): goal 5, 4 or 9, each with the target confirmed.
            fExpectBool("sleep: goal 5 confirmed keeps awake", UWSleepRules.BlocksSleep(5, true), true);
            fExpectBool("sleep: goal 5 unconfirmed does not", UWSleepRules.BlocksSleep(5, false), false);
            fExpectBool("sleep: goal 4 confirmed keeps awake", UWSleepRules.BlocksSleep(4, true), true);
            fExpectBool("sleep: goal 4 that never saw him does not", UWSleepRules.BlocksSleep(4, false), false);
            fExpectBool("sleep: goal 9 unconfirmed does not", UWSleepRules.BlocksSleep(9, false), false);
            fExpectBool("sleep: goal 9 confirmed does", UWSleepRules.BlocksSleep(9, true), true);
            fExpectBool("sleep: a wanderer (goal 8) does not", UWSleepRules.BlocksSleep(8, true), false);

            // gronk_area (SPELLS.C): -r..+r+1 on both axes.
            fExpectBool("area: -2/-2 is in the radius-2 area", UWSleepRules.InArea(-2, -2, 2), true);
            fExpectBool("area: +3/+3 is in it too", UWSleepRules.InArea(3, 3, 2), true);
            fExpectBool("area: -3 is not", UWSleepRules.InArea(-3, 0, 2), false);
            fExpectBool("area: +4 is not", UWSleepRules.InArea(0, 4, 2), false);
            fExpectBool("area: the ambush reaches +9", UWSleepRules.InArea(9, -8, 8), true);
            fExpectInt("sleep: the square before sleeping is two tiles", UWSleepRules.HostileCheckTileRadius, 2);
            fExpectInt("sleep: the ambush square is eight tiles", UWSleepRules.AmbushTileRadius, 8);

            // ovr104_71C: squared distance at most three times the squared travel range.
            fExpectBool("ambush: range 2 reaches 3/1 (10 <= 12)", UWSleepRules.WithinAmbushReach(3, 1, 2), true);
            fExpectBool("ambush: range 2 does not reach 3/2 (13 > 12)", UWSleepRules.WithinAmbushReach(3, 2, 2), false);
            fExpectBool("ambush: range 0 reaches nothing but its own tile", UWSleepRules.WithinAmbushReach(1, 0, 0), false);

            // The path buffer: both ends included, round a wall.
            FakeWalker lOWalker = new FakeWalker();
            lOWalker.Walls.Add((10 * 64) + 11);
            System.Collections.Generic.List<UWTilePos> lOPath = new System.Collections.Generic.List<UWTilePos>();
            bool lbFound = UWTilePath.TryGetPath(lOWalker, new UWTilePos(10, 10), new UWTilePos(12, 10), lOPath);
            fExpectBool("path: found round the wall", lbFound, true);
            fExpectInt("path: four steps round one wall tile, five entries", lOPath.Count, 5);
            fExpectBool("path: starts at the start and ends at the target",
                lOPath.Count == 5 && lOPath[0] == new UWTilePos(10, 10) && lOPath[4] == new UWTilePos(12, 10), true);

            // ovr104_BB: the spot by tile type, and the north-west quirk.
            int liSx;
            int liSy;
            fExpectBool("ambush spot: open tile 4/4",
                UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.open, out liSx, out liSy) && liSx == 4 && liSy == 4, true);
            fExpectBool("ambush spot: solid tile refused", UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.solid, out liSx, out liSy), false);
            UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.diagonal_ne, out liSx, out liSy);
            fExpectBool("ambush spot: north-east 6/6 lies in the open half",
                UWTileQueries.IsSubTileInOpenHalf(UWTile.TileTypeEnum.diagonal_ne, liSx, liSy), true);
            UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.diagonal_se, out liSx, out liSy);
            fExpectBool("ambush spot: south-east 6/1 lies in the open half",
                UWTileQueries.IsSubTileInOpenHalf(UWTile.TileTypeEnum.diagonal_se, liSx, liSy), true);
            UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.diagonal_sw, out liSx, out liSy);
            fExpectBool("ambush spot: south-west 1/1 lies in the open half",
                UWTileQueries.IsSubTileInOpenHalf(UWTile.TileTypeEnum.diagonal_sw, liSx, liSy), true);
            UWSleepRules.TryGetAmbushSpot(UWTile.TileTypeEnum.diagonal_nw, out liSx, out liSy);
            fExpectBool("ambush spot: north-west 6/1 lies in the CLOSED half (the original's table)",
                UWTileQueries.IsSubTileInOpenHalf(UWTile.TileTypeEnum.diagonal_nw, liSx, liSy), false);
        }
    }
}
