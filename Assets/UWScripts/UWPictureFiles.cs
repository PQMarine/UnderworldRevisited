using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// A LANGUAGE'S OWN PICTURES, READ (Docs/PICTURE-WORDS-PLAN.md step 3, 2026-11-08): the PNGs in
/// Languages/&lt;code&gt;/pictures beside the game, named as uwdump picture-template exports them ("OPBTN_00.png",
/// "WIN1.png" - UWPicturePack). Put in force with the language (UWLanguage.Apply): the names are listed once,
/// a picture is decoded when the game first asks for it (UWPictureWords.Localize, which keeps it until the
/// language changes). Unity's Texture2D.LoadImage decodes the file; its rows come bottom-up and are turned.
/// </summary>
public sealed class UWPictureFiles : IUWPictureFiles
{
    private readonly string msFolder;

    private readonly HashSet<string> mONames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private UWPictureFiles(string psFolder)
    {
        msFolder = psFolder;

        foreach (string lsFile in Directory.GetFiles(psFolder, "*.png"))
            mONames.Add(Path.GetFileName(lsFile));
    }

    /// <summary>The folder of a language's pictures.</summary>
    public static string FolderOf(string psCode)
    {
        return Path.Combine(UWLanguage.GameFolder, UWLanguage.FolderName, psCode, UWPicturePack.FolderName);
    }

    /// <summary>A language's pictures in force (UWPictureWords.Files): none for English or a language without
    /// the folder.</summary>
    public static void Apply(string psCode)
    {
        UWPictureFiles lOFiles = null;

        try
        {
            string lsFolder = string.IsNullOrEmpty(psCode) || !UWLanguage.IsCode(psCode) ? null : FolderOf(psCode);

            if (lsFolder != null && Directory.Exists(lsFolder))
                lOFiles = new UWPictureFiles(lsFolder);
        }
        catch (Exception lOError)
        {
            Debug.LogWarning("UWPictureFiles: the pictures of " + psCode + " could not be listed: " + lOError.Message);
        }

        if (lOFiles != null && lOFiles.mONames.Count == 0)
            lOFiles = null;

        UWPictureWords.Files = lOFiles;

        if (lOFiles != null)
            Debug.Log("UWPictureFiles: " + lOFiles.mONames.Count + " pictures of " + psCode + " from " + lOFiles.msFolder);
    }

    public bool Has(string psFileName)
    {
        return mONames.Contains(psFileName);
    }

    public bool TryRead(string psFileName, out int piWidth, out int piHeight, out UWColor32[] pyPixels)
    {
        piWidth = 0;
        piHeight = 0;
        pyPixels = null;

        Texture2D lOTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        try
        {
            if (!lOTexture.LoadImage(File.ReadAllBytes(Path.Combine(msFolder, psFileName)), false))
                return false;

            piWidth = lOTexture.width;
            piHeight = lOTexture.height;

            Color32[] lyColours = lOTexture.GetPixels32();

            pyPixels = new UWColor32[lyColours.Length];

            for (int y = 0; y < piHeight; y++)
            {
                int liSource = (piHeight - 1 - y) * piWidth;

                for (int x = 0; x < piWidth; x++)
                {
                    Color32 lOColour = lyColours[liSource + x];

                    pyPixels[(y * piWidth) + x] = new UWColor32(lOColour.r, lOColour.g, lOColour.b, lOColour.a);
                }
            }

            return true;
        }
        catch (Exception lOError)
        {
            Debug.LogWarning("UWPictureFiles: " + psFileName + " could not be read: " + lOError.Message);

            return false;
        }
        finally
        {
            UnityEngine.Object.Destroy(lOTexture);
        }
    }
}
