using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE RECORDINGS SAY WHERE THE TEXT DIFFERS (per user, 2026-11-08, heard in the intro with the
	/// subtitles on): the cutscenes' strings were written before or beside the recordings, and some
	/// recordings say it otherwise. A subtitle shown WITH its recording (UWCutsceneRunner,
	/// Timing.SubtitlesWithSpeech) gives what is said; without speech the original's written text stays, as
	/// the original shows it.
	///
	/// NO LINE OF THE GAME IS HELD HERE (per user, the same day, before 0.17.0: the repository holds no text of
	/// the original): each line is known by a check value of its English string and changed by the few words
	/// the recording says otherwise. A line is changed only where the game's string is still exactly that
	/// English one (its check value matches and every word to change is found) - a translation keeps its own
	/// text.
	///
	/// Keys: the cutscene and the string's index in the script (the game's numbering, as UWCutsceneScript
	/// reads it). The check value is FNV-1a over the string without its quotation marks (` ' ") and with its
	/// spaces collapsed, so the raw string (`` '') and the runner's cleaned one (") count alike.
	/// The lines were transcribed with faster-whisper and confirmed by the user, every one but the first
	/// three by listening (2026-11-08).
	/// </summary>
	public static class UWCutsceneSpoken
	{
		/// <summary>One line the recording says otherwise.</summary>
		public sealed class Change
		{
			/// <summary>The game whose cutscene it is (the second game's since UW2 plan phase 5 part D).</summary>
			public UWGame Game = UWGame.Uw1;

			public int Cutscene;

			public int Index;

			/// <summary>The check value of the written English string (see the class).</summary>
			public uint WrittenCheck;

			/// <summary>Pairs: the written words, then what is said instead, each changed once, in order.</summary>
			public string[] Edits;
		}

		private static Change fChange(int piCutscene, int piIndex, uint puCheck, params string[] psEdits)
		{
			return new Change { Cutscene = piCutscene, Index = piIndex, WrittenCheck = puCheck, Edits = psEdits };
		}

		private static Change fChangeUw2(int piCutscene, int piIndex, uint puCheck, params string[] psEdits)
		{
			return new Change { Game = UWGame.Uw2, Cutscene = piCutscene, Index = piIndex, WrittenCheck = puCheck, Edits = psEdits };
		}

		private static readonly Change[] msChanges =
		{
			// The bedroom (CS000, voice 27): "rest", not "sleep".
			fChange(0, 1, 0xDD38333C, "to sleep", "to rest"),

			// The ghost in the dream (CS000, voice 24): the recording ends earlier.
			fChange(0, 5, 0xF27AD806, " upon us", ""),

			// Garamon in the bedroom (voices 37 and 38).
			fChange(0, 9, 0x1A7E7B34, "visitor from", "visitor, and from"),
			fChange(0, 10, 0x59C59D52, "I'd suspect", "Were he not dead, I'd suspect", ", were he alive", ""),
			fChange(0, 11, 0x715B74D7, "Still,", "No matter,", "shalt draw", "shalt serve to draw"),

			// Before Baron Almric (voices 35 and 11) and Corwin at the gate (voice 20).
			fChange(0, 25, 0xF4455148, "piercing blue", "hard gray"),
			fChange(0, 33, 0x3BF9A8B0, "the comings of", "the coming of"),
			fChange(0, 38, 0x6A4241B7, "thy lies", "then thy lies"),
			fChange(0, 40, 0xCF2AEDF8, "until Arial's voice do I hear", "till I hear Arial's voice"),

			// THE SECOND GAME (UW2 plan phase 5 part D; transcribed with faster-whisper and confirmed by the user by
			// listening, 2026-11-17). The first castle dream (CS030, BSP00): one recording for the four lines.
			fChangeUw2(24, 1, 0xACD24175, "friends", "associates"),
			fChangeUw2(24, 2, 0xBC56BD8E, "near Cove", "on Cove"),
			fChangeUw2(24, 3, 0xD89B1117, "Wish thou", "As it is said on your world, wish thou"),

			// The fourth castle dream (CS033, BSP04): the recording begins otherwise and does not say the second line.
			fChangeUw2(27, 0, 0x906E30B3, "I shall", "Patience, Avatar, I shall", ", Avatar.", "."),
			fChangeUw2(27, 1, 0x5FE44E02, WholeLine, string.Empty)
		};

		/// <summary>When a recording reaches a later line it speaks (the dreams' 998 lines), in seconds from its start:
		/// the port's subtitles with speech show the line then, not at the script's frame, which paces the text for
		/// reading without speech (per user 2026-11-17: the dream's lines came some seconds after the voice). Word
		/// times by faster-whisper, 2026-11-17.</summary>
		public sealed class Timing
		{
			public UWGame Game;

			public int Cutscene;

			public int Index;

			public float Seconds;
		}

		private static readonly Timing[] msTimings =
		{
			new Timing { Game = UWGame.Uw2, Cutscene = 24, Index = 1, Seconds = 1.7f },
			new Timing { Game = UWGame.Uw2, Cutscene = 24, Index = 2, Seconds = 5.25f },
			new Timing { Game = UWGame.Uw2, Cutscene = 24, Index = 3, Seconds = 8.7f },
			new Timing { Game = UWGame.Uw2, Cutscene = 25, Index = 1, Seconds = 3.6f }
		};

		/// <summary>The lines a recording speaks after its first, with their times (see Timing).</summary>
		public static IEnumerable<Timing> TimingsOf(UWGame peGame, int piCutscene)
		{
			foreach (Timing lOTiming in msTimings)
			{
				if (lOTiming.Game == peGame && lOTiming.Cutscene == piCutscene)
					yield return lOTiming;
			}
		}

		/// <summary>An edit whose written words are the whole line: the line becomes what is said instead (nothing
		/// where the recording does not say it).</summary>
		public const string WholeLine = "";

		/// <summary>The subtitle for a line spoken by its recording: what is said, where it differs from the
		/// English text and the text is still that English one; else the text as it is.</summary>
		public static string Subtitle(int piCutscene, int piIndex, string psText)
		{
			return Subtitle(UWGame.Uw1, piCutscene, piIndex, psText);
		}

		/// <summary>The same for either game's cutscenes.</summary>
		public static string Subtitle(UWGame peGame, int piCutscene, int piIndex, string psText)
		{
			if (string.IsNullOrEmpty(psText))
				return psText;

			foreach (Change lOChange in msChanges)
			{
				if (lOChange.Game != peGame || lOChange.Cutscene != piCutscene || lOChange.Index != piIndex)
					continue;

				if (Check(psText) != lOChange.WrittenCheck)
					return psText;

				string lsText = psText.Trim();

				if (lOChange.Edits.Length == 2 && lOChange.Edits[0] == WholeLine)
					return lOChange.Edits[1];

				for (int i = 0; i + 1 < lOChange.Edits.Length; i += 2)
				{
					int liAt = lsText.IndexOf(lOChange.Edits[i], System.StringComparison.Ordinal);

					if (liAt < 0)
						return psText;

					lsText = lsText.Substring(0, liAt) + lOChange.Edits[i + 1] + lsText.Substring(liAt + lOChange.Edits[i].Length);
				}

				return lsText;
			}

			return psText;
		}

		/// <summary>The check value of a line: FNV-1a over its characters (low byte, then high byte) without
		/// quotation marks and with the spaces collapsed.</summary>
		public static uint Check(string psText)
		{
			uint luHash = 2166136261;
			bool lbSpace = false;
			bool lbAny = false;

			void fMix(int piByte)
			{
				luHash ^= (uint)(piByte & 0xFF);
				luHash *= 16777619;
			}

			foreach (char lcChar in psText ?? string.Empty)
			{
				if (lcChar == '`' || lcChar == '\'' || lcChar == '"')
					continue;

				if (char.IsWhiteSpace(lcChar))
				{
					lbSpace = lbAny;
					continue;
				}

				if (lbSpace)
				{
					fMix(' ');
					fMix(0);
					lbSpace = false;
				}

				fMix(lcChar);
				fMix(lcChar >> 8);
				lbAny = true;
			}

			return luHash;
		}

		/// <summary>Every changed line, for the check against the game's strings.</summary>
		public static IEnumerable<Change> Changes => msChanges;
	}
}
