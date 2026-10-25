using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The headless world (IUWWorld) the trap, spell, sleep, story and conversation checks run on,
    /// with its presentation that logs every call, the player's body and the doors (cleanup step
    /// 3.1, 2026-10-17).
    /// </summary>
    public static partial class SelfChecks
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
    }
}
