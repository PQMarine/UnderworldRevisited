using System;
using System.IO;
using UnityEngine;
using UnderworldRevisited;

/// <summary>
/// The detail levels of the options panel (per user, 2026-09-17). The original's four buttons
/// stay, with a new meaning - the old levels only existed for slow computers:
///
///   LOW        the palette renderer, as close to the original as we get
///   MEDIUM     Remastered without any of its effects - the plain URP look
///   HIGH       Remastered with every effect, at the shipped values
///   VERY HIGH  Remastered with the viewer's own values, set in UWGraphicsOptionsWindow
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

    /// <summary>The effect values - everything the viewer can change in the options window,
    /// plus the tonemapping. Base lighting (ambient floor, wrap lighting, roughness, light
    /// offsets) is not an effect and stays as shipped.</summary>
    [Serializable]
    public class EffectValues
    {
        public bool TorchLights;
        public bool TorchShadows;
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
            pOSettings.RemasterSpriteGlow = SpriteGlow;
            pOSettings.RemasterFireFlicker = FireFlicker;
            pOSettings.RemasterFireJitter = FireJitter;
            pOSettings.RemasterSpriteFlicker = SpriteFlicker;
            pOSettings.RemasterFireSparks = FireSparks;
            pOSettings.RemasterFireSmoke = FireSmoke;
            pOSettings.RemasterDust = Dust;
            pOSettings.RemasterGroundShadow = GroundShadow;
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
    }

    /// <summary>What the file holds.</summary>
    [Serializable]
    private class Profile
    {
        public LevelEnum Level = LevelEnum.High;

        /// <summary>The last level that was not LOW - where F6 goes back to.</summary>
        public LevelEnum LastRemasteredLevel = LevelEnum.High;

        public bool HasCustom;

        public EffectValues Custom;
    }

    private const string FileName = "graphics.json";

    private static Profile msProfile;

    private static EffectValues msShipped;

    private static bool mbValuesOverridden;

    public static LevelEnum CurrentLevel
    {
        get
        {
            EnsureApplied();

            return msProfile != null ? msProfile.Level : LevelEnum.High;
        }
    }

    /// <summary>The shipped effect values, as HIGH shows them.</summary>
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
            // No file yet: the level follows the settings asset, which is what F6 wrote until now.
            msProfile = new Profile
            {
                Level = lOSettings.RenderMode == UWSettings.RenderModeEnum.Palette ? LevelEnum.Low : LevelEnum.High
            };
        }

#if UNITY_EDITOR
        Application.quitting -= fRestoreShippedValues;
        Application.quitting += fRestoreShippedValues;
#endif

        fApplyValues(lOSettings, msProfile.Level);
        lOSettings.RenderMode = msProfile.Level == LevelEnum.Low
            ? UWSettings.RenderModeEnum.Palette : UWSettings.RenderModeEnum.Remastered;
    }

    /// <summary>Switches to a level right away and remembers it.</summary>
    public static void SetLevel(LevelEnum peLevel)
    {
        EnsureApplied();

        UWSettings lOSettings = UWSettings.Instance;

        if (msProfile == null || lOSettings == null)
            return;

        msProfile.Level = peLevel;

        if (peLevel != LevelEnum.Low)
            msProfile.LastRemasteredLevel = peLevel;

        if (peLevel == LevelEnum.VeryHigh && !msProfile.HasCustom)
        {
            msProfile.Custom = msShipped.Copy();
            msProfile.HasCustom = true;
        }

        fApplyValues(lOSettings, peLevel);
        fSetPaletteRenderer(peLevel == LevelEnum.Low);

        Save();
    }

    /// <summary>The viewer's own values, for the options window. Changing them does nothing
    /// until ApplyCustom.</summary>
    public static EffectValues CustomValues
    {
        get
        {
            EnsureApplied();

            if (msProfile == null)
                return null;

            if (!msProfile.HasCustom)
            {
                msProfile.Custom = msShipped.Copy();
                msProfile.HasCustom = true;
            }

            return msProfile.Custom;
        }
    }

    /// <summary>Takes the viewer's values over into the settings - they take effect at once
    /// (UWRemasterRenderer and UWRemasterLights read them every frame).</summary>
    public static void ApplyCustom()
    {
        UWSettings lOSettings = UWSettings.Instance;

        if (msProfile == null || lOSettings == null || msProfile.Level != LevelEnum.VeryHigh)
            return;

        fApplyValues(lOSettings, LevelEnum.VeryHigh);
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

    private static void fApplyValues(UWSettings pOSettings, LevelEnum peLevel)
    {
        switch (peLevel)
        {
            case LevelEnum.Medium:
                msShipped.AllOff().WriteTo(pOSettings);
                mbValuesOverridden = true;
                break;

            case LevelEnum.VeryHigh:
                (msProfile.HasCustom ? msProfile.Custom : msShipped).WriteTo(pOSettings);
                mbValuesOverridden = true;
                break;

            default:
                // LOW keeps the shipped values too, so that F6 back into Remastered is not dark.
                msShipped.WriteTo(pOSettings);
                break;
        }

        pOSettings.RenderMode = peLevel == LevelEnum.Low
            ? UWSettings.RenderModeEnum.Palette : UWSettings.RenderModeEnum.Remastered;
    }

    /// <summary>Switches the palette renderer on or off while the game runs. At the start it
    /// switches itself on from the render mode (UWPaletteRenderToggle.fAutoStart).</summary>
    private static void fSetPaletteRenderer(bool pbOn)
    {
        GameObject lOLevel = UWScene.LevelObject;

        if (lOLevel == null)
            return;

        UWPaletteRenderToggle lOToggle = lOLevel.GetComponent<UWPaletteRenderToggle>();

        if (lOToggle == null)
            lOToggle = lOLevel.AddComponent<UWPaletteRenderToggle>();

        if (lOToggle.IsActive != pbOn)
            lOToggle.Toggle();
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
