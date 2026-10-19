using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 3.1 (2026-10-17): the world, the presentation and the player's body, phase A -
    /// the spot's arithmetic, the silent scope (the lines dropped, everything else played), the four
    /// level conventions of the hosts and the split of travel and teleport. FakeWorld is the headless
    /// world the later steps of phase 3 build their trap, spell and sleep checks on.
    /// </summary>
    public static partial class Program
    {
        /// <summary>A presentation without a scene: every call becomes a line of the log.</summary>
        private sealed class LoggingPresentation : IUWPresentation
        {
            public readonly List<string> Log;

            public LoggingPresentation() : this(new List<string>())
            {
            }

            /// <summary>Into a log shared with another fake, to see the order across both.</summary>
            public LoggingPresentation(List<string> pOLog)
            {
                Log = pOLog;
            }

            /// <summary>What PlayCutscene answers - false as if no cutscene player were there.</summary>
            public bool CanPlayCutscenes = true;

            public bool ShowsMessages => true;

            public void AddMessage(string psMessage) { Log.Add("message " + psMessage); }

            public void AddGeneralMessage(int piIndex) { Log.Add("general " + piIndex); }

            public string GetGeneralMessage(int piIndex) { return "text " + piIndex; }

            public void PlaySoundAtPlayer(int piEffect) { Log.Add("sound " + piEffect); }

            public void ShowEffect(int piEffectObjectId, UWSpot pOAt, float pfSeconds, int piStartFrame = 0)
            {
                Log.Add("effect " + piEffectObjectId + " " + pOAt);
            }

            public void ShakeScreen(int piDuration) { Log.Add("shake " + piDuration); }

            public void FlashWindow(int piPaletteColour, float pfSeconds) { Log.Add("flash " + piPaletteColour); }

            /// <summary>How long the last black of the view window lasted; -1 for none.</summary>
            public float LastBlackSeconds = -1f;

            public void ShowNightBlack(float pfSeconds)
            {
                LastBlackSeconds = pfSeconds;
                Log.Add("black");
            }

            public bool PlayCutscene(int piCutscene)
            {
                Log.Add("cutscene " + piCutscene);

                return CanPlayCutscenes;
            }

            public void ChangeMusicTheme(int piTheme) { Log.Add("music " + piTheme); }
        }

        /// <summary>A body without a scene, standing where the check puts it.</summary>
        private sealed class FakeAvatar : IUWPlayerAvatar
        {
            public UWSpot Floor = UWSpot.Nowhere;

            public UWSpot Feet = UWSpot.Nowhere;

            public float EyeX;

            public float EyeZ;

            public float Yaw;

            public bool HasBody => Floor.IsOnMap;

            public UWSpot FloorSpot => Floor;

            public UWSpot FeetSpot => Feet;

            public void GetEyePosition(out float pfX, out float pfZ)
            {
                pfX = EyeX;
                pfZ = EyeZ;
            }

            public void GetViewDirection(out float pfX, out float pfZ)
            {
                (pfX, pfZ) = UWHeadings.HeadingByteToDirection(UWHeadings.DegreesToHeadingByte(Yaw));
            }

            public float ViewYawDegrees => Yaw;

            public bool Swimming;

            public bool OnLava;

            public bool OnGround = true;

            public bool IsSwimming => Swimming;

            public bool IsOnLava => OnLava;

            public bool IsOnGround => OnGround;

            public UWInventoryModel Carried;

            public UWInventoryModel Inventory => Carried;

            public UWPlayerVitals Numbers;

            public UWPlayerVitals Vitals => Numbers;

            public bool CombatModeLeft;

            public void LeaveCombatMode() { CombatModeLeft = true; }

            public int Proof;

            public int DamageTypeProof => Proof;

            public int Bounces;

            public void BounceUp(int piIntensity) { Bounces++; }
        }

        /// <summary>A door without a scene: its state is what the check sets, its moves are lines of
        /// the world's log.</summary>
        private sealed class FakeDoor : IUWDoor
        {
            private readonly List<string> mOLog;

            public FakeDoor(List<string> pOLog, UWObject pOData, bool pbClosed, bool pbMoving = false)
            {
                mOLog = pOLog;
                Data = pOData;
                Closed = pbClosed;
                Moving = pbMoving;
            }

            public bool Closed;

            public bool Moving;

            public UWObject Data { get; }

            public bool IsClosed => Closed && !Moving;

            public bool IsMoving => Moving;

            public bool IsHeadingClosed => Closed;

            public UWLockState LockState { get; } = new UWLockState();

            private string fName => Data != null ? Data.TileX + "/" + Data.TileY : "no data";

            public void Toggle(UWTrapRules.Setter peOpener)
            {
                Closed = !Closed;
                mOLog.Add("door " + fName + (Closed ? " closes" : " opens"));
                mOLog.Add("door " + fName + " moved by " + peOpener);
            }

            public void Open(UWTrapRules.Setter peOpener)
            {
                if (UWDoorRules.OpenActs(IsHeadingClosed))
                    Toggle(peOpener);
            }

            public void Close()
            {
                if (UWDoorRules.CloseActs(IsHeadingClosed))
                    Toggle(UWTrapRules.Setter.Thing);
            }

            public void ReplaceLock(UWObject pOTemplate)
            {
                mOLog.Add("door " + fName + " lock replaced");
            }
        }

        /// <summary>
        /// A world without a scene (plan section 9.2: FakeWorld): a level the check hands in, the
        /// logging presentation, the fake body, and the travels as lines. Shared with the checks of
        /// the later phase 3 steps. Since step 3.3 (2026-10-17) its blows land through the real
        /// UWBlowRouting on bodies that only log (Blows); a creature whose index is in
        /// RemovedCreatures takes none (DestroyedCreatures until cleanup step 4.12c, 2026-10-19: since
        /// then a dying creature takes blows, only a removed one none - UWCreatureActor.TakesBlows).
        /// </summary>
        private sealed class FakeWorld : IUWWorld, IUWBlowBodies
        {
            public readonly List<string> Log;

            public readonly List<string> Blows = new List<string>();

            public readonly HashSet<int> RemovedCreatures = new HashSet<int>();

            public readonly LoggingPresentation Shown;

            public FakeWorld() : this(new List<string>(), new List<string>())
            {
            }

            /// <summary>The world's and the presentation's lines into one log (cleanup step 3.6: the
            /// endgame's order across messages, gate and camera).</summary>
            public FakeWorld(List<string> pOSharedLog) : this(pOSharedLog, pOSharedLog)
            {
            }

            private FakeWorld(List<string> pOLog, List<string> pOShownLog)
            {
                Log = pOLog;
                Shown = new LoggingPresentation(pOShownLog);
            }

            /// <summary>Called after every travel (the endgame check stamps the clock).</summary>
            public System.Action Travelled;

            public readonly FakeAvatar Body = new FakeAvatar();

            public DataImport Data { get; set; }

            public UWLevel Level { get; set; }

            public int LevelIndex { get; set; }

            public int LevelCount { get; set; } = 9;

            public UWTileQueries Tiles => new UWTileQueries(Level, Data);

            public IUWPresentation Presentation => Shown;

            public IUWPlayerAvatar Player => Body;

            public void TravelTo(int piLevelIndex, int piTileX, int piTileY)
            {
                Log.Add("travel " + piLevelIndex + " " + piTileX + "/" + piTileY);
                Travelled?.Invoke();
            }

            public void TeleportTo(int piLevelIndex, int piTileX, int piTileY)
            {
                Log.Add("teleport " + piLevelIndex + " " + piTileX + "/" + piTileY);
            }

            /// <summary>The level's creatures in the order the check adds them - the fake's
            /// UWCritter.Active (cleanup step 3.2).</summary>
            public readonly List<UWCreatureRef> Creatures = new List<UWCreatureRef>();

            public int FindCreatures(UWTileArea pOArea, UWCreatureFilter peFilter, List<UWCreatureRef> pOInto)
            {
                return UWCreatureSearch.Collect(Creatures, pOArea, peFilter, pOInto);
            }

            public void Damage(UWDamageTarget pOTarget, UWBlow pOBlow)
            {
                UWBlowRouting.Land(this, pOTarget, pOBlow);
            }

            public bool CreatureTakesBlows(UWDamageTarget pOTarget)
            {
                return !RemovedCreatures.Contains(pOTarget.Index);
            }

            /// <summary>A creature's line names the attacker the blow carries (cleanup step 4.8); an
            /// object's keeps none.</summary>
            public void DamageBody(UWDamageTarget pOTarget, int piDamage, int piDamageType, int piAttacker)
            {
                Blows.Add(pOTarget + " takes " + piDamage + " type " + piDamageType
                    + (pOTarget.Kind == UWDamageTargetKind.Creature ? " by " + piAttacker : ""));
            }

            /// <summary>Every request the player's end took (cleanup step 4.2), beside its line.</summary>
            public readonly List<UWDamageRequest> PlayerRequests = new List<UWDamageRequest>();

            public void DamagePlayer(UWDamageRequest pORequest)
            {
                PlayerRequests.Add(pORequest);

                if (pORequest.Source == UWDamageSource.Curse)
                    Blows.Add("player cursed " + pORequest.Amount);
                else if (pORequest.Source == UWDamageSource.Backfire)
                    Blows.Add("player backfire " + pORequest.Amount);
                else
                    Blows.Add("player takes " + pORequest.Amount + " type " + pORequest.Type);
            }

            public void StrikeThingInFlight(int piFlightIndex, int piDamage, int piDamageType)
            {
                Blows.Add("flight " + piFlightIndex + " takes " + piDamage + " type " + piDamageType);
            }

            // ------------------------------------------------- cleanup step 3.6

            /// <summary>The level's doors in the order the check adds them; asking for them is a line
            /// of the log ("doors"), so a check sees when the night walks them.</summary>
            public readonly List<IUWDoor> DoorList = new List<IUWDoor>();

            public IUWDoors Doors => new FakeDoors(this);

            private sealed class FakeDoors : IUWDoors
            {
                private readonly FakeWorld mOWorld;

                public FakeDoors(FakeWorld pOWorld)
                {
                    mOWorld = pOWorld;
                }

                public IReadOnlyList<IUWDoor> All
                {
                    get
                    {
                        mOWorld.Log.Add("doors");

                        return mOWorld.DoorList;
                    }
                }

                public IUWDoor Find(UWObject pODoorData)
                {
                    return mOWorld.DoorList.Find(pODoor => pODoor.Data == pODoorData);
                }
            }

            /// <summary>What Spawn put into the level, in order.</summary>
            public readonly List<UWObject> Spawned = new List<UWObject>();

            public bool Spawn(UWObject pOObject, UWTilePos pOTile)
            {
                Spawned.Add(pOObject);
                Log.Add("spawn " + (pOObject != null ? pOObject.ID : -1) + " on " + pOTile.X + "/" + pOTile.Y);

                return pOObject != null;
            }

            public bool SpawnNewObject(int piObjectId, UWTilePos pOTile, int piQuantity, UWWorldEdit.Arrival peArrival)
            {
                Log.Add(string.Format("spawn {0:X} {1}/{2} {3:X} {4}", piObjectId, pOTile.X, pOTile.Y, piQuantity, peArrival));

                return true;
            }

            /// <summary>What Remove took, in order.</summary>
            public readonly List<UWObject> Removed = new List<UWObject>();

            /// <summary>Remove takes the object out of its tile's list in Level, as the level edit
            /// does (the trap checks run on the real levels).</summary>
            public bool RemovesFromTiles;

            /// <summary>What GiveToHand answers.</summary>
            public bool HandTakes = true;

            public bool Remove(UWObject pOObject)
            {
                Log.Add("remove " + (pOObject != null ? pOObject.ID : -1));

                if (pOObject == null)
                    return false;

                Removed.Add(pOObject);

                if (!RemovesFromTiles)
                    return true;

                UWTile lOTile = Level != null ? Level.GetTile(pOObject.TileX, pOObject.TileY) : null;

                return lOTile != null && lOTile.ObjectsInTile.Remove(pOObject);
            }

            public bool GiveToHand(UWObject pOItem, bool pbPreferBags)
            {
                Log.Add("give " + (pOItem != null ? pOItem.ID : -1) + (pbPreferBags ? " to the bags" : " to the hand"));

                return HandTakes && pOItem != null;
            }

            public void TalkTo(UWCreatureRef pOCreature)
            {
                Log.Add("talk " + pOCreature.ObjectId);
            }

            public void DropAtPlayer(UWObject pOItem)
            {
                Log.Add("drop " + (pOItem != null ? pOItem.ID : -1) + " at the player's feet");
            }

            public void DropAroundPlayer(UWObject pOItem)
            {
                Log.Add("drop " + (pOItem != null ? pOItem.ID : -1) + " around the player");
            }

            public bool DropAtCreature(UWObject pOItem, UWNpc pONpc)
            {
                Log.Add("drop " + (pOItem != null ? pOItem.ID : -1) + " at " + (pONpc != null ? pONpc.ID : -1));

                return pOItem != null && pONpc != null;
            }

            public bool DropObjectAt(UWObject pOObject, float pfWorldX, float pfWorldZ)
            {
                Log.Add("drop " + (pOObject != null ? pOObject.ID : -1) + " at " + pfWorldX.ToString("0.00",
                    System.Globalization.CultureInfo.InvariantCulture) + "/" + pfWorldZ.ToString("0.00",
                    System.Globalization.CultureInfo.InvariantCulture));

                return pOObject != null;
            }

            public bool SpawnWardRune(int piTileX, int piTileY)
            {
                Log.Add("ward rune on " + piTileX + "/" + piTileY);

                return true;
            }

            public bool DropBoulder(int piObjectId, int piTileX, int piTileY)
            {
                Log.Add("boulder " + piObjectId + " on " + piTileX + "/" + piTileY);

                return true;
            }

            /// <summary>What CreatureFitsAt answers.</summary>
            public bool CreaturesFit = true;

            public bool CreatureFitsAt(int piObjectId, float pfWorldX, float pfWorldZ)
            {
                return CreaturesFit;
            }

            /// <summary>The check's names for the creatures it puts into the level's tile lists (the
            /// log names them so; an unnamed one by its object id).</summary>
            public readonly Dictionary<UWObject, string> Names = new Dictionary<UWObject, string>();

            /// <summary>What a confusion or a target spell does to each creature; a creature named
            /// in Answers answers its own.</summary>
            public UWTargetSpellRules.Result TargetAnswer = UWTargetSpellRules.Result.Shown;

            public readonly Dictionary<UWObject, UWTargetSpellRules.Result> Answers =
                new Dictionary<UWObject, UWTargetSpellRules.Result>();

            public string NameOf(UWObject pOObject)
            {
                return pOObject == null ? "nothing" : Names.TryGetValue(pOObject, out string lsName) ? lsName : pOObject.ID.ToString("X");
            }

            /// <summary>Puts a creature (UWNpc, object id piId) at the tail of the tile's list in
            /// Level, as a creature that walked in is relinked (UWWorldEdit.Move), under a name.</summary>
            public UWNpc PutCreature(int piTileX, int piTileY, string psName, int piId = UWObjectIds.FirstCreature)
            {
                UWNpc lONpc = new UWNpc((ushort)piId) { TileX = piTileX, TileY = piTileY };

                Level.GetTile(piTileX, piTileY).ObjectsInTile.Add(lONpc);
                Names[lONpc] = psName;

                return lONpc;
            }

            private UWTargetSpellRules.Result fAnswerOf(UWObject pOCreature)
            {
                return Answers.TryGetValue(pOCreature, out UWTargetSpellRules.Result leAnswer) ? leAnswer : TargetAnswer;
            }

            public UWTargetSpellRules.Result ConfuseCreature(UWObject pOCreature)
            {
                Log.Add("confuse " + NameOf(pOCreature));

                return fAnswerOf(pOCreature);
            }

            public UWTargetSpellRules.Result CastTargetSpellOnCreature(UWObject pOCreature, int piMinorClass)
            {
                Log.Add("target spell " + piMinorClass + " on " + NameOf(pOCreature));

                return fAnswerOf(pOCreature);
            }

            public void ShowEffectAtCreature(UWObject pOCreature, int piEffectObjectId, float pfSeconds)
            {
                Log.Add("effect " + piEffectObjectId + " at " + NameOf(pOCreature));
            }

            public void StrikeTile(UWTilePos pOTile, int piDiceCount, int piDiceRange, int piDamageType, int piAttacker)
            {
                Log.Add("strike " + pOTile.X + "/" + pOTile.Y + " " + piDiceCount + "d" + piDiceRange + " type " + piDamageType
                    + " by " + piAttacker);
            }

            /// <summary>The travel range per creature object id (critter table 0x1C).</summary>
            public readonly Dictionary<int, int> TravelRanges = new Dictionary<int, int>();

            /// <summary>The path each creature (by object id) would find; none without an entry.</summary>
            public readonly Dictionary<int, List<UWTilePos>> Paths = new Dictionary<int, List<UWTilePos>>();

            public int CreatureTravelRange(UWCreatureRef pOCreature)
            {
                return TravelRanges.TryGetValue(pOCreature.ObjectId, out int liRange) ? liRange : 0;
            }

            public bool TryGetCreaturePath(UWCreatureRef pOCreature, UWTilePos pOTo, int piToHeight, List<UWTilePos> pOPath)
            {
                pOPath.Clear();

                if (!Paths.TryGetValue(pOCreature.ObjectId, out List<UWTilePos> lOPath))
                    return false;

                pOPath.AddRange(lOPath);
                Log.Add("path of " + pOCreature.ObjectId + " to " + pOTo.X + "/" + pOTo.Y + " at " + piToHeight);

                return true;
            }

            public bool PlaceCreature(UWCreatureRef pOCreature, UWTilePos pOTile, int piSubX, int piSubY)
            {
                Log.Add("place " + pOCreature.ObjectId + " on " + pOTile.X + "/" + pOTile.Y + " at " + piSubX + "/" + piSubY);

                return true;
            }

            public int RunRespawn(bool pbAnyDistance)
            {
                Log.Add("respawn " + (pbAnyDistance ? "anywhere" : "away from the player"));

                return 0;
            }

            public void LetTimePassForCreatures()
            {
                Log.Add("time passes");
            }

            public int CollectGarbage(int piRange, int piLimit)
            {
                Log.Add("cull " + piRange + "/" + piLimit);

                return 0;
            }
        }

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

        /// <summary>A creature for the fake world: its object (id 0x40 + piKind), its record at the
        /// record tile, and a body tile of its own.</summary>
        private static UWCreatureRef fFakeCreature(int piKind, int piRecordX, int piRecordY, int piBodyX, int piBodyY,
            bool pbDead = false, bool pbSpawned = false, bool pbWithRecord = true, bool pbWithObject = true,
            int piWhoami = 0, int piIndex = 0)
        {
            UWNpc lONpc = new UWNpc((ushort)(0x40 + piKind)) { NPCwhoami = (byte)piWhoami };
            UWCritterRecord lORecord = new UWCritterRecord(lONpc);

            lORecord.TileX = piRecordX;
            lORecord.TileY = piRecordY;

            if (pbSpawned)
                lONpc.MarkSpawned();

            return new UWCreatureRef(pbWithObject ? lONpc : null, pbWithRecord ? lORecord : null,
                new UWTilePos(piBodyX, piBodyY), pbDead, piIndex);
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
