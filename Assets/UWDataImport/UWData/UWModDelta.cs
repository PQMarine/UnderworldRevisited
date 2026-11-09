using System;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A DELTA OF THE UNDERWORLD EDITOR'S MOD FORMAT 2 (.uwd inside a .uwmod; the editor's docs/mod-format.md, "The
	/// delta"; Docs/MOD-SUPPORT-PLAN.md, built 2026-11-09): how to make an edited file from one exact original -
	/// COPY runs of the original, ADD the mod's own bytes, and for the second game's archives BLOCK, one block
	/// of the original unpacked, changed by a nested delta and packed again. Written here from the format's
	/// description; his Delta.Apply (MIT) was read for the cases the description leaves open.
	///
	///   "UWD2"  v:baseLength  v:targetLength  op*  0x00
	///   0x01 COPY   z:offset from the end of the previous COPY, v:length
	///   0x02 ADD    v:length, the bytes
	///   0x03 BLOCK  v:block of the original archive, a mode byte (1 LZSS with its length prefix, 0 plain),
	///               a nested delta of COPY and ADD from the block's start, ending with 0x00
	///
	/// Numbers are unsigned LEB128 (v), signed ones zigzag-coded (z). A delta is untrusted: reading outside
	/// the original, a wrong length or an unknown op is refused with InvalidDataException.
	/// </summary>
	public static class UWModDelta
	{
		private const byte OpEnd = 0x00;

		private const byte OpCopy = 0x01;

		private const byte OpAdd = 0x02;

		private const byte OpBlock = 0x03;

		private const byte BlockPlain = 0;

		private const byte BlockLzss = 1;

		private static readonly byte[] msMagic = { (byte)'U', (byte)'W', (byte)'D', (byte)'2' };

		/// <summary>The edited file from its original and the delta; InvalidDataException where the delta does not fit.</summary>
		public static byte[] Apply(byte[] pyOriginal, byte[] pyDelta)
		{
			if (pyOriginal == null || pyDelta == null)
				throw new InvalidDataException("no original or no delta");

			Reader lOReader = new Reader(pyDelta);

			foreach (byte lyMagic in msMagic)
			{
				if (lOReader.Byte() != lyMagic)
					throw new InvalidDataException("not a delta (UWD2)");
			}

			long liBase = lOReader.Unsigned();
			long liTarget = lOReader.Unsigned();

			if (liBase != pyOriginal.Length)
				throw new InvalidDataException("made from a file of " + liBase + " bytes, not " + pyOriginal.Length);

			byte[] lyOutput = new byte[liTarget];
			int liPos = 0;

			fRun(lOReader, pyOriginal, lyOutput, ref liPos);

			if (!lOReader.AtEnd)
				throw new InvalidDataException("bytes after the delta's end");

			if (liPos != lyOutput.Length)
				throw new InvalidDataException("gives " + liPos + " bytes, not the " + liTarget + " it promises");

			return lyOutput;
		}

		private static void fRun(Reader pOReader, byte[] pySource, byte[] pyOutput, ref int piPos)
		{
			long liPreviousEnd = 0;

			while (true)
			{
				byte lyOp = pOReader.Byte();

				if (lyOp == OpEnd)
					return;

				if (lyOp == OpCopy)
				{
					long liOffset = liPreviousEnd + pOReader.Signed();
					long liLength = pOReader.Unsigned();

					if (liOffset < 0 || liOffset + liLength > pySource.Length)
						throw new InvalidDataException("copies from outside the original");

					if (piPos + liLength > pyOutput.Length)
						throw new InvalidDataException("writes past its promised length");

					Array.Copy(pySource, liOffset, pyOutput, piPos, liLength);
					piPos += (int)liLength;
					liPreviousEnd = liOffset + liLength;
				}
				else if (lyOp == OpAdd)
				{
					long liLength = pOReader.Unsigned();

					if (piPos + liLength > pyOutput.Length)
						throw new InvalidDataException("writes past its promised length");

					pOReader.Bytes(pyOutput, piPos, (int)liLength);
					piPos += (int)liLength;
				}
				else if (lyOp == OpBlock)
				{
					int liBlock = (int)pOReader.Unsigned();
					byte lyMode = pOReader.Byte();
					byte[] lyContent = fReadBlock(pySource, liBlock);
					MemoryStream lOEdited = new MemoryStream();

					fRunNested(pOReader, lyContent, lOEdited, Math.Max(1L << 20, (pyOutput.Length - piPos) * 16L));

					byte[] lyEdited = lOEdited.ToArray();
					byte[] lyStored;

					if (lyMode == BlockLzss)
					{
						// The length prefix and the stream, as the game stores a packed block (UWArk, UWLzss).
						byte[] lyStream = UWLzss.Encode(lyEdited);

						lyStored = new byte[lyStream.Length + 4];
						lyStored[0] = (byte)lyEdited.Length;
						lyStored[1] = (byte)(lyEdited.Length >> 8);
						lyStored[2] = (byte)(lyEdited.Length >> 16);
						lyStored[3] = (byte)(lyEdited.Length >> 24);
						Array.Copy(lyStream, 0, lyStored, 4, lyStream.Length);
					}
					else if (lyMode == BlockPlain)
						lyStored = lyEdited;
					else
						throw new InvalidDataException("unknown block mode " + lyMode);

					if (piPos + lyStored.Length > pyOutput.Length)
						throw new InvalidDataException("writes past its promised length");

					Array.Copy(lyStored, 0, pyOutput, piPos, lyStored.Length);
					piPos += lyStored.Length;
				}
				else
					throw new InvalidDataException("unknown op 0x" + lyOp.ToString("X2"));
			}
		}

		/// <summary>A nested delta (COPY and ADD) into a stream of unknown length, capped by piCap.</summary>
		private static void fRunNested(Reader pOReader, byte[] pySource, MemoryStream pOOut, long piCap)
		{
			long liPreviousEnd = 0;

			while (true)
			{
				byte lyOp = pOReader.Byte();

				if (lyOp == OpEnd)
					return;

				if (lyOp == OpCopy)
				{
					long liOffset = liPreviousEnd + pOReader.Signed();
					long liLength = pOReader.Unsigned();

					if (liOffset < 0 || liOffset + liLength > pySource.Length)
						throw new InvalidDataException("copies from outside the original block");

					if (pOOut.Length + liLength > piCap)
						throw new InvalidDataException("a block grows past any sensible size");

					pOOut.Write(pySource, (int)liOffset, (int)liLength);
					liPreviousEnd = liOffset + liLength;
				}
				else if (lyOp == OpAdd)
				{
					long liLength = pOReader.Unsigned();

					if (pOOut.Length + liLength > piCap)
						throw new InvalidDataException("a block grows past any sensible size");

					byte[] lyBytes = new byte[liLength];

					pOReader.Bytes(lyBytes, 0, (int)liLength);
					pOOut.Write(lyBytes, 0, lyBytes.Length);
				}
				else
					throw new InvalidDataException("op 0x" + lyOp.ToString("X2") + " is not allowed inside a block");
			}
		}

		/// <summary>
		/// A block of a second-game archive as the game reads it: the count at 0, then per block its offset,
		/// flags, stored length and room (four bytes each); a block flagged packed (2) is the length prefix and
		/// the LZSS stream (UWArk).
		/// </summary>
		private static byte[] fReadBlock(byte[] pyArchive, int piBlock)
		{
			if (pyArchive.Length < 6)
				throw new InvalidDataException("reads a block, but the original is no archive");

			int liCount = pyArchive[0] | (pyArchive[1] << 8);

			if (liCount == 0 || 6 + (16L * liCount) > pyArchive.Length)
				throw new InvalidDataException("reads a block, but the original is no archive of the second game");

			if (piBlock < 0 || piBlock >= liCount)
				throw new InvalidDataException("reads block " + piBlock + ", which the original lacks");

			uint liOffset = fU32(pyArchive, 6 + (4 * piBlock));
			uint liFlags = fU32(pyArchive, 6 + (4 * liCount) + (4 * piBlock));
			uint liLength = fU32(pyArchive, 6 + (8 * liCount) + (4 * piBlock));

			if (liOffset == 0 || (ulong)liOffset + liLength > (ulong)pyArchive.Length)
				throw new InvalidDataException("reads block " + piBlock + ", which the original lacks");

			if ((liFlags & UWArk.CompressedFlag) == 0)
			{
				byte[] lyPlain = new byte[liLength];

				Array.Copy(pyArchive, liOffset, lyPlain, 0, liLength);

				return lyPlain;
			}

			if (liLength < 4)
				throw new InvalidDataException("block " + piBlock + " of the original cannot be read");

			int liExpected = (int)fU32(pyArchive, (int)liOffset);

			return UWLzss.Decode(pyArchive, (int)liOffset + 4, (int)liLength - 4, liExpected);
		}

		private static uint fU32(byte[] pyData, int piAt)
		{
			return (uint)(pyData[piAt] | (pyData[piAt + 1] << 8) | (pyData[piAt + 2] << 16) | (pyData[piAt + 3] << 24));
		}

		private sealed class Reader
		{
			private readonly byte[] myData;

			private int miPos;

			public Reader(byte[] pyData)
			{
				myData = pyData;
			}

			public bool AtEnd => miPos == myData.Length;

			public byte Byte()
			{
				if (miPos >= myData.Length)
					throw new InvalidDataException("the delta ends early");

				return myData[miPos++];
			}

			public long Unsigned()
			{
				ulong liValue = fVarint();

				if (liValue > int.MaxValue)
					throw new InvalidDataException("a number in the delta is too large");

				return (long)liValue;
			}

			public long Signed()
			{
				ulong liRaw = fVarint();
				long liValue = (long)(liRaw >> 1) ^ -(long)(liRaw & 1);

				if (liValue > int.MaxValue || liValue < -(long)int.MaxValue)
					throw new InvalidDataException("a number in the delta is too large");

				return liValue;
			}

			private ulong fVarint()
			{
				ulong liValue = 0;

				for (int liShift = 0; ; liShift += 7)
				{
					if (liShift > 63)
						throw new InvalidDataException("a number in the delta is too long");

					byte lyByte = Byte();

					liValue |= (ulong)(lyByte & 0x7F) << liShift;

					if ((lyByte & 0x80) == 0)
						return liValue;
				}
			}

			public void Bytes(byte[] pyTarget, int piAt, int piLength)
			{
				if (piLength < 0 || miPos + piLength > myData.Length)
					throw new InvalidDataException("the delta ends early");

				Array.Copy(myData, miPos, pyTarget, piAt, piLength);
				miPos += piLength;
			}
		}
	}
}
