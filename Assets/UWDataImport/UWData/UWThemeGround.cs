namespace UWDataImport.UWData
{
	/// <summary>
	/// THE CARVED THEMES' WINDOW GROUNDS AS PIXELS (cleanup phase 8d, 2026-10-25), engine-free like the
	/// HUD art builders (decision 26: the engine only uploads the finished picture). Four pictures a
	/// carved theme lays under a window (UWKitRows' grounds), each made once per theme and kept:
	///
	///   - GRADIENT: the icon's "stoneLight" - a radial gradient from the light stop at the top left
	///     through the middle to the dark - with the torch's warm light over it, stretched over the
	///     window as the SVG's gradient is over its block (both in the box's own units);
	///   - CLOUDS: seeded fractal value noise that tiles, coloured from the stone's three stops; laid
	///     at one texel per layout unit and repeated (CloudTileUnits), so a cloud keeps its size in
	///     every window;
	///   - LIGHT: what the clouds get of the gradient - the torch at the top left and the shadow
	///     towards the far corner - as one see-through picture stretched over the window;
	///   - GRAIN: the icon's feTurbulence grain as faint light and dark specks, repeated at one texel
	///     per screen pixel over either ground.
	///
	/// DETERMINISTIC: whole numbers only - 16.16 fixed point, an integer square root and an integer
	/// hash, no floating point and no Math call - so the bytes are the same on every machine, runtime
	/// and engine; the self-check pins a small picture of each (group WindowThemes).
	/// </summary>
	public static class UWThemeGround
	{
		/// <summary>The gradient's picture, stretched over the window (bilinear).</summary>
		public const int GradientSize = 256;

		/// <summary>The clouds' tile, and the layout units it covers: one texel per unit.</summary>
		public const int CloudTileSize = 512;

		public const int CloudTileUnits = 512;

		/// <summary>The clouds' light, stretched over the window (bilinear).</summary>
		public const int LightSize = 128;

		/// <summary>The grain's tile, one texel per screen pixel (point sampled).</summary>
		public const int GrainSize = 128;

		/// <summary>1.0 in 16.16 fixed point.</summary>
		private const long One = 65536;

		// ------------------------------------------------- The pictures

		/// <summary>The stone's radial light with the torch over it, opaque, piSize square.</summary>
		public static UWPicture Gradient(UWStoneLook pOLook, int piSize)
		{
			UWPicture lOPicture = new UWPicture(piSize, piSize);

			for (int y = 0; y < piSize; y++)
			{
				long llV = fCentreOf(y, piSize);

				for (int x = 0; x < piSize; x++)
				{
					long llU = fCentreOf(x, piSize);

					fStone(pOLook, fGradientAt(pOLook, llU, llV), out long llR, out long llG, out long llB);
					fTorch(pOLook, llU, llV, out long llTorchR, out long llTorchG, out long llTorchB, out long llTorchA);

					llR = fOver(llR, llTorchR, llTorchA);
					llG = fOver(llG, llTorchG, llTorchA);
					llB = fOver(llB, llTorchB, llTorchA);

					lOPicture.Set(x, y, new UWColor32(fByte(llR), fByte(llG), fByte(llB), 255));
				}
			}

			return lOPicture;
		}

		/// <summary>The cloud pattern, opaque, piSize square; it tiles (its lattice wraps). piSize should
		/// be a multiple of the finest octave's cells (CloudCells times 2 to the octaves less one).</summary>
		public static UWPicture Clouds(UWStoneLook pOLook, int piSize)
		{
			UWPicture lOPicture = new UWPicture(piSize, piSize);
			long llWeights = 0;

			for (int liOctave = 0; liOctave < pOLook.CloudOctaves; liOctave++)
				llWeights += One >> liOctave;

			for (int y = 0; y < piSize; y++)
			{
				for (int x = 0; x < piSize; x++)
				{
					long llSum = 0;

					for (int liOctave = 0; liOctave < pOLook.CloudOctaves; liOctave++)
					{
						int liCells = pOLook.CloudCells << liOctave;

						llSum += (fValueNoise(pOLook.CloudSeed, liOctave, liCells, x, y, piSize) * (One >> liOctave)) >> 16;
					}

					// The sum's middle spread by the contrast, then the stone's colour of it: dark at
					// 0, the middle at one half, light at one.
					long llNoise = (llSum << 16) / llWeights;

					llNoise = fClamp((((llNoise - (One / 2)) * pOLook.CloudContrast) / 1000) + (One / 2), 0, One);

					long llR;
					long llG;
					long llB;

					if (llNoise < One / 2)
					{
						long llAt = llNoise * 2;

						llR = fLerp(fChannel(pOLook.Dark, 16), fChannel(pOLook.Middle, 16), llAt);
						llG = fLerp(fChannel(pOLook.Dark, 8), fChannel(pOLook.Middle, 8), llAt);
						llB = fLerp(fChannel(pOLook.Dark, 0), fChannel(pOLook.Middle, 0), llAt);
					}
					else
					{
						long llAt = (llNoise - (One / 2)) * 2;

						llR = fLerp(fChannel(pOLook.Middle, 16), fChannel(pOLook.Light, 16), llAt);
						llG = fLerp(fChannel(pOLook.Middle, 8), fChannel(pOLook.Light, 8), llAt);
						llB = fLerp(fChannel(pOLook.Middle, 0), fChannel(pOLook.Light, 0), llAt);
					}

					lOPicture.Set(x, y, new UWColor32(fByte(fShade(pOLook, llR)), fByte(fShade(pOLook, llG)), fByte(fShade(pOLook, llB)), 255));
				}
			}

			return lOPicture;
		}

		/// <summary>
		/// The light the clouds get, piSize square and see-through: black towards the gradient's far
		/// side (CloudShadow at its radius, nothing inside its middle stop) and the torch over that, as
		/// one layer - drawn over a ground d it gives torch * a + d * (1 - shadow) * (1 - a), the two
		/// layers' result.
		/// </summary>
		public static UWPicture Light(UWStoneLook pOLook, int piSize)
		{
			UWPicture lOPicture = new UWPicture(piSize, piSize);
			long llMiddle = (pOLook.MiddleAt * One) / 1000;

			for (int y = 0; y < piSize; y++)
			{
				long llV = fCentreOf(y, piSize);

				for (int x = 0; x < piSize; x++)
				{
					long llU = fCentreOf(x, piSize);
					long llAt = fGradientAt(pOLook, llU, llV);
					long llShadow = llAt <= llMiddle ? 0 : (((llAt - llMiddle) << 16) / (One - llMiddle) * pOLook.CloudShadow) / 1000;

					fTorch(pOLook, llU, llV, out long llR, out long llG, out long llB, out long llTorch);

					long llAlpha = One - (((One - llShadow) * (One - llTorch)) >> 16);

					if (llAlpha <= 0)
					{
						lOPicture.Set(x, y, new UWColor32(0, 0, 0, 0));
						continue;
					}

					// The colour that, at llAlpha, puts the torch's share on: torch * a / alpha.
					lOPicture.Set(x, y, new UWColor32(fByte((llR * llTorch) / llAlpha), fByte((llG * llTorch) / llAlpha),
						fByte((llB * llTorch) / llAlpha), fByte(llAlpha * 255)));
				}
			}

			return lOPicture;
		}

		/// <summary>The grain, piSize square and see-through, tiling: each texel a light or dark speck of
		/// its own strength (two hashes added, so faint specks are the many and strong ones the few), at
		/// most GrainAlpha.</summary>
		public static UWPicture Grain(UWStoneLook pOLook, int piSize)
		{
			UWPicture lOPicture = new UWPicture(piSize, piSize);

			for (int y = 0; y < piSize; y++)
			{
				for (int x = 0; x < piSize; x++)
				{
					uint luHash = fHash(x, y, 1000, pOLook.GrainSeed);
					int liValue = (int)(luHash & 0xFF) + (int)((luHash >> 8) & 0xFF) - 255;
					int liAlpha = ((liValue < 0 ? -liValue : liValue) * pOLook.GrainAlpha + 127) / 255;
					byte lyLevel = liValue > 0 ? (byte)255 : (byte)0;

					lOPicture.Set(x, y, new UWColor32(lyLevel, lyLevel, lyLevel, (byte)liAlpha));
				}
			}

			return lOPicture;
		}

		// ------------------------------------------------- The stone and the torch

		/// <summary>Where a point stands in the stone's gradient: 0 at its centre, One at its radius and
		/// beyond (u, v in One of the box from the top left).</summary>
		private static long fGradientAt(UWStoneLook pOLook, long plU, long plV)
		{
			long llDistance = fDistance(plU - ((pOLook.CentreX * One) / 1000), plV - ((pOLook.CentreY * One) / 1000));

			return fClamp((llDistance * 1000) / pOLook.Radius, 0, One);
		}

		/// <summary>The stone's colour at a place of its gradient, in shade, channels in 16.16.</summary>
		private static void fStone(UWStoneLook pOLook, long plAt, out long plR, out long plG, out long plB)
		{
			long llMiddle = (pOLook.MiddleAt * One) / 1000;
			int liFrom;
			int liTo;
			long llAt;

			if (plAt <= llMiddle)
			{
				liFrom = pOLook.Light;
				liTo = pOLook.Middle;
				llAt = (plAt << 16) / llMiddle;
			}
			else
			{
				liFrom = pOLook.Middle;
				liTo = pOLook.Dark;
				llAt = ((plAt - llMiddle) << 16) / (One - llMiddle);
			}

			plR = fShade(pOLook, fLerp(fChannel(liFrom, 16), fChannel(liTo, 16), llAt));
			plG = fShade(pOLook, fLerp(fChannel(liFrom, 8), fChannel(liTo, 8), llAt));
			plB = fShade(pOLook, fLerp(fChannel(liFrom, 0), fChannel(liTo, 0), llAt));
		}

		/// <summary>The torch at a point: its colour (channels in 16.16) and its opacity (One = opaque),
		/// the icon's opacities times TorchStrength.</summary>
		private static void fTorch(UWStoneLook pOLook, long plU, long plV, out long plR, out long plG, out long plB, out long plAlpha)
		{
			long llDistance = fDistance(plU - ((pOLook.TorchX * One) / 1000), plV - ((pOLook.TorchY * One) / 1000));
			long llAt = fClamp((llDistance * 1000) / pOLook.TorchRadius, 0, One);
			long llOuterAt = (pOLook.TorchOuterAt * One) / 1000;
			long llInnerAlpha = (pOLook.TorchInnerAlpha * One) / 1000;
			long llOuterAlpha = (pOLook.TorchOuterAlpha * One) / 1000;
			long llAlpha;
			long llColourAt;

			if (llAt <= llOuterAt)
			{
				llColourAt = (llAt << 16) / llOuterAt;
				llAlpha = fLerp(llInnerAlpha, llOuterAlpha, llColourAt);
			}
			else
			{
				llColourAt = One;
				llAlpha = fLerp(llOuterAlpha, 0, ((llAt - llOuterAt) << 16) / (One - llOuterAt));
			}

			plR = fLerp(fChannel(pOLook.TorchInner, 16), fChannel(pOLook.TorchOuter, 16), llColourAt);
			plG = fLerp(fChannel(pOLook.TorchInner, 8), fChannel(pOLook.TorchOuter, 8), llColourAt);
			plB = fLerp(fChannel(pOLook.TorchInner, 0), fChannel(pOLook.TorchOuter, 0), llColourAt);
			plAlpha = (llAlpha * pOLook.TorchStrength) / 1000;
		}

		// ------------------------------------------------- The noise

		/// <summary>Tiling value noise of one octave at texel (x, y) of a piSize tile with piCells cells
		/// across: the lattice's values (0 to One) wrap at piCells, eased between them (smoothstep).</summary>
		private static long fValueNoise(int piSeed, int piOctave, int piCells, int x, int y, int piSize)
		{
			long llU = ((((2L * x) + 1) * piCells) << 16) / (2L * piSize);
			long llV = ((((2L * y) + 1) * piCells) << 16) / (2L * piSize);
			int liX0 = (int)(llU >> 16) % piCells;
			int liY0 = (int)(llV >> 16) % piCells;
			int liX1 = (liX0 + 1) % piCells;
			int liY1 = (liY0 + 1) % piCells;
			long llFx = fEase(llU & 0xFFFF);
			long llFy = fEase(llV & 0xFFFF);
			long llTop = fLerp(fLattice(piSeed, piOctave, liX0, liY0), fLattice(piSeed, piOctave, liX1, liY0), llFx);
			long llBottom = fLerp(fLattice(piSeed, piOctave, liX0, liY1), fLattice(piSeed, piOctave, liX1, liY1), llFx);

			return fLerp(llTop, llBottom, llFy);
		}

		/// <summary>A lattice point's value, 0 to One.</summary>
		private static long fLattice(int piSeed, int piOctave, int piX, int piY)
		{
			return fHash(piX, piY, piOctave, piSeed) & 0xFFFF;
		}

		/// <summary>3f^2 - 2f^3 in 16.16.</summary>
		private static long fEase(long plAt)
		{
			return (((plAt * plAt) >> 16) * ((3 * One) - (2 * plAt))) >> 16;
		}

		/// <summary>An integer hash of a place, a layer and a seed (MurmurHash3's finaliser over a mix).</summary>
		private static uint fHash(int piX, int piY, int piLayer, int piSeed)
		{
			unchecked
			{
				uint luHash = ((uint)piSeed * 0x9E3779B1u) ^ ((uint)piLayer * 0x7F4A7C15u);

				luHash ^= (uint)piX * 0x85EBCA77u;
				luHash = (luHash << 13) | (luHash >> 19);
				luHash ^= (uint)piY * 0xC2B2AE3Du;
				luHash = (luHash << 17) | (luHash >> 15);
				luHash ^= luHash >> 16;
				luHash *= 0x85EBCA6Bu;
				luHash ^= luHash >> 13;
				luHash *= 0xC2B2AE35u;
				luHash ^= luHash >> 16;

				return luHash;
			}
		}

		// ------------------------------------------------- Fixed point

		/// <summary>The middle of texel piAt of piSize, in One of the side.</summary>
		private static long fCentreOf(int piAt, int piSize)
		{
			return ((((2L * piAt) + 1) << 16) / (2L * piSize));
		}

		/// <summary>One channel of 0xRRGGBB (piShift 16, 8 or 0) in 16.16.</summary>
		private static long fChannel(int piRgb, int piShift)
		{
			return (long)((piRgb >> piShift) & 0xFF) << 16;
		}

		private static long fShade(UWStoneLook pOLook, long plChannel)
		{
			return (plChannel * pOLook.Shade) / 1000;
		}

		private static long fLerp(long plFrom, long plTo, long plAt)
		{
			return plFrom + (((plTo - plFrom) * plAt) >> 16);
		}

		/// <summary>A colour over another at an opacity (One = opaque), channels in 16.16.</summary>
		private static long fOver(long plUnder, long plOver, long plAlpha)
		{
			return ((plUnder * (One - plAlpha)) + (plOver * plAlpha)) >> 16;
		}

		/// <summary>A 16.16 channel as a byte, rounded and held to 0 to 255.</summary>
		private static byte fByte(long plChannel)
		{
			return (byte)fClamp((plChannel + (One / 2)) >> 16, 0, 255);
		}

		private static long fClamp(long plValue, long plMin, long plMax)
		{
			return plValue < plMin ? plMin : (plValue > plMax ? plMax : plValue);
		}

		/// <summary>The length of (x, y) in the same fixed point, by the integer square root.</summary>
		private static long fDistance(long plX, long plY)
		{
			return fSquareRoot((plX * plX) + (plY * plY));
		}

		/// <summary>The integer square root, rounded down (bit by bit).</summary>
		private static long fSquareRoot(long plValue)
		{
			if (plValue <= 0)
				return 0;

			long llRoot = 0;
			long llBit = 1L << 62;

			while (llBit > plValue)
				llBit >>= 2;

			while (llBit != 0)
			{
				if (plValue >= llRoot + llBit)
				{
					plValue -= llRoot + llBit;
					llRoot = (llRoot >> 1) + llBit;
				}
				else
					llRoot >>= 1;

				llBit >>= 2;
			}

			return llRoot;
		}
	}
}
