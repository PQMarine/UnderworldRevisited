using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Loads every settings.json fixture under Tools/Fixtures/settings, reads every public property
/// of UWUserSettings from it and compares the values with the recorded file next to the fixture
/// (the .expected.txt), and what JsonUtility writes back with the .written file (since cleanup
/// step 8.3, 2026-10-24). The oracle of the settings steps of the cleanup (Docs/CLEANUP-PLAN.md,
/// phase 0 and 8): a fixture written by an old version must read the same values after the
/// settings table replaces the hand-written properties, legacy quirks included (a mouse speed
/// of 0 reads as 1, an invalid frame count as 8).
///
/// Menu: UW / Check settings fixtures. Batch: -executeMethod UWSettingsFixtureCheck.Run with
/// UW_RECORD=1 in the environment to record instead of compare, and UW_SETTINGS_FIXTURES to
/// point at another folder. The game's own settings are untouched: the data is swapped in for
/// the check and put back afterwards.
/// </summary>
public static class UWSettingsFixtureCheck
{
    private const string DefaultFolder = "Tools/Fixtures/settings";

    [MenuItem("UW/Check settings fixtures")]
    public static void Run()
    {
        string lsFolder = Environment.GetEnvironmentVariable("UW_SETTINGS_FIXTURES");
        bool lbRecord = Environment.GetEnvironmentVariable("UW_RECORD") == "1";

        if (string.IsNullOrEmpty(lsFolder))
            lsFolder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DefaultFolder.Replace('/', Path.DirectorySeparatorChar));

        int liFailures = RunOn(lsFolder, lbRecord);

        if (Application.isBatchMode)
            EditorApplication.Exit(liFailures == 0 ? 0 : 1);
    }

    /// <summary>Returns the number of fixtures that differ (or could not be read).</summary>
    public static int RunOn(string psFolder, bool pbRecord)
    {
        if (!Directory.Exists(psFolder))
        {
            Debug.LogError("Settings fixtures: no folder " + psFolder);

            return 1;
        }

        // The data class is UWUserData since cleanup step 8.3 (2026-10-24), the facade's private
        // UWUserSettings.Data until then.
        Type lODataType = typeof(UWDataImport.UWData.UWUserData);
        FieldInfo lODataField = typeof(UWUserSettings).GetField("msData", BindingFlags.NonPublic | BindingFlags.Static);

        if (lODataField == null || lODataField.FieldType != lODataType)
        {
            Debug.LogError("Settings fixtures: UWUserSettings.msData (a UWUserData) not found - the check needs updating");

            return 1;
        }

        object lOBefore = lODataField.GetValue(null);
        int liFailures = 0;
        int liChecked = 0;

        try
        {
            foreach (string lsFixture in Directory.GetFiles(psFolder, "*.json"))
            {
                string lsExpected = Path.ChangeExtension(lsFixture, null) + ".expected.txt";
                string lsName = Path.GetFileName(lsFixture);
                object lOData;

                try
                {
                    lOData = JsonUtility.FromJson(File.ReadAllText(lsFixture), lODataType);
                }
                catch (Exception lOError)
                {
                    Debug.LogError("Settings fixture " + lsName + ": not read - " + lOError.Message);
                    liFailures++;

                    continue;
                }

                lODataField.SetValue(null, lOData);

                string lsValues = fDumpProperties();
                string lsWrittenPath = Path.ChangeExtension(lsFixture, null) + ".written";
                string lsWritten = JsonUtility.ToJson(lOData, true);

                liChecked++;

                if (pbRecord)
                {
                    File.WriteAllText(lsExpected, lsValues);
                    File.WriteAllText(lsWrittenPath, lsWritten);
                    Debug.Log("Settings fixture " + lsName + ": recorded " + Path.GetFileName(lsExpected) + " and " + Path.GetFileName(lsWrittenPath));

                    continue;
                }

                // What the game writes back after reading the fixture (cleanup step 8.3, 2026-10-24):
                // JsonUtility's own bytes, the file the engine-free self-check's settings codec has
                // to reproduce (SettingsTableChecks) - so the settings.json the game saves stays the
                // same byte for byte when the data class moves. Line ends aside: git may check the
                // recorded file out with CRLF, JsonUtility writes LF.
                if (!File.Exists(lsWrittenPath))
                {
                    Debug.LogError("Settings fixture " + lsName + ": no " + Path.GetFileName(lsWrittenPath) + " - record once with UW_RECORD=1");
                    liFailures++;

                    continue;
                }

                string lsRecordedWritten = File.ReadAllText(lsWrittenPath).Replace("\r\n", "\n");

                if (lsRecordedWritten != lsWritten)
                {
                    liFailures++;
                    Debug.LogError("Settings fixture " + lsName + ": JsonUtility writes other bytes than " + Path.GetFileName(lsWrittenPath)
                        + "\n" + fDiff(lsRecordedWritten, lsWritten));

                    continue;
                }

                if (!File.Exists(lsExpected))
                {
                    Debug.LogError("Settings fixture " + lsName + ": no " + Path.GetFileName(lsExpected) + " - record once with UW_RECORD=1");
                    liFailures++;

                    continue;
                }

                string lsRecorded = File.ReadAllText(lsExpected).Replace("\r\n", "\n");

                if (lsRecorded == lsValues)
                {
                    Debug.Log("Settings fixture " + lsName + ": every property as recorded");

                    continue;
                }

                liFailures++;
                Debug.LogError("Settings fixture " + lsName + ": differs from " + Path.GetFileName(lsExpected) + "\n" + fDiff(lsRecorded, lsValues));
            }
        }
        finally
        {
            lODataField.SetValue(null, lOBefore);
        }

        Debug.Log(string.Format("Settings fixtures: {0} checked, {1} failed", liChecked, liFailures));

        return liFailures;
    }

    /// <summary>Every public static property of UWUserSettings with a getter, name = value, sorted.</summary>
    private static string fDumpProperties()
    {
        SortedDictionary<string, string> lOValues = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (PropertyInfo lOProperty in typeof(UWUserSettings).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (!lOProperty.CanRead || lOProperty.GetIndexParameters().Length > 0)
                continue;

            string lsValue;

            try
            {
                lsValue = fFormat(lOProperty.GetValue(null));
            }
            catch (Exception lOError)
            {
                lsValue = "<threw " + (lOError.InnerException ?? lOError).GetType().Name + ">";
            }

            lOValues[lOProperty.Name] = lsValue;
        }

        StringBuilder lOText = new StringBuilder();

        foreach (KeyValuePair<string, string> lOEntry in lOValues)
            lOText.Append(lOEntry.Key).Append(" = ").Append(lOEntry.Value).Append('\n');

        return lOText.ToString();
    }

    private static string fFormat(object pOValue)
    {
        if (pOValue == null)
            return "null";

        if (pOValue is string lsText)
            return "\"" + lsText + "\"";

        if (pOValue is float lfValue)
            return lfValue.ToString("R", CultureInfo.InvariantCulture);

        if (pOValue is double ldValue)
            return ldValue.ToString("R", CultureInfo.InvariantCulture);

        if (pOValue is IEnumerable lOList && !(pOValue is string))
        {
            List<string> lOParts = new List<string>();

            foreach (object lOItem in lOList)
                lOParts.Add(fFormat(lOItem));

            return "[" + string.Join(", ", lOParts) + "]";
        }

        if (pOValue is IFormattable lOFormattable)
            return lOFormattable.ToString(null, CultureInfo.InvariantCulture);

        return pOValue.ToString();
    }

    private static string fDiff(string psRecorded, string psNow)
    {
        HashSet<string> lORecorded = new HashSet<string>(psRecorded.Split('\n'));
        HashSet<string> lONow = new HashSet<string>(psNow.Split('\n'));
        StringBuilder lOText = new StringBuilder();

        foreach (string lsLine in lORecorded)
            if (lsLine.Length > 0 && !lONow.Contains(lsLine))
                lOText.Append("  recorded: ").Append(lsLine).Append('\n');

        foreach (string lsLine in lONow)
            if (lsLine.Length > 0 && !lORecorded.Contains(lsLine))
                lOText.Append("  now:      ").Append(lsLine).Append('\n');

        return lOText.ToString();
    }
}
