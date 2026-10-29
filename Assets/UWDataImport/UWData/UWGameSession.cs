using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// ONE RUNNING GAME (cleanup step 2.7, 2026-10-14): everything that belongs to the game being
	/// played rather than to the installation - quest flags, game variables, the story flags of
	/// PLAYER.DAT, the level the carried objects live in, the creatures' clock and kin alarm, the
	/// quirks the original keeps for a whole run (the lava flag, the cast pause), the light colours
	/// worked out so far and the diagnostics (trap log, writer warnings); since step 2.8 also the
	/// one random generator every rule draws from (UWSessionRandom), since phase 10 the rule set
	/// (UWRules, with the player's rules.json applied at the start).
	///
	/// WHY: until then each of these was a static on its own class. Starting a new game or loading
	/// one had to clear every one of them by hand, and three were forgotten for a while (B21 of the
	/// cleanup plan: the lava quirk, the cast pause and the light-colour cache leaked across New Game
	/// and Load until 2026-10-11). A new game now gets a new session, so nothing of the previous one
	/// can survive by construction, and two sessions share nothing (the self-check proves it).
	///
	/// TRANSITIONAL: the old static classes (UWQuestFlags, UWGameVariables, UWGameFlags,
	/// UWCarriedSlots.Level, UWTrapLog, UWLevelWriter.Warnings, UWScene.CritterClock, ...) forward to
	/// Current, so their callers keep working while the rules move to taking the session as a
	/// parameter, domain by domain (plan section 7.1). Current goes last.
	/// </summary>
	public sealed class UWGameSession : ICritterServices
	{
		private static UWGameSession msCurrent;

		/// <summary>
		/// The session the forwarding statics read. Never null: without a started game (a tool, a
		/// self-check group that needs no data) it is an empty session of its own.
		/// </summary>
		public static UWGameSession Current
		{
			get { return msCurrent ?? (msCurrent = new UWGameSession(null)); }
		}

		/// <summary>A new game or a loaded one: a fresh session becomes the current one.</summary>
		public static UWGameSession Begin(UWGameData pOData)
		{
			msCurrent = new UWGameSession(pOData);

			return msCurrent;
		}

		/// <summary>Makes an existing session the current one (null starts an empty one on the
		/// next read) - for a host that keeps several, such as the self-check.</summary>
		public static void MakeCurrent(UWGameSession pOSession)
		{
			msCurrent = pOSession;
		}

		/// <summary>A session whose generator is seeded from the clock, as a game's is.</summary>
		public UWGameSession(UWGameData pOData)
			: this(pOData, UWSessionRandom.ClockSeed())
		{
		}

		/// <summary>A session with a given seed - a test or a replay.</summary>
		public UWGameSession(UWGameData pOData, int piSeed)
		{
			Data = pOData;
			Random = new UWSessionRandom(piSeed);

			// The second game's rule set since phase 2 step 4 (per user, 2026-10-29): the first game's
			// numbers with the second game's motion; Docs/UW2-PLAN.md lists the domains phase 4 holds to
			// the second game's (UWGameProfileUw2.CreateOriginalRules). Null until then (step 2).
			Rules = UWRules.CreateOriginal(Game);
		}

		/// <summary>The installation this game runs on; null in a session without data.</summary>
		public UWGameData Data { get; }

		/// <summary>The game being played (Docs/UW2-PLAN.md section 4.1): its data's profile, the first
		/// game's in a session without data.</summary>
		public IUWGameProfile Game
		{
			get { return Data != null ? Data.Profile : UWGameProfiles.Uw1; }
		}

		/// <summary>The one generator of this game (cleanup step 2.8; UWRandom forwards here).</summary>
		public UWSessionRandom Random { get; }

		/// <summary>The rule set of this game (cleanup phase 10, 2026-10-25): the original's numbers
		/// unless the host applied a rules.json at the session's start (ApplyRulesFile). UWRules.Current
		/// forwards here.</summary>
		public UWRules Rules { get; private set; }

		/// <summary>What the last ApplyRulesFile found; null before one (the original's rules).</summary>
		public UWRulesReport RulesReport { get; private set; }

		/// <summary>Puts a rule set in place of this session's (null: the original's) - for a test or a
		/// host that builds the set itself.</summary>
		public void UseRules(UWRules pORules)
		{
			Rules = pORules ?? UWRules.CreateOriginal(Game);
		}

		/// <summary>
		/// The player's rule set for this game: the original's, with the keys of the file at psPath
		/// applied (UWRulesJson.ApplyFile). No file leaves the original's; a malformed one leaves them
		/// too and says why in the report. Starts from the original's every time, so a key taken out of
		/// the file goes back to its default with the next session.
		/// </summary>
		public UWRulesReport ApplyRulesFile(string psPath)
		{
			UWRules lORules = UWRules.CreateOriginal(Game);

			RulesReport = UWRulesJson.ApplyFile(lORules, psPath);
			Rules = lORules;

			return RulesReport;
		}

		/// <summary>The world of this game: levels, LEV.ARK, map notes, the level the player is on.</summary>
		public UWSessionWorld World { get; } = new UWSessionWorld();

		/// <summary>The character as PLAYER.DAT holds it (a loaded save game's, the data folder's
		/// template, or a newly created one) - the former DataImport.InitialPlayer. Still mutated as
		/// live state by a few hosts (the rune bag, the incense counter); the player's running
		/// numbers live on the Unity side until phase 4 moves them here.</summary>
		public UWPlayerData Player { get; internal set; }

		/// <summary>The second game's PLAYER.DAT as a loaded save holds it (UW2 plan phase 2 step 6), null
		/// without one: its record's position is where the walk comes back to, and saving writes it back
		/// with the new one (UWPlayerRecordUw2).</summary>
		public UWPlayerFile PlayerFileUw2 { get; internal set; }

		/// <summary>The memory of the conversations (BGLOBALS.DAT) - the former
		/// DataImport.ConversationGlobals.</summary>
		public UWConversationGlobals ConversationMemory { get; internal set; }

		/// <summary>Puts a newly created character in place of the loaded one (character creation),
		/// with the item pictures of this installation.</summary>
		public void ReplacePlayer(UWPlayerData pOPlayer)
		{
			if (pOPlayer == null)
				return;

			Player = pOPlayer;

			if (Data != null)
				Player.AssignTextures(Data.Textures);
		}

		/// <summary>The quest flags (UWQuestFlags forwards here).</summary>
		public UWSessionNumbers Quests { get; } = new UWSessionNumbers(UWSessionNumbers.NoMask);

		/// <summary>The game variables, six bits each (UWGameVariables forwards here).</summary>
		public UWSessionNumbers Variables { get; } = new UWSessionNumbers(UWGameVariables.ValueMask);

		/// <summary>The story flags of PLAYER.DAT's flag bytes (UWGameFlags forwards here).</summary>
		public UWStoryFlags Story { get; } = new UWStoryFlags();

		/// <summary>The 16-slot clock of the level's creatures - one per level, see
		/// ResetCritterServices.</summary>
		public UWCritterClock CritterClock { get; private set; } = new UWCritterClock();

		/// <summary>The kin alarm of the level (UWCritterBrain.OnDamaged writes it).</summary>
		public UWCritterAlarm CritterAlarm { get; private set; } = new UWCritterAlarm();

		/// <summary>The sixteen stored creature paths of the level (UWCritterPathSlots, since
		/// 2026-10-15) - cleared with every level build, as init_level_creature_stuff does.</summary>
		public UWCritterPathSlots CritterPaths { get; } = new UWCritterPathSlots();

		/// <summary>What the world does later - after clock units, after a conversation, per frame
		/// of a sequence, per player tick (cleanup step 3.4, 2026-10-17). Per session and not per
		/// level, so a level change does not drop it (the void launch ends with one) and a new game
		/// does.</summary>
		public UWWorldScheduler Schedule { get; } = new UWWorldScheduler();

		/// <summary>The launch into the void (step 3.4; its running state was the static
		/// UWEndgame.mbLaunching).</summary>
		public UWEndgameSequence Endgame { get; } = new UWEndgameSequence();

		/// <summary>What the original keeps for a whole run and we keep for a session.</summary>
		public UWSessionQuirks Quirks { get; } = new UWSessionQuirks();

		/// <summary>Records for the debug overlay and the save writer's report.</summary>
		public UWSessionDiagnostics Diagnostics { get; } = new UWSessionDiagnostics();

		/// <summary>The light colours worked out from the object images so far (UWObjectLightColour).
		/// Kept with the session so a new game starts with an empty cache, as phase 1 decided (B21).</summary>
		public UWObjectLightColour.Cache LightColours { get; } = new UWObjectLightColour.Cache();

		/// <summary>A fresh clock and alarm for a newly built level, as init_level_creature_stuff
		/// clears crithit and typehit on entering one. Loading a save then brings the save's alarm
		/// back (UWCritterAlarm.RestoreFrom, since 2026-10-17); before, the reset also covered
		/// loading, after a rat attacked at once on 2026-09-12 - which the original does as well.</summary>
		public void ResetCritterServices()
		{
			CritterClock = new UWCritterClock();
			CritterAlarm = new UWCritterAlarm();
			CritterPaths.Clear();
		}

		// The creature minds' services (ICritterServices, cleanup step 4.13, 2026-10-19): the brain
		// draws, reads the clock, writes the alarm and stores its path through these, read anew at
		// every call, so a level build's fresh clock and alarm (ResetCritterServices) reach it. Their
		// rule set is the public Rules (since cleanup phase 10 wave 4, 2026-10-26).

		IUWRandom ICritterServices.Random => Random;

		long ICritterServices.Clock => CritterClock.Clock;

		UWCritterAlarm ICritterServices.Alarm => CritterAlarm;

		UWCritterPathSlots ICritterServices.PathSlots => CritterPaths;

		/// <summary>
		/// Takes over quest flags, game variables and the story flags from a save game's or a new
		/// character's PLAYER.DAT (null leaves everything cleared). Without this every loaded game
		/// starts with empty puzzles: the lever positions come from LEV.ARK, but the variables behind
		/// them were at zero (per user, 2026-09-07, at the three levers on level 5, tile 6/29).
		/// Only values that are set are taken, so All lists what the save game holds.
		/// </summary>
		public void SeedFrom(UWPlayerData pOPlayer)
		{
			Quests.Clear();
			Variables.Clear();
			Story.SeedFrom(pOPlayer);

			if (pOPlayer == null)
				return;

			for (int liAt = 0; liAt < pOPlayer.QuestFlags; liAt++)
			{
				int liValue = pOPlayer.GetQuestFlag(liAt);

				if (liValue != 0)
					Quests.Set(liAt, liValue);
			}

			for (int liAt = 0; liAt < pOPlayer.GameVariables; liAt++)
			{
				int liValue = pOPlayer.GetGameVariable(liAt);

				if (liValue != 0)
					Variables.Set(liAt, liValue);
			}
		}
	}

	/// <summary>
	/// The original's run-long quirks and the deviation switches the host pushes, kept per session
	/// (cleanup step 2.7, 2026-10-14; before, statics on the classes that use them).
	/// </summary>
	public sealed class UWSessionQuirks
	{
		/// <summary>The lava-proof quirk per handler block (UWCreatureMotion.Kind): once set, the
		/// block keeps the lava flag in its callback and path cost masks for the rest of the
		/// session - see UWCreatureMotion.LavaFlagKept.</summary>
		public readonly bool[] LavaFlagKept = new bool[UWCreatureMotion.KindCount];

		/// <summary>The game time of the last cast that set the pause, in PIT ticks, and the pause it
		/// set (UWSpellCasting; try_cast's lstime and spell_delay, both zero at the start as in the
		/// original). Real seconds until cleanup phase 10 (2026-10-26).</summary>
		public long LastCastTicks;

		public int CastDelayTicks;

		// The switch for the original's dead resistance nibble lived here until cleanup phase 10
		// (2026-10-26); it is the rule set's combat.resistBlowsAddsArmour now - see
		// UWArmourProtection.ResistBlowsAddsArmour.
	}

	/// <summary>
	/// What the session records for the developer: the traps that fired last (the overlay) and the
	/// level writer's warnings (cleanup step 2.7, 2026-10-14; before, statics on UWTrapLog and
	/// UWLevelWriter).
	/// </summary>
	public sealed class UWSessionDiagnostics
	{
		/// <summary>The most recent fired traps, oldest first (UWTrapLog).</summary>
		public readonly List<UWTrapLog.Entry> TrapLog = new List<UWTrapLog.Entry>();

		/// <summary>The trap kind's extra note for the line being recorded (UWTrapLog.Detail): a
		/// side channel cleared before every trap and read when the line is written.</summary>
		public string TrapDetail;

		/// <summary>What the level writer could not write as asked (UWLevelWriter.Warnings).</summary>
		public readonly List<string> WriterWarnings = new List<string>();
	}
}
