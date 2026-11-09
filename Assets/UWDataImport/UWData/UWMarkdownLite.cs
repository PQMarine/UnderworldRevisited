using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A MOD'S README.md IN THE MODS WINDOW (per user, 2026-11-09): the little of Markdown a readme uses, turned into
	/// the rich text Unity's interface text knows (b, i, color) - nothing more is drawn:
	///
	///   # Heading           bold in the accent colour (every level alike)
	///   **bold** *italic*   b and i (underscores are left alone: names_with_underscores are common in mods)
	///   - item, 1. item     a bullet or the number, indented by the list's depth
	///   > quote             indented, in italics
	///   [text](address)     the text and the address in brackets; an image's alt text alone - ON PURPOSE NEVER A
	///                       LINK TO CLICK (per user, 2026-11-09): the game parses no address out of a mod to show a
	///                       button for it or to open it, so a mod never leads from the game to the system's
	///                       handlers (Windows opens any registered scheme, not only web pages)
	///   `code`, ```code```  as written, without the marks
	///   ---                 an empty line
	///
	/// The lines of a paragraph are joined with a space, as Markdown joins them (a readme wrapped at 80 columns
	/// reads as one paragraph in a narrow window); two spaces at a line's end keep its break. A tag of Unity's rich
	/// text written in the readme itself is broken up, so it shows as written.
	/// </summary>
	public static class UWMarkdownLite
	{
		private static readonly Regex msHeading = new Regex(@"^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$");

		private static readonly Regex msBullet = new Regex(@"^(\s*)[-*+]\s+(.*)$");

		private static readonly Regex msNumbered = new Regex(@"^(\s*)(\d+)[.)]\s+(.*)$");

		private static readonly Regex msQuote = new Regex(@"^\s{0,3}>\s?(.*)$");

		private static readonly Regex msRule = new Regex(@"^\s{0,3}([-*_])(\s*\1){2,}\s*$");

		private static readonly Regex msFence = new Regex(@"^\s{0,3}(```|~~~)");

		private static readonly Regex msImage = new Regex(@"!\[([^\]]*)\]\([^)]*\)");

		private static readonly Regex msLink = new Regex(@"\[([^\]]+)\]\(([^)\s]+)(\s+""[^""]*"")?\)");

		private static readonly Regex msBold = new Regex(@"\*\*(?=\S)(.+?)(?<=\S)\*\*");

		private static readonly Regex msItalic = new Regex(@"(?<![*\w])\*(?=[^\s*])(.+?)(?<=[^\s*])\*(?![*\w])");

		private static readonly Regex msCode = new Regex(@"`([^`]+)`");

		private static readonly Regex msUnityTag = new Regex(@"<(/?)(b|i|size|color|material|quad)\b", RegexOptions.IgnoreCase);

		/// <summary>The readme as rich text; psAccentHex the headings' colour ("RRGGBB").</summary>
		public static string ToRichText(string psMarkdown, string psAccentHex)
		{
			if (string.IsNullOrEmpty(psMarkdown))
				return psMarkdown;

			string[] lsLines = psMarkdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			List<string> lsOut = new List<string>();
			StringBuilder lOParagraph = new StringBuilder();
			bool lbCode = false;

			void fFlush()
			{
				if (lOParagraph.Length > 0)
				{
					lsOut.Add(lOParagraph.ToString());
					lOParagraph.Clear();
				}
			}

			foreach (string lsRaw in lsLines)
			{
				if (msFence.IsMatch(lsRaw))
				{
					fFlush();
					lbCode = !lbCode;
					continue;
				}

				if (lbCode)
				{
					lsOut.Add("    " + fEscape(lsRaw));
					continue;
				}

				if (lsRaw.Trim().Length == 0)
				{
					fFlush();

					if (lsOut.Count > 0 && lsOut[lsOut.Count - 1].Length > 0)
						lsOut.Add(string.Empty);

					continue;
				}

				Match lOMatch;

				if ((lOMatch = msHeading.Match(lsRaw)).Success)
				{
					fFlush();
					lsOut.Add("<b><color=#" + psAccentHex + ">" + fInline(lOMatch.Groups[2].Value) + "</color></b>");
					continue;
				}

				if (msRule.IsMatch(lsRaw))
				{
					fFlush();
					lsOut.Add(string.Empty);
					continue;
				}

				if ((lOMatch = msBullet.Match(lsRaw)).Success)
				{
					fFlush();
					lsOut.Add(fIndent(lOMatch.Groups[1].Value) + "• " + fInline(lOMatch.Groups[2].Value));
					continue;
				}

				if ((lOMatch = msNumbered.Match(lsRaw)).Success)
				{
					fFlush();
					lsOut.Add(fIndent(lOMatch.Groups[1].Value) + lOMatch.Groups[2].Value + ". " + fInline(lOMatch.Groups[3].Value));
					continue;
				}

				if ((lOMatch = msQuote.Match(lsRaw)).Success)
				{
					fFlush();
					lsOut.Add("    <i>" + fInline(lOMatch.Groups[1].Value) + "</i>");
					continue;
				}

				// A paragraph's line: joined to the one before, unless that one ended with two spaces.
				bool lbBreak = lsRaw.EndsWith("  ");
				string lsLine = fInline(lsRaw.Trim());

				if (lOParagraph.Length > 0)
					lOParagraph.Append(' ');

				lOParagraph.Append(lsLine);

				if (lbBreak)
					fFlush();
			}

			fFlush();

			while (lsOut.Count > 0 && lsOut[lsOut.Count - 1].Length == 0)
				lsOut.RemoveAt(lsOut.Count - 1);

			return string.Join("\n", lsOut);
		}

		/// <summary>A list item's indent: four spaces for every two of the source's.</summary>
		private static string fIndent(string psLeading)
		{
			return new string(' ', (psLeading.Replace("\t", "  ").Length / 2) * 4);
		}

		private static string fInline(string psText)
		{
			string lsText = fEscape(psText);

			lsText = msCode.Replace(lsText, "$1");
			lsText = msImage.Replace(lsText, "$1");
			lsText = msLink.Replace(lsText, pOMatch => pOMatch.Groups[1].Value == pOMatch.Groups[2].Value
				? pOMatch.Groups[1].Value : pOMatch.Groups[1].Value + " (" + pOMatch.Groups[2].Value + ")");
			lsText = msBold.Replace(lsText, "<b>$1</b>");
			lsText = msItalic.Replace(lsText, "<i>$1</i>");

			return lsText.Replace("\\*", "*").Replace("\\#", "#").Replace("\\`", "`");
		}

		/// <summary>A tag of Unity's rich text in the readme itself broken up ("< b"), so it shows as written.</summary>
		private static string fEscape(string psText)
		{
			return msUnityTag.Replace(psText, "< $1$2");
		}
	}
}
