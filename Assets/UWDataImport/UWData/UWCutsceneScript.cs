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
			Unknown0C = 0x0C,
			TextAndPlay = 0x0D,
			Unknown0E = 0x0E,
			Chime = 0x0F
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

		/// <summary>For show-text and text-play this value means "no text".</summary>
		public const int NoText = 0xFFFF;

		private readonly List<Entry> mOEntries = new List<Entry>();

		public IReadOnlyList<Entry> Entries => mOEntries;

		public bool IsLoaded { get; private set; }

		public UWCutsceneScript(string psFileName)
		{
			if (!File.Exists(psFileName))
				return;

			byte[] lyData = File.ReadAllBytes(psFileName);
			int liCursor = 0;

			while (liCursor + 3 < lyData.Length)
			{
				int liFrame = lyData[liCursor] | (lyData[liCursor + 1] << 8);
				int liCommand = lyData[liCursor + 2] | (lyData[liCursor + 3] << 8);
				liCursor += 4;

				if (liCommand < 0 || liCommand >= miArgumentCount.Length)
					break;

				int liCount = miArgumentCount[liCommand];
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

			IsLoaded = mOEntries.Count > 0;
		}

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
