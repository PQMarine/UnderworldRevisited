using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Switch/lever -> trigger -> trap chain (original mechanism per uw-formats.txt,
	/// same sp_link mechanism as container contents, see UWObject.EnsureContentsLoaded).
	/// Use, look, pick-up, open and move triggers all run through here, with the same
	/// trigger->trap pattern the teleport scan of the level loader uses.
	///
	/// ENGINE-FREE since 2026-09-18 (P3 of the engine separation): the chain walk, the
	/// one-shot cleanup and every trap that works on the tile data, the variables, the
	/// inventory or the player's numbers live here. What needs the engine - the flight of an
	/// arrow, a spell, the remote camera, the creature bodies, doors and levers - goes through
	/// IUWTrapHost; UWTriggerSystem is the Unity facade with the old public API.
	///
	/// Trap kinds without an implementation are recorded in the trap log (see fRunTrap)
	/// and break the chain; see fRunTrapInner for the kinds that are handled.
	/// </summary>
	public static class UWTrapRules
	{
		/// <summary>The player has USED pOObject (switch, double door, ...). Looks up the
		/// chain object -> a_use trigger -> trap and fires it. A switch without a function still
		/// flips - see below.</summary>
		public static bool TryFireSwitch(UWObject pOObject, IUWTrapHost pIHost)
		{
			bool lbFired = fTryFireTriggerChain(pOObject, UWObjectMechanics.UseTriggerId, pIHost);

			// A SWITCH WITHOUT A FUNCTION STILL MOVES. The lever on level 3, 48/55 (381) has no a_use
			// trigger in SAVE4, and in the original it can still be thrown (per user, 2026-09-17);
			// ours did nothing at all. Only for switches - doors and other objects also come through
			// here and must not swallow the use.
			if (!lbFired && pOObject != null && pIHost != null
				&& pOObject.GetCategory() == UWObject.ObjectCategoryEnum.Switches
				&& (pIHost.CurrentLevel == null
					|| UWObjectMechanics.GetLinkedObject(pOObject, pIHost.CurrentLevel.Masterlist) == null))
			{
				pIHost.ToggleSwitchVisual(pOObject);
				lbFired = true;
			}

			return lbFired;
		}

		/// <summary>The Search skill value the Reveal spell temporarily raises it to
		/// (value from the reference). Against the highest occurring difficulty of 96 even
		/// that is not enough - the other eight hidden finds pass reliably with it.
		/// </summary>
		public const int RevealSearchSkill = 0x2D;

		/// <summary>The player has LOOKED AT pOObject. Looks up the chain
		/// object -> a_look trigger -> trap. This is how the orb on level 1 (58/13) hangs on its
		/// text trap - the text appears directly after "You see an orb.".</summary>
		/// <param name="piSearchSkill">The player's Search skill. Only hidden
		/// triggers ask for it, see fTryFireTriggerChain.</param>
		public static bool TryFireLookTrigger(UWObject pOObject, IUWTrapHost pIHost, int piSearchSkill)
		{
			return fTryFireTriggerChain(pOObject, UWObjectMechanics.LookTriggerId, pIHost, piSearchSkill);
		}

		/// <summary>Object -> trigger -> trap. Which INTERACTION fires the chain is decided
		/// solely by the trigger type (piExpectedTriggerId): a_use trigger on use, a_look
		/// trigger on look. That is why no special cases per object kind are needed - an object
		/// with the wrong trigger type simply returns false here, and the caller carries on
		/// normally.
		///
		/// Both jumps go through the raw sp_link (Quantity), NOT through
		/// EnsureContentsLoaded/Contents: HasQuantity is not reliably false on trigger
		/// objects (see UWObjectMechanics.GetLinkedObject) - the use/look triggers
		/// on level 1 even have it set throughout. Same approach as the already
		/// working teleport trigger scan in UWLevelLoader.</summary>
		private static bool fTryFireTriggerChain(UWObject pOObject, int piExpectedTriggerId, IUWTrapHost pIHost, int piSearchSkill = 0)
		{
			if (pOObject == null || pIHost == null || pIHost.CurrentLevel == null)
				return false;

			List<UWObject> lOMaster = pIHost.CurrentLevel.Masterlist;

			UWObject lOTrigger = UWObjectMechanics.GetLinkedObject(pOObject, lOMaster);

			if (lOTrigger == null || lOTrigger.ID != piExpectedTriggerId)
				return false;

			// HIDDEN FINDS: a look trigger can carry a search difficulty, and it is stored
			// in its ZPos. If it is zero there is no check - the trigger fires on a mere
			// look, which is how the orb on 58/13 hangs on its text trap. Only a
			// ZPos greater than zero turns it into a find you do not notice without the
			// Search skill.
			//
			// The whole game has 23 look triggers, nine of them with a check - none of them
			// on level 1 (see the removed tool UWLookTriggerDump). These nine are exactly what the Reveal
			// spell exists for: it temporarily raises the skill and fires them.
			//
			// Only on LOOK. Use and move triggers do not ask for the
			// skill, just like in the original.
			//
			// Every look rolls ANEW. Whoever finds nothing can try again - the original
			// keeps no list of checks that already failed.
			if (piExpectedTriggerId == UWObjectMechanics.LookTriggerId && lOTrigger.ZPos > 0
				&& !UWSkillCheck.IsSuccess(UWSkillCheck.Check(piSearchSkill, lOTrigger.ZPos)))
				return false;

			// A LEVER ADVANCES WHEN USED, no matter what hangs behind it. Previously
			// this only happened in the branch of the a_do trap "raise tile"; the puzzle levers on
			// level 5, which have a variable trap behind them, therefore did not move at all
			// (per user, 2026-09-07). This is the point where it is certain that the
			// use really counts: the trigger matches, and any search check has
			// been passed.
			//
			// BEFORE the traps, not after: the platform lever reads its position right
			// back to set the target tile.
			pIHost.AdvanceLever(pOObject);

			// A SWITCH FLIPS ON EVERY USE, whatever trap hangs behind it. This used to happen only
			// in the door trap and text trap branches, so the three buttons on level 3, 5/7 (variable
			// traps behind them) kept their image when pressed (per user, 2026-09-17). Once here
			// also means once per use: a chain with two door traps flipped it twice before.
			if (piExpectedTriggerId == UWObjectMechanics.UseTriggerId)
				pIHost.ToggleSwitchVisual(pOObject);

			return FireTraps(lOTrigger, pOObject, pIHost);
		}

		/// <summary>
		/// an_open trigger: fires when a door is opened.
		///
		/// IT IS NOT IN THE TILE LIST but in the chain of the door itself: the door
		/// points via sp_link to its a_lock, and that one's link leads on to the trigger. That is why
		/// the whole chain of the door is scanned here and not just its first link - a
		/// search in the tile lists does not find these triggers at all (counted over all
		/// nine levels: six of them, on 1, 3, 7 and 8, all behind an a_lock).
		///
		/// This is called only on OPENING, not on closing.
		/// </summary>
		public static bool TryFireOpenTrigger(UWObject pODoorData, IUWTrapHost pIHost)
		{
			if (pODoorData == null || pIHost == null || pIHost.CurrentLevel == null)
				return false;

			pODoorData.EnsureContentsLoaded(pIHost.CurrentLevel.Masterlist);

			if (pODoorData.Contents == null)
				return false;

			bool lbFired = false;

			foreach (UWObject lOLink in pODoorData.Contents)
			{
				if (lOLink == null || lOLink.ID != UWObjectMechanics.OpenTriggerId)
					continue;

				if (fFireTriggerTrap(lOLink, null, pIHost, 0))
					lbFired = true;
			}

			return lbFired;
		}

		/// <summary>
		/// The player has PICKED UP pOObject. Looks up the chain
		/// object -> a_pick up trigger -> trap.
		///
		/// This is how the ambush on level 3 works: the bag on 49/52 has a
		/// pick-up trigger with target tile 44/55, behind it a create-object trap with a
		/// headless as template. Whoever pockets the bag summons the monster.
		/// </summary>
		public static bool TryFirePickUpTrigger(UWObject pOObject, IUWTrapHost pIHost)
		{
			if (pOObject == null || pIHost == null || pIHost.CurrentLevel == null)
				return false;

			List<UWObject> lOMaster = pIHost.CurrentLevel.Masterlist;
			UWObject lOTrigger = UWObjectMechanics.GetLinkedObject(pOObject, lOMaster);

			bool lbFired = fTryFireTriggerChain(pOObject, UWObjectMechanics.PickUpTriggerId, pIHost);

			// A ONE-SHOT TRIGGER ON THE ITEM GOES WITH ITS TRAP. fRemoveOneShotTrap removes the trap
			// and every trigger pointing at it - but only from the tile lists, and this trigger hangs
			// in the chain of the bag itself, the same chain as its contents. So it showed as an item
			// when the bag was opened and stayed there after the ambush had fired (per user on the bag
			// of level 3, 49/52, 2026-09-17). Taken out of the chain it neither shows nor fires again,
			// and saving writes the bag without it. A repeatable trigger (flags bit 1) stays.
			// The bag points at the trigger with its own link field, and the real contents only follow
			// the trigger: taking it out of Contents alone left that pointer, so the trigger was found
			// and fired again on every pick-up (per user, 2026-09-17: the headless came several times).
			// The link now starts at the first real item. Whether the trap ran successfully does not
			// matter - the original removes a one-shot chain after running it.
			if (lOTrigger != null && lOTrigger.ID == UWObjectMechanics.PickUpTriggerId
				&& (lOTrigger.Flags & RepeatableTrapFlag) == 0)
			{
				pOObject.EnsureContentsLoaded(lOMaster);
				pOObject.Contents.Remove(lOTrigger);
				pOObject.Quantity = lOTrigger.Link;
			}

			return lbFired;
		}

		/// <summary>Fires a move trigger (a_move trigger) - unlike use/look triggers it does
		/// not hang on a clicked object but lies in the tile itself
		/// and fires on entering (see UWMoveTrigger). For the rune of warding the host carries
		/// the creature that stepped on it (IUWTrapHost.HasTriggeringCreature).</summary>
		public static bool TryFireMoveTrigger(UWObject pOTrigger, IUWTrapHost pIHost)
		{
			if (pOTrigger == null || pOTrigger.ID != UWObjectMechanics.MoveTriggerId)
				return false;

			return FireTraps(pOTrigger, null, pIHost);
		}

		/// <summary>Fires the trap of a trigger.
		///
		/// Afterwards it continues via the SPECIAL LINK, but only if another trap
		/// or trigger is there (major class 6, i.e. objects 384 to 447) - see
		/// fRunTrapChain. NOT via the link field: that merely continues the list the
		/// object happens to be in. Behind the move triggers on 15/33 and 42/51 it
		/// looks through that field as if two a_door traps hang on each; with both the
		/// chain would have been "close, then toggle" and the portcullis would always have
		/// opened on entering. In the original exactly that does NOT happen (per user, 2026-08-29).
		///
		/// A door trap does not chain on by itself: its sp_link carries the
		/// lock template, and a lock is not major class 6.
		///
		/// pOSwitchObject is the clicked object, if there is one - only for it is the
		/// switch visual toggled. Move triggers have none.</summary>
		public static bool FireTraps(UWObject pOTrigger, UWObject pOSwitchObject, IUWTrapHost pIHost)
		{
			if (pOTrigger == null || pIHost == null || pIHost.CurrentLevel == null)
				return false;

			return fFireTriggerTrap(pOTrigger, pOSwitchObject, pIHost, 0);
		}

		/// <summary>
		/// The trap of a trigger, including the chain behind it - and the cleanup afterwards.
		///
		/// ONE-SHOT OR REPEATABLE is decided by bit 1 of the TRIGGER flags. If it is zero,
		/// the original removes the trap from the target tile after firing: it never fires again.
		/// If it is set, it stays.
		///
		/// VERIFIED IN THE ORIGINAL (user, 2026-09-06) on the bag on level 3 (49/52): the headless
		/// only comes on the first pick-up. Its pick-up trigger carries flags 4, so bit 1
		/// is zero.
		///
		/// The portcullis and door triggers are unaffected - they carry flags 6 or 14
		/// throughout, bit 1 is set. Counted over all nine levels: 257 of 294
		/// trigger-trap pairs actually have the trap in the target tile, and only there
		/// does the removal take effect at all.
		/// </summary>
		private static bool fFireTriggerTrap(UWObject pOTrigger, UWObject pOSwitchObject, IUWTrapHost pIHost, int piDepth)
		{
			UWObject lOTrap = UWObjectMechanics.GetLinkedObject(pOTrigger, pIHost.CurrentLevel.Masterlist);

			if (lOTrap == null)
				return false;

			int liLevelBefore = pIHost.CurrentLevelIndex;

			bool lbRan = fRunTrapChain(lOTrap, pOTrigger, pOSwitchObject, pIHost, piDepth);

			// After a LEVEL CHANGE, CurrentLevel points to another level. Neither the cleanup
			// nor the chain has any business there - the tiles and the
			// object list are different. WHATEVER THE TRAP DID: Trigger_ovr153_3B cleans up after
			// RunTrap_ovr153_24A without looking at its result (UWTrapChainRemoval).
			if (pIHost.CurrentLevelIndex == liLevelBefore)
				RemoveOneShotTrap(pOTrigger, lOTrap, pIHost.CurrentLevel);

			return lbRan;
		}

		/// <summary>
		/// a_damage trap: damage or poison on whoever fired it.
		///
		/// "quality" is the value, "owner" the switch: zero means damage, anything else
		/// poison. Of the game's 26 damage traps, two poison.
		///
		/// NO DICE, NO ARMOUR - the original subtracts the number as is. The strongest
		/// is 40, on level 7; the row of identical twenties on level 2 belongs to
		/// the room where the floor hurts.
		///
		/// For poison a stronger value replaces the running one instead of adding up (see
		/// UWCharacter.ApplyPoison).
		///
		/// IT IS ALWAYS THE PLAYER WHO FIRED IT: our triggers only respond to him. In the original
		/// NPCs can also walk into a trap; that is dropped as long as they do not fire
		/// triggers.
		/// </summary>
		private static bool fFireDamageTrap(UWObject pOTrap, IUWTrapHost pIHost)
		{
			if (!pIHost.HasPlayer || pOTrap.Quality <= 0)
				return false;

			if (UWObjectMechanics.IsPoisonDamageTrap(pOTrap))
			{
				pIHost.PoisonPlayer(pOTrap.Quality);

				UWTrapLog.Detail = string.Format("-> poison {0}", pOTrap.Quality);
			}
			else
			{
				pIHost.DamagePlayer(pOTrap.Quality);

				UWTrapLog.Detail = string.Format("-> {0} damage", pOTrap.Quality);
			}

			return true;
		}

		/// <summary>String block 1, our numbering: "Your Rune of Warding has been set off ".
		/// The reference says 245; checked against a dump of the block.</summary>
		private const int WardMessage = 246;

		/// <summary>First direction text in string block 1, our numbering ("to the North").
		/// The same eight that Detect Monsters uses.</summary>
		public const int FirstDirectionMessage = 37;

		/// <summary>Base damage of the rune of warding, before the casting skill is added.</summary>
		private const int WardBaseDamage = 3;

		/// <summary>
		/// a_ward trap: the rune of warding goes off.
		///
		/// IT ONLY HITS CREATURES, never the one who placed it - the trigger already
		/// takes care of that (see UWMoveTrigger.fIsWardTrigger). The message names the direction
		/// in which it happened, seen from the player; so you do not have to be standing nearby.
		///
		/// THE DAMAGE DEPENDS ON THE CASTING SKILL: three plus a roll from zero to
		/// skill minus one. Nothing is subtracted - in the reference a trap does
		/// not go through the hit calculation of a sword blow, its value goes straight onto the
		/// hit points.
		///
		/// THE CLASS CHECK is included but always passes: the reference compares
		/// quality with the creature's class unless it holds 0x3F. The rune of warding sets
		/// exactly that 0x3F, and there are no other a_ward traps in uw1.
		/// </summary>
		private static bool fFireWardTrap(UWObject pOTrap, IUWTrapHost pIHost)
		{
			if (!pIHost.HasTriggeringCreature)
				return false;

			if (pOTrap.Quality != UWObjectMechanics.WardTrapAnyClass)
				return false;

			int liDamage = WardBaseDamage;

			if (pIHost.HasPlayer)
			{
				int liCasting = pIHost.PlayerCastingSkill;

				if (liCasting > 0)
					liDamage += UWRandom.Next(liCasting);
			}

			pIHost.ApplyWardDamage(liDamage, WardMessage, FirstDirectionMessage);

			UWTrapLog.Detail = string.Format("-> rune of warding, {0} damage", liDamage);

			return true;
		}

		/// <summary>
		/// a_teleport trap: takes the player to another tile, usually on another
		/// level. This is how the stairs connect.
		///
		/// Unlike the other traps, the fields are ON THE TRAP, not on the trigger:
		/// Quality and Owner are the target tile, and ZPos carries the TARGET LEVEL instead of a
		/// height. Zero means "same level", otherwise it is the level number counted from one.
		/// </summary>
		private static bool fFireTeleportTrap(UWObject pOTrap, IUWTrapHost pIHost)
		{
			int liTargetLevel = pOTrap.ZPos == 0 ? pIHost.CurrentLevelIndex : pOTrap.ZPos - 1;

			if (liTargetLevel < 0 || liTargetLevel >= pIHost.LevelCount)
				return false;

			UWTrapLog.Detail = string.Format("-> level {0}, tile {1}/{2}",
				liTargetLevel + 1, pOTrap.Quality, pOTrap.Owner);

			pIHost.TravelTo(liTargetLevel, pOTrap.Quality, pOTrap.Owner);

			return true;
		}

		/// <summary>
		/// a_set variable trap: changes a game variable (see UWGameVariables).
		///
		/// ZPos is the number of the variable, Heading the operation, and the right operand
		/// is in Quality/Owner/YPos (see UWObjectMechanics.GetVariableOperand). Every
		/// result is truncated to six bits - at least one puzzle relies on that,
		/// see UWGameVariables.
		///
		/// NUMBER ZERO MEANS A QUEST FLAG, not variable zero. In all of uw1 there is
		/// exactly one case: level 3, tile 27/21, "set 7". At this point the reference computes
		/// the flag number as 1 &lt;&lt; operand, which would give 128 here - a
		/// number its own SetQuest no longer even accepts for uw1 (there it goes up to
		/// 37). That cannot be what is meant; the operand itself is used, i.e.
		/// flag 7. If that is wrong, it shows up at exactly this one tile.
		/// </summary>
		private static bool fFireSetVariableTrap(UWObject pOTrap)
		{
			int liOperand = UWObjectMechanics.GetVariableOperand(pOTrap);
			int liOperation = pOTrap.Heading;

			if (pOTrap.ZPos == 0)
				return fSetQuestFlagFromTrap(liOperation, liOperand);

			int liOld = UWGameVariables.Get(pOTrap.ZPos);
			int liNew;

			switch (liOperation)
			{
				case 0: liNew = liOld + liOperand; break;
				case 1: liNew = liOld - liOperand; break;
				case 2: liNew = liOperand; break;
				case 3: liNew = liOld & liOperand; break;
				case 4: liNew = liOld | liOperand; break;
				case 5: liNew = liOld ^ liOperand; break;
				case 6: liNew = liOld << liOperand; break;

				default:
					UWTrapLog.Detail = string.Format("-> unknown operation {0}", liOperation);

					return false;
			}

			UWGameVariables.Set(pOTrap.ZPos, liNew);

			UWTrapLog.Detail = string.Format("-> Var {0}: {1} {2} {3} = {4}",
				pOTrap.ZPos, liOld, fGetOperationName(liOperation), liOperand,
				UWGameVariables.Get(pOTrap.ZPos));

			return true;
		}

		/// <summary>The special case "variable number zero" - see fFireSetVariableTrap.</summary>
		private static bool fSetQuestFlagFromTrap(int piOperation, int piFlag)
		{
			int liValue;

			switch (piOperation)
			{
				case 1: liValue = 0; break;
				case 5: liValue = UWQuestFlags.Get(piFlag) != 0 ? 0 : 1; break;
				default: liValue = 1; break;
			}

			UWQuestFlags.Set(piFlag, liValue);

			UWTrapLog.Detail = string.Format("-> quest flag {0} = {1}", piFlag, liValue);

			return true;
		}

		private static string fGetOperationName(int piOperation)
		{
			switch (piOperation)
			{
				case 0: return "+";
				case 1: return "-";
				case 2: return ":=";
				case 3: return "&";
				case 4: return "|";
				case 5: return "^";
				case 6: return "<<";
				default: return "?";
			}
		}

		/// <summary>
		/// a_check variable trap: the comparison itself. The branching is in
		/// fRunTrapChain; here only what was compared is recorded.
		/// </summary>
		private static bool fFireCheckVariableTrap(UWObject pOTrap)
		{
			bool lbTrue = fCheckVariableIsTrue(pOTrap);

			UWTrapLog.Detail = string.Format("-> Var {0}..{1} = {2}, expected {3} -> {4}",
				pOTrap.ZPos, pOTrap.ZPos + pOTrap.Heading, fGetCheckedValue(pOTrap),
				UWObjectMechanics.GetVariableOperand(pOTrap), lbTrue ? "true" : "false");

			return true;
		}

		/// <summary>
		/// Does the comparison of a check trap hold?
		///
		/// A RANGE of variables is checked: from ZPos, as many more as Heading
		/// specifies. How they become one number is decided by XPos:
		///
		///   XPos zero - PACKED: three bits per variable, the first one at the top.
		///   otherwise - SUM: the values are simply added.
		///
		/// The comparison is against Quality/Owner/YPos (see GetVariableOperand).
		///
		/// The packed method makes sense against the data: on level 5 three traps count
		/// variables 31, 32 and 33 up, and the check trap on tile 3/28 compares them
		/// packed against 470 - that is the three digits 7, 2 and 6.
		///
		/// This function changes NOTHING. It is called twice, once for the log
		/// and once for the branching; that is cheaper than a stored intermediate result.
		/// </summary>
		private static bool fCheckVariableIsTrue(UWObject pOTrap)
		{
			return fGetCheckedValue(pOTrap) == UWObjectMechanics.GetVariableOperand(pOTrap);
		}

		private static int fGetCheckedValue(UWObject pOTrap)
		{
			int liValue = 0;

			for (int liAt = pOTrap.ZPos; liAt <= pOTrap.ZPos + pOTrap.Heading; liAt++)
			{
				int liVariable = UWGameVariables.Get(liAt);

				if (pOTrap.XPos == 0)
					liValue = (liValue << 3) | (liVariable & 0x7);
				else
					liValue += liVariable;
			}

			return liValue;
		}

		/// <summary>Bit 1 of the trigger flags: set means the trap stays and fires
		/// again.</summary>
		public const int RepeatableTrapFlag = 0x2;

		/// <summary>
		/// Removes a one-shot trap after its trigger has run - TOGETHER WITH ITS WHOLE CHAIN and the
		/// triggers that point at it, as UW.EXE does (UWTrapChainRemoval, read 2026-10-01; until
		/// then this followed the reference's RemoveTrapChain, which freed only part of a long
		/// chain - the lever on level 3, 52/13, per user).
		///
		/// Seen in the original before (user, 2026-09-07): on level 8, tile 47/32 holds a move
		/// trigger (836, flags 4) and its arrow trap (837, flags 1); after firing, the tile is
		/// empty and the bolt dropped afterwards takes slot 837.
		/// </summary>
		public static void RemoveOneShotTrap(UWObject pOTrigger, UWObject pOTrap, UWLevel pOLevel)
		{
			if (pOTrigger == null || (pOTrigger.Flags & RepeatableTrapFlag) != 0)
				return;

			UWTrapChainRemoval.AfterOneShotTrigger(pOTrigger, pOTrap, pOLevel);
		}

		/// <summary>Major class 6 is traps (384 to 415) and triggers (416 to 447). Only
		/// such objects continue a chain.</summary>
		private const int TrapMajorClass = UWObjectMechanics.TrapMajorClass;

		/// <summary>From here on they are triggers, below it traps.</summary>
		public const int FirstTriggerId = 0x01A0;

		/// <summary>The last trigger; above it major class 6 ends.</summary>
		public const int LastTriggerId = 0x01BF;

		/// <summary>Emergency brake against a chain that goes round in a circle.</summary>
		private const int MaxTrapChainDepth = 8;

		/// <summary>
		/// Fires a trap and then continues along the chain.
		///
		/// SUCCESS means continuing: after a trap we know, the original looks into
		/// its sp_link. If a TRAP is there, it is fired right along with it and inherits
		/// the target tile of the running trigger. If a TRIGGER is there, it is fired
		/// but brings its own target tile - and WITHOUT a search check, since it was not
		/// looked at but follows from the trap.
		///
		/// VERIFIED IN THE ORIGINAL (user, 2026-09-06): the secret passage on level 3 needs exactly
		/// this. The look trigger on the fake wall (48/55) leads to a terrain trap
		/// that opens the tile behind it; its sp_link leads to a second
		/// look trigger and that one to a delete trap that removes the fake wall. Without the
		/// chain the fake wall stayed - in the original it disappears, recognizable by the fact that
		/// its texture does not blend quite seamlessly into the tile wall.
		///
		/// Both chain forms are common: in the whole game 39 traps point via sp_link to
		/// another trap and 45 to a trigger.
		///
		/// A trap we do not know breaks the chain - just like in the reference.
		/// </summary>
		private static bool fRunTrapChain(UWObject pOTrap, UWObject pOTrigger, UWObject pOSwitchObject,
			IUWTrapHost pIHost, int piDepth)
		{
			if (pOTrap == null || piDepth > MaxTrapChainDepth)
				return false;

			int liLevelBefore = pIHost.CurrentLevelIndex;

			if (!fRunTrap(pOTrap, pOTrigger, pOSwitchObject, pIHost))
				return false;

			// A level change ends the chain: the object list behind it belongs to another
			// level, every further jump would lead nowhere.
			if (pIHost.CurrentLevelIndex != liLevelBefore)
				return true;

			UWObject lONext = UWObjectMechanics.GetLinkedObject(pOTrap, pIHost.CurrentLevel.Masterlist);

			// A CHECK TRAP BRANCHES the chain instead of continuing it straight: if the
			// condition holds it continues at the sp_link target, otherwise at ITS neighbour
			// in the tile list. That is how both branches hang on the same thread in the original - see
			// fCheckVariableIsTrue.
			if (pOTrap.ID == UWObjectMechanics.CheckVariableTrapId && !fCheckVariableIsTrue(pOTrap))
				lONext = lONext == null
					? null
					: lONext.GetNextObjectInLine(pIHost.CurrentLevel.Masterlist);

			// THE INVENTORY TRAP DOES NOT BRANCH, it aborts: if the player has the item
			// being searched for, it continues at the sp_link, otherwise not at all. The reference
			// simply returns null for that (an_inventory_trap.Activate).
			if (pOTrap.ID == UWObjectMechanics.InventoryTrapId && !fInventoryTrapMatches(pOTrap, pIHost))
				lONext = null;

			if (lONext == null || (lONext.ID >> 6) != TrapMajorClass)
				return true;

			if (lONext.ID < FirstTriggerId)
			{
				fRunTrapChain(lONext, pOTrigger, pOSwitchObject, pIHost, piDepth + 1);

				return true;
			}

			// A trigger in the chain cleans up afterwards just like the first one - it brings
			// its own flags.
			fFireTriggerTrap(lONext, pOSwitchObject, pIHost, piDepth + 1);

			return true;
		}

		/// <summary>A single trap, without the chain. pOTrigger supplies the target tile.
		///
		/// Every firing is recorded for the overlay (see UWTrapLog) - including those
		/// of trap kinds we do not support yet. When testing, those are exactly
		/// the ones you want to see.</summary>
		private static bool fRunTrap(UWObject pOTrap, UWObject pOTrigger, UWObject pOSwitchObject, IUWTrapHost pIHost)
		{
			UWTrapLog.Detail = null;

			bool lbHandled = fRunTrapInner(pOTrap, pOTrigger, pOSwitchObject, pIHost);

			UWTrapLog.Record(pOTrap, pOTrigger, pIHost.Data, lbHandled);

			return lbHandled;
		}

		private static bool fRunTrapInner(UWObject pOTrap, UWObject pOTrigger, UWObject pOSwitchObject, IUWTrapHost pIHost)
		{
			if (pOTrap.ID == UWObjectMechanics.SetVariableTrapId)
				return fFireSetVariableTrap(pOTrap);

			if (pOTrap.ID == UWObjectMechanics.CheckVariableTrapId)
				return fFireCheckVariableTrap(pOTrap);

			if (pOTrap.ID == UWObjectMechanics.DoorTrapId)
			{
				fFireDoorTrap(pOTrigger, pOTrap, pIHost);
				return true;
			}

			if (pOTrap.ID == UWObjectMechanics.DoTrapId)
			{
				switch (pOTrap.Quality)
				{
					case UWObjectMechanics.DoTrapCameraAction:
						return fFireCameraTrap(pOTrap, pOTrigger, pIHost);

					// QUALITY 3 AND 4 both raise the tile: the original's dispatcher writes
					// "qual=3 or 4" over this branch (DoTraps_ovr153_F7B, label FEE); we tested
					// only the 3 until 2026-09-21.
					case UWObjectMechanics.DoTrapRaiseTileAction:
					case UWObjectMechanics.DoTrapRaiseTileAction + 1:
						fAdvanceHeightLever(pOSwitchObject, pOTrigger, pOTrap.ZPos, pIHost);
						return true;

					case UWObjectMechanics.DoTrapExplodingBookAction:
						return fFireExplodingBookTrap(pOTrap, pIHost);

					case UWObjectMechanics.DoTrapArialAction:
						pIHost.PlayCutscene(ArialCutscene);
						return true;

					case UWObjectMechanics.DoTrapQuakeFirstAction:
					case UWObjectMechanics.DoTrapQuakeFirstAction + 1:
					case UWObjectMechanics.DoTrapQuakeLastAction:
						return fFireQuakeTrap(pOTrap, pIHost);

					case UWObjectMechanics.DoTrapTrespassAction:
						return fFireTrespassTrap(pOTrap, pIHost);

					case UWObjectMechanics.DoTrapBullfrogAction:
						return fFireBullfrogTrap(pOTrap.Owner, pIHost);

					case UWObjectMechanics.DoTrapEmeraldPuzzleAction:
					{
						(int liGemX, int liGemY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fFireEmeraldPuzzleTrap(liGemX, liGemY, pIHost);
					}

					case UWObjectMechanics.DoTrapConversationAction:
						UWTrapLog.Detail = "-> talking door";

						return pIHost.HasMessages
							&& pIHost.BeginConversation(UWObjectMechanics.TalkingDoorConversationSlot);

					case UWObjectMechanics.DoTrapStartConversationAction:
						return fFireConversationTrap(pIHost);

					case UWObjectMechanics.DoTrapEndgameAction:
						UWTrapLog.Detail = "-> end sequence";

						return pIHost.PlayVictory();
				}

				return false;
			}

			if (pOTrap.ID == UWObjectMechanics.CreateObjectTrapId)
			{
				(int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
				return fFireCreateObjectTrap(pOTrap, liTileX, liTileY, pIHost);
			}

			if (pOTrap.ID == UWObjectMechanics.ChangeTerrainTrapId)
			{
				(int liTerrainX, int liTerrainY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
				return fFireChangeTerrainTrap(pOTrap, liTerrainX, liTerrainY, pIHost);
			}

			if (pOTrap.ID == UWObjectMechanics.DeleteObjectTrapId)
				return fFireDeleteObjectTrap(pOTrap, pIHost);

			if (pOTrap.ID == TeleportTrapId)
				return fFireTeleportTrap(pOTrap, pIHost);

			if (pOTrap.ID == UWObjectMechanics.DamageTrapId)
				return fFireDamageTrap(pOTrap, pIHost);

			if (pOTrap.ID == UWObjectMechanics.ArrowTrapId)
			{
				(int liArrowX, int liArrowY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
				return pIHost.FireArrowTrap(pOTrap, liArrowX, liArrowY);
			}

			if (pOTrap.ID == UWObjectMechanics.SpellTrapId)
			{
				(int liSpellX, int liSpellY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
				return fFireSpellTrap(pOTrap, liSpellX, liSpellY, pIHost);
			}

			if (pOTrap.ID == UWObjectMechanics.WardTrapId)
				return fFireWardTrap(pOTrap, pIHost);

			if (pOTrap.ID == UWObjectMechanics.InventoryTrapId)
				return true;

			if (pOTrap.ID == UWObjectMechanics.PitTrapId)
			{
				(int liPitX, int liPitY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

				return fFirePitTrap(pOTrap, liPitX, liPitY, pIHost);
			}

			if (pOTrap.ID == UWObjectMechanics.TextStringTrapId)
			{
				fFireTextTrap(pOTrap, pIHost);
				return true;
			}

			return false;
		}

		/// <summary>a_teleport trap - the same number the level loader's transition scan
		/// uses (UWLevelLoader.TeleportTrapId).</summary>
		public const int TeleportTrapId = 0x0181;

		/// <summary>
		/// Fires a create-object trap WITHOUT a trigger in front of it - for the
		/// periodic respawn (see UWCreatureRespawner).
		///
		/// Until 2026-09-06 a tile volume hung here instead that fired on ENTERING.
		/// That does not exist in the original: a trap in a tile fires there exclusively
		/// via a trigger. Cross-check against the data: of the game's 72 create-object traps,
		/// EVERY one with a set flags field has a trigger pointing at it (usually an a_move
		/// trigger on a neighbouring tile), and EVERY one with flags field zero has none - those are
		/// exactly the ones the respawn addresses.
		///
		/// Deliberately enabled only for the a_create object trap.
		/// </summary>
		/// <summary>
		/// A disarmable trap set off by a bumbled attempt (DefuseTrap_sub_8E27D on a critical
		/// failure): a trap hanging directly on the item runs as RunTrap_ovr153_24A does, with the
		/// player as the one who set it off and the item's tile as the target; with a trigger in
		/// between, the trigger fires as usual. Until 2026-09-30 ours handed it to FireTrapInTile,
		/// which runs create-object traps only - the poison of the green potion on level 2, 14/51,
		/// never reached the player (per user).
		/// </summary>
		public static bool SetOffDisarmableTrap(UWObject pOTrap, UWObject pOTrigger, UWObject pOItem,
			int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			if (pOTrap == null || pIHost == null || pIHost.CurrentLevel == null)
				return false;

			if (pOTrigger != null)
				return FireTraps(pOTrigger, pOItem, pIHost);

			// Only 0x180-0x182 are disarmable; the arrow trap alone needs a target tile.
			bool lbFired = pOTrap.ID == UWObjectMechanics.ArrowTrapId
				? pIHost.FireArrowTrap(pOTrap, piTileX, piTileY)
				: fRunTrap(pOTrap, null, pOItem, pIHost);

			// SET OFF, IT IS GONE: after RunTrap the original calls stub153_66 on the item's chain
			// and the trap, and the potion never asks again (per user on the original,
			// 2026-09-30). A save of the original taken right after shows what it does: the
			// potion's special link is 0 and the trap's slot (859) is on the static free list.
			UWTrapChainRemoval.RemoveTrapFromChain(pOItem, pOTrap, pIHost.CurrentLevel);

			return lbFired;
		}

		public static bool FireTrapInTile(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			if (pOTrap == null || pIHost == null || pIHost.CurrentLevel == null
				|| pOTrap.ID != UWObjectMechanics.CreateObjectTrapId)
				return false;

			return fFireCreateObjectTrap(pOTrap, piTileX, piTileY, pIHost);
		}

		/// <summary>The creature ids, major class 1 - the templates the crowding check applies
		/// to.</summary>
		private const int FirstCreatureId = 0x40;

		private const int LastCreatureId = 0x7F;

		/// <summary>The crowding check around a creature template: four tiles back and five
		/// forward - see fFireCreateObjectTrap.</summary>
		private const int CrowdTilesBack = 4;

		private const int CrowdTilesForward = 5;

		/// <summary>a_create object trap: creates a copy of the object the sp_link
		/// points to. Per uw-formats.txt only if a random number between 0 and 0x3F is GREATER
		/// than "quality" - so quality is an inverse probability.
		///
		/// On level 1 these are five monster generators: bat 47/4 (quality 31),
		/// giant rat 3/17 (40), giant spider 14/7 (50), flesh slug 27/34 (45) and the
		/// goblin 3/33 (0), the latter being the only one fired via a move trigger on 3/36.</summary>
		private static bool fFireCreateObjectTrap(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			UWObject lOTemplate = UWObjectMechanics.GetLinkedObject(pOTrap, pIHost.CurrentLevel.Masterlist);

			if (lOTemplate == null)
				return false;

			if (!pIHost.AlwaysFireCreateObjectTraps && UWRandom.Next(0x40) <= pOTrap.Quality)
				return true;

			// NOT WHILE ONE OF ITS KIND IS STILL ABOUT: for a creature template the original runs
			// CheckForBitDInArea_ovr153_148E around the TEMPLATE (Traps_ovr153_296 labels 70F-721),
			// which asks every creature within four tiles of it for bit 8 of word 0x0D - the mark
			// a respawned creature carries - and then creates nothing. The area routine includes
			// its far end, so it is four back and five forward; the template itself does not count.
			// Until 2026-09-23 the respawn held its whole pass back instead, looking around the
			// player (UWRespawnRules).
			if (lOTemplate.ID >= FirstCreatureId && lOTemplate.ID <= LastCreatureId
				&& pIHost.IsSpawnedCreatureNear(lOTemplate.TileX, lOTemplate.TileY, CrowdTilesBack, CrowdTilesForward))
				return true;

			UWObject lOCopy = UWObject.Clone(lOTemplate);

			if (lOCopy == null)
				return false;

			// Remember that this creature was only created during play - the respawn uses this
			// to count whether enough are already roaming nearby (see UWCreatureRespawner).
			lOCopy.WasSpawned = true;

			// With position and height of the template, like UW.EXE - see SpawnObjectInTileAtTemplatePosition.
			return pIHost.SpawnObjectInTileAtTemplatePosition(lOCopy, piTileX, piTileY);
		}

		/// <summary>a_text string trap: outputs a level-specific message from string block 9
		/// (index see UWObjectMechanics.GetTextTrapStringIndex).
		///
		/// Both text traps on level 1 hang on a regular trigger chain:
		/// - tile 34/1 (solid tile) &lt;- a_use trigger &lt;- both halves of the double door on
		///   31/1 and 32/1: "The doors are securely locked."
		/// - tile 58/13 &lt;- a_look trigger &lt;- the orb on the same tile: the
		///   crystal ball vision, directly after "You see an orb.".</summary>
		private static void fFireTextTrap(UWObject pOTrap, IUWTrapHost pIHost)
		{
			if (!pIHost.HasMessages || pIHost.Data == null)
				return;

			try
			{
				string lsMessage = pIHost.Data.GetLevelMessage(
					UWObjectMechanics.GetTextTrapStringIndex(pOTrap, pIHost.CurrentLevelIndex));

				// Only trim the TRAILING line break that the entries in block 9 carry
				// throughout - otherwise an empty line trails the message in the log
				// (noticed by the user). Inner line breaks are kept, some of the texts are
				// multi-line; an earlier attempt removed all of them and squeezed the text
				// into one line.
				if (!string.IsNullOrWhiteSpace(lsMessage))
					pIHost.AddMessage(lsMessage.TrimEnd('\r', '\n'));
			}
			catch
			{
				// As everywhere else with string access: a missing entry is not an error.
			}
		}

		/// <summary>Original (verified live by the user, 2026-08-28, first assumption corrected):
		/// an a_lever with a_do trap action 3 does NOT raise its own tile but a
		/// target tile - via the same trigger target coordinate convention as the door trap
		/// (GetTriggerTargetTile). The lever itself only advances its own visual position
		/// (see UWHeightLever). ONLY here (fired by actual player use) is
		/// the target tile height ever set - during level build it deliberately stays at its
		/// original static height, which does not match the lever's start position (confirmed by user,
		/// level 1 - not a bug, do not accidentally "fix" it).</summary>
		/// <summary>Cutscene 3, the freeing of Arial (ArialTalking_ovr107_1373).</summary>
		private const int ArialCutscene = 3;

		/// <summary>
		/// a_do trap 41, the exploding book - the same routine as opening Bronus' book, see
		/// UWExplodingBook (see ExplodingBook_ovr107_1259). No level holds this trap.
		///
		/// The routine ignores the trap and searches the PLAYER's tile, containers included, for
		/// the book 0x114 - ours looks in the inventory first, then on the trap's tile (the
		/// player stands on it when it fires). Until 2026-10-01 ours took any object of class 4
		/// minor 1 (orb rock, incense, stew ...) and said "The book explodes!".
		/// </summary>
		private static bool fFireExplodingBookTrap(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWInventoryModel lOInventory = pIHost.Inventory;
			UWObject lOBook = UWExplodingBook.FindCarried(lOInventory);
			bool lbCarried = lOBook != null;

			if (!lbCarried)
				lOBook = fFindExplodingBook(pOTrap.TileX, pOTrap.TileY, pIHost);

			if (lOBook == null)
				return false;

			if (pIHost.HasMessages)
				pIHost.AddMessage(UWExplodingBook.Message);

			UWQuestFlags.Set(UWExplodingBook.QuestFlag, 1);
			pIHost.CursePlayer(UWExplodingBook.CurseDice);

			if (lbCarried)
			{
				lOInventory.RemoveItem(lOBook);
				lOInventory.NotifyChanged();
			}
			else
				pIHost.RemoveObjectFromWorld(lOBook);

			return true;
		}

		/// <summary>The first book 0x114 lying on a tile.</summary>
		private static UWObject fFindExplodingBook(int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.CurrentLevel;

			if (lOLevel == null || lOLevel.TileData == null
				|| piTileX < 0 || piTileX >= UWWorldScale.TilesPerAxis
				|| piTileY < 0 || piTileY >= UWWorldScale.TilesPerAxis)
				return null;

			UWTile lOTile = lOLevel.TileData[(piTileY * UWWorldScale.TilesPerAxis) + piTileX];

			if (lOTile == null || lOTile.ObjectsInTile == null)
				return null;

			foreach (UWObject lOObject in lOTile.ObjectsInTile)
			{
				if (lOObject != null && lOObject.ID == UWExplodingBook.BookObjectId)
					return lOObject;
			}

			return null;
		}

		/// <summary>
		/// a_do traps 60 to 62, the quake (QuakeTrap_seg008_DE7): quality minus 59 is a bit
		/// mask - bit 0 shakes the screen, bit 1 bounces the player. The owner field is handed
		/// to both, but only the bounce uses it; the shake is fixed (read 2026-09-25, see the
		/// host). No quake trap in the nine levels can fire: 60 and 61 do not occur, the one
		/// 62 is an unlinked leftover record.
		/// </summary>
		private static bool fFireQuakeTrap(UWObject pOTrap, IUWTrapHost pIHost)
		{
			int liKind = pOTrap.Quality - UWObjectMechanics.DoTrapQuakeBase;

			if ((liKind & 1) != 0)
				pIHost.ShakeScreen(pOTrap.Owner);

			if ((liKind & 2) != 0)
				pIHost.BouncePlayer(pOTrap.Owner);

			return true;
		}

		private static void fAdvanceHeightLever(UWObject pOSwitchObject, UWObject pOTrigger, int piStepSize, IUWTrapHost pIHost)
		{
			(int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

			UWTile lOTargetTile = pIHost.CurrentLevel?.GetTile(liTileX, liTileY);

			if (lOTargetTile == null || pOSwitchObject == null)
				return;

			// The lever already advanced on use (see fTryFireTriggerChain) -
			// here its position is merely converted into a tile height.
			if (!pIHost.TryGetLeverTileHeight(pOSwitchObject, piStepSize, out int liHeight))
				return;

			lOTargetTile.FloorHeight = (ushort)liHeight;
			pIHost.RebuildGeometry();
		}

		/// <summary>A tile of the level, or null off the map.</summary>
		/// <summary>
		/// a_change terrain trap: turns solid rock into floor - this is how the
		/// original's secret passages work.
		///
		/// VERIFIED IN THE ORIGINAL (user, 2026-09-06) on level 3, tile 48/55: there is a
		/// fake wall (object 366, a surface with a wall texture). It hangs on a
		/// look trigger with search check 10, behind it this trap with target tile 48/54.
		/// That tile is solid and contains a secret door (327). After firing it becomes
		/// walkable and the door is exposed - observed exactly like that in the original.
		///
		/// The fields, all from the reference:
		///
		///   tile type      (heading &lt;&lt; 1) + (quality &amp; 1)
		///   floor height   zpos &gt;&gt; 3
		///   floor texture  quality &gt;&gt; 1, only 0 to 9 are valid
		///   wall texture   owner, only 0 to 47 are valid
		///   area           from the target tile, xpos tiles further east and ypos
		///                  north - both usually zero, i.e. exactly one tile
		///
		/// The two texture fields carry 63 and 11 respectively on most traps, i.e.
		/// deliberately invalid values: the tile keeps its appearance and only becomes walkable.
		///
		/// UNCERTAIN is the HEADING's share in the tile type. The reference includes it,
		/// but doubts it itself in a commented-out predecessor ("heading seems to
		/// have an impact but not in game"). Of the 33 traps in the game, four - all on
		/// level 4 - carry a heading of 5 and would thus get tile type 10, which does not
		/// exist in uw1 at all. For such values we leave the type unchanged; height and textures still
		/// apply. To be checked once level 4 is up.
		///
		/// Objects in a solid tile were skipped during level build and
		/// are spawned late as soon as it becomes walkable (see SpawnPendingObjectsInTile).
		/// </summary>
		private static bool fFireChangeTerrainTrap(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.CurrentLevel;

			if (lOLevel == null || lOLevel.TileData == null)
				return false;

			int liNewType = (pOTrap.Heading << 1) + (pOTrap.Quality & 1);
			int liNewHeight = pOTrap.ZPos >> 3;
			int liFloorTexture = pOTrap.Quality >> 1;
			int liWallTexture = pOTrap.Owner;

			bool lbChanged = false;

			for (int liX = piTileX; liX <= piTileX + pOTrap.XPos; liX++)
			{
				for (int liY = piTileY; liY <= piTileY + pOTrap.YPos; liY++)
				{
					UWTile lOTile = lOLevel?.GetTile(liX, liY);

					if (lOTile == null)
						continue;

					bool lbWasSolid = lOTile.TileType == UWTile.TileTypeEnum.solid;

					if (liNewType <= (int)UWTile.TileTypeEnum.slope_w)
					{
						lOTile.TileType = (UWTile.TileTypeEnum)liNewType;

						// As in UWTile's tile header: only slopes have a slope value.
						lOTile.Slope = (byte)(fIsSlope(lOTile.TileType) ? 16 : 0);
					}

					// Our floor height is already in world units - one height step
					// is sixteen of them (see UWTile).
					if (liNewHeight >= 0 && liNewHeight <= 14)
						lOTile.FloorHeight = (ushort)(liNewHeight << 4);

					// Via the slots, not the texture values - that way they also end up in the save game
					// (see UWTile.SetFloorTexture).
					if (lOLevel.TextureInfos != null && liFloorTexture < FloorTextureCount)
						lOTile.SetFloorTexture(liFloorTexture, lOLevel.TextureInfos);

					if (lOLevel.TextureInfos != null && liWallTexture < WallTextureCount)
						lOTile.SetWallTexture(liWallTexture, lOLevel.TextureInfos);

					lbChanged = true;

					// Whatever was inside the wall had never been drawn.
					if (lbWasSolid && lOTile.TileType != UWTile.TileTypeEnum.solid)
						pIHost.SpawnPendingObjectsInTile(liX, liY);
				}
			}

			if (lbChanged)
				pIHost.RebuildGeometry();

			return lbChanged;
		}

		/// <summary>
		/// a_pit trap: toggles the target tile between height zero and a preset height.
		/// If it is down, it is raised and gets the texture from Owner; if it is up,
		/// it drops to zero and gets the one from Quality - the texture of the pit floor.
		///
		/// From height 15 the tile counts as solid, below that as walkable. The reference does
		/// it the same way (a_pit_trap.Activate); it also shows that the height is NOT in ZPos
		/// but in YPos and XPos - see UWObjectMechanics.GetPitTrapHeight.
		///
		/// IN UW1 THIS TRAP HAS NO EFFECT. There is exactly one, on level 3 at 6/4, and its
		/// preset height is zero - so it toggles between zero and zero. It is built
		/// anyway so the chain does not break at it.
		/// </summary>
		private static bool fFirePitTrap(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.CurrentLevel;
			UWTile lOTile = lOLevel?.GetTile(piTileX, piTileY);

			if (lOTile == null)
				return false;

			bool lbIsDown = lOTile.FloorHeight == 0;

			int liNewHeight = lbIsDown ? UWObjectMechanics.GetPitTrapHeight(pOTrap) : 0;
			int liTexture = lbIsDown ? pOTrap.Owner : pOTrap.Quality;

			bool lbWasSolid = lOTile.TileType == UWTile.TileTypeEnum.solid;

			lOTile.TileType = liNewHeight < SolidFromHeight
				? UWTile.TileTypeEnum.open : UWTile.TileTypeEnum.solid;

			lOTile.Slope = 0;

			// Our floor heights are in world units, one height step is sixteen.
			lOTile.FloorHeight = (ushort)(liNewHeight << 4);

			if (lOLevel.TextureInfos != null && liTexture < FloorTextureCount)
				lOTile.SetFloorTexture(liTexture, lOLevel.TextureInfos);

			if (lbWasSolid && lOTile.TileType != UWTile.TileTypeEnum.solid)
				pIHost.SpawnPendingObjectsInTile(piTileX, piTileY);

			pIHost.RebuildGeometry();

			return true;
		}

		/// <summary>From this height a tile counts as solid - that is how the reference computes it in
		/// the pit trap.</summary>
		private const int SolidFromHeight = 15;

		// ------------------------------------------------- further a_do trap actions

		/// <summary>String block 1 in OUR numbering, each one above the reference: 192 "There is
		/// a pained whining sound.", 193 "There is an empty clicking sound.", 194 "A voice utters
		/// the words 'Reset Activated.'"</summary>
		private const int BullfrogWrongLevelMessage = 192;

		private const int BullfrogOutOfTriesMessage = 193;

		private const int BullfrogResetMessage = 194;

		/// <summary>
		/// a_do trap 5: trespassing.
		///
		/// Owner names the race that feels disturbed - for the storeroom on level 1 the
		/// goblins. Every member of that race in range loses one step of goodwill; whoever reaches
		/// zero turns hostile (see UWCritter.Anger).
		///
		/// THE SEARCH AREA IS CENTRED ON THE PLAYER, not on the trap - in both cases
		/// that lies in a solid tile. The reference runs from tile minus seven
		/// to tile plus EIGHT, so the area is shifted one tile to the north-east. That
		/// looks like an off-by-one in the original, but it is verifiably so - and makes
		/// a difference for the goblins on level 1. Read in the disassembly 2026-09-23: the
		/// area routine's loops include their far end, and the theft uses the very same area
		/// (UWCritterRules.TheftAreaBack/Forward). TrespassTrap_ovr107_11B9 hands ovr104_E4C the
		/// PLAYER object, so the owner cleared afterwards is the player's, which is zero anyway -
		/// the trap fires every time, it is not used up.
		///
		/// SIGHT RANGE AND LINE OF SIGHT are checked as well since 2026-09-16, the same way as for
		/// theft (UWCritter.CanSeeTheftAt) - in the reference both go through the same routine.
		/// The search over the creature bodies is the host's (IUWTrapHost.AngerRaceNearPlayer).
		/// </summary>
		private static bool fFireTrespassTrap(UWObject pOTrap, IUWTrapHost pIHost)
		{
			int liOwner = pOTrap.Owner;

			if (liOwner == 0 || !pIHost.HasMessages)
				return true;

			// Who minds it and what he says as for a theft - one routine in the original
			// (UWCritterRules.MindsTheft); the message follows the new attitude since 2026-09-23,
			// it was a fixed "is annoyed" here.
			int liAngered = pIHost.AngerRaceNearPlayer(liOwner, TrespassBack, TrespassForward);

			UWTrapLog.Detail = string.Format("-> owner {0}, {1} angered", liOwner, liAngered);

			return true;
		}

		/// <summary>See fFireTrespassTrap: the search area reaches seven tiles back and eight
		/// forward.</summary>
		private const int TrespassBack = UWCritterRules.TheftAreaBack;

		private const int TrespassForward = UWCritterRules.TheftAreaForward;

		/// <summary>
		/// a_do trap 2: the view jumps to the trap and stays there while the player holds the
		/// look that set it off - the right mouse button on the orb (see UWRemoteCamera).
		///
		/// POSITION AND VIEW DIRECTION ARE STORED IN THE TRAP ITSELF - tile, sub-tile position,
		/// zpos and heading. The reference reads only these fields and never those of the
		/// trigger. NO eye height offset: it adds that only when the camera is attached to an
		/// object, and here it is attached to nothing.
		///
		/// The two instances in uw1 therefore end up at:
		///
		///   level 2, tile 9/34, zpos 116, heading 0  ->  (544, 232, 2144), facing north
		///   level 4, tile 5/55, zpos 112, heading 2  ->  (328, 224, 3536), facing east
		///
		/// The first is also the best test of the whole conversion: in front of the camera is
		/// tile 9/35, and there lies the only moonstone in the world (see UWMiscSpell). If
		/// you see it, axes, tile origin and angle are all correct at a glance.
		///
		/// THE WORLD RUNS ON meanwhile (per user on the original, 2026-09-24): the picture shows
		/// only while the right mouse button is held, and the creatures move - at the player as at
		/// the camera's spot. Both triggers of UW1 are look triggers on an orb on a pedestal
		/// (level 2 tile 55/50, level 4 tile 54/45). A first reading the same day had frozen the
		/// world and was reverted.
		/// </summary>
		private static bool fFireCameraTrap(UWObject pOTrap, UWObject pOTrigger, IUWTrapHost pIHost)
		{
			int liTileX = pOTrap.TileX;
			int liTileY = pOTrap.TileY;

			// TileX/TileY are only set when reading the tile lists. If the trap hangs only on the
			// special link of a trigger, they would be 0/0 and the camera would end up in rock.
			// DEVIATION from the reference, which reads only its own fields - here
			// as an emergency exit so nothing silently points into the void.
			if (liTileX == 0 && liTileY == 0)
			{
				(liTileX, liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

				UWTrapLog.Detail = "-> camera (tile borrowed from trigger)";
			}
			else
			{
				UWTrapLog.Detail = string.Format("-> camera at {0}/{1}, zpos {2}, facing {3}",
					liTileX, liTileY, pOTrap.ZPos, pOTrap.Heading);
			}

			return pIHost.FireCameraTrap(pOTrap, liTileX, liTileY);
		}

		/// <summary>
		/// a_do trap 24: the Bullfrog puzzle on level 4.
		///
		/// Four traps make it up, all with the same quality and a different owner: 0 raises,
		/// 1 lowers, 2 and 3 advance the selected tile in Y and X respectively. Owner 4
		/// (reset: flattens the field and sets the tries to 0x3F) is handled as well. The
		/// state lives entirely in game variables 24, 25 and 26 - the traps themselves
		/// carry nothing apart from the owner.
		///
		/// A block of at most three by three tiles around the selected one is raised or
		/// lowered, and twice on the centre: the target tile therefore moves by two
		/// steps, its neighbours by one. At the edge of the field the block is clipped so that
		/// nothing outside 48..55 is ever touched.
		///
		/// ON ANOTHER LEVEL NOTHING HAPPENS except a message - that is how the
		/// reference has it, and the four traps only exist on level 4 anyway. The one other
		/// way in is the wand of the Frog (FireBullfrog, item spell 13/3 with mode 4), which
		/// can be carried away: used elsewhere it gives the same message.
		///
		/// THE TRIES COME FROM THE SAVE GAME. The original sets variable 26 of a fresh
		/// character to 53 - the only game variable it presets at all. Our character creation
		/// does the same (see UWPlayerData.BuildNewCharacter); a loaded save game brings its
		/// own value, and in all four of the user's saves there is a sensible value there
		/// (53 or 25 respectively). A fallback value here would therefore only be guesswork
		/// and is left out.
		/// </summary>
		public static void FireBullfrog(int piMode, IUWTrapHost pIHost)
		{
			if (pIHost != null)
				fFireBullfrogTrap(piMode, pIHost);
		}

		private static bool fFireBullfrogTrap(int piMode, IUWTrapHost pIHost)
		{
			if (!pIHost.HasMessages)
				return true;

			if (pIHost.CurrentLevelIndex != UWObjectMechanics.BullfrogLevelIndex)
			{
				pIHost.AddGeneralMessage(BullfrogWrongLevelMessage);

				UWTrapLog.Detail = "-> wrong level";

				return true;
			}

			// The two counting actions do not cost a try.
			if (piMode == 2 || piMode == 3)
			{
				int liVariable = piMode == 2 ? UWObjectMechanics.BullfrogVarY : UWObjectMechanics.BullfrogVarX;

				UWGameVariables.Set(liVariable, (UWGameVariables.Get(liVariable) + 1) & 7);

				UWTrapLog.Detail = string.Format("-> selection {0}/{1}",
					UWGameVariables.Get(UWObjectMechanics.BullfrogVarX),
					UWGameVariables.Get(UWObjectMechanics.BullfrogVarY));

				return true;
			}

			if (piMode == 4)
			{
				pIHost.AddGeneralMessage(BullfrogResetMessage);

				UWGameVariables.Set(UWObjectMechanics.BullfrogVarRetries, UWObjectMechanics.BullfrogRetriesOnReset);

				fFlattenBullfrogGrid(pIHost);

				UWTrapLog.Detail = "-> reset";

				return true;
			}

			if (piMode != 0 && piMode != 1)
				return true;

			int liRetries = UWGameVariables.Get(UWObjectMechanics.BullfrogVarRetries);

			if (liRetries <= 1)
			{
				pIHost.AddGeneralMessage(BullfrogOutOfTriesMessage);

				UWGameVariables.Set(UWObjectMechanics.BullfrogVarRetries, 1);

				UWTrapLog.Detail = "-> no tries left";

				return true;
			}

			UWGameVariables.Set(UWObjectMechanics.BullfrogVarRetries, liRetries - 1);

			int liChoiceX = UWGameVariables.Get(UWObjectMechanics.BullfrogVarX);
			int liChoiceY = UWGameVariables.Get(UWObjectMechanics.BullfrogVarY);

			int liCentreX = UWObjectMechanics.BullfrogGridOrigin + liChoiceX;
			int liCentreY = UWObjectMechanics.BullfrogGridOrigin + liChoiceY;

			int liDelta = piMode == 0 ? 1 : -1;

			// The edge of the field clips the block.
			int liCornerX = liChoiceX == 0 ? liCentreX : liCentreX - 1;
			int liSpanX = (liChoiceX == 0 || liChoiceX == UWObjectMechanics.BullfrogGridSize - 1) ? 1 : 2;

			int liCornerY = liChoiceY == 0 ? liCentreY : liCentreY - 1;
			int liSpanY = (liChoiceY == 0 || liChoiceY == UWObjectMechanics.BullfrogGridSize - 1) ? 1 : 2;

			fAdjustBullfrogTiles(liCornerX, liCornerY, liSpanX, liSpanY, liDelta, pIHost.CurrentLevel);
			fAdjustBullfrogTiles(liCentreX, liCentreY, 0, 0, liDelta, pIHost.CurrentLevel);

			pIHost.RebuildGeometry();

			UWTrapLog.Detail = string.Format("-> target {0}/{1}, height {2}{3}, left {4}",
				liCentreX, liCentreY, liDelta > 0 ? "+" : string.Empty, liDelta, liRetries - 1);

			return true;
		}

		/// <summary>Raises or lowers a tile rectangle by one step. The bounds are
		/// inclusive, as in the reference and as in fFireChangeTerrainTrap.</summary>
		private static void fAdjustBullfrogTiles(int piTileX, int piTileY, int piSpanX, int piSpanY,
			int piDelta, UWLevel pOLevel)
		{
			if (pOLevel == null || pOLevel.TileData == null)
				return;

			for (int liX = piTileX; liX <= piTileX + piSpanX; liX++)
			{
				for (int liY = piTileY; liY <= piTileY + piSpanY; liY++)
				{
					UWTile lOTile = pOLevel?.GetTile(liX, liY);

					if (lOTile == null)
						continue;

					int liHeight = (lOTile.FloorHeight >> 4) + piDelta;

					if (liHeight < 0 || liHeight > 14)
						continue;

					lOTile.FloorHeight = (ushort)(liHeight << 4);
				}
			}
		}

		/// <summary>Flattens the whole Bullfrog field back to height four - exactly the state
		/// it has in the level data.</summary>
		private static void fFlattenBullfrogGrid(IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.CurrentLevel;

			if (lOLevel == null || lOLevel.TileData == null)
				return;

			for (int liX = 0; liX < UWObjectMechanics.BullfrogGridSize; liX++)
			{
				for (int liY = 0; liY < UWObjectMechanics.BullfrogGridSize; liY++)
				{
					UWTile lOTile = lOLevel?.GetTile(UWObjectMechanics.BullfrogGridOrigin + liX, UWObjectMechanics.BullfrogGridOrigin + liY);

					if (lOTile != null)
						lOTile.FloorHeight = BullfrogResetHeight << 4;
				}
			}

			pIHost.RebuildGeometry();
		}

		/// <summary>Height the reset puts the Bullfrog field at.</summary>
		private const ushort BullfrogResetHeight = 4;

		/// <summary>
		/// a_do trap 40: the emerald puzzle on level 6.
		///
		/// Four pedestals lie in a square around the trigger tile, four tiles away on both axes.
		/// If an emerald lies on each, all four disappear and a Vas rune stone appears
		/// one tile behind the button. Otherwise simply nothing happens - the reference outputs
		/// neither text nor sound for that.
		///
		/// ONLY THE TILE LIST IS SEARCHED, not containers on it: the reference explicitly
		/// takes only the top-level chain.
		/// </summary>
		private static bool fFireEmeraldPuzzleTrap(int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			UWLevel lOLevel = pIHost.CurrentLevel;

			if (lOLevel == null || lOLevel.TileData == null)
				return true;

			List<UWObject> lOFound = new List<UWObject>();

			int liOffset = UWObjectMechanics.EmeraldPuzzleOffset;

			for (int liX = -liOffset; liX <= liOffset; liX += liOffset * 2)
			{
				for (int liY = -liOffset; liY <= liOffset; liY += liOffset * 2)
				{
					UWTile lOTile = lOLevel?.GetTile(piTileX + liX, piTileY + liY);

					if (lOTile == null || lOTile.ObjectsInTile == null)
						continue;

					foreach (UWObject lOAt in lOTile.ObjectsInTile)
					{
						if (lOAt == null || lOAt.ID != UWObjectMechanics.EmeraldObjectId)
							continue;

						lOFound.Add(lOAt);

						break;
					}
				}
			}

			if (lOFound.Count < EmeraldPuzzleCount)
			{
				UWTrapLog.Detail = string.Format("-> {0} of {1} emeralds", lOFound.Count, EmeraldPuzzleCount);

				return true;
			}

			foreach (UWObject lOEmerald in lOFound)
				pIHost.RemoveObjectFromWorld(lOEmerald);

			pIHost.SpawnObjectById(
				UWPlayerData.FirstRuneObjectId + (int)UWPlayerData.Rune.Vas, piTileX, piTileY + 1);

			UWTrapLog.Detail = "-> Vas rune stone";

			return true;
		}

		/// <summary>This many emeralds the puzzle requires.</summary>
		private const int EmeraldPuzzleCount = 4;

		/// <summary>
		/// a_do trap 50: starts a conversation.
		///
		/// WITH WHOM is in none of the trap's fields - all are zero. The number is
		/// hard-wired in the original: object slot 251 of the current level. On level 7 that is the
		/// troll guard on tile 26/56, three tiles from the move trigger that fires the trap,
		/// and his first line is "Ho ho, just like big rat in trap."
		/// </summary>
		private static bool fFireConversationTrap(IUWTrapHost pIHost)
		{
			if (!pIHost.HasMessages || pIHost.CurrentLevel == null)
				return false;

			List<UWObject> lOMaster = pIHost.CurrentLevel.Masterlist;

			int liAt = UWObjectMechanics.ConversationTrapNpcIndex;

			if (lOMaster == null || liAt <= 0 || liAt >= lOMaster.Count)
				return false;

			UWTrapLog.Detail = string.Format("-> conversation with object {0}", liAt);

			return pIHost.TalkTo(lOMaster[liAt]);
		}

		/// <summary>This many floor textures a level has. Where they start in UWLevel.TextureInfos
		/// is known by UWTile.SetFloorTexture.</summary>
		private const int FloorTextureCount = 10;

		/// <summary>This many wall textures a level has, from entry zero.</summary>
		private const int WallTextureCount = 48;

		/// <summary>Spell class of the cutscenes. The minor class is the number of the
		/// sequence - that is how the reference calls them (spellcasting: "case 14: cutscene
		/// spells").</summary>
		public const int CutsceneMajorClass = 14;

		/// <summary>Spell class of the projectiles.</summary>
		public const int ProjectileMajorClass = UWSpellCasting.ProjectileMajorClass;

		/// <summary>
		/// a_spelltrap: casts a spell. Quality is the major class, Owner the minor class.
		///
		/// Two special paths compared to ordinary casting, both on the host side:
		///
		///   Class 14 is the CUTSCENES - the minor class is the number. That is exactly
		///   the trap on level 7 at 55/6 (14/3).
		///
		///   Class 5 is PROJECTILES, and when cast they wait for the click that gives the
		///   direction. A trap has no click: its projectile starts at its own
		///   sub-tile position and flies toward the player. The reference does the same, passing
		///   the trap as the thrower and the player as the target. That is the trap on
		///   level 5 at 10/45 (5/3).
		///
		/// Everything else takes the normal path and affects the player character.
		/// </summary>
		private static bool fFireSpellTrap(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			int liMajor = pOTrap.Quality;
			int liMinor = pOTrap.Owner;

			UWTrapLog.Detail = string.Format("spell {0}/{1}", liMajor, liMinor);

			return pIHost.FireSpellTrap(pOTrap, piTileX, piTileY, liMajor, liMinor);
		}

		/// <summary>
		/// an_inventory trap: does the player carry the item being searched for?
		///
		/// The search covers the backpack, the equipment, the cursor and all containers
		/// in them. ZPos is a minimum count; if it is zero, one piece is enough.
		///
		/// DEVIATION: the reference compares major, minor and class index separately, which
		/// amounts to the same as comparing the whole object number - it only splits it
		/// because its search function requires that.
		/// </summary>
		private static bool fInventoryTrapMatches(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWInventoryModel lOInventory = pIHost.Inventory;

			if (lOInventory == null)
				return false;

			int liWanted = UWObjectMechanics.GetInventoryTrapItemId(pOTrap);
			int liNeeded = pOTrap.ZPos > 0 ? pOTrap.ZPos : 1;

			List<UWObject> lOItems = new List<UWObject>();

			lOItems.AddRange(lOInventory.Backpack);
			lOItems.AddRange(lOInventory.EquipSlots);
			lOItems.Add(lOInventory.CursorItem);

			bool lbFound = fCarriesItem(lOItems, liWanted, liNeeded,
				pIHost.CurrentLevel != null ? pIHost.CurrentLevel.Masterlist : null, 0);

			UWTrapLog.Detail = string.Format("object {0} x{1}: {2}", liWanted, liNeeded,
				lbFound ? "present" : "missing");

			return lbFound;
		}

		/// <summary>Searches for an object number in a list and in all containers in it.</summary>
		private static bool fCarriesItem(IList<UWObject> pOItems,
			int piWantedId, int piNeeded, List<UWObject> pOMasterlist, int piDepth)
		{
			if (pOItems == null || piDepth > MaxTrapChainDepth)
				return false;

			foreach (UWObject lOItem in pOItems)
			{
				if (lOItem == null)
					continue;

				if (lOItem.ID == piWantedId
					&& (!lOItem.HasQuantity || lOItem.Quantity >= piNeeded))
					return true;

				if (lOItem.GetCategory() != UWObject.ObjectCategoryEnum.Containers || pOMasterlist == null)
					continue;

				lOItem.EnsureContentsLoaded(pOMasterlist);

				if (fCarriesItem(lOItem.Contents, piWantedId, piNeeded, pOMasterlist, piDepth + 1))
					return true;
			}

			return false;
		}

		private static bool fIsSlope(UWTile.TileTypeEnum peType)
		{
			return peType == UWTile.TileTypeEnum.slope_n || peType == UWTile.TileTypeEnum.slope_s
				|| peType == UWTile.TileTypeEnum.slope_e || peType == UWTile.TileTypeEnum.slope_w;
		}

		/// <summary>
		/// a_delete object trap: removes ONE object from the world. Which one is in the sp_link;
		/// the tile in quality and owner of the trap (not of the trigger).
		///
		/// It forms the second half of a secret passage: first the terrain trap opens the
		/// tile behind, then this one removes the fake wall. On level 3 its
		/// sp_link points exactly at the fake wall object on 48/55.
		/// </summary>
		private static bool fFireDeleteObjectTrap(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWObject lOVictim = UWObjectMechanics.GetLinkedObject(pOTrap, pIHost.CurrentLevel.Masterlist);

			return lOVictim != null && pIHost.RemoveObjectFromWorld(lOVictim);
		}

		/// <summary>The first door object of a tile, or null.</summary>
		public static UWObject FindDoorInTile(int piTileX, int piTileY, UWLevel pOLevel)
		{
			UWTile lOTile = pOLevel?.GetTile(piTileX, piTileY);

			if (lOTile == null || lOTile.ObjectsInTile == null)
				return null;

			foreach (UWObject lOCandidate in lOTile.ObjectsInTile)
			{
				if (lOCandidate != null && lOCandidate.GetCategory() == UWObject.ObjectCategoryEnum.Doors)
					return lOCandidate;
			}

			return null;
		}

		/// <summary>a_door trap: opens, closes or toggles the door on the TRIGGER'S
		/// TARGET TILE - per uw-formats.txt the coordinates are on the trigger, not
		/// on the trap. "quality" determines the action (1 open, 2 close, 3 toggle; any other value also toggles).
		///
		/// Then there is the lock side: if the door has a lock, it is REMOVED; if it has
		/// none, one is created from the template the trap's sp_link points to. On
		/// level 1 all four traps chained like this point to an a_lock (271). The door and
		/// its lock are components, so the host does that part (IUWTrapHost.FireDoorTrap).</summary>
		private static void fFireDoorTrap(UWObject pOTrigger, UWObject pOTrap, IUWTrapHost pIHost)
		{
			(int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

			UWObject lODoorData = FindDoorInTile(liTileX, liTileY, pIHost.CurrentLevel);

			if (lODoorData == null)
				return;

			pIHost.FireDoorTrap(lODoorData, pOTrap.Quality,
				UWObjectMechanics.GetLinkedObject(pOTrap, pIHost.CurrentLevel.Masterlist));
		}
	}
}
