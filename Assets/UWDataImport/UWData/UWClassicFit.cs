using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIT CHECK OF THE CLASSIC FIXED BOXES (translation 5.2, 2026-10-26): a game string drawn in an
	/// original pixel font into a box of fixed width is clipped where it is wider (the raster draws no
	/// wider than its box, UWTextRaster.Render) - the German "Streitkolbe" lost its end on a character
	/// creation button. This measures, engine-free, every such string in the places the classic look
	/// draws them, in the font of that place, against the box, so a translation shows its overflows
	/// before it is played (uwdump fit, uwdump validate-tsv's warnings, the self-check).
	///
	/// THE PLACES COVERED: character creation (UWCharacterCreationScreen, the creation font FONTCHAR.SYS):
	/// the questions, the choice buttons (every answer of CHRGEN.DAT, every skill a class chooses from,
	/// the manual attributes' "Str +" and the port's "done"), the sex and class line, the attribute
	/// labels, the skill list and the name prompt; the classic panel's stats page (UWHudPanel, the italic
	/// font FONT5X6I.SYS, upper case): the class name beside the level and the skill names beside their
	/// value. The boxes are the constants below, which the screens use themselves.
	/// </summary>
	public static class UWClassicFit
	{
		/// <summary>Character creation's boxes, in the creation screen's pixels (UWCharacterCreationScreen).</summary>
		public static class Creation
		{
			/// <summary>A choice button (CHRBTNS.GR 0), the answer centred on it.</summary>
			public const int ButtonWidth = 67;

			/// <summary>The question above the buttons, centred, as wide as the right page.</summary>
			public const int QuestionWidth = 140;

			/// <summary>The name on the left page and the typed name.</summary>
			public const int LabelWidth = 120;

			public const int ValueWidth = 30;

			/// <summary>The sex and the class on the left page, each its own line.</summary>
			public const int SexClassWidth = ValueWidth * 2;

			/// <summary>The attribute labels (Str: Dex: Int: Vit:) left of their values.</summary>
			public const int StatLabelWidth = ValueWidth + 10;

			/// <summary>The skill list on the left page.</summary>
			public const int SkillLabelWidth = LabelWidth - 20;

			/// <summary>The name question's prompt ("Name: ") in the name box.</summary>
			public const int NamePromptWidth = ValueWidth + 10;
		}

		/// <summary>One string in one place.</summary>
		public sealed class Entry
		{
			/// <summary>Where it is drawn ("creation button" ...).</summary>
			public string Place;

			/// <summary>"block:index" of a game string, or the id of a port text.</summary>
			public string Key;

			/// <summary>The text as drawn (upper case where the place writes it so).</summary>
			public string Text;

			public int Width;

			public int Box;

			public bool IsOver => Width > Box;
		}

		/// <summary>The questions of CHRGEN.DAT that ask with buttons (sex, handedness, class, skill,
		/// difficulty, keep) and the name question.</summary>
		private static readonly int[] msButtonQuestions = { 0, 1, 2, 3, 5, 7 };

		private const int NameQuestion = 6;

		/// <summary>The skill question, whose answers in CHRGEN.DAT are a placeholder (the skills come from SKILLS.DAT).</summary>
		private const int SkillQuestion = 3;

		/// <summary>Every string of the places covered, measured. Strings and fonts are the game's (a
		/// game-text pack laid over them shows the language); a place without its font is left out.</summary>
		public static List<Entry> Measure(UWStrings pOStrings, UWMiscDataFiles pOMisc, UWFonts pOFonts)
		{
			List<Entry> lOEntries = new List<Entry>();
			HashSet<string> lOSeen = new HashSet<string>();
			UWFont lOCreation = pOFonts != null ? pOFonts.Get(UWFonts.FontType.CharacterGeneration) : null;
			UWFont lOItalic = pOFonts != null ? pOFonts.Get(UWFonts.FontType.Italic) : null;

			if (lOCreation != null && lOCreation.IsLoaded)
				fCreation(lOEntries, lOSeen, pOStrings, pOMisc, lOCreation);

			if (lOItalic != null && lOItalic.IsLoaded)
				fStatsPage(lOEntries, lOSeen, pOStrings, lOItalic);

			return lOEntries;
		}

		private static void fAdd(List<Entry> pOEntries, HashSet<string> pOSeen, string psPlace, string psKey, string psText, UWFont pOFont, int piBox)
		{
			if (string.IsNullOrEmpty(psText) || !pOSeen.Add(psPlace + "|" + psKey))
				return;

			// Blanks at the end ("Name: ") draw nothing: a box may cut them.
			pOEntries.Add(new Entry
			{
				Place = psPlace, Key = psKey, Text = psText, Width = pOFont.MeasureText(psText.TrimEnd(), 0, UWTextLayout.CharacterSpacing), Box = piBox
			});
		}

		private static string fKey(int piIndex)
		{
			return UWCharacterText.CharacterWordsBlock + ":" + piIndex;
		}

		private static string fWord(UWStrings pOStrings, int piIndex)
		{
			return UWText.Block(pOStrings, UWCharacterText.CharacterWordsBlock, piIndex, null);
		}

		private static void fCreation(List<Entry> pOEntries, HashSet<string> pOSeen, UWStrings pOStrings, UWMiscDataFiles pOMisc, UWFont pOFont)
		{
			UWCharacterGeneration lOGeneration = new UWCharacterGeneration(pOStrings, pOMisc);

			if (lOGeneration.IsLoaded)
			{
				foreach (int liQuestion in msButtonQuestions)
				{
					int liText = lOGeneration.QuestionStringIndex(liQuestion);

					fAdd(pOEntries, pOSeen, "creation question", fKey(liText), fWord(pOStrings, liText), pOFont, Creation.QuestionWidth);

					if (liQuestion == SkillQuestion)
						continue;

					foreach (int liChoice in lOGeneration.ChoiceStringIndices(liQuestion))
						fAdd(pOEntries, pOSeen, "creation button", fKey(liChoice), fWord(pOStrings, liChoice), pOFont, Creation.ButtonWidth);
				}

				int liPrompt = lOGeneration.QuestionStringIndex(NameQuestion);

				fAdd(pOEntries, pOSeen, "creation name prompt", fKey(liPrompt), fWord(pOStrings, liPrompt), pOFont, Creation.NamePromptWidth);
			}

			// The skills a class chooses from are buttons; every skill is a line of the list.
			if (pOMisc != null && pOMisc.SkillChoices != null && pOMisc.SkillChoices.IsLoaded)
			{
				for (int liClass = 0; liClass < UWCharacterGeneration.ClassCount; liClass++)
				{
					foreach (byte[] lyGroup in pOMisc.SkillChoices.GetGroups(liClass))
					{
						if (lyGroup.Length < 2)
							continue;

						foreach (byte lySkill in lyGroup)
							fAdd(pOEntries, pOSeen, "creation button", fKey(UWStringIds.SkillNameBase + lySkill),
								UWCharacterText.SkillName(pOStrings, lySkill), pOFont, Creation.ButtonWidth);
					}
				}
			}

			for (int liSkill = 0; liSkill < UWCharacterText.SkillEntryCount; liSkill++)
				fAdd(pOEntries, pOSeen, "creation skill list", fKey(UWStringIds.SkillNameBase + liSkill), UWCharacterText.SkillName(pOStrings, liSkill),
					pOFont, Creation.SkillLabelWidth);

			for (int liSex = 0; liSex < 2; liSex++)
				fAdd(pOEntries, pOSeen, "creation sex/class line", fKey(UWCharacterGeneration.SexStringBase + liSex),
					fWord(pOStrings, UWCharacterGeneration.SexStringBase + liSex), pOFont, Creation.SexClassWidth);

			for (int liClass = 0; liClass < UWCharacterGeneration.ClassCount; liClass++)
				fAdd(pOEntries, pOSeen, "creation sex/class line", fKey(UWStringIds.ClassNameBase + liClass),
					UWCharacterText.ClassName(pOStrings, liClass), pOFont, Creation.SexClassWidth);

			for (int liAttribute = 0; liAttribute < UWCharacterGeneration.AttributeLabelCount; liAttribute++)
			{
				int liIndex = UWCharacterGeneration.AttributeStringBase + liAttribute;
				string lsLabel = fWord(pOStrings, liIndex);

				fAdd(pOEntries, pOSeen, "creation attribute label", fKey(liIndex), lsLabel, pOFont, Creation.StatLabelWidth);

				// The manual attributes' buttons (off by default): the first three labels with + and -.
				if (liAttribute < 3 && lsLabel != null)
					fAdd(pOEntries, pOSeen, "creation button", fKey(liIndex) + " +", lsLabel.Trim().TrimEnd(':') + " +", pOFont, Creation.ButtonWidth);
			}

			fAdd(pOEntries, pOSeen, "creation button", UWCharacterGeneration.DoneTextId, UWText.T(UWCharacterGeneration.DoneTextId), pOFont, Creation.ButtonWidth);
			fAdd(pOEntries, pOSeen, "creation question", UWCharacterGeneration.PointsLeftTextId, UWText.T(UWCharacterGeneration.PointsLeftTextId) + "99",
				pOFont, Creation.QuestionWidth);
		}

		/// <summary>The highest level whose ordinal the stats page can show beside the class.</summary>
		private const int HighestLevel = 16;

		/// <summary>The widest value of the skill list.</summary>
		private const string WidestSkillValue = "30";

		/// <summary>
		/// The classic panel's stats page: the class from UWClassicLayout.Stats.LabelLeft up to the level,
		/// right-aligned at LevelRight - the box is what the widest ordinal (1ST to 16TH) leaves less a space;
		/// a skill name up to its value, right-aligned at ValueRight - what "30" leaves less a space. Both
		/// upper case, as the page writes them (UWCharacterSheet, the original's spelling).
		/// </summary>
		private static void fStatsPage(List<Entry> pOEntries, HashSet<string> pOSeen, UWStrings pOStrings, UWFont pOFont)
		{
			int liLevel = 0;

			for (int liAt = 1; liAt <= HighestLevel; liAt++)
				liLevel = Math.Max(liLevel, UWTextRaster.TightWidth(pOFont, UWCharacterText.Ordinal(liAt)));

			int liClassBox = UWClassicLayout.Stats.LevelRight - liLevel - pOFont.SpaceWidth - UWClassicLayout.Stats.LabelLeft;
			int liSkillBox = UWClassicLayout.Stats.ValueRight - UWTextRaster.TightWidth(pOFont, WidestSkillValue) - pOFont.SpaceWidth
				- UWClassicLayout.Stats.LabelLeft;

			for (int liClass = 0; liClass < UWCharacterGeneration.ClassCount; liClass++)
				fAdd(pOEntries, pOSeen, "stats page class", fKey(UWStringIds.ClassNameBase + liClass),
					UWCharacterText.ClassName(pOStrings, liClass).ToUpper(), pOFont, liClassBox);

			for (int liSkill = 0; liSkill < UWCharacterText.SkillEntryCount; liSkill++)
				fAdd(pOEntries, pOSeen, "stats page skill", fKey(UWStringIds.SkillNameBase + liSkill),
					UWCharacterText.SkillName(pOStrings, liSkill).ToUpper(), pOFont, liSkillBox);
		}
	}
}
