using System;
using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A GAME ON DISK (cleanup step 2.9, 2026-10-14): Load reads a save game folder - or, without one,
	/// the untouched world of the data folder - into a new UWGameSession; SaveWorld writes the
	/// session's levels, automaps, animation lists and map notes back into LEV.ARK; Save writes a
	/// whole slot (PLAYER.DAT, LEV.ARK, BGLOBALS.DAT, DESC).
	///
	/// Built from the codecs that were there: UWPlayerData for PLAYER.DAT, UWLevArkCodec /
	/// UWLevArkLayout / UWLevelWriter for LEV.ARK and UWConversationGlobals for BGLOBALS.DAT. Until
	/// then the load and the LEV.ARK write were the second half of DataImport (LoadGame,
	/// SaveMapData) and the slot write was UWSavegameStore.Write; both now forward here, so their
	/// callers keep working. Moved verbatim: the round trip of the self-check stays byte-identical.
	/// </summary>
	public static class UWSaveCodec
	{
		/// <summary>Any key byte - the encryption is an XOR, any value works.</summary>
		private const byte NewGameKey = 0x2A;

		private static readonly string[] msBaseFiles = { "PLAYER.DAT", "LEV.ARK", "BGLOBALS.DAT" };

		// ------------------------------------------------- Loading

		/// <summary>
		/// Everything that belongs to one game: world (LEV.ARK with automaps and map notes),
		/// character (PLAYER.DAT) and conversation memory (BGLOBALS.DAT), in a new session. Null or
		/// an invalid folder (one without LEV.ARK) means the data folder, i.e. a fresh world.
		/// </summary>
		public static UWGameSession Load(UWGameData pOData, string psSavegamePath)
		{
			UWGameSession lOSession = new UWGameSession(pOData);
			UWSessionWorld lOWorld = lOSession.World;

			lOWorld.SavegamePath = null;

			if (!string.IsNullOrEmpty(psSavegamePath) && Directory.Exists(psSavegamePath)
				&& File.Exists(Path.Combine(psSavegamePath, "LEV.ARK")))
				lOWorld.SavegamePath = psSavegamePath;

			string lsFolder = lOWorld.IsSavegame ? lOWorld.SavegamePath : pOData.DataPath;

			lOSession.ConversationMemory = new UWConversationGlobals(pOData.DataPath, lOWorld.IsSavegame ? lOWorld.SavegamePath : null);
			lOSession.Player = new UWPlayerData(lsFolder);
			lOSession.Player.AssignTextures(pOData.Textures);
			fLoadLevels(lOWorld, Path.Combine(lsFolder, "LEV.ARK"), pOData.Textures);

			return lOSession;
		}

		/// <summary>The five kinds of blocks by UWLevArkLayout's numbers (the reasons for 27 and
		/// for ninety-nine note blocks stand there); the header through UWLevArkCodec.</summary>
		private static void fLoadLevels(UWSessionWorld pOWorld, string psLevelFullPath, UWTextures pOTextures)
		{
			byte[] lyRaw = File.ReadAllBytes(psLevelFullPath);
			List<uint> lOOffsets = UWLevArkCodec.ReadBlockOffsets(lyRaw);
			List<UWLevel> lOLevels = new List<UWLevel>();

			pOWorld.RawLevelData = lyRaw;
			pOWorld.BlockOffsets = lOOffsets;

			for (int j = 0; j < UWLevArkLayout.LevelBlockCount; j++)
			{
				UWLevel item = new UWLevel(j + 1, lyRaw, lOOffsets[UWLevArkLayout.MapBlock(j)],
					lOOffsets[UWLevArkLayout.TextureMapBlock(j)], pOTextures);

				if (lOOffsets.Count > UWLevArkLayout.AutomapBlock(j))
					item.LoadAutomap(lyRaw, lOOffsets[UWLevArkLayout.AutomapBlock(j)]);

				// The animation list (UWAnimationOverlay).
				if (lOOffsets.Count > UWLevArkLayout.AnimationBlock(j))
					item.LoadAnimations(lyRaw, lOOffsets[UWLevArkLayout.AnimationBlock(j)]);

				lOLevels.Add(item);
			}

			pOWorld.Levels = lOLevels;

			for (int liLevel = 0; liLevel < UWLevArkLayout.MapNoteLevelCount; liLevel++)
			{
				int liBlock = UWLevArkLayout.MapNoteBlock(liLevel);

				pOWorld.SetMapNotes(liLevel, liBlock < lOOffsets.Count
					? UWLevel.ParseMapNotes(lyRaw, lOOffsets[liBlock], fGetBlockEnd(pOWorld, lOOffsets[liBlock]))
					: new List<UWLevel.MapNote>());
			}
		}

		/// <summary>Where does the block that starts at this position end? The file only lists
		/// starts, so the next start after it counts as the end - and for the last one the
		/// end of the file.</summary>
		private static int fGetBlockEnd(UWSessionWorld pOWorld, uint piOffset)
		{
			int liEnd = pOWorld.RawLevelData.Length;

			foreach (uint lyOther in pOWorld.BlockOffsets)
			{
				if (lyOther > piOffset && lyOther < liEnd)
					liEnd = (int)lyOther;
			}

			return liEnd;
		}

		// ------------------------------------------------- LEV.ARK

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
		public static bool SaveWorld(UWGameSession pOSession, string psTargetFolder = null, byte[] pyPlayerSlot = null, int piPlayerLevel = -1)
		{
			UWSessionWorld lOWorld = pOSession != null ? pOSession.World : null;

			if (lOWorld == null || lOWorld.RawLevelData == null || lOWorld.BlockOffsets == null)
				return false;

			byte[] lyRaw = lOWorld.RawLevelData;
			List<uint> lOOffsets = lOWorld.BlockOffsets;
			List<UWLevel> lOLevels = lOWorld.Levels;

			// Without a save game only into an explicitly named folder - that is the case of
			// a newly created character whose world still comes from the data folder.
			if (!lOWorld.IsSavegame && string.IsNullOrEmpty(psTargetFolder))
				return false;

			string lsFolder = string.IsNullOrEmpty(psTargetFolder) ? lOWorld.SavegamePath : psTargetFolder;
			string lsPath = Path.Combine(lsFolder, "LEV.ARK");

			if (!File.Exists(lsPath))
				return false;

			string lsBackup = lsPath + ".bak";

			if (!File.Exists(lsBackup))
				File.Copy(lsPath, lsBackup);

			int liCount = lOOffsets.Count;

			byte[][] lyBlocks = new byte[liCount][];
			Dictionary<UWObject, int>[] lOIndices = new Dictionary<UWObject, int>[UWLevArkLayout.LevelBlockCount];

			UWLevelWriter.Warnings.Clear();

			// The block numbers of the five kinds are UWLevArkLayout's (2026-10-12).
			for (int liBlock = 0; liBlock < liCount; liBlock++)
			{
				// THE LEVELS THEMSELVES - tiles and objects, i.e. the state of the world (since
				// 2026-09-10, see UWLevelWriter). The basis is the block as it last stood in
				// the file; only what the model knows is overwritten.
				int liMapLevel = liBlock - UWLevArkLayout.MapFirstBlock;

				if (lOLevels != null && liMapLevel < lOLevels.Count && liMapLevel < UWLevArkLayout.LevelBlockCount
					&& lOOffsets[liBlock] != 0
					&& lOOffsets[liBlock] + UWLevArkLayout.BlockSize <= lyRaw.Length)
				{
					byte[] lyOriginal = new byte[UWLevArkLayout.BlockSize];
					Array.Copy(lyRaw, (int)lOOffsets[liBlock], lyOriginal, 0, UWLevArkLayout.BlockSize);

					lyBlocks[liBlock] = UWLevelWriter.BuildBlock(lOLevels[liMapLevel], lyOriginal);
					lOIndices[liMapLevel] = UWLevelWriter.LastIndices;

					// Object 1 of the level, the player character (UWPlayerData.GetLevelPlayerSlot).
					if (liMapLevel == piPlayerLevel && pyPlayerSlot != null && pyPlayerSlot.Length == UWLevArkLayout.MobileRecordSize)
						Array.Copy(pyPlayerSlot, 0, lyBlocks[liBlock], UWLevArkLayout.PlayerSlotOffset, pyPlayerSlot.Length);

					continue;
				}

				// THE ANIMATION LIST follows its level: the entries with the numbers the writer just
				// gave the objects (until 2026-10-11 the block was copied unchanged, so a silver tree
				// planted here stood still in the original).
				int liAnimationLevel = liBlock - UWLevArkLayout.AnimationFirstBlock;

				if (liAnimationLevel >= 0 && liAnimationLevel < UWLevArkLayout.LevelBlockCount && lOLevels != null
					&& liAnimationLevel < lOLevels.Count && lOLevels[liAnimationLevel].Animations != null
					&& lOLevels[liAnimationLevel].Animations.IsLoaded)
				{
					Dictionary<UWObject, int> lOMap = lOIndices[liAnimationLevel];

					lyBlocks[liBlock] = lOLevels[liAnimationLevel].Animations.Serialize(
						pOObject => lOMap != null && lOMap.TryGetValue(pOObject, out int liIndex) ? liIndex : -1);
					continue;
				}

				int liNoteLevel = liBlock - UWLevArkLayout.MapNoteFirstBlock;

				if (liNoteLevel >= 0 && liNoteLevel < UWLevArkLayout.MapNoteLevelCount)
				{
					lyBlocks[liBlock] = BuildMapNoteBlock(lOWorld.GetMapNotes(liNoteLevel));
					continue;
				}

				// The automap comes from the level itself - that is where walking records which
				// tiles were entered.
				int liAutomapLevel = liBlock - UWLevArkLayout.AutomapFirstBlock;

				if (liAutomapLevel >= 0 && lOLevels != null && liAutomapLevel < lOLevels.Count
					&& lOLevels[liAutomapLevel].AutomapTiles != null)
				{
					lyBlocks[liBlock] = (byte[])lOLevels[liAutomapLevel].AutomapTiles.Clone();
					continue;
				}

				uint lyOffset = lOOffsets[liBlock];

				if (lyOffset == 0)
					continue;

				int liEnd = fGetBlockEnd(lOWorld, lyOffset);
				int liLength = liEnd - (int)lyOffset;

				if (liLength <= 0)
					continue;

				lyBlocks[liBlock] = new byte[liLength];
				Array.Copy(lyRaw, (int)lyOffset, lyBlocks[liBlock], 0, liLength);
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
				uint lyLeft = lOOffsets[piLeft];
				uint lyRight = lOOffsets[piRight];

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
			lOWorld.RawLevelData = lyOut;

			lOOffsets.Clear();

			for (int liBlock = 0; liBlock < liCount; liBlock++)
				lOOffsets.Add(UWBinary.Read32(lyOut, 2 + (liBlock * 4)));

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
		public static byte[] BuildMapNoteBlock(List<UWLevel.MapNote> pONotes)
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

				// A note read from the file with its text unchanged goes back byte for byte, the
				// original's leftovers after the text's zero included (UWLevel.MapNote.TextField,
				// 2026-10-23: without them a save the original wrote came back 16 bytes off).
				if (fFieldHolds(pONotes[liNote].TextField, lsText, liTextSize))
					Array.Copy(pONotes[liNote].TextField, 0, lyBlock, liAt, liTextSize);
				else
					for (int liChar = 0; liChar < lsText.Length && liChar < liTextSize - 1; liChar++)
						lyBlock[liAt + liChar] = (byte)lsText[liChar];

				lyBlock[liAt + 0x32] = (byte)(pONotes[liNote].X & 0xFF);
				lyBlock[liAt + 0x33] = (byte)((pONotes[liNote].X >> 8) & 0xFF);
				lyBlock[liAt + 0x34] = (byte)(pONotes[liNote].Y & 0xFF);
				lyBlock[liAt + 0x35] = (byte)((pONotes[liNote].Y >> 8) & 0xFF);
			}

			return lyBlock;
		}

		/// <summary>Whether the raw field still spells the text, up to its zero.</summary>
		private static bool fFieldHolds(byte[] pyField, string psText, int piTextSize)
		{
			if (pyField == null || pyField.Length != piTextSize || psText.Length >= piTextSize)
				return false;

			for (int liChar = 0; liChar < psText.Length; liChar++)
				if (pyField[liChar] != (byte)psText[liChar])
					return false;

			return pyField[psText.Length] == 0;
		}

		// ------------------------------------------------- A whole slot

		/// <summary>
		/// Writes a savegame slot from the session and a snapshot of what the host holds (position,
		/// the components' state). Returns null on success, otherwise an error message.
		///
		/// INTO AN EMPTY SLOT the loaded savegame is copied first and then overwritten - the same
		/// base as when saving into its own slot. A NEWLY CREATED CHARACTER has no savegame: then
		/// LEV.ARK from the data folder and the PLAYER.DAT from character creation are the base, and
		/// the slot becomes the loaded state. BACKUPS: the first write into a slot creates .bak
		/// copies. The slots are the original game's savegames.
		/// </summary>
		public static string Save(UWGameSession pOSession, string psSlotPath, UWSavegameStore.Snapshot pOSnapshot, string psDescription)
		{
			if (pOSession == null || pOSession.Data == null)
				return "No level loaded.";

			UWSessionWorld lOWorld = pOSession.World;
			UWPlayerData lOPlayer = pOSession.Player;
			bool lbNewGame = !lOWorld.IsSavegame;

			if (lbNewGame && (lOPlayer == null || !lOPlayer.IsNewCharacter))
				return "Saving only works from a loaded savegame or with a newly created character.";

			if (string.IsNullOrEmpty(psSlotPath))
				return "No savegame folder found.";

			try
			{
				Directory.CreateDirectory(psSlotPath);

				if (lbNewGame)
					fPrepareNewGame(pOSession, psSlotPath);
				else
					fCopyBase(lOWorld.SavegamePath, psSlotPath);
			}
			catch (Exception lOError)
			{
				return "Could not prepare the slot: " + lOError.Message;
			}

			UWLevel lOLevel = pOSnapshot.LevelIndex >= 0 && lOWorld.Levels != null && pOSnapshot.LevelIndex < lOWorld.Levels.Count
				? lOWorld.Levels[pOSnapshot.LevelIndex] : null;

			string lsError = UWPlayerData.WritePosition(psSlotPath,
				pOSnapshot.LevelIndex + 1,
				pOSnapshot.PositionX, pOSnapshot.PositionY, pOSnapshot.PositionZ, pOSnapshot.Heading,
				pOSnapshot.State,
				lOLevel != null ? lOLevel.Masterlist : null,
				lOPlayer != null ? lOPlayer.InventoryRecords : null);

			if (lsError != null)
				return lsError;

			// Object 1 of the level is the character itself, a second time - from the PLAYER.DAT just
			// written, which is how the original keeps the two identical.
			if (!SaveWorld(pOSession, psSlotPath, UWPlayerData.GetLevelPlayerSlot(psSlotPath), pOSnapshot.LevelIndex))
				return "Character saved, but not the map.";

			if (pOSession.ConversationMemory != null && pOSession.ConversationMemory.IsLoaded)
			{
				try
				{
					File.WriteAllBytes(Path.Combine(psSlotPath, "BGLOBALS.DAT"), pOSession.ConversationMemory.Serialize());
				}
				catch (Exception lOError)
				{
					return "Conversation memory not written: " + lOError.Message;
				}
			}

			try
			{
				File.WriteAllBytes(Path.Combine(psSlotPath, "DESC"), UWSavegameStore.EncodeDescription(psDescription));
			}
			catch (Exception lOError)
			{
				return "Name not written: " + lOError.Message;
			}

			// From now on this folder counts as the loaded save game - after the first save of a
			// newly created character that ran from the data folder until then.
			if (lbNewGame)
				AdoptSavegame(pOSession, psSlotPath);

			return null;
		}

		/// <summary>
		/// From now on this folder counts as the loaded save game - after the first save of a newly
		/// created character that ran from the data folder until then. Afterwards everything saves
		/// and loads as if the game had been started from there.
		/// </summary>
		public static void AdoptSavegame(UWGameSession pOSession, string psSavegamePath)
		{
			if (pOSession != null && !string.IsNullOrEmpty(psSavegamePath) && Directory.Exists(psSavegamePath))
				pOSession.World.SavegamePath = psSavegamePath;
		}

		/// <summary>
		/// The first state of a new character: LEV.ARK from the data folder (the levels are written
		/// over it right afterwards) and the PLAYER.DAT from the creation block, encrypted like a
		/// savegame. Whatever was in the slot is backed up as usual.
		/// </summary>
		private static void fPrepareNewGame(UWGameSession pOSession, string psTarget)
		{
			foreach (string lsFile in msBaseFiles)
			{
				string lsTo = Path.Combine(psTarget, lsFile);

				if (File.Exists(lsTo) && !File.Exists(lsTo + ".bak"))
					File.Copy(lsTo, lsTo + ".bak");
			}

			File.Copy(Path.Combine(pOSession.Data.DataPath, "LEV.ARK"), Path.Combine(psTarget, "LEV.ARK"), true);

			File.WriteAllBytes(Path.Combine(psTarget, "PLAYER.DAT"),
				UWPlayerData.Encrypt(pOSession.Player.PlainBytes, NewGameKey));
		}

		/// <summary>Brings the base into the target slot if it is not itself the loaded one. An
		/// existing state there is replaced - it gets overwritten anyway.</summary>
		private static void fCopyBase(string psSource, string psTarget)
		{
			if (string.Equals(Path.GetFullPath(psSource).TrimEnd('\\', '/'),
				Path.GetFullPath(psTarget).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
				return;

			foreach (string lsFile in msBaseFiles)
			{
				string lsFrom = Path.Combine(psSource, lsFile);

				if (!File.Exists(lsFrom))
					continue;

				string lsTo = Path.Combine(psTarget, lsFile);

				// Back up before replacing - exactly as when writing itself.
				if (File.Exists(lsTo) && !File.Exists(lsTo + ".bak"))
					File.Copy(lsTo, lsTo + ".bak");

				File.Copy(lsFrom, lsTo, true);
			}
		}
	}
}
