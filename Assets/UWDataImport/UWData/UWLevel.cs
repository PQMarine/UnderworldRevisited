using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	public class UWLevel
	{
		public int LevelNumber;

		public UWTile[] TileData;

		/// <summary>The tile at a position, or null outside the 64 x 64 map or before the map is loaded.</summary>
		public UWTile GetTile(int piTileX, int piTileY)
		{
			if (TileData == null || piTileX < 0 || piTileY < 0
				|| piTileX >= UWWorldScale.TilesPerAxis || piTileY >= UWWorldScale.TilesPerAxis)
				return null;

			return TileData[(piTileY * UWWorldScale.TilesPerAxis) + piTileX];
		}

		private readonly uint miBlockOffset;

		private ushort[] miTextureInfos;

		private readonly UWTextures mOTextures;

		private List<UWObject> mOMasterlist;

		public List<UWObject> Masterlist => mOMasterlist;

		/// <summary>
		/// Texture table of the level. The 64 entries map the 6-bit indices from the
		/// tile data to global texture ids: 0-47 walls, 48-57 floors (index 57 is
		/// the ceiling), 58-63 extra entries. Needed to build one Texture2DArray per
		/// level with exactly these slices.
		/// </summary>
		public ushort[] TextureInfos => miTextureInfos;

		public UWLevel(int piLevelNumber)
		{
			LevelNumber = piLevelNumber;
		}

		/// <summary>The automap: one byte per tile, in the same order as the tile map.
		/// The lower nibble holds the tile type as in the map itself, the upper one a
		/// display kind (see MapDisplayClear and the constants after it).
		/// A type from FirstUndiscoveredType on means: not yet discovered.
		///
		/// Stored in LEV.ARK from block 27 on (DataImport.AutomapFirstBlock), 0x1000 bytes per
		/// level. Null if the file does not have that many blocks or the block is missing
		/// (see EnsureAutomap).</summary>
		public byte[] AutomapTiles { get; private set; }

		/// <summary>A label on the map. X and Y are SCREEN pixels in the 320x200 frame,
		/// not tiles - and Y counts from the BOTTOM (uw-formats.txt 4.8).
		/// Cross-checked against a save of the user: "JEWELD SWORD" at 104/162 sits at the
		/// top centre of level 4 in the original (2026-09-03).</summary>
		public struct MapNote
		{
			public string Text;

			public int X;

			public int Y;
		}

		public System.Collections.Generic.List<MapNote> MapNotes { get; private set; }

		/// <summary>Reads the labels. 54 bytes per record: text up to 0x31, then X and Y as
		/// two-byte numbers. Unused records are filled with padding bytes - reading stops at
		/// the first record whose text does not start with a printable character.</summary>
		public void LoadMapNotes(byte[] pyRawLevelData, uint piOffset)
		{
			MapNotes = ParseMapNotes(pyRawLevelData, piOffset, pyRawLevelData.Length);
		}

		/// <summary>Reads the labels of one block. Static because there are notes for
		/// ninety-nine levels, but only nine UWLevel objects.</summary>
		public static System.Collections.Generic.List<MapNote> ParseMapNotes(byte[] pyRawLevelData, uint piOffset, int piBlockEnd)
		{
			System.Collections.Generic.List<MapNote> lONotes =
				new System.Collections.Generic.List<MapNote>();

			if (piOffset == 0)
				return lONotes;

			const int liRecordSize = 54;

			// Only read up to the end of THIS block. The note blocks lie directly one after
			// another and are often just one record long - without the limit the reader
			// wandered from level 1 straight into the blocks of levels 10, 46 and 99 (per
			// user, 2026-09-03).
			int liMaxNotes = (piBlockEnd - (int)piOffset) / liRecordSize;

			for (int liNote = 0; liNote < liMaxNotes; liNote++)
			{
				int liAt = (int)piOffset + (liNote * liRecordSize);

				if (liAt + liRecordSize > pyRawLevelData.Length || liAt + liRecordSize > piBlockEnd)
					return lONotes;

				byte lyFirst = pyRawLevelData[liAt];

				if (lyFirst < 32 || lyFirst > 126)
					return lONotes;

				System.Text.StringBuilder lOText = new System.Text.StringBuilder();

				// The text field is 50 bytes long: 54 per record, minus two each for X and Y. Until
				// 2026-09-03 only 32 were read, longer notes would have been cut off.
				for (int liChar = 0; liChar < 50; liChar++)
				{
					byte lyChar = pyRawLevelData[liAt + liChar];

					if (lyChar == 0)
						break;

					lOText.Append((char)lyChar);
				}

				lONotes.Add(new MapNote
				{
					Text = lOText.ToString(),
					X = pyRawLevelData[liAt + 0x32] | (pyRawLevelData[liAt + 0x33] << 8),
					Y = pyRawLevelData[liAt + 0x34] | (pyRawLevelData[liAt + 0x35] << 8)
				});
			}

			return lONotes;
		}

				public void LoadAutomap(byte[] pyRawLevelData, uint piOffset)
		{
			if (piOffset == 0 || piOffset + 0x1000 > pyRawLevelData.Length)
				return;

			AutomapTiles = new byte[0x1000];
			Array.Copy(pyRawLevelData, piOffset, AutomapTiles, 0L, 0x1000);
		}

		/// <summary>
		/// An empty automap for a level that has none yet. DATA\LEV.ARK has no automap block
		/// for any level; the original creates it as soon as the level is entered
		/// (fresh save of the user: block 27 with 4096 bytes, 2026-09-11). Without this,
		/// walking recorded nothing for a new character, and no block was saved.
		/// </summary>
		public void EnsureAutomap()
		{
			if (AutomapTiles == null)
				AutomapTiles = new byte[0x1000];
		}

		/// <summary>
		/// How a discovered tile looks on the map - the upper nibble of the
		/// automap byte. Values for UW1; Underworld 2 swaps water and door.
		/// </summary>
		public const int MapDisplayClear = 0;

		public const int MapDisplayWater = 1;

		public const int MapDisplayLava = 2;

		public const int MapDisplayDoor = 4;

		public const int MapDisplayBridge = 9;

		public const int MapDisplayStair = 12;

		/// <summary>The terrain part of a display value (water 1, lava 2) and the marker part
		/// (door 4, bridge 8, stair 12) - bits 4-5 and 6-7 of the automap byte.</summary>
		public const int MapDisplayTerrainMask = 0x3;

		public const int MapDisplayMarkerMask = 0xC;

		/// <summary>The marker of a bridge, without the water under it (MapDisplayBridge has both).</summary>
		public const int MapDisplayBridgeMarker = 8;

		/// <summary>From this tile type on, the tile counts as undiscovered. Below it is the
		/// real type, from it on the undiscovered marker (see myUndiscoveredMarkers).</summary>
		public const int FirstUndiscoveredType = 0xA;

		/// <summary>Undiscovered marker per tile type (UW.EXE AutomapUndiscoveredTileTypes,
		/// dseg 5F7).</summary>
		private static readonly byte[] myUndiscoveredMarkers =
		{
			0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x0B, 0x0B, 0x0B, 0x0B, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F
		};

		/// <summary>
		/// The renderer has passed over this tile: an automap byte that is still 0 gets the
		/// undiscovered marker of its tile type (UW.EXE seg017_1FDD_DBC). Returns true exactly
		/// in that case - it is what UW.EXE counts for exploration experience. Tiles that
		/// already carry any value stay untouched.
		/// </summary>
		public bool MarkTileSeen(int piTileX, int piTileY)
		{
			if (AutomapTiles == null || TileData == null || piTileX < 0 || piTileY < 0 || piTileX >= 64 || piTileY >= 64)
				return false;

			int liAt = (piTileY * 64) + piTileX;

			if (AutomapTiles[liAt] != 0 || TileData[liAt] == null)
				return false;

			AutomapTiles[liAt] = myUndiscoveredMarkers[(int)TileData[liAt].TileType & 0xF];

			return true;
		}

		/// <summary>
		/// Records that the player has entered this tile.
		///
		/// The automap byte holds the tile type in the lower part and the display in the upper.
		/// Undiscovered means: the type is stored raised by ten - solid as 0xA, open as 0xB, the
		/// four diagonals as 0xC to 0xF. Revealing therefore simply means writing the real
		/// type.
		///
		/// WHO CALLS THIS, and it took until 2026-09-22 to get right: the tile one stands on
		/// with its eight neighbours (UWPlayerTerrain.fUpdateAutomap), AND every tile the
		/// render band really sees (UWExplorationRules.EvaluateRenderBand). The second one was
		/// missing, so the map only ever grew by a tile at a time while the original reveals as
		/// far as the light lets one see. The eight neighbours stay because our cone starts at
		/// the player's own row and cannot hold what lies beside and behind him.
		///
		/// WHAT LED US ASTRAY: an automap byte carries the TILE TYPE, and an original save read
		/// on 2026-09-03 and again on 2026-09-13 was taken for a record of where the player had
		/// been. On 2026-09-22 the same column was held against the level's tile types and they
		/// matched one for one - the 07 next to an 01 is a slope next to an open tile, not a
		/// revealed tile next to an unrevealed one. In all three original saves every open tile
		/// of levels 1 to 8 carries its type and level 9, the Void, is all zero.
		///
		/// Walls stay undiscovered: in the same save only seven of
		/// four thousand tiles carry a solid DIScovered type. The map draws their outlines
		/// from the open neighbour tile anyway.
		///
		/// A DISCOVERED TILE IS WRITTEN AGAIN, as seg017_1FDD_DBC does every time it draws a cell
		/// bright enough: the tile type, the terrain of its floor texture (bits 4-5, water 1, lava
		/// 2) and the marker of what is drawn on it (bits 6-7, set by the object renderer through
		/// dseg_54F: 1 door, 2 bridge, 3 stair). So a change terrain trap shows on the map once the
		/// tile is seen again (per user, 2026-10-01: the lowered water basin on level 3 stayed water
		/// on our map; until then a tile was written once and never again). piDisplayType carries
		/// both parts the way the byte's upper nibble holds them (MapDisplay...). ONE DEVIATION:
		/// where our host recognises no marker, an existing one is kept - our marker detection
		/// does not cover every object the original's renderer marks (above all stairs in a save
		/// the original wrote), and a re-seen tile must not lose it.
		/// </summary>
		/// <returns>True if the byte has changed.</returns>
		public bool MarkTileVisited(int piTileX, int piTileY, int piDisplayType)
		{
			if (AutomapTiles == null || TileData == null)
				return false;

			if (piTileX < 0 || piTileY < 0 || piTileX >= 64 || piTileY >= 64)
				return false;

			int liAt = (piTileY * 64) + piTileX;

			UWTile lOTile = TileData[liAt];

			if (lOTile == null)
				return false;

			// AN EMPTY BYTE COUNTS AS UNDISCOVERED. Read literally, zero would mean
			// "solid, display empty" - i.e. discovered, and therefore untouchable. But that
			// cannot be a discovered tile: only what you stand on gets discovered, and nobody
			// stands on solid rock. Such a tile is not drawn anyway
			// (UWAutomapPainter paints types 1 to 9 only), so it looks undiscovered.
			//
			// Noticed on level 8 from SAVE3 (per user, 2026-09-09): half the map there is
			// zero, and walking through it revealed nothing. Measured:
			// bytes 0 to 1632 - everything south of row 25 - are zero, from there on is
			// a normal map with proper undiscovered markers. In SAVE2, where the original wrote
			// the same level, there is NOT A SINGLE zero byte;
			// on level 1 there are seven in each save. Where the gap in this
			// file comes from can no longer be read from the files.
			byte lyOld = AutomapTiles[liAt];
			bool lbDiscovered = lyOld != 0 && (lyOld & 0xF) < FirstUndiscoveredType;
			int liDisplay = piDisplayType & 0xF;

			if (lbDiscovered && (liDisplay & MapDisplayMarkerMask) == 0)
				liDisplay |= (lyOld >> 4) & MapDisplayMarkerMask;

			byte lyNew = (byte)((liDisplay << 4) | ((int)lOTile.TileType & 0xF));

			if (AutomapTiles[liAt] == lyNew)
				return false;

			AutomapTiles[liAt] = lyNew;

			return true;
		}

		public UWLevel(int piLevelNumber, byte[] pyRawLevelData, uint piBlockOffset, uint piTextureUsageOffset, UWTextures pOTextures)
			: this(piLevelNumber)
		{
			mOTextures = pOTextures;
			miBlockOffset = piBlockOffset;
			byte[] array = new byte[122];
			Array.Copy(pyRawLevelData, piTextureUsageOffset, array, 0L, 122L);
			fLoadTextureInfo(array);
			array = new byte[16384];
			Array.Copy(pyRawLevelData, miBlockOffset, array, 0L, 16384L);
			fLoadTileMap(array);
			array = new byte[13200];
			Array.Copy(pyRawLevelData, miBlockOffset + 16384, array, 0L, 13200L);
			fLoadObjectLists(array);
			fLoadFreeMobileSlots(pyRawLevelData);
			fLoadFreeStaticSlots(pyRawLevelData);
			fLoadActiveMobiles(pyRawLevelData);
		}

		/// <summary>Block offsets of the list of the allocated mobile objects and its count
		/// (see UWLevelWriter).</summary>
		private const int ActiveMobileListOffset = 0x7AFC;

		private const int ActiveMobileCountOffset = 0x7C00;

		private List<int> mOActiveMobiles;

		/// <summary>
		/// THE LIST OF THE ALLOCATED MOBILE OBJECTS, in the original's order: GetFreeObject
		/// appends a new mobile (seg027_2861_A70), freeing puts the last entry into its place
		/// (seg027_2861_A83), and the level file keeps the list at 0x7AFC. Routines that look
		/// for a creature by its whoami walk it in this order and stop at the first match
		/// (see RunFunctionOnWhoAmIList_seg038_3307_D4E) - per user, 2026-09-29: Tyball's death took
		/// the 209 guard #222 before #223 and #200, exactly the order of this list.
		/// </summary>
		private void fLoadActiveMobiles(byte[] pyRawLevelData)
		{
			mOActiveMobiles = new List<int>();

			long liCountAt = miBlockOffset + ActiveMobileCountOffset;

			if (liCountAt + 2 > pyRawLevelData.Length)
				return;

			int liCount = pyRawLevelData[liCountAt] | (pyRawLevelData[liCountAt + 1] << 8);

			for (int liAt = 0; liAt < liCount && ActiveMobileListOffset + liAt < ActiveMobileCountOffset; liAt++)
				mOActiveMobiles.Add(pyRawLevelData[miBlockOffset + ActiveMobileListOffset + liAt]);
		}

		/// <summary>The place of a mobile slot in the list of the allocated mobiles, or
		/// int.MaxValue when it is not in it.</summary>
		public int GetActiveMobileOrder(int piMobileIndex)
		{
			int liAt = mOActiveMobiles != null ? mOActiveMobiles.IndexOf(piMobileIndex) : -1;

			return liAt < 0 ? int.MaxValue : liAt;
		}

		/// <summary>Block offsets of the mobile free list and its top entry (see UWLevelWriter).</summary>
		private const int MobileFreeListOffset = 0x7300;

		private const int MobileFreeTopOffset = 0x7C02;

		/// <summary>The first slots are the null object and the player.</summary>
		private const int FirstFreeMobileSlot = 2;

		private const int MobileSlotCount = 256;

		private List<int> mOFreeMobileSlots;

		private void fLoadFreeMobileSlots(byte[] pyRawLevelData)
		{
			mOFreeMobileSlots = new List<int>();

			long liTopAt = miBlockOffset + MobileFreeTopOffset;

			if (liTopAt + 2 > pyRawLevelData.Length)
				return;

			int liTop = (short)(pyRawLevelData[liTopAt] | (pyRawLevelData[liTopAt + 1] << 8));

			for (int liAt = 0; liAt <= liTop && liAt < MobileSlotCount; liAt++)
			{
				long liEntry = miBlockOffset + MobileFreeListOffset + (liAt * 2);

				mOFreeMobileSlots.Add(pyRawLevelData[liEntry] | (pyRawLevelData[liEntry + 1] << 8));
			}
		}

		/// <summary>
		/// A CREATURE THAT COMES INTO BEING GETS ITS SLOT AT ONCE (2026-09-27): the original's
		/// GetFreeObject takes the top of the level's free list when a trap or a spell creates a
		/// creature, and from then on it has an index below 256 - the mind's target and attacker
		/// bytes, the kin alarm and the conversations all use it. Ours spawned such creatures
		/// outside the Masterlist, so their index was 0, "nobody": a creature a trap had made could
		/// not raise the kin alarm (per user: a summoned ally stayed mellow next to a trap's reaper
		/// the player hit), and a summoned one reported its own blows as nobody's. The object takes
		/// the slot in the Masterlist; UWLevelWriter keeps that slot on saving. False when the free
		/// list is used up.
		/// </summary>
		/// <summary>
		/// A creature leaves the level for good (death, a delete trap, culling): its slot goes
		/// back onto the top of the free list, as AddToFreeObjectsList_seg027_4DD does, so the
		/// next creature made at runtime takes it. The Masterlist keeps the old object until
		/// the slot is handed out again; UWLevelWriter frees it on saving either way.
		/// </summary>
		public void ReleaseMobileSlot(UWObject pOObject)
		{
			if (pOObject == null || mOFreeMobileSlots == null || mOMasterlist == null)
				return;

			int liIndex = mOMasterlist.IndexOf(pOObject);

			if (liIndex < FirstFreeMobileSlot || liIndex >= MobileSlotCount || mOFreeMobileSlots.Contains(liIndex))
				return;

			mOFreeMobileSlots.Add(liIndex);

			// Out of the allocated list: the last entry takes its place (seg027_2861_A83).
			int liActive = mOActiveMobiles != null ? mOActiveMobiles.IndexOf(liIndex) : -1;

			if (liActive >= 0)
			{
				mOActiveMobiles[liActive] = mOActiveMobiles[mOActiveMobiles.Count - 1];
				mOActiveMobiles.RemoveAt(mOActiveMobiles.Count - 1);
			}
		}

		/// <summary>Block offsets of the static free list and its top entry (see UWLevelWriter).</summary>
		private const int StaticFreeListOffset = 0x74FC;

		private const int StaticFreeTopOffset = 0x7C04;

		/// <summary>Static slots are numbered 256 to 1023.</summary>
		private const int StaticSlotCount = 768;

		private List<int> mOFreeStaticSlots;

		private void fLoadFreeStaticSlots(byte[] pyRawLevelData)
		{
			mOFreeStaticSlots = new List<int>();

			long liTopAt = miBlockOffset + StaticFreeTopOffset;

			if (liTopAt + 2 > pyRawLevelData.Length)
				return;

			int liTop = (short)(pyRawLevelData[liTopAt] | (pyRawLevelData[liTopAt + 1] << 8));

			for (int liAt = 0; liAt <= liTop && liAt < StaticSlotCount; liAt++)
			{
				long liEntry = miBlockOffset + StaticFreeListOffset + (liAt * 2);

				mOFreeStaticSlots.Add(pyRawLevelData[liEntry] | (pyRawLevelData[liEntry + 1] << 8));
			}
		}

		/// <summary>
		/// GetFreeObject for a static object (see UWObjectLimitRules): the top of the level's
		/// static free list, or 0 when it is empty (the original culls then). Only the carried
		/// objects take their numbers from here - see UWCarriedSlots.
		/// </summary>
		public int TakeStaticSlot()
		{
			if (mOFreeStaticSlots == null || mOFreeStaticSlots.Count == 0)
				return 0;

			int liIndex = mOFreeStaticSlots[mOFreeStaticSlots.Count - 1];

			mOFreeStaticSlots.RemoveAt(mOFreeStaticSlots.Count - 1);

			return liIndex;
		}

		/// <summary>Whether a static slot is on the free list (for the self-check).</summary>
		public bool IsStaticSlotFree(int piIndex)
		{
			return mOFreeStaticSlots != null && mOFreeStaticSlots.Contains(piIndex);
		}

		/// <summary>AddToFreeObjectsList_seg027_4DD for a static slot: back onto the top.</summary>
		public void ReturnStaticSlot(int piIndex)
		{
			if (mOFreeStaticSlots == null || piIndex < MobileSlotCount || piIndex >= MobileSlotCount + StaticSlotCount
				|| mOFreeStaticSlots.Contains(piIndex))
				return;

			mOFreeStaticSlots.Add(piIndex);
		}

		public bool TryPlaceInFreeMobileSlot(UWObject pOObject, out int piIndex)
		{
			piIndex = 0;

			if (pOObject == null || mOFreeMobileSlots == null || mOMasterlist == null)
				return false;

			while (mOFreeMobileSlots.Count > 0)
			{
				int liIndex = mOFreeMobileSlots[mOFreeMobileSlots.Count - 1];

				mOFreeMobileSlots.RemoveAt(mOFreeMobileSlots.Count - 1);

				if (liIndex < FirstFreeMobileSlot || liIndex >= MobileSlotCount || liIndex >= mOMasterlist.Count)
					continue;

				mOMasterlist[liIndex] = pOObject;
				piIndex = liIndex;

				// Appended to the allocated list (seg027_2861_A70).
				if (mOActiveMobiles != null && !mOActiveMobiles.Contains(liIndex))
					mOActiveMobiles.Add(liIndex);

				return true;
			}

			return false;
		}

		private void fLoadTextureInfo(byte[] pyRawTextureInfoData)
		{
			miTextureInfos = new ushort[64];
			for (int i = 0; i < 96; i += 2)
			{
				byte[] array = new byte[2];
				Array.Copy(pyRawTextureInfoData, i, array, 0, 2);
				miTextureInfos[i / 2] = BitConverter.ToUInt16(array, 0);
			}
			for (int i = 96; i < 116; i += 2)
			{
				byte[] array = new byte[2];
				Array.Copy(pyRawTextureInfoData, i, array, 0, 2);
				miTextureInfos[48 + (i - 96) / 2] = BitConverter.ToUInt16(array, 0);
			}
			Array.Copy(pyRawTextureInfoData, 116, miTextureInfos, 58, 6);
		}

		private void fLoadTileMap(byte[] pyTileMapData)
		{
			TileData = new UWTile[4096];
			for (int i = 0; i < 4096; i++)
			{
				if (i == 158)
				{
				}
				TileData[i] = new UWTile(i, BitConverter.ToUInt32(pyTileMapData, i * 4), miTextureInfos);
			}
		}

		private void fLoadObjectLists(byte[] pyObjectListData)
		{
			load_object_list(pyObjectListData);
		}

		public uint[] GetDebugTileData()
		{
			uint[] array = new uint[4096];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = (uint)TileData[i].TileType;
			}
			return array;
		}

		private void load_object_list(byte[] pyObjectListData)
		{
			mOMasterlist = new List<UWObject>();
			int num = 0;
			for (int i = 0; i < 1024; i++)
			{
				ushort num2 = BitConverter.ToUInt16(pyObjectListData, num);
				ushort num3 = BitConverter.ToUInt16(pyObjectListData, num + 2);
				ushort num4 = BitConverter.ToUInt16(pyObjectListData, num + 4);
				ushort num5 = BitConverter.ToUInt16(pyObjectListData, num + 6);
				num += 8;
				ushort piItem_id = (ushort)(num2 & 0x1FF);
				UWObject uWObject = ((i < 256) ? new UWNpc(piItem_id) : new UWObject(piItem_id));
				uWObject.Flags = (ushort)((num2 & 0x1F00) >> 9);
				uWObject.IsEnchanted = (ushort)((num2 & 0x1000) >> 12) != 0;
				uWObject.DoorDirection = (ushort)((num2 & 0x2000) >> 13) != 0;
				uWObject.IsHidden = (ushort)((num2 & 0x4000) >> 14) != 0;
				uWObject.HasQuantity = (ushort)((num2 & 0x8000) >> 15) != 0;
				uWObject.ZPos = (ushort)(num3 & 0x7F);
				uWObject.Heading = (ushort)((num3 & 0x380) >> 7);
				uWObject.YPos = (ushort)((num3 & 0x1C00) >> 10);
				uWObject.XPos = (ushort)((num3 & 0xE000) >> 13);
				uWObject.Quality = (ushort)(num4 & 0x3F);
				uWObject.Link = (ushort)((num4 & 0xFFC0) >> 6);
				uWObject.Owner = (ushort)(num5 & 0x3F);
				uWObject.Quantity = (ushort)((num5 & 0xFFC0) >> 6);
				if (uWObject.ID <= 460)
				{
					uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, uWObject.ID);
				}
				if (i == 992)
				{
				}
				// Pillar (352, 0x0160): otherwise falls back to the generic object icon. In the
				// original it shows a texture that differs per object instance
				// (confirmed by screenshot comparison). Originally implemented via Owner->wall texture
				// (as for the wall decoration 366/367) - according to uw-formats.txt
				// (vividos/UnderworldAdventures), however, it is "the lower byte of the flags field"
				// from tmobj.gr, not the wall texture. tmobj.gr images 0-3 really are
				// narrow pillar edge textures (confirmed by visual check), hence masked to 2 bits
				// (4 variants).
				if (uWObject.ID == 352)
				{
					uWObject.Icon = mOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, uWObject.ID);
					uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.TMOBJ, uWObject.Flags & 3);
				}
				if (uWObject.ID == 353)
				{
					uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.TMOBJ, (uWObject.Flags & 7) + 4);
				}
				if (uWObject.ID == 356)
				{
					uWObject.Icon = mOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, uWObject.ID);
					if (uWObject.Flags < 2)
					{
						uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.TMOBJ, uWObject.Flags + 30);
					}
					// From flags 2 on the level's floor list, entry flags - 2 (the reference's bridge.cs:
					// texture_map[flags - 2 + 48]). Ours took flags - 1 until 2026-09-29 and showed the
					// plate over the Wine of Compassion (level 6, 27/50, flags 2) in the grey floor 27
					// instead of the brown marble 12 (per user with screenshots of the original).
					if (uWObject.Flags > 1)
					{
						uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.FLOOR, miTextureInfos[uWObject.Flags - 2 + 48]);
					}
				}
				if (uWObject.ID == 358)
				{
					int num6 = uWObject.Flags & 7;
					uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.TMOBJ, num6 + 20);
				}
				// Gravestone (357, 0x0165): otherwise falls back to the generic object icon.
				// According to uw-formats.txt (vividos/UnderworldAdventures) the wall texture of the
				// gravestone is "flags" + 28 from tmobj.gr. But tmobj.gr has only 38 images, and of those
				// only 28 and 29 are actually gravestone reliefs by visual check (dump of all 38
				// images) (28 matches the original screenshot exactly) - so "flags" here
				// means only 1 bit (2 variants), not the full value range. Fits the
				// same mask pattern as the lever (353: Flags&7+4) or object 358
				// (Flags&7+20). The grave text display (grave.dat/quantity) is separate and not
				// implemented here.
				if (uWObject.ID == 357)
				{
					uWObject.Icon = mOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, uWObject.ID);
					uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.TMOBJ, (uWObject.Flags & 1) + 28);
				}
				// Table (344, 0x0158) and chair (348, 0x015C): according to uw-formats.txt fixed
				// TMOBJ textures (not flags-dependent as with other special cases), otherwise
				// both fall back to the generic object icon. tmobj.gr has only 38 images in this
				// installation (index 0-37, confirmed by visual check) - the
				// documented chair index 38 would be outside the valid range
				// (ArgumentOutOfRangeException, same pattern as with the gravestone before), hence
				// defensively clamped here against the actual image count.
				if (uWObject.ID == 344)
				{
					uWObject.Icon = mOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, uWObject.ID);
					uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.TMOBJ, 32);
				}
				if (uWObject.ID == 348)
				{
					List<UWTexture> lOTmobjTextures = mOTextures.GetTexturesByType(UWTexture.TextureTypes.TMOBJ);
					int liChairTextureIndex = (lOTmobjTextures != null && lOTmobjTextures.Count > 0)
						? System.Math.Min(38, lOTmobjTextures.Count - 1)
						: 0;

					uWObject.Icon = mOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, uWObject.ID);
					uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.TMOBJ, liChairTextureIndex);
				}
				// Wall decorations 366/367 take their wall texture from the Owner via the level
				// texture table. 365 "force field" is the same wall panel: on level 9 with Owner 0 and thus
				// wall texture 64, "nothing" according to string block 10 - black, it hides the paths
				// (per user in the original, 2026-09-14). The reference's tmap.cs also uses the Owner.
				if (uWObject.ID == 365 || uWObject.ID == 366 || uWObject.ID == 367)
				{
					uWObject.Icon = mOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, uWObject.ID);
					uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.WALL, miTextureInfos[uWObject.Owner]);
				}
				if (uWObject.ID >= 368 && uWObject.ID <= 383)
				{
					uWObject.Icon = mOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, uWObject.ID);
					uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.TMFLAT, uWObject.ID & 0xF);
				}
				// Doors come in TWO groups of eight (string block 4): 320-325 are closed
				// doors, 326 a portcullis, 327 a secret door - and 328-333, 334, 335 the same
				// in open state.
				//
				// Until 2026-09-03 the condition ended at 327, which made the branch for the open
				// doors inside it unreachable. This could not show up as a missing
				// texture: they kept the general OBJECTS.GR assignment, i.e. their
				// inventory icon, and were therefore drawn as placeholders (reported by the user on
				// level 7; the ids occur on every level, see the removed tool UWDoorIdDump).
				if (uWObject.ID >= 320 && uWObject.ID <= 335)
				{
					uWObject.Icon = mOTextures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, uWObject.ID);

					// Six door images per group, and the level table holds exactly six
					// entries (58 to 63, see miTextureInfos). Both groups use
					// the same six.
					if (uWObject.ID <= 325)
					{
						uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.DOORS, miTextureInfos[uWObject.ID - 320 + 58]);
					}
					else if (uWObject.ID >= 328 && uWObject.ID <= 333)
					{
						uWObject.Texture = mOTextures.GetTextureByType(UWTexture.TextureTypes.DOORS, miTextureInfos[uWObject.ID - 328 + 58]);
					}

					// Portcullis (326 and 334): no texture from lev.ark/DOORS.GR - the
					// grid geometry is procedural (UWObjectSpawner.fSpawnPortcullis), the
					// real original graphic could not be found in any accessible data source
					// (model table, DOORS.GR, TMOBJ.GR, TMFLAT.GR - all searched).
					//
					// Secret door (327 and 335): according to the user's knowledge (not from the docs)
					// almost identical to the normal door, only with the tile's wall texture instead of
					// the door graphic - disguised as part of the wall. That happens in
					// UWObjectSpawner.fSpawnDoor directly via the tile (pOTile.TextureWall),
					// analogous to the door frame (fSpawnDoorFrames); the texture deliberately stays
					// unchanged here.
				}
				if (i < 256)
				{
					UWNpc uWNpc = (UWNpc)uWObject;
					byte[] array = new byte[19];
					Array.Copy(pyObjectListData, num, array, 0, 19);
					uWNpc.NPCUsed = true;
					// Until 2026-09-11 this held the DOUBLED value (array[0] << 1), and that is how
					// it went into the conversation variables npc_hp and npc_health. The reference takes
					// the byte as it is (uwObject.npc_hp), and the original computes damage
					// on it - the doubled value was a mistake without any apparent reason.
					uWNpc.NPC_HP = array[0];
					uWNpc.HitPoints = array[0];
					uWNpc.RawNpcBytes = (byte[])array.Clone();
					ushort num7 = BitConverter.ToUInt16(array, 3);
					uWNpc.NPCGoal = (byte)(num7 & 0xF);
					uWNpc.NPCGTarg = (byte)((num7 & 0xFF0) >> 4);
					ushort num8 = BitConverter.ToUInt16(array, 5);
					uWNpc.NPCLevel = (byte)(num8 & 0xF);
					uWNpc.NPCLootSpawned = (num8 & 0x1000) != 0;
					uWNpc.NPCTalkedTo = (byte)((num8 & 0x2000) >> 13) == 1;
					uWNpc.NPCAttitude = (byte)((num8 & 0xC000) >> 14);
					uWNpc.NPCSpawned = (num8 & 0x0100) != 0;
					uWNpc.NPCNoHealing = (num8 & 0x0200) != 0;
					uWNpc.NPCAttitudeLocked = (array[2] & 0x80) != 0;
					ushort num9 = BitConverter.ToUInt16(array, 14);
					uWNpc.NPCYHome = (byte)((num9 & 0x3F0) >> 4);
					uWNpc.NPCXHome = (byte)((num9 & 0xFC00) >> 10);
					// The docs give the offsets in the NPC block in hexadecimal: npc_heading is at
					// 0x0010, npc_hunger at 0x0011, npc_whoami at 0x0012 - i.e. at 16, 17 and
					// 18. Previously this read array[12], the hex number 0x12 read as decimal;
					// as a result ALL creatures of level 1 carried the same conversation slot 132,
					// and heading and hunger were not read at all.
					uWNpc.NPCHeading = (byte)(array[16] & 0x1F);
					uWNpc.NPCHunger = (byte)(array[17] & 0x7F);
					uWNpc.NPCIsAlly = (array[17] & 0x40) != 0;
					uWNpc.NPCwhoami = array[18];
					num += 19;
				}
				mOMasterlist.Add(uWObject);
			}
			for (int j = 0; j < 64; j++)
			{
				for (int k = 0; k < 64; k++)
				{
					if (TileData[j * 64 + k].FirstObjectIndex != 0)
					{
						TileData[j * 64 + k].LoadObjects(mOMasterlist, mOTextures);
					}
				}
			}
		}
	}
}
