using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Hosts of the item use (IUWItemUseHost): the book's, which keeps the messages and the curse,
    /// and the dispatch coverage's.
    /// </summary>
    public static partial class SelfChecks
    {
        private sealed class BookHost : IUWItemUseHost
        {
            public readonly List<string> Messages = new List<string>();
            public readonly List<int> GeneralMessages = new List<int>();
            public int CurseDice;
            public bool Consumed;

            public void ConsumeUsedItem() { Consumed = true; }
            public void AddGeneralMessage(int piIndex) { GeneralMessages.Add(piIndex); }
            public void AddMessage(string psMessage) { Messages.Add(psMessage); }
            public void CursePlayer(int piDice) { CurseDice = piDice; }
            public int PlayerDamageTypeProof { get { return 0; } }
            public void FlashWindow(int piPaletteColour, float pfSeconds) { }
            public void PlayCutscene(int piCutscene) { }
            public bool CastSpellFromObject(int piSpellIndex) { return false; }
            public bool CastSpellByClass(int piMajorClass, int piMinorClass) { return false; }
            public bool IsWandCoolingDown { get { return false; } }
            public void StartWandCooldown() { }
            public void PlayMagicItemRefused() { }
            public bool TrySleep() { return false; }
            public void SleepPassedOut() { }
            public bool TryPlantSeed() { return false; }
            public bool IsInstrument(int piObjectId) { return false; }
            public bool PlayInstrument(UWObject pOItem) { return false; }
            public bool TryFish() { return false; }
        }

        /// <summary>A host that only answers the instrument question - the instruments are the
        /// Unity side's (UWInstrumentPlayer.IsInstrument, mandolin 291 and flute 292).</summary>
        private sealed class CoverageUseHost : IUWItemUseHost
        {
            public void ConsumeUsedItem() { }
            public void AddGeneralMessage(int piIndex) { }
            public void AddMessage(string psMessage) { }
            public void CursePlayer(int piDice) { }
            public int PlayerDamageTypeProof { get { return 0; } }
            public void FlashWindow(int piPaletteColour, float pfSeconds) { }
            public void PlayCutscene(int piCutscene) { }
            public bool CastSpellFromObject(int piSpellIndex) { return false; }
            public bool CastSpellByClass(int piMajorClass, int piMinorClass) { return false; }
            public bool IsWandCoolingDown { get { return false; } }
            public void StartWandCooldown() { }
            public void PlayMagicItemRefused() { }
            public bool TrySleep() { return false; }
            public void SleepPassedOut() { }
            public bool TryPlantSeed() { return false; }
            public bool IsInstrument(int piObjectId) { return piObjectId == CoverageMandolinId || piObjectId == CoverageFluteId; }
            public bool PlayInstrument(UWObject pOItem) { return false; }
            public bool TryFish() { return false; }
        }
    }
}
