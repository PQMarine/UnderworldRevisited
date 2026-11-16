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

			/// <summary>The test marks (UWTestMarks) whose square the line's chain starts on or aims at, null for none.</summary>
			public int[] Marks;

			/// <summary>The trap kind is not built.</summary>
			public bool NotBuilt;
		}

		/// <summary>This many lines stay visible.</summary>
		public const int MaxEntries = 8;

		/// <summary>The lines of marked chains are kept apart (UWSessionDiagnostics.MarkedTrapLog), this many, so the busy
		/// chains (timers, oscillators) do not push them out.</summary>
		public const int MaxMarkedEntries = 8;

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

		/// <summary>The lines of marked chains, oldest first.</summary>
		public static IReadOnlyList<Entry> MarkedEntries
		{
			get { return UWGameSession.Current.Diagnostics.MarkedTrapLog; }
		}

		public static void Clear()
		{
			UWGameSession.Current.Diagnostics.TrapLog.Clear();
			UWGameSession.Current.Diagnostics.MarkedTrapLog.Clear();
		}

		// ------------------------------------------------- The chain being run

		/// <summary>
		/// THE TRIGGER THAT STARTED THE CHAIN and who set it off (per user, 2026-11-15: a line showed only the target
		/// square, "target 29/53", while the trigger lay on 29/46, and not whether the player or Miranda had stepped on it).
		/// The games' outermost trigger call opens it (UWTrapRulesUw2's UseTrigger, UWTrapRules' first trigger) and closes
		/// it when the chain is done; the lines recorded meanwhile name it. Nested calls keep the outer one.
		/// </summary>
		private struct Chain
		{
			public UWObject Trigger;

			public string Setter;

			public bool IsUw2;

			/// <summary>1-based.</summary>
			public int Level;

			/// <summary>The thing on the map the trigger hangs on (a door's lock, an orb, a lever), null when the trigger
			/// lies on a square itself or nothing carries it.</summary>
			public UWObject Carrier;

			public string CarrierName;

			/// <summary>No real trigger: the game set the trap off on its square (the wandering monsters).</summary>
			public bool ByGame;
		}

		private static Chain msChain;

		private static int miChainDepth;

		/// <summary>Opens a chain (see Chain); every call is closed by EndChain, also when the chain throws. psSetter names
		/// who set it off where the host's firing does not tell (the wandering monsters: "the game").</summary>
		public static void BeginChain(UWObject pOTrigger, IUWTrapHost pIHost, string psSetter = null)
		{
			if (miChainDepth++ > 0)
				return;

			UWLevel lOLevel = pIHost != null && pIHost.World != null ? pIHost.World.Level : null;
			UWObject lOCarrier = psSetter == null && !fLiesOnMap(pOTrigger) && lOLevel != null ? fCarrierOf(pOTrigger, lOLevel.Masterlist) : null;

			msChain = new Chain
			{
				Trigger = pOTrigger,
				Setter = psSetter ?? SetterName(pIHost),
				ByGame = psSetter != null,
				IsUw2 = lOLevel != null && lOLevel.IsUw2,
				Level = lOLevel != null ? UWTrapRules.LevelIndexOf(pIHost) + 1 : 0,
				Carrier = lOCarrier,
				CarrierName = lOCarrier != null && pIHost.World.Data != null ? GetName(pIHost.World.Data, lOCarrier.ID) : null
			};
		}

		/// <summary>How far a carrier's special link chain is followed for its trigger (a door: the lock, then the trigger).</summary>
		private const int CarrierChainSteps = 8;

		/// <summary>
		/// THE THING A TRIGGER HANGS ON (per user, 2026-11-15: the open trigger of a storeroom door on UW1 level 1 lies on
		/// the door's lock, off the map, and its target is the square beside the door, so the line named neither the door
		/// nor the test mark on it): the object on the map whose special link, or the list it starts, holds the trigger.
		/// Only when a chain opens with a trigger that has no square.
		/// </summary>
		private static UWObject fCarrierOf(UWObject pOTrigger, List<UWObject> pOMaster)
		{
			if (pOTrigger == null || pOMaster == null)
				return null;

			foreach (UWObject lOObject in pOMaster)
			{
				if (lOObject == null || lOObject == pOTrigger || !fLiesOnMap(lOObject) || lOObject.HasQuantity || lOObject.Quantity == 0)
					continue;

				int liAt = lOObject.Quantity;

				for (int liStep = 0; liStep < CarrierChainSteps && liAt > 0 && liAt < pOMaster.Count; liStep++)
				{
					UWObject lOLinked = pOMaster[liAt];

					if (lOLinked == null)
						break;

					if (ReferenceEquals(lOLinked, pOTrigger))
						return lOObject;

					liAt = lOLinked.Link;
				}
			}

			return null;
		}

		public static void EndChain()
		{
			if (miChainDepth > 0 && --miChainDepth == 0)
				msChain = default;
		}

		/// <summary>Who set a chain off, for the log: "you", a creature by its personal name (block 7 by its whoami, as
		/// the conversation shows it) or else its kind, "a thing".</summary>
		public static string SetterName(IUWTrapHost pIHost)
		{
			if (pIHost == null)
				return null;

			UWTrapFiring lOFiring = pIHost.Firing;

			if (lOFiring.ByThing)
				return "a thing";

			if (!lOFiring.ByCreature)
				return "you";

			try
			{
				List<UWObject> lOMaster = pIHost.World != null && pIHost.World.Level != null ? pIHost.World.Level.Masterlist : null;
				UWObject lOCreature = lOMaster != null && lOFiring.SetterIndex > 0 && lOFiring.SetterIndex < lOMaster.Count
					? lOMaster[lOFiring.SetterIndex] : null;

				if (lOCreature == null)
					return "a creature";

				DataImport lOData = pIHost.World.Data;
				UWNpc lONpc = lOCreature as UWNpc;

				if (lONpc != null && lONpc.NPCwhoami > 0 && lONpc.NPCwhoami < FirstUnnamedWhoami && lOData != null && lOData.Strings != null)
				{
					string lsName = lOData.Strings.Blocks[UWConversations.PartnerNameStringBlock]
						.Strings[UWConversations.GetPartnerNameIndex(lONpc.NPCwhoami)].TrimEnd('\r', '\n');

					if (!string.IsNullOrEmpty(lsName))
						return lsName;
				}

				return "a " + GetName(lOData, lOCreature.ID);
			}
			catch (Exception)
			{
				return "a creature";
			}
		}

		/// <summary>From 0xF0 on a whoami has no personal name (UWConversationSession's FirstUnnamedWhoami).</summary>
		private const int FirstUnnamedWhoami = 0xF0;

		/// <summary>Records a fired trap. pbHandled is false if we have not built this
		/// trap type at all yet - exactly what you want to see while testing.</summary>
		public static void Record(UWObject pOTrap, UWObject pOTrigger, DataImport pOData, bool pbHandled)
		{
			if (pOTrap == null)
				return;

			System.Text.StringBuilder lOText = new System.Text.StringBuilder();

			lOText.Append(GetName(pOData, pOTrap.ID));
			lOText.AppendFormat(" (q {0}, own {1})", pOTrap.Quality, pOTrap.Owner);

			List<int> lONumbers = new List<int>();
			UWTestMarks lOMarks = UWTestMarks.Current;
			UWObject lOOrigin = miChainDepth > 0 ? msChain.Trigger : null;

			if (pOTrigger != null && lOOrigin != null && msChain.ByGame)
			{
				// No trigger to name: the game set the trap off on its square.
				(int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

				lOText.AppendFormat("  <- {0} on {1}/{2}", msChain.Setter, liTileX, liTileY);

				if (lOMarks != null)
					lOMarks.Collect(msChain.IsUw2, msChain.Level, liTileX, liTileY, lONumbers);
			}
			else if (pOTrigger != null)
			{
				(int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
				UWObject lOFirst = lOOrigin ?? pOTrigger;

				// The first trigger's own square (a trigger in a square's list knows it), the target when it differs, and who.
				lOText.AppendFormat("  <- {0}", GetName(pOData, lOFirst.ID));

				// A trigger off the map names the thing it hangs on and that thing's square.
				UWObject lOAt = fLiesOnMap(lOFirst) ? lOFirst : lOOrigin != null ? msChain.Carrier : null;

				if (lOAt != null && lOAt != lOFirst)
					lOText.AppendFormat(" in {0}", msChain.CarrierName);

				if (lOAt != null)
					lOText.AppendFormat(" on {0}/{1}", lOAt.TileX, lOAt.TileY);

				if (lOAt == null || lOAt.TileX != liTileX || lOAt.TileY != liTileY)
					lOText.AppendFormat(" target {0}/{1}", liTileX, liTileY);

				if (lOOrigin != null && !string.IsNullOrEmpty(msChain.Setter))
					lOText.AppendFormat(" by {0}", msChain.Setter);

				// Bit 1 of the trigger flags: off means the trap is removed afterwards.
				lOText.Append((pOTrigger.Flags & 0x2) != 0 ? "  [repeatable]" : "  [one-shot]");

				if (lOMarks != null && lOOrigin != null)
				{
					if (lOAt != null)
						lOMarks.Collect(msChain.IsUw2, msChain.Level, lOAt.TileX, lOAt.TileY, lONumbers);

					lOMarks.Collect(msChain.IsUw2, msChain.Level, liTileX, liTileY, lONumbers);
				}
			}

			if (!string.IsNullOrEmpty(Detail))
				lOText.AppendFormat("  {0}", Detail);

			if (!pbHandled)
				lOText.Append("  NOT BUILT");

			lONumbers.Sort();

			Entry lOEntry = new Entry
			{
				Time = Clock != null ? Clock() : 0f,
				Text = lOText.ToString(),
				Marks = lONumbers.Count > 0 ? lONumbers.ToArray() : null,
				NotBuilt = !pbHandled
			};

			fAdd(UWGameSession.Current.Diagnostics.TrapLog, lOEntry, MaxEntries);

			if (lOEntry.Marks != null)
				fAdd(UWGameSession.Current.Diagnostics.MarkedTrapLog, lOEntry, MaxMarkedEntries);
		}

		private static void fAdd(List<Entry> pOEntries, Entry pOEntry, int piMax)
		{
			pOEntries.Add(pOEntry);

			while (pOEntries.Count > piMax)
				pOEntries.RemoveAt(0);
		}

		/// <summary>A trigger out of a square's list (a chain's parked traps and triggers) has no square: 0/0.</summary>
		private static bool fLiesOnMap(UWObject pOObject)
		{
			return pOObject != null && (pOObject.TileX != 0 || pOObject.TileY != 0);
		}

		/// <summary>The raw object name from string block 4 (UWText.RawObjectName adds the plus
		/// one), the object number when there is none.</summary>
		public static string GetName(DataImport pOData, int piObjectId)
		{
			return UWText.RawObjectName(pOData, piObjectId, piObjectId.ToString());
		}
	}
}
