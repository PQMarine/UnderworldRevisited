namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE MODERN INTERFACE ASKS OF THE CHARACTER (Docs/UW2-PLAN.md, "The modern interface for UW2: one
	/// interface, two hosts", stage U1, 2026-11-23): the numbers its windows show and what a click on them
	/// says. Each game's host answers from its own character - the first game's from UWCharacter's vitals,
	/// the second's from its player record. It grows with the stages by what the windows ask, no further.
	/// </summary>
	public interface IUWModernCharacter
	{
		int HitPoints { get; }

		int MaxHitPoints { get; }

		int Mana { get; }

		int MaxMana { get; }

		/// <summary>A click on a flask: the game says what the classic frame's flask says (how poisoned and the
		/// vitality, or the mana) in its own message lines.</summary>
		void DescribeFlask(UWClickRules.FlaskPart pePart);

		/// <summary>A click on the heading or the compass: the game reports the character's state in its
		/// messages, as the classic compass's click does (stage U3 part B).</summary>
		void ReportStatus();

		/// <summary>A character is loaded (stage U3 part D: the weight in the pack shows only then).</summary>
		bool IsLoaded { get; }

		/// <summary>The most the character carries, in tenths of a stone: the weight left on the character page and
		/// the backpack window's load (stage U5 step 3; both read the first game's player data before, so the second
		/// game's page showed 0, per user at the test of 197).</summary>
		int MaxLoad { get; }

		/// <summary>Fills a page's sheet with the character as it is now - name, class, level, the attributes,
		/// the skills, the vitals - and hands it back (UWCharacterSheet, the pages' one reading of the character).</summary>
		UWCharacterSheet BindSheet(UWCharacterSheet pOSheet);
	}
}
