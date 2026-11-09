using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE MODS WINDOW'S ORDER (order.json, UWModOrder; per user 2026-11-09) on a made-up folder: the mods it names
    /// first in its order, the rest alphabetically; one switched off is not loaded and one requiring it neither;
    /// the window's list holds them all with why; a broken order.json gives every mod on; the file written and
    /// read back; a mod's readme or description.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckModOrder()
        {
            Check.Group("Mods: the load order and the switches (UWModOrder, order.json)");

            string lsRoot = Path.Combine(Path.GetTempPath(), "uwselfcheck-modorder-" + Guid.NewGuid().ToString("N"));

            try
            {
                fModFolder(lsRoot, "alpha", "{ \"id\": \"o.alpha\", \"name\": \"Alpha\" }");
                fModFolder(lsRoot, "beta", "{ \"id\": \"o.beta\" }");
                fModFolder(lsRoot, "gamma", "{ \"id\": \"o.gamma\", \"requires\": [\"o.beta\"] }");
                fModFolder(lsRoot, "delta", "{ \"id\": \"o.delta\", \"description\": \"Delta's own words.\" }");
                fModFolder(lsRoot, "broken", "{ \"id\": ");
                File.WriteAllText(Path.Combine(lsRoot, "alpha", "README.md"), "# Alpha\r\nWhat it does.", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(lsRoot, UWModOrder.FileName),
                    "{ \"order\": [\"o.delta\", \"o.alpha\", \"o.gone\"], \"disabled\": [\"o.beta\"] }", new UTF8Encoding(false));

                UWModCatalog lOCatalog = UWModCatalog.Scan(lsRoot);

                Check.Str("order: the named first in the file's order, the rest alphabetically (the window's list)",
                    string.Join(" ", lOCatalog.All.ConvertAll(pOEntry => pOEntry.Id)), "o.delta o.alpha o.beta o.gamma");
                Check.Str("order: loaded in that order, the switched-off one and what requires it not",
                    string.Join(" ", lOCatalog.Loaded.ConvertAll(pOEntry => pOEntry.Id)), "o.delta o.alpha");
                Check.Str("order: why - switched off, and the one requiring it",
                    lOCatalog.ReasonOf("o.beta") + " | " + lOCatalog.ReasonOf("o.gamma") + " | " + (lOCatalog.ReasonOf("o.alpha") ?? "loaded"),
                    "switched off | needs o.beta, which is switched off | loaded");
                Check.Str("order: switched off is no failure; the broken mod.json is the one unreadable",
                    lOCatalog.Disabled.Count + " " + lOCatalog.Unreadable.Count + " " + (lOCatalog.Unreadable.Count == 1 && lOCatalog.Unreadable[0].StartsWith("broken")),
                    "1 1 True");
                UWModCatalog.Broken lOBrokenOne = lOCatalog.BrokenMods.Count == 1 ? lOCatalog.BrokenMods[0] : null;

                Check.Str("broken: kept with its name, kind and place for the window; a mod's place from the folder on",
                    lOBrokenOne == null ? "none" : lOBrokenOne.Name + " " + lOBrokenOne.Kind + " " + lOCatalog.WhereInFolder(lOBrokenOne.Location)
                        + " " + lOCatalog.WhereInFolder(lOCatalog.All.Find(pOEntry => pOEntry.Id == "o.alpha").Source.Location),
                    "broken BadManifest broken alpha");

                Check.Str("readme: a README.md first, else mod.json's description, else none",
                    lOCatalog.All.Find(pOEntry => pOEntry.Id == "o.alpha").Readme().Replace("\n", "/") + " | "
                    + lOCatalog.All.Find(pOEntry => pOEntry.Id == "o.delta").Readme() + " | "
                    + (lOCatalog.All.Find(pOEntry => pOEntry.Id == "o.beta").Readme() ?? "none"),
                    "# Alpha/What it does. | Delta's own words. | none");

                // A description without end (the editor sets none) cut for the window.
                UWModCatalog.Entry lOLong = new UWModCatalog.Entry
                {
                    Source = UWModSource.Folder(Path.Combine(lsRoot, "beta")),
                    Manifest = new UWModManifest { Id = "o.long", Description = new string('x', UWModCatalog.ReadmeLimit + 500) }
                };

                Check.Str("readme: a description longer than the window takes is cut, with a mark",
                    lOLong.Readme().Length + " " + lOLong.Readme().EndsWith("\n..."), (UWModCatalog.ReadmeLimit + 4) + " True");

                // A README.md as rich text.
                Check.Str("markdown: heading, joined paragraph, bold and italic, a list, a link, code, a rule, a Unity tag left alone",
                    UWMarkdownLite.ToRichText("# Title\n\nOne line\nand the next with **bold** and *slant*.\n\n- item one\n  - nested\n"
                        + "1. first\n\nSee [the site](https://x.org) and `code` with my_mod_name.\n\n---\n\n```\nraw *text*\n```\n<b>not bold</b>", "ABCDEF")
                        .Replace("\n", "/"),
                    "<b><color=#ABCDEF>Title</color></b>//One line and the next with <b>bold</b> and <i>slant</i>.//"
                    + "• item one/    • nested/1. first//See the site (https://x.org) and code with my_mod_name.///    raw *text*/< b>not bold< /b>");

                // Written and read back; switching on again.
                UWModOrder lOOrder = new UWModOrder();

                lOOrder.Order.AddRange(new[] { "o.gamma", "o.beta", "o.alpha", "o.delta" });
                Check.Bool("write: order.json written", lOOrder.Write(lsRoot) == null, true);

                UWModCatalog lOAgain = UWModCatalog.Scan(lsRoot);

                Check.Str("write: read back, nothing off - every mod loaded, each after what it requires",
                    string.Join(" ", lOAgain.Loaded.ConvertAll(pOEntry => pOEntry.Id)), "o.beta o.gamma o.alpha o.delta");
                Check.Str("write: the file's text", lOOrder.ToJson().Replace("\n", "/"),
                    "{/  \"order\": [/    \"o.gamma\",/    \"o.beta\",/    \"o.alpha\",/    \"o.delta\"/  ],/  \"disabled\": []/}/");

                // A broken order.json: every mod on, alphabetically, with a warning.
                File.WriteAllText(Path.Combine(lsRoot, UWModOrder.FileName), "{ \"order\": [ ", new UTF8Encoding(false));

                UWModCatalog lOBroken = UWModCatalog.Scan(lsRoot);

                Check.Str("broken order.json: every mod on, alphabetically, a warning",
                    string.Join(" ", lOBroken.Loaded.ConvertAll(pOEntry => pOEntry.Id)) + " " + lOBroken.Warnings.Exists(psLine => psLine.StartsWith(UWModOrder.FileName)),
                    "o.alpha o.beta o.delta o.gamma True");
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
    }
}
