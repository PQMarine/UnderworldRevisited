using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The colour help of the menu bar (phase 5, 2026-09-17, per user): the setting, and where it
/// takes hold.
///
/// TWO WAYS, because the interface and the world get their colours from different places:
///
///   INTERFACE AND PALETTE RENDERER go through the palette. The colour table
///   (UWShadePalette.BuildColourTable) and the pictures of the menu screens
///   (UWScreenUi.BuildTexture) are put through the filter while they are built, so paper doll,
///   flasks, inventory, map and the classic 3D view all follow.
///
///   THE REMASTERED WORLD is lit by URP and does not use the palette. There the render
///   pipeline's own channel mixer does the same matrix (UWColourVisionVolume) - the interface
///   above it is already handled by the palette, so nothing is filtered twice.
///
/// Version counts every change; whoever caches colours (the colour table) rebuilds when it
/// differs.
/// </summary>
public static class UWColourVision
{
    private static readonly UWColourFilter mOFilter = new UWColourFilter();

    private static bool mbLoaded;

    public static int Version { get; private set; }

    public static UWColourFilter Filter
    {
        get
        {
            fEnsureLoaded();

            return mOFilter;
        }
    }

    public static UWColourFilter.ModeEnum Mode
    {
        get { return Filter.Mode; }
    }

    public static bool IsActive => Filter.IsActive;

    /// <summary>Brightness and contrast as the settings keep them (1 = unchanged).</summary>
    public static float Brightness => Filter.Brightness;

    public static float Contrast => Filter.Contrast;

    public static float Strength => UWUserSettings.ColourStrength;

    public static void Set(UWColourFilter.ModeEnum peMode, float pfStrength, float pfBrightness, float pfContrast)
    {
        UWUserSettings.ColourMode = (int)peMode;
        UWUserSettings.ColourStrength = pfStrength;
        UWUserSettings.ColourBrightness = pfBrightness;
        UWUserSettings.ColourContrast = pfContrast;
        UWUserSettings.Save();

        mOFilter.Set(peMode, pfStrength, pfBrightness, pfContrast);

        Version++;
    }

    /// <summary>One colour through the filter - for everything built from the palette.</summary>
    public static Color32 Apply(Color32 pOColour)
    {
        fEnsureLoaded();

        if (!mOFilter.IsActive)
            return pOColour;

        byte lyRed = pOColour.r;
        byte lyGreen = pOColour.g;
        byte lyBlue = pOColour.b;

        mOFilter.Apply(ref lyRed, ref lyGreen, ref lyBlue);

        return new Color32(lyRed, lyGreen, lyBlue, pOColour.a);
    }

    private static void fEnsureLoaded()
    {
        if (mbLoaded)
            return;

        mbLoaded = true;

        mOFilter.Set((UWColourFilter.ModeEnum)UWUserSettings.ColourMode, UWUserSettings.ColourStrength,
            UWUserSettings.ColourBrightness, UWUserSettings.ColourContrast);

        Version++;
    }
}
