using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S CUTSCENE SUBTITLE (UW2 plan phase 5 part C): cutsop_txt's lines and cuts_draw_text's place,
	/// drawn into the indexed screen in FONT_BIG (show_cutscene sets it). The text is split at its newlines, at most six,
	/// and each to lines of 320 pixels at its spaces (a word measured with the space after it); at most six lines in
	/// all. Each line is centred on the window ((x + w - width) / 2), the first at y - h + height * lines + 2 counted up
	/// from the bottom, each next one a font height lower: the block ends two rows above the window's bottom.
	/// </summary>
	public static class UWCutsceneTextUw2
	{
		public const int LineWidth = 320;

		public const int MaxLines = 6;

		/// <summary>The lines of psText as cutsop_txt splits them.</summary>
		public static List<string> Split(UWFont pOFont, string psText)
		{
			List<string> lsLines = new List<string>();

			if (pOFont == null || string.IsNullOrEmpty(psText))
				return lsLines;

			string[] lsParts = psText.Split('\n');

			for (int liPart = 0; liPart < lsParts.Length && liPart < MaxLines && lsLines.Count < MaxLines; liPart++)
			{
				string[] lsWords = lsParts[liPart].Split(' ');
				string lsLine = string.Empty;
				int liUsed = 0;

				for (int liWord = 0; liWord < lsWords.Length; liWord++)
				{
					bool lbLast = liWord == lsWords.Length - 1;
					string lsPiece = lbLast ? lsWords[liWord] : lsWords[liWord] + " ";
					int liWidth = pOFont.MeasureText(lsPiece, 0, UWTextLayout.CharacterSpacing);

					if (liUsed + liWidth > LineWidth && lsLine.Length > 0)
					{
						lsLines.Add(lsLine.TrimEnd(' '));
						lsLine = string.Empty;
						liUsed = 0;

						if (lsLines.Count >= MaxLines)
							break;
					}

					lsLine += lsPiece;
					liUsed += liWidth;
				}

				if (lsLine.Length > 0 && lsLines.Count < MaxLines)
					lsLines.Add(lsLine);
			}

			return lsLines;
		}

		/// <summary>cuts_draw_text into the screen: the lines centred in the full-screen window, in palette index
		/// piColour (the glyphs' set pixels only).</summary>
		public static void Draw(UWCutsceneScreenUw2 pOScreen, UWFont pOFont, string psText, int piColour)
		{
			List<string> lsLines = Split(pOFont, psText);

			if (lsLines.Count == 0)
				return;

			int liHeight = pOFont.Height;

			// y - h + height * lines + 2 bottom-up for the window 0, 199, 320, 200: the first line's top row.
			int liTopBottomUp = (UWCutsceneScreenUw2.ScreenHeight - 1) - UWCutsceneScreenUw2.ScreenHeight + (liHeight * lsLines.Count) + 2;

			for (int liLine = 0; liLine < lsLines.Count; liLine++)
			{
				int liWidth = pOFont.MeasureText(lsLines[liLine], 0, UWTextLayout.CharacterSpacing);
				int liX = (UWCutsceneScreenUw2.ScreenWidth - liWidth) / 2;
				int liTop = (UWCutsceneScreenUw2.ScreenHeight - 1) - (liTopBottomUp - (liLine * liHeight));

				fDrawLine(pOScreen, pOFont, lsLines[liLine], liX, liTop, (byte)piColour);
			}
		}

		private static void fDrawLine(UWCutsceneScreenUw2 pOScreen, UWFont pOFont, string psLine, int piX, int piTop, byte pyColour)
		{
			int liX = piX;

			for (int liAt = 0; liAt < psLine.Length; liAt++)
			{
				if (UWFont.IsColourCode(psLine, liAt))
				{
					liAt++;
					continue;
				}

				char lcChar = psLine[liAt];

				if (lcChar == ' ')
				{
					liX += pOFont.SpaceWidth + UWTextLayout.CharacterSpacing;
					continue;
				}

				if (!pOFont.TryGetGlyph(lcChar, out UWFont.Glyph lOGlyph) || lOGlyph.Pixels == null)
					continue;

				for (int liY = 0; liY < pOFont.Height; liY++)
				{
					for (int liGx = 0; liGx < lOGlyph.Width; liGx++)
					{
						int liPixel = (liY * lOGlyph.Width) + liGx;

						if (liPixel < lOGlyph.Pixels.Length && lOGlyph.Pixels[liPixel] != 0)
							pOScreen.Put(liX + liGx, piTop + liY, pyColour);
					}
				}

				liX += lOGlyph.Width + UWTextLayout.CharacterSpacing;
			}
		}
	}
}
