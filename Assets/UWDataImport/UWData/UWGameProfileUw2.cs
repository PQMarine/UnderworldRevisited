using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S PROFILE (phase 0 step 4, 2026-10-28): what the install code needs to find,
	/// check and extract it, read from GOG's game.gog - the image's folder UW2 with CRIT, CUTS, DATA,
	/// SOUND and UW2.EXE as plain files like the first game's, the GOG build of UW2.EXE by size and
	/// SHA-1, and the save folder UNDEROM2, which the image's UW2.BAT looks for ("sgfind UNDEROM2")
	/// as UW.BAT looks for UNDEROM1.
	///
	/// ITS RULES ARE NOT THERE YET: the rule set, the variants and the tables come domain by domain
	/// in phase 4. Until then asking for them throws, so nothing can play the second game with the
	/// first game's rules by mistake.
	/// </summary>
	public sealed class UWGameProfileUw2 : IUWGameProfile
	{
		public const string ImageFolderName = "UW2";

		public const string ExecutableName = "UW2.EXE";

		/// <summary>The GOG build of UW2.EXE (in game.gog of GOG's Ultima Underworld, product
		/// 1207662463, read 2026-10-28).</summary>
		public const long ExecutableBytes = 675184;

		public const string ExecutableSha1Hex = "41a23de0fa85c375920ae5d0dbd2ec5f4657c758";

		public const string CacheFolderName = "UW2";

		public const string SaveFolderName = "UNDEROM2";

		public const string GameFolderName = "UW2";

		/// <summary>
		/// WHERE A NEW GAME STARTS (UW2 plan phase 2 step 2): level 1, the middle of tile 0x13/0x30 at
		/// its floor's height, facing north. The main menu's new game loads level 1 and calls
		/// player_setup(0x13, 0x30, -1) (Underworld Exhumed uw2 ui/MAINMENU.C:200-203); player_setup
		/// puts the player at (x &lt;&lt; 8) + 0x80 and the floor's height (motion/PHYSICS.C:496);
		/// init_player and create_player set level 1 and heading 0 (game/PLAYER.C:135, game/CHARGEN.C:127).
		/// </summary>
		public const int NewGameLevel = 1;

		public const int NewGameTileX = 0x13;

		public const int NewGameTileY = 0x30;

		public const int NewGameHeading = 0;

		public UWGame Game => UWGame.Uw2;

		public string Title => "Ultima Underworld II: Labyrinth of Worlds";

		public string ImageFolder => ImageFolderName;

		public string Executable => ExecutableName;

		public long ExecutableSize => ExecutableBytes;

		public string ExecutableSha1 => ExecutableSha1Hex;

		public string CacheFolder => CacheFolderName;

		public string SaveFolder => SaveFolderName;

		public string GameFolder => GameFolderName;

		/// <summary>Not yet: its main menu screen only (UWScreensUw2), phase 0 step 5.</summary>
		public bool IsPlayable => false;

		public UWArk.LayoutEnum ArchiveLayout => UWArk.LayoutEnum.Uw2;

		public UWLevArkFile LevArk => UWLevArkFile.Uw2;

		public UW3DModelLayout Models => UW3DModelLayout.Uw2;

		/// <summary>
		/// THE SECOND GAME'S RULE SET (UW2 plan phase 2 step 4, per user 2026-10-29: "Regelsatz jetzt"): the
		/// rule set's own defaults, which are the first game's numbers, until phase 4 holds each domain to
		/// the second game's (Docs/UW2-PLAN.md lists them). What step 4 settled: the motion - its shares
		/// out of 20, the turn, the view's follow and the falls are its variant's (UWMotionVariantUw2);
		/// the base speeds, the turn step, the jump and the fall's Acrobat check (2, 30, above 3) are the
		/// same numbers in both games (uw2 motion/PHYSICS.C:604-610, newFPS), so the defaults hold.
		/// </summary>
		public UWRules CreateOriginalRules()
		{
			return new UWRules();
		}

		public IUWMagicVariant Magic => throw fNotYet();

		public IUWCombatVariant Combat => throw fNotYet();

		public IUWPlayerVariant Player => throw fNotYet();

		public IUWCritterVariant Critter => throw fNotYet();

		public IUWObjectVariant Objects => throw fNotYet();

		public IUWTrapVariant Traps => throw fNotYet();

		public IUWMotionVariant Motion { get; } = new UWMotionVariantUw2();

		public IUWInventoryVariant Inventory { get; } = new UWInventoryVariantUw2();

		public IUWConversationVariant Conversation => throw fNotYet();

		public UWTrapTable TrapKinds => throw fNotYet();

		public UWDoActionTable DoActions => throw fNotYet();

		public UWConversationFunctionTable ConversationFunctions => throw fNotYet();

		private static NotSupportedException fNotYet()
		{
			return new NotSupportedException("The rules of Ultima Underworld II come in phase 4 of Docs/UW2-PLAN.md.");
		}
	}
}
