using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The light colour of an item as a Unity Color, for the placed lights (UWRemasterLights) and the
/// carried light of the player (UWLighting), so that both match each other. The colour itself is
/// worked out engine-free in UWObjectLightColour (since 2026-09-17, P1 of the engine separation);
/// an image without colour information gives the caller's fallback.
/// </summary>
public static class UWLightColour
{
    /// <summary>The warm fire colour for an image without colour information
    /// (UWLightSourceTable.Fallback*), one value for the placed lights and the carried item.</summary>
    public static readonly Color Fallback = new Color(UWLightSourceTable.FallbackRed,
        UWLightSourceTable.FallbackGreen, UWLightSourceTable.FallbackBlue);

    public static Color FromObject(DataImport pOData, int piId, Color pOFallback)
    {
        float lfRed, lfGreen, lfBlue;

        return UWObjectLightColour.TryGet(pOData, piId, out lfRed, out lfGreen, out lfBlue)
            ? new Color(lfRed, lfGreen, lfBlue)
            : pOFallback;
    }
}
