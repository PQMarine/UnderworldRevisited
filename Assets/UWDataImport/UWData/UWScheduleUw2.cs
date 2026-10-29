using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// ONE SCHEDULE OF THE SECOND GAME'S SCD.ARK (Docs/UW2-PLAN.md phase 1 step 4, 2026-10-29): the
	/// game's 16 schedules, one per X clock, each read and written whole (Underworld Exhumed,
	/// docs/FORMATS.md "SCD.ARK"; event/SCHEDULE.C:126 and :144, struct SCDWork in event.h:191).
	///
	/// A BLOCK: a word, the number of rows; a byte, the block number (set when loaded, 0 in the shipped
	/// file); an unused byte; 80 clocks of two words (the time reached, the next row to run), one per
	/// level by its 1-based number; then the rows, 16 bytes each, sorted by time. Its length is rows x
	/// 16 + 0x144.
	///
	/// A ROW (struct SCDRow, event.h:61): a word, the time; a byte, the level (0xFF any level, 0xF6 + n
	/// world n); a byte, once (deleted after it ran); a signed byte, the event (negative: disabled; 0
	/// nothing, 1 change goal, 2 teleport, 3 kill, 4 set a quest bit, 5 fire triggers on a square, 6
	/// nothing, 7 a special case by its hack byte, 8 attitude, 9 set a variable, 10 test variables, 11
	/// remove - SCDEVENT.C:515); 11 bytes of parameters by event. What the events do comes with the
	/// second game's rules (phase 4).
	/// </summary>
	public sealed class UWScheduleUw2
	{
		public const int HeaderSize = 0x144;

		public const int RowSize = 16;

		public const int ClockCount = 80;

		public const int ParameterSize = 11;

		/// <summary>The schedules of SCD.ARK, one per X clock.</summary>
		public const int ScheduleCount = 16;

		public const int AnyLevel = 0xFF;

		/// <summary>0xF6 + n: every level of world n.</summary>
		public const int FirstWorld = 0xF6;

		public sealed class Row
		{
			public int Time;

			public int Level;

			public int Once;

			/// <summary>Negative: disabled.</summary>
			public int Event;

			public byte[] Parameters = new byte[ParameterSize];
		}

		public int BlockNumber;

		public int Unused;

		/// <summary>Each level's clock: the time reached and the next row to run.</summary>
		public readonly int[] ClockTime = new int[ClockCount];

		public readonly int[] ClockNextRow = new int[ClockCount];

		public readonly List<Row> Rows = new List<Row>();

		/// <summary>A schedule block; null when it is shorter than its header and rows.</summary>
		public static UWScheduleUw2 Read(byte[] pyBlock)
		{
			if (pyBlock == null || pyBlock.Length < HeaderSize)
				return null;

			int liRows = UWBinary.ReadU16(pyBlock, 0);

			if (pyBlock.Length < HeaderSize + (liRows * RowSize))
				return null;

			UWScheduleUw2 lOSchedule = new UWScheduleUw2 { BlockNumber = pyBlock[2], Unused = pyBlock[3] };

			for (int liClock = 0; liClock < ClockCount; liClock++)
			{
				lOSchedule.ClockTime[liClock] = UWBinary.ReadU16(pyBlock, 4 + (4 * liClock));
				lOSchedule.ClockNextRow[liClock] = UWBinary.ReadU16(pyBlock, 6 + (4 * liClock));
			}

			for (int liRow = 0; liRow < liRows; liRow++)
			{
				int liAt = HeaderSize + (liRow * RowSize);
				Row lORow = new Row
				{
					Time = UWBinary.ReadU16(pyBlock, liAt),
					Level = pyBlock[liAt + 2],
					Once = pyBlock[liAt + 3],
					Event = (sbyte)pyBlock[liAt + 4]
				};

				Array.Copy(pyBlock, liAt + 5, lORow.Parameters, 0, ParameterSize);
				lOSchedule.Rows.Add(lORow);
			}

			return lOSchedule;
		}

		/// <summary>The block as the game writes it: rows x 16 + 0x144 bytes.</summary>
		public byte[] ToBytes()
		{
			byte[] lyBlock = new byte[HeaderSize + (Rows.Count * RowSize)];

			fPut16(lyBlock, 0, Rows.Count);
			lyBlock[2] = (byte)BlockNumber;
			lyBlock[3] = (byte)Unused;

			for (int liClock = 0; liClock < ClockCount; liClock++)
			{
				fPut16(lyBlock, 4 + (4 * liClock), ClockTime[liClock]);
				fPut16(lyBlock, 6 + (4 * liClock), ClockNextRow[liClock]);
			}

			for (int liRow = 0; liRow < Rows.Count; liRow++)
			{
				int liAt = HeaderSize + (liRow * RowSize);
				Row lORow = Rows[liRow];

				fPut16(lyBlock, liAt, lORow.Time);
				lyBlock[liAt + 2] = (byte)lORow.Level;
				lyBlock[liAt + 3] = (byte)lORow.Once;
				lyBlock[liAt + 4] = (byte)(sbyte)lORow.Event;
				Array.Copy(lORow.Parameters, 0, lyBlock, liAt + 5, ParameterSize);
			}

			return lyBlock;
		}

		private static void fPut16(byte[] pyData, int piAt, int piValue)
		{
			pyData[piAt] = (byte)piValue;
			pyData[piAt + 1] = (byte)(piValue >> 8);
		}
	}
}
