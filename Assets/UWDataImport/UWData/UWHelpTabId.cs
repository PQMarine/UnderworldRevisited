namespace UWDataImport.UWData
{
	/// <summary>
	/// THE HELP WINDOW'S TABS BY THEIR SAVED NUMBERS (cleanup 8.6, 2026-10-24): settings.json keeps
	/// the tab the player left open as a plain number (UWUserData.HelpTab), so these numbers are
	/// part of the file and are never changed or reordered (the plan's frozen list, section 11).
	/// Until then they were the order of the help window's private TabEnum; now each tab is a
	/// class of its own (UWHelpTab) carrying its number, and the numbers live here, free of the
	/// engine, where the self-check pins them.
	/// </summary>
	public enum UWHelpTabId
	{
		Stats = 0,
		Map = 1,
		Notes = 2,
		Spells = 3,
		Mantras = 4,
		Manual = 5,
		Controls = 6
	}

	/// <summary>Reading the saved number back.</summary>
	public static class UWHelpTabIds
	{
		/// <summary>How many tabs there are - the saved numbers run from 0 to Count - 1.</summary>
		public const int Count = 7;

		/// <summary>The tab a fresh settings file opens on (UWUserData.HelpTab's 3), and the one the
		/// window stays on when the saved number is out of range.</summary>
		public const UWHelpTabId Default = UWHelpTabId.Spells;

		/// <summary>The tab of a saved number; one out of range keeps the default, as the window's
		/// Awake did before (it only took numbers 0 to 6).</summary>
		public static UWHelpTabId FromSaved(int piSaved)
		{
			return piSaved >= 0 && piSaved < Count ? (UWHelpTabId)piSaved : Default;
		}
	}
}
