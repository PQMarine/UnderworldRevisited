using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE CHECK FOR A NEWER RELEASE (GitHub issue 36, 2026-11-11, per user: opt-in, the option only, in the Game
	/// window): what the game asks GitHub, how it reads the answer, how it compares versions, when the next check is
	/// due and what it keeps between starts. Engine-free; UWUpdateChecker sends the request and the main menu shows
	/// the result. Nothing but the request itself goes out: no id, no settings, no statistics.
	/// </summary>
	public static class UWUpdateCheck
	{
		/// <summary>The newest published release (drafts and pre-releases are never "latest").</summary>
		public const string LatestReleaseUrl = "https://api.github.com/repos/PQMarine/UnderworldRevisited/releases/latest";

		/// <summary>The only pages the game opens from an answer: the project's own releases.</summary>
		public const string ReleasePagePrefix = "https://github.com/PQMarine/UnderworldRevisited/releases/";

		/// <summary>The hours between two checks: at most one a day.</summary>
		public const double HoursBetweenChecks = 24.0;

		/// <summary>The file beside settings.json that keeps the last check and its answer.</summary>
		public const string StateFileName = "update-check.txt";

		/// <summary>What the last check found and when.</summary>
		public sealed class State
		{
			public DateTime CheckedUtc = DateTime.MinValue;

			/// <summary>The latest release's tag (v0.20.0), empty when none is known.</summary>
			public string Tag = string.Empty;

			/// <summary>Its title as GitHub lists it (0.20.0 - Theme).</summary>
			public string Title = string.Empty;

			/// <summary>Its page, only ever under ReleasePagePrefix.</summary>
			public string Url = string.Empty;
		}

		/// <summary>The answer of LatestReleaseUrl read: tag_name, name and html_url. False when it is not JSON, has no
		/// tag, or its page is not one of the project's releases.</summary>
		public static bool TryParseLatest(string psJson, out string psTag, out string psTitle, out string psUrl)
		{
			psTag = psTitle = psUrl = string.Empty;

			Dictionary<string, object> lOObject = UWJsonReader.TryParseObject(psJson ?? string.Empty, out string _);

			if (lOObject == null)
				return false;

			psTag = lOObject.TryGetValue("tag_name", out object lOTag) ? lOTag as string ?? string.Empty : string.Empty;
			psTitle = lOObject.TryGetValue("name", out object lOName) ? lOName as string ?? string.Empty : string.Empty;
			psUrl = lOObject.TryGetValue("html_url", out object lOUrl) ? lOUrl as string ?? string.Empty : string.Empty;

			return ParseVersion(psTag) != null && IsReleasePage(psUrl);
		}

		/// <summary>Whether a page may be opened: one of the project's release pages, nothing else.</summary>
		public static bool IsReleasePage(string psUrl)
		{
			return !string.IsNullOrEmpty(psUrl) && psUrl.StartsWith(ReleasePagePrefix, StringComparison.Ordinal)
				&& psUrl.IndexOfAny(new[] { ' ', '"', '\'', '<', '>', '\\', '\n', '\r', '\t' }) < 0;
		}

		/// <summary>"v0.19.2" or "0.19.2" as its numbers; null when it is not one to three numbers with dots.</summary>
		public static int[] ParseVersion(string psVersion)
		{
			if (string.IsNullOrEmpty(psVersion))
				return null;

			string lsText = psVersion.Trim();

			if (lsText.StartsWith("v", StringComparison.OrdinalIgnoreCase))
				lsText = lsText.Substring(1);

			string[] lsParts = lsText.Split('.');

			if (lsParts.Length < 1 || lsParts.Length > 3)
				return null;

			int[] liNumbers = new int[3];

			for (int liAt = 0; liAt < lsParts.Length; liAt++)
			{
				if (!int.TryParse(lsParts[liAt], NumberStyles.None, CultureInfo.InvariantCulture, out liNumbers[liAt]))
					return null;
			}

			return liNumbers;
		}

		/// <summary>Whether psLatest is newer than psCurrent; false when either cannot be read (a build of our own
		/// never nags).</summary>
		public static bool IsNewer(string psCurrent, string psLatest)
		{
			int[] liCurrent = ParseVersion(psCurrent);
			int[] liLatest = ParseVersion(psLatest);

			if (liCurrent == null || liLatest == null)
				return false;

			for (int liAt = 0; liAt < 3; liAt++)
			{
				if (liLatest[liAt] != liCurrent[liAt])
					return liLatest[liAt] > liCurrent[liAt];
			}

			return false;
		}

		/// <summary>Whether a check is due: never checked, a day past, or the last check lies in the future (a clock
		/// set back).</summary>
		public static bool IsDue(DateTime pOCheckedUtc, DateTime pONowUtc)
		{
			return pOCheckedUtc == DateTime.MinValue || pOCheckedUtc > pONowUtc
				|| (pONowUtc - pOCheckedUtc).TotalHours >= HoursBetweenChecks;
		}

		/// <summary>The state as the file keeps it: key=value lines.</summary>
		public static string Write(State pOState)
		{
			StringBuilder lOText = new StringBuilder();

			lOText.Append("checked=").Append(pOState.CheckedUtc == DateTime.MinValue ? string.Empty
				: pOState.CheckedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)).Append('\n');
			lOText.Append("tag=").Append(fOneLine(pOState.Tag)).Append('\n');
			lOText.Append("title=").Append(fOneLine(pOState.Title)).Append('\n');
			lOText.Append("url=").Append(fOneLine(pOState.Url)).Append('\n');

			return lOText.ToString();
		}

		/// <summary>The state read back; what cannot be read stays empty (a page not of the project's releases too).</summary>
		public static State Read(string psText)
		{
			State lOState = new State();

			if (string.IsNullOrEmpty(psText))
				return lOState;

			foreach (string lsLine in psText.Replace("\r", string.Empty).Split('\n'))
			{
				int liAt = lsLine.IndexOf('=');

				if (liAt <= 0)
					continue;

				string lsKey = lsLine.Substring(0, liAt);
				string lsValue = lsLine.Substring(liAt + 1);

				switch (lsKey)
				{
					case "checked":
						if (DateTime.TryParseExact(lsValue, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture,
							DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime lOChecked))
							lOState.CheckedUtc = lOChecked;
						break;
					case "tag":
						lOState.Tag = ParseVersion(lsValue) != null ? lsValue : string.Empty;
						break;
					case "title":
						lOState.Title = lsValue;
						break;
					case "url":
						lOState.Url = IsReleasePage(lsValue) ? lsValue : string.Empty;
						break;
				}
			}

			return lOState;
		}

		private static string fOneLine(string psText)
		{
			return (psText ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
		}
	}
}
