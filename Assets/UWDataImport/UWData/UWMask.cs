using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A PICTURE OF YES AND NO beside UWPicture (cleanup 9.9, 2026-10-25, CLEANUP-PLAN 8.4): rows top
	/// down, one bit per pixel, for what the HUD art builders keep, clear, have finished or may draw
	/// from. The floods, the pieces and the neighbour counts the builders wrote out each time.
	///
	/// A flood's or a piece's SET does not depend on the order it is walked in, so one walk serves
	/// them all; where a builder depends on the order (FreeOnPage's depths) it keeps its own.
	/// </summary>
	public sealed class UWMask
	{
		public readonly int Width;

		public readonly int Height;

		/// <summary>The bits, rows top down (index y * Width + x).</summary>
		public readonly bool[] Bits;

		public UWMask(int piWidth, int piHeight)
			: this(piWidth, piHeight, new bool[Math.Max(0, piWidth) * Math.Max(0, piHeight)])
		{
		}

		/// <summary>A mask over the given bits (not copied).</summary>
		public UWMask(int piWidth, int piHeight, bool[] pbBits)
		{
			Width = Math.Max(0, piWidth);
			Height = Math.Max(0, piHeight);
			Bits = pbBits;
		}

		public bool this[int piX, int piY]
		{
			get { return Bits[(piY * Width) + piX]; }
			set { Bits[(piY * Width) + piX] = value; }
		}

		public bool Contains(int piX, int piY)
		{
			return piX >= 0 && piY >= 0 && piX < Width && piY < Height;
		}

		/// <summary>The bit, false outside the mask.</summary>
		public bool IsSet(int piX, int piY)
		{
			return Contains(piX, piY) && Bits[(piY * Width) + piX];
		}

		/// <summary>The pixels of a picture a rule holds for.</summary>
		public static UWMask Where(UWPicture pOPicture, Func<UWColor32, bool> pORule)
		{
			UWMask lOMask = new UWMask(pOPicture.Width, pOPicture.Height);

			for (int liAt = 0; liAt < lOMask.Bits.Length; liAt++)
				lOMask.Bits[liAt] = pORule(pOPicture.Pixels[liAt]);

			return lOMask;
		}

		/// <summary>
		/// A 4-connected flood: the seeds (indices) and every pixel reached from them through pixels
		/// inside the mask's size that pOPasses holds for. The seeds are in whatever pOPasses says of
		/// them.
		/// </summary>
		public static UWMask Flood(int piWidth, int piHeight, IEnumerable<int> pOSeeds, Func<int, int, bool> pOPasses)
		{
			UWMask lOMask = new UWMask(piWidth, piHeight);
			Queue<int> lOQueue = new Queue<int>();

			foreach (int liSeed in pOSeeds)
			{
				if (lOMask.Bits[liSeed])
					continue;

				lOMask.Bits[liSeed] = true;
				lOQueue.Enqueue(liSeed);
			}

			while (lOQueue.Count > 0)
			{
				int liAt = lOQueue.Dequeue();
				int liX0 = liAt % lOMask.Width;
				int liY0 = liAt / lOMask.Width;

				for (int liStep = 0; liStep < 4; liStep++)
				{
					int liX = liX0 + (liStep == 0 ? 1 : liStep == 1 ? -1 : 0);
					int liY = liY0 + (liStep == 2 ? 1 : liStep == 3 ? -1 : 0);

					if (!lOMask.Contains(liX, liY) || lOMask.Bits[(liY * lOMask.Width) + liX] || !pOPasses(liX, liY))
						continue;

					lOMask.Bits[(liY * lOMask.Width) + liX] = true;
					lOQueue.Enqueue((liY * lOMask.Width) + liX);
				}
			}

			return lOMask;
		}

		/// <summary>The pixels of a picture reached from its border through pixels pORule holds for
		/// (4-connected) - a backdrop or a black edge around what stays.</summary>
		public static UWMask FloodFromBorder(UWPicture pOPicture, Func<UWColor32, bool> pORule)
		{
			int liWidth = pOPicture.Width;
			int liHeight = pOPicture.Height;
			List<int> lOSeeds = new List<int>();

			for (int y = 0; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					if ((x == 0 || y == 0 || x == liWidth - 1 || y == liHeight - 1) && pORule(pOPicture.Get(x, y)))
						lOSeeds.Add((y * liWidth) + x);
				}
			}

			return Flood(liWidth, liHeight, lOSeeds, (piX, piY) => pORule(pOPicture.Get(piX, piY)));
		}

		/// <summary>The set pixels among a pixel's eight neighbours.</summary>
		public int CountAround(int piX, int piY)
		{
			int liCount = 0;

			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					if ((dx != 0 || dy != 0) && IsSet(piX + dx, piY + dy))
						liCount++;
				}
			}

			return liCount;
		}

		/// <summary>Whether one of the eight neighbours of a pixel is set.</summary>
		public bool AnyAround(int piX, int piY)
		{
			return CountAround(piX, piY) > 0;
		}

		/// <summary>The largest connected piece of the set pixels (4- or 8-connected); of pieces as
		/// large the first found, scanning rows top down.</summary>
		public UWMask Largest(bool pbEightConnected)
		{
			UWMask lOBest = new UWMask(Width, Height);
			bool[] lbSeen = new bool[Bits.Length];
			List<int> lOPiece = new List<int>();
			Queue<int> lOQueue = new Queue<int>();
			int liBestSize = 0;

			for (int liStart = 0; liStart < Bits.Length; liStart++)
			{
				if (!Bits[liStart] || lbSeen[liStart])
					continue;

				lOPiece.Clear();
				lbSeen[liStart] = true;
				lOQueue.Enqueue(liStart);

				while (lOQueue.Count > 0)
				{
					int liAt = lOQueue.Dequeue();
					int liX = liAt % Width;
					int liY = liAt / Width;

					lOPiece.Add(liAt);

					for (int dy = -1; dy <= 1; dy++)
					{
						for (int dx = -1; dx <= 1; dx++)
						{
							if ((dx == 0 && dy == 0) || (!pbEightConnected && dx != 0 && dy != 0) || !IsSet(liX + dx, liY + dy))
								continue;

							int liNext = ((liY + dy) * Width) + liX + dx;

							if (lbSeen[liNext])
								continue;

							lbSeen[liNext] = true;
							lOQueue.Enqueue(liNext);
						}
					}
				}

				if (lOPiece.Count > liBestSize)
				{
					liBestSize = lOPiece.Count;
					Array.Clear(lOBest.Bits, 0, lOBest.Bits.Length);

					foreach (int liAt in lOPiece)
						lOBest.Bits[liAt] = true;
				}
			}

			return lOBest;
		}

		/// <summary>One pixel smaller all round: a pixel stays when it and its four neighbours are
		/// set; the outermost rows and columns go.</summary>
		public UWMask Eroded()
		{
			UWMask lOOut = new UWMask(Width, Height);

			for (int y = 1; y < Height - 1; y++)
			{
				for (int x = 1; x < Width - 1; x++)
				{
					int liAt = (y * Width) + x;

					lOOut.Bits[liAt] = Bits[liAt] && Bits[liAt - 1] && Bits[liAt + 1] && Bits[liAt - Width] && Bits[liAt + Width];
				}
			}

			return lOOut;
		}
	}
}
