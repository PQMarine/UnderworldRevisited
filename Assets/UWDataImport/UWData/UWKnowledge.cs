using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE CHARACTER HAS LEARNED (GitHub issue 19, 2026-11-08; an idea from an RPG Codex post the user
	/// wanted too): the mantras and the spells the manual does not name, learned in the game instead of
	/// looked up, for the help's Mantras and Spells tabs. The first game only.
	///
	/// LEARNED when the game shows a string that names one (a book or scroll read, a writing looked at, a
	/// line said in a conversation - UWKnowledgeSources, keyed by the string's NUMBER, never by the word:
	/// RA, UN and NO stand in hundreds of strings), or when a mantra is chanted at a shrine or a spell cast.
	/// Some are only HALF known from their text: a riddle (Summon Monster, "Kal ...?"), a spell named
	/// without its runes (Sheet Lightning), Curse read without An and Sanct in the bag. INSAHN comes in
	/// PIECES from the three incense visions, kept in the order seen; the order to chant them in is the
	/// player's puzzle (the visions' times of day), the help never says it (per user). FANLO's book teaches
	/// it only after Gurstang gave the word (per user).
	///
	/// Kept with each save in our UWR.json (UWHelpNotes) as plain lines - the original's save untouched.
	/// Current is the game in play's; a load or a new character replaces it.
	/// </summary>
	public sealed class UWKnowledge
	{
		/// <summary>The game in play's knowledge.</summary>
		public static UWKnowledge Current { get; set; } = new UWKnowledge();

		public enum SourceEnum
		{
			None,
			Book,
			Writing,
			Talk,
			Chant,
			Cast,
			Vision
		}

		/// <summary>One thing learned: how, and from whom for a conversation.</summary>
		public struct Entry
		{
			public SourceEnum Source;

			public string Who;
		}

		private readonly Dictionary<int, Entry> mOMantras = new Dictionary<int, Entry>();

		private readonly Dictionary<int, Entry> mOSpells = new Dictionary<int, Entry>();

		private readonly HashSet<int> mOHalfSpells = new HashSet<int>();

		private readonly List<int> mOPieces = new List<int>();

		private readonly HashSet<string> mOFlags = new HashSet<string>();

		/// <summary>Counts every change, for whoever caches a view of it.</summary>
		public int Version { get; private set; }

		// ------------------------------------------------- What the game shows

		/// <summary>A string of block piBlock, number piIndex (our numbering, as `uwdump strings` prints it)
		/// was shown; psWho names the conversation partner, null elsewhere.</summary>
		public void SawString(int piBlock, int piIndex, string psWho)
		{
			SourceEnum leSource = piBlock == UWKnowledgeSources.BookBlock ? SourceEnum.Book
				: piBlock == UWKnowledgeSources.WritingBlock ? SourceEnum.Writing : SourceEnum.Talk;

			foreach (UWKnowledgeSources.Source lOSource in UWKnowledgeSources.Of(piBlock, piIndex))
			{
				switch (lOSource.Kind)
				{
					case UWKnowledgeSources.KindEnum.Mantra:
						// FANLO's book means something only once Gurstang has given the word.
						if (lOSource.Id == UWShrineRules.KeyOfTruthMantra && !mOFlags.Contains(UWKnowledgeSources.FolanaeFlag))
							break;

						fLearnMantra(lOSource.Id, leSource, psWho);
						break;

					case UWKnowledgeSources.KindEnum.Spell:
						fLearnSpell(lOSource.Id, leSource, psWho);
						break;

					case UWKnowledgeSources.KindEnum.HalfSpell:
						if (!mOSpells.ContainsKey(lOSource.Id) && mOHalfSpells.Add(lOSource.Id))
							Version++;
						break;

					case UWKnowledgeSources.KindEnum.Flag:
						if (mOFlags.Add(lOSource.Flag))
							Version++;
						break;
				}
			}
		}

		/// <summary>A cutscene was played: an incense vision shows a piece of INSAHN.</summary>
		public void SawCutscene(int piCutscene)
		{
			if (UWKnowledgeSources.PieceOf(piCutscene) == null || mOPieces.Contains(piCutscene))
				return;

			mOPieces.Add(piCutscene);
			Version++;
		}

		/// <summary>A mantra (UWShrineRules' number) was chanted and recognised at a shrine.</summary>
		public void Chanted(int piMantra)
		{
			if (piMantra < 0 || piMantra == UWShrineRules.EmptyMantra)
				return;

			fLearnMantra(piMantra, SourceEnum.Chant, null);

			// FANLO's plain meaning shows only once it was chanted (per user 2026-11-08).
			if (piMantra == UWShrineRules.KeyOfTruthMantra && mOFlags.Add(FanloChantedFlag))
				Version++;
		}

		/// <summary>FANLO was chanted at a shrine (a flag, kept as "f fanlo-chanted").</summary>
		public const string FanloChantedFlag = "fanlo-chanted";

		/// <summary>Whether FANLO was chanted at a shrine - the help then says what it does.</summary>
		public bool ChantedFanlo => mOFlags.Contains(FanloChantedFlag);

		/// <summary>Whether a flag is set (a lizard word learned from Murgo, UWLizardWords).</summary>
		public bool HasFlag(string psFlag) => psFlag != null && mOFlags.Contains(psFlag);

		/// <summary>A rune spell (UWRunicMagic's index) was cast and took effect.</summary>
		public void Cast(int piSpell)
		{
			fLearnSpell(piSpell, SourceEnum.Cast, null);
		}

		private void fLearnMantra(int piMantra, SourceEnum peSource, string psWho)
		{
			if (mOMantras.ContainsKey(piMantra))
				return;

			mOMantras[piMantra] = new Entry { Source = peSource, Who = psWho };
			Version++;
		}

		private void fLearnSpell(int piSpell, SourceEnum peSource, string psWho)
		{
			if (mOSpells.ContainsKey(piSpell))
				return;

			mOSpells[piSpell] = new Entry { Source = peSource, Who = psWho };
			mOHalfSpells.Remove(piSpell);
			Version++;
		}

		// ------------------------------------------------- What the help asks

		public bool KnowsMantra(int piMantra, out Entry pOEntry)
		{
			return mOMantras.TryGetValue(piMantra, out pOEntry);
		}

		public enum SpellStateEnum
		{
			Unknown,
			Half,
			Known
		}

		/// <summary>
		/// How well a spell is known. CURSE (per user): its scroll 3:112 says only the name; the pouch beside
		/// it holds An and Sanct - known once the scroll is read AND both runes are in the bag (pOBag), half
		/// known when read without them. The bag is asked now, not tracked: it says the spell can be cast.
		/// </summary>
		public SpellStateEnum SpellState(int piSpell, UWPlayerData pOBag, out Entry pOEntry)
		{
			if (mOSpells.TryGetValue(piSpell, out pOEntry))
				return SpellStateEnum.Known;

			if (piSpell == UWKnowledgeSources.CurseSpell && mOFlags.Contains(UWKnowledgeSources.CurseReadFlag))
			{
				pOEntry = new Entry { Source = SourceEnum.Book };

				return pOBag != null && pOBag.HasRune(UWPlayerData.Rune.An) && pOBag.HasRune(UWPlayerData.Rune.Sanct)
					? SpellStateEnum.Known : SpellStateEnum.Half;
			}

			pOEntry = default;

			return mOHalfSpells.Contains(piSpell) ? SpellStateEnum.Half : SpellStateEnum.Unknown;
		}

		/// <summary>INSAHN's pieces in the order seen ("IN", "SA", "HN").</summary>
		public List<string> Pieces()
		{
			List<string> lOPieces = new List<string>();

			foreach (int liCutscene in mOPieces)
				lOPieces.Add(UWKnowledgeSources.PieceOf(liCutscene));

			return lOPieces;
		}

		// ------------------------------------------------- Kept with the save

		/// <summary>The knowledge as lines for UWR.json: "m 7 Book", "m 3 Talk Sir Owen", "s 5 Cast",
		/// "h 38", "p 13", "f folanae".</summary>
		public string[] Export()
		{
			List<string> lOLines = new List<string>();

			foreach (KeyValuePair<int, Entry> lOMantra in mOMantras)
				lOLines.Add(fLine("m", lOMantra.Key, lOMantra.Value));

			foreach (KeyValuePair<int, Entry> lOSpell in mOSpells)
				lOLines.Add(fLine("s", lOSpell.Key, lOSpell.Value));

			foreach (int liSpell in mOHalfSpells)
				lOLines.Add("h " + liSpell);

			foreach (int liCutscene in mOPieces)
				lOLines.Add("p " + liCutscene);

			foreach (string lsFlag in mOFlags)
				lOLines.Add("f " + lsFlag);

			return lOLines.ToArray();
		}

		private static string fLine(string psKind, int piId, Entry pOEntry)
		{
			return psKind + " " + piId + " " + pOEntry.Source + (string.IsNullOrEmpty(pOEntry.Who) ? string.Empty : " " + pOEntry.Who);
		}

		/// <summary>The knowledge of UWR.json's lines; a line it cannot read is left out.</summary>
		public static UWKnowledge Import(string[] psLines)
		{
			UWKnowledge lOKnowledge = new UWKnowledge();

			if (psLines == null)
				return lOKnowledge;

			foreach (string lsLine in psLines)
			{
				if (string.IsNullOrEmpty(lsLine))
					continue;

				string[] lsParts = lsLine.Split(new[] { ' ' }, 4);

				if (lsParts.Length < 2)
					continue;

				if (lsParts[0] == "f")
				{
					lOKnowledge.mOFlags.Add(lsParts[1]);
					continue;
				}

				if (!int.TryParse(lsParts[1], out int liId))
					continue;

				switch (lsParts[0])
				{
					case "h":
						lOKnowledge.mOHalfSpells.Add(liId);
						break;

					case "p":
						if (UWKnowledgeSources.PieceOf(liId) != null && !lOKnowledge.mOPieces.Contains(liId))
							lOKnowledge.mOPieces.Add(liId);
						break;

					case "m":
					case "s":
						SourceEnum leSource = SourceEnum.None;

						if (lsParts.Length > 2)
							System.Enum.TryParse(lsParts[2], out leSource);

						Entry lOEntry = new Entry { Source = leSource, Who = lsParts.Length > 3 ? lsParts[3] : null };

						if (lsParts[0] == "m")
							lOKnowledge.mOMantras[liId] = lOEntry;
						else
							lOKnowledge.mOSpells[liId] = lOEntry;
						break;
				}
			}

			return lOKnowledge;
		}
	}
}
