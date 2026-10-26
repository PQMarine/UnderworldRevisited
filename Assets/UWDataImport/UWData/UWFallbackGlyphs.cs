using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Where the original fonts get a character they lack (translation wave 6, 2026-10-26): asked by
	/// UWFont.TryGetGlyph for a character beyond ASCII that is neither the font's own nor added
	/// (UWFontGlyphBuilder's German letters). The Unity layer gives one (UWFallbackFont: the language's
	/// font, grammar.rule.font, rasterised); without one - English, German - nothing is asked.
	/// </summary>
	public interface IUWGlyphSource
	{
		/// <summary>A glyph of piCode for pOFont - its height, in its style of rows (UWFallbackGlyphs.Build)
		/// - or false when no font of the language has the character.</summary>
		bool TryGetGlyph(UWFont pOFont, int piCode, out UWFont.Glyph pOGlyph);
	}

	/// <summary>One character rasterised by a font at a size, monochrome, as a rasteriser gives it.</summary>
	public struct UWRasterGlyph
	{
		/// <summary>Whether the font has the character at all.</summary>
		public bool Found;

		/// <summary>The bitmap's size; 0 for a character without ink (a blank).</summary>
		public int Width;

		public int Height;

		/// <summary>From the pen position to the bitmap's left edge, in pixels.</summary>
		public int BearingX;

		/// <summary>From the baseline up to the bitmap's top row, in pixels.</summary>
		public int BearingY;

		/// <summary>How far the pen moves on, in pixels.</summary>
		public int Advance;

		/// <summary>Width*Height values, rows top-down, not 0 where there is ink.</summary>
		public byte[] Pixels;
	}

	/// <summary>
	/// A CHARACTER OF THE LANGUAGE'S FONT IN AN ORIGINAL FONT'S ROWS (translation wave 6): the rule that
	/// fits a rasterised character into a glyph of a FONT*.SYS - engine-free, so it is checked with a
	/// made-up rasteriser in the self-check; the Unity layer only rasterises (UWFallbackFont).
	///
	///   - THE SIZE: the language's font is asked at the size where its capitals are as tall as the
	///     original font's 'H' (StartSize: the capital rows over the font's capital height per size,
	///     CapRatio) - a Cyrillic or Greek capital stands on the original's baseline at the original's
	///     capital height. A character taller than the original font's rows (an ideograph is about as
	///     tall as the whole size) is asked again one size smaller until it fits; a mark without ink at
	///     that size (a small comma rasterised away) up to two sizes larger.
	///   - THE PLACE: on the baseline below the 'H' (the rows under it are the descenders'), moved up or
	///     down only as far as needed to stay inside the rows; from the pen position at its bearing.
	///   - THE WIDTH: the font's advance, at least the ink and one free column - the original's glyphs
	///     carry their spacing in their width (UWTextLayout.CharacterSpacing is 0).
	/// The colour is the original's: a glyph is ink or not, UWTextRaster colours it.
	/// </summary>
	public static class UWFallbackGlyphs
	{
		/// <summary>The capital height per size where a font does not say (most Latin fonts are near it).</summary>
		public const float DefaultCapRatio = 0.7f;

		/// <summary>The first row of the original font's capitals (the 'H') and the row below its last -
		/// the baseline. Without an 'H': the font's top and its last row (the descenders' row).</summary>
		public static void CapitalRows(UWFont pOFont, out int piTop, out int piBaseline)
		{
			piTop = 0;
			piBaseline = pOFont.Height > 1 ? pOFont.Height - 1 : pOFont.Height;

			if (!pOFont.TryGetOwnGlyph('H', out UWFont.Glyph lOH) || lOH.Pixels == null || lOH.Width <= 0)
				return;

			int liFirst = -1;
			int liLast = -1;

			for (int y = 0; y < pOFont.Height; y++)
			{
				for (int x = 0; x < lOH.Width; x++)
				{
					if (lOH.Pixels[(y * lOH.Width) + x] == 0)
						continue;

					if (liFirst < 0)
						liFirst = y;

					liLast = y;
				}
			}

			if (liFirst < 0)
				return;

			piTop = liFirst;
			piBaseline = liLast + 1;
		}

		/// <summary>The size the language's font is asked at first: its capitals as tall as the original's
		/// (pfCapRatio: the font's capital height per size).</summary>
		public static int StartSize(UWFont pOFont, float pfCapRatio)
		{
			CapitalRows(pOFont, out int liTop, out int liBaseline);

			float lfRatio = pfCapRatio > 0.05f && pfCapRatio < 2f ? pfCapRatio : DefaultCapRatio;

			return Math.Max(1, (int)Math.Round((liBaseline - liTop) / lfRatio, MidpointRounding.AwayFromZero));
		}

		/// <summary>
		/// The glyph of a character for pOFont (see the class): pRasterAt rasterises the character at a
		/// size. False when the language's font does not have it.
		/// </summary>
		public static bool Build(UWFont pOFont, float pfCapRatio, Func<int, UWRasterGlyph> pRasterAt, out UWFont.Glyph pOGlyph)
		{
			pOGlyph = default;

			if (pOFont == null || !pOFont.IsLoaded || pOFont.Height <= 0 || pRasterAt == null)
				return false;

			CapitalRows(pOFont, out int liTop, out int liBaseline);

			UWRasterGlyph lOGlyph = default;
			int liStart = StartSize(pOFont, pfCapRatio);
			int liSize = liStart;

			for (; liSize >= 1; liSize--)
			{
				lOGlyph = pRasterAt(liSize);

				if (!lOGlyph.Found)
					return false;

				if (lOGlyph.Height <= pOFont.Height)
					break;
			}

			// A small mark can vanish at a small size (the ideographic comma at 6 pixels, measured with
			// Microsoft JhengHei, 2026-10-26): it is asked up to two sizes larger while it still fits.
			for (int liLarger = liSize + 1; !fHasInk(lOGlyph) && lOGlyph.Width > 0 && liLarger <= liSize + 2; liLarger++)
			{
				UWRasterGlyph lOLarger = pRasterAt(liLarger);

				if (lOLarger.Found && lOLarger.Height <= pOFont.Height && fHasInk(lOLarger))
					lOGlyph = lOLarger;
			}

			pOGlyph = Place(pOFont, liBaseline, lOGlyph);

			return true;
		}

		private static bool fHasInk(UWRasterGlyph pOGlyph)
		{
			if (pOGlyph.Pixels == null)
				return false;

			foreach (byte lyPixel in pOGlyph.Pixels)
			{
				if (lyPixel != 0)
					return true;
			}

			return false;
		}

		/// <summary>A rasterised character on the original font's baseline (see the class); rows beyond the
		/// font's are cut.</summary>
		public static UWFont.Glyph Place(UWFont pOFont, int piBaseline, UWRasterGlyph pOGlyph)
		{
			int liHeight = pOFont.Height;
			bool lbInk = pOGlyph.Width > 0 && pOGlyph.Height > 0 && pOGlyph.Pixels != null
				&& pOGlyph.Pixels.Length >= pOGlyph.Width * pOGlyph.Height;
			int liLeft = Math.Max(0, pOGlyph.BearingX);
			int liWidth = lbInk ? Math.Max(pOGlyph.Advance, liLeft + pOGlyph.Width + 1) : Math.Max(1, pOGlyph.Advance);
			byte[] lyPixels = new byte[liWidth * liHeight];

			if (lbInk)
			{
				int liRowTop = piBaseline - pOGlyph.BearingY;

				if (pOGlyph.Height <= liHeight)
					liRowTop = Math.Min(Math.Max(liRowTop, 0), liHeight - pOGlyph.Height);

				for (int y = 0; y < pOGlyph.Height; y++)
				{
					int liRow = liRowTop + y;

					if (liRow < 0 || liRow >= liHeight)
						continue;

					for (int x = 0; x < pOGlyph.Width; x++)
					{
						if (pOGlyph.Pixels[(y * pOGlyph.Width) + x] != 0)
							lyPixels[(liRow * liWidth) + liLeft + x] = 1;
					}
				}
			}

			return new UWFont.Glyph { Width = liWidth, Pixels = lyPixels };
		}
	}
}
