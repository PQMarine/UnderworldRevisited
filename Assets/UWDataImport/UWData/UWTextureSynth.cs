using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE HUD ART'S TEXTURE SYNTHESIS BY NEIGHBOURHOOD (cleanup 9.13, 2026-10-25, CLEANUP-PLAN 8.4):
	/// the stone shelf's band (UWStoneShelfBuilder), the Classic Wide strips (UWClassicWideBuilder)
	/// and the compass disc's F pixels (UWCompassDiscBuilder) grow a pixel by taking a source pixel
	/// whose surroundings match the pixels already in place - one of the three best by a seeded
	/// sequence. The three wrote the same top-3 insertion out each time; it is here once.
	///
	/// A picture stays the same only as long as the candidates come in the same order and the draws
	/// from the same sequence: of equal costs the one found first stays ahead.
	/// </summary>
	public static class UWTextureSynth
	{
		/// <summary>How many of the best candidates a pixel picks from.</summary>
		public const int Best = 3;

		/// <summary>Offers a candidate to the best so far (plCosts and piCandidates, Best long,
		/// cheapest first; piFound the candidates offered before, counted on here). A candidate goes
		/// before the ones that cost more, not before the ones that cost the same.</summary>
		public static void PickBest3(long[] plCosts, int[] piCandidates, ref int piFound, long plCost, int piCandidate)
		{
			// Keep the three cheapest, cheapest first.
			int liSlot = Math.Min(piFound, Best);

			while (liSlot > 0 && plCosts[liSlot - 1] > plCost)
			{
				if (liSlot < Best)
				{
					plCosts[liSlot] = plCosts[liSlot - 1];
					piCandidates[liSlot] = piCandidates[liSlot - 1];
				}

				liSlot--;
			}

			if (liSlot < Best)
			{
				plCosts[liSlot] = plCost;
				piCandidates[liSlot] = piCandidate;
			}

			piFound++;
		}

		/// <summary>One of the best candidates kept (piFound over 0), by the next draw of the
		/// sequence.</summary>
		public static int PickOne(int[] piCandidates, int piFound, UWSeededRandom pORandom)
		{
			return piCandidates[pORandom.Below(Math.Min(piFound, Best))];
		}

		/// <summary>
		/// How well the source pixel (piSourceX, piSourceY) fits the target pixel (piX, piY): the
		/// colour distances between the pixels around both, within two columns and rows - only where
		/// the target's pixel is known (pOKnown), lies in the target's rows piTop to piBottom
		/// (exclusive) and inside its width, and the source's may be drawn from (pOUsable, its rows
		/// counted from piUsableTop; outside it nothing may). The average over the pixels compared
		/// (their number differs at the edges), times 24, so few compared pixels do not look like a
		/// good match; false when fewer than piMinCompared were compared. The source may be the
		/// target itself (the compass disc grows from its own granite).
		/// </summary>
		public static bool MatchCost(UWPicture pOTarget, UWMask pOKnown, int piX, int piY, int piTop, int piBottom,
			UWPicture pOSource, UWMask pOUsable, int piUsableTop, int piSourceX, int piSourceY, int piMinCompared, out long plCost)
		{
			long llCost = 0;
			int liCompared = 0;

			for (int dy = -2; dy <= 2; dy++)
			{
				int liY = piY + dy;
				int liSY = piSourceY + dy - piUsableTop;

				if (liY < piTop || liY >= piBottom || liSY < 0 || liSY >= pOUsable.Height)
					continue;

				for (int dx = -2; dx <= 2; dx++)
				{
					int liX = piX + dx;
					int liSX = piSourceX + dx;

					if ((dx == 0 && dy == 0) || liX < 0 || liX >= pOTarget.Width || !pOKnown[liX, liY]
						|| liSX < 0 || liSX >= pOUsable.Width || !pOUsable[liSX, liSY])
						continue;

					llCost += UWPixelRules.Distance(pOTarget.Get(liX, liY), pOSource.Get(liSX, piSourceY + dy));
					liCompared++;
				}
			}

			if (liCompared < piMinCompared)
			{
				plCost = 0;

				return false;
			}

			plCost = llCost * 24 / liCompared;

			return true;
		}

		/// <summary>
		/// GROWS COLUMNS of the target (piX0 to piX1, exclusive) pixel by pixel, column by column from
		/// the left, each from row piRow1 - 1 up to piRow0: every pixel takes the source pixel of the
		/// same row from one of piColumns of the same column parity (for the dither) whose pixel may
		/// be drawn from (pOUsable, rows from piUsableTop) and whose surroundings match best
		/// (MatchCost within the rows piTop to piBottom, at least piMinCompared pixels compared) - one
		/// of the three best by pORandom, in the order of piColumns. Every pixel visited counts as
		/// known afterwards, also one nothing matched (it keeps what it had). The stone shelf's band
		/// and the Classic Wide strips; the compass disc's F pixels go in another order (an onion from
		/// the edge in) and from anywhere on the disc's top, so it uses MatchCost and PickBest3 alone.
		/// </summary>
		public static void GrowFrom(UWPicture pOTarget, UWMask pOKnown, int piX0, int piX1, int piRow0, int piRow1, int piTop, int piBottom,
			UWPicture pOSource, UWMask pOUsable, int piUsableTop, int[] piColumns, int piMinCompared, UWSeededRandom pORandom)
		{
			long[] llBest = new long[Best];
			int[] liBest = new int[Best];

			for (int x = piX0; x < piX1; x++)
			{
				for (int y = piRow1 - 1; y >= piRow0; y--)
				{
					int liFound = 0;

					foreach (int liSource in piColumns)
					{
						if (((liSource - x) & 1) != 0 || !pOUsable[liSource, y - piUsableTop])
							continue;

						if (!MatchCost(pOTarget, pOKnown, x, y, piTop, piBottom, pOSource, pOUsable, piUsableTop, liSource, y, piMinCompared,
							out long llCost))
							continue;

						PickBest3(llBest, liBest, ref liFound, llCost, liSource);
					}

					if (liFound > 0)
						pOTarget.Set(x, y, pOSource.Get(PickOne(liBest, liFound, pORandom), y));

					pOKnown[x, y] = true;
				}
			}
		}
	}
}
