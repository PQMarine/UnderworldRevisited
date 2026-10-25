using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
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
    }
}
