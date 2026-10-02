using UnityEngine;

/// <summary>
/// Sparks and smoke at open fire and dust in the air, only in the Remastered render mode
/// (per user, 2026-09-13: "Carry on with fire and dust"). Item 1 of the planned
/// effects in Todo.md.
///
/// WHY: the flames are flat images. With sparks and some smoke rising above them,
/// they look like a fire in the room. Dust that only lights up in the light makes the air
/// visible and gives the corridor depth.
///
/// Everything simulated in WORLD SPACE: a spark stays where it rose, even when the
/// light source flickers (UWLightFlicker) or moves (fire elemental), and the dust
/// stays put in the air when the player walks.
///
/// Dimensions in world units: a tile is 64 wide, the player 40 tall.
/// </summary>
public static class UWRemasterParticles
{
    private const string ShaderName = "UW/Particle";

    private static Material mOSparkMaterial;

    private static Material mOSmokeMaterial;

    private static Material mODustMaterial;

    /// <summary>
    /// Sparks and smoke for a fire. pfSize is the size of the fire: 1 for
    /// a torch, less for a candle, more for a campfire.
    /// </summary>
    public static void AddFire(Transform pOParent, Color pOColour, float pfSize,
        float pfSparks, float pfSmoke)
    {
        if (pfSize <= 0f)
            return;

        if (pfSparks > 0f && fEnsureMaterials())
            fAddSparks(pOParent, pOColour, pfSize, pfSparks);

        if (pfSmoke > 0f && fEnsureMaterials())
            fAddSmoke(pOParent, pfSize, pfSmoke);
    }

    /// <summary>
    /// Dust in front of the camera. It is spawned everywhere but only visible in the light - the
    /// shader tints it with the point lights. Returns the created object so that it
    /// can be removed during cleanup.
    /// </summary>
    public static GameObject AddDust(Transform pOCamera, float pfAmount)
    {
        if (pOCamera == null || pfAmount <= 0f || !fEnsureMaterials())
            return null;

        GameObject lOObject = new GameObject("Remastered Dust");

        lOObject.transform.SetParent(pOCamera, false);
        // The box lies in front of the eye: where the light in the hand falls.
        lOObject.transform.localPosition = new Vector3(0f, 0f, 70f);

        ParticleSystem lOSystem = fCreateSystem(lOObject, mODustMaterial);

        ParticleSystem.MainModule lOMain = lOSystem.main;

        lOMain.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        lOMain.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.6f);
        // Small and muted: close to the fire, larger, brighter grains turned into white
        // discs that looked more like balls of light (test image 2026-09-13).
        lOMain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        lOMain.startColor = new Color(0.7f, 0.65f, 0.58f, 0.7f);
        lOMain.maxParticles = Mathf.RoundToInt(260 * pfAmount);
        lOMain.gravityModifier = 0f;

        ParticleSystem.EmissionModule lOEmission = lOSystem.emission;

        lOEmission.rateOverTime = 30f * pfAmount;

        ParticleSystem.ShapeModule lOShape = lOSystem.shape;

        lOShape.enabled = true;
        lOShape.shapeType = ParticleSystemShapeType.Box;
        lOShape.scale = new Vector3(150f, 90f, 130f);

        // Slow drifting instead of straight flight.
        ParticleSystem.NoiseModule lONoise = lOSystem.noise;

        lONoise.enabled = true;
        lONoise.strength = 1.5f;
        lONoise.frequency = 0.15f;
        lONoise.scrollSpeed = 0.1f;

        fFadeInOut(lOSystem, 0.2f);

        lOSystem.Play();

        return lOObject;
    }

    private static void fAddSparks(Transform pOParent, Color pOColour, float pfSize, float pfAmount)
    {
        GameObject lOObject = new GameObject("Remastered Sparks");

        lOObject.transform.SetParent(pOParent, false);

        ParticleSystem lOSystem = fCreateSystem(lOObject, mOSparkMaterial);

        ParticleSystem.MainModule lOMain = lOSystem.main;

        lOMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        lOMain.startSpeed = new ParticleSystem.MinMaxCurve(10f * pfSize, 28f * pfSize);
        // Below a good one world unit in size, a spark at two tiles distance is barely
        // one pixel big (test image 2026-09-13).
        lOMain.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        // Brighter than the light colour, shifted towards yellow: a spark glows hotter than the
        // glow the fire casts on the wall. Bloom picks it up.
        Color lOHot = Color.Lerp(pOColour, new Color(1f, 0.85f, 0.4f), 0.5f);

        lOMain.startColor = new ParticleSystem.MinMaxGradient(lOHot, pOColour);
        lOMain.maxParticles = 60;
        // Pulled down slightly so that they die out in an arc.
        lOMain.gravityModifier = 0.02f * pfSize;

        ParticleSystem.EmissionModule lOEmission = lOSystem.emission;

        lOEmission.rateOverTime = 9f * pfSize * pfAmount;

        ParticleSystem.ShapeModule lOShape = lOSystem.shape;

        lOShape.enabled = true;
        lOShape.shapeType = ParticleSystemShapeType.Cone;
        lOShape.angle = 18f;
        lOShape.radius = 3f * pfSize;
        // Otherwise the cone points along z - rotate it upwards.
        lOShape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.NoiseModule lONoise = lOSystem.noise;

        lONoise.enabled = true;
        lONoise.strength = 6f * pfSize;
        lONoise.frequency = 0.6f;
        lONoise.scrollSpeed = 1.2f;

        // Sparks get smaller as they die out.
        ParticleSystem.SizeOverLifetimeModule lOSize = lOSystem.sizeOverLifetime;

        lOSize.enabled = true;
        lOSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        fFadeInOut(lOSystem, 0.05f);

        lOSystem.Play();
    }

    private static void fAddSmoke(Transform pOParent, float pfSize, float pfAmount)
    {
        GameObject lOObject = new GameObject("Remastered Smoke");

        lOObject.transform.SetParent(pOParent, false);
        lOObject.transform.localPosition = new Vector3(0f, 6f * pfSize, 0f);

        ParticleSystem lOSystem = fCreateSystem(lOObject, mOSmokeMaterial);

        ParticleSystem.MainModule lOMain = lOSystem.main;

        lOMain.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
        lOMain.startSpeed = new ParticleSystem.MinMaxCurve(5f * pfSize, 10f * pfSize);
        lOMain.startSize = new ParticleSystem.MinMaxCurve(5f * pfSize, 9f * pfSize);
        // Dark grey, tinted by the fire below (the material is lit). Light
        // smoke was just as bright as the lit wall behind it and vanished - 24
        // particles present, nothing visible (test image 2026-09-13). In front of a bright wall it has to
        // darken, in front of a dark one glow slightly.
        lOMain.startColor = new Color(0.18f, 0.17f, 0.16f, 0.7f * Mathf.Clamp01(pfAmount));
        lOMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        lOMain.maxParticles = 40;

        ParticleSystem.EmissionModule lOEmission = lOSystem.emission;

        lOEmission.rateOverTime = 4f * pfSize;

        ParticleSystem.ShapeModule lOShape = lOSystem.shape;

        lOShape.enabled = true;
        lOShape.shapeType = ParticleSystemShapeType.Cone;
        lOShape.angle = 8f;
        lOShape.radius = 2f * pfSize;
        lOShape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.NoiseModule lONoise = lOSystem.noise;

        lONoise.enabled = true;
        lONoise.strength = 3f * pfSize;
        lONoise.frequency = 0.25f;
        lONoise.scrollSpeed = 0.3f;

        // Smoke spreads out as it rises.
        ParticleSystem.SizeOverLifetimeModule lOSize = lOSystem.sizeOverLifetime;

        lOSize.enabled = true;
        lOSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));

        fFadeInOut(lOSystem, 0.25f);

        lOSystem.Play();
    }

    private static ParticleSystem fCreateSystem(GameObject pOObject, Material pOMaterial)
    {
        ParticleSystem lOSystem = pOObject.AddComponent<ParticleSystem>();

        // A freshly created system starts running immediately with Unity's defaults - stop it first,
        // otherwise duration and loop cannot be set.
        lOSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule lOMain = lOSystem.main;

        lOMain.duration = 5f;
        lOMain.loop = true;
        lOMain.prewarm = true;
        lOMain.simulationSpace = ParticleSystemSimulationSpace.World;
        lOMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
        lOMain.playOnAwake = true;

        ParticleSystemRenderer lORenderer = pOObject.GetComponent<ParticleSystemRenderer>();

        lORenderer.sharedMaterial = pOMaterial;
        lORenderer.renderMode = ParticleSystemRenderMode.Billboard;
        lORenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lORenderer.receiveShadows = false;

        return lOSystem;
    }

    /// <summary>Fade in and out smoothly instead of popping.</summary>
    private static void fFadeInOut(ParticleSystem pOSystem, float pfEdge)
    {
        ParticleSystem.ColorOverLifetimeModule lOColour = pOSystem.colorOverLifetime;
        Gradient lOGradient = new Gradient();

        lOGradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, pfEdge),
                new GradientAlphaKey(1f, 1f - pfEdge),
                new GradientAlphaKey(0f, 1f)
            });

        lOColour.enabled = true;
        lOColour.color = lOGradient;
    }

    private static bool fEnsureMaterials()
    {
        if (mOSparkMaterial != null && mOSmokeMaterial != null && mODustMaterial != null)
            return true;

        Shader lOShader = Shader.Find(ShaderName);

        if (lOShader == null)
        {
            Debug.LogError("Shader " + ShaderName + " not found - no particles.");
            return false;
        }

        mOSparkMaterial = new Material(lOShader) { name = "Remastered Sparks" };
        // Not too bright, otherwise the spark turns white instead of orange after tonemapping.
        mOSparkMaterial.SetFloat("_Intensity", 1.8f);
        mOSparkMaterial.SetFloat("_Additive", 1f);
        mOSparkMaterial.SetFloat("_Lit", 0f);
        mOSparkMaterial.SetFloat("_Softness", 0.6f);

        mOSmokeMaterial = new Material(lOShader) { name = "Remastered Smoke" };
        mOSmokeMaterial.SetFloat("_Intensity", 1f);
        mOSmokeMaterial.SetFloat("_Additive", 0f);
        mOSmokeMaterial.SetFloat("_Lit", 1f);
        mOSmokeMaterial.SetFloat("_Softness", 1f);

        mODustMaterial = new Material(lOShader) { name = "Remastered Dust" };
        mODustMaterial.SetFloat("_Intensity", 0.8f);
        mODustMaterial.SetFloat("_Additive", 1f);
        mODustMaterial.SetFloat("_Lit", 1f);
        mODustMaterial.SetFloat("_Softness", 0.8f);

        return true;
    }
}
