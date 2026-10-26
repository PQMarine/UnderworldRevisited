using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ENGLISH TEXT TABLE of the port's own texts (cleanup step 8.5, 2026-10-24): the default
	/// and the fallback of every id UWText.T and UWText.F know. One file per area, each a list of
	/// fAdd(id, text) in the order the texts appear on screen:
	///
	///   setting.*  the setting rows' labels, hints and choices (UWUserSettingsTable, ids of step 8.4)
	///   layout.*   the layout editor's Preset row (UWLayoutPresets.StartIds, ids of step 8.4)
	///   setup.*    the setup bar, its dialogs and the folder dialog
	///   keys.*     the names of the changeable keys (UWKeyBindings.Entries)
	///   pad.*      the gamepad: its changeable buttons, the button names, the hints at the screen edge
	///   help.*     the help window, its tabs and the controls guide
	///   editor.*   the layout editor and its inspector, the names of the interface's parts
	///   menu.*     the modern game menu
	///   hud.*      the modern interface's own words, and the few prompts the port adds itself
	///   effects.*  the Remastered effects window (graphics detail Custom, Very high in the classic options)
	///   grid.*     the gamepad's letter grid
	///   grammar.*  the look sentence's frames and the words of UW.EXE (IUWGrammar, translation wave 2)
	///   msg.*      the message lines UW.EXE assembles around a name or from pieces of the strings,
	///              and its fixed lines (translation step 3A)
	///
	/// The words are the ones the code showed until this step, unchanged; a text with {0} is used
	/// through UWText.F with as many arguments (the self-check counts them in the code).
	/// </summary>
	public static partial class UWTextEnglish
	{
		/// <summary>Id to English text.</summary>
		public static readonly Dictionary<string, string> Table = new Dictionary<string, string>();

		/// <summary>The ids in the order they were added.</summary>
		public static readonly List<string> Order = new List<string>();

		/// <summary>Ids added twice (the later text counts); the self-check wants none.</summary>
		public static readonly List<string> Twice = new List<string>();

		static UWTextEnglish()
		{
			fSettings();
			fSetup();
			fKeys();
			fPad();
			fHelp();
			fEditor();
			fMenu();
			fHud();
			fEffects();
			fGrid();
			fGrammar();
			fMessages();
		}

		private static void fAdd(string psId, string psText)
		{
			if (Table.ContainsKey(psId))
				Twice.Add(psId);
			else
				Order.Add(psId);

			Table[psId] = psText;
		}
	}
}
