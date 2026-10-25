using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A MASK WRITTEN AS TEXT (cleanup 9.9, 2026-10-25, CLEANUP-PLAN 8.4): rows of characters, top
	/// down, one per pixel, each from a LEGEND (for example ".X" or ".XF") - the gargoyle's head, the
	/// gem's stand and the compass disc were painted or worked out by hand this way (only the masks
	/// are in the project, not the pictures). The HUD art indexed the string arrays directly, without
	/// a check that they were as large as the crop they lie on; a mask is now checked once, when it
	/// is made: every row as long, every character in the legend, and the size of its place.
	/// </summary>
	public sealed class UWMaskText
	{
		public readonly int Width;

		public readonly int Height;

		/// <summary>Where the mask lies in its source picture (rows from the top).</summary>
		public readonly UWRectInt Place;

		private readonly string[] msRows;

		/// <summary>The mask over psRows at pOPlace; throws when a row's length, a character or the
		/// size does not fit.</summary>
		public UWMaskText(string[] psRows, string psLegend, UWRectInt pOPlace)
		{
			if (psRows == null || psRows.Length == 0)
				throw new ArgumentException("a text mask needs rows");

			for (int y = 0; y < psRows.Length; y++)
			{
				if (psRows[y].Length != psRows[0].Length)
					throw new ArgumentException(string.Format("text mask row {0} is {1} long, not {2}", y, psRows[y].Length, psRows[0].Length));

				foreach (char lcAt in psRows[y])
				{
					if (psLegend.IndexOf(lcAt) < 0)
						throw new ArgumentException(string.Format("text mask row {0} has '{1}', not in \"{2}\"", y, lcAt, psLegend));
				}
			}

			if (pOPlace.Width != psRows[0].Length || pOPlace.Height != psRows.Length)
				throw new ArgumentException(string.Format("text mask is {0} x {1}, its place {2} x {3}", psRows[0].Length, psRows.Length,
					pOPlace.Width, pOPlace.Height));

			Width = psRows[0].Length;
			Height = psRows.Length;
			Place = pOPlace;
			msRows = psRows;
		}

		/// <summary>The character at a pixel.</summary>
		public char At(int piX, int piY)
		{
			return msRows[piY][piX];
		}

		/// <summary>Whether a pixel has the given character.</summary>
		public bool Is(int piX, int piY, char pcWhat)
		{
			return msRows[piY][piX] == pcWhat;
		}

		/// <summary>The pixels with the given character, as a mask.</summary>
		public UWMask Where(char pcWhat)
		{
			UWMask lOMask = new UWMask(Width, Height);

			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
					lOMask[x, y] = msRows[y][x] == pcWhat;
			}

			return lOMask;
		}
	}
}
