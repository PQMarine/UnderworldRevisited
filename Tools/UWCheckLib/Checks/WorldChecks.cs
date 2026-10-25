using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 3.1 (2026-10-17): the world, the presentation and the player's body, phase A -
    /// the spot's arithmetic, the silent scope (the lines dropped, everything else played), the four
    /// level conventions of the hosts and the split of travel and teleport. FakeWorld is the headless
    /// world the later steps of phase 3 build their trap, spell and sleep checks on.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckWorld()
        {
            Check.Group("World, presentation and avatar, phase A (cleanup step 3.1)");

            fCheckSpot();
            fCheckSilentScope();
            fCheckLevelConventions();
            fCheckTravelAndTeleport();

            Check.Group("World: the creature search (cleanup step 3.2)");

            fCheckCreatureAreas();
            fCheckCreatureFilters();
            fCheckFindCreatures();
        }

        /// <summary>UWSpot: tile, eighths and height in the original's units.</summary>
        private static void fCheckSpot()
        {
            UWSpot lOSpot = UWSpot.FromEighths((10 << 3) + 3, (20 << 3) + 5, 43);

            fExpectInt("spot: tile x from the eighths", lOSpot.Tile.X, 10);
            fExpectInt("spot: tile y from the eighths", lOSpot.Tile.Y, 20);
            fExpectInt("spot: fine x", lOSpot.FineX, 3);
            fExpectInt("spot: fine y", lOSpot.FineY, 5);
            fExpectInt("spot: eighths x round trip", lOSpot.EighthsX, (10 << 3) + 3);
            fExpectInt("spot: eighths y round trip", lOSpot.EighthsY, (20 << 3) + 5);

            // The sleeper's height: zpos >> 3, as a creature record keeps it (UWSleep's
            // PlayerFloorLevel was (original z >> 3) >> 3, the feet's zpos shifted once more).
            fExpectInt("spot: floor level is zpos >> 3", lOSpot.FloorLevel, 5);
            fExpectBool("spot: on the map", lOSpot.IsOnMap, true);
            fExpectBool("spot: equal to the same spot made by hand",
                lOSpot == new UWSpot(new UWTilePos(10, 20), 3, 5, 43), true);
            fExpectBool("spot: a different height is a different spot",
                lOSpot == new UWSpot(new UWTilePos(10, 20), 3, 5, 44), false);

            // Nowhere is the trap host's old answer without a camera: tile -1/-1, off the map.
            fExpectBool("spot: Nowhere is off the map", UWSpot.Nowhere.IsOnMap, false);
            fExpectInt("spot: Nowhere's tile x", UWSpot.Nowhere.Tile.X, -1);
            fExpectInt("spot: Nowhere's tile y", UWSpot.Nowhere.Tile.Y, -1);
        }

        /// <summary>The silent scope: no line reaches the scroll, everything else plays.</summary>
        private static void fCheckSilentScope()
        {
            LoggingPresentation lOInner = new LoggingPresentation();
            IUWPresentation lOSilent = UWSilentPresentation.Of(lOInner);

            fExpectBool("silent scope: shows no messages", lOSilent.ShowsMessages, false);
            fExpectBool("silent scope: the presentation inside does", lOInner.ShowsMessages, true);

            lOSilent.AddMessage("dropped");
            lOSilent.AddGeneralMessage(7);

            // The hosts answered "" for a text without an Interaction (UWSpellHost.GetGeneralMessage).
            fExpectBool("silent scope: a text lookup answers empty", lOSilent.GetGeneralMessage(7) == string.Empty, true);

            lOSilent.PlaySoundAtPlayer(0x15);
            lOSilent.ShowEffect(0x1C0, new UWSpot(new UWTilePos(3, 4), 3, 3, 16), 0.7f);
            lOSilent.ShakeScreen(0x1E);
            lOSilent.FlashWindow(0xA8, 0.1f);
            lOSilent.ShowNightBlack(1.5f);

            bool lbPlayed = lOSilent.PlayCutscene(3);

            fExpectBool("silent scope: no line reached the presentation inside",
                lOInner.Log.Exists(psLine => psLine.StartsWith("message") || psLine.StartsWith("general")), false);
            fExpectInt("silent scope: sound, effect, shake, flash, black and cutscene still played, in order",
                fIndexOfSequence(lOInner.Log, "sound 21", "effect 448 3/4 at 3/3 z 16", "shake 30", "flash 168", "black",
                    "cutscene 3"), 0);
            fExpectBool("silent scope: the cutscene's answer comes from inside", lbPlayed, true);

            lOInner.CanPlayCutscenes = false;
            fExpectBool("silent scope: ... also when nothing can show it", lOSilent.PlayCutscene(3), false);

            fExpectBool("silent scope: silencing it again gives the same scope",
                ReferenceEquals(UWSilentPresentation.Of(lOSilent), lOSilent), true);

            IUWPresentation lONothing = UWSilentPresentation.Of(null);

            lONothing.ShakeScreen(1);
            fExpectBool("silent scope without a presentation: plays no cutscene", lONothing.PlayCutscene(3), false);
        }

        /// <summary>Where the lines appear in the log, in order: 0 when they all do, else -1.</summary>
        private static int fIndexOfSequence(List<string> pOLog, params string[] psLines)
        {
            int liFrom = 0;

            foreach (string lsLine in psLines)
            {
                int liAt = pOLog.IndexOf(lsLine, liFrom);

                if (liAt < 0)
                    return -1;

                liFrom = liAt + 1;
            }

            return 0;
        }

        /// <summary>The hosts' four conventions for the dungeon level (UWWorldLevels).</summary>
        private static void fCheckLevelConventions()
        {
            FakeWorld lOWorld = new FakeWorld { LevelIndex = 4 };

            fExpectInt("level: trap host, 0-based", UWWorldLevels.IndexOr(lOWorld, 0), 4);
            fExpectInt("level: trap host without a world, 0", UWWorldLevels.IndexOr(null, 0), 0);
            fExpectInt("level: vitals, 0-based", UWWorldLevels.IndexOr(lOWorld, -1), 4);
            fExpectInt("level: vitals without a world, -1", UWWorldLevels.IndexOr(null, -1), -1);
            fExpectInt("level: conversation's dungeon_level, 1-based", UWWorldLevels.NumberOr(lOWorld, 1), 5);
            fExpectInt("level: conversation without a world, 1", UWWorldLevels.NumberOr(null, 1), 1);
            fExpectInt("level: shrine, 1-based", UWWorldLevels.NumberOr(lOWorld, 0), 5);
            fExpectInt("level: shrine without a world, 0", UWWorldLevels.NumberOr(null, 0), 0);
        }

        /// <summary>The teleport trap's move and the plain one stay two members.</summary>
        private static void fCheckTravelAndTeleport()
        {
            FakeWorld lOWorld = new FakeWorld();

            lOWorld.TeleportTo(1, 32, 2);
            lOWorld.TravelTo(2, 20, 30);

            fExpectInt("travel: the teleport and the plain move are told apart",
                fIndexOfSequence(lOWorld.Log, "teleport 1 32/2", "travel 2 20/30"), 0);

            UWTileQueries lOTiles = lOWorld.Tiles;

            fExpectBool("tiles: a world without a level answers no tile", lOTiles.GetTile(1, 1) == null, true);
        }

        /// <summary>The boxes the old scans tested by hand, now UWTileArea: each must hold exactly
        /// the tiles the old delta test held.</summary>
        private static void fCheckCreatureAreas()
        {
            UWTilePos lOCentre = new UWTilePos(30, 40);

            // The theft and trespass witnesses: seven back, eight forward on both axes (the old
            // "delta < -back || delta > forward" skipped the rest).
            UWTileArea lOTheft = UWTileArea.Around(lOCentre, UWCritterRules.TheftAreaBack, UWCritterRules.TheftAreaForward,
                UWCreatureTile.Body);
            bool lbSame = true;

            for (int liDx = -12; liDx <= 12; liDx++)
            {
                for (int liDy = -12; liDy <= 12; liDy++)
                {
                    bool lbOld = !(liDx < -UWCritterRules.TheftAreaBack || liDx > UWCritterRules.TheftAreaForward
                        || liDy < -UWCritterRules.TheftAreaBack || liDy > UWCritterRules.TheftAreaForward);

                    lbSame &= lOTheft.Contains(new UWTilePos(lOCentre.X + liDx, lOCentre.Y + liDy)) == lbOld;
                }
            }

            fExpectBool("area: the theft box holds exactly the old delta test's tiles (-7..+8)", lbSame, true);
            fExpectBool("area: theft, -7/-7 inside", lOTheft.Contains(new UWTilePos(23, 33)), true);
            fExpectBool("area: theft, +8/+8 inside", lOTheft.Contains(new UWTilePos(38, 48)), true);
            fExpectBool("area: theft, -8 outside", lOTheft.Contains(new UWTilePos(22, 40)), false);
            fExpectBool("area: theft, +9 outside", lOTheft.Contains(new UWTilePos(30, 49)), false);

            // The sleep: gronk_area, one tile more on the + side - the same as UWSleepRules.InArea,
            // which now asks the area itself.
            bool lbSleepSame = true;

            foreach (int liRadius in new[] { UWSleepRules.HostileCheckTileRadius, UWSleepRules.AmbushTileRadius })
            {
                UWTileArea lOSleep = UWSleepRules.AreaAround(lOCentre, liRadius);

                for (int liDx = -12; liDx <= 12; liDx++)
                {
                    for (int liDy = -12; liDy <= 12; liDy++)
                    {
                        bool lbOld = liDx >= -liRadius && liDx <= liRadius + 1 && liDy >= -liRadius && liDy <= liRadius + 1;

                        lbSleepSame &= lOSleep.Contains(new UWTilePos(lOCentre.X + liDx, lOCentre.Y + liDy)) == lbOld
                            && UWSleepRules.InArea(liDx, liDy, liRadius) == lbOld;
                    }
                }
            }

            fExpectBool("area: the sleep's gronk_area (radius 2 and 8) holds exactly -r..r+1", lbSleepSame, true);
            fExpectBool("area: the sleep measures by the record",
                UWSleepRules.AreaAround(lOCentre, 2).Measure == UWCreatureTile.Record, true);

            // A creation trap with no area at all (0 back, 0 forward) holds its own tile only.
            UWTileArea lOTile = UWTileArea.Around(lOCentre, 0, 0, UWCreatureTile.Body);

            fExpectBool("area: 0/0 holds the centre", lOTile.Contains(lOCentre), true);
            fExpectBool("area: 0/0 holds nothing else", lOTile.Contains(new UWTilePos(31, 40)), false);

            fExpectBool("area: the whole level holds a tile off the map",
                UWTileArea.WholeLevel.Contains(UWSpot.Nowhere.Tile), true);
        }

        /// <summary>The filters: each is the test one of the old scans made.</summary>
        private static void fCheckCreatureFilters()
        {
            UWTileArea lOAll = UWTileArea.WholeLevel;
            UWCreatureRef lOAlive = fFakeCreature(1, 5, 5, 5, 5);
            UWCreatureRef lODead = fFakeCreature(2, 5, 5, 5, 5, pbDead: true);
            UWCreatureRef lONoRecord = fFakeCreature(3, 5, 5, 5, 5, pbWithRecord: false);
            UWCreatureRef lONoObject = fFakeCreature(4, 5, 5, 5, 5, pbWithObject: false);
            UWCreatureRef lOSpawned = fFakeCreature(5, 5, 5, 5, 5, pbSpawned: true);

            fExpectBool("filter: any takes the dead", UWCreatureSearch.Matches(lODead, lOAll, UWCreatureFilter.Any), true);
            fExpectBool("filter: any takes one without a record (the whole level)",
                UWCreatureSearch.Matches(lONoRecord, lOAll, UWCreatureFilter.Any), true);
            fExpectBool("filter: living takes the living",
                UWCreatureSearch.Matches(lOAlive, lOAll, UWCreatureFilter.Living), true);
            fExpectBool("filter: living drops the dead (sleep, Detect Monsters)",
                UWCreatureSearch.Matches(lODead, lOAll, UWCreatureFilter.Living), false);
            fExpectBool("filter: living drops one without a record",
                UWCreatureSearch.Matches(lONoRecord, lOAll, UWCreatureFilter.Living), false);
            fExpectBool("filter: with object drops one without (theft)",
                UWCreatureSearch.Matches(lONoObject, lOAll, UWCreatureFilter.HasObject), false);
            fExpectBool("filter: with object keeps a dead one (theft asked no more)",
                UWCreatureSearch.Matches(lODead, lOAll, UWCreatureFilter.HasObject), true);
            fExpectBool("filter: spawned takes one made during play (crowding)",
                UWCreatureSearch.Matches(lOSpawned, lOAll, UWCreatureFilter.Spawned), true);
            fExpectBool("filter: spawned drops one from the level file",
                UWCreatureSearch.Matches(lOAlive, lOAll, UWCreatureFilter.Spawned), false);
            fExpectBool("filter: spawned drops one without an object",
                UWCreatureSearch.Matches(lONoObject, lOAll, UWCreatureFilter.Spawned), false);
            fExpectBool("filter: living and spawned together need both",
                UWCreatureSearch.Matches(lOAlive, lOAll, UWCreatureFilter.Living | UWCreatureFilter.Spawned), false);

            // A creature without a record never lies in an area measured by the record.
            fExpectBool("filter: no record, no record tile",
                UWCreatureSearch.Matches(lONoRecord, UWTileArea.Around(UWSpot.Nowhere.Tile, 1, 1, UWCreatureTile.Record),
                    UWCreatureFilter.Any), false);
        }

        /// <summary>FindCreatures through the fake world: the measure decides between the record and
        /// the body, the order is the list's, the result list is cleared first.</summary>
        private static void fCheckFindCreatures()
        {
            FakeWorld lOWorld = new FakeWorld();
            List<UWCreatureRef> lOFound = new List<UWCreatureRef>();

            // The body trails the record by a step at a tile edge: record on 10/10, body still on 9/10.
            lOWorld.Creatures.Add(fFakeCreature(1, 10, 10, 9, 10));
            lOWorld.Creatures.Add(fFakeCreature(2, 20, 20, 20, 20, pbSpawned: true));
            lOWorld.Creatures.Add(fFakeCreature(3, 11, 10, 11, 10, pbDead: true));
            lOWorld.Creatures.Add(fFakeCreature(4, 10, 11, 10, 11, pbSpawned: true));

            UWTilePos lOEdge = new UWTilePos(10, 10);

            fExpectInt("find: by the record, the creature whose record moved on is on 10/10",
                lOWorld.FindCreatures(UWTileArea.Around(lOEdge, 0, 0, UWCreatureTile.Record), UWCreatureFilter.Any, lOFound), 1);
            fExpectInt("find: ... and it is that one", lOFound[0].ObjectId, 0x41);
            fExpectInt("find: by the body, nobody stands on 10/10 yet",
                lOWorld.FindCreatures(UWTileArea.Around(lOEdge, 0, 0, UWCreatureTile.Body), UWCreatureFilter.Any, lOFound), 0);
            fExpectInt("find: by the body, it is still on 9/10",
                lOWorld.FindCreatures(UWTileArea.Around(new UWTilePos(9, 10), 0, 0, UWCreatureTile.Body),
                    UWCreatureFilter.Any, lOFound), 1);

            // The whole level in the list's order (the Active order), dead ones included.
            lOWorld.FindCreatures(UWTileArea.WholeLevel, UWCreatureFilter.Any, lOFound);

            fExpectInt("find: the whole level finds all four", lOFound.Count, 4);
            fExpectBool("find: in the order of the level's list",
                lOFound[0].ObjectId == 0x41 && lOFound[1].ObjectId == 0x42 && lOFound[2].ObjectId == 0x43
                && lOFound[3].ObjectId == 0x44, true);

            // Detect Monsters' input: the living, by their record tile.
            lOWorld.FindCreatures(UWTileArea.WholeLevel, UWCreatureFilter.Living, lOFound);

            fExpectInt("find: the living leave out the dead one", lOFound.Count, 3);
            fExpectBool("find: the record tile is handed out", lOFound[0].RecordTile == lOEdge, true);

            // The creation trap's crowding: a spawned creature within one tile of 10/10, by the body.
            fExpectInt("find: crowding near 10/10 sees the spawned one on 10/11",
                lOWorld.FindCreatures(UWTileArea.Around(lOEdge, 1, 1, UWCreatureTile.Body), UWCreatureFilter.Spawned,
                    lOFound), 1);
            fExpectInt("find: ... not the one from the level file beside it", lOFound[0].ObjectId, 0x44);

            // The list is cleared first: a search that finds nothing leaves it empty.
            fExpectInt("find: nothing far away",
                lOWorld.FindCreatures(UWTileArea.Around(new UWTilePos(50, 50), 2, 3, UWCreatureTile.Record),
                    UWCreatureFilter.Any, lOFound), 0);
            fExpectInt("find: ... and the list was emptied", lOFound.Count, 0);
        }
    }
}
