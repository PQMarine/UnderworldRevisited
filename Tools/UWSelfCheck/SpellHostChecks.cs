using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 3.6 (2026-10-17): the spell rules on the fake world. The spell host shrank to
    /// the caster (FakeSpellHost below); the area targets are a list the rule holds, the creature
    /// caster a value (UWCastContext). Checked here for the first time without a scene: Confusion's
    /// walk over the targets, Sheet Lightning's strikes charged to the caster, the class 7 spell on
    /// the first creature in process_area's order, Detect Monsters on the world's creatures, and
    /// Tremor's boulders, shake and rumble.
    /// </summary>
    public static partial class Program
    {
        /// <summary>One cast without a scene: the caster's numbers are fixed, what he does is a
        /// line of the world's log.</summary>
        private sealed class FakeSpellHost : IUWSpellHost
        {
            public readonly FakeWorld Fake;

            public FakeSpellHost(FakeWorld pOWorld, UWCastContext pOCast)
            {
                Fake = pOWorld;
                Cast = pOCast;
            }

            public IUWWorld World => Fake;

            public UWCastContext Cast { get; }

            public DataImport Data => null;

            public bool IsMagicBlocked => false;

            public bool HasPlayer => true;

            public int PlayerLevel => 16;

            public int PlayerMana => 30;

            public int PlayerCastingSkill => 30;

            public void SpendMana(int piAmount) { Fake.Log.Add("spend " + piAmount); }

            public double RealSeconds => 1000d;

            public void RestoreManaFromSpell(int piMinorClass) { }

            public void Heal(int piAmount) { Fake.Log.Add("heal " + piAmount); }

            public void HealFully() { Fake.Log.Add("heal fully"); }

            public bool TryAddActiveSpell(int piMajorClass, int piMinorClass, int piStability) { return true; }

            public void BouncePlayerUp() { }

            public void BeginRoamingSightWithoutSpell() { }

            public void CurePoison() { }

            public void StartHallucination() { }

            public void PlaySpellSound() { Fake.Log.Add("spell sound"); }

            public void PlaySpellFailureSound() { Fake.Log.Add("failure sound"); }

            public void RefreshSpellIcons() { }

            public void BeginProjectileSpell(int piProjectileId, int piDamage, int piSpeed, int piManaCost) { }

            public void BeginTargetSpell(int piMajorClass, int piMinorClass) { }

            public void FirePendingSpell() { }

            public void Armageddon() { Fake.Log.Add("armageddon"); }

            public void FireBullfrog(int piMode) { }

            public bool FireLookTrigger(UWObject pOObject, int piSearchSkill) { return false; }
        }

        private static void fCheckSpellHost()
        {
            Check.Group("Spells on the world (cleanup step 3.6)");

            fCheckCastContext();

            UWRandom.Use(new FixedRandomSource(true));

            try
            {
                fCheckAreaConfusion();
                fCheckTargetSpellInArea();
                fCheckDetectOnWorld();
            }
            finally
            {
                UWRandom.Use(null);
            }

            UWRandom.Use(new FixedRandomSource(false));

            try
            {
                fCheckSheetLightningCaster();
                fCheckTremorOnWorld();
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>The caster as a value: the player's blasts are his, a creature's its own.</summary>
        private static void fCheckCastContext()
        {
            fExpectBool("cast: the player is no creature", UWCastContext.ThePlayer.ByCreature, false);
            fExpectInt("cast: the player's blasts are charged to him", UWCastContext.ThePlayer.AttackerIndex,
                UWCritterBrain.PlayerIndex);
            fExpectBool("cast: a creature casts", UWCastContext.Creature(7).ByCreature, true);
            fExpectInt("cast: a creature's blasts are charged to it", UWCastContext.Creature(7).AttackerIndex, 7);
        }

        /// <summary>A world for one cast: the player on 10/10 looking north on an open level.</summary>
        private static FakeWorld fCastWorld()
        {
            FakeWorld lOWorld = fNightWorld();

            lOWorld.Body.Floor = new UWSpot(new UWTilePos(10, 10), 4, 4, 16);

            return lOWorld;
        }

        /// <summary>Confusion (minor 3, creatures only) at 20/20: the creature in the area is
        /// confused and shows the sparkle; an object in the area and a creature outside it are
        /// left.</summary>
        private static void fCheckAreaConfusion()
        {
            FakeWorld lOWorld = fCastWorld();

            lOWorld.AreaTargets.Add(new UWAreaTarget(new UWTilePos(19, 20), true, "A"));
            lOWorld.AreaTargets.Add(new UWAreaTarget(new UWTilePos(20, 20), false, "B"));
            lOWorld.AreaTargets.Add(new UWAreaTarget(new UWTilePos(30, 30), true, "C"));

            fExpectBool("confusion: cast", UWAreaSpellRules.CastAt(3, 0x00, new UWTilePos(20, 20),
                new FakeSpellHost(lOWorld, UWCastContext.ThePlayer)), true);
            fExpectBool("confusion: the targets were collected once, radius 3",
                lOWorld.Log.FindAll(psLine => psLine.StartsWith("collect")).Count == 1
                && lOWorld.Log.Contains("collect around 20/20 radius 3"), true);
            fExpectInt("confusion: the creature in the area is confused and sparkles",
                fIndexOfSequence(lOWorld.Log, "confuse A", "effect 455 at A"), 0);
            fExpectBool("confusion: the object in the area is left", lOWorld.Log.Contains("confuse B"), false);
            fExpectBool("confusion: the creature outside is left", lOWorld.Log.Contains("confuse C"), false);

            // Resisted: it counts, without the picture.
            lOWorld = fCastWorld();
            lOWorld.TargetAnswer = UWTargetSpellRules.Result.Spent;
            lOWorld.AreaTargets.Add(new UWAreaTarget(new UWTilePos(19, 20), true, "A"));
            UWAreaSpellRules.CastAt(3, 0x00, new UWTilePos(20, 20), new FakeSpellHost(lOWorld, UWCastContext.ThePlayer));
            fExpectBool("confusion: resisted, no sparkle", lOWorld.Log.Contains("confuse A")
                && !lOWorld.Log.Contains("effect 455 at A"), true);
        }

        /// <summary>A class 7 spell in the area (process_area's order): of two creatures on one row
        /// the western one takes it, and only one.</summary>
        private static void fCheckTargetSpellInArea()
        {
            FakeWorld lOWorld = fCastWorld();
            FakeSpellHost lOHost = new FakeSpellHost(lOWorld, UWCastContext.ThePlayer);
            UWTilePos lOCentre = UWAreaSpellRules.GetCentreTile(lOHost);

            lOWorld.AreaTargets.Add(new UWAreaTarget(new UWTilePos(lOCentre.X + 1, lOCentre.Y), true, "E"));
            lOWorld.AreaTargets.Add(new UWAreaTarget(new UWTilePos(lOCentre.X - 1, lOCentre.Y), true, "W"));

            fExpectBool("target spell in the area: cast", UWAreaSpellRules.CastTargetSpellInArea(6, lOHost), true);
            fExpectBool("target spell in the area: the western creature takes it",
                lOWorld.Log.Contains("target spell 6 on W") && lOWorld.Log.Contains("effect 455 at W"), true);
            fExpectBool("target spell in the area: exactly one", lOWorld.Log.Contains("target spell 6 on E"), false);
            fExpectInt("target spell in the area: the centre is four tiles north of 10/10", lOCentre.Y, 14);
        }

        /// <summary>Detect Monsters asks the world's creature search: nobody there, "no activity".</summary>
        private static void fCheckDetectOnWorld()
        {
            FakeWorld lOWorld = fCastWorld();

            fExpectBool("detect: cast", UWMiscSpellRules.Track(new FakeSpellHost(lOWorld, UWCastContext.ThePlayer), 0x2D), true);
            fExpectBool("detect: nobody near says so", lOWorld.Shown.Log.Contains("general 63"), true);
        }

        /// <summary>Sheet Lightning (minor 2, tiles) with the lowest rolls: three strokes on the
        /// first open tiles of the area, 6d5 pure magic each, charged to the caster.</summary>
        private static void fCheckSheetLightningCaster()
        {
            FakeWorld lOWorld = fCastWorld();

            UWAreaSpellRules.CastAt(2, 0x40, new UWTilePos(20, 20), new FakeSpellHost(lOWorld, UWCastContext.ThePlayer));

            string lsFirst = "strike 18/18 6d5 type " + UWDamageTypes.Magic + " by " + UWCritterBrain.PlayerIndex;

            fExpectBool("sheet lightning: the player's stroke is his (" + lsFirst + ")", lOWorld.Log.Contains(lsFirst), true);
            fExpectInt("sheet lightning: three strokes with the lowest roll",
                lOWorld.Log.FindAll(psLine => psLine.StartsWith("strike")).Count, 3);
            fExpectInt("sheet lightning: a flash for each", lOWorld.Shown.Log.FindAll(psLine => psLine.StartsWith("effect 453")).Count, 3);

            lOWorld = fCastWorld();
            UWAreaSpellRules.CastAt(2, 0x40, new UWTilePos(20, 20), new FakeSpellHost(lOWorld, UWCastContext.Creature(7)));
            fExpectBool("sheet lightning: a creature's stroke is charged to it",
                lOWorld.Log.Contains("strike 18/18 6d5 type " + UWDamageTypes.Magic + " by 7"), true);
        }

        /// <summary>Tremor: boulders through the world, then the shake and the rumble.</summary>
        private static void fCheckTremorOnWorld()
        {
            FakeWorld lOWorld = fCastWorld();

            fExpectBool("tremor: cast", UWMiscSpellRules.Cast(UWMiscSpellRules.TremorSpell, 0,
                new FakeSpellHost(lOWorld, UWCastContext.ThePlayer)), true);
            fExpectBool("tremor: boulders fall", lOWorld.Log.Exists(psLine => psLine.StartsWith("boulder")), true);
            fExpectInt("tremor: then the shake (0x28) and the rumble (0x12)",
                fIndexOfSequence(lOWorld.Shown.Log, "shake 40", "sound 18"), 0);
        }
    }
}
