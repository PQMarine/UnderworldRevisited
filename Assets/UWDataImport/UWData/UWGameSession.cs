using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// ONE RUNNING GAME (cleanup step 2.7, 2026-10-14): everything that belongs to the game being
	/// played rather than to the installation - quest flags, game variables, the story flags of
	/// PLAYER.DAT, the level the carried objects live in, the creatures' clock and kin alarm, the
	/// quirks the original keeps for a whole run (the lava flag, the cast pause), the light colours
	/// worked out so far and the diagnostics (trap log, writer warnings); since step 2.8 also the
	/// one random generator every rule draws from (UWSessionRandom).
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
	public sealed class UWGameSession
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
		}

		/// <summary>The installation this game runs on; null in a session without data.</summary>
		public UWGameData Data { get; }

		/// <summary>The one generator of this game (cleanup step 2.8; UWRandom forwards here).</summary>
		public UWSessionRandom Random { get; }

		/// <summary>The world of this game: levels, LEV.ARK, map notes, the level the player is on.</summary>
		public UWSessionWorld World { get; } = new UWSessionWorld();

		/// <summary>The character as PLAYER.DAT holds it (a loaded save game's, the data folder's
		/// template, or a newly created one) - the former DataImport.InitialPlayer. Still mutated as
		/// live state by a few hosts (the rune bag, the incense counter); the player's running
		/// numbers live on the Unity side until phase 4 moves them here.</summary>
		public UWPlayerData Player { get; internal set; }

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
		/// <summary>The lava-proof quirk per handler block (UWCreatureMotion.Kind): once dropped,
		/// every creature of that kind walks over a lava edge for the rest of the session - see
		/// UWCreatureMotion.LavaFlagDropped.</summary>
		public readonly bool[] LavaFlagDropped = new bool[UWCreatureMotion.KindCount];

		/// <summary>The real-time second of the last cast that set the pause, and the pause it set
		/// (UWSpellCasting, try_cast's spell_delay).</summary>
		public double LastCastSeconds = double.NegativeInfinity;

		public double CastDelaySeconds;

		/// <summary>The switch for the original's dead resistance nibble (UWSettings), pushed by the
		/// host before every status pass - see UWArmourProtection.ResistBlowsAddsArmour. A rule
		/// switch rather than a quirk; it moves to the rule set (plan section 6) when that lands.</summary>
		public bool ResistBlowsAddsArmour;
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
