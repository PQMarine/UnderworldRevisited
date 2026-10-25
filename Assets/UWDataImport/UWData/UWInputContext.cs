namespace UWDataImport.UWData
{
	/// <summary>
	/// WHO HAS THE INPUT NOW (cleanup step 7.4, 2026-10-25; section 8.5 of the cleanup plan): the input
	/// contexts, one bit each, as a set - the world always at the bottom, and over it what the game is
	/// in. What each context does to the input is UWInputContexts' (one table instead of the
	/// hand-written lock list and the blocking tests in UWControlScheme). The set the code really
	/// needs, adjusted from the plan's World, UiModal, TextEntry, PadPointer, Cutscene:
	///
	///   - World: the game in play; the bottom of every set.
	///   - Conversation: a conversation runs - it halts the world but not the inventory, which the
	///     trade needs (UWControlScheme.IsConversationOpen). Split from UiModal for that difference.
	///   - UiModal: a modal hold of the interface (a menu, a window picture, a cutscene's hold), the
	///     detached view (the remote camera) or an input hold (asleep) - world and inventory stop.
	///   - TextEntry: text is typed somewhere, or something holds the keys as typing does (the
	///     typing lock: UWTypingSource lists who) - the keyboard's actions fall silent.
	///   - ScreenMenu: the main menu or the character creation (UWScreenUi.IsScreenMenuOpen) - the
	///     game's keys stay silent.
	///   - Cutscene: a sequence plays, or one ended in this very frame (UWIntroPlayer.HoldsKeys).
	///   - PadPointer (cleanup step 7.8, 2026-10-25): the pad's pointer has its buttons - the pad's
	///     presses bound to its click and right button (PadClick, PadContext) are its own, the actions
	///     they also trigger leave them alone (PadPointerTakes; UWGamepadPointer.Takes until then).
	///     Beside the stack rather than in it: it says who has the pad's pointer buttons, not who is on
	///     top, so it is not in Order.
	/// </summary>
	[System.Flags]
	public enum UWInputContext
	{
		None = 0,
		World = 1,
		Conversation = 2,
		UiModal = 4,
		TextEntry = 8,
		ScreenMenu = 16,
		Cutscene = 32,
		PadPointer = 64
	}

	/// <summary>
	/// WHO TYPES OR HOLDS THE KEYS AS TYPING DOES (cleanup step 7.4; the sources of UWTextEntryState,
	/// cleanup 5.18, moved here as flags): the Unity side sets one per place that is typing now, the
	/// three answers come from UWInputContexts.
	/// </summary>
	[System.Flags]
	public enum UWTypingSource
	{
		None = 0,

		/// <summary>A note on the map (UWHudMap.IsWritingNote).</summary>
		MapNote = 1,

		/// <summary>A save game's name in the classic options (UWHudOptions.miSaveSlotTyping).</summary>
		ClassicSaveName = 2,

		/// <summary>A save game's name in the modern game menu (UWModernGameMenu.IsTypingName).</summary>
		ModernSaveName = 4,

		/// <summary>A conversation's typed answer (UWConversationController.IsTypingAnswer).</summary>
		ConversationAnswer = 8,

		/// <summary>The character's name (UWCharacterCreationScreen.IsTypingName).</summary>
		CharacterName = 16,

		/// <summary>A text field of the windows on the kit (UWKitRows.IsTyping): the folder path, the
		/// debug panel's fields.</summary>
		KitField = 32,

		/// <summary>The instrument is played (UWInstrumentPlayer.IsPlaying).</summary>
		Instrument = 64,

		/// <summary>The help has the keyboard: its notes typed or the manual read
		/// (UWHelpWindow.BlocksGameKeys).</summary>
		HelpKeys = 128,

		/// <summary>The modern bags' split box (UWModernBags.IsSplitting).</summary>
		BagSplit = 256,

		/// <summary>The modern bags' menu (UWModernBags.IsMenuOpen).</summary>
		BagMenu = 512,

		/// <summary>A mantra is typed (Interaction.IsChanting).</summary>
		Mantra = 1024,

		/// <summary>The message scroll's running line is up - the input line, the count prompt, the
		/// yes/no question (Interaction.IsPromptActive).</summary>
		Prompt = 2048,

		/// <summary>The running line shows a text cursor and is not the count prompt (with Prompt:
		/// Interaction.ShowsPromptTextCursor, not UWItemDrag.IsAskingCount).</summary>
		PromptTyped = 4096,

		/// <summary>The debug tools' tab is open (UWDebugTools.IsTyping: its jump line and spell
		/// search).</summary>
		DebugTools = 8192
	}

	/// <summary>
	/// What each input context does (cleanup step 7.4): which actions fall silent, what blocks the
	/// world and the inventory, which context lies on top, and the typing lock's three answers - the
	/// rules exactly as UWControls, UWTextEntryState and UWControlScheme wrote them before.
	/// </summary>
	public static class UWInputContexts
	{
		/// <summary>The contexts from the top down: the first one a set holds is its top.</summary>
		public static readonly UWInputContext[] Order =
		{
			UWInputContext.Cutscene, UWInputContext.ScreenMenu, UWInputContext.TextEntry, UWInputContext.UiModal,
			UWInputContext.Conversation, UWInputContext.World
		};

		/// <summary>The context on top of a set (World for an empty one).</summary>
		public static UWInputContext Top(UWInputContext peSet)
		{
			foreach (UWInputContext leContext in Order)
			{
				if ((peSet & leContext) != 0)
					return leContext;
			}

			return UWInputContext.World;
		}

		/// <summary>The world's input pauses - look, movement, use (UWControlScheme.IsWorldInputBlocked):
		/// a modal hold or a conversation.</summary>
		public static bool BlocksWorld(UWInputContext peSet)
		{
			return (peSet & (UWInputContext.UiModal | UWInputContext.Conversation)) != 0;
		}

		/// <summary>The inventory's input pauses (UWControlScheme.IsInventoryInputBlocked): like the
		/// world, but not during a conversation, which trades.</summary>
		public static bool BlocksInventory(UWInputContext peSet)
		{
			return (peSet & UWInputContext.UiModal) != 0;
		}

		/// <summary>
		/// THE TYPING LOCK'S ACTIONS of the Player and Noclip maps (the hand-written list of
		/// UWTextEntryState since cleanup 5.18, moved verbatim): everything tied to the keyboard (per
		/// user, 2026-09-13: "during text input while chanting a mantra, switch off the special keys. B
		/// N F and whatever else comes along"); WalkSlow (Left Shift) and EasyModifier (Left Alt) since
		/// issue 4 (2026-10-21). Mouse, clicks and the gamepad's own actions remain, and Tab (ToggleHelp),
		/// whose two readers (UWHelpWindow, UWModernPanel) ask the lock themselves. The whole Debug map
		/// falls silent besides (SilencedWhileTyping).
		/// </summary>
		public static readonly UWAct[] TypingSilenced =
		{
			UWAct.Move, UWAct.ToggleScheme, UWAct.ToggleInventory, UWAct.HoverHeight, UWAct.Turn, UWAct.Strafe, UWAct.Jump,
			UWAct.LookDown, UWAct.LookReset, UWAct.LookUp, UWAct.ToggleMap, UWAct.ToggleFont, UWAct.KeyOptions, UWAct.KeyTalk,
			UWAct.KeyGet, UWAct.KeyLook, UWAct.KeyFight, UWAct.KeyUse, UWAct.KeyPanel, UWAct.KeyCast, UWAct.KeyTrack,
			UWAct.KeyCamp, UWAct.OptSave, UWAct.OptRestore, UWAct.OptMusic, UWAct.OptSound, UWAct.OptDetail, UWAct.OptQuit,
			UWAct.EasyForward, UWAct.EasyWalkForward, UWAct.EasyBack, UWAct.EasyTurnLeft, UWAct.EasyTurnRight,
			UWAct.ModernUse, UWAct.ModernReady, UWAct.WalkSlow, UWAct.EasyModifier, UWAct.ModernHover, UWAct.Menu,
			UWAct.ModernBags, UWAct.ModernLook, UWAct.ModernRunes, UWAct.ModernSlot1, UWAct.ModernSlot2, UWAct.ModernSlot3,
			UWAct.ModernSlot4, UWAct.ModernSlot5, UWAct.ModernSlot6, UWAct.ModernSlot7, UWAct.ModernSlot8,
			UWAct.ModernSlot9, UWAct.ModernSlot10,
			UWAct.NoclipMove, UWAct.NoclipUp, UWAct.NoclipDown, UWAct.NoclipSprint
		};

		/// <summary>Whether an action falls silent while the TextEntry context holds: the list above,
		/// and the whole Debug map.</summary>
		public static bool SilencedWhileTyping(UWAct peAct)
		{
			if (peAct >= UWActionTable.FirstDebug)
				return true;

			return System.Array.IndexOf(TypingSilenced, peAct) >= 0;
		}

		/// <summary>Whether an action is silent in a set of contexts.</summary>
		public static bool Silences(UWInputContext peSet, UWAct peAct)
		{
			return (peSet & UWInputContext.TextEntry) != 0 && SilencedWhileTyping(peAct);
		}

		/// <summary>The sources that are text entry in the wide sense: everything but the debug tools
		/// (and the prompt's text cursor, which comes with Prompt).</summary>
		public const UWTypingSource TextEntrySources = UWTypingSource.MapNote | UWTypingSource.ClassicSaveName
			| UWTypingSource.ModernSaveName | UWTypingSource.ConversationAnswer | UWTypingSource.CharacterName
			| UWTypingSource.KitField | UWTypingSource.Instrument | UWTypingSource.HelpKeys | UWTypingSource.BagSplit
			| UWTypingSource.BagMenu | UWTypingSource.Mantra | UWTypingSource.Prompt;

		/// <summary>The sources that type text in the narrow sense, for the gamepad's letter grid.</summary>
		public const UWTypingSource TypedTextSources = UWTypingSource.MapNote | UWTypingSource.ClassicSaveName
			| UWTypingSource.ModernSaveName | UWTypingSource.ConversationAnswer | UWTypingSource.Mantra
			| UWTypingSource.PromptTyped;

		/// <summary>Typing anywhere - map note, save game name, answer, name, mantra, help notes, an input
		/// line - or something holding the keys as typing does (the help, the instrument, the split box,
		/// the bags' menu): letter keys belong to the text (UWTextEntryState.IsTextEntryActive).</summary>
		public static bool IsTextEntry(UWTypingSource peSources)
		{
			return (peSources & TextEntrySources) != 0;
		}

		/// <summary>The lock in force for every UWControls (UWTextEntryState.LocksKeys): text entry, or
		/// the debug tools' tab open. The TextEntry context.</summary>
		public static bool LocksKeys(UWTypingSource peSources)
		{
			return IsTextEntry(peSources) || (peSources & UWTypingSource.DebugTools) != 0;
		}

		/// <summary>Text typed in the narrow sense - the letter grid comes up for these
		/// (UWTextEntryState.IsTypingText).</summary>
		public static bool IsTypingText(UWTypingSource peSources)
		{
			return (peSources & TypedTextSources) != 0;
		}

		/// <summary>The context the typing sources put on the stack.</summary>
		public static UWInputContext FromTyping(UWTypingSource peSources)
		{
			return LocksKeys(peSources) ? UWInputContext.TextEntry : UWInputContext.None;
		}

		/// <summary>
		/// THE PAD'S POINTER TAKES A PRESS (cleanup step 7.8; UWGamepadPointer.Takes until then): in the
		/// PadPointer context a gamepad press (pbFromPad) of a control bound to the pointer's click or
		/// right button (pbPointerButton: PadClick, PadContext - A, RT, X, LT by default) is the
		/// pointer's; the action it triggers as well (the modern use, look, strike, cast...) leaves it.
		/// </summary>
		public static bool PadPointerTakes(UWInputContext peSet, bool pbFromPad, bool pbPointerButton)
		{
			return (peSet & UWInputContext.PadPointer) != 0 && pbFromPad && pbPointerButton;
		}
	}
}
