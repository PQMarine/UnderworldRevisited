using System;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE CHECK FOR A NEWER RELEASE (UWUpdateCheck, GitHub issue 36, 2026-11-11): GitHub's answer read, versions
        /// compared, a day between checks, only the project's release pages opened, the kept state read back.
        /// </summary>
        private static void fCheckUpdateCheck()
        {
            Check.Group("Update check (UWUpdateCheck - issue 36)");

            string lsAnswer = "{\"tag_name\":\"v0.20.0\",\"name\":\"0.20.0 - Theme\",\"draft\":false,"
                + "\"html_url\":\"https://github.com/PQMarine/UnderworldRevisited/releases/tag/v0.20.0\",\"assets\":[]}";

            fExpectBool("update: GitHub's answer read (tag, title, page)",
                UWUpdateCheck.TryParseLatest(lsAnswer, out string lsTag, out string lsTitle, out string lsUrl)
                && lsTag == "v0.20.0" && lsTitle == "0.20.0 - Theme" && lsUrl.EndsWith("/releases/tag/v0.20.0", StringComparison.Ordinal), true);
            fExpectBool("update: an answer pointing elsewhere is refused",
                UWUpdateCheck.TryParseLatest(lsAnswer.Replace("https://github.com/PQMarine/UnderworldRevisited/releases/", "https://example.com/"),
                    out string _, out string _, out string _), false);
            fExpectBool("update: no JSON, no tag", UWUpdateCheck.TryParseLatest("rate limit", out string _, out string _, out string _), false);

            fExpectBool("update: 0.20.0 is newer than 0.19.2", UWUpdateCheck.IsNewer("0.19.2", "v0.20.0"), true);
            fExpectBool("update: 0.19.10 is newer than 0.19.9 (numbers, not text)", UWUpdateCheck.IsNewer("0.19.9", "v0.19.10"), true);
            fExpectBool("update: the same is not newer", UWUpdateCheck.IsNewer("0.19.2", "v0.19.2"), false);
            fExpectBool("update: an older one is not newer (a build of our own)", UWUpdateCheck.IsNewer("0.20.0", "v0.19.2"), false);
            fExpectBool("update: an unreadable version never nags", UWUpdateCheck.IsNewer("dev", "v9.0.0"), false);

            DateTime lONow = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

            fExpectBool("update: never checked is due", UWUpdateCheck.IsDue(DateTime.MinValue, lONow), true);
            fExpectBool("update: 23 hours later is not due", UWUpdateCheck.IsDue(lONow.AddHours(-23), lONow), false);
            fExpectBool("update: a day later is due", UWUpdateCheck.IsDue(lONow.AddHours(-24), lONow), true);
            fExpectBool("update: a check in the future (clock set back) is due", UWUpdateCheck.IsDue(lONow.AddHours(5), lONow), true);

            UWUpdateCheck.State lOState = new UWUpdateCheck.State
            {
                CheckedUtc = lONow, Tag = "v0.20.0", Title = "0.20.0 - Theme",
                Url = "https://github.com/PQMarine/UnderworldRevisited/releases/tag/v0.20.0"
            };
            UWUpdateCheck.State lOBack = UWUpdateCheck.Read(UWUpdateCheck.Write(lOState));

            fExpectBool("update: the kept state reads back", lOBack.CheckedUtc == lONow && lOBack.Tag == lOState.Tag
                && lOBack.Title == lOState.Title && lOBack.Url == lOState.Url, true);
            fExpectBool("update: a kept page not of the project's releases is dropped",
                UWUpdateCheck.Read("url=https://example.com/x\n").Url.Length == 0, true);
        }
    }
}
