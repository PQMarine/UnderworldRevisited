using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE HUD DRAGONS' FRAME LOGIC (cleanup 5.18, 2026-10-23: out of UWHudDragons, the state
	/// machine as it was; the pictures and the step timing stay with the Unity class), after UW.EXE
	/// (UpdateUIAnim_seg036_3087_3DF mode 4 for the requests, Dragons_seg036_3087_A20 for the
	/// state machine, UI update seg036_3087_5A5 for the tail roll; per user, 2026-09-14: "build the
	/// dragons after UW.EXE").
	///
	/// THREE HEAD ANIMATIONS, requested by the game:
	///   1 scroll  the message scroll moves on         frames 2..5 twice, then hidden
	///   2 nod     the player kills a creature         frames 6,7,8,9,8,9,8,9,8,7,6
	///   3 cover   heavy damage or damage below 16 HP  frames 10..13, hold 6 steps, back to 10
	/// The right dragon uses the same frames plus 18. A request for an animation that already runs
	/// or waits on either dragon is ignored; each dragon has one waiting slot, and a waiting request
	/// cuts the running animation short (scroll finishes its pass, nod plays backward, cover ends
	/// its hold).
	///
	/// THE TAIL WAG (frames 14,15,16,17,16,15,14) is rolled at random, about 5 % per step. UW.EXE
	/// sets the wrong redraw bit for it (the flask bit instead of the dragon's), so the wag only
	/// plays while that dragon's state machine runs anyway - during or right after a head
	/// animation. That quirk is kept, and so is the shared tail frame counter of both dragons.
	///
	/// THE RANDOM DRAWS keep their order (section 11 of the cleanup plan): per step first the
	/// flask bubble roll of UW.EXE, which does not change the dragons, then the tail roll; a
	/// request with both dragons free, or both busy with nothing waiting, draws the side. The
	/// generator is handed in (the Unity class keeps its clock-seeded System.Random); the
	/// self-check scripts it.
	///
	/// The animator tells its pictures (IPictures) what to show: a frame of DRAGONS.GR on the tail
	/// or the head's overlay, the overlay's corner in the original's pixels (UWClassicLayout.
	/// Dragons.Overlay, the right dragon moved by the widened frame's columns), the head hidden
	/// while it covers its eyes.
	/// </summary>
	public sealed class UWDragonAnimator
	{
		/// <summary>The pictures the animator drives, one of each per side (0 left, 1 right).</summary>
		public interface IPictures
		{
			void ShowTail(int piSide, int piImage);

			void ShowOverlay(int piSide, int piImage);

			void HideOverlay(int piSide);

			/// <summary>The overlay's top-left corner in the frame's pixels, y from the top.</summary>
			void PlaceOverlay(int piSide, int piX, int piY);

			void ShowHead(int piSide, int piImage);

			void HideHead(int piSide);
		}

		public const int ScrollAnimation = 1;

		public const int NodAnimation = 2;

		public const int CoverAnimation = 3;

		/// <summary>Right dragon images are the left ones plus this.</summary>
		public const int RightImageOffset = 18;

		public const int NeckImage = 0;

		public const int HeadImage = 1;

		public const int TailRestImage = 14;

		/// <summary>RNG() &lt; 0x666 of 0..32767, UW.EXE seg036_3087_5A5.</summary>
		public const int TailRollThreshold = 0x666;

		private static readonly int[] msTailSequence = { 14, 15, 16, 17, 16, 15, 14 };

		private static readonly int[] msFirstFrame = { 2, 6, 10 };

		private static readonly int[] msLastFrame = { 5, 9, 13 };

		private readonly IPictures mOPictures;

		/// <summary>How far the right dragon's pictures lie right of their original places - the
		/// widened frame's columns (Classic Wide).</summary>
		private readonly int miRightShift;

		/// <summary>The random draw: a value from 0 to the bound given, exclusive (System.Random.Next).</summary>
		private readonly Func<int, int> mONext;

		private readonly int[] miCurrent = new int[2];

		private readonly int[] miPending = new int[2];

		private readonly int[] miPhase = new int[2];

		private readonly int[] miCounter = new int[2];

		private readonly int[] miLoop = new int[2];

		private readonly bool[] mbRunning = new bool[2];

		private readonly bool[] mbTailWag = new bool[2];

		/// <summary>Shared by both dragons, as in UW.EXE (dseg 91C).</summary>
		private int miTailCounter;

		public UWDragonAnimator(IPictures pOPictures, int piRightShift, Func<int, int> pONext)
		{
			mOPictures = pOPictures;
			miRightShift = piRightShift;
			mONext = pONext;
		}

		/// <summary>The animation a dragon plays, 0 for none.</summary>
		public int Current(int piSide)
		{
			return miCurrent[piSide];
		}

		/// <summary>The dragon covers its eyes: its resting head is hidden.</summary>
		public bool IsCovering(int piSide)
		{
			return miCurrent[piSide] == CoverAnimation;
		}

		/// <summary>An animation is under way on the overlay (its phase is not 0).</summary>
		public bool IsOverlayActive(int piSide)
		{
			return miPhase[piSide] != 0;
		}

		/// <summary>
		/// A request from the game - UW.EXE UpdateUIAnim_seg036_3087_3DF case 4. Picks the dragon:
		/// a free one if only one is free, otherwise at random or the one without a waiting request.
		/// </summary>
		public void Request(int piValue)
		{
			if (piValue == miPending[0] || piValue == miPending[1] || piValue == miCurrent[0] || piValue == miCurrent[1])
				return;

			int liSide;

			if (miCurrent[0] == 0 && miCurrent[1] == 0)
				liSide = mONext(2);
			else if (miCurrent[0] != 0 && miCurrent[1] == 0)
				liSide = 1;
			else if (miCurrent[0] == 0)
				liSide = 0;
			else if (miPending[0] == 0 && miPending[1] == 0)
				liSide = mONext(2);
			else if (miPending[0] != 0 && miPending[1] == 0)
				liSide = 1;
			else
				liSide = 0;

			miPending[liSide] = piValue;
			mbRunning[liSide] = true;
		}

		/// <summary>One step of the 0.25 s clock (the Unity class keeps the time).</summary>
		public void Step()
		{
			// The flask bubble roll comes first in UW.EXE; it does not change the dragons.
			mONext(32768);

			int liRoll = mONext(32768);

			if (liRoll < TailRollThreshold && !mbTailWag[liRoll & 1])
				mbTailWag[liRoll & 1] = true;

			for (int liSide = 0; liSide < 2; liSide++)
			{
				if (mbRunning[liSide])
					fStep(liSide);
			}
		}

		private void fStep(int piSide)
		{
			if (mbTailWag[piSide])
			{
				mOPictures.ShowTail(piSide, msTailSequence[miTailCounter] + (piSide * RightImageOffset));

				if (++miTailCounter >= msTailSequence.Length)
				{
					miTailCounter = 0;
					mbTailWag[piSide] = false;
				}
			}

			if (miPhase[piSide] == 0 && miPending[piSide] != 0)
			{
				miCurrent[piSide] = miPending[piSide];
				miPhase[piSide] = 1;
			}

			switch (miCurrent[piSide])
			{
				case 0:
					if (!mbTailWag[piSide])
						mbRunning[piSide] = false;
					break;

				case ScrollAnimation:
					fStepScroll(piSide);
					break;

				case NodAnimation:
					fStepNod(piSide);
					break;

				case CoverAnimation:
					fStepCover(piSide);
					break;
			}
		}

		private int fFirst(int piSide) => msFirstFrame[miCurrent[piSide] - 1] + (piSide * RightImageOffset);

		private int fLast(int piSide) => msLastFrame[miCurrent[piSide] - 1] + (piSide * RightImageOffset);

		private void fBegin(int piSide, int piLoops)
		{
			miPending[piSide] = 0;
			miCounter[piSide] = fFirst(piSide);
			miLoop[piSide] = piLoops;

			// [side, animation - 1]: overlay corner (dseg 824/830, 91E/92A).
			(int X, int Y) lOCorner = UWClassicLayout.Dragons.Overlay[piSide, miCurrent[piSide] - 1];

			mOPictures.PlaceOverlay(piSide, lOCorner.X + (piSide * miRightShift), lOCorner.Y);
		}

		private void fEnd(int piSide)
		{
			mOPictures.HideOverlay(piSide);
			miCurrent[piSide] = 0;
			miPhase[piSide] = 0;
		}

		/// <summary>Scroll: P1 start, P3 forward loop, P4 last pass, P5 hide.</summary>
		private void fStepScroll(int piSide)
		{
			switch (miPhase[piSide])
			{
				case 1:
					fBegin(piSide, 1);
					miPhase[piSide] = 3;
					goto case 3;

				case 3:
					mOPictures.ShowOverlay(piSide, miCounter[piSide]++);

					if (miCounter[piSide] > fLast(piSide))
					{
						miCounter[piSide] = fFirst(piSide);
						miLoop[piSide]--;
					}

					if (miPending[piSide] != 0 || miLoop[piSide] == 0)
						miPhase[piSide] = 4;
					break;

				case 4:
					mOPictures.ShowOverlay(piSide, miCounter[piSide]++);

					if (miCounter[piSide] > fLast(piSide))
						miPhase[piSide] = 5;
					break;

				case 5:
					fEnd(piSide);
					break;
			}
		}

		/// <summary>Nod: P2 lead-in, P3 loop over the last two frames, P4 backward, P5 hide.</summary>
		private void fStepNod(int piSide)
		{
			switch (miPhase[piSide])
			{
				case 1:
					fBegin(piSide, 3);
					miPhase[piSide] = 2;
					goto case 2;

				case 2:
					mOPictures.ShowOverlay(piSide, miCounter[piSide]++);

					if (miCounter[piSide] > fFirst(piSide) + 1)
						miPhase[piSide] = 3;
					break;

				case 3:
					mOPictures.ShowOverlay(piSide, miCounter[piSide]++);

					if (miCounter[piSide] > fLast(piSide))
					{
						miCounter[piSide] = fFirst(piSide) + 2;
						miLoop[piSide]--;
					}

					if (miPending[piSide] != 0 || miLoop[piSide] == 0)
						miPhase[piSide] = 4;
					break;

				case 4:
					mOPictures.ShowOverlay(piSide, miCounter[piSide]--);

					if (miCounter[piSide] < fFirst(piSide))
						miPhase[piSide] = 5;
					break;

				case 5:
					fEnd(piSide);
					break;
			}
		}

		/// <summary>Cover: P1 hide the head, P2 forward, P3 hold, P4 backward, P5 head back.</summary>
		private void fStepCover(int piSide)
		{
			switch (miPhase[piSide])
			{
				case 1:
					mOPictures.HideHead(piSide);
					fBegin(piSide, 6);
					miPhase[piSide] = 2;
					goto case 2;

				case 2:
					mOPictures.ShowOverlay(piSide, miCounter[piSide]++);

					if (miCounter[piSide] > fLast(piSide))
					{
						miCounter[piSide]--;
						miPhase[piSide] = 3;
					}
					break;

				case 3:
					if (miPending[piSide] != 0 || --miLoop[piSide] == 0)
						miPhase[piSide] = 4;
					break;

				case 4:
					mOPictures.ShowOverlay(piSide, --miCounter[piSide]);

					if (miCounter[piSide] == fFirst(piSide))
						miPhase[piSide] = 5;
					break;

				case 5:
					mOPictures.ShowHead(piSide, HeadImage + (piSide * RightImageOffset));
					fEnd(piSide);
					break;
			}
		}
	}
}
