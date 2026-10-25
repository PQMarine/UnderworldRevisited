using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The creature brain, part of the CritterBrain group: what a creature perceives, how far it
    /// follows, how it turns, the kin alarm, water and the path search's window.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>Spec 3.1, deviation 22: heard while hear2 / 4 &gt; d2 (strict), seen while
        /// d2 &lt;= see^2 with the facing and the sight line, unchanged below hear2 * 4, lost
        /// from there. Loudness 8 and hearing 8 give hear 4; visibility 8 and sight 8 see 4.</summary>
        private static void fCheckPerceptionThresholds()
        {
            FakeCritterHost lOHost;
            UWCritterRecord lORecord;
            UWCritterBrain lOBrain;

            // Heard: d2 3 < 4.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.FacingEighth = 4;
            lOHost.PlacePlayer((6 << 3) + 4, (6 << 3) + 4);
            lOBrain.Update(lOHost);
            fExpectInt("hearing: d2 2 below hear2 / 4 = 4 is heard, goal 5", lORecord.Goal, 5);

            // Not heard at d2 4 (strict), not seen when facing away, unchanged: heard bit and walk over.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.FacingEighth = 0;
            lOHost.PlacePlayer((7 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("hearing: d2 4 equal to hear2 / 4 is not heard", lORecord.Goal, 0);
            fExpectBool("hearing: within hear2 * 4 the heard bit is set", lORecord.HeardSomething, true);
            fExpectInt("hearing: and with the coin it walks over to look", lORecord.Animation, UWCritterBrain.AnimWalking);

            // Seen at d2 4 when facing east.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.FacingEighth = 2;
            lOHost.PlacePlayer((7 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("sight: d2 4 facing the player is seen, goal 5", lORecord.Goal, 5);
            fExpectBool("sight: the target bit is set", lORecord.TargetConfirmed, true);

            // Seen at d2 16 (non-strict), one eighth off.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.FacingEighth = 1;
            lOHost.PlacePlayer((9 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("sight: d2 16 equal to see^2 is seen, one eighth off the facing", lORecord.Goal, 5);

            // Blocked sight line: not seen, unchanged.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.FacingEighth = 2;
            lOHost.LineOfSight = false;
            lOHost.PlacePlayer((9 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("sight: without a sight line it is not seen", lORecord.Goal, 0);

            // d2 25: beyond sight, below hear2 * 4 = 64 -> unchanged.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.FacingEighth = 2;
            lOHost.PlacePlayer((10 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("sight: d2 25 beyond see^2 is not seen", lORecord.Goal, 0);
            fExpectBool("sight: but still unchanged, heard bit set", lORecord.HeardSomething, true);

            // d2 64: lost, the target bit cleared.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.FacingEighth = 2;
            lORecord.TargetConfirmed = false;
            lORecord.HeardSomething = false;
            lOHost.PlacePlayer((13 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("hearing: d2 64 equal to hear2 * 4 is lost", lORecord.Goal, 0);
            fExpectBool("hearing: lost sets no heard bit", lORecord.HeardSomething, false);
        }

        /// <summary>Spec 6.11: the leash of goal 5 needs the target beyond 2 tiles, backup 4,
        /// bit 5 clear and the creature beyond twice its travel range from home.</summary>
        private static void fCheckLeash()
        {
            FakeCritterHost lOHost;
            UWCritterRecord lORecord;
            UWCritterBrain lOBrain;

            // Home 5/5, travel range 3: leash beyond 4 * 9 = 36 squared tiles, i.e. from 7 tiles.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(12, 5, 5, 0, 5, 5);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.BackupGoal = 4;
            lORecord.TargetConfirmed = true;
            lOHost.PlacePlayer((15 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("leash: 7 tiles from home with backup 4 gives goal 4", lORecord.Goal, 4);
            fExpectInt("leash: with gtarg 0", lORecord.GTarg, 0);
            fExpectBool("leash: the target bit is cleared", lORecord.TargetConfirmed, false);

            // The destination is the player's tile in the cases that pursue, so the 1-in-8
            // re-search of AttackGoalSearchForTarget (which would reset the goal on "unchanged"
            // with the coin) does not run.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(11, 5, 5, 0, 5, 5);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.BackupGoal = 4;
            lORecord.SetDestination(14, 5, 0);
            lOHost.PlacePlayer((14 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("leash: at twice the travel range (6 tiles) it still pursues", lORecord.Goal, 5);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(12, 5, 5, 0, 5, 5);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.BackupGoal = 4;
            lORecord.Relentless = true;
            lORecord.SetDestination(15, 5, 0);
            lOHost.PlacePlayer((15 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("leash: bit 5 set, no leash", lORecord.Goal, 5);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(12, 5, 5, 0, 5, 5);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.SetDestination(15, 5, 0);
            lOHost.PlacePlayer((15 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("leash: without backup 4, no leash", lORecord.Goal, 5);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(12, 5, 5, 0, 5, 5);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lORecord.BackupGoal = 4;
            lOHost.PlacePlayer((13 << 3) + 4, 44);
            lOBrain.Update(lOHost);
            fExpectInt("leash: the target within 2 tiles (0x100 squared eighths), no leash", lORecord.Goal, 5);
        }

        /// <summary>Spec 7.7: the facing turns at most 45 degrees per update; a moving
        /// creature that wants to turn 90 degrees or more stands one update and then takes
        /// the new heading; a 45 degree turn is taken directly; a deflected heading is kept.</summary>
        private static void fCheckTurnLimiter()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lORecord.Speed = 8;
            lORecord.FineHeading = 0;
            lORecord.FacingEighth = 0;
            lOHost.DefaultRoll = 1;
            lOBrain.Update(lOHost);

            fExpectInt("turn: the facing turns one eighth per update", lORecord.FacingEighth, 1);
            fExpectInt("turn: a 90 degree reversal stops the creature", lORecord.Speed, 0);
            fExpectInt("turn: and keeps the old heading for that update", lORecord.FineHeading, 0);

            lOBrain.Update(lOHost);
            fExpectInt("turn: the next update takes the new heading", lORecord.FineHeading, 64);
            fExpectInt("turn: at full speed", lORecord.Speed, 8);
            fExpectInt("turn: the facing completes the turn", lORecord.FacingEighth, 2);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lOHost.PlacePlayer((8 << 3) + 4, (8 << 3) + 4);
            lORecord.Speed = 8;
            lORecord.FineHeading = 0;
            lOHost.DefaultRoll = 1;
            lOBrain.Update(lOHost);

            fExpectInt("turn: a 45 degree turn is taken directly", lORecord.FineHeading, 32);
            fExpectInt("turn: without stopping", lORecord.Speed, 8);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lORecord.Speed = 8;
            lORecord.FineHeading = 200;
            lOHost.DefaultRoll = 1;
            lOHost.Step = new StepResult { Collided = true, HeadingDeflected = true };
            lOBrain.Update(lOHost);

            fExpectInt("turn: a deflected heading is kept as the physics left it", lORecord.FineHeading, 200);
            fExpectInt("turn: at the goal's speed", lORecord.Speed, 8);
            fExpectBool("turn: a deflection never sets the blocked bit", lORecord.Blocked, false);
        }

        /// <summary>Spec 4.2: the kin alarm reaches creatures of the same kind within the
        /// hearing range in Manhattan tiles for 0x200 PIT ticks after the player's blow.</summary>
        private static void fCheckKinAlarm()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lOVictim = fMakeRecord(5, 5, 0, 2);
            UWCritterBrain lOVictimBrain = fMakeBrain(lOVictim, fMakeRow(3, 20, 5, 4), 5, lOHost);

            lOHost.ClockValue = 1000;
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lOVictimBrain.OnDamaged(lOHost, 1, 2);

            fExpectInt("alarm: the player's blow writes the victim's kind", lOHost.Alarm.Kind, 3);
            fExpectInt("alarm: and its tile", lOHost.Alarm.TileX * 100 + lOHost.Alarm.TileY, 505);

            UWCritterRecord lOKin = fMakeRecord(8, 5, 0, 2);
            UWCritterBrain lOKinBrain = fMakeBrain(lOKin, fMakeRow(3, 20, 5, 4), 9, lOHost);

            // The alarm's destination is the victim's tile, not the player's, so the goal 5
            // routine that follows in the same update would re-search with 1 in 8 (and reset
            // on "unchanged" with the coin): a default roll of 1 keeps that path shut.
            lOHost.ClockValue = 1000 + 0x200;
            lOHost.DefaultRoll = 1;
            lOKinBrain.Update(lOHost);
            fExpectInt("alarm: kin at Manhattan 3 with hearing 4 attacks", lOKin.Goal, 5);
            fExpectBool("alarm: the alarm itself set goal 5", lOHost.HasEvent("goal 0>5"), true);
            fExpectInt("alarm: and turns hostile", lOKin.Attitude, 0);
            fExpectBool("alarm: with the target confirmed", lOKin.TargetConfirmed, true);
            lOHost.DefaultRoll = 0;

            lOKin = fMakeRecord(9, 5, 0, 2);
            lOKinBrain = fMakeBrain(lOKin, fMakeRow(3, 20, 5, 4), 9, lOHost);
            lOKinBrain.Update(lOHost);
            fExpectInt("alarm: kin at Manhattan 4 is out of hearing", lOKin.Goal, 0);

            lOKin = fMakeRecord(8, 5, 0, 2);
            lOKinBrain = fMakeBrain(lOKin, fMakeRow(4, 20, 5, 4), 9, lOHost);
            lOKinBrain.Update(lOHost);
            fExpectInt("alarm: another kind does not care", lOKin.Goal, 0);

            // An ALLY of another kind answers too (53524-53549): hostile, goal 5 at the victim
            // (per user on the original, 2026-09-27: a summoned creature turns hostile when the
            // player hits a creature nearby).
            lOKin = fMakeRecord(8, 5, 0, 2);
            lOKin.IsAlly = true;
            lOKinBrain = fMakeBrain(lOKin, fMakeRow(4, 20, 5, 4), 9, lOHost);
            lOHost.Objects[5] = new CritterTarget
            {
                Exists = true, Index = 5, IsCreature = true, TileX = 5, TileY = 5,
                X = (5 << 3) + 4, Y = (5 << 3) + 4, HitPoints = 20
            };
            lOHost.Events.Clear();
            lOHost.DefaultRoll = 1;
            lOKinBrain.Update(lOHost);
            lOHost.DefaultRoll = 0;
            lOHost.Objects.Remove(5);
            fExpectBool("alarm: an ally of another kind answers (goal 0>5)", lOHost.HasEvent("goal 0>5"), true);
            fExpectInt("alarm: an ally of another kind attacks", lOKin.Goal, 5);
            fExpectInt("alarm: ... the creature the player hit", lOKin.GTarg, 5);
            fExpectInt("alarm: ... and turns hostile", lOKin.Attitude, 0);

            lOKin = fMakeRecord(8, 5, 0, 2);
            lOKinBrain = fMakeBrain(lOKin, fMakeRow(3, 20, 5, 4), 9, lOHost);
            lOHost.ClockValue = 1000 + 0x201;
            lOKinBrain.Update(lOHost);
            fExpectInt("alarm: after 0x200 PIT ticks the alarm is over", lOKin.Goal, 0);

            lOKin = fMakeRecord(8, 5, 0, 2);
            lOKinBrain = fMakeBrain(lOKin, fMakeRow(3, 20, 5, 4), 5, lOHost);
            lOHost.ClockValue = 1000;
            lOKinBrain.Update(lOHost);
            fExpectInt("alarm: the victim itself is not its own kin", lOKin.Goal, 0);
        }

        /// <summary>
        /// Section 7, deviation 43, built 2026-09-20: a land creature that ends its step in
        /// deep water drowns. The original refuses water only in the TILE tests; the step
        /// itself walks in (seg006_1477_476, asm lines 41417-41460), which is why a creature
        /// can only get there while it closes in on the player in melee - the user watched
        /// exactly that in the original on 2026-09-20. The death removal afterwards is the
        /// ordinary one; what it puts down in deep water is destroyed there, which is why
        /// nothing was left (see fCheckWaterSwallowsRemains).
        /// </summary>
        private static void fCheckDrowning()
        {
            // The rule itself (41418-41421 "and ax, 0F8h; cmp ax, 10h").
            fExpectBool("drowning: water under the feet drowns a land creature",
                UWCritterRules.Drowns(0x10, false, false), true);
            fExpectBool("drowning: plain floor does not",
                UWCritterRules.Drowns(0x08, false, false), false);
            fExpectBool("drowning: lava does not - it burns instead",
                UWCritterRules.Drowns(0x20, false, false), false);
            fExpectBool("drowning: standing on an object in the water does not (bit 0x80)",
                UWCritterRules.Drowns(0x10 | 0x80, false, false), false);
            fExpectBool("drowning: the bits outside the 0xF8 mask are ignored",
                UWCritterRules.Drowns(0x10 | 0x1000 | 0x07, false, false), true);
            fExpectBool("drowning: a swimmer never drowns",
                UWCritterRules.Drowns(0x10, true, false), false);
            fExpectBool("drowning: a flier never drowns",
                UWCritterRules.Drowns(0x10, false, true), false);

            // The chain in the brain: the step's verdict and the removal in the SAME update.
            // In the original the drowning is written during the physics, which is step 6, and
            // the animation machine of the same update (step 9) finds animation 0x0C at frame 3
            // and removes the creature. Nothing of the death picture is ever drawn - the user
            // watched for it in the original and saw only the splash (2026-09-20).
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lORecord.Animation = UWCritterBrain.AnimWalking;
            lORecord.Speed = 8;
            lOHost.Step = new StepResult { Drowned = true };

            UWCritterBrain.Motion lOMotion = lOBrain.Update(lOHost);

            fExpectInt("drowning: the death animation 0x0C is set", lORecord.Animation, UWCritterBrain.AnimDying);
            fExpectInt("drowning: at frame 3", lORecord.Frame, UWCritterRules.DeathRemovalFrame);
            fExpectInt("drowning: with interval 1", lORecord.Interval, UWCritterRules.IntervalStuck);
            // As the original since cleanup step 4.12b (2026-10-19): crit_hndlr_walk writes no hp
            // (PATHFIND.C:251-253); ours set 0 until then.
            fExpectInt("drowning: the hit points stay (crit_hndlr_walk)", lORecord.HitPoints, 20);
            fExpectInt("drowning: exactly one splash", lOHost.CountEvents("drowned"), 1);
            fExpectBool("drowning: the body goes in the same update", lOMotion.Removed, true);
            fExpectInt("drowning: the death removal ran once", lOHost.CountEvents("removed"), 1);

            // A further update on the same record must not drown it a second time.
            lOMotion = lOBrain.Update(lOHost);

            fExpectInt("drowning: no second splash", lOHost.CountEvents("drowned"), 1);

            // Goal 3 skips the animation machine: drowned, it never reaches the removal and lives on
            // with its hit points and sequence 0x0C; a blow to 0 starts no death (crit_die refuses a
            // creature in sequence 0x0C, AI.C:1438) - as the original since cleanup step 4.12b.
            FakeCritterHost lOGoal3Host = new FakeCritterHost();
            UWCritterRecord lOGoal3 = fMakeRecord(5, 5, 3, 0);

            lOGoal3Host.PlacePlayer((5 << 3) + 4, (6 << 3) + 4);
            lOGoal3.Animation = UWCritterBrain.AnimWalking;
            lOGoal3Host.Step = new StepResult { Drowned = true };

            UWCritterBrain lOGoal3Brain = fMakeBrain(lOGoal3, fMakeRow(), 5, lOGoal3Host);

            lOMotion = lOGoal3Brain.Update(lOGoal3Host);
            fExpectBool("drowning, goal 3: the splash, sequence 0x0C at frame 3, the hit points kept, not removed",
                lOGoal3Host.CountEvents("drowned") == 1 && lOGoal3.Animation == UWCritterBrain.AnimDying
                && lOGoal3.Frame == UWCritterRules.DeathRemovalFrame && lOGoal3.HitPoints == 20 && !lOMotion.Removed
                && lOGoal3Host.CountEvents("removed") == 0, true);

            lOGoal3Brain.OnDamaged(lOGoal3Host, 1, 500);
            fExpectBool("drowning, goal 3: a blow to 0 starts no death",
                lOGoal3.HitPoints == 0 && lOGoal3Host.CountEvents("death") == 0, true);

            // A swimmer and a flier: the host never reports the water, so nothing happens.
            foreach (bool lbFlier in new[] { false, true })
            {
                FakeCritterHost lOWaterHost = new FakeCritterHost();
                UWCritterRecord lOWaterRecord = fMakeRecord(5, 5, 5, 0);
                UWObjectClassProperties.Critter lORow = fMakeRow();

                lORow.Row[0x0A] = (byte)(lbFlier ? 0x80 : 0x40);
                lOWaterHost.PlacePlayer((5 << 3) + 4, (5 << 3) + 4);

                UWCritterBrain lOWaterBrain = fMakeBrain(lOWaterRecord, lORow, 5, lOWaterHost);

                lOWaterHost.Step = new StepResult
                {
                    Drowned = UWCritterRules.Drowns(0x10, !lbFlier, lbFlier)
                };
                lOWaterBrain.Update(lOWaterHost);

                fExpectBool((lbFlier ? "drowning: a flier" : "drowning: a swimmer") + " crosses the water alive",
                    lOWaterRecord.Animation == UWCritterBrain.AnimDying, false);
            }

            // The TILE tests still keep water out, so no creature ever plans a step into it
            // (handler word 2 = 0x1010 for land, 04-movement.md section 5).
            FakeWalker lOWalker = new FakeWalker();

            for (int liY = 1; liY <= 3; liY++)
                lOWalker.Water[4, liY] = true;

            UWTilePos lONext;

            fExpectBool("drowning: the path search finds a way around the water",
                UWTilePath.TryGetNextStep(lOWalker, new UWTilePos(2, 2), new UWTilePos(6, 2), out lONext), true);
            fExpectBool("drowning: and its next tile is dry", lOWalker.Water[lONext.X, lONext.Y], false);

            for (int liY = 0; liY < 64; liY++)
                lOWalker.Water[4, liY] = true;

            fExpectBool("drowning: a wall of water leaves a land creature no path",
                UWTilePath.TryGetNextStep(lOWalker, new UWTilePos(2, 2), new UWTilePos(6, 2), out lONext), false);

            lOWalker.Swimmer = true;

            fExpectBool("drowning: a swimmer's path needs the water instead",
                UWTilePath.TryGetNextStep(lOWalker, new UWTilePos(4, 2), new UWTilePos(4, 8), out lONext), true);
        }

        /// <summary>
        /// The search window, built 2026-09-21 in place of our ceiling of 400 examined tiles:
        /// PathFindBetweenTiles_seg006_1477_12BB works only inside the box around start and
        /// target, five tiles wider on every side (labels 12F9-13C8), and skips every
        /// neighbour outside it (labels 15DE-15F8). The lower edge clamps at tile 1, so the
        /// solid border row and column 0 are out.
        ///
        /// The original has no memory of dead ends and needs none: this is a breadth-first
        /// search, which does not walk into one.
        /// </summary>
        private static void fCheckPathWindow()
        {
            UWTilePos lONext;

            // A wall from y 6 to 14 across the way from 10/10 to 20/10: the way round at y 5
            // is just inside the window, which reaches from 5 to 15.
            FakeWalker lONear = new FakeWalker();

            for (int liY = 6; liY <= 14; liY++)
                lONear.Water[15, liY] = true;

            fExpectBool("path window: a way round inside the window is found",
                UWTilePath.TryGetNextStep(lONear, new UWTilePos(10, 10), new UWTilePos(20, 10), out lONext), true);

            // The same wall two tiles longer at each end: now the way round would have to
            // leave the window, and the original does not look there.
            FakeWalker lOFar = new FakeWalker();

            for (int liY = 4; liY <= 16; liY++)
                lOFar.Water[15, liY] = true;

            fExpectBool("path window: a way round outside it is not found",
                UWTilePath.TryGetNextStep(lOFar, new UWTilePos(10, 10), new UWTilePos(20, 10), out lONext), false);

            fExpectInt("path window: five tiles beyond each end", UWTilePath.SearchMargin, 5);
            fExpectInt("path window: and never below tile 1", UWTilePath.FirstSearchTile, 1);

            // Row 0 is the map's solid border: a way that only exists there is none.
            FakeWalker lOBorder = new FakeWalker();

            for (int liY = 1; liY < 64; liY++)
                lOBorder.Water[4, liY] = true;

            fExpectBool("path window: the border row 0 is no way round",
                UWTilePath.TryGetNextStep(lOBorder, new UWTilePos(2, 2), new UWTilePos(6, 2), out lONext), false);

            // The waves stop at 0x20 (PathFindBetweenTiles, 2026-09-29): 32 steps are found,
            // 33 are not - Biden's way home of 42 never was.
            FakeWalker lOOpen = new FakeWalker();

            fExpectInt("path length: at most 32 steps", UWTilePath.MaxPathLength, 32);
            fExpectInt("path width: at most 64 tiles a wave", UWTilePath.MaxWaveWidth, 64);
            fExpectBool("path length: 32 steps are found",
                UWTilePath.TryGetNextStep(lOOpen, new UWTilePos(10, 10), new UWTilePos(42, 10), out lONext), true);
            fExpectBool("path length: 33 steps are not",
                UWTilePath.TryGetNextStep(lOOpen, new UWTilePos(10, 10), new UWTilePos(43, 10), out lONext), false);
        }
    }
}
