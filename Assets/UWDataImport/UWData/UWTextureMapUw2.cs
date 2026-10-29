namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S TEXTURE MAP (phase 1 step 2; UE FORMATS.md "The texture map", TEXTMAPS.C:64
	/// and :93): 64 words, texture numbers in T64.TR, for walls and floors alike; then three words of
	/// the six door textures, low byte first. The first game's (48 walls, 10 floors, the doors) is
	/// UWLevel's.
	/// </summary>
	public sealed class UWTextureMapUw2
	{
		public const int TextureCount = 64;

		public const int DoorCount = 6;

		public readonly int[] Textures = new int[TextureCount];

		public readonly int[] Doors = new int[DoorCount];

		/// <summary>A texture map block; null when it is shorter than 0x86.</summary>
		public static UWTextureMapUw2 Read(byte[] pyBlock)
		{
			if (pyBlock == null || pyBlock.Length < UWLevArkFile.Uw2TextureMapSize)
				return null;

			UWTextureMapUw2 lOMap = new UWTextureMapUw2();

			for (int liAt = 0; liAt < TextureCount; liAt++)
				lOMap.Textures[liAt] = UWBinary.ReadU16(pyBlock, 2 * liAt);

			for (int liAt = 0; liAt < DoorCount; liAt++)
				lOMap.Doors[liAt] = pyBlock[(2 * TextureCount) + liAt];

			return lOMap;
		}
	}
}
