using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The light colour of an item, derived from its own image: the average of its brightest
	/// pixels (the coloured ones, if there are enough of them), pulled up to full brightness. A
	/// campfire thus glows orange, a glowing mushroom greenish, an orb bluish - without a colour
	/// table anywhere. An almost white result means "no colour information", and the caller uses
	/// its fallback.
	///
	/// Engine-free since 2026-09-17 (P1 of the engine separation); UWLightColour on the Unity side
	/// turns the result into a Color.
	/// </summary>
	public static class UWObjectLightColour
	{
		private struct Entry
		{
			public bool Found;
			public float Red;
			public float Green;
			public float Blue;
		}

		private static readonly Dictionary<int, Entry> msCache = new Dictionary<int, Entry>();

		/// <summary>Which fraction of the highest brightness still counts towards the "brightest
		/// pixels".</summary>
		private const float BrightShare = 0.75f;

		/// <summary>From which fraction of the highest brightness a coloured pixel counts - lower
		/// than for the grey ones, because a flame may be darker than its wax.</summary>
		private const float ColouredShare = 0.45f;

		private const float MinSaturation = 0.3f;

		/// <summary>If even the weakest channel is above this, the colour is practically white.</summary>
		private const float NoColourInformation = 0.85f;

		private const int MinColouredPixels = 3;

		/// <summary>The light colour of object piId as channels 0 to 1; false when the image gives no
		/// colour (missing, empty, or practically white).</summary>
		public static bool TryGet(DataImport pOData, int piId, out float pfRed, out float pfGreen, out float pfBlue)
		{
			Entry lOEntry;

			if (!msCache.TryGetValue(piId, out lOEntry))
			{
				lOEntry = fCompute(pOData, piId);
				msCache[piId] = lOEntry;
			}

			pfRed = lOEntry.Red;
			pfGreen = lOEntry.Green;
			pfBlue = lOEntry.Blue;

			return lOEntry.Found;
		}

		private static Entry fCompute(DataImport pOData, int piId)
		{
			Entry lOResult = new Entry();

			List<UWTexture> lOObjects = pOData != null && pOData.Textures != null
				? pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.OBJECTS)
				: null;

			UWColor32[] lOPixels = null;

			if (lOObjects != null && piId >= 0 && piId < lOObjects.Count)
			{
				try
				{
					lOPixels = lOObjects[piId].GetUWColor32();
				}
				catch
				{
					lOPixels = null;
				}
			}

			if (lOPixels == null || lOPixels.Length == 0)
				return lOResult;

			float lfBest = 0f;

			for (int liAt = 0; liAt < lOPixels.Length; liAt++)
			{
				if (lOPixels[liAt].A != 0)
					lfBest = Math.Max(lfBest, fLuminance(lOPixels[liAt]));
			}

			if (lfBest <= 0f)
				return lOResult;

			float lfRed = 0f;
			float lfGreen = 0f;
			float lfBlue = 0f;
			int liTaken = 0;

			// COLOURED bright pixels first. With the candle the white wax is brighter than the
			// flame, and the brightest pixels gave a bluish white instead of candlelight. If there
			// are enough saturated bright pixels, only those count.
			bool lbColouredOnly = fCountColouredBright(lOPixels, lfBest * ColouredShare) >= MinColouredPixels;

			for (int liAt = 0; liAt < lOPixels.Length; liAt++)
			{
				if (lOPixels[liAt].A == 0)
					continue;

				if (lbColouredOnly)
				{
					if (fLuminance(lOPixels[liAt]) < lfBest * ColouredShare
						|| fSaturation(lOPixels[liAt]) < MinSaturation)
						continue;
				}
				else if (fLuminance(lOPixels[liAt]) < lfBest * BrightShare)
					continue;

				lfRed += lOPixels[liAt].R;
				lfGreen += lOPixels[liAt].G;
				lfBlue += lOPixels[liAt].B;
				liTaken++;
			}

			if (liTaken == 0)
				return lOResult;

			float lfPeak = Math.Max(1f, Math.Max(lfRed, Math.Max(lfGreen, lfBlue)));

			lOResult.Red = lfRed / lfPeak;
			lOResult.Green = lfGreen / lfPeak;
			lOResult.Blue = lfBlue / lfPeak;

			// An almost WHITE result says nothing about the light colour - the image then has no
			// coloured bright pixels, only white like the candle's wax.
			lOResult.Found = Math.Min(lOResult.Red, Math.Min(lOResult.Green, lOResult.Blue)) <= NoColourInformation;

			return lOResult;
		}

		private static int fCountColouredBright(UWColor32[] pOPixels, float pfThreshold)
		{
			int liCount = 0;

			for (int liAt = 0; liAt < pOPixels.Length; liAt++)
			{
				if (pOPixels[liAt].A != 0 && fLuminance(pOPixels[liAt]) >= pfThreshold
					&& fSaturation(pOPixels[liAt]) >= MinSaturation)
					liCount++;
			}

			return liCount;
		}

		private static float fSaturation(UWColor32 pOPixel)
		{
			int liMax = Math.Max(pOPixel.R, Math.Max(pOPixel.G, pOPixel.B));
			int liMin = Math.Min(pOPixel.R, Math.Min(pOPixel.G, pOPixel.B));

			return liMax <= 0 ? 0f : (float)(liMax - liMin) / liMax;
		}

		private static float fLuminance(UWColor32 pOPixel)
		{
			return (pOPixel.R * 0.299f) + (pOPixel.G * 0.587f) + (pOPixel.B * 0.114f);
		}
	}
}
