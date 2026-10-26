using System;
using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE TEMPLATE OF THE PORT'S OWN TEXTS (translation wave 6, 2026-10-26): the whole English table
	/// as a language file (id, TAB, text - the format at UWText), the file a translator starts from, and
	/// the same file brought up to date for a translation made against an older release. One code for
	/// the editor's menu (Underworld Revisited / Text / Write the English texts, UWLanguageMenu) and
	/// `uwdump text-template`, so both write the same bytes - until this wave the template came only out
	/// of the Unity editor.
	///
	/// UPDATING (Update): a translation's lines are kept as they stand - the text exactly as written,
	/// escapes and all, never read and written back - in the table's order; the comment and empty lines
	/// above a line stay above it, those before the first line stay at the top, those after the last at
	/// the end. An id the translation lacks is added as a COMMENT holding its English ("# id TAB
	/// English", NewPrefix): the game keeps the English until the translator removes the "# " and
	/// translates - an empty text would show nothing, an empty text being a text. A line whose id the
	/// table no longer knows is OBSOLETE: listed, and kept at the end as a comment ("# obsolete: id TAB
	/// text"), so the text of a renamed id is not lost. A line the game would skip (other placeholders
	/// than the English, a select that does not read) is kept as it is and listed. A translation in the
	/// table's order with every id comes back byte for byte (line ends as "\n").
	/// </summary>
	public static class UWTextTemplate
	{
		/// <summary>The first line of a template - the editor menu's since cleanup step 8.5.</summary>
		public const string Header = "# Underworld Revisited, the port's own texts: id TAB text (\\n a line break)\n";

		/// <summary>What a missing id's comment line starts with.</summary>
		public const string NewPrefix = "# ";

		/// <summary>What an obsolete line's comment starts with.</summary>
		public const string ObsoletePrefix = "# obsolete: ";

		/// <summary>The English table as a language file: the header line, then every id with its English
		/// in the table's order.</summary>
		public static string English()
		{
			return Header + UWTextLanguageFile.Write(UWText.TextIds, UWText.English);
		}

		/// <summary>What Update did.</summary>
		public sealed class Result
		{
			/// <summary>The updated file.</summary>
			public string Text;

			/// <summary>Lines of the translation kept.</summary>
			public int Kept;

			/// <summary>Ids added as comments with their English.</summary>
			public readonly List<string> Added = new List<string>();

			/// <summary>Ids the table no longer knows (kept at the end as comments).</summary>
			public readonly List<string> Obsolete = new List<string>();

			/// <summary>Lines kept although the game skips them, ids given twice (the later line kept, as
			/// the game counts it), and lines without an id that were dropped.</summary>
			public readonly List<string> Problems = new List<string>();
		}

		/// <summary>One line of a translation, raw: its id, the text exactly as written, and the comment
		/// and empty lines that stood above it.</summary>
		private sealed class Line
		{
			public string Id;

			public string RawText;

			public readonly List<string> Above = new List<string>();
		}

		/// <summary>A translation brought up to date with the table (see the class): psExisting is the
		/// content of the translator's &lt;code&gt;.tsv. A byte order mark goes, line ends become "\n";
		/// everything else of a kept line is as it was.</summary>
		public static Result Update(string psExisting)
		{
			Result lOResult = new Result();
			Dictionary<string, Line> lOLines = new Dictionary<string, Line>(StringComparer.Ordinal);
			List<Line> lOOrder = new List<Line>();
			List<string> lsTop = null;
			List<string> lsPending = new List<string>();
			string lsContent = (psExisting ?? string.Empty).TrimStart('﻿').Replace("\r\n", "\n");

			if (lsContent.EndsWith("\n", StringComparison.Ordinal))
				lsContent = lsContent.Substring(0, lsContent.Length - 1);

			string[] lsRows = lsContent.Length == 0 ? new string[0] : lsContent.Split('\n');

			for (int liRow = 0; liRow < lsRows.Length; liRow++)
			{
				string lsRow = lsRows[liRow].TrimEnd('\r');

				// Comments and empty lines go with the line below them.
				if (lsRow.Length == 0 || lsRow[0] == '#')
				{
					lsPending.Add(lsRow);

					continue;
				}

				int liTab = lsRow.IndexOf('\t');

				if (liTab <= 0)
				{
					lOResult.Problems.Add("line " + (liRow + 1) + ": no id and TAB - dropped");

					continue;
				}

				// Before the first line: the file's top.
				if (lsTop == null)
				{
					lsTop = new List<string>(lsPending);
					lsPending.Clear();
				}

				Line lOLine = new Line { Id = lsRow.Substring(0, liTab).Trim(), RawText = lsRow.Substring(liTab + 1) };

				lOLine.Above.AddRange(lsPending);
				lsPending.Clear();

				if (lOLines.TryGetValue(lOLine.Id, out Line lOEarlier))
				{
					lOResult.Problems.Add("line " + (liRow + 1) + ": " + lOLine.Id + " again, the later text kept");
					lOOrder.Remove(lOEarlier);
					lOLine.Above.InsertRange(0, lOEarlier.Above);
				}

				lOLines[lOLine.Id] = lOLine;
				lOOrder.Add(lOLine);
			}

			if (lsTop == null)
			{
				lsTop = new List<string>(lsPending);
				lsPending.Clear();
			}

			// What the game would make of the lines: the ones it skips are named, and kept all the same.
			foreach (string lsProblem in UWTextLanguageFile.Parse(psExisting).Problems)
			{
				if (lsProblem.IndexOf(": unknown id ", StringComparison.Ordinal) < 0 && lsProblem.IndexOf(" again, ", StringComparison.Ordinal) < 0
					&& lsProblem.IndexOf(": no id and TAB", StringComparison.Ordinal) < 0)
					lOResult.Problems.Add(lsProblem + " - kept as it is; the game shows the English");
			}

			StringBuilder lOText = new StringBuilder();

			fAppend(lOText, lsTop);

			foreach (string lsId in UWText.TextIds)
			{
				if (lOLines.TryGetValue(lsId, out Line lOLine))
				{
					fAppend(lOText, lOLine.Above);
					lOText.Append(lOLine.Id).Append('\t').Append(lOLine.RawText).Append('\n');
					lOResult.Kept++;

					continue;
				}

				lOText.Append(NewPrefix).Append(lsId).Append('\t').Append(UWTextLanguageFile.Escape(UWText.English(lsId))).Append('\n');
				lOResult.Added.Add(lsId);
			}

			foreach (Line lOLine in lOOrder)
			{
				if (UWText.HasText(lOLine.Id))
					continue;

				fAppend(lOText, lOLine.Above);
				lOText.Append(ObsoletePrefix).Append(lOLine.Id).Append('\t').Append(lOLine.RawText).Append('\n');
				lOResult.Obsolete.Add(lOLine.Id);
			}

			fAppend(lOText, lsPending);
			lOResult.Text = lOText.ToString();

			return lOResult;
		}

		private static void fAppend(StringBuilder pOText, List<string> plsLines)
		{
			foreach (string lsLine in plsLines)
				pOText.Append(lsLine).Append('\n');
		}
	}
}
