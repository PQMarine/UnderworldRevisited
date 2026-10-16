using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The shrine - here experience turns into a better skill (reference:
	/// objects/shrine.cs). Engine-free since 2026-09-18 (P3 of the engine separation), out of
	/// UWShrine, which keeps only the prompt.
	///
	/// ONE CHANTS A MANTRA. The twenty-six mantras are in string block 2 from entry
	/// 52; the first twenty each belong to one skill, in save game order
	/// (zero attack, one defence, then the eighteen named ones). The last six are
	/// special cases: three raise a RANDOM PICK from a group, two belong to
	/// quests, one is unused.
	///
	/// A SKILL POINT IS ALWAYS SPENT, but only if something actually happened -
	/// whoever is already at the cap gets "You cannot advance any further in that skill." and
	/// keeps the point.
	///
	/// "Chant the mantra: " is, like a few other sentences, not in STRINGS.PAK but in
	/// UW.EXE (found in the game image, 2026-09-10).
	/// </summary>
	public static class UWShrineRules
	{
		/// <summary>Object number of the shrine.</summary>
		public const int ShrineObjectId = 343;

		/// <summary>String block 2: the mantra list starts here.</summary>
		private const int FirstMantraString = 52;

		/// <summary>This many mantras exist.</summary>
		public const int MantraCount = 26;

		/// <summary>String block 2: the skill list starts here - entry 32 is
		/// "Attack".</summary>
		private const int FirstSkillNameString = UWPlayerData.SkillNameStringIndex;

		/// <summary>From here on they are no longer skill mantras.</summary>
		public const int FirstSpecialMantra = 20;

		/// <summary>INSAHN - it says where the Cup of Wonder is (case 0 of the switch at
		/// ChantMantraAtShrine_ovr143_48D label 581, 393681-393700).</summary>
		public const int CupOfWonderMantra = 20;

		/// <summary>FANLO - it hands over the Key of Truth (case 1, label 5F0).</summary>
		public const int KeyOfTruthMantra = 21;

		/// <summary>NO - it writes an EMPTY line and does nothing else (case 2, label 631:
		/// block 1 string 0x1F, which is our 32 and has no text).</summary>
		public const int EmptyMantra = 22;

		/// <summary>Where the Cup of Wonder lies, handed to the direction routine as fixed
		/// numbers (label 5AA pushes 4, 3, 0x2D, 0x18) - UWQuestPlaces and UWLevelIds hold them
		/// since 2026-10-11. The level is ONE-BASED here, as the host's DungeonLevel is.</summary>
		public const int CupTileX = UWQuestPlaces.CupTileX;

		public const int CupTileY = UWQuestPlaces.CupTileY;

		public const int CupLevel = UWLevelIds.CupOfWonder + 1;

		/// <summary>Within this many tiles, counted as the two distances added up, no direction
		/// is named any more - then it is only "very near"
		/// (GetDetectedCreatureDirections_ovr154_11B label 160).</summary>
		public const int CupNearDistance = 4;

		/// <summary>The Key of Truth, spawned into the hand (label 601 pushes 0xE1). String
		/// block 4 entry 226 names it, and the object id is one lower.</summary>
		public const int KeyOfTruthObjectId = UWObjectIds.KeyOfTruth;

		/// <summary>String block 1: "The Cup of Wonder is " (UW's 0x24).</summary>
		private const int CupPrefixMessage = 36;

		/// <summary>String block 1: the eight directions from "to the North" on, in the order
		/// GetCardinal_ovr154_7C numbers them.</summary>
		private const int FirstDirectionMessage = UWStringIds.FirstDirectionMessage;

		/// <summary>String block 1: the level phrases. The index is this plus the player's
		/// level minus the target's (label 1A1: 0x33 plus the difference), so "above you" one
		/// level down and "underneath you" one level up.</summary>
		private const int LevelPhraseMessage = 52;

		/// <summary>String block 1: the empty line the mantra NO writes.</summary>
		private const int EmptyLineMessage = 32;

		/// <summary>Sentences that are not in STRINGS.PAK but in UW.EXE, like the prompt.</summary>
		public const string AndText = " and ";

		public const string VeryNearText = "very near";

		public const string EndText = ".";

		// Numbers in string block 1, in OUR numbering.
		private const int NotReadyMessage = 25;

		private const int NotAMantraMessage = 26;

		private const int KnowledgeFillsYouMessage = 27;

		private const int NoFurtherAdvanceMessage = 28;

		private const int AdvancedGreatlyMessage = 29;

		private const int AdvancedMessage = 30;

		private const int NoSkillImprovedMessage = 31;

		/// <summary>What is entered at the shrine - the wording comes from UW.EXE.
		/// </summary>
		public const string Prompt = "Chant the mantra: ";

		/// <summary>
		/// What the two QUEST MANTRAS need beyond the skills: where the player stands, which of
		/// the two bits of PLAYER.DAT 0x60 are already set, and a hand to put the key in.
		/// Without a host the two mantras do nothing, as they did before 2026-09-22.
		/// </summary>
		public interface IHost
		{
			int TileX { get; }

			int TileY { get; }

			/// <summary>One-based, as PLAYER.DAT 0x5C keeps it.</summary>
			int DungeonLevel { get; }

			/// <summary>PLAYER.DAT 0x60 bit 7 - once the cup has been found the mantra says
			/// nothing (label 59D).</summary>
			bool CupOfWonderFound { get; }

			/// <summary>PLAYER.DAT 0x60 bit 6 - the key is handed over once (label 5F4, set at
			/// label 621).</summary>
			bool KeyOfTruthGiven { get; set; }

			/// <summary>Hands the player a new object of this kind: the pointer, else the
			/// backpack, else the floor at the feet (UWInventoryModel.GiveToPlayer). False only
			/// when nothing could be made, and then nothing is marked either. Until 2026-10-12
			/// this was "the pointer or nothing", as place_new has it (mantra_advance, SKILLS.C);
			/// B8 of the cleanup plan made the four gifts one behaviour.</summary>
			bool GiveObject(int piObjectId);
		}

		/// <summary>
		/// Which of the eight directions points from one tile to another
		/// (GetCardinal_ovr154_7C, 393100-393170): 0 north, then clockwise, so 2 east, 4 south,
		/// 6 west. A direction counts as straight when one distance is more than twice the
		/// other, otherwise it is the diagonal between them. The Y AXIS RUNS NORTH, which is
		/// why a positive difference in Y is north.
		/// </summary>
		public static int GetCardinalDirection(int piFromX, int piFromY, int piToX, int piToY)
		{
			int liX = piToX - piFromX;
			int liY = piToY - piFromY;
			int liAbsX = liX < 0 ? -liX : liX;
			int liAbsY = liY < 0 ? -liY : liY;

			if (liAbsX / 2 > liAbsY)
				return liX > 0 ? 2 : 6;

			if (liAbsY / 2 > liAbsX)
				return liY > 0 ? 0 : 4;

			if (liX < 0)
				return liY > 0 ? 7 : 5;

			return liY < 0 ? 3 : 1;
		}

		/// <summary>
		/// Accepts a chanted mantra. The lines for the scroll come back through the two
		/// callbacks - a line of string block 1 by number, or a finished sentence.
		/// </summary>
		public static void Chant(string psMantra, UWPlayerVitals pOVitals, DataImport pOData,
			Action<int> pOGeneralMessage, Action<string> pOMessage, IHost pIHost = null)
		{
			if (pOVitals == null || pOData == null || pOGeneralMessage == null || pOMessage == null)
				return;

			int liMantra = fFindMantra(psMantra, pOData);

			if (liMantra < 0)
			{
				pOGeneralMessage(NotAMantraMessage);

				// mantra_advance runs FixPlayerEquips at its end for this too - where a worn
				// curse strikes (UWPlayerVitals.RequestStatusPass).
				pOVitals.RequestStatusPass();

				return;
			}

			// THE THREE MANTRAS THAT ARE NOT ABOUT SKILLS ASK FOR NO SKILL POINT and spend
			// none: the switch at label 581 is reached before the check, and only the three
			// GROUP mantras check at label 683 (393838). Corrected 2026-09-22 - until then
			// one could not chant FANLO without a point.
			if (liMantra == CupOfWonderMantra)
			{
				fCupOfWonder(pOData, pOMessage, pIHost);

				return;
			}

			if (liMantra == KeyOfTruthMantra)
			{
				fKeyOfTruth(pOGeneralMessage, pIHost);

				return;
			}

			if (liMantra == EmptyMantra)
			{
				pOGeneralMessage(EmptyLineMessage);

				return;
			}

			if (pOVitals.SkillPoints <= 0)
				pOGeneralMessage(NotReadyMessage);
			else if (liMantra < FirstSpecialMantra)
				fAdvanceGreatly(liMantra, pOVitals, pOData, pOGeneralMessage, pOMessage);
			else
				fAdvanceGroup(liMantra - FirstSpecialMantra, pOVitals, pOData, pOGeneralMessage, pOMessage);

			// THE MAXIMA ARE RECOMPUTED HERE, after every skill and group mantra, "not ready"
			// included (mantra_advance calls player_compute(0) at the end of that branch): the
			// mana and, since 2026-10-13, the vitality too. A raise itself does not (get_skill),
			// so a trainer's Mana lesson shows only now or at the next level-up (2026-10-12; until
			// then every raise recomputed it at once).
			pOVitals.RecomputeMaxima();

			// And then FixPlayerEquips, the status pass where a worn curse strikes.
			pOVitals.RequestStatusPass();
		}

		/// <summary>
		/// INSAHN, the Cup of Wonder (case 0, label 599). While the cup has not been found it
		/// says where it lies: the prefix, then the direction unless one is already within
		/// CupNearDistance, then - if one stands on another level - "and" with the level
		/// phrase, or "very near" when neither a direction nor a level is left to name. The
		/// sentence ends with a full stop. Once the cup is found the mantra says nothing.
		/// </summary>
		private static void fCupOfWonder(DataImport pOData, Action<string> pOMessage, IHost pIHost)
		{
			if (pIHost == null || pIHost.CupOfWonderFound)
				return;

			System.Text.StringBuilder lOText = new System.Text.StringBuilder();

			lOText.Append(fGetGeneralMessage(pOData, CupPrefixMessage));

			int liDistance = Math.Abs(pIHost.TileX - CupTileX) + Math.Abs(pIHost.TileY - CupTileY);
			bool lbDirection = liDistance > CupNearDistance;

			if (lbDirection)
				lOText.Append(fGetGeneralMessage(pOData,
					FirstDirectionMessage + GetCardinalDirection(pIHost.TileX, pIHost.TileY, CupTileX, CupTileY)));

			if (pIHost.DungeonLevel != CupLevel && pIHost.DungeonLevel != 0)
			{
				if (lbDirection)
					lOText.Append(AndText);

				lOText.Append(fGetGeneralMessage(pOData,
					LevelPhraseMessage + pIHost.DungeonLevel - CupLevel));
			}
			else if (!lbDirection && pIHost.DungeonLevel != 0)
				lOText.Append(VeryNearText);

			lOText.Append(EndText);

			pOMessage(lOText.ToString());
		}

		/// <summary>
		/// FANLO, the Key of Truth (case 1, label 5F0). It hands the key over ONCE, straight
		/// onto the cursor, and says "None of your skills improved." while doing it - the
		/// original's own wording here, odd as it reads. In the original a held object makes
		/// place_new return nothing, so nothing is given or marked and it can be chanted again;
		/// ours hands the key over wherever it fits (IHost.GiveObject, since 2026-10-12).
		/// </summary>
		private static void fKeyOfTruth(Action<int> pOGeneralMessage, IHost pIHost)
		{
			if (pIHost == null || pIHost.KeyOfTruthGiven)
				return;

			if (!pIHost.GiveObject(KeyOfTruthObjectId))
				return;

			pOGeneralMessage(NoSkillImprovedMessage);
			pIHost.KeyOfTruthGiven = true;
		}

		private static int fFindMantra(string psMantra, DataImport pOData)
		{
			if (string.IsNullOrEmpty(psMantra) || pOData.Strings == null)
				return -1;

			string lsWanted = psMantra.Trim().ToUpperInvariant();

			for (int liAt = 0; liAt < MantraCount; liAt++)
			{
				string lsMantra = fGetString(pOData, 2, FirstMantraString + liAt);

				if (!string.IsNullOrEmpty(lsMantra)
					&& lsMantra.Trim().ToUpperInvariant() == lsWanted)
					return liAt;
			}

			return -1;
		}

		/// <summary>
		/// A skill mantra: the skill is raised TWICE, and a single one of the
		/// two attempts is enough for the point to be spent.
		/// </summary>
		private static void fAdvanceGreatly(int piSkillNumber, UWPlayerVitals pOVitals, DataImport pOData,
			Action<int> pOGeneralMessage, Action<string> pOMessage)
		{
			bool lbFirst = pOVitals.TryIncreaseSkill(piSkillNumber);
			bool lbSecond = pOVitals.TryIncreaseSkill(piSkillNumber);

			if (!lbFirst && !lbSecond)
			{
				pOGeneralMessage(NoFurtherAdvanceMessage);

				return;
			}

			pOGeneralMessage(KnowledgeFillsYouMessage);
			pOVitals.SpendSkillPoint();

			pOMessage(GetAdvancedGreatlyLine(pOData, piSkillNumber));
		}

		/// <summary>"You have advanced greatly in Sword." - with the full stop after the skill
		/// (great_advance, game/SKILLS.C:230; missing until 2026-10-15, audit A32).</summary>
		public static string GetAdvancedGreatlyLine(DataImport pOData, int piSkillNumber)
		{
			return fGetGeneralMessage(pOData, AdvancedGreatlyMessage) + fGetSkillName(piSkillNumber, pOData) + EndText;
		}

		/// <summary>
		/// The three group mantras. They pick a RANDOM skill from their group several
		/// times - the same one can come up more than once, and hitting a
		/// cap is simply ignored.
		///
		///   Combat    seven skills from attack, three attempts
		///   Magic     three from mana, two attempts - while mana is below 8 each attempt takes
		///             MANA at even odds, else one of the three
		///   Other     ten from traps, four attempts
		///
		/// THE POINT IS SPENT EVEN WHEN NOTHING ROSE: then "None of your skills improved." (until
		/// 2026-10-11 ours said "You are not ready to advance." and kept the point; UnderworldExhumed
		/// SKILLS.C mantra_advance, the same day).
		///
		/// THE REMAINING THREE SPECIAL MANTRAS are built since 2026-09-22 and sit in Chant:
		/// INSAHN says where the Cup of Wonder is, FANLO hands over the Key of Truth, and NO
		/// writes the empty line that the original writes there.
		/// </summary>
		private static void fAdvanceGroup(int piSpecial, UWPlayerVitals pOVitals, DataImport pOData,
			Action<int> pOGeneralMessage, Action<string> pOMessage)
		{
			if (!TryGetGroup(FirstSpecialMantra + piSpecial, out int liFirst, out int liCount, out int liTries))
			{
				pOGeneralMessage(NoSkillImprovedMessage);
				return;
			}

			List<string> lONames = new List<string>();

			for (int liAt = 0; liAt < liTries; liAt++)
			{
				int liSkill = liFirst == UWPlayerVitals.ManaSkillNumber && pOVitals.GetSkillByNumber(UWPlayerVitals.ManaSkillNumber) < 8
					&& UWRandom.Next(2) == 0
					? UWPlayerVitals.ManaSkillNumber
					: UWRandom.Next(liFirst, liFirst + liCount);

				if (pOVitals.TryIncreaseSkill(liSkill))
					lONames.Add(fGetSkillName(liSkill, pOData));
			}

			pOVitals.SpendSkillPoint();

			if (lONames.Count == 0)
			{
				pOGeneralMessage(NoSkillImprovedMessage);

				return;
			}

			pOMessage(GetAdvancedLine(pOData, lONames));
		}

		/// <summary>"You have advanced in Sword, Axe and Mace." - with the full stop after the
		/// list (report_advance, game/SKILLS.C:255; missing until 2026-10-15, audit A32).</summary>
		public static string GetAdvancedLine(DataImport pOData, List<string> pONames)
		{
			return fGetGeneralMessage(pOData, AdvancedMessage) + fJoinNames(pONames) + EndText;
		}

		/// <summary>"Sword, Axe and Mace" - the last one with "and", the ones before with a comma.
		/// </summary>
		private static string fJoinNames(List<string> pONames)
		{
			System.Text.StringBuilder lOText = new System.Text.StringBuilder();

			for (int liAt = 0; liAt < pONames.Count; liAt++)
			{
				if (liAt > 0)
					lOText.Append(liAt == pONames.Count - 1 ? " and " : ", ");

				lOText.Append(pONames[liAt]);
			}

			return lOText.ToString();
		}

		/// <summary>The skills a group mantra draws from (the first and how many, in save game
		/// order) and how many times it draws; false for a mantra that is not a group mantra.
		/// </summary>
		public static bool TryGetGroup(int piMantra, out int piFirstSkill, out int piSkillCount, out int piTries)
		{
			switch (piMantra - FirstSpecialMantra)
			{
				case 3: piFirstSkill = 0; piSkillCount = 7; piTries = 3; return true;
				case 4: piFirstSkill = 7; piSkillCount = 3; piTries = 2; return true;
				case 5: piFirstSkill = 10; piSkillCount = 10; piTries = 4; return true;
			}

			piFirstSkill = 0;
			piSkillCount = 0;
			piTries = 0;

			return false;
		}

		/// <summary>The words of a mantra, as the game spells them (for the help window).</summary>
		public static string GetMantra(int piMantra, DataImport pOData)
		{
			return pOData == null ? string.Empty : fGetString(pOData, 2, FirstMantraString + piMantra).Trim();
		}

		public static string GetSkillName(int piSkillNumber, DataImport pOData)
		{
			return pOData == null ? string.Empty : fGetSkillName(piSkillNumber, pOData);
		}

		private static string fGetSkillName(int piSkillNumber, DataImport pOData)
		{
			return fGetString(pOData, 2, FirstSkillNameString + piSkillNumber);
		}

		private static string fGetGeneralMessage(DataImport pOData, int piIndex)
		{
			return UWText.General(pOData, piIndex);
		}

		private static string fGetString(DataImport pOData, int piBlock, int piIndex)
		{
			return UWText.Block(pOData, piBlock, piIndex);
		}
	}
}
