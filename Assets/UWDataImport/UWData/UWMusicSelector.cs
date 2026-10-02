namespace UWDataImport.UWData
{
	/// <summary>The player of the pieces, as the theme logic sees it. The Unity side is
	/// UWMusic over UWAudioEngine.</summary>
	public interface IUWMusicOutput
	{
		bool IsReady { get; }

		bool IsPlaying { get; }

		/// <summary>Plays the theme's piece from the start; a theme without a piece stops
		/// the music.</summary>
		void Play(int piTheme);

		void Stop();
	}

	/// <summary>
	/// Which music plays when - the theme logic of the original (reference: xmimusic.cs,
	/// uw1 branch, from the disassembly). Engine-free since 2026-09-18 (P3 of the engine
	/// separation), out of UWMusic, which keeps the audio engine and passes the time in.
	///
	/// THE THEMES, number as in the game, file "AW" plus number in octal (8 becomes AW10):
	///
	///    1  Introduction   main menu
	///  2-4  Dark Abyss, Descent, Wanderer   the levels, chosen at random
	///  5-7  Battlefield, Combat, Injured    combat: player winning, even, player losing
	///    8  Armed          weapon drawn
	///    9  Victory        fanfare after a victory
	///   10  Death
	///   11  Fleeing
	///   13  Maps &amp; Legends   map, conversation, sleep
	///
	/// A theme is REQUESTED (ChangeTheme, PickLevelTheme), it is LOADED in Refresh, which
	/// runs every frame: a combat theme holds for ten seconds after the last blow, between
	/// two combat themes there are at least eight, the fanfare is not interrupted. When a
	/// piece ends, see Refresh: a requested theme plays again, otherwise a random level theme,
	/// with the weapon drawn always Armed.
	/// </summary>
	public sealed class UWMusicSelector
	{
		public const int IntroTheme = 1;

		public const int FirstLevelTheme = 2;

		public const int LastLevelTheme = 4;

		public const int FirstCombatTheme = 5;

		public const int LastCombatTheme = 7;

		public const int ArmedTheme = 8;

		public const int FanfareTheme = 9;

		public const int DeathTheme = 0xA;

		public const int FleeingTheme = 0xB;

		public const int MapsAndLegendsTheme = 0xD;

		/// <summary>Combat themes: 5 the player is winning, 6 undecided, 7 the player is losing.</summary>
		public const int WinningCombatTheme = 5;

		public const int EvenCombatTheme = 6;

		public const int LosingCombatTheme = 7;

		/// <summary>0xA00 ticks of the PIT counter (256 Hz) after the last blow.</summary>
		private const float CombatTimeout = 10f;

		/// <summary>0x800 ticks between two combat themes.</summary>
		private const float CombatChangeMinimum = 8f;

		private readonly IUWMusicOutput mIOutput;

		public int CurrentTheme { get; private set; }

		public int NewTheme { get; private set; }

		private float mfCombatTimer = -1000f;

		private float mfLastCombatChange;

		public UWMusicSelector(IUWMusicOutput pIOutput)
		{
			mIOutput = pIOutput;
		}

		/// <summary>At the start of a scene. If a piece from the previous one is still playing - the sound
		/// survives the restart -, it stays, theme included.</summary>
		public void Reset()
		{
			if (mIOutput == null || !mIOutput.IsPlaying)
			{
				CurrentTheme = 0;
				NewTheme = 0;
			}

			mfCombatTimer = -1000f;
			mfLastCombatChange = 0f;
		}

		/// <summary>Request a theme (ChangeThemeMusic_seg014_15BA: it only stores the number). A
		/// request during the fanfare waits - Refresh leaves the fanfare alone while it plays and
		/// takes the request when it has ended. Until 2026-10-02 ours dropped such a request (from
		/// the reference).</summary>
		public void ChangeTheme(int piTheme)
		{
			NewTheme = piTheme;
		}

		/// <summary>One of the three level themes, at random.</summary>
		public void PickLevelTheme()
		{
			NewTheme = FirstLevelTheme + UWRandom.Next(3);
		}

		/// <summary>After map, conversation or weapon put away: Armed with a drawn weapon, otherwise
		/// a level theme.</summary>
		public void ResumeAfterInterruption(bool pbWeaponDrawn)
		{
			if (pbWeaponDrawn)
				ChangeTheme(ArmedTheme);
			else
				PickLevelTheme();
		}

		/// <summary>
		/// THE WEAPON IS DRAWN (SetInteractionMode_seg024_24DC_13D5 and DrawWeapon_seg024_1531,
		/// read 2026-10-02; per user on the original: combat music that runs after an attack
		/// goes on when the player draws, ours played the Armed theme for a moment): Armed only
		/// when the piece playing is no combat theme (GetMusicFileNo outside 5..7).
		/// </summary>
		public void OnWeaponDrawn()
		{
			if (!fIsCombat(CurrentTheme))
				ChangeTheme(ArmedTheme);
		}

		/// <summary>Combat mode left by the player (SetInteractionMode, label 14E8): a level theme
		/// only while Armed is the piece playing - combat music goes on. PutAwayWeapon_seg024_15C0
		/// (the weapon taken away, e.g. on starting to swim) picks one in any case, see
		/// PickLevelTheme.</summary>
		public void OnCombatModeLeft()
		{
			if (CurrentTheme == ArmedTheme)
				PickLevelTheme();
		}

		/// <summary>A blow has been struck - the combat theme keeps playing.</summary>
		public void MarkCombat(float pfNow)
		{
			mfCombatTimer = pfNow;
		}

		/// <summary>The player was hit: if still doing well, the undecided combat theme,
		/// otherwise the losing one (reference: damage.cs).</summary>
		public void OnPlayerDamaged(float pfCurrentHealth, float pfMaxHealth, float pfNow)
		{
			int liRatio = ((int)pfCurrentHealth << 6) / ((int)pfMaxHealth + 1);

			ChangeTheme(liRatio >= 0x10 ? EvenCombatTheme : LosingCombatTheme);
			MarkCombat(pfNow);
		}

		// A CREATURE'S SIDE OF THE COMBAT MUSIC is the brain's since 2026-09-20 (ICritterHost.
		// PlayMusic, UWCritterBrain): theme 5 or 6 by hp * 64 / (vitality + 1) against 16 for every
		// damage the player deals (the brain's DamageNPC, cited there); theme 6 at a swing's frame 0
		// against the player. Two functions of the reference stood here until 2026-09-23 - the
		// one after a melee hit even ran AFTER the brain and overwrote its theme with a float
		// fraction against a quarter (deviation 53).

		private static bool fIsLevel(int piTheme)
		{
			return piTheme >= FirstLevelTheme && piTheme <= LastLevelTheme;
		}

		private static bool fIsCombat(int piTheme)
		{
			return piTheme >= FirstCombatTheme && piTheme <= LastCombatTheme;
		}

		/// <summary>Every frame: reconcile the request with what is playing (reference: RefreshMusic).</summary>
		/// <param name="pfNow">The time in seconds - the host's clock.</param>
		public void Refresh(bool pbWeaponDrawn, float pfNow)
		{
			if (mIOutput == null || !mIOutput.IsReady)
				return;

			bool lbPlaying = mIOutput.IsPlaying;

			if ((CurrentTheme == FanfareTheme || CurrentTheme == FleeingTheme) && lbPlaying)
				return;

			if (fIsCombat(CurrentTheme) && pfNow > mfCombatTimer + CombatTimeout)
			{
				if (pbWeaponDrawn)
					NewTheme = ArmedTheme;
				else
					PickLevelTheme();
			}

			// A PIECE HAS ENDED (RefreshMusic_seg014_160D from label 1746, read 2026-10-02; per user:
			// after the victory fanfare with combat mode ended no level theme came). Nothing
			// requested: a random level theme; a request that equals the piece just ended stays and
			// plays again. With the weapon drawn it is Armed either way. The change timer is cleared.
			// Until then ours went silent after every theme but level, combat and Armed (taken from
			// the reference) - after the fanfare only a sheathing's level theme request had filled
			// the gap. Not built: with the mouse mode 1 (dseg_5c99_565E) a level theme or a theme
			// flagged 0 in the table of seg061 picks a new level theme even over a request.
			if (NewTheme == 0 || NewTheme == CurrentTheme)
			{
				if (lbPlaying)
					return;

				if (NewTheme == 0)
					PickLevelTheme();

				if (pbWeaponDrawn)
					NewTheme = ArmedTheme;

				fLoadAgain(NewTheme);
				mfLastCombatChange = 0f;

				return;
			}

			if (fIsCombat(CurrentTheme) && fIsCombat(NewTheme))
			{
				if (pfNow <= mfLastCombatChange + CombatChangeMinimum)
					NewTheme = CurrentTheme;
				else
				{
					mfLastCombatChange = pfNow;
					fLoad(NewTheme, lbPlaying);
				}
			}
			else
			{
				fLoad(NewTheme, lbPlaying);
			}

			if (fIsCombat(NewTheme))
				mfLastCombatChange = pfNow;
		}

		/// <summary>The piece that ended - or another - from its start (LoadXMIFILE).</summary>
		private void fLoadAgain(int piTheme)
		{
			CurrentTheme = piTheme;
			NewTheme = 0;

			if (piTheme <= 0)
				mIOutput.Stop();
			else
				mIOutput.Play(piTheme);
		}

		/// <summary>
		/// Loads a theme. The same theme again only if nothing is playing any more - the
		/// reference leaves it in that case, which would mean silence where the original repeats.
		/// </summary>
		private void fLoad(int piTheme, bool pbPlaying)
		{
			if (CurrentTheme == piTheme && (pbPlaying || piTheme == 0))
			{
				NewTheme = 0;
				return;
			}

			CurrentTheme = piTheme;
			NewTheme = 0;

			if (piTheme <= 0)
			{
				mIOutput.Stop();
				return;
			}

			mIOutput.Play(piTheme);
		}

		/// <summary>The file name part of a theme: "AW" plus the number in octal (8 becomes 10).</summary>
		public static string GetFileNumber(int piTheme)
		{
			return ((char)('0' + (piTheme >> 3))).ToString() + (char)('0' + (piTheme & 7));
		}
	}
}
