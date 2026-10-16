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

		/// <summary>The time source for Entry.Time; zero without one. The host's wiring, not
		/// game state, so it stays here.</summary>
		public static Func<float> Clock;

		/// <summary>The lines live in the session since 2026-10-14 (UWGameSession.Diagnostics,
		/// cleanup step 2.7): a new game starts with an empty log.</summary>
		public static IReadOnlyList<Entry> Entries
		{
			get { return UWGameSession.Current.Diagnostics.TrapLog; }
		}

		/// <summary>
		/// Extra info contributed by an individual trap type - for example the actual damage.
		/// Cleared before every trap and appended to the line afterwards. A side channel kept in
		/// the session's diagnostics (UWSessionDiagnostics.TrapDetail).
		/// </summary>
		public static string Detail
		{
			get { return UWGameSession.Current.Diagnostics.TrapDetail; }
			set { UWGameSession.Current.Diagnostics.TrapDetail = value; }
		}

		public static void Clear()
		{
			UWGameSession.Current.Diagnostics.TrapLog.Clear();
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

			List<Entry> lOEntries = UWGameSession.Current.Diagnostics.TrapLog;

			lOEntries.Add(new Entry { Time = Clock != null ? Clock() : 0f, Text = lOText.ToString() });

			while (lOEntries.Count > MaxEntries)
				lOEntries.RemoveAt(0);
		}

		/// <summary>The raw object name from string block 4 (UWText.RawObjectName adds the plus
		/// one), the object number when there is none.</summary>
		public static string GetName(DataImport pOData, int piObjectId)
		{
			return UWText.RawObjectName(pOData, piObjectId, piObjectId.ToString());
		}
	}
}
