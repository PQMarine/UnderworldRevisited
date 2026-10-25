using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE NAMES OF THE BINDINGS, RECORDED (cleanup step 7.2, 2026-10-25): what the Controls window, the
/// help's Controls tab (both schemes, the gamepad's sections too), the pad's hint strip, the
/// crosshair and the action bar's pad hints, the letter grid's hint line and the world's mouse
/// buttons show for every binding - with the built-in keys and with rebinds, in every button style
/// and with the sticks swapped or not - and where the easy steps lie in either scheme. The oracle of
/// the binding lookup's move into UWBindingInfo: the dump before and after must be the same, and with
/// rebinds only the easy steps may move (decision 18: they follow their walk and turn keys).
///
/// Menu: UW / Check binding names. Batch: -executeMethod UWBindingNamesCheck.Run; UW_RECORD=1 records
/// Tools/Fixtures/bindings/names.expected.txt instead of comparing, UW_BINDING_NAMES_OUT writes the
/// dump to that file as well. The game's own settings are untouched: the settings data is swapped in
/// for the check (as UWSettingsFixtureCheck does) and put back afterwards; nothing is saved.
/// </summary>
public static class UWBindingNamesCheck
{
    private const string ExpectedFile = "Tools/Fixtures/bindings/names.expected.txt";

    /// <summary>The rebinds of the check, as settings.json keeps them ("Map|Action|BindingIndex|path"):
    /// none; one walk key moved (decision 18's example: Shift+T steps then, Shift+W no more); and many,
    /// keys onto mouse buttons and back, gamepad buttons, and an old line for an easy step's own
    /// binding, which no entry applies.</summary>
    private static readonly string[][] msRebinds =
    {
        new string[0],
        new[] { "Player|Move|1|<Keyboard>/t" },
        new[]
        {
            "Player|Move|1|<Keyboard>/upArrow", "Player|Move|2|<Keyboard>/downArrow", "Player|Move|3|<Keyboard>/leftArrow",
            "Player|Move|4|<Keyboard>/rightArrow", "Player|Turn|1|<Keyboard>/q", "Player|Turn|2|<Keyboard>/e",
            "Player|Jump|1|<Keyboard>/k", "Player|CursorDrag|0|<Keyboard>/g", "Player|Interact|0|<Mouse>/middleButton",
            "Player|ToggleMap|0|<Keyboard>/numpad5", "Player|ModernUse|0|<Keyboard>/f", "Player|EasyModifier|0|<Keyboard>/rightAlt",
            "Player|WalkSlow|0|<Keyboard>/capsLock", "Player|ModernUse|1|<Gamepad>/dpad/left", "Player|PadClick|0|<Gamepad>/buttonNorth",
            "Player|Menu|1|<Gamepad>/select", "Player|PadBack|0|<Gamepad>/leftShoulder", "Player|PadPointer|0|<Gamepad>/leftStickPress",
            "Player|EasyForward|0|<Keyboard>/k"
        }
    };

    /// <summary>The gamepad entries the hints read (UWPadHintStrip, UWModernCrosshair, UWModernActionBar).</summary>
    private static readonly string[][] msHintEntries =
    {
        new[] { "PadClick", "0" }, new[] { "PadBack", "0" }, new[] { "PadPointer", "0" }, new[] { "PadCast", "0" },
        new[] { "PadContext", "0" }, new[] { "PadSlotPrevious", "0" }, new[] { "PadSlotNext", "0" }, new[] { "PadSlotUse", "0" },
        new[] { "ModernUse", "1" }, new[] { "ModernLook", "1" }
    };

    private static readonly string[] msGridPaths =
    {
        "<Gamepad>/buttonSouth", "<Gamepad>/buttonEast", "<Gamepad>/buttonWest", "<Gamepad>/buttonNorth", "<Gamepad>/start"
    };

    private static readonly string[] msEasyActions = { "EasyForward", "EasyWalkForward", "EasyBack", "EasyTurnLeft", "EasyTurnRight" };

    [MenuItem("UW/Check binding names")]
    public static void Run()
    {
        bool lbRecord = Environment.GetEnvironmentVariable("UW_RECORD") == "1";
        string lsOut = Environment.GetEnvironmentVariable("UW_BINDING_NAMES_OUT");
        string lsExpected = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ExpectedFile.Replace('/', Path.DirectorySeparatorChar));
        int liFailures = 0;
        string lsDump;

        try
        {
            lsDump = Dump();
        }
        catch (Exception lOError)
        {
            Debug.LogError("Binding names: the dump threw - " + lOError);
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
                Debug.Log("Binding names: recorded " + ExpectedFile);
            }
            else if (!File.Exists(lsExpected))
            {
                Debug.LogError("Binding names: no " + ExpectedFile + " - record once with UW_RECORD=1");
                liFailures++;
            }
            else
            {
                string lsRecorded = File.ReadAllText(lsExpected).Replace("\r\n", "\n");

                if (lsRecorded == lsDump)
                    Debug.Log("Binding names: every name as recorded");
                else
                {
                    liFailures++;
                    Debug.LogError("Binding names: differ from " + ExpectedFile + "\n" + fDiff(lsRecorded, lsDump));
                }
            }
        }

        if (Application.isBatchMode)
            EditorApplication.Exit(liFailures == 0 ? 0 : 1);
    }

    /// <summary>The whole record: per rebind set the keyboard's names, per set, button style and stick
    /// swap the gamepad's.</summary>
    public static string Dump()
    {
        FieldInfo lODataField = typeof(UWUserSettings).GetField("msData", BindingFlags.NonPublic | BindingFlags.Static);

        if (lODataField == null || lODataField.FieldType != typeof(UWDataImport.UWData.UWUserData))
            throw new InvalidOperationException("UWUserSettings.msData (a UWUserData) not found - the check needs updating");

        object lOBefore = lODataField.GetValue(null);
        StringBuilder lOText = new StringBuilder();

        try
        {
            for (int liSet = 0; liSet < msRebinds.Length; liSet++)
            {
                for (int liStyle = 0; liStyle < 3; liStyle++)
                {
                    for (int liSwap = 0; liSwap < 2; liSwap++)
                    {
                        UWDataImport.UWData.UWUserData lOData = new UWDataImport.UWData.UWUserData();

                        lODataField.SetValue(null, lOData);
                        UWUserSettings.KeyBindings.AddRange(msRebinds[liSet]);
                        UWUserSettings.GamepadButtonStyle = liStyle;
                        UWUserSettings.GamepadSwapSticks = liSwap == 1;

                        UWControls lOControls = new UWControls();

                        try
                        {
                            lOText.Append("==== rebinds ").Append(liSet).Append(", style ").Append(liStyle).Append(", sticks swapped ")
                                .Append(liSwap == 1).Append('\n');

                            // The keyboard's names do not hang on the pad's style: once per rebind set.
                            if (liStyle == 0 && liSwap == 0)
                                fDumpKeyboard(lOText, lOControls);

                            fDumpPad(lOText);
                        }
                        finally
                        {
                            lOControls.Dispose();
                        }
                    }
                }
            }
        }
        finally
        {
            lODataField.SetValue(null, lOBefore);
        }

        return lOText.ToString();
    }

    private static void fDumpKeyboard(StringBuilder pOText, UWControls pOControls)
    {
        pOText.Append("-- Controls window, keyboard\n");

        foreach (UWKeyBindings.Entry lOEntry in UWKeyBindings.Entries)
            pOText.Append(lOEntry.Key).Append(" | ").Append(lOEntry.Label).Append(" | ").Append(UWBindingInfo.Name(lOEntry)).Append('\n');

        fDumpSections(pOText, "help, modern", UWHelpControls.Build(true));
        fDumpSections(pOText, "help, classic", UWHelpControls.Build(false));

        pOText.Append("-- the world's mouse buttons\n");

        MethodInfo lOGetPath = typeof(UWMouseButtons).GetMethod("fGetPath", BindingFlags.NonPublic | BindingFlags.Static);

        pOText.Append("CursorDrag = ").Append(lOGetPath.Invoke(null, new object[] { "CursorDrag", "<Mouse>/leftButton" })).Append('\n');
        pOText.Append("Interact = ").Append(lOGetPath.Invoke(null, new object[] { "Interact", "<Mouse>/rightButton" })).Append('\n');

        // Per scheme, as UWControlScheme.Apply puts them (decision 18: they follow the walk and turn
        // keys); the classic scheme's last, as ApplyTo left them without a scheme component.
        foreach (bool lbModern in new[] { true, false })
        {
            UWKeyBindings.ApplyEasySteps(pOControls, lbModern);

            pOText.Append("-- the easy steps' keys (binding 0 in force), ").Append(lbModern ? "modern" : "classic")
                .Append(": ").Append(UWBindingInfo.EasyStepKeyNames(lbModern)).Append('\n');

            foreach (string lsAction in msEasyActions)
            {
                InputAction lOAction = pOControls.FindAction("Player", lsAction);

                pOText.Append(lsAction).Append(" = ").Append(lOAction.bindings[0].effectivePath).Append('\n');
            }
        }
    }

    private static void fDumpPad(StringBuilder pOText)
    {
        pOText.Append("-- Controls window, gamepad\n");

        foreach (UWKeyBindings.Entry lOEntry in UWKeyBindings.PadEntries)
        {
            pOText.Append(lOEntry.Key).Append(" | ").Append(lOEntry.Label).Append(" | ").Append(UWBindingInfo.Name(lOEntry))
                .Append(" | ").Append(fName(UWGlyphs.ForPath(UWBindingInfo.Path(lOEntry)))).Append('\n');
        }

        MethodInfo lOModernPad = typeof(UWHelpControls).GetMethod("fModernPad", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo lOClassicPad = typeof(UWHelpControls).GetMethod("fClassicPad", BindingFlags.NonPublic | BindingFlags.Static);

        fDumpSections(pOText, "help, modern pad", (List<UWHelpControls.Section>)lOModernPad.Invoke(null, null));
        fDumpSections(pOText, "help, classic pad", (List<UWHelpControls.Section>)lOClassicPad.Invoke(null, null));

        pOText.Append("-- the hints' pad entries\n");

        foreach (string[] lsEntry in msHintEntries)
        {
            int liBinding = int.Parse(lsEntry[1]);
            string lsPath = UWBindingInfo.PadPath(lsEntry[0], liBinding);

            pOText.Append(lsEntry[0]).Append('|').Append(liBinding).Append(" = ").Append(lsPath).Append(" | ")
                .Append(UWBindingInfo.ButtonName(lsPath)).Append(" | ").Append(fName(UWGlyphs.ForEntry(lsEntry[0], liBinding))).Append('\n');
        }

        pOText.Append("-- the letter grid's hint line\n");

        foreach (string lsPath in msGridPaths)
            pOText.Append(lsPath).Append(" = ").Append(UWBindingInfo.ButtonName(lsPath)).Append(" | ").Append(fName(UWGlyphs.ForPath(lsPath))).Append('\n');
    }

    private static void fDumpSections(StringBuilder pOText, string psTitle, List<UWHelpControls.Section> pOSections)
    {
        pOText.Append("-- ").Append(psTitle).Append('\n');

        foreach (UWHelpControls.Section lOSection in pOSections)
        {
            pOText.Append("# ").Append(lOSection.Title).Append(" / ").Append(lOSection.Intro ?? "-").Append('\n');

            foreach (UWHelpControls.Row lORow in lOSection.Rows)
            {
                pOText.Append(lORow.Keys).Append(" | ").Append(lORow.Text).Append(" |");

                if (lORow.Glyphs != null)
                {
                    foreach (Texture2D lOGlyph in lORow.Glyphs)
                        pOText.Append(' ').Append(fName(lOGlyph));
                }

                pOText.Append('\n');
            }
        }
    }

    private static string fName(Texture2D pOTexture)
    {
        return pOTexture != null ? pOTexture.name : "(none)";
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
