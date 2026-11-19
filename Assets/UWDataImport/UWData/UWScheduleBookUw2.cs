using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S SCHEDULES IN PLAY (UW2 plan G6 part 1, 2026-11-16): the sixteen blocks of SCD.ARK held by the
	/// session (UWSessionWorld.Schedules) while the walk runs, where the original loads and saves one block at a time
	/// (uw2 event/SCHEDULE.C Sched_Load, Sched_Save), and the queue of rows a teleport could not do, which do_migrations
	/// adds to block 0 (up to 16). Written back whole with a save (UWSaveCodec.SaveUw2). The rules are
	/// UWScheduleRulesUw2's.
	/// </summary>
	public sealed class UWScheduleBookUw2
	{
		/// <summary>SCDWork's migration queue (Sched_Migrate): sixteen rows.</summary>
		public const int MigrationLimit = 16;

		public readonly UWScheduleUw2[] Blocks = new UWScheduleUw2[UWScheduleUw2.ScheduleCount];

		/// <summary>Rows waiting for block 0 (do_migrations).</summary>
		public readonly List<UWScheduleUw2.Row> Migrations = new List<UWScheduleUw2.Row>();

		/// <summary>The workspace taken: a pass is running (Sched_SetAllClocks' get_workspace).</summary>
		public bool IsRunning { get; set; }

		/// <summary>The archive the blocks came from; a save writes them back into it.</summary>
		public UWArk Ark { get; private set; }

		/// <summary>The session's schedules, read the first time from SCD.ARK beside the loaded save (else the data's,
		/// a new game's); null without a session, outside the second game or without the file.</summary>
		public static UWScheduleBookUw2 Of(UWGameSession pOSession)
		{
			if (pOSession == null || pOSession.World == null || pOSession.Data == null || pOSession.Game.Game != UWGame.Uw2)
				return null;

			if (pOSession.World.Schedules != null)
				return pOSession.World.Schedules;

			string lsPath = System.IO.Path.Combine(pOSession.World.IsSavegame ? pOSession.World.SavegamePath : pOSession.Data.DataPath,
				UWSaveCodec.ScheduleFileName);

			if (!System.IO.File.Exists(lsPath))
				lsPath = System.IO.Path.Combine(pOSession.Data.DataPath, UWSaveCodec.ScheduleFileName);

			if (!System.IO.File.Exists(lsPath))
				return null;

			pOSession.World.Schedules = Read(UWArk.Read(lsPath, UWArk.LayoutEnum.Uw2));

			return pOSession.World.Schedules;
		}

		/// <summary>The blocks of an SCD.ARK; null without an archive.</summary>
		public static UWScheduleBookUw2 Read(UWArk pOArk)
		{
			if (pOArk == null)
				return null;

			UWScheduleBookUw2 lOBook = new UWScheduleBookUw2 { Ark = pOArk };

			for (int liBlock = 0; liBlock < UWScheduleUw2.ScheduleCount && liBlock < pOArk.BlockCount; liBlock++)
			{
				// The header's block byte stays as the file has it: the original writes it (Sched_Load) only into a
				// block it saves, and saves only a changed one (ToBytes).
				lOBook.Blocks[liBlock] = UWScheduleUw2.Read(pOArk.ReadBlock(liBlock));
			}

			return lOBook;
		}

		/// <summary>The archive with every CHANGED block written back (Sched_Save's 16 bytes a row plus the 0x144 header,
		/// only with scdBlockHasBeenModified set), carrying its number in the header as Sched_Load put it there; an
		/// unchanged block keeps the file's bytes.</summary>
		public byte[] ToBytes()
		{
			for (int liBlock = 0; liBlock < Blocks.Length; liBlock++)
			{
				UWScheduleUw2 lOBlock = Blocks[liBlock];

				if (lOBlock == null || fSame(lOBlock.ToBytes(), Ark.ReadBlock(liBlock)))
					continue;

				lOBlock.BlockNumber = liBlock;
				Ark.WriteBlock(liBlock, lOBlock.ToBytes());
			}

			return Ark.ToBytes();
		}

		private static bool fSame(byte[] pyA, byte[] pyB)
		{
			if (pyA == null || pyB == null || pyA.Length != pyB.Length)
				return false;

			for (int liAt = 0; liAt < pyA.Length; liAt++)
			{
				if (pyA[liAt] != pyB[liAt])
					return false;
			}

			return true;
		}

		/// <summary>A row's copy (the migration queue's rows are copies, struct assignment in the original).</summary>
		public static UWScheduleUw2.Row Copy(UWScheduleUw2.Row pORow)
		{
			UWScheduleUw2.Row lOCopy = new UWScheduleUw2.Row { Time = pORow.Time, Level = pORow.Level, Once = pORow.Once, Event = pORow.Event };

			System.Array.Copy(pORow.Parameters, lOCopy.Parameters, UWScheduleUw2.ParameterSize);

			return lOCopy;
		}
	}
}
