namespace UWDataImport.UWData
{
	/// <summary>
	/// THE QUIRK SWITCHES of the rule set (quirks.*; cleanup phase 10, wave 2, 2026-10-26): slips of
	/// the original that the port keeps as it plays (Docs/MOD-SUPPORT-PLAN.md section 2.2). Each is
	/// on by default - the original's behaviour; switched off, the code takes the other branch the
	/// class comment of the anchor describes. A switch only chooses, it does not change either path.
	///
	/// Only the quirks whose other branch is one condition are switches. The rest of the plan's list
	/// would need a second path written first (Strengthen Door, the north-west ambush spot, the
	/// repair loss, the shared burn counter, the weight total, 6-bit game variables, the Lore reset,
	/// the free skill point, the quality-0 damage trap, the flank bonus, Acrobat), and the XP gate
	/// is already a number (experience.awardCap).
	/// </summary>
	public sealed class UWRulesQuirks
	{
		[UWRule(typeof(UWArmourProtection), nameof(UWArmourProtection.ToughnessTwiceOnBody),
			"Toughness on a ring or a shield goes twice onto the body and not onto the hands (false: once to each).")]
		public bool ToughnessTwiceOnBody = UWArmourProtection.ToughnessTwiceOnBody;

		[UWRule(typeof(UWObjectMechanics), nameof(UWObjectMechanics.LitTaperStaysLit),
			"A lit taper put into the world stays lit (false: it goes out like the other lights).")]
		public bool LitTaperStaysLit = UWObjectMechanics.LitTaperStaysLit;

		[UWRule(typeof(UWCreatureMotion), nameof(UWCreatureMotion.LavaProofSpreadsToKind),
			"One lava-proof creature lets every creature of its kind walk over lava edges for the rest of the game (false: only itself).")]
		public bool LavaProofSpreadsToKind = UWCreatureMotion.LavaProofSpreadsToKind;

		[UWRule(typeof(UWShrineRules), nameof(UWShrineRules.GroupMantraSpendsPointRegardless),
			"A group mantra spends the skill point even when no skill rose (false: the point is kept).")]
		public bool GroupMantraSpendsPointRegardless = UWShrineRules.GroupMantraSpendsPointRegardless;
	}
}
