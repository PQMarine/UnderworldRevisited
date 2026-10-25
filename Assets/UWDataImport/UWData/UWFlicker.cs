using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FLICKER RULES in one place (cleanup step 6.4, 2026-10-24). Until this step there were
	/// three formulas in three Unity classes, two of them identical in their jitter: the placed
	/// Remastered lights (UWLightFlicker), the carried light (UWLighting) and the palette light
	/// sources (UWPaletteEffects). Which source flickers at all is the table's (UWLightSourceTable:
	/// Flicker, OpenFire).
	///
	/// THE NOISE IS THE FRONT END'S: every formula takes a two-dimensional noise in 0..1 (Unity
	/// passes Mathf.PerlinNoise, see UWLightFlicker.Perlin), so the shapes, rates and amplitudes
	/// stay here and the picture stays exactly what Unity's noise gave before the step. Every rate
	/// is per second of the front end's clock (Unity: Time.time), not per frame.
	///
	/// THE ORIGINAL'S OWN FLICKER is no noise at all: it rotates colour ranges of the palette
	/// (UWPaletteRotation) - the flames of wall torches, the torch and candle icons, the light
	/// spell's icon. RotationStep is that clock, shared by the palette path's rotation table (world
	/// and icons, UWRenderPathHost) and the Remastered pre-baked texture steps
	/// (UWAnimatedTextures).
	/// </summary>
	public static class UWFlicker
	{
		/// <summary>A noise in 0..1 over two coordinates.</summary>
		public delegate float Noise2D(float pfX, float pfY);

		// ------------------------------------------------- Placed lights (Remastered)

		/// <summary>
		/// The placed light's noise, 0..1: TWO NOISE VALUES layered, a slow one for the rise and
		/// fall of the flame and a fast one for the twitching - with only one it looks periodic.
		/// The seed is the light's own (random per light), so no two lights flicker alike.
		/// </summary>
		public static float PlacedNoise(Noise2D pONoise, float pfSeed, float pfTime)
		{
			return (pONoise(pfSeed, pfTime * 4.1f) * 0.65f)
				+ (pONoise(pfSeed + 17.3f, pfTime * 11.7f) * 0.35f);
		}

		/// <summary>The factor on a placed light's intensity (and on its sprite's glow, with the
		/// much weaker RemasterSpriteFlicker as the amount): the noise only ever dims.</summary>
		public static float PlacedFactor(float pfAmount, float pfNoise)
		{
			return 1f - (pfAmount * pfNoise);
		}

		/// <summary>
		/// THE JITTER of open fire, the real gain: a light that only gets brighter and darker looks
		/// like a bad light bulb. If it also wanders by one or two units, the shadows dance on the
		/// wall - that is what fire looks like (per user, 2026-09-12: "open fire could flicker a bit
		/// more"). The offset from the light's resting place, in world units; the carried light
		/// uses seed 0.
		/// </summary>
		public static void Jitter(Noise2D pONoise, float pfSeed, float pfTime, float pfJitter,
			out float pfX, out float pfY, out float pfZ)
		{
			float lfScale = 2f * pfJitter;

			pfX = (pONoise(pfSeed + 3.1f, pfTime * 6.3f) - 0.5f) * lfScale;
			pfY = (pONoise(pfSeed + 7.7f, pfTime * 5.1f) - 0.5f) * lfScale;
			pfZ = (pONoise(pfSeed + 11.9f, pfTime * 6.9f) - 0.5f) * lfScale;
		}

		// ------------------------------------------------- Carried light

		/// <summary>The carried light's noise, 0..2: two superimposed noise values so that the
		/// flicker does not look periodic; the speed is UWLighting's Inspector value.</summary>
		public static float CarriedNoise(Noise2D pONoise, float pfTime, float pfSpeed)
		{
			return pONoise(pfTime * pfSpeed, 0f)
				+ pONoise(0f, pfTime * pfSpeed * 0.37f);
		}

		/// <summary>The factor on the carried light's intensity: around 1, by the amount (the
		/// Inspector's, or RemasterFireFlicker for open fire in the hand in Remastered).</summary>
		public static float CarriedFactor(float pfAmount, float pfNoise)
		{
			return 1f + ((pfNoise - 1f) * pfAmount);
		}

		// ------------------------------------------------- Palette light sources

		/// <summary>How far the light level of open fire swings, in levels.</summary>
		public const float PaletteFireSwing = 0.5f;

		/// <summary>A palette source's phase of the flicker, from where it stood when found (world
		/// units), so two fires side by side do not swing together.</summary>
		public static float PalettePhase(float pfX, float pfZ)
		{
			return (pfX * 0.137f) + (pfZ * 0.711f);
		}

		/// <summary>The light level of a changing palette source (open fire, a creature): its
		/// base level swung by up to PaletteFireSwing either way.</summary>
		public static float PaletteFireLevel(Noise2D pONoise, float pfPhase, float pfTime, float pfBaseLevel)
		{
			float lfNoise = pONoise(pfPhase, pfTime * 3f) - 0.5f;

			return pfBaseLevel + (lfNoise * 2f * PaletteFireSwing);
		}

		// ------------------------------------------------- The original's palette rotation

		/// <summary>The palette rotation's step at this time (UWSettings.PaletteRotationStepsPerSecond,
		/// independent of the frame rate); the caller wraps it to the table's step count.</summary>
		public static int RotationStep(float pfTime, float pfStepsPerSecond)
		{
			// Through a float, as Unity's Mathf.FloorToInt(float) took it before the step.
			float lfSteps = pfTime * pfStepsPerSecond;

			return (int)Math.Floor(lfSteps);
		}
	}
}
