namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S CONVERSATION STEPS (UW2 plan phase 3 step 7), from Underworld Exhumed's uw2
	/// conv/CONVERSE.C and conv/BARTER.C.
	/// </summary>
	public sealed class UWConversationVariantUw2 : IUWConversationVariant
	{
		/// <summary>Whoami 0x8C talks whatever it is doing (CONVERSE.C:117).</summary>
		private const int AlwaysTalksWhoami = 0x8C;

		public bool AlwaysAnswers(int piWhoami)
		{
			return piWhoami == AlwaysTalksWhoami;
		}

		/// <summary>The scores alone: an ally's mood of -1 is in its score (BARTER.C:694).</summary>
		public bool DemandGivesIn(int piPlayerScore, int piNpcScore, bool pbAlly)
		{
			return piPlayerScore > piNpcScore;
		}

		/// <summary>Down to 1 (BARTER.C:700).</summary>
		public bool GivingInLowersAttitude(int piAttitude)
		{
			return piAttitude > 1;
		}
	}
}
