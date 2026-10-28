using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UWDataImport.Audio;
using UWDataImport.Presentation;
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
    public static partial class SelfChecks
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
            string lsRoot = CheckContext.ProjectRoot;

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
        /// null). MotionSmoothing is the property MotionMode (the number; MotionSmooth until it became
        /// a choice of three, 2026-10-28); ShowsPadHints and SaveSuspended are no settings. Returns the differences, empty when none.
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
                string lsProperty = lODef.Key == "MotionSmoothing" ? "MotionMode" : lODef.Key;
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

                // The windows' theme and its ground read as their enums (the facade's properties, cleanup
                // phase 8d); the other InRangeOrFallback entries are the Off/Original/Smooth modes.
                // The motion mode reads as its number (Original, Smooth, Modern; the facade's MotionMode).
                case UWIntSettingDef lOInt when lOInt == UWUserSettingsTable.MotionSmoothing:
                    return lOInt.Read(pOData).ToString(CultureInfo.InvariantCulture);

                case UWIntSettingDef lOInt when lOInt == UWUserSettingsTable.WindowTheme:
                    return ((UWWindowThemeEnum)lOInt.Read(pOData)).ToString();

                case UWIntSettingDef lOInt when lOInt == UWUserSettingsTable.WindowBackground:
                    return ((UWWindowBackgroundEnum)lOInt.Read(pOData)).ToString();

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
}
