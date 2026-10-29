namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIRST GAME'S PROFILE (phase 0 step 1, 2026-10-28): the values the code held as literals
	/// of its own until then - UWGogInstall's image folder, program, size, hash, cache folder and
	/// save folder, UWGameFolder's folder name - gathered here, unchanged; those classes read them
	/// from here now. The constants stay public so a constant elsewhere can be built from them.
	/// </summary>
	public sealed class UWGameProfileUw1 : IUWGameProfile
	{
		public const string ImageFolderName = "UW";

		public const string ExecutableName = "UW.EXE";

		/// <summary>The GOG build of UW.EXE.</summary>
		public const long ExecutableBytes = 547248;

		public const string ExecutableSha1Hex = "226fed4dba9718a7c972de5451b520fcd16546c0";

		public const string CacheFolderName = "UW1";

		public const string SaveFolderName = "UNDEROM1";

		public const string GameFolderName = "UW1";

		public UWGame Game => UWGame.Uw1;

		public string Title => "Ultima Underworld: The Stygian Abyss";

		public string ImageFolder => ImageFolderName;

		public string Executable => ExecutableName;

		public long ExecutableSize => ExecutableBytes;

		public string ExecutableSha1 => ExecutableSha1Hex;

		public string CacheFolder => CacheFolderName;

		public string SaveFolder => SaveFolderName;

		public string GameFolder => GameFolderName;

		public bool IsPlayable => true;

		public UWArk.LayoutEnum ArchiveLayout => UWArk.LayoutEnum.Uw1;

		public UWLevArkFile LevArk => UWLevArkFile.Uw1;

		public UW3DModelLayout Models => UW3DModelLayout.Uw1;

		/// <summary>The first game's numbers are the rule set's own defaults.</summary>
		public UWRules CreateOriginalRules()
		{
			return new UWRules();
		}

		public IUWMagicVariant Magic { get; } = new UWMagicVariantUw1();

		public IUWCombatVariant Combat { get; } = new UWCombatVariantUw1();

		public IUWPlayerVariant Player { get; } = new UWPlayerVariantUw1();

		public IUWCritterVariant Critter { get; } = new UWCritterVariantUw1();

		public IUWObjectVariant Objects { get; } = new UWObjectVariantUw1();

		public IUWTrapVariant Traps { get; } = new UWTrapVariantUw1();

		public IUWMotionVariant Motion { get; } = new UWMotionVariantUw1();

		public IUWInventoryVariant Inventory { get; } = new UWInventoryVariantUw1();

		public IUWConversationVariant Conversation { get; } = new UWConversationVariantUw1();

		private UWTrapTable mOTrapKinds;

		private UWDoActionTable mODoActions;

		private UWConversationFunctionTable mOConversationFunctions;

		/// <summary>Built on first use, not with the profile: the tables' builders reach rule classes
		/// whose own static setup must not run while the profiles are being made.</summary>
		public UWTrapTable TrapKinds
		{
			get { return mOTrapKinds ?? (mOTrapKinds = UWTrapRules.CreateTrapTable()); }
		}

		public UWDoActionTable DoActions
		{
			get { return mODoActions ?? (mODoActions = UWTrapRules.CreateDoActionTable()); }
		}

		public UWConversationFunctionTable ConversationFunctions
		{
			get { return mOConversationFunctions ?? (mOConversationFunctions = UWConversationFunctionTable.CreateDefault()); }
		}
	}

	/// <summary>The games' profiles, one immutable instance each (UW2's since phase 0 step 4, with the
	/// install facts only; its rules come in phase 4).</summary>
	public static class UWGameProfiles
	{
		public static readonly IUWGameProfile Uw1 = new UWGameProfileUw1();

		public static readonly IUWGameProfile Uw2 = new UWGameProfileUw2();

		/// <summary>Both games, the first first.</summary>
		public static readonly IUWGameProfile[] All = { Uw1, Uw2 };

		/// <summary>The profile of a game; null for an id without one.</summary>
		public static IUWGameProfile Of(UWGame peGame)
		{
			return peGame == UWGame.Uw1 ? Uw1 : peGame == UWGame.Uw2 ? Uw2 : null;
		}
	}
}
