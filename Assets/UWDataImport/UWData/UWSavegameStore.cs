using System.Collections.Generic;

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
	///
	/// SINCE 2026-10-14 (cleanup step 2.9) the writing itself is UWSaveCodec.Save, on the session;
	/// this class keeps the Snapshot the host fills, the description's encoding and Write as the
	/// entry for callers that hold a DataImport.
	/// </summary>
	public static class UWSavegameStore
	{
		public const int MaxDescriptionLength = 30;

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

		/// <summary>Returns null on success, otherwise an error message. Since 2026-10-14 (cleanup
		/// step 2.9) the write is UWSaveCodec.Save on the game's session.</summary>
		public static string Write(DataImport pOData, string psSlotPath, Snapshot pOSnapshot, string psDescription)
		{
			if (pOData == null)
				return "No level loaded.";

			return UWSaveCodec.Save(pOData.Session, psSlotPath, pOSnapshot, psDescription);
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
