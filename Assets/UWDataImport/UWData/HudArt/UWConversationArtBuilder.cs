using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ORIGINAL'S CONVERSATION PIECES freed (CONV.BYT) and their outline, the answers'
	/// parchment with the message scroll's knob rollers (MAIN.BYT; cleanup 9.11, 2026-10-25: a
	/// builder of its own, UWHudArt forwards to it). The Build methods read the pictures, the
	/// Compose methods work on CONV.BYT and MAIN.BYT given.
	/// </summary>
	public static class UWConversationArtBuilder
	{
		// ------------------------------------------------- The original's conversation (CONV.BYT)

		/// <summary>The conversation's top in CONV.BYT: the two name plates, the portraits' frames
		/// and the two trade areas between them (x 43 to 232, rows 0 to 46).</summary>
		public static readonly UWRectInt ConversationHeader = UWClassicLayout.Conversation.Header;

		/// <summary>The parchment under it, its rolled rims included, and the ends of its two wooden
		/// rollers over the view's bars (x 42 to 233, rows 47 to 135; the bars and the vines on them
		/// go).</summary>
		public static readonly UWRectInt ConversationParchment = UWClassicLayout.Conversation.Parchment;

		/// <summary>The side columns of that crop where the bars lie (10 at either side), and the
		/// rows the rollers' ends take there: the upper roller 47 to 56 with the chain it hangs on
		/// (rows 47 to 54, from the conversation's top down over the roller), the lower 127 to 133.</summary>
		private const int ParchmentSide = 10;

		private static readonly (int Top, int Bottom)[] msRollerRows = { (47 - 47, 56 - 47), (127 - 47, 133 - 47) };

		private const int ChainBottom = 54 - 47;

		private const int UpperWoodTop = 50 - 47;

		/// <summary>The parchment's plain colour (#8C6854) and where CONV.BYT's sample text lies on
		/// it (x 56 to 218, rows 52 to 130).</summary>
		private static readonly UWColor32 ParchmentColour = new UWColor32(140, 104, 84, 255);

		private static readonly UWRectInt msParchmentText = new UWRectInt(56, 52, 163, 79);

		/// <summary>The rows of the parchment's rolled rims (with the rollers' ends) kept at the top and the bottom when it is
		/// made lower or taller (BuildConversationParchment).</summary>
		private const int ParchmentRim = 14;

		/// <summary>The name plates' text rows and colours: CONV.BYT's sample names (#8C8CA8) on the
		/// plates' brown (#48280C).</summary>
		private static readonly UWColor32 NamePlateColour = new UWColor32(72, 40, 12, 255);

		/// <summary>The two portraits' places (34 x 34), filled with black under the real ones.</summary>
		private static readonly UWRectInt[] msPortraitPlaces =
		{
			new UWRectInt(UWClassicLayout.Conversation.NpcPortraitLeft, UWClassicLayout.Conversation.PortraitTop,
				UWClassicLayout.Conversation.PortraitSize, UWClassicLayout.Conversation.PortraitSize),
			new UWRectInt(UWClassicLayout.Conversation.PlayerPortraitLeft, UWClassicLayout.Conversation.PortraitTop,
				UWClassicLayout.Conversation.PortraitSize, UWClassicLayout.Conversation.PortraitSize)
		};

		/// <summary>
		/// THE ORIGINAL'S CONVERSATION TOP (Classic+, per user 2026-10-10: "free the conversation
		/// UI"): CONV.BYT's top block - the file is a sample conversation, so its names ("Derek",
		/// "Tyrone Pop") go (the plates' text colour turned into the plates' brown) and its two
		/// portraits too (black under the real ones); the black reached from the block's edge
		/// becomes transparent. The names, portraits and traded things are drawn on it
		/// (UWModernConversation, original look).
		/// </summary>
		public static UWPicture BuildHeader(UWTextures pOTextures)
		{
			return ComposeHeader(UWPicture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.CONV, 0)));
		}

		public static UWPicture ComposeHeader(UWPicture pOConv)
		{
			UWPicture lOHeader = pOConv.Crop(ConversationHeader);

			for (int y = 1; y <= 7; y++)
			{
				for (int x = 0; x < lOHeader.Width; x++)
				{
					UWColor32 lOColour = lOHeader.Get(x, y);

					if (lOColour.A > 0 && lOColour.R == 140 && lOColour.G == 140 && lOColour.B == 168)
						lOHeader.Set(x, y, NamePlateColour);
				}
			}

			foreach (UWRectInt lOPlace in msPortraitPlaces)
				UWPixelOps.Fill(lOHeader, new UWRectInt(lOPlace.X - ConversationHeader.X, lOPlace.Y - ConversationHeader.Y, lOPlace.Width, lOPlace.Height),
					UWPixelOps.Black);

			fClearEdgeBlack(lOHeader);

			return lOHeader;
		}

		/// <summary>
		/// THE ORIGINAL'S CONVERSATION PARCHMENT (Classic+, per user 2026-10-10: "only free the
		/// scroll" - the vines beside it belong to the view's bars, as the dragons): CONV.BYT's
		/// parchment with its rolled rims, its sample text painted over in the plain colour, the
		/// black of the frame around it transparent. piRows other than the original's 89: the rims'
		/// 14 rows kept at both ends and the plain middle repeated (the answers' parchment is a
		/// lower one, per user the same day: "the area of the player's lines gets a parchment
		/// scroll, always the same size").
		/// </summary>
		public static UWPicture BuildParchment(UWTextures pOTextures, int piRows, bool pbKnobRollers = false)
		{
			return ComposeParchment(UWPicture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.CONV, 0)),
				pbKnobRollers ? UWMainFrame.Picture(pOTextures) : null, piRows, pbKnobRollers);
		}

		/// <summary>The parchment from CONV.BYT (not changed) and, with pbKnobRollers, MAIN.BYT (the
		/// message scroll's rollers).</summary>
		public static UWPicture ComposeParchment(UWPicture pOConv, UWPicture pOMain, int piRows, bool pbKnobRollers = false)
		{
			UWPicture lOConv = UWPixelOps.Copy(pOConv);

			UWPixelOps.Fill(lOConv, msParchmentText, ParchmentColour);

			UWPicture lOFull = lOConv.Crop(ConversationParchment);
			UWPicture lOSource = UWPixelOps.Copy(lOFull);

			// The frame's grey stone at the top right corner (the parchment has no neutral grey), and
			// the side columns with the bars, the brackets and the vines.
			for (int y = 0; y < lOFull.Height; y++)
			{
				for (int x = 0; x < lOFull.Width; x++)
				{
					UWColor32 lOColour = lOFull.Get(x, y);
					bool lbSide = x < ParchmentSide || x >= lOFull.Width - ParchmentSide;
					bool lbGrey = lOColour.A > 0 && lOColour.R == lOColour.G && lOColour.G == lOColour.B && lOColour.R > 20;

					if (lbSide || lbGrey)
						lOFull.Set(x, y, UWPixelOps.Transparent);
				}
			}

			fClearEdgeBlack(lOFull);

			// THE ROLLERS' ENDS (per user, 2026-10-10: "take the wooden rollers along", "they are
			// outlined in black", "the upper ones hang on a chain"): in the side columns their wood
			// (UWPixelRules.IsWood), the upper ones' chain (IsChain) and the black touching either; the
			// bars and the vines left out.
			foreach ((int liTop, int liBottom) in msRollerRows)
			{
				for (int y = liTop; y <= liBottom; y++)
				{
					for (int x = 0; x < lOFull.Width; x++)
					{
						if (x >= ParchmentSide && x < lOFull.Width - ParchmentSide)
							continue;

						UWColor32 lOColour = lOSource.Get(x, y);
						bool lbChainRow = liTop == 0 && y <= ChainBottom;
						// The upper roller's wood from row 50: above it the bar's copper edge.
						bool lbWoodRow = liTop != 0 || y >= UpperWoodTop;
						bool lbKeep = (lbWoodRow && UWPixelRules.IsWood(lOColour)) || (lbChainRow && UWPixelRules.IsChain(lOColour));

						if (!lbKeep && UWPixelRules.IsEdgeBlack(lOColour))
						{
							for (int liStep = 0; liStep < 4 && !lbKeep; liStep++)
							{
								int liX = x + (liStep == 0 ? 1 : liStep == 1 ? -1 : 0);
								int liY = y + (liStep == 2 ? 1 : liStep == 3 ? -1 : 0);

								lbKeep = liX >= 0 && liY >= liTop && liX < lOFull.Width && liY <= liBottom
									&& (((liTop != 0 || liY >= UpperWoodTop) && UWPixelRules.IsWood(lOSource.Get(liX, liY)))
										|| (liTop == 0 && liY <= ChainBottom && UWPixelRules.IsChain(lOSource.Get(liX, liY))));
							}
						}

						if (lbKeep)
							lOFull.Set(x, y, UWPixelOps.Opaque(lOColour));
					}
				}
			}

			// A black outline round the rollers' ends and the chains (per user, 2026-10-10: "in the
			// original the chain runs out into the black around it" - the answers' parchment's chain
			// hangs right under the history's lower roller): every empty side pixel touching one of
			// them, also diagonally, within the rollers' rows.
			UWPicture lOEnds = UWPixelOps.Copy(lOFull);

			bool fSide(int piX)
			{
				return piX < ParchmentSide || piX >= lOEnds.Width - ParchmentSide;
			}

			// Only round their coloured pixels: where the original's black edge already is, nothing
			// more (per user, 2026-10-10).
			bool fEnd(int piX, int piY)
			{
				return piX >= 0 && piY >= 0 && piX < lOEnds.Width && piY < lOEnds.Height && fSide(piX)
					&& UWPixelRules.IsColoured(lOEnds.Get(piX, piY));
			}

			// Within the rollers' rows only: under the lower ones a row too many (per user).
			bool fEmptySide(int piX, int piY)
			{
				return ((piY >= msRollerRows[0].Top && piY <= msRollerRows[0].Bottom) || (piY >= msRollerRows[1].Top && piY <= msRollerRows[1].Bottom))
					&& fSide(piX) && lOEnds.Get(piX, piY).A == 0;
			}

			UWPixelOps.Outline(lOFull, fEmptySide, fEnd, UWPixelOps.Black);

			int liRows = Math.Max(2 * ParchmentRim + 1, piRows);

			if (liRows == lOFull.Height && !pbKnobRollers)
				return lOFull;

			UWPicture lOOut = new UWPicture(lOFull.Width, liRows);
			int liMiddle = lOFull.Height / 2;

			for (int y = 0; y < liRows; y++)
			{
				int liFrom = y < ParchmentRim ? y
					: y >= liRows - ParchmentRim ? lOFull.Height - (liRows - y)
					: liMiddle;

				for (int x = 0; x < lOFull.Width; x++)
					lOOut.Set(x, y, lOFull.Get(x, liFrom));
			}

			if (pbKnobRollers)
			{
				// Above: the parchment's chain and roller give way to the message scroll's upper
				// roller with its own chain (per user: "it has a chain too, it only looks a little
				// different"), from the picture's top.
				fLayKnobRollers(pOMain, lOOut, 0, 0, ParchmentRim - 1, UWMainFrame.KnobUpperTop, UWMainFrame.KnobUpperRows);
				fLayKnobRollers(pOMain, lOOut,
					liRows - (lOFull.Height - msRollerRows[1].Top) - (UWMainFrame.KnobRollerOutlineTop - UWMainFrame.KnobRollerTop),
					liRows - (lOFull.Height - msRollerRows[1].Top) - 1, liRows - 1, UWMainFrame.KnobRollerTop, UWMainFrame.KnobRollerRows);
			}

			return lOOut;
		}

		/// <summary>
		/// THE ANSWERS' PARCHMENT WITH THE MESSAGE SCROLL'S KNOB ROLLERS (per user, 2026-10-10: "the
		/// knob rollers of the message scroll" - in the original the answers stand in that scroll;
		/// the history's parchment above keeps its own): the side columns' rows piClearFrom to
		/// piClearTo cleared (the parchment's roller, above also its chain) and the message scroll's
		/// rows piSourceTop on (piSourceRows of them) laid there from row piAt, knob to rod in the 10
		/// side columns: below its lower rollers, above its upper ones with their chains.
		/// </summary>
		private static void fLayKnobRollers(UWPicture pOMain, UWPicture pOParchment, int piAt, int piClearFrom, int piClearTo,
			int piSourceTop, int piSourceRows)
		{
			UWPicture lOMain = pOMain;
			int liWidth = pOParchment.Width;

			for (int y = Math.Max(0, piClearFrom); y <= piClearTo && y < pOParchment.Height; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					if (x < ParchmentSide || x >= liWidth - ParchmentSide)
						pOParchment.Set(x, y, UWPixelOps.Transparent);
				}
			}

			for (int liRow = 0; liRow < piSourceRows; liRow++)
			{
				int liY = piAt + liRow;

				if (liY < 0 || liY >= pOParchment.Height)
					continue;

				for (int liColumn = 0; liColumn < ParchmentSide; liColumn++)
				{
					UWColor32 lOLeft = lOMain.Get(UWMainFrame.KnobRollerLeftX + liColumn, piSourceTop + liRow);
					UWColor32 lORight = lOMain.Get(UWMainFrame.KnobRollerRightX + liColumn, piSourceTop + liRow);

					if (lOLeft.A > 0)
						pOParchment.Set(liColumn, liY, UWPixelOps.Opaque(lOLeft));

					if (lORight.A > 0)
						pOParchment.Set(liWidth - ParchmentSide + liColumn, liY, UWPixelOps.Opaque(lORight));
				}
			}
		}

		/// <summary>
		/// A picture a pixel larger each way, and with pbOutline a black outline in that room and in
		/// its gaps: every empty pixel touching a coloured one, also diagonally - not round black,
		/// where the original's edge already is (the conversation's pieces in the original's look,
		/// per user 2026-10-10: "with the outline it looks better", as an option); without it the
		/// black edge the pieces bring goes as well.
		/// </summary>
		public static UWPicture PadAndOutline(UWPicture pOPicture, bool pbOutline)
		{
			UWPicture lOOut = UWPixelOps.Pad(pOPicture, 1, 1, 1, 1);
			UWPicture lOShape = UWPixelOps.Copy(lOOut);

			bool fInside(int piX, int piY)
			{
				return piX >= 0 && piY >= 0 && piX < lOShape.Width && piY < lOShape.Height;
			}

			// Without the outline no black edge at all (per user, 2026-10-10: "with Off remove the
			// whole pixel of black all round, the rollers' too"): every black pixel touching the
			// empty outside, also diagonally, goes - once.
			if (!pbOutline)
			{
				UWPixelOps.Outline(lOOut,
					(piX, piY) => lOShape.Get(piX, piY).A != 0 && UWPixelRules.IsEdgeBlack(lOShape.Get(piX, piY)),
					(piX, piY) => !fInside(piX, piY) || lOShape.Get(piX, piY).A == 0,
					UWPixelOps.Transparent);

				return lOOut;
			}

			UWPixelOps.Outline(lOOut,
				(piX, piY) => lOShape.Get(piX, piY).A == 0,
				(piX, piY) => fInside(piX, piY) && UWPixelRules.IsColoured(lOShape.Get(piX, piY)),
				UWPixelOps.Black);

			return lOOut;
		}

		/// <summary>The black (or transparent) reached from a picture's edge through black becomes
		/// transparent; black inside, an outline or a gap between parts, stays.</summary>
		private static void fClearEdgeBlack(UWPicture pOPicture)
		{
			UWMask lOEdge = UWMask.FloodFromBorder(pOPicture, lOColour => lOColour.A == 0 || UWPixelRules.IsEdgeBlack(lOColour));

			UWPixelOps.Clear(pOPicture, lOEdge);

			// Opaque elsewhere: the picture's black (palette index 0) comes transparent.
			UWPixelOps.MakeOpaque(pOPicture, lOEdge);
		}
	}
}
