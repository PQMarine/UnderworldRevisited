using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE LANGUAGE FILES BESIDE THE BUILT GAME (translation step 3D, 2026-10-26): UWLanguage reads
/// Languages/&lt;code&gt;.tsv beside the executable (in the editor beside the project), so after a player
/// build the project's Languages/*.tsv are copied into the folder of the executable, Languages
/// beside UR.exe or UR.x86_64. The game-text packs (&lt;code&gt;.strings.tsv) are never copied: the
/// game's own texts are the rights holders' and do not travel with a build (the player lays his
/// own pack beside the game; Tools/Release/MakeReleaseZip.ps1 refuses a package that holds one).
/// </summary>
public sealed class UWLanguageBuildCopy : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport pOReport)
    {
        string lsSource = Path.Combine(Path.GetDirectoryName(Application.dataPath), UWLanguage.FolderName);
        string lsExecutable = pOReport.summary.outputPath;

        if (!Directory.Exists(lsSource) || string.IsNullOrEmpty(lsExecutable))
            return;

        string lsTarget = Path.Combine(Path.GetDirectoryName(lsExecutable), UWLanguage.FolderName);
        int liCopied = 0;

        Directory.CreateDirectory(lsTarget);

        foreach (string lsFile in Directory.GetFiles(lsSource, "*.tsv"))
        {
            if (lsFile.EndsWith(UWGameTextPack.FileSuffix, System.StringComparison.OrdinalIgnoreCase))
                continue;

            File.Copy(lsFile, Path.Combine(lsTarget, Path.GetFileName(lsFile)), true);
            liCopied++;
        }

        Debug.Log("UWLanguageBuildCopy: " + liCopied + " language files copied to " + lsTarget);
    }
}
