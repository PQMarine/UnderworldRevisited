using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	public class UWTexture
	{
		public enum TextureTypes
		{
			THRREDWIN,
			ANIMO,
			ARMOR_F,
			ARMOR_M,
			BODIES,
			BUTTONS,
			CHAINS,
			CHARHEAD,
			CHRBTNS,
			COMPASS,
			CONVERSE,
			CURSORS,
			DOORS,
			DRAGONS,
			EYES,
			FLASKS,
			GENHEAD,
			HEADS,
			INV,
			LFTI,
			OBJECTS,
			OPBTN,
			OPTB,
			OPTBTNS,
			PANELS,
			POWER,
			QUESTION,
			SCRLEDGE,
			SPELLS,
			TMFLAT,
			TMOBJ,
			VIEWS,
			WEAPONS,
			WALL,
			FLOOR,
			BLNKMAP,
			CHARGEN,
			CONV,
			MAIN,
			OPSCR,
			PRES1,
			PRES2,
			WIN1,
			WIN2
		}

		public enum UWImageFileFormatEnum
		{
			GR = 1,
			TR,
			CR,
			SR,
			AR
		}

		public enum UWImageFileEncodingEnum
		{
			Invalid = 0,
			EightBitUncompressed = 4,
			FourBitCompressed = 8,
			FourBitUncompressed = 10
		}

		public readonly UWPalettes Palettes;

		public byte[] PaletteIndices;

		public TextureTypes TextureType { get; set; }

		public int Index { get; set; }

		public string TextureFileName { get; set; }

		public UWImageFileFormatEnum ImageFileFormat { get; set; }

		public UWImageFileEncodingEnum ImageFileEncoding { get; set; }

		public int Width { get; set; }

		public int Height { get; set; }

		public int AuxPaletteIndex { get; set; }

		public int MainPaletteIndex { get; set; }

		public UWTexture(string psFileName, int piIndex, int piResolution, byte[] pyData, UWPalettes pOPalettes, TextureTypes peTextureType)
		{
			TextureFileName = psFileName;
			Palettes = pOPalettes;
			Index = piIndex;
			Width = piResolution;
			Height = piResolution;
			PaletteIndices = pyData;
			TextureType = peTextureType;
			AuxPaletteIndex = 255;
			ImageFileEncoding = UWImageFileEncodingEnum.EightBitUncompressed;
		}

		public UWTexture(string psFileName, byte[] pyPaletteIndices, TextureTypes peTextureType, UWPalettes pOPalettes, int piMainPaletteIndex)
		{
			TextureFileName = psFileName;
			PaletteIndices = pyPaletteIndices;
			TextureType = peTextureType;
			Palettes = pOPalettes;
			MainPaletteIndex = piMainPaletteIndex;
			AuxPaletteIndex = 255;
			ImageFileEncoding = UWImageFileEncodingEnum.EightBitUncompressed;
			Width = 320;
			Height = 200;
		}

		/// <summary>
		/// For images with a fixed size that is NOT stored in the file - PANELS.GR is the
		/// only such case: images of 83x114 pixels each plus a last one of 3x120 (the narrow
		/// side of the panel), raw and uncompressed one after another (see
		/// UWTextures.fImportPanels). uw-formats.txt considers the file faulty ("some invalid type,
		/// 0x0 and 1x1 resolution images") - it simply has no image header.
		/// </summary>
		public UWTexture(string psFileName, byte[] pyPaletteIndices, TextureTypes peTextureType, UWPalettes pOPalettes, int piMainPaletteIndex, int piWidth, int piHeight)
			: this(psFileName, pyPaletteIndices, peTextureType, pOPalettes, piMainPaletteIndex)
		{
			Width = piWidth;
			Height = piHeight;
		}

		public UWTexture(UWImageFileFormatEnum peImageFileFormat, ref byte[] pyImagefileData, int piIndexInFile, UWPalettes pOPalettes, int piMainPaletteIndex, TextureTypes peTextureType, int piTextureIndex, string psTextureFileName)
		{
			Palettes = pOPalettes;
			ImageFileFormat = peImageFileFormat;
			ImageFileEncoding = UWImageFileEncodingEnum.Invalid;
			TextureType = peTextureType;
			Index = piTextureIndex;
			TextureFileName = psTextureFileName;
			MainPaletteIndex = piMainPaletteIndex;
			int num = piIndexInFile;
			if (piIndexInFile >= pyImagefileData.Length)
			{
				return;
			}
			ImageFileEncoding = (UWImageFileEncodingEnum)pyImagefileData[num];
			if (ImageFileEncoding == UWImageFileEncodingEnum.EightBitUncompressed || ImageFileEncoding == UWImageFileEncodingEnum.FourBitCompressed || ImageFileEncoding != UWImageFileEncodingEnum.FourBitUncompressed)
			{
			}
			Width = pyImagefileData[num + 1];
			Height = pyImagefileData[num + 2];
			num += 3;
			byte auxPaletteIndex = byte.MaxValue;
			if (ImageFileEncoding != UWImageFileEncodingEnum.EightBitUncompressed)
			{
				auxPaletteIndex = pyImagefileData[num];
				num++;
			}
			AuxPaletteIndex = auxPaletteIndex;
			if (MainPaletteIndex != 0)
			{
				AuxPaletteIndex = 255;
			}
			int num2 = BitConverter.ToInt16(pyImagefileData, num);
			num += 2;
			if (ImageFileEncoding == UWImageFileEncodingEnum.EightBitUncompressed)
			{
				PaletteIndices = new byte[num2];
				Array.Copy(pyImagefileData, num, PaletteIndices, 0, num2);
			}
			else if (ImageFileEncoding == UWImageFileEncodingEnum.FourBitUncompressed)
			{
				if (Height != 15 || Width == 16)
				{
				}
				num2 = (int)Math.Ceiling((float)num2 / 2f);
				if (ImageFileEncoding == UWImageFileEncodingEnum.FourBitUncompressed && num2 != Width * Height)
				{
					num2 = ((Width * Height % 2 == 0) ? (Width * Height / 2) : (Width * Height + 0));
				}
				byte[] array = new byte[num2];
				Array.Copy(pyImagefileData, num, array, 0, num2);
				List<byte> list = new List<byte>();
				if (ImageFileEncoding != UWImageFileEncodingEnum.FourBitUncompressed)
				{
					return;
				}
				byte[] array2 = array;
				foreach (byte b in array2)
				{
					byte item = (byte)((b & 0xF0) >> 4);
					byte item2 = (byte)(b & 0xF);
					list.Add(item);
					list.Add(item2);
				}
				if (list.Count < Width * Height)
				{
					while (list.Count < Width * Height)
					{
						list.Add(0);
					}
				}
				PaletteIndices = list.ToArray();
			}
			else if (ImageFileEncoding == UWImageFileEncodingEnum.FourBitCompressed)
			{
				num2 = (int)Math.Ceiling((float)num2 / 2f);
				byte[] array = new byte[num2];
				Array.Copy(pyImagefileData, num, array, 0, num2);
				fDecompress(array, Width * Height);
			}
		}

		public UWTexture(UWTexture pOther)
		{
			TextureType = pOther.TextureType;
			Index = pOther.Index;
			TextureFileName = pOther.TextureFileName;
			ImageFileFormat = pOther.ImageFileFormat;
			ImageFileEncoding = pOther.ImageFileEncoding;
			Width = pOther.Width;
			Height = pOther.Height;
			AuxPaletteIndex = pOther.AuxPaletteIndex;
			MainPaletteIndex = pOther.MainPaletteIndex;
			Palettes = pOther.Palettes;
			PaletteIndices = new byte[pOther.PaletteIndices.Length];
			Array.Copy(pOther.PaletteIndices, PaletteIndices, pOther.PaletteIndices.Length);
		}

		public byte[] GetBGRA()
		{
			List<byte> list = new List<byte>();
			for (int i = 0; i < PaletteIndices.Length; i++)
			{
				list.AddRange(fGetPalette().GetBGRA(PaletteIndices[i]));
			}
			return list.ToArray();
		}

		public byte[] GetRGB()
		{
			List<byte> list = new List<byte>();
			for (int i = 0; i < PaletteIndices.Length; i++)
			{
				list.AddRange(fGetPalette().GetRGB(PaletteIndices[i]));
			}
			return list.ToArray();
		}

		public UWColor32[] GetUWColor32()
		{
			List<UWColor32> list = new List<UWColor32>();
			for (int i = 0; i < PaletteIndices.Length; i++)
			{
				list.Add(fGetPalette().GetUWColor(PaletteIndices[i]));
			}
			if (list.Count > Width * Height)
			{
				list.RemoveRange(Width * Height, list.Count - Width * Height);
			}
			return list.ToArray();
		}

		/// <summary>
		/// The pixels as indices of the MAIN palette.
		///
		/// PaletteIndices does not always contain main palette indices: object and
		/// critter sprites are 4-bit and go through an auxiliary palette, so they hold
		/// values from 0 to 15. Where not the COLOUR matters but the index itself - for
		/// palette rotation and the palette renderer - it has to be converted back first.
		///
		/// Returns null if that is not possible.
		/// </summary>
		public byte[] GetMainPaletteIndices()
		{
			if (PaletteIndices == null)
				return null;

			if (AuxPaletteIndex == 255)
				return PaletteIndices;

			if (Palettes == null)
				return null;

			UWAuxPalette lOAux = Palettes.GetAuxPalette(AuxPaletteIndex);

			if (lOAux == null)
				return null;

			byte[] lyMain = new byte[PaletteIndices.Length];

			for (int liAt = 0; liAt < PaletteIndices.Length; liAt++)
				lyMain[liAt] = lOAux.GetMainPaletteIndex(PaletteIndices[liAt]);

			return lyMain;
		}

		public void ReplaceIndices(int pStartIndex, byte[] pIndices)
		{
			Array.Copy(pIndices, 0, PaletteIndices, pStartIndex, pIndices.Length);
		}

		private IPalette fGetPalette()
		{
			if (AuxPaletteIndex == 255)
			{
				return Palettes.GetPalette(MainPaletteIndex);
			}
			return Palettes.GetAuxPalette(AuxPaletteIndex);
		}

		private void fDecompress(byte[] pyImageData, int piNumMaxPaletteIndices)
		{
			byte[] pyNibbles = new byte[pyImageData.Length * 2];
			for (int i = 0; i < pyImageData.Length; i++)
			{
				pyNibbles[i * 2] = (byte)((pyImageData[i] & 0xF0) >> 4);
				pyNibbles[i * 2 + 1] = (byte)(pyImageData[i] & 0xF);
			}
			bool flag = false;
			List<byte> list = new List<byte>();
			int piNibbleCursor = 0;
			int piCount = 0;
			int piCount2 = 0;
			while (list.Count < piNumMaxPaletteIndices && piNibbleCursor < pyNibbles.Length)
			{
				if (flag)
				{
					if (!fTryGetCount(ref pyNibbles, ref piNibbleCursor, out piCount))
					{
						break;
					}
					for (int j = 0; j < piCount; j++)
					{
						if (piNibbleCursor >= pyNibbles.Length)
						{
							break;
						}
						list.Add(pyNibbles[piNibbleCursor]);
						piNibbleCursor++;
					}
					flag = !flag;
					continue;
				}
				if (!fTryGetCount(ref pyNibbles, ref piNibbleCursor, out piCount))
				{
					break;
				}
				switch (piCount)
				{
				case 1:
					flag = true;
					continue;
				case 2:
				{
					if (!fTryGetCount(ref pyNibbles, ref piNibbleCursor, out piCount2))
					{
						break;
					}
					bool flag2 = false;
					for (int k = 0; k < piCount2; k++)
					{
						if (!fTryGetCount(ref pyNibbles, ref piNibbleCursor, out piCount))
						{
							break;
						}
						if (!fDoRepeat(ref piCount, ref pyNibbles, ref piNibbleCursor, list))
						{
							flag2 = true;
							break;
						}
					}
					if (flag2)
					{
						break;
					}
					flag = true;
					continue;
				}
				default:
					if (fDoRepeat(ref piCount, ref pyNibbles, ref piNibbleCursor, list))
					{
						flag = true;
						continue;
					}
					break;
				}
				break;
			}
			while (list.Count < piNumMaxPaletteIndices)
			{
				list.Add(0);
			}
			if (list.Count > piNumMaxPaletteIndices)
			{
				list.RemoveRange(piNumMaxPaletteIndices - 1, list.Count - piNumMaxPaletteIndices);
			}
			PaletteIndices = list.ToArray();
		}

		private bool fDoRepeat(ref int piCount, ref byte[] pyNibbles, ref int piNibbleCursor, List<byte> pOPaletteIndices)
		{
			if (piNibbleCursor >= pyNibbles.Length)
			{
				return false;
			}
			byte item = pyNibbles[piNibbleCursor];
			piNibbleCursor++;
			for (int i = 0; i < piCount; i++)
			{
				pOPaletteIndices.Add(item);
			}
			return true;
		}

		private bool fTryGetCount(ref byte[] pyNibbles, ref int piNibbleCursor, out int piCount)
		{
			piCount = 0;
			if (piNibbleCursor >= pyNibbles.Length)
			{
				return false;
			}
			piCount = pyNibbles[piNibbleCursor];
			piNibbleCursor++;
			if (piCount == 0)
			{
				if (piNibbleCursor >= pyNibbles.Length - 1)
				{
					return false;
				}
				piCount = (pyNibbles[piNibbleCursor] << 4) | pyNibbles[piNibbleCursor + 1];
				piNibbleCursor += 2;
				if (piCount == 0)
				{
					if (piNibbleCursor >= pyNibbles.Length - 2)
					{
						return false;
					}
					piCount = (((pyNibbles[piNibbleCursor] << 4) | pyNibbles[piNibbleCursor + 1]) << 4) | pyNibbles[piNibbleCursor + 2];
					piNibbleCursor += 3;
				}
			}
			return true;
		}
	}
}
