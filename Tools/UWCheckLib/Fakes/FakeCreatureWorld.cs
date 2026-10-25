using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The creature actor's surroundings (cleanup step 4.11): its view, its world, the player as
    /// a defender, the bodies of a blow, and a defender that records the wear.
    /// </summary>
    public static partial class SelfChecks
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

        /// <summary>The player as defender for the order check: counts the draws made before the wear.</summary>
        private sealed class WearRecordingDefender : IUWAttackDefender
        {
            private readonly RecordingRandom mORandom;

            public int DrawsBeforeWear = -1;

            public int WornPart = UWAttackResolution.NoPart;

            public WearRecordingDefender(RecordingRandom pORandom)
            {
                mORandom = pORandom;
            }

            public UWDefenderKind DefenderKind => UWDefenderKind.Player;

            public int Defence => 0;

            public int ProtectionAt(int piPart) => 0;

            public int ArmourAt(int piPart) => 0;

            public bool IsArmourMetalAt(int piPart) => false;

            public void OnCriticalHit(int piPart)
            {
                DrawsBeforeWear = mORandom.Ranges.Count;
                WornPart = piPart;
            }
        }
    }
}
