using System.Collections.Generic;

namespace UWDataImport.UWData
{
	public class UWTile
	{
		public enum TileTypeEnum
		{
			solid,
			open,
			diagonal_se,
			diagonal_sw,
			diagonal_ne,
			diagonal_nw,
			slope_n,
			slope_s,
			slope_e,
			slope_w
		}

		public const int NorthTileIndex = 0;

		public const int EastTileIndex = 1;

		public const int SouthTileIndex = 2;

		public const int WestTileIndex = 3;

		public TileTypeEnum TileType;

		/// <summary>
		/// One height step of the original, already in world units.
		///
		/// The tile carries its floor height in four bits, and we take it shifted four places
		/// to the left - so one step is sixteen. The same sixteen appears
		/// below at Slope and is the height a stair step has in the game.
		/// </summary>
		public const float HeightLevel = 16f;

		public ushort FloorHeight;

		public ushort CeilingHeight;

		public byte Slope;

		public ushort TextureWall;

		public ushort TextureFloor;

		public ushort TextureCeiling;

		public ushort FirstObjectIndex;

		public uint RawTileData;

		public int[] AdjacentTileIndices;

		public bool NoMagicAllowed;

		public bool HasDoor;

		public int NorthTile;

		public int EastTile;

		public int SouthTile;

		public int WestTile;

		public List<UWObject> ObjectsInTile { get; set; }

		public int X { get; set; }

		public int Z { get; set; }

		public UWTile(int piTileIndex, uint piData, ushort[] piTextureInfos)
		{
			ObjectsInTile = new List<UWObject>();
			AdjacentTileIndices = new int[4];
			NorthTile = (AdjacentTileIndices[0] = piTileIndex + 64);
			EastTile = (AdjacentTileIndices[1] = piTileIndex + 1);
			SouthTile = (AdjacentTileIndices[2] = piTileIndex - 64);
			WestTile = (AdjacentTileIndices[3] = piTileIndex - 1);
			X = piTileIndex % 64;
			Z = piTileIndex / 64;
			RawTileData = piData;
			TileType = (TileTypeEnum)(piData & 0xF);
			FloorHeight = (ushort)(piData & 0xF0);
			CeilingHeight = 128;
			if (TileType == TileTypeEnum.slope_e || TileType == TileTypeEnum.slope_n || TileType == TileTypeEnum.slope_s || TileType == TileTypeEnum.slope_w)
			{
				Slope = 16;
			}
			else
			{
				Slope = 0;
			}
			byte b = (byte)((piData & 0x3F0000) >> 16);
			byte b2 = (byte)((piData & 0x3C00) >> 10);
			bool flag = false;
			if (!flag)
			{
				if (b >= 48)
				{
					b = 0;
				}
				if (b2 >= 10)
				{
					b2 = 0;
				}
			}
			NoMagicAllowed = (byte)((piData & 0x4000) >> 14) == 1;
			HasDoor = (byte)((piData & 0x8000) >> 15) == 1;
			TextureWall = piTextureInfos[b];
			TextureFloor = piTextureInfos[b2 + ((!flag) ? 48 : 0)];
			TextureCeiling = piTextureInfos[flag ? 32 : 57];
			if (piData != 240)
			{
			}
			if (HasDoor)
			{
			}
			FirstObjectIndex = (ushort)((piData & 0xFFC00000u) >> 22);
		}

		/// <summary>
		/// Sets the floor texture via its slot in the texture table (0 to 9) - and also writes
		/// the slot into the tile word (bits 10-13), so that UWLevelWriter writes it.
		/// Setting only TextureFloor is not enough for that: the field holds the texture value, not
		/// the slot, and the maze sense changes it purely for display.
		/// </summary>
		public void SetFloorTexture(int piSlot, ushort[] piTextureInfos)
		{
			TextureFloor = piTextureInfos[piSlot + 48];
			RawTileData = (RawTileData & ~0x3C00u) | ((uint)(piSlot & 0xF) << 10);
		}

		/// <summary>Like SetFloorTexture, for the wall (slot 0 to 47, bits 16-21).</summary>
		public void SetWallTexture(int piIndex, ushort[] piTextureInfos)
		{
			TextureWall = piTextureInfos[piIndex];
			RawTileData = (RawTileData & ~0x3F0000u) | ((uint)(piIndex & 0x3F) << 16);
		}

		public void LoadObjects(List<UWObject> pOMasterList, UWTextures pOTextures)
		{
			UWObject uWObject = pOMasterList[FirstObjectIndex];
			uWObject.TileX = X;
			uWObject.TileY = Z;
			fCheckForSpecialProperty(uWObject, pOTextures);
			ObjectsInTile.Add(uWObject);
			do
			{
				if (uWObject != null)
				{
					uWObject = uWObject.GetNextObjectInLine(pOMasterList);
					if (uWObject != null)
					{
						uWObject.TileX = X;
						uWObject.TileY = Z;
						fCheckForSpecialProperty(uWObject, pOTextures);
						ObjectsInTile.Add(uWObject);
					}
				}
			}
			while (uWObject != null);
		}

		private void fCheckForSpecialProperty(UWObject pOUWObject, UWTextures pOTextures)
		{
			if (pOUWObject.ID == 327)
			{
				pOUWObject.Texture = pOTextures.GetTextureByType(UWTexture.TextureTypes.WALL, TextureWall);
			}
			if (pOUWObject.ID == 356 && pOUWObject.Flags != 2)
			{
			}
		}

		public override string ToString()
		{
			return TileType.ToString();
		}
	}
}
