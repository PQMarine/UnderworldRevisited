namespace UWDataImport.UWData
{
	/// <summary>
	/// WHICH MUSIC PLAYS WHEN IN THE SECOND GAME (UW2 plan phase 5 part A): uw2 sound/SOUND.C's set_new_music,
	/// set_random_walking_music, load_new_music, loop_music_maybe and change_music_maybe, engine-free. UWAudioService
	/// holds it beside the first game's UWMusicSelector, whose rules differ in more than the numbers.
	///
	/// THE THEMES (include/sound.h:45-54), the number in octal naming SOUND\UWAnn.XMI (UWRnn on the MT-32):
	///    1  the main menu            2  the foe nearly dead    3  combat         4  the player badly hurt
	///    5  the weapon drawn         6  victory                7  death          8-15  walking, by world
	///   0x18 (UWA30) the intro, left to repeat; 1, 6 and 7 are followed by 10 (loop_music_maybe).
	///
	/// THE WORLDS: (level - 1) / 8, nine of them, each with three walking themes (walking_music); a new world always
	/// starts with its first. A request (SetNewMusic) is dropped while the victory theme plays. ChangeMusicMaybe runs
	/// in the walk, LoopMusicMaybe in the screens that loop on their own (the menu, the creation, a conversation, the
	/// map). The time is the host's, in seconds (GAME_TIME counts 1/256 s: 0xA00 and 0x800 are ten and eight).
	/// </summary>
	public sealed class UWMusicSelectorUw2
	{
		public const int MenuTheme = 1;

		public const int FoeHurtTheme = 2;

		public const int CombatTheme = 3;

		public const int DangerTheme = 4;

		public const int ArmedTheme = 5;

		public const int VictoryTheme = 6;

		public const int DeathTheme = 7;

		public const int FirstWalkingTheme = 8;

		public const int LastWalkingTheme = 15;

		public const int IntroTheme = 0x18;

		/// <summary>What follows the menu, victory and death themes when they end (loop_music_maybe); also the theme of
		/// the gem, the jail and the waking from a dream (SKILLS.C:696, :717, WORLDEV.C:2291).</summary>
		public const int FollowTheme = 10;

		/// <summary>set_random_walking_music's picks: the world's first theme, a random one, the one chosen unless the
		/// world changed.</summary>
		public const int PickFirst = 0;

		public const int PickRandom = -1;

		public const int PickKeep = -2;

		/// <summary>0xA00 ticks after the last combat a combat theme gives way.</summary>
		public const float CombatTimeout = 10f;

		/// <summary>0x800 ticks between two combat themes.</summary>
		public const float CombatChangeMinimum = 8f;

		/// <summary>walking_music (SOUND.C:175), by world and pick.</summary>
		private static readonly int[,] miWalkingMusic =
		{
			{ 0x0A, 0x0C, 0x0E }, { 0x09, 0x0A, 0x0F }, { 0x0B, 0x0D, 0x0A },
			{ 0x0C, 0x0D, 0x09 }, { 0x08, 0x0B, 0x0E }, { 0x0D, 0x08, 0x0F },
			{ 0x0E, 0x09, 0x08 }, { 0x0F, 0x0B, 0x0A }, { 0x08, 0x0C, 0x09 }
		};

		public const int WorldCount = 9;

		private readonly IUWMusicOutput mIOutput;

		/// <summary>curmusic: the theme loaded.</summary>
		public int CurrentTheme { get; private set; }

		/// <summary>newmusic: the theme wanted next, 0 for none.</summary>
		public int NewTheme { get; private set; }

		/// <summary>music_world: the world the walking theme was last chosen for, 0xFF at the start.</summary>
		public int MusicWorld { get; private set; } = 0xFF;

		private float mfLastCombat = -1000f;

		/// <summary>theme_changed: when a combat theme last began.</summary>
		private float mfThemeChanged;

		public UWMusicSelectorUw2(IUWMusicOutput pIOutput)
		{
			mIOutput = pIOutput;
		}

		/// <summary>The walking theme of a world and pick.</summary>
		public static int WalkingTheme(int piWorld, int piPick)
		{
			return miWalkingMusic[piWorld, piPick];
		}

		/// <summary>The world of a level, (level - 1) / 8, kept inside the table.</summary>
		public static int WorldOf(int piLevel)
		{
			int liWorld = (piLevel - 1) / 8;

			return liWorld < 0 ? 0 : liWorld >= WorldCount ? WorldCount - 1 : liWorld;
		}

		public static bool IsWalking(int piTheme)
		{
			return piTheme >= FirstWalkingTheme && piTheme <= LastWalkingTheme;
		}

		public static bool IsCombat(int piTheme)
		{
			return piTheme >= FoeHurtTheme && piTheme <= DangerTheme;
		}

		/// <summary>The quarter of the hit points: hp * 64 / (most + 1) under it (AI.C:1515, :1523).</summary>
		public const int HurtRatio = 0x10;

		/// <summary>The theme of a damage to the player by someone (damage_critter, AI.C:1522): the danger theme below a
		/// quarter of the hit points (playerdat->avghit, the most), else the combat theme.</summary>
		public static int PlayerDamagedTheme(int piCurrentHealth, int piMaxHealth)
		{
			return (piCurrentHealth << 6) / (piMaxHealth + 1) < HurtRatio ? DangerTheme : CombatTheme;
		}

		/// <summary>The output playing nothing counts as a piece over (music_over without a sequence).</summary>
		private bool fMusicOver => !mIOutput.IsPlaying;

		/// <summary>set_new_music: a theme asked for next, unless the victory theme is playing.</summary>
		public void SetNewMusic(int piTheme)
		{
			if (CurrentTheme != VictoryTheme)
				NewTheme = piTheme;
		}

		/// <summary>set_random_walking_music: the walking theme of the player's world - PickFirst, PickRandom or
		/// PickKeep (nothing unless the world changed); a new world always takes its first. Not held back by the victory
		/// theme (it writes newmusic itself).</summary>
		public void SetRandomWalkingMusic(int piPick, int piLevel)
		{
			int liWorld = WorldOf(piLevel);

			if (liWorld != MusicWorld)
			{
				MusicWorld = liWorld;
				piPick = PickFirst;
			}
			else if (piPick == PickKeep)
				return;

			if (piPick == PickRandom)
				piPick = UWRandom.Next(3);

			NewTheme = miWalkingMusic[liWorld, piPick];
		}

		/// <summary>load_new_music with start: the theme (0: the current one) from its beginning; the request is
		/// cleared.</summary>
		public void LoadNewMusic(int piTheme)
		{
			if (!mIOutput.IsReady)
				return;

			if (piTheme == 0)
				piTheme = CurrentTheme;

			CurrentTheme = piTheme;
			NewTheme = 0;

			if (piTheme <= 0)
				mIOutput.Stop();
			else
				mIOutput.Play(piTheme);
		}

		/// <summary>The last combat (lastcombattime, AI.C:1125, :1520, :1527).</summary>
		public void MarkCombat(float pfNow)
		{
			mfLastCombat = pfNow;
		}

		/// <summary>loop_music_maybe: a piece over starts again, the menu, victory and death themes give way to
		/// FollowTheme.</summary>
		public void LoopMusicMaybe()
		{
			// Nothing loaded yet (before the menu's theme): nothing to start again.
			if (!mIOutput.IsReady || !fMusicOver || CurrentTheme == 0)
				return;

			int liTheme = CurrentTheme;

			if (liTheme == MenuTheme || liTheme == VictoryTheme || liTheme == DeathTheme)
				liTheme = FollowTheme;

			LoadNewMusic(liTheme);
		}

		/// <summary>
		/// change_music_maybe (SOUND.C:1581): the victory theme plays to its end; ten seconds after the last combat a
		/// combat theme gives way to the armed theme (the weapon drawn) or a walking one; a request starts at once, but
		/// one combat theme replaces another at most every eight seconds (a refused one counts as a change, as there);
		/// a piece over without a request: on the game screen a walking theme is picked unless it was a combat theme or
		/// the intro, the armed theme follows a drawn weapon unless it was combat, and a walking or combat theme or the
		/// intro repeats.
		/// </summary>
		/// <param name="pbDrawn">player->drawn.</param>
		/// <param name="pbGameScreen">scrmode 1, the walk's own screen.</param>
		public void ChangeMusicMaybe(bool pbDrawn, bool pbGameScreen, int piLevel, float pfNow)
		{
			if (!mIOutput.IsReady)
				return;

			if (CurrentTheme == VictoryTheme && !fMusicOver)
				return;

			if (IsCombat(CurrentTheme) && pfNow > mfLastCombat + CombatTimeout)
			{
				if (pbDrawn)
					NewTheme = ArmedTheme;
				else
					SetRandomWalkingMusic(PickRandom, piLevel);
			}

			if (NewTheme != 0 && NewTheme != CurrentTheme)
			{
				if (IsCombat(CurrentTheme) && IsCombat(NewTheme))
				{
					if (pfNow > mfThemeChanged + CombatChangeMinimum)
					{
						LoadNewMusic(NewTheme);
						mfThemeChanged = pfNow;
					}
					else
						NewTheme = CurrentTheme;
				}
				else
					LoadNewMusic(NewTheme);

				// After a load the request is 0; only a refused combat change leaves one here.
				if (IsCombat(NewTheme))
					mfThemeChanged = pfNow;
			}
			else if (fMusicOver)
			{
				bool lbKept = IsWalking(CurrentTheme) || IsCombat(CurrentTheme) || CurrentTheme == IntroTheme;

				if ((!lbKept || IsWalking(CurrentTheme)) && pbGameScreen)
					SetRandomWalkingMusic(PickRandom, piLevel);

				if (pbDrawn && !IsCombat(CurrentTheme))
					NewTheme = ArmedTheme;
				else if (lbKept && NewTheme == 0)
					NewTheme = CurrentTheme;

				LoadNewMusic(NewTheme);
				mfThemeChanged = IsCombat(NewTheme) ? pfNow : 0f;
			}
		}

		/// <summary>The weapon drawn (pick_fightmode, the fight button; INTERACT.C:683, :713): the armed theme unless
		/// a combat theme plays.</summary>
		public void OnWeaponDrawn()
		{
			if (!IsCombat(CurrentTheme))
				SetNewMusic(ArmedTheme);
		}

		/// <summary>The weapon sheathed (punt_fightmode, INTERACT.C:726): a walking theme by chance.</summary>
		public void OnWeaponSheathed(int piLevel)
		{
			SetRandomWalkingMusic(PickRandom, piLevel);
		}

		/// <summary>Turned on (turn_music, SOUND.C:1014): with nothing loaded, a random walking theme.</summary>
		public void TurnOn(int piLevel)
		{
			if (CurrentTheme != 0)
			{
				LoadNewMusic(CurrentTheme);
				return;
			}

			SetRandomWalkingMusic(PickRandom, piLevel);
			LoadNewMusic(NewTheme);
		}
	}
}
