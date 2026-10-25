using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FOLDED SECTIONS OF THE DEBUG PANEL as the settings keep them (cleanup 8c, 2026-10-25;
	/// UWUserData.DebugFolded): the folded sections' ids, comma-separated, sorted. An id the report does
	/// not know (a section of another game, UW2) is kept, so the two never undo each other's folds; an
	/// empty text, as every older settings file reads, folds nothing.
	/// </summary>
	public static class UWDebugFolds
	{
		public const char Separator = ',';

		/// <summary>The folded ids of a stored text (blanks around an id ignored, empty ids dropped).</summary>
		public static HashSet<string> Parse(string psStored)
		{
			HashSet<string> lOFolded = new HashSet<string>(StringComparer.Ordinal);

			if (string.IsNullOrEmpty(psStored))
				return lOFolded;

			foreach (string lsPart in psStored.Split(Separator))
			{
				string lsId = lsPart.Trim();

				if (lsId.Length > 0)
					lOFolded.Add(lsId);
			}

			return lOFolded;
		}

		/// <summary>The text to store: the ids sorted, so the same folds always write the same text.</summary>
		public static string Format(IEnumerable<string> pOFolded)
		{
			List<string> lOIds = new List<string>();

			if (pOFolded != null)
			{
				foreach (string lsId in pOFolded)
				{
					if (!string.IsNullOrEmpty(lsId) && lsId.IndexOf(Separator) < 0 && !lOIds.Contains(lsId))
						lOIds.Add(lsId);
				}
			}

			lOIds.Sort(StringComparer.Ordinal);

			return string.Join(Separator.ToString(), lOIds);
		}

		/// <summary>The stored text with one section folded or unfolded.</summary>
		public static string Toggle(string psStored, string psId)
		{
			HashSet<string> lOFolded = Parse(psStored);

			if (!lOFolded.Remove(psId))
				lOFolded.Add(psId);

			return Format(lOFolded);
		}
	}

	/// <summary>
	/// A VALUE THAT CHANGED LIGHTS UP BRIEFLY (cleanup 8c, 2026-10-25): for the traps and the creature's
	/// behaviour, which change between two looks. Each report is noted with the game's clock; a row whose
	/// value differs from what the same row (section and key) showed in the report before, or a row that
	/// is new in a section that was there before, starts its light, which fades over FadeSeconds by the
	/// clock - never by counting frames, so it lasts as long at any frame rate. Live rows (a position,
	/// an age) never light; a log line (UWDebugRowFlags.Log) lights only when it is new. The first report
	/// after Reset lights nothing, so opening the panel does not flash it whole.
	/// </summary>
	public sealed class UWDebugFlash
	{
		/// <summary>How long a light takes to fade, in seconds.</summary>
		public const float FadeSeconds = 1f;

		private struct Seen
		{
			public string Value;

			public float ChangedAt;
		}

		private Dictionary<string, Seen> mOSeen = new Dictionary<string, Seen>(StringComparer.Ordinal);

		private Dictionary<string, Seen> mONext = new Dictionary<string, Seen>(StringComparer.Ordinal);

		private readonly HashSet<string> mOSections = new HashSet<string>(StringComparer.Ordinal);

		private readonly HashSet<string> mONextSections = new HashSet<string>(StringComparer.Ordinal);

		private bool mbPrimed;

		/// <summary>Forgets every value: the next report lights nothing (the panel opened again, the
		/// CritterTrace mode switched).</summary>
		public void Reset()
		{
			mOSeen.Clear();
			mOSections.Clear();
			mbPrimed = false;
		}

		private static string fKey(UWDebugSection pOSection, UWDebugRow pORow)
		{
			return pOSection.Id + "/" + pORow.Key;
		}

		/// <summary>Notes this frame's report at the clock's pfNow; rows no longer in it are forgotten.</summary>
		public void Note(UWDebugReport pOReport, float pfNow)
		{
			mONext.Clear();
			mONextSections.Clear();

			foreach (UWDebugSection lOSection in pOReport.Sections)
			{
				bool lbSectionKnown = mOSections.Contains(lOSection.Id);

				mONextSections.Add(lOSection.Id);

				foreach (UWDebugRow lORow in lOSection.Rows)
				{
					string lsKey = fKey(lOSection, lORow);
					Seen lOSeen;
					bool lbKnown = mOSeen.TryGetValue(lsKey, out lOSeen);
					float lfChangedAt = lbKnown ? lOSeen.ChangedAt : float.NegativeInfinity;

					if (mbPrimed && !lORow.Is(UWDebugRowFlags.Live) && !lORow.Is(UWDebugRowFlags.Sub))
					{
						bool lbNew = !lbKnown && lbSectionKnown;
						bool lbChanged = lbKnown && !lORow.Is(UWDebugRowFlags.Log) && lOSeen.Value != lORow.Value;

						if (lbNew || lbChanged)
							lfChangedAt = pfNow;
					}

					mONext[lsKey] = new Seen { Value = lORow.Value, ChangedAt = lfChangedAt };
				}
			}

			Dictionary<string, Seen> lOSwap = mOSeen;

			mOSeen = mONext;
			mONext = lOSwap;
			mOSections.Clear();
			mOSections.UnionWith(mONextSections);
			mbPrimed = true;
		}

		/// <summary>How strongly the row lights at the clock's pfNow: 1 when it just changed, fading
		/// linearly to 0 over FadeSeconds.</summary>
		public float Strength(UWDebugSection pOSection, UWDebugRow pORow, float pfNow)
		{
			Seen lOSeen;

			if (!mOSeen.TryGetValue(fKey(pOSection, pORow), out lOSeen) || float.IsNegativeInfinity(lOSeen.ChangedAt))
				return 0f;

			float lfAge = pfNow - lOSeen.ChangedAt;

			if (lfAge < 0f || lfAge >= FadeSeconds)
				return lfAge < 0f ? 1f : 0f;

			return 1f - (lfAge / FadeSeconds);
		}
	}

	/// <summary>
	/// THE PANEL'S COLUMNS (cleanup 8c, 2026-10-25): two columns of sections side by side when the
	/// window is wide enough for both within the share of it the panel may take, one when narrow; the
	/// sections keep their order, the first column taking them until the split that leaves the taller
	/// column lowest.
	/// </summary>
	public static class UWDebugColumns
	{
		/// <summary>How many columns fit: two when both and the gap fit in pfAvailable, else one.</summary>
		public static int Count(float pfAvailable, float pfColumnWidth, float pfGap)
		{
			return pfAvailable >= (2f * pfColumnWidth) + pfGap ? 2 : 1;
		}

		/// <summary>
		/// Which column each section goes to (paColumnOf, 0 or 1), in order: with one column all in the
		/// first; with two the first k sections in the first column, k chosen so the taller column is
		/// as low as it can be (the earlier split on a tie). Returns the taller column's height.
		/// </summary>
		public static float Split(float[] paHeights, int piColumns, int[] paColumnOf)
		{
			int liCount = paHeights.Length;
			float lfTotal = 0f;

			for (int liAt = 0; liAt < liCount; liAt++)
				lfTotal += paHeights[liAt];

			if (piColumns < 2 || liCount < 2)
			{
				for (int liAt = 0; liAt < liCount; liAt++)
					paColumnOf[liAt] = 0;

				return lfTotal;
			}

			int liBest = liCount;
			float lfBest = lfTotal;
			float lfFirst = 0f;

			for (int liSplit = 1; liSplit < liCount; liSplit++)
			{
				lfFirst += paHeights[liSplit - 1];

				float lfTaller = Math.Max(lfFirst, lfTotal - lfFirst);

				if (lfTaller < lfBest)
				{
					lfBest = lfTaller;
					liBest = liSplit;
				}
			}

			for (int liAt = 0; liAt < liCount; liAt++)
				paColumnOf[liAt] = liAt < liBest ? 0 : 1;

			return lfBest;
		}
	}
}
