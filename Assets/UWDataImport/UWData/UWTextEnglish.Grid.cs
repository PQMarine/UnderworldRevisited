namespace UWDataImport.UWData
{
	public static partial class UWTextEnglish
	{
		/// <summary>The gamepad's letter grid (UWLetterGrid, cleanup step 8.5, 2026-10-24): the bottom
		/// row's keys and the hint line under them. The letter rows are typed keys and stay in the
		/// code.</summary>
		private static void fGrid()
		{
			fAdd("grid.key.case", "Aa");
			fAdd("grid.key.space", "Space");
			fAdd("grid.key.delete", "Delete");
			fAdd("grid.key.cancel", "Cancel");
			fAdd("grid.key.done", "Done");

			fAdd("grid.hint.type", "type");
			fAdd("grid.hint.delete", "delete");
			fAdd("grid.hint.space", "space");
			fAdd("grid.hint.case", "Aa");
			fAdd("grid.hint.done", "done");
		}
	}
}
