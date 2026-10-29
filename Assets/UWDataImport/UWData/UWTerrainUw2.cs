namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S TERRAIN.DAT (phase 1 step 2; UE FORMATS.md "TERRAIN.DAT", TEXTMAPS.C:121): a
	/// word per T64.TR texture, 256 of them. Bits 6-7 the class (TERR_CLASS, map.h:94): 1 water, 2
	/// lava, 3 ice; bits 3-5 on ice the slipperiness (7 not slippery), in water the current's
	/// direction plus one; bits 0-2 3 or 4 stairs for the automap, 5 a shaft to look down. The
	/// meanings of bits 0-5 are inferred from their use (UE). The first game's (walls from 0, floors
	/// from 0x200) is UWTerrain's.
	/// </summary>
	public sealed class UWTerrainUw2
	{
		public const int TextureCount = 256;

		public const int ClassWater = 1;

		public const int ClassLava = 2;

		public const int ClassIce = 3;

		private readonly int[] miWords = new int[TextureCount];

		/// <summary>TERRAIN.DAT's bytes; a short file reads 0 for the textures it lacks.</summary>
		public UWTerrainUw2(byte[] pyFile)
		{
			for (int liAt = 0; liAt < TextureCount && pyFile != null && (2 * liAt) + 1 < pyFile.Length; liAt++)
				miWords[liAt] = UWBinary.ReadU16(pyFile, 2 * liAt);
		}

		/// <summary>The word of a T64.TR texture; 0 for a number out of range.</summary>
		public int Word(int piTexture) => piTexture >= 0 && piTexture < TextureCount ? miWords[piTexture] : 0;

		/// <summary>The class: 0 none, ClassWater, ClassLava, ClassIce.</summary>
		public int Class(int piTexture) => (Word(piTexture) >> 6) & 3;

		/// <summary>Bits 3-5: on ice the slipperiness, in water the current's direction plus one.</summary>
		public int Flow(int piTexture) => (Word(piTexture) >> 3) & 7;

		/// <summary>Bits 0-2: 3 or 4 stairs, 5 a shaft.</summary>
		public int Mark(int piTexture) => Word(piTexture) & 7;
	}
}
