using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// THE PALETTE RENDERER'S OWN EFFECTS (per user, 2026-10-07: "Mach alle Grafikeffekte die Sinn
/// machen im Palette Renderer"; the plan in ROADMAP, "Modern effects in the Palette mode"). Each
/// one only moves the shade level before the palette lookup, so the picture keeps the palette's
/// colours (UWPaletteEffects.hlsl). Each has its own strength in the user's settings, 0 = off, and
/// all of them are off by default: the palette path stays the original's until the player asks.
///
///   ambient occlusion  URP's screen-space occlusion, which the Remastered mode uses as well, in
///                      shade levels (_UWPaletteAO).
///   grime              darker levels where a wall or a step up meets the floor, wandering in
///                      width like piled dirt, from the tile map (_UWPaletteGrime,
///                      UWPaletteEffects.hlsl).
///   depth              the Remastered parallax on whole texels, with a self shadow in levels
///                      (_UWPaletteDepth).
///   hollows            the Remastered height array as levels: the joints darker (_UWPaletteCavity);
///                      its relief (_UWPaletteRelief) is built but not offered - see fUpdateRelief.
///   ground shadows     darker levels under what stands in the world as a sprite, the nearest 64
///                      (_UWPaletteShadows).
///   glow               lava and the light sources' bright pixels keep their full colour
///                      (_UWPaletteGlow, _UWGlowSprite on the light sources' renderers).
///   light sources      the Remastered mode's list of glowing things (UWRemasterLights) and every
///                      lava tile, each lit like a carried light of its own level by the
///                      original's SHADES.DAT table, walls casting shadows (UWPaletteLightMap,
///                      _UWPaletteLightMap); fire flickers. Within the player's own view distance
///                      only (per user: a source must not let one see further than the original).
///
/// Set every LateUpdate while the palette path is on; in Remastered the effects are off here and
/// the occlusion feature belongs to UWRemasterRenderer. Created once and kept across scene loads.
/// </summary>
public class UWPaletteEffects : MonoBehaviour
{
    /// <summary>The most levels ambient occlusion may darken by.</summary>
    public const float MaxAmbientOcclusion = 6f;

    private static readonly int msAmbientOcclusionId = Shader.PropertyToID("_UWPaletteAO");

    private static readonly int msLightsId = Shader.PropertyToID("_UWPaletteLights");

    private static readonly int msGrimeId = Shader.PropertyToID("_UWPaletteGrime");

    private static readonly int msGrimeTintId = Shader.PropertyToID("_UWGrimeTint");

    private static readonly int msGrimeToneColourId = Shader.PropertyToID("_UWGrimeToneColour");

    private static readonly int msReliefId = Shader.PropertyToID("_UWPaletteRelief");

    private static readonly int msCavityId = Shader.PropertyToID("_UWPaletteCavity");

    private static readonly int msNormalArrayId = Shader.PropertyToID("_UWPaletteNormalArray");

    private static readonly int msDepthId = Shader.PropertyToID("_UWPaletteDepth");

    private static readonly int msParallaxId = Shader.PropertyToID("_UWPaletteParallax");

    private static readonly int msSelfShadowId = Shader.PropertyToID("_UWPaletteSelfShadow");


    private static readonly int msGlowId = Shader.PropertyToID("_UWPaletteGlow");

    private static readonly int msGlowSpriteId = Shader.PropertyToID("_UWGlowSprite");

    /// <summary>The renderers marked as glowing light sources, to unmark them again.</summary>
    private readonly List<Renderer> mOGlowing = new List<Renderer>();

    private MaterialPropertyBlock mOGlowBlock;

    private float mfNextGlowScan;

    private bool mbGlowMarked;

    /// <summary>The most levels relief and hollows may move by.</summary>
    public const float MaxRelief = 6f;

    /// <summary>The most the depth may be. 3 is the Remastered depth itself, and beyond it the
    /// parallax breaks up (per user, 2026-10-07: "Depth bei Palette sieht bei mehr als 3 fehlerhaft
    /// aus") - so the presets stay at or below 3, but Own reaches 6 like every other slider (per
    /// user the same day: "bei Own alles bis 6 einstellbar").</summary>
    public const float MaxDepth = 6f;

    /// <summary>
    /// THE PRESETS (per user, 2026-10-07: not to overwhelm with options - values proposed, the user
    /// unsure which would fit): Off as the original, Subtle near it without new light, Full with
    /// everything. Own is whatever the sliders hold otherwise.
    /// </summary>
    public enum PresetEnum
    {
        Off,
        Subtle,
        Full,
        Own
    }

    private struct Preset
    {
        public float Occlusion;
        public float Grime;
        public int GrimeTone;
        public float Depth;
        public float Hollows;
        public float Shadows;
        public bool Glow;
        public bool Lights;
    }

    private static readonly Preset[] msPresets =
    {
        new Preset { Occlusion = 0f, Grime = 0f, GrimeTone = 1, Depth = 0f, Hollows = 0f, Shadows = 0f, Glow = false, Lights = false },
        new Preset { Occlusion = 1.5f, Grime = 2f, GrimeTone = 1, Depth = 1.5f, Hollows = 1.5f, Shadows = 2f, Glow = true, Lights = false },
        new Preset { Occlusion = 3f, Grime = 3f, GrimeTone = 1, Depth = 3f, Hollows = 3f, Shadows = 3f, Glow = true, Lights = true },
    };

    /// <summary>Sets every palette effect to a preset and saves (Own: nothing changes).</summary>
    public static void ApplyPreset(PresetEnum pePreset)
    {
        if (pePreset == PresetEnum.Own)
            return;

        Preset lOPreset = msPresets[(int)pePreset];

        UWUserSettings.PaletteAmbientOcclusion = lOPreset.Occlusion;
        UWUserSettings.PaletteGrime = lOPreset.Grime;
        UWUserSettings.PaletteGrimeTone = lOPreset.GrimeTone;
        UWUserSettings.PaletteDepth = lOPreset.Depth;
        UWUserSettings.PaletteCavity = lOPreset.Hollows;
        UWUserSettings.PaletteGroundShadows = lOPreset.Shadows;
        UWUserSettings.PaletteGlow = lOPreset.Glow;
        UWUserSettings.PaletteLightSources = lOPreset.Lights;
        UWUserSettings.Save();
    }

    /// <summary>Which preset the sliders hold now (Own if none).</summary>
    public static PresetEnum CurrentPreset
    {
        get
        {
            for (int liAt = 0; liAt < msPresets.Length; liAt++)
            {
                Preset lOPreset = msPresets[liAt];

                if (Mathf.Approximately(UWUserSettings.PaletteAmbientOcclusion, lOPreset.Occlusion)
                    && Mathf.Approximately(UWUserSettings.PaletteGrime, lOPreset.Grime)
                    && (lOPreset.Grime <= 0f || UWUserSettings.PaletteGrimeTone == lOPreset.GrimeTone)
                    && Mathf.Approximately(UWUserSettings.PaletteDepth, lOPreset.Depth)
                    && Mathf.Approximately(UWUserSettings.PaletteCavity, lOPreset.Hollows)
                    && Mathf.Approximately(UWUserSettings.PaletteGroundShadows, lOPreset.Shadows)
                    && UWUserSettings.PaletteGlow == lOPreset.Glow
                    && UWUserSettings.PaletteLightSources == lOPreset.Lights)
                    return (PresetEnum)liAt;
            }

            return PresetEnum.Own;
        }
    }

    /// <summary>The normal and height array, built the first time relief or hollows are on.</summary>
    private Texture2DArray mONormalArray;

    private static readonly int msGrimeTintTableId = Shader.PropertyToID("_UWGrimeTintTable");

    /// <summary>The grime's colour tables, one row per tone (UWGrimeTint), built once.</summary>
    private Texture2D mOGrimeTintTable;

    private static readonly int msShadowsId = Shader.PropertyToID("_UWPaletteShadows");

    private static readonly int msShadowCountId = Shader.PropertyToID("_UWPaletteShadowCount");

    private static readonly int msShadowStrengthId = Shader.PropertyToID("_UWPaletteShadowStrength");

    /// <summary>The most levels a ground shadow may darken by.</summary>
    public const float MaxGroundShadows = 6f;

    /// <summary>As many casters as the shader takes (UW_GROUND_SHADOWS).</summary>
    private const int MaxShadowCasters = 64;

    /// <summary>The smallest blob's diameter - a coin or a key would get a barely visible dot
    /// (as UWRemasterGroundShadows).</summary>
    private const float MinShadowDiameter = 10f;

    private readonly List<Transform> mOCasters = new List<Transform>();

    private readonly List<float> mOCasterRadius = new List<float>();

    private readonly Vector4[] mOShadowData = new Vector4[MaxShadowCasters];

    private readonly List<int> mOCasterOrder = new List<int>();

    private float mfNextCasterScan;

    /// <summary>The most levels the grime along the walls may darken by.</summary>
    public const float MaxGrime = 6f;

    private static readonly int msLightMapId = Shader.PropertyToID("_UWPaletteLightMap");

    /// <summary>The light level a source shines with, as a carried light of that level would
    /// (0-7, UWShades): a candle or a mushroom like a candle, a torch or an orb like a torch, a
    /// lantern or a fire brighter. Not from the data - their brightness field is zero (see
    /// UWRemasterLights) -, chosen by what they are.</summary>
    private static readonly Dictionary<int, float> msLightLevels = new Dictionary<int, float>
    {
        { 145, 2f },   // torch
        { 146, 1f },   // candle
        { 144, 3f },   // lantern
        { 298, 3f },   // campfire
        { 120, 3f },   // fire elemental
        { 297, 1f },   // glowing stone
        { 184, 1f },   // glowing mushroom
        { 279, 2f },   // orb
        { 274, 2f },   // orb stone
    };

    /// <summary>The light level of a lava tile: 1 (by SHADES.DAT 4, 8, 14 at 0, 1, 2 tiles), dimmed
    /// by LavaDim levels: 7, 11, dark - a dull glow with a reach of about two tiles. It was 2 without
    /// a dimming (2, 6, 13) until the user found the brightening too strong, then 1 (4, 8, 14), still
    /// too bright: "In meiner Vorstellung macht Lava nur ein dumpfes Licht" (2026-10-07).</summary>
    private const float LavaLightLevel = 1f;

    private const float LavaDim = 3f;

    /// <summary>How far the light level of open fire swings, in levels.</summary>
    private const float FireFlicker = 0.5f;

    /// <summary>How often the sources are looked for again, in seconds (a torch taken or put
    /// down, a creature on fire walking).</summary>
    private const float SourceInterval = 0.5f;

    private readonly UWPaletteLightMap mOLightMap = new UWPaletteLightMap();

    private readonly List<UWPaletteLightMap.Source> mOSources = new List<UWPaletteLightMap.Source>();

    /// <summary>For every changing source: the entity it follows (null for none) and its phase
    /// of the flicker.</summary>
    private readonly List<Transform> mOSourceFollow = new List<Transform>();

    private readonly List<float> mOSourceBase = new List<float>();

    private readonly List<float> mOSourcePhase = new List<float>();

    private Texture2D mOLightTexture;

    private UWLevel mOLightLevel;

    private int miSourceSignature;

    private float mfNextSourceCheck;

    private static UWPaletteEffects msInstance;

    private ScreenSpaceAmbientOcclusion mOOcclusion;

    private bool mbSearchedOcclusion;

    private bool mbOwnsOcclusion;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void fCreate()
    {
        if (msInstance != null)
            return;

        GameObject lOObject = new GameObject("UW Palette Effects");

        DontDestroyOnLoad(lOObject);
        msInstance = lOObject.AddComponent<UWPaletteEffects>();
    }

    private void LateUpdate()
    {
        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
        bool lbPalette = lOSettings != null && !lOSettings.IsRemasteredActive;


        float lfOcclusion = lbPalette ? Mathf.Clamp(UWUserSettings.PaletteAmbientOcclusion, 0f, MaxAmbientOcclusion) : 0f;

        Shader.SetGlobalFloat(msAmbientOcclusionId, lfOcclusion);
        // The grime in both render modes (UWGrime.hlsl; per user, 2026-10-07).
        Shader.SetGlobalFloat(msGrimeId, Mathf.Clamp(UWUserSettings.PaletteGrime, 0f, MaxGrime));
        Shader.SetGlobalVector(msGrimeToneColourId, fGrimeToneColour());
        fUpdateGrimeTint(lbPalette);
        fUpdateRelief(lbPalette);

        fUpdateLights(lbPalette && UWUserSettings.PaletteLightSources);
        fUpdateShadows(lbPalette ? Mathf.Clamp(UWUserSettings.PaletteGroundShadows, 0f, MaxGroundShadows) : 0f);
        fUpdateGlow(lbPalette && UWUserSettings.PaletteGlow);

        // The occlusion feature: on while the palette path wants it; off again only if it was
        // this one that turned it on - in Remastered UWRemasterRenderer decides.
        if (lfOcclusion > 0f)
        {
            fSetOcclusion(true);
            mbOwnsOcclusion = true;
        }
        else if (mbOwnsOcclusion)
        {
            if (lbPalette)
                fSetOcclusion(false);

            mbOwnsOcclusion = false;
        }
    }

    // ------------------------------------------------- Relief and hollows

    private void fUpdateRelief(bool pbPalette)
    {
        // RELIEF IS NOT OFFERED (per user, 2026-10-07: "Relief zusaetzlich macht es aber nur
        // schlimmer"): lit by whole levels from the eye it reads as painted noise, and on the
        // shifted texels of the depth its normals flip between stone and joint. Form comes from
        // the depth and its self shadow, the joints from the hollows. The shader keeps it at 0.
        float lfRelief = 0f;
        float lfCavity = pbPalette ? Mathf.Clamp(UWUserSettings.PaletteCavity, 0f, MaxRelief) : 0f;
        float lfDepth = pbPalette ? Mathf.Clamp(UWUserSettings.PaletteDepth, 0f, MaxDepth) : 0f;

        if ((lfCavity > 0f || lfDepth > 0f) && mONormalArray == null)
        {
            UWLevelLoader lOLoader = UWScene.LevelLoader;
            UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

            if (lOLoader != null && lOLoader.UWDataImporter != null && lOSettings != null)
                mONormalArray = UWTextureArrayBuilder.BuildNormals(lOLoader.UWDataImporter, lOSettings.RemasterHeightStrength);
        }

        if (mONormalArray == null)
        {
            lfRelief = 0f;
            lfCavity = 0f;
            lfDepth = 0f;
        }
        else
        {
            Shader.SetGlobalTexture(msNormalArrayId, mONormalArray);
        }

        Shader.SetGlobalFloat(msReliefId, lfRelief);
        Shader.SetGlobalFloat(msCavityId, lfCavity);
        Shader.SetGlobalFloat(msDepthId, lfDepth);

        // The Remastered parallax and self shadow values (UWRemasterRenderer sets the same on its
        // material), so both modes share one tuning.
        UnderworldRevisited.UWSettings lOValues = UnderworldRevisited.UWSettings.Instance;

        if (lOValues != null)
        {
            Shader.SetGlobalVector(msParallaxId, new Vector4(lOValues.RemasterParallaxDepth, lOValues.RemasterParallaxLevels,
                lOValues.RemasterParallaxSteps, lOValues.RemasterParallaxInvert ? -1f : 1f));
            Shader.SetGlobalVector(msSelfShadowId, new Vector4(lOValues.RemasterSelfShadowDepth, lOValues.RemasterSelfShadowReach,
                lOValues.RemasterSelfShadowSoftness, 0f));
        }
    }

    // ------------------------------------------------- Grime colour

    /// <summary>The Remastered grime's multiplier on the albedo at full grime: the tone
    /// (UWGrimeTint.GetTone) brightened so the darkest channel keeps some of the colour, or a plain
    /// darkening for "no colour".</summary>
    private static Vector4 fGrimeToneColour()
    {
        UWGrimeTint.ToneEnum leTone = (UWGrimeTint.ToneEnum)Mathf.Clamp(UWUserSettings.PaletteGrimeTone, 0, UWGrimeTint.ToneCount - 1);

        if (leTone == UWGrimeTint.ToneEnum.None)
            return new Vector4(0.45f, 0.45f, 0.45f, 1f);

        UWGrimeTint.GetTone(leTone, out int liR, out int liG, out int liB);

        // The tones are dark colours (92/64/34 and the like); as a multiplier they are scaled so
        // the brightest channel is 0.6.
        float lfScale = 0.6f / Mathf.Max(1, Mathf.Max(liR, Mathf.Max(liG, liB)));

        return new Vector4(liR * lfScale, liG * lfScale, liB * lfScale, 1f);
    }

    private void fUpdateGrimeTint(bool pbPalette)
    {
        int liTone = Mathf.Clamp(UWUserSettings.PaletteGrimeTone, 0, UWGrimeTint.ToneCount - 1);
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (mOGrimeTintTable == null && lOLoader != null && lOLoader.UWDataImporter != null && lOLoader.UWDataImporter.Palettes != null)
        {
            UWPalette lOPalette = lOLoader.UWDataImporter.Palettes.GetPalette(0);

            mOGrimeTintTable = new Texture2D(UWGrimeTint.PaletteSize, UWGrimeTint.ToneCount, TextureFormat.R8, false, true);
            mOGrimeTintTable.name = "UW Grime Tint";
            mOGrimeTintTable.filterMode = FilterMode.Point;
            mOGrimeTintTable.wrapMode = TextureWrapMode.Clamp;

            byte[] lyAll = new byte[UWGrimeTint.PaletteSize * UWGrimeTint.ToneCount];

            for (int liRow = 0; liRow < UWGrimeTint.ToneCount; liRow++)
                System.Array.Copy(UWGrimeTint.Build(lOPalette, (UWGrimeTint.ToneEnum)liRow), 0, lyAll, liRow * UWGrimeTint.PaletteSize, UWGrimeTint.PaletteSize);

            mOGrimeTintTable.SetPixelData(lyAll, 0);
            mOGrimeTintTable.Apply(false, false);
        }

        if (mOGrimeTintTable != null)
            Shader.SetGlobalTexture(msGrimeTintTableId, mOGrimeTintTable);

        Shader.SetGlobalFloat(msGrimeTintId, pbPalette && mOGrimeTintTable != null ? liTone : 0f);
    }

    // ------------------------------------------------- Glow

    /// <summary>The light sources' renderers carry _UWGlowSprite while the glow is on (looked for
    /// every SourceInterval, as things come and go); off, the marks are taken away again.</summary>
    private void fUpdateGlow(bool pbOn)
    {
        Shader.SetGlobalFloat(msGlowId, pbOn ? 1f : 0f);

        if (!pbOn)
        {
            if (mbGlowMarked)
            {
                fMarkGlowing(0f);
                mOGlowing.Clear();
                mbGlowMarked = false;
            }

            return;
        }

        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader == null || Time.unscaledTime < mfNextGlowScan)
            return;

        mfNextGlowScan = Time.unscaledTime + SourceInterval;
        mOGlowing.Clear();

        foreach (UWEntityInfo lOEntity in lOLoader.GetWorldEntities())
        {
            if (lOEntity == null || lOEntity.ObjectData == null || !msLightLevels.ContainsKey(lOEntity.ObjectData.ID))
                continue;

            mOGlowing.AddRange(lOEntity.GetComponentsInChildren<Renderer>(true));
        }

        fMarkGlowing(1f);
        mbGlowMarked = true;
    }

    private void fMarkGlowing(float pfValue)
    {
        if (mOGlowBlock == null)
            mOGlowBlock = new MaterialPropertyBlock();

        foreach (Renderer lORenderer in mOGlowing)
        {
            if (lORenderer == null)
                continue;

            lORenderer.GetPropertyBlock(mOGlowBlock);

            if (Mathf.Approximately(mOGlowBlock.GetFloat(msGlowSpriteId), pfValue))
                continue;

            mOGlowBlock.SetFloat(msGlowSpriteId, pfValue);
            lORenderer.SetPropertyBlock(mOGlowBlock);
        }
    }

    // ------------------------------------------------- Ground shadows

    /// <summary>The casters nearest the eye into the shader's array every frame; who casts is
    /// looked for again every SourceInterval.</summary>
    private void fUpdateShadows(float pfStrength)
    {
        Shader.SetGlobalFloat(msShadowStrengthId, pfStrength);

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        Camera lOCamera = Camera.main;

        if (pfStrength <= 0f || lOLoader == null || lOCamera == null)
        {
            Shader.SetGlobalFloat(msShadowCountId, 0f);
            return;
        }

        if (Time.unscaledTime >= mfNextCasterScan)
        {
            mfNextCasterScan = Time.unscaledTime + SourceInterval;
            fCollectCasters(lOLoader);
        }

        Vector3 lOEye = lOCamera.transform.position;

        mOCasterOrder.Clear();

        for (int liAt = 0; liAt < mOCasters.Count; liAt++)
        {
            if (mOCasters[liAt] != null && mOCasters[liAt].gameObject.activeInHierarchy)
                mOCasterOrder.Add(liAt);
        }

        mOCasterOrder.Sort((a, b) => (mOCasters[a].position - lOEye).sqrMagnitude
            .CompareTo((mOCasters[b].position - lOEye).sqrMagnitude));

        int liCount = Mathf.Min(MaxShadowCasters, mOCasterOrder.Count);

        for (int liAt = 0; liAt < liCount; liAt++)
        {
            int liCaster = mOCasterOrder[liAt];
            Vector3 lOAt = mOCasters[liCaster].position;

            mOShadowData[liAt] = new Vector4(lOAt.x, lOAt.y, lOAt.z, mOCasterRadius[liCaster]);
        }

        Shader.SetGlobalVectorArray(msShadowsId, mOShadowData);
        Shader.SetGlobalFloat(msShadowCountId, liCount);
    }

    /// <summary>Who casts a ground shadow: what stands in the world as a sprite, as in the
    /// Remastered mode (UWRemasterGroundShadows) - not the light sources, nothing in flight; as
    /// wide as its collider in plan view.</summary>
    private void fCollectCasters(UWLevelLoader pOLoader)
    {
        mOCasters.Clear();
        mOCasterRadius.Clear();

        foreach (UWEntityInfo lOEntity in pOLoader.GetWorldEntities())
        {
            if (lOEntity == null || lOEntity.ObjectData == null || !lOEntity.gameObject.activeInHierarchy
                || UWRemasterLights.IsLightSource(lOEntity.ObjectData.ID)
                || lOEntity.GetComponent<UWProjectileFlight>() != null)
                continue;

            MeshRenderer lORenderer = lOEntity.GetComponent<MeshRenderer>();
            Shader lOShader = lORenderer != null && lORenderer.sharedMaterial != null ? lORenderer.sharedMaterial.shader : null;

            if (lOShader == null || (lOShader.name != "UW/Billboard" && lOShader.name != "UW/BillboardPalette"))
                continue;

            BoxCollider lOCollider = lOEntity.GetComponent<BoxCollider>();
            float lfWidth = lOCollider != null ? Mathf.Max(lOCollider.size.x, lOCollider.size.z) : 16f;

            mOCasters.Add(lOEntity.transform);
            mOCasterRadius.Add(Mathf.Max(MinShadowDiameter, lfWidth) * 0.5f);
        }
    }

    // ------------------------------------------------- Light sources

    private void fUpdateLights(bool pbOn)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (!pbOn || lOLoader == null || lOLoader.CurrentLevel == null || lOLoader.UWDataImporter == null)
        {
            Shader.SetGlobalFloat(msLightsId, 0f);
            mOLightLevel = null;
            return;
        }

        bool lbNewLevel = mOLightLevel != lOLoader.CurrentLevel;

        if (lbNewLevel)
        {
            mOLightLevel = lOLoader.CurrentLevel;
            mOLightMap.SetLevel(mOLightLevel, lOLoader.UWDataImporter.Shades);
            miSourceSignature = 0;
            mfNextSourceCheck = 0f;
        }

        if (Time.unscaledTime >= mfNextSourceCheck)
        {
            mfNextSourceCheck = Time.unscaledTime + SourceInterval;
            fCollectSources(lOLoader);
        }

        // The changing sources: their place and the flicker of open fire.
        for (int liAt = 0; liAt < mOSources.Count; liAt++)
        {
            UWPaletteLightMap.Source lOSource = mOSources[liAt];

            if (!lOSource.Changing)
                continue;

            Transform lOFollow = mOSourceFollow[liAt];

            if (lOFollow != null)
            {
                lOSource.X = lOFollow.position.x / UWWorldScale.TileSize;
                lOSource.Y = lOFollow.position.z / UWWorldScale.TileSize;
            }

            float lfPhase = mOSourcePhase[liAt];
            float lfNoise = Mathf.PerlinNoise(lfPhase, Time.time * 3f) - 0.5f;

            lOSource.LightLevel = mOSourceBase[liAt] + (lfNoise * 2f * FireFlicker);
            mOSources[liAt] = lOSource;
        }

        mOLightMap.Compose(mOSources);
        fUploadLightMap();
        Shader.SetGlobalFloat(msLightsId, 1f);
    }

    /// <summary>The sources of the level: every entity on the list of glowing things (open fire
    /// and creatures changing), and every lava tile (steady). The static part of the map is
    /// drawn anew only when the steady ones changed.</summary>
    private void fCollectSources(UWLevelLoader pOLoader)
    {
        mOSources.Clear();
        mOSourceFollow.Clear();
        mOSourceBase.Clear();
        mOSourcePhase.Clear();

        int liSignature = 17;
        UWEntityInfo[] lOEntities = pOLoader.GetWorldEntities();

        foreach (UWEntityInfo lOEntity in lOEntities)
        {
            if (lOEntity == null || lOEntity.ObjectData == null || !lOEntity.gameObject.activeInHierarchy
                || !msLightLevels.TryGetValue(lOEntity.ObjectData.ID, out float lfLevel)
                || !UWRemasterLights.IsLightSource(lOEntity.ObjectData.ID, out bool lbOpenFire))
                continue;

            Vector3 lOAt = lOEntity.transform.position;
            bool lbCreature = lOEntity.GetComponent<UWCritter>() != null;
            bool lbChanging = lbOpenFire || lbCreature;

            mOSources.Add(new UWPaletteLightMap.Source
            {
                X = lOAt.x / UWWorldScale.TileSize,
                Y = lOAt.z / UWWorldScale.TileSize,
                LightLevel = lfLevel,
                Changing = lbChanging
            });
            mOSourceFollow.Add(lbCreature ? lOEntity.transform : null);
            mOSourceBase.Add(lfLevel);
            mOSourcePhase.Add((lOAt.x * 0.137f) + (lOAt.z * 0.711f));

            if (!lbChanging)
                liSignature = (liSignature * 31) + Mathf.RoundToInt(lOAt.x) + (Mathf.RoundToInt(lOAt.z) * 7919) + lOEntity.ObjectData.ID;
        }

        UWTileQueries lOQueries = pOLoader.TileQueries;

        for (int liY = 0; liY < UWWorldScale.TilesPerAxis; liY++)
        {
            for (int liX = 0; liX < UWWorldScale.TilesPerAxis; liX++)
            {
                UWTile lOTile = mOLightLevel.GetTile(liX, liY);

                if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid || lOQueries == null || !lOQueries.IsLavaTile(lOTile))
                    continue;

                mOSources.Add(new UWPaletteLightMap.Source { X = liX, Y = liY, LightLevel = LavaLightLevel, Dim = LavaDim });
                mOSourceFollow.Add(null);
                mOSourceBase.Add(LavaLightLevel);
                mOSourcePhase.Add(0f);
                liSignature = (liSignature * 31) + liX + (liY * 64);
            }
        }

        if (liSignature != miSourceSignature)
        {
            miSourceSignature = liSignature;
            mOLightMap.BuildStatic(mOSources);
        }
    }

    private void fUploadLightMap()
    {
        if (mOLightTexture == null)
        {
            mOLightTexture = new Texture2D(UWPaletteLightMap.Size, UWPaletteLightMap.Size, TextureFormat.R8, false, true);
            mOLightTexture.name = "UW Palette Light Map";
            mOLightTexture.filterMode = FilterMode.Bilinear;
            mOLightTexture.wrapMode = TextureWrapMode.Clamp;
        }

        mOLightTexture.SetPixelData(mOLightMap.Map, 0);
        mOLightTexture.Apply(false, false);
        Shader.SetGlobalTexture(msLightMapId, mOLightTexture);
    }

    private void fSetOcclusion(bool pbOn)
    {
        if (!mbSearchedOcclusion)
        {
            mbSearchedOcclusion = true;

            ScreenSpaceAmbientOcclusion[] lOFound = Resources.FindObjectsOfTypeAll<ScreenSpaceAmbientOcclusion>();

            if (lOFound != null && lOFound.Length > 0)
                mOOcclusion = lOFound[0];
        }

        if (mOOcclusion != null && mOOcclusion.isActive != pbOn)
            mOOcclusion.SetActive(pbOn);
    }

    private void OnDestroy()
    {
        if (msInstance == this)
            msInstance = null;

        if (mOLightTexture != null)
            Destroy(mOLightTexture);

        if (mOGrimeTintTable != null)
            Destroy(mOGrimeTintTable);

        if (mONormalArray != null)
            Destroy(mONormalArray);
    }
}
