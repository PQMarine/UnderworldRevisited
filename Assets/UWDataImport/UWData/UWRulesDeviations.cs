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
	}
}
