using System;
using System.Globalization;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 6.4 (2026-10-24): the light sources, the flicker and the ground-shadow rule out
    /// of the Unity classes into UWLightSourceTable, UWFlicker and UWGroundShadowRule.
    ///
    /// THE TABLE PINS: every value of every row against the literals of UWRemasterLights.mORecipes
    /// and UWPaletteEffects.msLightLevels as they were before the step (modernisierung c4ab2e34),
    /// and the lava's numbers from both classes.
    ///
    /// THE FORMULA PINS: each UWFlicker and UWGroundShadowRule function against a line-by-line
    /// transcription of the expression it replaced (UWLightFlicker.Update, UWLighting.Update,
    /// UWPaletteEffects.fUpdateLights/fCollectSources, UWPaletteRenderToggle's rotation step,
    /// UWRemasterGroundShadows.TryAddShadow/fPlace, UWPaletteEffects.fCollectCasters), on a grid
    /// of inputs and a stand-in noise, compared bit for bit: the same noise gives the same float.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckLightSources()
        {
            Check.Group("Light sources, flicker, ground shadows (cleanup step 6.4)");

            fCheckLightSourceRows();
            fCheckFlickerFormulas();
            fCheckGroundShadowRule();
        }

        /// <summary>One expected row: Id, Tiles, Brightness, Flicker, Shadows, OpenFire, Glow,
        /// Fire, the palette light level.</summary>
        private static readonly object[][] msExpectedLightRows =
        {
            new object[] { 145, 3f,   0.8f,  1f,    true,  true,  1.5f, 1f,    2f }, // torch
            new object[] { 146, 1.5f, 0.5f,  0.9f,  false, true,  1.2f, 0.35f, 1f }, // candle
            new object[] { 144, 3.5f, 0.9f,  0.08f, true,  false, 1f,   0f,    3f }, // lantern
            new object[] { 298, 4f,   1.1f,  1.2f,  true,  true,  1.8f, 1.8f,  3f }, // campfire
            new object[] { 120, 4f,   1.0f,  1.2f,  false, true,  1.5f, 1.4f,  3f }, // fire elemental
            new object[] { 297, 2f,   0.35f, 0f,    false, false, 1f,   0f,    1f }, // glowing stone
            new object[] { 184, 1.5f, 0.25f, 0f,    false, false, 0.8f, 0f,    1f }, // glowing mushroom
            new object[] { 279, 2.5f, 0.6f,  0f,    false, false, 1f,   0f,    2f }, // orb
            new object[] { 274, 2.5f, 0.6f,  0f,    false, false, 1f,   0f,    2f }, // orb stone
        };

        private static void fCheckLightSourceRows()
        {
            Check.Int("lights: nine glowing things", UWLightSourceTable.Count, msExpectedLightRows.Length);

            for (int liAt = 0; liAt < msExpectedLightRows.Length; liAt++)
            {
                object[] lOExpected = msExpectedLightRows[liAt];
                int liId = (int)lOExpected[0];
                string lsName = "lights: id " + liId;

                Check.Int(lsName + " in table order", UWLightSourceTable.GetRow(liAt).Id, liId);

                if (!UWLightSourceTable.TryGet(liId, out UWLightSourceTable.Row lORow))
                {
                    Check.Fail(lsName, "not in the table");

                    continue;
                }

                fFloatPin(lsName + " tiles", lORow.Tiles, (float)lOExpected[1]);
                fFloatPin(lsName + " brightness", lORow.Brightness, (float)lOExpected[2]);
                fFloatPin(lsName + " flicker", lORow.Flicker, (float)lOExpected[3]);
                Check.Bool(lsName + " shadows", lORow.Shadows, (bool)lOExpected[4]);
                Check.Bool(lsName + " open fire", lORow.OpenFire, (bool)lOExpected[5]);
                Check.Bool(lsName + " IsOpenFire", UWLightSourceTable.IsOpenFire(liId), (bool)lOExpected[5]);
                fFloatPin(lsName + " glow", lORow.Glow, (float)lOExpected[6]);
                fFloatPin(lsName + " fire", lORow.Fire, (float)lOExpected[7]);
                fFloatPin(lsName + " palette level", lORow.PaletteLightLevel, (float)lOExpected[8]);
                Check.Bool(lsName + " is a light source", UWLightSourceTable.IsLightSource(liId), true);
            }

            // The lit carried lights (148-151), the taper and other things are not placed sources.
            foreach (int liId in new[] { 0, 147, 148, 149, 150, 151, 299, 0x40, 1023 })
                Check.Bool("lights: id " + liId + " is no light source", UWLightSourceTable.IsLightSource(liId), false);

            fFloatPin("lights: fallback colour red", UWLightSourceTable.FallbackRed, 1f);
            fFloatPin("lights: fallback colour green", UWLightSourceTable.FallbackGreen, 0.78f);
            fFloatPin("lights: fallback colour blue", UWLightSourceTable.FallbackBlue, 0.5f);

            fFloatPin("lights: lava tiles (Remastered)", UWLightSourceTable.LavaTiles, 3f);
            fFloatPin("lights: lava brightness (Remastered)", UWLightSourceTable.LavaBrightness, 0.7f);
            fFloatPin("lights: lava flicker (Remastered)", UWLightSourceTable.LavaFlicker, 0.12f);
            fFloatPin("lights: lava red", UWLightSourceTable.LavaRed, 1f);
            fFloatPin("lights: lava green", UWLightSourceTable.LavaGreen, 0.45f);
            fFloatPin("lights: lava blue", UWLightSourceTable.LavaBlue, 0.12f);
            fFloatPin("lights: lava level (palette)", UWLightSourceTable.LavaPaletteLightLevel, 1f);
            fFloatPin("lights: lava dimming (palette)", UWLightSourceTable.LavaPaletteDim, 3f);
        }

        /// <summary>A stand-in for Mathf.PerlinNoise: any deterministic 0..1 function will do, the
        /// pins compare the same noise through the old and the new expression.</summary>
        private static float fStandInNoise(float pfX, float pfY)
        {
            double ldValue = Math.Sin((pfX * 12.9898) + (pfY * 78.233)) * 43758.5453;

            return (float)(ldValue - Math.Floor(ldValue));
        }

        private static readonly UWFlicker.Noise2D msStandInNoise = fStandInNoise;

        private static void fCheckFlickerFormulas()
        {
            Func<float, float, float> P = fStandInNoise;
            UWFlicker.Noise2D lONoise = msStandInNoise;
            int liMismatch = 0;
            int liCases = 0;

            float[] lfTimes = { 0f, 0.016f, 0.5f, 1f, 3.3f, 17.25f, 123.456f, 3600.5f };
            float[] lfSeeds = { 0f, 12.5f, 57.31f, 99.99f };
            float[] lfAmounts = { 0f, 0.06f, 0.1f, 0.35f, 1.2f };

            foreach (float lfTime in lfTimes)
            {
                foreach (float lfSeed in lfSeeds)
                {
                    // UWLightFlicker.Update before the step.
                    float lfOldNoise = (P(lfSeed, lfTime * 4.1f) * 0.65f)
                        + (P(lfSeed + 17.3f, lfTime * 11.7f) * 0.35f);
                    float lfNewNoise = UWFlicker.PlacedNoise(lONoise, lfSeed, lfTime);

                    liCases++;
                    liMismatch += lfOldNoise == lfNewNoise ? 0 : 1;

                    foreach (float lfAmount in lfAmounts)
                    {
                        float lfOldFactor = 1f - (lfAmount * lfOldNoise);

                        liCases++;
                        liMismatch += lfOldFactor == UWFlicker.PlacedFactor(lfAmount, lfNewNoise) ? 0 : 1;

                        // The jitter: (noise - 0.5) * (2 * jitter) per axis.
                        float lfScale = 2f * lfAmount;
                        float lfOldX = (P(lfSeed + 3.1f, lfTime * 6.3f) - 0.5f) * lfScale;
                        float lfOldY = (P(lfSeed + 7.7f, lfTime * 5.1f) - 0.5f) * lfScale;
                        float lfOldZ = (P(lfSeed + 11.9f, lfTime * 6.9f) - 0.5f) * lfScale;

                        UWFlicker.Jitter(lONoise, lfSeed, lfTime, lfAmount, out float lfX, out float lfY, out float lfZ);

                        liCases++;
                        liMismatch += lfOldX == lfX && lfOldY == lfY && lfOldZ == lfZ ? 0 : 1;
                    }
                }

                // UWLighting.Update before the step (speed 6.5, the Inspector default).
                foreach (float lfSpeed in new[] { 6.5f, 1f, 10f })
                {
                    float lfOldNoise = P(lfTime * lfSpeed, 0f) + P(0f, lfTime * lfSpeed * 0.37f);
                    float lfNewNoise = UWFlicker.CarriedNoise(lONoise, lfTime, lfSpeed);

                    liCases++;
                    liMismatch += lfOldNoise == lfNewNoise ? 0 : 1;

                    foreach (float lfAmount in lfAmounts)
                    {
                        liCases++;
                        liMismatch += (1f + ((lfOldNoise - 1f) * lfAmount)) == UWFlicker.CarriedFactor(lfAmount, lfNewNoise) ? 0 : 1;
                    }
                }

                // UWPaletteEffects before the step: the phase from the place, the swing of 0.5 levels.
                foreach (float lfX in new[] { 0f, 96f, 1234.5f, 4000f })
                {
                    foreach (float lfZ in new[] { 0f, 32f, 2047.75f })
                    {
                        float lfOldPhase = (lfX * 0.137f) + (lfZ * 0.711f);
                        float lfNewPhase = UWFlicker.PalettePhase(lfX, lfZ);

                        liCases++;
                        liMismatch += lfOldPhase == lfNewPhase ? 0 : 1;

                        foreach (float lfBase in new[] { 1f, 2f, 3f })
                        {
                            float lfOldNoise = P(lfOldPhase, lfTime * 3f) - 0.5f;
                            float lfOldLevel = lfBase + (lfOldNoise * 2f * 0.5f);

                            liCases++;
                            liMismatch += lfOldLevel == UWFlicker.PaletteFireLevel(lONoise, lfNewPhase, lfTime, lfBase) ? 0 : 1;
                        }
                    }
                }

                // The palette rotation's step (Mathf.FloorToInt(Time.time * steps)).
                foreach (float lfSteps in new[] { 4f, 1f, 7.5f, 0f })
                {
                    float lfProduct = lfTime * lfSteps;

                    liCases++;
                    liMismatch += (int)Math.Floor(lfProduct) == UWFlicker.RotationStep(lfTime, lfSteps) ? 0 : 1;
                }
            }

            Check.Int("flicker: " + liCases + " cases equal the expressions before the step", liMismatch, 0);

            Check.Int("flicker: rotation step at 2.99 s, 4 a second", UWFlicker.RotationStep(2.99f, 4f), 11);
            Check.Int("flicker: rotation step at 3 s, 4 a second", UWFlicker.RotationStep(3f, 4f), 12);
            fFloatPin("flicker: palette fire swing", UWFlicker.PaletteFireSwing, 0.5f);
        }

        private static void fCheckGroundShadowRule()
        {
            Check.Bool("ground shadow: a key casts one", UWGroundShadowRule.Casts(0x100, false), true);
            Check.Bool("ground shadow: nothing in flight", UWGroundShadowRule.Casts(0x100, true), false);

            for (int liAt = 0; liAt < UWLightSourceTable.Count; liAt++)
            {
                int liId = UWLightSourceTable.GetRow(liAt).Id;

                Check.Bool("ground shadow: light source " + liId + " casts none", UWGroundShadowRule.Casts(liId, false), false);
            }

            fFloatPin("ground shadow: smallest diameter", UWGroundShadowRule.MinDiameter, 10f);
            fFloatPin("ground shadow: width without a collider", UWGroundShadowRule.FallbackWidth, 16f);
            fFloatPin("ground shadow: palette levels at most", UWGroundShadowRule.MaxPaletteLevels, 6f);

            int liMismatch = 0;
            int liCases = 0;
            float[] lfSizes = { 0f, 2f, 9.99f, 10f, 16f, 23.5f, 64f, 120f };

            foreach (float lfX in lfSizes)
            {
                foreach (float lfZ in lfSizes)
                {
                    // Mathf.Max(size.x, size.z), the collider's larger side.
                    float lfOldWidth = lfX > lfZ ? lfX : lfZ;
                    float lfWidth = UWGroundShadowRule.Width(true, lfX, lfZ);

                    liCases++;
                    liMismatch += lfOldWidth == lfWidth ? 0 : 1;

                    // UWPaletteEffects.fCollectCasters: Mathf.Max(10, width) * 0.5.
                    liCases++;
                    liMismatch += ((10f > lfOldWidth ? 10f : lfOldWidth) * 0.5f) == UWGroundShadowRule.PaletteRadius(lfWidth) ? 0 : 1;

                    // UWRemasterGroundShadows.fPlace: Mathf.Max(10, width * size).
                    foreach (float lfSize in new[] { 0.5f, 1f, 1.3f, 2f })
                    {
                        float lfProduct = lfOldWidth * lfSize;

                        liCases++;
                        liMismatch += (10f > lfProduct ? 10f : lfProduct) == UWGroundShadowRule.RemasterDiameter(lfWidth, lfSize) ? 0 : 1;
                    }
                }
            }

            Check.Int("ground shadow: " + liCases + " sizes equal the expressions before the step", liMismatch, 0);
            fFloatPin("ground shadow: no collider", UWGroundShadowRule.Width(false, 40f, 40f), 16f);
        }

        private static void fFloatPin(string psName, float pfActual, float pfExpected)
        {
            Check.Str(psName, pfActual.ToString("R", CultureInfo.InvariantCulture), pfExpected.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
