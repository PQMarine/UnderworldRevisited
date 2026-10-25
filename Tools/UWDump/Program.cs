using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Reads the original Ultima Underworld 1 data without a game engine.
    ///
    /// Everything it prints comes from the same layer the game itself loads from
    /// (Assets/UWDataImport), compiled here with the plain .NET SDK. The tool is useful in
    /// its own right - for looking things up while working on the port - and it is the
    /// standing proof that the layer needs no engine.
    ///
    /// The commands sit in a registry (cleanup 9.2, 2026-10-25): name, usage, whether they need
    /// the game data, and the method that runs them; the ones that need no data skip the load.
    /// The editor's dump scripts (strings, area, tile, NPCs, objects, conversations, automap,
    /// maps) became commands here, so they run without Unity and without editing a constant.
    /// The commands live in the partial files beside this one, one per domain.
    /// </summary>
    public static partial class Program
    {
        /// <summary>One command of the registry.</summary>
        private sealed class Command
        {
            public string Name;

            /// <summary>Arguments after the name (without the data path), for the usage text.</summary>
            public string Usage;

            public string Summary;

            /// <summary>False: the command runs without loading the game data.</summary>
            public bool NeedsData;

            public Func<DumpContext, List<string>, int> Run;
        }

        /// <summary>What a command gets: the data (loaded before it runs when it needs it) and
        /// the folders it came from.</summary>
        private sealed class DumpContext
        {
            public string DataPath;

            /// <summary>The save game folder given with --save, or null for the pristine world.</summary>
            public string SavePath;

            public DataImport Data;
        }

        private static readonly List<Command> msCommands = new List<Command>();

        private static void fRegister(string psName, string psUsage, string psSummary, bool pbNeedsData,
            Func<DumpContext, List<string>, int> pORun)
        {
            msCommands.Add(new Command
            {
                Name = psName, Usage = psUsage, Summary = psSummary, NeedsData = pbNeedsData, Run = pORun
            });
        }

        private static void fRegisterAll()
        {
            fRegister("info", "", "what was found and loaded", true, fPrintInfo);
            fRegister("strings", "<block> [from] [n]", "strings of a block", true, fPrintStrings);
            fRegister("find", "<text>", "search all string blocks", true, fFindString);
            fRegister("strings-export", "[--out file]", "every string, lossless, one line each (translation template)", true, fExportStrings);
            fRegister("level", "<1-9> [x y radius] [--detail]", "tile map and what stands in it", true, fPrintLevel);
            fRegister("tile", "<1-9> <x> <y> [radius]", "tile type, heights, textures and objects", true, fPrintTile);
            fRegister("object", "<id>", "properties of an object type", true, fPrintObject);
            fRegister("triggers", "", "every move trigger, its flags and traps", true, fPrintMoveTriggers);
            fRegister("where", "<id|from-to ...> [--level n] [--detail]", "where objects lie, inside containers too", true, fPrintWhere);
            fRegister("npcs", "[1-9|all]", "creatures with their behaviour fields", true, fPrintNpcs);
            fRegister("conv", "[slot|all] [--out dir] | --run <slot|all>", "conversations as bytecode, or run (answer 1 each time)", true, fConversation);
            fRegister("automap", "[1-9] [--png file]", "automap values (use --save for a save game's map)", true, fPrintAutomap);
            fRegister("maprender", "--out dir [--highlight id ...]", "a top view of every level as PNG", true, fRenderMaps);
            fRegister("rules-template", "[filter]", "every constant of the engine-free layer, as the rule set's template", false, fPrintRulesTemplate);
        }

        public static int Main(string[] psArgs)
        {
            fRegisterAll();

            if (psArgs.Length < 1)
            {
                fPrintUsage();

                return 1;
            }

            string lsName = psArgs[0].ToLowerInvariant();
            Command lOCommand = msCommands.Find(c => c.Name == lsName);

            if (lOCommand == null)
            {
                fPrintUsage();

                return 1;
            }

            List<string> lOArgs = new List<string>(psArgs);

            lOArgs.RemoveAt(0);

            DumpContext lOContext = new DumpContext { SavePath = fTakeOption(lOArgs, "--save") };

            if (!lOCommand.NeedsData)
                return lOCommand.Run(lOContext, lOArgs);

            string lsError;

            if (lOArgs.Count > 0 && fLooksLikePath(lOArgs[0]))
            {
                lOContext.DataPath = DataPath.Resolve(lOArgs[0], out lsError);
                lOArgs.RemoveAt(0);
            }
            else
            {
                lOContext.DataPath = DataPath.Find(out lsError);
            }

            if (lOContext.DataPath == null)
            {
                Console.Error.WriteLine(lsError);

                return 2;
            }

            if (lOContext.SavePath != null && !File.Exists(Path.Combine(lOContext.SavePath, "LEV.ARK")))
            {
                // DataImport would quietly fall back to the pristine world.
                Console.Error.WriteLine("No save game in " + lOContext.SavePath + " (LEV.ARK missing).");

                return 2;
            }

            try
            {
                lOContext.Data = new DataImport(lOContext.DataPath, lOContext.SavePath);
            }
            catch (Exception lOException)
            {
                Console.Error.WriteLine("Could not read the game data in " + lOContext.DataPath + ": " + lOException.Message);

                return 3;
            }

            return lOCommand.Run(lOContext, lOArgs);
        }

        private static void fPrintUsage()
        {
            Console.WriteLine("uwdump - reads Ultima Underworld 1 data without a game engine.");
            Console.WriteLine();

            foreach (Command lOCommand in msCommands)
            {
                string lsLeft = "  uwdump " + lOCommand.Name + (lOCommand.NeedsData ? " [path] " : " ") + lOCommand.Usage;

                Console.WriteLine(lsLeft.PadRight(66) + " " + lOCommand.Summary);
            }

            Console.WriteLine();
            Console.WriteLine("[path] is the DATA folder of an installation, or the GOG installation folder or");
            Console.WriteLine("game.gog itself - the image is then extracted into a cache once. Without it the");
            Console.WriteLine("environment variable UW_DATA is used, then the common installation folders.");
            Console.WriteLine("--save <folder> reads the world of a save game (SAVE1 ...) instead of the pristine one.");
            Console.WriteLine("Levels count from 1, object ids may be written as 0x...; nothing is ever written");
            Console.WriteLine("to the game's folders.");
        }

        /// <summary>The first argument is the data path when it names something on disk or is
        /// written like a path; anything else (a level, a slot, a search word) is the command's.</summary>
        private static bool fLooksLikePath(string psArg)
        {
            if (string.IsNullOrEmpty(psArg) || psArg.StartsWith("--", StringComparison.Ordinal))
                return false;

            return Directory.Exists(psArg) || File.Exists(psArg)
                || psArg.IndexOf('/') >= 0 || psArg.IndexOf('\\') >= 0 || psArg.IndexOf(':') >= 0;
        }

        /// <summary>Removes "--name value" from the arguments and returns the value, or null.</summary>
        private static string fTakeOption(List<string> pOArgs, string psName)
        {
            int liAt = pOArgs.FindIndex(a => string.Equals(a, psName, StringComparison.OrdinalIgnoreCase));

            if (liAt < 0 || liAt + 1 >= pOArgs.Count)
                return null;

            string lsValue = pOArgs[liAt + 1];

            pOArgs.RemoveRange(liAt, 2);

            return lsValue;
        }

        /// <summary>Removes a flag from the arguments; true when it was there.</summary>
        private static bool fTakeFlag(List<string> pOArgs, string psName)
        {
            int liAt = pOArgs.FindIndex(a => string.Equals(a, psName, StringComparison.OrdinalIgnoreCase));

            if (liAt < 0)
                return false;

            pOArgs.RemoveAt(liAt);

            return true;
        }

        private static bool fParse(string psText, out int piValue)
        {
            if (psText != null && psText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(psText.Substring(2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out piValue);

            return int.TryParse(psText, out piValue);
        }

        /// <summary>The argument at piIndex as a number, or piDefault when it is missing or not one.</summary>
        private static int fArgument(List<string> pOArgs, int piIndex, int piDefault)
        {
            return piIndex < pOArgs.Count && fParse(pOArgs[piIndex], out int liValue) ? liValue : piDefault;
        }

        /// <summary>A level by its number (1-9), or null after printing why.</summary>
        private static UWDataImport.UWData.UWLevel fLevel(DumpContext pOContext, int piLevel)
        {
            UWDataImport.UWData.UWLevel lOLevel = pOContext.Data.Levels == null
                ? null
                : pOContext.Data.Levels.Find(l => l != null && l.LevelNumber == piLevel);

            if (lOLevel == null || lOLevel.TileData == null)
                Console.Error.WriteLine("Level " + piLevel + " does not exist.");

            return lOLevel == null || lOLevel.TileData == null ? null : lOLevel;
        }

        /// <summary>The strings carry the original line breaks; for a list one line is enough.
        /// </summary>
        private static string fOneLine(string psText)
        {
            return psText == null ? string.Empty : psText.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        /// <summary>An object type's name: string block 4, entry id + 1; "?" for an id past the
        /// block's end.</summary>
        private static string fName(DumpContext pOContext, int piId)
        {
            try
            {
                return fOneLine(pOContext.Data.GetObjectDescription(piId + 1));
            }
            catch (ArgumentOutOfRangeException)
            {
                return "?";
            }
        }
    }
}
