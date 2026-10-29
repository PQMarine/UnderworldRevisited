namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIRST GAME'S CONVERSATION STEPS (phase 0 step 2, 2026-10-28): what UWClickRules and
	/// UWConversationTrade did at these steps until then, moved here unchanged.
	/// </summary>
	public sealed class UWConversationVariantUw1 : IUWConversationVariant
	{
		/// <summary>Whoami 0x16 always talks.</summary>
		private const int AlwaysTalksWhoami = 0x16;

		/// <summary>Rodrick always talks.</summary>
		private const int RodrickWhoami = 0x8E;

		/// <summary>Tyball always talks.</summary>
		private const int TyballWhoami = 0xE7;

		public bool AlwaysAnswers(int piWhoami)
		{
			return piWhoami == AlwaysTalksWhoami || piWhoami == RodrickWhoami || piWhoami == TyballWhoami;
		}

		/// <summary>The player wins when his score is higher - and an ally gives in whatever the
		/// scores say.</summary>
		public bool DemandGivesIn(int piPlayerScore, int piNpcScore, bool pbAlly)
		{
			return piPlayerScore > piNpcScore || pbAlly;
		}

		/// <summary>Down to 0.</summary>
		public bool GivingInLowersAttitude(int piAttitude)
		{
			return piAttitude > 0;
		}
	}
}
