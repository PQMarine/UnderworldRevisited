using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A SAVE GAME'S PLAYER.DAT OF EITHER GAME, AS A FILE (Docs/UW2-PLAN.md phase 1 step 3, 2026-10-29):
	/// the key byte, the player record deciphered, the plain count word and the inventory workspace
	/// (Underworld Exhumed, docs/FORMATS.md "PLAYER.DAT"; INVSAVE.C:94 and :243, PLAYDATA.C:86 and
	/// :101). Read and written back byte for byte. What the record's fields mean is UWPlayerData's for
	/// the first game; the two records agree to 0x5D (name, attributes, skills, vitals, level, runes,
	/// weight, experience, skill points, position, heading, level), and the second game's tail comes
	/// with its rules.
	///
	/// THE FILE: the key (the name's first letter xor 0xAA), the record enciphered (0xD2 bytes in the
	/// first game, 0x37D in the second), a word - the saved objects plus one - and the workspace of
	/// that many times 8 plus 0x5B bytes, plain: the player object (0x1B), the object on the cursor
	/// (8), 0x1C slot words (0 to 0x12 used), then the saved objects from number 1, 8 bytes each.
	///
	/// THE CIPHERS, each its own inverse, over blocks of 0x50 bytes with the chain restarting in each:
	/// the first game xors byte i with key + 3 (i + 1) (UW1 MISCUTIL.C:268; UWPlayerData.Decrypt does
	/// the same); the second builds a table of 0x50 from the key and xors byte i with the table plus the
	/// previous plain and enciphered bytes (MISCUTIL.C:281-297: a block shorter than 2 bytes stays as it
	/// is).
	/// </summary>
	public sealed class UWPlayerFile
	{
		public const int Uw1RecordSize = 0xD2;

		public const int Uw2RecordSize = 0x37D;

		public const int CipherBlock = 0x50;

		/// <summary>The key byte is the name's first letter xor this.</summary>
		public const int KeyXor = 0xAA;

		public const int WorkspaceHeaderSize = 0x5B;

		public const int PlayerObjectSize = 0x1B;

		public const int CursorObjectOffset = 0x1B;

		public const int SlotsOffset = 0x23;

		public const int SlotCount = 0x1C;

		/// <summary>Slots 0 to 0x12 are used; the words after them hold whatever was there.</summary>
		public const int UsedSlotCount = 0x13;

		public const int SavedObjectSize = 8;

		public UWGame Game { get; private set; }

		public byte Key { get; private set; }

		/// <summary>The player record, deciphered.</summary>
		public byte[] Record { get; private set; }

		/// <summary>The saved objects plus one, as stored.</summary>
		public int CountWord { get; private set; }

		public byte[] Workspace { get; private set; }

		/// <summary>What follows the workspace in the file; empty in every save measured.</summary>
		public byte[] Trailing { get; private set; }

		public static int RecordSize(UWGame peGame) => peGame == UWGame.Uw1 ? Uw1RecordSize : Uw2RecordSize;

		/// <summary>A new inventory workspace and its count word (UWInventoryWorkspace.WriteInto, UW2 plan
		/// phase 3 step 3).</summary>
		public void SetWorkspace(int piCountWord, byte[] pyWorkspace)
		{
			CountWord = piCountWord;
			Workspace = pyWorkspace ?? new byte[0];
		}

		/// <summary>A save game's PLAYER.DAT; null when it is shorter than the key, the record and the
		/// count word.</summary>
		public static UWPlayerFile Read(byte[] pyFile, UWGame peGame)
		{
			int liRecord = RecordSize(peGame);

			if (pyFile == null || pyFile.Length < 1 + liRecord + 2)
				return null;

			UWPlayerFile lOFile = new UWPlayerFile { Game = peGame, Key = pyFile[0] };
			byte[] lyRecord = new byte[liRecord];

			Array.Copy(pyFile, 1, lyRecord, 0, liRecord);
			lOFile.Record = Cipher(lyRecord, lOFile.Key, peGame);
			lOFile.CountWord = UWBinary.ReadU16(pyFile, 1 + liRecord);

			int liStart = 1 + liRecord + 2;
			int liWorkspace = Math.Min(pyFile.Length - liStart, WorkspaceHeaderSize + (SavedObjectSize * lOFile.CountWord));

			lOFile.Workspace = new byte[liWorkspace];
			Array.Copy(pyFile, liStart, lOFile.Workspace, 0, liWorkspace);
			lOFile.Trailing = new byte[pyFile.Length - liStart - liWorkspace];
			Array.Copy(pyFile, liStart + liWorkspace, lOFile.Trailing, 0, lOFile.Trailing.Length);

			return lOFile;
		}

		/// <summary>A file made from its parts (UW2 plan phase 2 step 6): the key is the name's first letter
		/// xor 0xAA, as the game writes it; nothing trails the workspace.</summary>
		public static UWPlayerFile Create(UWGame peGame, byte[] pyRecord, int piCountWord, byte[] pyWorkspace)
		{
			return new UWPlayerFile
			{
				Game = peGame,
				Key = (byte)(pyRecord[0] ^ KeyXor),
				Record = pyRecord,
				CountWord = piCountWord,
				Workspace = pyWorkspace,
				Trailing = new byte[0]
			};
		}

		/// <summary>The file again: the key, the record enciphered with it, the count word, the workspace.</summary>
		public byte[] ToBytes()
		{
			byte[] lyFile = new byte[1 + Record.Length + 2 + Workspace.Length + Trailing.Length];

			lyFile[0] = Key;
			Cipher(Record, Key, Game).CopyTo(lyFile, 1);
			lyFile[1 + Record.Length] = (byte)CountWord;
			lyFile[2 + Record.Length] = (byte)(CountWord >> 8);
			Workspace.CopyTo(lyFile, 3 + Record.Length);
			Trailing.CopyTo(lyFile, 3 + Record.Length + Workspace.Length);

			return lyFile;
		}

		/// <summary>The name at the record's start, up to its zero.</summary>
		public string Name
		{
			get
			{
				int liEnd = Array.IndexOf(Record, (byte)0);

				return System.Text.Encoding.ASCII.GetString(Record, 0, liEnd < 0 || liEnd > 0x1E ? 0x1E : liEnd);
			}
		}

		/// <summary>Whether the key is the name's first letter xor 0xAA, as the game writes it.</summary>
		public bool KeyFitsName => Record.Length > 0 && Key == (byte)(Record[0] ^ KeyXor);

		public int SavedObjectCount => Math.Max(0, CountWord - 1);

		/// <summary>The first object of the inventory: the player object's link field.</summary>
		public int InventoryHead => Workspace.Length >= 8 ? UWBinary.ReadU16(Workspace, 6) >> 6 : 0;

		/// <summary>An inventory slot's object number (0 empty), bits 6-15 as the first game's records.</summary>
		public int Slot(int piSlot) => Workspace.Length >= SlotsOffset + (2 * (piSlot + 1)) ? UWBinary.ReadU16(Workspace, SlotsOffset + (2 * piSlot)) >> 6 : 0;

		/// <summary>The record enciphered or deciphered with the game's cipher (each its own inverse).</summary>
		public static byte[] Cipher(byte[] pyData, byte pyKey, UWGame peGame)
		{
			byte[] lyOut = new byte[pyData.Length];

			if (peGame == UWGame.Uw1)
			{
				for (int liAt = 0; liAt < pyData.Length; liAt++)
					lyOut[liAt] = (byte)(pyData[liAt] ^ (byte)(pyKey + (3 * ((liAt % CipherBlock) + 1))));

				return lyOut;
			}

			byte[] lyTable = fTable(pyKey);

			for (int liBlock = 0; liBlock < pyData.Length; liBlock += CipherBlock)
			{
				int liLength = Math.Min(CipherBlock, pyData.Length - liBlock);

				if (liLength < 2)
				{
					Array.Copy(pyData, liBlock, lyOut, liBlock, liLength);

					continue;
				}

				lyOut[liBlock] = (byte)(pyData[liBlock] ^ lyTable[0]);

				for (int liAt = 1; liAt < liLength; liAt++)
					lyOut[liBlock + liAt] = (byte)(pyData[liBlock + liAt] ^ (byte)(lyTable[liAt] + lyOut[liBlock + liAt - 1] + pyData[liBlock + liAt - 1]));
			}

			return lyOut;
		}

		/// <summary>The second game's table of 0x50 bytes from the key, in the original's five passes.</summary>
		private static byte[] fTable(byte pyKey)
		{
			byte[] lyTable = new byte[CipherBlock];
			byte lyAt = (byte)(pyKey + 0x71);

			for (int liAt = 0x3C; liAt < CipherBlock; liAt++)
			{
				lyAt = (byte)(lyAt + liAt + 2);
				lyTable[liAt] = lyAt;
			}

			for (int liAt = 0; liAt < CipherBlock; liAt++)
			{
				lyAt = (byte)(lyAt + 6);
				lyTable[liAt] = lyAt;
			}

			for (int liAt = 0; liAt < 0x10; liAt++)
			{
				lyAt = (byte)(lyAt + 7);
				lyTable[5 * liAt] = lyAt;
			}

			for (int liAt = 0; liAt < 4; liAt++)
			{
				lyAt = (byte)(lyAt + 0x29);
				lyTable[12 * liAt] = lyAt;
			}

			for (int liAt = 0; liAt < 11; liAt++)
			{
				lyAt = (byte)(lyAt + 0x49);
				lyTable[7 * liAt] = lyAt;
			}

			return lyTable;
		}
	}
}
