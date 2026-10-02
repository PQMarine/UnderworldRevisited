using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ROLLING EDGES BESIDE THE TEXT (SCRLEDGE.GR). Left and right of the message box on the
	/// main screen and of the parchment in a conversation stands a four pixel wide strip that
	/// looks like the edge of a rolled up scroll, and it TURNS while the text moves. The user
	/// pointed at it 2026-09-22 ("several images that are a roll animation"), the disassembly
	/// says when it turns.
	///
	/// THE FILE holds 22 strips in four groups: 0 to 4 and 5 to 9 are 29 pixels high (main
	/// screen, left and right), 10 to 15 and 16 to 21 are 27 (conversation). Inside a group
	/// every image is the SAME texture moved by six pixels, so a group is a closed loop -
	/// measured on the exported images, 87 to 100 percent of the rows match at that shift, and
	/// between the groups nothing matches at all. The main screen rolls upwards in five frames,
	/// the conversation downwards in six.
	///
	/// WHEN IT TURNS: one frame per SCROLL of the text area, not per message. The printer
	/// (seg043_37F0_4BF, label 5E1) calls the scroll-up seg043_37F0_1CA for every line that
	/// would run past the bottom, WriteTextWithMore_seg043_37F0_257 calls it once to make room
	/// for [MORE], and seg043_37F0_8EB does it when the area is cleared and when a conversation
	/// starts. All of them end in the same fork: the active scroll dseg_5c99_3650 == 0x0A60 is
	/// the main message box, and then the dragon animation 1 (UpdateUIAnim_seg036_3087_3DF with
	/// 1, 4) and seg043_37F0_11E run, otherwise seg043_37F0_163 for the conversation. That is
	/// why the dragon and the edges always move together.
	///
	/// THE TWO DRAWING ROUTINES take one counter each. seg043_37F0_11E (137923-137972) draws
	/// counter + 0x20D5 at x 11 and counter + 0x20DA at x 306, both at y 169, then counts up
	/// and wraps at 5. seg043_37F0_163 (137978-138048) draws counter + 0x20DF at x 52 and
	/// counter + 0x20E5 at x 220, each at the three heights 51, 78 and 105 and all three with
	/// the SAME frame, then wraps at 6. The counters dseg_5c99_A94 and dseg_5c99_A96 are touched
	/// nowhere else and are never reset, so the phase carries over from one conversation to the
	/// next.
	///
	/// The image numbers are the pool ids of the original minus its base: 0x2000 plus the 213
	/// images loaded before SCRLEDGE.GR (LFTI 12, FLASKS 77, COMPASS 20, DRAGONS 36, INV 7,
	/// POWER 14, EYES 10, CHAINS 16, SPELLS 21) makes 0x20D5 the first strip - the same counting
	/// that gave the dragons their indices.
	/// </summary>
	public static class UWScrollEdgeRules
	{
		/// <summary>First image of the left strip beside the message box.</summary>
		public const int MainLeftFirstImage = 0;

		/// <summary>First image of the right strip beside the message box.</summary>
		public const int MainRightFirstImage = 5;

		/// <summary>Frames of the main screen loop (the counter wraps at 5).</summary>
		public const int MainFrames = 5;

		/// <summary>First image of the left strip beside the conversation parchment.</summary>
		public const int ConversationLeftFirstImage = 10;

		/// <summary>First image of the right strip beside the parchment.</summary>
		public const int ConversationRightFirstImage = 16;

		/// <summary>Frames of the conversation loop (the counter wraps at 6).</summary>
		public const int ConversationFrames = 6;

		/// <summary>The parchment carries the strip at three heights, all showing one frame.
		/// </summary>
		public const int ConversationRows = 3;

		/// <summary>One step of a counter.</summary>
		public static int Advance(int piFrame, int piFrames)
		{
			return fWrap(piFrame + 1, piFrames);
		}

		/// <summary>Several steps at once - a scroll that moved the text by more than one line.
		/// </summary>
		public static int Advance(int piFrame, int piFrames, int piSteps)
		{
			return piSteps <= 0 ? fWrap(piFrame, piFrames) : fWrap(piFrame + piSteps, piFrames);
		}

		public static int GetMainLeftImage(int piFrame)
		{
			return MainLeftFirstImage + fWrap(piFrame, MainFrames);
		}

		public static int GetMainRightImage(int piFrame)
		{
			return MainRightFirstImage + fWrap(piFrame, MainFrames);
		}

		public static int GetConversationLeftImage(int piFrame)
		{
			return ConversationLeftFirstImage + fWrap(piFrame, ConversationFrames);
		}

		public static int GetConversationRightImage(int piFrame)
		{
			return ConversationRightFirstImage + fWrap(piFrame, ConversationFrames);
		}

		/// <summary>
		/// HOW FAR THE VISIBLE WINDOW MOVED between two drawings, in lines - our way of noticing
		/// a scroll. The original knows it because it does the scrolling itself, one call per
		/// line that no longer fits; we rebuild the whole box from the text and therefore ask
		/// how far the old lines have slid up: the smallest k for which the old list from k on
		/// is the beginning of the new one. Nothing matching, or an empty new list, counts as no
		/// scroll, so a cleared box does not turn the strip.
		/// </summary>
		public static int GetScrollAmount(IList<string> pOBefore, IList<string> pOAfter)
		{
			if (pOBefore == null || pOAfter == null || pOBefore.Count == 0 || pOAfter.Count == 0)
				return 0;

			for (int liShift = 0; liShift < pOBefore.Count; liShift++)
			{
				if (fIsTail(pOBefore, liShift, pOAfter))
					return liShift;
			}

			return 0;
		}

		private static bool fIsTail(IList<string> pOBefore, int piShift, IList<string> pOAfter)
		{
			for (int liAt = piShift; liAt < pOBefore.Count; liAt++)
			{
				int liInAfter = liAt - piShift;

				if (liInAfter >= pOAfter.Count || pOBefore[liAt] != pOAfter[liInAfter])
					return false;
			}

			return true;
		}

		private static int fWrap(int piFrame, int piFrames)
		{
			if (piFrames <= 0)
				return 0;

			return ((piFrame % piFrames) + piFrames) % piFrames;
		}
	}
}
