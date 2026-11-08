using System;
using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>The setting LizardWords: the lizardmen's words with their meanings in brackets.</summary>
	public enum UWLizardWordsEnum
	{
		/// <summary>As the original: the words as they are.</summary>
		Off = 0,

		/// <summary>The words Murgo has mimed (UWKnowledge, kept with the save).</summary>
		Learned = 1,

		/// <summary>Every word of the list, Murgo or not (kri'kla is no word of his).</summary>
		All = 2
	}

	/// <summary>
	/// THE LIZARDMEN'S WORDS (GitHub issue 20, per user 2026-11-07; built 2026-11-08): people find the lizard
	/// language tiresome, so an option shows a word's meaning in brackets behind it - "Bica [hello],
	/// sor'click [stranger]." - in the lizardmen's conversations only, on display only: the strings, the
	/// conversation's run and its typed answers stay as they are.
	///
	///   - THE WORDS AND MEANINGS are those of Murgo's lessons (conversation 3608, slot 24: a word's string at
	///     51, 53 ... 91 in our numbering, each followed by his mime), the meanings after
	///     sircabirus.com/uw1walk_lizardmanlanguage.php (per user); kri'kla (lurker) is none of Murgo's and so
	///     shows only with All. "isila" (Iss'leek's slip, 3698:18) reads as isili. The meanings are the port's
	///     texts (lizard.*), so a translator gives them in the language.
	///   - LEARNED: a word counts once Murgo's mime for it was shown (UWKnowledgeSources: the mime string
	///     carries the flag "lizard:&lt;word&gt;", kept in UWR.json with the mantras and spells).
	///   - THE CONVERSATIONS: Sseetharee (slot 15), Ishtass (16, who speaks the player's tongue but names the
	///     Thepa and the kri'kla), Iss'leek (114) and the lizardmen's own (277, 280, 281) - the partner's lines
	///     and the player's answers. Murgo's own lessons keep their words bare.
	///   - MATCHING: whole words, case ignored, the apostrophe and the hyphen part of a word - "'click" inside
	///     "sor'click", "thes'click" or "'click-iriass" is not the word 'click.
	/// The first game only.
	/// </summary>
	public static class UWLizardWords
	{
		public sealed class Word
		{
			/// <summary>The word as the strings write it, lower case.</summary>
			public string Text;

			/// <summary>The meaning's text id (lizard.*).</summary>
			public string TextId;

			/// <summary>Murgo's mime for it in conversation 3608, our numbering; 0 for none.</summary>
			public int MimeIndex;

			/// <summary>The knowledge flag of a learned word (UWKnowledge).</summary>
			public string Flag => FlagPrefix + Text;
		}

		/// <summary>Murgo's conversation: his lessons' strings.</summary>
		public const int MurgoBlock = 3608;

		public const string FlagPrefix = "lizard:";

		/// <summary>
		/// HIS NAME GUESSED teaches urgo too (per user, 2026-11-08): the player who types "Murgo" in the
		/// name-guessing (compared with string 23, our numbering) sees string 28, "He nods his head excitedly",
		/// printed there only - so the word for Murgo is known without the lesson.
		/// </summary>
		public const int NameIndex = 23;

		public const int NameGuessedIndex = 28;

		public const string NameWord = "urgo";

		private static Word fWord(string psText, string psId, int piMime) => new Word { Text = psText, TextId = psId, MimeIndex = piMime };

		/// <summary>The words in Murgo's order, kri'kla last.</summary>
		public static readonly Word[] Words =
		{
			fWord("sseth", "lizard.sseth", 52), fWord("'click", "lizard.click", 54), fWord("sor'click", "lizard.sor_click", 56),
			fWord("sstresh", "lizard.sstresh", 58), fWord("sorr", "lizard.sorr", 60), fWord("yeshor'click", "lizard.yeshor_click", 62),
			fWord("zekka", "lizard.zekka", 64), fWord("thesh", "lizard.thesh", 66), fWord("'click-iriass", "lizard.click_iriass", 68),
			fWord("thes'click", "lizard.thes_click", 70), fWord("sorra", "lizard.sorra", 72), fWord("yethe", "lizard.yethe", 74),
			fWord("isili", "lizard.isili", 76), fWord("tosa", "lizard.tosa", 78), fWord("urgo", "lizard.urgo", 80),
			fWord("thepa", "lizard.thepa", 82), fWord("bica", "lizard.bica", 84), fWord("thit", "lizard.thit", 86),
			fWord("ossli", "lizard.ossli", 88), fWord("eppa", "lizard.eppa", 90), fWord("sel'a", "lizard.sel_a", 92),
			fWord("kri'kla", "lizard.kri_kla", 0)
		};

		/// <summary>Other spellings in the strings, read as the word given.</summary>
		private static readonly Dictionary<string, string> msVariants = new Dictionary<string, string> { { "isila", "isili" } };

		/// <summary>The lizardmen's conversations, by slot (the first game).</summary>
		public static readonly int[] ConversationSlots = { 15, 16, 114, 277, 280, 281 };

		private static readonly Dictionary<string, Word> msByText = fByText();

		private static Dictionary<string, Word> fByText()
		{
			Dictionary<string, Word> lOMap = new Dictionary<string, Word>(StringComparer.OrdinalIgnoreCase);

			foreach (Word lOWord in Words)
				lOMap[lOWord.Text] = lOWord;

			return lOMap;
		}

		/// <summary>Where the setting is read (the Unity layer's settings; the self-check its own); null reads Off.</summary>
		public static Func<UWLizardWordsEnum> ModeSource { get; set; }

		public static UWLizardWordsEnum Mode => ModeSource == null ? UWLizardWordsEnum.Off : ModeSource();

		/// <summary>Whether a conversation slot is one of the lizardmen's.</summary>
		public static bool IsLizardConversation(int piSlot)
		{
			return Array.IndexOf(ConversationSlots, piSlot) >= 0;
		}

		/// <summary>The word of a string, or null: case ignored, a known other spelling read as its word.</summary>
		public static Word Find(string psToken)
		{
			if (string.IsNullOrEmpty(psToken))
				return null;

			if (msVariants.TryGetValue(psToken.ToLowerInvariant(), out string lsWord))
				psToken = lsWord;

			return msByText.TryGetValue(psToken, out Word lOWord) ? lOWord : null;
		}

		/// <summary>The word Murgo's mime string stands for, or null.</summary>
		public static Word OfMime(int piIndex)
		{
			foreach (Word lOWord in Words)
			{
				if (lOWord.MimeIndex == piIndex && piIndex > 0)
					return lOWord;
			}

			return null;
		}

		/// <summary>
		/// A line with each word pKnown accepts followed by its meaning in brackets ("Bica [hello]."); the line
		/// itself where none is known. A word is a run of letters, apostrophes and hyphens; an apostrophe at its
		/// end belongs to it only where the word is one (a closing quote does not).
		/// </summary>
		public static string Translate(string psLine, Func<Word, bool> pKnown, Func<string, string> pMeaning)
		{
			if (string.IsNullOrEmpty(psLine) || pKnown == null || pMeaning == null)
				return psLine;

			StringBuilder lOOut = null;
			int liAt = 0;

			while (liAt < psLine.Length)
			{
				if (!fIsWordChar(psLine[liAt]))
				{
					lOOut?.Append(psLine[liAt]);
					liAt++;
					continue;
				}

				int liEnd = liAt;

				while (liEnd < psLine.Length && fIsWordChar(psLine[liEnd]))
					liEnd++;

				string lsToken = psLine.Substring(liAt, liEnd - liAt);
				Word lOWord = Find(lsToken) ?? Find(lsToken.Trim('\'', '-'));

				if (lOWord != null && pKnown(lOWord))
				{
					lOOut = lOOut ?? new StringBuilder(psLine.Substring(0, liAt));
					lOOut.Append(lsToken).Append(" [").Append(pMeaning(lOWord.TextId)).Append(']');
				}
				else
					lOOut?.Append(lsToken);

				liAt = liEnd;
			}

			return lOOut == null ? psLine : lOOut.ToString();
		}

		private static bool fIsWordChar(char pcChar)
		{
			return char.IsLetter(pcChar) || pcChar == '\'' || pcChar == '-';
		}

		/// <summary>The line as the setting shows it in a lizardman's conversation: Off the line itself, Learned the
		/// words the knowledge holds, All every word.</summary>
		public static string Shown(string psLine, UWKnowledge pOKnown)
		{
			UWLizardWordsEnum leMode = Mode;

			if (leMode == UWLizardWordsEnum.Off)
				return psLine;

			return Translate(psLine, lOWord => leMode == UWLizardWordsEnum.All || (pOKnown != null && pOKnown.HasFlag(lOWord.Flag)),
				psId => UWText.T(psId));
		}
	}
}
