using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Snapshots the engine-free layer's static state before a check group and puts it back
    /// afterwards, whatever the group did and whether it threw: the random source (a group may
    /// seed it and forget to unseed), the quest flags and game variables, the story flags, the
    /// armour switch and the creature motion's lava quirk. Each group therefore starts from the
    /// same state and no group inherits another's seed or flag.
    /// </summary>
    public sealed class StateGuard
    {
        private readonly IUWRandom mIRandom;
        private readonly Dictionary<int, int> mOQuestFlags;
        private readonly Dictionary<int, int> mOVariables;
        private readonly bool mbTalismansDestroyable;
        private readonly bool mbGaramonBuried;
        private readonly bool mbCupOfWonderFound;
        private readonly bool mbKeyOfTruthGiven;
        private readonly bool mbOrbDestroyed;
        private readonly int miOrbManaBackup;
        private readonly int miSilverTreeLevel;
        private readonly int miMoonstoneLevel;
        private readonly bool mbResistBlowsAddsArmour;

        public StateGuard()
        {
            mIRandom = UWRandom.Source;
            mOQuestFlags = new Dictionary<int, int>(UWQuestFlags.All);
            mOVariables = new Dictionary<int, int>(UWGameVariables.All);
            mbTalismansDestroyable = UWGameFlags.TalismansDestroyable;
            mbGaramonBuried = UWGameFlags.GaramonBuried;
            mbCupOfWonderFound = UWGameFlags.CupOfWonderFound;
            mbKeyOfTruthGiven = UWGameFlags.KeyOfTruthGiven;
            mbOrbDestroyed = UWGameFlags.OrbDestroyed;
            miOrbManaBackup = UWGameFlags.OrbManaBackup;
            miSilverTreeLevel = UWGameFlags.SilverTreeLevel;
            miMoonstoneLevel = UWGameFlags.MoonstoneLevel;
            mbResistBlowsAddsArmour = UWArmourProtection.ResistBlowsAddsArmour;
        }

        public void Restore()
        {
            UWRandom.Use(mIRandom);

            UWQuestFlags.Clear();
            foreach (KeyValuePair<int, int> lOFlag in mOQuestFlags)
                UWQuestFlags.Set(lOFlag.Key, lOFlag.Value);

            UWGameVariables.Clear();
            foreach (KeyValuePair<int, int> lOVariable in mOVariables)
                UWGameVariables.Set(lOVariable.Key, lOVariable.Value);

            UWGameFlags.TalismansDestroyable = mbTalismansDestroyable;
            UWGameFlags.GaramonBuried = mbGaramonBuried;
            UWGameFlags.CupOfWonderFound = mbCupOfWonderFound;
            UWGameFlags.KeyOfTruthGiven = mbKeyOfTruthGiven;
            UWGameFlags.OrbDestroyed = mbOrbDestroyed;
            UWGameFlags.OrbManaBackup = miOrbManaBackup;
            UWGameFlags.SilverTreeLevel = miSilverTreeLevel;
            UWGameFlags.MoonstoneLevel = miMoonstoneLevel;
            UWArmourProtection.ResistBlowsAddsArmour = mbResistBlowsAddsArmour;

            // The lava quirk is a per-session flag that starts cleared; a group that drops it
            // on purpose must not hand it on.
            UWCreatureMotion.ResetHandlers();
        }
    }
}
