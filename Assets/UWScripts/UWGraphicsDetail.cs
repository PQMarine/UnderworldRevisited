using System;
using System.IO;
using UnityEngine;
using UnderworldRevisited;

/// <summary>
/// HOW THE WORLD IS DRAWN, ON TWO AXES (per user, 2026-10-30: the four levels mixed which mode with
/// how many effects, and every step was set differently):
///
///   MODE       Palette, the palette renderer, as close to the original as we get; Palette colours,
///              the Remastered lighting through the original's light table (UWHallucination.hlsl); and
///              Remastered, the URP look
///   EFFECTS    Off, Subtle, Full, Own, in every mode alike. Palette keeps them as the sliders of its
///              own effects (UWPaletteEffects); the two others as effect values, Own the viewer's, set
///              in the effects window (UWEffectsScreen). Each of the two remembers its own.
///
/// The detail levels of the options panel (per user, 2026-09-17) stay as names of four pairs, for the
/// original's four buttons and the modern game menu (LevelEnum, CurrentLevel, SetLevel):
///
///   LOW        Palette
///   MEDIUM     Palette colours, with the effects it holds (per user, 2026-10-30)
///   HIGH       Remastered, Full
///   VERY HIGH  Remastered, Own
///
/// WHERE IT IS KEPT: UWSettings is a ScriptableObject in Resources, and a build never writes it
/// back - so the choice lives in its own file under Application.persistentDataPath, read once
/// per run and applied onto the settings. The values HIGH stands for are the settings as loaded,
/// captured before anything is applied.
///
/// IN THE EDITOR changes to the settings object at runtime end up in the asset. MEDIUM and VERY
/// HIGH overwrite the effect values, so they are put back when play mode ends - the file keeps
/// the choice, the asset keeps the shipped values.
/// </summary>
public static class UWGraphicsDetail
{
    public enum LevelEnum
    {
        Low = 0,
        Medium = 1,
        High = 2,
        VeryHigh = 3
    }

    public enum ModeEnum
    {
        Palette = 0,
        PaletteColours = 1,
        Remastered = 2
    }

    /// <summary>In the order and the numbers of UWPaletteEffects.PresetEnum.</summary>
    public enum EffectsEnum
    {
        Off = 0,
        Subtle = 1,
        Full = 2,
        Own = 3
    }

    /// <summary>The effect values - everything the viewer can change in the options window,
    /// plus the tonemapping. Base lighting (ambient floor, wrap lighting, roughness, light
    /// offsets) is not an effect and stays as shipped.</summary>
    [Serializable]
    public class EffectValues
    {
        public bool TorchLights;
        public bool TorchShadows;
        public float ShadowBrightness;
        public int MaxShadowLights;
        public bool LavaLights;
        public float LightBrightness;

        public float NormalStrength;
        public float SpecularStrength;
        public float ParallaxDepth;
        public float SelfShadow;
        public float Cavity;
        public float EmissiveStrength;

        public float BloomIntensity;
        public bool AmbientOcclusion;
        public UWSettings.RemasterTonemappingEnum Tonemapping;
        public bool Demaster;

        public float SpriteGlow;
        public float FireFlicker;
        public float FireJitter;
        public float SpriteFlicker;
        public float FireSparks;
        public float FireSmoke;
        public float Dust;
        public float GroundShadow;

        public static EffectValues ReadFrom(UWSettings pOSettings)
        {
            return new EffectValues
            {
                TorchLights = pOSettings.RemasterTorchLights,
                TorchShadows = pOSettings.RemasterTorchShadows,
                ShadowBrightness = pOSettings.RemasterShadowBrightness,
                MaxShadowLights = pOSettings.RemasterMaxShadowLights,
                LavaLights = pOSettings.RemasterLavaLights,
                LightBrightness = pOSettings.RemasterLightBrightness,
                NormalStrength = pOSettings.RemasterNormalStrength,
                SpecularStrength = pOSettings.RemasterSpecularStrength,
                ParallaxDepth = pOSettings.RemasterParallaxDepth,
                SelfShadow = pOSettings.RemasterSelfShadow,
                Cavity = pOSettings.RemasterCavity,
                EmissiveStrength = pOSettings.RemasterEmissiveStrength,
                BloomIntensity = pOSettings.RemasterBloomIntensity,
                AmbientOcclusion = pOSettings.RemasterAmbientOcclusion,
                Tonemapping = pOSettings.RemasterTonemapping,
                Demaster = pOSettings.RemasterDemaster,
                SpriteGlow = pOSettings.RemasterSpriteGlow,
                FireFlicker = pOSettings.RemasterFireFlicker,
                FireJitter = pOSettings.RemasterFireJitter,
                SpriteFlicker = pOSettings.RemasterSpriteFlicker,
                FireSparks = pOSettings.RemasterFireSparks,
                FireSmoke = pOSettings.RemasterFireSmoke,
                Dust = pOSettings.RemasterDust,
                GroundShadow = pOSettings.RemasterGroundShadow
            };
        }

        public void WriteTo(UWSettings pOSettings)
        {
            pOSettings.RemasterTorchLights = TorchLights;
            pOSettings.RemasterTorchShadows = TorchShadows;
            pOSettings.RemasterShadowBrightness = ShadowBrightness;
            pOSettings.RemasterMaxShadowLights = MaxShadowLights;
            pOSettings.RemasterLavaLights = LavaLights;
            pOSettings.RemasterLightBrightness = LightBrightness;
            pOSettings.RemasterNormalStrength = NormalStrength;
            pOSettings.RemasterSpecularStrength = SpecularStrength;
            pOSettings.RemasterParallaxDepth = ParallaxDepth;
            pOSettings.RemasterSelfShadow = SelfShadow;
            pOSettings.RemasterCavity = Cavity;
            pOSettings.RemasterEmissiveStrength = EmissiveStrength;
            pOSettings.RemasterBloomIntensity = BloomIntensity;
            pOSettings.RemasterAmbientOcclusion = AmbientOcclusion;
            pOSettings.RemasterTonemapping = Tonemapping;
            pOSettings.RemasterDemaster = Demaster;
            pOSettings.RemasterSpriteGlow = SpriteGlow;
            pOSettings.RemasterFireFlicker = FireFlicker;
            pOSettings.RemasterFireJitter = FireJitter;
            pOSettings.RemasterSpriteFlicker = SpriteFlicker;
            pOSettings.RemasterFireSparks = FireSparks;
            pOSettings.RemasterFireSmoke = FireSmoke;
            pOSettings.RemasterDust = Dust;
            pOSettings.RemasterGroundShadow = GroundShadow;

            pOSettings.MarkChanged();
        }

        public EffectValues Copy()
        {
            return (EffectValues)MemberwiseClone();
        }

        /// <summary>The same values with every effect switched off - MEDIUM. Light brightness
        /// stays: without the extra lights it has nothing left to scale.</summary>
        public EffectValues AllOff()
        {
            EffectValues lOOff = Copy();

            lOOff.TorchLights = false;
            lOOff.TorchShadows = false;
            lOOff.LavaLights = false;
            lOOff.NormalStrength = 0f;
            lOOff.SpecularStrength = 0f;
            lOOff.ParallaxDepth = 0f;
            lOOff.SelfShadow = 0f;
            lOOff.Cavity = 0f;
            lOOff.EmissiveStrength = 0f;
            lOOff.BloomIntensity = 0f;
            lOOff.AmbientOcclusion = false;
            lOOff.Tonemapping = UWSettings.RemasterTonemappingEnum.Off;
            lOOff.SpriteGlow = 0f;
            lOOff.FireFlicker = 0f;
            lOOff.FireJitter = 0f;
            lOOff.SpriteFlicker = 0f;
            lOOff.FireSparks = 0f;
            lOOff.FireSmoke = 0f;
            lOOff.Dust = 0f;
            lOOff.GroundShadow = 0f;

            return lOOff;
        }

        /// <summary>SUBTLE: the lights and their shadows as shipped, everything laid over them at half
        /// strength, no occlusion.</summary>
        public EffectValues Subtle()
        {
            EffectValues lOHalf = Copy();

            lOHalf.NormalStrength *= 0.5f;
            lOHalf.SpecularStrength *= 0.5f;
            lOHalf.ParallaxDepth *= 0.5f;
            lOHalf.SelfShadow *= 0.5f;
            lOHalf.Cavity *= 0.5f;
            lOHalf.EmissiveStrength *= 0.5f;
            lOHalf.BloomIntensity *= 0.5f;
            lOHalf.AmbientOcclusion = false;
            lOHalf.SpriteGlow *= 0.5f;
            lOHalf.FireFlicker *= 0.5f;
            lOHalf.FireJitter *= 0.5f;
            lOHalf.SpriteFlicker *= 0.5f;
            lOHalf.FireSparks *= 0.5f;
            lOHalf.FireSmoke *= 0.5f;
            lOHalf.Dust *= 0.5f;
            lOHalf.GroundShadow *= 0.5f;

            return lOHalf;
        }
    }

    /// <summary>What the file holds.</summary>
    [Serializable]
    private class Profile
    {
        public LevelEnum Level = LevelEnum.High;

        /// <summary>The last level that was not LOW - the one a switch back from the palette path returns to.</summary>
        public LevelEnum LastRemasteredLevel = LevelEnum.High;

        public bool HasCustom;

        public EffectValues Custom;

        /// <summary>Set once the two axes are in the file; a file from before them is read from Level
        /// (fFromLevel).</summary>
        public bool HasAxes;

        public ModeEnum Mode = ModeEnum.Remastered;

        public EffectsEnum RemasteredEffects = EffectsEnum.Full;

        public EffectsEnum ColoursEffects = EffectsEnum.Full;

        /// <summary>Own in Palette colours; Custom is Remastered's.</summary>
        public bool HasCustomColours;

        public EffectValues CustomColours;
    }

    private const string FileName = "graphics.json";

    private static Profile msProfile;

    private static EffectValues msShipped;

    private static bool mbValuesOverridden;

    public static ModeEnum Mode
    {
        get
        {
            EnsureApplied();

            return msProfile != null ? msProfile.Mode : ModeEnum.Remastered;
        }
    }

    /// <summary>The effects of the mode in force.</summary>
    public static EffectsEnum Effects
    {
        get
        {
            EnsureApplied();

            if (msProfile == null)
                return EffectsEnum.Full;

            switch (msProfile.Mode)
            {
                case ModeEnum.Palette: return (EffectsEnum)(int)UWPaletteEffects.CurrentPreset;
                case ModeEnum.PaletteColours: return msProfile.ColoursEffects;
                default: return msProfile.RemasteredEffects;
            }
        }
    }

    /// <summary>The four buttons' name of the pair in force. Remastered's Off and Subtle have none:
    /// they show as High.</summary>
    public static LevelEnum CurrentLevel
    {
        get
        {
            ModeEnum leMode = Mode;

            if (leMode == ModeEnum.Palette)
                return LevelEnum.Low;

            if (leMode == ModeEnum.PaletteColours)
                return LevelEnum.Medium;

            return Effects == EffectsEnum.Own ? LevelEnum.VeryHigh : LevelEnum.High;
        }
    }

    /// <summary>The shipped effect values, as Full shows them.</summary>
    public static EffectValues ShippedValues
    {
        get
        {
            EnsureApplied();

            return msShipped != null ? msShipped.Copy() : null;
        }
    }

    /// <summary>
    /// Reads the file once per run and applies the stored level onto the settings. Called by the
    /// level loader at its start - before the palette renderer looks at the render mode.
    /// </summary>
    public static void EnsureApplied()
    {
        if (msProfile != null || !Application.isPlaying)
            return;

        UWSettings lOSettings = UWSettings.Instance;

        if (lOSettings == null)
            return;

        msShipped = EffectValues.ReadFrom(lOSettings);
        msProfile = fLoad();

        if (msProfile == null)
        {
            // No file yet: the level follows the settings asset, which is what the old F6 switch wrote.
            msProfile = new Profile
            {
                Level = lOSettings.RenderMode == UWSettings.RenderModeEnum.Palette ? LevelEnum.Low : LevelEnum.High
            };
        }

        if (!msProfile.HasAxes)
            fFromLevel(msProfile);

#if UNITY_EDITOR
        Application.quitting -= fRestoreShippedValues;
        Application.quitting += fRestoreShippedValues;
#endif

        fApplyValues(lOSettings);
    }

    /// <summary>A file from before the two axes. Very High with its Demaster switch on was Palette
    /// colours in all but name: those values become that mode's own, the switch itself is the mode now.</summary>
    private static void fFromLevel(Profile pOProfile)
    {
        pOProfile.HasAxes = true;
        pOProfile.Mode = pOProfile.Level == LevelEnum.Low ? ModeEnum.Palette : ModeEnum.Remastered;
        pOProfile.RemasteredEffects = pOProfile.Level == LevelEnum.Medium ? EffectsEnum.Off
            : pOProfile.Level == LevelEnum.VeryHigh ? EffectsEnum.Own : EffectsEnum.Full;

        if (pOProfile.HasCustom && pOProfile.Custom.Demaster)
        {
            pOProfile.CustomColours = pOProfile.Custom.Copy();
            pOProfile.HasCustomColours = true;
            pOProfile.Custom.Demaster = false;
            pOProfile.ColoursEffects = EffectsEnum.Own;

            if (pOProfile.Level == LevelEnum.VeryHigh)
            {
                pOProfile.Mode = ModeEnum.PaletteColours;
                pOProfile.RemasteredEffects = EffectsEnum.Full;
            }
        }
    }

    /// <summary>Switches the mode right away and remembers it.</summary>
    public static void SetMode(ModeEnum peMode)
    {
        EnsureApplied();

        if (msProfile == null)
            return;

        msProfile.Mode = peMode;
        fApplyNow();
    }

    /// <summary>The effects of the mode in force, right away. Own in the palette mode is what its
    /// sliders hold, nothing to choose.</summary>
    public static void SetEffects(EffectsEnum peEffects)
    {
        EnsureApplied();

        if (msProfile == null)
            return;

        switch (msProfile.Mode)
        {
            case ModeEnum.Palette:
                UWPaletteEffects.ApplyPreset((UWPaletteEffects.PresetEnum)(int)peEffects);
                return;

            case ModeEnum.PaletteColours:
                msProfile.ColoursEffects = peEffects;
                break;

            default:
                msProfile.RemasteredEffects = peEffects;
                break;
        }

        fApplyNow();
    }

    /// <summary>Switches to the pair of one of the four buttons and remembers it.</summary>
    public static void SetLevel(LevelEnum peLevel)
    {
        EnsureApplied();

        if (msProfile == null)
            return;

        switch (peLevel)
        {
            case LevelEnum.Low:
                msProfile.Mode = ModeEnum.Palette;
                break;

            case LevelEnum.Medium:
                msProfile.Mode = ModeEnum.PaletteColours;
                break;

            case LevelEnum.High:
                msProfile.Mode = ModeEnum.Remastered;
                msProfile.RemasteredEffects = EffectsEnum.Full;
                break;

            default:
                msProfile.Mode = ModeEnum.Remastered;
                msProfile.RemasteredEffects = EffectsEnum.Own;
                break;
        }

        fApplyNow();
    }

    private static void fApplyNow()
    {
        UWSettings lOSettings = UWSettings.Instance;

        if (lOSettings == null)
            return;

        fEnsureOwn();
        fApplyValues(lOSettings);
        fSetPaletteRenderer(msProfile.Mode == ModeEnum.Palette);
        msProfile.Level = CurrentLevel;

        Save();
    }

    /// <summary>The viewer's own values start as the shipped ones, the first time they are asked for.</summary>
    private static void fEnsureOwn()
    {
        if (msProfile.Mode == ModeEnum.Remastered && msProfile.RemasteredEffects == EffectsEnum.Own && !msProfile.HasCustom)
        {
            msProfile.Custom = msShipped.Copy();
            msProfile.HasCustom = true;
        }

        if (msProfile.Mode == ModeEnum.PaletteColours && msProfile.ColoursEffects == EffectsEnum.Own && !msProfile.HasCustomColours)
        {
            msProfile.CustomColours = msShipped.Copy();
            msProfile.HasCustomColours = true;
        }
    }

    /// <summary>
    /// THE ONE PATH A DETAIL LEVEL IS CHOSEN BY (cleanup step 8.8, 2026-10-24): the bar's Graphics
    /// menu (UWSetupMenu) and the classic options' detail screen (UWHudOptions) each set the level
    /// and opened the effects screen for VERY HIGH on their own until then. Sets the level, lets the
    /// caller show its own state anew (the options panel draws its detail screen again; the bar
    /// has nothing to draw), and for VERY HIGH opens the effects window as the caller hands it over -
    /// since cleanup 8d.3 the setup bar's one window for both (UWSetupMenu.OpenEffectsWindow; the
    /// Graphics window's "Remastered effects...", the classic options' Very High); null opens none
    /// (the Graphics window's Low, Medium and High). The modern game menu's Detail page has no effects
    /// window and sets the level alone (SetLevel).
    /// </summary>
    public static void Choose(LevelEnum peLevel, Action pFShowLevel, Action pFOpenEffects)
    {
        SetLevel(peLevel);

        if (pFShowLevel != null)
            pFShowLevel();

        if (peLevel == LevelEnum.VeryHigh && pFOpenEffects != null)
            pFOpenEffects();
    }

    /// <summary>The viewer's own values of the mode in force (Remastered's while the palette mode is
    /// in force), for the options window. Changing them does nothing until ApplyCustom.</summary>
    public static EffectValues CustomValues
    {
        get
        {
            EnsureApplied();

            if (msProfile == null)
                return null;

            if (msProfile.Mode == ModeEnum.PaletteColours)
            {
                if (!msProfile.HasCustomColours)
                {
                    msProfile.CustomColours = msShipped.Copy();
                    msProfile.HasCustomColours = true;
                }

                return msProfile.CustomColours;
            }

            if (!msProfile.HasCustom)
            {
                msProfile.Custom = msShipped.Copy();
                msProfile.HasCustom = true;
            }

            return msProfile.Custom;
        }
    }

    /// <summary>Takes the viewer's values over into the settings - they take effect at once
    /// (UWRemasterRenderPath and its lights read them every frame, the material values when the
    /// settings' revision moved).</summary>
    public static void ApplyCustom()
    {
        UWSettings lOSettings = UWSettings.Instance;

        if (msProfile == null || lOSettings == null || Effects != EffectsEnum.Own || msProfile.Mode == ModeEnum.Palette)
            return;

        fApplyValues(lOSettings);
    }

    public static void Save()
    {
        if (msProfile == null)
            return;

        try
        {
            File.WriteAllText(fGetPath(), JsonUtility.ToJson(msProfile, true));
        }
        catch (Exception lOError)
        {
            Debug.LogWarning("Graphics detail not saved: " + lOError.Message);
        }
    }

    /// <summary>The values of the mode and effects in force. The palette mode keeps the shipped ones
    /// too, so that a switch back into the URP path is not dark.</summary>
    private static void fApplyValues(UWSettings pOSettings)
    {
        ModeEnum leMode = msProfile.Mode;
        EffectsEnum leEffects = leMode == ModeEnum.PaletteColours ? msProfile.ColoursEffects : msProfile.RemasteredEffects;
        EffectValues lOOwn = leMode == ModeEnum.PaletteColours
            ? (msProfile.HasCustomColours ? msProfile.CustomColours : msShipped)
            : (msProfile.HasCustom ? msProfile.Custom : msShipped);
        EffectValues lOValues = leMode == ModeEnum.Palette || leEffects == EffectsEnum.Full ? msShipped.Copy()
            : leEffects == EffectsEnum.Off ? msShipped.AllOff()
            : leEffects == EffectsEnum.Subtle ? msShipped.Subtle()
            : lOOwn.Copy();

        // The mode decides this, whatever a set of values holds.
        lOValues.Demaster = leMode == ModeEnum.PaletteColours;
        lOValues.WriteTo(pOSettings);
        mbValuesOverridden = true;

        pOSettings.RenderMode = leMode == ModeEnum.Palette
            ? UWSettings.RenderModeEnum.Palette : UWSettings.RenderModeEnum.Remastered;
    }

    /// <summary>Switches the palette renderer on or off while the game runs, through the one
    /// owner of the render mode (UWRenderPathHost, cleanup step 6.5, 2026-10-24). At the start the
    /// host follows the render mode by itself.</summary>
    private static void fSetPaletteRenderer(bool pbOn)
    {
        UWRenderPathHost.Switch(pbOn ? UWSettings.RenderModeEnum.Palette : UWSettings.RenderModeEnum.Remastered);
    }

    private static Profile fLoad()
    {
        try
        {
            string lsPath = fGetPath();

            if (!File.Exists(lsPath))
                return null;

            Profile lOProfile = JsonUtility.FromJson<Profile>(File.ReadAllText(lsPath));

            if (lOProfile != null && lOProfile.HasCustom && lOProfile.Custom == null)
                lOProfile.HasCustom = false;

            if (lOProfile != null && lOProfile.HasCustomColours && lOProfile.CustomColours == null)
                lOProfile.HasCustomColours = false;

            return lOProfile;
        }
        catch (Exception lOError)
        {
            Debug.LogWarning("Graphics detail not read: " + lOError.Message);

            return null;
        }
    }

    private static string fGetPath()
    {
        return Path.Combine(Application.persistentDataPath, FileName);
    }

#if UNITY_EDITOR
    /// <summary>Puts the shipped values back into the settings asset when play mode ends, and
    /// forgets the profile so the next play reads the file again.</summary>
    private static void fRestoreShippedValues()
    {
        Application.quitting -= fRestoreShippedValues;

        UWSettings lOSettings = UWSettings.Instance;

        if (lOSettings != null && msShipped != null && mbValuesOverridden)
            msShipped.WriteTo(lOSettings);

        msProfile = null;
        msShipped = null;
        mbValuesOverridden = false;
    }
#endif
}
