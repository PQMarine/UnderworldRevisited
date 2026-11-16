using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S TRIGGERS AND TRAP CHAINS (UW2 plan phase 4, G3 part H1, 2026-11-13), from Underworld Exhumed's
	/// uw2 event/TRIGGER.C: UseTrigger, SetOffTrap, UseTrap's chain and its else branch, the numbered variables. Its own
	/// class beside UWTrapRules (decided by the user 2026-11-13): the second game's TRIGGER.C differs in nearly every step
	/// - who may set a trigger off (a thing needs id bit 9), the trigger modes (16 of them, two minor classes), the chain's
	/// else branch, the numbered variables over the quests and X clocks. What both games share is called, not copied: the
	/// frame (UWTrapTable, IUWTrapHost, UWTrapLog, UWTrapChainRemoval) and the kinds that are the same code (the damage
	/// trap, the arrow's launch).
	///
	/// THE CHAIN (UseTrap's tail): after a kind's case the trap's link is followed when it names a trap or a trigger -
	/// a trap runs at the same square, a trigger with mode -1 (no mode or setter test) at its own; the create-object,
	/// delete-object and ward traps end it, the door trap when its owner is set (part H1b). A CONDITION trap that fails
	/// takes the else branch instead (RUN_ELSE_CHAIN): the object after the one it links to, and the chain goes on from
	/// there only - the variable check here, the skill, proximity and inventory checks with their kinds (H1b, H3).
	/// </summary>
	public static class UWTrapRulesUw2
	{
		// ------------------------------------------------- The trigger modes (Triggers[16], OBJECTS.DAT 0xD92)

		public const int MoveMode = 0;

		public const int PickUpMode = 2;

		public const int UseMode = 4;

		public const int LookMode = 5;

		public const int EnterMode = 6;

		public const int PressureMode = 7;

		public const int OpenMode = 8;

		public const int CloseMode = 9;

		public const int TimerMode = 0xA;

		public const int UnlockMode = 0xB;

		public const int ScheduledMode = 0xC;

		public const int ExitMode = 0xE;

		public const int ReleaseMode = 0xF;

		/// <summary>A trigger reached along a chain or by a special case: no mode and no setter test.</summary>
		public const int ChainMode = -1;

		/// <summary>The shipped table, the same 16 bytes for 0x1A0-0x1AF and 0x1B0-0x1BF (trap_init reads them; the
		/// self-check compares it with the game's OBJECTS.DAT).</summary>
		private static readonly int[] miModes = { MoveMode, PickUpMode, UseMode, LookMode, PressureMode, ReleaseMode, EnterMode,
			ExitMode, UnlockMode, TimerMode, OpenMode, CloseMode, ScheduledMode, 0, 0, 0 };

		/// <summary>Where trap_init reads the table in OBJECTS.DAT.</summary>
		public const int ModeTableOffset = 0xD92;

		public const int ModeTableLength = 16;

		/// <summary>The mode of a trigger item (Triggers[id &amp; ID_INCLASS]).</summary>
		public static int ModeOf(int piTriggerId)
		{
			return miModes[piTriggerId & 0xF];
		}

		/// <summary>Minor class 2 and 3 of major class 6 (0x1A0-0x1BF): the second game's triggers; the first game's
		/// UseTrigger knows minor 2 alone.</summary>
		public static bool IsTrigger(int piObjectId)
		{
			return UWObjectIds.Major(piObjectId) == UWObjectMechanics.TrapMajorClass && (UWObjectIds.Minor(piObjectId) & 2) != 0;
		}

		/// <summary>A trap (minor 0 and 1 of major class 6, 0x180-0x19F).</summary>
		public static bool IsTrap(int piObjectId)
		{
			return UWObjectIds.Major(piObjectId) == UWObjectMechanics.TrapMajorClass && (UWObjectIds.Minor(piObjectId) & 2) == 0;
		}

		// ------------------------------------------------- Who sets it off

		/// <summary>Flags bit 0 (word 0 bit 0x200, ID_FLAG9): a thing - or nobody - sets it off.</summary>
		public const int ThingSetsOffFlag = 1;

		/// <summary>
		/// UseTrigger's setter test: the player needs id bit 11 (flags bit 2), a creature the enchanted bit 12 (flags
		/// bit 3), anything else - a thing in flight, nobody - bit 9 (flags bit 0). The first game lets a thing set off
		/// any trigger but an enchanted one without bit 11 (UWTrapRules.MaySetOff).
		/// </summary>
		public static bool MaySetOff(UWObject pOTrigger, UWTrapRules.Setter peSetter)
		{
			if (pOTrigger == null)
				return false;

			switch (peSetter)
			{
				case UWTrapRules.Setter.Player:
					return (pOTrigger.Flags & UWTrapRules.PlayerSetsOffFlag) != 0;
				case UWTrapRules.Setter.Creature:
					return (pOTrigger.Flags & UWTrapRules.OthersSetOffFlag) != 0;
				default:
					return (pOTrigger.Flags & ThingSetsOffFlag) != 0;
			}
		}

		/// <summary>Who set the firing off, by the host's value.</summary>
		private static UWTrapRules.Setter fSetter(IUWTrapHost pIHost)
		{
			return pIHost.Firing.Opener;
		}

		// ------------------------------------------------- UseTrigger

		/// <summary>Emergency brake against a chain that runs in a circle.</summary>
		private const int MaxDepth = 16;

		/// <summary>Flags bit 1 (word 0 bit 0x400, ID_FLAG10): the trigger stays after it has run.</summary>
		public const int RepeatableFlag = UWTrapRules.RepeatableTrapFlag;

		/// <summary>A button's class (0x170-0x17F) and its "on" half (index 8 and up, CLASS_SWITCH and OBJ_INCLASS &gt; 7).</summary>
		private const int SwitchClassMask = 0x1F0;

		private const int SwitchClassBase = 0x170;

		private const int SwitchOnBit = 8;

		/// <summary>
		/// UseTrigger (TRIGGER.C:86): a trigger of the action's mode runs its trap at its target square (quality, owner),
		/// with the host's setter as the one who set it off and pOStart as the object acted on. Mode -1 skips the switch,
		/// mode and setter tests (a trigger reached along a chain). A trigger without id bit 10 is used up: its trap goes
		/// with its whole chain and every trigger aiming at it (UWTrapChainRemoval). piSearchSkill is the player's Search
		/// for a look trigger with a z above 0. True when the trap ran.
		/// </summary>
		public static bool UseTrigger(UWObject pOStart, UWObject pOTrigger, int piMode, IUWTrapHost pIHost, int piSearchSkill = 0)
		{
			return fUseTrigger(pOStart, pOTrigger, piMode, pIHost, piSearchSkill, 0);
		}

		private static bool fUseTrigger(UWObject pOStart, UWObject pOTrigger, int piMode, IUWTrapHost pIHost, int piSearchSkill, int piDepth)
		{
			UWLevel lOLevel = pIHost != null && pIHost.World != null ? pIHost.World.Level : null;

			if (pOTrigger == null || lOLevel == null || !IsTrigger(pOTrigger.ID) || piDepth > MaxDepth)
				return false;

			if (piMode >= 0)
			{
				// AN "ON" SWITCH PASSES THE ACTION TO THE TRIGGER AFTER THIS ONE, and that one again while there is a next.
				if (pOStart != null && (pOStart.ID & SwitchClassMask) == SwitchClassBase && (pOStart.ID & SwitchOnBit) != 0
					&& pOTrigger.Link != 0)
				{
					UWObject lONext = pOTrigger.GetNextObjectInLine(lOLevel.Masterlist);

					return lONext != null && fUseTrigger(pOStart, lONext, piMode, pIHost, piSearchSkill, piDepth + 1);
				}

				if (ModeOf(pOTrigger.ID) != piMode || !MaySetOff(pOTrigger, fSetter(pIHost)))
					return false;

				if (piMode == LookMode && fSetter(pIHost) == UWTrapRules.Setter.Player && pOTrigger.ZPos > 0
					&& !UWSkillCheck.IsSuccess(UWSkillCheck.Check(piSearchSkill, pOTrigger.ZPos)))
					return false;
			}

			UWObject lOTrap = UWObjectMechanics.GetLinkedObject(pOTrigger, lOLevel.Masterlist);

			if (lOTrap == null)
				return false;

			int liLevelBefore = UWTrapRules.LevelIndexOf(pIHost);

			// The log names the chain's first trigger and who set it off (UWTrapLog.BeginChain); a nested call keeps it.
			UWTrapLog.BeginChain(pOTrigger, pIHost);

			try
			{
				fSetOffTrap(lOTrap, pOTrigger, pOStart, pIHost, piDepth + 1);
			}
			finally
			{
				UWTrapLog.EndChain();
			}

			// It went off (UseTrigger's result past its tests), whatever the kind did.
			bool lbRan = true;

			// Used up (delete_trap on the target square) - not after a level change, whose tiles are another level's.
			if ((pOTrigger.Flags & RepeatableFlag) == 0 && pOTrigger.Quantity > 0 && UWTrapRules.LevelIndexOf(pIHost) == liLevelBefore)
				UWTrapChainRemoval.AfterOneShotTrigger(pOTrigger, lOTrap, pIHost.World.Level, true);

			return lbRan;
		}

		// ------------------------------------------------- SetOffTrap and UseTrap's chain

		/// <summary>
		/// SetOffTrap and UseTrap (TRIGGER.C:135, 279): the trap's kind from the game's table, then the else branch of a
		/// failed condition or the chain through the link. pOTrigger carries the target square for every trap of the
		/// chain until a trigger in it brings its own.
		/// </summary>
		private static bool fSetOffTrap(UWObject pOTrap, UWObject pOTrigger, UWObject pOStart, IUWTrapHost pIHost, int piDepth)
		{
			if (pOTrap == null || piDepth > MaxDepth)
				return false;

			int liLevelBefore = UWTrapRules.LevelIndexOf(pIHost);

			UWTrapLog.Detail = null;

			// A condition is decided before the kind's log line (the skill trap rolls once); none of them changes anything.
			bool lbElse = fTakesElseBranch(pOTrap, pOTrigger, pIHost);
			bool lbHandled = pIHost.World.Session.Game.TrapKinds.Run(pOTrap, pOTrigger, pOStart, pIHost);

			UWTrapLog.Record(pOTrap, pOTrigger, pIHost.World.Data, lbHandled);

			if (UWTrapRules.LevelIndexOf(pIHost) != liLevelBefore)
				return lbHandled;

			List<UWObject> lOMaster = pIHost.World.Level.Masterlist;

			if (lbElse)
			{
				fRunElseChain(pOTrap, pOTrigger, pOStart, pIHost, piDepth);

				return lbHandled;
			}

			if (EndsChain(pOTrap))
				return lbHandled;

			UWObject lONext = pOTrap.Quantity > 0 ? UWObjectMechanics.GetLinkedObject(pOTrap, lOMaster) : null;

			if (lONext == null || UWObjectIds.Major(lONext.ID) != UWObjectMechanics.TrapMajorClass)
				return lbHandled;

			if (IsTrigger(lONext.ID))
				fUseTrigger(pOStart, lONext, ChainMode, pIHost, 0, piDepth + 1);
			else
				fSetOffTrap(lONext, pOTrigger, pOStart, pIHost, piDepth + 1);

			return lbHandled;
		}

		/// <summary>
		/// RUN_ELSE_CHAIN: the object after the one the trap links to - a trigger runs with mode -1, a trap at the same
		/// square. The original hands any object there to UseTrap, which would read a non-trap's class bits as a trap
		/// kind; no shipped chain does that, so ours runs a trap or a trigger only.
		/// </summary>
		private static void fRunElseChain(UWObject pOTrap, UWObject pOTrigger, UWObject pOStart, IUWTrapHost pIHost, int piDepth)
		{
			List<UWObject> lOMaster = pIHost.World.Level.Masterlist;
			UWObject lOLinked = UWObjectMechanics.GetLinkedObject(pOTrap, lOMaster);

			if (lOLinked == null || lOLinked.Link == 0)
				return;

			UWObject lOElse = lOLinked.GetNextObjectInLine(lOMaster);

			if (lOElse == null || UWObjectIds.Major(lOElse.ID) != UWObjectMechanics.TrapMajorClass)
				return;

			if (IsTrigger(lOElse.ID))
				fUseTrigger(pOStart, lOElse, ChainMode, pIHost, 0, piDepth + 1);
			else
				fSetOffTrap(lOElse, pOTrigger, pOStart, pIHost, piDepth + 1);
		}

		/// <summary>The kinds whose case clears continue_chain: create object, delete object, ward; the door trap when
		/// its owner is set (a lock is swapped, TRIGGER.C:594 - the original asks for a door on the square as well; no
		/// shipped door trap with an owner aims at a square without one, so ours asks for the owner alone).</summary>
		public static bool EndsChain(UWObject pOTrap)
		{
			switch (pOTrap.ID)
			{
				case UWObjectMechanics.CreateObjectTrapId:
				case UWObjectMechanics.DeleteObjectTrapId:
				case UWObjectMechanics.WardTrapId:
					return true;
				case UWObjectMechanics.DoorTrapId:
					return pOTrap.Owner != 0;
				default:
					return false;
			}
		}

		/// <summary>Whether a condition trap fails and takes its else branch: the variable check when the values
		/// differ and the trap links to something. The skill, proximity and inventory checks come with their kinds.</summary>
		private static bool fTakesElseBranch(UWObject pOTrap, UWObject pOTrigger, IUWTrapHost pIHost)
		{
			if (pOTrap.Quantity <= 0)
				return false;

			switch (pOTrap.ID)
			{
				case UWObjectMechanics.CheckVariableTrapId:
					return !CheckVariableHolds(pOTrap, fRecord(pIHost));
				case UWObjectMechanics.InventoryTrapId:
					return InventoryTrips(pOTrap, pIHost);
				case SkillTrapId:
					return fSkillPasses(pOTrap, pIHost);
				case ProximityTrapId:
					return !fWithinProximity(pOTrap, pOTrigger, pIHost);
				default:
					return false;
			}
		}

		// ------------------------------------------------- The numbered variables

		/// <summary>The second game's player record the variables live in.</summary>
		private static UWPlayerFile fRecord(IUWTrapHost pIHost)
		{
			return pIHost != null && pIHost.World != null && pIHost.World.Session != null ? pIHost.World.Session.PlayerFileUw2 : null;
		}

		/// <summary>do_math_op (TRIGGER.C:151): 0 add, 1 subtract, 2 set, 3 and, 4 or, 5 xor, 6 shift left, 7 count up
		/// while equal (value + 1 when it equals right, else 0); another op leaves the value.</summary>
		public static int DoMathOp(int piValue, int piOp, int piRight)
		{
			switch (piOp)
			{
				case 0: return piValue + piRight;
				case 1: return piValue - piRight;
				case 2: return piRight;
				case 3: return piValue & piRight;
				case 4: return piValue | piRight;
				case 5: return piValue ^ piRight;
				case 6: return piValue << piRight;
				case 7: return piValue == piRight ? piValue + 1 : 0;
				default: return piValue;
			}
		}

		/// <summary>The signs of do_math_op's eight ops, for the trap log.</summary>
		private static readonly string[] msOps = { "+", "-", ":=", "&", "|", "^", "<<", "count" };

		/// <summary>The first numbered variable past the game variables: the quest bits (0x100-0x17F), the quest bytes
		/// (0x180-0x18F), the X clocks (0x190-0x19F).</summary>
		public const int FirstQuestBit = 0x100;

		public const int FirstQuestByte = 0x180;

		public const int FirstXClock = 0x190;

		/// <summary>X clock 15, XC_CHANGED: counted up by a set-variable trap with a fine y.</summary>
		public const int ChangedXClock = 15;

		/// <summary>
		/// set_numbered_variable (TRIGGER.C:169): a game variable keeps a byte of the result; a quest bit is toggled by op
		/// 5, left by op 1 and set to right &gt; 0 by the others; a quest byte and an X clock keep a byte of the result.
		/// </summary>
		public static void SetNumbered(UWPlayerFile pOFile, int piIndex, int piOp, int piRight)
		{
			if (pOFile == null || piIndex < 0)
				return;

			if (piIndex >= FirstQuestBit && piIndex < FirstQuestByte)
			{
				int liQuest = piIndex - FirstQuestBit;

				if (piOp == 5)
					UWPlayerRecordUw2.SetQuest(pOFile, liQuest, UWPlayerRecordUw2.GetQuest(pOFile, liQuest) ^ 1);
				else if (piOp != 1)
					UWPlayerRecordUw2.SetQuest(pOFile, liQuest, piRight > 0 ? 1 : 0);

				return;
			}

			UWPlayerRecordUw2.SetNumbered(pOFile, piIndex, DoMathOp(UWPlayerRecordUw2.GetNumbered(pOFile, piIndex), piOp, piRight));
		}

		/// <summary>The set-variable trap's variable: z + fine x * 128.</summary>
		public static int SetVariableIndex(UWObject pOTrap)
		{
			return pOTrap.ZPos + (pOTrap.XPos << 7);
		}

		/// <summary>Its right side: quality * 64 + owner.</summary>
		public static int SetVariableValue(UWObject pOTrap)
		{
			return (pOTrap.Quality << 6) + pOTrap.Owner;
		}

		/// <summary>TRAP_SET_VARIABLE: the variable, op the heading; a fine y counts XC_CHANGED up.</summary>
		private static bool fFireSetVariable(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWPlayerFile lOFile = fRecord(pIHost);

			if (lOFile == null)
				return false;

			int liIndex = SetVariableIndex(pOTrap);
			int liOld = UWPlayerRecordUw2.GetNumbered(lOFile, liIndex);

			SetNumbered(lOFile, liIndex, pOTrap.Heading, SetVariableValue(pOTrap));

			if (pOTrap.YPos != 0)
				UWPlayerRecordUw2.SetNumbered(lOFile, FirstXClock + ChangedXClock,
					UWPlayerRecordUw2.GetNumbered(lOFile, FirstXClock + ChangedXClock) + 1);

			UWTrapLog.Detail = string.Format("-> var 0x{0:X}: {1} {2} {3} = {4}{5}", liIndex, liOld, msOps[pOTrap.Heading & 7],
				SetVariableValue(pOTrap), UWPlayerRecordUw2.GetNumbered(lOFile, liIndex), pOTrap.YPos != 0 ? ", changed clock up" : string.Empty);

			return true;
		}

		/// <summary>The check-variable trap's first variable: z, with fine x bits 0-1 as bits 7-8.</summary>
		public static int CheckVariableFirst(UWObject pOTrap)
		{
			return pOTrap.ZPos | ((pOTrap.XPos & 3) << 7);
		}

		/// <summary>What it compares with: ((quality * 32 | owner) * 8) | fine y.</summary>
		public static int CheckVariableExpected(UWObject pOTrap)
		{
			return (((pOTrap.Quality << 5) | pOTrap.Owner) << 3) | pOTrap.YPos;
		}

		/// <summary>The value of the checked range: heading + 1 variables from the first, summed when fine x bit 2 is set,
		/// else packed three bits each, the first on top.</summary>
		public static int CheckVariableValue(UWObject pOTrap, UWPlayerFile pOFile)
		{
			int liFirst = CheckVariableFirst(pOTrap);
			int liValue = 0;

			for (int liAt = liFirst; liAt <= liFirst + pOTrap.Heading; liAt++)
			{
				int liVariable = UWPlayerRecordUw2.GetNumbered(pOFile, liAt);

				liValue = (pOTrap.XPos >> 2) != 0 ? liValue + liVariable : (liValue << 3) | (liVariable & 7);
			}

			return liValue;
		}

		/// <summary>Whether the check holds (the chain goes on through the link); otherwise the else branch.</summary>
		public static bool CheckVariableHolds(UWObject pOTrap, UWPlayerFile pOFile)
		{
			return pOFile == null || CheckVariableValue(pOTrap, pOFile) == CheckVariableExpected(pOTrap);
		}

		private static bool fFireCheckVariable(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWPlayerFile lOFile = fRecord(pIHost);

			if (lOFile == null)
				return false;

			bool lbHolds = CheckVariableHolds(pOTrap, lOFile);

			UWTrapLog.Detail = string.Format("-> var 0x{0:X}..0x{1:X} = {2}, expected {3} -> {4}", CheckVariableFirst(pOTrap),
				CheckVariableFirst(pOTrap) + pOTrap.Heading, CheckVariableValue(pOTrap, lOFile), CheckVariableExpected(pOTrap),
				lbHolds ? "on" : pOTrap.Quantity > 0 ? "else branch" : "stops");

			return true;
		}

		// ------------------------------------------------- The kinds

		/// <summary>The player record's word 0x62 bit 5 (b62_5): a text trap with owner bit 5 speaks only while it is set;
		/// nothing sets it and no shipped text trap has the bit.</summary>
		private const int TextFlagOffset = UWPlayerRecordUw2.AutomapByteOffset;

		private const int TextFlagBit = 0x20;

		/// <summary>The trap text's number in block 9: quality * 32 + (owner &amp; 31), ours one higher.</summary>
		public static int TextIndex(UWObject pOTrap)
		{
			return (((pOTrap.Quality & 0x3F) << 5) | (pOTrap.Owner & 0x1F)) + 1;
		}

		/// <summary>TRAP_TEXT_STRING: the string, printed when owner bit 5 is clear or b62_5 is set.</summary>
		private static bool fFireText(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWPlayerFile lOFile = fRecord(pIHost);
			bool lbAllowed = (pOTrap.Owner >> 5) == 0 || (lOFile != null && (lOFile.Record[TextFlagOffset] & TextFlagBit) != 0);

			UWTrapLog.Detail = string.Format("-> text {0}{1}", TextIndex(pOTrap), lbAllowed ? string.Empty : " (owner bit 5, silent)");

			if (!lbAllowed || !pIHost.World.Presentation.ShowsMessages || pIHost.World.Data == null)
				return true;

			try
			{
				string lsText = pIHost.World.Data.GetLevelMessage(TextIndex(pOTrap));

				if (!string.IsNullOrWhiteSpace(lsText))
					pIHost.World.Presentation.AddMessage(lsText.TrimEnd('\r', '\n'));
			}
			catch
			{
				// A missing entry is not an error, as for the first game's text trap.
			}

			return true;
		}

		/// <summary>
		/// TRAP_TELEPORT: only the player moves (do_teleport records his move for the main loop; anyone else gets
		/// trap_teleport_data -1 and stays). The target, the fall from the ceiling, the heading and the map scrap are
		/// UWTeleportUw2's; the walk carries it out (IUWWorld.TeleportSecondGame).
		/// </summary>
		private static bool fFireTeleport(UWObject pOTrap, IUWTrapHost pIHost)
		{
			if (fSetter(pIHost) != UWTrapRules.Setter.Player)
			{
				UWTrapLog.Detail = "-> not the player: nobody moved";

				return true;
			}

			UWTeleportUw2.Target lOTarget = UWTeleportUw2.Decode(pOTrap);

			UWTrapLog.Detail = string.Format("-> level {0}, tile {1}/{2}{3}{4}", lOTarget.Level == 0 ? "this" : lOTarget.Level.ToString(),
				lOTarget.TileX, lOTarget.TileY, lOTarget.Falls ? ", from above" : string.Empty, lOTarget.AutomapOff ? ", map off" : string.Empty);

			pIHost.World.TeleportSecondGame(lOTarget);

			return true;
		}

		/// <summary>The second game's trap kinds, sealed - a new table each call, for its profile. Kinds without an
		/// entry are written to the trap log as not built, and the chain goes on past them.</summary>
		public static UWTrapTable CreateTrapTable()
		{
			UWTrapTable lOTable = new UWTrapTable()

				// The same code in both games (whack_thing, WORLDEV.C); the 7-in-10 roll before it only picks an
				// argument whack_thing does not read.
				.Register(UWObjectMechanics.DamageTrapId, "damage",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => UWTrapRules.FireDamageTrap(pOTrap, pIHost))
				.Register(UWTeleportUw2.TrapId, "teleport",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireTeleport(pOTrap, pIHost))

				// trap_fire: the first game's launch; the second game plays sound 0xA at the missile (the host).
				.Register(UWObjectMechanics.ArrowTrapId, "arrow",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return pIHost.FireArrowTrap(pOTrap, liX, liY);
					})
				.Register(UWObjectMechanics.ChangeTerrainTrapId, "change terrain",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fFireChangeTerrain(pOTrap, liX, liY, pIHost);
					})
				.Register(UWObjectMechanics.SpellTrapId, "spell",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fFireSpell(pOTrap, liX, liY, pIHost);
					})
				.Register(UWObjectMechanics.CreateObjectTrapId, "create object",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fFireCreateObject(pOTrap, liX, liY, pIHost);
					})
				.Register(UWObjectMechanics.DoorTrapId, "door",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fFireDoor(pOTrap, liX, liY, pIHost);
					})
				.Register(UWObjectMechanics.DeleteObjectTrapId, "delete object",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireDeleteObject(pOTrap, pIHost))
				.Register(UWObjectMechanics.InventoryTrapId, "inventory",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						UWTrapLog.Detail = string.Format("-> item 0x{0:X3}{1}{2}: {3}", UWObjectMechanics.GetInventoryTrapItemId(pOTrap),
							pOTrap.XPos != 0 ? ", worn" : string.Empty, pOTrap.ZPos > 0 ? ", at least " + pOTrap.ZPos : string.Empty,
							InventoryTrips(pOTrap, pIHost) ? "missing, else branch" : "there, on");

						return true;
					})
				.Register(SpecialEffectTrapId, "special effect",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireSpecialEffect(pOTrap, pIHost))
				.Register(SkillTrapId, "skill",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => true)
				.Register(ExperienceTrapId, "experience",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireExperience(pOTrap, pIHost))
				.Register(JumpTrapId, "jump",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireJump(pOTrap, pIHost))
				.Register(ChangeFromTrapId, "change from",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fFireChangeFrom(pOTrap, liX, liY, pIHost);
					})

				// UseTrap has no case for the change-to trap: it is the change-from trap's data, and the chain goes on.
				.Register(ChangeToTrapId, "change to",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => true)
				.Register(OscillatorTrapId, "oscillator",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fFireOscillator(pOTrap, liX, liY, pIHost);
					})
				.Register(ProximityTrapId, "proximity",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => true)
				.Register(PitTrapId, "pit",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fFirePit(pOTrap, liX, liY, pIHost);
					})
				.Register(BridgeTrapId, "bridge",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fFireBridge(pOTrap, liX, liY, pIHost);
					})
				.Register(UWObjectMechanics.DoTrapId, "hack",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => pIHost.World.Session.Game.DoActions.Run(pOTrap, pOTrigger, pOSwitch, pIHost))
				.Register(UWObjectMechanics.SetVariableTrapId, "set variable",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireSetVariable(pOTrap, pIHost))
				.Register(UWObjectMechanics.CheckVariableTrapId, "check variable",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireCheckVariable(pOTrap, pIHost))

				// UseTrap has no case for the null trap: the chain simply goes on.
				.Register(NullTrapId, "null",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						UWTrapLog.Detail = "-> nothing, the chain goes on";

						return true;
					})
				.Register(UWObjectMechanics.TextStringTrapId, "text string",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireText(pOTrap, pIHost));

			lOTable.Seal();

			return lOTable;
		}

		// ------------------------------------------------- Change terrain (part H1b)

		/// <summary>The bounds of the second game's change_terrain: a height up to 0xE (0xF only with adjust 4, which no
		/// trap passes), a floor below 0xF, a wall below 0x3F, a type below 10.</summary>
		public const int MaxTerrainHeight = 0xE;

		public const int FloorLimit = 0xF;

		public const int WallLimit = 0x3F;

		public const int TypeLimit = 10;

		/// <summary>
		/// TRAP_CHANGE_TERRAIN (TRIGGER.C:324) and change_terrain (uw2 WORLDEV.C, ChangeTile_ovr110_F37): over the
		/// target square and fine x, fine y squares on, the wall (owner), the floor (quality / 2), the height (z / 8)
		/// and the type (heading * 2 + quality's bit 0, 15 read as 10) within their bounds. Unlike the first game:
		///   - a floor of water makes every later square of the change remove its lying things as they move, a floor of
		///     lava one time in two, a solid type as well (the flag is the function's, it does not reset per square);
		///   - what stands over the edge from a neighbour moves with the floor too (filter: the thing's footprint by its
		///     COMOBJ radius reaches over the edge at the right height); doors and traps never move;
		///   - a thing lifted above the ceiling (height * 8 + its COMOBJ height over 0x7F) is struck for 0xFF.
		/// The creatures go with the floor of the changed square (IUWTrapHost.MoveCreaturesWithFloor, as for the first
		/// game: ours keeps them in their records); a sinking creature falls in the original, ours goes down at once.
		/// </summary>
		private static bool fFireChangeTerrain(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			int liType = (pOTrap.Heading << 1) + (pOTrap.Quality & 1);

			if (liType == 15)
				liType = 10;

			bool lbChanged = ChangeTerrain(piTileX, piTileY, pOTrap.Owner, pOTrap.Quality >> 1, pOTrap.ZPos >> 3, liType,
				pOTrap.XPos, pOTrap.YPos, pIHost);

			UWTrapLog.Detail = string.Format("-> {0}/{1}+{2}/{3}: height {4}, floor {5}, wall {6}, type {7}", piTileX, piTileY,
				pOTrap.XPos, pOTrap.YPos, pOTrap.ZPos >> 3, pOTrap.Quality >> 1, pOTrap.Owner, liType);

			return lbChanged;
		}

		/// <summary>change_terrain (see fFireChangeTerrain). piAdjust: 0 the trap's form; 1 and 3 raise or lower each square
		/// by one instead of setting piHeight; 4 also allows height 0xF (the oscillator and the pit).</summary>
		public static bool ChangeTerrain(int piX, int piY, int piWall, int piFloor, int piHeight, int piType, int piDx, int piDy,
			IUWTrapHost pIHost, int piAdjust = 0)
		{
			UWLevel lOLevel = pIHost.World.Level;

			if (lOLevel == null || lOLevel.TileData == null)
				return false;

			bool lbSolid = false;
			bool lbChanged = false;
			UWTerrain lOTerrain = pIHost.World.Data != null ? pIHost.World.Data.Terrain : null;

			for (int liX = piX; liX <= piX + piDx; liX++)
			{
				for (int liY = piY; liY <= piY + piDy; liY++)
				{
					UWTile lOTile = lOLevel.GetTile(liX, liY);

					if (lOTile == null)
						continue;

					lbChanged = true;

					int liOld = lOTile.FloorHeight >> 4;
					bool lbWasSolid = lOTile.TileType == UWTile.TileTypeEnum.solid;

					if (piAdjust == 1 || piAdjust == 3)
						piHeight = liOld + 2 - piAdjust;

					if (piHeight >= 0 && piHeight <= MaxTerrainHeight)
						lOTile.FloorHeight = (ushort)(piHeight << 4);

					if (piHeight == 0xF && piAdjust == 4)
						lOTile.FloorHeight = 0xF << 4;

					if (piFloor < FloorLimit && lOLevel.TextureInfos != null && piFloor + lOTile.FloorSlotBase < lOLevel.TextureInfos.Length)
					{
						lOTile.SetFloorTexture(piFloor, lOLevel.TextureInfos);

						UWTerrain.TerrainType leTerrain = lOTerrain != null ? lOTerrain.GetFloorTerrain(lOTile.TextureFloor) : UWTerrain.TerrainType.Normal;

						if (leTerrain == UWTerrain.TerrainType.Lava)
							lbSolid = (UWRandom.Next(0x8000) & 1) != 0;
						else if (leTerrain == UWTerrain.TerrainType.Water)
							lbSolid = true;
					}

					if (piWall < WallLimit && lOLevel.TextureInfos != null && piWall < lOLevel.TextureInfos.Length)
						lOTile.SetWallTexture(piWall, lOLevel.TextureInfos);

					if (piType < TypeLimit)
					{
						lOTile.TileType = (UWTile.TileTypeEnum)piType;
						lOTile.Slope = (byte)(piType >= (int)UWTile.TileTypeEnum.slope_n ? 16 : 0);

						if (lOTile.TileType == UWTile.TileTypeEnum.solid)
							lbSolid = true;
					}

					if (lbWasSolid && lOTile.TileType != UWTile.TileTypeEnum.solid)
						pIHost.SpawnPendingObjectsInTile(liX, liY);

					int liNew = lOTile.FloorHeight >> 4;

					if (liNew == liOld)
						continue;

					pIHost.MoveCreaturesWithFloor(liX, liY, liOld << 3, liNew << 3);

					// The player, wherever filter lets him through (on the square, or reaching over from a neighbour).
					fMovePlayerWithFloor(liX, liY, liOld, liNew, piAdjust, pIHost);

					// The square and, at the change's edge, the neighbours beyond it (xmin..xmax, ymin..ymax).
					int liFromX = liX == piX && liX > 1 ? liX - 1 : liX;
					int liToX = liX == piX + piDx && liX < 0x3F ? liX + 1 : liX;
					int liFromY = liY == piY && liY > 1 ? liY - 1 : liY;
					int liToY = liY == piY + piDy && liY < 0x3F ? liY + 1 : liY;

					for (int liNearX = liFromX; liNearX <= liToX; liNearX++)
					{
						for (int liNearY = liFromY; liNearY <= liToY; liNearY++)
							fMoveWithFloor(lOLevel.GetTile(liNearX, liNearY), liX, liY, liOld, liNew, lbSolid, pIHost);
					}
				}
			}

			if (lbChanged)
				pIHost.RebuildGeometry();

			return lbChanged;
		}

		/// <summary>raise_up and put_down over one square's list, for the things filter lets through.</summary>
		private static void fMoveWithFloor(UWTile pONear, int piChangedX, int piChangedY, int piOld, int piNew, bool pbSolid,
			IUWTrapHost pIHost)
		{
			if (pONear == null || pONear.ObjectsInTile == null)
				return;

			UWLevel lOLevel = pIHost.World.Level;
			UWCommonObjectProperties lOCommon = pIHost.World.Data != null ? pIHost.World.Data.CommonObjectProperties : null;
			int liNewZ = piNew << 3;
			int liOldZ = piOld << 3;

			foreach (UWObject lOObject in new List<UWObject>(pONear.ObjectsInTile))
			{
				if (lOObject == null || lOObject is UWNpc || !TerrainMoves(lOObject, pONear.X, pONear.Z, piChangedX, piChangedY, piOld, piNew, lOCommon))
					continue;

				int liIndex = lOLevel.Masterlist.IndexOf(lOObject);
				bool lbStatic = liIndex >= UWLevArkLayout.MobileCount;

				if (piNew > piOld)
				{
					// Lifted above the ceiling: struck for 0xFF (damage_item, type 0).
					if (lOCommon != null && lOCommon.TryGet(lOObject.ID, out UWCommonObjectProperties.Entry lOEntry) && liNewZ + lOEntry.Height > 0x7F)
						pIHost.World.Damage(UWDamageTarget.ObjectOf(lOObject), UWBlow.Trap(0xFF, 0));

					if (lOObject.ZPos >= liNewZ)
						continue;
				}
				else if (lOObject.ZPos != liOldZ)
					continue;

				int liWas = lOObject.ZPos;

				lOObject.ZPos = (ushort)liNewZ;
				pIHost.OnObjectHeightChanged(lOObject, liWas);

				// Obj_Punt(..., 0): a lying thing on a square turned to water, lava or rock goes, unless it is tenacious
				// against range 10 (Obj_Elem_Fate, UWObjectLimitRules.CullingTest).
				if (lbStatic && pbSolid && UWObjectLimitRules.CullingTest(lOObject, PuntRange, lOCommon, lOLevel.Masterlist,
						() => UWRandom.Next(0x8000)))
					pIHost.World.Remove(lOObject);
			}
		}

		/// <summary>Obj_Punt's range for Obj_Elem_Fate.</summary>
		private const int PuntRange = 10;

		/// <summary>filter (uw2 WORLDEV.C): whether a thing on square (piNearX, piNearY) moves with the floor of the changed
		/// square - anything on it but traps and doors, and from a neighbour a thing at the right height whose footprint
		/// reaches over the edge.</summary>
		public static bool TerrainMoves(UWObject pOObject, int piNearX, int piNearY, int piChangedX, int piChangedY, int piOld, int piNew,
			UWCommonObjectProperties pOCommon)
		{
			int liMajor = UWObjectIds.Major(pOObject.ID);

			if (liMajor == UWObjectMechanics.TrapMajorClass || liMajor == RectMajorClass)
				return false;

			int liRadius = pOCommon != null && pOCommon.TryGet(pOObject.ID, out UWCommonObjectProperties.Entry lOEntry) ? lOEntry.Radius : 0;

			return TerrainMovesAt(piNearX, piNearY, piChangedX, piChangedY, piOld, piNew, pOObject.XPos, pOObject.YPos, pOObject.ZPos, liRadius);
		}

		/// <summary>filter's test for a thing at fine x, y (0 to 7) and zpos with its radius on square (piNearX, piNearY):
		/// anything on the changed square, and from a neighbour a thing at the right height reaching over the edge.</summary>
		public static bool TerrainMovesAt(int piNearX, int piNearY, int piChangedX, int piChangedY, int piOld, int piNew,
			int piFineX, int piFineY, int piZPos, int piRadius)
		{
			if (piNearX == piChangedX && piNearY == piChangedY)
				return true;

			if (piOld > piNew ? piZPos != piOld << 3 : piZPos < piOld << 3 || piZPos >= piNew << 3)
				return false;

			int liX = ((piNearX - piChangedX) << 3) + piFineX;
			int liY = ((piNearY - piChangedY) << 3) + piFineY;

			bool lbX = (liX < 0 && liX + piRadius >= 0) || (liX > 7 && liX - piRadius <= 7) || piNearX == piChangedX;
			bool lbY = (liY < 0 && liY + piRadius >= 0) || (liY > 7 && liY - piRadius <= 7) || piNearY == piChangedY;

			return lbX && lbY;
		}

		/// <summary>
		/// raise_up and put_down for ThePlayer (WORLDEV.C): the player stands in no square's list here, so the terrain
		/// change asks him apart - where his feet are, filter's test with the adventurer's radius, then the motion follows
		/// the floor (IUWPlayerAvatar.FollowFloor; set onto a step of one, adjust 1 or 3, else put in the air).
		/// </summary>
		private static void fMovePlayerWithFloor(int piChangedX, int piChangedY, int piOld, int piNew, int piAdjust, IUWTrapHost pIHost)
		{
			IUWPlayerAvatar lOPlayer = pIHost.World != null ? pIHost.World.Player : null;

			if (lOPlayer == null || !lOPlayer.HasBody)
				return;

			UWSpot lOFeet = lOPlayer.FeetSpot;

			if (!lOFeet.IsOnMap || System.Math.Abs(lOFeet.Tile.X - piChangedX) > 1 || System.Math.Abs(lOFeet.Tile.Y - piChangedY) > 1)
				return;

			UWCommonObjectProperties lOCommon = pIHost.World.Data != null ? pIHost.World.Data.CommonObjectProperties : null;
			int liRadius = lOCommon != null && lOCommon.TryGet(UWObjectMechanics.AdventurerObjectId, out UWCommonObjectProperties.Entry lOEntry)
				? lOEntry.Radius : 0;

			if (!TerrainMovesAt(lOFeet.Tile.X, lOFeet.Tile.Y, piChangedX, piChangedY, piOld, piNew, lOFeet.FineX, lOFeet.FineY, lOFeet.ZPos, liRadius))
				return;

			lOPlayer.FollowFloor(piOld << 3, piNew << 3, piAdjust == 1 || piAdjust == 3);
		}

		/// <summary>MAJOR_RECT: the doors and the other things of class 5, which a floor change never moves.</summary>
		private const int RectMajorClass = 5;

		// ------------------------------------------------- Create object, door, delete, inventory (part H1b)

		/// <summary>The adventurer, a create trap's template for a random castle monster (ITEM_ADVENTURER).</summary>
		public const int AdventurerTemplateId = 0x7F;

		/// <summary>The Tombs of Praecor Loth (world 6 counted from 0, levels 49-56) create nothing once quest 7 is set
		/// (quests[1] bit 3; Praecor Loth's death sets it, WORLDEV.C:805).</summary>
		public const int TombsWorld = 6;

		public const int TombsClearedQuest = 7;

		private const int LevelsPerWorld = 8;

		/// <summary>
		/// TRAP_CREATE_OBJECT (TRIGGER.C:523): nothing when rand() * 0x3F / 0x8000 is below the quality
		/// (UWTrapRules.CreateObjectRollFails), when the trap has the quantity bit, when no template is linked, when a
		/// temporary creature is about the template (dont_create_wandering_monster_here - the first game's crowding
		/// area), or in world 6 once quest 7 is set. THE ADVENTURER TEMPLATE (0x7F) becomes a random castle monster for
		/// the copy (RollCastleMonster): the template takes its kind and a new creature's set-up (init_this_critter,
		/// UWCritterRecord.InitialiseAsNew, home 32/32) and the loner bit, is copied, and is an adventurer again; the copy
		/// is hostile and temporary. The copy - the record and the first thing it holds (UWObject.Clone) - is put down
		/// near the target square at the template's spot (put_at with range 4). The chain ends here either way.
		/// </summary>
		private static bool fFireCreateObject(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			if (!pIHost.AlwaysFireCreateObjectTraps && UWTrapRules.CreateObjectRollFails(UWRandom.Next(0x8000), pOTrap.Quality))
			{
				UWTrapLog.Detail = "-> the roll: nothing";

				return true;
			}

			if (pOTrap.HasQuantity)
			{
				UWTrapLog.Detail = "-> quantity bit: nothing";

				return true;
			}

			UWLevel lOLevel = pIHost.World.Level;
			UWObject lOTemplate = UWObjectMechanics.GetLinkedObject(pOTrap, lOLevel.Masterlist);
			UWPlayerFile lOFile = fRecord(pIHost);

			if (lOTemplate == null)
				return false;

			if (UWObjectIds.IsCreature(lOTemplate.ID) && pIHost.World.FindCreatures(UWTileArea.Around(new UWTilePos(lOTemplate.TileX, lOTemplate.TileY),
					pIHost.World.Session.Rules.Respawn.CrowdTilesBack, pIHost.World.Session.Rules.Respawn.CrowdTilesForward, UWCreatureTile.Body),
					UWCreatureFilter.Spawned, new List<UWCreatureRef>()) > 0)
			{
				UWTrapLog.Detail = "-> a wandering creature is about: nothing";

				return true;
			}

			if (lOFile != null && (lOLevel.LevelNumber - 1) / LevelsPerWorld == TombsWorld && UWPlayerRecordUw2.GetQuest(lOFile, TombsClearedQuest) != 0)
			{
				UWTrapLog.Detail = "-> the Tombs are cleared: nothing";

				return true;
			}

			bool lbCastleMonster = lOTemplate.ID == AdventurerTemplateId && lOTemplate is UWNpc;

			if (lbCastleMonster)
			{
				int liKind = RollCastleMonster(lOLevel.LevelNumber, lOFile != null ? UWPlayerRecordUw2.XClock(lOFile, CastleXClock) : 0,
					lOFile != null ? UWPlayerRecordUw2.CharacterLevel(lOFile) : 1, pIHost.World.Data != null ? pIHost.World.Data.ObjectClassProperties : null);

				if (liKind < 0)
				{
					UWTrapLog.Detail = "-> no castle monster fits";

					return false;
				}

				lOTemplate.ID = (ushort)liKind;

				UWCritterRecord lOSetUp = new UWCritterRecord((UWNpc)lOTemplate);
				int liVitality = pIHost.World.Data != null && pIHost.World.Data.ObjectClassProperties.TryGetCritter(liKind, out UWObjectClassProperties.Critter lOStats)
					? lOStats.Vitality : 0;

				lOSetUp.InitialiseAsNew(liVitality, UWRandom.Next(UWCritterRecord.NewCreatureHitPointRollRange));
				lOSetUp.AttitudeLocked = true;
				((UWNpc)lOTemplate).NPC_HP = (byte)lOSetUp.HitPoints;
			}

			UWObject lOCopy = UWObject.Clone(lOTemplate, lOLevel.Masterlist);

			if (lbCastleMonster)
				lOTemplate.ID = AdventurerTemplateId;

			if (lOCopy == null)
				return false;

			if (lbCastleMonster && lOCopy is UWNpc lONpc)
			{
				UWCritterRecord lORecord = new UWCritterRecord(lONpc);

				lORecord.Attitude = UWNpc.AttitudeHostile;
				lONpc.NPCAttitude = UWNpc.AttitudeHostile;
				lONpc.NPCSpawned = true;
			}

			bool lbPlaced = pIHost.World.PutAt(lOCopy, new UWTilePos(piTileX, piTileY), UWTrapRules.CreateObjectPutAtRange);

			UWTrapLog.Detail = string.Format("-> 0x{0:X3}{1} {2}", lOCopy.ID, lbCastleMonster ? " (castle monster)" : string.Empty,
				lbPlaced ? "placed" : "found no room");

			return lbPlaced;
		}

		/// <summary>The castle's plot clock (XC_CASTLE).</summary>
		public const int CastleXClock = 1;

		/// <summary>The first creature item (FIRST_CREATURE).</summary>
		private const int FirstCreature = 0x40;

		/// <summary>
		/// The random castle monster (TRIGGER.C:538): lo = ((level within the world * 3) + the castle clock) / 9, at least
		/// 1; range = the character level + the castle clock + 1, at most 16; the kind 0x40 + (rand() % lo) * 16 +
		/// rand() % range, drawn again until eligible_castle_monster takes it (strength 0 never, else one time in its
		/// strength). -1 when none fits after many draws (the original would draw on for ever).
		/// </summary>
		public static int RollCastleMonster(int piDungeonLevel, int piCastleClock, int piCharacterLevel, UWObjectClassProperties pOProperties)
		{
			int liLow = ((((piDungeonLevel - 1) % LevelsPerWorld) + 1) * 3 + piCastleClock) / 9;
			int liRange = System.Math.Min(16, piCharacterLevel + piCastleClock + 1);

			if (liLow < 1)
				liLow = 1;

			if (pOProperties == null || liRange < 1)
				return -1;

			for (int liDraw = 0; liDraw < 4096; liDraw++)
			{
				int liKind = ((UWRandom.Next(0x8000) % liLow) << 4) + (UWRandom.Next(0x8000) % liRange) + FirstCreature;

				if (pOProperties.TryGetCritter(liKind, out UWObjectClassProperties.Critter lOStats) && lOStats.Power != 0
					&& UWRandom.Next(0x8000) % lOStats.Power == 0)
					return liKind;
			}

			return -1;
		}

		/// <summary>
		/// DoWanderingMonsters' UseTrap (TRIGGER.C:1033): a create-object trap without flags whose template is a mobile
		/// record marks the template temporary and runs at its own square (as the respawn pass hands it). False for any
		/// other trap.
		/// </summary>
		public static bool FireWanderingTrap(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost != null && pIHost.World != null ? pIHost.World.Level : null;

			if (pOTrap == null || lOLevel == null || pOTrap.ID != UWObjectMechanics.CreateObjectTrapId || (pOTrap.Flags & 0xF) != 0
				|| pOTrap.Quantity <= 0 || pOTrap.Quantity >= UWLevArkLayout.MobileCount)
				return false;

			if (UWObjectMechanics.GetLinkedObject(pOTrap, lOLevel.Masterlist) is UWNpc lOTemplate)
				lOTemplate.NPCSpawned = true;

			UWObject lOCarrier = SquareCarrier(piTileX, piTileY);

			// A chain of its own for the log: set off by the game, a test mark on the trap's square tags it (UWTrapLog).
			UWTrapLog.BeginChain(lOCarrier, pIHost, "the game");

			try
			{
				return fSetOffTrap(pOTrap, lOCarrier, null, pIHost, 0);
			}
			finally
			{
				UWTrapLog.EndChain();
			}
		}

		/// <summary>
		/// TRAP_DOOR (TRIGGER.C:594): the door on the target square opens (1), closes (2) or toggles (3) - a moving door
		/// is turned round as in the first game (UWDoorRules.DoorTrap); THEN, only when the trap's owner is set, the lock
		/// goes and a copy of the trap's linked lock takes its place. The first game swaps the lock first and always.
		/// </summary>
		private static bool fFireDoor(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			UWObject lODoorData = UWTrapRules.FindDoorInTile(piTileX, piTileY, pIHost.World.Level);
			IUWDoor lODoor = lODoorData != null ? pIHost.World.Doors.Find(lODoorData) : null;

			if (lODoor == null)
			{
				UWTrapLog.Detail = "-> no door there";

				return true;
			}

			UWDoorRules.TrapOutcome lOOutcome = UWDoorRules.DoorTrap(pOTrap.Quality, lODoor.IsHeadingClosed, lODoor.IsMoving);

			if (lOOutcome.Toggle)
				lODoor.Toggle(pIHost.Firing.Opener);

			bool lbLock = pIHost.World.Session.Game.Traps.SwapsLock(pOTrap.Owner);

			if (lbLock)
				lODoor.ReplaceLock(!pOTrap.HasQuantity ? UWObjectMechanics.GetLinkedObject(pOTrap, pIHost.World.Level.Masterlist) : null);

			UWTrapLog.Detail = string.Format("-> door {0}/{1}: {2}{3}", piTileX, piTileY, lOOutcome.Toggle ? "moves" : "stays",
				lbLock ? ", lock swapped" : string.Empty);

			return true;
		}

		/// <summary>
		/// TRAP_DELETE_OBJECT (TRIGGER.C:649): the object the trap links to, on square (quality, owner). A trap or a
		/// trigger goes as trap_obj_del has it (UWTrapChainRemoval.DeleteTrapObjectSecondGame: a trigger counts its
		/// trap's triggers down, the last takes the trap along); anything else leaves the world with what it holds.
		/// </summary>
		private static bool fFireDeleteObject(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.World.Level;
			UWObject lOVictim = UWObjectMechanics.GetLinkedObject(pOTrap, lOLevel.Masterlist);

			if (lOVictim == null)
				return false;

			UWTrapLog.Detail = string.Format("-> 0x{0:X3} on {1}/{2}", lOVictim.ID, pOTrap.Quality, pOTrap.Owner);

			if (UWObjectIds.Major(lOVictim.ID) == UWObjectMechanics.TrapMajorClass)
			{
				UWTrapChainRemoval.DeleteTrapObjectSecondGame(lOVictim, lOLevel.GetTile(pOTrap.Quality, pOTrap.Owner), lOLevel);

				return true;
			}

			return pIHost.World.Remove(lOVictim);
		}

		/// <summary>
		/// TRAP_INVENTORY (TRIGGER.C:664): the item quality * 32 + owner, searched as FindObj does (the paperdoll, the
		/// backpack, then inside them); it trips - the else branch - when it is not carried, when fine x asks for it worn
		/// and it is not (ObjWorn: armour slots 0-4, the rings 9 and 10, a shield in the off hand), or when z asks for
		/// at least that many of a counted stack. The first game ends the chain where this takes the else branch.
		/// </summary>
		public static bool InventoryTrips(UWObject pOTrap, IUWTrapHost pIHost)
		{
			int liWanted = UWObjectMechanics.GetInventoryTrapItemId(pOTrap);
			UWObject lOFound = UWTrapRules.FindCarriedLikeFindObj(pIHost.World.Player.Inventory, liWanted,
				pIHost.World.Level != null ? pIHost.World.Level.Masterlist : null, out int liSlot);

			if (lOFound == null)
				return true;

			UWPlayerFile lOFile = fRecord(pIHost);

			if (pOTrap.XPos != 0 && !ObjWorn(liWanted, liSlot, lOFile != null && UWPlayerRecordUw2.LeftyBit(lOFile)))
				return true;

			return pOTrap.ZPos > 0 && lOFound.HasQuantity && (lOFound.Quantity & UWObjectRecordCodec.SpecialLinkBit) == 0
				&& pOTrap.ZPos > lOFound.Quantity;
		}

		/// <summary>ObjWorn (uw2 INVDATA.C:382): slots 0-4 and 9-10 always; the hand at lefty + 7 for a shield (major 0,
		/// minor 2 or 3, class index 0xB-0xF).</summary>
		public static bool ObjWorn(int piId, int piSlot, bool pbLefty)
		{
			if ((piSlot >= 0 && piSlot <= 4) || piSlot == 9 || piSlot == 10)
				return true;

			if ((pbLefty ? 1 : 0) + 7 != piSlot)
				return false;

			return (piId >> 6) == 0 && ((piId & 0x30) >> 4) >= 2 && (piId & 0xF) >= 0xB;
		}

		// ------------------------------------------------- The spell trap (part H1b)

		/// <summary>
		/// TRAP_SPELL (TRIGGER.C:517): inanimate_spell -> do_spell(quality, owner) with the TRAP as the caster and whoever
		/// set it off as the target. With an object as the caster the lasting spells of classes 0 to 3 do nothing
		/// (set_curmagic only for the player), nor do a heal (it heals the trap), a backfire, restoring mana or the
		/// extra spells but Tremor (xt_spells: "caster == ThePlayer || sub == 9"). The shipped traps use class 5 (the
		/// missiles), 11/9 (Tremor), 13/5 (hallucination), 14 (cutscenes) and 0/0 (nothing). The casting is engine work
		/// (IUWTrapHost.FireSpellTrap, the second game's branch).
		/// </summary>
		private static bool fFireSpell(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			int liMajor = pOTrap.Quality;
			int liMinor = pOTrap.Owner;

			if (!SpellTrapCasts(liMajor, liMinor))
			{
				UWTrapLog.Detail = string.Format("-> spell {0}/{1}: nothing with a trap as the caster", liMajor, liMinor);

				return true;
			}

			UWTrapLog.Detail = string.Format("-> spell {0}/{1}", liMajor, liMinor);

			return pIHost.FireSpellTrap(pOTrap, piTileX, piTileY, liMajor, liMinor);
		}

		/// <summary>Whether a spell trap of this class and minor does anything (see fFireSpell).</summary>
		public static bool SpellTrapCasts(int piMajor, int piMinor)
		{
			if (piMajor <= 4 || piMajor == 9 || piMajor == 10)
				return false;

			return piMajor != UWExtraSpellRulesUw2.MajorClass || (piMinor & 0x3F) == UWExtraSpellRulesUw2.TremorSpell;
		}

		// ------------------------------------------------- checkTrap (part H2)

		/// <summary>
		/// checkTrap (uw2 obj/OBJUSE.C:388): the traps and triggers in pOObject's chain after something happened to it -
		/// piMode 4 a use, 8 and 9 a door opening and closing, 0xB an unlock. The first trap-class thing of the chain; on a
		/// switch only one, and a switch that is on (index above 7) takes the second. A trigger goes to UseTrigger with
		/// pOObject as the thing acted on, then the next trap-class thing after it; a plain trap without flags, other than a
		/// door trap, goes off on a use only, at the object's square (piTileX, piTileY), and is deleted.
		/// </summary>
		public static void CheckTrap(UWObject pOObject, int piMode, int piTileX, int piTileY, IUWTrapHost pIHost, int piSearchSkill = 0)
		{
			UWLevel lOLevel = pIHost != null && pIHost.World != null ? pIHost.World.Level : null;

			if (pOObject == null || lOLevel == null || pOObject.HasQuantity || pOObject.Quantity == 0)
				return;

			List<UWObject> lOChain = fChainOf(pOObject, lOLevel.Masterlist);
			int liAt = fNextTrapClass(lOChain, 0);
			bool lbRunNext = true;
			int liLevelBefore = UWTrapRules.LevelIndexOf(pIHost);

			if ((pOObject.ID & SwitchClassMask) == SwitchClassBase)
			{
				lbRunNext = false;

				if ((pOObject.ID & SwitchOnBit) != 0 && liAt >= 0)
				{
					int liSecond = fNextTrapClass(lOChain, liAt + 1);

					if (liSecond >= 0)
						liAt = liSecond;
				}
			}

			for (int liGuard = 0; liAt >= 0 && liGuard < MaxDepth; liGuard++)
			{
				UWObject lOTrap = lOChain[liAt];

				if (IsTrigger(lOTrap.ID))
				{
					int liNext = fNextTrapClass(lOChain, liAt + 1);

					UseTrigger(pOObject, lOTrap, piMode, pIHost, piSearchSkill);

					if (UWTrapRules.LevelIndexOf(pIHost) != liLevelBefore || !lbRunNext)
						return;

					liAt = liNext;

					continue;
				}

				if ((lOTrap.Flags & 0xF) == 0 && piMode == UseMode && lOTrap.ID != UWObjectMechanics.DoorTrapId)
				{
					fSetOffTrap(lOTrap, SquareCarrier(piTileX, piTileY), pOObject, pIHost, 0);
					UWTrapChainRemoval.RemoveTrapFromChain(pOObject, lOTrap, lOLevel);
				}

				return;
			}
		}

		/// <summary>SetOffTrap takes a square where ours takes the trigger that names it: a stand-in trigger for a trap set
		/// off without one (a plain trap on a used thing, a fumbled disarm of one).</summary>
		public static UWObject SquareCarrier(int piTileX, int piTileY)
		{
			return new UWObject((ushort)UWObjectMechanics.UseTriggerId) { Quality = (ushort)piTileX, Owner = (ushort)piTileY };
		}

		/// <summary>An object's chain: its resolved contents where loaded, else the links.</summary>
		private static List<UWObject> fChainOf(UWObject pOObject, List<UWObject> pORecords)
		{
			return pOObject.Contents != null ? new List<UWObject>(pOObject.Contents)
				: new List<UWObject>(UWObjectChain.Walk(pOObject, pORecords, pORecords.Count));
		}

		/// <summary>Obj_InList(.., MAJOR_TRAP, -1, -1): the first trap-class thing from piFrom on, or -1.</summary>
		private static int fNextTrapClass(List<UWObject> pOChain, int piFrom)
		{
			for (int liAt = piFrom; liAt < pOChain.Count; liAt++)
			{
				if (pOChain[liAt] != null && UWObjectIds.Major(pOChain[liAt].ID) == UWObjectMechanics.TrapMajorClass)
					return liAt;
			}

			return -1;
		}

		// ------------------------------------------------- Finding and disarming (part H2)

		/// <summary>
		/// DetectedTrap (uw2 game/SKILLS.C:874): a trigger of minor class 3 in the thing's chain (UWObjectSpellRulesUw2.TryFindTrap)
		/// and skill_check(skill, 10 + twice the world); null when there is none (the original's 0). The look asks on a
		/// success, Detect Trap reports a success; Remove Trap goes on for any result but a plain failure ("if
		/// (DetectedTrap(target, 0x2D))" - a critical failure is not 0).
		/// </summary>
		public static UWSkillCheck.ResultEnum? DetectTrap(UWObject pOThing, IList<UWObject> pORecords, int piSkill, int piDungeonLevel)
		{
			if (!UWObjectSpellRulesUw2.TryFindTrap(pOThing, pORecords, out _, out _))
				return null;

			return UWSkillCheck.Check(piSkill, UWObjectSpellRulesUw2.DetectTarget(piDungeonLevel));
		}

		/// <summary>
		/// RemoveTrap (uw2 game/SKILLS.C:893): skill_check(skill, 8 + the world) on the found trigger's trap.
		///   success        "The <trap> on the <thing> was successfully disarmed." - the trap goes from the trigger's
		///                  target square with every trigger aiming at it (delete_trap), the thing's own one included when it
		///                  lies in the level;
		///   critical fail  "Your bumbling attempts have set off the <trap>." - the trigger goes off with mode -1, the
		///                  player setting it off and the thing acted on;
		///   failure        "Unable to defuse trap.".
		/// Null when the thing holds no such trigger. The messages go to the firing's presentation.
		/// </summary>
		public static UWSkillCheck.ResultEnum? RemoveTrap(UWObject pOThing, int piSkill, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.World.Level;
			DataImport lOData = pIHost.World.Data;

			if (lOLevel == null || !UWObjectSpellRulesUw2.TryFindTrap(pOThing, lOLevel.Masterlist, out UWObject lOTrigger, out UWObject lOTrap))
				return null;

			UWSkillCheck.ResultEnum leResult = UWSkillCheck.Check(piSkill, UWObjectSpellRulesUw2.DisarmTarget(lOLevel.LevelNumber));
			IUWPresentation lOShown = pIHost.World.Presentation;
			string lsTrap = UWObjectSpellRulesUw2.TrapName(lOData, lOTrap);

			if (UWSkillCheck.IsSuccess(leResult))
			{
				lOShown.AddMessage("The " + lsTrap + " on the " + UWText.ObjectName(lOData, pOThing.ID)
					+ UWText.General(lOData, UWObjectSpellRulesUw2.DisarmedString));

				if (lOTrap != null)
					UWTrapChainRemoval.AfterOneShotTrigger(lOTrigger, lOTrap, lOLevel, true);

				return leResult;
			}

			if (leResult == UWSkillCheck.ResultEnum.CriticalFailure)
			{
				lOShown.AddMessage(UWText.General(lOData, UWObjectSpellRulesUw2.SetOffString) + lsTrap
					+ UWText.General(lOData, UWObjectSpellRulesUw2.PeriodString, "."));

				UseTrigger(pOThing, lOTrigger, ChainMode, pIHost);

				return leResult;
			}

			lOShown.AddGeneralMessage(UWObjectSpellRulesUw2.UnableToDefuseString);

			return leResult;
		}

		/// <summary>"You found a trap! Do you wish to try to disarm it? " (string 0x103 of block 1, ours one higher).</summary>
		public const int TrapFoundString = 0x103 + 1;

		// ------------------------------------------------- The second game's own kinds (part H3)

		public const int SpecialEffectTrapId = 0x184;

		public const int SkillTrapId = 0x18A;

		public const int ExperienceTrapId = 0x191;

		public const int JumpTrapId = 0x192;

		public const int ChangeFromTrapId = 0x193;

		public const int ChangeToTrapId = 0x194;

		public const int OscillatorTrapId = 0x195;

		public const int ProximityTrapId = 0x196;

		public const int PitTrapId = 0x197;

		public const int BridgeTrapId = 0x198;

		/// <summary>
		/// TRAP_SPECIAL_EFFECT (do_sfx, uw2 event/WORLDEV.C:635) with the trap's quality as the type and owner as the
		/// argument: 5 to 8 flash the screen in colour owner + 0x40 * (8 - type), 2 plays sound owner + 0x64, 4 shakes the
		/// screen for owner * 2. The flash waits for the second game's presentation (its FlashWindow is empty so far).
		/// </summary>
		private static bool fFireSpecialEffect(UWObject pOTrap, IUWTrapHost pIHost)
		{
			int liType = pOTrap.Quality;
			int liArgument = pOTrap.Owner;
			IUWPresentation lOShown = pIHost.World.Presentation;

			switch (liType)
			{
				case 5:
				case 6:
				case 7:
				case 8:
					lOShown.FlashWindow(liArgument + (0x40 * (8 - liType)), FlashSeconds);
					UWTrapLog.Detail = string.Format("-> flash in colour {0}", liArgument + (0x40 * (8 - liType)));

					return true;

				case 2:
					lOShown.PlaySoundAtPlayer((liArgument + SoundBase) & 0xFF);
					UWTrapLog.Detail = string.Format("-> sound 0x{0:X}", (liArgument + SoundBase) & 0xFF);

					return true;

				case 4:
					lOShown.ShakeScreen((liArgument << 1) & 0xFF);
					UWTrapLog.Detail = string.Format("-> shake {0}", (liArgument << 1) & 0xFF);

					return true;

				default:
					UWTrapLog.Detail = string.Format("-> type {0}: nothing", liType);

					return true;
			}
		}

		/// <summary>do_sfx's sounds start at 0x64.</summary>
		private const int SoundBase = 0x64;

		/// <summary>fill_FB's flash, as long as the first game's coloured flashes last.</summary>
		private const float FlashSeconds = 0.25f;

		/// <summary>
		/// TRAP_SKILL (TRIGGER.C:364): the value of quality - 0 to 2 an attribute (strength, dexterity, intelligence), 3 the
		/// value 15, 4 and up the skill quality - 4 in the record's numbering - against owner * 3: with heading 1 a
		/// skill_check that must not fail, else a plain comparison. When it PASSES the chain takes the else branch.
		/// </summary>
		private static bool fSkillPasses(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWPlayerVitals lOVitals = pIHost.World.Player.Vitals;
			int liWhich = pOTrap.Quality;
			int liValue = lOVitals == null ? 0
				: liWhich == 0 ? lOVitals.Strength : liWhich == 1 ? lOVitals.Dexterity : liWhich == 2 ? lOVitals.Intelligence
				: liWhich == 3 ? 15 : lOVitals.GetSkillByNumber(liWhich - 4);
			int liDifficulty = pOTrap.Owner * 3;
			bool lbFailed = pOTrap.Heading == 1
				? UWSkillCheck.Check(liValue, liDifficulty) <= UWSkillCheck.ResultEnum.Failure
				: liValue < liDifficulty;

			UWTrapLog.Detail = string.Format("-> {0} {1} against {2}{3}: {4}", liWhich < 3 ? "attribute" : liWhich == 3 ? "fixed" : "skill",
				liValue, liDifficulty, pOTrap.Heading == 1 ? " (rolled)" : string.Empty, lbFailed ? "failed, on" : "passed, else branch");

			return !lbFailed;
		}

		/// <summary>
		/// TRAP_PROXIMITY (TRIGGER.C:386): whether who set it off stands in the rectangle from the target square to quality
		/// squares east and owner squares north, and - unless fine x is set - not above the trap's height, and - unless
		/// fine y is set - above it. Outside, the chain takes the else branch. The player's spot is the world's, a
		/// creature's its record's (UWTrapFiring.SetterSpot); a thing's is not known and counts as outside.
		/// </summary>
		private static bool fWithinProximity(UWObject pOTrap, UWObject pOTrigger, IUWTrapHost pIHost)
		{
			(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
			UWSpot lOSpot = pIHost.Firing.ByCreature || pIHost.Firing.ByThing ? pIHost.Firing.SetterSpot : pIHost.World.Player.FeetSpot;

			bool lbInside = ProximityHolds(pOTrap, liX, liY, lOSpot);

			UWTrapLog.Detail = string.Format("-> {0} {1}/{2} z {3}: {4}", pIHost.Firing.ByCreature ? "creature" : pIHost.Firing.ByThing ? "thing" : "player",
				lOSpot.Tile.X, lOSpot.Tile.Y, lOSpot.ZPos, lbInside ? "inside, on" : "outside, else branch");

			return lbInside;
		}

		/// <summary>The proximity test for a spot (see fWithinProximity), the target square given.</summary>
		public static bool ProximityHolds(UWObject pOTrap, int piX, int piY, UWSpot pOSpot)
		{
			if (!pOSpot.IsOnMap)
				return false;

			int liX = pOSpot.Tile.X;
			int liY = pOSpot.Tile.Y;
			int liZ = pOSpot.ZPos;

			return liX >= piX && liY >= piY && liX - piX <= pOTrap.Quality && liY - piY <= pOTrap.Owner
				&& (pOTrap.XPos != 0 || liZ <= pOTrap.ZPos) && (pOTrap.YPos != 0 || liZ > pOTrap.ZPos);
		}

		/// <summary>TRAP_EXPERIENCE (TRIGGER.C:501): ((quality * 8 + (owner &amp; 7)) - 0x100) * 2^(owner &gt;&gt; 3), which can be
		/// negative, through player_get_exp (UWPlayerVitals.ChangeExperience).</summary>
		public static int ExperienceOf(UWObject pOTrap)
		{
			return ((pOTrap.Quality << 3) + (pOTrap.Owner & 7) - 0x100) * (1 << (pOTrap.Owner >> 3));
		}

		private static bool fFireExperience(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWPlayerVitals lOVitals = pIHost.World.Player.Vitals;

			if (lOVitals == null)
				return false;

			lOVitals.ChangeExperience(ExperienceOf(pOTrap), pIHost.World.Level.LevelNumber);
			UWTrapLog.Detail = string.Format("-> experience {0}", ExperienceOf(pOTrap));

			return true;
		}

		/// <summary>TRAP_JUMP (set_jmp(quality, owner, heading)): the mover that set it off jumps when fast enough. The player
		/// through the avatar; a creature's or a thing's jump is not built (none of the shipped jump traps answers them).</summary>
		private static bool fFireJump(UWObject pOTrap, IUWTrapHost pIHost)
		{
			if (pIHost.Firing.ByCreature || pIHost.Firing.ByThing)
			{
				UWTrapLog.Detail = "-> not the player: not built";

				return false;
			}

			bool lbJumped = pIHost.World.Player.Jump(pOTrap.Quality, pOTrap.Owner != 0, pOTrap.Heading);

			// The log tells a throw from too slow a pass (per user 2026-11-15: a slow step logged "jump" as well).
			UWTrapLog.Detail = string.Format("-> {0} {1}{2} from speed {3}", lbJumped ? "jump" : "too slow, no jump", pOTrap.Quality,
				pOTrap.Owner != 0 ? ", stopping" : string.Empty, pOTrap.Heading * 0x2F);

			return true;
		}

		/// <summary>The limits of do_change_grokking's four fields - wall, floor, type, height: a field at its limit in the
		/// change-to trap is left, in the change-from trap matches anything.</summary>
		private static readonly int[] miGrokLimits = { 0x3F, 0x0F, 0x0A, 0x0F };

		/// <summary>A change trap's four fields: quality the wall, heading + zpos bit 4 / 2 the floor, owner the type, zpos's
		/// low four bits the height.</summary>
		public static int[] ChangeFields(UWObject pOTrap)
		{
			return new[] { pOTrap.Quality, pOTrap.Heading + ((pOTrap.ZPos & 0x10) >> 1), pOTrap.Owner, pOTrap.ZPos & 0xF };
		}

		/// <summary>
		/// TRAP_CHANGE_FROM (do_change_grokking, uw2 event/WORLDEV.C): over fine x by fine y squares from the target square
		/// (fine y 0: as many as fine x; fine x 0: the whole map, 1 to 0x3E), every square whose fields match the change-from
		/// trap's - only the fields the linked change-to trap sets are compared, a change-from field at its limit matches
		/// anything - takes the change-to values (change_terrain).
		/// </summary>
		private static bool fFireChangeFrom(UWObject pOTrap, int piX, int piY, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.World.Level;
			UWObject lOTo = UWObjectMechanics.GetLinkedObject(pOTrap, lOLevel.Masterlist);

			if (lOTo == null || lOLevel.TileData == null)
				return false;

			int[] liFrom = ChangeFields(pOTrap);
			int[] liTo = ChangeFields(lOTo);
			int liStartX = piX;
			int liStartY = piY;
			int liWidth = pOTrap.XPos;
			int liDepth = pOTrap.YPos;

			if (liWidth == 0)
			{
				liStartX = 1;
				liStartY = 1;
				liWidth = 0x3E;
				liDepth = 0x3E;
			}
			else if (liDepth == 0)
				liDepth = liWidth;

			int liChanged = 0;

			for (int liX = liStartX; liX < liStartX + liWidth; liX++)
			{
				for (int liY = liStartY; liY < liStartY + liDepth; liY++)
				{
					UWTile lOTile = lOLevel.GetTile(liX, liY);

					if (lOTile == null)
						continue;

					int[] liNow = { WallSlot(lOTile), FloorSlot(lOTile), (int)lOTile.TileType, lOTile.FloorHeight >> 4 };
					bool lbMatches = true;

					for (int liField = 0; liField < 4; liField++)
					{
						if (liTo[liField] < miGrokLimits[liField])
						{
							lbMatches &= liFrom[liField] == liNow[liField] || liFrom[liField] == miGrokLimits[liField];
							liNow[liField] = liTo[liField];
						}
					}

					if (lbMatches && ChangeTerrain(liX, liY, liNow[0], liNow[1], liNow[3], liNow[2], 0, 0, pIHost))
						liChanged++;
				}
			}

			UWTrapLog.Detail = string.Format("-> {0} squares changed", liChanged);

			return true;
		}

		/// <summary>The tile's wall slot (TILE_WALL, raw bits 16-21) and floor slot (raw bits 10-13).</summary>
		public static int WallSlot(UWTile pOTile)
		{
			return (int)((pOTile.RawTileData >> 16) & 0x3F);
		}

		public static int FloorSlot(UWTile pOTile)
		{
			return (int)((pOTile.RawTileData >> 10) & 0xF);
		}

		/// <summary>
		/// TRAP_OSCILLATOR (TRIGGER.C:401): steps the target square's height (fine x bits 1-2 = 0), floor (1) or wall (2)
		/// by one - up with fine x bit 0 set, else down - while the new value stays within quality to owner (change_terrain
		/// with adjust 4). A height going below a solid square opens it at 15; one going to 16 on an open square closes
		/// it. At the end it turns: fine x bit 0 is cleared at owner on the way up, set at quality on the way down (the
		/// limit 15 for heights, 0x3F with owner 0x10) - and on level 0x44 the sunken moongate is checked
		/// (check_for_sunken_moongate). The trap keeps its direction in its own record.
		/// </summary>
		private static bool fFireOscillator(UWObject pOTrap, int piX, int piY, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.World.Level;
			UWTile lOTile = lOLevel.GetTile(piX, piY);

			if (lOTile == null)
				return false;

			int liMode = (pOTrap.XPos & 6) >> 1;
			int liStep = (pOTrap.XPos & 1) != 0 ? 1 : -1;
			int liFloor = FloorLimit;
			int liWall = WallLimit;
			int liHeight = 0x3F;
			int liType = 0xF;
			int liLimit = 0x3F;
			int liNow = 0;

			switch (liMode)
			{
				case 0:
					liLimit = 0xF;
					liNow = liHeight = (lOTile.FloorHeight >> 4) + liStep;

					if (lOTile.TileType == UWTile.TileTypeEnum.solid && liStep < 0)
					{
						liType = (int)UWTile.TileTypeEnum.open;
						liHeight = 0xF;
					}
					else if (lOTile.TileType == UWTile.TileTypeEnum.open && liHeight == 0x10)
						liType = (int)UWTile.TileTypeEnum.solid;

					if (pOTrap.Owner == 0x10)
						liLimit = 0x3F;

					break;

				case 1:
					liNow = liFloor = FloorSlot(lOTile) + liStep;

					break;

				case 2:
					liNow = liWall = WallSlot(lOTile) + liStep;

					break;
			}

			bool lbMoved = pOTrap.Quality <= liNow && pOTrap.Owner >= liNow;

			if (lbMoved)
				ChangeTerrain(piX, piY, liWall, liFloor, liHeight, liType, 0, 0, pIHost, 4);

			if (liStep == 1)
			{
				if (pOTrap.Owner == liNow && pOTrap.Quality < liLimit)
					pOTrap.XPos = (ushort)(pOTrap.XPos & 6);
			}
			else if (pOTrap.Quality == liNow && pOTrap.Owner < liLimit)
			{
				pOTrap.XPos = (ushort)((pOTrap.XPos & 6) + 1);

				if (lOLevel.LevelNumber == SunkenMoongateLevel)
					fCheckSunkenMoongate(pIHost);
			}

			UWTrapLog.Detail = string.Format("-> {0} {1} to {2}{3}", liMode == 0 ? "height" : liMode == 1 ? "floor" : "wall",
				liStep > 0 ? "up" : "down", liNow, lbMoved ? string.Empty : " (beyond its range, stays)");

			return true;
		}

		/// <summary>Level 0x44, where the oscillators lower the moongate's square.</summary>
		public const int SunkenMoongateLevel = 0x44;

		/// <summary>check_for_sunken_moongate (TRIGGER.C:1247): once 0x18/2 has risen, it and the squares beside it go down
		/// to 0 and 2/0x15 comes up to 4.</summary>
		private static void fCheckSunkenMoongate(IUWTrapHost pIHost)
		{
			UWTile lOTile = pIHost.World.Level.GetTile(0x18, 2);

			if (lOTile == null || (lOTile.FloorHeight >> 4) == 0)
				return;

			ChangeTerrain(0x18, 2, 0x17, 4, 0, 1, 6, 5, pIHost);
			ChangeTerrain(2, 0x15, 0x14, 4, 4, 1, 0, 0, pIHost);
		}

		/// <summary>
		/// TRAP_PIT (TRIGGER.C:450): a square at height 0 is filled to fine x + fine y * 8 with owner's floor (above 15 it
		/// turns solid); any other is dug to 0 with quality's floor. The type becomes open, or stays when it is open already
		/// (change_terrain with adjust 4).
		/// </summary>
		private static bool fFirePit(UWObject pOTrap, int piX, int piY, IUWTrapHost pIHost)
		{
			UWTile lOTile = pIHost.World.Level.GetTile(piX, piY);

			if (lOTile == null)
				return false;

			int liHeight;
			int liFloor;

			if ((lOTile.FloorHeight >> 4) == 0)
			{
				liHeight = pOTrap.XPos + ((pOTrap.YPos & 7) << 3);
				liFloor = pOTrap.Owner;
			}
			else
			{
				liHeight = 0;
				liFloor = pOTrap.Quality;
			}

			int liType = liHeight > 15 ? (int)UWTile.TileTypeEnum.solid
				: lOTile.TileType != UWTile.TileTypeEnum.open ? (int)UWTile.TileTypeEnum.open : 0x3F;

			ChangeTerrain(piX, piY, WallLimit, liFloor, liHeight, liType, 0, 0, pIHost, 4);
			UWTrapLog.Detail = string.Format("-> {0}/{1} to height {2}, floor {3}", piX, piY, liHeight, liFloor);

			return true;
		}

		/// <summary>The bridge object (ITEM_BRIDGE).</summary>
		public const int BridgeId = 0x164;

		/// <summary>
		/// TRAP_BRIDGE (TRIGGER.C:469): quality squares from the target square, east with heading 2, west 6, north 0, south
		/// 4 (heading is kept even); owner bits 4-5 say 1 place a bridge (place_bridge: none there yet of that heading and
		/// height, set down at fine 3 or 4 by the heading) or 2 take it away (destroy_bridge); a placed one gets owner's low
		/// bits as its flags.
		/// </summary>
		private static bool fFireBridge(UWObject pOTrap, int piX, int piY, IUWTrapHost pIHost)
		{
			pOTrap.Heading = (ushort)(pOTrap.Heading & 6);

			int liHeading = pOTrap.Heading;
			int liSign = liHeading < 4 ? 1 : -1;
			int liDx = (liHeading & 2) != 0 ? liSign : 0;
			int liDy = (liHeading & 2) != 0 ? 0 : liSign;
			int liMode = pOTrap.Owner >> 4;
			int liDone = 0;
			int liX = piX;
			int liY = piY;

			for (int liLeft = pOTrap.Quality; liLeft > 0; liLeft--, liX += liDx, liY += liDy)
			{
				if (liMode == 1)
				{
					UWObject lOBridge = fPlaceBridge(liX, liY, pOTrap.ZPos, liHeading, pIHost);

					if (lOBridge != null)
					{
						lOBridge.Flags = (ushort)(pOTrap.Owner & 0xF);
						liDone++;
					}
				}
				else if (liMode == 2)
				{
					UWObject lOBridge = fFindBridge(liX, liY, pOTrap.ZPos, liHeading, pIHost.World.Level);

					if (lOBridge != null && pIHost.World.Remove(lOBridge))
						liDone++;
				}
			}

			UWTrapLog.Detail = string.Format("-> {0} bridges {1}", liDone, liMode == 1 ? "placed" : liMode == 2 ? "removed" : "(nothing to do)");

			return true;
		}

		/// <summary>A bridge of that heading and height on the square, or null.</summary>
		private static UWObject fFindBridge(int piX, int piY, int piZ, int piHeading, UWLevel pOLevel)
		{
			UWTile lOTile = pOLevel.GetTile(piX, piY);

			return lOTile != null && lOTile.ObjectsInTile != null
				? lOTile.ObjectsInTile.Find(o => o != null && o.ID == BridgeId && o.Heading == piHeading && o.ZPos == piZ) : null;
		}

		/// <summary>place_bridge (TRIGGER.C:1190): the bridge there, or a new one at fine (3 + heading above 2, 3 + heading 2
		/// or 4), that height and heading.</summary>
		private static UWObject fPlaceBridge(int piX, int piY, int piZ, int piHeading, IUWTrapHost pIHost)
		{
			UWObject lOThere = fFindBridge(piX, piY, piZ, piHeading, pIHost.World.Level);

			if (lOThere != null)
				return lOThere;

			if (pIHost.World.Level.GetTile(piX, piY) == null)
				return null;

			UWObject lOBridge = UWObjectFactory.Create(BridgeId, pIHost.World.Data, UWObjectFactory.Unset, 0);

			if (lOBridge == null)
				return null;

			lOBridge.XPos = (ushort)(3 + (piHeading > 2 ? 1 : 0));
			lOBridge.YPos = (ushort)(3 + (piHeading == 2 || piHeading == 4 ? 1 : 0));
			lOBridge.ZPos = piZ;
			lOBridge.Heading = (ushort)piHeading;

			return pIHost.World.Spawn(lOBridge, new UWTilePos(piX, piY)) ? lOBridge : null;
		}

		// ------------------------------------------------- Enter, exit and pressure plates (part H4)

		/// <summary>The plate thresholds of check_pplate: more than 4, or 84 with fine y bit 2 (tenths of a stone).</summary>
		public const int LightPlate = 4;

		public const int HeavyPlate = 84;

		/// <summary>
		/// check_pplate (uw2 event/TRIGGER.C:1091) for the setter the host names, on the square and at height piZ: piMode 6
		/// enter or 0xE exit (a change of square), 7 pressure or 0xF release (a change of height on the square, a pick-up, a
		/// thing coming to rest). Only the first trigger of the square's list is asked as an enter or exit trigger (by the
		/// mover); from then on the mode is the pressure mode. A plate - a trigger of that mode at height piZ - goes off as by
		/// the player when the weight there crosses its threshold (fine y bit 0 its state: released on pressure, pressed on
		/// release), and then every plate of the square flips (UpdatePlate). With none gone off, a plate of the other mode
		/// that passes the same test flips them without going off. True when the square has a plate.
		/// </summary>
		public static bool CheckPlates(UWTile pOTile, int piZ, int piMode, IUWTrapHost pIHost)
		{
			if (pOTile == null || pOTile.ObjectsInTile == null || pIHost == null || pIHost.World == null)
				return false;

			int liMode = piMode;
			bool lbPlate = false;
			bool lbRan = false;
			UWObject lOPrevious = null;
			UWTrapHostAsPlayer lIAsPlayer = new UWTrapHostAsPlayer(pIHost) { PlateTile = pOTile };

			foreach (UWObject lOTrigger in new List<UWObject>(pOTile.ObjectsInTile))
			{
				if (lOTrigger == null || !IsTrigger(lOTrigger.ID))
					continue;

				int liTriggerMode = ModeOf(lOTrigger.ID);

				if (liTriggerMode == liMode && (liMode & 7) == EnterMode)
					UseTrigger(null, lOTrigger, liMode, pIHost);

				if ((liMode & 7) == EnterMode)
					liMode++;

				if ((liMode & 7) == PressureMode && liTriggerMode == liMode)
				{
					lbPlate = true;

					if (lOTrigger.ZPos != piZ || !fPlateTurns(lOTrigger, liMode, pOTile, pIHost))
						continue;

					if (UseTrigger(null, lOTrigger, liMode, lIAsPlayer))
						UpdatePlate(lOTrigger, pOTile, pIHost);

					lbRan = true;
				}
				else if ((liMode & 7) == PressureMode && (liTriggerMode & 7) == PressureMode)
					lOPrevious = lOTrigger;
			}

			if (lOPrevious != null && !lbRan)
			{
				lbPlate = true;

				if (fPlateTurns(lOPrevious, liMode, pOTile, pIHost))
					UpdatePlate(lOPrevious, pOTile, pIHost);
			}

			return lbPlate;
		}

		/// <summary>Whether a plate in the state its fine y bit 0 gives changes under piMode: a released plate on pressure when
		/// the weight is now over its threshold, a pressed one on release when it no longer is.</summary>
		private static bool fPlateTurns(UWObject pOPlate, int piMode, UWTile pOTile, IUWTrapHost pIHost)
		{
			bool lbPressed = (pOPlate.YPos & 1) != 0;

			if (!((lbPressed && piMode == ReleaseMode) || (!lbPressed && piMode == PressureMode)))
				return false;

			bool lbHeavy = WeightOn(pOTile, pOPlate.ZPos, pIHost) > ((pOPlate.YPos & 4) != 0 ? HeavyPlate : LightPlate);

			return lbHeavy == (piMode == PressureMode);
		}

		/// <summary>
		/// update_pplate (TRIGGER.C:1147): every plate of the square (a trigger of mode 7 or 0xF) takes the flipped state in
		/// fine y bit 0, and with the plate's fine y bit 1 the square's floor steps one up when it is pressed now, one down
		/// when released.
		/// </summary>
		public static void UpdatePlate(UWObject pOPlate, UWTile pOTile, IUWTrapHost pIHost)
		{
			int liNotSet = (pOPlate.YPos & 1) != 0 ? 0 : 1;

			foreach (UWObject lOAt in pOTile.ObjectsInTile)
			{
				if (lOAt != null && IsTrigger(lOAt.ID) && (ModeOf(lOAt.ID) & 7) == PressureMode)
					lOAt.YPos = (ushort)(liNotSet + (lOAt.YPos & 6));
			}

			if ((pOPlate.YPos & 2) == 0)
				return;

			UWLevel lOLevel = pIHost.World.Level;
			int liFloor = FloorSlot(pOTile) + (liNotSet != 0 ? 1 : -1);

			if (lOLevel != null && lOLevel.TextureInfos != null && liFloor >= 0 && liFloor + pOTile.FloorSlotBase < lOLevel.TextureInfos.Length)
			{
				pOTile.SetFloorTexture(liFloor, lOLevel.TextureInfos);
				pIHost.RebuildGeometry();
			}
		}

		/// <summary>
		/// check_weight with the player's load (OBJECTS.C:530, Ply_Weight): at height piZ on the square, every lying thing's
		/// mass times its count, a container with what it holds (at that height, as the original walks it); the creatures
		/// whose records stand there; the player when his feet are there, his own mass and his load (the record's carried
		/// weight and the thing on the pointer). Tenths of a stone.
		/// </summary>
		public static int WeightOn(UWTile pOTile, int piZ, IUWTrapHost pIHost)
		{
			IUWWorld lIWorld = pIHost.World;
			UWCommonObjectProperties lOCommon = lIWorld.Data != null ? lIWorld.Data.CommonObjectProperties : null;
			List<UWObject> lOMaster = lIWorld.Level != null ? lIWorld.Level.Masterlist : null;
			int liWeight = 0;

			foreach (UWObject lOAt in pOTile.ObjectsInTile)
			{
				if (lOAt != null && !(lOAt is UWNpc))
					liWeight += fWeightAt(lOAt, piZ, lOCommon, lOMaster, 0);
			}

			List<UWCreatureRef> lOCreatures = new List<UWCreatureRef>();

			lIWorld.FindCreatures(UWTileArea.Around(new UWTilePos(pOTile.X, pOTile.Z), 0, 0, UWCreatureTile.Record), UWCreatureFilter.Living, lOCreatures);

			foreach (UWCreatureRef lOCreature in lOCreatures)
			{
				if (lOCreature.Record != null && lOCreature.Record.ZPos == piZ)
					liWeight += fMass(lOCreature.ObjectId, lOCommon);
			}

			UWSpot lOFeet = lIWorld.Player.FeetSpot;

			if (lOFeet.IsOnMap && lOFeet.Tile.X == pOTile.X && lOFeet.Tile.Y == pOTile.Z && lOFeet.ZPos == piZ)
			{
				UWPlayerFile lOFile = lIWorld.Session != null ? lIWorld.Session.PlayerFileUw2 : null;
				UWObject lOHeld = lIWorld.Player.Inventory != null ? lIWorld.Player.Inventory.CursorItem : null;

				liWeight += fMass(UWObjectMechanics.AdventurerObjectId, lOCommon) + (lOFile != null ? UWPlayerRecordUw2.Weight(lOFile) : 0)
					+ (lOHeld != null ? ItemWeight(lOHeld, lOCommon, lOMaster) : 0);
			}

			return liWeight;
		}

		private static int fMass(int piId, UWCommonObjectProperties pOCommon)
		{
			return pOCommon != null && pOCommon.TryGet(piId, out UWCommonObjectProperties.Entry lOEntry) ? lOEntry.MassTenthStones : 0;
		}

		/// <summary>check_weight's step for one thing at the height (its contents walked at the same height).</summary>
		private static int fWeightAt(UWObject pOObject, int piZ, UWCommonObjectProperties pOCommon, List<UWObject> pOMaster, int piDepth)
		{
			if (pOObject.ZPos != piZ || piDepth > 8)
				return 0;

			if (pOObject.HasQuantity)
				return fMass(pOObject.ID, pOCommon) * ((pOObject.Quantity & UWObjectRecordCodec.SpecialLinkBit) == 0 ? pOObject.Quantity : 1);

			int liWeight = fMass(pOObject.ID, pOCommon);

			if (pOMaster != null && pOObject.Quantity > 0 && UWObjectIds.Major(pOObject.ID) != UWObjectMechanics.TrapMajorClass)
			{
				IEnumerable<UWObject> lOHeld = pOObject.Contents != null ? (IEnumerable<UWObject>)pOObject.Contents
					: UWObjectChain.Walk(pOObject, pOMaster, pOMaster.Count);

				foreach (UWObject lOAt in lOHeld)
				{
					if (lOAt != null)
						liWeight += fWeightAt(lOAt, piZ, pOCommon, pOMaster, piDepth + 1);
				}
			}

			return liWeight;
		}

		/// <summary>ItemWeight (uw2 inv/INVDATA.C:450): a stack's mass times its count, else the mass and what it holds.</summary>
		public static int ItemWeight(UWObject pOObject, UWCommonObjectProperties pOCommon, List<UWObject> pOMaster)
		{
			if (pOObject.HasQuantity)
				return fMass(pOObject.ID, pOCommon) * ((pOObject.Quantity & UWObjectRecordCodec.SpecialLinkBit) == 0 ? pOObject.Quantity : 1);

			int liWeight = fMass(pOObject.ID, pOCommon);
			IEnumerable<UWObject> lOHeld = pOObject.Contents != null ? (IEnumerable<UWObject>)pOObject.Contents
				: pOMaster != null && pOObject.Quantity > 0 ? UWObjectChain.Walk(pOObject, pOMaster, pOMaster.Count) : null;

			if (lOHeld != null)
			{
				foreach (UWObject lOAt in lOHeld)
				{
					if (lOAt != null)
						liWeight += ItemWeight(lOAt, pOCommon, pOMaster);
				}
			}

			return liWeight;
		}

		/// <summary>
		/// A mover's change, as the original's physics reports it (change_GrSq, set_phys_data, hgt_change): from one square
		/// to another, the exit plates of the old one at the old height and the enter plates of the new; on the same
		/// square at another height, release at the old height and - when the square has a plate - pressure at the new.
		/// </summary>
		public static void MoverMoved(UWTile pOFrom, int piFromZ, UWTile pOTo, int piToZ, IUWTrapHost pIHost)
		{
			if (pOFrom != pOTo)
			{
				CheckPlates(pOFrom, piFromZ, ExitMode, pIHost);
				CheckPlates(pOTo, piToZ, EnterMode, pIHost);

				return;
			}

			if (piFromZ != piToZ && CheckPlates(pOTo, piFromZ, ReleaseMode, pIHost))
				CheckPlates(pOTo, piToZ, PressureMode, pIHost);
		}

		// ------------------------------------------------- Timer triggers (part H4)

		/// <summary>The timer clock wraps at 128 steps (timer_tick).</summary>
		public const int TimerTickWrap = 0x80;

		/// <summary>A timer goes off only within this many squares of its target square, on both axes.</summary>
		public const int TimerReach = 8;

		/// <summary>
		/// update_animobj's timers (uw2 obj/EFFECT.C:243): each timer trigger of the level's list with height z goes off
		/// once for every multiple of z + 1 the step counter passes in these piFrames steps, as by the player with mode 0xA -
		/// only while the player (or the eye of Roaming Sight, pOEye when it roams) is within 8 squares of its target square
		/// on both axes. Returns the counter after the steps, wrapped at 128.
		/// </summary>
		public static int TickTimers(int piTick, int piFrames, UWTilePos pOPlayer, UWTilePos pOEye, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.World.Level;

			if (lOLevel != null && lOLevel.Timers != null)
			{
				foreach (int liIndex in new List<int>(lOLevel.Timers))
				{
					UWObject lOTimer = liIndex > 0 && liIndex < lOLevel.Masterlist.Count ? lOLevel.Masterlist[liIndex] : null;

					if (lOTimer == null)
						continue;

					int liLeft = ((piTick + piFrames) / (lOTimer.ZPos + 1)) - (piTick / (lOTimer.ZPos + 1));

					if (liLeft <= 0 || !(fNear(pOPlayer, lOTimer) || fNear(pOEye, lOTimer)))
						continue;

					for (; liLeft > 0; liLeft--)
						UseTrigger(null, lOTimer, TimerMode, pIHost);
				}
			}

			return (piTick + piFrames) & (TimerTickWrap - 1);
		}

		private static bool fNear(UWTilePos pOAt, UWObject pOTimer)
		{
			return pOAt.IsOnMap && System.Math.Abs(pOAt.X - pOTimer.Quality) < TimerReach && System.Math.Abs(pOAt.Y - pOTimer.Owner) < TimerReach;
		}

		/// <summary>a_null trap (0x18F).</summary>
		public const int NullTrapId = 0x018F;

		/// <summary>The second game's hack traps (do_trap_hack, 0x183, G3 part H6): the general ones; a quality without
		/// an entry is written to the trap log as not built.</summary>
		public static UWDoActionTable CreateDoActionTable()
		{
			UWDoActionTable lOTable = new UWDoActionTable()
				.Register(CrystalBallHack, "crystal ball",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fCrystalBall(pOTrap, liX, liY, pIHost);
					})
				.Register(RaiseObjectsHack, "raise two objects", (pOTrap, pOTrigger, pOSwitch, pIHost) => fToggleObjectHeight(pOTrap, true, pIHost))
				.Register(RaiseObjectHack, "raise an object", (pOTrap, pOTrigger, pOSwitch, pIHost) => fToggleObjectHeight(pOTrap, false, pIHost))
				.Register(SetOwnerHack, "set owner", (pOTrap, pOTrigger, pOSwitch, pIHost) => fSetLinkedField(pOTrap, false, pIHost))
				.Register(SetOwnerHackB, "set owner", (pOTrap, pOTrigger, pOSwitch, pIHost) => fSetLinkedField(pOTrap, false, pIHost))
				.Register(SetQualityHack, "set quality", (pOTrap, pOTrigger, pOSwitch, pIHost) => fSetLinkedField(pOTrap, true, pIHost))
				.Register(InvisibleHack, "show or hide", (pOTrap, pOTrigger, pOSwitch, pIHost) => fSetInvisible(pOTrap, pIHost))
				.Register(GoalByWhoamiHack, "goal by whoami", (pOTrap, pOTrigger, pOSwitch, pIHost) => fGoalByWhoami(pOTrap, pIHost))
				.Register(UnseenHack, "map unseen",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fMakeUnseen(liX, liY, pOTrap.Owner, pIHost);
					})
				.Register(SetterGoalHack, "the setter's goal", (pOTrap, pOTrigger, pOSwitch, pIHost) => fSetterGoal(pOTrap, pIHost))
				.Register(BestWeaponHack, "best weapon", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fBestWeapon(x, y, pOTrap.Owner, pIHost)))
				.Register(FrazniumHack, "fraznium field",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fFraznium(liX, liY, pOTrap.Owner, pIHost);
					})
				.Register(CycleFloorHack, "texture cycle",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fCycleFloor(liX, liY, pOTrap.XPos * 2, pOTrap.YPos * 2, pOTrap.Owner, pOTrap.ZPos, pOTrap.Heading != 0, pIHost);
					})
				.Register(ThinIceHack, "thin ice", (pOTrap, pOTrigger, pOSwitch, pIHost) => fThinIce(pOTrap, pIHost))
				.Register(StandingWaveHack, "standing wave", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fStandingWave(pOTrap, x, y, pIHost)))
				.Register(SwitchFlipHack, "flip a switch", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fSwitchFlip(x, y, pOTrap.Owner, pIHost)))
				.Register(ArrowPillarsHack, "arrow pillars", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fResetArrowPillars(x, y, pOTrap.Owner != 0, pIHost)))
				.Register(PillarsHack, "Scintillus pillars", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fTogglePillars(pOTrap, x, y, pIHost)))
				.Register(ForceFieldHack, "force field", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fToggleForceFields(x, y, pIHost)))
				.Register(RandomSwitchesHack, "random switches", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fPlayWithSwitches(x, y, pIHost)))
				.Register(LightSpheresHack, "light spheres", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fRechargeLights(x, y, pIHost)))
				.Register(GemRotateHack, "gem facet", (pOTrap, pOTrigger, pOSwitch, pIHost) => fGemRotate(pIHost))
				.Register(GemTripHack, "gem travel", (pOTrap, pOTrigger, pOSwitch, pIHost) => fGemTrip(pIHost))
				.Register(GraffitiHack, "graffiti", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fGraffiti(x, y, pOTrap.Owner, pIHost)))
				.Register(BottlesHack, "bottle return", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fRedeemBottles(x, y, pIHost)))
				.Register(CurePotionsHack, "spoiled cure potions", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fRuinCurePotions(x, y, pIHost)))
				.Register(CourtyardHack, "Britannia goes dry", (pOTrap, pOTrigger, pOSwitch, pIHost) => fAt(pOTrigger, (x, y) => fCourtyard(pOTrap, x, y, pIHost)))
				.Register(BliySkupHack, "Bliy Skup's chamber", (pOTrap, pOTrigger, pOSwitch, pIHost) => fBliySkup(pIHost))
				.Register(QbertHack, "q*bert floor", (pOTrap, pOTrigger, pOSwitch, pIHost) => fQbert(pOTrap.Owner, pIHost));

			lOTable.Seal();

			return lOTable;
		}

		// ------------------------------------------------- The general hacks (part H6)

		public const int CrystalBallHack = 2;

		public const int RaiseObjectsHack = 21;

		public const int RaiseObjectHack = 22;

		public const int SetOwnerHack = 23;

		public const int SetQualityHack = 27;

		public const int SetOwnerHackB = 28;

		public const int InvisibleHack = 39;

		public const int GoalByWhoamiHack = 43;

		public const int UnseenHack = 45;

		public const int SetterGoalHack = 62;

		public const int BestWeaponHack = 10;

		// change_weapon_playerbest's items (uw2 include/items.h).
		private const int ManiStoneId = 0x0F4;

		private const int WoodenShieldId = 0x03C;

		private const int LeatherGlovesId = 0x026;

		private const int SlingStoneId = 0x010;

		private const int ShortswordId = 0x004;

		private const int HandAxeId = 0x000;

		private const int CudgelId = 0x007;

		/// <summary>
		/// Hack 10, change_weapon_playerbest (uw2 event/WORLDEV.C:1449; found 2026-11-15 by the user behind the open trigger
		/// of the door at 1:19/50, the castle's armoury): the weapon numbered owner (major 0, minor 0) on the target square
		/// becomes the thing for the player's best skill. Magic (the higher of Casting and Mana) at least as high as the
		/// best of Unarmed to Missile (the first highest wins): a Mani stone, or with no magic at all a wooden shield, both
		/// quality 0x3F; else Unarmed leather gloves (0x3F), Missile 30 to 41 sling stones (rand*8/0x8000 + rand*4/0x8000
		/// + 30), Sword a shortsword, Axe a hand axe, Mace a cudgel.
		/// </summary>
		private static bool fBestWeapon(int piX, int piY, int piOwner, IUWTrapHost pIHost)
		{
			UWPlayerVitals lOVitals = pIHost.World.Player.Vitals;
			UWTile lOTile = pIHost.World.Level != null ? pIHost.World.Level.GetTile(piX, piY) : null;
			UWObject lOWeapon = lOTile != null && lOTile.ObjectsInTile != null ? lOTile.ObjectsInTile.Find(o => o != null && o.ID == (piOwner & 0xF)) : null;

			if (lOVitals == null || lOWeapon == null)
			{
				UWTrapLog.Detail = string.Format("-> no weapon {0} on {1}/{2}", piOwner & 0xF, piX, piY);

				return true;
			}

			UWPlayerData.Skill[] leCombat = { UWPlayerData.Skill.Unarmed, UWPlayerData.Skill.Sword, UWPlayerData.Skill.Axe,
				UWPlayerData.Skill.Mace, UWPlayerData.Skill.Missile };
			int liCombat = -1;
			UWPlayerData.Skill leBest = UWPlayerData.Skill.Unarmed;

			foreach (UWPlayerData.Skill leSkill in leCombat)
			{
				if (lOVitals.GetSkill(leSkill) > liCombat)
				{
					liCombat = lOVitals.GetSkill(leSkill);
					leBest = leSkill;
				}
			}

			int liMagic = System.Math.Max(lOVitals.GetSkill(UWPlayerData.Skill.Casting), lOVitals.GetSkill(UWPlayerData.Skill.Mana));
			int liItem;
			int liQuality = -1;
			int liCount = 0;

			if (liMagic >= liCombat)
			{
				liItem = liMagic > 0 ? ManiStoneId : WoodenShieldId;
				liQuality = 0x3F;
			}
			else
			{
				switch (leBest)
				{
					case UWPlayerData.Skill.Unarmed:
						liItem = LeatherGlovesId;
						liQuality = 0x3F;
						break;

					case UWPlayerData.Skill.Missile:
						liItem = SlingStoneId;
						liCount = (UWRandom.Next(0x8000) * 8 / 0x8000) + (UWRandom.Next(0x8000) * 4 / 0x8000) + 0x1E;
						break;

					case UWPlayerData.Skill.Sword:
						liItem = ShortswordId;
						break;

					case UWPlayerData.Skill.Axe:
						liItem = HandAxeId;
						break;

					default:
						liItem = CudgelId;
						break;
				}
			}

			lOWeapon.ID = (ushort)liItem;

			if (liQuality != -1)
				lOWeapon.Quality = (ushort)liQuality;

			if (liCount > 0)
			{
				lOWeapon.HasQuantity = true;
				lOWeapon.Quantity = (ushort)liCount;
			}

			pIHost.World.RedrawObject(lOWeapon);
			UWTrapLog.Detail = string.Format("-> best skill {0} ({1}), magic {2}: 0x{3:X3}{4}", leBest, liCombat, liMagic, liItem,
				liCount > 0 ? " x" + liCount : string.Empty);

			return true;
		}

		/// <summary>The Search Reveal raises the player's to for its look trigger: the crystal ball shows nothing then
		/// ("why 45 in search stops it is not known", uw2 game/PLAYER.C crystal_ball).</summary>
		private const int RevealSearch = 45;

		/// <summary>Hack 2, crystal_ball (uw2 game/PLAYER.C): the view goes to the trap's spot on the target square, facing
		/// its heading, lit at 6, until a key - not while the player's Search is 45 (Reveal's call).</summary>
		private static bool fCrystalBall(UWObject pOTrap, int piX, int piY, IUWTrapHost pIHost)
		{
			UWPlayerVitals lOVitals = pIHost.World.Player.Vitals;

			if (lOVitals != null && lOVitals.GetSkill(UWPlayerData.Skill.Search) == RevealSearch)
			{
				UWTrapLog.Detail = "-> Search 45 (Reveal): no vision";

				return true;
			}

			UWTrapLog.Detail = string.Format("-> vision at {0}/{1}, z {2}, facing {3}", piX, piY, pOTrap.ZPos, pOTrap.Heading);

			return pIHost.FireCameraTrap(pOTrap, piX, piY);
		}

		/// <summary>Hacks 21 and 22, toggle_object_height (uw2 WORLDEV.C): the linked object - and with 21 the next one of
		/// its group of five - goes to the trap's height, or to that plus owner when it is not above it already.</summary>
		private static bool fToggleObjectHeight(UWObject pOTrap, bool pbTwo, IUWTrapHost pIHost)
		{
			List<UWObject> lOMaster = pIHost.World.Level.Masterlist;
			int liLink = pOTrap.Quantity & 0x3FF;

			for (int liAt = 0; liAt <= (pbTwo ? 1 : 0); liAt++)
			{
				int liIndex = ((liLink / 5) * 5) + ((liLink + liAt) % 5);
				UWObject lOObject = liIndex > 0 && liIndex < lOMaster.Count ? lOMaster[liIndex] : null;

				if (lOObject == null)
					continue;

				int liZ = pOTrap.ZPos;

				if (lOObject.ZPos <= liZ)
					liZ += pOTrap.Owner;

				int liWas = lOObject.ZPos;

				lOObject.ZPos = liZ;
				pIHost.OnObjectHeightChanged(lOObject, liWas);
			}

			UWTrapLog.Detail = string.Format("-> object {0}{1} height", liLink, pbTwo ? " and its neighbour" : string.Empty);

			return true;
		}

		/// <summary>Hacks 23 and 28 set the linked object's owner to the trap's owner, 27 its quality.</summary>
		private static bool fSetLinkedField(UWObject pOTrap, bool pbQuality, IUWTrapHost pIHost)
		{
			UWObject lOObject = UWObjectMechanics.GetLinkedObject(pOTrap, pIHost.World.Level.Masterlist);

			if (lOObject == null)
				return false;

			if (pbQuality)
				lOObject.Quality = pOTrap.Owner;
			else
				lOObject.Owner = pOTrap.Owner;

			UWTrapLog.Detail = string.Format("-> 0x{0:X3} {1} {2}", lOObject.ID, pbQuality ? "quality" : "owner", pOTrap.Owner);

			return true;
		}

		/// <summary>Hack 39, SET_INVIS: the linked object shown (owner 0) or hidden (owner 1).</summary>
		private static bool fSetInvisible(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWObject lOObject = UWObjectMechanics.GetLinkedObject(pOTrap, pIHost.World.Level.Masterlist);

			if (lOObject == null)
				return false;

			pIHost.World.SetObjectHidden(lOObject, (pOTrap.Owner & 1) != 0);
			UWTrapLog.Detail = string.Format("-> 0x{0:X3} {1}", lOObject.ID, (pOTrap.Owner & 1) != 0 ? "hidden" : "shown");

			return true;
		}

		/// <summary>
		/// Hack 43 (gronk_whoami with gronkify_change_goal): the creature of whoami z (+ 128 with a heading) - every one with
		/// fine x set, else the first - takes goal owner (critter_set_goal), its target the player with fine y set, else
		/// whoever set the chain off.
		/// </summary>
		private static bool fGoalByWhoami(UWObject pOTrap, IUWTrapHost pIHost)
		{
			int liWhoami = ((pOTrap.Heading > 0 ? 1 : 0) << 7) + pOTrap.ZPos;
			bool lbAll = pOTrap.XPos > 0;
			int liTarget = pOTrap.YPos > 0 ? UWCritterBrain.PlayerIndex : fSetterIndex(pIHost);
			List<UWCreatureRef> lOCreatures = new List<UWCreatureRef>();
			int liChanged = 0;

			pIHost.World.FindCreatures(UWTileArea.WholeLevel, UWCreatureFilter.Living, lOCreatures);

			foreach (UWCreatureRef lOCreature in lOCreatures)
			{
				if (!(lOCreature.Object is UWNpc lONpc) || lONpc.NPCwhoami != liWhoami)
					continue;

				if (pIHost.World.SetCreatureGoal(lOCreature.Object, pOTrap.Owner, liTarget, -1))
					liChanged++;

				if (!lbAll)
					break;
			}

			UWTrapLog.Detail = string.Format("-> whoami {0}: goal {1}, target {2} ({3} changed)", liWhoami, pOTrap.Owner, liTarget, liChanged);

			return true;
		}

		/// <summary>Who set the chain off as a mobile index: a creature's (UWTrapFiring.SetterIndex), the player's 1, 0 for
		/// a thing or nobody.</summary>
		private static int fSetterIndex(IUWTrapHost pIHost)
		{
			if (pIHost.Firing.ByCreature)
				return pIHost.Firing.SetterIndex;

			return pIHost.Firing.ByThing ? 0 : UWCritterBrain.PlayerIndex;
		}

		/// <summary>Hack 62: when the trap links to the creature that set the chain off (or links to 1, anyone), that
		/// creature takes goal owner.</summary>
		private static bool fSetterGoal(UWObject pOTrap, IUWTrapHost pIHost)
		{
			if (!pIHost.Firing.ByCreature)
			{
				UWTrapLog.Detail = "-> not set off by a creature";

				return true;
			}

			int liLink = pOTrap.Quantity & 0x3FF;

			if (liLink != 1 && liLink != pIHost.Firing.SetterIndex)
			{
				UWTrapLog.Detail = "-> another creature";

				return true;
			}

			List<UWCreatureRef> lOCreatures = new List<UWCreatureRef>();

			pIHost.World.FindCreatures(UWTileArea.WholeLevel, UWCreatureFilter.Any, lOCreatures);

			foreach (UWCreatureRef lOCreature in lOCreatures)
			{
				if (lOCreature.Index == pIHost.Firing.SetterIndex && lOCreature.Record != null)
				{
					lOCreature.Record.Goal = pOTrap.Owner;
					UWTrapLog.Detail = string.Format("-> the creature's goal {0}", pOTrap.Owner);

					return true;
				}
			}

			return true;
		}

		public const int FrazniumHack = 11;

		public const int CycleFloorHack = 14;

		public const int ThinIceHack = 17;

		/// <summary>The fraznium gauntlets and circlet (ITEM_FRAZNIUM_GAUNTLETS, ITEM_FRAZNIUM_CIRCLET) and the force field
		/// (MAJOR_RECT minor 2 class 0xD).</summary>
		public const int FrazniumGauntletsId = 0x33;

		public const int FrazniumCircletId = 0x34;

		public const int ForceFieldId = 0x16D;

		/// <summary>The paperdoll slots check_fraznium reads: Inventory[2] the hands, Inventory[0] the head.</summary>
		private const int HandsSlot = 2;

		private const int HeadSlot = 0;

		/// <summary>
		/// Hack 11, check_fraznium (uw2 WORLDEV.C): the force field on the target square goes up out of the way (z 0x7F)
		/// when, with owner 0, the player wears the fraznium gauntlets or the circlet; otherwise down (z 0), in the way.
		/// </summary>
		private static bool fFraznium(int piX, int piY, int piOwner, IUWTrapHost pIHost)
		{
			bool lbWorn = false;
			UWInventoryModel lOInventory = pIHost.World.Player.Inventory;

			if (piOwner == 0 && lOInventory != null)
			{
				UWObject[] lOWorn = lOInventory.GetSavegameEquipment();

				lbWorn = (lOWorn.Length > HandsSlot && lOWorn[HandsSlot] != null && lOWorn[HandsSlot].ID == FrazniumGauntletsId)
					|| (lOWorn.Length > HeadSlot && lOWorn[HeadSlot] != null && lOWorn[HeadSlot].ID == FrazniumCircletId);
			}

			UWTile lOTile = pIHost.World.Level.GetTile(piX, piY);
			UWObject lOField = lOTile != null && lOTile.ObjectsInTile != null ? lOTile.ObjectsInTile.Find(o => o != null && o.ID == ForceFieldId) : null;

			if (lOField == null)
				return false;

			int liWas = lOField.ZPos;

			lOField.ZPos = lbWorn ? 0x7F : 0;
			pIHost.OnObjectHeightChanged(lOField, liWas);
			UWTrapLog.Detail = lbWorn ? "-> fraznium worn: the field rises" : "-> the field stands";

			return true;
		}

		/// <summary>Hack 14, cycle_floor (uw2 WORLDEV.C): over the target square and fine x * 2 by fine y * 2 squares on,
		/// every floor (and with a heading every wall) between owner and z steps one on, after z back to owner.</summary>
		private static bool fCycleFloor(int piX, int piY, int piWidth, int piDepth, int piFirst, int piLast, bool pbWalls, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.World.Level;

			if (lOLevel.TextureInfos == null)
				return false;

			for (int liX = piX; liX <= piX + piWidth; liX++)
			{
				for (int liY = piY; liY <= piY + piDepth; liY++)
				{
					UWTile lOTile = lOLevel.GetTile(liX, liY);

					if (lOTile == null)
						continue;

					int liFloor = FloorSlot(lOTile);

					if (liFloor >= piFirst && liFloor <= piLast)
						lOTile.SetFloorTexture(liFloor + 1 > piLast ? piFirst : liFloor + 1, lOLevel.TextureInfos);

					int liWall = WallSlot(lOTile);

					if (pbWalls && liWall >= piFirst && liWall <= piLast)
						lOTile.SetWallTexture(liWall + 1 > piLast ? piFirst : liWall + 1, lOLevel.TextureInfos);
				}
			}

			pIHost.RebuildGeometry();
			UWTrapLog.Detail = string.Format("-> textures {0}..{1} cycled", piFirst, piLast);

			return true;
		}

		/// <summary>
		/// Hack 17, do_ice_hack (TRIGGER.C:1168): when the player stands on floor owner and an Acrobat check against his
		/// load / 12 (when that is over 20, else 0) fails critically, his square drops by one and takes floor fine y * 8 +
		/// fine x - the ice breaks.
		/// </summary>
		private static bool fThinIce(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWSpot lOFeet = pIHost.World.Player.FloorSpot;
			UWTile lOTile = lOFeet.IsOnMap ? pIHost.World.Level.GetTile(lOFeet.Tile.X, lOFeet.Tile.Y) : null;
			UWPlayerVitals lOVitals = pIHost.World.Player.Vitals;
			UWPlayerFile lOFile = fRecord(pIHost);

			if (lOTile == null || lOVitals == null)
				return false;

			int liWeight = (lOFile != null ? UWPlayerRecordUw2.Weight(lOFile) : 0) / 12;
			UWSkillCheck.ResultEnum leCheck = UWSkillCheck.Check(lOVitals.GetSkill(UWPlayerData.Skill.Acrobat), liWeight > 20 ? liWeight : 0);

			if (FloorSlot(lOTile) != pOTrap.Owner || leCheck != UWSkillCheck.ResultEnum.CriticalFailure)
			{
				UWTrapLog.Detail = "-> the ice holds";

				return true;
			}

			int liHeight = System.Math.Max(0, (lOTile.FloorHeight >> 4) - 1);

			ChangeTerrain(lOFeet.Tile.X, lOFeet.Tile.Y, WallLimit, (pOTrap.YPos << 3) | pOTrap.XPos, liHeight, 0x10, 0, 0, pIHost);
			UWTrapLog.Detail = "-> the ice breaks";

			return true;
		}

		// ------------------------------------------------- The world hacks (part H6b)

		public const int StandingWaveHack = 12;

		public const int SwitchFlipHack = 18;

		public const int ArrowPillarsHack = 19;

		public const int PillarsHack = 20;

		public const int ForceFieldHack = 26;

		public const int RandomSwitchesHack = 29;

		public const int LightSpheresHack = 35;

		public const int GemRotateHack = 54;

		public const int GemTripHack = 55;

		/// <summary>A hack at the trigger's target square.</summary>
		private static bool fAt(UWObject pOTrigger, System.Func<int, int, bool> pFAction)
		{
			(int liX, int liY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

			return pFAction(liX, liY);
		}

		/// <summary>Hack 12, standing_wave (uw2 WORLDEV.C): the seven squares south of the target (y - 1 to y - 7) take the
		/// target's height plus a wave {0 1 2 1 0 -1 -2 -1} times its size by the phase owner / 2; the phase counts up in
		/// the trap, 0 to 15.</summary>
		private static bool fStandingWave(UWObject pOTrap, int piX, int piY, IUWTrapHost pIHost)
		{
			int[] liWave = { 0, 1, 2, 1, 0, -1, -2, -1 };
			UWTile lOTile = pIHost.World.Level.GetTile(piX, piY);

			if (lOTile == null)
				return false;

			int liHeight = lOTile.FloorHeight >> 4;
			int liPhase = pOTrap.Owner;
			int liSize = liPhase < 8 ? 4 - System.Math.Abs(4 - liPhase) : System.Math.Abs(12 - liPhase) - 4;

			for (int liAt = 1; liAt < 8; liAt++)
				ChangeTerrain(piX, piY - liAt, WallLimit, FloorLimit, liHeight + (liSize * liWave[liAt] / 2), 0xF, 0, 0, pIHost);

			pOTrap.Owner = (ushort)((liPhase + 1) & 0xF);
			UWTrapLog.Detail = string.Format("-> wave phase {0}", liPhase);

			return true;
		}

		/// <summary>The switches (MAJOR_RECT minor 3, 0x170-0x17F): index 8 and up is the second state.</summary>
		private static bool fIsSwitch(UWObject pOObject)
		{
			return pOObject != null && (pOObject.ID & SwitchClassMask) == SwitchClassBase;
		}

		/// <summary>flip_switch (uw2 OBJUSE.C): state 3 always flips; 1 or 2 only a switch in its first state, 4 and up only
		/// one in its second; 0 never. The picture and the record together (IUWTrapHost.ToggleSwitchVisual), with the
		/// click. True when it flipped.</summary>
		public static bool FlipSwitch(UWObject pOSwitch, int piState, IUWTrapHost pIHost)
		{
			if (piState == 0 || !fIsSwitch(pOSwitch))
				return false;

			bool lbSecond = (pOSwitch.ID & SwitchOnBit) != 0;

			if (piState != 3 && lbSecond != (piState > 2))
				return false;

			int liBefore = pOSwitch.ID;

			pIHost.ToggleSwitchVisual(pOSwitch);

			// Without a picture to carry it (none drawn), the record alone.
			if (pOSwitch.ID == liBefore)
				pOSwitch.ID = (ushort)(pOSwitch.ID ^ SwitchOnBit);

			pIHost.World.Presentation.PlaySoundAtPlayer(SwitchClickSound);

			return true;
		}

		private const int SwitchClickSound = 0x13;

		/// <summary>Hack 18, switch_flip_hack: the switch on the target square flips to state owner, and if it did its use
		/// triggers go off as by nobody (checkTrap(0L, obj, 4)).</summary>
		private static bool fSwitchFlip(int piX, int piY, int piState, IUWTrapHost pIHost)
		{
			UWTile lOTile = pIHost.World.Level.GetTile(piX, piY);
			UWObject lOSwitch = lOTile != null && lOTile.ObjectsInTile != null ? lOTile.ObjectsInTile.Find(fIsSwitch) : null;

			if (lOSwitch == null || !FlipSwitch(lOSwitch, piState, pIHost))
			{
				UWTrapLog.Detail = "-> no switch flipped";

				return true;
			}

			CheckTrap(lOSwitch, UseMode, piX, piY, new UWTrapHostAsPlayer(pIHost, new UWTrapFiring(false, true)));
			UWTrapLog.Detail = string.Format("-> switch {0}/{1} flipped", piX, piY);

			return true;
		}

		/// <summary>Hack 29, play_with_switches: every switch in the 5 by 2 squares from the target flips one time in two
		/// (flip_switch with 3), no triggers.</summary>
		private static bool fPlayWithSwitches(int piX, int piY, IUWTrapHost pIHost)
		{
			int liFlipped = 0;

			for (int liDx = 0; liDx < 5; liDx++)
			{
				for (int liDy = 0; liDy < 2; liDy++)
				{
					UWTile lOTile = pIHost.World.Level.GetTile(piX + liDx, piY + liDy);

					if (lOTile == null || lOTile.ObjectsInTile == null)
						continue;

					foreach (UWObject lOAt in new List<UWObject>(lOTile.ObjectsInTile))
					{
						if (fIsSwitch(lOAt) && UWRandom.Next(2) != 0 && FlipSwitch(lOAt, 3, pIHost))
							liFlipped++;
					}
				}
			}

			UWTrapLog.Detail = string.Format("-> {0} switches flipped", liFlipped);

			return true;
		}

		/// <summary>
		/// Hack 19, reset_arrow_pillars: from the target square east and north to the first solid square, every square with
		/// a pressure trigger (0x1A4 or 0x1B4) goes back to that trigger's height; with owner set it takes a floor by the
		/// direction to the trigger's target (1 on its row, 2 when behind it), else keeps its floor.
		/// </summary>
		private static bool fResetArrowPillars(int piX, int piY, bool pbFloors, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.World.Level;
			int liReset = 0;

			for (int liX = piX; liX < 0x3F; liX++)
			{
				for (int liY = piY; liY < 0x3F; liY++)
				{
					UWTile lOTile = lOLevel.GetTile(liX, liY);

					if (lOTile == null)
						break;

					if (lOTile.TileType == UWTile.TileTypeEnum.solid)
					{
						if (liY == piY)
							liX = 0x3F;

						break;
					}

					UWObject lOPlate = lOTile.ObjectsInTile != null ? lOTile.ObjectsInTile.Find(o => o != null && (o.ID == 0x1A4 || o.ID == 0x1B4)) : null;

					if (lOPlate == null || (lOTile.FloorHeight >> 4) == lOPlate.ZPos >> 3)
						continue;

					int liDx = lOPlate.Quality - liX;
					int liDy = lOPlate.Owner - liY;
					int liFloor = pbFloors ? (liDy == 0 ? 1 : 0) | ((liDx < 0 || liDy < 0) ? 2 : 0) : FloorLimit;

					ChangeTerrain(liX, liY, WallLimit, liFloor, lOPlate.ZPos >> 3, 0x10, 0, 0, pIHost);
					liReset++;
				}
			}

			UWTrapLog.Detail = string.Format("-> {0} pillars reset", liReset);

			return true;
		}

		/// <summary>Hack 20, toggle_pillars_hack: in a 5 by 3 grid of squares three apart from the target, the pillars
		/// whose bit is set in fine x | fine y &lt;&lt; 3 | heading &lt;&lt; 6 | z &lt;&lt; 9 switch between the lower height
		/// ((owner &amp; 3) + 1) * 2 and the upper lower + 2 + (owner &amp; 0x1C) / 2.</summary>
		private static bool fTogglePillars(UWObject pOTrap, int piX, int piY, IUWTrapHost pIHost)
		{
			int liLower = ((pOTrap.Owner & 3) + 1) * 2;
			int liUpper = liLower + 2 + ((pOTrap.Owner & 0x1C) >> 1);
			int liMask = pOTrap.XPos | (pOTrap.YPos << 3) | (pOTrap.Heading << 6) | (pOTrap.ZPos << 9);

			for (int liI = 0; liI < 5; liI++)
			{
				for (int liJ = 0; liJ < 3; liJ++)
				{
					if ((liMask & (1 << ((liI * 3) + liJ))) == 0)
						continue;

					UWTile lOTile = pIHost.World.Level.GetTile(piX + (liI * 3), piY + (liJ * 3));

					if (lOTile == null)
						continue;

					int liNew = (lOTile.FloorHeight >> 4) <= liLower ? liUpper : liLower;

					ChangeTerrain(piX + (liI * 3), piY + (liJ * 3), WallLimit, FloorLimit, liNew, 0xF, 0, 0, pIHost);
				}
			}

			UWTrapLog.Detail = string.Format("-> pillars {0:X} between {1} and {2}", liMask, liLower, liUpper);

			return true;
		}

		/// <summary>Hack 26, find_and_gronk_force_field: every force field on the target square (and in what lies there)
		/// goes up out of the way, or back down when it was up.</summary>
		private static bool fToggleForceFields(int piX, int piY, IUWTrapHost pIHost)
		{
			UWTile lOTile = pIHost.World.Level.GetTile(piX, piY);
			int liToggled = 0;

			if (lOTile != null && lOTile.ObjectsInTile != null)
			{
				foreach (UWObject lOField in fDeep(lOTile.ObjectsInTile, pIHost.World.Level.Masterlist))
				{
					if (lOField.ID != ForceFieldId)
						continue;

					int liWas = lOField.ZPos;

					lOField.ZPos = liWas < 0x7F ? 0x7F : 0;
					pIHost.OnObjectHeightChanged(lOField, liWas);
					liToggled++;
				}
			}

			UWTrapLog.Detail = string.Format("-> {0} force fields toggled", liToggled);

			return true;
		}

		/// <summary>Hack 35, recharge_lightbulbs: every light sphere (0x93) on the target square, in what lies there too,
		/// gets full quality. (The sparkle over each is not drawn.)</summary>
		private static bool fRechargeLights(int piX, int piY, IUWTrapHost pIHost)
		{
			UWTile lOTile = pIHost.World.Level.GetTile(piX, piY);
			int liCharged = 0;

			if (lOTile != null && lOTile.ObjectsInTile != null)
			{
				foreach (UWObject lOAt in fDeep(lOTile.ObjectsInTile, pIHost.World.Level.Masterlist))
				{
					if (lOAt.ID == LightSphereId)
					{
						lOAt.Quality = 0x3F;
						liCharged++;
					}
				}
			}

			UWTrapLog.Detail = string.Format("-> {0} light spheres recharged", liCharged);

			return true;
		}

		public const int LightSphereId = 0x93;

		/// <summary>Obj_Check's walk: the things of a list and, depth first, what each holds.</summary>
		private static List<UWObject> fDeep(List<UWObject> pOList, List<UWObject> pOMaster)
		{
			List<UWObject> lOAll = new List<UWObject>();

			foreach (UWObject lOAt in new List<UWObject>(pOList))
				fCollect(lOAt, pOMaster, lOAll, 0);

			return lOAll;
		}

		private static void fCollect(UWObject pOObject, List<UWObject> pOMaster, List<UWObject> pOInto, int piDepth)
		{
			if (pOObject == null || piDepth > 8)
				return;

			pOInto.Add(pOObject);

			if (pOObject.HasQuantity || pOObject.Quantity <= 0 || pOObject is UWNpc || UWObjectIds.Major(pOObject.ID) == UWObjectMechanics.TrapMajorClass)
				return;

			IEnumerable<UWObject> lOHeld = pOObject.Contents != null ? (IEnumerable<UWObject>)pOObject.Contents : UWObjectChain.Walk(pOObject, pOMaster, pOMaster.Count);

			foreach (UWObject lOAt in lOHeld)
				fCollect(lOAt, pOMaster, pOInto, piDepth + 1);
		}

		// ------------------------------------------------- The rest of the world hacks (part H6c)

		public const int GraffitiHack = 24;

		public const int BliySkupHack = 25;

		public const int QbertHack = 32;

		public const int BottlesHack = 33;

		public const int CourtyardHack = 34;

		public const int CurePotionsHack = 38;

		/// <summary>The wall picture (special tmap object, MAJOR_RECT minor 2 class 0xE): its owner is its picture.</summary>
		public const int WallPictureId = 0x16E;

		/// <summary>
		/// Hack 24, do_graffiti (uw2 WORLDEV.C): over the target square and 8 squares east and north, every wall picture
		/// showing picture "from" turns to "to" with chance p in 16, by owner: 1 0x3E to 0x25 (16), 2 0x3F to 0x2A (10), 3
		/// 0x3D to 0x3C (6), 4 0x3B to 0x3A (6), 5 0x29 to 0x2A (16); any other owner does nothing.
		/// </summary>
		private static bool fGraffiti(int piX, int piY, int piOwner, IUWTrapHost pIHost)
		{
			int liFrom;
			int liTo;
			int liChance;

			switch (piOwner)
			{
				case 1: liFrom = 0x3E; liTo = 0x25; liChance = 0x10; break;
				case 2: liFrom = 0x3F; liTo = 0x2A; liChance = 10; break;
				case 3: liFrom = 0x3D; liTo = 0x3C; liChance = 6; break;
				case 4: liFrom = 0x3B; liTo = 0x3A; liChance = 6; break;
				case 5: liFrom = 0x29; liTo = 0x2A; liChance = 0x10; break;
				default: return true;
			}

			int liChanged = 0;

			for (int liX = piX; liX <= piX + 8; liX++)
			{
				for (int liY = piY; liY <= piY + 8; liY++)
				{
					UWTile lOTile = pIHost.World.Level.GetTile(liX, liY);

					if (lOTile == null || lOTile.ObjectsInTile == null)
						continue;

					foreach (UWObject lOPicture in new List<UWObject>(lOTile.ObjectsInTile))
					{
						if (lOPicture == null || lOPicture.ID != WallPictureId || lOPicture.Owner != liFrom || UWRandom.Next(0x8000) % 16 >= liChance)
							continue;

						lOPicture.Owner = (ushort)liTo;
						pIHost.World.RedrawObject(lOPicture);
						liChanged++;
					}
				}
			}

			UWTrapLog.Detail = string.Format("-> {0} wall pictures changed", liChanged);

			return true;
		}

		public const int BottleId = 0x13D;

		public const int CoinId = 0xA0;

		/// <summary>Hack 33, redeem_all_bottles: every empty bottle (0x13D) on the target square, inside what lies there too,
		/// becomes a coin.</summary>
		private static bool fRedeemBottles(int piX, int piY, IUWTrapHost pIHost)
		{
			UWTile lOTile = pIHost.World.Level.GetTile(piX, piY);
			int liRedeemed = 0;

			if (lOTile != null && lOTile.ObjectsInTile != null)
			{
				foreach (UWObject lOAt in fDeep(lOTile.ObjectsInTile, pIHost.World.Level.Masterlist))
				{
					if (lOAt.ID != BottleId)
						continue;

					lOAt.ID = CoinId;
					pIHost.World.RedrawObject(lOAt);
					liRedeemed++;
				}
			}

			UWTrapLog.Detail = string.Format("-> {0} bottles redeemed", liRedeemed);

			return true;
		}

		public const int RedPotionId = 0xE5;

		public const int GreenPotionId = 0xE4;

		public const int BottleOfWaterId = 0xBC;

		/// <summary>
		/// Hack 38, ruin_cure_potions with morpheus_ruin_potion: on the target square - and in the player's pack and on his
		/// pointer when he stands there - every red potion with the quantity bit, flags bit 2 and a link becomes a green
		/// potion holding a poison trap of 1 to 8 (CreateObj of a damage trap, owner 1).
		/// </summary>
		private static bool fRuinCurePotions(int piX, int piY, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.World.Level;
			UWTile lOTile = lOLevel.GetTile(piX, piY);
			List<UWObject> lOThings = lOTile != null && lOTile.ObjectsInTile != null ? fDeep(lOTile.ObjectsInTile, lOLevel.Masterlist) : new List<UWObject>();
			UWSpot lOFeet = pIHost.World.Player.FloorSpot;
			UWInventoryModel lOInventory = pIHost.World.Player.Inventory;

			if (lOFeet.IsOnMap && lOFeet.Tile.X == piX && lOFeet.Tile.Y == piY && lOInventory != null)
			{
				List<UWObject> lOCarried = new List<UWObject>(lOInventory.GetSavegameEquipment());

				lOCarried.AddRange(lOInventory.Backpack);

				if (lOInventory.CursorItem != null)
					lOCarried.Add(lOInventory.CursorItem);

				lOCarried.RemoveAll(o => o == null);
				lOThings.AddRange(fDeep(lOCarried, lOLevel.Masterlist));
			}

			int liRuined = 0;

			foreach (UWObject lOPotion in lOThings)
			{
				if (lOPotion.ID != RedPotionId || !lOPotion.HasQuantity || (lOPotion.Flags & UWTrapRules.PlayerSetsOffFlag) == 0 || lOPotion.Quantity == 0)
					continue;

				UWObject lOTrap = UWObjectFactory.Create(UWObjectMechanics.DamageTrapId, pIHost.World.Data);

				if (lOTrap == null)
				{
					lOPotion.ID = BottleOfWaterId;
				}
				else
				{
					lOTrap.Quality = (ushort)(UWRandom.Next(0x8000) % 8 + 1);
					lOTrap.Owner = 1;
					lOPotion.HasQuantity = false;
					lOPotion.Quantity = 0;
					lOPotion.SetContents(new List<UWObject> { lOTrap });
					lOPotion.ID = GreenPotionId;
				}

				pIHost.World.RedrawObject(lOPotion);
				liRuined++;
			}

			UWTrapLog.Detail = string.Format("-> {0} cure potions spoiled", liRuined);

			return true;
		}

		public const int PlantId = 0xD9;

		public const int WiltedPlantId = 0xDA;

		public const int DebrisPileId = 0xD6;

		public const int GrassId = 0xC1;

		public const int MushroomId = 0xB9;

		/// <summary>The fountain's water (MAJOR_ANIMOBJ minor 0 class 9) and the fountain (MAJOR_SPEC minor 2 class 0xE).</summary>
		public const int FountainWaterId = 0x1C9;

		public const int FountainId = 0x12E;

		/// <summary>The castle plot stages courtyard_hacking has already handled (its static "dried"), per session.</summary>
		private static int msiDried;

		private static object msODriedSession;

		/// <summary>
		/// Hack 34, courtyard_hacking (uw2 WORLDEV.C): once per stage of the castle plot, over the target square and fine x
		/// by fine y squares on, the plants wilt (kill_plants: one in three steps on - a plant wilts, a wilted one becomes
		/// debris, grass or a mushroom - and shifts a little); from stage 13 a mushroom springs up in 43 in 100 squares
		/// where it fits; from stage 3 the fountains' water goes, their floor turns to the trap's owner and the fountains
		/// run dry (quality 0).
		/// </summary>
		private static bool fCourtyard(UWObject pOTrap, int piX, int piY, IUWTrapHost pIHost)
		{
			UWPlayerFile lOFile = fRecord(pIHost);

			if (lOFile == null)
				return false;

			if (msODriedSession != pIHost.World.Session)
			{
				msODriedSession = pIHost.World.Session;
				msiDried = 0;
			}

			int liStage = UWPlayerRecordUw2.XClock(lOFile, CastleXClock);

			if ((msiDried & (1 << liStage)) != 0)
			{
				UWTrapLog.Detail = string.Format("-> stage {0} done already", liStage);

				return true;
			}

			msiDried |= 1 << liStage;

			UWLevel lOLevel = pIHost.World.Level;

			for (int liX = piX; liX <= piX + pOTrap.XPos; liX++)
			{
				for (int liY = piY; liY <= piY + pOTrap.YPos; liY++)
				{
					UWTile lOTile = lOLevel.GetTile(liX, liY);

					if (lOTile == null || lOTile.ObjectsInTile == null)
						continue;

					foreach (UWObject lOAt in fDeep(lOTile.ObjectsInTile, lOLevel.Masterlist))
					{
						if (fKillPlant(lOAt))
							pIHost.World.RedrawObject(lOAt);
					}

					if (liStage >= 13)
					{
						if (UWRandom.Next(0x8000) * 100 / 0x8000 < 0x2B)
						{
							int liFineX = (UWRandom.Next(0x8000) * 6 / 0x8000) + 1;
							int liFineY = (UWRandom.Next(0x8000) * 6 / 0x8000) + 1;
							int liZ = (lOTile.FloorHeight >> 4) << 3;

							if (pIHost.World.CanPlace(MushroomId, (liX << 3) + liFineX, (liY << 3) + liFineY, liZ, false, 0))
							{
								UWObject lOMushroom = UWObjectFactory.Create(MushroomId, pIHost.World.Data);

								if (lOMushroom != null)
								{
									lOMushroom.XPos = (ushort)liFineX;
									lOMushroom.YPos = (ushort)liFineY;
									lOMushroom.ZPos = liZ;
									pIHost.World.Spawn(lOMushroom, new UWTilePos(liX, liY));
								}
							}
						}
					}
					else if (liStage >= 3)
					{
						UWObject lOWater = lOTile.ObjectsInTile.Find(o => o != null && o.ID == FountainWaterId);

						if (lOWater != null)
						{
							pIHost.World.Remove(lOWater);

							if (lOLevel.TextureInfos != null)
							{
								lOTile.SetFloorTexture(pOTrap.Owner & 0xF, lOLevel.TextureInfos);
								pIHost.RebuildGeometry();
							}
						}

						UWObject lOFountain = lOTile.ObjectsInTile.Find(o => o != null && o.ID == FountainId);

						if (lOFountain != null)
							lOFountain.Quality = 0;
					}
				}
			}

			UWTrapLog.Detail = string.Format("-> the courtyard at stage {0}", liStage);

			return true;
		}

		/// <summary>kill_plants: true when the thing changed.</summary>
		private static bool fKillPlant(UWObject pOObject)
		{
			if (UWRandom.Next(0x8000) * 3 / 0x8000 != 0)
				return false;

			int liItem;

			switch (pOObject.ID)
			{
				case PlantId:
					liItem = WiltedPlantId;
					break;

				case WiltedPlantId:
					liItem = DebrisPileId;

					if (UWRandom.Next(0x8000) * 2 / 0x8000 != 0)
						liItem = GrassId;

					if (UWRandom.Next(0x8000) * 2 / 0x8000 != 0)
						liItem = MushroomId;

					break;

				case GrassId:
					// The draw of a mushroom or debris is made and never used (as in DOS).
					UWRandom.Next(0x8000);

					return false;

				default:
					return false;
			}

			pOObject.ID = (ushort)liItem;

			if (pOObject.XPos != 0 && pOObject.XPos != 7)
				pOObject.XPos = (ushort)(pOObject.XPos + (UWRandom.Next(0x8000) * 2 / 0x8000) + (UWRandom.Next(0x8000) * 2 / 0x8000) - 1);

			if (pOObject.YPos != 0 && pOObject.YPos != 7)
				pOObject.YPos = (ushort)(pOObject.YPos + (UWRandom.Next(0x8000) * 2 / 0x8000) + (UWRandom.Next(0x8000) * 2 / 0x8000) - 1);

			return true;
		}

		/// <summary>Bliy Skup Ductosnore's death (quest 122).</summary>
		public const int BliySkupQuest = 122;

		/// <summary>
		/// Hack 25, skup_ductosnore (uw2 WORLDEV.C): after his death, with the thing 0xAE on 0x3A/4 and a crystal (0xA1) of
		/// quality 2 or 6 on each of 0x39/4 and 0x3B/4, both crystals go to the middle (0x3A/3, fine 3/3, on its floor), the
		/// trigger at 0x3A/5 goes off as by nobody (fire_trigger_at) and the quest flag is cleared.
		/// </summary>
		private static bool fBliySkup(IUWTrapHost pIHost)
		{
			UWPlayerFile lOFile = fRecord(pIHost);
			UWLevel lOLevel = pIHost.World.Level;

			if (lOFile == null || UWPlayerRecordUw2.GetQuest(lOFile, BliySkupQuest) == 0)
			{
				UWTrapLog.Detail = "-> Bliy Skup lives: nothing";

				return true;
			}

			UWObject lOMiddle = fFirstOf(lOLevel, 0x3A, 4, 0xAE);
			UWObject lOWest = fFirstOf(lOLevel, 0x39, 4, 0xA1);
			UWObject lOEast = fFirstOf(lOLevel, 0x3B, 4, 0xA1);

			if (lOMiddle == null || lOWest == null || lOEast == null || !(lOWest.Quality == 2 || lOWest.Quality == 6)
				|| !(lOEast.Quality == 2 || lOEast.Quality == 6))
			{
				UWTrapLog.Detail = "-> the crystals are not in place";

				return true;
			}

			UWTile lOCentre = lOLevel.GetTile(0x3A, 3);
			int liZ = lOCentre != null ? (lOCentre.FloorHeight >> 4) << 3 : 0;

			foreach (UWObject lOCrystal in new[] { lOWest, lOEast })
			{
				if (!pIHost.World.Remove(lOCrystal))
					continue;

				lOCrystal.XPos = 3;
				lOCrystal.YPos = 3;
				lOCrystal.ZPos = liZ;
				pIHost.World.PutAt(lOCrystal, new UWTilePos(0x3A, 3), 3);
			}

			FireTriggerAt(0x3A, 5, new UWTrapHostAsPlayer(pIHost, new UWTrapFiring(false, true)));
			UWPlayerRecordUw2.SetQuest(lOFile, BliySkupQuest, 0);
			UWTrapLog.Detail = "-> the crystals move to the middle";

			return true;
		}

		private static UWObject fFirstOf(UWLevel pOLevel, int piX, int piY, int piId)
		{
			UWTile lOTile = pOLevel.GetTile(piX, piY);

			return lOTile != null && lOTile.ObjectsInTile != null ? lOTile.ObjectsInTile.Find(o => o != null && o.ID == piId) : null;
		}

		/// <summary>fire_trigger_at (uw2 WORLDEV.C): the first trap or trigger in the square's list goes off - a trigger with
		/// mode -1, a trap at that square - as the host's setter has it (nobody in the original).</summary>
		public static bool FireTriggerAt(int piX, int piY, IUWTrapHost pIHost)
		{
			UWTile lOTile = pIHost.World.Level.GetTile(piX, piY);
			UWObject lOFirst = lOTile != null && lOTile.ObjectsInTile != null
				? lOTile.ObjectsInTile.Find(o => o != null && UWObjectIds.Major(o.ID) == UWObjectMechanics.TrapMajorClass) : null;

			if (lOFirst == null)
				return false;

			return IsTrigger(lOFirst.ID)
				? UseTrigger(null, lOFirst, ChainMode, pIHost)
				: fSetOffTrap(lOFirst, SquareCarrier(piX, piY), null, pIHost, 0);
		}

		// ------------------------------------------------- The q*bert floor (hack 32)

		/// <summary>do_qbert's state in the numbered variables 100 to 108: the colour sequence (7), done, the last square.</summary>
		private const int QbertFirstVariable = 100;

		private const int QbertDone = 107;

		private const int QbertLastSquare = 108;

		/// <summary>The gates' links by colour (gate_links).</summary>
		private static readonly int[] miQbertGates = { 0x21, 0x7F, 0x4F, 0x5B, 0x10, 0x29, 0xC2 };

		/// <summary>The pyramid's corner, the gate pieces (fixed object numbers of level 69) and the plate at 49/51.</summary>
		private const int QbertCornerX = 0x31;

		private const int QbertCornerY = 0x33;

		private const int QbertResetSquare = 0xCF1;

		private const int QbertLevel = 0x45;

		private static int fQbertGet(UWPlayerFile pOFile, int piAt)
		{
			return (short)UWPlayerRecordUw2.GetNumbered(pOFile, QbertFirstVariable + piAt);
		}

		private static void fQbertSet(UWPlayerFile pOFile, int piAt, int piValue)
		{
			UWBinary.Write16(pOFile.Record, UWPlayerRecordUw2.VariablesOffset + ((QbertFirstVariable + piAt) * 2), piValue & 0xFFFF);
		}

		/// <summary>
		/// Hack 32, do_qbert (uw2 WORLDEV.C) - the Ethereal Void's colour pyramid. Owner 0x3F (from a plate): the plate's square
		/// takes the next colour of the learnt sequence; when the whole pyramid (49..53 by 51..55, a triangle) shows one
		/// colour, its walls take it and a gate opens to that colour's world (and with the fifth colour learnt a hidden
		/// thing shows); stepping off breaks it again. Owner below 10: the colour is learnt (the fifth brings 5 after it)
		/// and the player goes to 0x31/0x33 of level 0x45. Owner 10 to 19: a random teleport within level 0x45 by the tables
		/// in the walls of column owner + 0x2E. Owner 0x1E and up: a crystal ball over the colour's place when it is learnt.
		/// </summary>
		private static bool fQbert(int piOwner, IUWTrapHost pIHost)
		{
			UWPlayerFile lOFile = fRecord(pIHost);
			UWLevel lOLevel = pIHost.World.Level;

			if (lOFile == null)
				return false;

			if (fQbertGet(lOFile, 0) != 6)
			{
				fQbertSet(lOFile, 0, 6);

				for (int liAt = 1; liAt < 7; liAt++)
					fQbertSet(lOFile, liAt, -1);
			}

			if (piOwner == 0x3F)
				return fQbertPlate(lOFile, lOLevel, pIHost);

			if (piOwner < 0xA)
			{
				int liAt = 0;

				while (liAt < 6 && fQbertGet(lOFile, liAt) != -1 && fQbertGet(lOFile, liAt) != piOwner)
					liAt++;

				if (fQbertGet(lOFile, liAt) != piOwner)
				{
					fQbertSet(lOFile, liAt, piOwner);

					if (liAt == 4)
						fQbertSet(lOFile, liAt + 1, 5);
				}

				pIHost.World.TeleportSecondGame(UWTeleportUw2.FromWord(QbertLevel, QbertCornerX, QbertCornerY, 0x3D));
				UWTrapLog.Detail = string.Format("-> colour {0} learnt, to the pyramid", piOwner);

				return true;
			}

			if (piOwner < 0x14)
				return fQbertTeleport(piOwner, lOLevel, pIHost);

			int liColour = piOwner - 0x1E;

			for (int liAt = 0; liAt < 7; liAt++)
			{
				if (fQbertGet(lOFile, liAt) != liColour)
					continue;

				int liY = 0x1D - ((6 - liAt) * (7 - liAt) / 2);
				UWObject lOEye = new UWObject((ushort)UWObjectMechanics.DoTrapId) { XPos = 3, YPos = 3, ZPos = 0x74 };

				pIHost.FireCameraTrap(lOEye, 0x20, liY);
				UWTrapLog.Detail = string.Format("-> a vision of colour {0}", liColour);
			}

			return true;
		}

		/// <summary>do_qbert's owner 0x3F (see fQbert).</summary>
		private static bool fQbertPlate(UWPlayerFile pOFile, UWLevel pOLevel, IUWTrapHost pIHost)
		{
			UWTile lOPlate = pIHost is UWTrapHostAsPlayer lOAsPlayer ? lOAsPlayer.PlateTile : null;

			if (lOPlate == null || pOLevel.TextureInfos == null)
			{
				UWTrapLog.Detail = "-> not from a plate";

				return true;
			}

			int liSquare = (lOPlate.Z << 6) + lOPlate.X;

			if (liSquare == QbertResetSquare)
			{
				for (int liX = 0x1D; liX <= 0x23; liX++)
				{
					UWObject lOPicture = fFirstOf(pOLevel, liX, 4, WallPictureId);

					for (int liAt = 0; lOPicture != null && liAt < 7 && fQbertGet(pOFile, liAt) != -1; liAt++)
					{
						if (fQbertGet(pOFile, liAt) == lOPicture.Owner)
						{
							lOPicture.Owner = 5;
							pIHost.World.RedrawObject(lOPicture);
						}
					}
				}
			}

			if (fQbertGet(pOFile, 8) == liSquare)
				return true;

			fQbertSet(pOFile, 8, liSquare);

			int liFloor = FloorSlot(lOPlate);
			int liIndex = 0;

			while (liIndex < 6 && fQbertGet(pOFile, liIndex) != liFloor && fQbertGet(pOFile, liIndex) != -1)
				liIndex++;

			if (fQbertGet(pOFile, liIndex) != liFloor)
			{
				if (fQbertGet(pOFile, 0) > 0)
					fSetFloor(lOPlate, fQbertGet(pOFile, 0), pOLevel, pIHost);

				return true;
			}

			liIndex++;

			if (liIndex > 6 || fQbertGet(pOFile, liIndex) == -1)
				liIndex = 0;

			fSetFloor(lOPlate, fQbertGet(pOFile, liIndex), pOLevel, pIHost);

			// The pyramid: rows 0x33..0x37, each from 0x31 back to 0x31 - 4 + row.
			int liColour = -1;
			int liTop = -1;
			bool lbSame = true;

			for (int liRow = 0; liRow < 5 && lbSame; liRow++)
			{
				for (int liCol = 0; liCol > liRow - 5 && lbSame; liCol--)
				{
					UWTile lOTile = pOLevel.GetTile(liCol + QbertCornerX, liRow + QbertCornerY);

					liTop = lOTile != null ? FloorSlot(lOTile) : -2;

					if (liColour == -1)
						liColour = liTop;
					else if (liColour != liTop)
						lbSame = false;
				}
			}

			bool lbComplete = liColour == liTop && lbSame;
			List<UWObject> lOMaster = pOLevel.Masterlist;

			if (lbComplete)
			{
				for (int liRow = 0; liRow < 6; liRow++)
				{
					for (int liCol = 0; liCol > liRow - 6; liCol--)
					{
						UWTile lOTile = pOLevel.GetTile(liCol + QbertCornerX, liRow + QbertCornerY);

						if (lOTile != null && liColour >= 0)
							lOTile.SetWallTexture(liColour, pOLevel.TextureInfos);
					}
				}

				fQbertSet(pOFile, 7, 0);
				fSetZ(fAtIndex(lOMaster, 0x3CC), 0x60, pIHost);

				UWObject lOGate = fAtIndex(lOMaster, 0x3CD);

				if (lOGate != null)
				{
					if (liColour == 5)
					{
						lOGate.Quality = 0x20;
						lOGate.Owner = 0x19;
						lOGate.Heading = 0;
					}
					else
					{
						lOGate.Quality = 4;
						lOGate.Owner = (ushort)((liColour * 6) + 4);

						if (fQbertGet(pOFile, 4) == liColour)
						{
							UWObject lOShown = fAtIndex(lOMaster, 0x29A);
							UWObject lOLift = fAtIndex(lOMaster, 0x279);

							if (lOShown != null)
							{
								pIHost.World.SetObjectHidden(lOShown, false);

								if (lOLift != null)
									fSetZ(lOLift, lOShown.ZPos, pIHost);
							}
						}
					}
				}

				UWObject lOMoongate = fAtIndex(lOMaster, 0x3CE);

				if (lOMoongate != null && liColour >= 0 && liColour < miQbertGates.Length)
				{
					lOMoongate.HasQuantity = true;
					lOMoongate.Quantity = (ushort)(UWObjectRecordCodec.SpecialLinkBit | miQbertGates[liColour]);
					pIHost.World.SetObjectHidden(lOMoongate, false);

					// The moongate's colour is its link (UWObjectSpawner's moongate colour): its model is built anew (per user
					// 2026-11-16: the pyramid turned green and the gate stayed white).
					pIHost.World.RedrawObject(lOMoongate);
				}
			}

			if ((fQbertGet(pOFile, 7) & 0xFF) == 0 && !lbComplete)
			{
				fQbertSet(pOFile, 7, 1);
				fSetZ(fAtIndex(lOMaster, 0x3CC), 0, pIHost);

				UWObject lOMoongate = fAtIndex(lOMaster, 0x3CE);

				if (lOMoongate != null)
					pIHost.World.SetObjectHidden(lOMoongate, true);
			}

			pIHost.RebuildGeometry();
			UWTrapLog.Detail = string.Format("-> pyramid square {0}/{1} to colour {2}{3}", lOPlate.X, lOPlate.Z, FloorSlot(lOPlate),
				lbComplete ? ", complete" : string.Empty);

			return true;
		}

		/// <summary>do_qbert's owner 10 to 19: a random teleport within level 0x45 by the tables in the walls of column
		/// owner + 0x2E (row 1 the count, rows 0x20 on the x, rows 2 on the y with the facing in their floors).</summary>
		private static bool fQbertTeleport(int piOwner, UWLevel pOLevel, IUWTrapHost pIHost)
		{
			int liColumn = piOwner + 0x2E;
			UWTile lOCount = pOLevel.GetTile(liColumn, 1);
			int liRange = lOCount != null ? WallSlot(lOCount) : 0;
			UWSpot lOPlayer = pIHost.World.Player.FloorSpot;

			if (liRange <= 0)
				return false;

			int liTries = 0;
			UWTile lOFrom;
			UWTile lOTo;
			int liX;
			int liY;

			do
			{
				int liPick = UWRandom.Next(0x8000) % ((liRange * 3) >> 1);

				if (liPick >= liRange)
					liPick = 1;

				lOFrom = pOLevel.GetTile(liColumn, liPick + 0x20);
				lOTo = pOLevel.GetTile(liColumn, liPick + 2);
				liX = lOFrom != null ? WallSlot(lOFrom) : 0;
				liY = lOTo != null ? WallSlot(lOTo) : 0;
			}
			while (++liTries < 4 && System.Math.Abs(liX - lOPlayer.Tile.X) < 3 && System.Math.Abs(liY - lOPlayer.Tile.Y) < 3);

			if (lOFrom == null || lOTo == null)
				return false;

			int liWord = ((FloorSlot(lOTo) + 8) << 2) + ((FloorSlot(lOFrom) & 8) >> 3);
			int liLevel = QbertLevel - (FloorSlot(lOFrom) & 7);

			pIHost.World.TeleportSecondGame(UWTeleportUw2.FromWord(liLevel, liX, liY, liWord));
			UWTrapLog.Detail = string.Format("-> to level {0}, {1}/{2}", liLevel, liX, liY);

			return true;
		}

		private static void fSetFloor(UWTile pOTile, int piFloor, UWLevel pOLevel, IUWTrapHost pIHost)
		{
			if (piFloor < 0 || piFloor + pOTile.FloorSlotBase >= pOLevel.TextureInfos.Length)
				return;

			pOTile.SetFloorTexture(piFloor, pOLevel.TextureInfos);
			pIHost.RebuildGeometry();
		}

		private static UWObject fAtIndex(List<UWObject> pOMaster, int piIndex)
		{
			return piIndex < pOMaster.Count ? pOMaster[piIndex] : null;
		}

		private static void fSetZ(UWObject pOObject, int piZ, IUWTrapHost pIHost)
		{
			if (pOObject == null)
				return;

			int liWas = pOObject.ZPos;

			pOObject.ZPos = piZ;
			pIHost.OnObjectHeightChanged(pOObject, liWas);
		}

		// ------------------------------------------------- The blackrock gem (hacks 54 and 55)

		/// <summary>vars[6]: the facet the gem shows.</summary>
		public const int GemFacetVariable = 6;

		/// <summary>QB_GEMS_USED (quest byte 2): a bit per world's gem used; QB_WORLDS_VISITED (13): a bit per world seen.</summary>
		public const int GemsUsedQuestByte = 2;

		public const int WorldsVisitedQuestByte = 13;

		/// <summary>black_gem_trip's worlds[]: each facet's level, square and teleport word.</summary>
		private static readonly int[,] miGemWorlds =
		{
			{ 0x09, 0x21, 0x20, 0x38 }, { 0x11, 0x1B, 0x22, 0x20 }, { 0x19, 0x12, 0x27, 0x30 }, { 0x21, 0x1F, 0x1F, 0x01 },
			{ 0x29, 0x04, 0x26, 0x30 }, { 0x39, 0x3B, 0x14, 0x20 }, { 0x31, 0x20, 0x20, 0x30 }, { 0x45, 0x20, 0x16, 0x23 }
		};

		/// <summary>The gem's middle in eighths (0xE3, 0x143).</summary>
		private const int GemX8 = 0xE3;

		private const int GemY8 = 0x143;

		/// <summary>"That face of the gem remains opaque, and you are bounced back." (0x15C of block 1, ours one higher).</summary>
		public const int GemOpaqueString = 0x15C + 1;

		/// <summary>Hack 54, black_gem_rotate: a new random facet among the first 1, 3, 6 or all 8 by the castle plot (below
		/// 4, 8, 13), not the one shown and not a used one - 8 (none) after eight tries.</summary>
		private static bool fGemRotate(IUWTrapHost pIHost)
		{
			UWPlayerFile lOFile = fRecord(pIHost);

			if (lOFile == null)
				return false;

			int liFacet = GemRotate(UWPlayerRecordUw2.GetNumbered(lOFile, GemFacetVariable), UWPlayerRecordUw2.XClock(lOFile, CastleXClock),
				UWPlayerRecordUw2.GetNumbered(lOFile, FirstQuestByte + GemsUsedQuestByte));

			UWPlayerRecordUw2.SetNumbered(lOFile, GemFacetVariable, liFacet);
			UWTrapLog.Detail = string.Format("-> the gem shows facet {0}", liFacet);

			return true;
		}

		/// <summary>black_gem_rotate's draw (see fGemRotate).</summary>
		public static int GemRotate(int piOld, int piCastle, int piUsed)
		{
			int liRange = piCastle < 4 ? 1 : piCastle < 8 ? 3 : piCastle < 0xD ? 6 : 8;
			int liGem = UWRandom.Next(0x8000) % liRange;
			int liTries = 0;

			while ((liGem == piOld || ((1 << liGem) & piUsed) != 0) && liTries++ < 8)
				liGem = (liGem + 1) % liRange;

			return liTries >= 8 ? 8 : liGem;
		}

		/// <summary>The facet the player stands at, from his spot in eighths around the gem (black_gem_trip).</summary>
		public static int GemFacet(int piPlayerX8, int piPlayerY8)
		{
			int liRelX = piPlayerX8 - GemX8;
			int liRelY = piPlayerY8 - GemY8;
			int liFacet = System.Math.Abs(liRelX) < System.Math.Abs(liRelY) ? 1 : 0;

			if (liRelY > 0)
				liFacet = 1 - liFacet;
			else
				liFacet += 2;

			if (liRelX < 0)
				liFacet = 7 - liFacet;

			return liFacet;
		}

		/// <summary>
		/// Hack 55, black_gem_trip (uw2 WORLDEV.C): the facet the player stands at takes him to its world when that world's
		/// gem has been used or the gem shows that facet - its level, square and facing (worlds[]); the world is marked
		/// visited (facets 5 and 6 swapped for the bit). Otherwise "That face of the gem remains opaque, and you are bounced
		/// back.".
		/// </summary>
		private static bool fGemTrip(IUWTrapHost pIHost)
		{
			UWPlayerFile lOFile = fRecord(pIHost);
			UWSpot lOPlayer = pIHost.World.Player.FloorSpot;

			if (lOFile == null || !lOPlayer.IsOnMap)
				return false;

			int liFacet = GemFacet(lOPlayer.EighthsX, lOPlayer.EighthsY);
			int liUsed = UWPlayerRecordUw2.GetNumbered(lOFile, FirstQuestByte + GemsUsedQuestByte);
			bool lbOpen = ((1 << liFacet) & liUsed) != 0 || (UWPlayerRecordUw2.GetNumbered(lOFile, GemFacetVariable) & 7) == liFacet;

			if (!lbOpen)
			{
				pIHost.World.Presentation.AddGeneralMessage(GemOpaqueString);
				UWTrapLog.Detail = string.Format("-> facet {0}: opaque", liFacet);

				return true;
			}

			int liBit = liFacet == 5 ? 6 : liFacet == 6 ? 5 : liFacet;

			UWPlayerRecordUw2.SetNumbered(lOFile, FirstQuestByte + WorldsVisitedQuestByte,
				UWPlayerRecordUw2.GetNumbered(lOFile, FirstQuestByte + WorldsVisitedQuestByte) | (1 << liBit));

			pIHost.World.TeleportSecondGame(UWTeleportUw2.FromWord(miGemWorlds[liFacet, 0], miGemWorlds[liFacet, 1], miGemWorlds[liFacet, 2],
				miGemWorlds[liFacet, 3]));
			UWTrapLog.Detail = string.Format("-> facet {0}: to level {1}", liFacet, miGemWorlds[liFacet, 0]);

			return true;
		}

		/// <summary>Hack 45, make_terrain_unseen (uw2 ui/AUTOMAP.C): the automap of the square and owner squares east and
		/// north - a seen square's value of 5 or below goes up by 10, back to unseen.</summary>
		private static bool fMakeUnseen(int piX, int piY, int piSize, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.World.Level;
			byte[] lyMap = lOLevel != null ? lOLevel.AutomapTiles : null;

			if (lyMap == null)
				return false;

			for (int liX = piX; liX <= piX + piSize && liX < 64; liX++)
			{
				for (int liY = piY; liY <= piY + piSize && liY < 64; liY++)
				{
					int liAt = (liY * 64) + liX;
					int liLow = lyMap[liAt] & 0xF;

					if (liLow <= 5)
						liLow += 0xA;

					lyMap[liAt] = (byte)((lyMap[liAt] & 0xF0) | (liLow & 0xF));
				}
			}

			UWTrapLog.Detail = string.Format("-> map {0}/{1} + {2} unseen", piX, piY, piSize);

			return true;
		}
	}
}
