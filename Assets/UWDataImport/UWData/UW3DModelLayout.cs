namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE A GAME'S PROGRAM KEEPS ITS 3D MODELS (Docs/UW2-PLAN.md section 4.2, phase 1 step 5,
	/// 2026-10-29): the file offsets UW3DModelImport reads, per supported build (the GOG one, size and
	/// hash checked by UWGogInstall). Both games run their models as the same bytecode through the same
	/// kind of interpreter; models 1 to 3 (door frame, bridge, bench) begin with the same words in both
	/// programs.
	///
	/// THE FIRST GAME (UW.EXE, 2026-09-15, from the program itself): the table of 64 model offsets at
	/// seg051:28A0, relative to seg051:2938; the colour registers at DS 0x2920 + 2n; the colour records
	/// of 4 bytes (flags, three colours), 30 of them, at DS:060A.
	///
	/// THE SECOND GAME (UW2.EXE, 2026-10-29): the models in the renderer's far data, UE's segment 052
	/// (its FD58), loaded at paragraph 0x4FAF, file 0x526F0 (Underworld Exhumed, docs/LAYOUT.md; named
	/// without UW1's seg label form, which the rule index reads as a place in UW.EXE). The render
	/// database starts at cDbase, seg052:26A4 (sys/C3DENTRY.ASM); grdb_blank reads the 64 model labels
	/// as byte offsets from cDbase out of the words 178 to 115 before it (conv/GRDB.C), so the table is
	/// at seg052:2600 and its offsets are relative to seg052:26A4; Clk(n) gives the model variable n
	/// 0x24 bytes before cDbase, so the colour registers are seg052 0x2680 + 2n. The colour records
	/// (rect_cols, 3d/DRAWOBJ.C) are 5 bytes each - flags, three colours, the first texture and the
	/// texture count - 32 of them, followed by the 32 words of the model drawn for each 3D object type
	/// (rect_sub); found in the GOG UW2.EXE by their bytes, once each.
	/// </summary>
	public sealed class UW3DModelLayout
	{
		public const int ModelCount = 64;

		public const int Uw1TableStart = 0x4CCD0;

		public const int Uw1CodeBase = 0x4CD68;

		public const int Uw1ColorRegisterBase = 0x2920;

		public const int Uw1ColorRecordsStart = 0x5E2CA;

		public const int Uw1ColorRecordSize = 4;

		public const int Uw1ColorRecordCount = 30;

		/// <summary>The far data segment's start in the file: the header's 0x2C00 and paragraph 0x4FAF.</summary>
		public const int Uw2ModelSegment = 0x526F0;

		public const int Uw2TableStart = Uw2ModelSegment + 0x2600;

		public const int Uw2CodeBase = Uw2ModelSegment + 0x26A4;

		public const int Uw2ColorRegisterBase = 0x2680;

		public const int Uw2ColorRecordsStart = 0x6908A;

		public const int Uw2ColorRecordSize = 5;

		public const int Uw2ColorRecordCount = 32;

		/// <summary>The model of each 3D object type, items 0x150 + n, a word each (-1 none).</summary>
		public const int Uw2ObjectModelsStart = Uw2ColorRecordsStart + (Uw2ColorRecordCount * Uw2ColorRecordSize);

		public static readonly UW3DModelLayout Uw1 = new UW3DModelLayout(Uw1TableStart, Uw1CodeBase, Uw1ColorRegisterBase,
			Uw1ColorRecordsStart, Uw1ColorRecordSize, Uw1ColorRecordCount);

		public static readonly UW3DModelLayout Uw2 = new UW3DModelLayout(Uw2TableStart, Uw2CodeBase, Uw2ColorRegisterBase,
			Uw2ColorRecordsStart, Uw2ColorRecordSize, Uw2ColorRecordCount);

		private UW3DModelLayout(int piTableStart, int piCodeBase, int piColorRegisterBase, int piColorRecordsStart,
			int piColorRecordSize, int piColorRecordCount)
		{
			TableStart = piTableStart;
			CodeBase = piCodeBase;
			ColorRegisterBase = piColorRegisterBase;
			ColorRecordsStart = piColorRecordsStart;
			ColorRecordSize = piColorRecordSize;
			ColorRecordCount = piColorRecordCount;
		}

		/// <summary>The file offset of the 64 model offsets.</summary>
		public int TableStart { get; }

		/// <summary>The file offset the model offsets count from.</summary>
		public int CodeBase { get; }

		/// <summary>The address a colour opcode names register 0 by; register n is 2n above.</summary>
		public int ColorRegisterBase { get; }

		public int ColorRecordsStart { get; }

		/// <summary>The bytes of one colour record: the flags (bits 0-2 the number of colours), then the colours.</summary>
		public int ColorRecordSize { get; }

		public int ColorRecordCount { get; }
	}
}
