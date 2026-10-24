using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE CONVERSATION SHOWS (cleanup step 5.12, 2026-10-20), in one place for both schemes:
	/// the history as the parchment shows it - the lines wrapped, each with its speaker's colour role,
	/// [MORE] after twelve lines of one text and at every "\m" -, the answers and their click rows,
	/// the typed answer (babl_ask), the look text that replaces the answers for two seconds, the
	/// trade slots with their marks and counts, the names, the portraits and the vine edges' frame.
	/// Until then the classic screen (UWConversationScreen) kept all of it in its fields and drew it
	/// at once, and the modern conversation read it from the screen's accessors. Engine-free
	/// (decision 26 of the cleanup plan): UWConversationController feeds it (input, time and the
	/// session's progress), UWClassicConversationRenderer and UWModernConversation draw it. Time is
	/// passed in, in seconds of the unscaled clock.
	///
	/// ONE VIEW LIVES AS LONG AS ITS CONTROLLER: the vine edges' frame carries from one conversation
	/// into the next, as dseg_5c99_A96 does in the original. Begin resets the rest.
	///
	/// THE SCHEMES DIFFER in their Policy only: the classic parchment pauses for [MORE], the modern
	/// history scrolls and never waits (the modern renderer reads the whole entries, Lines, and wraps
	/// them itself).
	/// </summary>
	public sealed class UWConversationView
	{
		/// <summary>How a scheme shows the history.</summary>
		public struct Policy
		{
			/// <summary>[MORE] after a full parchment and at every "\m"; the modern history scrolls,
			/// nothing waits for a click there.</summary>
			public bool PauseForMore;

			/// <summary>The width the lines wrap to (the parchment's).</summary>
			public int WrapWidth;

			/// <summary>How many lines the parchment holds.</summary>
			public int LinesCapacity;

			/// <summary>The parchment's policy in this font; the modern scheme's is the same without
			/// the pause.</summary>
			public static Policy For(UWFont pOFont, bool pbModern)
			{
				return new Policy
				{
					PauseForMore = !pbModern,
					WrapWidth = UWConversationLayout.HistoryWidth,
					LinesCapacity = UWConversationLayout.HistoryCapacity(pOFont)
				};
			}

			/// <summary>The same for the scheme in force (UWUiSchemePolicy.ConversationPausesForMore,
			/// cleanup 5.19).</summary>
			public static Policy For(UWFont pOFont, UWUiSchemePolicy pOScheme)
			{
				return For(pOFont, !pOScheme.ConversationPausesForMore);
			}
		}

		/// <summary>A line that is not yet in the parchment.</summary>
		private struct PendingLine
		{
			public string Text;
			public UWConversationLayout.RoleEnum Role;

			/// <summary>After this line the text pauses - a "\m" in the original.</summary>
			public bool BreakAfter;
		}

		/// <summary>One slot of a trade area as it is shown.</summary>
		public struct TradeSlot
		{
			public UWObject Item;

			/// <summary>The pieces of a stack (UWStackRules.Count); shown only above one.</summary>
			public int Count;

			/// <summary>Marked for the deal (the original's cross, the modern ring).</summary>
			public bool Marked;
		}

		private UWConversationSession mOSession;

		private IReadOnlyList<UWConversationSession.Line> mOLines = new UWConversationSession.Line[0];

		private IReadOnlyList<string> mOChoices = new string[0];

		/// <summary>The running conversation; null in the layout editor's preview.</summary>
		public UWConversationSession Session => mOSession;

		/// <summary>Every entry said so far, oldest first - the modern history wraps them itself.</summary>
		public IReadOnlyList<UWConversationSession.Line> Lines => mOSession != null ? mOSession.Lines : mOLines;

		/// <summary>The answers open now, unnumbered.</summary>
		public IReadOnlyList<string> Choices => mOSession != null ? mOSession.Choices : mOChoices;

		/// <summary>The trade; null without a partner and in the preview.</summary>
		public UWConversationTrade Trade => mOSession != null ? mOSession.Trade : null;

		/// <summary>The partner's name as the session has it (null where it has none), the preview's otherwise.</summary>
		public string PartnerName => mOSession != null ? mOSession.PartnerName : msPreviewPartnerName;

		private string msPreviewPartnerName = string.Empty;

		/// <summary>The player character's name, empty without one.</summary>
		public string PlayerName { get; private set; } = string.Empty;

		public UWTexture.TextureTypes PartnerPortraitType { get; private set; } = UWTexture.TextureTypes.GENHEAD;

		public int PartnerPortraitIndex { get; private set; }

		public int PlayerPortraitIndex { get; private set; }

		/// <summary>A running conversation begins: the names, the portraits, an empty parchment.</summary>
		public void Begin(UWConversationSession pOSession, UWNpc pONpc, int piSlot, UWPlayerData pOPlayer)
		{
			mOSession = pOSession;
			PlayerName = pOPlayer != null && !string.IsNullOrEmpty(pOPlayer.Name) ? pOPlayer.Name : string.Empty;

			UWConversationLayout.PartnerPortrait(piSlot, pONpc, out UWTexture.TextureTypes leType, out int liIndex);

			PartnerPortraitType = leType;
			PartnerPortraitIndex = liIndex;
			PlayerPortraitIndex = UWConversationLayout.PlayerPortrait(pOPlayer);

			ResetHistory();
		}

		/// <summary>The conversation is over: the view lets go of the session.</summary>
		public void End()
		{
			mOSession = null;
		}

		/// <summary>The layout editor's preview: fixed lines and answers, no session, no trade.</summary>
		public static UWConversationView Preview(IReadOnlyList<UWConversationSession.Line> pOLines, IReadOnlyList<string> pOChoices,
			string psPartnerName, string psPlayerName, UWTexture.TextureTypes pePartnerType, int piPartnerIndex, int piPlayerIndex)
		{
			return new UWConversationView
			{
				mOLines = pOLines ?? new UWConversationSession.Line[0],
				mOChoices = pOChoices ?? new string[0],
				msPreviewPartnerName = psPartnerName ?? string.Empty,
				PlayerName = psPlayerName ?? string.Empty,
				PartnerPortraitType = pePartnerType,
				PartnerPortraitIndex = piPartnerIndex,
				PlayerPortraitIndex = piPlayerIndex
			};
		}

		/// <summary>The partner's portrait (CHARHEAD or GENHEAD) or the player's (HEADS); null where
		/// the picture is missing.</summary>
		public UWTexture PortraitTexture(UWTextures pOTextures, bool pbPlayer)
		{
			if (pOTextures == null)
				return null;

			try
			{
				return pbPlayer
					? pOTextures.GetTextureByType(UWTexture.TextureTypes.HEADS, PlayerPortraitIndex)
					: pOTextures.GetTextureByType(PartnerPortraitType, PartnerPortraitIndex);
			}
			catch
			{
				return null;
			}
		}

		// ------------------------------------------------- History with [MORE]

		public const string MoreMarker = "[MORE]";

		/// <summary>What is in the parchment, line by line, oldest first.</summary>
		private readonly List<string> mOShownLines = new List<string>();

		private readonly List<UWConversationLayout.RoleEnum> mOShownRoles = new List<UWConversationLayout.RoleEnum>();

		private readonly Queue<PendingLine> mOPendingLines = new Queue<PendingLine>();

		/// <summary>How many history lines have already been processed.</summary>
		private int miLinesConsumed;

		/// <summary>Lines since the last pause within ONE text.</summary>
		private int miLinesSincePause;

		/// <summary>"[MORE]" is shown at the bottom, the text is waiting for a click.</summary>
		public bool MorePending { get; private set; }

		/// <summary>
		/// Brings the history up to the session, like the original: the parchment holds thirteen
		/// lines and pushes older ones out at the top. If ONE text needs more than twelve lines,
		/// "[MORE]" appears after twelve and the rest waits for a click - the same at every "\m" in the
		/// text (reference: MessageDisplay.AddText, Rows 13). Until 2026-09-11 the history showed
		/// only the last lines, and for long speeches the beginning was missing.
		/// </summary>
		public void Refresh(UWFont pOFont, Policy pyPolicy)
		{
			fQueueNewLines(pOFont, pyPolicy.WrapWidth);
			fRevealLines(pyPolicy);
		}

		private void fQueueNewLines(UWFont pOFont, int piWidth)
		{
			for (; miLinesConsumed < Lines.Count; miLinesConsumed++)
			{
				UWConversationSession.Line lOLine = Lines[miLinesConsumed];
				UWConversationLayout.RoleEnum leRole = UWConversationLayout.RoleOf(lOLine);

				string[] lsSegments = (lOLine.Text ?? string.Empty).Split(UWConversationLayout.PageBreaks, System.StringSplitOptions.None);

				for (int liSegment = 0; liSegment < lsSegments.Length; liSegment++)
				{
					List<string> lOWrapped = UWConversationLayout.WrapWithBreaks(pOFont, lsSegments[liSegment].Trim(), piWidth);

					for (int liAt = 0; liAt < lOWrapped.Count; liAt++)
					{
						mOPendingLines.Enqueue(new PendingLine
						{
							Text = lOWrapped[liAt],
							Role = leRole,
							BreakAfter = liAt == lOWrapped.Count - 1 && liSegment < lsSegments.Length - 1
						});
					}
				}

				// A new text counts its lines from the start; the limit applies per text.
				mOPendingLines.Enqueue(new PendingLine { Text = null });
			}
		}

		/// <summary>Brings lines into the parchment until a pause is due.</summary>
		private void fRevealLines(Policy pyPolicy)
		{
			while (!MorePending && mOPendingLines.Count > 0)
			{
				PendingLine lOLine = mOPendingLines.Dequeue();

				// End of text: the counter starts over.
				if (lOLine.Text == null)
				{
					miLinesSincePause = 0;
					continue;
				}

				// The modern history scrolls: nothing waits for a click there.
				if (miLinesSincePause >= pyPolicy.LinesCapacity - 1 && pyPolicy.PauseForMore)
				{
					// Back to the front of the queue - only the click fetches it.
					fRequeueFront(lOLine);
					MorePending = true;
					break;
				}

				mOShownLines.Add(lOLine.Text);
				mOShownRoles.Add(lOLine.Role);
				miLinesSincePause++;

				if (lOLine.BreakAfter && pyPolicy.PauseForMore)
					MorePending = true;
			}

			if (MorePending && mOPendingLines.Count == 0)
				MorePending = false;
		}

		private void fRequeueFront(PendingLine pOLine)
		{
			List<PendingLine> lORest = new List<PendingLine>(mOPendingLines);

			mOPendingLines.Clear();
			mOPendingLines.Enqueue(pOLine);

			foreach (PendingLine lOOther in lORest)
				mOPendingLines.Enqueue(lOOther);
		}

		/// <summary>The parchment's lines as they stand: the shown ones, [MORE] under them while the
		/// text waits, the oldest pushed out above the capacity.</summary>
		public void VisibleHistory(Policy pyPolicy, List<string> pOLines, List<UWConversationLayout.RoleEnum> pORoles)
		{
			pOLines.Clear();
			pORoles.Clear();
			pOLines.AddRange(mOShownLines);
			pORoles.AddRange(mOShownRoles);

			if (MorePending)
			{
				pOLines.Add(MoreMarker);
				pORoles.Add(UWConversationLayout.RoleEnum.Ui);
			}

			int liExcess = pOLines.Count - pyPolicy.LinesCapacity;

			if (liExcess > 0)
			{
				pOLines.RemoveRange(0, liExcess);
				pORoles.RemoveRange(0, liExcess);
			}
		}

		/// <summary>The click on "[MORE]": continue with the text.</summary>
		public void ContinueAfterMore()
		{
			MorePending = false;
			miLinesSincePause = 0;
		}

		/// <summary>An empty parchment - and one step of the vine edges.</summary>
		public void ResetHistory()
		{
			mOShownLines.Clear();
			mOShownRoles.Clear();
			mOPendingLines.Clear();
			miLinesConsumed = 0;
			miLinesSincePause = 0;
			MorePending = false;

			// Clearing the parchment is worth ONE step in the original as well: a conversation
			// starts through seg043_37F0_8EB, and that ends in the same fork as a scroll.
			mOLastShownLines.Clear();
			EdgeFrame = UWScrollEdgeRules.Advance(EdgeFrame, UWScrollEdgeRules.ConversationFrames);
		}

		// ------------------------------------------------- Vine edges

		/// <summary>The frame of the vine edges, as dseg_5c99_A96 counts it in the original: one
		/// step per scroll of the parchment, never reset, so the phase carries from one
		/// conversation into the next. UWScrollEdgeRules holds the strips and the count.</summary>
		public int EdgeFrame { get; private set; }

		/// <summary>The lines the parchment showed last time - see
		/// UWScrollEdgeRules.GetScrollAmount.</summary>
		private List<string> mOLastShownLines = new List<string>();

		/// <summary>
		/// THE VINE EDGES ROLL WITH THE PARCHMENT, one frame per scroll (UWScrollEdgeRules). Until
		/// 2026-09-22 they advanced with every redraw and each of the three heights carried a phase
		/// of its own; the original (seg043_37F0_163) draws all three with the SAME frame and steps
		/// only when the text area has scrolled. True when the frame moved.
		/// </summary>
		public bool RollEdges(IList<string> pOShownLines)
		{
			int liScrolled = UWScrollEdgeRules.GetScrollAmount(mOLastShownLines, pOShownLines);

			mOLastShownLines = new List<string>(pOShownLines);

			if (liScrolled <= 0)
				return false;

			EdgeFrame = UWScrollEdgeRules.Advance(EdgeFrame, UWScrollEdgeRules.ConversationFrames, liScrolled);

			return true;
		}

		// ------------------------------------------------- The answers

		/// <summary>
		/// THE NUMBER KEYS THAT CHOOSE (decided per user 2026-10-15, C5): the classic scheme has the
		/// original's 1 to 4 (PLAYER.C binds '1'..'4' to conv_play_menu; further answers need a click,
		/// and the classic scroll shows four anyway), the modern one 1 to 9.
		/// </summary>
		public const int ClassicChoiceKeys = 4;

		public const int ModernChoiceKeys = 9;

		public static int ChoiceKeys(bool pbModern)
		{
			return pbModern ? ModernChoiceKeys : ClassicChoiceKeys;
		}

		/// <summary>The answers for the scroll: numbered, wrapped, and the line each one starts on
		/// (UWConversationLayout.ChoiceRows makes the click rows of them).</summary>
		public void ChoiceLines(UWFont pOFont, List<string> pOLines, List<int> pOFirstLines)
		{
			UWConversationLayout.WrapChoices(pOFont, Choices, pOLines, pOFirstLines);
		}

		// ------------------------------------------------- The typed answer (babl_ask)

		/// <summary>The answer is being typed.</summary>
		public bool IsTyping { get; private set; }

		/// <summary>The typed text, while the question is open.</summary>
		public string TypedAnswer { get; private set; } = string.Empty;

		/// <summary>The question opens: an empty answer.</summary>
		public void BeginTyping()
		{
			IsTyping = true;
			TypedAnswer = string.Empty;
		}

		/// <summary>The question closes; the text stays readable until the next one.</summary>
		public void EndTyping()
		{
			IsTyping = false;
		}

		/// <summary>One more character, if wdialog's limits take it (UWConversationLayout.CanTypeMore).
		/// True when it was taken.</summary>
		public bool TryType(char pcChar, UWFont pOFont)
		{
			if (!UWConversationLayout.CanTypeMore(pOFont, TypedAnswer))
				return false;

			TypedAnswer += pcChar;

			return true;
		}

		/// <summary>The last character goes; true when there was one.</summary>
		public bool Backspace()
		{
			if (TypedAnswer.Length == 0)
				return false;

			TypedAnswer = TypedAnswer.Substring(0, TypedAnswer.Length - 1);

			return true;
		}

		/// <summary>The scroll's line while typing.</summary>
		public string TypedAnswerLine => UWConversationLayout.TypedAnswerLine(TypedAnswer);

		// ------------------------------------------------- The look text

		/// <summary>
		/// LOOKING AT A TRADE ITEM shows its description in the answer scroll for about two seconds,
		/// then the answers come back (per user from the original, 2026-09-17). Until then the text
		/// went into the conversation history, where nothing seemed to happen and it only turned up
		/// in black after the trade.
		/// </summary>
		public const float LookTextSeconds = 2f;

		/// <summary>Until when a look text is shown; below zero none is.</summary>
		private float mfLookTextUntil = -1f;

		private string msLookText;

		/// <summary>A look text replaces the answers from pfNow on.</summary>
		public void ShowLookText(string psText, float pfNow)
		{
			msLookText = psText;
			mfLookTextUntil = pfNow + LookTextSeconds;
		}

		/// <summary>A look text stands in the scroll.</summary>
		public bool IsLookTextShown => mfLookTextUntil >= 0f;

		/// <summary>Its two seconds have run out.</summary>
		public bool IsLookTextOver(float pfNow)
		{
			return mfLookTextUntil >= 0f && pfNow >= mfLookTextUntil;
		}

		/// <summary>The answers come back.</summary>
		public void EndLookText()
		{
			mfLookTextUntil = -1f;
		}

		/// <summary>The look text replacing the answers for its two seconds, else null.</summary>
		public string LookText => mfLookTextUntil >= 0f ? msLookText : null;

		/// <summary>A look text wrapped to the scroll.</summary>
		public static List<string> LookLines(UWFont pOFont, string psText)
		{
			return UWTextLayout.WrapText(pOFont, psText, UWConversationLayout.ChoicesWidth);
		}

		// ------------------------------------------------- The trade areas

		/// <summary>The trade's version as it changes with every move (UWConversationTrade.Version),
		/// -1 without a trade.</summary>
		public int TradeVersion => Trade != null ? Trade.Version : -1;

		/// <summary>One slot of the partner's area (pbNpc) or the player's.</summary>
		public TradeSlot TradeSlotOf(bool pbNpc, int piSlot)
		{
			UWConversationTrade lOTrade = Trade;

			if (lOTrade == null)
				return new TradeSlot();

			UWObject lOItem = pbNpc ? lOTrade.NpcItems[piSlot] : lOTrade.PlayerItems[piSlot];

			return new TradeSlot
			{
				Item = lOItem,
				Count = lOItem != null ? UWStackRules.Count(lOItem) : 0,
				Marked = pbNpc ? lOTrade.NpcSelected[piSlot] : lOTrade.PlayerSelected[piSlot]
			};
		}
	}
}
