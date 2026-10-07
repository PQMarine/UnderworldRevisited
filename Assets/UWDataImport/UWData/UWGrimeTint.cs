using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE COLOUR OF THE PALETTE RENDERER'S GRIME (per user, 2026-10-07: "Ja, bau Farben fuer Grime
	/// ein"): for every palette index the palette's own colour nearest to that colour mixed towards
	/// a dirt tone - so a grimy pixel stays a colour of the palette, and the shading by levels still
	/// applies on top. The shader dithers between a pixel's own index and this one by how much
	/// grime lies there (UWPaletteEffects.hlsl).
	///
	/// Candidates are the palette's ordinary colours: not 0 (transparent in the sprites), not the
	/// rotating water and lava ranges (they would make the dirt flow), not the top sixteen (the
	/// translucency markers and the interface's fixed colours).
	/// </summary>
	public static class UWGrimeTint
	{
		public enum ToneEnum
		{
			/// <summary>No colour: the grime only darkens.</summary>
			None = 0,

			Dirt = 1,

			Moss = 2,

			Soot = 3
		}

		public const int ToneCount = 4;

		public const int PaletteSize = 256;

		/// <summary>How far a colour moves towards the tone before the nearest palette colour is
		/// looked for.</summary>
		public const float Mix = 0.45f;

		private const int LastCandidate = 239;

		/// <summary>The tones in RGB.</summary>
		public static void GetTone(ToneEnum peTone, out int piR, out int piG, out int piB)
		{
			switch (peTone)
			{
				case ToneEnum.Dirt: piR = 92; piG = 64; piB = 34; return;
				case ToneEnum.Moss: piR = 52; piG = 78; piB = 30; return;
				case ToneEnum.Soot: piR = 22; piG = 20; piB = 18; return;
				default: piR = 0; piG = 0; piB = 0; return;
			}
		}

		/// <summary>The table for one tone: index to the grimy index (None: every index itself).
		/// </summary>
		public static byte[] Build(UWPalette pOPalette, ToneEnum peTone)
		{
			byte[] lyTable = new byte[PaletteSize];

			for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
				lyTable[liIndex] = (byte)liIndex;

			if (pOPalette == null || peTone == ToneEnum.None)
				return lyTable;

			GetTone(peTone, out int liToneR, out int liToneG, out int liToneB);

			for (int liIndex = 1; liIndex <= LastCandidate; liIndex++)
			{
				if (UWPaletteRotation.IsRotatingIndex(liIndex))
					continue;

				UWColor32 lOColour = pOPalette.GetUWColor(liIndex);
				float lfR = lOColour.R + ((liToneR - lOColour.R) * Mix);
				float lfG = lOColour.G + ((liToneG - lOColour.G) * Mix);
				float lfB = lOColour.B + ((liToneB - lOColour.B) * Mix);

				lyTable[liIndex] = (byte)fNearest(pOPalette, lfR, lfG, lfB, liIndex);
			}

			return lyTable;
		}

		/// <summary>The candidate colour nearest to the given one, weighted as the eye weighs
		/// the channels; piFallback when none is nearer than it.</summary>
		private static int fNearest(UWPalette pOPalette, float pfR, float pfG, float pfB, int piFallback)
		{
			int liBest = piFallback;
			float lfBest = float.MaxValue;

			for (int liAt = 1; liAt <= LastCandidate; liAt++)
			{
				if (UWPaletteRotation.IsRotatingIndex(liAt))
					continue;

				UWColor32 lOColour = pOPalette.GetUWColor(liAt);
				float lfR = lOColour.R - pfR;
				float lfG = lOColour.G - pfG;
				float lfB = lOColour.B - pfB;
				float lfDistance = (0.30f * lfR * lfR) + (0.59f * lfG * lfG) + (0.11f * lfB * lfB);

				if (lfDistance < lfBest)
				{
					lfBest = lfDistance;
					liBest = liAt;
				}
			}

			return liBest;
		}
	}
}
