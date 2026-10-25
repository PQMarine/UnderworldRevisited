namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE CHARACTER PAGES SHOW, IN ONE PLACE (cleanup 5.9, 2026-10-20): the name, the class,
	/// the level, the six values (strength, dexterity, intelligence, vitality, mana, experience),
	/// the twenty entries of the skill list (attack, defence, the eighteen skills) and the paging of
	/// the original's six-row list. Until then each page wrote them itself: the classic panel's
	/// stats page (UWHudPanel), the modern scheme's original stats page (UWModernStatsPage), the
	/// modern panel's Character tab (UWModernPanel) and the help window's Stats tab (UWHelpWindow).
	/// The words come from UWCharacterText, the numbers from the live UWPlayerVitals.
	///
	/// THE SPELLINGS STAY AS THEY WERE, as the sheet's option (SpellingEnum): the original's pages
	/// in upper case with the level as an ordinal ("1ST") and "a/b"; the modern panel and the help
	/// window readable, in title case, the level as a number and "a / b". Where and how a page
	/// lays the lines out stays with the page.
	///
	/// One sheet per page: it keeps that page's spelling and list offset; Bind hands in the
	/// sources before reading (the values are read live, nothing is cached).
	/// </summary>
	public sealed class UWCharacterSheet
	{
		/// <summary>How a page spells the sheet.</summary>
		public enum SpellingEnum
		{
			/// <summary>The original's stats page (classic panel, modern original stats page): the
			/// name upper case and cut to fifteen, "AVATAR" without one; the class upper case; the
			/// level "1ST"; "a/b"; a skill without its word shows no line.</summary>
			Original,

			/// <summary>The modern panel and the help window: the whole name, "Avatar" without one;
			/// title case; the level as a number; "a / b"; a skill without its word as "?".</summary>
			Readable
		}

		/// <summary>The entries of the skill list: Attack and Defense, then the eighteen skills.</summary>
		public const int SkillEntries = UWCharacterText.SkillEntryCount;

		/// <summary>The six values, in the order of the original's page (its labels STR to EXP are
		/// in the picture).</summary>
		public const int ValueCount = 6;

		/// <summary>The values' names where a page writes them itself (the modern panel, the help
		/// window) - English, as before; the modern panel names its pieces by them. What a page
		/// shows is ValueLabel (cleanup step 8.5, 2026-10-24).</summary>
		public static readonly string[] ValueLabels = { "Strength", "Dexterity", "Intelligence", "Vitality", "Mana", "Experience" };

		/// <summary>The values' names' ids in the text table, in the order of ValueLabels.</summary>
		public static readonly string[] ValueLabelIds =
		{
			"hud.sheet.strength", "hud.sheet.dexterity", "hud.sheet.intelligence", "hud.sheet.vitality", "hud.sheet.mana", "hud.sheet.experience"
		};

		/// <summary>A value's name in the language in force.</summary>
		public static string ValueLabel(int piRow)
		{
			return UWText.T(ValueLabelIds[piRow]);
		}

		/// <summary>The name in the original's stats header: at most fifteen characters,
		/// upper-cased (UnderworldExhumed STATS.C sp_hdr copies 0xF of the name). It matters since
		/// 2026-10-12, when names grew from 14 to 29 characters.</summary>
		public const int OriginalNameLength = 0xF;

		public SpellingEnum Spelling { get; }

		/// <summary>The rows the page shows of the skill list at once; the whole list for 0.</summary>
		public int SkillRows { get; }

		/// <summary>The first entry shown (the original's list pages one entry at a time).</summary>
		public int SkillOffset { get; private set; }

		private UWStrings mOStrings;

		private UWPlayerData mOPlayer;

		private UWPlayerVitals mOVitals;

		public UWCharacterSheet(SpellingEnum peSpelling, int piSkillRows = 0)
		{
			Spelling = peSpelling;
			SkillRows = piSkillRows > 0 && piSkillRows < SkillEntries ? piSkillRows : SkillEntries;
		}

		/// <summary>The sources the sheet reads: the game's strings (the words), the player data
		/// (the name and the class) and the live vitals (every number). Any may be null.</summary>
		public UWCharacterSheet Bind(UWStrings pOStrings, UWPlayerData pOPlayer, UWPlayerVitals pOVitals)
		{
			mOStrings = pOStrings;
			mOPlayer = pOPlayer;
			mOVitals = pOVitals;

			return this;
		}

		private bool fIsOriginal
		{
			get { return Spelling == SpellingEnum.Original; }
		}

		// ------------------------------------------------- Header

		/// <summary>The character's name in the page's spelling.</summary>
		public string Name
		{
			get
			{
				if (mOPlayer == null || string.IsNullOrEmpty(mOPlayer.Name))
					return fIsOriginal ? "AVATAR" : "Avatar";

				if (!fIsOriginal)
					return mOPlayer.Name;

				string lsName = mOPlayer.Name.Length > OriginalNameLength ? mOPlayer.Name.Substring(0, OriginalNameLength) : mOPlayer.Name;

				return lsName.ToUpper();
			}
		}

		/// <summary>The class name from string block 2 (UWCharacterText.ClassName): upper case on
		/// the original's page, title case otherwise; empty without one.</summary>
		public string ClassName
		{
			get
			{
				string lsClass = UWCharacterText.ClassName(mOStrings, mOPlayer);

				return fIsOriginal ? lsClass.ToUpper() : UWCharacterText.TitleCase(lsClass);
			}
		}

		/// <summary>The level: 1ST, 2ND ... on the original's page (UWCharacterText.Ordinal), the
		/// plain number otherwise; empty without vitals.</summary>
		public string LevelText
		{
			get
			{
				if (mOVitals == null)
					return string.Empty;

				return fIsOriginal ? UWCharacterText.Ordinal(mOVitals.Level) : mOVitals.Level.ToString();
			}
		}

		/// <summary>The modern panel's class line: "Fighter, level 3".</summary>
		public string ClassWithLevel
		{
			get { return UWText.F("hud.sheet.class_level", ClassName, LevelText); }
		}

		/// <summary>The help window's line under the name: "Level 3 Fighter".</summary>
		public string LevelWithClass
		{
			get { return UWText.F("hud.sheet.level_class", LevelText, ClassName); }
		}

		// ------------------------------------------------- Values

		/// <summary>
		/// One of the six values: strength, dexterity, intelligence; vitality and mana as current
		/// over maximum, rounded; the experience in whole points. Experience is stored in tenths in
		/// the file - divided by ten and truncated; internally the tenths go on, the thresholds in
		/// UWExperience are in that unit too. Empty without vitals or past the six.
		/// </summary>
		public string Value(int piRow)
		{
			if (mOVitals == null)
				return string.Empty;

			string lsOver = fIsOriginal ? "/" : " / ";

			switch (piRow)
			{
				case 0: return mOVitals.Strength.ToString();
				case 1: return mOVitals.Dexterity.ToString();
				case 2: return mOVitals.Intelligence.ToString();
				case 3: return UWUnits.RoundToInt(mOVitals.CurrentHP) + lsOver + UWUnits.RoundToInt(mOVitals.MaxHP);
				case 4: return UWUnits.RoundToInt(mOVitals.CurrentMana) + lsOver + UWUnits.RoundToInt(mOVitals.MaxMana);
				case 5: return (mOVitals.Experience / 10).ToString();
				default: return string.Empty;
			}
		}

		/// <summary>The unspent skill points, and the modern panel's words for them.</summary>
		public int SkillPoints
		{
			get { return mOVitals != null ? mOVitals.SkillPoints : 0; }
		}

		public string SkillPointsText
		{
			get { return SkillPoints == 1 ? UWText.T("hud.sheet.point_to_spend") : UWText.F("hud.sheet.points_to_spend", SkillPoints); }

		}

		// ------------------------------------------------- Skills

		/// <summary>
		/// An entry of the skill list by its number (0 Attack, 1 Defense, then the skills as
		/// UWPlayerData.Skill + 2), its name in the page's spelling and its live value. The
		/// original's page shows nothing for an entry without its word (both empty), the readable
		/// pages "?" and the value. False (both empty) past the list.
		///
		/// The defence is the live value (UWPlayerVitals.Defence). Until 2026-10-01 the classic
		/// page still read the loaded save game, from before we kept our own - a mantra at a shrine
		/// raised it ("You have advanced greatly in defense") and the page kept the old number (per
		/// user, ANRA on SAVE1).
		/// </summary>
		public bool TryGetSkill(int piEntry, out string psName, out string psValue)
		{
			psName = string.Empty;
			psValue = string.Empty;

			if (piEntry < 0 || piEntry >= SkillEntries)
				return false;

			if (fIsOriginal)
			{
				string lsWord = UWCharacterText.SkillName(mOStrings, piEntry, null);

				if (lsWord == null)
					return true;

				psName = lsWord;
			}
			else
				psName = SkillName(piEntry);

			psValue = SkillValue(piEntry);

			return true;
		}

		/// <summary>An entry's name alone in the page's spelling (TryGetSkill) - for a page that
		/// writes the names once and the values every frame.</summary>
		public string SkillName(int piEntry)
		{
			if (piEntry < 0 || piEntry >= SkillEntries)
				return string.Empty;

			return fIsOriginal
				? UWCharacterText.SkillName(mOStrings, piEntry, string.Empty)
				: UWCharacterText.TitleCase(UWCharacterText.SkillName(mOStrings, piEntry, "?"));
		}

		/// <summary>An entry's live value alone (UWPlayerVitals.GetSkillByNumber: 0 attack, 1
		/// defence, then the skills); empty without vitals or past the list.</summary>
		public string SkillValue(int piEntry)
		{
			if (mOVitals == null || piEntry < 0 || piEntry >= SkillEntries)
				return string.Empty;

			return mOVitals.GetSkillByNumber(piEntry).ToString();
		}

		/// <summary>The entry a row of the shown list stands for (SkillOffset on).</summary>
		public int EntryAtRow(int piRow)
		{
			return SkillOffset + piRow;
		}

		/// <summary>The highest offset: the list's last page ends with its last entry.</summary>
		public int MaxSkillOffset
		{
			get { return SkillEntries - SkillRows; }
		}

		/// <summary>Pages the list by entries: -1 back, 1 forward, clamped to the list (the
		/// original's click pages one entry, ovr145_445, the offset clamped to 0..14). True when the
		/// offset moved.</summary>
		public bool PageSkills(int piStep)
		{
			int liOffset = SkillOffset + piStep;

			if (liOffset < 0)
				liOffset = 0;
			else if (liOffset > MaxSkillOffset)
				liOffset = MaxSkillOffset;

			if (liOffset == SkillOffset)
				return false;

			SkillOffset = liOffset;

			return true;
		}

		// ------------------------------------------------- Signature

		/// <summary>
		/// A signature of every number the sheet shows, so a page redraws as soon as one changes
		/// and not every frame. In the original the stats page updates while open - experience from
		/// exploring, hit points, mana (per user, 2026-09-14). The name and the class do not change
		/// while a game runs.
		/// </summary>
		public int Signature
		{
			get
			{
				if (mOVitals == null)
					return 0;

				unchecked
				{
					int liSignature = 17;

					liSignature = (liSignature * 31) + mOVitals.Strength;
					liSignature = (liSignature * 31) + mOVitals.Dexterity;
					liSignature = (liSignature * 31) + mOVitals.Intelligence;
					liSignature = (liSignature * 31) + UWUnits.RoundToInt(mOVitals.CurrentHP);
					liSignature = (liSignature * 31) + UWUnits.RoundToInt(mOVitals.MaxHP);
					liSignature = (liSignature * 31) + UWUnits.RoundToInt(mOVitals.CurrentMana);
					liSignature = (liSignature * 31) + UWUnits.RoundToInt(mOVitals.MaxMana);
					liSignature = (liSignature * 31) + (mOVitals.Experience / 10);
					liSignature = (liSignature * 31) + mOVitals.Level;
					liSignature = (liSignature * 31) + mOVitals.SkillPoints;

					for (int liSkill = 0; liSkill < UWPlayerVitals.SkillNumberCount; liSkill++)
						liSignature = (liSignature * 31) + mOVitals.GetSkillByNumber(liSkill);

					return liSignature;
				}
			}
		}
	}
}
