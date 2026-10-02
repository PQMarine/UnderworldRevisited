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
		private const int FirstKeyMessage = 3;

		private const int SpellUnlocksMessage = 271;

		private const int SpellNoEffectMessage = 272;

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
			NotLocked = 4
		}

		/// <summary>The outcome of a lockpick attempt.</summary>
		public struct PickResult
		{
			/// <summary>A line of string block 1.</summary>
			public int MessageIndex;

			/// <summary>The lock sprang open. The door does NOT open with it.</summary>
			public bool Unlocked;
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
			bool pbTargetIsOpen, int piKeyOrMinusSkill)
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

				if ((liDifficulty == HardLockDifficulty && liSkill < HardLockSkill)
					|| liDifficulty == UnpickableLockDifficulty)
					return Outcome.Failed;

				if (!UWSkillCheck.IsSuccess(UWSkillCheck.Check(liSkill, liDifficulty * 3)))
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

			if (pOContainer.Contents == null)
				return null;

			foreach (UWObject lOItem in pOContainer.Contents)
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
