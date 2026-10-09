using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// BACKS FOR THE MODERN INTERFACE'S PARTS (per user, 2026-10-09). Cut from the frame they looked
	/// stuck on - the original's compass lay embedded in its stone and light -, so a back is
	/// GENERATED: a SHAPE (none, oval, rectangular) filled with a PATTERN in a COLOUR, chosen apart
	/// (per user: "colour and pattern offered separately - colours leather, marble, stone; patterns
	/// the clouds, which are the leather's, marble and granite"), and an optional border.
	///
	///   - The COLOURS are shade ramps of the game's own colours, darkest first, so the back stays
	///     pixel art in the original's palette: leather the cloud leather's shades (INV.GR 6,
	///     UWHudArt.fCloudColours), marble the frame's blue-grey slate beside the classic compass
	///     (MAIN.BYT 82/135, 22 x 22, without the rust's warm colours), stone the plain grey of the
	///     classic compass's pedestal (MAIN.BYT 104/136, 68 x 18, its top without the light rim:
	///     more even than the shelves, per user), bronze the character panel's copper (MAIN.BYT
	///     240/12, 74 x 100, without the rings, the sheen and its yellow specks; added per user,
	///     "four colours should be enough"). Per user, after the first try: the disc's light
	///     greys made a marble far too light, and the slate read as marble, not as stone.
	///   - The PATTERNS give each pixel a value from 0 to 1, which picks the shade: the clouds are the
	///     cloud leather's soft noise; the other three are WALL TEXTURES OF THE GAME, their brightness
	///     stretched between its 2nd and 98th percentile and tiled (they tile seamlessly), so they
	///     keep the original's hand-drawn stone: W64 126 rough rock with soft shadows, 207 rock with
	///     large soft blots, 201 the dark marble with light veins. Per user, 2026-10-09, after
	///     generated marbles and granites that all looked artificial ("an die Texturen hatte ich gar
	///     nicht gedacht"); four patterns to the four colours, offered unnamed.
	///   - The BORDER: an outer ring in the darkest shade and inside it a bevel, the lightest shade at
	///     the top and the left, the second darkest at the bottom and the right - so the back sits in
	///     the interface instead of on it.
	///
	/// Every value comes from the pixel's coordinates (UWHudArt.fHash), so a back that grows shows
	/// more of the same pattern; piVariant picks another part of it. First used for the compass
	/// (UWModernCompass); meant for every part later, per user.
	/// </summary>
	public static class UWBackdropArt
	{
		public enum ShapeEnum
		{
			None,
			Oval,
			Rect
		}

		public enum ColourEnum
		{
			Leather,
			Marble,
			Stone,
			Bronze
		}

		public enum PatternEnum
		{
			Clouds,
			Rock,
			Blots,
			Marble
		}

		/// <summary>The wall texture behind each pattern but the clouds (see the class comment).</summary>
		private static readonly int[] miPatternWalls = { -1, 126, 207, 201 };

		/// <summary>The textures' values, made once per data set.</summary>
		private static UWTextures mOPatternSource;

		private static readonly Dictionary<PatternEnum, PatternTexture> mOPatternTextures = new Dictionary<PatternEnum, PatternTexture>();

		private sealed class PatternTexture
		{
			public float[] Values;

			public int Width;

			public int Height;
		}

		/// <summary>How many shades a ramp keeps at most.</summary>
		private const int RampShades = 6;

		private static readonly float[] mfCloudCells = { 24f, 10f, 4f };

		private static readonly float[] mfCloudWeights = { 0.55f, 0.3f, 0.15f };

		/// <summary>A back of the given size; transparent outside the shape, empty for None.</summary>
		public static UWPicture Build(UWTextures pOTextures, int piWidth, int piHeight, ShapeEnum peShape, ColourEnum peColour,
			PatternEnum pePattern, bool pbBorder, int piVariant = 0)
		{
			UWPicture lOOut = new UWPicture(Math.Max(0, piWidth), Math.Max(0, piHeight));

			if (peShape == ShapeEnum.None || piWidth <= 0 || piHeight <= 0)
				return lOOut;

			List<UWColor32> lORamp = Ramp(pOTextures, peColour);

			if (lORamp.Count == 0)
				return lOOut;

			bool[] lbShape = fShape(peShape, piWidth, piHeight);
			bool[] lbInner = fErode(lbShape, piWidth, piHeight);
			bool[] lbCore = fErode(lbInner, piWidth, piHeight);
			int liOffsetX = piVariant * 977;
			int liOffsetY = piVariant * 613;
			PatternTexture lOTexture = fPatternTexture(pOTextures, pePattern);

			for (int y = 0; y < piHeight; y++)
			{
				for (int x = 0; x < piWidth; x++)
				{
					int liAt = (y * piWidth) + x;

					if (!lbShape[liAt])
						continue;

					if (pbBorder && !lbInner[liAt])
					{
						lOOut.Set(x, y, lORamp[0]);
						continue;
					}

					if (pbBorder && !lbCore[liAt])
					{
						// The bevel: light from the top left, shade to the bottom right.
						bool lbLit = (x * piHeight) + (y * piWidth) < piWidth * piHeight;

						lOOut.Set(x, y, lbLit ? lORamp[lORamp.Count - 1] : lORamp[Math.Min(1, lORamp.Count - 1)]);
						continue;
					}

					float lfValue = lOTexture != null
						? lOTexture.Values[(((y + liOffsetY) % lOTexture.Height) * lOTexture.Width) + ((x + liOffsetX) % lOTexture.Width)]
						: fCloudsPattern(x + liOffsetX, y + liOffsetY);

					lOOut.Set(x, y, lORamp[(int)(UWHudArt.fClamp(lfValue, 0f, 0.999f) * lORamp.Count)]);
				}
			}

			return lOOut;
		}

		/// <summary>The ramps, made once per data set (a back that changes size every frame - the
		/// heading's - would read the whole frame picture each time).</summary>
		private static UWTextures mORampSource;

		private static readonly Dictionary<ColourEnum, List<UWColor32>> mORamps = new Dictionary<ColourEnum, List<UWColor32>>();

		/// <summary>The shades of a colour, darkest first (see the class comment).</summary>
		public static List<UWColor32> Ramp(UWTextures pOTextures, ColourEnum peColour)
		{
			if (!ReferenceEquals(mORampSource, pOTextures))
			{
				mORamps.Clear();
				mORampSource = pOTextures;
			}

			if (!mORamps.TryGetValue(peColour, out List<UWColor32> lORamp))
			{
				lORamp = fRamp(pOTextures, peColour);
				mORamps[peColour] = lORamp;
			}

			return lORamp;
		}

		private static List<UWColor32> fRamp(UWTextures pOTextures, ColourEnum peColour)
		{
			List<UWColor32> lOColours = new List<UWColor32>();

			try
			{
				switch (peColour)
				{
					case ColourEnum.Leather:
					{
						UWPicture lOPanel = UWPicture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.INV, UWHudArt.LeatherImage));
						UWPicture lOSource = lOPanel.Crop(new UWRectInt(0, 0, lOPanel.Width - UWHudArt.LeatherTransparentRight, lOPanel.Height));

						return UWHudArt.fCloudColours(lOSource);
					}

					case ColourEnum.Marble:
					{
						UWPicture lOMain = UWHudArt.fMain(pOTextures);

						for (int y = 135; y < 157; y++)
						{
							for (int x = 82; x < 104; x++)
							{
								UWColor32 lOColour = lOMain.Get(x, y);

								// The slate's greys and blues; the rust's warm browns stay out.
								if (lOColour.A > 0 && lOColour.B + 10 >= lOColour.R)
									lOColours.Add(lOColour);
							}
						}

						break;
					}

					case ColourEnum.Stone:
					{
						UWPicture lOMain = UWHudArt.fMain(pOTextures);

						for (int y = 136; y < 154; y++)
						{
							for (int x = 104; x < 172; x++)
							{
								UWColor32 lOColour = lOMain.Get(x, y);

								// The pedestal's plain greys, its top only: the cross's gold, the dark
								// outline and the light rim stay out (with them the stone came out blotchy).
								if (lOColour.A > 0 && lOColour.R >= 48 && lOColour.R <= 108 && Math.Abs(lOColour.R - lOColour.G) < 8
									&& Math.Abs(lOColour.G - lOColour.B) < 8)
									lOColours.Add(lOColour);
							}
						}

						break;
					}

					case ColourEnum.Bronze:
					{
						UWPicture lOMain = UWHudArt.fMain(pOTextures);

						for (int y = 12; y < 112; y++)
						{
							for (int x = 240; x < 314; x++)
							{
								UWColor32 lOColour = lOMain.Get(x, y);

								// The panel's warm body: the rings' greys and greens, the sheen and its yellow specks stay out.
								if (lOColour.A > 0 && lOColour.R >= 100 && lOColour.R <= 176 && lOColour.R - lOColour.G > 40
									&& lOColour.R - lOColour.B >= 60)
									lOColours.Add(lOColour);
							}
						}

						break;
					}
				}
			}
			catch
			{
				return new List<UWColor32>();
			}

			return fReduce(lOColours);
		}

		/// <summary>The distinct colours sorted by brightness, RampShades of them picked evenly.</summary>
		private static List<UWColor32> fReduce(List<UWColor32> pOColours)
		{
			List<UWColor32> lODistinct = new List<UWColor32>();

			foreach (UWColor32 lOColour in pOColours)
			{
				if (!lODistinct.Exists(lOOther => UWHudArt.fSame(lOOther, lOColour)))
					lODistinct.Add(lOColour);
			}

			lODistinct.Sort((lOA, lOB) => UWHudArt.fLuma(lOA).CompareTo(UWHudArt.fLuma(lOB)));

			if (lODistinct.Count <= RampShades)
				return lODistinct;

			List<UWColor32> lORamp = new List<UWColor32>();

			for (int liAt = 0; liAt < RampShades; liAt++)
				lORamp.Add(lODistinct[(int)Math.Round(liAt * (lODistinct.Count - 1) / (double)(RampShades - 1))]);

			return lORamp;
		}

		private static float fClouds(int piX, int piY, int piLayer)
		{
			float lfValue = 0f;

			for (int liOctave = 0; liOctave < mfCloudCells.Length; liOctave++)
				lfValue += mfCloudWeights[liOctave] * UWHudArt.fSmoothNoise(piLayer + liOctave, piX / mfCloudCells[liOctave], piY / mfCloudCells[liOctave]);

			return lfValue;
		}

		/// <summary>The cloud leather's clouds, with a little grain.</summary>
		private static float fCloudsPattern(int piX, int piY)
		{
			float lfValue = fClouds(piX, piY, 0) + ((UWHudArt.fHash(piX, piY, 99) - 0.5f) * 0.08f);

			return (lfValue - 0.2f) / 0.6f;
		}

		/// <summary>A pattern's wall texture as values from 0 to 1; null for the clouds or when the
		/// texture cannot be read (the clouds stand in then).</summary>
		private static PatternTexture fPatternTexture(UWTextures pOTextures, PatternEnum pePattern)
		{
			int liWall = miPatternWalls[Math.Max(0, Math.Min(miPatternWalls.Length - 1, (int)pePattern))];

			if (liWall < 0 || pOTextures == null)
				return null;

			if (!ReferenceEquals(mOPatternSource, pOTextures))
			{
				mOPatternTextures.Clear();
				mOPatternSource = pOTextures;
			}

			if (mOPatternTextures.TryGetValue(pePattern, out PatternTexture lOCached))
				return lOCached;

			PatternTexture lOTexture = null;

			try
			{
				UWPicture lOWall = UWPicture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.WALL, liWall));
				float[] lfLuma = new float[lOWall.Width * lOWall.Height];

				for (int y = 0; y < lOWall.Height; y++)
				{
					for (int x = 0; x < lOWall.Width; x++)
						lfLuma[(y * lOWall.Width) + x] = UWHudArt.fLuma(lOWall.Get(x, y));
				}

				float[] lfSorted = (float[])lfLuma.Clone();

				Array.Sort(lfSorted);

				// Stretched between the 2nd and the 98th percentile: the texture keeps its own calm
				// ground, only its veins and grains reach the ends of the ramp.
				float lfLow = lfSorted[(int)(lfSorted.Length * 0.02f)];
				float lfHigh = lfSorted[(int)(lfSorted.Length * 0.98f)];
				float lfRange = Math.Max(1f, lfHigh - lfLow);

				for (int liAt = 0; liAt < lfLuma.Length; liAt++)
					lfLuma[liAt] = (lfLuma[liAt] - lfLow) / lfRange;

				if (lfLuma.Length > 0)
					lOTexture = new PatternTexture { Values = lfLuma, Width = lOWall.Width, Height = lOWall.Height };
			}
			catch
			{
				lOTexture = null;
			}

			mOPatternTextures[pePattern] = lOTexture;

			return lOTexture;
		}

		private static bool[] fShape(ShapeEnum peShape, int piWidth, int piHeight)
		{
			bool[] lbShape = new bool[piWidth * piHeight];

			for (int y = 0; y < piHeight; y++)
			{
				for (int x = 0; x < piWidth; x++)
				{
					float lfX = ((x + 0.5f) / piWidth) - 0.5f;
					float lfY = ((y + 0.5f) / piHeight) - 0.5f;

					lbShape[(y * piWidth) + x] = peShape == ShapeEnum.Rect || ((lfX * lfX) + (lfY * lfY)) <= 0.25f;
				}
			}

			return lbShape;
		}

		/// <summary>One pixel smaller all round: a pixel stays when its four neighbours are in.</summary>
		private static bool[] fErode(bool[] pbShape, int piWidth, int piHeight)
		{
			bool[] lbOut = new bool[pbShape.Length];

			for (int y = 1; y < piHeight - 1; y++)
			{
				for (int x = 1; x < piWidth - 1; x++)
				{
					int liAt = (y * piWidth) + x;

					lbOut[liAt] = pbShape[liAt] && pbShape[liAt - 1] && pbShape[liAt + 1] && pbShape[liAt - piWidth] && pbShape[liAt + piWidth];
				}
			}

			return lbOut;
		}
	}
}
