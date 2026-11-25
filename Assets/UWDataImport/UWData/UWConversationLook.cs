namespace UWDataImport.UWData
{
	/// <summary>
	/// THE CONVERSATION IN THE ORIGINAL'S LOOK PER GAME (UW2 plan, "The modern interface for UW2: one interface, two
	/// hosts", stage U7 step 3b, 2026-11-24): where the modern conversation (UWModernConversation, the original look)
	/// lays its pieces and draws on them - every place in the block's pixels from its top-left corner, which is the top
	/// piece's.
	///
	/// THE FIRST GAME: CONV.BYT's pieces (UWConversationArtBuilder.BuildHeader, BuildParchment; the top from x 43): the
	/// name plates, the portraits' frames and the two trade areas of 2 x 2, the history's parchment under them and an
	/// answers' parchment of its own with the message scroll's knob rollers; the colours the sample's.
	///
	/// THE SECOND GAME: BYT.ARK screen 2's pieces (BuildHeaderUw2, BuildParchmentUw2, BuildAnswersUw2; the top from x 0):
	/// the two portrait boxes, the trade grids of 2 x 3 between them and the name bars - the partner's under the grids,
	/// the player's above them, right-aligned -, the history's parchment and the answers' scroll the block's width under
	/// it; the inks the screen's palette colours (spec_col).
	/// </summary>
	public sealed class UWConversationLook
	{
		public bool IsSecondGame { get; private set; }

		/// <summary>The block's width and the top piece's rows.</summary>
		public int Width { get; private set; }

		public int HeaderRows { get; private set; }

		/// <summary>The history's parchment: its left, top and rows.</summary>
		public int ParchmentX { get; private set; }

		public int ParchmentTop { get; private set; }

		public int ParchmentRows { get; private set; }

		/// <summary>The answers' piece: its left, top and rows (always this high, the answers' text made to fit).</summary>
		public int AnswersX { get; private set; }

		public int AnswersTop { get; private set; }

		public int AnswersRows { get; private set; }

		/// <summary>The answers' piece's width where it is not the parchment's (the second game's scroll; -1 the first game's).</summary>
		public int AnswersWidth { get; private set; } = -1;

		/// <summary>The answers' text: its first row, the rows kept free under it, its left and width.</summary>
		public int AnswerTextTop { get; private set; }

		public int AnswerBottom { get; private set; }

		public int AnswerTextX { get; private set; }

		public int AnswerTextWidth { get; private set; }

		/// <summary>The history's text on the parchment.</summary>
		public int TextX { get; private set; }

		public int TextTop { get; private set; }

		public int TextWidth { get; private set; }

		public int TextRows { get; private set; }

		/// <summary>The names (the partner's first): left, top, width, and whether one stands right-aligned.</summary>
		public int[] NameX { get; private set; }

		public int[] NameTop { get; private set; }

		public int NameWidth { get; private set; }

		public bool[] NameRight { get; private set; }

		/// <summary>The portraits' top-left corners (the partner's first).</summary>
		public int[] PortraitX { get; private set; }

		public int PortraitTop { get; private set; }

		/// <summary>The trade slots' top-left corners, the partner's first, and their size.</summary>
		public (int X, int Y)[] Slots { get; private set; }

		public int SlotSize { get; private set; }

		/// <summary>The colours: fixed ones (the first game's sample), or the palette's indices (the second game's inks).</summary>
		private UWColor32 mONameColour, mONpcText, mOPlayerText, mONarratorText;

		private int miNameInk = -1, miNpcInk = -1, miPlayerInk = -1, miNarratorInk = -1;

		public UWColor32 NameColour(UWPalettes pOPalettes) => fColour(pOPalettes, miNameInk, mONameColour);

		public UWColor32 NpcText(UWPalettes pOPalettes) => fColour(pOPalettes, miNpcInk, mONpcText);

		public UWColor32 PlayerText(UWPalettes pOPalettes) => fColour(pOPalettes, miPlayerInk, mOPlayerText);

		public UWColor32 NarratorText(UWPalettes pOPalettes) => fColour(pOPalettes, miNarratorInk, mONarratorText);

		private static UWColor32 fColour(UWPalettes pOPalettes, int piInk, UWColor32 pOFixed)
		{
			if (piInk < 0)
				return pOFixed;

			UWPalette lOPalette = pOPalettes != null ? pOPalettes.GetPalette(0) : null;
			UWColor32 lOColour = lOPalette != null ? lOPalette.GetUWColor(piInk) : pOFixed;

			return new UWColor32(lOColour.R, lOColour.G, lOColour.B, 255);
		}

		public static UWConversationLook Of(UWTextures pOTextures)
		{
			return pOTextures != null && pOTextures.Game == UWGame.Uw2 ? Uw2 : Uw1;
		}

		/// <summary>The first game's: the numbers UWModernConversation kept as constants until step 3b.</summary>
		public static readonly UWConversationLook Uw1 = fUw1();

		private static UWConversationLook fUw1()
		{
			int liLeft = UWClassicLayout.Conversation.HeaderLeft;
			int liParchmentX = UWClassicLayout.Conversation.ParchmentLeft - liLeft;
			int liAnswersTop = UWClassicLayout.Conversation.ParchmentTop + UWClassicLayout.Conversation.ParchmentHeight - 2;
			(int X, int Y)[] liSlots = new (int X, int Y)[UWClassicLayout.Conversation.NpcTradeSlots.Length + UWClassicLayout.Conversation.PlayerTradeSlots.Length];
			int liAt = 0;

			foreach ((int X, int Y) lySlot in UWClassicLayout.Conversation.NpcTradeSlots)
				liSlots[liAt++] = (lySlot.X - liLeft, lySlot.Y);

			foreach ((int X, int Y) lySlot in UWClassicLayout.Conversation.PlayerTradeSlots)
				liSlots[liAt++] = (lySlot.X - liLeft, lySlot.Y);

			return new UWConversationLook
			{
				IsSecondGame = false,
				Width = UWClassicLayout.Conversation.HeaderWidth,
				HeaderRows = UWClassicLayout.Conversation.HeaderHeight,
				ParchmentX = liParchmentX,
				ParchmentTop = UWClassicLayout.Conversation.ParchmentTop,
				ParchmentRows = UWClassicLayout.Conversation.ParchmentHeight,
				AnswersX = liParchmentX,
				AnswersTop = liAnswersTop,
				AnswersRows = AnswersRowsFixed,
				AnswerTextTop = liAnswersTop + 7,
				AnswerBottom = 8,
				AnswerTextX = UWClassicLayout.Conversation.HistoryLeft - liLeft,
				AnswerTextWidth = UWClassicLayout.Conversation.HistoryWidth,
				TextX = UWClassicLayout.Conversation.HistoryLeft - liLeft,
				TextTop = UWClassicLayout.Conversation.HistoryTop,
				TextWidth = UWClassicLayout.Conversation.HistoryWidth,
				TextRows = UWClassicLayout.Conversation.HistoryHeight,
				NameX = new[] { UWClassicLayout.Conversation.NpcNameLeft - liLeft, UWClassicLayout.Conversation.PlayerNameLeft - liLeft },
				NameTop = new[] { 1, 1 },
				NameWidth = UWClassicLayout.Conversation.NameWidth,
				NameRight = new[] { false, false },
				PortraitX = new[] { UWClassicLayout.Conversation.NpcPortraitLeft - liLeft, UWClassicLayout.Conversation.PlayerPortraitLeft - liLeft },
				PortraitTop = UWClassicLayout.Conversation.PortraitTop,
				Slots = liSlots,
				SlotSize = UWClassicLayout.Conversation.TradeSlotSize,
				mONameColour = UWClassicLayout.Conversation.NameColour,
				mONpcText = UWClassicLayout.Conversation.NpcText,
				mOPlayerText = UWClassicLayout.Conversation.PlayerText,
				mONarratorText = UWClassicLayout.Conversation.NarratorText
			};
		}

		/// <summary>The answers' piece's rows on both games (the first game's since 2026-10-10, per user: "always the same
		/// size"; the second game's scroll made as high, its plain middle repeated, so the answers have the same room).</summary>
		public const int AnswersRowsFixed = 52;

		/// <summary>The second game's: BYT.ARK screen 2's places (UWClassicLayoutUw2.Conversation), the block from the top's
		/// x 0.</summary>
		public static readonly UWConversationLook Uw2 = fUw2();

		private static UWConversationLook fUw2()
		{
			UWRectInt lOParchment = UWClassicLayoutUw2.Conversation.Parchment;
			int liWidth = UWClassicLayoutUw2.Conversation.Header.Width;
			int liAnswersTop = lOParchment.Y + lOParchment.Height;
			int liSlotsASide = UWConversationTrade.SlotCountUw2;
			(int X, int Y)[] liSlots = new (int X, int Y)[2 * liSlotsASide];
			int liSlotTop = UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Conversation.SlotTopY;

			for (int liSlot = 0; liSlot < liSlotsASide; liSlot++)
			{
				int liX = (liSlot % 2) * UWClassicLayoutUw2.Conversation.SlotColumnStep;
				int liY = liSlotTop + ((liSlot / 2) * UWClassicLayoutUw2.Conversation.SlotRowStep);

				liSlots[liSlot] = (UWClassicLayoutUw2.Conversation.NpcSlotLeft + liX, liY);
				liSlots[liSlotsASide + liSlot] = (UWClassicLayoutUw2.Conversation.PlayerSlotLeft + liX, liY);
			}

			return new UWConversationLook
			{
				IsSecondGame = true,
				Width = liWidth,
				HeaderRows = UWClassicLayoutUw2.Conversation.Header.Height,
				ParchmentX = lOParchment.X,
				ParchmentTop = lOParchment.Y,
				ParchmentRows = lOParchment.Height,
				AnswersX = 0,
				AnswersTop = liAnswersTop,
				AnswersRows = AnswersRowsFixed,
				AnswersWidth = liWidth,
				// menu_scroll's first row is the scroll's third (169 on the screen, the scroll from 167).
				AnswerTextTop = liAnswersTop + 3,
				AnswerBottom = 4,
				AnswerTextX = UWClassicLayoutUw2.Conversation.AnswerClickLeft,
				AnswerTextWidth = liWidth - (2 * UWClassicLayoutUw2.Conversation.AnswerClickLeft),
				TextX = UWClassicLayoutUw2.Conversation.HistoryLeft,
				TextTop = UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Conversation.HistoryTopY,
				TextWidth = UWClassicLayoutUw2.Conversation.HistoryWidth,
				TextRows = UWClassicLayoutUw2.Conversation.HistoryLines * UWClassicLayoutUw2.Conversation.LineHeight,
				// The partner's name in the bar under the grids from 0x47, the player's in the bar above them ending before
				// 0xA4 (strt_converse); a row under each bar's outline.
				NameX = new[] { UWClassicLayoutUw2.Conversation.NpcNameLeft, UWClassicLayoutUw2.Conversation.PlayerNameRight - NameWidthUw2 },
				NameTop = new[] { UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Conversation.NpcNameY - 1, 1 },
				NameWidth = NameWidthUw2,
				NameRight = new[] { false, true },
				PortraitX = new[] { UWClassicLayoutUw2.Conversation.NpcPortraitLeft, UWClassicLayoutUw2.Conversation.PlayerPortraitLeft },
				PortraitTop = UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Conversation.PortraitY,
				Slots = liSlots,
				SlotSize = UWClassicLayoutUw2.Conversation.SlotSize,
				miNameInk = UWClassicLayoutUw2.Conversation.NameInk,
				miNpcInk = UWClassicLayoutUw2.Conversation.NpcInk,
				miPlayerInk = UWClassicLayoutUw2.Conversation.PlayerInk,
				miNarratorInk = UWClassicLayoutUw2.Conversation.NarratorInk
			};
		}

		/// <summary>The second game's name bars' room: from 0x47 to the grids' right edge.</summary>
		public const int NameWidthUw2 = 92;

		/// <summary>
		/// THE SECOND GAME'S BLOCK IN CLASSIC WIDE (UW2 plan stage U7 step 3c, per user 2026-11-24, variant b of three: as the
		/// first game's block over the wide view): the full block is 214 rows, the frame 200 - so the parchment lower,
		/// WideParchmentRows, its history a few lines shorter, and the answers' scroll as high as in the block (52 rows) right
		/// under it; no scaling, the frame's pixels.
		/// </summary>
		public static readonly UWConversationLook Uw2Wide = fUw2Wide();

		/// <summary>The parchment's rows in the second game's Classic Wide block.</summary>
		public const int WideParchmentRows = 70;

		private static UWConversationLook fUw2Wide()
		{
			UWConversationLook lOLook = fUw2();
			int liShorter = lOLook.ParchmentRows - WideParchmentRows;

			lOLook.ParchmentRows = WideParchmentRows;
			lOLook.TextRows -= liShorter;
			lOLook.AnswersTop -= liShorter;
			lOLook.AnswerTextTop -= liShorter;

			return lOLook;
		}
	}
}
