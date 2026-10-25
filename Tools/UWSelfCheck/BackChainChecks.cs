using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 7.5 (2026-10-25): THE ESCAPE AND BACK CHAIN, RECORDED. Per open state - each window,
    /// the help, the map, the conversation, the cutscene, the letter grid, the count prompt, the split
    /// box, the modern game menu, the setup bar, the layout editor, the debug panel, typing, the bags
    /// - who takes Escape, the pad's Start and the pad's B, as the code before the step decided it:
    /// read off each consumer's own test of the others (the expected strings below were written from
    /// that code, not from the table). The chain (UWBackChain) must give the same takers. Where two took
    /// one press, wave 2b (2026-10-25, per user: one Escape closes only the topmost) changed the case on
    /// purpose - the cases marked "(was double)" or "(wave 2b)"; one pair stays, the modern save name,
    /// whose field is the page's own (one close). The table itself is pinned (Pins/back-chain.txt).
    /// Data-free.
    /// </summary>
    public static partial class Program
    {
        private static readonly UWBackOwner[] msModernRouting = { UWBackOwner.ModernMap, UWBackOwner.ModernChain, UWBackOwner.ModernMenu };

        /// <summary>One state: the owners whose code runs and asks (pOAsking), the states that hold;
        /// the takers of Escape, Start and B, top first, "-" for none.</summary>
        private static void fBackCase(string psName, UWBackState peStates, UWBackOwner[] pOAsking, string psEscape, string psStart, string psPadB)
        {
            Check.Str(psName + ": Escape", fTakers(pOAsking, UWBackChain.EscapeKeys(peStates), peStates), psEscape);
            Check.Str(psName + ": Start", fTakers(pOAsking, UWBackChain.StartKeys(peStates), peStates), psStart);
            Check.Str(psName + ": pad B", fTakers(pOAsking, UWBackChain.PadBKeys, peStates), psPadB);
        }

        private static string fTakers(UWBackOwner[] pOAsking, UWBackKey peKeys, UWBackState peStates)
        {
            List<UWBackOwner> lOTakers = UWBackChain.Takers(pOAsking, peKeys, peStates);
            List<string> lsNames = new List<string>();

            foreach (UWBackOwner leOwner in lOTakers)
                lsNames.Add(leOwner.ToString());

            return lsNames.Count == 0 ? "-" : string.Join(", ", lsNames);
        }

        /// <summary>The modern scheme's routing asks always; pOOthers besides.</summary>
        private static UWBackOwner[] fModern(params UWBackOwner[] pOOthers)
        {
            List<UWBackOwner> lOAsking = new List<UWBackOwner>(pOOthers);

            lOAsking.AddRange(msModernRouting);

            return lOAsking.ToArray();
        }

        private static UWBackOwner[] fOwners(params UWBackOwner[] pOOwners)
        {
            return pOOwners;
        }

        private static void fCheckBackChain(CheckContext pOContext)
        {
            Check.Group("Escape and back chain (UWBackChain - cleanup step 7.5)");

            // ------------------------------------------------- The table, pinned

            SortedDictionary<string, string> lONow = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
            string[] lsLines = UWBackChain.Describe().Split('\n');
            int liRow = 0;

            foreach (string lsLine in lsLines)
            {
                if (lsLine.Length > 0)
                    lONow[string.Format("{0:00}", liRow++)] = lsLine;
            }

            fComparePins(pOContext, "back-chain.txt", "back chain", lONow, lsKey => "rows");

            HashSet<UWBackOwner> lOOwned = new HashSet<UWBackOwner>();

            foreach (UWBackRow lORow in UWBackChain.Rows)
                lOOwned.Add(lORow.Owner);

            Check.Int("every owner has a row", lOOwned.Count, (int)UWBackOwner.ModernMenu + 1);

            foreach (UWBackRow lORow in UWBackChain.Rows)
            {
                if (lORow.Covers != UWBackState.None && lORow.Needs != lORow.Covers)
                    Check.Fail("a covering row needs its own state: " + lORow.Owner, lORow.Needs.ToString());
            }

            for (int liAt = 1; liAt < UWBackChain.Rows.Length; liAt++)
            {
                if (UWBackChain.Rows[liAt].Owner < UWBackChain.Rows[liAt - 1].Owner)
                    Check.Fail("the rows top first: " + UWBackChain.Rows[liAt].Owner, "after " + UWBackChain.Rows[liAt - 1].Owner);
            }

            UWBackState leNone = UWBackState.None;

            // ------------------------------------------------- Before the game

            fBackCase("main menu, its buttons", UWBackState.ScreenMenu, fOwners(), "-", "-", "-");
            fBackCase("main menu, the save list", UWBackState.ScreenMenu, fOwners(UWBackOwner.MainMenuSaves),
                "MainMenuSaves", "-", "MainMenuSaves");
            fBackCase("main menu, the save list under the setup bar's keys",
                UWBackState.ScreenMenu | UWBackState.SetupCapturing | UWBackState.SetupKeyNav,
                fOwners(UWBackOwner.MainMenuSaves, UWBackOwner.SetupKeyNav), "SetupKeyNav", "-", "-");
            fBackCase("main menu, the setup bar's effects screen", UWBackState.ScreenMenu | UWBackState.SetupEffects | UWBackState.SetupCapturing,
                fOwners(UWBackOwner.SetupEffects, UWBackOwner.MainMenuSaves), "SetupEffects", "SetupEffects", "SetupEffects");
            fBackCase("first run, the folder path typed", UWBackState.ScreenMenu | UWBackState.KitText | UWBackState.SetupCapturing,
                fOwners(UWBackOwner.KitText), "KitText", "-", "-");
            fBackCase("creation, a choice", UWBackState.ScreenMenu, fOwners(UWBackOwner.CharacterCreation),
                "CharacterCreation", "-", "CharacterCreation");
            fBackCase("creation, the name typed with the letter grid", UWBackState.ScreenMenu | UWBackState.CharacterName | UWBackState.LetterGrid,
                fOwners(UWBackOwner.CharacterName, UWBackOwner.LetterGrid), "CharacterName", "-", "LetterGrid");
            fBackCase("the intro, a cutscene", UWBackState.Cutscene, fModern(UWBackOwner.Cutscene), "Cutscene", "-", "-");

            // ------------------------------------------------- The modern scheme

            fBackCase("modern, nothing open", leNone, fModern(), "ModernMenu", "ModernMenu", "-");
            fBackCase("modern, the bags open (the chain)", UWBackState.ModernClosable, fModern(), "ModernChain", "ModernChain", "ModernChain");
            fBackCase("modern, a waiting spell (the chain)", UWBackState.ModernClosable, fModern(), "ModernChain", "ModernChain", "ModernChain");
            fBackCase("modern, the game menu", UWBackState.MenuOpen, fModern(), "ModernMenu", "ModernMenu", "ModernMenu");
            fBackCase("modern, the game menu over the bags", UWBackState.MenuOpen | UWBackState.ModernClosable, fModern(),
                "ModernMenu", "ModernMenu", "ModernMenu");
            fBackCase("modern, the big map", UWBackState.MapVisible, fModern(), "ModernMap", "ModernMap", "ModernMap");
            fBackCase("modern, the big map over the bags", UWBackState.MapVisible | UWBackState.ModernClosable, fModern(),
                "ModernMap", "ModernMap", "ModernMap");
            fBackCase("modern, a map note typed with the letter grid", UWBackState.MapVisible | UWBackState.MapNote | UWBackState.LetterGrid,
                fModern(UWBackOwner.MapNote, UWBackOwner.LetterGrid), "MapNote", "-", "LetterGrid");
            fBackCase("modern, the save name typed in the game menu (one close: the field is the page's own)",
                UWBackState.MenuOpen | UWBackState.ModernSaveName | UWBackState.KitText,
                fModern(UWBackOwner.KitText, UWBackOwner.ModernSaveName), "KitText, ModernSaveName", "-", "-");
            fBackCase("modern, the save name typed with the setup bar's keys on (wave 2b: the field has the keys)",
                UWBackState.MenuOpen | UWBackState.ModernSaveName | UWBackState.KitText | UWBackState.SetupCapturing | UWBackState.SetupKeyNav,
                fModern(UWBackOwner.SetupKeyNav, UWBackOwner.KitText, UWBackOwner.ModernSaveName), "KitText, ModernSaveName", "-", "-");
            fBackCase("modern, the game menu with the setup bar's keys (was double)",
                UWBackState.MenuOpen | UWBackState.SetupCapturing | UWBackState.SetupKeyNav,
                fModern(UWBackOwner.SetupKeyNav), "SetupKeyNav", "ModernMenu", "ModernMenu");
            fBackCase("modern, a setup dialog over the game menu", UWBackState.MenuOpen | UWBackState.SetupPanel | UWBackState.SetupCapturing,
                fModern(), "ModernMenu", "ModernMenu", "ModernMenu");
            fBackCase("modern, the setup bar's effects screen over the game menu (was double; B and Start closed the menu under it)",
                UWBackState.MenuOpen | UWBackState.SetupEffects | UWBackState.SetupCapturing,
                fModern(UWBackOwner.SetupEffects, UWBackOwner.SetupKeyNav), "SetupEffects", "SetupEffects", "SetupEffects");
            fBackCase("modern, the layout editor's question (was double: the menu's leave waited for it by the update order)",
                UWBackState.MenuOpen | UWBackState.LayoutAsk, fModern(UWBackOwner.LayoutAsk), "LayoutAsk", "ModernMenu", "ModernMenu");
            fBackCase("modern, the layout editor, no question: the menu's leave asks it", UWBackState.MenuOpen, fModern(),
                "ModernMenu", "ModernMenu", "ModernMenu");
            fBackCase("modern, the bags' menu", UWBackState.BagMenu | UWBackState.ModernClosable, fModern(UWBackOwner.BagMenu),
                "BagMenu", "-", "BagMenu");
            fBackCase("modern, the split box", UWBackState.BagSplit | UWBackState.ModernClosable, fModern(UWBackOwner.BagSplit),
                "BagSplit", "-", "BagSplit");
            fBackCase("modern, the split box under the game menu", UWBackState.BagSplit | UWBackState.ModernClosable | UWBackState.MenuOpen,
                fModern(UWBackOwner.BagSplit), "-", "-", "-");
            fBackCase("modern, the yes/no box", UWBackState.PromptLine, fModern(UWBackOwner.ModernQuestion), "ModernQuestion", "-", "-");
            fBackCase("modern, a mantra with the letter grid", UWBackState.Mantra | UWBackState.LetterGrid,
                fModern(UWBackOwner.Mantra, UWBackOwner.LetterGrid), "Mantra", "-", "LetterGrid");
            fBackCase("modern, the roaming view", UWBackState.RoamingSight, fModern(UWBackOwner.RoamingSight), "RoamingSight", "-", "-");
            fBackCase("modern, a conversation", UWBackState.Conversation, fModern(), "-", "-", "-");
            fBackCase("modern, a conversation with the bags open", UWBackState.Conversation | UWBackState.ModernClosable, fModern(), "-", "-", "-");
            fBackCase("modern, a conversation's typed answer with the letter grid",
                UWBackState.Conversation | UWBackState.ConversationAnswer | UWBackState.LetterGrid,
                fModern(UWBackOwner.ConversationAnswer, UWBackOwner.LetterGrid), "ConversationAnswer", "-", "LetterGrid");
            fBackCase("modern, the help's notes typed in the panel", UWBackState.KitText | UWBackState.HelpKeys | UWBackState.ModernClosable,
                fModern(UWBackOwner.KitText), "KitText", "-", "-");
            fBackCase("modern, the help's reading mode", UWBackState.HelpKeys | UWBackState.ModernClosable,
                fModern(UWBackOwner.HelpReading), "HelpReading", "-", "-");
            fBackCase("modern, the debug panel's tools with a field typed (was double; the field goes with the tab)",
                UWBackState.DebugTools | UWBackState.KitText | UWBackState.ModernClosable,
                fModern(UWBackOwner.DebugTools, UWBackOwner.KitText), "DebugTools", "-", "-");
            fBackCase("modern, the debug panel's tools, no field typed, the bags open", UWBackState.DebugTools | UWBackState.ModernClosable,
                fModern(UWBackOwner.DebugTools), "DebugTools", "-", "ModernChain");
            fBackCase("modern, the instrument", UWBackState.Instrument, fModern(UWBackOwner.Instrument), "Instrument", "-", "-");
            fBackCase("modern, a cutscene over the bags", UWBackState.Cutscene | UWBackState.ModernClosable, fModern(UWBackOwner.Cutscene),
                "Cutscene", "-", "-");
            fBackCase("a cutscene with the debug panel's tools open (wave 2b: the panel is drawn over it)",
                UWBackState.Cutscene | UWBackState.DebugTools, fModern(UWBackOwner.Cutscene, UWBackOwner.DebugTools), "DebugTools", "-", "-");

            // Any key pages a long text or hides a window picture (wave 2b: Escape paged and opened the
            // game menu, Start and B paged and closed the bags).
            fBackCase("modern, a long text waits for its next page (was double)", UWBackState.Paging, fModern(UWBackOwner.Paging),
                "Paging", "Paging", "Paging");
            fBackCase("modern, a long text waits, the bags open (was double)", UWBackState.Paging | UWBackState.ModernClosable,
                fModern(UWBackOwner.Paging), "Paging", "Paging", "Paging");
            fBackCase("modern, a window picture (was double)", UWBackState.WindowPicture, fModern(UWBackOwner.WindowPicture),
                "WindowPicture", "WindowPicture", "WindowPicture");
            fBackCase("modern, a long text waiting under the game menu (the interaction does not get to the paging)", UWBackState.MenuOpen,
                fModern(UWBackOwner.Paging), "ModernMenu", "ModernMenu", "ModernMenu");

            // The Controls window waits for a key or a button: the press is the wait's (wave 2b: Escape
            // cancelled it and closed the game menu with the window; B or Start became the binding and
            // closed the menu).
            fBackCase("modern, the Controls window waits over the game menu (was double)",
                UWBackState.MenuOpen | UWBackState.SetupPanel | UWBackState.SetupCapturing | UWBackState.Rebind,
                fModern(UWBackOwner.Rebind), "Rebind", "Rebind", "Rebind");
            fBackCase("modern, the Controls window's wait with the setup bar's keys on",
                UWBackState.MenuOpen | UWBackState.SetupPanel | UWBackState.SetupCapturing | UWBackState.SetupKeyNav | UWBackState.Rebind,
                fModern(UWBackOwner.Rebind, UWBackOwner.SetupKeyNav), "Rebind", "Rebind", "Rebind");

            // ------------------------------------------------- The classic scheme (no modern routing)

            fBackCase("classic, nothing open", leNone, fOwners(), "-", "-", "-");
            fBackCase("classic, the big map", UWBackState.MapVisible, fOwners(), "-", "-", "-");
            fBackCase("classic, the options", leNone, fOwners(UWBackOwner.ClassicOptions), "ClassicOptions", "-", "-");
            fBackCase("classic, the options with the setup bar's keys (was double)", UWBackState.SetupCapturing | UWBackState.SetupKeyNav,
                fOwners(UWBackOwner.ClassicOptions, UWBackOwner.SetupKeyNav), "SetupKeyNav", "-", "-");
            fBackCase("classic, the options under a setup dialog", UWBackState.SetupPanel | UWBackState.SetupCapturing,
                fOwners(UWBackOwner.ClassicOptions), "-", "-", "-");
            fBackCase("classic, the options, a save name typed with the letter grid", UWBackState.ClassicSaveName | UWBackState.LetterGrid,
                fOwners(UWBackOwner.ClassicSaveName, UWBackOwner.ClassicOptions, UWBackOwner.LetterGrid), "ClassicSaveName", "-", "LetterGrid");
            fBackCase("classic, the options' effects screen", UWBackState.OptionsEffects,
                fOwners(UWBackOwner.OptionsEffects, UWBackOwner.ClassicOptions), "OptionsEffects", "-", "-");
            fBackCase("classic, the setup bar's effects screen over the options (was double)", UWBackState.SetupEffects | UWBackState.SetupCapturing,
                fOwners(UWBackOwner.SetupEffects, UWBackOwner.SetupKeyNav, UWBackOwner.ClassicOptions), "SetupEffects", "SetupEffects", "SetupEffects");
            fBackCase("classic, the help's notes typed with the options open (was double)", UWBackState.KitText | UWBackState.HelpKeys,
                fOwners(UWBackOwner.KitText, UWBackOwner.ClassicOptions), "KitText", "-", "-");
            fBackCase("classic, the help's reading mode with the options open (wave 2b)", UWBackState.HelpKeys,
                fOwners(UWBackOwner.HelpReading, UWBackOwner.ClassicOptions), "HelpReading", "-", "-");
            fBackCase("classic, the Controls window waits over the options", UWBackState.SetupPanel | UWBackState.SetupCapturing | UWBackState.Rebind,
                fOwners(UWBackOwner.Rebind, UWBackOwner.ClassicOptions), "Rebind", "Rebind", "Rebind");
            fBackCase("classic, a long text waits", UWBackState.Paging, fOwners(UWBackOwner.Paging), "Paging", "Paging", "Paging");
            fBackCase("classic, a map note with the letter grid", UWBackState.MapVisible | UWBackState.MapNote | UWBackState.LetterGrid,
                fOwners(UWBackOwner.MapNote, UWBackOwner.LetterGrid), "MapNote", "-", "LetterGrid");
            fBackCase("classic, the count prompt", UWBackState.PromptLine, fOwners(UWBackOwner.CountPrompt), "CountPrompt", "-", "CountPrompt");
            fBackCase("classic, the yes/no question", UWBackState.PromptLine, fOwners(UWBackOwner.YesNo), "YesNo", "-", "YesNo");
            fBackCase("classic, a mantra with the letter grid", UWBackState.Mantra | UWBackState.LetterGrid,
                fOwners(UWBackOwner.Mantra, UWBackOwner.LetterGrid), "Mantra", "-", "LetterGrid");
            fBackCase("classic, a conversation's typed answer", UWBackState.Conversation | UWBackState.ConversationAnswer,
                fOwners(UWBackOwner.ConversationAnswer), "ConversationAnswer", "-", "-");
            fBackCase("classic, the typed answer while the help's notes are typed", UWBackState.Conversation | UWBackState.ConversationAnswer
                | UWBackState.KitText | UWBackState.HelpKeys, fOwners(UWBackOwner.ConversationAnswer, UWBackOwner.KitText), "KitText", "-", "-");
            fBackCase("classic, the typed answer under a running line", UWBackState.Conversation | UWBackState.ConversationAnswer
                | UWBackState.PromptLine, fOwners(UWBackOwner.ConversationAnswer, UWBackOwner.CountPrompt), "CountPrompt", "-", "CountPrompt");
            fBackCase("classic, the instrument", UWBackState.Instrument, fOwners(UWBackOwner.Instrument), "Instrument", "-", "-");
            fBackCase("classic, the debug panel's tools over the options (was double)", UWBackState.DebugTools,
                fOwners(UWBackOwner.DebugTools, UWBackOwner.ClassicOptions), "DebugTools", "-", "-");
            fBackCase("classic, the roaming view (the classic scheme ends it by its icon)", UWBackState.RoamingSight,
                fOwners(), "-", "-", "-");

            // ------------------------------------------------- PadBack rebound (step 7.6)

            // The back binding on another button (say LB): that button gives the PadBack act and its
            // binding read on the pad, B its place alone. The prompts, the split box and the screens
            // follow the binding now; the letter grid keeps B by its place.
            UWBackKey leRebound = UWBackKey.PadBack | UWBackKey.PadButton;

            Check.Str("rebound: the count prompt takes the new button",
                fTakers(fOwners(UWBackOwner.CountPrompt), leRebound, UWBackState.PromptLine), "CountPrompt");
            Check.Str("rebound: the count prompt leaves B",
                fTakers(fOwners(UWBackOwner.CountPrompt), UWBackKey.PadEast, UWBackState.PromptLine), "-");
            Check.Str("rebound: the main menu's save list takes the new button",
                fTakers(fOwners(UWBackOwner.MainMenuSaves), leRebound, UWBackState.ScreenMenu), "MainMenuSaves");
            Check.Str("rebound: the letter grid keeps B",
                fTakers(fModern(UWBackOwner.LetterGrid, UWBackOwner.MapNote), UWBackKey.PadEast,
                    UWBackState.MapVisible | UWBackState.MapNote | UWBackState.LetterGrid), "LetterGrid");
            Check.Str("rebound: the new button under the letter grid closes no map",
                fTakers(fModern(UWBackOwner.LetterGrid), leRebound, UWBackState.MapVisible | UWBackState.LetterGrid), "-");
            Check.Str("rebound: the new button closes the setup bar's effects screen, not the game menu under it",
                fTakers(fModern(UWBackOwner.SetupEffects), leRebound, UWBackState.MenuOpen | UWBackState.SetupEffects | UWBackState.SetupCapturing),
                "SetupEffects");
            Check.Str("rebound: B by its place alone leaves the effects screen and the menu",
                fTakers(fModern(UWBackOwner.SetupEffects), UWBackKey.PadEast, UWBackState.MenuOpen | UWBackState.SetupEffects | UWBackState.SetupCapturing),
                "-");
            Check.Str("rebound: the new button closes the bags",
                fTakers(fModern(), leRebound, UWBackState.ModernClosable), "ModernChain");
        }
    }
}
