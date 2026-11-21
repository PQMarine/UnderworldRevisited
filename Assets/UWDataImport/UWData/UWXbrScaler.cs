using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE TEXTURES' xBR SCALER (per user 2026-11-22: "xBR hat was. Sieht aus wie gemalt."): the xBR edge
	/// interpolation of level 1 at twice the size, written here after the published description of Hyllian's algorithm
	/// (no code taken over). Each source pixel becomes four; for each of its corners the two diagonals of its 5x5
	/// neighbourhood are weighed by colour distance, and where the edge runs across the corner that quarter takes half of
	/// the nearer neighbour's colour. The textures repeat, so the neighbourhood wraps round. Twice applied it gives 4x.
	/// Pixels are packed 0xAABBGGRR (R in the low byte); alpha stays opaque.
	///
	/// FASTER THE SAME DAY (switching it on stood still for about five seconds, per user): every distance the corners
	/// weigh is between two pixels side by side, one above the other or diagonal, so the four kinds are worked out once
	/// per pixel (fDistance as before) and looked up; the wrapped rows and columns come from tables instead of a
	/// remainder per neighbour. The result is the same to the bit (compared on every wall and floor of both games).
	/// </summary>
	public static class UWXbrScaler
	{
		/// <summary>The weights of the colour distance on Y, U and V (the luma counts most).</summary>
		private const float LumaWeight = 48f;

		private const float UWeight = 7f;

		private const float VWeight = 6f;

		/// <summary>The cross diagonal counts four times against the four edges beside it.</summary>
		private const float CrossWeight = 4f;

		/// <summary>How far the neighbourhood reaches from a pixel: two each way.</summary>
		private const int Reach = 2;

		/// <summary>piPasses times twice the size: 1 gives 2x, 2 gives 4x. The source unchanged.</summary>
		public static uint[] Scale(uint[] puySource, int piWidth, int piHeight, int piPasses)
		{
			uint[] luyImage = puySource;
			int liWidth = piWidth;
			int liHeight = piHeight;

			for (int liPass = 0; liPass < piPasses; liPass++)
			{
				luyImage = Scale2x(luyImage, liWidth, liHeight);
				liWidth *= 2;
				liHeight *= 2;
			}

			return luyImage;
		}

		/// <summary>The distances a pass looks up, each pixel's to its neighbour on the right, below, below right and below
		/// left; and the wrapped columns and rows (index + Reach for -Reach to the size + Reach - 1).</summary>
		private sealed class Pass
		{
			public float[] Right;
			public float[] Down;
			public float[] DownRight;
			public float[] DownLeft;
			public int[] Column;
			public int[] Row;

			public int At(int piX, int piY)
			{
				return Row[piY + Reach] + Column[piX + Reach];
			}

			/// <summary>The distance between two diagonal neighbours.</summary>
			public float Diagonal(int piX1, int piY1, int piX2, int piY2)
			{
				if (piY2 < piY1)
				{
					int liX = piX1;
					piX1 = piX2;
					piX2 = liX;
					piY1 = piY2;
				}

				return piX2 > piX1 ? DownRight[At(piX1, piY1)] : DownLeft[At(piX1, piY1)];
			}
		}

		/// <summary>One pass, twice the size.</summary>
		public static uint[] Scale2x(uint[] puySource, int piWidth, int piHeight)
		{
			int liCount = piWidth * piHeight;
			float[] lfY = new float[liCount];
			float[] lfU = new float[liCount];
			float[] lfV = new float[liCount];

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				uint luPixel = puySource[liAt];
				float lfR = luPixel & 0xFF;
				float lfG = (luPixel >> 8) & 0xFF;
				float lfB = (luPixel >> 16) & 0xFF;

				lfY[liAt] = 0.299f * lfR + 0.587f * lfG + 0.114f * lfB;
				lfU[liAt] = -0.169f * lfR - 0.331f * lfG + 0.5f * lfB;
				lfV[liAt] = 0.5f * lfR - 0.419f * lfG - 0.081f * lfB;
			}

			Pass lOPass = new Pass
			{
				Right = new float[liCount],
				Down = new float[liCount],
				DownRight = new float[liCount],
				DownLeft = new float[liCount],
				Column = new int[piWidth + (2 * Reach)],
				Row = new int[piHeight + (2 * Reach)]
			};

			for (int liX = -Reach; liX < piWidth + Reach; liX++)
				lOPass.Column[liX + Reach] = fWrap(liX, piWidth);

			for (int liY = -Reach; liY < piHeight + Reach; liY++)
				lOPass.Row[liY + Reach] = fWrap(liY, piHeight) * piWidth;

			for (int liY = 0; liY < piHeight; liY++)
			{
				for (int liX = 0; liX < piWidth; liX++)
				{
					int liAt = lOPass.At(liX, liY);

					lOPass.Right[liAt] = fDistance(lfY, lfU, lfV, liAt, lOPass.At(liX + 1, liY));
					lOPass.Down[liAt] = fDistance(lfY, lfU, lfV, liAt, lOPass.At(liX, liY + 1));
					lOPass.DownRight[liAt] = fDistance(lfY, lfU, lfV, liAt, lOPass.At(liX + 1, liY + 1));
					lOPass.DownLeft[liAt] = fDistance(lfY, lfU, lfV, liAt, lOPass.At(liX - 1, liY + 1));
				}
			}

			int liOutWidth = piWidth * 2;
			uint[] luyOut = new uint[liCount * 4];

			for (int liY = 0; liY < piHeight; liY++)
			{
				for (int liX = 0; liX < piWidth; liX++)
				{
					int liOut = (liY * 2) * liOutWidth + liX * 2;

					// The four corners as "down right" with the neighbourhood mirrored: x by sx, y by sy.
					luyOut[liOut] = fCorner(puySource, lOPass, liX, liY, -1, -1);
					luyOut[liOut + 1] = fCorner(puySource, lOPass, liX, liY, 1, -1);
					luyOut[liOut + liOutWidth] = fCorner(puySource, lOPass, liX, liY, -1, 1);
					luyOut[liOut + liOutWidth + 1] = fCorner(puySource, lOPass, liX, liY, 1, 1);
				}
			}

			return luyOut;
		}

		private static int fWrap(int piValue, int piSize)
		{
			int liValue = piValue % piSize;

			return liValue < 0 ? liValue + piSize : liValue;
		}

		private static float fDistance(float[] pfY, float[] pfU, float[] pfV, int piA, int piB)
		{
			return LumaWeight * Math.Abs(pfY[piA] - pfY[piB]) + UWeight * Math.Abs(pfU[piA] - pfU[piB])
				+ VWeight * Math.Abs(pfV[piA] - pfV[piB]);
		}

		/// <summary>The corner of E towards (sx, sy): E its pixel, F the one beside it that way, H the one below it that way,
		/// I the diagonal; the far ones as in the published layout (C, G, D, B, F4, I4, H5, I5). Every distance is
		/// looked up (Pass.Diagonal, Right, Down); the sums in the order the first version added them.</summary>
		private static uint fCorner(uint[] puySource, Pass pOPass, int piX, int piY, int piSx, int piSy)
		{
			int liXf = piX + piSx;
			int liYh = piY + piSy;

			float lfAcross = pOPass.Diagonal(piX, piY, liXf, piY - piSy)                 // E, C
				+ pOPass.Diagonal(piX, piY, piX - piSx, liYh)                            // E, G
				+ pOPass.Diagonal(liXf, liYh, piX + 2 * piSx, piY)                       // I, F4
				+ pOPass.Diagonal(liXf, liYh, piX, piY + 2 * piSy)                       // I, H5
				+ CrossWeight * pOPass.Diagonal(piX, liYh, liXf, piY);                   // H, F
			float lfAlong = pOPass.Diagonal(piX, liYh, piX - piSx, piY)                  // H, D
				+ pOPass.Diagonal(piX, liYh, liXf, piY + 2 * piSy)                       // H, I5
				+ pOPass.Diagonal(liXf, piY, piX + 2 * piSx, liYh)                       // F, I4
				+ pOPass.Diagonal(liXf, piY, piX, piY - piSy)                            // F, B
				+ CrossWeight * pOPass.Diagonal(piX, piY, liXf, liYh);                   // E, I

			int E = pOPass.At(piX, piY);

			if (lfAcross >= lfAlong)
				return puySource[E];

			float lfToF = pOPass.Right[pOPass.At(Math.Min(piX, liXf), piY)];
			float lfToH = pOPass.Down[pOPass.At(piX, Math.Min(piY, liYh))];
			int liNear = lfToF <= lfToH ? pOPass.At(liXf, piY) : pOPass.At(piX, liYh);

			return fHalf(puySource[E], puySource[liNear]);
		}

		// ------------------------------------------------- Sprites

		/// <summary>The weight of a difference in opacity, per step of 0..255: as far as the most different luma, so the
		/// outline against the clear counts as an edge.</summary>
		private const float AlphaWeight = 48f;

		/// <summary>From this opacity on a scaled sprite pixel is shown: half of 255, the billboards' cutoff of 0.5, so a
		/// corner mixed half with the clear stays (the comparison sheets the user chose from).</summary>
		public const float SpriteShownFrom = 127.5f;

		/// <summary>
		/// THE SPRITES' xBR (per user 2026-11-22 after comparison sheets: "Harter Rand. Der weiche Rand wirkt unscharf."):
		/// the same edge rule on a picture with clear pixels. Clear counts as a colour of its own (AlphaWeight), outside the
		/// picture is clear (sprites do not repeat), and two pixels are mixed by their opacity, so a clear neighbour gives
		/// its corner no dark seam but half the opacity. Between the passes the opacity stays as mixed; at the end a pixel is
		/// shown from SpriteShownFrom on and clear below it - the hard edge. Pixels 0xAABBGGRR; alpha must be 0 or 255.
		/// pbClampEdges: outside is the nearest edge pixel instead of clear, for a picture without clear pixels (a door, a
		/// model's texture), whose corners would else round off into holes.
		/// </summary>
		public static uint[] ScaleSprite(uint[] puySource, int piWidth, int piHeight, int piPasses, bool pbClampEdges = false)
		{
			int liCount = piWidth * piHeight;
			float[] lfR = new float[liCount];
			float[] lfG = new float[liCount];
			float[] lfB = new float[liCount];
			float[] lfA = new float[liCount];

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				uint luPixel = puySource[liAt];

				lfA[liAt] = luPixel >> 24;
				lfR[liAt] = lfA[liAt] > 0f ? luPixel & 0xFF : 0f;
				lfG[liAt] = lfA[liAt] > 0f ? (luPixel >> 8) & 0xFF : 0f;
				lfB[liAt] = lfA[liAt] > 0f ? (luPixel >> 16) & 0xFF : 0f;
			}

			int liWidth = piWidth;
			int liHeight = piHeight;

			for (int liPass = 0; liPass < piPasses; liPass++)
			{
				fSpritePass(ref lfR, ref lfG, ref lfB, ref lfA, liWidth, liHeight, pbClampEdges);
				liWidth *= 2;
				liHeight *= 2;
			}

			uint[] luyOut = new uint[liWidth * liHeight];

			for (int liAt = 0; liAt < luyOut.Length; liAt++)
			{
				if (lfA[liAt] < SpriteShownFrom)
					continue;

				luyOut[liAt] = 0xFF000000u | ((uint)Math.Round(lfB[liAt]) << 16) | ((uint)Math.Round(lfG[liAt]) << 8)
					| (uint)Math.Round(lfR[liAt]);
			}

			return luyOut;
		}

		/// <summary>
		/// A sprite with translucent pixels as well (per user 2026-11-22: the second game's people stayed pixelated - nearly
		/// every frame of theirs has a translucent shadow at the feet, and such pictures were left whole): the opaque part
		/// by ScaleSprite with the translucent pixels taken as clear, and where that leaves a pixel clear while the source
		/// pixel under it is translucent, that pixel enlarged as it is. A ghost, translucent throughout, so stays as plainly
		/// enlarged; a figure gets its round outline and keeps its shadow.
		/// </summary>
		public static uint[] ScaleSpriteKeepingTranslucent(uint[] puySource, int piWidth, int piHeight, int piPasses, bool pbClampEdges = false)
		{
			uint[] luyOpaque = new uint[puySource.Length];

			for (int liAt = 0; liAt < puySource.Length; liAt++)
				luyOpaque[liAt] = puySource[liAt] >> 24 == 255 ? puySource[liAt] : 0u;

			uint[] luyOut = ScaleSprite(luyOpaque, piWidth, piHeight, piPasses, pbClampEdges);
			int liScale = 1 << piPasses;
			int liOutWidth = piWidth * liScale;

			for (int liY = 0; liY < piHeight * liScale; liY++)
			{
				for (int liX = 0; liX < liOutWidth; liX++)
				{
					int liAt = liY * liOutWidth + liX;
					uint luSource = puySource[(liY / liScale) * piWidth + (liX / liScale)];
					uint luAlpha = luSource >> 24;

					if (luyOut[liAt] == 0u && luAlpha > 0 && luAlpha < 255)
						luyOut[liAt] = luSource;
				}
			}

			return luyOut;
		}

		private static void fSpritePass(ref float[] pfR, ref float[] pfG, ref float[] pfB, ref float[] pfA, int piWidth, int piHeight,
			bool pbClampEdges)
		{
			int liCount = piWidth * piHeight;
			float[] lfY = new float[liCount];
			float[] lfU = new float[liCount];
			float[] lfV = new float[liCount];

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				lfY[liAt] = 0.299f * pfR[liAt] + 0.587f * pfG[liAt] + 0.114f * pfB[liAt];
				lfU[liAt] = -0.169f * pfR[liAt] - 0.331f * pfG[liAt] + 0.5f * pfB[liAt];
				lfV[liAt] = 0.5f * pfR[liAt] - 0.419f * pfG[liAt] - 0.081f * pfB[liAt];
			}

			int liOutWidth = piWidth * 2;
			int liOutCount = liCount * 4;
			float[] lfR = new float[liOutCount];
			float[] lfG = new float[liOutCount];
			float[] lfB = new float[liOutCount];
			float[] lfA = new float[liOutCount];
			SpriteImage lOImage = new SpriteImage { Width = piWidth, Height = piHeight, Clamp = pbClampEdges, Y = lfY, U = lfU, V = lfV, A = pfA };

			for (int liY = 0; liY < piHeight; liY++)
			{
				for (int liX = 0; liX < piWidth; liX++)
				{
					int liOut = (liY * 2) * liOutWidth + liX * 2;

					for (int liCorner = 0; liCorner < 4; liCorner++)
					{
						int liSx = (liCorner & 1) == 0 ? -1 : 1;
						int liSy = liCorner < 2 ? -1 : 1;
						int liTo = liOut + (liCorner & 1) + (liCorner < 2 ? 0 : liOutWidth);
						int liE = liY * piWidth + liX;
						int liNear = fSpriteCorner(lOImage, liX, liY, liSx, liSy);

						if (liNear == OutsideNeighbour)
						{
							// Mixed with the clear outside: E's colour at half its opacity.
							lfR[liTo] = pfR[liE];
							lfG[liTo] = pfG[liE];
							lfB[liTo] = pfB[liE];
							lfA[liTo] = pfA[liE] / 2f;

							continue;
						}

						if (liNear == KeepCorner)
						{
							lfR[liTo] = pfR[liE];
							lfG[liTo] = pfG[liE];
							lfB[liTo] = pfB[liE];
							lfA[liTo] = pfA[liE];

							continue;
						}

						float lfSum = pfA[liE] + pfA[liNear];

						lfA[liTo] = lfSum / 2f;

						if (lfSum <= 0f)
							continue;

						lfR[liTo] = (pfR[liE] * pfA[liE] + pfR[liNear] * pfA[liNear]) / lfSum;
						lfG[liTo] = (pfG[liE] * pfA[liE] + pfG[liNear] * pfA[liNear]) / lfSum;
						lfB[liTo] = (pfB[liE] * pfA[liE] + pfB[liNear] * pfA[liNear]) / lfSum;
					}
				}
			}

			pfR = lfR;
			pfG = lfG;
			pfB = lfB;
			pfA = lfA;
		}

		/// <summary>A sprite pass's picture: colour as Y, U, V of the opacity-weighted colour (clear is black), and the
		/// opacity. Outside it is clear (index -1).</summary>
		private sealed class SpriteImage
		{
			public int Width;
			public int Height;
			public bool Clamp;
			public float[] Y;
			public float[] U;
			public float[] V;
			public float[] A;

			public int At(int piX, int piY)
			{
				if (Clamp)
					return Math.Min(Math.Max(piY, 0), Height - 1) * Width + Math.Min(Math.Max(piX, 0), Width - 1);

				return piX < 0 || piY < 0 || piX >= Width || piY >= Height ? -1 : piY * Width + piX;
			}

			public float Distance(int piA, int piB)
			{
				float lfYa = piA < 0 ? 0f : Y[piA], lfUa = piA < 0 ? 0f : U[piA], lfVa = piA < 0 ? 0f : V[piA], lfAa = piA < 0 ? 0f : A[piA];
				float lfYb = piB < 0 ? 0f : Y[piB], lfUb = piB < 0 ? 0f : U[piB], lfVb = piB < 0 ? 0f : V[piB], lfAb = piB < 0 ? 0f : A[piB];

				return LumaWeight * Math.Abs(lfYa - lfYb) + UWeight * Math.Abs(lfUa - lfUb) + VWeight * Math.Abs(lfVa - lfVb)
					+ AlphaWeight * Math.Abs(lfAa - lfAb);
			}
		}

		/// <summary>fSpriteCorner's answer where the corner keeps E.</summary>
		private const int KeepCorner = -2;

		/// <summary>fSpriteCorner's answer where the corner mixes with a neighbour outside the picture (clear).</summary>
		private const int OutsideNeighbour = -1;

		/// <summary>The sprite corner's rule as fCorner's: KeepCorner where E stays, else the neighbour (F or H) it mixes
		/// with, OutsideNeighbour (-1, SpriteImage.At's outside) when that one lies outside the picture.</summary>
		private static int fSpriteCorner(SpriteImage pOImage, int piX, int piY, int piSx, int piSy)
		{
			int E = pOImage.At(piX, piY);
			int F = pOImage.At(piX + piSx, piY);
			int H = pOImage.At(piX, piY + piSy);
			int I = pOImage.At(piX + piSx, piY + piSy);
			int C = pOImage.At(piX + piSx, piY - piSy);
			int G = pOImage.At(piX - piSx, piY + piSy);
			int D = pOImage.At(piX - piSx, piY);
			int B = pOImage.At(piX, piY - piSy);
			int F4 = pOImage.At(piX + 2 * piSx, piY);
			int I4 = pOImage.At(piX + 2 * piSx, piY + piSy);
			int H5 = pOImage.At(piX, piY + 2 * piSy);
			int I5 = pOImage.At(piX + piSx, piY + 2 * piSy);

			float lfAcross = pOImage.Distance(E, C) + pOImage.Distance(E, G) + pOImage.Distance(I, F4) + pOImage.Distance(I, H5)
				+ CrossWeight * pOImage.Distance(H, F);
			float lfAlong = pOImage.Distance(H, D) + pOImage.Distance(H, I5) + pOImage.Distance(F, I4) + pOImage.Distance(F, B)
				+ CrossWeight * pOImage.Distance(E, I);

			if (lfAcross >= lfAlong)
				return KeepCorner;

			return pOImage.Distance(E, F) <= pOImage.Distance(E, H) ? F : H;
		}

		/// <summary>Half of each colour, opaque.</summary>
		private static uint fHalf(uint puA, uint puB)
		{
			uint luOut = 0xFF000000u;

			for (int liShift = 0; liShift < 24; liShift += 8)
			{
				uint luA = (puA >> liShift) & 0xFF;
				uint luB = (puB >> liShift) & 0xFF;

				luOut |= ((luA + luB + 1) >> 1) << liShift;
			}

			return luOut;
		}
	}
}
