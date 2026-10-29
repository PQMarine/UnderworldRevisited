using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Reads the original Ultima Underworld data without a game engine - the first game's whole, the
    /// second's through the generic readers (--game uw2, info; UW2 plan phase 0 step 6).
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

            /// <summary>The game-text pack given with --strings, laid over the English strings; null
            /// for none.</summary>
            public string StringsPath;

            /// <summary>The game given with --game (uw1, uw2), the first without it (UW2 plan phase 0
            /// step 6).</summary>
            public IUWGameProfile Game = UWGameProfiles.Uw1;

            /// <summary>The strings of either game: the first game's from its data, the second's from
            /// STRINGS.PAK (phase 1 step 6).</summary>
            public UWStrings Strings;

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
            fRegister("strings-export", "[--out file]", "every string, lossless, one line each", true, fExportStrings);
            fRegister("strings-template", "<code> [--out file] [--empty]", "a game-text pack of the player's STRINGS.PAK to translate", true, fStringsTemplate);
            fRegister("validate-tsv", "<file.strings.tsv> [--language code]", "check a game-text pack against the English strings", true, fValidateTsv);
            fRegister("fit", "[--over]", "game strings in the classic look's fixed boxes: width, box, OVER where clipped (with --strings)", true, fFit);
            fRegister("typed-words", "", "the mantras and the words conversations compare typed answers with, and their hints", true, fTypedWords);
            fRegister("looks","[family ...] [--hashes]", "every look sentence the data gives (with --strings, in that language)", true, fLooks);
            fRegister("level", "<1-9> [x y radius] [--detail]", "tile map and what stands in it", true, fPrintLevel);
            fRegister("tile", "<1-9> <x> <y> [radius]", "tile type, heights, textures and objects", true, fPrintTile);
            fRegister("object", "<id>", "properties of an object type", true, fPrintObject);
            fRegister("triggers", "", "every move trigger, its flags and traps", true, fPrintMoveTriggers);
            fRegister("where", "<id|from-to ...> [--level n] [--detail]", "where objects lie, inside containers too", true, fPrintWhere);
            fRegister("npcs", "[1-9|all]", "creatures with their behaviour fields", true, fPrintNpcs);
            fRegister("conv", "[slot|all] [--out dir] | --run <slot|all>", "conversations as bytecode, or run (answer 1 each time)", true, fConversation);
            fRegister("automap", "[1-9] [--png file]", "automap values (use --save for a save game's map)", true, fPrintAutomap);
            fRegister("maprender", "--out dir [--highlight id ...]", "a top view of every level as PNG", true, fRenderMaps);
            fRegister("pictures", "<NAME.GR|BYT> --out dir [--palette n]", "a picture file (or the second game's BYT.ARK screens) as PNGs", true, fDumpPictures);
            fRegister("text-template", "<code> [--out file] [--from existing.tsv]", "the port's own texts in English to translate, or a translation brought up to date", false, fTextTemplate);
            fRegister("rules-template", "[filter]", "the rule set at the original's values, a rules.json with a meaning per key", false, fPrintRulesTemplate);
            fRegister("constants", "[filter]", "every constant of the engine-free layer (the self-check's constant pin)", false, fPrintConstants);
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

            DumpContext lOContext = new DumpContext { SavePath = fTakeOption(lOArgs, "--save"), StringsPath = fTakeOption(lOArgs, "--strings") };
            string lsGame = fTakeOption(lOArgs, "--game");

            if (lsGame != null)
            {
                lOContext.Game = Enum.TryParse(lsGame, true, out UWGame leGame) ? UWGameProfiles.Of(leGame) : null;

                if (lOContext.Game == null)
                {
                    Console.Error.WriteLine("No game " + lsGame + " - uw1 or uw2.");

                    return 1;
                }
            }

            if (!lOCommand.NeedsData)
                return lOCommand.Run(lOContext, lOArgs);

            // The second game's data is not DataImport's yet (phase 2): its own commands, from the format
            // readers (phase 1 step 6).
            if (lOContext.Game != UWGameProfiles.Uw1 && !msUw2Commands.Contains(lOCommand.Name))
            {
                Console.Error.WriteLine(lOCommand.Name + " reads the first game's data only so far; for " + lOContext.Game.Title
                    + " there are " + string.Join(", ", msUw2Commands) + ".");

                return 1;
            }

            string lsError;

            if (lOArgs.Count > 0 && fLooksLikePath(lOArgs[0]))
            {
                lOContext.DataPath = DataPath.Resolve(lOArgs[0], lOContext.Game, out lsError);
                lOArgs.RemoveAt(0);
            }
            else
            {
                lOContext.DataPath = DataPath.Find(lOContext.Game, out lsError);
            }

            if (lOContext.DataPath == null)
            {
                Console.Error.WriteLine(lsError);

                return 2;
            }

            if (lOContext.Game != UWGameProfiles.Uw1)
                return fRunUw2(lOCommand.Name, lOContext, lOArgs);

            if (lOContext.StringsPath != null && !File.Exists(lOContext.StringsPath))
            {
                Console.Error.WriteLine("No game-text pack " + lOContext.StringsPath + ".");

                return 2;
            }

            // A pack's texts go beyond ASCII: printed as UTF-8, not in the console's code page. The
            // English is ASCII, so without a pack the output stays as it was.
            if (lOContext.StringsPath != null)
                Console.OutputEncoding = new System.Text.UTF8Encoding(false);

            // One code, two files, as the game reads them (UWLanguage): beside de.strings.tsv the port's
            // own texts of de.tsv - the grammar's sentence frames among them ("Du siehst {0}.").
            fLoadOwnTexts(lOContext.StringsPath);

            if (lOContext.SavePath != null && !File.Exists(Path.Combine(lOContext.SavePath, "LEV.ARK")))
            {
                // DataImport would quietly fall back to the pristine world.
                Console.Error.WriteLine("No save game in " + lOContext.SavePath + " (LEV.ARK missing).");

                return 2;
            }

            try
            {
                lOContext.Data = new DataImport(lOContext.DataPath, lOContext.SavePath, lOContext.StringsPath);
            }
            catch (Exception lOException)
            {
                Console.Error.WriteLine("Could not read the game data in " + lOContext.DataPath + ": " + lOException.Message);

                return 3;
            }

            lOContext.Strings = lOContext.Data.Strings;

            return lOCommand.Run(lOContext, lOArgs);
        }

        /// <summary>The port's own texts beside a game-text pack (Languages/de.tsv beside
        /// Languages/de.strings.tsv) put in force, when there are; a note on stderr.</summary>
        private static void fLoadOwnTexts(string psStringsPath)
        {
            string lsCode = UWGrammar.CodeOfPackFile(psStringsPath);

            if (string.IsNullOrEmpty(lsCode))
                return;

            string lsTexts = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(psStringsPath)), lsCode + ".tsv");

            if (!File.Exists(lsTexts))
                return;

            UWTextLanguageFile lOFile = UWText.LoadLanguage(lsCode, lsTexts);

            if (lOFile != null)
                Console.Error.WriteLine("The port's own texts of " + lsCode + " from " + lsTexts + ": " + lOFile.Texts.Count + " texts, "
                    + lOFile.Problems.Count + " lines skipped.");
        }

        private static void fPrintUsage()
        {
            Console.WriteLine("uwdump - reads Ultima Underworld data without a game engine.");
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
            Console.WriteLine("--game uw2 reads the second game from the same game.gog (its own cache): info, strings, find, level,");
            Console.WriteLine("maprender and conv so far.");
            Console.WriteLine("--save <folder> reads the world of a save game (SAVE1 ...) instead of the pristine one.");
            Console.WriteLine("--strings <file> lays a game-text pack (Languages/<code>.strings.tsv) over the English");
            Console.WriteLine("strings, as the game does, e.g. to see a translated conversation with conv --run;");
            Console.WriteLine("its <code>.tsv beside it gives the port's own texts (the look sentence's frames).");
            Console.WriteLine("Levels count from 1, object ids may be written as 0x...; nothing is ever written");
            Console.WriteLine("to the game's folders.");
        }

        /// <summary>The first argument is the data path when it names something on disk or is
        /// written like a path; anything else (a level, a slot, a search word) is the command's. A
        /// .tsv file is a game-text pack, never the data.</summary>
        private static bool fLooksLikePath(string psArg)
        {
            if (string.IsNullOrEmpty(psArg) || psArg.StartsWith("--", StringComparison.Ordinal)
                || psArg.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
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
