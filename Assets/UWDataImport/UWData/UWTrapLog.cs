using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Remembers which traps fired most recently - for the overlay
	/// (UWDebugOverlay).
	///
	/// Reason: while testing the damage traps on level 7 the user took damage without
	/// being able to tell WHICH trap it was (2026-09-06). Level 7 has twenty-eight
	/// move triggers that share three traps dealing 3, 4 and 40 damage, plus eight
	/// pickup triggers at the treasure.
	///
	/// Deliberately no log line: the overlay is on screen and can be hidden with the same key
	/// as the rest. A log in the console would not be visible while playing.
	///
	/// Engine-free since 2026-09-18 (with the trap rules): the time stamp comes from Clock,
	/// which the Unity side connects to its game time (UWLogBridge).
	/// </summary>
	public static class UWTrapLog
	{
		public struct Entry
		{
			/// <summary>The clock value when fired - the overlay computes the age from it.</summary>
			public float Time;

			public string Text;
		}

		/// <summary>This many lines stay visible.</summary>
		public const int MaxEntries = 6;

		/// <summary>The time source for Entry.Time; zero without one.</summary>
		public static Func<float> Clock;

		private static readonly List<Entry> mOEntries = new List<Entry>();

		public static IReadOnlyList<Entry> Entries
		{
			get { return mOEntries; }
		}

		/// <summary>
		/// Extra info contributed by an individual trap type - for example the actual damage.
		/// Cleared before every trap and appended to the line afterwards.
		/// </summary>
		public static string Detail { get; set; }

		public static void Clear()
		{
			mOEntries.Clear();
		}

		/// <summary>Records a fired trap. pbHandled is false if we have not built this
		/// trap type at all yet - exactly what you want to see while testing.</summary>
		public static void Record(UWObject pOTrap, UWObject pOTrigger, DataImport pOData, bool pbHandled)
		{
			if (pOTrap == null)
				return;

			System.Text.StringBuilder lOText = new System.Text.StringBuilder();

			lOText.Append(GetName(pOData, pOTrap.ID));
			lOText.AppendFormat(" (q {0}, own {1})", pOTrap.Quality, pOTrap.Owner);

			if (pOTrigger != null)
			{
				(int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

				lOText.AppendFormat("  <- {0} target {1}/{2}",
					GetName(pOData, pOTrigger.ID), liTileX, liTileY);

				// Bit 1 of the trigger flags: off means the trap is removed afterwards.
				lOText.Append((pOTrigger.Flags & 0x2) != 0 ? "  [repeatable]" : "  [one-shot]");
			}

			if (!string.IsNullOrEmpty(Detail))
				lOText.AppendFormat("  {0}", Detail);

			if (!pbHandled)
				lOText.Append("  NOT BUILT");

			mOEntries.Add(new Entry { Time = Clock != null ? Clock() : 0f, Text = lOText.ToString() });

			while (mOEntries.Count > MaxEntries)
				mOEntries.RemoveAt(0);
		}

		/// <summary>The object name from string block 4. It is stored there under the object number plus
		/// one.</summary>
		public static string GetName(DataImport pOData, int piObjectId)
		{
			try
			{
				string lsName = pOData.Strings.Blocks[4].Strings[piObjectId + 1].Trim();

				return string.IsNullOrEmpty(lsName) ? piObjectId.ToString() : lsName;
			}
			catch
			{
				return piObjectId.ToString();
			}
		}
	}
}
