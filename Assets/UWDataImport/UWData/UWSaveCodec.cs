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

			// The second game's world (UW2 plan phase 2 step 2); its character, PLAYER.DAT's record of
			// 0x37D bytes, is not UWPlayerData's layout and comes with phase 2 step 6 - until then the
			// session has none.
			if (pOData.Profile.Game == UWGame.Uw2)
			{
				fLoadLevelsUw2(lOWorld, Path.Combine(lsFolder, "LEV.ARK"), pOData.Textures, pOData.Profile.LevArk);

				// A save's PLAYER.DAT (step 6); DATA's fits neither game's layout (Underworld Exhumed
				// docs/FORMATS.md "PLAYER.DAT").
				string lsPlayer = Path.Combine(lsFolder, PlayerFileName);

				if (lOWorld.IsSavegame && File.Exists(lsPlayer))
					lOSession.PlayerFileUw2 = UWPlayerFile.Read(File.ReadAllBytes(lsPlayer), UWGame.Uw2);

				return lOSession;
			}

			lOSession.Player = new UWPlayerData(lsFolder);
			lOSession.Player.AssignTextures(pOData.Textures);
			fLoadLevels(lOWorld, Path.Combine(lsFolder, "LEV.ARK"), pOData.Textures, pOData.Profile.LevArk);

			return lOSession;
		}

		/// <summary>The five kinds of blocks by the game's numbers (UWLevArkFile; the reasons for 27
		/// and for ninety-nine note blocks stand there); the header through UWLevArkCodec.</summary>
		private static void fLoadLevels(UWSessionWorld pOWorld, string psLevelFullPath, UWTextures pOTextures, UWLevArkFile pOFile)
		{
			byte[] lyRaw = File.ReadAllBytes(psLevelFullPath);
			List<uint> lOOffsets = UWLevArkCodec.ReadBlockOffsets(lyRaw);
			List<UWLevel> lOLevels = new List<UWLevel>();

			pOWorld.RawLevelData = lyRaw;
			pOWorld.BlockOffsets = lOOffsets;

			for (int j = 0; j < pOFile.LevelCount; j++)
			{
				UWLevel item = new UWLevel(j + 1, lyRaw, lOOffsets[pOFile.MapBlock(j)],
					lOOffsets[pOFile.TextureMapBlock(j)], pOTextures);

				if (lOOffsets.Count > pOFile.AutomapBlock(j))
					item.LoadAutomap(lyRaw, lOOffsets[pOFile.AutomapBlock(j)]);

				// The animation list (UWAnimationOverlay).
				if (pOFile.AnimationBlock(j) >= 0 && lOOffsets.Count > pOFile.AnimationBlock(j))
					item.LoadAnimations(lyRaw, lOOffsets[pOFile.AnimationBlock(j)]);

				lOLevels.Add(item);
			}

			pOWorld.Levels = lOLevels;

			for (int liLevel = 0; liLevel < pOFile.MapNoteLevelCount; liLevel++)
			{
				int liBlock = pOFile.MapNoteBlock(liLevel);

				pOWorld.SetMapNotes(liLevel, liBlock < lOOffsets.Count
					? UWLevel.ParseMapNotes(lyRaw, lOOffsets[liBlock], fGetBlockEnd(pOWorld, lOOffsets[liBlock]))
					: new List<UWLevel.MapNote>());
			}
		}

		/// <summary>
		/// THE SECOND GAME'S LEV.ARK (UW2 plan phase 2 step 2, 2026-10-29): read through UWArk, its
		/// blocks decoded as they are asked for. Each of the 80 levels with a map block becomes a
		/// UWLevel (with its texture block, its overlays and timers inside the map block); a level
		/// without one stays null - the shipped file has 42 (Underworld Exhumed docs/FORMATS.md
		/// "LEV.ARK"). Automaps and notes by the same numbers; the notes are the first game's records,
		/// kept for every page. The shipped file's automaps of levels 74 and 79, which have no map block,
		/// stay in the archive only.
		/// RawLevelData keeps the file as read, BlockOffsets stays empty: the blocks are UWArk's.
		/// </summary>
		private static void fLoadLevelsUw2(UWSessionWorld pOWorld, string psLevelFullPath, UWTextures pOTextures, UWLevArkFile pOFile)
		{
			byte[] lyRaw = File.ReadAllBytes(psLevelFullPath);
			UWArk lOArk = UWArk.Read(lyRaw, UWArk.LayoutEnum.Uw2);
			List<UWLevel> lOLevels = new List<UWLevel>();

			pOWorld.RawLevelData = lyRaw;
			pOWorld.BlockOffsets = new List<uint>();
			pOWorld.Ark = lOArk;

			for (int liLevel = 0; liLevel < pOFile.LevelCount; liLevel++)
			{
				byte[] lyBlock = fArkBlock(lOArk, pOFile.MapBlock(liLevel));

				if (lyBlock.Length < pOFile.BlockSize)
				{
					lOLevels.Add(null);

					continue;
				}

				UWLevel lOLevel = new UWLevel(liLevel + 1, lyBlock, fArkBlock(lOArk, pOFile.TextureMapBlock(liLevel)), pOTextures, pOFile);

				lOLevel.LoadAutomap(fArkBlock(lOArk, pOFile.AutomapBlock(liLevel)));
				lOLevels.Add(lOLevel);
			}

			pOWorld.Levels = lOLevels;

			for (int liLevel = 0; liLevel < pOFile.MapNoteLevelCount; liLevel++)
			{
				byte[] lyNotes = fArkBlock(lOArk, pOFile.MapNoteBlock(liLevel));

				pOWorld.SetMapNotes(liLevel, UWLevel.ParseMapNoteBlock(lyNotes));
			}
		}

		public const string PlayerFileName = "PLAYER.DAT";

		/// <summary>The second game's schedules, copied with a save (its rules come in phase 4).</summary>
		public const string ScheduleFileName = "SCD.ARK";

		/// <summary>
		/// A NEW SECOND-GAME CHARACTER IN THE SESSION (UW2 plan phase 3 step 5): its PLAYER.DAT built from
		/// the creation's choices (UWPlayerRecordUw2.NewCharacter). The session must hold a world begun
		/// without a save.
		/// </summary>
		public static void StartNewCharacterUw2(UWGameSession pOSession, UWPlayerData.NewCharacter pOCharacter)
		{
			pOSession.PlayerFileUw2 = UWPlayerRecordUw2.NewCharacter(pOCharacter);
		}

		/// <summary>
		/// THE SECOND GAME'S WALK SAVED (UW2 plan phase 2 step 6, 2026-10-29) into psSlotPath as the
		/// original's save holds a game (Underworld Exhumed docs/FORMATS.md): LEV.ARK through UWArk - every
		/// level's block written back from the session by UWLevelWriter (the shared 0x7C08 bytes) with its
		/// animation overlays and timers inside it (Anim_Save zeroes what follows the count, MAP.C:169),
		/// each automap and each page's notes, kept or appended by put_arc's rules and compressed where that
		/// is shorter (decision 4); PLAYER.DAT - the loaded save's, else a walker's (UWPlayerRecordUw2.NewWalker)
		/// - with the position, facing and level written in; BGLOBALS.DAT from the conversation memory;
		/// SCD.ARK copied from where the world came from; DESC. The world then counts as loaded from there.
		/// Returns null, or what failed.
		/// </summary>
		public static string SaveUw2(UWGameSession pOSession, string psSlotPath, string psDescription, int piLevelIndex,
			int piPositionX, int piPositionY, int piZPosition, int piHeading)
		{
			UWSessionWorld lOWorld = pOSession.World;
			UWLevArkFile lOFile = UWLevArkFile.Uw2;

			if (lOWorld.Ark == null || lOWorld.Levels == null)
				return "no world to save";

			try
			{
				Directory.CreateDirectory(psSlotPath);

				string lsFrom = lOWorld.IsSavegame ? lOWorld.SavegamePath : pOSession.Data.DataPath;
				int liAutomapFlags = fTemplateFlags(lOWorld.Ark, lOFile.AutomapBlock(0), lOFile.LevelCount);
				int liNoteFlags = fTemplateFlags(lOWorld.Ark, lOFile.MapNoteBlock(0), lOFile.MapNoteLevelCount);

				for (int liLevel = 0; liLevel < lOFile.LevelCount; liLevel++)
				{
					UWLevel lOLevel = lOWorld.Levels[liLevel];

					if (lOLevel == null)
						continue;

					byte[] lyBlock = UWLevelWriter.BuildBlock(lOLevel, lOWorld.Ark.ReadBlock(lOFile.MapBlock(liLevel)));
					Dictionary<UWObject, int> lOIndices = UWLevelWriter.LastIndices;

					fWriteOverlaysAndTimers(lyBlock, lOLevel, lOIndices, lOFile);
					lOWorld.Ark.WriteBlock(lOFile.MapBlock(liLevel), lyBlock);

					if (lOLevel.AutomapTiles != null)
						lOWorld.Ark.WriteBlock(lOFile.AutomapBlock(liLevel), (byte[])lOLevel.AutomapTiles.Clone(), liAutomapFlags);
				}

				for (int liPage = 0; liPage < lOFile.MapNoteLevelCount; liPage++)
				{
					List<UWLevel.MapNote> lONotes = lOWorld.GetMapNotes(liPage);
					int liBlock = lOFile.MapNoteBlock(liPage);

					if ((lONotes != null && lONotes.Count > 0) || lOWorld.Ark.StoredLength(liBlock) > 0)
						lOWorld.Ark.WriteBlock(liBlock, BuildMapNoteBlock(lONotes ?? new List<UWLevel.MapNote>()), liNoteFlags);
				}

				byte[] lyLevArk = lOWorld.Ark.ToBytes();

				File.WriteAllBytes(Path.Combine(psSlotPath, "LEV.ARK"), lyLevArk);

				UWPlayerFile lOPlayer = pOSession.PlayerFileUw2 ?? UWPlayerRecordUw2.NewWalker();

				UWPlayerRecordUw2.WritePosition(lOPlayer, piPositionX, piPositionY, piZPosition, piHeading, piLevelIndex + 1);
				File.WriteAllBytes(Path.Combine(psSlotPath, PlayerFileName), lOPlayer.ToBytes());
				pOSession.PlayerFileUw2 = lOPlayer;

				if (pOSession.ConversationMemory != null)
					File.WriteAllBytes(Path.Combine(psSlotPath, "BGLOBALS.DAT"), pOSession.ConversationMemory.Serialize());

				string lsSchedule = Path.Combine(lsFrom, ScheduleFileName);

				if (File.Exists(lsSchedule) && !string.Equals(Path.GetFullPath(lsFrom), Path.GetFullPath(psSlotPath), System.StringComparison.OrdinalIgnoreCase))
					File.Copy(lsSchedule, Path.Combine(psSlotPath, ScheduleFileName), true);

				File.WriteAllBytes(Path.Combine(psSlotPath, "DESC"), UWSavegameStore.EncodeDescription(psDescription));

				lOWorld.RawLevelData = lyLevArk;
				lOWorld.SavegamePath = psSlotPath;

				return null;
			}
			catch (System.Exception lOError)
			{
				return lOError.Message;
			}
		}

		/// <summary>The flags a block of a kind is written with when it is new: those of the first present
		/// block of the kind (the shipped file has automaps and notes of three levels), else compressed with
		/// room (UWArk's flags 1 and 4).</summary>
		private static int fTemplateFlags(UWArk pOArk, int piFirstBlock, int piCount)
		{
			for (int liBlock = piFirstBlock; liBlock < piFirstBlock + piCount && liBlock < pOArk.BlockCount; liBlock++)
			{
				if (pOArk.IsPresent(liBlock))
					return pOArk.FlagsOf(liBlock) & (UWArk.CompressFlag | UWArk.RoomFlag);
			}

			return UWArk.CompressFlag | UWArk.RoomFlag;
		}

		/// <summary>The overlays and the timers into the second game's level block (after the shared part):
		/// the overlays as UWAnimationOverlay serialises them with the writer's object numbers, the timers
		/// up to the count and zero after it (Anim_Save, Underworld Exhumed uw2 map/MAP.C:169).</summary>
		private static void fWriteOverlaysAndTimers(byte[] pyBlock, UWLevel pOLevel, Dictionary<UWObject, int> pOIndices, UWLevArkFile pOFile)
		{
			if (pOLevel.Animations != null && pOLevel.Animations.IsLoaded)
			{
				byte[] lyOverlays = pOLevel.Animations.Serialize(pOObject => pOIndices != null && pOIndices.TryGetValue(pOObject, out int liIndex) ? liIndex : -1);

				System.Array.Copy(lyOverlays, 0, pyBlock, pOFile.OverlayOffset, System.Math.Min(lyOverlays.Length, UWAnimationOverlay.BlockLength));
			}

			for (int liAt = 0; liAt < pOFile.TimerCount; liAt++)
				UWBinary.Write16(pyBlock, pOFile.TimerOffset + (2 * liAt), liAt < pOLevel.Timers.Count ? pOLevel.Timers[liAt] : 0);
		}

		/// <summary>A block of the archive, empty when it is beyond its blocks or absent.</summary>
		private static byte[] fArkBlock(UWArk pOArk, int piBlock)
		{
			return piBlock >= 0 && piBlock < pOArk.BlockCount ? pOArk.ReadBlock(piBlock) : new byte[0];
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
			UWLevArkFile lOFile = pOSession.Game.LevArk;

			byte[][] lyBlocks = new byte[liCount][];
			Dictionary<UWObject, int>[] lOIndices = new Dictionary<UWObject, int>[lOFile.LevelCount];

			UWLevelWriter.Warnings.Clear();

			// The block numbers of the five kinds are the game's (UWLevArkLayout's from 2026-10-12,
			// UWLevArkFile's since UW2 plan phase 1 step 2).
			for (int liBlock = 0; liBlock < liCount; liBlock++)
			{
				// THE LEVELS THEMSELVES - tiles and objects, i.e. the state of the world (since
				// 2026-09-10, see UWLevelWriter). The basis is the block as it last stood in
				// the file; only what the model knows is overwritten.
				int liMapLevel = lOFile.MapLevelOf(liBlock);

				if (lOLevels != null && liMapLevel >= 0 && liMapLevel < lOLevels.Count
					&& lOOffsets[liBlock] != 0
					&& lOOffsets[liBlock] + lOFile.BlockSize <= lyRaw.Length)
				{
					byte[] lyOriginal = new byte[lOFile.BlockSize];
					Array.Copy(lyRaw, (int)lOOffsets[liBlock], lyOriginal, 0, lOFile.BlockSize);

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
				int liAnimationLevel = lOFile.AnimationLevelOf(liBlock);

				if (liAnimationLevel >= 0 && lOLevels != null
					&& liAnimationLevel < lOLevels.Count && lOLevels[liAnimationLevel].Animations != null
					&& lOLevels[liAnimationLevel].Animations.IsLoaded)
				{
					Dictionary<UWObject, int> lOMap = lOIndices[liAnimationLevel];

					lyBlocks[liBlock] = lOLevels[liAnimationLevel].Animations.Serialize(
						pOObject => lOMap != null && lOMap.TryGetValue(pOObject, out int liIndex) ? liIndex : -1);
					continue;
				}

				int liNoteLevel = lOFile.MapNoteLevelOf(liBlock);

				if (liNoteLevel >= 0)
				{
					lyBlocks[liBlock] = BuildMapNoteBlock(lOWorld.GetMapNotes(liNoteLevel));
					continue;
				}

				// The automap comes from the level itself - that is where walking records which
				// tiles were entered.
				int liAutomapLevel = lOFile.AutomapLevelOf(liBlock);

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
				UWPlayerData.Encrypt(pOSession.Player.PlainBytes, UWPlayerData.KeyFor(pOSession.Player.PlainBytes)));
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
