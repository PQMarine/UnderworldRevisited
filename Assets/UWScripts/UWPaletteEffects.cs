using System.Collections.Generic;
using UnityEngine;
using UWDataImport.Presentation;
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
///   light sources      the list of glowing things both modes share (UWLightSourceTable) and every
///                      lava tile, each lit like a carried light of its own level by the
///                      original's SHADES.DAT table, walls casting shadows (UWPaletteLightMap,
///                      _UWPaletteLightMap); fire flickers. Within the player's own view distance
///                      only (per user: a source must not let one see further than the original).
///
/// Ticked by the palette path (UWPaletteRenderPath.LateTick) while it is in force and cleared
/// when it goes (Clear): every effect off, and the globals only the palette shaders read taken
/// away, so a Remastered dump shows none of them. Until cleanup step 6.5 (2026-10-24) this was a
/// component of its own, kept across scene loads, that asked the render mode every frame and in
/// Remastered kept the light map, the normal array and the shadow list set. The grime is the one
/// effect both modes draw (ApplyShared, from UWRenderPathHost every frame). URP's occlusion
/// feature is the host's: the palette path wants it while the ambient occlusion is above 0
/// (UWPaletteRenderPath.WantsOcclusion).
/// The values go through UWShaderGlobals (2026-10-24), which writes only what changed: the copies
/// of the user's settings once, the shadow array and the light map while something moves.
/// </summary>
public sealed class UWPaletteEffects
{
    /// <summary>The most levels ambient occlusion may darken by.</summary>
    public const float MaxAmbientOcclusion = 6f;

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

    /// <summary>The largest cell of the dither pattern, in pixels of the picture.</summary>
    public const float MaxDitherCell = 12f;

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

    /// <summary>The normal and height array, asked for the first time relief or hollows are on;
    /// UWTextureCache owns it (a destroyed one reads as null here).</summary>
    private Texture2DArray mONormalArray;

    /// <summary>The grime's colour tables, one row per tone (UWGrimeTint), built once.</summary>
    private Texture2D mOGrimeTintTable;

    /// <summary>The most levels a ground shadow may darken by (UWGroundShadowRule).</summary>
    public const float MaxGroundShadows = UWGroundShadowRule.MaxPaletteLevels;

    /// <summary>As many casters as the shader takes (UW_GROUND_SHADOWS).</summary>
    private const int MaxShadowCasters = 64;

    private readonly List<Transform> mOCasters = new List<Transform>();

    private readonly List<float> mOCasterRadius = new List<float>();

    private readonly Vector4[] mOShadowData = new Vector4[MaxShadowCasters];

    private readonly List<int> mOCasterOrder = new List<int>();

    /// <summary>Each caster's squared distance from the eye this frame, by caster - read by
    /// fCompareCasters, which is made once: until 2026-10-24 (cleanup step 6.3) the sort took a
    /// closure over the eye, a new allocation every frame.</summary>
    private readonly List<float> mOCasterDistance = new List<float>();

    private System.Comparison<int> mOCompareCasters;

    private float mfNextCasterScan;

    /// <summary>The most levels the grime along the walls may darken by.</summary>
    public const float MaxGrime = 6f;

    // The light level each source shines with (UWLightSourceTable.Row.PaletteLightLevel), the lava
    // tile's level and dimming (UWLightSourceTable.LavaPalette*) and the swing of open fire
    // (UWFlicker.PaletteFireLevel) are shared tables and rules since cleanup step 6.4 (2026-10-24).

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

    /// <summary>The ambient occlusion the user asked for, in levels.</summary>
    public static float AmbientOcclusionLevels => Mathf.Clamp(UWUserSettings.PaletteAmbientOcclusion, 0f, MaxAmbientOcclusion);

    /// <summary>
    /// What both render modes draw of these effects: the grime (UWGrime.hlsl; per user,
    /// 2026-10-07, in Remastered too), set by UWRenderPathHost every frame in either mode.
    /// </summary>
    public static void ApplyShared()
    {
        UWShaderGlobals.SetFloat(UWShaderIds.PaletteGrime, Mathf.Clamp(UWUserSettings.PaletteGrime, 0f, MaxGrime));
        UWShaderGlobals.SetVector(UWShaderIds.GrimeToneColour, fGrimeToneColour());
    }

    /// <summary>Every frame while the palette path is in force (UWPaletteRenderPath.LateTick).</summary>
    public void Tick()
    {
        float lfOcclusion = AmbientOcclusionLevels;

        UWShaderGlobals.SetFloat(UWShaderIds.PaletteAO, lfOcclusion);
        fUpdateGrimeTint();
        fUpdateRelief();

        fUpdateLights(UWUserSettings.PaletteLightSources);
        fUpdateShadows(Mathf.Clamp(UWUserSettings.PaletteGroundShadows, 0f, MaxGroundShadows));
        fUpdateGlow(UWUserSettings.PaletteGlow);
    }

    /// <summary>
    /// The palette path goes (UWPaletteRenderPath.Deactivate): every effect off, the glow marks
    /// taken away, and the textures and the shadow list only the palette shaders read cleared
    /// (cleanup step 6.5, 2026-10-24; until then they stayed set in Remastered).
    /// </summary>
    public void Clear()
    {
        UWShaderGlobals.SetFloat(UWShaderIds.PaletteAO, 0f);

        UWShaderGlobals.SetFloat(UWShaderIds.GrimeTint, 0f);
        UWShaderGlobals.ClearTexture(UWShaderIds.GrimeTintTable);

        UWShaderGlobals.SetFloat(UWShaderIds.PaletteRelief, 0f);
        UWShaderGlobals.SetFloat(UWShaderIds.PaletteCavity, 0f);
        UWShaderGlobals.SetFloat(UWShaderIds.PaletteDepth, 0f);
        UWShaderGlobals.ClearTexture(UWShaderIds.PaletteNormalArray);
        UWShaderGlobals.SetVector(UWShaderIds.PaletteParallax, Vector4.zero);
        UWShaderGlobals.SetVector(UWShaderIds.PaletteSelfShadow, Vector4.zero);

        fUpdateLights(false);
        UWShaderGlobals.ClearTexture(UWShaderIds.PaletteLightMap);

        UWShaderGlobals.SetFloat(UWShaderIds.PaletteShadowStrength, 0f);
        UWShaderGlobals.SetFloat(UWShaderIds.PaletteShadowCount, 0f);
        UWShaderGlobals.ClearVectorArray(UWShaderIds.PaletteShadows);

        fUpdateGlow(false);
    }

    // ------------------------------------------------- Relief and hollows

    private void fUpdateRelief()
    {
        // RELIEF IS NOT OFFERED (per user, 2026-10-07: "Relief zusaetzlich macht es aber nur
        // schlimmer"): lit by whole levels from the eye it reads as painted noise, and on the
        // shifted texels of the depth its normals flip between stone and joint. Form comes from
        // the depth and its self shadow, the joints from the hollows. The shader keeps it at 0.
        float lfRelief = 0f;
        float lfCavity = Mathf.Clamp(UWUserSettings.PaletteCavity, 0f, MaxRelief);
        float lfDepth = Mathf.Clamp(UWUserSettings.PaletteDepth, 0f, MaxDepth);

        if (lfCavity > 0f || lfDepth > 0f)
        {
            UWLevelLoader lOLoader = UWScene.LevelLoader;
            UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

            // Built once, and again when the height strength changes (until 2026-10-11 this one
            // stayed at the first strength) - by UWTextureCache since 2026-10-24, the same array
            // Remastered uses.
            if (lOLoader != null && lOLoader.UWDataImporter != null && lOSettings != null)
                mONormalArray = UWTextureCache.GetNormalArray(lOLoader.UWDataImporter, lOSettings.RemasterHeightStrength);
        }

        if (mONormalArray == null)
        {
            lfRelief = 0f;
            lfCavity = 0f;
            lfDepth = 0f;
        }
        else
        {
            UWShaderGlobals.SetTexture(UWShaderIds.PaletteNormalArray, mONormalArray);
        }

        UWShaderGlobals.SetFloat(UWShaderIds.PaletteRelief, lfRelief);
        UWShaderGlobals.SetFloat(UWShaderIds.PaletteCavity, lfCavity);
        UWShaderGlobals.SetFloat(UWShaderIds.PaletteDepth, lfDepth);

        // The Remastered parallax and self shadow values (UWRemasterRenderPath sets the same on its
        // material), so both modes share one tuning.
        UnderworldRevisited.UWSettings lOValues = UnderworldRevisited.UWSettings.Instance;

        if (lOValues != null)
        {
            UWShaderGlobals.SetVector(UWShaderIds.PaletteParallax, new Vector4(lOValues.RemasterParallaxDepth, lOValues.RemasterParallaxLevels,
                lOValues.RemasterParallaxSteps, lOValues.RemasterParallaxInvert ? -1f : 1f));
            UWShaderGlobals.SetVector(UWShaderIds.PaletteSelfShadow, new Vector4(lOValues.RemasterSelfShadowDepth, lOValues.RemasterSelfShadowReach,
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

    private void fUpdateGrimeTint()
    {
        int liTone = Mathf.Clamp(UWUserSettings.PaletteGrimeTone, 0, UWGrimeTint.ToneCount - 1);
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (mOGrimeTintTable == null && lOLoader != null && lOLoader.UWDataImporter != null && lOLoader.UWDataImporter.Palettes != null)
            mOGrimeTintTable = UWTextureFactory.GrimeTintTable(lOLoader.UWDataImporter.Palettes.GetPalette(0));

        if (mOGrimeTintTable != null)
            UWShaderGlobals.SetTexture(UWShaderIds.GrimeTintTable, mOGrimeTintTable);

        UWShaderGlobals.SetFloat(UWShaderIds.GrimeTint, mOGrimeTintTable != null ? liTone : 0f);
    }

    // ------------------------------------------------- Glow

    /// <summary>The light sources' renderers carry _UWGlowSprite while the glow is on (looked for
    /// every SourceInterval, as things come and go); off, the marks are taken away again.</summary>
    private void fUpdateGlow(bool pbOn)
    {
        UWShaderGlobals.SetFloat(UWShaderIds.PaletteGlow, pbOn ? 1f : 0f);

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
            if (lOEntity == null || lOEntity.ObjectData == null || !UWLightSourceTable.IsLightSource(lOEntity.ObjectData.ID))
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

            if (Mathf.Approximately(mOGlowBlock.GetFloat(UWShaderIds.GlowSprite), pfValue))
                continue;

            mOGlowBlock.SetFloat(UWShaderIds.GlowSprite, pfValue);
            lORenderer.SetPropertyBlock(mOGlowBlock);
        }
    }

    // ------------------------------------------------- Ground shadows

    /// <summary>The casters nearest the eye into the shader's array every frame; who casts is
    /// looked for again every SourceInterval.</summary>
    private void fUpdateShadows(float pfStrength)
    {
        UWShaderGlobals.SetFloat(UWShaderIds.PaletteShadowStrength, pfStrength);

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        Camera lOCamera = Camera.main;

        if (pfStrength <= 0f || lOLoader == null || lOCamera == null)
        {
            UWShaderGlobals.SetFloat(UWShaderIds.PaletteShadowCount, 0f);
            return;
        }

        if (Time.unscaledTime >= mfNextCasterScan)
        {
            mfNextCasterScan = Time.unscaledTime + SourceInterval;
            fCollectCasters(lOLoader);
        }

        Vector3 lOEye = lOCamera.transform.position;

        mOCasterOrder.Clear();
        mOCasterDistance.Clear();

        for (int liAt = 0; liAt < mOCasters.Count; liAt++)
        {
            bool lbActive = mOCasters[liAt] != null && mOCasters[liAt].gameObject.activeInHierarchy;

            mOCasterDistance.Add(lbActive ? (mOCasters[liAt].position - lOEye).sqrMagnitude : 0f);

            if (lbActive)
                mOCasterOrder.Add(liAt);
        }

        if (mOCompareCasters == null)
            mOCompareCasters = fCompareCasters;

        mOCasterOrder.Sort(mOCompareCasters);

        int liCount = Mathf.Min(MaxShadowCasters, mOCasterOrder.Count);

        for (int liAt = 0; liAt < liCount; liAt++)
        {
            int liCaster = mOCasterOrder[liAt];
            Vector3 lOAt = mOCasters[liCaster].position;

            mOShadowData[liAt] = new Vector4(lOAt.x, lOAt.y, lOAt.z, mOCasterRadius[liCaster]);
        }

        UWShaderGlobals.SetVectorArray(UWShaderIds.PaletteShadows, mOShadowData);
        UWShaderGlobals.SetFloat(UWShaderIds.PaletteShadowCount, liCount);
    }

    /// <summary>Nearer the eye first (this frame's distances, mOCasterDistance).</summary>
    private int fCompareCasters(int piA, int piB)
    {
        return mOCasterDistance[piA].CompareTo(mOCasterDistance[piB]);
    }

    /// <summary>Who casts a ground shadow: what stands in the world as a sprite, as in the
    /// Remastered mode (UWRemasterGroundShadows) - not the light sources, nothing in flight; as
    /// wide as its collider in plan view. The one rule of both modes: UWGroundShadowRule.</summary>
    private void fCollectCasters(UWLevelLoader pOLoader)
    {
        mOCasters.Clear();
        mOCasterRadius.Clear();

        foreach (UWEntityInfo lOEntity in pOLoader.GetWorldEntities())
        {
            if (lOEntity == null || !lOEntity.gameObject.activeInHierarchy
                || !UWGroundShadowCasters.TryGetWidth(lOEntity, true, out float lfWidth))
                continue;

            mOCasters.Add(lOEntity.transform);
            mOCasterRadius.Add(UWGroundShadowRule.PaletteRadius(lfWidth));
        }
    }

    // ------------------------------------------------- Light sources

    private void fUpdateLights(bool pbOn)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (!pbOn || lOLoader == null || lOLoader.CurrentLevel == null || lOLoader.UWDataImporter == null)
        {
            UWShaderGlobals.SetFloat(UWShaderIds.PaletteLights, 0f);
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

            lOSource.LightLevel = UWFlicker.PaletteFireLevel(UWLightFlicker.Perlin, mOSourcePhase[liAt],
                Time.time, mOSourceBase[liAt]);
            mOSources[liAt] = lOSource;
        }

        mOLightMap.Compose(mOSources);
        fUploadLightMap();
        UWShaderGlobals.SetFloat(UWShaderIds.PaletteLights, 1f);
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
                || !UWLightSourceTable.TryGet(lOEntity.ObjectData.ID, out UWLightSourceTable.Row lORow))
                continue;

            float lfLevel = lORow.PaletteLightLevel;
            Vector3 lOAt = lOEntity.transform.position;
            bool lbCreature = lOEntity.GetComponent<UWCritter>() != null;
            bool lbChanging = lORow.OpenFire || lbCreature;

            mOSources.Add(new UWPaletteLightMap.Source
            {
                X = lOAt.x / UWWorldScale.TileSize,
                Y = lOAt.z / UWWorldScale.TileSize,
                LightLevel = lfLevel,
                Changing = lbChanging
            });
            mOSourceFollow.Add(lbCreature ? lOEntity.transform : null);
            mOSourceBase.Add(lfLevel);
            mOSourcePhase.Add(UWFlicker.PalettePhase(lOAt.x, lOAt.z));

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

                mOSources.Add(new UWPaletteLightMap.Source { X = liX, Y = liY,
                    LightLevel = UWLightSourceTable.LavaPaletteLightLevel, Dim = UWLightSourceTable.LavaPaletteDim });
                mOSourceFollow.Add(null);
                mOSourceBase.Add(UWLightSourceTable.LavaPaletteLightLevel);
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
        UWShaderGlobals.SetTexture(UWShaderIds.PaletteLightMap, mOLightTexture);
    }

    /// <summary>The palette path goes with its scene (UWPaletteRenderPath.Dispose).</summary>
    public void Dispose()
    {
        // Not the normal array: UWTextureCache owns it (since 2026-10-24).
        foreach (Texture lOTexture in new Texture[] { mOLightTexture, mOGrimeTintTable })
        {
            if (lOTexture != null)
            {
                UWShaderGlobals.Invalidate(lOTexture);
                Object.Destroy(lOTexture);
            }
        }

        mOLightTexture = null;
        mOGrimeTintTable = null;
    }
}
