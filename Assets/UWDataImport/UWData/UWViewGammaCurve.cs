using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE 3D VIEW'S GAMMA (issue 9 on GitHub, 2026-10-30, per user: one slider for both render
	/// modes): the limits of the setting and what it does to a colour. A value g raises every channel
	/// to the power 1 / g, so g above 1 lifts the dark tones much more than the bright ones (a tenth
	/// becomes a third at 2) and black and white stay where they are; 1 is the picture as drawn. The
	/// host does it in the render pipeline's colour grading (UWViewGamma), which raises the linear
	/// value to the same power - on the sRGB value that is the same curve to within the sRGB curve's
	/// difference from a pure power.
	///
	/// Not the colour help's brightness (UWColourFilter.Brightness): that one multiplies, so black
	/// stays black and the dark corners of a level stay dark, and it reaches the interface as well.
	/// </summary>
	public static class UWViewGammaCurve
	{
		public const float Min = 0.5f;

		public const float Max = 2f;

		/// <summary>The picture as drawn.</summary>
		public const float Neutral = 1f;

		/// <summary>The setting as it is used; anything not a number reads as the neutral value.</summary>
		public static float Clamp(float pfGamma)
		{
			if (float.IsNaN(pfGamma) || pfGamma <= 0f)
				return Neutral;

			return pfGamma < Min ? Min : (pfGamma > Max ? Max : pfGamma);
		}

		/// <summary>Whether the value leaves the picture alone - then nothing is switched on for it.
		/// The slider moves in hundredths, so half of one counts as none.</summary>
		public static bool IsNeutral(float pfGamma)
		{
			return Math.Abs(Clamp(pfGamma) - Neutral) < 0.005f;
		}
	}
}
