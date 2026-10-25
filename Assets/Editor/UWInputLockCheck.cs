using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE TYPING LOCK'S SET, RECORDED (cleanup step 7.4, 2026-10-25): which actions of a live UWControls
/// fall silent while text is typed (UWControls.SetTextEntryActive) and which stay - every action of
/// the three maps in their order, with its binding count, once without the lock and once with it. The
/// oracle of the lock's move from the hand-written list (UWTextEntryState.LockedPlayerActions,
/// LockedNoclipActions and the whole debug map) onto the input contexts (UWInputContexts): the dump
/// before and after must be the same. It also pins the maps' action names and binding counts that
/// settings.json's "Map|Action|BindingIndex|path" lines and UWActionTable depend on.
///
/// Menu: UW / Check input lock. Batch: -executeMethod UWInputLockCheck.Run; UW_RECORD=1 records
/// Tools/Fixtures/input/typing-lock.expected.txt instead of comparing, UW_INPUT_LOCK_OUT writes the
/// dump to that file as well. Nothing is read from or written to the game's settings beyond what a new
/// UWControls reads (the rebinds in force, which do not change what is enabled).
/// </summary>
public static class UWInputLockCheck
{
    private const string ExpectedFile = "Tools/Fixtures/input/typing-lock.expected.txt";

    [MenuItem("UW/Check input lock")]
    public static void Run()
    {
        bool lbRecord = Environment.GetEnvironmentVariable("UW_RECORD") == "1";
        string lsOut = Environment.GetEnvironmentVariable("UW_INPUT_LOCK_OUT");
        string lsExpected = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ExpectedFile.Replace('/', Path.DirectorySeparatorChar));
        int liFailures = 0;
        string lsDump;

        try
        {
            lsDump = Dump();
        }
        catch (Exception lOError)
        {
            Debug.LogError("Input lock: the dump threw - " + lOError);
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
                Debug.Log("Input lock: recorded " + ExpectedFile);
            }
            else if (!File.Exists(lsExpected))
            {
                Debug.LogError("Input lock: no " + ExpectedFile + " - record once with UW_RECORD=1");
                liFailures++;
            }
            else
            {
                string lsRecorded = File.ReadAllText(lsExpected).Replace("\r\n", "\n");

                if (lsRecorded == lsDump)
                    Debug.Log("Input lock: every action as recorded");
                else
                {
                    liFailures++;
                    Debug.LogError("Input lock: differs from " + ExpectedFile + "\n" + fDiff(lsRecorded, lsDump));
                }
            }
        }

        if (Application.isBatchMode)
            EditorApplication.Exit(liFailures == 0 ? 0 : 1);
    }

    /// <summary>Every action per map: "Map|Action (bindings) free|silent", without the lock, then
    /// with it. The lock is a static of UWControls; it is put back as it was.</summary>
    public static string Dump()
    {
        bool lbBefore = UWControls.IsTextEntryActive;
        StringBuilder lOText = new StringBuilder();
        UWControls lOControls = new UWControls();

        try
        {
            lOControls.Enable();

            foreach (bool lbLocked in new[] { false, true })
            {
                UWControls.SetTextEntryActive(lbLocked);

                lOText.Append("==== ").Append(lbLocked ? "typing" : "not typing").Append('\n');

                foreach (string lsMap in new[] { "Player", "Noclip", "Debug" })
                {
                    InputActionMap lOMap = lOControls.FindAction(lsMap, fFirstAction(lsMap)).actionMap;

                    foreach (InputAction lOAction in lOMap.actions)
                    {
                        lOText.Append(lsMap).Append('|').Append(lOAction.name).Append(" (").Append(lOAction.bindings.Count)
                            .Append(") ").Append(lOAction.enabled ? "free" : "silent").Append('\n');
                    }
                }
            }
        }
        finally
        {
            UWControls.SetTextEntryActive(lbBefore);
            lOControls.Dispose();
        }

        return lOText.ToString();
    }

    /// <summary>An action every map has first, to reach the map through FindAction.</summary>
    private static string fFirstAction(string psMap)
    {
        return psMap == "Debug" ? "ToggleOverlay" : "Move";
    }

    /// <summary>The first lines that differ, for the log.</summary>
    private static string fDiff(string psRecorded, string psNow)
    {
        string[] lsRecorded = psRecorded.Split('\n');
        string[] lsNow = psNow.Split('\n');
        StringBuilder lOText = new StringBuilder();
        int liShown = 0;

        for (int liLine = 0; liLine < Math.Max(lsRecorded.Length, lsNow.Length) && liShown < 20; liLine++)
        {
            string lsWas = liLine < lsRecorded.Length ? lsRecorded[liLine] : "<none>";
            string lsIs = liLine < lsNow.Length ? lsNow[liLine] : "<none>";

            if (lsWas == lsIs)
                continue;

            lOText.Append("line ").Append(liLine + 1).Append(": recorded '").Append(lsWas).Append("', now '").Append(lsIs).Append("'\n");
            liShown++;
        }

        return lOText.ToString();
    }
}
