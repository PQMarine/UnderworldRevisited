using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE THE GAME'S STRINGS ARE PIECES OF A FRAME (translation wave 4, 2026-10-26): the strings
	/// of STRINGS.PAK that the code puts into one of the port's frames as an argument
	/// (UWTextFragment) - the anvil's outcomes and estimate, the taste, the theft, the level-up, the
	/// shrine's advance. A language may write slots into these strings ({n}, select, plural - the
	/// frame's arguments, UWMessageFormat) and, in its Languages/&lt;code&gt;.tsv, leave out of such a
	/// frame an argument its fragments name instead ("Halb geflickt hast du {1}" with the frame
	/// msg.repair.result "{0}."). Only these frames may leave an argument out (UWTextLanguageFile);
	/// `uwdump validate-tsv` checks a fragment's slots against its frame and that a frame's left-out
	/// argument is named by every fragment of it. A new call site that passes UWText.Fragment belongs
	/// in this table (the self-check MessageFormat compares them).
	///
	/// PER GAME (2026-11-25, the second game's game-text pack): the same frames take the second game's
	/// own lines, at other places of its block 1 - the anvil, the taste, the theft and the level-up (it
	/// has no shrines). Each use names its game.
	/// </summary>
	public static class UWTextFragments
	{
		/// <summary>A range of strings used as one argument of a frame.</summary>
		public sealed class Use
		{
			public int Block;

			public int First;

			public int Last;

			/// <summary>The frame's id (UWText).</summary>
			public string Frame;

			/// <summary>Which argument of the frame the string is.</summary>
			public int Argument;

			/// <summary>What the frame's arguments are, for the translator.</summary>
			public string Arguments;

			/// <summary>The game whose strings these are.</summary>
			public UWGame Game = UWGame.Uw1;
		}

		/// <summary>Every use, by block and first index.</summary>
		public static readonly Use[] Uses =
		{
			new Use { Block = 1, First = 29, Last = 29, Frame = "msg.shrine.advanced_greatly", Argument = 0, Arguments = "{0} this piece, {1} the skill" },
			new Use { Block = 1, First = 30, Last = 30, Frame = "msg.shrine.advanced", Argument = 0, Arguments = "{0} this piece, {1} the skills" },
			new Use { Block = 1, First = UWRepairRules.CannotRepairMessage - 2, Last = UWRepairRules.CannotRepairMessage - 1, Frame = "msg.repair.result", Argument = 0,
				Arguments = "{0} this piece, {1} the thing (accusative)" },
			new Use { Block = 1, First = UWRepairRules.CannotRepairMessage + 1, Last = UWRepairRules.CannotRepairMessage + 3, Frame = "msg.repair.result", Argument = 0,
				Arguments = "{0} this piece, {1} the thing (accusative)" },
			new Use { Block = 1, First = UWPlayerVariantUw1.LevelUpMessage, Last = UWPlayerVariantUw1.LevelUpMessage, Frame = "msg.level_up", Argument = 0, Arguments = "{0} this piece, {1} the level (a blank before one digit)" },
			new Use { Block = 1, First = UWItemMessages.FirstTasteMessage, Last = UWItemMessages.FirstTasteMessage + 4, Frame = "msg.item.taste", Argument = 1,
				Arguments = "{0} the food (nominative), {1} this piece" },
			new Use { Block = 1, First = UWRepairRules.RepairEstimatePrefix, Last = UWRepairRules.RepairEstimatePrefix, Frame = "msg.repair.estimate", Argument = 0, Arguments = RepairEstimate },
			new Use { Block = 1, First = UWRepairRules.RepairEstimateInfix, Last = UWRepairRules.RepairEstimateInfix, Frame = "msg.repair.estimate", Argument = 2, Arguments = RepairEstimate },
			new Use { Block = 1, First = UWRepairRules.RepairEstimateSuffix, Last = UWRepairRules.RepairEstimateSuffix, Frame = "msg.repair.estimate", Argument = 4, Arguments = RepairEstimate },
			new Use { Block = 1, First = UWRepairRules.FirstRepairDifficultyWord, Last = UWRepairRules.FirstRepairDifficultyWord + UWRepairRules.DifficultyWordCount - 1,
				Frame = "msg.repair.estimate", Argument = 1, Arguments = RepairEstimate },
			new Use { Block = 1, First = UWCritterRules.TheftMessageBase, Last = UWCritterRules.TheftMessageBase + 2, Frame = "msg.item.angered", Argument = 1,
				Arguments = Angered },

			// The second game.
			new Use { Game = UWGame.Uw2, Block = 1, First = UWRepairRules.Uw2CannotRepairMessage - 2, Last = UWRepairRules.Uw2CannotRepairMessage - 1,
				Frame = "msg.repair.result", Argument = 0, Arguments = "{0} this piece, {1} the thing (accusative)" },
			new Use { Game = UWGame.Uw2, Block = 1, First = UWRepairRules.Uw2CannotRepairMessage + 1, Last = UWRepairRules.Uw2CannotRepairMessage + 3,
				Frame = "msg.repair.result", Argument = 0, Arguments = "{0} this piece, {1} the thing (accusative)" },
			new Use { Game = UWGame.Uw2, Block = 1, First = UWPlayerVariantUw2.LevelUpMessage, Last = UWPlayerVariantUw2.LevelUpMessage, Frame = "msg.level_up", Argument = 0,
				Arguments = "{0} this piece, {1} the level (a blank before one digit)" },
			new Use { Game = UWGame.Uw2, Block = 1, First = UWFoodUw2.FirstTasteMessage, Last = UWFoodUw2.FirstTasteMessage + 4, Frame = "msg.item.taste", Argument = 1,
				Arguments = "{0} the food (nominative), {1} this piece" },
			new Use { Game = UWGame.Uw2, Block = 1, First = UWRepairRules.Uw2RepairEstimatePrefix, Last = UWRepairRules.Uw2RepairEstimatePrefix, Frame = "msg.repair.estimate",
				Argument = 0, Arguments = Uw2RepairEstimate },
			new Use { Game = UWGame.Uw2, Block = 1, First = UWRepairRules.Uw2RepairEstimateInfix, Last = UWRepairRules.Uw2RepairEstimateInfix, Frame = "msg.repair.estimate",
				Argument = 2, Arguments = Uw2RepairEstimate },
			new Use { Game = UWGame.Uw2, Block = 1, First = UWRepairRules.Uw2RepairEstimateSuffix, Last = UWRepairRules.Uw2RepairEstimateSuffix, Frame = "msg.repair.estimate",
				Argument = 4, Arguments = Uw2RepairEstimate },
			new Use { Game = UWGame.Uw2, Block = 1, First = UWRepairRules.Uw2FirstRepairDifficultyWord,
				Last = UWRepairRules.Uw2FirstRepairDifficultyWord + UWRepairRules.DifficultyWordCount - 1, Frame = "msg.repair.estimate", Argument = 1, Arguments = Uw2RepairEstimate },
			new Use { Game = UWGame.Uw2, Block = 1, First = UWCritterRules.Uw2TheftMessageBase, Last = UWCritterRules.Uw2TheftMessageBase + 2, Frame = "msg.item.angered",
				Argument = 1, Arguments = Angered }
		};

		private const string RepairEstimate = "{0} 1:217, {1} the difficulty word 1:220-224, {2} 1:218, {3} the thing (accusative), {4} 1:219";

		private const string Uw2RepairEstimate = "{0} 1:232, {1} the difficulty word 1:235-239, {2} 1:233, {3} the thing (accusative), {4} 1:234";

		private const string Angered = "{0} the creature with its indefinite article (nominative), {1} this piece";

		/// <summary>The port's words a grammar puts before or after a name (UWGrammarGeneric): their text
		/// is a fragment with {0} the name, so a language may make them agree with it
		/// ("{0,select,f{maudite}other{maudit}}"); the English has no {0} there.</summary>
		public static readonly string[] WordIds = { "grammar.word.magical", "grammar.word.cursed" };

		/// <summary>Whether an id's text is such a word.</summary>
		public static bool IsWord(string psId)
		{
			return System.Array.IndexOf(WordIds, psId) >= 0;
		}

		/// <summary>The use of a string of a game, or null when it is no piece of a frame.</summary>
		public static Use Find(int piBlock, int piIndex, UWGame peGame = UWGame.Uw1)
		{
			foreach (Use lOUse in Uses)
			{
				if (lOUse.Game == peGame && lOUse.Block == piBlock && piIndex >= lOUse.First && piIndex <= lOUse.Last)
					return lOUse;
			}

			return null;
		}

		/// <summary>Whether a frame takes game fragments among its arguments (in either game: the port's
		/// own texts serve both).</summary>
		public static bool TakesFragments(string psFrame)
		{
			foreach (Use lOUse in Uses)
			{
				if (lOUse.Frame == psFrame)
					return true;
			}

			return false;
		}

		/// <summary>The uses of one frame in a game, in the table's order.</summary>
		public static List<Use> Of(string psFrame, UWGame peGame = UWGame.Uw1)
		{
			List<Use> lOUses = new List<Use>();

			foreach (Use lOUse in Uses)
			{
				if (lOUse.Game == peGame && lOUse.Frame == psFrame)
					lOUses.Add(lOUse);
			}

			return lOUses;
		}
	}
}
