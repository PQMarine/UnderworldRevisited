using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Lighting of the dungeon.
///
/// In Ultima Underworld 1 there is no per-tile light - bit 8 of the tile properties
/// intended for it is always 0 according to the format description - and none of the nine
/// levels contains a burning light source. All light comes from the
/// light source object the player carries. That is exactly why the abyss is pitch dark
/// without a torch.
///
/// The brightness level of the carried object is stored in OBJECTS.DAT and ranges from 0 to
/// 4; here it determines, together with the light spell (the larger of the two counts),
/// range and strength of the player lamp.
/// </summary>
public class UWLighting : MonoBehaviour
{
    /// <summary>Name of the existing light object at the player in the scene.</summary>
    private const string PlayerLightName = "PlayerLight";

    [Header("Carried light source")]
    [SerializeField]
    [Tooltip("Object id of the carried light source. 0x0095 is the lit torch, 0x0094 the lit lantern, 0 means no light.")]
    // Default is NO light. Until 2026-09-03 this held the lit torch as a
    // placeholder while carried light sources were not built - but it has the same
    // brightness as the light spell and thereby made the spell mathematically ineffective.
    private int miCarriedLightObjectId = 0;

    [Header("Player lamp")]
    [SerializeField]
    [Tooltip("Range per brightness level. One tile is 64 units wide.")]
    private float mfRangePerBrightness = 90f;

    [SerializeField]
    [Tooltip("Brightness at a distance of one tile. URP uses physical falloff, the actual intensity is this times 64 squared.")]
    private float mfBrightnessAtOneTile = 1.2f;

    /// <summary>How strongly the strength grows with the range. Two means equal
    /// illumination at the respective edge, zero the earlier behaviour with fixed strength.
    ///
    /// ONE, because in the original the lantern is only a little brighter than In Lor (per
    /// user, 2026-09-05) - that is five to four tiles and thus a quarter more. With
    /// two it would be half and too much.
    /// </summary>
    [SerializeField]
    private float mfIntensityRangeExponent = 1f;

    /// <summary>
    /// STOPGAP: the ranges from SHADES.DAT are multiplied by this.
    ///
    /// URP computes the falloff fixed as max(0, 1-(d²/r²)²)² - a very soft curve that
    /// leaves practically nothing long before the given range. The
    /// light sources therefore shine much shorter than the table says. Two was too little,
    /// hence FOUR here (per user, 2026-09-05) - a tuned value, not a measured one.
    ///
    /// The ratio of the light levels to each other stays correct, because all get the same
    /// factor - including the strength that results from the range.
    ///
    /// REMOVE IT as soon as the renderer is rebuilt: the original shades via the
    /// palette tables from SHADES.DAT and does not know this curve at all. A custom
    /// falloff can only be set in URP via a custom version of DistanceAttenuation in
    /// the shader library, there is no setting for it.
    /// </summary>
    [SerializeField]
    private float mfSoftFalloffRangeFactor = 4f;

    [SerializeField]
    private Color mOLightColor = new Color(.5f, 0.5f, 0.5f);

    [SerializeField]
    [Tooltip("Strength of the flicker, 0 turns it off.")]
    [Range(0f, 0.5f)]
    private float mfFlickerAmount = 0.1f;

    [SerializeField]
    private float mfFlickerSpeed = 6.5f;

    [Header("Environment")]
    [SerializeField]
    [Tooltip("Base brightness of the scene. In the original you see practically nothing without a light source.")]
    private Color mOAmbientColor = new Color(0.05f, 0.05f, 0.07f);

    [SerializeField]
    private bool mbFogEnabled = true;

    [SerializeField]
    private Color mOFogColor = new Color(0.02f, 0.02f, 0.03f);

    [SerializeField]
    [Tooltip("Density of the exponential fog. Larger values shorten the view distance.")]
    private float mfFogDensity = 0.0035f;

    private DataImport mOData;
    private Light mOPlayerLight;
    private float mfBaseIntensity;
    private bool mbDebugBrightnessOverride;

    public bool IsBright
        => mbDebugBrightnessOverride;

    /// <summary>
    /// The carried light in the Remastered render mode.
    ///
    /// WHY IT SITS DIFFERENTLY THERE: at the eye it lights everything frontally, flat like a
    /// camera flash - every shadow falls exactly behind what casts it and stays
    /// invisible. With the placed lights it looked right, but no longer once the player turned on
    /// their own light (per user, 2026-09-12). Therefore: a torch or
    /// candle shines from the right hand, a lantern from the belt (per user), a
    /// light spell from the left hand. In addition it becomes weaker so that it does not outshine the mood of the
    /// placed lights, and it gets the colour of its item like
    /// they do (UWLightColour).
    ///
    /// Without the Remastered effects everything stays as it was - the original position is remembered
    /// the first time and restored there.
    /// </summary>
    private void fApplyRemasterCarriedLight()
    {
        if (!mbKnowsLightPosition)
        {
            mODefaultLightPosition = mOPlayerLight.transform.localPosition;
            mbKnowsLightPosition = true;
        }

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        bool lbRemastered = lOSettings != null
            && lOSettings.IsRemasteredActive;

        if (!lbRemastered)
        {
            mOPlayerLight.transform.localPosition = mODefaultLightPosition;
            mbCarriedOpenFire = false;
            return;
        }

        // Which source makes the light: the carried item, unless the spell shines
        // brighter (see MagicBrightness - the original takes the larger).
        bool lbFromItem = miCarriedLightObjectId > 0 && CarriedBrightness >= miMagicBrightness;

        // What is carried is always the LIT number (lantern 148, torch 149, candle 150),
        // the comparisons below refer to the base number 144 to 147. The colour still comes from the
        // lit image, only there is the flame visible.
        // Without this the torch in the hand did not flicker and the lantern did not hang at the
        // belt (per user, 2026-09-13: "shadow does not move").
        int liBaseId = UWDataImport.UWData.UWObjectMechanics.IsLitLight(miCarriedLightObjectId)
            ? miCarriedLightObjectId - UWDataImport.UWData.UWObjectMechanics.LitLightOffset
            : miCarriedLightObjectId;

        Vector3 lOOffset = !lbFromItem ? lOSettings.RemasterMagicLightOffset
            : liBaseId == LanternObjectId ? lOSettings.RemasterBeltLightOffset
            : lOSettings.RemasterHandLightOffset;

        mOPlayerLight.transform.localPosition = lOOffset;

        mOCarriedBasePosition = lOOffset;
        mbCarriedOpenFire = lbFromItem && (liBaseId == TorchObjectId || liBaseId == CandleObjectId);

        mOPlayerLight.color = lbFromItem
            ? UWLightColour.FromObject(mOData, miCarriedLightObjectId, new Color(1f, 0.78f, 0.5f))
            : MagicLightColour;

        mfBaseIntensity *= lOSettings.RemasterCarriedLightFactor;
    }

    private void fRefreshOnRemasterChange()
    {
        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        if (lOSettings == null || mOPlayerLight == null)
            return;

        int liState = lOSettings.RenderMode.GetHashCode()
            ^ (lOSettings.RemasterCarriedLightFactor.GetHashCode() * 3)
            ^ (lOSettings.RemasterHandLightOffset.GetHashCode() * 7)
            ^ (lOSettings.RemasterBeltLightOffset.GetHashCode() * 11)
            ^ (lOSettings.RemasterMagicLightOffset.GetHashCode() * 13)
            ^ (lOSettings.RemasterTorchShadows.GetHashCode() * 17);

        if (liState == miRemasterState)
            return;

        miRemasterState = liState;

        fApplyPlayerLight();
    }

    private int miRemasterState;

    private const int TorchObjectId = UWObjectMechanics.TorchObjectId;

    private const int CandleObjectId = 146;

    /// <summary>Whether the player carries open fire in Remastered mode - then it flickers and
    /// jitters like the placed fires.</summary>
    private bool mbCarriedOpenFire;

    private Vector3 mOCarriedBasePosition;

    /// <summary>a_lantern - the only light source that hangs at the belt.</summary>
    private const int LanternObjectId = UWObjectMechanics.LanternObjectId;

    /// <summary>A light spell has no item whose colour could be taken; it
    /// gets a cool white so that it differs from fire.</summary>
    private static readonly Color MagicLightColour = new Color(0.8f, 0.88f, 1f);

    private bool mbKnowsLightPosition;

    private Vector3 mODefaultLightPosition;


    /// <summary>Sets up the player light anew - for a render mode change that alters
    /// shadow casting (see UWRemasterLights). Otherwise this only happens when the
    /// light source or spell changes.</summary>
    public void RefreshPlayerLight()
    {
        fApplyPlayerLight();
    }

    /// <summary>
    /// Brightness from an active light spell, 0 to 5.
    ///
    /// The original takes the LARGER of carried and cast brightness, it does
    /// not add (reference: "if (lightlevel < minorclass) lightlevel = minorclass").
    /// In Lor carries minor class 3.
    /// </summary>
    public int MagicBrightness
    {
        get { return miMagicBrightness; }
        set
        {
            if (miMagicBrightness == value)
                return;

            miMagicBrightness = value;

            fApplyPlayerLight();
        }
    }

    private int miMagicBrightness;

    public int CarriedLightObjectId
    {
        get { return miCarriedLightObjectId; }
    }

    /// <summary>Brightness level of the carried light source, 0 to 4.</summary>
    public int CarriedBrightness
    {
        get
        {
            if (mOData == null || mOData.ObjectProperties == null)
                return 0;

            return mOData.ObjectProperties.GetLightBrightness(miCarriedLightObjectId);
        }
    }

    /// <summary>
    /// The player's light level, 0 to 7 - the LARGER of carried and
    /// cast brightness, capped to the eight entries of SHADES.DAT.
    ///
    /// In the original this is the ONLY quantity from which the lighting of the whole
    /// scene results: together with the distance to the camera it determines every pixel via SHADES.DAT and
    /// LIGHT.DAT. Wall torches light up nothing, they are only bright
    /// images. Groundwork for the palette renderer, see UWShadePalette.
    ///
    /// The full brightness toggle (key B) yields the highest level here.
    /// </summary>
    public int LightLevel
    {
        get
        {
            if (mbDebugBrightnessOverride)
                return UWDataImport.UWData.UWShades.EntryCount - 1;

            int liBrightness = Mathf.Max(CarriedBrightness, miMagicBrightness);

            return Mathf.Clamp(liBrightness, 0, UWDataImport.UWData.UWShades.EntryCount - 1);
        }
    }

    public void Init(DataImport pOData)
    {
        mOData = pOData;

        fApplyEnvironment();
        fApplyPlayerLight();
    }

    /// <summary>Changes the carried light source, e.g. when lighting a torch.</summary>
    public void SetCarriedLight(int piObjectId)
    {
        miCarriedLightObjectId = piObjectId;
        fApplyPlayerLight();
    }

    /// <summary>
    /// Debug: bright environment without fog, independent of the carried light source - the
    /// player torch hangs at the player and drops away during noclip exploration (the player is
    /// deactivated then), leaving everything except the immediate surroundings black.
    /// </summary>
    public void ToggleDebugBrightness()
    {
        mbDebugBrightnessOverride = !mbDebugBrightnessOverride;

        if (mbDebugBrightnessOverride)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.9f, 0.9f, 0.95f);
            RenderSettings.fog = false;
        }
        else
        {
            fApplyEnvironment();
        }
    }

    private void fApplyEnvironment()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = mOAmbientColor;

        RenderSettings.fog = mbFogEnabled;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = mOFogColor;
        RenderSettings.fogDensity = mfFogDensity;
    }

    /// <summary>What currently contributes to brightness - read-only in the Inspector. Carried
    /// and cast are shown separately so you can see which path does not arrive.
    /// </summary>
    [Header("Read only")]
    [SerializeField]
    private int miDebugCarriedLightId;

    [SerializeField]
    private int miDebugCarriedBrightness;

    [SerializeField]
    private int miDebugMagicBrightness;

    [SerializeField]
    private int miDebugAppliedBrightness;

    [SerializeField]
    private bool mbDebugHasPlayerLight;

    [SerializeField]
    private float mfDebugLightIntensity;

    [SerializeField]
    private float mfDebugLightRange;

    [SerializeField]
    private Color mODebugLightColour;

    /// <summary>How many UWLighting exist at runtime. More than one would be the bug:
    /// they drive the same lamp via GameObject.Find and overwrite each other.</summary>
    [SerializeField]
    private int miDebugInstanceCount;

    /// <summary>How often this instance has already updated the lamp.</summary>
    [SerializeField]
    private int miDebugApplyCount;

    /// <summary>
    /// Turns off the player's point light.
    ///
    /// The palette renderer has no local light sources: brightness depends
    /// solely on the light level and the distance, computed via SHADES.DAT and LIGHT.DAT.
    /// An additional URP light would double the effect. The light level itself
    /// is still maintained - the palette renderer reads it via LightLevel.
    /// </summary>
    public bool SuppressPlayerLight { get; set; }

    private void fApplyPlayerLight()
    {
        if (mOPlayerLight == null)
            mOPlayerLight = fFindPlayerLight();

        if (mOPlayerLight == null)
            return;

        if (SuppressPlayerLight)
        {
            mOPlayerLight.enabled = false;
            return;
        }

        // The LARGER of carried and cast brightness, not the sum (see
        // MagicBrightness).
        miDebugApplyCount++;
        miDebugInstanceCount = FindObjectsByType<UWLighting>().Length;
        miDebugCarriedLightId = miCarriedLightObjectId;
        miDebugCarriedBrightness = CarriedBrightness;
        miDebugMagicBrightness = miMagicBrightness;
        mbDebugHasPlayerLight = mOPlayerLight != null;

        int liBrightness = Mathf.Max(CarriedBrightness, miMagicBrightness);

        miDebugAppliedBrightness = liBrightness;

        if (liBrightness <= 0)
        {
            mOPlayerLight.enabled = false;
            return;
        }

        mOPlayerLight.enabled = true;
        mOPlayerLight.type = LightType.Point;
        mOPlayerLight.color = mOLightColor;
        // The view distance is a table in SHADES.DAT, one tile per light level - the
        // original does not compute it. Two tests by the user match it exactly: In Lor
        // is level 3 and shows four tiles, the lantern is level 4 and shows five
        // (2026-09-03). The factor per level used earlier remains as a fallback in case the
        // file is missing.
        UWDataImport.UWData.UWShades.Entry lOShade;

        mOPlayerLight.range = (mOData != null && mOData.Shades != null
            && mOData.Shades.TryGet(liBrightness, out lOShade) && lOShade.ViewingDistance > 0
            ? lOShade.ViewingDistance * UnderworldRevisited.Build.UWLevelMeshBuilder.TileSpacing
            : liBrightness * mfRangePerBrightness) * mfSoftFalloffRangeFactor;
        // Shadows only in the Remastered render mode (per user, 2026-09-12: "shadows from the
        // torch light"). The original has none, and with the effect switched off nothing changes.
        UnderworldRevisited.UWSettings lORenderSettings = UnderworldRevisited.UWSettings.Instance;

        mOPlayerLight.shadows = lORenderSettings != null
            && lORenderSettings.IsRemasteredActive
            && lORenderSettings.RemasterTorchShadows
                ? LightShadows.Soft : LightShadows.None;

        // URP uses the physical one-over-distance-squared falloff. One tile is 64
        // units wide, so the intensity has to scale with the square of that size
        // - otherwise practically nothing is left at a tile's distance.
        // The strength grows with the range, otherwise the light levels do not differ
        // visibly: with one over distance squared almost nothing is left at four tiles
        // distance already, and whether the lamp ends there or only at five cannot be
        // seen (per user, 2026-09-05: ranges 320 versus 256 correct, but in the world
        // no difference).
        //
        // With the exponent TWO the illumination at the respective edge is equal - the
        // lantern with five tiles then shines about one and a half times as strong as In Lor with
        // four. Zero restores the old behaviour.
        float lfRangeInTiles = mOPlayerLight.range / UnderworldRevisited.Build.UWLevelMeshBuilder.TileSpacing;

        mfBaseIntensity = mfBrightnessAtOneTile
            * UnderworldRevisited.Build.UWLevelMeshBuilder.TileSpacing * UnderworldRevisited.Build.UWLevelMeshBuilder.TileSpacing
            * Mathf.Pow(Mathf.Max(lfRangeInTiles, 0.001f), mfIntensityRangeExponent);

        fApplyRemasterCarriedLight();

        mOPlayerLight.intensity = mfBaseIntensity;

        mfDebugLightIntensity = mOPlayerLight.intensity;
        mfDebugLightRange = mOPlayerLight.range;
        mODebugLightColour = mOPlayerLight.color;
    }

    private Light fFindPlayerLight()
    {
        GameObject lOExisting = GameObject.Find(PlayerLightName);

        if (lOExisting != null)
        {
            Light lOLight = lOExisting.GetComponent<Light>();

            if (lOLight != null)
                return lOLight;
        }

        // No existing light in the scene: create one at the player.
        GameObject lOPlayer = UWScene.Player;

        if (lOPlayer == null)
            return null;

        GameObject lOObject = new GameObject(PlayerLightName);
        lOObject.transform.SetParent(lOPlayer.transform, false);
        lOObject.transform.localPosition = new Vector3(0f, 16f, 0f);

        return lOObject.AddComponent<Light>();
    }

    private void Update()
    {
        // Anyone who changes position or strength of the carried light in the Inspector in
        // Remastered mode should see it immediately - otherwise UWLighting only sets up the light
        // anew for a new light source.
        fRefreshOnRemasterChange();

        if (mOPlayerLight == null || !mOPlayerLight.enabled || mfFlickerAmount <= 0f)
            return;

        // Two superimposed noise values so that the flicker does not look periodic.
        float lfNoise = Mathf.PerlinNoise(Time.time * mfFlickerSpeed, 0f)
            + Mathf.PerlinNoise(0f, Time.time * mfFlickerSpeed * 0.37f);

        float lfAmount = mfFlickerAmount;
        UnderworldRevisited.UWSettings lOFireSettings = mbCarriedOpenFire ? UnderworldRevisited.UWSettings.Instance : null;

        // Open fire in the hand flickers in Remastered mode like the placed fires,
        // and its light jitters so that the shadows dance (see UWLightFlicker).
        if (lOFireSettings != null)
        {
            lfAmount = lOFireSettings.RemasterFireFlicker;

            Vector3 lOWobble = new Vector3(
                Mathf.PerlinNoise(3.1f, Time.time * 6.3f) - 0.5f,
                Mathf.PerlinNoise(7.7f, Time.time * 5.1f) - 0.5f,
                Mathf.PerlinNoise(11.9f, Time.time * 6.9f) - 0.5f);

            mOPlayerLight.transform.localPosition = mOCarriedBasePosition
                + (lOWobble * (2f * lOFireSettings.RemasterFireJitter));
        }

        float lfFactor = 1f + ((lfNoise - 1f) * lfAmount);

        mOPlayerLight.intensity = mfBaseIntensity * lfFactor;
    }
}
