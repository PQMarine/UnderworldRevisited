using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The height of a wall or floor texture for the Remastered render mode - from the
	/// MORTAR JOINTS instead of from the brightness.
	///
	/// WHY: guessed from brightness, every dark spot on a stone became a hole
	/// and every bright streak an edge. The relief was noisy, and parallax only made it
	/// more visible (per user, 2026-09-13: "I did not find POM suitable here").
	///
	/// HOW IT WORKS:
	///   1. Joints are pixels that are clearly darker than their surroundings.
	///   2. Small dark spots are grain, not joints - only connected lines
	///      above a minimum size remain.
	///   3. Each stone bulges with the distance to the nearest joint: deep in the joint,
	///      rounded towards the edge, flat in the middle.
	///   4. The grain on the stone is added only weakly.
	/// If a texture has no joints (wood, earth, plaster), it stays flat with light
	/// grain.
	///
	/// Everything is computed wrapping around: the textures tile, a joint at the right edge
	/// continues on the left.
	///
	/// Engine-free since 2026-09-17 (P1 of the engine separation): it takes brightness values
	/// instead of Unity pixels; UWHeightMapBuilder on the Unity side converts.
	/// </summary>
	public static class UWHeightMap
	{
		public struct Result
		{
			public float[] Heights;

			/// <summary>Which pixels were detected as joints.</summary>
			public bool[] Mortar;

			/// <summary>Whether the texture counts as masonry - otherwise grain only.</summary>
			public bool HasMortar;
		}

		/// <summary>Half the edge length of the window for the surrounding brightness.</summary>
		public static int NeighbourhoodRadius = 6;

		/// <summary>A pixel this much darker than its surroundings is a joint
		/// (fraction of the surrounding brightness).
		///
		/// A threshold based on the variance of the surroundings was tried and discarded
		/// (preview 2026-09-13): in cobblestones the joints themselves drive the variance
		/// up and vanish, on earth floors random spots appeared. Weak joints
		/// (sandstone, grey slabs) thus stay undetected and come through the grain as
		/// a slight depression.</summary>
		public static float MortarContrast = 0.25f;

		/// <summary>Below this a pixel is always a joint, no matter what lies around it.</summary>
		public static float MortarAbsolute = 0.1f;

		/// <summary>Smaller dark islands are grain. With 24, dark grains on the ceiling became pits (test image 2026-09-13); a joint network has hundreds of pixels.</summary>
		public static int MinMortarPixels = 64;

		/// <summary>Share of joints within which a texture counts as masonry.</summary>
		public static float MinMortarShare = 0.04f;

		public static float MaxMortarShare = 0.45f;

		/// <summary>How wide the rounded edge of a stone is, in pixels.</summary>
		public static float BevelPixels = 4f;

		/// <summary>
		/// How differently far the stones protrude: each stone gets its own
		/// height between 1 minus this value and 1.
		///
		/// WHAT FOR: if all stones are equally high, a stone only casts shadow into the joint - and that
		/// is black in the texture anyway. The self-shadowing changed a mere 313
		/// pixels in the test image, barely noticeably (2026-09-13). If a stone protrudes further, its
		/// shadow falls across the joint onto the neighbour's edge.
		///
		/// The height depends only on the position of the stone, not on randomness at startup -
		/// the same wall every time.
		/// </summary>
		public static float StoneHeightVariation = 0.4f;

		/// <summary>Height of the stone edge right at the joint.</summary>
		public static float EdgeHeight = 0.2f;

		/// <summary>How strongly the grain on the stone changes the height.</summary>
		public static float GrainOnStone = 0.5f;

		/// <summary>Grain on textures without joints.</summary>
		public static float GrainFlat = 0.8f;

		/// <summary>From this share of water or lava colours on, a texture stays completely flat:
		/// its spots are waves or embers, not shape - they turned into bumps and
		/// holes (preview 2026-09-13). The lava floors with rock are at 28 percent.</summary>
		public static float MaxLiquidShare = 0.2f;

		/// <summary>Edge length of a texture slice; UWTextureArrayBuilder.SliceResolution has to match.</summary>
		public const int Size = 64;

		/// <param name="pyIndices">The palette indices of the same pixels (see
		/// UWTextureArrayBuilder.GetSliceIndices), for detecting water and lava.
		/// May be null.</param>
		/// <param name="pfLuminance">Brightness 0 to 1 per pixel, Size * Size values (0.299 R +
		/// 0.587 G + 0.114 B).</param>
		public static Result Build(float[] pfLuminance, byte[] pyIndices)
		{
			int liCount = Size * Size;

			Result lOResult = new Result();

			lOResult.Heights = new float[liCount];
			lOResult.Mortar = new bool[liCount];

			if (fGetLiquidShare(pyIndices) >= MaxLiquidShare)
			{
				for (int i = 0; i < liCount; i++)
					lOResult.Heights[i] = 0.5f;

				return lOResult;
			}

			float[] lfLuminance = pfLuminance;

			float[] lfLocal = fBoxBlur(lfLuminance, NeighbourhoodRadius);

			bool[] lbMortar = lOResult.Mortar;

			for (int i = 0; i < liCount; i++)
				lbMortar[i] = lfLuminance[i] < MortarAbsolute
					|| lfLuminance[i] < lfLocal[i] * (1f - MortarContrast);

			fRemoveSmallIslands(lbMortar, MinMortarPixels);

			int liMortarCount = 0;

			for (int i = 0; i < liCount; i++)
			{
				if (lbMortar[i])
					liMortarCount++;
			}

			float lfShare = liMortarCount / (float)liCount;

			lOResult.HasMortar = lfShare >= MinMortarShare && lfShare <= MaxMortarShare;

			if (!lOResult.HasMortar)
			{
				for (int i = 0; i < liCount; i++)
				{
					lOResult.Heights[i] = Math.Clamp(0.5f + ((lfLuminance[i] - lfLocal[i]) * GrainFlat), 0f, 1f);
					lbMortar[i] = false;
				}

				return lOResult;
			}

			float[] lfDistance = fDistanceToMortar(lbMortar, (int)System.Math.Ceiling(BevelPixels) + 1);
			float[] lfStoneTop = fGetStoneTops(lbMortar);

			for (int i = 0; i < liCount; i++)
			{
				if (lbMortar[i])
				{
					lOResult.Heights[i] = 0f;
					continue;
				}

				// Rounded edge: steep at the joint, flat towards the middle.
				float lfT = Math.Clamp((lfDistance[i] - 0.5f) / BevelPixels, 0f, 1f);
				float lfProfile = 1f - ((1f - lfT) * (1f - lfT));
				float lfHeight = EdgeHeight + ((lfStoneTop[i] - EdgeHeight) * lfProfile);

				lfHeight += (lfLuminance[i] - lfLocal[i]) * GrainOnStone;

				lOResult.Heights[i] = Math.Clamp(lfHeight, 0f, 1f);
			}

			return lOResult;
		}

		/// <summary>Water (palette 48 to 63) and lava (16 to 23), the same ranges as
		/// in UWTextureArrayBuilder.BuildMaterialLookup.</summary>
		private static float fGetLiquidShare(byte[] pyIndices)
		{
			if (pyIndices == null || pyIndices.Length == 0)
				return 0f;

			int liLiquid = 0;

			foreach (byte lyIndex in pyIndices)
			{
				if ((lyIndex >= 48 && lyIndex <= 63) || (lyIndex >= 16 && lyIndex <= 23))
					liLiquid++;
			}

			return liLiquid / (float)pyIndices.Length;
		}

		/// <summary>The top of each stone: connected areas without joints (four
		/// neighbours, wrapping around) get a fixed height from their first pixel.</summary>
		private static float[] fGetStoneTops(bool[] pbMortar)
		{
			float[] lfTop = new float[pbMortar.Length];
			bool[] lbDone = new bool[pbMortar.Length];
			Stack<int> lOOpen = new Stack<int>();
			List<int> lOStone = new List<int>();

			for (int i = 0; i < pbMortar.Length; i++)
			{
				if (pbMortar[i] || lbDone[i])
					continue;

				lOStone.Clear();
				lOOpen.Push(i);
				lbDone[i] = true;

				while (lOOpen.Count > 0)
				{
					int liAt = lOOpen.Pop();

					lOStone.Add(liAt);

					int liX = liAt % Size;
					int liY = liAt / Size;

					fVisit(pbMortar, lbDone, lOOpen, (fWrap(liY) * Size) + fWrap(liX + 1));
					fVisit(pbMortar, lbDone, lOOpen, (fWrap(liY) * Size) + fWrap(liX - 1));
					fVisit(pbMortar, lbDone, lOOpen, (fWrap(liY + 1) * Size) + fWrap(liX));
					fVisit(pbMortar, lbDone, lOOpen, (fWrap(liY - 1) * Size) + fWrap(liX));
				}

				// Fixed variation from the position: a simple integer hash, no Random.
				uint luHash = (uint)i * 2654435761u;

				luHash ^= luHash >> 13;
				luHash *= 1274126177u;
				luHash ^= luHash >> 16;

				float lfStoneTop = 1f - (StoneHeightVariation * ((luHash & 0xFFFF) / 65535f));

				foreach (int liAt in lOStone)
					lfTop[liAt] = lfStoneTop;
			}

			return lfTop;
		}

		private static void fVisit(bool[] pbMortar, bool[] pbDone, Stack<int> pOOpen, int piAt)
		{
			if (pbMortar[piAt] || pbDone[piAt])
				return;

			pbDone[piAt] = true;
			pOOpen.Push(piAt);
		}

		private static int fWrap(int piValue)
		{
			return ((piValue % Size) + Size) % Size;
		}

		private static float[] fBoxBlur(float[] pfSource, int piRadius)
		{
			float[] lfTarget = new float[pfSource.Length];
			float lfWeight = 1f / ((2 * piRadius + 1) * (2 * piRadius + 1));

			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					float lfSum = 0f;

					for (int liY = -piRadius; liY <= piRadius; liY++)
					{
						int liRow = fWrap(y + liY) * Size;

						for (int liX = -piRadius; liX <= piRadius; liX++)
							lfSum += pfSource[liRow + fWrap(x + liX)];
					}

					lfTarget[(y * Size) + x] = lfSum * lfWeight;
				}
			}

			return lfTarget;
		}

		/// <summary>Connected joint pieces (eight neighbours, wrapping around) below the
		/// minimum size are removed.</summary>
		private static void fRemoveSmallIslands(bool[] pbMask, int piMinPixels)
		{
			int[] liLabel = new int[pbMask.Length];
			List<int> lOIsland = new List<int>();
			Stack<int> lOOpen = new Stack<int>();
			int liNext = 1;

			for (int i = 0; i < pbMask.Length; i++)
			{
				if (!pbMask[i] || liLabel[i] != 0)
					continue;

				lOIsland.Clear();
				lOOpen.Push(i);
				liLabel[i] = liNext;

				while (lOOpen.Count > 0)
				{
					int liAt = lOOpen.Pop();

					lOIsland.Add(liAt);

					int liX = liAt % Size;
					int liY = liAt / Size;

					for (int liDy = -1; liDy <= 1; liDy++)
					{
						for (int liDx = -1; liDx <= 1; liDx++)
						{
							int liN = (fWrap(liY + liDy) * Size) + fWrap(liX + liDx);

							if (pbMask[liN] && liLabel[liN] == 0)
							{
								liLabel[liN] = liNext;
								lOOpen.Push(liN);
							}
						}
					}
				}

				if (lOIsland.Count < piMinPixels)
				{
					foreach (int liAt in lOIsland)
						pbMask[liAt] = false;
				}

				liNext++;
			}
		}

		/// <summary>Distance to the nearest joint, up to at most piLimit (then piLimit).</summary>
		private static float[] fDistanceToMortar(bool[] pbMortar, int piLimit)
		{
			float[] lfDistance = new float[pbMortar.Length];

			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					float lfBest = piLimit;

					for (int liY = -piLimit; liY <= piLimit; liY++)
					{
						int liRow = fWrap(y + liY) * Size;

						for (int liX = -piLimit; liX <= piLimit; liX++)
						{
							if (!pbMortar[liRow + fWrap(x + liX)])
								continue;

							float lfD = (float)System.Math.Sqrt((liX * liX) + (liY * liY));

							if (lfD < lfBest)
								lfBest = lfD;
						}
					}

					lfDistance[(y * Size) + x] = lfBest;
				}
			}

			return lfDistance;
		}
	}
}
