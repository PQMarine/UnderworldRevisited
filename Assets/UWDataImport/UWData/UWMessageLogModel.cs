using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MESSAGE LOG AS BOTH SCHEMES SHOW IT (cleanup step 5.7, 2026-10-20): which lines the scroll
	/// shows (wrapped, the newest kept), the colour code in effect at each line's start, the [MORE]
	/// row, the rolling edges' frame, the input box's blink and the paging of a long text. Until then
	/// the classic box (UWHudMessageLog) and the modern scroll (UWModernScroll) each wrapped, trimmed,
	/// rolled their edges and placed [MORE] in their own copy, blinked at different rates, and
	/// Interaction paged long texts by the classic box's five lines while the modern scroll showed two
	/// to eight (B28). Engine-free (decision 26 of the cleanup plan): the renderers hand in the
	/// messages (Interaction.ActiveStrings), the width and lines of the scroll they draw and the time,
	/// and draw what comes back.
	///
	/// ONE [MORE] POLICY (decision 17, DECIDED 2026-10-20 per user): a long text pages by what the
	/// SHOWN scroll holds, its lines less one for the [MORE] row - the classic box its five (four new
	/// lines and [MORE], as before), the modern scroll its configured size (two to eight lines, four
	/// in the Classic Wide preset). While [MORE] waits, its row (the scroll's last) stays free of text.
	/// The capacity is asked again for every page, so a scheme switch (Shift+F2) in the middle of a
	/// text pages the rest by the scroll now shown.
	///
	/// ONE BLINK RULE (decision 17): the input box is on for 0.4 s and off for 0.4 s in both schemes
	/// (the classic box's rate; the modern scroll showed it for the first half of every second). The
	/// original counts passes of its input loop instead of time: the box is drawn while the counter
	/// stands at 2000 to 3999, cleared at 4000 and the counter starts over (the scroll's answer input,
	/// uw1/src/ui/SCROLLIO.C:538-557 of UnderworldExhumed), so its speed hung on the CPU; recorded in
	/// Docs/DEVIATIONS.md, Rules.
	///
	/// ONE INSTANCE PER SCROLL: the edges' frame is counted per scroll as dseg_5c99_A94 counts it in
	/// the original (UWScrollEdgeRules) - one step per line that left the top, never reset.
	/// </summary>
	public sealed class UWMessageLogModel
	{
		/// <summary>The hint that more of a text is pending. Not in the game data - in the original it
		/// comes from the program (per user on the original, 2026-09-03: white, in square brackets).
		/// A text id since translation step 3A (msg.scroll.more).
		/// </summary>
		public static string MoreMarkerText => UWText.T("msg.scroll.more");

		/// <summary>How long the input box is on, and then off (decision 17, see the class comment).
		/// </summary>
		public const float CursorBlinkSeconds = 0.4f;

		/// <summary>The classic box's lines while no font is loaded: its 30 rows over font5x6p's six
		/// (UWTextLayout.LineSpacing).</summary>
		public const int FallbackCapacity = 5;

		/// <summary>
		/// The text colours of the message box by colour code 0 to 6, as palette indices.
		/// UW.EXE has no table for them: the message scroll print routine (seg043_37F0_4BF, called
		/// from WriteMultipleStringsToMessageScroll_seg043_37F0_2EA) compares the character after a
		/// backslash and writes a fixed byte into the font colour for each code (GOG build: the
		/// immediate values at file 0x3976D-0x397AF), verified 2026-09-15. Codes 7 to 9 do not exist
		/// in uw1 and leave the colour unchanged (UWTextRaster ignores codes beyond the table).
		/// 0 is the usual brown (index 46 = 3C 28 20, also the default at DS:0A73; the fallback colour,
		/// 3D 1C 08, is only used while no palette is loaded), 4 the red of
		/// "Save Game Failed.", 6 the green of "Save Game Succeeded." and of the name prompt - that
		/// is how the user sees it in the original (2026-09-12). Code 3 (96) is also the colour of
		/// [MORE].
		/// </summary>
		public static readonly int[] ColourIndices = UWClassicLayout.MessageLog.ColourIndices;

		/// <summary>The number of colour codes the log knows (0 to 6).</summary>
		public static int ColourCodeCount => ColourIndices.Length;

		// ------------------------------------------------- the rules

		/// <summary>Whether the input box is drawn at this time (seconds of the unscaled clock): on
		/// for CursorBlinkSeconds, off for as long, starting on.</summary>
		public static bool IsCursorOn(float pfSeconds)
		{
			return ((int)(pfSeconds / CursorBlinkSeconds) & 1) == 0;
		}

		/// <summary>How many lines fit into a box piHeight rows tall: computed from the font, not
		/// hard-coded (the classic box: 30 rows, five lines).</summary>
		public static int CapacityOf(UWFont pOFont, int piHeight)
		{
			if (pOFont == null || pOFont.Height <= 0)
				return FallbackCapacity;

			return Math.Max(1, (piHeight + UWTextLayout.LineSpacing) / (pOFont.Height + UWTextLayout.LineSpacing));
		}

		/// <summary>The lines above [MORE] while it waits - also the new lines a page brings.</summary>
		public static int LinesAboveMore(int piCapacity)
		{
			return Math.Max(1, piCapacity - 1);
		}

		/// <summary>The row [MORE] is drawn in, counted from the top: the scroll's last.</summary>
		public static int MoreRow(int piCapacity)
		{
			return Math.Max(0, piCapacity - 1);
		}

		/// <summary>
		/// Wraps text the way the scroll would show it, for everything that needs to know how many
		/// lines a text occupies - the paging of long texts (ShowNextPage). A line of nothing but
		/// spaces counts as an empty line here; the scroll's own wrap (Update) keeps such a line as it
		/// is - both draw nothing, the strings differ.
		/// </summary>
		public static List<string> WrapForPaging(UWFont pOFont, string psText, int piWidth)
		{
			List<string> lOLines = new List<string>();

			if (pOFont == null || string.IsNullOrEmpty(psText))
				return lOLines;

			foreach (string lsLine in psText.Split('\n'))
			{
				if (string.IsNullOrEmpty(lsLine.Trim()))
				{
					lOLines.Add(string.Empty);
					continue;
				}

				lOLines.AddRange(UWTextLayout.WrapText(pOFont, lsLine, piWidth));
			}

			return lOLines;
		}

		/// <summary>
		/// Prints the next page of a long text (Interaction.ShowPagedText fills the lists).
		///
		/// The log SCROLLS in the process, it is not cleared: per page up to LinesAboveMore new
		/// lines are added (four in the classic box), and the last ones are always shown. If more is
		/// pending, the last line stays free for the [MORE] hint, so only the lines above it are
		/// visible.
		///
		/// Until 2026-09-03 the box was cleared instead and only the rest shown - this made
		/// the text slide two lines too far up. In the original, after confirming, the
		/// third line is at the very top again (measured by user on the Lakshi letter).
		///
		/// piCapacity is the lines of the scroll shown now (decision 17): asked per page, so the
		/// page follows a scheme switch. pOShown receives the lines the log holds afterwards
		/// (Interaction.ActiveStrings). Returns whether the scroll moved (more lines printed than it
		/// holds) - paging moves the message scroll too (UW.EXE seg043_37F0_8EB), the caller turns
		/// the dragon then.
		/// </summary>
		public static bool ShowNextPage(List<string> pOPrinted, List<string> pOPending, int piCapacity, List<string> pOShown)
		{
			int liPerPage = LinesAboveMore(piCapacity);

			for (int liAt = 0; liAt < liPerPage && pOPending.Count > 0; liAt++)
			{
				pOPrinted.Add(pOPending[0]);
				pOPending.RemoveAt(0);
			}

			bool lbScrolled = pOPrinted.Count > piCapacity;
			int liShow = pOPending.Count > 0 ? LinesAboveMore(piCapacity) : piCapacity;

			pOShown.Clear();

			for (int liAt = Math.Max(0, pOPrinted.Count - liShow); liAt < pOPrinted.Count; liAt++)
				pOShown.Add(pOPrinted[liAt]);

			return lbScrolled;
		}

		/// <summary>The colour code in effect after a line, starting from piCode: a backslash and a
		/// digit switch to that code, a digit beyond the table leaves it (UWTextRaster draws the same).
		/// </summary>
		public static int ColourCodeAfter(string psLine, int piCode)
		{
			for (int liAt = 0; psLine != null && liAt < psLine.Length; liAt++)
			{
				if (!UWFont.IsColourCode(psLine, liAt))
					continue;

				int liCode = psLine[liAt + 1] - '0';

				if (liCode < ColourCodeCount)
					piCode = liCode;

				liAt++;
			}

			return piCode;
		}

		/// <summary>The colour code at each line's start, the first line starting at 0 and a code
		/// holding across lines ("\6     Please enter a save file description:" also colours the
		/// typed line below it).</summary>
		public static int[] LineColourCodesOf(IReadOnlyList<string> pOLines)
		{
			int[] liyCodes = new int[pOLines != null ? pOLines.Count : 0];
			int liCode = 0;

			for (int liAt = 0; liAt < liyCodes.Length; liAt++)
			{
				liyCodes[liAt] = liCode;
				liCode = ColourCodeAfter(pOLines[liAt], liCode);
			}

			return liyCodes;
		}

		/// <summary>The log's colours by code from palette 0, or - while no palette is loaded - the
		/// fallback colour for every code (pbFromPalette false: such a table is not to be kept).
		/// </summary>
		public static UWColor32[] ColourTable(UWPalettes pOPalettes, out bool pbFromPalette)
		{
			UWColor32[] lyTable = new UWColor32[ColourIndices.Length];
			UWPalette lOPalette = pOPalettes != null ? pOPalettes.GetPalette(0) : null;

			pbFromPalette = pOPalettes != null;

			for (int liAt = 0; liAt < lyTable.Length; liAt++)
			{
				if (!pbFromPalette)
				{
					lyTable[liAt] = UWClassicLayout.MessageLog.FallbackColour;
					continue;
				}

				if (lOPalette == null)
				{
					lyTable[liAt] = new UWColor32(255, 255, 255, 255);
					continue;
				}

				UWColor32 lOColour = lOPalette.GetUWColor(ColourIndices[liAt]);

				lyTable[liAt] = new UWColor32(lOColour.R, lOColour.G, lOColour.B, 255);
			}

			return lyTable;
		}

		// ------------------------------------------------- one scroll

		private string msKey;

		private List<string> mOShown = new List<string>();

		private int[] miyLineColourCodes = new int[0];

		/// <summary>The lines the scroll shows, oldest first; empty lines stay empty.</summary>
		public IReadOnlyList<string> ShownLines => mOShown;

		/// <summary>The colour code at the start of each shown line (LineColourCodesOf).</summary>
		public IReadOnlyList<int> LineColourCodes => miyLineColourCodes;

		/// <summary>The width the lines were wrapped to - the width to draw them at (B27: the modern
		/// scroll wrapped wider than it drew in Classic Wide).</summary>
		public int Width { get; private set; }

		/// <summary>The lines the scroll holds.</summary>
		public int Capacity { get; private set; }

		/// <summary>[MORE] is shown, in MoreRowShown.</summary>
		public bool MoreShown { get; private set; }

		public int MoreRowShown => MoreRow(Capacity);

		/// <summary>The input box behind the last line is drawn.</summary>
		public bool CursorOn { get; private set; }

		/// <summary>The rolling edges' frame (UWScrollEdgeRules.GetMainLeftImage/GetMainRightImage).
		/// </summary>
		public int EdgeFrame { get; private set; }

		/// <summary>How many lines the last change scrolled the text up (the edges turned as many
		/// frames).</summary>
		public int Scrolled { get; private set; }

		/// <summary>
		/// The scroll's state for these messages. Returns false - and changes nothing - while the
		/// messages, the width, the lines, the waiting [MORE] and the input box are as last time;
		/// otherwise wraps anew: each message split at its line breaks, an empty one kept empty,
		/// the others wrapped to piWidth; the newest that fit kept (as many as piCapacity, the lines
		/// above [MORE] while it waits); the edges turned one frame per line that left the top.
		/// pbCursorOn is the input box's state (a running input that shows one, and IsCursorOn).
		/// </summary>
		public bool Update(UWFont pOFont, IReadOnlyList<string> pOMessages, int piWidth, int piCapacity, bool pbWaitingForPage,
			bool pbCursorOn)
		{
			string lsText = pOMessages != null ? string.Join("\n", pOMessages) : string.Empty;

			// The input box is not in the text but only in the key - the finished lines would keep it.
			string lsKey = lsText + (pbCursorOn ? "\u0001" : string.Empty) + (pbWaitingForPage ? "\u0002" : string.Empty)
				+ "\u0003" + piWidth + "\u0004" + piCapacity;

			if (lsKey == msKey)
				return false;

			msKey = lsKey;

			List<string> lOLines = new List<string>();

			foreach (string lsLine in lsText.Split('\n'))
			{
				if (string.IsNullOrEmpty(lsLine))
				{
					lOLines.Add(string.Empty);
					continue;
				}

				lOLines.AddRange(UWTextLayout.WrapText(pOFont, lsLine, piWidth));
			}

			int liRoom = pbWaitingForPage ? LinesAboveMore(piCapacity) : Math.Max(1, piCapacity);

			if (lOLines.Count > liRoom)
				lOLines.RemoveRange(0, lOLines.Count - liRoom);

			// ONE STEP PER SCROLL, as in the original (UWScrollEdgeRules): the frame only moves when
			// lines have left the box at the top. While the box is still filling up, nothing scrolls
			// and nothing turns; a blinking cursor redraws the same lines and leaves the strip alone.
			Scrolled = Math.Max(0, UWScrollEdgeRules.GetScrollAmount(mOShown, lOLines));

			if (Scrolled > 0)
				EdgeFrame = UWScrollEdgeRules.Advance(EdgeFrame, UWScrollEdgeRules.MainFrames, Scrolled);

			mOShown = lOLines;
			miyLineColourCodes = LineColourCodesOf(lOLines);
			Width = piWidth;
			Capacity = piCapacity;
			MoreShown = pbWaitingForPage;
			CursorOn = pbCursorOn;

			return true;
		}

		/// <summary>The scroll was hidden (a conversation, the big map): the next Update draws anew.
		/// The shown lines and the edges' frame stay, so the strip does not turn for lines it showed.
		/// </summary>
		public void Invalidate()
		{
			msKey = null;
		}
	}
}
