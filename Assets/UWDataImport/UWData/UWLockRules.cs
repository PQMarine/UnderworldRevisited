using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Lockpick, key and the Unlock spell on a lock - a door's (UWLockState) or the one in the
	/// chain of a barrel or chest (a lock object). Engine-free since 2026-09-18 (P3 of the engine
	/// separation), out of Interaction, which keeps the door component, the sound and the
	/// inventory.
	///
	/// ONE ROUTINE DECIDES ALL OF THEM in the original, UnlockDoor_seg040_352B_1D3B (read whole
	/// 2026-09-25, with its callers PickLock_seg040_498, UseKey_seg040_51D, the Unlock spell and
	/// the creatures' door opening in segment 6). It takes a KEY as the key's id (owner & 0x3F) and
	/// a PICK as MINUS the skill: the player passes his Picklock skill, the Unlock spell 45, a
	/// creature byte 0x2E of its row. See Unlock.
	///
	/// Until 2026-09-25 ours followed the reference instead: the pick got skill PLUS ONE, the
	/// hard lock wanted 32, a critical failure could break the pick ("You broke your pick." -
	/// a line UW1 does not have), a picked door opened by itself, a key fitted a lock of id 0
	/// and locked an open door, the Unlock spell always worked and only on doors, and no
	/// lock ever went away.
	/// </summary>
	public static class UWLockRules
	{
		// Numbers in string block 1, in OUR numbering.
		private const int LockpickFailedMessage = 121;

		private const int LockpickSucceededMessage = 122;

		public const int NotLockedMessage = 123;

		private const int NoLockMessage = 4;

		/// <summary>"The key does not fit." - UseKey prints this line plus the outcome
		/// (Outcome.Failed 0 to Outcome.NotLocked 4, "That is already open.").</summary>
		public const int FirstKeyMessage = 3;

		private const int SpellUnlocksMessage = 271;

		private const int SpellNoEffectMessage = UWStringIds.SpellNoEffectMessage;

		/// <summary>This difficulty is never picked.</summary>
		private const int UnpickableLockDifficulty = 0xF;

		/// <summary>This difficulty needs a skill of at least HardLockSkill.</summary>
		private const int HardLockDifficulty = 0xE;

		private const int HardLockSkill = 0x1F;

		/// <summary>The skill the Unlock spell picks with (it passes 0xFFD3).</summary>
		public const int UnlockSpellSkill = 45;

		/// <summary>What UnlockDoor returns.</summary>
		public enum Outcome
		{
			/// <summary>The key does not fit, the pick failed, or nothing to do.</summary>
			Failed = 0,

			NoLock = 1,

			/// <summary>The key locked the lock.</summary>
			Locked = 2,

			Unlocked = 3,

			/// <summary>Not locked (pick), or the door or container is open (key).</summary>
			NotLocked = 4,

			/// <summary>The second game's checkLock only: the pick's roll was a critical failure (skill_check's -1).</summary>
			Fumbled = 5
		}

		/// <summary>The outcome of a lockpick attempt.</summary>
		public struct PickResult
		{
			/// <summary>A line of string block 1.</summary>
			public int MessageIndex;

			/// <summary>The lock sprang open. The door does NOT open with it.</summary>
			public bool Unlocked;

			/// <summary>The second game's fumble: the pick breaks and is used up (UseLockpickOn's using_punt).</summary>
			public bool Broke;
		}

		/// <summary>
		/// UnlockDoor. piKeyOrMinusSkill is a key id (above 0) or minus a picking skill.
		///
		/// NO LOCK: 1. A LOCKED LOCK: a pick fails on difficulty 15, on 14 below a skill of 31,
		/// otherwise it needs a success of skill against three times the difficulty (the lock
		/// object's zpos); a key must equal the lock's id, and a lock of id 0 takes no key. On
		/// success 3 - and a lock without its keep bit (UWObjectMechanics.IsLockKeptWhenOpened)
		/// leaves the door or container for good: 3 of the 84 locks in the nine levels (the chest
		/// on level 1 at 3/50, the door on level 3 at 15/26, the chest on level 4 at 19/20).
		/// AN UNLOCKED LOCK: a pick gets 4; a key gets 4 on an open door or container, locks a
		/// fitting lock (2) and fails otherwise.
		///
		/// NOT BUILT: before the lock goes, the original fires the target's linked trigger with
		/// event 6 (TriggerObjectLink) - which trigger answers event 6 is not read.
		/// </summary>
		public static Outcome Unlock(UWLockState pODoorLock, UWObject pOContainer, UWObject pOContainerLock,
			bool pbTargetIsOpen, int piKeyOrMinusSkill, bool pbSecondGame = false)
		{
			if ((pODoorLock == null || !pODoorLock.HasLock) && pOContainerLock == null)
				return Outcome.NoLock;

			UWObject lOLock = pODoorLock != null ? pODoorLock.LockObject : pOContainerLock;
			bool lbLocked = pODoorLock != null ? pODoorLock.IsLocked : UWObjectMechanics.IsLockLocked(pOContainerLock);
			int liLockId = lOLock != null ? lOLock.Quantity & 0x1FF : 0;

			if (!lbLocked)
			{
				if (piKeyOrMinusSkill <= 0 || pbTargetIsOpen)
					return Outcome.NotLocked;

				if (liLockId != piKeyOrMinusSkill)
					return Outcome.Failed;

				if (pODoorLock != null)
					pODoorLock.Lock();
				else
					UWObjectMechanics.SetLockLocked(pOContainerLock, true);

				return Outcome.Locked;
			}

			if (piKeyOrMinusSkill < 0)
			{
				int liSkill = -piKeyOrMinusSkill;
				int liDifficulty = lOLock != null ? lOLock.ZPos : 0;

				// THE SECOND GAME'S checkLock (uw2 obj/OBJUSE.C) rolls first, on every lock: a critical failure is 5 (the
				// lockpick breaks unless Dexterity saves it, PickLockSecondGame), the hard lock wants 0x20 of the skill it is
				// handed - its lockpick hands Picklock + 1, so the same 31 of Picklock as the first game's.
				if (pbSecondGame)
				{
					UWSkillCheck.ResultEnum leRoll = UWGameSession.Current.Game.Objects.PickRoll(liSkill, liDifficulty);

					if ((liDifficulty == HardLockDifficulty && liSkill < HardLockSkill + 1)
						|| liDifficulty == UnpickableLockDifficulty || leRoll == UWSkillCheck.ResultEnum.Failure)
						return leRoll == UWSkillCheck.ResultEnum.CriticalFailure ? Outcome.Fumbled : Outcome.Failed;

					if (leRoll == UWSkillCheck.ResultEnum.CriticalFailure)
						return Outcome.Fumbled;
				}
				else if ((liDifficulty == HardLockDifficulty && liSkill < HardLockSkill)
					|| liDifficulty == UnpickableLockDifficulty)
					return Outcome.Failed;

				if (!pbSecondGame && !UWSkillCheck.IsSuccess(UWGameSession.Current.Game.Objects.PickRoll(liSkill, liDifficulty)))
					return Outcome.Failed;
			}
			else if (piKeyOrMinusSkill == 0 || liLockId == 0 || liLockId != piKeyOrMinusSkill)
			{
				return Outcome.Failed;
			}

			bool lbKeep = UWObjectMechanics.IsLockKeptWhenOpened(lOLock);

			if (pODoorLock != null)
			{
				if (lbKeep)
					pODoorLock.Unlock();
				else
					pODoorLock.RemoveLock();
			}
			else if (lbKeep)
			{
				UWObjectMechanics.SetLockLocked(pOContainerLock, false);
			}
			else if (pOContainer != null && pOContainer.Contents != null)
			{
				pOContainer.Contents.Remove(pOContainerLock);
			}

			return Outcome.Unlocked;
		}

		/// <summary>The lock object in the chain of a barrel or chest, or null.</summary>
		public static UWObject FindContainerLock(UWObject pOContainer, List<UWObject> pOMasterlist)
		{
			if (pOContainer == null || pOMasterlist == null)
				return null;

			pOContainer.EnsureContentsLoaded(pOMasterlist);

			return FindLoadedLock(pOContainer);
		}

		/// <summary>The lock object among what a thing already holds, or null - for a carried thing,
		/// whose contents the inventory has resolved and whose links do not point into the level's
		/// list. checkLock (UnderworldExhumed obj/USEITEMS.C) looks into any thing's chain, carried
		/// or not (2026-10-15).</summary>
		public static UWObject FindLoadedLock(UWObject pOThing)
		{
			if (pOThing == null || pOThing.Contents == null)
				return null;

			foreach (UWObject lOItem in pOThing.Contents)
				if (lOItem != null && lOItem.ID == UWObjectMechanics.LockObjectId)
					return lOItem;

			return null;
		}

		/// <summary>
		/// Pick a lock (PickLock_seg040_498): Unlock with minus the skill, then the line - failed,
		/// no lock, not locked, or on success the lockpick sound (the caller) and "You succeed in
		/// picking the lock.". The pick is never lost.
		/// </summary>
		public static PickResult PickLock(UWLockState pODoorLock, UWObject pOContainer, UWObject pOContainerLock,
			int piPicklockSkill)
		{
			Outcome leOutcome = Unlock(pODoorLock, pOContainer, pOContainerLock, false, -piPicklockSkill);

			PickResult lOResult = new PickResult { Unlocked = leOutcome == Outcome.Unlocked };

			switch (leOutcome)
			{
				case Outcome.NoLock: lOResult.MessageIndex = NoLockMessage; break;
				case Outcome.NotLocked: lOResult.MessageIndex = NotLockedMessage; break;
				case Outcome.Unlocked: lOResult.MessageIndex = LockpickSucceededMessage; break;
				default: lOResult.MessageIndex = LockpickFailedMessage; break;
			}

			return lOResult;
		}

		// ------------------------------------------------- The second game (G4 part A, 2026-11-13)

		/// <summary>"There is no lock on that." (string 3 of block 1, ours + 1).</summary>
		public const int NoLockStringUw2 = 3 + 1;

		/// <summary>"Your lockpicking attempt failed." (0x85).</summary>
		public const int PickFailedStringUw2 = 0x85 + 1;

		/// <summary>"You broke your pick." (0x86).</summary>
		public const int PickBrokeStringUw2 = 0x86 + 1;

		/// <summary>"You succeed in picking the lock." (0x87).</summary>
		public const int PickSucceededStringUw2 = 0x87 + 1;

		/// <summary>"That is not locked." (0x88).</summary>
		public const int NotLockedStringUw2 = 0x88 + 1;

		/// <summary>The Dexterity roll that saves a fumbled pick (skill_check(dexterity, 20) &gt; 0).</summary>
		private const int PickSavedByDexterity = 20;

		/// <summary>
		/// UseLockpickOn (uw2 obj/USEITEMS.C): checkLock with minus (Picklock + 1). A fumble breaks the pick unless a
		/// Dexterity roll against 20 succeeds (then it only failed); without any Picklock an unlocked lock says the attempt
		/// failed rather than "That is not locked.". The lines: no lock 3, broke 0x86, failed 0x85, not locked 0x88, picked
		/// 0x87 (with the caller's click, sound 0x13).
		/// </summary>
		public static PickResult PickLockSecondGame(UWLockState pODoorLock, UWObject pOContainer, UWObject pOContainerLock,
			int piPicklockSkill, int piDexterity)
		{
			int liSkill = piPicklockSkill + 1;
			Outcome leOutcome = Unlock(pODoorLock, pOContainer, pOContainerLock, false, -liSkill, true);

			if ((leOutcome == Outcome.Fumbled && UWSkillCheck.IsSuccess(UWSkillCheck.Check(piDexterity, PickSavedByDexterity)))
				|| (leOutcome == Outcome.NotLocked && liSkill == 1))
				leOutcome = Outcome.Failed;

			PickResult lOResult = new PickResult { Unlocked = leOutcome == Outcome.Unlocked, Broke = leOutcome == Outcome.Fumbled };

			switch (leOutcome)
			{
				case Outcome.NoLock: lOResult.MessageIndex = NoLockStringUw2; break;
				case Outcome.Fumbled: lOResult.MessageIndex = PickBrokeStringUw2; break;
				case Outcome.NotLocked: lOResult.MessageIndex = NotLockedStringUw2; break;
				case Outcome.Unlocked: lOResult.MessageIndex = PickSucceededStringUw2; break;
				default: lOResult.MessageIndex = PickFailedStringUw2; break;
			}

			return lOResult;
		}

		/// <summary>
		/// checkLock's "open" for a key (uw1 obj/USEITEMS.C:1015-1017, uw2 obj/OBJUSE.C): an open door (index 8 and up) or an
		/// open container (index below 0xC with the odd id) answers "That is already open." when the lock is unlocked. Both
		/// games since cleanup plan 2, K24 (2026-11-28; the first game asked doors only and let a key lock an open chest; the
		/// only open container holding a lock in the shipped game, a coffer on level 3, is linked to nothing - latent).
		/// </summary>
		public static bool IsOpenForKey(UWObject pOTarget)
		{
			if (pOTarget == null)
				return false;

			int liClass = pOTarget.ID & 0x1F0;
			int liIndex = pOTarget.ID & 0xF;

			return (liClass == SecondGameDoorClass && liIndex >= 8) || (liClass == SecondGameContainerClass && liIndex < 0xC && (pOTarget.ID & 1) != 0);
		}

		private const int SecondGameDoorClass = 0x140;

		private const int SecondGameContainerClass = 0x80;

		/// <summary>
		/// A key on a lock (UseKey_seg040_51D): unlocks a locked lock, locks an unlocked one, and
		/// never opens the door. The line is "The key does not fit." plus the outcome. Returns -1
		/// when the item is no key.
		/// </summary>
		public static int ToggleWithKey(UWLockState pODoorLock, UWObject pOContainer, UWObject pOContainerLock,
			bool pbTargetIsOpen, UWObject pOKey)
		{
			if (pOKey == null || pOKey.GetCategory() != UWObject.ObjectCategoryEnum.KeysLockpickLock)
				return -1;

			// UseKeyOn of the second game is the same: checkLock with the key's owner, game_sprint(result + 2) - its lines
			// stand where the first game's do (ours 3 to 7).
			return FirstKeyMessage + (int)Unlock(pODoorLock, pOContainer, pOContainerLock, pbTargetIsOpen,
				UWObjectMechanics.GetKeyId(pOKey));
		}

		/// <summary>The Unlock spell: Unlock with a pick of skill 45, then "The spell unlocks the
		/// lock." or "The spell has no discernable effect.".</summary>
		public static int CastUnlock(UWLockState pODoorLock, UWObject pOContainer, UWObject pOContainerLock)
		{
			return Unlock(pODoorLock, pOContainer, pOContainerLock, false, -UnlockSpellSkill) == Outcome.Unlocked
				? SpellUnlocksMessage : SpellNoEffectMessage;
		}
	}
}
