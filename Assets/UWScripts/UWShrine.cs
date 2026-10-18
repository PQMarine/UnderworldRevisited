using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The shrine on the Unity side. The mantras and the skill raising are engine-free in
/// UWShrineRules since 2026-09-18 (P3 of the engine separation); this class keeps the
/// old entry points for Interaction. Since 2026-09-22 the two QUEST MANTRAS ask where the player
/// stands, the two bits of PLAYER.DAT 0x60 and a hand to put the Key of Truth in - since cleanup
/// step 3.6 (2026-10-17) straight from the world (UWWorld) and UWGameFlags; the shrine's own host
/// (UWShrineRules.IHost) is gone.
/// </summary>
public static class UWShrine
{
    /// <summary>Object number of the shrine.</summary>
    public const int ShrineObjectId = UWShrineRules.ShrineObjectId;

    /// <summary>What is entered at the shrine - the wording comes from UW.EXE.</summary>
    public const string Prompt = UWShrineRules.Prompt;

    /// <summary>Accepts a chanted mantra - see UWShrineRules.Chant. The world is the scene's (null
    /// without a level loader; then the quest mantras say nothing).</summary>
    public static void Chant(string psMantra, UWCharacter pOCharacter, Interaction pOInteraction,
        DataImport pOData)
    {
        if (pOCharacter == null || pOInteraction == null || pOData == null)
            return;

        UWShrineRules.Chant(psMantra, pOCharacter.Vitals, pOData,
            pOInteraction.AddGeneralMessage, pOInteraction.AddMessage, UWScene.World);
    }
}
