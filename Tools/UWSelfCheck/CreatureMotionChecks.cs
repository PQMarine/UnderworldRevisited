using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// A CREATURE ON THE MOTION CORE (stage 2 of the motion rework, 2026-10-05): real steps of a
    /// goblin, a bat and a lurker on the small map of the object checks, with the real COMOBJ.DAT
    /// and critter table and the original's RNG. What the land callback decides at a wall, a
    /// door, the player, a water edge, a drop and lava; the flier's ceiling; the swimmer's bank;
    /// the clock under Freeze Time and the easy step; the push.
    /// </summary>
    public static partial class Program
    {
        private const int GoblinKind = 0x47;

        private const int BatKind = 0x41;

        private const int StoneGolemKind = 0x78;

        private const int ClosedDoorId = 0x140;

        /// <summary>A creature record of a kind at a tile, eighth and height, walking: heading
        /// 0 (+y), interval 4, pitch level, animation 0x2C.</summary>
        private static UWCritterRecord fMakeCreature(int piKind, int piTileX, int piTileY, int piFineX, int piFineY,
            int piZPos, int piHeading, int piSpeed)
        {
            UWNpc lONpc = new UWNpc((ushort)piKind);
            UWCritterRecord lORecord = new UWCritterRecord(lONpc);

            lONpc.TileX = piTileX;
            lONpc.TileY = piTileY;
            lORecord.TileX = piTileX;
            lORecord.TileY = piTileY;
            lORecord.FineX = piFineX;
            lORecord.FineY = piFineY;
            lORecord.ZPos = piZPos;
            lORecord.FineHeading = piHeading;
            lORecord.Speed = piSpeed;
            lORecord.Interval = UWCritterRules.IntervalWalking;
            lORecord.Pitch = UWCritterRules.FlierPitchLevel;
            lORecord.Animation = UWCritterBrain.AnimWalking;
            lORecord.IsStanding = false;

            return lORecord;
        }

        private static int fFindKind(DataImport pOData, bool pbFlier, bool pbSwimmer)
        {
            for (int liId = 0x40; liId < 0x80; liId++)
            {
                UWObjectClassProperties.Critter lORow;

                if (pOData.ObjectClassProperties.TryGetCritter(liId, out lORow) && lORow.IsFlier == pbFlier && lORow.IsSwimmer == pbSwimmer)
                    return liId;
            }

            return -1;
        }

        private static void fCheckCreatureMotion(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Creature motion (the engine-free core, stage 2)");

            if (pOData == null || pOData.CommonObjectProperties == null || pOData.ObjectClassProperties == null)
            {
                fFail("creature motion", "no COMOBJ.DAT or OBJECTS.DAT");

                return;
            }

            UWObjectClassProperties.Critter lOGoblin;

            fExpectBool("creature motion: the goblin walks", pOData.ObjectClassProperties.TryGetCritter(GoblinKind, out lOGoblin)
                && UWCreatureMotion.KindOf(lOGoblin) == UWCreatureMotion.Kind.Land, true);

            int liFlier = fFindKind(pOData, true, false);
            int liSwimmer = fFindKind(pOData, false, true);

            fExpectBool("creature motion: a flier and a swimmer exist in the table", liFlier > 0 && liSwimmer > 0, true);

            fCheckCreatureWalk(pOData);
            fCheckCreatureWalls(pOData);
            fCheckCreatureObjects(pOData);
            fCheckCreatureWaterEdge(pOData);
            fCheckCreatureDrop(pOData);
            fCheckCreatureLava(pOData);

            if (liFlier > 0)
                fCheckFlier(pOData, liFlier);

            if (liSwimmer > 0)
                fCheckSwimmer(pOData, liSwimmer);

            fCheckCreatureClock();
            fCheckCreaturePush();
            fCheckBumpSetsPath(pOData);
            fCheckSlope(pOData);
            fCheckTileRoute();
            fCheckTileTraverse(pOData);
        }

        private static UWCreatureMotion fNewCreatureMotion(DataImport pOData, out FakeMotionWorld pOWorld)
        {
            UWCreatureMotion.ResetHandlers();
            pOWorld = new FakeMotionWorld();

            return new UWCreatureMotion(pOWorld, pOData.CommonObjectProperties);
        }

        /// <summary>Open floor: speed 8 over 64 ticks is 8 * 47 * 64 / 0x2000 = 2.9 eighths along
        /// +y, so the creature goes from eighth 4 to 6 or 7 (the random spot inside the eighth
        /// decides), stays on its height and in its column, and nothing collides.</summary>
        private static void fCheckCreatureWalk(DataImport pOData)
        {
            FakeMotionWorld lOWorld;
            UWCreatureMotion lOMotion = fNewCreatureMotion(pOData, out lOWorld);
            UWCritterRecord lORecord = fMakeCreature(GoblinKind, 8, 8, 4, 4, 16, 0, 8);

            StepResult lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("creature motion: a walk on open floor collides with nothing", lOStep.Collided, false);
            fExpectInt("creature motion: ... keeps its column", lORecord.FineX, 4);
            fExpectBool("creature motion: ... advances 2.9 eighths along +y", lORecord.TileY == 8 && (lORecord.FineY == 6 || lORecord.FineY == 7), true);
            fExpectInt("creature motion: ... stays on the floor", lORecord.ZPos, 16);
            fExpectInt("creature motion: ... keeps its heading", lORecord.FineHeading, 0);
            fExpectInt("creature motion: ... keeps its speed byte", lORecord.Speed, 8);
            fExpectBool("creature motion: ... is not stuck", lOStep.Stuck, false);
            fExpectInt("creature motion: ... stands on the ground (contact state 0)", lORecord.TileState, 0);

            // Across a tile edge the record's tile changes and the host is told to relink.
            lORecord = fMakeCreature(GoblinKind, 8, 8, 4, 7, 16, 0, 8);
            lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);
            fExpectBool("creature motion: a walk across the tile edge reports the relink", lOStep.TileChanged && lOStep.OldTileY == 8, true);
            fExpectInt("creature motion: ... into the next tile", lORecord.TileY, 9);

            // Standing still is no physics at all.
            lORecord = fMakeCreature(GoblinKind, 8, 8, 4, 4, 16, 0, 0);
            lORecord.IsStanding = true;
            lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);
            fExpectBool("creature motion: standing with speed 0 runs no physics", !lOStep.Collided && lORecord.FineY == 4, true);
        }

        /// <summary>A wall ahead: head-on the step stops before it with the heading kept; at 45
        /// degrees the slide turns the heading along the wall (HeadingDeflected, the core's
        /// TurnAlongWall) and the creature moves sideways instead of stopping.</summary>
        private static void fCheckCreatureWalls(DataImport pOData)
        {
            FakeMotionWorld lOWorld;
            UWCreatureMotion lOMotion = fNewCreatureMotion(pOData, out lOWorld);

            lOWorld.Paint(0, 10, FakeMotionWorld.Size - 1, 10, 0, 2, 0);

            UWCritterRecord lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 4, 16, 0, 8);
            StepResult lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("creature motion: a wall head-on collides", lOStep.Collided, true);
            fExpectBool("creature motion: ... without an object", lOStep.HitObject, false);
            fExpectInt("creature motion: ... keeps the heading", lORecord.FineHeading, 0);
            fExpectBool("creature motion: ... and the creature stays before the wall", lORecord.TileY == 9, true);

            lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 4, 16, 0x20, 8);
            lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("creature motion: a wall at 45 degrees deflects the heading", lOStep.HeadingDeflected, true);
            fExpectBool("creature motion: ... along the wall (east or west)", lORecord.FineHeading == 0x40 || lORecord.FineHeading == 0xC0, true);
            fExpectBool("creature motion: ... and the creature moved sideways", lORecord.FineX != 4 || lORecord.TileX != 8, true);
            fExpectBool("creature motion: ... staying before the wall", lORecord.TileY == 9, true);
        }

        /// <summary>A closed door in the next tile is reported as such with its index; the
        /// player's body as an object hit with item 0x7F.</summary>
        private static void fCheckCreatureObjects(DataImport pOData)
        {
            FakeMotionWorld lOWorld;
            UWCreatureMotion lOMotion = fNewCreatureMotion(pOData, out lOWorld);

            lOWorld.AddBody(8, 10, new UWMotionBody { Index = 50, ItemId = ClosedDoorId, XPos = 4, YPos = 4, ZPos = 16 });

            // Six eighths from the door's centre: the step of 2.9 brings the goblin's radius 2
            // into the door's radius 3.
            UWCritterRecord lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 6, 16, 0, 8);
            StepResult lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("creature motion: a closed door ahead is hit", lOStep.Collided && lOStep.HitObject, true);
            fExpectBool("creature motion: ... and reported as a closed door", lOStep.HitClosedDoor, true);
            fExpectInt("creature motion: ... with its index", lOStep.HitObjectIndex, 50);
            fExpectBool("creature motion: ... not as a creature", lOStep.HitObjectIsCreature, false);
            fExpectInt("creature motion: ... the heading kept", lORecord.FineHeading, 0);

            lOMotion = fNewCreatureMotion(pOData, out lOWorld);
            lOWorld.AddBody(8, 10, new UWMotionBody
            {
                Index = 1,
                ItemId = UWObjectMechanics.AdventurerObjectId,
                XPos = 4,
                YPos = 3,
                ZPos = 16,
                IsMobile = true,
                IsCreature = true
            });

            // Creature against creature the scan takes one off both radii (ScanForCollisions'
            // NPC shrink), so the bodies have to come within 2 eighths: 4 - 2.9.
            lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 7, 16, 0, 8);
            lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("creature motion: the player ahead is hit", lOStep.Collided && lOStep.HitObject, true);
            fExpectInt("creature motion: ... as item 0x7F", lOStep.HitObjectItemId, UWObjectMechanics.AdventurerObjectId);
            fExpectBool("creature motion: ... a creature, not a door", lOStep.HitObjectIsCreature && !lOStep.HitClosedDoor, true);
            fExpectBool("creature motion: ... and the creature stays two eighths off his centre", (10 * 8) + 3 - lORecord.Y >= 2, true);
        }

        /// <summary>Water ahead on the same floor: a creature without a path stops at the edge
        /// (the user's measurement of 2026-09-20: it cannot be pushed in); one with a path walks
        /// on and drowns when water alone is under it.</summary>
        private static void fCheckCreatureWaterEdge(DataImport pOData)
        {
            FakeMotionWorld lOWorld;
            UWCreatureMotion lOMotion = fNewCreatureMotion(pOData, out lOWorld);

            lOWorld.Paint(0, 10, FakeMotionWorld.Size - 1, 12, 1, 2, 1);

            UWCritterRecord lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 4, 16, 0, 8);
            StepResult lOStep = default(StepResult);

            for (int liAt = 0; liAt < 3 && !lOStep.Drowned; liAt++)
                lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("creature motion: water ahead stops a creature without a path", lOStep.Collided && !lOStep.Drowned, true);
            fExpectBool("creature motion: ... before the water", lORecord.TileY == 9, true);

            lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 4, 16, 0, 8);
            lORecord.HasPath = true;

            bool lbDrowned = false;

            for (int liAt = 0; liAt < 4 && !lbDrowned; liAt++)
                lbDrowned = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land).Drowned;

            fExpectBool("creature motion: with a path it walks in and drowns", lbDrowned, true);
            fExpectBool("creature motion: ... in the water tile", lORecord.TileY >= 10, true);
        }

        /// <summary>A ledge of two floor levels ahead: without a path the creature stops at the
        /// edge; with one it steps over, is stuck in the air (gravity on, interval 1) and lands
        /// on the lower floor within a few updates.</summary>
        private static void fCheckCreatureDrop(DataImport pOData)
        {
            FakeMotionWorld lOWorld;
            UWCreatureMotion lOMotion = fNewCreatureMotion(pOData, out lOWorld);

            lOWorld.Paint(0, 10, FakeMotionWorld.Size - 1, FakeMotionWorld.Size - 2, 1, 0, 0);

            UWCritterRecord lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 4, 16, 0, 8);
            StepResult lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("creature motion: a drop ahead stops a creature without a path", lOStep.Collided && !lOStep.Stuck, true);
            fExpectBool("creature motion: ... on the upper floor", lORecord.TileY == 9 && lORecord.ZPos == 16, true);

            lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 4, 16, 0, 8);
            lORecord.HasPath = true;

            bool lbStuck = false;
            bool lbGravity = false;

            for (int liAt = 0; liAt < 3 && !lbStuck; liAt++)
            {
                lbStuck = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land).Stuck;
                lbGravity |= lORecord.Gravity;
            }

            fExpectBool("creature motion: with a path it steps over the edge and is stuck in the air", lbStuck, true);
            fExpectBool("creature motion: ... with gravity on and interval 1", lbGravity && lORecord.Interval == 1, true);

            for (int liAt = 0; liAt < 12 && lORecord.ZPos > 0; liAt++)
                lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectInt("creature motion: ... and lands on the lower floor", lORecord.ZPos, 0);
        }

        /// <summary>Lava ahead stops a goblin without a path; a stone golem walks on - and from
        /// then on the land handler lets every land creature over a lava edge (the original's
        /// static block, modified in place and never restored), until the handlers are reset.</summary>
        private static void fCheckCreatureLava(DataImport pOData)
        {
            FakeMotionWorld lOWorld;
            UWCreatureMotion lOMotion = fNewCreatureMotion(pOData, out lOWorld);

            lOWorld.Paint(0, 10, FakeMotionWorld.Size - 1, 12, 1, 2, 2);

            UWCritterRecord lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 4, 16, 0, 8);
            StepResult lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("creature motion: lava ahead stops a goblin without a path", lOStep.Collided && lORecord.TileY == 9, true);
            fExpectBool("creature motion: the land handler still holds the lava flag", UWCreatureMotion.LavaFlagDropped(UWCreatureMotion.Kind.Land), false);

            UWCommonObjectProperties.Entry lOGolem;

            pOData.CommonObjectProperties.TryGet(StoneGolemKind, out lOGolem);
            fExpectBool("creature motion: the stone golem is lava-proof (COMOBJ byte 8 bit 3)",
                (lOGolem.Resistances & UWCreatureMotion.LavaProofResistanceBit) != 0, true);

            lORecord = fMakeCreature(StoneGolemKind, 8, 9, 4, 4, 16, 0, 8);

            for (int liAt = 0; liAt < 3 && lORecord.TileY == 9; liAt++)
                lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("creature motion: the stone golem walks onto the lava", lORecord.TileY >= 10, true);
            fExpectBool("creature motion: ... and the land handler lost the lava flag for the session",
                UWCreatureMotion.LavaFlagDropped(UWCreatureMotion.Kind.Land), true);

            lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 4, 16, 0, 8);

            for (int liAt = 0; liAt < 3 && lORecord.TileY == 9; liAt++)
                lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("creature motion: now a goblin walks onto the lava too", lORecord.TileY >= 10, true);
            fExpectInt("creature motion: ... with the lava contact state", lORecord.TileState, 2);

            UWCreatureMotion.ResetHandlers();
            lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 4, 16, 0, 8);
            lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);
            fExpectBool("creature motion: after the reset the goblin stops again", lOStep.Collided && lORecord.TileY == 9, true);

            // On lava the burn comes on one update in five: over many updates some, not all.
            lOMotion = fNewCreatureMotion(pOData, out lOWorld);
            lOWorld.Paint(0, 0, FakeMotionWorld.Size - 1, FakeMotionWorld.Size - 1, 1, 2, 2);
            lORecord = fMakeCreature(GoblinKind, 8, 8, 4, 4, 16, 0, 0);
            lORecord.TileState = 2;

            int liBurns = 0;

            for (int liAt = 0; liAt < 40; liAt++)
            {
                lORecord.Speed = 1;
                lORecord.FineHeading = (liAt * 0x40) & 0xFF;

                if (lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land).LavaBurn)
                    liBurns++;
            }

            fExpectBool("creature motion: a creature moving on lava burns on about one update in five", liBurns > 2 && liBurns < 20, true);
        }

        /// <summary>A flier: pitch 18 climbs (vz +128 per tick), pitch 14 sinks but not below
        /// the floor; a raised floor ahead (the step flag 0x100) is the "ceiling" mark of the
        /// flier's callback, which pushes it up (vz 0x80, pitch 18) so the brain climbs over.</summary>
        private static void fCheckFlier(DataImport pOData, int piKind)
        {
            FakeMotionWorld lOWorld;
            UWCreatureMotion lOMotion = fNewCreatureMotion(pOData, out lOWorld);
            UWCritterRecord lORecord = fMakeCreature(piKind, 8, 8, 4, 4, 32, 0, 4);

            lORecord.Pitch = UWCritterRules.FlierPitchUp;

            StepResult lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Flier);

            fExpectBool("creature motion: a flier with pitch 18 climbs", lORecord.ZPos > 32, true);
            fExpectBool("creature motion: ... and is never stuck in the air", lOStep.Stuck, false);

            lORecord = fMakeCreature(piKind, 8, 8, 4, 4, 20, 0, 4);
            lORecord.Pitch = UWCritterRules.FlierPitchDown;

            for (int liAt = 0; liAt < 4; liAt++)
                lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Flier);

            fExpectBool("creature motion: a flier with pitch 14 sinks to the floor and not below", lORecord.ZPos == 16, true);

            lORecord = fMakeCreature(piKind, 8, 8, 4, 4, 0x70, 0, 4);
            lORecord.Pitch = UWCritterRules.FlierPitchUp;

            for (int liAt = 0; liAt < 6; liAt++)
                lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Flier);

            fExpectBool("creature motion: a flier climbing under the ceiling stays below 0x80", lORecord.ZPos < 0x80, true);

            lOWorld.Paint(0, 10, FakeMotionWorld.Size - 1, 12, 1, 6, 0);
            lORecord = fMakeCreature(piKind, 8, 9, 4, 4, 16, 0, 4);

            bool lbCeiling = false;

            for (int liAt = 0; liAt < 4 && !lbCeiling; liAt++)
                lbCeiling = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Flier).TouchedCeiling;

            fExpectBool("creature motion: a raised floor ahead gives a flier the ceiling mark", lbCeiling, true);
        }

        /// <summary>A swimmer: moves through water, and the bank (plain floor ahead) collides.</summary>
        private static void fCheckSwimmer(DataImport pOData, int piKind)
        {
            FakeMotionWorld lOWorld;
            UWCreatureMotion lOMotion = fNewCreatureMotion(pOData, out lOWorld);

            lOWorld.Paint(4, 4, 12, 12, 1, 1, 1);

            UWCritterRecord lORecord = fMakeCreature(piKind, 8, 8, 4, 4, 8, 0, 4);
            lORecord.TileState = 1;

            StepResult lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Swimmer);

            fExpectBool("creature motion: a swimmer moves through the water", !lOStep.Collided && lORecord.FineY > 4, true);

            lORecord = fMakeCreature(piKind, 8, 12, 4, 4, 8, 0, 4);
            lORecord.TileState = 1;

            for (int liAt = 0; liAt < 3 && !lOStep.Collided; liAt++)
                lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Swimmer);

            lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Swimmer);
            fExpectBool("creature motion: the bank ahead collides", lOStep.Collided, true);
            fExpectBool("creature motion: ... and the swimmer stays in the water", lORecord.TileY <= 12, true);
        }

        /// <summary>The clock: under Freeze Time the units are consumed and the phase stands
        /// still; the easy step gives 4 units, 2 under Speed; Speed keeps the odd unit.</summary>
        private static void fCheckCreatureClock()
        {
            UWCritterClock lOClock = new UWCritterClock();

            fExpectInt("creature clock: a frozen frame hands out no units", lOClock.Advance(64, false, true), 0);
            fExpectInt("creature clock: ... the phase stands still", lOClock.Phase, 0);
            fExpectInt("creature clock: ... the game clock runs on", (int)lOClock.Clock, 64);
            fExpectInt("creature clock: the next frame after the freeze gives its own units only", lOClock.Advance(32, false, false), 2);
            fExpectInt("creature clock: ... phase 2", lOClock.Phase, 2);
            fExpectInt("creature clock: the easy step gives 4 units", lOClock.AdvanceFixedStep(false), 4);
            fExpectInt("creature clock: ... 2 under Speed", lOClock.AdvanceFixedStep(true), 2);
            fExpectInt("creature clock: ... none while frozen", lOClock.AdvanceFixedStep(false, true), 0);
            fExpectInt("creature clock: Speed keeps the odd unit", lOClock.Advance(16, true), 0);
            fExpectInt("creature clock: ... and hands it out with the next", lOClock.Advance(16, true), 1);
        }

        private static void fCheckCreaturePush()
        {
            UWCritterRecord lORecord = fMakeCreature(GoblinKind, 8, 8, 4, 4, 16, 0, 0);

            UWCreatureMotion.Push(lORecord, 0x4000, UWCreatureMotion.PushSpeed, 0);
            fExpectInt("creature push: the heading byte is the pusher's", lORecord.FineHeading, 0x40);
            fExpectInt("creature push: the speed byte is 5", lORecord.Speed, 5);
            fExpectInt("creature push: the pitch stays level", lORecord.Pitch, 16);
            fExpectBool("creature push: the position stays", lORecord.FineX == 4 && lORecord.FineY == 4, true);
        }
        /// <summary>
        /// A bump against the player at the pool (the user's measurement in the original,
        /// 2026-10-05, SAVE4 level 1): a goblin walking at him on the bank stops at the water
        /// without a path; one that has touched him carries the "collided with a mobile" bit -
        /// byte 0x15 bit 7, the same bit as "has a path" (CollideObjects, see UWMotionCore) - and
        /// walks on into the water and drowns. The bank is floor 10, the pool floor 9.
        /// </summary>
        private static void fCheckBumpSetsPath(DataImport pOData)
        {
            FakeMotionWorld lOWorld;
            UWCreatureMotion lOMotion = fNewCreatureMotion(pOData, out lOWorld);

            lOWorld.Paint(8, 11, 16, 19, 1, 10, 0);
            lOWorld.Paint(11, 14, 12, 16, 1, 9, 1);
            lOWorld.AddBody(11, 15, new UWMotionBody { Index = 1, ItemId = UWObjectMechanics.AdventurerObjectId, XPos = 4, YPos = 4, ZPos = 72, IsMobile = true, IsCreature = true });

            UWCritterRecord lORecord = fMakeCreature(GoblinKind, 10, 15, 4, 4, 80, 0x40, 8);
            StepResult lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("pool: a goblin without a path stops at the bank", lOStep.Collided && !lOStep.Drowned && lORecord.TileX == 10, true);
            fExpectBool("pool: ... and has no path bit", lORecord.HasPath, false);

            // The bump: the player on open floor right in front of the goblin.
            lOMotion = fNewCreatureMotion(pOData, out lOWorld);
            lOWorld.AddBody(8, 10, new UWMotionBody { Index = 1, ItemId = UWObjectMechanics.AdventurerObjectId, XPos = 4, YPos = 3, ZPos = 16, IsMobile = true, IsCreature = true });
            lORecord = fMakeCreature(GoblinKind, 8, 9, 4, 7, 16, 0, 8);
            lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

            fExpectBool("pool: a bump against the player is a hit on item 0x7F", lOStep.HitObject && lOStep.HitObjectItemId == UWObjectMechanics.AdventurerObjectId, true);
            fExpectBool("pool: ... and sets the path bit (byte 0x15 bit 7)", lORecord.HasPath, true);

            // With the bit the goblin follows into the pool: over the water it is airborne (the
            // bank corners no longer hold it up), falls with gravity at interval 1, lands in the
            // water and drowns. The player stands further in, out of contact - against his body
            // every sub-step would be undone and the goblin would hang in the air (the user's
            // "I must not go further in first; when the goblin steps forward it follows").
            lOMotion = fNewCreatureMotion(pOData, out lOWorld);
            lOWorld.Paint(8, 11, 16, 19, 1, 10, 0);
            lOWorld.Paint(11, 14, 12, 16, 1, 9, 1);
            lOWorld.AddBody(12, 15, new UWMotionBody { Index = 1, ItemId = UWObjectMechanics.AdventurerObjectId, XPos = 4, YPos = 4, ZPos = 72, IsMobile = true, IsCreature = true });
            lORecord = fMakeCreature(GoblinKind, 10, 15, 4, 4, 80, 0x40, 8);
            lORecord.HasPath = true;

            bool lbDrowned = false;

            for (int liAt = 0; liAt < 8 && !lbDrowned; liAt++)
            {
                lbDrowned = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land).Drowned;
                lORecord.Speed = 8;
                lORecord.FineHeading = 0x40;
                lORecord.IsStanding = false;
            }

            fExpectBool("pool: with the bit it walks into the pool and drowns", lbDrowned, true);
        }
        /// <summary>A slope (tile type 8, rising with x, from floor 2 to 3): the creature walks
        /// up and down it in eighth-steps, its height following the slope, never stuck (the user
        /// wondered whether creatures slide on slopes, 2026-10-05).</summary>
        private static void fCheckSlope(DataImport pOData)
        {
            FakeMotionWorld lOWorld;
            UWCreatureMotion lOMotion = fNewCreatureMotion(pOData, out lOWorld);

            lOWorld.Paint(9, 4, 9, 12, 8, 2, 0);
            lOWorld.Paint(10, 4, 14, 12, 1, 3, 0);

            UWCritterRecord lORecord = fMakeCreature(GoblinKind, 7, 8, 4, 4, 16, 0x40, 8);
            bool lbStuck = false;
            bool lbCollided = false;
            int liTop = 0;

            for (int liAt = 0; liAt < 14; liAt++)
            {
                StepResult lOStep = lOMotion.Step(lORecord, 5, UWCreatureMotion.Kind.Land);

                lbStuck |= lOStep.Stuck;
                lbCollided |= lOStep.Collided;
                liTop = Math.Max(liTop, lORecord.ZPos);

                if (liAt == 6)
                    lORecord.FineHeading = 0xC0;

                lORecord.Speed = 8;
                lORecord.Interval = 4;
                lORecord.IsStanding = false;
            }

            fExpectBool("slope: a goblin walks up and down a slope without a collision", lbCollided, false);
            fExpectBool("slope: ... and is never stuck", lbStuck, false);
            fExpectInt("slope: ... reaches the upper floor", liTop, 24);
            fExpectBool("slope: ... and is back on the lower floor", lORecord.TileX == 7 && lORecord.ZPos == 16, true);
        }
        /// <summary>The straight line and the path search of UWTileRoute with a plain triple test
        /// (solid tiles of the small map block, nothing else): the line's tile sequence, a wall
        /// across it, the untested adjacent goal, a path around a wall, the zero budget.</summary>
        private static void fCheckTileRoute()
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWTileRoute lORoute = new UWTileRoute();
            int liTested = 0;

            UWTileRoute.TripleDelegate lOTriple = (int piAx, int piAy, int piBx, int piBy, int piCx, int piCy,
                int piHeightIn, ref int piHeightOut, ref int piCost, out bool pbClimb) =>
            {
                pbClimb = false;
                piHeightOut = piHeightIn;
                liTested++;

                int liType;
                int liFloor;
                int liTerrain;

                // The middle tile: solid blocks, water (terrain 1) blocks; the next tile: solid
                // blocks (the walls between), its terrain is not looked at - the shape of the
                // original's triple test.
                if (!lOWorld.TryGetTile(piBx, piBy, out liType, out liFloor, out liTerrain) || liType == 0 || liTerrain == 1)
                    return false;

                if (piCx != 0 && (!lOWorld.TryGetTile(piCx, piCy, out liType, out liFloor, out liTerrain) || liType == 0))
                    return false;

                return true;
            };

            fExpectInt("route: a line to the same tile", lORoute.StraightLine(8, 8, 8, 8, 2, lOTriple), UWTileRoute.LineSameTile);
            fExpectInt("route: a line over open floor is clear", lORoute.StraightLine(8, 8, 12, 10, 2, lOTriple), UWTileRoute.LineClear);
            fExpectBool("route: ... it starts at the start and ends at the destination",
                lORoute.Tiles[0] == new UWTilePos(8, 8) && lORoute.Tiles[lORoute.Tiles.Count - 1] == new UWTilePos(12, 10), true);
            fExpectBool("route: ... with 7 tiles (4 along x, 2 carried along y, the start)", lORoute.Tiles.Count == 7, true);
            fExpectBool("route: ... every tile adjacent to the one before", fAdjacentChain(lORoute.Tiles), true);

            lOWorld.Paint(10, 4, 10, 12, 0, 2, 0);
            fExpectInt("route: a wall across the line blocks it", lORoute.StraightLine(8, 8, 12, 8, 2, lOTriple), UWTileRoute.LineBlocked);

            // The path around the wall (it ends at y 12, the room is open from 13 on).
            fExpectBool("route: the search finds a way round the wall", lORoute.FindPath(8, 8, 2, 12, 8, 2, 4, lOTriple), true);
            fExpectBool("route: ... from the start to the goal", lORoute.Tiles[0] == new UWTilePos(8, 8) && lORoute.Tiles[lORoute.Tiles.Count - 1] == new UWTilePos(12, 8), true);
            fExpectBool("route: ... adjacent tiles all the way", fAdjacentChain(lORoute.Tiles), true);
            fExpectBool("route: ... and never through the wall", !lORoute.Tiles.Exists(t => t.X == 10 && t.Y >= 4 && t.Y <= 12), true);

            // The untested adjacent goal: a solid goal next to the start is still a path.
            liTested = 0;
            fExpectBool("route: a goal right beside the start is a path", lORoute.FindPath(8, 8, 2, 9, 8, 2, 4, lOTriple), true);
            fExpectInt("route: ... of two tiles", lORoute.Tiles.Count, 2);
            lOWorld.Paint(9, 8, 9, 8, 1, 2, 1);
            fExpectBool("route: ... even when the goal is water (the first wave does not test the goal)", lORoute.FindPath(8, 8, 2, 9, 8, 2, 4, lOTriple), true);
            lOWorld.Paint(9, 8, 10, 8, 1, 2, 1);
            fExpectBool("route: a water goal two tiles away finds no path (tested as the middle)", lORoute.FindPath(8, 8, 2, 10, 8, 2, 4, lOTriple), false);

            fExpectBool("route: a line to a water destination is blocked (tested as the middle)", lORoute.StraightLine(8, 8, 9, 8, 2, lOTriple) == UWTileRoute.LineBlocked, true);
            fExpectInt("route: the line's budget is zero", lORoute.Budget, 0);
        }

        private static bool fAdjacentChain(System.Collections.Generic.List<UWTilePos> pOTiles)
        {
            for (int liAt = 1; liAt < pOTiles.Count; liAt++)
            {
                if (Math.Abs(pOTiles[liAt].X - pOTiles[liAt - 1].X) + Math.Abs(pOTiles[liAt].Y - pOTiles[liAt - 1].Y) != 1)
                    return false;
            }

            return true;
        }
        /// <summary>TraverseMultipleTiles on the small map with the real COMOBJ: the terrain of
        /// the middle tile per kind, drops and steps, a closed door's orientation, a bridge over
        /// water, the jump, and the routes built on it (a water line blocked, a lava goal
        /// costing, a drop costing).</summary>
        private static void fCheckTileTraverse(DataImport pOData)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWTileRoute lORoute = new UWTileRoute();
            UWTileTraverse lOTraverse = new UWTileTraverse(lOWorld, pOData.CommonObjectProperties, lORoute);
            int liHeight = 2;
            int liCost = 0;
            bool lbClimb;

            UWCreatureMotion.ResetHandlers();
            lOTraverse.SetCreature(UWCreatureMotion.Kind.Land, 20, false);

            fExpectBool("traverse: open floor passes", lOTraverse.Triple(8, 8, 9, 8, 10, 8, 2, ref liHeight, ref liCost, out lbClimb), true);
            fExpectInt("traverse: ... height out is the floor", liHeight, 2);

            lOWorld.Paint(9, 8, 9, 8, 1, 2, 1);
            fExpectBool("traverse: water as the middle blocks a land creature", lOTraverse.Triple(8, 8, 9, 8, 10, 8, 2, ref liHeight, ref liCost, out lbClimb), false);
            lOTraverse.SetCreature(UWCreatureMotion.Kind.Swimmer, 9, false);
            fExpectBool("traverse: ... a swimmer passes water", lOTraverse.Triple(8, 8, 9, 8, 10, 8, 2, ref liHeight, ref liCost, out lbClimb), true);
            fExpectBool("traverse: ... but not plain floor", lOTraverse.Triple(9, 8, 10, 8, 11, 8, 2, ref liHeight, ref liCost, out lbClimb), false);
            lOTraverse.SetCreature(UWCreatureMotion.Kind.Flier, 9, false);
            fExpectBool("traverse: a flier passes water and floor alike", lOTraverse.Triple(8, 8, 9, 8, 10, 8, 2, ref liHeight, ref liCost, out lbClimb)
                && lOTraverse.Triple(9, 8, 10, 8, 11, 8, 2, ref liHeight, ref liCost, out lbClimb), true);
            lOTraverse.SetCreature(UWCreatureMotion.Kind.Land, 20, false);

            fExpectBool("traverse: the first tile is not tested (water start)", lOTraverse.Triple(0, 0, 9, 8, 10, 8, 2, ref liHeight, ref liCost, out lbClimb), true);

            lOTraverse.SetCreature(UWCreatureMotion.Kind.Land, 20, true);
            fExpectInt("traverse: a jumper's line over water is still blocked (budget 0)", lORoute.StraightLine(8, 8, 10, 8, 2, lOTraverse.Triple), UWTileRoute.LineBlocked);
            fExpectBool("traverse: ... but its path jumps the water", lORoute.FindPath(8, 8, 2, 10, 8, 2, 3, lOTraverse.Triple), true);
            lOTraverse.SetCreature(UWCreatureMotion.Kind.Land, 20, false);
            fExpectBool("traverse: a goblin's path goes round it", lORoute.FindPath(8, 8, 2, 10, 8, 2, 3, lOTraverse.Triple)
                && !lORoute.Tiles.Exists(t => t.X == 9 && t.Y == 8), true);

            lOWorld.AddBody(9, 8, new UWMotionBody { Index = 50, ItemId = 0x164, XPos = 4, YPos = 4, ZPos = 16 });
            liCost = 0;
            fExpectBool("traverse: a bridge over the water carries a land creature", lOTraverse.Triple(8, 8, 9, 8, 10, 8, 2, ref liHeight, ref liCost, out lbClimb), true);

            lOWorld = new FakeMotionWorld();
            lORoute = new UWTileRoute();
            lOTraverse = new UWTileTraverse(lOWorld, pOData.CommonObjectProperties, lORoute);
            lOTraverse.SetCreature(UWCreatureMotion.Kind.Land, 20, false);
            lOWorld.Paint(9, 8, 9, 8, 1, 2, 2);
            fExpectInt("traverse: a line across lava is clear for a land creature", lORoute.StraightLine(8, 8, 10, 8, 2, lOTraverse.Triple), UWTileRoute.LineClear);
            fExpectInt("traverse: a line ending on lava is blocked (the goal costs 2 against 0)", lORoute.StraightLine(8, 8, 9, 8, 2, lOTraverse.Triple), UWTileRoute.LineBlocked);
            fExpectBool("traverse: a path to a lava goal two tiles off needs budget 2", !lORoute.FindPath(7, 8, 2, 9, 8, 2, 1, lOTraverse.Triple)
                && lORoute.FindPath(7, 8, 2, 9, 8, 2, 2, lOTraverse.Triple), true);

            lOWorld = new FakeMotionWorld();
            lORoute = new UWTileRoute();
            lOTraverse = new UWTileTraverse(lOWorld, pOData.CommonObjectProperties, lORoute);
            lOTraverse.SetCreature(UWCreatureMotion.Kind.Land, 20, false);
            lOWorld.Paint(10, 4, 14, 12, 1, 4, 0);
            fExpectInt("traverse: a step up of two levels blocks the line", lORoute.StraightLine(8, 8, 11, 8, 2, lOTraverse.Triple), UWTileRoute.LineBlocked);
            fExpectInt("traverse: a drop of two levels blocks the line (cost 1 against 0)", lORoute.StraightLine(12, 8, 8, 8, 4, lOTraverse.Triple), UWTileRoute.LineBlocked);
            fExpectBool("traverse: ... but a path with budget 1 takes the drop", lORoute.FindPath(12, 8, 4, 8, 8, 2, 1, lOTraverse.Triple), true);
            lOWorld.Paint(10, 4, 14, 12, 1, 3, 0);
            fExpectInt("traverse: a step up of one level is free", lORoute.StraightLine(8, 8, 11, 8, 2, lOTraverse.Triple), UWTileRoute.LineClear);

            lOWorld = new FakeMotionWorld();
            lORoute = new UWTileRoute();
            lOTraverse = new UWTileTraverse(lOWorld, pOData.CommonObjectProperties, lORoute);
            lOTraverse.SetCreature(UWCreatureMotion.Kind.Land, 20, false);
            lOWorld.AddBody(9, 8, new UWMotionBody { Index = 51, ItemId = 0x140, XPos = 4, YPos = 7, ZPos = 16, Direction = 0 });
            liCost = 0;
            fExpectBool("traverse: a closed door across the way blocks (south to north)", lOTraverse.Triple(9, 7, 9, 8, 9, 9, 2, ref liHeight, ref liCost, out lbClimb), false);
            fExpectBool("traverse: ... and lets a pass along it through (west to east)", lOTraverse.Triple(8, 8, 9, 8, 10, 8, 2, ref liHeight, ref liCost, out lbClimb), true);
            fExpectBool("traverse: ... a turn from the west towards the north is blocked by the door on the north edge",
                lOTraverse.Triple(8, 8, 9, 8, 9, 9, 2, ref liHeight, ref liCost, out lbClimb), false);
            fExpectBool("traverse: ... a turn from the west towards the south passes (the door is on the north edge)",
                lOTraverse.Triple(8, 8, 9, 8, 9, 7, 2, ref liHeight, ref liCost, out lbClimb), true);
            fExpectInt("traverse: the line through the door tile from the south is blocked", lORoute.StraightLine(9, 6, 9, 10, 2, lOTraverse.Triple), UWTileRoute.LineBlocked);
        }
    }
}
