using System.Collections.Generic;
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
	/// UWTextRaster draws.
	///
	/// The six fonts of the game:
	///   font4x5p.sys  small font
	///   font5x6i.sys  italic, character stats screen
	///   font5x6p.sys  normal font, scrolls and messages
	///   fontbig.sys   large font, cutscenes
	///   fontbutn.sys  buttons
	///   fontchar.sys  character creation
	///
	/// The files hold ASCII only. Characters beyond them can be added at run time (AddGlyph):
	/// UWFonts adds the German letters UWFontGlyphBuilder draws in each font's style (translations
	/// wave 1, 2026-10-26); TryGetGlyph, MeasureText and everything drawing through them count
	/// those like the font's own. What is still missing beyond ASCII comes from the language's font
	/// where it names one (Fallback, translation wave 6).
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

		/// <summary>The characters added at run time beyond the font's own (AddGlyph), by their
		/// Unicode code; null while there are none.</summary>
		private SortedDictionary<int, Glyph> mOAddedGlyphs;

		public UWFont(string psFileName)
			: this(File.Exists(psFileName) ? File.ReadAllBytes(psFileName) : null)
		{
		}

		/// <summary>The font from the bytes of a font*.sys (null or too short: an empty font that is
		/// not loaded).</summary>
		public UWFont(byte[] pyData)
		{
			byte[] lyData = pyData;

			if (lyData == null || lyData.Length < HeaderSize)
			{
				mOGlyphs = new Glyph[0];
				return;
			}

			// 0x0000 is always 1 according to the docs and is not needed.
			int liCharSize = UWBinary.Read16(lyData, 2);
			SpaceWidth = UWBinary.Read16(lyData, 4);
			Height = UWBinary.Read16(lyData, 6);
			int liRowBytes = UWBinary.Read16(lyData, 8);
			MaxWidth = UWBinary.Read16(lyData, 10);

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

		/// <summary>The glyph of a character: the font's own below GlyphCount, an added one (AddGlyph)
		/// beyond it, and beyond ASCII one of the language's font (Fallback, translation wave 6) where the
		/// language names one.</summary>
		public bool TryGetGlyph(int piIndex, out Glyph pOGlyph)
		{
			if (TryGetOwnGlyph(piIndex, out pOGlyph))
				return true;

			return fTryFallback(piIndex, out pOGlyph);
		}

		/// <summary>The font's own glyph or an added one (AddGlyph), never the language's font - for whoever
		/// copies the font's glyphs elsewhere (UWUiFonts' atlas) or builds on them (UWFontGlyphBuilder).</summary>
		public bool TryGetOwnGlyph(int piIndex, out Glyph pOGlyph)
		{
			if (mOGlyphs == null || piIndex < 0 || piIndex >= mOGlyphs.Length)
			{
				if (mOAddedGlyphs != null && mOAddedGlyphs.TryGetValue(piIndex, out pOGlyph))
					return true;

				pOGlyph = default;
				return false;
			}

			pOGlyph = mOGlyphs[piIndex];
			return true;
		}

		// ------------------------------------------------- The language's font (translation wave 6)

		private static IUWGlyphSource msFallback;

		/// <summary>
		/// THE LANGUAGE'S FONT for the characters the original fonts lack (IUWGlyphSource; the Unity layer
		/// sets it with the language - UWFallbackFont, from grammar.rule.font). Asked only beyond ASCII,
		/// for a character neither the font's own nor added: ASCII and the German letters draw exactly as
		/// before, with or without it. Null: none (English, German).
		/// </summary>
		public static IUWGlyphSource Fallback
		{
			get { return msFallback; }
			set
			{
				if (ReferenceEquals(msFallback, value))
					return;

				msFallback = value;
				FallbackVersion++;
			}
		}

		/// <summary>Counts the changes of Fallback - every font forgets the glyphs it had from the one
		/// before, and whoever keeps pictures of text (UWTextTextures) draws them anew.</summary>
		public static int FallbackVersion { get; private set; }

		/// <summary>The glyphs this font had from Fallback (null: the source has no such character), as of
		/// miFallbackVersion.</summary>
		private Dictionary<int, Glyph?> mOFallbackGlyphs;

		private int miFallbackVersion = -1;

		private bool fTryFallback(int piCode, out Glyph pOGlyph)
		{
			pOGlyph = default;

			IUWGlyphSource lISource = msFallback;

			if (lISource == null || piCode <= 0x7E || !IsLoaded)
				return false;

			if (mOFallbackGlyphs == null || miFallbackVersion != FallbackVersion)
			{
				mOFallbackGlyphs = new Dictionary<int, Glyph?>();
				miFallbackVersion = FallbackVersion;
			}

			if (!mOFallbackGlyphs.TryGetValue(piCode, out Glyph? lOKnown))
			{
				lOKnown = null;

				if (lISource.TryGetGlyph(this, piCode, out Glyph lOMade) && lOMade.Width > 0 && lOMade.Pixels != null
					&& lOMade.Pixels.Length == lOMade.Width * Height)
					lOKnown = lOMade;

				mOFallbackGlyphs[piCode] = lOKnown;
			}

			if (lOKnown == null)
				return false;

			pOGlyph = lOKnown.Value;
			return true;
		}

		/// <summary>
		/// A character added at run time (UWFontGlyphBuilder: the German letters, translations wave
		/// 1, 2026-10-26), under its Unicode code. Only beyond the font's own glyphs - a character the
		/// font has keeps its own, so the English texts draw exactly as before - and only a glyph of
		/// the font's height; false otherwise.
		/// </summary>
		public bool AddGlyph(int piCode, Glyph pOGlyph)
		{
			if (!IsLoaded || piCode < GlyphCount || pOGlyph.Width <= 0 || pOGlyph.Pixels == null
				|| pOGlyph.Pixels.Length != pOGlyph.Width * Height)
				return false;

			if (mOAddedGlyphs == null)
				mOAddedGlyphs = new SortedDictionary<int, Glyph>();

			mOAddedGlyphs[piCode] = pOGlyph;
			return true;
		}

		/// <summary>The codes of the added characters, in ascending order.</summary>
		public IEnumerable<int> AddedCodes
		{
			get { return mOAddedGlyphs != null ? mOAddedGlyphs.Keys : (IEnumerable<int>)new int[0]; }
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
	}
}
