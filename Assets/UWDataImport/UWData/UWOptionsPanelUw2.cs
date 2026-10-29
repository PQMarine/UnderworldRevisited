namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S OPTIONS PANEL (UW2 plan phase 3 step 4, 2026-10-29), as Underworld Exhumed's uw2
	/// ui/WRAPPER.C and ui/ICONS.C have it: five groups of up to seven buttons in the right panel - the game
	/// (Save, Restore, Music, Sound, Detail, Quit, Return), the detail (four levels, Done), the quit (Yes,
	/// No), the file (the four slots, the way back) and music or sound (On, Off, Done). A button has a
	/// handler, a group it opens, or both (struct buttongroup's fn and sub); pressing it lights it, runs the
	/// handler and, unless that ended the options, opens the group (deal_with_button). Opening a group lights
	/// its first choice (each group's init function) and may show a message row over its first button
	/// (load_message: the detail level, "Restore Game", the music or sound being on or off). The engine-free
	/// part: which button is lit, which message shows, and what a press asks the game to do (Outcome); the
	/// pictures and the input are the screen's (UWGameScreenUw2).
	/// </summary>
	public sealed class UWOptionsPanelUw2
	{
		public enum GroupEnum
		{
			Game = 0,
			Detail = 1,
			Quit = 2,
			File = 3,
			MusicSound = 4
		}

		public enum ActionEnum
		{
			/// <summary>Nothing for the game to do; the panel shows the new state.</summary>
			None,
			/// <summary>The options end (Return, Escape, No, the file group's way back).</summary>
			Close,
			/// <summary>The file group opens for saving or restoring: the saves' descriptions into the scroll
			/// (ShowSaveRest).</summary>
			ListSaves,
			/// <summary>Save into slot Value (1 to 4), then the options end.</summary>
			Save,
			/// <summary>Restore slot Value, then the options end.</summary>
			Restore,
			/// <summary>Music on (Value 1) or off (0).</summary>
			SetMusic,
			/// <summary>Sound effects on (Value 1) or off (0).</summary>
			SetSound,
			/// <summary>The detail level Value (0 to 3).</summary>
			SetDetail,
			/// <summary>Leave the game (Yes on the quit page).</summary>
			Quit
		}

		public struct Outcome
		{
			public ActionEnum Action;

			public int Value;
		}

		public const int ButtonCount = 7;

		/// <summary>No button lit, no message shown.</summary>
		public const int None = -1;

		/// <summary>The group shown.</summary>
		public GroupEnum Group { get; private set; }

		/// <summary>The lit button (current_hilit_button), None before a group's init lights one.</summary>
		public int Highlight { get; private set; } = None;

		/// <summary>The row of the group's message picture over the first button, None for none.</summary>
		public int MessageRow { get; private set; } = None;

		/// <summary>The file group restores (save_rest 1) rather than saves.</summary>
		public bool Restoring { get; private set; }

		/// <summary>The music-or-sound group is the sound's (music_sound 1).</summary>
		public bool SoundPage { get; private set; }

		/// <summary>The slot last saved into or restored (last_sr_file_num), lit when the file group opens.</summary>
		public int LastSlot { get; set; } = 1;

		/// <summary>The state the groups' init functions show: the detail level, music and sound on.</summary>
		public int DetailLevel { get; set; }

		public bool MusicOn { get; set; }

		public bool SoundOn { get; set; }

		/// <summary>Whether a button of the group does anything (is_really_button: a handler or a group).</summary>
		public bool IsButton(int piButton)
		{
			return piButton >= 0 && piButton < ButtonCount && (fKind(Group, piButton) != KindEnum.Nothing);
		}

		/// <summary>Opens a group (install_buttongroup): nothing lit, then its init function.</summary>
		public void Open(GroupEnum peGroup)
		{
			Group = peGroup;
			Highlight = None;
			MessageRow = None;

			switch (peGroup)
			{
				case GroupEnum.Game:
					Highlight = 0;
					break;
				case GroupEnum.Detail:
					Highlight = DetailLevel + 2;
					MessageRow = DetailLevel;
					break;
				case GroupEnum.Quit:
					Highlight = 3;
					break;
				case GroupEnum.File:
					Highlight = LastSlot;

					if (Restoring)
						MessageRow = 1;
					break;
				case GroupEnum.MusicSound:
					Highlight = 3 - ((SoundPage ? SoundOn : MusicOn) ? 1 : 0);
					MessageRow = (SoundPage ? 2 : 0) + ((SoundPage ? SoundOn : MusicOn) ? 1 : 0);
					break;
			}
		}

		/// <summary>Opens the file group for saving or restoring (save_opt, restore_opt; the shortcuts Ctrl+S
		/// and Ctrl+R), the music or the sound group (music_opt, sound_opt; Ctrl+M, Ctrl+F).</summary>
		public void OpenFile(bool pbRestore)
		{
			Restoring = pbRestore;
			Open(GroupEnum.File);
		}

		public void OpenMusicSound(bool pbSound)
		{
			SoundPage = pbSound;
			Open(GroupEnum.MusicSound);
		}

		/// <summary>Moves the light by piDirection, round the seven, past the empty ones (move_hilite).</summary>
		public void Move(int piDirection)
		{
			if (Highlight == None)
				return;

			int liButton = Highlight;

			for (int liStep = 0; liStep < ButtonCount; liStep++)
			{
				liButton = (((liButton + piDirection) % ButtonCount) + ButtonCount) % ButtonCount;

				if (IsButton(liButton))
				{
					Highlight = liButton;

					return;
				}
			}
		}

		/// <summary>Lights a button under the held pointer (better_mouse_release), the old one off it.</summary>
		public void Light(int piButton)
		{
			if (IsButton(piButton))
				Highlight = piButton;
		}

		/// <summary>Presses a button (deal_with_button): its handler, then its group unless the handler ended
		/// the options.</summary>
		public Outcome Press(int piButton)
		{
			Outcome lOResult = new Outcome { Action = ActionEnum.None };

			if (!IsButton(piButton))
				return lOResult;

			Highlight = piButton;

			switch (fKind(Group, piButton))
			{
				case KindEnum.Save:
					Restoring = false;
					OpenFile(false);
					lOResult.Action = ActionEnum.ListSaves;
					break;
				case KindEnum.Restore:
					OpenFile(true);
					lOResult.Action = ActionEnum.ListSaves;
					break;
				case KindEnum.Music:
					OpenMusicSound(false);
					break;
				case KindEnum.Sound:
					OpenMusicSound(true);
					break;
				case KindEnum.OpenDetail:
					Open(GroupEnum.Detail);
					break;
				case KindEnum.OpenQuit:
					Open(GroupEnum.Quit);
					break;
				case KindEnum.OpenGame:
					Open(GroupEnum.Game);
					break;
				case KindEnum.Close:
					lOResult.Action = ActionEnum.Close;
					break;
				case KindEnum.SetDetail:
					DetailLevel = piButton - 2;
					MessageRow = DetailLevel;
					lOResult = new Outcome { Action = ActionEnum.SetDetail, Value = DetailLevel };
					break;
				case KindEnum.QuitYes:
					lOResult.Action = ActionEnum.Quit;
					break;
				case KindEnum.Slot:
					LastSlot = piButton;
					lOResult = new Outcome { Action = Restoring ? ActionEnum.Restore : ActionEnum.Save, Value = piButton };
					break;
				case KindEnum.On:
				case KindEnum.Off:
					bool lbOn = fKind(Group, piButton) == KindEnum.On;

					if (SoundPage)
						SoundOn = lbOn;
					else
						MusicOn = lbOn;

					MessageRow = (SoundPage ? 2 : 0) + (lbOn ? 1 : 0);
					lOResult = new Outcome { Action = SoundPage ? ActionEnum.SetSound : ActionEnum.SetMusic, Value = lbOn ? 1 : 0 };
					break;
			}

			return lOResult;
		}

		/// <summary>The button under a point relative to the panel's corner 0xEC and y 0x4E counted up
		/// (get_buttonreg_button): 0 at the top, 6 at the bottom; None outside.</summary>
		public static int ButtonAt(int piX, int piY)
		{
			int liButton = 6 - (piY / 16);

			return piY < 0 || liButton < 0 || liButton >= ButtonCount || piX < 0 || piX > 0x50 ? None : liButton;
		}

		private enum KindEnum
		{
			Nothing,
			Save,
			Restore,
			Music,
			Sound,
			OpenDetail,
			OpenQuit,
			OpenGame,
			Close,
			SetDetail,
			QuitYes,
			Slot,
			On,
			Off
		}

		/// <summary>The groups' tables (ICONS.C gameopts_buttongroup to musicsound_buttongroup).</summary>
		private static KindEnum fKind(GroupEnum peGroup, int piButton)
		{
			switch (peGroup)
			{
				case GroupEnum.Game:
					return msGame[piButton];
				case GroupEnum.Detail:
					return piButton >= 2 && piButton <= 5 ? KindEnum.SetDetail : piButton == 6 ? KindEnum.OpenGame : KindEnum.Nothing;
				case GroupEnum.Quit:
					return piButton == 2 ? KindEnum.QuitYes : piButton == 3 ? KindEnum.Close : KindEnum.Nothing;
				case GroupEnum.File:
					return piButton >= 1 && piButton <= 4 ? KindEnum.Slot : piButton == 5 ? KindEnum.Close : KindEnum.Nothing;
				case GroupEnum.MusicSound:
					return piButton == 2 ? KindEnum.On : piButton == 3 ? KindEnum.Off : piButton == 4 ? KindEnum.OpenGame : KindEnum.Nothing;
			}

			return KindEnum.Nothing;
		}

		private static readonly KindEnum[] msGame =
		{
			KindEnum.Save, KindEnum.Restore, KindEnum.Music, KindEnum.Sound, KindEnum.OpenDetail, KindEnum.OpenQuit, KindEnum.Close
		};
	}
}
