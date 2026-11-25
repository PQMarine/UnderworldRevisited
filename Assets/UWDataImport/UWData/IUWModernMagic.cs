namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE MODERN INTERFACE ASKS OF THE MAGIC (Docs/UW2-PLAN.md, "The modern interface for UW2: one
	/// interface, two hosts", stage U1, 2026-11-23): so far the active spells' row. The games keep their
	/// active spells differently (the first game's vitals hold effects, the second's record words), so the
	/// row asks for what it draws: a picture of SPELLS.GR and a text per place. Since stage U3 part B the
	/// runes, the shelf and the cast too: the bag's runes, what could be cast, the shelf's runes and the two
	/// ways to cast, which the first game answers from its player data, UWHudRunes and UWGameActions.
	/// </summary>
	public interface IUWModernMagic
	{
		/// <summary>Changes whenever the active spells change; the row rebuilds its pictures then.</summary>
		int ActiveSpellVersion { get; }

		int ActiveSpellCount { get; }

		/// <summary>The picture of SPELLS.GR for the spell in a place, -1 for a spell that shows none.</summary>
		int ActiveSpellPicture(int piSlot);

		/// <summary>The name and state of the spell in a place for the pointer's hover, empty for none.</summary>
		string ActiveSpellText(int piSlot);

		/// <summary>A click on an active spell: the game names it in its messages.</summary>
		void DescribeActiveSpell(int piSlot);

		/// <summary>The other button on an active spell ends it, where the game lets it end.</summary>
		void CancelActiveSpell(int piSlot);

		// ------------------------------------------------- The runes, the shelf and the cast (stage U3 part B)

		/// <summary>The game's rune page stands: until then nothing is chosen, looked at or cast.</summary>
		bool IsReady { get; }

		/// <summary>Another input mode holds the runes (a question, a spell being aimed): no rune click acts.</summary>
		bool IsHeldByAnotherInput { get; }

		/// <summary>How many runes the game has (the tablet's places).</summary>
		int RuneCount { get; }

		/// <summary>The object whose picture a rune shows on the tablet.</summary>
		int RuneObject(int piRune);

		/// <summary>The rune is in the character's bag.</summary>
		bool HasRune(int piRune);

		/// <summary>Every rune of the sequence is in the bag.</summary>
		bool HasRunes(int piSequence);

		/// <summary>The highest circle the character can cast.</summary>
		int Circle { get; }

		/// <summary>Whether the spell could be cast now, and if not why (the skill check still decides).</summary>
		UWSpellCasting.CastCheckEnum CastCheck(int piSequence);

		/// <summary>The runes chosen for the next cast, in order; null before the page stands.</summary>
		System.Collections.Generic.IReadOnlyList<int> Shelf { get; }

		void AddToShelf(int piRune);

		void PutOnShelf(System.Collections.Generic.IReadOnlyList<int> pORunes);

		void ClearShelf();

		/// <summary>The other button on a rune of the bag: the game names it.</summary>
		void LookAtRune(int piRune);

		/// <summary>Casts what lies on the shelf.</summary>
		void CastShelf();

		/// <summary>Casts a spell by its runes at once (the action bar), the shelf left as it is.</summary>
		void CastSequence(int piSequence);

		/// <summary>The spell's picture for the rune panel's list and the action bar: a lasting spell's in SPELLS.GR
		/// as the game shows it among the active spells, a missile's in OBJECTS.GR, else null. The games keep these
		/// tables differently (UW2 plan part U4c step 2: the first game's icon offsets, the second game's
		/// parse_aspells table and its own missiles).</summary>
		UWTexture SpellPicture(int piSequence);

		/// <summary>Which of the spells the manual does not name the list shows, a bit each in the order of
		/// UWKnowledgeSources.HiddenSpells: those learned in the game, all of them with the player's "show every"
		/// switch or in a game that hides none.</summary>
		int ShownHiddenSpells { get; }
	}
}
