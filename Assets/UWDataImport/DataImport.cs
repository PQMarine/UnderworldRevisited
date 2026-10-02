using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UWDataImport
{
	public class DataImport
	{
		private byte[] myRawLevelData;

		private int miNumBlocks = 0;

		private List<uint> mOBlockOffsets;

		private UWPalettes mOPalettes;

		/// <summary>Raw 256-colour palettes (PALS.DAT/ALLPALS.DAT) - needed where a
		/// real palette colour is required instead of one taken from a graphic, e.g. for
		/// the single-colour faces of the baked-in 3D models (see UW3DModelImport).</summary>
		public UWPalettes Palettes => mOPalettes;

		/// <summary>Brightness level lookup tables (LIGHT.DAT) - see UWLightLevels.</summary>
		public UWLightLevels LightLevels { get; private set; }

		public List<UWLevel> Levels { get; private set; }

		public UWTextures Textures { get; private set; }

		public UWStrings Strings { get; private set; }

		public UWWeaponAnimations WeaponAnimations { get; private set; }

		public UWWearables Wearables { get; private set; }

		public UWCursors Cursors { get; private set; }

		public UWPowerGem PowerGem { get; set; }

		public UWFlasks Flasks { get; set; }

		/// <summary>Properties of whole object ranges from OBJECTS.DAT, e.g. light sources.</summary>
		public UWObjectProperties ObjectProperties { get; private set; }

		/// <summary>Properties every object has (COMOBJ.DAT) - height, radius, mass,
		/// value, pickupability. See UWCommonObjectProperties.</summary>
		public UWCommonObjectProperties CommonObjectProperties { get; private set; }

		/// <summary>Class-specific tables from OBJECTS.DAT - weapons, armour,
		/// creatures. See UWObjectClassProperties.</summary>
		public UWObjectClassProperties ObjectClassProperties { get; private set; }

		/// <summary>Creature animations from the "crit" folder - see UWCritterAnimations.</summary>
		public UWCritterAnimations CritterAnimations { get; private set; }

		/// <summary>Conversation programs from CNV.ARK - see UWConversations.</summary>
		public UWConversations Conversations { get; private set; }

		/// <summary>The memory of the conversations (BGLOBALS.DAT), see UWConversationGlobals.
		/// Lives here during the game and goes into the save game when saving.</summary>
		public UWConversationGlobals ConversationGlobals { get; private set; }

		/// <summary>Terrain properties of the textures (TERRAIN.DAT) - water, lava, windows,
		/// stairs. See UWTerrain.</summary>
		public UWTerrain Terrain { get; private set; }

		/// <summary>The six original fonts (FONT*.SYS) - see UWFonts.</summary>
		public UWFonts Fonts { get; private set; }

		/// <summary>Combination rules for items (CMB.DAT) - see UWObjectCombining.</summary>
		public UWObjectCombining ObjectCombining { get; private set; }

		/// <summary>Starting character from PLAYER.DAT - see UWPlayerData.</summary>
		public UWPlayerData InitialPlayer { get; private set; }

		/// <summary>Puts a newly created character in place of the loaded one - see
		/// UWCharacterCreationScreen and UWLevelLoader.</summary>
		public void ReplaceInitialPlayer(UWPlayerData pOPlayer)
		{
			if (pOPlayer == null)
				return;

			InitialPlayer = pOPlayer;
			InitialPlayer.AssignTextures(Textures);
		}

		/// <summary>
		/// From now on this folder counts as the loaded save game - after the first
		/// save of a newly created character that ran from the data folder until then
		/// (see UWSavegameWriter). Afterwards everything saves and loads as if the game had
		/// been started from there.
		/// </summary>
		public void AdoptSavegame(string psSavegamePath)
		{
			if (!string.IsNullOrEmpty(psSavegamePath) && Directory.Exists(psSavegamePath))
				SavegamePath = psSavegamePath;
		}

		/// <summary>Colour lookup tables for transparency (XFER.DAT) - see
		/// UWTransparencyTables.</summary>
		public UWTransparencyTables TransparencyTables { get; private set; }

		/// <summary>Lighting and view distance (SHADES.DAT) - see UWShades.</summary>
		public UWShades Shades { get; private set; }

		/// <summary>The game keeps map notes for this many levels.</summary>
		public const int MapNoteLevelCount = 99;

		private readonly List<UWLevel.MapNote>[] mOMapNotes = new List<UWLevel.MapNote>[MapNoteLevelCount];

		/// <summary>Where does the block that starts at this position end? The file only lists
		/// starts, so the next start after it counts as the end - and for the last one the
		/// end of the file.</summary>
		private int fGetBlockEnd(uint piOffset)
		{
			int liEnd = myRawLevelData.Length;

			foreach (uint lyOther in mOBlockOffsets)
			{
				if (lyOther > piOffset && lyOther < liEnd)
					liEnd = (int)lyOther;
			}

			return liEnd;
		}

		/// <summary>First block of the automaps. One per level follows.</summary>
		public const int AutomapFirstBlock = 27;

		/// <summary>Blocks 0 to 8 are the levels themselves: tiles and objects.</summary>
		private const int LevelBlockCount = 9;

		/// <summary>Object 1 of a level, the player character - 27 bytes from the first mobile
		/// record (see UWPlayerData.PlayerObjectOffset).</summary>
		private const int PlayerSlotOffset = 0x4000 + 27;

		/// <summary>
		/// Writes map notes, automaps AND the levels themselves (tiles and objects, see
		/// UWLevelWriter) back into LEV.ARK.
		///
		/// The file is REBUILT in the process, because a note block grows and shrinks: header
		/// with all block starts, then the blocks in the order they had in the file (blocks
		/// that did not exist before at the end). Everything except the notes, the automaps
		/// and the level blocks is copied byte for byte.
		///
		/// Before the first write a backup is created as LEV.ARK.bak. An empty
		/// note block gets the start zero - that is how the file marks a block that
		/// does not exist.
		/// </summary>
		/// <param name="psTargetFolder">Selects the save game that is written to - empty means the
		/// loaded one. Saving from the options panel (see UWSavegameWriter) selects the folder
		/// and writes location and map into it.</param>
		/// <param name="pyPlayerSlot">The player object for object 1 of level piPlayerLevel
		/// (see UWPlayerData.GetLevelPlayerSlot). Without it, object 1 stays as it was - for
		/// a new character that would be the junk data from DATA\LEV.ARK.</param>
		public bool SaveMapData(string psTargetFolder = null, byte[] pyPlayerSlot = null, int piPlayerLevel = -1)
		{
			if (myRawLevelData == null || mOBlockOffsets == null)
				return false;

			// Without a save game only into an explicitly named folder - that is the case of
			// a newly created character whose world still comes from the data folder.
			if (!IsSavegame && string.IsNullOrEmpty(psTargetFolder))
				return false;

			string lsFolder = string.IsNullOrEmpty(psTargetFolder) ? SavegamePath : psTargetFolder;
			string lsPath = Path.Combine(lsFolder, "LEV.ARK");

			if (!File.Exists(lsPath))
				return false;

			string lsBackup = lsPath + ".bak";

			if (!File.Exists(lsBackup))
				File.Copy(lsPath, lsBackup);

			int liCount = mOBlockOffsets.Count;

			byte[][] lyBlocks = new byte[liCount][];

			UWLevelWriter.Warnings.Clear();

			for (int liBlock = 0; liBlock < liCount; liBlock++)
			{
				// THE LEVELS THEMSELVES - tiles and objects, i.e. the state of the world (since
				// 2026-09-10, see UWLevelWriter). The basis is the block as it last stood in
				// the file; only what the model knows is overwritten.
				if (Levels != null && liBlock < Levels.Count && liBlock < LevelBlockCount
					&& mOBlockOffsets[liBlock] != 0
					&& mOBlockOffsets[liBlock] + UWLevelWriter.BlockSize <= myRawLevelData.Length)
				{
					byte[] lyOriginal = new byte[UWLevelWriter.BlockSize];
					Array.Copy(myRawLevelData, (int)mOBlockOffsets[liBlock], lyOriginal, 0, UWLevelWriter.BlockSize);

					lyBlocks[liBlock] = UWLevelWriter.BuildBlock(Levels[liBlock], lyOriginal);

					if (liBlock == piPlayerLevel && pyPlayerSlot != null && pyPlayerSlot.Length == 27)
						Array.Copy(pyPlayerSlot, 0, lyBlocks[liBlock], PlayerSlotOffset, pyPlayerSlot.Length);

					continue;
				}

				int liNoteLevel = liBlock - 36;

				if (liNoteLevel >= 0 && liNoteLevel < MapNoteLevelCount)
				{
					lyBlocks[liBlock] = fBuildMapNoteBlock(mOMapNotes[liNoteLevel]);
					continue;
				}

				// The automap comes from the level itself - that is where walking records which
				// tiles were entered.
				int liMapLevel = liBlock - AutomapFirstBlock;

				if (liMapLevel >= 0 && Levels != null && liMapLevel < Levels.Count
					&& Levels[liMapLevel].AutomapTiles != null)
				{
					lyBlocks[liBlock] = (byte[])Levels[liMapLevel].AutomapTiles.Clone();
					continue;
				}

				uint lyOffset = mOBlockOffsets[liBlock];

				if (lyOffset == 0)
					continue;

				int liEnd = fGetBlockEnd(lyOffset);
				int liLength = liEnd - (int)lyOffset;

				if (liLength <= 0)
					continue;

				lyBlocks[liBlock] = new byte[liLength];
				Array.Copy(myRawLevelData, (int)lyOffset, lyBlocks[liBlock], 0, liLength);
			}

			int liHeader = 2 + (liCount * 4);
			int liTotal = liHeader;

			foreach (byte[] lyBlock in lyBlocks)
			{
				if (lyBlock != null)
					liTotal += lyBlock.Length;
			}

			byte[] lyOut = new byte[liTotal];

			lyOut[0] = (byte)(liCount & 0xFF);
			lyOut[1] = (byte)((liCount >> 8) & 0xFF);

			int liAt = liHeader;

			// Write in the ORDER OF THE FILE, not by index - that way a save without
			// changes stays byte for byte the same file. Blocks that did not exist before
			// are appended at the end.
			//
			// The original orders slightly differently: it also moves a GROWN block to the
			// end, while we leave it in place and shift the following ones up. Checked against
			// a save of the original (2026-09-03): same file size, 134 of 135
			// blocks identical byte for byte - differing only in the game state that the original
			// had changed itself. Replicating its order exactly is not worth it: the
			// moved blocks stand there in the order in which they were EDITED,
			// not according to a rule.
			List<int> lOOrder = new List<int>();

			for (int liBlock = 0; liBlock < liCount; liBlock++)
			{
				if (lyBlocks[liBlock] != null)
					lOOrder.Add(liBlock);
			}

			lOOrder.Sort((piLeft, piRight) =>
			{
				uint lyLeft = mOBlockOffsets[piLeft];
				uint lyRight = mOBlockOffsets[piRight];

				if (lyLeft == 0 && lyRight == 0)
					return piLeft.CompareTo(piRight);

				if (lyLeft == 0)
					return 1;

				if (lyRight == 0)
					return -1;

				return lyLeft.CompareTo(lyRight);
			});

			foreach (int liBlock in lOOrder)
			{
				int liEntry = 2 + (liBlock * 4);

				lyOut[liEntry + 0] = (byte)(liAt & 0xFF);
				lyOut[liEntry + 1] = (byte)((liAt >> 8) & 0xFF);
				lyOut[liEntry + 2] = (byte)((liAt >> 16) & 0xFF);
				lyOut[liEntry + 3] = (byte)((liAt >> 24) & 0xFF);

				Array.Copy(lyBlocks[liBlock], 0, lyOut, liAt, lyBlocks[liBlock].Length);

				liAt += lyBlocks[liBlock].Length;
			}

			File.WriteAllBytes(lsPath, lyOut);

			// Our own state has to follow, otherwise a later save computes with the
			// old starts.
			myRawLevelData = lyOut;

			mOBlockOffsets.Clear();

			for (int liBlock = 0; liBlock < liCount; liBlock++)
				mOBlockOffsets.Add(BitConverter.ToUInt32(lyOut, 2 + (liBlock * 4)));

			return true;
		}

		/// <summary>
		/// A note block: 54 bytes per line, 50 of them text. Without lines the block does not exist.
		///
		/// DEVIATION FROM THE ORIGINAL, deliberate: there the LAST line of a level cannot be
		/// deleted at all - it is back as soon as the map is reopened, without any
		/// save (reproduced per user on levels 46 and 99, 2026-09-03; per user 2026-09-25 only seen on
		/// the note pages beyond level 9, the real levels 1 to 9 delete it). Here it
		/// disappears together with its block.
		/// </summary>
		private static byte[] fBuildMapNoteBlock(List<UWLevel.MapNote> pONotes)
		{
			if (pONotes == null || pONotes.Count == 0)
				return null;

			const int liRecordSize = 54;
			const int liTextSize = 50;

			byte[] lyBlock = new byte[pONotes.Count * liRecordSize];

			for (int liNote = 0; liNote < pONotes.Count; liNote++)
			{
				int liAt = liNote * liRecordSize;

				string lsText = pONotes[liNote].Text ?? string.Empty;

				for (int liChar = 0; liChar < lsText.Length && liChar < liTextSize - 1; liChar++)
					lyBlock[liAt + liChar] = (byte)lsText[liChar];

				lyBlock[liAt + 0x32] = (byte)(pONotes[liNote].X & 0xFF);
				lyBlock[liAt + 0x33] = (byte)((pONotes[liNote].X >> 8) & 0xFF);
				lyBlock[liAt + 0x34] = (byte)(pONotes[liNote].Y & 0xFF);
				lyBlock[liAt + 0x35] = (byte)((pONotes[liNote].Y >> 8) & 0xFF);
			}

			return lyBlock;
		}

		public List<UWLevel.MapNote> GetMapNotes(int piLevelIndex)
		{
			return piLevelIndex >= 0 && piLevelIndex < MapNoteLevelCount
				? mOMapNotes[piLevelIndex]
				: null;
		}

		/// <summary>The small remaining files: MONO.DAT, WEAPONS.CM, SKILLS.DAT as well as the
		/// undocumented LIGHTS.DAT and CHRGEN.DAT as raw bytes.</summary>
		public UWMiscDataFiles MiscDataFiles { get; private set; }

		/// <summary>Sound effects and music from the "sound" folder - see UWSound.</summary>
		public UWSound Sound { get; private set; }

		/// <summary>Folder with the game data (DATA), needed for files outside
		/// of it - see GetCutscene.</summary>
		public string DataPath { get; private set; }

		private readonly Dictionary<string, UWCutscene> mOCutscenes = new Dictionary<string, UWCutscene>();

		public string GetTextureDescription(int piTextureIndex)
		{
			return Strings.Blocks[10].Strings[piTextureIndex];
		}

		public string GetObjectDescription(int pObjectId)
		{
			return Strings.Blocks[4].Strings[pObjectId];
		}

		public string GetObjectLookAndQualityDescription(int pObjectId)
		{
			return Strings.Blocks[5].Strings[pObjectId];
		}

		public string GetWallTextBookDescription(int pObjectId)
		{
			return Strings.Blocks[3].Strings[pObjectId];
		}

		/// <summary>Readable inscriptions (block 8): wall writings, plaques, signs,
		/// runes, shrine texts - and from index 370 on the matching introductions ("The writing
		/// reads: ", "The plaque reads: ", ...). See
		/// UWObjectMechanics.GetWritingTextIndex/GetWritingPrefixIndex.</summary>
		public string GetReadableText(int piIndex)
		{
			return Strings.Blocks[8].Strings[piIndex];
		}

		/// <summary>The text of a book or scroll (block 3) - for the index see
		/// UWObjectMechanics.GetBookStringIndex. Empty means: not a text but a picture.
		/// </summary>
		public string GetBookText(int piIndex)
		{
			if (piIndex < 0 || Strings == null || !Strings.Blocks.ContainsKey(3))
				return string.Empty;

			var lOStrings = Strings.Blocks[3].Strings;

			return piIndex < lOStrings.Count ? lOStrings[piIndex] : string.Empty;
		}

		/// <summary>General game messages (block 1) - the feedback messages not tied to
		/// a level, e.g. "The cauldron is empty." or the fountain texts.
		/// Index is the ALREADY PARSED index, i.e. the one from the string export; the
		/// project's usual +1 only applies when starting from an object id.</summary>
		public string GetGeneralMessage(int piIndex)
		{
			return Strings.Blocks[1].Strings[piIndex];
		}

		/// <summary>Level-related event messages (block 9), e.g. the texts of the
		/// a_text string traps - see UWObjectMechanics.GetTextTrapStringIndex for how the
		/// index is formed. Block 9 also contains global texts (intro sequence,
		/// special messages like "The doors are securely locked.") in the lower 64
		/// entries, which do NOT belong to a level.</summary>
		public string GetLevelMessage(int piIndex)
		{
			return Strings.Blocks[9].Strings[piIndex];
		}

		/// <summary>Cutscene animation from the "cuts" folder, which lies NEXT TO "data" (i.e.
		/// C:\UW\CUTS alongside C:\UW\DATA). The result is cached because a file is needed
		/// several times (e.g. CS400.N01 for the windows of every level). If the folder is
		/// missing (with an incomplete copy of the game data it is often only in the CD image),
		/// null is returned instead of an exception.</summary>
		public UWCutscene GetCutscene(string psFileName)
		{
			UWCutscene lOResult;

			if (mOCutscenes.TryGetValue(psFileName, out lOResult))
			{
				return lOResult;
			}

			string lsRoot = Path.GetDirectoryName(DataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
			string lsPath = Path.Combine(Path.Combine(lsRoot, "CUTS"), psFileName);
			lOResult = File.Exists(lsPath) ? new UWCutscene(lsPath) : null;
			mOCutscenes[psFileName] = lOResult;

			return lOResult;
		}

		/// <summary>Folder of the loaded save game, or empty on a normal start.</summary>
		public string SavegamePath { get; private set; }

		/// <summary>Whether the world data comes from a save game.</summary>
		public bool IsSavegame => !string.IsNullOrEmpty(SavegamePath);

		/// <summary>
		/// Loads the static game data and then the game state - either from the save game
		/// folder or, without one, the untouched world of the data folder. From a save game
		/// only the changeable part comes (LEV.ARK, PLAYER.DAT, BGLOBALS.DAT, see LoadGame);
		/// everything else (textures, strings, tables) stays with the data folder. Diagnostic
		/// tools use this; the game itself keeps one instance per data folder and calls LoadGame
		/// for every start (see UWLevelLoader).
		/// </summary>
		public DataImport(string psPath, string psSavegamePath = null)
		{
			fLoadStaticData(psPath);
			LoadGame(psSavegamePath);
		}

		/// <summary>
		/// (Re)loads everything that belongs to one game: world (LEV.ARK with automaps and map
		/// notes), character (PLAYER.DAT) and conversation memory (BGLOBALS.DAT). Null or an
		/// invalid folder means the data folder, i.e. a fresh world. Everything else - textures,
		/// strings, tables, fonts, sound - stays as loaded, which is what makes starting a game
		/// from the main menu cheap.
		/// </summary>
		public void LoadGame(string psSavegamePath)
		{
			SavegamePath = null;

			if (!string.IsNullOrEmpty(psSavegamePath) && Directory.Exists(psSavegamePath)
				&& File.Exists(Path.Combine(psSavegamePath, "LEV.ARK")))
				SavegamePath = psSavegamePath;

			ConversationGlobals = new UWConversationGlobals(DataPath, IsSavegame ? SavegamePath : null);
			InitialPlayer = new UWPlayerData(IsSavegame ? SavegamePath : DataPath);
			InitialPlayer.AssignTextures(Textures);
			fLoadLevels(Path.Combine(IsSavegame ? SavegamePath : DataPath, "LEV.ARK"));
		}

		/// <summary>The part of the data that does not depend on a save game.</summary>
		private void fLoadStaticData(string psPath)
		{
			DataPath = psPath;

			fLoadPalettes(psPath);
			LightLevels = new UWLightLevels(psPath);
			fLoadTextures(psPath);
			PowerGem = new UWPowerGem(Textures);
			Flasks = new UWFlasks(Textures);
			WeaponAnimations = new UWWeaponAnimations(psPath, Textures.GetTexturesByType(UWTexture.TextureTypes.WEAPONS));
			Wearables = new UWWearables(Textures, WeaponAnimations.Black);
			Cursors = new UWCursors(Textures.GetTexturesByType(UWTexture.TextureTypes.CURSORS));
			ObjectProperties = new UWObjectProperties(psPath);
			CommonObjectProperties = new UWCommonObjectProperties(psPath);
			ObjectClassProperties = new UWObjectClassProperties(psPath);
			CritterAnimations = new UWCritterAnimations(psPath);
			Conversations = new UWConversations(psPath);
			Terrain = new UWTerrain(psPath);
			Fonts = new UWFonts(psPath);
			ObjectCombining = new UWObjectCombining(psPath);
			TransparencyTables = new UWTransparencyTables(psPath);
			Shades = new UWShades(psPath);
			MiscDataFiles = new UWMiscDataFiles(psPath);
			Sound = new UWSound(psPath);
			fLoadStrings($"{psPath}\\STRINGS.PAK");
		}

		private void fLoadPalettes(string psPaletteDirectory)
		{
			mOPalettes = new UWPalettes(psPaletteDirectory);
		}

		private void fLoadTextures(string psPath)
		{
			Textures = new UWTextures(psPath, mOPalettes);
		}

		private void fLoadStrings(string psPath)
		{
			Strings = new UWStrings(File.ReadAllBytes(psPath));
		}

		private void fLoadLevels(string psLevelFullPath)
		{
			myRawLevelData = File.ReadAllBytes(psLevelFullPath);
			miNumBlocks = BitConverter.ToInt16(myRawLevelData, 0);
			mOBlockOffsets = new List<uint>();
			int num = miNumBlocks * 4 + 2;
			for (int i = 2; i < num; i += 4)
			{
				mOBlockOffsets.Add(BitConverter.ToUInt32(myRawLevelData, i));
			}
			Levels = new List<UWLevel>();
			for (int j = 0; j < 9; j++)
			{
				UWLevel item = new UWLevel(j + 1, myRawLevelData, mOBlockOffsets[j], mOBlockOffsets[j + 18], Textures);

				// In UW1 the automap starts at block 27: the 135 blocks are nine levels times
				// fifteen - maps, animations, textures, automap, notes. The 160 from
				// uw-formats.txt 4.7 applies to Underworld 2, which has 320 blocks.
				if (mOBlockOffsets.Count > 27 + j)
					item.LoadAutomap(myRawLevelData, mOBlockOffsets[27 + j]);

				Levels.Add(item);
			}

			// Map notes exist for NINETY-NINE levels, not only for the nine
			// walkable ones: blocks 36 to 134 are one each. uw-formats.txt calls the
			// later ones "unused" - but the user wrote on levels 10, 46 and 99,
			// saved and reloaded, and the texts were in exactly these blocks
			// (2026-09-03). 36 plus 99 gives the 135 blocks of the file.
			for (int liLevel = 0; liLevel < MapNoteLevelCount; liLevel++)
			{
				int liBlock = 36 + liLevel;

				mOMapNotes[liLevel] = liBlock < mOBlockOffsets.Count
					? UWLevel.ParseMapNotes(myRawLevelData, mOBlockOffsets[liBlock], fGetBlockEnd(mOBlockOffsets[liBlock]))
					: new List<UWLevel.MapNote>();
			}
		}
	}
}
