using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A cutscene control file (cs*.n00) from the "cuts" folder, uw-formats.txt 3.7.1. The
	/// .n00 files do NOT follow the animation format of the other files, but contain
	/// instructions: from which frame which text appears, when to wait, fade or
	/// open another image file - and which speech recording plays along.
	///
	/// Layout: entries of variable length, each entry two 16-bit values (frame number
	/// and command) followed by as many arguments as the command needs.
	///
	/// The most important command for us is 0x0D (text-play): it names text colour, string number
	/// AND sound number in one. That way the mapping speech file -> spoken
	/// sentence can be derived from the data instead of guessed.
	/// </summary>
	public class UWCutsceneScript
	{
		public enum Command
		{
			ShowText = 0x00,
			/// <summary>cutsop_erase: the subtitle goes (flag39 = 0); the recording plays on.</summary>
			SetFlag = 0x01,
			NoOp = 0x02,
			Pause = 0x03,
			ToFrame = 0x04,
			SetStaticFrame = 0x05,
			EndCutscene = 0x06,
			RepeatSegment = 0x07,
			OpenFile = 0x08,
			FadeOut = 0x09,
			FadeIn = 0x0A,
			Unknown0B = 0x0B,
			/// <summary>cutsop_punt(flag): whether Escape may end the cutscene (b4, 1 at the start).</summary>
			Unknown0C = 0x0C,
			TextAndPlay = 0x0D,
			Unknown0E = 0x0E,
			Chime = 0x0F,

			// THE SECOND GAME'S (uw2 gfx/CUTS.C cuts_dispatch, UW2 plan phase 5 part C): 5 is next there, without an
			// argument; 16 to 27 are new.
			/// <summary>palrange(first, count, rgb...): palette entries from the script (6-bit).</summary>
			PaletteRange = 0x10,
			/// <summary>palfade(period, total, first, count, rgb...): a timed fade of those entries towards the script's.</summary>
			PaletteFade = 0x11,
			/// <summary>palset: nothing in DOS but its four words.</summary>
			PaletteSet = 0x12,
			/// <summary>palsimplefade(pal, period, total): the whole palette towards PALS.DAT's pal.</summary>
			PaletteSimpleFade = 0x13,
			/// <summary>vscreen(w, h, split): a virtual screen; 320, 200, 999 the plain one.</summary>
			VirtualScreen = 0x14,
			/// <summary>focus(x, y): the point of the virtual screen shown (y counted up from the bottom).</summary>
			Focus = 0x15,
			/// <summary>lback(x, y, n): CUTS\LBACKnnn.BYT into the virtual screen.</summary>
			Back = 0x16,
			/// <summary>pan(dir, step, count): the focus moves step pixels a frame for count frames.</summary>
			Pan = 0x17,
			/// <summary>splity(y): the row the subtitle bar begins at, 999 none.</summary>
			SplitY = 0x18,
			/// <summary>music(theme), 0 stops it.</summary>
			Music = 0x19,
			/// <summary>test: nothing.</summary>
			Test = 0x1A,
			/// <summary>wait_for_sound(n): until the speech ends, cut off after n * 256 ticks.</summary>
			WaitForSound = 0x1B
		}

		public struct Entry
		{
			public int Frame;

			public Command Command;

			public int[] Arguments;
		}

		/// <summary>Number of arguments per command, per uw-formats.txt 3.7.1. Without this
		/// table the file cannot be walked - the entries have
		/// different lengths.</summary>
		private static readonly int[] miArgumentCount =
		{
			2, 0, 2, 1, 2, 1, 0, 1, 2, 1, 1, 1, 1, 3, 2, 0
		};

		/// <summary>The second game's (each cutsop_ returns how many words it used, uw2 gfx/CUTS.C): 5 takes none;
		/// 16 and 17 are VariableArguments.</summary>
		private static readonly int[] miArgumentCountUw2 =
		{
			2, 0, 2, 1, 2, 0, 0, 1, 2, 1, 1, 1, 1, 3, 2, 0, VariableArguments, VariableArguments, 4, 3, 3, 2, 3, 3, 1, 1, 1, 1
		};

		/// <summary>palrange: count * 3 + 2 words (its second word the count); palfade: count * 3 + 4 (its fourth).</summary>
		private const int VariableArguments = -1;

		/// <summary>The game whose table read the file.</summary>
		public UWGame Game { get; private set; } = UWGame.Uw1;

		/// <summary>For show-text and text-play this value means "no text".</summary>
		public const int NoText = 0xFFFF;

		private readonly List<Entry> mOEntries = new List<Entry>();

		public IReadOnlyList<Entry> Entries => mOEntries;

		public bool IsLoaded { get; private set; }

		public UWCutsceneScript(string psFileName)
			: this(psFileName, UWGame.Uw1)
		{
		}

		/// <summary>A script by its game's table (the second game's since UW2 plan phase 5 part C). The second game's
		/// cuts_process_opcodes stops at an opcode of 29 or more (28 is the table's empty end); the parse stops at 28.</summary>
		public UWCutsceneScript(string psFileName, UWGame peGame)
		{
			Game = peGame;

			if (!File.Exists(psFileName))
				return;

			byte[] lyData = File.ReadAllBytes(psFileName);
			int[] liTable = peGame == UWGame.Uw2 ? miArgumentCountUw2 : miArgumentCount;
			int liCursor = 0;

			while (liCursor + 3 < lyData.Length)
			{
				int liFrame = lyData[liCursor] | (lyData[liCursor + 1] << 8);
				int liCommand = lyData[liCursor + 2] | (lyData[liCursor + 3] << 8);
				liCursor += 4;

				if (liCommand < 0 || liCommand >= liTable.Length)
					break;

				int liCount = liTable[liCommand];

				if (liCount == VariableArguments)
				{
					int liCountWord = liCommand == (int)Command.PaletteRange ? 1 : 3;

					if (liCursor + (liCountWord * 2) + 1 >= lyData.Length)
						break;

					int liEntries = lyData[liCursor + (liCountWord * 2)] | (lyData[liCursor + (liCountWord * 2) + 1] << 8);

					liCount = (liEntries * 3) + liCountWord + 1;
				}
				int[] liArguments = new int[liCount];

				for (int liIndex = 0; liIndex < liCount; liIndex++)
				{
					if (liCursor + 1 >= lyData.Length)
						break;

					liArguments[liIndex] = lyData[liCursor] | (lyData[liCursor + 1] << 8);
					liCursor += 2;
				}

				mOEntries.Add(new Entry
				{
					Frame = liFrame,
					Command = (Command)liCommand,
					Arguments = liArguments
				});
			}

			UnreadBytes = lyData.Length - liCursor;
			IsLoaded = mOEntries.Count > 0;
		}

		/// <summary>The bytes left after the last whole entry (a file read to its end leaves none).</summary>
		public int UnreadBytes { get; private set; }

		/// <summary>All speech output of this cutscene as pairs of string number and
		/// sound number. 0xFFFF as string number means "sound without text".</summary>
		public List<KeyValuePair<int, int>> GetSpokenLines()
		{
			List<KeyValuePair<int, int>> lOResult = new List<KeyValuePair<int, int>>();

			foreach (Entry lOEntry in mOEntries)
			{
				if (lOEntry.Command != Command.TextAndPlay || lOEntry.Arguments.Length < 3)
					continue;

				lOResult.Add(new KeyValuePair<int, int>(lOEntry.Arguments[1], lOEntry.Arguments[2]));
			}

			return lOResult;
		}
	}
}
