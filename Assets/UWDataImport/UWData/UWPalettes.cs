using System;
using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	public class UWPalettes
	{
		private readonly List<UWPalette> mOPalettes;

		private readonly List<UWAuxPalette> mOAuxPalettes;

		public UWPalettes(string psPaletteDirectory)
		{
			string path = $"{psPaletteDirectory}\\PALS.DAT";
			string path2 = $"{psPaletteDirectory}\\ALLPALS.DAT";
			byte[] array = File.ReadAllBytes(path);
			mOPalettes = new List<UWPalette>();
			for (int i = 0; i < array.Length; i += 768)
			{
				byte[] array2 = new byte[768];
				Array.Copy(array, i, array2, 0, 768);
				mOPalettes.Add(new UWPalette(array2));
			}
			array = File.ReadAllBytes(path2);
			mOAuxPalettes = new List<UWAuxPalette>();
			for (int j = 0; j < array.Length - 2; j += 16)
			{
				byte[] array3 = new byte[16];
				Array.Copy(array, j, array3, 0, 16);
				mOAuxPalettes.Add(new UWAuxPalette(array3, mOPalettes[0]));
			}
		}

		public byte[] GetMainPaletteBGRA(int piPaletteIndex, int piColorIndex)
		{
			return mOPalettes[piPaletteIndex].GetBGRA(piColorIndex);
		}

		public byte[] GetMainPaletteRGB(int piPaletteIndex, int piColorIndex)
		{
			return mOPalettes[piPaletteIndex].GetRGB(piColorIndex);
		}

		public UWPalette GetPalette(int piIndex)
		{
			return mOPalettes[piIndex];
		}

		public UWAuxPalette GetAuxPalette(int piIndex)
		{
			return mOAuxPalettes[piIndex];
		}

		public byte[] GetAuxPaletteBGRA(int piPaletteIndex, int piColorIndex)
		{
			return mOAuxPalettes[piPaletteIndex].GetBGRA(piColorIndex);
		}
	}
}
