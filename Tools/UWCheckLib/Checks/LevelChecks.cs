using System;
using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The level's own rules: the sight line, the respawn distance, the map rewrite, the order
    /// of the allocated mobiles, the scatter, the object limit, a creature made in code and the
    /// removal of a one-shot chain.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE SIGHT LINE (UWTilePath.TestBetweenPoints, TestBetweenPoints_seg006_1477_1BD1 with
        /// TestTileTraversal_seg006_1056) on a small map made up here: a corridor of three tiles
        /// running east at y 10, floor 2, the line at zpos 40 through the tile centres.
        /// </summary>
        private static void fCheckSightLine()
        {
            Check.Group("Sight line");

            const int Z = 40;
            int liX0 = (10 * 8) + 4;
            int liX2 = (12 * 8) + 4;
            int liY = (10 * 8) + 4;

            UWTile[] lOMap = fSightMap(UWTile.TileTypeEnum.open, 2);
            fExpectBool("sight: along an open corridor",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), true);

            lOMap = fSightMap(UWTile.TileTypeEnum.solid, 2);
            fExpectBool("sight: a solid tile in between",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), false);

            // A diagonal tile in the middle: 0x13 (se) closes west and north, 0x15 (sw) east and
            // north - the line east enters by the west and leaves by the east.
            lOMap = fSightMap(UWTile.TileTypeEnum.diagonal_se, 2);
            fExpectBool("sight: into a diagonal by its closed side",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), false);

            lOMap = fSightMap(UWTile.TileTypeEnum.diagonal_sw, 2);
            fExpectBool("sight: out of a diagonal by its closed side",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), false);

            // From the south into the middle tile: se has its south open, nw closes it.
            int liYSouth = (9 * 8) + 4;
            int liXMid = (11 * 8) + 4;

            lOMap = fSightMap(UWTile.TileTypeEnum.diagonal_se, 2);
            lOMap[(9 * 64) + 11] = fSightTile((9 * 64) + 11, UWTile.TileTypeEnum.open, 2);
            fExpectBool("sight: into a diagonal by an open side",
                UWTilePath.TestBetweenPoints(lOMap, liXMid, liYSouth, Z, liXMid, liY, Z), true);

            lOMap = fSightMap(UWTile.TileTypeEnum.diagonal_nw, 2);
            lOMap[(9 * 64) + 11] = fSightTile((9 * 64) + 11, UWTile.TileTypeEnum.open, 2);
            fExpectBool("sight: the same side closed on the other diagonal",
                UWTilePath.TestBetweenPoints(lOMap, liXMid, liYSouth, Z, liXMid, liY, Z), false);

            // A slope closes no side (0x20).
            lOMap = fSightMap(UWTile.TileTypeEnum.slope_e, 2);
            fExpectBool("sight: a slope lets it through",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), true);

            // A floor above the line: zpos 40 is level 5, a floor of 8 stops it.
            lOMap = fSightMap(UWTile.TileTypeEnum.open, 8);
            fExpectBool("sight: a floor higher than the line",
                UWTilePath.TestBetweenPoints(lOMap, liX0, liY, Z, liX2, liY, Z), false);
        }

        /// <summary>A tile as the level loader builds it: type in the low nibble, floor in the
        /// next.</summary>
        private static UWTile fSightTile(int piIndex, UWTile.TileTypeEnum peType, int piFloor)
        {
            return new UWTile(piIndex, (uint)(((piFloor & 0xF) << 4) | ((int)peType & 0xF)), new ushort[64]);
        }

        /// <summary>All solid, the corridor 10..12 / 10 open at floor 2, its middle tile of the
        /// given type and floor.</summary>
        private static UWTile[] fSightMap(UWTile.TileTypeEnum peMiddle, int piMiddleFloor)
        {
            UWTile[] lOMap = new UWTile[64 * 64];

            for (int liAt = 0; liAt < lOMap.Length; liAt++)
                lOMap[liAt] = fSightTile(liAt, UWTile.TileTypeEnum.solid, 0);

            lOMap[(10 * 64) + 10] = fSightTile((10 * 64) + 10, UWTile.TileTypeEnum.open, 2);
            lOMap[(10 * 64) + 11] = fSightTile((10 * 64) + 11, peMiddle, piMiddleFloor);
            lOMap[(10 * 64) + 12] = fSightTile((10 * 64) + 12, UWTile.TileTypeEnum.open, 2);

            return lOMap;
        }

        /// <summary>
        /// WHERE A RESPAWN TRAP FIRES (UWRespawnRules.IsNearPlayer,
        /// CheckIfWithin8TilesFromObject_ovr153_14C4): only when it is NOT near the player -
        /// near meaning fewer than eight tiles on both axes. The player stands on 10/10.
        /// </summary>
        private static void fCheckRespawnDistance(DataImport pOData)
        {
            Check.Group("Respawn distance");

            UWTilePos lOPlayer = new UWTilePos(10, 10);

            fExpectBool("respawn: seven tiles east is near", UWRespawnRules.IsNearPlayer(lOPlayer, 17, 10), true);
            fExpectBool("respawn: eight tiles east is not", UWRespawnRules.IsNearPlayer(lOPlayer, 18, 10), false);
            fExpectBool("respawn: eight tiles south is not", UWRespawnRules.IsNearPlayer(lOPlayer, 10, 2), false);
            fExpectBool("respawn: seven on both axes is still near", UWRespawnRules.IsNearPlayer(lOPlayer, 3, 3), true);
            fExpectBool("respawn: the player's own tile is near", UWRespawnRules.IsNearPlayer(lOPlayer, 10, 10), true);

            // The game clock at real time (seg034_2F89_4E adds the PIT ticks per frame): a tick
            // of 21 seconds is 21 * 256 units, 2,394 seconds were 613,021 in the original.
            fExpectInt("clock: one tick is 21 seconds of 256 units", UWGameClock.UnitsPerTick, 5376);
            fExpectInt("clock: a game minute is 60 real seconds", UWGameClock.UnitsPerMinute / UWPlayerTick.PitTicksPerSecond, 60);

            // Tybal's orb (UWTybalOrbRules): level 7 is index 6, and a broken orb stands nowhere.
            bool lbOrbWas = UWGameFlags.OrbDestroyed;
            UWGameFlags.OrbDestroyed = false;
            fExpectBool("orb: stands in the lair, level 7 = index 6", UWTybalOrbRules.OrbStands(6), true);
            fExpectBool("orb: not on level 8 (index 7, the old mistake)", UWTybalOrbRules.OrbStands(7), false);
            UWGameFlags.OrbDestroyed = true;
            fExpectBool("orb: broken, it stands nowhere", UWTybalOrbRules.OrbStands(6), false);
            fExpectInt("orb: Tybal at 120 is left with 61", UWTybalOrbRules.WeakenedHitPoints(120), 61);
            UWGameFlags.OrbDestroyed = lbOrbWas;
            UWCommonObjectProperties.Entry lOOrbRock;
            fExpectBool("orb: the orb rock is used on what it hits when thrown",
                pOData.CommonObjectProperties != null && pOData.CommonObjectProperties.TryGet(UWObjectMechanics.OrbRockObjectId, out lOOrbRock) && lOOrbRock.UsedWhenThrown, true);

            // The head bob (PlayerMotion_seg034_2F89_604): table 1 3 4 3 1 -3 0 0 twice, factor
            // speed * 8 / full - 1, at least 2, nothing at a quarter of the full speed or below.
            fExpectInt("head bob: full speed, top of the step", UWHeadBobRules.GetOffset(32f, 200f, 200f), 28);
            fExpectInt("head bob: full speed, the dip", UWHeadBobRules.GetOffset(80f, 200f, 200f), -21);
            fExpectInt("head bob: slow walk keeps factor 2", UWHeadBobRules.GetOffset(32f, 60f, 200f), 8);
            fExpectInt("head bob: a quarter of the speed does not bob", UWHeadBobRules.GetOffset(32f, 50f, 200f), 0);
            fExpectInt("head bob: two steps a second", UWHeadBobRules.GetOffset(160f, 200f, 200f), 28);
            fExpectInt("head bob: a measured speed above full is capped", UWHeadBobRules.GetOffset(32f, 900f, 200f), 28);
            fExpectInt("head bob smooth: passes through the table at a step", (int)System.Math.Round(UWHeadBobRules.GetSmoothOffset(32f, 200f, 200f)), 28);
            fExpectBool("head bob smooth: between 4 and 3 lies between 28 and 21",
                UWHeadBobRules.GetSmoothOffset(40f, 200f, 200f) < 28f && UWHeadBobRules.GetSmoothOffset(40f, 200f, 200f) > 21f, true);

            // The weapon jitter levels (seg036_3087_245D): speed * 2 / 0x31F + 1 against 0x3AC.
            fExpectInt("weapon jitter: standing is level 0", UWHeadBobRules.GetWeaponJitterLevel(0f), 0);
            fExpectInt("weapon jitter: 40 per cent is level 1", UWHeadBobRules.GetWeaponJitterLevel(0.4f), 1);
            fExpectInt("weapon jitter: 60 per cent is level 2", UWHeadBobRules.GetWeaponJitterLevel(0.6f), 2);
            fExpectInt("weapon jitter: full speed is level 3", UWHeadBobRules.GetWeaponJitterLevel(1f), 3);
            fExpectInt("weapon jitter: level 1 rolls 0 to 4", UWHeadBobRules.GetWeaponJitterRange(1), 5);
            fExpectInt("weapon jitter: level 3 keeps the last", UWHeadBobRules.GetWeaponJitterRange(3), -1);
        }

        /// <summary>UWLevel.MarkTileVisited writes a discovered tile again, as seg017_1FDD_DBC does
        /// (per user, 2026-10-01: a lowered water basin stayed water on our map); a marker our host
        /// does not recognise is kept. The byte is put back afterwards.</summary>
        private static void fCheckMapRewrite(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;

            if (lOLevel == null)
                return;

            lOLevel.EnsureAutomap();

            int liX = -1, liY = -1;

            for (int liAt = 0; liAt < 4096 && liX < 0; liAt++)
            {
                if (lOLevel.TileData[liAt] != null && lOLevel.TileData[liAt].TileType == UWTile.TileTypeEnum.open)
                {
                    liX = liAt % 64;
                    liY = liAt / 64;
                }
            }

            int liIndex = (liY * 64) + liX;
            byte lyKept = lOLevel.AutomapTiles[liIndex];

            lOLevel.AutomapTiles[liIndex] = 0;
            lOLevel.MarkTileVisited(liX, liY, UWLevel.MapDisplayWater);
            fExpectInt("map rewrite: discovered as water", lOLevel.AutomapTiles[liIndex] >> 4, UWLevel.MapDisplayWater);

            lOLevel.MarkTileVisited(liX, liY, UWLevel.MapDisplayClear);
            fExpectInt("map rewrite: seen again after the change, no longer water", lOLevel.AutomapTiles[liIndex] >> 4, 0);

            lOLevel.AutomapTiles[liIndex] = (byte)((UWLevel.MapDisplayDoor << 4) | 1);
            lOLevel.MarkTileVisited(liX, liY, UWLevel.MapDisplayWater);
            fExpectInt("map rewrite: a door we cannot see is kept, the terrain renewed",
                lOLevel.AutomapTiles[liIndex] >> 4, UWLevel.MapDisplayDoor | UWLevel.MapDisplayWater);

            lOLevel.AutomapTiles[liIndex] = (byte)((UWLevel.MapDisplayStair << 4) | 1);
            lOLevel.MarkTileVisited(liX, liY, UWLevel.MapDisplayWater);
            fExpectInt("map rewrite: a stair the original's rule does not find is cleared",
                lOLevel.AutomapTiles[liIndex] >> 4, UWLevel.MapDisplayWater);

            lOLevel.AutomapTiles[liIndex] = lyKept;

            // The stair marker by the original's rule, a wall picture with a stair texture: the
            // stairs down at 27/20 have one, the water hole into level 2 at 47/53 only a trigger
            // and a teleport (per user, 2026-10-07: black and red specks on our map there).
            UWTileQueries lOQueries = new UWTileQueries(lOLevel, pOData);

            fExpectBool("map stair: level 1 27/20, the stairs down", lOQueries.HasStairMarker(lOLevel.GetTile(27, 20)), true);
            fExpectBool("map stair: level 1 47/53, the water hole, none", lOQueries.HasStairMarker(lOLevel.GetTile(47, 53)), false);
        }

        /// <summary>UWLevel.GetActiveMobileOrder (2026-09-29): the list of allocated mobiles at
        /// 0x7AFC, in whose order Tyball's death takes the first creature of each whoami - on
        /// level 7 the 209 guard #222 comes before #223 and #200, as the original removed.</summary>
        private static void fCheckActiveMobileOrder(DataImport pOData)
        {
            Check.Group("Allocated mobiles");

            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 6 ? pOData.Levels[6] : null;

            if (lOLevel == null)
            {
                fExpectBool("mobile order: level 7 loaded", false, true);
                return;
            }

            fExpectBool("mobile order: 209 guard #222 before #223",
                lOLevel.GetActiveMobileOrder(222) < lOLevel.GetActiveMobileOrder(223), true);
            fExpectBool("mobile order: #223 before #200",
                lOLevel.GetActiveMobileOrder(223) < lOLevel.GetActiveMobileOrder(200), true);
            fExpectBool("mobile order: 219 guard #216 before #202",
                lOLevel.GetActiveMobileOrder(216) < lOLevel.GetActiveMobileOrder(202), true);
            fExpectBool("mobile order: 220 guard #197 before #247",
                lOLevel.GetActiveMobileOrder(197) < lOLevel.GetActiveMobileOrder(247), true);
            fExpectInt("mobile order: #197 is the second entry", lOLevel.GetActiveMobileOrder(197), 1);
        }

        private static void fCheckScatter()
        {
            Check.Group("Scatter around a point");

            int liX;
            int liY;
            int liMinX = int.MaxValue, liMaxX = int.MinValue;

            for (int liRun = 0; liRun < 2000; liRun++)
            {
                UWScatterRules.TryPickSpot(100, 200, UWScatterRules.LootRadius, (x, y) => true, out liX, out liY);
                liMinX = Math.Min(liMinX, liX);
                liMaxX = Math.Max(liMaxX, liX);
            }

            fExpectInt("scatter: loot reaches centre - 6", liMinX, 94);
            fExpectInt("scatter: loot reaches centre + 6", liMaxX, 106);
            fExpectBool("scatter: no spot fits after 24 tries",
                UWScatterRules.TryPickSpot(100, 200, 4, (x, y) => false, out liX, out liY), false);
            fExpectInt("scatter: then the point itself", liX * 1000 + liY, 100200);
        }

        private static void fCheckObjectLimit(DataImport pOData)
        {
            Check.Group("Object limit (GetFreeObject, CullObjects)");

            fExpectBool("limit: range 3 spares the player's own tile",
                UWObjectLimitRules.TileQualifies(10, 10, 10, 10, UWObjectLimitRules.CullRange), false);
            fExpectBool("limit: range 3 takes distance 8 (the apples on 50/2, per the original)",
                UWObjectLimitRules.TileQualifies(14, 14, 10, 10, UWObjectLimitRules.CullRange), true);
            fExpectBool("limit: range 3 spares distance 7 (51/2 kept its apples)",
                UWObjectLimitRules.TileQualifies(13, 14, 10, 10, UWObjectLimitRules.CullRange), false);
            fExpectBool("limit: range 3 takes distance 8 in a column too",
                UWObjectLimitRules.TileQualifies(10, 18, 10, 10, UWObjectLimitRules.CullRange), true);

            UWCommonObjectProperties lOProperties = pOData.CommonObjectProperties;
            UWObject lOPotion = new UWObject(0xBB);
            UWObject lOKey = new UWObject(0x100);
            UWObject lOProtected = new UWObject(0xB3) { DoorDirection = true };

            fExpectBool("limit: a potion stays at range 3 (priority 6 - none went in the original)",
                UWObjectLimitRules.Culls(lOPotion, UWObjectLimitRules.CullRange, lOProperties, null), false);
            fExpectBool("limit: a key stays at range 3 (priority 12)",
                UWObjectLimitRules.Culls(lOKey, UWObjectLimitRules.CullRange, lOProperties, null), false);
            fExpectBool("limit: word 0 bit 13 protects",
                UWObjectLimitRules.Culls(lOProtected, UWObjectLimitRules.CullRange, lOProperties, null), false);

            bool lbFits = true;
            string lsCounts = string.Empty;

            for (int liLevel = 0; pOData.Levels != null && liLevel < pOData.Levels.Count; liLevel++)
            {
                int liMobile;
                int liStatic;

                UWObjectLimitRules.CountUsage(pOData.Levels[liLevel], out liMobile, out liStatic);
                lsCounts += string.Format(" {0}:{1}/{2}", liLevel + 1, liMobile, liStatic);
                lbFits &= liMobile <= UWObjectLimitRules.MobileSlots && liStatic <= UWObjectLimitRules.StaticSlots;
            }

            fExpectBool("limit: every fresh level fits its slots (mobile/static)" + lsCounts, lbFits, true);
        }

        /// <summary>
        /// A CREATURE MADE IN CODE (per user, 2026-09-27: a summoned monster turned hostile): the
        /// record must not read zeros where the object has properties, and a new creature starts
        /// as ovr101_5E sets it up.
        /// </summary>
        private static void fCheckNewCreature()
        {
            Check.Group("New creatures");

            UWNpc lONpc = new UWNpc(70) { NPCAttitude = 3, NPCIsAlly = true, NPCGoal = 4 };
            UWCritterRecord lORecord = new UWCritterRecord(lONpc);

            fExpectInt("creature: without raw bytes the attitude comes from the object", lORecord.Attitude, 3);
            fExpectBool("creature: ... and the ally bit", lORecord.IsAlly, true);
            fExpectInt("creature: ... and the goal", lORecord.Goal, 4);

            lORecord.InitialiseAsNew(40, 0);

            fExpectInt("creature: new, attitude 2", lORecord.Attitude, 2);
            fExpectInt("creature: new, goal 8", lORecord.Goal, 8);
            fExpectBool("creature: new, no ally bit of its own", lORecord.IsAlly, false);
            fExpectInt("creature: new, hit points vitality * 0x10 / 0x20 at the lowest roll", lORecord.HitPoints, 20);
            fExpectInt("creature: new, home post x 0x20", lORecord.HomeX, 0x20);
            fExpectInt("creature: new, home post y 0x20", lORecord.HomeY, 0x20);
        }

        /// <summary>UWTrapChainRemoval on the lever of level 3, 52/13 (per user, 2026-10-01): its use
        /// trigger is one-shot, and the original's save after it shows the whole chain gone - the
        /// lever's link 0, the trigger, the three change terrain traps, the text trap and both move
        /// triggers on the free list, the traps out of 44/12, 44/8 and 46/7. The check changes
        /// the level; the runner reloads the world afterwards (MutatesData).</summary>
        private static void fCheckOneShotChainRemoval(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 2 ? pOData.Levels[2] : null;
            UWObject lOLever = lOLevel?.GetTile(52, 13)?.ObjectsInTile?.Find(pOAt => pOAt.ID == 0x175);

            if (lOLever == null)
            {
                fFail("one-shot chain: the lever on level 3, 52/13", "not found");
                return;
            }

            List<UWObject> lOMaster = lOLevel.Masterlist;
            UWObject lOTrigger = UWObjectMechanics.GetLinkedObject(lOLever, lOMaster);
            UWObject lOTrap = lOTrigger != null ? UWObjectMechanics.GetLinkedObject(lOTrigger, lOMaster) : null;
            List<int> liChain = new List<int>();

            for (UWObject lOAt = lOTrigger; lOAt != null && liChain.Count < 16; lOAt = UWObjectMechanics.GetLinkedObject(lOAt, lOMaster))
                liChain.Add(lOMaster.IndexOf(lOAt));

            fExpectInt("one-shot chain: trigger, three terrain traps, text trap, two move triggers", liChain.Count, 7);

            UWTrapRules.RemoveOneShotTrap(lOTrigger, lOTrap, lOLevel);

            fExpectInt("one-shot chain: the lever's link is 0", lOLever.Quantity, 0);
            fExpectBool("one-shot chain: every object of it is on the free list",
                liChain.TrueForAll(piAt => lOLevel.IsStaticSlotFree(piAt)), true);

            foreach ((int liX, int liY) in new[] { (44, 12), (44, 8), (46, 7) })
                fExpectBool(string.Format("one-shot chain: no terrain trap left in {0}/{1}", liX, liY),
                    lOLevel.GetTile(liX, liY).ObjectsInTile.Exists(pOAt => pOAt.ID == UWObjectMechanics.ChangeTerrainTrapId), false);
        }
    }
}
