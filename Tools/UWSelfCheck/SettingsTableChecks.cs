using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 8.3 (2026-10-24): settings.json as data (UWUserData) and a table
    /// (UWUserSettingsTable). Three parts, all data-free:
    ///   - the table's shape: one entry per field of UWUserData in the file's order, each entry's
    ///     accessors on the field of its own key, the limits sane, the layout editor's snapshot set;
    ///   - every reading rule (the clamps and the legacy quirks) on crafted values, and that reading
    ///     never writes a value back;
    ///   - the fixtures of Tools/Fixtures/settings: each .json read through the table must read the
    ///     values the editor oracle recorded (.expected.txt, UWSettingsFixtureCheck), and written
    ///     again must give JsonUtility's bytes (.written) - so the file the game saves stays the
    ///     same byte for byte. The JSON reader and writer below mirror JsonUtility as far as these
    ///     files go (pretty print, four spaces, floats as their double's shortest form without an
    ///     exponent, non-ASCII letters as they are, a missing text as "").
    /// </summary>
    public static partial class Program
    {
        private const string SettingsFixturesFolder = "Tools/Fixtures/settings";

        /// <summary>The layout editor's snapshot (UWUserSettings.TakeLayoutSnapshot) as the list of
        /// names it was until step 8.3 - the plan's "24-key check"; 25 since ActionBarHidden
        /// (2026-10-10). The table's Layout entries must be exactly these.</summary>
        private static readonly string[] msSnapshotKeys =
        {
            "ModernLayout", "ModernBacks", "ModernUiPercent", "ModernCompass", "ModernCompassDisc", "ModernCompassOutline",
            "ModernScroll", "ModernScrollLines", "ModernMessageLines", "ModernGemApart", "ModernFlasksApart", "ModernStoneShelf", "ModernStoneShelfWidth",
            "ModernRuneHollow", "ModernRuneHollowOutline", "ModernRuneTablet", "ModernStatsPanel", "ModernCharacterPage",
            "ModernConversationOriginal", "ModernConversationNoOutline", "ConversationTextPercent", "MinimapHidden", "MinimapTurns",
            "ActionBarHidden", "InterfaceFont"
        };

        private static void fCheckSettingsTable()
        {
            Check.Group("Settings table (UWUserData, UWUserSettingsTable - cleanup step 8.3)");

            fCheckSettingsTableShape();
            fCheckSettingsReadingRules();
            fCheckSettingsFixtures();
        }

        private static void fCheckSettingsTableShape()
        {
            FieldInfo[] lOFields = typeof(UWUserData).GetFields(BindingFlags.Public | BindingFlags.Instance);

            Array.Sort(lOFields, (pOLeft, pORight) => pOLeft.MetadataToken.CompareTo(pORight.MetadataToken));

            UWSettingDef[] lODefs = UWUserSettingsTable.All;

            Check.Int("table: one entry per field of UWUserData", lODefs.Length, lOFields.Length);

            StringBuilder lOOrder = new StringBuilder();
            StringBuilder lOAccess = new StringBuilder();
            HashSet<string> lOKeys = new HashSet<string>();

            for (int liAt = 0; liAt < lODefs.Length && liAt < lOFields.Length; liAt++)
            {
                UWSettingDef lODef = lODefs[liAt];
                FieldInfo lOField = lOFields[liAt];

                if (lODef.Key != lOField.Name)
                    lOOrder.Append(liAt).Append(": ").Append(lODef.Key).Append(" vs field ").Append(lOField.Name).Append("; ");

                if (!lOKeys.Add(lODef.Key))
                    lOOrder.Append("twice: ").Append(lODef.Key).Append("; ");

                // The accessors must reach the field of the entry's own key, both ways (a switch
                // both on and off, so that another switch's default cannot pass for it).
                foreach (object lOMarker in fSettingsMarkers(lOField.FieldType, liAt))
                {
                    UWUserData lOData = new UWUserData();

                    lOField.SetValue(lOData, lOMarker);

                    if (!Equals(lODef.RawOf(lOData), lOMarker))
                        lOAccess.Append(lODef.Key).Append(" reads another field; ");

                    UWUserData lOWritten = new UWUserData();

                    lODef.SetRaw(lOWritten, lOMarker);

                    if (!Equals(lOField.GetValue(lOWritten), lOMarker))
                        lOAccess.Append(lODef.Key).Append(" writes another field; ");
                }

                if (!Equals(lODef.Default, lOField.GetValue(new UWUserData())) && !(lOField.FieldType == typeof(List<string>)))
                    lOAccess.Append(lODef.Key).Append(" default differs; ");

                if (UWUserSettingsTable.Find(lODef.Key) != lODef)
                    lOAccess.Append(lODef.Key).Append(" not found by key; ");
            }

            Check.Str("table: the keys are the field names, in the file's order", lOOrder.ToString(), string.Empty);
            Check.Str("table: each entry reads and writes its own field", lOAccess.ToString(), string.Empty);

            // The limits: a fallback a rule reads must itself lie within the limits, so that the old
            // property's "fallback, unclamped" and the table's "fallback, clamped" read the same.
            StringBuilder lOLimits = new StringBuilder();

            foreach (UWSettingDef lODef in lODefs)
            {
                if (lODef is UWFloatSettingDef lOFloat && lOFloat.Normalize != UWSettingNormalize.None)
                {
                    if (!(lOFloat.Min <= lOFloat.Max))
                        lOLimits.Append(lODef.Key).Append(" min above max; ");

                    if (lOFloat.Normalize == UWSettingNormalize.NotPositiveIsFallback && (lOFloat.Fallback < lOFloat.Min || lOFloat.Fallback > lOFloat.Max))
                        lOLimits.Append(lODef.Key).Append(" fallback outside; ");
                }

                if (lODef is UWIntSettingDef lOInt)
                {
                    if (lOInt.Normalize == UWSettingNormalize.OneOfOrFallback && Array.IndexOf(lOInt.Choices, lOInt.Fallback) < 0)
                        lOLimits.Append(lODef.Key).Append(" fallback no choice; ");

                    if (lOInt.Normalize == UWSettingNormalize.InRangeOrFallback && (lOInt.Fallback < lOInt.Min || lOInt.Fallback > lOInt.Max))
                        lOLimits.Append(lODef.Key).Append(" fallback outside; ");

                    if (lOInt.Normalize == UWSettingNormalize.UnsetIsComputed && lOInt.WhenUnset == null)
                        lOLimits.Append(lODef.Key).Append(" nothing computes the unset value; ");
                }
            }

            Check.Str("table: fallbacks within the limits", lOLimits.ToString(), string.Empty);

            List<string> lOLayout = new List<string>();

            foreach (UWSettingDef lODef in UWUserSettingsTable.InStore(UWSettingStore.Layout))
                lOLayout.Add(lODef.Key);

            List<string> lOExpected = new List<string>(msSnapshotKeys);

            lOLayout.Sort(StringComparer.Ordinal);
            lOExpected.Sort(StringComparer.Ordinal);

            Check.Str("table: the layout snapshot takes the 25 layout settings",
                string.Join(",", lOLayout), string.Join(",", lOExpected));
            Check.Int("table: nothing in graphics.json or the asset yet",
                UWUserSettingsTable.InStore(UWSettingStore.Graphics).Length + UWUserSettingsTable.InStore(UWSettingStore.Live).Length, 0);
        }

        /// <summary>Values of the field's type that no default has, different per field; for a
        /// switch both on and off.</summary>
        private static object[] fSettingsMarkers(Type pOType, int piAt)
        {
            if (pOType == typeof(bool))
                return new object[] { true, false };

            if (pOType == typeof(int))
                return new object[] { 7000 + piAt };

            if (pOType == typeof(float))
                return new object[] { 7000.5f + piAt };

            if (pOType == typeof(string))
                return new object[] { "marker " + piAt };

            return new object[] { new List<string> { "marker " + piAt } };
        }

        private static void fCheckSettingsReadingRules()
        {
            UWUserData lOData = new UWUserData();

            // Mouse and stick speed: 0 or below was never written by an older build - reads 1.
            lOData.MouseLookSpeed = 0f;
            fSettingsFloat("mouse speed 0 reads 1", UWUserSettingsTable.MouseLookSpeed.Read(lOData), 1f);
            lOData.MouseLookSpeed = -2f;
            fSettingsFloat("mouse speed -2 reads 1", UWUserSettingsTable.MouseLookSpeed.Read(lOData), 1f);
            fSettingsFloat("reading leaves the stored -2 alone", lOData.MouseLookSpeed, -2f);
            lOData.MouseLookSpeed = 0.01f;
            fSettingsFloat("mouse speed 0.01 reads the minimum", UWUserSettingsTable.MouseLookSpeed.Read(lOData), 0.05f);
            lOData.MouseLookSpeed = 3f;
            fSettingsFloat("mouse speed 3 reads the maximum", UWUserSettingsTable.MouseLookSpeed.Read(lOData), 2f);
            UWUserSettingsTable.MouseLookSpeed.Write(lOData, 9f);
            fSettingsFloat("mouse speed 9 is stored as 2", lOData.MouseLookSpeed, 2f);
            UWUserSettingsTable.MouseLookSpeed.Write(lOData, 0f);
            fSettingsFloat("mouse speed 0 is stored as the minimum", lOData.MouseLookSpeed, 0.05f);

            lOData.StickLookSpeed = 0f;
            fSettingsFloat("stick speed 0 reads 1", UWUserSettingsTable.StickLookSpeed.Read(lOData), 1f);
            lOData.StickLookSpeed = 0.1f;
            fSettingsFloat("stick speed 0.1 reads the minimum", UWUserSettingsTable.StickLookSpeed.Read(lOData), 0.25f);

            lOData.EasyMovementInterval = 0f;
            fSettingsFloat("easy interval 0 reads the default", UWUserSettingsTable.EasyMovementInterval.Read(lOData), UWEasyMovement.DefaultRepeatSeconds);
            lOData.EasyMovementInterval = 0.05f;
            fSettingsFloat("easy interval 0.05 reads the minimum", UWUserSettingsTable.EasyMovementInterval.Read(lOData), UWEasyMovement.MinRepeatSeconds);
            lOData.EasyMovementInterval = 0.9f;
            fSettingsFloat("easy interval 0.9 reads the maximum", UWUserSettingsTable.EasyMovementInterval.Read(lOData), UWEasyMovement.MaxRepeatSeconds);

            lOData.LeftStickDeadzone = -0.5f;
            fSettingsFloat("deadzone below 0 reads 0", UWUserSettingsTable.LeftStickDeadzone.Read(lOData), 0f);
            lOData.RightStickDeadzone = 2f;
            fSettingsFloat("deadzone 2 reads half the deflection", UWUserSettingsTable.RightStickDeadzone.Read(lOData), 0.5f);

            lOData.HeadBobStrength = 0f;
            fSettingsFloat("bob strength 0 reads 0.1", UWUserSettingsTable.HeadBobStrength.Read(lOData), 0.1f);
            lOData.SwimViewStrength = 3f;
            fSettingsFloat("swim strength 3 reads 1", UWUserSettingsTable.SwimViewStrength.Read(lOData), 1f);

            lOData.MusicVolume = 1.7f;
            fSettingsFloat("music volume 1.7 reads 1", UWUserSettingsTable.MusicVolume.Read(lOData), 1f);
            lOData.SoundVolume = -0.2f;
            fSettingsFloat("sound volume -0.2 reads 0", UWUserSettingsTable.SoundVolume.Read(lOData), 0f);

            lOData.MusicDevice = 42;
            Check.Int("settings: music device 42 reads the MT-32", UWUserSettingsTable.MusicDevice.Read(lOData), (int)UWMusicDeviceId.Mt32);
            UWUserSettingsTable.MusicDevice.Write(lOData, -4);
            Check.Int("settings: music device -4 is stored as the AdLib", lOData.MusicDevice, (int)UWMusicDeviceId.AdLib);

            lOData.OriginalFrameTicks = 5;
            Check.Int("settings: frame ticks 5 read 8", UWUserSettingsTable.OriginalFrameTicks.Read(lOData), 8);
            lOData.OriginalFrameTicks = 12;
            Check.Int("settings: frame ticks 12 read 12", UWUserSettingsTable.OriginalFrameTicks.Read(lOData), 12);
            UWUserSettingsTable.OriginalFrameTicks.Write(lOData, 5);
            Check.Int("settings: frame ticks are stored as given", lOData.OriginalFrameTicks, 5);
            lOData.SmoothRampTicks = 0;
            Check.Int("settings: ramp 0 (at once) is a choice", UWUserSettingsTable.SmoothRampTicks.Read(lOData), 0);
            lOData.SmoothRampTicks = 3;
            Check.Int("settings: ramp 3 reads 8", UWUserSettingsTable.SmoothRampTicks.Read(lOData), 8);

            lOData.HeadBobMode = 9;
            Check.Int("settings: head bob 9 reads Original", UWUserSettingsTable.HeadBobMode.Read(lOData), (int)UWHeadBobRules.ModeEnum.Original);
            lOData.WeaponJitterMode = -1;
            Check.Int("settings: weapon jitter -1 reads Original", UWUserSettingsTable.WeaponJitterMode.Read(lOData), (int)UWHeadBobRules.ModeEnum.Original);
            lOData.SwimViewMode = 0;
            Check.Int("settings: swim view Off reads Off", UWUserSettingsTable.SwimViewMode.Read(lOData), (int)UWHeadBobRules.ModeEnum.Off);

            UWUserData lOPreset = new UWUserData();

            Check.Int("settings: preset -1 without an own layout reads Modern", UWUserSettingsTable.InterfacePreset.Read(lOPreset), 0);
            lOPreset.ModernLayout = "0:0.5:0:12:0:100;";
            Check.Int("settings: preset -1 with an own layout reads Custom", UWUserSettingsTable.InterfacePreset.Read(lOPreset), 1);
            lOPreset.ModernLayout = string.Empty;
            lOPreset.MinimapHidden = true;
            Check.Int("settings: preset -1 with the minimap hidden reads Custom", UWUserSettingsTable.InterfacePreset.Read(lOPreset), 1);
            lOPreset.InterfacePreset = 2;
            Check.Int("settings: a chosen preset reads as it is", UWUserSettingsTable.InterfacePreset.Read(lOPreset), 2);

            Check.Int("settings: control scheme -1 (not chosen) reads as it is", UWUserSettingsTable.ControlScheme.Read(new UWUserData()), -1);
            Check.Int("settings: display mode -1 (not chosen) reads as it is", UWUserSettingsTable.DisplayMode.Read(new UWUserData()), -1);

            lOData.MotionSmoothing = 5;
            Check.Bool("settings: motion smoothing 5 reads Smooth", UWUserSettingsTable.MotionSmoothing.ReadOn(lOData), true);
            UWUserSettingsTable.MotionSmoothing.WriteOn(lOData, false);
            Check.Int("settings: Original is stored as 0", lOData.MotionSmoothing, 0);
            UWUserSettingsTable.MotionViewStepped.WriteOn(lOData, true);
            Check.Int("settings: stepped is stored as 1", lOData.MotionViewStepped, 1);

            lOData.Mt32RomPath = null;
            Check.Str("settings: no ROM folder reads empty", UWUserSettingsTable.Mt32RomPath.Read(lOData), string.Empty);
            UWUserSettingsTable.GogInstallPath.Write(lOData, null);
            Check.Str("settings: no game folder is stored empty", lOData.GogInstallPath, string.Empty);
            Check.Bool("settings: the layout strings keep null", UWUserSettingsTable.ModernLayout.Read(new UWUserData()) == null, true);

            lOData.KeyBindings = null;

            List<string> lOBindings = UWUserSettingsTable.KeyBindings.Read(lOData);

            Check.Bool("settings: no key list reads a new empty one, which stays",
                lOBindings != null && lOBindings.Count == 0 && ReferenceEquals(lOData.KeyBindings, lOBindings), true);
        }

        private static void fSettingsFloat(string psName, float pfActual, float pfExpected)
        {
            Check.Str("settings: " + psName, pfActual.ToString("R", CultureInfo.InvariantCulture), pfExpected.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void fCheckSettingsFixtures()
        {
            string lsRoot = UWRuleIndexScan.FindProjectRoot();

            if (lsRoot == null)
            {
                Check.Pass("settings fixtures", "outside the source tree, skipped");

                return;
            }

            string lsFolder = Path.Combine(lsRoot, SettingsFixturesFolder.Replace('/', Path.DirectorySeparatorChar));
            string[] lsFixtures = Directory.Exists(lsFolder) ? Directory.GetFiles(lsFolder, "*.json") : new string[0];

            Array.Sort(lsFixtures, StringComparer.Ordinal);

            Check.Bool("settings fixtures: found", lsFixtures.Length >= 5, true);

            foreach (string lsFixture in lsFixtures)
            {
                string lsName = Path.GetFileNameWithoutExtension(lsFixture);
                string lsWrittenPath = Path.Combine(lsFolder, lsName + ".written");
                string lsExpectedPath = Path.Combine(lsFolder, lsName + ".expected.txt");
                UWUserData lOData;

                try
                {
                    lOData = UWSettingsJson.Read(File.ReadAllText(lsFixture));
                }
                catch (Exception lOError)
                {
                    Check.Fail("settings fixture " + lsName + ": reads", lOError.Message);

                    continue;
                }

                if (File.Exists(lsWrittenPath))
                {
                    string lsRecorded = File.ReadAllText(lsWrittenPath).Replace("\r\n", "\n");
                    string lsWritten = UWSettingsJson.Write(lOData);

                    Check.Str("settings fixture " + lsName + ": written as JsonUtility writes it", fFirstDifference(lsWritten, lsRecorded), string.Empty);
                    Check.Str("settings fixture " + lsName + ": the written file reads and writes again the same",
                        fFirstDifference(UWSettingsJson.Write(UWSettingsJson.Read(lsRecorded)), lsRecorded), string.Empty);
                }
                else
                {
                    Check.Fail("settings fixture " + lsName + ": written", "no " + lsName + ".written - record with UWSettingsFixtureCheck");
                }

                if (File.Exists(lsExpectedPath))
                    Check.Str("settings fixture " + lsName + ": reads what the editor oracle recorded", fSettingsAgainstOracle(lOData, lsExpectedPath), string.Empty);
                else
                    Check.Fail("settings fixture " + lsName + ": expected", "no " + lsName + ".expected.txt");
            }
        }

        /// <summary>The first line where two texts part, or empty when they are the same.</summary>
        private static string fFirstDifference(string psActual, string psExpected)
        {
            if (psActual == psExpected)
                return string.Empty;

            string[] lsActual = psActual.Split('\n');
            string[] lsExpected = psExpected.Split('\n');

            for (int liAt = 0; liAt < Math.Max(lsActual.Length, lsExpected.Length); liAt++)
            {
                string lsLeft = liAt < lsActual.Length ? lsActual[liAt] : "<end>";
                string lsRight = liAt < lsExpected.Length ? lsExpected[liAt] : "<end>";

                if (lsLeft != lsRight)
                    return "line " + (liAt + 1) + ": got " + lsLeft + ", expected " + lsRight;
            }

            return "differs (line ends?)";
        }

        /// <summary>
        /// Compares every table entry's read value with the line UWSettingsFixtureCheck recorded for
        /// the UWUserSettings property of the same name (formatted as it formats them: bools as
        /// True/False, floats in their shortest form, the three modes by name, texts quoted, null as
        /// null). MotionSmoothing is the property MotionSmooth; ShowsPadHints and SaveSuspended are
        /// no settings. Returns the differences, empty when none.
        /// </summary>
        private static string fSettingsAgainstOracle(UWUserData pOData, string psExpectedPath)
        {
            Dictionary<string, string> lORecorded = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string lsLine in File.ReadAllLines(psExpectedPath))
            {
                int liEquals = lsLine.IndexOf(" = ", StringComparison.Ordinal);

                if (liEquals > 0)
                    lORecorded[lsLine.Substring(0, liEquals)] = lsLine.Substring(liEquals + 3);
            }

            StringBuilder lODifferences = new StringBuilder();
            int liCompared = 0;

            foreach (UWSettingDef lODef in UWUserSettingsTable.All)
            {
                string lsProperty = lODef.Key == "MotionSmoothing" ? "MotionSmooth" : lODef.Key;
                string lsRead = fSettingsOracleFormat(lODef, pOData);
                string lsRecorded;

                if (!lORecorded.TryGetValue(lsProperty, out lsRecorded))
                {
                    lODifferences.Append(lsProperty).Append(" not recorded; ");

                    continue;
                }

                liCompared++;

                if (lsRead != lsRecorded)
                    lODifferences.Append(lsProperty).Append(" reads ").Append(lsRead).Append(", recorded ").Append(lsRecorded).Append("; ");
            }

            if (liCompared != UWUserSettingsTable.All.Length)
                lODifferences.Append("compared ").Append(liCompared).Append(" of ").Append(UWUserSettingsTable.All.Length);

            return lODifferences.ToString();
        }

        private static string fSettingsOracleFormat(UWSettingDef pODef, UWUserData pOData)
        {
            switch (pODef)
            {
                case UWBoolSettingDef lOBool:
                    return lOBool.Read(pOData) ? "True" : "False";

                case UWFloatSettingDef lOFloat:
                    return lOFloat.Read(pOData).ToString("R", CultureInfo.InvariantCulture);

                case UWIntSettingDef lOInt when lOInt.Normalize == UWSettingNormalize.NonZeroIsOn:
                    return lOInt.ReadOn(pOData) ? "True" : "False";

                case UWIntSettingDef lOInt when lOInt.Normalize == UWSettingNormalize.InRangeOrFallback:
                    return ((UWHeadBobRules.ModeEnum)lOInt.Read(pOData)).ToString();

                case UWIntSettingDef lOInt:
                    return lOInt.Read(pOData).ToString(CultureInfo.InvariantCulture);

                case UWTextSettingDef lOText:
                    string lsText = lOText.Read(pOData);

                    return lsText == null ? "null" : "\"" + lsText + "\"";

                case UWListSettingDef lOList:
                    List<string> lsParts = new List<string>();

                    foreach (string lsItem in lOList.Read(pOData))
                        lsParts.Add("\"" + lsItem + "\"");

                    return "[" + string.Join(", ", lsParts) + "]";

                default:
                    return "?";
            }
        }
    }

    /// <summary>
    /// settings.json as JsonUtility reads and writes it, without Unity - for the self-check only
    /// (the game itself uses JsonUtility). Reading: keys map to the table's entries by name,
    /// unknown keys are skipped, a missing key keeps UWUserData's initializer. Writing: every
    /// entry in the table's order, pretty printed with four spaces, no newline at the end, floats
    /// as the shortest form of their double without an exponent and with ".0" when whole, texts
    /// with \" and \\ escaped and other letters as they are, a missing text as "", an empty list
    /// as []. Control characters in a text are escaped as JSON has them; no fixture has one, so
    /// that part is not tied to JsonUtility.
    /// </summary>
    internal static class UWSettingsJson
    {
        public static UWUserData Read(string psJson)
        {
            int liAt = 0;
            object lOValue = fParseValue(psJson, ref liAt);

            if (!(lOValue is List<KeyValuePair<string, object>> lOObject))
                throw new FormatException("not a JSON object");

            UWUserData lOData = new UWUserData();

            foreach (KeyValuePair<string, object> lOPair in lOObject)
            {
                UWSettingDef lODef = UWUserSettingsTable.Find(lOPair.Key);

                if (lODef == null)
                    continue;

                switch (lODef)
                {
                    case UWBoolSettingDef _:
                        lODef.SetRaw(lOData, (bool)lOPair.Value);
                        break;

                    case UWIntSettingDef _:
                        lODef.SetRaw(lOData, int.Parse((string)((NumberToken)lOPair.Value).Text, CultureInfo.InvariantCulture));
                        break;

                    case UWFloatSettingDef _:
                        lODef.SetRaw(lOData, float.Parse(((NumberToken)lOPair.Value).Text, CultureInfo.InvariantCulture));
                        break;

                    case UWTextSettingDef _:
                        lODef.SetRaw(lOData, lOPair.Value as string ?? string.Empty);
                        break;

                    case UWListSettingDef _:
                        List<string> lsItems = new List<string>();

                        foreach (object lOItem in (List<object>)lOPair.Value)
                            lsItems.Add((string)lOItem);

                        lODef.SetRaw(lOData, lsItems);
                        break;
                }
            }

            return lOData;
        }

        public static string Write(UWUserData pOData)
        {
            StringBuilder lOText = new StringBuilder();

            lOText.Append("{\n");

            UWSettingDef[] lODefs = UWUserSettingsTable.All;

            for (int liAt = 0; liAt < lODefs.Length; liAt++)
            {
                lOText.Append("    \"").Append(lODefs[liAt].Key).Append("\": ");
                fWriteValue(lOText, lODefs[liAt].RawOf(pOData), lODefs[liAt]);

                if (liAt < lODefs.Length - 1)
                    lOText.Append(',');

                lOText.Append('\n');
            }

            lOText.Append('}');

            return lOText.ToString();
        }

        private static void fWriteValue(StringBuilder pOText, object pOValue, UWSettingDef pODef)
        {
            switch (pODef)
            {
                case UWBoolSettingDef _:
                    pOText.Append((bool)pOValue ? "true" : "false");
                    break;

                case UWIntSettingDef _:
                    pOText.Append(((int)pOValue).ToString(CultureInfo.InvariantCulture));
                    break;

                case UWFloatSettingDef _:
                    pOText.Append(FormatFloat((float)pOValue));
                    break;

                case UWTextSettingDef _:
                    fWriteString(pOText, (string)pOValue ?? string.Empty);
                    break;

                case UWListSettingDef _:
                    List<string> lsItems = (List<string>)pOValue;

                    if (lsItems == null || lsItems.Count == 0)
                    {
                        pOText.Append("[]");
                        break;
                    }

                    pOText.Append("[\n");

                    for (int liAt = 0; liAt < lsItems.Count; liAt++)
                    {
                        pOText.Append("        ");
                        fWriteString(pOText, lsItems[liAt] ?? string.Empty);

                        if (liAt < lsItems.Count - 1)
                            pOText.Append(',');

                        pOText.Append('\n');
                    }

                    pOText.Append("    ]");
                    break;
            }
        }

        /// <summary>A float as JsonUtility writes it: its double in the shortest form that reads
        /// back, never with an exponent (1E-05f is 0.000009999999747378752), ".0" when whole.</summary>
        public static string FormatFloat(float pfValue)
        {
            string lsText = ((double)pfValue).ToString("R", CultureInfo.InvariantCulture);
            int liExponent = lsText.IndexOfAny(new[] { 'E', 'e' });

            if (liExponent >= 0)
            {
                bool lbNegative = lsText.StartsWith("-", StringComparison.Ordinal);
                string lsMantissa = lsText.Substring(lbNegative ? 1 : 0, liExponent - (lbNegative ? 1 : 0));
                int liPower = int.Parse(lsText.Substring(liExponent + 1), CultureInfo.InvariantCulture);
                int liPoint = lsMantissa.IndexOf('.');
                string lsDigits = lsMantissa.Replace(".", string.Empty);
                int liPointAt = (liPoint < 0 ? lsMantissa.Length : liPoint) + liPower;

                if (liPointAt <= 0)
                    lsText = "0." + new string('0', -liPointAt) + lsDigits;
                else if (liPointAt >= lsDigits.Length)
                    lsText = lsDigits + new string('0', liPointAt - lsDigits.Length);
                else
                    lsText = lsDigits.Substring(0, liPointAt) + "." + lsDigits.Substring(liPointAt);

                if (lbNegative)
                    lsText = "-" + lsText;
            }

            if (lsText.IndexOf('.') < 0 && lsText.IndexOf('N') < 0 && lsText.IndexOf('I') < 0)
                lsText += ".0";

            return lsText;
        }

        private static void fWriteString(StringBuilder pOText, string psValue)
        {
            pOText.Append('"');

            foreach (char lcChar in psValue)
            {
                switch (lcChar)
                {
                    case '"': pOText.Append("\\\""); break;
                    case '\\': pOText.Append("\\\\"); break;
                    case '\n': pOText.Append("\\n"); break;
                    case '\r': pOText.Append("\\r"); break;
                    case '\t': pOText.Append("\\t"); break;
                    case '\b': pOText.Append("\\b"); break;
                    case '\f': pOText.Append("\\f"); break;

                    default:
                        if (lcChar < ' ')
                            pOText.Append("\\u").Append(((int)lcChar).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            pOText.Append(lcChar);

                        break;
                }
            }

            pOText.Append('"');
        }

        /// <summary>A number as it stood in the file, read by the field's type.</summary>
        private sealed class NumberToken
        {
            public string Text;
        }

        private static object fParseValue(string psJson, ref int piAt)
        {
            fSkipSpace(psJson, ref piAt);

            if (piAt >= psJson.Length)
                throw new FormatException("unexpected end");

            char lcChar = psJson[piAt];

            if (lcChar == '{')
            {
                List<KeyValuePair<string, object>> lOPairs = new List<KeyValuePair<string, object>>();

                piAt++;
                fSkipSpace(psJson, ref piAt);

                if (psJson[piAt] == '}')
                {
                    piAt++;

                    return lOPairs;
                }

                while (true)
                {
                    fSkipSpace(psJson, ref piAt);

                    string lsKey = fParseString(psJson, ref piAt);

                    fSkipSpace(psJson, ref piAt);
                    fExpect(psJson, ref piAt, ':');

                    object lOValue = fParseValue(psJson, ref piAt);

                    lOPairs.Add(new KeyValuePair<string, object>(lsKey, lOValue));
                    fSkipSpace(psJson, ref piAt);

                    if (psJson[piAt] == ',')
                    {
                        piAt++;
                        continue;
                    }

                    fExpect(psJson, ref piAt, '}');

                    return lOPairs;
                }
            }

            if (lcChar == '[')
            {
                List<object> lOItems = new List<object>();

                piAt++;
                fSkipSpace(psJson, ref piAt);

                if (psJson[piAt] == ']')
                {
                    piAt++;

                    return lOItems;
                }

                while (true)
                {
                    lOItems.Add(fParseValue(psJson, ref piAt));
                    fSkipSpace(psJson, ref piAt);

                    if (psJson[piAt] == ',')
                    {
                        piAt++;
                        continue;
                    }

                    fExpect(psJson, ref piAt, ']');

                    return lOItems;
                }
            }

            if (lcChar == '"')
                return fParseString(psJson, ref piAt);

            if (string.CompareOrdinal(psJson, piAt, "true", 0, 4) == 0)
            {
                piAt += 4;

                return true;
            }

            if (string.CompareOrdinal(psJson, piAt, "false", 0, 5) == 0)
            {
                piAt += 5;

                return false;
            }

            if (string.CompareOrdinal(psJson, piAt, "null", 0, 4) == 0)
            {
                piAt += 4;

                return null;
            }

            int liStart = piAt;

            while (piAt < psJson.Length && "+-0123456789.eE".IndexOf(psJson[piAt]) >= 0)
                piAt++;

            if (piAt == liStart)
                throw new FormatException("unexpected '" + lcChar + "' at " + piAt);

            return new NumberToken { Text = psJson.Substring(liStart, piAt - liStart) };
        }

        private static string fParseString(string psJson, ref int piAt)
        {
            fExpect(psJson, ref piAt, '"');

            StringBuilder lOText = new StringBuilder();

            while (psJson[piAt] != '"')
            {
                char lcChar = psJson[piAt++];

                if (lcChar != '\\')
                {
                    lOText.Append(lcChar);
                    continue;
                }

                char lcEscape = psJson[piAt++];

                switch (lcEscape)
                {
                    case 'n': lOText.Append('\n'); break;
                    case 'r': lOText.Append('\r'); break;
                    case 't': lOText.Append('\t'); break;
                    case 'b': lOText.Append('\b'); break;
                    case 'f': lOText.Append('\f'); break;

                    case 'u':
                        lOText.Append((char)int.Parse(psJson.Substring(piAt, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        piAt += 4;
                        break;

                    default:
                        lOText.Append(lcEscape);
                        break;
                }
            }

            piAt++;

            return lOText.ToString();
        }

        private static void fSkipSpace(string psJson, ref int piAt)
        {
            while (piAt < psJson.Length && char.IsWhiteSpace(psJson[piAt]))
                piAt++;
        }

        private static void fExpect(string psJson, ref int piAt, char pcWanted)
        {
            if (piAt >= psJson.Length || psJson[piAt] != pcWanted)
                throw new FormatException("expected '" + pcWanted + "' at " + piAt);

            piAt++;
        }
    }
}
