using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The creature brain's world for the brain checks: one creature on a flat floor, the player
    /// where the check puts him, scripted rolls, every event as a line.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// A world for one creature: a flat 16 x 16 floor, a scripted RNG (a queue of values,
        /// then a default), the player as object 1 wherever the check puts him, the outcome of
        /// the last step as the check sets it, and every event the brain raises as a line.
        /// The path search steps one tile towards the destination, x first.
        /// The level's services too (ICritterServices, cleanup step 4.13, 2026-10-19): the clock, the
        /// alarm, the path slots and the scripted rolls as an IUWRandom (ScriptedRolls); fMakeBrain
        /// hands them, the own height and the velocities to a brain and listens to its goal changes.
        /// </summary>
        private sealed class FakeCritterHost : ICritterHost, ICritterServices
        {
            public long ClockValue;

            private readonly ScriptedRolls mORolls = new ScriptedRolls();

            public Queue<int> Rolls => mORolls.Queue;

            /// <summary>What Random answers when the queue is empty, taken modulo n.</summary>
            public int DefaultRoll
            {
                get { return mORolls.Default; }
                set { mORolls.Default = value; }
            }

            public readonly UWCritterAlarm AlarmValue = new UWCritterAlarm();

            public readonly Dictionary<int, CritterTarget> Objects = new Dictionary<int, CritterTarget>();

            public StepResult Step;

            public bool LineOfSight = true;

            public bool StraightClear = true;

            public bool PathAvailable = true;

            public readonly List<string> Events = new List<string>();

            private readonly int[] miFloors = new int[16 * 16];

            public long Clock => ClockValue;

            public IUWRandom Random => mORolls;

            public UWCritterAlarm Alarm => AlarmValue;

            public int FloorLevelAt(int piTileX, int piTileY)
            {
                return piTileX < 0 || piTileY < 0 || piTileX >= 16 || piTileY >= 16 ? 0 : miFloors[(piTileY * 16) + piTileX];
            }

            public bool IsMagicBlockedAt(int piTileX, int piTileY) => false;

            public int DungeonLevel { get; set; } = 1;

            public bool TybalOrbStands => false;

            /// <summary>The own object height a brain of this fake gets (fMakeBrain).</summary>
            public int OwnObjectHeight => 16;

            /// <summary>The ammunition velocities a brain of this fake gets: 20 each.</summary>
            public int[] MissileVelocities
            {
                get
                {
                    int[] liVelocities = new int[UWObjectProperties.RangedObjectCount];

                    for (int liAt = 0; liAt < liVelocities.Length; liAt++)
                        liVelocities[liAt] = 20;

                    return liVelocities;
                }
            }

            public UWTilePos ViewTile { get; set; } = new UWTilePos(-100, -100);

            public CritterTarget GetObject(int piIndex)
            {
                CritterTarget lOTarget;

                return Objects.TryGetValue(piIndex, out lOTarget) ? lOTarget : default;
            }

            public bool HasLineOfSight(int piX0, int piY0, int piZ0, int piX1, int piY1, int piZ1) => LineOfSight;

            public bool IsStraightLineClear(int piToTileX, int piToTileY) => StraightClear;

            /// <summary>A square the found path passes between start and destination; -1 for a
            /// path straight from the start to the destination.</summary>
            public int NextPathTileX = -1;

            public int NextPathTileY = -1;

            /// <summary>The jump flags the found path's squares get (start first); none by default.</summary>
            public bool[] PathJumps;

            /// <summary>How often the path search ran.</summary>
            public int PathSearches;

            public UWCritterPathSlots Slots = new UWCritterPathSlots();

            public UWCritterPathSlots PathSlots => Slots;

            public bool TryFindPath(int piFromX, int piFromY, int piFromHeight, int piToTileX, int piToTileY, int piToHeight,
                int piRangeBudget, List<UWTilePos> pOSquares, List<bool> pOJumps)
            {
                pOSquares.Clear();
                pOJumps.Clear();
                PathSearches++;

                if (!PathAvailable)
                    return false;

                pOSquares.Add(new UWTilePos(piFromX, piFromY));

                if (NextPathTileX >= 0)
                    pOSquares.Add(new UWTilePos(NextPathTileX, NextPathTileY));

                pOSquares.Add(new UWTilePos(piToTileX, piToTileY));

                for (int liAt = 0; liAt < pOSquares.Count; liAt++)
                    pOJumps.Add(PathJumps != null && liAt < PathJumps.Length && PathJumps[liAt]);

                return true;
            }

            /// <summary>The floor level of one tile of the 16 x 16 floor.</summary>
            public void SetFloor(int piTileX, int piTileY, int piLevel)
            {
                miFloors[(piTileY * 16) + piTileX] = piLevel;
            }

            public StepResult RunMotion()
            {
                return Step;
            }

            public bool Teleport(int piTileX, int piTileY)
            {
                Events.Add("teleport " + piTileX + "/" + piTileY);

                return true;
            }

            public bool UseDoor(int piDoorIndex)
            {
                Events.Add("use door " + piDoorIndex);

                return false;
            }

            public void PickDoor(int piDoorIndex, int piSkill) => Events.Add("pick door " + piDoorIndex);

            public void BashDoor(int piDoorIndex, int piDamage) => Events.Add("bash door " + piDoorIndex + " " + piDamage);

            public void OnSwingStarted(int piAttack) => Events.Add("swing " + piAttack);

            public void OnBlow(int piTargetIndex, int piAttack, int piCharge, int piSwingType, int piFlank)
                => Events.Add("blow " + piTargetIndex + " " + piAttack + " charge " + piCharge + " flank " + piFlank);

            public void OnMissileLaunched(int piAmmoIndex, int piPitch) => Events.Add("missile " + piAmmoIndex);

            public void OnSpellCast(int piSpellIndex, int piPitch) => Events.Add("spell " + piSpellIndex);

            public void OnTalk() => Events.Add("talk");

            public void PlaySound(int piSound) => Events.Add("sound " + piSound);

            public void PlayMusic(int piTheme) => Events.Add("music " + piTheme);

            public void OnDrowned() => Events.Add("drowned");

            public void OnDeathStarted(bool pbByPlayer) => Events.Add("death");

            /// <summary>Whether a named creature refuses its death (UWSpecialDeaths: the golem).</summary>
            public bool Vetoes;

            public bool VetoDeath() => Vetoes;

            public void OnDeathFinished() => Events.Add("removed");

            public void OnGoalChanged(int piOldGoal, int piNewGoal, int piGTarg) => Events.Add("goal " + piOldGoal + ">" + piNewGoal);

            /// <summary>The player at a position in eighths, on the floor, looking this way.</summary>
            public void PlacePlayer(int piX, int piY, int piFacing = 0, int piLoudness = 8, int piVisibility = 8, bool pbWeaponDrawn = false)
            {
                Objects[1] = new CritterTarget
                {
                    Exists = true,
                    Index = 1,
                    IsPlayer = true,
                    IsCreature = true,
                    ItemId = 0x7F,
                    X = piX,
                    Y = piY,
                    ZPos = 0,
                    TileX = piX >> 3,
                    TileY = piY >> 3,
                    ObjectHeight = 16,
                    FacingEighth = piFacing,
                    Loudness = piLoudness,
                    Visibility = piVisibility,
                    HitPoints = 30,
                    Goal = 0,
                    Kind = 0,
                    WeaponDrawn = pbWeaponDrawn
                };
            }

            public bool HasEvent(string psPrefix)
            {
                foreach (string lsEvent in Events)
                {
                    if (lsEvent.StartsWith(psPrefix, StringComparison.Ordinal))
                        return true;
                }

                return false;
            }

            public int CountEvents(string psPrefix)
            {
                int liCount = 0;

                foreach (string lsEvent in Events)
                {
                    if (lsEvent.StartsWith(psPrefix, StringComparison.Ordinal))
                        liCount++;
                }

                return liCount;
            }
        }

        /// <summary>The scripted RNG of FakeCritterHost: a queue of values, then a default, each
        /// taken modulo n (cleanup step 4.13, 2026-10-19; FakeCritterHost.Random until then).</summary>
        private sealed class ScriptedRolls : IUWRandom
        {
            public readonly Queue<int> Queue = new Queue<int>();

            public int Default;

            public int Next(int piMaxExclusive)
            {
                int liValue = Queue.Count > 0 ? Queue.Dequeue() : Default;

                return piMaxExclusive <= 0 ? 0 : liValue % piMaxExclusive;
            }

            public int Next(int piMin, int piMaxExclusive)
            {
                return piMin + Next(piMaxExclusive - piMin);
            }

            public double NextDouble()
            {
                return Next(0x8000) / (double)0x8000;
            }
        }
    }
}
