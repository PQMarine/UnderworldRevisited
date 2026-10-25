using System;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The creature brain against the specification (Docs/AI/creature-ai.md), on FakeCritterHost:
    /// the slot clock, the distance cull, melee range and cadence, the damage reaction, the morale
    /// formula, the blocked step and the goals. BrainSenseChecks and BrainCombatChecks hold the rest
    /// of the group.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// The creature brain against the specification (Docs/AI/creature-ai.md): each
        /// check is one sentence of the spec run on the fake host. Needs no data.
        /// </summary>
        private static void fCheckCritterBrain()
        {
            Console.WriteLine("Creature brain");

            fCheckSlotClock();
            fCheckDistanceCull();
            fCheckMeleeRange();
            fCheckMeleeCadence();
            fCheckDamageReaction();
            fCheckMoraleFormula();
            fCheckBlockedStep();
            fCheckTalkGoal();
            fCheckUnknownGoals();
            fCheckMantras();
            fCheckPerceptionThresholds();
            fCheckLeash();
            fCheckTurnLimiter();
            fCheckKinAlarm();
            fCheckCombatExecution();
            fCheckPlayerAsDefender();
            fCheckDrowning();
            fCheckPathWindow();

            Console.WriteLine();
        }

        /// <summary>Spec 1.1-1.2: interval 4 is due every fourth slot at one slot per frame, the
        /// wrap of the 16-slot clock included; a slow frame is clamped to 4 units; Speed halves.</summary>
        private static void fCheckSlotClock()
        {
            UWCritterClock lOClock = new UWCritterClock();
            int liSlot = 0;
            int liDue = 0;
            // Slot 0 fires when the phase has moved past it, at frame 1; then every fourth.
            int liLastDueFrame = -3;
            bool lbGapsOfFour = true;

            for (int liFrame = 1; liFrame <= 64; liFrame++)
            {
                lOClock.Advance(16, false);

                while (lOClock.IsDue(liSlot))
                {
                    liDue++;

                    if (liFrame - liLastDueFrame != 4)
                        lbGapsOfFour = false;

                    liLastDueFrame = liFrame;
                    liSlot = UWCritterClock.NextSlot(liSlot, 4);
                }
            }

            fExpectInt("clock: interval 4 fires 16 times in 64 slots", liDue, 16);
            fExpectBool("clock: every fourth slot, across the wrap", lbGapsOfFour, true);

            lOClock = new UWCritterClock();
            fExpectInt("clock: a slow frame of 200 ticks hands out 4 units", lOClock.Advance(200, false), 4);
            fExpectBool("clock: slot 0 is due after 4 units", lOClock.IsDue(0), true);
            fExpectBool("clock: slot 4 is not due after 4 units", lOClock.IsDue(4), false);

            lOClock = new UWCritterClock();
            int liFirst = lOClock.Advance(16, true);
            int liSecond = lOClock.Advance(16, true);

            fExpectInt("clock: Speed halves the units, odd one kept", liFirst * 10 + liSecond, 1);
        }

        /// <summary>Spec 1.3: beyond 10 tiles the update returns with the slot advanced by 8;
        /// at exactly 10 tiles it runs.</summary>
        private static void fCheckDistanceCull()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(2, 2, 0, 2);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lOHost.PlacePlayer((13 << 3) + 4, (2 << 3) + 4);

            UWCritterBrain.Motion lOMotion = lOBrain.Update(lOHost);

            fExpectBool("cull: 11 tiles away is culled", lOMotion.Culled, true);
            fExpectInt("cull: the slot advances by 8", lORecord.DueSlot, 8);

            lOHost.PlacePlayer((12 << 3) + 4, (2 << 3) + 4);
            lOMotion = lOBrain.Update(lOHost);
            fExpectBool("cull: 10 tiles away runs", lOMotion.Culled, false);
        }

        /// <summary>Spec 6.5 and 8.2: at 9 eighths goal 5 walks in and never strikes, because
        /// NPC_Goto overwrites the rolled swing; at 8 eighths it stands and the swing survives.</summary>
        private static void fCheckMeleeRange()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lOHost.PlacePlayer(44 + 9, 44);

            bool lbAlwaysWalking = true;

            for (int liAt = 0; liAt < 8; liAt++)
            {
                lOBrain.Update(lOHost);

                if (lORecord.Animation != UWCritterBrain.AnimWalking)
                    lbAlwaysWalking = false;
            }

            fExpectBool("melee: at 9 eighths the creature walks in every update", lbAlwaysWalking, true);
            fExpectBool("melee: at 9 eighths no swing survives, no blow", lOHost.HasEvent("swing") || lOHost.HasEvent("blow"), false);
            fExpectInt("melee: it walks at the pursuit speed", lORecord.Speed, 8);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lOHost.PlacePlayer(44 + 8, 44);
            lOBrain.Update(lOHost);

            fExpectInt("melee: at 8 eighths the 1-in-4 roll starts the swing (animation 1)", lORecord.Animation, 1);
            fExpectInt("melee: the swing starts at frame 0", lORecord.Frame, 0);
            fExpectInt("melee: standing, speed 0", lORecord.Speed, 0);
        }

        /// <summary>Spec 8.2: failed rolls raise the charge index, the swing lands on the fifth
        /// update after the decision with the charge table value, and the blow clears it.</summary>
        private static void fCheckMeleeCadence()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            // The player looks east, away from the creature east of him: a blow from behind.
            lOHost.PlacePlayer(44 + 8, 44, 2);
            lORecord.SetDestination(6, 5, 0);
            lORecord.NewDestination = false;
            lOHost.DefaultRoll = 1;

            for (int liAt = 0; liAt < 3; liAt++)
                lOBrain.Update(lOHost);

            fExpectInt("cadence: three failed rolls raise the charge index to 3", lORecord.ChargeIndex, 3);
            fExpectInt("cadence: the combat idle stays animation 0", lORecord.Animation, 0);

            // Dexterity roll, the start roll, the attack pick.
            lOHost.Rolls.Enqueue(1);
            lOHost.Rolls.Enqueue(0);
            lOHost.Rolls.Enqueue(0);
            lOBrain.Update(lOHost);

            fExpectInt("cadence: the swing starts", lORecord.Animation, 1);

            int liBlowUpdate = 0;

            for (int liAt = 1; liAt <= 6 && liBlowUpdate == 0; liAt++)
            {
                lOBrain.Update(lOHost);

                if (lOHost.HasEvent("blow"))
                    liBlowUpdate = liAt;
            }

            fExpectInt("cadence: the blow lands on the fifth update after the decision", liBlowUpdate, 5);
            fExpectBool("cadence: the blow carries charge 80 (index 3) and flank 4 from behind",
                lOHost.HasEvent("blow 1 0 charge 80 flank 4"), true);
            fExpectInt("cadence: the blow clears the charge index", lORecord.ChargeIndex, 0);
            fExpectBool("cadence: the swing started the combat music", lOHost.HasEvent("music 6"), true);
            fExpectInt("cadence: after the blow the combat idle resumes", lORecord.Animation, 0);
        }

        /// <summary>Spec 4.3: a far hit sets bit 5 and goal 5 without a roll; a near hit rolls
        /// the morale check; bit 4 gives goal 9; bit 5 gives goal 5 at any distance.</summary>
        private static void fCheckDamageReaction()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 10);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(3, 20, 0), 5, lOHost);

            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lOBrain.OnDamaged(lOHost, 1, 3);

            fExpectInt("reaction: DamageNPC stores the attacker", lORecord.Attacker, 1);
            fExpectInt("reaction: DamageNPC adds the damage", lORecord.DamageTaken, 3);
            fExpectInt("reaction: DamageNPC subtracts the hit points", lORecord.HitPoints, 7);

            lOHost.Rolls.Enqueue(9);
            lOBrain.Update(lOHost);

            fExpectBool("reaction: a far hit sets the relentless bit", lORecord.Relentless, true);
            fExpectInt("reaction: a far hit gives goal 5", lORecord.Goal, 5);
            fExpectInt("reaction: the attacker becomes the target", lORecord.GTarg, 1);
            fExpectInt("reaction: the attacker byte is cleared", lORecord.Attacker, 0);
            fExpectInt("reaction: the damage byte is cleared", lORecord.DamageTaken, 0);
            fExpectInt("reaction: a far hit makes the creature hostile", lORecord.Attitude, 0);

            // Near: hp 10 of 20, morale 0, damage 3 -> 15 >= 8 + roll, flee on any roll.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 13);
            lOBrain = fMakeBrain(lORecord, fMakeRow(3, 20, 0), 5, lOHost);
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lOBrain.OnDamaged(lOHost, 1, 3);
            lOHost.Rolls.Enqueue(2);
            lOBrain.Update(lOHost);

            fExpectInt("reaction: a near hit rolls the morale check and flees (goal 6)", lORecord.Goal, 6);
            fExpectBool("reaction: the roll was consumed", lOHost.Rolls.Count == 0, true);
            fExpectBool("reaction: a near hit leaves bit 5 clear", lORecord.Relentless, false);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 13);
            lOBrain = fMakeBrain(lORecord, fMakeRow(3, 20, 0), 5, lOHost);
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lORecord.MadeStand = true;
            lOBrain.OnDamaged(lOHost, 1, 3);
            lOBrain.Update(lOHost);

            fExpectInt("reaction: bit 4 gives goal 9 without a roll", lORecord.Goal, 9);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 13);
            lOBrain = fMakeBrain(lORecord, fMakeRow(3, 20, 0), 5, lOHost);
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lORecord.MadeStand = true;
            lORecord.Relentless = true;
            lOBrain.OnDamaged(lOHost, 1, 3);
            lOBrain.Update(lOHost);

            fExpectInt("reaction: bit 5 gives goal 5 before bit 4", lORecord.Goal, 5);

            // THE WHOLE SEQUENCE ON ONE CREATURE, which is what the counter-test in the game
            // asks (2026-09-21): first a hit from afar, then one in melee that would certainly
            // make it flee on its own - it must not. Same numbers as the near case above, where
            // the same creature does flee.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 13);
            lOBrain = fMakeBrain(lORecord, fMakeRow(3, 20, 0), 5, lOHost);

            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lOBrain.OnDamaged(lOHost, 1, 3);
            lOBrain.Update(lOHost);

            fExpectBool("flee block: the far hit sets the bit", lORecord.Relentless, true);

            // And now from one tile away, with hit points that would flee on any roll.
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lORecord.HitPoints = 10;
            lOBrain.OnDamaged(lOHost, 1, 3);
            lOBrain.Update(lOHost);

            fExpectInt("flee block: the melee hit afterwards pursues instead of fleeing", lORecord.Goal, 5);
            fExpectBool("flee block: no morale roll was needed", lOHost.Rolls.Count == 0, true);
            fExpectBool("flee block: the bit stays", lORecord.Relentless, true);

            // Another creature's blow does not provoke a bystander.
            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 0, 2, -1, -1, 13);
            lOBrain = fMakeBrain(lORecord, fMakeRow(3, 20, 0), 5, lOHost);
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lOHost.Objects[7] = new CritterTarget { Exists = true, Index = 7, IsCreature = true, ItemId = 0x45, X = 52, Y = 44, TileX = 6, TileY = 5, HitPoints = 9 };
            lOBrain.OnDamaged(lOHost, 7, 3);
            lOBrain.Update(lOHost);

            fExpectInt("reaction: a hit by a creature that is no ally is ignored", lORecord.Goal, 0);
        }

        /// <summary>Spec 4.4: the four boundaries of the morale check.</summary>
        private static void fCheckMoraleFormula()
        {
            fExpectBool("morale: hp above three quarters never withdraws", UWCritterRules.MoraleCheck(16, 13, 0, 0, 0), false);
            fExpectBool("morale: hp at three quarters goes on to the roll", UWCritterRules.MoraleCheck(16, 12, 0, 0, 0), true);
            fExpectBool("morale: hp below an eighth fights to the death", UWCritterRules.MoraleCheck(16, 1, 0, 9, 0), false);
            fExpectBool("morale: hp at an eighth goes on", UWCritterRules.MoraleCheck(16, 2, 0, 9, 0), true);
            fExpectBool("morale: damage above half the vitality withdraws", UWCritterRules.MoraleCheck(16, 8, 15, 9, 3), true);
            fExpectBool("morale: damage at half goes on to the roll", UWCritterRules.MoraleCheck(16, 8, 15, 8, 3), false);
            fExpectBool("morale: 15 - morale >= hp * 16 / vitality + roll, at equality", UWCritterRules.MoraleCheck(16, 8, 7, 0, 0), true);
            fExpectBool("morale: one above and it stays", UWCritterRules.MoraleCheck(16, 8, 7, 0, 1), false);
            fExpectBool("morale: vitality 0 never withdraws", UWCritterRules.MoraleCheck(0, 0, 0, 0, 0), false);
        }

        /// <summary>Spec 7.1, 7.3, 6.3: a collision with the player sets the blocked bit and the
        /// wander step turns a quarter with speed 0; two attackers do not block; one in eight
        /// clears the bit.</summary>
        private static void fCheckBlockedStep()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lORecord.SetDestination(8, 5, 0);
            lORecord.NewDestination = false;
            lORecord.StraightLineKnown = true;
            lORecord.FineHeading = 64;
            lORecord.FacingEighth = 2;
            lORecord.Speed = 8;
            lORecord.Animation = UWCritterBrain.AnimWalking;
            lOHost.DefaultRoll = 2;
            lOHost.Step = new StepResult { Collided = true, HitObject = true, HitObjectIndex = 1, HitObjectItemId = 0x7F, HitObjectIsCreature = true };
            lOBrain.Update(lOHost);

            fExpectBool("blocked: a collision with the player sets the blocked bit", lORecord.Blocked, true);
            fExpectInt("blocked: the wander step turns a quarter circle", lORecord.FineHeading, 0);
            fExpectInt("blocked: and stands this update", lORecord.Speed, 0);
            fExpectBool("blocked: the straight line is forgotten", lORecord.StraightLineKnown, false);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lOHost.Objects[7] = new CritterTarget { Exists = true, Index = 7, IsCreature = true, ItemId = 0x45, X = 52, Y = 44, TileX = 6, TileY = 5, HitPoints = 9, Goal = 5 };
            lORecord.SetDestination(8, 5, 0);
            lORecord.NewDestination = false;
            lORecord.StraightLineKnown = true;
            lORecord.Speed = 8;
            lOHost.DefaultRoll = 2;
            lOHost.Step = new StepResult { Collided = true, HitObject = true, HitObjectIndex = 7, HitObjectItemId = 0x45, HitObjectIsCreature = true };
            lOBrain.Update(lOHost);

            fExpectBool("blocked: two attackers bumping do not block", lORecord.Blocked, false);
            fExpectBool("blocked: the bump still drops the straight line and takes the path", lORecord.HasPath, true);
            fExpectInt("blocked: and the walk goes on", lORecord.Animation, UWCritterBrain.AnimWalking);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lORecord.SetDestination(8, 5, 0);
            lORecord.NewDestination = false;
            lORecord.Blocked = true;
            lORecord.Animation = UWCritterBrain.AnimWalking;
            lOHost.DefaultRoll = 1;
            lOBrain.Update(lOHost);

            fExpectBool("blocked: RNG(8) != 0 keeps the bit", lORecord.Blocked, true);
            fExpectInt("blocked: the reset sends it to goal 2", lORecord.Goal, 2);

            lOHost = new FakeCritterHost();
            lORecord = fMakeRecord(5, 5, 5, 0);
            lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);
            lOHost.PlacePlayer((8 << 3) + 4, 44);
            lORecord.SetDestination(8, 5, 0);
            lORecord.NewDestination = false;
            lORecord.Blocked = true;
            lORecord.Animation = UWCritterBrain.AnimWalking;
            lOHost.DefaultRoll = 0;
            lOBrain.Update(lOHost);

            fExpectBool("blocked: RNG(8) == 0 clears the bit", lORecord.Blocked, false);
            fExpectInt("blocked: the wander step walks on", lORecord.Animation, UWCritterBrain.AnimWalking);
            fExpectInt("blocked: goal 5 stays", lORecord.Goal, 5);
        }

        /// <summary>Spec 6.8: goal 10 turns within 0x190 squared eighths and talks within 0x90
        /// when the player looks back.</summary>
        private static void fCheckTalkGoal()
        {
            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 10, 2);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

            lOHost.PlacePlayer(44 + 20, 44, 6, 15, 15);
            lOBrain.Update(lOHost);

            fExpectInt("talk: at 0x190 the creature only stands", lORecord.Animation, UWCritterBrain.AnimStanding);
            fExpectInt("talk: and does not turn", lORecord.FacingEighth, 0);

            lOHost.PlacePlayer(44 + 19, 44, 6, 15, 15);
            lOBrain.Update(lOHost);

            fExpectInt("talk: below 0x190 it turns, one eighth per update", lORecord.FacingEighth, 1);
            fExpectBool("talk: but does not speak yet", lOHost.HasEvent("talk"), false);

            lOBrain.Update(lOHost);
            fExpectInt("talk: the second update completes the turn", lORecord.FacingEighth, 2);

            lOHost.PlacePlayer(44 + 12, 44, 6, 15, 15);
            lOBrain.Update(lOHost);
            fExpectBool("talk: at 0x90 it does not speak", lOHost.HasEvent("talk"), false);

            lOHost.PlacePlayer(44 + 11, 44, 2, 15, 15);
            lOBrain.Update(lOHost);
            fExpectBool("talk: below 0x90 with the player looking away it waits", lOHost.HasEvent("talk"), false);

            lOHost.PlacePlayer(44 + 11, 44, 6, 15, 15);
            lOBrain.Update(lOHost);
            fExpectBool("talk: below 0x90 with the player looking back it speaks", lOHost.HasEvent("talk"), true);
            fExpectInt("talk: interval 6 while waiting", lORecord.Interval, 6);
        }

        /// <summary>
        /// Goals 13 to 15 are the default case of the goal switch and do nothing but set the
        /// interval (SwitchGoals_seg007_1798_3247, 54050-54056). Pinned on 2026-09-22, when the
        /// Todo's "petrification" turned out to be a label of ours with nothing behind it and
        /// the shipped level data turned out to place no such creature at all.
        /// </summary>
        private static void fCheckUnknownGoals()
        {
            Check.Group("Goals 13 to 15");

            for (int liGoal = 13; liGoal <= 15; liGoal++)
            {
                FakeCritterHost lOHost = new FakeCritterHost();
                UWCritterRecord lORecord = fMakeRecord(5, 5, liGoal, 0);
                UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

                lOHost.PlacePlayer((6 << 3) + 4, (6 << 3) + 4);
                lOBrain.Update(lOHost);

                fExpectInt("goal " + liGoal + ": takes the interval of the default case",
                    lORecord.Interval, UWCritterRules.IntervalUnknownGoal);
                fExpectInt("goal " + liGoal + ": and keeps its goal", lORecord.Goal, liGoal);
                fExpectBool("goal " + liGoal + ": and never starts walking",
                    lORecord.Animation == UWCritterBrain.AnimWalking, false);
                fExpectInt("goal " + liGoal + ": the player right beside it changes nothing",
                    lORecord.Goal, liGoal);
            }
        }

        /// <summary>
        /// The two quest mantras, built 2026-09-22 out of
        /// ChantMantraAtShrine_ovr143_48D label 581 and the direction routine
        /// GetDetectedCreatureDirections_ovr154_11B.
        /// </summary>
        private static void fCheckMantras()
        {
            Check.Group("Mantras");

            fExpectInt("mantra: INSAHN is the Cup of Wonder", UWShrineRules.CupOfWonderMantra, 20);
            fExpectInt("mantra: FANLO is the Key of Truth", UWShrineRules.KeyOfTruthMantra, 21);
            fExpectInt("mantra: NO is the empty one", UWShrineRules.EmptyMantra, 22);
            fExpectInt("mantra: the cup lies on tile 24", UWShrineRules.CupTileX, 24);
            fExpectInt("mantra: and on 45", UWShrineRules.CupTileY, 45);
            fExpectInt("mantra: on level three", UWShrineRules.CupLevel, 3);
            fExpectInt("mantra: the key is object 225", UWShrineRules.KeyOfTruthObjectId, 225);

            // The eight directions of GetCardinal_ovr154_7C: 0 north, clockwise, and the Y
            // axis runs north. A straight direction needs one distance to be more than twice
            // the other.
            fExpectInt("mantra: straight north is 0",
                UWShrineRules.GetCardinalDirection(10, 10, 10, 20), 0);
            fExpectInt("mantra: straight east is 2",
                UWShrineRules.GetCardinalDirection(10, 10, 20, 10), 2);
            fExpectInt("mantra: straight south is 4",
                UWShrineRules.GetCardinalDirection(10, 10, 10, 0), 4);
            fExpectInt("mantra: straight west is 6",
                UWShrineRules.GetCardinalDirection(10, 10, 0, 10), 6);
            fExpectInt("mantra: northeast is 1",
                UWShrineRules.GetCardinalDirection(10, 10, 20, 20), 1);
            fExpectInt("mantra: southeast is 3",
                UWShrineRules.GetCardinalDirection(10, 10, 20, 0), 3);
            fExpectInt("mantra: southwest is 5",
                UWShrineRules.GetCardinalDirection(10, 10, 0, 0), 5);
            fExpectInt("mantra: northwest is 7",
                UWShrineRules.GetCardinalDirection(10, 10, 0, 20), 7);

            // The boundary: straight needs half of the one distance to be GREATER than the
            // other, so twice as far is still the diagonal and anything beyond it is straight.
            fExpectInt("mantra: exactly twice as far is still the diagonal",
                UWShrineRules.GetCardinalDirection(0, 0, 20, 10), 1);
            fExpectInt("mantra: one step less and it is straight east",
                UWShrineRules.GetCardinalDirection(0, 0, 20, 9), 2);
        }
    }
}
