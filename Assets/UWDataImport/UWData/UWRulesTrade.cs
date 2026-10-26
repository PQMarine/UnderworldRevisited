namespace UWDataImport.UWData
{
	/// <summary>
	/// THE TRADE RULES of the rule set (trade.*; cleanup phase 10, wave 2, 2026-10-26): what a
	/// partner rolls at the barter's start, how he lays out his goods, what an offer, a demand and
	/// the player's judgement weigh, and the Lore checks on the trade area (UWConversationTrade,
	/// UWConversationSession). Each default is the const of the class that applies it; see UWRules.
	/// The 3/2 of a liked good is a shift ((v * 3) &gt;&gt; 1) and stays code.
	/// </summary>
	public sealed class UWRulesTrade
	{
		public readonly ThresholdRules Threshold = new ThresholdRules();

		public readonly PatienceRules Patience = new PatienceRules();

		public readonly AppraisalRules Appraisal = new AppraisalRules();

		public readonly LayoutRules Layout = new LayoutRules();

		public readonly DemandRules Demand = new DemandRules();

		public readonly JudgementRules Judgement = new JudgementRules();

		public readonly LoreRules Lore = new LoreRules();

		/// <summary>trade.threshold.*: the evaluation (in percent) an offer must reach - the partner's
		/// row value times perPoint, moved by noiseLow to noiseHigh percent, less the player's Charm
		/// times charmFactor.</summary>
		public sealed class ThresholdRules
		{
			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.ThresholdPerPoint),
				"The partner's threshold: his creature row's trade value times this.")]
			public int PerPoint = UWConversationTrade.ThresholdPerPoint;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.ThresholdNoiseLow),
				"The threshold is moved by at least this many percent (negative: down).")]
			public int NoiseLow = UWConversationTrade.ThresholdNoiseLow;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.ThresholdNoiseHigh),
				"The threshold is moved by at most this many percent.")]
			public int NoiseHigh = UWConversationTrade.ThresholdNoiseHigh;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.ThresholdCharmFactor),
				"The player's Charm times this comes off the threshold.")]
			public int CharmFactor = UWConversationTrade.ThresholdCharmFactor;
		}

		/// <summary>trade.patience.*: how many bad offers the partner bears - his row value moved by
		/// noiseLow to noiseHigh percent, plus Charm / charmDivisor; a worse offer than the last costs
		/// insultCost, a better one that is still too little tooLittleCost.</summary>
		public sealed class PatienceRules
		{
			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.PatienceNoiseLow),
				"The partner's patience is moved by at least this many percent (negative: down).")]
			public int NoiseLow = UWConversationTrade.PatienceNoiseLow;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.PatienceNoiseHigh),
				"The partner's patience is moved by at most this many percent.")]
			public int NoiseHigh = UWConversationTrade.PatienceNoiseHigh;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.PatienceCharmDivisor),
				"The player's Charm divided by this adds to the patience.", Minimum = 1)]
			public int CharmDivisor = UWConversationTrade.PatienceCharmDivisor;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.InsultPatienceCost),
				"Patience an offer worse than the last one costs.")]
			public int InsultCost = UWConversationTrade.InsultPatienceCost;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.TooLittlePatienceCost),
				"Patience a better offer that is still too little costs.")]
			public int TooLittleCost = UWConversationTrade.TooLittlePatienceCost;
		}

		/// <summary>trade.appraisal.*: how far off the partner values the goods - (base - his row
		/// value) times perPoint percent, moved by noiseLow to noiseHigh percent.</summary>
		public sealed class AppraisalRules
		{
			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.AppraisalBase),
				"The partner's appraisal: this minus his creature row's appraisal value, times perPoint percent off.")]
			public int Base = UWConversationTrade.AppraisalBase;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.AppraisalPerPoint),
				"The partner's appraisal: see base.")]
			public int PerPoint = UWConversationTrade.AppraisalPerPoint;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.AppraisalNoiseLow),
				"The appraisal's error is moved by at least this many percent (negative: down).")]
			public int NoiseLow = UWConversationTrade.AppraisalNoiseLow;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.AppraisalNoiseHigh),
				"The appraisal's error is moved by at most this many percent.")]
			public int NoiseHigh = UWConversationTrade.AppraisalNoiseHigh;
		}

		/// <summary>trade.layout.*: the partner lays out his goods (setup_to_barter) - at most
		/// lookLimit items looked at; once the four slots are full an item is passed over when a roll
		/// of 0 to overflowRoll - 1 is below overflowSkipBelow.</summary>
		public sealed class LayoutRules
		{
			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.BarterLookLimit),
				"The partner looks at this many of his items at most when he lays out his goods.", Minimum = 0)]
			public int LookLimit = UWConversationTrade.BarterLookLimit;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.BarterOverflowRoll),
				"With his four slots full, a roll of 0 to this minus one decides about the next item.", Minimum = 1)]
			public int OverflowRoll = UWConversationTrade.BarterOverflowRoll;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.BarterOverflowSkipBelow),
				"... and a roll below this passes the item over.")]
			public int OverflowSkipBelow = UWConversationTrade.BarterOverflowSkipBelow;
		}

		/// <summary>trade.demand.*: the player's score against the partner's when he demands goods
		/// (do_demand) - health, level, Charm / charmDivisor and weaponBonus with the weapon drawn,
		/// against health, the goods' value / goodsDivisor, the conversation level and a mood.</summary>
		public sealed class DemandRules
		{
			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.DemandCharmDivisor),
				"A demand: the player's Charm divided by this adds to his score.", Minimum = 1)]
			public int CharmDivisor = UWConversationTrade.DemandCharmDivisor;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.DemandWeaponBonus),
				"A demand: what a drawn weapon adds to the player's score.")]
			public int WeaponBonus = UWConversationTrade.DemandWeaponBonus;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.DemandGoodsDivisor),
				"A demand: the demanded goods' value divided by this adds to the partner's score.", Minimum = 1)]
			public int GoodsDivisor = UWConversationTrade.DemandGoodsDivisor;
		}

		/// <summary>trade.judgement.*: the player's own appraisal of a deal (do_judgement) - an
		/// error of accuracyBase - Appraise * appraiseNumerator / appraiseDenominator percent, and the
		/// evaluations the nine verdicts start above.</summary>
		public sealed class JudgementRules
		{
			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.JudgementAccuracyBase),
				"The player's appraisal is off by up to this many percent, before his Appraise skill.")]
			public int AccuracyBase = UWConversationTrade.JudgementAccuracyBase;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.JudgementAppraiseNumerator),
				"The Appraise skill times this, divided by appraiseDenominator, comes off the error.")]
			public int AppraiseNumerator = UWConversationTrade.JudgementAppraiseNumerator;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.JudgementAppraiseDenominator),
				"See appraiseNumerator.", Minimum = 1)]
			public int AppraiseDenominator = UWConversationTrade.JudgementAppraiseDenominator;

			[UWRule(typeof(UWConversationTrade), nameof(UWConversationTrade.miJudgementVerdictAbove),
				"The evaluations (in percent) the verdicts start above, best first (8 numbers; below all, the ninth).")]
			public int[] VerdictAbove = (int[])UWConversationTrade.miJudgementVerdictAbove.Clone();
		}

		/// <summary>trade.lore.*: the Lore check's target when the player looks at a good on the
		/// trade area (UWConversationSession).</summary>
		public sealed class LoreRules
		{
			[UWRule(typeof(UWLoreCheck), nameof(UWLoreCheck.OwnTradeTarget),
				"The Lore check's target for a good on the player's side of the trade.")]
			public int OwnTarget = UWLoreCheck.OwnTradeTarget;

			[UWRule(typeof(UWLoreCheck), nameof(UWLoreCheck.PartnerTradeTarget),
				"The Lore check's target for a good on the partner's side of the trade.")]
			public int PartnerTarget = UWLoreCheck.PartnerTradeTarget;
		}
	}
}
