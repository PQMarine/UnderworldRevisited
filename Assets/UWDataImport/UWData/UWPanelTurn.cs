using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PANEL'S TURN IN ONE PLACE (cleanup 5.9, 2026-10-20): which page the turning panel on the
	/// right shows (the inventory, the stats, the rune shelf), where a pull of the chain or a click
	/// on the rune bag turns it, the eight steps of a turn with the panel's edge in the middle and
	/// the chain's picture per step, and a conversation forcing the inventory page and giving the
	/// page back afterwards. Until then the classic HUD's UWHudPanel kept all of it in fields of its
	/// own and the modern scheme's Classic Wide frame (UWModernClassicFrame) a second copy of the
	/// step rule.
	///
	/// THE STEP TIMING STAYS WITH THE UNITY CONTROLLERS (section 8.3 of the cleanup plan): they
	/// call Advance when a step's time has come - the first at once, then every
	/// UWGameUI.mfPanelStepSeconds on the unscaled clock, as before. This class only says what each
	/// step shows.
	///
	/// ROTATION: eight steps (UWClassicLayout.Panel.TurnSteps), at step four you see the panel from
	/// the side and thus only a line (per user, 2026-09-02). Drawn as squashing the page's width by
	/// the cosine; on the edge step the panel's own edge picture (PANELS.GR 3) stands instead, and
	/// there the page flips over. The chain turns along in its own eight pictures, one per step.
	///
	/// THE SECOND GAME'S PANEL SLIDES (Slides; do_panel_frame, uw2 ui/PANELS.C:816-843, UWClassicLayoutUw2.PanelSlide;
	/// UW2 plan stage U7 step 2b, 2026-11-24): twenty frames, the old page moving right eight columns a frame for ten
	/// frames, then the new one coming in from the right (Offset) - the page flips on the eleventh; CHAINS.GR 0 to 3 on
	/// frames 1, 4, 13 and 16 (ChainFrame, -1 between). The same sides, chain and rune bag targets and conversation hold.
	/// </summary>
	public sealed class UWPanelTurn
	{
		/// <summary>The three pages. Until 2026-09-03 there were two, as a bool - the rune bag added
		/// a third. The numbers are UWModernClassicFrame.PageEnum's.</summary>
		public enum SideEnum
		{
			Inventory = 0,
			Stats = 1,
			Runes = 2
		}

		/// <summary>Steps of a turn.</summary>
		public const int Steps = UWClassicLayout.Panel.TurnSteps;

		/// <summary>The step on which the panel stands on its edge and the page flips: step four of
		/// eight, exactly where the chain also has its edge picture.</summary>
		public const int EdgeStep = Steps / 2;

		/// <summary>What one step of a turn shows.</summary>
		public struct Frame
		{
			/// <summary>The turn is over: the page at full width, no edge, the chain at rest
			/// (picture 0).</summary>
			public bool Finished;

			/// <summary>The step shown, 0 to Steps - 1.</summary>
			public int Step;

			/// <summary>The page's width as a fraction: the cosine's size over 180 degrees, 0 on the
			/// edge step.</summary>
			public float Scale;

			/// <summary>The panel stands on its edge: its edge picture shows instead of the page.</summary>
			public bool OnEdge;

			/// <summary>The chain's picture (and the short piece's): one per step, without skipping.</summary>
			public int ChainFrame;

			/// <summary>The page flipped on this step: the new Side shows from now on.</summary>
			public bool Flipped;

			/// <summary>A slide's step (Slides): the columns the page shown lies right of its place.</summary>
			public int Offset;
		}

		/// <summary>The panel slides instead of turning (the second game's); set before Begin.</summary>
		public bool Slides { get; set; }

		/// <summary>Steps of the turn or the slide.</summary>
		public int StepCount => Slides ? UWClassicLayoutUw2.PanelSlide.Frames : Steps;

		/// <summary>The page shown (during a turn: until the edge step the old one).</summary>
		public SideEnum Side { get; private set; } = SideEnum.Inventory;

		/// <summary>The page a turn under way turns to.</summary>
		public SideEnum Target { get; private set; }

		/// <summary>The next step to show; -1 at rest.</summary>
		public int Step { get; private set; } = -1;

		public bool IsTurning
		{
			get { return Step >= 0; }
		}

		/// <summary>The page shows and stands still - its clicks may hit (a turned page leaves its
		/// hit areas behind, and during a turn they lie askew; per user, 2026-09-03).</summary>
		public bool IsAtRestOn(SideEnum peSide)
		{
			return Side == peSide && !IsTurning;
		}

		/// <summary>
		/// The chain leads from the inventory to the stats page - and from every other page back to
		/// the inventory. From the rune shelf it therefore does NOT go on to the stats (per user,
		/// 2026-09-03): as soon as the inventory is turned away, the chain brings it back.
		/// </summary>
		public SideEnum ChainTarget
		{
			get { return Side == SideEnum.Inventory ? SideEnum.Stats : SideEnum.Inventory; }
		}

		/// <summary>The rune bag (and the modern runes key) turns to the rune shelf, or from there
		/// back to the inventory.</summary>
		public SideEnum RuneBagTarget
		{
			get { return Side == SideEnum.Runes ? SideEnum.Inventory : SideEnum.Runes; }
		}

		/// <summary>Starts a turn to the given page. During a running turn nothing happens, likewise
		/// if the page is already at the front; false then. The first step is due at once.</summary>
		public bool Begin(SideEnum peTarget)
		{
			if (IsTurning || peTarget == Side)
				return false;

			Target = peTarget;
			Step = 0;

			return true;
		}

		/// <summary>
		/// The next step, when its time has come. The width follows the cosine over 180 degrees, on
		/// the edge step the page flips.
		///
		/// The last step needs its own dwell time: the call after it ends the turn (Finished). Until
		/// 2026-09-02 the reset happened directly after it, in the same pass - it was set and
		/// immediately overwritten again and thus never visible (reported per user, first for step
		/// 6, after the switch to eight steps for step 7).
		/// </summary>
		public Frame Advance()
		{
			if (!IsTurning)
				return new Frame { Finished = true };

			if (Step >= StepCount)
			{
				Step = -1;

				return new Frame { Finished = true, Scale = 1f };
			}

			if (Slides)
				return fAdvanceSlide();

			// Eight steps, as many as the chain has pictures (per user, 2026-09-02). The angle runs
			// in eighths over 180 degrees, the edge-on position thus falls on step four - exactly
			// where the chain also has its edge picture. In floats as Unity's Mathf computed it
			// (the product, then the double cosine).
			float lfAngle = (Step / (float)Steps) * (float)Math.PI;
			bool lbOnEdge = Step == EdgeStep;

			Frame lOFrame = new Frame
			{
				Step = Step,
				Scale = lbOnEdge ? 0f : Math.Abs((float)Math.Cos(lfAngle)),
				OnEdge = lbOnEdge,
				ChainFrame = Step,
				Flipped = lbOnEdge
			};

			// Halfway the content flips over.
			if (lbOnEdge)
				Side = Target;

			Step++;

			return lOFrame;
		}

		/// <summary>A slide's frame (Step + 1 of twenty): the old page out to the right for ten frames, the new one in from
		/// the right after it, the page flipping on the first frame in; the chain's pictures on their frames.</summary>
		private Frame fAdvanceSlide()
		{
			int liFrame = Step + 1;
			bool lbOut = liFrame <= UWClassicLayoutUw2.PanelSlide.OutFrames;
			bool lbFlip = !lbOut && Side != Target;

			if (lbFlip)
				Side = Target;

			Frame lOFrame = new Frame
			{
				Step = Step,
				Scale = 1f,
				Offset = (lbOut ? liFrame : UWClassicLayoutUw2.PanelSlide.Frames - liFrame) * UWClassicLayoutUw2.PanelSlide.StepColumns,
				ChainFrame = Array.IndexOf(UWClassicLayoutUw2.PanelSlide.ChainFrames, liFrame),
				Flipped = lbFlip
			};

			Step++;

			return lOFrame;
		}

		/// <summary>Ends a turn under way at once, where it stands: a page already flipped stays,
		/// one not yet flipped never comes. The caller puts the pictures back at rest.</summary>
		public bool Cancel()
		{
			if (!IsTurning)
				return false;

			Step = -1;

			return true;
		}

		/// <summary>Shows a page without a turn, a turn under way cut short.</summary>
		public void Show(SideEnum peSide)
		{
			Cancel();
			Side = peSide;
		}

		// ------------------------------------------------- Conversation

		/// <summary>The page the panel showed (or turned to) before a conversation began; null
		/// outside one.</summary>
		public SideEnum? SideBeforeConversation { get; private set; }

		/// <summary>What HoldForConversation changed.</summary>
		public struct ConversationChange
		{
			/// <summary>A turn under way was cut short: the pictures go back to rest.</summary>
			public bool TurnCut;

			/// <summary>The page changed: Side shows now.</summary>
			public bool SideChanged;
		}

		/// <summary>
		/// THE INVENTORY SHOWS DURING A CONVERSATION whatever page was up before - the stats page or
		/// the rune shelf - and afterwards that page is back (per user on the original,
		/// 2026-09-18). Switched without the turn, a turn under way is cut short and its target
		/// remembered. Called every frame of a conversation; only the first call changes anything.
		/// </summary>
		public ConversationChange HoldForConversation()
		{
			ConversationChange lOChange = new ConversationChange();

			if (SideBeforeConversation.HasValue)
				return lOChange;

			SideBeforeConversation = IsTurning ? Target : Side;
			lOChange.TurnCut = Cancel();

			if (Side != SideEnum.Inventory)
			{
				Side = SideEnum.Inventory;
				lOChange.SideChanged = true;
			}

			return lOChange;
		}

		/// <summary>After the conversation: the page from before it back, without a turn. True when
		/// the page changed (Side shows now); called every frame outside a conversation.</summary>
		public bool ReleaseAfterConversation()
		{
			if (!SideBeforeConversation.HasValue)
				return false;

			SideEnum leBefore = SideBeforeConversation.Value;

			SideBeforeConversation = null;

			if (leBefore == Side)
				return false;

			Side = leBefore;

			return true;
		}
	}
}
