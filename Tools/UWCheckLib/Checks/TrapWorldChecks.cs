using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The trap rules on FakeWorld, part of the Traps group (TrapChecks): the door trap, crowding,
    /// the silent firing, the quake and the book, the spell traps, the poison trap, the trigger
    /// setters, the tile height, the ward rune and the pit.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// The a_door trap as a rule over the world's doors (UWDoorRules.DoorTrap; on the trap host
        /// until cleanup step 3.6, 2026-10-17): at rest the lock is replaced, then the leaf acts by
        /// the quality; a moving door keeps its lock and turns only against the trap's wish; the one
        /// who set the chain off opens it.
        /// </summary>
        private static void fCheckDoorTrapOnWorld(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[0];
            List<UWObject> lOMaster = lOLevel.Masterlist;
            int liBase = lOMaster.Count;
            UWTile lOTile = lOLevel.GetTile(1, 1);
            UWObject lODoorData = new UWObject(0x140) { TileX = 1, TileY = 1 };

            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.DoorTrapId) { Quantity = (ushort)(liBase + 1) });
            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.LockObjectId));
            lOTile.ObjectsInTile.Add(lODoorData);

            try
            {
                UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.UseTriggerId)
                {
                    Quantity = (ushort)liBase, Flags = 6, Quality = 1, Owner = 1
                };

                // Close on an open door at rest: the lock is replaced, then it closes.
                lOMaster[liBase].Quality = (ushort)UWObjectMechanics.DoorTrapCloseAction;

                FakeTrapHost lOHost = new FakeTrapHost(pOData, 0);
                FakeDoor lODoor = new FakeDoor(lOHost.Fake.Log, lODoorData, false);

                lOHost.Fake.DoorList.Add(lODoor);
                UWTrapRules.FireTraps(lOTrigger, null, lOHost);

                fExpectInt("door trap: at rest the lock is replaced, then the door closes",
                    fIndexOfSequence(lOHost.Fake.Log, "door 1/1 lock replaced", "door 1/1 closes"), 0);

                // Open, set off by a creature: it opens the door.
                lOMaster[liBase].Quality = (ushort)UWObjectMechanics.DoorTrapOpenAction;
                lOHost = new FakeTrapHost(pOData, 0) { HasTriggeringCreature = true };
                lOHost.Fake.DoorList.Add(new FakeDoor(lOHost.Fake.Log, lODoorData, true));
                UWTrapRules.FireTraps(lOTrigger, null, lOHost);

                fExpectBool("door trap: a creature's chain opens the door as the creature",
                    lOHost.Fake.Log.Contains("door 1/1 opens") && lOHost.Fake.Log.Contains("door 1/1 moved by Creature"), true);

                // Close on a door swinging shut: the lock stays, nothing moves.
                lOMaster[liBase].Quality = (ushort)UWObjectMechanics.DoorTrapCloseAction;
                lOHost = new FakeTrapHost(pOData, 0);
                lOHost.Fake.DoorList.Add(new FakeDoor(lOHost.Fake.Log, lODoorData, true, true));
                UWTrapRules.FireTraps(lOTrigger, null, lOHost);

                fExpectBool("door trap: a closing door keeps its lock and goes on closing",
                    lOHost.Fake.Log.Exists(psLine => psLine.StartsWith("door 1/1")), false);

                // Open on a door swinging shut: it is turned round, by the player.
                lOMaster[liBase].Quality = (ushort)UWObjectMechanics.DoorTrapOpenAction;
                lOHost = new FakeTrapHost(pOData, 0);
                lOHost.Fake.DoorList.Add(new FakeDoor(lOHost.Fake.Log, lODoorData, true, true));
                UWTrapRules.FireTraps(lOTrigger, null, lOHost);

                fExpectBool("door trap: a closing door is turned round against the trap's wish, no new lock",
                    lOHost.Fake.Log.Contains("door 1/1 moved by Player") && !lOHost.Fake.Log.Contains("door 1/1 lock replaced"),
                    true);
            }
            finally
            {
                lOTile.ObjectsInTile.Remove(lODoorData);
                lOMaster.RemoveRange(liBase, 2);
            }
        }

        /// <summary>A creation trap whose template is a creature creates nothing while one made
        /// during play stands within four back and five forward (the world's creature search by
        /// the body's tile).</summary>
        private static void fCheckCrowdingOnWorld(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[0];
            List<UWObject> lOMaster = lOLevel.Masterlist;
            int liBase = lOMaster.Count;
            UWNpc lOTemplate = new UWNpc((ushort)(UWObjectClassProperties.CritterFirstId + 1)) { TileX = 20, TileY = 20 };

            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.CreateObjectTrapId) { Quantity = (ushort)(liBase + 1) });
            lOMaster.Add(lOTemplate);

            try
            {
                FakeTrapHost lOHost = new FakeTrapHost(pOData, 0) { AlwaysFireCreateObjectTraps = true };

                lOHost.Fake.Creatures.Add(fFakeCreature(1, 25, 25, 25, 25, pbSpawned: true));
                UWTrapRules.FireTrapInTile(lOMaster[liBase], 21, 21, lOHost);

                fExpectInt("crowding: a spawned creature five tiles on keeps the trap from creating", lOHost.Spawned.Count, 0);

                lOHost = new FakeTrapHost(pOData, 0) { AlwaysFireCreateObjectTraps = true };
                lOHost.Fake.Creatures.Add(fFakeCreature(1, 26, 26, 26, 26, pbSpawned: true));
                UWTrapRules.FireTrapInTile(lOMaster[liBase], 21, 21, lOHost);

                fExpectInt("crowding: six tiles on it creates, at the template's spot",
                    lOHost.Fake.Log.FindAll(psLine => psLine.StartsWith("spawn")).Count, 1);
            }
            finally
            {
                lOMaster.RemoveRange(liBase, 2);
            }
        }

        /// <summary>A firing without a message channel (the respawn) is the silent scope: a text
        /// trap says nothing, the world still plays.</summary>
        private static void fCheckSilentFiring(DataImport pOData)
        {
            FakeTrapHost lOHost = new FakeTrapHost(pOData, 0);

            fExpectBool("silent firing: a host's world shows messages", lOHost.World.Presentation.ShowsMessages, true);

            IUWWorld lOSilent = UWScopedWorld.With(lOHost.World, UWSilentPresentation.Of(lOHost.World.Presentation));

            fExpectBool("silent firing: the scoped world shows none", lOSilent.Presentation.ShowsMessages, false);
            fExpectBool("silent firing: ... and is the same level", ReferenceEquals(lOSilent.Level, lOHost.World.Level), true);
            fExpectBool("silent firing: scoping it again keeps one layer",
                UWScopedWorld.With(lOSilent, lOSilent.Presentation) == lOSilent, true);
        }

        /// <summary>A59: the quake hack acts only when the player set it off; the exploding book
        /// hack finds the book on the player's tile, inside a bag lying there.</summary>
        private static void fCheckQuakeAndBook(DataImport pOData)
        {
            UWObject lOQuake = new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 0x3E };
            FakeTrapHost lOByCreature = new FakeTrapHost(pOData, 0) { HasTriggeringCreature = true };
            FakeTrapHost lOByPlayer = new FakeTrapHost(pOData, 0);

            UWTrapRules.SetOffDisarmableTrap(lOQuake, null, null, 0, 0, lOByCreature);
            UWTrapRules.SetOffDisarmableTrap(lOQuake, null, null, 0, 0, lOByPlayer);

            fExpectInt("quake: a creature shakes nothing", lOByCreature.Shakes + lOByCreature.Bounces, 0);
            fExpectInt("quake: the player gets shake and bounce", lOByPlayer.Shakes + lOByPlayer.Bounces, 2);

            UWLevel lOLevel = pOData.Levels[0];

            if (!fFindWorldEditTiles(new UWTileQueries(lOLevel, pOData), out UWTilePos lOOpen, out UWTilePos _, out UWTilePos _))
                return;

            List<UWObject> lOBagList = new List<UWObject>
            {
                null,
                new UWObject((ushort)UWObjectIds.FirstContainer) { HasQuantity = false, Quantity = 2 },
                new UWObject((ushort)UWExplodingBook.BookObjectId)
            };

            UWObject lOBag = lOBagList[1];

            lOBag.EnsureContentsLoaded(lOBagList);

            UWTile lOTile = lOLevel.GetTile(lOOpen.X, lOOpen.Y);

            lOTile.ObjectsInTile.Add(lOBag);

            FakeTrapHost lOHost = new FakeTrapHost(pOData, 0) { PlayerTile = lOOpen };
            bool lbQuest = UWQuestFlags.Get(UWExplodingBook.QuestFlag) != 0;

            UWTrapRules.SetOffDisarmableTrap(new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 0x29 }, null, null,
                0, 0, lOHost);

            lOTile.ObjectsInTile.Remove(lOBag);
            UWQuestFlags.Set(UWExplodingBook.QuestFlag, lbQuest ? 1 : 0);

            fExpectInt("exploding book: the book in a bag on the player's tile explodes", lOHost.Curses, 1);
            fExpectInt("exploding book: ... and is gone from the bag", lOBag.Contents.Count, 0);
        }

        /// <summary>A53: do_spell with the trap as the caster - classes 0-3, 7, 9, 10 and 11 do
        /// nothing, a heal goes to the player who set it off, the rest is cast.</summary>
        private static void fCheckSpellTrapCaster(DataImport pOData)
        {
            int[] liCasts = new int[15];
            List<UWObject> lOMaster = pOData.Levels[0].Masterlist;

            for (int liClass = 0; liClass < liCasts.Length; liClass++)
            {
                FakeTrapHost lOHost = new FakeTrapHost(pOData, 0);
                UWObject lOTrap = new UWObject((ushort)UWObjectMechanics.SpellTrapId) { Quality = (ushort)liClass, Owner = 1 };

                lOMaster.Add(lOTrap);

                // A repeatable trigger (flags bit 1) - nothing is cleaned up.
                UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.MoveTriggerId)
                {
                    Quantity = (ushort)(lOMaster.Count - 1), Flags = 6, Quality = 1, Owner = 1
                };

                UWTrapRules.FireTraps(lOTrigger, null, lOHost);
                lOMaster.RemoveAt(lOMaster.Count - 1);
                liCasts[liClass] = lOHost.SpellsCast.Count;
            }

            fExpectBool("spell trap caster: classes 0-3, 7, 9, 10, 11 do nothing, the rest is cast",
                string.Join(",", liCasts) == "0,0,0,0,1,1,1,0,1,0,0,0,1,1,1", true);
            fExpectBool("spell trap caster: a creature that sets off a heal is not healed by the player's spell",
                UWTrapRules.SpellTrapCastsFor(4, true, false), false);
        }

        /// <summary>A11: the spell trap of level 5 (slot 562 at 10/45, fireball 5/3) fires flat along
        /// its heading from its height. Its trigger 561 at 14/45 names 10/45; the corridor 10..13/45
        /// is open, and the ball at z 51 flies within the body of a player standing on the floor of
        /// 14/45 (z 32, height 23) - so it still hits him there when he stands in line.</summary>
        private static void fCheckSpellTrapLine(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[4];
            UWObject lOTrap = lOLevel.Masterlist.Count > 562 ? lOLevel.Masterlist[562] : null;
            UWObject lOTrigger = lOTrap != null ? lOLevel.Masterlist[561] : null;

            if (lOTrap == null || lOTrap.ID != UWObjectMechanics.SpellTrapId || lOTrigger == null
                || lOTrigger.ID != UWObjectMechanics.MoveTriggerId)
            {
                Check.Fail("spell trap", "level 5 slots 561/562 are not the trigger and the spell trap");

                return;
            }

            fExpectInt("spell trap: the fireball flies east (heading byte 64)", UWTrapRules.SpellTrapMissileHeading(lOTrap), 64);
            fExpectInt("spell trap: the trigger names the trap's tile", (lOTrigger.Quality << 8) | lOTrigger.Owner, (10 << 8) | 45);

            bool lbOpen = true;

            for (int liX = 10; liX <= 14; liX++)
                lbOpen &= lOLevel.GetTile(liX, 45).TileType == UWTile.TileTypeEnum.open;

            fExpectBool("spell trap: the corridor to 14/45 is open", lbOpen, true);

            int liFloorZ = (lOLevel.GetTile(14, 45).FloorHeight >> 4) << 3;
            UWCommonObjectProperties.Entry lOPlayer;
            int liPlayerHeight = pOData.CommonObjectProperties.TryGet(UWObjectMechanics.AdventurerObjectId, out lOPlayer)
                ? lOPlayer.Height : 0;

            fExpectBool("spell trap: the ball flies within a standing player's body on 14/45",
                lOTrap.ZPos > liFloorZ && lOTrap.ZPos < liFloorZ + liPlayerHeight, true);
        }

        /// <summary>A10: whack_thing poisons only past check_res(player, 1, 0x10) - the poison
        /// needles of level 3 (damage trap 828 at 1/10, quality 4, owner 1).</summary>
        private static void fCheckPoisonTrap(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[2];
            UWObject lOTrap = lOLevel.Masterlist.Count > 828 ? lOLevel.Masterlist[828] : null;

            if (lOTrap == null || lOTrap.ID != UWObjectMechanics.DamageTrapId || lOTrap.Owner == 0)
            {
                Check.Fail("poison trap", "level 3 slot 828 is no poison trap");

                return;
            }

            FakeTrapHost lOHost = new FakeTrapHost(pOData, 2);

            UWTrapRules.SetOffDisarmableTrap(lOTrap, null, null, 1, 10, lOHost);
            fExpectInt("poison trap: poisons with its quality", lOHost.PlayerPoison, lOTrap.Quality);

            lOHost = new FakeTrapHost(pOData, 2) { PlayerDamageTypeProof = UWDamageTypes.Poison };

            UWTrapRules.SetOffDisarmableTrap(lOTrap, null, null, 1, 10, lOHost);
            fExpectInt("poison trap: a poison resistance keeps it off", lOHost.PlayerPoison, 0);
            fExpectInt("poison trap: ... and does no damage either", lOHost.PlayerDamage, 0);

            UWPlayerVitals lOVitals = new UWPlayerVitals(new FakeVitalsHost());

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);

            lOVitals.ApplyPoison(20);
            fExpectInt("poison: the field keeps four bits (20 -> 4)", lOVitals.Poison, 4);
        }

        /// <summary>A4, A13: cast_trap_spell lays trigger and trap at the head of the tile's list,
        /// any tile, with their numbers; the trigger is a creature's only, one-shot, and the first
        /// creature takes both out again with their slots.</summary>
        /// <summary>UseTrigger (TRIGGER.C) asks who set a trigger off for every kind: the player
        /// uses, looks and picks up, so every use, look and pick-up trigger the shipped levels can
        /// reach - from a tile, along the chains and the special links - must let him
        /// (UWTrapRules.MaySetOff, asked since 2026-10-13: nothing may change). Records no tile
        /// reaches are left over (two on level 9 at 0/0) and do not count.</summary>
        private static void fCheckTriggerSetters(DataImport pOData)
        {
            int liCount = 0;
            int liRefused = 0;

            for (int liLevel = 0; liLevel < 9; liLevel++)
            {
                UWLevel lOLevel = pOData.Levels[liLevel];
                List<UWObject> lOMaster = lOLevel.Masterlist;
                HashSet<UWObject> lOSeen = new HashSet<UWObject>();
                Stack<UWObject> lOOpen = new Stack<UWObject>();

                foreach (UWTile lOTile in lOLevel.TileData)
                    if (lOTile != null && lOTile.ObjectsInTile != null)
                        foreach (UWObject lOObject in lOTile.ObjectsInTile)
                            lOOpen.Push(lOObject);

                while (lOOpen.Count > 0)
                {
                    UWObject lOObject = lOOpen.Pop();

                    if (lOObject == null || !lOSeen.Add(lOObject))
                        continue;

                    UWObject lONext = lOObject.GetNextObjectInLine(lOMaster);
                    UWObject lOLinked = UWObjectMechanics.GetLinkedObject(lOObject, lOMaster);

                    if (lONext != null)
                        lOOpen.Push(lONext);

                    if (lOLinked != null)
                        lOOpen.Push(lOLinked);

                    if (lOObject.ID != UWObjectMechanics.UseTriggerId && lOObject.ID != UWObjectMechanics.LookTriggerId
                        && lOObject.ID != UWObjectMechanics.PickUpTriggerId)
                        continue;

                    liCount++;

                    if (!UWTrapRules.MaySetOff(lOObject, UWTrapRules.Setter.Player))
                        liRefused++;
                }
            }

            fExpectBool("trigger setters: use, look and pick-up triggers reached", liCount > 0, true);
            fExpectInt("trigger setters: every one lets the player set it off", liRefused, 0);
        }

        /// <summary>change_terrain (WORLDEV.C): a rising tile lifts what lies below the new floor,
        /// a sinking one takes what rests on the old floor; a trigger stays, and a thing above the
        /// old floor stays when the tile sinks (UWTrapRules.SetTileHeight, 2026-10-13).</summary>
        private static void fCheckTileHeightCarries(DataImport pOData)
        {
            UWTile lOTile = new UWTile(0, (4u << 4) | 1u, pOData.Levels[0].TextureInfos);
            UWObject lOCoin = new UWObject(0xA0) { ZPos = 4 << 3 };
            UWObject lOHigh = new UWObject(0xA0) { ZPos = (4 << 3) + 5 };
            UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.MoveTriggerId) { ZPos = 0 };
            FakeTrapHost lOHost = new FakeTrapHost(pOData, 0);

            lOTile.ObjectsInTile.Add(lOCoin);
            lOTile.ObjectsInTile.Add(lOHigh);
            lOTile.ObjectsInTile.Add(lOTrigger);

            UWTrapRules.SetTileHeight(lOTile, 6, lOHost);
            fExpectInt("tile height: rising lifts the coin onto the new floor", lOCoin.ZPos, 6 << 3);
            fExpectInt("tile height: rising lifts what lies below the new floor", lOHigh.ZPos, 6 << 3);
            fExpectInt("tile height: a trigger stays", lOTrigger.ZPos, 0);

            lOHigh.ZPos = (6 << 3) + 5;
            UWTrapRules.SetTileHeight(lOTile, 3, lOHost);
            fExpectInt("tile height: sinking takes what rests on the old floor", lOCoin.ZPos, 3 << 3);
            fExpectInt("tile height: sinking leaves what lies above it", lOHigh.ZPos, (6 << 3) + 5);
            fExpectInt("tile height: the pictures follow (three moves)", lOHost.HeightMoves, 3);
            fExpectInt("tile height: the floor itself", lOTile.FloorHeight >> 4, 3);
        }

        private static void fCheckWardRune(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[0];
            UWTileQueries lOQueries = new UWTileQueries(lOLevel, pOData);
            UWWorldEdit lOEdit = new UWWorldEdit(lOQueries);

            if (!fFindWorldEditTiles(lOQueries, out UWTilePos lOOpen, out UWTilePos _, out UWTilePos lOSolid))
            {
                Check.Fail("ward rune", "level 1 has no open tile");

                return;
            }

            string lsPlaced = pOData.GetGeneralMessage(277) ?? string.Empty;

            fExpectBool("ward rune: message 277 is the placement", lsPlaced.StartsWith("The Rune of Warding is placed"), true);

            UWTile lOTile = lOLevel.GetTile(lOOpen.X, lOOpen.Y);
            int liFreeBefore = lOLevel.FreeStaticSlots.Count;

            fExpectBool("ward rune: laid on an open tile", UWTrapRules.CastTrapSpell(lOEdit, pOData.CommonObjectProperties,
                lOOpen.X, lOOpen.Y, UWObjectMechanics.WardTrapId), true);

            UWObject lOTrap = lOTile.ObjectsInTile.Count > 1 ? lOTile.ObjectsInTile[0] : null;
            UWObject lOTrigger = lOTile.ObjectsInTile.Count > 1 ? lOTile.ObjectsInTile[1] : null;

            if (lOTrap == null || lOTrap.ID != UWObjectMechanics.WardTrapId || lOTrigger == null
                || lOTrigger.ID != UWObjectMechanics.MoveTriggerId)
            {
                Check.Fail("ward rune", "the trap and the trigger are not the head of the tile's list");

                return;
            }

            fExpectInt("ward rune: two static slots taken", lOLevel.FreeStaticSlots.Count, liFreeBefore - 2);
            fExpectInt("ward rune: the trigger links the trap by number", lOTrigger.Quantity, lOLevel.Masterlist.IndexOf(lOTrap));
            fExpectInt("ward rune: trigger flags 8", lOTrigger.Flags, 8);
            fExpectBool("ward rune: invisible, door bit and quantity bit", lOTrigger.IsHidden && lOTrigger.DoorDirection
                && lOTrigger.HasQuantity && lOTrap.IsHidden && lOTrap.DoorDirection && lOTrap.HasQuantity, true);
            fExpectInt("ward rune: the trigger aims at its own tile", (lOTrigger.Quality << 8) | lOTrigger.Owner,
                (lOOpen.X << 8) | lOOpen.Y);
            fExpectInt("ward rune: trap flags 1 (one trigger)", lOTrap.Flags, 1);
            fExpectInt("ward rune: trap quality 0x3F", lOTrap.Quality, 0x3F);
            fExpectInt("ward rune: on the floor", lOTrigger.ZPos, (lOTile.FloorHeight >> 4) << 3);
            fExpectBool("ward rune: the player never sets it off", UWTrapRules.MaySetOff(lOTrigger, UWTrapRules.Setter.Player), false);
            fExpectBool("ward rune: a thing never sets it off", UWTrapRules.MaySetOff(lOTrigger, UWTrapRules.Setter.Thing), false);

            // An older save of ours laid it with flags 0 on both; reading the level repairs it.
            lOTrigger.Flags = 0;
            lOTrap.Flags = 0;
            fExpectInt("ward rune: an old save's rune is repaired once", UWTrapRules.RepairOldWardRunes(lOLevel), 1);
            fExpectBool("ward rune: ... a creature sets it off again, the trap counts one",
                UWTrapRules.MaySetOff(lOTrigger, UWTrapRules.Setter.Creature) && lOTrap.Flags == 1, true);
            fExpectInt("ward rune: ... and a second read finds nothing more", UWTrapRules.RepairOldWardRunes(lOLevel), 0);
            fExpectBool("ward rune: a creature sets it off", UWTrapRules.MaySetOff(lOTrigger, UWTrapRules.Setter.Creature), true);

            FakeTrapHost lOHost = new FakeTrapHost(pOData, 0) { HasTriggeringCreature = true, PlayerCastingSkill = 10 };

            UWTrapRules.TryFireMoveTrigger(lOTrigger, lOHost);

            fExpectInt("ward rune: the creature is hit once", lOHost.WardHits, 1);
            fExpectBool("ward rune: trigger and trap are gone from the tile",
                lOTile.ObjectsInTile.Contains(lOTrap) || lOTile.ObjectsInTile.Contains(lOTrigger), false);
            fExpectInt("ward rune: both slots are free again", lOLevel.FreeStaticSlots.Count, liFreeBefore);

            UWTile lOWall = lOLevel.GetTile(lOSolid.X, lOSolid.Y);

            fExpectBool("ward rune: laid in a wall too", UWTrapRules.CastTrapSpell(lOEdit, pOData.CommonObjectProperties,
                lOSolid.X, lOSolid.Y, UWObjectMechanics.WardTrapId), true);
            fExpectBool("ward rune: ... in the wall's list", lOWall.ObjectsInTile.Count > 1
                && lOWall.ObjectsInTile[0].ID == UWObjectMechanics.WardTrapId, true);
        }

        /// <summary>A trigger in the tile lists of a level whose special link is the given slot.</summary>
        private static UWObject fFindTriggerOf(UWLevel pOLevel, int piTrapIndex)
        {
            foreach (UWTile lOTile in pOLevel.TileData)
            {
                if (lOTile == null || lOTile.ObjectsInTile == null)
                    continue;

                foreach (UWObject lOObject in lOTile.ObjectsInTile)
                {
                    if (lOObject != null && lOObject.ID >= UWObjectIds.FirstTrigger && lOObject.ID <= UWObjectIds.LastTrigger
                        && lOObject.Quantity == piTrapIndex)
                        return lOObject;
                }
            }

            return null;
        }

        /// <summary>A1: UseTrap has no case for TRAP_PIT. The one pit trap of the game, level 3 slot
        /// 583 at 6/4, behind the use trigger 995 in the chain of the check trap at 5/5 - its tile
        /// stays the solid wall of height 15 it is.</summary>
        private static void fCheckPitTrap(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[2];
            UWObject lOPit = lOLevel.Masterlist.Count > 995 ? lOLevel.Masterlist[583] : null;
            UWObject lOTrigger = lOPit != null ? lOLevel.Masterlist[995] : null;

            if (lOPit == null || lOPit.ID != UWObjectMechanics.PitTrapId || lOTrigger == null || lOTrigger.Quantity != 583)
            {
                Check.Fail("pit trap", "level 3 slot 583 or its trigger 995 not found");

                return;
            }

            UWTile lOTile = lOLevel.GetTile(6, 4);
            FakeTrapHost lOHost = new FakeTrapHost(pOData, 2);

            fExpectBool("pit trap: the chain runs", UWTrapRules.FireTraps(lOTrigger, null, lOHost), true);
            fExpectBool("pit trap: level 3, 6/4 stays solid", lOTile.TileType == UWTile.TileTypeEnum.solid, true);
            fExpectInt("pit trap: ... at height 15", lOTile.FloorHeight >> 4, 15);
            fExpectInt("pit trap: nothing is rebuilt", lOHost.Rebuilds, 0);
        }
    }
}
