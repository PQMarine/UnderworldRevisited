using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// Real light sources for the "Remastered" render mode: torches, candles, campfires,
/// glowing mushrooms, orbs and the lava itself light up their surroundings here.
///
/// WHY THIS IS A DEVIATION, and a deliberate one: in the nine levels of uw1 there is
/// NOT A SINGLE light source that lights anything up - all light comes from the player's
/// torch, and that sits at his eye (see UWObjectSpawner.fAddLightIfSource, which
/// does the same for dropped burning items, and the note in the palette path:
/// "no effect at all of wall torches on the walls"). But that is exactly where any
/// modern lighting fails: without light from the side no relief casts a shadow, and a
/// specular highlight is physically almost invisible with a lamp right at the eye (per user,
/// 2026-09-12: "now you can hardly see a difference"). That is why ONLY this mode gets
/// additional lights; with the effects off or in the palette path nothing changes.
///
/// WHICH OBJECTS is not stated in the object data - their brightness field is zero for all
/// placed objects. The list comes from the NAMES and was counted at the locations found
/// (see the removed tool UWLightObjectDump):
/// torch 145 (9 pieces), candle 146 (4), lantern 144 (6), campfire 298 (22),
/// fire elemental 120 (23), glowing stone 297 (83, all on level 5), mushroom 184 (18),
/// orb 279 (4) and orb stone 274 (10).
///
/// THE LIGHT COLOUR IS NOT GUESSED but taken from the object's own image:
/// the mean of its brightest pixels. A campfire thus glows orange, a
/// glowing mushroom greenish, an orb bluish - without a colour table anywhere.
/// </summary>
public class UWRemasterLights : MonoBehaviour
{
    /// <summary>What makes up a glowing object: range in tiles, brightness at
    /// one tile distance and how strongly it flickers (0 = steady).</summary>
    private struct Recipe
    {
        public int Id;

        public float Tiles;

        public float Brightness;

        /// <summary>How strongly it flickers. For open fire a factor on the setting
        /// RemasterFireFlicker, otherwise the value itself (the lantern barely flickers behind glass).
        /// Independently of this, the sprite's glow flickers only weakly
        /// (RemasterSpriteFlicker).</summary>
        public float Flicker;

        public bool Shadows;

        /// <summary>Open fire: flickers more strongly, and the light jitters a little so the
        /// shadows dance.</summary>
        public bool OpenFire;

        /// <summary>How strongly the bright spots of the sprite itself glow.</summary>
        public float Glow;

        /// <summary>Size of the fire for sparks and smoke (UWRemasterParticles), 0 = none.
        /// </summary>
        public float Fire;
    }

    private static readonly Recipe[] mORecipes =
    {
        new Recipe { Id = 145, Tiles = 3f,   Brightness = 0.8f,  Flicker = 1f,    Shadows = true,  OpenFire = true,  Glow = 1.5f, Fire = 1f },    // torch
        new Recipe { Id = 146, Tiles = 1.5f, Brightness = 0.5f,  Flicker = 0.9f,  Shadows = false, OpenFire = true,  Glow = 1.2f, Fire = 0.35f }, // candle
        new Recipe { Id = 144, Tiles = 3.5f, Brightness = 0.9f,  Flicker = 0.08f, Shadows = true,  OpenFire = false, Glow = 1f },   // lantern
        new Recipe { Id = 298, Tiles = 4f,   Brightness = 1.1f,  Flicker = 1.2f,  Shadows = true,  OpenFire = true,  Glow = 1.8f, Fire = 1.8f },  // campfire
        new Recipe { Id = 120, Tiles = 4f,   Brightness = 1.0f,  Flicker = 1.2f,  Shadows = false, OpenFire = true,  Glow = 1.5f, Fire = 1.4f },  // fire elemental
        new Recipe { Id = 297, Tiles = 2f,   Brightness = 0.35f, Flicker = 0f,    Shadows = false, OpenFire = false, Glow = 1f },   // glowing stone
        new Recipe { Id = 184, Tiles = 1.5f, Brightness = 0.25f, Flicker = 0f,    Shadows = false, OpenFire = false, Glow = 0.8f }, // glowing mushroom
        new Recipe { Id = 279, Tiles = 2.5f, Brightness = 0.6f,  Flicker = 0f,    Shadows = false, OpenFire = false, Glow = 1f },   // orb
        new Recipe { Id = 274, Tiles = 2.5f, Brightness = 0.6f,  Flicker = 0f,    Shadows = false, OpenFire = false, Glow = 1f },   // orb stone
    };

    /// <summary>Renderers whose sprite is currently glowing - reset to zero on cleanup.
    /// </summary>
    private readonly List<Renderer> mOGlowingRenderers = new List<Renderer>();

    private MaterialPropertyBlock mOBlock;

    /// <summary>Lava lights its surroundings. One light per this many tiles - one per
    /// lava tile would be hundreds on a lava level.</summary>
    private const int LavaLightSpacing = 3;

    private const int MaxLavaLights = 48;

    private const float LavaLightTiles = 3f;

    private const float LavaLightBrightness = 0.7f;

    /// <summary>From this share of lava colours on, a floor texture counts as lava.</summary>
    private const float LavaFloorShare = 0.5f;

    private UWLevelLoader mOLevelLoader;

    private readonly List<GameObject> mOLights = new List<GameObject>();

    /// <summary>Lights that are allowed to cast shadows - which ones currently do is decided by
    /// fUpdateShadowLights based on distance.</summary>
    private readonly List<Light> mOShadowLights = new List<Light>();

    /// <summary>How often the shadow casters are redistributed, in seconds.</summary>
    private const float ShadowLightInterval = 0.25f;

    private float mfNextShadowUpdate;

    private int miBuiltLevel = -1;

    /// <summary>State of the particle settings at build time - if they change, it is
    /// rebuilt.</summary>
    private int miBuiltParticleState;

    private bool mbBuilt;

    private void Update()
    {
        if (mOLevelLoader == null)
            mOLevelLoader = GetComponent<UWLevelLoader>();

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        if (mOLevelLoader == null || lOSettings == null)
            return;

        bool lbWanted = lOSettings.IsRemasteredActive
            && lOSettings.RemasterTorchLights;

        if (!lbWanted)
        {
            if (mbBuilt)
                fClear();

            return;
        }

        if (!mbBuilt || miBuiltLevel != mOLevelLoader.CurrentLevelIndex
            || miBuiltParticleState != fGetParticleState(lOSettings))
        {
            fClear();
            fBuild(lOSettings);
            mfNextShadowUpdate = 0f;
        }

        fUpdateShadowLights(lOSettings);
    }

    /// <summary>
    /// Only the nearest shadow casters cast shadows (RemasterMaxShadowLights, per user
    /// 2026-09-13). Before, every torch with recipe shadows cast soft point light shadows -
    /// six shadow maps per light; the log reported 60 times that URP had to lower the
    /// resolution, and Remastered ran increasingly choppy. Lights out of range do not
    /// count: their shadow would not be visible anyway.
    /// </summary>
    private void fUpdateShadowLights(UnderworldRevisited.UWSettings pOSettings)
    {
        if (mOShadowLights.Count == 0 || Time.unscaledTime < mfNextShadowUpdate)
            return;

        mfNextShadowUpdate = Time.unscaledTime + ShadowLightInterval;

        Camera lOCamera = Camera.main;
        int liMax = pOSettings.RemasterTorchShadows ? Mathf.Max(0, pOSettings.RemasterMaxShadowLights) : 0;

        mOShadowLights.RemoveAll(lOLight => lOLight == null);

        if (lOCamera == null || liMax == 0)
        {
            foreach (Light lOLight in mOShadowLights)
                lOLight.shadows = LightShadows.None;

            return;
        }

        Vector3 lOEye = lOCamera.transform.position;

        mOShadowLights.Sort((a, b) =>
            (a.transform.position - lOEye).sqrMagnitude.CompareTo((b.transform.position - lOEye).sqrMagnitude));

        int liGiven = 0;

        foreach (Light lOLight in mOShadowLights)
        {
            bool lbInRange = lOLight.isActiveAndEnabled
                && (lOLight.transform.position - lOEye).sqrMagnitude <= lOLight.range * lOLight.range;

            bool lbShadow = lbInRange && liGiven < liMax;

            if (lbShadow)
                liGiven++;

            LightShadows leWanted = lbShadow ? LightShadows.Soft : LightShadows.None;

            if (lOLight.shadows != leWanted)
                lOLight.shadows = leWanted;
        }
    }

    private void fBuild(UnderworldRevisited.UWSettings pOSettings)
    {
        if (mOLevelLoader.CurrentLevel == null || mOLevelLoader.UWDataImporter == null)
            return;

        int liObjects = fAddObjectLights(pOSettings);
        int liLava = pOSettings.RemasterLavaLights ? fAddLavaLights(pOSettings) : 0;

        // Dust in front of the player's eye, visible only in the light.
        Camera lOCamera = Camera.main;

        if (lOCamera != null)
        {
            GameObject lODust = UWRemasterParticles.AddDust(lOCamera.transform, pOSettings.RemasterDust);

            if (lODust != null)
                mOLights.Add(lODust);
        }

        miBuiltLevel = mOLevelLoader.CurrentLevelIndex;
        miBuiltParticleState = fGetParticleState(pOSettings);
        mbBuilt = true;

        fRefreshPlayerLight();

        if (liObjects > 0 || liLava > 0)
            Debug.Log(string.Format("Remastered: {0} object lights and {1} lava lights on level {2}.",
                liObjects, liLava, miBuiltLevel + 1));
    }

    private int fAddObjectLights(UnderworldRevisited.UWSettings pOSettings)
    {
        UWEntityInfo[] lOEntities = mOLevelLoader.GetWorldEntities();

        if (lOEntities == null)
            return 0;

        int liCount = 0;

        for (int liAt = 0; liAt < lOEntities.Length; liAt++)
        {
            UWEntityInfo lOEntity = lOEntities[liAt];

            if (lOEntity == null || lOEntity.ObjectData == null)
                continue;

            Recipe lORecipe;

            if (!fTryGetRecipe(lOEntity.ObjectData.ID, out lORecipe))
                continue;

            // The LIGHT of open fire flickers strongly and jitters, the GLOW of the sprite
            // only weakly - a pulsing flame image looked odd, the dancing light
            // did not (per user, 2026-09-12).
            float lfFlicker = lORecipe.OpenFire
                ? lORecipe.Flicker * pOSettings.RemasterFireFlicker
                : lORecipe.Flicker;

            float lfJitter = lORecipe.OpenFire ? pOSettings.RemasterFireJitter : 0f;
            float lfGlow = lORecipe.Glow * pOSettings.RemasterSpriteGlow;

            Renderer[] lORenderers = lOEntity.GetComponentsInChildren<Renderer>(true);

            mOGlowingRenderers.AddRange(lORenderers);

            // Steady light sources glow evenly - setting it once is enough. Flickering ones
            // are set by UWLightFlicker every frame in step with their light.
            if (lfFlicker <= 0f)
                UWLightFlicker.SetGlow(lORenderers, lfGlow, ref mOBlock);

            Color lOColour = fGetLightColour(lOEntity.ObjectData.ID);

            GameObject lOLight = fCreateLight(lOEntity.transform, Vector3.up * 8f, lORecipe.Tiles,
                lORecipe.Brightness * pOSettings.RemasterLightBrightness,
                lOColour, lfFlicker,
                lORecipe.Shadows, lfJitter, lORenderers, lfGlow);

            // Sparks and smoke hang on the light and disappear with it.
            if (lORecipe.Fire > 0f)
                UWRemasterParticles.AddFire(lOLight.transform, lOColour, lORecipe.Fire,
                    pOSettings.RemasterFireSparks, pOSettings.RemasterFireSmoke);

            liCount++;
        }

        return liCount;
    }

    /// <summary>
    /// A light above every so-many-th lava tile. The lava has glowed by itself since stage 3,
    /// but a glowing pixel lights up nothing around it in a forward renderer
    /// - that takes real lamps.
    /// </summary>
    private int fAddLavaLights(UnderworldRevisited.UWSettings pOSettings)
    {
        UWLevel lOLevel = mOLevelLoader.CurrentLevel;
        List<UWTexture> lOFloors = mOLevelLoader.UWDataImporter.Textures
            .GetTexturesByType(UWTexture.TextureTypes.FLOOR);

        Dictionary<int, bool> lOIsLava = new Dictionary<int, bool>();
        int liCount = 0;
        int liSize = UWLevelMeshBuilder.TilesPerAxis;

        for (int liY = 0; liY < liSize; liY += LavaLightSpacing)
        {
            for (int liX = 0; liX < liSize; liX += LavaLightSpacing)
            {
                if (liCount >= MaxLavaLights)
                    return liCount;

                UWTile lOTile = lOLevel.TileData[(liY * liSize) + liX];

                if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
                    continue;

                bool lbLava;

                if (!lOIsLava.TryGetValue(lOTile.TextureFloor, out lbLava))
                {
                    lbLava = lOTile.TextureFloor < lOFloors.Count
                        && fGetLavaShare(lOFloors[lOTile.TextureFloor]) >= LavaFloorShare;

                    lOIsLava[lOTile.TextureFloor] = lbLava;
                }

                if (!lbLava)
                    continue;

                Vector3 lOAt = new Vector3(liX * UWLevelMeshBuilder.TileSpacing,
                    lOTile.FloorHeight + 16f, liY * UWLevelMeshBuilder.TileSpacing);

                fCreateLight(transform, lOAt, LavaLightTiles,
                    LavaLightBrightness * pOSettings.RemasterLightBrightness,
                    new Color(1f, 0.45f, 0.12f), 0.12f, false, 0f, null, 0f);

                liCount++;
            }
        }

        return liCount;
    }

    private GameObject fCreateLight(Transform pOParent, Vector3 pOLocalPosition, float pfTiles,
        float pfBrightness, Color pOColour, float pfFlicker, bool pbShadows,
        float pfJitter, Renderer[] pORenderers, float pfGlow)
    {
        GameObject lOObject = new GameObject("Remastered Light");

        lOObject.transform.SetParent(pOParent, false);
        lOObject.transform.localPosition = pOLocalPosition;

        Light lOLight = lOObject.AddComponent<Light>();

        lOLight.type = LightType.Point;
        lOLight.color = pOColour;
        lOLight.range = pfTiles * UWLevelMeshBuilder.TileSpacing;

        // Same calculation as for the player light (UWLighting): URP uses one over
        // distance squared, a tile is 64 units wide.
        lOLight.intensity = pfBrightness * UWLevelMeshBuilder.TileSpacing
            * UWLevelMeshBuilder.TileSpacing * pfTiles * pfTiles;

        // Shadows are distributed by fUpdateShadowLights - only register here.
        lOLight.shadows = LightShadows.None;
        lOLight.shadowStrength = 0.8f;

        if (pbShadows)
            mOShadowLights.Add(lOLight);

        if (pfFlicker > 0f)
            lOObject.AddComponent<UWLightFlicker>().Initialise(lOLight.intensity, pfFlicker,
                pfJitter, pORenderers, pfGlow,
                UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.RemasterSpriteFlicker : 0.06f);

        mOLights.Add(lOObject);

        return lOObject;
    }

    private static int fGetParticleState(UnderworldRevisited.UWSettings pOSettings)
    {
        return pOSettings.RemasterFireSparks.GetHashCode()
            ^ (pOSettings.RemasterFireSmoke.GetHashCode() * 3)
            ^ (pOSettings.RemasterDust.GetHashCode() * 7);
    }

    private void fClear()
    {
        for (int liAt = 0; liAt < mOLights.Count; liAt++)
        {
            if (mOLights[liAt] != null)
                Destroy(mOLights[liAt]);
        }

        mOLights.Clear();
        mOShadowLights.Clear();

        // The sprites glow only in this mode.
        UWLightFlicker.SetGlow(mOGlowingRenderers.ToArray(), 0f, ref mOBlock);
        mOGlowingRenderers.Clear();

        mbBuilt = false;
        miBuiltLevel = -1;

        fRefreshPlayerLight();
    }

    /// <summary>The player light casts shadows only in this mode - it has to notice the
    /// switch (UWLighting otherwise only sets it on a new light source).</summary>
    private void fRefreshPlayerLight()
    {
        UWLighting lOLighting = GetComponent<UWLighting>();

        if (lOLighting != null)
            lOLighting.RefreshPlayerLight();
    }

    /// <summary>Whether an object counts as a light source here - for UWRemasterGroundShadows, which
    /// places no dark spot under light sources.</summary>
    public static bool IsLightSource(int piId)
    {
        Recipe lORecipe;

        return fTryGetRecipe(piId, out lORecipe);
    }

    private static bool fTryGetRecipe(int piId, out Recipe pORecipe)
    {
        for (int liAt = 0; liAt < mORecipes.Length; liAt++)
        {
            if (mORecipes[liAt].Id == piId)
            {
                pORecipe = mORecipes[liAt];
                return true;
            }
        }

        pORecipe = default(Recipe);

        return false;
    }

    /// <summary>The light colour from the object's image - see UWLightColour, which the
    /// player's carried light also uses.</summary>
    private Color fGetLightColour(int piId)
    {
        return UWLightColour.FromObject(mOLevelLoader.UWDataImporter, piId, new Color(1f, 0.78f, 0.5f));
    }

    private static float fGetLavaShare(UWTexture pOTexture)
    {
        byte[] lyIndices = pOTexture == null ? null : pOTexture.GetMainPaletteIndices();

        if (lyIndices == null || lyIndices.Length == 0)
            return 0f;

        int liLava = 0;

        for (int liAt = 0; liAt < lyIndices.Length; liAt++)
        {
            if (UWPaletteRotation.IsLavaIndex(lyIndices[liAt]))
                liLava++;
        }

        return (float)liLava / lyIndices.Length;
    }

    private void OnDestroy()
    {
        fClear();
    }
}
