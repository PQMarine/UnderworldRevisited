namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STEPS OF THE CONVERSATION RULES WHERE THE GAMES PART (Docs/UW2-PLAN.md section 4.3, phase
	/// 0 step 2, 2026-10-28), from Underworld Exhumed's list of differences
	/// (docs/UW1-UW2-DIFFERENCES.md, "Conversations"). The numbers that differ (the quest bits and
	/// bytes, x_traps' range, the trade slots a side) are each game's data or rule set entries; what
	/// only the second game has (x_skills' skill point, the refusals while time stands still or to a
	/// held critter, the wisp's conversation, the empty demand turned down, the fudge, part of a
	/// stack, the new built-ins) comes with its variant in phase 4.
	/// </summary>
	public interface IUWConversationVariant
	{
		/// <summary>Whether a creature with this whoami answers whatever it is doing (UW1: 0x16,
		/// Rodrick and Tyball, CONVERSE.C:136; UW2: 0x8C, CONVERSE.C:117).</summary>
		bool AlwaysAnswers(int piWhoami);

		/// <summary>Whether the partner gives in to a demand (UW1: when the player scores higher,
		/// and an ally always, BARTER.C:719; UW2: an ally only scores one lower, BARTER.C:694).</summary>
		bool DemandGivesIn(int piPlayerScore, int piNpcScore, bool pbAlly);

		/// <summary>Whether giving in lowers the partner's attitude by one step (UW1: while above 0,
		/// BARTER.C:723; UW2: while above 1, BARTER.C:700).</summary>
		bool GivingInLowersAttitude(int piAttitude);
	}
}
