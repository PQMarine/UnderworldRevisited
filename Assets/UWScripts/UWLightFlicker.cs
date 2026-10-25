using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Makes a light burn restlessly: noise on the intensity, and for open fire additionally a
/// slight jitter of the light position and a sprite self-glow that flickers along.
///
/// The formulas - THE JITTER, the TWO NOISE VALUES layered - are UWFlicker's since cleanup step
/// 6.4 (2026-10-24), shared with the carried light (UWLighting) and the palette light sources
/// (UWPaletteEffects); this component applies them to a Light and its renderers.
/// </summary>
public class UWLightFlicker : MonoBehaviour
{
    /// <summary>Unity's noise for UWFlicker's formulas, one cached delegate (no allocation per
    /// frame).</summary>
    public static readonly UWFlicker.Noise2D Perlin = Mathf.PerlinNoise;

    private Light mOLight;

    private float mfBaseIntensity;

    private float mfAmount;

    private float mfJitter;

    private float mfSeed;

    private Vector3 mOBasePosition;

    private Renderer[] mORenderers;

    private float mfGlow;

    /// <summary>How strongly the sprite's glow flickers along - independent of the light and
    /// much weaker.</summary>
    private float mfGlowFlicker;

    private MaterialPropertyBlock mOBlock;

    public void Initialise(float pfBaseIntensity, float pfAmount, float pfJitter,
        Renderer[] pORenderers, float pfGlow, float pfGlowFlicker)
    {
        mfGlowFlicker = pfGlowFlicker;
        mOLight = GetComponent<Light>();
        mfBaseIntensity = pfBaseIntensity;
        mfAmount = pfAmount;
        mfJitter = pfJitter;
        mORenderers = pORenderers;
        mfGlow = pfGlow;
        mfSeed = Random.Range(0f, 100f);
        mOBasePosition = transform.localPosition;
    }

    private void Update()
    {
        if (mOLight == null)
            return;

        float lfTime = Time.time;

        float lfNoise = UWFlicker.PlacedNoise(Perlin, mfSeed, lfTime);

        mOLight.intensity = mfBaseIntensity * UWFlicker.PlacedFactor(mfAmount, lfNoise);

        if (mfJitter > 0f)
        {
            UWFlicker.Jitter(Perlin, mfSeed, lfTime, mfJitter, out float lfX, out float lfY, out float lfZ);

            transform.localPosition = mOBasePosition + new Vector3(lfX, lfY, lfZ);
        }

        if (mORenderers != null && mfGlow > 0f)
            SetGlow(mORenderers, mfGlow * UWFlicker.PlacedFactor(mfGlowFlicker, lfNoise), ref mOBlock);
    }

    /// <summary>
    /// Sets the self-glow on all renderers of an object WITHOUT losing other values of their
    /// property block - critters, for example, keep their current frame in it.
    /// Hence read first, then write.
    /// </summary>
    public static void SetGlow(Renderer[] pORenderers, float pfGlow, ref MaterialPropertyBlock pOBlock)
    {
        if (pORenderers == null)
            return;

        if (pOBlock == null)
            pOBlock = new MaterialPropertyBlock();

        for (int liAt = 0; liAt < pORenderers.Length; liAt++)
        {
            Renderer lORenderer = pORenderers[liAt];

            if (lORenderer == null)
                continue;

            lORenderer.GetPropertyBlock(pOBlock);
            pOBlock.SetFloat(UWShaderIds.SpriteGlow, pfGlow);
            lORenderer.SetPropertyBlock(pOBlock);
        }
    }
}
