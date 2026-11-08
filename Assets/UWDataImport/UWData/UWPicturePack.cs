using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// What reads a language's pictures (Languages/&lt;code&gt;/pictures, UWPicturePack): the Unity layer decodes a
	/// PNG; none - English, a language without pictures - hands every picture out as before.
	/// </summary>
	public interface IUWPictureFiles
	{
		/// <summary>Whether the language has a picture of this name ("OPBTN_00.png").</summary>
		bool Has(string psFileName);

		/// <summary>The picture's pixels, rows top-down; false when it cannot be read.</summary>
		bool TryRead(string psFileName, out int piWidth, out int piHeight, out UWColor32[] pyPixels);
	}

	/// <summary>
	/// A LANGUAGE'S OWN PICTURES (Docs/PICTURE-WORDS-PLAN.md step 3, 2026-11-08): the main menu's captions in
	/// their decorative script, the congratulations at the end, or any picture with English painted into it
	/// that a translator paints anew - as PNGs in Languages/&lt;code&gt;/pictures, named as uwdump exports them:
	/// a .GR picture "OPBTN_00.png" (file and index in hex), a .BYT screen "WIN1.png". A picture of the
	/// language wins over the words written anew (UWPictureWords). It must have the original's size; its
	/// colours are taken to the picture's own palette, the nearest colour for each pixel (an unchanged pixel
	/// finds its own), a pixel with alpha below a half to the palette's transparent colour.
	/// </summary>
	public static class UWPicturePack
	{
		/// <summary>The folder of a language's pictures beside its texts: Languages/&lt;code&gt;/pictures.</summary>
		public const string FolderName = "pictures";

		/// <summary>The kinds read whole from a .BYT file (UWTextures' bitmap screens).</summary>
		private static readonly HashSet<UWTexture.TextureTypes> msScreens = new HashSet<UWTexture.TextureTypes>
		{
			UWTexture.TextureTypes.BLNKMAP, UWTexture.TextureTypes.CHARGEN, UWTexture.TextureTypes.CONV, UWTexture.TextureTypes.MAIN,
			UWTexture.TextureTypes.OPSCR, UWTexture.TextureTypes.PRES1, UWTexture.TextureTypes.PRES2, UWTexture.TextureTypes.WIN1,
			UWTexture.TextureTypes.WIN2
		};

		/// <summary>Whether a kind is a whole .BYT screen.</summary>
		public static bool IsScreen(UWTexture.TextureTypes peType)
		{
			return msScreens.Contains(peType);
		}

		/// <summary>The original file's name without its extension: the kind's name, but "3DWIN" for 3DWIN.GR.</summary>
		public static string Stem(UWTexture.TextureTypes peType)
		{
			return peType == UWTexture.TextureTypes.THRREDWIN ? "3DWIN" : peType.ToString();
		}

		/// <summary>The name of a picture in the language's folder: "WIN1.png", "OPBTN_00.png".</summary>
		public static string FileName(UWTexture.TextureTypes peType, int piIndex)
		{
			return IsScreen(peType) ? Stem(peType) + ".png" : Stem(peType) + "_" + piIndex.ToString("X2") + ".png";
		}

		/// <summary>The picture a file name stands for, or false for a name of no picture.</summary>
		public static bool TryParse(string psFileName, out UWTexture.TextureTypes peType, out int piIndex)
		{
			peType = default;
			piIndex = 0;

			if (string.IsNullOrEmpty(psFileName) || !psFileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
				return false;

			string lsStem = psFileName.Substring(0, psFileName.Length - 4).ToUpperInvariant();
			int liUnderscore = lsStem.LastIndexOf('_');

			if (liUnderscore > 0 && int.TryParse(lsStem.Substring(liUnderscore + 1), System.Globalization.NumberStyles.HexNumber, null, out piIndex))
				lsStem = lsStem.Substring(0, liUnderscore);
			else
				piIndex = -1;

			if (lsStem == "3DWIN")
				lsStem = UWTexture.TextureTypes.THRREDWIN.ToString();

			if (!Enum.TryParse(lsStem, false, out peType) || lsStem != peType.ToString())
				return false;

			if (IsScreen(peType))
			{
				if (piIndex >= 0)
					return false;

				piIndex = 0;

				return true;
			}

			return piIndex >= 0;
		}

		/// <summary>
		/// The pixels of a language's picture as indices of the original picture's palette: each colour the
		/// nearest of the palette (squared distance in R, G, B), a pixel with alpha below 128 the palette's
		/// first transparent colour. An unchanged pixel finds its own colour again - where two indices share a
		/// colour, the lower one, so the original's index is not always kept (the drawn picture is the same).
		/// </summary>
		public static byte[] ToIndices(UWTexture pOPicture, UWColor32[] pyPixels)
		{
			UWColor32[] lyPalette = pOPicture.PaletteColours();
			byte[] lyIndices = new byte[pyPixels.Length];
			Dictionary<int, byte> lONearest = new Dictionary<int, byte>();
			int liTransparent = -1;

			for (int liAt = 0; liAt < lyPalette.Length && liTransparent < 0; liAt++)
			{
				if (lyPalette[liAt].A == 0)
					liTransparent = liAt;
			}

			for (int liPixel = 0; liPixel < pyPixels.Length; liPixel++)
			{
				UWColor32 lOColour = pyPixels[liPixel];

				if (lOColour.A < 128 && liTransparent >= 0)
				{
					lyIndices[liPixel] = (byte)liTransparent;
					continue;
				}

				int liKey = (lOColour.R << 16) | (lOColour.G << 8) | lOColour.B;

				if (!lONearest.TryGetValue(liKey, out byte lyIndex))
				{
					int liBest = int.MaxValue;

					for (int liAt = 0; liAt < lyPalette.Length; liAt++)
					{
						if (lyPalette[liAt].A == 0)
							continue;

						int liR = lyPalette[liAt].R - lOColour.R;
						int liG = lyPalette[liAt].G - lOColour.G;
						int liB = lyPalette[liAt].B - lOColour.B;
						int liDistance = (liR * liR) + (liG * liG) + (liB * liB);

						if (liDistance < liBest)
						{
							liBest = liDistance;
							lyIndex = (byte)liAt;
						}
					}

					lONearest[liKey] = lyIndex;
				}

				lyIndices[liPixel] = lyIndex;
			}

			return lyIndices;
		}

		/// <summary>A PNG's size from its header (signature and IHDR), without decoding it - for the checks of
		/// a language's pictures outside the game (uwdump validate-tsv).</summary>
		public static bool TryReadPngSize(byte[] pyData, out int piWidth, out int piHeight)
		{
			piWidth = 0;
			piHeight = 0;

			byte[] lySignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

			if (pyData == null || pyData.Length < 24)
				return false;

			for (int liAt = 0; liAt < lySignature.Length; liAt++)
			{
				if (pyData[liAt] != lySignature[liAt])
					return false;
			}

			if (pyData[12] != 'I' || pyData[13] != 'H' || pyData[14] != 'D' || pyData[15] != 'R')
				return false;

			piWidth = (pyData[16] << 24) | (pyData[17] << 16) | (pyData[18] << 8) | pyData[19];
			piHeight = (pyData[20] << 24) | (pyData[21] << 16) | (pyData[22] << 8) | pyData[23];

			return piWidth > 0 && piHeight > 0;
		}
	}
}
