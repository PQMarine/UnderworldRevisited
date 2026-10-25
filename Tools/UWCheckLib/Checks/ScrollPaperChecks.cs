using System;
using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE MODERN MESSAGE SCROLL'S PAPER (UWScrollPaperBuilder, cleanup 9.16, 2026-10-25) over a
        /// grid of heights and edge frames, from MAIN.BYT and from Classic Wide's widened frame,
        /// hashed against Pins/scroll-paper-hashes.txt (its own file: the hashes were new when the
        /// builder moved out of UWModernScroll, proven byte-identical to the old upload then). The
        /// heights: the frame's own and some rows more or fewer, as the scroll's lines ask for (a
        /// line's height more or less per line from four; the rows are given, not taken from the
        /// font, which the GOG archive does not carry). The hashes are of our composed pictures, not
        /// of the game's art.
        /// </summary>
        private static void fCheckScrollPaper(CheckContext pOContext)
        {
            Check.Group("Scroll paper hashes (UWScrollPaperBuilder over heights and edge frames)");

            DataImport lOData = pOContext.Data;
            UWTextures lOTextures = lOData.Textures;
            SortedDictionary<string, string> lONow = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach ((string lsFrame, int liWideExtra) in new[] { ("main", -1), ("classic wide 1", 1), ("classic wide 100", 100), ("classic wide 480", 480) })
            {
                string lsWide = lsFrame;
                UWPicture lOWide = null;

                if (liWideExtra >= 0)
                {
                    try
                    {
                        lOWide = UWClassicWideBuilder.Build(lOTextures, liWideExtra).Picture;
                    }
                    catch (Exception lOException)
                    {
                        lONow["scroll paper: " + lsWide] = "<threw " + lOException.GetType().Name + ">";

                        continue;
                    }
                }

                foreach (int liExtra in new[] { -12, -6, 0, 6, 7, 12, 18, 24, 28 })
                {
                    for (int liEdge = 0; liEdge < UWScrollEdgeRules.MainFrames; liEdge++)
                    {
                        // Every edge frame at the frame's own height and one taller, the first elsewhere.
                        if (liEdge > 0 && liExtra != 0 && liExtra != 12)
                            continue;

                        string lsName = string.Format("scroll paper: {0} extra {1} edge {2}", lsWide, liExtra, liEdge);

                        try
                        {
                            UWPicture lOPaper = UWScrollPaperBuilder.Build(lOTextures, lOWide, liExtra, liEdge);

                            lONow[lsName] = lOPaper == null ? "null" : string.Format("{0}x{1} {2:X8}", lOPaper.Width, lOPaper.Height, fCrc32(lOPaper));
                        }
                        catch (Exception lOException)
                        {
                            lONow[lsName] = "<threw " + lOException.GetType().Name + ">";
                        }
                    }
                }
            }

            fComparePins(pOContext, "scroll-paper-hashes.txt", "scroll paper", lONow, psName =>
            {
                int liFrom = psName.IndexOf(": ", StringComparison.Ordinal) + 2;
                int liAt = psName.IndexOf(" extra", StringComparison.Ordinal);

                return liAt > liFrom ? "from " + psName.Substring(liFrom, liAt - liFrom) : psName;
            });
        }
    }
}
