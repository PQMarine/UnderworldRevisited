using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.12 (2026-10-19; B22): the creature's death as one state machine on
    /// UWCreatureActor (Alive, Dying, Removed), run headless with the 4.11 fakes (FakeCreatureWorld,
    /// FakeCreatureView) and the brain's own updates on a clock of the check's. Pinned: the kill by
    /// the player (experience once, the body committed once, a second blow starting no second
    /// death), the end at frame 3 with the special death and the remains exactly once, a dying
    /// creature far from the player and under Freeze Time staying Dying until its updates run again
    /// (critter_ai's distance cull, no updates under the freeze - until this step the remains came
    /// after three seconds without the special death), the drowning (the splash, then the commit
    /// and the end in one go), the golem's refusal (Alive at 0 hit points, saved so), the creature
    /// saved while dying (B11) and the goal that never finishes dying. Needs the installation (the
    /// creature table, level 1 for the world).
    /// </summary>
    public static partial class Program
    {
        /// <summary>The tile the check's creatures stand on; the player stands beside it or far away.</summary>
        private const int DeathCheckTile = 5;

        private const int DeathCheckFarTile = 40;

        private static void fCheckCreatureDeath(DataImport pOData)
        {
            Check.Group("Creature death machine (cleanup step 4.12)");

            if (pOData == null || pOData.Levels == null || pOData.Levels.Count == 0 || pOData.ObjectClassProperties == null
                || pOData.CommonObjectProperties == null)
            {
                fFail("creature death", "no data");

                return;
            }

            // A walking kind of level 1 with a table row (the goblins), as the actor's checks take it.
            UWNpc lOModel = null;
            UWObjectClassProperties.Critter lORow = default;

            foreach (UWObject lOObject in pOData.Levels[0].Masterlist)
            {
                if (lOObject is UWNpc lONpc && UWObjectIds.IsCreature(lONpc.ID) && lONpc.HitPoints > 0
                    && pOData.ObjectClassProperties.TryGetCritter(lONpc.ID, out UWObjectClassProperties.Critter lOCandidate)
                    && !lOCandidate.IsFlier && !lOCandidate.IsSwimmer && lOCandidate.Vitality > 0)
                {
                    lOModel = lONpc;
                    lORow = lOCandidate;

                    break;
                }
            }

            if (lOModel == null)
            {
                fFail("creature death", "level 1 has no walking creature");

                return;
            }

            int liDreams = UWQuestFlags.Get(UWPlayerData.DreamQuestFlag);

            try
            {
                fCheckDeathByPlayer(pOData, lOModel.ID, lORow);
                fCheckDyingStruckThroughRouting(pOData, lOModel.ID, lORow);
                fCheckDeathFarAway(pOData, lOModel.ID, lORow);
                fCheckDeathFrozen(pOData, lOModel.ID, lORow);
                fCheckDeathDrowned(pOData, lOModel.ID, lORow);
                fCheckDeathRefused(pOData, lOModel.ID, lORow);
                fCheckDeathLoaded(pOData, lOModel.ID, lORow);
                fCheckDeathNeverEnding(pOData, lOModel.ID, lORow);
                fCheckDeathDropChain();
                fCheckDyingInTileList(pOData, lOModel.ID, lORow);
            }
            finally
            {
                UWQuestFlags.Set(UWPlayerData.DreamQuestFlag, liDreams);
            }
        }

        /// <summary>2026-10-19 (the user's decision "as the original"): the record is in its tile list
        /// while alive and while dying, until the removal at frame 3 (critter/AI.C:1135-1140) - what
        /// the collision scan of can_place finds (motion/COLLIDE.C:466-527, no dying test), so a
        /// dying body is in a summon's way (UWWorld.CreatureFitsAt); the blows ask the same.</summary>
        private static void fCheckDyingInTileList(DataImport pOData, int piItemId, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOData.Levels[0]);
            ActorUnderTest lOOne = fDeathActor(lOWorld, piItemId, pORow, 0);
            ICritterHost lIHost = lOOne.Host;

            bool lbAlive = lOOne.Actor.IsInTileList;

            lIHost.OnDeathStarted(false);

            bool lbDying = lOOne.Actor.DeathState == UWCreatureDeathState.Dying && lOOne.Actor.IsInTileList
                && lOOne.Actor.TakesBlows;

            lIHost.OnDeathFinished();
            fExpectBool("tile list: alive and dying the body is in the way (can_place), removed it is not",
                lbAlive && lbDying && !lOOne.Actor.IsInTileList && !lOOne.Actor.TakesBlows, true);
        }

        /// <summary>A creature of the check's own on tile 5/5, not in the level's list: 20 hit points,
        /// this goal and whoami, standing, due at slot 0 every four slots.</summary>
        private static ActorUnderTest fDeathActor(FakeCreatureWorld pOWorld, int piItemId, UWObjectClassProperties.Critter pORow,
            int piWhoami, int piGoal = 8, int piHitPoints = 20, int piAnimation = UWCritterBrain.AnimStanding, int piFrame = 0)
        {
            UWNpc lONpc = new UWNpc((ushort)piItemId) { RawNpcBytes = new byte[19] };
            UWCritterRecord lOProbe = new UWCritterRecord(lONpc);

            lOProbe.TileX = DeathCheckTile;
            lOProbe.TileY = DeathCheckTile;
            lOProbe.FineX = 4;
            lOProbe.FineY = 4;
            lOProbe.HitPoints = piHitPoints;
            lOProbe.Goal = piGoal;
            lOProbe.Animation = piAnimation;
            lOProbe.Frame = piFrame;
            lOProbe.Interval = UWCritterRules.IntervalWalking;
            lOProbe.DueSlot = 0;

            lONpc.NPCwhoami = (byte)piWhoami;
            lONpc.RawNpcBytes[UWNpcRecordCodec.WhoAmIByte] = (byte)piWhoami;

            return fMakeActor(pOWorld, lONpc, pORow);
        }

        /// <summary>The player beside the creature, or far from it (more than ten tiles: the brain's
        /// distance cull, the view being far as well in the fake world).</summary>
        private static void fPlacePlayerForDeath(FakeCreatureWorld pOWorld, bool pbNear)
        {
            int liTile = pbNear ? DeathCheckTile : DeathCheckFarTile;

            pOWorld.PlayerThere = true;
            pOWorld.Player = new CritterTarget
            {
                Exists = true,
                Index = UWCritterBrain.PlayerIndex,
                IsPlayer = true,
                TileX = liTile,
                TileY = liTile + 1,
                X = (liTile << 3) + 4,
                Y = ((liTile + 1) << 3) + 4,
                HitPoints = 30
            };
        }

        /// <summary>Frames of the driver: the clock advances by a frame's four slots (frozen: by none,
        /// as UWCritterDriver hands it Freeze Time) and the creature runs what is due.</summary>
        private static void fRunDeathFrames(ActorUnderTest pOActor, UWCritterClock pOClock, int piFrames, bool pbFrozen = false)
        {
            for (int liFrame = 0; liFrame < piFrames; liFrame++)
            {
                pOClock.Advance(UWCritterRules.SlotPitTicks * UWCritterRules.MaxSlotsPerFrame, false, pbFrozen);
                pOActor.Actor.RunDueUpdates(pOClock);
            }
        }

        private static int fCountLines(List<string> pOLog, string psLine)
        {
            return pOLog.FindAll(psEntry => psEntry == psLine).Count;
        }

        /// <summary>The player's kill near him: Alive, Dying with the experience and the body committed
        /// once, a second blow without a second death, Removed at frame 3 with the remains once.</summary>
        private static void fCheckDeathByPlayer(DataImport pOData, int piItemId, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOData.Levels[0]);
            ActorUnderTest lOOne = fDeathActor(lOWorld, piItemId, pORow, 0);
            UWCritterClock lOClock = new UWCritterClock();
            UWPlayerVitals lOVitals = lOWorld.WorldFake.Body.Numbers;

            fPlacePlayerForDeath(lOWorld, true);
            fExpectBool("by the player: a new creature is alive", lOOne.Actor.DeathState == UWCreatureDeathState.Alive
                && !lOOne.Actor.IsDead && !lOOne.Health.IsDestroyed, true);

            int liExperience = lOVitals.Experience;

            lOOne.Actor.TakeBlow(500, UWCritterBrain.PlayerIndex);

            int liGained = lOVitals.Experience - liExperience;

            fExpectBool("by the player: Dying, the body committed once",
                lOOne.Actor.DeathState == UWCreatureDeathState.Dying && lOOne.Actor.IsDead && lOOne.Health.IsDestroyed
                && fCountLines(lOOne.View.Log, "destroyed") == 1, true);
            fExpectBool("by the player: the kill's experience (" + liGained + ")", liGained > 0, true);
            fExpectBool("by the player: sequence 0x0C from frame 0 with 0 hit points",
                lOOne.Actor.Record.Animation == UWCritterBrain.AnimDying && lOOne.Actor.Record.Frame == 0
                && lOOne.Actor.Record.HitPoints == 0, true);

            liExperience = lOVitals.Experience;
            lOOne.Actor.TakeBlow(5, UWCritterBrain.PlayerIndex);
            fExpectBool("by the player: a blow on the dying one lands, but starts no second death",
                lOVitals.Experience == liExperience && fCountLines(lOOne.View.Log, "destroyed") == 1
                && lOOne.View.Events.FindAll(psEvent => psEvent.StartsWith("dying")).Count == 1
                && lOOne.Actor.Record.Animation == UWCritterBrain.AnimDying, true);

            fRunDeathFrames(lOOne, lOClock, 3);
            fExpectBool("by the player: three updates later still dying, at frame 3",
                lOOne.Actor.DeathState == UWCreatureDeathState.Dying && lOOne.Actor.Record.Frame == UWCritterRules.DeathRemovalFrame
                && !lOOne.View.Log.Contains("complete death"), true);

            fRunDeathFrames(lOOne, lOClock, 1);
            fExpectBool("by the player: frame 3's update removes it, remains once",
                lOOne.Actor.DeathState == UWCreatureDeathState.Removed && lOOne.Actor.IsRemoved
                && fCountLines(lOOne.View.Log, "complete death") == 1, true);

            fRunDeathFrames(lOOne, lOClock, 8);
            ((ICritterHost)lOOne.Actor).OnDeathFinished();
            fExpectBool("by the player: nothing more - no update, no second end",
                fCountLines(lOOne.View.Log, "complete death") == 1 && fCountLines(lOOne.View.Log, "destroyed") == 1, true);
        }

        /// <summary>The Unity world's bodies for one actor, as UWWorld's Bodies carry a blow out: the
        /// creature takes blows by its death machine (UWDamageable.TakesBlows asks
        /// UWCreatureActor.TakesBlows) and its body hands the damage with the attacker to DamageNPC
        /// (UWDamageable.CreatureTakesBlow, UWCritter, UWCreatureActor.TakeBlow).</summary>
        private sealed class ActorBlowBodies : IUWBlowBodies
        {
            private readonly UWCreatureActor mOActor;

            public ActorBlowBodies(UWCreatureActor pOActor)
            {
                mOActor = pOActor;
            }

            public bool CreatureTakesBlows(UWDamageTarget pOTarget) => mOActor.TakesBlows;

            public void DamageBody(UWDamageTarget pOTarget, int piDamage, int piDamageType, int piAttacker)
            {
                mOActor.TakeBlow(piDamage, piAttacker);
            }

            public void DamagePlayer(UWDamageRequest pORequest)
            {
            }

            public void StrikeThingInFlight(int piFlightIndex, int piDamage, int piDamageType)
            {
            }
        }

        /// <summary>
        /// Cleanup step 4.12c (2026-10-19, the user's decision "as the original"): the player's blow on
        /// a DYING creature reaches DamageNPC through the world's routing - damage_critter has no dying
        /// test (UnderworldExhumed critter/AI.C:1458-1513): the attacker byte, the kin alarm, the
        /// foe-hurt music; the tally grows by the whole damage (since 2026-10-19, the user's decision
        /// "as the original"; by what the hit points lost, 0, until then - the clamp of cleanup step
        /// 4.8 in UWCreatureActor.TakeBlow); crit_die starts no second death and no second experience.
        /// A removed one takes nothing. Until 4.12c the routing dropped the blow on a committed body.
        /// </summary>
        private static void fCheckDyingStruckThroughRouting(DataImport pOData, int piItemId, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOData.Levels[0]);
            ActorUnderTest lOOne = fDeathActor(lOWorld, piItemId, pORow, 0);
            UWPlayerVitals lOVitals = lOWorld.WorldFake.Body.Numbers;
            ActorBlowBodies lOBodies = new ActorBlowBodies(lOOne.Actor);
            UWDamageTarget lOTarget = UWDamageTarget.Creature(lOOne.Actor.Index);
            UWCritterClock lOClock = new UWCritterClock();

            fPlacePlayerForDeath(lOWorld, true);
            lOOne.Actor.Record.IsAlly = false;
            fExpectBool("dying, routed: a living one takes blows", lOOne.Actor.TakesBlows, true);

            UWBlowRouting.Land(lOBodies, lOTarget, UWBlow.PlayerBlow(500));

            int liExperience = lOVitals.Experience;

            fExpectBool("dying, routed: the player's blow kills - Dying, the body committed once",
                lOOne.Actor.DeathState == UWCreatureDeathState.Dying && fCountLines(lOOne.View.Log, "destroyed") == 1
                && lOOne.Actor.TakesBlows, true);

            // Another attacker in byte 0x12, the tally and the alarm cleared, the music logs emptied:
            // what the next blow writes is its own.
            lOOne.Actor.Record.Attacker = 7;
            lOOne.Actor.Record.DamageTaken = 0;
            UWGameSession.Current.CritterAlarm.Reset();
            lOWorld.Log.Clear();
            lOWorld.WorldFake.Shown.Log.Clear();

            UWBlowRouting.Land(lOBodies, lOTarget, UWBlow.PlayerBlow(6));

            fExpectBool("dying, routed: the player's blow stores the player as attacker (byte 0x12)",
                lOOne.Actor.Record.Attacker == UWCritterBrain.PlayerIndex, true);
            fExpectBool("dying, routed: the kin alarm names the dying one",
                UWGameSession.Current.CritterAlarm.Kind != UWCritterAlarm.NoKind
                && UWGameSession.Current.CritterAlarm.Index == lOOne.Actor.Index, true);
            fExpectBool("dying, routed: the foe-hurt music and the combat timer",
                lOWorld.WorldFake.Shown.Log.Contains("music " + UWCritterRules.HurtCombatTheme)
                && lOWorld.Log.Contains("mark combat"), true);
            fExpectInt("dying, routed: the tally grows by the whole damage (SET_DAMAGE)", lOOne.Actor.Record.DamageTaken, 6);

            // The byte wraps as the original's field does (include/object.h:286).
            lOOne.Actor.Record.DamageTaken = 250;
            UWBlowRouting.Land(lOBodies, lOTarget, UWBlow.PlayerBlow(10));
            fExpectInt("dying, routed: the tally is a byte - 250 and 10 wrap to 4", lOOne.Actor.Record.DamageTaken, 4);
            fExpectBool("dying, routed: no second death, no second experience, still at 0 hit points",
                lOVitals.Experience == liExperience && fCountLines(lOOne.View.Log, "destroyed") == 1
                && lOOne.View.Events.FindAll(psEvent => psEvent.StartsWith("dying")).Count == 1
                && lOOne.Actor.DeathState == UWCreatureDeathState.Dying && lOOne.Actor.Record.HitPoints == 0, true);

            // A creature's blow the same way: its index in byte 0x12, no alarm.
            UWGameSession.Current.CritterAlarm.Reset();
            UWBlowRouting.Land(lOBodies, lOTarget, UWBlow.CreatureBlow(4, 9));
            fExpectBool("dying, routed: a creature's blow stores its attacker, no alarm",
                lOOne.Actor.Record.Attacker == 9 && UWGameSession.Current.CritterAlarm.Kind == UWCritterAlarm.NoKind, true);

            fRunDeathFrames(lOOne, lOClock, 4);
            lOOne.Actor.Record.Attacker = 7;
            UWBlowRouting.Land(lOBodies, lOTarget, UWBlow.PlayerBlow(6));
            fExpectBool("dying, routed: once removed it takes nothing",
                lOOne.Actor.IsRemoved && !lOOne.Actor.TakesBlows && lOOne.Actor.Record.Attacker == 7, true);
        }

        /// <summary>B22: killed by another creature far from the player, Tyball's whoami: no end while
        /// the brain culls it, the special death and the remains once when the player comes near.</summary>
        private static void fCheckDeathFarAway(DataImport pOData, int piItemId, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOData.Levels[0]);
            ActorUnderTest lOTyball = fDeathActor(lOWorld, piItemId, pORow, UWSpecialDeaths.TyballWhoami);
            UWCritterClock lOClock = new UWCritterClock();
            UWPlayerVitals lOVitals = lOWorld.WorldFake.Body.Numbers;

            UWQuestFlags.Set(UWPlayerData.DreamQuestFlag, 0);
            fPlacePlayerForDeath(lOWorld, false);

            int liExperience = lOVitals.Experience;

            lOTyball.Actor.TakeBlow(500, 7);
            fExpectBool("far: another creature's kill gives no experience, the machine is Dying",
                lOVitals.Experience == liExperience && lOTyball.Actor.DeathState == UWCreatureDeathState.Dying, true);

            fRunDeathFrames(lOTyball, lOClock, 40);
            fExpectBool("far: forty frames later still Dying at frame 0 - no remains, no special death (B22)",
                lOTyball.Actor.DeathState == UWCreatureDeathState.Dying && lOTyball.Actor.Record.Frame == 0
                && !lOTyball.View.Log.Contains("complete death") && !lOWorld.WorldFake.Shown.Log.Contains("cutscene 2"), true);

            fPlacePlayerForDeath(lOWorld, true);
            fRunDeathFrames(lOTyball, lOClock, 12);
            fExpectBool("far: the player near, it ends on its own updates - the special death once and the remains once",
                lOTyball.Actor.DeathState == UWCreatureDeathState.Removed
                && fCountLines(lOWorld.WorldFake.Shown.Log, "cutscene 2") == 1
                && fCountLines(lOTyball.View.Log, "complete death") == 1, true);
            fExpectInt("far: Tyball's dream bit", UWQuestFlags.Get(UWPlayerData.DreamQuestFlag) & 4, 4);
        }

        /// <summary>B22: killed, then Freeze Time - no updates, so it stays Dying; it ends once when the
        /// freeze is over.</summary>
        private static void fCheckDeathFrozen(DataImport pOData, int piItemId, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOData.Levels[0]);
            ActorUnderTest lOOne = fDeathActor(lOWorld, piItemId, pORow, 0);
            UWCritterClock lOClock = new UWCritterClock();

            fPlacePlayerForDeath(lOWorld, true);
            lOOne.Actor.TakeBlow(500, UWCritterBrain.PlayerIndex);

            // The first death picture at once, with no update of its own (go_into_dying_sequence writes
            // sequence 0x0C frame 0 and the renderer draws the record; since cleanup step 4.12b,
            // 2026-10-19, per user in the original).
            string lsDeathPicture = "motion " + UWCritterBrain.AnimDying + "/0";

            fExpectBool("frozen: the first death picture at once, right after the commit",
                lOOne.View.Log.Count >= 2 && lOOne.View.Log[lOOne.View.Log.Count - 2] == "destroyed"
                && lOOne.View.Log[lOOne.View.Log.Count - 1] == lsDeathPicture, true);

            fRunDeathFrames(lOOne, lOClock, 40, true);
            fExpectBool("frozen: forty frozen frames later still Dying at frame 0, no remains (B22)",
                lOOne.Actor.DeathState == UWCreatureDeathState.Dying && lOOne.Actor.Record.Frame == 0
                && !lOOne.View.Log.Contains("complete death"), true);
            fExpectBool("frozen: the first death picture stays - no other picture while frozen",
                lOOne.View.Log.FindAll(psLine => psLine.StartsWith("motion")).Count == 1
                && lOOne.View.Log[lOOne.View.Log.Count - 1] == lsDeathPicture, true);

            fRunDeathFrames(lOOne, lOClock, 12);
            fExpectBool("frozen: after the freeze it ends once",
                lOOne.Actor.DeathState == UWCreatureDeathState.Removed && fCountLines(lOOne.View.Log, "complete death") == 1, true);
        }

        /// <summary>The drowning as the brain raises it in one update (the splash, then frame 3): from
        /// Alive straight to Removed, the body committed on the way, no experience.</summary>
        private static void fCheckDeathDrowned(DataImport pOData, int piItemId, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOData.Levels[0]);
            ActorUnderTest lOOne = fDeathActor(lOWorld, piItemId, pORow, 0);
            ICritterHost lIHost = lOOne.Host;
            UWPlayerVitals lOVitals = lOWorld.WorldFake.Body.Numbers;
            int liExperience = lOVitals.Experience;

            lIHost.OnDrowned();
            fExpectBool("drowned: the splash leaves the machine Alive",
                lOOne.Actor.DeathState == UWCreatureDeathState.Alive && lOOne.View.Log.Contains("drowning"), true);

            lIHost.OnDeathFinished();
            fExpectBool("drowned: splash, commit, end - in that order, once",
                string.Join(",", lOOne.View.Log) == "drowning,destroyed,complete death"
                && lOOne.Actor.DeathState == UWCreatureDeathState.Removed && lOVitals.Experience == liExperience, true);
            fExpectInt("drowned: the hit points stay (crit_hndlr_walk, since cleanup step 4.12b)", lOOne.Actor.Record.HitPoints, 20);

            // A goal-3 creature that drowned: sequence 0x0C at frame 3 with its hit points (the brain's
            // drowning, as the original since 4.12b). It is no creature saved while dying - Alive, saved
            // with its hit points and the sequence - and it never finishes (goal 3 skips the machine).
            FakeCreatureWorld lOGoal3World = fActorWorld(pOData, pOData.Levels[0]);
            ActorUnderTest lOGoal3 = fDeathActor(lOGoal3World, piItemId, pORow, 0, 3, 20, UWCritterBrain.AnimDying,
                UWCritterRules.DeathRemovalFrame);
            UWCritterClock lOClock = new UWCritterClock();

            fPlacePlayerForDeath(lOGoal3World, true);
            UWWorldCapture.CaptureCritter(new UWTileQueries(null, null), lOGoal3.Actor.Record, lOGoal3.Actor.IsDead,
                lOGoal3.Health.InitialHealth, false);
            fExpectBool("drowned, goal 3: Alive, not loaded dying, saved with its hit points and the sequence",
                !lOGoal3.Actor.IsLoadedDying && lOGoal3.Actor.DeathState == UWCreatureDeathState.Alive
                && lOGoal3.Actor.Npc.HitPoints == 20 && lOGoal3.Actor.Record.Animation == UWCritterBrain.AnimDying, true);

            fRunDeathFrames(lOGoal3, lOClock, 40);
            fExpectBool("drowned, goal 3: it never finishes - no end, no remains",
                lOGoal3.Actor.DeathState == UWCreatureDeathState.Alive && !lOGoal3.View.Log.Contains("complete death"), true);
        }

        /// <summary>The golem refuses: the machine stays Alive at 0 hit points, nothing committed, and a
        /// save keeps the 0 (B23); his reward once.</summary>
        private static void fCheckDeathRefused(DataImport pOData, int piItemId, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOData.Levels[0]);
            ActorUnderTest lOGolem = fDeathActor(lOWorld, piItemId, pORow, UWSpecialDeaths.MetalGolemWhoami);
            UWPlayerVitals lOVitals = lOWorld.WorldFake.Body.Numbers;

            fPlacePlayerForDeath(lOWorld, true);

            int liExperience = lOVitals.Experience;

            lOGolem.Actor.TakeBlow(500, UWCritterBrain.PlayerIndex);

            int liGained = lOVitals.Experience - liExperience;

            fExpectBool("golem: the death refused - Alive with 0 hit points, the body not destroyed",
                lOGolem.Actor.DeathState == UWCreatureDeathState.Alive && !lOGolem.Actor.IsDead && !lOGolem.Health.IsDestroyed
                && lOGolem.Actor.Record.HitPoints == 0 && !lOGolem.View.Log.Contains("destroyed")
                && lOGolem.View.HasEvent("death refused"), true);
            fExpectBool("golem: his reward (" + liGained + ") and his talk", liGained > 0
                && lOWorld.WorldFake.Log.Exists(psLine => psLine.StartsWith("talk")), true);

            liExperience = lOVitals.Experience;
            lOGolem.Actor.TakeBlow(5, UWCritterBrain.PlayerIndex);
            fExpectBool("golem: the next blow is refused again, no second reward",
                lOGolem.Actor.DeathState == UWCreatureDeathState.Alive && lOVitals.Experience == liExperience, true);

            UWWorldCapture.CaptureCritter(new UWTileQueries(null, null), lOGolem.Actor.Record, lOGolem.Actor.IsDead,
                lOGolem.Health.InitialHealth, false);
            fExpectInt("golem: saved with his 0", lOGolem.Actor.Npc.HitPoints, 0);
        }

        /// <summary>B11: saved while dying, it starts Dying - the body committed once, no experience -
        /// a blow starts no second death, and it ends at frame 3 once.</summary>
        private static void fCheckDeathLoaded(DataImport pOData, int piItemId, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOData.Levels[0]);
            UWPlayerVitals lOVitals = lOWorld.WorldFake.Body.Numbers;
            int liExperience = lOVitals.Experience;
            ActorUnderTest lOLoaded = fDeathActor(lOWorld, piItemId, pORow, 0, 8, 0, UWCritterBrain.AnimDying, 2);
            UWCritterClock lOClock = new UWCritterClock();

            fExpectBool("loaded dying: starts Dying, committed once, no experience",
                lOLoaded.Actor.IsLoadedDying && lOLoaded.Actor.DeathState == UWCreatureDeathState.Dying
                && fCountLines(lOLoaded.View.Log, "destroyed") == 1 && lOVitals.Experience == liExperience, true);

            fPlacePlayerForDeath(lOWorld, true);
            lOLoaded.Actor.TakeBlow(5, UWCritterBrain.PlayerIndex);
            fExpectBool("loaded dying: a blow starts no second death",
                !lOLoaded.View.HasEvent("dying") && lOVitals.Experience == liExperience && lOLoaded.Actor.Record.Frame == 2, true);

            UWWorldCapture.CaptureCritter(new UWTileQueries(null, null), lOLoaded.Actor.Record, lOLoaded.Actor.IsDead,
                lOLoaded.Health.InitialHealth, false);
            fExpectBool("loaded dying: saved again with 0 hit points and the sequence",
                lOLoaded.Actor.Npc.HitPoints == 0 && lOLoaded.Actor.Record.Animation == UWCritterBrain.AnimDying, true);

            fRunDeathFrames(lOLoaded, lOClock, 12);
            fExpectBool("loaded dying: it ends from the saved frame, once",
                lOLoaded.Actor.DeathState == UWCreatureDeathState.Removed && fCountLines(lOLoaded.View.Log, "complete death") == 1
                && fCountLines(lOLoaded.View.Log, "destroyed") == 1, true);
        }

        /// <summary>Goal 11 skips the animation machine: dying, it never reaches frame 3, so nothing
        /// removes it (per user in the original, 2026-10-17).</summary>
        private static void fCheckDeathNeverEnding(DataImport pOData, int piItemId, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOData.Levels[0]);
            ActorUnderTest lOOne = fDeathActor(lOWorld, piItemId, pORow, 0, 11);
            UWCritterClock lOClock = new UWCritterClock();

            fPlacePlayerForDeath(lOWorld, true);
            lOOne.Actor.TakeBlow(500, 7);
            fRunDeathFrames(lOOne, lOClock, 40);
            fExpectBool("goal 11: dying for good - no end, no remains",
                lOOne.Actor.DeathState == UWCreatureDeathState.Dying && !lOOne.View.Log.Contains("complete death"), true);
        }

        /// <summary>Cleanup step 4.12b (2026-10-19, as the original): generate_inventory puts each rolled
        /// piece at the head of the creature's chain (Obj_Add), so the one spill of the death
        /// (drop_some_objects) goes through the loot last rolled first, then what it carried.</summary>
        private static void fCheckDeathDropChain()
        {
            UWObject lOCarriedA = new UWObject(0x01);
            UWObject lOCarriedB = new UWObject(0x02);
            UWObject lOCoin = new UWObject(0xA0);
            UWObject lOMeat = new UWObject(0xB0);
            UWObject lOClub = new UWObject(0x04);
            List<UWObject> lOChain = new List<UWObject> { lOCarriedA, lOCarriedB };

            UWCritterLoot.AddToChain(lOChain, new List<UWObject> { lOCoin, lOMeat, null, lOClub });
            fExpectBool("drop chain: the loot at the head, the last rolled first, then the carried items",
                lOChain.Count == 5 && lOChain[0] == lOClub && lOChain[1] == lOMeat && lOChain[2] == lOCoin
                && lOChain[3] == lOCarriedA && lOChain[4] == lOCarriedB, true);
        }
    }
}
