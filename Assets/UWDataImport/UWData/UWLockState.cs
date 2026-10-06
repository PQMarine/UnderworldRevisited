using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Runtime lock state of a door: whether it has a lock, whether that is locked, which key opens
	/// it and how hard it is to pick. Engine-free since 2026-09-17 (P1 of the engine separation);
	/// UWDoorLock on the Unity side holds one per door GameObject.
	///
	/// Resolved only on demand (EnsureResolved), not at spawn time - the same pattern as
	/// EnsureContentsLoaded for containers.
	/// </summary>
	public sealed class UWLockState
	{
		private bool mbResolved;
		private UWObject mOLockObject;

		public bool HasLock { get; private set; }

		public bool IsLocked { get; private set; }

		/// <summary>Whether anyone has asked for the lock. Before that it is untouched and stays in
		/// the data as it was loaded - saving then leaves it alone.</summary>
		public bool IsResolved => mbResolved;

		/// <summary>The lock object, for writing the locked bit back when saving.</summary>
		public UWObject LockObject => mOLockObject;

		public void EnsureResolved(UWObject pODoorData, List<UWObject> pOMasterlist)
		{
			if (mbResolved || pODoorData == null || pOMasterlist == null)
				return;

			mbResolved = true;

			pODoorData.EnsureContentsLoaded(pOMasterlist);

			mOLockObject = pODoorData.Contents.Count > 0 ? pODoorData.Contents[0] : null;
			HasLock = mOLockObject != null;
			IsLocked = HasLock && UWObjectMechanics.IsLockLocked(mOLockObject);
		}

		/// <summary>The key id that fits, -1 without a lock.</summary>
		public int LockId => mOLockObject != null ? UWObjectMechanics.GetLockId(mOLockObject) : -1;

		/// <summary>
		/// How hard the lock is to pick - the height value of the lock object, which is not a
		/// height here. The reference checks the lockpicking skill against three times that
		/// (lockpick.UseOn).
		/// </summary>
		public int LockDifficulty => mOLockObject != null ? mOLockObject.ZPos : 0;

		/// <summary>Raised after every change of the lock (unlock, lock, remove, apply), so the
		/// host writes the state into the level data at once (per user, 2026-10-05: everything
		/// game-visible goes into the data the moment it changes, not at the save).</summary>
		public event System.Action Changed;

		public void Unlock()
		{
			IsLocked = false;
			fRaiseChanged();
		}

		private void fRaiseChanged()
		{
			System.Action lOChanged = Changed;

			if (lOChanged != null)
				lOChanged();
		}

		/// <summary>The same key locks an unlocked door again (confirmed per user).</summary>
		public void Lock()
		{
			IsLocked = true;
			fRaiseChanged();
		}

		/// <summary>Removes the lock entirely, not just the locked state: per uw-formats.txt an
		/// a_door trap DELETES the associated a_lock. Afterwards the door has none and can be
		/// operated by hand permanently.</summary>
		public void RemoveLock()
		{
			mbResolved = true;
			mOLockObject = null;
			HasLock = false;
			IsLocked = false;
			fRaiseChanged();
		}

		/// <summary>Counterpart to RemoveLock: attaches a lock based on the template the a_door
		/// trap points to. Without a template nothing happens - the door then stays lockless
		/// instead of inventing a lock without a key id.</summary>
		public void ApplyLock(UWObject pOTemplate)
		{
			if (pOTemplate == null)
				return;

			mbResolved = true;
			mOLockObject = pOTemplate;
			HasLock = true;
			IsLocked = UWObjectMechanics.IsLockLocked(pOTemplate);
			fRaiseChanged();
		}
	}
}
