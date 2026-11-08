using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE THE FIRST GAME TEACHES ITS MANTRAS AND HIDDEN SPELLS (GitHub issue 19; found with `uwdump
	/// typed-words` and `uwdump find`, 2026-11-07, the special cases per user): every string that names one,
	/// by block and number in our numbering (as `uwdump strings` prints it) - block 3 the books and scrolls,
	/// block 8 the writings, 3584 + slot the conversations. Mantras by UWShrineRules' number, spells by
	/// UWRunicMagic's index. UWKnowledge applies them.
	///
	///   - Mantras: writings 8:71-73 (Summ Ra, Mu Ahm, Om Cah), 8:140 ORA, 8:141 ANRA; books 3:34-37 (RA, LU,
	///     UN, HUNN), 3:69 SAHF, 3:71 FAL, 3:72 ONO, 3:129 AAM, 3:130 MUL, 3:132 ROMM, 3:134 LAHN, 3:175 IMU,
	///     3:176 SOL, 3:172 FANLO (only after Gurstang's word, 3801:8 "Tell Illomo 'Folanae.'" or 3801:9 "...say this word: 'Folanae.'" - the
	///     two ways his talk goes, per user 2026-11-08); the knights
	///     3730-3733 (AMO, GAR, KOH, FAHM, lines 34-37 each); 3586:9 LON. INSAHN in no string: its pieces are
	///     the incense visions' (0x0B "in" at dawn, 0x0C "sa" at midday, 0x0D "hn" in the evening - read off
	///     their pictures 2026-11-08; the help keeps only the letters, per user).
	///   - The 8 spells the manual leaves out: 3:5 and 3:197 Leap (5); 3:70 Water Walk (18, the lizardman);
	///     3:133 Smite Undead (25); 3898:11 Armageddon (47); 3:112 "Finally, I can cast Curse!" (6, with the
	///     pouch of An and Sanct beside it - UWKnowledge.SpellState); 3:171 Summon Monster's riddle "Kal...
	///     Mani perhaps?" (38, half known as "Kal ...?"); 3609:13 the mage who forgot Sheet Lightning (31, half
	///     known, no runes) and 3609:16 the same door giving its runes when asked (Vas, Ort and Grav - fully
	///     known; found 2026-11-08), and 3609:38 when asked again on a later visit ("Again!? For the last
	///     time..." - reached without 13, per user's save); 3:38 "Remove Bet from a Resist Blows spell for better protection." (17
	///     Thick Skin, half known - the player works out In Sanct; per user 2026-11-08).
	/// </summary>
	public static class UWKnowledgeSources
	{
		public const int BookBlock = 3;

		/// <summary>FANLO's book, "A volume in a foreign tongue, entitled 'Folanae Fanlo'" (3:172) - the help quotes
		/// it until FANLO was chanted (per user 2026-11-08).</summary>
		public const int FanloBook = 172;

		public const int WritingBlock = 8;

		public const int CurseSpell = 6;

		public const int SummonMonsterSpell = 38;

		public const int SheetLightningSpell = 31;

		/// <summary>Thick Skin (In Sanct): half known from the scroll that says to drop Bet from Resist Blows (3:38).</summary>
		public const int ThickSkinSpell = 17;

		/// <summary>The talking door's conversation (slot 25, level 6 at 4/2): the game names it only "Door";
		/// what it teaches is kept with DoorWho, and the help says who it really is (per user 2026-11-08).</summary>
		public const int DoorStringBlock = 3609;

		/// <summary>The "who" kept for the talking door instead of its name.</summary>
		public const string DoorWho = "door";

		/// <summary>Gurstang gave the word "Folanae" (3801:8 after Illomo's name, 3801:9 after "I, too, seek the Key").</summary>
		public const string FolanaeFlag = "folanae";

		/// <summary>Curse's scroll was read (3:112).</summary>
		public const string CurseReadFlag = "curse";

		public enum KindEnum
		{
			Mantra,
			Spell,
			HalfSpell,
			Flag
		}

		public struct Source
		{
			public KindEnum Kind;

			public int Id;

			public string Flag;
		}

		/// <summary>The spells the manual leaves out, the ones the help counts as still to find.</summary>
		public static readonly int[] HiddenSpells = { 5, 6, 17, 18, 25, 31, 38, 47 };

		/// <summary>The mantras the manual leaves out: every one but the three group mantras and NO.</summary>
		public static bool IsHiddenMantra(int piMantra)
		{
			return piMantra >= 0 && piMantra < UWShrineRules.FirstSpecialMantra + 2;
		}

		/// <summary>What a half-known spell already shows of its runes: Summon Monster's first; nothing for the
		/// others.</summary>
		public static string HalfRunes(int piSpell)
		{
			return piSpell == SummonMonsterSpell ? "Kal ...?" : "?";
		}

		private static readonly Dictionary<long, Source[]> msSources = fBuild();

		/// <summary>What a shown string teaches; empty for most strings.</summary>
		public static Source[] Of(int piBlock, int piIndex)
		{
			return msSources.TryGetValue(fKey(piBlock, piIndex), out Source[] lOSources) ? lOSources : msNone;
		}

		private static readonly Source[] msNone = new Source[0];

		/// <summary>INSAHN's piece an incense vision shows, null for any other cutscene.</summary>
		public static string PieceOf(int piCutscene)
		{
			switch (piCutscene)
			{
				case 0x0B: return "IN";
				case 0x0C: return "SA";
				case 0x0D: return "HN";
				default: return null;
			}
		}

		private static long fKey(int piBlock, int piIndex)
		{
			return ((long)piBlock << 16) | (uint)piIndex;
		}

		private static Dictionary<long, Source[]> fBuild()
		{
			Dictionary<long, Source[]> lOTable = new Dictionary<long, Source[]>();

			void fMantra(int piBlock, int piIndex, int piMantra) => fAdd(lOTable, piBlock, piIndex, new Source { Kind = KindEnum.Mantra, Id = piMantra });

			void fSpell(int piBlock, int piIndex, int piSpell) => fAdd(lOTable, piBlock, piIndex, new Source { Kind = KindEnum.Spell, Id = piSpell });

			// The writings.
			fMantra(8, 71, 23);
			fMantra(8, 72, 24);
			fMantra(8, 73, 25);
			fMantra(8, 140, 2);
			fMantra(8, 141, 1);

			// The books and scrolls.
			fMantra(3, 34, 0);
			fMantra(3, 35, 11);
			fMantra(3, 36, 15);
			fMantra(3, 37, 18);
			fMantra(3, 69, 12);
			fMantra(3, 71, 17);
			fMantra(3, 72, 19);
			fMantra(3, 129, 16);
			fMantra(3, 130, 13);
			fMantra(3, 132, 10);
			fMantra(3, 134, 8);
			fMantra(BookBlock, FanloBook, UWShrineRules.KeyOfTruthMantra);
			fMantra(3, 175, 7);
			fMantra(3, 176, 9);

			// The knights, each of the four with the four lines.
			for (int liBlock = 3730; liBlock <= 3733; liBlock++)
			{
				fMantra(liBlock, 34, 3);
				fMantra(liBlock, 35, 4);
				fMantra(liBlock, 36, 5);
				fMantra(liBlock, 37, 6);
			}

			fMantra(3586, 9, 14);

			// The spells.
			fSpell(3, 5, 5);
			fSpell(3, 197, 5);
			fSpell(3, 70, 18);
			fSpell(3, 133, 25);
			fSpell(3898, 11, 47);
			fAdd(lOTable, 3, 171, new Source { Kind = KindEnum.HalfSpell, Id = SummonMonsterSpell });
			fAdd(lOTable, 3609, 13, new Source { Kind = KindEnum.HalfSpell, Id = SheetLightningSpell });
			fSpell(3609, 16, SheetLightningSpell);
			fSpell(3609, 38, SheetLightningSpell);
			fAdd(lOTable, 3, 38, new Source { Kind = KindEnum.HalfSpell, Id = ThickSkinSpell });
			fAdd(lOTable, 3, 112, new Source { Kind = KindEnum.Flag, Flag = CurseReadFlag });
			fAdd(lOTable, 3801, 8, new Source { Kind = KindEnum.Flag, Flag = FolanaeFlag });
			fAdd(lOTable, 3801, 9, new Source { Kind = KindEnum.Flag, Flag = FolanaeFlag });

			// THE LIZARD WORDS (GitHub issue 20, 2026-11-08): Murgo's mime for a word teaches it (UWLizardWords).
			foreach (UWLizardWords.Word lOWord in UWLizardWords.Words)
			{
				if (lOWord.MimeIndex > 0)
					fAdd(lOTable, UWLizardWords.MurgoBlock, lOWord.MimeIndex, new Source { Kind = KindEnum.Flag, Flag = lOWord.Flag });
			}

			// His name guessed right teaches urgo as well.
			fAdd(lOTable, UWLizardWords.MurgoBlock, UWLizardWords.NameGuessedIndex,
				new Source { Kind = KindEnum.Flag, Flag = UWLizardWords.FlagPrefix + UWLizardWords.NameWord });

			return lOTable;
		}

		private static void fAdd(Dictionary<long, Source[]> pOTable, int piBlock, int piIndex, Source pOSource)
		{
			long llKey = fKey(piBlock, piIndex);

			if (!pOTable.TryGetValue(llKey, out Source[] lOOld))
			{
				pOTable[llKey] = new[] { pOSource };

				return;
			}

			Source[] lONew = new Source[lOOld.Length + 1];

			lOOld.CopyTo(lONew, 0);
			lONew[lOOld.Length] = pOSource;
			pOTable[llKey] = lONew;
		}
	}
}
