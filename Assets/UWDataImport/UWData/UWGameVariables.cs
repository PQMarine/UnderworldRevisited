using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The GAME VARIABLES of the original - the scratch store in which puzzles keep their
	/// state.
	///
	/// They are not the same as the quest flags (see UWQuestFlags). In the savegame the two are
	/// stored separately: the quest flags from 0x65, the first 32 of them as individual bits, and the
	/// game variables as whole bytes from 0x70 (reference: playerdatquest.GetGameVariable reads
	/// 0x71 + number, counted with the key byte before it).
	///
	/// They are written and read by the variable traps 397 and 398 (see
	/// UWTriggerSystem). An example that shows their purpose is on level 5:
	/// three traps each increase variables 31, 32 and 33 by one, and a check trap
	/// compares the three packed together against 470 - i.e. 7, 2 and 6. Only with this
	/// combination does the chain continue.
	///
	/// VALUE RANGE: six bits. The reference masks every arithmetic result of the trap with 0x3F,
	/// not only when storing it. A left shift therefore overflows at the top, and the puzzle on
	/// level 3 relies on exactly that: variable 16 is set to 56 and then shifted once,
	/// which would be 112 - masked, 48 remains, and the trap there checks exactly against 48.
	///
	/// Deliberately a simple scratch store for the game session, like UWQuestFlags. Values are
	/// loaded from the savegame by UWLevelLoader and written back by UWSavegameWriter, so a
	/// puzzle keeps its state across saving and loading.
	/// </summary>
	public static class UWGameVariables
	{
		/// <summary>Six bits, see class comment.</summary>
		public const int ValueMask = 0x3F;

		private static readonly Dictionary<int, int> mOValues = new Dictionary<int, int>();

		public static int Get(int piVariable)
		{
			int liValue;

			return mOValues.TryGetValue(piVariable, out liValue) ? liValue : 0;
		}

		public static void Set(int piVariable, int piValue)
		{
			mOValues[piVariable] = piValue & ValueMask;
		}

		/// <summary>All variables that have been set, for debugging and later for
		/// saving.</summary>
		public static IReadOnlyDictionary<int, int> All
		{
			get { return mOValues; }
		}

		public static void Clear()
		{
			mOValues.Clear();
		}
	}
}
