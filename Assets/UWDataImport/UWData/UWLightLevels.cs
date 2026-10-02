using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// LIGHT.DAT: 16 blocks of 256 bytes each, one per brightness level. Each block maps
	/// a palette index (palette 0) to the palette index that is actually to be shown
	/// at this brightness - block 0 is the original colours, block 15 is "almost
	/// black" (see UWFileInfo.txt 3.1.3). This presumably also resolves the small
	/// per-vertex values from the 3D model Gouraud nodes (00D4) - see
	/// UWObjectSpawner.fSpawn3DModel.
	/// </summary>
	public class UWLightLevels
	{
		public const int LevelCount = 16;
		private const int BlockSize = 256;

		private readonly byte[][] myLevels;

		public UWLightLevels(string psDataDirectory)
		{
			myLevels = new byte[LevelCount][];

			string lsPath = Path.Combine(psDataDirectory, "LIGHT.DAT");
			byte[] lyData = File.ReadAllBytes(lsPath);

			for (int i = 0; i < LevelCount; i++)
			{
				myLevels[i] = new byte[BlockSize];

				if ((i * BlockSize) + BlockSize <= lyData.Length)
					System.Array.Copy(lyData, i * BlockSize, myLevels[i], 0, BlockSize);
			}
		}

		/// <summary>Maps piPaletteIndex at brightness level piLevel (0=original colours,
		/// 15=almost black) to the palette index that is actually to be shown.</summary>
		public byte Remap(int piLevel, int piPaletteIndex)
		{
			int liLevel = piLevel < 0 ? 0 : (piLevel >= LevelCount ? LevelCount - 1 : piLevel);
			int liIndex = piPaletteIndex < 0 ? 0 : (piPaletteIndex >= BlockSize ? BlockSize - 1 : piPaletteIndex);

			return myLevels[liLevel][liIndex];
		}
	}
}
