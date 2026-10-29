namespace UWDataImport.UWData
{
	/// <summary>
	/// ONE GAME'S PROFILE (Docs/UW2-PLAN.md section 4.1, phase 0 step 1, 2026-10-28): what is one
	/// game's own and not a rule - its name, where it lies in GOG's game.gog, the program that
	/// identifies its build, the folders it uses. The game data is built for one profile
	/// (UWGameData.Profile) and the session reaches it as UWGameSession.Game. Later steps add the
	/// formats, the rule variants, the registrations and the screens (sections 4.2 to 4.4).
	/// Profiles are immutable; one instance per game (UWGameProfiles).
	/// </summary>
	public interface IUWGameProfile
	{
		/// <summary>Which game.</summary>
		UWGame Game { get; }

		/// <summary>The game's title as players know it.</summary>
		string Title { get; }

		/// <summary>The game's folder in GOG's game.gog image ("UW" for the first game).</summary>
		string ImageFolder { get; }

		/// <summary>The game's program in that folder, which tells the supported build.</summary>
		string Executable { get; }

		/// <summary>The size and SHA-1 of the supported program (the GOG build).</summary>
		long ExecutableSize { get; }

		string ExecutableSha1 { get; }

		/// <summary>The folder the game's files are extracted into, under the extraction cache.</summary>
		string CacheFolder { get; }

		/// <summary>The installation's folder holding the game's save games (SAVE1 to SAVE4).</summary>
		string SaveFolder { get; }

		/// <summary>The game's own folder beside settings.json (UWGameFolder: rules.json, the music
		/// recordings).</summary>
		string GameFolder { get; }

		/// <summary>Whether the port builds a game of it (phase 0 step 5): the first game's yes; the
		/// second's no until its formats and rules are there (phases 1 to 4) - the level loader then
		/// shows its main menu screen only.</summary>
		bool IsPlayable { get; }

		/// <summary>The layout of the game's archives, LEV.ARK and CNV.ARK among them (UWArk; phase 1
		/// step 1).</summary>
		UWArk.LayoutEnum ArchiveLayout { get; }

		/// <summary>The game's LEV.ARK: its blocks, levels and block lengths (UWLevArkFile; phase 1 step 2).</summary>
		UWLevArkFile LevArk { get; }

		/// <summary>Where the game's program keeps its 3D models (UW3DModelLayout; phase 1 step 5).</summary>
		UW3DModelLayout Models { get; }

		/// <summary>A new rule set with the game's original numbers (section 4.3: a number that differs
		/// between the games is data, each game's default here).</summary>
		UWRules CreateOriginalRules();

		/// <summary>The steps of magic where the games part (IUWMagicVariant).</summary>
		IUWMagicVariant Magic { get; }

		/// <summary>The steps of combat where the games part (IUWCombatVariant).</summary>
		IUWCombatVariant Combat { get; }

		/// <summary>The steps of the player's rules where the games part (IUWPlayerVariant).</summary>
		IUWPlayerVariant Player { get; }

		/// <summary>The steps of the creatures' rules where the games part (IUWCritterVariant).</summary>
		IUWCritterVariant Critter { get; }

		/// <summary>The steps of the items' rules where the games part (IUWObjectVariant).</summary>
		IUWObjectVariant Objects { get; }

		/// <summary>The steps of the traps' rules where the games part (IUWTrapVariant).</summary>
		IUWTrapVariant Traps { get; }

		/// <summary>The steps of the motion rules where the games part (IUWMotionVariant).</summary>
		IUWMotionVariant Motion { get; }

		/// <summary>The steps of the inventory where the games part (IUWInventoryVariant).</summary>
		IUWInventoryVariant Inventory { get; }

		/// <summary>The steps of the conversation rules where the games part (IUWConversationVariant).</summary>
		IUWConversationVariant Conversation { get; }

		/// <summary>The game's trap kinds by id (UseTrap's switch), sealed and built once per profile -
		/// a static of UWTrapRules until phase 0 step 3 (2026-10-28).</summary>
		UWTrapTable TrapKinds { get; }

		/// <summary>The game's a_do actions by quality (do_trap_hack), sealed and built once per profile.</summary>
		UWDoActionTable DoActions { get; }

		/// <summary>The game's conversation functions by import name, sealed and built once per profile
		/// - a static of UWConversationSession until phase 0 step 3.</summary>
		UWConversationFunctionTable ConversationFunctions { get; }
	}
}
