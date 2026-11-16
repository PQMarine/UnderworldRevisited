using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// features.json BESIDE THE BUILT GAME (2026-11-16, for the Underworld Editor (Avalonia)'s mod manager, whose roadmap asks
/// for a machine-readable version and capability answer): a small file beside UR.exe or UR.x86_64 that another program
/// reads instead of guessing from file names. A Unity player with a window writes nothing to a console, so a file is the
/// dependable answer. Written after every player build, so the release packages carry it (MakeReleaseZip packs the
/// build folder). Docs/MOD-TOOLS.md describes the fields; featuresFormat goes up when one changes its meaning.
/// </summary>
public sealed class UWFeaturesBuildFile : IPostprocessBuildWithReport
{
    public const string FileName = "features.json";

    public int callbackOrder => 1;

    public void OnPostprocessBuild(BuildReport pOReport)
    {
        string lsExecutable = pOReport.summary.outputPath;

        if (string.IsNullOrEmpty(lsExecutable))
            return;

        string lsFolder = Path.GetDirectoryName(lsExecutable);
        string lsCompany = PlayerSettings.companyName;
        string lsProduct = PlayerSettings.productName;

        StringBuilder lOJson = new StringBuilder();

        lOJson.Append("{\n");
        lOJson.Append("  \"featuresFormat\": 1,\n");
        lOJson.Append("  \"name\": \"").Append(fEscape(lsProduct)).Append("\",\n");
        lOJson.Append("  \"version\": \"").Append(fEscape(PlayerSettings.bundleVersion)).Append("\",\n");
        lOJson.Append("  \"executable\": \"").Append(fEscape(Path.GetFileName(lsExecutable))).Append("\",\n");
        lOJson.Append("  \"games\": [\"uw1\", \"uw2\"],\n");
        lOJson.Append("  \"preview\": [\"uw2\"],\n");
        // The editor's own package format number ("format" in a .uwmod's mod.json), from the constant the loader checks.
        lOJson.Append("  \"uwmod\": { \"formats\": [").Append(UWModManifest.EditorFormat).Append("], \"games\": [\"uw1\", \"uw2\"] },\n");
        lOJson.Append("  \"mods\": { \"folder\": \"Mods\", \"order\": \"Mods/order.json\", \"sameFileInTwoPackages\": \"laterRefused\", \"folderArgument\": \"-mods\" },\n");
        lOJson.Append("  \"userFolder\": {\n");
        lOJson.Append("    \"windows\": \"%USERPROFILE%\\\\AppData\\\\LocalLow\\\\").Append(fEscape(lsCompany)).Append("\\\\").Append(fEscape(lsProduct)).Append("\",\n");
        lOJson.Append("    \"linux\": \"~/.config/unity3d/").Append(fEscape(lsCompany)).Append("/").Append(fEscape(lsProduct)).Append("\",\n");
        lOJson.Append("    \"portable\": \"UserData\",\n");
        lOJson.Append("    \"portableWhen\": [\"portable.txt beside the executable\", \"UserData beside the executable\", \"-portable\"]\n");
        lOJson.Append("  },\n");
        lOJson.Append("  \"doc\": \"https://github.com/PQMarine/UnderworldRevisited/blob/main/Docs/MOD-TOOLS.md\"\n");
        lOJson.Append("}\n");

        File.WriteAllText(Path.Combine(lsFolder, FileName), lOJson.ToString(), new UTF8Encoding(false));
        Debug.Log("UWFeaturesBuildFile: " + FileName + " written to " + lsFolder);
    }

    private static string fEscape(string psText)
    {
        return (psText ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
