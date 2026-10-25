using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The run as a JUnit XML report (--junit), for a build server: one test suite, one test case
    /// per group with its time, a failure carrying the group's FAIL lines, a group skipped for want
    /// of the data as skipped. The expectations inside a group are counted, not listed - there are
    /// some six thousand of them, and the group is what a run selects and reports.
    /// </summary>
    public static class JUnitReport
    {
        public static void Write(string psPath, string psSuite, IReadOnlyList<CheckRunner.GroupResult> pOResults,
            long plTotalMilliseconds)
        {
            int liFailed = 0;
            int liSkipped = 0;

            foreach (CheckRunner.GroupResult lOResult in pOResults)
            {
                if (lOResult.Skipped)
                    liSkipped++;
                else if (lOResult.Failures > 0)
                    liFailed++;
            }

            XmlWriterSettings lOSettings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };

            using (XmlWriter lOWriter = XmlWriter.Create(psPath, lOSettings))
            {
                lOWriter.WriteStartDocument();
                lOWriter.WriteStartElement("testsuites");
                lOWriter.WriteStartElement("testsuite");
                lOWriter.WriteAttributeString("name", psSuite);
                lOWriter.WriteAttributeString("tests", pOResults.Count.ToString(CultureInfo.InvariantCulture));
                lOWriter.WriteAttributeString("failures", liFailed.ToString(CultureInfo.InvariantCulture));
                lOWriter.WriteAttributeString("errors", "0");
                lOWriter.WriteAttributeString("skipped", liSkipped.ToString(CultureInfo.InvariantCulture));
                lOWriter.WriteAttributeString("time", fSeconds(plTotalMilliseconds));

                foreach (CheckRunner.GroupResult lOResult in pOResults)
                {
                    string[] lsTags = lOResult.Entry.Tags;

                    lOWriter.WriteStartElement("testcase");
                    lOWriter.WriteAttributeString("classname", psSuite + "." + (lsTags.Length > 0 ? lsTags[0] : "misc"));
                    lOWriter.WriteAttributeString("name", lOResult.Name);
                    lOWriter.WriteAttributeString("time", fSeconds(lOResult.Milliseconds));

                    if (lOResult.Skipped)
                    {
                        lOWriter.WriteStartElement("skipped");
                        lOWriter.WriteAttributeString("message", "needs the game data");
                        lOWriter.WriteEndElement();
                    }
                    else if (lOResult.Failures > 0)
                    {
                        lOWriter.WriteStartElement("failure");
                        lOWriter.WriteAttributeString("message", lOResult.Failures + " of "
                            + (lOResult.Passes + lOResult.Failures) + " check(s) failed");
                        lOWriter.WriteString(fXmlSafe(string.Join("\n", lOResult.FailLines)));
                        lOWriter.WriteEndElement();
                    }

                    lOWriter.WriteEndElement();
                }

                lOWriter.WriteEndElement();
                lOWriter.WriteEndElement();
                lOWriter.WriteEndDocument();
            }
        }

        private static string fSeconds(long plMilliseconds)
        {
            return (plMilliseconds / 1000.0).ToString("0.000", CultureInfo.InvariantCulture);
        }

        /// <summary>A check's detail may quote raw bytes; XML has no place for control characters.</summary>
        private static string fXmlSafe(string psText)
        {
            StringBuilder lOText = new StringBuilder(psText.Length);

            foreach (char lcAt in psText)
                lOText.Append(lcAt < 0x20 && lcAt != '\n' && lcAt != '\t' ? '?' : lcAt);

            return lOText.ToString();
        }
    }
}
