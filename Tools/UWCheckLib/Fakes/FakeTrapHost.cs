using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// The game around one firing of the trap rules, recording what the rules asked of it.
        /// The player stands on 0/0 with no inventory unless a check gives him one.
        ///
        /// SINCE CLEANUP STEP 3.6 (2026-10-17) the world of the firing is a FakeWorld (WorldChecks.cs)
        /// over the real level: the player's numbers are real vitals, the messages, shakes, blows,
        /// spawns and removals its logs. The counters below read those logs, so the checks still ask
        /// what they asked before.
        /// </summary>
        private sealed class FakeTrapHost : IUWTrapHost
        {
            public readonly FakeWorld Fake;

            public FakeTrapHost(DataImport pOData, int piLevelIndex)
            {
                Fake = new FakeWorld
                {
                    Data = pOData,
                    Level = pOData.Levels[piLevelIndex],
                    LevelIndex = piLevelIndex,
                    LevelCount = pOData.Levels.Count,
                    RemovesFromTiles = true
                };

                Fake.Body.Floor = new UWSpot(new UWTilePos(0, 0), 0, 0, 0);
                Fake.Body.Numbers = fTrapPlayer(-1);
            }

            /// <summary>A player with this casting skill (-1: the default).</summary>
            private static UWPlayerVitals fTrapPlayer(int piCastingSkill)
            {
                UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

                lOStart.CastingSkill = piCastingSkill;

                UWPlayerVitals lOVitals = new UWPlayerVitals(new FakeVitalsHost());

                lOVitals.Initialise(null, lOStart);

                return lOVitals;
            }

            public IUWWorld World => Fake;

            public UWTrapFiring Firing => new UWTrapFiring(HasTriggeringCreature, HasTriggeringThing);

            public bool HasTriggeringCreature { get; set; }

            public bool HasTriggeringThing { get; set; }

            public bool AlwaysFireCreateObjectTraps { get; set; }

            // ------------------------------------------------- What the check sets on the player

            public UWTilePos PlayerTile
            {
                get { return Fake.Body.Floor.Tile; }
                set { Fake.Body.Floor = new UWSpot(value, 0, 0, 0); }
            }

            public UWInventoryModel Inventory
            {
                get { return Fake.Body.Carried; }
                set { Fake.Body.Carried = value; }
            }

            public int PlayerCastingSkill
            {
                get { return Fake.Body.Numbers.GetSkill(UWPlayerData.Skill.Casting); }
                set { Fake.Body.Numbers = fTrapPlayer(value); }
            }

            public int PlayerDamageTypeProof
            {
                get { return Fake.Body.Proof; }
                set { Fake.Body.Proof = value; }
            }

            // ------------------------------------------------- What the check reads

            /// <summary>The untyped damage the player took (the trap's UWBlow on ThePlayer); setting
            /// it to 0 forgets the blows so far.</summary>
            public int PlayerDamage
            {
                get
                {
                    int liSum = 0;

                    foreach (string lsLine in Fake.Blows)
                    {
                        if (lsLine.StartsWith("player takes "))
                            liSum += int.Parse(lsLine.Substring("player takes ".Length).Split(' ')[0]);
                    }

                    return liSum;
                }
                set
                {
                    if (value == 0)
                        Fake.Blows.RemoveAll(psLine => psLine.StartsWith("player takes "));
                }
            }

            public int PlayerPoison => Fake.Body.Numbers.Poison;

            public int Curses => Fake.Blows.FindAll(psLine => psLine.StartsWith("player cursed")).Count;

            public int Shakes => Fake.Shown.Log.FindAll(psLine => psLine.StartsWith("shake")).Count;

            public int Bounces => Fake.Body.Bounces;

            /// <summary>The lines for the scroll: a composed one as it is, a line of string block 1
            /// as "#n".</summary>
            public List<string> Messages
            {
                get
                {
                    List<string> lOLines = new List<string>();

                    foreach (string lsLine in Fake.Shown.Log)
                    {
                        if (lsLine.StartsWith("message "))
                            lOLines.Add(lsLine.Substring("message ".Length));
                        else if (lsLine.StartsWith("general "))
                            lOLines.Add("#" + lsLine.Substring("general ".Length));
                    }

                    return lOLines;
                }
            }

            public List<UWObject> Spawned => Fake.Spawned;

            public List<UWObject> Removed => Fake.Removed;

            // ------------------------------------------------- The firing's own

            public void AdvanceLever(UWObject pOSwitchObject) { }

            public int Toggles;

            public void ToggleSwitchVisual(UWObject pOSwitchObject) { Toggles++; }

            public int Rebuilds;

            public void RebuildGeometry() { Rebuilds++; }

            public void OnObjectHeightChanged(UWObject pOObject, int piOldZPos) { HeightMoves++; }

            public void MoveCreaturesWithFloor(int piTileX, int piTileY, int piOldZPos, int piNewZPos) { }

            public int HeightMoves;

            public void SpawnPendingObjectsInTile(int piTileX, int piTileY) { }

            public void StrikeTriggeringThing(int piDamage) { }

            public int CreatureDamage;

            public void DamageTriggeringCreature(int piDamage) { CreatureDamage += piDamage; }

            public int WardHits;

            public void ApplyWardDamage(int piDamage, int piWardMessage, int piFirstDirectionMessage) { WardHits++; }

            public bool FireArrowTrap(UWObject pOTrap, int piTileX, int piTileY) => true;

            public readonly List<int> SpellsCast = new List<int>();

            public bool FireSpellTrap(UWObject pOTrap, int piTileX, int piTileY, int piMajorClass, int piMinorClass)
            {
                SpellsCast.Add((piMajorClass << 4) | piMinorClass);

                return true;
            }

            public bool FireCameraTrap(UWObject pOTrap, int piTileX, int piTileY) => true;

            public int AngerRaceNearPlayer(int piOwner, int piTilesBack, int piTilesForward) => 0;

            public bool BeginConversation(int piSlot) => true;

            public UWObject TalkedTo;

            public bool TalkTo(UWObject pONpcData)
            {
                TalkedTo = pONpcData;

                return true;
            }

            public bool PlayVictory() => true;
        }
    }
}
