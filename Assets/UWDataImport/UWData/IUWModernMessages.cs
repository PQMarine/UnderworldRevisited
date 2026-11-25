using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE MODERN INTERFACE ASKS OF THE GAME'S MESSAGES (Docs/UW2-PLAN.md, "The modern interface for UW2:
	/// one interface, two hosts", stage U1, 2026-11-23): the lines the game said, for the message box and the
	/// original's scroll in its place. The first game's host answers from Interaction's message log, the
	/// second's from its screen's scroll. How many lines the box shows is the player's setting, not the game's.
	/// </summary>
	public interface IUWModernMessages
	{
		/// <summary>The lines kept, the newest last, with the game's colour codes in them.</summary>
		IReadOnlyList<string> Lines { get; }

		/// <summary>When each line was written, on the clock the box fades by (unscaled seconds); may be shorter
		/// than Lines, a line without a time does not fade.</summary>
		IReadOnlyList<float> LineTimes { get; }

		/// <summary>The game waits for a page: [MORE] shows and nothing fades.</summary>
		bool IsWaitingForPage { get; }

		/// <summary>The line typed input runs on (a mantra, "Move how many?"), which keeps while the others
		/// fade; -1 for none.</summary>
		int PromptLine { get; }

		/// <summary>Typed input shows its cursor at the end of the lines.</summary>
		bool ShowsPromptCursor { get; }

		/// <summary>The game asks a yes or no question now (stage U2: the use mode's end waits for its answer).</summary>
		bool IsAskingYesNo { get; }

		/// <summary>The question's text (stage U3 part C: the question box), null for none.</summary>
		string YesNoQuestion { get; }

		void AnswerYesNo(bool pbYes);

		/// <summary>A mantra is being typed at a shrine.</summary>
		bool IsChanting { get; }

		/// <summary>The line the game asks the mantra with.</summary>
		string MantraPrompt { get; }

		/// <summary>The mantra typed so far.</summary>
		string MantraText { get; }

		/// <summary>The mantra is done: chanted, or given up with pbCancel.</summary>
		void FinishMantra(bool pbCancel);

		/// <summary>Typed input or a question runs in the messages (the interface does not hide meanwhile).</summary>
		bool IsPromptActive { get; }

		/// <summary>A line of the windows' own into the game's messages.</summary>
		void Say(string psMessage);

		/// <summary>One of the game's general messages (its string block's number) into the messages.</summary>
		void SayGeneral(int piIndex);

		/// <summary>The text of one of the game's general messages, for a window that shows it itself.</summary>
		string General(int piIndex);
	}
}
