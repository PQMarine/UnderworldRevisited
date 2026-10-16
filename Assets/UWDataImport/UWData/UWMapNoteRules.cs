using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The notes on the automap as ManageDungeonMap takes them (ui/AUTOMAP.C:408-547 of
	/// UnderworldExhumed; reference audit 2026-10-14, A33, built 2026-10-15). UWHudMap keeps the
	/// screen, the pointer and the keyboard; what a note may hold and which one the eraser takes
	/// is decided here.
	///
	/// Coordinates are the ones the save game keeps (UWLevel.MapNote): x a screen pixel, y counted
	/// from the bottom, the note's top row; the text reaches down five rows from it.
	///
	/// NOT BUILT: the original wastes the slot of an erased note that is not the last one until
	/// the map page is saved (SaveTheWords compacts the list on closing or paging), and those
	/// holes count towards the hundred; ours removes the note at once. And it beeps when a
	/// character is refused, ours stays silent.
	/// </summary>
	public static class UWMapNoteRules
	{
		/// <summary>A level page holds at most a hundred notes (num_words != 100 before a new
		/// one starts; a click on the parchment then does nothing).</summary>
		public const int MaxNotesPerLevel = 100;

		/// <summary>46 characters: the original refuses a character once the index passes 0x2D.</summary>
		public const int MaxLength = 46;

		/// <summary>Only codes 0x20 to 0x7A are taken (upper-cased): the four characters above
		/// "z" ({, |, } and ~) are ignored.</summary>
		public const char FirstTypeable = (char)0x20;

		public const char LastTypeable = (char)0x7A;

		/// <summary>A character that would put the note's right end past x 0x13B is refused -
		/// the right edge of the parchment.</summary>
		public const int RightEdge = 0x13B;

		/// <summary>The rows a note covers below its top (the eraser's box, note y - 5 to y).</summary>
		public const int BoxHeight = 5;

		/// <summary>DetermineBetterHit measures from the note's middle row, two below the top.</summary>
		private const int MiddleBelowTop = 2;

		/// <summary>Whether a new note may be started on a page holding piCount.</summary>
		public static bool CanAddNote(int piCount)
		{
			return piCount < MaxNotesPerLevel;
		}

		/// <summary>
		/// One typed character onto the note: false (nothing changes) for a code outside 0x20 to
		/// 0x7A, for a character whose width would carry the note past the right edge, and past
		/// 46 characters - checked in that order, as ManageDungeonMap does. pfWidth is
		/// string_width of the small font (the sum of the glyph widths).
		/// </summary>
		public static bool TryAppend(string psText, char pcTyped, int piX, Func<string, int> pfWidth, out string psNew)
		{
			psNew = psText ?? string.Empty;

			if (pcTyped < FirstTypeable || pcTyped > LastTypeable || pfWidth == null)
				return false;

			string lsCandidate = psNew + char.ToUpperInvariant(pcTyped);

			if (piX + pfWidth(lsCandidate) > RightEdge)
				return false;

			if (lsCandidate.Length > MaxLength)
				return false;

			psNew = lsCandidate;

			return true;
		}

		/// <summary>
		/// The note the eraser takes at (piX, piY), or -1: of all notes whose box (x to x + width,
		/// y - 5 to y) holds the point, DetermineBetterHit (AUTOMAP.C:376-402) keeps the earlier
		/// one unless the later one's middle is nearer vertically, or - on its own - nearer
		/// horizontally. Until 2026-10-15 ours erased the first note under the pointer.
		/// </summary>
		public static int FindNoteToErase(IList<UWLevel.MapNote> pONotes, int piX, int piY, Func<string, int> pfWidth)
		{
			if (pONotes == null || pfWidth == null)
				return -1;

			int liBest = -1;
			int liBestWidth = 0;

			for (int liAt = 0; liAt < pONotes.Count; liAt++)
			{
				UWLevel.MapNote lONote = pONotes[liAt];

				if (string.IsNullOrEmpty(lONote.Text))
					continue;

				int liWidth = pfWidth(lONote.Text);

				if (piX < lONote.X || piX > lONote.X + liWidth || piY < lONote.Y - BoxHeight || piY > lONote.Y)
					continue;

				if (liBest < 0 || fIsNearer(lONote, liWidth, pONotes[liBest], liBestWidth, piX, piY))
				{
					liBest = liAt;
					liBestWidth = liWidth;
				}
			}

			return liBest;
		}

		/// <summary>DetermineBetterHit's two tests, b against the kept a.</summary>
		private static bool fIsNearer(UWLevel.MapNote pOB, int piBWidth, UWLevel.MapNote pOA, int piAWidth, int piX, int piY)
		{
			int liAX = Math.Abs(pOA.X + (piAWidth / 2) - piX);
			int liAY = Math.Abs(pOA.Y - MiddleBelowTop - piY);
			int liBX = Math.Abs(pOB.X + (piBWidth / 2) - piX);
			int liBY = Math.Abs(pOB.Y - MiddleBelowTop - piY);

			return liBY < liAY || liBX < liAX;
		}
	}
}
