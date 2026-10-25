namespace UWDataImport.UWData
{
	/// <summary>
	/// THE CLASSIC OPTIONS MENU'S STATE MACHINE (cleanup 5.18, 2026-10-23: out of UWHudOptions, the
	/// rules as they were; UWHudOptions keeps the drawing, the input, the holds and the messages):
	/// which screen the panel shows (the top menu, save, restore, detail, quit, music, sound),
	/// which picture of OPTBTNS.GR stands on each of its seven slots and which background behind
	/// them, which slot the key highlight lights and how the arrow keys move it, which slots can be
	/// chosen at all, and what a choice does - a page change here, or an outcome the view carries
	/// out (a save name to type, a slot to restore, a setting to switch, the detail level, the quit,
	/// the panel closed).
	///
	/// The screens are the original's (read 2026-09-26, ovr130_2AA and the entry table from 0x1A6D
	/// for the music and sound screens, ovr130_0 and ovr130_6F2 for the keys): the picture numbers
	/// in OPTBTNS.GR follow the reference (uimanager_options.OptionButtonIndices) - for uw1 they
	/// are directly the picture numbers, only uw2 first cuts its buttons to shape.
	/// </summary>
	public sealed class UWOptionsMenuModel
	{
		public enum PageEnum
		{
			Closed,
			Top,
			Restore,
			Save,
			Detail,
			Quit,
			Music,
			Sound
		}

		/// <summary>What the view has to do after a choice - the page is already changed.</summary>
		public enum ActionEnum
		{
			None,

			/// <summary>SAVE on the top menu: the view asks whether saving refuses (the Void,
			/// "partway"; UWHudOptions.RefusesToSave) and, if not, calls ShowSave and lists the
			/// games.</summary>
			AskSave,

			/// <summary>The restore screen is up: the view lists the four games in the scroll.</summary>
			ListSaves,

			/// <summary>A save slot (Value) was chosen: the view starts typing its name.</summary>
			BeginSaveName,

			/// <summary>A restore slot (Value) was chosen: the view restores it, or says nothing is
			/// there and stays on the screen.</summary>
			Restore,

			/// <summary>Music on (Value 1) or off (0); the screen shows the new state, unless the
			/// panel closes (ClosesPanel, opened by a Ctrl shortcut).</summary>
			SetMusic,

			SetSound,

			/// <summary>The detail level (Value, UWGraphicsDetail.LevelEnum's number); the view
			/// sets it and shows the screen anew with ShowDetail.</summary>
			SetDetail,

			/// <summary>VERY HIGH: the level set, then the effects window opens (the setup bar's, with
			/// the bar, since cleanup 8d.3).</summary>
			SetDetailAndOpenEffects,

			/// <summary>YES on the quit question: back to the main menu (the panel closes).</summary>
			QuitToMainMenu
		}

		/// <summary>A choice's outcome: the action, its value, and whether the view closes the
		/// panel (which also closes this model, Close).</summary>
		public readonly struct Outcome
		{
			public readonly ActionEnum Action;

			public readonly int Value;

			public readonly bool ClosesPanel;

			public Outcome(ActionEnum peAction, int piValue, bool pbClosesPanel)
			{
				Action = peAction;
				Value = piValue;
				ClosesPanel = pbClosesPanel;
			}

			public static readonly Outcome None = new Outcome(ActionEnum.None, 0, false);
		}

		// ------------------------------------------------- Picture numbers in OPTBTNS.GR

		public const int AllOptionButtons = 1;

		public const int AllSaveButtons = 2;

		public const int SaveGame = 6;

		public const int RestoreGame = 8;

		public const int Music = 10;

		public const int Sound = 12;

		public const int Detail = 14;

		public const int DetailButtons = 5;

		public const int LowDetail = 38;

		/// <summary>"QUIT GAME: ARE YOU SURE?" with its two buttons - YES on slot 2, NO on slot 3
		/// (matched against the panel, OPTBTNS 3).</summary>
		public const int QuitBackground = 3;

		public const int Yes = 57;

		public const int No = 59;

		/// <summary>"DETAIL LEVEL IS: LOW" - the three others follow (53 to 56). 34 by 18, one pixel
		/// further up and left than the slot grid (matched against the detail panel, OPTBTNS 5).</summary>
		public const int DetailLabel = 53;

		public const int ReturnToGame = 16;

		public const int QuitGame = 18;

		public const int Cancel = 24;

		// THE MUSIC AND SOUND SCREENS (read 2026-09-26, ovr130_2AA and the entry table from
		// 0x1A6D): background 4, the state at the top ("MUSIC IS ON." / "... OFF.", "SOUND IS
		// ON." / "... OFF"), under it "TURN MUSIC:" / "TURN SOUND:", then ON, OFF and DONE.

		public const int OnOffBackground = 4;

		public const int On = 20;

		public const int Off = 22;

		/// <summary>The DONE of the music and sound screens; the detail screen has its own
		/// (DetailDone).</summary>
		public const int OnOffDone = 26;

		public const int MusicIsOn = 47;

		public const int MusicIsOff = 48;

		public const int SoundIsOn = 49;

		public const int SoundIsOff = 50;

		public const int TurnMusic = 51;

		public const int TurnSound = 52;

		/// <summary>The detail screen's DONE: picture 28 in the original's table, not 26.</summary>
		public const int DetailDone = 28;

		public const int Save1 = 30;

		public const int RestoreGameLabel = 46;

		/// <summary>Each button has two pictures: the idle one and, one further, the
		/// highlighted one.</summary>
		public const int HighlightOffset = 1;

		/// <summary>No picture at this slot.</summary>
		public const int NoPicture = -1;

		/// <summary>The seven slots of the panel (UWClassicLayout.Options).</summary>
		public const int EntryCount = UWClassicLayout.Options.EntryCount;

		/// <summary>The entries of the top menu, counted from the top (the original counts them
		/// from the bottom: quit 0 ... save 6).</summary>
		public const int TopSave = 0;

		public const int TopRestore = 1;

		public const int TopMusic = 2;

		public const int TopSound = 3;

		public const int TopDetail = 4;

		public const int TopReturn = 5;

		public const int TopQuit = 6;

		/// <summary>The save and restore screens' slots: the first game on slot 1, cancel on 5.</summary>
		public const int SlotCount = 4;

		public const int SlotCancel = 5;

		// ------------------------------------------------- State

		public PageEnum Page { get; private set; } = PageEnum.Closed;

		/// <summary>The panel was opened by a Ctrl shortcut (the original's flag 0x1A34): then the
		/// music and sound screens close the whole panel after their choice instead of going back
		/// to the top menu.</summary>
		public bool OpenedByShortcut { get; private set; }

		/// <summary>The background picture of the screen, NoPicture while closed.</summary>
		public int Background { get; private set; } = NoPicture;

		private readonly int[] miEntries = new int[EntryCount];

		/// <summary>Which picture lies on which slot - for highlighting and to know whether a slot
		/// is clickable at all; NoPicture leaves a slot empty.</summary>
		public int EntryAt(int piEntry)
		{
			return miEntries[piEntry];
		}

		/// <summary>The entry the keys have highlighted, -1 for none (then the mouse's counts).</summary>
		public int KeyEntry { get; private set; } = -1;

		/// <summary>The menu the key highlight belongs to - a new menu starts without one.</summary>
		private PageEnum meKeyMenu = PageEnum.Closed;

		public UWOptionsMenuModel()
		{
			fSetEntries(NoPicture, NoPicture, NoPicture, NoPicture, NoPicture, NoPicture, NoPicture, NoPicture);
		}

		public bool IsOpen => Page != PageEnum.Closed;

		/// <summary>A slot moved away from its grid position: the detail screen's label lies one
		/// pixel further up and left; every other screen uses the grid.</summary>
		public (int DeltaX, int DeltaY) EntryOffset(int piEntry)
		{
			return Page == PageEnum.Detail && piEntry == 0 ? (-1, -1) : (0, 0);
		}

		// ------------------------------------------------- Opening and closing

		/// <summary>The panel opens on the top menu (the holds are the view's).</summary>
		public void Open()
		{
			OpenedByShortcut = false;
			ShowTop();
		}

		/// <summary>
		/// THE ORIGINAL'S CTRL SHORTCUTS (read 2026-09-26: the key table sends Ctrl+S, R, M, F, D
		/// and Q to ovr130_7E8, which sets the top menu and has ovr130_6F2 choose its entry through
		/// ovr130_689 - s 6, r 5, m 4, f 3, d 2, q 0 from the bottom): they open the options and
		/// choose that entry as a click would, and the panel stays open on that entry's screen -
		/// the options loop ovr130_0 runs on. The flag 0x1A34 stays set meanwhile, so the music
		/// and sound screens then close the whole panel after their choice (OpenedByShortcut).
		/// The view opens (Open), marks this, then chooses the entry.
		/// </summary>
		public void MarkOpenedByShortcut()
		{
			OpenedByShortcut = true;
		}

		/// <summary>Closed: no screen, no pictures, the shortcut flag cleared.</summary>
		public void Close()
		{
			Page = PageEnum.Closed;
			OpenedByShortcut = false;
			fSetEntries(NoPicture, NoPicture, NoPicture, NoPicture, NoPicture, NoPicture, NoPicture, NoPicture);
		}

		// ------------------------------------------------- The screens

		/// <summary>Fills the seven slots; NoPicture leaves a slot empty.</summary>
		private void fSetEntries(int piBackground, int pi0, int pi1, int pi2, int pi3, int pi4, int pi5, int pi6)
		{
			Background = piBackground;
			miEntries[0] = pi0;
			miEntries[1] = pi1;
			miEntries[2] = pi2;
			miEntries[3] = pi3;
			miEntries[4] = pi4;
			miEntries[5] = pi5;
			miEntries[6] = pi6;
		}

		/// <summary>
		/// THE HIGHLIGHT A SCREEN STARTS WITH (per user on the original, 2026-09-26: the top entry
		/// is lit at once; read the same day in the screens' drawing routines): the top menu
		/// SAVE, the save and load screens the first slot, the detail screen the current level,
		/// the quit question NO, the music and sound screens the button of the current state.
		/// </summary>
		private void fSetKeyHighlight(int piEntry)
		{
			KeyEntry = piEntry;
			meKeyMenu = Page;
		}

		/// <summary>The top screen: seven buttons, as in the original.</summary>
		public void ShowTop()
		{
			Page = PageEnum.Top;
			fSetEntries(AllOptionButtons, SaveGame, RestoreGame, Music, Sound, Detail, ReturnToGame, QuitGame);
			fSetKeyHighlight(0);
		}

		/// <summary>
		/// The save screen: heading, the four slots, cancel - the same layout as
		/// for loading, only with "Save Game" as heading. The reference puts exactly the
		/// picture of the topmost button on the first slot for it.
		/// </summary>
		public void ShowSave()
		{
			Page = PageEnum.Save;
			fSetEntries(AllSaveButtons, SaveGame, Save1, Save1 + 2, Save1 + 4, Save1 + 6, Cancel, NoPicture);
			fSetKeyHighlight(1);
		}

		/// <summary>
		/// The load screen: a heading, the four slots and cancel.
		///
		/// The NAMES are not on the buttons but in the scroll - exactly that is
		/// how the reference does it (listsaves), and therefore the slots only have one
		/// picture each with "I" to "IV".
		/// </summary>
		public void ShowRestore()
		{
			Page = PageEnum.Restore;
			fSetEntries(AllSaveButtons, RestoreGameLabel, Save1, Save1 + 2, Save1 + 4, Save1 + 6, Cancel, NoPicture);
			fSetKeyHighlight(1);
		}

		/// <summary>
		/// The music or the sound screen (ovr130_2AA): the state and "TURN ...:" over ON, OFF and
		/// DONE, the button of the current state lit. Until 2026-09-26 the top menu's entry
		/// switched at once with a line in the scroll (per user: the original has these screens).
		/// </summary>
		public void ShowOnOff(bool pbMusic, bool pbOn)
		{
			Page = pbMusic ? PageEnum.Music : PageEnum.Sound;

			int liState = pbMusic ? (pbOn ? MusicIsOn : MusicIsOff) : (pbOn ? SoundIsOn : SoundIsOff);

			fSetEntries(OnOffBackground, liState, pbMusic ? TurnMusic : TurnSound, On, Off, OnOffDone, NoPicture, NoPicture);
			fSetKeyHighlight(pbOn ? 2 : 3);
		}

		/// <summary>
		/// The detail screen of the original, with new meanings (per user, 2026-09-17): LOW the
		/// palette renderer, MEDIUM Remastered without effects, HIGH Remastered with all of them,
		/// VERY HIGH the viewer's own values on the effects screen (see UWGraphicsDetail). The
		/// panel carries "SET NEW LEVEL TO:" itself; the label at the top names the current level
		/// (0 to 3), one pixel up and left of the grid (EntryOffset).
		/// </summary>
		public void ShowDetail(int piCurrentLevel)
		{
			Page = PageEnum.Detail;
			fSetEntries(DetailButtons, DetailLabel + piCurrentLevel, NoPicture, LowDetail, LowDetail + 2, LowDetail + 4, LowDetail + 6, DetailDone);
			fSetKeyHighlight(2 + piCurrentLevel);
		}

		/// <summary>
		/// "Quit Game" asks first, as in the original, and then goes BACK TO THE MAIN MENU instead of
		/// ending the program (per user, 2026-09-17) - from there one can load, start a new character
		/// or really quit. The panel is the same in both control schemes.
		/// </summary>
		public void ShowQuit()
		{
			Page = PageEnum.Quit;
			fSetEntries(QuitBackground, NoPicture, NoPicture, Yes, No, NoPicture, NoPicture, NoPicture);
			fSetKeyHighlight(3);
		}

		// ------------------------------------------------- The key highlight

		/// <summary>
		/// THE KEYS (per user on the original, 2026-09-26: arrows and Enter work in the options;
		/// read the same day in the options loop ovr130_0 and ovr130_6F2): Up and Down move the
		/// highlight one entry - Space moves it down too -, Enter chooses it, Escape closes the
		/// panel (ovr130_1BE). The highlight stops at the ends and does not step onto an entry
		/// that cannot be chosen. NO MOUSE HOVER (per user on the original, 2026-09-26): only
		/// this highlight is ever lit - the pointer over an entry lights nothing, a click just
		/// chooses the entry under it. A new menu starts without a highlight of the keys.
		/// </summary>
		public void ResetKeyEntryIfPageChanged()
		{
			if (meKeyMenu != Page)
			{
				meKeyMenu = Page;
				KeyEntry = -1;
			}
		}

		/// <summary>One step of the key highlight, Up (-1) or Down (1).</summary>
		public void StepKey(int piStep)
		{
			KeyEntry = StepKeyEntry(KeyEntry, piStep);
		}

		/// <summary>One step of the key highlight (entries counted from the top). Without a
		/// highlight yet, Down starts on the first entry that can be chosen and Up on the last;
		/// otherwise it moves one entry and stays where it is at the end or before an entry that
		/// cannot be chosen, as the original's ovr130_6F2 does.</summary>
		public int StepKeyEntry(int piFrom, int piStep)
		{
			if (piFrom < 0)
			{
				for (int liAt = piStep > 0 ? 0 : EntryCount - 1; liAt >= 0 && liAt < EntryCount; liAt += piStep)
				{
					if (IsKeyEntryUsable(liAt))
						return liAt;
				}

				return -1;
			}

			int liTo = piFrom + piStep;

			return liTo >= 0 && liTo < EntryCount && IsKeyEntryUsable(liTo) ? liTo : piFrom;
		}

		public bool IsKeyEntryUsable(int piEntry)
		{
			return miEntries[piEntry] != NoPicture && IsEntryClickable(piEntry);
		}

		/// <summary>A CLICK MOVES THE HIGHLIGHT onto the entry it chooses, so the chosen save game
		/// stays lit while its name is typed or it loads (per user, 2026-09-27: since the hover
		/// went, the chosen slot was no longer lit when saving or loading).</summary>
		public void HighlightByClick(int piEntry)
		{
			KeyEntry = piEntry;
		}

		/// <summary>Only one is ever highlighted, and the heading never - it has no second picture
		/// at all.</summary>
		public bool IsLit(int piEntry)
		{
			return piEntry == KeyEntry && IsEntryClickable(piEntry);
		}

		/// <summary>The picture the slot shows: its own, one further when lit.</summary>
		public int PictureOf(int piEntry)
		{
			return miEntries[piEntry] + (IsLit(piEntry) ? HighlightOffset : 0);
		}

		/// <summary>The heading of the load and save screens is not a button.</summary>
		public bool IsEntryClickable(int piEntry)
		{
			if (Page == PageEnum.Detail)
				return piEntry >= 2;

			if (Page == PageEnum.Quit)
				return piEntry == 2 || piEntry == 3;

			if (Page == PageEnum.Music || Page == PageEnum.Sound)
				return piEntry >= 2 && piEntry <= 4;

			return !((Page == PageEnum.Restore || Page == PageEnum.Save)
				&& piEntry == 0);
		}

		// ------------------------------------------------- A choice

		/// <summary>The settings a screen is drawn from: the music and sound switches, the detail
		/// level (UWGraphicsDetail.LevelEnum's number).</summary>
		public readonly struct Settings
		{
			public readonly bool MusicOn;

			public readonly bool SoundOn;

			public readonly int DetailLevel;

			public Settings(bool pbMusicOn, bool pbSoundOn, int piDetailLevel)
			{
				MusicOn = pbMusicOn;
				SoundOn = pbSoundOn;
				DetailLevel = piDetailLevel;
			}
		}

		/// <summary>A click or Enter on an entry: the page changes here, the outcome tells the view
		/// what to carry out.</summary>
		public Outcome Choose(int piEntry, Settings pOSettings)
		{
			if (Page == PageEnum.Top)
			{
				switch (piEntry)
				{
					case TopSave:
						// The Void and "partway" refuse here, not at the opener (check_save) - the
						// view asks, then ShowSave.
						return new Outcome(ActionEnum.AskSave, 0, false);

					case TopRestore:
						ShowRestore();
						return new Outcome(ActionEnum.ListSaves, 0, false);

					case TopReturn:
						return new Outcome(ActionEnum.None, 0, true);

					case TopQuit:
						ShowQuit();
						return Outcome.None;

					// Music and sound: the original's own screens since 2026-09-26 (until then
					// the entry switched at once with a line in the scroll).
					case TopMusic:
						ShowOnOff(true, pOSettings.MusicOn);
						return Outcome.None;

					case TopSound:
						ShowOnOff(false, pOSettings.SoundOn);
						return Outcome.None;

					case TopDetail:
						ShowDetail(pOSettings.DetailLevel);
						return Outcome.None;
				}

				return Outcome.None;
			}

			if (Page == PageEnum.Detail)
				return fChooseDetailEntry(piEntry);

			if (Page == PageEnum.Music || Page == PageEnum.Sound)
				return fChooseOnOffEntry(piEntry, pOSettings);

			if (Page == PageEnum.Quit)
			{
				if (piEntry == 2)
					return new Outcome(ActionEnum.QuitToMainMenu, 0, true);

				if (piEntry == 3)
					ShowTop();

				return Outcome.None;
			}

			if (Page == PageEnum.Save)
			{
				if (piEntry == SlotCancel)
					ShowTop();
				else if (piEntry >= 1 && piEntry <= SlotCount)
					return new Outcome(ActionEnum.BeginSaveName, piEntry, false);

				return Outcome.None;
			}

			if (Page != PageEnum.Restore)
				return Outcome.None;

			if (piEntry == SlotCancel)
			{
				ShowTop();

				return Outcome.None;
			}

			// Nothing on the slot - message, and stay on the load screen, just as in the original
			// (the view's part).
			return new Outcome(ActionEnum.Restore, piEntry, false);
		}

		/// <summary>LOW, MEDIUM and HIGH set the level and draw the screen anew (the view, with
		/// ShowDetail); VERY HIGH also opens the effects screen; DONE goes back to the top menu.</summary>
		private Outcome fChooseDetailEntry(int piEntry)
		{
			switch (piEntry)
			{
				case 2:
				case 3:
				case 4:
					return new Outcome(ActionEnum.SetDetail, piEntry - 2, false);

				case 5:
					return new Outcome(ActionEnum.SetDetailAndOpenEffects, 3, false);

				case 6:
					ShowTop();
					return Outcome.None;
			}

			return Outcome.None;
		}

		/// <summary>
		/// A choice on the music or sound screen (ovr130_3B5, ovr130_40D): ON and OFF switch and
		/// draw the screen anew, DONE switches nothing. Opened by a Ctrl shortcut, every one of
		/// the three closes the panel; otherwise ON and OFF stay on the screen and DONE goes back
		/// to the top menu.
		/// </summary>
		private Outcome fChooseOnOffEntry(int piEntry, Settings pOSettings)
		{
			bool lbMusic = Page == PageEnum.Music;
			bool lbOn = lbMusic ? pOSettings.MusicOn : pOSettings.SoundOn;
			ActionEnum leAction = ActionEnum.None;

			if (piEntry == 2 || piEntry == 3)
			{
				lbOn = piEntry == 2;
				leAction = lbMusic ? ActionEnum.SetMusic : ActionEnum.SetSound;
			}

			if (OpenedByShortcut)
				return new Outcome(leAction, lbOn ? 1 : 0, true);

			if (piEntry == 4)
				ShowTop();
			else
				ShowOnOff(lbMusic, lbOn);

			return new Outcome(leAction, lbOn ? 1 : 0, false);
		}
	}
}
