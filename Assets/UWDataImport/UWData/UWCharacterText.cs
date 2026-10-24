namespace UWDataImport.UWData
{
	/// <summary>
	/// THE CHARACTER'S WORDS: the class name, the skill names and the level as an ordinal, as
	/// the character pages print them. Until cleanup step 5.6 (2026-10-20) each was written where
	/// it was shown - the class name in the classic panel (UWHudPanel), the modern original stats
	/// page (UWModernStatsPage), the help window (also for the modern panel) and character
	/// creation; the skill names there and at the shrine; the ordinal twice. The places keep their
	/// own spelling (upper case on the original's pages, title case in the help window and the
	/// modern panel); which string, its trimming and the ordinal live here.
	///
	/// String block 2 holds the character words: the class names from UWStringIds.ClassNameBase
	/// (0x18) on, the skill list from UWStringIds.SkillNameBase (32) on - Attack and Defense
	/// first, then the eighteen skills in the order of UWPlayerData.Skill.
	/// </summary>
	public static class UWCharacterText
	{
		/// <summary>String block 2: classes, skills, attributes, mantras.</summary>
		public const int CharacterWordsBlock = 2;

		/// <summary>The entries of the skill list: Attack and Defense, then the eighteen skills -
		/// the twenty the classic stats page and the modern ones page through.</summary>
		public const int SkillEntryCount = 2 + UWPlayerData.SkillCount;

		/// <summary>The name of a character class as the file spells it (upper case on the
		/// original's pages comes from the caller), trimmed; the fallback for no class (below 0)
		/// or no string. Fighter is at 24, not at 23 - the export in AllStrings.txt writes the raw
		/// runtime indices, so there is no offset here. With the old base every class was off by
		/// one (reported per user, 2026-09-03).</summary>
		public static string ClassName(UWStrings pOStrings, int piClass, string psFallback = "")
		{
			return piClass < 0 ? psFallback : fWord(pOStrings, UWStringIds.ClassNameBase + piClass, psFallback);
		}

		/// <summary>The player's class name - the fallback without a player.</summary>
		public static string ClassName(UWStrings pOStrings, UWPlayerData pOPlayer, string psFallback = "")
		{
			return pOPlayer == null ? psFallback : ClassName(pOStrings, pOPlayer.CharacterClass, psFallback);
		}

		/// <summary>An entry of the skill list by its number: 0 Attack, 1 Defense, then the skills
		/// (UWPlayerData.Skill + 2) - the numbering of the stats pages, the shrine and character
		/// creation alike. Trimmed; the fallback for no string.</summary>
		public static string SkillName(UWStrings pOStrings, int piEntry, string psFallback = "")
		{
			return piEntry < 0 ? psFallback : fWord(pOStrings, UWStringIds.SkillNameBase + piEntry, psFallback);
		}

		/// <summary>The level as an ordinal number, as on the original's stats page: 1ST, 2ND,
		/// 3RD, 4TH ... - 11TH to 13TH (and 111TH ...) take TH.</summary>
		public static string Ordinal(int piNumber)
		{
			int liLastTwo = piNumber % 100;
			int liLast = piNumber % 10;

			string lsSuffix = "TH";

			if (liLastTwo < 11 || liLastTwo > 13)
			{
				if (liLast == 1)
					lsSuffix = "ST";
				else if (liLast == 2)
					lsSuffix = "ND";
				else if (liLast == 3)
					lsSuffix = "RD";
			}

			return piNumber + lsSuffix;
		}

		/// <summary>A word in title case, "Fighter", "Picklock" - the spelling of the help window
		/// and the modern panel; null gives an empty string.</summary>
		public static string TitleCase(string psText)
		{
			return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
				(psText ?? string.Empty).Trim().ToLowerInvariant());
		}

		/// <summary>A word of block 2, trimmed at both ends as every page trimmed it.</summary>
		private static string fWord(UWStrings pOStrings, int piIndex, string psFallback)
		{
			string lsWord = UWText.Block(pOStrings, CharacterWordsBlock, piIndex, null);

			return lsWord == null ? psFallback : lsWord.Trim();
		}
	}
}
