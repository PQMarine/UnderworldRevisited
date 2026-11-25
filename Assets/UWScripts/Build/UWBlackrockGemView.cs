using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE LARGE BLACKROCK GEM'S COLOURS IN THE SCENE (UWBlackrockGemLook, per user 2026-11-16): the colour the game gives a
/// register of the model now - the used facets glowing, the facet the gem shows, the body pulsing on the game clock, and the
/// blinking once every gem is used. UWModelColourView repaints the model with it (until 2026-11-22 this class was that
/// view, for the gem alone).
/// </summary>
public static class UWBlackrockGemView
{
    /// <summary>The colour the game gives a register now, -1 when the gem does not set it: the quest byte of the used gems
    /// and the facet shown from the second game's player record.</summary>
    public static int ColourNow(int piRegister)
    {
        UWPlayerFile lOFile = UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;
        int liUsed = lOFile != null
            ? UWPlayerRecordUw2.GetNumbered(lOFile, UWTrapRulesUw2.FirstQuestByte + UWTrapRulesUw2.GemsUsedQuestByte) : 0;
        int liShown = lOFile != null ? UWPlayerRecordUw2.GetNumbered(lOFile, UWTrapRulesUw2.GemFacetVariable) : 0;

        return UWBlackrockGemLook.Colour(piRegister, liUsed, liShown, UWModelColourView.GameTime);
    }
}
