using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// ONE LEVEL BLOCK OF EITHER GAME, READ AS THE FORMAT HAS IT (UW2 plan phase 1 step 2, 2026-10-28):
	/// the tile words, the free and active lists, the magic word - the part both games share
	/// (UWLevArkLayout) - and the second game's animation overlays and timers after it (UWLevArkFile).
	/// The tile's fields as Underworld Exhumed's docs/FORMATS.md "The tile" gives them (map.h:44):
	/// word 0 bits 0-3 the type, 4-7 the floor height, 10-13 the floor texture (an index into the
	/// level's texture map), 14 no magic, 15 a door; word 2 bits 0-5 the wall texture, 6-15 the first
	/// object. The first game's world is built by UWLevel; this reader serves the second game's until
	/// phase 2 builds its levels, and the checks of both.
	///
	/// TIMERS (level.h:29): 64 words, counted up to the first 0 (Anim_Load, MAP.C:147).
	/// </summary>
	public sealed class UWLevelBlock
	{
		private readonly byte[] myBlock;

		private UWLevelBlock(byte[] pyBlock, UWLevArkFile pOFile)
		{
			myBlock = pyBlock;
			File = pOFile;
		}

		/// <summary>A decoded level block; null when it is shorter than its game's block.</summary>
		public static UWLevelBlock Read(byte[] pyBlock, UWLevArkFile pOFile)
		{
			return pyBlock == null || pyBlock.Length < pOFile.BlockSize ? null : new UWLevelBlock(pyBlock, pOFile);
		}

		public UWLevArkFile File { get; }

		public bool HasMagic => UWBinary.ReadU16(myBlock, UWLevArkLayout.MagicOffset) == UWLevArkLayout.Magic;

		/// <summary>The tile's two words as one, word 0 low (x + 64 y).</summary>
		public uint TileWord(int piTile) => UWBinary.Read32(myBlock, piTile * UWLevArkLayout.TileRecordSize);

		public int TileType(int piTile) => (int)(TileWord(piTile) & 0xF);

		public int FloorHeight(int piTile) => (int)((TileWord(piTile) >> 4) & 0xF);

		/// <summary>The floor texture: an index into the level's texture map.</summary>
		public int FloorTexture(int piTile) => (int)((TileWord(piTile) >> 10) & 0xF);

		public bool IsNoMagic(int piTile) => ((TileWord(piTile) >> 14) & 1) != 0;

		public bool HasDoor(int piTile) => ((TileWord(piTile) >> 15) & 1) != 0;

		/// <summary>The wall texture: an index into the level's texture map.</summary>
		public int WallTexture(int piTile) => (int)((TileWord(piTile) >> 16) & 0x3F);

		public int FirstObject(int piTile) => (int)(TileWord(piTile) >> UWLevArkLayout.TileFirstObjectShift);

		public List<int> MobileFreeList => UWLevArkCodec.ReadMobileFreeList(myBlock, 0);

		public List<int> StaticFreeList => UWLevArkCodec.ReadStaticFreeList(myBlock, 0);

		public List<int> ActiveMobiles => UWLevArkCodec.ReadActiveMobiles(myBlock, 0);

		/// <summary>The second game's animation overlays inside the block; null for the first game, whose
		/// are a block of their own.</summary>
		public UWAnimationOverlay Overlays => File.OverlayOffset < 0 ? null : UWAnimationOverlay.Load(myBlock, File.OverlayOffset, null);

		/// <summary>The second game's timers up to the first 0; empty for the first game.</summary>
		public List<int> Timers
		{
			get
			{
				List<int> lOTimers = new List<int>();

				for (int liAt = 0; File.TimerOffset >= 0 && liAt < File.TimerCount; liAt++)
				{
					int liTimer = UWBinary.ReadU16(myBlock, File.TimerOffset + (2 * liAt));

					if (liTimer == 0)
						break;

					lOTimers.Add(liTimer);
				}

				return lOTimers;
			}
		}
	}
}
