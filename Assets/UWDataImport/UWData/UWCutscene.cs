using System;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A cutscene animation from the CUTS folder (CS000.N01 and so on): a DeluxePaint
	/// Animation file ("LPF " with content type "ANIM"), 320x200, 256 colours.
	///
	/// Written 2026-09-15 from the format description in Electronic Arts' "Programmer's Kit
	/// for DeluxePaint Animation" (1990, ANIMFILE.TXT and the kit's headers; the kit may be
	/// freely incorporated into programs) and checked against all 47 files of UW1. Layout,
	/// all numbers little-endian:
	///   0x000  header: "LPF ", number of large pages (0x06), number of records (0x08),
	///          "ANIM" (0x10), width/height (0x14/0x16), hasLastDelta (0x1A), number of
	///          frames incl. the loop-back delta (0x40), frames per second (0x44);
	///   0x080  16 colour cycles (not used here);
	///   0x100  palette, 256 entries of B, G, R, 0 with full 8-bit values (the order is B,G,R
	///          in the files, although the description says R,G,B);
	///   0x500  large page table, 256 entries of baseRecord, recordCount (bits 14/15 are
	///          continuation flags), byteCount;
	///   0xB00 + page * 0x10000  a large page: the same three words, a word of continued bytes,
	///          the size of each record, then the records one after another.
	/// Record n is frame n, a delta on the previous frame (size 0 = unchanged). A record starts
	/// with 'B' and a flag byte (extra data when non-zero, never in UW1), then a body type word:
	/// 0 = raw pixels, 1 = RunSkipDump. RunSkipDump opcodes, a cursor over the frame buffer:
	///   00 count pixel: run; 01-7F: dump that many bytes; 81-FF: skip (op &amp; 7F);
	///   80 word: 0 = end, 0001-7FFF skip, 8000-BFFF dump (w &amp; 3FFF), C000-FFFF run of
	///   (w &amp; 3FFF) with the following pixel.
	/// With hasLastDelta the last record leads from the last frame back to the first; it is
	/// counted in FrameCount (callers rely on the header value).
	/// </summary>
	public class UWCutscene
	{
		private const int HeaderSize = 0xB00;

		private const int LargePageSize = 0x10000;

		private const int LargePageTable = 0x500;

		private const int PaletteOffset = 0x100;

		public int Width { get; private set; }

		public int Height { get; private set; }

		/// <summary>Number of frames as stored in the header, including the loop-back delta
		/// (whose result equals frame 0).</summary>
		public int FrameCount { get; private set; }

		/// <summary>Frames per second from the header.</summary>
		public int FrameRate { get; private set; }

		/// <summary>768 bytes, R, G, B per palette index.</summary>
		public byte[] PaletteRgb { get; private set; }

		private readonly byte[] myData;

		private int miLargePages;

		private byte[] myFrame;

		/// <summary>Index of the frame currently held in myFrame, -1 before the first.</summary>
		private int miDecodedFrame = -1;

		public UWCutscene(string psFileName)
		{
			PaletteRgb = new byte[768];
			myData = File.ReadAllBytes(psFileName);

			if (myData.Length < HeaderSize || fReadTag(0) != "LPF " || fReadTag(0x10) != "ANIM")
				return;

			miLargePages = fU16(0x06);
			Width = fU16(0x14);
			Height = fU16(0x16);
			FrameCount = (int)fU32(0x40);
			FrameRate = fU16(0x44);

			for (int liColour = 0; liColour < 256; liColour++)
			{
				int liEntry = PaletteOffset + (liColour * 4);
				PaletteRgb[(liColour * 3) + 0] = myData[liEntry + 2];
				PaletteRgb[(liColour * 3) + 1] = myData[liEntry + 1];
				PaletteRgb[(liColour * 3) + 2] = myData[liEntry + 0];
			}

			myFrame = new byte[Width * Height];
		}

		/// <summary>
		/// The frame as palette indices, Width * Height bytes, row 0 at the top. Any order is
		/// allowed: later frames are decoded onwards from the current one, earlier frames from
		/// the start. Returns a copy; out-of-range numbers are clamped, null without frames.
		/// </summary>
		public byte[] GetFrame(int piFrameNumber)
		{
			if (FrameCount <= 0 || myFrame == null)
				return null;

			int liTarget = Math.Max(0, Math.Min(piFrameNumber, FrameCount - 1));

			if (liTarget < miDecodedFrame)
			{
				Array.Clear(myFrame, 0, myFrame.Length);
				miDecodedFrame = -1;
			}

			while (miDecodedFrame < liTarget)
			{
				miDecodedFrame++;
				fApplyRecord(miDecodedFrame);
			}

			return (byte[])myFrame.Clone();
		}

		/// <summary>Applies record piRecord as a delta onto myFrame.</summary>
		private void fApplyRecord(int piRecord)
		{
			int liRecordStart;
			int liRecordSize;

			if (!fFindRecord(piRecord, out liRecordStart, out liRecordSize) || liRecordSize < 4)
				return;

			int liEnd = Math.Min(liRecordStart + liRecordSize, myData.Length);
			int liBody = liRecordStart + 2;

			// a non-zero flag byte announces extra data (a word with its length, padded to even)
			if (myData[liRecordStart + 1] != 0)
				liBody = liRecordStart + 4 + ((fU16(liRecordStart + 2) + 1) & ~1);

			if (liBody + 2 > liEnd)
				return;

			int liType = fU16(liBody);
			int liSource = liBody + 2;

			if (liType == 0)
			{
				int liCount = Math.Min(myFrame.Length, liEnd - liSource);

				if (liCount > 0)
					Array.Copy(myData, liSource, myFrame, 0, liCount);
			}
			else if (liType == 1)
			{
				fRunSkipDump(liSource, liEnd);
			}
		}

		/// <summary>The RunSkipDump delta, bounded by the frame and the record end.</summary>
		private void fRunSkipDump(int piSource, int piEnd)
		{
			int liTarget = 0;
			int liPixels = myFrame.Length;

			while (piSource < piEnd && liTarget < liPixels)
			{
				int liOp = myData[piSource++];

				if (liOp == 0x00)
				{
					if (piSource + 2 > piEnd)
						return;

					int liCount = myData[piSource++];
					byte lyPixel = myData[piSource++];
					fFill(ref liTarget, liCount, lyPixel);
				}
				else if (liOp < 0x80)
				{
					fCopy(ref piSource, piEnd, ref liTarget, liOp);
				}
				else if (liOp > 0x80)
				{
					liTarget += liOp & 0x7F;
				}
				else
				{
					if (piSource + 2 > piEnd)
						return;

					int liWord = fU16(piSource);
					piSource += 2;

					if (liWord == 0)
						return;

					if (liWord < 0x8000)
					{
						liTarget += liWord;
					}
					else if (liWord < 0xC000)
					{
						fCopy(ref piSource, piEnd, ref liTarget, liWord & 0x3FFF);
					}
					else
					{
						if (piSource >= piEnd)
							return;

						fFill(ref liTarget, liWord & 0x3FFF, myData[piSource++]);
					}
				}
			}
		}

		private void fFill(ref int piTarget, int piCount, byte pyPixel)
		{
			int liCount = Math.Min(piCount, myFrame.Length - piTarget);

			for (int liAt = 0; liAt < liCount; liAt++)
				myFrame[piTarget + liAt] = pyPixel;

			piTarget += piCount;
		}

		private void fCopy(ref int piSource, int piEnd, ref int piTarget, int piCount)
		{
			int liCount = Math.Min(Math.Min(piCount, myFrame.Length - piTarget), piEnd - piSource);

			if (liCount > 0)
				Array.Copy(myData, piSource, myFrame, piTarget, liCount);

			piSource += piCount;
			piTarget += piCount;
		}

		/// <summary>Finds the start and size of a record via the large page table.</summary>
		private bool fFindRecord(int piRecord, out int piStart, out int piSize)
		{
			piStart = 0;
			piSize = 0;

			for (int liPage = 0; liPage < miLargePages && liPage < 256; liPage++)
			{
				int liEntry = LargePageTable + (liPage * 6);
				int liBase = fU16(liEntry);
				int liCount = fU16(liEntry + 2) & 0x3FFF;

				if (piRecord < liBase || piRecord >= liBase + liCount)
					continue;

				int liPageStart = HeaderSize + (liPage * LargePageSize);
				int liSizes = liPageStart + 8;
				int liIndex = piRecord - liBase;

				if (liSizes + (liCount * 2) > myData.Length)
					return false;

				int liOffset = liSizes + (liCount * 2);

				for (int liAt = 0; liAt < liIndex; liAt++)
					liOffset += fU16(liSizes + (liAt * 2));

				piStart = liOffset;
				piSize = fU16(liSizes + (liIndex * 2));
				return piStart + piSize <= myData.Length;
			}

			return false;
		}

		private string fReadTag(int piOffset)
		{
			return System.Text.Encoding.ASCII.GetString(myData, piOffset, 4);
		}

		private int fU16(int piOffset)
		{
			return myData[piOffset] | (myData[piOffset + 1] << 8);
		}

		private uint fU32(int piOffset)
		{
			return (uint)(fU16(piOffset) | (fU16(piOffset + 2) << 16));
		}
	}
}
