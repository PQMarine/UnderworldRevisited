using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE CREATURE BRAIN AGAINST AI.C AND PATHFIND.C, reference audit round 2 (2026-10-15): the
    /// smaller differences of the per-update brain, each one sentence of the original run on the
    /// fake host of the CritterBrain group.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckBrainRound2()
        {
            Check.Group("Creature brain, audit round 2 (AI.C, PATHFIND.C)");

            fCheckGuardFallsToMill();
            fCheckWanderWalkPicture();
            fCheckStandingBit();
            fCheckBendAway();
            fCheckFleeTurn();
            fCheckNeighbourOrder();
            fCheckFlierWanderPitch();
            fCheckFlierPathPitch();
            fCheckArrivingHome();

            // A23: line_of_sight is never clear for more than ten tile steps along its major axis.
            UWTile[] lOOpen = new UWTile[64 * 64];

            for (int liAt = 0; liAt < lOOpen.Length; liAt++)
                lOOpen[liAt] = fSightTile(liAt, UWTile.TileTypeEnum.open, 0);

            fExpectBool("sight: ten tiles east over open floor is clear",
                UWTilePath.TestBetweenPoints(lOOpen, (10 << 3) + 4, (10 << 3) + 4, 20, (20 << 3) + 4, (10 << 3) + 4, 20), true);
            fExpectBool("sight: eleven tiles is never clear",
                UWTilePath.TestBetweenPoints(lOOpen, (10 << 3) + 4, (10 << 3) + 4, 20, (21 << 3) + 4, (10 << 3) + 4, 20), false);
            fExpectBool("sight: ... nor eleven north with five east",
                UWTilePath.TestBetweenPoints(lOOpen, (10 << 3) + 4, (30 << 3) + 4, 20, (15 << 3) + 4, (19 << 3) + 4, 20), false);

            fCheckDeadAttacker();
            fCheckStoredPaths();
            fCheckPathLeap();

            // A21: acceptable_danger's exception, conversation 0x16 on level 6.
            fExpectInt("danger: a hostile one with full hit points and nerve 8 may risk 6",
                UWCritterRules.GetPathRangeBudget(0, 20, 20, 8, false, 5, 0x16), 6);
            fExpectInt("danger: ... conversation 0x16 on level 6 none",
                UWCritterRules.GetPathRangeBudget(0, 20, 20, 8, false, 6, 0x16), 0);
            fExpectInt("danger: ... another one on level 6 the usual",
                UWCritterRules.GetPathRangeBudget(0, 20, 20, 8, false, 6, 0x17), 6);
        }

        /// <summary>A22: store_path, set_next_square_on_path, close_to_square and the sixteen slots;
        /// a creature searches once, follows its stored path, lets it go on a new destination, and
        /// wanders blocked when all sixteen slots are taken.</summary>
        private static void fCheckStoredPaths()
        {
            UWCritterPath lOPath = new UWCritterPath();

            lOPath.Store(new[] { new UWTilePos(5, 5), new UWTilePos(6, 5), new UWTilePos(6, 6) }, new[] { false, false, true });
            fExpectBool("stored path: starts on its first square, no step taken", lOPath.X == 5 && lOPath.Y == 5 && lOPath.Count == 0, true);
            fExpectBool("stored path: the next square", lOPath.Advance() && lOPath.X == 6 && lOPath.Y == 5 && !lOPath.JumpStep, true);
            fExpectBool("stored path: ... a leap into the last", lOPath.Advance() && lOPath.X == 6 && lOPath.Y == 6 && lOPath.JumpStep, true);
            fExpectBool("stored path: ... and the end", lOPath.Advance(), false);

            fExpectBool("close: on the square", UWCritterPath.CloseToSquare(false, 6, 5, 4, 4, 6, 5), true);
            fExpectBool("close: two eighths from the edge towards it counts", UWCritterPath.CloseToSquare(false, 5, 5, 6, 4, 6, 5), true);
            fExpectBool("close: ... not three", UWCritterPath.CloseToSquare(false, 5, 5, 5, 4, 6, 5), false);
            fExpectBool("close: ... nor for a leap", UWCritterPath.CloseToSquare(true, 5, 5, 6, 4, 6, 5), false);

            UWCritterPathSlots lOSlots = new UWCritterPathSlots();

            for (int liAt = 0; liAt < UWCritterPathSlots.SlotCount; liAt++)
            {
                lOSlots.TryFindFree(out int liSlot);
                lOSlots.Take(liSlot, 100 + liAt);
            }

            fExpectBool("slots: sixteen, then none", lOSlots.TryFindFree(out int _), false);
            lOSlots.Free(9);
            fExpectBool("slots: a freed one is found again", lOSlots.TryFindFree(out int liFound) && liFound == 9, true);

            // The brain: home at 12/5, no straight line, a path over 6/5.
            FakeCritterHost lOHost = new FakeCritterHost { StraightClear = false, NextPathTileX = 6, NextPathTileY = 5, DefaultRoll = 1 };
            UWCritterRecord lORecord = fMakeRecord(5, 5, 1, 2, 12, 5);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lOHost.PlacePlayer((12 << 3) + 4, (9 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectBool("path: found, stored in a slot", lORecord.HasPath && lOHost.Slots.FreeCount == 15, true);
            fExpectInt("path: ... heading for its next square, east", lORecord.FineHeading, 0x40);

            lOBrain.Update(lOHost);
            fExpectInt("path: the next update follows it without a new search", lOHost.PathSearches, 1);

            // A new destination lets the path go (crit_head_for_loc: B18_5 with B15_7).
            lORecord.Goal = 5;
            lORecord.GTarg = 1;
            lORecord.TargetConfirmed = true;
            lORecord.Attitude = 0;
            lOHost.PathAvailable = false;
            lOBrain.Update(lOHost);
            fExpectBool("path: a new destination gives the slot back", lOHost.Slots.FreeCount == 16 && !lORecord.HasPath, true);

            // All sixteen taken: find_free_path fails, the creature wanders blocked.
            lOHost = new FakeCritterHost { StraightClear = false, DefaultRoll = 1 };

            for (int liAt = 0; liAt < UWCritterPathSlots.SlotCount; liAt++)
                lOHost.Slots.Take(liAt, 100 + liAt);

            lORecord = fMakeRecord(5, 5, 1, 2, 12, 5);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lOHost.PlacePlayer((12 << 3) + 4, (9 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectBool("path: with all sixteen slots taken no search, blocked", lOHost.PathSearches == 0 && lORecord.Blocked && !lORecord.HasPath, true);

            // The bit from touching a mobile (no path of its own): another creature's slot 0 is
            // neither followed nor freed; the creature searches its own next time.
            lOHost = new FakeCritterHost { StraightClear = false, DefaultRoll = 1, PathAvailable = false };
            lOHost.Slots.Take(0, 77);
            lORecord = fMakeRecord(5, 5, 1, 2, 12, 5);
            lORecord.SetDestination(12, 5, 0);
            lORecord.NewDestination = false;
            lORecord.HasPath = true;
            lORecord.PathSlot = 0;
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lOHost.PlacePlayer((12 << 3) + 4, (9 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectBool("path: a bit from a touch frees no other creature's slot", lOHost.Slots.IsOwner(0, 77), true);
            fExpectBool("path: ... and is let go", lORecord.HasPath, false);
        }

        /// <summary>A12: do_that_jump_kinda_thing - a path step into a flagged square: at the edge
        /// point (Manhattan below 3) the creature leaps towards the square after it, interval 1,
        /// pitch 0x16, speed 11, and the walk block is skipped.</summary>
        private static void fCheckPathLeap()
        {
            FakeCritterHost lOHost = new FakeCritterHost
            {
                StraightClear = false,
                NextPathTileX = 6,
                NextPathTileY = 5,
                PathJumps = new[] { false, true, false },
                DefaultRoll = 1
            };

            UWCritterRecord lORecord = fMakeRecord(5, 5, 1, 2, 7, 5);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lORecord.Animation = UWCritterBrain.AnimStanding;
            lOHost.PlacePlayer((12 << 3) + 4, (9 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectInt("leap: pitch 0x16", lORecord.Pitch, UWCritterRules.HopPitch);
            fExpectInt("leap: speed 11", lORecord.Speed, UWCritterRules.HopSpeed);
            fExpectInt("leap: interval 1", lORecord.Interval, UWCritterRules.IntervalStuck);
            fExpectInt("leap: towards the square after the jump square, east", lORecord.FineHeading, 0x40);
            fExpectInt("leap: no walk block (the picture stays)", lORecord.Animation, UWCritterBrain.AnimStanding);
        }

        /// <summary>A24: critter_mv's damage reaction against an attacker that no longer exists
        /// jumps to the goal and keeps last_hit and the tally (byte 0x11).</summary>
        private static void fCheckDeadAttacker()
        {
            FakeCritterHost lOHost = new FakeCritterHost { DefaultRoll = 1 };
            UWCritterRecord lORecord = fMakeRecord(5, 5, 0, 2);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            // An ally hit by creature 7, which is gone.
            lORecord.IsAlly = true;
            lORecord.Attacker = 7;
            lORecord.DamageTaken = 5;
            lOHost.PlacePlayer((12 << 3) + 4, (5 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectInt("dead attacker: the attacker is kept", lORecord.Attacker, 7);
            fExpectInt("dead attacker: ... and the tally", lORecord.DamageTaken, 5);
        }

        /// <summary>A20: crit_head_for_loc at the destination with goal 1 sets goal 8 and goes on:
        /// with the straight-line bit the creature heads south (deltatotheta(0, 0) = 4) and
        /// walks; without it the line to its own tile is none and it wanders blocked.</summary>
        private static void fCheckArrivingHome()
        {
            FakeCritterHost lOHost = new FakeCritterHost { DefaultRoll = 1 };
            UWCritterRecord lORecord = fMakeRecord(5, 5, 1, 2);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lORecord.SetDestination(5, 5, 0);
            lORecord.NewDestination = false;
            lORecord.StraightLineKnown = true;
            lOHost.PlacePlayer((12 << 3) + 4, (5 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectInt("home: arrived, it mills", lORecord.Goal, 8);
            fExpectInt("home: ... and still takes a step south", lORecord.FineHeading, 0x80);
            fExpectInt("home: ... walking", lORecord.Animation, UWCritterBrain.AnimWalking);
        }

        /// <summary>A18: move_along_path aims a flier's height at the destination's floor
        /// (adjust_height(OBJ_DESTX, OBJ_DESTY)): low over a next tile of floor 0 with the
        /// destination at floor 4, it climbs.</summary>
        private static void fCheckFlierPathPitch()
        {
            FakeCritterHost lOHost = new FakeCritterHost { StraightClear = false, NextPathTileX = 6, NextPathTileY = 5, DefaultRoll = 1 };
            UWCritterRecord lORecord = fMakeRecord(5, 5, 1, 2, 12, 5);
            UWObjectClassProperties.Critter lORow = fMakeRow();

            lORow.Row[0x0A] = 0x80;
            lORecord.ZPos = UWCritterRules.FlierTargetHeightBase;
            lOHost.SetFloor(12, 5, 4);

            UWCritterBrain lOBrain = fMakeBrain(lORecord, lORow, 5, lOHost);

            lOHost.PlacePlayer((12 << 3) + 4, (9 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectInt("flier: on a path it climbs towards the destination's height", lORecord.Pitch, UWCritterRules.FlierPitchUp);
        }

        /// <summary>A17: crit_drunkwalk tests the flier's own height (myzpos > 0xE): a bat at
        /// zpos 0x78 over a floor of 0 dips, 0xE + rand() % 3.</summary>
        private static void fCheckFlierWanderPitch()
        {
            FakeCritterHost lOHost = new FakeCritterHost { DefaultRoll = 4 };
            UWCritterRecord lORecord = fMakeRecord(5, 5, 2, 2);
            UWObjectClassProperties.Critter lORow = fMakeRow();

            lORow.Row[0x0A] = 0x80;
            lORecord.ZPos = 0x78;

            UWCritterBrain lOBrain = fMakeBrain(lORecord, lORow, 5, lOHost);

            lOHost.PlacePlayer((12 << 3) + 4, (5 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectInt("flier: high up by its own height it dips", lORecord.Pitch, UWCritterRules.FlierPitchDown + 1);
        }

        /// <summary>A16: flood_path walks the neighbours +y, +x, -y, -x (PathingOffset), so of the
        /// two equal routes to the diagonal tile the one over +y comes first.</summary>
        private static void fCheckNeighbourOrder()
        {
            UWTileRoute lORoute = new UWTileRoute();

            bool lbFound = lORoute.FindPath(10, 10, 0, 11, 11, 0, 0,
                (int piAx, int piAy, int piBx, int piBy, int piCx, int piCy, int piIn, ref int piOut, ref int piCost, out bool pbClimb) =>
                {
                    piOut = piIn;
                    pbClimb = false;

                    return true;
                });

            fExpectBool("path order: found on an open floor", lbFound, true);
            fExpectBool("path order: the route over +y (10/11) comes first",
                lORoute.Tiles.Count == 3 && lORoute.Tiles[1] == new UWTilePos(10, 11), true);
        }

        /// <summary>A15: a fleeing creature that bumps far from its pursuer turns a quarter from
        /// its own travel heading (crit_flee: meptr->heading >> 5), whatever the pursuer's
        /// direction. Travelling east (0x40), the player north, a roll of 1: 0x80 + 1.</summary>
        private static void fCheckFleeTurn()
        {
            FakeCritterHost lOHost = new FakeCritterHost { DefaultRoll = 1 };
            UWCritterRecord lORecord = fMakeRecord(5, 5, 6, 1);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lORecord.FineHeading = 0x40;
            lOHost.Step = new StepResult { Collided = true };
            lOHost.PlacePlayer((5 << 3) + 4, (9 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectInt("flee: bumped far off, a quarter turn from its own heading", lORecord.FineHeading, 0x81);
        }

        /// <summary>A14: crit_avoid_player's bands, away = 0x80 (the player to the north).</summary>
        private static void fCheckBendAway()
        {
            fExpectInt("bend: 0x30 off away is kept", UWCritterRules.BendAwayFromPlayer(0x50, 0x80), 0x50);
            fExpectInt("bend: 0x50 off gives away - 0x20", UWCritterRules.BendAwayFromPlayer(0x30, 0x80), 0x60);
            fExpectInt("bend: 0x70 off gives the heading + 0x20", UWCritterRules.BendAwayFromPlayer(0x10, 0x80), 0x30);
            fExpectInt("bend: 0x90 off gives the heading - 0x20", UWCritterRules.BendAwayFromPlayer(0xF0, 0x80), 0xD0);
            fExpectInt("bend: 0xB0 off gives away + 0x20", UWCritterRules.BendAwayFromPlayer(0xD0, 0x80), 0xA0);
            fExpectInt("bend: 0xD0 off is kept", UWCritterRules.BendAwayFromPlayer(0xB0, 0x80), 0xB0);
        }

        /// <summary>A10: crit_guard's stand (goals 0 and 7) leaves B15_6 clear, so the physics
        /// keeps running; only the wander step's stand sets it.</summary>
        private static void fCheckStandingBit()
        {
            FakeCritterHost lOHost = new FakeCritterHost { DefaultRoll = 1 };
            UWCritterRecord lORecord = fMakeRecord(5, 5, 0, 2);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lOHost.PlacePlayer((12 << 3) + 4, (5 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectInt("stand: a guard at its post stands", lORecord.Animation, UWCritterBrain.AnimStanding);
            fExpectBool("stand: ... without the standing bit, its physics runs", lORecord.IsStanding, false);

            // The wander stand: standing, frame 3, a roll of 15 keeps it standing (lazy 5 > 15 fails).
            lORecord = fMakeRecord(5, 5, 2, 2);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.Animation = UWCritterBrain.AnimStanding;
            lOHost.DefaultRoll = 15;
            lOBrain.Update(lOHost);

            fExpectBool("stand: the wander step's stand sets the standing bit", lORecord.IsStanding, true);
        }

        /// <summary>A9: crit_drunkwalk's non-standing branch sets SEQ_WALK unless the frame-3 roll
        /// makes it stand - a creature backing off (sequence 7) that drops into the wander step
        /// walks off in the walking picture.</summary>
        private static void fCheckWanderWalkPicture()
        {
            FakeCritterHost lOHost = new FakeCritterHost { DefaultRoll = 1 };
            UWCritterRecord lORecord = fMakeRecord(5, 5, 2, 2);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lORecord.Animation = 7;
            lOHost.PlacePlayer((12 << 3) + 4, (5 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectInt("wander: a creature backing off walks off in the walking picture", lORecord.Animation, UWCritterBrain.AnimWalking);
        }

        /// <summary>A8: crit_guard's tail sends every goal but 2, 0 and 7 to crit_mill, which
        /// turns a hostile creature into a guard. A hostile creature on its way home (goal 1)
        /// that finds no path wanders, the wander step's hostile half runs crit_guard - and the
        /// creature becomes a guard instead of standing for an update.</summary>
        private static void fCheckGuardFallsToMill()
        {
            FakeCritterHost lOHost = new FakeCritterHost
            {
                PathAvailable = false,
                StraightClear = false,
                DefaultRoll = 1
            };

            UWCritterRecord lORecord = fMakeRecord(5, 5, 1, 0, 12, 12);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lOHost.PlacePlayer((12 << 3) + 4, (5 << 3) + 4);
            lOBrain.Update(lOHost);

            fExpectInt("guard check: a hostile one going home that falls through becomes a guard", lORecord.Goal, 4);
        }
    }
}
