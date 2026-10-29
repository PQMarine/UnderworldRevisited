namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE THE SECOND GAME'S CREATION PUTS ITS ANSWERS (UW2 plan phase 3 step 5, 2026-10-29), as
	/// Underworld Exhumed's uw2 game/CHARGEN.C works it out: drawopt lays a question's answers out in as many
	/// columns as their height needs within 0xC4 rows (less 20 for a question), spaces the columns evenly
	/// over the right page's 0xA0 columns and centres the rows, the question one row above them; selopt
	/// finds an answer's place again, mousopt the answer under the pointer. Every y is counted up from the
	/// screen's bottom and names a top edge (UWClassicLayoutUw2.Chargen). Engine-free; the screen is
	/// UWCharacterCreationScreenUw2.
	/// </summary>
	public static class UWChargenLayoutUw2
	{
		/// <summary>A question's answers laid out: columns and rows, the space between the columns, the
		/// picture's size, the first row's top and the question's (one row higher), whether there is one.</summary>
		public struct Arrangement
		{
			public int Count;

			public int Columns;

			public int Rows;

			public int Spacing;

			public int Width;

			public int Height;

			public bool HasQuestion;

			public int QuestionY;

			public int FirstRowY;
		}

		/// <summary>drawopt: the columns, rows and spacing for piCount answers of a piWidth x piHeight
		/// picture, and where the question and the first row stand. C's division truncates towards zero:
		/// no answers (the name) still make one column and no row.</summary>
		public static Arrangement Arrange(int piCount, int piWidth, int piHeight, bool pbQuestion)
		{
			int liStep = piHeight + UWClassicLayoutUw2.Chargen.AnswerGap;
			int liRoom = UWClassicLayoutUw2.Chargen.AnswerArea - (pbQuestion ? UWClassicLayoutUw2.Chargen.QuestionRoom : 0);
			int liColumns = (((piCount * liStep) - UWClassicLayoutUw2.Chargen.AnswerGap) / liRoom) + 1;
			int liRows = (piCount + liColumns - 1) / liColumns;
			int liHasQuestion = pbQuestion ? 1 : 0;
			int liY = UWClassicLayoutUw2.Chargen.AnswerTop
				- ((UWClassicLayoutUw2.Chargen.AnswerArea - ((liRows + liHasQuestion) * liStep) + UWClassicLayoutUw2.Chargen.AnswerGap) / 2);

			return new Arrangement
			{
				Count = piCount,
				Columns = liColumns,
				Rows = liRows,
				Spacing = (UWClassicLayoutUw2.Chargen.RightPage - (liColumns * piWidth)) / (liColumns + 1),
				Width = piWidth,
				Height = piHeight,
				HasQuestion = pbQuestion,
				QuestionY = liY,
				FirstRowY = pbQuestion ? liY - liStep : liY
			};
		}

		/// <summary>Answer piIndex's left edge and top (drawopt's walk, selopt's place): row by row,
		/// piColumns to a row, each column a picture and a space further.</summary>
		public static void Place(Arrangement pOArrangement, int piIndex, out int piX, out int piY)
		{
			piX = UWClassicLayoutUw2.Chargen.RightPage + pOArrangement.Spacing
				+ ((piIndex % pOArrangement.Columns) * (pOArrangement.Width + pOArrangement.Spacing));
			piY = pOArrangement.FirstRowY - ((piIndex / pOArrangement.Columns) * (pOArrangement.Height + UWClassicLayoutUw2.Chargen.AnswerGap));
		}

		/// <summary>mousopt: the answer whose picture holds the point (x, y counted up), or -1 - left of the
		/// first column, above the first row, past the last answer or in the space between.</summary>
		public static int AnswerAt(Arrangement pOArrangement, int piX, int piY)
		{
			int liStepX = pOArrangement.Width + pOArrangement.Spacing;
			int liStepY = pOArrangement.Height + UWClassicLayoutUw2.Chargen.AnswerGap;
			int liLeft = UWClassicLayoutUw2.Chargen.RightPage + pOArrangement.Spacing;
			int liTop = pOArrangement.FirstRowY;

			if (piY > liTop || piX < liLeft || liStepX <= 0)
				return -1;

			int liAnswer = ((liTop - piY) / liStepY * pOArrangement.Columns) + ((piX - liLeft) / liStepX);

			if (liAnswer < 0 || liAnswer >= pOArrangement.Count || (piX - liLeft) / liStepX >= pOArrangement.Columns)
				return -1;

			int liInX = piX - liLeft - (liStepX * (liAnswer % pOArrangement.Columns));
			int liInY = liTop - piY - (liStepY * (liAnswer / pOArrangement.Columns));

			return liInX >= pOArrangement.Width || liInY >= pOArrangement.Height ? -1 : liAnswer;
		}

		/// <summary>pickopt's keys: piStep moves the light by piStep (a row is the columns), kept inside the
		/// answers.</summary>
		public static int Move(Arrangement pOArrangement, int piSelected, int piStep)
		{
			int liSelected = piSelected + piStep;

			if (liSelected < 0)
				return 0;

			return liSelected >= pOArrangement.Count ? pOArrangement.Count - 1 : liSelected;
		}
	}
}
