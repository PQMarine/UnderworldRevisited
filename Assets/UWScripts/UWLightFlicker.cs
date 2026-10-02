using UnityEngine;

/// <summary>
/// Makes a light burn restlessly: noise on the intensity, and for open fire additionally a
/// slight jitter of the light position and a sprite self-glow that flickers along.
///
/// THE JITTER is the real gain: a light that only gets brighter and darker
/// looks like a bad light bulb. If it also wanders by one or two units, the shadows
/// dance on the wall - that is what fire looks like (per user, 2026-09-12: "open fire could
/// flicker a bit more").
///
/// TWO NOISE VALUES layered, a slow one for the rise and fall of the flame and a
/// fast one for the twitching - with only one it looks periodic.
///
/// The player light has its own flicker in UWLighting.
/// </summary>
public class UWLightFlicker : MonoBehaviour
{
    private const string GlowProperty = "_SpriteGlow";

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

        float lfNoise = (Mathf.PerlinNoise(mfSeed, lfTime * 4.1f) * 0.65f)
            + (Mathf.PerlinNoise(mfSeed + 17.3f, lfTime * 11.7f) * 0.35f);

        float lfFactor = 1f - (mfAmount * lfNoise);

        mOLight.intensity = mfBaseIntensity * lfFactor;

        if (mfJitter > 0f)
        {
            Vector3 lOWobble = new Vector3(
                Mathf.PerlinNoise(mfSeed + 3.1f, lfTime * 6.3f) - 0.5f,
                Mathf.PerlinNoise(mfSeed + 7.7f, lfTime * 5.1f) - 0.5f,
                Mathf.PerlinNoise(mfSeed + 11.9f, lfTime * 6.9f) - 0.5f);

            transform.localPosition = mOBasePosition + (lOWobble * (2f * mfJitter));
        }

        if (mORenderers != null && mfGlow > 0f)
            SetGlow(mORenderers, mfGlow * (1f - (mfGlowFlicker * lfNoise)), ref mOBlock);
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
            pOBlock.SetFloat(GlowProperty, pfGlow);
            lORenderer.SetPropertyBlock(pOBlock);
        }
    }
}
