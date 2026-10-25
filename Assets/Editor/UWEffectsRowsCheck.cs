using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE REMASTERED EFFECTS' ROWS, RECORDED (cleanup 8d.3, 2026-10-25): the oracle of the effects
/// screen's move off its 320x200 picture onto the kit. UWEffectsScreen.Describe writes every row's
/// label, kind and range, the text it shows and what a change leaves in the effect values and the
/// grime's two settings - set along the bar, a switch pressed twice, a choice row stepped round - and
/// ALL ON and ALL OFF. Recorded once from the picture's own click and bar code; the kit window's rows
/// must give the same record.
///
/// Menu: UW / Check effects rows. Batch: -executeMethod UWEffectsRowsCheck.Run; UW_RECORD=1 records
/// Tools/Fixtures/effects/rows.expected.txt instead of comparing, UW_EFFECTS_ROWS_OUT writes the dump
/// to that file as well. The game's own settings are untouched: the settings data is swapped in for
/// the check (as UWBindingNamesCheck does) and put back afterwards; nothing is saved. The effect
/// values start from a fixed set of the check's own, not from the settings asset.
/// </summary>
public static class UWEffectsRowsCheck
{
    private const string ExpectedFile = "Tools/Fixtures/effects/rows.expected.txt";

    [MenuItem("UW/Check effects rows")]
    public static void Run()
    {
        bool lbRecord = Environment.GetEnvironmentVariable("UW_RECORD") == "1";
        string lsOut = Environment.GetEnvironmentVariable("UW_EFFECTS_ROWS_OUT");
        string lsExpected = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ExpectedFile.Replace('/', Path.DirectorySeparatorChar));
        int liFailures = 0;
        string lsDump;

        try
        {
            lsDump = Dump();
        }
        catch (Exception lOError)
        {
            Debug.LogError("Effects rows: the dump threw - " + lOError);
            lsDump = null;
            liFailures++;
        }

        if (lsDump != null)
        {
            if (!string.IsNullOrEmpty(lsOut))
                File.WriteAllText(lsOut, lsDump);

            if (lbRecord)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(lsExpected));
                File.WriteAllText(lsExpected, lsDump);
                Debug.Log("Effects rows: recorded " + ExpectedFile);
            }
            else if (!File.Exists(lsExpected))
            {
                Debug.LogError("Effects rows: no " + ExpectedFile + " - record once with UW_RECORD=1");
                liFailures++;
            }
            else
            {
                string lsRecorded = File.ReadAllText(lsExpected).Replace("\r\n", "\n");

                if (lsRecorded == lsDump)
                    Debug.Log("Effects rows: every row as recorded");
                else
                {
                    liFailures++;
                    Debug.LogError("Effects rows: differ from " + ExpectedFile + "\n" + fDiff(lsRecorded, lsDump));
                }
            }
        }

        if (Application.isBatchMode)
            EditorApplication.Exit(liFailures == 0 ? 0 : 1);
    }

    /// <summary>The record, in English, on fresh settings data and the check's own effect values.</summary>
    public static string Dump()
    {
        FieldInfo lODataField = typeof(UWUserSettings).GetField("msData", BindingFlags.NonPublic | BindingFlags.Static);

        if (lODataField == null || lODataField.FieldType != typeof(UWUserData))
            throw new InvalidOperationException("UWUserSettings.msData (a UWUserData) not found - the check needs updating");

        object lOBefore = lODataField.GetValue(null);

        try
        {
            UWText.UseEnglish();
            lODataField.SetValue(null, new UWUserData());

            return UWEffectsScreen.Describe(fShipped());
        }
        finally
        {
            lODataField.SetValue(null, lOBefore);
        }
    }

    /// <summary>The "shipped" values of the check: each effect on and somewhere inside its range, none at
    /// an end, so every change shows.</summary>
    private static UWGraphicsDetail.EffectValues fShipped()
    {
        return new UWGraphicsDetail.EffectValues
        {
            TorchLights = true,
            TorchShadows = true,
            MaxShadowLights = 4,
            LavaLights = true,
            LightBrightness = 1.25f,
            NormalStrength = 1.5f,
            SpecularStrength = 0.75f,
            ParallaxDepth = 0.05f,
            SelfShadow = 0.5f,
            Cavity = 0.4f,
            EmissiveStrength = 2f,
            BloomIntensity = 1f,
            AmbientOcclusion = true,
            Tonemapping = UnderworldRevisited.UWSettings.RemasterTonemappingEnum.Neutral,
            SpriteGlow = 1f,
            FireFlicker = 0.3f,
            FireJitter = 0.2f,
            SpriteFlicker = 0.1f,
            FireSparks = 1f,
            FireSmoke = 0.5f,
            Dust = 1f,
            GroundShadow = 0.6f
        };
    }

    private static string fDiff(string psRecorded, string psNow)
    {
        string[] lsRecorded = psRecorded.Split('\n');
        string[] lsNow = psNow.Split('\n');
        StringBuilder lOText = new StringBuilder();
        int liShown = 0;

        for (int liLine = 0; liLine < Math.Max(lsRecorded.Length, lsNow.Length) && liShown < 40; liLine++)
        {
            string lsWas = liLine < lsRecorded.Length ? lsRecorded[liLine] : "<end>";
            string lsIs = liLine < lsNow.Length ? lsNow[liLine] : "<end>";

            if (lsWas == lsIs)
                continue;

            lOText.Append("  line ").Append(liLine + 1).Append(" recorded: ").Append(lsWas).Append('\n')
                .Append("  line ").Append(liLine + 1).Append(" now:      ").Append(lsIs).Append('\n');
            liShown++;
        }

        return lOText.ToString();
    }
}
