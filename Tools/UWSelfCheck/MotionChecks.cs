using System;
using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE MOTION CORE ON A SMALL MAP (stage 1 of the motion rework, 2026-10-05): real motion
    /// steps of thrown objects on synthetic tiles with the real COMOBJ.DAT, a fake world that
    /// records what the host would do, and the original's own RNG so a run is repeatable. These
    /// are the first checks that move anything; until then the self-check only scripted the
    /// outcome of a step.
    /// </summary>
    public static partial class Program
    {
        /// <summary>A 32 x 32 map: solid border, open floor of height 2 inside, and whatever a
        /// check paints onto it. Bodies per tile, a host that only remembers.</summary>
        private sealed class FakeMotionWorld : IUWMotionWorld, IUWMobileObjectHost
        {
            public const int Size = 32;

            public readonly int[,] Type = new int[Size, Size];

            public readonly int[,] Floor = new int[Size, Size];

            public readonly int[,] Terrain = new int[Size, Size];

            private readonly Dictionary<int, List<UWMotionBody>> mOBodies = new Dictionary<int, List<UWMotionBody>>();

            public UWOriginalRandom Rng = new UWOriginalRandom(1234);

            public int Level = 1;

            public int Phase;

            public readonly List<string> Log = new List<string>();

            public int Splashes;

            public int Removed;

            public int Rested;

            public int RestTileX;

            public int RestTileY;

            public int RestXPos;

            public int RestYPos;

            public int RestZPos;

            public int RestOctant;

            public int RestQuality;

            public int LandingSounds;

            public int Pushes;

            public FakeMotionWorld()
            {
                for (int liY = 0; liY < Size; liY++)
                {
                    for (int liX = 0; liX < Size; liX++)
                    {
                        bool lbBorder = liX == 0 || liY == 0 || liX == Size - 1 || liY == Size - 1;

                        Type[liX, liY] = lbBorder ? 0 : 1;
                        Floor[liX, liY] = 2;
                    }
                }
            }

            public void Paint(int piX0, int piY0, int piX1, int piY1, int piType, int piFloor, int piTerrain)
            {
                for (int liY = piY0; liY <= piY1; liY++)
                {
                    for (int liX = piX0; liX <= piX1; liX++)
                    {
                        Type[liX, liY] = piType;
                        Floor[liX, liY] = piFloor;
                        Terrain[liX, liY] = piTerrain;
                    }
                }
            }

            public void AddBody(int piTileX, int piTileY, UWMotionBody pOBody)
            {
                int liKey = (piTileY * Size) + piTileX;

                if (!mOBodies.ContainsKey(liKey))
                    mOBodies[liKey] = new List<UWMotionBody>();

                mOBodies[liKey].Add(pOBody);
            }

            public bool TryGetTile(int piTileX, int piTileY, out int piType, out int piFloorNibble, out int piTerrain)
            {
                piType = 0;
                piFloorNibble = 0;
                piTerrain = 0;

                if (piTileX < 0 || piTileY < 0 || piTileX >= Size || piTileY >= Size)
                    return false;

                piType = Type[piTileX, piTileY];
                piFloorNibble = Floor[piTileX, piTileY];
                piTerrain = Terrain[piTileX, piTileY];

                return true;
            }

            public void GetBodiesInTile(int piTileX, int piTileY, List<UWMotionBody> pOInto)
            {
                List<UWMotionBody> lOList;

                if (piTileX >= 0 && piTileY >= 0 && piTileX < Size && piTileY < Size
                    && mOBodies.TryGetValue((piTileY * Size) + piTileX, out lOList))
                    pOInto.AddRange(lOList);
            }

            public int Random()
            {
                return Rng.Next();
            }

            public void UseOnMover(int piOtherIndex, int piMoverIndex, int piHitTileX, int piHitTileY)
            {
                Log.Add("use " + piOtherIndex + " on " + piMoverIndex);
            }

            public int TriggerMove(int piMoverIndex, int piOtherIndex, int piHitTileX, int piHitTileY)
            {
                Log.Add("trigger " + piOtherIndex);

                return 2;
            }

            public bool MissileHits(int piMoverIndex, int piOtherIndex, int piMoverTileX, int piMoverTileY)
            {
                Log.Add("missile " + piMoverIndex + " hits " + piOtherIndex);

                return false;
            }

            public void PushObject(int piOtherIndex, int piHeading, int piSpeed, int piVz, int piHitTileX, int piHitTileY)
            {
                Pushes++;
                Log.Add("push " + piOtherIndex + " heading " + piHeading + " vz " + piVz);
            }

            public void PlaySoundAt(int piSound, int piX8, int piY8, int piVolume)
            {
                if (piSound == 0x0F)
                    LandingSounds++;

                Log.Add("sound " + piSound);
            }

            public int DungeonLevel => Level;

            public int ClockPhase => Phase;

            public void MoveToTile(UWMobileRecord pORecord, int piOldTileX, int piOldTileY, int piNewTileX, int piNewTileY)
            {
            }

            public bool DamageSelf(UWMobileRecord pORecord, int piDamage, int piType)
            {
                Log.Add("damage " + piDamage + " type " + piType);

                return false;
            }

            public void PlaySoundAtObject(int piSound, UWMobileRecord pORecord, int piVolume)
            {
                Log.Add("sound at object " + piSound);
            }

            public void Splash(UWMobileRecord pORecord)
            {
                Splashes++;
            }

            public bool TalismanBurns(UWMobileRecord pORecord, int piTileX, int piTileY)
            {
                return false;
            }

            public void OnDemoted(UWMobileRecord pORecord)
            {
            }

            public int Detonate(UWMobileRecord pORecord, int piTileX, int piTileY, int piOwner)
            {
                Log.Add("detonate at " + piTileX + "/" + piTileY);

                return 0;
            }

            public void Remove(UWMobileRecord pORecord)
            {
                Removed++;
            }

            public void PlaceAtRest(UWMobileRecord pORecord, int piOctant, int piQuality)
            {
                Rested++;
                RestTileX = pORecord.TileX;
                RestTileY = pORecord.TileY;
                RestXPos = pORecord.XPos;
                RestYPos = pORecord.YPos;
                RestZPos = pORecord.ZPos;
                RestOctant = piOctant;
                RestQuality = piQuality;
            }
        }

        /// <summary>A thrown item of the player from tile 8/8, eighth 4/4, on the floor, heading
        /// north (+y) with the pitch; false when the launch finds no room.</summary>
        private static bool fThrow(FakeMotionWorld pOWorld, UWMobileObjectMotion pOMotion, DataImport pOData,
            int piItemId, int piPitch, int piHeading8, out UWMobileRecord pORecord, int piLauncherZPos = 16)
        {
            UWCommonObjectProperties.Entry lOPlayer;

            pOData.CommonObjectProperties.TryGet(UWObjectMechanics.AdventurerObjectId, out lOPlayer);

            pORecord = new UWMobileRecord { Object = new UWObject((ushort)piItemId), Index = 1024 };

            UWMobileObjectMotion.Launcher lOLauncher = new UWMobileObjectMotion.Launcher
            {
                Index = 1,
                IsCreature = true,
                IsPlayer = true,
                TileX = 8,
                TileY = 8,
                XPos = 4,
                YPos = 4,
                ZPos = piLauncherZPos,
                Octant = (piHeading8 >> 5) & 7,
                FineHeadingBits = piHeading8 & 0x1F,
                Height = lOPlayer.Height,
                Radius = lOPlayer.Radius
            };

            if (!pOMotion.TryLaunch(pORecord, lOLauncher, true, 0, piPitch, 0x0F))
                return false;

            pORecord.Hp = 0x28;

            return true;
        }

        /// <summary>Steps until the object rests or is removed; the steps taken, or -1 when it
        /// never stopped.</summary>
        private static int fFly(FakeMotionWorld pOWorld, UWMobileObjectMotion pOMotion, UWMobileRecord pORecord, out int piLowestZ)
        {
            piLowestZ = int.MaxValue;

            for (int liStep = 0; liStep < 600; liStep++)
            {
                if (!pOMotion.Step(pORecord))
                    return liStep + 1;

                if (pORecord.ZPos < piLowestZ)
                    piLowestZ = pORecord.ZPos;
            }

            return -1;
        }

        /// <summary>The things thrown here: 0x20, a piece of armour (COMOBJ: height 0, radius 2,
        /// elasticity 1, never breaks, culling priority 7 - the water takes it), and 0xE1, a rune
        /// stone (radius 1, elasticity 9, priority 15 - nothing takes it). Every carried thing has
        /// height 0 in COMOBJ.DAT: a point in height, a box across.</summary>
        private const int CommonThing = 0x20;

        private const int ValuableThing = 0xE1;

        private static void fCheckObjectMotion(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Object motion (the engine-free core on a small map)");

            // The sine table: the binary's own words, not a rounded sine (entry 45 would be 29268).
            int liSin;
            int liCos;

            UWMotionTables.SinCos(0x100, out liSin, out liCos);
            fExpectInt("motion: sine of one step", liSin, 804);
            UWMotionTables.SinCos(0x2D00, out liSin, out liCos);
            fExpectInt("motion: sine entry 45 is the table's 29269", liSin, 29269);
            UWMotionTables.SinCos(0x4000, out liSin, out liCos);
            fExpectInt("motion: sine of a quarter turn", liSin, 32767);
            fExpectInt("motion: cosine of a quarter turn", liCos, 0);
            UWMotionTables.SinCos(0x0080, out liSin, out liCos);
            fExpectInt("motion: half a step interpolates to 402", liSin, 402);

            // GetCoordinateInDirection: the table over 0x80, times the distance, over 0x100, then
            // pushed away from zero.
            int liX = 100;
            int liY = 100;

            UWMobileObjectMotion.StepInDirection(0, 7, ref liX, ref liY);
            fExpectInt("motion: a step north of 7 moves y by 7", liY, 107);
            fExpectInt("motion: ... and x not at all", liX, 100);
            UWMobileObjectMotion.StepInDirection(0x40, 7, ref liX, ref liY);
            fExpectInt("motion: a step east of 7 moves x by 7", liX, 107);
            UWMobileObjectMotion.StepInDirection(0x20, 7, ref liX, ref liY);
            fExpectInt("motion: a step north-east of 7 moves x by 5", liX, 112);
            fExpectInt("motion: ... and y by 5", liY, 112);

            if (pOData == null || pOData.CommonObjectProperties == null)
            {
                fFail("motion", "no COMOBJ.DAT");

                return;
            }

            UWCommonObjectProperties.Entry lOCommon;
            UWCommonObjectProperties.Entry lOValuable;

            pOData.CommonObjectProperties.TryGet(CommonThing, out lOCommon);
            pOData.CommonObjectProperties.TryGet(ValuableThing, out lOValuable);
            fExpectBool("motion: the common thing is what the water takes",
                lOCommon.ProjectileCulling == 0 && UWLiquidCulling.AlwaysSwallows(lOCommon.CullingPriority), true);
            fExpectBool("motion: the valuable thing is what it keeps",
                lOValuable.ProjectileCulling == 0 && UWLiquidCulling.NeverSwallows(lOValuable.CullingPriority), true);

            fCheckThrowAhead(pOData, CommonThing);
            fCheckWallMirror(pOData, CommonThing);
            fCheckWater(pOData, CommonThing, ValuableThing);
            fCheckDropOff(pOData, CommonThing);
            fCheckStraightDrop(pOData, CommonThing);
            fCheckLaunchRoom(pOData, CommonThing);
            fCheckSchedule(pOData, CommonThing);
            fCheckMissileAtCreature(pOData);
        }

        private static void fCheckThrowAhead(DataImport pOData, int piItemId)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWMobileObjectMotion lOMotion = new UWMobileObjectMotion(lOWorld, lOWorld, pOData.CommonObjectProperties);
            UWMobileRecord lORecord;

            fExpectBool("motion: a throw on open floor finds room", fThrow(lOWorld, lOMotion, pOData, piItemId, 2, 0, out lORecord), true);

            int liStartY = (lORecord.TileY * 8) + lORecord.YPos;
            int liLowest;
            int liSteps = fFly(lOWorld, lOMotion, lORecord, out liLowest);

            fExpectBool("motion: the thrown thing comes to rest", liSteps > 0, true);
            fExpectInt("motion: it is placed as a lying object", lOWorld.Rested, 1);
            fExpectInt("motion: nothing removed it", lOWorld.Removed, 0);
            fExpectBool("motion: it lies ahead of the thrower (north)", (lOWorld.RestTileY * 8) + lOWorld.RestYPos > liStartY + 8, true);
            fExpectInt("motion: it rests on the floor", lOWorld.RestZPos, 16);
            fExpectBool("motion: it never went below the floor", liLowest >= 16, true);
            fExpectBool("motion: the floor sounded at least once", lOWorld.LandingSounds >= 1, true);
            fExpectInt("motion: it keeps its quality", lOWorld.RestQuality, 0x28);

            // The same seed, the same flight: repeatable.
            FakeMotionWorld lOAgain = new FakeMotionWorld();
            UWMobileObjectMotion lOMotionAgain = new UWMobileObjectMotion(lOAgain, lOAgain, pOData.CommonObjectProperties);
            UWMobileRecord lORecordAgain;

            fThrow(lOAgain, lOMotionAgain, pOData, piItemId, 2, 0, out lORecordAgain);

            int liStepsAgain = fFly(lOAgain, lOMotionAgain, lORecordAgain, out liLowest);

            fExpectBool("motion: the same seed gives the same flight",
                liStepsAgain == liSteps && lOAgain.RestTileX == lOWorld.RestTileX && lOAgain.RestTileY == lOWorld.RestTileY
                && lOAgain.RestXPos == lOWorld.RestXPos && lOAgain.RestYPos == lOWorld.RestYPos, true);
        }

        private static void fCheckWallMirror(DataImport pOData, int piItemId)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWMobileObjectMotion lOMotion = new UWMobileObjectMotion(lOWorld, lOWorld, pOData.CommonObjectProperties);
            UWMobileRecord lORecord;

            // A wall across the way two tiles ahead.
            lOWorld.Paint(1, 11, FakeMotionWorld.Size - 2, 11, 0, 0, 0);

            fThrow(lOWorld, lOMotion, pOData, piItemId, 1, 0, out lORecord);

            bool lbMirrored = false;
            bool lbInsideWall = false;

            for (int liStep = 0; liStep < 600 && !lbInsideWall; liStep++)
            {
                if (!lOMotion.Step(lORecord))
                    break;

                if (lORecord.HeadingByte == 0x80)
                    lbMirrored = true;

                lbInsideWall = lORecord.TileY >= 11;
            }

            fExpectBool("motion: never inside the wall", lbInsideWall, false);
            fExpectBool("motion: a head-on wall hit mirrors the heading (north to south)", lbMirrored, true);
            fExpectInt("motion: it rests before the wall", lOWorld.Rested, 1);
            fExpectBool("motion: ... in front of it", lOWorld.RestTileY < 11, true);
        }

        private static void fCheckWater(DataImport pOData, int piCommon, int piValuable)
        {
            // Water from two tiles ahead on: the common thing splashes and is gone, the valuable
            // one splashes and lies on the water floor.
            for (int liRound = 0; liRound < 2; liRound++)
            {
                FakeMotionWorld lOWorld = new FakeMotionWorld();
                UWMobileObjectMotion lOMotion = new UWMobileObjectMotion(lOWorld, lOWorld, pOData.CommonObjectProperties);
                UWMobileRecord lORecord;

                lOWorld.Paint(1, 10, FakeMotionWorld.Size - 2, FakeMotionWorld.Size - 2, 1, 2, 1);

                int liItem = liRound == 0 ? piCommon : piValuable;

                fThrow(lOWorld, lOMotion, pOData, liItem, 2, 0, out lORecord);

                int liLowest;

                fFly(lOWorld, lOMotion, lORecord, out liLowest);

                string lsWhat = liRound == 0 ? "a common thing" : "a valuable thing";

                fExpectInt("motion: " + lsWhat + " thrown into water splashes", lOWorld.Splashes, 1);
                fExpectInt("motion: " + lsWhat + " is " + (liRound == 0 ? "taken by the water" : "not taken"),
                    lOWorld.Removed, liRound == 0 ? 1 : 0);
                fExpectInt("motion: " + lsWhat + (liRound == 0 ? " does not lie there" : " lies on the water floor"),
                    lOWorld.Rested, liRound == 0 ? 0 : 1);
            }
        }

        private static void fCheckDropOff(DataImport pOData, int piItemId)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWMobileObjectMotion lOMotion = new UWMobileObjectMotion(lOWorld, lOWorld, pOData.CommonObjectProperties);
            UWMobileRecord lORecord;

            // The thrower stands on a plateau of height 6 that ends two tiles ahead; beyond it the
            // floor is 2 - the thing lands on the plateau or flies over its edge, and comes to rest
            // on the lower floor.
            lOWorld.Paint(1, 1, FakeMotionWorld.Size - 2, 10, 1, 6, 0);

            UWCommonObjectProperties.Entry lOPlayer;

            pOData.CommonObjectProperties.TryGet(UWObjectMechanics.AdventurerObjectId, out lOPlayer);

            lORecord = new UWMobileRecord { Object = new UWObject((ushort)piItemId), Index = 1024 };

            UWMobileObjectMotion.Launcher lOLauncher = new UWMobileObjectMotion.Launcher
            {
                Index = 1, IsCreature = true, IsPlayer = true, TileX = 8, TileY = 8, XPos = 4, YPos = 4, ZPos = 48,
                Octant = 0, FineHeadingBits = 0, Height = lOPlayer.Height, Radius = lOPlayer.Radius
            };

            fExpectBool("motion: a throw from the plateau finds room", lOMotion.TryLaunch(lORecord, lOLauncher, true, 0, 1, 0x0F), true);

            int liLowest;
            int liSteps = fFly(lOWorld, lOMotion, lORecord, out liLowest);

            fExpectBool("motion: the thing comes to rest beyond the edge", liSteps > 0 && lOWorld.Rested == 1, true);
            fExpectBool("motion: ... on the lower floor", lOWorld.RestTileY > 10, true);
            fExpectInt("motion: ... at the lower floor's height", lOWorld.RestZPos, 16);
        }

        private static void fCheckStraightDrop(DataImport pOData, int piItemId)
        {
            // The rounding of a descending step: with gravity the vertical speed becomes -256,
            // the rate 64, dz -0x1000 - an exact multiple of 0x800, which the original rounds to
            // THREE coarse z instead of two (motion-core.md 9, quirk 4).
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWMobileObjectMotion lOMotion = new UWMobileObjectMotion(lOWorld, lOWorld, pOData.CommonObjectProperties);

            lOWorld.Paint(1, 1, FakeMotionWorld.Size - 2, FakeMotionWorld.Size - 2, 1, 0, 0);

            UWMobileRecord lORecord = new UWMobileRecord
            {
                Object = new UWObject((ushort)piItemId), Index = 1024, TileX = 8, TileY = 8, XPos = 4, YPos = 4, ZPos = 40,
                FineX = (8 << 8) + (4 << 5) + 0xF, FineY = (8 << 8) + (4 << 5) + 0xF, FineZ = 40 << 3,
                HeadingByte = 0, SpeedByte = 0, GravityBit = true, VzField = 13, Period = 1, Phase = 1, Hp = 0x28
            };

            fExpectBool("motion: a straight drop steps", lOMotion.Step(lORecord), true);
            fExpectInt("motion: ... three coarse z in the first step, not two", lORecord.ZPos, 37);
            fExpectInt("motion: ... keeps its place", lORecord.XPos, 4);
            fExpectInt("motion: ... stores the vertical speed in steps of 64", lORecord.VzField, 12);
        }

        private static void fCheckLaunchRoom(DataImport pOData, int piItemId)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWMobileObjectMotion lOMotion = new UWMobileObjectMotion(lOWorld, lOWorld, pOData.CommonObjectProperties);
            UWMobileRecord lORecord;
            UWCommonObjectProperties.Entry lOPlayer;
            UWCommonObjectProperties.Entry lOItem;

            pOData.CommonObjectProperties.TryGet(UWObjectMechanics.AdventurerObjectId, out lOPlayer);
            pOData.CommonObjectProperties.TryGet(piItemId, out lOItem);

            // The start lies both radii plus four eighths ahead.
            fThrow(lOWorld, lOMotion, pOData, piItemId, 0, 0, out lORecord);

            int liExpected = (8 * 8) + 4 + lOPlayer.Radius + lOItem.Radius + 4;

            fExpectInt("motion: the start lies radii + 4 eighths ahead", (lORecord.TileY * 8) + lORecord.YPos, liExpected);
            fExpectInt("motion: ... five sixths of the thrower's height up", lORecord.ZPos, 16 + ((lOPlayer.Height * 5) / 6));
            fExpectInt("motion: ... due one unit after the clock", lORecord.Phase, 1);
            fExpectInt("motion: ... every unit", lORecord.Period, 1);

            // A wall right ahead: no room.
            FakeMotionWorld lOBlocked = new FakeMotionWorld();
            UWMobileObjectMotion lOMotionBlocked = new UWMobileObjectMotion(lOBlocked, lOBlocked, pOData.CommonObjectProperties);

            lOBlocked.Paint(1, 9, FakeMotionWorld.Size - 2, 9, 0, 0, 0);
            fExpectBool("motion: a throw into a wall finds no room", fThrow(lOBlocked, lOMotionBlocked, pOData, piItemId, 0, 0, out lORecord), false);

            // A man-sized creature standing at the start (height 23, up to the missile's z): no
            // room either. A short one is flown over - a level fireball passes a bloodworm.
            FakeMotionWorld lOCrowded = new FakeMotionWorld();
            UWMobileObjectMotion lOMotionCrowded = new UWMobileObjectMotion(lOCrowded, lOCrowded, pOData.CommonObjectProperties);

            lOCrowded.AddBody(8, 9, new UWMotionBody { Index = 7, ItemId = UWObjectMechanics.AdventurerObjectId, XPos = 4, YPos = 3, ZPos = 16, IsMobile = true, IsCreature = true });
            fExpectBool("motion: a throw into a man-sized creature finds no room", fThrow(lOCrowded, lOMotionCrowded, pOData, piItemId, 0, 0, out lORecord), false);

            FakeMotionWorld lOLow = new FakeMotionWorld();
            UWMobileObjectMotion lOMotionLow = new UWMobileObjectMotion(lOLow, lOLow, pOData.CommonObjectProperties);

            lOLow.AddBody(8, 9, new UWMotionBody { Index = 7, ItemId = UWObjectMechanics.AdventurerObjectId, XPos = 4, YPos = 3, ZPos = 2, IsMobile = true, IsCreature = true });
            fExpectBool("motion: a throw over a creature's head finds room", fThrow(lOLow, lOMotionLow, pOData, piItemId, 0, 0, out lORecord), true);
        }

        private static void fCheckSchedule(DataImport pOData, int piItemId)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWMobileObjectMotion lOMotion = new UWMobileObjectMotion(lOWorld, lOWorld, pOData.CommonObjectProperties);
            UWMobileRecord lORecord;
            UWCritterClock lOClock = new UWCritterClock();

            fThrow(lOWorld, lOMotion, pOData, piItemId, 3, 0, out lORecord);

            // Launched at clock 0 it is due at 1 - and the due test wants the clock PAST the
            // phase, so the first unit leaves it, the second steps it (seg007_1798_3825).
            lOClock.Advance(16, false);
            fExpectBool("motion: the first clock unit leaves a launched missile", lOMotion.RunDueSteps(lORecord, lOClock), true);
            fExpectInt("motion: ... its phase stays", lORecord.Phase, 1);
            lOClock.Advance(16, false);
            lOMotion.RunDueSteps(lORecord, lOClock);
            fExpectInt("motion: the second unit steps it, a unit on", lORecord.Phase, 2);

            // Speed halves the world: 32 ticks are one unit.
            lOClock.Advance(32, true);
            lOMotion.RunDueSteps(lORecord, lOClock);
            fExpectInt("motion: under Speed 32 ticks are one step", lORecord.Phase, 3);
        }
        /// <summary>
        /// A magic missile (0x17: weightless, elasticity 0, culling 8) at a goblin on the floor, shot
        /// level or a little down from the player's launch height (five sixths of 23 = 19, the goblin
        /// 20 high): the hit is reported once, the bounce of elasticity 0 leaves it no speed, it comes
        /// to rest at once and the landing's culling removes it - it vanishes at the creature. Acid
        /// (0x16, not weightless) falls at the creature and is removed where it lands. Shot from
        /// above onto a creature's HEAD the vertical pass lands on it and, the creature being nothing
        /// to stand on, the missile HOPS off (DoCollision, see the hop in UWMotionCore) and vanishes
        /// where it lands - the mountainman is 18 high, one below the launch height, so a shot a
        /// little down at one lands on its head (per user, 2026-10-05: the missile bounced off).
        /// </summary>
        private static void fCheckMissileAtCreature(DataImport pOData)
        {
            foreach (int liCase in new[] { 0x1700, 0x17FF, 0x17FE, 0x1600 })
            {
                int liItemId = liCase >> 8;
                int liPitch = (sbyte)(liCase & 0xFF);
                FakeMotionWorld lOWorld = new FakeMotionWorld();
                UWMobileObjectMotion lOMotion = new UWMobileObjectMotion(lOWorld, lOWorld, pOData.CommonObjectProperties);
                UWMobileRecord lORecord;

                lOWorld.AddBody(8, 10, new UWMotionBody { Index = 5, ItemId = GoblinKind, XPos = 4, YPos = 4, ZPos = 16, IsMobile = true, IsCreature = true });

                fThrow(lOWorld, lOMotion, pOData, liItemId, liPitch, 0, out lORecord);

                int liLowest;
                int liSteps = fFly(lOWorld, lOMotion, lORecord, out liLowest);
                string lsName = (liItemId == 0x17 ? "magic missile" : "acid") + " at pitch " + liPitch;

                fExpectBool("motion: the " + lsName + " hits the creature", lOWorld.Log.Contains("missile 1024 hits 5"), true);
                fExpectInt("motion: ... is removed", lOWorld.Removed, 1);
                fExpectInt("motion: ... never lies down", lOWorld.Rested, 0);
                fExpectBool("motion: ... within " + (liItemId == 0x17 ? "six" : "twelve") + " steps (" + liSteps + ")",
                    liSteps > 0 && liSteps <= (liItemId == 0x17 ? 6 : 12), true);
                fExpectBool("motion: ... at the creature's tile", lORecord.TileY == 10, true);
            }

            // From a ledge onto the goblin's head: the hop.
            FakeMotionWorld lOAbove = new FakeMotionWorld();
            UWMobileObjectMotion lOMotionAbove = new UWMobileObjectMotion(lOAbove, lOAbove, pOData.CommonObjectProperties);
            UWMobileRecord lORecordAbove;

            lOAbove.AddBody(8, 10, new UWMotionBody { Index = 5, ItemId = GoblinKind, XPos = 4, YPos = 4, ZPos = 16, IsMobile = true, IsCreature = true });
            fThrow(lOAbove, lOMotionAbove, pOData, 0x17, -5, 0, out lORecordAbove, 40);

            bool lbHopped = false;

            for (int liStep = 0; liStep < 60; liStep++)
            {
                if (!lOMotionAbove.Step(lORecordAbove))
                    break;

                if (lORecordAbove.SpeedByte == 5 && lORecordAbove.GravityBit)
                    lbHopped = true;
            }

            fExpectBool("motion: a magic missile coming down on the creature's head hits it", lOAbove.Log.Contains("missile 1024 hits 5"), true);
            fExpectBool("motion: ... and hops off (speed 5, gravity on)", lbHopped, true);
            fExpectInt("motion: ... and is removed where it lands", lOAbove.Removed, 1);
        }
    }
}
