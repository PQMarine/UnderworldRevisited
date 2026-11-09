using System;
using System.Collections.Generic;
using System.Globalization;

namespace UWDataImport.UWData
{
	/// <summary>A picture extended by a mod: the larger picture and where the original's top left lies in it.
	/// A view draws it at the original's place minus (Left, Top).</summary>
	public sealed class UWExtendedPicture
	{
		public UWTexture Picture;

		public int Left;

		public int Top;

		/// <summary>The mod whose pixels these are.</summary>
		public string ModId;

		/// <summary>Made from the other hand's picture, mirrored (UW1's left hand).</summary>
		public bool Mirrored;
	}

	/// <summary>
	/// COMPOSING AN EXTENDED PICTURE (Docs/WIDESCREEN-WEAPONS-PLAN.md 4.3, step 2, 2026-11-09): the pack's pixels
	/// around the original's, which come from the player's own files. Where both have a pixel the original's wins,
	/// so a pack holds only what the artist added and cannot change the original. It stays palette indices: the
	/// palette effects, the colour modes and UW2's skin colours act on the extension as on the original.
	/// </summary>
	public static class UWPictureExtension
	{
		/// <summary>The palette's first transparent index, -1 for none.</summary>
		public static int TransparentIndex(UWTexture pOPicture)
		{
			UWColor32[] lyPalette = pOPicture.PaletteColours();

			for (int liAt = 0; liAt < lyPalette.Length; liAt++)
			{
				if (lyPalette[liAt].A == 0)
					return liAt;
			}

			return -1;
		}

		/// <summary>
		/// The extended picture: the pack's picture (piWidth x piHeight, rows top-down, alpha below 128 is no
		/// pixel) with the original at (piLeft, piTop) in it. The two need not overlap; the result covers both.
		/// The pack's colours are taken to the original's palette (the nearest colour). Null with a reason where
		/// the palette has no transparent colour or the sizes do not match.
		/// </summary>
		public static UWExtendedPicture Compose(UWTexture pOOriginal, UWColor32[] pyPack, int piWidth, int piHeight, int piLeft, int piTop,
			out string psProblem)
		{
			psProblem = null;

			if (pOOriginal == null || pOOriginal.PaletteIndices == null)
			{
				psProblem = "no original picture";

				return null;
			}

			if (pyPack == null || piWidth <= 0 || piHeight <= 0 || pyPack.Length != piWidth * piHeight)
			{
				psProblem = "the picture's size does not match its pixels";

				return null;
			}

			int liClear = TransparentIndex(pOOriginal);

			if (liClear < 0)
			{
				psProblem = "the original's palette has no transparent colour";

				return null;
			}

			UWColor32[] lyPalette = pOOriginal.PaletteColours();
			byte[] lyPackIndices = UWPicturePack.ToIndices(pOOriginal, pyPack);
			int liOriginalWidth = pOOriginal.Width;
			int liOriginalHeight = pOOriginal.Height;
			int liMinX = Math.Min(0, piLeft);
			int liMinY = Math.Min(0, piTop);
			int liWidth = Math.Max(piWidth, piLeft + liOriginalWidth) - liMinX;
			int liHeight = Math.Max(piHeight, piTop + liOriginalHeight) - liMinY;
			byte[] lyCanvas = new byte[liWidth * liHeight];

			for (int liAt = 0; liAt < lyCanvas.Length; liAt++)
				lyCanvas[liAt] = (byte)liClear;

			for (int y = 0; y < piHeight; y++)
			{
				for (int x = 0; x < piWidth; x++)
				{
					int liPack = (y * piWidth) + x;

					if (pyPack[liPack].A >= 128)
						lyCanvas[((y - liMinY) * liWidth) + (x - liMinX)] = lyPackIndices[liPack];
				}
			}

			int liOriginalX = piLeft - liMinX;
			int liOriginalY = piTop - liMinY;

			for (int y = 0; y < liOriginalHeight; y++)
			{
				for (int x = 0; x < liOriginalWidth; x++)
				{
					int liAt = (y * liOriginalWidth) + x;

					if (liAt >= pOOriginal.PaletteIndices.Length)
						break;

					byte lyIndex = pOOriginal.PaletteIndices[liAt];

					if (lyIndex < lyPalette.Length && lyPalette[lyIndex].A != 0)
						lyCanvas[((y + liOriginalY) * liWidth) + x + liOriginalX] = lyIndex;
				}
			}

			UWTexture lOPicture = pOOriginal.WithPixels(lyCanvas);

			lOPicture.Width = liWidth;
			lOPicture.Height = liHeight;

			return new UWExtendedPicture { Picture = lOPicture, Left = liOriginalX, Top = liOriginalY };
		}

		/// <summary>
		/// The pack's picture mirrored left to right, with the original's place mirrored: the other hand's
		/// extension (UW1's left hand mirrors the right, Docs/WIDESCREEN-WEAPONS-PLAN.md 4.2). piOriginalWidth is
		/// the width of the original the shift belongs to.
		/// </summary>
		public static UWColor32[] Mirror(UWColor32[] pyPack, int piWidth, int piHeight, int piLeft, int piOriginalWidth, out int piMirroredLeft)
		{
			UWColor32[] lyMirrored = new UWColor32[pyPack.Length];

			for (int y = 0; y < piHeight; y++)
			{
				for (int x = 0; x < piWidth; x++)
					lyMirrored[(y * piWidth) + x] = pyPack[(y * piWidth) + (piWidth - 1 - x)];
			}

			piMirroredLeft = piWidth - piLeft - piOriginalWidth;

			return lyMirrored;
		}

		// ------------------------------------------------- For the artist (uwdump pack-strip, pack-check)

		/// <summary>
		/// WHERE THE ORIGINAL LIES IN A PAINTED PICTURE (pack-strip, decision 3: the artist paints on whole
		/// pictures): the place where the most of the original's pixels are found unchanged - each painted pixel
		/// taken to the palette's nearest colour, so a colour a program shifted a little still matches. A place
		/// with more than a twentieth of the original's pixels changed is no match. piChanged: the original's
		/// pixels painted over at the place found. False where the original is found nowhere.
		/// </summary>
		public static bool TryFindOriginal(UWTexture pOOriginal, UWColor32[] pyPainted, int piWidth, int piHeight,
			out int piLeft, out int piTop, out int piChanged)
		{
			piLeft = 0;
			piTop = 0;
			piChanged = 0;

			int liWidth = pOOriginal.Width, liHeight = pOOriginal.Height;

			if (pyPainted == null || liWidth > piWidth || liHeight > piHeight)
				return false;

			UWColor32[] lyPalette = pOOriginal.PaletteColours();
			byte[] lyPainted = UWPicturePack.ToIndices(pOOriginal, pyPainted);
			List<int> lOOpaque = new List<int>();

			for (int liAt = 0; liAt < liWidth * liHeight && liAt < pOOriginal.PaletteIndices.Length; liAt++)
			{
				if (lyPalette[pOOriginal.PaletteIndices[liAt]].A != 0)
					lOOpaque.Add(liAt);
			}

			if (lOOpaque.Count == 0)
				return false;

			int liAllowed = lOOpaque.Count / 20;
			int liBest = int.MaxValue;

			for (int liTop = 0; liTop <= piHeight - liHeight; liTop++)
			{
				for (int liLeft = 0; liLeft <= piWidth - liWidth; liLeft++)
				{
					int liMisses = 0;

					foreach (int liAt in lOOpaque)
					{
						int x = liAt % liWidth, y = liAt / liWidth;
						int liPainted = ((y + liTop) * piWidth) + x + liLeft;

						if (pyPainted[liPainted].A < 128 || !fSameColour(lyPalette[lyPainted[liPainted]], lyPalette[pOOriginal.PaletteIndices[liAt]]))
						{
							liMisses++;

							if (liMisses > liAllowed || liMisses >= liBest)
								break;
						}
					}

					if (liMisses <= liAllowed && liMisses < liBest)
					{
						liBest = liMisses;
						piLeft = liLeft;
						piTop = liTop;

						if (liBest == 0)
						{
							piChanged = 0;

							return true;
						}
					}
				}
			}

			piChanged = liBest == int.MaxValue ? 0 : liBest;

			return liBest != int.MaxValue;
		}

		/// <summary>
		/// THE NEW PIXELS ALONE (pack-strip): the painted picture without the pixels where the original has one,
		/// cut to what is left plus the original's place, so the pack holds only the artist's pixels. The
		/// original's place in the result is (piNewLeft, piNewTop). Null where nothing new was painted.
		/// </summary>
		public static UWColor32[] Strip(UWTexture pOOriginal, UWColor32[] pyPainted, int piWidth, int piHeight, int piLeft, int piTop,
			out int piNewWidth, out int piNewHeight, out int piNewLeft, out int piNewTop, out int piNewPixels)
		{
			UWColor32[] lyPalette = pOOriginal.PaletteColours();
			int liWidth = pOOriginal.Width, liHeight = pOOriginal.Height;
			int liMinX = piLeft, liMinY = piTop, liMaxX = piLeft + liWidth - 1, liMaxY = piTop + liHeight - 1;
			bool[] lbNew = new bool[pyPainted.Length];

			piNewPixels = 0;

			for (int y = 0; y < piHeight; y++)
			{
				for (int x = 0; x < piWidth; x++)
				{
					int liAt = (y * piWidth) + x;

					if (pyPainted[liAt].A < 128)
						continue;

					int liOx = x - piLeft, liOy = y - piTop;
					bool lbOriginal = liOx >= 0 && liOy >= 0 && liOx < liWidth && liOy < liHeight
						&& lyPalette[pOOriginal.PaletteIndices[(liOy * liWidth) + liOx]].A != 0;

					if (lbOriginal)
						continue;

					lbNew[liAt] = true;
					piNewPixels++;
					liMinX = Math.Min(liMinX, x);
					liMinY = Math.Min(liMinY, y);
					liMaxX = Math.Max(liMaxX, x);
					liMaxY = Math.Max(liMaxY, y);
				}
			}

			piNewWidth = liMaxX - liMinX + 1;
			piNewHeight = liMaxY - liMinY + 1;
			piNewLeft = piLeft - liMinX;
			piNewTop = piTop - liMinY;

			if (piNewPixels == 0)
				return null;

			UWColor32[] lyOut = new UWColor32[piNewWidth * piNewHeight];

			for (int y = 0; y < piNewHeight; y++)
			{
				for (int x = 0; x < piNewWidth; x++)
				{
					int liAt = ((y + liMinY) * piWidth) + x + liMinX;

					if (lbNew[liAt])
						lyOut[(y * piNewWidth) + x] = pyPainted[liAt];
				}
			}

			return lyOut;
		}

		/// <summary>The pack's opaque pixels whose colour is none of the palette's (pack-check): the game takes the
		/// nearest, which may not be what was painted.</summary>
		public static int CountOutsidePalette(UWTexture pOOriginal, UWColor32[] pyPack)
		{
			HashSet<int> lOColours = new HashSet<int>();

			foreach (UWColor32 lOColour in pOOriginal.PaletteColours())
			{
				if (lOColour.A != 0)
					lOColours.Add((lOColour.R << 16) | (lOColour.G << 8) | lOColour.B);
			}

			int liCount = 0;

			foreach (UWColor32 lOPixel in pyPack)
			{
				if (lOPixel.A >= 128 && !lOColours.Contains((lOPixel.R << 16) | (lOPixel.G << 8) | lOPixel.B))
					liCount++;
			}

			return liCount;
		}

		/// <summary>The pack's opaque pixels where the original has one (pack-check): the game keeps the original's.</summary>
		public static int CountOverlap(UWTexture pOOriginal, UWColor32[] pyPack, int piWidth, int piHeight, int piLeft, int piTop)
		{
			UWColor32[] lyPalette = pOOriginal.PaletteColours();
			int liCount = 0;

			for (int y = 0; y < pOOriginal.Height; y++)
			{
				for (int x = 0; x < pOOriginal.Width; x++)
				{
					int liPx = x + piLeft, liPy = y + piTop;

					if (liPx < 0 || liPy < 0 || liPx >= piWidth || liPy >= piHeight)
						continue;

					if (pyPack[(liPy * piWidth) + liPx].A >= 128 && lyPalette[pOOriginal.PaletteIndices[(y * pOOriginal.Width) + x]].A != 0)
						liCount++;
				}
			}

			return liCount;
		}

		/// <summary>The pixels with alpha neither 0 nor 255 (pack-check): the game takes below 128 as none, the rest as whole.</summary>
		public static int CountHalfClear(UWColor32[] pyPack)
		{
			int liCount = 0;

			foreach (UWColor32 lOPixel in pyPack)
			{
				if (lOPixel.A != 0 && lOPixel.A != 255)
					liCount++;
			}

			return liCount;
		}

		private static bool fSameColour(UWColor32 pOA, UWColor32 pOB)
		{
			return pOA.R == pOB.R && pOA.G == pOB.G && pOA.B == pOB.B;
		}

		// ------------------------------------------------- extend.json

		/// <summary>A folder's extend.json read: its default shift and the shifts of single pictures by index.</summary>
		public sealed class Shifts
		{
			public bool HasDefault;

			public int DefaultLeft;

			public int DefaultTop;

			public readonly Dictionary<int, (int Left, int Top)> Pictures = new Dictionary<int, (int Left, int Top)>();

			/// <summary>
			/// Where the original lies in a pack's picture: the picture's own entry here, then the PNG's grAb,
			/// then the file's default, then 0, 0 (decided 2026-11-08).
			/// </summary>
			public (int Left, int Top) For(int piIndex, UWPngReader.Image pOImage)
			{
				if (Pictures.TryGetValue(piIndex, out (int Left, int Top) lShift))
					return lShift;

				if (pOImage != null && pOImage.HasGrab)
					return (pOImage.GrabX, pOImage.GrabY);

				return HasDefault ? (DefaultLeft, DefaultTop) : (0, 0);
			}
		}

		/// <summary>No extend.json: every shift from the grAb or 0, 0.</summary>
		public static readonly Shifts NoShifts = new Shifts();

		/// <summary>An extend.json - { "default": { "left": 0, "top": 0 }, "pictures": { "012": { "left": 40, "top": 0 } } } -
		/// or null with the reason.</summary>
		public static Shifts ParseShifts(string psJson, out string psError)
		{
			Dictionary<string, object> lOObject = UWJsonReader.TryParseObject(psJson, out psError);

			if (lOObject == null)
				return null;

			Shifts lOShifts = new Shifts();
			Dictionary<string, object> lODefault = UWJsonReader.GetObject(lOObject, "default");

			if (lODefault != null)
			{
				lOShifts.HasDefault = true;
				lOShifts.DefaultLeft = (int)UWJsonReader.GetNumber(lODefault, "left", 0);
				lOShifts.DefaultTop = (int)UWJsonReader.GetNumber(lODefault, "top", 0);
			}

			Dictionary<string, object> lOPictures = UWJsonReader.GetObject(lOObject, "pictures");

			if (lOPictures != null)
			{
				foreach (KeyValuePair<string, object> lOPair in lOPictures)
				{
					if (!TryParseIndex(lOPair.Key, out int liIndex) || !(lOPair.Value is Dictionary<string, object> lOShift))
					{
						psError = "\"" + lOPair.Key + "\" is no picture number with left and top";

						return null;
					}

					lOShifts.Pictures[liIndex] = ((int)UWJsonReader.GetNumber(lOShift, "left", 0), (int)UWJsonReader.GetNumber(lOShift, "top", 0));
				}
			}

			return lOShifts;
		}

		/// <summary>A picture's number as the pack names it: hex, "012" or "12".</summary>
		public static bool TryParseIndex(string psName, out int piIndex)
		{
			piIndex = -1;

			return !string.IsNullOrEmpty(psName) && psName.Length <= 4
				&& int.TryParse(psName, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out piIndex) && piIndex >= 0;
		}
	}
}
