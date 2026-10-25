using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE CLOUD LEATHER'S NOISE (cleanup 9.9, 2026-10-25, CLEANUP-PLAN 8.4), shared by the leather
	/// (UWLeatherBuilder) and the modern parts' backs (UWBackdropArt), which had it once and called into the
	/// other. Value noise in floats: every value comes from its coordinates alone (Hash), so a
	/// picture that grows shows more of the same field (per user, 2026-10-04). The window themes'
	/// grounds (UWThemeGround) have a noise of their own in whole numbers - another algorithm, kept
	/// apart so their pinned bytes stay.
	/// </summary>
	public static class UWNoise
	{
		/// <summary>The layer of the grain the clouds get on top.</summary>
		public const int GrainLayer = 99;

		/// <summary>The cloud octaves' cell sizes and weights.</summary>
		private static readonly float[] mfCloudCells = { 24f, 10f, 4f };

		private static readonly float[] mfCloudWeights = { 0.55f, 0.3f, 0.15f };

		/// <summary>A value from 0 to 1 that depends on the point and the layer alone.</summary>
		public static float Hash(int piX, int piY, int piLayer)
		{
			unchecked
			{
				uint luHash = ((uint)piX * 73856093u) ^ ((uint)piY * 19349663u) ^ ((uint)piLayer * 83492791u);

				luHash ^= luHash >> 13;
				luHash *= 0x5bd1e995u;
				luHash ^= luHash >> 15;

				return (luHash & 0xFFFFFF) / 16777216f;
			}
		}

		/// <summary>The lattice of Hash at whole coordinates, eased between them (smoothstep).</summary>
		public static float Smooth(int piLayer, float pfX, float pfY)
		{
			int liX = (int)Math.Floor(pfX);
			int liY = (int)Math.Floor(pfY);
			float lfX = fSmoothStep(pfX - liX);
			float lfY = fSmoothStep(pfY - liY);
			float lfTop = fLerp(Hash(liX, liY, piLayer), Hash(liX + 1, liY, piLayer), lfX);
			float lfBottom = fLerp(Hash(liX, liY + 1, piLayer), Hash(liX + 1, liY + 1, piLayer), lfX);

			return fLerp(lfTop, lfBottom, lfY);
		}

		/// <summary>The clouds at a point: the three octaves (cells 24, 10 and 4, weights 0.55, 0.3
		/// and 0.15) of Smooth, from layer piLayer on; about 0.2 to 0.8.</summary>
		public static float Clouds(int piX, int piY, int piLayer)
		{
			float lfValue = 0f;

			for (int liOctave = 0; liOctave < mfCloudCells.Length; liOctave++)
				lfValue += mfCloudWeights[liOctave] * Smooth(piLayer + liOctave, piX / mfCloudCells[liOctave], piY / mfCloudCells[liOctave]);

			return lfValue;
		}

		private static float fSmoothStep(float pfT)
		{
			return pfT * pfT * (3f - (2f * pfT));
		}

		/// <summary>As Unity's Mathf.Lerp: t kept to 0..1.</summary>
		private static float fLerp(float pfA, float pfB, float pfT)
		{
			return pfA + ((pfB - pfA) * UWMath.Clamp(pfT, 0f, 1f));
		}
	}
}
