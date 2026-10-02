using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The game's quest flags (uw-formats.txt 7.8). Conversations read and set them, and
	/// through them NPCs know about each other: whether you have talked to Hagbard, whether the
	/// gazer is dead, how far the Crux Ansata quest has progressed.
	///
	/// Deliberately a simple cache for the game session. In the original the values live in
	/// the save game; UWLevelLoader fills this cache from a loaded save game and
	/// UWSavegameWriter writes it back.
	/// </summary>
	public static class UWQuestFlags
	{
		private static readonly Dictionary<int, int> mOFlags = new Dictionary<int, int>();

		public static int Get(int piFlag)
		{
			int liValue;

			return mOFlags.TryGetValue(piFlag, out liValue) ? liValue : 0;
		}

		public static void Set(int piFlag, int piValue)
		{
			mOFlags[piFlag] = piValue;
		}

		/// <summary>All set flags, for debugging.</summary>
		public static IReadOnlyDictionary<int, int> All
		{
			get { return mOFlags; }
		}

		public static void Clear()
		{
			mOFlags.Clear();
		}
	}
}
