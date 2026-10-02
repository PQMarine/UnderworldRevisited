using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Terrain properties of the wall and floor textures from "terrain.dat"
	/// (uw-formats.txt 4.9). One 16-bit word per texture, 256 wall textures from the start of the file and
	/// 256 floor textures from 0x200.
	///
	/// This is the file that marks water and lava as such - until now there was no way in the
	/// project to tell a water tile from a normal one.
	/// Along the way it also names windows, gratings, drains and stairs, which can be cross-checked
	/// against the texture names from string block 10.
	/// </summary>
	public class UWTerrain
	{
		public enum TerrainType
		{
			Normal = 0x0000,
			AnkhMural = 0x0002,
			StairsUp = 0x0003,
			StairsDown = 0x0004,
			Pipe = 0x0005,
			Grating = 0x0006,
			Drain = 0x0007,
			ChainedPrincess = 0x0008,
			Window = 0x0009,
			Tapestry = 0x000A,
			TexturedDoor = 0x000B,
			Water = 0x0010,
			Lava = 0x0020
		}

		private const int TextureCount = 256;

		private const int FloorOffset = 0x200;

		private readonly ushort[] myWall = new ushort[TextureCount];

		private readonly ushort[] myFloor = new ushort[TextureCount];

		public bool IsLoaded { get; private set; }

		public UWTerrain(string psDataPath)
		{
			string lsFile = Path.Combine(psDataPath, "TERRAIN.DAT");

			if (!File.Exists(lsFile))
				return;

			byte[] lyData = File.ReadAllBytes(lsFile);

			if (lyData.Length < FloorOffset + (TextureCount * 2))
				return;

			for (int liIndex = 0; liIndex < TextureCount; liIndex++)
			{
				myWall[liIndex] = (ushort)(lyData[liIndex * 2] | (lyData[(liIndex * 2) + 1] << 8));
				myFloor[liIndex] = (ushort)(lyData[FloorOffset + (liIndex * 2)]
					| (lyData[FloorOffset + (liIndex * 2) + 1] << 8));
			}

			IsLoaded = true;
		}

		public TerrainType GetWallTerrain(int piTextureIndex)
		{
			return piTextureIndex >= 0 && piTextureIndex < TextureCount
				? (TerrainType)myWall[piTextureIndex]
				: TerrainType.Normal;
		}

		public TerrainType GetFloorTerrain(int piTextureIndex)
		{
			return piTextureIndex >= 0 && piTextureIndex < TextureCount
				? (TerrainType)myFloor[piTextureIndex]
				: TerrainType.Normal;
		}

		/// <summary>Floor on which the player swims instead of walking.</summary>
		public bool IsWaterFloor(int piTextureIndex)
		{
			return GetFloorTerrain(piTextureIndex) == TerrainType.Water;
		}

		/// <summary>Floor that causes damage.</summary>
		public bool IsLavaFloor(int piTextureIndex)
		{
			return GetFloorTerrain(piTextureIndex) == TerrainType.Lava;
		}

		/// <summary>Window texture - the wall decoration 366/367 uses it to show the window
		/// onto the volcano's maw (see UWObjectMechanics.IsWindowTexture).</summary>
		public bool IsWindowWall(int piTextureIndex)
		{
			return GetWallTerrain(piTextureIndex) == TerrainType.Window;
		}
	}
}
