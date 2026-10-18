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
	/// arrow, a spell, the remote camera, the creature bodies and levers - goes through
	/// IUWTrapHost; UWTriggerSystem is the Unity facade with the old public API. Since cleanup
	/// step 3.6 (2026-10-17) the level, the player, the messages, the objects and the doors are the
	/// world's (IUWTrapHost.World, see the helpers below), the one who set the firing off a value
	/// (UWTrapFiring).
	///
	/// Trap kinds without an implementation are recorded in the trap log (see fRunTrap)
	/// (NOT BUILT); the chain goes on past them as the original's does (see fRunTrapChain). The
	/// kinds that are handled are the tables Traps and DoActions (UWTrapTable, since 2026-10-14).
	/// </summary>
	public static class UWTrapRules
	{
		/// <summary>Who set a trigger off - the "who" of the original's UseTrigger.</summary>
		public enum Setter
		{
			Player,
			Creature,
			/// <summary>Anything else: a thing in flight, a door broken by a blow (ours does not
			/// keep the attacker), the Gronk conversation (the original passes no object; its
			/// read then lands in the DOS vector table).</summary>
			Thing
		}

		// ------------------------------------------------- The firing's world (cleanup step 3.6)
		//
		// What the trap host answered itself until 2026-10-17, with the host's conventions kept:
		// without a world the level is null, the index 0 (UWWorldLevels.IndexOr 0, the trap host's
		// convention of the four), the count 0 and the player's tile Nowhere's.

		private static UWLevel fLevel(IUWTrapHost pIHost)
		{
			return pIHost.World != null ? pIHost.World.Level : null;
		}

		private static DataImport fData(IUWTrapHost pIHost)
		{
			return pIHost.World != null ? pIHost.World.Data : null;
		}

		/// <summary>The dungeon level of the firing, 0-based, 0 without a world - the trap host's
		/// convention (UWWorldLevels).</summary>
		public static int LevelIndexOf(IUWTrapHost pIHost)
		{
			return UWWorldLevels.IndexOr(pIHost.World, 0);
		}

		private static int fLevelCount(IUWTrapHost pIHost)
		{
			return pIHost.World != null ? pIHost.World.LevelCount : 0;
		}

		/// <summary>A character is there (UWScene.Character, the host's lazy lookup until
		/// 2026-10-17): his numbers are.</summary>
		private static bool fHasPlayer(IUWTrapHost pIHost)
		{
			return pIHost.World != null && pIHost.World.Player.Vitals != null;
		}

		/// <summary>The tile under the eye, as the trespass search takes it; -1/-1 without a view
		/// or a world (UWSpot.Nowhere).</summary>
		private static UWTilePos fPlayerTile(IUWTrapHost pIHost)
		{
			return pIHost.World != null ? pIHost.World.Player.FloorSpot.Tile : UWSpot.Nowhere.Tile;
		}

		/// <summary>The player's inventory, or null without one.</summary>
		private static UWInventoryModel fInventory(IUWTrapHost pIHost)
		{
			return pIHost.World != null ? pIHost.World.Player.Inventory : null;
		}

		/// <summary>The player's damage-type immunities of the moment - worn enchantments and
		/// spells (UWCharacter.DamageTypeProof), the resist byte check_res reads.</summary>
		private static int fDamageTypeProof(IUWTrapHost pIHost)
		{
			return fHasPlayer(pIHost) ? pIHost.World.Player.DamageTypeProof : 0;
		}

		private static int fCastingSkill(IUWTrapHost pIHost)
		{
			return fHasPlayer(pIHost) ? pIHost.World.Player.Vitals.GetSkill(UWPlayerData.Skill.Casting) : 0;
		}

		/// <summary>The damage trap on the player: type 4 as on everything else (WORLDEV.C
		/// whack_thing), untyped until 2026-10-17 - B5 of Docs/CLEANUP-PLAN.md, decided by the
		/// original; the same for the player, whose proof has no bit 4 (UWDamageRequest.Trap).</summary>
		private static void fDamagePlayer(IUWTrapHost pIHost, int piDamage)
		{
			if (fHasPlayer(pIHost))
				pIHost.World.Damage(UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.Trap(piDamage)));
		}

		private static void fPoisonPlayer(IUWTrapHost pIHost, int piStrength)
		{
			if (fHasPlayer(pIHost))
				pIHost.World.Player.Vitals.ApplyPoison(piStrength);
		}

		/// <summary>The class 9 curse on the player: dice plus dice eight-sided, never below
		/// three hit points (UWDamageRequest.Curse). The exploding book uses three dice.</summary>
		private static void fCursePlayer(IUWTrapHost pIHost, int piDice)
		{
			if (pIHost.World != null)
				pIHost.World.Damage(UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.Curse(piDice)));
		}

		/// <summary>Is a creature that was created during play (UWObject.IsSpawned) standing
		/// within piTilesBack below and piTilesForward above this tile on both axes? The crowding
		/// check of a creation trap - see fFireCreateObjectTrap. Through the world's creature search
		/// since cleanup step 3.2 (2026-10-17; before, a scene search on the host), by the BODY's
		/// tile as before; asked here instead of on the host since step 3.6.</summary>
		private static bool fIsSpawnedCreatureNear(IUWTrapHost pIHost, int piTileX, int piTileY, int piTilesBack,
			int piTilesForward)
		{
			if (pIHost.World == null)
				return false;

			return pIHost.World.FindCreatures(UWTileArea.Around(new UWTilePos(piTileX, piTileY), piTilesBack,
				piTilesForward, UWCreatureTile.Body), UWCreatureFilter.Spawned, new List<UWCreatureRef>()) > 0;
		}

		/// <summary>Flags bit 2 of a trigger (word 0 bit 0x800, ID_FLAG11): the player sets it off.</summary>
		public const int PlayerSetsOffFlag = 4;

		/// <summary>Flags bit 3 (word 0 bit 0x1000, ID_ENCHANT): creatures set it off; for a thing
		/// it is the one bit that, without bit 2, keeps it out.</summary>
		public const int OthersSetOffFlag = 8;

		/// <summary>
		/// Whether peSetter may set the trigger off - UseTrigger (TRIGGER.C) for every trigger
		/// kind: the player only with bit 2, a creature only with bit 3, anything else unless bit
		/// 3 is set without bit 2. (The original also refuses a creature on a trigger of major
		/// class MAJOR_RECT, which no trigger is.) The move trigger's scan had this since
		/// 2026-10-06 (UWMotionHost.TriggerMove); the open trigger since 2026-10-12.
		/// </summary>
		public static bool MaySetOff(UWObject pOTrigger, Setter peSetter)
		{
			if (pOTrigger == null)
				return false;

			bool lbPlayerFlag = (pOTrigger.Flags & PlayerSetsOffFlag) != 0;
			bool lbOthersFlag = (pOTrigger.Flags & OthersSetOffFlag) != 0;

			switch (peSetter)
			{
				case Setter.Player:
					return lbPlayerFlag;
				case Setter.Creature:
					return lbOthersFlag;
				default:
					return !lbOthersFlag || lbPlayerFlag;
			}
		}

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
				&& (fLevel(pIHost) == null
					|| UWObjectMechanics.GetLinkedObject(pOObject, fLevel(pIHost).Masterlist) == null))
			{
				pIHost.ToggleSwitchVisual(pOObject);
				lbFired = true;
			}

			return lbFired;
		}

		/// <summary>The buttons and switches, class 0x17 (items 0x170-0x17F, major 5 minor 3).</summary>
		private const int ButtonClassBase = 0x170;

		private const int ClassMask = 0x1F0;

		/// <summary>A button's state is its class index: 8 and up is on (pressed).</summary>
		private const int SwitchOnBit = 0x8;

		/// <summary>
		/// WHAT CLICKS WHEN USED: UseRect (UnderworldExhumed USEITEMS.C) plays sound 0x13 for minor
		/// class 3 alone - the buttons and switches 0x170-0x17F - on every press, whether a trigger
		/// hangs on it or not; a lever (minor 2, 0x161/0x162) only steps its position, other minor 2
		/// objects are looked at, a door plays its own sound (reference audit, traps D7).
		/// </summary>
		public static bool ClicksWhenUsed(int piObjectId)
		{
			return (piObjectId & ClassMask) == ButtonClassBase;
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
			if (pOObject == null || pIHost == null || fLevel(pIHost) == null)
				return false;

			List<UWObject> lOMaster = fLevel(pIHost).Masterlist;

			UWObject lOTrigger = UWObjectMechanics.GetLinkedObject(pOObject, lOMaster);

			// A SWITCH THAT IS ON PASSES THE ACTION ON (UseTrigger, UnderworldExhumed TRIGGER.C): a
			// button of class 0x17 whose state is above 7 hands it to the next trigger of its chain,
			// and that one again while there is a next - so a switch holding two triggers fires the
			// first when it is turned off and the second when it is turned on. A use flips the
			// button BEFORE the trigger is asked (UseRect, then checkTrap); ours flips it below,
			// so the state after this press is taken. No shipped switch holds two triggers (latent,
			// reference audit traps D18; built 2026-10-12).
			bool lbOnNow = (pOObject.ID & SwitchOnBit) != 0;
			bool lbOnWhenAsked = piExpectedTriggerId == UWObjectMechanics.UseTriggerId ? !lbOnNow : lbOnNow;

			if (lOTrigger != null && ClicksWhenUsed(pOObject.ID) && lbOnWhenAsked)
			{
				for (int liStep = 0; lOTrigger.Link != 0 && lOTrigger.Link < lOMaster.Count && liStep < MaxTrapChainDepth; liStep++)
				{
					UWObject lONext = lOTrigger.GetNextObjectInLine(lOMaster);

					if (lONext == null)
						break;

					lOTrigger = lONext;
				}
			}

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

			// THE PLAYER IS THE ONE who uses, looks and picks up, and UseTrigger asks him as for
			// every trigger kind (MaySetOff; the lever and the switch have moved already, UseRect
			// does that before checkTrap). Every use, look and pick-up trigger of UW1 carries the
			// player bit (self-check "trigger setters"), so nothing changes in the shipped game;
			// until 2026-10-13 ours did not ask (reference audit, finding of the fix agents).
			if (!MaySetOff(lOTrigger, Setter.Player))
				return false;

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
		///
		/// WHO OPENED decides (MaySetOff): OpenDoor hands its "who" to checkTrap and on to
		/// UseTrigger. All six open triggers of UW1 carry flags 4 or 6 - bit 2 set, bit 3 not - so
		/// the player, a door trap set off by the player and a broken door fire them, a creature
		/// opening the door never does. Until 2026-10-12 ours fired them for every opener, and a
		/// goblin walking through the storeroom doors of level 1 (4/49, 55/59) set off the
		/// trespass trap meant for the player (reference audit, traps).
		/// </summary>
		public static bool TryFireOpenTrigger(UWObject pODoorData, IUWTrapHost pIHost, Setter peOpener)
		{
			if (pODoorData == null || pIHost == null || fLevel(pIHost) == null)
				return false;

			pODoorData.EnsureContentsLoaded(fLevel(pIHost).Masterlist);

			if (pODoorData.Contents == null)
				return false;

			bool lbFired = false;

			foreach (UWObject lOLink in pODoorData.Contents)
			{
				if (lOLink == null || lOLink.ID != UWObjectMechanics.OpenTriggerId
					|| !MaySetOff(lOLink, peOpener))
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
			if (pOObject == null || pIHost == null || fLevel(pIHost) == null)
				return false;

			List<UWObject> lOMaster = fLevel(pIHost).Masterlist;
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
				&& (lOTrigger.Flags & RepeatableTrapFlag) == 0 && MaySetOff(lOTrigger, Setter.Player))
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
		/// the creature that stepped on it (IUWTrapHost.Firing, UWTrapFiring.ByCreature).</summary>
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
			if (pOTrigger == null || pIHost == null || fLevel(pIHost) == null)
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
			UWObject lOTrap = UWObjectMechanics.GetLinkedObject(pOTrigger, fLevel(pIHost).Masterlist);

			if (lOTrap == null)
				return false;

			int liLevelBefore = LevelIndexOf(pIHost);

			bool lbRan = fRunTrapChain(lOTrap, pOTrigger, pOSwitchObject, pIHost, piDepth);

			// After a LEVEL CHANGE, CurrentLevel points to another level. Neither the cleanup
			// nor the chain has any business there - the tiles and the
			// object list are different. WHATEVER THE TRAP DID: Trigger_ovr153_3B cleans up after
			// RunTrap_ovr153_24A without looking at its result (UWTrapChainRemoval).
			if (LevelIndexOf(pIHost) == liLevelBefore)
				RemoveOneShotTrap(pOTrigger, lOTrap, fLevel(pIHost));

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
		/// WHOEVER SET IT OFF is struck (whack_thing, UnderworldExhumed WORLDEV.C): the player, a
		/// creature or a thing - see below. (Until 2026-10-12 this said "it is always the player";
		/// creatures and things set off move triggers since 2026-10-06.)
		/// </summary>
		private static bool fFireDamageTrap(UWObject pOTrap, IUWTrapHost pIHost)
		{
			// QUALITY 0 DOES NOTHING, BUT THE CHAIN GOES ON (DamageTrap_ovr107_CB2 skips a damage of
			// 0, then TriggerNext). Level 5, 55/2 hangs a move trigger behind such a trap, whose door
			// trap closes the open portcullis at 57/2 while 54/2 calls up a dire ghost - an ambush.
			// Ours stopped the chain here until 2026-10-06, so the grate stayed open.
			if (pOTrap.Quality <= 0)
			{
				UWTrapLog.Detail = "-> 0 damage, nothing";

				return true;
			}

			// WHOEVER SET IT OFF (DamageTrap_ovr107_CB2, read 2026-10-06): a creature takes the
			// quality as damage - poison only ever goes to the player, for anyone else the
			// negative value is turned back into damage. A thing would take it on its own
			// quality (DamageObject); that is not done here.
			if (pIHost.Firing.ByCreature)
			{
				pIHost.DamageTriggeringCreature(pOTrap.Quality);
				UWTrapLog.Detail = string.Format("-> creature, {0} damage", pOTrap.Quality);

				return true;
			}

			// A THING: nobody is struck, and the chain goes on (TriggerNext). A thrown item on the
			// poison needles of level 3 brings their text, again and again while it slides, and
			// nobody is poisoned (per user on the original, 2026-10-06; ours stopped the chain
			// here, so no text came). AND THE THING IS NOT WORN EITHER: the user threw a wand of
			// Name Enchantment over the needles ten times in the original, its quality stayed 40
			// (SAVE3 before, SAVE4 after). Read literally, DamageTrap_ovr107_CB2 hands the thing to
			// DamageObject and DamageObjectAndDoors takes it off its byte 8 - why that does not show
			// is not found (probably how a throw from the inventory fills byte 8); the
			// observation rules, so nothing is done to it.
			// FOUND the same evening: the damage is taken off inside the motion step, and
			// ApplyProjectileMotion writes the hp from before the step back at its end - so only a
			// hit that destroys the thing counts: it becomes debris (StrikeTriggeringThing).
			if (pIHost.Firing.ByThing)
			{
				pIHost.StrikeTriggeringThing(pOTrap.Quality);
				UWTrapLog.Detail = string.Format("-> a thing, struck by {0} (destroyed only if worn out)", pOTrap.Quality);

				return true;
			}

			if (!fHasPlayer(pIHost))
				return false;

			if (UWObjectMechanics.IsPoisonDamageTrap(pOTrap))
			{
				if (PlayerResistsPoison(fDamageTypeProof(pIHost)))
				{
					UWTrapLog.Detail = string.Format("-> poison {0}, resisted", pOTrap.Quality);

					return true;
				}

				fPoisonPlayer(pIHost, pOTrap.Quality);

				UWTrapLog.Detail = string.Format("-> poison {0}", pOTrap.Quality);
			}
			else
			{
				fDamagePlayer(pIHost, pOTrap.Quality);

				UWTrapLog.Detail = string.Format("-> {0} damage", pOTrap.Quality);
			}

			return true;
		}

		/// <summary>
		/// THE POISON OF A TRAP AND A POISONED POTION IS RESISTED: whack_thing (UnderworldExhumed
		/// WORLDEV.C) poisons the player only when the new strength is above his poison AND
		/// check_res(ThePlayer, 1, 0x10) lets the test damage through - the player's resist byte
		/// gets 0x10 from a worn Poison Resistance (PLAYDATA.C). The potion runs the same trap
		/// (checkTrap after eating, USEITEMS.C). The strength test is UWPlayerVitals.ApplyPoison's.
		/// Until 2026-10-12 neither asked for the resistance (reference audit, traps D6).
		/// </summary>
		public static bool PlayerResistsPoison(int piDamageTypeProof)
		{
			return UWDamageTypes.Scale(piDamageTypeProof, 1, UWDamageTypes.Poison) == 0;
		}

		/// <summary>String block 1, our numbering: "Your Rune of Warding has been set off ".
		/// The reference says 245; checked against a dump of the block.</summary>
		private const int WardMessage = 246;

		/// <summary>First direction text in string block 1, our numbering ("to the North").
		/// The same eight that Detect Monsters uses.</summary>
		public const int FirstDirectionMessage = UWStringIds.FirstDirectionMessage;

		/// <summary>Base damage of the rune of warding, before the casting skill is added.</summary>
		private const int WardBaseDamage = 3;

		/// <summary>
		/// a_ward trap: the rune of warding goes off.
		///
		/// IT ONLY HITS CREATURES, never the one who placed it - the trigger already
		/// takes care of that (flags 8, see CastTrapSpell and MaySetOff); with nobody as the one
		/// who set it off (a sleeper's ambush, UWSleepRules) it does nothing. Either way it ends
		/// the chain (TRIGGER.C, TRAP_WARD). The message names the direction
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
			if (!pIHost.Firing.ByCreature)
				return false;

			if (pOTrap.Quality != UWObjectMechanics.WardTrapAnyClass)
				return false;

			int liDamage = WardBaseDamage;

			if (fHasPlayer(pIHost))
			{
				int liCasting = fCastingSkill(pIHost);

				if (liCasting > 0)
					liDamage += UWRandom.Next(liCasting);
			}

			pIHost.ApplyWardDamage(liDamage, WardMessage, FirstDirectionMessage);

			UWTrapLog.Detail = string.Format("-> rune of warding, {0} damage", liDamage);

			return true;
		}

		/// <summary>The fine position cast_trap_spell gives both records: the middle of the tile.</summary>
		private const int LaidTrapFinePosition = 3;

		/// <summary>The trap of a spell-laid trigger carries flags 1: one trigger points at it, the
		/// count the one-shot cleanup works down (UWTrapChainRemoval).</summary>
		private const int LaidTrapTriggerCount = 1;

		/// <summary>
		/// A TRAP LAID BY A SPELL - cast_trap_spell (UnderworldExhumed TRIGGER.C), which the Rune of
		/// Warding calls with trap kind 9 (creat_spell, SPELLS.C). Two static records, the trigger
		/// allocated first, the trap second (the trigger freed again when there is no slot for the
		/// trap), both put at the HEAD of the tile's list, so the trap comes first:
		///
		///   trigger  a_move trigger 0x1A0, invisible, door bit, quantity bit; flags 8 - creatures
		///            set it off (bit 3), the player never (bit 2 clear), one-shot (bit 1 clear);
		///            its special link the trap, its target tile its own; z the tile's floor
		///            (height &lt;&lt; 3), fine position 3/3, heading 0.
		///   trap     0x180 + kind, invisible, door bit, quantity bit, link 0; quality 0x3F (any
		///            creature), flags 1 (one trigger points at it); the same place.
		///
		/// The first creature over it sets it off, and the one-shot cleanup takes trigger and trap
		/// out of the tile (UseTrigger -> delete_trap): the rune goes off once. ANY TILE - the
		/// original makes no tile test, a rune cast into a wall lies in the wall's list.
		///
		/// UNTIL 2026-10-12 (reference audit, traps D1 and D12) ours followed the reference: the
		/// trigger with flags 0, the trap appended to the Masterlist in no tile list. The cleanup
		/// never found the trap, so the rune hurt every creature that walked in, again and again;
		/// in UW.EXE flags 0 would let only thrown things set it off. A solid tile was refused.
		/// False when no slot was free (the caller says "There is no room to create that.").
		/// </summary>
		public static bool CastTrapSpell(UWWorldEdit pOEdit, UWCommonObjectProperties pOCommon, int piTileX, int piTileY,
			int piTrapId)
		{
			UWLevel lOLevel = pOEdit != null ? pOEdit.Level : null;
			UWTile lOTile = lOLevel != null ? lOLevel.GetTile(piTileX, piTileY) : null;

			if (lOTile == null)
				return false;

			int liZPos = (lOTile.FloorHeight >> 4) << 3;

			UWObject lOTrigger = UWObjectFactory.Create(UWObjectMechanics.MoveTriggerId, pOCommon, 0, 0);
			UWObject lOTrap = UWObjectFactory.Create(piTrapId, pOCommon, 0, 0);

			int liTriggerIndex = pOEdit.TakeStaticNumber(lOTrigger);

			if (liTriggerIndex <= 0)
				return false;

			int liTrapIndex = pOEdit.TakeStaticNumber(lOTrap);

			if (liTrapIndex <= 0)
			{
				pOEdit.ReturnStaticNumber(liTriggerIndex);

				return false;
			}

			fSetLaidRecord(lOTrigger, liZPos);
			lOTrigger.Flags = OthersSetOffFlag;
			lOTrigger.Quantity = (ushort)liTrapIndex;
			lOTrigger.Quality = (ushort)piTileX;
			lOTrigger.Owner = (ushort)piTileY;

			fSetLaidRecord(lOTrap, liZPos);
			lOTrap.Flags = LaidTrapTriggerCount;
			lOTrap.Quantity = 0;
			lOTrap.Quality = UWObjectMechanics.WardTrapAnyClass;

			pOEdit.AddAtHead(lOTrigger, piTileX, piTileY);
			pOEdit.AddAtHead(lOTrap, piTileX, piTileY);

			return true;
		}

		/// <summary>
		/// A RUNE OF WARDING FROM AN OLDER SAVE OF OURS: until 2026-10-12 ours laid its move trigger
		/// with flags 0 and its trap with flags 0, so since the setter rule (MaySetOff) no creature
		/// sets it off and the one-shot cleanup finds no count. The game's own levels hold no ward
		/// trap at all - every one comes from the spell - so a move trigger on a ward trap without
		/// either setter bit is such a rune: it gets cast_trap_spell's flags 8, its trap the count 1
		/// (reference audit, finding of the fix agents, 2026-10-14). Called when a level is read.
		/// Returns how many it repaired.
		/// </summary>
		public static int RepairOldWardRunes(UWLevel pOLevel)
		{
			if (pOLevel == null || pOLevel.TileData == null || pOLevel.Masterlist == null)
				return 0;

			int liRepaired = 0;

			foreach (UWTile lOTile in pOLevel.TileData)
			{
				if (lOTile == null || lOTile.ObjectsInTile == null)
					continue;

				foreach (UWObject lOTrigger in lOTile.ObjectsInTile)
				{
					if (lOTrigger == null || lOTrigger.ID != UWObjectMechanics.MoveTriggerId
						|| (lOTrigger.Flags & (PlayerSetsOffFlag | OthersSetOffFlag)) != 0)
						continue;

					UWObject lOTrap = UWObjectMechanics.GetLinkedObject(lOTrigger, pOLevel.Masterlist);

					if (lOTrap == null || lOTrap.ID != UWObjectMechanics.WardTrapId)
						continue;

					lOTrigger.Flags = (ushort)(lOTrigger.Flags | OthersSetOffFlag);

					if ((lOTrap.Flags & 0xF) == 0)
						lOTrap.Flags = LaidTrapTriggerCount;

					liRepaired++;
				}
			}

			return liRepaired;
		}

		/// <summary>What cast_trap_spell sets alike on trigger and trap.</summary>
		private static void fSetLaidRecord(UWObject pOObject, int piZPos)
		{
			pOObject.IsHidden = true;
			pOObject.DoorDirection = true;
			pOObject.HasQuantity = true;
			pOObject.ZPos = piZPos;
			pOObject.XPos = LaidTrapFinePosition;
			pOObject.YPos = LaidTrapFinePosition;
			pOObject.Heading = 0;
			pOObject.Link = 0;
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
			int liTargetLevel = pOTrap.ZPos == 0 ? LevelIndexOf(pIHost) : pOTrap.ZPos - 1;

			if (liTargetLevel < 0 || liTargetLevel >= fLevelCount(pIHost))
				return false;

			// NOBODY BUT THE PLAYER IS MOVED (Teleport_ovr107_949 and its spot search ovr107_0, read
			// 2026-10-06): for anyone else on the same level ovr107_0 only looks for a tile in the
			// 9 by 9 around the target where it would fit (CheckIfItemFitsInTile) and hands the
			// tile back - nothing moves it; another level does not even search. Then the chain goes
			// on (TriggerNext). Per user on the original the same day: thrown things are not
			// teleported. A creature is not either (ours set it down on the target tile for a while
			// that evening - wrong).
			if (pIHost.Firing.ByCreature || pIHost.Firing.ByThing)
			{
				UWTrapLog.Detail = "-> not the player: nobody moved";

				return true;
			}

			UWTrapLog.Detail = string.Format("-> level {0}, tile {1}/{2}",
				liTargetLevel + 1, pOTrap.Quality, pOTrap.Owner);

			pIHost.World.TeleportTo(liTargetLevel, pOTrap.Quality, pOTrap.Owner);

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

			// The operation and its sign from one table (UWVariableTrapOperations, 2026-10-14).
			if (!UWVariableTrapOperations.TryApply(liOperation, liOld, liOperand, out liNew))
			{
				UWTrapLog.Detail = string.Format("-> unknown operation {0}", liOperation);

				return false;
			}

			UWGameVariables.Set(pOTrap.ZPos, liNew);

			UWTrapLog.Detail = string.Format("-> Var {0}: {1} {2} {3} = {4}",
				pOTrap.ZPos, liOld, UWVariableTrapOperations.NameOf(liOperation), liOperand,
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

		/// <summary>From here on they are triggers, below it traps (UWObjectIds).</summary>
		public const int FirstTriggerId = UWObjectIds.FirstTrigger;

		/// <summary>The last trigger; above it major class 6 ends.</summary>
		public const int LastTriggerId = UWObjectIds.LastTrigger;

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
		/// A trap we do not know, or one that reports failure, does not break the chain: the
		/// original follows the link whatever the kind did (since 2026-10-12, see below).
		/// </summary>
		private static bool fRunTrapChain(UWObject pOTrap, UWObject pOTrigger, UWObject pOSwitchObject,
			IUWTrapHost pIHost, int piDepth)
		{
			if (pOTrap == null || piDepth > MaxTrapChainDepth)
				return false;

			int liLevelBefore = LevelIndexOf(pIHost);

			// WHATEVER THE TRAP DID, THE CHAIN GOES ON (UseTrap, UnderworldExhumed TRIGGER.C): after
			// the kind's case the link is followed unless the kind itself ends the chain. Until
			// 2026-10-12 a kind ours does not build, or one that reported failure, ended it here
			// (reference audit, traps D14; no shipped chain reaches the difference). The result is
			// still this trap's, for the caller.
			bool lbHandled = fRunTrap(pOTrap, pOTrigger, pOSwitchObject, pIHost);

			// A level change ends the chain: the object list behind it belongs to another
			// level, every further jump would lead nowhere.
			if (LevelIndexOf(pIHost) != liLevelBefore)
				return lbHandled;

			// A DELETE OBJECT TRAP ENDS THE CHAIN (DeleteObjectTrap_ovr153_ADC clears the "continue"
			// flag that TriggerNext_ovr153_D03 tests): its sp_link is the object it removes, not a
			// next step. Level 7, 20/42 (per user, 2026-10-03: "keening noise" twice in ours, once in
			// the original): the delete trap removes the move trigger that started the chain, and we
			// fired that trigger again through the link. The create object, door, ward and tell
			// traps clear the flag too (continue_chain = 0); their links are a template, a lock or
			// nothing.
			if (EndsChain(pOTrap.ID))
				return lbHandled;

			UWObject lONext = UWObjectMechanics.GetLinkedObject(pOTrap, fLevel(pIHost).Masterlist);

			// A CHECK TRAP BRANCHES the chain instead of continuing it straight: if the
			// condition holds it continues at the sp_link target, otherwise at ITS neighbour
			// in the tile list. That is how both branches hang on the same thread in the original - see
			// fCheckVariableIsTrue.
			if (pOTrap.ID == UWObjectMechanics.CheckVariableTrapId && !fCheckVariableIsTrue(pOTrap))
				lONext = lONext == null
					? null
					: lONext.GetNextObjectInLine(fLevel(pIHost).Masterlist);

			// THE INVENTORY TRAP DOES NOT BRANCH, it aborts: if the player has the item
			// being searched for, it continues at the sp_link, otherwise not at all. The reference
			// simply returns null for that (an_inventory_trap.Activate).
			if (pOTrap.ID == UWObjectMechanics.InventoryTrapId && !fInventoryTrapMatches(pOTrap, pIHost))
				lONext = null;

			if (lONext == null || UWObjectIds.Major(lONext.ID) != TrapMajorClass)
				return lbHandled;

			if (lONext.ID < FirstTriggerId)
			{
				fRunTrapChain(lONext, pOTrigger, pOSwitchObject, pIHost, piDepth + 1);

				return lbHandled;
			}

			// A trigger in the chain cleans up afterwards just like the first one - it brings
			// its own flags.
			fFireTriggerTrap(lONext, pOSwitchObject, pIHost, piDepth + 1);

			return lbHandled;
		}

		/// <summary>a_tell trap (0x18A): UseTrap handles it in the ward's case (TRIGGER.C).</summary>
		public const int TellTrapId = 0x018A;

		/// <summary>The trap kinds whose case in UseTrap clears continue_chain (TRIGGER.C): create
		/// object, door, ward and tell, delete object. The others go on through their link.</summary>
		public static bool EndsChain(int piTrapId)
		{
			return piTrapId == UWObjectMechanics.CreateObjectTrapId || piTrapId == UWObjectMechanics.DoorTrapId
				|| piTrapId == UWObjectMechanics.WardTrapId || piTrapId == TellTrapId
				|| piTrapId == UWObjectMechanics.DeleteObjectTrapId;
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

			UWTrapLog.Record(pOTrap, pOTrigger, fData(pIHost), lbHandled);

			return lbHandled;
		}

		/// <summary>
		/// The trap kinds by id (UseTrap's switch, TRIGGER.C) and the a_do actions by quality
		/// (do_trap_hack). Since 2026-10-14 (cleanup step 2.10) two tables instead of an if-chain
		/// with a nested switch; the handlers are the ones the chain called, in the same cases.
		/// Read-only for the dispatch-coverage check of the self-check.
		/// </summary>
		public static UWTrapTable Traps { get; } = fBuildTrapTable();

		public static UWDoActionTable DoActions { get; } = fBuildDoActionTable();

		private static bool fRunTrapInner(UWObject pOTrap, UWObject pOTrigger, UWObject pOSwitchObject, IUWTrapHost pIHost)
		{
			return Traps.Run(pOTrap, pOTrigger, pOSwitchObject, pIHost);
		}

		private static UWTrapTable fBuildTrapTable()
		{
			UWTrapTable lOTable = new UWTrapTable()
				.Register(UWObjectMechanics.DamageTrapId, "damage",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireDamageTrap(pOTrap, pIHost))
				.Register(TeleportTrapId, "teleport",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireTeleportTrap(pOTrap, pIHost))
				.Register(UWObjectMechanics.ArrowTrapId, "arrow",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liArrowX, int liArrowY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
						return pIHost.FireArrowTrap(pOTrap, liArrowX, liArrowY);
					})
				.Register(UWObjectMechanics.DoTrapId, "do",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => DoActions.Run(pOTrap, pOTrigger, pOSwitch, pIHost))

				// THE PIT TRAP DOES NOTHING IN UW1: UseTrap (UnderworldExhumed TRIGGER.C) has no case
				// for TRAP_PIT, so the chain simply goes on through its link. Until 2026-10-12 ours ran
				// the reference's a_pit_trap here, which saw the one pit trap of the game (level 3,
				// 6/4, a SOLID tile of height 15) as "up", dropped it to height 0 and opened a hole
				// twelve steps deep in the wall west of 7/4 - reached through the three buttons at 5/7
				// (reference audit, traps).
				.Register(UWObjectMechanics.PitTrapId, "pit",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						UWTrapLog.Detail = "-> pit trap: nothing in UW1";

						return true;
					})
				.Register(UWObjectMechanics.ChangeTerrainTrapId, "change terrain",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liTerrainX, int liTerrainY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
						return fFireChangeTerrainTrap(pOTrap, liTerrainX, liTerrainY, pIHost);
					})
				.Register(UWObjectMechanics.SpellTrapId, "spell",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liSpellX, int liSpellY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
						return fFireSpellTrap(pOTrap, liSpellX, liSpellY, pIHost);
					})
				.Register(UWObjectMechanics.CreateObjectTrapId, "create object",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
						return fFireCreateObjectTrap(pOTrap, liTileX, liTileY, pIHost);
					})
				.Register(UWObjectMechanics.DoorTrapId, "door",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						fFireDoorTrap(pOTrigger, pOTrap, pIHost);
						return true;
					})

				// The ward and the tell trap share one case in UseTrap.
				.Register(UWObjectMechanics.WardTrapId, "ward",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireWardTrap(pOTrap, pIHost))
				.Register(TellTrapId, "tell",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireWardTrap(pOTrap, pIHost))
				.Register(UWObjectMechanics.DeleteObjectTrapId, "delete object",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireDeleteObjectTrap(pOTrap, pIHost))

				// The inventory trap's test decides the chain (fRunTrapChain); the kind itself does nothing.
				.Register(UWObjectMechanics.InventoryTrapId, "inventory",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => true)
				.Register(UWObjectMechanics.SetVariableTrapId, "set variable",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireSetVariableTrap(pOTrap))
				.Register(UWObjectMechanics.CheckVariableTrapId, "check variable",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireCheckVariableTrap(pOTrap))
				.Register(UWObjectMechanics.TextStringTrapId, "text string",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						fFireTextTrap(pOTrap, pIHost);
						return true;
					});

			lOTable.Seal();

			return lOTable;
		}

		private static UWDoActionTable fBuildDoActionTable()
		{
			UWDoActionTable lOTable = new UWDoActionTable()
				.Register(UWObjectMechanics.DoTrapCameraAction, "crystal ball",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireCameraTrap(pOTrap, pOTrigger, pIHost))

				// QUALITY 3 AND 4 share the routine (do_trap_hack, TRIGGER.C): 3 sets the target
				// tile's height, 4 an object's z - see fFireEightPositionSwitch.
				.Register(UWObjectMechanics.DoTrapRaiseTileAction, "eight-position switch (tile)", fEightPositionSwitchAction)
				.Register(UWObjectMechanics.DoTrapRaiseTileAction + 1, "eight-position switch (object)", fEightPositionSwitchAction)
				.Register(UWObjectMechanics.DoTrapTrespassAction, "trespass",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireTrespassTrap(pOTrap, pIHost))
				.Register(UWObjectMechanics.DoTrapBullfrogAction, "bullfrog",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireBullfrogTrap(pOTrap.Owner, pIHost))
				.Register(UWObjectMechanics.DoTrapEmeraldPuzzleAction, "emerald puzzle",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						(int liGemX, int liGemY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

						return fFireEmeraldPuzzleTrap(liGemX, liGemY, pIHost);
					})
				.Register(UWObjectMechanics.DoTrapExplodingBookAction, "exploding book",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireExplodingBookTrap(pOTrap, pIHost))
				.Register(UWObjectMechanics.DoTrapConversationAction, "talking door",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						UWTrapLog.Detail = "-> talking door";

						return pIHost.World.Presentation.ShowsMessages
							&& pIHost.BeginConversation(UWObjectMechanics.TalkingDoorConversationSlot);
					})
				.Register(UWObjectMechanics.DoTrapStartConversationAction, "start conversation",
					(pOTrap, pOTrigger, pOSwitch, pIHost) => fFireConversationTrap(pIHost))
				.Register(UWObjectMechanics.DoTrapArialAction, "Arial",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						pIHost.World.Presentation.PlayCutscene(ArialCutscene);
						return true;
					})
				.Register(UWObjectMechanics.DoTrapQuakeFirstAction, "quake", fQuakeAction)
				.Register(UWObjectMechanics.DoTrapQuakeFirstAction + 1, "quake", fQuakeAction)
				.Register(UWObjectMechanics.DoTrapQuakeLastAction, "quake", fQuakeAction)
				.Register(UWObjectMechanics.DoTrapEndgameAction, "end sequence",
					(pOTrap, pOTrigger, pOSwitch, pIHost) =>
					{
						UWTrapLog.Detail = "-> end sequence";

						return pIHost.PlayVictory();
					});

			lOTable.Seal();

			return lOTable;
		}

		private static bool fEightPositionSwitchAction(UWObject pOTrap, UWObject pOTrigger, UWObject pOSwitchObject, IUWTrapHost pIHost)
		{
			fFireEightPositionSwitch(pOSwitchObject, pOTrigger, pOTrap, pIHost);
			return true;
		}

		private static bool fQuakeAction(UWObject pOTrap, UWObject pOTrigger, UWObject pOSwitchObject, IUWTrapHost pIHost)
		{
			return fFireQuakeTrap(pOTrap, pIHost);
		}

		/// <summary>a_teleport trap - the same number the level loader's transition scan
		/// uses (UWLevelLoader.TeleportTrapId).</summary>
		public const int TeleportTrapId = 0x0181;

		/// <summary>
		/// Fires a create-object trap WITHOUT a trigger in front of it - for the
		/// periodic respawn (see UWTriggerSystem.RespawnCreatures).
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
			if (pOTrap == null || pIHost == null || fLevel(pIHost) == null)
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
			UWTrapChainRemoval.RemoveTrapFromChain(pOItem, pOTrap, fLevel(pIHost));

			return lbFired;
		}

		/// <summary>
		/// The wandering-monster pass (DoWanderingMonsters, TRIGGER.C): a create-object trap
		/// without a trigger (flags 0, UWRespawnRules) whose template is a creature first marks the
		/// TEMPLATE as temporary (SET_TEMP, word 0x0D bit 8), then fires; the copy inherits the
		/// mark, so the crowding check counts it and the level exit removes it.
		/// </summary>
		public static bool FireTrapInTile(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			if (pOTrap == null || pIHost == null || fLevel(pIHost) == null
				|| pOTrap.ID != UWObjectMechanics.CreateObjectTrapId)
				return false;

			if (pOTrap.Flags == 0
				&& UWObjectMechanics.GetLinkedObject(pOTrap, fLevel(pIHost).Masterlist) is UWNpc lOTemplate)
				lOTemplate.NPCSpawned = true;

			return fFireCreateObjectTrap(pOTrap, piTileX, piTileY, pIHost);
		}

		/// <summary>The crowding check around a creature template: four tiles back and five
		/// forward - see fFireCreateObjectTrap.</summary>
		private const int CrowdTilesBack = 4;

		private const int CrowdTilesForward = 5;

		/// <summary>
		/// THE CREATE ROLL (TRAP_CREATE_OBJECT, UnderworldExhumed TRIGGER.C): nothing when
		/// rand() * 0x3F / 0x8000 - 0 to 62 - is below the quality, so quality 0 always creates and
		/// quality q creates with chance (63 - q) in 63. Until 2026-10-12 ours drew 0 to 63 and
		/// gave up at or below the quality, which lost a quality-0 ambush one time in 64
		/// (reference audit, traps D8). piRand is a draw of 0 to 0x7FFF.
		/// </summary>
		public static bool CreateObjectRollFails(int piRand, int piQuality)
		{
			return ((piRand & 0x7FFF) * 0x3F >> 15) < piQuality;
		}

		/// <summary>a_create object trap: creates a copy of the object the sp_link
		/// points to - quality is an inverse probability, see CreateObjectRollFails.
		///
		/// On level 1 these are five monster generators: bat 47/4 (quality 31),
		/// giant rat 3/17 (40), giant spider 14/7 (50), flesh slug 27/34 (45) and the
		/// goblin 3/33 (0), the latter being the only one fired via a move trigger on 3/36.</summary>
		private static bool fFireCreateObjectTrap(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			UWObject lOTemplate = UWObjectMechanics.GetLinkedObject(pOTrap, fLevel(pIHost).Masterlist);

			if (lOTemplate == null)
				return false;

			if (!pIHost.AlwaysFireCreateObjectTraps && CreateObjectRollFails(UWRandom.Next(0x8000), pOTrap.Quality))
				return true;

			// NOT WHILE ONE OF ITS KIND IS STILL ABOUT: for a creature template the original runs
			// CheckForBitDInArea_ovr153_148E around the TEMPLATE (Traps_ovr153_296 labels 70F-721),
			// which asks every creature within four tiles of it for bit 8 of word 0x0D - the mark
			// a respawned creature carries - and then creates nothing. The area routine includes
			// its far end, so it is four back and five forward; the template itself does not count.
			// Until 2026-09-23 the respawn held its whole pass back instead, looking around the
			// player (UWRespawnRules).
			if (UWObjectIds.IsCreature(lOTemplate.ID)
				&& fIsSpawnedCreatureNear(pIHost, lOTemplate.TileX, lOTemplate.TileY, CrowdTilesBack, CrowdTilesForward))
				return true;

			// The record copied, and of what the template holds only the first item, copied too
			// (TRIGGER.C TRAP_CREATE_OBJECT; see UWObject.Clone).
			UWObject lOCopy = UWObject.Clone(lOTemplate, fLevel(pIHost).Masterlist);

			if (lOCopy == null)
				return false;

			// THE MARK COMES WITH THE TEMPLATE (the record is copied whole): the original's bit 8 of
			// word 0x0D is set on the template by the wandering-monster pass only (FireTrapInTile),
			// so a wandering creature counts in the crowding check above and the level exit removes
			// it (UWLevelExit), while the creature of a triggered trap - an ambush, the goblin of
			// level 1 at 3/33 - stays. Until 2026-10-12 every copy was marked, and an ambush
			// monster vanished when the player left the level (reference audit, TRIGGER.C
			// DoWanderingMonsters and TRAP_CREATE_OBJECT).

			// With position and height of the template, like UW.EXE - see SpawnObjectInTileAtTemplatePosition.
			return pIHost.World.Spawn(lOCopy, new UWTilePos(piTileX, piTileY));
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
			if (!pIHost.World.Presentation.ShowsMessages || fData(pIHost) == null)
				return;

			try
			{
				string lsMessage = fData(pIHost).GetLevelMessage(
					UWObjectMechanics.GetTextTrapStringIndex(pOTrap));

				// Only trim the TRAILING line break that the entries in block 9 carry
				// throughout - otherwise an empty line trails the message in the log
				// (noticed by the user). Inner line breaks are kept, some of the texts are
				// multi-line; an earlier attempt removed all of them and squeezed the text
				// into one line.
				if (!string.IsNullOrWhiteSpace(lsMessage))
					pIHost.World.Presentation.AddMessage(lsMessage.TrimEnd('\r', '\n'));
			}
			catch
			{
				// As everywhere else with string access: a missing entry is not an error.
			}
		}

		/// <summary>Cutscene 3, the freeing of Arial (ArialTalking_ovr107_1373).</summary>
		private const int ArialCutscene = 3;

		/// <summary>
		/// a_do trap 41, the exploding book - the same routine as opening Bronus' book, see
		/// UWExplodingBook (see ExplodingBook_ovr107_1259). No level holds this trap.
		///
		/// THE PLAYER'S TILE, DEEP (the same routine, UnderworldExhumed WORLDEV.C): the
		/// routine ignores the trap and looks the book 0x114 up in the list of the tile the PLAYER
		/// stands on with Obj_InList recursing - into the containers lying there, and into the
		/// player object, whose chain is his inventory. Ours looks in the inventory first (where
		/// the player object sits in the tile's chain was not read), then on the player's tile and
		/// in the containers there. Until 2026-10-12 it searched the TRAP's tile and only its top
		/// level (reference audit, traps D19); until 2026-10-01 it took any object of class 4
		/// minor 1 (orb rock, incense, stew ...) and said "The book explodes!".
		/// </summary>
		private static bool fFireExplodingBookTrap(UWObject pOTrap, IUWTrapHost pIHost)
		{
			UWInventoryModel lOInventory = fInventory(pIHost);
			UWObject lOBook = UWExplodingBook.FindCarried(lOInventory);
			UWObject lOHolder = null;
			bool lbCarried = lOBook != null;

			if (!lbCarried)
			{
				UWTile lOTile = fLevel(pIHost) != null
					? fLevel(pIHost).GetTile(fPlayerTile(pIHost).X, fPlayerTile(pIHost).Y) : null;

				lOBook = lOTile != null
					? fFindExplodingBook(lOTile.ObjectsInTile, null, fLevel(pIHost).Masterlist, 0, out lOHolder)
					: null;
			}

			if (lOBook == null)
				return true;

			if (pIHost.World.Presentation.ShowsMessages)
				pIHost.World.Presentation.AddMessage(UWExplodingBook.Message);

			UWQuestFlags.Set(UWExplodingBook.QuestFlag, 1);
			fCursePlayer(pIHost, UWExplodingBook.CurseDice);

			if (lbCarried)
			{
				lOInventory.RemoveItem(lOBook);
				lOInventory.NotifyChanged();
			}
			else if (lOHolder != null)
				lOHolder.Contents.Remove(lOBook);
			else
				pIHost.World.Remove(lOBook);

			return true;
		}

		/// <summary>Obj_InList with recurse for the book 0x114: the first in the list, depth first
		/// into every member without the quantity bit. pOHolder is the object whose contents hold
		/// it, null on the top level.</summary>
		private static UWObject fFindExplodingBook(IList<UWObject> pOList, UWObject pOOwner, List<UWObject> pOMasterlist,
			int piDepth, out UWObject pOHolder)
		{
			pOHolder = null;

			if (pOList == null || piDepth > MaxTrapChainDepth)
				return null;

			foreach (UWObject lOObject in pOList)
			{
				if (lOObject == null)
					continue;

				if (lOObject.ID == UWExplodingBook.BookObjectId)
				{
					pOHolder = pOOwner;

					return lOObject;
				}

				if (lOObject.HasQuantity)
					continue;

				if (lOObject.Contents == null && lOObject.Quantity > 0 && lOObject.Quantity < pOMasterlist.Count
					&& lOObject.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
					lOObject.EnsureContentsLoaded(pOMasterlist);

				UWObject lOFound = fFindExplodingBook(lOObject.Contents, lOObject, pOMasterlist, piDepth + 1, out pOHolder);

				if (lOFound != null)
					return lOFound;
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
			// ONLY WHEN THE PLAYER SET IT OFF (do_trap_hack 0x3C-0x3E, UnderworldExhumed TRIGGER.C:
			// CharacterThatTriggeredTrap == ThePlayer). Until 2026-10-12 a creature or a thing
			// shook the screen too (reference audit, traps D19).
			if (pIHost.Firing.ByCreature || pIHost.Firing.ByThing)
			{
				UWTrapLog.Detail = "-> quake: not set off by the player";

				return true;
			}

			int liKind = pOTrap.Quality - UWObjectMechanics.DoTrapQuakeBase;

			// The screen shakes on the large channel for 0x1E, whatever the owner field says -
			// DoTrapScreenShake_seg008_DD6 takes no argument (read 2026-09-25; until then the owner
			// was the length). The owner therefore stays unread here, while Tremor passes its own
			// duration - cleanup step 3.1 kept the two apart (2026-10-17), and since step 3.6 the
			// quake's fixed length lives here (QuakeShakeDuration) instead of on the trap host.
			if ((liKind & 1) != 0)
				pIHost.World.Presentation.ShakeScreen(QuakeShakeDuration);

			// The player is thrown up (BouncePlayer_seg008_D9E) by the owner field.
			if ((liKind & 2) != 0)
				pIHost.World.Player.BounceUp(pOTrap.Owner);

			return true;
		}

		/// <summary>The quake's shake: 0x1E on the large channel, always (DoTrapScreenShake_seg008_DD6;
		/// the trap host's QuakeShakeDuration until cleanup step 3.6, 2026-10-17).</summary>
		private const int QuakeShakeDuration = 0x1E;

		/// <summary>eight_pos_switch applies a tile height only below this (0x68, WORLDEV.C).</summary>
		private const int EightPositionHeightLimit = 0x68;

		/// <summary>Its quality 4 names the object to move by the trap's link, nine bits.</summary>
		private const int EightPositionLinkMask = 0x1FF;

		/// <summary>
		/// a_do traps 3 and 4, the eight-position switch - eight_pos_switch (UnderworldExhumed
		/// WORLDEV.C) with the flags of the switch that was used (TriggeringButton): the height is
		/// the trap's z plus the flags times 8. Quality 3 sets the TARGET tile's height to that over
		/// 8 when it is below 0x68 (change_terrain); quality 4 sets the z of the object the trap's
		/// link names instead. The lever has already stepped its flags (UseRect, then checkTrap -
		/// see fTryFireTriggerChain).
		///
		/// Verified live by the user (2026-08-28, first assumption corrected): an a_lever with
		/// a_do trap action 3 does NOT raise its own tile but a target tile - via the same trigger
		/// target coordinate convention as the door trap (GetTriggerTargetTile). The lever itself
		/// only advances its own visual position (see UWHeightLever). ONLY here (fired by actual
		/// player use) is the target tile height ever set - during level build it deliberately stays
		/// at its original static height, which does not match the lever's start position
		/// (confirmed by user, level 1 - not a bug, do not accidentally "fix" it). The user's
		/// measurement there - position 0 at 32, each position 16 higher - is this formula for the
		/// four traps of level 1 (z 16).
		///
		/// Until 2026-10-12 ours computed 32 + position * z world units and raised the tile for
		/// quality 4 too (reference audit, traps D15; the same numbers for z 16, and no quality-4
		/// trap exists).
		/// </summary>
		private static void fFireEightPositionSwitch(UWObject pOSwitchObject, UWObject pOTrigger, UWObject pOTrap,
			IUWTrapHost pIHost)
		{
			if (pOSwitchObject == null || fLevel(pIHost) == null)
				return;

			int liHeight = pOTrap.ZPos + ((pOSwitchObject.Flags & 0xF) << 3);

			if (pOTrap.Quality != UWObjectMechanics.DoTrapRaiseTileAction)
			{
				List<UWObject> lOMaster = fLevel(pIHost).Masterlist;
				int liIndex = pOTrap.Quantity & EightPositionLinkMask;
				UWObject lOMoved = liIndex > 0 && liIndex < lOMaster.Count ? lOMaster[liIndex] : null;

				if (lOMoved != null)
					lOMoved.ZPos = liHeight;

				UWTrapLog.Detail = string.Format("-> z {0} for object {1}", liHeight, pOTrap.Quantity & EightPositionLinkMask);

				return;
			}

			(int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
			UWTile lOTargetTile = fLevel(pIHost).GetTile(liTileX, liTileY);

			if (lOTargetTile == null || liHeight >= EightPositionHeightLimit)
				return;

			SetTileHeight(lOTargetTile, liHeight >> 3, pIHost);
			pIHost.RebuildGeometry();

			UWTrapLog.Detail = string.Format("-> tile {0}/{1} height {2}", liTileX, liTileY, liHeight >> 3);
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
		///   floor height   zpos &gt;&gt; 3, only 0 to 13 are applied
		///   floor texture  quality &gt;&gt; 1, only 0 to 10 are applied
		///   wall texture   owner, only 0 to 47 are applied
		///
		/// THE BOUNDS are change_terrain's (UnderworldExhumed WORLDEV.C): floor below 0xB, wall
		/// below 0x30, height at most 0xD. Until 2026-10-12 ours took floor 0 to 9 and height 0 to
		/// 14; no trap uses floor 10 or height 14 (reference audit, traps D17).
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
			UWLevel lOLevel = fLevel(pIHost);

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
					if (liNewHeight >= 0 && liNewHeight <= MaxTerrainHeight)
						SetTileHeight(lOTile, liNewHeight, pIHost);

					// Via the slots, not the texture values - that way they also end up in the save game
					// (see UWTile.SetFloorTexture).
					if (lOLevel.TextureInfos != null && liFloorTexture < ChangeTerrainFloorLimit
						&& liFloorTexture + FirstFloorTextureEntry < lOLevel.TextureInfos.Length)
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

			if (liOwner == 0 || !pIHost.World.Presentation.ShowsMessages)
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
			if (!pIHost.World.Presentation.ShowsMessages)
				return true;

			if (LevelIndexOf(pIHost) != UWObjectMechanics.BullfrogLevelIndex)
			{
				pIHost.World.Presentation.AddGeneralMessage(BullfrogWrongLevelMessage);

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
				pIHost.World.Presentation.AddGeneralMessage(BullfrogResetMessage);

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
				pIHost.World.Presentation.AddGeneralMessage(BullfrogOutOfTriesMessage);

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

			fAdjustBullfrogTiles(liCornerX, liCornerY, liSpanX, liSpanY, liDelta, pIHost);
			fAdjustBullfrogTiles(liCentreX, liCentreY, 0, 0, liDelta, pIHost);

			pIHost.RebuildGeometry();

			UWTrapLog.Detail = string.Format("-> target {0}/{1}, height {2}{3}, left {4}",
				liCentreX, liCentreY, liDelta > 0 ? "+" : string.Empty, liDelta, liRetries - 1);

			return true;
		}

		/// <summary>Raises or lowers a tile rectangle by one step. The bounds are
		/// inclusive, as in the reference and as in fFireChangeTerrainTrap. A step that would leave
		/// 0 to 13 is not taken - change_terrain with adjust 1 or 3 (WORLDEV.C); until 2026-10-12
		/// ours let the field rise to 14 (reference audit, traps D10).</summary>
		private static void fAdjustBullfrogTiles(int piTileX, int piTileY, int piSpanX, int piSpanY,
			int piDelta, IUWTrapHost pIHost)
		{
			UWLevel pOLevel = fLevel(pIHost);

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

					if (liHeight < 0 || liHeight > MaxTerrainHeight)
						continue;

					SetTileHeight(lOTile, liHeight, pIHost);
				}
			}
		}

		/// <summary>Flattens the whole Bullfrog field back to height four - exactly the state
		/// it has in the level data.</summary>
		private static void fFlattenBullfrogGrid(IUWTrapHost pIHost)
		{
			UWLevel lOLevel = fLevel(pIHost);

			if (lOLevel == null || lOLevel.TileData == null)
				return;

			for (int liX = 0; liX < UWObjectMechanics.BullfrogGridSize; liX++)
			{
				for (int liY = 0; liY < UWObjectMechanics.BullfrogGridSize; liY++)
				{
					UWTile lOTile = lOLevel?.GetTile(UWObjectMechanics.BullfrogGridOrigin + liX, UWObjectMechanics.BullfrogGridOrigin + liY);

					if (lOTile != null)
						SetTileHeight(lOTile, BullfrogResetHeight, pIHost);
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
			UWLevel lOLevel = fLevel(pIHost);

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
				pIHost.World.Remove(lOEmerald);

			// emerald_trap Obj_Adds the stone: at the head of the tile's list (2026-10-18).
			pIHost.World.SpawnNewObject(
				UWPlayerData.FirstRuneObjectId + (int)UWPlayerData.Rune.Vas, new UWTilePos(piTileX, piTileY + 1), 0,
				UWWorldEdit.Arrival.Created);

			UWTrapLog.Detail = "-> Vas rune stone";

			return true;
		}

		/// <summary>This many emeralds the puzzle requires.</summary>
		private const int EmeraldPuzzleCount = 4;

		/// <summary>The conversation a_do trap 50 looks for: the troll guard of level 7.</summary>
		public const int ConversationTrapWhoAmI = 0xD8;

		/// <summary>The goals gronkify_talkto_player knows: a waiting critter (7) goes home (1).</summary>
		private const int GoalStand7 = 7;

		private const int GoalGoHome = 1;

		/// <summary>
		/// a_do trap 50: starts a conversation.
		///
		/// WITH WHOM is in none of the trap's fields - all are zero. do_trap_hack case 0x32
		/// (UnderworldExhumed TRIGGER.C) runs gronk_whoami (SPELLS.C) over the ACTIVE mobile list
		/// for the first critter with conversation 0xD8, and its callback gronkify_talkto_player
		/// sends one that waits (goal 7) home (goal 1) before it talks to the player. Dead or not
		/// active, nothing happens. On level 7 that is the troll guard in slot 251 on tile 26/56,
		/// three tiles from the move trigger that fires the trap; his first line is "Ho ho, just
		/// like big rat in trap." Until 2026-10-12 ours talked to whatever held slot 251 and left
		/// the goal alone (reference audit, traps D9).
		/// </summary>
		private static bool fFireConversationTrap(IUWTrapHost pIHost)
		{
			if (!pIHost.World.Presentation.ShowsMessages || fLevel(pIHost) == null)
				return true;

			UWNpc lONpc = FindActiveByWhoAmI(fLevel(pIHost), ConversationTrapWhoAmI);

			if (lONpc == null)
			{
				UWTrapLog.Detail = "-> no active creature with conversation 216";

				return true;
			}

			UWCritterRecord lORecord = new UWCritterRecord(lONpc);

			if (lORecord.Goal == GoalStand7)
				lORecord.Goal = GoalGoHome;

			UWTrapLog.Detail = string.Format("-> conversation with object {0}",
				fLevel(pIHost).Masterlist.IndexOf(lONpc));

			pIHost.TalkTo(lONpc);

			return true;
		}

		/// <summary>gronk_whoami for the first match: the critters of the level's active mobile
		/// list, in its order, whose whoami is the one asked for. Null when none.</summary>
		public static UWNpc FindActiveByWhoAmI(UWLevel pOLevel, int piWhoAmI)
		{
			if (pOLevel == null || pOLevel.ActiveMobiles == null || pOLevel.Masterlist == null)
				return null;

			foreach (int liIndex in pOLevel.ActiveMobiles)
			{
				if (liIndex > 0 && liIndex < pOLevel.Masterlist.Count && pOLevel.Masterlist[liIndex] is UWNpc lONpc
					&& lONpc.NPCwhoami == piWhoAmI)
					return lONpc;
			}

			return null;
		}

		/// <summary>change_terrain writes a floor below this (0xB, WORLDEV.C) - one more than the ten
		/// floor textures of a level; slot 10 reads the entry after them, as the tile word would.</summary>
		private const int ChangeTerrainFloorLimit = 0xB;

		/// <summary>Where a level's floor textures start in UWLevel.TextureInfos (UWTile.SetFloorTexture).</summary>
		private const int FirstFloorTextureEntry = 48;

		/// <summary>The highest floor change_terrain sets (0xD, WORLDEV.C) - by a trap, the bullfrog
		/// and the height levers alike.</summary>
		public const int MaxTerrainHeight = 0xD;

		/// <summary>
		/// A TILE'S NEW HEIGHT AND WHAT LIES ON IT - change_terrain (UnderworldExhumed WORLDEV.C),
		/// the one way every height change goes: the change terrain trap, the eight-position switch
		/// (the four levers of level 1, 56-59/45), the bullfrog's steps and its reset. On a tile that
		/// RISES every object below the new floor is lifted onto it; on a tile that SINKS every object
		/// resting on the old floor goes down with it. Traps and triggers stay where they are.
		/// Until 2026-10-13 ours changed the floor alone, and what lay there floated or sank into it
		/// (reference audit, finding of the fix agents; per user the same for the levers of level 1).
		///
		/// THE CREATURES go with the floor by the same rule: change_terrain walks the tile's list,
		/// creatures included, and sets their z (per user on the original 2026-10-15: a troll on a
		/// lowered bullfrog tile went down at once; ours waited until it moved, because our creatures
		/// live in their records, not in the tile lists - IUWTrapHost.MoveCreaturesWithFloor). THE
		/// PLAYER follows through his own motion (the original updates his physics here too).
		/// </summary>
		public static void SetTileHeight(UWTile pOTile, int piHeight, IUWTrapHost pIHost)
		{
			if (pOTile == null)
				return;

			int liOld = pOTile.FloorHeight >> 4;

			pOTile.FloorHeight = (ushort)(piHeight << 4);

			if (piHeight == liOld)
				return;

			int liNewZ = piHeight << 3;
			int liOldZ = liOld << 3;

			if (pIHost != null)
				pIHost.MoveCreaturesWithFloor(pOTile.X, pOTile.Z, liOldZ, liNewZ);

			if (pOTile.ObjectsInTile == null)
				return;

			foreach (UWObject lOObject in pOTile.ObjectsInTile)
			{
				if (lOObject == null || lOObject is UWNpc || UWObjectIds.Major(lOObject.ID) == TrapMajorClass)
					continue;

				bool lbMoves = piHeight > liOld ? lOObject.ZPos < liNewZ : lOObject.ZPos == liOldZ;

				if (!lbMoves)
					continue;

				int liWas = lOObject.ZPos;

				lOObject.ZPos = liNewZ;

				if (pIHost != null)
					pIHost.OnObjectHeightChanged(lOObject, liWas);
			}
		}

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
		///   Class 5 is PROJECTILES, and when cast by the player they wait for the click that
		///   gives the direction. A trap has no click: release_missile with the trap as the
		///   caster, spell_fire for an object (UnderworldExhumed SPELLS.C, MISSILE.C) - from the
		///   trigger's target tile at the trap's fine position and height, flat, along the trap's
		///   heading (SpellTrapMissileHeading); nobody is aimed at. That is the trap on level 5
		///   at 10/45 (5/3, heading 2, z 51): its fireball flies east down the corridor, 19 above
		///   the floor, and hits the player standing in line on the trigger at 14/45. Until
		///   2026-10-12 ours aimed it at the camera, the reference's way.
		///
		/// THE CASTER IS THE TRAP (inanimate_spell -> do_spell(class, minor, trap, who), SPELLS.C),
		/// so what needs the player as the caster does nothing: the lasting spells of classes 0
		/// to 3 (set_curmagic only for who == ThePlayer), the spells on a creature in front of
		/// the caster (class 7, nail_1area), restoring mana (class 10) and the extra spells
		/// (class 11, xt_spells); a backfire (class 9) hurts only a creature caster. A heal
		/// (class 4) goes to the one who set the trap off - the player; a creature's heal is not
		/// built. The rest - area, creation, the special class 13 - takes the normal path.
		/// Until 2026-10-12 every class took the normal path and affected the player. No
		/// shipped spell trap has any of these classes (latent, reference audit spells 20).
		/// </summary>
		private static bool fFireSpellTrap(UWObject pOTrap, int piTileX, int piTileY, IUWTrapHost pIHost)
		{
			int liMajor = pOTrap.Quality;
			int liMinor = pOTrap.Owner;

			UWTrapLog.Detail = string.Format("spell {0}/{1}", liMajor, liMinor);

			if (!SpellTrapCastsFor(liMajor, pIHost.Firing.ByCreature, pIHost.Firing.ByThing))
			{
				UWTrapLog.Detail = string.Format("spell {0}/{1}: nothing with a trap as the caster", liMajor, liMinor);

				return true;
			}

			return pIHost.FireSpellTrap(pOTrap, piTileX, piTileY, liMajor, liMinor);
		}

		/// <summary>Spell classes of do_spell (combat.h) a trap cannot cast - see fFireSpellTrap.</summary>
		private const int LightClass = 0;

		private const int ProtectClass = 3;

		private const int HealClass = 4;

		private const int OneAreaClass = 7;

		private const int BackfireClass = 9;

		private const int ManaClass = 10;

		private const int ExtraSpellsClass = 11;

		/// <summary>Whether a spell trap of this class does anything with the trap as the caster and
		/// whoever set it off as the target - see fFireSpellTrap.</summary>
		public static bool SpellTrapCastsFor(int piMajorClass, bool pbCreatureSetItOff, bool pbThingSetItOff)
		{
			if (piMajorClass >= LightClass && piMajorClass <= ProtectClass)
				return false;

			switch (piMajorClass)
			{
				case OneAreaClass:
				case BackfireClass:
				case ManaClass:
				case ExtraSpellsClass:
					return false;

				case HealClass:
					return !pbCreatureSetItOff && !pbThingSetItOff;

				default:
					return true;
			}
		}

		/// <summary>The heading a spell trap's missile flies in: missile_fire with missile_arc 0
		/// takes OBJ_HEADING(trap) &lt;&lt; 5 and adds missile_trx 0 (MISSILE.C).</summary>
		public static int SpellTrapMissileHeading(UWObject pOTrap)
		{
			return pOTrap != null ? (pOTrap.Heading & 7) << 5 : 0;
		}

		/// <summary>
		/// an_inventory trap: does the player carry the item being searched for?
		///
		/// THE ORIGINAL'S SEARCH (TRAP_INVENTORY, UnderworldExhumed TRIGGER.C, with FindObj how 4,
		/// inv/INVDATA.C): the paperdoll slots 0 to 10, the backpack slots 11 to 18, then the
		/// contents of every object there that carries no count, depth first - never the object on
		/// the cursor. Only the FIRST object found is weighed: with a z above 0 a counted stack
		/// (no special link) must hold at least z pieces, else the chain ends. Until 2026-10-12
		/// ours searched the cursor too and kept looking past a stack too small (reference audit,
		/// traps D13). The one trap of the game: level 7 at 21/41, a crystal splinter, z 1.
		///
		/// The object number is compared whole - the original splits it into major, minor and
		/// class only because FindObj asks for them that way.
		/// </summary>
		private static bool fInventoryTrapMatches(UWObject pOTrap, IUWTrapHost pIHost)
		{
			int liWanted = UWObjectMechanics.GetInventoryTrapItemId(pOTrap);
			UWObject lOFound = FindCarriedLikeFindObj(fInventory(pIHost), liWanted,
				fLevel(pIHost) != null ? fLevel(pIHost).Masterlist : null);

			bool lbMatches = lOFound != null
				&& !(pOTrap.ZPos > 0 && lOFound.HasQuantity
					&& (lOFound.Quantity & UWObjectRecordCodec.SpecialLinkBit) == 0
					&& pOTrap.ZPos > lOFound.Quantity);

			UWTrapLog.Detail = string.Format("object {0} z {1}: {2}", liWanted, pOTrap.ZPos,
				lbMatches ? "present" : lOFound != null ? "too few" : "missing");

			return lbMatches;
		}

		/// <summary>The paperdoll slots and the backpack slots of the original's inventory.</summary>
		private const int InventoryTopSlots = 19;

		/// <summary>FindObj with how 4 for an object number - see fInventoryTrapMatches. Null when
		/// it is not carried.</summary>
		public static UWObject FindCarriedLikeFindObj(UWInventoryModel pOInventory, int piWantedId, List<UWObject> pOMasterlist)
		{
			if (pOInventory == null)
				return null;

			UWObject[] lOTop = new UWObject[InventoryTopSlots];
			UWObject[] lOEquipment = pOInventory.GetSavegameEquipment();

			for (int liAt = 0; liAt < lOEquipment.Length && liAt < InventoryTopSlots; liAt++)
				lOTop[liAt] = lOEquipment[liAt];

			for (int liAt = 0; liAt < pOInventory.Backpack.Length && lOEquipment.Length + liAt < InventoryTopSlots; liAt++)
				lOTop[lOEquipment.Length + liAt] = pOInventory.Backpack[liAt];

			foreach (UWObject lOItem in lOTop)
			{
				if (lOItem != null && lOItem.ID == piWantedId)
					return lOItem;
			}

			foreach (UWObject lOItem in lOTop)
			{
				if (lOItem == null || lOItem.HasQuantity)
					continue;

				UWObject lOFound = fFindInContents(lOItem, piWantedId, pOMasterlist, 0);

				if (lOFound != null)
					return lOFound;
			}

			return null;
		}

		/// <summary>find_obj on an object's contents: each in turn, and inside each that carries
		/// no count, depth first.</summary>
		private static UWObject fFindInContents(UWObject pOOwner, int piWantedId, List<UWObject> pOMasterlist, int piDepth)
		{
			if (piDepth > MaxTrapChainDepth)
				return null;

			if (pOOwner.Contents == null && pOOwner.GetCategory() == UWObject.ObjectCategoryEnum.Containers
				&& pOMasterlist != null)
				pOOwner.EnsureContentsLoaded(pOMasterlist);

			if (pOOwner.Contents == null)
				return null;

			foreach (UWObject lOItem in pOOwner.Contents)
			{
				if (lOItem == null)
					continue;

				if (lOItem.ID == piWantedId)
					return lOItem;

				if (lOItem.HasQuantity)
					continue;

				UWObject lOFound = fFindInContents(lOItem, piWantedId, pOMasterlist, piDepth + 1);

				if (lOFound != null)
					return lOFound;
			}

			return null;
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
			UWObject lOVictim = UWObjectMechanics.GetLinkedObject(pOTrap, fLevel(pIHost).Masterlist);

			return lOVictim != null && pIHost.World.Remove(lOVictim);
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
		/// on the trap. "quality" determines the action (1 open, 2 close, 3 toggle; any other value
		/// moves nothing - TRAP_DOOR, UnderworldExhumed TRIGGER.C).
		///
		/// Then there is the lock side (DoorTrap_loc_8F8DD, read 2026-10-06): a lock on the door
		/// is removed, then a copy of the template the trap's sp_link points to is put on, if
		/// there is one - so a repeated close trap leaves the same lock, it does not toggle. On
		/// level 1 all four traps chained like this point to an a_lock (271). A door in motion is
		/// an animation object then: its lock stays, and only a swing against the trap's wish is
		/// turned round (UWDoorRules.DoorTrap; until 2026-10-12 this comment said nothing happens).
		/// The door and its lock are the world's (IUWWorld.Doors; the trap host's FireDoorTrap until
		/// cleanup step 3.6, 2026-10-17).
		///
		/// THE RULE is UWDoorRules.DoorTrap (2026-10-12): on a door at rest the lock is replaced
		/// first, then the leaf acts by the trap's quality; on a door in motion the lock stays and
		/// only a swing against the trap's wish is turned round.
		///
		/// THE LOCK IS REPLACED, NOT TOGGLED (DoorTrap_loc_8F8DD labels 943 to 9F5, read
		/// 2026-10-06): an a_lock on the door is taken off and freed, then a copy of the template
		/// the trap links to is put on - the same lock however often it fires. Until 2026-10-06 a
		/// lock was removed when there was one and the template applied when there was none,
		/// which a repeated close trap turned into locked, unlocked, locked.
		///
		/// A DOOR IN MOTION: from 2026-10-06 to 2026-10-12 the trap skipped it altogether, after an
		/// IDA reading that saw only the door object branch - the byte-exact decompilation
		/// (UnderworldExhumed TRIGGER.C, TRAP_DOOR) has a second branch for the moving door 0x1CF.
		/// What the user saw on 2026-10-06 (the close trap before Drog's grate, level 1, 15/33,
		/// fired again while the grate came down and turned it round: down and up, over and over)
		/// was "close unless closed" on a grate that is not closed while it closes; the original's
		/// CloseDoor leaves a closing door alone (B10).</summary>
		private static void fFireDoorTrap(UWObject pOTrigger, UWObject pOTrap, IUWTrapHost pIHost)
		{
			(int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

			UWObject lODoorData = FindDoorInTile(liTileX, liTileY, fLevel(pIHost));

			if (lODoorData == null)
				return;

			UWObject lOLockTemplate = UWObjectMechanics.GetLinkedObject(pOTrap, fLevel(pIHost).Masterlist);
			IUWDoor lODoor = pIHost.World.Doors.Find(lODoorData);

			if (lODoor == null)
				return;

			UWDoorRules.TrapOutcome lOOutcome = UWDoorRules.DoorTrap(pOTrap.Quality, lODoor.IsHeadingClosed,
				lODoor.IsMoving);

			if (lOOutcome.ReplaceLock)
				lODoor.ReplaceLock(lOLockTemplate);

			// OpenDoor(CharacterThatTriggeredTrap, door): whoever set the chain off opens it.
			if (lOOutcome.Toggle)
				lODoor.Toggle(pIHost.Firing.Opener);
		}
	}
}
