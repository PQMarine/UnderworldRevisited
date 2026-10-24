using System;
using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MAP'S NOTES AND ITS CLICKS (cleanup step 5.11, 2026-10-19): which level page is shown,
	/// what a click on the parchment does (end the note being written, arm the eraser, erase, page,
	/// close, start a note), typing and Backspace, and where each note and the writing quill stand.
	/// UWHudMap kept all of it next to its pictures until then, and the help window's map tab and
	/// the modern minimap laid the notes out a second time (UWHudMap.GetMinimapNotes); both
	/// renderers now take the notes' places from Layout. What a note may hold and which note the
	/// eraser takes stays UWMapNoteRules' (after ManageDungeonMap, ui/AUTOMAP.C).
	///
	/// Coordinates: a note keeps the save game's (UWLevel.MapNote: x a screen pixel, y counted from
	/// the bottom, the note's top row). A click comes in screen pixels of the 320x200 map screen, y
	/// from the TOP, as the pointer lands on the parchment; Layout answers in the same.
	/// </summary>
	public sealed class UWMapNotesModel
	{
		/// <summary>What a click on the parchment did.</summary>
		public enum ClickEnum
		{
			/// <summary>Nothing changed (a page arrow at the first or last page, a full page).</summary>
			None,

			/// <summary>The note being written ended (kept unless empty) - any click does only that.</summary>
			NoteEnded,

			/// <summary>ERASE was hit: the next click erases.</summary>
			EraseArmed,

			/// <summary>The armed eraser was used - on a note or not, the eraser is put down again.</summary>
			Erased,

			/// <summary>Another level page is shown.</summary>
			Paged,

			/// <summary>CLOSE was hit.</summary>
			Close,

			/// <summary>A new note starts at the click.</summary>
			NoteStarted
		}

		/// <summary>A note as it is drawn: its index on the page, its text and its top-left corner
		/// on the map screen (y from the top), and the width of its label.</summary>
		public struct ShownNote
		{
			public int Index;

			public string Text;

			public int Left;

			public int Top;

			public int Width;
		}

		private readonly Func<int, List<UWLevel.MapNote>> mFNotes;

		private readonly Func<string, int> mFWidth;

		private readonly List<ShownNote> mOLayout = new List<ShownNote>();

		/// <summary>pFNotes: the notes of a level page (DataImport.GetMapNotes, null when there are
		/// none); pFWidth: string_width of the notes' small font (the sum of the glyph widths).</summary>
		public UWMapNotesModel(Func<int, List<UWLevel.MapNote>> pFNotes, Func<string, int> pFWidth)
		{
			mFNotes = pFNotes;
			mFWidth = pFWidth;
		}

		/// <summary>
		/// The currently shown level. On opening it is the own one, with the two side corners
		/// on the right it can be changed.
		/// </summary>
		public int ShownLevel { get; private set; }

		/// <summary>The eraser is armed: the pointer is the eraser stone.</summary>
		public bool IsErasing { get; private set; }

		/// <summary>A note is being written: the keyboard belongs to it.</summary>
		public bool IsWriting { get; private set; }

		/// <summary>The index of the note being written on the shown page, or -1.</summary>
		public int WritingNote { get; private set; } = -1;

		/// <summary>The map opens on a level page.</summary>
		public void Show(int piLevel)
		{
			ShownLevel = piLevel;
		}

		/// <summary>The map closes: the note being written is kept (unless empty), the eraser is put
		/// down. True when a note was being written.</summary>
		public bool Close()
		{
			bool lbEnded = End(true);

			IsErasing = false;

			return lbEnded;
		}

		/// <summary>
		/// A click on the map screen at pfX/pfYTop, with either button - the original's map listens to
		/// both everywhere (per user on the original, 2026-09-03; AUTOMAP.C: any mouse command below
		/// 4). In this order:
		///   1. While writing, any click only ends the note (the typing loop of ManageDungeonMap
		///      breaks on a mouse command and returns) - also on ERASE, CLOSE or an arrow, which with
		///      the arrows taking the right button too would otherwise page away from the note being
		///      written (2026-10-15).
		///   2. ERASE (the stone including the label below it) arms the eraser - the pointer becomes
		///      the eraser stone; once armed, the next click erases the note under it, if one lies
		///      there, and brings the quill back (per user in the original, 2026-09-03). If you miss,
		///      erase mode ends anyway.
		///   3. The corners at top and bottom right page: the top one goes one level up, the bottom
		///      one down (until 2026-10-15 a right click there started a note).
		///   4. CLOSE closes.
		///   5. Anywhere else a new note starts; a click while writing only finished the last one and
		///      does NOT immediately start the next one (1.).
		/// </summary>
		public ClickEnum Click(float pfX, float pfYTop)
		{
			if (IsWriting)
			{
				End(true);

				return ClickEnum.NoteEnded;
			}

			if (!IsErasing)
			{
				if (fIsIn(UWClassicLayout.Map.Erase, pfX, pfYTop))
				{
					IsErasing = true;

					return ClickEnum.EraseArmed;
				}
			}
			else
			{
				EraseAt(fRound(pfX), fRound(UWClassicLayout.Screen.Height - pfYTop));

				IsErasing = false;

				return ClickEnum.Erased;
			}

			int liStep = fIsIn(UWClassicLayout.Map.PageUp, pfX, pfYTop) ? -1
				: fIsIn(UWClassicLayout.Map.PageDown, pfX, pfYTop) ? 1
				: 0;

			if (liStep != 0)
				return PageBy(liStep) ? ClickEnum.Paged : ClickEnum.None;

			if (fIsIn(UWClassicLayout.Map.Close, pfX, pfYTop))
				return ClickEnum.Close;

			// The text starts at the clicked column, its top four rows above the clicked row, as
			// the original places it from the pointer's hot spot (UWClassicLayout.Map.NoteColumnOffset,
			// NoteRowsAboveClick; 2026-10-23 from AUTOMAP.C - three left and three below until then,
			// per user on the original 2026-09-03 with a pointer the system drew too small). Y counts
			// from the bottom, so higher means larger.
			return Begin(fRound(pfX) + UWClassicLayout.Map.NoteColumnOffset,
				fRound(UWClassicLayout.Screen.Height - pfYTop) + UWClassicLayout.Map.NoteRowsAboveClick)
				? ClickEnum.NoteStarted
				: ClickEnum.None;
		}

		/// <summary>One level page further, clamped to the pages the game keeps: up to level 99,
		/// that many note pages there are, even if only the first nine have a map. True when the
		/// page changed.</summary>
		public bool PageBy(int piStep)
		{
			int liNew = Math.Max(0, Math.Min(ShownLevel + piStep, DataImport.MapNoteLevelCount - 1));

			if (liNew == ShownLevel)
				return false;

			ShownLevel = liNew;

			return true;
		}

		/// <summary>Starts an empty note with its text at piX/piY (save game coordinates). A hundred
		/// notes fill the page: then nothing happens (UWMapNoteRules.CanAddNote).</summary>
		public bool Begin(int piX, int piY)
		{
			List<UWLevel.MapNote> lONotes = fNotes();

			if (lONotes == null || !UWMapNoteRules.CanAddNote(lONotes.Count))
				return false;

			lONotes.Add(new UWLevel.MapNote { Text = string.Empty, X = piX, Y = piY });

			WritingNote = lONotes.Count - 1;
			IsWriting = true;

			return true;
		}

		/// <summary>Ends the note being written. An empty line is not kept - not even when
		/// confirming; pbKeep false drops it in any case. True when a note was being written.</summary>
		public bool End(bool pbKeep)
		{
			if (!IsWriting)
				return false;

			IsWriting = false;

			List<UWLevel.MapNote> lONotes = fNotes();

			if (lONotes != null && WritingNote >= 0 && WritingNote < lONotes.Count
				&& (!pbKeep || string.IsNullOrEmpty(lONotes[WritingNote].Text)))
			{
				lONotes.RemoveAt(WritingNote);
			}

			WritingNote = -1;

			return true;
		}

		/// <summary>
		/// One typed character onto the note being written. CAPITAL LETTERS: font4x5p knows no lower
		/// case ones, and in the original all notes are in capitals. Typed in lower case, nothing
		/// arrived at all otherwise (per user, 2026-09-03). Codes 0x20 to 0x7A only, and nothing past
		/// the parchment's right edge or 46 characters (UWMapNoteRules.TryAppend, 2026-10-15). True
		/// when the note changed.
		/// </summary>
		public bool Type(char pcTyped)
		{
			if (!IsWriting || !fHasWritingNote(out List<UWLevel.MapNote> lONotes))
				return false;

			string lsText;

			if (!UWMapNoteRules.TryAppend(lONotes[WritingNote].Text, pcTyped, lONotes[WritingNote].X, mFWidth, out lsText))
				return false;

			fSetText(lONotes, lsText);

			return true;
		}

		/// <summary>Takes the last character off the note being written. True when it changed.</summary>
		public bool Backspace()
		{
			if (!IsWriting || !fHasWritingNote(out List<UWLevel.MapNote> lONotes))
				return false;

			string lsText = lONotes[WritingNote].Text ?? string.Empty;

			if (lsText.Length == 0)
				return false;

			fSetText(lONotes, lsText.Substring(0, lsText.Length - 1));

			return true;
		}

		/// <summary>Erases the note under piX/piY (save game coordinates) on the SHOWN level, not
		/// the one the character stands on - of several, the one whose middle is nearest
		/// (UWMapNoteRules.FindNoteToErase, DetermineBetterHit; until 2026-10-15 the first one). True
		/// when a note went.</summary>
		public bool EraseAt(int piX, int piY)
		{
			List<UWLevel.MapNote> lONotes = fNotes();

			if (lONotes == null || mFWidth == null)
				return false;

			int liNote = UWMapNoteRules.FindNoteToErase(lONotes, piX, piY, mFWidth);

			if (liNote < 0)
				return false;

			lONotes.RemoveAt(liNote);

			return true;
		}

		/// <summary>The notes of the shown page as they are drawn (see the static Layout).</summary>
		public List<ShownNote> Layout()
		{
			Layout(fNotes(), mFWidth, mOLayout);

			return mOLayout;
		}

		/// <summary>
		/// The labels on the map, into pOInto (cleared first): every note with text - a still empty
		/// line shows only the quill. Their position is stored as a SCREEN pixel in the save game, not
		/// as a tile - and Y counts from the bottom. Cross-checked against the user's save game:
		/// 104/162 on level 4 stands in the original at top centre (2026-09-03). The label is as wide
		/// as the text's ink, at least one pixel (LabelWidth).
		/// </summary>
		public static void Layout(IList<UWLevel.MapNote> pONotes, Func<string, int> pFWidth, List<ShownNote> pOInto)
		{
			pOInto.Clear();

			if (pONotes == null || pFWidth == null)
				return;

			for (int liNote = 0; liNote < pONotes.Count; liNote++)
			{
				UWLevel.MapNote lONote = pONotes[liNote];

				if (string.IsNullOrEmpty(lONote.Text))
					continue;

				pOInto.Add(new ShownNote
				{
					Index = liNote,
					Text = lONote.Text,
					Left = lONote.X,
					Top = UWClassicLayout.Screen.Height - lONote.Y,
					Width = LabelWidth(pFWidth(lONote.Text))
				});
			}
		}

		/// <summary>A label's width from the text's string_width: the spacing after the last glyph
		/// is not ink; never below one pixel.</summary>
		public static int LabelWidth(int piStringWidth)
		{
			return Math.Max(1, piStringWidth - UWTextLayout.CharacterSpacing);
		}

		/// <summary>
		/// Where the upright quill stands while writing, top-left of the line it sits on (map screen,
		/// y from the top): right behind the text, or at the start of a still empty line. It moves on
		/// with every character (per user on the original, 2026-09-03), so it hangs on the text end and
		/// not on the mouse pointer. False when no note is being written.
		/// </summary>
		public bool TryGetQuill(out int piLeft, out int piTop)
		{
			piLeft = 0;
			piTop = 0;

			if (!IsWriting || !fHasWritingNote(out List<UWLevel.MapNote> lONotes))
				return false;

			UWLevel.MapNote lONote = lONotes[WritingNote];

			piLeft = string.IsNullOrEmpty(lONote.Text) || mFWidth == null ? lONote.X : lONote.X + LabelWidth(mFWidth(lONote.Text));
			piTop = UWClassicLayout.Screen.Height - lONote.Y;

			return true;
		}

		/// <summary>A key of a page's notes: level, place and text of each. The help window's map tab
		/// and the modern minimap render their note pictures again only when it changes - a note
		/// written, erased, or a save game loaded.</summary>
		public static string Key(int piLevel, IList<UWLevel.MapNote> pONotes)
		{
			StringBuilder lOKey = new StringBuilder().Append(piLevel);

			if (pONotes != null)
			{
				foreach (UWLevel.MapNote lONote in pONotes)
					lOKey.Append('|').Append(lONote.X).Append(',').Append(lONote.Y).Append(',').Append(lONote.Text);
			}

			return lOKey.ToString();
		}

		private List<UWLevel.MapNote> fNotes()
		{
			return mFNotes != null ? mFNotes(ShownLevel) : null;
		}

		private bool fHasWritingNote(out List<UWLevel.MapNote> pONotes)
		{
			pONotes = fNotes();

			return pONotes != null && WritingNote >= 0 && WritingNote < pONotes.Count;
		}

		/// <summary>The line holds 46 characters - measured on the original, although the field has
		/// 50 bytes.</summary>
		private void fSetText(List<UWLevel.MapNote> pONotes, string psText)
		{
			if (psText.Length > UWMapNoteRules.MaxLength)
				psText = psText.Substring(0, UWMapNoteRules.MaxLength);

			UWLevel.MapNote lONote = pONotes[WritingNote];

			lONote.Text = psText;

			pONotes[WritingNote] = lONote;
		}

		/// <summary>The click areas' edges count as inside (as the map always tested them).</summary>
		private static bool fIsIn(UWRectInt pORect, float pfX, float pfYTop)
		{
			return pfX >= pORect.X && pfX <= pORect.X + pORect.Width
				&& pfYTop >= pORect.Y && pfYTop <= pORect.Y + pORect.Height;
		}

		/// <summary>Mathf.RoundToInt's rounding (half to even), which the map used.</summary>
		private static int fRound(float pfValue)
		{
			return (int)Math.Round((double)pfValue);
		}
	}
}
