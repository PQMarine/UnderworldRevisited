using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.11 (2026-10-18): the creature's game side as the engine-free UWCreatureActor,
    /// run headless on level 1's own goblins and doors with a fake creature world (on FakeWorld) and
    /// a logging fake view. Pinned: the setup (index, path bit, the spawn spot, a creature saved
    /// while dying), the snapshots of the player, another creature and an object, the doors (use,
    /// lockpick, bash), the blow on nothing, the world, an object, the player and another creature -
    /// with the creature's blow drawing exactly what UWAttackResolution.CreatureBlow draws, seed by
    /// seed - the same side spared without a draw, the damage intake (DamageNPC), the spells on the
    /// creature with their resistance, the music rule, the teleport, the talk, the missile and the
    /// death's events. Needs the installation (the creature table, COMOBJ, level 1).
    /// </summary>
    public static partial class Program
    {
        /// <summary>A view without a scene: every call a line of the log, the events apart.</summary>
        private sealed class FakeCreatureView : ICreatureView
        {
            public readonly List<string> Log = new List<string>();

            public readonly List<string> Events = new List<string>();

            /// <summary>The body's hit points, marked destroyed as UWDamageable.MarkDestroyed does.</summary>
            public UWHealth Health;

            /// <summary>Set once the actor exists: its index and record.</summary>
            public UWCreatureActor Actor;

            public UWDamageTarget DamageTarget => UWDamageTarget.Creature(Actor != null ? Actor.Index : 0);

            public UWCreatureRef Ref => new UWCreatureRef(Actor != null ? Actor.Npc : null, Actor != null ? Actor.Record : null,
                Actor != null ? new UWTilePos(Actor.Record.TileX, Actor.Record.TileY) : UWSpot.Nowhere.Tile, Health != null && Health.IsDestroyed,
                Actor != null ? Actor.Index : 0);

            public void MarkDestroyed()
            {
                if (Health != null)
                    Health.MarkDestroyed();

                Log.Add("destroyed");
            }

            public void PlaceBody(int piInterval) { Log.Add("place " + piInterval); }

            public void SetBodyNow() { Log.Add("body now"); }

            public void ShowMotion(UWCritterBrain.Motion pOMotion) { Log.Add("motion " + pOMotion.Animation + "/" + pOMotion.Frame); }

            public void PlaySound(int piSound) { Log.Add("sound " + piSound); }

            public void PlayMissSound() { Log.Add("miss"); }

            public void PlayStruckSound(in UWAttackOutcome pOOutcome) { Log.Add(pOOutcome.Hit ? "struck" : "blocked"); }

            public void ShowHitEffect(int piPart, int piDamage, int piMissileZ) { Log.Add("hit effect " + piPart); }

            public void ShowDrowning() { Log.Add("drowning"); }

            public void CompleteDeath() { Log.Add("complete death"); }

            public void Trace(string psFormat, params object[] pOArguments)
            {
            }

            public void Event(string psFormat, params object[] pOArguments)
            {
                Events.Add(pOArguments == null || pOArguments.Length == 0 ? psFormat : string.Format(psFormat, pOArguments));
            }

            public bool HasEvent(string psStart)
            {
                return Events.Exists(psEvent => psEvent.StartsWith(psStart));
            }
        }

        /// <summary>The player as a blow's defender: no protection, no armour, a fixed defence.</summary>
        private sealed class FakePlayerDefender : IUWAttackDefender
        {
            public UWDefenderKind DefenderKind => UWDefenderKind.Player;

            public int Defence => 5;

            public int ProtectionAt(int piPart) => 0;

            public int ArmourAt(int piPart) => 0;

            public bool IsArmourMetalAt(int piPart) => false;

            public void OnCriticalHit(int piPart)
            {
            }
        }

        /// <summary>
        /// A creature world without a scene, on FakeWorld: no motion engines (a step is empty, a path
        /// search finds nothing), the actors by their level objects, the player as the check sets
        /// him, the melee contact the check hands in, and every picture, launch, talk and music call
        /// a line of the log.
        /// </summary>
        private sealed class FakeCreatureWorld : ICreatureWorld
        {
            public readonly FakeWorld WorldFake;

            public readonly List<string> Log = new List<string>();

            public readonly Dictionary<UWObject, UWCreatureActor> Actors = new Dictionary<UWObject, UWCreatureActor>();

            public FakeCreatureWorld(FakeWorld pOWorld)
            {
                WorldFake = pOWorld;
            }

            public IUWWorld World => WorldFake;

            public UWCreatureMotion Motion => null;

            public UWTileRoute Route => null;

            public UWTileTraverse Traverse => null;

            public bool TryGetStaticObject(int piIndex, out UWObject pOObject)
            {
                pOObject = null;

                return false;
            }

            public void MoveObjectData(UWNpc pONpc, int piTileX, int piTileY) { Log.Add("relink " + piTileX + "/" + piTileY); }

            public UWCreatureActor ActorOf(UWObject pOObject)
            {
                return pOObject != null && Actors.TryGetValue(pOObject, out UWCreatureActor lOActor) ? lOActor : null;
            }

            public bool PlayerThere = true;

            public CritterTarget Player;

            public bool HasPlayer => PlayerThere;

            public CritterTarget SnapshotPlayer() => PlayerThere ? Player : new CritterTarget();

            public UWTilePos ViewTile => new UWTilePos(-100, -100);

            public bool TryGetPlayerAsDefender(out IUWAttackDefender pIDefender, out int piFeetZ, out int piBodyHeightZ)
            {
                Log.Add("player status");
                pIDefender = new FakePlayerDefender();
                piFeetZ = Player.ZPos;
                piBodyHeightZ = 16;

                return PlayerThere;
            }

            public UWMeleeContact Contact = UWMeleeContact.Nothing;

            public UWMeleeContact FindMeleeDefender(UWCreatureActor pOAttacker) => Contact;

            public UWMeleeContact DoorContact(UWObject pODoorData)
            {
                return UWMeleeContact.OfObject(UWDamageTarget.ObjectOf(pODoorData), true, "door", null);
            }

            public void ShowBlowOnPlayer(in UWAttackOutcome pOOutcome) { Log.Add(pOOutcome.Hit ? "player struck" : "player blocked"); }

            public void ShowBlowOnObject(UWCreatureActor pOAttacker, in UWMeleeContact pOContact, int piVolume)
            {
                Log.Add("object blow " + piVolume);
            }

            public void ShowBlowOnWorld(UWCreatureActor pOAttacker, in UWMeleeContact pOContact) { Log.Add("world blow"); }

            public void ShowDoorBashed(in UWMeleeContact pOContact, int piVolume) { Log.Add("door bashed " + piVolume); }

            public bool LaunchRoom = true;

            public bool LaunchMissile(UWCreatureActor pOLauncher, int piObjectId, int piPitch, int piBaseDamage, int piDamageType,
                int piImpactId)
            {
                Log.Add(string.Format("launch {0:X} base {1} type {2}", piObjectId, piBaseDamage, piDamageType));

                return LaunchRoom;
            }

            public bool Talks = true;

            public bool CanTalk => Talks;

            public void Talk(UWNpc pONpc) { Log.Add("talk"); }

            public int Theme;

            public int MusicTheme => Theme;

            public void MarkCombatMusic() { Log.Add("mark combat"); }
        }

        /// <summary>One actor of the check: its view and its body's hit points.</summary>
        private sealed class ActorUnderTest
        {
            public UWCreatureActor Actor;

            public FakeCreatureView View;

            public UWHealth Health;

            public ICritterHost Host => Actor;
        }

        private static ActorUnderTest fMakeActor(FakeCreatureWorld pOWorld, UWNpc pONpc, UWObjectClassProperties.Critter pORow,
            int piResistances = 0)
        {
            UWHealth lOHealth = new UWHealth();

            lOHealth.Initialise(System.Math.Max(1, (int)pORow.Vitality), 0, 0, pORow.DefensePower, piResistances,
                System.Math.Max(1, (int)pONpc.HitPoints));

            FakeCreatureView lOView = new FakeCreatureView { Health = lOHealth };
            UWCreatureActor lOActor = new UWCreatureActor(pONpc, pORow, true, pOWorld, lOView, lOHealth);

            lOView.Actor = lOActor;
            pOWorld.Actors[pONpc] = lOActor;

            return new ActorUnderTest { Actor = lOActor, View = lOView, Health = lOHealth };
        }

        private static void fCheckCreatureActor(DataImport pOData)
        {
            Check.Group("Creature actor (cleanup step 4.11)");

            if (pOData == null || pOData.Levels == null || pOData.Levels.Count == 0 || pOData.ObjectClassProperties == null
                || pOData.CommonObjectProperties == null || pOData.ObjectProperties == null)
            {
                fFail("creature actor", "no data");

                return;
            }

            UWLevel lOLevel = pOData.Levels[0];
            List<UWNpc> lOWalkers = new List<UWNpc>();
            List<UWObjectClassProperties.Critter> lORows = new List<UWObjectClassProperties.Critter>();
            int liDoorIndex = 0;

            for (int liIndex = 2; liIndex < lOLevel.Masterlist.Count; liIndex++)
            {
                UWObject lOObject = lOLevel.Masterlist[liIndex];

                if (lOObject is UWNpc lONpc && lOWalkers.Count < 2 && UWObjectIds.IsCreature(lONpc.ID) && lONpc.HitPoints > 0
                    && lONpc.TileX > 0 && pOData.ObjectClassProperties.TryGetCritter(lONpc.ID, out UWObjectClassProperties.Critter lORow)
                    && !lORow.IsFlier && !lORow.IsSwimmer && lORow.Attacks != null)
                {
                    lOWalkers.Add(lONpc);
                    lORows.Add(lORow);
                }

                if (liDoorIndex == 0 && lOObject != null && lOObject.ID >= 0x140 && lOObject.ID <= 0x145 && lOObject.TileX > 0)
                    liDoorIndex = liIndex;
            }

            if (lOWalkers.Count < 2 || liDoorIndex == 0)
            {
                fFail("creature actor", "level 1 has no two walking creatures and a closed door");

                return;
            }

            fCheckActorSetup(pOData, lOLevel, lOWalkers[0], lORows[0]);
            fCheckActorQueries(pOData, lOLevel, lOWalkers, lORows, liDoorIndex);
            fCheckActorDoors(pOData, lOLevel, lOWalkers[0], lORows[0], liDoorIndex);
            fCheckActorBlows(pOData, lOLevel, lOWalkers, lORows);
            fCheckActorReactions(pOData, lOLevel, lOWalkers[0], lORows[0]);
            fCheckActorEvents(pOData, lOLevel, lOWalkers[0], lORows[0]);
        }

        private static FakeCreatureWorld fActorWorld(DataImport pOData, UWLevel pOLevel)
        {
            FakeWorld lOWorld = new FakeWorld { Data = pOData, Level = pOLevel, LevelIndex = 0 };

            lOWorld.Body.Numbers = new UWPlayerVitals(null);

            return new FakeCreatureWorld(lOWorld);
        }

        /// <summary>The setup: the index in the master list, no stored path, the spawn spot, the
        /// record as the body's store, and a creature saved while dying.</summary>
        private static void fCheckActorSetup(DataImport pOData, UWLevel pOLevel, UWNpc pONpc, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOLevel);

            UWCritterRecord lOProbe = new UWCritterRecord(pONpc);

            lOProbe.HasPath = true;

            ActorUnderTest lOOne = fMakeActor(lOWorld, pONpc, pORow);
            UWCritterRecord lORecord = lOOne.Actor.Record;

            fExpectInt("setup: the index is the master list's", lOOne.Actor.Index, pOLevel.Masterlist.IndexOf(pONpc));
            fExpectBool("setup: no stored path (clear_paths)", lORecord.HasPath, false);
            fExpectBool("setup: it has not moved", lOOne.Actor.HasMoved, false);
            fExpectBool("setup: the body reads the record", lOOne.Health.HasStore && lOOne.Health.CurrentHealth == lORecord.HitPoints, true);
            fExpectBool("setup: a living one is not loaded dying", lOOne.Actor.IsLoadedDying, false);

            int liFineX = lORecord.FineX;

            lORecord.FineX = (liFineX + 1) & 7;
            fExpectBool("setup: an eighth further it has moved", lOOne.Actor.HasMoved, true);
            lORecord.FineX = liFineX;

            UWNpc lODying = new UWNpc(pONpc.ID) { RawNpcBytes = new byte[19] };
            UWCritterRecord lODyingRecord = new UWCritterRecord(lODying);

            lODyingRecord.HitPoints = 0;
            lODyingRecord.Animation = UWCritterBrain.AnimDying;

            UWCreatureActor lOLoaded = new UWCreatureActor(lODying, pORow, true, lOWorld, new FakeCreatureView(), null);

            fExpectBool("setup: saved while dying loads dying (B11)", lOLoaded.IsLoadedDying, true);
            fExpectInt("setup: an object not in the list has index 0", lOLoaded.Index, 0);

            UWCreatureActor lOScratch = new UWCreatureActor(null, pORow, true, lOWorld, new FakeCreatureView(), null);

            fExpectBool("setup: without data a scratch record, index 0",
                lOScratch.Record != null && lOScratch.Index == 0 && lOScratch.Npc.ID == UWObjectClassProperties.CritterFirstId, true);
        }

        /// <summary>The questions the brain asks: the level, the player, another creature, an
        /// object.</summary>
        private static void fCheckActorQueries(DataImport pOData, UWLevel pOLevel, List<UWNpc> pOWalkers,
            List<UWObjectClassProperties.Critter> pORows, int piDoorIndex)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOLevel);
            ActorUnderTest lOOne = fMakeActor(lOWorld, pOWalkers[0], pORows[0]);
            ActorUnderTest lOOther = fMakeActor(lOWorld, pOWalkers[1], pORows[1]);
            ICritterHost lIHost = lOOne.Host;
            UWCritterRecord lORecord = lOOne.Actor.Record;

            UWTile lOTile = pOLevel.GetTile(lORecord.TileX, lORecord.TileY);

            fExpectInt("queries: the floor level of its tile", lIHost.FloorLevelAt(lORecord.TileX, lORecord.TileY), lOTile.FloorHeight >> 4);
            fExpectInt("queries: off the map, 0", lIHost.FloorLevelAt(-1, 70), 0);
            fExpectInt("queries: the dungeon level, 1-based", lIHost.DungeonLevel, 1);
            fExpectBool("queries: no orb rule on level 1", lIHost.TybalOrbStands, false);

            pOData.CommonObjectProperties.TryGet(pOWalkers[0].ID, out UWCommonObjectProperties.Entry lOOwn);
            fExpectInt("queries: its own height is COMOBJ's (the brain's, step 4.13)", lOOne.Actor.Brain.OwnObjectHeight, lOOwn.Height);

            lOWorld.Player = new CritterTarget { Exists = true, Index = 1, IsPlayer = true, X = 99, Y = 77 };
            CritterTarget lOPlayer = lIHost.GetObject(UWCritterBrain.PlayerIndex);

            fExpectBool("queries: the player is the world's snapshot", lOPlayer.Exists && lOPlayer.X == 99 && lOPlayer.Y == 77, true);

            lOWorld.PlayerThere = false;
            fExpectBool("queries: no player, none", lIHost.GetObject(UWCritterBrain.PlayerIndex).Exists, false);

            CritterTarget lOCreature = lIHost.GetObject(lOOther.Actor.Index);
            UWCritterRecord lOOtherRecord = lOOther.Actor.Record;

            pOData.CommonObjectProperties.TryGet(pOWalkers[1].ID, out UWCommonObjectProperties.Entry lOOtherSize);
            fExpectBool("queries: another creature from its actor's record",
                lOCreature.Exists && lOCreature.IsCreature && lOCreature.X == lOOtherRecord.X && lOCreature.Y == lOOtherRecord.Y
                && lOCreature.HitPoints == lOOtherRecord.HitPoints && lOCreature.ObjectHeight == lOOtherSize.Height
                && lOCreature.Kind == pORows[1].GeneralType, true);

            ((ICritterHost)lOOther.Actor).OnDeathStarted(false);
            fExpectInt("queries: a dead one has 0 hit points", lIHost.GetObject(lOOther.Actor.Index).HitPoints, 0);

            UWObject lODoor = pOLevel.Masterlist[piDoorIndex];
            CritterTarget lODoorTarget = lIHost.GetObject(piDoorIndex);

            fExpectBool("queries: an object from its data",
                lODoorTarget.Exists && !lODoorTarget.IsCreature && lODoorTarget.ItemId == (lODoor.ID & 0x1FF)
                && lODoorTarget.X == (lODoor.TileX << 3) + (lODoor.XPos & 7) && lODoorTarget.HitPoints == 0, true);
            fExpectBool("queries: index 0 and past the list are nothing",
                !lIHost.GetObject(0).Exists && !lIHost.GetObject(pOLevel.Masterlist.Count).Exists, true);

            fExpectBool("queries: no motion core, an empty step and no picture moved",
                !lIHost.RunMotion().Collided && !lOOne.View.Log.Exists(psLine => psLine.StartsWith("place")), true);
            fExpectBool("queries: no route, no straight line and no path",
                !lIHost.IsStraightLineClear(lORecord.TileX + 1, lORecord.TileY)
                && !lIHost.TryFindPath(lORecord.X, lORecord.Y, 0, lORecord.TileX + 1, lORecord.TileY, 0, 0, new List<UWTilePos>(),
                    new List<bool>()), true);
        }

        /// <summary>The doors through the world's doors (UWDoorRules.Use with the creature as the
        /// user, the lockpick only unlocking, the bash charged to the creature).</summary>
        private static void fCheckActorDoors(DataImport pOData, UWLevel pOLevel, UWNpc pONpc, UWObjectClassProperties.Critter pORow,
            int piDoorIndex)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOLevel);
            ActorUnderTest lOOne = fMakeActor(lOWorld, pONpc, pORow);
            ICritterHost lIHost = lOOne.Host;
            UWObject lODoorData = pOLevel.Masterlist[piDoorIndex];
            FakeDoor lODoor = new FakeDoor(lOWorld.WorldFake.Log, lODoorData, true);
            int liOwner = lODoorData.Owner;

            fExpectBool("doors: a door the world does not know stays shut", lIHost.UseDoor(piDoorIndex), false);

            lOWorld.WorldFake.DoorList.Add(lODoor);
            lODoorData.Owner = (ushort)(liOwner & ~1);
            lODoor.LockState.Lock();

            fExpectBool("doors: a locked door refuses", lIHost.UseDoor(piDoorIndex) || !lODoor.Closed, false);

            lODoor.LockState.Unlock();
            lODoorData.Owner = (ushort)(liOwner | 1);
            fExpectBool("doors: a spiked door refuses a creature (UseRect)", lIHost.UseDoor(piDoorIndex) || !lODoor.Closed, false);

            lODoorData.Owner = (ushort)(liOwner & ~1);
            fExpectBool("doors: an unlocked door opens", lIHost.UseDoor(piDoorIndex) && !lODoor.Closed
                && lOWorld.WorldFake.Log.Contains("door " + lODoorData.TileX + "/" + lODoorData.TileY + " moved by Creature"), true);
            fExpectBool("doors: an open door is open", lIHost.UseDoor(piDoorIndex), true);

            lODoor.Closed = true;
            lODoor.LockState.Lock();
            UWRandom.UseSeed(7);
            lIHost.PickDoor(piDoorIndex, 0);
            fExpectBool("doors: a lockpick only unlocks - the door stays shut", lODoor.Closed, true);

            lIHost.BashDoor(piDoorIndex, 5);
            fExpectBool("doors: the bash is shown with the damage times four",
                lOWorld.Log.Contains("door bashed 20"), true);
            fExpectBool("doors: ... and lands on the door charged to the creature",
                lOWorld.WorldFake.Blows.Exists(psBlow => psBlow.StartsWith("object " + lODoorData.ID + " takes")), true);

            lODoorData.Owner = (ushort)liOwner;
        }

        /// <summary>The blow by what it met, and the creature's blow drawing what
        /// UWAttackResolution.CreatureBlow draws (the 4.9b order), seed by seed.</summary>
        private static void fCheckActorBlows(DataImport pOData, UWLevel pOLevel, List<UWNpc> pOWalkers,
            List<UWObjectClassProperties.Critter> pORows)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOLevel);
            ActorUnderTest lOOne = fMakeActor(lOWorld, pOWalkers[0], pORows[0]);
            ActorUnderTest lOOther = fMakeActor(lOWorld, pOWalkers[1], pORows[1]);
            ICritterHost lIHost = lOOne.Host;

            lOOne.Actor.Record.IsAlly = false;
            lOOther.Actor.Record.IsAlly = false;

            lOWorld.Contact = UWMeleeContact.Nothing;
            lIHost.OnBlow(1, 0, 128, 0, 0);
            fExpectBool("blow: nothing in reach is a miss at the attacker",
                lOOne.View.HasEvent("blow 0: nothing in reach") && lOOne.View.Log.Contains("miss")
                && lOWorld.WorldFake.Blows.Count == 0, true);

            lOOne.View.Log.Clear();
            lOWorld.Contact = UWMeleeContact.OfWorld("a wall", null);
            lIHost.OnBlow(1, 1, 128, 0, 0);
            fExpectBool("blow: the world gets the impact, then the miss",
                lOWorld.Log.IndexOf("world blow") >= 0 && lOOne.View.Log.Contains("miss") && lOWorld.WorldFake.Blows.Count == 0, true);
            fExpectInt("blow: the attack index follows the blow", lOOne.Actor.AttackIndex, 1);

            UWObject lOBarrel = new UWObject(0x15F);

            lOWorld.Contact = UWMeleeContact.OfObject(UWDamageTarget.ObjectOf(lOBarrel), true, "barrel", null);
            UWRandom.UseSeed(11);
            lIHost.OnBlow(1, 0, 128, 0, 2);
            UWRandom.UseSeed(11);
            UWAttackOutcome lOObjectOutcome = UWAttackResolution.Resolve(
                UWAttackerStats.Creature(pORows[0], true, 0, lOOne.Actor.Record.IsStrong, lOOne.Actor.Index, 128, 2),
                UWAttackResolution.ObjectDefender, UWAttackResolution.NoPart);
            fExpectBool("blow: an object takes the rolled damage without a check",
                lOWorld.Log.Contains("object blow " + lOObjectOutcome.HitVolume)
                && lOWorld.WorldFake.Blows.Contains("object " + lOBarrel.ID + " takes " + lOObjectOutcome.Blow.Amount + " type "
                    + lOObjectOutcome.Blow.DamageType), true);

            lOWorld.WorldFake.Blows.Clear();
            lOWorld.Contact = UWMeleeContact.OfObject(UWDamageTarget.ObjectOf(lOBarrel), false, "broken barrel", null);
            lIHost.OnBlow(1, 0, 128, 0, 0);
            fExpectInt("blow: an object that takes no blows takes none", lOWorld.WorldFake.Blows.Count, 0);

            // The player: the status pass, the pictures, the request.
            lOWorld.Player = new CritterTarget { Exists = true, Index = 1, IsPlayer = true, ZPos = lOOne.Actor.Record.ZPos };
            lOWorld.Contact = UWMeleeContact.Player();
            lOWorld.Log.Clear();
            UWRandom.UseSeed(3);
            lIHost.OnBlow(1, 0, 128, 0, 0);
            fExpectBool("blow: the player's status first, then the pictures and one request",
                lOWorld.Log.Count == 2 && lOWorld.Log[0] == "player status"
                && (lOWorld.Log[1] == "player struck" || lOWorld.Log[1] == "player blocked")
                && lOWorld.WorldFake.PlayerRequests.Count == 1, true);

            // Another creature of the other side (an ally strikes a hostile one - two of one side spare
            // each other, do_attack), seed by seed against UWAttackResolution.CreatureBlow.
            lOOne.Actor.Record.IsAlly = true;
            lOOther.Actor.Record.IsAlly = false;
            lOOne.Actor.GetOwnSize(out int liOwnRadius, out int liOwnHeight);
            lOOther.Actor.GetOwnSize(out int liOtherRadius, out int liOtherHeight);
            lOWorld.Contact = UWMeleeContact.OfCreature(lOOther.Actor);

            int liSame = 0;
            int liHits = 0;
            int liMisses = 0;
            int liSeeds = 200;

            for (int liSeed = 1; liSeed <= liSeeds; liSeed++)
            {
                lOWorld.WorldFake.Blows.Clear();
                lOOther.View.Log.Clear();

                UWRandom.UseSeed(liSeed);
                lIHost.OnBlow(lOOther.Actor.Index, liSeed % 3, 160, 0, liSeed % 5);
                int liAfter = UWRandom.Next(1000);

                UWRandom.UseSeed(liSeed);
                UWAttackOutcome lOExpected = UWAttackResolution.CreatureBlow(pORows[0], true, liSeed % 3, lOOne.Actor.Record.IsStrong,
                    lOOne.Actor.Index, 160, liSeed % 5, lOOne.Actor.Record.ZPos, liOwnHeight, lOOther.Actor,
                    lOOther.Actor.Record.ZPos, lOOther.Actor.Record.ZPos + liOtherHeight, out UWAttackerStats lOStats);
                int liExpectedAfter = UWRandom.Next(1000);

                string lsLine = "Creature " + lOOther.Actor.Index + " takes " + lOExpected.Blow.Amount + " type "
                    + lOExpected.Blow.DamageType + " by " + lOOne.Actor.Index;
                bool lbEffect = lOOther.View.Log.Exists(psLine => psLine.StartsWith("hit effect"));

                if (lOExpected.Hit)
                    liHits++;
                else
                    liMisses++;

                if (lOWorld.WorldFake.Blows.Count == 1 && lOWorld.WorldFake.Blows[0] == lsLine && liAfter == liExpectedAfter
                    && lOOther.View.Log[0] == (lOExpected.Hit ? "struck" : "blocked") && lbEffect == lOExpected.LeavesTrace)
                    liSame++;
            }

            fExpectInt("blow on a creature: the draws, the blow and its pictures of CreatureBlow, " + liSeeds + " seeds",
                liSame, liSeeds);
            fExpectBool("blow on a creature: the seeds give hits and misses", liHits > 0 && liMisses > 0, true);

            // The same side: refused before any draw - two allies, and two that are no allies.
            lOOther.Actor.Record.IsAlly = true;
            lOWorld.WorldFake.Blows.Clear();
            CountingRandomSource lOCounter = new CountingRandomSource();

            UWRandom.Use(lOCounter);
            lIHost.OnBlow(lOOther.Actor.Index, 0, 160, 0, 0);
            UWRandom.Use(null);
            fExpectBool("blow on a creature: two allies spare each other without a draw",
                lOCounter.Draws == 0 && lOWorld.WorldFake.Blows.Count == 0 && lOOne.View.HasEvent("blow 0 refused"), true);

            lOOne.Actor.Record.IsAlly = false;
            lOOther.Actor.Record.IsAlly = false;
            UWRandom.Use(lOCounter);
            lIHost.OnBlow(lOOther.Actor.Index, 0, 160, 0, 0);
            UWRandom.Use(null);
            fExpectBool("blow on a creature: two hostile creatures spare each other without a draw",
                lOCounter.Draws == 0 && lOWorld.WorldFake.Blows.Count == 0, true);

            // A DYING one is struck as the original strikes it (do_attack has no dying test; since
            // cleanup step 4.12b, 2026-10-19 - it took nothing until then): the draws, the sound and
            // the damage call, no second death. A removed one takes nothing.
            lOOne.Actor.Record.IsAlly = true;
            ((ICritterHost)lOOther.Actor).OnDeathStarted(false);
            lOOther.View.Log.Clear();
            UWRandom.Use(lOCounter);
            lIHost.OnBlow(lOOther.Actor.Index, 0, 160, 0, 0);
            UWRandom.Use(null);
            fExpectBool("blow on a creature: a dying one is struck, no second death",
                lOCounter.Draws > 0 && lOWorld.WorldFake.Blows.Count == 1
                && (lOOther.View.Log.Contains("struck") || lOOther.View.Log.Contains("blocked"))
                && lOOther.Actor.DeathState == UWCreatureDeathState.Dying
                && lOOther.View.Events.FindAll(psEvent => psEvent.StartsWith("dying")).Count == 1, true);

            ((ICritterHost)lOOther.Actor).OnDeathFinished();
            lOWorld.WorldFake.Blows.Clear();
            lOCounter = new CountingRandomSource();
            UWRandom.Use(lOCounter);
            lIHost.OnBlow(lOOther.Actor.Index, 0, 160, 0, 0);
            UWRandom.Use(null);
            fExpectBool("blow on a creature: a removed one takes nothing", lOCounter.Draws == 0 && lOWorld.WorldFake.Blows.Count == 0, true);
        }

        /// <summary>The damage it takes and the spells on it.</summary>
        private static void fCheckActorReactions(DataImport pOData, UWLevel pOLevel, UWNpc pONpc, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOLevel);
            ActorUnderTest lOOne = fMakeActor(lOWorld, pONpc, pORow);
            UWCritterRecord lORecord = lOOne.Actor.Record;

            lORecord.HitPoints = 20;
            lORecord.Attitude = 2;
            lOOne.Actor.TakeBlow(5, UWCritterBrain.PlayerIndex);
            fExpectBool("intake: DamageNPC takes it off the record and keeps the attacker",
                lORecord.HitPoints == 15 && lORecord.Attacker == UWCritterBrain.PlayerIndex, true);

            int liTally = lORecord.DamageTaken;

            // The whole damage into the tally, the byte wrapping (damage_critter's SET_DAMAGE,
            // 2026-10-19, the user's decision "as the original"; at most the hit points until then).
            lOOne.Actor.TakeBlow(500, 7);
            fExpectBool("intake: the whole damage into the tally (a byte), then the death",
                lORecord.HitPoints == 0 && lORecord.DamageTaken == ((liTally + 500) & 0xFF) && lOOne.View.Log.Contains("destroyed")
                && lOOne.Actor.IsDead, true);
            fExpectBool("intake: a dead one is not angered", lOOne.Actor.Anger() == -1, true);

            // A fresh actor over the record the death above left (0 hit points, sequence 0x0C) would be
            // one saved while dying, and starts Dying since the death machine (cleanup step 4.12,
            // 2026-10-19): the record is set living first.
            UWCritterRecord lOLiving = new UWCritterRecord(pONpc);

            lOLiving.HitPoints = 20;
            lOLiving.Animation = 0;

            ActorUnderTest lOFresh = fMakeActor(lOWorld, pONpc, pORow);

            lORecord = lOFresh.Actor.Record;
            lORecord.HitPoints = 20;
            lORecord.Attitude = 2;
            lORecord.Animation = 0;

            fExpectInt("anger: one step of goodwill less", lOFresh.Actor.Anger(), 1);
            fExpectBool("confusion: goal 2, attitude 1, the player as target",
                lOFresh.Actor.Confuse() == UWTargetSpellRules.Result.Shown && lORecord.Goal == 2 && lORecord.Attitude == 1
                && lORecord.GTarg == UWCritterBrain.PlayerIndex, true);
            fExpectBool("paralyse: goal 7",
                lOFresh.Actor.Paralyse() == UWTargetSpellRules.Result.Shown && lORecord.Goal == 7, true);

            lORecord.IsAlly = false;
            lORecord.GTarg = 1;
            fExpectBool("ally: wandering, bound to nobody, the ally bit, attitude 3",
                lOFresh.Actor.Befriend() == UWTargetSpellRules.Result.Shown && lORecord.Goal == 2 && lORecord.GTarg == 0
                && lORecord.IsAlly && lORecord.Attitude == 3, true);

            fExpectBool("fear: goal 6 against the player", lOFresh.Actor.Flee() == UWTargetSpellRules.Result.Shown
                && lORecord.Goal == 6 && lORecord.GTarg == UWCritterBrain.PlayerIndex, true);
            lOWorld.PlayerThere = false;
            fExpectBool("fear: no player, missed", lOFresh.Actor.Flee() == UWTargetSpellRules.Result.Missed, true);

            ActorUnderTest lOResisting = fMakeActor(lOWorld, pONpc, pORow, 3);

            fExpectBool("resistance 3: every spell spent, nothing written",
                lOResisting.Actor.Confuse() == UWTargetSpellRules.Result.Spent
                && lOResisting.Actor.Paralyse() == UWTargetSpellRules.Result.Spent && lORecord.Goal == 6, true);

            int liFull = System.Math.Max(2, (int)pORow.Vitality);

            lORecord.HitPoints = liFull;
            lOFresh.Actor.WeakenByBrokenOrb();
            fExpectBool("orb: the hit points halved plus one, no healing at the exit",
                lORecord.HitPoints == UWTybalOrbRules.WeakenedHitPoints(liFull) && lORecord.NoLevelExitHealing, true);
            lORecord.NoLevelExitHealing = false;
        }

        /// <summary>The events out of the brain that the actor turns into world and view calls.</summary>
        private static void fCheckActorEvents(DataImport pOData, UWLevel pOLevel, UWNpc pONpc, UWObjectClassProperties.Critter pORow)
        {
            FakeCreatureWorld lOWorld = fActorWorld(pOData, pOLevel);
            ActorUnderTest lOOne = fMakeActor(lOWorld, pONpc, pORow);
            ICritterHost lIHost = lOOne.Host;
            UWCritterRecord lORecord = lOOne.Actor.Record;

            // The music: theme 6 at a swing only while no combat theme plays; the timer always.
            lOWorld.Theme = UWMusicSelector.FirstCombatTheme;
            lIHost.PlayMusic(UWCritterRules.CombatTheme);
            fExpectBool("music: a combat theme playing keeps on, the timer starts anew",
                !lOWorld.WorldFake.Shown.Log.Contains("music " + UWCritterRules.CombatTheme) && lOWorld.Log.Contains("mark combat"), true);

            lOWorld.Theme = 1;
            lIHost.PlayMusic(UWCritterRules.CombatTheme);
            fExpectBool("music: else the combat theme", lOWorld.WorldFake.Shown.Log.Contains("music " + UWCritterRules.CombatTheme), true);

            // The teleport: refused on a solid tile, else to the tile's centre on its floor.
            UWTilePos lOSolid = new UWTilePos(-1, -1);

            for (int liAt = 0; liAt < 64 * 64 && lOSolid.X < 0; liAt++)
                if (pOLevel.TileData[liAt].TileType == UWTile.TileTypeEnum.solid)
                    lOSolid = new UWTilePos(liAt % 64, liAt / 64);

            int liTileX = lORecord.TileX;
            int liTileY = lORecord.TileY;

            fExpectBool("teleport: a solid tile refuses", lIHost.Teleport(lOSolid.X, lOSolid.Y), false);
            fExpectBool("teleport: its own tile at the centre and the floor, relinked, the body at once",
                lIHost.Teleport(liTileX, liTileY) && lORecord.FineX == 4 && lORecord.FineY == 4
                && lORecord.ZPos == (pOLevel.GetTile(liTileX, liTileY).FloorHeight >> 4) << 3
                && lOWorld.Log.Contains("relink " + liTileX + "/" + liTileY) && lOOne.View.Log.Contains("body now"), true);

            // The talk: only when a conversation can start.
            lOWorld.Talks = false;
            lIHost.OnTalk();
            fExpectBool("talk: none while a conversation is open", lOWorld.Log.Contains("talk"), false);
            lOWorld.Talks = true;
            lIHost.OnTalk();
            fExpectBool("talk: else the creature's conversation", lOWorld.Log.Contains("talk"), true);

            // The missile: the ammunition of the table at the target, counted when made.
            lOWorld.Player = new CritterTarget { Exists = true, Index = 1, IsPlayer = true };
            lORecord.GTarg = UWCritterBrain.PlayerIndex;
            lIHost.OnMissileLaunched(0, 0);
            fExpectBool("missile: the arrow with its ranged damage, type missile",
                lOWorld.Log.Contains(string.Format("launch {0:X} base {1} type {2}", UWObjectMechanics.FirstAmmunitionObjectId,
                    pOData.ObjectProperties.GetRangedDamage(UWObjectMechanics.FirstAmmunitionObjectId), UWDamageTypes.Missile))
                && lOOne.Actor.ShotsFired == 1, true);

            lOWorld.LaunchRoom = false;
            lIHost.OnMissileLaunched(0, 0);
            fExpectInt("missile: no room at the start, none made", lOOne.Actor.ShotsFired, 1);

            lOWorld.PlayerThere = false;
            lOWorld.LaunchRoom = true;
            int liLaunches = lOWorld.Log.FindAll(psLine => psLine.StartsWith("launch")).Count;

            lIHost.OnMissileLaunched(0, 0);
            fExpectInt("missile: no target, no missile", lOWorld.Log.FindAll(psLine => psLine.StartsWith("launch")).Count, liLaunches);

            // The drowning and the death's events, in the order the remains rely on.
            lIHost.OnDrowned();
            fExpectBool("drowning: the view's splash", lOOne.View.Log.Contains("drowning"), true);

            // Committed once since the death machine (cleanup step 4.12, 2026-10-19): the end commits
            // only a death that never started (a drowning); until then it marked the body again.
            lOOne.View.Log.Clear();
            lIHost.OnDeathStarted(false);
            lIHost.OnDeathFinished();
            // The record's picture right after the commit since cleanup step 4.12b (2026-10-19): the
            // first death picture at once, as the original's renderer draws the record.
            fExpectBool("death: destroyed when it starts, its picture at once, complete at frame 3",
                string.Join(",", lOOne.View.Log) == "destroyed,motion " + lOOne.Actor.Record.Animation + "/"
                + lOOne.Actor.Record.Frame + ",complete death", true);
            fExpectBool("death: no veto without a whoami", lIHost.VetoDeath(), false);
        }
    }
}
