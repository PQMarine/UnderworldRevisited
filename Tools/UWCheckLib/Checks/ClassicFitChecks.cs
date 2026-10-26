using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// TRANSLATION 5.2 (2026-10-26): the classic look's fixed boxes (UWClassicFit) - every English string
    /// fits where it is drawn (character creation, the stats page), and a made-up long word laid over a
    /// skill name is reported over its box in every place that draws it. With the data (the fonts).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckClassicFit(DataImport pOData)
        {
            Check.Group("The classic fixed boxes (UWClassicFit, uwdump fit - translation 5.2)");

            if (pOData == null)
            {
                Check.Fail("fit: data", "not loaded");

                return;
            }

            string lsPak = Path.Combine(pOData.DataPath, "STRINGS.PAK");
            List<UWClassicFit.Entry> lOEnglish = UWClassicFit.Measure(new UWStrings(File.ReadAllBytes(lsPak)), pOData.MiscDataFiles, pOData.Fonts);
            List<string> lsOver = new List<string>();
            Dictionary<string, int> lOPlaces = new Dictionary<string, int>();

            foreach (UWClassicFit.Entry lOEntry in lOEnglish)
            {
                if (lOEntry.IsOver)
                    lsOver.Add(lOEntry.Place + " " + lOEntry.Key);

                lOPlaces.TryGetValue(lOEntry.Place, out int liCount);
                lOPlaces[lOEntry.Place] = liCount + 1;
            }

            Check.Str("fit: English - strings measured, none over", lOEnglish.Count + " " + string.Join(", ", lsOver), "110 ");
            Check.Str("fit: the places and how many strings each", string.Join(", ", new List<string>(fPlaceCounts(lOPlaces))),
                "creation question 7, creation button 40, creation name prompt 1, creation skill list 20, creation sex/class line 10, "
                + "creation attribute label 4, stats page class 8, stats page skill 20");

            UWStrings lOLaid = new UWStrings(File.ReadAllBytes(lsPak));

            UWGameTextPack.Parse("2\t37\tWumpelgrumpfkolben\n").Apply(lOLaid);

            List<string> lsLaidOver = new List<string>();

            foreach (UWClassicFit.Entry lOEntry in UWClassicFit.Measure(lOLaid, pOData.MiscDataFiles, pOData.Fonts))
            {
                if (lOEntry.IsOver)
                    lsLaidOver.Add(lOEntry.Place + " " + lOEntry.Key + " " + (lOEntry.Width > lOEntry.Box));
            }

            Check.Str("fit: a made-up long skill name is over in every place drawing it", string.Join(", ", lsLaidOver),
                "creation button 2:37 True, creation skill list 2:37 True, stats page skill 2:37 True");
        }

        private static IEnumerable<string> fPlaceCounts(Dictionary<string, int> pOPlaces)
        {
            foreach (KeyValuePair<string, int> lOPair in pOPlaces)
                yield return lOPair.Key + " " + lOPair.Value;
        }
    }
}
