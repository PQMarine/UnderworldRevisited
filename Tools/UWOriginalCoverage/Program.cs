using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Writes Docs/ORIGINAL-COVERAGE.md: what of UW.EXE we have never looked at.
    ///
    /// THE COUNTER-PIECE TO THE RULES INDEX. That one says where a place of the original lives
    /// in our code; this one comes from the other end - it walks EVERY routine of the
    /// disassembly and asks which of them our sources have never named. The first sweep of
    /// 2026-09-21 went by name and found four gaps; this goes by SEGMENT, so the 372 routines
    /// the disassembler never named are covered too.
    ///
    /// HOW A ROUTINE IS JUDGED without reading it: by the company it keeps. For every routine
    /// the tool collects which NAMED routines it calls - loading a file, drawing a cursor,
    /// damaging an object, firing a trap. That fingerprint, plus the segment it sits in, says
    /// whether something is engine, presentation, the conversation machine or game logic.
    ///
    /// THE DISASSEMBLY IS NOT PART OF THE REPOSITORY and must never be published. The tool
    /// takes its path as an argument, reads it, and writes nothing out of it but ROUTINE NAMES
    /// and counts - the same thing our comments already carry. If the file is updated (it is
    /// hankmorgan's ongoing work), run this again: the report notes size and hash of the input,
    /// so one can see what it was built from, and msJudged keeps the decisions already taken.
    ///
    ///   dotnet run --project Tools/UWOriginalCoverage -- &lt;UW1_asm.asm&gt; [project root]
    /// </summary>
    internal static class Program
    {
        /// <summary>A routine's head line: "Name proc far".</summary>
        private static readonly Regex mORoutineHead = new Regex(
            @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s+proc\s+far", RegexOptions.Compiled);

        /// <summary>The place part of a name, the same key the rules index uses.</summary>
        private static readonly Regex mOCore = new Regex(
            @"(?:seg|ovr|stub)[0-9]{3}(?:_[0-9A-Fa-f]{1,6})+", RegexOptions.Compiled);

        /// <summary>A call to a routine that carries a readable name.</summary>
        private static readonly Regex mONamedCall = new Regex(
            @"call\s+(?:near ptr\s+|far ptr\s+)?(?<target>[A-Za-z_][A-Za-z0-9_]*_(?:seg|ovr|stub)[0-9]{3}(?:_[0-9A-Fa-f]{1,6})+)",
            RegexOptions.Compiled);

        /// <summary>
        /// What a segment or overlay of the original holds, and why we do or do not care.
        /// Everything not listed counts as game logic and is reported in full.
        ///
        /// THE REASONS MATTER MORE THAN THE VERDICTS: whoever re-runs this against a newer
        /// disassembly should be able to see on what grounds a whole segment was set aside.
        /// </summary>
        private static readonly string[,] msSegments =
        {
            { "seg003", "engine", "text output to the screen" },
            { "seg005", "engine", "the C runtime: files, memory, strings, arithmetic, the RNG" },
            { "seg010", "engine", "the event dispatcher (our click rules sit on top of it)" },
            { "seg013", "engine", "cursor and mouse" },
            { "seg014", "engine", "the sound and music driver, replaced by our own OPL2 emulation" },
            { "seg015", "engine", "string helpers" },
            { "seg036", "engine", "drawing the flasks" },
            { "seg039", "engine", "the message scroll's text" },
            { "seg043", "engine", "message scroll and prompts" },
            { "seg029", "physics", "projectile motion - deliberately Unity's job" },
            { "seg030", "physics", "object motion and collision - deliberately Unity's job" },
            { "seg031", "physics", "bouncing and sliding - deliberately Unity's job" },
            { "seg034", "physics", "player motion and camera - deliberately Unity's job" },
            { "ovr093", "engine", "the overlay loader and its import stubs" },
            { "ovr096", "engine", "file and resource loading" },
            { "ovr105", "conversation", "the cutscene functions, built" },
            { "ovr109", "engine", "start-up and the main loop" },
            { "ovr110", "engine", "error messages" },
            { "ovr111", "engine", "fonts" },
            { "ovr112", "engine", "resource files" },
            { "ovr113", "engine", "resource files" },
            { "ovr114", "engine", "fonts and palettes" },
            { "ovr115", "engine", "art files" },
            { "ovr116", "engine", "art files" },
            { "ovr141", "engine", "bitmaps" },
            { "ovr154", "engine", "file reading" },

            // Whole features of the game that ARE built with us. They are set aside here not
            // because they do not matter but because we went through them once already, in
            // play and against the reference; what is still open about them stands in Todo.md,
            // not here.
            { "ovr092", "built", "the automap" },
            { "ovr094", "built", "conversation built-ins (place_object, add_to_npc_inv, GetQuest) and Gronk's door" },
            { "ovr095", "built", "bartering with an NPC" },
            { "ovr100", "built", "the conversation machine and its screen" },
            { "ovr117", "built", "clicking the inventory slots" },
            { "ovr120", "built", "searching and removing things in the inventory" },
            { "ovr122", "built", "the look texts of every kind of object" },
            { "ovr134", "physics", "camera modes and the moongate pull - Unity's job, except DoTrapCamera, which is built" },
            { "ovr138", "built", "the main menu, the save list and the intro" },
            { "ovr140", "built", "changing level, the save games and their events" },

            // Gone through routine by routine on 2026-09-21, the first segment taken on after
            // the tool was written. All twenty of its unnamed routines belong to the command
            // layer or are its helpers, and every command behind them is built and in daily
            // use: Look with the trap search and its yes/no question (Interaction plus
            // UWTrapDisarming), Use, Pickup with the trigger link it fires, TalkTo, Combat,
            // the weapon toggle, and the driver that calls the player tick.
            { "seg024", "built", "the player commands and the tick driver, each routine checked 2026-09-21" },

            // Gone through 2026-09-21. It is the original's object STORAGE: the fixed arrays
            // of 256 mobile and 768 static slots, their free lists, the chain of a tile and of
            // a container, and the lookups over them. We keep objects in C# lists instead and
            // rebuild exactly this layout when saving (UWLevelWriter), so the mechanics do not
            // transfer - a structural deviation, decided long before this survey. ONE RULE
            // inside it does not follow from the layout and IS missing with us: when the free
            // list runs out, GetFreeObject_seg027_446 calls CullObjects_seg027_2861_329, which
            // sweeps the tile map from far away towards the player and throws low priority
            // objects out. See Todo, "The object limit of a level".
            { "seg027", "storage", "the object arrays, free lists and chains - we keep lists and rebuild the layout on save" },

            // Gone through 2026-09-21. Everything in it is built: sleeping and making camp
            // (UWSleepRules), the mantra at the shrine with the skill gain and both of its
            // messages (UWShrineRules, UWPlayerVitals), hunger, Garamon dreams, eating, the
            // trap search behind the look command, Gate Travel and the teleport lookup, the
            // level-up message. THE ONE GAP is SleepingOnDamagingSurface_ovr143_C7A - sleeping
            // on lava hurts in the original and not with us; it stands in Todo among the four
            // gaps of the name sweep, together with the doors that sleeping closes.
            { "ovr143", "built", "sleep, camp, shrine, hunger, dreams, eating, gate travel - except the damage while sleeping, see Todo" },

            // Gone through 2026-09-21. The trap and special overlay. Built: teleport, change
            // tile, damage trap, spell trap, trespass, bullfrog, the emerald puzzle, the
            // talking door, the platform, fishing, item repair and its durability, the reset
            // of the identification. THREE GAPS, all of them a_do trap actions or a death
            // script, and all in Todo under "The three missing a_do trap actions" and
            // "Tyball's death": the exploding book (quality 41), Free Arial (57) and the
            // quake trap (60 to 62), plus TyballDeath_ovr107_13D1.
            { "ovr107", "built", "traps and special cases - except three do-trap actions and Tyball's death, see Todo" },

            // Gone through 2026-09-21. The trigger and trap core, and it is ours: the trigger
            // dispatcher with its skill check and its recursion, the trap dispatcher, creating
            // a trap for the class 8 spells, disarming, the Reveal spell that fires a look
            // trigger, talking to an NPC - all built in UWTrapRules, UWTrapDisarming and
            // UWAreaSpellRules. TWO THINGS out of it stand in Todo: the doors that sleeping
            // closes (FindAndCloseDoors_ovr153_15C4), and the respawn - our UWRespawnRules
            // was built from the reference and should be held against
            // TriggerCreateObjectTrap_ovr153_1517, which is what the original runs from the
            // player update and from sleeping.
            { "ovr153", "built", "triggers and traps - except the doors on sleeping and a check of the respawn, see Todo" },

            // Gone through 2026-09-21, nothing missing. Goal and gtarg setting (the brain),
            // calming the creatures and tidying the mobile objects when leaving a level
            // (UWLevelExit), the anger of the bystanders at a theft (UWCritterRules over its
            // 15 by 15 tiles), the movement update of the creatures near the player, and the
            // attitude changing spells, whose ally part is a deviation of ours that is written
            // down in UWSummonSpellRules. ONE NAME MISLEADS: CheckNearbyNPCHunger_ovr104_6F1
            // has nothing to do with hunger - it sets NearbyHostileAndAwareOfPlayer and is
            // what Sleep asks before it lets one sleep; we have it as IUWSleepHost.IsEnemyNearby.
            { "ovr104", "built", "attitude, calming on a level change, the anger at a theft, the nearby check before sleeping" },

            // Gone through 2026-09-21. The inventory panel and what happens in it: dragging
            // between the slots, the containers with their capacity (UWContainerCapacity), the
            // question "Move how many?" when a stack is split (UWItemDrag), the weight display,
            // the slot clicks. ONE QUESTION came out of it and stands in Todo: the container
            // logic calls Eat when something is dropped on slot 0, which looks like eating by
            // dropping food on the paperdoll - we do not have that, and it wants one try in
            // the original before anything is built.
            { "ovr121", "built", "the inventory panel, containers, splitting a stack, the weight - except the drop that eats, see Todo" },

            // Gone through 2026-09-21, nothing missing. Character creation and its screens:
            // InitPlayer_ovr098_0 fills the player and his critter row (we cite it in
            // UWPlayerCritterRow), ovr098_582 rolls into PlayerData and PlayerCritterData,
            // the rest writes text and draws the cursor. Ours is UWCharacterGeneration with
            // the creation screens, built and confirmed 2026-09-11. The name
            // MaybeShrine_ovr098_C44 is one of the disassembler guesses and only writes text
            // here; the shrine rules themselves sit in UWShrineRules.
            { "ovr098", "built", "character creation and its screens" },

            // Gone through 2026-09-21, nothing missing. The options panel in the game: its
            // entries call ToggleMusic_seg014_768 and DetailLevel_seg017_50, the rest draws
            // the cursor and handles the clicks, and one branch after a RESTORE puts the
            // weapon away (PutAwayWeapon_seg024_15C0). Ours is UWHudOptions; loading rebuilds
            // the scene with combat mode off, so that last detail comes out the same without
            // a case of its own.
            { "ovr130", "built", "the options panel in the game (UWHudOptions)" },

            // Gone through 2026-09-21, nothing missing. The spell effects: the projectile,
            // area and target classes 5 to 8 (UWRunicMagic, UWAreaSpellRules, UWSpellCasting),
            // the attitude spells cause fear, confusion and paralysis (UWTargetSpell,
            // UWAreaSpellRules), poison, smite undead, sheet lightning, the tremor, reveal,
            // detect monster, the cursed objects, the no-magic bit of a tile, and the helpers
            // that run something over an area or over the whoami list. THE ONE DEVIATION is
            // an old and written down one: we have no ally system, so Ally and the summoned
            // creatures do not fight for the player (UWSummonSpellRules).
            { "seg038", "built", "the spell effects of classes 5 to 9 and their helpers" },

            // Gone through 2026-09-21. Using things and the doors: open, close, toggle, lock,
            // unlock, the key, the lockpick, the container, the spike, the oil flask, the rock
            // hammer, the pole, reading, repairing, the light sources, the wand charges, the
            // enchantment of an item, the "Use ... on what?" prompt, and the clearing of an
            // object with the trigger it fires. TWO GAPS of the name sweep live here and have
            // their own rows: the orb rock on Tyball's orb (TybalsOrb_seg040_A12) and the
            // exploding book. ONE MORE CAME OUT of CalculateAttackScorePlayer_seg022_FF6,
            // which reads this segment's GetItemEnchantment: see Todo, "Difficulty in the
            // attack score".
            { "seg040", "built", "using things and the doors - except the orb, the book and the difficulty bonus, see Todo" },

            // Gone through 2026-09-21. The player's own motion and the state that hangs on it:
            // calculating a step from the command, the initial pass, falling and stopping,
            // bouncing, placing the player in a tile (which the teleport uses), and the tile
            // state with the swim counter. The motion is Unity's job by decision; the tile
            // state and the swim counter are built (UWPlayerTerrain, PLAYER.DAT 0xB9). The
            // QUAKE TRAP also lives here, and its rule went into Todo with the other two
            // missing do-trap actions: quality minus 59 is a bit mask, bit 0 shakes the screen
            // with the intensity from the trap's owner field, bit 1 bounces the player.
            { "seg008", "physics", "the player's motion, with the tile state and swim counter built on our side" },

            // Gone through 2026-09-21. Fourteen of its routines we already name - the goals,
            // NPC_Goto, the turning, the drowning. What is left is the original PATH SEARCH
            // with its tables and the per-creature path cursor (PathFindBetweenTiles,
            // TraverseMultipleTiles, TestTileTraversal, MaybeUpdatePathFlag), plus motion
            // helpers. The search we deliberately did not copy: UWTilePath rebuilds its
            // RESULT, a way across walkable neighbours, and the host keeps the path instead of
            // the record (deviation 14 of Docs/AI/creature-ai.md). That path flag is the
            // one the water edge would have wanted; our stand-in for it is written down in
            // Todo under the drowning row.
            { "seg006", "replaced", "the original path search and its path cursor - UWTilePath and a host-side cache instead" },

            // Gone through 2026-09-21. Combat: ten of its routines we name already (the blow,
            // the attack results, the missile hit, the kill experience). What was left is the
            // player's side of it - the combat loop with charge and release, ending combat,
            // the ammunition check before a shot, the weapon animation of the panel - all
            // built in Interaction and UWPlayerAttack. ONE SMALL THING CAME OUT and was built
            // the same day: SpawnImpactAnimo_seg022_2D2 plays effect 7 beside the impact
            // picture, but only for the player's own missile; ours was silent there.
            { "seg022", "built", "combat, on the player's side too" },

            // Gone through 2026-09-21. Nine routines, all of them writing numbers and names
            // onto the panel: the character page with its attributes and skills, through the
            // font, the cursor and a panel redraw. Nothing but presentation, and ours is
            // UWHudPanel with the stats page one reaches by clicking the chain.
            { "ovr145", "built", "the character page of the side panel (UWHudPanel)" },

            // Gone through 2026-09-21. The ANIMO system: a table of at most 64 entries that
            // carries the class-7 effects - blood, splash, explosions - and advances their
            // frames, plus the detonation of a projectile that spawns several of them at once.
            // We name its two entry points (SpawnClass7Object, ProbablyCreateAnimo); the rest
            // is the table's own bookkeeping, which we replaced: UWLevelLoader.SpawnEffectAt
            // puts the picture down for a fixed time instead of stepping through frames, and
            // the user has seen blood, flash and splash and found them right. ONE THING NOTED:
            // seg044_E9 plays effect 0x0C when a placed animo does not fit in its tile; which
            // case that is in play is unclear and nobody has missed a sound there.
            { "seg044", "replaced", "the animo table of the class-7 effects - we spawn the picture for a fixed time" },

            // Gone through 2026-09-21. The level and savegame FILES: building a path, opening,
            // reading, writing, and the clearing of the object lists that goes with unloading
            // a level. Ours is UWSavegameStore with UWLevelWriter on the writing side and the
            // loader on the reading side, plus UWLevelExit for what a level change tidies up.
            { "ovr118", "built", "the level and savegame files, and clearing the object lists" },

            // Gone through 2026-09-21. Casting from the rune shelf: the attempt, the error when
            // the runes are no spell, the valid cast, looking at one of the active spell icons,
            // and the cursor around it. Ours is UWRunicMagic with UWSpellCasting behind it and
            // UWHudRunes for the shelf and the icons left of the compass.
            { "ovr119", "built", "casting from the rune shelf and the active spell icons" },

            // THE REST, gone through in one pass on 2026-09-21, once no segment had more than
            // five routines left. Each line was checked the same way as the big ones: what the
            // routines call, and whether the feature behind them exists with us.
            { "ovr089", "engine", "reading a data file" },
            { "ovr090", "engine", "one of the full-screen displays" },
            { "ovr091", "engine", "the resource files and the paths to them" },
            { "ovr099", "built", "the rotworm stew and a failure message - eating is built (UWItemUse)" },
            { "ovr101", "engine", "a data file and a roll" },
            { "ovr106", "engine", "registering the event handlers" },
            { "ovr123", "engine", "clearing and building the tile map" },
            { "ovr125", "engine", "reading a data file" },
            { "ovr126", "built", "the acknowledgements, a cutscene (UWIntroPlayer)" },
            { "ovr129", "engine", "resource files" },
            { "ovr131", "built", "the terrain data and the wall and door textures of a level" },
            { "ovr133", "built", "the player's status pass - we name six of its routines; what is left is settings, palette and the list of active spells (UWArmourProtection, UWPlayerCritterRow, UWMazeNavigation)" },
            { "ovr142", "engine", "SHADES.DAT and its kin" },
            { "ovr150", "built", "the loot a death leaves behind (UWCritterLoot)" },
            { "ovr152", "engine", "one of the full-screen displays" },
            { "seg000", "engine", "the cursor" },
            { "seg007", "built", "the creature loop - the goals in it are the most quoted part of our code" },
            { "seg011", "engine", "the game loop's bit field and the screen size" },
            { "seg017", "engine", "rendering" },
            { "seg023", "built", "damage to objects and doors (UWDamageTypes, UWDamageable, the door bashing)" },
            { "seg025", "built", "preparing a projectile - the one path every flying thing takes (UWLevelLoader.SpawnProjectile)" },
            { "seg026", "physics", "placing objects and sorting collisions" },
            { "seg028", "built", "cancelling an active spell" },
            { "seg033", "engine", "the pass over the objects while rendering (called from StartRendering)" },
            { "seg035", "engine", "access to a tile" },
            { "seg037", "built", "the skill check and the refreshing of the panels" },
            { "seg041", "built", "small helpers, among them the end of a mantra" },
            { "seg042", "engine", "arithmetic helpers" },
            { "seg045", "engine", "arithmetic helpers" },
            { "stub09", "engine", "overlay stubs" },
            { "stub10", "engine", "overlay stubs" },
            { "stub15", "engine", "overlay stubs" },
        };

        /// <summary>
        /// Routines judged once and found harmless, with the reason. They keep the report short
        /// on the next run; a routine that is NOT here and not in an engine segment shows up
        /// and wants a decision.
        /// </summary>
        private static readonly string[,] msJudged =
        {
            { "seg038_4EE", "class 5 projectile spells - built (UWSpellCasting and the class rules), the name is simply not quoted" },
            { "seg040_1FF6", "firing an object's trigger link - built in UWTrapRules as the trigger chain" },
            { "seg024_24DC_109B", "the inventory action behind it (skill check plus trigger link) - reached through ovr121, which is built" },
            { "seg040_352B_254B", "the click path into the projectile spells - built through UWCommandMode and UWTargetSpell" },
            { "seg040_352B_16C4", "using a class 4/2 object - bedroll, musical instrument, silver seed, spike, cursed items: all built in UWItemUse and its host (checked 2026-09-21 during the ovr143 pass)" },

            // The two routines the disassembler named without a segment, judged in the final
            // pass of 2026-09-21.
            { "DefuseTrap_sub_8E27D", "disarming a trap - built in UWTrapDisarming with its three messages" },
            { "TalkingDoor_sub_69CB9", "the talking door - built in UWTrapRules as do-trap action 42" },
        };

        /// <summary>
        /// Words in a routine's own name that say it belongs to the presentation, the file
        /// handling or the machinery around them. Judging by name alone would be sloppy; this
        /// is only ever used TOGETHER with the company a routine keeps (fIsPlumbing).
        /// </summary>
        private static readonly string[] msPlumbingWords =
        {
            "Draw", "Render", "Blit", "Palette", "Font", "Text", "String", "Print", "Write",
            "Cursor", "Screen", "Display", "Bitmap", "Image", "Art", "Cutscene", "Splash",
            "Menu", "Button", "Slider", "Panel", "Window", "Scroll", "Refresh", "Redraw",
            "Load", "Save", "File", "Read", "Seek", "Close", "Open", "Decode", "Encode",
            "Alloc", "Free", "Copy", "Append", "Combine", "Sort", "Init", "Setup", "Register",
            "Sound", "Music", "Xmi", "Voc", "Key", "Mouse", "Click", "Cursor", "Colour",
            "Color", "Fade", "Frame", "Anim", "Sprite", "Texture", "Map", "Compass", "Rune",
            "Inventory", "Paperdoll", "Stats", "Dialog", "Question", "Error", "Debug"
        };

        private sealed class Routine
        {
            public string Name;

            public string Core;

            public string Segment;

            public bool IsNamed;

            public readonly List<string> Calls = new List<string>();
        }

        private static int Main(string[] psArgs)
        {
            if (psArgs.Length == 0 || !File.Exists(psArgs[0]))
            {
                Console.WriteLine("uworiginalcoverage <UW1_asm.asm> [project root]");

                return 1;
            }

            string lsAsm = psArgs[0];
            string lsRoot = psArgs.Length > 1 ? psArgs[1] : UWRuleIndexScan.FindProjectRoot();

            if (lsRoot == null || !Directory.Exists(Path.Combine(lsRoot, "Assets")))
            {
                Console.WriteLine("the project root (the folder with Assets) was not found");

                return 1;
            }

            List<Routine> lORoutines = fRead(lsAsm);
            HashSet<string> lOCited = UWRuleIndexScan.CollectCitedCores(lsRoot);
            string lsPath = Path.Combine(lsRoot, "Docs", "ORIGINAL-COVERAGE.md");

            Directory.CreateDirectory(Path.GetDirectoryName(lsPath));
            File.WriteAllText(lsPath, fBuild(lORoutines, lOCited, lsAsm), new UTF8Encoding(false));

            int liOpen = lORoutines.Count(pORoutine => fIsOpen(pORoutine, lOCited, fByName(lORoutines)));

            Console.WriteLine($"{lORoutines.Count} routines in the disassembly, "
                + $"{lORoutines.Count(pORoutine => lOCited.Contains(pORoutine.Core))} of them named in our sources, "
                + $"{liOpen} left to judge");
            Console.WriteLine("written: " + lsPath);

            return 0;
        }

        /// <summary>Reads the disassembly into routines with their calls.</summary>
        private static List<Routine> fRead(string psAsm)
        {
            List<Routine> lORoutines = new List<Routine>();
            Routine lOCurrent = null;

            foreach (string lsLine in File.ReadLines(psAsm))
            {
                Match lOHead = mORoutineHead.Match(lsLine);

                if (lOHead.Success)
                {
                    string lsName = lOHead.Groups["name"].Value;
                    Match lOCore = mOCore.Match(lsName);

                    lOCurrent = new Routine
                    {
                        Name = lsName,
                        Core = lOCore.Success ? lOCore.Value : lsName,
                        Segment = lOCore.Success ? lOCore.Value.Substring(0, 6) : "?",
                        IsNamed = !lsName.StartsWith("seg", StringComparison.Ordinal)
                            && !lsName.StartsWith("ovr", StringComparison.Ordinal)
                            && !lsName.StartsWith("sub_", StringComparison.Ordinal)
                            && !lsName.StartsWith("j_", StringComparison.Ordinal)
                            && !lsName.StartsWith("loc_", StringComparison.Ordinal)
                            && !lsName.StartsWith("stub", StringComparison.Ordinal)
                    };

                    lORoutines.Add(lOCurrent);

                    continue;
                }

                if (lOCurrent == null)
                    continue;

                if (lsLine.Contains(" endp"))
                {
                    lOCurrent = null;

                    continue;
                }

                Match lOCall = mONamedCall.Match(lsLine);

                if (lOCall.Success)
                {
                    string lsTarget = lOCall.Groups["target"].Value;

                    if (lsTarget.StartsWith("j_", StringComparison.Ordinal))
                        lsTarget = lsTarget.Substring(2);

                    if (!lOCurrent.Calls.Contains(lsTarget))
                        lOCurrent.Calls.Add(lsTarget);
                }
            }

            return lORoutines;
        }

        /// <summary>The verdict for a segment, or null when it counts as game logic.</summary>
        private static string fSegmentVerdict(string psSegment, out string psReason)
        {
            for (int liAt = 0; liAt < msSegments.GetLength(0); liAt++)
            {
                if (msSegments[liAt, 0] == psSegment)
                {
                    psReason = msSegments[liAt, 2];

                    return msSegments[liAt, 1];
                }
            }

            psReason = null;

            return null;
        }

        /// <summary>The reason a routine was judged harmless, or null.</summary>
        private static string fJudged(string psCore)
        {
            for (int liAt = 0; liAt < msJudged.GetLength(0); liAt++)
            {
                if (msJudged[liAt, 0] == psCore)
                    return msJudged[liAt, 1];
            }

            return null;
        }

        /// <summary>
        /// Whether a routine belongs to the plumbing: it keeps company ONLY with routines of
        /// segments we set aside, and its own name is one of the presentation or file words.
        /// Both conditions together, because either alone would throw away game logic - a rule
        /// that loads a table reads files too, and a routine called "UseKey" is not a keyboard.
        /// </summary>
        private static bool fIsPlumbing(Routine pORoutine, Dictionary<string, Routine> pOByName)
        {
            bool lbWord = false;

            foreach (string lsWord in msPlumbingWords)
            {
                if (pORoutine.Name.IndexOf(lsWord, StringComparison.Ordinal) >= 0)
                {
                    lbWord = true;

                    break;
                }
            }

            if (!lbWord)
                return false;

            foreach (string lsCall in pORoutine.Calls)
            {
                Routine lOTarget;
                Match lOCore = mOCore.Match(lsCall);
                string lsSegment = lOCore.Success ? lOCore.Value.Substring(0, 6) : "?";

                if (fSegmentVerdict(lsSegment, out _) != null)
                    continue;

                if (pOByName.TryGetValue(lsCall, out lOTarget) && fIsPlumbing(lOTarget, pOByName))
                    continue;

                return false;
            }

            return true;
        }

        /// <summary>
        /// A routine still wanting a decision: our sources never name it, its segment is not
        /// set aside, it carries no judgement of its own and it is not plumbing. Routines that
        /// call nothing and carry no name of their own are entry stubs and drop out.
        /// </summary>
        private static bool fIsOpen(Routine pORoutine, HashSet<string> pOCited, Dictionary<string, Routine> pOByName)
        {
            if (pOCited.Contains(pORoutine.Core) || fJudged(pORoutine.Core) != null)
                return false;

            string lsReason;

            if (fSegmentVerdict(pORoutine.Segment, out lsReason) != null)
                return false;

            if (pORoutine.Calls.Count == 0 && !pORoutine.IsNamed)
                return false;

            return !fIsPlumbing(pORoutine, pOByName);
        }

        /// <summary>The routines by their full name, for following a call.</summary>
        private static Dictionary<string, Routine> fByName(List<Routine> pORoutines)
        {
            Dictionary<string, Routine> lOByName = new Dictionary<string, Routine>(StringComparer.Ordinal);

            foreach (Routine lORoutine in pORoutines)
                lOByName[lORoutine.Name] = lORoutine;

            return lOByName;
        }

        private static string fBuild(List<Routine> pORoutines, HashSet<string> pOCited, string psAsm)
        {
            StringBuilder lOOut = new StringBuilder();
            FileInfo lOFile = new FileInfo(psAsm);

            lOOut.AppendLine("# What of the original we have not looked at");
            lOOut.AppendLine();
            lOOut.AppendLine("GENERATED by `Tools/UWOriginalCoverage` - do not edit. Rebuild with:");
            lOOut.AppendLine();
            lOOut.AppendLine("```");
            lOOut.AppendLine("dotnet run --project Tools/UWOriginalCoverage -- <path to UW1_asm.asm>");
            lOOut.AppendLine("```");
            lOOut.AppendLine();
            lOOut.AppendLine("The counter-piece to `RULES-INDEX.md`: that one says where a place of the original");
            lOOut.AppendLine("lives in our code, this one walks every routine of the disassembly and asks which");
            lOOut.AppendLine("we have never named. Only names and counts are written here, never code - the");
            lOOut.AppendLine("disassembly itself stays out of the repository.");
            lOOut.AppendLine();
            lOOut.AppendLine("Built from a disassembly of " + lOFile.Length.ToString("N0") + " bytes, "
                + fHash(psAsm) + ". If that file is updated, run the tool again and compare.");
            lOOut.AppendLine();

            int liNamed = pORoutines.Count(pORoutine => lOCitedContains(pOCited, pORoutine));
            Dictionary<string, Routine> lOByName = fByName(pORoutines);
            List<Routine> lOOpen = pORoutines.Where(pORoutine => fIsOpen(pORoutine, pOCited, lOByName)).ToList();

            lOOut.AppendLine("| | Routines |");
            lOOut.AppendLine("|---|---|");
            lOOut.AppendLine("| In the disassembly | " + pORoutines.Count + " |");
            lOOut.AppendLine("| Named in our sources | " + liNamed + " |");
            lOOut.AppendLine("| In a segment we set aside (engine, presentation, physics, conversation) | "
                + pORoutines.Count(pORoutine => !lOCitedContains(pOCited, pORoutine)
                    && fSegmentVerdict(pORoutine.Segment, out _) != null) + " |");
            lOOut.AppendLine("| Plumbing by name and company (fIsPlumbing) | "
                + pORoutines.Count(pORoutine => !lOCitedContains(pOCited, pORoutine)
                    && fSegmentVerdict(pORoutine.Segment, out _) == null
                    && fIsPlumbing(pORoutine, lOByName)) + " |");
            lOOut.AppendLine("| Judged one by one (msJudged) | "
                + pORoutines.Count(pORoutine => !lOCitedContains(pOCited, pORoutine)
                    && fJudged(pORoutine.Core) != null) + " |");
            lOOut.AppendLine("| Left to judge | " + lOOpen.Count + " |");
            lOOut.AppendLine();

            lOOut.AppendLine("## Left to judge");
            lOOut.AppendLine();
            lOOut.AppendLine("Everything here sits in a segment that holds game logic and is named nowhere in");
            lOOut.AppendLine("our sources. The second column is the company it keeps: the named routines it");
            lOOut.AppendLine("calls, which usually says what it is without reading a line of it.");
            lOOut.AppendLine();

            if (lOOpen.Count == 0)
            {
                lOOut.AppendLine("None.");
            }
            else
            {
                lOOut.AppendLine("| Routine | Calls |");
                lOOut.AppendLine("|---|---|");

                foreach (Routine lORoutine in lOOpen.OrderBy(pORoutine => pORoutine.Name, StringComparer.Ordinal))
                {
                    lOOut.AppendLine("| `" + lORoutine.Name + "` | "
                        + (lORoutine.Calls.Count == 0 ? "-" : string.Join(", ", lORoutine.Calls.Take(8))) + " |");
                }
            }

            lOOut.AppendLine();
            lOOut.AppendLine("## By segment");
            lOOut.AppendLine();
            lOOut.AppendLine("| Segment | Routines | Named by us | Left to judge | Verdict |");
            lOOut.AppendLine("|---|---|---|---|---|");

            foreach (IGrouping<string, Routine> lOGroup in pORoutines
                .GroupBy(pORoutine => pORoutine.Segment)
                .OrderBy(pOGroup => pOGroup.Key, StringComparer.Ordinal))
            {
                string lsReason;
                string lsVerdict = fSegmentVerdict(lOGroup.Key, out lsReason);

                lOOut.AppendLine("| " + lOGroup.Key + " | " + lOGroup.Count() + " | "
                    + lOGroup.Count(pORoutine => lOCitedContains(pOCited, pORoutine)) + " | "
                    + lOGroup.Count(pORoutine => fIsOpen(pORoutine, pOCited, lOByName)) + " | "
                    + (lsVerdict == null ? "game logic" : lsVerdict + ": " + lsReason) + " |");
            }

            lOOut.AppendLine();
            lOOut.AppendLine("## Judged one by one");
            lOOut.AppendLine();
            lOOut.AppendLine("| Routine | Why it needs nothing |");
            lOOut.AppendLine("|---|---|");

            for (int liAt = 0; liAt < msJudged.GetLength(0); liAt++)
                lOOut.AppendLine("| `" + msJudged[liAt, 0] + "` | " + msJudged[liAt, 1] + " |");

            lOOut.AppendLine();

            return lOOut.ToString();
        }

        private static bool lOCitedContains(HashSet<string> pOCited, Routine pORoutine)
        {
            return pOCited.Contains(pORoutine.Core);
        }

        /// <summary>A short hash of the input, so one can tell two disassemblies apart.</summary>
        private static string fHash(string psPath)
        {
            using (FileStream lOStream = File.OpenRead(psPath))
            using (SHA256 lOHash = SHA256.Create())
            {
                byte[] lyHash = lOHash.ComputeHash(lOStream);

                return "sha256 " + BitConverter.ToString(lyHash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
