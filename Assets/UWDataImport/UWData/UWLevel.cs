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
		/// Stored in LEV.ARK from block 27 on (UWLevArkLayout.AutomapFirstBlock), 0x1000 bytes per
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

				/// <summary>The level's animation list (LEV.ARK block 8 + level), see UWAnimationOverlay;
		/// empty and unloaded without the block.</summary>
		public UWAnimationOverlay Animations { get; private set; } = new UWAnimationOverlay();

		public void LoadAnimations(byte[] pyRawLevelData, uint piOffset)
		{
			Animations = UWAnimationOverlay.Load(pyRawLevelData, (int)piOffset, mOMasterlist);
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
			if (AutomapTiles == null || TileData == null || !new UWTilePos(piTileX, piTileY).IsOnMap)
				return false;

			int liAt = new UWTilePos(piTileX, piTileY).Index;

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
		/// (UWPlayerTerrain.fUpdateAutomap), AND every cell the original's sweep draws bright
		/// enough (UWExplorationRules.EvaluateSweep). The second one was missing, so the map only
		/// ever grew by a tile at a time while the original reveals as far as the light lets one
		/// see. The eight neighbours of the own tile were dropped on 2026-09-23 (measured: the
		/// original does not discover them in the dark).
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
		/// where our host recognises no marker, an existing door or bridge is kept - our detection
		/// of those may not cover every object the original's renderer marks, and a re-seen tile
		/// must not lose it. A stair is not kept: since 2026-10-07 it is found by the original's
		/// own rule (UWTileQueries.HasStairMarker), and the port's earlier rule had written
		/// stairs onto tiles without one - seeing the tile again clears them.
		/// </summary>
		/// <returns>True if the byte has changed.</returns>
		public bool MarkTileVisited(int piTileX, int piTileY, int piDisplayType)
		{
			if (AutomapTiles == null || TileData == null)
				return false;

			if (!new UWTilePos(piTileX, piTileY).IsOnMap)
				return false;

			int liAt = new UWTilePos(piTileX, piTileY).Index;

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

			int liOldMarker = (lyOld >> 4) & MapDisplayMarkerMask;

			if (lbDiscovered && (liDisplay & MapDisplayMarkerMask) == 0 && liOldMarker != MapDisplayStair)
				liDisplay |= liOldMarker;

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
			byte[] array = new byte[UWLevArkLayout.TextureMapSize];
			Array.Copy(pyRawLevelData, piTextureUsageOffset, array, 0L, UWLevArkLayout.TextureMapSize);
			fLoadTextureInfo(array);
			array = new byte[UWLevArkLayout.TileMapSize];
			Array.Copy(pyRawLevelData, miBlockOffset, array, 0L, UWLevArkLayout.TileMapSize);
			fLoadTileMap(array);
			// Until 2026-10-12 this copied 13200 bytes, 144 past the two object lists into the
			// mobile free list; the parse never read them.
			array = new byte[UWLevArkLayout.ObjectListSize];
			Array.Copy(pyRawLevelData, miBlockOffset + UWLevArkLayout.MobileBase, array, 0L, UWLevArkLayout.ObjectListSize);
			fLoadObjectLists(array);
			// The lists at the block's end, through UWLevArkCodec (the layout in UWLevArkLayout).
			mOFreeMobileSlots = UWLevArkCodec.ReadMobileFreeList(pyRawLevelData, (int)miBlockOffset);
			mOFreeStaticSlots = UWLevArkCodec.ReadStaticFreeList(pyRawLevelData, (int)miBlockOffset);
			mOActiveMobiles = UWLevArkCodec.ReadActiveMobiles(pyRawLevelData, (int)miBlockOffset);

			fTakeFlightsOutOfTiles();

			// A rune of warding an older save of ours laid without its flags (2026-10-14).
			UWTrapRules.RepairOldWardRunes(this);
		}

		/// <summary>
		/// THE LIST OF THE ALLOCATED MOBILE OBJECTS, in the original's order: GetFreeObject
		/// appends a new mobile (seg027_2861_A70), freeing puts the last entry into its place
		/// (seg027_2861_A83), and the level file keeps the list at 0x7AFC. Routines that look
		/// for a creature by its whoami walk it in this order and stop at the first match
		/// (see RunFunctionOnWhoAmIList_seg038_3307_D4E) - per user, 2026-09-29: Tyball's death took
		/// the 209 guard #222 before #223 and #200, exactly the order of this list. Read by
		/// UWLevArkCodec.ReadActiveMobiles in the constructor.
		/// </summary>
		private List<int> mOActiveMobiles;

		/// <summary>
		/// THE THINGS IN FLIGHT ON THIS LEVEL (2026-10-19): arrows, bolts, spell missiles, thrown
		/// and knocked-loose things that are moving - the original's mobile non-creatures on the
		/// active list (see UWMobileRecordCodec), which move_mobile steps like any flight. They are
		/// in no tile list here: a flight is not a lying object, the motion host asks its flights
		/// itself (UWMotionHost). The level keeps them while the player is elsewhere, the motion
		/// host steps them while he is here (it adds and removes its flights), and UWLevelWriter
		/// writes them back as mobile records in their tiles' lists. In the active list's order.
		/// Until 2026-10-19 a flight from a save stood still as a lying object, and a flight of ours
		/// was lost on saving or leaving the level.
		/// </summary>
		public List<UWMobileRecord> InFlight { get; } = new List<UWMobileRecord>();

		/// <summary>The flights of the level data out of their tiles' lists into InFlight, each with
		/// its place in its list as read (all places first, then the removals, so two flights of one
		/// tile come back where they were).</summary>
		private void fTakeFlightsOutOfTiles()
		{
			if (mOActiveMobiles == null || mOMasterlist == null || TileData == null)
				return;

			List<UWMobileRecord> lOFound = new List<UWMobileRecord>();

			foreach (int liIndex in mOActiveMobiles)
			{
				UWObject lOObject = liIndex > 0 && liIndex < mOMasterlist.Count ? mOMasterlist[liIndex] : null;
				UWTile lOTile = lOObject != null ? GetTile(lOObject.TileX, lOObject.TileY) : null;
				int liAt = lOTile != null && lOTile.ObjectsInTile != null ? lOTile.ObjectsInTile.IndexOf(lOObject) : -1;

				if (!UWMobileRecordCodec.IsInFlight(lOObject, true, liAt >= 0))
					continue;

				UWMobileRecord lORecord = UWMobileRecordCodec.Read(lOObject, ((UWNpc)lOObject).RawNpcBytes,
					lOObject.TileX, lOObject.TileY);

				lORecord.ChainPosition = liAt;
				lORecord.Slot = liIndex;
				lOFound.Add(lORecord);
			}

			foreach (UWMobileRecord lORecord in lOFound)
			{
				GetTile(lORecord.TileX, lORecord.TileY).ObjectsInTile.Remove(lORecord.Object);
				InFlight.Add(lORecord);
			}
		}

		/// <summary>The place of a mobile slot in the list of the allocated mobiles, or
		/// int.MaxValue when it is not in it.</summary>
		public int GetActiveMobileOrder(int piMobileIndex)
		{
			int liAt = mOActiveMobiles != null ? mOActiveMobiles.IndexOf(piMobileIndex) : -1;

			return liAt < 0 ? int.MaxValue : liAt;
		}

		/// <summary>The mobile free list in storage order, the top of the stack last
		/// (UWLevArkCodec.ReadMobileFreeList; the layout in UWLevArkLayout).</summary>
		private List<int> mOFreeMobileSlots;

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

			ReleaseMobileSlotAt(mOMasterlist.IndexOf(pOObject));
		}

		/// <summary>The same for a slot known by its number - a thing in flight keeps the slot it
		/// holds in its record (UWMobileRecord.Slot, 2026-10-19).</summary>
		public void ReleaseMobileSlotAt(int piIndex)
		{
			if (mOFreeMobileSlots == null)
				return;

			if (piIndex < UWLevArkLayout.FirstFreeMobileSlot || !UWLevArkLayout.IsMobileSlot(piIndex)
				|| mOFreeMobileSlots.Contains(piIndex))
				return;

			mOFreeMobileSlots.Add(piIndex);

			// Out of the allocated list: the last entry takes its place (free_critter, seg027_2861_A83).
			if (mOActiveMobiles != null)
				UWLevArkCodec.RemoveActiveMobile(mOActiveMobiles, piIndex);
		}

		/// <summary>The static free list, slots 256 to 1023, in storage order
		/// (UWLevArkCodec.ReadStaticFreeList).</summary>
		private List<int> mOFreeStaticSlots;

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

		/// <summary>The three lists as they stand, the top of each free list last - read only, for
		/// the self-check (UWWorldEdit's group, 2026-10-12).</summary>
		public IReadOnlyList<int> FreeMobileSlots => mOFreeMobileSlots;

		public IReadOnlyList<int> FreeStaticSlots => mOFreeStaticSlots;

		public IReadOnlyList<int> ActiveMobiles => mOActiveMobiles;

		/// <summary>Whether a static slot is on the free list (for the self-check).</summary>
		public bool IsStaticSlotFree(int piIndex)
		{
			return mOFreeStaticSlots != null && mOFreeStaticSlots.Contains(piIndex);
		}

		/// <summary>AddToFreeObjectsList_seg027_4DD for a static slot: back onto the top.</summary>
		public void ReturnStaticSlot(int piIndex)
		{
			if (mOFreeStaticSlots == null || !UWLevArkLayout.IsStaticSlot(piIndex)
				|| mOFreeStaticSlots.Contains(piIndex))
				return;

			mOFreeStaticSlots.Add(piIndex);
		}

		/// <summary>
		/// THE OBJECT STORE EMPTIED, as Map_ObjFix (OBJECTS.C) leaves it - what the Armageddon
		/// spell's clearobj (UWEDIT.C) runs, and do_level_hacks (GAMEWRAP.C) on every arrival
		/// afterwards: the free stacks refilled with every index from 2 to 0x3FF in ascending
		/// order (mobile 2 to 0xFF, static 0x100 to 0x3FF, so the next one handed out is the
		/// highest), the active mobile list and the animation list emptied (animcount 0). The
		/// tile lists are the caller's (UWWorldEdit.WipeAll); the Masterlist keeps the old records
		/// until their numbers are handed out again, and UWLevelWriter frees what no tile reaches.
		/// A list this level never read stays unread.
		/// </summary>
		public void ResetObjectStore()
		{
			if (mOFreeMobileSlots != null)
			{
				mOFreeMobileSlots.Clear();

				for (int liIndex = UWLevArkLayout.FirstFreeMobileSlot; UWLevArkLayout.IsMobileSlot(liIndex); liIndex++)
					mOFreeMobileSlots.Add(liIndex);
			}

			if (mOFreeStaticSlots != null)
			{
				mOFreeStaticSlots.Clear();

				for (int liIndex = UWLevArkLayout.MobileCount; liIndex < UWLevArkLayout.ObjectCount; liIndex++)
					mOFreeStaticSlots.Add(liIndex);
			}

			if (mOActiveMobiles != null)
				mOActiveMobiles.Clear();

			// The things in flight were on the active list (2026-10-19).
			InFlight.Clear();

			Animations.Entries.Clear();
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

				if (liIndex < UWLevArkLayout.FirstFreeMobileSlot || !UWLevArkLayout.IsMobileSlot(liIndex)
					|| liIndex >= mOMasterlist.Count)
					continue;

				mOMasterlist[liIndex] = pOObject;
				piIndex = liIndex;

				// Appended to the allocated list (active_critter, seg027_2861_A70).
				if (mOActiveMobiles != null)
					UWLevArkCodec.AddActiveMobile(mOActiveMobiles, liIndex);

				return true;
			}

			return false;
		}

		private void fLoadTextureInfo(byte[] pyRawTextureInfoData)
		{
			miTextureInfos = new ushort[64];
			for (int i = 0; i < 96; i += 2)
				miTextureInfos[i / 2] = UWBinary.ReadU16(pyRawTextureInfoData, i);
			for (int i = 96; i < 116; i += 2)
				miTextureInfos[48 + (i - 96) / 2] = UWBinary.ReadU16(pyRawTextureInfoData, i);
			Array.Copy(pyRawTextureInfoData, 116, miTextureInfos, 58, 6);
		}

		private void fLoadTileMap(byte[] pyTileMapData)
		{
			TileData = new UWTile[UWLevArkLayout.TileCount];
			for (int i = 0; i < UWLevArkLayout.TileCount; i++)
				TileData[i] = new UWTile(i, UWBinary.Read32(pyTileMapData, i * UWLevArkLayout.TileRecordSize), miTextureInfos);
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

		/// <summary>The 1024 records: 256 mobile ones of 27 bytes, then 768 static ones of 8.
		/// The record itself is UWObjectRecordCodec's, the nineteen creature bytes
		/// UWNpcRecordCodec's (2026-10-12); what stays here is the per-id texture choice.</summary>
		private void load_object_list(byte[] pyObjectListData)
		{
			mOMasterlist = new List<UWObject>();
			int num = 0;
			for (int i = 0; i < UWLevArkLayout.ObjectCount; i++)
			{
				bool lbMobile = UWLevArkLayout.IsMobileSlot(i);
				ushort piItem_id = (ushort)UWObjectRecordCodec.ReadId(pyObjectListData, num);
				UWObject uWObject = lbMobile ? new UWNpc(piItem_id) : new UWObject(piItem_id);
				UWObjectRecordCodec.Read(pyObjectListData, num, uWObject);
				num += UWObjectRecordCodec.RecordSize;
				// The pictures: the OBJECTS.GR image and, for the kinds drawn from another file, that
				// file's - the per-id rules moved to UWObjectAppearance on 2026-10-12 so that an object
				// made during play wears the same (cleanup plan section 4.1).
				UWObjectAppearance.Apply(uWObject, mOTextures, miTextureInfos);
				if (lbMobile)
				{
					UWNpcRecordCodec.Read(pyObjectListData, num, (UWNpc)uWObject);
					num += UWNpcRecordCodec.ByteCount;
				}
				mOMasterlist.Add(uWObject);
			}
			for (int j = 0; j < UWWorldScale.TilesPerAxis; j++)
			{
				for (int k = 0; k < UWWorldScale.TilesPerAxis; k++)
				{
					if (TileData[(j * UWWorldScale.TilesPerAxis) + k].FirstObjectIndex != 0)
					{
						TileData[(j * UWWorldScale.TilesPerAxis) + k].LoadObjects(mOMasterlist, mOTextures);
					}
				}
			}
		}
	}
}
