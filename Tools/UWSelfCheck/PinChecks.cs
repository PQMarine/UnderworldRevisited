using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The pins of the cleanup (Docs/CLEANUP-PLAN.md, phase 0): checks that compare the current
    /// state of the code against a recorded one, so that a refactor which changes a byte of a
    /// save, a rule constant or a pixel of the composed HUD art is caught at once. Recorded with
    /// --record into Tools/UWSelfCheck/Pins; a run without that option compares.
    /// </summary>
    public static partial class Program
    {
        private const string PinsFolder = "Tools/UWSelfCheck/Pins";

        /// <summary>The folder of the pin files, or null outside the source tree.</summary>
        private static string fGetPinsFolder()
        {
            string lsRoot = UWRuleIndexScan.FindProjectRoot();

            return lsRoot != null ? Path.Combine(lsRoot, PinsFolder.Replace('/', Path.DirectorySeparatorChar)) : null;
        }

        // ------------------------------------------------- save round trip

        /// <summary>
        /// THE SAVE ROUND TRIP: the level writer, given a level as loaded and the block it came
        /// from, must produce that block again byte for byte; the whole LEV.ARK written into a
        /// copy must equal the file read; PLAYER.DAT survives decrypt and encrypt; BGLOBALS.DAT
        /// survives load and serialise. Over the nine DATA levels always, and over every save
        /// folder under --saves when given. No such check existed before 2026-10-11, and every
        /// step of the cleanup that touches the data layer leans on it.
        ///
        /// Not covered yet: PLAYER.DAT through UWPlayerData.WritePosition with the same state,
        /// because the SavedState is only built on the Unity side today (phase 2 of the plan).
        /// </summary>
        private static void fCheckSaveRoundTrip(CheckContext pOContext)
        {
            Check.Group("Save round trip (UWLevelWriter, UWPlayerData, UWConversationGlobals)");

            fCheckWorldRoundTrip(pOContext.Data, pOContext.DataPath, "DATA");

            byte[] lyTemplate = File.ReadAllBytes(Path.Combine(pOContext.DataPath, "PLAYER.DAT"));

            Check.Bool("round trip: DATA\\PLAYER.DAT is the plain template", UWPlayerData.IsPlainTemplate(lyTemplate), true);
            Check.Bool("round trip: the template decrypts after encrypting",
                fSameBytes(UWPlayerData.Decrypt(UWPlayerData.Encrypt(lyTemplate, 0x2A)), lyTemplate, out _), true);
            Check.Bool("round trip: the template parses", UWPlayerData.FromPlain(lyTemplate) != null, true);

            if (string.IsNullOrEmpty(pOContext.SavesPath))
            {
                Check.Pass("round trip: saves", "none given (--saves <folder> checks every SAVE* in it)");

                return;
            }

            int liSaves = 0;

            foreach (string lsSave in Directory.GetDirectories(pOContext.SavesPath))
            {
                if (!File.Exists(Path.Combine(lsSave, "LEV.ARK")) || !File.Exists(Path.Combine(lsSave, "PLAYER.DAT")))
                    continue;

                string lsName = Path.GetFileName(lsSave);
                DataImport lOData;

                try
                {
                    lOData = new DataImport(pOContext.DataPath, lsSave);
                }
                catch (Exception lOException)
                {
                    Check.Fail("round trip: " + lsName + " loads", lOException.Message);

                    continue;
                }

                liSaves++;
                fCheckWorldRoundTrip(lOData, lsSave, lsName);

                byte[] lyRaw = File.ReadAllBytes(Path.Combine(lsSave, "PLAYER.DAT"));
                byte[] lyAgain = UWPlayerData.Encrypt(UWPlayerData.Decrypt(lyRaw), lyRaw[0]);

                Check.Bool("round trip: " + lsName + " PLAYER.DAT decrypt then encrypt", fSameBytes(lyAgain, lyRaw, out _), true);

                string lsGlobals = Path.Combine(lsSave, "BGLOBALS.DAT");

                if (File.Exists(lsGlobals) && lOData.ConversationGlobals != null)
                {
                    byte[] lyFile = File.ReadAllBytes(lsGlobals);
                    int liFirst;
                    bool lbSame = fSameBytes(lOData.ConversationGlobals.Serialize(), lyFile, out liFirst);

                    if (lbSame)
                        Check.Pass("round trip: " + lsName + " BGLOBALS.DAT serialises identically", lyFile.Length + " bytes");
                    else
                        Check.Fail("round trip: " + lsName + " BGLOBALS.DAT serialises identically", "differs at " + liFirst);
                }
            }

            Check.Bool("round trip: at least one save folder found under " + pOContext.SavesPath, liSaves > 0, true);
        }

        /// <summary>The nine level blocks through BuildBlock, then the whole file through
        /// SaveMapData into a scratch copy of the folder.</summary>
        private static void fCheckWorldRoundTrip(DataImport pOData, string psFolder, string psLabel)
        {
            byte[] lyArk = File.ReadAllBytes(Path.Combine(psFolder, "LEV.ARK"));
            int liCount = lyArk[0] | (lyArk[1] << 8);

            for (int liLevel = 0; liLevel < 9 && liLevel < liCount; liLevel++)
            {
                uint luOffset = BitConverter.ToUInt32(lyArk, 2 + (liLevel * 4));
                string lsName = string.Format("round trip: {0} level {1} block through BuildBlock", psLabel, liLevel + 1);

                if (luOffset == 0 || luOffset + UWLevArkLayout.BlockSize > lyArk.Length || pOData.Levels == null || pOData.Levels.Count <= liLevel)
                {
                    Check.Fail(lsName, "no block or no level");

                    continue;
                }

                byte[] lyOriginal = new byte[UWLevArkLayout.BlockSize];

                Array.Copy(lyArk, (int)luOffset, lyOriginal, 0, lyOriginal.Length);

                UWLevelWriter.Warnings.Clear();

                byte[] lyBuilt = UWLevelWriter.BuildBlock(pOData.Levels[liLevel], lyOriginal);
                byte[] lyExpected = (byte[])lyOriginal.Clone();
                int liRepaired = fRepairActiveList(lyExpected, 0);
                int liFirst = -1;

                if (lyBuilt != null && fSameBytes(lyBuilt, lyExpected, out liFirst))
                    Check.Pass(lsName, "identical" + (liRepaired > 0 ? ", active list repaired by " + liRepaired : "")
                        + (UWLevelWriter.Warnings.Count > 0 ? ", " + UWLevelWriter.Warnings.Count + " warning(s)" : ""));
                else
                    Check.Fail(lsName, lyBuilt == null ? "no block built" : fDescribeDifference(lyBuilt, lyExpected, liFirst));
            }

            // The whole file: SaveMapData writes the loaded world over a copy of the folder.
            string lsScratch = Path.Combine(Path.GetTempPath(), "uwselfcheck-roundtrip", psLabel);

            try
            {
                if (Directory.Exists(lsScratch))
                    Directory.Delete(lsScratch, true);

                Directory.CreateDirectory(lsScratch);
                File.Copy(Path.Combine(psFolder, "LEV.ARK"), Path.Combine(lsScratch, "LEV.ARK"));

                string lsName = "round trip: " + psLabel + " LEV.ARK through SaveMapData";

                if (!pOData.SaveMapData(lsScratch))
                {
                    Check.Fail(lsName, "SaveMapData refused");
                }
                else
                {
                    byte[] lyWritten = File.ReadAllBytes(Path.Combine(lsScratch, "LEV.ARK"));
                    byte[] lyExpected = (byte[])lyArk.Clone();
                    int liFirst;

                    for (int liLevel = 0; liLevel < 9 && liLevel < liCount; liLevel++)
                    {
                        uint luOffset = BitConverter.ToUInt32(lyExpected, 2 + (liLevel * 4));

                        if (luOffset != 0 && luOffset + UWLevArkLayout.BlockSize <= lyExpected.Length)
                            fRepairActiveList(lyExpected, (int)luOffset);
                    }

                    if (fSameBytes(lyWritten, lyExpected, out liFirst))
                        Check.Pass(lsName, lyArk.Length + " bytes identical");
                    else
                        Check.Fail(lsName, fDescribeDifference(lyWritten, lyExpected, liFirst));
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(lsScratch))
                        Directory.Delete(lsScratch, true);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// What the writer must make of a level block's list of active critters, worked out
        /// from the bytes alone: in the original's data the list holds exactly the creatures
        /// standing in a tile (UWLevelWriter.fWriteActiveMobiles). Entries that hold none leave
        /// it (the last moving in), standing creatures it lacks are appended in tile order - our
        /// saves before 2026-10-12 left off creatures made during play. A non-creature in a mobile
        /// slot in a tile's list that is on the list is a thing in flight and stays where it is
        /// (UWMobileRecordCodec, 2026-10-19). Returns how many entries changed.
        /// </summary>
        private static int fRepairActiveList(byte[] pyData, int piBlock)
        {
            List<int> lOStanding = new List<int>();
            List<int> lOInTiles = new List<int>();

            for (int liTile = 0; liTile < UWLevArkLayout.TileCount; liTile++)
            {
                int liIndex = (int)(BitConverter.ToUInt32(pyData, piBlock + (liTile * UWLevArkLayout.TileRecordSize))
                    >> UWLevArkLayout.TileFirstObjectShift);
                int liGuard = 0;

                while (liIndex != 0 && liIndex < UWLevArkLayout.ObjectCount && liGuard++ < UWLevArkLayout.ObjectCount)
                {
                    int liAt = piBlock + UWLevArkLayout.MobileBase + (liIndex < UWLevArkLayout.MobileCount
                        ? liIndex * UWLevArkLayout.MobileRecordSize
                        : (UWLevArkLayout.MobileCount * UWLevArkLayout.MobileRecordSize)
                            + ((liIndex - UWLevArkLayout.MobileCount) * UWLevArkLayout.StaticRecordSize));
                    int liId = BitConverter.ToUInt16(pyData, liAt) & 0x1FF;

                    if (UWLevArkLayout.IsMobileSlot(liIndex) && UWObjectIds.IsCreature(liId) && !lOStanding.Contains(liIndex))
                        lOStanding.Add(liIndex);

                    if (UWLevArkLayout.IsMobileSlot(liIndex) && liId != 0 && !UWObjectIds.IsCreature(liId))
                        lOInTiles.Add(liIndex);

                    liIndex = BitConverter.ToUInt16(pyData, liAt + 4) >> 6;
                }
            }

            List<int> lOList = UWLevArkCodec.ReadActiveMobiles(pyData, piBlock);
            int liChanged = 0;

            foreach (int liListed in new List<int>(lOList))
            {
                if (!lOStanding.Contains(liListed) && !lOInTiles.Contains(liListed))
                {
                    UWLevArkCodec.RemoveActiveMobile(lOList, liListed);
                    liChanged++;
                }
            }

            foreach (int liIndex in lOStanding)
            {
                if (!lOList.Contains(liIndex))
                {
                    lOList.Add(liIndex);
                    liChanged++;
                }
            }

            if (liChanged > 0)
                UWLevArkCodec.WriteActiveMobiles(pyData, piBlock, lOList);

            return liChanged;
        }

        private static bool fSameBytes(byte[] pyLeft, byte[] pyRight, out int piFirstDifference)
        {
            piFirstDifference = -1;

            if (pyLeft == null || pyRight == null)
                return pyLeft == pyRight;

            int liCount = Math.Min(pyLeft.Length, pyRight.Length);

            for (int liAt = 0; liAt < liCount; liAt++)
            {
                if (pyLeft[liAt] != pyRight[liAt])
                {
                    piFirstDifference = liAt;

                    return false;
                }
            }

            if (pyLeft.Length != pyRight.Length)
            {
                piFirstDifference = liCount;

                return false;
            }

            return true;
        }

        private static string fDescribeDifference(byte[] pyBuilt, byte[] pyOriginal, int piFirst)
        {
            if (pyBuilt.Length != pyOriginal.Length)
                return string.Format("length {0}, expected {1}", pyBuilt.Length, pyOriginal.Length);

            int liDiffering = 0;

            for (int liAt = 0; liAt < pyBuilt.Length; liAt++)
                if (pyBuilt[liAt] != pyOriginal[liAt])
                    liDiffering++;

            return string.Format("{0} byte(s) differ, the first at 0x{1:X} (got {2:X2}, expected {3:X2})",
                liDiffering, piFirst, pyBuilt[piFirst], pyOriginal[piFirst]);
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

            SortedDictionary<string, string> lONow = new SortedDictionary<string, string>(StringComparer.Ordinal);
            int liTypesSkipped = 0;

            // Static readonly arrays that are state, not constants: the path search's scratch
            // buffers and the creature motion's per-session quirk. They change while the checks
            // run and would show as changed constants.
            HashSet<string> lOState = new HashSet<string>(StringComparer.Ordinal)
            {
                "UWTilePath.miStamp", "UWTilePath.miDistance", "UWTilePath.miQueue", "UWTilePath.miWaveCount",
                "UWCreatureMotion.msLavaFlagDropped",
            };

            foreach (Type lOType in typeof(DataImport).Assembly.GetTypes())
            {
                FieldInfo[] lOFields;

                try
                {
                    lOFields = lOType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                }
                catch
                {
                    liTypesSkipped++;

                    continue;
                }

                foreach (FieldInfo lOField in lOFields)
                {
                    string lsName = fTypeName(lOType) + "." + lOField.Name;

                    if (lOState.Contains(lsName))
                        continue;

                    try
                    {
                        if (lOField.IsLiteral)
                            lONow[lsName] = fFormatConstant(lOField.GetRawConstantValue());
                        else if (lOField.IsInitOnly && fIsPinnable(lOField.FieldType))
                            lONow[lsName] = fFormatConstant(lOField.GetValue(null));
                    }
                    catch (Exception lOException)
                    {
                        lONow[lsName] = "<unreadable: " + lOException.GetType().Name + ">";
                    }
                }
            }

            fComparePins(pOContext, "rule-constants.txt", "constants", lONow, lsLine => lsLine);

            if (liTypesSkipped > 0)
                Check.Fail("constants: every type readable", liTypesSkipped + " type(s) could not be reflected");
        }

        private static string fTypeName(Type pOType)
        {
            if (pOType.DeclaringType != null)
                return fTypeName(pOType.DeclaringType) + "." + pOType.Name;

            return pOType.Name;
        }

        private static bool fIsPinnable(Type pOType)
        {
            if (pOType.IsArray)
            {
                Type lOElement = pOType.GetElementType();

                return lOElement != null && (lOElement.IsPrimitive || lOElement == typeof(string) || lOElement.IsEnum)
                    || (lOElement != null && lOElement.IsArray && fIsPinnable(lOElement));
            }

            return pOType.IsPrimitive || pOType == typeof(string) || pOType == typeof(decimal) || pOType.IsEnum;
        }

        private static string fFormatConstant(object pOValue)
        {
            if (pOValue == null)
                return "null";

            if (pOValue is string lsText)
                return "\"" + lsText.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";

            if (pOValue is float lfValue)
                return lfValue.ToString("R", CultureInfo.InvariantCulture) + "f";

            if (pOValue is double ldValue)
                return ldValue.ToString("R", CultureInfo.InvariantCulture) + "d";

            if (pOValue is char lcValue)
                return "'" + lcValue + "'";

            if (pOValue is Array lOArray)
            {
                // Rows flattened; a two-dimensional table prints as one row (its shape is in the
                // length).
                StringBuilder lOText = new StringBuilder("[");
                bool lbFirst = true;

                foreach (object lOElement in lOArray)
                {
                    if (!lbFirst)
                        lOText.Append(", ");

                    lbFirst = false;
                    lOText.Append(fFormatConstant(lOElement));
                }

                return lOText.Append("]").ToString();
            }

            if (pOValue is IFormattable lOFormattable)
                return lOFormattable.ToString(null, CultureInfo.InvariantCulture);

            return pOValue.ToString();
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

        // ------------------------------------------------- HUD art hashes

        /// <summary>
        /// EVERY COMPOSED HUD PICTURE (UWHudArt, UWBackdropArt) over a grid of sizes and options,
        /// hashed, against Pins/hud-art-hashes.txt. The user confirmed these pictures pixel by
        /// pixel (the cloud leather, the freed paperdoll, the stone shelf seams, the Classic Wide
        /// cuts, the compass disc), so the split of UWHudArt into builder classes must keep every
        /// hash. The hashes are of our composed pictures, not of the game's art.
        /// </summary>
        private static void fCheckHudArt(CheckContext pOContext)
        {
            Check.Group("HUD art hashes (UWHudArt and UWBackdropArt over a grid of sizes)");

            DataImport lOData = pOContext.Data;
            UWTextures lOTextures = lOData.Textures;
            SortedDictionary<string, string> lONow = new SortedDictionary<string, string>(StringComparer.Ordinal);

            void fAdd(string psName, UWPicture pOPicture, string psExtra = null)
            {
                lONow[psName] = pOPicture == null
                    ? "null"
                    : string.Format("{0}x{1} {2:X8}{3}", pOPicture.Width, pOPicture.Height, fCrc32(pOPicture), psExtra != null ? " " + psExtra : "");
            }

            void fAddMask(string psName, bool[] pbMask)
            {
                lONow[psName] = pbMask == null ? "null" : string.Format("{0} {1:X8}", pbMask.Length, fCrc32(pbMask));
            }

            void fTry(string psName, Action pOBuild)
            {
                try
                {
                    pOBuild();
                }
                catch (Exception lOException)
                {
                    lONow[psName] = "<threw " + lOException.GetType().Name + ">";
                }
            }

            (int W, int H)[] lOLeatherSizes = { (1, 1), (8, 8), (9, 9), (20, 12), (44, 24), (85, 41), (100, 60), (160, 41), (236, 80), (300, 200), (320, 24) };

            foreach ((int liWidth, int liHeight) in lOLeatherSizes)
                for (int liVariant = 0; liVariant < 3; liVariant++)
                {
                    string lsName = string.Format("leather: {0}x{1} v{2}", liWidth, liHeight, liVariant);

                    fTry(lsName, () => fAdd(lsName, UWHudArt.BuildLeather(lOTextures, liWidth, liHeight, liVariant)));
                }

            foreach ((int liWidth, int liHeight) in new[] { (20, 12), (85, 41), (236, 80) })
            {
                string lsName = string.Format("leather tab: {0}x{1}", liWidth, liHeight);

                fTry(lsName, () => fAdd(lsName, UWHudArt.BuildLeatherTab(lOTextures, liWidth, liHeight)));
            }

            foreach ((int liWidth, int liOpenHalf) in new[] { (44, 46), (120, 60), (200, 46), (320, 100) })
            {
                string lsName = string.Format("heading strip: {0} open {1}", liWidth, liOpenHalf);

                fTry(lsName, () => fAdd(lsName, UWHudArt.BuildHeadingStrip(lOTextures, liWidth, liOpenHalf)));
            }

            foreach (int liWidth in new[] { 120, 320 })
            {
                string lsName = "heading strip: " + liWidth + " brand centred";

                fTry(lsName, () => fAdd(lsName, UWHudArt.BuildHeadingStripWithBrandAt(lOTextures, liWidth, UWHudArt.BrandCentredLeft(liWidth))));
            }

            fTry("slot: circle", () => fAdd("slot: circle", UWHudArt.BuildSlotCircle(lOTextures)));
            fTry("slot: ring plain", () => fAdd("slot: ring plain", UWHudArt.BuildSlotRing(lOTextures)));
            fTry("paperdoll: page", () => fAdd("paperdoll: page", UWHudArt.BuildPaperdollPage(lOTextures)));
            fTry("paperdoll: marks plain", () => fAdd("paperdoll: marks plain", UWHudArt.BuildPaperdollMarks(lOTextures)));

            foreach (UWBackdropArt.ColourEnum leColour in Enum.GetValues(typeof(UWBackdropArt.ColourEnum)))
            {
                UWBackdropArt.ColourEnum leAt = leColour;

                fTry("slot: ring " + leAt, () => fAdd("slot: ring " + leAt, UWHudArt.BuildSlotRing(lOTextures, UWBackdropArt.Ramp(lOTextures, leAt))));
                fTry("paperdoll: marks " + leAt, () => fAdd("paperdoll: marks " + leAt, UWHudArt.BuildPaperdollMarks(lOTextures, UWBackdropArt.Ramp(lOTextures, leAt))));
                fTry("backdrop: ramp " + leAt, () =>
                {
                    List<UWColor32> lORamp = UWBackdropArt.Ramp(lOTextures, leAt);
                    UWPicture lOStrip = new UWPicture(Math.Max(1, lORamp.Count), 1);

                    for (int liAt = 0; liAt < lORamp.Count; liAt++)
                        lOStrip.Set(liAt, 0, lORamp[liAt]);

                    fAdd("backdrop: ramp " + leAt, lOStrip);
                });
            }

            List<UWTexture> lOBodies = lOTextures.GetTexturesByType(UWTexture.TextureTypes.BODIES);

            for (int liBody = 0; lOBodies != null && liBody < lOBodies.Count; liBody++)
            {
                int liAt = liBody;
                string lsName = "paperdoll: figure body " + liAt;

                fTry(lsName, () =>
                {
                    UWPicture lOFigure = UWHudArt.ComposePaperdoll(lOTextures, liAt, new List<UWPicture>(), new List<(int X, int Y)>(), out int liX, out int liY);

                    fAdd(lsName, lOFigure, string.Format("at {0},{1}", liX, liY));
                });

                if (liAt < 2)
                {
                    string lsFree = "paperdoll: freed body " + liAt;

                    fTry(lsFree, () => fAdd(lsFree, UWHudArt.FreeOnPage(lOTextures, UWPicture.From(lOBodies[liAt]), UWHudArt.PaperdollBodyX, UWHudArt.PaperdollBodyY)));
                    fTry(lsFree + " all pieces", () => fAdd(lsFree + " all pieces",
                        UWHudArt.FreeOnPage(lOTextures, UWPicture.From(lOBodies[liAt]), UWHudArt.PaperdollBodyX, UWHudArt.PaperdollBodyY, false)));
                }
            }

            foreach (bool lbApart in new[] { false, true })
            {
                string lsName = "shelf: gem " + (lbApart ? "apart" : "on");

                fTry(lsName, () =>
                {
                    UWPicture lOShelf = UWHudArt.BuildShelf(lOTextures, out UWHudArt.ShelfLayout lOLayout, lbApart);

                    fAdd(lsName, lOShelf, string.Format("flasks {0},{1} {2},{3} gem {4},{5}", lOLayout.HealthFlaskX, lOLayout.HealthFlaskY,
                        lOLayout.ManaFlaskX, lOLayout.ManaFlaskY, lOLayout.GemX, lOLayout.GemY));
                });
            }

            fTry("shelf: gem stand", () =>
            {
                UWPicture lOStand = UWHudArt.BuildGemStand(lOTextures, out int liGemX, out int liGemY);

                fAdd("shelf: gem stand", lOStand, string.Format("gem {0},{1}", liGemX, liGemY));
            });

            foreach (int liWidth in new[] { 1, 24, 25, 80, 81, 150, 250 })
            {
                string lsName = "stone shelf: " + liWidth;

                fTry(lsName, () => fAdd(lsName, UWHudArt.BuildStoneShelf(lOTextures, liWidth)));
            }

            foreach (int liExtra in new[] { 0, 1, 2, 63, 100, 201, 480 })
            {
                string lsName = "classic wide: extra " + liExtra;

                fTry(lsName, () =>
                {
                    UWHudArt.ClassicWideFrame lOFrame = UWHudArt.BuildClassicWideFrame(lOTextures, liExtra);

                    fAdd(lsName, lOFrame.Picture, string.Format("extra {0} left {1} hole {2},{3} {4}x{5}", lOFrame.Extra, lOFrame.Left,
                        lOFrame.Hole.X, lOFrame.Hole.Y, lOFrame.Hole.Width, lOFrame.Hole.Height));
                });
            }

            fTry("conversation: header", () => fAdd("conversation: header", UWHudArt.BuildConversationHeader(lOTextures)));
            fTry("conversation: header padded", () => fAdd("conversation: header padded", UWHudArt.PadAndOutline(UWHudArt.BuildConversationHeader(lOTextures), false)));
            fTry("conversation: header outlined", () => fAdd("conversation: header outlined", UWHudArt.PadAndOutline(UWHudArt.BuildConversationHeader(lOTextures), true)));

            foreach (int liRows in new[] { 29, 89, 120 })
                foreach (bool lbKnobs in new[] { false, true })
                {
                    string lsName = string.Format("conversation: parchment {0} rows{1}", liRows, lbKnobs ? " knobs" : "");

                    fTry(lsName, () => fAdd(lsName, UWHudArt.BuildConversationParchment(lOTextures, liRows, lbKnobs)));
                }

            foreach (bool lbOutline in new[] { false, true })
            {
                string lsSuffix = lbOutline ? " outlined" : "";

                fTry("rune hollow:" + lsSuffix, () => fAdd("rune hollow:" + lsSuffix, UWHudArt.BuildRuneHollow(lOTextures, lbOutline)));
                fTry("compass: disc" + lsSuffix, () => fAdd("compass: disc" + lsSuffix, UWHudArt.BuildCompassDisc(lOTextures, lbOutline)));
            }

            List<UWTexture> lOCompass = lOTextures.GetTexturesByType(UWTexture.TextureTypes.COMPASS);

            for (int liPicture = 0; lOCompass != null && liPicture < lOCompass.Count && liPicture < 20; liPicture++)
            {
                int liAt = liPicture;
                string lsName = "compass: " + (liAt < 4 ? "cross mask " : "tip keeps ") + liAt;

                fTry(lsName, () =>
                {
                    UWColor32[] lOPixels = lOCompass[liAt].GetUWColor32();
                    bool[] lbKeep;

                    if (liAt < 4)
                        lbKeep = UWHudArt.CompassCrossMask(lOPixels, lOCompass[liAt].Width, lOCompass[liAt].Height);
                    else
                    {
                        lbKeep = new bool[lOPixels.Length];

                        for (int liPixel = 0; liPixel < lOPixels.Length; liPixel++)
                            lbKeep[liPixel] = UWHudArt.CompassTipKeeps(lOPixels[liPixel]);
                    }

                    fAddMask(lsName, lbKeep);
                });
            }

            fTry("flasks: mask", () => fAddMask("flasks: mask", UWHudArt.BuildFlaskMask(lOData.Flasks)));
            fTry("flasks: free mask", () => fAddMask("flasks: free mask", UWHudArt.BuildFreeFlaskMask(lOData.Flasks)));
            fTry("flasks: gem mask", () => fAddMask("flasks: gem mask", UWHudArt.BuildGemMask(lOData.PowerGem)));

            foreach (UWBackdropArt.ShapeEnum leShape in new[] { UWBackdropArt.ShapeEnum.Oval, UWBackdropArt.ShapeEnum.Rect })
                foreach (UWBackdropArt.ColourEnum leColour in Enum.GetValues(typeof(UWBackdropArt.ColourEnum)))
                    foreach (UWBackdropArt.PatternEnum lePattern in Enum.GetValues(typeof(UWBackdropArt.PatternEnum)))
                        foreach (bool lbBorder in new[] { false, true })
                        {
                            UWBackdropArt.ShapeEnum leShapeAt = leShape;
                            UWBackdropArt.ColourEnum leColourAt = leColour;
                            UWBackdropArt.PatternEnum lePatternAt = lePattern;
                            bool lbBorderAt = lbBorder;
                            string lsName = string.Format("backdrop: {0} {1} {2}{3}", leShapeAt, leColourAt, lePatternAt, lbBorderAt ? " border" : "");

                            fTry(lsName, () => fAdd(lsName, UWBackdropArt.Build(lOTextures, 100, 60, leShapeAt, leColourAt, lePatternAt, lbBorderAt)));
                        }

            fTry("backdrop: none", () => fAdd("backdrop: none", UWBackdropArt.Build(lOTextures, 100, 60, UWBackdropArt.ShapeEnum.None,
                UWBackdropArt.ColourEnum.Leather, UWBackdropArt.PatternEnum.Clouds, false)));

            // The pure functions, as sequences.
            fTry("pure: classic wide shift", () =>
            {
                List<int> liShifts = new List<int>();

                foreach (int liExtra in new[] { 0, 63, 480 })
                    for (int liY = 0; liY < 200; liY += 7)
                        for (int liX = 0; liX < 320; liX += 3)
                            liShifts.Add(UWHudArt.ClassicWideShiftX(liX, liY, liExtra));

                lONow["pure: classic wide shift"] = string.Format("{0} {1:X8}", liShifts.Count, fCrc32(liShifts.ToArray()));
            });

            fTry("pure: page column to main", () =>
            {
                int[] liColumns = new int[UWHudArt.PaperdollPageWidth];

                for (int liAt = 0; liAt < liColumns.Length; liAt++)
                    liColumns[liAt] = UWHudArt.PageColumnToMain(liAt);

                lONow["pure: page column to main"] = string.Format("{0} {1:X8}", liColumns.Length, fCrc32(liColumns));
            });

            fTry("pure: compass disc arrows", () =>
            {
                List<int> liArrows = new List<int>();

                for (int liY = 0; liY < 37; liY++)
                    for (int liX = 0; liX < 65; liX++)
                        liArrows.Add(UWHudArt.CompassDiscArrowAt(liX, liY));

                lONow["pure: compass disc arrows"] = string.Format("{0} {1:X8}", liArrows.Count, fCrc32(liArrows.ToArray()));
            });

            fComparePins(pOContext, "hud-art-hashes.txt", "art", lONow, psName =>
            {
                int liAt = psName.IndexOf(':');

                return liAt > 0 ? psName.Substring(0, liAt) : psName;
            });
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
