using System;
using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ARCHIVES OF BOTH GAMES (Docs/UW2-PLAN.md section 4.2, phase 1 step 1, 2026-10-28): LEV.ARK,
	/// CNV.ARK, and the second game's BYT.ARK and SCD.ARK, read and written whole, block by block.
	///
	/// THE FIRST GAME'S LAYOUT: a word n, then n longs of block offsets (0 for an absent block); a
	/// block runs to the next block's start or the file's end. Blocks are stored plain.
	///
	/// THE SECOND GAME'S LAYOUT (Underworld Exhumed, docs/FORMATS.md "Archives"; sys/ARC.C): a word n,
	/// a long the game never uses, then n longs each of the offsets, the flags (bit 0 compress when
	/// writing, bit 1 stored compressed, bit 2 keep room), the stored lengths and the room on disk. A
	/// compressed block is a long, the length it decodes to, and an LZSS stream (UWLzss).
	///
	/// WRITING follows put_arc (ARC.C:203): a block flagged for compression is compressed and kept so
	/// only when the stream is shorter than the data (ac_shrink_disk, CompressLZW_disk); it keeps its
	/// room when it still fits (with bit 2 up to the room, else exactly), and grows to its length plus
	/// 15% with bit 2 otherwise. The file is written whole, each block in its room - after it what lay
	/// there in the file read (put_arc overwrites a block in place and leaves the rest), zeros in a fresh
	/// room - in the order the blocks lay in the file read - a new block, and one that outgrew its room, at
	/// the end, as put_arc appends them (a save game's LEV.ARK whose map notes came later lies so). A
	/// block not written since it was read keeps its bytes as they were, so a read and a write give
	/// the file back byte for byte; Okumura's encoder (UWLzss.Encode) gives the shipped streams back
	/// byte for byte too (the self-check holds both).
	/// </summary>
	public sealed class UWArk
	{
		public enum LayoutEnum
		{
			/// <summary>Offsets only.</summary>
			Uw1 = 1,

			/// <summary>Offsets, flags, lengths and room; LZSS.</summary>
			Uw2 = 2
		}

		/// <summary>Flag bit 0: compress the block when writing it.</summary>
		public const int CompressFlag = 1;

		/// <summary>Flag bit 1: the block is stored compressed.</summary>
		public const int CompressedFlag = 2;

		/// <summary>Flag bit 2: the block keeps room to grow into.</summary>
		public const int RoomFlag = 4;

		/// <summary>The room a growing block with RoomFlag gets beyond its length, in percent (ARC.C:290).</summary>
		public const int RoomPercent = 15;

		/// <summary>The bytes of the second game's compressed block's length prefix.</summary>
		public const int LengthPrefixSize = 4;

		private sealed class Block
		{
			public bool Present;

			public int Flags;

			/// <summary>The bytes allotted on disk (the second game's).</summary>
			public int Room;

			/// <summary>The bytes on disk: a compressed block with its prefix.</summary>
			public byte[] Stored;

			/// <summary>The second game's whole room as it lay on disk after the block: what an earlier,
			/// longer copy left there (put_arc writes a block that fits over the start of its room and
			/// leaves the rest as it was - 70 blocks of a DOS save's LEV.ARK carry such bytes). Null for a
			/// fresh room, which is zeros.</summary>
			public byte[] Leftover;

			/// <summary>Decoded, on first use.</summary>
			public byte[] Data;

			/// <summary>Where it goes in the file: its offset as read, after every other block once
			/// it is new or has outgrown its room.</summary>
			public long Order;
		}

		private readonly List<Block> mOBlocks = new List<Block>();

		/// <summary>The next order key at the file's end.</summary>
		private long miNextOrder = 1;

		public LayoutEnum Layout { get; }

		/// <summary>The second game's long at 2, kept as read (0 in every shipped archive).</summary>
		public uint Unused { get; private set; }

		private UWArk(LayoutEnum peLayout)
		{
			Layout = peLayout;
		}

		/// <summary>An archive of piCount absent blocks.</summary>
		public static UWArk Create(LayoutEnum peLayout, int piCount)
		{
			UWArk lOArk = new UWArk(peLayout);

			for (int liAt = 0; liAt < piCount; liAt++)
				lOArk.mOBlocks.Add(new Block());

			return lOArk;
		}

		public static UWArk Read(string psPath, LayoutEnum peLayout)
		{
			return Read(File.ReadAllBytes(psPath), peLayout);
		}

		public static UWArk Read(byte[] pyFile, LayoutEnum peLayout)
		{
			if (pyFile == null)
				throw new ArgumentNullException(nameof(pyFile));

			UWArk lOArk = new UWArk(peLayout);
			int liCount = BitConverter.ToUInt16(pyFile, 0);

			if (peLayout == LayoutEnum.Uw1)
				lOArk.fReadUw1(pyFile, liCount);
			else
				lOArk.fReadUw2(pyFile, liCount);

			return lOArk;
		}

		private void fReadUw1(byte[] pyFile, int piCount)
		{
			long[] liOffsets = new long[piCount];

			for (int liAt = 0; liAt < piCount; liAt++)
				liOffsets[liAt] = BitConverter.ToUInt32(pyFile, 2 + (4 * liAt));

			for (int liAt = 0; liAt < piCount; liAt++)
			{
				Block lOBlock = new Block { Present = liOffsets[liAt] != 0 };

				if (lOBlock.Present)
				{
					// To the nearest later start, or the file's end.
					long liEnd = pyFile.Length;

					foreach (long liOther in liOffsets)
					{
						if (liOther > liOffsets[liAt] && liOther < liEnd)
							liEnd = liOther;
					}

					lOBlock.Stored = fSlice(pyFile, liOffsets[liAt], (int)(liEnd - liOffsets[liAt]));
					lOBlock.Room = lOBlock.Stored.Length;
				}

				lOBlock.Order = liOffsets[liAt];
				miNextOrder = System.Math.Max(miNextOrder, lOBlock.Order + 1);
				mOBlocks.Add(lOBlock);
			}
		}

		private void fReadUw2(byte[] pyFile, int piCount)
		{
			Unused = BitConverter.ToUInt32(pyFile, 2);

			for (int liAt = 0; liAt < piCount; liAt++)
			{
				long liOffset = BitConverter.ToUInt32(pyFile, 6 + (4 * liAt));
				int liLength = (int)BitConverter.ToUInt32(pyFile, 6 + (8 * piCount) + (4 * liAt));

				Block lOBlock = new Block
				{
					Present = liOffset != 0,
					Flags = (int)BitConverter.ToUInt32(pyFile, 6 + (4 * piCount) + (4 * liAt)),
					Room = (int)BitConverter.ToUInt32(pyFile, 6 + (12 * piCount) + (4 * liAt))
				};

				if (lOBlock.Present)
				{
					lOBlock.Stored = liOffset + liLength <= pyFile.Length ? fSlice(pyFile, liOffset, liLength) : new byte[0];

					if (lOBlock.Room > lOBlock.Stored.Length && liOffset + lOBlock.Room <= pyFile.Length)
						lOBlock.Leftover = fSlice(pyFile, liOffset, lOBlock.Room);
				}

				lOBlock.Order = liOffset;
				miNextOrder = System.Math.Max(miNextOrder, liOffset + 1);
				mOBlocks.Add(lOBlock);
			}
		}

		public int BlockCount => mOBlocks.Count;

		/// <summary>Whether the block has a place in the file (a present block may be empty, as
		/// BYT.ARK's 3 and 10).</summary>
		public bool IsPresent(int piBlock) => mOBlocks[piBlock].Present;

		public int FlagsOf(int piBlock) => mOBlocks[piBlock].Flags;

		public bool IsCompressed(int piBlock) => (mOBlocks[piBlock].Flags & CompressedFlag) != 0;

		/// <summary>The bytes the block takes on disk (a compressed block with its prefix).</summary>
		public int StoredLength(int piBlock) => mOBlocks[piBlock].Stored == null ? 0 : mOBlocks[piBlock].Stored.Length;

		public int RoomOf(int piBlock) => mOBlocks[piBlock].Room;

		/// <summary>The length a compressed block says it decodes to (its prefix); -1 for a block
		/// stored plain, absent or empty. The original reads it and does not use it (FORMATS.md).</summary>
		public int DeclaredLength(int piBlock)
		{
			Block lOBlock = mOBlocks[piBlock];

			if (!IsCompressed(piBlock) || lOBlock.Stored == null || lOBlock.Stored.Length < LengthPrefixSize)
				return -1;

			return (int)BitConverter.ToUInt32(lOBlock.Stored, 0);
		}

		/// <summary>The stream of a compressed block as stored, without its prefix; null for any other.</summary>
		public byte[] StoredStream(int piBlock)
		{
			Block lOBlock = mOBlocks[piBlock];

			if (!IsCompressed(piBlock) || lOBlock.Stored == null || lOBlock.Stored.Length < LengthPrefixSize)
				return null;

			return fSlice(lOBlock.Stored, LengthPrefixSize, lOBlock.Stored.Length - LengthPrefixSize);
		}

		/// <summary>The block's bytes, decoded when it is stored compressed - a copy; empty for an absent
		/// or empty block, as the original reads one.</summary>
		public byte[] ReadBlock(int piBlock)
		{
			Block lOBlock = mOBlocks[piBlock];

			if (!lOBlock.Present || lOBlock.Stored == null || lOBlock.Stored.Length == 0)
				return new byte[0];

			if (lOBlock.Data == null)
			{
				lOBlock.Data = IsCompressed(piBlock) && lOBlock.Stored.Length >= LengthPrefixSize
					? UWLzss.Decode(lOBlock.Stored, LengthPrefixSize, lOBlock.Stored.Length - LengthPrefixSize, DeclaredLength(piBlock))
					: lOBlock.Stored;
			}

			return (byte[])lOBlock.Data.Clone();
		}

		/// <summary>
		/// Puts pyData into the block as put_arc does (see the class): compressed when its flags ask for
		/// it and the stream is shorter, the room kept when it fits, else grown. A new block takes the
		/// flags given (the second game's; ignored for the first).
		/// </summary>
		public void WriteBlock(int piBlock, byte[] pyData, int piFlagsForNew = 0)
		{
			Block lOBlock = mOBlocks[piBlock];
			byte[] lyData = pyData ?? new byte[0];

			if (!lOBlock.Present && Layout == LayoutEnum.Uw2)
				lOBlock.Flags = piFlagsForNew;

			bool lbNew = !lOBlock.Present;
			byte[] lyStored = lyData;

			if (Layout == LayoutEnum.Uw2)
			{
				byte[] lyStream = (lOBlock.Flags & CompressFlag) != 0 && lyData.Length > 0 ? UWLzss.Encode(lyData) : null;

				if (lyStream != null && lyStream.Length < lyData.Length)
				{
					lyStored = new byte[LengthPrefixSize + lyStream.Length];
					BitConverter.GetBytes((uint)lyData.Length).CopyTo(lyStored, 0);
					lyStream.CopyTo(lyStored, LengthPrefixSize);
					lOBlock.Flags |= CompressedFlag;
				}
				else
				{
					lOBlock.Flags &= ~CompressedFlag;
				}

				bool lbRoom = (lOBlock.Flags & RoomFlag) != 0;
				bool lbFits = !lbNew && ((lbRoom && lyStored.Length <= lOBlock.Room) || lyStored.Length == lOBlock.Room);

				if (!lbFits)
				{
					lOBlock.Room = lyStored.Length + (lbRoom ? RoomPercent * lyStored.Length / 100 : 0);
					lOBlock.Order = miNextOrder++;
					lOBlock.Leftover = null;
				}
			}
			else
			{
				if (lbNew || lyStored.Length != lOBlock.Room)
					lOBlock.Order = miNextOrder++;

				lOBlock.Room = lyStored.Length;
			}

			// Written in place, the block covers the start of its room; the rest keeps what lay there.
			if (lOBlock.Leftover != null && lOBlock.Leftover.Length >= lyStored.Length)
				lyStored.CopyTo(lOBlock.Leftover, 0);

			lOBlock.Present = true;
			lOBlock.Stored = (byte[])lyStored.Clone();
			lOBlock.Data = (byte[])lyData.Clone();
		}

		/// <summary>The whole file: the header, then the present blocks in block order, each in its room.</summary>
		public byte[] ToBytes()
		{
			int liCount = mOBlocks.Count;
			int liHeader = Layout == LayoutEnum.Uw1 ? 2 + (4 * liCount) : 6 + (16 * liCount);
			long liSize = liHeader;

			foreach (Block lOBlock in mOBlocks)
			{
				if (lOBlock.Present)
					liSize += Layout == LayoutEnum.Uw1 ? lOBlock.Stored.Length : Math.Max(lOBlock.Room, lOBlock.Stored.Length);
			}

			byte[] lyFile = new byte[liSize];
			int liAt = liHeader;

			BitConverter.GetBytes((ushort)liCount).CopyTo(lyFile, 0);

			if (Layout == LayoutEnum.Uw2)
				BitConverter.GetBytes(Unused).CopyTo(lyFile, 2);

			// Where each present block starts, in the file's order.
			List<int> lOOrder = new List<int>();

			for (int liBlock = 0; liBlock < liCount; liBlock++)
			{
				if (mOBlocks[liBlock].Present)
					lOOrder.Add(liBlock);
			}

			lOOrder.Sort((piOne, piOther) => mOBlocks[piOne].Order != mOBlocks[piOther].Order
				? mOBlocks[piOne].Order.CompareTo(mOBlocks[piOther].Order) : piOne.CompareTo(piOther));

			int[] liStarts = new int[liCount];

			foreach (int liBlock in lOOrder)
			{
				Block lOBlock = mOBlocks[liBlock];

				liStarts[liBlock] = liAt;
				(Layout == LayoutEnum.Uw2 && lOBlock.Leftover != null ? lOBlock.Leftover : lOBlock.Stored).CopyTo(lyFile, liAt);
				liAt += Layout == LayoutEnum.Uw1 ? lOBlock.Stored.Length : Math.Max(lOBlock.Room, lOBlock.Stored.Length);
			}

			for (int liBlock = 0; liBlock < liCount; liBlock++)
			{
				Block lOBlock = mOBlocks[liBlock];
				int liOffset = lOBlock.Present ? liStarts[liBlock] : 0;

				if (Layout == LayoutEnum.Uw1)
				{
					BitConverter.GetBytes((uint)liOffset).CopyTo(lyFile, 2 + (4 * liBlock));
				}
				else
				{
					BitConverter.GetBytes((uint)liOffset).CopyTo(lyFile, 6 + (4 * liBlock));
					BitConverter.GetBytes((uint)lOBlock.Flags).CopyTo(lyFile, 6 + (4 * liCount) + (4 * liBlock));
					BitConverter.GetBytes((uint)(lOBlock.Stored == null ? 0 : lOBlock.Stored.Length)).CopyTo(lyFile, 6 + (8 * liCount) + (4 * liBlock));
					BitConverter.GetBytes((uint)lOBlock.Room).CopyTo(lyFile, 6 + (12 * liCount) + (4 * liBlock));
				}
			}

			return lyFile;
		}

		private static byte[] fSlice(byte[] pyFrom, long piOffset, int piLength)
		{
			byte[] lySlice = new byte[Math.Max(0, piLength)];

			Array.Copy(pyFrom, piOffset, lySlice, 0, lySlice.Length);

			return lySlice;
		}
	}
}
