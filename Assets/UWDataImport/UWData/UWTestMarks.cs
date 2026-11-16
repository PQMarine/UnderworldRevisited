using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// TEST MARKS (per user, 2026-11-15): what a prepared test is about, as a file the developer puts beside
	/// settings.json - test-focus.json. The traps of the second game run many chains at once (timers, oscillators,
	/// the castle's edges), and a test written as "level 1, 29/46" left the tester to find the square and to pick the
	/// one line of the trap log that mattered among the others. A mark names a square with a number, a short label and
	/// the expected result; the game draws the numbers over the squares (UWTestMarkView), lists the marks with where
	/// they lie from the player in the debug report's Test section, and tags every trap log line whose chain starts or
	/// aims at a marked square with its number (UWTrapLog), keeping those lines in a log of their own so the busy
	/// chains do not push them out.
	///
	/// A DEVELOPER TOOL: without the file nothing changes; the file is written for a test and deleted after it. The
	/// format:
	///
	///   { "test": "44.8", "title": "Creatures on enter triggers",
	///     "marks": [ { "game": "uw2", "level": 1, "x": 29, "y": 46,
	///                  "label": "edge square, creatures only", "expect": "Hack 62 sends the creature home" } ] }
	///
	/// The marks are numbered in their order from 1. "game" (uw1 or uw2) may be left out for both games; "level" is
	/// 1-based as the game shows it.
	/// </summary>
	public sealed class UWTestMarks
	{
		public const string FileName = "test-focus.json";

		public sealed class Mark
		{
			/// <summary>1-based, the mark's place in the file.</summary>
			public int Number;

			/// <summary>"uw1", "uw2" or null for both.</summary>
			public string Game;

			/// <summary>1-based.</summary>
			public int Level;

			public int X;

			public int Y;

			public string Label = string.Empty;

			public string Expect = string.Empty;

			/// <summary>The mark belongs to this game and level.</summary>
			public bool IsOn(bool pbUw2, int piLevel)
			{
				return piLevel == Level && (Game == null || Game == (pbUw2 ? "uw2" : "uw1"));
			}
		}

		public string Test = string.Empty;

		public string Title = string.Empty;

		public readonly List<Mark> Marks = new List<Mark>();

		/// <summary>The marks in force, null without a file. Set by the host when it reads the file.</summary>
		public static UWTestMarks Current;

		/// <summary>The marks of a test-focus.json text, or null with the reason.</summary>
		public static UWTestMarks Parse(string psText, out string psError)
		{
			Dictionary<string, object> lORoot = UWJsonReader.TryParseObject(psText, out psError);

			if (lORoot == null)
				return null;

			UWTestMarks lOMarks = new UWTestMarks
			{
				Test = UWJsonReader.GetString(lORoot, "test") ?? string.Empty,
				Title = UWJsonReader.GetString(lORoot, "title") ?? string.Empty
			};

			List<object> lOList = UWJsonReader.GetList(lORoot, "marks");

			if (lOList == null)
			{
				psError = "no \"marks\" list";
				return null;
			}

			foreach (object lOItem in lOList)
			{
				Dictionary<string, object> lOMark = lOItem as Dictionary<string, object>;

				if (lOMark == null)
				{
					psError = "a mark is not an object";
					return null;
				}

				string lsGame = UWJsonReader.GetString(lOMark, "game");

				lOMarks.Marks.Add(new Mark
				{
					Number = lOMarks.Marks.Count + 1,
					Game = string.IsNullOrEmpty(lsGame) ? null : lsGame.ToLowerInvariant(),
					Level = (int)UWJsonReader.GetNumber(lOMark, "level", 0),
					X = (int)UWJsonReader.GetNumber(lOMark, "x", -1),
					Y = (int)UWJsonReader.GetNumber(lOMark, "y", -1),
					Label = UWJsonReader.GetString(lOMark, "label") ?? string.Empty,
					Expect = UWJsonReader.GetString(lOMark, "expect") ?? string.Empty
				});
			}

			psError = null;

			return lOMarks;
		}

		/// <summary>The numbers of the marks on that square of that game's level, none when there are none.</summary>
		public void Collect(bool pbUw2, int piLevel, int piTileX, int piTileY, List<int> pONumbers)
		{
			foreach (Mark lOMark in Marks)
			{
				if (lOMark.IsOn(pbUw2, piLevel) && lOMark.X == piTileX && lOMark.Y == piTileY && !pONumbers.Contains(lOMark.Number))
					pONumbers.Add(lOMark.Number);
			}
		}

		/// <summary>
		/// Where a square lies from the player in words, for the tester who does not think in tile numbers: "here", or the
		/// squares north or south and east or west ("3 south, 1 east"), and the floor's step up or down when it differs.
		/// North is +y.
		/// </summary>
		public static string Direction(int piFromX, int piFromY, int piToX, int piToY, int piHeightSteps)
		{
			List<string> lsParts = new List<string>();
			int liDy = piToY - piFromY;
			int liDx = piToX - piFromX;

			if (liDy != 0)
				lsParts.Add(Math.Abs(liDy) + (liDy > 0 ? " north" : " south"));

			if (liDx != 0)
				lsParts.Add(Math.Abs(liDx) + (liDx > 0 ? " east" : " west"));

			string lsWhere = lsParts.Count == 0 ? "here" : string.Join(", ", lsParts.ToArray());

			if (piHeightSteps != 0)
				lsWhere += piHeightSteps > 0 ? ", " + piHeightSteps + " up" : ", " + (-piHeightSteps) + " down";

			return lsWhere;
		}
	}
}
