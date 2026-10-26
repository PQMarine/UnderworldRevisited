using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE GAMEPAD LETTER GRID'S KEYS AND ITS MOVES (translation 5.1, 2026-10-26; engine-free, so the
	/// self-check proves the layout per letter set - UWLetterGrid draws and drives it). Rows of ten:
	///
	///   the language's ALPHABET (UWTextLine.Alphabet, A to Z in English) followed by the signs ' - . ,
	///   - English: ABCDEFGHIJ / KLMNOPQRST / UVWXYZ'-., as it always was; a 33-letter alphabet four rows;
	///   the language's OTHER LETTERS (UWTextLine.LanguageLetters less the alphabet's, one key per letter
	///   whatever its case - German: ä ö ü ß), in rows of ten from the left, the cells after the last one
	///   empty and skipped; none in English;
	///   the DIGITS 1 to 0;
	///   the BOTTOM ROW: case, space, delete, cancel, done, each key two columns wide.
	///
	/// A key shows and types its upper case while the grid is in upper case, its lower case otherwise; a
	/// letter without a distinct capital (the sharp s) stays itself, as does a case form the language
	/// does not take. THE MOVES: sideways wraps within the row's keys (two columns at a time on the
	/// bottom row); up and down wrap over the rows and keep the column, a short row taking its last key -
	/// the column is remembered through a short row, so passing it goes on in the column it came from.
	/// With full rows only (English) that is exactly the grid's old movement.
	/// </summary>
	public sealed class UWLetterGridLayout
	{
		public const int Columns = 10;

		/// <summary>The signs after the alphabet.</summary>
		public const string Signs = "'-.,";

		public const string Digits = "1234567890";

		/// <summary>The bottom row's keys (case, space, delete, cancel, done), each BottomSpan columns wide.</summary>
		public const int BottomKeys = 5;

		public const int BottomSpan = 2;

		/// <summary>One key: what it shows and types in upper and in lower case (the same for a sign).</summary>
		public struct Key
		{
			public char Upper;

			public char Lower;

			public char Shown(bool pbUpper)
			{
				return pbUpper ? Upper : Lower;
			}
		}

		private readonly List<Key[]> mOKeyRows = new List<Key[]>();

		private readonly string msAlphabet;

		private readonly string msAlphabetLower;

		private readonly string msLetters;

		/// <summary>The rows above the bottom row: the alphabet's with the signs, the other letters', the digits.</summary>
		public int KeyRows => mOKeyRows.Count;

		/// <summary>The bottom row's index - the last row.</summary>
		public int BottomRow => mOKeyRows.Count;

		/// <summary>Every row, the bottom row with it.</summary>
		public int Rows => mOKeyRows.Count + 1;

		/// <summary>How many rows the language's other letters take (0 in English).</summary>
		public int LetterRows { get; private set; }

		private UWLetterGridLayout(string psAlphabet, string psAlphabetLower, string psLetters)
		{
			msAlphabet = psAlphabet ?? string.Empty;
			msAlphabetLower = psAlphabetLower ?? string.Empty;
			msLetters = psLetters ?? string.Empty;

			List<Key> lOKeys = new List<Key>();

			for (int liAt = 0; liAt < msAlphabet.Length; liAt++)
			{
				char lcUpper = msAlphabet[liAt];

				lOKeys.Add(new Key { Upper = lcUpper, Lower = liAt < msAlphabetLower.Length ? msAlphabetLower[liAt] : char.ToLowerInvariant(lcUpper) });
			}

			foreach (char lcSign in Signs)
				lOKeys.Add(new Key { Upper = lcSign, Lower = lcSign });

			fAddRows(lOKeys);

			// The other letters: one key per letter in both its cases, those of the alphabet left out.
			List<Key> lOLetters = new List<Key>();
			StringBuilder lOSeen = new StringBuilder(msAlphabet + msAlphabetLower);

			foreach (char lcChar in msLetters)
			{
				char lcLower = char.ToLowerInvariant(lcChar);
				char lcUpper = char.ToUpperInvariant(lcChar);

				if (lcLower != lcChar && msLetters.IndexOf(lcLower) < 0)
					lcLower = lcChar;

				if (lcUpper != lcChar && msLetters.IndexOf(lcUpper) < 0)
					lcUpper = lcChar;

				string lsSeen = lOSeen.ToString();

				if (lsSeen.IndexOf(lcLower) >= 0 || lsSeen.IndexOf(lcUpper) >= 0)
					continue;

				lOLetters.Add(new Key { Upper = lcUpper, Lower = lcLower });
				lOSeen.Append(lcUpper).Append(lcLower);
			}

			int liBefore = mOKeyRows.Count;

			fAddRows(lOLetters);
			LetterRows = mOKeyRows.Count - liBefore;

			List<Key> lODigits = new List<Key>();

			foreach (char lcDigit in Digits)
				lODigits.Add(new Key { Upper = lcDigit, Lower = lcDigit });

			fAddRows(lODigits);
		}

		private void fAddRows(List<Key> pOKeys)
		{
			for (int liAt = 0; liAt < pOKeys.Count; liAt += Columns)
				mOKeyRows.Add(pOKeys.GetRange(liAt, System.Math.Min(Columns, pOKeys.Count - liAt)).ToArray());
		}

		/// <summary>The layout of an alphabet (upper case, and its lower case letter for letter) and the
		/// language's letters beyond ASCII (UWTextLine.LanguageLetters).</summary>
		public static UWLetterGridLayout Build(string psAlphabet, string psAlphabetLower, string psLetters)
		{
			return new UWLetterGridLayout(psAlphabet, psAlphabetLower, psLetters);
		}

		private static UWLetterGridLayout msCurrent;

		/// <summary>The layout of the language in force (UWTextLine.Alphabet, AlphabetLower, LanguageLetters),
		/// built anew when they changed - so a switch of language reaches an open grid at once.</summary>
		public static UWLetterGridLayout Current
		{
			get
			{
				UWLetterGridLayout lOCurrent = msCurrent;

				if (lOCurrent == null || lOCurrent.msAlphabet != UWTextLine.Alphabet || lOCurrent.msAlphabetLower != UWTextLine.AlphabetLower
					|| lOCurrent.msLetters != UWTextLine.LanguageLetters)
				{
					lOCurrent = Build(UWTextLine.Alphabet, UWTextLine.AlphabetLower, UWTextLine.LanguageLetters);
					msCurrent = lOCurrent;
				}

				return lOCurrent;
			}
		}

		/// <summary>The keys of a row (BottomKeys on the bottom row).</summary>
		public int KeysIn(int piRow)
		{
			return piRow == BottomRow ? BottomKeys : piRow >= 0 && piRow < mOKeyRows.Count ? mOKeyRows[piRow].Length : 0;
		}

		/// <summary>How many columns a key of the row is wide.</summary>
		public int SpanOf(int piRow)
		{
			return piRow == BottomRow ? BottomSpan : 1;
		}

		/// <summary>A key of a row above the bottom row.</summary>
		public Key KeyAt(int piRow, int piKey)
		{
			return mOKeyRows[piRow][piKey];
		}

		/// <summary>The key a column of a row falls on.</summary>
		public int KeyOfColumn(int piRow, int piColumn)
		{
			return piColumn / SpanOf(piRow);
		}

		/// <summary>The column the cursor takes in a row when it wants piColumn: the key under it, or the
		/// row's last key in a short row.</summary>
		public int ClampColumn(int piRow, int piColumn)
		{
			int liSpan = SpanOf(piRow);
			int liKey = System.Math.Max(0, System.Math.Min(piColumn / liSpan, KeysIn(piRow) - 1));

			return liKey * liSpan;
		}

		/// <summary>
		/// One step of the cursor (row, column on the ten-column grid, and the column it wants - kept through a
		/// short row): piDx sideways within the row's keys, piDy over the rows, both wrapping.
		/// </summary>
		public void Move(ref int piRow, ref int piColumn, ref int piWanted, int piDx, int piDy)
		{
			if (piDx != 0)
			{
				int liKeys = KeysIn(piRow);
				int liSpan = SpanOf(piRow);
				int liKey = ((KeyOfColumn(piRow, piColumn) + piDx) % liKeys + liKeys) % liKeys;

				piColumn = liKey * liSpan;
				piWanted = piColumn;
			}

			if (piDy != 0)
			{
				piRow = ((piRow + piDy) % Rows + Rows) % Rows;
				piColumn = ClampColumn(piRow, piWanted);

				// A full row takes the column as its own (the bottom row its even one, as always); a short
				// row keeps the wanted column for the next row.
				if (KeysIn(piRow) * SpanOf(piRow) >= Columns)
					piWanted = piColumn;
			}
		}

		/// <summary>The rows as text for the checks: each row's keys in the case asked, "/" between rows,
		/// the bottom row as "[5x2]".</summary>
		public string Describe(bool pbUpper)
		{
			StringBuilder lOText = new StringBuilder();

			foreach (Key[] lyRow in mOKeyRows)
			{
				foreach (Key lOKey in lyRow)
					lOText.Append(lOKey.Shown(pbUpper));

				lOText.Append('/');
			}

			return lOText.Append('[').Append(BottomKeys).Append('x').Append(BottomSpan).Append(']').ToString();
		}
	}
}
