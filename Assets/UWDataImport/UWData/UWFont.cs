using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// An original font from "font*.sys" (uw-formats.txt 3.5). The characters are
	/// 1-bit bitmaps, each with its own width - i.e. non-monospaced in the sense of "every
	/// character as wide as it needs to be".
	///
	/// A Unity font (which the classic UI once used) cannot rebuild the original lettering
	/// pixel-exactly. This class provides the real character bitmaps, which
	/// UWFontRenderer draws.
	///
	/// The six fonts of the game:
	///   font4x5p.sys  small font
	///   font5x6i.sys  italic, character stats screen
	///   font5x6p.sys  normal font, scrolls and messages
	///   fontbig.sys   large font, cutscenes
	///   fontbutn.sys  buttons
	///   fontchar.sys  character creation
	/// </summary>
	public class UWFont
	{
		public struct Glyph
		{
			/// <summary>Width in pixels - differs per character.</summary>
			public int Width;

			/// <summary>Width*Height values, 1 where colour should go.</summary>
			public byte[] Pixels;
		}

		private const int HeaderSize = 12;

		private readonly Glyph[] mOGlyphs;

		public int Height { get; private set; }

		/// <summary>Width of the space character in pixels.</summary>
		public int SpaceWidth { get; private set; }

		public int MaxWidth { get; private set; }

		public int GlyphCount => mOGlyphs == null ? 0 : mOGlyphs.Length;

		public bool IsLoaded { get; private set; }

		public UWFont(string psFileName)
		{
			if (!File.Exists(psFileName))
			{
				mOGlyphs = new Glyph[0];
				return;
			}

			byte[] lyData = File.ReadAllBytes(psFileName);

			if (lyData.Length < HeaderSize)
			{
				mOGlyphs = new Glyph[0];
				return;
			}

			// 0x0000 is always 1 according to the docs and is not needed.
			int liCharSize = fRead16(lyData, 2);
			SpaceWidth = fRead16(lyData, 4);
			Height = fRead16(lyData, 6);
			int liRowBytes = fRead16(lyData, 8);
			MaxWidth = fRead16(lyData, 10);

			if (liCharSize <= 0 || Height <= 0 || liRowBytes <= 0)
			{
				mOGlyphs = new Glyph[0];
				return;
			}

			// Each character occupies charsize bytes of bitmap plus one byte of width.
			int liCount = (lyData.Length - HeaderSize) / (liCharSize + 1);
			mOGlyphs = new Glyph[liCount];

			for (int liIndex = 0; liIndex < liCount; liIndex++)
			{
				int liOffset = HeaderSize + (liIndex * (liCharSize + 1));
				int liWidth = lyData[liOffset + liCharSize];

				// According to the docs, fontbig.sys and font5x6p.sys contain characters wider
				// than the maxwidth field states - so the actual width of the character
				// is used here and not clamped to maxwidth.
				byte[] lyPixels = new byte[liWidth * Height];

				for (int liY = 0; liY < Height; liY++)
				{
					for (int liX = 0; liX < liWidth; liX++)
					{
						// Each image row starts at a byte boundary; the remaining bits
						// of a row stay unused.
						int liByte = liOffset + (liY * liRowBytes) + (liX >> 3);

						if (liByte >= lyData.Length)
							continue;

						int liBit = 7 - (liX & 7);
						lyPixels[(liY * liWidth) + liX] = (byte)((lyData[liByte] >> liBit) & 1);
					}
				}

				mOGlyphs[liIndex] = new Glyph
				{
					Width = liWidth,
					Pixels = lyPixels
				};
			}

			IsLoaded = true;
		}

		public bool TryGetGlyph(int piIndex, out Glyph pOGlyph)
		{
			if (mOGlyphs == null || piIndex < 0 || piIndex >= mOGlyphs.Length)
			{
				pOGlyph = default;
				return false;
			}

			pOGlyph = mOGlyphs[piIndex];
			return true;
		}

		/// <summary>Width of a string in pixels, with piSpacing pixels added after every
		/// character (see UWFontRenderer.CharacterSpacing).</summary>
		public int MeasureText(string psText, int piFirstCharacter, int piSpacing)
		{
			if (string.IsNullOrEmpty(psText))
				return 0;

			int liWidth = 0;

			for (int liAt = 0; liAt < psText.Length; liAt++)
			{
				char lcChar = psText[liAt];

				// A colour code (backslash and digit, see UWFontRenderer) has no width.
				if (IsColourCode(psText, liAt))
				{
					liAt++;
					continue;
				}

				if (lcChar == ' ')
				{
					liWidth += SpaceWidth + piSpacing;
					continue;
				}

				if (TryGetGlyph(lcChar - piFirstCharacter, out Glyph lOGlyph))
					liWidth += lOGlyph.Width + piSpacing;
			}

			return liWidth;
		}

		/// <summary>Whether a colour code of the game texts stands at this position: a backslash
		/// and a digit, e.g. "\\4Save Game Failed." (block 1). The digit selects the
		/// text colour, 0 is the ordinary one; the code itself is never drawn.</summary>
		public static bool IsColourCode(string psText, int piAt)
		{
			return psText != null && piAt >= 0 && piAt + 1 < psText.Length
				&& psText[piAt] == '\\' && psText[piAt + 1] >= '0' && psText[piAt + 1] <= '9';
		}

		private static int fRead16(byte[] pyData, int piOffset)
		{
			return pyData[piOffset] | (pyData[piOffset + 1] << 8);
		}
	}
}
