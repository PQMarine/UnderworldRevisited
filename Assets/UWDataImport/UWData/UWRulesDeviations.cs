namespace UWDataImport.UWData
{
	/// <summary>
	/// THE DEVIATION SWITCHES of the rule set (deviations.*; cleanup phase 10, wave 2, 2026-10-26):
	/// where the port deliberately differs from the original (Docs/DEVIATIONS.md, Rules). Each is on
	/// by default - our deviation; switched off, the code takes the original's branch the class
	/// comment of the anchor describes. A switch only chooses, it does not change either path.
	///
	/// Only the deviations whose original branch is still in the code are switches. The others
	/// replaced the original's path (the stack limit 511, Gate Travel's search, the last map note,
	/// the gifts, the burial's bones, a full level's pickup, a night's flying objects, the arrival
	/// that fits nowhere, the typed spaces) and would need it written back first; the frame-rate
	/// rule and the manual attributes are no switches of the rule set (fixed rates; a Game menu
	/// option).
	/// </summary>
	public sealed class UWRulesDeviations
	{
		[UWRule(typeof(UWConversationPatches), nameof(UWConversationPatches.PatchesApplied),
			"Conversations run with the port's fixes of the original's script bugs (false: the scripts as shipped).")]
		public bool ConversationPatches = UWConversationPatches.PatchesApplied;

		[UWRule(typeof(UWTybalOrbRules), nameof(UWTybalOrbRules.ManaAsideOnlyWhileOrbStands),
			"A new maximum of mana on level 7 is held back only while Tybal's orb stands (false: on level 7 always).")]
		public bool OrbManaAsideOnlyWhileOrbStands = UWTybalOrbRules.ManaAsideOnlyWhileOrbStands;

		[UWRule(typeof(UWCreatureActor), nameof(UWCreatureActor.FollowerLandsWhereItCanStand),
			"A follower that jumps to the player refuses a solid tile and the wrong medium (false: any tile of the map).")]
		public bool FollowerLandsWhereItCanStand = UWCreatureActor.FollowerLandsWhereItCanStand;

		[UWRule(typeof(UWCreatureActor), nameof(UWCreatureActor.UnpushableNpcsApplied),
			"The creatures of story.unpushableNpcs (Judy) cannot be pushed over the edge of their ledge (false: as the original).")]
		public bool UnpushableNpcs = UWCreatureActor.UnpushableNpcsApplied;

		[UWRule(typeof(UWLevelWriter), nameof(UWLevelWriter.TidyLeakedObjectsApplied),
			"Saving frees the leaked copy of a stack split on a too heavy pickup in the original (false: the slot stays taken).")]
		public bool TidyLeakedObjects = UWLevelWriter.TidyLeakedObjectsApplied;

		[UWRule(typeof(UWObjectDamageRules), nameof(UWObjectDamageRules.DebrisLosesSpellApplied),
			"The debris of a destroyed potion or scroll drops the spell it carried (false: as the original, the pile casts it for ever). Set by the Game option Remove exploits of the original.")]
		public bool DebrisLosesSpell = UWObjectDamageRules.DebrisLosesSpellApplied;

		/// <summary>
		/// THE GAME WINDOW'S "FIX BUGS OF THE ORIGINAL" (bug plan, per user 2026-11-03: one option for all of them, on by
		/// default): the switches of the original's bugs that can cost the game, set together before a rules.json is
		/// read, so the file's key still decides a single switch.
		/// </summary>
		public void SetOriginalBugFixes(bool pbOn)
		{
			ConversationPatches = pbOn;
			UnpushableNpcs = pbOn;
			TidyLeakedObjects = pbOn;
		}

		/// <summary>
		/// THE GAME WINDOW'S "REMOVE EXPLOITS OF THE ORIGINAL" (per user 2026-11-07: a harmless exploit is no bug that
		/// costs the game, so an option of its own, OFF by default - the game plays as the original): the switches of the
		/// original's exploits, set before a rules.json is read like SetOriginalBugFixes. Only the debris that keeps a
		/// potion's spell so far.
		/// </summary>
		public void SetExploitFixes(bool pbOn)
		{
			DebrisLosesSpell = pbOn;
		}
	}
}
