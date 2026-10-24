namespace UWDataImport.UWData
{
	/// <summary>
	/// What the rules and their hosts show and play, fire and forget: lines for the message scroll,
	/// sounds at the player, an effect picture on a spot, the screen shake, a flash or the black of
	/// the view window, a cutscene. Nothing here changes the game; a headless check may ignore all
	/// of it (Tools/UWSelfCheck WorldChecks logs it instead).
	///
	/// WHY (cleanup step 3.1, 2026-10-17): the trap, spell, sleep, item-use and vitals hosts each
	/// had their own copy of these bodies - five AddMessage, four AddGeneralMessage, five cutscene
	/// calls, three shakes - with small differences nobody chose (section 4.5 of
	/// Docs/CLEANUP-PLAN.md). The hosts now delegate here; the Unity side is UWPresentation.
	///
	/// THE SILENT SCOPE: a firing without a message channel (the respawn's create-object traps, a
	/// cast without an Interaction) drops its lines and still shakes, flashes and plays - see
	/// UWSilentPresentation. Until now the trap host expressed that by a null Interaction.
	/// </summary>
	public interface IUWPresentation : IUWMessageSink
	{
		/// <summary>False in a silent scope: the lines are dropped, and traps that only talk do
		/// nothing (IUWTrapHost.HasMessages until cleanup step 3.6; the trap rules ask this).</summary>
		bool ShowsMessages { get; }

		// AddMessage, AddGeneralMessage and GetGeneralMessage are IUWMessageSink's (cleanup step
		// 5.13, 2026-10-22); a silent scope answers GetGeneralMessage empty, as the hosts did
		// without an Interaction.

		/// <summary>A sound effect at the player, without a position (UW.EXE's sound at the
		/// avatar).</summary>
		void PlaySoundAtPlayer(int piEffect);

		/// <summary>An effect picture stands on a spot for pfSeconds, its animation starting on
		/// piStartFrame. Nothing when the level has none loaded or the spot is off the map.</summary>
		void ShowEffect(int piEffectObjectId, UWSpot pOAt, float pfSeconds, int piStartFrame = 0);

		/// <summary>The view shakes on the large channel for this many ticks (the quake, Tremor, a
		/// failed sip); a new value overwrites a running one.</summary>
		void ShakeScreen(int piDuration);

		/// <summary>A brief flash of the view window in a palette colour.</summary>
		void FlashWindow(int piPaletteColour, float pfSeconds);

		/// <summary>The view window goes black for a moment - the night without a dream.</summary>
		void ShowNightBlack(float pfSeconds);

		/// <summary>Plays a cutscene. False when nothing can show it (no cutscene player in the
		/// scene).</summary>
		bool PlayCutscene(int piCutscene);

		/// <summary>Asks the music for a theme (UWMusicSelector's numbers) - the night's "Maps &amp;
		/// Legends". Not possible during the fanfare. Since cleanup step 3.6 (2026-10-17).</summary>
		void ChangeMusicTheme(int piTheme);
	}

	/// <summary>
	/// THE SILENT SCOPE of a presentation (cleanup step 3.1, 2026-10-17): the lines for the message
	/// scroll are dropped and GetGeneralMessage answers empty, everything else - sounds, effects,
	/// shake, flash, cutscenes - goes on to the presentation inside. This is what a trap firing
	/// without a message channel always did (UWTrapHost with a null Interaction): its lines were
	/// dropped, while a quake or a cutscene in the same chain still played.
	/// </summary>
	public sealed class UWSilentPresentation : IUWPresentation
	{
		private readonly IUWPresentation mOInner;

		/// <summary>The silent scope of pOInner (which may be null: then nothing plays at all).
		/// Silencing a silent scope gives it back unchanged.</summary>
		public static IUWPresentation Of(IUWPresentation pOInner)
		{
			return pOInner is UWSilentPresentation ? pOInner : new UWSilentPresentation(pOInner);
		}

		private UWSilentPresentation(IUWPresentation pOInner)
		{
			mOInner = pOInner;
		}

		public bool ShowsMessages => false;

		public void AddMessage(string psMessage)
		{
		}

		public void AddGeneralMessage(int piIndex)
		{
		}

		public string GetGeneralMessage(int piIndex)
		{
			return string.Empty;
		}

		public void PlaySoundAtPlayer(int piEffect)
		{
			mOInner?.PlaySoundAtPlayer(piEffect);
		}

		public void ShowEffect(int piEffectObjectId, UWSpot pOAt, float pfSeconds, int piStartFrame = 0)
		{
			mOInner?.ShowEffect(piEffectObjectId, pOAt, pfSeconds, piStartFrame);
		}

		public void ShakeScreen(int piDuration)
		{
			mOInner?.ShakeScreen(piDuration);
		}

		public void FlashWindow(int piPaletteColour, float pfSeconds)
		{
			mOInner?.FlashWindow(piPaletteColour, pfSeconds);
		}

		public void ShowNightBlack(float pfSeconds)
		{
			mOInner?.ShowNightBlack(pfSeconds);
		}

		public bool PlayCutscene(int piCutscene)
		{
			return mOInner != null && mOInner.PlayCutscene(piCutscene);
		}

		public void ChangeMusicTheme(int piTheme)
		{
			mOInner?.ChangeMusicTheme(piTheme);
		}
	}
}
