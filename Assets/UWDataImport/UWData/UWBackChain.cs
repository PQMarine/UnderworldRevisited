using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>The inputs of a back press.</summary>
	[System.Flags]
	public enum UWBackKey
	{
		None = 0,

		/// <summary>The Escape key itself (not rebindable; the text entries, the windows, the cutscene).</summary>
		Escape = 1,

		/// <summary>The Menu act - Escape and the pad's Start by default, silent while typing (the
		/// modern scheme's map, chain and game menu).</summary>
		Menu = 2,

		/// <summary>The PadBack act - the pad's B by default.</summary>
		PadBack = 4,

		/// <summary>The pad's B read by its place: the letter grid, whose keys are the pad's by place
		/// (its hint line names them so).</summary>
		PadEast = 8,

		/// <summary>The PadBack binding's button read on the pad itself (cleanup step 7.6: the screens
		/// before the game, the prompts and the split box read the pad's B by its place until then) - a
		/// rebind reaches it, and it works where the player's actions are off or not there.</summary>
		PadButton = 16
	}

	/// <summary>
	/// EVERY CONSUMER OF ESCAPE AND THE PAD'S B (cleanup step 7.5, 2026-10-25), top first: the order in
	/// which they win. Each asks UWBack.Takes (the Unity side) where it read the key itself before.
	/// </summary>
	public enum UWBackOwner
	{
		/// <summary>The Controls window waits for a key or a button (UWKeyBindings.Listen, ListenGamepad):
		/// Escape cancels the wait, the pad's buttons wait with it or become the binding - every back key
		/// is the wait's, also in the frame it ended (cleanup 7, wave 2b). Nobody asks: the wait reads its
		/// keys itself (InputSystem.onAnyButtonPress); the row keeps the press from everything below.</summary>
		Rebind,

		/// <summary>The debug panel's tools tab: Escape closes it (UWDebugTools). The panel is drawn over
		/// everything (UWUiSorting.DebugPanel), so it comes first since wave 2b.</summary>
		DebugTools,

		/// <summary>A sequence plays: Escape ends it (UWIntroPlayer).</summary>
		Cutscene,

		/// <summary>The modern scheme's roaming view: Escape ends it and its spell (UWRoamingSight).</summary>
		RoamingSight,

		/// <summary>The setup bar's Remastered effects window: Escape closes it (UWSetupMenu), the pad's B and
		/// Start too since wave 2b (they closed the game menu under it). Since cleanup 8d.3 the one effects
		/// window - the classic options' Very High opens it too (their own screen and its row,
		/// OptionsEffects, went).</summary>
		SetupEffects,

		/// <summary>The setup bar's keyboard navigation: Escape leaves it (UWSetupMenu).</summary>
		SetupKeyNav,

		/// <summary>A kit text field or the help's notes: Escape lets go of it (UWKitRows).</summary>
		KitText,

		/// <summary>The help's reading mode: Escape leaves it (UWHelpManualTab).</summary>
		HelpReading,

		/// <summary>The pad's letter grid: its B deletes (UWLetterGrid).</summary>
		LetterGrid,

		/// <summary>The character's name: Escape leaves the creation (UWCharacterCreationScreen).</summary>
		CharacterName,

		/// <summary>The creation's other stages: Escape or B leave it (UWCharacterCreationScreen; B as
		/// Escape there, per user 2026-10-08).</summary>
		CharacterCreation,

		/// <summary>The main menu's list of save games: Escape or B go back to its buttons (UWMainMenu;
		/// B as Escape there, per user 2026-10-08).</summary>
		MainMenuSaves,

		/// <summary>A note on the map: Escape keeps it and closes the map (UWHudMap).</summary>
		MapNote,

		/// <summary>A save game's name in the classic options: Escape gives up the save (UWHudOptions).</summary>
		ClassicSaveName,

		/// <summary>The classic options panel: Escape closes it (UWHudOptions).</summary>
		ClassicOptions,

		/// <summary>The instrument: Escape puts it down (UWInstrumentPlayer).</summary>
		Instrument,

		/// <summary>A conversation's typed answer: Escape answers nothing (UWConversationController).</summary>
		ConversationAnswer,

		/// <summary>The classic "how many" prompt: Escape or B cancel it (UWCountPrompt).</summary>
		CountPrompt,

		/// <summary>A mantra: Escape cancels it (UWPromptController).</summary>
		Mantra,

		/// <summary>The classic yes/no question: Escape or B answer no (UWPromptController).</summary>
		YesNo,

		/// <summary>The modern yes/no box: Escape answers no (UWModernQuestion).</summary>
		ModernQuestion,

		/// <summary>The modern bags' menu: Escape or B close it (UWModernBagMenu).</summary>
		BagMenu,

		/// <summary>The modern bags' split box: Escape or B let it go (UWModernBagSplit).</summary>
		BagSplit,

		/// <summary>The modern game menu's save name: Escape goes back to the slots (UWModernGameMenu).</summary>
		ModernSaveName,

		/// <summary>The layout editor's save question: Escape goes back to editing (UWModernLayoutEditor).</summary>
		LayoutAsk,

		/// <summary>A long text waits for its next page: any key or pad button pages, Escape, Start and B
		/// too (Interaction, UWMessageLog.UpdatePaging; cleanup 7, wave 2b). Nobody asks: the paging reads
		/// "any key" itself; the row keeps the press from the modern routing below.</summary>
		Paging,

		/// <summary>A window picture is up: any key or pad button hides it (Interaction; wave 2b), as
		/// Paging.</summary>
		WindowPicture,

		/// <summary>The big map in the modern scheme: Escape, Start or B close it (UWGameUI, UWHotkeys).</summary>
		ModernMap,

		/// <summary>The modern closable chain - a waiting spell, the bags' use mode, the bags, the panels:
		/// the first link closes (UWModernUi.CloseNext; UWGameUI, UWHotkeys).</summary>
		ModernChain,

		/// <summary>The modern game menu: Escape and Start open and close it, B closes it (UWGameUI, UWHotkeys).</summary>
		ModernMenu
	}

	/// <summary>The states the chain asks about: what is open or holds the keys now. One bit each; the
	/// Unity side tests them once a frame, when a back press came.</summary>
	[System.Flags]
	public enum UWBackState : long
	{
		None = 0,
		Cutscene = 1L << 0,
		RoamingSight = 1L << 1,
		DebugTools = 1L << 2,
		SetupEffects = 1L << 3,

		/// <summary>The setup bar has the input: a dialog, its keyboard navigation, the mouse on it
		/// (UWSetupMenu.IsCapturingInput).</summary>
		SetupCapturing = 1L << 4,

		/// <summary>A dialog of the setup bar is open (UWSetupMenu.HasOpenPanel).</summary>
		SetupPanel = 1L << 5,
		KitText = 1L << 6,

		/// <summary>The help has the keyboard: its notes typed or the manual read (UWHelpWindow.BlocksGameKeys).</summary>
		HelpKeys = 1L << 7,
		LetterGrid = 1L << 8,
		CharacterName = 1L << 9,
		MapNote = 1L << 10,
		ClassicSaveName = 1L << 11,

		// 1L << 12 was OptionsEffects, the classic options' own effects screen, until cleanup 8d.3.
		Instrument = 1L << 13,
		ConversationAnswer = 1L << 14,

		/// <summary>The message scroll's running line: the count prompt, the yes/no question, the
		/// input line (Interaction.IsPromptActive).</summary>
		PromptLine = 1L << 15,
		Mantra = 1L << 16,
		BagMenu = 1L << 17,
		BagSplit = 1L << 18,
		ModernSaveName = 1L << 19,

		/// <summary>The main menu or the character creation (UWScreenUi.IsScreenMenuOpen).</summary>
		ScreenMenu = 1L << 20,

		/// <summary>A conversation is open.</summary>
		Conversation = 1L << 21,

		/// <summary>The big map is up.</summary>
		MapVisible = 1L << 22,

		/// <summary>The modern game menu is open (the layout editor included).</summary>
		MenuOpen = 1L << 23,

		/// <summary>A link of the modern closable chain has something to close (UWModernUi.HasClosable).</summary>
		ModernClosable = 1L << 24,

		/// <summary>The Controls window waits for a key or a button, or the wait ended in this frame
		/// (UWKeyBindings.HoldsKeys).</summary>
		Rebind = 1L << 25,

		/// <summary>The setup bar's keyboard navigation is on (UWSetupMenu.IsKeyNavOn).</summary>
		SetupKeyNav = 1L << 26,

		/// <summary>The layout editor's save question is up (UWModernLayoutEditor.IsAskingLeave).</summary>
		LayoutAsk = 1L << 27,

		/// <summary>A long text waits for its next page and the interaction gets to it this frame, or it
		/// paged with this frame's press (Interaction.PagingTakesKeys).</summary>
		Paging = 1L << 28,

		/// <summary>A window picture is up and the interaction gets to it this frame, or it went with this
		/// frame's press (Interaction.PictureTakesKeys).</summary>
		WindowPicture = 1L << 29
	}

	/// <summary>One way an owner takes a back press: by these keys, when Needs all hold and none of
	/// Under (the owners above it that keep the press from it today).</summary>
	public sealed class UWBackRow
	{
		public UWBackOwner Owner;

		public UWBackKey Keys;

		public UWBackState Needs;

		public UWBackState Under;

		/// <summary>THE TOPMOST KEEPS IT (cleanup 7, wave 2b): when this state holds and the row takes the
		/// press, no owner below takes it - the state that says the owner is up. None for an owner whose
		/// state does not say that exactly (it keeps from the others only what their Under lists).</summary>
		public UWBackState Covers;
	}

	/// <summary>
	/// THE ONE ESCAPE AND BACK CHAIN (cleanup step 7.5, 2026-10-25). About thirty places read Escape or
	/// the pad's B each for itself, the order in which they won given by their own tests of the others
	/// and by the frame's update order - the risk section 11 of the cleanup plan names. Here every one
	/// is a row, top first, with the keys it takes and, as Under, exactly the states that kept the press
	/// from it before - the guards its code or its caller wrote out (the lock's silent Menu act, the
	/// classic options under a typed save name, the modern bags under the game menu, the conversation's
	/// answer under any other typing, the modern map, chain and menu under every typing, a screen menu,
	/// a cutscene, a conversation and the roaming view). Where nothing kept two owners apart, both took
	/// the press until wave 2b (below).
	///
	/// ONE PRESS, ONE TAKER (cleanup 7, wave 2b, decided 2026-10-25 per user): one Escape, one Start and
	/// one pad B close only the topmost thing. The doubles step 7.5 kept are gone: an owner that is up
	/// (its Covers state) and takes the press keeps it from every owner below - the Controls window's
	/// wait for a key, the debug tools, the setup bar's effects screen and its keyboard navigation (both
	/// drawn over the game menu and the classic options), the layout editor's question, a long text's
	/// paging and a window picture (both take any key) -, and the classic options lie under the help
	/// that has the keyboard. Covering goes by key: an owner that does not take a key (the debug tools
	/// and the setup bar's key navigation take no pad button) leaves that key to the owners below; the
	/// setup bar's effects screen takes the pad's B and Start too.
	///
	/// DECIDED ONCE A FRAME (UWBack): at the first question of a frame with a back press the states are
	/// tested and the takers fixed - the state when the press came. Before, each place tested the others
	/// when its own Update ran, so a place that closed in the same frame could hand the press on, in an
	/// order Unity does not promise; the frame flags that kept the cutscene's and the roaming view's
	/// Escape from opening the game menu (UWIntroPlayer.HoldsKeys, UWRoamingSight.EndedByEscapeThisFrame)
	/// say the same and stay. An owner's own state is not asked: it asks only while it is open, under the
	/// conditions its code always had - but for a covering row's (fCover).
	/// </summary>
	public static class UWBackChain
	{
		/// <summary>Text entry in the wide sense (UWInputContexts.IsTextEntry): the modern routing is shut
		/// under it (UWGameUI's key routing, the Typing gate of UWHotkeys).</summary>
		public const UWBackState Typing = UWBackState.MapNote | UWBackState.ClassicSaveName | UWBackState.ModernSaveName
			| UWBackState.ConversationAnswer | UWBackState.CharacterName | UWBackState.KitText | UWBackState.Instrument
			| UWBackState.HelpKeys | UWBackState.BagSplit | UWBackState.BagMenu | UWBackState.Mantra | UWBackState.PromptLine;

		/// <summary>The modern routing's other closures: a screen menu, a cutscene, a conversation, the
		/// roaming view.</summary>
		public const UWBackState ModernShut = Typing | UWBackState.ScreenMenu | UWBackState.Cutscene | UWBackState.Conversation
			| UWBackState.RoamingSight;

		/// <summary>The pad's B goes past these to the modern map, chain and menu no more (UWGameUI).</summary>
		public const UWBackState PadKept = UWBackState.LetterGrid | UWBackState.BagMenu | UWBackState.BagSplit;

		/// <summary>The typing lock silences the Menu act under these (UWInputContexts.LocksKeys).</summary>
		public const UWBackState LocksKeys = Typing | UWBackState.DebugTools;

		/// <summary>Every back key: what "any key" takes.</summary>
		public const UWBackKey AnyKey = UWBackKey.Escape | UWBackKey.Menu | UWBackKey.PadBack | UWBackKey.PadEast | UWBackKey.PadButton;

		/// <summary>The rows, top first.</summary>
		public static readonly UWBackRow[] Rows =
		{
			fCover(UWBackOwner.Rebind, AnyKey, UWBackState.Rebind),
			fCover(UWBackOwner.DebugTools, UWBackKey.Escape, UWBackState.DebugTools),
			fRow(UWBackOwner.Cutscene, UWBackKey.Escape),
			fRow(UWBackOwner.RoamingSight, UWBackKey.Escape),
			fCover(UWBackOwner.SetupEffects, UWBackKey.Escape | UWBackKey.Menu | UWBackKey.PadBack | UWBackKey.PadButton, UWBackState.SetupEffects),

			// The setup bar's Escape is its effects window's while that is open (cleanup 8d.3: a window of the
			// bar, which its keyboard navigation steps through; until then the bar read no keys under it); a
			// kit field that types has the keys, the arrows too (UWKitRows.Begin).
			fCover(UWBackOwner.SetupKeyNav, UWBackKey.Escape, UWBackState.SetupKeyNav, UWBackState.SetupEffects | UWBackState.KitText),
			fRow(UWBackOwner.KitText, UWBackKey.Escape),
			fRow(UWBackOwner.HelpReading, UWBackKey.Escape),
			fRow(UWBackOwner.LetterGrid, UWBackKey.PadEast),
			fRow(UWBackOwner.CharacterName, UWBackKey.Escape),
			fRow(UWBackOwner.CharacterCreation, UWBackKey.Escape | UWBackKey.PadButton),

			// The main menu leaves its keys to the setup bar while that has the input.
			fRow(UWBackOwner.MainMenuSaves, UWBackKey.Escape | UWBackKey.PadButton, UWBackState.None, UWBackState.SetupCapturing),
			fRow(UWBackOwner.MapNote, UWBackKey.Escape),
			fRow(UWBackOwner.ClassicSaveName, UWBackKey.Escape),

			// The classic options: after the save name, not under a setup dialog - the effects window of their
			// Very High included, the setup bar's since cleanup 8d.3 -, nor under the help that has the
			// keyboard - its notes typed or its manual read (wave 2b).
			fRow(UWBackOwner.ClassicOptions, UWBackKey.Escape, UWBackState.None,
				UWBackState.ClassicSaveName | UWBackState.SetupPanel | UWBackState.KitText | UWBackState.HelpKeys),
			fRow(UWBackOwner.Instrument, UWBackKey.Escape),

			// The typed answer hears no key while the help has the keyboard, a running line is up or
			// anything else types (UWConversationController.IsKeyboardTaken).
			fRow(UWBackOwner.ConversationAnswer, UWBackKey.Escape, UWBackState.None,
				(LocksKeys & ~UWBackState.ConversationAnswer)),
			fRow(UWBackOwner.CountPrompt, UWBackKey.Escape | UWBackKey.PadButton),
			fRow(UWBackOwner.Mantra, UWBackKey.Escape),
			fRow(UWBackOwner.YesNo, UWBackKey.Escape | UWBackKey.PadButton),
			fRow(UWBackOwner.ModernQuestion, UWBackKey.Escape),

			// The bags' menu and split box rest under the game menu and the map (UWModernBags.Update).
			fRow(UWBackOwner.BagMenu, UWBackKey.Escape | UWBackKey.PadBack, UWBackState.None, UWBackState.MenuOpen | UWBackState.MapVisible),
			fRow(UWBackOwner.BagSplit, UWBackKey.Escape | UWBackKey.PadButton, UWBackState.None, UWBackState.MenuOpen | UWBackState.MapVisible),

			// The save page draws only without a setup dialog over it.
			fRow(UWBackOwner.ModernSaveName, UWBackKey.Escape, UWBackState.None, UWBackState.SetupPanel),
			fCover(UWBackOwner.LayoutAsk, UWBackKey.Escape, UWBackState.LayoutAsk),

			// Any key pages a long text or hides a window picture (wave 2b: the modern game menu opened
			// with the same Escape).
			fCover(UWBackOwner.Paging, AnyKey, UWBackState.Paging),
			fCover(UWBackOwner.WindowPicture, AnyKey, UWBackState.WindowPicture),

			// The modern routing (UWGameUI): the map first, then the chain, then the menu.
			fRow(UWBackOwner.ModernMap, UWBackKey.Menu, UWBackState.MapVisible, ModernShut),
			fRow(UWBackOwner.ModernMap, UWBackKey.PadBack, UWBackState.MapVisible, ModernShut | UWBackState.LetterGrid),
			fRow(UWBackOwner.ModernChain, UWBackKey.Menu, UWBackState.ModernClosable, ModernShut | UWBackState.MapVisible | UWBackState.MenuOpen),
			fRow(UWBackOwner.ModernChain, UWBackKey.PadBack, UWBackState.ModernClosable,
				ModernShut | PadKept | UWBackState.MapVisible | UWBackState.MenuOpen),
			fRow(UWBackOwner.ModernMenu, UWBackKey.Menu, UWBackState.None, ModernShut | UWBackState.MapVisible | UWBackState.ModernClosable),

			// B closes the menu, Escape and Start open it too; with the menu open the chain is passed.
			fRow(UWBackOwner.ModernMenu, UWBackKey.Menu, UWBackState.MenuOpen | UWBackState.ModernClosable, ModernShut | UWBackState.MapVisible),
			fRow(UWBackOwner.ModernMenu, UWBackKey.PadBack, UWBackState.MenuOpen, ModernShut | PadKept | UWBackState.MapVisible)
		};

		private static UWBackRow fRow(UWBackOwner peOwner, UWBackKey peKeys, UWBackState peNeeds = UWBackState.None,
			UWBackState peUnder = UWBackState.None)
		{
			return new UWBackRow { Owner = peOwner, Keys = peKeys, Needs = peNeeds, Under = peUnder };
		}

		/// <summary>A row of an owner whose state says it is up: it keeps the press it takes from the
		/// owners below (UWBackRow.Covers). It needs that state - the one place an owner's own state is
		/// asked: the owners that read their keys themselves (the wait for a key, the paging, the
		/// picture) ask nobody, and the others ask only while it holds.</summary>
		private static UWBackRow fCover(UWBackOwner peOwner, UWBackKey peKeys, UWBackState peCovers, UWBackState peUnder = UWBackState.None)
		{
			UWBackRow lORow = fRow(peOwner, peKeys, peCovers, peUnder);

			lORow.Covers = peCovers;

			return lORow;
		}

		/// <summary>Whether a row takes the press: a pressed key of its own, its needs hold and nothing it
		/// lies under is there.</summary>
		private static bool fRowTakes(UWBackRow pORow, UWBackKey pePressed, UWBackState peStates)
		{
			return (pORow.Keys & pePressed) != 0 && (pORow.Needs & peStates) == pORow.Needs && (pORow.Under & peStates) == 0;
		}

		/// <summary>Whether an owner takes this frame's press: one of its rows takes it, and no owner above
		/// it that is up (its Covers state) takes it first.</summary>
		public static bool Takes(UWBackOwner peOwner, UWBackKey pePressed, UWBackState peStates)
		{
			bool lbTakes = false;

			foreach (UWBackRow lORow in Rows)
			{
				if (lORow.Owner == peOwner && fRowTakes(lORow, pePressed, peStates))
					lbTakes = true;
			}

			return lbTakes && CoveredBy(peOwner, pePressed, peStates) == null;
		}

		/// <summary>The owner above peOwner that is up and takes the press, keeping it from peOwner; null
		/// when there is none.</summary>
		public static UWBackOwner? CoveredBy(UWBackOwner peOwner, UWBackKey pePressed, UWBackState peStates)
		{
			foreach (UWBackRow lORow in Rows)
			{
				if (lORow.Owner >= peOwner)
					break;

				if (lORow.Covers != UWBackState.None && (lORow.Covers & peStates) == lORow.Covers && fRowTakes(lORow, pePressed, peStates))
					return lORow.Owner;
			}

			return null;
		}

		/// <summary>The keys a physical press gives: Escape is the Escape key and the Menu act unless the
		/// typing lock silences that; Start the Menu act alone; the pad's B its act and its place.</summary>
		public static UWBackKey EscapeKeys(UWBackState peStates)
		{
			return UWBackKey.Escape | ((peStates & LocksKeys) != 0 ? UWBackKey.None : UWBackKey.Menu);
		}

		public static UWBackKey StartKeys(UWBackState peStates)
		{
			return (peStates & LocksKeys) != 0 ? UWBackKey.None : UWBackKey.Menu;
		}

		public const UWBackKey PadBKeys = UWBackKey.PadBack | UWBackKey.PadEast | UWBackKey.PadButton;

		/// <summary>The owners among pOAsking that take a press, top first (the record's form).</summary>
		public static List<UWBackOwner> Takers(IEnumerable<UWBackOwner> pOAsking, UWBackKey pePressed, UWBackState peStates)
		{
			List<UWBackOwner> lOTakers = new List<UWBackOwner>();

			for (int liOwner = 0; liOwner <= (int)UWBackOwner.ModernMenu; liOwner++)
			{
				UWBackOwner leOwner = (UWBackOwner)liOwner;

				foreach (UWBackOwner leAsking in pOAsking)
				{
					if (leAsking == leOwner && Takes(leOwner, pePressed, peStates))
					{
						lOTakers.Add(leOwner);
						break;
					}
				}
			}

			return lOTakers;
		}

		/// <summary>The chain as text, one row a line: the record the self-check pins.</summary>
		public static string Describe()
		{
			StringBuilder lOText = new StringBuilder();

			foreach (UWBackRow lORow in Rows)
			{
				lOText.Append(lORow.Owner).Append(" | keys ").Append(lORow.Keys).Append(" | needs ").Append(lORow.Needs)
					.Append(" | under ").Append(lORow.Under);

				if (lORow.Covers != UWBackState.None)
					lOText.Append(" | covers ").Append(lORow.Covers);

				lOText.Append('\n');
			}

			return lOText.ToString();
		}
	}
}
