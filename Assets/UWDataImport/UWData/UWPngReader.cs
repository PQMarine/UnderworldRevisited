using System;
using System.IO;
using System.IO.Compression;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A PNG READER WITHOUT AN ENGINE (Docs/WIDESCREEN-WEAPONS-PLAN.md step 2, 2026-11-09): the mods' pictures are
	/// read the same in the game, in the self-check and in uwdump (pack-strip, pack-check), and the reader also
	/// gives the grAb chunk, the shift GZDoom's sprites and SLADE carry in the file, which Unity's LoadImage drops.
	///
	/// Reads what painting programs write: grey, RGB, indexed, grey with alpha and RGBA; 1, 2, 4, 8 and 16 bits
	/// (16 is cut to its high byte); the transparency chunk tRNS; every row filter. Not read: interlaced files
	/// (Adam7) - the error says to save without interlacing. The pixels come out as RGBA, rows top-down.
	/// </summary>
	public static class UWPngReader
	{
		/// <summary>A picture read: its size, its pixels (rows top-down) and its grAb shift where it has one.</summary>
		public sealed class Image
		{
			public int Width;

			public int Height;

			public UWColor32[] Pixels;

			public bool HasGrab;

			public int GrabX;

			public int GrabY;
		}

		private static readonly byte[] msSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

		/// <summary>Largest side read: a weapon picture extended for a wide screen is a few hundred pixels.</summary>
		public const int MaxSide = 4096;

		/// <summary>The picture, or null with the reason in psError.</summary>
		public static Image Read(byte[] pyData, out string psError)
		{
			psError = null;

			try
			{
				return fRead(pyData, out psError);
			}
			catch (Exception lOError) when (lOError is InvalidDataException || lOError is IOException || lOError is IndexOutOfRangeException
				|| lOError is ArgumentException || lOError is OverflowException)
			{
				psError = "damaged (" + lOError.Message + ")";

				return null;
			}
		}

		private static Image fRead(byte[] pyData, out string psError)
		{
			psError = null;

			if (pyData == null || pyData.Length < 8 + 25)
			{
				psError = "not a PNG";

				return null;
			}

			for (int liAt = 0; liAt < msSignature.Length; liAt++)
			{
				if (pyData[liAt] != msSignature[liAt])
				{
					psError = "not a PNG";

					return null;
				}
			}

			int liWidth = 0, liHeight = 0, liDepth = 0, liColourType = -1, liInterlace = 0;
			byte[] lyPalette = null;
			byte[] lyTransparency = null;
			Image lOImage = new Image();
			MemoryStream lOCompressed = new MemoryStream();
			int liPos = 8;
			bool lbHeader = false;

			while (liPos + 8 <= pyData.Length)
			{
				int liLength = fInt(pyData, liPos);
				string lsType = System.Text.Encoding.ASCII.GetString(pyData, liPos + 4, 4);
				int liData = liPos + 8;

				if (liLength < 0 || liData + liLength > pyData.Length)
				{
					psError = "damaged (chunk " + lsType + " runs past the end)";

					return null;
				}

				switch (lsType)
				{
				case "IHDR":
					liWidth = fInt(pyData, liData);
					liHeight = fInt(pyData, liData + 4);
					liDepth = pyData[liData + 8];
					liColourType = pyData[liData + 9];
					liInterlace = pyData[liData + 12];
					lbHeader = true;
					break;
				case "PLTE":
					lyPalette = new byte[liLength];
					Array.Copy(pyData, liData, lyPalette, 0, liLength);
					break;
				case "tRNS":
					lyTransparency = new byte[liLength];
					Array.Copy(pyData, liData, lyTransparency, 0, liLength);
					break;
				case "grAb":
					if (liLength >= 8)
					{
						lOImage.HasGrab = true;
						lOImage.GrabX = fInt(pyData, liData);
						lOImage.GrabY = fInt(pyData, liData + 4);
					}

					break;
				case "IDAT":
					lOCompressed.Write(pyData, liData, liLength);
					break;
				}

				if (lsType == "IEND")
					break;

				liPos = liData + liLength + 4;
			}

			if (!lbHeader || liWidth <= 0 || liHeight <= 0 || liWidth > MaxSide || liHeight > MaxSide)
			{
				psError = lbHeader ? "size " + liWidth + " x " + liHeight + " not read (largest side " + MaxSide + ")" : "no header";

				return null;
			}

			if (liInterlace != 0)
			{
				psError = "interlaced - save it without interlacing";

				return null;
			}

			int liChannels;

			switch (liColourType)
			{
			case 0: liChannels = 1; break;
			case 2: liChannels = 3; break;
			case 3: liChannels = 1; break;
			case 4: liChannels = 2; break;
			case 6: liChannels = 4; break;
			default:
				psError = "colour type " + liColourType + " unknown";

				return null;
			}

			bool lbDepthOk = liColourType == 0 ? (liDepth == 1 || liDepth == 2 || liDepth == 4 || liDepth == 8 || liDepth == 16)
				: liColourType == 3 ? (liDepth == 1 || liDepth == 2 || liDepth == 4 || liDepth == 8)
				: (liDepth == 8 || liDepth == 16);

			if (!lbDepthOk)
			{
				psError = liDepth + " bits for colour type " + liColourType + " not allowed";

				return null;
			}

			if (liColourType == 3 && lyPalette == null)
			{
				psError = "indexed without a palette";

				return null;
			}

			int liBitsPerPixel = liChannels * liDepth;
			int liStride = ((liWidth * liBitsPerPixel) + 7) / 8;
			int liStep = Math.Max(1, liBitsPerPixel / 8);
			byte[] lyRaw = fInflate(lOCompressed.ToArray(), (liStride + 1) * liHeight);

			if (lyRaw == null)
			{
				psError = "damaged (the picture data is short)";

				return null;
			}

			byte[] lyRows = fUnfilter(lyRaw, liStride, liHeight, liStep, out psError);

			if (lyRows == null)
				return null;

			lOImage.Width = liWidth;
			lOImage.Height = liHeight;
			lOImage.Pixels = new UWColor32[liWidth * liHeight];

			for (int y = 0; y < liHeight; y++)
			{
				int liRow = y * liStride;

				for (int x = 0; x < liWidth; x++)
					lOImage.Pixels[(y * liWidth) + x] = fPixel(lyRows, liRow, x, liColourType, liDepth, liChannels, lyPalette, lyTransparency);
			}

			return lOImage;
		}

		private static UWColor32 fPixel(byte[] pyRows, int piRow, int x, int piColourType, int piDepth, int piChannels, byte[] pyPalette, byte[] pyTransparency)
		{
			if (piDepth < 8)
			{
				int liBit = x * piDepth;
				int liValue = (pyRows[piRow + (liBit >> 3)] >> (8 - piDepth - (liBit & 7))) & ((1 << piDepth) - 1);

				if (piColourType == 3)
					return fIndexed(liValue, pyPalette, pyTransparency);

				byte lyGrey = (byte)(liValue * 255 / ((1 << piDepth) - 1));
				bool lbClear = pyTransparency != null && pyTransparency.Length >= 2 && ((pyTransparency[0] << 8) | pyTransparency[1]) == liValue;

				return new UWColor32(lyGrey, lyGrey, lyGrey, lbClear ? (byte)0 : (byte)255);
			}

			int liBytes = piDepth / 8;
			int liAt = piRow + (x * piChannels * liBytes);

			// A sample: its high byte for the colour, its whole value for tRNS.
			int fSample(int piChannel) => pyRows[liAt + (piChannel * liBytes)];
			int fFull(int piChannel) => liBytes == 1 ? pyRows[liAt + piChannel] : (pyRows[liAt + (piChannel * 2)] << 8) | pyRows[liAt + (piChannel * 2) + 1];

			switch (piColourType)
			{
			case 3:
				return fIndexed(pyRows[liAt], pyPalette, pyTransparency);
			case 0:
			{
				bool lbClear = pyTransparency != null && pyTransparency.Length >= 2 && ((pyTransparency[0] << 8) | pyTransparency[1]) == fFull(0);
				byte lyGrey = (byte)fSample(0);

				return new UWColor32(lyGrey, lyGrey, lyGrey, lbClear ? (byte)0 : (byte)255);
			}
			case 2:
			{
				bool lbClear = pyTransparency != null && pyTransparency.Length >= 6
					&& ((pyTransparency[0] << 8) | pyTransparency[1]) == fFull(0)
					&& ((pyTransparency[2] << 8) | pyTransparency[3]) == fFull(1)
					&& ((pyTransparency[4] << 8) | pyTransparency[5]) == fFull(2);

				return new UWColor32((byte)fSample(0), (byte)fSample(1), (byte)fSample(2), lbClear ? (byte)0 : (byte)255);
			}
			case 4:
				return new UWColor32((byte)fSample(0), (byte)fSample(0), (byte)fSample(0), (byte)fSample(1));
			default:
				return new UWColor32((byte)fSample(0), (byte)fSample(1), (byte)fSample(2), (byte)fSample(3));
			}
		}

		private static UWColor32 fIndexed(int piIndex, byte[] pyPalette, byte[] pyTransparency)
		{
			if ((piIndex * 3) + 2 >= pyPalette.Length)
				return new UWColor32(0, 0, 0, 0);

			byte lyAlpha = pyTransparency != null && piIndex < pyTransparency.Length ? pyTransparency[piIndex] : (byte)255;

			return new UWColor32(pyPalette[piIndex * 3], pyPalette[(piIndex * 3) + 1], pyPalette[(piIndex * 3) + 2], lyAlpha);
		}

		/// <summary>The zlib stream's deflate data unpacked (its two header bytes skipped, the checksum not checked);
		/// null when shorter than piExpected.</summary>
		private static byte[] fInflate(byte[] pyZlib, int piExpected)
		{
			if (pyZlib.Length < 2)
				return null;

			byte[] lyOut = new byte[piExpected];
			int liRead = 0;

			using (MemoryStream lOIn = new MemoryStream(pyZlib, 2, pyZlib.Length - 2))
			using (DeflateStream lODeflate = new DeflateStream(lOIn, CompressionMode.Decompress))
			{
				while (liRead < piExpected)
				{
					int liGot = lODeflate.Read(lyOut, liRead, piExpected - liRead);

					if (liGot <= 0)
						break;

					liRead += liGot;
				}
			}

			return liRead == piExpected ? lyOut : null;
		}

		/// <summary>The rows without their filter bytes and filters undone (None, Sub, Up, Average, Paeth).</summary>
		private static byte[] fUnfilter(byte[] pyRaw, int piStride, int piHeight, int piStep, out string psError)
		{
			psError = null;

			byte[] lyRows = new byte[piStride * piHeight];

			for (int y = 0; y < piHeight; y++)
			{
				int liFilter = pyRaw[y * (piStride + 1)];
				int liIn = (y * (piStride + 1)) + 1;
				int liOut = y * piStride;
				int liUp = liOut - piStride;

				for (int x = 0; x < piStride; x++)
				{
					int liLeft = x >= piStep ? lyRows[liOut + x - piStep] : 0;
					int liAbove = y > 0 ? lyRows[liUp + x] : 0;
					int liCorner = y > 0 && x >= piStep ? lyRows[liUp + x - piStep] : 0;
					int liValue = pyRaw[liIn + x];

					switch (liFilter)
					{
					case 0: break;
					case 1: liValue += liLeft; break;
					case 2: liValue += liAbove; break;
					case 3: liValue += (liLeft + liAbove) >> 1; break;
					case 4: liValue += Paeth(liLeft, liAbove, liCorner); break;
					default:
						psError = "damaged (row filter " + liFilter + ")";

						return null;
					}

					lyRows[liOut + x] = (byte)liValue;
				}
			}

			return lyRows;
		}

		/// <summary>The PNG standard's Paeth predictor (also for the checks' writer).</summary>
		public static int Paeth(int a, int b, int c)
		{
			int p = a + b - c;
			int pa = Math.Abs(p - a);
			int pb = Math.Abs(p - b);
			int pc = Math.Abs(p - c);

			if (pa <= pb && pa <= pc)
				return a;

			return pb <= pc ? b : c;
		}

		private static int fInt(byte[] pyData, int piAt)
		{
			return (pyData[piAt] << 24) | (pyData[piAt + 1] << 16) | (pyData[piAt + 2] << 8) | pyData[piAt + 3];
		}
	}
}
