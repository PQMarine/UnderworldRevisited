using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Colour help for colour vision deficiencies (2026-09-17, per user), engine-free: a 3x3
	/// matrix over the colours plus brightness and contrast.
	///
	/// HOW IT WORKS (daltonisation, the usual method): first the picture is simulated as the eye
	/// in question sees it. What is lost in that step - the difference between the original and
	/// the simulation - is then put back into the channels the eye CAN tell apart. For red-green
	/// deficiencies that means the lost red-green difference ends up in green and blue, so a red
	/// and a green object that looked alike now differ in brightness and in blue.
	///
	/// Both steps are linear, so they collapse into one matrix: M = I + S * (I - Sim). The host
	/// applies it to whatever carries colour - the palette table of the interface and the
	/// palette renderer, the pictures of the menu screens, and, in the Remastered path, the
	/// channel mixer of the render pipeline.
	///
	/// The simulation matrices are the ones commonly used for protanopia, deuteranopia and
	/// tritanopia; the redistribution is the classic one from the daltonisation papers.
	/// </summary>
	public class UWColourFilter
	{
		public enum ModeEnum
		{
			/// <summary>No change.</summary>
			Off = 0,

			/// <summary>Red-blind (protanopia).</summary>
			Red = 1,

			/// <summary>Green-blind (deuteranopia).</summary>
			Green = 2,

			/// <summary>Blue-blind (tritanopia).</summary>
			Blue = 3
		}

		private static readonly float[] myProtanopia =
		{
			0.567f, 0.433f, 0.000f,
			0.558f, 0.442f, 0.000f,
			0.000f, 0.242f, 0.758f
		};

		private static readonly float[] myDeuteranopia =
		{
			0.625f, 0.375f, 0.000f,
			0.700f, 0.300f, 0.000f,
			0.000f, 0.300f, 0.700f
		};

		private static readonly float[] myTritanopia =
		{
			0.950f, 0.050f, 0.000f,
			0.000f, 0.433f, 0.567f,
			0.000f, 0.475f, 0.525f
		};

		/// <summary>Where the lost difference goes. For red and green deficiencies into green and
		/// blue, for the blue one into red and green.</summary>
		private static readonly float[] myShiftRedGreen =
		{
			0.000f, 0.000f, 0.000f,
			0.700f, 1.000f, 0.000f,
			0.700f, 0.000f, 1.000f
		};

		private static readonly float[] myShiftBlue =
		{
			1.000f, 0.000f, 0.700f,
			0.000f, 1.000f, 0.700f,
			0.000f, 0.000f, 0.000f
		};

		/// <summary>The matrix in use, row by row (nine values).</summary>
		public float[] Matrix { get; private set; } = fIdentity();

		public ModeEnum Mode { get; private set; }

		/// <summary>Brightness as a factor, 1 leaves it alone.</summary>
		public float Brightness { get; private set; } = 1f;

		/// <summary>Contrast as a factor around mid grey, 1 leaves it alone.</summary>
		public float Contrast { get; private set; } = 1f;

		public bool IsActive => Mode != ModeEnum.Off
			|| Math.Abs(Brightness - 1f) > 0.001f || Math.Abs(Contrast - 1f) > 0.001f;

		/// <summary>Whether the colour matrix alone does anything - the render pipeline needs to
		/// know, it does brightness and contrast itself.</summary>
		public bool HasMatrix => Mode != ModeEnum.Off;

		/// <summary>
		/// Sets the correction. pfStrength runs from 0 (off) to 1 (full daltonisation) and simply
		/// blends between the identity and the matrix.
		/// </summary>
		public void Set(ModeEnum peMode, float pfStrength, float pfBrightness, float pfContrast)
		{
			Mode = peMode;
			Brightness = pfBrightness;
			Contrast = pfContrast;

			if (peMode == ModeEnum.Off)
			{
				Matrix = fIdentity();

				return;
			}

			float[] lySimulation = peMode == ModeEnum.Red
				? myProtanopia : (peMode == ModeEnum.Green ? myDeuteranopia : myTritanopia);

			float[] lyShift = peMode == ModeEnum.Blue ? myShiftBlue : myShiftRedGreen;

			// M = I + S * (I - Sim)
			float[] lyLost = new float[9];

			for (int liAt = 0; liAt < 9; liAt++)
				lyLost[liAt] = (liAt % 4 == 0 ? 1f : 0f) - lySimulation[liAt];

			float[] lyMatrix = fMultiply(lyShift, lyLost);

			for (int liAt = 0; liAt < 9; liAt++)
				lyMatrix[liAt] += liAt % 4 == 0 ? 1f : 0f;

			// Strength: between doing nothing and the full correction.
			float lfStrength = pfStrength < 0f ? 0f : (pfStrength > 1f ? 1f : pfStrength);

			for (int liAt = 0; liAt < 9; liAt++)
			{
				float lfIdentity = liAt % 4 == 0 ? 1f : 0f;

				lyMatrix[liAt] = lfIdentity + ((lyMatrix[liAt] - lfIdentity) * lfStrength);
			}

			Matrix = lyMatrix;
		}

		/// <summary>One colour through matrix, brightness and contrast. Values 0 to 255 in and
		/// out.</summary>
		public void Apply(ref byte pyRed, ref byte pyGreen, ref byte pyBlue)
		{
			if (!IsActive)
				return;

			float lfRed = pyRed / 255f;
			float lfGreen = pyGreen / 255f;
			float lfBlue = pyBlue / 255f;

			float lfOutRed = (Matrix[0] * lfRed) + (Matrix[1] * lfGreen) + (Matrix[2] * lfBlue);
			float lfOutGreen = (Matrix[3] * lfRed) + (Matrix[4] * lfGreen) + (Matrix[5] * lfBlue);
			float lfOutBlue = (Matrix[6] * lfRed) + (Matrix[7] * lfGreen) + (Matrix[8] * lfBlue);

			pyRed = fToByte(fAdjust(lfOutRed));
			pyGreen = fToByte(fAdjust(lfOutGreen));
			pyBlue = fToByte(fAdjust(lfOutBlue));
		}

		private float fAdjust(float pfValue)
		{
			return ((pfValue * Brightness) - 0.5f) * Contrast + 0.5f;
		}

		private static byte fToByte(float pfValue)
		{
			int liValue = (int)((pfValue * 255f) + 0.5f);

			return (byte)(liValue < 0 ? 0 : (liValue > 255 ? 255 : liValue));
		}

		private static float[] fIdentity()
		{
			return new float[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };
		}

		/// <summary>Row-by-row product of two 3x3 matrices.</summary>
		private static float[] fMultiply(float[] pyLeft, float[] pyRight)
		{
			float[] lyResult = new float[9];

			for (int liRow = 0; liRow < 3; liRow++)
			{
				for (int liColumn = 0; liColumn < 3; liColumn++)
				{
					float lfSum = 0f;

					for (int liAt = 0; liAt < 3; liAt++)
						lfSum += pyLeft[(liRow * 3) + liAt] * pyRight[(liAt * 3) + liColumn];

					lyResult[(liRow * 3) + liColumn] = lfSum;
				}
			}

			return lyResult;
		}
	}
}
