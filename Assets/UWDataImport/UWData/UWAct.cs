namespace UWDataImport.UWData
{
	/// <summary>
	/// EVERY INPUT ACTION OF THE GAME BY NAME (cleanup step 7.4, 2026-10-25): one entry per action of
	/// the three maps UWControls builds - Player, Noclip, Debug -, in the maps' order. The input facade
	/// (UWInput) is asked by these instead of by the struct fields of UWControls; UWActionTable gives
	/// each its map and action name, THE SAME STRINGS AS settings.json's rebinding lines
	/// ("Map|Action|BindingIndex|path"), so no action is renamed and no binding moves. The noclip map's
	/// actions carry its name in front, as Player has a Move and a Look of its own; the debug map's
	/// names are unique as they are.
	/// </summary>
	public enum UWAct
	{
		// The Player map.
		Move,
		Look,
		LookStick,
		CursorDrag,
		Interact,
		ToggleScheme,
		ToggleInventory,
		HoverHeight,
		Turn,
		Strafe,
		Jump,
		LookDown,
		LookReset,
		LookUp,
		ToggleMap,
		ToggleFont,
		ToggleHelp,
		KeyOptions,
		KeyTalk,
		KeyGet,
		KeyLook,
		KeyFight,
		KeyUse,
		KeyPanel,
		KeyCast,
		KeyTrack,
		KeyCamp,
		OptSave,
		OptRestore,
		OptMusic,
		OptSound,
		OptDetail,
		OptQuit,
		EasyForward,
		EasyWalkForward,
		EasyBack,
		EasyTurnLeft,
		EasyTurnRight,
		ModernUse,
		ModernReady,
		WalkSlow,
		EasyModifier,
		ModernHover,
		Menu,
		ModernBags,
		ModernLook,
		ModernRunes,
		ModernAttack,
		PadBack,
		PadJump,
		PadCast,
		PadSlotPrevious,
		PadSlotNext,
		PadSlotUse,
		PadPointer,
		PadClick,
		PadContext,
		ModernSlot1,
		ModernSlot2,
		ModernSlot3,
		ModernSlot4,
		ModernSlot5,
		ModernSlot6,
		ModernSlot7,
		ModernSlot8,
		ModernSlot9,
		ModernSlot10,

		// The Noclip map (the free debug camera).
		NoclipMove,
		NoclipLook,
		NoclipUp,
		NoclipDown,
		NoclipSprint,

		// The Debug map.
		ToggleOverlay,
		TeleportDebug,
		ToggleSpectator,
		NextLevel,
		PrevLevel,
		ToggleBrightness,
		DumpShaderGlobals,
		ForceCast
	}

	/// <summary>
	/// The map and action name of every UWAct (cleanup step 7.4): the strings UWControls builds its
	/// maps with and settings.json stores rebinds under. The self-check holds this table against the
	/// maps as Unity built them (Tools/Fixtures/input/typing-lock.expected.txt, UWInputLockCheck).
	/// </summary>
	public static class UWActionTable
	{
		public const string PlayerMap = "Player";

		public const string NoclipMap = "Noclip";

		public const string DebugMap = "Debug";

		/// <summary>How many actions there are (the last UWAct plus one).</summary>
		public const int Count = (int)UWAct.ForceCast + 1;

		/// <summary>The first act of the Noclip map; before it every act is the Player map's.</summary>
		public const UWAct FirstNoclip = UWAct.NoclipMove;

		/// <summary>The first act of the Debug map.</summary>
		public const UWAct FirstDebug = UWAct.ToggleOverlay;

		/// <summary>The noclip map's acts carry the map's name in front of the action's.</summary>
		private const string NoclipPrefix = "Noclip";

		/// <summary>The map an act belongs to.</summary>
		public static string MapOf(UWAct peAct)
		{
			return peAct >= FirstDebug ? DebugMap : peAct >= FirstNoclip ? NoclipMap : PlayerMap;
		}

		/// <summary>The action's name in its map, as UWControls and settings.json write it.</summary>
		public static string NameOf(UWAct peAct)
		{
			string lsName = peAct.ToString();

			return peAct >= FirstNoclip && peAct < FirstDebug ? lsName.Substring(NoclipPrefix.Length) : lsName;
		}

		/// <summary>The act of a map's action; false for a name no map has.</summary>
		public static bool TryFind(string psMap, string psAction, out UWAct peAct)
		{
			for (int liAct = 0; liAct < Count; liAct++)
			{
				UWAct leAct = (UWAct)liAct;

				if (MapOf(leAct) == psMap && NameOf(leAct) == psAction)
				{
					peAct = leAct;
					return true;
				}
			}

			peAct = UWAct.Move;
			return false;
		}

		/// <summary>The modern action bar's slot (0 to 9) as its act.</summary>
		public static UWAct ModernSlot(int piSlot)
		{
			return UWAct.ModernSlot1 + piSlot;
		}
	}
}
