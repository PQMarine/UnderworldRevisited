using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// TEXT IN THE ORIGINAL FONTS AS PIXELS (cleanup step 5.5, 2026-10-19, decision 26 of the
	/// cleanup plan: the interface must not hang on Unity). The lines are drawn into our own
	/// picture (UWPicture, rows top down, the background 0,0,0,0) from the font's glyphs; an engine
	/// only uploads the finished picture (UWTextTextures through UWTextureFactory on the Unity
	/// side). It replaced UWFontRenderer.RenderLines, which made a new Unity texture per call and set
	/// every glyph pixel with its own SetPixel, the conversation's line-by-line composition (one
	/// temporary texture per line, read back and destroyed) and the prompt cursor that was drawn into
	/// an already finished texture (UWHudMessageLog.fDrawPromptCursor).
	///
	/// THE PIXELS ARE THOSE OF THE OLD RENDERER, one for one (the self-check TextRaster compares them
	/// against its loop for every font): a line starts at x 0, or centred at (width - line width) / 2
	/// but never left of 0; a space advances SpaceWidth, a glyph its width, both plus
	/// UWTextLayout.CharacterSpacing; a glyph the font lacks draws nothing and does not advance;
	/// whatever falls outside the picture is cut off. The lines lie Height + LineSpacing apart and the
	/// picture is that times the line count, less one LineSpacing, tall.
	///
	/// TWO WAYS TO COLOUR:
	///   Codes   - the colour codes of the game texts (UWFont.IsColourCode): a backslash and a digit
	///             switch to table[digit], 0 the normal colour, and the switch holds across lines,
	///             as in the original ("\\6     Please enter a save file description:" also colours
	///             the typed line below it); a digit beyond the table changes nothing. A table of
	///             one colour draws everything in it.
	///   PerLine - every line in its own colour (the conversation's history and answers, which the
	///             old code drew line by line in one colour each): a code is skipped, \0 means the
	///             line's colour, the others change nothing.
	/// Either way the codes are never drawn, and the colour at the start of each line is reported -
	/// the modern font (UWTextLabel) colours its lines with it.
	/// </summary>
	public static class UWTextRaster
	{
		public enum ColoursEnum
		{
			Codes = 0,
			PerLine = 1
		}

		/// <summary>The colour of the lines when no colour is given.</summary>
		public static readonly UWColor32 White = new UWColor32(255, 255, 255, 255);

		/// <summary>The distance from one line's top to the next one's.</summary>
		public static int LineHeight(UWFont pOFont)
		{
			return pOFont.Height + UWTextLayout.LineSpacing;
		}

		/// <summary>The picture's height for so many lines (the old renderer's formula).</summary>
		public static int Height(UWFont pOFont, int piLines)
		{
			return (LineHeight(pOFont) * piLines) - UWTextLayout.LineSpacing;
		}

		/// <summary>
		/// A picture exactly as wide as one line of text, at least one pixel: MeasureText counts one
		/// CharacterSpacing after the last character too, which is taken off - otherwise one pixel of
		/// padding would remain (the stats lines, the map's level number and notes, [MORE]; measured
		/// against the original 2026-09-03).
		/// </summary>
		public static int TightWidth(UWFont pOFont, string psText)
		{
			return Math.Max(1, pOFont.MeasureText(psText, 0, UWTextLayout.CharacterSpacing) - UWTextLayout.CharacterSpacing);
		}

		/// <summary>
		/// The lines drawn into a new picture piWidth wide (see the class comment), or null when
		/// there is nothing to draw (no font, no lines, no width). pyColours is the code table or the
		/// colour per line (the last one for the lines beyond it); null or empty draws white.
		/// pOLineColours, if given, receives the colour at the start of each line. pbCursor adds
		/// the blinking input box behind the last line (see CursorBox).
		/// </summary>
		public static UWPicture Render(UWFont pOFont, IReadOnlyList<string> pOLines, int piWidth, UWColor32[] pyColours,
			ColoursEnum peColours, bool pbCentred, bool pbCursor = false, List<UWColor32> pOLineColours = null)
		{
			if (pOFont == null || !pOFont.IsLoaded || pOLines == null || pOLines.Count == 0 || piWidth <= 0)
				return null;

			int liHeight = Height(pOFont, pOLines.Count);

			if (liHeight <= 0)
				return null;

			UWPicture lOPicture = new UWPicture(piWidth, liHeight);
			UWColor32[] lyOne = peColours == ColoursEnum.PerLine ? new UWColor32[1] : null;
			UWColor32 lOColour = pyColours != null && pyColours.Length > 0 ? pyColours[0] : White;
			UWColor32 lOLastLineColour = lOColour;

			for (int liLine = 0; liLine < pOLines.Count; liLine++)
			{
				UWColor32[] lyTable = pyColours;

				if (lyOne != null)
				{
					lOColour = pyColours != null && pyColours.Length > 0 ? pyColours[Math.Min(liLine, pyColours.Length - 1)] : White;
					lyOne[0] = lOColour;
					lyTable = lyOne;
				}

				string lsLine = pOLines[liLine] ?? string.Empty;
				int liLeft = pbCentred ? Math.Max(0, (piWidth - pOFont.MeasureText(lsLine, 0, UWTextLayout.CharacterSpacing)) / 2) : 0;

				pOLineColours?.Add(lOColour);
				lOLastLineColour = lOColour;

				DrawLine(lOPicture, pOFont, lsLine, liLeft, liLine * LineHeight(pOFont), lyTable, ref lOColour);
			}

			if (pbCursor)
			{
				UWRectInt lOBox = CursorBox(pOFont, pOLines, piWidth, liHeight);

				Fill(lOPicture, lOBox, lOLastLineColour);
			}

			return lOPicture;
		}

		/// <summary>
		/// One line into a picture, its top-left corner at piLeft / piTop; pORunningColour is the
		/// colour in effect and comes back as the colour at the line's end (a code switches it to
		/// pyTable[digit]; null or a digit beyond the table leaves it).
		/// </summary>
		public static void DrawLine(UWPicture pOTarget, UWFont pOFont, string psLine, int piLeft, int piTop, UWColor32[] pyTable,
			ref UWColor32 pORunningColour)
		{
			int liCursor = piLeft;

			for (int liAt = 0; liAt < psLine.Length; liAt++)
			{
				char lcChar = psLine[liAt];

				if (UWFont.IsColourCode(psLine, liAt))
				{
					int liCode = psLine[liAt + 1] - '0';

					if (pyTable != null && liCode < pyTable.Length)
						pORunningColour = pyTable[liCode];

					liAt++;
					continue;
				}

				if (lcChar == ' ')
				{
					liCursor += pOFont.SpaceWidth + UWTextLayout.CharacterSpacing;
					continue;
				}

				// The character set starts at ASCII 0 - see UWFont.
				if (!pOFont.TryGetGlyph(lcChar, out UWFont.Glyph lOGlyph))
					continue;

				for (int liY = 0; liY < pOFont.Height; liY++)
				{
					int liTargetY = piTop + liY;

					if (liTargetY < 0 || liTargetY >= pOTarget.Height)
						continue;

					for (int liX = 0; liX < lOGlyph.Width; liX++)
					{
						if (lOGlyph.Pixels[(liY * lOGlyph.Width) + liX] == 0)
							continue;

						int liTargetX = liCursor + liX;

						if (liTargetX >= 0 && liTargetX < pOTarget.Width)
							pOTarget.Set(liTargetX, liTargetY, pORunningColour);
					}
				}

				liCursor += lOGlyph.Width + UWTextLayout.CharacterSpacing;
			}
		}

		/// <summary>
		/// The blinking box behind a running input (UWHudMessageLog.fDrawPromptCursor until
		/// 2026-10-19; the font has no character for it, so it is filled by hand, its size
		/// estimated): right after the last line's text, Max(2, SpaceWidth) wide and the font's
		/// height tall, cut to the picture. It is drawn as part of the picture now - a texture handed
		/// out is never changed afterwards.
		/// </summary>
		public static UWRectInt CursorBox(UWFont pOFont, IReadOnlyList<string> pOLines, int piWidth, int piHeight)
		{
			if (pOLines == null || pOLines.Count == 0)
				return new UWRectInt(0, 0, 0, 0);

			int liLast = pOLines.Count - 1;
			int liLeft = pOFont.MeasureText(pOLines[liLast] ?? string.Empty, 0, UWTextLayout.CharacterSpacing);
			int liTop = liLast * LineHeight(pOFont);
			int liRight = Math.Min(piWidth, liLeft + Math.Max(2, pOFont.SpaceWidth));
			int liBottom = Math.Min(piHeight, liTop + pOFont.Height);

			return new UWRectInt(liLeft, liTop, Math.Max(0, liRight - liLeft), Math.Max(0, liBottom - liTop));
		}

		/// <summary>A rectangle of the picture in one colour (nothing outside the picture).</summary>
		public static void Fill(UWPicture pOTarget, UWRectInt pORect, UWColor32 pOColour)
		{
			for (int liY = Math.Max(0, pORect.Y); liY < Math.Min(pOTarget.Height, pORect.Y + pORect.Height); liY++)
			{
				for (int liX = Math.Max(0, pORect.X); liX < Math.Min(pOTarget.Width, pORect.X + pORect.Width); liX++)
					pOTarget.Set(liX, liY, pOColour);
			}
		}
	}
}
