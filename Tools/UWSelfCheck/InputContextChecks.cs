using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup phase 7, wave 2 (2026-10-25): the input facade's table of actions (UWActionTable, step
    /// 7.4) held against the maps as Unity built them - the recording of the editor check
    /// UWInputLockCheck, Tools/Fixtures/input/typing-lock.expected.txt, made from the code before the
    /// step. Data-free.
    /// </summary>
    public static partial class Program
    {
        private const string TypingLockFixture = "Tools/Fixtures/input/typing-lock.expected.txt";

        /// <summary>One line of the recording: map, action, binding count, silent while typing.</summary>
        private struct RecordedAction
        {
            public string Map;

            public string Action;

            public int Bindings;

            public bool Silent;
        }

        /// <summary>The recording's two sections (not typing, typing), null outside the source tree or
        /// without the file.</summary>
        private static List<RecordedAction>[] fReadTypingLockFixture()
        {
            string lsRoot = UWRuleIndexScan.FindProjectRoot();

            if (lsRoot == null)
                return null;

            string lsPath = Path.Combine(lsRoot, TypingLockFixture.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(lsPath))
                return null;

            List<RecordedAction>[] lOSections = { new List<RecordedAction>(), new List<RecordedAction>() };
            int liSection = -1;

            foreach (string lsRaw in File.ReadAllLines(lsPath))
            {
                string lsLine = lsRaw.Trim();

                if (lsLine.Length == 0)
                    continue;

                if (lsLine.StartsWith("===="))
                {
                    liSection = lsLine == "==== typing" ? 1 : 0;
                    continue;
                }

                // "Map|Action (bindings) free|silent"
                int liBar = lsLine.IndexOf('|');
                int liOpen = lsLine.IndexOf(" (");
                int liClose = lsLine.IndexOf(") ");

                if (liSection < 0 || liBar < 0 || liOpen < liBar || liClose < liOpen)
                    continue;

                lOSections[liSection].Add(new RecordedAction
                {
                    Map = lsLine.Substring(0, liBar),
                    Action = lsLine.Substring(liBar + 1, liOpen - liBar - 1),
                    Bindings = int.Parse(lsLine.Substring(liOpen + 2, liClose - liOpen - 2)),
                    Silent = lsLine.Substring(liClose + 2) == "silent"
                });
            }

            return lOSections;
        }

        /// <summary>
        /// Step 7.4: every UWAct names an action of the maps UWControls builds, in their order, and every
        /// action of those maps has its UWAct - the same map and action strings settings.json keeps its
        /// rebinds under ("Map|Action|BindingIndex|path"), so no action was renamed.
        /// </summary>
        private static void fCheckActionTable()
        {
            Check.Group("Action table (UWAct, UWActionTable - cleanup step 7.4)");

            List<string> lsTable = new List<string>();
            HashSet<string> lOUnique = new HashSet<string>();

            for (int liAct = 0; liAct < UWActionTable.Count; liAct++)
            {
                UWAct leAct = (UWAct)liAct;
                string lsKey = UWActionTable.MapOf(leAct) + "|" + UWActionTable.NameOf(leAct);

                lsTable.Add(lsKey);
                lOUnique.Add(lsKey);

                UWAct leFound;

                if (!UWActionTable.TryFind(UWActionTable.MapOf(leAct), UWActionTable.NameOf(leAct), out leFound) || leFound != leAct)
                    Check.Fail("act found by its map and name: " + leAct, "found " + leFound);
            }

            Check.Int("each map and name once", lOUnique.Count, UWActionTable.Count);
            Check.Str("the noclip map's walk", UWActionTable.MapOf(UWAct.NoclipMove) + "|" + UWActionTable.NameOf(UWAct.NoclipMove), "Noclip|Move");
            Check.Str("the debug map's overlay key", UWActionTable.MapOf(UWAct.ToggleOverlay) + "|" + UWActionTable.NameOf(UWAct.ToggleOverlay), "Debug|ToggleOverlay");
            Check.Str("the action bar's last slot", UWActionTable.NameOf(UWActionTable.ModernSlot(9)), "ModernSlot10");

            UWAct leNone;

            Check.Bool("no act for an action no map has", UWActionTable.TryFind("Player", "NoSuchAction", out leNone), false);

            List<RecordedAction>[] lORecorded = fReadTypingLockFixture();

            if (lORecorded == null)
            {
                Check.Pass("the maps as recorded", "skipped - " + TypingLockFixture + " not found (outside the source tree)");
                return;
            }

            List<string> lsMaps = new List<string>();

            foreach (RecordedAction lOAction in lORecorded[0])
                lsMaps.Add(lOAction.Map + "|" + lOAction.Action);

            Check.Int("as many acts as the maps have actions", lsTable.Count, lsMaps.Count);
            Check.Str("the acts in the maps' order", string.Join(", ", lsTable), string.Join(", ", lsMaps));
        }

        /// <summary>
        /// Step 7.4: the input contexts (UWInputContexts) give the answers UWTextEntryState and
        /// UWControlScheme wrote out before. THE TYPING LOCK'S SET: the actions the TextEntry context
        /// silences are exactly those the recording found silent while typing (UWInputLockCheck on the
        /// code before the step: 52 Player actions, four noclip ones, the whole debug map), the others
        /// stay free. WHO LOCKS: each typing source alone, with its three answers as UWTextEntryState
        /// gave them (LocksKeys, IsTextEntryActive, IsTypingText) - read off that class before the move.
        /// And what blocks the world and the inventory (UWControlScheme.IsWorldInputBlocked,
        /// IsInventoryInputBlocked), and which context lies on top.
        /// </summary>
        private static void fCheckInputContexts()
        {
            Check.Group("Input contexts (UWInputContexts, the typing lock - cleanup step 7.4)");

            List<RecordedAction>[] lORecorded = fReadTypingLockFixture();

            if (lORecorded == null)
                Check.Pass("the typing lock's set as recorded", "skipped - " + TypingLockFixture + " not found (outside the source tree)");
            else
            {
                List<string> lsSilentNow = new List<string>();
                List<string> lsSilentRecorded = new List<string>();
                List<string> lsFreeRecorded = new List<string>();

                for (int liAct = 0; liAct < UWActionTable.Count; liAct++)
                {
                    UWAct leAct = (UWAct)liAct;

                    if (UWInputContexts.Silences(UWInputContext.World | UWInputContext.TextEntry, leAct))
                        lsSilentNow.Add(UWActionTable.MapOf(leAct) + "|" + UWActionTable.NameOf(leAct));

                    if (UWInputContexts.Silences(UWInputContext.World | UWInputContext.UiModal | UWInputContext.Conversation
                        | UWInputContext.ScreenMenu | UWInputContext.Cutscene, leAct))
                        Check.Fail("only TextEntry silences: " + leAct, "silent without it");
                }

                foreach (RecordedAction lOAction in lORecorded[1])
                {
                    if (lOAction.Silent)
                        lsSilentRecorded.Add(lOAction.Map + "|" + lOAction.Action);
                    else
                        lsFreeRecorded.Add(lOAction.Map + "|" + lOAction.Action);
                }

                Check.Int("silent while typing: as many as recorded", lsSilentNow.Count, lsSilentRecorded.Count);
                Check.Str("silent while typing: the recorded set", string.Join(", ", lsSilentNow), string.Join(", ", lsSilentRecorded));
                Check.Str("free while typing (recorded)", string.Join(", ", lsFreeRecorded),
                    "Player|Look, Player|LookStick, Player|CursorDrag, Player|Interact, Player|ToggleHelp, Player|ModernAttack, "
                    + "Player|PadBack, Player|PadJump, Player|PadCast, Player|PadSlotPrevious, Player|PadSlotNext, Player|PadSlotUse, "
                    + "Player|PadPointer, Player|PadClick, Player|PadContext, Noclip|Look");

                foreach (RecordedAction lOAction in lORecorded[0])
                {
                    if (lOAction.Silent)
                        Check.Fail("nothing silent without typing: " + lOAction.Map + "|" + lOAction.Action, "silent in the recording");
                }
            }

            // Each source alone: LocksKeys, IsTextEntryActive, IsTypingText.
            fTypingAnswers("nothing typed", UWTypingSource.None, "---");
            fTypingAnswers("map note", UWTypingSource.MapNote, "LET");
            fTypingAnswers("classic save name", UWTypingSource.ClassicSaveName, "LET");
            fTypingAnswers("modern save name", UWTypingSource.ModernSaveName, "LET");
            fTypingAnswers("conversation answer", UWTypingSource.ConversationAnswer, "LET");
            fTypingAnswers("character name (the grid asks the screen itself)", UWTypingSource.CharacterName, "LE-");
            fTypingAnswers("kit text field", UWTypingSource.KitField, "LE-");
            fTypingAnswers("instrument", UWTypingSource.Instrument, "LE-");
            fTypingAnswers("help notes or reading", UWTypingSource.HelpKeys, "LE-");
            fTypingAnswers("split box", UWTypingSource.BagSplit, "LE-");
            fTypingAnswers("bags' menu", UWTypingSource.BagMenu, "LE-");
            fTypingAnswers("mantra", UWTypingSource.Mantra, "LET");
            fTypingAnswers("running line (count prompt, yes/no)", UWTypingSource.Prompt, "LE-");
            fTypingAnswers("running line with its text cursor", UWTypingSource.Prompt | UWTypingSource.PromptTyped, "LET");
            fTypingAnswers("debug tools' tab", UWTypingSource.DebugTools, "L--");
            fTypingAnswers("debug tools' tab and a map note", UWTypingSource.DebugTools | UWTypingSource.MapNote, "LET");

            Check.Bool("the lock puts TextEntry on the stack", UWInputContexts.FromTyping(UWTypingSource.Instrument) == UWInputContext.TextEntry, true);
            Check.Bool("no lock, no TextEntry", UWInputContexts.FromTyping(UWTypingSource.None) == UWInputContext.None, true);

            // What blocks the world and the inventory: a modal hold (a menu, the remote view, asleep)
            // both, a conversation the world only - the inventory trades.
            Check.Bool("world alone: the world goes on", UWInputContexts.BlocksWorld(UWInputContext.World), false);
            Check.Bool("world alone: the inventory goes on", UWInputContexts.BlocksInventory(UWInputContext.World), false);
            Check.Bool("modal: the world stops", UWInputContexts.BlocksWorld(UWInputContext.World | UWInputContext.UiModal), true);
            Check.Bool("modal: the inventory stops", UWInputContexts.BlocksInventory(UWInputContext.World | UWInputContext.UiModal), true);
            Check.Bool("conversation: the world stops", UWInputContexts.BlocksWorld(UWInputContext.World | UWInputContext.Conversation), true);
            Check.Bool("conversation: the inventory goes on", UWInputContexts.BlocksInventory(UWInputContext.World | UWInputContext.Conversation), false);
            Check.Bool("typing alone blocks no world", UWInputContexts.BlocksWorld(UWInputContext.World | UWInputContext.TextEntry), false);

            Check.Str("top: typing over a conversation",
                UWInputContexts.Top(UWInputContext.World | UWInputContext.Conversation | UWInputContext.TextEntry).ToString(), "TextEntry");
            Check.Str("top: a cutscene over everything",
                UWInputContexts.Top(UWInputContext.World | UWInputContext.UiModal | UWInputContext.Cutscene | UWInputContext.TextEntry).ToString(), "Cutscene");
            Check.Str("top: the world alone", UWInputContexts.Top(UWInputContext.World).ToString(), "World");
        }

        /// <summary>One typing state's answers as three letters: L locks the keys, E is text entry
        /// (the wide sense), T types text (the narrow sense, the letter grid); '-' for no.</summary>
        private static void fTypingAnswers(string psName, UWTypingSource peSources, string psExpected)
        {
            string lsAnswers = (UWInputContexts.LocksKeys(peSources) ? "L" : "-") + (UWInputContexts.IsTextEntry(peSources) ? "E" : "-")
                + (UWInputContexts.IsTypingText(peSources) ? "T" : "-");

            Check.Str("typing: " + psName, lsAnswers, psExpected);
        }
    }
}
