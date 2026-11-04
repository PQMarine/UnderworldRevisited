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

        return Toned(UWObjectLightColour.TryGet(pOData, piId, out lfRed, out lfGreen, out lfBlue)
            ? new Color(lfRed, lfGreen, lfBlue)
            : pOFallback);
    }

    public const int ToneWarm = 0;

    public const int ToneCool = 1;

    public const int ToneWhite = 2;

    /// <summary>The cool white the Cooler preset mixes in, half and half.</summary>
    private static readonly Color CoolWhite = new Color(0.82f, 0.9f, 1f);

    private const float CoolShare = 0.5f;

    /// <summary>
    /// THE LIGHT COLOUR PRESET (UWUserSettings.LightTone, per user 2026-11-04): Warm keeps each light
    /// its item's colour; Cooler mixes it half with a cool white; White makes it a neutral white. Both
    /// keep the colour's luminance, so a light does not grow brighter or darker by its tone. The
    /// lava's lights are no items' and keep their red.
    /// </summary>
    public static Color Toned(Color pOColour)
    {
        int liTone = UWUserSettings.LightTone;

        if (liTone != ToneCool && liTone != ToneWhite)
            return pOColour;

        float lfLuminance = fLuminance(pOColour);

        if (liTone == ToneWhite)
            return new Color(lfLuminance, lfLuminance, lfLuminance, pOColour.a);

        Color lOCool = CoolWhite * (lfLuminance / Mathf.Max(fLuminance(CoolWhite), 0.0001f));
        Color lOMixed = Color.Lerp(pOColour, lOCool, CoolShare);

        lOMixed *= lfLuminance / Mathf.Max(fLuminance(lOMixed), 0.0001f);
        lOMixed.a = pOColour.a;

        return lOMixed;
    }

    private static float fLuminance(Color pOColour)
    {
        return (0.2126f * pOColour.r) + (0.7152f * pOColour.g) + (0.0722f * pOColour.b);
    }
}
