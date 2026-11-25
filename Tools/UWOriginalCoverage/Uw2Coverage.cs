using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Writes Docs/ORIGINAL-COVERAGE-UW2.md: what of UW2.EXE's routines we have never named (UW2 plan, the beta gate,
    /// 2026-11-25).
    ///
    /// THE SECOND GAME'S COUNTERPART to ORIGINAL-COVERAGE.md. Its source is Underworld Exhumed's decompilation of UW2.EXE
    /// (uw2/src, one C file per module, every function a line ending in ')' followed by '{' in the first column). The tool
    /// walks every function there and asks whether our sources name it anywhere - a comment citing "uw2 ui/INTERACT.C
    /// mous_in_inv" names mous_in_inv. A function is judged without reading it, by the company it keeps: the functions it
    /// calls, which usually say what it is.
    ///
    /// THE BETA GATE'S SCOPE: the classic interface (ui, inv, the creation screen) is listed function by function; the other
    /// folders - the rules, which the play-through against the original covered - are counted only. Nothing is written out
    /// of the decompilation but FUNCTION NAMES and counts, as our comments already carry them; Underworld Exhumed's C is
    /// never copied (UW2 plan, decision 2).
    ///
    ///   dotnet run --project Tools/UWOriginalCoverage -- --uw2 &lt;UnderworldExhumed/uw2/src&gt; [project root]
    /// </summary>
    internal static class Uw2Coverage
    {
        /// <summary>A function's head: a line ending in ')' - its name is the last word before the first '('.</summary>
        private static readonly Regex mOHead = new Regex(@"^(?!\s)(?!#)(?!typedef)(?<sig>[^;{}]*?\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^;{}]*\))\s*$",
            RegexOptions.Compiled);

        /// <summary>A word that may be a call: a name followed by '('.</summary>
        private static readonly Regex mOCall = new Regex(@"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);

        /// <summary>A word of our sources.</summary>
        private static readonly Regex mOWord = new Regex(@"[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

        /// <summary>
        /// The folders and files of the beta gate's scope (the classic interface, listed one by one) - a folder name or
        /// "folder/FILE.C". The rest is counted only.
        /// </summary>
        private static readonly string[] msScope = { "ui", "inv", "game/CHARGEN.C" };

        /// <summary>
        /// Functions judged one by one: the name and why it needs nothing of ours (or where it is covered without being
        /// named). Each with its reason, so a later run against a newer decompilation can see the grounds.
        /// </summary>
        private static readonly string[,] msJudged =
        {
            { "CloseTheBag", "inv, 2026-11-25: covered - the open bag's own place closes it, back to the bag around or the pack (UWInventoryModel.CloseContainer from UWGameScreenUw2's display click; DoSpecialActions, named, is its caller)" },
            { "DisplayOpenBag", "inv, 2026-11-25: covered - the open bag's eight places and its arrows by whether more lies above or below (UWGameScreenUw2's page, the model's CanIncrease/CanDecreaseContainerScrollOffset)" },
            { "SwapItemsInBag", "inv, 2026-11-25: covered - an item put on an occupied place of the open bag swaps with it, and one that does not fit stays on the pointer (UWInventoryModel.DropCursorItemInContainerAt, the first game's rule; AddToOccupiedSlot, named)" },
            { "RedisplayInvSlot", "inv, 2026-11-25: plumbing - redraws one place" },
            { "FindEmptySlot", "inv, 2026-11-25: covered - the first free place of the pack (the model's first free backpack slot)" },
            { "AddToInventory", "inv, 2026-11-25: covered - an object into a place, tested by ItemFitsSlot and weighed (the model's drops; its callers - the creation's starting things, the hand swap of USEITEMS, AddToEmptySlot - are named or covered)" },
            { "WhatsInSlot", "inv, 2026-11-25: plumbing - reads a place" },
            { "InvRemoveObject", "inv, 2026-11-25: covered - a wrapper of invRemoveObject (named)" },
            { "RemoveAllFromSlot", "inv, 2026-11-25: covered - a place's whole stack taken out (the model's take onto the pointer, SetCursorObj named)" },
            { "RemoveOneFromSlot", "inv, 2026-11-25: covered - one piece of a place's stack (the shot's ammunition, MISSILE.C's fire, named)" },
            { "removeFromSlot", "inv, 2026-11-25: covered - the common part of RemoveAll/RemoveOneFromSlot" },
            { "takeFromSlot", "inv, 2026-11-25: covered - splits a stack or takes it whole, with the weight (the model's split and take)" },
            { "EndInventory", "inv, 2026-11-25: needs nothing - empty in the original (Exhumed's note: DoesNothing_ovr125_1E6)" },
            { "DoInventoryDrag", "inv, 2026-11-25: covered - a thing taken from the world with the button held lands where the button is let go (UWItemDrag, UWClassicDrop; player_3dget, named, is its caller)" },
            { "load_inv_pic", "inv, 2026-11-25: covered - the armour's pictures from ARMOR_F or ARMOR_M by the player's sex (UWGameScreenUw2's paperdoll)" },
            { "AddToEmptySlot", "inv, 2026-11-25: covered - the pointer's object into an empty place, never the open bag's own (the model's drops, UWGameScreenUw2.fPutInto)" },
            { "Punt_player_inv", "inv, 2026-11-25: plumbing - frees the original's in-memory inventory before a restore; our save game writes and reads the inventory itself" },
            { "replaceInInv", "inv, 2026-11-25: plumbing - the original's inventory save copy" },
            { "allocSaveObj", "inv, 2026-11-25: plumbing - the original's inventory save copy" },
            { "getSaveObj", "inv, 2026-11-25: plumbing - the original's inventory save copy" },
            { "reset_scrgr", "panels, 2026-11-25: plumbing - resets the display state at the editor's restore (UWEDIT)" },
            { "hold_scrgr", "panels, 2026-11-25: plumbing - stops a slide where it is, at the editor's restore (UWEDIT)" },
            { "free_scrgr", "panels, 2026-11-25: plumbing - frees the slide's buffer" },
            { "adjust_compass", "panels, 2026-11-25: TO CHECK IN THE ORIGINAL - it turns the compass one step of sixteen per update the short way towards the heading; ours shows the heading at once (UWGameScreenUw2's compass), which differs only when the heading jumps (a teleport, a load). Test list section 218" },
            { "adjust_panel", "panels, 2026-11-25: covered - the panel's slide (UWGameScreenUw2.fSlideStep, UWPanelTurn.Slides; do_panel_frame named)" },
            { "adr_weapon", "panels, 2026-11-25: plumbing - the EMS buffer of the weapon's pictures" },
            { "move_weapon", "panels, 2026-11-25: plumbing - the EMS buffer of the weapon's pictures" },
            { "init_panelflip", "panels, 2026-11-25: plumbing - draws the page that slides in into a buffer (our pages are drawn as they are)" },
            { "free_panelflip", "panels, 2026-11-25: needs nothing - empty in the original" },
            { "pretty_panelagain", "panels, 2026-11-25: covered - redraws the page in full; ours follow their values (fFollowExperience, the inventory's change event)" },
            { "restore_sliding_panel", "panels, 2026-11-25: covered - the options end a slide at once and take the panel (UWGameScreenUw2's options open)" },
            { "send_FB", "panels, 2026-11-25: plumbing - copies the 3D frame buffer to the screen" },
            { "player_look_shaft", "panels, 2026-11-25: needs nothing - empty in the original" },
            { "player_look_grave", "panels, 2026-11-25: needs nothing - empty in the original" },
            { "panel_check", "panels, 2026-11-25: covered - the stats page redraws when the experience, vitality or mana move (fFollowExperience); the level-up, the skill points and the sleep, its callers, all move one of them" },
            { "term_icon_buttons", "panels, 2026-11-25: plumbing - removes the command bar's mouse areas" },
            { "new_IconUnselect", "panels, 2026-11-25: covered - the command bar drawn plain with the lit icon framed (UWGameScreenUw2's command bar)" },
            { "RedispInv", "panels, 2026-11-25: covered - the inventory page redrawn (after a conversation too); ours follows the inventory's change event" },
            { "BridgeHeight", "panels, 2026-11-25: covered - the highest bridge on a tile for BlockingTerrain (UWReachRules' bridge height)" },
            { "seg026_2716_F8A", "panels, 2026-11-25: needs nothing - never called in DOS (Exhumed's note)" },
            { "seg026_2716_F98", "panels, 2026-11-25: needs nothing - never called in DOS (Exhumed's note)" },
            { "start_gameinp", "panels, 2026-11-25: plumbing - registers the game screen's mouse areas (ours: UWClickRules' areas)" },
            { "clear_gameinp", "panels, 2026-11-25: plumbing - removes them" },
            { "init_gamedisp", "panels, 2026-11-25: plumbing - sets up the game screen (UWGameScreenUw2 builds its own)" },
            { "clear_gamedisp", "panels, 2026-11-25: plumbing - tears it down" },
            { "set_mouse_in", "scroll, 2026-11-25: plumbing - hides the pointer while the scroll under it is drawn" },
            { "do_main_scroll", "scroll, 2026-11-25: plumbing - chooses the game's scroll for printing (ours prints to the scroll that shows)" },
            { "do_npc_scroll", "scroll, 2026-11-25: plumbing - chooses the conversation's scroll" },
            { "do_play_scroll", "scroll, 2026-11-25: plumbing - chooses the menu scroll" },
            { "scroll_up", "scroll, 2026-11-25: covered - a line up, the freed line in the paper colour and the edges rolled (UWMessageLogModel, UWGameScreenUw2.fApplyEdges; draw_edges named)" },
            { "scroll_wrap", "scroll, 2026-11-25: covered - the word wrap at the last space, a word longer than a line broken on a fresh line (UWTextLayout.WrapText, the first game's same routine)" },
            { "draw_scroll", "scroll, 2026-11-25: covered - clears the box in the paper colour 0x71 (UWGameScreenUw2's paper, MessageLog.PaperColour)" },
            { "seg043_3619_669", "scroll, 2026-11-25: needs nothing - never called in UW2.EXE (Exhumed's note)" },
            { "init_scroll", "scroll, 2026-11-25: covered - the game's scroll cleared with its edges at the game screen's start (UWGameScreenUw2.fBuildScroll)" },
            { "pick_scroll", "scroll, 2026-11-25: plumbing - the scroll an answer is typed into, the conversation's or the game's (ours: the prompt's line where the host puts it, UWCountAskUw2)" },
            { "wd_replace", "scroll, 2026-11-25: covered - the count typed after \"Move how many?\" rewritten by a click's one or all (the first game's UWCountPrompt: a left click one, a right click all; AskHowMany named)" },
            { "wd_bool", "scroll, 2026-11-25: covered - the yes/no answer rewritten in place by Y and N, No on Escape (UWYesNoPrompt's line, UWGameScreenUw2.fShowYesNoLine; wyorn named)" },
            { "init_strings", "texts, 2026-11-25: plumbing - opens STRINGS.PAK (ours: UWStrings reads it)" },
            { "make_string", "texts, 2026-11-25: covered - a conversation's strings made at run time and the player's name (UWConversationVM's own string table, the name from the player record)" },
            { "replace_string", "texts, 2026-11-25: covered - the same, a made string pointed at new text (the conversation's ask string)" },
            { "clear_dynamics", "texts, 2026-11-25: covered - a conversation's made strings dropped at its end" },
            { "game_strings_3", "texts, 2026-11-25: covered - up to three game strings run together (the status lines of print_info, fSayStatus, and the hunger's, both named)" },
            { "LoadFileStringsPak_seg039_547", "texts, 2026-11-25: plumbing - reads STRINGS.PAK's Huffman tree (UWStrings)" },
            { "seg039_3452_5E1", "texts, 2026-11-25: plumbing - frees the strings" },
            { "read_string", "texts, 2026-11-25: plumbing - decodes a string from STRINGS.PAK (UWStrings)" },
            { "seg039_3452_781", "texts, 2026-11-25: plumbing - the Huffman decoder's bit reader" },
            { "seg039_3452_7B2", "texts, 2026-11-25: plumbing - the Huffman tree's walk" },
            { "seg039_3452_857", "texts, 2026-11-25: plumbing - lower-cases a string for the conversation's comparisons (C#'s own)" },
            { "seg039_3452_89A", "texts, 2026-11-25: plumbing - atoi by hand (the conversation's number reading)" },
            { "str_cat", "texts, 2026-11-25: plumbing - strcat" },
            { "init_mouse", "mouse, 2026-11-25: plumbing - the mouse driver's start (ours: UWPointer)" },
            { "_actual_mhide", "mouse, 2026-11-25: plumbing - hides the drawn cursor and restores what lay under it" },
            { "mouse_show", "mouse, 2026-11-25: plumbing - draws the cursor (ours: UWPointer's picture)" },
            { "mous_3d_set", "mouse, 2026-11-25: plumbing - the 3D view's rectangle for drawing the cursor into the frame buffer" },
            { "mous_in_3d_p", "mouse, 2026-11-25: plumbing - whether the pointer is in that rectangle (ours: IsInViewWindow)" },
            { "mous_3d_hide", "mouse, 2026-11-25: plumbing - the cursor drawn into the frame buffer" },
            { "mous_3d_show", "mouse, 2026-11-25: needs nothing - empty in the original" },
            { "mouse_getxy", "mouse, 2026-11-25: plumbing - the pointer's place (UWPointer.Position)" },
            { "mouse_Qgetxy", "mouse, 2026-11-25: plumbing - the queued press's place" },
            { "mouse_clearQ", "mouse, 2026-11-25: plumbing - clears the one-deep press queue" },
            { "flush_keys", "mouse, 2026-11-25: plumbing - empties the keyboard buffer" },
            { "mouse_getbut", "mouse, 2026-11-25: plumbing - the buttons held (UWMouseButtons)" },
            { "mouse_freereign", "mouse, 2026-11-25: plumbing - lifts the pointer's constraint box (ours: UWPointer.ClampTo while a walk holds it)" },
            { "do_mouse_input", "mouse, 2026-11-25: plumbing - replays a press queued while the game was busy; ours reads every frame, nothing waits" },
            { "do_keyboard_input", "mouse, 2026-11-25: plumbing - reads a key with Ctrl, Alt and Shift (ours: the Input System, UWControls)" },
            { "get_input", "mouse, 2026-11-25: plumbing - alternates mouse and keyboard into one event (ours: the Input System)" },
            { "mouse_get_input_sp", "mouse, 2026-11-25: plumbing - the same" },
            { "defineMouseRegion", "mouse, 2026-11-25: covered - the cursor regions that pick the pointer's picture (the view's arrows by RegionCursor, UWGameScreenUw2.fUpdatePointer)" },
            { "undefineMouseRegion", "mouse, 2026-11-25: covered - the same" },
            { "unforce_mouse_cursor", "mouse, 2026-11-25: covered - a forced pointer (the red X, the aim, the thing on the pointer) given back (UWGameScreenUw2.fUpdatePointer; force_mouse_cursor named)" },
            { "mouse_check_reg", "mouse, 2026-11-25: plumbing - a box test" },
            { "checkMouse", "mouse, 2026-11-25: covered - the region under the pointer picks the picture (fUpdatePointer)" },
            { "port_mouse_look", "mouse, 2026-11-25: needs nothing - Underworld Exhumed's own mouse look, not in UW2.EXE" },
            { "mouse_look_set", "mouse, 2026-11-25: needs nothing - Underworld Exhumed's own mouse look, not in UW2.EXE" },
            { "port_mouse_look_toggle", "mouse, 2026-11-25: needs nothing - Underworld Exhumed's own mouse look, not in UW2.EXE" },
            { "port_mouse_look_screen", "mouse, 2026-11-25: needs nothing - Underworld Exhumed's own mouse look, not in UW2.EXE" },
            { "mouse_look_step", "mouse, 2026-11-25: needs nothing - Underworld Exhumed's own mouse look, not in UW2.EXE" },
            { "moveMouse", "mouse, 2026-11-25: plumbing - polls the driver and redraws the cursor (UWPointer); the keyboard warp it carries out is keyboard_mouse's" },
            { "MousQUp", "mouse, 2026-11-25: plumbing - queues a press seen while the 3D view renders; ours reads every frame" },
            { "MousReSave", "mouse, 2026-11-25: plumbing - the cursor's save-under" },
            { "MousReSave3d", "mouse, 2026-11-25: plumbing - the same in the frame buffer" },
            { "drawMouse", "mouse, 2026-11-25: plumbing - draws the cursor" },
            { "draw3dMouse", "mouse, 2026-11-25: plumbing - draws the cursor into the frame buffer" },
            { "keyboard_mouse", "mouse, 2026-11-25: DEVIATION, BOTH GAMES (Exhumed: UW1's MOUSE.C has it too) - Tab and Shift+Tab jump the pointer between the view, the panel and the bottom right, the keypad's 1 to 9 warp it towards that edge or corner; left out on purpose in the first game already, Tab is the help (per user 2026-11-25; Docs/DEVIATIONS.md, no keyboard pointer)" },
            { "warpMouse", "mouse, 2026-11-25: DEVIATION, BOTH GAMES - the keyboard warp keyboard_mouse starts; see there" },
            { "mouse_btns", "mouse, 2026-11-25: DEVIATION, BOTH GAMES - INS and DEL as the left and right button, and Alt+H's mouse_hand swapping the two; left out with the keyboard pointer, see keyboard_mouse" },
            { "init_input", "mouse, 2026-11-25: plumbing - the key table's start (ours: UWControls, UWHotkeys)" },
            { "free_input", "mouse, 2026-11-25: plumbing - frees it" },
            { "input_del", "mouse, 2026-11-25: plumbing - removes a key or area handler" },
            { "input_dispatch", "mouse, 2026-11-25: covered - a key or a click to its handler (ours: UWControls' actions, UWHotkeys' sites, UWClickRules' areas)" },
            { "dispatch_key", "mouse, 2026-11-25: covered - the same for a key" },
            { "seg011_6", "mouse, 2026-11-25: plumbing - the joystick read (ours: the gamepad, our own scheme)" },
            { "seg011_12B", "mouse, 2026-11-25: plumbing - the joystick calibration's wait" },
            { "JoyStickCalibration_seg011_1B8", "mouse, 2026-11-25: needs nothing - the DOS joystick calibration; ours reads gamepads through the Input System" },
            { "seg011_2C6", "mouse, 2026-11-25: plumbing - the joystick's position" },
            { "donothing_opt", "options, 2026-11-25: needs nothing - empty in the original" },
            { "resume_play_opt", "options, 2026-11-25: covered - Return clears the scroll and ends the options (UWOptionsPanelUw2's Close, UWGameScreenUw2.ClearScroll)" },
            { "detail_set_opt", "options, 2026-11-25: covered - a detail button sets the level at once and shows its message (UWOptionsPanelUw2's SetDetail, UWGraphicsDetail.Choose)" },
            { "done_ovr136_315", "options, 2026-11-25: covered - Done ends the options (UWOptionsPanelUw2)" },
            { "quit_do_opt", "options, 2026-11-25: covered - Yes leaves the game, No goes back (UWOptionsPanelUw2's Quit)" },
            { "do_saverest_opt", "options, 2026-11-25: covered - saves or restores the slot and remembers it as the last one; a restore starts with the weapon away and no mode (ours loads the save anew; UWGameScreenUw2's msiLastSlot)" },
            { "do_musicsound_opt", "options, 2026-11-25: covered - music or sound on or off with the message (UWOptionsPanelUw2's SetMusic and SetSound)" },
            { "null_group_fun", "options, 2026-11-25: needs nothing - empty in the original" },
            { "game_group_fun", "options, 2026-11-25: covered - the main page lights its first button (UWOptionsPanelUw2.Open)" },
            { "detail_group_fun", "options, 2026-11-25: covered - the detail page lights the current level and shows its message (UWOptionsPanelUw2.Open)" },
            { "detail_setting_message", "options, 2026-11-25: covered - the detail level's message row (UWOptionsPanelUw2.MessageRow)" },
            { "quit_group_fun", "options, 2026-11-25: covered - the quit page lights No (UWOptionsPanelUw2.Open)" },
            { "file_group_fun", "options, 2026-11-25: covered - the file page lights the slot last used, \"Restore Game\" when restoring (UWOptionsPanelUw2.LastSlot)" },
            { "musicsound_group_fun", "options, 2026-11-25: covered - the music or sound page lights On or Off as it is now, with its message (UWOptionsPanelUw2.Open)" },
            { "ButtonDoNothing_ovr136_5CB", "options, 2026-11-25: needs nothing - empty in the original" },
            { "new_hilit_button", "options, 2026-11-25: covered - lights a button (UWOptionsPanelUw2.Light)" },
            { "blit_panel_to_main", "options, 2026-11-25: plumbing - a rectangle drawn over the panel's area" },
            { "adr_opbtn", "main menu, 2026-11-25: plumbing - gronk_gr's callback loading the menu buttons" },
            { "move_opbtn", "main menu, 2026-11-25: plumbing - the same" },
            { "UnknownAutomapLoop_ovr147_A56", "main menu, 2026-11-25: needs nothing - never called (Exhumed's note)" },
            { "Region_ovr147_A73", "main menu, 2026-11-25: needs nothing - its callback, never called" },
            { "set_skills", "creation, 2026-11-25: covered - the chosen skills raised by add_to_skill (named; UWCharacterCreationScreenUw2, UWCharacterGeneration)" },
            { "adr_chrpic", "creation, 2026-11-25: plumbing - gronk_gr's callback loading the creation's buttons" },
            { "move_chrpic", "creation, 2026-11-25: plumbing - the same" },
            { "strt_chargen", "creation, 2026-11-25: covered - loads the creation screen, its palette, buttons and SKILLS.DAT and runs gen_char (named; UWCharacterCreationScreenUw2)" },
            { "AddObjIfAny_ovr101_18CB", "creation, 2026-11-25: needs nothing - not referenced in DOS (Exhumed's note)" },
            { "ReturnOne_ovr094_2A1", "automap, 2026-11-25: needs nothing - the callback of ovr094_2A8" },
            { "ovr094_2A8", "automap, 2026-11-25: needs nothing - a DOS-only routine mapping and drawing the whole level, not reached from the game's controls" },
            { "ClearAutoMap", "automap, 2026-11-25: plumbing - clears the map's seen marks for a new game (UWAutomapStateUw2)" },
            { "ShowDungeonMap", "automap, 2026-11-25: covered - the seen tiles, three pixels a tile, their sides shaded against unseen or solid neighbours (UWAutomapPainter, the first game's painter with the second game's ice; DoTile named)" },
            { "ShadeSide", "automap, 2026-11-25: covered - a tile's side shaded (UWAutomapPainter)" },
            { "PixelDarken", "automap, 2026-11-25: covered - one pixel darkened (UWAutomapPainter's steps)" },
            { "DoDoorTile", "automap, 2026-11-25: covered - a door's mark on the map (UWAutomapPainter.fDoor)" },
            { "CornerShade_ovr094_9D2", "automap, 2026-11-25: covered - the corner pixel where two shaded sides meet (UWAutomapPainter's corner)" },
            { "GetTheWords", "automap, 2026-11-25: covered - a level's notes read from the save (UWMapNotesModel, UWAutomapStateUw2)" },
            { "automap_scr", "automap, 2026-11-25: covered - keeps the music going while the map shows (the music runs on its own)" },
            { "port_map_key", "automap, 2026-11-25: needs nothing - Underworld Exhumed's own key, not in UW2.EXE" },
        };

        private sealed class Function
        {
            public string Name;

            public string File;

            public readonly List<string> Calls = new List<string>();
        }

        public static int Run(string[] psArgs)
        {
            if (psArgs.Length < 2 || !Directory.Exists(psArgs[1]))
            {
                Console.WriteLine("uworiginalcoverage --uw2 <UnderworldExhumed/uw2/src> [project root]");

                return 1;
            }

            string lsSource = Path.GetFullPath(psArgs[1]);
            string lsRoot = psArgs.Length > 2 ? psArgs[2] : UWRuleIndexScan.FindProjectRoot();

            if (lsRoot == null || !Directory.Exists(Path.Combine(lsRoot, "Assets")))
            {
                Console.WriteLine("the project root (the folder with Assets) was not found");

                return 1;
            }

            List<Function> lOFunctions = fRead(lsSource);
            HashSet<string> lOWords = fOurWords(lsRoot);
            string lsPath = Path.Combine(lsRoot, "Docs", "ORIGINAL-COVERAGE-UW2.md");

            File.WriteAllText(lsPath, fBuild(lOFunctions, lOWords, lsSource), new UTF8Encoding(false));

            List<Function> lOScope = lOFunctions.Where(fInScope).ToList();

            Console.WriteLine($"{lOFunctions.Count} functions, {lOFunctions.Count(pO => lOWords.Contains(pO.Name))} named in our sources; "
                + $"in the interface's scope {lOScope.Count}, {lOScope.Count(pO => fIsOpen(pO, lOWords))} left to judge");
            Console.WriteLine("written: " + lsPath);

            return 0;
        }

        /// <summary>Every function of every C file under the source folder, with the known functions it calls.</summary>
        private static List<Function> fRead(string psSource)
        {
            List<Function> lOFunctions = new List<Function>();
            Dictionary<Function, List<string>> lOBodies = new Dictionary<Function, List<string>>();

            foreach (string lsFile in Directory.GetFiles(psSource, "*.C", SearchOption.AllDirectories).OrderBy(ps => ps, StringComparer.Ordinal))
            {
                string lsRelative = Path.GetRelativePath(psSource, lsFile).Replace('\\', '/');

                if (lsRelative.StartsWith("port/", StringComparison.Ordinal))
                    continue;

                string[] lsLines = File.ReadAllLines(lsFile);
                Function lOCurrent = null;
                List<string> lOBody = null;

                for (int liAt = 0; liAt < lsLines.Length; liAt++)
                {
                    string lsLine = lsLines[liAt];

                    if (lOCurrent != null)
                    {
                        if (lsLine.StartsWith("}", StringComparison.Ordinal))
                        {
                            lOCurrent = null;
                            continue;
                        }

                        lOBody.Add(lsLine);
                        continue;
                    }

                    if (!lsLine.StartsWith("{", StringComparison.Ordinal) || liAt == 0)
                        continue;

                    Match lOHead = mOHead.Match(lsLines[liAt - 1]);

                    if (!lOHead.Success)
                        continue;

                    lOCurrent = new Function { Name = lOHead.Groups["name"].Value, File = lsRelative };
                    lOBody = new List<string>();
                    lOFunctions.Add(lOCurrent);
                    lOBodies[lOCurrent] = lOBody;
                }
            }

            HashSet<string> lONames = new HashSet<string>(lOFunctions.Select(pO => pO.Name), StringComparer.Ordinal);

            foreach (KeyValuePair<Function, List<string>> lOPair in lOBodies)
            {
                foreach (string lsLine in lOPair.Value)
                {
                    foreach (Match lOCall in mOCall.Matches(lsLine))
                    {
                        string lsName = lOCall.Groups["name"].Value;

                        if (lONames.Contains(lsName) && lsName != lOPair.Key.Name && !lOPair.Key.Calls.Contains(lsName))
                            lOPair.Key.Calls.Add(lsName);
                    }
                }
            }

            return lOFunctions;
        }

        /// <summary>Every word of our sources (Assets and Tools, C# and shaders): a function named there in a comment or in
        /// code counts as named.</summary>
        private static HashSet<string> fOurWords(string psRoot)
        {
            HashSet<string> lOWords = new HashSet<string>(StringComparer.Ordinal);

            foreach (string lsFolder in new[] { "Assets", "Tools" })
            {
                string lsPath = Path.Combine(psRoot, lsFolder);

                if (!Directory.Exists(lsPath))
                    continue;

                foreach (string lsFile in Directory.EnumerateFiles(lsPath, "*.*", SearchOption.AllDirectories))
                {
                    string lsExtension = Path.GetExtension(lsFile).ToLowerInvariant();

                    if ((lsExtension != ".cs" && lsExtension != ".shader" && lsExtension != ".hlsl")
                        || lsFile.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                        || lsFile.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        || lsFile.EndsWith("Uw2Coverage.cs", StringComparison.Ordinal))
                        continue;

                    foreach (Match lOWord in mOWord.Matches(File.ReadAllText(lsFile)))
                        lOWords.Add(lOWord.Value);
                }
            }

            return lOWords;
        }

        private static bool fInScope(Function pOFunction)
        {
            foreach (string lsScope in msScope)
            {
                if (lsScope.Contains('/') ? pOFunction.File == lsScope : pOFunction.File.StartsWith(lsScope + "/", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string fJudged(string psName)
        {
            for (int liAt = 0; liAt < msJudged.GetLength(0); liAt++)
            {
                if (msJudged[liAt, 0] == psName)
                    return msJudged[liAt, 1];
            }

            return null;
        }

        private static bool fIsOpen(Function pOFunction, HashSet<string> pOWords)
        {
            return fInScope(pOFunction) && !pOWords.Contains(pOFunction.Name) && fJudged(pOFunction.Name) == null;
        }

        private static string fBuild(List<Function> pOFunctions, HashSet<string> pOWords, string psSource)
        {
            StringBuilder lOOut = new StringBuilder();

            lOOut.AppendLine("# What of the second game's original we have not looked at");
            lOOut.AppendLine();
            lOOut.AppendLine("GENERATED by `Tools/UWOriginalCoverage` - do not edit. Rebuild with:");
            lOOut.AppendLine();
            lOOut.AppendLine("```");
            lOOut.AppendLine("dotnet run --project Tools/UWOriginalCoverage -- --uw2 <path to UnderworldExhumed/uw2/src>");
            lOOut.AppendLine("```");
            lOOut.AppendLine();
            lOOut.AppendLine("The second game's counterpart to `ORIGINAL-COVERAGE.md`, for the beta gate: it walks every function of");
            lOOut.AppendLine("[Underworld Exhumed](https://github.com/abedegno/underworld-exhumed)'s decompilation of `UW2.EXE` and asks");
            lOOut.AppendLine("which our sources never name. The classic interface (the scope below) is listed function by function, the");
            lOOut.AppendLine("rest - the rules, covered by the play-through against the original - is counted only. Only names and counts");
            lOOut.AppendLine("are written here, never code.");
            lOOut.AppendLine();
            lOOut.AppendLine("Built from Underworld Exhumed at " + fCommit(psSource) + ". If it is updated, run the tool again and compare.");
            lOOut.AppendLine();

            List<Function> lOScope = pOFunctions.Where(fInScope).ToList();
            List<Function> lOOpen = lOScope.Where(pO => fIsOpen(pO, pOWords)).ToList();

            lOOut.AppendLine("| | Functions |");
            lOOut.AppendLine("|---|---|");
            lOOut.AppendLine("| In the decompilation (without port/) | " + pOFunctions.Count + " |");
            lOOut.AppendLine("| Named in our sources | " + pOFunctions.Count(pO => pOWords.Contains(pO.Name)) + " |");
            lOOut.AppendLine("| In the interface's scope (" + string.Join(", ", msScope) + ") | " + lOScope.Count + " |");
            lOOut.AppendLine("| Of those named in our sources | " + lOScope.Count(pO => pOWords.Contains(pO.Name)) + " |");
            lOOut.AppendLine("| Of those judged one by one | " + lOScope.Count(pO => !pOWords.Contains(pO.Name) && fJudged(pO.Name) != null) + " |");
            lOOut.AppendLine("| Left to judge | " + lOOpen.Count + " |");
            lOOut.AppendLine();

            lOOut.AppendLine("## Left to judge");
            lOOut.AppendLine();
            lOOut.AppendLine("The interface's functions named nowhere in our sources, by file. The second column is the company each keeps:");
            lOOut.AppendLine("the functions it calls.");
            lOOut.AppendLine();

            if (lOOpen.Count == 0)
                lOOut.AppendLine("None.");

            foreach (IGrouping<string, Function> lOFile in lOOpen.GroupBy(pO => pO.File).OrderBy(pO => pO.Key, StringComparer.Ordinal))
            {
                lOOut.AppendLine("### " + lOFile.Key);
                lOOut.AppendLine();
                lOOut.AppendLine("| Function | Calls |");
                lOOut.AppendLine("|---|---|");

                foreach (Function lOFunction in lOFile)
                    lOOut.AppendLine("| `" + lOFunction.Name + "` | " + (lOFunction.Calls.Count == 0 ? "-" : string.Join(", ", lOFunction.Calls.Take(10))) + " |");

                lOOut.AppendLine();
            }

            lOOut.AppendLine("## By file");
            lOOut.AppendLine();
            lOOut.AppendLine("| File | Functions | Named by us | Left to judge |");
            lOOut.AppendLine("|---|---|---|---|");

            foreach (IGrouping<string, Function> lOFile in pOFunctions.GroupBy(pO => pO.File).OrderBy(pO => pO.Key, StringComparer.Ordinal))
            {
                lOOut.AppendLine("| " + lOFile.Key + " | " + lOFile.Count() + " | " + lOFile.Count(pO => pOWords.Contains(pO.Name)) + " | "
                    + (fInScope(lOFile.First()) ? lOFile.Count(pO => fIsOpen(pO, pOWords)).ToString() : "(rules, counted only)") + " |");
            }

            lOOut.AppendLine();
            lOOut.AppendLine("## Judged one by one");
            lOOut.AppendLine();

            if (msJudged.GetLength(0) == 0)
                lOOut.AppendLine("None yet.");
            else
            {
                lOOut.AppendLine("| Function | Why it needs nothing, or where it is covered |");
                lOOut.AppendLine("|---|---|");

                for (int liAt = 0; liAt < msJudged.GetLength(0); liAt++)
                    lOOut.AppendLine("| `" + msJudged[liAt, 0] + "` | " + msJudged[liAt, 1] + " |");
            }

            lOOut.AppendLine();

            return lOOut.ToString();
        }

        /// <summary>The decompilation's commit, so one can tell two runs apart.</summary>
        private static string fCommit(string psSource)
        {
            try
            {
                ProcessStartInfo lOStart = new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    WorkingDirectory = psSource,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };

                using (Process lOProcess = Process.Start(lOStart))
                {
                    string lsOut = lOProcess.StandardOutput.ReadToEnd().Trim();

                    lOProcess.WaitForExit();

                    return lsOut.Length > 0 ? "commit " + lsOut : "an unknown commit";
                }
            }
            catch
            {
                return "an unknown commit";
            }
        }
    }
}
