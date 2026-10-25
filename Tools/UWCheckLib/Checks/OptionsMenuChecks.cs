using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.18 (2026-10-23): the classic options menu's state machine
    /// (UWOptionsMenuModel) left UWHudOptions. Pinned with what that class showed and did: the
    /// pictures of every screen, the highlight each starts with, the key stepping that stops at
    /// the ends and skips the headings, which slots can be chosen, what each choice does, and the
    /// Ctrl shortcut's flag that closes the panel after a music or sound choice.
    /// </summary>
    public static partial class SelfChecks
    {
        private static string fOptionsEntries(UWOptionsMenuModel pOModel)
        {
            string lsEntries = string.Empty;

            for (int liAt = 0; liAt < UWOptionsMenuModel.EntryCount; liAt++)
                lsEntries += (liAt > 0 ? " " : string.Empty) + pOModel.EntryAt(liAt);

            return lsEntries;
        }

        private static void fCheckOptionsMenu()
        {
            Check.Group("Options menu (UWOptionsMenuModel - cleanup step 5.18)");

            UWOptionsMenuModel.Settings lOOn = new UWOptionsMenuModel.Settings(true, true, 2);
            UWOptionsMenuModel.Settings lOOff = new UWOptionsMenuModel.Settings(false, false, 0);
            UWOptionsMenuModel lOMenu = new UWOptionsMenuModel();

            fExpectBool("closed: not open", lOMenu.IsOpen, false);
            fExpectInt("closed: no background", lOMenu.Background, UWOptionsMenuModel.NoPicture);
            Check.Str("closed: no pictures", fOptionsEntries(lOMenu), "-1 -1 -1 -1 -1 -1 -1");

            // THE TOP MENU: seven buttons, SAVE lit at once.
            lOMenu.Open();
            fExpectBool("top: open", lOMenu.IsOpen && lOMenu.Page == UWOptionsMenuModel.PageEnum.Top, true);
            fExpectInt("top: the background of all option buttons", lOMenu.Background, 1);
            Check.Str("top: save, restore, music, sound, detail, return, quit", fOptionsEntries(lOMenu), "6 8 10 12 14 16 18");
            fExpectInt("top: SAVE is lit at once (per user, 2026-09-26)", lOMenu.KeyEntry, 0);
            fExpectBool("top: every entry can be chosen", lOMenu.IsEntryClickable(0) && lOMenu.IsEntryClickable(6), true);
            fExpectInt("top: the lit entry shows its second picture", lOMenu.PictureOf(0), 7);
            fExpectInt("top: the others their first", lOMenu.PictureOf(1), 8);

            // THE KEYS: one step at a time, stopping at the ends.
            lOMenu.StepKey(-1);
            fExpectInt("keys: Up at the top stays", lOMenu.KeyEntry, 0);
            lOMenu.StepKey(1);
            fExpectInt("keys: Down moves one", lOMenu.KeyEntry, 1);

            for (int liAt = 0; liAt < 10; liAt++)
                lOMenu.StepKey(1);

            fExpectInt("keys: Down stops at QUIT", lOMenu.KeyEntry, 6);
            fExpectInt("keys: without a highlight Down starts on the first usable entry", lOMenu.StepKeyEntry(-1, 1), 0);
            fExpectInt("keys: without a highlight Up starts on the last", lOMenu.StepKeyEntry(-1, -1), 6);
            lOMenu.HighlightByClick(2);
            fExpectBool("keys: a click moves the highlight (per user, 2026-09-27)", lOMenu.IsLit(2) && !lOMenu.IsLit(6), true);
            lOMenu.ResetKeyEntryIfPageChanged();
            fExpectInt("keys: the same page keeps its highlight", lOMenu.KeyEntry, 2);

            // SAVE: the view asks whether saving refuses, then shows the screen.
            UWOptionsMenuModel.Outcome lOOutcome = lOMenu.Choose(UWOptionsMenuModel.TopSave, lOOn);
            fExpectBool("save: SAVE asks the view first, the page stays", lOOutcome.Action == UWOptionsMenuModel.ActionEnum.AskSave
                && lOMenu.Page == UWOptionsMenuModel.PageEnum.Top, true);
            lOMenu.ShowSave();
            Check.Str("save: the heading, four slots and cancel", fOptionsEntries(lOMenu), "6 30 32 34 36 24 -1");
            fExpectInt("save: the background of the save buttons", lOMenu.Background, 2);
            fExpectInt("save: the first slot is lit", lOMenu.KeyEntry, 1);
            fExpectBool("save: the heading cannot be chosen", lOMenu.IsEntryClickable(0), false);
            lOMenu.StepKey(-1);
            fExpectInt("save: Up does not step onto the heading", lOMenu.KeyEntry, 1);
            fExpectInt("save: without a highlight Down starts on the first slot", lOMenu.StepKeyEntry(-1, 1), 1);
            fExpectInt("save: without a highlight Up starts on cancel", lOMenu.StepKeyEntry(-1, -1), 5);
            lOOutcome = lOMenu.Choose(3, lOOn);
            fExpectBool("save: slot III starts the name", lOOutcome.Action == UWOptionsMenuModel.ActionEnum.BeginSaveName && lOOutcome.Value == 3, true);
            lOOutcome = lOMenu.Choose(UWOptionsMenuModel.SlotCancel, lOOn);
            fExpectBool("save: cancel goes back to the top menu", lOOutcome.Action == UWOptionsMenuModel.ActionEnum.None
                && lOMenu.Page == UWOptionsMenuModel.PageEnum.Top && lOMenu.KeyEntry == 0, true);

            // RESTORE: the list goes into the scroll, a slot restores or says nothing is there.
            lOOutcome = lOMenu.Choose(UWOptionsMenuModel.TopRestore, lOOn);
            fExpectBool("restore: the view lists the games", lOOutcome.Action == UWOptionsMenuModel.ActionEnum.ListSaves, true);
            Check.Str("restore: the heading, four slots and cancel", fOptionsEntries(lOMenu), "46 30 32 34 36 24 -1");
            fExpectInt("restore: the first slot is lit", lOMenu.KeyEntry, 1);
            lOOutcome = lOMenu.Choose(4, lOOn);
            fExpectBool("restore: slot IV restores, the screen stays", lOOutcome.Action == UWOptionsMenuModel.ActionEnum.Restore && lOOutcome.Value == 4
                && lOMenu.Page == UWOptionsMenuModel.PageEnum.Restore, true);
            lOMenu.Choose(UWOptionsMenuModel.SlotCancel, lOOn);
            fExpectBool("restore: cancel goes back to the top menu", lOMenu.Page == UWOptionsMenuModel.PageEnum.Top, true);

            // DETAIL: the label of the current level one pixel up and left, the level lit.
            lOMenu.Choose(UWOptionsMenuModel.TopDetail, lOOn);
            Check.Str("detail: the label of HIGH, the four levels, DONE", fOptionsEntries(lOMenu), "55 -1 38 40 42 44 28");
            fExpectInt("detail: the detail background", lOMenu.Background, 5);
            fExpectInt("detail: the current level is lit", lOMenu.KeyEntry, 4);
            fExpectBool("detail: the label lies one pixel up and left", lOMenu.EntryOffset(0) == (-1, -1) && lOMenu.EntryOffset(2) == (0, 0), true);
            fExpectBool("detail: the label and the gap cannot be chosen, the levels can",
                !lOMenu.IsEntryClickable(0) && !lOMenu.IsEntryClickable(1) && lOMenu.IsEntryClickable(2) && lOMenu.IsEntryClickable(6), true);
            lOOutcome = lOMenu.Choose(3, lOOn);
            fExpectBool("detail: MEDIUM sets level 1", lOOutcome.Action == UWOptionsMenuModel.ActionEnum.SetDetail && lOOutcome.Value == 1, true);
            lOMenu.ShowDetail(1);
            fExpectBool("detail: shown anew with the new level", lOMenu.EntryAt(0) == 54 && lOMenu.KeyEntry == 3, true);
            lOOutcome = lOMenu.Choose(5, lOOn);
            fExpectBool("detail: VERY HIGH sets level 3 and opens the effects screen",
                lOOutcome.Action == UWOptionsMenuModel.ActionEnum.SetDetailAndOpenEffects && lOOutcome.Value == 3, true);
            lOMenu.Choose(6, lOOn);
            fExpectBool("detail: DONE goes back to the top menu", lOMenu.Page == UWOptionsMenuModel.PageEnum.Top, true);

            // MUSIC AND SOUND: the state at the top, the button of the current state lit.
            lOMenu.Choose(UWOptionsMenuModel.TopMusic, lOOn);
            Check.Str("music: MUSIC IS ON, TURN MUSIC, ON, OFF, DONE", fOptionsEntries(lOMenu), "47 51 20 22 26 -1 -1");
            fExpectInt("music: the on-off background", lOMenu.Background, 4);
            fExpectInt("music: ON is lit while music is on", lOMenu.KeyEntry, 2);
            fExpectBool("music: only ON, OFF and DONE can be chosen",
                !lOMenu.IsEntryClickable(1) && lOMenu.IsEntryClickable(2) && lOMenu.IsEntryClickable(4) && !lOMenu.IsEntryClickable(5), true);
            lOOutcome = lOMenu.Choose(3, lOOn);
            fExpectBool("music: OFF switches it off and stays on the screen",
                lOOutcome.Action == UWOptionsMenuModel.ActionEnum.SetMusic && lOOutcome.Value == 0 && !lOOutcome.ClosesPanel
                && lOMenu.Page == UWOptionsMenuModel.PageEnum.Music, true);
            fExpectBool("music: the screen shows MUSIC IS OFF with OFF lit", lOMenu.EntryAt(0) == 48 && lOMenu.KeyEntry == 3, true);
            lOOutcome = lOMenu.Choose(4, lOOff);
            fExpectBool("music: DONE switches nothing and goes back to the top menu",
                lOOutcome.Action == UWOptionsMenuModel.ActionEnum.None && lOMenu.Page == UWOptionsMenuModel.PageEnum.Top, true);
            lOMenu.Choose(UWOptionsMenuModel.TopSound, lOOff);
            Check.Str("sound: SOUND IS OFF, TURN SOUND, ON, OFF, DONE", fOptionsEntries(lOMenu), "50 52 20 22 26 -1 -1");
            fExpectInt("sound: OFF is lit while sound is off", lOMenu.KeyEntry, 3);
            lOOutcome = lOMenu.Choose(2, lOOff);
            fExpectBool("sound: ON switches it on", lOOutcome.Action == UWOptionsMenuModel.ActionEnum.SetSound && lOOutcome.Value == 1
                && lOMenu.EntryAt(0) == 49, true);
            lOMenu.Choose(4, lOOn);

            // QUIT: NO lit, YES back to the main menu with the panel closed.
            lOMenu.Choose(UWOptionsMenuModel.TopQuit, lOOn);
            Check.Str("quit: the question's two buttons", fOptionsEntries(lOMenu), "-1 -1 57 59 -1 -1 -1");
            fExpectInt("quit: NO is lit", lOMenu.KeyEntry, 3);
            fExpectBool("quit: only YES and NO can be chosen", lOMenu.IsEntryClickable(2) && lOMenu.IsEntryClickable(3) && !lOMenu.IsEntryClickable(4), true);
            lOMenu.Choose(3, lOOn);
            fExpectBool("quit: NO goes back to the top menu", lOMenu.Page == UWOptionsMenuModel.PageEnum.Top, true);
            lOMenu.Choose(UWOptionsMenuModel.TopQuit, lOOn);
            lOOutcome = lOMenu.Choose(2, lOOn);
            fExpectBool("quit: YES closes the panel and goes to the main menu",
                lOOutcome.Action == UWOptionsMenuModel.ActionEnum.QuitToMainMenu && lOOutcome.ClosesPanel, true);

            // RETURN TO GAME closes; closed, nothing is shown.
            lOMenu.ShowTop();
            lOOutcome = lOMenu.Choose(UWOptionsMenuModel.TopReturn, lOOn);
            fExpectBool("return: closes the panel without another action", lOOutcome.Action == UWOptionsMenuModel.ActionEnum.None && lOOutcome.ClosesPanel, true);
            lOMenu.Close();
            fExpectBool("closed: no page, no pictures, no background", !lOMenu.IsOpen && lOMenu.Background == UWOptionsMenuModel.NoPicture
                && fOptionsEntries(lOMenu) == "-1 -1 -1 -1 -1 -1 -1", true);

            // THE CTRL SHORTCUT (ovr130_7E8): opened by it, the music and sound screens close the
            // whole panel after their choice; opened by the button, the flag is clear.
            lOMenu.Open();
            lOMenu.MarkOpenedByShortcut();
            fExpectBool("shortcut: the flag is set", lOMenu.OpenedByShortcut, true);
            lOMenu.Choose(UWOptionsMenuModel.TopMusic, lOOn);
            lOOutcome = lOMenu.Choose(2, lOOn);
            fExpectBool("shortcut: ON on the music screen switches and closes the panel",
                lOOutcome.Action == UWOptionsMenuModel.ActionEnum.SetMusic && lOOutcome.Value == 1 && lOOutcome.ClosesPanel, true);
            lOMenu.Close();
            fExpectBool("shortcut: closing clears the flag", lOMenu.OpenedByShortcut, false);
            lOMenu.Open();
            lOMenu.MarkOpenedByShortcut();
            lOMenu.Choose(UWOptionsMenuModel.TopSound, lOOn);
            lOOutcome = lOMenu.Choose(4, lOOn);
            fExpectBool("shortcut: DONE on the sound screen switches nothing and closes the panel",
                lOOutcome.Action == UWOptionsMenuModel.ActionEnum.None && lOOutcome.ClosesPanel, true);
            lOMenu.Close();
            lOMenu.Open();
            fExpectBool("button: opened by the button the flag is clear", lOMenu.OpenedByShortcut, false);
            lOMenu.Choose(UWOptionsMenuModel.TopSave, lOOn);
            lOMenu.ShowSave();
            lOMenu.ShowTop();
            fExpectBool("shortcut: the flag survives the screens until the panel closes", lOMenu.OpenedByShortcut, false);
        }
    }
}
