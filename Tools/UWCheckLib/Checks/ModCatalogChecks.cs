using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE MOD FOLDER'S CATALOG (Docs/WIDESCREEN-WEAPONS-PLAN.md step 1, 2026-11-09) on a made-up folder:
    /// a folder mod, one a level deeper, a zip with a top folder, an editor package (.uwmod), the same id
    /// as a folder and a zip (the folder wins), a bad id, a requirement too old, a ring and a mod needing
    /// it, a mod for the second game only. Also the JSON reader's values and errors.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckModCatalog()
        {
            Check.Group("Mods: the folder's catalog (UWModCatalog, UWModSource, UWModManifest, UWJsonReader)");

            // The JSON reader.
            object lOValue = UWJsonReader.Parse("﻿{ \"a\": [1, 2.5, \"x\\u0041\", true, null], \"b\": { \"c\": false } }");
            Dictionary<string, object> lOObject = lOValue as Dictionary<string, object>;
            List<object> lOList = UWJsonReader.GetList(lOObject, "a");

            Check.Str("json: values of every kind",
                lOList.Count + " " + lOList[0] + " " + lOList[1] + " " + lOList[2] + " " + lOList[3] + " " + (lOList[4] == null)
                + " " + UWJsonReader.GetObject(lOObject, "b")["c"], "5 1 2.5 xA True True False");
            UWJsonReader.TryParseObject("{\n \"a\": 1,\n \"b\" 2 }", out string lsError);
            Check.Str("json: an error names its line", lsError, "line 3: expected ':'");

            string lsRoot = Path.Combine(Path.GetTempPath(), "uwselfcheck-mods-" + Guid.NewGuid().ToString("N"));

            try
            {
                fModFolder(lsRoot, "base", "{ \"id\": \"a.base\", \"version\": \"1.2\", \"games\": [\"uw1\"] }");
                fModFolder(Path.Combine(lsRoot, "Graphics"), "ext", "{ \"id\": \"b.ext\", \"version\": \"1.0\", \"requires\": [\"a.base >= 1.0\"] }");
                fModZip(Path.Combine(lsRoot, "c-mod.zip"), "cmod/", "{ \"id\": \"c.zip\", \"version\": \"3\" }");
                fModZip(Path.Combine(lsRoot, "zzz-dup.zip"), string.Empty, "{ \"id\": \"a.base\", \"version\": \"9.0\" }");
                fModFolder(lsRoot, "bad", "{ \"id\": \"Bad Id\" }");
                fModFolder(lsRoot, "old", "{ \"id\": \"d.old\", \"requires\": [\"a.base >= 2.0\"] }");
                fModFolder(lsRoot, "one", "{ \"id\": \"e.one\", \"requires\": [\"e.two\"] }");
                fModFolder(lsRoot, "two", "{ \"id\": \"e.two\", \"requires\": [\"e.one\"] }");
                fModFolder(lsRoot, "dep", "{ \"id\": \"f.dep\", \"requires\": [\"e.one\"] }");
                fModZip(Path.Combine(lsRoot, "garamon.uwmod"), string.Empty,
                    "{ \"format\": 2, \"game\": \"uw1\", \"id\": \"karl.garamon-moved\", \"name\": \"Garamon moved\", \"version\": \"1.0.0\", \"authors\": [\"Karl\"], \"files\": [] }");
                fModFolder(lsRoot, "second", "{ \"id\": \"x.uw2\", \"games\": [\"uw2\"] }");
                // A folder without a mod.json is a category folder (searched, not refused); a zip without one is refused.
                fModFolder(lsRoot, "empty", null);

                using (ZipArchive lONoManifest = ZipFile.Open(Path.Combine(lsRoot, "nomanifest.zip"), ZipArchiveMode.Create))
                    fZipText(lONoManifest, "readme.txt", "no manifest");

                UWModCatalog lOCatalog = UWModCatalog.Scan(lsRoot);
                List<string> lsLoaded = new List<string>();

                foreach (UWModCatalog.Entry lOEntry in lOCatalog.Loaded)
                    lsLoaded.Add(lOEntry.Id);

                Check.Str("mods: loaded in order, each after what it requires", string.Join(" ", lsLoaded),
                    "a.base b.ext c.zip karl.garamon-moved x.uw2");

                UWModCatalog.Entry lOBase = lOCatalog.Loaded[0];

                Check.Str("mods: the folder wins over a zip of the same id", lOBase.Source.IsZip + " " + lOBase.Manifest.Version, "False 1.2");
                Check.Bool("mods: ... with a line about the zip left out",
                    lOCatalog.Warnings.Exists(psLine => psLine.StartsWith("a.base: found twice")), true);

                string lsRefused = string.Join(" | ", lOCatalog.Refused);

                Check.Bool("mods: refused - no mod.json, a bad id, too old, the ring and what needs it (" + lOCatalog.Refused.Count + ")",
                    lsRefused.Contains("nomanifest.zip: no mod.json") && !lsRefused.Contains("empty") && lsRefused.Contains("bad: no valid id")
                    && lsRefused.Contains("d.old") && lsRefused.Contains("found 1.2")
                    && lsRefused.Contains("e.one") && lsRefused.Contains("e.two") && lsRefused.Contains("f.dep")
                    && lOCatalog.Refused.Count == 6, true);

                UWModCatalog.Entry lOPackage = lOCatalog.Loaded.Find(pOEntry => pOEntry.Id == "karl.garamon-moved");

                Check.Str("mods: the editor's package read as format 2 for uw1",
                    lOPackage.Manifest.IsEditorPackage + " " + lOPackage.Manifest.Kind + " " + string.Join(",", lOPackage.Manifest.Games)
                    + " " + string.Join(",", lOPackage.Manifest.Authors), "True uwmod uw1 Karl");

                UWModCatalog.Entry lOZip = lOCatalog.Loaded.Find(pOEntry => pOEntry.Id == "c.zip");

                Check.Str("mods: a zip read from inside its one top folder",
                    lOZip.Source.IsZip + " " + string.Join(",", lOZip.Source.ListFiles()) + " " + (lOZip.Source.ReadAllText("pictures/readme.txt") ?? "none"),
                    "True mod.json,pictures/readme.txt hello");

                Check.Str("mods: the first game's set leaves out the second game's mod", lOCatalog.SetFor("uw1").Key,
                    "a.base@1.2;b.ext@1.0;c.zip@3;karl.garamon-moved@1.0.0;");
                Check.Str("mods: a folder that does not exist has none", string.Join("|", UWModCatalog.Scan(Path.Combine(lsRoot, "nowhere")).Describe()),
                    "Mods: none");
                Check.Str("mods: versions compared as numbers", UWModManifest.CompareVersions("1.10", "1.9") + " " + UWModManifest.CompareVersions("2", "2.0"), "1 0");
            }
            finally
            {
                try
                {
                    Directory.Delete(lsRoot, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void fModFolder(string psParent, string psName, string psJson)
        {
            string lsFolder = Path.Combine(psParent, psName);

            Directory.CreateDirectory(lsFolder);

            if (psJson != null)
                File.WriteAllText(Path.Combine(lsFolder, UWModManifest.FileName), psJson, new UTF8Encoding(false));
            else
                File.WriteAllText(Path.Combine(lsFolder, "readme.txt"), "no manifest");
        }

        private static void fModZip(string psZip, string psTop, string psJson)
        {
            using (ZipArchive lOZip = ZipFile.Open(psZip, ZipArchiveMode.Create))
            {
                fZipText(lOZip, psTop + UWModManifest.FileName, psJson);
                fZipText(lOZip, psTop + "pictures/readme.txt", "hello");
            }
        }

        private static void fZipText(ZipArchive pOZip, string psName, string psText)
        {
            using (StreamWriter lOWriter = new StreamWriter(pOZip.CreateEntry(psName).Open(), new UTF8Encoding(false)))
                lOWriter.Write(psText);
        }
    }
}
