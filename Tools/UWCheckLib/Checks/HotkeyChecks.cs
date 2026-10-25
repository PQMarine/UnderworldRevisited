using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 7.5 (2026-10-25): the hotkeys in one table (UWHotkeys). The table itself is pinned
    /// (Pins/hotkeys.txt, one line per group and row: scheme, modifiers, gates, handler); the routing is
    /// held against cases written from the if-chains it replaced (UWGameUI's fCheckFontToggle,
    /// fCheckModernKeys, fCheckOptionShortcuts, fCheckOriginalKeys, UWControlScheme's Shift+F2,
    /// UWHelpWindow's and UWModernPanel's Tab, DebugFunctions, UWDebugOverlay, UWDebugTools) with a
    /// fake host whose handlers change its state as the game's do, so the live gates between rows count.
    /// Data-free.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>One routing case: the site routed with the host as set up; the handlers it ran, in
        /// order, as one string ("-" for none).</summary>
        private static void fHotkeyCase(string psName, UWHotkeySite peSite, FakeHotkeyHost pOHost, string psExpected)
        {
            UWHotkeys.Route(peSite, pOHost);

            Check.Str(psName, pOHost.Ran.Count == 0 ? "-" : string.Join(", ", pOHost.Ran), psExpected);
        }

        private static FakeHotkeyHost fClassicHost(UWHotkeyMods peMods, params UWAct[] peActs)
        {
            FakeHotkeyHost lOHost = new FakeHotkeyHost { Modern = false, HeldMods = peMods, Holding = UWHotkeyGate.Interaction | UWHotkeyGate.CanCamp };

            foreach (UWAct leAct in peActs)
                lOHost.PressedActs.Add(leAct);

            return lOHost;
        }

        private static FakeHotkeyHost fModernHost(UWHotkeyMods peMods, params UWAct[] peActs)
        {
            FakeHotkeyHost lOHost = fClassicHost(peMods, peActs);

            lOHost.Modern = true;
            lOHost.Holding |= UWHotkeyGate.ModernHud;

            return lOHost;
        }

        private static FakeHotkeyHost fWith(FakeHotkeyHost pOHost, UWHotkeyGate peGates)
        {
            pOHost.Holding |= peGates;

            return pOHost;
        }

        /// <summary>This frame's back press is peOwner's; piClosables links of the chain to close.</summary>
        private static FakeHotkeyHost fBackTo(FakeHotkeyHost pOHost, int piClosables, UWBackOwner peOwner)
        {
            pOHost.BackTakers.Add(peOwner);
            pOHost.Closables = piClosables;

            return pOHost;
        }

        private static void fCheckHotkeys(CheckContext pOContext)
        {
            Check.Group("Hotkeys (UWHotkeys - cleanup step 7.5)");

            // ------------------------------------------------- The table, pinned

            SortedDictionary<string, string> lONow = new SortedDictionary<string, string>(StringComparer.Ordinal);
            string[] lsLines = UWHotkeys.Describe().Split('\n');
            int liGroup = -1;
            int liRow = 0;

            foreach (string lsLine in lsLines)
            {
                if (lsLine.Length == 0)
                    continue;

                if (lsLine.StartsWith("["))
                {
                    liGroup++;
                    liRow = 0;
                    lONow[string.Format("{0:00}.00", liGroup)] = lsLine;
                }
                else
                    lONow[string.Format("{0:00}.{1:00}", liGroup, ++liRow)] = lsLine.Trim();
            }

            Check.Int("groups", liGroup + 1, UWHotkeys.Groups.Length);
            fComparePins(pOContext, "hotkeys.txt", "hotkey table", lONow, lsKey => "group " + lsKey.Substring(0, 2));

            foreach (UWHotkeyRow lORow in UWHotkeys.Rows)
            {
                if (lORow.Group < 0 || lORow.Group >= UWHotkeys.Groups.Length)
                    Check.Fail("every row in a group: " + lORow.Keys, "group " + lORow.Group);
            }

            // ------------------------------------------------- UWGameUI, the classic scheme

            UWHotkeyMods leNone = UWHotkeyMods.None;
            UWHotkeyMods leCtrl = UWHotkeyMods.Ctrl;
            UWHotkeyMods leRightCtrl = UWHotkeyMods.Ctrl | UWHotkeyMods.RightCtrl;

            fHotkeyCase("classic F2: talk", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyTalk), "ModeTalk");
            fHotkeyCase("classic Shift+F2: not the original's", UWHotkeySite.GameUi, fClassicHost(UWHotkeyMods.Shift, UWAct.KeyTalk), "-");
            fHotkeyCase("classic Alt+F2: nothing", UWHotkeySite.GameUi, fClassicHost(UWHotkeyMods.Alt, UWAct.KeyTalk), "-");
            fHotkeyCase("classic Ctrl+F2: nothing", UWHotkeySite.GameUi, fClassicHost(leCtrl, UWAct.KeyTalk), "-");
            fHotkeyCase("classic F3 to F8", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyGet), "ModeGet");
            fHotkeyCase("classic F4", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyLook), "ModeLook");
            fHotkeyCase("classic F5", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyFight), "ModeFight");
            fHotkeyCase("classic F6", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyUse), "ModeUse");
            fHotkeyCase("classic F7: the panel turns", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyPanel), "TurnPanel");
            fHotkeyCase("classic F8: the shelf casts", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyCast), "CastShelf");
            fHotkeyCase("classic F9: tracks", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyTrack), "Track");
            fHotkeyCase("classic F10: camps", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyCamp), "Camp");

            FakeHotkeyHost lONoCamp = fClassicHost(leNone, UWAct.KeyCamp);

            lONoCamp.Holding &= ~UWHotkeyGate.CanCamp;
            fHotkeyCase("classic F10 where camping cannot be: nothing", UWHotkeySite.GameUi, lONoCamp, "-");
            fHotkeyCase("classic F2 and F3 together: the first of the chain", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyTalk, UWAct.KeyGet), "ModeTalk");
            fHotkeyCase("classic F1: the options", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyOptions), "OpenOptions");
            fHotkeyCase("classic F1 and F2: the options only", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.KeyOptions, UWAct.KeyTalk), "OpenOptions");
            fHotkeyCase("classic options up, F2: nothing", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.KeyTalk), UWHotkeyGate.OptionsOpen), "-");
            fHotkeyCase("classic options up, F1: the options again", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.KeyOptions), UWHotkeyGate.OptionsOpen), "OpenOptions");
            fHotkeyCase("classic roaming, F7: the panel turns", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.KeyPanel), UWHotkeyGate.Roaming), "TurnPanel");
            fHotkeyCase("classic roaming, F2: refused", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.KeyTalk), UWHotkeyGate.Roaming), "-");
            fHotkeyCase("classic roaming, F9: refused (deliberate deviation)", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.KeyTrack), UWHotkeyGate.Roaming), "-");
            fHotkeyCase("classic conversation, F2: nothing", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.KeyTalk), UWHotkeyGate.Conversation), "-");
            fHotkeyCase("classic conversation, M: nothing", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.ToggleMap), UWHotkeyGate.Conversation), "-");
            fHotkeyCase("classic conversation, F: the font", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.ToggleFont), UWHotkeyGate.Conversation), "ToggleFont");
            FakeHotkeyHost lONoInteraction = fClassicHost(leNone, UWAct.KeyTalk, UWAct.ToggleMap);

            lONoInteraction.Holding &= ~UWHotkeyGate.Interaction;
            fHotkeyCase("classic without the interaction, F2 and M: the map only", UWHotkeySite.GameUi, lONoInteraction, "ToggleMap");
            fHotkeyCase("classic M: the map", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.ToggleMap), "ToggleMap");
            fHotkeyCase("classic M and F2 together: the map opened, F2 refused over it", UWHotkeySite.GameUi,
                fClassicHost(leNone, UWAct.ToggleMap, UWAct.KeyTalk), "ToggleMap");
            fHotkeyCase("classic map up, M: closed", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.ToggleMap), UWHotkeyGate.MapVisible), "ToggleMap");
            fHotkeyCase("classic map up, F2: nothing", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.KeyTalk), UWHotkeyGate.MapVisible), "-");
            fHotkeyCase("classic M taken by the pad's pointer: nothing", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.ToggleMap), UWHotkeyGate.PadPointerTakes), "-");
            fHotkeyCase("classic map up, M taken by the pad's pointer: closed", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.ToggleMap), UWHotkeyGate.PadPointerTakes | UWHotkeyGate.MapVisible), "ToggleMap");
            fHotkeyCase("classic Ctrl+M: the music, not the map", UWHotkeySite.GameUi, fClassicHost(leCtrl, UWAct.ToggleMap, UWAct.OptMusic), "ShortcutMusic");
            fHotkeyCase("classic Ctrl+S: save", UWHotkeySite.GameUi, fClassicHost(leCtrl, UWAct.OptSave), "ShortcutSave");
            fHotkeyCase("classic Ctrl+R: restore", UWHotkeySite.GameUi, fClassicHost(leCtrl, UWAct.OptRestore), "ShortcutRestore");
            fHotkeyCase("classic Ctrl+D: detail", UWHotkeySite.GameUi, fClassicHost(leCtrl, UWAct.OptDetail), "ShortcutDetail");
            fHotkeyCase("classic Ctrl+Q: quit", UWHotkeySite.GameUi, fClassicHost(leCtrl, UWAct.OptQuit), "ShortcutQuit");
            fHotkeyCase("classic Ctrl+F: the sound, not the font", UWHotkeySite.GameUi, fClassicHost(leCtrl, UWAct.ToggleFont, UWAct.OptSound), "ShortcutSound");
            fHotkeyCase("classic Ctrl+S and Ctrl+R: save first", UWHotkeySite.GameUi, fClassicHost(leCtrl, UWAct.OptSave, UWAct.OptRestore), "ShortcutSave");
            fHotkeyCase("classic Ctrl+Shift+S: nothing", UWHotkeySite.GameUi, fClassicHost(leCtrl | UWHotkeyMods.Shift, UWAct.OptSave), "-");
            fHotkeyCase("classic right Ctrl+S: save as well", UWHotkeySite.GameUi, fClassicHost(leRightCtrl, UWAct.OptSave), "ShortcutSave");
            fHotkeyCase("classic Ctrl+S with the options up: nothing", UWHotkeySite.GameUi,
                fWith(fClassicHost(leCtrl, UWAct.OptSave), UWHotkeyGate.OptionsOpen), "-");
            fHotkeyCase("classic Ctrl+S while roaming: nothing", UWHotkeySite.GameUi,
                fWith(fClassicHost(leCtrl, UWAct.OptSave), UWHotkeyGate.Roaming), "-");
            fHotkeyCase("classic Ctrl+S over the map: nothing", UWHotkeySite.GameUi,
                fWith(fClassicHost(leCtrl, UWAct.OptSave), UWHotkeyGate.MapVisible), "-");
            fHotkeyCase("F: the font", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.ToggleFont), "ToggleFont");
            fHotkeyCase("Shift+F: the font as well", UWHotkeySite.GameUi, fClassicHost(UWHotkeyMods.Shift, UWAct.ToggleFont), "ToggleFont");
            fHotkeyCase("F and F2: both", UWHotkeySite.GameUi, fClassicHost(leNone, UWAct.ToggleFont, UWAct.KeyTalk), "ToggleFont, ModeTalk");
            fHotkeyCase("typing: nothing", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.ToggleFont, UWAct.KeyTalk), UWHotkeyGate.Typing), "-");
            fHotkeyCase("a screen menu: nothing", UWHotkeySite.GameUi,
                fWith(fClassicHost(leNone, UWAct.ToggleFont, UWAct.KeyOptions), UWHotkeyGate.ScreenMenu), "-");
            fHotkeyCase("a sequence, or the frame it ended: nothing", UWHotkeySite.GameUi,
                fWith(fModernHost(leNone, UWAct.ToggleFont), UWHotkeyGate.Cutscene), "-");

            // ------------------------------------------------- UWGameUI, the modern scheme

            fHotkeyCase("modern F9: tracks", UWHotkeySite.GameUi, fModernHost(leNone, UWAct.KeyTrack), "Track");
            fHotkeyCase("modern F10: camps", UWHotkeySite.GameUi, fModernHost(leNone, UWAct.KeyCamp), "Camp");
            fHotkeyCase("modern F2: not routed", UWHotkeySite.GameUi, fModernHost(leNone, UWAct.KeyTalk), "-");
            fHotkeyCase("modern F7: not routed", UWHotkeySite.GameUi, fModernHost(leNone, UWAct.KeyPanel), "-");
            fHotkeyCase("modern F1: the menu", UWHotkeySite.GameUi, fModernHost(leNone, UWAct.KeyOptions), "ToggleModernMenu");
            fHotkeyCase("modern F: the font", UWHotkeySite.GameUi, fModernHost(leNone, UWAct.ToggleFont), "ToggleFont");
            fHotkeyCase("modern M: the map", UWHotkeySite.GameUi, fModernHost(leNone, UWAct.ToggleMap), "ToggleMap");
            fHotkeyCase("modern M taken by the pad's pointer: nothing", UWHotkeySite.GameUi,
                fWith(fModernHost(leNone, UWAct.ToggleMap), UWHotkeyGate.PadPointerTakes), "-");
            fHotkeyCase("modern menu open, F9: nothing", UWHotkeySite.GameUi,
                fWith(fModernHost(leNone, UWAct.KeyTrack), UWHotkeyGate.MenuOpen), "-");
            fHotkeyCase("modern menu open, F1: closed", UWHotkeySite.GameUi,
                fWith(fModernHost(leNone, UWAct.KeyOptions), UWHotkeyGate.MenuOpen), "ToggleModernMenu");
            fHotkeyCase("modern Shift+F9: nothing", UWHotkeySite.GameUi, fModernHost(UWHotkeyMods.Shift, UWAct.KeyTrack), "-");
            fHotkeyCase("modern left Ctrl+F9 (Ctrl sinks): tracks", UWHotkeySite.GameUi, fModernHost(leCtrl, UWAct.KeyTrack), "Track");
            fHotkeyCase("modern left Ctrl+S: nothing", UWHotkeySite.GameUi, fModernHost(leCtrl, UWAct.OptSave), "-");
            fHotkeyCase("modern right Ctrl+S: the save page", UWHotkeySite.GameUi, fModernHost(leRightCtrl, UWAct.OptSave), "ModernMenuSave");
            fHotkeyCase("modern right Ctrl+R: the load page", UWHotkeySite.GameUi, fModernHost(leRightCtrl, UWAct.OptRestore), "ModernMenuLoad");
            fHotkeyCase("modern right Ctrl+M: the main page", UWHotkeySite.GameUi, fModernHost(leRightCtrl, UWAct.OptMusic), "ModernMenuMain");
            fHotkeyCase("modern right Ctrl+F: the main page", UWHotkeySite.GameUi, fModernHost(leRightCtrl, UWAct.OptSound), "ModernMenuMain");
            fHotkeyCase("modern right Ctrl+D: the main page", UWHotkeySite.GameUi, fModernHost(leRightCtrl, UWAct.OptDetail), "ModernMenuMain");
            fHotkeyCase("modern right Ctrl+Q: the main page", UWHotkeySite.GameUi, fModernHost(leRightCtrl, UWAct.OptQuit), "ModernMenuMain");
            fHotkeyCase("modern right Ctrl+F9: nothing (the right Ctrl ends the routing)", UWHotkeySite.GameUi,
                fModernHost(leRightCtrl, UWAct.KeyTrack), "-");
            fHotkeyCase("modern right Ctrl+Shift+S: nothing", UWHotkeySite.GameUi,
                fModernHost(leRightCtrl | UWHotkeyMods.Shift, UWAct.OptSave), "-");

            // The back press: whose it is, the Escape chain says (UWBackChain, its own group); here the
            // routing of the press it gives.
            fHotkeyCase("modern back, the chain's: closed, the menu waits", UWHotkeySite.GameUi,
                fBackTo(fModernHost(leNone), 1, UWBackOwner.ModernChain), "CloseChain");
            fHotkeyCase("modern back, the menu's: the menu", UWHotkeySite.GameUi, fBackTo(fModernHost(leNone), 0, UWBackOwner.ModernMenu), "ToggleModernMenu");
            fHotkeyCase("modern back, the chain's with nothing left to close: the menu does not take it", UWHotkeySite.GameUi,
                fBackTo(fModernHost(leNone), 0, UWBackOwner.ModernChain), "-");
            fHotkeyCase("modern back, menu open: the menu", UWHotkeySite.GameUi,
                fBackTo(fWith(fModernHost(leNone), UWHotkeyGate.MenuOpen), 1, UWBackOwner.ModernMenu), "ToggleModernMenu");
            fHotkeyCase("modern back and F1 together: the chain first", UWHotkeySite.GameUi,
                fBackTo(fModernHost(leNone, UWAct.KeyOptions), 1, UWBackOwner.ModernChain), "CloseChain");
            fHotkeyCase("modern back in the frame Escape ended the roaming view: nothing", UWHotkeySite.GameUi,
                fBackTo(fWith(fModernHost(leNone), UWHotkeyGate.RoamingEndedByEscape), 0, UWBackOwner.ModernMenu), "-");
            fHotkeyCase("modern back in a conversation: nothing", UWHotkeySite.GameUi,
                fBackTo(fWith(fModernHost(leNone), UWHotkeyGate.Conversation), 0, UWBackOwner.ModernMenu), "-");
            fHotkeyCase("modern Alt+back: nothing", UWHotkeySite.GameUi, fBackTo(fModernHost(UWHotkeyMods.Alt), 0, UWBackOwner.ModernMenu), "-");

            fHotkeyCase("modern map up, M: closed", UWHotkeySite.GameUi,
                fWith(fModernHost(leNone, UWAct.ToggleMap), UWHotkeyGate.MapVisible), "HideMap");
            fHotkeyCase("modern map up, back: closed", UWHotkeySite.GameUi,
                fBackTo(fWith(fModernHost(leNone), UWHotkeyGate.MapVisible), 0, UWBackOwner.ModernMap), "HideMap");
            fHotkeyCase("modern map up, M and back: closed once", UWHotkeySite.GameUi,
                fBackTo(fWith(fModernHost(leNone, UWAct.ToggleMap), UWHotkeyGate.MapVisible), 0, UWBackOwner.ModernMap), "HideMap");
            fHotkeyCase("modern map up, the back press not the map's (the letter grid's B): nothing", UWHotkeySite.GameUi,
                fWith(fModernHost(leNone), UWHotkeyGate.MapVisible), "-");
            fHotkeyCase("modern map up, Ctrl+Escape: nothing", UWHotkeySite.GameUi,
                fBackTo(fWith(fModernHost(leCtrl), UWHotkeyGate.MapVisible), 0, UWBackOwner.ModernMap), "-");
            fHotkeyCase("modern map up, F9: nothing", UWHotkeySite.GameUi,
                fWith(fModernHost(leNone, UWAct.KeyTrack), UWHotkeyGate.MapVisible), "-");
            fHotkeyCase("modern map up, F and back: the font, then closed", UWHotkeySite.GameUi,
                fBackTo(fWith(fModernHost(leNone, UWAct.ToggleFont), UWHotkeyGate.MapVisible), 0, UWBackOwner.ModernMap), "ToggleFont, HideMap");
            fHotkeyCase("modern conversation, F: the font", UWHotkeySite.GameUi,
                fWith(fModernHost(leNone, UWAct.ToggleFont, UWAct.KeyTrack), UWHotkeyGate.Conversation), "ToggleFont");
            fHotkeyCase("modern roaming, F9: nothing", UWHotkeySite.GameUi,
                fWith(fModernHost(leNone, UWAct.KeyTrack), UWHotkeyGate.Roaming), "-");

            FakeHotkeyHost lONoHud = fModernHost(leNone, UWAct.KeyTrack, UWAct.ToggleFont);

            lONoHud.Holding &= ~UWHotkeyGate.ModernHud;
            fHotkeyCase("modern without its HUD: the font only", UWHotkeySite.GameUi, lONoHud, "ToggleFont");

            // ------------------------------------------------- The other sites

            FakeHotkeyHost lOScheme = fClassicHost(leNone, UWAct.ToggleScheme);

            fHotkeyCase("F2 from the keyboard without Shift: no switch", UWHotkeySite.Scheme, lOScheme, "-");
            fHotkeyCase("Shift+F2: the scheme switches", UWHotkeySite.Scheme, fClassicHost(UWHotkeyMods.Shift, UWAct.ToggleScheme), "SwitchScheme");
            lOScheme = fModernHost(leNone, UWAct.ToggleScheme);
            lOScheme.FromPad.Add(UWAct.ToggleScheme);
            fHotkeyCase("the scheme's binding from the pad: switches without Shift", UWHotkeySite.Scheme, lOScheme, "SwitchScheme");
            fHotkeyCase("Shift+F2 typing: the router switches (the lock silences the act)", UWHotkeySite.Scheme,
                fWith(fClassicHost(UWHotkeyMods.Shift, UWAct.ToggleScheme), UWHotkeyGate.Typing), "SwitchScheme");

            fHotkeyCase("Tab: the help", UWHotkeySite.Help, fClassicHost(leNone, UWAct.ToggleHelp), "ToggleHelp");
            fHotkeyCase("Alt+Tab: nothing", UWHotkeySite.Help, fClassicHost(UWHotkeyMods.Alt, UWAct.ToggleHelp), "-");
            fHotkeyCase("Shift+Tab: nothing", UWHotkeySite.Help, fClassicHost(UWHotkeyMods.Shift, UWAct.ToggleHelp), "-");
            fHotkeyCase("Ctrl+Tab: the help", UWHotkeySite.Help, fClassicHost(leCtrl, UWAct.ToggleHelp), "ToggleHelp");
            fHotkeyCase("Tab while typing: nothing", UWHotkeySite.Help, fWith(fClassicHost(leNone, UWAct.ToggleHelp), UWHotkeyGate.KeysLocked), "-");
            fHotkeyCase("Tab where the modern panel holds the help: nothing", UWHotkeySite.Help,
                fWith(fModernHost(leNone, UWAct.ToggleHelp), UWHotkeyGate.PanelHoldsHelp), "-");
            fHotkeyCase("Tab in Classic Wide (modern, no panel): the help", UWHotkeySite.Help, fModernHost(leNone, UWAct.ToggleHelp), "ToggleHelp");
            fHotkeyCase("Tab under a setup panel: nothing", UWHotkeySite.Help, fWith(fClassicHost(leNone, UWAct.ToggleHelp), UWHotkeyGate.SetupPanel), "-");
            fHotkeyCase("Tab under a cutscene: nothing", UWHotkeySite.Help, fWith(fClassicHost(leNone, UWAct.ToggleHelp), UWHotkeyGate.HelpCovered), "-");
            fHotkeyCase("Tab while the help has the keys: nothing", UWHotkeySite.Help, fWith(fClassicHost(leNone, UWAct.ToggleHelp), UWHotkeyGate.HelpKeys), "-");

            fHotkeyCase("modern Tab: the panel's Help tab", UWHotkeySite.ModernPanel, fModernHost(leNone, UWAct.ToggleHelp), "TogglePanelHelp");
            fHotkeyCase("classic Tab: not the panel's", UWHotkeySite.ModernPanel, fClassicHost(leNone, UWAct.ToggleHelp), "-");
            fHotkeyCase("modern Tab, menu open: nothing", UWHotkeySite.ModernPanel, fWith(fModernHost(leNone, UWAct.ToggleHelp), UWHotkeyGate.MenuOpen), "-");
            fHotkeyCase("modern Tab over the map: nothing", UWHotkeySite.ModernPanel, fWith(fModernHost(leNone, UWAct.ToggleHelp), UWHotkeyGate.MapVisible), "-");
            fHotkeyCase("modern Alt+Tab: nothing", UWHotkeySite.ModernPanel, fModernHost(UWHotkeyMods.Alt, UWAct.ToggleHelp), "-");

            fHotkeyCase("P: the test teleport", UWHotkeySite.DebugKeys, fClassicHost(leNone, UWAct.TeleportDebug), "Teleport");
            fHotkeyCase("N: noclip", UWHotkeySite.DebugKeys, fClassicHost(leNone, UWAct.ToggleSpectator), "ToggleNoclip");
            fHotkeyCase("N in a screen menu: nothing", UWHotkeySite.DebugKeys, fWith(fClassicHost(leNone, UWAct.ToggleSpectator), UWHotkeyGate.ScreenMenu), "-");
            fHotkeyCase("Page Up outside noclip: nothing", UWHotkeySite.DebugKeys, fClassicHost(leNone, UWAct.NextLevel), "-");
            fHotkeyCase("Page Up in noclip: the next level", UWHotkeySite.DebugKeys, fWith(fClassicHost(leNone, UWAct.NextLevel), UWHotkeyGate.Noclip), "NextLevel");
            fHotkeyCase("Page Up and Down in noclip: the next only", UWHotkeySite.DebugKeys,
                fWith(fClassicHost(leNone, UWAct.NextLevel, UWAct.PrevLevel), UWHotkeyGate.Noclip), "NextLevel");
            fHotkeyCase("Page Down in noclip", UWHotkeySite.DebugKeys, fWith(fClassicHost(leNone, UWAct.PrevLevel), UWHotkeyGate.Noclip), "PreviousLevel");
            fHotkeyCase("F11 without the lighting: nothing", UWHotkeySite.DebugKeys, fClassicHost(leNone, UWAct.ToggleBrightness), "-");
            fHotkeyCase("F11: the brightness", UWHotkeySite.DebugKeys, fWith(fClassicHost(leNone, UWAct.ToggleBrightness), UWHotkeyGate.Lighting), "ToggleBrightness");
            fHotkeyCase("F11 in a screen menu: nothing", UWHotkeySite.DebugKeys,
                fWith(fClassicHost(leNone, UWAct.ToggleBrightness), UWHotkeyGate.Lighting | UWHotkeyGate.ScreenMenu), "-");
            fHotkeyCase("Shift+F12: the shader globals", UWHotkeySite.DebugKeys, fClassicHost(leNone, UWAct.DumpShaderGlobals), "DumpShaderGlobals");
            fHotkeyCase("Shift+F11: the forced cast", UWHotkeySite.DebugKeys, fClassicHost(leNone, UWAct.ForceCast), "ForceCast");
            fHotkeyCase("Shift+F11 in a screen menu: nothing", UWHotkeySite.DebugKeys, fWith(fClassicHost(leNone, UWAct.ForceCast), UWHotkeyGate.ScreenMenu), "-");

            // Wave 2b: F11's plain binding fires with Shift+F11 too - only the forced cast then.
            fHotkeyCase("Shift+F11 (F11's binding pressed with it): the forced cast only", UWHotkeySite.DebugKeys,
                fWith(fClassicHost(UWHotkeyMods.Shift, UWAct.ToggleBrightness, UWAct.ForceCast), UWHotkeyGate.Lighting), "ForceCast");
            fHotkeyCase("F11 with Ctrl: the brightness", UWHotkeySite.DebugKeys,
                fWith(fClassicHost(leCtrl, UWAct.ToggleBrightness), UWHotkeyGate.Lighting), "ToggleBrightness");
            fHotkeyCase("P, N, F11, Shift+F12 at once: in the old order", UWHotkeySite.DebugKeys,
                fWith(fClassicHost(leNone, UWAct.DumpShaderGlobals, UWAct.ToggleBrightness, UWAct.ToggleSpectator, UWAct.TeleportDebug), UWHotkeyGate.Lighting),
                "Teleport, ToggleNoclip, ToggleBrightness, DumpShaderGlobals");

            fHotkeyCase("Shift+F1: the debug panel", UWHotkeySite.DebugOverlay, fClassicHost(leNone, UWAct.ToggleOverlay), "ToggleOverlay");

            FakeHotkeyHost lOTools = fClassicHost(UWHotkeyMods.Shift);

            lOTools.RawF7 = true;
            fHotkeyCase("Shift+F7: the jump", UWHotkeySite.DebugTools, lOTools, "ToggleDebugJump");
            lOTools = fClassicHost(leNone);
            lOTools.RawF7 = true;
            fHotkeyCase("F7 alone: not the tools'", UWHotkeySite.DebugTools, lOTools, "-");
            lOTools = fClassicHost(UWHotkeyMods.Shift);
            lOTools.RawF7 = true;
            lOTools.RawF10 = true;
            fHotkeyCase("Shift+F7 and Shift+F10: both", UWHotkeySite.DebugTools, lOTools, "ToggleDebugJump, ToggleDebugSearch");
            lOTools = fWith(fClassicHost(UWHotkeyMods.Shift), UWHotkeyGate.OtherTyping);
            lOTools.RawF10 = true;
            fHotkeyCase("Shift+F10 while typing elsewhere: nothing", UWHotkeySite.DebugTools, lOTools, "-");

            // ------------------------------------------------- The wait for a key (wave 2b)

            // The Controls window waits for a key or a button, or the wait ended this frame: the press is
            // the wait's, no site routes it (F1 closed the modern game menu with the window).
            UWHotkeyGate leWait = UWHotkeyGate.Rebinding;

            fHotkeyCase("the wait, modern menu open, F1: nothing", UWHotkeySite.GameUi,
                fWith(fModernHost(leNone, UWAct.KeyOptions), UWHotkeyGate.MenuOpen | leWait), "-");
            fHotkeyCase("the wait, modern back to the menu: nothing", UWHotkeySite.GameUi,
                fBackTo(fWith(fModernHost(leNone), UWHotkeyGate.MenuOpen | leWait), 0, UWBackOwner.ModernMenu), "-");
            fHotkeyCase("the wait, classic F2: nothing", UWHotkeySite.GameUi, fWith(fClassicHost(leNone, UWAct.KeyTalk), leWait), "-");
            fHotkeyCase("the wait, F: nothing", UWHotkeySite.GameUi, fWith(fClassicHost(leNone, UWAct.ToggleFont), leWait), "-");
            fHotkeyCase("the wait, Shift+F2: nothing", UWHotkeySite.Scheme, fWith(fClassicHost(UWHotkeyMods.Shift, UWAct.ToggleScheme), leWait), "-");
            fHotkeyCase("the wait, Tab: nothing", UWHotkeySite.Help, fWith(fClassicHost(leNone, UWAct.ToggleHelp), leWait), "-");
            fHotkeyCase("the wait, P and N: nothing", UWHotkeySite.DebugKeys,
                fWith(fClassicHost(leNone, UWAct.TeleportDebug, UWAct.ToggleSpectator), leWait), "-");
            fHotkeyCase("the wait, Shift+F1: nothing", UWHotkeySite.DebugOverlay, fWith(fClassicHost(leNone, UWAct.ToggleOverlay), leWait), "-");
            lOTools = fWith(fClassicHost(UWHotkeyMods.Shift), leWait);
            lOTools.RawF7 = true;
            fHotkeyCase("the wait, Shift+F7: nothing", UWHotkeySite.DebugTools, lOTools, "-");

            // ------------------------------------------------- The shortcut modifier (wave 2b)

            // While the scheme's shortcut modifier is held, the other acts on the shortcuts' keys are
            // silent (UWControls switches them off): the modern right Ctrl, the classic Ctrl; the modern
            // left Ctrl is no modifier (it sinks).
            Check.Str("the shortcut modifier, both schemes", UWHotkeys.DescribeShortcutModifier(),
                "classic: Ctrl silences the other acts on the keys of OptSave, OptRestore, OptMusic, OptSound, OptDetail, OptQuit\n"
                + "modern: RightCtrl silences the other acts on the keys of OptSave, OptRestore, OptMusic, OptSound, OptDetail, OptQuit\n");
            Check.Bool("modern: the right Ctrl is the modifier", UWHotkeys.IsShortcutModifierHeld(true, leRightCtrl), true);
            Check.Bool("modern: the left Ctrl is not (it sinks)", UWHotkeys.IsShortcutModifierHeld(true, leCtrl), false);
            Check.Bool("modern: the right Ctrl with Shift is held as well", UWHotkeys.IsShortcutModifierHeld(true, leRightCtrl | UWHotkeyMods.Shift), true);
            Check.Bool("classic: the left Ctrl is the modifier", UWHotkeys.IsShortcutModifierHeld(false, leCtrl), true);
            Check.Bool("classic: the right Ctrl too", UWHotkeys.IsShortcutModifierHeld(false, leRightCtrl), true);
            Check.Bool("classic: no Ctrl, no modifier", UWHotkeys.IsShortcutModifierHeld(false, UWHotkeyMods.Shift | UWHotkeyMods.Alt), false);

            foreach (UWHotkeyGroup lOGroup in UWHotkeys.Groups)
            {
                if ((lOGroup.Refuses & UWHotkeyGate.Rebinding) == 0)
                    Check.Fail("every group refuses the wait for a key: " + lOGroup.Name, lOGroup.Refuses.ToString());
            }
        }
    }
}
