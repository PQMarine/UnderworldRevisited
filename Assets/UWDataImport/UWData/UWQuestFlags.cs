using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The game's quest flags (uw-formats.txt 7.8). Conversations read and set them, and
	/// through them NPCs know about each other: whether you have talked to Hagbard, whether the
	/// gazer is dead, how far the Crux Ansata quest has progressed.
	///
	/// In the original the values live in the save game; the session is seeded from a loaded
	/// save game (UWGameSession.SeedFrom) and UWSavegameWriter writes them back.
	///
	/// SINCE 2026-10-14 (cleanup step 2.7) the values belong to the session
	/// (UWGameSession.Quests); this class forwards to the current one until its callers take
	/// the session themselves.
	/// </summary>
	public static class UWQuestFlags
	{
		public static int Get(int piFlag)
		{
			return UWGameSession.Current.Quests.Get(piFlag);
		}

		public static void Set(int piFlag, int piValue)
		{
			UWGameSession.Current.Quests.Set(piFlag, piValue);
		}

		/// <summary>All set flags, for debugging.</summary>
		public static IReadOnlyDictionary<int, int> All
		{
			get { return UWGameSession.Current.Quests.All; }
		}

		public static void Clear()
		{
			UWGameSession.Current.Quests.Clear();
		}
	}
}
