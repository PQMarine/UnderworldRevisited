using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE WORDS PAINTED INTO THE ORIGINAL'S PICTURES, IN THE LANGUAGE (Docs/PICTURE-WORDS-PLAN.md, per user
	/// 2026-11-08): the options panel's buttons and headings carry English as pixels, not as strings. Where
	/// the language's text for a picture's box differs from the English one, the box's lettering is painted
	/// over with the button's own grain and the text written anew as the original wrote it - FONT4X5P's
	/// capitals in the picture's own ink, its shadow one pixel left and one down. English (and a language
	/// that leaves the text as it is) gets the picture handed out unchanged, the same object.
	///
	///   - THE INK: in the box, the bright and nearly grey colour (204,204,220 idle, 252,252,252 lit,
	///     220,220,220 on YES and NO - the picture's most frequent such palette index); THE SHADOW: the
	///     palette index most often found one pixel left and one down of an ink pixel. Both measured on
	///     OPTBTNS.GR 2026-11-08: "SAVE", "GAME", "RESTORE" in FONT4X5P match the pictures pixel for pixel.
	///   - PAINTING OVER: every ink pixel and its shadow take the nearest other pixel of the same row
	///     inside the box (then of the rows above and below), so the button's grain stays.
	///   - WRITING: the text in upper case, broken at blanks (or "\n") into lines that fit the box's width;
	///     lines six rows apart, the block centred in the box with the extra row below, each line centred
	///     with its shadow column counted - the English words come back where they were. A box may name the
	///     rows of its lines (Tops) where the original spaced them otherwise.
	///
	/// Engine-free: the game's pictures pass through Localize where UWTextures hands them out, so every look
	/// of a picture (icon, colours, the modern pages) follows. A copy is kept per picture until the language
	/// changes (UWText.LanguageVersion). The second game's pictures are a later step: only the first game's
	/// table exists.
	/// </summary>
	public sealed class UWPictureWords
	{
		/// <summary>One box of lettering in a picture and the text id that replaces it.</summary>
		public sealed class Box
		{
			public int X;

			public int Y;

			public int Width;

			public int Height;

			public string TextId;

			/// <summary>The rows of the lines' tops where the original placed them otherwise than the rule -
			/// used when the text has as many lines; null: the rule.</summary>
			public int[] Tops;

			/// <summary>The font the words are written in: FONT4X5P on the options panel, FONT5X6I on the stats
			/// page and the map, FONTCHAR for the map's "Level".</summary>
			public UWFonts.FontType Font = UWFonts.FontType.Small;

			/// <summary>Dark ink on a light ground without a shadow (the stats page, the map): the ink is what
			/// is clearly darker than the box's middle brightness. Else bright ink with its shadow.</summary>
			public bool Dark;

			/// <summary>The dark ink's limit, a part of the box's median brightness: 0.7 on the stats page, whose
			/// brown has dark grain of its own; 0.85 on the map, whose words are pressed in with a lighter rim
			/// too (left at 0.7, ERASE and CLOSE showed their rims beside the new words, per user's screenshot
			/// 2026-11-08).</summary>
			public float DarkLimit = 0.7f;

			/// <summary>Lines from the box's left edge (the stats page's labels); else centred.</summary>
			public bool Left;

			/// <summary>Columns added between glyphs (negative: closer): the map's "Level" is set a column closer
			/// than FONTCHAR's own spacing - 29 pixels in the picture, 33 in the font (per user's screenshot
			/// 2026-11-08: "Ebene" in the font's spacing reached the level number).</summary>
			public int Spacing;

			/// <summary>The text in upper case, as the original's lettering (all but the map's "Level").</summary>
			public bool Upper = true;

			public Box(int piX, int piY, int piWidth, int piHeight, string psTextId, params int[] paTops)
			{
				X = piX;
				Y = piY;
				Width = piWidth;
				Height = piHeight;
				TextId = psTextId;
				Tops = paTops != null && paTops.Length > 0 ? paTops : null;
			}
		}

		/// <summary>A line's pitch: four rows of glyph, the shadow's row, one free.</summary>
		public const int LinePitch = 6;

		/// <summary>The rows of a line with its shadow.</summary>
		public const int LineRows = 5;

		private readonly UWFonts mOFonts;

		/// <summary>The small font (FONT4X5P), the options panel's.</summary>
		private readonly UWFont mOFont;

		private readonly Dictionary<(UWTexture.TextureTypes, int), Box[]> mOTable;

		private readonly Dictionary<(UWTexture.TextureTypes, int), UWTexture> mOMade = new Dictionary<(UWTexture.TextureTypes, int), UWTexture>();

		private int miVersion = -1;

		private int miFilesVersion = -1;

		private readonly bool mbFirstGame;

		/// <summary>The language's own pictures (UWPicturePack, step 3): set with the language by the Unity layer
		/// (UWPictureFiles), null for none. One for every game's words, as the language is.</summary>
		public static IUWPictureFiles Files
		{
			get { return msFiles; }
			set
			{
				msFiles = value;
				msFilesVersion++;
			}
		}

		private static IUWPictureFiles msFiles;

		private static int msFilesVersion;

		/// <summary>Why a language's picture was not taken (a wrong size, unreadable), each once - for the log.</summary>
		public static readonly List<string> FileProblems = new List<string>();

		/// <summary>The words of a game's pictures, written in its own fonts.</summary>
		public UWPictureWords(UWFonts pOFonts, UWGame peGame)
		{
			mOFonts = pOFonts;
			mbFirstGame = peGame == UWGame.Uw1;
			mOFont = pOFonts?.Get(UWFonts.FontType.Small);
			mOTable = peGame == UWGame.Uw1 ? fFirstGameTable() : new Dictionary<(UWTexture.TextureTypes, int), Box[]>();
		}

		/// <summary>The boxes of a picture, or null where it carries no words.</summary>
		public Box[] BoxesOf(UWTexture.TextureTypes peType, int piIndex)
		{
			return mOTable.TryGetValue((peType, piIndex), out Box[] laBoxes) ? laBoxes : null;
		}

		/// <summary>Whether a kind of picture has any with words or of the language's own (UWTextures.GetTexturesByType).</summary>
		public bool HasType(UWTexture.TextureTypes peType)
		{
			if (msFiles != null)
				return true;

			foreach ((UWTexture.TextureTypes Type, int Index) lKey in mOTable.Keys)
			{
				if (lKey.Type == peType)
					return true;
			}

			return false;
		}

		/// <summary>
		/// The pictures with English in them a translation may replace with its own (uwdump picture-template):
		/// those with words written anew, and the first game's main menu captions (OPBTN.GR 0 to 7, idle and lit,
		/// in a decorative script) and its congratulations at the end (WIN1.BYT), which only a painter can
		/// translate.
		/// </summary>
		public IEnumerable<(UWTexture.TextureTypes Type, int Index)> TranslatablePictures
		{
			get
			{
				foreach ((UWTexture.TextureTypes Type, int Index) lKey in mOTable.Keys)
					yield return lKey;

				if (!mbFirstGame)
					yield break;

				for (int liAt = 0; liAt < 8; liAt++)
					yield return (UWTexture.TextureTypes.OPBTN, liAt);

				yield return (UWTexture.TextureTypes.WIN1, 0);
			}
		}

		/// <summary>Every picture with words, for the checks.</summary>
		public IEnumerable<(UWTexture.TextureTypes Type, int Index)> Pictures => mOTable.Keys;

		/// <summary>
		/// The picture as the language shows it: the same object where nothing is to change (English, a
		/// text left as it is, a picture without words), else a copy with its words written anew - kept
		/// until the language changes.
		/// </summary>
		public UWTexture Localize(UWTexture pOPicture, UWTexture.TextureTypes peType, int piIndex)
		{
			if (pOPicture == null)
				return pOPicture;

			IUWPictureFiles lOFiles = msFiles;
			bool lbBoxes = mOFonts != null && mOTable.ContainsKey((peType, piIndex));

			// Most pictures carry no words and the language has no picture of theirs: as they are, at once.
			if (!lbBoxes && (lOFiles == null || !lOFiles.Has(UWPicturePack.FileName(peType, piIndex))))
				return pOPicture;

			if (miVersion != UWText.LanguageVersion || miFilesVersion != msFilesVersion)
			{
				mOMade.Clear();
				miVersion = UWText.LanguageVersion;
				miFilesVersion = msFilesVersion;
			}

			if (mOMade.TryGetValue((peType, piIndex), out UWTexture lOMade))
				return lOMade ?? pOPicture;

			lOMade = fFromFile(lOFiles, pOPicture, peType, piIndex);

			if (lOMade == null && lbBoxes)
				lOMade = Write(pOPicture, mOTable[(peType, piIndex)], psId => UWText.T(psId), psId => UWText.English(psId));

			mOMade[(peType, piIndex)] = lOMade;

			return lOMade ?? pOPicture;
		}

		/// <summary>The language's own picture in the original's palette, or null (none, unreadable, another size -
		/// FileProblems says which).</summary>
		private static UWTexture fFromFile(IUWPictureFiles pOFiles, UWTexture pOPicture, UWTexture.TextureTypes peType, int piIndex)
		{
			string lsName = UWPicturePack.FileName(peType, piIndex);

			if (pOFiles == null || !pOFiles.Has(lsName))
				return null;

			if (!pOFiles.TryRead(lsName, out int liWidth, out int liHeight, out UWColor32[] lyPixels) || lyPixels == null)
			{
				fProblem(lsName + ": cannot be read - left as the original");

				return null;
			}

			if (liWidth != pOPicture.Width || liHeight != pOPicture.Height || lyPixels.Length != liWidth * liHeight)
			{
				fProblem(lsName + ": " + liWidth + " x " + liHeight + ", the original is " + pOPicture.Width + " x " + pOPicture.Height
					+ " - left as the original");

				return null;
			}

			return pOPicture.WithPixels(UWPicturePack.ToIndices(pOPicture, lyPixels));
		}

		private static void fProblem(string psProblem)
		{
			lock (FileProblems)
			{
				if (!FileProblems.Contains(psProblem))
					FileProblems.Add(psProblem);
			}
		}

		/// <summary>
		/// The picture with the boxes whose text differs from the English written anew; null when none
		/// differs. pText and pEnglish give a box's text in the language and in English.
		/// </summary>
		public UWTexture Write(UWTexture pOPicture, Box[] paBoxes, Func<string, string> pText, Func<string, string> pEnglish)
		{
			byte[] lyPixels = null;
			UWColor32[] lyColours = null;

			foreach (Box lOBox in paBoxes)
			{
				string lsText = pText(lOBox.TextId) ?? string.Empty;

				if (lsText == (pEnglish(lOBox.TextId) ?? string.Empty))
					continue;

				UWFont lOFont = FontOf(lOBox);

				if (lOFont == null || !lOFont.IsLoaded)
					continue;

				lyColours = lyColours ?? pOPicture.GetUWColor32();
				lyPixels = lyPixels ?? (byte[])pOPicture.PaletteIndices.Clone();

				fWriteBox(lyPixels, lyColours, pOPicture.Width, pOPicture.Height, lOBox, lsText, lOFont);
			}

			return lyPixels == null ? null : pOPicture.WithPixels(lyPixels);
		}

		/// <summary>A box's font.</summary>
		public UWFont FontOf(Box pOBox)
		{
			return mOFonts?.Get(pOBox.Font);
		}

		/// <summary>
		/// The picture with the lettering of its boxes painted over and nothing written: for words the game
		/// draws as text of its own instead (the stats page's labels in the modern font, UWHudPanel). Null
		/// where the picture has no boxes.
		/// </summary>
		public UWTexture Erase(UWTexture pOPicture, Box[] paBoxes)
		{
			if (pOPicture == null || paBoxes == null)
				return null;

			UWColor32[] lyColours = pOPicture.GetUWColor32();
			byte[] lyPixels = (byte[])pOPicture.PaletteIndices.Clone();

			foreach (Box lOBox in paBoxes)
				fWriteBox(lyPixels, lyColours, pOPicture.Width, pOPicture.Height, lOBox, null, FontOf(lOBox));

			return pOPicture.WithPixels(lyPixels);
		}

		/// <summary>The lines a text takes in a box of the small font, upper case (the options panel).</summary>
		public List<string> Lines(string psText, int piWidth)
		{
			return Lines(psText, piWidth, mOFont, true, 0);
		}

		/// <summary>The lines a text takes in a box: upper case where the box writes so, broken at "\n" and at
		/// blanks where a line would be wider than the box (a single word wider than it stays whole - uwdump fit
		/// reports it).</summary>
		public List<string> Lines(string psText, int piWidth, UWFont pOFont, bool pbUpper, int piSpacing)
		{
			List<string> lsLines = new List<string>();
			string lsText = psText ?? string.Empty;

			foreach (string lsPart in (pbUpper ? lsText.ToUpperInvariant() : lsText).Replace("\\n", "\n").Split('\n'))
			{
				string lsLine = string.Empty;

				foreach (string lsWord in lsPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
				{
					string lsTry = lsLine.Length == 0 ? lsWord : lsLine + " " + lsWord;

					if (lsLine.Length > 0 && LineWidth(pOFont, lsTry, piSpacing) + 1 > piWidth)
					{
						lsLines.Add(lsLine);
						lsLine = lsWord;
					}
					else
						lsLine = lsTry;
				}

				lsLines.Add(lsLine);
			}

			return lsLines;
		}

		/// <summary>A line's ink width in pixels in the small font, without the free column after its last glyph.</summary>
		public int LineWidth(string psLine)
		{
			return LineWidth(mOFont, psLine, 0);
		}

		/// <summary>A line's ink width in pixels with piSpacing more columns between glyphs, without the free
		/// column after its last glyph.</summary>
		public static int LineWidth(UWFont pOFont, string psLine, int piSpacing)
		{
			int liWidth = pOFont == null ? 0 : pOFont.MeasureText(psLine, 0, piSpacing);

			return liWidth > 0 ? liWidth - piSpacing - 1 : 0;
		}

		/// <summary>Whether a text fits its box: as many lines as the box holds, none wider than it. The
		/// widest line and the line count for the report.</summary>
		public bool Fits(Box pOBox, string psText, out int piWidest, out int piLines)
		{
			UWFont lOFont = FontOf(pOBox);
			List<string> lsLines = Lines(psText, pOBox.Width, lOFont, pOBox.Upper, pOBox.Spacing);

			piLines = lsLines.Count;
			piWidest = 0;

			foreach (string lsLine in lsLines)
				piWidest = Math.Max(piWidest, LineWidth(lOFont, lsLine, pOBox.Spacing) + (pOBox.Dark ? 0 : 1));

			// The small font's lines six rows apart; another font's lines only where the box names them.
			bool lbRows = pOBox.Tops != null ? lsLines.Count <= pOBox.Tops.Length
				: pOBox.Font == UWFonts.FontType.Small && lsLines.Count * LinePitch - 1 <= pOBox.Height;

			return piWidest <= pOBox.Width && lbRows;
		}

		private void fWriteBox(byte[] pyPixels, UWColor32[] pyColours, int piWidth, int piHeight, Box pOBox, string psText, UWFont pOFont)
		{
			// The ink and its shadow, as the picture has them.
			Dictionary<int, int> lOInk = new Dictionary<int, int>();
			Func<UWColor32, bool> fIsInk = pOBox.Dark ? fDarkInk(pyColours, piWidth, piHeight, pOBox) : (Func<UWColor32, bool>)fIsBrightInk;

			for (int y = pOBox.Y; y < pOBox.Y + pOBox.Height && y < piHeight; y++)
			{
				for (int x = pOBox.X; x < pOBox.X + pOBox.Width && x < piWidth; x++)
				{
					if (fIsInk(pyColours[(y * piWidth) + x]))
						lOInk[pyPixels[(y * piWidth) + x]] = lOInk.TryGetValue(pyPixels[(y * piWidth) + x], out int liCount) ? liCount + 1 : 1;
				}
			}

			if (lOInk.Count == 0)
				return;

			int liInk = fMost(lOInk);
			Dictionary<int, int> lOShadow = new Dictionary<int, int>();
			bool[] lbErase = new bool[pyPixels.Length];

			for (int y = pOBox.Y; y < pOBox.Y + pOBox.Height && y < piHeight; y++)
			{
				for (int x = pOBox.X; x < pOBox.X + pOBox.Width && x < piWidth; x++)
				{
					if (!fIsInk(pyColours[(y * piWidth) + x]))
						continue;

					lbErase[(y * piWidth) + x] = true;

					if (!pOBox.Dark && x > 0 && y + 1 < piHeight && !fIsInk(pyColours[((y + 1) * piWidth) + x - 1]))
					{
						int liShadow = pyPixels[((y + 1) * piWidth) + x - 1];

						lOShadow[liShadow] = lOShadow.TryGetValue(liShadow, out int liCount) ? liCount + 1 : 1;
						lbErase[((y + 1) * piWidth) + x - 1] = true;
					}
				}
			}

			int liShadowIndex = lOShadow.Count > 0 ? fMost(lOShadow) : liInk;

			// The shadow's colour touching ink inside the box is lettering too: the original's art has dark
			// pixels between glyphs that are no glyph's shadow ("TURN", OPTBTNS.GR 0x33) - left, the painting
			// over would copy them along the row.
			for (int y = pOBox.Y; y < pOBox.Y + pOBox.Height && y < piHeight && !pOBox.Dark; y++)
			{
				for (int x = pOBox.X; x < pOBox.X + pOBox.Width && x < piWidth; x++)
				{
					if (pyPixels[(y * piWidth) + x] == liShadowIndex && fTouchesInk(pyColours, piWidth, piHeight, x, y))
						lbErase[(y * piWidth) + x] = true;
				}
			}

			fPaintOver(pyPixels, lbErase, piWidth, piHeight, pOBox);

			// Erase only (Erase): nothing written.
			if (psText == null || pOFont == null)
				return;

			// The text anew.
			List<string> lsLines = Lines(psText, pOBox.Width, pOFont, pOBox.Upper, pOBox.Spacing);
			int[] laTops = new int[lsLines.Count];

			if (pOBox.Tops != null && pOBox.Tops.Length == lsLines.Count)
				Array.Copy(pOBox.Tops, laTops, laTops.Length);
			else
			{
				int liPitch = lsLines.Count * LinePitch - 1 <= pOBox.Height ? LinePitch : LineRows;
				int liBlock = (lsLines.Count * liPitch) - (liPitch - LineRows);
				int liTop = pOBox.Y + (int)Math.Ceiling((pOBox.Height - liBlock) / 2.0);

				for (int liLine = 0; liLine < laTops.Length; liLine++)
					laTops[liLine] = liTop + (liLine * liPitch);
			}

			for (int liPass = pOBox.Dark ? 1 : 0; liPass < 2; liPass++)
			{
				for (int liLine = 0; liLine < lsLines.Count; liLine++)
				{
					int liWidth = LineWidth(pOFont, lsLines[liLine], pOBox.Spacing);
					int liLeft = pOBox.Left ? pOBox.X
						: pOBox.Dark ? pOBox.X + ((pOBox.Width - liWidth) / 2)
						: pOBox.X + ((pOBox.Width - (liWidth + 1)) / 2) + 1;

					fDrawLine(pyPixels, piWidth, piHeight, pOFont, lsLines[liLine], liLeft, laTops[liLine], liPass == 0 ? liShadowIndex : liInk,
						liPass == 0, pOBox.Spacing);
				}
			}
		}

		/// <summary>A line of glyphs: the shadow pass one pixel left and one down, the ink pass in place.</summary>
		private static void fDrawLine(byte[] pyPixels, int piWidth, int piHeight, UWFont pOFont, string psLine, int piLeft, int piTop, int piIndex,
			bool pbShadow, int piSpacing)
		{
			int liX = piLeft;

			foreach (char lcChar in psLine)
			{
				if (lcChar == ' ')
				{
					liX += pOFont.SpaceWidth + piSpacing;
					continue;
				}

				if (!pOFont.TryGetGlyph(lcChar, out UWFont.Glyph lOGlyph) || lOGlyph.Pixels == null)
					continue;

				for (int y = 0; y < pOFont.Height; y++)
				{
					for (int x = 0; x < lOGlyph.Width; x++)
					{
						if (lOGlyph.Pixels[(y * lOGlyph.Width) + x] == 0)
							continue;

						int liPx = liX + x - (pbShadow ? 1 : 0);
						int liPy = piTop + y + (pbShadow ? 1 : 0);

						if (liPx >= 0 && liPy >= 0 && liPx < piWidth && liPy < piHeight)
							pyPixels[(liPy * piWidth) + liPx] = (byte)piIndex;
					}
				}

				liX += lOGlyph.Width + piSpacing;
			}
		}

		/// <summary>Every pixel to erase inside the box takes the nearest kept pixel of its row inside the box,
		/// else of the nearest row - never one of the frame around it (the button's black border: copied, it
		/// left a dark pixel before "SPIEL LADEN", per user's screenshot 2026-11-08), and the frame itself is
		/// never painted over (a shadow falling on it is the border's own black).</summary>
		private static void fPaintOver(byte[] pyPixels, bool[] pbErase, int piWidth, int piHeight, Box pOBox)
		{
			int liLeft = Math.Max(0, pOBox.X);
			int liRight = Math.Min(piWidth - 1, pOBox.X + pOBox.Width - 1);
			int liTop = Math.Max(0, pOBox.Y);
			int liBottom = Math.Min(piHeight - 1, pOBox.Y + pOBox.Height - 1);
			byte[] lySource = (byte[])pyPixels.Clone();

			for (int y = liTop; y <= liBottom; y++)
			{
				for (int x = liLeft; x <= liRight; x++)
				{
					if (!pbErase[(y * piWidth) + x])
						continue;

					int liFound = -1;

					for (int liRow = 0; liRow <= liBottom - liTop && liFound < 0; liRow++)
					{
						foreach (int liDy in liRow == 0 ? new[] { 0 } : new[] { -liRow, liRow })
						{
							int liY = y + liDy;

							if (liY < liTop || liY > liBottom)
								continue;

							for (int liD = 1; liD <= liRight - liLeft && liFound < 0; liD++)
							{
								foreach (int liX in new[] { x - liD, x + liD })
								{
									if (liX >= liLeft && liX <= liRight && !pbErase[(liY * piWidth) + liX])
									{
										liFound = lySource[(liY * piWidth) + liX];
										break;
									}
								}
							}

							if (liFound >= 0)
								break;
						}
					}

					if (liFound >= 0)
						pyPixels[(y * piWidth) + x] = (byte)liFound;
				}
			}
		}

		/// <summary>The lettering's ink: bright and nearly grey (measured: 184 to 252, a blue tint of 16 at
		/// most); the buttons' grain is brown or red.</summary>
		private static bool fIsBrightInk(UWColor32 pOColour)
		{
			int liMin = Math.Min(pOColour.R, Math.Min(pOColour.G, pOColour.B));
			int liMax = Math.Max(pOColour.R, Math.Max(pOColour.G, pOColour.B));

			return pOColour.A > 0 && liMin >= 170 && liMax - liMin <= 40;
		}

		private static bool fTouchesInk(UWColor32[] pyColours, int piWidth, int piHeight, int piX, int piY)
		{
			for (int liDy = -1; liDy <= 1; liDy++)
			{
				for (int liDx = -1; liDx <= 1; liDx++)
				{
					int liX = piX + liDx;
					int liY = piY + liDy;

					if ((liDx != 0 || liDy != 0) && liX >= 0 && liY >= 0 && liX < piWidth && liY < piHeight && fIsBrightInk(pyColours[(liY * piWidth) + liX]))
						return true;
				}
			}

			return false;
		}

		/// <summary>Dark ink: clearly darker than the box's middle brightness (below the box's DarkLimit of the
		/// median) - the black labels of the stats page on its brown, the map's words pressed into the
		/// parchment.</summary>
		private static Func<UWColor32, bool> fDarkInk(UWColor32[] pyColours, int piWidth, int piHeight, Box pOBox)
		{
			List<int> liLights = new List<int>();

			for (int y = pOBox.Y; y < pOBox.Y + pOBox.Height && y < piHeight; y++)
			{
				for (int x = pOBox.X; x < pOBox.X + pOBox.Width && x < piWidth; x++)
					liLights.Add(fLight(pyColours[(y * piWidth) + x]));
			}

			liLights.Sort();

			int liLimit = liLights.Count == 0 ? 0 : (int)(liLights[liLights.Count / 2] * pOBox.DarkLimit);

			return pOColour => pOColour.A > 0 && fLight(pOColour) < liLimit;
		}

		private static int fLight(UWColor32 pOColour)
		{
			return ((pOColour.R * 3) + (pOColour.G * 6) + pOColour.B) / 10;
		}

		private static int fMost(Dictionary<int, int> pOCounts)
		{
			int liBest = -1;
			int liBestCount = -1;

			foreach (KeyValuePair<int, int> lOPair in pOCounts)
			{
				if (lOPair.Value > liBestCount || (lOPair.Value == liBestCount && lOPair.Key < liBest))
				{
					liBest = lOPair.Key;
					liBestCount = lOPair.Value;
				}
			}

			return liBest;
		}

		// ------------------------------------------------- The first game's table

		/// <summary>A button picture (31 x 14 and the detail labels' 34 x 18): its box is the picture
		/// without its one-pixel border.</summary>
		private static Box[] fButton(int piWidth, int piHeight, string psId, params int[] paTops)
		{
			return new[] { new Box(1, 1, piWidth - 2, piHeight - 2, psId, paTops) };
		}

		/// <summary>A heading (OPTBTNS.GR 0x2E to 0x38): lettering on the stone without a button's border,
		/// its shadow reaching the picture's first column ("RESTORE GAME:", per user's screenshot 2026-11-08)
		/// - its box is the whole picture.</summary>
		private static Box[] fHeading(int piWidth, int piHeight, string psId, params int[] paTops)
		{
			return new[] { new Box(0, 0, piWidth, piHeight, psId, paTops) };
		}

		/// <summary>A slot of the options backgrounds (35 x 108): the button picture's box placed at the
		/// slot (UWClassicLayout.Options: the slots one pixel right of the panel, from row 2, 15 apart).</summary>
		private static Box fSlot(int piSlot, string psId, params int[] paTops)
		{
			return new Box(2, 3 + (piSlot * UWClassicLayout.Options.EntryPitch), 29, 12, psId, paTops);
		}

		/// <summary>The ids of the options panel's words (UWTextEnglish.Pictures).</summary>
		public const string SaveGame = "picture.options.save_game";

		public const string RestoreGame = "picture.options.restore_game";

		public const string Music = "picture.options.music";

		public const string Sound = "picture.options.sound";

		public const string Detail = "picture.options.detail";

		public const string ReturnToGame = "picture.options.return_to_game";

		public const string QuitGame = "picture.options.quit_game";

		private static Dictionary<(UWTexture.TextureTypes, int), Box[]> fFirstGameTable()
		{
			UWTexture.TextureTypes O = UWTexture.TextureTypes.OPTBTNS;
			Dictionary<(UWTexture.TextureTypes, int), Box[]> lOTable = new Dictionary<(UWTexture.TextureTypes, int), Box[]>();

			void fPair(int piIdle, string psId, params int[] paTops)
			{
				lOTable[(O, piIdle)] = fButton(31, 14, psId, paTops);
				lOTable[(O, piIdle + 1)] = fButton(31, 14, psId, paTops);
			}

			// The buttons, idle and lit (UWOptionsMenuModel's picture numbers).
			fPair(0x06, SaveGame);
			fPair(0x08, RestoreGame);
			fPair(0x0A, Music);
			fPair(0x0C, Sound);
			fPair(0x0E, Detail);
			fPair(0x10, ReturnToGame);
			fPair(0x12, QuitGame);
			fPair(0x14, "picture.options.on");
			fPair(0x16, "picture.options.off");
			fPair(0x18, "picture.options.cancel");
			fPair(0x1A, "picture.options.done");
			fPair(0x1C, "picture.options.done");
			fPair(0x26, "picture.options.low");
			fPair(0x28, "picture.options.medium");
			fPair(0x2A, "picture.options.high");
			fPair(0x2C, "picture.options.very_high");
			fPair(0x39, "picture.options.yes");
			fPair(0x3B, "picture.options.no");

			// The headings: one picture each.
			lOTable[(O, 0x2E)] = fHeading(31, 14, "picture.options.restore_title");
			lOTable[(O, 0x2F)] = fHeading(31, 14, "picture.options.music_is_on");
			lOTable[(O, 0x30)] = fHeading(31, 14, "picture.options.music_is_off");
			lOTable[(O, 0x31)] = fHeading(31, 14, "picture.options.sound_is_on");
			lOTable[(O, 0x32)] = fHeading(31, 14, "picture.options.sound_is_off");
			lOTable[(O, 0x33)] = fHeading(31, 14, "picture.options.turn_music", 1, 7);
			lOTable[(O, 0x34)] = fHeading(31, 14, "picture.options.turn_sound", 1, 7);
			lOTable[(O, 0x35)] = fHeading(34, 18, "picture.options.detail_is_low", 1, 6, 12);
			lOTable[(O, 0x36)] = fHeading(34, 18, "picture.options.detail_is_medium", 1, 6, 12);
			lOTable[(O, 0x37)] = fHeading(34, 18, "picture.options.detail_is_high", 1, 6, 12);
			lOTable[(O, 0x38)] = fHeading(34, 18, "picture.options.detail_is_very_high", 1, 6, 12);

			// The backgrounds: their painted buttons (covered by the button pictures in the game) and the
			// headings they show themselves.
			lOTable[(O, 0x01)] = new[]
			{
				fSlot(0, SaveGame), fSlot(1, RestoreGame), fSlot(2, Music), fSlot(3, Sound), fSlot(4, Detail), fSlot(5, ReturnToGame),
				fSlot(6, QuitGame)
			};
			lOTable[(O, 0x02)] = new[] { fSlot(0, "picture.options.save_title"), fSlot(5, "picture.options.cancel") };
			lOTable[(O, 0x03)] = new[]
			{
				fSlot(0, "picture.options.quit_title"), fSlot(1, "picture.options.are_you_sure", 19, 24), fSlot(2, "picture.options.yes"),
				fSlot(3, "picture.options.no")
			};
			lOTable[(O, 0x04)] = new[]
			{
				fSlot(0, "picture.options.music_is_on"), fSlot(1, "picture.options.turn_music", 18, 24), fSlot(2, "picture.options.on"),
				fSlot(3, "picture.options.off"), fSlot(4, "picture.options.done")
			};
			lOTable[(O, 0x05)] = new[]
			{
				new Box(1, 2, 32, 16, "picture.options.detail_is_low", 2, 7, 13), new Box(1, 19, 33, 13, "picture.options.set_new_level", 21, 27),
				fSlot(2, "picture.options.low"), fSlot(3, "picture.options.medium"), fSlot(4, "picture.options.high"),
				fSlot(5, "picture.options.very_high"), fSlot(6, "picture.options.done")
			};

			// THE STATS PAGE (PANELS.GR 2, 83 x 114): STR to EXP in FONT5X6I, black without a shadow, each from
			// column 7, seven rows apart from row 21 (measured 2026-11-08: the glyphs match the picture); the
			// values beside them are drawn by the game (UWHudPanel.fRefreshStats).
			string[] lsStats =
			{
				"picture.stats.str", "picture.stats.dex", "picture.stats.int", "picture.stats.vit", "picture.stats.mana", "picture.stats.exp"
			};
			Box[] laStats = new Box[lsStats.Length];

			for (int liRow = 0; liRow < lsStats.Length; liRow++)
			{
				laStats[liRow] = new Box(7, 20 + (liRow * 7), 34, 7, lsStats[liRow], 21 + (liRow * 7))
				{
					Font = UWFonts.FontType.Italic, Dark = true, Left = true
				};
			}

			lOTable[(UWTexture.TextureTypes.PANELS, 2)] = laStats;

			// THE MAP (BLNKMAP.BYT, 320 x 200): "Level" at the top in FONTCHAR from column 253 (the level's number
			// is drawn beside it by the game, UWHudMap, and moves right after a wider word - the box reaches to just
			// before the folded corner), ERASE under the eraser and CLOSE in its scroll in
			// FONT5X6I, pressed dark into the parchment, centred.
			lOTable[(UWTexture.TextureTypes.BLNKMAP, 0)] = new[]
			{
				new Box(253, 8, 40, 9, "picture.map.level", 8) { Font = UWFonts.FontType.CharacterGeneration, Dark = true, Left = true, Upper = false, Spacing = -1 },
				new Box(267, 138, 35, 7, "picture.map.erase", 139) { Font = UWFonts.FontType.Italic, Dark = true, DarkLimit = 0.85f },
				new Box(274, 160, 25, 7, "picture.map.close", 161) { Font = UWFonts.FontType.Italic, Dark = true, DarkLimit = 0.85f }
			};

			return lOTable;
		}
	}
}
