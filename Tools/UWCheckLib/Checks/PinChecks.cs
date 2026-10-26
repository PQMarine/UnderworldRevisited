using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The pins of the cleanup (Docs/CLEANUP-PLAN.md, phase 0): checks that compare the current
    /// state of the code against a recorded one, so that a refactor which changes a byte of a
    /// save, a rule constant or a pixel of the composed HUD art is caught at once. Recorded with
    /// --record into Tools/UWSelfCheck/Pins; a run without that option compares. The save round
    /// trip (SaveRoundTripChecks) and the HUD art hashes (HudArtChecks) have files of their own
    /// since cleanup 9.2; the pin files, the constant snapshot and the CRC stay here.
    /// </summary>
    public static partial class SelfChecks
    {
        private const string PinsFolder = "Tools/UWSelfCheck/Pins";

        /// <summary>The folder of the pin files, or null outside the source tree.</summary>
        private static string fGetPinsFolder()
        {
            string lsRoot = CheckContext.ProjectRoot;

            return lsRoot != null ? Path.Combine(lsRoot, PinsFolder.Replace('/', Path.DirectorySeparatorChar)) : null;
        }

        // ------------------------------------------------- constant snapshot

        /// <summary>
        /// EVERY CONSTANT of the engine-free layer, by name and value, against the recorded list
        /// in Pins/rule-constants.txt: the const fields and the static readonly scalars and
        /// primitive arrays (the tables). A refactor that moves a constant keeps its name in
        /// the list under the new type, so a renamed home shows as one line removed and one
        /// added; a changed value shows as a change. Re-record with --record after a change
        /// that is meant.
        /// </summary>
        private static void fCheckConstants(CheckContext pOContext)
        {
            Check.Group("Constant snapshot (every const and table of the engine-free layer)");

            // The scan is shared with uwdump constants (Tools/Shared/UWConstantScan, cleanup 9.2), so
            // that command lists exactly what is pinned here.
            SortedDictionary<string, string> lONow = UWConstantScan.Scan(out int liTypesSkipped);

            fComparePins(pOContext, "rule-constants.txt", "constants", lONow, lsLine => lsLine);

            if (liTypesSkipped > 0)
                Check.Fail("constants: every type readable", liTypesSkipped + " type(s) could not be reflected");
        }

        /// <summary>
        /// Compares a computed name-to-value map with a pin file (one "name = value" per line,
        /// sorted); with --record the file is written instead. psFamilyOf groups the names for
        /// the pass lines, so a thousand matching entries print as a few lines.
        /// </summary>
        private static void fComparePins(CheckContext pOContext, string psFile, string psLabel,
            SortedDictionary<string, string> pONow, Func<string, string> psFamilyOf)
        {
            string lsFolder = fGetPinsFolder();

            if (lsFolder == null)
            {
                Check.Pass(psLabel + ": pins", "no source tree here, nothing to compare");

                return;
            }

            string lsPath = Path.Combine(lsFolder, psFile);

            if (pOContext.Record)
            {
                Directory.CreateDirectory(lsFolder);

                StringBuilder lOText = new StringBuilder();

                foreach (KeyValuePair<string, string> lOEntry in pONow)
                    lOText.Append(lOEntry.Key).Append(" = ").Append(lOEntry.Value).Append('\n');

                File.WriteAllText(lsPath, lOText.ToString());
                Check.Pass(psLabel + ": recorded", pONow.Count + " entries into " + PinsFolder + "/" + psFile);

                return;
            }

            if (!File.Exists(lsPath))
            {
                Check.Fail(psLabel + ": pin file " + PinsFolder + "/" + psFile, "missing - run once with --record");

                return;
            }

            Dictionary<string, string> lOPinned = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string lsLine in File.ReadAllLines(lsPath))
            {
                int liAt = lsLine.IndexOf(" = ", StringComparison.Ordinal);

                if (liAt > 0)
                    lOPinned[lsLine.Substring(0, liAt)] = lsLine.Substring(liAt + 3);
            }

            List<string> lOChanged = new List<string>();
            List<string> lOAdded = new List<string>();
            List<string> lORemoved = new List<string>();
            SortedDictionary<string, int> lOFamilies = new SortedDictionary<string, int>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> lOEntry in pONow)
            {
                string lsPinned;

                if (!lOPinned.TryGetValue(lOEntry.Key, out lsPinned))
                    lOAdded.Add(lOEntry.Key);
                else if (lsPinned != lOEntry.Value)
                    lOChanged.Add(lOEntry.Key + ": now " + lOEntry.Value + ", pinned " + lsPinned);
                else
                {
                    string lsFamily = psFamilyOf(lOEntry.Key);
                    int liCount;

                    lOFamilies.TryGetValue(lsFamily, out liCount);
                    lOFamilies[lsFamily] = liCount + 1;
                }
            }

            foreach (string lsKey in lOPinned.Keys)
                if (!pONow.ContainsKey(lsKey))
                    lORemoved.Add(lsKey);

            if (psFamilyOf == null || lOFamilies.Count > 40)
                Check.Pass(psLabel + ": entries as pinned", (pONow.Count - lOAdded.Count - lOChanged.Count) + " of " + pONow.Count);
            else
                foreach (KeyValuePair<string, int> lOFamily in lOFamilies)
                    Check.Pass(psLabel + ": " + lOFamily.Key, lOFamily.Value + " as pinned");

            fReportPinList(psLabel + ": changed", lOChanged);
            fReportPinList(psLabel + ": added (not pinned)", lOAdded);
            fReportPinList(psLabel + ": removed (pinned, gone)", lORemoved);
        }

        private static void fReportPinList(string psName, List<string> psEntries)
        {
            if (psEntries.Count == 0)
                return;

            Check.Fail(psName, psEntries.Count + " entr" + (psEntries.Count == 1 ? "y" : "ies") + " - re-record with --record if meant");

            for (int liAt = 0; liAt < psEntries.Count && liAt < 12; liAt++)
                Console.WriteLine("         " + psEntries[liAt]);

            if (psEntries.Count > 12)
                Console.WriteLine("         ... and " + (psEntries.Count - 12) + " more");
        }

        // ------------------------------------------------- CRC32

        private static uint[] msCrcTable;

        private static uint fCrc32(byte[] pyBytes)
        {
            if (msCrcTable == null)
            {
                msCrcTable = new uint[256];

                for (uint luAt = 0; luAt < 256; luAt++)
                {
                    uint luCrc = luAt;

                    for (int liBit = 0; liBit < 8; liBit++)
                        luCrc = (luCrc & 1) != 0 ? 0xEDB88320u ^ (luCrc >> 1) : luCrc >> 1;

                    msCrcTable[luAt] = luCrc;
                }
            }

            uint luResult = 0xFFFFFFFFu;

            foreach (byte lyByte in pyBytes)
                luResult = msCrcTable[(luResult ^ lyByte) & 0xFF] ^ (luResult >> 8);

            return luResult ^ 0xFFFFFFFFu;
        }

        private static uint fCrc32(UWPicture pOPicture)
        {
            byte[] lyBytes = new byte[pOPicture.Pixels.Length * 4];

            for (int liAt = 0; liAt < pOPicture.Pixels.Length; liAt++)
            {
                UWColor32 lOColour = pOPicture.Pixels[liAt];

                lyBytes[liAt * 4] = lOColour.R;
                lyBytes[(liAt * 4) + 1] = lOColour.G;
                lyBytes[(liAt * 4) + 2] = lOColour.B;
                lyBytes[(liAt * 4) + 3] = lOColour.A;
            }

            return fCrc32(lyBytes);
        }

        private static uint fCrc32(bool[] pbMask)
        {
            byte[] lyBytes = new byte[pbMask.Length];

            for (int liAt = 0; liAt < pbMask.Length; liAt++)
                lyBytes[liAt] = pbMask[liAt] ? (byte)1 : (byte)0;

            return fCrc32(lyBytes);
        }

        private static uint fCrc32(int[] piValues)
        {
            byte[] lyBytes = new byte[piValues.Length * 4];

            for (int liAt = 0; liAt < piValues.Length; liAt++)
            {
                lyBytes[liAt * 4] = (byte)piValues[liAt];
                lyBytes[(liAt * 4) + 1] = (byte)(piValues[liAt] >> 8);
                lyBytes[(liAt * 4) + 2] = (byte)(piValues[liAt] >> 16);
                lyBytes[(liAt * 4) + 3] = (byte)(piValues[liAt] >> 24);
            }

            return fCrc32(lyBytes);
        }
    }
}
