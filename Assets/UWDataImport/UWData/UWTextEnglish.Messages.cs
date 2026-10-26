namespace UWDataImport.UWData
{
	public static partial class UWTextEnglish
	{
		/// <summary>
		/// The message lines UW.EXE assembles around a game name or from pieces of the strings
		/// (translation step 3A, 2026-10-26; until then written in code). The {0} {1} of a frame are
		/// filled by the grammar of the game-text pack's language (IUWGrammar): a thing's name as
		/// IUWGrammar.TheName gives it (English the bare singular - the frame writes "The"; German
		/// with the definite article in the frame's case), the player's own as YourName ("dein
		/// Dolch"), and the line goes through IUWGrammar.Sentence (German: a capital first letter, so a
		/// frame may start with its name, "{0} ist verschlossen."). Which case each {0} is in is said
		/// beside it; frames of only placeholders let a language reorder pieces of the strings.
		/// </summary>
		private static void fMessages()
		{
			// UWItemMessages: a container or a tool, {0} nominative.
			fAdd("msg.item.cannot_use_on", "The {0} cannot be used on that.");
			fAdd("msg.item.too_full", "The {0} is too full.");
			fAdd("msg.item.locked", "The {0} is locked.");
			fAdd("msg.item.empty", "The {0} is empty.");
			fAdd("msg.item.unnamed", "item");

			// A book or scroll before its text, {0} accusative.
			fAdd("msg.item.read", "You read the {0}...");
			fAdd("msg.item.read_unnamed", "UNNAMED");

			// Food without a line of its own: {0} the thing (nominative), {1} the taste of block 1
			// (" tasted great.").
			fAdd("msg.item.taste", "That {0}{1}");

			// A launcher without its ammunition, {0} the plural.
			fAdd("msg.item.no_ammunition", "Sorry, you have no {0}.");

			// A theft seen: {0} the creature with its indefinite article (nominative), {1} the line of
			// its attitude in block 1 (" is angered by your action.").
			fAdd("msg.item.angered", "{0}{1}");

			// Worn equipment (UWEquipmentWear), {0} the player's thing (nominative); the plural lines
			// for a name that takes its verb in the plural.
			fAdd("msg.wear.damaged", "Your {0} was damaged.");
			fAdd("msg.wear.damaged_plural", "Your {0} were damaged.");
			fAdd("msg.wear.destroyed", "Your {0} was destroyed.");
			fAdd("msg.wear.destroyed_plural", "Your {0} were destroyed.");

			// Disarming (UWTrapDisarming): {0} the trap (nominative), {1} the thing it is on (dative);
			// the set-off line names the trap in the accusative and has no full stop in UW.EXE.
			fAdd("msg.trap.disarmed", "The {0} on the {1} was successfully dearmed.");
			fAdd("msg.trap.set_off", "Your bumbling attempts have set off the {0}");
			fAdd("msg.trap.failed", "Unable to defuse trap.");

			// The anvil (UWRepairRules), pieces of block 1: the estimate {0} "You think it will be ", {1}
			// the difficulty word, {2} " to repair the ", {3} the thing (accusative), {4} the question;
			// the outcome {0} its line of block 1, {1} the thing (accusative).
			fAdd("msg.repair.estimate", "{0}{1}{2}{3}{4}");
			fAdd("msg.repair.result", "{0}{1}.");

			// The level-up (UWPlayerVitals): {0} the line of block 1, {1} the level as UW.EXE writes it
			// (a blank before a single digit); the fallback when the strings lack the line.
			fAdd("msg.level_up", "{0}{1}");
			fAdd("msg.level_up.fallback", "You have attained experience level ");

			// The shrine (UWShrineRules): the prompt; an advance, {0} the line of block 1, {1} the skill
			// or the list of skills; the list's joints; INSAHN's joints between the pieces of block 1.
			fAdd("msg.shrine.prompt", "Chant the mantra: ");
			fAdd("msg.shrine.advanced_greatly", "{0}{1}.");
			fAdd("msg.shrine.advanced", "{0}{1}.");
			fAdd("msg.shrine.list_comma", ", ");
			fAdd("msg.shrine.list_and", " and ");
			fAdd("msg.shrine.cup_and", " and ");
			fAdd("msg.shrine.cup_very_near", "very near");
			fAdd("msg.shrine.cup_end", ".");

			// The fixed lines UW.EXE keeps (and a few of the port's own) that were literals in code
			// until step 3A: Bronus' book, the runes that are no spell, the action bar's missing rune,
			// the classic door that is locked, the flask's "out of" between two numbers, the save list's
			// heading (with the scroll's colour code \6 - keep it), the count prompt, the answers of a
			// yes/no question, an empty save slot, the manual dealing of the attribute points.
			fAdd("msg.book.explodes", "The book explodes in your face!");
			fAdd("msg.spell.not_a_spell", "Not a spell");
			fAdd("msg.spell.rune_missing", "A rune of that spell is not in your bag.");
			fAdd("msg.door.locked", "The door is locked");
			fAdd("msg.flask.out_of", " out of ");
			fAdd("msg.save.list_heading", "\\6     Save Game Descriptions");
			fAdd("msg.save.unused_slot", "<not used yet>");
			fAdd("msg.count.question", "Move how many? ");
			fAdd("msg.question.yes", "Yes");
			fAdd("msg.question.no", "No");
			fAdd("msg.creation.points_left", "Points left: ");
			fAdd("msg.creation.done", "Done");

			// The scroll's hint that more of a text is pending (UWMessageLogModel), white in brackets.
			fAdd("msg.scroll.more", "[MORE]");
		}
	}
}
