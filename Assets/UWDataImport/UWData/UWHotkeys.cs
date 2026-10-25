using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>Where a hotkey is routed: the component whose Update asks UWHotkeys.Route for it, each in
	/// its own place of the frame as before.</summary>
	public enum UWHotkeySite
	{
		/// <summary>UWGameUI's key routing: F1 to F10, the Ctrl shortcuts, M, F.</summary>
		GameUi,

		/// <summary>UWControlScheme: Shift+F2 switches the scheme.</summary>
		Scheme,

		/// <summary>UWHelpWindow: Tab opens and closes the help (the classic scheme, Classic Wide).</summary>
		Help,

		/// <summary>UWModernPanel: Tab opens and closes the character panel's Help tab.</summary>
		ModernPanel,

		/// <summary>DebugFunctions: noclip, the level keys, the brightness, the dumps.</summary>
		DebugKeys,

		/// <summary>UWDebugOverlay: Shift+F1, the debug panel.</summary>
		DebugOverlay,

		/// <summary>UWDebugTools: Shift+F7 and Shift+F10, the debug panel's tools tab.</summary>
		DebugTools
	}

	/// <summary>The scheme a group of hotkeys belongs to.</summary>
	public enum UWHotkeyScheme
	{
		Both,
		Classic,
		Modern
	}

	/// <summary>The modifiers held now.</summary>
	[System.Flags]
	public enum UWHotkeyMods
	{
		None = 0,
		Shift = 1,
		Alt = 2,
		Ctrl = 4,

		/// <summary>The right Ctrl in particular (Ctrl is set with it).</summary>
		RightCtrl = 8
	}

	/// <summary>What a group or a row asks of the modifiers.</summary>
	public enum UWHotkeyModRule
	{
		/// <summary>Whatever is held (the debug map's own Shift lies in its bindings).</summary>
		Any,

		/// <summary>Not with Ctrl (Ctrl+F and Ctrl+M are the original's shortcuts).</summary>
		NoCtrl,

		/// <summary>Neither Shift nor Alt (Alt+Tab, Shift+Tab, our own Shift keys).</summary>
		NoShiftAlt,

		/// <summary>Ctrl, without Shift and Alt: the classic scheme's shortcuts.</summary>
		Ctrl,

		/// <summary>The right Ctrl, without Shift and Alt: the modern scheme's shortcuts (its left Ctrl
		/// sinks).</summary>
		RightCtrl,

		/// <summary>None of Shift, Alt and Ctrl: the original's function keys.</summary>
		Plain,

		/// <summary>Shift held: the debug tools' keys.</summary>
		Shift,

		/// <summary>From the keyboard only with Shift (Shift+F2; the gamepad's binding as it is).</summary>
		ShiftOnKeyboard,

		/// <summary>Not with Shift: a debug key whose Shift form is another debug key (F11 and Shift+F11;
		/// cleanup 7, wave 2b - the plain binding fired with the Shift form as well).</summary>
		NoShift
	}

	/// <summary>What fires a row.</summary>
	public enum UWHotkeyTrigger
	{
		/// <summary>The act pressed this frame.</summary>
		Pressed,

		/// <summary>The act performed this frame (an interaction: the pad's View tapped or held).</summary>
		Performed,

		/// <summary>Always - a barrier: the row ends its group when its gates hold.</summary>
		Always,

		/// <summary>The modern scheme's back press (Escape and Start through Menu, the pad's B through
		/// PadBack) when the Escape chain gives it to the row's BackOwner (UWBackChain).</summary>
		Back,

		/// <summary>The F7 key itself, pressed this frame (the debug tools read it off the keyboard: their
		/// key must close their own tab while the typing lock silences every action).</summary>
		RawF7,

		/// <summary>The F10 key itself, likewise.</summary>
		RawF10
	}

	/// <summary>The states a hotkey's group or row asks about - each one a test the host makes live, at
	/// the moment the router asks.</summary>
	[System.Flags]
	public enum UWHotkeyGate : long
	{
		None = 0,

		/// <summary>Text is typed or something holds the keys as typing does (UWTextEntryState.
		/// IsTextEntryActive - the debug tools' tab not counted).</summary>
		Typing = 1L << 0,

		/// <summary>The main menu or the character creation (the ScreenMenu context).</summary>
		ScreenMenu = 1L << 1,

		/// <summary>A sequence plays or ended this frame (the Cutscene context).</summary>
		Cutscene = 1L << 2,

		/// <summary>The modern HUD exists.</summary>
		ModernHud = 1L << 3,

		/// <summary>A conversation is open.</summary>
		Conversation = 1L << 4,

		/// <summary>The view roams (a roaming sight's spell or a trap's view).</summary>
		Roaming = 1L << 5,

		/// <summary>Escape ended the roaming view in this frame.</summary>
		RoamingEndedByEscape = 1L << 6,

		/// <summary>The interaction is there (the game is running).</summary>
		Interaction = 1L << 7,

		/// <summary>The big map is up.</summary>
		MapVisible = 1L << 8,

		/// <summary>The modern game menu is open (the layout editor included).</summary>
		MenuOpen = 1L << 9,

		/// <summary>The classic options panel is up.</summary>
		OptionsOpen = 1L << 10,

		/// <summary>Camping is possible (UWGameActions.CanCamp).</summary>
		CanCamp = 1L << 11,

		/// <summary>The gamepad's pointer takes the act's pad press (the PadPointer context,
		/// UWInputContexts.PadPointerTakes; UWGamepadPointer.Takes until cleanup step 7.8).</summary>
		PadPointerTakes = 1L << 12,

		/// <summary>The typing lock is in force for every UWControls (the TextEntry context:
		/// UWControls.IsTextEntryActive, the debug tools' tab counted).</summary>
		KeysLocked = 1L << 13,

		/// <summary>The help has the keyboard - its notes typed or the manual read.</summary>
		HelpKeys = 1L << 14,

		/// <summary>The modern character panel holds the help (UWModernPanel.HoldsHelp).</summary>
		PanelHoldsHelp = 1L << 15,

		/// <summary>A panel of the setup bar is open (UWSetupMenu.HasOpenPanel).</summary>
		SetupPanel = 1L << 16,

		/// <summary>The help steps aside for a screen menu or a cutscene.</summary>
		HelpCovered = 1L << 17,

		/// <summary>The noclip camera flies, the level loader is there.</summary>
		Noclip = 1L << 18,

		/// <summary>The lighting is there (the debug brightness).</summary>
		Lighting = 1L << 19,

		/// <summary>Text is typed elsewhere while the debug tools' tab is shut.</summary>
		OtherTyping = 1L << 20,

		/// <summary>The Controls window waits for a key or a button, or the wait ended in this frame
		/// (UWKeyBindings.HoldsKeys): the press is the wait's. Every group refuses it (fGroup).</summary>
		Rebinding = 1L << 21
	}

	/// <summary>What a row does - the host runs it.</summary>
	public enum UWHotkeyHandler
	{
		/// <summary>Nothing: a barrier row.</summary>
		None,
		ToggleFont,
		HideMap,
		ModernMenuSave,
		ModernMenuLoad,
		ModernMenuMain,

		/// <summary>Closes the next link of the modern closable chain (UWModernUi.CloseNext); declines
		/// when there is none, so the next row may take the press.</summary>
		CloseChain,
		ToggleModernMenu,
		Track,
		Camp,
		ToggleMap,
		ShortcutSave,
		ShortcutRestore,
		ShortcutMusic,
		ShortcutSound,
		ShortcutDetail,
		ShortcutQuit,
		TurnPanel,
		OpenOptions,
		ModeTalk,
		ModeGet,
		ModeLook,
		ModeFight,
		ModeUse,
		CastShelf,
		SwitchScheme,
		ToggleHelp,
		TogglePanelHelp,
		Teleport,
		ToggleNoclip,
		NextLevel,
		PreviousLevel,
		ToggleBrightness,
		DumpShaderGlobals,
		ForceCast,
		ToggleOverlay,
		ToggleDebugJump,
		ToggleDebugSearch
	}

	/// <summary>
	/// A group of hotkeys: entered when its scheme, modifiers and gates hold; its rows tried in order.
	/// FirstMatch: the first row that fires ends the group (an else-if chain); otherwise every row may
	/// fire. StopsWhenEntered: once entered, nothing after it is routed (a return in the old code).
	/// </summary>
	public sealed class UWHotkeyGroup
	{
		public UWHotkeySite Site;

		public string Name;

		public UWHotkeyScheme Scheme;

		public UWHotkeyModRule Mods;

		public UWHotkeyGate Requires;

		public UWHotkeyGate Refuses;

		public bool FirstMatch;

		public bool StopsWhenEntered;
	}

	/// <summary>One hotkey: in a group, fired by its act (Trigger), with the modifiers and gates of its
	/// own, doing Handler. Keys says which keys and buttons it has by default, for the table.</summary>
	public sealed class UWHotkeyRow
	{
		public int Group;

		public string Keys;

		public UWAct Act;

		public UWHotkeyTrigger Trigger;

		public UWHotkeyModRule Mods;

		public UWHotkeyGate Requires;

		public UWHotkeyGate Refuses;

		public UWHotkeyHandler Handler;

		/// <summary>For the Back trigger: whose back press it is in the Escape chain (UWBackChain).</summary>
		public UWBackOwner BackOwner;
	}

	/// <summary>The Unity side of a site: the modifiers, the triggers and the gates as they are now, and
	/// the handlers.</summary>
	public interface IUWHotkeyHost
	{
		bool IsModern { get; }

		UWHotkeyMods Mods { get; }

		/// <summary>Whether the row's trigger fired this frame.</summary>
		bool Fired(UWHotkeyRow pORow);

		/// <summary>Whether the act's press of this frame came from the keyboard (ShiftOnKeyboard).</summary>
		bool FromKeyboard(UWAct peAct);

		/// <summary>One gate, tested now.</summary>
		bool Gate(UWHotkeyGate peGate, UWHotkeyRow pORow);

		/// <summary>Runs a handler; false when it declined (the row did not fire).</summary>
		bool Run(UWHotkeyHandler peHandler);
	}

	/// <summary>
	/// THE HOTKEYS IN ONE TABLE (cleanup step 7.5, 2026-10-25): UWGameUI's key routing (F1 to F10, the
	/// Ctrl shortcuts - the classic Ctrl, the modern right Ctrl -, M, F, the modern back press), Tab
	/// (UWHelpWindow, UWModernPanel), Shift+F2 (UWControlScheme) and the debug keys (DebugFunctions,
	/// UWDebugOverlay, UWDebugTools), each row with its scheme, modifiers, context (the gates) and
	/// handler. The rules exactly as the if-chains wrote them before: the groups are those chains, in
	/// their order; a group that returned once entered stops the routing (StopsWhenEntered), an
	/// else-if chain ends with its first fired row (FirstMatch), a bare "return when X" is a barrier
	/// row. The gates are asked live, row by row, as the old code did between its handlers. Each site
	/// routes its own rows from its own Update, so nothing moves in the frame.
	/// </summary>
	public static class UWHotkeys
	{
		/// <summary>The groups, in routing order.</summary>
		public static readonly UWHotkeyGroup[] Groups =
		{
			// 0 UWGameUI: F (both schemes) - the original's Ctrl+F is its own.
			fGroup(UWHotkeySite.GameUi, "font", UWHotkeyScheme.Both, UWHotkeyModRule.Any, UWHotkeyGate.None, Shut, false, false),

			// 1 The modern scheme, the big map up: M, Escape or the pad's B close it; nothing else.
			fGroup(UWHotkeySite.GameUi, "modern, map up", UWHotkeyScheme.Modern, UWHotkeyModRule.Any,
				ModernOpen | UWHotkeyGate.MapVisible, Shut | ModernShut, true, true),

			// 2 The modern scheme's shortcuts on the right Ctrl.
			fGroup(UWHotkeySite.GameUi, "modern, right Ctrl", UWHotkeyScheme.Modern, UWHotkeyModRule.RightCtrl,
				ModernOpen, Shut | ModernShut | UWHotkeyGate.MapVisible, true, true),

			// 3 The modern scheme's keys: back, F1, F9, F10, M.
			fGroup(UWHotkeySite.GameUi, "modern", UWHotkeyScheme.Modern, UWHotkeyModRule.NoShiftAlt,
				ModernOpen, Shut | ModernShut | UWHotkeyGate.MapVisible, true, true),

			// 4 The classic scheme's map key - not in a conversation (using the map is locked there).
			fGroup(UWHotkeySite.GameUi, "classic map", UWHotkeyScheme.Classic, UWHotkeyModRule.NoCtrl,
				UWHotkeyGate.None, Shut | UWHotkeyGate.Conversation, true, false),

			// 5 The original's Ctrl shortcuts: normal play only.
			fGroup(UWHotkeySite.GameUi, "classic Ctrl", UWHotkeyScheme.Classic, UWHotkeyModRule.Ctrl, UWHotkeyGate.Interaction,
				Shut | UWHotkeyGate.Conversation | UWHotkeyGate.MapVisible | UWHotkeyGate.OptionsOpen | UWHotkeyGate.Roaming, true, false),

			// 6 The original's function keys while the view roams: only F7 turns the panel.
			fGroup(UWHotkeySite.GameUi, "classic, roaming", UWHotkeyScheme.Classic, UWHotkeyModRule.Plain,
				UWHotkeyGate.Interaction | UWHotkeyGate.Roaming, Shut | UWHotkeyGate.Conversation | UWHotkeyGate.MapVisible, true, true),

			// 7 The original's function keys.
			fGroup(UWHotkeySite.GameUi, "classic", UWHotkeyScheme.Classic, UWHotkeyModRule.Plain, UWHotkeyGate.Interaction,
				Shut | UWHotkeyGate.Conversation | UWHotkeyGate.MapVisible | UWHotkeyGate.Roaming, true, false),

			// 8 UWControlScheme: Shift+F2.
			fGroup(UWHotkeySite.Scheme, "scheme", UWHotkeyScheme.Both, UWHotkeyModRule.Any, UWHotkeyGate.None, UWHotkeyGate.None, false, false),

			// 9 UWHelpWindow: Tab, not where the modern panel holds the help.
			fGroup(UWHotkeySite.Help, "help", UWHotkeyScheme.Both, UWHotkeyModRule.NoShiftAlt, UWHotkeyGate.None,
				UWHotkeyGate.HelpKeys | UWHotkeyGate.PanelHoldsHelp | UWHotkeyGate.KeysLocked | UWHotkeyGate.SetupPanel | UWHotkeyGate.HelpCovered,
				false, false),

			// 10 UWModernPanel: Tab, its Help tab.
			fGroup(UWHotkeySite.ModernPanel, "panel help", UWHotkeyScheme.Modern, UWHotkeyModRule.NoShiftAlt, UWHotkeyGate.None,
				UWHotkeyGate.MenuOpen | UWHotkeyGate.KeysLocked | UWHotkeyGate.HelpKeys | UWHotkeyGate.MapVisible | UWHotkeyGate.SetupPanel,
				false, false),

			// 11 DebugFunctions: P, N.
			fGroup(UWHotkeySite.DebugKeys, "debug", UWHotkeyScheme.Both, UWHotkeyModRule.Any, UWHotkeyGate.None, UWHotkeyGate.None, false, false),

			// 12 The level keys, in noclip only (the player does not move along).
			fGroup(UWHotkeySite.DebugKeys, "debug levels", UWHotkeyScheme.Both, UWHotkeyModRule.Any, UWHotkeyGate.Noclip, UWHotkeyGate.None, true, false),

			// 13 F11, Shift+F12, Shift+F11.
			fGroup(UWHotkeySite.DebugKeys, "debug more", UWHotkeyScheme.Both, UWHotkeyModRule.Any, UWHotkeyGate.None, UWHotkeyGate.None, false, false),

			// 14 UWDebugOverlay: Shift+F1.
			fGroup(UWHotkeySite.DebugOverlay, "overlay", UWHotkeyScheme.Both, UWHotkeyModRule.Any, UWHotkeyGate.None, UWHotkeyGate.None, false, false),

			// 15 UWDebugTools: Shift+F7, Shift+F10 - silent while typing elsewhere; they still close their own tab.
			fGroup(UWHotkeySite.DebugTools, "debug tools", UWHotkeyScheme.Both, UWHotkeyModRule.Shift, UWHotkeyGate.None,
				UWHotkeyGate.OtherTyping, false, false)
		};

		/// <summary>The rows, by group, in each group's order.</summary>
		public static readonly UWHotkeyRow[] Rows =
		{
			fRow(0, "F", UWAct.ToggleFont, UWHotkeyTrigger.Pressed, UWHotkeyModRule.NoCtrl, UWHotkeyHandler.ToggleFont),

			fRow(1, "M, View tapped", UWAct.ToggleMap, UWHotkeyTrigger.Performed, UWHotkeyModRule.NoCtrl, UWHotkeyHandler.HideMap),
			fBack(1, "Escape, Start, pad B", UWBackOwner.ModernMap, UWHotkeyModRule.NoCtrl, UWHotkeyHandler.HideMap),

			fRow(2, "Right Ctrl+S", UWAct.OptSave, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ModernMenuSave),
			fRow(2, "Right Ctrl+R", UWAct.OptRestore, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ModernMenuLoad),
			fRow(2, "Right Ctrl+M", UWAct.OptMusic, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ModernMenuMain),
			fRow(2, "Right Ctrl+F", UWAct.OptSound, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ModernMenuMain),
			fRow(2, "Right Ctrl+D", UWAct.OptDetail, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ModernMenuMain),
			fRow(2, "Right Ctrl+Q", UWAct.OptQuit, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ModernMenuMain),

			fBack(3, "Escape, Start, pad B", UWBackOwner.ModernChain, UWHotkeyModRule.Any, UWHotkeyHandler.CloseChain),
			fRow(3, "F1", UWAct.KeyOptions, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ToggleModernMenu),
			fBack(3, "Escape, Start, pad B", UWBackOwner.ModernMenu, UWHotkeyModRule.Any, UWHotkeyHandler.ToggleModernMenu),
			fRow(3, "(menu open)", UWAct.Menu, UWHotkeyTrigger.Always, UWHotkeyModRule.Any, UWHotkeyHandler.None, UWHotkeyGate.MenuOpen),
			fRow(3, "F9", UWAct.KeyTrack, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.Track),
			fRow(3, "F10", UWAct.KeyCamp, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.Camp, UWHotkeyGate.CanCamp),
			fRow(3, "M, View tapped", UWAct.ToggleMap, UWHotkeyTrigger.Performed, UWHotkeyModRule.Any, UWHotkeyHandler.ToggleMap,
				UWHotkeyGate.None, UWHotkeyGate.PadPointerTakes),

			// The map up: closed by M, the pad's pointer taking the press or not; else opened by M.
			fRow(4, "M, View tapped", UWAct.ToggleMap, UWHotkeyTrigger.Performed, UWHotkeyModRule.Any, UWHotkeyHandler.ToggleMap,
				UWHotkeyGate.MapVisible),
			fRow(4, "M, View tapped", UWAct.ToggleMap, UWHotkeyTrigger.Performed, UWHotkeyModRule.Any, UWHotkeyHandler.ToggleMap,
				UWHotkeyGate.None, UWHotkeyGate.MapVisible | UWHotkeyGate.PadPointerTakes),

			fRow(5, "Ctrl+S", UWAct.OptSave, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ShortcutSave),
			fRow(5, "Ctrl+R", UWAct.OptRestore, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ShortcutRestore),
			fRow(5, "Ctrl+M", UWAct.OptMusic, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ShortcutMusic),
			fRow(5, "Ctrl+F", UWAct.OptSound, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ShortcutSound),
			fRow(5, "Ctrl+D", UWAct.OptDetail, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ShortcutDetail),
			fRow(5, "Ctrl+Q", UWAct.OptQuit, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ShortcutQuit),

			fRow(6, "F7", UWAct.KeyPanel, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.TurnPanel),

			fRow(7, "F1", UWAct.KeyOptions, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.OpenOptions),
			fRow(7, "(options up)", UWAct.KeyOptions, UWHotkeyTrigger.Always, UWHotkeyModRule.Any, UWHotkeyHandler.None, UWHotkeyGate.OptionsOpen),
			fRow(7, "F2", UWAct.KeyTalk, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ModeTalk),
			fRow(7, "F3", UWAct.KeyGet, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ModeGet),
			fRow(7, "F4", UWAct.KeyLook, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ModeLook),
			fRow(7, "F5", UWAct.KeyFight, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ModeFight),
			fRow(7, "F6", UWAct.KeyUse, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ModeUse),
			fRow(7, "F7", UWAct.KeyPanel, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.TurnPanel),
			fRow(7, "F8", UWAct.KeyCast, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.CastShelf),
			fRow(7, "F9", UWAct.KeyTrack, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.Track),
			fRow(7, "F10", UWAct.KeyCamp, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.Camp, UWHotkeyGate.CanCamp),

			fRow(8, "Shift+F2, pad View", UWAct.ToggleScheme, UWHotkeyTrigger.Pressed, UWHotkeyModRule.ShiftOnKeyboard, UWHotkeyHandler.SwitchScheme),

			fRow(9, "Tab, View held", UWAct.ToggleHelp, UWHotkeyTrigger.Performed, UWHotkeyModRule.Any, UWHotkeyHandler.ToggleHelp),

			fRow(10, "Tab, View held", UWAct.ToggleHelp, UWHotkeyTrigger.Performed, UWHotkeyModRule.Any, UWHotkeyHandler.TogglePanelHelp),

			fRow(11, "P", UWAct.TeleportDebug, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.Teleport),
			fRow(11, "N", UWAct.ToggleSpectator, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ToggleNoclip,
				UWHotkeyGate.None, UWHotkeyGate.ScreenMenu),

			fRow(12, "Page Up", UWAct.NextLevel, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.NextLevel),
			fRow(12, "Page Down", UWAct.PrevLevel, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.PreviousLevel),

			// F11 without Shift: Shift+F11 is the forced cast alone (wave 2b).
			fRow(13, "F11", UWAct.ToggleBrightness, UWHotkeyTrigger.Pressed, UWHotkeyModRule.NoShift, UWHotkeyHandler.ToggleBrightness,
				UWHotkeyGate.Lighting, UWHotkeyGate.ScreenMenu),
			fRow(13, "Shift+F12", UWAct.DumpShaderGlobals, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.DumpShaderGlobals),
			fRow(13, "Shift+F11", UWAct.ForceCast, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ForceCast,
				UWHotkeyGate.None, UWHotkeyGate.ScreenMenu),

			fRow(14, "Shift+F1", UWAct.ToggleOverlay, UWHotkeyTrigger.Pressed, UWHotkeyModRule.Any, UWHotkeyHandler.ToggleOverlay),

			// The keys themselves, not an act (the act names the debug map they would lie in).
			fRow(15, "Shift+F7", UWAct.ToggleOverlay, UWHotkeyTrigger.RawF7, UWHotkeyModRule.Any, UWHotkeyHandler.ToggleDebugJump),
			fRow(15, "Shift+F10", UWAct.ToggleOverlay, UWHotkeyTrigger.RawF10, UWHotkeyModRule.Any, UWHotkeyHandler.ToggleDebugSearch)
		};

		/// <summary>UWGameUI's routing is shut: typing, a screen menu, a sequence (the old early return).</summary>
		private const UWHotkeyGate Shut = UWHotkeyGate.Typing | UWHotkeyGate.ScreenMenu | UWHotkeyGate.Cutscene;

		/// <summary>The modern routing needs the HUD and the interaction ...</summary>
		private const UWHotkeyGate ModernOpen = UWHotkeyGate.ModernHud | UWHotkeyGate.Interaction;

		/// <summary>... and is shut in a conversation, while the view roams, and in the frame Escape ended
		/// the roaming view.</summary>
		private const UWHotkeyGate ModernShut = UWHotkeyGate.Conversation | UWHotkeyGate.Roaming | UWHotkeyGate.RoamingEndedByEscape;

		/// <summary>A group. THE WAIT FOR A KEY HOLDS EVERY HOTKEY (cleanup 7, wave 2b, per user 2026-10-25:
		/// one press, one taker): while the Controls window waits for a key or a button, and in the frame
		/// the wait ended, no group is entered - the key that became a binding fired its old hotkey as
		/// well (F1 closed the modern game menu with the window).</summary>
		private static UWHotkeyGroup fGroup(UWHotkeySite peSite, string psName, UWHotkeyScheme peScheme, UWHotkeyModRule peMods,
			UWHotkeyGate peRequires, UWHotkeyGate peRefuses, bool pbFirstMatch, bool pbStops)
		{
			return new UWHotkeyGroup
			{
				Site = peSite, Name = psName, Scheme = peScheme, Mods = peMods, Requires = peRequires,
				Refuses = peRefuses | UWHotkeyGate.Rebinding, FirstMatch = pbFirstMatch, StopsWhenEntered = pbStops
			};
		}

		/// <summary>A row of the back press, given to its owner by the Escape chain.</summary>
		private static UWHotkeyRow fBack(int piGroup, string psKeys, UWBackOwner peOwner, UWHotkeyModRule peMods, UWHotkeyHandler peHandler)
		{
			UWHotkeyRow lORow = fRow(piGroup, psKeys, UWAct.Menu, UWHotkeyTrigger.Back, peMods, peHandler);

			lORow.BackOwner = peOwner;

			return lORow;
		}

		private static UWHotkeyRow fRow(int piGroup, string psKeys, UWAct peAct, UWHotkeyTrigger peTrigger, UWHotkeyModRule peMods,
			UWHotkeyHandler peHandler, UWHotkeyGate peRequires = UWHotkeyGate.None, UWHotkeyGate peRefuses = UWHotkeyGate.None)
		{
			return new UWHotkeyRow
			{
				Group = piGroup, Keys = psKeys, Act = peAct, Trigger = peTrigger, Mods = peMods, Requires = peRequires,
				Refuses = peRefuses, Handler = peHandler
			};
		}

		/// <summary>
		/// Routes one site's hotkeys for this frame: each group of the site in order - its scheme, its
		/// modifiers and its gates asked now -, its rows in order (the trigger first, then the row's own
		/// modifiers and gates), the handler run; a handler that declines lets the next row try.
		/// </summary>
		public static void Route(UWHotkeySite peSite, IUWHotkeyHost pOHost)
		{
			for (int liGroup = 0; liGroup < Groups.Length; liGroup++)
			{
				UWHotkeyGroup lOGroup = Groups[liGroup];

				if (lOGroup.Site != peSite || !fSchemeHolds(lOGroup.Scheme, pOHost.IsModern)
					|| !fModsHold(lOGroup.Mods, pOHost, null) || !fGatesHold(lOGroup.Requires, lOGroup.Refuses, pOHost, null))
					continue;

				for (int liRow = 0; liRow < Rows.Length; liRow++)
				{
					UWHotkeyRow lORow = Rows[liRow];

					if (lORow.Group != liGroup || !pOHost.Fired(lORow) || !fModsHold(lORow.Mods, pOHost, lORow)
						|| !fGatesHold(lORow.Requires, lORow.Refuses, pOHost, lORow) || !pOHost.Run(lORow.Handler))
						continue;

					if (lOGroup.FirstMatch)
						break;
				}

				if (lOGroup.StopsWhenEntered)
					return;
			}
		}

		private static bool fSchemeHolds(UWHotkeyScheme peScheme, bool pbModern)
		{
			return peScheme == UWHotkeyScheme.Both || (peScheme == UWHotkeyScheme.Modern) == pbModern;
		}

		private static bool fModsHold(UWHotkeyModRule peRule, IUWHotkeyHost pOHost, UWHotkeyRow pORow)
		{
			UWHotkeyMods leMods = pOHost.Mods;
			bool lbShift = (leMods & UWHotkeyMods.Shift) != 0;
			bool lbAlt = (leMods & UWHotkeyMods.Alt) != 0;
			bool lbCtrl = (leMods & UWHotkeyMods.Ctrl) != 0;

			switch (peRule)
			{
				case UWHotkeyModRule.NoCtrl: return !lbCtrl;
				case UWHotkeyModRule.NoShiftAlt: return !lbShift && !lbAlt;
				case UWHotkeyModRule.Ctrl: return lbCtrl && !lbShift && !lbAlt;
				case UWHotkeyModRule.RightCtrl: return (leMods & UWHotkeyMods.RightCtrl) != 0 && !lbShift && !lbAlt;
				case UWHotkeyModRule.Plain: return !lbShift && !lbAlt && !lbCtrl;
				case UWHotkeyModRule.Shift: return lbShift;
				case UWHotkeyModRule.ShiftOnKeyboard: return pORow == null || lbShift || !pOHost.FromKeyboard(pORow.Act);
				case UWHotkeyModRule.NoShift: return !lbShift;
				default: return true;
			}
		}

		/// <summary>Every gate of peRequires holds and none of peRefuses, asked one by one in the order of
		/// their bits (stopping at the first that fails).</summary>
		private static bool fGatesHold(UWHotkeyGate peRequires, UWHotkeyGate peRefuses, IUWHotkeyHost pOHost, UWHotkeyRow pORow)
		{
			for (int liBit = 0; liBit < 63; liBit++)
			{
				UWHotkeyGate leGate = (UWHotkeyGate)(1L << liBit);

				if ((peRequires & leGate) != 0 && !pOHost.Gate(leGate, pORow))
					return false;

				if ((peRefuses & leGate) != 0 && pOHost.Gate(leGate, pORow))
					return false;
			}

			return true;
		}

		/// <summary>The table as text, one line per row under its group: the record of what each hotkey
		/// does where (the self-check pins it).</summary>
		public static string Describe()
		{
			StringBuilder lOText = new StringBuilder();

			for (int liGroup = 0; liGroup < Groups.Length; liGroup++)
			{
				UWHotkeyGroup lOGroup = Groups[liGroup];

				lOText.Append("[").Append(liGroup).Append("] ").Append(lOGroup.Site).Append(" / ").Append(lOGroup.Name)
					.Append(" | ").Append(lOGroup.Scheme).Append(" | mods ").Append(lOGroup.Mods)
					.Append(" | needs ").Append(lOGroup.Requires).Append(" | not ").Append(lOGroup.Refuses)
					.Append(lOGroup.FirstMatch ? " | first match" : " | each")
					.Append(lOGroup.StopsWhenEntered ? " | stops" : "").Append('\n');

				foreach (UWHotkeyRow lORow in Rows)
				{
					if (lORow.Group != liGroup)
						continue;

					bool lbByAct = lORow.Trigger == UWHotkeyTrigger.Pressed || lORow.Trigger == UWHotkeyTrigger.Performed;

					lOText.Append("    ").Append(lORow.Keys).Append(" = ").Append(lORow.Trigger);

					if (lbByAct)
						lOText.Append(' ').Append(UWActionTable.MapOf(lORow.Act)).Append('|').Append(UWActionTable.NameOf(lORow.Act));
					else if (lORow.Trigger == UWHotkeyTrigger.Back)
						lOText.Append(" to ").Append(lORow.BackOwner);

					lOText.Append(" | mods ").Append(lORow.Mods).Append(" | needs ").Append(lORow.Requires).Append(" | not ").Append(lORow.Refuses)
						.Append(lbByAct && UWInputContexts.SilencedWhileTyping(lORow.Act) ? " | silent while typing" : "")
						.Append(" -> ").Append(lORow.Handler).Append('\n');
				}
			}

			return lOText.ToString();
		}

		/// <summary>
		/// THE SHORTCUT MODIFIER (cleanup 7, wave 2b, per user 2026-10-25: the right Ctrl+R in the modern
		/// scheme opened the restore page AND readied the weapon - ModernReady read R without asking for
		/// a modifier): while the modifier of the scheme's Ctrl shortcuts is held - the group on Ctrl or on
		/// the right Ctrl, of the classic or the modern scheme -, the keys of those shortcuts' acts do
		/// nothing else. ONE RULE FOR EVERY READER: the Unity side switches the player's actions that
		/// share a key with them off while it is held (UWControls.SetShortcutModifierHeld, beside the
		/// typing lock), so no site checks a modifier for itself. The modern left Ctrl is not the
		/// modifier: it still sinks while flying, with S and D walking.
		/// </summary>
		public static UWHotkeyMods ShortcutModifier(bool pbModern)
		{
			UWHotkeyGroup lOGroup = fShortcutGroup(pbModern);

			if (lOGroup == null)
				return UWHotkeyMods.None;

			return lOGroup.Mods == UWHotkeyModRule.RightCtrl ? UWHotkeyMods.RightCtrl : UWHotkeyMods.Ctrl;
		}

		/// <summary>Whether the scheme's shortcut modifier is held among peMods (the keys alone, whatever
		/// else is held: a shortcut with Shift does nothing, its letter does nothing either).</summary>
		public static bool IsShortcutModifierHeld(bool pbModern, UWHotkeyMods peMods)
		{
			UWHotkeyMods leModifier = ShortcutModifier(pbModern);

			return leModifier != UWHotkeyMods.None && (peMods & leModifier) == leModifier;
		}

		/// <summary>The acts of the scheme's Ctrl shortcuts (S, R, M, F, D, Q by default): the rows of its
		/// shortcut group.</summary>
		public static List<UWAct> ShortcutActs(bool pbModern)
		{
			List<UWAct> lOActs = new List<UWAct>();
			int liGroup = System.Array.IndexOf(Groups, fShortcutGroup(pbModern));

			foreach (UWHotkeyRow lORow in Rows)
			{
				if (lORow.Group == liGroup && lORow.Trigger == UWHotkeyTrigger.Pressed && !lOActs.Contains(lORow.Act))
					lOActs.Add(lORow.Act);
			}

			return lOActs;
		}

		/// <summary>The scheme's group of the Ctrl shortcuts: UWGameUI's group on Ctrl (classic) or on the
		/// right Ctrl (modern).</summary>
		private static UWHotkeyGroup fShortcutGroup(bool pbModern)
		{
			foreach (UWHotkeyGroup lOGroup in Groups)
			{
				if (lOGroup.Site == UWHotkeySite.GameUi && fSchemeHolds(lOGroup.Scheme, pbModern)
					&& lOGroup.Mods == (pbModern ? UWHotkeyModRule.RightCtrl : UWHotkeyModRule.Ctrl))
					return lOGroup;
			}

			return null;
		}

		/// <summary>The rule as text, both schemes: the record the self-check holds.</summary>
		public static string DescribeShortcutModifier()
		{
			StringBuilder lOText = new StringBuilder();

			foreach (bool lbModern in new[] { false, true })
			{
				lOText.Append(lbModern ? "modern" : "classic").Append(": ").Append(ShortcutModifier(lbModern)).Append(" silences the other acts on the keys of ")
					.Append(string.Join(", ", ShortcutActs(lbModern))).Append('\n');
			}

			return lOText.ToString();
		}

		/// <summary>The rows of a site, in routing order (for the host's and the self-check's lists).</summary>
		public static List<UWHotkeyRow> RowsOf(UWHotkeySite peSite)
		{
			List<UWHotkeyRow> lORows = new List<UWHotkeyRow>();

			foreach (UWHotkeyRow lORow in Rows)
			{
				if (Groups[lORow.Group].Site == peSite)
					lORows.Add(lORow);
			}

			return lORows;
		}
	}
}
