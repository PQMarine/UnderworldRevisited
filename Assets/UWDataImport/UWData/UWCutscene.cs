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

		/// <summary>The file of a cutscene's page: "CS" and the cutscene number in three octal
		/// digits, ".N" and the page in two (uw-formats.txt on the command open-file) - 0x108
		/// page 1 is CS410.N01.</summary>
		public static string FileName(int piCutscene, int piPage)
		{
			return string.Format("CS{0}.N{1}",
				System.Convert.ToString(piCutscene, 8).PadLeft(3, '0'),
				System.Convert.ToString(piPage, 8).PadLeft(2, '0'));
		}

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

		/// <summary>Bounding box (top-down canvas pixels) of everything that differs from the
		/// top-left background colour in any frame of the file - the same background rule as
		/// UWGameUI.fMeasureWindowContent. The whole canvas when nothing is painted. Decodes its
		/// own copy of the file, so a playing one keeps its place. Moved from
		/// UWIntroPlayer.fMeasurePaintedBlock (cleanup step 8.7, 2026-10-24), which places the
		/// viewport cutscenes by it.</summary>
		public static void MeasurePaintedBlock(string psPath, out int piX, out int piY, out int piWidth, out int piHeight)
		{
			UWCutscene lOCutscene = new UWCutscene(psPath);
			int liMinX = lOCutscene.Width, liMinY = lOCutscene.Height, liMaxX = -1, liMaxY = -1;
			byte lyBackground = 0;

			for (int liFrame = 0; liFrame < lOCutscene.FrameCount; liFrame++)
			{
				byte[] lyPixels = lOCutscene.GetFrame(liFrame);

				if (liFrame == 0 && lyPixels.Length > 0)
					lyBackground = lyPixels[0];

				for (int liY = 0; liY < lOCutscene.Height; liY++)
				{
					for (int liX = 0; liX < lOCutscene.Width; liX++)
					{
						if (lyPixels[(liY * lOCutscene.Width) + liX] == lyBackground)
							continue;

						if (liX < liMinX) liMinX = liX;
						if (liX > liMaxX) liMaxX = liX;
						if (liY < liMinY) liMinY = liY;
						if (liY > liMaxY) liMaxY = liY;
					}
				}
			}

			if (liMaxX < liMinX || liMaxY < liMinY)
			{
				piX = 0;
				piY = 0;
				piWidth = lOCutscene.Width;
				piHeight = lOCutscene.Height;

				return;
			}

			piX = liMinX;
			piY = liMinY;
			piWidth = liMaxX - liMinX + 1;
			piHeight = liMaxY - liMinY + 1;
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

		// ------------------------------------------------- The second game's player (UW2 plan phase 5 part C)

		/// <summary>A colour cycle of the header (0x80, 16 of 8 bytes): its rate as read_anmhdr keeps it, (word &gt;&gt; 8)
		/// &amp; 0x3F, which anm_cycle divides 0x38E by (0 is no cycle), and the entries it rotates.</summary>
		public struct ColourCycle
		{
			public int Rate;

			public int First;

			public int Last;
		}

		private const int CycleTable = 0x80;

		private const int CycleCount = 16;

		/// <summary>The header's 16 colour cycles.</summary>
		public ColourCycle[] Cycles
		{
			get
			{
				ColourCycle[] lOCycles = new ColourCycle[CycleCount];

				for (int liAt = 0; liAt < CycleCount && myData.Length >= HeaderSize; liAt++)
				{
					int liEntry = CycleTable + (liAt * 8);

					lOCycles[liAt] = new ColourCycle { Rate = (fU16(liEntry + 2) >> 8) & 0x3F, First = myData[liEntry + 6], Last = myData[liEntry + 7] };
				}

				return lOCycles;
			}
		}

		/// <summary>Has the last record a delta back to the first (header 0x1A)? The second game's player leaves it out.</summary>
		public bool HasLastDelta => myData.Length >= HeaderSize && myData[0x1A] != 0;

		/// <summary>The records the second game's player plays: every large page's records, the last page's loop-back
		/// delta left out (cuts_process_lp's nrec).</summary>
		public int PlayedRecords
		{
			get
			{
				int liRecords = 0;

				for (int liPage = 0; liPage < miLargePages && liPage < 256; liPage++)
					liRecords += fU16(LargePageTable + (liPage * 6) + 2) & 0x3FFF;

				return Math.Max(0, liRecords - (HasLastDelta && liRecords > 0 ? 1 : 0));
			}
		}

		/// <summary>The large pages (header 0x06).</summary>
		public int PageCount => miLargePages;

		/// <summary>The records of large page piPage (its table entry's count).</summary>
		public int PageRecords(int piPage)
		{
			return piPage >= 0 && piPage < miLargePages && piPage < 256 ? fU16(LargePageTable + (piPage * 6) + 2) & 0x3FFF : 0;
		}

		/// <summary>The first record of large page piPage.</summary>
		public int PageFirstRecord(int piPage)
		{
			return piPage >= 0 && piPage < miLargePages && piPage < 256 ? fU16(LargePageTable + (piPage * 6)) : 0;
		}

		/// <summary>A record's size in its page's table (cuts_process_lp's sizes[i]), 0 when there is none.</summary>
		public int RecordSize(int piRecord)
		{
			return fFindRecord(piRecord, out _, out int liSize) ? liSize : 0;
		}

		/// <summary>DrawRecord onto a buffer of its own, clipped to it.</summary>
		public bool DrawRecord(int piRecord, byte[] pyTarget, int piTargetWidth, int piTargetHeight, int piX, int piY, int piRows)
		{
			return DrawRecord(piRecord, piRows, (piPx, piPy, pyPixel) =>
			{
				int liX = piX + piPx;
				int liY = piY + piPy;

				if (liX >= 0 && liY >= 0 && liX < piTargetWidth && liY < piTargetHeight)
					pyTarget[(liY * piTargetWidth) + liX] = pyPixel;
			});
		}

		/// <summary>
		/// DRAWS ONE RECORD (cuts_process_lp: show for a whole frame, draw_rsd for a delta; UW2 plan phase 5 part C): the
		/// record's picture is a frame 320 wide of which piRows rows count; pFPut gets each pixel it writes, at its place
		/// in that frame - a delta's skips write nothing, so what is there stays. A record of 4 bytes or fewer draws
		/// nothing (its size - 4 is 0). True when it drew.
		/// </summary>
		public bool DrawRecord(int piRecord, int piRows, Action<int, int, byte> pFPut)
		{
			if (!fFindRecord(piRecord, out int liStart, out int liSize) || liSize <= 4)
				return false;

			int liEnd = Math.Min(liStart + liSize, myData.Length);
			int liBody = liStart + 2;

			if (myData[liStart + 1] != 0)
				liBody = liStart + 4 + ((fU16(liStart + 2) + 1) & ~1);

			if (liBody + 2 > liEnd)
				return false;

			int liType = myData[liBody];
			int liSource = liBody + 2;
			int liFrameWidth = Width > 0 ? Width : 320;
			int liPixels = liFrameWidth * Math.Max(0, piRows);

			if (liType == 0)
			{
				for (int liAt = 0; liAt < liPixels && liSource + liAt < liEnd; liAt++)
					pFPut(liAt % liFrameWidth, liAt / liFrameWidth, myData[liSource + liAt]);

				return true;
			}

			if (liType != 1)
				return false;

			int liCursor = 0;

			while (liSource < liEnd && liCursor < liPixels)
			{
				int liOp = myData[liSource++];

				if (liOp == 0x00)
				{
					if (liSource + 2 > liEnd)
						break;

					int liCount = myData[liSource++];

					fRun(pFPut, liFrameWidth, liPixels, ref liCursor, liCount, myData[liSource++]);
				}
				else if (liOp < 0x80)
					fDump(pFPut, liFrameWidth, liPixels, ref liCursor, ref liSource, liEnd, liOp);
				else if (liOp > 0x80)
					liCursor += liOp & 0x7F;
				else
				{
					if (liSource + 2 > liEnd)
						break;

					int liWord = fU16(liSource);

					liSource += 2;

					if (liWord == 0)
						break;

					if (liWord < 0x8000)
						liCursor += liWord;
					else if (liWord < 0xC000)
						fDump(pFPut, liFrameWidth, liPixels, ref liCursor, ref liSource, liEnd, liWord & 0x3FFF);
					else
					{
						if (liSource >= liEnd)
							break;

						fRun(pFPut, liFrameWidth, liPixels, ref liCursor, liWord & 0x3FFF, myData[liSource++]);
					}
				}
			}

			return true;
		}

		private static void fRun(Action<int, int, byte> pFPut, int piFrameWidth, int piPixels, ref int piCursor, int piCount, byte pyPixel)
		{
			for (int liAt = 0; liAt < piCount && piCursor < piPixels; liAt++, piCursor++)
				pFPut(piCursor % piFrameWidth, piCursor / piFrameWidth, pyPixel);
		}

		private void fDump(Action<int, int, byte> pFPut, int piFrameWidth, int piPixels, ref int piCursor, ref int piSource, int piEnd, int piCount)
		{
			for (int liAt = 0; liAt < piCount; liAt++, piCursor++, piSource++)
			{
				if (piSource >= piEnd)
					return;

				if (piCursor < piPixels)
					pFPut(piCursor % piFrameWidth, piCursor / piFrameWidth, myData[piSource]);
			}
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
