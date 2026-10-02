using System;
using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Writes a savegame slot from a snapshot of the game (P2 of the engine separation,
	/// 2026-09-17). The host (UWSavegameWriter) reads the camera position and the components into a
	/// Snapshot; everything that touches files lives here.
	///
	/// WHAT GOES IN
	///
	///   DESC           the typed-in name, raw ASCII text
	///   PLAYER.DAT     position, heading, level, the SavedState (hunger, fatigue, clock, vitality,
	///                  mana, spells, quest flags, game variables, runes, attributes, skills,
	///                  experience, inventory including contents)
	///   LEV.ARK        the levels (tiles and objects), map notes and automap
	///   BGLOBALS.DAT   the memory of the conversations
	///
	/// INTO AN EMPTY SLOT the loaded savegame is copied first and then overwritten - the same base as
	/// when saving into its own slot. A NEWLY CREATED CHARACTER has no savegame: then LEV.ARK from the
	/// data folder and the PLAYER.DAT from character creation are the base, and the slot becomes the
	/// loaded state.
	///
	/// BACKUPS: the first write into a slot creates .bak copies. The slots are the original game's
	/// savegames.
	/// </summary>
	public static class UWSavegameStore
	{
		public const int MaxDescriptionLength = 30;

		private static readonly string[] msBaseFiles = { "PLAYER.DAT", "LEV.ARK", "BGLOBALS.DAT" };

		/// <summary>Any key byte - the encryption is an XOR, any value works.</summary>
		private const byte NewGameKey = 0x2A;

		/// <summary>Everything the host collects before writing.</summary>
		public sealed class Snapshot
		{
			/// <summary>Zero-based index of the level the character is on.</summary>
			public int LevelIndex;

			/// <summary>Tile in the upper byte, position within the tile (0-255) in the lower one.</summary>
			public int PositionX;

			public int PositionY;

			/// <summary>Eighths of a zpos step, from the floor below the character.</summary>
			public int PositionZ;

			/// <summary>Full circle in 16 bits, 0 north, clockwise.</summary>
			public int Heading;

			public UWPlayerData.SavedState State;
		}

		/// <summary>Returns null on success, otherwise an error message.</summary>
		public static string Write(DataImport pOData, string psSlotPath, Snapshot pOSnapshot, string psDescription)
		{
			if (pOData == null)
				return "No level loaded.";

			bool lbNewGame = !pOData.IsSavegame;

			if (lbNewGame && (pOData.InitialPlayer == null || !pOData.InitialPlayer.IsNewCharacter))
				return "Saving only works from a loaded savegame or with a newly created character.";

			if (string.IsNullOrEmpty(psSlotPath))
				return "No savegame folder found.";

			try
			{
				Directory.CreateDirectory(psSlotPath);

				if (lbNewGame)
					fPrepareNewGame(pOData, psSlotPath);
				else
					fCopyBase(pOData.SavegamePath, psSlotPath);
			}
			catch (Exception lOError)
			{
				return "Could not prepare the slot: " + lOError.Message;
			}

			UWLevel lOLevel = pOSnapshot.LevelIndex >= 0 && pOData.Levels != null && pOSnapshot.LevelIndex < pOData.Levels.Count
				? pOData.Levels[pOSnapshot.LevelIndex] : null;

			string lsError = UWPlayerData.WritePosition(psSlotPath,
				pOSnapshot.LevelIndex + 1,
				pOSnapshot.PositionX, pOSnapshot.PositionY, pOSnapshot.PositionZ, pOSnapshot.Heading,
				pOSnapshot.State,
				lOLevel != null ? lOLevel.Masterlist : null,
				pOData.InitialPlayer != null ? pOData.InitialPlayer.InventoryRecords : null);

			if (lsError != null)
				return lsError;

			// Object 1 of the level is the character itself, a second time - from the PLAYER.DAT just
			// written, which is how the original keeps the two identical.
			if (!pOData.SaveMapData(psSlotPath, UWPlayerData.GetLevelPlayerSlot(psSlotPath), pOSnapshot.LevelIndex))
				return "Character saved, but not the map.";

			if (pOData.ConversationGlobals != null && pOData.ConversationGlobals.IsLoaded)
			{
				try
				{
					File.WriteAllBytes(Path.Combine(psSlotPath, "BGLOBALS.DAT"), pOData.ConversationGlobals.Serialize());
				}
				catch (Exception lOError)
				{
					return "Conversation memory not written: " + lOError.Message;
				}
			}

			try
			{
				File.WriteAllBytes(Path.Combine(psSlotPath, "DESC"), EncodeDescription(psDescription));
			}
			catch (Exception lOError)
			{
				return "Name not written: " + lOError.Message;
			}

			if (lbNewGame)
				pOData.AdoptSavegame(psSlotPath);

			return null;
		}

		/// <summary>
		/// The first state of a new character: LEV.ARK from the data folder (the levels are written
		/// over it right afterwards) and the PLAYER.DAT from the creation block, encrypted like a
		/// savegame. Whatever was in the slot is backed up as usual.
		/// </summary>
		private static void fPrepareNewGame(DataImport pOData, string psTarget)
		{
			foreach (string lsFile in msBaseFiles)
			{
				string lsTo = Path.Combine(psTarget, lsFile);

				if (File.Exists(lsTo) && !File.Exists(lsTo + ".bak"))
					File.Copy(lsTo, lsTo + ".bak");
			}

			File.Copy(Path.Combine(pOData.DataPath, "LEV.ARK"), Path.Combine(psTarget, "LEV.ARK"), true);

			File.WriteAllBytes(Path.Combine(psTarget, "PLAYER.DAT"),
				UWPlayerData.Encrypt(pOData.InitialPlayer.PlainBytes, NewGameKey));
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

		/// <summary>As in the original: printable ASCII only, at most thirty characters, no
		/// terminator.</summary>
		public static byte[] EncodeDescription(string psDescription)
		{
			List<byte> lyResult = new List<byte>();

			if (!string.IsNullOrEmpty(psDescription))
			{
				foreach (char lcChar in psDescription)
				{
					if (lyResult.Count >= MaxDescriptionLength)
						break;

					if (lcChar >= 0x20 && lcChar <= 0x7E)
						lyResult.Add((byte)lcChar);
				}
			}

			return lyResult.ToArray();
		}
	}
}
