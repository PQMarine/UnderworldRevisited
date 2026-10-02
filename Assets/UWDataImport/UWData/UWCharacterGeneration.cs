using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The original's character creation - the sequence and the dice rolls, without display
	/// (that is UWCharacterCreationScreen).
	///
	/// THE SEQUENCE is in CHRGEN.DAT, 238 bytes that uw-formats.txt considers unknown.
	/// Decoded after the reference (chargen.cs, 2026-09-11): eight records of 18 bytes,
	/// one per question - byte 0 the number of the question text in string block 2, byte 8 the
	/// number of answers. From 0x90 the answer texts follow as words, one zero-terminated
	/// list per question:
	///
	///   0  Sex              "Choose character sex:"   Male / Female
	///   1  Handedness       "Select handedness:"      Left / Right
	///   2  Class            "Pick a class:"           eight classes
	///   3  Skill            "Pick a skill:"           comes from SKILLS.DAT, placeholder here
	///   4  Portrait         (no text)                 five images from CHRBTNS.GR
	///   5  Difficulty       "Choose difficulty:"      Standard / Easy
	///   6  Name             "Name: "                  text input
	///   7  Confirmation     "Keep this character?"    Yes / No
	///
	/// THE VALUES come from SKILLS.DAT: per class strength, dexterity, intelligence and a sum
	/// of bonus points that is randomly distributed over the three in chunks of one to four
	/// (ovr098_582) - or, with ManualAttributes, by the player himself (Stage.Attributes, a
	/// deliberate deviation, off by default).
	/// After that, five groups per class: a group with one skill is innate and is rolled
	/// right away, a group with several is a question to the player. Every choice rolls the
	/// skill up (fRollSkill): the first time three plus one ninth of the governing attribute,
	/// after that one plus one thirteenth, each plus a random amount and three skill checks
	/// against 20. At most 30. The same skill may be chosen several times
	/// (fighter: Attack or Defense once more).
	///
	/// Vitality and mana as on level-up (UWExperience), level 1, experience 0, one
	/// skill point, fed and rested, clock at 0x10B3000 (reference: InitEmptyPlayer).
	/// </summary>
	public class UWCharacterGeneration
	{
		public enum Stage
		{
			Sex,
			Handedness,
			Class,
			/// <summary>Only with ManualAttributes: the player deals the class's bonus points.</summary>
			Attributes,
			Skill,
			Portrait,
			Difficulty,
			Name,
			Confirm,
			Done
		}

		/// <summary>Skills in the original's numbering: 0 Attack, 1 Defense, then the
		/// eighteen named ones from UWPlayerData.Skill.</summary>
		public const int SkillNumberCount = 20;

		public const int PortraitCount = 5;

		public const int ClassCount = 8;

		/// <summary>Highest value of a skill at creation.</summary>
		public const int MaxSkillValue = UWPlayerData.MaxSkillValue;

		/// <summary>Target value of the checks when rolling a skill up.</summary>
		private const int SkillRollTarget = 0x14;

		/// <summary>A bonus portion is (RNG &amp; 3) + 1 points (ovr098_582).</summary>
		private const int BonusPortionRange = 4;

		/// <summary>No attribute is dealt above this at creation (ovr098_582).</summary>
		private const int MaxAttributeValue = 30;

		/// <summary>How long a name the original can hold - the field is 14 bytes long.</summary>
		public const int MaxNameLength = 14;

		// String block 2
		private const int StringBlock = 2;

		private const int SexStringBase = 10;

		private const int ClassStringBase = 24;

		private const int SkillStringBase = UWPlayerData.SkillNameStringIndex;

		private const int AttributeStringBase = 18;

		/// <summary>The numbers in CHRGEN.DAT count like the original; our string block is
		/// one higher (see DataImport.GetGeneralMessage). The fixed numbers above are
		/// already converted.</summary>
		private const int ChrgenStringOffset = 1;

		private const int QuestionRecordSize = 18;

		private const int QuestionCount = 8;

		private const int QuestionBodyOffset = 0x90;

		private class Question
		{
			public int StringId;

			public List<int> ChoiceStrings = new List<int>();
		}

		private readonly Question[] mOQuestions = new Question[QuestionCount];

		private readonly UWStrings mOStrings;

		private readonly UWMiscDataFiles.CharacterSkills mOSkillData;

		public bool IsLoaded { get; private set; }

		public Stage CurrentStage { get; private set; } = Stage.Done;

		/// <summary>The question text of the current stage, empty if there is none.</summary>
		public string QuestionText { get; private set; } = string.Empty;

		/// <summary>The answers of the current stage - five empty ones for portraits.</summary>
		public IReadOnlyList<string> Choices => mOChoices;

		private readonly List<string> mOChoices = new List<string>();

		/// <summary>The skill numbers behind the answers of the skill question.</summary>
		private readonly List<int> mOSkillChoiceNumbers = new List<int>();

		private IReadOnlyList<byte[]> mOSkillGroups;

		private int miSkillGroup;

		public bool IsFemale { get; private set; }

		public bool IsLeftHanded { get; private set; }

		/// <summary>0 to 7, or -1 while none is chosen.</summary>
		public int CharacterClass { get; private set; } = -1;

		public int Body { get; private set; }

		/// <summary>0 Standard, 1 Easy - the order of the answers.</summary>
		public int Difficulty { get; private set; }

		public string Name { get; private set; } = string.Empty;

		public int Strength { get; private set; }

		public int Dexterity { get; private set; }

		public int Intelligence { get; private set; }

		private readonly int[] miSkills = new int[SkillNumberCount];

		/// <summary>
		/// THE PLAYER DEALS THE BONUS POINTS HIMSELF (per user, 2026-10-01: for those who do not
		/// like their values to depend on luck; a deliberate deviation, off by default, set from
		/// UWUserSettings). After the class the stage Attributes offers plus and minus for each
		/// attribute, between the class's base value and 30, until no point is left; then the
		/// innate skills are rolled as usual - they depend on the attributes. Only the
		/// attributes: the skills keep their dice (per user, variant 1).
		/// </summary>
		public bool ManualAttributes { get; set; }

		/// <summary>Bonus points not dealt yet (Stage.Attributes).</summary>
		public int BonusPointsLeft { get; private set; }

		private readonly int[] miBaseAttributes = new int[3];

		public int MaxVitality { get; private set; }

		public int MaxMana { get; private set; }

		public UWCharacterGeneration(UWStrings pOStrings, UWMiscDataFiles pOMisc)
		{
			mOStrings = pOStrings;
			mOSkillData = pOMisc != null ? pOMisc.SkillChoices : null;

			byte[] lyData = pOMisc != null ? pOMisc.RawCharacterGeneration : null;

			if (lyData == null || lyData.Length < QuestionBodyOffset || mOSkillData == null || !mOSkillData.IsLoaded)
				return;

			int liBody = QuestionBodyOffset;

			for (int liAt = 0; liAt < QuestionCount; liAt++)
			{
				Question lOQuestion = new Question
				{
					StringId = lyData[liAt * QuestionRecordSize]
				};

				// The answer lists follow one another, each terminated by a zero word.
				// The count in byte 8 of the record applies only to the fixed questions; for the
				// skill question the original fills it in only at runtime.
				while (liBody + 1 < lyData.Length)
				{
					int liWord = lyData[liBody] | (lyData[liBody + 1] << 8);
					liBody += 2;

					if (liWord == 0)
						break;

					lOQuestion.ChoiceStrings.Add(liWord);
				}

				mOQuestions[liAt] = lOQuestion;
			}

			IsLoaded = true;
		}

		public int GetSkill(int piSkillNumber)
		{
			return piSkillNumber >= 0 && piSkillNumber < SkillNumberCount ? miSkills[piSkillNumber] : 0;
		}

		public string GetSkillName(int piSkillNumber)
		{
			return fGetString(SkillStringBase + piSkillNumber);
		}

		public string GetAttributeLabel(int piIndex)
		{
			return fGetString(AttributeStringBase + piIndex);
		}

		public string ClassName => CharacterClass >= 0 ? fGetString(ClassStringBase + CharacterClass) : string.Empty;

		public string SexName => fGetString(SexStringBase + (IsFemale ? 1 : 0));

		/// <summary>Start over - also after a "No" at the end.</summary>
		public void Begin()
		{
			IsFemale = false;
			IsLeftHanded = false;
			CharacterClass = -1;
			Body = 0;
			Difficulty = 0;
			Name = string.Empty;
			Strength = 0;
			Dexterity = 0;
			Intelligence = 0;
			MaxVitality = 0;
			MaxMana = 0;
			BonusPointsLeft = 0;
			Array.Clear(miSkills, 0, miSkills.Length);
			mOSkillGroups = null;
			miSkillGroup = 0;

			fEnter(IsLoaded ? Stage.Sex : Stage.Done);
		}

		/// <summary>An answer to the current question, as an index into Choices.</summary>
		public void Choose(int piChoice)
		{
			if (piChoice < 0 || piChoice >= mOChoices.Count)
				return;

			switch (CurrentStage)
			{
				case Stage.Sex:
					IsFemale = piChoice == 1;
					fEnter(Stage.Handedness);
					break;

				case Stage.Handedness:
					IsLeftHanded = piChoice == 0;
					fEnter(Stage.Class);
					break;

				case Stage.Class:
					fInitClass(piChoice);
					fEnter(ManualAttributes ? Stage.Attributes : Stage.Skill);
					break;

				case Stage.Attributes:
					fChooseAttribute(piChoice);
					break;

				case Stage.Skill:
					if (piChoice < mOSkillChoiceNumbers.Count)
						fRollSkill(mOSkillChoiceNumbers[piChoice]);

					miSkillGroup++;
					fEnter(Stage.Skill);
					break;

				case Stage.Portrait:
					Body = piChoice;
					fEnter(Stage.Difficulty);
					break;

				case Stage.Difficulty:
					Difficulty = piChoice;
					fEnter(Stage.Name);
					break;

				case Stage.Confirm:
					if (piChoice == 0)
						fEnter(Stage.Done);
					else
						Begin();
					break;
			}
		}

		/// <summary>The typed name - completes the name question.</summary>
		public void SetName(string psName)
		{
			if (CurrentStage != Stage.Name)
				return;

			string lsName = (psName ?? string.Empty).Trim();

			if (lsName.Length == 0)
				return;

			if (lsName.Length > MaxNameLength)
				lsName = lsName.Substring(0, MaxNameLength);

			Name = lsName;
			fEnter(Stage.Confirm);
		}

		private void fEnter(Stage peStage)
		{
			CurrentStage = peStage;
			mOChoices.Clear();
			mOSkillChoiceNumbers.Clear();
			QuestionText = string.Empty;

			switch (peStage)
			{
				case Stage.Sex:
					fPresentQuestion(0);
					break;

				case Stage.Handedness:
					fPresentQuestion(1);
					break;

				case Stage.Class:
					fPresentQuestion(2);
					break;

				case Stage.Attributes:
					fPresentAttributeChoice();
					break;

				case Stage.Skill:
					if (!fPresentSkillChoice())
						fEnter(Stage.Portrait);
					break;

				case Stage.Portrait:
					for (int liAt = 0; liAt < PortraitCount; liAt++)
						mOChoices.Add(string.Empty);
					break;

				case Stage.Difficulty:
					fPresentQuestion(5);
					break;

				case Stage.Name:
					QuestionText = fGetString(mOQuestions[6].StringId + ChrgenStringOffset);
					break;

				case Stage.Confirm:
					fPresentQuestion(7);
					break;
			}
		}

		private void fPresentQuestion(int piQuestion)
		{
			Question lOQuestion = mOQuestions[piQuestion];

			QuestionText = fGetString(lOQuestion.StringId + ChrgenStringOffset);

			foreach (int liString in lOQuestion.ChoiceStrings)
				mOChoices.Add(fGetString(liString + ChrgenStringOffset));
		}

		/// <summary>The next group with a choice; false if none is left.</summary>
		private bool fPresentSkillChoice()
		{
			if (mOSkillGroups == null)
				return false;

			while (miSkillGroup < mOSkillGroups.Count && mOSkillGroups[miSkillGroup].Length < 2)
				miSkillGroup++;

			if (miSkillGroup >= mOSkillGroups.Count)
				return false;

			QuestionText = fGetString(mOQuestions[3].StringId + ChrgenStringOffset);

			foreach (byte lySkill in mOSkillGroups[miSkillGroup])
			{
				mOSkillChoiceNumbers.Add(lySkill);
				mOChoices.Add(GetSkillName(lySkill));
			}

			return true;
		}

		/// <summary>Base values of the class, distribute bonus points (ovr098_582), roll innate
		/// skills (reference: RollClassBaseSkills).</summary>
		private void fInitClass(int piClass)
		{
			CharacterClass = piClass;
			Array.Clear(miSkills, 0, miSkills.Length);

			int liStrength, liDexterity, liIntelligence, liBonus;

			if (!mOSkillData.TryGetBaseAttributes(piClass, out liStrength, out liDexterity, out liIntelligence, out liBonus))
			{
				liStrength = 12;
				liDexterity = 12;
				liIntelligence = 12;
				liBonus = 0;
			}

			Strength = liStrength;
			Dexterity = liDexterity;
			Intelligence = liIntelligence;

			miBaseAttributes[0] = liStrength;
			miBaseAttributes[1] = liDexterity;
			miBaseAttributes[2] = liIntelligence;

			mOSkillGroups = mOSkillData.GetGroups(piClass);
			miSkillGroup = 0;

			if (ManualAttributes)
			{
				BonusPointsLeft = liBonus;
				fRecalculate();

				return;
			}

			// THE BONUS POINTS AS UW.EXE DEALS THEM (ovr098_582, read 2026-10-01 on the user's
			// question whether a fighter could have had intelligence 15): in portions of
			// (RNG & 3) + 1, so one to FOUR points, each to an attribute RNG % 3, a portion cut to
			// what is left and to what the attribute still has up to 30. The reference (and ours
			// until then) dealt one to three and knew no cap.
			int[] liAttributes = { Strength, Dexterity, Intelligence };

			while (liBonus > 0)
			{
				int liAdd = 1 + UWRandom.Next(BonusPortionRange);

				if (liAdd > liBonus)
					liAdd = liBonus;

				int liAttribute = UWRandom.Next(3);

				if (liAttributes[liAttribute] + liAdd > MaxAttributeValue)
					liAdd = MaxAttributeValue - liAttributes[liAttribute];

				liAttributes[liAttribute] += liAdd;
				liBonus -= liAdd;
			}

			Strength = liAttributes[0];
			Dexterity = liAttributes[1];
			Intelligence = liAttributes[2];

			fRollInnateSkills();
		}

		/// <summary>A group with one skill is innate and rolled right away (RollClassBaseSkills).
		/// </summary>
		private void fRollInnateSkills()
		{
			if (mOSkillGroups != null)
			{
				foreach (byte[] lyGroup in mOSkillGroups)
				{
					if (lyGroup.Length == 1)
						fRollSkill(lyGroup[0]);
				}
			}

			fRecalculate();
		}

		/// <summary>The answers of Stage.Attributes, in this order: plus and minus for strength,
		/// dexterity and intelligence, then done.</summary>
		private const int AttributeDoneChoice = 6;

		private const string PointsLeftText = "Points left: ";

		private const string DoneText = "Done";

		private void fPresentAttributeChoice()
		{
			QuestionText = PointsLeftText + BonusPointsLeft;

			for (int liAttribute = 0; liAttribute < 3; liAttribute++)
			{
				string lsLabel = GetAttributeLabel(liAttribute).Trim().TrimEnd(':');

				mOChoices.Add(lsLabel + " +");
				mOChoices.Add(lsLabel + " -");
			}

			mOChoices.Add(DoneText);
		}

		/// <summary>Plus or minus one point on an attribute - between its class base value and
		/// 30, and only while points are left to give - or done once none is left.</summary>
		private void fChooseAttribute(int piChoice)
		{
			if (piChoice == AttributeDoneChoice)
			{
				if (BonusPointsLeft > 0)
					return;

				fRollInnateSkills();
				fEnter(Stage.Skill);

				return;
			}

			int liAttribute = piChoice / 2;
			bool lbPlus = piChoice % 2 == 0;
			int[] liValues = { Strength, Dexterity, Intelligence };

			if (lbPlus && BonusPointsLeft > 0 && liValues[liAttribute] < MaxAttributeValue)
			{
				liValues[liAttribute]++;
				BonusPointsLeft--;
			}
			else if (!lbPlus && liValues[liAttribute] > miBaseAttributes[liAttribute])
			{
				liValues[liAttribute]--;
				BonusPointsLeft++;
			}

			Strength = liValues[0];
			Dexterity = liValues[1];
			Intelligence = liValues[2];

			fRecalculate();
			fEnter(Stage.Attributes);
		}

		/// <summary>Rolls a skill up (reference: chargen.RollSkill).</summary>
		private void fRollSkill(int piSkill)
		{
			if (piSkill < 0 || piSkill >= SkillNumberCount)
				return;

			int liValue = miSkills[piSkill];
			int liBase;
			int liDivisor;
			const int liRolls = 3;

			if (liValue == 0)
			{
				liBase = 3;
				liDivisor = 9;
			}
			else
			{
				liBase = 1;
				liDivisor = 0xD;
			}

			int liGoverning = fGetGoverningValue(piSkill);

			liValue += liBase;
			liValue += liGoverning / liDivisor;
			liValue += UWRandom.Next(liRolls);

			for (int liAt = 0; liAt < liRolls; liAt++)
				liValue += (int)UWSkillCheck.Check(liGoverning, SkillRollTarget);

			if (liValue > MaxSkillValue)
				liValue = MaxSkillValue;

			if (liValue < 0)
				liValue = 0;

			miSkills[piSkill] = liValue;

			fRecalculate();
		}

		/// <summary>The governing attribute: combat depends on strength, magic and lore on
		/// intelligence, the rest on dexterity (reference: playerdat.GetGoverningAttribute).</summary>
		private int fGetGoverningValue(int piSkill)
		{
			if (piSkill < 7)
				return Strength;

			if (piSkill < 10)
				return Intelligence;

			return Dexterity;
		}

		private void fRecalculate()
		{
			MaxVitality = UWExperience.GetMaximumHealth(Strength, 1);
			MaxMana = UWExperience.GetMaximumMana(miSkills[(int)UWPlayerData.Skill.Mana + 2], Intelligence);
		}

		private string fGetString(int piIndex)
		{
			if (mOStrings == null || !mOStrings.Blocks.ContainsKey(StringBlock))
				return string.Empty;

			var lOStrings = mOStrings.Blocks[StringBlock].Strings;

			return piIndex >= 0 && piIndex < lOStrings.Count ? lOStrings[piIndex] : string.Empty;
		}

		/// <summary>The finished character as an unencrypted PLAYER.DAT.</summary>
		public byte[] BuildPlayerData()
		{
			int[] liNamed = new int[UWPlayerData.SkillCount];

			for (int liAt = 0; liAt < liNamed.Length; liAt++)
				liNamed[liAt] = miSkills[liAt + 2];

			return UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter
			{
				Name = Name,
				CharacterClass = CharacterClass < 0 ? 0 : CharacterClass,
				Body = Body,
				IsFemale = IsFemale,
				IsLeftHanded = IsLeftHanded,
				Difficulty = Difficulty,
				Strength = Strength,
				Dexterity = Dexterity,
				Intelligence = Intelligence,
				Attack = miSkills[0],
				Defense = miSkills[1],
				Skills = liNamed,
				MaxVitality = MaxVitality,
				MaxMana = MaxMana
			});
		}
	}
}
