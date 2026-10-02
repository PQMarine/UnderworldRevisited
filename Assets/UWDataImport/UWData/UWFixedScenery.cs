using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Hand-maintained list of the objects that in the original stand visibly in the world and
	/// can be looked at, but can NOT be picked up - fixed or
	/// painted-on scenery.
	///
	/// Needed because UWObjectSpawner.fSpawnBillboard so far flatly marked every billboard object as
	/// pickable (CanBePickedUp = true for everything that is not a door/3D model/
	/// wall decoration/switch). The object data itself holds no field that
	/// distinguishes "scenery" from "item" - the category SceneryAndJunk contains
	/// both side by side (bones and pieces of wood can very well be picked up), hence
	/// a list of individual IDs instead of a category rule.
	///
	/// Deliberately only entries the user has checked in the original - the list is intentionally
	/// incomplete and grows with every confirmed observation, instead of making items
	/// unpickable on suspicion.
	///
	/// WHY THIS LIST REMAINS NECESSARY DESPITE COMOBJ.DAT: the file has a pickable bit
	/// (see UWCommonObjectProperties), and a comparison over the 32 IDs relevant here
	/// yielded 27 matches. The five deviations are no coincidence though - four of them
	/// are exactly the exceptions that uw-formats.txt itself lists: orb, campfire,
	/// fountain and cauldron have the bit set and still cannot be picked up. The
	/// bit alone is therefore not enough; this list remains the truth, but is now cross-checked
	/// against the game data instead of only against memory (with an editor audit, removed before the release).
	/// </summary>
	public static class UWFixedScenery
	{
		/// <summary>Checked by the user in the original (2026-08-28, level 1): "grass, stalagmites,
		/// bloodstain, campfire definitely cannot be picked up", afterwards additionally
		/// "pile of debris".
		///
		/// CAUTION - same description does NOT mean same behaviour: several IDs share
		/// the same text (3x "a_plant", 3x "a_blood stain", 4x "a_pile of debris", 2x
		/// "a_skull", 2x "a_bone", 2x "a_piece of wood"). Exactly this came to light with the
		/// plant case - the user could NOT pick up one "plant", but could pick up
		/// another (2026-08-28). Every ID therefore needs its OWN confirmation; inferring
		/// from one observation to the whole name group was wrong.
		///
		/// The blood stain and debris pile groups below still rest on exactly such a
		/// group inference and are therefore only partially verified - check them individually
		/// in the next test run.</summary>
		private static readonly HashSet<int> mOFixedIds = new HashSet<int>
		{
			192, // a_plant - CAUTION: ONLY this one of the three plant IDs is confirmed
			     //            unpickable, 206 and 207 are still open (see comment above).
			193, // some_grass
			208, // a_pile of debris - group inference, individually unconfirmed
			209, // a_pile of debris - group inference, individually unconfirmed
			210, // a_pile of debris - group inference, individually unconfirmed
			211, // a_stalactite
			215, // an_anvil - confirmed per user in the original, 2026-09-17 (COMOBJ.DAT pickable bit clear too)
			// 213 (a_pile of debris) was listed here as well, as a group inference from
			// 208/209/210 - and was wrong: COMOBJ.DAT sets the pickable bit for 213 as the only one
			// of the four debris IDs (comparison 2026-08-29 with an editor audit,
			// since removed). 208, 209 and 210 on the other hand are confirmed
			// as fixed by the file. Exactly the mistake the comment above warns about, only this time
			// uncovered by the game data instead of by the user. Cross-check in the original
			// in the next test run.
			218, // some_rubble
			219, // a_pile of wood chips
			220, // a_pile of bones - confirmed per user in the original, 2026-09-17 (what a skeleton leaves; COMOBJ.DAT pickable bit clear too, unlike 198)
			// a_fountain: not pickable, but has its OWN use effect - according to the
			// user (2026-08-28) it refreshes the player. Not implemented yet, see
			// level 1 checklist; here it is only listed as "not pickable".
			302,
			// a_cauldron: likewise not pickable and likewise with its own
			// use effect - when empty it prints "The cauldron is empty." (user,
			// 2026-08-28). What else can be done with it is still open.
			303,
			221, // a_blood stain - group inference, individually unconfirmed
			222, // a_blood stain - group inference, individually unconfirmed
			223, // a_blood stain - group inference, individually unconfirmed
			// an_orb: not pickable, on look it fires the a_text string trap on its
			// own tile (58/13) via its a_look trigger - confirmed per user, 2026-08-28. See
			// UWTriggerSystem.TryFireLookTrigger.
			279,
			298, // a_campfire
			// an_urn: confirmed per user in the original, 2026-09-30 (level 2, 24/36, six spikes
			// inside) - cannot be picked up although COMOBJ.DAT sets its pickable bit, like the
			// orb and the fountain; used, it spills like a barrel (Interaction.fTryUseObjectEffect).
			140,
			// The animated objects (0x01C0 to 0x01CF): water, fire, the silver tree and
			// the like. These are animations, not items - the fountain on tile
			// 8/55, for example, consists of the basin (302) AND the animated water (457),
			// lying on top of each other in the same tile (dump 2026-08-30).
			448, 449, 450, 451, 452, 453, 454, 455,
			456, 457, 458, 459, 460, 461, 462, 463,
		};

		public static bool IsFixed(int piObjectId)
		{
			return mOFixedIds.Contains(piObjectId);
		}
	}
}
