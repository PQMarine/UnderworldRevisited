using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 7.8 (2026-10-25): THE INPUT LATCHES, RECORDED, and the owners that replace them
    /// (UWPressOwners, UWInputContexts.PadPointerTakes, UWPressHold). The expected answers were written
    /// from the code before the step, each latch's own test read off its readers:
    ///   - UWGamepad.ConsumePress: which place took a pad press so that the pad's pointer, the last
    ///     script of the frame, sent no click for it - per waiting place and pad button;
    ///   - UWGamepadPointer.Takes: which reader leaves a pad press to the pointer - per reader with the
    ///     default pad bindings of UWControls (recorded 2026-10-25), and when the pointer has its
    ///     buttons (ClicksActive, its rule from fComputeDriving);
    ///   - UWModernPointer.SwitchedFrame: the press the switch took is held for its frame only.
    /// One case changed on purpose, marked "(7.8)": the conversation's end took a pad press after the
    /// pointer had clicked with it (a coroutine, after every Update). Data-free.
    /// </summary>
    public static partial class Program
    {
        private const UWPadPress PadA = UWPadPress.Button | UWPadPress.Click;

        private const UWPadPress PadX = UWPadPress.Button | UWPadPress.Context;

        /// <summary>LB, RB, B, the d-pad...: a button, not the prompts' A or X.</summary>
        private const UWPadPress PadOther = UWPadPress.Button;

        /// <summary>RT or LT: a button, the second binding of PadClick or PadContext.</summary>
        private const UWPadPress PadTrigger = UWPadPress.Button | UWPadPress.Triggers;

        /// <summary>One situation of the pad's press: the owner, by name.</summary>
        private static void fPadCase(string psName, UWPadPressState peStates, UWPadPress pePresses, UWBackKey peBack,
            UWBackState peBackStates, string psExpected)
        {
            UWPadPressOwner leOwner = UWPressOwners.PadOwner(pePresses, peStates,
                leBackOwner => peBack != UWBackKey.None && UWBackChain.Takes(leBackOwner, peBack, peBackStates));

            Check.Str("pad press: " + psName, leOwner.ToString(), psExpected);
        }

        private static void fPadCase(string psName, UWPadPressState peStates, UWPadPress pePresses, string psExpected)
        {
            fPadCase(psName, peStates, pePresses, UWBackKey.None, UWBackState.None, psExpected);
        }

        /// <summary>The default gamepad bindings of the readers that asked UWGamepadPointer.Takes, and of
        /// the pointer's own buttons (UWControls.fBuildPlayerMap, 2026-10-25).</summary>
        private static readonly string[] msPadPointerButtons =
        {
            "<Gamepad>/buttonSouth", "<Gamepad>/rightTrigger", "<Gamepad>/buttonWest", "<Gamepad>/leftTrigger"
        };

        private static void fCheckPressOwners()
        {
            Check.Group("Press owners (the input latches recorded and replaced - cleanup step 7.8)");

            // ------------------------------------------------- ConsumePress: the pad press's owners

            foreach (string lsLine in UWPressOwners.DescribePadRows().Split('\n'))
            {
                if (lsLine.Length > 0)
                    Check.Pass("pad press row", lsLine);
            }

            fPadCase("nothing waits, A", UWPadPressState.None, PadA, "Pointer");
            fPadCase("nothing waits, X", UWPadPressState.None, PadX, "Pointer");

            // The window picture, the paging, the conversation's "[MORE]": any button, then
            // UWGamepad.ConsumePress (Interaction, UWMessageLog.UpdatePaging, UWConversationController).
            fPadCase("window picture, A", UWPadPressState.WindowPicture, PadA, "WindowPicture");
            fPadCase("window picture, RT", UWPadPressState.WindowPicture, PadTrigger, "WindowPicture");
            fPadCase("paging, X", UWPadPressState.Paging, PadX, "Paging");
            fPadCase("paging, LT", UWPadPressState.Paging, PadOther, "Paging");
            fPadCase("conversation [MORE], A", UWPadPressState.ConversationMore, PadA, "ConversationMore");
            fPadCase("conversation [MORE], RT", UWPadPressState.ConversationMore, PadTrigger, "ConversationMore");

            // (7.8) The end's wait read the press in a coroutine, after the pointer had clicked: today's
            // answer was Pointer; the end takes it now (one press, one owner).
            fPadCase("conversation end (7.8), A", UWPadPressState.ConversationEnd, PadA, "ConversationEnd");

            // The count prompt: A takes the count, X all, B (as the back chain gives it) cancels - each
            // with ConsumePress (UWCountPrompt.fUpdatePad). RT and LT it did not read; since 2026-10-25 (per
            // user) they answer as the mouse's buttons, one and all, and are the prompt's too - as the
            // pointer's they clicked once the prompt was over and put the count straight back.
            fPadCase("count prompt, A", UWPadPressState.CountPrompt, PadA, "CountPrompt");
            fPadCase("count prompt, X", UWPadPressState.CountPrompt, PadX, "CountPrompt");
            fPadCase("count prompt, RT (2026-10-25)", UWPadPressState.CountPrompt, PadTrigger, "CountPrompt");
            fPadCase("count prompt, LB", UWPadPressState.CountPrompt, PadOther, "Pointer");
            fPadCase("count prompt, B", UWPadPressState.CountPrompt, PadOther, UWBackKey.PadButton,
                UWBackState.PromptLine, "CountPrompt");

            // The classic yes/no: A yes, B no (UWPromptController.UpdateYesNo); X it does not read.
            fPadCase("yes/no, A", UWPadPressState.YesNo, PadA, "YesNo");
            fPadCase("yes/no, X", UWPadPressState.YesNo, PadX, "Pointer");
            fPadCase("yes/no, B", UWPadPressState.YesNo, PadOther, UWBackKey.PadButton, UWBackState.PromptLine, "YesNo");

            // The split box: A takes, B lets go (UWModernBagSplit.fUpdateSplitPad); X and RT are the
            // pointer's, which drives meanwhile.
            fPadCase("split box, A", UWPadPressState.BagSplit, PadA, "BagSplit");
            fPadCase("split box, X", UWPadPressState.BagSplit, PadX, "Pointer");
            fPadCase("split box, RT", UWPadPressState.BagSplit, PadTrigger, "Pointer");
            fPadCase("split box, B", UWPadPressState.BagSplit, PadOther, UWBackKey.PadButton, UWBackState.BagSplit, "BagSplit");
            fPadCase("split box under the map, B (the chain keeps it from the box)", UWPadPressState.BagSplit, PadOther,
                UWBackKey.PadButton, UWBackState.BagSplit | UWBackState.MapVisible, "Pointer");
            fPadCase("split box, a pad press without B (no back key)", UWPadPressState.BagSplit, PadOther, UWBackKey.None,
                UWBackState.BagSplit, "Pointer");

            Check.Bool("the pointer clicks when nothing waits", UWPressOwners.PointerClicks(PadA, UWPadPressState.None, null), true);
            Check.Bool("the pointer is silent under a window picture", UWPressOwners.PointerClicks(PadA, UWPadPressState.WindowPicture, null), false);

            // ------------------------------------------------- Takes: the PadPointer context

            // When the pointer has its buttons (UWGamepadPointer.fComputeDriving, fRefresh before 7.8).
            fPadPointerCase("game, nothing on", false, false, false, false, false, false, false, true, true, "-");
            fPadPointerCase("modern, R3 on", false, false, false, false, true, false, false, true, true, "drives, PadPointer");
            fPadPointerCase("modern, pointer freed (mouse)", false, false, false, false, false, true, false, true, true, "drives, PadPointer");
            fPadPointerCase("modern, a window holds the world", false, false, false, false, false, false, true, true, true, "drives, PadPointer");
            fPadPointerCase("classic, stick turns the view", false, false, false, false, false, false, false, true, false, "PadPointer");
            fPadPointerCase("classic, R3 on", false, false, false, false, true, false, false, true, false, "drives, PadPointer");
            fPadPointerCase("no scheme (menus)", false, false, false, false, false, false, true, false, false, "drives, PadPointer");
            fPadPointerCase("modern look holds the pointer", true, false, false, false, true, true, true, true, true, "-");
            fPadPointerCase("letter grid up", false, true, false, false, true, true, true, true, false, "-");
            fPadPointerCase("classic count prompt", false, false, true, false, false, false, false, true, false, "-");
            fPadPointerCase("classic yes/no on the pad", false, false, false, true, false, false, false, true, false, "-");

            // Which reader leaves which pad press to the pointer, in the PadPointer context.
            fTakesCase("A: the modern use (UWModernWorldInput)", "<Gamepad>/buttonSouth", true);
            fTakesCase("X: the modern look (UWModernWorldInput)", "<Gamepad>/buttonWest", true);
            fTakesCase("RT: the pad's draw and strike (UWModernWorldInput, UWPlayerAttackInput)", "<Gamepad>/rightTrigger", true);
            fTakesCase("LT: the cast tap and hold (UWModernRunePanel)", "<Gamepad>/leftTrigger", true);
            fTakesCase("LB: the bar's previous slot (UWModernActionBar)", "<Gamepad>/leftShoulder", false);
            fTakesCase("RB: the bar's next slot (UWModernActionBar)", "<Gamepad>/rightShoulder", false);
            fTakesCase("d-pad up: the bar's slot used (UWModernActionBar)", "<Gamepad>/dpad/up", false);
            fTakesCase("d-pad down: the panel tap and hold (UWModernBags)", "<Gamepad>/dpad/down", false);
            fTakesCase("View tapped: the map (UWHotkeys, gate PadPointerTakes)", "<Gamepad>/select", false);

            Check.Bool("Takes: out of the context nothing is taken",
                UWInputContexts.PadPointerTakes(UWInputContext.World, true, true), false);
            Check.Bool("Takes: a key or the mouse on the same action is never taken",
                UWInputContexts.PadPointerTakes(UWInputContext.World | UWInputContext.PadPointer, false, true), false);

            // ------------------------------------------------- SwitchedFrame: the press hold

            UWPressHold lOHold = new UWPressHold();

            Check.Bool("hold: nothing held at first", lOHold.Holds(UWAct.Interact, 10), false);
            lOHold.Hold(UWAct.Interact, 10);
            Check.Bool("hold: the switching press is held in its frame", lOHold.Holds(UWAct.Interact, 10), true);
            Check.Bool("hold: only that act's press", lOHold.Holds(UWAct.CursorDrag, 10), false);
            Check.Bool("hold: not in the next frame", lOHold.Holds(UWAct.Interact, 11), false);
            lOHold.Hold(UWAct.CursorDrag, 12);
            Check.Bool("hold: a new frame forgets the old holds", lOHold.Holds(UWAct.Interact, 12), false);
        }

        /// <summary>One situation of the pad's pointer: "drives" when it drives, "PadPointer" when the
        /// context holds, "-" for neither.</summary>
        private static void fPadPointerCase(string psName, bool pbLookLocked, bool pbLetterGrid, bool pbAskingCount, bool pbYesNoOnPad,
            bool pbOn, bool pbModernFree, bool pbHeldByWindow, bool pbScheme, bool pbModern, string psExpected)
        {
            bool lbDrives = UWPressOwners.PadPointerDrives(pbLookLocked, pbLetterGrid, pbAskingCount, pbYesNoOnPad, pbOn, pbModernFree,
                pbHeldByWindow, out bool lbFree);
            UWInputContext leContext = UWPressOwners.PadPointerContext(lbDrives, lbFree, pbScheme, pbModern);
            List<string> lsParts = new List<string>();

            if (lbDrives)
                lsParts.Add("drives");

            if (leContext == UWInputContext.PadPointer)
                lsParts.Add("PadPointer");

            Check.Str("pad pointer: " + psName, lsParts.Count == 0 ? "-" : string.Join(", ", lsParts), psExpected);
        }

        private static void fTakesCase(string psName, string psPadPath, bool pbExpected)
        {
            bool lbPointerButton = System.Array.IndexOf(msPadPointerButtons, psPadPath) >= 0;

            Check.Bool("Takes: " + psName,
                UWInputContexts.PadPointerTakes(UWInputContext.World | UWInputContext.PadPointer, true, lbPointerButton), pbExpected);
        }
    }
}
