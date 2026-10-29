using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The world part of a session (UWGameSession.World; cleanup step 2.7, 2026-10-14): the nine
	/// levels with their automaps and animation lists, LEV.ARK as it was last read or written, the
	/// map notes, where the game was loaded from, and the level the player is on - which is also the
	/// level whose static slots the carried objects take (UWCarriedSlots.Level forwards here).
	///
	/// Since step 2.9 (the same day) the levels, the raw LEV.ARK and the map notes live here instead
	/// of in DataImport; UWSaveCodec fills them on loading and writes them back on saving.
	/// </summary>
	public sealed class UWSessionWorld
	{
		/// <summary>The game keeps map notes for this many levels (UWLevArkLayout) - the first game's 99,
		/// which hold the second game's 80 too.</summary>
		public const int MapNoteLevelCount = UWLevArkFile.Uw1MapNoteLevelCount;

		/// <summary>The level the player is on - set by the level loader whenever it builds one.</summary>
		public UWLevel CurrentLevel { get; set; }

		/// <summary>The levels, tiles and objects, as the game changes them: the first game's nine; the
		/// second game's 80, null where LEV.ARK has no map block (UW2 plan phase 2 step 2).</summary>
		public List<UWLevel> Levels { get; internal set; }

		/// <summary>The second game's LEV.ARK as read (null for the first game's, whose blocks are found
		/// through BlockOffsets).</summary>
		public UWArk Ark { get; internal set; }

		/// <summary>LEV.ARK as it was last read or written - the base every save rebuilds from
		/// (UWSaveCodec.SaveWorld copies every block the model does not know byte for byte).</summary>
		public byte[] RawLevelData { get; internal set; }

		/// <summary>The block starts of RawLevelData's header, zero for a block that does not exist.</summary>
		public List<uint> BlockOffsets { get; internal set; }

		/// <summary>Folder of the loaded save game, or empty for a fresh world from the data folder.</summary>
		public string SavegamePath { get; internal set; }

		/// <summary>Whether the world comes from a save game.</summary>
		public bool IsSavegame => !string.IsNullOrEmpty(SavegamePath);

		private readonly List<UWLevel.MapNote>[] mOMapNotes = new List<UWLevel.MapNote>[MapNoteLevelCount];

		/// <summary>The notes of one map page (0-based), null beyond the pages the game keeps. The
		/// list itself - changes are saved with the world.</summary>
		public List<UWLevel.MapNote> GetMapNotes(int piLevelIndex)
		{
			return piLevelIndex >= 0 && piLevelIndex < MapNoteLevelCount
				? mOMapNotes[piLevelIndex]
				: null;
		}

		internal void SetMapNotes(int piLevelIndex, List<UWLevel.MapNote> pONotes)
		{
			mOMapNotes[piLevelIndex] = pONotes;
		}
	}
}
