using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A door of the level as the rules see it (cleanup step 3.6, 2026-10-17): its object in the
	/// level data, where its leaf is, its lock, and the original's ways to move it. The Unity side
	/// is UWDoor, the door's component, which writes every change into the level data at once and
	/// fires the door's open trigger; the rules ask UWDoorRules whether the door is to move at all.
	///
	/// WHY: the night's closing doors, the door trap and gronk_door each reached for the scene's
	/// doors with a scan of their own on the host (section 4.3 of Docs/CLEANUP-PLAN.md); the night's
	/// rule could not be checked without a scene.
	/// </summary>
	public interface IUWDoor
	{
		/// <summary>The door object in the level data; null when the door has none.</summary>
		UWObject Data { get; }

		/// <summary>At rest and shut. False while the leaf moves, either way.</summary>
		bool IsClosed { get; }

		bool IsMoving { get; }

		/// <summary>Shut, or on its way to shut - what OpenDoor and CloseDoor of the original
		/// decide on.</summary>
		bool IsHeadingClosed { get; }

		/// <summary>The engine-free lock state; never null.</summary>
		UWLockState LockState { get; }

		/// <summary>Sets the leaf in motion, whatever the lock says (see UWDoor.Toggle).</summary>
		void Toggle(UWTrapRules.Setter peOpener);

		/// <summary>OpenDoor: moves the leaf when the door is closed or closing.</summary>
		void Open(UWTrapRules.Setter peOpener);

		/// <summary>CloseDoor: moves the leaf when the door is open or opening.</summary>
		void Close();

		/// <summary>The door trap's lock side: the lock goes, a copy of the template comes.</summary>
		void ReplaceLock(UWObject pOTemplate);
	}

	/// <summary>
	/// The doors of the current level (cleanup step 3.6, 2026-10-17): all of them, in the order
	/// they came into the scene, and the one of a door object.
	/// </summary>
	public interface IUWDoors
	{
		/// <summary>Every door of the level. A caller that changes doors while it walks them walks
		/// a copy: a door that moves writes into the data and may change the list.</summary>
		IReadOnlyList<IUWDoor> All { get; }

		/// <summary>The door of this door object in the level (the door trap's door, gronk_door's);
		/// null when it has none or there is no level.</summary>
		IUWDoor Find(UWObject pODoorData);
	}
}
